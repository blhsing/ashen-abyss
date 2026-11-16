// SFX framework: registry, variation context, shared layer builders (voices, rattles, shards, fire beds, bells).
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

/// <summary>Variation context handed to every generator.</summary>
public sealed class V
{
    public Rng R; public int Idx; public string Name;
    public V(string name, int idx) { Name = name; Idx = idx; R = new Rng("sfx/" + name, idx); }
}

public sealed class SfxDef
{
    public string Name, Cat, Desc; public int Vars; public bool Loop, Stereo; public double PeakDb, PitchVar, MinDur, MaxDur; public Func<V, object> Gen;
    public IEnumerable<string> Files() { for (int i = 1; i <= Vars; i++) yield return $"{Name}_{i}.wav"; }
}

public readonly record struct Vowel(double F1, double F2, double F3, double F4);

public static partial class Sfx
{
    public static readonly List<SfxDef> All = new();
    static string curCat = "combat";
    static void Cat(string c) => curCat = c;
    /// <param name="peak">peak target in dBFS for the loudest variation</param>
    /// <param name="pv">pitch spread (semitones, +/-) applied across variations by resampling</param>
    static void Add(string name, int vars, double peak, Func<V, object> gen, bool loop = false, bool stereo = false, double pv = 1.5, string desc = null, double min = 0, double max = 0)
        => All.Add(new SfxDef { Name = name, Cat = curCat, Vars = vars, PeakDb = peak, Gen = gen, Loop = loop, Stereo = stereo, PitchVar = vars > 1 ? pv : 0, Desc = desc, MinDur = min, MaxDur = max });

    static bool built;
    public static void Build()
    {
        if (built) return; built = true;
        Combat(); Skills(); Monsters(); Ui(); Ext();
    }

    // ---------- vowels (male reference; scale formants for creature size) ----------
    public static readonly Vowel VA = new(730, 1090, 2440, 3400), VE = new(530, 1840, 2480, 3400), VI = new(270, 2290, 3010, 3500),
        VO = new(570, 840, 2410, 3400), VU = new(300, 870, 2240, 3400), VER = new(490, 1350, 1690, 3300), VAE = new(660, 1720, 2410, 3400),
        VUH = new(640, 1190, 2390, 3400), VM = new(250, 1000, 2200, 3000);

    /// <summary>Moving formant filter: parallel band-passes whose centres glide through vowel keyframes.</summary>
    public static float[] Formant(float[] src, (double t, Vowel v)[] keys, double scale = 1, double q = 1, double dryDb = -20)
    {
        Fn Track(Func<Vowel, double> sel)
        {
            var tv = new double[keys.Length * 2];
            for (int i = 0; i < keys.Length; i++) { tv[2 * i] = keys[i].t; tv[2 * i + 1] = sel(keys[i].v) * scale; }
            return PtsS(tv);
        }
        var f1 = src.SvfBP(Track(v => v.F1), 7 * q); var f2 = src.SvfBP(Track(v => v.F2), 10 * q);
        var f3 = src.SvfBP(Track(v => v.F3), 12 * q); var f4 = src.SvfBP(Track(v => v.F4), 12 * q);
        var o = new float[src.Length]; float d = (float)Db(dryDb);
        for (int i = 0; i < o.Length; i++) o[i] = f1[i] + 0.63f * f2[i] + 0.35f * f3[i] + 0.2f * f4[i] + d * src[i];
        return o;
    }

    /// <summary>Creature / human voice: glottal source (jitter, shimmer, optional octave-down sub and growl AM) + breath noise through moving formants.</summary>
    public static float[] Voice(Rng r, double dur, Fn f0, (double t, Vowel v)[] keys, double scale = 1, double breath = 0.1, double jitter = 0.015,
        double shimmer = 0.08, double growl = 0, double growlHz = 40, double sub = 0, double tiltHz = 2500, double q = 1)
    {
        var src = Glottal(r, dur, f0, jitter, shimmer);
        if (sub > 0) { var s2 = Glottal(r, dur, t => f0(t) * 0.5, jitter * 1.5, shimmer); for (int i = 0; i < src.Length; i++) src[i] += (float)(sub * s2[i]); }
        if (growl > 0)
        {
            var gw = Wander(r, growlHz * 0.8, growlHz * 1.25, 9, dur); double ph = 0;
            for (int i = 0; i < src.Length; i++) { ph += gw(i / (double)SR) / SR; double m = 0.5 + 0.5 * Math.Sin(TAU * ph); m = m * m; src[i] *= (float)(1 - growl + growl * m * 1.6); }
        }
        var noise = White(r, dur).HP(900);
        var amp = Wander(r, 0.6, 1, 14, dur);
        for (int i = 0; i < src.Length; i++) src[i] = (float)(src[i] + breath * 0.5 * noise[i] * amp(i / (double)SR));
        return Formant(src.LP1(tiltHz), keys, scale, q);
    }

