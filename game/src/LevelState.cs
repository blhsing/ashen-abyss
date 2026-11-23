using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

// ═════════════════════════ saved data ═════════════════════════

/// <summary>
/// The state of the area the hero is in, so loading a save resumes exactly there: the same layout (seed and monster
/// level), the hero's spot, facing, life and cooldowns, which monsters are dead, which objects are spent, ground loot,
/// the dungeon objective, the boss and its door, the explored map. Plain fields, serialised with the save (JSON).
/// Also used for the town-portal round trip within a session (the dungeon is as the hero left it).
/// </summary>
public sealed class LevelSnapshot
{
    public int Ver = 1;
    public string Area;
    public int Seed, MonsterLevel;
    public float BuildX, BuildZ;                 // the checkpoint the level was populated with (spawn spacing depends on it)
    public float CheckX, CheckZ;                 // the checkpoint now (moves to the boss door / the beacon)
    public float X, Z, Facing;
    public float LifeFrac = 1f, Resource, EvadeCd, SecondWindCd;
    public int Pips, Potions = -1;
    public List<CdRec> Cds = new();
    public int RosterCount, ObjCountAll;         // how many monsters / objects Populate placed (layout check)
    public string RosterSig = "";
    public List<MonRec> DeadMons = new();
    public List<ObjRec> Spent = new();
    public List<LootRec> Loot = new();
    public List<GoldRec> Gold = new();
    public List<ObjRec> Shards = new();          // 燼晶 lying on the ground
    public List<ObjRec> Chests = new();          // unopened reward chests (boss, event, rift); K = quality
    public int ObjCount, Embers;
    public bool EmbersOffered, DoorOpen, BossDead;
    public bool LibAtBuild;                      // the overworld stronghold was already free when the level was built (Populate differs)
    public float BossX, BossZ;
    public float RiftTimeLeft, RiftPoints; public bool RiftFailed;
    public string Fog;                           // explored cells, one bit each, base64 (dungeons and rifts; town and regions: SaveData.Fog)
    public int FogLen;
    public List<HurtRec> Hurt = new();           // living monsters Populate placed that are wounded (not bosses, not named)
    public List<int> EventsDone = new();         // world events (Level.Events order) already finished
    public bool MidBoss;                         // saved during a boss fight: the hero was put back at the checkpoint, the boss is whole again

    public sealed class CdRec { public string Id; public float Left, Max; public int C, MC; }
    /// <summary>A monster Populate placed: I = order of placement, D = type id, X/Z = where it was placed.</summary>
    public sealed class MonRec { public int I; public string D; public float X, Z; }
    /// <summary>An object Populate placed (I = order, K = kind) or a dynamic one (I = -1).</summary>
    public sealed class ObjRec { public int I = -1; public string K; public float X, Z; }
    public sealed class LootRec { public Item It; public float X, Z; }
    public sealed class GoldRec { public int A; public float X, Z; }
    /// <summary>A wounded monster: I/D/X/Z as in MonRec, F = life fraction.</summary>
    public sealed class HurtRec { public int I; public string D; public float X, Z, F; }
}

/// <summary>An open town portal (saved while the hero stands in town) and the state of the place it leads back to.</summary>
public sealed class PortalSnapshot { public string Area; public float X, Z; public int Seed; public LevelSnapshot State; }

/// <summary>Explored map of an area other than the current one (town and overworld keep their layout).</summary>
public sealed class FogRec { public int Seed, Len; public string Bits; }

public sealed partial class SaveData
{
    /// <summary>Where and how the game left off (null in saves older than format 2: those wake at a portal, as before).</summary>
    public LevelSnapshot Resume;
    public PortalSnapshot Portal;
    public Dictionary<string, FogRec> Fog = new();
}

// ═════════════════════════ capture / restore ═════════════════════════

