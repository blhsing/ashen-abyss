using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AshenAbyss;

/// <summary>
/// Fills a freshly generated level: monster packs with a role mix, champions and elites, the stronghold and its
/// beacon, dungeon objectives and bosses, world events, the town's people and services, and every chest, shrine,
/// altar and breakable. Runs once from <see cref="Level"/> after the layout and scenery exist.
/// </summary>
public static class Populate
{
    // ───────────────────────── rosters ─────────────────────────

    private sealed class Roster
    {
        public readonly List<(MonsterDef d, float w)> All = new(), Core = new(), Ranged = new(), Bruiser = new(), Leaders = new();
    }

    private static Roster _roster;
    private static RegionDef _rosterOf;
    private static readonly Dictionary<string, MonsterDef> SturdyDefs = new();

    /// <summary>A region's monsters sorted by role: the core of a pack (swarmers, melee), its back line and its heavies.</summary>
    private static Roster RosterOf(RegionDef reg)
    {
        if (_roster != null && _rosterOf == reg) return _roster;
        var r = new Roster();
        if (reg?.Spawns != null)
            foreach (var (id, w) in reg.Spawns)
            {
                var d = MonsterDb.Get(id);
                if (d == null) { Log.Warn("populate", $"region {reg.Id} lists unknown monster '{id}'"); continue; }
                r.All.Add((d, w));
                if (d.Role is "ranged" or "caster" or "support") r.Ranged.Add((d, w));
                else if (d.Role == "bruiser") r.Bruiser.Add((d, w));
                else r.Core.Add((d, w));
                if (d.AI != MonAI.Exploder && d.Role != "support") r.Leaders.Add((d, w));
            }
        if (r.All.Count == 0) r.All.Add((MonsterDb.Get("husk") ?? MonsterDb.All[0], 1f));
        if (r.Core.Count == 0) r.Core.AddRange(r.All);
        if (r.Leaders.Count == 0) foreach (var e in r.All) r.Leaders.Add((Sturdy(e.d), e.w));
        _roster = r; _rosterOf = reg;
        return r;
    }

    private static MonsterDef Pick(List<(MonsterDef d, float w)> list, Random rng)
    {
        float total = 0f;
        foreach (var e in list) total += e.w;
        float x = (float)rng.NextDouble() * total;
        foreach (var e in list) { x -= e.w; if (x <= 0f) return e.d; }
        return list[^1].d;
    }

    /// <summary>
    /// A monster fit to lead: one that bursts on its first attack would end its own fight, so as a champion, elite or
    /// mini-boss it becomes a heavy hitter with telegraphed swings instead (same id, so kill counters still match).
    /// </summary>
    private static MonsterDef Sturdy(MonsterDef d)
    {
        if (d == null || d.AI != MonAI.Exploder) return d;
        if (!SturdyDefs.TryGetValue(d.Id, out var s))
            SturdyDefs[d.Id] = s = d.Variant(d.Id, d.Name, d.Tint, v =>
            {
                v.AI = MonAI.Brute; v.Range = 3f; v.Contact = 0.65f; v.Cooldown = 2.3f; v.Dmg = 1.6f; v.Speed = MathF.Max(v.Speed, 3.4f);
            });
        return s;
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }

    /// <summary>Elite affixes, each from a different group (hazard / control / defensive / behavioural).</summary>
    public static List<string> PickAffixes(int n, Random rng)
    {
        var groups = MonsterDb.Affixes.GroupBy(a => a.Group).Select(g => g.ToList()).ToList();
        Shuffle(groups, rng);
        var res = new List<string>(n);
        for (int i = 0; i < n && i < groups.Count; i++) res.Add(groups[i][rng.Next(groups[i].Count)].Id);
        return res;
    }

    // ───────────────────────── spawning ─────────────────────────

    public static Monster SpawnOne(Level L, MonsterDef def, MonTier tier, Vector2 pos, int pack, IEnumerable<string> affixes = null, bool mini = false)
    {
        var m = Monster.Spawn(def, L.MonsterLevel, tier, L.Grid.NearestWalk(pos), Faction.Monster, affixes, mini);
        m.Pack = pack;
        return m;
    }

