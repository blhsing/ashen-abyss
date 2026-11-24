using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace AshenAbyss;

/// <summary>
/// [MOBILE 0.4.1] --suite=mobile — moving while attacking (施法時可移動, Player.Mobile.cs):
/// - data: which skills are Mobile (every ranged kind; no melee, dash, leap or channel);
/// - the hero walks during a Mobile cast at ~65 % speed, and not during a non-Mobile one;
/// - the shot goes to the aim point (behind the hero) while walking away;
/// - Shift still roots; the setting off restores stop-and-shoot;
/// - holding the attack on an enemy while steering with W/A/S/D is a kite (the real input path);
/// - a click on an enemy out of reach still runs up, then shoots;
/// - each ranged class (and the ranged skills of the melee classes) can back away from an enemy while shooting it.
/// --suite=mobile:dps[:classes[:levels[:secs]]] — damage per second standing, and against a chaser while kiting with the
/// setting off (stop and shoot) and on (shoot on the move), fixed gear (the fair harness's seeds).
/// --suite=mobile:shots — pictures in Y:\shots\mobile\ (windowed runs).
/// </summary>
public partial class TestSuite
{
    private IEnumerator MobileSuite()
    {
        var parts = _arg.Split(':', StringSplitOptions.TrimEntries);
        string mode = parts.Length > 0 ? parts[0] : "";
        try
        {
            if (mode == "dps") { yield return MobileDps(parts); yield break; }
            if (mode == "shots") { yield return MobileShots(); yield break; }
            MobileData();
            yield return MobileHunterChecks();
            yield return MobileClasses();
            if (mode != "noshots" && DisplayServer.GetName() != "headless") yield return MobileShots();
        }
        finally
        {
            Player.TestMove = Vector2.Zero; Player.MobileForce = null;
            Player.TestMouse = null; Player.TestLeft = false; Player.TestShift = false;
        }
    }

    // ───────────────────────── data ─────────────────────────

    private void MobileData()
    {
        _section = "mobile: data";
        var ranged = new HashSet<SkillKind> { SkillKind.Proj, SkillKind.Chain, SkillKind.Ground, SkillKind.Zone, SkillKind.Trap, SkillKind.Command };
        var rooted = new HashSet<SkillKind> { SkillKind.Melee, SkillKind.Dash, SkillKind.Leap, SkillKind.Blink, SkillKind.Channel, SkillKind.Buff, SkillKind.Nova };
        var missing = SkillDb.All.Where(s => ranged.Contains(s.Kind) && !s.Mobile && !SkillDb.NotMobileIds.Contains(s.Id)).Select(s => s.Id).ToList();
        Check(missing.Count == 0, "mobile: every ranged-kind skill is Mobile or listed in NotMobileIds", missing.Count == 0 ? $"{SkillDb.All.Count(s => s.Mobile)} Mobile" : string.Join(",", missing));
        var wrong = SkillDb.All.Where(s => s.Mobile && rooted.Contains(s.Kind)).Select(s => s.Id).ToList();
        Check(wrong.Count == 0, "mobile: no melee strike, dash, leap, blink, channel, buff or nova is Mobile", string.Join(",", wrong));
        var unknown = SkillDb.MobileIds.Where(id => SkillDb.Get(id) == null).ToList();
        Check(unknown.Count == 0, "mobile: every id in MobileIds exists", string.Join(",", unknown));
        foreach (var c in new[] { ClassId.Hunter, ClassId.Astromancer, ClassId.Soulbinder, ClassId.Pactbinder })
        {
            var b = SkillDb.Get(SkillDb.DefaultBasic(c));
            Check(b != null && b.Mobile, $"mobile: the basic attack of {Names.ClassKey(c)} ({b?.Id}) is Mobile");
        }
        Log.Info("suite", "mobile: Mobile skills = " + string.Join(" ", SkillDb.All.Where(s => s.Mobile).Select(s => $"{s.Id}({s.Kind})")));

        Check(SkillDb.Get("hu_shot").Clone().Mobile, "mobile: the flag survives a clone (Hero.Eff, every-Nth variants)");
    }

    // ───────────────────────── helpers ─────────────────────────

