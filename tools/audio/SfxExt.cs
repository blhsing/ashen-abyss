// Scope extension: four more classes (shapeshifter, martial artist, holy shield-bearer, demon-binder), new elements/monsters, system sounds.
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public static partial class Sfx
{
    static void Ext()
    {
        // ---------------- Nature / beast ----------------
        Cat("skills");
        Add("earth_cast", 2, -4, v =>
        {
            var r = v.R; double d = 0.75;
            return Mx().Add(Brown(r, d).LP(140).Env(Bell(d, 0.55)), 0).Add(Crackle(r, d, t => 500 * Bell(d, 0.6)(t), 200, 1500, 0.006, 0.03, 1.8), -5)
                .Add(Sine(d, Exp(40, 72, d)).Env(Bell(d, 0.6)), -4).Add(Click(r, 1800, 0.7, 0.02), -3, 0.45).Add(Clack(r, r.R(260, 340), 0.08), -5, 0.45).Out();
        }, desc: "earth magic: rock rumble + crack");
        Add("earth_impact", 3, -2, v =>
        {
            var r = v.R;
            var m = Mx().Add(Thump(90, 38, 0.07, 0.3), 0).Add(Clack(r, r.R(240, 360), 0.09), -2).Add(Click(r, r.R(1500, 2200), 0.7, 0.016), -3)
                .Add(Crackle(r, 0.6, Exp(900, 15, 0.6), 200, 3000, 0.004, 0.035, 2), -5, 0.01).Add(Pink(r, 0.4).SvfBP(PtsE(0, 1800, 0.4, 500), 0.8).Env(AD(0.01, 0.35)), -13).Out();
            return m.Reverb(0.4, -15, 0.5);
        }, desc: "boulder / stone spike impact");
        Add("wind_cast", 2, -7, v =>
        {
            var r = v.R; double d = 0.7;
            return Mx().Add(Pink(r, d).SvfBP(PtsE(0, 300, 0.3, 1800, d, 500), 1.0).Env(Bell(d, 0.4)), 0)
                .Add(White(r, d).SvfBP(PtsE(0, 1500, 0.3, 3500, d, 2200), 12).Env(Bell(d, 0.4, 2)), -12)
                .Add(Squelch(r, d, 3000, 9000, 30, 1.2, 0.9).Env(Bell(d, 0.5)), -14).Add(Brown(r, d).LP(200).Env(Bell(d, 0.4)), -8).Out();
        }, desc: "wind gust cast");
        Add("storm_bolt", 2, -2, v =>
        {
            var r = v.R; double d = 0.3, f = r.R(100, 130); var gate = Square(d, f); var nz = White(r, d).HP(1600);
            for (int i = 0; i < nz.Length; i++) nz[i] *= 0.5f + 0.5f * gate[i];
            var m = Mx().Add(White(r, 0.014).Env(AD(0.0002, 0.012)), 0).Add(nz.Env(AD(0.002, d)), -4).Add(Crackle(r, d, Exp(800, 60, d), 2500, 12000, 0.0005, 0.004, 1.5), -6)
                .Add(Brown(r, 0.9).SvfLP(PtsE(0, 500, 0.9, 90), 0.7).Env(AD(0.01, 0.85)), -3, 0.02)
                .Add(Pink(r, 0.6).SvfBP(PtsE(0, 400, 0.25, 1500, 0.6, 600), 1).Env(Bell(0.6, 0.35)), -10).Out();
            return m.Reverb(0.7, -12, 0.8, 3500);
        }, desc: "storm bolt: snap, buzz, short thunder and gust");
        Add("beast_roar", 3, -2, v =>
        {
            var r = v.R; double d = r.R(1.4, 1.8), x = 1 + 0.08 * (v.Idx - 1);
            var ro = Roar(r, d, PtsE(0, 95 * x, 0.2, 150 * x, d * 0.6, 130 * x, d, 80 * x), 0.8, 3.2, r.R(26, 36), 48, PtsS(0, 0, 0.07, 1, d * 0.6, 0.85, d, 0), false);
            return ro.LP(6500).Reverb(0.6, -13, 0.7);
        }, pv: 0.5, desc: "shapeshifter: big animal roar (synthesised)");
        Add("beast_claw", 4, -5, v =>
        {
            var r = v.R; var m = Mx(); double d = 0.13;
            for (int k = 0; k < 3; k++) m.Add(White(r, d).SvfBP(PtsE(0, 3000, d * 0.5, r.R(6000, 7500), d, 3500), 5).Env(Bell(d, 0.4, 2)), -k * 1.5, k * r.R(0.01, 0.014));
            return m.Add(Squelch(r, 0.09, 800, 4000, 60, 3).Env(AD(0.004, 0.08)), -10, 0.04).Add(Whoosh(r, 0.16, PtsE(0, 500, 0.08, 1600, 0.16, 700), 1.3, Bell(0.16, 0.45)), -6).Out();
        }, desc: "fast triple claw swipe");
        Add("beast_bite", 2, -4, v =>
        {
            var r = v.R;
            var growl = Voice(r, 0.25, Exp(120, 90, 0.25), K((0, VUH), (0.25, VO)), 0.85, 0.3, 0.05, 0.3, 0.6, 40, 0.4).Drive(4).Env(Bell(0.25, 0.3));
            return Mx().Add(Clack(r, r.R(650, 800), 0.035), 0, 0.06).Add(Click(r, 3500, 1.5, 0.004), -3, 0.06).Add(Click(r, 4200, 1.5, 0.004), -5, 0.068)
                .Add(Squelch(r, 0.12, 500, 3500, 60, 4).Env(AD(0.004, 0.1)), -7, 0.065).Add(growl, -6).Add(Thump(120, 70, 0.02, 0.07), -8, 0.06).Out();
        }, desc: "jaws snap shut");
        Add("shapeshift", 2, -5, v =>
        {
            var r = v.R; double d = 0.75; var m = Mx();
            m.Add(Squelch(r, d, 200, 3000, 30, 4).Env(Bell(d, 0.45)), 0).Add(Pink(r, d).SvfBP(PtsE(0, 300, d * 0.5, 1600, d, 400), 1).Env(Bell(d, 0.5)), -4);
            for (int k = 0; k < 5; k++) m.Add(Clack(r, r.R(500, 1100), 0.03), r.R(-12, -6), r.R(0.08, 0.6));
            m.Add(Voice(r, d, v.Idx == 0 ? PtsE(0, 180, d, 80) : PtsE(0, 85, d, 170), K((0, VA), (d, VU)), 0.9, 0.3, 0.05, 0.3, 0.5, 38, 0.4).Drive(3).Env(Bell(d, 0.5)), -8);
            return m.Out().Reverb(0.5, -13, 0.5);
        }, pv: 0.7, desc: "flesh/fur morph (1 = into beast, 2 = back to human)");

        // ---------------- Martial artist ----------------
        Add("punch_whoosh", 4, -10, v =>
        {
            var r = v.R; double d = r.R(0.12, 0.16);
            return Mx().Add(Whoosh(r, d, PtsE(0, 800, d * 0.5, r.R(2600, 3400), d, 1200), 2, Bell(d, 0.5, 1.6)), 0).Add(Click(r, 2500, 1, 0.008), -11, d * 0.45).Out();
        }, desc: "fast strike whoosh");
        Add("punch_hit", 5, -3, v =>
        {
            var r = v.R;
            return Mx().Add(Thump(r.R(130, 150), 65, 0.025, 0.11), -3).Add(Click(r, r.R(1600, 2100), 1, 0.008), -1).Add(Body(r, r.R(340, 420), 1.5, 0.06), 0)
                .Add(White(r, 0.03).BP(2500, 0.8).Env(AD(0.001, 0.02)), -3).Out().LP(8000);
        }, desc: "hard fist impact with a tight low thump");
        Add("kick_heavy", 2, -2, v =>
        {
            var r = v.R;
            return Mx().Add(Whoosh(r, 0.06, PtsE(0, 600, 0.06, 2000), 1.5, t => t / 0.06).Fade(0, 0.003), -12).Add(Thump(110, 45, 0.05, 0.22), 0, 0.05).Add(Body(r, 240, 1.2, 0.08), -2, 0.05)
                .Add(Click(r, 2200, 1, 0.01), -3, 0.05).Add(Clack(r, r.R(450, 550), 0.03), -9, 0.056).Out();
        }, desc: "heavy kick");
        Add("chi_burst", 2, -3, v =>
        {
            var r = v.R;
            var m = Mx().Add(Pink(r, 0.6).SvfBP(PtsE(0, 400, 0.08, 2200, 0.6, 500), 0.9).Env(AD(0.01, 0.55)), 0).Add(Thump(130, 55, 0.05, 0.25), -3)
                .Add(Tones(r, 0.55, Harm(v.Idx == 0 ? 196 : 220, 5, 1.3), 0.4).Env(AD(0.02, 0.5)), -8).Add(White(r, 0.5).HP(5000).Env(AD(0.03, 0.45)), -16).Out();
            return m.Reverb(0.5, -12, 0.6);
        }, pv: 0.5, desc: "chi burst: warm airy blast");
        Add("chi_charge", 1, -8, v =>
        {
            var r = v.R; double d = 1.2; Fn rise = t => Math.Pow(Clamp(t / d, 0, 1), 1.5); Fn gl = Exp(98, 196, d); double ph = 0;
            var tone = Mx().Add(Sine(d, gl)).Add(Sine(d, t => gl(t) * 2), -5).Add(Sine(d, t => gl(t) * 3), -9).Add(Sine(d, t => gl(t) * 4.02), -13).Out();
            for (int i = 0; i < tone.Length; i++) { double t = i / (double)SR; ph += (6 + 8 * t / d) / SR; tone[i] *= (float)(0.65 + 0.35 * Math.Sin(TAU * ph)); }
            return Mx().Add(tone.Env(rise), 0).Add(White(r, d).SvfBP(PtsE(0, 500, d, 4000), 2).Env(rise), -8).Out().Fade(0.01, 0.03);
        }, desc: "chi gathering: rising pulsing tone (release with chi_burst)");

        // ---------------- Holy shield-bearer ----------------
        Add("holy_cast", 2, -6, v =>
        {
            var r = v.R; double d = 1.2, k = v.Idx == 0 ? 1 : 1.1225; var m = Mx(); Fn env = PtsS(0, 0, 0.35, 1, 0.7, 0.85, d, 0);
            foreach (var f in new[] { 220.0, 329.63, 440.0, 554.37 }) m.Add(Voice(r, d, C(f * k * r.R(0.998, 1.002)), K((0, VO), (0.5, VA), (d, VA)), 1.1, 0.06, 0.008, 0.05).Env(env), -4);
            m.Add(Tones(r, d, new[] { (880 * k, 1.0), (1318.5 * k, 0.6), (1760 * k, 0.35), (2637 * k, 0.2) }, 0.6).Env(env), -10).Add(BellTone(r, 880 * k, 0.9), -9, 0.3);
            return m.Out().Reverb(1.2, -8, 0.9, 6000);
        }, pv: 0, desc: "holy cast: warm bright choir-like swell");
        Add("holy_impact", 3, -3, v =>
        {
            var r = v.R; double f = new[] { 880.0, 987.77, 1046.5 }[v.Idx % 3];
            var m = Mx().Add(BellTone(r, f, 0.9), 0).Add(Thump(120, 60, 0.04, 0.2), -3).Add(White(r, 0.32).SvfBP(PtsE(0, 6000, 0.3, 1500), 1).Env(AD(0.003, 0.3)), -6)
                .Add(Voice(r, 0.5, C(f / 2), K((0, VA), (0.5, VO)), 1.1, 0.08, 0.01, 0.05).Env(AD(0.02, 0.45)), -10).Add(Shards(r, 0.3, 8, 4000, 9000, 0.04, 0.1), -15).Out();
            return m.Reverb(0.8, -10, 0.8, 6000);
        }, pv: 0, desc: "holy impact: bright bell burst");
        Add("shield_bash", 3, -2, v =>
        {
            var r = v.R;
            return Mx().Add(MetalHit(r, r.R(300, 420), 0.3, -3), 0).Add(Thump(100, 50, 0.05, 0.2), -7).Add(Body(r, 260, 1.2, 0.09), -4).Add(Squelch(r, 0.1, 500, 2500, 50, 3).Env(AD(0.004, 0.09)), -13).Out();
        }, desc: "shield slam: metal on body");
        Add("shield_block", 3, -3, v =>
        {
            var r = v.R;
            return Mx().Add(MetalHit(r, r.R(420, 560), r.R(0.3, 0.45)), 0).Add(Clack(r, r.R(180, 240), 0.07), -6).Add(Thump(110, 70, 0.03, 0.1), -5).Out();
        }, desc: "tower shield block");
        Add("aura_loop", 1, -16, v =>
        {
            var r = v.R; double L = 4.0, d = L + 0.5; int n = N(L); double Q(double f) => Math.Round(f * L) / L;
            var parts = new List<(double, double)>();
            foreach (var (f, a) in new[] { (440.0, 1.0), (659.25, 0.7), (880.0, 0.5), (1108.73, 0.3), (1318.5, 0.2) }) { parts.Add((Q(f), a)); parts.Add((Q(f) + 1 / L, a * 0.8)); parts.Add((Q(f) - 2 / L, a * 0.5)); }
            var hum = Tones(new Rng(11), L, parts, 0).Len(n);
            var sh = White(r, d).HP(5000).Env(Wander(r, 0.25, 1, 3, d)).LoopXfade(n);
            return Mx().Add(hum, 0).Add(sh, -14).Out().Len(n);
        }, loop: true, desc: "holy aura: soft beating shimmer, loop");
        Add("bell_toll", 2, -3, v =>
        {
            var r = v.R; double f = v.Idx == 0 ? 98.0 : 110.0;
            var m = Mx().Add(BellTone(r, f * 2, 5.0, RBell, 0.72), 0).Add(Click(r, 1500, 0.8, 0.012), -10).Add(Thump(f, f * 0.9, 0.05, 0.4), -8).Out();
            return m.Reverb(2.5, -10, 1.2, 4500);
        }, pv: 0, desc: "deep bell toll (G / A)");
        Add("consecrate", 1, -4, v =>
        {
            var r = v.R; double d = 2.2; var m = Mx(); Fn env = PtsS(0, 0, 0.5, 1, 1.1, 0.8, d, 0);
            m.Add(Thump(90, 45, 0.06, 0.4), -3).Add(Tones(r, d, new[] { 146.83, 220.0, 293.66, 369.99 }.SelectMany(q => Harm(q, 5, 1.4)), 0.4).Env(env), 0);
            foreach (var f in new[] { 146.83, 220.0, 293.66 }) m.Add(Voice(r, d, C(f), K((0, VO), (0.9, VA), (d, VO)), 1, 0.06, 0.008, 0.05).Env(env), -9);
            m.Add(BellTone(r, 880, 1.6), -9, 0.5).Add(White(r, 1.0).SvfBP(PtsE(0, 800, 1.0, 6000), 1.5).Env(Bell(1.0, 0.7)), -14).Add(Crackle(r, 1.6, 30, 4000, 10000, 0.01, 0.05, 1.4).Env(Bell(1.6, 0.4)), -20, 0.4);
            return m.Out().Reverb(1.8, -8, 1.0, 5500);
        }, desc: "consecrated ground: low hit, warm chord, choir, bell");

        // ---------------- Demon-binder ----------------
        Add("hex_cast", 2, -6, v =>
        {
            var r = v.R; double d = 0.6;
            var saws = Mx().Add(Saw(d, 110)).Add(Saw(d, 155.56)).Add(Saw(d, 111.5), -3).Out().SvfBP(Wander(r, 400, 1600, 9, d, true), 4).Env(Bell(d, 0.45));
            return Mx().Add(saws, 0).Add(Squelch(r, d, 1500, 6000, 22, 3).Env(Bell(d, 0.55)), -8).Add(Sine(d, Exp(160, 60, d)).Env(Bell(d, 0.3)), -6)
                .Add(White(r, 0.3).BP(1400, 1.5).Env(AD(0.002, 0.28)).Rev(), -8).Out();
        }, desc: "hex cast: dissonant wobble, whispers, sub drop");
        Add("hex_impact", 3, -3, v =>
        {
            var r = v.R;
            var m = Mx().Add(Thump(110, 40, 0.07, 0.3), 0).Add(Squelch(r, 0.3, 400, 4000, 45, 5).Env(AD(0.003, 0.28)), -4)
                .Add(BellTone(r, r.R(350, 400), 0.5, RBell, 0.8), -9).Add(Squelch(r, 0.4, 2000, 7000, 20, 3).Env(AD(0.03, 0.4)), -15).Out();
            return m.Reverb(0.5, -12, 0.6, 3000);
        }, desc: "hex impact: dark wet burst with a sour ring");
        Add("fiend_summon", 2, -2, v =>
        {
            var r = v.R; double t0 = 0.5; Fn rise = t => Math.Pow(Clamp(t / t0, 0, 1), 1.6); var rip = Crackle(r, t0, 900, 1500, 7000, 0.002, 0.008, 1.2);
            var tear = White(r, t0).SvfBP(PtsE(0, 500, t0, 5000), 3); for (int i = 0; i < tear.Length; i++) tear[i] *= 0.4f + 2f * Math.Abs(rip[i]);
            double x = v.Idx == 0 ? 1 : 0.9;
            var ro = Roar(r, 1.0, PtsE(0, 110 * x, 0.15, 170 * x, 1.0, 90 * x), 0.85, 7, 45, 44, PtsS(0, 0, 0.04, 1, 0.6, 0.85, 1.0, 0));
            var m = Mx().Add(tear.Env(rise).Fade(0, 0.004), -4).Add(Thump(100, 32, 0.1, 0.5), 0, t0).Add(ro, -2, t0 + 0.05)
                .Add(Crackle(r, 1.0, Exp(200, 10, 1.0), 1000, 6000, 0.002, 0.012, 2), -11, t0).Out();
            return m.Reverb(1.0, -10, 0.9, 3500);
        }, pv: 0.5, desc: "fiend summoned: tearing rift + roar");
        Add("pact_command", 2, -3, v =>
        {
            var r = v.R; double t0 = 0.08;
            var m = Mx().Add(Whoosh(r, t0, PtsE(0, 1500, t0, 8000), 2, t => Math.Pow(t / t0, 2), false).Fade(0, 0.002), -8)
                .Add(White(r, 0.008).Env(AD(0.0002, 0.006)), 0, t0).Add(Click(r, 5000, 0.8, 0.004), -2, t0)
                .Add(Sine(0.2, Exp(3000, 600, 0.15)).Env(AD(0.002, 0.15)), -10, t0).Add(Saw(0.12, r.R(130, 150)).HP(400).Env(AD(0.002, 0.1)), -12, t0).Out();
            return m.Reverb(0.4, -14, 0.5);
        }, desc: "pact command: whip-crack of energy");
        Add("sigil_gain", 1, -12, v =>
        {
            var r = v.R;
            return Mx().Add(BellTone(r, 739.99, 0.5, RBell, 0.6), 0).Add(BellTone(r, 523.25, 0.4, RBell, 0.6), -6, 0.06).Add(Squelch(r, 0.3, 2500, 7000, 20, 2).Env(Bell(0.3, 0.3)), -18).Out();
        }, desc: "sigil gained: small sour chime");
        Add("rift_open", 1, -2, v =>
        {
            var r = v.R; double t0 = 1.0; Fn rise = t => Math.Pow(Clamp(t / t0, 0, 1), 2);
            var tear = White(r, t0).SvfBP(t => 300 * Math.Pow(2, 4 * t / t0) * (1 + 0.2 * Math.Sin(TAU * 13 * t)), 3.5).Env(rise);
            var drone = Tones(r, 1.4, new[] { (55.0, 1.0), (58.3, 0.8), (77.8, 0.7), (110.0, 0.4), (116.5, 0.3) }, 0.5).Drive(2).Env(PtsS(0, 0, 0.05, 1, 0.5, 0.7, 1.4, 0));
            var vo = Voice(r, 0.9, Exp(240, 150, 0.9), K((0, VA), (0.9, VU)), 1, 0.7, 0.03, 0.2).Env(AD(0.01, 0.8)).Rev();
            var m = Mx().Add(tear.Fade(0, 0.004), -4).Add(vo, -10, 0.1).Add(Thump(95, 30, 0.12, 0.6), 0, t0).Add(White(r, 0.5).SvfLP(PtsE(0, 7000, 0.5, 400), 0.8).Env(AD(0.003, 0.45)), -4, t0)
                .Add(drone, -5, t0).Out();
            return m.Reverb(1.6, -8, 1.1, 3500);
        }, desc: "great rift tears open");

        // ---------------- Elements ----------------
        Add("poison_cast", 2, -8, v =>
        {
            var r = v.R; double d = 0.5; var m = Mx();
            m.Add(Squelch(r, d, 800, 4000, 35, 4).Env(Bell(d, 0.45)), 0).Add(White(r, d).HP(4500).Env(Bell(d, 0.5)), -10).Add(Sine(d, t => 140 * (1 + 0.2 * Math.Sin(TAU * 11 * t))).Env(Bell(d, 0.4)), -10);
            for (int k = 0; k < 5; k++) m.Add(Bubble(r, r.X(300, 900), r.R(0.04, 0.07)), r.R(-9, -4), r.R(0.02, d - 0.1));
            return m.Out();
        }, desc: "poison cast: bubbling hiss");
        Add("poison_splash", 3, -5, v =>
        {
            var r = v.R; var m = Mx(); var siz = Crackle(r, 0.65, 700, 3000, 9000, 0.001, 0.004, 1.2); var hs = White(r, 0.65).HP(3500);
            for (int i = 0; i < hs.Length; i++) hs[i] *= 0.3f + 2.5f * Math.Abs(siz[i]);
            m.Add(Squelch(r, 0.2, 300, 5000, 70, 3.5).Env(AD(0.003, 0.18)), 0).Add(Thump(120, 70, 0.03, 0.08), -8).Add(hs.Env(AD(0.02, 0.6)), -6, 0.02);
            for (int k = 0; k < 4; k++) m.Add(Bubble(r, r.X(400, 1000), r.R(0.03, 0.06)), r.R(-16, -11), r.R(0.1, 0.5));
            return m.Out();
        }, desc: "poison splash: wet splat with sizzling hiss");
        Add("ice_crack", 3, -4, v =>
        {
            var r = v.R; var m = Mx(); int n = r.I(5, 8);
            m.Add(Click(r, 5500, 0.8, 0.005), 0).Add(Sine(0.08, Exp(400, 250, 0.05)).Env(AD(0.001, 0.07)), -8).Add(Metal(r, r.R(1800, 2600), 0.25, 5, RGlass, 0.6), -10);
            for (int k = 0; k < n; k++) m.Add(Click(r, r.X(3000, 7000), 1.5, r.R(0.003, 0.006)), -4 - k * 1.5 + r.R(-2, 2), 0.02 + 0.25 * Math.Pow((k + r.U()) / n, 1.3));
            m.Add(Creak(r, 0.2, Lin(60, 140, 0.2), new[] { 1800.0, 3200, 5200 }, 10).Env(Bell(0.2, 0.4)), -12, 0.03);
            return m.Out();
        }, desc: "ice cracks and splits");
        Add("sand_burst", 2, -5, v =>
        {
            var r = v.R; double d = 0.7;
            return Mx().Add(White(r, d).SvfBP(PtsE(0, 1500, 0.08, 5000, d, 2500), 0.8).Env(AD(0.008, 0.6)), 0)
                .Add(Crackle(r, d, Exp(3000, 100, d), 2500, 9000, 0.0008, 0.003, 2), -6).Add(Thump(110, 55, 0.04, 0.18), -5)
                .Add(Pink(r, 0.4).BP(500, 0.8).Env(AD(0.01, 0.35)), -10).Out();
        }, desc: "burst of sand");

        // ---------------- New monsters ----------------
        Cat("monsters");
        Add("construct_step", 3, -4, v =>
        {
            var r = v.R;
            return Mx().Add(Thump(70, 38, 0.05, 0.25), -4).Add(Metal(r, r.R(220, 320), 0.35, 9), 0).Add(Click(r, 2500, 0.8, 0.01), -4)
                .Add(Creak(r, 0.12, Lin(250, 400, 0.12), new[] { 2200.0, 3600 }, 9).Env(Bell(0.12, 0.4)), -15, 0.03)
                .Add(Crackle(r, 0.25, Exp(300, 20, 0.25), 400, 3000, 0.003, 0.02, 2), -14, 0.02).Out();
        }, desc: "brass construct: heavy metal footfall");
        Add("construct_attack", 2, -3, v =>
        {
            var r = v.R; var m = Mx(); double x = v.Idx == 0 ? 1 : 0.88;
            m.Add(Saw(0.38, Exp(60 * x, 140 * x, 0.38)).Bank((700 * x, 5, 1), (1500 * x, 6, 0.6), (2800, 5, 0.3)).Env(PtsS(0, 0, 0.3, 1, 0.38, 0)), -6);
            for (int k = 0; k < 5; k++) m.Add(Mx().Add(Click(r, 2600, 4, 0.005)).Add(Metal(r, r.R(3000, 3600), 0.03, 3), -4).Out(), -9, 0.04 + k * 0.065);
            m.Add(Whoosh(r, 0.3, PtsE(0, 250, 0.16, 1200, 0.3, 450), 1.3, Bell(0.3, 0.5)), -5, 0.36).Add(MetalHit(r, r.R(360, 440) * x, 0.4), 0, 0.52).Add(Thump(90, 45, 0.04, 0.18), -3, 0.52);
            return m.Out();
        }, pv: 0.6, desc: "construct: gear wind-up, swing, clank");
        Add("construct_death", 1, -1, v =>
        {
            var r = v.R; var m = Mx(); int n = 13;
            for (int k = 0; k < n; k++) { double t = 1.5 * Math.Pow((k + r.U() * 0.6) / n, 1.2); m.Add(MetalHit(r, r.X(150, 900), r.R(0.2, 0.8)), r.R(-9, -2), t); }
            m.Add(Creak(r, 0.9, PtsE(0, 30, 0.9, 70), new[] { 260.0, 540, 1150, 1900 }, 16).Env(Bell(0.9, 0.5)), -6, 0.05)
             .Add(White(r, 1.3).HP(4000).Env(AD(0.05, 1.2)), -13, 0.3).Add(MetalHit(r, 130, 1.0), 0, 1.55).Add(Thump(75, 30, 0.1, 0.5), -1, 1.55)
             .Add(Shards(r, 0.7, 20, 1500, 6000, 0.03, 0.1, RPlate.Take(3).ToArray()), -10, 1.6);
            return m.Out().Reverb(0.9, -11, 0.9, 4500);
        }, desc: "construct collapses into scrap");
        Add("construct_idle", 3, -9, v =>
        {
            var r = v.R; double d = r.R(1.1, 1.5); var m = Mx(); int ticks = r.I(5, 8); double gap = d / (ticks + 1);
            m.Add(Saw(d, Wander(r, 52, 60, 1.5, d)).Bank((r.R(260, 320), 6, 1), (r.R(620, 720), 7, 0.6), (1450, 6, 0.3)).Env(Bell(d, 0.5, 0.6)), -8);
            for (int k = 0; k < ticks; k++) m.Add(Mx().Add(Click(r, r.R(2400, 3000), 4, 0.005)).Add(Metal(r, r.R(2800, 3600), 0.04, 3), -5).Out(), r.R(-10, -5), gap * (k + 0.5) * r.R(0.95, 1.05));
            m.Add(White(r, 0.35).SvfBP(PtsE(0, 5000, 0.35, 2500), 1.5).Env(AD(0.03, 0.3)), -12, d * r.R(0.5, 0.65));
            m.Add(Creak(r, 0.3, Lin(120, 200, 0.3), new[] { 1500.0, 2600 }, 10).Env(Bell(0.3, 0.4)), -13, d * r.R(0.1, 0.3));
            return m.Out().Reverb(0.4, -14, 0.5, 5000);
        }, pv: 0.8, desc: "brass construct idle: ticking gears, low hum, a hiss of pressure");
        // the generic boss voice set the game addresses as boss_idle / boss_attack / boss_death
        Add("boss_idle", 3, -6, v =>
        {
            var r = v.R; double d = r.R(1.9, 2.4); var w = Wander(r, 44, 58, 1.6, d); Fn env = PtsS(0, 0, 0.35, 1, d * 0.55, 0.85, d, 0);
            var vo = Voice(r, d, t => w(t) * (1 - 0.1 * t / d), K((0, VU), (d * 0.45, VO), (d, VU)), 0.6, 0.35, 0.05, 0.3, 0.65, r.R(16, 24), 0.6).Drive(4).LP(2600).Env(env);
            var breath = Pink(r, d).SvfBP(PtsE(0, 300, d * 0.5, 520, d, 260), 0.9).Env(Bell(d, 0.45, 0.8));
            return Mx().Add(vo, 0).Add(Sine(d, 31).Env(env), -5).Add(breath, -9).Add(Crackle(r, d, 14, 1200, 5000), -22).Out().Reverb(1.3, -11, 1.1, 3000);
        }, pv: 0.6, desc: "boss idle: slow heavy breath-growl with sub rumble");
        Add("boss_attack", 3, -2, v =>
        {
            var r = v.R; double d = r.R(0.85, 1.05), x = 1 + 0.08 * (v.Idx - 1);
            var ro = Roar(r, d, PtsE(0, 85 * x, 0.12, 135 * x, d * 0.55, 120 * x, d, 70 * x), 0.7, 8, r.R(28, 38), 44, PtsS(0, 0, 0.04, 1, d * 0.6, 0.9, d, 0));
            var sw = Whoosh(r, 0.45, PtsE(0, 180, 0.22, 900, 0.45, 300), 1.2, Bell(0.45, 0.5));
            return Mx().Add(ro, 0).Add(sw, -7, 0.18).Add(Thump(80, 40, 0.06, 0.25), -8, 0.42).Out().Reverb(1.0, -11, 1.0, 3500);
        }, pv: 0.5, desc: "boss attack: short snarling roar with a heavy swing");
        Add("hag_idle", 3, -8, v =>
        {
            var r = v.R; int syl = r.I(2, 4); double sd = r.R(0.17, 0.22), d = syl * sd + 0.15; var w = Wander(r, 190, 260, 6, d);
            var amp = new List<double> { 0, 0 }; for (int k = 0; k < syl; k++) { amp.Add(k * sd + 0.04); amp.Add(r.R(0.7, 1)); amp.Add(k * sd + sd * 0.8); amp.Add(0.2); }
            amp.Add(d); amp.Add(0);
            var vo = Voice(r, d, t => w(t), K((0, VE), (d * 0.5, VAE), (d, VE)), 1.15, 0.3, 0.06, 0.3, 0.5, 70).Drive(2).Env(PtsS(amp.ToArray()));
            return vo.LP(6500).Reverb(0.4, -15, 0.4);
        }, pv: 1.0, desc: "mire hag: creaky cackle");
        Add("hag_attack", 2, -4, v =>
        {
            var r = v.R; double d = 0.5, x = v.Idx == 0 ? 1 : 1.1; Fn env = PtsS(0, 0, 0.02, 1, 0.35, 0.9, d, 0);
            var vo = Voice(r, d, PtsE(0, 300 * x, 0.1, 620 * x, 0.45, 480 * x), K((0, VAE), (0.2, VE), (d, VI)), 1.15, 0.35, 0.06, 0.3, 0.45, 80).Drive(3).Env(env);
            return Mx().Add(vo, 0).Add(White(r, d).SvfBP(PtsE(0, 2500, 0.2, 4500, d, 3500), 4).Env(env), -8).Out();
        }, pv: 0.6, desc: "mire hag: shriek");
        Add("hag_death", 2, -4, v =>
        {
            var r = v.R; double d = 1.1, x = v.Idx == 0 ? 1 : 0.92;
            var vo = Voice(r, d, PtsE(0, 520 * x, 0.15, 560 * x, d, 150 * x), K((0, VE), (0.5, VAE), (d, VUH)), 1.15, 0.4, 0.07, 0.35, 0.5, 60).Drive(2.5)
                .Env(t => PtsS(0, 0, 0.02, 1, 0.5, 0.8, d, 0)(t) * (1 - 0.5 * (t / d) * (0.5 + 0.5 * Math.Sin(TAU * 12 * t))));
            return Mx().Add(vo, 0).Add(Squelch(r, 0.3, 300, 1500, 30, 5).Env(Bell(0.3, 0.4)), -10, 0.7).Out().Reverb(0.5, -14, 0.5);
        }, pv: 0.6, desc: "mire hag: breaking wail");
        float[] ChoirCluster(Rng r, double d, double[] f, Fn glide, Fn env, Vowel a, Vowel b)
        {
            var m = Mx();
            foreach (var q in f) { double dt = r.R(0.995, 1.005), vr = r.R(4.5, 6); m.Add(Voice(r, d, t => q * dt * glide(t) * (1 + 0.008 * Math.Sin(TAU * vr * t)), K((0, a), (d, b)), 1.12, 0.25, 0.008, 0.06), r.R(-3, 0)); }
            return m.Out().Env(env).Chorus(17, 7, 0.35, 0.45);
        }
        Add("choir_idle", 3, -9, v =>
        {
            var r = v.R; double d = 2.0; double[][] cl = { new[] { 220.0, 233.08, 311.13 }, new[] { 196.0, 277.18, 293.66 }, new[] { 246.94, 261.63, 369.99 } };
            return ChoirCluster(r, d, cl[v.Idx % 3], C(1), PtsS(0, 0, 0.6, 1, 1.3, 0.85, d, 0), VU, VO).Reverb(1.6, -6, 1.0, 4500);
        }, pv: 0, desc: "hollow choir: eerie sustained cluster");
        Add("choir_attack", 2, -4, v =>
        {
            var r = v.R; double d = 0.9; var f = v.Idx == 0 ? new[] { 293.66, 311.13, 440.0 } : new[] { 329.63, 349.23, 466.16 };
            var m = ChoirCluster(r, d, f, Exp(1, 1.26, d), PtsE(0, 0.05, d * 0.9, 1, d, 0.02), VO, VA);
            return Mx().Add(m, 0).Add(White(r, d).SvfBP(PtsE(0, 1500, d, 4500), 2.5).Env(PtsE(0, 0.03, d * 0.9, 1, d, 0.02)), -9).Out().Fade(0, 0.01).Reverb(1.0, -9, 0.9, 5000);
        }, pv: 0, desc: "hollow choir: rising shrieked cluster");
        Add("choir_death", 2, -5, v =>
        {
            var r = v.R; double d = 2.0; var f = v.Idx == 0 ? new[] { 293.66, 311.13, 440.0 } : new[] { 261.63, 369.99, 392.0 }; var m = Mx();
            for (int i = 0; i < f.Length; i++)
            {
                double fall = r.R(0.35, 0.6), tr = r.R(7, 11), q = f[i];
                m.Add(Voice(r, d, t => q * Math.Pow(fall, t / d), K((0, VA), (d, VU)), 1.12, 0.5, 0.02, 0.15).Env(t => (1 - 0.5 * (t / d) * (0.5 + 0.5 * Math.Sin(TAU * tr * t)))), -i);
            }
            return m.Out().Env(PtsS(0, 0, 0.05, 1, 0.9, 0.7, d, 0)).Chorus(17, 7, 0.35, 0.45).Reverb(1.8, -5, 1.0, 4000);
        }, pv: 0, desc: "hollow choir: cluster collapses downward");
        Add("revenant_idle", 3, -8, v =>
        {
            var r = v.R; double d = 1.1; var w = Wander(r, 70, 90, 2, d); Fn env = PtsS(0, 0, 0.15, 1, d * 0.6, 0.8, d, 0);
            var vo = Voice(r, d, t => w(t), K((0, VO), (d, VU)), 0.9, 0.5, 0.04, 0.25, 0.4, 28, 0.3).Comb(0.006, 0.6, 3500).Env(env);
            return Mx().Add(vo, 0).Add(Shards(r, 0.6, 4, 1500, 3500, 0.04, 0.1, RPlate.Take(3).ToArray()), -12, 0.2)
                .Add(Creak(r, 0.25, Lin(90, 150, 0.25), new[] { 900.0, 1700 }, 10).Env(Bell(0.25, 0.5)), -14, r.R(0.3, 0.7)).Out().Reverb(0.4, -14, 0.5);
        }, pv: 1.0, desc: "revenant: hollow armoured breath");
        Add("revenant_attack", 2, -4, v =>
        {
            var r = v.R; double d = 0.6, x = v.Idx == 0 ? 1 : 0.9; Fn env = PtsS(0, 0, 0.03, 1, 0.35, 0.85, d, 0);
            var vo = Voice(r, d, PtsE(0, 95 * x, 0.1, 140 * x, d, 85 * x), K((0, VO), (0.15, VA), (d, VO)), 0.9, 0.4, 0.05, 0.3, 0.5, 36, 0.4).Drive(3.5).Comb(0.005, 0.6, 4000).Env(env);
            return Mx().Add(vo, 0).Add(White(r, 0.3).SvfBP(PtsE(0, 3000, 0.3, 6000), 4).Env(Bell(0.3, 0.5)), -11, 0.1).Out();
        }, pv: 0.6, desc: "revenant: hollow roar with steel scrape");
        Add("revenant_death", 2, -3, v =>
        {
            var r = v.R; double d = 1.0; var m = Mx();
            m.Add(Voice(r, d, PtsE(0, 130, 0.1, 120, d, 55), K((0, VA), (0.5, VO), (d, VU)), 0.9, 0.5, 0.05, 0.3, 0.5, 26, 0.4).Drive(2.5).Comb(0.006, 0.6, 3500).Env(PtsS(0, 0, 0.03, 1, 0.5, 0.75, d, 0)), -2);
            for (int k = 0; k < 6; k++) m.Add(MetalHit(r, r.X(250, 900), r.R(0.15, 0.4)), r.R(-8, -1), 0.55 + 0.6 * Math.Pow((k + r.U() * 0.5) / 6, 1.2));
            return m.Add(Thump(90, 45, 0.05, 0.2), -4, 0.6).Out().Reverb(0.6, -13, 0.6);
        }, pv: 0.6, desc: "revenant: groan and armour collapse");
        Add("rotspawn_idle", 2, -10, v =>
        {
            var r = v.R; double d = 0.6; var w = Wander(r, 300, 400, 8, d);
            var chit = Crackle(r, d, t => 60 * (0.5 + 0.5 * Math.Sin(TAU * 7 * t)), 2500, 6000, 0.002, 0.006, 1.2);
            var vo = Voice(r, d, t => w(t), K((0, VI), (d, VE)), 1.4, 0.4, 0.09, 0.4).Env(Bell(d, 0.4));
            return Mx().Add(Squelch(r, d, 400, 3000, 45, 5).Env(Bell(d, 0.5)), 0).Add(chit, -5).Add(vo, -8).Out();
        }, pv: 1.5, desc: "rotspawn: wet chitter");
        Add("rotspawn_attack", 2, -6, v =>
        {
            var r = v.R; double d = 0.3, x = v.Idx == 0 ? 1 : 1.15; Fn env = PtsS(0, 0, 0.015, 1, 0.2, 0.85, d, 0);
            var vo = Voice(r, d, PtsE(0, 350 * x, 0.1, 700 * x, d, 500 * x), K((0, VE), (d, VI)), 1.4, 0.4, 0.08, 0.35).Drive(2.5).Env(env);
            return Mx().Add(vo, 0).Add(Squelch(r, d, 800, 5000, 60, 4).Env(env), -5).Out();
        }, pv: 1.0, desc: "rotspawn: wet squeal");
        Add("rotspawn_death", 1, -5, v =>
        {
            var r = v.R;
            var vo = Voice(r, 0.4, PtsE(0, 650, 0.4, 220), K((0, VI), (0.4, VUH)), 1.4, 0.4, 0.09, 0.4).Drive(2).Env(AD(0.01, 0.38));
            return Mx().Add(Bubble(r, 420, 0.05, 1.6), -3).Add(Squelch(r, 0.3, 300, 5000, 70, 4).Env(AD(0.003, 0.28)), 0, 0.03).Add(vo, -4).Add(Thump(140, 80, 0.02, 0.08), -8, 0.03).Out();
        }, desc: "rotspawn: pop and splat");
        Add("stalker_idle", 3, -9, v =>
        {
            var r = v.R; double d = 1.0; var m = Mx(); var w = Wander(r, 22, 30, 3, d);
            m.Add(Formant(Glottal(r, d, t => w(t), 0.04, 0.2, 0.25), K((0, VUH), (d, VO)), 0.9).Env(Bell(d, 0.45, 0.7)), 0);
            int n = r.I(3, 5); double t0 = r.R(0.2, 0.5);
            for (int k = 0; k < n; k++) m.Add(Clack(r, r.R(1500, 2500), 0.012), -6 - k, t0 + 0.07 * k * (1 - 0.08 * k));
            return m.Out().LP(6000);
        }, pv: 1.0, desc: "dune stalker: purring trill and throat clicks");
        Add("stalker_attack", 2, -4, v =>
        {
            var r = v.R; double d = 0.45, x = v.Idx == 0 ? 1 : 1.1; Fn env = PtsS(0, 0, 0.012, 1, 0.3, 0.85, d, 0);
            var hiss = Formant(White(r, d).HP(700), K((0, VE), (d, VA)), 1.0).Env(env);
            var vo = Voice(r, d, PtsE(0, 200 * x, 0.08, 380 * x, d, 240 * x), K((0, VE), (d, VA)), 1.0, 0.3, 0.06, 0.3, 0.5, 60).Drive(4).Env(env);
            return Mx().Add(vo, 0).Add(hiss, -4).Add(Whoosh(r, 0.15, PtsE(0, 2500, 0.08, 6000, 0.15, 3000), 4, Bell(0.15, 0.4), false), -9, 0.2).Out();
        }, pv: 0.6, desc: "dune stalker: hiss-snarl and swipe");
        Add("stalker_death", 2, -4, v =>
        {
            var r = v.R; double d = 0.9, x = v.Idx == 0 ? 1 : 0.9; var m = Mx();
            m.Add(Voice(r, d, PtsE(0, 420 * x, 0.1, 460 * x, d, 130 * x), K((0, VE), (0.4, VA), (d, VUH)), 1.0, 0.4, 0.07, 0.35, 0.5, 45).Drive(3.5).Env(PtsS(0, 0, 0.02, 1, 0.4, 0.75, d, 0)), 0);
            for (int k = 0; k < 6; k++) m.Add(Clack(r, r.R(1200, 2000), 0.015), -8 - k * 1.5, 0.45 + 0.06 * k * (1 + 0.25 * k));
            return m.Out();
        }, pv: 0.6, desc: "dune stalker: falling screech, slowing clicks");

        // ---------------- Systems ----------------
        Cat("ui");
        Add("beacon_light", 1, -5, v =>
        {
            var r = v.R; var st = new St(N(4.6)); double d = 4.2;
            st.Add(Pink(r, 0.7).SvfBP(PtsE(0, 200, 0.35, 1800, 0.7, 500), 0.9).Env(PtsS(0, 0, 0.25, 1, 0.7, 0)), -5)
              .Add(Mx().Add(Thump(80, 40, 0.1, 0.5)).Add(White(r, 0.5).SvfLP(PtsE(0, 5000, 0.5, 300), 0.8).Env(AD(0.004, 0.45)), -4).Out(), -3, 0.25);
            st.Add(FireBed(r, d, 45, 330, 15).Env(PtsS(0, 0, 0.5, 1, 2.5, 0.8, d, 0)), -9, 0.25, -0.3).Add(FireBed(r, d, 45, 330, 15).Env(PtsS(0, 0, 0.5, 1, 2.5, 0.8, d, 0)), -9, 0.25, 0.3);
            double[] ch = { 146.83, 220.0, 293.66, 369.99, 440.0 }; double[] pans = { 0, -0.4, 0.4, -0.2, 0.2 };
            for (int i = 0; i < ch.Length; i++)
            {
                double t0 = 0.3 + i * 0.3, dd = d - t0;
                st.Add(Tones(r, dd, Harm(ch[i], 6, 1.3), 0.4).Env(PtsS(0, 0, 0.5, 1, dd * 0.55, 0.85, dd, 0)), -10 - i * 0.5, t0, pans[i]);
                if (i < 3) st.Add(Voice(r, dd, C(ch[i] * r.R(0.998, 1.002)), K((0, VO), (dd * 0.4, VA), (dd, VO)), 1, 0.06, 0.008, 0.05).Env(PtsS(0, 0, 0.7, 1, dd * 0.6, 0.85, dd, 0)), -14, t0, -pans[i]);
            }
            st.Add(BellTone(r, 587.33, 2.6), -9, 1.6).Add(Crackle(r, 3.0, t => 50 * Bell(3.0, 0.3)(t), 4000, 11000, 0.01, 0.05, 1.3), -22, 0.6, 0.2);
            return st.ReverbSt(2.4, -8, 1.0, 5500);
        }, stereo: true, desc: "beacon relit: great fire ignites under a warm rising chord");
        Add("event_start", 1, -4, v =>
        {
            var r = v.R; var st = new St(N(1.7));
            float[] Dr(double f) => Mx().Add(Thump(f, f * 0.55, 0.04, 0.3)).Add(Body(r, f * 2, 1, 0.12), -4).Add(Click(r, 1800, 1, 0.01), -10).Out();
            st.Add(Dr(95), -2, 0).Add(Dr(85), 0, 0.28);
            var sw = Mx().Add(Saw(1.2, 110)).Add(Saw(1.2, 110.6)).Add(Saw(1.2, 164.81), -2).Add(Saw(1.2, 165.5), -2).Add(Saw(1.2, 55), -3).Out()
                .SvfLP(PtsE(0, 200, 0.6, 1500, 1.2, 500), 1).Env(PtsS(0, 0, 0.45, 1, 0.8, 0.8, 1.2, 0));
            st.Add(sw, -5, 0.3).Add(BellTone(r, 329.63, 1.0, RBell, 0.7), -9, 0.28, 0.3);
            return st.ReverbSt(1.3, -9, 1.0, 4500);
        }, stereo: true, desc: "world event begins: two drums and a dark rising swell");
        Add("event_complete", 1, -5, v =>
        {
            var r = v.R; var st = new St(N(2.2));
            st.Add(Mx().Add(Thump(100, 55, 0.04, 0.3)).Add(Body(r, 180, 1, 0.14), -4).Out(), -3);
            double[] f = { 196.0, 293.66, 392.0, 493.88 };
            for (int i = 0; i < f.Length; i++) { st.Add(Pluck(r, f[i], 1.4), -4 + i * 0.7, i * 0.15, -0.4 + 0.27 * i); st.Add(BellTone(r, f[i] * 2, 1.1), -13 + i, i * 0.15, 0.4 - 0.27 * i); }
            st.Add(Tones(r, 1.8, new[] { 98.0, 146.83, 196.0, 246.94 }.SelectMany(q => Harm(q, 4, 1.5)), 0.3).Env(PtsS(0, 0, 0.5, 1, 1.0, 0.8, 1.8, 0)), -11, 0.3);
            return st.ReverbSt(1.6, -8, 1.0);
        }, stereo: true, desc: "world event complete: rising four-note figure in G");
        Add("world_tier", 1, -4, v =>
        {
            var r = v.R; var st = new St(N(3.4)); double t0 = 0.6;
            st.Add(White(r, t0).SvfBP(PtsE(0, 300, t0, 3000), 2).Env(t => Math.Pow(t / t0, 2.5)).Fade(0, 0.005), -10)
              .Add(Mx().Add(Thump(85, 28, 0.15, 0.8)).Add(Body(r, 120, 0.8, 0.3), -3).Out(), -1, t0);
            double[] ch = { 73.42, 77.78, 110.0, 146.83 }; double[] to = { 73.42, 73.42, 110.0, 146.83 }; double[] pans = { 0, 0.3, -0.4, 0.4 };
            for (int i = 0; i < 4; i++) { int k = i; st.Add(Voice(r, 2.6, PtsE(0, ch[k], 0.9, ch[k], 1.5, to[k], 2.6, to[k]), K((0, VU), (1.2, VO), (2.6, VA)), 1, 0.08, 0.01, 0.06).Env(PtsS(0, 0, 0.4, 1, 1.7, 0.85, 2.6, 0)), -9, t0, pans[i]); }
            st.Add(BellTone(r, 146.83, 2.8, RBell, 0.72), -5, t0).Add(Crackle(r, 2.4, t => 40 * Bell(2.4, 0.3)(t), 4000, 10000, 0.01, 0.05, 1.3), -24, t0 + 0.2, -0.2);
            return st.ReverbSt(2.6, -7, 1.1, 4500);
        }, stereo: true, desc: "world tier raised: swell, deep hit, cluster resolving to a fifth, great bell");
        Add("glyph_socket", 1, -9, v =>
        {
            var r = v.R;
            return Mx().Add(Clack(r, 900, 0.03), 0).Add(Metal(r, 2600, 0.4, 4, RGlass, 0.5), -5, 0.012)
                .Add(Tones(r, 0.5, new[] { (220.0, 1.0), (330.0, 0.6), (440.0, 0.3) }, 0.3).Env(Bell(0.5, 0.25)), -9, 0.01).Out();
        }, desc: "glyph set in stone: clack, hum and glassy ping");
    }
}
