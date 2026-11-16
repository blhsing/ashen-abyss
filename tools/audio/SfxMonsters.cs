// Monster SFX: skeletons, husks, cultists, wraiths, ember fiends, brutes, bosses.
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public static partial class Sfx
{
    static (double, Vowel)[] K(params (double, Vowel)[] k) => k;

    /// <summary>Demon / beast roar stack: main voice + fifth + scream layer + noise + sub, driven.</summary>
    static float[] Roar(Rng r, double d, Fn f0, double scale, double drive, double growlHz, double subHz, Fn env, bool scream = true)
    {
        var keys = K((0, VO), (d * 0.18, VA), (d * 0.7, VA), (d, VO));
        var v1 = Voice(r, d, f0, keys, scale, 0.25, 0.05, 0.35, 0.65, growlHz, 0.7).Drive(drive);
        var v2 = Voice(r, d, t => f0(t) * 1.498, keys, scale * 1.12, 0.3, 0.05, 0.3, 0.5, growlHz * 1.3, 0.3).Drive(drive * 0.6);
        var m = Mx().Add(v1, 0).Add(v2, -6);
        if (scream) m.Add(Voice(r, d, t => f0(t) * 2.02, keys, scale * 1.3, 0.5, 0.04, 0.3).Drive(3), -10);
        m.Add(Pink(r, d).SvfBP(t => 4.5 * f0(t) + 250, 0.7), -5).Add(Sine(d, t => subHz * f0(t) / f0(d * 0.4)), -9);
        return m.Out().Env(env);
    }

    static void Monsters()
    {
        Cat("monsters");
        Add("skeleton_idle", 3, -10, v =>
        {
            var r = v.R;
            var breath = White(r, 0.7).SvfBP(Wander(r, 600, 1400, 4, 0.7, true), 2).Env(Bell(0.7, 0.4)).Comb(0.0045, 0.72, 5000);
            return Mx().Add(BoneRattle(r, 0.6, r.I(4, 7), 700, 1000, 1, 1, 0.7), 0).Add(breath, -9).Add(Clack(r, r.R(1100, 1500), 0.03), -6, r.R(0.3, 0.6)).Out().Reverb(0.25, -14, 0.35);
        }, desc: "skeleton: bone rattle and hollow breath");
        Add("skeleton_attack", 2, -5, v =>
        {
            var r = v.R; double d = 0.5; var src = White(r, d).HP(500); var g = Glottal(r, d, Exp(260, 340, d), 0.05, 0.3);
            for (int i = 0; i < src.Length; i++) src[i] += 0.5f * g[i];
            var hiss = Formant(src, K((0, VE), (d, VA)), 1.1).Comb(0.0032, 0.78, 6000).Env(PtsS(0, 0, 0.03, 1, 0.3, 0.8, d, 0));
            return Mx().Add(hiss, 0).Add(BoneRattle(r, 0.2, 5, 900, 1300), -4).Add(Whoosh(r, 0.25, PtsE(0, 600, 0.12, 2400, 0.25, 900), 1.5, Bell(0.25, 0.5)), -12, 0.1).Out().Reverb(0.25, -15, 0.35);
        }, desc: "skeleton attack: hollow shriek + rattle");
        Add("skeleton_death", 3, -4, v =>
        {
            var r = v.R;
            var exhale = Formant(White(r, 0.55).HP(400), K((0, VO), (0.55, VU))).Comb(0.005, 0.7, 4000).Env(AD(0.02, 0.5));
            return Mx().Add(BoneRattle(r, 0.9, 18, 1200, 450, 0.7, 1, 0.35), 0).Add(Clack(r, r.R(520, 680), 0.07), -1).Add(exhale, -10)
                .Add(Thump(130, 80, 0.02, 0.06), -8, 0.1).Add(Thump(120, 75, 0.02, 0.06), -9, 0.35)
                .Add(Pink(r, 0.5).BP(2500, 0.7).Env(AD(0.05, 0.45)), -17, 0.1).Out();
        }, desc: "skeleton collapses into a bone pile");
        Add("husk_idle", 4, -8, v =>
        {
            var r = v.R; double d = r.R(0.7, 1.0); var w = Wander(r, 95, 140, 3, d); Fn env = PtsS(0, 0, 0.1, 1, d * 0.6, 0.7, d, 0);
            var vo = Voice(r, d, t => w(t) * (1 - 0.15 * t / d), K((0, VUH), (d * 0.5, VER), (d, VU)), 0.95, 0.35, 0.05, 0.3, 0.5, r.R(28, 40), 0.15).Drive(2).Env(env);
            return Mx().Add(vo, 0).Add(Squelch(r, d, 200, 900, 22, 6).Env(env), -9).Out().LP(4500);
        }, pv: 1.0, desc: "husk: raspy gurgling moan");
        Add("husk_attack", 3, -4, v =>
        {
            var r = v.R; double d = 0.4, x = r.R(0.92, 1.1); Fn env = PtsS(0, 0, 0.02, 1, 0.25, 0.9, d, 0);
            var vo = Voice(r, d, PtsE(0, 170 * x, 0.1, 290 * x, d, 200 * x), K((0, VAE), (0.15, VA), (d, VE)), 1, 0.3, 0.06, 0.35, 0.6, 55).Drive(4).Env(env);
            return Mx().Add(vo, 0).Add(White(r, d).SvfBP(PtsE(0, 1500, 0.15, 3200, d, 2000), 4).Env(env), -8).Out();
        }, pv: 1.0, desc: "husk: snarling screech");
        Add("husk_death", 3, -4, v =>
        {
            var r = v.R; double d = 0.9, x = r.R(0.92, 1.08);
            var vo = Voice(r, d, PtsE(0, 230 * x, 0.15, 250 * x, d, 80 * x), K((0, VA), (0.4, VUH), (d, VU)), 1, 0.4, 0.07, 0.35, 0.5, 30).Drive(3).Env(PtsS(0, 0, 0.03, 1, 0.5, 0.7, d, 0));
            return Mx().Add(vo, 0).Add(Squelch(r, 0.6, 150, 1200, 30, 5).Env(Bell(0.6, 0.3)), -6, 0.3).Out().LP(6000);
        }, pv: 1.0, desc: "husk: falling gurgled cry");
        Add("bloat_explode", 2, -1, v =>
        {
            var r = v.R; var m = Mx();
            m.Add(Sine(0.12, Exp(60, 140, 0.12)).Env(Lin(0, 1, 0.12)).Fade(0, 0.004), -10)
             .Add(Thump(100, 36, 0.08, 0.4), 0, 0.1).Add(Squelch(r, 0.5, 250, 5000, 70, 3.5, 0.8).Env(AD(0.003, 0.45)), -1, 0.1)
             .Add(White(r, 0.3).SvfLP(PtsE(0, 5000, 0.3, 400), 1).Env(AD(0.002, 0.25)), -5, 0.1).Add(White(r, 0.55).BP(3500, 0.8).Env(AD(0.02, 0.5)), -12, 0.12);
            for (int k = 0; k < 10; k++)
                m.Add(Mx().Add(Squelch(r, 0.05, 600, 3000, 80, 6).Env(AD(0.002, 0.04))).Add(Bubble(r, r.R(300, 700), 0.04, 1.5), -4).Out(), r.R(-20, -12), r.R(0.25, 1.05));
            return m.Out();
        }, desc: "bloated husk bursts: wet explosion and drips");
        Add("cultist_idle", 3, -12, v =>
        {
            var r = v.R; int syl = r.I(4, 6); double sd = r.R(0.24, 0.3), d = syl * sd + 0.1, f = r.R(96, 118);
            var vs = new[] { VM, VU, VO, VUH, VM, VU, VO }; var keys = new List<(double, Vowel)>(); var pitch = new List<double>(); var amp = new List<double>();
            for (int k = 0; k <= syl; k++)
            {
                keys.Add((k * sd, vs[(k + r.I(3)) % vs.Length])); double st = r.Pick(0.0, 0.0, 1, -2, 0, 3);
                pitch.Add(k * sd); pitch.Add(f * Semi(st));
                amp.Add(k * sd); amp.Add(0.15); amp.Add(k * sd + sd * 0.4); amp.Add(r.R(0.7, 1));
            }
            amp.Add(d); amp.Add(0);
            var pf = PtsS(pitch.ToArray());
            var vo = Voice(r, d, t => pf(t) * (1 + 0.01 * Math.Sin(TAU * 4.7 * t)), keys.ToArray(), 1, 0.08, 0.012, 0.08).Env(PtsS(amp.ToArray())).Fade(0.03, 0.08);
            return vo.LP(2200).Reverb(0.5, -14, 0.5);
        }, pv: 1.0, desc: "cultist: muffled tonal chant murmurs (no words)");
        Add("cultist_attack", 2, -5, v =>
        {
            var r = v.R; double d = 0.32, x = v.Idx == 0 ? 1 : 0.92;
            var vo = Voice(r, d, PtsE(0, 190 * x, 0.05, 215 * x, d, 150 * x), K((0, VUH), (0.06, VA), (d, VUH)), 1, 0.25, 0.03, 0.15, 0.25, 60).Drive(2.2).Env(PtsS(0, 0, 0.015, 1, 0.18, 0.8, d, 0));
            return Mx().Add(vo, 0).Add(White(r, 0.07).HP(2000).Env(AD(0.002, 0.06)), -12).Out().LP(6500);
        }, pv: 0.7, desc: "cultist: sharp attack shout");
        Add("cultist_death", 2, -5, v =>
        {
            var r = v.R; double d = 0.9, x = v.Idx == 0 ? 1 : 0.9;
            var vo = Voice(r, d, PtsE(0, 200 * x, 0.1, 180 * x, d, 90 * x), K((0, VA), (0.5, VO), (d, VU)), 1, 0.3, 0.04, 0.2, 0.3, 35).Env(PtsS(0, 0, 0.02, 1, 0.45, 0.75, d, 0));
            return Mx().Add(vo, 0).Add(Squelch(r, 0.2, 300, 1500, 30, 5).Env(Bell(0.2, 0.4)), -12, 0.55).Out().LP(5500).Reverb(0.5, -16, 0.5);
        }, pv: 0.7, desc: "cultist: dying groan");
        Add("wraith_idle", 3, -9, v =>
        {
            var r = v.R; double d = 1.8, x = r.R(0.9, 1.12); Fn b = PtsE(0, 380 * x, 0.6, 470 * x, 1.2, 420 * x, d, 330 * x);
            Fn f0 = t => b(t) * (1 + 0.02 * Math.Sin(TAU * 5.5 * t));
            var m = Mx().Add(Voice(r, d, f0, K((0, VU), (0.8, VO), (d, VU)), 1.15, 0.8, 0.01, 0.1), 0)
                .Add(Voice(r, d, t => f0(t) * 0.5, K((0, VO), (d, VU)), 1.1, 0.5, 0.01, 0.1), -8)
                .Add(White(r, d).SvfBP(Wander(r, 1500, 4000, 3, d, true), 2).Env(Bell(d, 0.5)), -10).Out();
            return m.Chorus(15, 8, 0.4, 0.5).Chorus(22, 5, 0.27, 0.4).Env(Bell(d, 0.4, 0.8)).Reverb(1.4, -6, 0.9, 4000);
        }, pv: 1.0, desc: "wraith: airy chorused wail");
        Add("wraith_attack", 2, -5, v =>
        {
            var r = v.R; double d = 0.75, x = v.Idx == 0 ? 1 : 1.1; Fn env = PtsS(0, 0, 0.05, 1, 0.55, 0.9, d, 0);
            var m = Mx().Add(Voice(r, d, PtsE(0, 420 * x, 0.25, 900 * x, 0.6, 820 * x, d, 600 * x), K((0, VO), (0.25, VA), (d, VE)), 1.15, 0.6, 0.02, 0.15).Env(env), 0)
                .Add(White(r, d).SvfBP(PtsE(0, 2000, 0.3, 4500, d, 3000), 3).Env(env), -6).Out();
            return m.Chorus(13, 7, 0.6, 0.5).Reverb(0.9, -8, 0.8, 5000);
        }, pv: 0.7, desc: "wraith: rising shriek");
        Add("wraith_death", 2, -5, v =>
        {
            var r = v.R; double d = 2.0, x = v.Idx == 0 ? 1 : 0.9;
            var vo = Voice(r, d, PtsE(0, 700 * x, 0.2, 760 * x, d, 180 * x), K((0, VA), (0.8, VO), (d, VU)), 1.15, 0.9, 0.02, 0.2)
                .Env(t => (1 - 0.6 * (t / d) * (0.5 + 0.5 * Math.Sin(TAU * (6 + 10 * t / d) * t))) * PtsS(0, 0, 0.05, 1, 1.0, 0.7, d, 0)(t));
            var m = Mx().Add(vo, 0).Add(White(r, d).SvfBP(PtsE(0, 5000, d, 800), 1.5).Env(Bell(d, 0.6)), -8).Out();
            return m.Chorus(15, 8, 0.4, 0.5).Reverb(1.8, -5, 1.0, 4000);
        }, pv: 0.7, desc: "wraith: falling wail dissolving into air");
        Add("fiend_idle", 3, -7, v =>
        {
            var r = v.R; double d = r.R(0.9, 1.15); var w = Wander(r, 85, 115, 3, d); Fn env = PtsS(0, 0, 0.12, 1, d * 0.6, 0.8, d, 0);
            var gh = r.R(30, 42);
            var vo = Voice(r, d, t => w(t) * (1 - 0.12 * t / d), K((0, VO), (d * 0.5, VA), (d, VUH)), 0.85, 0.2, 0.04, 0.3, 0.7, gh, 0.4).Drive(5).LP(5000).Env(env);
            return Mx().Add(vo, 0).Add(Sine(d, 45).Trem(gh, 0.8).Env(env), -10).Add(Crackle(r, d, 20, 1500, 6000), -20).Out();
        }, pv: 1.2, desc: "ember fiend: low distorted growl (pitch up for imp)");
        Add("fiend_attack", 2, -3, v =>
        {
            var r = v.R; double d = 0.55, x = v.Idx == 0 ? 1 : 1.1; Fn env = PtsS(0, 0, 0.02, 1, 0.3, 0.85, d, 0);
            var vo = Voice(r, d, PtsE(0, 120 * x, 0.07, 190 * x, d, 110 * x), K((0, VUH), (0.1, VA), (d, VO)), 0.85, 0.2, 0.05, 0.3, 0.6, 50, 0.4).Drive(7).Env(env);
            return Mx().Add(vo, 0).Add(White(r, d).BP(1200, 0.7).Env(env), -9).Out().LP(7000);
        }, pv: 0.8, desc: "ember fiend: bark-roar");
        Add("fiend_death", 2, -3, v =>
        {
            var r = v.R; double d = 1.3, x = v.Idx == 0 ? 1 : 1.08;
            var vo = Voice(r, d, PtsE(0, 180 * x, 0.15, 170 * x, d, 60 * x), K((0, VA), (0.6, VO), (d, VU)), 0.85, 0.25, 0.06, 0.35, 0.65, 34, 0.5).Drive(6).Env(PtsS(0, 0, 0.03, 1, 0.6, 0.75, d, 0));
            return Mx().Add(vo, 0).Add(Crackle(r, 1.0, Exp(150, 10, 1.0), 1000, 6000, 0.002, 0.012, 2), -9, 0.4)
                .Add(White(r, 0.8).HP(3500).Env(Bell(0.8, 0.3)), -15, 0.5).Out().LP(7000);
        }, pv: 0.8, desc: "ember fiend: death roar with sputtering embers");
        Add("brute_idle", 2, -6, v =>
        {
            var r = v.R; double d = 1.6; var w = Wander(r, 48, 62, 2, d); Fn env = PtsS(0, 0, 0.25, 1, d * 0.6, 0.8, d, 0);
            var vo = Voice(r, d, t => w(t), K((0, VU), (0.8, VO), (d, VU)), 0.62, 0.3, 0.05, 0.3, 0.6, r.R(18, 26), 0.6).Drive(3).LP(2500).Env(env);
            return Mx().Add(vo, 0).Add(Sine(d, 34).Env(env), -6).Add(Pink(r, d).BP(350, 0.8).Env(Bell(d, 0.45)), -8).Out();
        }, pv: 0.8, desc: "lava brute: very low rumbling breath-growl");
        Add("brute_attack", 2, -2, v =>
        {
            var r = v.R; double d = 1.2, x = v.Idx == 0 ? 1 : 0.92;
            var ro = Roar(r, d, PtsE(0, 60 * x, 0.2, 95 * x, 0.8, 85 * x, d, 55 * x), 0.62, 6, 30, 38, PtsS(0, 0, 0.06, 1, d * 0.65, 0.9, d, 0), false);
            return ro.Reverb(0.8, -12, 0.8, 3500);
        }, pv: 0.6, desc: "lava brute: big roar");
        Add("brute_stomp", 2, -1, v =>
        {
            var r = v.R;
            var m = Mx().Add(Thump(65, 28, 0.1, 0.4), 0).Add(Body(r, 120, 0.8, 0.22), -2).Add(Click(r, 1500, 0.7, 0.02), -5)
                .Add(Crackle(r, 0.8, Exp(500, 10, 0.8), 200, 2500, 0.005, 0.04, 2), -7, 0.02).Add(Brown(r, 0.7).LP(90).Env(AD(0.02, 0.6)), -6).Out();
            return m.Reverb(0.6, -13, 0.7, 3000);
        }, desc: "lava brute stomp");
        Add("brute_death", 1, -1, v =>
        {
            var r = v.R; double d = 2.4;
            var ro = Roar(r, d, PtsE(0, 100, 0.3, 90, d, 38), 0.62, 5, 24, 36, PtsS(0, 0, 0.06, 1, 1.2, 0.8, d, 0), false);
            var crash = Mx().Add(Thump(70, 27, 0.12, 0.6), 0).Add(Body(r, 110, 0.8, 0.3), -2).Add(Crackle(r, 1.0, Exp(600, 10, 1.0), 200, 3000, 0.005, 0.04, 2), -6).Out();
            return Mx().Add(ro, -2).Add(crash, 0, 1.9).Add(White(r, 1.0).HP(3000).Env(Bell(1.0, 0.2)), -16, 2.0).Out().Reverb(1.0, -12, 0.9, 3000);
        }, desc: "lava brute: long falling groan and crash");
        Add("boss_roar", 3, -1, v =>
        {
            var r = v.R; double d = r.R(2.4, 2.8), x = 1 + 0.07 * (v.Idx - 1);
            var ro = Roar(r, d, PtsE(0, 70 * x, 0.3, 110 * x, d * 0.6, 100 * x, d, 55 * x), 0.7, 8, r.R(24, 34), 42, PtsS(0, 0, 0.08, 1, d * 0.65, 0.9, d, 0));
            return ro.Reverb(1.6, -9, 1.2, 3500);
        }, pv: 0.5, desc: "boss roar: layered, distorted, sub-heavy");
        Add("boss_slam", 2, -1, v =>
        {
            var r = v.R;
            var m = Mx().Add(Thump(85, 26, 0.16, 0.65), 0).Add(White(r, 0.04).Env(AD(0.0003, 0.035)), -2).Add(Body(r, 110, 0.7, 0.35), -1)
                .Add(Crackle(r, 1.4, Exp(900, 8, 1.4), 180, 3000, 0.006, 0.05, 1.8), -5, 0.03).Add(Brown(r, 1.3).LP(70).Env(AD(0.03, 1.2)), -5)
                .Add(Pink(r, 0.9).SvfBP(PtsE(0, 2000, 0.8, 400), 0.8).Env(AD(0.02, 0.8)), -13).Out();
            return m.Reverb(1.2, -10, 1.2, 3000);
        }, desc: "boss ground slam");
        Add("boss_phase", 1, -1, v =>
        {
            var r = v.R; double t0 = 0.9; Fn rise = t => Math.Pow(Clamp(t / t0, 0, 1), 2.5);
            var swell = Mx().Add(White(r, t0).SvfBP(PtsE(0, 300, t0, 4000), 2).Env(rise)).Add(Tones(r, t0, new[] { (73.4, 1.0), (77.8, 0.8), (110.0, 0.7), (155.6, 0.5) }).Env(rise), -3).Out().Fade(0, 0.005);
            var roar = Roar(r, 1.3, PtsE(0, 90, 0.2, 120, 1.3, 60), 0.7, 8, 30, 40, PtsS(0, 0, 0.05, 1, 0.8, 0.85, 1.3, 0));
            var choir = Mx(); foreach (var f in new[] { 73.42, 155.56, 220.0 }) choir.Add(Voice(r, 2.2, C(f), K((0, VU), (1.0, VA), (2.2, VO)), 1, 0.1, 0.01, 0.06).Env(PtsS(0, 0, 0.3, 1, 1.2, 0.8, 2.2, 0)));
            var m = Mx().Add(swell, -5).Add(Explosion(r, 0.9, 1.0), 0, t0).Add(roar, -3, t0 + 0.08).Add(choir.Out(), -9, t0).Add(BellTone(r, 146.83, 2.2, RBell, 0.7), -8, t0).Out();
            return m.Reverb(2.0, -8, 1.2, 3500);
        }, desc: "boss phase change: swell, blast, roar, dark choir and bell");
        Add("boss_death", 1, -1, v =>
        {
            var r = v.R; double d = 2.6;
            var roar = Roar(r, d, PtsE(0, 110, 0.4, 120, d, 40), 0.7, 8, 26, 40, t => PtsS(0, 0, 0.06, 1, 1.4, 0.8, d, 0)(t) * (1 - 0.4 * (t / d) * (0.5 + 0.5 * Math.Sin(TAU * 9 * t))));
            var rumble = Brown(r, 3.8).LP(120).Env(PtsS(0, 0, 0.6, 0.6, 2.2, 1, 3.8, 0));
            var rocks = Crackle(r, 3.6, PtsE(0, 80, 2.1, 400, 3.6, 20), 200, 3000, 0.005, 0.04, 2);
            var sand = White(r, 3.2).SvfBP(PtsE(0, 7000, 3.2, 1500), 1.2).Env(Bell(3.2, 0.3));
            var sparks = Crackle(r, 3.2, t => 40 * Bell(3.2, 0.3)(t), 4000, 11000, 0.01, 0.06, 1.2);
            var ember = Mx().Add(Tones(r, 2.8, Harm(220, 6, 1.3), 0.5)).Add(Tones(r, 2.8, Harm(330, 5, 1.4), 0.5), -4).Out().Env(Bell(2.8, 0.35));
            var m = Mx().Add(roar, -1).Add(rumble, -6, 0.3).Add(rocks, -8, 0.3).Add(Explosion(r, 1.0, 1.2), 0, 2.4)
                .Add(sand, -9, 2.6).Add(sparks, -13, 2.6).Add(ember, -14, 2.8).Out();
            return m.Reverb(2.4, -8, 1.2, 3500);
        }, desc: "boss death (long): dying roar, collapse, blast, ash and ember release");
        Add("elite_spawn", 1, -3, v =>
        {
            var r = v.R; double t0 = 0.7;
            var swell = Mx().Add(Saw(t0, 55)).Add(Saw(t0, 58.27)).Add(Saw(t0, 82.41)).Out().SvfLP(PtsE(0, 120, t0, 1600), 1.2).Env(t => Math.Pow(t / t0, 2)).Fade(0, 0.005);
            var growl = Voice(r, 0.6, PtsE(0, 80, 0.6, 60), K((0, VO), (0.6, VU)), 0.75, 0.2, 0.05, 0.3, 0.7, 32, 0.5).Drive(5).Env(Bell(0.6, 0.3));
            var m = Mx().Add(swell, -4).Add(Thump(95, 35, 0.1, 0.5), 0, t0).Add(BellTone(r, 420, 1.1, RBell, 0.7), -6, t0).Add(growl, -8, t0 + 0.05).Out();
            return m.Reverb(1.2, -9, 1.0, 3500);
        }, desc: "elite appears: dark swell, hit, metallic sting");
        Add("telegraph_warn", 1, -6, v =>
        {
            var r = v.R;
            float[] Tick(double f0, double f1, double d) => Mx().Add(Sine(d, Exp(f0, f1, d * 0.8))).Add(Sine(d, Exp(f0 * 3, f1 * 3, d * 0.8)), -12).Add(Click(r, 3000, 2, 0.003), -8).Out().Env(AD(0.002, d));
            return Mx().Add(Tick(900, 1300, 0.07), -2).Add(Tick(1300, 1900, 0.12), 0, 0.09).Out();
        }, desc: "ground-danger telegraph: short rising double tick");
    }
}
