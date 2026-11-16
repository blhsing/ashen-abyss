// Skill SFX: fire, frost, lightning, shadow, ember, ranged, movement, summons, traps, barriers.
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public static partial class Sfx
{
    static float[] Explosion(Rng r, double size, double tail)
    {
        // size 1 = meteor-class; scales sub depth and durations
        var m = Mx().Add(White(r, 0.03).Env(AD(0.0003, 0.025)), -1)
            .Add(Thump(95 / Math.Sqrt(size), 27 / Math.Sqrt(size) + 8, 0.14 * size, 0.75 * size), 0)
            .Add(White(r, 1.2 * size).SvfLP(PtsE(0, 9000, 0.25 * size, 1500, 1.2 * size, 150), 0.7).Env(AD(0.004, 1.0 * size)), -2)
            .Add(Body(r, 130, 0.8, 0.4 * size), -3)
            .Add(Crackle(r, 1.6 * size, Exp(600, 8, 1.6 * size), 250, 4000, 0.004, 0.04, 2), -7, 0.05)
            .Add(FireBed(r, tail, 60, 300, 18).Env(AD(0.05, tail)), -10, 0.05);
        return m.Out();
    }

    static void Skills()
    {
        Cat("skills");
        Add("fire_cast", 2, -6, v =>
        {
            var r = v.R; double d = 0.55; var fl = Wander(r, 0.6, 1, 26, d);
            return Mx().Add(Pink(r, d).SvfBP(PtsE(0, 250, d * 0.45, 1600, d, 500), 0.9).Env(PtsS(0, 0, d * 0.3, 1, d, 0)), 0)
                .Add(Brown(r, d).LP(300).Env(t => fl(t)).Env(Bell(d, 0.4)), -5)
                .Add(Crackle(r, d, t => 80 * Bell(d, 0.4)(t), 1200, 7000, 0.002, 0.012, 2.5), -9).Add(Click(r, 1800, 0.7, 0.02), -8).Out();
        }, desc: "fire spell ignition whoosh");
        Add("fire_impact", 3, -3, v =>
        {
            var r = v.R;
            return Mx().Add(Thump(r.R(95, 120), 42, 0.07, 0.32), 0)
                .Add(White(r, 0.6).SvfLP(PtsE(0, 6000, 0.5, 350), 0.8).Env(AD(0.003, 0.5)), -3)
                .Add(Brown(r, 0.7).LP(250).Env(AD(0.01, 0.6)), -6)
                .Add(Crackle(r, 0.9, Exp(200, 12, 0.9), 1000, 6000, 0.002, 0.015, 2.2), -8).Out();
        }, desc: "fireball burst");
        Add("fire_loop", 1, -9, v => FireBed(v.R, 3.5, 38, 340, 16).LoopXfade(N(3.0)), loop: true, desc: "sustained flame (channelled fire / burning ground), loop");
        Add("frost_cast", 2, -7, v =>
        {
            var r = v.R; double d = 0.55, f = r.R(2200, 2800);
            var sh = Tones(r, d + 0.35, RGlass.Take(6).Select((q, i) => (f * q, Math.Pow(0.7, i))).Concat(RGlass.Take(4).Select((q, i) => (f * 1.5 * q * 1.004, 0.5 * Math.Pow(0.7, i)))), 0.5)
                .Env(PtsS(0, 0, 0.25, 1, d + 0.35, 0));
            return Mx().Add(sh, 0).Add(White(r, d).SvfBP(PtsE(0, 3000, d, 9000), 2).Env(Bell(d, 0.6)), -9)
                .Add(Crackle(r, d, Lin(10, 90, d), 5000, 12000, 0.003, 0.02, 1.5), -10).Add(Sine(d, Exp(180, 320, d)).Env(Bell(d, 0.6)), -16).Out();
        }, desc: "frost spell: rising glassy shimmer");
        Add("frost_impact", 3, -3, v =>
        {
            var r = v.R;
            return Mx().Add(Click(r, r.R(4500, 6500), 1, 0.006), 0).Add(Click(r, 2500, 1.2, 0.012), -3)
                .Add(Metal(r, r.R(1700, 2400), r.R(0.3, 0.5), 6, RGlass, 0.6), -4).Add(Thump(130, 70, 0.03, 0.1), -6).Add(Body(r, 600, 2, 0.04), -8)
                .Add(Shards(r, 0.35, r.I(10, 16), 3000, 9000, 0.03, 0.09), -9, 0.01).Out();
        }, desc: "ice shard impact");
        Add("frost_shatter", 2, -2, v =>
        {
            var r = v.R; var m = Mx();
            m.Add(Click(r, 5000, 0.8, 0.006), 0).Add(Click(r, 3500, 1, 0.008), -2, r.R(0.03, 0.06)).Add(Click(r, 4200, 1, 0.008), -4, r.R(0.09, 0.14));
            m.Add(Shards(r, 0.8, 45, 2000, 10000, 0.03, 0.12), -2).Add(Metal(r, 1300, 0.6, 6, RGlass, 0.6), -8)
             .Add(Thump(110, 50, 0.05, 0.2), -5).Add(White(r, 0.35).HP(6000).Env(AD(0.005, 0.3)), -12);
            return m.Out();
        }, desc: "frozen enemy shatters");
        Add("lightning_cast", 2, -6, v =>
        {
            var r = v.R; double d = 0.45; var nz = White(r, d).LP(900);
            var buzz = Saw(d, t => Exp(90, 140, d)(t) * (1 + 0.03 * Math.Sin(TAU * 31 * t)));
            for (int i = 0; i < buzz.Length; i++) buzz[i] *= 0.4f + 0.6f * Math.Abs(nz[i]) * 3;
            return Mx().Add(buzz.HP(300).Drive(3).Env(PtsS(0, 0, d * 0.7, 1, d, 0)), 0)
                .Add(Crackle(r, d, Lin(40, 300, d), 3000, 12000, 0.0005, 0.003, 1.5), -6).Add(Sine(d, 100).Env(Bell(d, 0.6)), -14).Out();
        }, desc: "lightning charge buzz");
        Add("lightning_zap", 3, -2, v =>
        {
            var r = v.R; double d = r.R(0.22, 0.3), f = r.R(95, 135);
            var gate = Square(d, t => f * (1 + 0.05 * Math.Sin(TAU * 17 * t))); var nz = White(r, d).HP(1800);
            for (int i = 0; i < nz.Length; i++) nz[i] *= 0.5f + 0.5f * gate[i];
            var m = Mx().Add(White(r, 0.012).Env(AD(0.0002, 0.01)), 0).Add(nz.Env(AD(0.002, d)), -4)
                .Add(Crackle(r, d, Exp(900, 60, d), 2500, 12000, 0.0005, 0.004, 1.5), -5).Add(Thump(160, 70, 0.03, 0.1), -9).Out();
            return m.Reverb(0.35, -16, 0.4);
        }, desc: "lightning strike: snap + buzz");
        Add("thunder", 2, -2, v =>
        {
            var r = v.R; double d = 3.2; var roll = Wander(r, 0.25, 1, 5, d);
            var m = Mx().Add(White(r, 0.06).LP(7000).Env(AD(0.0005, 0.05)), -1).Add(White(r, 0.06).LP(5000).Env(AD(0.0005, 0.05)), -4, r.R(0.04, 0.09))
                .Add(Brown(r, d).SvfLP(PtsE(0, 600, 0.6, 220, d, 70), 0.7).Env(t => roll(t) * AD(0.02, d)(t)), 0)
                .Add(Pink(r, 0.6).BP(400, 0.7).Env(AD(0.005, 0.5)), -6).Add(Thump(70, 30, 0.3, 1.2), -4).Out();
            return m.Reverb(1.6, -9, 1.2, 2500);
        }, pv: 1.0, desc: "thunder crack and rolling rumble");
        Add("shadow_cast", 2, -6, v =>
        {
            var r = v.R; double d = 0.6;
            return Mx().Add(Sine(d, Exp(210, 48, d)).Env(Bell(d, 0.25)), 0)
                .Add(White(r, 0.45).BP(900, 1.2).Env(AD(0.002, 0.4)).Rev(), -5)
                .Add(White(r, d).SvfBP(Wander(r, 500, 1800, 6, d, true), 3).Env(Bell(d, 0.6)), -8)
                .Add(Mx().Add(Saw(d, 55)).Add(Saw(d, 56.7)).Out().LP(400).Env(Bell(d, 0.5)), -10)
                .Add(White(r, d).HP(5000).Env(Wander(r, 0, 1, 9, d)).Env(Bell(d, 0.5)), -19).Out();
        }, desc: "shadow bolt cast: down-swept sub, reversed swell, breath");
        Add("shadow_impact", 3, -3, v =>
        {
            var r = v.R;
            var m = Mx().Add(Thump(100, 36, 0.09, 0.35), 0)
                .Add(White(r, 0.4).SvfBP(PtsE(0, 1500, 0.4, 250), 1.5).Env(AD(0.004, 0.35)), -5)
                .Add(Mx().Add(Saw(0.5, 58)).Add(Saw(0.5, 82)).Out().LP(500).Drive(2).Env(AD(0.01, 0.45)), -8)
                .Add(Squelch(r, 0.5, 1500, 6000, 18, 3).Env(AD(0.03, 0.5)), -14).Out();
            return m.Reverb(0.6, -12, 0.6, 2500);
        }, desc: "shadow bolt impact: dull implosion");
        Add("ember_buff", 2, -7, v =>
        {
            var r = v.R; double d = 1.3, f = v.Idx == 0 ? 220 : 246.94;
            return Mx().Add(Tones(r, d, Harm(f, 7, 1.2), 0.45).Env(PtsS(0, 0, 0.35, 1, 0.7, 0.8, d, 0)), 0)
                .Add(Tones(r, d, Harm(f * 1.5, 5, 1.3), 0.45).Env(PtsS(0, 0, 0.45, 1, 0.8, 0.8, d, 0)), -8)
                .Add(Crackle(r, d, t => 45 * Bell(d, 0.4)(t), 3500, 10000, 0.01, 0.05, 1.5), -15)
                .Add(Pink(r, d).SvfBP(PtsE(0, 300, 0.5, 1500, d, 600), 1).Env(Bell(d, 0.35)), -10).Out();
        }, pv: 0, desc: "ember buff: warm shimmering harmonic swell");
        Add("bolt_shoot", 3, -5, v =>
        {
            var r = v.R; double f = r.R(420, 560);
            return Mx().Add(Click(r, r.R(2800, 3800), 1, 0.005), 0)
                .Add(Modes(0.1, new[] { (f, 0.045, 1.0), (f * 1.6, 0.04, 0.6), (f * 2.3, 0.035, 0.4), (f * 3.1, 0.03, 0.25) }), -2)
                .Add(Saw(0.12, Exp(260, 180, 0.1)).LP(1800).Env(AD(0.001, 0.09)).Trem(85, 0.5), -8)
                .Add(Click(r, 1500, 3, 0.01), -8, 0.018).Add(Whoosh(r, 0.16, PtsE(0, 4000, 0.16, 1500), 2, AD(0.01, 0.14)), -10, 0.01)
                .Add(Thump(150, 90, 0.02, 0.05), -8).Out();
        }, desc: "hand crossbow shot");
        Add("bolt_impact", 3, -5, v =>
        {
            var r = v.R; double f = r.R(260, 380);
            return Mx().Add(Click(r, 3500, 1.2, 0.004), -2)
                .Add(Modes(0.14, new[] { (f, 0.07, 1.0), (f * 1.52, 0.06, 0.6), (f * 2.2, 0.05, 0.4), (f * 2.9, 0.045, 0.25) }), 0)
                .Add(Squelch(r, 0.07, 600, 3000, 60, 3), -12).Add(Sine(0.2, r.R(170, 230)).Trem(r.R(28, 40), 0.7).Env(AD(0.002, 0.18)), -14)
                .Add(Thump(110, 70, 0.02, 0.07), -6).Out();
        }, desc: "bolt thunk");
        Add("knife_throw", 2, -8, v =>
        {
            var r = v.R; double d = 0.3; var rate = Lin(26, 34, d); double ph = 0;
            var spin = White(r, d).SvfBP(PtsE(0, 2500, d, 4500), 2.5);
            for (int i = 0; i < spin.Length; i++) { ph += rate(i / (double)SR) / SR; double a = 0.5 + 0.5 * Math.Sin(TAU * ph); spin[i] *= (float)(0.25 + 0.75 * a * a); }
            return Mx().Add(spin.Env(Bell(d, 0.3)), 0).Add(Metal(r, 3200, 0.2, 3, RBar), -16).Add(Whoosh(r, d, C(700), 1, Bell(d, 0.3)), -9).Out();
        }, desc: "spinning thrown blade");
        Add("whirlwind_loop", 1, -8, v =>
        {
            var r = v.R; double T = 0.4, L = 1.2, d = L + 0.2;
            var air = Pink(r, d).SvfBP(t => 450 * Math.Pow(2, 1.6 * (0.5 + 0.5 * Math.Sin(TAU * t / T))), 1.3).Env(t => 0.35 + 0.65 * (0.5 + 0.5 * Math.Sin(TAU * t / T - 0.6)));
            var wh = White(r, d).SvfBP(t => 2500 * Math.Pow(2, 0.5 * (0.5 + 0.5 * Math.Sin(TAU * t / T))), 6).Env(t => Math.Pow(0.5 + 0.5 * Math.Sin(TAU * t / T - 0.6), 2));
            var low = Brown(r, d).LP(160).Env(t => 0.6 + 0.4 * Math.Sin(TAU * t / T));
            return Mx().Add(air, 0).Add(wh, -12).Add(low, -8).Out().LoopXfade(N(L));
        }, loop: true, desc: "spinning blade whirlwind (3 swooshes), loop");
        Add("ground_slam", 3, -1, v =>
        {
            var r = v.R;
            var m = Mx().Add(Thump(r.R(72, 88), 30, 0.1, 0.38), 0).Add(Click(r, r.R(1800, 2600), 0.7, 0.018), -2).Add(Body(r, r.R(140, 220), 0.9, 0.16), -1)
                .Add(Crackle(r, 0.7, Exp(700, 15, 0.7), 300, 3500, 0.004, 0.03, 2), -6, 0.02)
                .Add(Pink(r, 0.55).SvfBP(PtsE(0, 2500, 0.5, 600), 0.8).Env(AD(0.01, 0.5)), -12).Out();
            return m.Reverb(0.5, -14, 0.6);
        }, desc: "heavy ground slam with rock debris");
        Add("leap_land", 2, -4, v =>
        {
            var r = v.R;
            return Mx().Add(Thump(90, 45, 0.05, 0.2), 0).Add(Body(r, 250, 1, 0.09), -2).Add(Shards(r, 0.1, 6, 1500, 4000, 0.02, 0.05, RPlate.Take(3).ToArray()), -14, 0.01)
                .Add(Crackle(r, 0.3, Exp(300, 15, 0.3), 500, 4000, 0.003, 0.015, 2), -12, 0.02).Add(Click(r, 2000, 1, 0.01), -6).Out();
        }, desc: "leap landing");
        Add("war_cry", 2, -3, v =>
        {
            var r = v.R; double d = r.R(0.8, 0.9), x = v.Idx == 0 ? 1 : 1.07;
            Fn f0 = PtsE(0, 150 * x, 0.08, 205 * x, d * 0.6, 195 * x, d, 140 * x); var keys = new[] { (0.0, VUH), (0.1, VA), (d * 0.7, VA), (d, VO) };
            var env = PtsS(0, 0, 0.04, 1, d * 0.75, 0.85, d, 0);
            var v1 = Voice(r, d, f0, keys, 1, 0.12, 0.025, 0.2, 0.35, 48, 0.2).Drive(2.5).Env(env);
            var v2 = Voice(r, d, t => f0(t) * 0.985, keys, 0.96, 0.12, 0.03, 0.2, 0.3, 43, 0.2).Drive(2.5).Env(env);
            var bloom = Tones(r, d, new[] { (110.0, 1.0), (165.0, 0.6), (220.0, 0.4) }).Env(Bell(d, 0.4));
            return Mx().Add(v1, 0).Add(v2, -4).Add(bloom, -13).Out().LP(6500).Reverb(0.9, -11, 0.7);
        }, pv: 0, desc: "warrior shout (synthesised) with warm bloom");
        Add("teleport", 2, -5, v =>
        {
            var r = v.R; double d1 = 0.2; Fn rise = t => Math.Pow(Clamp(t / d1, 0, 1), 2);
            var m = Mx().Add(White(r, d1).SvfBP(PtsE(0, 400, d1, 5000), 3).Env(rise).Fade(0, 0.004), -2).Add(Sine(d1, Exp(300, 2600, d1)).Env(rise).Fade(0, 0.004), -8)
                .Add(Click(r, 2500, 0.8, 0.01), 0, d1).Add(Thump(180, 80, 0.02, 0.08), -4, d1)
                .Add(Shards(r, 0.35, 14, 3000, 9000, 0.03, 0.1), -8, d1).Add(Sine(0.3, Exp(2400, 900, 0.3)).Env(AD(0.002, 0.25)), -12, d1).Out();
            return m.Reverb(0.5, -12, 0.5);
        }, desc: "blink: suck-in, pop, sparkle");
        Add("summon_raise", 2, -4, v =>
        {
            var r = v.R; double d = 1.1;
            var ghost = Voice(r, d, Exp(110, 180, d), new[] { (0.0, VU), (d, VO) }, 1, 0.6, 0.02, 0.15).Chorus(14, 7, 0.5, 0.5).Env(Bell(d, 0.6));
            var m = Mx().Add(BoneRattle(r, 1.0, 14, 500, 1100, 0.75, 0.5, 1), 0)
                .Add(Pink(r, d).SvfBP(PtsE(0, 120, d, 600), 1.2).Env(PtsS(0, 0, d * 0.8, 1, d, 0)), -3).Add(ghost, -10)
                .Add(Sine(d, Exp(40, 70, d)).Env(Bell(d, 0.7)), -8).Add(Thump(90, 50, 0.04, 0.15), -6, d - 0.08).Out();
            return m.Reverb(0.8, -10, 0.7);
        }, desc: "minion rises: bones rattling up + dark whoosh");
        Add("soul_wisp", 2, -8, v =>
        {
            var r = v.R; double d = 0.9, x = v.Idx == 0 ? 1 : 1.12; Fn f = PtsE(0, 620 * x, d * 0.5, 1040 * x, d, 880 * x);
            var t1 = Sine(d, t => f(t) * (1 + 0.012 * Math.Sin(TAU * 6.5 * t))).Env(Bell(d, 0.35));
            var t2 = Sine(d, t => f(t) * 1.498 * (1 + 0.012 * Math.Sin(TAU * 6.1 * t))).Env(Bell(d, 0.4));
            var br = White(r, d).SvfBP(Wander(r, 700, 2400, 5, d, true), 5).Env(Bell(d, 0.4));
            return Mx().Add(t1, 0).Add(t2, -10).Add(br, -6).Out().Chorus(12, 6, 0.8, 0.5).Reverb(1.0, -8, 0.8);
        }, pv: 0, desc: "soul remnant wisp");
        Add("meteor_fall", 1, -4, v =>
        {
            var r = v.R; double d = 2.2; Fn up = PtsE(0, 0.04, d, 1); var fl = Wander(r, 0.55, 1, 28, d);
            var wh = Mx().Add(Saw(d, PtsE(0, 1400, d, 180))).Add(Sine(d, PtsE(0, 1400, d, 180)), -3).Out().LP(3000).Env(up);
            return Mx().Add(Pink(r, d).SvfBP(PtsE(0, 3500, d, 350), 0.8).Env(up), 0).Add(wh, -9)
                .Add(Brown(r, d).LP(350).Env(t => fl(t) * up(t)), -4)
                .Add(Crackle(r, d, Lin(20, 250, d), 1500, 8000, 0.002, 0.01, 2.5).Env(up), -9).Out().Fade(0.01, 0.02);
        }, desc: "meteor descending (long, cut by the explosion)");
        Add("meteor_explode", 2, -1, v => Explosion(v.R, 1.0, 1.8).Reverb(1.4, -10, 1.1, 3000), pv: 1.0, desc: "meteor explosion");
        Add("trap_arm", 1, -8, v =>
        {
            var r = v.R; var m = Mx(); double[] ts = { 0, 0.07, 0.13, 0.18 };
            foreach (var t in ts) m.Add(Mx().Add(Click(r, 2800, 4, 0.006)).Add(Metal(r, r.R(3200, 3800), 0.03, 3), -4).Out(), -4, t);
            m.Add(Creak(r, 0.22, Lin(180, 320, 0.22), new[] { 1200.0, 2100 }, 6).Env(Bell(0.22, 0.6)), -13, 0.02);
            m.Add(Mx().Add(Click(r, 1800, 2, 0.012)).Add(Metal(r, 1400, 0.1, 6), -2).Add(Thump(160, 100, 0.01, 0.03), -8).Out(), 0, 0.27);
            return m.Out();
        }, desc: "trap ratchet and latch");
        Add("trap_trigger", 2, -3, v =>
        {
            var r = v.R;
            var twang = Mx().Add(Sine(0.35, PtsE(0, 700, 0.08, 420, 0.35, 410))).Add(Sine(0.35, PtsE(0, 1750, 0.08, 1050, 0.35, 1020)), -6).Out().Trem(55, 0.6).Env(AD(0.001, 0.3));
            return Mx().Add(Click(r, 3500, 0.8, 0.008), 0).Add(twang, -6).Add(Whoosh(r, 0.22, PtsE(0, 2500, 0.1, 7000, 0.22, 5000), 3, Bell(0.22, 0.4), false), -5, 0.01)
                .Add(Metal(r, 3800, 0.35, 4, RBar), -10, 0.03).Add(Clack(r, 500, 0.05), -3).Add(Thump(130, 70, 0.02, 0.08), -6).Out();
        }, desc: "trap snaps: spring twang + blade");
        Add("nova", 2, -5, v =>
        {
            var r = v.R; double d = 0.85;
            var m = Mx().Add(White(r, d).SvfBP(PtsE(0, 300, 0.12, 4500, d, 500), 1.1).Env(PtsS(0, 0, 0.06, 1, 0.2, 0.6, d, 0)), 0)
                .Add(Thump(120, 45, 0.06, 0.3), -2).Add(Metal(r, r.R(900, 1300), 0.6, 5, RGlass), -10).Add(White(r, 0.65).HP(4000).Env(AD(0.05, 0.6)), -16).Out();
            return m.Reverb(0.6, -12, 0.6);
        }, desc: "expanding ring burst");
        Add("chain", 2, -5, v =>
        {
            var r = v.R; var m = Mx(); int n = r.I(12, 16);
            for (int k = 0; k < n; k++) m.Add(Metal(r, r.X(1800, 4200), r.R(0.03, 0.08), 5), ToDb(r.R(0.35, 0.9)), 0.4 * Math.Pow((k + r.U() * 0.5) / n, 0.85));
            m.Add(Whoosh(r, 0.4, PtsE(0, 800, 0.2, 2600, 0.4, 1200), 1.5, Bell(0.4, 0.5)), -8)
             .Add(Metal(r, r.R(820, 980), 0.25, 8), -1, 0.42).Add(Thump(120, 70, 0.02, 0.08), -8, 0.42);
            return m.Out();
        }, desc: "chain lash: rattling links whip out and catch");
        Add("barrier_up", 1, -6, v =>
        {
            var r = v.R; double d = 0.8; Fn gl = Exp(98, 196, 0.3);
            var bw = Mx().Add(Sine(d, gl)).Add(Sine(d, t => gl(t) * 2), -6).Add(Sine(d, t => gl(t) * 3), -12).Out().Env(PtsS(0, 0, 0.12, 1, 0.4, 0.7, d, 0));
            var m = Mx().Add(bw, 0).Add(Metal(r, 1568, 0.8, 5, RGlass, 0.4), -8, 0.1)
                .Add(White(r, 0.35).SvfBP(PtsE(0, 500, 0.3, 5000), 2).Env(Bell(0.35, 0.7)), -10).Out();
            return m.Reverb(0.7, -12, 0.6);
        }, desc: "barrier forms");
        Add("barrier_break", 1, -3, v =>
        {
            var r = v.R;
            return Mx().Add(Click(r, 4000, 0.8, 0.008), 0).Add(Shards(r, 0.5, 30, 1800, 9000), -3).Add(Sine(0.4, Exp(900, 120, 0.35)).Env(AD(0.003, 0.35)), -8)
                .Add(Thump(120, 50, 0.05, 0.2), -4).Add(White(r, 0.3).SvfLP(PtsE(0, 8000, 0.3, 600), 0.8).Env(AD(0.002, 0.25)), -6).Out();
        }, desc: "barrier shatters");
        Add("buff_expire", 1, -14, v =>
        {
            var r = v.R;
            return Mx().Add(Sine(0.25, 587.33).Env(AD(0.01, 0.22)), 0).Add(Sine(0.34, 466.16).Env(AD(0.01, 0.3)), -1, 0.16)
                .Add(White(r, 0.45).BP(2000, 2).Env(Bell(0.45, 0.4)), -14).Out().LP(3000);
        }, desc: "buff fades: soft falling third");
    }
}
