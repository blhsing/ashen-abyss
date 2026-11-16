using Godot;
using System;
using System.Collections.Generic;

namespace AshenAbyss;

/// <summary>Logs a repeating failure once every few seconds, so a fault inside a per-frame update cannot flood the log.</summary>
internal static class UiSafe
{
    private static readonly Dictionary<string, ulong> Last = new();

    public static void Report(string tag, Exception e)
    {
        ulong now = Time.GetTicksMsec();
        if (Last.TryGetValue(tag, out ulong t) && now - t < 5000UL) return;
        Last[tag] = now;
        Log.Error("ui", tag + " failed (repeats are muted for 5 s)", e);
    }
}

/// <summary>
/// The panel manager. Owns one instance of every panel, shows them stacked over a dimmed backdrop, holds the pause
/// lock while a pausing panel is open, plays the open/close sounds, routes Esc and the panel hotkeys, and provides
/// the shared tooltip layer (with an optional comparison card) and the Confirm / Prompt dialogs.
/// Layer order inside this control: panels, modal dialog, tooltips.
/// </summary>
public partial class Ui : Control
{
    /// <summary>Text column of a tooltip card in design pixels; follows the text scale (see UiTheme.TipWidth).</summary>
    private static float TipWidth => UiTheme.TipWidth;
    /// <summary>The everyday panels that replace one another instead of stacking.</summary>
    private static readonly string[] MainGroup = { "inventory", "skills", "paragon", "map", "quests" };

    private readonly Dictionary<string, UiPanel> _panels = new();
    private readonly List<UiPanel> _open = new();
    private readonly Dictionary<UiPanel, ColorRect> _dims = new();
    private readonly HashSet<UiPanel> _locks = new();
    private readonly Dictionary<UiPanel, Tween> _fades = new();
    private readonly HashSet<string> _warned = new();
    private readonly Control _panelLayer, _modalLayer, _tipLayer;

    // tooltips
    private PanelContainer _tip, _tip2;
    private RichTextLabel _tipText, _tip2Text;
    private string _tipBb, _tip2Bb;
    private Rect2 _tipAnchor;
    private bool _tipShown;
    private int _tipFresh;
    private int _tipWide, _tip2Wide;        // how often a card was re-wrapped wider because it was taller than the screen
    private Control _tipOwner;

    // modal dialog
    private Control _modal;
    private Action _modalYes;
    private Action<string> _modalText;
    private LineEdit _modalEdit;
    private bool _modalLocked;

    private bool _mouseOver;
    private UiScript _script;               // interface test driver (--uido / --uiclick / --uikeys / --hover); null in normal play
    private Hero _hero;
    private bool _dirty, _refreshing;
    private Player _blocked;

    public Ui()
    {
        Name = "Ui";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Theme;
        _panelLayer = Layer("Panels");
        _modalLayer = Layer("Modal");
        _tipLayer = Layer("Tooltips");
        BuildTooltips();
        RegisterPanels();
    }

    private Control Layer(string name)
    {
        var c = new Control { Name = name, MouseFilter = MouseFilterEnum.Ignore };
        c.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(c);
        return c;
    }

    public override void _Ready()
    {
        // everything outside this control (title screen, loading screen) inherits the same look
        try
        {
            var root = GetTree()?.Root;
            if (root != null && root.Theme == null) root.Theme = UiTheme.Theme;
        }
        catch (Exception e) { Log.Error("ui", "could not install the theme on the root window", e); }
        try { _script = UiScript.FromArgs(); } catch (Exception e) { Log.Error("uitest", "interface script setup failed", e); }
        // the raster art comes in two densities: follow the window across the 1080p line
        try
        {
            var win = GetTree()?.Root;
            if (win != null)
            {
                UiArt.FitWindow(win.Size);
                win.SizeChanged += () => { if (IsInstanceValid(win)) UiArt.FitWindow(win.Size); };
                UiArt.SetChanged += () => { if (IsInstanceValid(win)) UiTheme.RedrawAll(win); };
            }
        }
        catch (Exception e) { Log.Error("ui", "could not follow the window size for the interface art", e); }
    }

    public override void _EnterTree()
    {
        UiTheme.TextScaleChanged -= OnTextScale;
        UiTheme.TextScaleChanged += OnTextScale;
    }

    public override void _ExitTree()
    {
        UiTheme.TextScaleChanged -= OnTextScale;
        if (_hero != null) { _hero.Changed -= OnHeroChanged; _hero = null; }
    }

