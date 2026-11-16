// Ambience beds: stereo seamless loops (45-60 s) built from cross-faded stationary beds plus wrapped one-shot events with reverb.
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public sealed class AmbDef { public string Name, Desc; public double Seconds; public Func<St> Render; }

public sealed class AmbMix
{
    public readonly int Len; public readonly double L; public readonly Rng R; public readonly St Beds, Ev; readonly string name;
    public AmbMix(string name, double seconds) { this.name = name; L = seconds; Len = N(seconds); R = new Rng("amb/" + name); Beds = new St(Len); Ev = new St(Len); }
    /// <summary>Stationary bed, rendered twice (decorrelated L/R) and loop-cross-faded. width 0 = mono, 1 = fully decorrelated.</summary>
    public void Bed(Func<Rng, double, float[]> gen, double db, double width = 1, double pan = 0, double xf = 4)
    {
        int nx = Len + N(xf); var a = gen(R.Fork(), L + xf).Len(nx).LoopXfade(Len); var b = gen(R.Fork(), L + xf).Len(nx).LoopXfade(Len);
        double pk = Math.Max(a.Peak(), b.Peak()); if (pk < 1e-9) return; float g = (float)(Db(db) / pk), w = (float)width;
        double ang = (pan + 1) * Math.PI / 4; float gl = (float)(Math.Cos(ang) * 1.4142), gr = (float)(Math.Sin(ang) * 1.4142);
        for (int i = 0; i < Len; i++) { float m = 0.5f * (a[i] + b[i]); Beds.L[i] += g * gl * (m + w * (a[i] - m)); Beds.R[i] += g * gr * (m + w * (b[i] - m)); }
    }
    /// <summary>One-shot event (layer normalised to unit peak, db = its peak level relative to a 0 dB bed).</summary>
    public void Event(float[] mono, double db, double at, double pan) => Ev.Add(mono, db, ((at % L) + L) % L, pan);
    public double T(double lo, double hi) => R.R(lo, hi);
    public St Finish(double rt60, double wetDb, double dampHz = 4000, double size = 1.0, double bedWetDb = -100)
    {
        var wet = Ev.ReverbSt(rt60, wetDb, size, dampHz, 0.03, rt60 * 1.1).LoopWrap(Len);
        var o = new St(Len); for (int i = 0; i < Len; i++) { o.L[i] = Beds.L[i] + wet.L[i]; o.R[i] = Beds.R[i] + wet.R[i]; }
        float[] H(float[] x) { var b = Biquad.HP(25); foreach (var v in x) b.Tick(v); var y = new float[x.Length]; for (int i = 0; i < x.Length; i++) y[i] = (float)b.Tick(x[i]); return y; }
        o = new St(H(o.L), H(o.R)); double red = 0;
        for (int it = 0; it < 3; it++) { Master.ToLufs(o, -24, true); red = Math.Max(red, Master.Limit(o, -3, true)); }
        Program.Log($"    master {name}: {Meter.Lufs(o, true):F2} LUFS, peak {ToDb(o.Peak()):F2} dBFS, max limiting {red:F1} dB");
        return RotateToQuietPoint(o);
    }

    /// <summary>An ambience loop may start anywhere, so start it where it is momentarily quietest. A lossy codec cannot know that the
    /// end of the file continues into its start; the less signal there is at that instant, the smaller the error it can leave on the seam.</summary>
    static St RotateToQuietPoint(St s)
    {
        int n = s.Len, w = 512; var cs = new double[2 * n + 1];
        for (int i = 0; i < 2 * n; i++) { int j = i % n; cs[i + 1] = cs[i] + (double)s.L[j] * s.L[j] + (double)s.R[j] * s.R[j]; }
        int best = 0; double be = double.MaxValue;
        for (int k = 0; k < n; k += 8) { double e = cs[k + w] - cs[k]; if (e < be) { be = e; best = k; } }
        int cut = (best + w / 2) % n; var o = new St(n);
        for (int i = 0; i < n; i++) { int j = (i + cut) % n; o.L[i] = s.L[j]; o.R[i] = s.R[j]; }
        return o;
    }
}

public static class Amb
{
    public static readonly List<AmbDef> All = new();
    static bool built;
    static void Add(string name, double sec, string desc, Func<AmbMix, St> f) => All.Add(new AmbDef { Name = name, Seconds = sec, Desc = desc, Render = () => f(new AmbMix(name, sec)) });
    static (double, Vowel)[] K(params (double, Vowel)[] k) => k;

