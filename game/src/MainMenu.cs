using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// The title screen. Behind the title, load, settings and credits screens: a close-up of the final boss lurking in
/// the dark (MainMenu.Lair.cs). During character creation a stage of our own — a great brazier among standing stones,
/// embers, falling ash and fog — shows the chosen hero; both are rendered into SubViewports behind the interface and
/// cross-fade. Screens: title, class choice, name / hardcore / slot, load, settings and credits.
/// Game adds this control while in the Menu state; it starts and loads games through <see cref="Game"/>.
/// Text: every font size here is a DESIGN size (px at 1080p, never below 20) that passes through
/// <see cref="UiTheme.Fs"/> exactly once — MenuKit's widgets take the finished size, so their call sites say
/// UiTheme.Fs(n). Boxes that must grow with the text use <see cref="UiTheme.MinSize"/>; panes whose content can
/// outgrow them scroll. <see cref="OnTextScale"/> answers a change of the text-size setting.
/// </summary>
public partial class MainMenu : Control
{
    private enum Page { Title, ClassPick, Identity, Load, Settings, Credits }

    // layout in the 1920×1080 design space (the viewport grows on other aspect ratios, panes stay anchored to the edges).
    // The panes are sized for the larger text: the class sheet is 680 wide and all three start 48 px below the top edge.
    private const float Edge = 70f, ListW = 380f, IdentW = 610f, InfoW = 680f, PaneTop = 48f, PaneBottom = 116f;
    // a save-slot card on the load screen: its width is fixed (four fit side by side), its body grows with the text
    private const float CardW = 424f, CardBodyH = 336f;
    // the 3D stage
    private static readonly Vector3 Stage = new(2.0f, 0f, 6.3f);       // where the hero stands, in front of the brazier
    private static readonly Vector3 FirePos = new(0f, 2.95f, 0f);
    private const float StageH = 0.22f;

    private bool _built, _busy, _faulted, _wasVisible = true;
    private int _faults;
    private Page _page = Page.Title;
    private float _t;
    private List<SlotInfo> _slots = new();

    // layers
    private Control _pages, _modalLayer;
    private ColorRect _dim, _fade;
    private TextureRect _scrim;
    private float _dimNow, _dimTarget, _scrimNow = 1f, _scrimTarget = 1f;
    private MenuModal _modal;
    private Control _focusBeforeModal;

    // title
    private Control _titleRoot;
    private Button _continueBtn;
    private Label _continueNote;
    private TextureRect _titleGlow;
    private Label _logoLabel, _latinLabel;
    private Font _logoFont, _latinFont;                 // the two title faces: their letter-spacing follows the text scale
    private Control _crest, _titleGap;                  // the Sun-Forge emblem over the name; the gap between the lockup and the menu
    private bool _titleAxis;                            // the title is the centred lockup of the raster art (not the old left-edge layout)
    private UiArt.Piece _emblem;
    private int _continueSlot;
    private readonly List<Button> _entries = new();

    // character creation
    private Control _newRoot, _classPane, _identPane, _classBar, _identBar, _dragArea;
    private Button _nextBtn, _startBtn;
    private readonly Button[] _classBtns = new Button[Names.ClassCount];
    private ClassId _cls = ClassId.Emberblade;
    private float _clsPickedAt = -10f;
    private bool _infoFilled;
    private Label _ciName, _ciRole, _ciDesc, _ciResName, _ciResNote, _ciStat, _ciDiff, _ciDiffNote, _dragHint, _plate;
    private Label _ciWeapons;          // SYSTEMS: 可用武器 lines from ItemDb.LoadoutLines (the same table as tooltips and loot)
    private readonly Control[] _skRow = new Control[3];
    private readonly PanelContainer[] _skFrame = new PanelContainer[3];
    private readonly TextureRect[] _skIcon = new TextureRect[3];
    private readonly Label[] _skName = new Label[3], _skMeta = new Label[3], _skDesc = new Label[3];
    private readonly Control[] _spRow = new Control[3];
    private readonly Label[] _spName = new Label[3], _spDesc = new Label[3];
    private LineEdit _nameEdit;
    private Label _nameMsg, _slotMsg, _idClass, _hcText;
    private Button _hcCheck;
    private PanelContainer _hcPanel;
    private bool _hardcore, _nameAuto = true;
    private int _newSlot = 1, _suggest;
    private readonly Button[] _slotBtns = new Button[SaveSystem.SlotCount];

    // load
    private Control _loadRoot;
    private GridContainer _loadGrid;
    private Button _loadBack;
    private int _focusSlot;
    private readonly Dictionary<int, Button> _loadPrimary = new();

    // settings and credits
    private Control _settingsRoot, _creditsRoot;
    private SettingsView _settings;
    private ScrollContainer _creditsScroll;
    private Button _creditsBack;
    private float _creditsPos, _creditsHold;

    // backdrop
    private SubViewport _vp;
    private TextureRect _vpRect;
    private Node3D _scene, _heroPivot, _heroFallback;
    private Camera3D _cam;
    private CameraAttributesPractical _camAttr;
    private Godot.Environment _env;
    private OmniLight3D _fireLight;
    private SpotLight3D _keyLight, _rimLight, _fillLight, _rim2Light;
    private StandardMaterial3D _coalMat, _ringMat, _fallbackMat;
    private GpuParticles3D _swapBurst;
    private ParticleProcessMaterial _swapBurstMat;
    private CharModel _hero;
    private int _heroClass = -1;
    private const float HeroFrontYaw = 0.45f;      // about 26° off front: the three-quarter view the stage was framed for
    private float _swayT;
    private float _heroHeight = 1.8f, _heroYaw = 0.45f, _spinHold, _heroIdleAt = -1f, _heroFrac = 0.43f, _heroFracTarget = 0.43f;
    private float _camBlend, _camBlendTarget, _rimPulse;
    private Color _rimColor = Palette.Ember, _rimTarget = Palette.Ember;
    private Vector3 _fireLightPos;
    private Vector2I _winSize, _vpSize = new(1920, 1080);
    private int _quality = -1;
    private float _scaleCfg = -1f;
    private AudioStreamPlayer _crackle;

    // ───────────────────────── lifecycle ─────────────────────────

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
        var theme = MenuKit.Theme;
        if (theme != null) Theme = theme;

        Log.Guard("menu", BuildBackdrop);
        Log.Guard("menu", BuildChrome);
        Log.Guard("menu", () =>
        {
            _titleRoot = BuildTitle();
            _pages.AddChild(_titleRoot);
        });
        _built = _pages != null && _titleRoot != null;
        if (!_built) { Log.Error("menu", "the title screen could not be built"); return; }

        Log.Guard("menu", BuildCrackle);
        Go(Page.Title, true);
        FadeIn(1.8f);
        Log.Info("menu", $"title screen ready: {_slots.Count(s => s.Exists)} of {SaveSystem.SlotCount} slots used, continue slot={_continueSlot}");

