using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// The monster roster's suites (run with --bestiary while the roster is not live by default):
///   census[:regions[:visits[:level]]]   builds every overworld, side dungeon and story dungeon of each region `visits` times
///                                       (and rifts), counts what Populate placed, and fails on every bestiary type or
///                                       reskin that never appeared (zero rows); also prints the density per area
///   bestiary:&lt;region|all&gt;[:shots]      every type of a region's families in a line-up: model / stand-in, clips, walk, attack
///                                       (with :shots and a window, close-up pictures, four types a frame)
///   ai[:names]                          every AI kind and trait in its own scene against a standing (deathless) hero: each must
///                                       raise its behaviour events (Monster.AiSeen); with --shot, a picture at each telegraph
/// </summary>
public sealed partial class TestSuite
{
    private IEnumerator RosterSuite()
    {
        if (_name == "rosterperf") { yield return RosterPerf(); yield break; }
        if (_name == "census" && !Bestiary.Live) { yield return CensusSuite(); yield break; }      // the old roster's density, for comparison
        if (!Bestiary.Live) { Fail($"{_name}: the bestiary is not live in this run (add --bestiary)"); yield break; }
        switch (_name)
        {
            case "census": yield return CensusSuite(); break;
            case "bestiary":
                if (_arg != null && _arg.StartsWith("creatures")) yield return CreatureSuite(_arg.Split(':', StringSplitOptions.TrimEntries));
                else yield return BestiarySuite();
                break;
            case "ai": yield return AiSuite(); break;
        }
    }

    private IEnumerator TravelTo(string area)
    {
        var before = G.Level;
        yield return Until(() => G.State != Game.GameState.Loading, 90, "the previous load");
        if (!G.HarnessTravel(area)) { Fail($"travel to {area} refused"); yield break; }
        yield return Until(() => G.State == Game.GameState.Play && G.Level != null && G.Level != before && G.Level.Built && GodotObject.IsInstanceValid(G.Level), 180, $"{area} built");
        yield return 0.2f;
    }

    // ═════════════════════════ cost of a crowd ═════════════════════════

