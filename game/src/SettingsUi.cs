using Godot;
using System;
using System.Collections.Generic;
using System.IO;

namespace AshenAbyss;

/// <summary>
/// The settings sheet itself, shared by the in-game panel (<see cref="SettingsUi"/>) and the title screen.
/// Every change is written into <c>Game.I.Cfg</c>, applied through <c>Game.I.ApplySettings()</c> and saved at once;
/// sliders preview live and commit a moment after the last movement so dragging does not thrash the window.
/// </summary>
public sealed class SettingsView
{
    public readonly PanelContainer Root;
    private readonly Action _onBack, _onApplied;
    private Settings _fallback;
    private readonly Godot.Timer _debounce;
    private bool _pending, _refreshing, _audioWarned;
    private string _pendingWhat = "";

    private readonly MenuSeg _tabs;
    private readonly List<Control> _pages = new();
    private MenuSeg _display, _vsync, _quality, _scaleMode, _dmgNum, _shake, _subs, _loot, _textScale;
    private HSlider _scale, _bright;
    private Label _brightGlyph;
    private Label _scaleValue;
    private readonly HSlider[] _vol = new HSlider[5];
    private readonly Label[] _volValue = new Label[5];
    private static readonly string[] VolNames = { "主音量", "音樂", "音效", "語音", "環境音" };
    private static readonly string[] VolHints =
    {
        "所有聲音的總音量", "各地區的配樂與戰鬥樂段", "技能、打擊與介面的聲響", "對白與旁白", "風聲、火聲與遠處的動靜",
    };
    // Design sizes (px at 1080p). MenuKit / MenuSeg do not scale by themselves, so every size handed to them goes
    // through UiTheme.Fs here; lengths that must grow with the text go through UiTheme.Px / UiTheme.MinSize.
    private const int FsTitle = 44, FsTab = 24, FsLabel = 24, FsHint = 20, FsOption = 22, FsValue = 22, FsGroup = 26, FsKey = 20, FsKeyText = 22, FsButton = 24;
    private const float LabelW = 440f;

    /// <summary>The control that should take keyboard focus when the sheet opens.</summary>
    public Control DefaultFocus => _tabs?.Current;

    private Settings Cfg
    {
        get
        {
            var g = Game.I;
            if (g != null && g.Cfg != null) return g.Cfg;
            return _fallback ??= Settings.Load();
        }
    }

    public SettingsView(Action onBack, Action onApplied = null)
    {
        _onBack = onBack;
        _onApplied = onApplied;

        Root = MenuKit.Frame(true);
        Root.Name = "SettingsSheet";
        // a sheet that fits 1920x1080 at every text scale: the pages inside scroll when their rows outgrow it
        FitSheet();
        Root.MouseFilter = Control.MouseFilterEnum.Stop;

        var col = MenuKit.VBox(12);
        Root.AddChild(MenuKit.Pad(col, 34, 24, 34, 24));

        var head = MenuKit.HBox(16);
        head.AddChild(MenuKit.Text("設定", UiTheme.Fs(FsTitle), MenuKit.Gold, MenuKit.TitleFont));
        var note = MenuKit.Text("變更會立即套用，並自動儲存。", UiTheme.Fs(FsHint), MenuKit.Faint, MenuKit.UiFont);
        note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        note.HorizontalAlignment = HorizontalAlignment.Right;
        note.VerticalAlignment = VerticalAlignment.Bottom;
        head.AddChild(note);
        col.AddChild(head);

        _tabs = new MenuSeg(new[] { "顯示", "音量", "遊戲", "操作說明" }, ShowTab, UiTheme.Fs(FsTab), UiTheme.Px(168f));
        col.AddChild(_tabs.Root);
        col.AddChild(MenuKit.Rule(new Color(MenuKit.Line, 0.9f)));

        _pages.Add(Page(BuildDisplay()));
        _pages.Add(Page(BuildAudio()));
        _pages.Add(Page(BuildGame()));
        _pages.Add(Page(BuildControls()));
        foreach (var p in _pages) col.AddChild(p);

        col.AddChild(MenuKit.Rule(new Color(MenuKit.Line, 0.9f)));
        var foot = MenuKit.HBox(16);
        foot.AddChild(MenuKit.Btn("返回", () => { Flush(); _onBack?.Invoke(); }, MenuTone.Primary, UiTheme.Fs(FsButton), UiTheme.Px(190f)));
        var gap = MenuKit.Spacer();
        gap.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foot.AddChild(gap);
        foot.AddChild(MenuKit.Btn("恢復預設值", ResetDefaults, MenuTone.Normal, UiTheme.Fs(FsOption), UiTheme.Px(180f)));
        col.AddChild(foot);

        _debounce = new Godot.Timer { Name = "Debounce", OneShot = true, WaitTime = 0.35 };
        _debounce.Timeout += Flush;
        Root.AddChild(_debounce);

        ShowTab(0);
        Refresh();
    }

