using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace AshenAbyss;

/// <summary>
/// The in-game heads-up display. It owns no game state: every frame it polls Game.I / World and feeds its parts —
/// the bottom cluster (globes, action bar, potion, evade, experience), the buff row, the minimap corner with the
/// area name, quest tracker and dungeon objective, the boss / target frame, the world-space overlays (health bars,
/// damage numbers, loot labels, prompts), and the transient notices (toasts, warnings, title cards, subtitles,
/// banners). It also draws the low-life vignette and full-screen flashes.
/// Every part tolerates a missing player or level, so the HUD is safe during loading and in the menu.
/// </summary>
public partial class Hud : Control
{
    private static readonly StringName PStrength = "strength";

    private readonly Control _chrome;
    private readonly ColorRect _vignette, _flash;
    private readonly ShaderMaterial _vigMat;
    private readonly HudWorld _world;
    private readonly HudBar _bar;
    private readonly HudBuffs _buffs;
    private readonly HudMinimap _map;
    private readonly HudBossBar _boss;
    private readonly HudNotices _notices;
    private readonly Label _areaName, _areaInfo, _objective;
    private readonly RichTextLabel _tracker;
    private readonly ProgressBar _objBar;
    private readonly HBoxContainer _remind;
    private readonly Button _skillBtn, _paragonBtn;
    private readonly StringBuilder _sb = new(256);

    private float _clock, _slow, _bossT, _bossBand;
    private float _vig = -1f, _hitPulse, _lowPhase;
    private Color _flashCol;
    private float _flashT = 1f, _flashLife = 1f, _flashPeak;
    private Hero _hero;
    private int _lastLevel, _lastParagon;
    private Monster _bossPick;
    private string _areaShown, _infoShown, _objShown;
    private int _trackerHash, _skillPts = -1, _paragonPts = -1;
    private float _objProg = -1f;
    private static Func<Hero, int> _paragonFree;

    /// <summary>True while the pointer rests on a loot label or prompt (the click belongs to the HUD, not the ground).</summary>
    public bool PointerOnLabel => _world != null && _world.PointerObj != null;
    /// <summary>The object whose loot label or prompt the pointer rests on, or null (read by the mouse cursor, Cursors.cs).</summary>
    public Interactable PointerObj => _world?.PointerObj;
    /// <summary>展示模式 (DemoMode.cs): only the world layer (names, bars, damage, loot labels), the boss frame and the notices show.</summary>
    public bool Lean;
    private bool _leanShown;

