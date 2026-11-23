using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// AI 代打: in a normal game the TestBot logic takes over the hero and plays the REAL save along the main quest, until
/// the player takes control back. On: F8, or 「AI 代打：開啟」 in the pause menu. Off: F8 again, the pause-menu button, or
/// any deliberate play input — a mouse click in the world (not on the HUD or a panel), a movement / skill / potion /
/// dodge / town-portal key (W A S D, 1-4, Q, Space, T). Moving the mouse, the wheel (zoom), Alt (loot labels) and the
/// panel keys (I C K P M Tab J Esc) do not end it: a panel the player opens pauses the game as usual and the pilot waits.
///
/// Rules of play: no god mode, no cheats, no teleports. It travels only the ways a player has (walking to an entrance or
/// exit, the town gate / waypoint travel map, the town portal T when out of combat, the portal back), sells and salvages
/// only in town, spends free skill points without moving what the player put on the bar, and the game autosaves as
/// usual. When it keeps dying (after two deaths without progress it first gains levels in the open world; the fourth hands back), is stuck with nowhere legal to go, or the main quest is finished,
/// it hands control back with a notice. Hardcore characters are refused (one AI mistake would end them).
/// Test switch: --pilot-at=&lt;s&gt; turns it on s seconds after the first level is on screen.
/// </summary>
public partial class AiPilot : Node
{
    public static AiPilot I { get; private set; }
    public static bool Active => I != null && IsInstanceValid(I) && I._bot != null;
    public const Key HotKey = Key.F8;

    private TestBot _bot;
    private Hero _hero;
    private double _since, _clock, _travelAt = -10, _useAt = -10, _portalAt = -10, _bagTripAt = -100, _playerPanelKeyAt = -10, _pilotAt = -1, _playSince = -1;
    private bool _wasOpen, _playerPanels;
    private readonly List<double> _deaths = new(), _stucks = new();
    private int _equipped;
    private Control _badge;
    private Label _badgeText;

    public override void _Ready()
    {
        I = this;
        World.Event += OnWorldEvent;
        string at = Game.I?.ArgValue("pilot-at");
        if (!string.IsNullOrEmpty(at) && double.TryParse(at, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double s)) _pilotAt = s;
        ReadTestSwitch();                                      // --pilot-test (AiPilotPlan.cs)
    }

    public override void _ExitTree() { if (I == this) I = null; World.Event -= OnWorldEvent; }

    /// <summary>Progress (a level, a boss, a stronghold, a dungeon, a waypoint) wipes the death count: dying while still getting on is not a stall.</summary>
    private void OnWorldEvent(string evt, string arg)
    {
        TestOnEvent(evt, arg); if (evt == "area") TestOnArea();       // test harness (AiPilotPlan.cs)
        if (_bot == null || (_deaths.Count == 0 && _stucks.Count == 0)) return;
        if (evt is "levelup" or "boss" or "miniboss" or "stronghold" or "dungeon" or "waypoint")
        {
            Log.Info("pilot", $"progress ({evt} {arg}): the death count ({_deaths.Count}) and the stuck count ({_stucks.Count}) start over");
            _deaths.Clear(); _stucks.Clear();
        }
    }

    // ───────────────────────── on / off ─────────────────────────

    /// <summary>Pause menu and hotkey: turns the pilot on or off. Returns whether it is on afterwards.</summary>
    public static bool Toggle(string source)
    {
        var p = I;
        if (p == null || !IsInstanceValid(p)) return false;
        if (Active) { p.Stop(source, "已由你接管操作。"); return false; }
        return p.Start(source);
    }

