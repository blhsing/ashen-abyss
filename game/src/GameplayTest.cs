using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AshenAbyss;

/// <summary>
/// Test harness of the gameplay engineer (--gptest=&lt;mode&gt;), bot off. Drives the hero through Player's simulated mouse
/// (Player.TestMouse / TestClick: the same UpdateAim → Hover → HandleOrders path a real click takes) and checks the result
/// in the log as "gptest ... PASS/FAIL" lines. Modes:
///   chase   — click an enemy out of reach with the basic skill: the hero runs up and strikes (ranged classes stop at
///             range; also a target out of sight). Wildsoul repeats it in bear and wolf form.
///   cancel  — run-up cancel cases: ground click, evade, target dies, unreachable target, stun; Shift = attack in place;
///             holding the button keeps attacking (also after the cursor slips off).
///   save    — makes progress here (2 totems toppled, monsters killed, a chest opened, an item dropped, a spot), writes
///             the expectations to --gpexpect=&lt;file&gt; and quits (the quit autosave).
///   verify  — after --load: compares the resumed level with --gpexpect.
///   portal  — opens a town portal from here (T), quits in town; verify then takes the portal back.
/// Ends with Game.Quit (saves). Use private slots 90-99.
/// </summary>
public partial class GameplayTest : Node
{
    private const string Cat = "gptest";
    private IEnumerator<float> _run;
    private double _wait = 2.0;
    private string _mode;
    private int _pass, _fail;
    private float _dbgT;
    private static Game G => Game.I;
    private static Player P => G?.Player;

    public override void _Ready() { _mode = (G.ArgValue("gptest") ?? "chase").ToLowerInvariant(); Log.Info(Cat, $"mode={_mode}"); }

    public override void _Process(double delta)
    {
        if (_run == null)
        {
            if (G.State != Game.GameState.Play || P == null || !IsInstanceValid(P) || G.Level == null) return;
            _run = _mode switch { "cancel" => Cancel(), "save" => SaveRun(), "verify" => Verify(), "portal" => PortalRun(), "portalback" => PortalBack(), "idle" => IdleCheck(), "chaseall" => ChaseAll(), "saveall" => SaveAll(), "verifyall" => VerifyAll(), _ => Chase() };
            return;
        }
        if (G.State != Game.GameState.Play) return;          // a loading screen in between: wait
        _wait -= delta;
        if (_wait > 0) return;
        bool more;
        try { more = _run.MoveNext(); }
        catch (Exception e) { Log.Error(Cat, "test step failed", e); more = false; _fail++; }
        if (more) { _wait = _run.Current; return; }
        Log.Info(Cat, $"done mode={_mode} pass={_pass} fail={_fail}");
        _run = Idle();
        Player.TestMouse = null; Player.TestLeft = Player.TestRight = Player.TestShift = false;
        G.Quit();
    }

    private static IEnumerator<float> Idle() { while (true) yield return 1f; }

    /// <summary>Just reports where the hero woke up (old-format saves).</summary>
    private IEnumerator<float> IdleCheck()
    {
        yield return 1.0f;
        var p = P; var lv = G.Level;
        Log.Info(Cat, $"woke in {lv.AreaId} seed={lv.Seed} at ({p.Pos2.X:0.000},{p.Pos2.Y:0.000}) facing={p.Facing:0.000} checkpoint=({lv.Checkpoint.X:0.0},{lv.Checkpoint.Y:0.0}) monsters={World.Monsters.Count}");
        Check(true, $"idle: loaded into {lv.AreaId}");
    }

    private void Check(bool ok, string what)
    {
        if (ok) _pass++; else _fail++;
        Log.Info(Cat, $"{(ok ? "PASS" : "FAIL")} {what}");
    }

    // ───────────────────────── simulated mouse ─────────────────────────

    private static Vector2 Screen(Vector3 world) => G.Cam.Cam.UnprojectPosition(world);
    private static Vector2 ScreenOf(Actor a) => Screen(a.GlobalPosition + Vector3.Up * 1.0f);
    private static void PointAt(Actor a) => Player.TestMouse = ScreenOf(a);
    private static void PointAtGround(Vector2 p) => Player.TestMouse = Screen(World.To3(p));
    private static void Click(int button = 0) => P.TestClick(button);

    private static IEnumerable<Monster> Living => World.Monsters.Where(m => m != null && IsInstanceValid(m) && !m.Dead && !m.Untargetable && !m.Stealthed && m.IsInsideTree());

    /// <summary>A sleeping monster between <paramref name="min"/> and <paramref name="max"/> metres away that the hero can walk to.</summary>
    private static float _pickLen;