/// <summary>
/// Records what Populate placed in a level (monsters and objects in placement order, with type and position), captures
/// the level into a <see cref="LevelSnapshot"/> and puts a snapshot back after the same level was rebuilt from the same
/// seed. Placement is deterministic for a seed (Populate draws from Level.Rand only), so the n-th monster of the rebuilt
/// level is the n-th of the saved one; type and position are checked as well, and a mismatch falls back to the nearest
/// monster of the same type, or is skipped.
/// </summary>
public static class LevelState
{
    private const string Cat = "levelstate";
    /// <summary>
    /// Feature switch for the exact save resume (ON since 0.4.0; the save tests are Y:/tools/gameplay/run_save.sh). Off:
    /// saves keep format 1 (no Resume / Portal written), loading wakes at the portal as before and the town portal back
    /// rebuilds the place afresh (the old behaviour). The placement is recorded on every level either way (cheap).
    /// </summary>
    public static bool Enabled = true;
    /// <summary>Set by Game before Level.Build: applied once to the level with the same area id and seed.</summary>
    public static LevelSnapshot Pending;
    /// <summary>The hero's life fraction right after the last resume (tests: monsters may hit the hero on the next frames).</summary>
    public static float ResumedLife = -1f;

    private static Level _lv;
    private static Monster[] _mons = Array.Empty<Monster>();
    private static Vector2[] _monPos = Array.Empty<Vector2>();
    private static string[] _monDef = Array.Empty<string>();
    private static bool[] _monEvent = Array.Empty<bool>();
    private static Interactable[] _objs = Array.Empty<Interactable>();
    private static Vector2[] _objPos = Array.Empty<Vector2>();
    private static string[] _objKind = Array.Empty<string>();
    private static readonly HashSet<int> _deadRestored = new(), _spentRestored = new();
    private static string _sig = "";

    /// <summary>Objects whose used state is kept (altars are kept by save flags; travel objects and NPCs have none).</summary>
    private static bool Tracked(string kind) => kind is "chest" or "shrine" or "breakable" or "idol" or "prisoner" or "emberaltar";

    private static bool Matches(LevelSnapshot s, Level L) => s != null && L != null && s.Area == L.AreaId && s.Seed == L.Seed;

    private static LevelSnapshot _apply;        // the snapshot waiting for its level to be ready (OnArrived)
    private static Level _applyTo;

    /// <summary>Level.Build, before population: a pending snapshot fixes the monster level and the checkpoint the level was first populated with.</summary>
    public static void BeforePopulate(Level L)
    {
        var s = Pending;
        if (!Matches(s, L)) return;
        if (s.MonsterLevel > 0) L.MonsterLevel = s.MonsterLevel;
        L.Checkpoint = new Vector2(s.BuildX, s.BuildZ);
        L.BuildCheckpoint = L.Checkpoint;
    }

    /// <summary>
    /// SAVE-HOOK for Populate.Overworld: whether the stronghold counts as free while the level is populated. A resumed save
    /// rebuilds the camp as it was when the level was first built (the restore frees it again afterwards, without fanfare),
    /// so the same monsters are placed in the same order.
    /// </summary>
    public static bool LiberatedForBuild(Level L, bool now) => Matches(Pending, L) ? Pending.LibAtBuild : now;

    /// <summary>Level._Ready, right after Populate: keeps a pending snapshot for <see cref="OnArrived"/>.</summary>
    public static void OnPopulated(Level L)
    {
        L.LiberatedAtBuild = L.Liberated;
        _lv = null;                                  // nothing is recorded for this level until it is complete
        var s = Pending;
        Pending = null;
        _apply = null; _applyTo = null;
        if (s == null) return;
        if (!Matches(s, L)) { Log.Warn(Cat, $"snapshot for {s.Area}/{s.Seed} not applied to {L.AreaId}/{L.Seed}"); return; }
        _apply = s; _applyTo = L;
    }