    private bool Start(string source)
    {
        var g = Game.I;
        if (g == null || g.State != Game.GameState.Play || g.Hero == null || g.Player == null || !IsInstanceValid(g.Player)) return false;
        if (DemoMode.Active) return false;
        if (g.Hero.D.Hardcore)
        {
            Log.Info("pilot", $"refused ({source}): hardcore character");
            Snd.Ui("ui_error");
            g.Hud?.Warn("專家模式的角色無法使用 AI 代打");
            return false;
        }
        _hero = g.Hero;
        _since = _clock;
        _deaths.Clear(); _stucks.Clear(); _equipped = 0;
        _wasOpen = false; _playerPanels = false;
        _bot = new TestBot { Name = "AiPilotBot", Pilot = this };
        AddChild(_bot);
        ShowBadge(true);
        Log.Info("pilot", $"AI pilot ON ({source}): {Names.ClassKey(_hero.Class)} lv{_hero.D.Level} in {g.Level?.AreaId ?? "?"} slot={g.Slot} quest={SafeQuestArea()}");
        Log.Guard("hud", () => g.Hud?.Toast("AI 代打開啟：按 F8、點擊地面或按移動／技能鍵即可接管", Palette.Ember));
        Snd.Ui("ui_open");
        return true;
    }

    /// <summary>Ends the pilot. <paramref name="notice"/> (zh-TW) is shown to the player; null = silent.</summary>
    public void Stop(string reason, string notice)
    {
        if (_bot == null) return;
        var bot = _bot;
        _bot = null;
        Log.Info("pilot", $"AI pilot OFF ({reason}) after {_clock - _since:0}s: kills={bot.Kills} bosses={bot.Bosses} deaths={bot.Deaths} areas={bot.Areas} items={bot.ItemsFound} equipped={_equipped} level={_hero?.D.Level ?? 0}");
        Log.Guard("pilot", () => bot.Summary("pilot"));
        _lastReflex = bot.ReflexStats();
        if (IsInstanceValid(bot)) { RemoveChild(bot); bot.QueueFree(); }      // leaving the tree releases every key it held
        var g = Game.I;
        var p = g?.Player;
        if (p != null && IsInstanceValid(p) && !p.Dead) Log.Guard("pilot", p.Stop);      // the bot's last order ends; a click of the player's gives the next one
        if (p != null && IsInstanceValid(p)) p.ForceHold = -1;
        ShowBadge(false);
        _hero = null;
        if (notice != null && g?.Hud != null && g.State == Game.GameState.Play)
            Log.Guard("hud", () => g.Hud.Toast("AI 代打結束：" + notice, Palette.Ember));
    }

    // ───────────────────────── input ─────────────────────────

    public override void _Input(InputEvent e)
    {
        try { OnInput(e); }
        catch (Exception ex) { Log.Error("pilot", "input failed", ex); }
    }

    private void OnInput(InputEvent e)
    {
        if (e.Device == TestBot.BotDevice) return;          // the pilot's own keys
        var g = Game.I;
        if (g == null || DemoMode.Active) return;
        if (e is InputEventKey k && k.Pressed && !k.Echo)
        {
            Key key = k.Keycode != Key.None ? k.Keycode : k.PhysicalKeycode;
            if (key == HotKey)
            {
                if (g.State == Game.GameState.Play && g.Hero != null) { Toggle("F8"); GetViewport().SetInputAsHandled(); }
                return;
            }
            if (!Active) return;
            if (key is Key.I or Key.C or Key.K or Key.P or Key.M or Key.Tab or Key.J or Key.Escape) { _playerPanelKeyAt = _clock; return; }
            if (g.Paused || (g.Ui != null && g.Ui.AnyOpen)) return;
            if (key is Key.W or Key.A or Key.S or Key.D or Key.Space or Key.Q or Key.T or Key.Key1 or Key.Key2 or Key.Key3 or Key.Key4
                or Key.Up or Key.Down or Key.Left or Key.Right)
                Stop("player input", "已由你接管操作。");
        }
        else if (e is InputEventMouseButton mb && mb.Pressed && Active)
        {
            if (mb.ButtonIndex != MouseButton.Left && mb.ButtonIndex != MouseButton.Right) return;     // the wheel only zooms
            _playerPanelKeyAt = _clock;                                                               // a click on a HUD button opens the player's panel
            if (g.Paused || g.UiBlocksMouse) return;
            Stop("player input", "已由你接管操作。");
        }
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        try { Tick(delta); }
        catch (Exception e) { Log.Error("pilot", "tick failed", e); }
    }

