using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// 展示模式: an attract loop of the most chaotic scenes, started from the title screen (the entry 「展示模式」, or by itself
/// after <see cref="IdleSeconds"/> without input on the title). Every 30–45 s a new scene behind a short ember title card:
/// a class (all eight in turn, never twice running) at a level fitting the place, in legendary / unique gear with a
/// changing skill bar the bot casts; a place from the seven regions, their side dungeons, the story-boss lairs, the
/// named-encounter arenas, the rifts and the town (never the same look twice running); dense waves of packs, champions,
/// affixed elites and named rares; now and then a shower of loot of every rarity. The hero cannot die (but takes the
/// hits), nothing is saved, no setting changes. Any key, mouse button or pad button returns to the title screen as it was
/// (an attract run started by idling also ends on a mouse move). Alt-Tab / minimising ends it too.
/// Test switches: --demo (start from the title at once), --demo-secs=N (scene length), --demo-idle=N (idle seconds).
/// </summary>
public partial class DemoMode : Node
{
    /// <summary>Seconds without input on the title screen before the attract loop starts by itself.</summary>
    public const float DefaultIdleSeconds = 60f;
    public static float IdleSeconds
    {
        get
        {
            string v = Game.I?.ArgValue("demo-idle");
            return !string.IsNullOrEmpty(v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0.5f ? s : DefaultIdleSeconds;
        }
    }

    public static DemoMode Current { get; private set; }
    public static bool Active => Current != null && IsInstanceValid(Current) && !Current._stopping;

    private enum Kind { Region, Dungeon, StoryBoss, Named, Rift, Town }
    private sealed class Scene
    {
        public Kind Kind; public string Area, Theme, Place; public RegionDef Region; public DungeonDef Dungeon; public NamedDef Named;
    }

    private bool _attract, _stopping, _staged, _blackOn;
    private double _clock, _sceneT, _sceneLen, _waveT, _lootT, _zoomT, _inputGrace = 1.0;
    private float _motion;
    private Scene _scene, _lastScene;
    private string _lastTheme;
    private Kind? _lastKind;
    private ClassId _cls;
    private int _scenes, _spawned;
    private Level _stagedLevel;
    private TestBot _bot;
    private readonly Random _rng = new();
    private readonly List<ClassId> _classBag = new();
    private readonly Dictionary<Kind, List<Scene>> _queues = new();
    private readonly HashSet<string> _seenClasses = new(), _seenThemes = new(), _seenBosses = new(), _seenAreas = new();
    private ClassId? _lastClass;
    private string _featured;

    // overlay: the caption (bottom left), the hint, and a black veil for the cut between scenes
    private CanvasLayer _layer;
    private ColorRect _black;
    private Label _caption, _hint;

    // ───────────────────────── start / stop ─────────────────────────

    /// <summary>Starts the loop from the title screen. <paramref name="attract"/>: started by idling (a mouse move ends it too).</summary>
    public static void Start(bool attract)
    {
        var g = Game.I;
        if (g == null || Active) return;
        if (g.State != Game.GameState.Menu || g.Hero != null) { Log.Warn("demo", $"start refused: state={g.State}"); return; }
        var d = new DemoMode { Name = "DemoMode", _attract = attract, _inputGrace = attract ? 0.25 : 1.0 };
        Current = d;
        g.AddChild(d);          // last child of Game: it sees every input event before the game's own handlers
        Log.Info("demo", $"展示模式 started ({(attract ? "title screen idle" : "chosen from the title screen")})");
        d.NextScene();
    }

    /// <summary>Back to the title screen: everything of the demo is freed, nothing was saved, music back to the menu theme.</summary>
    public static void Stop(string why)
    {
        var d = Current;
        if (d == null || !IsInstanceValid(d) || d._stopping) return;
        d._stopping = true;
        var g = Game.I;
        Log.Info("demo", $"展示模式 ends ({why}) after {d._clock:0}s: {d.Report()}");
        if (d._bot != null && IsInstanceValid(d._bot)) { d.RemoveChild(d._bot); d._bot.QueueFree(); }
        d._bot = null;
        if (g != null)
        {
            if (g.Hud != null) g.Hud.Lean = false;
            g.CardTitle = g.CardSub = g.CardTip = null;
            g.NoSave = true;                 // ToMenu would save the character: there is nothing to save
            Log.Guard("demo", g.ToMenu);
            g.NoSave = false;
            Log.Guard("demo", () => g.Cam?.SetZoomTarget(1f, 0f));
        }
        Current = null;
        d.QueueFree();
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        var g = Game.I;
        g.NoSave = true;
        string secs = g.ArgValue("demo-secs");
        if (!string.IsNullOrEmpty(secs) && double.TryParse(secs, NumberStyles.Float, CultureInfo.InvariantCulture, out double s) && s >= 5) _fixedLen = s;
        BuildCatalogue();
        BuildOverlay();
        _bot = new TestBot { Name = "DemoBot", Demo = true };
        AddChild(_bot);
    }

    private double _fixedLen = -1;

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
        var g = Game.I;
        if (g != null) { g.NoSave = false; if (g.Hud != null) g.Hud.Lean = false; g.CardTitle = g.CardSub = g.CardTip = null; }
    }