    /// <summary>The most open straight line from the arena centre: the direction (of 16) with the longest clear walk, up to 24 m.</summary>
    private static Vector2 OpenDir(Vector2 c, out float len)
    {
        var g = World.Grid;
        Vector2 best = Vector2.Right; len = 0f;
        for (int k = 0; k < 16; k++)
        {
            var d = Vector2.Right.Rotated(k * Mathf.Tau / 16f);
            var end = g != null ? g.ClampLine(c, c + d * 24f, false) : c + d * 24f;
            float l = end.DistanceTo(c);
            if (g != null && !g.LineWalkWide(c, end, 0.5f)) l *= 0.5f;
            if (l > len) { len = l; best = d; }
        }
        return best;
    }

    private void Prime(Player p, Hero h)
    {
        p.CancelAction(); p.SetForm(0); p.Cds.Clear(); p.Stop();
        p.Resource = h.MaxResource; p.Pips = h.MaxPips;
    }

    /// <summary>Walks with <paramref name="dir"/> held for <paramref name="secs"/>, casting the bar slot whenever free. Measures what moved during casts.</summary>
    private sealed class WalkCast { public float CastMetres, CastTime, FreeMetres, FreeTime; public int Fired, Casts; public Vector2 Start, End; }

    private IEnumerator WalkAndCast(Player p, Hero h, int slot, Vector2 dir, float secs, Func<Vector2> point, Func<Actor> target, WalkCast r, Action perFrame = null)
    {
        Player.TestMove = dir;
        r.Start = p.Pos2;
        int fired0 = p.Fired;
        double t0 = G.Time, last = G.Time;
        Vector2 prev = p.Pos2;
        bool wasBusy = p.Busy;
        while (G.Time - t0 < secs)
        {
            perFrame?.Invoke();
            if (!p.Busy) { p.Resource = h.MaxResource; p.Cds.Clear(); if (p.TryCast(slot, point(), target(), true)) r.Casts++; }
            yield return null;
            float dt = (float)(G.Time - last); last = G.Time;
            float step = p.Pos2.DistanceTo(prev); prev = p.Pos2;
            // a frame counts for the cast when the hero was casting at both ends of it
            bool busy = p.Busy;
            if (busy && wasBusy) { r.CastMetres += step; r.CastTime += dt; } else if (!busy && !wasBusy) { r.FreeMetres += step; r.FreeTime += dt; }
            wasBusy = busy;
        }
        Player.TestMove = Vector2.Zero;
        r.Fired = p.Fired - fired0;
        r.End = p.Pos2;
        yield return 0.3f;
    }

    private static float Speed(float m, float t) => t > 0.05f ? m / t : 0f;

    // ───────────────────────── hunter: the rules ─────────────────────────

    private IEnumerator MobileHunterChecks()
    {
        _section = "mobile: hunter";
        yield return StartHero(ClassId.Hunter, 20, "town");
        var h = G.Hero; var p = G.Player;
        if (h == null || h.Class != ClassId.Hunter) { Fail("mobile: hunter not created"); yield break; }
        ClearField();
        Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 8);
        Vector2 dir = OpenDir(arena, out float room);
        Vector2 start = arena - dir * Mathf.Min(6f, room * 0.25f);
        Log.Info("suite", $"mobile: open line {dir.X:0.00},{dir.Y:0.00} {room:0.0} m");
        Configure(h, SkillDb.Get("hu_flurry"), null, "base", 1);           // bar: 0 hu_shot (Mobile), 2 hu_flurry (melee)
        Check(h.Eff("hu_shot")?.Mobile == true && h.Eff("hu_flurry")?.Mobile == false, "mobile: Hero.Eff keeps the flag (hu_shot yes, hu_flurry no)");
        float full = Player.BaseSpeed * h.MoveMul * p.SlowMul;