    /// <summary>
    /// The text-size setting changed: every panel (on screen or not), the dialog and the tooltip cards are brought
    /// to the new sizes, each panel may re-lay itself out (<see cref="UiPanel.OnTextScale"/>), and the panels that are
    /// open rebuild their dynamic content.
    /// </summary>
    private void OnTextScale(float oldScale)
    {
        HideTooltip();
        _tipBb = null; _tip2Bb = null;
        UiTheme.Rescale(this, oldScale);
        foreach (var p in _panels.Values)
        {
            if (!IsInstanceValid(p)) continue;
            if (!p.IsInsideTree() || !IsAncestorOf(p)) UiTheme.Rescale(p, oldScale);     // closed panels wait outside the tree
            try { p.OnTextScale(); } catch (Exception e) { Log.Error("ui", $"{p.Id}.OnTextScale failed", e); }
        }
        for (int i = 0; i < _open.Count; i++)
        {
            var p = _open[i];
            if (!IsInstanceValid(p)) continue;
            try { p.Refresh(); } catch (Exception e) { Log.Error("ui", $"refresh of {p.Id} after a text-scale change failed", e); }
        }
        Log.Info("ui", $"panels rescaled to text scale ×{UiTheme.TextScale:0.00} ({_panels.Count} panels, {_open.Count} open)");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMMouseExit) { _pointer = new Vector2(-9999f, -9999f); return; }     // the pointer left the window
        if (what != NotificationPredelete) return;
        // panels that are not on screen are outside the tree and would otherwise outlive the manager
        foreach (var p in _panels.Values)
            if (IsInstanceValid(p) && !p.IsInsideTree()) p.Free();
        _panels.Clear();
    }

    // ───────────────────────── panels ─────────────────────────

    private void RegisterPanels()
    {
        Make("inventory", () => new InventoryUi());
        Make("skills", () => new SkillTreeUi());
        Make("paragon", () => new ParagonUi());
        Make("map", () => new MapUi());
        Make("quests", () => new QuestLogUi());
        Make("vendor", () => new VendorUi());
        Make("dialogue", () => new DialogueUi());
        Make("pause", () => new PauseMenu());
        Make("settings", () => new SettingsUi());
        Make("death", () => new DeathUi());
        Make("tier", () => new WorldTierUi());
        Make("rift", () => new RiftUi());
        Log.Info("ui", $"panels registered: {string.Join(", ", _panels.Keys)}");
    }

    private void Make(string expectedId, Func<UiPanel> create)
    {
        try
        {
            var p = create();
            Register(p);
            if (p != null && p.Id != expectedId) Log.Warn("ui", $"panel {p.GetType().Name} reports id '{p.Id}', expected '{expectedId}'");
        }
        catch (Exception e) { Log.Error("ui", $"panel '{expectedId}' could not be created", e); }
    }

    public void Register(UiPanel p)
    {
        if (p == null) return;
        string id = p.Id;
        if (string.IsNullOrEmpty(id)) { Log.Warn("ui", $"panel {p.GetType().Name} has no id and was not registered"); return; }
        if (_panels.TryGetValue(id, out var old) && !ReferenceEquals(old, p))
        {
            if (_open.Contains(old)) Close(id);
            if (IsInstanceValid(old)) old.QueueFree();
            Log.Warn("ui", $"panel '{id}' registered twice; the newer instance replaces the older");
        }
        _panels[id] = p;
    }

    /// <summary>The registered panel with this id, or null.</summary>
    public UiPanel Find(string id) => id != null && _panels.TryGetValue(id, out var p) ? p : null;

    public bool IsOpen(string id) => id != null && _panels.TryGetValue(id, out var p) && _open.Contains(p);
    public bool AnyOpen => _open.Count > 0 || _modal != null;
    public bool MouseOverUi => _mouseOver;
    /// <summary>True while a panel that covers the whole screen is open (the HUD hides).</summary>
    public bool FullscreenOpen
    {
        get
        {
            for (int i = 0; i < _open.Count; i++) if (_open[i].Fullscreen) return true;
            return false;
        }
    }
    /// <summary>Id of the panel on top of the stack, or null.</summary>
    public string TopId => _open.Count > 0 ? _open[^1].Id : null;
    /// <summary>Alt is held (or the setting is on): every loot label on screen should be shown.</summary>
    public bool ShowAllLoot { get; private set; }

    public void Open(string id, object arg = null)
    {
        if (id == null || !_panels.TryGetValue(id, out var p) || !IsInstanceValid(p))
        {
            if (_warned.Add(id ?? "")) Log.Warn("ui", $"open: unknown panel '{id}'");
            return;
        }
        HideTooltip();
        // already showing: close it quietly and open it afresh on top with the new argument
        if (_open.Contains(p)) CloseOne(p, id, true);
        if (Array.IndexOf(MainGroup, id) >= 0)
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                if (i >= _open.Count) continue;
                var other = _open[i];
                if (Array.IndexOf(MainGroup, other.Id) >= 0) CloseOne(other, other.Id, true);
            }

        bool full = false;
        try { full = p.Fullscreen; } catch { }
        var dim = new ColorRect { Name = "Dim_" + id, MouseFilter = MouseFilterEnum.Stop, Color = new Color(0.008f, 0.008f, 0.012f, 0f) };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panelLayer.AddChild(dim);
        var parent = p.GetParent();
        if (parent != null && parent != _panelLayer) parent.RemoveChild(p);
        if (p.GetParent() == null) _panelLayer.AddChild(p);
        _panelLayer.MoveChild(p, -1);
        Place(p, full);
        p.Visible = true;
        // a short fade so a window settles onto the screen instead of snapping on
        try
        {
            if (_fades.Remove(p, out var oldFade) && oldFade != null && oldFade.IsValid()) oldFade.Kill();
            p.Modulate = new Color(1f, 1f, 1f, 0f);
            var fade = p.CreateTween();
            fade.TweenProperty(p, "modulate:a", 1f, 0.13).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            _fades[p] = fade;
        }
        catch (Exception) { p.Modulate = Colors.White; }
        _dims[p] = dim;
        _open.Add(p);
        Snd.Ui("ui_open");
        Log.Info("ui", $"open {id}" + (arg != null ? $" arg={arg}" : ""));
        bool failed = false;
        try { p.OnOpen(arg); }
        catch (Exception e) { failed = true; Log.Error("ui", $"{id}.OnOpen failed", e); }
        if (!_open.Contains(p) || !ReferenceEquals(_dims.GetValueOrDefault(p), dim))
        {
            Log.Info("ui", $"{id} closed or reopened itself while opening");
            return;
        }
        if (failed)
        {
            // a panel that could not set itself up must not stay on screen holding the game
            CloseOne(p, id, true);
            return;
        }

        // asked after OnOpen: a panel may decide from its argument whether it pauses or covers the screen
        bool pauses = true;
        try { pauses = p.Pauses; full = p.Fullscreen; } catch { }
        if (pauses && Game.I != null && _locks.Add(p)) Game.I.PauseLocks++;
        float target = full ? 0.93f : pauses ? 0.62f : 0.22f;
        try
        {
            var tw = dim.CreateTween();
            tw.TweenProperty(dim, "color:a", target, 0.14);
        }
        catch { dim.Color = new Color(dim.Color, target); }
        SyncPlayerInput(false);
    }

    /// <summary>
    /// Gives a panel its place on screen unless it already arranged itself: an untouched panel fills the screen
    /// (its children centre whatever window they draw); a panel with only a size is centred.
    /// </summary>
    private static void Place(UiPanel p, bool fullscreen)
    {
        bool anchored = p.AnchorLeft != 0f || p.AnchorTop != 0f || p.AnchorRight != 0f || p.AnchorBottom != 0f;
        if (anchored) return;
        Vector2 size = p.Size;
        if (size.X <= 1f || size.Y <= 1f || fullscreen) { p.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); return; }
        if (p.Position != Vector2.Zero) return;
        p.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
    }

    public void Close(string id)
    {
        if (id == null || !_panels.TryGetValue(id, out var p) || !_open.Contains(p)) return;
        CloseOne(p, id, false);
    }

    /// <summary>
    /// Takes one open panel off the stack. The bookkeeping (stack, backdrop, pause lock) is settled before OnClose
    /// runs, because OnClose may well open something else — or this very panel again (chained conversations).
    /// </summary>
    private void CloseOne(UiPanel p, string id, bool quiet)
    {
        HideTooltip();
        _open.Remove(p);
        if (_dims.Remove(p, out var dim) && IsInstanceValid(dim)) dim.QueueFree();
        if (_locks.Remove(p) && Game.I != null) Game.I.PauseLocks = Math.Max(0, Game.I.PauseLocks - 1);
        if (!quiet) Snd.Ui("ui_close");
        Log.Info("ui", $"close {id}");
        if (!IsInstanceValid(p)) return;
        try { p.OnClose(); } catch (Exception e) { Log.Error("ui", $"{id}.OnClose failed", e); }
        if (!IsInstanceValid(p) || _open.Contains(p)) return;
        p.Visible = false;
        // detach after the current input/signal callback has unwound (a close button lives inside the panel)
        Callable.From(() =>
        {
            if (IsInstanceValid(p) && !_open.Contains(p) && p.GetParent() == _panelLayer) _panelLayer.RemoveChild(p);
        }).CallDeferred();
    }

    public void Toggle(string id)
    {
        if (IsOpen(id)) Close(id); else Open(id);
    }

    public void CloseAll()
    {
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            if (i >= _open.Count) continue;
            Close(_open[i].Id);
        }
        DismissModal(false, true);
        HideTooltip();
    }

    private void OnHeroChanged()
    {
        if (!_refreshing) _dirty = true;
    }

    /// <summary>While anything is open the hero takes no orders; control returns a frame after the last panel closes.</summary>
    private void SyncPlayerInput(bool mayRestore)
    {
        var pl = Game.I?.Player;
        if (AnyOpen)
        {
            if (pl != null && IsInstanceValid(pl) && pl.InputEnabled) { pl.InputEnabled = false; _blocked = pl; }
        }
        else if (mayRestore && _blocked != null)
        {
            if (IsInstanceValid(_blocked)) _blocked.InputEnabled = true;
            _blocked = null;
        }
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        try
        {
            var g = Game.I;
            var vp = GetViewport();
            Control hov = vp?.GuiGetHoveredControl();
            bool over = hov != null && hov.IsVisibleInTree();
            if (!over && g?.Hud != null && g.Hud.PointerOnLabel) over = true;
            _mouseOver = over;
            ShowAllLoot = Input.IsKeyPressed(Key.Alt) || (g?.Cfg != null && g.Cfg.ShowAllLoot);

            // panels freed behind our back must not linger in the stack
            for (int i = _open.Count - 1; i >= 0; i--)
            {
                var p = _open[i];
                if (IsInstanceValid(p)) continue;
                _open.RemoveAt(i);
                if (_dims.Remove(p, out var dim) && IsInstanceValid(dim)) dim.QueueFree();
                if (_locks.Remove(p) && g != null) g.PauseLocks = Math.Max(0, g.PauseLocks - 1);
            }
            SyncPlayerInput(true);

            var hero = g?.Hero;
            if (!ReferenceEquals(hero, _hero))
            {
                if (_hero != null) _hero.Changed -= OnHeroChanged;
                _hero = hero;
                if (hero != null) hero.Changed += OnHeroChanged;
                _dirty = false;
            }
            if (_dirty)
            {
                _dirty = false;
                if (_open.Count > 0)
                {
                    _refreshing = true;
                    for (int i = 0; i < _open.Count; i++)
                    {
                        var p = _open[i];
                        try { p.Refresh(); } catch (Exception e) { UiSafe.Report("refresh of " + p.Id, e); }
                    }
                    _refreshing = false;
                }
            }

            if (_tipShown)
            {
                if (_tipOwner != null && (!IsInstanceValid(_tipOwner) || !_tipOwner.IsVisibleInTree())) HideTooltip();
                else LayoutTooltip();
            }
        }
        catch (Exception e)
        {
            _refreshing = false;
            UiSafe.Report("ui frame", e);
        }
        if (_script != null)
        {
            try { _script.Tick(this); } catch (Exception e) { UiSafe.Report("interface script", e); }
        }
    }

    // ───────────────────────── keys ─────────────────────────

    private static Vector2 _pointer;
    private static bool _pointerKnown;

    /// <summary>
    /// Where the pointer is, in the interface's own coordinates (what Control.GetGlobalMousePosition would give),
    /// taken from the mouse events themselves rather than from the operating system. Everything that polls the
    /// pointer (cells found by rectangle, custom-drawn boards, loot labels) asks here, so synthetic input - the test
    /// driver, a touch screen, a remote session - counts exactly like the real mouse.
    /// </summary>
    public static Vector2 PointerPos(Control c)
    {
        if (_pointerKnown) return _pointer;
        return c != null && c.IsInsideTree() ? c.GetGlobalMousePosition() : Vector2.Zero;
    }

    /// <summary>The pointer in the local coordinates of <paramref name="c"/> (what Control.GetLocalMousePosition would give).</summary>
    public static Vector2 PointerIn(Control c) => c.GetGlobalTransformWithCanvas().AffineInverse() * PointerPos(c);

    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouse mouse) { _pointer = mouse.Position; _pointerKnown = true; }
        // the dialog owns Esc and Enter before anything else sees them
        if (_modal == null || e is not InputEventKey k || !k.Pressed || k.Echo) return;
        Key key = k.Keycode != Key.None ? k.Keycode : k.PhysicalKeycode;
        if (key == Key.Escape)
        {
            GetViewport().SetInputAsHandled();
            DismissModal(false, false);
        }
        else if ((key == Key.Enter || key == Key.KpEnter) && _modalEdit == null)
        {
            GetViewport().SetInputAsHandled();
            DismissModal(true, false);
        }
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey k || !k.Pressed || k.Echo) return;
        var g = Game.I;
        if (g == null || TextFocused()) return;
        Key key = k.Keycode != Key.None ? k.Keycode : k.PhysicalKeycode;
        try
        {
            if (HandleKey(g, key)) GetViewport().SetInputAsHandled();
        }
        catch (Exception ex) { Log.Error("ui", $"hotkey {key} failed", ex); }
    }

    private bool TextFocused()
    {
        var f = GetViewport()?.GuiGetFocusOwner();
        return f is LineEdit || f is TextEdit;
    }

    private bool HandleKey(Game g, Key key)
    {
        if (key == Key.Escape)
        {
            if (_open.Count > 0)
            {
                string top = _open[^1].Id;
                if (top == "death" || top == "dialogue") return false;       // these decide for themselves how they end
                Close(top);
                return true;
            }
            if (g.State == Game.GameState.Play && g.Player != null) { Open("pause"); return true; }
            return false;
        }
        if (g.State != Game.GameState.Play || g.Player == null || g.Hero == null || _modal != null) return false;
        if (IsOpen("dialogue") || IsOpen("death")) return false;

        string id = key switch
        {
            Key.I or Key.C => "inventory",
            Key.K => "skills",
            Key.P => "paragon",
            Key.M or Key.Tab => "map",
            Key.J => "quests",
            _ => null
        };
        if (id != null)
        {
            // a vendor, the pause menu or the settings keep the keyboard to themselves
            if (_open.Count > 0 && Array.IndexOf(MainGroup, _open[^1].Id) < 0) return false;
            if (g.Player.Dead) return false;
            if (id == "paragon" && !IsOpen(id) && g.Hero.D.Level < Balance.LevelCap)
            {
                g.Hud?.Warn($"燼紋盤於等級 {Balance.LevelCap} 開啟");
                return true;
            }
            Toggle(id);
            return true;
        }
        if (key == Key.T && _open.Count == 0)
        {
            TownPortal(g);
            return true;
        }
        return false;
    }

    private void TownPortal(Game g)
    {
        var p = g.Player;
        var lv = g.Level;
        if (p == null || p.Dead) return;
        if (lv != null && IsInstanceValid(lv) && lv.IsTown) { g.Hud?.Warn("你已身在殘燈鎮"); return; }
        if (p.InCombat > 0f)
        {
            g.Hud?.Warn("戰鬥中無法返回殘燈鎮");
            Snd.Ui("ui_error");
            return;
        }
        Log.Info("ui", $"town portal from {(lv != null && IsInstanceValid(lv) ? lv.AreaId : "?")}");
        g.EnterArea("town");
    }

    // ───────────────────────── tooltips ─────────────────────────

    private void BuildTooltips()
    {
        _tip = TipCard(out _tipText);
        _tip2 = TipCard(out _tip2Text);
        _tip2.AddThemeStyleboxOverride("panel", UiTheme.TipBoxDim);
    }

    private Color _tipAccent, _tip2Accent;

    /// <summary>The colour of the first coloured run of a card's text (its title: rarity, element, gold) or a quiet bronze.</summary>
    private static Color AccentOf(string bbcode)
    {
        const string tag = "[color=#";
        int at = bbcode == null ? -1 : bbcode.IndexOf(tag, StringComparison.Ordinal);
        if (at >= 0 && at + tag.Length + 6 <= bbcode.Length)
        {
            try { return new Color(bbcode.Substring(at + tag.Length, 6)); }
            catch (Exception) { }
        }
        return UiTheme.Border.Lightened(0.15f);
    }

    private PanelContainer TipCard(out RichTextLabel text)
    {
        var card = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", UiTheme.TipBox);
        // a thin band along the top edge in the colour of the card's title: rarity at a glance
        card.Draw += () =>
        {
            Color c = ReferenceEquals(card, _tip2) ? _tip2Accent : _tipAccent;
            UiTheme.DrawTipBand(card, c);
        };
        text = UiTheme.Rich("", TipWidth);
        text.AddThemeConstantOverride("line_separation", 2);
        card.AddChild(text);
        _tipLayer.AddChild(card);
        return card;
    }

    /// <summary>Shows a tooltip beside a screen point (usually the pointer); it is kept inside the screen.</summary>
    public void ShowTooltip(string bbcode, Vector2 screenPos, string compareBbcode = null)
    {
        ShowTooltipAt(new Rect2(screenPos + new Vector2(2f, 12f), Vector2.Zero), bbcode, compareBbcode, null);
    }

    /// <summary>
    /// Shows a tooltip beside a rectangle without covering it (to the right, else to the left). When an owner is
    /// given, the tooltip disappears by itself as soon as that control is hidden or freed.
    /// </summary>
    private bool _tipAbove;

    /// <summary>
    /// Shows a tooltip centred above a rectangle (below it when there is no room): for the HUD's bottom cluster,
    /// where a card beside the control would cover the action bar or a globe.
    /// </summary>
    public void ShowTooltipAbove(Rect2 anchor, string bbcode, Control owner = null)
    {
        ShowTooltipAt(anchor, bbcode, null, owner);
        _tipAbove = !string.IsNullOrEmpty(bbcode);
    }

    public void ShowTooltipAt(Rect2 anchor, string bbcode, string compareBbcode = null, Control owner = null)
    {
        _tipAbove = false;
        if (string.IsNullOrEmpty(bbcode)) { HideTooltip(); return; }
        _tipAnchor = anchor;
        _tipOwner = owner;
        if (!_tipShown || bbcode != _tipBb)
        {
            _tipWide = 0;
            SetTipText(_tipText, bbcode, 0);
            _tipBb = bbcode;
            _tipFresh = 2;
            _tipAccent = AccentOf(bbcode);
            _tip.QueueRedraw();
        }
        bool second = !string.IsNullOrEmpty(compareBbcode);
        if (second && (compareBbcode != _tip2Bb || !_tip2.Visible))
        {
            _tip2Wide = 0;
            SetTipText(_tip2Text, compareBbcode, 0);
            _tip2Bb = compareBbcode;
            _tipFresh = 2;
            // the comparison card opens with a dim "目前裝備" line: its accent is the second colour, the worn item's own
            int at2 = compareBbcode.IndexOf("[color=#", Math.Min(12, compareBbcode.Length), StringComparison.Ordinal);
            _tip2Accent = AccentOf(at2 > 0 ? compareBbcode.Substring(at2) : compareBbcode);
            _tip2.QueueRedraw();
        }
        if (!second) _tip2Bb = null;
        _tip2.Visible = second;
        _tip.Visible = true;
        _tipShown = true;
        if (_tipFresh > 0)
        {
            // invisible until the text has been laid out, so the card never flashes at the wrong size or place
            _tip.Modulate = new Color(1f, 1f, 1f, 0f);
            _tip2.Modulate = new Color(1f, 1f, 1f, 0f);
        }
    }

    public void HideTooltip()
    {
        if (!_tipShown && !_tip.Visible) return;
        _tipShown = false;
        _tipOwner = null;
        _tip.Visible = false;
        _tip2.Visible = false;
    }

    /// <summary>Hides the tooltip only if it belongs to this control.</summary>
    public void HideTooltipOf(Control owner)
    {
        if (owner != null && ReferenceEquals(_tipOwner, owner)) HideTooltip();
    }

    private static void SetTipText(RichTextLabel label, string bbcode, int wide)
    {
        // short one-liners hug their text; everything else wraps at the card width. The text is shown as given:
        // whoever built it already sent its [font_size] tags through UiTheme.Fs / Bb (never rewrite them twice).
        bool oneLine = bbcode.IndexOf('\n') < 0 && VisibleLength(bbcode) <= 16 && bbcode.IndexOf("[font_size", StringComparison.Ordinal) < 0;
        label.AutowrapMode = oneLine ? TextServer.AutowrapMode.Off : TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(oneLine ? 0f : MathF.Round(TipWidth * (1f + 0.45f * wide)), 0f);
        if (label.Text != bbcode) label.Text = bbcode;
    }

    private static int VisibleLength(string bbcode)
    {
        int n = 0;
        bool tag = false;
        foreach (char c in bbcode)
        {
            if (c == '[') tag = true;
            else if (c == ']') tag = false;
            else if (!tag) n++;
        }
        return n;
    }

    private void LayoutTooltip()
    {
        const float gap = 12f, edge = 8f;
        Vector2 screen = GetViewportRect().Size;
        _tip.Scale = Vector2.One;
        _tip2.Scale = Vector2.One;
        _tip.ResetSize();
        Vector2 s = _tip.Size;
        float room = screen.Y - edge * 2f;
        if (_tip2.Visible) _tip2.ResetSize();

        // A card taller than the screen (a long legendary at the largest text size) is first wrapped again at a
        // wider column - up to twice, once when a comparison card shares the screen - so the text keeps its size.
        if (_tipFresh <= 1)
        {
            int maxWide = _tip2.Visible ? 1 : 2;
            bool again = false;
            if (s.Y > room && _tipWide < maxWide && _tipText.AutowrapMode != TextServer.AutowrapMode.Off && _tipBb != null)
            {
                _tipWide++;
                SetTipText(_tipText, _tipBb, _tipWide);
                again = true;
            }
            if (_tip2.Visible && _tip2.Size.Y > room && _tip2Wide < maxWide && _tip2Text.AutowrapMode != TextServer.AutowrapMode.Off && _tip2Bb != null)
            {
                _tip2Wide++;
                SetTipText(_tip2Text, _tip2Bb, _tip2Wide);
                again = true;
            }
            if (again)
            {
                _tipFresh = 2;
                _tip.Modulate = new Color(1f, 1f, 1f, 0f);
                _tip2.Modulate = new Color(1f, 1f, 1f, 0f);
                return;
            }
        }

        // Last resort only: a card that still does not fit is shrunk as a whole rather than cut off by the screen edge.
        float fit = Mathf.Min(1f, room / Mathf.Max(1f, s.Y));
        if (_tip2.Visible)
        {
            fit = Mathf.Min(fit, room / Mathf.Max(1f, _tip2.Size.Y));
            fit = Mathf.Min(fit, (screen.X - edge * 2f - 8f) / Mathf.Max(1f, s.X + _tip2.Size.X));
        }
        if (fit < 0.999f)
        {
            _tip.Scale = new Vector2(fit, fit);
            s *= fit;
        }
        Rect2 a = _tipAnchor;
        float x = a.End.X + gap;
        bool left = false;
        if (x + s.X > screen.X - edge)
        {
            x = a.Position.X - gap - s.X;
            left = true;
        }
        x = Mathf.Clamp(x, edge, Mathf.Max(edge, screen.X - s.X - edge));
        float y = Mathf.Clamp(a.Position.Y, edge, Mathf.Max(edge, screen.Y - s.Y - edge));
        if (_tipAbove && !_tip2.Visible)
        {
            x = Mathf.Clamp(a.GetCenter().X - s.X * 0.5f, edge, Mathf.Max(edge, screen.X - s.X - edge));
            y = a.Position.Y - gap - s.Y;
            if (y < edge) y = Mathf.Min(a.End.Y + gap, Mathf.Max(edge, screen.Y - s.Y - edge));
        }
        Vector2 s2v = _tip2.Visible ? _tip2.Size * (fit < 0.999f ? fit : 1f) : Vector2.Zero;
        if (fit < 0.999f && _tip2.Visible) _tip2.Scale = new Vector2(fit, fit);
        // A card for something inside an open panel (an item in the bag, a node in the skill tree) goes into the free
        // screen space beside that panel, so it never covers the panel's own contents.
        if (!_tipAbove && PlaceOutsidePanel(a, s, s2v, screen, gap, edge, out var p1, out var p2))
        {
            _tip.Position = p1;
            if (_tip2.Visible) _tip2.Position = p2;
            FinishTooltipFade();
            return;
        }
        _tip.Position = new Vector2(x, y);

        if (_tip2.Visible)
        {
            Vector2 s2 = s2v;
            float x2 = left ? x - 8f - s2.X : x + s.X + 8f;
            if (x2 < edge || x2 + s2.X > screen.X - edge) x2 = left ? x + s.X + 8f : x - 8f - s2.X;
            x2 = Mathf.Clamp(x2, edge, Mathf.Max(edge, screen.X - s2.X - edge));
            float y2 = Mathf.Clamp(y, edge, Mathf.Max(edge, screen.Y - s2.Y - edge));
            _tip2.Position = new Vector2(x2, y2);
        }
        FinishTooltipFade();
    }

    private void FinishTooltipFade()
    {
        if (_tipFresh <= 0) return;
        _tipFresh--;
        if (_tipFresh == 0)
        {
            _tip.Modulate = Colors.White;
            _tip2.Modulate = Colors.White;
        }
    }

    /// <summary>
    /// The on-screen frame of the open panel that contains a point: the panel's visible direct child under the point
    /// (panels are full-screen roots holding their frame), ignoring full-screen backdrops.
    /// </summary>
    private bool PanelFrameAt(Vector2 point, Vector2 screen, out Rect2 frame)
    {
        frame = default;
        for (int i = _open.Count - 1; i >= 0; i--)
        {
            var panel = _open[i];
            if (!IsInstanceValid(panel) || !panel.Visible) continue;
            foreach (var n in panel.GetChildren())
            {
                if (n is not Control c || !c.Visible) continue;
                Rect2 r = c.GetGlobalRect();
                if (r.Size.X >= screen.X * 0.9f && r.Size.Y >= screen.Y * 0.9f) continue;
                if (!r.HasPoint(point)) continue;
                frame = r;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Places the card (and the comparison card) beside the panel frame that holds the anchor, on the side with more
    /// room: side by side when both fit, else stacked in one column, else not at all (the caller falls back).
    /// </summary>
    private bool PlaceOutsidePanel(Rect2 a, Vector2 s, Vector2 s2, Vector2 screen, float gap, float edge, out Vector2 p1, out Vector2 p2)
    {
        p1 = p2 = Vector2.Zero;
        if (!PanelFrameAt(a.GetCenter(), screen, out Rect2 f)) return false;
        bool two = s2 != Vector2.Zero;
        float roomL = f.Position.X - gap - edge;
        float roomR = screen.X - f.End.X - gap - edge;
        float tall = screen.Y - edge * 2f;
        foreach (bool right in roomR >= roomL ? new[] { true, false } : new[] { false, true })
        {
            float room = right ? roomR : roomL;
            float near = right ? f.End.X + gap : f.Position.X - gap;      // the frame edge the cards start from
            float Col(float w) => right ? near : near - w;                // left x of a card of width w hugging the frame
            if (!two)
            {
                if (s.X > room) continue;
                p1 = new Vector2(Col(s.X), Mathf.Clamp(a.Position.Y, edge, Mathf.Max(edge, screen.Y - s.Y - edge)));
                return true;
            }
            if (s.X + 8f + s2.X <= room)
            {
                // hovered card next to the frame, the worn item's card beyond it
                float x1 = Col(s.X);
                float x2 = right ? x1 + s.X + 8f : x1 - 8f - s2.X;
                float y = Mathf.Clamp(a.Position.Y, edge, Mathf.Max(edge, screen.Y - Mathf.Max(s.Y, s2.Y) - edge));
                p1 = new Vector2(x1, y);
                p2 = new Vector2(x2, y);
                return true;
            }
            if (Mathf.Max(s.X, s2.X) <= room && s.Y + 8f + s2.Y <= tall)
            {
                // one column: the worn item on top, the hovered item right under it
                float h = s2.Y + 8f + s.Y;
                float top = Mathf.Clamp(a.GetCenter().Y - h * 0.5f, edge, Mathf.Max(edge, screen.Y - h - edge));
                p2 = new Vector2(Col(s2.X), top);
                p1 = new Vector2(Col(s.X), top + s2.Y + 8f);
                return true;
            }
        }
        if (!two) return false;
        // Too wide for the free strip and too tall to stack: the pair sits against the screen edge on the far side
        // of the hovered item, as far from it as possible. It may then overlap the panel's outer part (the stats
        // column), but never the hovered item or the rest of its row and column.
        float w = s.X + 8f + s2.X;
        float yb = Mathf.Clamp(a.Position.Y, edge, Mathf.Max(edge, screen.Y - Mathf.Max(s.Y, s2.Y) - edge));
        bool itemOnRight = a.GetCenter().X >= screen.X * 0.5f;
        if (itemOnRight && edge + w <= a.Position.X - gap)
        {
            p2 = new Vector2(edge, yb);                     // worn item at the far left
            p1 = new Vector2(edge + s2.X + 8f, yb);         // hovered item next to it, nearer the item
            return true;
        }
        if (!itemOnRight && a.End.X + gap + w <= screen.X - edge)
        {
            p2 = new Vector2(screen.X - edge - s2.X, yb);
            p1 = new Vector2(screen.X - edge - w, yb);
            return true;
        }
        return false;
    }

    // ───────────────────────── dialogs ─────────────────────────

    public void Confirm(string text, Action onYes, string yes = "確定", string no = "取消")
    {
        var body = UiTheme.Rich("[center]" + (text ?? "") + "[/center]", UiTheme.Px(560f));
        ShowModal(null, body, null, yes, no, onYes, null);
    }

    public void Prompt(string title, string initial, Action<string> onOk)
    {
        var edit = new LineEdit
        {
            Text = initial ?? "", MaxLength = 24, CustomMinimumSize = new Vector2(UiTheme.Px(480f), UiTheme.Px(54f)),
            Alignment = HorizontalAlignment.Center, CaretBlink = true, ContextMenuEnabled = false,
        };
        ShowModal(title, null, edit, "確定", "取消", null, onOk);
    }

    private void ShowModal(string title, Control body, LineEdit edit, string yes, string no, Action onYes, Action<string> onText)
    {
        DismissModal(false, true);        // a newer dialog silently replaces an older one
        HideTooltip();

        var root = new Control { Name = "Dialog", MouseFilter = MouseFilterEnum.Stop };
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0f, 0f, 0f, 0.62f), MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddChild(dim);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddChild(center);
        var frame = UiTheme.Frame(true);
        center.AddChild(frame);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 20);
        frame.AddChild(box);

        if (!string.IsNullOrEmpty(title))
        {
            var head = UiTheme.Label(title, UiTheme.FsTitle, UiTheme.Gold, true);
            head.HorizontalAlignment = HorizontalAlignment.Center;
            box.AddChild(head);
            box.AddChild(UiTheme.Rule());
        }
        if (body != null) box.AddChild(body);
        if (edit != null) box.AddChild(edit);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 24);
        var bYes = UiTheme.Button(yes ?? "確定", () => DismissModal(true, false), UiTheme.FsBody);
        var bNo = UiTheme.Button(no ?? "取消", () => DismissModal(false, false), UiTheme.FsBody);
        bYes.CustomMinimumSize = new Vector2(UiTheme.Px(180f), UiTheme.Px(54f));
        bNo.CustomMinimumSize = new Vector2(UiTheme.Px(180f), UiTheme.Px(54f));
        bYes.FocusMode = FocusModeEnum.None;
        bNo.FocusMode = FocusModeEnum.None;
        row.AddChild(bYes);
        row.AddChild(bNo);
        box.AddChild(row);

        _modalLayer.AddChild(root);
        _modal = root;
        _modalYes = onYes;
        _modalText = onText;
        _modalEdit = edit;
        if (edit != null)
        {
            edit.TextSubmitted += _ => DismissModal(true, false);
            edit.CallDeferred(Control.MethodName.GrabFocus);
            edit.CallDeferred(LineEdit.MethodName.SelectAll);
        }
        if (Game.I != null) { Game.I.PauseLocks++; _modalLocked = true; }
        Snd.Ui("ui_open");
        SyncPlayerInput(false);
    }

    private void DismissModal(bool accept, bool silent)
    {
        if (_modal == null) return;
        string text = _modalEdit != null && IsInstanceValid(_modalEdit) ? (_modalEdit.Text ?? "").Trim() : null;
        if (accept && !silent && _modalEdit != null && string.IsNullOrEmpty(text)) return;      // an empty answer keeps the prompt open

        var node = _modal;
        var yes = _modalYes;
        var onText = _modalText;
        _modal = null;
        _modalYes = null;
        _modalText = null;
        _modalEdit = null;
        if (IsInstanceValid(node)) node.QueueFree();
        if (_modalLocked)
        {
            _modalLocked = false;
            if (Game.I != null) Game.I.PauseLocks = Math.Max(0, Game.I.PauseLocks - 1);
        }
        if (silent || !accept) return;
        if (onText != null) Log.Guard("ui", () => onText(text));
        else if (yes != null) Log.Guard("ui", yes);
    }
}