    private static Vector2 Scatter(Level L, Vector2 centre, float rMin, float rMax) => L.Grid.RandomWalkNear(centre, rMin, rMax, L.Rand);

    /// <summary>
    /// A pack: mostly one or two kinds of swarmer or melee, zero to two ranged or casters behind them and sometimes a
    /// bruiser. Everyone shares a pack id so the whole group wakes together.
    /// </summary>
    public static List<Monster> Pack(Level L, Vector2 pos, int count, bool objective = false, List<Monster> into = null)
    {
        var ro = RosterOf(L.SpawnRegion); var rng = L.Rand;
        // the first levels meet small packs: a fresh hero with a starter weapon cannot hold eight husks at once
        int cap = L.MonsterLevel <= 2 ? 4 : L.MonsterLevel <= 4 ? 6 : L.MonsterLevel <= 8 ? 9 : 14;
        int n = Math.Clamp(count, 1, cap), pack = L.NextPack();
        int ranged = 0, bruiser = 0;
        if (ro.Ranged.Count > 0)
        {
            double x = rng.NextDouble();
            ranged = n >= 5 ? (x < 0.3 ? 0 : x < 0.75 ? 1 : 2) : n >= 3 ? (x < 0.55 ? 0 : 1) : 0;
        }
        if (ro.Bruiser.Count > 0 && n - ranged >= 3 && rng.NextDouble() < 0.28) bruiser = 1;
        int core = n - ranged - bruiser;
        var a = Pick(ro.Core, rng);
        var b = rng.NextDouble() < 0.3 ? Pick(ro.Core, rng) : a;
        var list = into ?? new List<Monster>(n);
        void Add(Monster m) { m.Objective = objective; list.Add(m); }
        for (int i = 0; i < core; i++) Add(SpawnOne(L, i % 3 == 2 ? b : a, MonTier.Normal, Scatter(L, pos, 0.4f, 1.6f + n * 0.3f), pack));
        for (int i = 0; i < ranged; i++) Add(SpawnOne(L, Pick(ro.Ranged, rng), MonTier.Normal, Scatter(L, pos, 2.5f, 4.5f), pack));
        if (bruiser > 0) Add(SpawnOne(L, Pick(ro.Bruiser, rng), MonTier.Normal, Scatter(L, pos, 0.5f, 2.2f), pack));
        return list;
    }

    /// <summary>One champion with a single affix, joining an existing pack (or alone when pack is below zero).</summary>
    public static Monster Champion(Level L, Vector2 pos, int pack = -1)
    {
        var ro = RosterOf(L.SpawnRegion);
        return SpawnOne(L, Sturdy(Pick(ro.Leaders, L.Rand)), MonTier.Champion, Scatter(L, pos, 0.5f, 2.5f), pack < 0 ? L.NextPack() : pack, PickAffixes(1, L.Rand));
    }

    /// <summary>Three to five champions of one kind sharing one affix.</summary>
    public static List<Monster> Champions(Level L, Vector2 pos, int count, List<Monster> into = null)
    {
        var ro = RosterOf(L.SpawnRegion); var rng = L.Rand;
        int n = Math.Clamp(count, 3, 5), pack = L.NextPack();
        var def = Sturdy(Pick(ro.Leaders, rng));
        var affix = PickAffixes(1, rng);
        var list = into ?? new List<Monster>(n);
        for (int i = 0; i < n; i++) list.Add(SpawnOne(L, def, MonTier.Champion, Scatter(L, pos, 0.5f, 3f), pack, affix));
        return list;
    }