        // test harness: --menu=title|newgame|class:<key>|name|load|settings|credits opens that screen straight away
        string page = Game.I?.ArgValue("menu");
        if (!string.IsNullOrWhiteSpace(page))
            Callable.From(() => { Log.Info("menu", $"--menu: opening '{page}'"); Log.Guard("menu", () => Open(page)); }).CallDeferred();
    }

    public override void _EnterTree()
    {
        UiTheme.TextScaleChanged -= OnTextScale;          // never twice, however often the screen enters the tree
        UiTheme.TextScaleChanged += OnTextScale;
    }

    public override void _ExitTree()
    {
        UiTheme.TextScaleChanged -= OnTextScale;
        _settings?.Flush();
    }

    /// <summary>
    /// The player changed 介面文字大小 (on the settings sheet shown inside this screen, or anywhere else while the
    /// title screen exists). Every font size, [font_size] tag and <see cref="UiTheme.MinSize"/> box below this control
    /// is brought to the new scale; then the few things this screen derives from text sizes by hand are redone.
    /// Screens that have not been opened yet are built later and take the new sizes from <see cref="UiTheme.Fs"/>.
    /// </summary>
    private void OnTextScale(float oldScale)
    {
        if (!_built) return;
        UiTheme.Rescale(this, oldScale);
        Log.Guard("menu", TuneTitleFonts);
        Log.Guard("menu", RenewHardcoreCheck);
        Log.Guard("menu", () => _settings?.FitSheet());
        if (_creditsScroll != null && IsInstanceValid(_creditsScroll))
        {
            // the roll has a new length: start it over instead of keeping a position measured in the old sizes
            _creditsPos = 0f; _creditsHold = 0.4f;
            _creditsScroll.ScrollVertical = 0;
        }
        Log.Info("menu", $"title screen rescaled: text scale ×{oldScale:0.00} -> ×{UiTheme.TextScale:0.00}");
    }

    public override void _Process(double delta)
    {
        if (!_built || _faulted) return;
        try { Tick((float)delta); }
        catch (Exception e)
        {
            Log.Error("menu", "title screen frame failed", e);
            if (++_faults > 5) { _faulted = true; Log.Error("menu", "title screen animation stopped after repeated failures"); }
        }
    }

    private ScrollContainer _infoScroll;     // [fixes] the class sheet and its 「▼ 捲動看更多」 cue
    private Control _infoMore;

    private void Tick(float dt)
    {
        if (_infoScroll != null && _infoMore != null)
        {
            var bar = _infoScroll.GetVScrollBar();
            _infoMore.Visible = bar != null && bar.MaxValue - bar.Page - bar.Value > 2.0;
        }
        bool vis = IsVisibleInTree();
        if (vis != _wasVisible)
        {
            _wasVisible = vis;
            if (_crackle != null) { if (vis) { if (!_crackle.Playing) _crackle.Play(); } else _crackle.Stop(); }
            if (vis)
            {
                // Game brought the title screen back (instead of creating a new one): start clean
                _busy = false;
                _modal?.Close();
                _modal = null;
                Go(Page.Title, true);
                FadeIn(1.2f);
                // back from 展示模式: the focus goes back where it was (after FocusDefault's own deferred call)
                var back = _demoFocus;
                _demoFocus = null;
                _idleT = 0f;
                if (back != null)
                    Callable.From(() => { if (_modal == null && IsInstanceValid(back) && back.IsVisibleInTree() && back.FocusMode != FocusModeEnum.None) back.GrabFocus(); }).CallDeferred();
            }
        }
        GateBackdrops(vis);
        if (!vis) return;

        dt = Mathf.Min(dt, 0.1f);
        _t += dt;
        TickIdle(dt);
        if (_modal != null && !_modal.Alive) _modal = null;

        TuneBackdrop(false);
        UpdateCamera(dt);
        UpdateStage(dt);
        UpdateLair(dt);
        MeasureTitleFps(dt);

        if (!Mathf.IsEqualApprox(_dimNow, _dimTarget) && _dim != null)
        {
            _dimNow = Mathf.MoveToward(_dimNow, _dimTarget, dt * 1.8f);
            _dim.Color = new Color(0.012f, 0.010f, 0.010f, _dimNow);
        }
        if (!Mathf.IsEqualApprox(_scrimNow, _scrimTarget) && _scrim != null)
        {
            _scrimNow = Mathf.MoveToward(_scrimNow, _scrimTarget, dt * 1.6f);
            _scrim.Modulate = new Color(1, 1, 1, _scrimNow);
        }
        if (_page == Page.Title && _titleGlow != null)
            _titleGlow.Modulate = new Color(1f, 0.40f, 0.09f, 0.20f + 0.05f * Mathf.Sin(_t * 0.9f) + 0.02f * Mathf.Sin(_t * 2.3f));

        if (_page == Page.Credits && _creditsScroll != null) TickCredits(dt);
    }

    public override void _Input(InputEvent e)
    {
        if (!_built || !IsVisibleInTree()) return;
        NoteActivity(e);
        try
        {
            if (_busy)
            {
                // a game is starting: nothing may be pressed any more
                if (e is InputEventKey || e is InputEventMouseButton) GetViewport().SetInputAsHandled();
                return;
            }
            if (e is InputEventKey k && k.Pressed && !k.Echo)
            {
                if (k.Keycode == Key.Escape)
                {
                    Back();
                    GetViewport().SetInputAsHandled();
                    return;
                }
                if (_modal != null) return;
                if (_page == Page.Load && k.Keycode == Key.Delete)
                {
                    var s = SlotAt(_focusSlot);
                    if (s != null && s.Exists) { AskDelete(s); GetViewport().SetInputAsHandled(); }
                }
                else if (_page == Page.Credits)
                {
                    // a step is a few lines of the roll, so it grows with the text
                    if (k.Keycode == Key.Up || k.Keycode == Key.Pageup) { _creditsPos = Mathf.Max(0f, _creditsPos - UiTheme.Px(k.Keycode == Key.Pageup ? 420f : 90f)); _creditsHold = 2.5f; ApplyCreditsScroll(); GetViewport().SetInputAsHandled(); }
                    else if (k.Keycode == Key.Down || k.Keycode == Key.Pagedown) { _creditsPos += UiTheme.Px(k.Keycode == Key.Pagedown ? 420f : 90f); _creditsHold = 2.5f; ApplyCreditsScroll(); GetViewport().SetInputAsHandled(); }
                }
                else if (_page == Page.Title && GetViewport().GuiGetFocusOwner() == null
                         && (k.Keycode == Key.Up || k.Keycode == Key.Down || k.Keycode == Key.Enter || k.Keycode == Key.KpEnter))
                {
                    // the keyboard wakes the menu even after a click on empty space dropped the focus
                    FocusDefault();
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Xbutton1)
            {
                Back();
                GetViewport().SetInputAsHandled();
            }
        }
        catch (Exception ex) { Log.Error("menu", "title screen input failed", ex); }
    }

    // ───────────────────────── 展示模式 (DemoMode.cs) ─────────────────────────

    private float _idleT;
    private Control _demoFocus;
    private Vector2? _lastMouse;

    /// <summary>Any key, button, stick or a real mouse move on the title screen restarts the idle clock.</summary>
    private void NoteActivity(InputEvent e)
    {
        if (e.Device == TestBot.BotDevice) return;
        switch (e)
        {
            case InputEventMouseMotion mm:
            {
                Vector2 d = mm.Relative;
                if (d == Vector2.Zero && _lastMouse.HasValue) d = mm.Position - _lastMouse.Value;     // synthetic events carry no Relative
                _lastMouse = mm.Position;
                if (d.LengthSquared() > 9f) _idleT = 0f;
                break;
            }
            case InputEventJoypadMotion jm: if (Mathf.Abs(jm.AxisValue) > 0.3f) _idleT = 0f; break;
            case InputEventKey or InputEventMouseButton or InputEventJoypadButton: _idleT = 0f; break;
        }
    }

    /// <summary>
    /// The attract loop: after <see cref="DemoMode.IdleSeconds"/> without input on the title screen itself — not on the
    /// other screens, not with a dialog open, not while the window is in the background or minimised — 展示模式 starts.
    /// </summary>
    private void TickIdle(float dt)
    {
        var g = Game.I;
        bool armed = _page == Page.Title && _modal == null && !_busy && g != null && g.State == Game.GameState.Menu && g.Hero == null
                     && g.AppFocused && !(g.Ui?.AnyOpen ?? false) && !DemoMode.Active
                     && DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Minimized;
        if (!armed) { _idleT = 0f; return; }
        _idleT += dt;
        if (_idleT < DemoMode.IdleSeconds) return;
        _idleT = 0f;
        Log.Info("menu", $"title screen idle for {DemoMode.IdleSeconds:0.#} s: 展示模式 starts");
        StartDemo(true);
    }

    private void StartDemo(bool attract)
    {
        if (_busy || DemoMode.Active) return;
        _demoFocus = GetViewport().GuiGetFocusOwner();
        Log.Info("menu", $"展示模式 ({(attract ? "idle" : "chosen")}); focus was on '{(_demoFocus as Button)?.Text ?? _demoFocus?.Name ?? "-"}'");
        Leave(attract ? "attract demo" : "demo", () => DemoMode.Start(attract));
    }

    /// <summary>Opens a screen by name: title, new, load, settings or credits (used by the test harness).</summary>
    public void Open(string page)
    {
        if (!_built || _busy) return;
        string want = (page ?? "").Trim().ToLowerInvariant();
        if (want.StartsWith("class:", StringComparison.Ordinal))
        {
            // class:<key> — the class sheet of one class (test harness)
            StartNewGame(0);
            int idx = Array.IndexOf(Names.ClassKeys, want[6..]);
            if (idx < 0) { Log.Warn("menu", $"Open: unknown class '{want[6..]}'"); return; }
            // after the screen's own deferred focus call (which selects the class whose row takes the focus)
            Callable.From(() =>
            {
                if (_page != Page.ClassPick || _classBtns[idx] == null) return;
                SelectClass((ClassId)idx, true);
                _classBtns[idx].GrabFocus();
            }).CallDeferred();
            return;
        }
        switch (want)
        {
            case "new": case "newgame": case "class": StartNewGame(0); break;
            case "identity": case "name": StartNewGame(0); Go(Page.Identity); break;
            case "load": Go(Page.Load); break;
            case "settings": Go(Page.Settings); break;
            case "credits": Go(Page.Credits); break;
            default: Go(Page.Title); break;
        }
    }

    // ───────────────────────── navigation ─────────────────────────

    private Control RootOf(Page p) => p switch
    {
        Page.Title => _titleRoot,
        Page.ClassPick or Page.Identity => _newRoot,
        Page.Load => _loadRoot,
        Page.Settings => _settingsRoot,
        _ => _creditsRoot,
    };

    private void Ensure(Page p)
    {
        switch (p)
        {
            case Page.ClassPick:
            case Page.Identity:
                if (_newRoot == null) { _newRoot = BuildNewGame(); _pages.AddChild(_newRoot); }
                break;
            case Page.Load:
                if (_loadRoot == null) { _loadRoot = BuildLoad(); _pages.AddChild(_loadRoot); }
                break;
            case Page.Settings:
                if (_settingsRoot == null) { _settingsRoot = BuildSettings(); _pages.AddChild(_settingsRoot); }
                break;
            case Page.Credits:
                if (_creditsRoot == null) { _creditsRoot = BuildCredits(); _pages.AddChild(_creditsRoot); }
                break;
        }
    }

    private void Go(Page p, bool instant = false)
    {
        if (!_built) return;
        var old = _page;
        try { Ensure(p); }
        catch (Exception e) { Log.Error("menu", $"building the {p} screen failed", e); p = Page.Title; }
        if (old == Page.Settings && p != Page.Settings) _settings?.Flush();

        if (_titleRoot != null) _titleRoot.Visible = false;
        if (_newRoot != null) _newRoot.Visible = false;
        if (_loadRoot != null) _loadRoot.Visible = false;
        if (_settingsRoot != null) _settingsRoot.Visible = false;
        if (_creditsRoot != null) _creditsRoot.Visible = false;

        var root = RootOf(p);
        if (root == null) { p = Page.Title; root = _titleRoot; }
        _page = p;
        root.Visible = true;
        if (instant) root.Modulate = Colors.White;
        else
        {
            root.Modulate = new Color(1, 1, 1, 0);
            CreateTween().TweenProperty(root, "modulate:a", 1f, 0.28f);
            if ((old == Page.Title) != (p == Page.Title)) MenuKit.Whoosh();
        }

        bool stage = p == Page.ClassPick || p == Page.Identity;
        _camBlendTarget = stage ? 1f : 0f;
        _scrimTarget = p == Page.Title ? 1f : 0f;
        _dimTarget = p switch { Page.Load => 0.56f, Page.Settings => 0.62f, Page.Credits => 0.74f, _ => 0f };
        if (_heroPivot != null) _heroPivot.Visible = stage;

        switch (p)
        {
            case Page.Title: RefreshSlots(); break;
            case Page.ClassPick: ShowNewStep(false); break;
            case Page.Identity: ShowNewStep(true); break;
            case Page.Load: RefreshSlots(); RebuildLoadGrid(); break;
            case Page.Settings: _settings?.Refresh(); break;
            case Page.Credits:
                _creditsPos = 0f; _creditsHold = 0.4f;
                if (_creditsScroll != null) _creditsScroll.ScrollVertical = 0;
                break;
        }
        MenuKit.Quiet();
        FocusDefault();
        Log.Debug("menu", $"screen {old} -> {p}");
    }

    private void Back()
    {
        if (_modal != null) { _modal.Cancel(); return; }
        switch (_page)
        {
            case Page.Title:
                // Esc on the title offers the way out
                if (_entries.Count > 0) _entries[^1].GrabFocus();
                return;
            case Page.Identity: MenuKit.CloseSound(); Go(Page.ClassPick); return;
            default: MenuKit.CloseSound(); Go(Page.Title); return;
        }
    }

    private void FocusDefault()
    {
        Control c = _page switch
        {
            Page.Title => FirstEntry(),
            Page.ClassPick => _classBtns[(int)_cls],
            Page.Identity => _nameEdit,
            Page.Load => FirstLoadButton(),
            Page.Settings => _settings?.DefaultFocus,
            _ => _creditsBack,
        };
        if (c == null) return;
        Callable.From(() =>
        {
            if (_modal != null || !IsInstanceValid(c) || !c.IsVisibleInTree()) return;
            c.GrabFocus();
            if (c is LineEdit le) { le.Edit(); le.SelectAll(); }
        }).CallDeferred();
    }

    private Button FirstEntry()
    {
        foreach (var b in _entries) if (!b.Disabled) return b;
        return null;
    }

    private void FadeIn(float seconds)
    {
        if (_fade == null) return;
        _fade.Visible = true;
        _fade.MouseFilter = MouseFilterEnum.Ignore;
        _fade.Modulate = Colors.White;
        var tw = CreateTween();
        tw.TweenProperty(_fade, "modulate:a", 0f, seconds).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.TweenCallback(Callable.From(() => { if (!_busy) _fade.Visible = false; }));
    }

    /// <summary>Fades to black, then hands over to Game. If the game does not take over, the menu comes back.</summary>
    private void Leave(string what, Action go)
    {
        _busy = true;
        _settings?.Flush();
        if (_fade == null) { RunLeave(what, go); return; }
        _fade.Visible = true;
        _fade.MouseFilter = MouseFilterEnum.Stop;
        var tw = CreateTween();
        tw.TweenProperty(_fade, "modulate:a", 1f, 0.45f);
        tw.TweenCallback(Callable.From(() => RunLeave(what, go)));
        tw.TweenInterval(5.0);
        tw.TweenCallback(Callable.From(() =>
        {
            var g = Game.I;
            if (!_busy || !IsVisibleInTree() || (g != null && g.State != Game.GameState.Menu)) return;
            Log.Warn("menu", $"{what}: still on the title screen after 5 s, giving control back");
            Recover("旅途沒能展開。請再試一次；若問題持續，記錄檔裡會有線索。");
        }));
    }

    private void RunLeave(string what, Action go)
    {
        try
        {
            if (Game.I == null) throw new InvalidOperationException("Game is not running");
            go();
        }
        catch (Exception e)
        {
            Log.Error("menu", $"{what} failed", e);
            Recover("旅途沒能展開。詳情已寫入記錄檔。");
        }
    }

    private void Recover(string message)
    {
        _busy = false;
        FadeIn(0.6f);
        Go(Page.Title, true);
        Notice("出了點差錯", message);
    }

    // ───────────────────────── modal ─────────────────────────

    private void Ask(string title, string body, string yes, string no, Action onYes, MenuTone tone = MenuTone.Primary)
    {
        if (_modalLayer == null) return;
        _modal?.Close();
        _focusBeforeModal = GetViewport().GuiGetFocusOwner();
        MenuModal m = null;
        m = MenuKit.Modal(_modalLayer, title, body, yes, no, onYes, null, tone);
        m.Closed = () =>
        {
            if (_modal == m) _modal = null;
            var back = _focusBeforeModal;
            _focusBeforeModal = null;
            Callable.From(() =>
            {
                if (_modal != null && _modal.Alive) return;
                if (back != null && IsInstanceValid(back) && back.IsVisibleInTree() && back.FocusMode != FocusModeEnum.None) back.GrabFocus();
                else FocusDefault();
            }).CallDeferred();
        };
        _modal = m;
    }

    private void Notice(string title, string body) => Ask(title, body, "知道了", null, null, MenuTone.Normal);

    // ───────────────────────── save slots ─────────────────────────

    private void RefreshSlots()
    {
        try { _slots = SaveSystem.List() ?? new List<SlotInfo>(); }
        catch (Exception e) { Log.Error("menu", "listing the save slots failed", e); _slots = new List<SlotInfo>(); }
        for (int i = 1; i <= SaveSystem.SlotCount; i++)
        {
            bool found = false;
            foreach (var s in _slots) if (s.Slot == i) { found = true; break; }
            if (!found) _slots.Add(new SlotInfo { Slot = i });
        }
        _slots.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        RefreshContinue();
    }

    private SlotInfo SlotAt(int slot)
    {
        foreach (var s in _slots) if (s.Slot == slot) return s;
        return null;
    }

    private static bool Playable(SlotInfo s) => s != null && s.Exists && !s.Corrupt && !(s.Hardcore && s.Dead);

    private void RefreshContinue()
    {
        SlotInfo best = null;
        int last = Game.I?.Cfg?.LastSlot ?? 0;
        foreach (var s in _slots)
        {
            if (!Playable(s)) continue;
            if (best == null) { best = s; continue; }
            int cmp = string.CompareOrdinal(s.SavedAt ?? "", best.SavedAt ?? "");
            if (cmp > 0 || (cmp == 0 && s.Slot == last)) best = s;
        }
        _continueSlot = best?.Slot ?? 0;
        if (_continueBtn != null)
        {
            _continueBtn.Disabled = best == null;
            _continueBtn.FocusMode = best == null ? FocusModeEnum.None : FocusModeEnum.All;
            _continueBtn.MouseDefaultCursorShape = best == null ? CursorShape.Arrow : CursorShape.PointingHand;
        }
        if (_continueNote != null)
        {
            _continueNote.Text = best == null
                ? "尚無可以延續的旅途"
                : $"{(string.IsNullOrEmpty(best.Name) ? "無名者" : best.Name)}　{MenuKit.LevelLine(best.Class, best.Level, best.Paragon)}　·　{MenuKit.AreaName(best.Area)}";
        }
    }

    private void RememberSlot(int slot)
    {
        var cfg = Game.I?.Cfg;
        if (cfg == null || cfg.LastSlot == slot) return;
        cfg.LastSlot = slot;
        cfg.Save();
    }

    private void OnContinue()
    {
        if (_busy) return;
        RefreshSlots();
        var s = SlotAt(_continueSlot);
        if (!Playable(s)) { MenuKit.ErrorSound(); return; }
        LoadSlot(s);
    }

    private void LoadSlot(SlotInfo s)
    {
        if (_busy) return;
        if (!Playable(s)) { MenuKit.ErrorSound(); return; }
        int slot = s.Slot;
        Log.Info("menu", $"load game: slot={slot} level={s.Level} class={MenuKit.ClassName(s.Class)} hardcore={s.Hardcore}");
        RememberSlot(slot);
        Leave("loading slot " + slot, () => Game.I.LoadGame(slot));
    }

    private void AskDelete(SlotInfo s)
    {
        if (s == null || !s.Exists || _busy) return;
        int slot = s.Slot;
        string who = s.Corrupt ? $"欄位 {slot} 的毀損存檔" : $"欄位 {slot} 的{MenuKit.SlotLine(s)}";
        string tail = s.Corrupt ? "這份記錄已無法讀取，刪除後欄位即可重新使用。" : s.Hardcore && s.Dead ? "其名將自灰燼中抹去。此舉無法復原。" : "這段旅途將永遠消失。此舉無法復原。";
        Ask("刪除存檔", $"確定要刪除{who}嗎？\n{tail}", "刪除", "保留", () =>
        {
            Log.Info("menu", $"delete save: slot={slot} corrupt={s.Corrupt} hardcore={s.Hardcore} dead={s.Dead}");
            SaveSystem.Delete(slot);
            var cfg = Game.I?.Cfg;
            if (cfg != null && cfg.LastSlot == slot) { cfg.LastSlot = 0; cfg.Save(); }
            RefreshSlots();
            if (_page == Page.Load) { _focusSlot = slot; RebuildLoadGrid(); FocusDefault(); }
            else if (_page == Page.Identity) SyncIdentity();
        }, MenuTone.Danger);
    }

    // ───────────────────────── chrome ─────────────────────────

    private void BuildChrome()
    {
        // darkness on the left so the name and the entries read against the fire
        _scrim = MenuKit.Stretched(MenuKit.Fade(false,
            (0f, new Color(0, 0, 0, 0.90f)), (0.32f, new Color(0, 0, 0, 0.66f)), (0.60f, new Color(0, 0, 0, 0f)), (1f, new Color(0, 0, 0, 0f))));
        AddChild(_scrim);
        var vignette = MenuKit.Stretched(MenuKit.Fade(true,
            (0f, new Color(0, 0, 0, 0.64f)), (0.20f, new Color(0, 0, 0, 0f)), (0.76f, new Color(0, 0, 0, 0f)), (1f, new Color(0, 0, 0, 0.80f))));
        AddChild(vignette);

        _dim = new ColorRect { Color = new Color(0.012f, 0.010f, 0.010f, 0f), MouseFilter = MouseFilterEnum.Ignore };
        _dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_dim);

        _pages = new Control { Name = "Pages", MouseFilter = MouseFilterEnum.Ignore };
        _pages.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_pages);

        _modalLayer = new Control { Name = "ModalLayer", MouseFilter = MouseFilterEnum.Ignore };
        _modalLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_modalLayer);

        _fade = new ColorRect { Name = "Fade", Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        _fade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_fade);
    }

    // ───────────────────────── title ─────────────────────────

    private Control BuildTitle()
    {
        var root = new Control { Name = "Title", MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _titleGlow = new TextureRect
        {
            Texture = MenuKit.SoftDot, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1f, 0.40f, 0.09f, 0.2f),
        };
        MenuKit.Place(_titleGlow, 0, 0.5f, 0, 0.5f, -100, -520, 960, 0);      // behind the lockup: its axis is 432 px from the left edge
        root.AddChild(_titleGlow);

        var col = MenuKit.VBox(0);
        col.Name = "Column";
        col.MouseFilter = MouseFilterEnum.Ignore;
        col.Alignment = BoxContainer.AlignmentMode.Center;
        MenuKit.Place(col, 0, 0, 0, 1, 150, 24, 150 + 820, -64);      // tall enough for the column at the largest text size
        root.AddChild(col);

        _logoFont = MenuKit.Spaced(MenuKit.BlackFont, 28, 36, 34);
        _latinFont = MenuKit.Latin(600, 21);

        // With the raster art the lockup — the dead Sun-Forge, the name, its Latin form, the ember rule and the motto —
        // and the menu under it stand on one axis: the lockup is as wide as its rule and everything in it is centred.
        // Without the art the title keeps its old shape: everything on the left edge, a plain ember line, bare entries.
        _emblem = UiTheme.Art ? UiArt.P("emblem") : null;
        var line = MenuKit.Ornament("title_rule", 28f);
        bool axis = _emblem != null && line != null && MenuKit.EntryPlates;
        var lockup = MenuKit.VBox(0);
        lockup.Name = "Lockup";
        lockup.MouseFilter = MouseFilterEnum.Ignore;
        lockup.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        col.AddChild(lockup);

        // the emblem crowns the lettering: it is drawn first, so the name stays on top where its lowest rays reach down
        if (axis)
        {
            _crest = new Control { Name = "Crest", MouseFilter = MouseFilterEnum.Ignore };
            _crest.Draw += DrawCrest;
            lockup.AddChild(_crest);
        }

        // the game's name is a logo, not running text: it follows the text-size setting only a little (LogoScale)
        var name = UiTheme.FixedSize(MenuKit.Text(CreditsData.GameTitle, LogoPx(190), new Color(0.97f, 0.91f, 0.80f), _logoFont));
        _logoLabel = name;
        name.AddThemeColorOverride("font_outline_color", new Color(0.30f, 0.09f, 0.02f, 1f));
        name.AddThemeConstantOverride("outline_size", 5);
        MenuKit.Shadow(name, new Color(0, 0, 0, 0.72f), 14, 6);
        lockup.AddChild(name);

        var latin = UiTheme.FixedSize(MenuKit.Text(CreditsData.GameTitleLatin, LogoPx(44), new Color(0.84f, 0.69f, 0.45f), _latinFont));
        _latinLabel = latin;
        MenuKit.Shadow(latin, new Color(0, 0, 0, 0.85f), 8, 2);

        var tag = MenuKit.Text("日爐已死。唯餘燼火，與持燼之人。", UiTheme.Fs(24), MenuKit.Ash, MenuKit.BodyFont);
        MenuKit.Shadow(tag, new Color(0, 0, 0, 0.85f), 6, 2);

        if (axis)
        {
            name.HorizontalAlignment = HorizontalAlignment.Center;
            latin.HorizontalAlignment = HorizontalAlignment.Center;
            tag.HorizontalAlignment = HorizontalAlignment.Center;
            lockup.AddChild(latin);
            // under the Latin name: the tapering gold rule with its ember stone
            line.CustomMinimumSize = new Vector2(LockupW, 28);
            lockup.AddChild(MenuKit.Pad(line, 0, 4, 0, 2));
            lockup.AddChild(tag);
        }
        else
        {
            lockup.AddChild(MenuKit.Pad(latin, 12, 0, 0, 0));
            var fade = MenuKit.Stretched(MenuKit.Fade(false, (0f, new Color(1f, 0.5f, 0.15f, 0.9f)), (0.7f, new Color(1f, 0.5f, 0.15f, 0.25f)), (1f, new Color(1f, 0.5f, 0.15f, 0f))));
            fade.CustomMinimumSize = new Vector2(0, 2);
            var lineBox = new Control { CustomMinimumSize = new Vector2(560, 2), MouseFilter = MouseFilterEnum.Ignore };
            lineBox.AddChild(fade);
            lockup.AddChild(MenuKit.Pad(lineBox, 14, 16, 240, 14));
            lockup.AddChild(MenuKit.Pad(tag, 14, 0, 0, 0));
        }
        _titleGap = MenuKit.Spacer(0, 44);                   // between the motto and the menu: gives way to larger text (TuneTitleFonts)
        lockup.AddChild(_titleGap);

        var list = MenuKit.VBox(0);
        list.MouseFilter = MouseFilterEnum.Ignore;
        lockup.AddChild(list);

        _entries.Clear();
        _titleAxis = axis;
        _continueBtn = AddEntry(list, "繼續遊戲", OnContinue);
        // one line as wide as the lockup: a long name is cut with an ellipsis and shown whole on hover
        _continueNote = UiTheme.Trimmed(MenuKit.Text("", UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont));
        _continueNote.CustomMinimumSize = new Vector2(axis ? LockupW : 700, 0);
        MenuKit.Shadow(_continueNote, new Color(0, 0, 0, 0.8f), 5, 1);
        if (axis)
        {
            _continueNote.HorizontalAlignment = HorizontalAlignment.Center;
            list.AddChild(MenuKit.Pad(_continueNote, 0, 0, 0, 8));
        }
        else list.AddChild(MenuKit.Pad(_continueNote, 44, 0, 0, 8));     // under the text of the bare entry
        TuneTitleFonts();
        AddEntry(list, "新遊戲", () => StartNewGame(0));
        AddEntry(list, "載入遊戲", () => Go(Page.Load));
        AddEntry(list, "設定", () => Go(Page.Settings));
        AddEntry(list, "製作名單", () => Go(Page.Credits));
        AddEntry(list, "展示模式", () => StartDemo(false));          // DemoMode.cs
        AddEntry(list, "離開遊戲", OnQuit);

        // the two footers stand on the bottom edge and grow upward with the text
        var version = MenuKit.Text($"版本 {CreditsData.Version}", UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont);
        version.HorizontalAlignment = HorizontalAlignment.Right;
        version.VerticalAlignment = VerticalAlignment.Bottom;
        version.GrowVertical = GrowDirection.Begin;
        MenuKit.Place(version, 1, 1, 1, 1, -420, -52, -40, -22);
        root.AddChild(version);

        // the keys as forged caps, held back so the foot of the screen stays quiet
        var hint = MenuKit.KeyHints(UiTheme.Fs(20), MenuKit.Faint, "[↑]", "[↓]", "選擇", "", "[Enter]", "確認");
        hint.Modulate = new Color(1f, 1f, 1f, 0.8f);
        if (axis) hint.Alignment = BoxContainer.AlignmentMode.Center;         // on the axis of the lockup
        hint.GrowVertical = GrowDirection.Begin;
        MenuKit.Place(hint, 0, 1, 0, 1, axis ? 150 : 194, -56, axis ? 150 + LockupW : 800, -22);
        root.AddChild(hint);
        return root;
    }

    /// <summary>
    /// The letter-spacing of the two title faces and the trimmed line box of the game's name are lengths in pixels:
    /// they follow the text scale like the glyphs do (called when the title is built and after every scale change).
    /// </summary>
    private void TuneTitleFonts()
    {
        float k = LogoScale;
        if (_logoFont is FontVariation logo)
        {
            logo.SpacingGlyph = (int)MathF.Round(28f * k);
            logo.SpacingTop = -(int)MathF.Round(36f * k);
            logo.SpacingBottom = -(int)MathF.Round(34f * k);
        }
        if (_latinFont is FontVariation latin) latin.SpacingGlyph = (int)MathF.Round(21f * k);
        if (_logoLabel != null && IsInstanceValid(_logoLabel)) _logoLabel.AddThemeFontSizeOverride("font_size", LogoPx(190));
        if (_latinLabel != null && IsInstanceValid(_latinLabel)) _latinLabel.AddThemeFontSizeOverride("font_size", LogoPx(44));
        // the emblem over the name and the gap above the menu are what gives way when larger text needs the height
        if (_crest != null && IsInstanceValid(_crest))
        {
            _crest.CustomMinimumSize = new Vector2(0, MathF.Round(CrestRoom * CrestScale));
            _crest.QueueRedraw();
        }
        if (_titleAxis && _titleGap != null && IsInstanceValid(_titleGap))
            _titleGap.CustomMinimumSize = new Vector2(0, MathF.Max(4f, MathF.Round(44f - (UiTheme.TextScale - 1f) * 133f)));     // seven entries since 展示模式
    }

    /// <summary>The logo grows with the text-size setting only by a third of the way (×1.09 at 特大), so the menu under it keeps its room.</summary>
    private static float LogoScale => 1f + (UiTheme.TextScale - 1f) * 0.3f;
    private static int LogoPx(int design) => (int)MathF.Round(design * LogoScale);

    /// <summary>Width of the title lockup (design px): the logical width of its rule piece.</summary>
    private const float LockupW = 564f;

    // the emblem over the name (design px at 標準): the scale of the piece, the height it reserves in the lockup, and how
    // far above the top of the name's line box the centre of its ring sits. The six entries under the lockup grow with
    // the text and the screen does not, so the emblem shrinks as the text grows (×0.52 at 特大; seven entries since 展示模式).
    private const float CrestSize = 0.74f, CrestRoom = 112f, CrestAxis = 50f;
    private static readonly Color CrestTint = new(1f, 0.97f, 0.93f, 0.96f);
    private static float CrestScale => 1f - (UiTheme.TextScale - 1f) * 1.6f;      // ×0.52 at 特大: room for the seventh entry (展示模式)

    private void DrawCrest()
    {
        if (_emblem == null || _crest == null) return;
        float k = CrestScale;
        UiArt.SpriteAt(_crest, _emblem, new Vector2(MathF.Round(_crest.Size.X * 0.5f), _crest.Size.Y - MathF.Round(CrestAxis * k)), CrestTint, CrestSize * k);
    }

    private Button AddEntry(Container list, string text, Action onPress)
    {
        // on the forged plates (56 design px tall) the lettering is a little smaller than the bare entries of old, so
        // it keeps clear of the plate's hairline; under the centred lockup the plates and their text are centred too
        bool plates = MenuKit.EntryPlates;
        var b = MenuKit.Entry(text, UiTheme.Fs(plates ? 30 : 34), () => { if (!_busy) onPress(); });
        b.SizeFlagsHorizontal = _titleAxis ? SizeFlags.ShrinkCenter : SizeFlags.ShrinkBegin;
        if (_titleAxis) b.Alignment = HorizontalAlignment.Center;
        UiTheme.MinSize(b, 360, plates ? 56 : 54);
        list.AddChild(b);
        _entries.Add(b);
        return b;
    }

    /// <summary>
    /// A boxed command button (MenuKit.Btn) from a DESIGN font size: the size goes through <see cref="UiTheme.Fs"/>
    /// here, and the button's minimum box grows with the text and stays in step when the text scale changes.
    /// </summary>
    private static Button Btn(string text, Action onPress, MenuTone tone, int size, float minWidth)
    {
        var b = MenuKit.Btn(text, onPress, tone, UiTheme.Fs(size), minWidth);
        return UiTheme.MinSize(b, minWidth, size + 26);
    }

    private void OnQuit()
    {
        Ask("離開遊戲", "要熄去燈火，離開瑟蘭嗎？", "離開", "留下", () =>
        {
            Log.Info("menu", "quit from the title screen");
            _busy = true;
            _settings?.Flush();
            var g = Game.I;
            if (g != null) g.Quit();
            else GetTree().Quit();
        });
    }

    // ───────────────────────── character creation ─────────────────────────

    private void StartNewGame(int presetSlot)
    {
        RefreshSlots();
        _newSlot = presetSlot;
        if (_newSlot < 1 || _newSlot > SaveSystem.SlotCount)
        {
            _newSlot = 1;
            foreach (var s in _slots) if (!s.Exists) { _newSlot = s.Slot; break; }
        }
        _hardcore = false;
        _nameAuto = true;
        _suggest = Rng.Range(0, 3);
        Go(Page.ClassPick);
        if (_hcCheck != null) _hcCheck.SetPressedNoSignal(false);
        SyncHardcore();
        ApplySuggestedName();
    }

    private Control BuildNewGame()
    {
        var root = new Control { Name = "NewGame", MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // the open middle of the screen: drag there to turn the hero
        _dragArea = new Control { Name = "Turn", MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.Drag, FocusMode = FocusModeEnum.None };
        MenuKit.Place(_dragArea, 0, 0, 1, 1, Edge + ListW + 24, PaneTop, -(Edge + InfoW + 24), -PaneBottom);
        _dragArea.GuiInput += OnHeroDrag;
        root.AddChild(_dragArea);

        // both lines stand on the bottom edge of the open middle and grow upward with the text
        _dragHint = MenuKit.Text("按住滑鼠左鍵拖曳，可轉動身形", UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont);
        _dragHint.HorizontalAlignment = HorizontalAlignment.Center;
        _dragHint.VerticalAlignment = VerticalAlignment.Center;
        // [fixes] below the stage, in the band of the 返回／下一步 bar (it sat on the floor ring and was hard to read)
        MenuKit.Shadow(_dragHint, new Color(0, 0, 0, 0.8f), 5, 1);
        MenuKit.Place(_dragHint, 0, 1, 1, 1, 0, PaneBottom - 98, 0, PaneBottom - 42);
        _dragArea.AddChild(_dragHint);

        // the name plate keeps to the gap between the panes: a long name ends in an ellipsis (it is in the name field anyway)
        _plate = UiTheme.Trimmed(MenuKit.Text("", UiTheme.Fs(34), MenuKit.Ivory, MenuKit.TitleFont), false);
        _plate.HorizontalAlignment = HorizontalAlignment.Center;
        _plate.VerticalAlignment = VerticalAlignment.Bottom;
        _plate.GrowVertical = GrowDirection.Begin;
        MenuKit.Shadow(_plate, new Color(0, 0, 0, 0.88f), 9, 2);
        MenuKit.Place(_plate, 0, 1, 1, 1, 0, -62, 0, -6);
        _dragArea.AddChild(_plate);

        _classPane = BuildClassPane();
        root.AddChild(_classPane);
        _identPane = BuildIdentityPane();
        root.AddChild(_identPane);
        root.AddChild(BuildInfoPane());

        _classBar = BuildBar("返回", () => Go(Page.Title), "下一步　銘記其名", GoIdentity, out _nextBtn);
        root.AddChild(_classBar);
        _identBar = BuildBar("返回", () => Go(Page.ClassPick), "點燃燼火", TryStart, out _startBtn);
        root.AddChild(_identBar);
        return root;
    }

    private Control BuildBar(string backText, Action onBack, string nextText, Action onNext, out Button next)
    {
        var bar = MenuKit.HBox(16);
        bar.MouseFilter = MouseFilterEnum.Ignore;
        MenuKit.Place(bar, 0, 1, 1, 1, Edge, -98, -Edge, -42);
        bar.AddChild(Btn(backText, () => { if (!_busy) onBack(); }, MenuTone.Normal, 22, 170f));
        var gap = MenuKit.Spacer();
        gap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bar.AddChild(gap);
        next = Btn(nextText, () => { if (!_busy) onNext(); }, MenuTone.Primary, 24, 320f);
        bar.AddChild(next);
        return bar;
    }

    private Control BuildClassPane()
    {
        var col = MenuKit.VBox(6);
        col.Name = "Classes";
        col.MouseFilter = MouseFilterEnum.Ignore;
        MenuKit.Place(col, 0, 0, 0, 1, Edge, PaneTop, Edge + ListW, -PaneBottom);

        var head = MenuKit.Text("擇定持燼者", UiTheme.Fs(44), MenuKit.Ivory, MenuKit.TitleFont);
        MenuKit.Shadow(head, new Color(0, 0, 0, 0.85f), 9, 2);
        col.AddChild(head);
        var sub = MenuKit.Wrap("燼火選中的，從來不是最強的人。", UiTheme.Fs(22), MenuKit.Ash, ListW);
        MenuKit.Shadow(sub, new Color(0, 0, 0, 0.8f), 5, 1);
        col.AddChild(sub);
        col.AddChild(MenuKit.Spacer(0, 14));

        for (int i = 0; i < Names.ClassCount; i++)
        {
            var b = BuildClassButton((ClassId)i);
            _classBtns[i] = b;
            col.AddChild(b);
        }
        return col;
    }

    private static StyleBox ClassBox(Color bg, Color border)
    {
        var s = MenuKit.Box(bg, border, 1, 2);
        s.ContentMarginLeft = 56; s.ContentMarginRight = 12; s.ContentMarginTop = 4; s.ContentMarginBottom = 4;
        return s;
    }

    private Button BuildClassButton(ClassId c)
    {
        Color cc = Names.ClassColor(c);
        var b = new Button
        {
            Text = Names.Class(c), ToggleMode = true, Alignment = HorizontalAlignment.Left, FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        UiTheme.MinSize(b, 0, 60);                          // the column gives the width (ListW); the row grows with the text
        // forged row plates — dark at rest, lit under the pointer, the ember face when chosen — with a fine line in
        // the class colour on the hovered and the chosen one; without the art, the tinted boxes of old
        if (!MenuKit.Plate(b, 56f, 4f, 12f, 4f, cc))
        {
            var bgDark = new Color(0.030f, 0.028f, 0.027f, 0.66f);
            var bgHot = new Color(cc.R * 0.17f + 0.02f, cc.G * 0.17f + 0.02f, cc.B * 0.17f + 0.02f, 0.88f);
            b.AddThemeStyleboxOverride("normal", ClassBox(bgDark, new Color(MenuKit.Line, 0.5f)));
            b.AddThemeStyleboxOverride("hover", ClassBox(new Color(0.06f, 0.055f, 0.05f, 0.78f), new Color(cc, 0.6f)));
            b.AddThemeStyleboxOverride("pressed", ClassBox(bgHot, cc));
            b.AddThemeStyleboxOverride("hover_pressed", ClassBox(bgHot, cc.Lightened(0.2f)));
        }
        b.AddThemeStyleboxOverride("focus", MenuKit.FocusBox());
        var font = MenuKit.TitleFont;
        if (font != null) b.AddThemeFontOverride("font", font);
        b.AddThemeFontSizeOverride("font_size", UiTheme.Fs(28));
        b.AddThemeColorOverride("font_color", MenuKit.Ash);
        b.AddThemeColorOverride("font_hover_color", MenuKit.Ivory);
        b.AddThemeColorOverride("font_focus_color", MenuKit.Ivory);
        b.AddThemeColorOverride("font_pressed_color", Colors.White);
        b.AddThemeColorOverride("font_hover_pressed_color", Colors.White);

        var pts = new Vector2[4];
        b.Draw += () =>
        {
            bool on = b.ButtonPressed;
            float cy = b.Size.Y * 0.5f, cx = 29f, r = on ? 9f : 6f;
            pts[0] = new Vector2(cx, cy - r); pts[1] = new Vector2(cx + r, cy); pts[2] = new Vector2(cx, cy + r); pts[3] = new Vector2(cx - r, cy);
            b.DrawColoredPolygon(pts, on ? cc : new Color(cc, 0.55f));
        };

        var res = MenuKit.Text(Names.Resource(c), UiTheme.Fs(20), new Color(Names.ResourceColor(c).Lerp(MenuKit.Ash, 0.35f), 0.9f), MenuKit.UiFont);
        res.HorizontalAlignment = HorizontalAlignment.Right;
        res.VerticalAlignment = VerticalAlignment.Center;
        MenuKit.Place(res, 1, 0, 1, 1, -140, 0, -18, 0);
        b.AddChild(res);

        b.FocusEntered += () => { if (_page == Page.ClassPick && !_busy) SelectClass(c, true); };
        b.MouseEntered += MenuKit.Hover;
        b.Pressed += () =>
        {
            if (_busy) return;
            // the first press chooses; pressing the chosen class again (or Enter) moves on
            bool confirm = c == _cls && _t - _clsPickedAt > 0.4f;
            MenuKit.Click();
            if (confirm) GoIdentity();
            else SelectClass(c, true);
        };
        return b;
    }

    private Control BuildInfoPane()
    {
        var frame = MenuKit.Glass(0.80f);
        frame.Name = "ClassInfo";
        MenuKit.Place(frame, 1, 0, 1, 1, -(Edge + InfoW), PaneTop, -Edge, -PaneBottom);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        // the sheet is laid out to fit the pane at the standard text size; with larger text (or a wordy class) it scrolls
        var padded = MenuKit.Pad(scroll, 26, 14, 16, 12);          // the scroll bar keeps clear of the pane's moulding
        frame.AddChild(padded);
        // [fixes] when the sheet runs past the pane (職業專精 at the bottom), its last rows fade into the pane with a
        // 「▼ 捲動看更多」 cue instead of ending on a half-cut line; gone once scrolled to the end (README pass)
        var cue = new Control { MouseFilter = MouseFilterEnum.Ignore };
        padded.AddChild(cue);
        var grad = new Gradient();
        grad.SetColor(0, new Color(MenuKit.GlassBg, 0f));
        grad.SetColor(1, new Color(MenuKit.GlassBg, 1f));
        grad.AddPoint(0.55f, new Color(MenuKit.GlassBg, 1f));     // solid under the cue line
        var fade = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = grad, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 4, Height = 64 },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore,
        };
        MenuKit.Place(fade, 0, 1, 1, 1, 0, -UiTheme.Px(100f), -14, 0);
        cue.AddChild(fade);
        var more = MenuKit.Text("▼ 捲動看更多", UiTheme.Fs(18), MenuKit.Faint, MenuKit.UiFont);
        more.HorizontalAlignment = HorizontalAlignment.Center;
        MenuKit.Shadow(more, new Color(0, 0, 0, 0.9f), 4, 1);
        MenuKit.Place(more, 0, 1, 1, 1, 0, -UiTheme.Px(26f), -14, 0);
        cue.AddChild(more);
        _infoScroll = scroll; _infoMore = cue;
        var col = MenuKit.VBox(6);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(col);
        float w = InfoW - 26 - 16 - 16;                    // usable text width beside the scroll bar

        _ciName = MenuKit.Text("", UiTheme.Fs(46), Palette.Ember, MenuKit.BlackFont);
        col.AddChild(_ciName);
        _ciRole = MenuKit.Wrap("", UiTheme.Fs(22), MenuKit.Ash, w, MenuKit.UiFont);
        col.AddChild(_ciRole);
        col.AddChild(MenuKit.Rule(new Color(MenuKit.Line, 0.8f)));
        _ciDesc = MenuKit.Wrap("", UiTheme.Fs(24), MenuKit.Ivory, w);
        col.AddChild(_ciDesc);

        // 可用武器 (SYSTEMS): two-hand / one-hand (dual-wield) / off-hand bases, generated from the loadout table
        var wpnRow = MenuKit.HBox(12);
        wpnRow.AddChild(KeyLabel("可用武器"));
        _ciWeapons = MenuKit.Wrap("", UiTheme.Fs(20), MenuKit.Ivory, 0f, MenuKit.UiFont);
        _ciWeapons.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        wpnRow.AddChild(_ciWeapons);
        col.AddChild(wpnRow);
        // SYSTEMS: the weapon guide of the chosen class (武器指南: play style, strengths and alternatives of every base)
        var guideBtn = MenuKit.Btn("武器指南", () => Game.I?.Ui?.Open("guide", (ClassId)Math.Max(0, _heroClass)), MenuTone.Normal, UiTheme.Fs(20), UiTheme.Px(160f));
        guideBtn.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        col.AddChild(guideBtn);

        // key / value / note rows: the key and value columns grow with the text, the note wraps in what they leave
        var resRow = MenuKit.HBox(12);
        resRow.AddChild(KeyLabel("資源"));
        _ciResName = MenuKit.Text("", UiTheme.Fs(22), Palette.Ember, MenuKit.TitleFont);
        UiTheme.MinSize(_ciResName, 56, 0);
        resRow.AddChild(_ciResName);
        _ciResNote = MenuKit.Wrap("", UiTheme.Fs(20), MenuKit.Ash, 0f, MenuKit.UiFont);
        resRow.AddChild(_ciResNote);
        col.AddChild(resRow);

        var statRow = MenuKit.HBox(12);
        statRow.AddChild(KeyLabel("主屬性"));
        _ciStat = MenuKit.Text("", UiTheme.Fs(22), MenuKit.Ivory, MenuKit.BodyFont);
        statRow.AddChild(_ciStat);
        col.AddChild(statRow);

        var diffRow = MenuKit.HBox(12);
        diffRow.AddChild(KeyLabel("上手難度"));
        _ciDiff = MenuKit.Text("", UiTheme.Fs(22), MenuKit.Gold, MenuKit.UiFont);
        UiTheme.MinSize(_ciDiff, 136, 0);
        diffRow.AddChild(_ciDiff);
        _ciDiffNote = MenuKit.Wrap("", UiTheme.Fs(20), MenuKit.Ash, 0f, MenuKit.UiFont);
        diffRow.AddChild(_ciDiffNote);
        col.AddChild(diffRow);

        col.AddChild(MenuKit.Spacer(0, 4));
        col.AddChild(MenuKit.Text("招牌技能", UiTheme.Fs(26), MenuKit.Gold, MenuKit.TitleFont));
        for (int i = 0; i < 3; i++)
        {
            var row = MenuKit.HBox(12);
            var holder = new PanelContainer { CustomMinimumSize = new Vector2(SkillCell, SkillCell), SizeFlagsVertical = SizeFlags.ShrinkBegin };
            holder.AddThemeStyleboxOverride("panel", SkillFrame(MenuKit.Line));
            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(SkillCell - 24, SkillCell - 24),
            };
            holder.AddChild(icon);
            row.AddChild(holder);
            var text = MenuKit.VBox(1);
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var top = MenuKit.HBox(10);
            var nm = MenuKit.Text("", UiTheme.Fs(24), MenuKit.Ivory, MenuKit.TitleFont);
            top.AddChild(nm);
            var meta = MenuKit.Text("", UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont);
            meta.SizeFlagsVertical = SizeFlags.ShrinkEnd;
            top.AddChild(meta);
            text.AddChild(top);
            // the whole description, on as many lines as it needs: the sheet scrolls, nothing is cut off
            var desc = MenuKit.Wrap("", UiTheme.Fs(20), MenuKit.Ash, w - SkillCell - 12, MenuKit.UiFont);
            text.AddChild(desc);
            row.AddChild(text);
            col.AddChild(row);
            _skRow[i] = row; _skFrame[i] = holder; _skIcon[i] = icon; _skName[i] = nm; _skMeta[i] = meta; _skDesc[i] = desc;
        }

        col.AddChild(MenuKit.Spacer(0, 4));
        var specHead = MenuKit.HBox(12);
        specHead.AddChild(MenuKit.Text("職業專精", UiTheme.Fs(26), MenuKit.Gold, MenuKit.TitleFont));
        var specNote = MenuKit.Text($"等級 {SkillDb.SpecLevel} 時三擇其一", UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont);
        specNote.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        specHead.AddChild(specNote);
        col.AddChild(specHead);
        for (int i = 0; i < 3; i++)
        {
            var row = MenuKit.HBox(12);
            var nm = MenuKit.Text("", UiTheme.Fs(22), Palette.Ember, MenuKit.TitleFont);
            UiTheme.MinSize(nm, 96, 0);                     // four glyphs of the name; grows with the text
            nm.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            row.AddChild(nm);
            var desc = MenuKit.Wrap("", UiTheme.Fs(20), MenuKit.Ash, 0f, MenuKit.UiFont);
            row.AddChild(desc);
            col.AddChild(row);
            _spRow[i] = row; _spName[i] = nm; _spDesc[i] = desc;
        }
        return frame;
    }

    // a signature skill's icon cell on the class sheet (design px): the icon keeps 12 px of rim on every side
    private const float SkillCell = 64f;

    /// <summary>
    /// The frame of a skill icon on the class sheet: a forged skill cell with a fine rim in the class colour
    /// (shared per colour); without the art, the tinted box of old.
    /// </summary>
    private static StyleBox SkillFrame(Color cc)
    {
        if (UiTheme.Art && UiArt.Has("skill_slot"))
            return UiTheme.ArtBox("menu_skill|" + cc.ToHtml(false), () =>
                new UiNine { Frame = UiArt.P("skill_slot"), Over1 = UiArt.P("slot_rarity"), Over1Mod = new Color(cc, 0.75f) }.Margins(12f, 12f, 12f, 12f));
        return MenuKit.Box(new Color(0.05f, 0.045f, 0.04f, 0.95f), new Color(cc, 0.55f), 1, 3, 12, 12);
    }

    private static Label KeyLabel(string text)
    {
        var l = MenuKit.Text(text, UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont);
        UiTheme.MinSize(l, 88, 0);                          // four glyphs of caption text; grows with the text
        l.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        return l;
    }

    private Control BuildIdentityPane()
    {
        var frame = MenuKit.Glass(0.76f);
        frame.Name = "Identity";
        MenuKit.Place(frame, 0, 0, 0, 1, Edge, PaneTop, Edge + IdentW, -PaneBottom);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto, FollowFocus = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        // fits the pane at every text size while the chosen slot is free; at the largest size the longer overwrite
        // notice makes the sheet scroll a little (it follows the focus)
        frame.AddChild(MenuKit.Pad(scroll, 26, 14, 16, 12));          // the scroll bar keeps clear of the pane's moulding
        var col = MenuKit.VBox(8);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(col);
        float w = IdentW - 26 - 16 - 16;

        col.AddChild(MenuKit.Text("銘記其名", UiTheme.Fs(44), MenuKit.Ivory, MenuKit.TitleFont));
        var subRow = MenuKit.HBox(14);
        _idClass = MenuKit.Text("", UiTheme.Fs(22), Palette.Ember, MenuKit.TitleFont);
        subRow.AddChild(_idClass);
        subRow.AddChild(MenuKit.Text("灰燼會記得每一個名字。", UiTheme.Fs(22), MenuKit.Ash, MenuKit.BodyFont));
        col.AddChild(subRow);
        col.AddChild(MenuKit.Rule(new Color(MenuKit.Line, 0.8f)));

        col.AddChild(MenuKit.Text("名字", UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont));
        var nameRow = MenuKit.HBox(10);
        _nameEdit = new LineEdit
        {
            MaxLength = MenuKit.NameMax, PlaceholderText = "為持燼者取名",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, ContextMenuEnabled = false, CaretBlink = true, SelectAllOnFocus = false,
        };
        UiTheme.MinSize(_nameEdit, 0, 54);
        if (UiTheme.Art && UiArt.Has("input") && UiArt.Has("input_focus"))
        {
            // the theme's forged text field, with a little more room for the larger name lettering
            _nameEdit.AddThemeStyleboxOverride("normal", UiTheme.ArtBox("input", 16f, 6f));
            _nameEdit.AddThemeStyleboxOverride("focus", UiTheme.ArtBox("input_focus", 16f, 6f));
        }
        else
        {
            _nameEdit.AddThemeStyleboxOverride("normal", MenuKit.Box(new Color(0.05f, 0.046f, 0.042f, 0.95f), MenuKit.Line, 1, 2, 14, 6));
            _nameEdit.AddThemeStyleboxOverride("focus", MenuKit.Box(new Color(0.07f, 0.06f, 0.05f, 0.97f), Palette.Ember, 1, 2, 14, 6));
        }
        var nameFont = MenuKit.TitleFont;
        if (nameFont != null) _nameEdit.AddThemeFontOverride("font", nameFont);
        _nameEdit.AddThemeFontSizeOverride("font_size", UiTheme.Fs(28));
        _nameEdit.AddThemeColorOverride("font_color", MenuKit.Ivory);
        _nameEdit.AddThemeColorOverride("font_placeholder_color", MenuKit.Faint);
        _nameEdit.AddThemeColorOverride("caret_color", Palette.Ember);
        _nameEdit.AddThemeColorOverride("selection_color", new Color(Palette.Ember, 0.35f));
        _nameEdit.TextChanged += _ => { _nameAuto = false; ValidateName(true); };
        _nameEdit.TextSubmitted += _ => { if (ValidateName(false) && _startBtn != null) _startBtn.GrabFocus(); };
        nameRow.AddChild(_nameEdit);
        nameRow.AddChild(Btn("換個名字", NextSuggestion, MenuTone.Normal, 22, 132f));
        col.AddChild(nameRow);
        // an empty label still keeps one line free for the message; a long message wraps instead of leaving the pane
        _nameMsg = MenuKit.Wrap("", UiTheme.Fs(20), new Color(0.96f, 0.46f, 0.38f), w, MenuKit.UiFont);
        col.AddChild(_nameMsg);

        _hcCheck = NewHardcoreCheck();
        col.AddChild(_hcCheck);
        _hcPanel = new PanelContainer();
        // the notice sits in a card (see SyncHardcore); its text keeps clear of the card's moulding
        bool hcCard = HardcoreCards;
        _hcText = MenuKit.Wrap(
            "專家模式下，死亡就是終結。持燼者一旦倒下，便永遠化為灰燼——這份存檔只能被悼念與刪除，無法再次喚醒。創建之後，此選擇不能更改。",
            UiTheme.Fs(22), MenuKit.Ash, w - (hcCard ? 44 : 32), MenuKit.BodyFont);
        _hcPanel.AddChild(hcCard ? MenuKit.Pad(_hcText, 22, 14, 22, 14) : MenuKit.Pad(_hcText, 16, 10, 16, 10));
        col.AddChild(_hcPanel);
        col.AddChild(MenuKit.Spacer(0, 2));

        col.AddChild(MenuKit.Text("存檔欄位", UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont));
        // four equal columns across the pane; each button holds two lines of caption text and grows with them
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            int slot = i + 1;
            var b = new Button
            {
                ToggleMode = true, FocusMode = FocusModeEnum.All, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseDefaultCursorShape = CursorShape.PointingHand,
            };
            UiTheme.MinSize(b, 0, 66);
            // forged plates: the chosen slot shows the ember face with its bright hairline
            if (!MenuKit.Plate(b, 8f, 4f, 8f, 4f))
            {
                b.AddThemeStyleboxOverride("normal", MenuKit.Box(MenuKit.RowBg, new Color(MenuKit.Line, 0.6f), 1, 2, 6, 4));
                b.AddThemeStyleboxOverride("hover", MenuKit.Box(new Color(0.12f, 0.10f, 0.085f, 0.95f), Palette.Ember.Darkened(0.3f), 1, 2, 6, 4));
                b.AddThemeStyleboxOverride("pressed", MenuKit.Box(new Color(0.22f, 0.12f, 0.05f, 0.97f), Palette.Ember, 2, 2, 6, 4));
                b.AddThemeStyleboxOverride("hover_pressed", MenuKit.Box(new Color(0.25f, 0.14f, 0.06f, 0.97f), Palette.Ember.Lightened(0.15f), 2, 2, 6, 4));
            }
            b.AddThemeStyleboxOverride("focus", MenuKit.FocusBox());
            var f = MenuKit.UiFont;
            if (f != null) b.AddThemeFontOverride("font", f);
            b.AddThemeFontSizeOverride("font_size", UiTheme.Fs(20));
            b.AddThemeColorOverride("font_pressed_color", MenuKit.Gold);
            b.AddThemeColorOverride("font_hover_pressed_color", MenuKit.Gold);
            b.Pressed += () => { MenuKit.Click(); _newSlot = slot; SyncIdentity(); };
            b.MouseEntered += MenuKit.Hover;
            grid.AddChild(b);
            _slotBtns[i] = b;
        }
        col.AddChild(grid);
        _slotMsg = MenuKit.Wrap("", UiTheme.Fs(20), MenuKit.Ash, w, MenuKit.UiFont);
        col.AddChild(_slotMsg);
        return frame;
    }

    private Button NewHardcoreCheck() =>
        MenuKit.Check("專家模式", _hardcore, on => { _hardcore = on; SyncHardcore(); }, UiTheme.Fs(24), MenuKit.Blood);

    /// <summary>
    /// MenuKit.Check draws its box and insets its label from the font size it was created with, so after a text-scale
    /// change a new tick box (same state, same place) replaces the old one.
    /// </summary>
    private void RenewHardcoreCheck()
    {
        var old = _hcCheck;
        if (old == null || !IsInstanceValid(old)) return;
        var parent = old.GetParent();
        if (parent == null) return;
        int at = old.GetIndex();
        bool focused = old.HasFocus();
        _hcCheck = NewHardcoreCheck();
        parent.AddChild(_hcCheck);
        parent.MoveChild(_hcCheck, at);
        parent.RemoveChild(old);
        old.QueueFree();
        if (focused) _hcCheck.GrabFocus();
    }

    private void ShowNewStep(bool identity)
    {
        if (_newRoot == null) return;
        _classPane.Visible = !identity;
        _classBar.Visible = !identity;
        _identPane.Visible = identity;
        _identBar.Visible = identity;
        _dragHint.Visible = !identity;
        _plate.Visible = identity;
        _dragArea.OffsetLeft = Edge + (identity ? IdentW : ListW) + 24f;
        SelectClass(_cls, false);
        if (identity)
        {
            RefreshSlots();
            SyncIdentity();
            SyncHardcore();
            if (_nameAuto) ApplySuggestedName();
            ValidateName(true);
        }
    }

    private void GoIdentity()
    {
        if (_busy) return;
        Go(Page.Identity);
    }

    private void SelectClass(ClassId c, bool flourish)
    {
        if (!MenuKit.ValidClass(c)) c = ClassId.Emberblade;
        bool changed = c != _cls || !_infoFilled;
        _cls = c;
        _clsPickedAt = _t;
        for (int i = 0; i < _classBtns.Length; i++) _classBtns[i]?.SetPressedNoSignal(i == (int)c);
        if (changed)
        {
            FillInfo(c);
            _infoFilled = true;
            if (_nameAuto) ApplySuggestedName();
        }
        if (_heroClass != (int)c) SetHero(c, flourish);
        _rimTarget = Names.ClassColor(c);
        UpdatePlate();
    }

    private void FillInfo(ClassId c)
    {
        if (_ciName == null) return;
        var blurb = MenuKit.Blurb(c);
        Color cc = Names.ClassColor(c);
        _ciName.Text = Names.Class(c);
        _ciName.AddThemeColorOverride("font_color", cc.Lerp(Colors.White, 0.12f));
        _ciRole.Text = blurb.Role;          // the weapons are listed from the loadout table under the description (可用武器)
        _ciDesc.Text = Names.ClassDesc(c);
        if (_ciWeapons != null) _ciWeapons.Text = string.Join("\n", ItemDb.LoadoutLines(c));
        _ciResName.Text = Names.Resource(c);
        _ciResName.AddThemeColorOverride("font_color", Names.ResourceColor(c).Lerp(Colors.White, 0.15f));
        _ciResNote.Text = blurb.ResourceNote;
        _ciStat.Text = StatInfo.Name(Names.MainStat(c));
        int d = Mathf.Clamp(blurb.Difficulty, 1, 3);
        _ciDiff.Text = new string('◆', d) + new string('◇', 3 - d) + "　" + MenuKit.DifficultyWord(d);
        _ciDiffNote.Text = blurb.DifficultyNote;
        if (_idClass != null) { _idClass.Text = Names.Class(c); _idClass.AddThemeColorOverride("font_color", cc.Lerp(Colors.White, 0.12f)); }

        var skills = MenuKit.SignatureSkills(c);
        for (int i = 0; i < 3; i++)
        {
            bool has = i < skills.Count;
            _skRow[i].Visible = has;
            if (!has) continue;
            var s = skills[i];
            _skName[i].Text = s.Name ?? "";
            _skMeta[i].Text = $"{Names.SkillCat(s.Cat)} · {Names.Element(s.Element)} · 等級 {s.UnlockLevel}";
            _skMeta[i].AddThemeColorOverride("font_color", Palette.Element(s.Element).Lerp(MenuKit.Faint, 0.45f));
            _skDesc[i].Text = MenuKit.SkillText(s);         // shown in full (no three-line cut any more, so no hover copy either)
            Texture2D tex = null;
            try { tex = Icons.ForSkill(s); }
            catch (Exception e) { Log.Debug("menu", "skill icon unavailable: " + e.Message); }
            _skIcon[i].Texture = tex;
            _skIcon[i].Modulate = Icons.Tint(tex, cc.Lerp(Colors.White, 0.35f));
            _skFrame[i].AddThemeStyleboxOverride("panel", SkillFrame(cc));
        }

        List<ClassSpec> specs;
        try { specs = SkillDb.SpecsOf(c).ToList(); }
        catch (Exception e) { Log.Error("menu", "class specialisations unavailable", e); specs = new List<ClassSpec>(); }
        for (int i = 0; i < 3; i++)
        {
            bool has = i < specs.Count;
            _spRow[i].Visible = has;
            if (!has) continue;
            _spName[i].Text = specs[i].Name ?? "";
            _spName[i].AddThemeColorOverride("font_color", cc.Lerp(Colors.White, 0.2f));
            _spDesc[i].Text = specs[i].Desc ?? "";
        }
    }

    private void OnHeroDrag(InputEvent e)
    {
        if (e is InputEventMouseMotion mm)
        {
            if ((mm.ButtonMask & MouseButtonMask.Left) != 0) { _heroYaw += mm.Relative.X * 0.012f; _spinHold = 2.5f; }
        }
        else if (e is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed) _spinHold = 2.5f;
    }

    // ── name, hardcore, slot ──

    private void ApplySuggestedName()
    {
        if (_nameEdit == null) return;
        var names = MenuKit.Blurb(_cls).Suggestions;
        if (names == null || names.Length == 0) return;
        _nameEdit.Text = names[((_suggest % names.Length) + names.Length) % names.Length];
        _nameEdit.CaretColumn = _nameEdit.Text.Length;
        _nameAuto = true;
        ValidateName(true);
    }

    private void NextSuggestion()
    {
        _suggest++;
        ApplySuggestedName();
        if (_nameEdit != null) { _nameEdit.GrabFocus(); _nameEdit.Edit(); _nameEdit.SelectAll(); }
    }

    /// <summary>Checks the typed name; shows the reason when it is not acceptable (an empty field stays quiet while typing).</summary>
    private bool ValidateName(bool quietWhenEmpty)
    {
        if (_nameEdit == null) return false;
        string name = MenuKit.CleanName(_nameEdit.Text);
        string err = MenuKit.NameError(name);
        if (_nameMsg != null) _nameMsg.Text = err == null || (quietWhenEmpty && name.Length == 0) ? "" : err;
        UpdatePlate();
        return err == null;
    }

    private void UpdatePlate()
    {
        if (_plate == null) return;
        string name = _nameEdit != null ? MenuKit.CleanName(_nameEdit.Text) : "";
        _plate.Text = name.Length == 0 ? Names.Class(_cls) : $"{name}　·　{Names.Class(_cls)}";
    }

    private static bool HardcoreCards => UiTheme.Art && UiArt.Has("card") && UiArt.Has("card_on");

    private void SyncHardcore()
    {
        if (_hcPanel == null) return;
        if (HardcoreCards)
        {
            // a quiet card while the mode is off; the lit card, steeped in blood, once it is chosen
            _hcPanel.AddThemeStyleboxOverride("panel", _hardcore
                ? UiTheme.ArtBox("menu_hc_on", () => new UiNine("card_on", "soot", 4f)
                    { FrameMod = new Color(1f, 0.50f, 0.44f), FieldMod = new Color(1f, 0.55f, 0.5f, 0.55f), Back = new Color(0.30f, 0.04f, 0.03f, 0.92f), BackInset = 4f })
                : UiTheme.ArtBox("menu_hc_off", () => new UiNine("card", "soot", 4f)
                    { FrameMod = new Color(0.82f, 0.66f, 0.63f, 0.9f), FieldMod = new Color(1f, 0.86f, 0.84f, 0.72f) }));
        }
        else
        {
            var bg = _hardcore ? new Color(0.16f, 0.035f, 0.028f, 0.92f) : new Color(0.06f, 0.04f, 0.036f, 0.80f);
            var edge = _hardcore ? MenuKit.Blood : MenuKit.Blood.Darkened(0.55f);
            _hcPanel.AddThemeStyleboxOverride("panel", MenuKit.Box(bg, edge, _hardcore ? 2 : 1, 2));
        }
        _hcText.AddThemeColorOverride("font_color", _hardcore ? new Color(0.98f, 0.72f, 0.66f) : MenuKit.Ash);
    }

    private void SyncIdentity()
    {
        if (_slotMsg == null) return;
        for (int i = 0; i < _slotBtns.Length; i++)
        {
            var b = _slotBtns[i];
            if (b == null) continue;
            int slot = i + 1;
            var s = SlotAt(slot);
            string second = s == null || !s.Exists ? "空欄位" : s.Corrupt ? "毀損" : string.IsNullOrEmpty(s.Name) ? "無名者" : s.Name;
            b.Text = $"欄位 {slot}\n{second}";
            bool used = s != null && s.Exists;
            b.TooltipText = used ? MenuKit.SlotLine(s) : "";          // the button cuts a long name short: hovering tells it in full
            // an empty slot stays quieter than a used one, but on the forged plates it needs more light than on the flat boxes
            Color idle = UiTheme.Art ? new Color(0.62f, 0.60f, 0.56f) : MenuKit.Faint;
            b.AddThemeColorOverride("font_color", used ? MenuKit.Ivory : idle);
            b.AddThemeColorOverride("font_hover_color", used ? Colors.White : UiTheme.Art ? MenuKit.Ivory : MenuKit.Ash);
            b.AddThemeColorOverride("font_focus_color", used ? Colors.White : UiTheme.Art ? MenuKit.Ivory : MenuKit.Ash);
            b.SetPressedNoSignal(slot == _newSlot);
        }
        var cur = SlotAt(_newSlot);
        if (cur == null || !cur.Exists)
        {
            _slotMsg.Text = $"欄位 {_newSlot} 是空的，新的旅途將記錄於此。";
            _slotMsg.AddThemeColorOverride("font_color", MenuKit.Ash);
        }
        else
        {
            _slotMsg.Text = $"欄位 {_newSlot} 已有{MenuKit.SlotLine(cur)}。開始前會再向你確認是否覆寫。";
            _slotMsg.AddThemeColorOverride("font_color", new Color(0.98f, 0.66f, 0.36f));
        }
    }

    private void TryStart()
    {
        if (_busy || _nameEdit == null) return;
        string name = MenuKit.CleanName(_nameEdit.Text);
        if (MenuKit.NameError(name) != null)
        {
            ValidateName(false);
            MenuKit.ErrorSound();
            _nameEdit.GrabFocus();
            _nameEdit.Edit();
            return;
        }
        RefreshSlots();
        var cur = SlotAt(_newSlot);
        if (cur != null && cur.Exists)
        {
            string what = cur.Corrupt ? "一份毀損的存檔" : MenuKit.SlotLine(cur);
            Ask("覆寫存檔", $"欄位 {_newSlot} 已有{what}。\n覆寫之後，原有的記錄將永遠消失，無法復原。", "覆寫並開始", "取消", () => Begin(name, true), MenuTone.Danger);
        }
        else Begin(name, false);
    }

    private void Begin(string name, bool overwrite)
    {
        if (_busy) return;
        int slot = _newSlot;
        var cls = _cls;
        bool hardcore = _hardcore;
        Log.Info("menu", $"new game: slot={slot} class={Names.ClassKey(cls)} hardcore={hardcore} overwrite={overwrite}");
        Leave("starting a new game", () =>
        {
            if (overwrite) SaveSystem.Delete(slot);       // also removes the old backup, so it can never resurface as this hero
            RememberSlot(slot);
            Game.I.NewGame(slot, name, cls, hardcore);
        });
    }

    // ───────────────────────── load ─────────────────────────

    private Control BuildLoad()
    {
        var root = new Control { Name = "Load", MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // the heading takes the height its text needs; the cards get the rest of the screen above the bottom bar
        var body = MenuKit.VBox(10);
        body.MouseFilter = MouseFilterEnum.Ignore;
        MenuKit.Place(body, 0, 0, 1, 1, Edge - 20, 54, -(Edge - 20), -104);
        root.AddChild(body);

        var head = MenuKit.VBox(2);
        head.MouseFilter = MouseFilterEnum.Ignore;
        var title = MenuKit.Text("載入遊戲", UiTheme.Fs(46), MenuKit.Ivory, MenuKit.TitleFont);
        MenuKit.Shadow(title, new Color(0, 0, 0, 0.85f), 9, 2);
        head.AddChild(title);
        head.AddChild(MenuKit.Text("八段旅途，八簇燼火。選一位持燼者，接續其路。", UiTheme.Fs(22), MenuKit.Ash, MenuKit.BodyFont));
        var headPad = MenuKit.Pad(head, 20, 0, 20, 0);
        headPad.MouseFilter = MouseFilterEnum.Ignore;
        body.AddChild(headPad);

        // two rows of four cards, centred while they fit; with larger text the rows grow taller and the grid scrolls
        // (the wheel works anywhere over it, and keyboard focus pulls the focused card into view)
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto, FollowFocus = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddChild(scroll);
        var center = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(center);
        _loadGrid = new GridContainer { Columns = 4 };
        _loadGrid.AddThemeConstantOverride("h_separation", 18);
        _loadGrid.AddThemeConstantOverride("v_separation", 18);
        center.AddChild(_loadGrid);

        var bar = MenuKit.HBox(16);
        bar.MouseFilter = MouseFilterEnum.Ignore;
        MenuKit.Place(bar, 0, 1, 1, 1, Edge, -98, -Edge, -42);
        _loadBack = Btn("返回", () => { if (!_busy) Go(Page.Title); }, MenuTone.Normal, 22, 170f);
        bar.AddChild(_loadBack);
        var hint = MenuKit.KeyHints(UiTheme.Fs(20), MenuKit.Faint, "[Enter]", "載入", "", "[Delete]", "刪除所選存檔", "", "[Esc]", "返回");
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hint.Alignment = BoxContainer.AlignmentMode.End;
        hint.Modulate = new Color(1f, 1f, 1f, 0.85f);
        bar.AddChild(hint);
        root.AddChild(bar);
        return root;
    }

    private Button FirstLoadButton()
    {
        if (_loadPrimary.TryGetValue(_focusSlot, out var b) && IsInstanceValid(b) && !b.Disabled) return b;
        if (_continueSlot > 0 && _loadPrimary.TryGetValue(_continueSlot, out b) && IsInstanceValid(b) && !b.Disabled) return b;
        for (int i = 1; i <= SaveSystem.SlotCount; i++)
            if (_loadPrimary.TryGetValue(i, out b) && IsInstanceValid(b) && !b.Disabled) return b;
        return _loadBack;
    }

    private void RebuildLoadGrid()
    {
        if (_loadGrid == null) return;
        foreach (var child in _loadGrid.GetChildren())
        {
            _loadGrid.RemoveChild(child);
            child.QueueFree();
        }
        _loadPrimary.Clear();
        foreach (var s in _slots) _loadGrid.AddChild(BuildSlotCard(s));
    }

    private static HBoxContainer CardRow(string key, string value, Color? valueColor = null)
    {
        var row = MenuKit.HBox(10);
        row.MouseFilter = MouseFilterEnum.Ignore;
        var k = MenuKit.Text(key, UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont);
        k.VerticalAlignment = VerticalAlignment.Center;
        UiTheme.MinSize(k, 92, 0);                          // four glyphs of caption text; grows with the text
        row.AddChild(k);
        // the value keeps to the rest of the card: what does not fit ends in an ellipsis and shows whole on hover
        var v = UiTheme.Trimmed(MenuKit.Text(value, UiTheme.Fs(22), valueColor ?? MenuKit.Ivory, MenuKit.UiFont));
        v.VerticalAlignment = VerticalAlignment.Center;
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(v);
        return row;
    }

    private static Control Badge(string text, Color c) => MenuKit.Badge(text, c, UiTheme.Fs(20));

    private Control BuildSlotCard(SlotInfo s)
    {
        int slot = s.Slot;
        bool empty = !s.Exists, corrupt = s.Exists && s.Corrupt, fallen = s.Exists && !s.Corrupt && s.Hardcore && s.Dead;
        Color cc = MenuKit.ClassColor(s.Class);

        // the card moulding over a soot field the backdrop still glows through: quieter for an empty slot, the lit
        // moulding while one of the card's buttons has the focus; without the art, the line boxes of old
        bool art = UiTheme.Art && UiArt.Has("card") && UiArt.Has("card_on");
        StyleBox normal, hot;
        if (art)
        {
            normal = empty
                ? UiTheme.ArtBox("menu_card_empty", () => new UiNine("card", "soot", 4f) { FrameMod = new Color(0.78f, 0.77f, 0.76f, 0.62f), FieldMod = new Color(1f, 1f, 1f, 0.52f) })
                : UiTheme.ArtBox("menu_card", () => new UiNine("card", "soot", 4f) { FieldMod = new Color(1f, 1f, 1f, 0.9f) });
            hot = UiTheme.ArtBox("menu_card_on", () => new UiNine("card_on", "soot", 4f) { FieldMod = new Color(1f, 1f, 1f, 0.95f) });
        }
        else
        {
            normal = MenuKit.Box(new Color(0.034f, 0.031f, 0.030f, empty ? 0.62f : 0.88f), new Color(MenuKit.Line, empty ? 0.45f : 0.8f), 1, 3);
            hot = MenuKit.Box(new Color(0.062f, 0.052f, 0.044f, 0.94f), Palette.Ember.Darkened(0.1f), 1, 3);
        }
        // the card keeps its width (four side by side); its body is at least as tall as a full hero card at the current
        // text size, so empty and used cards line up, and a card with more lines simply makes its row taller
        var card = new PanelContainer { Name = "Slot" + slot, MouseFilter = MouseFilterEnum.Pass };
        card.AddThemeStyleboxOverride("panel", normal);
        var col = MenuKit.VBox(5);
        col.MouseFilter = MouseFilterEnum.Ignore;
        UiTheme.MinSize(col, 0, CardBodyH);
        // the card's moulding is about 9 px thick: the text keeps clear of it, and two rows of cards (one with the
        // extra line of a fallen hero) still fit above the bottom bar at the standard text size
        float padX = art ? 24f : 20f;
        var pad = art ? MenuKit.Pad(col, 24, 16, 24, 16) : MenuKit.Pad(col, 20, 14, 20, 16);
        pad.CustomMinimumSize = new Vector2(CardW, 0);
        card.AddChild(pad);
        float textW = CardW - 2f * padX - 4f;               // wrap width of running text inside the card

        var top = MenuKit.HBox(8);
        top.MouseFilter = MouseFilterEnum.Ignore;
        var num = MenuKit.Text($"欄位 {slot}", UiTheme.Fs(20), MenuKit.Faint, MenuKit.UiFont);
        num.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(num);
        if (s.Exists && !s.Corrupt && s.Hardcore) top.AddChild(Badge("專家", MenuKit.Blood));
        if (fallen) top.AddChild(Badge("已殞落", new Color(0.62f, 0.60f, 0.58f)));
        if (corrupt) top.AddChild(Badge("毀損", MenuKit.Blood));
        col.AddChild(top);

        Button primary;
        Button delete = null;
        void Watch(Button b)
        {
            b.FocusEntered += () => { _focusSlot = slot; if (IsInstanceValid(card)) card.AddThemeStyleboxOverride("panel", hot); };
            b.FocusExited += () => { if (IsInstanceValid(card) && !card.IsQueuedForDeletion()) card.AddThemeStyleboxOverride("panel", normal); };
        }

        if (empty)
        {
            col.AddChild(MenuKit.Spacer(0, 44));
            var t = MenuKit.Text("空欄位", UiTheme.Fs(28), MenuKit.Faint, MenuKit.TitleFont);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(t);
            var n = MenuKit.Wrap("尚無持燼者在此留名。", UiTheme.Fs(22), MenuKit.Faint.Darkened(0.1f), textW, MenuKit.BodyFont);
            n.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(n);
            var gap = MenuKit.Spacer();
            gap.SizeFlagsVertical = SizeFlags.ExpandFill;
            col.AddChild(gap);
            primary = Btn("創建新的持燼者", () => { if (!_busy) StartNewGame(slot); }, MenuTone.Normal, 22, 0f);
            primary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            col.AddChild(primary);
        }
        else if (corrupt)
        {
            col.AddChild(MenuKit.Text("存檔毀損", UiTheme.Fs(30), new Color(0.96f, 0.46f, 0.38f), MenuKit.TitleFont));
            col.AddChild(MenuKit.Rule(new Color(MenuKit.Blood, 0.5f)));
            col.AddChild(MenuKit.Wrap("這份記錄與它的備份都已無法辨讀，像被雨水泡爛的名冊。", UiTheme.Fs(22), MenuKit.Ash, textW, MenuKit.BodyFont));
            col.AddChild(MenuKit.Wrap("刪除之後，欄位即可重新使用。", UiTheme.Fs(20), MenuKit.Faint, textW, MenuKit.UiFont));
            var gap = MenuKit.Spacer();
            gap.SizeFlagsVertical = SizeFlags.ExpandFill;
            col.AddChild(gap);
            primary = Btn("刪除毀損的存檔", () => AskDelete(s), MenuTone.Danger, 22, 0f);
            primary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            col.AddChild(primary);
        }
        else
        {
            // one line each inside the card: a long name or level line ends in an ellipsis and shows whole on hover
            var name = UiTheme.Trimmed(MenuKit.Text(string.IsNullOrEmpty(s.Name) ? "無名者" : s.Name, UiTheme.Fs(30), fallen ? MenuKit.Ash : MenuKit.Ivory, MenuKit.TitleFont));
            col.AddChild(name);
            var cls = UiTheme.Trimmed(MenuKit.Text(MenuKit.LevelLine(s.Class, s.Level, s.Paragon), UiTheme.Fs(22), fallen ? MenuKit.Faint : cc.Lerp(Colors.White, 0.15f), MenuKit.BodyFont));
            col.AddChild(cls);
            col.AddChild(MenuKit.Rule(new Color(fallen ? MenuKit.Faint : cc, 0.45f)));
            col.AddChild(CardRow("世界階級", MenuKit.TierName(s.Tier)));
            col.AddChild(CardRow(fallen ? "殞落之地" : "所在", MenuKit.AreaName(s.Area)));
            col.AddChild(CardRow("遊玩時間", MenuKit.PlayTime(s.PlayTime)));
            col.AddChild(CardRow("最後儲存", string.IsNullOrEmpty(s.SavedAt) ? "不明" : s.SavedAt, MenuKit.Ash));
            if (fallen)
            {
                // wraps instead of being cut when the text is large
                var epitaph = MenuKit.Wrap("其名已刻入灰燼，無法再被喚醒。", UiTheme.Fs(20), new Color(0.86f, 0.52f, 0.46f), textW, MenuKit.BodyFont);
                col.AddChild(epitaph);
            }
            var gap = MenuKit.Spacer();
            gap.SizeFlagsVertical = SizeFlags.ExpandFill;
            col.AddChild(gap);
            var row = MenuKit.HBox(10);
            primary = Btn(fallen ? "已化為灰燼" : "繼續旅途", () => LoadSlot(s), fallen ? MenuTone.Normal : MenuTone.Primary, 22, 0f);
            primary.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (fallen) { primary.Disabled = true; primary.FocusMode = FocusModeEnum.None; primary.MouseDefaultCursorShape = CursorShape.Arrow; }
            row.AddChild(primary);
            delete = Btn("刪除", () => AskDelete(s), MenuTone.Danger, 22, 96f);
            row.AddChild(delete);
            col.AddChild(row);
        }

        Watch(primary);
        if (delete != null) Watch(delete);
        _loadPrimary[slot] = primary.Disabled && delete != null ? delete : primary;

        // a double click anywhere on the card does what its main button does
        card.GuiInput += e =>
        {
            if (_busy || _modal != null) return;
            if (e is InputEventMouseButton mb && mb.Pressed && mb.DoubleClick && mb.ButtonIndex == MouseButton.Left)
            {
                if (empty) StartNewGame(slot);
                else if (!corrupt && !fallen) { MenuKit.Click(); LoadSlot(s); }
            }
        };
        return card;
    }

    // ───────────────────────── settings ─────────────────────────

    private Control BuildSettings()
    {
        var root = new Control { Name = "Settings", MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddChild(center);
        _settings = new SettingsView(() => { if (!_busy) { MenuKit.CloseSound(); Go(Page.Title); } }, OnSettingsApplied);
        center.AddChild(_settings.Root);
        return root;
    }

    private void OnSettingsApplied()
    {
        TuneBackdrop(false);
        SyncCrackleVolume();
    }

    // ───────────────────────── credits ─────────────────────────

    private Control BuildCredits()
    {
        var root = new Control { Name = "Credits", MouseFilter = MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _creditsScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            FollowFocus = false,
        };
        MenuKit.Place(_creditsScroll, 0.5f, 0, 0.5f, 1, -520, 40, 520, -110);
        root.AddChild(_creditsScroll);

        var col = MenuKit.VBox(0);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.MouseFilter = MouseFilterEnum.Pass;
        _creditsScroll.AddChild(col);
        col.AddChild(MenuKit.Spacer(0, 760));                       // the roll rises from below the screen

        string bb;
        try { bb = CreditsData.BbCode(); }
        catch (Exception e)
        {
            Log.Error("menu", "credits could not be built", e);
            bb = $"[center]{CreditsData.GameTitle}　{CreditsData.GameTitleLatin}[/center]";
        }
        var text = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, SelectionEnabled = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(1040, 0),
            MouseFilter = MouseFilterEnum.Pass, SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var body = MenuKit.BodyFont;
        if (body != null) { text.AddThemeFontOverride("normal_font", body); text.AddThemeFontOverride("bold_font", MenuKit.TitleFont ?? body); }
        UiTheme.RichSize(text, UiTheme.FsBody);
        text.AddThemeColorOverride("default_color", MenuKit.Ivory);
        text.AddThemeConstantOverride("line_separation", 7);
        // the roll carries its own [font_size] tags (headings, sub-headings, small print): through the text scale, once, here
        text.Text = UiTheme.Bb(bb);
        col.AddChild(text);
        col.AddChild(MenuKit.Spacer(0, 620));

        // the roll fades in and out at the edges of the column
        var topFade = MenuKit.Stretched(MenuKit.Fade(true, (0f, new Color(0.012f, 0.010f, 0.010f, 0.96f)), (1f, new Color(0.012f, 0.010f, 0.010f, 0f))));
        MenuKit.Place(topFade, 0, 0, 1, 0, 0, 0, 0, 190);
        root.AddChild(topFade);
        // fully dark by the line where the roll is clipped (150 of these 260 px), so lines dissolve instead of being cut in half
        var bottomFade = MenuKit.Stretched(MenuKit.Fade(true, (0f, new Color(0.012f, 0.010f, 0.010f, 0f)), (0.55f, new Color(0.012f, 0.010f, 0.010f, 0.98f)), (1f, new Color(0.012f, 0.010f, 0.010f, 0.98f))));
        MenuKit.Place(bottomFade, 0, 1, 1, 1, 0, -260, 0, 0);
        root.AddChild(bottomFade);

        var bar = MenuKit.HBox(16);
        bar.MouseFilter = MouseFilterEnum.Ignore;
        MenuKit.Place(bar, 0, 1, 1, 1, Edge, -98, -Edge, -42);
        _creditsBack = Btn("返回", () => { if (!_busy) Go(Page.Title); }, MenuTone.Normal, 22, 170f);
        bar.AddChild(_creditsBack);
        var hint = MenuKit.KeyHints(UiTheme.Fs(20), MenuKit.Faint, "滾輪或", "[↑]", "[↓]", "捲動", "", "[Esc]", "返回");
        hint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hint.Alignment = BoxContainer.AlignmentMode.End;
        hint.Modulate = new Color(1f, 1f, 1f, 0.85f);
        bar.AddChild(hint);
        root.AddChild(bar);
        return root;
    }

    private float CreditsMax()
    {
        var bar = _creditsScroll.GetVScrollBar();
        return bar == null ? 0f : Mathf.Max(0f, (float)bar.MaxValue - _creditsScroll.Size.Y);
    }

    private void ApplyCreditsScroll()
    {
        if (_creditsScroll == null) return;
        _creditsPos = Mathf.Clamp(_creditsPos, 0f, Mathf.Max(CreditsMax(), 0f));
        _creditsScroll.ScrollVertical = (int)_creditsPos;
    }

    private void TickCredits(float dt)
    {
        int cur = _creditsScroll.ScrollVertical;
        if (Mathf.Abs(cur - (int)_creditsPos) > 2) { _creditsPos = cur; _creditsHold = 2.5f; }     // the player scrolled by hand
        float max = CreditsMax();
        if (max <= 1f) return;
        if (_creditsHold > 0f) _creditsHold -= dt;
        else _creditsPos += dt * UiTheme.Px(48f);                                                   // about 1.1 lines of the roll per second at every text size
        if (_creditsPos >= max) { _creditsPos = 0f; _creditsHold = 0.4f; }                          // the roll starts over
        _creditsScroll.ScrollVertical = (int)_creditsPos;
    }

    // ───────────────────────── backdrop: construction ─────────────────────────

    private void BuildBackdrop()
    {
        var black = new ColorRect { Name = "Black", Color = new Color(0.008f, 0.008f, 0.010f), MouseFilter = MouseFilterEnum.Ignore };
        black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(black);

        var world = new World3D();
        _vp = new SubViewport
        {
            Name = "Backdrop", World3D = world, TransparentBg = false, HandleInputLocally = false, GuiDisableInput = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Size = _vpSize, UseDebanding = true,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Fxaa, Msaa3D = Viewport.Msaa.Disabled, PositionalShadowAtlasSize = 2048,
        };
        AddChild(_vp);
        _scene = new Node3D { Name = "Scene" };
        _vp.AddChild(_scene);

        BuildEnvironment(world);
        BuildSet();
        BuildFire();
        BuildAir();
        BuildStage();

        _cam = new Camera3D { Name = "Camera", Fov = 40f, Near = 0.15f, Far = 600f, Current = true };
        _camAttr = new CameraAttributesPractical { DofBlurFarEnabled = false, DofBlurFarDistance = 34f, DofBlurFarTransition = 40f, DofBlurAmount = 0.07f };
        _cam.Attributes = _camAttr;
        _scene.AddChild(_cam);

        _vpRect = new TextureRect
        {
            Name = "BackdropView", Texture = _vp.GetTexture(), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore,
        };
        _vpRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_vpRect);
        Log.Guard("menu", BuildLair);       // the title's own backdrop, drawn over the stage until creation fades it out

        TuneBackdrop(true);
        UpdateCamera(0f);
    }

    private void BuildEnvironment(World3D world)
    {
        var skyMat = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.010f, 0.012f, 0.020f), SkyHorizonColor = new Color(0.082f, 0.094f, 0.125f), SkyCurve = 0.09f,
            GroundBottomColor = new Color(0.008f, 0.008f, 0.010f), GroundHorizonColor = new Color(0.066f, 0.075f, 0.100f), GroundCurve = 0.05f,
            SunAngleMax = 0f, SunCurve = 0.01f,
        };
        var sky = new Sky { SkyMaterial = skyMat, RadianceSize = Sky.RadianceSizeEnum.Size32, ProcessMode = Sky.ProcessModeEnum.Incremental };
        _env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky, Sky = sky,
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.22f, 0.27f, 0.38f), AmbientLightEnergy = 0.30f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = 1.05f, TonemapWhite = 5f,
            GlowEnabled = true, GlowIntensity = 0.9f, GlowStrength = 1.05f, GlowBloom = 0.10f, GlowHdrThreshold = 0.95f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Screen,
            FogEnabled = true, FogLightColor = new Color(0.050f, 0.058f, 0.078f), FogLightEnergy = 1f, FogSunScatter = 0f,
            FogDensity = 0.018f, FogSkyAffect = 0.3f, FogAerialPerspective = 0f,
            VolumetricFogEnabled = false, VolumetricFogDensity = 0.014f, VolumetricFogAlbedo = new Color(0.78f, 0.82f, 0.90f),
            VolumetricFogEmission = new Color(0.20f, 0.26f, 0.38f), VolumetricFogEmissionEnergy = 0.03f, VolumetricFogAnisotropy = 0.45f,
            VolumetricFogLength = 64f, VolumetricFogAmbientInject = 0.4f, VolumetricFogSkyAffect = 0.3f,
            SsaoEnabled = false,
            AdjustmentEnabled = true, AdjustmentContrast = 1.06f, AdjustmentSaturation = 1.08f,
        };
        world.Environment = _env;

        // the last of the dusk: a dim, cold light from beyond the stones that only shapes silhouettes
        var dusk = new DirectionalLight3D
        {
            Name = "Dusk", LightColor = new Color(0.42f, 0.52f, 0.74f), LightEnergy = 0.16f, ShadowEnabled = false, LightSpecular = 0.1f,
            LightVolumetricFogEnergy = 0f, SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly,
            Transform = new Transform3D(Basis.LookingAt(new Vector3(0.35f, -0.55f, 0.75f).Normalized(), Vector3.Up), Vector3.Zero),
        };
        _scene.AddChild(dusk);
    }

    private MeshInstance3D Put(Mesh mesh, Material mat, Vector3 pos, Vector3 rotDeg, Vector3 scale, bool shadow = true)
    {
        var mi = new MeshInstance3D
        {
            Mesh = mesh, MaterialOverride = mat, Position = pos, RotationDegrees = rotDeg, Scale = scale,
            CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _scene.AddChild(mi);
        return mi;
    }

    private void BuildSet()
    {
        var rng = new Random(20241);
        float R() => (float)rng.NextDouble();

        // shared procedural surfaces: a cracked relief for stone and ash, and a soft mottling so nothing is one flat grey
        var relief = new NoiseTexture2D
        {
            Width = 512, Height = 512, Seamless = true, AsNormalMap = true, BumpStrength = 6f,
            Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.03f, FractalOctaves = 3, Seed = 5 },
        };
        var mottle = new NoiseTexture2D
        {
            Width = 512, Height = 512, Seamless = true,
            Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.012f, FractalOctaves = 5, Seed = 9 },
            ColorRamp = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color(0.55f, 0.55f, 0.56f), new Color(1f, 1f, 1f) } },
        };
        var ground = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.105f, 0.098f, 0.092f), AlbedoTexture = mottle, Roughness = 0.94f, Metallic = 0f,
            NormalEnabled = true, NormalTexture = relief, NormalScale = 1.1f,
            Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = new Vector3(0.20f, 0.20f, 0.20f),
        };
        var stone = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.185f, 0.178f, 0.170f), AlbedoTexture = mottle, Roughness = 0.90f, Metallic = 0f,
            NormalEnabled = true, NormalTexture = relief, NormalScale = 1.4f,
            Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = new Vector3(0.34f, 0.34f, 0.34f),
        };
        var iron = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.075f, 0.068f, 0.062f), Roughness = 0.48f, Metallic = 0.85f,
            NormalEnabled = true, NormalTexture = relief, NormalScale = 0.5f,
            Uv1Triplanar = true, Uv1WorldTriplanar = true, Uv1Scale = new Vector3(0.9f, 0.9f, 0.9f),
        };
        var far = new StandardMaterial3D { AlbedoColor = new Color(0.03f, 0.03f, 0.034f), Roughness = 1f, Metallic = 0f };
        _coalMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.05f, 0.02f, 0.01f), Roughness = 1f, EmissionEnabled = true, Emission = new Color(1f, 0.80f, 0.58f),
            EmissionEnergyMultiplier = 3.4f,
            EmissionTexture = new NoiseTexture2D
            {
                Width = 256, Height = 256, Seamless = true,
                Noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Frequency = 0.06f, Seed = 3 },
                ColorRamp = new Gradient
                {
                    Offsets = new[] { 0f, 0.45f, 0.8f, 1f },
                    Colors = new[] { new Color(0.02f, 0f, 0f), new Color(0.75f, 0.14f, 0.02f), new Color(1f, 0.55f, 0.12f), new Color(1f, 0.9f, 0.55f) },
                },
            },
            EmissionOperator = BaseMaterial3D.EmissionOperatorEnum.Multiply, Uv1Scale = new Vector3(2f, 2f, 2f),
        };
        _fallbackMat = stone;

        Vector3 one = Vector3.One, zero = Vector3.Zero;
        Put(new PlaneMesh { Size = new Vector2(280, 280) }, ground, zero, zero, one, false);

        // the brazier: three worn steps, a pedestal, a wide iron bowl on struts, glowing coals
        float[] stepR = { 4.5f, 3.7f, 2.9f };
        for (int i = 0; i < stepR.Length; i++)
            Put(new CylinderMesh { TopRadius = stepR[i], BottomRadius = stepR[i] + 0.14f, Height = 0.22f, RadialSegments = 12, Rings = 1 },
                stone, new Vector3(0, 0.11f + 0.22f * i, 0), new Vector3(0, 13f * i, 0), one);
        Put(new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.85f, Height = 1.3f, RadialSegments = 8, Rings = 1 }, stone, new Vector3(0, 1.31f, 0), zero, one);
        Put(new CylinderMesh { TopRadius = 1.78f, BottomRadius = 0.72f, Height = 0.86f, RadialSegments = 16, Rings = 1 }, iron, new Vector3(0, 2.39f, 0), zero, one);
        Put(new TorusMesh { InnerRadius = 1.70f, OuterRadius = 1.90f, Rings = 24, RingSegments = 8 }, iron, new Vector3(0, 2.82f, 0), zero, one);
        Put(new CylinderMesh { TopRadius = 1.62f, BottomRadius = 1.62f, Height = 0.08f, RadialSegments = 16, Rings = 1 }, _coalMat, new Vector3(0, 2.77f, 0), zero, one, false);
        var lump = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 8, Rings = 5 };
        for (int i = 0; i < 9; i++)
        {
            float a = R() * Mathf.Tau, r = 0.2f + R() * 1.1f, s = 0.42f + R() * 0.4f;
            Put(lump, _coalMat, new Vector3(Mathf.Cos(a) * r, 2.82f + R() * 0.08f, Mathf.Sin(a) * r), new Vector3(R() * 90f, R() * 360f, 0), new Vector3(s, s * 0.7f, s), false);
        }
        var strut = new BoxMesh { Size = new Vector3(0.16f, 0.16f, 1f) };
        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.Pi * 0.25f + i * Mathf.Pi * 0.5f;
            var from = new Vector3(Mathf.Cos(a) * 2.0f, 0.66f, Mathf.Sin(a) * 2.0f);
            var to = new Vector3(Mathf.Cos(a) * 1.15f, 2.25f, Mathf.Sin(a) * 1.15f);
            var dir = to - from;
            var basis = Basis.LookingAt(dir.Normalized(), Vector3.Up);
            basis.Z *= dir.Length();
            var mi = new MeshInstance3D { Mesh = strut, MaterialOverride = iron, Transform = new Transform3D(basis, (from + to) * 0.5f) };
            _scene.AddChild(mi);
        }

        // standing stones: a horseshoe around the fire, open toward the viewer
        var slab = new CylinderMesh { TopRadius = 0.40f, BottomRadius = 0.60f, Height = 1f, RadialSegments = 5, Rings = 1 };
        const int inner = 9;
        for (int i = 0; i < inner; i++)
        {
            float a = Mathf.DegToRad(Mathf.Lerp(80f, 280f, i / (float)(inner - 1)) + (R() - 0.5f) * 9f);
            float r = 9.4f + R() * 2.4f;
            bool stump = i % 4 == 2;
            float h = stump ? 1.5f + R() * 1.1f : 4.0f + R() * 3.2f;
            float w = 1.5f + R() * 0.9f;
            Put(slab, stone, new Vector3(Mathf.Sin(a) * r, h * 0.5f - 0.25f, Mathf.Cos(a) * r),
                new Vector3((R() - 0.5f) * (stump ? 22f : 9f), R() * 360f, (R() - 0.5f) * (stump ? 22f : 9f)), new Vector3(w, h, w * (0.7f + R() * 0.4f)));
        }
        const int outer = 13;
        for (int i = 0; i < outer; i++)
        {
            float a = Mathf.DegToRad(Mathf.Lerp(96f, 264f, i / (float)(outer - 1)) + (R() - 0.5f) * 8f);
            float r = 19f + R() * 12f, h = 7f + R() * 7.5f, w = 2.4f + R() * 2.0f;
            Put(slab, stone, new Vector3(Mathf.Sin(a) * r, h * 0.5f - 0.4f, Mathf.Cos(a) * r),
                new Vector3((R() - 0.5f) * 7f, R() * 360f, (R() - 0.5f) * 7f), new Vector3(w, h, w * (0.7f + R() * 0.5f)));
        }

        // rubble, kept clear of the hero's dais and of the camera's lane
        var block = new BoxMesh { Size = Vector3.One };
        for (int i = 0; i < 30; i++)
        {
            float a = R() * Mathf.Tau, r = 5.2f + R() * 12f;
            var p = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
            float s = 0.25f + R() * 0.75f;
            if (new Vector2(p.X - Stage.X, p.Z - Stage.Z).Length() < 2.4f) continue;
            if (p.Z > 8.5f && Mathf.Abs(p.X) < 5.5f) continue;
            Put(block, stone, p + new Vector3(0, s * 0.18f, 0), new Vector3(R() * 40f - 20f, R() * 360f, R() * 40f - 20f), new Vector3(s * (0.8f + R() * 0.9f), s * 0.6f, s));
        }

        // the far land: a low, broken skyline that the fog almost swallows
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * Mathf.Tau + R() * 0.2f, r = 92f + R() * 30f, h = 7f + R() * 9f, w = 30f + R() * 30f;
            Put(slab, far, new Vector3(Mathf.Sin(a) * r, h * 0.5f - 2f, Mathf.Cos(a) * r), new Vector3(0, R() * 360f, (R() - 0.5f) * 5f), new Vector3(w, h, w), false);
        }

        // one beacon tower still burning, very far away: a single warm point above the skyline
        float ta = Mathf.DegToRad(200f);
        var towerAt = new Vector3(Mathf.Sin(ta) * 180f, 0f, Mathf.Cos(ta) * 180f);
        Put(new CylinderMesh { TopRadius = 3.0f, BottomRadius = 6.2f, Height = 44f, RadialSegments = 6, Rings = 1 }, far, towerAt + new Vector3(0, 22f, 0), zero, one, false);
        Put(block, far, towerAt + new Vector3(0, 45.4f, 0), new Vector3(0, 30f, 0), new Vector3(8.4f, 2.8f, 8.4f), false);
        var spark = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(3.4f, 1.6f, 0.55f), DisableFog = true,
        };
        Put(new SphereMesh { Radius = 1.1f, Height = 2.2f, RadialSegments = 8, Rings = 4 }, spark, towerAt + new Vector3(0, 48.4f, 0), zero, one, false);
    }

    private void BuildFire()
    {
        Node3D fire = null;
        try { fire = Fx.Fire(2.6f, false); }
        catch (Exception e) { Log.Warn("menu", "Fx.Fire is unavailable, the brazier uses the menu's own flames: " + e.Message); }
        if (fire != null && IsInstanceValid(fire))
        {
            fire.GetParent()?.RemoveChild(fire);
            _scene.AddChild(fire);
            fire.Position = FirePos;
        }
        else _scene.AddChild(OwnFlames());

        _fireLightPos = FirePos + new Vector3(0, 0.75f, 0);
        _fireLight = new OmniLight3D
        {
            Name = "FireLight", Position = _fireLightPos, LightColor = new Color(1f, 0.52f, 0.20f), LightEnergy = 7.5f, OmniRange = 30f,
            OmniAttenuation = 1.15f, ShadowEnabled = true, ShadowBias = 0.06f, ShadowBlur = 2.2f, LightSpecular = 0.6f, LightVolumetricFogEnergy = 1.6f,
        };
        _scene.AddChild(_fireLight);
    }

    private static StandardMaterial3D Sprite(bool additive, Color tint)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles, BillboardKeepScale = true, VertexColorUseAsAlbedo = true,
            AlbedoColor = tint, AlbedoTexture = MenuKit.SoftDot, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DisableReceiveShadows = true, DisableFog = true,
        };
    }

    private static GradientTexture1D Ramp(params (float at, Color c)[] stops)
    {
        return new GradientTexture1D
        {
            Gradient = new Gradient { Offsets = stops.Select(s => s.at).ToArray(), Colors = stops.Select(s => s.c).ToArray() },
        };
    }

    private static CurveTexture ScaleCurve(params (float at, float v)[] points)
    {
        var c = new Curve();
        foreach (var (at, v) in points) c.AddPoint(new Vector2(at, v));
        return new CurveTexture { Curve = c };
    }

    private GpuParticles3D Emit(string name, int amount, double lifetime, ParticleProcessMaterial pm, Material sprite, Vector3 pos, Aabb bounds, double preprocess)
    {
        var p = new GpuParticles3D
        {
            Name = name, Amount = amount, Lifetime = lifetime, Preprocess = preprocess, ProcessMaterial = pm, Position = pos,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Material = sprite }, VisibilityAabb = bounds,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _scene.AddChild(p);
        return p;
    }

    /// <summary>Flames built here, used when the effects module cannot provide its fire.</summary>
    private Node3D OwnFlames()
    {
        var root = new Node3D { Name = "Flames", Position = FirePos };
        var pm = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.8f,
            Direction = Vector3.Up, Spread = 14f, InitialVelocityMin = 1.4f, InitialVelocityMax = 3.0f, Gravity = new Vector3(0, 1.6f, 0),
            ScaleMin = 1.0f, ScaleMax = 1.9f, ScaleCurve = ScaleCurve((0f, 0.45f), (0.18f, 1f), (1f, 0.15f)),
            AngleMin = -180f, AngleMax = 180f,
            ColorRamp = Ramp((0f, new Color(1f, 0.95f, 0.7f, 0f)), (0.15f, new Color(1f, 0.82f, 0.42f, 0.9f)), (0.5f, new Color(1f, 0.45f, 0.10f, 0.75f)),
                (0.82f, new Color(0.6f, 0.12f, 0.02f, 0.3f)), (1f, new Color(0.2f, 0.03f, 0f, 0f))),
            TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.6f, TurbulenceNoiseScale = 2.2f,
        };
        var p = new GpuParticles3D
        {
            Name = "Flame", Amount = 60, Lifetime = 1.15, Preprocess = 1.5, ProcessMaterial = pm,
            DrawPass1 = new QuadMesh { Size = Vector2.One, Material = Sprite(true, new Color(1.6f, 1.2f, 0.9f)) },
            VisibilityAabb = new Aabb(new Vector3(-4, -1, -4), new Vector3(8, 9, 8)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        root.AddChild(p);
        return root;
    }

    private void BuildAir()
    {
        var emberRamp = Ramp((0f, new Color(1f, 0.9f, 0.6f, 0f)), (0.08f, new Color(1f, 0.75f, 0.35f, 1f)), (0.6f, new Color(1f, 0.38f, 0.08f, 0.9f)), (1f, new Color(0.5f, 0.08f, 0f, 0f)));
        var emberSprite = Sprite(true, new Color(2.6f, 1.5f, 0.7f));

        // sparks torn off the fire
        Emit("Sparks", 70, 3.2, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.9f,
            Direction = Vector3.Up, Spread = 22f, InitialVelocityMin = 1.6f, InitialVelocityMax = 3.6f, Gravity = new Vector3(0.15f, 0.5f, 0.05f),
            ScaleMin = 0.035f, ScaleMax = 0.08f, ColorRamp = emberRamp,
            TurbulenceEnabled = true, TurbulenceNoiseStrength = 1.4f, TurbulenceNoiseScale = 2.5f,
        }, emberSprite, FirePos, new Aabb(new Vector3(-8, -2, -8), new Vector3(16, 16, 16)), 3.0);

        // embers adrift over the whole stage
        Emit("Embers", 120, 9.0, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(14f, 3.5f, 14f),
            Direction = new Vector3(0.3f, 1f, 0.1f), Spread = 60f, InitialVelocityMin = 0.15f, InitialVelocityMax = 0.6f, Gravity = new Vector3(0.06f, 0.08f, 0f),
            ScaleMin = 0.03f, ScaleMax = 0.06f, ColorRamp = emberRamp,
            TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.9f, TurbulenceNoiseScale = 3.5f,
        }, emberSprite, new Vector3(0, 3.6f, 3f), new Aabb(new Vector3(-24, -6, -24), new Vector3(48, 20, 48)), 9.0);

        // ash, falling without end
        Emit("Ash", 170, 12.0, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(18f, 0.5f, 18f),
            Direction = Vector3.Down, Spread = 25f, InitialVelocityMin = 0.3f, InitialVelocityMax = 0.7f, Gravity = new Vector3(0.10f, -0.12f, 0.04f),
            ScaleMin = 0.03f, ScaleMax = 0.075f,
            ColorRamp = Ramp((0f, new Color(1, 1, 1, 0f)), (0.1f, new Color(1, 1, 1, 0.6f)), (0.85f, new Color(1, 1, 1, 0.5f)), (1f, new Color(1, 1, 1, 0f))),
            TurbulenceEnabled = true, TurbulenceNoiseStrength = 0.5f, TurbulenceNoiseScale = 4f,
        }, Sprite(false, new Color(0.36f, 0.37f, 0.41f)), new Vector3(0, 9.5f, 4f), new Aabb(new Vector3(-26, -14, -26), new Vector3(52, 18, 52)), 12.0);

        // low fog banks crawling across the ground
        Emit("Mist", 14, 16.0, new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(17f, 0.3f, 15f),
            Direction = new Vector3(1f, 0f, 0.3f), Spread = 35f, InitialVelocityMin = 0.12f, InitialVelocityMax = 0.32f, Gravity = Vector3.Zero,
            ScaleMin = 6f, ScaleMax = 11f,
            ColorRamp = Ramp((0f, new Color(1, 1, 1, 0f)), (0.3f, new Color(1, 1, 1, 0.075f)), (0.7f, new Color(1, 1, 1, 0.075f)), (1f, new Color(1, 1, 1, 0f))),
        }, Sprite(false, new Color(0.34f, 0.41f, 0.55f)), new Vector3(0, 0.8f, 2f), new Aabb(new Vector3(-40, -8, -40), new Vector3(80, 20, 80)), 16.0);
    }

    private void BuildStage()
    {
        var stoneMat = _fallbackMat;
        Put(new CylinderMesh { TopRadius = 1.30f, BottomRadius = 1.44f, Height = StageH, RadialSegments = 14, Rings = 1 }, stoneMat,
            Stage + new Vector3(0, StageH * 0.5f, 0), Vector3.Zero, Vector3.One);
        _ringMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.04f, 0.03f, 0.02f), Roughness = 0.6f, EmissionEnabled = true, Emission = Palette.Ember, EmissionEnergyMultiplier = 0f,
        };
        Put(new TorusMesh { InnerRadius = 1.20f, OuterRadius = 1.27f, Rings = 32, RingSegments = 6 }, _ringMat,
            Stage + new Vector3(0, StageH + 0.005f, 0), Vector3.Zero, Vector3.One, false);

        _heroPivot = new Node3D { Name = "HeroPivot", Position = Stage + new Vector3(0, StageH, 0), Visible = false };
        GearCatalog.PreloadShowcase();         // the eight signature looks load in the background: switching classes never stalls
        _scene.AddChild(_heroPivot);

        // a plain statue stands in when no character model can be loaded at all
        _heroFallback = new Node3D { Name = "Statue", Visible = false };
        _heroFallback.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.34f, Height = 1.45f, RadialSegments = 12, Rings = 4 }, MaterialOverride = stoneMat, Position = new Vector3(0, 0.74f, 0) });
        _heroFallback.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.21f, Height = 0.42f, RadialSegments = 12, Rings = 6 }, MaterialOverride = stoneMat, Position = new Vector3(0, 1.62f, 0) });
        _heroPivot.AddChild(_heroFallback);

        Vector3 keyPos = Stage + new Vector3(-2.4f, 3.2f, 4.4f), keyAt = Stage + new Vector3(0, 1.15f, 0);
        _keyLight = new SpotLight3D
        {
            Name = "Key", LightColor = new Color(1f, 0.95f, 0.87f), LightEnergy = 0f, SpotRange = 10f, SpotAngle = 38f, SpotAttenuation = 0.8f,
            SpotAngleAttenuation = 0.6f, ShadowEnabled = false, LightVolumetricFogEnergy = 0f, Visible = false,
            Transform = new Transform3D(Basis.LookingAt((keyAt - keyPos).Normalized(), Vector3.Up), keyPos),
        };
        _scene.AddChild(_keyLight);
        Vector3 rimPos = Stage + new Vector3(2.1f, 2.7f, -3.2f), rimAt = Stage + new Vector3(0, 1.25f, 0);
        _rimLight = new SpotLight3D
        {
            Name = "Rim", LightColor = Palette.Ember, LightEnergy = 0f, SpotRange = 9f, SpotAngle = 40f, SpotAttenuation = 0.7f,
            SpotAngleAttenuation = 0.5f, ShadowEnabled = false, LightVolumetricFogEnergy = 0f, Visible = false,
            Transform = new Transform3D(Basis.LookingAt((rimAt - rimPos).Normalized(), Vector3.Up), rimPos),
        };
        _scene.AddChild(_rimLight);
        // the hero, not the room: a soft neutral fill from the other front side and a second rim from behind on the left,
        // so dark armour reads and the silhouette is cut clean against the dark background
        Vector3 fillPos = Stage + new Vector3(2.7f, 1.7f, 3.8f), fillAt = Stage + new Vector3(0, 1.1f, 0);
        _fillLight = new SpotLight3D
        {
            Name = "Fill", LightColor = new Color(0.86f, 0.9f, 1f), LightEnergy = 0f, SpotRange = 10f, SpotAngle = 36f, SpotAttenuation = 0.8f,
            SpotAngleAttenuation = 0.6f, ShadowEnabled = false, LightVolumetricFogEnergy = 0f, Visible = false,
            Transform = new Transform3D(Basis.LookingAt((fillAt - fillPos).Normalized(), Vector3.Up), fillPos),
        };
        _scene.AddChild(_fillLight);
        Vector3 rim2Pos = Stage + new Vector3(-2.3f, 2.6f, -3.0f);
        _rim2Light = new SpotLight3D
        {
            Name = "Rim2", LightColor = new Color(0.92f, 0.95f, 1f), LightEnergy = 0f, SpotRange = 9f, SpotAngle = 34f, SpotAttenuation = 0.7f,
            SpotAngleAttenuation = 0.5f, ShadowEnabled = false, LightVolumetricFogEnergy = 0f, Visible = false,
            Transform = new Transform3D(Basis.LookingAt((rimAt - rim2Pos).Normalized(), Vector3.Up), rim2Pos),
        };
        _scene.AddChild(_rim2Light);

        // a puff of class-coloured sparks when the hero on the dais changes
        _swapBurstMat = new ParticleProcessMaterial
        {
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.55f,
            Direction = Vector3.Up, Spread = 70f, InitialVelocityMin = 0.8f, InitialVelocityMax = 2.6f, Gravity = new Vector3(0, 0.4f, 0),
            ScaleMin = 0.03f, ScaleMax = 0.07f, Color = Palette.Ember,
            ColorRamp = Ramp((0f, new Color(1, 1, 1, 1f)), (0.6f, new Color(1, 1, 1, 0.8f)), (1f, new Color(1, 1, 1, 0f))),
        };
        _swapBurst = new GpuParticles3D
        {
            Name = "SwapBurst", Amount = 44, Lifetime = 1.0, OneShot = true, Explosiveness = 0.92f, Emitting = false, ProcessMaterial = _swapBurstMat,
            Position = Stage + new Vector3(0, StageH + 0.9f, 0), DrawPass1 = new QuadMesh { Size = Vector2.One, Material = Sprite(true, new Color(2.4f, 2.4f, 2.4f)) },
            VisibilityAabb = new Aabb(new Vector3(-5, -3, -5), new Vector3(10, 10, 10)), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _scene.AddChild(_swapBurst);
    }

    private void BuildCrackle()
    {
        var g = Game.I;
        bool mute = false;
        try { mute = g != null && (g.Arg("mute") || g.Arg("--mute")); } catch (Exception) { }
        if (mute) return;
        string path = null;
        foreach (var p in new[] { "res://assets/audio/sfx/fire_loop.wav", "res://assets/audio/sfx/fire_loop_1.wav", "res://assets/audio/sfx/torch_loop.wav" })
            if (ResourceLoader.Exists(p)) { path = p; break; }
        if (path == null) return;
        var stream = GD.Load<AudioStream>(path);
        if (stream == null) return;
        _crackle = new AudioStreamPlayer { Name = "Crackle", Stream = stream };
        foreach (var bus in new[] { "Ambience", "Amb", "Ambient", "Sfx", "SFX" })
            if (AudioServer.GetBusIndex(bus) >= 0) { _crackle.Bus = bus; break; }
        AddChild(_crackle);
        _crackle.Finished += () => { if (IsInstanceValid(_crackle) && IsVisibleInTree()) _crackle.Play(); };
        SyncCrackleVolume();
        _crackle.Play();
    }

    private void SyncCrackleVolume()
    {
        if (_crackle == null) return;
        var cfg = Game.I?.Cfg;
        float v = 0.34f;
        // on a dedicated bus the audio module already applies the player's volumes; on Master we apply the ambience level here
        if (cfg != null && _crackle.Bus.ToString() == "Master")
        {
            v *= cfg.Ambience;
            if (AudioServer.GetBusVolumeDb(0) > -0.05f) v *= cfg.Master;
        }
        _crackle.VolumeDb = Mathf.LinearToDb(Mathf.Max(v, 0.0001f));
    }

    // ───────────────────────── backdrop: per frame ─────────────────────────

    /// <summary>Sizes the backdrop to the window and applies the graphics settings that matter for it.</summary>
    private void TuneBackdrop(bool force)
    {
        if (_vp == null) return;
        var win = GetTree().Root.Size;
        if (win.X < 64 || win.Y < 64) return;
        var cfg = Game.I?.Cfg;
        int q = Mathf.Clamp(cfg?.Quality ?? 2, 0, 3);
        float rs = cfg?.RenderScale ?? 0f;
        if (!force && win == _winSize && q == _quality && Mathf.IsEqualApprox(rs, _scaleCfg)) return;
        _winSize = win; _quality = q; _scaleCfg = rs;

        var size = new Vector2I(Math.Min(win.X, 3840), Math.Min(win.Y, 2160));
        _vpSize = size;
        _vp.Size = size;
        float target = q == 0 ? 1080f : 1440f;
        float scale = rs > 0.01f ? Mathf.Clamp(rs, 0.4f, 1f) : Mathf.Clamp(target / size.Y, 0.5f, 1f);
        _vp.Scaling3DMode = scale < 0.99f ? Viewport.Scaling3DModeEnum.Fsr : Viewport.Scaling3DModeEnum.Bilinear;
        _vp.Scaling3DScale = scale;
        _vp.FsrSharpness = 0.3f;
        _vp.Msaa3D = q >= 3 ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled;
        _vp.PositionalShadowAtlasSize = q >= 2 ? 4096 : 2048;
        if (_env != null)
        {
            _env.VolumetricFogEnabled = q >= 1;
            _env.FogDensity = q >= 1 ? 0.016f : 0.024f;        // without the volumetric pass the plain fog carries the mood alone
            _env.SsaoEnabled = q >= 2;
        }
        if (_fireLight != null) _fireLight.OmniShadowMode = q >= 2 ? OmniLight3D.ShadowMode.Cube : OmniLight3D.ShadowMode.DualParaboloid;
        if (_camAttr != null) _camAttr.DofBlurFarEnabled = q >= 2;
        TuneLair(size, scale, q);
        Log.Info("menu", $"backdrop {size.X}x{size.Y} render scale {scale:0.00} quality {q}");
    }

    private void UpdateCamera(float dt)
    {
        if (_cam == null) return;
        _camBlend = Mathf.MoveToward(_camBlend, _camBlendTarget, dt * 0.85f);
        float k = _camBlend * _camBlend * (3f - 2f * _camBlend);
        float t = _t;

        // title: a slow pendulum around the brazier
        float a = 0.26f * Mathf.Sin(t * 0.045f + 0.6f) + 0.05f * Mathf.Sin(t * 0.13f);
        float r = 13.8f + 0.9f * Mathf.Sin(t * 0.06f + 1.3f);
        var posA = new Vector3(Mathf.Sin(a) * r, 2.75f + 0.3f * Mathf.Sin(t * 0.08f), Mathf.Cos(a) * r);
        var tgtA = new Vector3(0f, 2.55f, 0f);
        // creation: a portrait of the hero with the fire over one shoulder
        float hh = _heroHeight;
        var tgtB = Stage + new Vector3(0f, StageH + hh * 0.54f, 0f);
        var posB = Stage + new Vector3(0.18f * Mathf.Sin(t * 0.21f), StageH + hh * 0.70f + 0.05f * Mathf.Sin(t * 0.17f), 3.45f * hh);

        var pos = posA.Lerp(posB, k);
        var tgt = tgtA.Lerp(tgtB, k);
        float fov = Mathf.Lerp(40f, 30f, k);
        var dir = tgt - pos;
        float dist = dir.Length();
        if (dist < 0.01f) return;
        _cam.Transform = new Transform3D(Basis.LookingAt(dir / dist, Vector3.Up), pos);
        _cam.Fov = fov;

        // shift the frame sideways so the subject sits where the interface leaves room for it
        float viewW = Size.X > 1f ? Size.X : 1920f;
        float left = Edge + (_page == Page.Identity ? IdentW : ListW) + 24f, right = viewW - Edge - InfoW - 24f;
        _heroFracTarget = Mathf.Clamp((left + right) * 0.5f / viewW, 0.3f, 0.6f);
        _heroFrac = Mathf.MoveToward(_heroFrac, _heroFracTarget, dt * 0.25f);
        float aspect = _vpSize.Y > 0 ? _vpSize.X / (float)_vpSize.Y : 16f / 9f;
        float visW = 2f * dist * Mathf.Tan(Mathf.DegToRad(fov) * 0.5f) * aspect;
        float frac = Mathf.Lerp(0.665f, _heroFrac, k);
        _cam.HOffset = -(frac - 0.5f) * visW;

        if (_camAttr != null)
        {
            _camAttr.DofBlurFarDistance = Mathf.Lerp(34f, dist + 2.2f, k);
            _camAttr.DofBlurFarTransition = Mathf.Lerp(40f, 3.5f, k);
            _camAttr.DofBlurAmount = Mathf.Lerp(0.07f, 0.12f, k);
        }
    }

    private void UpdateStage(float dt)
    {
        float t = _t;
        float k = _camBlend * _camBlend * (3f - 2f * _camBlend);

        // the fire breathes
        if (_fireLight != null)
        {
            float f = 1f + 0.10f * Mathf.Sin(t * 9.7f) * Mathf.Sin(t * 3.1f) + 0.06f * Mathf.Sin(t * 23.3f + 1.7f) + 0.05f * Mathf.Sin(t * 14.1f);
            _fireLight.LightEnergy = 7.5f * f;
            _fireLight.Position = _fireLightPos + new Vector3(0.05f * Mathf.Sin(t * 7.3f), 0.06f * Mathf.Sin(t * 11.9f), 0.05f * Mathf.Cos(t * 6.1f));
            if (_coalMat != null) _coalMat.EmissionEnergyMultiplier = 3.4f * (0.82f + 0.36f * (f - 0.78f) / 0.44f);
        }

        // stage lights follow the camera blend and the chosen class
        _rimColor = _rimColor.Lerp(_rimTarget, 1f - Mathf.Exp(-6f * dt));
        _rimPulse = Mathf.MoveToward(_rimPulse, 0f, dt * 2.2f);
        bool lit = k > 0.01f;
        // the hero is lit in neutral light (true colours) and cut out by strong near-white rims; the class colour is only an
        // accent (the floor ring, the sparks, a faint tint in one rim). The room behind is pushed back while the stage is up.
        if (_keyLight != null) { _keyLight.Visible = lit; _keyLight.LightEnergy = 4.4f * k; }
        if (_fillLight != null) { _fillLight.Visible = lit; _fillLight.LightEnergy = 1.5f * k; }
        if (_rimLight != null)
        {
            _rimLight.Visible = lit;
            _rimLight.LightColor = new Color(0.95f, 0.95f, 0.95f).Lerp(_rimColor, 0.2f);
            _rimLight.LightEnergy = (9.5f + 6f * _rimPulse) * k;
        }
        if (_rim2Light != null) { _rim2Light.Visible = lit; _rim2Light.LightEnergy = 7.5f * k; }
        if (_fireLight != null) { _fireLight.LightEnergy *= 1f - 0.7f * k; _fireLight.LightVolumetricFogEnergy = 1.6f * (1f - 0.85f * k); }
        if (_env != null) _env.AmbientLightEnergy = 0.30f * (1f - 0.45f * k);
        if (_ringMat != null)
        {
            _ringMat.Emission = _rimColor;
            _ringMat.EmissionEnergyMultiplier = (1.3f + 2.2f * _rimPulse + 0.25f * Mathf.Sin(t * 1.7f)) * k;
        }

        if (_heroPivot != null && _heroPivot.Visible)
        {
            // a chosen hero faces the lens three-quarter on; after a pause (or a drag) it sways gently about that view
            // (±35°) instead of spinning, so its back is never shown for long. A dragged figure eases back onto the sway.
            if (_spinHold > 0f) _spinHold -= dt;
            else
            {
                _swayT += dt;
                float sway = HeroFrontYaw + Mathf.DegToRad(35f) * Mathf.Sin(_swayT * 0.32f);
                _heroYaw = Mathf.Lerp(_heroYaw, sway, 1f - Mathf.Exp(-dt * 2.5f));
            }
            if (_heroYaw > Mathf.Pi) _heroYaw -= Mathf.Tau;
            else if (_heroYaw < -Mathf.Pi) _heroYaw += Mathf.Tau;
            _heroPivot.Rotation = new Vector3(0f, _heroYaw, 0f);
            if (_heroIdleAt > 0f && t >= _heroIdleAt)
            {
                _heroIdleAt = -1f;
                if (_hero != null && IsInstanceValid(_hero)) _hero.Play(_hero.Gear != null ? _hero.StanceIdle : "Idle_Loop", 1f, 0.3f);
            }
        }
    }

    /// <summary>Puts the chosen class's model on the dais (hero_&lt;classKey&gt;, idle, class-coloured rim).</summary>
    private void SetHero(ClassId c, bool flourish)
    {
        if (_heroPivot == null) return;
        _heroYaw = HeroFrontYaw; _spinHold = 3f; _swayT = 0f;        // every class is first seen from the front, three-quarter on
        ulong t0 = Time.GetTicksUsec();
        _heroClass = (int)c;
        _heroIdleAt = -1f;
        if (_hero != null)
        {
            if (IsInstanceValid(_hero)) { _heroPivot.RemoveChild(_hero); _hero.QueueFree(); }
            _hero = null;
        }
        Color cc = Names.ClassColor(c);
        CharModel m = null;
        try { m = CharModel.Create("hero_" + Names.ClassKey(c)); }
        catch (Exception e) { Log.Error("menu", $"hero preview for {Names.ClassKey(c)} failed", e); }

        bool usable = m != null && m.GetChildCount() > 0;
        if (usable)
        {
            _heroPivot.AddChild(m);
            _hero = m;
            _heroHeight = Mathf.Clamp(m.Height, 1.3f, 2.4f);
            m.SetRim(new Color(0.5f, 0.5f, 0.5f).Lerp(cc, 0.2f));        // a neutral edge: the class colour is not the hero's light
            m.SetGear(GearSet.Showcase(c));             // the class's signature look (t4 armour, its own weapon at v3): display only, never the new hero's gear
            Log.Info("menu", $"class preview {Names.ClassKey(c)}: {m.GearReport} built in {(Time.GetTicksUsec() - t0) / 1000.0:0.0} ms");
            m.Play(m.Gear != null ? m.StanceIdle : "Idle_Loop");
            string anim = MenuKit.Blurb(c).Flourish;
            if (flourish && !string.IsNullOrEmpty(anim) && m.Has(anim))
            {
                m.Play(anim, 1f, 0.12f, true);
                _heroIdleAt = _t + Mathf.Max(0.4f, m.Len(anim) - 0.1f);
            }
        }
        else
        {
            if (m != null) m.QueueFree();
            _heroHeight = 1.8f;
        }
        if (_heroFallback != null) _heroFallback.Visible = !usable;

        if (flourish)
        {
            _rimPulse = 1f;
            if (_swapBurst != null && _swapBurstMat != null && _heroPivot.Visible)
            {
                _swapBurstMat.Color = cc.Lerp(Colors.White, 0.25f);
                _swapBurst.Restart();
                _swapBurst.Emitting = true;
            }
        }
    }
}
