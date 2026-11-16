using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// Panel "death": what the hero sees after falling. A quiet line, the killer when it is known, and the way on —
/// rekindle at the checkpoint or return to 殘燈鎮. A hardcore hero gets a memorial card instead, and only the road
/// back to the title screen. The world keeps moving behind the veil (the panel does not pause the game).
/// </summary>
public partial class DeathUi : UiPanel
{
    public override string Id => "death";
    public override bool Pauses => false;
    public override bool Fullscreen => true;

    // ── who struck the fatal blow: noted from the damage stream, because the Died event carries no source ──
    private static bool _fatalKnown, _fatalElite, _fatalBoss, _fatalDot;
    private static string _fatalName;
    private static Element _fatalEl;

    static DeathUi()
    {
        World.Damage += OnWorldDamage;
    }

    private static void OnWorldDamage(Actor target, Hit h, float amount)
    {
        // raised after life was reduced and before the death is processed: life at or below zero marks the fatal hit
        if (target == null || !ReferenceEquals(target, World.P) || target.Life > 0f) return;
        try
        {
            var src = h.Source;
            _fatalKnown = true;
            _fatalDot = h.Dot;
            _fatalEl = h.El;
            if (src == null || ReferenceEquals(src, target)) { _fatalName = null; _fatalElite = _fatalBoss = false; return; }
            _fatalName = src.DisplayName;
            _fatalElite = src.IsElite;
            _fatalBoss = src.IsBoss;
        }
        catch (Exception) { _fatalName = null; }
    }

    private static void ClearFatal()
    {
        _fatalKnown = false;
        _fatalName = null;
        _fatalElite = _fatalBoss = _fatalDot = false;
    }

    private static readonly string[] Lines =
    {
        "灰燼落定，四下無聲。",
        "火光晃了一下，然後是漫長的黑。",
        "沒有鐘聲為你而鳴——寂靜早已帶走了它們。",
        "你倒下的地方，餘溫尚在。",
        "灰暮不為任何人停步。",
        "又一縷燼火，沒入了灰暮。",
        "風把你的名字吹散，像吹散一捧灰。",
        "遠方的燼塔仍亮著。它還在等。",
    };

    private static readonly string[] FinalLines =
    {
        "這一次，沒有人把火重新點起。",
        "灰燼收下了這個名字，不再歸還。",
        "路在這裡斷了。後來的人會看見腳印。",
    };

    private bool _built, _open, _done, _reopen, _openedAlive, _hardcore, _faulted;
    private object _lastArg;
    private int _lineIdx;
    private double _openFor;
    private Tween _tween;
    private Control _veil, _col, _buttons, _memorial, _memorialRow, _gap;
    private Label _title, _line, _killer, _where, _hope;
    private Label _mName, _mClass, _mPlace, _mCause, _mTier, _mTime, _mDeeds, _mDate;
    private Button _respawn, _town, _toTitle;

    public override void _Ready()
    {
        base._Ready();
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        try { Build(); }
        catch (Exception e) { Log.Error("ui", "building the death screen failed", e); }
    }

    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // the veil: near-black with a trace of old blood, darker toward the edges
        var veil = new Control { Name = "Veil", MouseFilter = MouseFilterEnum.Ignore };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var wash = new ColorRect { Color = new Color(0.035f, 0.008f, 0.006f, 0.80f), MouseFilter = MouseFilterEnum.Ignore };
        wash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        veil.AddChild(wash);
        veil.AddChild(MenuKit.Stretched(MenuKit.Fade(true,
            (0f, new Color(0, 0, 0, 0.85f)), (0.3f, new Color(0, 0, 0, 0.1f)), (0.7f, new Color(0, 0, 0, 0.1f)), (1f, new Color(0, 0, 0, 0.9f)))));
        veil.AddChild(MenuKit.Stretched(MenuKit.Fade(false,
            (0f, new Color(0, 0, 0, 0.7f)), (0.25f, new Color(0, 0, 0, 0f)), (0.75f, new Color(0, 0, 0, 0f)), (1f, new Color(0, 0, 0, 0.7f)))));
        AddChild(veil);
        _veil = veil;

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var col = MenuKit.VBox(10);
        col.MouseFilter = MouseFilterEnum.Ignore;
        col.CustomMinimumSize = new Vector2(900, 0);
        center.AddChild(col);
        _col = col;

