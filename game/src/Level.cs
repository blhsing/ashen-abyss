using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>Something worth showing on the minimap and the full map.</summary>
public sealed class MapMarker { public string Kind, Label; public Vector2 Pos; public bool Visible = true; public object Ref; }

/// <summary>
/// One loaded area: the town, a region's overworld, a dungeon or a rift. Owns the generated layout, the fog of war and
/// map markers, and runs everything that makes the place playable — objectives, the sealed boss door and the boss
/// fight, the stronghold and its beacon, world events, the rift timer. Monsters and objects are placed by
/// <see cref="Populate"/>; the visible world is built by LevelBuilder.
/// </summary>
public partial class Level : Node3D
{
    // ── contract (ARCHITECTURE.md 3.4) ──
    public string AreaId; public RegionDef Region; public DungeonDef Dungeon; public bool IsTown, IsRift; public int RiftTier, MonsterLevel;
    public LevelData Data; public NavGrid Grid; public LevelTheme Theme; public WorldEnvironment Env;
    public Vector2 Checkpoint;
    public string ObjectiveText = ""; public float ObjectiveProgress; public bool ObjectiveDone;
    public Monster Boss; public event Action Changed;
    public bool[] Seen; public List<MapMarker> Markers = new();
    public float RiftTimeLeft, RiftProgress;

    // ── shared with Populate and WorldObjects ──
    /// <summary>The level actors are currently spawned into (set before population starts, unlike Game.I.Level).</summary>
    public static Level Current => World.Root as Level;
    public const float RiftTime = 600f;
    public RegionDef SpawnRegion;                    // whose monsters live here (a rift borrows a region)
    public Random Rand = new();
    public int Seed;
    public readonly List<NpcRef> Npcs = new();
    public readonly List<WorldEvent> Events = new();
    public readonly List<string> Loops = new();      // looping sounds started for this level, stopped when it unloads
    public readonly List<Monster> StrongholdMobs = new();
    public Interactable Beacon; public bool Liberated; public Vector2 StrongholdPos; public float StrongholdRadius;
    public DungeonGoal Goal; public readonly List<Monster> ObjMonsters = new();
    public int ObjTotal, ObjCount, Embers, EmbersNeeded; public bool EmbersOffered;
    public Monster BossMon; public Vector2 ArenaPos; public float ArenaRadius = 12f; public bool BossIsStory, HasArena;
    public float RiftPoints, RiftNeed = 30f, RiftMul = 1f; public bool RiftFailed;
    public bool Built { get; private set; }
    public bool BossDead => _bossDead;
    // ── save resume (LevelState) ──
    /// <summary>The checkpoint the level was populated with (spawns keep clear of it; a resumed save rebuilds with the same one).</summary>
    public Vector2 BuildCheckpoint;
    /// <summary>Where the boss fell (the exit portal stands beside it).</summary>
    public Vector2 BossFellAt;
    /// <summary>Reward chests that arrived during play (boss, events, rifts) with their quality.</summary>
    public readonly List<(Interactable chest, int quality)> RewardChests = new();
    public bool DoorOpened => _doorCells != null && _doorOpen;
    public bool LiberatedAtBuild;
    /// <summary>
    /// Where a dungeon or rift stands. <see cref="ObjectiveDone"/> alone only says the door is open: the boss may still
    /// be alive. Objective = the task is still open (the door is sealed); Boss = the task is done and the boss or rift
    /// guardian is still to be killed; Cleared = the boss is dead, the place is done (chest and exit portal are out).
    /// Town and overworld are always None.
    /// </summary>
    public enum DungeonPhase { None, Objective, Boss, Cleared }
    public DungeonPhase Phase =>
        Dungeon == null && !IsRift ? DungeonPhase.None
        : _bossDead ? DungeonPhase.Cleared
        : ObjectiveDone ? (BossMon != null && IsInstanceValid(BossMon) && !BossMon.Dead ? DungeonPhase.Boss : DungeonPhase.Cleared)
        : DungeonPhase.Objective;
    /// <summary>Objective done, boss (or rift guardian) still standing.</summary>
    public bool BossPending => Phase == DungeonPhase.Boss;
    /// <summary>The dungeon or rift is finished: its boss is dead.</summary>
    public bool Cleared => Phase == DungeonPhase.Cleared;
    public bool DoorSealed => _doorCells != null && !_doorOpen;
    public Vector2 DoorOutside => _doorOutside;

    private sealed class Timed { public float T; public Action A; }
    private string _entry;
    private bool _dirty, _bossDead, _engaged, _doorOpen;
    private float _revealT, _slowT, _clock;
    private int _errors, _packId;
    private Vector2 _lastReveal = new(float.MaxValue, float.MaxValue);
    private readonly List<MapMarker> _hidden = new();
    private readonly List<Action> _deferred = new();
    private readonly List<Timed> _timers = new();
    private readonly HashSet<ulong> _riftScaled = new();
    private List<Vector2I> _doorCells;
    private readonly List<Node3D> _doorVisuals = new();
    private Vector2 _doorPos, _doorOutside;
    private MapMarker _doorMarker;
    // channelled interaction (toppling an idol)
    private Interactable _chanObj; private float _chanT, _chanDur; private Vector2 _chanFrom; private Action _chanDone; private string _chanText, _chanSub; private Node3D _chanFx;
    // where the hero last left an area, so a return portal can put them back
    private static string _leftArea; private static Vector2 _leftPos; private static int _leftSeed;
    private static bool _globalsReady;
    private static readonly StringName HeroPos = "hero_pos";

    private static readonly string[] HallThemes = { "crypt", "icehall", "brasshall", "sandtomb", "cathedral", "forge" };
    private static readonly string[] CaveThemes = { "cave", "icecave", "wetcave", "lavacave" };

    // ───────────────────────── building ─────────────────────────