    /// <summary>
    /// --suite=rosterperf[:region[:count]]: `count` (40) monsters of the region's spawn list fight a standing hero for 20 s;
    /// the [perf] lines between "perf window start" and "perf window end" give the frame and monster-code cost. Run once
    /// with --bestiary and once with --no-bestiary (same seed) to compare the designed roster with the old one.
    /// </summary>
    private IEnumerator RosterPerf()
    {
        var parts = _arg.Split(':', StringSplitOptions.TrimEntries);
        var reg = ZoneDb.Region(parts.Length > 0 && parts[0].Length > 0 ? parts[0] : "wastes") ?? ZoneDb.Regions[0];
        int count = parts.Length > 1 && int.TryParse(parts[1], out int c) ? c : 40;
        _section = "rosterperf";
        yield return StartHero(ClassId.Lampknight, 20, reg.Id, gear: true, god: true);
        var p = G.Player;
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 10);
        p.Teleport(arena);
        var rng = new Random(1234);
        var list = reg.Spawns.Select(s => (d: MonsterDb.Get(s.id), s.w)).Where(e => e.d != null).ToList();
        float total = list.Sum(e => e.w);
        var mons = new List<Monster>();
        for (int i = 0; i < count; i++)
        {
            float x = (float)rng.NextDouble() * total; MonsterDef def = list[^1].d;
            foreach (var e in list) { x -= e.w; if (x <= 0f) { def = e.d; break; } }
            float ang = (float)rng.NextDouble() * Mathf.Tau, r = 4f + 8f * (float)rng.NextDouble();
            var m = Monster.Spawn(def, 20, MonTier.Normal, World.Grid.NearestWalk(arena + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r), Faction.Monster);
            m.MaxLife = m.Life = 1e8f; m.NoLoot = true; m.AiWakeNow();
            mons.Add(m);
        }
        Log.Info("suite", $"rosterperf: {mons.Count} monsters of {reg.Id} ({(Bestiary.Live ? "bestiary" : "built-in roster")}): {string.Join(",", mons.GroupBy(m => m.Def.Id).Select(g => $"{g.Key}×{g.Count()}"))}");
        yield return 3f;
        Log.Info("suite", "perf window start");
        for (int i = 0; i < 40; i++) { p.Life = p.MaxLife; yield return 0.5f; }
        Log.Info("suite", "perf window end");
        Pass($"rosterperf: {mons.Count(m => GodotObject.IsInstanceValid(m) && !m.Dead)} monsters alive at the end");
        ClearField();
    }

    // ═════════════════════════ census ═════════════════════════

    private IEnumerator CensusSuite()
    {
        var parts = _arg.Split(':', StringSplitOptions.TrimEntries);
        var regions = parts.Length > 0 && parts[0].Length > 0 && parts[0] != "all"
            ? parts[0].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(ZoneDb.Region).Where(r => r != null).ToList()
            : ZoneDb.Regions.ToList();
        int visits = parts.Length > 1 && int.TryParse(parts[1], out int v) ? Math.Max(1, v) : 3;
        int level = parts.Length > 2 && int.TryParse(parts[2], out int lv) ? Math.Clamp(lv, 1, 50) : 14;
        _section = "census";
        yield return StartHero(ClassId.Emberblade, level, "town", gear: true, god: true);
        Census.Reset();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        foreach (var reg in regions)
        {
            var areas = new List<string> { reg.Id };
            areas.AddRange(reg.Dungeons.Select(d => d.Id));
            for (int i = 0; i < visits; i++)
                foreach (var a in areas)
                {
                    _section = $"census {a} #{i + 1}";
                    yield return TravelTo(a);
                }
            Log.Info("suite", $"census: {reg.Id} done ({clock.Elapsed.TotalSeconds:0} s so far)");
        }
        bool all = regions.Count == ZoneDb.Regions.Count;
        if (all)
            for (int i = 0; i < visits * 2; i++)
            {
                _section = $"census rift #{i + 1}";
                yield return TravelTo($"rift:{(i % 2 == 0 ? 1 : 6)}");
            }
        _section = "census: analysis";
        if (!Bestiary.Live)
        {
            foreach (var kv in Census.ByArea.OrderBy(k => k.Key)) Log.Info("suite", $"census area {kv.Key}: {kv.Value.Values.Sum()} monsters, {kv.Value.Count} types (built-in roster)");
            Pass($"census (built-in roster): {Census.Levels} levels, {Census.Monsters} monsters");
            yield break;
        }
        // per region: every type of its family table appears in the region's own areas
        foreach (var reg in regions)
        {
            var seen = new Dictionary<string, int>();
            foreach (var kv in Census.ByArea)
                if (kv.Key == reg.Id || reg.Dungeons.Any(d => d.Id == kv.Key))
                    foreach (var t in kv.Value) { seen.TryGetValue(t.Key, out int c); seen[t.Key] = c + t.Value; }
            int zero = 0, rows = 0;
            if (reg.Families == null) { Fail($"census {reg.Id}: no family table"); continue; }
            foreach (var (fid, w, sig, types) in reg.Families)
                foreach (var id in types)
                {
                    rows++;
                    seen.TryGetValue(id, out int n);
                    Log.Info("suite", $"census {reg.Id} {fid} {id} = {n}");
                    if (n == 0) { zero++; Fail($"census {reg.Id}: {id} ({fid}) never appeared in {visits} visits"); }
                }
            int areaMonsters = Census.ByArea.Where(kv => kv.Key == reg.Id).Sum(kv => kv.Value.Values.Sum());
            Log.Info("suite", $"census {reg.Id}: {rows - zero}/{rows} types seen; overworld monsters per visit {areaMonsters / (float)visits:0}");
            if (zero == 0) Pass($"census {reg.Id}: all {rows} types appeared");
        }
        if (all)
        {
            int missT = 0, missR = 0;
            foreach (var t in Bestiary.Types) if (!Census.Total.ContainsKey(t.Id)) { missT++; Fail($"census: type {t.Id} never appeared anywhere"); }
            foreach (var r in Bestiary.Reskins) if (!Census.Total.ContainsKey(r.Id)) { missR++; Fail($"census: reskin {r.Id} never appeared anywhere"); }
            Check(missT == 0, $"census: {Bestiary.Types.Count - missT}/{Bestiary.Types.Count} regular types appeared");
            Check(missR == 0, $"census: {Bestiary.Reskins.Count - missR}/{Bestiary.Reskins.Count} reskins appeared");
            if (Census.ByArea.TryGetValue("rift", out var rift)) Log.Info("suite", $"census rift: {rift.Count} types over {visits * 2} rifts: {string.Join(" ", rift.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
        foreach (var kv in Census.ByArea.OrderBy(k => k.Key))
            Log.Info("suite", $"census area {kv.Key}: {kv.Value.Values.Sum()} monsters, {kv.Value.Count} types");
        Log.Info("suite", $"census: {Census.Levels} levels, {Census.Monsters} monsters, {clock.Elapsed.TotalSeconds:0} s");
    }

    // ═════════════════════════ bestiary line-up ═════════════════════════

    private IEnumerator BestiarySuite()
    {
        var parts = _arg.Split(':', StringSplitOptions.TrimEntries);
        string which = parts.Length > 0 && parts[0].Length > 0 ? parts[0] : "all";
        bool shots = parts.Contains("shots");
        var regions = which == "all" ? ZoneDb.Regions.ToList() : ZoneDb.Regions.Where(r => r.Id == which).ToList();
        if (regions.Count == 0) { Fail($"bestiary: unknown region '{which}'"); yield break; }
        bool first = true;
        foreach (var reg in regions)
        {
            _section = $"bestiary {reg.Id}";
            if (first) yield return StartHero(ClassId.Lampknight, Math.Max(reg.Level, 10), reg.Id, gear: true, god: true);
            else yield return TravelTo(reg.Id);
            first = false;
            var p = G.Player;
            ClearField();
            Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 10);
            p.Teleport(arena); p.FaceDir(Vector2.Down);
            yield return 0.4f;
            var ids = reg.Families?.SelectMany(f => f.types).Distinct().ToList() ?? new List<string>();
            const int batch = 4;
            for (int b0 = 0; b0 < ids.Count; b0 += batch)
            {
                ClearField();
                p.Teleport(arena); p.Life = p.MaxLife;
                yield return null;
                var group = ids.Skip(b0).Take(batch).Select(MonsterDb.Get).Where(d => d != null).ToList();
                var mons = new List<Monster>();
                for (int i = 0; i < group.Count; i++)
                {
                    float ang = Mathf.DegToRad(-45f + 90f * (group.Count == 1 ? 0.5f : i / (float)(group.Count - 1)));
                    Vector2 at = World.Grid.NearestWalk(arena + new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)) * 7f);
                    var m = Monster.Spawn(group[i], Math.Max(reg.Level, 10), MonTier.Normal, at, Faction.Monster);
                    m.MaxLife = 5e7f; m.Life = 5e7f; m.NoLoot = true;
                    m.FaceDir(arena - at);
                    mons.Add(m);
                }
                yield return 0.5f;
                if (shots) yield return Snap($"best_{reg.Id}_{b0 / batch}_wait", 30f);
                foreach (var m in mons) m.AiWakeNow();
                yield return 2.2f;
                if (shots) yield return Snap($"best_{reg.Id}_{b0 / batch}_fight", 24f);
                yield return 1.5f;
                foreach (var m in mons)
                {
                    if (!GodotObject.IsInstanceValid(m)) { Log.Info("suite", $"bestiary {reg.Id}: a monster of batch {b0 / batch} is gone (burst / split / hatch)"); continue; }
                    var mod = m.Model;
                    Log.Info("suite", $"bestiary {reg.Id} {m.Def.Id}: model {m.Def.Model} → file {mod?.FileId ?? "-"}{(mod != null && mod.IsStandIn ? " (stand-in)" : "")}{(mod != null && mod.OwnAnimations ? " own clips: " + mod.OwnAnimationNote : "")} " +
                        $"el={m.Def.Element} onHit={Bestiary.StatusText(m.Def.OnHit)}{(m.Def.OnHit2.HasValue ? "+" + Bestiary.StatusText(m.Def.OnHit2) : "")} clip={mod?.Current} {m.DebugState} {m.AiState}");
                    Check(mod != null && (mod.Anim != null || mod.IsPrimitive), $"bestiary {m.Def.Id}: has a model that animates", $"file {mod?.FileId}");
                }
            }
            ClearField();
        }
        G.HarnessFov(0f);
        Check(Log.ErrorCount == 0, "bestiary: no errors in the log", $"errors={Log.ErrorCount}");
    }

    // ═════════════════════════ AI behaviours ═════════════════════════

    private sealed class AiCase
    {
        public string Name, Type; public float Dist = 6f, Time = 12f, Kite;
        public string[] Expect;                        // AiSeen keys that must increase
        public string Shot;                            // the key at which to take a picture (a telegraph on screen)
        public Action<Monster, Player> Setup;          // extra preparation
        public Func<Monster, Player, IEnumerator> Drive; // what the hero does meanwhile
    }

    private static int Seen(string key) => Monster.AiSeen.TryGetValue(key, out int n) ? n : 0;

    private static Monster Near(string id, Vector2 at, int level, bool aggro = true)
    {
        var def = MonsterDb.Get(id);
        if (def == null) return null;
        var m = Monster.Spawn(def, level, MonTier.Normal, World.Grid.NearestWalk(at), Faction.Monster);
        m.NoLoot = true;
        if (aggro) m.AiWakeNow();
        return m;
    }

    private static IEnumerator HitRepeatedly(Monster m, Player p, Element el, float every, float mulOfLife, bool frontal = true, int times = 99)
    {
        for (int i = 0; i < times && GodotObject.IsInstanceValid(m) && !m.Dead; i++)
        {
            Vector2 dir = frontal ? (m.Pos2 - p.Pos2).Normalized() : -m.Forward;
            m.TakeHit(new Hit { Source = p, Amount = m.MaxLife * mulOfLife, El = el, Dir = dir });
            yield return every;
        }
    }

    private static List<AiCase> AiCases() => new()
    {
        // ── kinds ──
        new() { Name = "Charger", Type = "dusk_boar", Dist = 10f, Kite = 10f, Expect = new[] { "Charger.windup", "Charger.charge" }, Shot = "Charger.windup" },
        new() { Name = "Shieldbearer", Type = "bone_pallbearer", Dist = 2.5f, Time = 10f, Expect = new[] { "Shieldbearer.block", "Shieldbearer.guardbreak", "Shieldbearer.blockshot" },
            Drive = (m, p) => ShieldDrive(m, p) },
        new() { Name = "Burrower", Type = "sexton_digger", Dist = 8f, Time = 16f, Expect = new[] { "Burrower.emerge", "Burrower.dive" }, Shot = "Burrower.emerge" },
        new() { Name = "Burrower:stationary", Type = "char_root", Dist = 8f, Time = 12f, Expect = new[] { "Burrower.emerge" }, Shot = "Burrower.emerge" },
        new() { Name = "Bomber", Type = "reaver_firepot", Dist = 9f, Expect = new[] { "Bomber.throw", "Bomber.land" }, Shot = "Bomber.throw" },
        new() { Name = "Bomber:melee", Type = "hauler_hod", Dist = 2.5f, Expect = new[] { "Bomber.melee" } },
        new() { Name = "Healer", Type = "cult_censer", Dist = 10f, Time = 12f, Expect = new[] { "Healer.channel" }, Shot = "Healer.channel",
            Setup = (m, p) => { var a = Near("cultist", m.Pos2 + new Vector2(2f, 0f), m.Level); a.Life = a.MaxLife * 0.4f; } },
        new() { Name = "Reviver", Type = "bone_urnbearer", Dist = 10f, Time = 14f, Expect = new[] { "Reviver.cast", "Reviver.raised" }, Shot = "Reviver.cast",
            Drive = (m, p) => ReviveDrive(m, p) },
        new() { Name = "Spawner", Type = "sexton_matron", Dist = 9f, Time = 12f, Expect = new[] { "Spawner.spawn", "Spawner.release" }, Shot = "Spawner.spawn",
            Drive = (m, p) => KillAfter(m, 8f) },
        new() { Name = "Spawner:egg", Type = "shroud_matron", Dist = 9f, Time = 14f, Expect = new[] { "Spawner.egg", "Spawner.hatch" } },
        new() { Name = "Caller", Type = "husk_wailer", Dist = 9f, Time = 10f, Expect = new[] { "Caller.cast", "Caller.arrive" }, Shot = "Caller.cast" },
        new() { Name = "Caller:escort", Type = "reaver_houndmaster", Dist = 9f, Time = 10f, Expect = new[] { "Caller.cast" } },
        new() { Name = "Swarm", Type = "dusk_moths", Dist = 3f, Time = 8f, Expect = new[] { "Swarm.bite" },
            Setup = (m, p) => p.ApplyStatus(St.Burn(0.01f, 6f), m, 0.01f, Element.Fire), Drive = (m, p) => SwarmDrive(m, p) },
        new() { Name = "Turret", Type = "engine_heliostat", Dist = 10f, Time = 14f, Expect = new[] { "Turret.fire", "Turret.close" }, Shot = "Turret.fire",
            Drive = (m, p) => TurretDrive(m, p) },
        new() { Name = "Turret:shutter", Type = "forge_eye", Dist = 9f, Time = 10f, Expect = new[] { "Turret.fire" } },
        new() { Name = "Flyer", Type = "dusk_crow", Dist = 9f, Kite = 9f, Expect = new[] { "Flyer.windup", "Flyer.swoop" }, Shot = "Flyer.windup" },
        new() { Name = "Stalker", Type = "cult_assassin", Dist = 9f, Time = 14f, Expect = new[] { "Stalker.pounce", "Stalker.land" }, Shot = "Stalker.pounce" },
        // ── traits ──
        new() { Name = "Dormant:buried", Type = "bog_sleeper", Dist = 3.5f, Time = 6f, Expect = new[] { "Dormant.wake" }, Shot = "Dormant.wake" },
        new() { Name = "Dormant:statue", Type = "wax_sealed", Dist = 3.5f, Time = 6f, Expect = new[] { "Dormant.wake" } },
        new() { Name = "Dormant:ceiling", Type = "shroud_dropper", Dist = 3.5f, Time = 6f, Expect = new[] { "Dormant.wake", "Dormant.strike" }, Shot = "Dormant.wake" },
        new() { Name = "Hook", Type = "reaver_hook", Dist = 7f, Kite = 7f, Expect = new[] { "Hook.windup", "Hook.pull" }, Shot = "Hook.windup" },
        new() { Name = "Grab", Type = "hauler_collector", Dist = 2.5f, Time = 14f, Expect = new[] { "Grab.windup", "Grab.hold" }, Shot = "Grab.windup" },
        new() { Name = "Breath", Type = "rime_horn", Dist = 5f, Expect = new[] { "Breath.windup", "Breath.blow" }, Shot = "Breath.windup" },
        new() { Name = "Eruption", Type = "ash_weeper", Dist = 8f, Expect = new[] { "Eruption.cast", "Eruption.hit" }, Shot = "Eruption.cast" },
        new() { Name = "Shell", Type = "rime_sentry", Dist = 6f, Time = 8f, Expect = new[] { "Shell.fire", "Shell.broken" },
            Drive = (m, p) => HitRepeatedly(m, p, Element.Fire, 0.4f, 0.12f) },
        new() { Name = "Reform", Type = "ash_cast", Dist = 6f, Time = 9f, Expect = new[] { "Reform.pile", "Reform.stand" },
            Drive = (m, p) => HitRepeatedly(m, p, Element.Physical, 0.5f, 2f, true, 1) },
        new() { Name = "Flammable", Type = "char_walker", Dist = 6f, Time = 5f, Expect = new[] { "Flammable.lit" },
            Drive = (m, p) => HitRepeatedly(m, p, Element.Fire, 0.5f, 0.05f, true, 1) },
        new() { Name = "Aura", Type = "reaver_cagebearer", Dist = 8f, Time = 5f, Expect = new[] { "Aura.haste" },
            Setup = (m, p) => Near("reaver_cutter", m.Pos2 + new Vector2(1.5f, 0f), m.Level) },
        new() { Name = "Aura:chill", Type = "cf_hulk", Dist = 3f, Time = 5f, Expect = new[] { "Aura.chill" } },
        new() { Name = "SplitOnDeath", Type = "husk_huddle", Dist = 6f, Time = 3f, Expect = new[] { "SplitOnDeath.split" }, Drive = (m, p) => KillAfter(m, 0.5f) },
        new() { Name = "DeathBurst", Type = "wraith_gasp", Dist = 3f, Time = 3f, Expect = new[] { "DeathBurst.armed", "DeathBurst.burst" }, Drive = (m, p) => KillAfter(m, 0.5f) },
        new() { Name = "Escort", Type = "reaver_houndmaster", Dist = 9f, Time = 4f, Expect = new[] { "Escort.adopt" },
            Setup = (m, p) => { var e = new List<Monster> { Near("dusk_hound", m.Pos2 + new Vector2(1f, 0f), m.Level), Near("dusk_hound", m.Pos2 + new Vector2(-1f, 0f), m.Level) }; m.AdoptEscorts(e); } },
        new() { Name = "Absorb", Type = "hush_mouth", Dist = 8f, Time = 10f, Expect = new[] { "Absorb.open", "Absorb.close", "Absorb.burst" }, Shot = "Absorb.open",
            Drive = (m, p) => ShootAt(m, p, 0.3f) },
        new() { Name = "Drain", Type = "hush_blank", Dist = 2.5f, Time = 8f, Expect = new[] { "Drain.drain" }, Setup = (m, p) => p.Resource = p.Hero.MaxResource },
        new() { Name = "Extinguish", Type = "cult_snuffer", Dist = 2.5f, Time = 8f, Expect = new[] { "Extinguish.snuff" },
            Setup = (m, p) => GroundZone.SpawnPlayer(p, null, new ZoneSpec { El = Element.Fire, Radius = 3f, Dur = 9f, Coef = 0f }, p.Pos2 + (m.Pos2 - p.Pos2) * 0.5f, 0f, false) },
        new() { Name = "Pulse", Type = "bell_hanging", Dist = 5f, Expect = new[] { "Pulse.mark", "Pulse.ring" }, Shot = "Pulse.ring" },
        new() { Name = "Pulse:brute", Type = "bell_clapper", Dist = 2.8f, Time = 14f, Expect = new[] { "Pulse.ring" }, Shot = "Pulse.ring" },
        new() { Name = "Pulse:forge", Type = "forge_hammer", Dist = 2.8f, Time = 14f, Expect = new[] { "Pulse.ring" }, Shot = "Pulse.ring" },
        new() { Name = "Snare", Type = "shroud_weaver", Dist = 9f, Expect = new[] { "Snare.throw" } },
        new() { Name = "Return", Type = "dune_disc", Dist = 8f, Expect = new[] { "Return.throw", "Return.turn" }, Shot = "Return.throw" },
        new() { Name = "Trail", Type = "cf_spirit", Dist = 8f, Expect = new[] { "Trail.zone" } },
        new() { Name = "Trail:flyer", Type = "fiend_wing", Dist = 9f, Kite = 9f, Expect = new[] { "Flyer.swoop", "Trail.zone" } },
        new() { Name = "Empower", Type = "dune_masker", Dist = 9f, Time = 10f, Expect = new[] { "Empower.cast", "Empower.mask" },
            Setup = (m, p) => Near("sand_revenant", m.Pos2 + new Vector2(1.5f, 0f), m.Level) },
        new() { Name = "FrontArmour", Type = "sexton_beetle", Dist = 3f, Time = 3f, Expect = new[] { "FrontArmour.deflect" },
            Drive = (m, p) => HitRepeatedly(m, p, Element.Physical, 0.5f, 0.02f, true, 3) },
        new() { Name = "Reflect", Type = "dune_mirror", Dist = 6f, Time = 6f, Expect = new[] { "Reflect.reflect" }, Drive = (m, p) => ShootAt(m, p, 0.5f) },
        new() { Name = "Phasing", Type = "wraith_mourner", Dist = 6f, Time = 4f, Expect = Array.Empty<string>() },
        new() { Name = "Leaper", Type = "effigy_hound", Dist = 3f, Time = 6f, Expect = new[] { "Dormant.wake" } },
        new() { Name = "Exploder:trail", Type = "wax_wick", Dist = 7f, Time = 8f, Expect = new[] { "Trail.zone" } },
        new() { Name = "Charger:burst", Type = "rime_packbeast", Dist = 10f, Kite = 10f, Time = 12f, Expect = new[] { "Charger.charge" } },
    };

    /// <summary>The most open floor (largest clear square) and its half-size in cells.</summary>
    private static (Vector2, int) OpenSpot(NavGrid grid, Vector2 near, int maxCells)
    {
        int best = -1; float bestD = float.MaxValue; Vector2 at = near;
        for (int y = 0; y < grid.H; y++)
            for (int x = 0; x < grid.W; x++)
            {
                if (!grid.Clear(x, y)) continue;
                int r = 0;
                for (int rr = 1; rr <= maxCells; rr++)
                {
                    bool open = true;
                    for (int dy = -rr; dy <= rr && open; dy++)
                        for (int dx = -rr; dx <= rr; dx++)
                            if (!grid.Clear(x + dx, y + dy)) { open = false; break; }
                    if (!open) break;
                    r = rr;
                }
                var c = grid.Center(x, y);
                float d = c.DistanceSquaredTo(near);
                if (r > best || (r == best && d < bestD)) { best = r; bestD = d; at = c; }
            }
        return (at, best);
    }

    /// <summary>The hero keeps stepping away (out of reach) so that gap-closers and swoops have room to show themselves.</summary>
    private static IEnumerator Kite(Monster m, Player p, Vector2 arena, float keep)
    {
        for (int i = 0; i < 200 && GodotObject.IsInstanceValid(m) && !m.Dead; i++)
        {
            if (m.Pos2.DistanceTo(p.Pos2) < 4f)
            {
                Vector2 away = (p.Pos2 - m.Pos2).Normalized();
                Vector2 to = World.Grid.NearestWalk(m.Pos2 + away * keep);
                if (to.DistanceTo(arena) > 16f) to = World.Grid.NearestWalk(m.Pos2 - away * keep);
                p.Teleport(to);
                p.FaceDir(m.Pos2 - to);
            }
            yield return 0.3f;
        }
    }

    private static IEnumerator KillAfter(Monster m, float s)
    {
        yield return s;
        if (GodotObject.IsInstanceValid(m) && !m.Dead) m.TakeHit(new Hit { Source = World.P, Amount = m.MaxLife * 10f, NoReact = true });
    }

    private static IEnumerator ShieldDrive(Monster m, Player p)
    {
        yield return 0.8f;
        for (int i = 0; i < 3 && GodotObject.IsInstanceValid(m) && !m.Dead; i++)
        {
            m.TakeHit(new Hit { Source = p, Amount = m.MaxLife * 0.03f, Dir = (m.Pos2 - p.Pos2).Normalized() });      // frontal: blocked
            yield return 0.5f;
        }
        yield return ShootAt(m, p, 0.4f, 3);
        for (int i = 0; i < 3 && GodotObject.IsInstanceValid(m) && !m.Dead; i++)
        {
            m.TakeHit(new Hit { Source = p, Amount = m.MaxLife * 0.03f, Dir = m.Forward });                             // from behind
            yield return 0.3f;
        }
    }

    private static IEnumerator ShootAt(Monster m, Player p, float every, int times = 30)
    {
        var shot = SkillDb.Get("hu_shot");
        for (int i = 0; i < times && GodotObject.IsInstanceValid(m) && !m.Dead; i++)
        {
            if (shot != null)
            {
                Vector2 dir = (m.Pos2 - p.Pos2).Normalized();
                Projectile.SpawnPlayer(p, shot, p.Chest, dir, 0.3f, 1f, false);
            }
            yield return every;
        }
    }

    private static IEnumerator ReviveDrive(Monster m, Player p)
    {
        var sk = Near("skeleton", p.Pos2 + new Vector2(0f, 3f), m.Level);
        yield return 1.2f;
        if (GodotObject.IsInstanceValid(sk) && !sk.Dead) sk.TakeHit(new Hit { Source = p, Amount = sk.MaxLife * 10f });
    }

    private static IEnumerator SwarmDrive(Monster m, Player p)
    {
        yield return 1f;
        for (int i = 0; i < 6 && GodotObject.IsInstanceValid(m) && !m.Dead; i++)
        {
            m.TakeHit(new Hit { Source = p, Amount = m.MaxLife * 0.02f, Dir = (m.Pos2 - p.Pos2).Normalized() });
            yield return 0.5f;
        }
    }

    private static IEnumerator TurretDrive(Monster m, Player p)
    {
        yield return 6f;
        if (!GodotObject.IsInstanceValid(m)) yield break;
        p.Teleport(World.Grid.NearestWalk(m.Pos2 + (p.Pos2 - m.Pos2).Normalized() * 3f));
    }

    private IEnumerator AiSuite()
    {
        var only = _arg.Length > 0 ? _arg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s.ToLowerInvariant()).ToHashSet() : null;
        bool shots = !string.IsNullOrEmpty(G.ArgValue("shot"));
        _section = "ai: start";
        yield return StartHero(ClassId.Hunter, 20, "town", gear: true, god: true);
        var p = G.Player;
        Vector2 arena = Vector2.Zero; int clear = -1;
        // an open field: charges and swoops need 10-15 m of clear floor
        foreach (var area in new[] { "frost", "sands", "abyss", "sanctum", "wastes", "mire", "crypt" })
        {
            yield return TravelTo(area);
            ClearField();
            (arena, clear) = OpenSpot(World.Grid, G.Level.Checkpoint, 9);
            Log.Info("suite", $"ai: {area} open spot clearance {clear} cells");
            if (clear >= 6) break;
        }
        p = G.Player;
        int ran = 0;
        foreach (var c in AiCases())
        {
            if (only != null && !only.Contains(c.Name.ToLowerInvariant()) && !only.Contains(c.Name.Split(':')[0].ToLowerInvariant()) && !only.Contains(c.Type)) continue;
            _section = $"ai {c.Name}";
            ClearField();
            p.Teleport(arena); p.Life = p.MaxLife; p.ClearCc(); p.FaceDir(Vector2.Down);
            yield return 0.3f;
            var before = c.Expect.ToDictionary(k => k, Seen);
            int errs = Log.ErrorCount;
            var m = Near(c.Type, arena + new Vector2(0f, c.Dist), 20, aggro: !c.Name.StartsWith("Dormant") && c.Name != "Leaper");
            if (m == null) { Fail($"ai {c.Name}: type {c.Type} has no definition"); continue; }
            m.MaxLife *= 6f; m.Life = m.MaxLife;           // long enough to show every phase
            if (c.Name is "SplitOnDeath" or "DeathBurst" or "Reform") { m.MaxLife /= 6f; m.Life = m.MaxLife; }
            m.FaceDir(arena - m.Pos2);
            ran++;
            c.Setup?.Invoke(m, p);
            IEnumerator drive = c.Drive?.Invoke(m, p) ?? (c.Kite > 0f ? Kite(m, p, arena, c.Kite) : null);
            int shotBefore = c.Shot != null ? Seen(c.Shot) : 0;
            bool shotTaken = false;
            float t = 0f;
            var bg = drive != null ? new Stack<IEnumerator>(new[] { drive }) : null;
            float bgWait = 0f;
            while (t < c.Time && !c.Expect.All(k => Seen(k) > before[k]))
            {
                // the drive coroutine runs alongside, stepped here
                if (bg != null && bg.Count > 0)
                {
                    if (bgWait > 0f) bgWait -= 0.05f;
                    else
                    {
                        var top = bg.Peek();
                        if (!top.MoveNext()) bg.Pop();
                        else if (top.Current is IEnumerator sub) bg.Push(sub);
                        else if (top.Current is float f) bgWait = f;
                    }
                }
                if (shots && !shotTaken && c.Shot != null && Seen(c.Shot) > shotBefore)
                {
                    shotTaken = true;
                    yield return 0.25f;
                    yield return Snap($"ai_{c.Name.Replace(':', '_')}", 0f);
                }
                if (c.Name == "Stalker") p.FaceDir(Vector2.Up);      // looks away, so the pounce comes from outside its front
                p.Life = p.MaxLife;
                t += 0.05f;
                yield return 0.05f;
            }
            // a shot keyed on the last expected event: the loop above ends on that very event, before its picture
            if (shots && !shotTaken && c.Shot != null && Seen(c.Shot) > shotBefore)
            {
                shotTaken = true;
                yield return 0.25f;
                yield return Snap($"ai_{c.Name.Replace(':', '_')}", 0f);
            }
            var missing = c.Expect.Where(k => Seen(k) <= before[k]).ToList();
            string got = string.Join(" ", Monster.AiSeen.Where(kv => kv.Key.StartsWith(c.Name.Split(':')[0]) || c.Expect.Contains(kv.Key)).Select(kv => $"{kv.Key}={kv.Value}"));
            if (c.Name == "Phasing") Check(GodotObject.IsInstanceValid(m) && m.AiNoSeparation, "ai Phasing: the mourner ignores unit separation", m.AiState);
            else Check(missing.Count == 0, $"ai {c.Name} ({c.Type})", missing.Count > 0 ? $"missing {string.Join(",", missing)} after {t:0.0} s; seen {got}; {(GodotObject.IsInstanceValid(m) ? m.DebugState + " " + m.AiState : "gone")}" : $"{t:0.0} s; {got}");
            if (Log.ErrorCount != errs) Fail($"ai {c.Name}: {Log.ErrorCount - errs} errors in the log during the case");
            yield return 0.3f;
        }
        ClearField();
        G.HarnessFov(0f);
        Log.Info("suite", "ai seen: " + string.Join(" ", Monster.AiSeen.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}")));
        Check(ran > 0, $"ai: {ran} cases ran");
    }
}