    // ───────────────────────── input: anything returns to the title ─────────────────────────

    public override void _Input(InputEvent e)
    {
        if (_stopping || e.Device == TestBot.BotDevice) return;
        bool quit = false;
        switch (e)
        {
            case InputEventKey k when k.Pressed && !k.Echo:
                if (k.Keycode is Key.F12 or Key.F11) return;         // a screenshot / full-screen toggle of the demo is fine
                quit = true; break;
            case InputEventMouseButton mb when mb.Pressed: quit = true; break;
            case InputEventJoypadButton jb when jb.Pressed: quit = true; break;
            case InputEventMouseMotion mm when _attract:
                _motion += MoveOf(mm);
                quit = _motion > 40f;
                break;
        }
        if (!quit) return;
        if (_clock < _inputGrace) { GetViewport().SetInputAsHandled(); return; }     // the click / key that started it
        GetViewport().SetInputAsHandled();
        Stop(e is InputEventMouseMotion ? "mouse moved" : "input");
    }

    private Vector2? _lastMouse;
    /// <summary>How far the pointer moved: Relative, or the step between two positions when an event carries none (synthetic ones).</summary>
    private float MoveOf(InputEventMouseMotion mm)
    {
        Vector2 d = mm.Relative;
        if (d == Vector2.Zero && _lastMouse.HasValue) d = mm.Position - _lastMouse.Value;
        _lastMouse = mm.Position;
        return d.Length();
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        if (_stopping) return;
        try { Tick(delta); }
        catch (Exception e) { Log.Error("demo", "demo frame failed", e); }
    }

    private void Tick(double dt)
    {
        _clock += dt;
        _motion = Mathf.Max(0f, _motion - (float)dt * 20f);            // slow drift of a resting hand does not add up
        var g = Game.I;
        if (g == null) return;
        if (!g.AppFocused || DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Minimized) { Stop("window in the background"); return; }
        if (g.Ui != null && g.Ui.AnyOpen) Log.Guard("demo", g.Ui.CloseAll);      // a dialogue or a panel a script opened
        if (g.State == Game.GameState.Menu && g.Hero == null && _scenes > 0) { Stop("the game left the demo"); return; }
        if (g.State != Game.GameState.Play) return;
        if (_cutting)
        {
            _cutT += dt;
            SetBlack((float)Math.Min(1.0, _cutT / 0.3));
            if (_cutT >= 0.32) { _cutting = false; Switch(); }
            return;
        }

        var lv = g.Level;
        if (lv == null || !IsInstanceValid(lv) || g.Player == null || !IsInstanceValid(g.Player)) return;
        if (lv != _stagedLevel) { Stage(g, lv); return; }
        if (_blackOn) SetBlack(0f);

        _sceneT += dt;
        if (_sceneT >= _sceneLen) { NextScene(); return; }
        _waveT -= dt; _lootT -= dt; _zoomT -= dt;
        if (_waveT <= 0f) { _waveT = 4.5 + _rng.NextDouble() * 2.0; Log.Guard("demo", () => Wave(g, lv, false)); }
        if (_lootT <= 0f) { _lootT = 13.0 + _rng.NextDouble() * 7.0; Log.Guard("demo", () => LootShower(g, lv)); }
        if (_zoomT <= 0f && _scene != null && _scene.Kind is not (Kind.StoryBoss or Kind.Named))
        {
            _zoomT = 7.0 + _rng.NextDouble() * 5.0;
            float z = 0.86f + (float)_rng.NextDouble() * 0.3f;          // a slow, slight push in or out
            Log.Guard("demo", () => g.Cam?.SetZoomTarget(z, 3.2f));
        }
    }

