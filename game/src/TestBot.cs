using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// A hero that plays by itself for soak tests (--bot). It drives the real Player through its public orders
/// (SetMove / TryCast / GoUse) and, for the things only the keyboard can do (potion, evade, holding a channel),
/// through synthetic key events that travel the normal input path. It fights the nearest monster within 30 m with
/// every skill that is ready, drinks and rolls away when hurt, picks up loot, opens chests, uses shrines and other
/// objects, equips upgrades and salvages junk, spends skill points, explores what it has not been near, visits the
/// level's markers and its boss, then leaves through an exit (or asks the game to travel) and carries on.
/// Every ten seconds it writes one summary line to the log (category "bot").
/// </summary>
public partial class TestBot : Node
{
    public int Kills, Elites, Bosses, ItemsFound, Deaths, Areas, Casts, Unstuck;

    /// <summary>
    /// AI 代打 (AiPilot.cs), set before the bot enters the tree: it plays the player's real save along the main quest —
    /// no teleports, travel only the ways a player has (on foot, gates, waypoints, the town portal), panels the player
    /// opened are left alone, and when it is stuck or keeps dying it hands control back.
    /// </summary>
    public AiPilot Pilot;
    /// <summary>展示模式 (DemoMode.cs), set before the bot enters the tree: fights, loots and cycles skills; never leaves the area.</summary>
    public bool Demo;
    /// <summary>InputEvent.Device of the bot's synthetic key events, so the AI pilot and the demo can tell them from the player's.</summary>
    public const int BotDevice = 0x5B07;
    /// <summary>True once the main quest has nothing left (directed play has finished the campaign).</summary>
    public bool CampaignOver => _campaignOver;

    private int _errors;
    private double _clock, _summaryT, _lastThink;
    private float _thinkT, _deadT, _uiT;
    private bool _wasDead, _moving;
    private string _area = "", _doing = "idle";
    private double _areaSince;

    // combat
    private static readonly int[] SlotOrder = { 5, 4, 3, 2, 1, 0 };
    private static readonly Key[] SlotKeys = { Key.None, Key.None, Key.Key1, Key.Key2, Key.Key3, Key.Key4 };
    private ulong _targetId;
    private double _targetSince;
    private float _targetLife, _targetBest;
    private double _targetFirst, _longLog, _nudgeUntil;
    private readonly Dictionary<string, (double at, Vector2 pos)> _zoneCast = new();
    private readonly Dictionary<ulong, double> _ignore = new();        // monster → ignored until
    private double _lastPotion = -10, _lastEvade = -10;
    private int _chanSlot = -1;
    private double _chanSince;
    private readonly List<(Key key, float left)> _held = new();

    // objects
    private readonly Dictionary<ulong, double> _used = new();          // interactable → not before
    private ulong _useId, _arrivedId;
    private double _useSince, _useIssued, _arrivedAt;
    private double _bagT, _buildT;
    private int _lastBag = -1, _idlePoints = -1;

    // movement
    private Vector2 _moveTo, _stuckPos;
    private double _moveAt = -10, _stuckSince;
    private int _nudges;

    // exploration
    private static readonly int[] Dx = { 1, -1, 0, 0 }, Dy = { 0, 0, 1, -1 };
    private bool[] _explored;
    private int[] _bfsMark, _bfsQueue;
    private int _bfsStamp;
    private Vector2 _goal;
    private string _goalKind;
    private MapMarker _goalMarker;
    private double _goalSince;
    private int _goalFails;
    private bool _bossTried;
    private readonly HashSet<MapMarker> _visited = new();

    // leaving
    private double _leaveSince = -1, _lastDirect = -10, _doorSince, _doorIssued;
    private ulong _doorId;
    private readonly HashSet<ulong> _travelTried = new();
    private readonly Dictionary<string, int> _doorUses = new();        // area|kind|cell → times chosen, so the bot spreads over all ways out
    private readonly List<string> _options = new();

    // scripted suites and directed play (--suite, --focus, --campaign, --allskills)
    private TestSuite _suite;
    private bool _focus, _campaign, _allSkills, _campaignOver;
    private double _rotT = -100, _travelT = -100, _bossDeadAt = -1, _focusIssued = -10;
    private int _rot, _lastLevel = -1;
    private double _levelAt;
    private string _focusNote = "";
    // --tour=a,b,c: after the starting area, visit these in turn; each is left when its purpose is fulfilled (or its time is up)
    private List<string> _tour;
    private int _tourI;
    private bool _tourOver, _bossRush;      // --bossrush: skip the dungeon objective, open the door, fight the boss

    // statistics: per-skill casts / hits / damage, time-to-kill by tier, damage taken
    private readonly Dictionary<string, int> _castBy = new(), _hitBy = new();
    private readonly Dictionary<string, double> _dmgBy = new();
    private readonly Dictionary<ulong, double> _firstHit = new();
    private readonly double[] _ttkSum = new double[4];
    private readonly int[] _ttkN = new int[4];
    private static readonly string[] TtkNames = { "normal", "champion", "elite", "boss" };
    private double _dmgTaken, _statsSince = -1;
    private int _hitsTaken;