    /// <summary>An elite leader (two affixes, three from world tier 2) with two to four ordinary followers. Returns the leader.</summary>
    public static Monster Elite(Level L, Vector2 pos, int count, bool objective, List<Monster> into = null)
    {
        var ro = RosterOf(L.SpawnRegion); var rng = L.Rand;
        int pack = L.NextPack();
        int affixes = (Game.I?.WorldTier ?? 0) >= 2 ? 3 : 2;
        var leader = SpawnOne(L, Sturdy(Pick(ro.Leaders, rng)), MonTier.Elite, pos, pack, PickAffixes(affixes, rng));
        leader.Objective = objective;
        into?.Add(leader);
        var follower = Pick(ro.Core, rng);
        int minions = Math.Clamp(count - 1, 2, 4);
        for (int i = 0; i < minions; i++)
        {
            var m = SpawnOne(L, follower, MonTier.Normal, Scatter(L, pos, 1.2f, 3.4f), pack);
            into?.Add(m);
        }
        return leader;
    }

    /// <summary>A stronghold's garrison: a denser pack with one champion among it.</summary>
    public static List<Monster> StrongholdPack(Level L, Vector2 pos, int count, List<Monster> into = null)
    {
        var list = into ?? new List<Monster>();
        int before = list.Count;
        Pack(L, pos, count + 2, false, list);
        int pack = list.Count > before ? list[before].Pack : -1;
        list.Add(Champion(L, pos, pack));
        return list;
    }

    /// <summary>A named mini-boss built from an ordinary monster.</summary>
    public static Monster MiniBoss(Level L, MonsterDef def, Vector2 pos, int affixes, string title)
    {
        def = Sturdy(def ?? Pick(RosterOf(L.SpawnRegion).Leaders, L.Rand));
        var m = SpawnOne(L, def, MonTier.Elite, pos, L.NextPack(), PickAffixes(affixes, L.Rand), true);
        if (!string.IsNullOrEmpty(title)) m.Title = title;
        return m;
    }

    /// <summary>The region's spell-slinger, for rituals.</summary>
    public static MonsterDef CasterDef(Level L)
    {
        var ro = RosterOf(L.SpawnRegion);
        foreach (var e in ro.Ranged) if (e.d.Role == "caster") return e.d;
        return MonsterDb.Get("cult_caster") ?? Pick(ro.Leaders, L.Rand);
    }

    private static void SpawnGroupOf(Level L, SpawnGroup g)
    {
        switch (g.Kind)
        {
            case "champions": Champions(L, g.Pos, g.Count); break;
            case "elite": Elite(L, g.Pos, g.Count, false); break;
            case "stronghold": StrongholdPack(L, g.Pos, g.Count); break;
            default: Pack(L, g.Pos, g.Count); break;
        }
    }

    // ───────────────────────── entry point ─────────────────────────

    public static void Run(Level L)
    {
        if (L?.Data == null || L.Grid == null) return;
        _roster = null; _rosterOf = null;
        L.Data.Pois ??= new List<Poi>(); L.Data.Spawns ??= new List<SpawnGroup>(); L.Data.Props ??= new List<PropSlot>();
        if (L.IsTown) Town(L);
        else if (L.IsRift) Rift(L);
        else if (L.Dungeon != null) DungeonContent(L);
        else if (L.Region != null) Overworld(L);
        Log.Guard("populate", () => Breakables(L));
        Log.Info("populate", $"area={L.AreaId} monsters={World.Monsters.Count} events={L.Events.Count} npcs={L.Npcs.Count} " +
            $"stronghold={(L.Liberated ? "liberated" : L.StrongholdMobs.Count.ToString())} objective={L.Goal}:{L.ObjTotal}");
    }

    private static void Breakables(Level L)
    {
        int n = 0;
        foreach (var s in L.Data.Props)
        {
            if (s == null || s.Category != "breakable") continue;
            WorldObjects.Breakable(s, L.Theme, L.Rand);
            n++;
        }
        Log.Debug("populate", $"breakables={n}");
    }