        // 1. walking through a Mobile cast, ~65 % of the speed
        Player.MobileForce = true;
        Prime(p, h); p.Teleport(start); yield return 0.3f;
        var r1 = new WalkCast();
        Vector2 behind1 = start - dir * 10f;
        yield return WalkAndCast(p, h, 0, dir, 2.0f, () => behind1, () => null, r1);
        float v1 = Speed(r1.CastMetres, r1.CastTime);
        Check(r1.Fired >= 3 && r1.CastTime > 0.5f && v1 > full * 0.45f, "mobile: the hero walks during a Mobile cast (hu_shot)", $"fired {r1.Fired}, {r1.CastMetres:0.0} m in {r1.CastTime:0.00} s casting = {v1:0.0} m/s; free {Speed(r1.FreeMetres, r1.FreeTime):0.0} m/s; full speed {full:0.0}");
        Check(Mathf.Abs(v1 / full - Player.MobileSpeedMul) < 0.15f, $"mobile: the walking speed in the cast is ~{Player.MobileSpeedMul:P0}", $"{v1 / full:P0}");
        Check(p.MobileMetres > 0.5f && p.MobileCastsMoving > 0, "mobile: the player counts the walked casts", $"{p.MobileMetres:0.0} m, {p.MobileCastsMoving}/{p.MobileCasts} casts moving");
        Check(p.LastFireMoving, $"mobile: a shot fired on the move takes the moving-shot damage (x{Player.MobileDmgMul:0.00})");
        Prime(p, h); p.Teleport(start); Player.TestMove = Vector2.Zero; yield return 0.3f;
        { int fs = p.Fired; p.TryCast(0, behind1, null, true); for (int i = 0; i < 60 && p.Fired == fs; i++) yield return null; }
        Check(!p.LastFireMoving, "mobile: a shot fired standing deals full damage");
        yield return 0.3f;

        // 2. a non-Mobile cast (a melee flurry, in the air) still roots
        Prime(p, h); p.Teleport(start); yield return 0.3f;
        var r2 = new WalkCast();
        yield return WalkAndCast(p, h, 2, dir, 1.6f, () => start + dir * 2f, () => null, r2);
        Check(r2.Casts >= 1 && r2.CastTime > 0.3f && r2.CastMetres < 0.15f, "mobile: no walking during a non-Mobile cast (hu_flurry)", $"casts {r2.Casts}, {r2.CastMetres:0.00} m in {r2.CastTime:0.00} s casting");

        // 3. the shot leaves toward the aim point behind the hero while walking away from it
        Prime(p, h); p.Teleport(start); yield return 0.3f;
        Vector2 aimPt = start - dir * 9f;
        var known = new HashSet<Projectile>(Projectile.Live);
        Player.TestMove = dir;
        yield return 0.25f;            // already walking when the shot begins
        int f0 = p.Fired;
        p.TryCast(0, aimPt, null, true);
        Projectile shot = null;
        for (int i = 0; i < 60 && shot == null; i++)
        {
            yield return null;
            shot = Projectile.Live.FirstOrDefault(q => GodotObject.IsInstanceValid(q) && !known.Contains(q) && !q.HostileShot);
        }
        Vector2 walked = p.Pos2 - start;
        Player.TestMove = Vector2.Zero;
        if (shot != null)
        {
            Vector2 want = (aimPt - p.LastFireFrom).Normalized();
            Check(shot.Heading.Dot(want) > 0.97f && shot.Heading.Dot(dir) < -0.9f && p.LastFireAim.DistanceTo(aimPt) < 0.05f,
                "mobile: walking away, the arrow flies to the aim point behind the hero", $"heading·aim {shot.Heading.Dot(want):0.000}, heading·walk {shot.Heading.Dot(dir):0.00}, walked {walked.Dot(dir):0.0} m");
        }
        else Fail($"mobile: no arrow seen (fired {p.Fired - f0})");
        yield return 0.4f;

        // 4. Shift still roots a Mobile cast
        Prime(p, h); p.Teleport(start); yield return 0.3f;
        Player.TestMouse = G.GetViewport().GetVisibleRect().Size * 0.5f; Player.TestShift = true;
        var r4 = new WalkCast();
        yield return WalkAndCast(p, h, 0, dir, 1.6f, () => behind1, () => null, r4);
        Player.TestShift = false; Player.TestMouse = null;
        Check(r4.Fired >= 2 && r4.CastTime > 0.3f && r4.CastMetres < 0.15f, "mobile: Shift roots a Mobile cast", $"fired {r4.Fired}, {r4.CastMetres:0.00} m in {r4.CastTime:0.00} s casting");