    static float[] Drip(Rng r)
    {
        double f = r.X(1100, 2600);
        return Mx().Add(Sine(0.07, Exp(f, f * 1.35, 0.03)).Env(AD(0.001, 0.055)), 0).Add(Click(r, f * 2.5, 2, 0.002), -10)
            .Add(Sine(0.05, Exp(f * 1.5, f * 1.9, 0.02)).Env(AD(0.001, 0.035)), -9, r.R(0.06, 0.1)).Out();
    }
    static float[] Hammer(Rng r, int hits, double gap)
    {
        var m = Mx(); double t = 0; double f = r.R(1900, 2300); double[] ra = { 1, 1.19, 1.71, 2.28, 2.74, 3.76 };
        for (int k = 0; k < hits; k++)
        {
            m.Add(Mx().Add(Sfx.Metal(r, f * r.R(0.99, 1.01), r.R(0.5, 0.8), 6, ra, 0.5, 0.8)).Add(Click(r, 3500, 0.8, 0.004), -4).Add(Thump(180, 110, 0.01, 0.04), -8).Out(), r.R(-4, 0), t);
            t += gap * (k == hits - 2 ? 0.5 : 1) * r.R(0.94, 1.06);
        }
        return m.Out().LP(2800);
    }
    static float[] Howl(Rng r, double d)
    {
        double x = r.R(0.85, 1.15);
        return Sfx.Voice(r, d, PtsE(0, 250 * x, d * 0.2, 340 * x, d * 0.7, 310 * x, d, 215 * x), K((0, Sfx.VU), (d * 0.4, Sfx.VO), (d, Sfx.VU)), 0.9, 0.25, 0.01, 0.08)
            .Env(PtsS(0, 0, d * 0.25, 1, d * 0.7, 0.8, d, 0)).LP(1300);
    }
    static float[] Caws(Rng r)
    {
        var m = Mx(); int n = r.I(2, 3); double f = r.R(360, 430);
        for (int k = 0; k < n; k++)
            m.Add(Sfx.Voice(r, 0.2, Exp(f, f * 0.84, 0.2), K((0, Sfx.VAE), (0.2, Sfx.VA)), 1.25, 0.5, 0.12, 0.5, 0.6, 90).Drive(2.5).BP(2200, 0.8).Env(AD(0.012, 0.19)), -k * 1.5, k * r.R(0.3, 0.38));
        return m.Out().LP(3500);
    }
    static float[] Moan(Rng r, double d)
    {
        double f = r.R(92, 118);
        return Sfx.Voice(r, d, PtsE(0, f * 1.12, d * 0.3, f * 1.18, d, f), K((0, Sfx.VU), (d * 0.5, Sfx.VO), (d, Sfx.VU)), 1, 0.5, 0.02, 0.15).Env(Bell(d, 0.4, 0.8)).LP(900);
    }
    static float[] Groan(Rng r, double d, double f0)
    {
        var gl = Wander(r, f0 * 0.8, f0 * 1.25, 0.6, d, true);
        var s = Mx().Add(Saw(d, t => gl(t))).Add(Saw(d, t => gl(t) * 1.502), -5).Out().SvfLP(PtsE(0, 120, d * 0.5, 320, d, 140), 4);
        return Mx().Add(s.Env(Bell(d, 0.45, 0.8)), 0).Add(Sine(d, t => gl(t) * 0.5).Env(Bell(d, 0.5)), -3).Out();
    }
    static float[] MetalGroan(Rng r, double d)
    {
        var cr = Sfx.Creak(r, d, Wander(r, 18, 40, 1.2, d), new[] { 180.0, 410, 770, 1310 }, 25).Env(Bell(d, 0.45, 0.7));
        var tone = Sfx.Tones(r, d, new[] { (r.R(170, 190), 1.0), (r.R(400, 420), 0.6), (r.R(760, 780), 0.3) }, 0.7).Env(Bell(d, 0.5));
        return Mx().Add(cr, 0).Add(tone, -8).Out().LP(2000);
    }
    static float[] Chirr(Rng r, double d, double carrier, double rate)
    {
        // cricket-like: sine carrier gated by a pulse train, in bursts
        var x = Sine(d, carrier); var burst = Wander(r, -0.6, 1, 1.6, d);
        for (int i = 0; i < x.Length; i++) { double t = i / (double)SR, g = Math.Pow(0.5 + 0.5 * Math.Sin(TAU * rate * t), 6), b = Clamp(burst(t) * 3, 0, 1); x[i] *= (float)(g * b); }
        return x;
    }
    static float[] Croak(Rng r)
    {
        var m = Mx(); int n = r.I(2, 4); double f = r.R(42, 68), d = r.R(0.14, 0.24);
        for (int k = 0; k < n; k++)
            m.Add(Glottal(r, d, C(f), 0.05, 0.2, 0.25).Bank((r.R(330, 380), 6, 1), (r.R(850, 950), 8, 0.6), (1500, 8, 0.3)).Env(Bell(d, 0.3, 0.8)), -k, k * (d + r.R(0.06, 0.12)));
        return m.Out();
    }