    /// <summary>Chests, shrines, altars and exits wherever the layout marked them.</summary>
    private static void CommonObjects(Level L, bool indoors)
    {
        var rng = L.Rand; var data = L.Data;
        foreach (var p in data.All("chest").ToList())
        {
            int q = indoors ? (rng.NextDouble() < 0.3 ? 2 : 1) : (rng.NextDouble() < 0.25 ? 1 : 0);
            var c = WorldObjects.Chest(p.Pos, q, p.Facing);
            L.AddMarker("chest", c.Label, p.Pos, c, true);
        }
        int kind = rng.Next(WorldObjects.ShrineCount);
        foreach (var p in data.All("shrine").ToList())
        {
            var s = WorldObjects.Shrine(p.Pos, kind++ % WorldObjects.ShrineCount);
            L.AddMarker("shrine", s.Label, p.Pos, s, true);
        }
        int index = 0;
        string scope = L.Dungeon?.Id ?? L.Region?.Id ?? L.AreaId;
        foreach (var p in data.All("altar").ToList())
        {
            if (L.IsRift)                               // a rift is never the same twice: a blessing instead of a permanent gift
            {
                var s = WorldObjects.Shrine(p.Pos, kind++ % WorldObjects.ShrineCount);
                L.AddMarker("shrine", s.Label, p.Pos, s, true);
                continue;
            }
            var a = WorldObjects.Altar(p.Pos, $"altar_{scope}_{index++}");
            if (a.CanUse) L.AddMarker("altar", a.Label, p.Pos, a, true);
        }
        foreach (var p in data.All("exit").ToList())
        {
            string to = !string.IsNullOrEmpty(p.Tag) ? p.Tag : L.Dungeon != null && L.Region != null ? L.Region.Id : ZoneDb.Town;
            var e = WorldObjects.Exit(p.Pos, to, p.Facing);
            L.AddMarker("exit", e.Label, p.Pos, e);
        }
    }

    private static bool NearEvent(Level L, Vector2 p, float r)
    {
        foreach (var e in L.Events) if (e.Pos.DistanceTo(p) < r) return true;
        return false;
    }

    private static Vector2 StartOf(Level L) => L.Data.Find("start")?.Pos ?? L.Checkpoint;

    // ───────────────────────── town ─────────────────────────

    private static MemberInfo _returnMember;
    private static bool _returnLooked;

    /// <summary>Game.ReturnArea (where a town portal was opened), read loosely so either a field or a property works.</summary>
    private static string ReturnArea(bool clear = false)
    {
        var g = Game.I;
        if (g == null) return null;
        if (!_returnLooked)
        {
            _returnLooked = true;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            _returnMember = (MemberInfo)typeof(Game).GetField("ReturnArea", flags) ?? typeof(Game).GetProperty("ReturnArea", flags);
            if (_returnMember == null) Log.Warn("populate", "Game.ReturnArea not found: the town will have no return portal");
        }
        try
        {
            switch (_returnMember)
            {
                case FieldInfo f:
                {
                    string v = f.GetValue(f.IsStatic ? null : g) as string;
                    if (clear && !f.IsInitOnly) f.SetValue(f.IsStatic ? null : g, null);
                    return v;
                }
                case PropertyInfo pr:
                {
                    string v = pr.GetValue(g) as string;
                    if (clear && pr.CanWrite) pr.SetValue(g, null);
                    return v;
                }
            }
        }
        catch (Exception e) { Log.Error("populate", "reading Game.ReturnArea failed", e); }
        return null;
    }

