using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// Where named encounters are met. Runs on every area load (World.Event "area", after the level is populated):
/// · a region overworld: the stronghold's placeholder leader becomes <c>sh_&lt;region&gt;</c>, each of the region's four
///   rare elites rolls its chance to be out (fixed spot rule per rare), and a waking world boss opens its gateway;
/// · a side dungeon: the placeholder boss becomes <c>db_&lt;dungeon&gt;</c>;
/// · the town: the altar of the Ember Stair (燼階) beside the rift stone;
/// · the encounter areas "rift~arena:&lt;id&gt;" (test arena), "rift~wb:&lt;id&gt;" (a world boss's field) and
///   "rift~pin:&lt;rung&gt;" (a rung of the ladder), built by the level generator's arena layout (Level.Resolve hook).
/// Test switch: <c>--named=&lt;id&gt;</c> sends the hero to the test arena of that encounter once the first area is up;
/// <c>--named=&lt;id&gt;@route</c> to the encounter's own place in the world instead (the rare is forced out).
/// </summary>
public static class NamedAreas
{
    public const string Prefix = "rift~";
    public const float RareChance = 0.3f;

    // ── feature flags (PAUSE 2026-11-19, lead's order): what reaches normal play. Every encounter stays reachable through
    //    --named=<id> / --area=rift~arena:<id> / --suite=named whatever these say. ──
    /// <summary>Rare elites roll their chance in the region overworlds (tested: spawn, announce, fight in the overworld).</summary>
    public static bool LiveRares = true;
    /// <summary>The stronghold leader is the named boss (tested: sh_wastes killed in the overworld, the stronghold liberated).</summary>
    public static bool LiveStrongholds = true;
    /// <summary>OFF: side-dungeon bosses stay the old placeholders (the normal-play dungeon route was not smoke-tested).</summary>
    public static bool LiveDungeons = false;
    /// <summary>OFF: no world-boss gateways in the overworld (route not smoke-tested).</summary>
    public static bool LiveWorld = false;
    /// <summary>OFF: no Ember Stair altar in town (route and panel not tested on screen).</summary>
    public static bool LiveLadder = false;
    private static bool _testDone;
    private static string _forceRare;

    public static bool IsNamedArea(string areaId) => areaId != null && areaId.StartsWith(Prefix, StringComparison.Ordinal);
    public static string ArenaOf(string id) => Prefix + "arena:" + id;
    public static string FieldOf(string worldBossId) => Prefix + "wb:" + worldBossId;
    public static string RungArea(int rung) => Prefix + "pin:" + rung;

    /// <summary>The encounter an encounter area stands for, or null.</summary>
    public static NamedDef DefOfArea(string areaId)
    {
        if (!IsNamedArea(areaId)) return null;
        string key = areaId[Prefix.Length..];
        int colon = key.IndexOf(':');
        if (colon < 0) return null;
        string kind = key[..colon], arg = key[(colon + 1)..];
        return kind switch
        {
            "arena" or "wb" => NamedDb.Get(arg),
            "pin" => int.TryParse(arg, out int r) ? NamedDb.RungOf(r) : null,
            _ => null,
        };
    }

    public static string Name(string areaId)
    {
        var d = DefOfArea(areaId);
        if (d == null) return "深淵裂隙";
        if (areaId.Contains("~pin:")) return $"燼階・第{"一二三四五六七八"[Math.Clamp(d.Rung - 1, 0, 7)]}階";
        if (areaId.Contains("~wb:")) return WorldFieldName(d);
        return $"試煉場・{d.NameZh}";
    }

    private static string WorldFieldName(NamedDef d) => d.Id switch
    {
        "wb_barrowbearer" => "送葬行列之野", "wb_firstbearer" => "薪道渡口", "wb_daylessdisc" => "正午平原", "wb_breathofsilence" => "玄武岩劇場", _ => "世界首領之野",
    };

    // ───────────────────────── Level.Resolve hook ─────────────────────────