    private void Tick(double delta)
    {
        _clock += delta;
        var g = Game.I;
        if (g == null) return;
        if (_pilotAt >= 0 && g.State == Game.GameState.Play && g.Hero != null)
        {
            if (_playSince < 0) _playSince = _clock;
            if (_clock - _playSince >= _pilotAt) { _pilotAt = -1; Log.Info("pilot", "--pilot-at: turning the AI pilot on"); Start("--pilot-at"); }
        }
        TestTick(g);                                           // test harness (AiPilotPlan.cs)
        if (_bot == null) return;
        // the session ended (title screen, another save loaded): the pilot ends with it
        if (g.Hero == null || g.Hero != _hero || g.State == Game.GameState.Menu) { Stop("session ended", null); return; }
        if (g.State != Game.GameState.Play) return;

        // which panels are the player's: those opened right after a panel key or a click of theirs
        bool open = g.Ui != null && g.Ui.AnyOpen;
        if (open && !_wasOpen) _playerPanels = _clock - _playerPanelKeyAt < 1.0;
        if (!open) _playerPanels = false;
        _wasOpen = open;

        PulseBadge();
        if (g.Paused) return;

        if (_bot.CampaignOver) { Stop("campaign over", "主線旅程已全部完成。"); return; }
        BagTrip(g);
        PotionTrip(g);
        if (_walkOut && g.Level != null && IsInstanceValid(g.Level) && !g.Level.IsTown) _walkOut = false;
        if (MayTidyBag() && g.Hero.D.Bag.Count >= 24) TidyInTown(g.Hero);
    }

    /// <summary>True when the bot may close what is open: only panels its own walk opened (never the pause menu or the settings).</summary>
    internal bool MayClosePanels()
    {
        var ui = Game.I?.Ui;
        if (ui == null || _playerPanels) return false;
        string top = ui.TopId;
        return top != "pause" && top != "settings";
    }

    /// <summary>The bag is sorted (sold / salvaged) in town only, where the merchant and the smith are.</summary>
    internal bool MayTidyBag()
    {
        var lv = Game.I?.Level;
        return lv != null && IsInstanceValid(lv) && lv.IsTown;
    }

    /// <summary>A bag nearly full in the field: open the town portal when out of combat (the way back is the portal).</summary>
    private void BagTrip(Game g)
    {
        var hero = g.Hero; var p = g.Player; var lv = g.Level;
        if (hero == null || p == null || !IsInstanceValid(p) || lv == null || !IsInstanceValid(lv) || lv.IsTown) return;
        if (hero.D.Bag.Count < Hero.BagSize * 4 / 5 || _clock - _bagTripAt < 60.0) return;      // 80 % full: home at the next safe moment
        // the town portal now brings the hero back to the spot with the dungeon / rift as it was left (LevelState, SAVE-RESTORE
        // 2026-11-22); without it a dungeon would be rebuilt afresh (its objective lost), so then finish it first
        if ((lv.Dungeon != null || lv.IsRift) && !LevelState.Enabled) return;
        if (p.InCombat > 0f || p.Dead || lv.Boss != null && IsInstanceValid(lv.Boss) && !lv.Boss.Dead && lv.Boss.Aggro) return;
        _bagTripAt = _clock;
        Log.Info("pilot", $"bag nearly full ({hero.D.Bag.Count}/{Hero.BagSize}): town portal to sell");
        TownPortal(g, p, "bag full");
    }


    private double _potTripAt = -100;