    public override void _Ready()
    {
        World.MonsterKilled += OnKill;
        World.Damage += OnDamage;
        var g = Game.I;
        if (g != null)
        {
            g.AreaEntered += OnArea;
            string suite = g.ArgValue("suite");
            if (!string.IsNullOrEmpty(suite)) _suite = new TestSuite(suite, this);
            _campaign = g.Arg("campaign");
            _focus = _campaign || g.Arg("focus");
            _allSkills = g.Arg("allskills");
            _bossRush = g.Arg("bossrush");
            if (_bossRush) _focus = true;
            string tour = g.ArgValue("tour");
            if (!string.IsNullOrEmpty(tour))
            {
                _tour = new List<string>(tour.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                _tourI = -1;                       // the area the run starts in comes first
                _focus = true;
            }
            if (Pilot != null) { _campaign = true; _focus = true; _allSkills = false; _bossRush = false; _tour = null; }
            if (Pilot != null && Pilot.BossTest) { _campaign = false; _bossRush = g.Arg("bossrush"); }      // --pilot-test=boss: this area's boss only
            if (Demo) { _allSkills = true; _campaign = false; _focus = false; _bossRush = false; _tour = null; }
        }
        Log.Info("bot", _suite != null ? "TestBot ready: running a scripted suite"
            : $"TestBot ready: it fights, loots, explores and travels on its own (focus={_focus} campaign={_campaign} allskills={_allSkills})");
    }

    public override void _ExitTree()
    {
        World.MonsterKilled -= OnKill;
        World.Damage -= OnDamage;
        if (Game.I != null) Game.I.AreaEntered -= OnArea;
        _suite?.Abort();
        ReleaseAll();
    }

    private void OnKill(Monster m, Hit h)
    {
        if (m == null || m.Faction != Faction.Monster) return;
        Kills++;
        if (m.Brain != null) Bosses++;
        else if (m.IsElite) Elites++;
        int tier = m.Brain != null ? 3 : m.MiniBoss || m.Tier == MonTier.Elite ? 2 : m.Tier == MonTier.Champion ? 1 : 0;
        ulong id = m.GetInstanceId();
        if (_firstHit.TryGetValue(id, out double t0))
        {
            _firstHit.Remove(id);
            double ttk = (Game.I?.Time ?? 0) - t0;
            _ttkSum[tier] += ttk; _ttkN[tier]++;
            if (tier == 3 || m.MiniBoss)
                Log.Info("bot", $"{(tier == 3 ? "boss" : "mini-boss")} {m.Def?.Id} lv{m.Level} (life {m.MaxLife:0}) killed {ttk:0.0} s after the first hit; hero lv{Game.I?.Hero?.D.Level} life {(Game.I?.Player != null ? Game.I.Player.LifeFrac * 100f : 0):0}% deaths={Deaths}");
        }
    }

    private void OnDamage(Actor a, Hit h, float amount)
    {
        if (amount <= 0f) return;
        if (a is Monster m && m.Faction == Faction.Monster)
        {
            if (h.Source == a) return;              // a bloated husk bursting itself is not the hero's damage
            string id = h.Skill?.Id ?? (h.Dot ? "(dot)" : h.Minion ? "(minion)" : "(other)");
            _hitBy[id] = _hitBy.GetValueOrDefault(id) + 1;
            _dmgBy[id] = _dmgBy.GetValueOrDefault(id) + amount;
            ulong mid = m.GetInstanceId();
            if (!_firstHit.ContainsKey(mid)) _firstHit[mid] = Game.I?.Time ?? 0;
        }
        else if (a is Player) { _dmgTaken += amount; _hitsTaken++; }
    }

    /// <summary>One combat decision against a given target: suites measure damage with the same rotation the bot plays.</summary>
    internal void FightStep(Monster t)
    {
        var g = Game.I; var p = g?.Player;
        if (p == null || !IsInstanceValid(p) || t == null || !IsInstanceValid(t) || t.Dead) return;
        _targetId = t.GetInstanceId();
        Fight(p, g.Hero, t, p.Pos2, World.Grid);
    }

    internal void FightStop() => ReleaseAll();

    /// <summary>AI pilot, stuck: what a player tries first — a few steps with the movement keys, then a dodge roll.</summary>
    internal void Wiggle()
    {
        Key[] dirs = { Key.W, Key.A, Key.S, Key.D };
        GetViewport()?.GuiReleaseFocus();
        Game.I?.Player?.Stop();
        Hold(dirs[Rng.R.Next(4)], 0.8f);
        Hold(dirs[Rng.R.Next(4)], 0.5f);
        Hold(Key.Space, 0.1f);
        _targetId = 0;
        AbandonGoal(World.Grid);
        _nudgeUntil = _clock + 1.2;
    }

    internal void ResetStats()
    {
        _castBy.Clear(); _hitBy.Clear(); _dmgBy.Clear(); _firstHit.Clear();
        Array.Clear(_ttkSum, 0, 4); Array.Clear(_ttkN, 0, 4);
        _dmgTaken = 0; _hitsTaken = 0; Kills = 0;
        _statsSince = Game.I?.Time ?? 0;
    }

    /// <summary>Per-skill casts / hits / damage, time-to-kill by monster tier and damage taken since the bot started.</summary>
    public void Stats(string tag = null)
    {
        var g = Game.I;
        var hero = g?.Hero;
        if (hero == null) return;
        var ids = new List<string>(_hitBy.Keys);
        foreach (var k in _castBy.Keys) if (!ids.Contains(k)) ids.Add(k);
        ids.Sort(StringComparer.Ordinal);
        var sb = new System.Text.StringBuilder();
        foreach (var id in ids)
            sb.Append($" {id}:c{_castBy.GetValueOrDefault(id)}/h{_hitBy.GetValueOrDefault(id)}/d{_dmgBy.GetValueOrDefault(id):0}");
        Log.Info("bot", $"{(tag != null ? tag + " " : "")}skills [{Names.ClassKey(hero.Class)} lv{hero.D.Level}] cast/hits/damage:{sb}");
        var known = SkillDb.Of(hero.Class).Where(s => hero.BaseRank(s.Id) > 0).Select(s => s.Id).ToList();
        var unused = known.Where(id => _castBy.GetValueOrDefault(id) == 0).ToList();
        var silent = known.Where(id => _castBy.GetValueOrDefault(id) > 0 && _hitBy.GetValueOrDefault(id) == 0 && (SkillDb.Get(id)?.Coef ?? 0) > 0 && SkillDb.Get(id).Kind != SkillKind.Buff).ToList();
        double mins = Math.Max(0.05, (g.Time - Math.Max(0, _statsSince)) / 60.0);
        var ttk = new System.Text.StringBuilder();
        for (int i = 0; i < 4; i++) ttk.Append($" {TtkNames[i]}={(_ttkN[i] > 0 ? (_ttkSum[i] / _ttkN[i]).ToString("0.0") : "-")}s(n={_ttkN[i]})");
        Log.Info("bot", $"{(tag != null ? tag + " " : "")}balance: ttk{ttk} | taken {_dmgTaken / mins:0}/min in {_hitsTaken} hits (life {hero.MaxLife:0}; {_dmgTaken / mins / Math.Max(1f, hero.MaxLife):0.00} life bars/min) god-saves={Player.GodSaves} | kills/min {Kills / mins:0.0} | never cast: [{string.Join(",", unused)}] cast but never hit: [{string.Join(",", silent)}]");
    }

    private void OnArea(string id)
    {
        Areas++;
        _area = id ?? "";
        _areaSince = _clock;
        _explored = null;
        _visited.Clear(); _used.Clear(); _ignore.Clear(); _travelTried.Clear();
        _goalKind = null; _goalMarker = null; _goalFails = 0; _bossTried = false;
        _leaveSince = -1; _doorId = 0; _useId = 0; _targetId = 0;
        _bossDeadAt = -1;
        _stuckSince = _clock; _nudges = 0; _moving = false;
        ReleaseAll();
        Log.Info("bot", $"entered {_area} (area {Areas})");
    }

    /// <summary>One line with everything a soak test wants to watch.</summary>
    public void Summary(string tag = null)
    {
        var g = Game.I;
        var hero = g?.Hero;
        var p = g?.Player;
        int life = p != null && IsInstanceValid(p) ? Mathf.RoundToInt(p.LifeFrac * 100f) : 0;
        Log.Info("bot", $"{(tag != null ? tag + " " : "")}summary t={_clock:0}s game={g?.Time ?? 0:0}s area={(_area.Length > 0 ? _area : "-")} doing={_doing} kills={Kills} elites={Elites} bosses={Bosses} " +
            $"level={hero?.D.Level ?? 0} life={life}% items={ItemsFound} gold={hero?.D.Gold ?? 0} deaths={Deaths} areas={Areas} casts={Casts} unstuck={Unstuck} " +
            $"monsters={World.Monsters.Count} errors={Log.ErrorCount} warnings={Log.WarnCount} botErrors={_errors}{(_focusNote.Length > 0 ? " focus=" + _focusNote : "")}");
        if (tag == "final" && hero != null)
            Log.Info("bot", $"final progress: waypoints=[{string.Join(",", hero.D.Waypoints)}] renown=[{string.Join(",", hero.D.Renown.Select(kv => kv.Key + ":" + kv.Value))}] " +
                $"quests done={hero.D.Quests.Count(kv => kv.Value == Quests.Done)}/{hero.D.Quests.Count} skillpts bonus={hero.D.BonusSkillPts} potion cap={hero.D.PotionCap} altars={hero.D.Flags.Count(f => f.StartsWith("altar_"))} " +
                $"codex={hero.D.Codex.Count} tier={hero.D.Tier}/{hero.D.MaxTier} riftBest={hero.D.RiftBest} paragon={hero.D.Paragon} playtime={hero.D.PlayTime:0}s");
        if (tag == "final" && _suite == null) Stats(tag);
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        long t0 = Prof.Now;
        try { Tick((float)delta); }
        catch (Exception e)
        {
            _errors++;
            if (_errors <= 25) Log.Error("bot", "bot tick failed", e);
        }
        Prof.Bot += Prof.Now - t0;
    }

    private void Tick(float dt)
    {
        var g = Game.I;
        if (g == null) return;
        // the bot keeps real time, so its summary and patience do not stretch during hit-stop
        float real = Engine.TimeScale > 0.01 ? dt / (float)Engine.TimeScale : dt;
        if (_suite != null)
        {
            _clock += real;
            _suite.Tick(dt, real);
            return;
        }
        // the bot thinks in game time, so a sped-up run (--speed) plays the same game, only faster
        _clock += dt;
        _summaryT += dt;
        if (_summaryT >= 10.0) { _summaryT = 0; Summary(); }
        TickKeys(dt);
        real = dt;

        if (g.State != Game.GameState.Play || g.Hero == null) { ReleaseAll(); return; }
        var p = g.Player;
        if (p == null || !IsInstanceValid(p) || !p.IsInsideTree()) return;

        if (p.Dead)
        {
            if (!_wasDead)
            {
                _wasDead = true; _deadT = 0; Deaths++;
                ReleaseAll();
                Log.Info("bot", $"died in {_area} (death {Deaths})");
            }
            _deadT += real;
            if (_deadT >= 3f)
            {
                _deadT = 0; Log.Info("bot", "respawning"); g.Respawn();
                Pilot?.OnDeath();
                if (Pilot != null) PilotRespawned();
            }
            return;
        }
        _wasDead = false;

        if (g.Ui != null && g.Ui.AnyOpen)
        {
            ReleaseAll();
            _uiT += real;
            if (Pilot != null)
            {
                // the player's own panels (and the pause menu) are left alone; what the bot's walk opened is read, then closed
                if (_uiT >= 1.5f && Pilot.MayClosePanels()) { _uiT = 0; g.Ui.CloseAll(); }
                return;
            }
            if (Demo) { g.Ui.CloseAll(); return; }
            if (_uiT >= 1.5f && g.AppFocused) { _uiT = 0; g.Ui.CloseAll(); }   // a player who Alt-Tabbed away is not here to close the pause menu       // dialogues, shops, the map: read for a moment, then move on
            return;
        }
        _uiT = 0;
        if (g.Paused) return;

        if (Pilot != null && PilotReflex(g, p, real)) return;      // AI pilot: dodging, potions (AiPilotCombat.cs)
        _thinkT -= real;
        if (_thinkT > 0) return;
        _thinkT = 0.12f;
        Think(g, p);
    }

    private void Think(Game g, Player p)
    {
        var hero = g.Hero;
        var level = g.Level != null && IsInstanceValid(g.Level) ? g.Level : null;
        var grid = World.Grid;
        Vector2 me = p.Pos2;
        if (_clock - _lastThink > 1.0) { _stuckSince = _clock; _stuckPos = me; }      // was paused, dead or in a menu
        _lastThink = _clock;
        if (level != null && _area.Length == 0) OnArea(level.AreaId);

        TrackBag(hero);
        MarkExplored(grid, me, 6);
        if (_statsSince < 0) _statsSince = g.Time;
        if (hero.D.Level != _lastLevel)
        {
            if (_lastLevel > 0) Log.Info("bot", $"level {hero.D.Level} reached at game t={g.Time:0}s, {g.Time - _levelAt:0}s after level {_lastLevel} (kills={Kills})");
            _lastLevel = hero.D.Level; _levelAt = g.Time;
        }
        if (_allSkills && _clock - _rotT > 14.0 && !p.Busy) { _rotT = _clock; RotateBar(hero); }

        // housekeeping, also in the middle of a long fight: wear upgrades, clear the bag, spend skill points
        if (_clock - _bagT > 4.0) { _bagT = _clock; ManageBag(hero, Pilot == null || Pilot.MayTidyBag()); }
        if (_clock - _buildT > 8.0)
        {
            _buildT = _clock;
            int pts = hero.SkillPoints;
            if (pts > 0 && pts != _idlePoints && !_allSkills)
            {
                if (Pilot != null) AiPilot.SpendPoints(hero); else Game.AutoBuild(hero);
                _idlePoints = hero.SkillPoints;      // whatever could not be spent is not retried until the total changes
            }
        }

        // survival first
        float life = p.LifeFrac;
        if (Pilot == null && life < 0.5f && p.Potions > 0 && _clock - _lastPotion > 1.5) { _lastPotion = _clock; Tap(Key.Q); }
        var target = PickTarget(me, grid);
        if (Pilot != null && (PilotAfterRespawn(p, me) || PilotRetreat(p, me) || PilotFrostDiscipline(p, me))) return;
        if (Pilot == null && target != null && life < 0.35f && p.EvadeCd <= 0f && !p.Busy && _clock - _lastEvade > 1.0 && me.DistanceTo(target.Pos2) < 7f)
        {
            _lastEvade = _clock;
            StopChannel();
            EvadeFrom(p, grid, me, me - target.Pos2);
            _doing = "evade";
            return;
        }

        // directed play walks past monsters that have not noticed the hero
        if (target != null && _focus && !target.Aggro && me.DistanceTo(target.Pos2) > 9f && target != level?.BossMon) target = null;
        if (Pilot != null && target != null && PilotLeaveAlone(target, level)) target = null;      // optional world bosses and named rares are not picked fights
        if (Pilot != null) { target = PilotArenaTarget(target, me); if (PilotBackToArena(p, me)) return; }      // boss leash (AiPilotCombat.cs)
        if (Pilot != null && Pilot.Errand(g, p, level, target)) { _stuckSince = _clock; _doing = "errand"; return; }      // town shopping, rest before a boss (AiPilotPlan.cs)
        if (target != null)
        {
            // between packs: grab the loot at the hero's feet before running to the next monster
            bool threat = target.Aggro && me.DistanceTo(target.Pos2) < 11f;
            if (!threat && _chanSlot < 0 && !p.Busy && Interact(p, hero, me, 7f, true)) { _stuckSince = _clock; return; }
            _stuckSince = _clock; _stuckPos = me;
            Fight(p, hero, target, me, grid);
            return;
        }
        StopChannel();
        _targetId = 0;

        if (_clock < _nudgeUntil) { _doing = "side-step"; return; }
        // the AI pilot, beaten twice: it gains a level or two in the region's open world before the quest again
        bool grind = Pilot != null && Pilot.Grinding;
        if (grind) _focusNote = "grind";
        if (grind && level != null && Pilot.GrindStep(g, level)) { _focusNote = "grind (travel)"; WatchStuck(p, level, grid, me); return; }
        if (_focus && level != null && grid != null && !grind)
        {
            bool handled = false;
            try { handled = Focus(g, p, hero, level, grid, me); }
            catch (Exception e) { _errors++; if (_errors <= 25) Log.Error("bot", "focus step failed", e); }
            if (handled) { WatchStuck(p, level, grid, me); return; }
        }

        if (Interact(p, hero, me)) { WatchStuck(p, level, grid, me); return; }

        Explore(g, p, level, grid, me);
        WatchStuck(p, level, grid, me);
    }

    // ───────────────────────── synthetic keys ─────────────────────────

    private static void Send(Key k, bool down)
    {
        if (k == Key.None) return;
        Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = down, Device = BotDevice });
    }

    private void Hold(Key k, float seconds)
    {
        Send(k, true);
        _held.Add((k, seconds));
    }

    private void Tap(Key k)
    {
        GetViewport()?.GuiReleaseFocus();       // a focused button would swallow the key
        Hold(k, 0.1f);
    }

    private void TickKeys(float real)
    {
        for (int i = _held.Count - 1; i >= 0; i--)
        {
            var (k, left) = _held[i];
            left -= real;
            if (left <= 0f) { Send(k, false); _held.RemoveAt(i); }
            else _held[i] = (k, left);
        }
    }

    private void ReleaseAll()
    {
        for (int i = 0; i < _held.Count; i++) Send(_held[i].key, false);
        _held.Clear();
        StopChannel();
    }

    private void StartChannel(int slot)
    {
        if (_chanSlot == slot) return;
        StopChannel();
        var p = Game.I?.Player;
        if (slot >= 2)
        {
            // the number keys travel the real input path
            GetViewport()?.GuiReleaseFocus();
            Send(SlotKeys[slot], true);
        }
        else if (p != null && IsInstanceValid(p))
        {
            // mouse buttons cannot be held synthetically: the hero is told directly
            p.ForceHold = slot;
            var t = PickedTarget();
            p.TryCast(slot, t != null ? t.Pos2 : p.Pos2 + p.Forward * 2f, t, false);
        }
        _chanSlot = slot;
        _chanSince = _clock;
        Casts++;
        string id = Game.I?.Hero?.D.Bar[slot];
        if (id != null) _castBy[id] = _castBy.GetValueOrDefault(id) + 1;
    }

    private void StopChannel()
    {
        if (_chanSlot < 0) return;
        if (_chanSlot >= 2) Send(SlotKeys[_chanSlot], false);
        var p = Game.I?.Player;
        if (p != null && IsInstanceValid(p)) p.ForceHold = -1;
        _chanSlot = -1;
    }

    private Monster PickedTarget()
    {
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++) if (list[i] != null && IsInstanceValid(list[i]) && list[i].GetInstanceId() == _targetId) return list[i];
        return null;
    }

    /// <summary>Rolls away from a threat. W/A/S/D are world axes (W = −Z, D = +X), so the roll goes in one of eight directions.</summary>
    private void EvadeFrom(Player p, NavGrid grid, Vector2 me, Vector2 away)
    {
        if (away.LengthSquared() < 0.01f) away = -p.Forward;
        away = away.Normalized();
        Vector2 best = Vector2.Zero;
        float bestScore = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            float ang = i * Mathf.Pi / 4f;
            var dir = new Vector2(Mathf.Round(Mathf.Cos(ang)), Mathf.Round(Mathf.Sin(ang)));
            var n = dir.Normalized();
            float room = grid == null ? 5.5f : grid.ClampLine(me, me + n * 5.5f, false).DistanceTo(me);
            float score = n.Dot(away) * 3f + room;
            if (score > bestScore) { bestScore = score; best = dir; }
        }
        GetViewport()?.GuiReleaseFocus();
        if (best.X > 0.5f) Hold(Key.D, 0.2f); else if (best.X < -0.5f) Hold(Key.A, 0.2f);
        if (best.Y > 0.5f) Hold(Key.S, 0.2f); else if (best.Y < -0.5f) Hold(Key.W, 0.2f);
        Hold(Key.Space, 0.1f);
    }

    // ───────────────────────── combat ─────────────────────────

    private Monster PickTarget(Vector2 me, NavGrid grid)
    {
        Monster best = null;
        float bestScore = float.MaxValue;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || m.Untargetable || !IsInstanceValid(m) || !m.IsInsideTree()) continue;
            float d = m.Pos2.DistanceTo(me);
            if (d > 30f) continue;
            ulong id = m.GetInstanceId();
            if (_ignore.TryGetValue(id, out double until))
            {
                if (until > _clock) continue;
                _ignore.Remove(id);
            }
            float score = d;
            if (m.Aggro) score -= 4f;
            if (id == _targetId) score -= 3f;                                             // stay on the current target
            if (d > 10f && grid != null && !grid.LineShot(me, m.Pos2)) score += 8f;       // behind a wall: likely a detour
            if (score < bestScore) { bestScore = score; best = m; }
        }
        if (best == null) return null;

        ulong bid = best.GetInstanceId();
        float bd = best.Pos2.DistanceTo(me);
        if (bid != _targetId) { _targetId = bid; _targetSince = _clock; _targetLife = best.Life; _targetBest = bd; _targetFirst = _clock; _longLog = _clock; }
        else if (best.Life < _targetLife - 0.5f || bd < _targetBest - 1f)
        {
            _targetSince = _clock; _targetLife = best.Life; _targetBest = Math.Min(_targetBest, bd);
        }
        else if (_clock - _targetSince > 9.0)
        {
            // neither hurt nor any closer for a while: it is out of reach, look elsewhere
            _ignore[bid] = _clock + 25.0;
            _targetId = 0;
            Log.Debug("bot", $"giving up on {best.Def?.Id ?? "?"} for now");
            return null;
        }
        return best;
    }

    private void Fight(Player p, Hero hero, Monster t, Vector2 me, NavGrid grid)
    {
        Vector2 tp = t.Pos2;
        float dist = me.DistanceTo(tp);
        bool los = grid == null || grid.LineShot(me, tp);
        _doing = "fight " + (t.Def?.Id ?? "?");
        if (_clock - _longLog > 20.0)
        {
            _longLog = _clock;
            Log.Info("bot", $"long fight ({_clock - _targetFirst:0}s): {t.Def?.Id} {t.Tier}{(t.MiniBoss ? " mini-boss" : "")} [{string.Join(",", t.Affixes)}] life {t.Life:0}/{t.MaxLife:0} barrier {t.Barrier:0} dr×{t.DrMul:0.##} " +
                $"dist {dist:0.0} los={los} aggro={t.Aggro} at ({tp.X:0},{tp.Y:0}) hero at ({me.X:0},{me.Y:0}) busy={p.Busy} res={p.Resource:0} chan={_chanSlot}");
        }

        if (_chanSlot >= 0)
        {
            // holding a channel: stay on the target, let go when it stops paying
            var cd = hero.Eff(hero.D.Bar[_chanSlot]);
            bool keep = cd != null && _clock - _chanSince < 3.5 && p.Resource > 6f && dist <= cd.Radius + t.Radius + 2.5f;
            if (!keep) StopChannel();
            else
            {
                if (dist > 1.6f) MoveTo(p, tp, 0.3);
                return;
            }
        }
        if (p.Busy) return;

        var basic = hero.Eff(hero.D.Bar[0]) ?? hero.Eff(hero.D.Bar[1]);
        float want = basic == null ? 2.5f : Reach(basic, t);
        if (Pilot != null && PilotPosition(p, hero, t, me, dist, los, want, true)) return;     // AI pilot: kiting (AiPilotCombat.cs)

        for (int k = 0; k < SlotOrder.Length; k++)
            if (TryUse(p, hero, SlotOrder[k], t, me, dist, los)) return;

        if (Pilot != null) PilotPosition(p, hero, t, me, dist, los, want, false);
        else if (dist > want || !los) MoveTo(p, tp, 0.35);
        else if (_moving) { p.Stop(); _moving = false; }
    }

    /// <summary>How close the hero has to be for a skill to connect with this target.</summary>
    private static float Reach(SkillDef d, Actor t) => d.Kind switch
    {
        SkillKind.Melee => d.Radius * 0.8f + t.Radius - 0.15f,
        SkillKind.Nova or SkillKind.Channel => d.Radius * 0.75f + t.Radius,
        SkillKind.Wave => Mathf.Max(4f, d.Range * 0.8f),
        SkillKind.Dash or SkillKind.Leap or SkillKind.Blink => Mathf.Max(3f, d.Range * 0.9f),
        _ => Mathf.Clamp(d.Range > 0f ? d.Range * 0.75f : 10f, 4f, 14f),
    };

    private bool TryUse(Player p, Hero hero, int slot, Monster t, Vector2 me, float dist, bool los)
    {
        string id = hero.D.Bar[slot];
        if (id == null) return false;
        var d = hero.Eff(id);
        if (d == null || hero.Rank(id) <= 0 || p.WhyCant(d) != null) return false;
        if (Pilot != null && !PilotMayCast(p, hero, d)) return false;      // AI pilot: no long swing just before the boss strikes (AiPilotCombat.cs)
        Vector2 tp = t.Pos2;
        switch (d.Kind)
        {
            case SkillKind.Buff:
            {
                if (dist > 14f) return false;
                if (d.Duration > 0f && HasBuff(hero, id + "_t")) return false;
                int near = World.CountMonstersNear(me, 9f);
                if (d.Cat == SkillCat.Defensive && p.LifeFrac > 0.8f && near < 3 && !t.IsElite) return false;
                if (d.Cat == SkillCat.Ultimate && !t.IsElite && near < 4) return false;
                return Cast(p, slot, me + p.Forward * 2f, null);
            }
            case SkillKind.Summon:
                if (dist > 22f) return false;
                if (d.Cooldown <= 0f && CountMinions(id) >= Math.Max(1, d.SummonMax)) return false;
                return Cast(p, slot, me.Lerp(tp, 0.35f), null);
            case SkillKind.Channel:
                if (dist > Reach(d, t) + 0.5f) return false;
                // a spinning channel is for crowds: against one or two enemies the resource is better spent on the core skill
                if (World.CountMonstersNear(me, d.Radius + 1.5f) < 3 && p.ResourceFrac < 0.9f) return false;
                StartChannel(slot);
                return true;
            case SkillKind.Melee:
            case SkillKind.Nova:
                if (dist > Reach(d, t)) return false;
                return Cast(p, slot, tp, t);
            case SkillKind.Dash:
            case SkillKind.Leap:
            case SkillKind.Blink:
            {
                bool escape = d.Coef <= 0f || d.Kind == SkillKind.Blink;
                if (escape)
                {
                    if (p.LifeFrac > 0.5f || dist > 7f) return false;
                    Vector2 away = me - tp;
                    away = away.LengthSquared() < 0.01f ? -p.Forward : away.Normalized();
                    return Cast(p, slot, me + away * Mathf.Max(6f, d.Range), null);
                }
                if (dist < 2.5f || dist > Reach(d, t) || !los) return false;
                return Cast(p, slot, tp, d.Kind == SkillKind.Dash ? t : null);
            }
            default:        // projectiles, waves, chains, ground strikes, zones, traps, commands
                if (!los || dist > Reach(d, t)) return false;
                if (d.Kind == SkillKind.Zone && d.Cooldown <= 0f)
                {
                    // a pool that is still burning under the target does not need a second one on top
                    if (_zoneCast.TryGetValue(id, out var z) && _clock - z.at < d.Duration * 0.85 && z.pos.DistanceTo(tp) < d.Radius * 0.7f) return false;
                    if (!Cast(p, slot, tp, t)) return false;
                    _zoneCast[id] = (_clock, tp);
                    return true;
                }
                return Cast(p, slot, tp, t);
        }
    }

    private bool Cast(Player p, int slot, Vector2 point, Actor target)
    {
        if (!p.TryCast(slot, point, target, false)) return false;
        Casts++;
        string id = p.Hero.D.Bar[slot];
        if (id != null) _castBy[id] = _castBy.GetValueOrDefault(id) + 1;
        _moving = false;
        return true;
    }

    private static bool HasBuff(Hero hero, string id)
    {
        var buffs = hero.Buffs;
        for (int i = 0; i < buffs.Count; i++) if (buffs[i].Def.Id == id) return true;
        return false;
    }

    private static int CountMinions(string skillId)
    {
        int n = 0;
        var list = World.Minions;
        for (int i = 0; i < list.Count; i++) if (list[i] != null && !list[i].Dead && list[i].SummonSkill == skillId) n++;
        return n;
    }

    // ───────────────────────── loot and objects ─────────────────────────

    private static bool Has(string s, string part) => s.Contains(part, StringComparison.OrdinalIgnoreCase);

    private static bool IsTravel(string kind) => Has(kind, "exit") || Has(kind, "entrance") || Has(kind, "portal");

    private static int Priority(Interactable it, Hero hero)
    {
        if (it.Item != null) return hero.BagFull ? 0 : 3 + (int)it.Item.Rarity;
        if (it.AutoRadius > 0f) return 2;                          // gold and globes: just walk over them
        string k = it.Kind ?? "";
        // ways out are for Leave(); the stash, the tier statue, the rift stone and the town gate only open panels
        if (IsTravel(k) || Has(k, "stash") || Has(k, "tier") || Has(k, "rift") || Has(k, "vendor") || Has(k, "gate")) return 0;
        if (Has(k, "loot") || Has(k, "item")) return hero.BagFull ? 0 : 3;
        if (Has(k, "altar") || Has(k, "idol") || Has(k, "prisoner") || Has(k, "objective") || Has(k, "ember") || Has(k, "cage")
            || Has(k, "beacon") || Has(k, "event")) return 5;
        if (Has(k, "chest") || Has(k, "shrine")) return 4;
        if (Has(k, "npc") || Has(k, "waypoint") || Has(k, "brazier")) return 2;
        if (Has(k, "break") || Has(k, "urn") || Has(k, "barrel") || Has(k, "crate")) return 1;
        return 2;                                                  // an unknown kind is worth one try
    }

    private bool Interact(Player p, Hero hero, Vector2 me, float maxDist = 18f, bool lootOnly = false)
    {
        Interactable best = null;
        float bestScore = float.MaxValue;
        var all = Interactable.All;
        for (int i = 0; i < all.Count; i++)
        {
            var it = all[i];
            if (it == null || !IsInstanceValid(it) || !it.CanUse || !it.IsInsideTree()) continue;
            if (lootOnly && it.Item == null && it.AutoRadius <= 0f) continue;
            float d = it.Pos2.DistanceTo(me);
            if (d > maxDist) continue;
            ulong id = it.GetInstanceId();
            if (id != _useId && _used.TryGetValue(id, out double until) && until > _clock) continue;
            int pr = Priority(it, hero);
            if (pr <= 0) continue;
            if (Demo && it.Item == null && it.AutoRadius <= 0f && !Has(it.Kind ?? "", "chest") && !Has(it.Kind ?? "", "shrine") && pr != 1) continue;   // the demo only loots and smashes
            float score = d - pr * 2.5f - (id == _useId ? 4f : 0f);
            if (score < bestScore) { bestScore = score; best = it; }
        }
        if (best == null) { _useId = 0; return false; }

        ulong bid = best.GetInstanceId();
        if (bid != _useId) { _useId = bid; _useSince = _clock; _useIssued = -10; _arrivedId = 0; }
        _doing = "use " + (best.Item != null ? "loot" : string.IsNullOrEmpty(best.Kind) ? "object" : best.Kind);
        float bd = best.Pos2.DistanceTo(me);
        if (best.AutoRadius <= 0f && bd <= best.UseRange + 0.3f)
        {
            // In reach, so Player.GoUse has used it — and it is still there. Objective objects may be channelled
            // (toppling an idol means standing still for a few seconds), so wait by it; anything else has had its turn.
            if (_arrivedId != bid) { _arrivedId = bid; _arrivedAt = _clock; }
            double linger = Priority(best, hero) >= 5 ? 6.5 : 0.8;
            if (_clock - _arrivedAt > linger)
            {
                _used[bid] = _clock + 120.0;
                _useId = 0; _arrivedId = 0;
                return false;
            }
            if (_clock - _useIssued > 1.5) { _useIssued = _clock; p.GoUse(best); }
            _stuckSince = _clock;               // standing still on purpose
            return true;
        }
        if (_clock - _useSince > 9.0)
        {
            _used[bid] = _clock + 40.0;           // could not get to it: try again much later
            _useId = 0;
            return false;
        }
        _used[bid] = _clock + 6.0;
        if (_clock - _useIssued > 0.8)
        {
            _useIssued = _clock;
            if (best.AutoRadius > 0f) p.SetMove(best.Pos2, false); else p.GoUse(best);
            _moving = true;
        }
        return true;
    }

    private void TrackBag(Hero hero)
    {
        int c = hero.D.Bag.Count;
        if (_lastBag >= 0 && c > _lastBag) ItemsFound += c - _lastBag;
        _lastBag = c;
    }

    private static float Worth(Item it) => it.Power * (1f + 0.2f * (int)it.Rarity) + it.Upgrade * 5f;

    /// <summary>Wears anything better than what is equipped, then salvages or sells the rest before the bag fills up.</summary>
    private void ManageBag(Hero hero, bool tidy = true)
    {
        var bag = hero.D.Bag;
        for (int i = bag.Count - 1; i >= 0; i--)
        {
            if (i >= bag.Count) continue;
            var it = bag[i];
            if (it == null || !it.IsEquip || hero.WhyCantEquip(it) != null) continue;
            var cur = hero.Compared(it);             // null = there is an empty slot for it
            // hands: a two-hand weapon is weighed against main + off hand, an off-hand never displaces a two-hand weapon
            if (GearEval.IsUpgrade(hero, it)) { if (Pilot != null) Pilot.Equipped(it, cur); hero.Equip(it); }      // the shared evaluator (simulated swap)
        }
        // the AI pilot sells and salvages only in town (where the merchant and the smith are), and sooner, so the bag never fills
        if (tidy && bag.Count >= (Pilot != null ? 24 : 28))
        {
            int before = bag.Count;
            for (int i = bag.Count - 1; i >= 0; i--)
            {
                if (i >= bag.Count) continue;
                var it = bag[i];
                if (it == null) { bag.RemoveAt(i); continue; }
                if (hero.IsProtected(it)) continue;      // 鎖定 by the player or in a gear set: never sold or salvaged
                if (!it.IsEquip) { if (bag.Count >= 34) hero.Sell(it); continue; }
                int keepFrom = bag.Count >= 39 ? 99 : bag.Count >= 36 ? (int)Rarity.Unique : (int)Rarity.Legendary;
                if ((int)it.Rarity >= keepFrom) continue;
                hero.Salvage(it);
            }
            Log.Debug("bot", $"bag tidied: {before} → {bag.Count} items");
        }
        _lastBag = bag.Count;
    }

    // ───────────────────────── directed play ─────────────────────────

    /// <summary>--allskills: every unlocked skill gets its turn on the bar (the alternates of each category swap in and out).</summary>
    private void RotateBar(Hero hero)
    {
        var d = hero.D;
        var all = SkillDb.Of(hero.Class).Where(s => s.UnlockLevel <= d.Level).OrderBy(s => s.UnlockLevel).ToList();
        if (all.Count == 0) return;
        bool changed = false;
        foreach (var s in all) if (hero.BaseRank(s.Id) <= 0) { d.Skills[s.Id] = 1; changed = true; }
        _rot++;
        var bar = new string[6];
        var used = new HashSet<string>();
        SkillCat[] order = { SkillCat.Basic, SkillCat.Core, SkillCat.Defensive, SkillCat.Mobility, SkillCat.Mastery, SkillCat.Ultimate };
        for (int i = 0; i < 6; i++)
        {
            var cat = all.Where(s => s.Cat == order[i]).ToList();
            if (cat.Count == 0) continue;
            var pick = cat[_rot % cat.Count];
            bar[i] = pick.Id; used.Add(pick.Id);
        }
        var rest = all.Where(s => !used.Contains(s.Id)).ToList();
        int ri = _rot;
        for (int i = 0; i < 6 && rest.Count > 0; i++)
            if (bar[i] == null) { var pick = rest[ri++ % rest.Count]; if (used.Add(pick.Id)) bar[i] = pick.Id; }
        if (!changed && bar.SequenceEqual(d.Bar)) return;
        d.Bar = bar;
        hero.Recalc();
        Log.Info("bot", $"bar rotation {_rot}: [{string.Join(", ", bar.Select(b => b ?? "-"))}]");
    }

    private static (QuestDef q, QuestStep s) MainStep(Level level)
    {
        QuestDef pick = null;
        foreach (var q in Quests.Active())
        {
            if (!q.Main) continue;
            if (level?.Region != null && q.Region == level.Region.Id) { pick = q; break; }
            if (pick == null || AiPilot.Active && AiPilot.RegionRank(q.Region) < AiPilot.RegionRank(pick.Region)) pick = q;      // the pilot takes the open regions in its own order (AiPilotPlan.cs)
        }
        return pick == null ? (null, null) : (pick, Quests.CurrentStep(pick));
    }

    private void TravelTo(Game g, string area, string from)
    {
        if (Pilot != null) { Pilot.Travel(g, area, from); _stuckSince = _clock; return; }     // only the ways a player has
        if (_clock - _travelT < 4.0) return;
        _travelT = _clock;
        Log.Info("bot", $"focus: travelling {from ?? "-"} → {area}");
        g.EnterArea(area, from);
    }

    private void GoUseThrottled(Player p, Interactable it)
    {
        if (_clock - _focusIssued < 1.2) return;
        _focusIssued = _clock;
        p.GoUse(it);
        _moving = true;
    }

    private static Interactable NearestObject(Vector2 me, Func<Interactable, bool> test)
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

    private static Monster NearestMonster(Vector2 me, Func<Monster, bool> test)
    {
        Monster best = null; float bd = float.MaxValue;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || m.Dead || !IsInstanceValid(m) || !m.IsInsideTree() || !test(m)) continue;
            float d = m.Pos2.DistanceSquaredTo(me);
            if (d < bd) { bd = d; best = m; }
        }
        return best;
    }

    /// <summary>--tour: moves on when the area has served its purpose; one result line per area.</summary>
    private bool Tour(Game g, Hero hero, Level level)
    {
        if (_tourOver) return false;
        double t = _clock - _areaSince;
        bool dungeon = level.Dungeon != null || level.IsRift;
        if (level.BossDead && _bossDeadAt < 0) _bossDeadAt = _clock;
        int evDone = 0;
        foreach (var e in level.Events) if (e.Done) evDone++;
        bool done;
        if (level.IsTown) done = t > 4.0;
        else if (dungeon) done = level.BossDead && _clock - _bossDeadAt > 9.0;
        else done = (level.Liberated || level.StrongholdMobs.Count == 0) && evDone >= level.Events.Count && t > 6.0;
        bool timedOut = !done && t > (dungeon ? 420.0 : 540.0);
        if (!done && !timedOut) return false;
        string line = $"tour: {level.AreaId} {(timedOut ? "TIMED OUT" : "done")} after {t:0}s: goal={level.Goal} objective={(level.ObjectiveDone ? "done" : "open")} '{level.ObjectiveText}' " +
            $"boss={(level.BossMon != null && IsInstanceValid(level.BossMon) ? level.BossMon.Def.Id : "-")} bossDead={level.BossDead} doorSealed={level.DoorSealed} liberated={level.Liberated} " +
            $"events={evDone}/{level.Events.Count} kills={Kills} elites={Elites} deaths={Deaths} unstuck={Unstuck} heroLevel={hero.D.Level} doing='{_doing}' focus={_focusNote}";
        if (timedOut) Log.Warn("bot", line); else Log.Info("bot", line);
        _tourI++;
        if (_tourI >= _tour.Count)
        {
            _tourOver = true;
            Log.Info("bot", "tour finished");
            g.Quit();
            return true;
        }
        Log.Info("bot", $"tour: next area {_tour[_tourI]} ({_tourI + 1}/{_tour.Count})");
        g.HarnessTravel(_tour[_tourI]);
        return true;
    }

    /// <summary>Walks toward a far goal; within 11 m the ordinary object handling takes over (returns false).</summary>
    private bool Approach(Player p, Vector2 me, Vector2 goal, string what, float near = 11f)
    {
        _focusNote = what;
        if (me.DistanceTo(goal) <= near) return false;
        MoveTo(p, goal, 1.0);
        _doing = "focus " + what;
        return true;
    }

    /// <summary>
    /// Directed play (--focus, --campaign): do what the place is for instead of exploring every corner. In a dungeon or
    /// rift: the objective, then the boss, then the way out. In the open: the stronghold, then the events. With
    /// --campaign the main quest decides where to go: talk to whoever it names, travel where it points, and repeat
    /// until the last act is done. Returns true when it issued this think's order.
    /// </summary>
    private bool Focus(Game g, Player p, Hero hero, Level level, NavGrid grid, Vector2 me)
    {
        string here = level.AreaId ?? "";
        if (_tour != null && Tour(g, hero, level)) return true;
        QuestDef quest = null; QuestStep step = null;
        if (_campaign)
        {
            (quest, step) = MainStep(level);
            if (step == null)
            {
                if (!_campaignOver)
                {
                    _campaignOver = true;
                    Log.Info("bot", $"campaign: no main quest is active any more — campaignDone={Quests.CampaignDone} {Quests.Summary()} level={hero.D.Level} deaths={Deaths} game t={g.Time:0}s");
                }
                _focusNote = "campaign over";
                return false;
            }
            string dest = step.Event == "talk" ? ZoneDb.Town : step.Area ?? quest.Region ?? ZoneDb.Town;
            _focusNote = $"{quest.Id}@{Quests.StepIndex(quest)}:{step.Event}({step.Arg ?? step.Region})→{dest}";
            if (level.IsTown)
            {
                if (step.Event == "talk")
                {
                    var npc = NearestObject(me, it => it.Tag as string == step.Arg);
                    if (npc == null) { Log.Warn("bot", $"campaign: nobody called '{step.Arg}' in town"); return false; }
                    _doing = "talk " + step.Arg;
                    GoUseThrottled(p, npc);
                    _stuckSince = _clock;
                    return true;
                }
                TravelTo(g, dest, here);
                return true;
            }
            bool sameRegion = level.Region != null && ZoneDb.RegionOf(dest)?.Id == level.Region.Id;
            if (dest != here)
            {
                // a boss that has just died leaves a chest: take it before leaving
                if (level.BossDead && _bossDeadAt < 0) _bossDeadAt = _clock;
                if (level.BossDead && _clock - _bossDeadAt < 9.0) return false;
                if (dest == ZoneDb.Town || !sameRegion || level.Dungeon != null || step.Event != "stronghold") { TravelTo(g, dest, here); return true; }
            }
        }
        if (level.IsTown) return false;

        if (level.Dungeon != null || level.IsRift)
        {
            if (level.Cleared)
            {
                if (_bossDeadAt < 0) _bossDeadAt = _clock;
                if (_clock - _bossDeadAt < 9.0) { _focusNote = "boss loot"; return false; }
                var portal = NearestObject(me, it => it.Kind == "portal");
                if (portal != null) { _focusNote = "portal out"; _doing = "focus leave"; GoUseThrottled(p, portal); return true; }
                return false;
            }
            _bossDeadAt = -1;
            if (_bossRush && level.DoorSealed) { level.OpenBossDoor(); Log.Info("bot", $"bossrush: door of {here} opened without the objective"); }
            if (level.Phase == Level.DungeonPhase.Objective && !_bossRush)
            {
                if (level.IsRift)
                {
                    var m = NearestMonster(me, x => x != level.BossMon);
                    return m != null && Approach(p, me, m.Pos2, "rift hunt", 8f);
                }
                switch (level.Goal)
                {
                    case DungeonGoal.SlayElites:
                    {
                        Monster best = null; float bd = float.MaxValue;
                        foreach (var m in level.ObjMonsters)
                        {
                            if (m == null || !IsInstanceValid(m) || m.Dead) continue;
                            float d = m.Pos2.DistanceSquaredTo(me);
                            if (d < bd) { bd = d; best = m; }
                        }
                        return best != null && Approach(p, me, best.Pos2, "marked elite", 8f);
                    }
                    case DungeonGoal.DestroyIdols:
                    case DungeonGoal.FreePrisoners:
                    {
                        string kind = level.Goal == DungeonGoal.DestroyIdols ? "idol" : "prisoner";
                        var obj = NearestObject(me, it => it.Kind == kind);
                        return obj != null && Approach(p, me, obj.Pos2, kind);
                    }
                    case DungeonGoal.CollectEmbers:
                    {
                        if (level.Embers >= level.EmbersNeeded)
                        {
                            var altar = NearestObject(me, it => it.Kind == "emberaltar");
                            return altar != null && Approach(p, me, altar.Pos2, "ember altar");
                        }
                        var shard = NearestObject(me, it => it.Kind == "ember");
                        if (shard != null) return Approach(p, me, shard.Pos2, "ember shard");
                        var carrier = NearestMonster(me, x => x.Objective);
                        return carrier != null && Approach(p, me, carrier.Pos2, "ember carrier", 8f);
                    }
                }
                return false;
            }
            var boss = level.BossMon;
            if (boss != null && IsInstanceValid(boss) && !boss.Dead) return Approach(p, me, level.ArenaPos, "boss", 4f);
            return false;
        }

        if (level.Region != null)
        {
            bool wantHold = !level.Liberated && level.StrongholdMobs.Count > 0 && (!_campaign || step == null || step.Event == "stronghold" || step.Area == here);
            if (wantHold)
            {
                Monster best = null; float bd = float.MaxValue;
                foreach (var m in level.StrongholdMobs)
                {
                    if (m == null || !IsInstanceValid(m) || m.Dead) continue;
                    float d = m.Pos2.DistanceSquaredTo(me);
                    if (d < bd) { bd = d; best = m; }
                }
                if (best != null) return Approach(p, me, best.Pos2, "stronghold", 8f);
            }
            if (_campaign) return false;
            // events: the one in progress, else the nearest one not yet tried (a ritual has no object to click: walking up starts it)
            WorldEvent ev = null; float evD = float.MaxValue;
            foreach (var e in level.Events)
            {
                if (e.Done) continue;
                if (e.Active) { ev = e; break; }
                float d = e.Pos.DistanceSquaredTo(me);
                if (d < evD) { evD = d; ev = e; }
            }
            if (ev != null) return Approach(p, me, ev.Pos, ev.Active ? "event in progress" : "event", 5f);
        }
        return false;
    }

    // ───────────────────────── movement ─────────────────────────

    private void MoveTo(Player p, Vector2 pos, double every)
    {
        if (_clock - _moveAt < every && pos.DistanceSquaredTo(_moveTo) < 2.25f) return;
        _moveAt = _clock;
        _moveTo = pos;
        p.SetMove(pos, false);
        _moving = true;
    }

    private Vector2 _lingerPos; private double _lingerSince; private int _lingerCasts = -1, _lingerKills;

    private void WatchStuck(Player p, Level level, NavGrid grid, Vector2 me)
    {
        // going round in circles: a minute within a few metres without a cast, a kill or a goal reached
        if (Casts != _lingerCasts || Kills != _lingerKills || me.DistanceTo(_lingerPos) > 9f || level == null || level.IsTown)
        { _lingerCasts = Casts; _lingerKills = Kills; _lingerPos = me; _lingerSince = _clock; }
        else if (_clock - _lingerSince > 60.0 && Pilot != null)
        {
            Unstuck++;
            Log.Warn("bot", $"pilot: going nowhere near ({me.X:0.0}, {me.Y:0.0}) in {_area} for a minute while '{_doing}' ({_focusNote})");
            AbandonGoal(grid);
            _lingerPos = me; _lingerSince = _clock; _stuckPos = me; _stuckSince = _clock; _nudges = 0;
            Pilot.OnStuck("going nowhere");
            return;
        }
        else if (_clock - _lingerSince > 60.0)
        {
            Unstuck++;
            Vector2 to = level.Checkpoint;
            if (grid != null) to = grid.NearestWalk(to);
            Log.Warn("bot", $"going nowhere near ({me.X:0.0}, {me.Y:0.0}) in {_area} for a minute while '{_doing}' ({_focusNote}); jumping to the checkpoint ({to.X:0.0}, {to.Y:0.0})");
            AbandonGoal(grid);
            p.Stop(); p.Teleport(to);
            _lingerPos = to; _lingerSince = _clock; _stuckPos = to; _stuckSince = _clock; _nudges = 0;
            return;
        }
        if (me.DistanceTo(_stuckPos) > 1.2f) { _stuckPos = me; _stuckSince = _clock; _nudges = 0; return; }
        double still = _clock - _stuckSince;
        if (_nudges < 3 && still > 3.0 * (_nudges + 1))
        {
            // not getting anywhere: the goal may be unreachable. Step aside, and give the goal up the second time.
            _nudges++;
            if (++_goalFails >= 2) AbandonGoal(grid);
            if (_doorId != 0 && _goalFails >= 2) { _travelTried.Add(_doorId); _doorId = 0; }
            if (grid != null)
            {
                Vector2 q = grid.RandomWalkNear(me, 2f, 6f);
                p.SetMove(q, false);
                _moveAt = _clock; _moveTo = q;
                _nudgeUntil = _clock + 1.3;          // let the side-step happen before the goal is ordered again
            }
        }
        else if (still > 40.0 && Pilot != null)
        {
            Unstuck++;
            Log.Warn("bot", $"pilot: stuck at ({me.X:0.0}, {me.Y:0.0}) in {_area} while '{_doing}'");
            AbandonGoal(grid);
            _stuckPos = me; _stuckSince = _clock; _nudges = 0;
            Pilot.OnStuck("stuck");
        }
        else if (still > 40.0)
        {
            Unstuck++;
            Vector2 safe = level != null ? level.Checkpoint : me;
            if (grid != null)
            {
                safe = grid.NearestWalk(safe);
                if (!grid.WalkAt(safe) && level != null) safe = grid.NearestWalk(level.EntryPos(null));
            }
            Log.Warn("bot", $"stuck at ({me.X:0.0}, {me.Y:0.0}) in {_area} while '{_doing}'; jumping to the checkpoint");
            AbandonGoal(grid);
            p.Stop();
            p.Teleport(safe);
            _stuckPos = safe; _stuckSince = _clock; _nudges = 0;
        }
    }

    // ───────────────────────── exploration ─────────────────────────

    private void MarkExplored(NavGrid grid, Vector2 at, int cells)
    {
        if (grid == null) return;
        int n = grid.W * grid.H;
        if (_explored == null || _explored.Length != n) _explored = new bool[n];
        var c = grid.CellOf(at);
        for (int dy = -cells; dy <= cells; dy++)
            for (int dx = -cells; dx <= cells; dx++)
            {
                if (dx * dx + dy * dy > cells * cells) continue;
                int x = c.X + dx, y = c.Y + dy;
                if (grid.In(x, y)) _explored[y * grid.W + x] = true;
            }
    }

    private bool IsExplored(NavGrid grid, Vector2 at)
    {
        if (grid == null || _explored == null) return true;
        var c = grid.CellOf(at);
        return !grid.In(c.X, c.Y) || _explored[c.Y * grid.W + c.X];
    }

    private void SetGoal(Vector2 pos, string kind, MapMarker marker)
    {
        _goal = pos; _goalKind = kind; _goalMarker = marker; _goalSince = _clock; _goalFails = 0;
    }

    private void AbandonGoal(NavGrid grid)
    {
        if (_goalKind == null) return;
        if (_goalMarker != null) _visited.Add(_goalMarker);
        else MarkExplored(grid, _goal, 3);
        _goalKind = null; _goalMarker = null; _goalFails = 0;
    }

    private void Explore(Game g, Player p, Level level, NavGrid grid, Vector2 me)
    {
        if (grid == null || level == null) { _doing = "idle"; return; }
        double budget = level.IsTown ? 25.0 : level.IsRift ? 900.0 : 420.0;
        if (_focus || Demo) _leaveSince = -1;   // directed play decides when and how to leave; the demo never leaves
        else if (_leaveSince < 0 && _clock - _areaSince > budget) BeginLeave("time is up");

        if (_leaveSince < 0)
        {
            if (_goalKind != null)
            {
                bool reached = me.DistanceTo(_goal) < 3f;
                bool seen = _goalMarker == null && _goalKind == "frontier" && IsExplored(grid, _goal);
                if (reached || seen) { if (_goalMarker != null) _visited.Add(_goalMarker); _goalKind = null; _goalMarker = null; }
                else if (_clock - _goalSince > 45.0) AbandonGoal(grid);
            }
            if (_goalKind == null && !PickGoal(level, grid, me))
            {
                if (_focus || Demo) { _doing = "idle (nothing left here)"; return; }
                BeginLeave("nothing left to explore");
            }
            if (_goalKind != null)
            {
                MoveTo(p, _goal, 1.2);
                _doing = "explore " + _goalKind;
                return;
            }
        }
        Leave(g, p, level, me);
    }

    private bool PickGoal(Level level, NavGrid grid, Vector2 me)
    {
        // 1. what the level advertises (objectives, events, altars, shrines, chests…), nearest first; the boss comes last
        MapMarker bestM = null, boss = null;
        float bd = float.MaxValue;
        var markers = level.Markers;
        if (markers != null)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                if (m == null || _visited.Contains(m)) continue;
                string k = m.Kind ?? "";
                if (IsTravel(k) || Has(k, "npc") || Has(k, "stash") || Has(k, "start") || Has(k, "waypoint") || Has(k, "player") || Has(k, "hero") || Has(k, "tier") || Has(k, "rift")) continue;
                float d = m.Pos.DistanceTo(me);
                if (d < 3f || !grid.WalkAt(grid.NearestWalk(m.Pos))) { _visited.Add(m); continue; }
                if (Has(k, "boss")) { boss ??= m; continue; }
                if (d < bd) { bd = d; bestM = m; }
            }
        }
        if (bestM != null) { SetGoal(grid.NearestWalk(bestM.Pos), "marker:" + bestM.Kind, bestM); return true; }

        // 2. the nearest ground the bot can walk to and has not been near yet
        if (FindFrontier(grid, me, out var frontier)) { SetGoal(frontier, "frontier", null); return true; }

        // 3. the boss
        if (boss != null) { SetGoal(grid.NearestWalk(boss.Pos), "marker:" + boss.Kind, boss); return true; }
        var b = level.Boss;
        if (!_bossTried && b != null && IsInstanceValid(b) && !b.Dead && b.IsInsideTree() && b.Pos2.DistanceTo(me) > 6f)
        {
            _bossTried = true;
            SetGoal(grid.NearestWalk(b.Pos2), "boss", null);
            return true;
        }
        return false;
    }

    /// <summary>Breadth-first search over walkable cells for the nearest one the bot has not been within 12 m of.</summary>
    private bool FindFrontier(NavGrid grid, Vector2 me, out Vector2 goal)
    {
        goal = default;
        int w = grid.W, n = w * grid.H;
        if (_explored == null || _explored.Length != n) return false;
        if (_bfsMark == null || _bfsMark.Length != n) { _bfsMark = new int[n]; _bfsQueue = new int[n]; _bfsStamp = 0; }
        if (_bfsStamp >= int.MaxValue - 1) { Array.Clear(_bfsMark, 0, _bfsMark.Length); _bfsStamp = 0; }
        _bfsStamp++;
        var s = grid.NearestWalkCell(grid.CellOf(me));
        if (!grid.Walk(s.X, s.Y)) return false;
        int head = 0, tail = 0;
        int si = s.Y * w + s.X;
        _bfsQueue[tail++] = si;
        _bfsMark[si] = _bfsStamp;
        while (head < tail)
        {
            int ci = _bfsQueue[head++];
            int cx = ci % w, cy = ci / w;
            if (!_explored[ci]) { goal = grid.Center(cx, cy); return true; }
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + Dx[k], ny = cy + Dy[k];
                if (!grid.Walk(nx, ny)) continue;
                int ni = ny * w + nx;
                if (_bfsMark[ni] == _bfsStamp) continue;
                _bfsMark[ni] = _bfsStamp;
                _bfsQueue[tail++] = ni;
            }
        }
        return false;
    }

    // ───────────────────────── leaving ─────────────────────────

    private void BeginLeave(string why)
    {
        _leaveSince = _clock;
        _goalKind = null; _goalMarker = null;
        _doorId = 0;
        _lastDirect = _clock;                   // give the level's own exits a fair chance before asking the game directly
        Log.Info("bot", $"leaving {_area}: {why}");
    }

    private void Leave(Game g, Player p, Level level, Vector2 me)
    {
        _doing = "leave";
        double t = _clock - _leaveSince;
        if (t > 120.0)
        {
            // nothing let the hero out (quest gates, most likely): look around once more, talk to people again
            Log.Info("bot", $"could not leave {_area}; exploring again");
            _leaveSince = -1; _areaSince = _clock; _explored = null; _travelTried.Clear(); _used.Clear(); _visited.Clear(); _bossTried = false;
            return;
        }

        // the level's own ways out: dungeon entrances first, then portals, then exits; the least used first
        Interactable door = null;
        float bestScore = float.MaxValue;
        string doorKey = null;
        var all = Interactable.All;
        for (int i = 0; i < all.Count; i++)
        {
            var it = all[i];
            if (it == null || !IsInstanceValid(it) || !it.CanUse || !it.IsInsideTree()) continue;
            string k = it.Kind ?? "";
            int rank = Has(k, "entrance") ? 0 : Has(k, "portal") ? 1 : Has(k, "exit") ? 2 : -1;
            if (rank < 0) continue;
            ulong id = it.GetInstanceId();
            if (_travelTried.Contains(id)) continue;
            Vector2 pos = it.Pos2;
            string key = id == _doorId ? null : $"{_area}|{rank}|{Mathf.RoundToInt(pos.X / 4f)},{Mathf.RoundToInt(pos.Y / 4f)}";
            int uses = key != null && _doorUses.TryGetValue(key, out int u) ? u : 0;
            float score = id == _doorId ? float.MinValue : uses * 5000f + rank * 300f + pos.DistanceTo(me);
            if (score < bestScore) { bestScore = score; door = it; doorKey = key; }
        }
        if (door != null)
        {
            ulong id = door.GetInstanceId();
            if (id != _doorId)
            {
                _doorId = id; _doorSince = _clock; _doorIssued = -10; _goalFails = 0;
                if (doorKey != null) _doorUses[doorKey] = _doorUses.GetValueOrDefault(doorKey) + 1;
            }
            float d = door.Pos2.DistanceTo(me);
            bool there = door.AutoRadius > 0f ? d <= door.AutoRadius + 0.3f : d <= door.UseRange + 0.3f;
            if ((there && _clock - _doorSince > 1.5) || _clock - _doorSince > 30.0)
            {
                _travelTried.Add(id);           // used it (or could not reach it) and the hero is still here: try another way
                _doorId = 0;
                return;
            }
            if (_clock - _doorIssued > 1.0)
            {
                _doorIssued = _clock;
                if (door.AutoRadius > 0f) p.SetMove(door.Pos2, false); else p.GoUse(door);
                _moving = true;
            }
            _doing = "leave via " + door.Kind;
            return;
        }

        // no door worked: ask the game directly
        if (_clock - _lastDirect > 6.0)
        {
            _lastDirect = _clock;
            DirectTravel(g, level);
        }
    }

    private void DirectTravel(Game g, Level level)
    {
        string here = level.AreaId ?? "";
        _options.Clear();
        string next = null;
        try { next = Quests.NextObjectiveArea(); }
        catch (Exception e) { _errors++; if (_errors <= 25) Log.Error("bot", "NextObjectiveArea failed", e); }
        if (!string.IsNullOrEmpty(next) && next != here) _options.Add(next);

        int from = _options.Count;
        var region = level.Region ?? ZoneDb.RegionOf(here);
        if (region != null)
        {
            if (region.Id != here) _options.Add(region.Id);
            foreach (var dgn in region.Dungeons) if (dgn.Id != here && !_options.Contains(dgn.Id)) _options.Add(dgn.Id);
        }
        foreach (var r in ZoneDb.Regions) if (r.Id != here && !_options.Contains(r.Id)) _options.Add(r.Id);
        for (int i = _options.Count - 1; i > from; i--)       // shuffle everything after the quest's own suggestion
        {
            int j = Rng.R.Next(from, i + 1);
            (_options[i], _options[j]) = (_options[j], _options[i]);
        }
        if (!level.IsTown) _options.Add(ZoneDb.Town);

        foreach (var area in _options)
        {
            bool open;
            try { open = area == ZoneDb.Town || Quests.CanEnter(area); }
            catch { open = false; }
            if (!open) continue;
            Log.Info("bot", $"travelling {here} → {area}");
            g.EnterArea(area, here);
            if (g.State == Game.GameState.Loading) return;
        }
        Log.Debug("bot", $"no area is open from {here} yet");
    }
}