/// <summary>
/// Drives the interface for screenshot reviews. Inert unless one of these command-line switches is given:
/// <c>--uido=&lt;steps&gt;</c> (steps separated by ';', each "command:argument") or <c>--uido=@&lt;file&gt;</c> (one
/// "command argument" per line, '#' starts a comment); the shorthands <c>--uikeys=K;K</c>, <c>--uiclick=x,y;x,y</c> and
/// <c>--hover=x,y</c>; <c>--uidir=&lt;folder&gt;</c> for the pictures (default Y:\shots\ui).
/// The clock starts when the title screen or the first level is on screen. Positions are WINDOW pixels (what a
/// screenshot shows) or <c>@text</c>: the middle of the visible button, tab or label carrying that text
/// (<c>@text#2</c> = the second match).
/// Commands: wait s · open id[:arg] · close [id] · click/rclick/mclick/dbl/down/up/move/hover p · unhover ·
/// wheel x,y,n · key [Shift+]Name · keydown/keyup Name · text s · shot name · dump name · menu page · scale 0|1|2 ·
/// hud warn|toast|sub|title|flash text · give common|magic|rare|legendary|unique|gem|set [kind] · gold n · mats n ·
/// xp n · life pct · points n · log s · quit.
/// </summary>
internal sealed class UiScript
{
    private readonly List<(string cmd, string arg)> _steps = new();
    private readonly List<(InputEvent ev, int frames)> _later = new();
    private int _at;
    private double _wait;
    private ulong _lastMs;
    private bool _started, _finished, _hoverOn, _waitPlay, _sawLoading;
    private double _holdT;
    private int _depth;
    private readonly HashSet<int> _ownSlots = new();
    private Vector2 _hover;
    private string _dir = @"Y:\shots\ui";

