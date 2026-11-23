using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// Panel "dialogue" (argument: a <see cref="DialogueScript"/>, or a script id). Conversations run in a lower-third box
/// with a name plate and typewriter text; the scripts "intro" and "ending" are told full-screen instead: black, large
/// centred serif lines fading one after another over slowly drifting embers.
/// Click / Space / Enter: finish the line, then go on. Esc: skip the script (press twice during the narrated scenes).
/// Each line's recording is played through <c>Game.I.Audio.PlayVoice</c>; the text never waits for audio to exist, and a
/// finished, read line moves on by itself so a conversation can never stall.
/// </summary>
public partial class DialogueUi : UiPanel
{
    public override string Id => "dialogue";
    public override bool Fullscreen => _cinematic;
    /// <summary>
    /// The narrated scenes stop the world; a talk beside the town brazier lets it breathe on (the hero takes no orders
    /// while any panel is open). Anywhere monsters roam, a conversation pauses the game too.
    /// </summary>
    public override bool Pauses => _cinematic || !(Game.I?.Level?.IsTown ?? false);

    // The box is BoxW wide and sits BoxBottom above the screen edge. BoxH is its least height in design pixels (three
    // lines of speech); it scales with the text and the box grows further upward when a line needs more.
    private const float BoxW = 1400f, BoxH = 236f, BoxBottom = 56f;
    private const float PlateInset = 34f, PlateOverlap = 24f, ShadeMin = 460f, ShadeAbove = 170f;
    // the forged name plate starts clear of the box's corner casting and sits a little more inside the box than out
    private const float ForgedInset = 46f, ForgedSink = 5f;
    /// <summary>點擊或按空白鍵繼續　·　Esc 略過 — in the parts of a hint row: words and key names in turn.</summary>
    private static readonly string[] HintWords = { "點擊或按", "空白鍵", "繼續　·　", "Esc", "略過" };
    private const float Cps = 34f;                  // typewriter speed, characters per second
    private const float AutoDelay = 1.5f;           // pause after a spoken, fully shown line before moving on
    private const float FadeIn = 1.1f, FadeOut = 0.75f, FastOut = 0.3f, CineHold = 1.4f, OutroTime = 1.3f, SkipWindow = 3.5f;
    private const int EmberCount = 72;

    private struct Ember
    {
        public float X, Y, Vx, Vy, Life, Max, Size, Phase, Heat;
    }

    private bool _built, _active, _cinematic, _typed, _voiceDone, _fastOut, _forged, _plateForged;
    /// <summary>[fixes] A conversation is on screen: quest toasts wait for it (Quests.ReleaseHeldToasts).</summary>
    public static bool Talking { get; private set; }
    private DialogueScript _script;
    private int _index, _len, _voiceToken, _phase, _errors;
    private string _lineText = "";
    private float _lineT, _chars, _pause, _auto, _minRead, _voiceWait, _blink, _alpha, _outro, _skipArm, _clock, _emberFade;

    // conversation box
    private TextureRect _shade;
    private PanelContainer _box, _plate;
    private Label _name, _text, _count;
    private Control _arrow;

    // narrated scenes
    private ColorRect _black;
    private Control _embers;
    private VBoxContainer _cineBox;
    private Label _cineSpeaker, _cineText;
    private HBoxContainer _cineHint, _cineHintNormal, _cineHintArmed;
    private readonly Ember[] _em = new Ember[EmberCount];
    private static Texture2D _dot;

    public override void _Ready()
    {
        base._Ready();
        Log.Guard("dialogue", EnsureBuilt);
    }

    // ───────────────────────── construction ─────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        MouseFilter = MouseFilterEnum.Stop;          // a click anywhere moves the conversation on
        WorldUiKit.FullRect(this);

        // ── narrated scenes ──
        _black = new ColorRect { Color = Colors.Black };
        AddChild(_black);
        WorldUiKit.FullRect(_black);