    private static Monster PickTarget(float min, float max, Func<Monster, bool> extra = null, bool sightFree = true, bool detour = false)
    {
        var p = P; var grid = World.Grid;
        Monster best = null; float bd = float.MaxValue;
        foreach (var m in Living)
        {
            if (m.IsBoss || m == G.Level?.BossMon) continue;
            float d = m.Pos2.DistanceTo(p.Pos2);
            if (d < min || d > max) continue;
            if (extra != null && !extra(m)) continue;
            var path = grid.LineWalk(p.Pos2, m.Pos2) ? new List<Vector2> { m.Pos2 } : grid.Path(p.Pos2, m.Pos2);
            if (path.Count == 0 || path[^1].DistanceTo(m.Pos2) > 2.5f) continue;
            // a route not much longer than the straight line, and a clear shot along it (no detour round a cliff for this case)
            float len = 0f; Vector2 cur = p.Pos2; foreach (var q in path) { len += cur.DistanceTo(q); cur = q; }
            if (detour) { if (len < d * 1.4f + 3f || len > 120f) continue; }
            else
            {
                if (len > d * 1.3f + 2f) continue;
                if (sightFree && !grid.LineShot(p.Pos2, m.Pos2)) continue;
            }
            _pickLen = len;
            if (d < bd) { bd = d; best = m; }
        }
        return best;
    }

    /// <summary>A target <paramref name="min"/>-<paramref name="max"/> m away with a direct route; tries spots near other packs when there is none here.</summary>
    private IEnumerator<float> FindTarget(float min, float max, Action<Monster> found)
    {
        var t = PickTarget(min, max);
        var anchors = Living.Where(m => !m.IsBoss && m != G.Level?.BossMon).OrderBy(m => m.GetInstanceId() % 89).Take(12).ToList();
        foreach (var a in anchors)
        {
            if (t != null) break;
            var q = World.Grid.RandomWalkNear(a.Pos2, min + 0.5f, Mathf.Min(max - 0.5f, min + 6f));
            if (q == a.Pos2) continue;
            P.Teleport(q); G.Cam?.Snap();
            yield return 0.2f;
            t = PickTarget(min, max);
        }
        found(t);
    }