    // ───────────────────────── scenes ─────────────────────────

    private void BuildCatalogue()
    {
        foreach (Kind k in Enum.GetValues<Kind>()) _queues[k] = new List<Scene>();
        foreach (var r in ZoneDb.Regions)
        {
            _queues[Kind.Region].Add(new Scene { Kind = Kind.Region, Area = r.Id, Theme = r.Theme, Region = r });
            foreach (var d in r.Dungeons)
                _queues[d.Story ? Kind.StoryBoss : Kind.Dungeon].Add(new Scene { Kind = d.Story ? Kind.StoryBoss : Kind.Dungeon, Area = d.Id, Theme = d.Theme, Region = r, Dungeon = d });
        }
        try
        {
            NamedDb.Ensure();
            foreach (var n in NamedDb.All)
                if (n.Kind is NamedKind.Rare or NamedKind.Stronghold or NamedKind.Dungeon or NamedKind.World)
                    _queues[Kind.Named].Add(new Scene { Kind = Kind.Named, Area = NamedAreas.ArenaOf(n.Id), Theme = n.Theme, Named = n, Region = ZoneDb.Region(n.Region) });
        }
        catch (Exception e) { Log.Error("demo", "listing the named encounters failed", e); }
        foreach (int t in new[] { 1, 3, 5, 8 }) _queues[Kind.Rift].Add(new Scene { Kind = Kind.Rift, Area = "rift:" + t, Theme = "rift" });
        _queues[Kind.Town].Add(new Scene { Kind = Kind.Town, Area = ZoneDb.Town, Theme = "town" });
        Log.Info("demo", "catalogue: " + string.Join(" ", _queues.Select(kv => $"{kv.Key}={kv.Value.Count}")));
        _pools = _queues.ToDictionary(kv => kv.Key, kv => new List<Scene>());
    }

    private Dictionary<Kind, List<Scene>> _pools;
    private static readonly (Kind k, int w)[] KindWeights =
    {
        (Kind.Region, 24), (Kind.Dungeon, 14), (Kind.StoryBoss, 16), (Kind.Named, 22), (Kind.Rift, 10), (Kind.Town, 6),
    };