    /// <summary>
    /// Game.BuildArea, after the "area" event (the named layer has put its named monsters in by then): records what the
    /// level holds and applies a pending snapshot.
    /// </summary>
    public static void OnArrived(Level L)
    {
        if (L == null || !GodotObject.IsInstanceValid(L)) return;
        _lv = L;
        _deadRestored.Clear(); _spentRestored.Clear();
        var mons = World.Monsters.Where(m => m != null && GodotObject.IsInstanceValid(m) && !m.Dead && L.IsAncestorOf(m)).ToArray();
        _mons = mons;
        _monPos = mons.Select(m => m.Pos2).ToArray();
        _monDef = mons.Select(m => m.Def?.Id ?? m.Name.ToString()).ToArray();
        // monsters of a world event belong to the event, which starts afresh on every load
        _monEvent = mons.Select(m => L.Events.Any(e => e.Pos.DistanceTo(m.Pos2) < 7.5f)).ToArray();
        var objs = Interactable.All.Where(i => i != null && GodotObject.IsInstanceValid(i) && L.IsAncestorOf(i)).ToArray();
        _objs = objs;
        _objPos = objs.Select(i => i.Pos2).ToArray();
        _objKind = objs.Select(i => i.Kind ?? "").ToArray();
        unchecked
        {
            uint h = 2166136261u;
            for (int i = 0; i < mons.Length; i++)
            {
                foreach (char c in _monDef[i]) { h ^= c; h *= 16777619u; }
                h ^= (uint)Mathf.RoundToInt(_monPos[i].X * 10f); h *= 16777619u;
                h ^= (uint)Mathf.RoundToInt(_monPos[i].Y * 10f); h *= 16777619u;
            }
            _sig = h.ToString("x8");
        }
        string props = string.Join(" ", (L.Data?.Props ?? new List<PropSlot>()).Take(3).Select(p => $"({p.Pos.X:0.00},{p.Pos.Y:0.00})"));
        Log.Info(Cat, $"placed area={L.AreaId} seed={L.Seed} mlvl={L.MonsterLevel} monsters={mons.Length} objects={objs.Length} roster={_sig} props={props}");

        var s = _applyTo == L ? _apply : null;
        _apply = null; _applyTo = null;
        if (s == null) return;
        var h0 = Game.I?.Hero;
        bool relib = !s.LibAtBuild && L.Region != null && L.Dungeon == null && !L.IsRift && h0 != null && h0.D.Has("sh_" + L.Region.Id);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { Apply(L, s); if (relib) L.RestoreLiberated(); Log.Info(Cat, $"restore took {sw.Elapsed.TotalMilliseconds:0.0} ms"); }
        catch (Exception e) { Log.Error(Cat, $"restoring {L.AreaId} failed part-way; the level stays as built", e); }
    }

    private static bool Ours(Level L) => L != null && ReferenceEquals(_lv, L) && GodotObject.IsInstanceValid(L);

    /// <summary>Areas whose layout never changes for a character (town, overworld regions): their explored map lives in SaveData.Fog.</summary>
    public static bool StableFog(string area) => area == ZoneDb.Town || ZoneDb.Region(area) != null;

    // ───────────────────────── capture ─────────────────────────