    public Hud()
    {
        Name = "Hud";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiTheme.Theme;

        // screen-edge vignette (low life, heavy hits) sits under everything else
        _vigMat = new ShaderMaterial { Shader = HudShaders.Vignette };
        _vignette = new ColorRect { Name = "Vignette", Material = _vigMat, Color = Colors.White, MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _vignette.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_vignette);

        _world = new HudWorld { Visible = false };
        AddChild(_world);

        _chrome = new Control { Name = "Chrome", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        _chrome.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_chrome);

        // ── top centre: boss / target frame ──
        _boss = new HudBossBar();
        _boss.AnchorLeft = 0.5f; _boss.AnchorRight = 0.5f;
        _boss.OffsetLeft = -HudBossBar.W * 0.5f; _boss.OffsetRight = HudBossBar.W * 0.5f;
        _boss.OffsetTop = 22f; _boss.OffsetBottom = 22f + HudBossBar.H;
        _chrome.AddChild(_boss);

        // ── top right: minimap, area, quest tracker, objective ──
        // The text column is wider than the minimap and grows with the text scale (Relayout): the tracker's lines
        // are list rows (22 design px) and wrap inside it, right-aligned under the map.
        _corner = new VBoxContainer { Name = "Corner", MouseFilter = MouseFilterEnum.Ignore };
        var corner = _corner;
        corner.AddThemeConstantOverride("separation", 2);
        corner.AnchorLeft = 1f; corner.AnchorRight = 1f;
        _chrome.AddChild(corner);

        _map = new HudMinimap { SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        corner.AddChild(_map);
        _areaName = UiTheme.Outlined(UiTheme.Label("", AreaNameSize, UiTheme.Gold, true), 6);
        _areaName.HorizontalAlignment = HorizontalAlignment.Right;
        _areaName.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        corner.AddChild(_areaName);
        _areaInfo = UiTheme.Outlined(UiTheme.Small("", UiTheme.FsCaption, new Color(0.74f, 0.71f, 0.65f)), 5);
        _areaInfo.HorizontalAlignment = HorizontalAlignment.Right;
        _areaInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        corner.AddChild(_areaInfo);
        corner.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 6f), MouseFilter = MouseFilterEnum.Ignore });

        _tracker = UiTheme.Rich("", CornerWidth, UiTheme.FsRow);
        _tracker.AddThemeConstantOverride("outline_size", 5);
        _tracker.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        _tracker.AddThemeConstantOverride("line_separation", 1);
        _tracker.Visible = false;
        if (UiTheme.Art)
            _tracker.AddThemeStyleboxOverride("normal", UiTheme.ArtBox("tracker", () => new UiNine("tracker").Margins(12f, 8f, 14f, 9f)));
        corner.AddChild(_tracker);

        _objective = UiTheme.Outlined(UiTheme.Label("", UiTheme.FsRow, UiTheme.Text), 5);
        _objective.HorizontalAlignment = HorizontalAlignment.Right;
        _objective.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _objective.Visible = false;
        corner.AddChild(_objective);
        _objBar = new ProgressBar
        {
            MinValue = 0.0, MaxValue = 1.0, Step = 0.0, Value = 0.0, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(240f, 10f), SizeFlagsHorizontal = SizeFlags.ShrinkEnd, Visible = false,
        };
        corner.AddChild(_objBar);

        // ── bottom centre: globes and action bar, buffs above the plate, reminders above the cluster ──
        _bar = new HudBar();
        _bar.AnchorLeft = 0.5f; _bar.AnchorRight = 0.5f; _bar.AnchorTop = 1f; _bar.AnchorBottom = 1f;
        _bar.OffsetLeft = -HudBar.W * 0.5f; _bar.OffsetRight = HudBar.W * 0.5f;
        _bar.OffsetTop = -(HudBar.H + 8f); _bar.OffsetBottom = -8f;
        _chrome.AddChild(_bar);

        _buffs = new HudBuffs();
        _buffs.AnchorLeft = 0.5f; _buffs.AnchorRight = 0.5f; _buffs.AnchorTop = 1f; _buffs.AnchorBottom = 1f;
        _chrome.AddChild(_buffs);

        _remind = new HBoxContainer { Name = "Reminders", MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        _remind.AddThemeConstantOverride("separation", 14);
        _remind.AnchorLeft = 0.5f; _remind.AnchorRight = 0.5f; _remind.AnchorTop = 1f; _remind.AnchorBottom = 1f;
        _remind.GrowVertical = GrowDirection.Begin;
        _chrome.AddChild(_remind);
        _skillBtn = ReminderButton("skills");
        _paragonBtn = ReminderButton("paragon");
        _remind.AddChild(_skillBtn);
        _remind.AddChild(_paragonBtn);

        _notices = new HudNotices();
        AddChild(_notices);

        _flash = new ColorRect { Name = "Flash", MouseFilter = MouseFilterEnum.Ignore, Visible = false, Color = new Color(0f, 0f, 0f, 0f) };
        _flash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_flash);

        Relayout();
    }

    /// <summary>Design size of the area name under the minimap, and the design width of the text column there.</summary>
    private const int AreaNameSize = 26;
    private const float CornerWidth = 400f;
    private readonly VBoxContainer _corner;

    public override void _EnterTree()
    {
        UiTheme.TextScaleChanged -= OnTextScale;
        UiTheme.TextScaleChanged += OnTextScale;
    }

    public override void _ExitTree()
    {
        UiTheme.TextScaleChanged -= OnTextScale;
    }

    /// <summary>
    /// The text-size setting changed: every label of the HUD takes its new size (UiTheme.Rescale), the parts that are
    /// placed by hand are laid out again from the new font metrics, and the cached texts are pushed afresh.
    /// The drawn layers (numbers, nameplates, loot labels, notices, boss frame) read UiTheme.Fs each frame.
    /// </summary>
    private void OnTextScale(float oldScale)
    {
        UiTheme.Rescale(this, oldScale);
        try { Relayout(); } catch (Exception e) { Log.Error("hud", "layout after a text-scale change failed", e); }
        _trackerHash = 0; _areaShown = null; _infoShown = null; _objShown = null; _skillPts = -1; _paragonPts = -1;
        _slow = 0f;
        Log.Info("hud", $"HUD laid out for text scale ×{UiTheme.TextScale:0.00}");
    }

    /// <summary>
    /// Everything in the HUD that is positioned by hand and depends on text metrics: the bottom cluster (the bar lays
    /// out its plate, cells and key caps), the buff row above the plate, the reminders above the buffs, the corner
    /// column, and where toasts and subtitles may start.
    /// </summary>
    private void Relayout()
    {
        // top right: the column is CornerWidth design px wide (scaled), right edge 18 px from the screen edge
        float cw = UiTheme.Px(CornerWidth);
        _corner.CustomMinimumSize = new Vector2(cw, 0f);
        _corner.OffsetLeft = -(cw + 18f); _corner.OffsetRight = -18f; _corner.OffsetTop = 16f; _corner.OffsetBottom = 16f;
        _tracker.CustomMinimumSize = new Vector2(cw, 0f);

        // bottom centre
        _bar.Layout();
        float plateTop = HudBar.H + 8f - _bar.PlateTop;          // distance from the bottom of the screen to the plate's top edge
        float row = HudBuffs.RowHeight;
        _buffs.OffsetBottom = -(plateTop + 8f);
        _buffs.OffsetTop = _buffs.OffsetBottom - row;
        float remindBottom = plateTop + 8f + row + 8f;
        _remind.OffsetLeft = -480f; _remind.OffsetRight = 480f;
        _remind.OffsetBottom = -remindBottom;
        _remind.OffsetTop = -remindBottom - UiTheme.Px(44f);

        // toasts run down the upper left, starting a little below the band of the boss frame; subtitles stand above
        // the reminders
        _bossBand = HudBossBar.FullHeight;
        _notices.ToastTop = 22f + _bossBand + 10f;
        _notices.ToastLeft = 36f;
        _notices.SubtitleBottom = remindBottom + UiTheme.Px(44f) + 22f;
        _notices.Remeasure();
    }

    private static Button ReminderButton(string panel)
    {
        var b = UiTheme.Button("", () => Game.I?.Ui?.Open(panel), UiTheme.FsRow);
        b.FocusMode = FocusModeEnum.None;
        b.Visible = false;
        UiTheme.MinSize(b, 0f, 44f);
        if (UiTheme.Art)
        {
            // an ember-lit plate: points are waiting to be spent
            b.AddThemeStyleboxOverride("normal", UiTheme.ArtBox("tab_on", 18f, 6f));
            b.AddThemeStyleboxOverride("hover", UiTheme.ArtBox("btn_hover", 18f, 6f));
            b.AddThemeStyleboxOverride("pressed", UiTheme.ArtBox("btn_pressed", 18f, 6f));
        }
        else
        {
            var box = new StyleBoxFlat { BgColor = new Color(0.14f, 0.1f, 0.05f, 0.92f), BorderColor = UiTheme.Gold, AntiAliasing = true };
            box.SetBorderWidthAll(1);
            box.SetCornerRadiusAll(3);
            box.ContentMarginLeft = 16f; box.ContentMarginRight = 16f; box.ContentMarginTop = 4f; box.ContentMarginBottom = 4f;
            var hot = (StyleBoxFlat)box.Duplicate();
            hot.BgColor = new Color(0.24f, 0.17f, 0.07f, 0.96f);
            b.AddThemeStyleboxOverride("normal", box);
            b.AddThemeStyleboxOverride("hover", hot);
            b.AddThemeStyleboxOverride("pressed", hot);
        }
        b.AddThemeColorOverride("font_color", UiTheme.Gold.Lightened(0.2f));
        return b;
    }

    // ───────────────────────── public API ─────────────────────────

    /// <summary>A short red line in the middle of the screen: something the player tried cannot be done.</summary>
    public void Warn(string text)
    {
        _notices.Warn(text);
    }

    /// <summary>A line that floats in the toast lane (upper left) for a few seconds; repeats of the same text are counted.</summary>
    public void Toast(string text, Color c)
    {
        if (Lean) return;                       // 展示模式: no toast lane
        _notices.AddToast(text, c);
    }

    public void AreaTitle(string name, string subtitle)
    {
        Log.Debug("hud", $"area title: {name} / {subtitle}");
        _notices.AreaTitle(name, subtitle);
    }

    public void Subtitle(string speaker, string text, float seconds)
    {
        _notices.Subtitle(speaker, text, seconds);
    }

    /// <summary>Full-screen tint that fades out. The colour's alpha is the peak strength; an opaque colour is softened.</summary>
    public void Flash(Color c, float seconds)
    {
        _flashCol = c;
        _flashPeak = c.A >= 0.99f ? 0.5f : c.A;
        _flashLife = Mathf.Max(0.05f, seconds);
        _flashT = 0f;
        _flash.Color = new Color(c.R, c.G, c.B, _flashPeak);
        _flash.Visible = _flashPeak > 0.003f;
    }

    // ───────────────────────── input ─────────────────────────

    public override void _Input(InputEvent e)
    {
        // a click on a loot label or prompt walks the hero there and uses it
        if (e is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;
        var obj = _world.PointerObj;
        if (obj == null) return;
        try
        {
            var g = Game.I;
            var p = g?.Player;
            if (p == null || !IsInstanceValid(p) || p.Dead || g.Paused || !IsInstanceValid(obj) || !obj.CanUse) return;
            if (g.Ui != null && g.Ui.AnyOpen) return;
            if (GetViewport().GuiGetHoveredControl() != null) return;
            p.GoUse(obj);
            GetViewport().SetInputAsHandled();
        }
        catch (Exception ex) { UiSafe.Report("label click", ex); }
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _clock += dt;
        Game g = Game.I;
        Player p = null; Hero hero = null; Level lv = null; Ui ui = null;
        bool play = false;
        try
        {
            if (g != null)
            {
                ui = g.Ui;
                p = g.Player; lv = g.Level;
                if (p != null && (!IsInstanceValid(p) || !p.IsInsideTree() || p.Hero == null)) p = null;
                if (lv != null && !IsInstanceValid(lv)) lv = null;
                hero = p?.Hero;
                play = g.State == Game.GameState.Play && p != null && hero != null;
            }
        }
        catch (Exception e) { UiSafe.Report("hud state", e); play = false; }

        TickFlash(dt);
        try { _notices.Tick(dt, g != null && g.State == Game.GameState.Play); }
        catch (Exception e) { UiSafe.Report("hud notices", e); }

        bool covered = false, panelOpen = false, deathOpen = false;
        // a conversation owns the lower third of the screen: the bar and the corner step aside for it as well
        try { if (ui != null) { panelOpen = ui.AnyOpen; deathOpen = ui.IsOpen("death"); covered = ui.FullscreenOpen || deathOpen || ui.IsOpen("dialogue"); } }
        catch (Exception e) { UiSafe.Report("hud ui state", e); }
        // nothing of the HUD may glow through the death veil: notices (toasts, title card, warnings) hide while it is up
        // (their timers keep running underneath) and the red flash of the fatal blow ends
        if (_notices.Visible == deathOpen) _notices.Visible = !deathOpen;
        if (deathOpen && _flashT < _flashLife) { _flashT = _flashLife; _flash.Visible = false; }

        bool chrome = play && !covered;
        _notices.BossBottom = chrome && _boss.Visible ? 22f + _bossBand : 0f;       // an area title card stands below it
        if (_chrome.Visible != chrome) _chrome.Visible = chrome;
        if (_world.Visible != chrome) _world.Visible = chrome;
        if (!chrome)
        {
            _world.Idle();
            SetVignette(0f);
            if (!play) { _hero = null; _bossPick = null; }
            return;
        }

        if (!ReferenceEquals(hero, _hero))
        {
            _hero = hero;
            _lastLevel = hero.D.Level; _lastParagon = hero.D.Paragon;
            _slow = 0f; _skillPts = -1; _paragonPts = -1; _trackerHash = 0; _areaShown = null; _infoShown = null; _objShown = null;
        }

        try { _bar.Tick(p, dt); } catch (Exception e) { UiSafe.Report("hud action bar", e); }
        try { _buffs.Tick(p, dt); } catch (Exception e) { UiSafe.Report("hud buffs", e); }
        try { _map.Tick(lv, p, dt); } catch (Exception e) { UiSafe.Report("hud minimap", e); }

        try
        {
            _bossT -= dt;
            if (_bossT <= 0f) { _bossT = 0.2f; _bossPick = FindBoss(lv, p); }
            if (_bossPick != null && (!IsInstanceValid(_bossPick) || _bossPick.Dead)) _bossPick = null;
            Monster hover = _bossPick == null ? (p.Hover ?? p.AttackTarget) as Monster : null;
            if (hover != null && (!IsInstanceValid(hover) || hover.Dead || hover.Faction != Faction.Monster)) hover = null;
            _boss.Tick(_bossPick, hover, dt);
        }
        catch (Exception e) { UiSafe.Report("hud boss bar", e); }

        try
        {
            bool interactive = !panelOpen && !p.Dead && !g.Paused && GetViewport().GuiGetHoveredControl() == null;
            bool allLoot = ui != null ? ui.ShowAllLoot : Input.IsKeyPressed(Key.Alt);
            // loot labels and prompts yield to the HUD regions (minimap / tracker column, bottom cluster, boss frame).
            // Without this call the world layer has no keep-out rectangles and draws labels anywhere.
            if (LabelsYieldToHud) KeepOutHud();
            _world.Tick(p, dt, interactive, allLoot, _boss.Shown);
        }
        catch (Exception e) { UiSafe.Report("hud world layer", e); }

        _slow -= dt;
        if (_slow <= 0f)
        {
            _slow = 0.25f;
            try { PollSlow(g, p, hero, lv); } catch (Exception e) { UiSafe.Report("hud corner", e); }
        }
        if (Lean != _leanShown) { _leanShown = Lean; _bar.Visible = !Lean; _buffs.Visible = !Lean; _corner.Visible = !Lean; }
        if (Lean) { if (_buffs.Visible) _buffs.Visible = false; if (_bar.Visible) _bar.Visible = false; if (_corner.Visible) _corner.Visible = false; }
        if (Lean && _remind.Visible) _remind.Visible = false;
        if (_remind.Visible)
        {
            float a = 0.8f + 0.2f * Mathf.Sin(_clock * 3.2f);
            _remind.Modulate = new Color(1f, 1f, 1f, a);
        }

        try { Milestones(hero); } catch (Exception e) { UiSafe.Report("hud milestones", e); }

        // low life makes the edges of the screen throb; a heavy blow flashes them once
        float max = p.MaxLife;
        float life = max > 0f ? p.Life / max : 1f;
        float low = !p.Dead && life < 0.35f ? Mathf.Clamp((0.35f - life) / 0.3f, 0f, 1f) : 0f;
        _lowPhase += dt * (3f + 4f * low);
        float hit = _world.TakeHeroHit();
        if (hit > 0.03f) _hitPulse = Mathf.Max(_hitPulse, Mathf.Clamp(hit * 3.5f, 0.2f, 0.75f));
        _hitPulse = Mathf.Max(0f, _hitPulse - dt * 1.8f);
        // [gfx 0.3] toned down: the shader keeps the tint in the outer band of the frame and caps it at 0.55
        float strength = low * (0.36f + 0.12f * Mathf.Sin(_lowPhase)) + _hitPulse * 0.6f;
        if (p.Dead) strength = Mathf.Max(strength, 0.5f);
        SetVignette(Mathf.Min(0.55f, strength));
    }

    /// <summary>
    /// The HUD regions loot labels and prompts yield to, handed to the world layer every frame: the minimap / area /
    /// tracker / objective column, the bottom cluster (globes, action bar, buffs, reminders) and the boss or target frame.
    /// </summary>
    /// <summary>[fixes 0.4.0] On: seen working in a staged run (Y:\shots\fixes\labels_*.png).</summary>
    private static readonly bool LabelsYieldToHud = true;

    private void KeepOutHud()
    {
        _world.ClearKeepOut();
        if (!_chrome.Visible) return;           // HUD hidden: nothing to yield to
        Vector2 o = _world.GetGlobalPosition();
        // the minimap itself (the column is wider than the map), then the column's text block under it: area name and
        // info, quest tracker, objective and its bar. Lean mode hides the column and the bottom cluster.
        if (_corner.Visible)
        {
            if (_map.Visible) _world.KeepOut(Shifted(_map.GetGlobalRect(), o));
            Rect2 col = _corner.GetGlobalRect();
            float textTop = _areaName.GetGlobalRect().Position.Y;
            _world.KeepOut(Shifted(new Rect2(col.Position.X, textTop, col.Size.X, col.End.Y - textTop), o));
        }
        if (_bar.Visible) _world.KeepOut(Shifted(_bar.GetGlobalRect(), o));
        if (_buffs.Visible) _world.KeepOut(Shifted(_buffs.GetGlobalRect(), o));
        if (_skillBtn.IsVisibleInTree()) _world.KeepOut(Shifted(_skillBtn.GetGlobalRect(), o));
        if (_paragonBtn.IsVisibleInTree()) _world.KeepOut(Shifted(_paragonBtn.GetGlobalRect(), o));
        if (_boss.Visible)
        {
            // the frame's control keeps its nominal height; the name, bar and stagger meter reach down to _bossBand
            Rect2 b = _boss.GetGlobalRect();
            _world.KeepOut(Shifted(new Rect2(b.Position, b.Size.X, Mathf.Max(b.Size.Y, _bossBand)), o));
        }
    }

    private static Rect2 Shifted(Rect2 r, Vector2 by) => new(r.Position - by, r.Size);

    private void TickFlash(float dt)
    {
        if (_flashT >= _flashLife) return;
        _flashT += dt;
        float k = Mathf.Clamp(1f - _flashT / _flashLife, 0f, 1f);
        float a = _flashPeak * k * k;
        _flash.Color = new Color(_flashCol.R, _flashCol.G, _flashCol.B, a);
        bool vis = a > 0.003f && _flashT < _flashLife;
        if (_flash.Visible != vis) _flash.Visible = vis;
    }

    private void SetVignette(float strength)
    {
        if (Mathf.Abs(strength - _vig) < 0.008f && !(strength <= 0f && _vig > 0f)) return;
        _vig = strength;
        bool vis = strength > 0.008f;
        if (_vignette.Visible != vis) _vignette.Visible = vis;
        if (vis) _vigMat.SetShaderParameter(PStrength, strength);
    }

    /// <summary>The boss that deserves the big bar: the level's own boss once the fight is on, else any roused boss nearby.</summary>
    private static Monster FindBoss(Level lv, Player p)
    {
        Monster b = lv?.Boss;
        if (b != null && IsInstanceValid(b) && !b.Dead && (b.Aggro || b.Life < b.MaxLife)) return b;
        Monster best = null;
        Vector2 hp = p.Pos2;
        var mons = World.Monsters;
        for (int i = 0; i < mons.Count; i++)
        {
            var m = mons[i];
            if (m == null || m.Dead || !m.IsBoss || !m.Aggro || !IsInstanceValid(m)) continue;
            if (m.Pos2.DistanceSquaredTo(hp) > 46f * 46f) continue;
            if (best == null || (m.Tier == MonTier.Boss && best.Tier != MonTier.Boss)) best = m;
        }
        return best;
    }

    // ───────────────────────── slow polls (4 Hz) ─────────────────────────

    private void PollSlow(Game g, Player p, Hero hero, Level lv)
    {
        // area name and what kind of place it is
        string areaId = lv?.AreaId ?? hero.D.Area ?? "";
        string area = ZoneDb.AreaName(areaId);
        if (area != _areaShown) { _areaShown = area; _areaName.Text = area; }
        string info;
        if (lv == null) info = "";
        else if (lv.IsTown) info = ZoneDb.TownSubtitle;
        else
        {
            int tier = Math.Clamp(g.WorldTier, 0, ZoneDb.TierNames.Length - 1);
            info = lv.IsRift ? $"第 {lv.RiftTier} 層　怪物等級 {lv.MonsterLevel}" : $"怪物等級 {lv.MonsterLevel}　{ZoneDb.TierNames[tier]}";
        }
        if (info != _infoShown) { _infoShown = info; _areaInfo.Text = info; _areaInfo.Visible = info.Length > 0; }

        // quest tracker
        IReadOnlyList<string> lines = null;
        try { lines = Quests.Tracker(); } catch (Exception e) { UiSafe.Report("quest tracker", e); }
        int hash = 17;
        if (lines != null) for (int i = 0; i < lines.Count; i++) hash = unchecked(hash * 31 + (lines[i]?.GetHashCode() ?? 0));
        if (hash != _trackerHash)
        {
            _trackerHash = hash;
            _sb.Clear();
            if (lines != null && lines.Count > 0)
            {
                string gold = UiTheme.Hex(UiTheme.Gold), text = UiTheme.Hex(UiTheme.Text), dim = UiTheme.Hex(new Color(0.78f, 0.75f, 0.68f));
                _sb.Append("[right]");
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    if (_sb.Length > 7) _sb.Append('\n');
                    string col = line.StartsWith('◆') ? gold : line.StartsWith('◇') ? text : dim;
                    _sb.Append("[color=#").Append(col).Append(']').Append(line.Replace("[", "[lb]")).Append("[/color]");
                }
                _sb.Append("[/right]");
            }
            string bb = _sb.Length > 15 ? _sb.ToString() : "";
            _tracker.Text = bb;
            _tracker.Visible = bb.Length > 0;
        }

        // dungeon objective / rift progress
        string obj = "";
        float prog = -1f;
        bool done = false, next = false;
        if (lv != null)
        {
            obj = lv.ObjectiveText ?? "";
            // the level's phase tells a met goal with the boss still standing (Boss: the line names that next step and
            // must not wear the check mark) from a finished place (Cleared); town and overworld are None
            var phase = lv.Phase;
            done = phase == Level.DungeonPhase.Cleared;
            next = phase == Level.DungeonPhase.Boss;
            if (lv.IsRift)
            {
                int sec = Mathf.Max(0, Mathf.CeilToInt(lv.RiftTimeLeft));
                string time = $"{sec / 60}:{sec % 60:00}";
                obj = obj.Length > 0 ? $"{obj}　{time}" : $"裂隙將於 {time} 後閉合";
                prog = Mathf.Clamp(lv.RiftProgress, 0f, 1f);
            }
            else if (obj.Length > 0 && !done && !next) prog = Mathf.Clamp(lv.ObjectiveProgress, 0f, 1f);
            if (done && obj.Length > 0) obj = "✓ " + obj;
            else if (next && obj.Length > 0) obj = "◆ " + obj;
        }
        if (obj != _objShown)
        {
            _objShown = obj;
            _objective.Text = obj;
            _objective.Visible = obj.Length > 0;
            _objective.AddThemeColorOverride("font_color", done ? Palette.Good : next ? UiTheme.Gold : UiTheme.Text);
        }
        bool showBar = prog >= 0f && obj.Length > 0;
        if (_objBar.Visible != showBar) _objBar.Visible = showBar;
        if (showBar && Mathf.Abs(prog - _objProg) > 0.002f) { _objProg = prog; _objBar.Value = prog; }

        // unspent points
        int sp = Math.Max(0, hero.SkillPoints);
        if (sp != _skillPts)
        {
            _skillPts = sp;
            _skillBtn.Visible = sp > 0;
            if (sp > 0) _skillBtn.Text = $"尚有 {sp} 點技能點　〔K〕";
        }
        int pp = Math.Max(0, ParagonFree(hero));
        if (pp != _paragonPts)
        {
            _paragonPts = pp;
            _paragonBtn.Visible = pp > 0;
            if (pp > 0) _paragonBtn.Text = $"尚有 {pp} 點燼紋點　〔P〕";
        }
        bool any = sp > 0 || pp > 0;
        if (_remind.Visible != any) _remind.Visible = any;
    }

    /// <summary>
    /// Unspent paragon points. The paragon module owns the bookkeeping (Hero.ParagonPoints); it is looked up by name
    /// so this file does not depend on that module's exact surface, with the contract's formula as the fallback.
    /// </summary>
    private static int ParagonFree(Hero h)
    {
        if (_paragonFree == null)
        {
            Func<Hero, int> f = null;
            try
            {
                foreach (string name in new[] { "ParagonPoints", "ParagonPointsLeft", "ParagonAvailable", "ParagonUnspent", "ParagonFree" })
                {
                    PropertyInfo prop = typeof(Hero).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null || prop.PropertyType != typeof(int) || prop.GetMethod == null) continue;
                    f = (Func<Hero, int>)Delegate.CreateDelegate(typeof(Func<Hero, int>), prop.GetMethod);
                    Log.Info("hud", $"paragon points read from Hero.{name}");
                    break;
                }
            }
            catch (Exception e) { Log.Warn("hud", "paragon point lookup failed: " + e.Message); f = null; }
            if (f == null) Log.Warn("hud", "Hero exposes no paragon point count; estimating from the save data");
            _paragonFree = f ?? (hero => hero.D.Level >= Balance.LevelCap
                ? Math.Max(0, hero.D.Paragon + hero.D.BonusParagonPts - (hero.D.Talents?.Count ?? 0)) : 0);
        }
        return _paragonFree(h);
    }

    /// <summary>Level and paragon gains get a banner; the plain toast the game sends for the same event steps aside.</summary>
    private void Milestones(Hero hero)
    {
        int lvl = hero.D.Level, par = hero.D.Paragon;
        if (lvl > _lastLevel)
        {
            string sub = lvl >= Balance.LevelCap ? $"等級 {lvl}　·　燼紋盤已為你開啟" : $"等級 {lvl}　·　獲得 {lvl - _lastLevel} 點技能點";
            _notices.DropToasts("等級提升");
            _notices.Banner("等級提升", sub, UiTheme.Gold);
            Log.Info("hud", $"level-up banner: {lvl}");
        }
        else if (par > _lastParagon)
        {
            _notices.DropToasts("燼紋等級提升");
            _notices.Banner("燼紋昇華", $"燼紋 {par}　·　獲得 {par - _lastParagon} 點燼紋點", Palette.Ember);
            Log.Info("hud", $"paragon banner: {par}");
        }
        _lastLevel = lvl; _lastParagon = par;
    }
}
