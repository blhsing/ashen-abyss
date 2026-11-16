// Combat SFX: swings, impacts, player voice, footsteps, body sounds.
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public static partial class Sfx
{
    // shared impact builders (also used by the class packs)
    static float[] FleshHit(Rng r, double weight = 1)
    {
        var trans = Click(r, r.R(2200, 3200), 1.2, 0.012);
        var slap = White(r, 0.05).BP(r.R(1000, 1400), 0.8).Env(AD(0.001, 0.03));
        var body = Body(r, r.R(300, 420) / weight, 1.5, 0.07 * weight);
        var sub = Thump(r.R(85, 100) / Math.Sqrt(weight), 48 / Math.Sqrt(weight), 0.05, 0.19 * weight);
        var wet = Squelch(r, 0.15, 500, 3500, 60, 4).Env(AD(0.004, 0.13));
        return Mx().Add(sub, -4).Add(body, 0).Add(trans, -2).Add(slap, -4).Add(wet, -7, 0.008).Out().LP(9000);
    }
    static float[] MetalHit(Rng r, double f0, double t60, double transDb = -2)
        => Mx().Add(Metal(r, f0, t60, 10, RPlate, 0.8, 0.85), 0)
               .Add(Metal(r, f0 * r.R(3.9, 4.6), t60 * 0.5, 4, RBar), -11)
               .Add(Click(r, 4200, 1, 0.006), transDb).Add(Body(r, 520, 2, 0.05), -7).Add(Thump(110, 60, 0.03, 0.12), -9).Out();

    static void Combat()
    {
        Cat("combat");
        Add("swing_light", 4, -7, v =>
        {
            var r = v.R; double d = r.R(0.20, 0.26);
            var air = Whoosh(r, d, PtsE(0, 500, d * 0.55, r.R(2200, 3000), d, 900), 1.6, Bell(d, 0.55, 1.5));
            var hiss = Whoosh(r, d, PtsE(0, 3000, d * 0.5, 6500, d, 4000), 3, Bell(d, 0.5, 2.5), false);
            var whistle = Whoosh(r, d, PtsE(0, 1800, d * 0.5, 3500, d, 2000), 12, Bell(d, 0.5, 2), false);
            var low = Whoosh(r, d, C(220), 1.2, Bell(d, 0.5, 1.2));
            return Mx().Add(air).Add(hiss, -12).Add(whistle, -15).Add(low, -14).Out();
        }, desc: "light weapon swing");
        Add("swing_heavy", 3, -5, v =>
        {
            var r = v.R; double d = r.R(0.38, 0.46);
            var air = Whoosh(r, d, PtsE(0, 250, d * 0.55, r.R(1100, 1500), d, 450), 1.3, Bell(d, 0.55, 1.3));
            var low = Whoosh(r, d, C(120), 1.0, Bell(d, 0.5, 1.1));
            var whump = Sine(d, Exp(90, 60, d)).Env(Bell(d, 0.5, 1.5));
            var whistle = Whoosh(r, d, PtsE(0, 900, d * 0.55, 2200, d, 1200), 10, Bell(d, 0.55, 2.2), false);
            return Mx().Add(air).Add(low, -6).Add(whump, -10).Add(whistle, -16).Out();
        }, desc: "greatsword swing");
        Add("hit_flesh", 5, -3, v => FleshHit(v.R), desc: "weapon into flesh");
        Add("hit_bone", 4, -3, v =>
        {
            var r = v.R; double f = r.R(700, 1000);
            return Mx().Add(Click(r, r.R(2600, 3800), 2, 0.008), 0).Add(Click(r, r.R(2000, 3000), 2, 0.008), -4, r.R(0.012, 0.022))
                .Add(Clack(r, f, r.R(0.04, 0.06)), -1).Add(Body(r, r.R(450, 650), 2.5, 0.04), -5).Add(Thump(140, 80, 0.03, 0.07), -8)
                .Add(Crackle(r, 0.12, Exp(250, 20, 0.12), 1500, 5000, 0.002, 0.006), -14, 0.01).Out();
        }, desc: "dry bone crack");
        Add("hit_metal", 4, -2, v => MetalHit(v.R, v.R.R(620, 900), v.R.R(0.5, 0.8)), desc: "weapon on armour");
        Add("hit_crit", 3, -1, v =>
        {
            var r = v.R;
            var m = Mx().Add(Thump(120, 38, 0.08, 0.30), 0).Add(Click(r, 3000, 0.8, 0.015), -1).Add(Click(r, 5000, 1, 0.005), -3)
                .Add(Body(r, 260, 1.2, 0.12), -3).Add(Squelch(r, 0.22, 400, 4000, 50, 4).Env(AD(0.004, 0.2)), -8, 0.006)
                .Add(Metal(r, r.R(2600, 3200), 0.6, 5, RBar, 0.5), -9).Out();
            return m.Reverb(0.5, -15, 0.5);
        }, pv: 1.0, desc: "critical hit: heavy layered impact with bright ring");
        Add("block", 2, -3, v =>
        {
            var r = v.R;
            return Mx().Add(Metal(r, r.R(380, 520), 0.22, 9), 0).Add(Clack(r, r.R(200, 260), 0.06), -3).Add(Click(r, 3200, 1, 0.006), -2)
                .Add(Thump(100, 65, 0.03, 0.1), -6).Out();
        }, desc: "blocked blow (damped metal + wood)");
        Add("stagger_break", 1, -1, v =>
        {
            var r = v.R;
            var m = Mx().Add(Thump(130, 34, 0.12, 0.45), 0).Add(Click(r, 2600, 0.7, 0.02), -1)
                .Add(Crackle(r, 0.6, Exp(900, 30, 0.6), 2500, 9000, 0.004, 0.03, 2), -8)
                .Add(Metal(r, 1500, 0.7, 6, RGlass), -10).Add(Whoosh(r, 0.5, PtsE(0, 3000, 0.5, 200), 2, AD(0.005, 0.45)), -9).Out();
            return m.Reverb(0.7, -12, 0.6);
        }, desc: "guard break");
        Add("player_hurt", 3, -5, v =>
        {
            var r = v.R; double d = r.R(0.20, 0.30), x = r.R(0.92, 1.08);
            var keys = v.Idx switch { 0 => new[] { (0.0, VUH), (d, VA) }, 1 => new[] { (0.0, VAE), (d, VUH) }, _ => new[] { (0.0, VM), (0.05, VUH), (d, VO) } };
            var vo = Voice(r, d, PtsE(0, 150 * x, 0.04, 168 * x, d, 112 * x), keys, 1, 0.15, 0.02, 0.15, 0.2, 55)
                .Env(PtsS(0, 0, 0.012, 1, d * 0.5, 0.8, d, 0));
            return Mx().Add(vo).Add(White(r, 0.05).HP(1500).Env(AD(0.002, 0.04)), -15).Out().LP(5500);
        }, pv: 1.0, desc: "short male-neutral pain grunt (synthesised)");
        Add("player_death", 1, -4, v =>
        {
            var r = v.R; double d = 1.3;
            var vo = Voice(r, d, PtsE(0, 170, 0.15, 185, d, 85), new[] { (0.0, VA), (0.6, VO), (d, VU) }, 1, 0.3, 0.03, 0.2, 0.3, 40)
                .Env(PtsS(0, 0, 0.03, 1, 0.5, 0.8, d, 0));
            var exhale = Formant(White(r, 0.6).HP(600), new[] { (0.0, VO), (0.6, VU) }).Env(Bell(0.6, 0.25));
            return Mx().Add(vo).Add(exhale, -12, 0.9).Out().LP(6000).Reverb(0.8, -16, 0.5);
        }, desc: "player death cry (synthesised)");
        Add("heartbeat_loop", 1, -6, v =>
        {
            var r = v.R; double T = 0.84; int L = N(2 * T); var m = Mx();
            for (int k = 0; k < 2; k++)
            {
                double t0 = 0.02 + k * T, a = k == 0 ? 0 : -0.8;
                m.Add(Mx().Add(Thump(62, 42, 0.04, 0.16)).Add(Body(r, 90, 1.5, 0.08), -6).Out(), a, t0);
                m.Add(Mx().Add(Thump(72, 48, 0.03, 0.12)).Add(Body(r, 110, 1.5, 0.06), -8).Out(), a - 3.5, t0 + 0.27);
            }
            return m.Out().LP(240).LoopWrap(L);
        }, loop: true, desc: "low-health heartbeat, 2 beats, loop");
        Add("footstep_stone", 6, -9, v =>
        {
            var r = v.R;
            return Mx().Add(Body(r, r.R(280, 480), 1.6, r.R(0.035, 0.055)), 0).Add(Click(r, r.R(1800, 3200), 1.5, 0.007), -3)
                .Add(Thump(r.R(95, 120), 70, 0.02, 0.06), -5).Add(Crackle(r, 0.07, Exp(600, 50, 0.07), 2500, 8000, 0.001, 0.004), -14, 0.004)
                .Add(Click(r, r.R(2000, 2800), 1.5, 0.005), -9, r.R(0.03, 0.05)).Out();
        }, desc: "boot on stone");
        Add("footstep_dirt", 6, -11, v =>
        {
            var r = v.R;
            return Mx().Add(Body(r, r.R(140, 260), 1.1, r.R(0.05, 0.08)), 0).Add(Thump(85, 60, 0.02, 0.07), -4)
                .Add(Pink(r, 0.09).LP(1200).Env(AD(0.006, 0.07)), -6)
                .Add(Crackle(r, 0.1, Exp(1500, 80, 0.1), 1800, 7000, 0.001, 0.005, 2).Env(Bell(0.1, 0.2)), -10).Out();
        }, desc: "boot on ash/dirt");
        Add("evade", 2, -9, v =>
        {
            var r = v.R; double d = 0.34;
            return Mx().Add(Whoosh(r, d, PtsE(0, 350, d * 0.5, 1400, d, 500), 1.1, Bell(d, 0.45, 1.2)), 0)
                .Add(Squelch(r, d, 800, 5000, 35, 1.5, 0.8).Env(Bell(d, 0.4)), -10).Add(Pink(r, 0.14).BP(600, 1).Env(AD(0.01, 0.12)), -8, 0.2).Out();
        }, desc: "dodge roll: cloth whoosh + scuff");
        Add("potion", 1, -7, v =>
        {
            var r = v.R; var m = Mx();
            m.Add(Sine(0.05, Exp(1100, 500, 0.03)).Env(AD(0.001, 0.04)), 0).Add(Click(r, 3000, 1, 0.004), -4);
            foreach (var (t, f) in new[] { (0.10, 250.0), (0.24, 290.0), (0.37, 230.0) }) m.Add(Bubble(r, f, 0.09, 1.9), -3, t);
            m.Add(Squelch(r, 0.35, 600, 2500, 25, 3).Env(Bell(0.35, 0.3)), -13, 0.08);
            m.Add(Tones(r, 0.9, Harm(330, 5, 1.4)).Env(PtsS(0, 0, 0.12, 1, 0.9, 0)), -9, 0.42);
            m.Add(Crackle(r, 0.7, 25, 4000, 9000, 0.01, 0.04, 1.5).Env(Bell(0.7, 0.3)), -22, 0.45);
            return m.Out();
        }, desc: "cork pop, gulps, warm ember glow");
        Add("level_up", 1, -5, v =>
        {
            var r = v.R; double d = 2.2; var st = new St(N(d));
            var bloom = Mx(); foreach (var f in new[] { 73.42, 110.0, 146.83 }) { bloom.Add(Saw(d, f * 0.996), -3).Add(Saw(d, f * 1.004), -3); }
            st.Add(bloom.Out().SvfLP(PtsE(0, 150, 0.8, 1800, d, 400), 1.1).Env(PtsS(0, 0, 0.45, 1, 1.2, 0.8, d, 0)), -6);
            double[] ch = { 146.83, 220.0, 293.66, 349.23 }; double[] pans = { -0.5, 0.4, -0.2, 0.6 };
            for (int i = 0; i < ch.Length; i++)
                st.Add(Voice(r, d, C(ch[i] * r.R(0.997, 1.003)), new[] { (0.0, VO), (1.2, VA), (d, VO) }, 1, 0.05, 0.008, 0.05)
                    .Env(PtsS(0, 0, 0.6, 1, 1.3, 0.85, d, 0)), -12, 0, pans[i]);
            st.Add(Mx().Add(Thump(90, 40, 0.08, 0.5)).Add(Body(r, 200, 1, 0.2), -4).Out(), -3);
            double[] arp = { 293.66, 440.0, 587.33, 698.46 };
            for (int i = 0; i < arp.Length; i++) st.Add(Pluck(r, arp[i], 1.2, 0.55), -9 - i * 0.5, 0.4 + i * 0.13, -0.4 + 0.27 * i);
            st.Add(Crackle(r, 1.6, 40, 4000, 10000, 0.01, 0.05, 1.5).Env(Bell(1.6, 0.4)), -24, 0.4, 0.3);
            return st.ReverbSt(1.5, -9, 1.0, 4500, 0.012, 0.75);
        }, stereo: true, max: 3.0, min: 2.0, desc: "triumphant-but-dark swell (2-3 s): drum, low bloom, choir, rising plucks");
        Add("body_fall", 3, -5, v =>
        {
            var r = v.R; double tb = r.R(0.14, 0.2);
            return Mx().Add(Thump(r.R(75, 90), 42, 0.05, 0.22), -3).Add(Body(r, r.R(160, 240), 1, 0.11), 0)
                .Add(Pink(r, 0.14).BP(1500, 0.7).Env(AD(0.01, 0.12)), -8).Add(Body(r, 200, 1, 0.06), -9, tb).Add(Thump(70, 50, 0.03, 0.1), -10, tb)
                .Add(Metal(r, r.R(1800, 2600), 0.1, 5), -19, 0.02).Out();
        }, desc: "body hits the ground");
        Add("ash_crumble", 4, -8, v =>
        {
            var r = v.R; double d = r.R(0.85, 1.15);
            var grains = Crackle(r, d, Exp(2500, 60, d), 1500, 9000, 0.0008, 0.004, 2.2).Env(AD(0.01, d));
            var sand = White(r, d).SvfBP(PtsE(0, 7000, d, 2200), 1.2).Env(PtsS(0, 0, 0.04, 1, d * 0.4, 0.5, d, 0));
            var sparks = Crackle(r, d, 14, 4000, 11000, 0.01, 0.05, 1.2).Env(Bell(d, 0.3));
            return Mx().Add(grains, 0).Add(sand, -8).Add(sparks, -12).Add(Thump(110, 55, 0.05, 0.14), -10).Add(Pink(r, 0.18).LP(400).Env(AD(0.005, 0.15)), -10).Out();
        }, desc: "enemy dissolving into ash and sparks (signature death)");
    }
}