    /// <summary>
    /// Out of potions in a dungeon or rift (hunter, final round: a lv6 lair horde after the last potion): at the next
    /// lull the town portal refills them; the way back finds the place as it was left (LevelState).
    /// </summary>
    private void PotionTrip(Game g)
    {
        var hero = g.Hero; var p = g.Player; var lv = g.Level;
        if (hero == null || p == null || !IsInstanceValid(p) || lv == null || !IsInstanceValid(lv) || lv.IsTown) return;
        if (lv.Dungeon == null && !lv.IsRift || !LevelState.Enabled) return;
        if (p.Potions > 0 || hero.D.PotionCap <= 0 || _clock - _potTripAt < 60.0) return;
        if (p.InCombat > 0f || p.Dead || lv.Boss != null && IsInstanceValid(lv.Boss) && !lv.Boss.Dead && lv.Boss.Aggro) return;
        _potTripAt = _clock;
        Log.Info("pilot", $"no potions left in {lv.AreaId}: town portal to refill");
        TownPortal(g, p, "potions out");
    }
    private static string SafeQuestArea()
    {
        try { return Quests.NextObjectiveArea() ?? "-"; }
        catch (Exception) { return "?"; }
    }

    private static float Worth(Item it) => it.Power * (1f + 0.2f * (int)it.Rarity) + it.Upgrade * 5f;       // as TestBot rates gear

    /// <summary>
    /// In town, with the bag filling up: the junk goes to the smith (salvage) — equipment below legendary that is not an
    /// upgrade for the hero. Legendary and unique pieces, gems and anything the hero would wear are kept.
    /// </summary>
    private void TidyInTown(Hero hero)
    {
        if (!Overflow(hero)) return;                    // stash, junk, found legendaries, or hand back (AiPilotPlan.cs)
        var bag = hero.D.Bag;
        int before = bag.Count, gone = 0;
        for (int i = bag.Count - 1; i >= 0; i--)
        {
            if (i >= bag.Count) continue;
            var it = bag[i];
            if (it == null || !it.IsEquip || it.Rarity >= Rarity.Legendary) continue;
            if (hero.IsProtected(it)) continue;            // locked (鎖定) or in a gear set: never salvaged
            if (PlayerValues(hero, it)) continue;          // locked / set pieces, legendaries and uniques: not junk (overflow handles a full stash)
            if (GearEval.IsUpgrade(hero, it)) continue;    // an upgrade by the shared evaluator (GearEval): the bot puts it on
            hero.Salvage(it);
            gone++;
        }
        if (gone > 0)
        {
            Log.Info("pilot", $"town: salvaged {gone} pieces of junk ({before} → {bag.Count} in the bag)");
            Log.Guard("hud", () => Game.I?.Hud?.Toast($"AI 代打在鎮上拆解了 {gone} 件無用裝備", Palette.Ember));
        }
    }

    internal void Equipped(Item it, Item old)
    {
        if (it == null) return;
        _equipped++;
        Log.Info("pilot", $"equips {it.Name} ({it.Rarity}, power {it.Power}) instead of {old?.Name ?? "nothing"}");
        Log.Guard("hud", () => Game.I?.Hud?.Toast($"AI 代打換上：{it.Name}", Palette.Rarity(it.Rarity)));
    }

    // ───────────────────────── troubles ─────────────────────────