    public static void Build()
    {
        if (built) return; built = true;
        Add("amb_town", 54, "Lastlamp: soft wind, great brazier fire, distant smith's hammer, timber creaks", a =>
        {
            var r = a.R;
            a.Bed((g, d) => Sfx.Wind(g, d, 200, 700, 0.15), -8);
            a.Bed((g, d) => Sfx.FireBed(g, d, 30, 300, 13, 0, -6, -24), 0, 0.7, -0.15);
            foreach (var (t, hits) in new[] { (8.0, 4), (26.5, 5), (42.0, 3) }) a.Event(Hammer(r, hits, 0.62), -13, t, 0.55);
            for (int k = 0; k < 5; k++) { double d = r.R(0.5, 0.9); a.Event(Sfx.Creak(r, d, PtsE(0, r.R(40, 60), d, r.R(70, 95)), new[] { 350.0, 800, 1500 }).Env(Bell(d, 0.5, 0.7)).LP(3000), r.R(-19, -15), 4 + k * 10.3 + r.R(-2, 2), r.R(-0.7, 0.7)); }
            return a.Finish(1.6, -6, 4000, 0.8);
        });
        Add("amb_wastes", 60, "Ashen Wastes: cold wind gusts through dead trees, distant howls and caws, creaking wood", a =>
        {
            var r = a.R;
            a.Bed((g, d) => Sfx.Wind(g, d, 250, 1100, 0.2, 0.5), 0);
            a.Bed((g, d) => White(g, d).SvfBP(Wander(g, 2500, 6000, 0.3, d, true), 0.9).Env(Wander(g, 0.05, 1, 0.25, d)), -14);
            for (int k = 0; k < 5; k++) { double d = r.R(0.8, 1.6); a.Event(Sfx.Creak(r, d, PtsE(0, r.R(25, 40), d * 0.5, r.R(45, 60), d, r.R(25, 40)), new[] { 250.0, 600, 1200 }, 16).Env(Bell(d, 0.5, 0.7)).LP(2600), r.R(-15, -11), 3 + k * 11.7 + r.R(-2, 2), r.R(-0.8, 0.8)); }
            foreach (var t in new[] { 12.0, 33.0, 51.0 }) a.Event(Howl(r, r.R(2.2, 3.0)), r.R(-17, -14), t + r.R(-1, 1), r.R(-0.7, 0.7));
            foreach (var t in new[] { 21.0, 44.5 }) a.Event(Caws(r), -19, t, r.R(-0.8, 0.8));
            return a.Finish(2.5, -3, 3500, 1.1);
        });
        Add("amb_crypt", 58, "Slumbering Catacombs: deep room tone, slow water drips with long reverb, far stone scrapes, faint moans", a =>
        {
            var r = a.R;
            a.Bed((g, d) => Brown(g, d).LP(110), 0);
            a.Bed((g, d) => Sfx.Tones(new Rng(3), d, new[] { (47.0, 1.0), (47.4, 0.8), (71.0, 0.5), (71.3, 0.4) }, 0.6), -8, 0.3);
            a.Bed((g, d) => Pink(g, d).BP(900, 0.5), -26);
            for (int k = 0; k < 13; k++) a.Event(Drip(r), r.R(-12, -6), k * (58.0 / 13) + r.R(-1.5, 1.5), r.R(-0.8, 0.8));
            for (int k = 0; k < 3; k++)
            {
                double d = r.R(0.8, 1.5); var gr = Wander(r, 0.2, 1, 30, d);
                a.Event(Pink(r, d).SvfBP(Wander(r, 300, 900, 3, d, true), 1.2).Env(t => gr(t) * Bell(d, 0.5)(t)).LP(1800), r.R(-15, -12), 9 + k * 19 + r.R(-3, 3), r.R(-0.8, 0.8));
            }
            foreach (var t in new[] { 17.0, 46.0 }) a.Event(Moan(r, r.R(2.4, 3.2)), -17, t + r.R(-2, 2), r.R(-0.6, 0.6));
            return a.Finish(4.5, 2, 3500, 1.2);
        });
        Add("amb_abyss", 56, "Ember Abyss: deep rumble, lava bubbling and hiss, far tectonic groans, falling ember crackle", a =>
        {
            var r = a.R;
            a.Bed((g, d) => Brown(g, d).LP(80).Env(Wander(g, 0.5, 1, 0.15, d)), 0);
            a.Bed((g, d) => Brown(g, d).BP(250, 0.7).Env(Wander(g, 0.5, 1, 0.4, d)), -9);
            a.Bed((g, d) => White(g, d).HP(3500).Env(Wander(g, 0.05, 1, 0.3, d)), -24);
            a.Bed((g, d) => Crackle(g, d, 6, 3000, 9000, 0.004, 0.02, 2), -14);
            for (int k = 0; k < 46; k++) a.Event(Sfx.Bubble(r, r.X(70, 220), r.R(0.08, 0.2), r.R(1.5, 2.0)), r.R(-16, -8), r.R(0, 56), r.R(-0.9, 0.9));
            foreach (var t in new[] { 7.0, 25.0, 41.0 }) a.Event(Groan(r, r.R(3.5, 5), r.R(34, 46)), r.R(-9, -6), t + r.R(-2, 2), r.R(-0.5, 0.5));
            return a.Finish(3.5, -6, 2500, 1.2);
        });
        Add("amb_menu", 48, "Menu: low wind and a slow ember crackle", a =>
        {
            a.Bed((g, d) => Sfx.Wind(g, d, 150, 500, 0.1), 0);
            a.Bed((g, d) => Crackle(g, d, 5, 1500, 7000, 0.003, 0.015, 2.5), -9);
            a.Bed((g, d) => Crackle(g, d, 0.5, 400, 1400, 0.01, 0.03, 1.2), -12);
            a.Bed((g, d) => Brown(g, d).LP(260).Env(Wander(g, 0.5, 1, 6, d)), -12, 0.5);
            return a.Finish(2.0, -10);
        });
        Add("amb_frost", 56, "Snowbound pass: blizzard wind, ice creaks, distant avalanche rumbles", a =>
        {
            var r = a.R;
            a.Bed((g, d) => Sfx.Wind(g, d, 250, 1400, 0.3, 0.8, 1.0), 0);
            a.Bed((g, d) => White(g, d).SvfBP(Wander(g, 3000, 7000, 0.4, d, true), 0.8).Env(Wander(g, 0.1, 1, 0.35, d)), -10);
            for (int k = 0; k < 6; k++)
            {
                double d = r.R(0.3, 0.8);
                var ice = Mx().Add(Sfx.Creak(r, d, PtsE(0, r.R(80, 120), d, r.R(150, 220)), new[] { 1800.0, 3200, 5200 }, 18).Env(Bell(d, 0.4, 0.7)), 0).Add(Click(r, r.R(4000, 6500), 1, 0.005), -2, d * r.R(0.2, 0.8)).Out();
                a.Event(ice, r.R(-16, -12), 2 + k * 9.2 + r.R(-2, 2), r.R(-0.8, 0.8));
            }
            foreach (var t in new[] { 15.0, 40.0 })
            {
                double d = r.R(6, 8);
                a.Event(Mx().Add(Brown(r, d).LP(140).Env(Bell(d, 0.3, 0.9)), 0).Add(Crackle(r, d, t2 => 120 * Bell(d, 0.35)(t2), 200, 1200, 0.006, 0.04, 2).LP(1500), -10).Out(), -5, t, t < 20 ? -0.5 : 0.5);
            }
            return a.Finish(2.5, -6, 3000, 1.1);
        });
        Add("amb_mire", 58, "Rotting mire: insect-like chirrs, bubbling mud, dripping, croaks, low fog drone", a =>
        {
            var r = a.R;
            a.Bed((g, d) => Mx().Add(Brown(g, d).LP(150).Env(Wander(g, 0.5, 1, 0.2, d)), 0).Out(), -3);
            a.Bed((g, d) => Sfx.Tones(new Rng(5), d, new[] { (55.0, 1.0), (58.3, 0.8), (82.6, 0.4) }, 0.6), -8, 0.4);
            a.Bed((g, d) => White(g, d).BP(6500, 8).Env(t => (0.6 + 0.4 * Math.Sin(TAU * 90 * t))).Env(Wander(g, 0.1, 1, 0.2, d)), -22);
            a.Bed((g, d) => Chirr(g, d, 4300, 31), -17, 1, -0.6); a.Bed((g, d) => Chirr(g, d, 4900, 37), -19, 1, 0.5); a.Bed((g, d) => Chirr(g, d, 3700, 26), -21, 1, 0.1);
            for (int k = 0; k < 32; k++) a.Event(Sfx.Bubble(r, r.X(90, 300), r.R(0.1, 0.22), r.R(1.4, 1.8)), r.R(-14, -7), r.R(0, 58), r.R(-0.9, 0.9));
            for (int k = 0; k < 9; k++) a.Event(Drip(r).LP(5000), r.R(-16, -10), r.R(0, 58), r.R(-0.8, 0.8));
            for (int k = 0; k < 7; k++) a.Event(Croak(r), r.R(-13, -9), 3 + k * 8.1 + r.R(-2.5, 2.5), r.R(-0.8, 0.8));
            return a.Finish(1.8, -9, 3000, 0.9);
        });
        Add("amb_sands", 52, "Brass-city dunes: dry wind with sand hiss, distant metallic groans, loose debris", a =>
        {
            var r = a.R;
            a.Bed((g, d) => Sfx.Wind(g, d, 300, 1600, 0.18, 0.2), 0);
            a.Bed((g, d) => White(g, d).SvfBP(Wander(g, 4000, 8000, 0.3, d, true), 0.8).Env(t => 1).Env(Wander(g, 0.05, 1, 0.3, d)), -9);
            foreach (var t in new[] { 9.0, 27.0, 43.0 }) a.Event(MetalGroan(r, r.R(2.2, 3.5)), r.R(-13, -10), t + r.R(-2, 2), r.R(-0.7, 0.7));
            for (int k = 0; k < 6; k++)
            {
                double d = r.R(0.4, 0.9);
                a.Event(Crackle(r, d, Exp(60, 10, d), 800, 4000, 0.003, 0.01, 1.5), r.R(-17, -13), 2 + k * 8.5 + r.R(-2, 2), r.R(-0.9, 0.9));
            }
            return a.Finish(2.2, -6, 3500, 1.1);
        });
        Add("amb_sanctum", 60, "Cathedral-city: vast stone room tone, far bell tolls, candle flicker, faint breath-like choir", a =>
        {
            var r = a.R;
            a.Bed((g, d) => Brown(g, d).LP(140), 0);
            a.Bed((g, d) => Pink(g, d).BP(800, 0.5), -22);
            a.Bed((g, d) => Crackle(g, d, 3, 2000, 6000, 0.002, 0.006, 2), -24);
            a.Bed((g, d) => Brown(g, d).LP(400).HP(120).Env(Wander(g, 0, 1, 5, d)).Env(Wander(g, 0.2, 1, 0.5, d)), -22, 0.8);
            foreach (var (t, f) in new[] { (6.0, 196.0), (26.0, 196.0), (45.0, 146.83) }) a.Event(Sfx.BellTone(r, f, 5.0, RBell, 0.7).LP(1800), -9, t, r.R(-0.4, 0.4));
            foreach (var (t, f) in new[] { (14.0, 98.0), (35.0, 110.0), (52.0, 98.0) })
            {
                double d = r.R(5.5, 7.5); var m = Mx();
                foreach (var q in new[] { 1.0, 1.5, 2.0 }) m.Add(Sfx.Voice(r, d, C(f * q * r.R(0.997, 1.003)), K((0, Sfx.VU), (d * 0.5, Sfx.VO), (d, Sfx.VU)), 1, 0.9, 0.008, 0.06).Env(Bell(d, 0.5, 0.8)), q == 1 ? 0 : -4);
                a.Event(m.Out().LP(1600), -15, t, r.R(-0.5, 0.5));
            }
            return a.Finish(6.0, 3, 3000, 1.3);
        });
    }
}