    /// <summary>Sustained sum of sines (freq, amp) with slow independent shimmer per partial.</summary>
    public static float[] Tones(Rng r, double dur, IEnumerable<(double f, double a)> parts, double shimmer = 0.3, double vibHz = 0, double vibDepth = 0)
    {
        int n = N(dur); var o = new float[n];
        foreach (var (f, a) in parts)
        {
            if (f >= SR * 0.45) continue;
            double ph = r.U() * TAU, sr1 = r.R(2.5, 7), sp = r.U() * TAU, vp = r.U() * TAU;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)SR, m = 1 - shimmer * (0.5 + 0.5 * Math.Sin(TAU * sr1 * t + sp));
                o[i] += (float)(a * m * Math.Sin(ph)); ph += TAU * f * (1 + vibDepth * Math.Sin(TAU * vibHz * t + vp)) / SR;
            }
        }
        return o;
    }
    public static IEnumerable<(double f, double a)> Harm(double f0, int n, double rolloff = 1.2, double gain = 1)
    { for (int k = 1; k <= n; k++) yield return (f0 * k, gain / Math.Pow(k, rolloff)); }

    /// <summary>Bell-like struck tone (near-harmonic stretched partials).</summary>
    public static float[] BellTone(Rng r, double f, double t60, double[] ratios = null, double bright = 0.55)
    {
        ratios ??= RTube; var m = new List<(double, double, double)>();
        for (int i = 0; i < ratios.Length; i++) m.Add((f * ratios[i] * (1 + r.R(-0.002, 0.002)), t60 / Math.Pow(ratios[i], 0.9), Math.Pow(bright, i)));
        // beating twin for the fundamental gives the slow bell shimmer
        m.Add((f * 1.0035, t60 * 0.9, 0.35));
        return Modes(t60 * 1.15, m, 0.002);
    }
    /// <summary>Plucked string-like tone (harmonic, higher partials die quicker).</summary>
    public static float[] Pluck(Rng r, double f, double t60, double bright = 0.6, int n = 10)
    {
        var m = new List<(double, double, double)>();
        for (int k = 1; k <= n; k++) m.Add((f * k * (1 + 0.0004 * k * k), t60 / Math.Pow(k, 0.8), Math.Pow(bright, k - 1) * (k % 3 == 0 ? 0.5 : 1)));
        return Mx().Add(Modes(t60 * 1.1, m, 0.001)).Add(Click(r, Math.Min(6000, f * 6), 1.2, 0.006), -16).Out();
    }

    /// <summary>Metal ring from a plate/bar partial set.</summary>
    public static float[] Metal(Rng r, double f0, double t60, int n = 10, double[] ratios = null, double slope = 0.7, double tilt = 0.82)
        => Modes(t60 * 1.2, PartialSet(r, f0, (ratios ?? RPlate).Take(n).ToArray(), t60, slope, tilt));

    /// <summary>Dry bone / wood clack.</summary>
    public static float[] Clack(Rng r, double f, double t60 = 0.04)
        => Mx().Add(Modes(t60 * 2 + 0.01, new[] { (f, t60, 1.0), (f * 1.48, t60 * 0.75, 0.6), (f * 2.13, t60 * 0.6, 0.4), (f * 2.92, t60 * 0.5, 0.25) }))
               .Add(Click(r, Math.Min(9000, f * 3.2), 2, 0.004), -5).Out();

    /// <summary>Sequence of bone clacks. accel &lt; 1 front-loads the hits, &gt; 1 accelerates toward the end.</summary>
    public static float[] BoneRattle(Rng r, double dur, int n, double fA, double fB, double accel = 1, double ampA = 1, double ampB = 1)
    {
        var m = Mx();
        for (int k = 0; k < n; k++)
        {
            double x = (k + r.R(0, 0.6)) / n, t = dur * Math.Pow(x, accel);
            m.Add(Clack(r, Lerp(fA, fB, x) * r.R(0.85, 1.15), r.R(0.025, 0.05)), ToDb(Lerp(ampA, ampB, x) * r.R(0.45, 1)), t);
        }
        return m.Out();
    }

    /// <summary>Scatter of small resonant pings (glass / ice / ceramic / mail / coins). Times biased early, levels fall with time.</summary>
    public static float[] Shards(Rng r, double dur, int n, double fLo, double fHi, double t60Lo = 0.03, double t60Hi = 0.09, double[] ratios = null, double bias = 1.6)
    {
        ratios ??= new[] { 1, 2.32, 4.25 }; var m = Mx();
        for (int k = 0; k < n; k++)
        {
            double t = dur * Math.Pow(r.U(), bias), f = r.X(fLo, fHi), t60 = r.X(t60Lo, t60Hi);
            var parts = ratios.Select((q, i) => (f * q * r.R(0.98, 1.02), t60 / (1 + 0.6 * i), Math.Pow(0.55, i))).ToArray();
            m.Add(Mx().Add(Modes(t60 * 1.3 + 0.004, parts)).Add(Click(r, Math.Min(11000, f * 1.5), 1.5, 0.002), -8).Out(), ToDb(r.R(0.3, 1) * (1 - 0.6 * t / dur)), t);
        }
        return m.Out();
    }

    /// <summary>Fire bed: low fluttering roar + mid body + hiss + stochastic crackle. Stationary (use LoopXfade for loops).</summary>
    public static float[] FireBed(Rng r, double dur, double crackRate, double roarLp = 320, double flutterHz = 14, double roarDb = 0, double crackDb = -5, double hissDb = -22)
    {
        var fl = Wander(r, 0.55, 1.0, flutterHz, dur); var fl2 = Wander(r, 0.35, 1.0, flutterHz * 0.45, dur);
        var roar = Brown(r, dur).LP(roarLp).HP(40).Env(t => fl(t));
        var mid = Pink(r, dur).BP(650, 0.6).Env(t => fl2(t) * fl(t));
        var hiss = White(r, dur).HP(4500).Env(Wander(r, 0.2, 1, 7, dur));
        var pops = Crackle(r, dur, crackRate, 900, 6500, 0.002, 0.014, 3);
        var big = Crackle(r, dur, crackRate * 0.08, 350, 1400, 0.01, 0.03, 1.5);
        return Mx().Add(roar, roarDb).Add(mid, roarDb - 8).Add(hiss, hissDb).Add(pops, crackDb).Add(big, crackDb - 5).Out();
    }

    /// <summary>Stick-slip creak (wood / hinge): irregular pulse train through body resonances.</summary>
    public static float[] Creak(Rng r, double dur, Fn rate, double[] res, double q = 14)
    {
        var src = Glottal(r, dur, rate, 0.18, 0.35, 0.3);
        var bands = res.Select((f, i) => (f * r.R(0.94, 1.06), q, Math.Pow(0.75, i))).ToArray();
        return Mx().Add(src.Bank(bands)).Add(src.HP(1500), -16).Out();
    }

    /// <summary>Wind bed: noise through wandering band-passes with slow gust envelope.</summary>
    public static float[] Wind(Rng r, double dur, double fLo, double fHi, double gustHz = 0.25, double whistle = 0.0, double q = 1.2)
    {
        var gust = Wander(r, 0.25, 1, gustHz, dur);
        var a = Pink(r, dur).SvfBP(Wander(r, fLo, fHi, gustHz * 1.7, dur, true), q).Env(t => gust(t));
        var b = Brown(r, dur).LP(fLo * 0.8).Env(Wander(r, 0.4, 1, gustHz * 0.8, dur));
        var m = Mx().Add(a).Add(b, -4);
        if (whistle > 0) m.Add(White(r, dur).SvfBP(Wander(r, fHi * 1.6, fHi * 3.2, gustHz * 1.3, dur, true), 14).Env(t => Math.Pow(gust(t), 3)), ToDb(whistle) - 6);
        return m.Out();
    }

    /// <summary>Rising liquid bubble "bloop" (resonant frequency climbs as the bubble closes).</summary>
    public static float[] Bubble(Rng r, double f, double dur, double rise = 1.8)
        => Mx().Add(Sine(dur, Exp(f, f * rise, dur)).Env(Bell(dur, 0.3, 0.8))).Add(Click(r, f * 6, 1.2, 0.006), -12, dur * 0.85).Out();
}