    /// <summary>The state of level <paramref name="L"/> (null when it is not ready or was not recorded).</summary>
    public static LevelSnapshot Capture(Level L, Player p, int seed)
    {
        if (L == null || !GodotObject.IsInstanceValid(L) || !L.Built || !Ours(L) || L.Grid == null) return null;
        var s = new LevelSnapshot
        {
            Area = L.AreaId, Seed = seed != 0 ? seed : L.Seed, MonsterLevel = L.MonsterLevel,
            BuildX = L.BuildCheckpoint.X, BuildZ = L.BuildCheckpoint.Y, CheckX = L.Checkpoint.X, CheckZ = L.Checkpoint.Y,
            RosterCount = _mons.Length, ObjCountAll = _objs.Length, RosterSig = _sig,
            ObjCount = L.ObjCount, Embers = L.Embers, EmbersOffered = L.EmbersOffered, DoorOpen = L.DoorOpened, BossDead = L.BossDead,
            BossX = L.BossFellAt.X, BossZ = L.BossFellAt.Y,
            RiftTimeLeft = L.RiftTimeLeft, RiftPoints = L.RiftPoints, RiftFailed = L.RiftFailed,
            LibAtBuild = L.LiberatedAtBuild,
        };
        if (s.Seed != L.Seed) s.Seed = L.Seed;
        // the hero
        Vector2 at = L.Checkpoint;
        if (p != null && GodotObject.IsInstanceValid(p) && p.IsInsideTree() && !p.Dead)
        {
            at = p.Pos2; s.Facing = p.Facing;
            s.LifeFrac = Mathf.Clamp(p.LifeFrac, 0.05f, 1f); s.Resource = p.Resource; s.Pips = p.Pips; s.Potions = p.Potions;
            s.EvadeCd = p.EvadeCd; s.SecondWindCd = p.SecondWindCd;
            foreach (var kv in p.Cds) s.Cds.Add(new LevelSnapshot.CdRec { Id = kv.Key, Left = kv.Value.Left, Max = kv.Value.Max, C = kv.Value.Charges, MC = kv.Value.MaxCharges });
        }
        // Saved mid-fight with the level's boss: the boss comes back whole (as after a death, Level.ResetBossFight) and the
        // hero wakes at the checkpoint (outside the opened boss door) rather than inside the arena with the fight on.
        // A named encounter that is not the level's boss: the hero is put just outside its arena (the encounter restarts).
        if (!L.BossDead && L.BossEngaged && L.BossMon != null && GodotObject.IsInstanceValid(L.BossMon) && !L.BossMon.Dead)
        {
            s.MidBoss = true; at = L.Checkpoint;
        }
        else
        {
            foreach (var e in NamedHub.Live)
            {
                if (e == null || !GodotObject.IsInstanceValid(e) || !e.Engaged || !e.Alive || e.LevelManaged || e.L != L) continue;
                if (at.DistanceTo(e.Center) > e.ArenaR + 3f) continue;
                Vector2 dir = at - e.Center; if (dir.LengthSquared() < 0.01f) dir = L.Checkpoint - e.Center;
                if (dir.LengthSquared() < 0.01f) dir = Vector2.Right;
                at = L.Grid.NearestWalk(e.Center + dir.Normalized() * (e.ArenaR + 8f));
                s.MidBoss = true;
                break;
            }
        }
        s.X = at.X; s.Z = at.Y;
        // monsters Populate placed that are gone, and the wounded ones (not bosses or named monsters: those come back whole)
        for (int i = 0; i < _mons.Length; i++)
        {
            if (_monEvent[i]) continue;
            var m = _mons[i];
            bool dead = _deadRestored.Contains(i) || !GodotObject.IsInstanceValid(m) || m.Dead || !m.IsInsideTree();
            if (dead) { s.DeadMons.Add(new LevelSnapshot.MonRec { I = i, D = _monDef[i], X = _monPos[i].X, Z = _monPos[i].Y }); continue; }
            if (m.IsBoss || ReferenceEquals(m, L.BossMon) || NamedHub.Of(m) != null || m.MaxLife <= 0f || m.Life >= m.MaxLife - 0.5f) continue;
            s.Hurt.Add(new LevelSnapshot.HurtRec { I = i, D = _monDef[i], X = _monPos[i].X, Z = _monPos[i].Y, F = Mathf.Clamp(m.Life / m.MaxLife, 0.01f, 1f) });
        }
        for (int i = 0; i < L.Events.Count; i++) if (L.Events[i] is { Done: true }) s.EventsDone.Add(i);
        // objects used up
        for (int i = 0; i < _objs.Length; i++)
        {
            if (!Tracked(_objKind[i])) continue;
            var o = _objs[i];
            bool spent = _spentRestored.Contains(i) || !GodotObject.IsInstanceValid(o) || !o.CanUse || !o.IsInsideTree();
            if (spent) s.Spent.Add(new LevelSnapshot.ObjRec { I = i, K = _objKind[i], X = _objPos[i].X, Z = _objPos[i].Y });
        }
        // ground loot, 燼晶 and reward chests that appeared since
        foreach (var it in Interactable.All)
        {
            if (it == null || !GodotObject.IsInstanceValid(it) || it.IsQueuedForDeletion() || !L.IsAncestorOf(it)) continue;
            Vector2 q = it.Pos2;
            switch (it.Kind)
            {
                case "loot": if (it.Item != null) s.Loot.Add(new LevelSnapshot.LootRec { It = it.Item, X = q.X, Z = q.Y }); break;
                case "gold": if (it.CanUse && it.Tag is int a && a > 0) s.Gold.Add(new LevelSnapshot.GoldRec { A = a, X = q.X, Z = q.Y }); break;
                case "ember": if (it.CanUse) s.Shards.Add(new LevelSnapshot.ObjRec { K = "ember", X = q.X, Z = q.Y }); break;
            }
        }
        foreach (var (chest, quality) in L.RewardChests)
            if (chest != null && GodotObject.IsInstanceValid(chest) && chest.CanUse && !chest.IsQueuedForDeletion())
                s.Chests.Add(new LevelSnapshot.ObjRec { K = quality.ToString(), X = chest.Pos2.X, Z = chest.Pos2.Y });
        // the explored map: dungeons and rifts here; the town and the regions keep theirs in SaveData.Fog (Game.KeepStableFog)
        if (L.Seen != null && !StableFog(L.AreaId)) { s.Fog = PackBits(L.Seen); s.FogLen = L.Seen.Length; }
        return s;
    }