        _title = MenuKit.Text("燼火已熄", UiTheme.Fs(104), new Color(0.80f, 0.76f, 0.70f), MenuKit.Spaced(MenuKit.BlackFont, 22, 22, 18));
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.AddThemeColorOverride("font_outline_color", new Color(0.26f, 0.03f, 0.02f, 1f));
        _title.AddThemeConstantOverride("outline_size", 4);
        MenuKit.Shadow(_title, new Color(0, 0, 0, 0.8f), 12, 5);
        col.AddChild(_title);

        // under the title: the tapering rule with its stone, steeped in blood; a plain red line without the art
        var gemRule = MenuKit.Ornament("title_rule", 20f, new Color(1f, 0.42f, 0.36f));
        if (gemRule != null) col.AddChild(gemRule);
        else
        {
            var rule = MenuKit.Stretched(MenuKit.Fade(false,
                (0f, new Color(0.7f, 0.12f, 0.08f, 0f)), (0.5f, new Color(0.7f, 0.12f, 0.08f, 0.9f)), (1f, new Color(0.7f, 0.12f, 0.08f, 0f))));
            var ruleBox = new Control { CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore };
            ruleBox.AddChild(rule);
            col.AddChild(MenuKit.Pad(ruleBox, 180, 0, 180, 6));
        }

        _line = MenuKit.Text("", UiTheme.Fs(30), MenuKit.Ash, MenuKit.BodyFont);
        _line.HorizontalAlignment = HorizontalAlignment.Center;
        MenuKit.Shadow(_line, new Color(0, 0, 0, 0.8f), 6, 2);
        col.AddChild(_line);

        _killer = MenuKit.Text("", UiTheme.Fs(26), MenuKit.Ivory, MenuKit.BodyFont);
        _killer.HorizontalAlignment = HorizontalAlignment.Center;
        MenuKit.Shadow(_killer, new Color(0, 0, 0, 0.8f), 6, 2);
        col.AddChild(_killer);