        _embers = new Control { Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
        AddChild(_embers);
        WorldUiKit.FullRect(_embers);
        _embers.Draw += DrawEmbers;

        _cineBox = WorldUiKit.VBox(26);
        _cineBox.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(_cineBox);
        _cineBox.SetAnchorsPreset(LayoutPreset.FullRect);
        _cineBox.OffsetLeft = 250; _cineBox.OffsetRight = -250; _cineBox.OffsetTop = 150; _cineBox.OffsetBottom = -190;
        _cineSpeaker = UiTheme.Label("", 30, UiTheme.Gold, true);
        _cineSpeaker.HorizontalAlignment = HorizontalAlignment.Center;
        _cineBox.AddChild(_cineSpeaker);
        _cineText = UiTheme.Label("", 48, new Color(0.94f, 0.89f, 0.79f), true);
        _cineText.HorizontalAlignment = HorizontalAlignment.Center;
        _cineText.VerticalAlignment = VerticalAlignment.Center;
        _cineText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _cineText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _cineText.AddThemeConstantOverride("line_spacing", 16);
        _cineText.AddThemeColorOverride("font_shadow_color", new Color(0.9f, 0.35f, 0.08f, 0.28f));
        _cineText.AddThemeConstantOverride("shadow_offset_x", 0);
        _cineText.AddThemeConstantOverride("shadow_offset_y", 3);
        _cineBox.AddChild(_cineText);

        // the prompt of the narrated scenes, bottom right, with its keys in key caps: the usual one, and the one that
        // asks for the second Esc (only one of the two shows, see UpdateHints)
        _cineHint = WorldUiKit.HBox(0);
        _cineHint.Alignment = BoxContainer.AlignmentMode.End;
        AddChild(_cineHint);
        // stands on its bottom edge and grows upward, so the line keeps its place whatever the text size
        _cineHint.AnchorLeft = 0f; _cineHint.AnchorRight = 1f; _cineHint.AnchorTop = 1f; _cineHint.AnchorBottom = 1f;
        _cineHint.OffsetLeft = 60; _cineHint.OffsetRight = -60; _cineHint.OffsetTop = -40; _cineHint.OffsetBottom = -40;
        _cineHint.GrowVertical = GrowDirection.Begin;
        _cineHintNormal = WorldUiKit.HintRow(UiTheme.Dim, HintWords);
        _cineHint.AddChild(_cineHintNormal);
        _cineHintArmed = WorldUiKit.HintRow(UiTheme.Gold, "再按一次", "Esc", "，略過這段敘事");
        _cineHintArmed.Visible = false;
        _cineHint.AddChild(_cineHintArmed);

        // ── conversation box ──
        var fade = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color(0f, 0f, 0f, 0f), new Color(0f, 0f, 0f, 0.84f) } };
        _shade = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = fade, Width = 8, Height = 128, Fill = GradientTexture2D.FillEnum.Linear, FillFrom = new Vector2(0.5f, 0f), FillTo = new Vector2(0.5f, 1f) },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        AddChild(_shade);
        _shade.AnchorLeft = 0f; _shade.AnchorRight = 1f; _shade.AnchorTop = 1f; _shade.AnchorBottom = 1f;
        _shade.OffsetLeft = 0; _shade.OffsetRight = 0; _shade.OffsetTop = -ShadeMin; _shade.OffsetBottom = 0;

        // The box hangs from the bottom edge with no height of its own in its offsets: its minimum height (BoxH, scaled
        // with the text) and the wrapped line decide how far it reaches upward, so a long line at the largest text
        // size makes the box taller instead of being cut off.
        // With the art the box is always the compact heavy moulding with the small crest: the shared window frame
        // would switch to the full window moulding and its large crest whenever a long line made the box tall.
        _forged = UiTheme.Art && UiArt.Has("modal");
        if (_forged)
        {
            _box = new PanelContainer();
            _box.AddThemeStyleboxOverride("panel", UiTheme.ArtBox("dialogue", () =>
                new UiNine("modal", "slate", 5f, 32f, 26f) { Top = WorldUiKit.Piece("crest_small"), TopMinWidth = 300f }));
        }
        else _box = UiTheme.Frame(true) ?? new PanelContainer();
        AddChild(_box);
        _box.AnchorLeft = 0.5f; _box.AnchorRight = 0.5f; _box.AnchorTop = 1f; _box.AnchorBottom = 1f;
        _box.OffsetLeft = -BoxW * 0.5f; _box.OffsetRight = BoxW * 0.5f; _box.OffsetTop = -BoxBottom; _box.OffsetBottom = -BoxBottom;
        _box.GrowHorizontal = GrowDirection.Both; _box.GrowVertical = GrowDirection.Begin;
        UiTheme.MinSize(_box, 0f, BoxH);
        var margin = WorldUiKit.Margin(18, 22, 18, 0);       // on top of the frame's own padding; clears the name plate
        _box.AddChild(margin);
        var col = WorldUiKit.VBox(8);
        margin.AddChild(col);
        _text = UiTheme.Label("", 28, UiTheme.Text);         // the game's subtitle text
        _text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _text.VerticalAlignment = VerticalAlignment.Top;
        _text.SizeFlagsVertical = SizeFlags.ExpandFill;
        _text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _text.VisibleCharactersBehavior = TextServer.VisibleCharactersBehavior.CharsAfterShaping;
        _text.AddThemeConstantOverride("line_spacing", 8);
        col.AddChild(_text);
        var foot = WorldUiKit.HBox(14);
        col.AddChild(foot);
        // 點擊或按空白鍵繼續　·　Esc 略過 — the two keys in key caps
        foot.AddChild(WorldUiKit.HintRow(UiTheme.Dim, HintWords));
        foot.AddChild(WorldUiKit.Spacer());
        _count = UiTheme.Label("", UiTheme.FsCaption, UiTheme.Dim);
        _count.VerticalAlignment = VerticalAlignment.Center;
        foot.AddChild(_count);
        // the "go on" mark: the forged chevron (a gold ▼ without the art), blinking once the line is complete
        var chevron = UiTheme.Art && UiArt.Has("arrow_down") ? UiArt.Icon("arrow_down") : null;
        if (chevron != null)
        {
            _arrow = UiTheme.MinSize(new TextureRect
            {
                Texture = chevron, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            }, 38f, 34f);
        }
        else
        {
            var mark = UiTheme.MinSize(UiTheme.Label("▼", UiTheme.FsRow, UiTheme.Gold), 30f, 0f);
            mark.HorizontalAlignment = HorizontalAlignment.Center;
            mark.VerticalAlignment = VerticalAlignment.Center;
            _arrow = mark;
        }
        foot.AddChild(_arrow);

        // the name plate straddles the top edge of the box and is as wide and as tall as the name: it has no size of
        // its own either, and PlacePlate keeps it on the box's top edge wherever the text has pushed that edge.
        // With the art it is the forged plate with pointed ends (the name between its two studs).
        bool plateArt = _plateForged = UiTheme.Art && UiArt.Has("nameplate");
        _plate = new PanelContainer();
        _plate.AddThemeStyleboxOverride("panel", WorldUiKit.NamePlateBox());
        AddChild(_plate);
        _plate.AnchorLeft = 0.5f; _plate.AnchorRight = 0.5f; _plate.AnchorTop = 1f; _plate.AnchorBottom = 1f;
        _plate.GrowHorizontal = GrowDirection.End; _plate.GrowVertical = GrowDirection.Begin;
        var plateMargin = plateArt ? WorldUiKit.Margin(0, 0, 0, 0) : WorldUiKit.Margin(10, 0, 10, 0);
        _plate.AddChild(plateMargin);
        _name = UiTheme.Label("", 30, UiTheme.Gold, true);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        plateMargin.AddChild(_name);
        _box.ItemRectChanged += PlacePlate;
        PlacePlate();

        IgnoreMouse(this);
        ShowMode(false, false);
    }

    /// <summary>
    /// Puts the name plate on the top edge of the box and lets the shade reach a little above it. The box grows upward
    /// with its text and with the text scale, so both follow the box's real rectangle, not fixed offsets. All three
    /// hang from the bottom centre of the panel, which is where the positions are measured from.
    /// </summary>
    private void PlacePlate()
    {
        if (_box == null || _plate == null || _shade == null) return;
        Vector2 area = Size;
        float top, left;
        if (area.X > 1f && area.Y > 1f)
        {
            top = _box.Position.Y - area.Y;
            left = _box.Position.X - area.X * 0.5f;
        }
        else
        {
            // not laid out yet: where the box will stand once it is
            top = -(MathF.Max(UiTheme.Px(BoxH), _box.GetCombinedMinimumSize().Y) + BoxBottom);
            left = -BoxW * 0.5f;
        }
        float x = MathF.Round(left + PlateInset), y = MathF.Round(top + PlateOverlap);
        if (_plateForged)
        {
            // the forged plate lies across the box's top moulding, whatever height the text size gives it
            x = MathF.Round(left + ForgedInset);
            y = MathF.Round(top + _plate.GetCombinedMinimumSize().Y * 0.5f + ForgedSink);
        }
        if (_plate.OffsetLeft != x || _plate.OffsetRight != x) { _plate.OffsetLeft = x; _plate.OffsetRight = x; }
        if (_plate.OffsetTop != y || _plate.OffsetBottom != y) { _plate.OffsetTop = y; _plate.OffsetBottom = y; }
        float shade = MathF.Round(MathF.Min(-ShadeMin, top - ShadeAbove));
        if (_shade.OffsetTop != shade) _shade.OffsetTop = shade;
    }

    /// <summary>The text size changed: the box has a new height, so the plate and the shade are put back on its edge.</summary>
    public override void OnTextScale()
    {
        if (_built) Log.Guard("dialogue", PlacePlate);
    }

    private static void IgnoreMouse(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is Control ctl) ctl.MouseFilter = MouseFilterEnum.Ignore;
            IgnoreMouse(c);
        }
    }

    private void ShowMode(bool box, bool cinema)
    {
        _shade.Visible = _box.Visible = box;
        _plate.Visible = box && _name.Text.Length > 0;
        _black.Visible = _embers.Visible = _cineBox.Visible = _cineHint.Visible = cinema;
    }

    private static Texture2D Dot()
    {
        if (_dot != null) return _dot;
        var g = new Gradient
        {
            Offsets = new[] { 0f, 0.35f, 1f },
            Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.35f), new Color(1f, 1f, 1f, 0f) },
        };
        _dot = new GradientTexture2D
        {
            Gradient = g, Width = 64, Height = 64, Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
        };
        return _dot;
    }

    // ───────────────────────── panel life cycle ─────────────────────────

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        WorldUiKit.FullRect(this);
        var s = arg as DialogueScript;
        if (s == null && arg is string id)
        {
            try { s = DialogueDb.Get(id); }
            catch (Exception e) { Log.Error("dialogue", $"script '{id}' could not be fetched", e); }
        }

        if (_active)
        {
            // a new script replaces a running one: the old one still gets its ending, after this call has returned
            var old = _script;
            _active = false; _script = null; _voiceToken++;
            Log.Info("dialogue", $"script '{old?.Id}' interrupted by '{s?.Id}'");
            EndLater(old);
        }

        if (s == null || s.Lines == null || s.Lines.Count == 0)
        {
            Log.Warn("dialogue", $"opened with nothing to say (arg={arg ?? "null"}); closing");
            ShowMode(false, false);
            Callable.From(() =>
            {
                if (_active) return;
                WorldUiKit.ClosePanel(Id);
                if (s?.OnEnd != null) Log.Guard("dialogue", s.OnEnd);
            }).CallDeferred();
            return;
        }

        _script = s;
        _active = true; Talking = true;
        _index = 0;
        _cinematic = s.Id == "intro" || s.Id == "ending";
        _skipArm = 0f; _outro = 0f; _phase = 0; _alpha = 0f; _clock = 0f; _emberFade = 0f; _blink = 0f; _errors = 0;
        if (_cinematic)
        {
            _black.Color = Colors.Black;
            _cineBox.Modulate = new Color(1, 1, 1, 0);
            for (int i = 0; i < _em.Length; i++) Spawn(ref _em[i], true);
        }
        ShowMode(!_cinematic, _cinematic);
        UpdateHints();
        Log.Info("dialogue", $"script '{s.Id}' started: {s.Lines.Count} lines, {(_cinematic ? "narrated" : "conversation")}");
        ShowLine();
        PlacePlate();
    }

    public override void OnClose()
    {
        if (_active)
        {
            // closed from outside (the panel manager's Esc, CloseAll, an area change): the script still ends properly
            var s = _script;
            int at = _index, n = s?.Lines?.Count ?? 0;
            _active = false; _script = null; _voiceToken++;
            StopVoice();
            Log.Info("dialogue", $"script '{s?.Id}' closed at line {Math.Min(at + 1, n)}/{n}");
            EndLater(s);
        }
        if (_built) ShowMode(false, false);
        _cinematic = false;
        Talking = false;
        Quests.ReleaseHeldToasts();             // [fixes] 「目標更新」 and the like, now that the lines have been read
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        Talking = false;
        if (!_active) return;
        _active = false; _script = null; _voiceToken++;
        StopVoice();
    }

    /// <summary>Runs a script's OnEnd on the next idle frame, and only while the game is still being played.</summary>
    private static void EndLater(DialogueScript s)
    {
        if (s?.OnEnd == null) return;
        Callable.From(() =>
        {
            var g = Game.I;
            if (g != null && g.State == Game.GameState.Play) Log.Guard("dialogue", s.OnEnd);
            else Log.Info("dialogue", $"script '{s.Id}': ending dropped, the game is no longer in play");
        }).CallDeferred();
    }

    private void Finish(bool skipped)
    {
        if (!_active) return;
        var s = _script;
        int at = _index, n = s?.Lines?.Count ?? 0;
        _active = false; _script = null; _voiceToken++;
        StopVoice();
        Log.Info("dialogue", $"script '{s?.Id}' {(skipped ? "skipped" : "finished")} at line {Math.Min(at + 1, n)}/{n}");
        WorldUiKit.ClosePanel(Id);              // OnClose sees an inactive panel and only hides it
        if (s?.OnEnd != null) Log.Guard("dialogue", s.OnEnd);
    }

    // ───────────────────────── lines ─────────────────────────

    private void ShowLine()
    {
        var lines = _script.Lines;
        while (_index < lines.Count && (lines[_index] == null || string.IsNullOrEmpty(lines[_index].Text))) _index++;
        if (_index >= lines.Count) { EndOfScript(); return; }

        var line = lines[_index];
        _lineText = line.Text;
        _len = _lineText.Length;
        _lineT = 0f; _auto = 0f; _chars = 0f; _pause = 0f; _typed = false; _fastOut = false;
        _minRead = 0.9f + 0.15f * _len;

        string speaker = line.Speaker ?? "";
        string voice = line.Voice ?? "";
        bool hero = voice.StartsWith("hero_", StringComparison.Ordinal), boss = voice.StartsWith("boss_", StringComparison.Ordinal);
        if (hero)
        {
            string heroName = Game.I?.Hero?.D.Name;
            if (!string.IsNullOrEmpty(heroName)) speaker = heroName;
        }
        Color nameCol = boss ? new Color(1f, 0.45f, 0.32f) : hero ? Names.ClassColor(Game.I?.Hero?.Class ?? ClassId.Emberblade).Lerp(Colors.White, 0.25f) : UiTheme.Gold;

        if (_cinematic)
        {
            _cineSpeaker.Text = speaker.Length > 0 ? $"—　{speaker}　—" : "";
            _cineSpeaker.Visible = speaker.Length > 0;
            _cineSpeaker.AddThemeColorOverride("font_color", nameCol);
            _cineText.Text = _lineText;
            _cineText.AddThemeFontSizeOverride("font_size", UiTheme.Fs(_len > 72 ? 36 : _len > 46 ? 42 : 48));      // long lines step down, never below 36
            _phase = 0; _alpha = 0f;
            _cineBox.Modulate = new Color(1, 1, 1, 0);
        }
        else
        {
            _name.Text = speaker;
            _name.AddThemeColorOverride("font_color", nameCol);
            _plate.Visible = speaker.Length > 0;
            _text.Text = _lineText;
            _text.VisibleRatio = 0f;
            _text.AddThemeColorOverride("font_color", speaker.Length > 0 ? UiTheme.Text : new Color(0.84f, 0.78f, 0.66f));      // the narrator speaks in parchment
            _count.Text = $"{_index + 1} / {lines.Count}";
            _arrow.Modulate = new Color(1, 1, 1, 0);
        }
        StartVoice(line);
    }

    private void StartVoice(DialogueLine line)
    {
        _voiceDone = false;
        int token = ++_voiceToken;
        bool sent = false;
        if (!string.IsNullOrEmpty(line.Voice))
        {
            try
            {
                var audio = Game.I?.Audio;
                if (audio != null)
                {
                    audio.PlayVoice(line.Voice, () => { if (token == _voiceToken) _voiceDone = true; });
                    sent = true;
                }
            }
            catch (Exception e) { Log.Warn("dialogue", $"voice '{line.Voice}' could not be played: {e.Message}"); }
        }
        // without a voice system the line simply stays for a reading time; with one, this is only a safety net
        _voiceWait = sent ? 6f + 0.5f * _len : 1.2f + 0.2f * _len;
    }

    private static void StopVoice()
    {
        try { Game.I?.Audio?.StopVoice(); }
        catch (Exception e) { Log.Warn("dialogue", "stopping the voice failed: " + e.Message); }
    }

    private void CompleteLine()
    {
        _typed = true;
        _chars = _len;
        _text.VisibleRatio = 1f;
        _auto = 0f;
    }

    private void Advance()
    {
        _index++;
        if (_script == null || _index >= _script.Lines.Count) EndOfScript();
        else ShowLine();
    }

    private void EndOfScript()
    {
        if (_cinematic) StartOutro();
        else Finish(false);
    }

    private void StartOutro()
    {
        if (_phase == 3) return;
        _phase = 3; _outro = 0f; _skipArm = 0f;
        _voiceToken++;
        StopVoice();
        UpdateHints();          // no more prompts while the dark lifts
    }

    /// <summary>Click, Space or Enter: the first press completes the line, the next one moves on.</summary>
    private void Next()
    {
        if (!_active) return;
        if (_cinematic)
        {
            if (_phase == 0) { _alpha = 1f; _phase = 1; _auto = 0f; }
            else if (_phase == 1) { _phase = 2; _fastOut = true; WorldUiKit.Sfx("ui_click", 0.4f); }
            else if (_phase == 2) _fastOut = true;
            return;
        }
        if (!_typed) CompleteLine();
        else { WorldUiKit.Sfx("ui_click", 0.5f); Advance(); }
    }

    /// <summary>Esc: conversations end at once; the narrated scenes ask for a second press first.</summary>
    private void Skip()
    {
        if (!_active) return;
        if (!_cinematic) { Finish(true); return; }
        if (_phase == 3) return;
        if (_skipArm > 0f)
        {
            Log.Info("dialogue", $"script '{_script?.Id}' skipped by the player at line {_index + 1}");
            StartOutro();
            _skipArm = 0f;
            UpdateHints();
            return;
        }
        _skipArm = SkipWindow;
        UpdateHints();
    }

    private void UpdateHints()
    {
        // the conversation box always shows 點擊或按空白鍵繼續　·　Esc 略過 (built once, see EnsureBuilt); the narrated
        // scenes show the same line, 再按一次 Esc，略過這段敘事 in gold while a second Esc is awaited, and nothing
        // while the dark lifts
        bool armed = _skipArm > 0f;
        _cineHintArmed.Visible = armed;
        _cineHintNormal.Visible = !armed && _phase != 3;
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!_active) return;
        try
        {
            float dt = MathF.Min((float)delta, 0.1f);
            _lineT += dt; _blink += dt; _clock += dt;
            if (_skipArm > 0f)
            {
                _skipArm -= dt;
                if (_skipArm <= 0f) UpdateHints();
            }
            if (!_voiceDone)
            {
                _voiceWait -= dt;
                if (_voiceWait <= 0f) _voiceDone = true;
            }
            if (_cinematic) TickCinema(dt); else TickBox(dt);
        }
        catch (Exception e)
        {
            Log.Error("dialogue", "dialogue update failed", e);
            if (++_errors >= 3) Log.Guard("dialogue", () => Finish(true));       // never trap the player inside a broken conversation
        }
    }

    private void TickBox(float dt)
    {
        PlacePlate();           // nothing to do unless the box has moved (a longer line, another text size)
        if (!_typed)
        {
            if (_pause > 0f) { _pause -= dt; return; }
            int from = (int)_chars;
            _chars += Cps * dt;
            int to = Math.Min((int)_chars, _len);
            for (int i = from; i < to; i++)             // a breath after punctuation
            {
                char ch = _lineText[i];
                if (ch == '。' || ch == '！' || ch == '？' || ch == '…') { _pause = 0.2f; _chars = i + 1; break; }
                if (ch == '，' || ch == '、' || ch == '；' || ch == '：' || ch == '—') { _pause = 0.08f; _chars = i + 1; break; }
            }
            if (_chars >= _len) CompleteLine();
            else _text.VisibleRatio = _len > 0 ? _chars / _len : 1f;
            return;
        }
        _arrow.Modulate = new Color(1, 1, 1, 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(_blink * 5f)));
        if (!_voiceDone || _lineT < _minRead) return;
        _auto += dt;
        if (_auto >= AutoDelay) Advance();
    }

    private void TickCinema(float dt)
    {
        _emberFade = MathF.Min(1f, _emberFade + dt / 2.5f);
        switch (_phase)
        {
            case 0:
                _alpha = MathF.Min(1f, _alpha + dt / FadeIn);
                if (_alpha >= 1f) { _phase = 1; _auto = 0f; }
                break;
            case 1:
                if (_voiceDone && _lineT >= _minRead)
                {
                    _auto += dt;
                    if (_auto >= CineHold) _phase = 2;
                }
                break;
            case 2:
                _alpha -= dt / (_fastOut ? FastOut : FadeOut);
                if (_alpha <= 0f)
                {
                    _alpha = 0f;
                    _cineBox.Modulate = new Color(1, 1, 1, 0);
                    Advance();
                    if (!_active) return;
                }
                break;
            default:        // outro: the dark lifts and the world returns
                _outro += dt;
                float k = Math.Clamp(1f - _outro / OutroTime, 0f, 1f);
                _alpha = MathF.Min(_alpha, k);
                _emberFade = MathF.Min(_emberFade, k);
                _black.Color = new Color(0f, 0f, 0f, k);
                if (_outro >= OutroTime) { Finish(false); return; }
                break;
        }
        float eased = _alpha * _alpha * (3f - 2f * _alpha);
        _cineBox.Modulate = new Color(1, 1, 1, eased);

        var size = Area();
        for (int i = 0; i < _em.Length; i++)
        {
            ref var e = ref _em[i];
            e.Life += dt;
            e.X += (e.Vx + MathF.Sin(_clock * 0.6f + e.Phase) * 11f) * dt;
            e.Y += e.Vy * dt;
            if (e.Life >= e.Max || e.Y < -40f || e.X < -60f || e.X > size.X + 60f) Spawn(ref e, false);
        }
        _embers.QueueRedraw();
    }

    /// <summary>The area the embers drift across: the panel, or the whole view before the panel has been laid out.</summary>
    private Vector2 Area()
    {
        var s = Size;
        if (s.X >= 64f && s.Y >= 64f) return s;
        return IsInsideTree() ? GetViewportRect().Size : new Vector2(1920f, 1080f);
    }

    private void Spawn(ref Ember e, bool anywhere)
    {
        var size = Area();
        float w = size.X, h = size.Y;
        e.X = Rng.Range(0f, w);
        e.Y = anywhere ? Rng.Range(0f, h) : h + Rng.Range(10f, 90f);
        e.Vx = Rng.Range(-9f, 9f);
        e.Vy = -Rng.Range(14f, 46f);
        e.Max = Rng.Range(7f, 16f);
        e.Life = anywhere ? Rng.Range(0f, e.Max * 0.8f) : 0f;
        e.Size = Rng.Range(10f, 34f);
        e.Phase = Rng.Range(0f, Mathf.Tau);
        e.Heat = Rng.F();
    }

    private void DrawEmbers()
    {
        if (!_active || !_cinematic || _emberFade <= 0.001f) return;
        try
        {
            var tex = Dot();
            var size = _embers.Size;
            // the glow of a fire somewhere below the frame
            _embers.DrawTextureRect(tex, new Rect2(size.X * 0.5f - 1000f, size.Y - 300f, 2000f, 620f), false, new Color(1f, 0.3f, 0.07f, 0.11f * _emberFade));
            for (int i = 0; i < _em.Length; i++)
            {
                ref var e = ref _em[i];
                float life = Math.Clamp(e.Life / e.Max, 0f, 1f);
                float a = MathF.Sin(life * MathF.PI) * (0.55f + 0.45f * MathF.Sin(_clock * 5f + e.Phase * 3f)) * _emberFade;
                if (a <= 0.01f) continue;
                var col = new Color(1f, 0.34f + 0.42f * e.Heat, 0.07f + 0.28f * e.Heat, a * 0.85f);
                _embers.DrawTextureRect(tex, new Rect2(e.X - e.Size * 0.5f, e.Y - e.Size * 0.5f, e.Size, e.Size), false, col);
                _embers.DrawTextureRect(tex, new Rect2(e.X - e.Size * 0.14f, e.Y - e.Size * 0.14f, e.Size * 0.28f, e.Size * 0.28f), false, new Color(1f, 0.9f, 0.7f, a));
            }
        }
        catch (Exception ex)
        {
            if (_errors++ < 3) Log.Error("dialogue", "drawing embers failed", ex);
        }
    }

    // ───────────────────────── input ─────────────────────────

    public override void _GuiInput(InputEvent e)
    {
        if (_active && e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            AcceptEvent();
            Log.Guard("dialogue", Next);
            return;
        }
        base._GuiInput(e);
    }

    public override void _Input(InputEvent e)
    {
        if (_active && e is InputEventKey k && k.Pressed)
        {
            switch (k.Keycode)
            {
                case Key.Space: case Key.Enter: case Key.KpEnter:
                    if (!k.Echo) Log.Guard("dialogue", Next);
                    GetViewport()?.SetInputAsHandled();
                    return;
                case Key.Escape:
                    if (!k.Echo) Log.Guard("dialogue", Skip);
                    GetViewport()?.SetInputAsHandled();
                    return;
                case Key.I: case Key.C: case Key.K: case Key.P: case Key.M: case Key.Tab: case Key.J: case Key.T:
                    GetViewport()?.SetInputAsHandled();          // panel hotkeys wait until the conversation is over
                    return;
            }
        }
        base._Input(e);
    }
}
