using Godot;
using System;

namespace AshenAbyss;

/// <summary>
/// Panel "pause": the game stands still. Shows who and where the hero is and how long the journey has lasted, and
/// offers to resume, open the settings, return to the title screen or quit to the desktop (both of which save).
/// </summary>
public partial class PauseMenu : UiPanel
{
    public override string Id => "pause";
    public override bool Pauses => true;
    public override bool Fullscreen => true;

    private bool _built, _open, _waitingSettings, _faulted;
    private Control _content;
    private Label _hero, _area, _tier, _time, _note;
    private Control _hardcoreBadge;
    private Button _resume;
    private double _tick, _settingsGrace;

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
        catch (Exception e) { Log.Error("ui", "building the pause menu failed", e); }
    }

    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0.010f, 0.008f, 0.008f, 0.66f), MouseFilter = MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);
        AddChild(MenuKit.Stretched(MenuKit.Fade(true,
            (0f, new Color(0, 0, 0, 0.55f)), (0.25f, new Color(0, 0, 0, 0f)), (0.75f, new Color(0, 0, 0, 0f)), (1f, new Color(0, 0, 0, 0.65f)))));

        var center = new CenterContainer { Name = "Content", MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        _content = center;

        var frame = MenuKit.Frame(true);
        frame.CustomMinimumSize = new Vector2(660, 0);
        center.AddChild(frame);
        var col = MenuKit.VBox(8);
        frame.AddChild(MenuKit.Pad(col, 44, 30, 44, 30));

        var title = MenuKit.Text("暫停", UiTheme.Fs(60), MenuKit.Ivory, MenuKit.Spaced(MenuKit.BlackFont, 14, 12, 10));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        MenuKit.Shadow(title, new Color(0, 0, 0, 0.7f), 8, 3);
        col.AddChild(title);
        var sub = MenuKit.Text("燼火未熄，旅途暫歇。", UiTheme.Fs(UiTheme.FsBody), MenuKit.Ash, MenuKit.BodyFont);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(sub);
        // the tapering gold rule with its ember stone; a plain ember line without the art
        var gemRule = MenuKit.Ornament("title_rule", 26f, null, 0.86f);
        if (gemRule != null) col.AddChild(gemRule);
        else
        {
            col.AddChild(MenuKit.Spacer(0, 4));
            var rule = MenuKit.Stretched(MenuKit.Fade(false,
                (0f, new Color(1f, 0.5f, 0.15f, 0f)), (0.5f, new Color(1f, 0.5f, 0.15f, 0.85f)), (1f, new Color(1f, 0.5f, 0.15f, 0f))));
            var ruleBox = new Control { CustomMinimumSize = new Vector2(0, 2), MouseFilter = MouseFilterEnum.Ignore };
            ruleBox.AddChild(rule);
            col.AddChild(ruleBox);
            col.AddChild(MenuKit.Spacer(0, 4));
        }

        var heroRow = MenuKit.HBox(12);
        heroRow.Alignment = BoxContainer.AlignmentMode.Center;
        _hero = MenuKit.Text("", UiTheme.Fs(UiTheme.FsName), MenuKit.Ivory, MenuKit.TitleFont);
        heroRow.AddChild(_hero);
        var badge = MenuKit.Badge("專家", MenuKit.Blood, UiTheme.Fs(UiTheme.FsCaption), new Color(0.98f, 0.62f, 0.55f));
        badge.Visible = false;
        heroRow.AddChild(badge);
        _hardcoreBadge = badge;
        col.AddChild(heroRow);

        _area = InfoRow(col, "所在");
        _tier = InfoRow(col, "世界階級");
        _time = InfoRow(col, "遊玩時間");
        col.AddChild(MenuKit.Spacer(0, 10));

        _resume = MenuKit.Btn("繼續旅途", Resume, MenuTone.Primary, UiTheme.Fs(26), 0f);
        col.AddChild(_resume);
        col.AddChild(MenuKit.Btn("設定", OpenSettings, MenuTone.Normal, UiTheme.Fs(UiTheme.FsBody), 0f));
        col.AddChild(MenuKit.Btn("返回標題畫面", ToTitle, MenuTone.Normal, UiTheme.Fs(UiTheme.FsBody), 0f));
        col.AddChild(MenuKit.Btn("離開遊戲", QuitGame, MenuTone.Normal, UiTheme.Fs(UiTheme.FsBody), 0f));
        col.AddChild(MenuKit.Spacer(0, 2));

        _note = MenuKit.Text("返回標題或離開遊戲時，進度會自動儲存。", UiTheme.Fs(UiTheme.FsCaption), MenuKit.Faint, MenuKit.UiFont);
        _note.HorizontalAlignment = HorizontalAlignment.Center;
        _note.AutowrapMode = TextServer.AutowrapMode.WordSmart;        // two lines rather than a wider frame at the largest text size
        col.AddChild(_note);
    }

    private static Label InfoRow(Container parent, string key)
    {
        var row = MenuKit.HBox(14);
        row.MouseFilter = MouseFilterEnum.Ignore;
        var k = MenuKit.Text(key, UiTheme.Fs(UiTheme.FsCaption), MenuKit.Faint, MenuKit.UiFont);
        UiTheme.MinSize(k, 140f, 0f);
        k.HorizontalAlignment = HorizontalAlignment.Right;
        k.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(k);
        var v = MenuKit.Text("", UiTheme.Fs(UiTheme.FsRow), MenuKit.Ivory, MenuKit.BodyFont);
        v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        v.ClipText = true;
        v.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(v);
        parent.AddChild(row);
        return v;
    }

    // ───────────────────────── panel ─────────────────────────

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        _open = true;
        _waitingSettings = false;
        _tick = 0;
        if (_content == null) return;
        _content.Visible = true;
        UpdateInfo();
        MenuKit.Quiet();
        _content.Modulate = new Color(1, 1, 1, 0);
        CreateTween().TweenProperty(_content, "modulate:a", 1f, 0.16f);
        var first = _resume;
        Callable.From(() => { if (_open && first != null && IsInstanceValid(first) && first.IsVisibleInTree()) first.GrabFocus(); }).CallDeferred();
        var d = Game.I?.Hero?.D;
        Log.Info("ui", $"paused: area={Game.I?.Level?.AreaId ?? d?.Area ?? "?"} playtime={(d?.PlayTime ?? 0):F0}s");
    }

    public override void OnClose()
    {
        _open = false;
        _waitingSettings = false;
        if (_content != null) _content.Visible = true;
    }

    public override void Refresh() => UpdateInfo();

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (!_built || !_open || _faulted || _content == null) return;
        try
        {
            if (_waitingSettings)
            {
                // the settings sheet lies over this menu; come back into view once it is gone
                _settingsGrace -= delta;
                var ui = Game.I?.Ui;
                if (_settingsGrace <= 0 && (ui == null || !ui.IsOpen("settings")))
                {
                    _waitingSettings = false;
                    _content.Visible = true;
                    MenuKit.Quiet();
                    if (_resume != null && _resume.IsVisibleInTree()) _resume.GrabFocus();
                }
            }
            _tick += delta;
            if (_tick >= 0.5) { _tick = 0; UpdateInfo(); }
        }
        catch (Exception e)
        {
            _faulted = true;
            Log.Error("ui", "pause menu update failed", e);
        }
    }

    private void UpdateInfo()
    {
        if (_hero == null) return;
        var g = Game.I;
        var d = g?.Hero?.D;
        if (d == null)
        {
            _hero.Text = "持燼者";
            _area.Text = _tier.Text = _time.Text = "—";
            _hardcoreBadge.Visible = false;
            return;
        }
        _hero.Text = $"{(string.IsNullOrEmpty(d.Name) ? "無名者" : d.Name)}　{MenuKit.LevelLine(d.Class, d.Level, d.Paragon)}";
        _hero.AddThemeColorOverride("font_color", MenuKit.ClassColor(d.Class).Lerp(Colors.White, 0.25f));
        _hardcoreBadge.Visible = d.Hardcore;

        var level = g.Level;
        string areaId = level?.AreaId ?? d.Area;
        string area = MenuKit.AreaName(areaId);
        if (level != null && level.IsRift) area = $"深淵裂隙 · 第 {Math.Max(level.RiftTier, 1)} 層";
        else if (!string.IsNullOrEmpty(areaId))
        {
            RegionDef region = null;
            try { region = ZoneDb.RegionOf(areaId); } catch (Exception) { }
            if (region != null && region.Name != area) area += " · " + region.Name;
        }
        _area.Text = area;
        _tier.Text = MenuKit.TierName(d.Tier);
        _time.Text = MenuKit.PlayTime(d.PlayTime);
        _note.Text = d.Hardcore ? "專家模式：進度會自動儲存，倒下則無可挽回。" : "返回標題或離開遊戲時，進度會自動儲存。";
    }

    // ───────────────────────── actions ─────────────────────────

    private void Resume()
    {
        MenuKit.CloseSound();
        var ui = Game.I?.Ui;
        if (ui != null) ui.Close(Id);
    }

    private void OpenSettings()
    {
        var ui = Game.I?.Ui;
        if (ui == null) return;
        _waitingSettings = true;
        _settingsGrace = 0.3;
        if (_content != null) _content.Visible = false;
        ui.Open("settings", "pause");
    }

    private void ToTitle()
    {
        var g = Game.I;
        if (g == null || g.Ui == null) return;
        g.Ui.Confirm("返回標題畫面？\n目前的進度會先行儲存。", () =>
        {
            Log.Info("ui", "pause menu: return to the title screen");
            g.Ui.Close(Id);
            g.ToMenu();
        }, "返回標題", "取消");
    }

    private void QuitGame()
    {
        var g = Game.I;
        if (g == null || g.Ui == null) return;
        g.Ui.Confirm("離開遊戲，回到桌面？\n目前的進度會先行儲存。", () =>
        {
            Log.Info("ui", "pause menu: quit to desktop");
            g.SaveNow("quit");
            g.Quit();
        }, "離開遊戲", "取消");
    }
}