    /// <summary>
    /// A death: the second one without progress in between (a level, a boss, a stronghold…) sends the pilot to gain levels in the open world first (four minutes, or
    /// two levels); the fourth hands control back — the player had better look at gear and skills.
    /// </summary>
    internal void OnDeath()
    {
        if (TestDeath()) return;                              // the boss test counts deaths and keeps trying
        if (WallReached()) return;                            // the same place keeps killing the hero: hand back (AiPilotPlan.cs)
        _deaths.Add(_clock);
        _deaths.RemoveAll(t => _clock - t > 900.0);
        Log.Info("pilot", $"hero died ({_deaths.Count} since the last progress)");
        if (_deaths.Count >= 4) { Stop("dying repeatedly", "英雄接連倒下，請親自調整裝備或技能後再交給 AI。"); return; }
        if (_deaths.Count >= 2 && _grindUntil < _clock)
        {
            var g = Game.I; var lv = g?.Level;
            _grindRegion = lv != null && IsInstanceValid(lv) ? (lv.Region ?? ZoneDb.RegionOf(lv.AreaId))?.Id : null;
            _grindRegion ??= ZoneDb.RegionOf(g?.Hero?.D.Area ?? "")?.Id ?? ZoneDb.Regions[0].Id;
            _grindUntil = _clock + 240.0;
            _grindLevel = g?.Hero?.D.Level ?? 1;
            ChooseRegroup(g);                                 // died twice in one place: level up somewhere else first (AiPilotPlan.cs)
            Log.Info("pilot", $"two deaths: grinding in {_grindRegion} for up to 4 minutes (level {_grindLevel})");
            Log.Guard("hud", () => g?.Hud?.Toast("AI 代打：先到荒野磨練一番，再回頭挑戰", Palette.Ember));
        }
    }

    private double _grindUntil = -1, _grindArrived = -1;
    private string _grindRegion;
    private int _grindLevel;

    /// <summary>True while the pilot gains levels in the open world instead of following the quest.</summary>
    internal bool Grinding
    {
        get
        {
            if (_grindUntil < 0) return false;
            var h = Game.I?.Hero;
            bool over = _clock > _grindUntil || (h != null && h.D.Level >= _grindLevel + 2);
            if (over) { Log.Info("pilot", $"grinding over at level {h?.D.Level}; back to the quest"); _grindUntil = -1; _grindArrived = -1; return false; }
            return true;
        }
    }

    /// <summary>Grinding: get to the region's overworld (true = an order was given); there the bot hunts and explores.</summary>
    internal bool GrindStep(Game g, Level lv)
    {
        if (lv.IsTown || lv.Dungeon != null || lv.AreaId != _grindRegion)
        {
            Travel(g, _grindRegion, lv.AreaId);
            _grindUntil = Math.Max(_grindUntil, _clock + 240.0);     // four minutes from the arrival in the open world
            return true;
        }
        if (_grindArrived < 0) _grindArrived = _clock;
        return false;
    }

    internal void OnStuck(string why)
    {
        var g = Game.I; var p = g?.Player; var lv = g?.Level;
        _stucks.Add(_clock);
        _stucks.RemoveAll(t => _clock - t > 600.0);
        if (p == null || lv == null || !IsInstanceValid(lv)) return;
        // what a player does, in turn: step out with the movement keys and roll; then the town portal and back out on foot
        // through the town gate (not the portal back, which returns to the same spot); then give the controls back
        if (_stucks.Count >= 3 || (lv.IsTown && _stucks.Count >= 2))
        {
            Stop("stuck: " + why, "找不到前進的路，請親自引路。");
            return;
        }
        if (_stucks.Count == 1 || lv.IsTown)
        {
            Log.Info("pilot", $"stuck ({why}): steps aside with the movement keys and rolls");
            _bot?.Wiggle();
            return;
        }
        if (p.InCombat > 0f) { Log.Info("pilot", $"stuck ({why}) but in combat: steps aside and keeps fighting"); _bot?.Wiggle(); return; }
        _walkOut = true;
        TownPortal(g, p, "stuck: " + why);
    }

    private bool _walkOut;         // after a stuck: leave town through the gate, not by the portal back to the same spot

    // ───────────────────────── travel ─────────────────────────

    private static bool CanEnter(string area)
    {
        try { return Quests.CanEnter(area); }
        catch (Exception e) { Log.Error("pilot", $"CanEnter({area}) failed", e); return false; }
    }