    private static void Town(Level L)
    {
        var rng = L.Rand;
        foreach (var p in L.Data.Pois.ToList())
        {
            switch (p.Kind)
            {
                case "npc":
                {
                    var n = WorldObjects.Npc(p.Pos, p.Tag, p.Facing, rng);
                    if (n.Tag as string != "villager") L.AddMarker("npc", WorldObjects.NpcTitle(p.Tag), p.Pos, n);
                    break;
                }
                case "stash": L.AddMarker("stash", "燼藏箱", p.Pos, WorldObjects.Stash(p.Pos, p.Facing)); break;
                case "brazier": L.AddMarker("brazier", "大燼火盆", p.Pos, WorldObjects.Brazier(p.Pos)); break;
                case "tierstatue": L.AddMarker("tierstatue", "界階石像", p.Pos, WorldObjects.TierStatue(p.Pos, p.Facing)); break;
                case "riftstone": L.AddMarker("riftstone", "裂隙石", p.Pos, WorldObjects.RiftStone(p.Pos)); break;
                case "waypoint": L.AddMarker("waypoint", "燼塔路標", p.Pos, WorldObjects.Waypoint(p.Pos)); break;
                case "exit": L.AddMarker("exit", "鎮門", p.Pos, WorldObjects.TownGate(p.Pos, p.Facing)); break;
                case "chest": WorldObjects.Chest(p.Pos, 0, p.Facing); break;
            }
        }
        string back = ReturnArea();
        if (!string.IsNullOrEmpty(back) && back != ZoneDb.Town)
        {
            Vector2 start = StartOf(L);
            Vector2 at = L.Grid.RandomWalkNear(start, 2.6f, 4.2f, rng);
            if (at == start) at = L.StandNear(start, start + Vector2.Down, 3f);
            var portal = WorldObjects.Portal(at, $"返回{ZoneDb.AreaName(back)}", "回到你離開的地方", new Color(0.45f, 0.75f, 1f), _ =>
            {
                string area = ReturnArea(true) ?? back;
                Log.Info("populate", $"return portal → {area}");
                Game.I.EnterArea(area, "return");
            });
            L.AddMarker("portal", portal.Label, at, portal);
        }
    }

    // ───────────────────────── overworld ─────────────────────────

    private static void Overworld(Level L)
    {
        var data = L.Data; var rng = L.Rand; var reg = L.Region; var hero = Game.I?.Hero;
        bool lib = hero != null && hero.D.Has("sh_" + reg.Id);
        var sh = data.Find("stronghold"); var wp = data.Find("waypoint");
        bool hasCamp = sh != null || wp != null;
        L.Liberated = lib;
        L.StrongholdPos = sh?.Pos ?? wp?.Pos ?? L.Checkpoint;
        L.StrongholdRadius = sh != null ? Mathf.Max(sh.Radius, 8f) : 12f;

        if (hasCamp)
        {
            Vector2 at = wp?.Pos ?? sh.Pos;
            L.Beacon = WorldObjects.Beacon(at, reg, lib);
            L.AddMarker(lib ? "waypoint" : "beacon", L.Beacon.Label, at, L.Beacon);
            if (lib && hero != null && !hero.D.Waypoints.Contains(reg.Id)) hero.D.Waypoints.Add(reg.Id);
        }

        // two or three events per visit, placed first so wandering packs keep clear of them
        var eventPois = data.All("event").ToList();
        Shuffle(eventPois, rng);
        int evCount = Math.Min(eventPois.Count, 2 + rng.Next(2));
        var order = new List<int> { 0, 1, 2 };
        Shuffle(order, rng);
        for (int i = 0; i < eventPois.Count; i++)
        {
            var p = eventPois[i];
            if (i >= evCount)
            {
                var c = WorldObjects.Chest(p.Pos, 1, p.Facing);
                L.AddMarker("chest", c.Label, p.Pos, c, true);
                continue;
            }
            WorldEvent ev = order[i % 3] switch { 0 => new BrazierEvent(), 1 => new CursedChestEvent(), _ => new RitualEvent() };
            ev.L = L; ev.Pos = p.Pos;
            L.Events.Add(ev);
            try { ev.Setup(); }
            catch (Exception e) { Log.Error("populate", $"event {ev.GetType().Name} failed to set up", e); L.Events.Remove(ev); }
        }

        foreach (var g in data.Spawns)
        {
            if (g.Pos.DistanceTo(L.Checkpoint) < 13f) continue;                  // breathing room where the hero arrives
            bool inCamp = g.Kind == "stronghold" || (sh != null && g.Pos.DistanceTo(sh.Pos) <= sh.Radius + 2f);
            if (inCamp)
            {
                if (!lib) StrongholdPack(L, g.Pos, g.Count, L.StrongholdMobs);    // a liberated camp stays peaceful
                continue;
            }
            if (NearEvent(L, g.Pos, 9f)) continue;
            SpawnGroupOf(L, g);
        }

        if (!lib && (hasCamp || L.StrongholdMobs.Count > 0))
        {
            var boss = MiniBoss(L, MonsterDb.Get(reg.StrongholdBoss), Scatter(L, L.StrongholdPos, 2.5f, 5f), 2, $"{reg.Stronghold}之主");
            L.StrongholdMobs.Add(boss);
            L.StrongholdMarker = L.AddMarker("stronghold", reg.Stronghold, L.StrongholdPos);
        }
        else if (lib && hasCamp)
        {
            int folk = 2 + rng.Next(2);
            for (int i = 0; i < folk; i++)
            {
                Vector2 at = Scatter(L, L.StrongholdPos, 3f, 6f);
                Vector2 look = L.StrongholdPos - at;
                WorldObjects.CampFolk(at, Mathf.Atan2(look.X, look.Y), rng);
            }
        }

        foreach (var p in data.All("entrance").ToList())
        {
            var d = ZoneDb.Dungeon(p.Tag);
            if (d == null) { Log.Warn("populate", $"entrance to unknown dungeon '{p.Tag}'"); continue; }
            var e = WorldObjects.Entrance(p.Pos, d, reg, p.Facing);
            L.AddMarker("entrance", d.Name, p.Pos, e);
        }
        CommonObjects(L, false);
    }