    public static Level Build(string areaId, string entry, int seed)
    {
        EnsureShaderGlobals();
        var lv = new Level { Name = "Level", Seed = seed, _entry = entry, Rand = new Random(unchecked(seed * 31 + 17)) };
        var req = lv.Resolve(string.IsNullOrEmpty(areaId) ? ZoneDb.Town : areaId, seed);

        LevelData data = null;
        Exception last = null;
        for (int attempt = 0; attempt < 3 && data == null; attempt++)
        {
            try
            {
                req.Seed = unchecked(seed + attempt * 7919);
                data = LevelGen.Generate(req);
                if (data != null && data.Grid == null) data = null;
            }
            catch (Exception e) { last = e; Log.Error("level", $"generation failed for {req.AreaId} (seed {req.Seed})", e); }
        }
        if (data == null) throw new InvalidOperationException($"level generation failed for '{req.AreaId}'", last);

        if (World.Root is Level old && old != lv) old.RememberDeparture();
        if (World.Root != null && World.Root != lv) World.Clear();      // the previous level's actors go with it
        lv.Data = data; lv.Grid = data.Grid;
        World.Grid = lv.Grid; World.Root = lv;
        try { lv.Theme = LevelThemes.Get(req.ThemeId) ?? LevelThemes.ForArea(lv.AreaId); }
        catch (Exception e) { Log.Error("level", $"theme '{req.ThemeId}' unavailable", e); }
        if (lv.Theme != null && !string.IsNullOrEmpty(lv.Theme.Footstep)) World.FootstepSfx = lv.Theme.Footstep;
        lv.Seen = new bool[lv.Grid.W * lv.Grid.H];

        int heroLevel = Game.I?.Hero?.D.Level ?? 1;
        int tier = Math.Clamp(Game.I?.WorldTier ?? 0, 0, Balance.TierLevelBonus.Length - 1);
        lv.MonsterLevel = lv.IsTown ? 1 : Balance.AreaLevel(lv.IsRift ? 1 : lv.Region?.Level ?? 1, heroLevel, tier, lv.Dungeon != null || lv.IsRift);
        lv.RiftMul = lv.IsRift ? MathF.Pow(1.12f, lv.RiftTier) : 1f;
        lv.RiftTimeLeft = lv.IsRift ? RiftTime : 0f;
        lv.Goal = req.Goal;
        lv.Checkpoint = lv.EntryPos(entry);
        lv.BuildCheckpoint = lv.Checkpoint;
        LevelState.BeforePopulate(lv);                    // a resumed save: the monster level and checkpoint of the saved level
        Log.Info("level", $"build area={lv.AreaId} kind={req.Kind} theme={req.ThemeId} size={req.Size} seed={req.Seed} grid={lv.Grid.W}x{lv.Grid.H} " +
            $"mlvl={lv.MonsterLevel} tier={tier} entry={entry ?? "-"} pois={data.Pois?.Count ?? 0} spawns={data.Spawns?.Count ?? 0} props={data.Props?.Count ?? 0}");

        // Monsters, effects and interactables need the scene tree while they are created, so the level joins it here
        // (under Game.WorldRoot) and builds itself in _Ready. Without a world root yet, it builds when it is added.
        var parent = Game.I?.WorldRoot;
        if (parent != null && IsInstanceValid(parent) && lv.GetParent() == null) parent.AddChild(lv);
        return lv;
    }

    private static void EnsureShaderGlobals()
    {
        if (_globalsReady) return;
        _globalsReady = true;
        // hero_pos is declared in project.godot ([shader_globals]); nothing to register at runtime.
    }

    private static AreaKind KindOf(string theme) => Array.IndexOf(CaveThemes, theme) >= 0 ? AreaKind.Cave : AreaKind.Halls;

    private static string RiftRegionOf(string theme) => theme switch
    {
        "crypt" => "crypt", "icehall" or "icecave" => "frost", "brasshall" or "sandtomb" => "sands", "cathedral" => "sanctum",
        "forge" or "lavacave" => "abyss", "wetcave" => "mire", _ => "wastes",
    };

    /// <summary>Works out what kind of place an area id names and what to ask the generator for.</summary>
    private LevelRequest Resolve(string areaId, int seed)
    {
        var r = new LevelRequest { AreaId = areaId, Seed = seed, Goal = DungeonGoal.None };
        if (NamedAreas.Resolve(this, areaId, seed) is { } named) { AreaId = named.AreaId; return named; }      // NAMED-HOOK: encounter arenas (rift~arena / rift~wb / rift~pin)
        var rng = new Random(seed);
        if (areaId.StartsWith("rift"))
        {
            IsRift = true;
            int tier = 1, colon = areaId.IndexOf(':');
            if (colon >= 0) int.TryParse(areaId[(colon + 1)..], out tier);
            RiftTier = Math.Max(1, tier);
            r.AreaId = "rift:" + RiftTier;
            string theme = rng.Next(10) < 6 ? HallThemes[rng.Next(HallThemes.Length)] : CaveThemes[rng.Next(CaveThemes.Length)];
            SpawnRegion = ZoneDb.Region(RiftRegionOf(theme)) ?? ZoneDb.Regions[rng.Next(ZoneDb.Regions.Count)];
            r.ThemeId = theme; r.Kind = KindOf(theme); r.Size = 64 + rng.Next(0, 13); r.Boss = true;
        }
        else if (ZoneDb.Dungeon(areaId) is { } d)
        {
            Dungeon = d; Region = ZoneDb.RegionOf(areaId); SpawnRegion = Region;
            r.ThemeId = d.Theme; r.Goal = d.Goal; r.Boss = true;
            if (d.Theme == "graveyard") { r.Kind = AreaKind.Overworld; r.Size = 56; }       // a small outdoor field with the boss arena
            else { r.Kind = KindOf(d.Theme); r.Size = (d.Story ? 68 : 60) + rng.Next(0, 13); }
        }
        else if (ZoneDb.Region(areaId) is { } reg)
        {
            Region = reg; SpawnRegion = reg;
            r.ThemeId = reg.Theme; r.Kind = AreaKind.Overworld; r.Size = 110; r.Stronghold = true;
            r.DungeonIds = reg.Dungeons.Select(x => x.Id).ToArray();
        }
        else
        {
            if (areaId != ZoneDb.Town) Log.Warn("level", $"unknown area '{areaId}', loading the town instead");
            IsTown = true;
            r.AreaId = ZoneDb.Town; r.ThemeId = "town"; r.Kind = AreaKind.Town; r.Size = 50;
        }
        AreaId = r.AreaId;
        return r;
    }

    public override void _EnterTree()
    {
        World.Event += OnWorldEvent;
        World.MonsterKilled += OnMonsterKilled;
    }

    public override void _Ready()
    {
        if (Built || Data == null) return;
        Built = true;
        Log.Guard("level", () => LevelBuilder.Build(this, Data, Theme));
        Log.Guard("level", OpenPropBlockages);            // before objectives are placed: their spots must see the real routes
        Log.Guard("level", () => Populate.Run(this));
        Log.Guard("level", OpenPropBlockages);            // objects added by Populate (chests, altars, totems) can block too
        if (IsRift) ScaleRiftMonsters();
        Log.Guard("levelstate", () => LevelState.OnPopulated(this));      // records the placement; a resumed save puts its state back
        UpdateObjective();
        var p = World.P;
        if (p != null && IsInstanceValid(p)) RenderingServer.GlobalShaderParameterSet(HeroPos, p.GlobalPosition);
        Reveal(Checkpoint, RevealRadius);
        // home is known ground: the map of the town shows every street and service from the first visit (its markers were
        // drawn over black fog until the hero had walked past each of them)
        if (IsTown && Grid != null) Reveal(Grid.Center(Grid.W / 2, Grid.H / 2), (Grid.W + Grid.H) * NavGrid.Cell);
        _dirty = true;
        Log.Info("level", $"ready area={AreaId} monsters={World.Monsters.Count} interactables={Interactable.All.Count} markers={Markers.Count} " +
            $"goal={Goal} objective='{ObjectiveText}' door={(DoorSealed ? "sealed" : "open")} boss={(BossMon != null ? BossMon.Def.Id : "-")}");
    }

    public override void _ExitTree()
    {
        World.Event -= OnWorldEvent;
        World.MonsterKilled -= OnMonsterKilled;
        foreach (var key in Loops) Snd.StopLoop(key);
        Loops.Clear();
        if (World.Root == this)
        {
            RememberDeparture();
            World.Clear();
            Log.Info("level", $"unloaded area={AreaId}");
        }
    }