    public static UiScript FromArgs()
    {
        var g = Game.I;
        if (g == null) return null;
        var s = new UiScript();
        try
        {
            string dir = g.ArgValue("uidir");
            if (!string.IsNullOrWhiteSpace(dir)) s._dir = dir.Trim();
            string script = g.ArgValue("uido");
            if (!string.IsNullOrWhiteSpace(script)) s.Load(script.Trim());
            string keys = g.ArgValue("uikeys"), clicks = g.ArgValue("uiclick"), hover = g.ArgValue("hover");
            if (!string.IsNullOrWhiteSpace(keys) || !string.IsNullOrWhiteSpace(clicks) || !string.IsNullOrWhiteSpace(hover))
            {
                s._steps.Add(("wait", "2"));
                foreach (var k in (keys ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) { s._steps.Add(("key", k)); s._steps.Add(("wait", "0.6")); }
                foreach (var c in (clicks ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) { s._steps.Add(("click", c)); s._steps.Add(("wait", "0.6")); }
                if (!string.IsNullOrWhiteSpace(hover)) s._steps.Add(("hover", hover.Trim()));
            }
        }
        catch (Exception e) { Log.Error("uitest", "the interface script could not be read", e); }
        if (s._steps.Count == 0) return null;
        Log.Info("uitest", $"interface script: {s._steps.Count} steps, pictures in {s._dir}");
        return s;
    }

    private void Load(string script)
    {
        IEnumerable<string> raw;
        if (script.StartsWith('@'))
        {
            string file = script[1..].Trim('"');
            if (!System.IO.File.Exists(file)) { Log.Error("uitest", $"script file not found: {file}"); return; }
            raw = System.IO.File.ReadAllLines(file, System.Text.Encoding.UTF8);
        }
        else raw = script.Split(';');
        foreach (string line0 in raw)
        {
            string line = line0.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            int cut = line.IndexOfAny(new[] { ' ', ':', '\t' });
            string cmd = (cut < 0 ? line : line[..cut]).Trim().ToLowerInvariant();
            string arg = cut < 0 ? "" : line[(cut + 1)..].Trim();
            if (cmd == "include") { if (_depth++ < 4) Load("@" + arg); _depth--; continue; }
            if (cmd == "quit" && _depth > 0) continue;              // an included script must not end the run
            _steps.Add((cmd, arg));
        }
    }

    public void Tick(Ui ui)
    {
        var g = Game.I;
        if (g == null) return;
        ulong now = Time.GetTicksMsec();
        if (!_started)
        {
            if (g.State != Game.GameState.Menu && g.State != Game.GameState.Play) return;
            _started = true;
            _lastMs = now;
            Log.Info("uitest", $"script started ({g.State})");
        }
        double dt = (now - _lastMs) / 1000.0;
        _lastMs = now;

        for (int i = 0; i < _later.Count; i++)
        {
            var (ev, frames) = _later[i];
            if (frames > 0) { _later[i] = (ev, frames - 1); continue; }
            Input.ParseInputEvent(ev);
            _later.RemoveAt(i--);
        }
        if (_hoverOn && _later.Count == 0) Motion(_hover);

        if (_finished) return;
        if (_waitPlay)
        {
            // a new game or a journey was asked for: hold the script until the new level is on screen
            if (g.State != Game.GameState.Play) { _sawLoading = true; return; }
            if (!_sawLoading) { _holdT += dt; if (_holdT < 1.5) return; }       // the journey never started
            _waitPlay = false; _holdT = 0;
            _wait = 1.0;
            return;
        }
        _wait -= dt;
        int guard = 0;
        while (_wait <= 0 && !_waitPlay && _later.Count == 0 && _at < _steps.Count && guard++ < 40)
        {
            var (cmd, arg) = _steps[_at++];
            try { Run(ui, g, cmd, arg); }
            catch (Exception e) { Log.Error("uitest", $"step {_at} '{cmd} {arg}' failed", e); }
        }
        if (_at >= _steps.Count && _wait <= 0 && _later.Count == 0) { _finished = true; Log.Info("uitest", "script finished"); }
    }

    private void Run(Ui ui, Game g, string cmd, string arg)
    {
        switch (cmd)
        {
            case "wait": _wait = Num(arg, 0.5); break;
            case "open":
            {
                string id = arg, a = null;
                int colon = id.IndexOf(':');
                if (colon > 0) { a = id[(colon + 1)..]; id = id[..colon]; }
                object payload = a;
                if (id == "dialogue") payload = DialogueDb.Get(a ?? "intro");
                else if (id == "vendor" && a == null) payload = "merchant";
                Log.Info("uitest", $"open {id} {a}");
                ui.Open(id, payload);
                _wait = 0.05;
                break;
            }
            case "close": if (arg.Length == 0) ui.CloseAll(); else ui.Close(arg); _wait = 0.05; break;
            case "click": Click(ui, arg, MouseButton.Left, false); break;
            case "rclick": Click(ui, arg, MouseButton.Right, false); break;
            case "mclick": Click(ui, arg, MouseButton.Middle, false); break;
            case "dbl": Click(ui, arg, MouseButton.Left, true); break;
            case "down": { if (Target(ui, arg, out var p)) { Motion(p); Press(p, MouseButton.Left, true, false, 1); _dragHeld = true; _wait = 0.08; } break; }
            case "up": { if (Target(ui, arg, out var p)) { Motion(p); _dragHeld = false; Press(p, MouseButton.Left, false, false, 1); _wait = 0.08; } break; }
            case "drag":
            {
                // drag x1,y1,x2,y2: press, move in a few steps, release
                var c = arg.Split(',');
                if (c.Length < 4) { Log.Warn("uitest", "drag needs x1,y1,x2,y2"); break; }
                var a = new Vector2((float)Num(c[0], 0), (float)Num(c[1], 0)); var b = new Vector2((float)Num(c[2], 0), (float)Num(c[3], 0));
                Motion(a);
                Press(a, MouseButton.Left, true, false, 1);
                for (int i = 1; i <= 6; i++)
                {
                    Vector2 at = a.Lerp(b, i / 6f), from = a.Lerp(b, (i - 1) / 6f);
                    _later.Add((new InputEventMouseMotion { Position = at, GlobalPosition = at, Relative = at - from, ButtonMask = MouseButtonMask.Left }, 1 + i * 2));
                }
                Press(b, MouseButton.Left, false, false, 16);
                _lastMotion = b;
                break;
            }
            case "move": { if (Target(ui, arg, out var p)) { _hoverOn = false; Motion(p); _wait = 0.03; } break; }
            case "hover": { if (Target(ui, arg, out var p)) { _hover = p; _hoverOn = true; Motion(p); _wait = 0.03; } break; }
            case "unhover": _hoverOn = false; Motion(new Vector2(3f, 3f)); break;
            case "wheel":
            {
                var parts = arg.Split(',');
                if (parts.Length < 3) { Log.Warn("uitest", "wheel needs x,y,n"); break; }
                var p = new Vector2((float)Num(parts[0], 0), (float)Num(parts[1], 0));
                int n = (int)Num(parts[2], -1);
                Motion(p);
                for (int i = 0; i < Math.Abs(n); i++)
                {
                    var b = n > 0 ? MouseButton.WheelUp : MouseButton.WheelDown;
                    Press(p, b, true, false, 1 + i * 2);
                    Press(p, b, false, false, 2 + i * 2);
                }
                break;
            }
            case "key": KeyEvent(arg, true, 0); KeyEvent(arg, false, 2); break;
            case "keydown": KeyEvent(arg, true, 0); break;
            case "keyup": KeyEvent(arg, false, 0); break;
            case "text":
            {
                var f = ui.GetViewport()?.GuiGetFocusOwner();
                if (f is LineEdit le) { le.Text = arg; le.CaretColumn = arg.Length; le.EmitSignal(LineEdit.SignalName.TextChanged, arg); }
                else Log.Warn("uitest", $"text: no text field has the focus ({f?.GetType().Name ?? "nothing"})");
                break;
            }
            case "shot":
            {
                string path = PathOf(arg, ".png");
                Log.Info("uitest", $"shot {path}");
                g.HarnessShot(path);
                _wait = 0.12;
                break;
            }
            case "dump": Dump(ui, arg); break;
            case "menu":
            {
                MainMenu menu = null;
                if (g.UiLayer != null) foreach (var n in g.UiLayer.GetChildren()) if (n is MainMenu m) menu = m;
                if (menu == null) Log.Warn("uitest", "menu: the title screen is not on screen");
                else menu.Open(arg);
                _wait = 0.05;
                break;
            }
            case "scale": UiTheme.ApplyTextScale((int)Num(arg, 0)); _wait = 0.1; break;
            case "hud": HudCmd(g, arg); break;
            case "give": Give(g, arg); break;
            case "gold": if (g.Hero != null) { g.Hero.D.Gold = (long)Num(arg, 100000); g.Hero.Notify(); } break;
            case "mats": if (g.Hero != null) { int n = (int)Num(arg, 500); g.Hero.D.Scrap = n; g.Hero.D.Dust = n; g.Hero.D.Cores = n; g.Hero.Notify(); } break;
            case "xp": g.Hero?.AddXp((float)Num(arg, 1000)); break;
            case "points": if (g.Hero != null) { g.Hero.D.BonusSkillPts += (int)Num(arg, 5); g.Hero.Recalc(); g.Hero.Notify(); } break;
            case "ppoints": if (g.Hero != null) { g.Hero.D.BonusParagonPts += (int)Num(arg, 5); g.Hero.Recalc(); g.Hero.Notify(); } break;
            case "res": if (g.Player != null && g.Hero != null) g.Player.Resource = g.Hero.MaxResource * (float)Num(arg, 100) / 100f; break;
            case "pips": if (g.Player != null && g.Hero != null) g.Player.Pips = Math.Clamp((int)Num(arg, 3), 0, g.Hero.MaxPips); break;
            case "drop":
            {
                // one item of every rarity, a gem, gold and materials on the ground around the hero (loot labels)
                var pl = g.Player; var h = g.Hero;
                if (pl == null || h == null) break;
                int power = Math.Clamp(h.D.Level * 10 + h.D.Tier * 30, 10, ItemDb.MaxPower);
                Rarity[] rs = { Rarity.Common, Rarity.Magic, Rarity.Rare, Rarity.Legendary, Rarity.Unique };
                for (int i = 0; i < rs.Length; i++)
                {
                    float a = -2.4f + i * 0.5f;
                    Loot.DropItem(ItemDb.Generate(power, rs[i], h.Class, i % 2 == 0 ? ItemKind.Weapon : ItemKind.Chest), pl.Pos2 + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (3.2f + (i % 2) * 1.6f));
                }
                Loot.DropItem(ItemDb.RandomGem(h.D.Level), pl.Pos2 + new Vector2(2.6f, 2.2f));
                Loot.DropGold(137, pl.Pos2 + new Vector2(-2.4f, 2.0f));
                break;
            }
            case "boss":
            {
                // wakes the level's boss for the boss frame: a scratch of damage, and a stagger value when given (0-100, 100 = reeling)
                var b = g.Level?.Boss ?? g.Level?.BossMon;
                if (b == null || !GodotObject.IsInstanceValid(b)) { Log.Warn("uitest", "boss: this level has none"); break; }
                b.TakeHit(new Hit { Source = g.Player, Amount = b.MaxLife * 0.27f, El = Element.Fire });
                if (arg.Length > 0) { float st = (float)Num(arg, 50); b.Stagger = Math.Min(99f, st); if (st >= 100f) b.StaggerLeft = 6f; }
                Log.Info("uitest", $"boss {b.DisplayName}: life {b.Life:0}/{b.MaxLife:0} stagger {b.Stagger:0} reeling={b.Staggered}");
                break;
            }
            case "life": if (g.Player != null) g.Player.Life = g.Player.MaxLife * (float)Num(arg, 20) / 100f; break;
            case "log": Log.Info("uitest", arg); break;
            case "newgame":
            {
                // newgame <classKey> [level] [area] [paragon] [hardcore]: a fresh test hero in the harness slot, without restarting the game
                string[] p = arg.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                int idx = p.Length > 0 ? Array.IndexOf(Names.ClassKeys, p[0].ToLowerInvariant()) : 0;
                if (idx < 0) { Log.Warn("uitest", $"newgame: unknown class '{p[0]}'"); idx = 0; }
                int level = p.Length > 1 ? (int)Num(p[1], 20) : 20;
                string area = p.Length > 2 ? p[2] : "town";
                int paragon = p.Length > 3 ? (int)Num(p[3], -1) : -1;
                bool hc = p.Length > 4 && p[4].StartsWith("h", StringComparison.OrdinalIgnoreCase);
                ui.CloseAll();
                g.HarnessNext(area, level, true, -1, false, paragon);
                g.NewGame(Game.HarnessSlot, "試煉者", (ClassId)idx, hc);
                _waitPlay = true; _sawLoading = false;
                break;
            }
            case "saveas":
            {
                // saveas <slot 1-8> [dead]: a copy of the current hero in a real save slot (to dress the load screen)
                string[] p = arg.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                int slot = p.Length > 0 ? (int)Num(p[0], 0) : 0;
                if (g.Hero == null || slot < 1 || slot > SaveSystem.SlotCount) { Log.Warn("uitest", "saveas: needs a hero and a slot 1-8"); break; }
                // never touch a slot this script did not create itself: a real character may live there
                if (!_ownSlots.Contains(slot))
                {
                    bool taken = false;
                    foreach (var info in SaveSystem.List()) if (info.Slot == slot && info.Exists) taken = true;
                    if (taken) { Log.Warn("uitest", $"saveas: slot {slot} already holds a character; left alone"); break; }
                    _ownSlots.Add(slot);
                }
                var d = g.Hero.D;
                bool wasDead = d.Dead, wasHc = d.Hardcore; string wasName = d.Name; double wasTime = d.PlayTime;
                if (p.Length > 1 && p[1] == "dead") { d.Dead = true; d.Hardcore = true; }
                if (p.Length > 2) d.Name = p[2];
                d.PlayTime = 3600.0 * slot * 2.7 + 420;
                bool ok = SaveSystem.Save(slot, d);
                d.Dead = wasDead; d.Hardcore = wasHc; d.Name = wasName; d.PlayTime = wasTime;
                Log.Info("uitest", $"saveas slot {slot}: {ok}");
                break;
            }
            case "delsave":
            {
                int slot = (int)Num(arg, 0);
                if (!_ownSlots.Remove(slot)) { Log.Warn("uitest", $"delsave: slot {slot} was not created by this script; left alone"); break; }
                SaveSystem.Delete(slot);
                Log.Info("uitest", $"delsave slot {slot}");
                break;
            }
            case "travel": ui.CloseAll(); g.EnterArea(arg); _waitPlay = true; _sawLoading = false; break;
            case "waitplay": _waitPlay = true; _sawLoading = true; break;
            case "die":
            {
                var pl = g.Player;
                if (pl == null || pl.Dead) break;
                pl.IFrames = 0f; pl.Barrier = 0f;
                pl.TakeHit(new Hit { Source = null, Amount = pl.MaxLife * 50f, El = Element.Shadow });
                break;
            }
            case "quit":
                // whatever save slots this script dressed for a picture are removed again before the game closes
                foreach (int own in _ownSlots) { SaveSystem.Delete(own); Log.Info("uitest", $"quit: removed test slot {own}"); }
                _ownSlots.Clear();
                Log.Info("uitest", "quit");
                g.Quit();
                break;
            default: Log.Warn("uitest", $"unknown command '{cmd}'"); break;
        }
    }

    private static double Num(string s, double fallback) =>
        double.TryParse((s ?? "").Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : fallback;

    private string PathOf(string name, string ext)
    {
        name = (name ?? "").Trim().Trim('"');
        if (name.Length == 0) name = "shot";
        if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) name += ext;
        return System.IO.Path.IsPathRooted(name) ? name : System.IO.Path.Combine(_dir, name);
    }

    // ───────────────────────── synthetic input ─────────────────────────

    private static Vector2 _lastMotion;
    private static bool _dragHeld;

    /// <summary>A pointer move; while a 'down' is being held it carries the button and the distance moved, so drags work.</summary>
    private static void Motion(Vector2 win)
    {
        var ev = new InputEventMouseMotion { Position = win, GlobalPosition = win, Relative = _dragHeld ? win - _lastMotion : Vector2.Zero };
        if (_dragHeld) ev.ButtonMask = MouseButtonMask.Left;
        _lastMotion = win;
        Input.ParseInputEvent(ev);
    }

    private void Press(Vector2 win, MouseButton button, bool pressed, bool dbl, int afterFrames)
    {
        var ev = new InputEventMouseButton
        {
            Position = win, GlobalPosition = win, ButtonIndex = button, Pressed = pressed, DoubleClick = dbl && pressed,
            ButtonMask = pressed ? (button == MouseButton.Left ? MouseButtonMask.Left : button == MouseButton.Right ? MouseButtonMask.Right : button == MouseButton.Middle ? MouseButtonMask.Middle : 0) : 0,
        };
        _later.Add((ev, afterFrames));
    }

    private void Click(Ui ui, string arg, MouseButton button, bool dbl)
    {
        if (!Target(ui, arg, out var p)) return;
        Motion(p);
        Press(p, button, true, false, 1);
        Press(p, button, false, false, 3);
        if (dbl) { Press(p, button, true, true, 5); Press(p, button, false, false, 7); }
    }

    private void KeyEvent(string name, bool pressed, int afterFrames)
    {
        bool shift = false, ctrl = false, alt = false;
        string n = name.Trim();
        while (true)
        {
            if (n.StartsWith("shift+", StringComparison.OrdinalIgnoreCase)) { shift = true; n = n[6..]; }
            else if (n.StartsWith("ctrl+", StringComparison.OrdinalIgnoreCase)) { ctrl = true; n = n[5..]; }
            else if (n.StartsWith("alt+", StringComparison.OrdinalIgnoreCase)) { alt = true; n = n[4..]; }
            else break;
        }
        Key key = Key.None;
        if (n.Length == 1 && char.IsDigit(n[0])) key = Key.Key0 + (n[0] - '0');
        else if (!Enum.TryParse(n, true, out key)) { Log.Warn("uitest", $"key: unknown key '{name}'"); return; }
        var ev = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed, ShiftPressed = shift, CtrlPressed = ctrl, AltPressed = alt };
        if (afterFrames <= 0) Input.ParseInputEvent(ev); else _later.Add((ev, afterFrames));
    }

    // ───────────────────────── targets ─────────────────────────

    /// <summary>"x,y" in window pixels, or "@text[#n]": the middle of a visible control carrying that text.</summary>
    private bool Target(Ui ui, string arg, out Vector2 win)
    {
        win = default;
        arg = (arg ?? "").Trim();
        if (arg.StartsWith('@'))
        {
            string text = arg[1..];
            int nth = 0;
            int hash = text.LastIndexOf('#');
            if (hash > 0 && int.TryParse(text[(hash + 1)..], out int k)) { nth = k; text = text[..hash]; }
            var found = new List<(Rect2 rect, int score)>();
            Collect(Game.I?.UiLayer, text, found);
            if (found.Count == 0) { Log.Warn("uitest", $"target '{arg}' not found on screen"); return false; }
            (Rect2 rect, int score) pick;
            if (nth > 0)
            {
                if (nth > found.Count) { Log.Warn("uitest", $"target '{arg}': only {found.Count} matches"); return false; }
                pick = found[nth - 1];
            }
            else
            {
                pick = found[0];
                foreach (var f in found) if (f.score >= pick.score) pick = f;      // the best kind of match, and the top-most of those
            }
            Transform2D toWin = ui.GetViewport().GetFinalTransform();
            win = toWin * pick.rect.GetCenter();
            Log.Info("uitest", $"target '{arg}' -> window {win.X:0},{win.Y:0} ({found.Count} matches)");
            return true;
        }
        var parts = arg.Split(',');
        if (parts.Length < 2) { Log.Warn("uitest", $"bad position '{arg}'"); return false; }
        win = new Vector2((float)Num(parts[0], 0), (float)Num(parts[1], 0));
        return true;
    }

    private static void Collect(Node n, string text, List<(Rect2, int)> found)
    {
        if (n == null) return;
        if (n is CanvasItem ci && !ci.Visible) return;
        if (n is Control c && c.Size.X > 1f && c.Size.Y > 1f)
        {
            Transform2D xf = c.GetGlobalTransformWithCanvas();
            if (c is TabBar tabs)
            {
                for (int i = 0; i < tabs.TabCount; i++)
                {
                    string t = tabs.GetTabTitle(i) ?? "";
                    if (t.Contains(text, StringComparison.Ordinal))
                    {
                        Rect2 r = tabs.GetTabRect(i);
                        found.Add((new Rect2(xf * r.Position, r.Size * xf.Scale), t == text ? 4 : 3));
                    }
                }
            }
            else
            {
                string t = c is Button b ? b.Text : c is Label l ? l.Text : c is RichTextLabel r ? r.GetParsedText() : null;
                if (!string.IsNullOrEmpty(t) && t.Contains(text, StringComparison.Ordinal))
                {
                    int score = c is Button ? (t.Trim() == text ? 4 : 3) : (t.Trim() == text ? 2 : 1);
                    found.Add((new Rect2(xf.Origin, c.Size * xf.Scale), score));
                }
            }
        }
        int count = n.GetChildCount(true);
        for (int i = 0; i < count; i++) Collect(n.GetChild(i, true), text, found);
    }

    // ───────────────────────── layout dump and lint ─────────────────────────

    /// <summary>
    /// Writes every visible control that carries text or takes clicks (window-pixel rectangles), and flags what looks
    /// wrong: text cut by its box, boxes smaller than their content, controls outside the screen or outside the frame
    /// they belong to, fonts below the floor.
    /// </summary>
    private void Dump(Ui ui, string name)
    {
        var sb = new System.Text.StringBuilder(8192);
        Transform2D toWin = ui.GetViewport().GetFinalTransform();
        Rect2 screen = new(Vector2.Zero, ui.GetViewportRect().Size);
        int shown = 0, issues = 0;
        var problems = new List<string>();

        void Walk(Node n, int depth, bool clipped, Control frame)
        {
            if (n is CanvasItem ci && !ci.Visible) return;
            if (n is Control c)
            {
                Transform2D xf = c.GetGlobalTransformWithCanvas();
                Rect2 r = new(xf.Origin, c.Size * xf.Scale);
                string text = c is Button b ? b.Text : c is Label l ? l.Text : c is RichTextLabel rt ? rt.GetParsedText() : c is LineEdit le ? le.Text : null;
                string flags;
                try { flags = Lint(c, r, screen, clipped, frame, text); } catch (Exception e) { flags = " !LINTFAIL " + e.Message; }
                bool interesting = !string.IsNullOrEmpty(text) || c is BaseButton || c is TabBar || c is UiPanel || c is LineEdit || c is Godot.Range || flags.Length > 0;
                if (interesting && r.Size.X > 0.5f && r.Size.Y > 0.5f)
                {
                    Rect2 w = new(toWin * r.Position, r.Size * toWin.Scale);
                    string t = text == null ? "" : text.Replace("\n", "⏎");
                    if (t.Length > 60) t = t[..60] + "…";
                    string extra = "";
                    if (c is TabBar tb)
                    {
                        var names = new List<string>();
                        for (int i = 0; i < tb.TabCount; i++) names.Add((i == tb.CurrentTab ? "*" : "") + tb.GetTabTitle(i));
                        extra = " tabs=[" + string.Join("|", names) + "]";
                    }
                    int fs = c is Label or Button or LineEdit ? c.GetThemeFontSize("font_size") : c is RichTextLabel ? c.GetThemeFontSize("normal_font_size") : 0;
                    string line = $"{new string(' ', Math.Min(depth, 24))}{c.GetType().Name} [{w.Position.X:0},{w.Position.Y:0} {w.Size.X:0}x{w.Size.Y:0}]{(fs > 0 ? " fs" + fs : "")} \"{t}\"{extra}{flags}";
                    sb.AppendLine(line);
                    shown++;
                    if (flags.Length > 0) { issues++; problems.Add(line.Trim()); }
                }
                if (c is PanelContainer || c is Panel || c is UiPanel || c is TabContainer) frame = c;
                if (c.ClipContents || c is ScrollContainer) { clipped = true; frame = null; }
            }
            int count = n.GetChildCount(true);
            for (int i = 0; i < count; i++) Walk(n.GetChild(i, true), depth + 1, clipped, frame);
        }

        Walk(Game.I?.UiLayer, 0, false, null);
        string path = PathOf(name.Length == 0 ? "dump" : name, ".txt");
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? ".");
            var head = new System.Text.StringBuilder();
            head.AppendLine($"# {name}: {shown} controls, {issues} flagged, text scale x{UiTheme.TextScale:0.00}, window px (design {screen.Size.X:0}x{screen.Size.Y:0})");
            foreach (var p in problems) head.AppendLine("ISSUE " + p);
            head.AppendLine("# ---- tree ----");
            System.IO.File.WriteAllText(path, head.ToString() + sb, System.Text.Encoding.UTF8);
        }
        catch (Exception e) { Log.Error("uitest", $"dump {path} failed", e); }
        Log.Info("uitest", $"dump {name}: {shown} controls, {issues} flagged -> {path}");
    }

    private static string Lint(Control c, Rect2 r, Rect2 screen, bool clipped, Control frame, string text)
    {
        string f = "";
        bool hasText = !string.IsNullOrEmpty(text);
        if (!clipped && (hasText || c is BaseButton) && r.Size.X > 1f && r.Size.Y > 1f && !screen.Grow(1.5f).Encloses(r)) f += " !OFFSCREEN";
        if (!clipped && frame != null && (hasText || c is BaseButton) && c is not UiPanel)
        {
            Transform2D fx = frame.GetGlobalTransformWithCanvas();
            Rect2 fr = new(fx.Origin, frame.Size * fx.Scale);
            if (!fr.Grow(2f).Encloses(r)) f += $" !OUTSIDE({frame.GetType().Name} {fr.Size.X:0}x{fr.Size.Y:0})";
        }
        Vector2 min = c.GetCombinedMinimumSize();
        if ((hasText || c is BaseButton) && (min.X > c.Size.X + 1.5f || min.Y > c.Size.Y + 1.5f)) f += $" !SMALLER-THAN-CONTENT(min {min.X:0}x{min.Y:0}, has {c.Size.X:0}x{c.Size.Y:0})";
        if (c is Label l && hasText)
        {
            int fs = l.GetThemeFontSize("font_size");
            if (fs < UiTheme.MinFont) f += $" !TINY({fs})";
            if (l.AutowrapMode != TextServer.AutowrapMode.Off)
            {
                if (l.GetVisibleLineCount() < l.GetLineCount()) f += $" !LINES-CUT({l.GetVisibleLineCount()}/{l.GetLineCount()})";
            }
            else if (l.ClipText || l.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming)
            {
                var font = l.GetThemeFont("font");
                float w = 0f;
                foreach (string line in text.Split('\n')) w = Mathf.Max(w, font.GetStringSize(line, HorizontalAlignment.Left, -1f, fs).X);
                if (w > l.Size.X + 1.5f) f += $" !TRIMMED(text {w:0} > {l.Size.X:0})";
            }
        }
        else if (c is Button b && hasText)
        {
            int fs = b.GetThemeFontSize("font_size");
            if (fs < UiTheme.MinFont) f += $" !TINY({fs})";
            if (b.ClipText || b.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming)
            {
                var font = b.GetThemeFont("font");
                var box = b.GetThemeStylebox("normal");
                float room = b.Size.X - (box != null ? box.GetMinimumSize().X : 0f) - (b.Icon != null ? b.Icon.GetWidth() + 8f : 0f);
                float w = 0f;
                foreach (string line in text.Split('\n')) w = Mathf.Max(w, font.GetStringSize(line, HorizontalAlignment.Left, -1f, fs).X);      // two-line buttons: the widest line counts
                if (w > room + 1.5f) f += $" !TRIMMED(text {w:0} > {room:0})";
            }
        }
        else if (c is RichTextLabel rt && hasText)
        {
            int fs = rt.GetThemeFontSize("normal_font_size");
            if (fs < UiTheme.MinFont) f += $" !TINY({fs})";
            if (!rt.ScrollActive && rt.GetContentHeight() > rt.Size.Y + 2f) f += $" !RICH-CUT(content {rt.GetContentHeight()} > {rt.Size.Y:0})";
        }
        return f;
    }

    // ───────────────────────── game-side helpers ─────────────────────────

    private static void HudCmd(Game g, string arg)
    {
        var hud = g.Hud;
        if (hud == null) return;
        int cut = arg.IndexOfAny(new[] { ' ', ':' });
        string what = (cut < 0 ? arg : arg[..cut]).ToLowerInvariant(), rest = cut < 0 ? "" : arg[(cut + 1)..];
        string[] two = rest.Split('|');
        switch (what)
        {
            case "warn": hud.Warn(rest.Length > 0 ? rest : "法力不足"); break;
            case "toast": hud.Toast(rest.Length > 0 ? rest : "獲得 128 金幣", Palette.Gold); break;
            case "sub": hud.Subtitle(two.Length > 1 ? two[0] : "引燈長老", two.Length > 1 ? two[1] : (rest.Length > 0 ? rest : "燼火未熄，孩子。只要還有人肯往深處走，這座鎮就還沒有輸。"), 8f); break;
            case "title": hud.AreaTitle(two[0].Length > 0 ? two[0] : "灰燼荒原", two.Length > 1 ? two[1] : "第一章　餘燼之地"); break;
            case "flash": hud.Flash(new Color(1f, 0.85f, 0.4f, 0.4f), 1.2f); break;
            default: Log.Warn("uitest", $"hud: unknown '{what}'"); break;
        }
    }

    private static void Give(Game g, string arg)
    {
        var h = g.Hero;
        if (h == null) return;
        string[] p = arg.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
        string what = p.Length > 0 ? p[0].ToLowerInvariant() : "set";
        ItemKind? kind = null;
        if (p.Length > 1 && Enum.TryParse(p[1], true, out ItemKind k)) kind = k;
        int power = Math.Clamp(h.D.Level * 10 + h.D.Tier * 30, 10, ItemDb.MaxPower);
        void Add(Item it) { if (it != null && !h.BagFull) { it.ReqLevel = Math.Min(it.ReqLevel, h.D.Level); h.D.Bag.Add(it); } }
        switch (what)
        {
            case "gem":
                foreach (var gd in ItemDb.Gems)
                {
                    if (p.Length > 1 && !gd.Id.Contains(p[1], StringComparison.OrdinalIgnoreCase)) continue;
                    Add(ItemDb.MakeGem(gd.Id));
                    if (h.BagFull) break;
                }
                break;
            case "set":
                foreach (Rarity r in new[] { Rarity.Common, Rarity.Magic, Rarity.Rare, Rarity.Legendary, Rarity.Unique })
                {
                    Add(ItemDb.Generate(power, r, h.Class, ItemKind.Weapon));
                    Add(ItemDb.Generate(power, r, h.Class, ItemKind.Chest));
                    Add(ItemDb.Generate(power, r, h.Class, ItemKind.Ring));
                }
                {
                    var soc = ItemDb.Generate(power, Rarity.Legendary, h.Class, ItemKind.Chest);
                    soc.Sockets = 2; soc.Upgrade = 5;
                    Add(soc);
                    // one stone of every kind, and three of the first so the jeweler has something to combine
                    int n = 0;
                    foreach (var gd in ItemDb.Gems)
                    {
                        if (gd.Tier != 1) continue;
                        Add(ItemDb.MakeGem(gd.Id));
                        if (n == 0) { Add(ItemDb.MakeGem(gd.Id)); Add(ItemDb.MakeGem(gd.Id)); }
                        if (++n >= 7) break;
                    }
                }
                break;
            default:
                if (Enum.TryParse(what, true, out Rarity rr)) Add(ItemDb.Generate(power, rr, h.Class, kind));
                else Log.Warn("uitest", $"give: unknown '{what}'");
                break;
        }
        h.Notify();
        Log.Info("uitest", $"give {arg}: bag now holds {h.D.Bag.Count} items");
    }
}