    // ───────────────────────── dungeons ─────────────────────────

    private static void SetArena(Level L)
    {
        var boss = L.Data.Find("boss");
        L.HasArena = boss != null;
        if (boss != null) { L.ArenaPos = boss.Pos; L.ArenaRadius = Mathf.Max(boss.Radius, 9f); return; }
        Vector2 far = L.Checkpoint; float bd = -1f;
        foreach (var g in L.Data.Spawns) { float d = g.Pos.DistanceSquaredTo(L.Checkpoint); if (d > bd) { bd = d; far = g.Pos; } }
        L.ArenaPos = far; L.ArenaRadius = 10f;
        Log.Warn("populate", $"{L.AreaId} has no boss arena; the boss waits at the far end");
    }

    /// <summary>Up to <paramref name="n"/> well-separated spots for idols or cages: marked objectives first, then objective groups, then far rooms.</summary>
    private static List<Vector2> ObjectiveSpots(Level L, List<SpawnGroup> objective, int n)
    {
        var res = new List<Vector2>();
        void TryAdd(Vector2 p)
        {
            if (res.Count >= n) return;
            // The objective must be reachable from the entrance while the boss door is still shut (the door opens only
            // when every objective is done): a spot in a pocket, on a wall, or in a room behind the sealed arena would
            // leave the dungeon unfinishable. Such a spot moves to the nearest reachable floor, or is dropped.
            if (!L.ReachableSpot(p, 4, out var at))
            {
                Log.Warn("level", $"objective spot ({p.X:0},{p.Y:0}) in {L.AreaId} cannot be reached from the entrance: skipped");
                return;
            }
            if (at != p) Log.Info("level", $"objective spot ({p.X:0},{p.Y:0}) in {L.AreaId} moved to reachable floor ({at.X:0},{at.Y:0})");
            p = at;
            if (p.DistanceTo(L.ArenaPos) <= L.ArenaRadius + 2f || p.DistanceTo(L.Checkpoint) < 10f) return;
            foreach (var q in res) if (q.DistanceTo(p) < 8f) return;
            res.Add(p);
        }
        foreach (var p in L.Data.All("objective")) TryAdd(p.Pos);
        foreach (var g in objective) TryAdd(g.Pos);
        if (res.Count < n)
            foreach (var g in L.Data.Spawns.OrderByDescending(s => s.Pos.DistanceSquaredTo(L.Checkpoint))) TryAdd(g.Pos);
        return res;
    }