    private void RememberDeparture()
    {
        var p = World.P;
        if (IsTown || p == null || !IsInstanceValid(p) || !p.IsInsideTree()) return;
        _leftArea = AreaId; _leftPos = p.Pos2; _leftSeed = Seed;
    }

    // ───────────────────────── positions ─────────────────────────

    private float RevealRadius => Theme != null && Theme.Outdoor ? 26f : 20f;

    private Vector2 StartPos()
    {
        var s = Data?.Find("start");
        if (s != null) return s.Pos;
        if (Grid == null) return Vector2.Zero;
        var c = Grid.NearestWalkCell(new Vector2I(Grid.W / 2, Grid.H / 2), Math.Max(Grid.W, Grid.H));
        return Grid.Center(c);
    }

    /// <summary>
    /// Where the hero appears: the entrance or exit that leads to the area they came from, the waypoint for
    /// "waypoint", the spot they left for "return" (a town portal back into the same layout), otherwise the start.
    /// </summary>
    public Vector2 EntryPos(string entry)
    {
        if (Data == null || Grid == null) return Vector2.Zero;
        Vector2 start = StartPos();
        if (string.IsNullOrEmpty(entry)) return start;
        if (entry == "return") return _leftArea == AreaId && _leftSeed == Seed && Grid.WalkAt(_leftPos) ? _leftPos : start;
        Poi poi = null;
        if (entry == "waypoint") poi = Data.Find("waypoint");
        else
        {
            foreach (var p in Data.All("entrance")) if (p.Tag == entry) { poi = p; break; }
            if (poi == null) foreach (var p in Data.All("exit")) if (p.Tag == entry) { poi = p; break; }
        }
        return poi == null ? start : StandNear(poi.Pos, start, entry == "waypoint" ? 3.4f : 2.8f);
    }