        _where = MenuKit.Text("", UiTheme.Fs(UiTheme.FsRow), MenuKit.Faint, MenuKit.UiFont);
        _where.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_where);

        // the memorial card: the crest of its frame rises above the top edge, so the card keeps that much room under
        // the line before it (the row hides with the card, taking the room with it)
        _memorial = BuildMemorial();
        var memorialRow = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        memorialRow.AddChild(_memorial);
        _memorialRow = UiTheme.Art ? MenuKit.Pad(memorialRow, 0, 28, 0, 0) : memorialRow;
        _memorialRow.MouseFilter = MouseFilterEnum.Ignore;
        col.AddChild(_memorialRow);

        // the breath before the way on; shorter under the memorial card, which fills the screen at the largest text size
        _gap = MenuKit.Spacer(0, 26);
        col.AddChild(_gap);

        _hope = MenuKit.Text("燼火尚未放棄你。", UiTheme.Fs(UiTheme.FsBody), new Color(0.92f, 0.62f, 0.34f), MenuKit.BodyFont);
        _hope.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_hope);

        var buttons = MenuKit.HBox(22);
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        _respawn = MenuKit.Btn("重生", DoRespawn, MenuTone.Primary, UiTheme.Fs(28), UiTheme.Px(260f));
        _respawn.TooltipText = "在最近的檢查點重新燃起";
        buttons.AddChild(_respawn);
        _town = MenuKit.Btn("返回殘燈鎮", DoTown, MenuTone.Normal, UiTheme.Fs(26), UiTheme.Px(260f));
        buttons.AddChild(_town);
        _toTitle = MenuKit.Btn("返回標題畫面", DoTitle, MenuTone.Normal, UiTheme.Fs(26), UiTheme.Px(300f));
        buttons.AddChild(_toTitle);
        col.AddChild(buttons);
        _buttons = buttons;
    }

    private Control BuildMemorial()
    {
        var frame = MenuKit.Frame(true);
        UiTheme.MinSize(frame, 740f, 0f);
        frame.MouseFilter = MouseFilterEnum.Ignore;
        var col = MenuKit.VBox(6);
        col.MouseFilter = MouseFilterEnum.Ignore;
        // the frame brings 28 px above and below already; the heading still stands clear of the crest's pendant (27 px)
        frame.AddChild(UiTheme.Art ? MenuKit.Pad(col, 40, 14, 40, 16) : MenuKit.Pad(col, 40, 26, 40, 26));

        var head = MenuKit.HBox(10);
        head.Alignment = BoxContainer.AlignmentMode.Center;
        var tag = MenuKit.Text("悼　念", UiTheme.Fs(UiTheme.FsRow), MenuKit.Faint, MenuKit.UiFont);
        head.AddChild(tag);
        head.AddChild(MenuKit.Badge("專家模式", MenuKit.Blood, UiTheme.Fs(UiTheme.FsCaption), new Color(0.98f, 0.62f, 0.55f)));
        col.AddChild(head);

        _mName = MenuKit.Text("", UiTheme.Fs(48), MenuKit.Ivory, MenuKit.BlackFont);
        _mName.HorizontalAlignment = HorizontalAlignment.Center;
        _mName.ClipText = true;
        col.AddChild(_mName);
        _mClass = MenuKit.Text("", UiTheme.Fs(UiTheme.FsBody), MenuKit.Ash, MenuKit.BodyFont);
        _mClass.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_mClass);
        col.AddChild(MenuKit.Spacer(0, 2));
        col.AddChild(MenuKit.Rule(new Color(MenuKit.Line, 0.9f)));
        col.AddChild(MenuKit.Spacer(0, 2));

        _mPlace = MemorialRow(col, "殞落之地");
        _mCause = MemorialRow(col, "死因");
        _mTier = MemorialRow(col, "世界階級");
        _mTime = MemorialRow(col, "遊玩時間");
        _mDeeds = MemorialRow(col, "生前討伐");
        _mDate = MemorialRow(col, "殞落之日");

        col.AddChild(MenuKit.Spacer(0, 4));
        var epitaph = MenuKit.Text("其名已刻入灰燼，再不會被喚醒。", UiTheme.Fs(UiTheme.FsRow), new Color(0.88f, 0.56f, 0.50f), MenuKit.BodyFont);
        epitaph.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(epitaph);
        return frame;
    }

    private static Label MemorialRow(Container parent, string key)
    {
        var row = MenuKit.HBox(16);
        row.MouseFilter = MouseFilterEnum.Ignore;
        var k = MenuKit.Text(key, UiTheme.Fs(UiTheme.FsCaption), MenuKit.Faint, MenuKit.UiFont);
        UiTheme.MinSize(k, 160f, 0f);
        k.HorizontalAlignment = HorizontalAlignment.Right;
        k.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(k);
        var v = MenuKit.Text("", UiTheme.Fs(UiTheme.FsRow), MenuKit.Ivory, MenuKit.BodyFont);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.AutowrapMode = TextServer.AutowrapMode.WordSmart;       // a long line (the tally of kills) wraps instead of being cut
        row.AddChild(v);
        parent.AddChild(row);
        return v;
    }

    // ───────────────────────── panel ─────────────────────────

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        _open = true;
        _done = false;
        _openFor = 0;
        _lastArg = arg;
        if (_col == null) return;

        var p = World.P;
        _openedAlive = p == null || !IsInstanceValid(p) || !p.Dead;     // opened by hand (test harness): do not auto-close
        bool again = _reopen;
        _reopen = false;
        if (!again) _lineIdx = Rng.Range(0, 1000);                       // one line per death, kept across refreshes
        Fill(arg);

        _tween?.Kill();
        _tween = null;
        if (again)
        {
            // something closed the screen while the hero still lay dead; it returns as it was
            _veil.Modulate = Colors.White;
            _col.Modulate = Colors.White;
            _buttons.Modulate = Colors.White;
            EnableButtons();
            return;
        }

        SetButtonsEnabled(false);
        _veil.Modulate = new Color(1, 1, 1, 0);
        _col.Modulate = new Color(1, 1, 1, 0);
        _buttons.Modulate = new Color(1, 1, 1, 0);
        _tween = CreateTween();
        _tween.TweenProperty(_veil, "modulate:a", 1f, 1.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        _tween.Parallel().TweenProperty(_col, "modulate:a", 1f, 1.1f).SetDelay(0.5f);
        _tween.TweenProperty(_buttons, "modulate:a", 1f, 0.45f);
        _tween.TweenCallback(Callable.From(EnableButtons));

        var d = Game.I?.Hero?.D;
        Log.Info("ui", $"death screen: hardcore={_hardcore} killer={(string.IsNullOrEmpty(_killer.Text) ? "unknown" : "known")} area={Game.I?.Level?.AreaId ?? d?.Area ?? "?"} deaths={d?.Deaths ?? 0}");
    }

    public override void OnClose()
    {
        _open = false;
        _tween?.Kill();
        _tween = null;
        if (_done) return;
        // closed by something other than our own buttons (Esc, a blanket close): if the hero is still dead, return
        var arg = _lastArg;
        Callable.From(() =>
        {
            var g = Game.I;
            if (g == null || g.Ui == null || g.State != Game.GameState.Play || _done) return;
            var p = World.P;
            if (p == null || !IsInstanceValid(p) || !p.Dead) return;
            if (g.Ui.IsOpen(Id)) return;
            _reopen = true;
            g.Ui.Open(Id, arg);
        }).CallDeferred();
    }

    public override void Refresh()
    {
        if (_open && _col != null) Fill(_lastArg);
    }

    public override void _Input(InputEvent e)
    {
        base._Input(e);
        if (!_open || !IsVisibleInTree()) return;
        // while the hero lies dead, no hotkey may open other panels, close this one or call the town portal
        if (e is InputEventKey k && k.Pressed)
        {
            var key = k.Keycode;
            bool allowed = key == Key.Enter || key == Key.KpEnter || key == Key.Space || key == Key.Left || key == Key.Right
                || key == Key.Up || key == Key.Down || key == Key.F12 || key == Key.Print;
            if (!allowed) GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!_open || _done || _faulted || _openedAlive) return;
        try
        {
            _openFor += delta;
            if (_openFor < 0.5) return;
            // revived or taken elsewhere by other means (test bot, a scripted respawn): the screen has no more to say
            var g = Game.I;
            var p = World.P;
            bool gone = g == null || g.State != Game.GameState.Play || p == null || !IsInstanceValid(p) || !p.Dead;
            if (!gone) return;
            _done = true;
            ClearFatal();
            g?.Ui?.Close(Id);
        }
        catch (Exception e)
        {
            _faulted = true;
            Log.Error("ui", "death screen update failed", e);
        }
    }

    // ───────────────────────── content ─────────────────────────

    private static string KillerName(object arg)
    {
        switch (arg)
        {
            case string s when !string.IsNullOrWhiteSpace(s): return s.Trim();
            case Actor a when !string.IsNullOrEmpty(a.DisplayName): return a.DisplayName;
            case Hit h when h.Source != null && !string.IsNullOrEmpty(h.Source.DisplayName): return h.Source.DisplayName;
        }
        return _fatalKnown && !string.IsNullOrEmpty(_fatalName) ? _fatalName : null;
    }

    private void Fill(object arg)
    {
        var g = Game.I;
        var d = g?.Hero?.D;
        _hardcore = d != null && d.Hardcore;

        string killer = KillerName(arg);
        bool fromTracker = killer != null && _fatalKnown && killer == _fatalName;
        string cause;
        if (killer == null) cause = _fatalKnown && _fatalDot ? $"{Names.Element(_fatalEl)}的侵蝕" : null;
        else if (fromTracker && _fatalDot) cause = $"「{killer}」留下的{Names.Element(_fatalEl)}傷勢";
        else cause = $"「{killer}」";

        var level = g?.Level;
        string areaId = level?.AreaId ?? d?.Area;
        string place = level != null && level.IsRift ? $"深淵裂隙 · 第 {Math.Max(level.RiftTier, 1)} 層" : MenuKit.AreaName(areaId);

        _title.Text = _hardcore ? "永眠於灰燼" : "燼火已熄";
        var pool = _hardcore ? FinalLines : Lines;
        _line.Text = pool[Math.Abs(_lineIdx) % pool.Length];

        if (cause == null) { _killer.Text = ""; _killer.Visible = false; }
        else
        {
            _killer.Visible = !_hardcore;
            _killer.Text = killer != null && fromTracker && (_fatalBoss || _fatalElite) && !_fatalDot
                ? $"你倒在{(_fatalBoss ? "強敵" : "菁英")}{cause}面前"
                : $"奪去你性命的是{cause}";
        }

        _where.Visible = !_hardcore && d != null;
        if (d != null) _where.Text = $"{place}　·　{MenuKit.LevelLine(d.Class, d.Level, d.Paragon)}";

        _memorial.Visible = _hardcore;
        if (_memorialRow != null) _memorialRow.Visible = _hardcore;
        if (_gap != null) _gap.CustomMinimumSize = new Vector2(0, _hardcore ? 8 : 26);
        if (_hardcore && d != null)
        {
            _mName.Text = string.IsNullOrEmpty(d.Name) ? "無名者" : d.Name;
            _mClass.Text = MenuKit.LevelLine(d.Class, d.Level, d.Paragon);
            _mClass.AddThemeColorOverride("font_color", MenuKit.ClassColor(d.Class).Lerp(MenuKit.Ash, 0.4f));
            _mPlace.Text = place;
            _mCause.Text = cause ?? "無人知曉";
            _mTier.Text = MenuKit.TierName(d.Tier);
            _mTime.Text = MenuKit.PlayTime(d.PlayTime);
            _mDeeds.Text = $"敵人 {d.Kills:N0}　·　菁英 {d.Elites:N0}　·　首領 {d.Bosses:N0}";
            _mDate.Text = DateTime.Now.ToString("yyyy 年 M 月 d 日");
        }

        bool inTown = level != null && level.IsTown;
        _hope.Visible = !_hardcore;
        _respawn.Visible = !_hardcore;
        _town.Visible = !_hardcore && !inTown;
        _toTitle.Visible = _hardcore;
    }

    private void SetButtonsEnabled(bool on)
    {
        foreach (var b in new[] { _respawn, _town, _toTitle })
        {
            if (b == null) continue;
            b.Disabled = !on;
            b.FocusMode = on ? FocusModeEnum.All : FocusModeEnum.None;
        }
    }

    private void EnableButtons()
    {
        if (!_open) return;
        SetButtonsEnabled(true);
        MenuKit.Quiet();
        var first = _hardcore ? _toTitle : _respawn;
        if (first != null && first.IsVisibleInTree()) first.GrabFocus();
    }

    // ───────────────────────── actions ─────────────────────────

    private bool Leave(string what)
    {
        if (_done || !_open) return false;
        var g = Game.I;
        if (g == null) return false;
        _done = true;
        ClearFatal();
        Log.Info("ui", "death screen: " + what);
        g.Ui?.Close(Id);
        return true;
    }

    private void DoRespawn()
    {
        if (_hardcore || !Leave("respawn at the checkpoint")) return;
        Game.I.Respawn();
    }

    private void DoTown()
    {
        if (_hardcore || !Leave("return to town")) return;
        var g = Game.I;
        g.Respawn();                 // back on their feet first, so nobody arrives in town as a corpse
        g.EnterArea(ZoneDb.Town);
    }

    private void DoTitle()
    {
        if (!Leave("return to the title screen")) return;
        Game.I.ToMenu();
    }
}