    private static Interactable Nearest(Vector2 me, Func<Interactable, bool> test)
    {
        Interactable best = null; float bd = float.MaxValue;
        var all = Interactable.All;
        for (int i = 0; i < all.Count; i++)
        {
            var it = all[i];
            if (it == null || !IsInstanceValid(it) || !it.CanUse || !it.IsInsideTree() || !test(it)) continue;
            float d = it.Pos2.DistanceSquaredTo(me);
            if (d < bd) { bd = d; best = it; }
        }
        return best;
    }

    /// <summary>The T key: home to town when out of combat (the game remembers the way back).</summary>
    private void TownPortal(Game g, Player p, string why)
    {
        if (_clock - _portalAt < 5.0 || g.State != Game.GameState.Play) return;
        var lv = g.Level;
        if (lv == null || !IsInstanceValid(lv) || lv.IsTown || p.InCombat > 0f || p.Dead) return;
        _portalAt = _clock;
        Log.Info("pilot", $"town portal from {lv.AreaId} ({why})");
        g.EnterArea(ZoneDb.Town);
    }

    private void Use(Player p, Interactable it, string what)
    {
        if (_clock - _useAt < 1.5) return;
        _useAt = _clock;
        Log.Debug("pilot", $"walks to {what} ({it.Kind} '{it.Label}')");
        p.GoUse(it);
    }

    /// <summary>
    /// TestBot's directed play wants to be in <paramref name="dest"/>: takes the next legal step there. From town: the return
    /// portal when it leads that way, else the town gate / waypoint travel map (any region the story opens). In the
    /// destination's overworld: its dungeon entrance. In one of its dungeons: the exit (or the portal after the boss).
    /// Anywhere else: the town portal, and on from town.
    /// </summary>
    internal void Travel(Game g, string dest, string from)
    {
        var p = g.Player; var lv = g.Level;
        if (p == null || !IsInstanceValid(p) || lv == null || !IsInstanceValid(lv) || g.State != Game.GameState.Play || p.Dead) return;
        string here = lv.AreaId ?? "";
        if (string.IsNullOrEmpty(dest) || dest == here) return;
        if (BeforeTravel(g, dest)) return;                     // underlevelled or short of potions for a boss (AiPilotPlan.cs)
        Vector2 me = p.Pos2;
        var destRegion = ZoneDb.Region(dest) ?? ZoneDb.RegionOf(dest);
        if (dest == ZoneDb.Town) { TownPortal(g, p, "quest leads to town"); return; }

        if (lv.IsTown)
        {
            if (destRegion == null) return;
            if (!_walkOut && g.ReturnArea != null && ZoneDb.RegionOf(g.ReturnArea)?.Id == destRegion.Id)
            {
                var portal = Nearest(me, it => it.Kind == "portal");
                if (portal != null) { Use(p, portal, "the portal back"); return; }
            }
            if (!CanEnter(destRegion.Id)) { Log.Debug("pilot", $"{destRegion.Id} is not open yet"); return; }
            var stone = Nearest(me, it => it.Kind == "gate" || it.Kind == "waypoint");
            bool lit = g.Hero.D.Waypoints != null && g.Hero.D.Waypoints.Contains(destRegion.Id);
            if (stone != null && stone.Pos2.DistanceTo(me) > stone.UseRange + 1.2f)
            {
                if (_clock - _useAt >= 1.2) { _useAt = _clock; p.SetMove(stone.Pos2, false); }
                return;
            }
            if (_clock - _travelAt < 4.0) return;
            _travelAt = _clock;
            // what clicking the region on the travel map does (MapUi.TravelRule): the 燼塔 when lit, else on foot from town
            Log.Info("pilot", $"travel map: town → {destRegion.Id} ({(lit ? "waypoint" : "on foot")}) for {dest}");
            g.EnterArea(destRegion.Id, lit ? "waypoint" : ZoneDb.Town);
            return;
        }

        if (destRegion != null && lv.Region != null && lv.Region.Id == destRegion.Id)
        {
            if (lv.Dungeon == null && ZoneDb.Dungeon(dest) != null)
            {
                var door = Nearest(me, it => it.Kind == "entrance" && it.Tag is DungeonDef d && d.Id == dest);
                if (door != null) { Use(p, door, "the entrance of " + dest); return; }
            }
            else if (lv.Dungeon != null && dest == destRegion.Id)
            {
                var exit = Nearest(me, it => it.Kind == "exit" && it.Tag as string == dest) ?? (lv.Cleared ? Nearest(me, it => it.Kind == "portal") : null);
                if (exit != null) { Use(p, exit, "the way out"); return; }
            }
        }
        TownPortal(g, p, $"on the way to {dest}");
    }