    /// <summary>
    /// The sheet is 900 design px tall at 標準 and grows a little with the text (1010 at most, which a 1080-line screen
    /// still holds), so the display page fits without scrolling at every text size. Called again when the size changes.
    /// </summary>
    public void FitSheet()
    {
        Root.CustomMinimumSize = new Vector2(1320f, MathF.Min(1010f, 900f + (UiTheme.TextScale - 1f) * 400f));
    }

    // ───────────────────────── pages ─────────────────────────

    private static ScrollContainer Page(Control content)
    {
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto, FollowFocus = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 560),
        };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        // the scroller only stretches a child that asks for it: without this the page keeps its minimum width and
        // every wrapping text in it (the key list, the folder paths) collapses to one character per line
        var pad = MenuKit.Pad(content, 6, 8, 18, 8);
        pad.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(pad);
        return scroll;
    }

    private void ShowTab(int i)
    {
        i = Math.Clamp(i, 0, _pages.Count - 1);
        for (int j = 0; j < _pages.Count; j++) _pages[j].Visible = j == i;
        _tabs.Select(i);
    }

    /// <summary>The calibration glyph of the 亮度 row follows the slider the way unlit ground follows the exposure.</summary>
    private void SyncBright(float b)
    {
        if (_brightGlyph == null) return;
        float g = Mathf.Clamp(0.045f * Mathf.Pow(Mathf.Clamp(b, 0.8f, 1.3f), 1.4f), 0f, 0.2f);
        _brightGlyph.AddThemeColorOverride("font_color", new Color(g, g, g));
    }

    private static HBoxContainer Row(string label, string hint, Control control)
    {
        var row = MenuKit.HBox(20);
        UiTheme.MinSize(row, 0f, 66f);
        var left = MenuKit.VBox(0);
        left.CustomMinimumSize = new Vector2(LabelW, 0);
        left.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        left.AddChild(MenuKit.Text(label, UiTheme.Fs(FsLabel), MenuKit.Ivory, MenuKit.TitleFont));
        if (!string.IsNullOrEmpty(hint)) left.AddChild(MenuKit.Wrap(hint, UiTheme.Fs(FsHint), MenuKit.Faint, LabelW, MenuKit.UiFont));
        row.AddChild(left);
        control.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(control);
        return row;
    }

    private static string[] OnOff => new[] { "開啟", "關閉" };

    private Control BuildDisplay()
    {
        var col = MenuKit.VBox(6);
        _display = new MenuSeg(new[] { "全螢幕", "視窗" }, i => { Cfg.Fullscreen = i == 0; Commit("fullscreen=" + Cfg.Fullscreen); }, UiTheme.Fs(FsOption), UiTheme.Px(150f));
        col.AddChild(Row("顯示模式", "全螢幕，或可自由縮放的視窗", _display.Root));

        // 介面文字大小: applies at once — UiTheme rewrites the shared theme and raises TextScaleChanged, on which the
        // panel manager (every panel, open or not), the HUD and the title screen rescale what they built — and is saved.
        _textScale = new MenuSeg(new[] { "標準", "大", "特大" }, i =>
        {
            Cfg.UiTextScale = Math.Clamp(i, 0, 2);
            UiTheme.ApplyTextScale(Cfg.UiTextScale);
            Commit("uiTextScale=" + Cfg.UiTextScale);
        }, UiTheme.Fs(FsOption), UiTheme.Px(150f));
        col.AddChild(Row("介面文字大小", "選單、提示與戰鬥畫面上所有文字的大小；變更後立即生效", _textScale.Root));

        _vsync = new MenuSeg(OnOff, i => { Cfg.VSync = i == 0; Commit("vsync=" + Cfg.VSync); }, UiTheme.Fs(FsOption), UiTheme.Px(150f));
        col.AddChild(Row("垂直同步", "消除畫面撕裂；關閉可降低操作延遲", _vsync.Root));

        _quality = new MenuSeg(new[] { "低", "中", "高", "極致" }, i => { Cfg.Quality = i; Commit("quality=" + i); }, UiTheme.Fs(FsOption), UiTheme.Px(122f));
        col.AddChild(Row("畫質", "陰影、霧氣、環境光遮蔽與光暈的精細程度", _quality.Root));

        // 亮度 (graphics pass): exposure of the 3D picture. The glyph on the left is drawn at the level of unlit ground.
        var brightRow = MenuKit.HBox(14);
        // a black floor in a recessed rim (the well moulding): the glyph must stand on true black to calibrate against
        var brightSwatch = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        brightSwatch.AddThemeStyleboxOverride("panel", UiTheme.Art && UiArt.Has("well")
            ? UiTheme.ArtBox("menu_swatch", () => new UiNine("well") { Back = Colors.Black, BackInset = 2f })
            : MenuKit.FlatBox(Colors.Black, Colors.Black, 0, 0));
        UiTheme.MinSize(brightSwatch, 64f, 44f);
        _brightGlyph = MenuKit.Text("燼", UiTheme.Fs(FsValue), new Color(0.045f, 0.045f, 0.045f), MenuKit.UiFont);
        _brightGlyph.HorizontalAlignment = HorizontalAlignment.Center;
        _brightGlyph.VerticalAlignment = VerticalAlignment.Center;
        brightSwatch.AddChild(_brightGlyph);
        brightRow.AddChild(brightSwatch);
        _bright = MenuKit.Slider(80, 130, 5, 380f);
        _bright.ValueChanged += v =>
        {
            SyncBright((float)(v / 100.0));
            if (_refreshing) return;
            Cfg.Brightness = (float)(v / 100.0);
            Later("brightness=" + Cfg.Brightness.ToString("0.00"));
        };
        _bright.DragEnded += changed => { if (changed) Flush(); };
        brightRow.AddChild(_bright);
        col.AddChild(Row("亮度", "調整到幾乎看不見左邊的符號；預設值即為本作設計的幽暗氛圍", brightRow));

        var scaleBox = MenuKit.VBox(8);
        _scaleMode = new MenuSeg(new[] { "自動", "自訂" }, i =>
        {
            Cfg.RenderScale = i == 0 ? 0f : (float)(_scale.Value / 100.0);
            SyncScale();
            Commit("renderScale=" + Cfg.RenderScale.ToString("0.00"));
        }, UiTheme.Fs(FsOption), UiTheme.Px(150f));
        scaleBox.AddChild(_scaleMode.Root);
        var scaleRow = MenuKit.HBox(14);
        _scale = MenuKit.Slider(50, 100, 5, 380f);
        _scale.ValueChanged += v =>
        {
            if (_refreshing || _scaleMode.Index != 1) return;
            Cfg.RenderScale = (float)(v / 100.0);
            SyncScale();
            Later("renderScale=" + Cfg.RenderScale.ToString("0.00"));
        };
        _scale.DragEnded += changed => { if (changed) Flush(); };
        scaleRow.AddChild(_scale);
        _scaleValue = MenuKit.Text("", UiTheme.Fs(FsValue), MenuKit.Ivory, MenuKit.UiFont);
        UiTheme.MinSize(_scaleValue, 200f, 0f);
        _scaleValue.VerticalAlignment = VerticalAlignment.Center;
        scaleRow.AddChild(_scaleValue);
        scaleBox.AddChild(scaleRow);
        col.AddChild(Row("渲染比例", "以較低的解析度繪製 3D 畫面再放大，可大幅提高流暢度。自動模式會依螢幕解析度決定", scaleBox));
        return col;
    }

    private Control BuildAudio()
    {
        var col = MenuKit.VBox(6);
        for (int i = 0; i < 5; i++)
        {
            int idx = i;
            var row = MenuKit.HBox(14);
            var s = MenuKit.Slider(0, 100, 1, 460f);
            var value = MenuKit.Text("", UiTheme.Fs(FsValue), MenuKit.Ivory, MenuKit.UiFont);
            UiTheme.MinSize(value, 76f, 0f);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.VerticalAlignment = VerticalAlignment.Center;
            s.ValueChanged += v =>
            {
                if (_refreshing) return;
                SetVolume(idx, (float)(v / 100.0));
                _volValue[idx].Text = $"{(int)Math.Round(v)}%";
                PreviewVolumes();
                Later($"volume {idx}={(int)Math.Round(v)}");
            };
            s.DragEnded += changed =>
            {
                if (!changed) return;
                Flush();
                if (idx == 0 || idx == 2) MenuKit.Click();      // lets the player hear the new level
            };
            row.AddChild(s);
            row.AddChild(value);
            _vol[i] = s;
            _volValue[i] = value;
            col.AddChild(Row(VolNames[i], VolHints[i], row));
        }
        return col;
    }

    private Control BuildGame()
    {
        var col = MenuKit.VBox(6);
        _dmgNum = new MenuSeg(OnOff, i => { Cfg.DamageNumbers = i == 0; Commit("damageNumbers=" + Cfg.DamageNumbers); }, UiTheme.Fs(FsOption), UiTheme.Px(150f));
        col.AddChild(Row("傷害數字", "在敵人身上顯示每一擊的傷害數值", _dmgNum.Root));
        _shake = new MenuSeg(OnOff, i => { Cfg.ScreenShake = i == 0; Commit("screenShake=" + Cfg.ScreenShake); }, UiTheme.Fs(FsOption), UiTheme.Px(150f));
        col.AddChild(Row("畫面震動", "重擊、爆炸與首領招式晃動鏡頭", _shake.Root));
        _subs = new MenuSeg(OnOff, i => { Cfg.Subtitles = i == 0; Commit("subtitles=" + Cfg.Subtitles); }, UiTheme.Fs(FsOption), UiTheme.Px(150f));
        col.AddChild(Row("字幕", "顯示對白與旁白的文字", _subs.Root));
        _loot = new MenuSeg(OnOff, i => { Cfg.ShowAllLoot = i == 0; Commit("showAllLoot=" + Cfg.ShowAllLoot); }, UiTheme.Fs(FsOption), UiTheme.Px(150f));
        col.AddChild(Row("總是顯示掉落物標籤", "關閉時，按住 Alt 才會顯示地上物品的名稱", _loot.Root));
        return col;
    }

    // ───────────────────────── controls reference ─────────────────────────

    private static Control KeyCap(string text) => MenuKit.KeyCap(text, UiTheme.Fs(FsKey));

    private static Control KeyRow(string[] caps, string what)
    {
        var row = MenuKit.HBox(12);
        UiTheme.MinSize(row, 0f, 42f);
        var keys = MenuKit.HBox(5);
        UiTheme.MinSize(keys, 204f, 0f);
        foreach (var c in caps) keys.AddChild(KeyCap(c));
        row.AddChild(keys);
        var l = MenuKit.Wrap(what, UiTheme.Fs(FsKeyText), MenuKit.Ash, 0f, MenuKit.UiFont);     // wraps instead of being cut off
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(l);
        return row;
    }

    private static Control KeyGroup(string title, (string[] caps, string what)[] rows)
    {
        var col = MenuKit.VBox(3);
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        col.AddChild(MenuKit.Text(title, UiTheme.Fs(FsGroup), MenuKit.Gold, MenuKit.TitleFont));
        col.AddChild(MenuKit.Rule(new Color(MenuKit.Line, 0.7f)));
        col.AddChild(MenuKit.Spacer(0, 2));
        foreach (var (caps, what) in rows) col.AddChild(KeyRow(caps, what));
        return col;
    }

    private static string[] K(params string[] caps) => caps;

    private Control BuildControls()
    {
        var col = MenuKit.VBox(12);
        var groups = MenuKit.HBox(36);
        groups.AddChild(KeyGroup("戰鬥與移動", new[]
        {
            (K("滑鼠左鍵"), "移動、攻擊、互動（技能欄 1）"),
            (K("滑鼠右鍵"), "技能欄 2"),
            (K("1", "2", "3", "4"), "技能欄 3 至 6"),
            (K("W", "A", "S", "D"), "直接移動"),
            (K("Shift"), "按住：原地出手，不追向目標"),
            (K("空白鍵"), "閃避"),
            (K("Q"), "飲用藥水"),
            (K("T"), "開啟回城傳送（非戰鬥中）"),
            (K("滑鼠滾輪"), "拉近或拉遠視角"),
        }));
        groups.AddChild(KeyGroup("介面", new[]
        {
            (K("I", "C"), "物品欄與角色"),
            (K("K"), "技能"),
            (K("P"), "燼紋盤"),
            (K("M", "Tab"), "地圖"),
            (K("J"), "任務日誌"),
            (K("Alt"), "按住：顯示所有掉落物標籤"),
            (K("Esc"), "關閉視窗；無視窗時開啟暫停選單"),
            (K("PrtSc", "F12"), "擷取畫面"),
        }));
        col.AddChild(groups);

        col.AddChild(MenuKit.Spacer(0, 2));
        col.AddChild(MenuKit.Text("檔案位置", UiTheme.Fs(FsGroup), MenuKit.Gold, MenuKit.TitleFont));
        col.AddChild(MenuKit.Rule(new Color(MenuKit.Line, 0.7f)));
        col.AddChild(PathRow("擷取畫面", "PNG 圖檔", ScreenshotDir()));
        col.AddChild(PathRow("存檔", "每個欄位一個檔案，並保留上一份備份", SaveDir()));
        col.AddChild(PathRow("記錄檔", "遇到問題時，這裡的檔案能幫忙找出原因", LogDir()));
        return col;
    }

    private static string Native(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        return OS.GetName() == "Windows" ? path.Replace('/', '\\') : path;
    }

    public static string ScreenshotDir()
    {
        try { return Native(Path.Combine(OS.GetSystemDir(OS.SystemDir.Pictures), "AshenAbyss")); }
        catch (Exception) { return ""; }
    }

    public static string SaveDir()
    {
        try { return Native(ProjectSettings.GlobalizePath("user://saves")); }
        catch (Exception) { return ""; }
    }

    public static string LogDir()
    {
        try { return Native(string.IsNullOrEmpty(Log.Dir) ? ProjectSettings.GlobalizePath("user://logs") : Log.Dir); }
        catch (Exception) { return ""; }
    }

    private static Control PathRow(string label, string hint, string path)
    {
        var row = MenuKit.HBox(14);
        UiTheme.MinSize(row, 0f, 60f);
        var left = MenuKit.VBox(0);
        UiTheme.MinSize(left, 280f, 0f);
        left.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        left.AddChild(MenuKit.Text(label, UiTheme.Fs(FsLabel), MenuKit.Ivory, MenuKit.TitleFont));
        left.AddChild(MenuKit.Wrap(hint, UiTheme.Fs(FsHint), MenuKit.Faint, 0f, MenuKit.UiFont));
        row.AddChild(left);

        var where = MenuKit.Wrap(string.IsNullOrEmpty(path) ? "（無法取得位置）" : path, UiTheme.Fs(FsHint), MenuKit.Ash, 0f, MenuKit.UiFont);
        where.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        where.VerticalAlignment = VerticalAlignment.Center;
        where.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(where);

        var open = MenuKit.Btn("開啟資料夾", () => OpenFolder(label, path), MenuTone.Normal, UiTheme.Fs(FsOption), UiTheme.Px(150f));
        open.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        open.Disabled = string.IsNullOrEmpty(path);
        row.AddChild(open);
        return row;
    }

    private static void OpenFolder(string label, string path)
    {
        try
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            var err = OS.ShellOpen(path);
            if (err != Error.Ok) { Log.Warn("menu", $"could not open the {label} folder: {err}"); MenuKit.ErrorSound(); }
            else Log.Info("menu", $"opened the {label} folder");
        }
        catch (Exception e)
        {
            Log.Error("menu", $"opening the {label} folder failed", e);
            MenuKit.ErrorSound();
        }
    }

    // ───────────────────────── values ─────────────────────────

    private float GetVolume(int i)
    {
        var c = Cfg;
        return i switch { 0 => c.Master, 1 => c.Music, 2 => c.Sfx, 3 => c.Voice, _ => c.Ambience };
    }

    private void SetVolume(int i, float v)
    {
        var c = Cfg;
        v = Mathf.Clamp(v, 0f, 1f);
        switch (i)
        {
            case 0: c.Master = v; break;
            case 1: c.Music = v; break;
            case 2: c.Sfx = v; break;
            case 3: c.Voice = v; break;
            default: c.Ambience = v; break;
        }
    }

    private static float AutoScale()
    {
        var win = DisplayServer.WindowGetSize();
        try
        {
            float s = Gfx.AutoScale(win);
            if (s > 0.2f && s <= 1.001f) return s;
        }
        catch (Exception) { }
        return Mathf.Clamp(1440f / Math.Max(win.Y, 1), 0.5f, 1f);
    }

    private void SyncScale()
    {
        bool custom = Cfg.RenderScale > 0.01f;
        _scale.Editable = custom;
        _scale.FocusMode = custom ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
        _scale.Modulate = custom ? Colors.White : new Color(1, 1, 1, 0.45f);
        _scaleValue.Text = custom
            ? $"{(int)Math.Round(Cfg.RenderScale * 100f)}%"
            : $"自動 · 約 {(int)Math.Round(AutoScale() * 100f)}%";
    }

    /// <summary>Loads the current settings into every control (no change is raised).</summary>
    public void Refresh()
    {
        var c = Cfg;
        _refreshing = true;
        try
        {
            _display.Select(c.Fullscreen ? 0 : 1);
            _textScale.Select(Math.Clamp(c.UiTextScale, 0, 2));
            _vsync.Select(c.VSync ? 0 : 1);
            _quality.Select(Math.Clamp(c.Quality, 0, 3));
            _bright.SetValueNoSignal(Math.Clamp(Math.Round(c.Brightness * 20f) * 5.0, 80.0, 130.0));
            SyncBright(c.Brightness);
            bool custom = c.RenderScale > 0.01f;
            _scaleMode.Select(custom ? 1 : 0);
            float shown = custom ? c.RenderScale : AutoScale();
            _scale.SetValueNoSignal(Math.Clamp(Math.Round(shown * 20f) * 5.0, 50.0, 100.0));
            SyncScale();
            for (int i = 0; i < 5; i++)
            {
                double pct = Math.Round(Mathf.Clamp(GetVolume(i), 0f, 1f) * 100f);
                _vol[i].SetValueNoSignal(pct);
                _volValue[i].Text = $"{(int)pct}%";
            }
            _dmgNum.Select(c.DamageNumbers ? 0 : 1);
            _shake.Select(c.ScreenShake ? 0 : 1);
            _subs.Select(c.Subtitles ? 0 : 1);
            _loot.Select(c.ShowAllLoot ? 0 : 1);
        }
        finally { _refreshing = false; }
    }

    // ───────────────────────── applying ─────────────────────────

    /// <summary>A discrete choice: apply and save right away.</summary>
    private void Commit(string what)
    {
        if (_refreshing) return;
        Log.Info("cfg", "setting changed: " + what);
        Apply();
    }

    /// <summary>A slider moved: apply shortly after the last movement.</summary>
    private void Later(string what)
    {
        _pending = true;
        _pendingWhat = what;
        if (_debounce.IsInsideTree()) _debounce.Start(0.35);
        else Flush();
    }

    /// <summary>Applies and saves anything a slider left pending (called on drag end, on leaving and by the timer).</summary>
    public void Flush()
    {
        if (!_pending) return;
        Log.Info("cfg", "setting changed: " + _pendingWhat);
        Apply();
    }

    private void Apply()
    {
        _pending = false;
        if (_debounce.IsInsideTree()) _debounce.Stop();
        try { Game.I?.ApplySettings(); }
        catch (Exception e) { Log.Error("cfg", "applying the settings failed", e); }
        try { Cfg.Save(); }
        catch (Exception e) { Log.Error("cfg", "saving the settings failed", e); }
        if (_onApplied != null) Log.Guard("cfg", _onApplied);
    }

    private void PreviewVolumes()
    {
        try { Game.I?.Audio?.ApplyVolumes(Cfg); }
        catch (Exception e)
        {
            if (!_audioWarned) { _audioWarned = true; Log.Warn("cfg", "live volume preview unavailable: " + e.Message); }
        }
    }

    private void ResetDefaults()
    {
        var c = Cfg;
        var d = new Settings();
        c.Master = d.Master; c.Music = d.Music; c.Sfx = d.Sfx; c.Voice = d.Voice; c.Ambience = d.Ambience;
        c.Fullscreen = d.Fullscreen; c.VSync = d.VSync; c.DamageNumbers = d.DamageNumbers; c.ScreenShake = d.ScreenShake;
        c.Subtitles = d.Subtitles; c.ShowAllLoot = d.ShowAllLoot; c.Quality = d.Quality; c.RenderScale = d.RenderScale;
        c.UiTextScale = d.UiTextScale; c.Brightness = d.Brightness;
        // LastSlot is not a preference and stays as it is
        try { UiTheme.ApplyTextScale(c.UiTextScale); }
        catch (Exception e) { Log.Error("cfg", "resetting the text size failed", e); }
        Refresh();
        Log.Info("cfg", "settings reset to defaults");
        Apply();
    }
}