        // 5. the setting off: stop and shoot, exactly as before
        Player.MobileForce = false;
        Prime(p, h); p.Teleport(start); yield return 0.3f;
        var r5 = new WalkCast();
        yield return WalkAndCast(p, h, 0, dir, 2.0f, () => behind1, () => null, r5);
        Check(r5.Fired >= 3 && r5.CastTime > 0.3f && r5.CastMetres < 0.15f && !p.MobileCasting, "mobile: setting off — no walking during the shot (old behaviour)", $"fired {r5.Fired}, {r5.CastMetres:0.00} m in {r5.CastTime:0.00} s casting, free {Speed(r5.FreeMetres, r5.FreeTime):0.0} m/s");
        Check(r5.End.DistanceTo(r5.Start) < r1.End.DistanceTo(r1.Start) - 0.5f, "mobile: setting off — less ground covered in the same time", $"{r5.End.DistanceTo(r5.Start):0.0} m off vs {r1.End.DistanceTo(r1.Start):0.0} m on");
        Player.MobileForce = true;

        // 6. the real input path: the left button held on an enemy while steering away = a kite
        Prime(p, h); p.Teleport(start); yield return 0.3f;
        var dummy = Dummy("cultist", 20, start - dir * 7f, 5e8f);
        Vector2 pin = dummy.Pos2;
        _focusTarget = dummy; _focusDmg = 0f;
        yield return 0.3f;
        var cam = G.GetViewport().GetCamera3D();
        void AimAtDummy()
        {
            if (!GodotObject.IsInstanceValid(dummy)) return;
            dummy.Knock = Vector2.Zero; dummy.Pos2 = pin;
            if (cam != null) Player.TestMouse = cam.UnprojectPosition(dummy.GlobalPosition + Vector3.Up * 1.0f);
        }
        AimAtDummy();
        yield return null;
        Player.TestLeft = true;
        p.TestClick(0);
        int k0 = p.Fired; Vector2 s6 = p.Pos2;
        yield return 0.4f;
        Player.TestMove = dir;
        for (double t0 = G.Time; G.Time - t0 < 2.0;) { AimAtDummy(); yield return null; }
        Player.TestMove = Vector2.Zero; Player.TestLeft = false; Player.TestMouse = null;
        float away6 = p.Pos2.DistanceTo(pin) - s6.DistanceTo(pin);
        Check(p.Fired - k0 >= 4 && away6 > 4f && _focusDmg > 0f, "mobile: left button held on an enemy + W/A/S/D = kite (backs away, keeps shooting it)", $"fired {p.Fired - k0}, {away6:0.0} m further away, damage {_focusDmg:0}");
        yield return 0.4f;