    /// <summary>Level.Resolve: what the generator is asked for when the area is an encounter area (null otherwise).</summary>
    public static LevelRequest Resolve(Level lv, string areaId, int seed)
    {
        if (!IsNamedArea(areaId)) return null;
        NamedDb.Ensure();
        var d = DefOfArea(areaId);
        string theme = d?.Theme ?? "crypt";
        if (LevelThemes.Get(theme) == null || theme == "wastes" || theme == "town") theme = "crypt";
        lv.SpawnRegion = ZoneDb.Region(d?.Region ?? "abyss") ?? ZoneDb.Regions[^1];
        bool big = d != null && d.Kind is NamedKind.World or NamedKind.Pinnacle;
        var r = new LevelRequest { AreaId = areaId, Seed = seed, ThemeId = theme, Kind = AreaKind.Arena, Size = big ? 56 : 46, Boss = true, Goal = DungeonGoal.None };
        Log.Info("named", $"encounter area {areaId}: {d?.Id ?? "?"} theme={theme} size={r.Size}");
        return r;
    }

    // ───────────────────────── area loads ─────────────────────────

    internal static void OnArea(Level L, string areaId)
    {
        if (L == null || !GodotObject.IsInstanceValid(L) || L.Grid == null) return;
        TestSwitch(areaId);
        if (IsNamedArea(L.AreaId)) { BuildArena(L); return; }
        if (L.IsTown) { if (LiveLadder) Ladder(L); return; }
        if (L.IsRift) return;
        if (L.Dungeon != null) { if (!L.Dungeon.Story && LiveDungeons) DungeonBoss(L); return; }
        if (L.Region != null)
        {
            if (LiveStrongholds) Stronghold(L);
            if (LiveRares) Rares(L);
            if (LiveWorld || WorldBossEvent.Force != null) WorldBossEvent.Offer(L);
        }
    }

    private static void TestSwitch(string areaId)
    {
        if (_testDone) return;
        var g = Game.I;
        string v = g?.ArgValue("named");
        if (string.IsNullOrEmpty(v)) { _testDone = true; return; }
        _testDone = true;
        bool route = v.EndsWith("@route", StringComparison.OrdinalIgnoreCase);
        string id = route ? v[..^6] : v;
        var d = NamedDb.Get(id);
        if (d == null) { Log.Error("named", $"--named={v}: no such encounter (ids: {string.Join(",", NamedDb.All.Where(n => n.Kind != NamedKind.Region).Select(n => n.Id))})"); return; }
        string dest;
        if (!route) dest = ArenaOf(id);
        else switch (d.Kind)
        {
            case NamedKind.Rare: _forceRare = id; dest = d.Region; break;
            case NamedKind.Stronghold: dest = d.Region; break;
            case NamedKind.Dungeon: dest = d.DungeonId; break;
            case NamedKind.World: WorldBossEvent.Force = id; dest = d.Region; break;
            default: dest = ZoneDb.Town; break;
        }
        if (dest == areaId) { Log.Info("named", $"--named={v}: already in {dest}"); return; }
        Log.Info("named", $"--named={v}: travelling to {dest}");
        TravelSoon(dest, 0);
    }

    private static void TravelSoon(string dest, int tries)
    {
        NamedDriver.Later(0.5, () =>
        {
            if (Game.I.HarnessTravel(dest)) return;
            if (tries < 20) TravelSoon(dest, tries + 1); else Log.Error("named", $"--named: could not travel to {dest}");
        });
    }

    // ── overworld: the stronghold's leader ──

    private static void Stronghold(Level L)
    {
        if (L.Liberated || L.StrongholdMobs.Count == 0) return;
        var n = NamedDb.StrongholdOf(L.Region.Id);
        if (n == null) return;
        int idx = L.StrongholdMobs.FindLastIndex(m => m != null && GodotObject.IsInstanceValid(m) && m.MiniBoss);
        if (idx < 0) return;
        var old = L.StrongholdMobs[idx];
        Vector2 at = old.Pos2;
        int pack = old.Pack;
        RemoveMonster(old);
        var e = NamedHub.Spawn(n, L, at, NamedHub.LevelFor(n, L.MonsterLevel), L.StrongholdPos, Mathf.Clamp(L.StrongholdRadius + 4f, 12f, 20f), pack: pack);
        if (e == null) return;
        L.StrongholdMobs[idx] = e.M;
        Log.Info("named", $"stronghold {L.Region.Stronghold}: leader replaced by {n.Id}");
    }

    // ── overworld: rare elites ──