    // ───────────────────────── skill points ─────────────────────────

    /// <summary>
    /// Spends free points the way TestBot does (Game.AutoBuild: never unlearns, keeps the chosen enhancement branches),
    /// then puts the player's own bar back: every skill stays in the slot the player gave it; new skills fill empty slots.
    /// </summary>
    internal static void SpendPoints(Hero hero)
    {
        var before = (string[])hero.D.Bar.Clone();
        int pts = hero.SkillPoints;
        PreferUnstoppable(hero);                                // first claim on the points: crowd-control immunity (AiPilotPlan.cs)
        Game.AutoBuild(hero);
        PreferUnstoppable(hero);                                // crowd-control immunity on the defensive skill (AiPilotPlan.cs)
        var bar = (string[])before.Clone();
        foreach (var id in hero.D.Bar)
        {
            if (id == null || Array.IndexOf(bar, id) >= 0) continue;
            int free = Array.IndexOf(bar, null);
            if (free >= 0) bar[free] = id;
        }
        hero.D.Bar = bar;
        hero.Recalc();
        Log.Info("pilot", $"spent {pts - hero.SkillPoints} skill points; bar kept as the player set it: [{string.Join(", ", bar.Select(b => b ?? "-"))}]");
        Log.Guard("hud", () => Game.I?.Hud?.Toast("AI 代打分配了技能點數", Palette.Ember));
    }

    // ───────────────────────── badge ─────────────────────────

    private void ShowBadge(bool on)
    {
        if (on && (_badge == null || !IsInstanceValid(_badge)))
        {
            var hud = Game.I?.Hud;
            if (hud == null) return;
            var box = new PanelContainer { Name = "AiPilotBadge", MouseFilter = Control.MouseFilterEnum.Ignore };
            box.AddThemeStyleboxOverride("panel", UiTheme.Box(new Color(0.05f, 0.035f, 0.03f, 0.82f), new Color(1f, 0.5f, 0.15f, 0.85f), 2, 4));
            _badgeText = UiTheme.Outlined(UiTheme.Label("AI 代打中 — 按 F8、點擊地面或移動鍵接管", 20, new Color(1f, 0.82f, 0.55f)), 4);
            var pad = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            pad.AddThemeConstantOverride("margin_left", 14); pad.AddThemeConstantOverride("margin_right", 14);
            pad.AddThemeConstantOverride("margin_top", 6); pad.AddThemeConstantOverride("margin_bottom", 6);
            pad.AddChild(_badgeText);
            box.AddChild(pad);
            // top-left, under the toast lane's first line: clear of the globes, the bar and the minimap column
            box.AnchorLeft = 0f; box.AnchorRight = 0f; box.AnchorTop = 0f; box.AnchorBottom = 0f;
            box.OffsetLeft = 24f; box.OffsetTop = 18f;
            hud.AddChild(box);
            _badge = box;
        }
        if (_badge != null && IsInstanceValid(_badge)) _badge.Visible = on;
    }

    private void PulseBadge()
    {
        if (_badge == null || !IsInstanceValid(_badge) || !_badge.Visible) return;
        float a = 0.78f + 0.22f * Mathf.Sin((float)_clock * 2.4f);
        _badge.Modulate = new Color(1f, 1f, 1f, a);
    }
}