        // 7. a click on an enemy out of reach still runs up to it, then shoots (no keys held)
        Prime(p, h); p.Teleport(start); yield return 0.2f;
        float reach = Player.ReachOf(h.Eff("hu_shot"), out _);
        Vector2 far = start + dir * Mathf.Min(room - 2f, reach + 4f);
        dummy.Pos2 = far; pin = dummy.Pos2;
        yield return 0.2f;
        int cs0 = p.ChaseStarts, ch0 = p.ChaseHits, kf = p.Fired;
        AimAtDummy(); yield return null;
        p.TestClick(0);
        for (double t0 = G.Time; G.Time - t0 < 3.0 && p.ChaseHits == ch0;) { AimAtDummy(); yield return null; }
        Player.TestMouse = null;
        bool farEnough = start.DistanceTo(pin) > reach + 0.5f;
        Check(!farEnough || p.ChaseStarts > cs0 && p.ChaseHits > ch0, "mobile: a click on an enemy out of reach runs up, then shoots", $"dist {start.DistanceTo(pin):0.0} reach {reach:0.0}, run-ups {p.ChaseStarts - cs0}, struck {p.ChaseHits - ch0}, fired {p.Fired - kf}");
        if (GodotObject.IsInstanceValid(dummy)) dummy.QueueFree();
        _focusTarget = null;
        Player.MobileForce = null;
        ClearField();
    }

    // ───────────────────────── every ranged class backs away while shooting ─────────────────────────

    private static readonly (ClassId cls, string skill, bool basic)[] MobileCases =
    {
        (ClassId.Hunter, "hu_shot", true), (ClassId.Hunter, "hu_pierce", false),
        (ClassId.Astromancer, "as_spark", true), (ClassId.Astromancer, "as_fireball", false),
        (ClassId.Soulbinder, "sb_bolt", true), (ClassId.Soulbinder, "sb_spear", false),
        (ClassId.Pactbinder, "pb_hexbolt", true), (ClassId.Pactbinder, "pb_chains", false),
        (ClassId.Wildsoul, "ws_storm", false), (ClassId.Forgebody, "fb_wave", false), (ClassId.Lampknight, "lk_lance", false),
    };

    private IEnumerator MobileClasses()
    {
        Player.MobileForce = true;
        ClassId cur = (ClassId)(-1);
        Hero h = null; Player p = null;
        Vector2 arena = default, dir = default; float room = 0f;
        foreach (var (cls, skill, basic) in MobileCases)
        {
            _section = $"mobile: {Names.ClassKey(cls)} {skill}";
            if (cls != cur)
            {
                yield return StartHero(cls, 30, "town");
                h = G.Hero; p = G.Player; cur = cls;
                if (h == null || h.Class != cls) { Fail($"mobile: {Names.ClassKey(cls)} not created"); cur = (ClassId)(-1); continue; }
                ClearField();
                arena = FindArena(World.Grid, G.Level.Checkpoint, 8);
                dir = OpenDir(arena, out room);
            }
            if (h == null || h.Class != cls) continue;
            var def = SkillDb.Get(skill);
            Configure(h, def, null, "base", 1);
            int slot = basic ? 0 : 2;
            Vector2 start = arena - dir * Mathf.Min(5f, room * 0.2f);
            Prime(p, h); p.Teleport(start);
            yield return 0.3f;
            var dummy = Dummy("cultist", 30, start - dir * 6f, 5e8f);
            Vector2 pin = dummy.Pos2;
            _focusTarget = dummy; _focusDmg = 0f;
            yield return 0.2f;
            var r = new WalkCast();
            float d0 = p.Pos2.DistanceTo(pin);
            int casts0 = p.MobileCastsMoving;
            yield return WalkAndCast(p, h, slot, dir, 2.5f, () => GodotObject.IsInstanceValid(dummy) ? dummy.Pos2 : pin, () => GodotObject.IsInstanceValid(dummy) ? dummy : null, r,
                () => { if (GodotObject.IsInstanceValid(dummy)) { dummy.Knock = Vector2.Zero; dummy.Pos2 = pin; } });
            yield return 1.0f;      // slow projectiles and ground strikes land
            float away = p.Pos2.DistanceTo(pin) - d0;
            Check(r.Fired >= 1 && away > 3f && p.MobileCastsMoving > casts0 && _focusDmg > 0f, $"mobile: {Names.ClassKey(cls)} backs away while casting {skill} at an enemy",
                $"casts {r.Casts}, fired {r.Fired}, {away:0.0} m further away ({r.CastMetres:0.0} m during casts), damage {_focusDmg:0}");
            _focusTarget = null;
            if (GodotObject.IsInstanceValid(dummy)) dummy.QueueFree();
            ClearField();
            yield return 0.2f;
        }
        Player.MobileForce = null;
    }

    // ───────────────────────── pictures ─────────────────────────

    private IEnumerator MobileShots()
    {
        Player.MobileForce = true;
        foreach (var (cls, skill) in new[] { (ClassId.Hunter, "hu_shot"), (ClassId.Astromancer, "as_fireball"), (ClassId.Soulbinder, "sb_spear"), (ClassId.Pactbinder, "pb_hexbolt") })
        {
            _section = $"mobile: shots {Names.ClassKey(cls)}";
            yield return StartHero(cls, 30, "town");
            var h = G.Hero; var p = G.Player;
            if (h == null || h.Class != cls) continue;
            ClearField();
            Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 8);
            Vector2 dir = OpenDir(arena, out float room);
            foreach (var side in new[] { Vector2.Right, Vector2.Left })      // sideways on the wide screen when there is room: hero and target both in the picture
            {
                var e = World.Grid?.ClampLine(arena - side * 6f, arena + side * 8f, false) ?? arena + side * 8f;
                if (e.DistanceTo(arena) > 7f && (World.Grid == null || World.Grid.LineWalkWide(arena - side * 6f, e, 0.5f))) { dir = side; room = 14f; break; }
            }
            var def = SkillDb.Get(skill);
            Configure(h, def, null, "base", 1);
            int slot = def.Cat == SkillCat.Basic ? 0 : 2;
            Vector2 start = arena - dir * Mathf.Min(5f, room * 0.2f);
            Prime(p, h); p.Teleport(start);
            yield return 5.0f;          // the area banner fades first
            var dummy = Dummy("cultist", 30, start - dir * 5f, 5e8f);
            Vector2 pin = dummy.Pos2;
            yield return 0.3f;
            string key = Names.ClassKey(cls);
            int n = 0;
            var r = new WalkCast();
            double t0 = G.Time; double nextShot = 0.25;
            yield return WalkAndCast(p, h, slot, dir, 2.2f, () => dummy.Pos2, () => dummy, r, () =>
            {
                if (GodotObject.IsInstanceValid(dummy)) { dummy.Knock = Vector2.Zero; dummy.Pos2 = pin; }
                if (G.Time - t0 >= nextShot && n < 3) { nextShot += 0.3; G.HarnessShot($"Y:/shots/mobile/{key}_{skill}_backing_{++n}.png"); }
            });
            if (GodotObject.IsInstanceValid(dummy)) dummy.QueueFree();
            ClearField();
        }
        Player.MobileForce = null;
    }

    // ───────────────────────── damage per second while kiting ─────────────────────────

    /// <summary>The fair harness's fixed gear (DpsBases' seed) and a plain starter weapon at mid implicits.</summary>
    private static void MobileFixGear(Hero h, ClassId cls, int level)
    {
        int power = Math.Clamp(level * 10, 10, ItemDb.MaxPower);
        var grng = new Random(90000 + level * 131 + (int)cls * 7);
        ItemKind[] kinds = { ItemKind.Helm, ItemKind.Chest, ItemKind.Gloves, ItemKind.Pants, ItemKind.Boots, ItemKind.Amulet, ItemKind.Ring, ItemKind.Ring };
        EquipSlot[] slots = { EquipSlot.Helm, EquipSlot.Chest, EquipSlot.Gloves, EquipSlot.Pants, EquipSlot.Boots, EquipSlot.Amulet, EquipSlot.Ring1, EquipSlot.Ring2 };
        for (int i = 0; i < kinds.Length; i++)
        {
            var it = ItemDb.Generate(power, Rarity.Rare, cls, kinds[i], grng);
            it.ReqLevel = Math.Min(it.ReqLevel, level);
            h.D.Equip[(int)slots[i]] = it;
        }
        var g = ItemDb.Loadout(cls);
        var rng = new Random(level * 7919 + (int)cls);
        Item Make(string b)
        {
            var gb = ItemDb.BaseOf(b);
            var it = ItemDb.Generate(power, Rarity.Common, cls, gb.Off ? ItemKind.OffHand : ItemKind.Weapon, rng, b);
            it.ReqLevel = Math.Min(it.ReqLevel, level);
            it.Implicits.Clear();
            foreach (var (s, lo, hi) in gb.Implicit)
            {
                float v = (lo + (hi - lo) * (power / (float)ItemDb.MaxPower)) * 0.95f;
                it.Implicits.Add(new Affix { Stat = s, Value = StatInfo.IsPct(s) ? MathF.Round(v * 10f) / 10f : MathF.Max(1, MathF.Round(v)) });
            }
            return it;
        }
        h.D.Equip[(int)EquipSlot.MainHand] = Make(g.Starter);
        h.D.Equip[(int)EquipSlot.OffHand] = g.StarterOff != null ? Make(g.StarterOff) : null;
        h.Buffs.RemoveAll(b => b.Def.Id != "test_god");
        h.Recalc();
        h.Notify();
    }

    /// <summary>
    /// mobile:dps[:classes[:levels[:secs]]] — per class and level, the bot's own rotation on one dummy:
    /// stand: pinned at 7 m; kite-off / kite-on: the dummy walks at the hero (55 % of the hero's speed) and the hero must
    /// keep 8 m — off, it stops to shoot only when far enough (each shot roots it); on, it shoots on the move.
    /// "caught" = seconds the dummy was within 2.5 m of the hero.
    /// </summary>
    private IEnumerator MobileDps(string[] parts)
    {
        if (_bot == null) { Fail("mobile:dps: no bot"); yield break; }
        var classes = new List<ClassId>();
        if (parts.Length > 1 && parts[1].Length > 0 && parts[1] != "all")
            foreach (var key in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            { int idx = Array.IndexOf(Names.ClassKeys, key.ToLowerInvariant()); if (idx >= 0) classes.Add((ClassId)idx); }
        if (classes.Count == 0) classes.AddRange(new[] { ClassId.Hunter, ClassId.Astromancer, ClassId.Soulbinder, ClassId.Pactbinder, ClassId.Emberblade, ClassId.Lampknight });
        var levels = new List<int>();
        if (parts.Length > 2) foreach (var l in parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries)) if (int.TryParse(l, out int lv)) levels.Add(Math.Clamp(lv, 1, Balance.LevelCap));
        if (levels.Count == 0) levels.AddRange(new[] { 30, 50 });
        double window = parts.Length > 3 && double.TryParse(parts[3], out double w) ? Math.Clamp(w, 10.0, 120.0) : 40.0;
        foreach (int level in levels)
            foreach (var cls in classes)
            {
                _section = $"mobile:dps {Names.ClassKey(cls)} lv{level}";
                yield return StartHero(cls, level, "town");
                var h = G.Hero; var p = G.Player;
                if (h == null || h.Class != cls) { Fail($"{_section}: hero not created"); continue; }
                MobileFixGear(h, cls, level);
                // no dash, leap or blink in this harness (the bot dashes INTO its target: against a chaser that undoes the kite); the
                // same bar in all three modes
                for (int i = 0; i < h.D.Bar.Length; i++) { var bd = h.Eff(h.D.Bar[i]); if (bd != null && bd.Kind is SkillKind.Dash or SkillKind.Leap or SkillKind.Blink) h.D.Bar[i] = null; }
                Log.Info("suite", $"mobile-dps {Names.ClassKey(cls)} lv{level}: bar=[{string.Join(",", h.D.Bar.Select(b => b ?? "-"))}] weapon {h.MainHand?.Base}{(h.OffHand != null ? "+" + h.OffHand.Base : "")}");
                yield return 0.5f;
                ClearField();
                Vector2 arena = FindArena(World.Grid, G.Level.Checkpoint, 8);
                bool ranged = new[] { ClassId.Hunter, ClassId.Astromancer, ClassId.Soulbinder, ClassId.Pactbinder }.Contains(cls);
                var res = new Dictionary<string, (float dps, float caught, float moved)>();
                foreach (string mode in ranged ? new[] { "stand", "kite-off", "kite-on" } : new[] { "stand" })
                {
                    Player.MobileForce = mode != "kite-off";
                    ClearField();
                    p.CancelAction(); p.SetForm(0); p.Cds.Clear(); p.Stop();
                    h.Buffs.RemoveAll(b => b.Def.Id != "test_god"); h.Recalc();
                    p.Resource = Names.IsBuilder(cls) ? 0f : h.MaxResource; p.Pips = 0;
                    bool stand = mode == "stand";
                    // the lane: an open straight line from the arena; at its end hero, chaser and every shot in flight step
                    // back together (a treadmill: the same as an endless open field)
                    Vector2 lane = OpenDir(arena, out float laneLen);
                    laneLen = Mathf.Clamp(laneLen - 2f, 6f, 18f);
                    Vector2 laneA = arena;
                    p.Teleport(stand ? arena : laneA);
                    yield return 0.3f;
                    float startD = stand ? (ranged ? 7f : 2.4f) : 7f;
                    var target = Dummy("cultist", level, stand ? arena + new Vector2(startD, 0f) : laneA - lane * startD, 5e8f);
                    Vector2 cpos = stand ? target.Pos2 : laneA - lane * startD;
                    _focusTarget = target; _focusDmg = 0f; _focusCdDmg = 0f; _dmg = 0f; _hits = 0; _minionDmg = 0f; _otherDmg = 0f;
                    _bot.ResetStats();
                    double t0 = G.Time, last = G.Time, think = -1, flee = -1;
                    float caught = 0f, moved = 0f, keep = 6f, vc = 0.55f * Player.BaseSpeed * h.MoveMul;
                    int shifts = 0;
                    bool fleeing = false;          // hysteresis: flee below 6 m, until 9 m (a player gets several shots per gap)
                    Vector2 prevHero = p.Pos2;
                    while (G.Time - t0 < window && GodotObject.IsInstanceValid(target) && !target.Dead)
                    {
                        float dt = (float)(G.Time - last); last = G.Time;
                        moved += p.Pos2.DistanceTo(prevHero); prevHero = p.Pos2;
                        if (!stand)
                        {
                            Vector2 to = p.Pos2 - cpos;
                            if (to.Length() > 1.2f) cpos += to.Normalized() * Mathf.Min(to.Length() - 1.2f, vc * dt);
                            if ((p.Pos2 - laneA).Dot(lane) > laneLen && (!p.Busy || p.MobileCasting))      // not in the middle of a dash (it would land back at its end)
                            {
                                // treadmill: everything steps back one lane length
                                Vector2 back = -lane * (laneLen - 1f);
                                p.Teleport(p.Pos2 + back);
                                cpos += back;
                                foreach (var q in Projectile.Live) if (GodotObject.IsInstanceValid(q)) q.GlobalPosition += World.To3(back);
                                foreach (var c in World.Root.GetChildren()) if (c is GroundZone z && GodotObject.IsInstanceValid(z)) z.GlobalPosition += World.To3(back);
                                prevHero = p.Pos2; flee = -1; shifts++;
                            }
                        }
                        target.Knock = Vector2.Zero; target.Pos2 = cpos;
                        float dist = p.Pos2.DistanceTo(cpos);
                        if (dist < 2.5f) caught += dt;
                        if (!stand && dist < keep) fleeing = true; else if (dist >= keep + 3f) fleeing = false;
                        bool tooClose = !stand && fleeing;
                        if (tooClose && G.Time >= flee)
                        {
                            flee = G.Time + 0.2;
                            Vector2 spot = p.Pos2 + lane * 4f;
                            p.SetMove(World.Grid?.NearestWalk(spot) ?? spot, false);
                        }
                        // off: a shot roots, so it only shoots when far enough; on: it shoots on the move
                        if (G.Time >= think && (stand || mode == "kite-on" || !tooClose)) { think = G.Time + 0.12; _bot.FightStep(target); }
                        yield return null;
                    }
                    if (!stand) Log.Info("suite", $"mobile-dps lane {laneLen:0} m, {shifts} treadmill steps");
                    _bot.FightStop();
                    float secs = (float)Math.Max(1.0, G.Time - t0);
                    res[mode] = (_focusDmg / secs, caught, moved);
                    _bot.Stats($"mobile:dps {Names.ClassKey(cls)} lv{level} {mode}");
                    _focusTarget = null;
                    if (GodotObject.IsInstanceValid(target)) target.QueueFree();
                }
                Player.MobileForce = null;
                ClearField();
                string line = string.Join(" | ", res.Select(kv => $"{kv.Key} {kv.Value.dps:0}/s caught {kv.Value.caught:0.0}s moved {kv.Value.moved:0}m"));
                float baseDps = res.TryGetValue("stand", out var sd) ? sd.dps : 0f;
                string ratio = ranged && baseDps > 0 ? $" | kite-off {res["kite-off"].dps / baseDps:0.00}x stand, kite-on {res["kite-on"].dps / baseDps:0.00}x stand, on/off {res["kite-on"].dps / Math.Max(1f, res["kite-off"].dps):0.00}x" : "";
                Log.Info("suite", $"mobile-dps {Names.ClassKey(cls)} lv{level} speed {Player.MobileSpeedMul:0.00}: {line}{ratio} | expected {Balance.ExpectedDps(level):0}");
            }
    }
}