    private static void Rares(Level L)
    {
        var rng = L.Rand ?? new Random();
        var used = new List<Vector2>();
        foreach (var n in NamedDb.Of(NamedKind.Rare).Where(x => x.Region == L.Region.Id).ToList())
        {
            bool forced = _forceRare == n.Id;
            double roll = rng.NextDouble();
            if (!forced && roll > RareChance) { Log.Info("named", $"rare {n.Id}: not out this visit (roll {roll:0.00})"); continue; }
            if (forced) _forceRare = null;
            Vector2? spot = forced ? NearHero(L) : SpotFor(L, n.Spot, used, rng);
            if (spot == null) { Log.Warn("named", $"rare {n.Id}: no spot for rule '{n.Spot}'"); continue; }
            used.Add(spot.Value);
            var e = NamedHub.Spawn(n, L, spot.Value, NamedHub.LevelFor(n, L.MonsterLevel), spot.Value, 14f, pack: L.NextPack());
            if (e == null) continue;
            L.AddMarker("boss", n.NameZh, spot.Value, e.M, true);
            Log.Info("named", $"rare {n.Id} is out at ({spot.Value.X:0},{spot.Value.Y:0}) rule={n.Spot}{(forced ? " (forced)" : "")}");
        }
    }

    private static Vector2? NearHero(Level L)
    {
        var p = World.P;
        Vector2 from = p != null ? p.Pos2 : L.Checkpoint;
        for (int i = 0; i < 20; i++)
        {
            var q = L.Grid.RandomWalkNear(from, 16f, 22f, L.Rand);
            if (L.ReachableSpot(q, 3, out var at)) return at;
        }
        return L.Grid.NearestWalk(from + new Vector2(14f, 0f));
    }