    // ───────────────────────── restore ─────────────────────────

    private static int FindMon(LevelSnapshot.MonRec r, HashSet<int> used)
    {
        var at = new Vector2(r.X, r.Z);
        if (r.I >= 0 && r.I < _mons.Length && !used.Contains(r.I) && _monDef[r.I] == r.D && _monPos[r.I].DistanceTo(at) < 0.75f) return r.I;
        int best = -1; float bd = 1.5f;
        for (int i = 0; i < _mons.Length; i++)
        {
            if (used.Contains(i) || _monDef[i] != r.D) continue;
            float d = _monPos[i].DistanceTo(at);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    private static int FindObj(LevelSnapshot.ObjRec r, HashSet<int> used)
    {
        var at = new Vector2(r.X, r.Z);
        if (r.I >= 0 && r.I < _objs.Length && !used.Contains(r.I) && _objKind[r.I] == r.K && _objPos[r.I].DistanceTo(at) < 0.6f) return r.I;
        int best = -1; float bd = 1.0f;
        for (int i = 0; i < _objs.Length; i++)
        {
            if (used.Contains(i) || _objKind[i] != r.K) continue;
            float d = _objPos[i].DistanceTo(at);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    /// <summary>Takes a monster out of the level as if it had died long ago: no loot, experience, kill events or death animation.</summary>
    private static void Remove(Level L, Monster m)
    {
        if (m == null || !GodotObject.IsInstanceValid(m)) return;
        L.RemoveMarker(m);
        m.Dead = true;
        World.Monsters.Remove(m);
        m.GetParent()?.RemoveChild(m);
        m.QueueFree();
    }

    private static void Apply(Level L, LevelSnapshot s)
    {
        bool sameRoster = s.RosterCount == _mons.Length && s.RosterSig == _sig;
        if (!sameRoster) Log.Warn(Cat, $"{L.AreaId}: placement differs from the save (monsters {s.RosterCount}→{_mons.Length}, roster {s.RosterSig}→{_sig}); matching by type and position");
        // monsters
        var used = new HashSet<int>();
        int removed = 0, missed = 0;
        var boss = L.BossMon;
        foreach (var r in s.DeadMons)
        {
            int i = FindMon(r, used);
            if (i < 0) { missed++; continue; }
            used.Add(i);
            _deadRestored.Add(i);
            var m = _mons[i];
            if (ReferenceEquals(m, boss)) continue;          // the boss below
            Remove(L, m);
            removed++;
        }
        if (s.BossDead && boss != null && GodotObject.IsInstanceValid(boss))
        {
            for (int i = 0; i < _mons.Length; i++) if (ReferenceEquals(_mons[i], boss)) _deadRestored.Add(i);
            Remove(L, boss);
            L.RestoreBossDefeated(new Vector2(s.BossX, s.BossZ));
        }
        // wounded monsters keep their wounds (bosses and named monsters were never recorded: they come back whole)
        int hurt = 0;
        foreach (var r in s.Hurt)
        {
            if (r == null) continue;
            int i = FindMon(new LevelSnapshot.MonRec { I = r.I, D = r.D, X = r.X, Z = r.Z }, used);
            if (i < 0) { missed++; continue; }
            used.Add(i);
            var m = _mons[i];
            if (m == null || !GodotObject.IsInstanceValid(m) || m.Dead || m.IsBoss || ReferenceEquals(m, L.BossMon) || NamedHub.Of(m) != null) continue;
            m.Life = Mathf.Clamp(m.MaxLife * r.F, 1f, m.MaxLife);
            hurt++;
        }
        // world events already finished stay finished (one in progress starts afresh: its waves are not kept)
        int evDone = 0;
        foreach (int i in s.EventsDone)
            if (i >= 0 && i < L.Events.Count && L.Events[i] is { } ev && !ev.Done) { ev.RestoreDone(); evDone++; }
        // objects
        var usedObj = new HashSet<int>();
        int spent = 0;
        foreach (var r in s.Spent)
        {
            int i = FindObj(r, usedObj);
            if (i < 0) { missed++; continue; }
            usedObj.Add(i);
            _spentRestored.Add(i);
            var o = _objs[i];
            if (o != null && GodotObject.IsInstanceValid(o)) { o.RestoreSpent(); spent++; }
        }
        // the objective
        if (L.Goal is DungeonGoal.DestroyIdols or DungeonGoal.FreePrisoners) L.ObjCount = Math.Clamp(s.ObjCount, 0, Math.Max(L.ObjTotal, s.ObjCount));
        L.Embers = s.Embers; L.EmbersOffered = s.EmbersOffered;
        foreach (var r in s.Shards) WorldObjects.EmberShard(L.Grid.NearestWalk(new Vector2(r.X, r.Z)), L.AddEmber);
        if (s.DoorOpen && L.DoorSealed) L.OpenBossDoor(true);
        L.Checkpoint = L.Grid.NearestWalk(new Vector2(s.CheckX, s.CheckZ));
        if (L.IsRift) { L.RiftTimeLeft = s.RiftTimeLeft; L.RiftPoints = s.RiftPoints; L.RiftFailed = s.RiftFailed; L.RiftProgress = Mathf.Clamp(L.RiftPoints / Mathf.Max(1f, L.RiftNeed), 0f, 1f); }
        // what lies on the ground
        foreach (var r in s.Chests)
        {
            int.TryParse(r.K, out int q);
            L.RestoreRewardChest(new Vector2(r.X, r.Z), q);
        }
        foreach (var r in s.Loot) if (r.It != null) { r.It.Affixes ??= new(); r.It.Gems ??= new(); AshenAbyss.Loot.DropItem(r.It, new Vector2(r.X, r.Z)); }
        foreach (var r in s.Gold) AshenAbyss.Loot.DropGold(r.A, new Vector2(r.X, r.Z));
        // the explored map
        int fog = 0;
        if (L.Seen != null && s.Fog != null && s.FogLen == L.Seen.Length) fog = UnpackBitsInto(s.Fog, L.Seen);
        L.UpdateObjective();
        L.MarkDirty();
        Log.Info(Cat, $"restored area={L.AreaId} seed={L.Seed} roster={(sameRoster ? "same" : "differs")} dead={removed}{(s.BossDead ? "+boss" : "")} hurt={hurt} eventsDone={evDone}{(s.MidBoss ? " midBoss" : "")} spent={spent} missed={missed} " +
            $"objective={L.ObjCount}/{L.ObjTotal} embers={L.Embers}/{L.EmbersNeeded}{(L.EmbersOffered ? " offered" : "")} door={(L.DoorSealed ? "sealed" : "open")} " +
            $"loot={s.Loot.Count} gold={s.Gold.Count} shards={s.Shards.Count} chests={s.Chests.Count} fogCells={fog}");
    }

    // ───────────────────────── checking a loaded snapshot ─────────────────────────

    private static bool Fin(float v) => float.IsFinite(v);
    private static bool Fin(float a, float b) => float.IsFinite(a) && float.IsFinite(b) && Mathf.Abs(a) < 1e5f && Mathf.Abs(b) < 1e5f;

    /// <summary>
    /// A snapshot read from a save (possibly damaged or hand-edited): drops records that cannot be used and puts odd values
    /// back in range. False when the place itself cannot be rebuilt (no area, no seed, no usable spot): the caller then
    /// wakes the hero the old way. Never throws.
    /// </summary>
    public static bool Sanitize(LevelSnapshot s, out string why)
    {
        why = null;
        try
        {
            if (s == null) { why = "no state"; return false; }
            if (string.IsNullOrEmpty(s.Area) || s.Seed == 0) { why = "no area or seed"; return false; }
            if (!Fin(s.X, s.Z) || !Fin(s.BuildX, s.BuildZ) || !Fin(s.CheckX, s.CheckZ)) { why = "position unreadable"; return false; }
            int dropped = 0;
            int Drop<T>(List<T> list, Predicate<T> bad) { if (list == null) return 0; int n = list.RemoveAll(bad); dropped += n; return n; }
            s.Cds ??= new(); s.DeadMons ??= new(); s.Spent ??= new(); s.Loot ??= new(); s.Gold ??= new(); s.Shards ??= new(); s.Chests ??= new(); s.Hurt ??= new(); s.EventsDone ??= new();
            Drop(s.Cds, c => c == null || string.IsNullOrEmpty(c.Id) || !Fin(c.Left) || !Fin(c.Max));
            Drop(s.DeadMons, r => r == null || r.D == null || !Fin(r.X, r.Z));
            Drop(s.Hurt, r => r == null || r.D == null || !Fin(r.X, r.Z) || !Fin(r.F));
            Drop(s.Spent, r => r == null || r.K == null || !Fin(r.X, r.Z));
            Drop(s.Shards, r => r == null || !Fin(r.X, r.Z));
            Drop(s.Chests, r => r == null || !Fin(r.X, r.Z));
            Drop(s.Loot, r => r == null || r.It == null || !Fin(r.X, r.Z));
            Drop(s.Gold, r => r == null || r.A <= 0 || !Fin(r.X, r.Z));
            foreach (var r in s.Hurt) r.F = Mathf.Clamp(r.F, 0.01f, 1f);
            if (!Fin(s.Facing)) s.Facing = 0f;
            s.LifeFrac = Fin(s.LifeFrac) && s.LifeFrac > 0f && s.LifeFrac <= 1.001f ? Mathf.Clamp(s.LifeFrac, 0.05f, 1f) : 1f;
            if (!Fin(s.Resource)) s.Resource = 0f;
            if (!Fin(s.EvadeCd) || s.EvadeCd < 0f) s.EvadeCd = 0f;
            if (!Fin(s.SecondWindCd) || s.SecondWindCd < 0f) s.SecondWindCd = 0f;
            if (!Fin(s.BossX, s.BossZ)) { s.BossX = 0f; s.BossZ = 0f; }
            if (!Fin(s.RiftTimeLeft) || s.RiftTimeLeft < 0f) s.RiftTimeLeft = 0f;
            s.RiftTimeLeft = Mathf.Min(s.RiftTimeLeft, AshenAbyss.Level.RiftTime);
            if (!Fin(s.RiftPoints) || s.RiftPoints < 0f) s.RiftPoints = 0f;
            s.ObjCount = Math.Max(0, s.ObjCount); s.Embers = Math.Max(0, s.Embers);
            if (s.MonsterLevel < 0 || s.MonsterLevel > Balance.LevelCap + 20) s.MonsterLevel = 0;      // 0 = the level's own
            if (s.FogLen <= 0 || s.FogLen > 4_000_000) { s.Fog = null; s.FogLen = 0; }
            foreach (var l in s.Loot) { l.It.Affixes ??= new(); l.It.Gems ??= new(); l.It.Implicits ??= new(); }
            if (dropped > 0) why = $"{dropped} unreadable record(s) dropped";
            return true;
        }
        catch (Exception e) { why = "unreadable: " + e.Message; return false; }
    }

    /// <summary>Puts the saved hero state on a freshly made Player (position, facing, life, resource, cooldowns).</summary>
    public static void ApplyHero(Player p, LevelSnapshot s, Level L, bool town)
    {
        if (p == null || s == null) return;
        var grid = L?.Grid ?? World.Grid;
        Vector2 at = new(s.X, s.Z);
        if (grid != null && !grid.WalkAt(at)) at = grid.NearestWalk(at);
        p.Teleport(at);
        p.FaceDir(new Vector2(Mathf.Sin(s.Facing), Mathf.Cos(s.Facing)));
        foreach (var c in s.Cds) if (!string.IsNullOrEmpty(c.Id)) p.Cds[c.Id] = new Player.Cd { Left = c.Left, Max = c.Max, Charges = c.C, MaxCharges = Math.Max(1, c.MC) };
        p.EvadeCd = s.EvadeCd; p.SecondWindCd = s.SecondWindCd;
        p.Pips = Math.Clamp(s.Pips, 0, p.Hero.MaxPips);
        p.Resource = Mathf.Clamp(s.Resource, 0f, p.Hero.MaxResource);
        if (!town)
        {
            p.Life = Mathf.Max(1f, p.MaxLife * Mathf.Clamp(s.LifeFrac, 0.05f, 1f));
            if (s.Potions >= 0) p.Potions = Math.Clamp(s.Potions, 0, p.Hero.D.PotionCap);
        }
        ResumedLife = p.LifeFrac;
        Log.Info(Cat, $"hero resumed at ({at.X:0.00}, {at.Y:0.00}) facing={s.Facing:0.00} life={p.LifeFrac:P0} resource={p.Resource:0}");
    }

    // ───────────────────────── fog bits ─────────────────────────

    public static string PackBits(bool[] seen)
    {
        var bytes = new byte[(seen.Length + 7) / 8];
        for (int i = 0; i < seen.Length; i++) if (seen[i]) bytes[i >> 3] |= (byte)(1 << (i & 7));
        return Convert.ToBase64String(bytes);
    }

    public static bool[] UnpackBits(string b64, int len)
    {
        var res = new bool[len];
        UnpackBitsInto(b64, res);
        return res;
    }

    /// <summary>ORs the saved cells into <paramref name="into"/>; returns how many cells are explored afterwards.</summary>
    public static int UnpackBitsInto(string b64, bool[] into)
    {
        if (string.IsNullOrEmpty(b64) || into == null) return 0;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(b64); } catch (FormatException) { return 0; }
        int n = 0;
        for (int i = 0; i < into.Length; i++)
        {
            int b = i >> 3;
            if (b < bytes.Length && (bytes[b] & (1 << (i & 7))) != 0) into[i] = true;
            if (into[i]) n++;
        }
        return n;
    }
}