    /// <summary>A walkable spot <paramref name="dist"/> metres from a point, on the side facing <paramref name="toward"/>.</summary>
    public Vector2 StandNear(Vector2 pos, Vector2 toward, float dist = 2.6f)
    {
        if (Grid == null) return pos;
        Vector2 best = pos; float bd = float.MaxValue;
        for (int ring = 0; ring < 2 && bd == float.MaxValue; ring++)
        {
            float d = dist + ring * 1.4f;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.Tau / 12f;
                var q = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (!Grid.WalkAt(q) || !Grid.LineWalk(pos, q)) continue;
                float dd = q.DistanceSquaredTo(toward);
                if (dd < bd) { bd = dd; best = q; }
            }
        }
        return best;
    }

    private int[] _ccMark; private int[] _ccQueue; private int _ccStamp;

    private int ReachableFrom(Vector2I start, Vector2I blocked, bool props = false, bool doorOpen = true)
    {
        int w = Grid.W, n = w * Grid.H;
        if (_ccMark == null || _ccMark.Length != n) { _ccMark = new int[n]; _ccQueue = new int[n]; _ccStamp = 0; }
        _ccStamp++;
        if (!Grid.Walk(start.X, start.Y) || start == blocked) return 0;
        int head = 0, tail = 0, bi = Grid.In(blocked.X, blocked.Y) ? blocked.Y * w + blocked.X : -1;
        int si = start.Y * w + start.X;
        _ccQueue[tail++] = si; _ccMark[si] = _ccStamp;
        while (head < tail)
        {
            int ci = _ccQueue[head++];
            int cx = ci % w, cy = ci / w;
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);
                // a sealed boss door counts as open: what stands in front of it must not block it once it opens
                if (!Grid.Walk(nx, ny) && !(doorOpen && DoorSealed && _doorCells.Contains(new Vector2I(nx, ny)))) continue;
                if (props && Grid.Solid(nx, ny)) continue;               // a big prop the hero's route must go round
                int ni = ny * w + nx;
                if (ni == bi || _ccMark[ni] == _ccStamp) continue;
                _ccMark[ni] = _ccStamp;
                _ccQueue[tail++] = ni;
            }
        }
        return tail;
    }

    /// <summary>
    /// False when an object standing on this cell would cut the level in two (a one-cell passage, a doorway): such an
    /// object must not block, or whatever lies behind it — an idol, the boss door — could never be reached.
    /// </summary>
    private float _doorHintT;

    /// <summary>
    /// Walking up to the sealed boss gate says what opens it (once every 12 s while the hero stays near), so a closed
    /// gate never looks like a bug.
    /// </summary>
    private void TickDoorHint(float dt, Actor p)
    {
        _doorHintT -= dt;
        if (!DoorSealed || _doorHintT > 0f || p.Pos2.DistanceTo(_doorOutside) > 5f) return;
        _doorHintT = 12f;
        string need;
        if (Goal == DungeonGoal.CollectEmbers)
            need = Embers >= EmbersNeeded ? "將燼晶獻於門旁的祭壇" : $"{ZoneDb.GoalText[(int)Goal]}（{Embers}/{EmbersNeeded}）";
        else if (Goal != DungeonGoal.None && ObjTotal > 0)
            need = $"{ZoneDb.GoalText[(int)Goal]}（{Mathf.Min(ObjCount, ObjTotal)}/{ObjTotal}）";
        else return;
        Game.I?.Hud?.Toast($"封印之門緊閉——{need}後開啟", Palette.Gold);
        Log.Info("level", $"sealed-gate hint shown: {need}");
    }

    /// <summary>
    /// The walkable cell nearest to <paramref name="pos"/> (within <paramref name="maxCells"/>) that the hero can walk
    /// to from the start right now — with the boss door as it is, so a room that is only reachable through the
    /// sealed arena does not count. False when there is none.
    /// </summary>
    public bool ReachableSpot(Vector2 pos, int maxCells, out Vector2 spot)
    {
        spot = pos;
        if (Grid == null) return true;
        var start = Grid.NearestWalkCell(Grid.CellOf(StartPos()));
        ReachableFrom(start, new Vector2I(-1, -1), props: true, doorOpen: false);
        var c0 = Grid.CellOf(pos);
        int best = -1; float bd = float.MaxValue;
        for (int dy = -maxCells; dy <= maxCells; dy++)
            for (int dx = -maxCells; dx <= maxCells; dx++)
            {
                int x = c0.X + dx, y = c0.Y + dy;
                if (x < 0 || y < 0 || x >= Grid.W || y >= Grid.H || !Grid.Walk(x, y)) continue;
                int i = y * Grid.W + x;
                if (_ccMark[i] != _ccStamp) continue;
                float d = dx * dx + dy * dy;
                if (d < bd) { bd = d; best = i; }
            }
        if (best < 0) return false;
        spot = bd == 0 ? pos : Grid.Center(best % Grid.W, best / Grid.W);
        return true;
    }

    /// <summary>
    /// After the level is built and populated: every walkable cell that the hero could reach if there were no props
    /// must be reachable with them. A big prop (a boulder, a statue) that closes a passage one cell wide becomes
    /// walk-through; the cells are opened one at a time, always the prop at the edge of the reachable area that
    /// lets the most new floor through, so the fewest possible props lose their collision.
    /// </summary>
    public void OpenPropBlockages()
    {
        if (Grid == null) return;
        int w = Grid.W, n = w * Grid.H;
        var start = Grid.NearestWalkCell(Grid.CellOf(StartPos()));
        int opened = 0;
        // 1. small props in a passage one cell wide: route planning ignores them, but they leave less room than an actor
        //    needs (a 2 m corridor minus a 0.7 m rock), so the hero is sent down a route his body does not fit through.
        int narrowed = 0;
        for (int y = 0; y < Grid.H; y++)
            for (int x = 0; x < Grid.W; x++)
            {
                if (!Grid.Walk(x, y) || Grid.Clear(x, y)) continue;
                // two or more walled sides: a corridor, a bend or a dead-end nook (in a room corner this only makes a
                // rock walk-through, which costs nothing)
                int walls = (Grid.Walk(x - 1, y) ? 0 : 1) + (Grid.Walk(x + 1, y) ? 0 : 1) + (Grid.Walk(x, y - 1) ? 0 : 1) + (Grid.Walk(x, y + 1) ? 0 : 1);
                if (walls < 2) continue;
                int n0 = Grid.ClearProps(x, y);
                if (n0 == 0) continue;
                narrowed++;
                var c = Grid.Center(x, y);
                Log.Info("level", $"{AreaId}: {n0} prop(s) in the narrow passage at ({c.X:0},{c.Y:0}) are walk-through now");
            }
        if (narrowed > 0) Log.Info("level", $"{AreaId}: {narrowed} narrow passage cell(s) cleared of colliding props");
        for (int iter = 0; iter < 64; iter++)
        {
            ReachableFrom(start, new Vector2I(-1, -1));
            var all = (int[])_ccMark.Clone(); int allStamp = _ccStamp;
            int with = ReachableFrom(start, new Vector2I(-1, -1), props: true);
            int withStamp = _ccStamp;
            // floor the hero could stand on (not a prop cell) that is reachable only when props are ignored
            int missing = 0;
            for (int i = 0; i < n; i++)
                if (all[i] == allStamp && _ccMark[i] != withStamp && !Grid.Solid(i % w, i / w)) missing++;
            if (missing == 0) break;
            // candidate: a solid cell touching the reachable area and touching unreachable floor
            int best = -1;
            for (int i = 0; i < n && best < 0; i++)
            {
                int x = i % w, y = i / w;
                if (!Grid.Solid(x, y) || all[i] != allStamp) continue;
                bool nearIn = false, nearOut = false;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!Grid.In(nx, ny)) continue;
                    int ni = ny * w + nx;
                    if (_ccMark[ni] == withStamp) nearIn = true;
                    else if (all[ni] == allStamp) nearOut = true;
                }
                if (nearIn && nearOut) best = i;
            }
            if (best < 0) { Log.Warn("level", $"{AreaId}: {missing} floor cell(s) are cut off by props but no single prop could be opened"); break; }
            int bx = best % w, by = best / w;
            Grid.ClearProps(bx, by);
            opened++;
            var at = Grid.Center(bx, by);
            Log.Info("level", $"{AreaId}: prop at ({at.X:0},{at.Y:0}) closed off {missing} floor cell(s); it is walk-through now");
        }
        if (opened > 0) Log.Info("level", $"{AreaId}: {opened} blocking prop(s) made walk-through");
    }

    public bool KeepsConnected(Vector2 pos)
    {
        if (Grid == null) return true;
        var c = Grid.CellOf(pos);
        if (!Grid.Walk(c.X, c.Y)) return true;
        var start = Grid.NearestWalkCell(Grid.CellOf(StartPos()));
        if (start == c) return false;
        int all = ReachableFrom(start, new Vector2I(-1, -1));
        // only cells the hero could reach anyway matter (a sealed arena is judged when its door opens)
        if (_ccMark[c.Y * Grid.W + c.X] != _ccStamp) return true;
        int without = ReachableFrom(start, c);
        return without >= all - 1;
    }

    public int NextPack() => ++_packId;
    public void MarkDirty() => _dirty = true;
    /// <summary>Runs an action on the level's next update (used to keep spawning out of kill callbacks).</summary>
    public void Defer(Action a) { if (a != null) _deferred.Add(a); }
    /// <summary>Runs an action after some unpaused seconds.</summary>
    public void After(float seconds, Action a) { if (a != null) _timers.Add(new Timed { T = seconds, A = a }); }

    // ───────────────────────── fog of war and markers ─────────────────────────

    /// <summary>Uncovers the map around a point. Indoors only what can be seen from there; walls next to open cells come along.</summary>
    public void Reveal(Vector2 pos, float radius)
    {
        if (Grid == null || Seen == null) return;
        var c = Grid.CellOf(pos);
        int r = Mathf.CeilToInt(radius / NavGrid.Cell), w = Grid.W, h = Grid.H;
        float r2 = radius * radius;
        bool los = Theme != null && !Theme.Outdoor && !IsTown;
        var t = Grid.T;
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = c.X + dx, y = c.Y + dy;
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                int i = y * w + x;
                if (Seen[i] || t[i] == NavGrid.Wall) continue;
                var cc = Grid.Center(x, y);
                if (cc.DistanceSquaredTo(pos) > r2) continue;
                if (los && !Grid.LineShot(pos, cc)) continue;
                Seen[i] = true;
                for (int ny = y - 1; ny <= y + 1; ny++)
                    for (int nx = x - 1; nx <= x + 1; nx++)
                        if (nx >= 0 && ny >= 0 && nx < w && ny < h && t[ny * w + nx] == NavGrid.Wall) Seen[ny * w + nx] = true;
            }
    }

    public bool SeenAt(Vector2 pos)
    {
        if (Grid == null || Seen == null) return false;
        var c = Grid.CellOf(pos);
        return Grid.In(c.X, c.Y) && Seen[c.Y * Grid.W + c.X];
    }

    /// <summary>Adds a map marker. Hidden ones appear once the fog over them lifts.</summary>
    public MapMarker AddMarker(string kind, string label, Vector2 pos, object reference = null, bool hiddenUntilSeen = false)
    {
        var m = new MapMarker { Kind = kind, Label = label ?? "", Pos = pos, Ref = reference, Visible = !hiddenUntilSeen };
        Markers.Add(m);
        if (hiddenUntilSeen) _hidden.Add(m);
        _dirty = true;
        return m;
    }

    /// <summary>Removes a marker, given either the marker itself or the object it points at.</summary>
    public void RemoveMarker(object reference)
    {
        if (reference == null) return;
        for (int i = Markers.Count - 1; i >= 0; i--)
        {
            var m = Markers[i];
            if (m != reference && m.Ref != reference) continue;
            Markers.RemoveAt(i);
            _hidden.Remove(m);
            _dirty = true;
        }
    }

    public MapMarker MarkerOf(object reference)
    {
        foreach (var m in Markers) if (m.Ref == reference) return m;
        return null;
    }

    /// <summary>A reward chest that arrives with a flash (boss kills, events, rifts) and gets its own marker.</summary>
    public Interactable RewardChest(Vector2 at, int quality, Action<Vector2> onOpened = null)
    {
        at = Grid != null ? Grid.NearestWalk(at) : at;
        var chest = WorldObjects.Chest(at, quality, 0f, onOpened);
        RewardChests.Add((chest, quality));
        Fx.Burst(World.To3(at, 0.6f), Element.Fire, 2.5f, 0.8f);
        Snd.At("drop_rare", World.To3(at, 0.5f), 0.9f);
        AddMarker("chest", chest.Label, at, chest);
        return chest;
    }

    // ───────────────────────── channelled interaction ─────────────────────────

    /// <summary>Starts a stand-still interaction on an object; moving away, dying or the object vanishing cancels it.</summary>
    public void BeginChannel(Interactable obj, float seconds, string text, Action done)
    {
        var p = World.P;
        if (obj == null || p == null || _chanObj == obj) return;
        CancelChannel();
        p.Stop();
        p.FaceDir(obj.Pos2 - p.Pos2);
        _chanObj = obj; _chanDur = Mathf.Max(0.1f, seconds); _chanT = 0f; _chanFrom = p.Pos2; _chanDone = done; _chanText = text ?? ""; _chanSub = obj.Sub;
        _chanFx = Fx.Telegraph(obj.Pos2, 1.6f, _chanDur, Palette.Ember, true, Sigil.Arcane);
        Snd.At("hex_cast", obj.GlobalPosition, 0.6f);
    }

    private void CancelChannel()
    {
        if (_chanObj == null) return;
        if (IsInstanceValid(_chanObj)) _chanObj.Sub = _chanSub;
        if (_chanFx != null && IsInstanceValid(_chanFx)) _chanFx.QueueFree();
        _chanObj = null; _chanDone = null; _chanFx = null;
    }

    private void TickChannel(float dt, Player p)
    {
        if (_chanObj == null) return;
        if (!IsInstanceValid(_chanObj) || !_chanObj.CanUse || p.Dead || p.Pos2.DistanceTo(_chanFrom) > 0.9f) { CancelChannel(); return; }
        _chanT += dt;
        _chanObj.Sub = $"{_chanText}… {Mathf.Min(99, (int)(_chanT / _chanDur * 100f))}%";
        if (_chanT < _chanDur) return;
        var done = _chanDone;
        _chanFx = null;                 // the telegraph has filled and frees itself
        CancelChannel();
        done?.Invoke();
    }

    // ───────────────────────── frame ─────────────────────────

    public override void _Process(double delta)
    {
        if (Game.I == null || Game.I.Paused) return;
        if (!Built || World.Root != this) return;
        long t0 = Prof.Now;
        try { Tick((float)delta); }
        catch (Exception e) { if (_errors++ < 20) Log.Error("level", "update failed", e); }
        Prof.Level += Prof.Now - t0;
    }

    private void Tick(float dt)
    {
        var p = World.P;
        if (p == null || !IsInstanceValid(p) || !p.IsInsideTree()) return;
        _clock += dt;
        RenderingServer.GlobalShaderParameterSet(HeroPos, p.GlobalPosition);     // occluders cut a see-through disc around the hero

        if (_deferred.Count > 0) RunDeferred();
        if (_timers.Count > 0) RunTimers(dt);

        _revealT -= dt;
        if (_revealT <= 0f || p.Pos2.DistanceSquaredTo(_lastReveal) > 1f)
        {
            _revealT = 0.5f; _lastReveal = p.Pos2;
            Reveal(p.Pos2, RevealRadius);
        }

        TickChannel(dt, p);
        TickNpcs(dt, p);
        for (int i = 0; i < Events.Count; i++) Events[i].Tick(dt);
        TickBoss(p);
        TickDoorHint(dt, p);
        if (IsRift) TickRift(dt);

        _slowT -= dt;
        if (_slowT <= 0f) { _slowT = 0.25f; SlowTick(p); }
        if (_dirty) { _dirty = false; Changed?.Invoke(); }
    }

    private void RunDeferred()
    {
        for (int i = 0; i < _deferred.Count; i++)
        {
            try { _deferred[i](); }
            catch (Exception e) { Log.Error("level", "deferred action failed", e); }
        }
        _deferred.Clear();
    }

    private void RunTimers(float dt)
    {
        for (int i = _timers.Count - 1; i >= 0; i--)
        {
            var t = _timers[i];
            t.T -= dt;
            if (t.T > 0f) continue;
            _timers.RemoveAt(i);
            try { t.A(); }
            catch (Exception e) { Log.Error("level", "timed action failed", e); }
        }
    }

    private void TickNpcs(float dt, Player p)
    {
        if (Npcs.Count == 0) return;
        float k = 1f - Mathf.Exp(-6f * dt);
        for (int i = Npcs.Count - 1; i >= 0; i--)
        {
            var n = Npcs[i];
            if (n.Obj == null || !IsInstanceValid(n.Obj) || n.Model == null) { Npcs.RemoveAt(i); continue; }
            Vector2 to = p.Pos2 - n.Obj.Pos2;
            float d2 = to.LengthSquared();
            if (d2 > 900f) continue;
            float want = d2 < 36f && d2 > 0.04f ? Mathf.Atan2(to.X, to.Y) : n.BaseYaw;      // people turn to whoever walks up to them
            float cur = n.Model.Rotation.Y, next = Mathf.LerpAngle(cur, want, k);
            if (Mathf.Abs(next - cur) > 0.0005f) n.Model.Rotation = new Vector3(0, next, 0);
            if (n.QuestMark != null && n.QuestMark.Visible) n.QuestMark.Position = new Vector3(0, n.MarkY + Mathf.Sin(_clock * 2.4f) * 0.1f, 0);
        }
    }

    private bool _questsBroken;
    private int _shAlive;
    public MapMarker StrongholdMarker;

    private void SlowTick(Player p)
    {
        for (int i = _hidden.Count - 1; i >= 0; i--)
            if (SeenAt(_hidden[i].Pos)) { _hidden[i].Visible = true; _hidden.RemoveAt(i); _dirty = true; }

        if (IsTown && !_questsBroken)
        {
            try
            {
                foreach (var n in Npcs)
                {
                    if (n.QuestMark == null) continue;
                    bool has = Quests.HasQuestFor(n.Id);
                    if (n.QuestMark.Visible != has) { n.QuestMark.Visible = has; _dirty = true; }
                }
            }
            catch (Exception e) { _questsBroken = true; Log.Error("level", "quest markers unavailable", e); }
        }
        if (IsRift) ScaleRiftMonsters();

        if (Region != null && Dungeon == null && !Liberated && StrongholdMobs.Count > 0)
        {
            int alive = 0;
            foreach (var m in StrongholdMobs) if (IsInstanceValid(m) && !m.Dead) alive++;
            _shAlive = alive;
            if (alive == 0) Liberate();
        }
        UpdateObjective();
    }

    // ───────────────────────── objectives ─────────────────────────

    private static string BossLabel(Monster m) => m == null || !IsInstanceValid(m) ? "首領" : m.Brain == null && !string.IsNullOrEmpty(m.Title) ? m.Title : m.DisplayName;

    /// <summary>Recomputes the objective line, its progress and completion; opens the boss door the moment it completes.</summary>
    public void UpdateObjective()
    {
        string text = ""; float prog = 0f; bool done = false;
        if (IsRift)
        {
            prog = RiftProgress; done = RiftProgress >= 1f;
            text = _bossDead ? (RiftFailed ? "裂隙已崩解——經由傳送門離開" : "裂隙已封閉——經由傳送門離開")
                : done ? $"擊敗{BossLabel(BossMon)}"
                : $"獵殺裂隙中的怪物（{(int)(RiftProgress * 100f)}%）";
        }
        else if (Dungeon != null)
        {
            if (Goal == DungeonGoal.SlayElites)
            {
                int dead = 0;
                foreach (var m in ObjMonsters) if (!IsInstanceValid(m) || m.Dead) dead++;
                ObjCount = dead; ObjTotal = ObjMonsters.Count;
            }
            if (Goal == DungeonGoal.None || ObjTotal <= 0) { done = true; prog = 1f; }
            else if (Goal == DungeonGoal.CollectEmbers)
            {
                done = EmbersOffered;
                prog = done ? 1f : Mathf.Min(Embers, EmbersNeeded) / (float)Mathf.Max(1, EmbersNeeded) * 0.9f;
                text = Embers >= EmbersNeeded ? "將燼晶獻於封印之門旁的祭壇" : $"{ZoneDb.GoalText[(int)Goal]}（{Embers}/{EmbersNeeded}）";
            }
            else
            {
                done = ObjCount >= ObjTotal;
                prog = Mathf.Clamp(ObjCount / (float)ObjTotal, 0f, 1f);
                text = $"{ZoneDb.GoalText[(int)Goal]}（{Mathf.Min(ObjCount, ObjTotal)}/{ObjTotal}）";
            }
            if (_bossDead) text = "此地已淨化——經由傳送門離開";
            else if (done) text = BossMon != null ? $"擊敗{BossLabel(BossMon)}" : "";
        }
        else if (Region != null)
        {
            WorldEvent active = null;
            foreach (var e in Events) if (e.Active) { active = e; break; }
            var p = World.P;
            if (active != null) { text = active.Text; prog = active.Progress; }
            else if (!Liberated && StrongholdMobs.Count > 0 && p != null && p.Pos2.DistanceTo(StrongholdPos) <= StrongholdRadius + 8f)
            {
                text = $"奪回{Region.Stronghold}（殘敵 {_shAlive}）";
                prog = 1f - _shAlive / (float)StrongholdMobs.Count;
            }
        }
        bool was = ObjectiveDone;
        if (text == ObjectiveText && done == was && Mathf.Abs(prog - ObjectiveProgress) < 0.002f) return;
        ObjectiveText = text; ObjectiveProgress = prog; ObjectiveDone = done;
        _dirty = true;
        World.Emit("objective", "");
        if (done && !was && DoorSealed)
        {
            OpenBossDoor();
            Game.I?.Hud?.Toast(IsRift ? "裂隙守衛現身了" : "封印之門開啟了", Palette.Gold);
        }
    }

    /// <summary>The stronghold is clear: the beacon burns again, becomes this region's waypoint, and the game is saved.</summary>
    private void Liberate()
    {
        if (Liberated || Region == null) return;
        Liberated = true;
        string id = Region.Id;
        WorldObjects.LightBeacon(Beacon, true);
        var hero = Game.I?.Hero;
        if (hero != null)
        {
            bool fresh = !hero.D.Waypoints.Contains(id);
            if (fresh) hero.D.Waypoints.Add(id);
            hero.D.Set("sh_" + id);
            World.Emit("stronghold", id);
            if (fresh) World.Emit("waypoint", id);
        }
        Game.I?.Hud?.Toast($"燼塔重燃——{Region.Stronghold}已奪回", Palette.Ember);
        Snd.Ui("victory_sting");
        RemoveMarker(StrongholdMarker);
        if (Beacon != null && IsInstanceValid(Beacon))
        {
            var bm = MarkerOf(Beacon);
            if (bm != null) { bm.Kind = "waypoint"; bm.Label = Beacon.Label; }
            Checkpoint = StandNear(Beacon.Pos2, World.P != null ? World.P.Pos2 : StrongholdPos, 3.4f);
        }
        _dirty = true;
        Log.Info("level", $"stronghold liberated: {id}");
        Game.I?.SaveNow("stronghold");
    }

    // ───────────────────────── the boss door ─────────────────────────

    /// <summary>Shortest walkable cell path (4-connected, which is how actors actually fit through the grid), or null.</summary>
    private List<Vector2I> BfsPath(Vector2I from, Vector2I to)
    {
        int w = Grid.W, h = Grid.H;
        if (!Grid.Walk(from.X, from.Y)) from = Grid.NearestWalkCell(from);
        if (!Grid.Walk(to.X, to.Y)) to = Grid.NearestWalkCell(to);
        if (!Grid.Walk(from.X, from.Y) || !Grid.Walk(to.X, to.Y)) return null;
        var prev = new int[w * h];
        Array.Fill(prev, -1);
        var queue = new Queue<int>();
        int start = from.Y * w + from.X, goal = to.Y * w + to.X;
        prev[start] = start;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            if (cur == goal) break;
            int cx = cur % w, cy = cur / w;
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (!Grid.Walk(nx, ny)) continue;
                int ni = ny * w + nx;
                if (prev[ni] >= 0) continue;
                prev[ni] = cur;
                queue.Enqueue(ni);
            }
        }
        if (prev[goal] < 0) return null;
        var path = new List<Vector2I>();
        for (int c = goal; ; c = prev[c]) { path.Add(new Vector2I(c % w, c / w)); if (c == start) break; }
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Seals the boss arena: the cells of the doorway become wall (however wide the generator made it), pathing is
    /// rebuilt and a barred gate stands in the gap. Returns false when the layout has no door that can be sealed.
    /// </summary>
    public bool CloseBossDoor()
    {
        var door = Data?.Find("bossdoor"); var boss = Data?.Find("boss");
        if (door == null || boss == null || Grid == null || _doorCells != null) return false;
        Vector2I from = Grid.CellOf(StartPos()), to = Grid.CellOf(boss.Pos);
        var cells = new List<Vector2I>();
        bool sealedOff = false;
        Vector2 outside = door.Pos;
        for (int iter = 0; iter < 8; iter++)
        {
            var path = BfsPath(from, to);
            if (path == null) { sealedOff = true; break; }
            int best = -1; float bd = 7f * 7f;
            for (int i = 1; i < path.Count - 1; i++)
            {
                float d = Grid.Center(path[i]).DistanceSquaredTo(door.Pos);
                if (d < bd) { bd = d; best = i; }
            }
            if (best < 0) break;                        // a way in that does not pass the door
            if (iter == 0) outside = Grid.Center(path[Math.Max(0, best - 2)]);
            Grid.Set(path[best].X, path[best].Y, NavGrid.Wall);
            cells.Add(path[best]);
        }
        if (!sealedOff || cells.Count == 0)
        {
            foreach (var c in cells) Grid.Set(c.X, c.Y, NavGrid.Floor);
            Log.Warn("level", $"boss door of {AreaId} could not be sealed (cells tried={cells.Count}); the arena stays open");
            return false;
        }
        _doorCells = cells; _doorPos = door.Pos; _doorOutside = outside; _doorOpen = false;
        Grid.BuildPathing();

        Vector2 centre = Vector2.Zero;
        foreach (var c in cells) centre += Grid.Center(c);
        centre /= cells.Count;
        bool alongX = cells.Count > 1 ? cells[0].Y == cells[1].Y : !Grid.Walk(cells[0].X - 1, cells[0].Y) && !Grid.Walk(cells[0].X + 1, cells[0].Y);
        _doorVisuals.Add(WorldObjects.Gate(centre, alongX ? 0f : Mathf.Pi * 0.5f, cells.Count * NavGrid.Cell));
        _doorMarker = AddMarker("bossdoor", "封印之門", centre);
        Log.Info("level", $"boss door sealed: {cells.Count} cell(s) at ({centre.X:F0},{centre.Y:F0})");
        return true;
    }

    /// <summary>Opens the sealed door: the cells are floor again, the gate sinks away and the checkpoint moves here.</summary>
    public void OpenBossDoor(bool instant = false)
    {
        if (_doorCells == null || _doorOpen) return;
        _doorOpen = true;
        foreach (var c in _doorCells) Grid.Set(c.X, c.Y, NavGrid.Floor);
        Grid.BuildPathing();
        foreach (var v in _doorVisuals)
        {
            if (!IsInstanceValid(v)) continue;
            if (instant) { v.QueueFree(); continue; }       // a resumed save: the gate is simply gone
            var node = v;
            var tw = CreateTween();
            tw.TweenProperty(node, "position:y", -4.2f, 1.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
            tw.TweenCallback(Callable.From(() => { if (IsInstanceValid(node)) node.QueueFree(); }));
            Snd.At("door_open", node.GlobalPosition + Vector3.Up, 1f);
            Fx.Dust(node.GlobalPosition);
        }
        _doorVisuals.Clear();
        if (!instant) Fx.Shake(0.15f, 0.4f);
        Checkpoint = Grid.WalkAt(_doorOutside) ? _doorOutside : Grid.NearestWalk(_doorOutside);
        RemoveMarker(_doorMarker);
        _doorMarker = null;
        _dirty = true;
        Log.Info("level", $"boss door opened; checkpoint moved to ({Checkpoint.X:F0},{Checkpoint.Y:F0})");
    }

    // ───────────────────────── the boss fight ─────────────────────────

    private bool _bossMusic;

    /// <summary>Registers the monster that ends this level. A boss with a brain sleeps until the hero steps into its arena.</summary>
    public void SetBoss(Monster m, bool story)
    {
        BossMon = m; BossIsStory = story; _bossDead = false; _engaged = false;
        if (m == null) return;
        if (m.Brain != null) { m.SetProcess(false); m.SetPhysicsProcess(false); }
        AddMarker("boss", BossLabel(m), ArenaPos, m);
    }

    private void TickBoss(Player p)
    {
        var b = BossMon;
        if (b == null || _bossDead) return;
        if (!IsInstanceValid(b)) { BossMon = null; Boss = null; _dirty = true; return; }
        if (_engaged || p.Dead) return;
        bool inArena = !DoorSealed && p.Pos2.DistanceTo(ArenaPos) <= ArenaRadius + 1f;
        bool provoked = b.Life < b.MaxLife - 0.5f || (b.Brain == null && b.Aggro);
        if (inArena || provoked) Engage();
    }

    private void Engage()
    {
        var b = BossMon;
        _engaged = true; Boss = b;
        b.SetProcess(true); b.SetPhysicsProcess(true);
        b.ForceAggro();
        bool brain = b.Brain != null;
        Game.I?.Cam?.SetZoomTarget(brain ? 1.45f : 1.25f, 1.6f);
        if (brain)
        {
            _bossMusic = true;
            Game.I?.Audio?.PlayMusic(b.Def.Id == "boss_osaros" ? "boss_final" : "boss");
            Snd.At("boss_roar", b.GlobalPosition, 1f);
            Fx.Shake(0.3f, 0.6f);
        }
        else Snd.At("elite_spawn", b.GlobalPosition, 1f);
        Game.I?.Hud?.AreaTitle(BossLabel(b), brain ? Dungeon?.Name ?? "" : b.DisplayName);
        _dirty = true;
        Log.Info("boss", $"engaged {b.Def.Id} life={b.MaxLife:F0} level={b.Level} area={AreaId}");
    }

    private void RestoreMusic()
    {
        if (!_bossMusic) return;
        _bossMusic = false;
        string track = (Region ?? SpawnRegion)?.Music;
        if (!string.IsNullOrEmpty(track)) Game.I?.Audio?.PlayMusic(track);
    }

    /// <summary>The hero died mid-fight: the boss is whole again, back at the centre of its arena, and its adds are gone.</summary>
    private void ResetBossFight()
    {
        var b = BossMon;
        if (b == null || _bossDead || !_engaged || !IsInstanceValid(b) || b.Dead) return;
        _engaged = false; Boss = null;
        int removed = 0;
        for (int i = World.Monsters.Count - 1; i >= 0; i--)
        {
            var m = World.Monsters[i];
            if (m == b || !IsInstanceValid(m) || m.Pos2.DistanceTo(ArenaPos) > ArenaRadius + 5f) continue;
            World.Monsters.RemoveAt(i);
            m.QueueFree();
            removed++;
        }
        b.Life = b.MaxLife; b.Barrier = 0f; b.BarrierLeft = 0f; b.Dots.Clear(); b.ClearCc();
        b.Stagger = 0f; b.StaggerLeft = 0f; b.StaggerImmune = 0f; b.Knock = Vector2.Zero;
        b.GlobalPosition = World.To3(ArenaPos);
        if (b.Brain != null)
        {
            b.SpeedMul = 1f; b.AtkSpeedMul = 1f; b.DrMul = 1f; b.DmgMul = 1f; b.BlocksProjectiles = false;
            b.Brain = new BossBrain(b);
            b.SetProcess(false); b.SetPhysicsProcess(false);
        }
        b.Model?.Pause(false);
        b.Model?.Play(b.Def.Idle, 1f, 0.3f);
        Game.I?.Cam?.SetZoomTarget(1f, 1.5f);
        RestoreMusic();
        _dirty = true;
        Log.Info("boss", $"fight reset: {b.Def.Id} adds removed={removed}");
    }

    private Vector2 ExitSpot(Vector2 chestAt)
    {
        Vector2 dir = ArenaPos - chestAt;
        if (dir.LengthSquared() < 9f) dir = new Vector2(1f, 0.4f);
        Vector2 q = chestAt + dir.Normalized() * 5f;
        return Grid.WalkAt(q) && Grid.LineWalk(chestAt, q) ? q : StandNear(chestAt, ArenaPos, 4f);
    }

    private void OnBossDefeated(Monster m)
    {
        _bossDead = true; _engaged = false;
        Vector2 at = Grid.NearestWalk(m.Pos2);
        BossFellAt = at;
        string defId = m.Def.Id;
        Log.Info("boss", $"defeated {defId} in {AreaId}");
        Defer(() =>
        {
            Boss = null;
            Game.I?.Cam?.SetZoomTarget(1f, 2.5f);
            RestoreMusic();
            RemoveMarker(m);
            if (IsRift) FinishRift(at);
            else if (Dungeon != null)
            {
                RewardChest(StandNear(at, ArenaPos, 2.2f), BossIsStory ? 3 : 2);
                Game.I?.Hero?.D.Set("dg_" + Dungeon.Id);
                World.Emit("dungeon", Dungeon.Id);
                if (!BossIsStory) World.Emit("miniboss", Dungeon.Id);
                Game.I?.Hud?.Toast($"{Dungeon.Name}已淨化", Palette.Gold);
                DungeonExitPortal(at);
            }
            Snd.Ui("victory_sting");
            UpdateObjective();
            Game.I?.SaveNow("boss");
        });
    }

    private void DungeonExitPortal(Vector2 at)
    {
        string back = Region?.Id ?? ZoneDb.Town, from = AreaId;
        var portal = WorldObjects.Portal(ExitSpot(at), $"返回{ZoneDb.AreaName(back)}", "傳送門", Palette.Ember, _ => Game.I.EnterArea(back, from));
        AddMarker("exit", portal.Label, portal.Pos2, portal);
    }

    private void RiftExitPortal(Vector2 at)
    {
        string from = AreaId;
        var portal = WorldObjects.Portal(ExitSpot(at), $"返回{ZoneDb.TownName}", "傳送門", Palette.Ember, _ => Game.I.EnterArea(ZoneDb.Town, from));
        AddMarker("exit", portal.Label, portal.Pos2, portal);
    }

    /// <summary>
    /// Save restore: the boss of this dungeon or rift died before the save. Nothing is paid, flagged or announced again;
    /// the place is cleared and its exit portal stands where it did. (The boss monster itself is removed by LevelState.)
    /// </summary>
    public void RestoreBossDefeated(Vector2 at)
    {
        if (_bossDead) return;
        _bossDead = true; _engaged = false; Boss = null;
        if (BossMon != null) RemoveMarker(BossMon);
        BossMon = null;
        if (Grid != null) at = Grid.NearestWalk(at == Vector2.Zero ? ArenaPos : at);
        BossFellAt = at;
        if (IsRift) RiftExitPortal(at);
        else if (Dungeon != null) DungeonExitPortal(at);
        _dirty = true;
    }

    /// <summary>Save restore: the stronghold was freed after this level was first built. The beacon burns, without fanfare or events.</summary>
    public void RestoreLiberated()
    {
        if (Liberated || Region == null) return;
        Liberated = true;
        if (Beacon != null && IsInstanceValid(Beacon))
        {
            WorldObjects.LightBeacon(Beacon, false);
            var bm = MarkerOf(Beacon);
            if (bm != null) { bm.Kind = "waypoint"; bm.Label = Beacon.Label; }
        }
        RemoveMarker(StrongholdMarker);
        _dirty = true;
    }

    /// <summary>Save restore: a reward chest that was still closed (no arrival flash).</summary>
    public void RestoreRewardChest(Vector2 at, int quality)
    {
        at = Grid != null ? Grid.NearestWalk(at) : at;
        var chest = WorldObjects.Chest(at, quality);
        RewardChests.Add((chest, quality));
        AddMarker("chest", chest.Label, at, chest);
    }

    // ───────────────────────── rift ─────────────────────────

    /// <summary>Rift monsters carry 1.12^tier life and damage on top of the level curve; late arrivals (summons, splits) are caught here too.</summary>
    public void ScaleRiftMonsters()
    {
        if (!IsRift || RiftMul <= 1.0001f) return;
        var list = World.Monsters;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (!_riftScaled.Add(m.GetInstanceId())) continue;
            float frac = m.MaxLife > 0f ? m.Life / m.MaxLife : 1f;
            m.MaxLife = Mathf.Round(m.MaxLife * RiftMul);
            m.Life = m.MaxLife * frac;
            m.BaseDamage *= RiftMul;
        }
    }

    private void TickRift(float dt)
    {
        if (_bossDead || RiftFailed) return;
        RiftTimeLeft = Mathf.Max(0f, RiftTimeLeft - dt);
        if (RiftTimeLeft > 0f) return;
        RiftFailed = true; _dirty = true;
        Game.I?.Hud?.Toast("時限已盡——裂隙開始崩解，戰利品將所剩無幾", Palette.Bad);
        Snd.Ui("bell_toll");
        Log.Info("rift", $"tier {RiftTier} timed out at progress {RiftProgress:P0}");
    }

    private void FinishRift(Vector2 at)
    {
        var hero = Game.I?.Hero;
        bool ok = !RiftFailed;
        int tier = RiftTier, lvl = MonsterLevel;
        if (ok && hero != null)
        {
            if (tier > hero.D.RiftBest) hero.D.RiftBest = tier;
            World.Emit("rift", tier.ToString());
        }
        RewardChest(StandNear(at, ArenaPos, 2.2f), ok ? (tier >= 3 ? 3 : 2) : 1, spot =>
        {
            if (!ok) return;
            Loot.DropGold((int)(30f * lvl * (1f + 0.3f * tier)), spot);
            Loot.DropMaterials(4 + tier * 2, 2 + tier, tier / 3, spot);
            for (int i = 0; i < Math.Min(3, tier / 4); i++) Loot.OpenChest(Grid.NearestWalk(spot + new Vector2(1.2f * (i - 1), 1f)), 2);
        });
        Game.I?.Hud?.Toast(ok ? $"裂隙第 {tier} 層已封閉" : "裂隙已崩解——只搶回些許殘物", ok ? Palette.Gold : Palette.TextDim);
        RiftExitPortal(at);
        Log.Info("rift", $"tier {tier} finished success={ok} timeLeft={RiftTimeLeft:F0}s best={hero?.D.RiftBest ?? 0}");
    }

    // ───────────────────────── world hooks ─────────────────────────

    /// <summary>A carried 燼晶 was picked up.</summary>
    public void AddEmber()
    {
        Embers++;
        if (Embers == EmbersNeeded) Game.I?.Hud?.Toast("燼晶已足——前往封印之門旁的祭壇", Palette.Ember);
        UpdateObjective();
    }

    private void OnWorldEvent(string evt, string arg)
    {
        if (World.Root != this || evt != "death") return;
        CancelChannel();
        Defer(ResetBossFight);          // on the next update: the blow that killed the hero may still be resolving in the boss's own code
    }

    private void OnMonsterKilled(Monster m, Hit h)
    {
        if (World.Root != this || m == null || m.Faction != Faction.Monster) return;
        try
        {
            if (IsRift && m != BossMon && !_bossDead && RiftProgress < 1f)
            {
                RiftPoints += m.Tier == MonTier.Elite ? 8f : m.Tier == MonTier.Champion ? 3f : 1f;
                RiftProgress = Mathf.Clamp(RiftPoints / Mathf.Max(1f, RiftNeed), 0f, 1f);
            }
            if (m.Objective)
            {
                RemoveMarker(m);
                if (Goal == DungeonGoal.CollectEmbers && Dungeon != null && !EmbersOffered)
                {
                    Vector2 at = Grid.NearestWalk(m.Pos2);
                    Defer(() => WorldObjects.EmberShard(at, AddEmber));
                }
            }
            if (m == BossMon && !_bossDead) OnBossDefeated(m);
            _slowT = 0f;                                // refresh objective state on the next update
        }
        catch (Exception e) { Log.Error("level", "kill handler failed", e); }
    }
}