    /// <summary>A reachable spot for a rare elite by its rule (road, camp_north, open, grove, ledge, far, nook, shore).</summary>
    public static Vector2? SpotFor(Level L, string rule, List<Vector2> used, Random rng)
    {
        var grid = L.Grid; var deco = L.Data?.Deco;
        Vector2 start = L.Checkpoint;
        var cand = new List<(Vector2 p, float score)>();
        bool Ok(Vector2 p)
        {
            if (p.DistanceTo(start) < 30f) return false;
            if (L.StrongholdMobs.Count > 0 && p.DistanceTo(L.StrongholdPos) < L.StrongholdRadius + 10f && rule != "camp_north") return false;
            foreach (var u in used) if (u.DistanceTo(p) < 26f) return false;
            foreach (var mk in L.Markers) if ((mk.Kind == "entrance" || mk.Kind == "event" || mk.Kind == "waypoint") && mk.Pos.DistanceTo(p) < 12f) return false;
            return true;
        }
        int step = 2;
        for (int y = 2; y < grid.H - 2; y += step)
            for (int x = 2; x < grid.W - 2; x += step)
            {
                if (!grid.Clear(x, y)) continue;
                var p = grid.Center(x, y);
                if (!Ok(p)) continue;
                int i = y * grid.W + x;
                byte dc = deco != null && i < deco.Length ? deco[i] : (byte)0;
                int open = 0;
                for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) if (grid.Clear(x + dx, y + dy)) open++;
                float far = p.DistanceTo(start);
                float s = rule switch
                {
                    "road" => dc == 1 ? 10f + far * 0.02f : -1f,
                    "camp_north" => L.StrongholdMobs.Count > 0 || L.StrongholdPos != Vector2.Zero
                        ? (p.Y < L.StrongholdPos.Y - 8f && p.DistanceTo(L.StrongholdPos) is > 16f and < 34f ? 20f - Mathf.Abs(p.DistanceTo(L.StrongholdPos) - 24f) : -1f) : -1f,
                    "open" => open >= 24 ? open + far * 0.01f : -1f,
                    "grove" => dc == 2 ? 10f + open * 0.2f : -1f,
                    "ledge" => open is >= 12 and <= 19 ? far * 0.05f + 5f : -1f,
                    "far" => far,
                    "nook" => open is >= 9 and <= 15 ? 10f + far * 0.02f : -1f,
                    "shore" => dc == 5 ? 10f + far * 0.02f : -1f,
                    _ => open >= 20 ? 1f : -1f,
                };
                if (s > 0f) cand.Add((p, s + (float)rng.NextDouble() * 3f));
            }
        if (cand.Count == 0 && rule != "open") return SpotFor(L, "open", used, rng);
        if (cand.Count == 0) return null;
        cand.Sort((a, b) => b.score.CompareTo(a.score));
        int top = Math.Max(1, Math.Min(cand.Count, rule == "far" ? 4 : 12));
        for (int k = 0; k < top; k++)
        {
            var c = cand[rng.Next(top)];
            if (L.ReachableSpot(c.p, 3, out var at)) return at;
        }
        return null;
    }

    // ── side dungeon: its boss ──

    private static void DungeonBoss(Level L)
    {
        var n = NamedDb.DungeonBossOf(L.Dungeon.Id);
        var old = L.BossMon;
        if (n == null || old == null || !GodotObject.IsInstanceValid(old) || old.Brain != null) return;
        Vector2 at = old.Pos2, look = L.DoorOutside - L.ArenaPos;
        L.RemoveMarker(old);
        RemoveMonster(old);
        L.BossMon = null;
        var e = NamedHub.Spawn(n, L, at, NamedHub.LevelFor(n, L.MonsterLevel), L.ArenaPos, L.ArenaRadius, levelBoss: true);
        if (e?.M != null) { e.M.FaceDir(look); Log.Info("named", $"dungeon {L.Dungeon.Id}: boss replaced by {n.Id} (arena r={L.ArenaRadius:0})"); }
    }

    private static void RemoveMonster(Monster m)
    {
        if (m == null || !GodotObject.IsInstanceValid(m)) return;
        World.Monsters.Remove(m);
        m.Dead = true;
        m.QueueFree();
    }

    // ── encounter areas ──

    private static void BuildArena(Level L)
    {
        var d = DefOfArea(L.AreaId);
        var boss = L.Data.Find("boss");
        Vector2 centre = boss?.Pos ?? L.Grid.NearestWalk(L.Checkpoint + new Vector2(0f, -24f));
        float r = Mathf.Clamp(boss?.Radius ?? 12f, 10f, 18f);
        L.HasArena = true; L.ArenaPos = centre; L.ArenaRadius = r;
        Vector2 start = L.Checkpoint;
        string back = d?.Kind == NamedKind.World && d.Region != null ? d.Region : ZoneDb.Town;
        string from = L.AreaId;
        var exit = WorldObjects.Portal(L.StandNear(start, start + (start - centre), 3.2f), back == ZoneDb.Town ? $"返回{ZoneDb.TownName}" : $"返回{ZoneDb.AreaName(back)}", "傳送門",
            new Color(1f, 0.7f, 0.35f), _ => Game.I.EnterArea(back, from));
        L.AddMarker("exit", exit.Label, exit.Pos2, exit);
        if (d == null) { Log.Error("named", $"{L.AreaId}: no encounter behind this area"); return; }
        int heroLv = Game.I?.Hero?.D.Level ?? 1, tier = Game.I?.WorldTier ?? 0;
        int regionLv = d.Regions.Length > 0 ? d.Regions.Max(id => ZoneDb.Region(id)?.Level ?? 1) : 40;
        if (d.Kind == NamedKind.Pinnacle) regionLv = 50;
        int lv = NamedHub.LevelFor(d, Balance.AreaLevel(regionLv, heroLv, tier, d.Kind is NamedKind.Stronghold or NamedKind.Dungeon));
        bool test = L.AreaId.Contains("~arena:");
        var e = NamedHub.Spawn(d, L, centre, lv, centre, r, levelBoss: d.Boss, test: test);
        if (e == null) return;
        if (e.M != null) e.M.FaceDir(start - centre);
        if (!d.Boss) L.AddMarker("boss", d.NameZh, centre, e.M);
        Game.I?.Hud?.Toast(d.Kind == NamedKind.World ? $"大地深處傳來回響——{d.NameZh}" : d.Kind == NamedKind.Pinnacle ? $"燼憶甦醒：{d.NameZh}" : $"試煉：{d.NameZh}", Palette.Gold);
        NamedDriver.Later(1.0, () => Game.I?.Hud?.AreaTitle(Name(L.AreaId), d.Kind == NamedKind.Pinnacle ? d.NameZh : d.Title));
    }

    /// <summary>After a kill in an encounter area: the way out (and, on the ladder, the way up).</summary>
    internal static void AfterKill(NamedEncounter e, Vector2 at)
    {
        var L = e.L;
        if (!IsNamedArea(L.AreaId) || L.Grid == null) return;
        string from = L.AreaId;
        Vector2 spot = L.Grid.RandomWalkNear(at, 3f, 5f, L.Rand);
        if (e.Def.Kind == NamedKind.Pinnacle && e.Def.Rung < 8)
        {
            int next = e.Def.Rung + 1;
            var p = WorldObjects.Portal(spot, $"登上燼階第{"一二三四五六七八"[next - 1]}階", NamedDb.RungOf(next)?.NameZh ?? "", new Color(1f, 0.85f, 0.5f), _ => Game.I.EnterArea(RungArea(next), from));
            L.AddMarker("exit", p.Label, p.Pos2, p);
            spot = L.Grid.RandomWalkNear(at, 5f, 7f, L.Rand);
        }
        string back = e.Def.Kind == NamedKind.World && e.Def.Region != null ? e.Def.Region : ZoneDb.Town;
        var portal = WorldObjects.Portal(spot, $"返回{ZoneDb.AreaName(back)}", "傳送門", Palette.Ember, _ => Game.I.EnterArea(back, from));
        L.AddMarker("exit", portal.Label, portal.Pos2, portal);
    }

    // ── town: the Ember Stair ──

    public static bool LadderOpen(out string why)
    {
        var g = Game.I;
        why = "";
        if (g?.Hero == null) { why = "沒有角色"; return false; }
        if (!Quests.CampaignDone && !(g.Arg("named") || g.Arg("suite"))) { why = "寂主仍在日爐之心沉睡：先結束主線，燼階才會回應。"; return false; }
        if (g.WorldTier < 2 && !g.Arg("suite")) { why = $"燼憶只回應「{ZoneDb.TierNames[2]}」以上的世界階級。"; return false; }
        return true;
    }

    /// <summary>A rung may be climbed when it is the first, or the rung below fell at this world tier or higher.</summary>
    public static bool RungOpen(int rung)
    {
        if (rung <= 1) return true;
        if (Game.I != null && Game.I.Arg("suite")) return true;
        var below = NamedDb.RungOf(rung - 1);
        return below != null && NamedRecords.BestTier(below.Id) >= (Game.I?.WorldTier ?? 0);
    }

    private static void Ladder(Level L)
    {
        var ui = Game.I?.Ui;
        if (ui != null && ui.Find(NamedLadderUi.PanelId) == null) ui.Register(new NamedLadderUi());     // --ui=named_ladder can open it too
        if (!Quests.CampaignDone && !(Game.I?.Arg("named") ?? false)) return;
        var stone = L.Markers.FirstOrDefault(k => k.Kind == "riftstone");
        Vector2 near = stone?.Pos ?? L.Checkpoint;
        Vector2 at = L.Grid.RandomWalkNear(near, 4.5f, 6.5f, L.Rand);
        if (!L.ReachableSpot(at, 3, out at)) at = L.Grid.NearestWalk(near + new Vector2(5f, 0f));
        var it = WorldObjects.Make("named_ladder", at, "燼階", new Color(1f, 0.82f, 0.45f), 3f, 2.6f);
        it.Sub = "八段燼憶，依序挑戰";
        var look = NamedProps.Brazier(true, 1.25f, new Color(1f, 0.75f, 0.4f));
        it.AddChild(look);
        var ring = NamedProps.Ring(2.2f, new Color(1f, 0.7f, 0.35f), 1.6f);
        it.AddChild(ring);
        it.OnUse = _ => NamedLadderUi.Show();
        L.AddMarker("riftstone", "燼階", at, it);
        Log.Info("named", $"town: the Ember Stair altar at ({at.X:0},{at.Y:0})");
    }
}

/// <summary>A node on the game that runs deferred steps of the named layer once play is running (after a load).</summary>
public partial class NamedDriver : Node
{
    private static NamedDriver _i;
    private readonly List<(double at, Action a)> _q = new();
    private double _clock;

    public static void Later(double seconds, Action a)
    {
        if (a == null) return;
        var g = Game.I;
        if (g == null) return;
        if (_i == null || !IsInstanceValid(_i)) { _i = new NamedDriver { Name = "NamedDriver" }; g.AddChild(_i); }
        _i._q.Add((_i._clock + seconds, a));
    }

    public override void _Process(double delta)
    {
        var g = Game.I;
        if (g == null || g.State != Game.GameState.Play) return;
        _clock += delta;
        for (int i = 0; i < _q.Count; i++)
        {
            if (_q[i].at > _clock) continue;
            var a = _q[i].a; _q.RemoveAt(i); i--;
            try { a(); } catch (Exception e) { Log.Error("named", "deferred step failed", e); }
        }
    }
}