/// <summary>Panel "settings": the settings sheet over the paused game. Opened from the pause menu with arg "pause".</summary>
public partial class SettingsUi : UiPanel
{
    public override string Id => "settings";
    public override bool Pauses => true;
    public override bool Fullscreen => true;

    private SettingsView _view;
    private ColorRect _dim;
    private string _returnTo;
    private bool _built;

    public override void _Ready()
    {
        base._Ready();
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        try
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;
            _dim = new ColorRect { Color = new Color(0.01f, 0.008f, 0.008f, 0.72f), MouseFilter = MouseFilterEnum.Ignore };
            _dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(_dim);
            var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(center);
            _view = new SettingsView(CloseSelf);
            center.AddChild(_view.Root);
        }
        catch (Exception e) { Log.Error("ui", "building the settings panel failed", e); }
    }

    private void CloseSelf()
    {
        MenuKit.CloseSound();
        var ui = Game.I?.Ui;
        if (ui != null) ui.Close(Id);
        else Visible = false;
    }

    public override void OnOpen(object arg)
    {
        EnsureBuilt();
        _returnTo = arg as string;
        if (_view == null) return;
        _view.Refresh();
        MenuKit.Quiet();
        var focus = _view.DefaultFocus;
        if (focus != null)
            Callable.From(() => { if (IsInstanceValid(focus) && focus.IsVisibleInTree()) focus.GrabFocus(); }).CallDeferred();
        Log.Debug("ui", "settings opened" + (_returnTo != null ? " from " + _returnTo : ""));
    }

    public override void OnClose()
    {
        _view?.Flush();
        string back = _returnTo;
        _returnTo = null;
        if (back != "pause") return;
        // opened from the pause menu: if that menu is no longer up (the panel manager shows one panel at a time), bring it back
        Callable.From(() =>
        {
            var g = Game.I;
            if (g == null || g.Ui == null || g.State != Game.GameState.Play) return;
            if (!g.Ui.IsOpen("pause") && !g.Ui.IsOpen("settings")) g.Ui.Open("pause");
        }).CallDeferred();
    }

    public override void Refresh() => _view?.Refresh();

    /// <summary>The text size changed: the sheet takes its new height.</summary>
    public override void OnTextScale() => _view?.FitSheet();

    public override void _ExitTree()
    {
        _view?.Flush();
        base._ExitTree();
    }
}