    /// <summary>A tucked-away walkable cell near a point (most walls around it), so an altar there is not in the way.</summary>
    private static Vector2 NookNear(Level L, Vector2 around, int cells)
    {
        var grid = L.Grid; var c0 = grid.CellOf(around);
        Vector2 best = around; int bestScore = -1000;
        for (int dy = -cells; dy <= cells; dy++)
            for (int dx = -cells; dx <= cells; dx++)
            {
                int x = c0.X + dx, y = c0.Y + dy;
                if (!grid.Walk(x, y) || (dx == 0 && dy == 0)) continue;
                var p = grid.Center(x, y);
                if (!grid.LineWalk(around, p)) continue;
                int walls = 0;
                for (int ny = -1; ny <= 1; ny++) for (int nx = -1; nx <= 1; nx++) if (!grid.Walk(x + nx, y + ny)) walls++;
                int score = walls * 4 - Math.Abs(dx) - Math.Abs(dy);
                if (!L.KeepsConnected(p)) score -= 100;                // never in the throat of a corridor
                if (score > bestScore) { bestScore = score; best = p; }
            }
        return best;
    }

    private static void DungeonContent(Level L)
    {
        var data = L.Data; var d = L.Dungeon; var reg = L.Region;
        SetArena(L);

        var groups = new List<SpawnGroup>();
        foreach (var g in data.Spawns)
            if (g.Pos.DistanceTo(L.ArenaPos) > L.ArenaRadius + 2f && g.Pos.DistanceTo(L.Checkpoint) > 11f) groups.Add(g);
        var objective = groups.Where(g => g.Kind == "objective").ToList();
        if (L.Goal != DungeonGoal.None && objective.Count == 0)                  // none marked: take the groups deepest into the dungeon
            objective = groups.OrderByDescending(g => g.Pos.DistanceSquaredTo(L.Checkpoint)).Take(4).ToList();
        if (L.Goal != DungeonGoal.CollectEmbers && objective.Count > 4) objective = objective.Take(4).ToList();
        if (L.Goal == DungeonGoal.None) objective.Clear();
        foreach (var g in objective) groups.Remove(g);

        // seal the arena before anything is placed, so nothing ends up standing in the doorway
        if (L.Goal != DungeonGoal.None) L.CloseBossDoor();

        switch (L.Goal)
        {
            case DungeonGoal.SlayElites:
                foreach (var g in objective)
                {
                    var leader = Elite(L, g.Pos, g.Count, true);
                    L.ObjMonsters.Add(leader);
                    L.AddMarker("objective", "被標記的菁英", g.Pos, leader);
                }
                L.ObjTotal = L.ObjMonsters.Count;
                break;
            case DungeonGoal.DestroyIdols:
            case DungeonGoal.FreePrisoners:
            {
                bool idols = L.Goal == DungeonGoal.DestroyIdols;
                var spots = ObjectiveSpots(L, objective, 4);
                Action counted = () => { L.ObjCount++; L.UpdateObjective(); };
                foreach (var s in spots)
                {
                    var it = idols ? WorldObjects.Idol(s, counted) : WorldObjects.Prisoner(s, counted, L.Rand);
                    L.AddMarker("objective", it.Label, s, it);
                }
                L.ObjTotal = spots.Count;
                // the same planner the hero uses for a click must find a way to each one (diagnostic for the log)
                foreach (var s in spots)
                {
                    var path = L.Grid.Path(L.Checkpoint, s);
                    float end = path.Count > 0 ? path[^1].DistanceTo(s) : float.MaxValue;
                    if (path.Count == 0 || end > 3f) Log.Warn("level", $"objective at ({s.X:0},{s.Y:0}) in {L.AreaId}: the hero's path planner finds no way there (points={path.Count}, ends {end:0.0} m away)");
                    else Log.Info("level", $"objective at ({s.X:0},{s.Y:0}): path of {path.Count} points");
                }
                foreach (var g in objective) StrongholdPack(L, g.Pos, g.Count);  // the guards
                foreach (var s in spots)
                {
                    bool guarded = false;
                    foreach (var g in objective) if (g.Pos.DistanceTo(s) < 9f) { guarded = true; break; }
                    if (!guarded) Pack(L, s, 4);
                }
                break;
            }
            case DungeonGoal.CollectEmbers:
            {
                var carriers = new List<Monster>();
                foreach (var g in objective) Pack(L, g.Pos, g.Count + 1, true, carriers);
                int need = Math.Clamp((int)MathF.Round(carriers.Count * 0.7f), Math.Min(4, carriers.Count), 14);
                L.EmbersNeeded = need; L.ObjTotal = need;
                if (need > 0)
                {
                    Vector2 at = NookNear(L, L.DoorOutside, 2);
                    var altar = WorldObjects.EmberAltar(at, () => L.Embers >= L.EmbersNeeded, () => { L.EmbersOffered = true; L.UpdateObjective(); });
                    L.AddMarker("objective", altar.Label, at, altar);
                }
                break;
            }
        }
        foreach (var g in groups) SpawnGroupOf(L, g);

        Monster boss = null;
        if (d.Story)
        {
            var def = MonsterDb.Get(reg?.Boss);
            if (def != null) boss = SpawnOne(L, def, MonTier.Boss, L.ArenaPos, L.NextPack());
            else Log.Error("populate", $"story dungeon {d.Id}: boss '{reg?.Boss}' is not defined");
        }
        boss ??= MiniBoss(L, MonsterDb.Get(d.MiniBoss), L.ArenaPos, 3, $"{d.Name}之主");
        boss.FaceDir(L.DoorOutside - L.ArenaPos);
        L.SetBoss(boss, d.Story && boss.Brain != null);

        if (!data.All("exit").Any())                                             // always a way back out
        {
            Vector2 start = StartOf(L);
            Vector2 at = L.StandNear(start, start + (start - L.ArenaPos), 3.2f);
            var e = WorldObjects.Exit(at, reg?.Id ?? ZoneDb.Town);
            L.AddMarker("exit", e.Label, at, e);
        }
        CommonObjects(L, true);
    }