    /// <summary>A kind by weight (not the last one twice running), then the next entry of its shuffled pool whose look differs from the last scene.</summary>
    private Scene PickScene()
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int total = KindWeights.Where(x => _queues[x.k].Count > 0 && x.k != _lastKind).Sum(x => x.w);
            int roll = _rng.Next(Math.Max(1, total));
            Kind kind = Kind.Region;
            foreach (var (k, w) in KindWeights)
            {
                if (_queues[k].Count == 0 || k == _lastKind) continue;
                if (roll < w) { kind = k; break; }
                roll -= w;
            }
            var pool = _pools[kind];
            if (pool.Count == 0) { pool.AddRange(_queues[kind].OrderBy(_ => _rng.Next())); }
            int idx = pool.FindIndex(s => s.Theme != _lastTheme && s.Area != _lastScene?.Area);
            if (idx < 0) continue;
            var s = pool[idx];
            pool.RemoveAt(idx);
            _lastKind = kind;
            return s;
        }
        return _queues[Kind.Region][_rng.Next(_queues[Kind.Region].Count)];
    }

    private ClassId PickClass()
    {
        if (_classBag.Count == 0)
        {
            _classBag.AddRange(Enumerable.Range(0, Names.ClassCount).Select(i => (ClassId)i).OrderBy(_ => _rng.Next()));
            if (_lastClass.HasValue && _classBag[0] == _lastClass.Value) { _classBag.RemoveAt(0); _classBag.Add(_lastClass.Value); }
        }
        var c = _classBag[0];
        _classBag.RemoveAt(0);
        _lastClass = c;
        return c;
    }

    private int LevelFor(Scene s)
    {
        int baseLv = s.Region?.Level ?? 20;
        int lv = s.Kind switch
        {
            Kind.Region or Kind.Dungeon or Kind.StoryBoss => baseLv + _rng.Next(4, 15),
            Kind.Named => Math.Max(baseLv + 6, 20) + _rng.Next(0, 16),
            _ => _rng.Next(24, 51),
        };
        return Math.Clamp(Math.Max(lv, 14 + _rng.Next(0, 8)), 1, Balance.LevelCap);
    }

    /// <summary>The cut: in play the screen fades to black first (0.3 s); the next place is built behind its title card.</summary>
    private void NextScene()
    {
        var g = Game.I;
        if (g == null || _stopping) return;
        if (g.State == Game.GameState.Play && _scenes > 0) { _cutting = true; _cutT = 0; return; }
        Switch();
    }

    private bool _cutting;
    private double _cutT;

    private void Switch()
    {
        var g = Game.I;
        if (g == null || _stopping) return;
        var s = PickScene();
        _cls = PickClass();
        int level = LevelFor(s);
        s.Place = s.Kind == Kind.Named && s.Region != null ? s.Region.Name : ZoneDb.AreaName(s.Area);     // an arena is named after its boss: the caption names the region
        _scene = s;
        _staged = false;
        _scenes++;
        _sceneT = 0;
        _sceneLen = _fixedLen > 0 ? _fixedLen : 30.0 + _rng.NextDouble() * 15.0;
        _featured = s.Named?.NameZh ?? (s.Kind == Kind.StoryBoss ? MonsterDb.Get(s.Region?.Boss)?.Name : null);

        SaveData d;
        try { d = Hero.NewCharacter(Names.Class(_cls), _cls, false); }
        catch (Exception e) { Log.Error("demo", "creating the demo character failed", e); Stop("error"); return; }
        d.Level = level; d.Xp = 0;
        d.Seed = _rng.Next(1, int.MaxValue);
        d.Name = Names.Class(_cls);

        // the ember card of the next place comes up over the black
        if (g.State == Game.GameState.Play) SetBlack(1f);
        g.CardTitle = s.Place;
        g.CardSub = $"{Names.Class(_cls)}　·　等級 {level}{(_featured != null ? "　·　對決 " + _featured : "")}";
        g.CardTip = _attract ? "展示模式　·　按任意鍵返回" : "展示模式　·　按任意鍵返回主選單";
        Log.Info("demo", $"scene {_scenes}: {s.Kind} {s.Area} ({s.Place}, look {s.Theme}) class={Names.ClassKey(_cls)} lv{level} for {_sceneLen:0}s");
        g.DemoSession(d, s.Area);
        var h = g.Hero;
        if (h == null || h.D != d) { Log.Error("demo", "the demo character did not take over"); Stop("error"); return; }
        Log.Guard("demo", () => Game.AutoBuild(h));
        Log.Guard("demo", () => Gear(h));
        Log.Guard("demo", () => DemoGolem(h));               // [golem] Soulbinder scenes show each golem kind (DemoMode.Golem.cs)
        Game.EnsureGod(h);
        _seenClasses.Add(Names.ClassKey(_cls));
        _seenAreas.Add(s.Area);
        _lastScene = s;
        SetCaption(s.Place, null);
    }

    /// <summary>Legendary and unique gear (the weapon always one of the two, so it shows on the model), the rest rare to unique.</summary>
    private void Gear(Hero h)
    {
        var d = h.D;
        int power = Math.Clamp(d.Level * 10 + 40, 10, ItemDb.MaxPower);
        ItemKind[] kinds = { ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.Ring, ItemKind.Ring };
        EquipSlot[] slots = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Pants, EquipSlot.Boots, EquipSlot.Amulet, EquipSlot.Ring1, EquipSlot.Ring2 };
        int legend = 0;
        // the hands: of the styles this class can show (two-hand, dual-wield, one-hand + off-hand), the one the show has used
        // least so far, so a demo run passes through every kind of loadout
        var styles = ItemDb.HandStyles(d.Class);
        int least = styles.Min(s => StyleShown.GetValueOrDefault(s));
        var pickFrom = styles.Where(s => StyleShown.GetValueOrDefault(s) == least).ToList();
        string style = pickFrom[_rng.Next(pickFrom.Count)];
        StyleShown[style] = StyleShown.GetValueOrDefault(style) + 1;
        bool first = true;
        var (mh, oh) = ItemDb.RollHands(d.Class, power, style,
            () => { var r = first ? (_rng.NextDouble() < 0.5 ? Rarity.Unique : Rarity.Legendary) : _rng.NextDouble() < 0.5 ? Rarity.Rare : Rarity.Legendary; first = false; return r; }, _rng);
        foreach (var w in new[] { mh, oh }) if (w != null) { w.ReqLevel = Math.Min(w.ReqLevel, d.Level); if (w.Rarity >= Rarity.Legendary) legend++; }
        d.Equip[(int)EquipSlot.MainHand] = mh;
        d.Equip[(int)EquipSlot.OffHand] = oh;
        for (int i = 0; i < kinds.Length; i++)
        {
            Rarity r = _rng.NextDouble() switch { < 0.35 => Rarity.Rare, < 0.75 => Rarity.Legendary, _ => Rarity.Unique };
            Item it;
            try { it = ItemDb.Generate(power, r, d.Class, kinds[i], _rng); }
            catch (Exception) { it = ItemDb.Generate(power, Rarity.Rare, d.Class, kinds[i], _rng); }
            if (it == null) continue;
            it.ReqLevel = Math.Min(it.ReqLevel, d.Level);
            d.Equip[(int)slots[i]] = it;
            if (it.Rarity >= Rarity.Legendary) legend++;
        }
        h.Recalc();
        Log.Info("demo", $"gear: {legend}/{8 + (oh != null ? 2 : 1)} legendary or unique, style {style}: main {mh?.Base} {mh?.Name} ({mh?.Rarity}), off {oh?.Base ?? "-"} {oh?.Name}; life={h.MaxLife:0}");
    }

    /// <summary>How often each hand style (2h / dual / off / 1h) has been shown in this run of the demo.</summary>
    private static readonly Dictionary<string, int> StyleShown = new();

    /// <summary>The new level is up: place the hero for the show, wake the place, name it in the caption.</summary>
    private void Stage(Game g, Level lv)
    {
        _stagedLevel = lv;
        _staged = true;
        _spawned = 0;
        _waveT = 2.5; _lootT = 6.0 + _rng.NextDouble() * 4.0; _zoomT = 5.0;
        if (g.Hud != null) g.Hud.Lean = true;
        var s = _scene;
        var p = g.Player;
        _seenThemes.Add(lv.Theme?.Id ?? s?.Theme ?? "?");
        _lastTheme = s?.Theme;
        try
        {
            if (s != null && s.Kind == Kind.StoryBoss && lv.BossMon != null && IsInstanceValid(lv.BossMon))
            {
                if (lv.DoorSealed) lv.OpenBossDoor(true);
                var grid = lv.Grid;
                Vector2 at = grid.RandomWalkNear(lv.ArenaPos, lv.ArenaRadius * 0.45f, lv.ArenaRadius * 0.75f, lv.Rand);
                p.Stop(); p.Teleport(at);
                g.Cam?.Snap();
                Wave(g, lv, true);
            }
            else if (s != null && s.Kind == Kind.Named) Wave(g, lv, true);
            else
            {
                if (s != null && s.Kind == Kind.Town)
                {
                    lv.SpawnRegion ??= ZoneDb.Regions[_rng.Next(ZoneDb.Regions.Count)];
                    lv.MonsterLevel = Math.Max(lv.MonsterLevel, g.Hero.D.Level);
                }
                // away from the gate the hero arrives by (its arch would stand between the camera and the fight)
                Vector2 spot = OpenGround(lv, p.Pos2);
                if (spot.DistanceTo(p.Pos2) > 4f) { p.Stop(); p.Teleport(spot); g.Cam?.Snap(); }
                Wave(g, lv, true);
                Wave(g, lv, false);
                if (s != null && s.Kind == Kind.Region && _rng.NextDouble() < 0.6) Rare(g, lv);
            }
        }
        catch (Exception e) { Log.Error("demo", $"staging {lv.AreaId} failed", e); }

        // the star of the scene: the boss, a named one, else the strongest elite about
        Monster star = lv.BossMon != null && IsInstanceValid(lv.BossMon) && !lv.BossMon.Dead ? lv.BossMon : null;
        if (star == null && _rareMon != null && IsInstanceValid(_rareMon)) star = _rareMon;
        if (star == null)
        {
            Vector2 me = p.Pos2;
            foreach (var m in World.Monsters)
                if (m != null && IsInstanceValid(m) && !m.Dead && (m.MiniBoss || m.Tier == MonTier.Elite) && m.Pos2.DistanceTo(me) < 40f) { star = m; break; }
        }
        string starName = _scene?.Named?.NameZh ?? star?.DisplayName ?? _featured;
        if (star != null && (star.Brain != null || star.MiniBoss)) _seenBosses.Add(starName ?? star.Def?.Id ?? "?");
        SetCaption(s?.Place ?? ZoneDb.AreaName(lv.AreaId), starName);
        Log.Info("demo", $"staged {lv.AreaId}: theme={lv.Theme?.Id} monsterLevel={lv.MonsterLevel} monsters={World.Monsters.Count} star={starName ?? "-"} | {Report()}");
        _rareMon = null;
    }

    private Monster _rareMon;

    /// <summary>A named rare of the region (or any) beside the hero.</summary>
    private void Rare(Game g, Level lv)
    {
        var all = NamedDb.Of(NamedKind.Rare).ToList();
        if (all.Count == 0) return;
        var mine = all.Where(n => lv.Region != null && n.Region == lv.Region.Id).ToList();
        var n = (mine.Count > 0 ? mine : all)[_rng.Next(mine.Count > 0 ? mine.Count : all.Count)];
        Vector2 at = Around(lv, g.Player.Pos2, 9f, 13f);
        var e = NamedHub.Spawn(n, lv, at, NamedHub.LevelFor(n, lv.MonsterLevel), at, 16f, pack: lv.NextPack());
        if (e?.M == null) return;
        e.M.AiWakeNow();
        _rareMon = e.M;
        _spawned++;
        lv.AddMarker("boss", n.NameZh, at, e.M, true);
        Log.Info("demo", $"named rare {n.Id} ({n.NameZh}) out beside the hero");
    }

    /// <summary>A walkable spot 12–24 m from the arrival, at least 9 m from every gate, portal and waypoint, with room round it.</summary>
    private Vector2 OpenGround(Level lv, Vector2 from)
    {
        var grid = lv.Grid;
        Vector2 best = from; float bestScore = float.MinValue;
        for (int i = 0; i < 24; i++)
        {
            Vector2 q = grid.RandomWalkNear(from, 12f, 24f, lv.Rand);
            if (!grid.WalkAt(q)) continue;
            float gate = 99f;
            foreach (var it in Interactable.All)
            {
                if (it == null || !IsInstanceValid(it) || !it.IsInsideTree()) continue;
                string k = it.Kind ?? "";
                if (k is "exit" or "entrance" or "portal" or "gate" or "waypoint" or "beacon") gate = Mathf.Min(gate, it.Pos2.DistanceTo(q));
            }
            int room = 0;
            for (int a = 0; a < 8; a++)
            {
                Vector2 dir = new(Mathf.Cos(a * Mathf.Pi / 4f), Mathf.Sin(a * Mathf.Pi / 4f));
                if (grid.WalkAt(q + dir * 5f)) room++;
            }
            float score = Mathf.Min(gate, 14f) + room * 1.5f;
            if (score > bestScore) { bestScore = score; best = q; }
        }
        return best;
    }

    private Vector2 Around(Level lv, Vector2 me, float rMin, float rMax)
    {
        var grid = lv.Grid;
        for (int i = 0; i < 8; i++)
        {
            float a = (float)(_rng.NextDouble() * Math.PI * 2.0), r = rMin + (float)_rng.NextDouble() * (rMax - rMin);
            Vector2 q = grid.NearestWalk(me + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            if (grid.WalkAt(q) && q.DistanceTo(me) > rMin * 0.5f) return q;
        }
        return grid.RandomWalkNear(me, rMin * 0.5f, rMax, lv.Rand);
    }

    /// <summary>Keeps the fight around the hero thick: packs, champions and affixed elites, all of them coming.</summary>
    private void Wave(Game g, Level lv, bool first)
    {
        var p = g.Player;
        if (p == null || !IsInstanceValid(p) || lv.Grid == null) return;
        Vector2 me = p.Pos2;
        int near = 0;
        foreach (var m in World.Monsters)
            if (m != null && IsInstanceValid(m) && !m.Dead && m.Faction == Faction.Monster && m.Pos2.DistanceSquaredTo(me) < 28f * 28f) near++;
        bool boss = _scene != null && _scene.Kind is Kind.StoryBoss or Kind.Named;
        int want = boss ? 8 : 18;
        if (!first && (near >= want || _spawned >= 140 || World.Monsters.Count > 160)) return;
        var made = new List<Monster>();
        double roll = _rng.NextDouble();
        if (boss)
        {
            if (roll < 0.6) Populate.Pack(lv, Around(lv, me, 7f, 11f), 6 + _rng.Next(4), false, made);
            else Populate.Elite(lv, Around(lv, me, 7f, 11f), 4, false, made);
        }
        else if (first)
        {
            Populate.Pack(lv, Around(lv, me, 8f, 13f), 8 + _rng.Next(5), false, made);
            Populate.Pack(lv, Around(lv, me, 10f, 15f), 7 + _rng.Next(5), false, made);
            Populate.Elite(lv, Around(lv, me, 9f, 14f), 4, false, made);
        }
        else if (roll < 0.45) Populate.Pack(lv, Around(lv, me, 10f, 15f), 7 + _rng.Next(6), false, made);
        else if (roll < 0.75) Populate.Champions(lv, Around(lv, me, 10f, 15f), 3 + _rng.Next(3), made);
        else Populate.Elite(lv, Around(lv, me, 10f, 15f), 3 + _rng.Next(3), false, made);
        foreach (var m in made) if (m != null && IsInstanceValid(m)) { m.AiWakeNow(); _spawned++; }
    }

    /// <summary>A burst of items of every rarity round the hero: labels and beams of all colours.</summary>
    private void LootShower(Game g, Level lv)
    {
        var p = g.Player; var h = g.Hero;
        if (p == null || h == null || lv.Grid == null) return;
        int items = 0;
        foreach (var it in Interactable.All) if (it != null && IsInstanceValid(it) && it.Item != null) items++;
        if (items > 30) return;
        Rarity[] rs = { Rarity.Common, Rarity.Magic, Rarity.Magic, Rarity.Rare, Rarity.Rare, Rarity.Legendary, Rarity.Legendary, Rarity.Unique };
        int power = Math.Clamp(h.D.Level * 10, 10, ItemDb.MaxPower);
        Vector2 me = p.Pos2;
        for (int i = 0; i < rs.Length; i++)
        {
            Item it;
            try { it = ItemDb.Generate(power, rs[i], h.Class, null, _rng); }
            catch (Exception) { it = ItemDb.Generate(power, Rarity.Rare, h.Class, null, _rng); }
            if (it == null) continue;
            Loot.DropItem(it, Around(lv, me, 2.2f, 5.0f));
        }
        Loot.DropGold(40 + h.D.Level * 12, Around(lv, me, 1.5f, 3.5f));
        Log.Debug("demo", "loot shower");
    }

    private string Report() =>
        $"scenes={_scenes} classes={_seenClasses.Count}/8 [{string.Join(",", _seenClasses)}] looks={_seenThemes.Count} [{string.Join(",", _seenThemes)}] " +
        $"areas={_seenAreas.Count} bosses={_seenBosses.Count} [{string.Join(",", _seenBosses)}] errors={Log.ErrorCount}";

    // ───────────────────────── overlay ─────────────────────────

    private void BuildOverlay()
    {
        _layer = new CanvasLayer { Name = "DemoLayer", Layer = 80 };          // over the HUD and panels, under the loading card (90)
        AddChild(_layer);
        _black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _layer.AddChild(_black);
        _black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 2);
        _layer.AddChild(box);
        box.AnchorLeft = 0f; box.AnchorRight = 0f; box.AnchorTop = 1f; box.AnchorBottom = 1f;
        box.OffsetLeft = 40f; box.OffsetRight = 1100f; box.OffsetTop = -118f; box.OffsetBottom = -30f;
        box.GrowVertical = Control.GrowDirection.Begin;
        _caption = UiTheme.Outlined(UiTheme.Label("", 30, new Color(0.96f, 0.86f, 0.66f), true), 7);
        box.AddChild(_caption);
        var rule = new ColorRect { Color = new Color(1f, 0.5f, 0.15f, 0.7f), CustomMinimumSize = new Vector2(320, 2), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin, MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddChild(rule);
        _hint = UiTheme.Outlined(UiTheme.Small(_attract ? "展示模式　·　按任意鍵返回" : "展示模式　·　按任意鍵返回主選單", 20, new Color(0.82f, 0.76f, 0.66f)), 5);
        box.AddChild(_hint);
    }

    private void SetCaption(string place, string star)
    {
        if (_caption == null) return;
        string cls = Names.Class(_cls);
        _caption.Text = string.IsNullOrEmpty(star) ? $"{place}・{cls}" : $"{place}・{cls} 對 {star}";
    }

    private void SetBlack(float a)
    {
        if (_black == null) return;
        _blackOn = a > 0.001f;
        _black.Visible = _blackOn;
        _black.Color = new Color(0f, 0f, 0f, a);
    }
}