    /// <summary>Puts the hero on open ground a few metres from the nearest pack (a place to test from).</summary>
    private static bool MoveNear(float dist)
    {
        var p = P; var grid = World.Grid;
        foreach (var m in Living.OrderBy(m => m.Pos2.DistanceTo(p.Pos2)))
        {
            if (m.IsBoss || m == G.Level?.BossMon) continue;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.Tau / 16f;
                var q = m.Pos2 + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dist;
                if (!grid.WalkAt(q) || !grid.LineWalk(q, m.Pos2)) continue;
                if (Living.Any(o => o.Pos2.DistanceTo(q) < dist - 1f)) continue;
                p.Teleport(q);
                G.Cam?.Snap();
                return true;
            }
        }
        return false;
    }

    private static string Id(Actor a) => (a as Monster)?.Def?.Id ?? a?.Name ?? "-";

    // ───────────────────────── chase ─────────────────────────

    private IEnumerator<float> Chase()
    {
        yield return 0.5f;
        var p = P; var h = G.Hero;
        p.Life = p.MaxLife;
        var d = h.Eff(h.D.Bar[0]);
        float reach = Player.ReachOf(d, out bool sight);
        Log.Info(Cat, $"class={Names.ClassKey(h.Class)} basic={d?.Id} kind={d?.Kind} reach={reach:0.0} sight={sight} area={G.Level.AreaId}");
        var forms = h.Class == ClassId.Wildsoul ? new[] { 0, 1, 2 } : new[] { 0 };
        foreach (int form in forms)
        {
            if (form != 0) { p.SetForm(form); yield return 0.4f; }
            var it = RunUp(reach, sight, form == 0 ? "" : form == 1 ? " form=bear" : " form=wolf");
            while (it.MoveNext()) yield return it.Current;
        }
        p.SetForm(0);
        // detours and walls: in the dungeon (a cave: walls everywhere)
        string dz = G.ArgValue("gpdetour") ?? "wastes_d2";
        { var g = Go(dz); while (g.MoveNext()) yield return g.Current; }
        p = P; p.Life = p.MaxLife;
        { var dt = Detour(reach, ""); while (dt.MoveNext()) yield return dt.Current; }
        if (sight)
        {
            // out of sight: in range but behind a wall → walk until it can be seen, then shoot
            var t = PickTarget(4f, reach, m => !World.Grid.LineShot(p.Pos2, m.Pos2), false);
            if (t == null && MoveNear(reach * 0.7f)) { yield return 0.3f; t = PickTarget(4f, reach, m => !World.Grid.LineShot(p.Pos2, m.Pos2), false); }
            if (t == null) Log.Info(Cat, "line-of-sight case: no target behind a wall within range here (not tested)");
            else
            {
                int h0 = p.ChaseHits, c0 = p.ChaseStarts; float d0 = p.Pos2.DistanceTo(t.Pos2); Vector2 s0 = p.Pos2;
                PointAt(t); yield return 0f;
                if (p.Hover is Monster hm2) t = hm2;
                bool blocked = !World.Grid.LineShot(p.Pos2, t.Pos2);
                Click(0);
                bool struck = false, losAtStrike = false; float t0 = 0;
                // the blow starts the moment the target is in range and in sight: sight is checked on that very frame
                while (t0 < 8f) { yield return 0f; t0 += (float)GetProcessDeltaTime(); if (!IsInstanceValid(t)) break; PointAt(t); if (p.ChaseHits > h0) { struck = true; losAtStrike = p.LastStrikeSight; break; } }
                Check(!blocked || (p.ChaseStarts > c0 && struck && losAtStrike), $"los class={Names.ClassKey(h.Class)} target={Id(t)} start={d0:0.0}m blockedAtClick={blocked} ranUp={p.ChaseStarts > c0} walked={p.Pos2.DistanceTo(s0):0.0}m struck={struck} sightAtStrike={losAtStrike}");
            }
        }
        Player.TestMouse = null;
    }


    /// <summary>A target the hero can only reach by a detour (round a cliff, a wall, a pit): he must follow the route and strike.</summary>
    private IEnumerator<float> Detour(float reach, string note)
    {
        var p = P; var h = G.Hero;
        var t = PickTarget(reach + 2f, reach + 22f, null, false, true);
        // none from here: try spots near other packs
        var anchors = Living.Where(m => !m.IsBoss && m != G.Level?.BossMon).OrderBy(m => m.GetInstanceId() % 97).Take(10).ToList();
        foreach (var a in anchors)
        {
            if (t != null) break;
            var q = World.Grid.RandomWalkNear(a.Pos2, 9f, 18f);
            if (q == a.Pos2 || Living.Any(o => o.Pos2.DistanceTo(q) < 6f)) continue;
            if (G.Level.ReachableSpot(q, 0, out _) == false) continue;
            p.Teleport(q); G.Cam?.Snap();
            yield return 0.2f;
            t = PickTarget(reach + 2f, reach + 22f, null, false, true);
        }
        if (t == null) { Log.Info(Cat, $"detour class={Names.ClassKey(h.Class)}{note}: no detour target here (not tested)"); yield break; }
        float len = _pickLen, d0 = p.Pos2.DistanceTo(t.Pos2), life0 = t.Life, limit = len / 4f + 8f, reachT = reach;
        int f0 = p.Fired; Vector2 start = p.Pos2;
        p.Resource = h.MaxResource;
        PointAt(t); yield return 0f;
        if (p.Hover is Monster hm) t = hm;
        Click(0);
        bool fired = false; float el = 0f, dAt = -1f;
        while (el < limit)
        {
            yield return 0f;
            el += (float)GetProcessDeltaTime();
            if (!IsInstanceValid(t) || t.Dead) break;
            PointAt(t);
            if (p.Fired > f0) { fired = true; dAt = p.Pos2.DistanceTo(t.Pos2); break; }
            if ((_dbgT += (float)GetProcessDeltaTime()) > 1f) { _dbgT = 0f; Log.Info(Cat, $"  detour: dist={p.Pos2.DistanceTo(t.Pos2):0.0} sight={World.Grid.LineShot(p.Pos2, t.Pos2)} {p.ChaseDebug}"); }
        }
        yield return 0.7f;
        bool hit = !IsInstanceValid(t) || t.Dead || t.Life < life0;
        // the blow is what is checked (a projectile may still meet another monster of the pack on its way)
        Check(fired && dAt <= reachT + t.Radius + 0.6f, $"detour class={Names.ClassKey(h.Class)}{note} target={Id(t)} straight={d0:0.0}m route={len:0.0}m walked={p.Pos2.DistanceTo(start):0.0}m struckAt={dAt:0.0}m after={el:0.0}s (limit {limit:0}) hit={hit} last='{p.LastChaseEnd}'");
    }

    /// <summary>One run-up: click an enemy out of reach, check the hero gets there, strikes it and (ranged) stops at range.</summary>
    private IEnumerator<float> RunUp(float reach, bool ranged, string note)
    {
        var p = P; var h = G.Hero;
        float min = reach + 2.5f, max = reach + (ranged ? 9f : 12f);
        Monster t = null;
        { var ft = FindTarget(min, max, m => t = m); while (ft.MoveNext()) yield return ft.Current; }
        if (t == null) { Check(false, $"run-up class={Names.ClassKey(h.Class)}{note}: no target {min:0}-{max:0} m away"); yield break; }
        p.Resource = h.MaxResource;
        int f0 = p.Fired, s0 = p.ChaseStarts, hits0 = p.ChaseHits;
        float d0 = p.Pos2.DistanceTo(t.Pos2), life0 = t.Life;
        Vector2 start = p.Pos2;
        PointAt(t);
        yield return 0f;
        var kind = p.HoverKind;
        if (p.Hover != t) Log.Info(Cat, $"note: the cursor names {Id(p.Hover)} rather than {Id(t)} (a pack); the run-up goes for that one");
        if (p.Hover is Monster hm) t = hm;
        Click(0);
        bool fired = false; float dAt = -1, el = 0f;
        while (el < 10f)
        {
            yield return 0f;
            el += (float)GetProcessDeltaTime();
            if (!IsInstanceValid(t) || t.Dead) break;
            PointAt(t);
            if (p.Fired > f0) { fired = true; dAt = p.Pos2.DistanceTo(t.Pos2); break; }
            if ((_dbgT += (float)GetProcessDeltaTime()) > 0.5f) { _dbgT = 0f; Log.Info(Cat, $"  run-up: hero=({p.Pos2.X:0.0},{p.Pos2.Y:0.0}) target=({t.Pos2.X:0.0},{t.Pos2.Y:0.0}) dist={p.Pos2.DistanceTo(t.Pos2):0.0} sight={World.Grid.LineShot(p.Pos2, t.Pos2)} hover={Id(p.Hover)} {p.ChaseDebug}"); }
        }
        for (float w = 0f; w < 1.5f && IsInstanceValid(t) && !t.Dead && !(t.Life < life0); w += 0.1f) yield return 0.1f;      // a bolt is still flying
        bool hit = !IsInstanceValid(t) || t.Dead || t.Life < life0;
        bool started = p.ChaseStarts > s0;
        bool stoppedAtRange = !ranged || dAt > Mathf.Min(reach - 3f, d0 - 3f);
        Check(started && fired && (hit || ranged) && dAt <= reach + t.Radius + 0.6f && stoppedAtRange && kind == Player.HoverAct.Attack,
            $"run-up class={Names.ClassKey(h.Class)}{note} target={Id(t)} hover={kind} start={d0:0.0}m reach={reach:0.0} ran={p.Pos2.DistanceTo(start):0.0}m " +
            $"struckAt={dAt:0.0}m after={el:0.00}s chaseStarted={started} strikeOnArrival={p.ChaseHits > hits0} targetHit={hit} last='{p.LastChaseEnd}'");
    }

    // ───────────────────────── cancel cases ─────────────────────────

    private IEnumerator<float> Cancel()
    {
        yield return 0.5f;
        var p = P; var h = G.Hero; var lv = G.Level;
        var d = h.Eff(h.D.Bar[0]);
        float reach = Player.ReachOf(d, out _);
        float min = reach + 6f, max = reach + 16f;

        // hover kinds
        var anyT = PickTarget(2f, 30f);
        if (anyT != null) { PointAt(anyT); yield return 0.05f; var k1 = p.HoverKind; PointAtGround(p.Pos2 + new Vector2(0.5f, 2.5f)); yield return 0.05f; Check(k1 == Player.HoverAct.Attack && p.HoverKind is Player.HoverAct.Move or Player.HoverAct.Interact, $"hover kinds enemy={k1} ground={p.HoverKind}"); }

        foreach (var c in new[] { "ground", "evade", "died", "stun", "wasd" })
        {
            if (c == "wasd") continue;        // keyboard state cannot be simulated through Input here; covered by code review only
            Monster t = null;
            { var ft = FindTarget(min, max, m => t = m); while (ft.MoveNext()) yield return ft.Current; }
            if (t == null) { Check(false, $"cancel {c}: no target"); continue; }
            p.Life = p.MaxLife; p.Resource = h.MaxResource;
            int f0 = p.Fired;
            PointAt(t); Click(0);
            yield return 0.15f;
            bool chasing = p.AttackTarget == t;
            switch (c)
            {
                case "ground":
                {
                    // open ground with nothing clickable on it (a barrel there would be "use", not a ground click)
                    Vector2 back = (p.Pos2 - t.Pos2).Normalized(), q = World.Grid.NearestWalk(p.Pos2 + back * 3f);
                    for (int k = 1; k < 12 && Interactable.Pick(q, 2.2f) != null; k++) q = World.Grid.NearestWalk(p.Pos2 + back.Rotated(k * 0.5f) * 3f);
                    PointAtGround(q); Click(0); break;
                }
                case "evade": p.QueueEvade(); break;
                case "died": t.TakeHit(new Hit { Source = p, Amount = t.Life + t.MaxLife * 10f, El = Element.Physical, NoReact = true }); break;
                case "stun": p.ApplyStatus(St.Stun(1.2f), t); break;
            }
            Player.TestMouse = null;
            yield return 0.4f;
            string want = c switch { "ground" => "ground click", "evade" => "evade", "died" => "target died", _ => "interrupted" };
            Check(chasing && p.LastChaseEnd == want && p.AttackTarget == null && p.Fired == f0, $"cancel {c}: wasChasing={chasing} ended='{p.LastChaseEnd}' fired={p.Fired - f0}");
            yield return 1.3f;
        }

        // unreachable: the boss behind its sealed door (or anything without a path)
        Monster far = lv.DoorSealed && lv.BossMon != null && IsInstanceValid(lv.BossMon) ? lv.BossMon : null;
        if (far == null) Log.Info(Cat, "cancel unreachable: no sealed boss here (not tested)");
        else
        {
            int s0 = p.ChaseStarts;
            PointAt(far); Click(0);
            yield return 0f;
            float el = 0f;
            while (el < 4f && p.AttackTarget == far) { yield return 0.1f; el += 0.1f; PointAt(far); }
            yield return 0.05f;
            var kind = p.HoverKind;
            Check(p.ChaseStarts > s0 && p.AttackTarget == null && (p.LastChaseEnd == "no path" || p.LastChaseEnd == "stuck"),
                $"cancel unreachable: boss behind the sealed door ended='{p.LastChaseEnd}' after={el:0.0}s hoverNow={kind}");
            Player.TestMouse = null;
        }

        // shift = attack in place
        {
            var t = PickTarget(min, max);
            if (t == null && MoveNear(min + 2f)) { yield return 0.3f; t = PickTarget(min, max); }
            if (t != null)
            {
                p.Resource = h.MaxResource;
                int f0 = p.Fired, s0 = p.ChaseStarts; Vector2 at = p.Pos2;
                Player.TestShift = true; PointAt(t); Click(0);
                yield return 0.6f;
                Player.TestShift = false;
                Check(p.Fired > f0 && p.ChaseStarts == s0 && p.Pos2.DistanceTo(at) < 0.3f, $"shift-click: fired={p.Fired - f0} chaseStarts={p.ChaseStarts - s0} moved={p.Pos2.DistanceTo(at):0.00}m");
                Player.TestMouse = null;
                yield return 0.8f;
            }
        }

        // hold: keeps attacking, also once the cursor slips off the enemy
        {
            var t = PickTarget(min, max, m => m.MaxLife > 0);
            if (t == null && MoveNear(min + 2f)) { yield return 0.3f; t = PickTarget(min, max); }
            if (t != null)
            {
                t.MaxLife *= 50f; t.Life = t.MaxLife;          // lives through the test
                p.Resource = h.MaxResource;
                int f0 = p.Fired;
                PointAt(t); Player.TestLeft = true; Click(0);
                float el = 0f;
                while (el < 4f) { yield return 0.1f; el += 0.1f; if (IsInstanceValid(t)) PointAt(t); p.Resource = h.MaxResource; if (Mathf.PosMod(el, 0.5f) < 0.11f) Log.Info(Cat, $"  hold: dist={p.Pos2.DistanceTo(t.Pos2):0.0} fired={p.Fired - f0} hover={Id(p.Hover)} held={Player.TestLeft} {p.ChaseDebug} last={p.LastChaseEnd}"); }
                int onTarget = p.Fired - f0;
                // slip off: the cursor on empty ground beside the hero, button still down
                // beside the hero, square to the target (a blow taken from it pushes the hero along that line, not toward here)
                Vector2 dirT = (t.Pos2 - p.Pos2).Normalized();
                Vector2 off = World.Grid.NearestWalk(p.Pos2 + new Vector2(-dirT.Y, dirT.X) * 3.5f);
                float offD0 = p.Pos2.DistanceTo(off);
                PointAtGround(off);
                int f1 = p.Fired; Vector2 at = p.Pos2;
                el = 0f;
                while (el < 2f) { yield return 0.1f; el += 0.1f; p.Resource = h.MaxResource; }
                int slipped = p.Fired - f1;
                Player.TestLeft = false;
                Check(onTarget >= 3 && slipped >= 1 && IsInstanceValid(t) && p.Pos2.DistanceTo(off) > offD0 - 1.0f,
                    $"hold: blows on target in 4 s={onTarget}, after the cursor slipped off in 2 s={slipped}, moved toward cursor={offD0 - p.Pos2.DistanceTo(off):0.0}m");
                Player.TestMouse = null;
            }
        }
    }

    // ───────────────────────── save / verify ─────────────────────────

    private sealed class Expect
    {
        public string Area; public int Seed; public float X, Z, Facing; public int Dead, Spent, Toppled, Fog, Loot, Alive;
        public List<float[]> DeadPos = new(), ToppledPos = new(), ChestPos = new(), PropPos = new();
        public string LootName, Mode;
        public float TownX, TownZ, TownFacing;
    }

    private string _expect;
    private string ExpectPath => _expect ?? G.ArgValue("gpexpect") ?? "Y:/tools/gameplay/test_out/expect.json";
    private static string OutDir => G.ArgValue("gpout") ?? "Y:/tools/gameplay/test_out";

    private static int FogCount(Level lv) => lv.Seen?.Count(s => s) ?? 0;

    private IEnumerator<float> SaveRun()
    {
        yield return 0.8f;
        var p = P; var h = G.Hero; var lv = G.Level;
        var ex = new Expect { Area = lv.AreaId, Seed = lv.Seed };
        // 1. two totems / prisoners down
        var objectives = Interactable.All.Where(i => IsInstanceValid(i) && i.CanUse && (i.Kind == "idol" || i.Kind == "prisoner")).OrderBy(i => i.Pos2.DistanceTo(p.Pos2)).Take(2).ToList();
        foreach (var o in objectives)
        {
            Vector2 at = lv.StandNear(o.Pos2, p.Pos2, 1.8f);
            p.Teleport(at); G.Cam?.Snap();
            yield return 0.2f;
            o.Use(p);
            yield return 2.0f;
            bool down = !IsInstanceValid(o) || !o.CanUse;
            Check(down, $"save: {o.Kind} at ({o.Pos2.X:0.0},{o.Pos2.Y:0.0}) used");
            if (down) { ex.Toppled++; ex.ToppledPos.Add(new[] { o.Pos2.X, o.Pos2.Y }); }
        }
        // 2. kill some monsters (the nearest six, not the boss)
        var victims = Living.Where(m => !m.IsBoss && m != lv.BossMon && !m.Objective).OrderBy(m => m.Pos2.DistanceTo(p.Pos2)).Take(6).ToList();
        foreach (var m in victims)
        {
            ex.DeadPos.Add(new[] { m.Pos2.X, m.Pos2.Y });
            m.TakeHit(new Hit { Source = p, Amount = m.Life + m.MaxLife * 10f, El = Element.Physical, NoReact = true });
        }
        yield return 1.0f;
        ex.Dead = victims.Count(m => !IsInstanceValid(m) || m.Dead);
        // 3. open a chest
        var chest = Interactable.All.Where(i => IsInstanceValid(i) && i.CanUse && i.Kind == "chest").OrderBy(i => i.Pos2.DistanceTo(p.Pos2)).FirstOrDefault();
        if (chest != null)
        {
            p.Teleport(lv.StandNear(chest.Pos2, p.Pos2, 1.6f)); G.Cam?.Snap();
            yield return 0.2f;
            chest.Use(p);
            yield return 1.2f;
            Check(!chest.CanUse, $"save: chest at ({chest.Pos2.X:0.0},{chest.Pos2.Y:0.0}) opened");
            ex.ChestPos.Add(new[] { chest.Pos2.X, chest.Pos2.Y });
        }
        // 4. drop an item from the bag (what Ctrl+click in the bag does)
        var item = h.D.Bag.FirstOrDefault();
        if (item == null) { item = Loot.Roll(h.D.Level, 1, h.Class); }
        else h.D.Bag.Remove(item);
        if (item != null) { Loot.DropItem(item, p.Pos2 + new Vector2(1.2f, 0.4f)); ex.LootName = item.Name; }
        yield return 1.0f;
        // 5. walk somewhere and face a direction
        Vector2 spot = World.Grid.RandomWalkNear(p.Pos2, 3f, 6f);
        p.SetMove(spot, false);
        yield return 1.5f;
        p.Stop();
        p.FaceDir(new Vector2(0.6f, -0.8f));
        yield return 0.3f;
        ex.X = p.Pos2.X; ex.Z = p.Pos2.Y; ex.Facing = p.Facing;
        ex.Fog = FogCount(lv);
        ex.Alive = Living.Count();
        ex.Loot = Interactable.All.Count(i => IsInstanceValid(i) && i.Kind == "loot" && !i.IsQueuedForDeletion());
        foreach (var ps in (lv.Data?.Props ?? new List<PropSlot>()).Take(5)) ex.PropPos.Add(new[] { ps.Pos.X, ps.Pos.Y });
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ExpectPath));
        File.WriteAllText(ExpectPath, JsonSerializer.Serialize(ex, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
        Log.Info(Cat, $"save state: area={ex.Area} seed={ex.Seed} pos=({ex.X:0.000},{ex.Z:0.000}) facing={ex.Facing:0.000} dead={ex.Dead} toppled={ex.Toppled} chest={ex.ChestPos.Count} " +
            $"objective={lv.ObjCount}/{lv.ObjTotal} fog={ex.Fog} groundLoot={ex.Loot} dropped='{ex.LootName}' → {ExpectPath}");
    }

    private IEnumerator<float> Verify()
    {
        yield return 0.8f;
        var p = P; var lv = G.Level;
        Expect ex = null;
        try { ex = JsonSerializer.Deserialize<Expect>(File.ReadAllText(ExpectPath), new JsonSerializerOptions { IncludeFields = true }); }
        catch (Exception e) { Log.Error(Cat, $"cannot read {ExpectPath}", e); }
        if (ex == null) { Check(false, "verify: no expectations"); yield break; }
        Check(lv.AreaId == ex.Area && lv.Seed == ex.Seed, $"verify: area {lv.AreaId} (saved {ex.Area}), seed {lv.Seed} (saved {ex.Seed})");
        var props = (lv.Data?.Props ?? new List<PropSlot>()).Take(5).Select(ps => new[] { ps.Pos.X, ps.Pos.Y }).ToList();
        bool sameProps = props.Count == ex.PropPos.Count && props.Zip(ex.PropPos).All(z => Mathf.Abs(z.First[0] - z.Second[0]) < 0.01f && Mathf.Abs(z.First[1] - z.Second[1]) < 0.01f);
        Check(sameProps, $"verify: first {props.Count} prop positions identical: {string.Join(" ", props.Select(q => $"({q[0]:0.00},{q[1]:0.00})"))}");
        float dp = p.Pos2.DistanceTo(new Vector2(ex.X, ex.Z));
        Check(dp <= 0.1f && Mathf.Abs(Mathf.AngleDifference(p.Facing, ex.Facing)) < 0.05f, $"verify: hero at ({p.Pos2.X:0.000},{p.Pos2.Y:0.000}) off by {dp:0.000} m, facing {p.Facing:0.000} (saved {ex.Facing:0.000})");
        int stillDown = 0;
        foreach (var q in ex.ToppledPos)
        {
            var at = new Vector2(q[0], q[1]);
            var o = Interactable.All.FirstOrDefault(i => IsInstanceValid(i) && (i.Kind == "idol" || i.Kind == "prisoner") && i.Pos2.DistanceTo(at) < 0.5f);
            if (o == null || !o.CanUse) stillDown++;
        }
        if (ex.ToppledPos.Count > 0) Check(stillDown == ex.ToppledPos.Count && lv.ObjCount >= ex.Toppled, $"verify: {stillDown}/{ex.ToppledPos.Count} objectives still done, objective {lv.ObjCount}/{lv.ObjTotal}");
        int back = 0;
        foreach (var q in ex.DeadPos)
        {
            var at = new Vector2(q[0], q[1]);
            if (Living.Any(m => m.Pos2.DistanceTo(at) < 0.3f)) back++;
        }
        Check(back == 0, $"verify: {ex.DeadPos.Count - back}/{ex.DeadPos.Count} slain monsters stay dead (none standing at their spots)");
        int alive = Living.Count();
        Check(alive <= ex.Alive && alive >= ex.Alive - 4, $"verify: living monsters {alive} (at saving {ex.Alive}; monsters summoned during play are not kept)");
        foreach (var q in ex.ChestPos)
        {
            var at = new Vector2(q[0], q[1]);
            var c = Interactable.All.FirstOrDefault(i => IsInstanceValid(i) && i.Kind == "chest" && i.Pos2.DistanceTo(at) < 0.5f);
            Check(c != null && !c.CanUse, $"verify: chest at ({at.X:0.0},{at.Y:0.0}) {(c == null ? "missing" : c.CanUse ? "closed again" : "still open")}");
        }
        int fog = FogCount(lv);
        Check(fog >= ex.Fog, $"verify: explored cells {fog} (saved {ex.Fog})");
        int loot = Interactable.All.Count(i => IsInstanceValid(i) && i.Kind == "loot" && !i.IsQueuedForDeletion());
        bool hasDrop = ex.LootName == null || Interactable.All.Any(i => IsInstanceValid(i) && i.Kind == "loot" && i.Item?.Name == ex.LootName);
        Check(hasDrop && loot >= ex.Loot, $"verify: ground loot {loot} items (saved {ex.Loot}), dropped item '{ex.LootName}' {(hasDrop ? "lies there" : "MISSING")}");
        Log.Info(Cat, $"verify state: door={(lv.DoorSealed ? "sealed" : "open")} boss={(lv.BossMon != null ? "alive" : lv.BossDead ? "dead" : "-")} monsters={World.Monsters.Count}");
        if ((G.ArgValue("gpthen") ?? "") == "portal")
        {
            var it = PortalBack();
            while (it.MoveNext()) yield return it.Current;
        }
    }


    // ───────────────────────── batches (one process, fewer turns on the shared run lock) ─────────────────────────

    private static IEnumerator<float> WaitPlay()
    {
        yield return 0.5f;
        while (G.State != Game.GameState.Play || P == null || !IsInstanceValid(P)) yield return 0.2f;
        yield return 1.0f;
    }

    private static IEnumerator<float> Go(string area)
    {
        if (G.Level != null && G.Level.AreaId == area) yield break;
        G.EnterArea(area);
        var w = WaitPlay(); while (w.MoveNext()) yield return w.Current;
    }

    private static IEnumerator<float> NewHero(int slot, string key, string area)
    {
        int idx = Math.Max(0, Array.IndexOf(Names.ClassKeys, key));
        G.NewGame(slot, "試煉者", (ClassId)idx, false);
        var w = WaitPlay(); while (w.MoveNext()) yield return w.Current;
        var g = Go(area); while (g.MoveNext()) yield return g.Current;
        Log.Info(Cat, $"hero {key} in slot {slot}, area {G.Level.AreaId}");
    }

    /// <summary>--gptest=chaseall: the run-up for every class (--gpclasses=a,b,…), then the cancel cases in a dungeon.</summary>
    private IEnumerator<float> ChaseAll()
    {
        var keys = (G.ArgValue("gpclasses") ?? string.Join(",", Names.ClassKeys)).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var key in keys)
        {
            if (G.Hero == null || Names.ClassKey(G.Hero.Class) != key) { var n = NewHero(92, key, "wastes"); while (n.MoveNext()) yield return n.Current; }
            else { var g = Go("wastes"); while (g.MoveNext()) yield return g.Current; }
            var c = Chase(); while (c.MoveNext()) yield return c.Current;
        }
        foreach (var spec in (G.ArgValue("gpcancel") ?? "emberblade:wastes_d2,hunter:wastes").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var sp = spec.Split(':'); string key = sp[0], where = sp.Length > 1 ? sp[1] : "wastes_d2";
            var n = NewHero(92, key, where); while (n.MoveNext()) yield return n.Current;
            Log.Info(Cat, $"cancel cases class={key} area={G.Level.AreaId}");
            var c = Cancel(); while (c.MoveNext()) yield return c.Current;
        }
    }

    /// <summary>--gptest=saveall: progress in a dungeon (slot 93), the overworld (94), town (96), and a town portal opened from a dungeon (97); each saved.</summary>
    private IEnumerator<float> SaveAll()
    {
        // --gpcases=slot:class:area[:portal],… (gameplay slots are 92 and 93)
        var cases = (G.ArgValue("gpcases") ?? "92:emberblade:wastes_d2,93:hunter:wastes").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Split(':')).Select(a => (slot: int.Parse(a[0]), cls: a[1], area: a[2], portal: a.Length > 3 && a[3] == "portal")).ToList();
        foreach (var (slot, cls, area, portal) in cases)
        {
            if (!(G.Slot == slot && G.Level?.AreaId == area && Names.ClassKey(G.Hero.Class) == cls)) { var n = NewHero(slot, cls, area); while (n.MoveNext()) yield return n.Current; }
            _expect = $"{OutDir}/exp_{slot}.json";
            var s = portal ? PortalRun() : SaveRun(); while (s.MoveNext()) yield return s.Current;
            G.SaveNow("test");
            Log.Info(Cat, $"slot {slot} saved in {G.Level.AreaId}");
        }
    }

    /// <summary>--gptest=verifyall (a new process): loads slots 93, 94, 96 and checks them, 97 takes its portal back, 98 is an old-format save.</summary>
    private IEnumerator<float> VerifyAll()
    {
        var slots = (G.ArgValue("gpslots") ?? "92,93").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        int old = int.TryParse(G.ArgValue("gpold"), out int o) ? o : -1;
        foreach (int slot in slots)
        {
            if (!File.Exists(ProjectSettings.GlobalizePath($"user://saves/slot{slot}.json"))) { Check(false, $"verifyall: slot {slot} missing"); continue; }
            if (G.Slot != slot) { G.LoadGame(slot); var w = WaitPlay(); while (w.MoveNext()) yield return w.Current; }
            Log.Info(Cat, $"loaded slot {slot}: {G.Level.AreaId}");
            _expect = $"{OutDir}/exp_{slot}.json";
            string mode = "";
            try { mode = JsonSerializer.Deserialize<Expect>(File.ReadAllText(ExpectPath), new JsonSerializerOptions { IncludeFields = true })?.Mode ?? ""; } catch { }
            var v = slot == old ? IdleCheck() : mode == "portal" ? PortalBack() : Verify();
            while (v.MoveNext()) yield return v.Current;
        }
    }

    // ───────────────────────── town portal ─────────────────────────

    private IEnumerator<float> PortalRun()
    {
        // progress here, then T to town (the portal stays open), and quit in town
        var s = SaveRun();
        while (s.MoveNext()) yield return s.Current;
        G.EnterArea(ZoneDb.Town);
        yield return 1.0f;
        while (G.State != Game.GameState.Play) yield return 0.2f;
        yield return 1.0f;
        Check(G.Level.IsTown && G.ReturnArea != null, $"portal: in town with a portal back to {G.ReturnArea ?? "-"}");
        // the town spot is part of the save as well
        var p = P;
        p.Teleport(World.Grid.RandomWalkNear(p.Pos2, 2f, 4f)); p.FaceDir(new Vector2(-1f, 0.2f));
        yield return 0.3f;
        Log.Info(Cat, $"town spot before quitting: ({p.Pos2.X:0.000},{p.Pos2.Y:0.000}) facing={p.Facing:0.000}");
        var ex = JsonSerializer.Deserialize<Expect>(File.ReadAllText(ExpectPath), new JsonSerializerOptions { IncludeFields = true });
        ex.Mode = "portal"; ex.TownX = p.Pos2.X; ex.TownZ = p.Pos2.Y; ex.TownFacing = p.Facing;
        File.WriteAllText(ExpectPath, JsonSerializer.Serialize(ex, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
    }

    /// <summary>After loading in town: the portal is there; through it, the dungeon is as it was left.</summary>
    private IEnumerator<float> PortalBack()
    {
        var portal = Interactable.All.FirstOrDefault(i => IsInstanceValid(i) && i.Kind == "portal" && G.Level.IsAncestorOf(i));
        Check(G.Level.IsTown && portal != null && G.ReturnArea != null, $"portal after load: in town={G.Level.IsTown} portal={(portal != null ? portal.Label : "missing")} back to {G.ReturnArea ?? "-"}");
        try
        {
            var et = JsonSerializer.Deserialize<Expect>(File.ReadAllText(ExpectPath), new JsonSerializerOptions { IncludeFields = true });
            float dt = P.Pos2.DistanceTo(new Vector2(et.TownX, et.TownZ));
            Check(dt <= 0.1f && Mathf.Abs(Mathf.AngleDifference(P.Facing, et.TownFacing)) < 0.05f, $"town: hero at ({P.Pos2.X:0.000},{P.Pos2.Y:0.000}) off by {dt:0.000} m, facing {P.Facing:0.000} (saved {et.TownFacing:0.000})");
        }
        catch (Exception e) { Log.Error(Cat, "town expectations unreadable", e); }
        if (portal == null) yield break;
        string area = G.ReturnArea;
        portal.Use(P);
        yield return 1.0f;
        while (G.State != Game.GameState.Play) yield return 0.2f;
        yield return 1.0f;
        Check(G.Level.AreaId == area, $"portal: back in {G.Level.AreaId}");
        // compare with the expectations written before the portal was taken (dead monsters, totems, chest)
        var lv = G.Level;
        Expect ex = JsonSerializer.Deserialize<Expect>(File.ReadAllText(ExpectPath), new JsonSerializerOptions { IncludeFields = true });
        int back = ex.DeadPos.Count(q => Living.Any(m => m.Pos2.DistanceTo(new Vector2(q[0], q[1])) < 0.3f));
        int down = ex.ToppledPos.Count(q => Interactable.All.FirstOrDefault(i => IsInstanceValid(i) && (i.Kind == "idol" || i.Kind == "prisoner") && i.Pos2.DistanceTo(new Vector2(q[0], q[1])) < 0.5f) is not { CanUse: true });
        Check(lv.Seed == ex.Seed && back == 0 && down == ex.ToppledPos.Count, $"portal: dungeon as left: seed {lv.Seed}/{ex.Seed}, slain back on their feet={back}, objectives still done={down}/{ex.ToppledPos.Count}, objective {lv.ObjCount}/{lv.ObjTotal}");
    }
}