    // ───────────────────────── rift ─────────────────────────

    private static void Rift(Level L)
    {
        var data = L.Data; var rng = L.Rand; var ro = RosterOf(L.SpawnRegion);
        SetArena(L);
        L.CloseBossDoor();
        float points = 0f;
        foreach (var g in data.Spawns)
        {
            if (g.Pos.DistanceTo(L.ArenaPos) <= L.ArenaRadius + 2f || g.Pos.DistanceTo(L.Checkpoint) <= 11f) continue;
            switch (g.Kind)
            {
                case "objective":
                case "elite":
                {
                    var list = new List<Monster>();
                    Elite(L, g.Pos, g.Count, false, list);
                    points += 8f + (list.Count - 1);
                    break;
                }
                case "champions": points += 3f * Champions(L, g.Pos, g.Count).Count; break;
                default: points += Pack(L, g.Pos, g.Count + (L.RiftTier >= 5 ? 1 : 0)).Count; break;
            }
        }
        L.RiftNeed = Mathf.Max(10f, points * 0.6f);
        if (points < 1f) L.RiftProgress = 1f;                                    // nothing to hunt: the guardian is the whole rift

        var guardian = MiniBoss(L, Pick(ro.Bruiser.Count > 0 ? ro.Bruiser : ro.Leaders, rng), L.ArenaPos, 3, "裂隙守衛");
        guardian.FaceDir(L.DoorOutside - L.ArenaPos);
        L.SetBoss(guardian, false);

        Vector2 start = StartOf(L);
        Vector2 at = L.StandNear(start, start + (start - L.ArenaPos), 3.2f);
        string from = L.AreaId;
        var portal = WorldObjects.Portal(at, "離開裂隙", $"返回{ZoneDb.TownName}", new Color(0.45f, 0.75f, 1f), _ => Game.I.EnterArea(ZoneDb.Town, from));
        L.AddMarker("exit", portal.Label, at, portal);
        CommonObjects(L, true);
        Log.Info("rift", $"tier {L.RiftTier}: theme={L.Theme?.Id} roster={L.SpawnRegion?.Id} points={points:F0} need={L.RiftNeed:F0} mul={L.RiftMul:F2}");
    }
}

