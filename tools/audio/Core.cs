// Core DSP toolkit: deterministic RNG, time-function helpers, sources, filters, envelopes, effects.
// Everything works on mono float[] buffers at 44.1 kHz; stereo is a pair (class St).
using System.Runtime.CompilerServices;

namespace AudioTool;

public delegate double Fn(double t);

public static class A
{
    public const int SR = 44100;
    public const double TAU = Math.PI * 2;
    public static int N(double sec) => Math.Max(1, (int)Math.Round(sec * SR));
    public static double Db(double db) => Math.Pow(10, db / 20);
    public static double ToDb(double lin) => 20 * Math.Log10(Math.Max(lin, 1e-12));
    public static double Semi(double st) => Math.Pow(2, st / 12);
    public static double Mtof(double note) => 440 * Math.Pow(2, (note - 69) / 12);
    public static double Clamp(double v, double a, double b) => v < a ? a : v > b ? b : v;
    public static double Lerp(double a, double b, double x) => a + (b - a) * x;
    public static double Smooth(double x) { x = Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
}

/// <summary>xoroshiro128+ seeded through splitmix64; seeds derive from FNV-1a name hashes so output is reproducible.</summary>
public sealed class Rng
{
    ulong s0, s1;
    public Rng(ulong seed) { s0 = Split(ref seed); s1 = Split(ref seed); if ((s0 | s1) == 0) s1 = 1; for (int i = 0; i < 8; i++) Next(); }
    public Rng(string name, int idx = 0) : this(Hash(name) ^ ((ulong)(idx + 1) * 0x9E3779B97F4A7C15UL)) { }
    static ulong Split(ref ulong x) { x += 0x9E3779B97F4A7C15UL; ulong z = x; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL; z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31); }
    public static ulong Hash(string s) { ulong h = 14695981039346656037UL; foreach (char c in s) { h ^= c; h *= 1099511628211UL; } return h; }
    static ulong Rot(ulong x, int k) => (x << k) | (x >> (64 - k));
    public ulong Next() { ulong a = s0, b = s1, r = a + b; b ^= a; s0 = Rot(a, 24) ^ b ^ (b << 16); s1 = Rot(b, 37); return r; }
    public double U() => (Next() >> 11) * (1.0 / 9007199254740992.0);
    public double R(double a, double b) => a + (b - a) * U();
    public double S() => U() * 2 - 1;
    public double N() { double u = 1 - U(), v = U(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(A.TAU * v); }
    public int I(int n) => Math.Min(n - 1, (int)(U() * n));
    public int I(int a, int b) => a + I(b - a + 1);
    public double Semi(double range) => Math.Pow(2, R(-range, range) / 12);
    public double J(double v, double pct) => v * (1 + R(-pct, pct));
    public double X(double a, double b) => a * Math.Pow(b / a, U());
    public bool Chance(double p) => U() < p;
    public Rng Fork() => new Rng(Next());
    public T Pick<T>(params T[] items) => items[I(items.Length)];
}

/// <summary>Time-function helpers (parameter curves and envelopes).</summary>
public static class F
{
    public static Fn C(double v) => _ => v;
    public static Fn Lin(double a, double b, double dur) => t => t >= dur ? b : t <= 0 ? a : a + (b - a) * (t / dur);
    public static Fn Exp(double a, double b, double dur) => t => t >= dur ? b : t <= 0 ? a : a * Math.Pow(b / a, t / dur);
    /// <summary>Piecewise linear through (t0,v0,t1,v1,...)</summary>
    public static Fn Pts(params double[] tv) => t =>
    {
        int n = tv.Length / 2;
        if (t <= tv[0]) return tv[1];
        for (int i = 1; i < n; i++)
            if (t < tv[2 * i]) { double t0 = tv[2 * i - 2], t1 = tv[2 * i]; return A.Lerp(tv[2 * i - 1], tv[2 * i + 1], (t - t0) / (t1 - t0)); }
        return tv[2 * n - 1];
    };
    /// <summary>Piecewise exponential (geometric) through (t0,v0,t1,v1,...), all v &gt; 0.</summary>
    public static Fn PtsE(params double[] tv) => t =>
    {
        int n = tv.Length / 2;
        if (t <= tv[0]) return tv[1];
        for (int i = 1; i < n; i++)
            if (t < tv[2 * i]) { double t0 = tv[2 * i - 2], t1 = tv[2 * i]; return tv[2 * i - 1] * Math.Pow(tv[2 * i + 1] / tv[2 * i - 1], (t - t0) / (t1 - t0)); }
        return tv[2 * n - 1];
    };
    /// <summary>Piecewise smooth (cosine-eased) through (t0,v0,...)</summary>
    public static Fn PtsS(params double[] tv) => t =>
    {
        int n = tv.Length / 2;
        if (t <= tv[0]) return tv[1];
        for (int i = 1; i < n; i++)
            if (t < tv[2 * i]) { double t0 = tv[2 * i - 2], t1 = tv[2 * i]; return A.Lerp(tv[2 * i - 1], tv[2 * i + 1], A.Smooth((t - t0) / (t1 - t0))); }
        return tv[2 * n - 1];
    };
    /// <summary>Linear attack then exponential decay reaching -60 dB after t60.</summary>
    public static Fn AD(double atk, double t60) => t => t < 0 ? 0 : t < atk ? t / atk : Math.Exp(-6.9078 * (t - atk) / t60);
    /// <summary>Smooth attack / sustain / release (raised cosine ramps), total = a + s + r.</summary>
    public static Fn ASR(double a, double s, double r) => t =>
        t < 0 ? 0 : t < a ? 0.5 - 0.5 * Math.Cos(Math.PI * t / a) : t < a + s ? 1 : t < a + s + r ? 0.5 + 0.5 * Math.Cos(Math.PI * (t - a - s) / r) : 0;
    /// <summary>Arch with peak at pos*dur; pow shapes steepness.</summary>
    public static Fn Bell(double dur, double pos = 0.5, double pow = 1) => t =>
    {
        if (t <= 0 || t >= dur) return 0;
        double x = t / dur, y = x < pos ? x / pos : (1 - x) / (1 - pos);
        return Math.Pow(Math.Sin(y * Math.PI / 2), pow * 2);
    };
    public static Fn Mul(Fn a, Fn b) => t => a(t) * b(t);
    public static Fn Add(Fn a, Fn b) => t => a(t) + b(t);
    public static Fn Shift(Fn a, double dt) => t => a(t - dt);
    /// <summary>Smoothed random walk between lo and hi with a new target every 1/rate s (deterministic).</summary>
    public static Fn Wander(Rng r, double lo, double hi, double rate, double dur, bool log = false)
    {
        int n = (int)(dur * rate) + 3; var v = new double[n];
        for (int i = 0; i < n; i++) v[i] = log ? r.X(lo, hi) : r.R(lo, hi);
        return t => { double x = Math.Max(0, t * rate); int i = Math.Min(n - 2, (int)x); double fr = A.Smooth(x - i); return log ? v[i] * Math.Pow(v[i + 1] / v[i], fr) : A.Lerp(v[i], v[i + 1], fr); };
    }
}

public struct Biquad
{
    public double b0, b1, b2, a1, a2, z1, z2;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Tick(double x) { double y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return y; }
    static Biquad Mk(double b0, double b1, double b2, double a0, double a1, double a2) => new Biquad { b0 = b0 / a0, b1 = b1 / a0, b2 = b2 / a0, a1 = a1 / a0, a2 = a2 / a0 };
    static void P(double f, double q, out double c, out double al) { f = A.Clamp(f, 5, A.SR * 0.49); double w = A.TAU * f / A.SR; c = Math.Cos(w); al = Math.Sin(w) / (2 * q); }
    public static Biquad LP(double f, double q = 0.7071) { P(f, q, out var c, out var al); return Mk((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + al, -2 * c, 1 - al); }
    public static Biquad HP(double f, double q = 0.7071) { P(f, q, out var c, out var al); return Mk((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + al, -2 * c, 1 - al); }
    public static Biquad BP(double f, double q) { P(f, q, out var c, out var al); return Mk(al, 0, -al, 1 + al, -2 * c, 1 - al); }
    public static Biquad Notch(double f, double q) { P(f, q, out var c, out var al); return Mk(1, -2 * c, 1, 1 + al, -2 * c, 1 - al); }
    public static Biquad Peak(double f, double q, double db) { P(f, q, out var c, out var al); double a = Math.Pow(10, db / 40); return Mk(1 + al * a, -2 * c, 1 - al * a, 1 + al / a, -2 * c, 1 - al / a); }
    public static Biquad LoShelf(double f, double db, double q = 0.7071)
    {
        P(f, q, out var c, out var al); double a = Math.Pow(10, db / 40), s = 2 * Math.Sqrt(a) * al;
        return Mk(a * ((a + 1) - (a - 1) * c + s), 2 * a * ((a - 1) - (a + 1) * c), a * ((a + 1) - (a - 1) * c - s), (a + 1) + (a - 1) * c + s, -2 * ((a - 1) + (a + 1) * c), (a + 1) + (a - 1) * c - s);
    }
    public static Biquad HiShelf(double f, double db, double q = 0.7071)
    {
        P(f, q, out var c, out var al); double a = Math.Pow(10, db / 40), s = 2 * Math.Sqrt(a) * al;
        return Mk(a * ((a + 1) + (a - 1) * c + s), -2 * a * ((a - 1) + (a + 1) * c), a * ((a + 1) + (a - 1) * c - s), (a + 1) - (a - 1) * c + s, 2 * ((a - 1) - (a + 1) * c), (a + 1) - (a - 1) * c - s);
    }
    public float[] Run(float[] x) { var o = new float[x.Length]; var f = this; for (int i = 0; i < x.Length; i++) o[i] = (float)f.Tick(x[i]); return o; }
}

/// <summary>Stereo buffer.</summary>
public sealed class St
{
    public float[] L, R;
    public St(float[] l, float[] r) { L = l; R = r; if (l.Length != r.Length) { int n = Math.Max(l.Length, r.Length); Array.Resize(ref L, n); Array.Resize(ref R, n); } }
    public St(int n) { L = new float[n]; R = new float[n]; }
    public int Len => L.Length;
    /// <summary>Constant-power pan, pan in [-1,1].</summary>
    public static St Pan(float[] m, double pan = 0)
    {
        double a = (pan + 1) * Math.PI / 4; float gl = (float)Math.Cos(a), gr = (float)Math.Sin(a);
        var s = new St(m.Length); for (int i = 0; i < m.Length; i++) { s.L[i] = m[i] * gl; s.R[i] = m[i] * gr; }
        return s;
    }
    public St Add(St o, double db = 0, double at = 0)
    {
        float g = (float)A.Db(db); int off = (int)Math.Round(at * A.SR); int need = off + o.Len;
        if (need > Len) { Array.Resize(ref L, need); Array.Resize(ref R, need); }
        for (int i = 0; i < o.Len; i++) { int j = i + off; if (j < 0) continue; L[j] += o.L[i] * g; R[j] += o.R[i] * g; }
        return this;
    }
    /// <summary>Adds a mono layer NORMALISED to unit peak (db = its peak level), panned.</summary>
    public St Add(float[] m, double db = 0, double at = 0, double pan = 0)
    {
        double p = 0; foreach (var v in m) p = Math.Max(p, Math.Abs(v)); if (p < 1e-9) return this;
        return Add(Pan(m, pan), db - A.ToDb(p), at);
    }
    /// <summary>Adds a mono layer at its own level.</summary>
    public St Raw(float[] m, double db = 0, double at = 0, double pan = 0) => Add(Pan(m, pan), db, at);
    public St Gain(double db) { float g = (float)A.Db(db); for (int i = 0; i < Len; i++) { L[i] *= g; R[i] *= g; } return this; }
    public St Map(Func<float[], float[]> f) => new St(f(L), f(R));
    public float[] Mono() { var m = new float[Len]; for (int i = 0; i < Len; i++) m[i] = 0.5f * (L[i] + R[i]); return m; }
    public double Peak() { double p = 0; for (int i = 0; i < Len; i++) { p = Math.Max(p, Math.Abs(L[i])); p = Math.Max(p, Math.Abs(R[i])); } return p; }
    public St Resize(int n) { Array.Resize(ref L, n); Array.Resize(ref R, n); return this; }
}

/// <summary>Layer mixer for mono buffers: Mx().Add(a, dB, atSeconds)...Out()</summary>
public sealed class Mixer
{
    readonly List<(float[] b, float g, int at)> items = new();
    /// <summary>Adds a layer NORMALISED to unit peak, so db is the layer's peak level in the mix.</summary>
    public Mixer Add(float[] b, double db = 0, double at = 0)
    {
        if (b == null) return this; double p = 0; foreach (var v in b) p = Math.Max(p, Math.Abs(v)); if (p < 1e-9) return this;
        items.Add((b, (float)(A.Db(db) / p), (int)Math.Round(at * A.SR))); return this;
    }
    /// <summary>Adds a layer at its own level (no normalisation).</summary>
    public Mixer Raw(float[] b, double db = 0, double at = 0) { if (b != null) items.Add((b, (float)A.Db(db), (int)Math.Round(at * A.SR))); return this; }
    public float[] Out()
    {
        int n = 1; foreach (var (b, _, at) in items) n = Math.Max(n, at + b.Length);
        var o = new float[n];
        foreach (var (b, g, at) in items) for (int i = 0; i < b.Length; i++) { int j = i + at; if (j >= 0) o[j] += b[i] * g; }
        return o;
    }
}

/// <summary>Signal sources.</summary>
public static class Gen
{
    public static Mixer Mx() => new Mixer();
    public static float[] Sil(double dur) => new float[A.N(dur)];

    public static float[] Sine(double dur, Fn f, double ph = 0)
    {
        int n = A.N(dur); var o = new float[n]; double p = ph * A.TAU;
        for (int i = 0; i < n; i++) { o[i] = (float)Math.Sin(p); p += A.TAU * f(i / (double)A.SR) / A.SR; if (p > 1e6) p -= A.TAU * Math.Floor(p / A.TAU); }
        return o;
    }
    public static float[] Sine(double dur, double f, double ph = 0) => Sine(dur, F.C(f), ph);

    static double Blep(double t, double dt)
    {
        if (t < dt) { t /= dt; return t + t - t * t - 1; }
        if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
        return 0;
    }
    public static float[] Saw(double dur, Fn f, double ph = 0)
    {
        int n = A.N(dur); var o = new float[n]; double p = ph - Math.Floor(ph);
        for (int i = 0; i < n; i++) { double dt = Math.Min(0.45, f(i / (double)A.SR) / A.SR); o[i] = (float)(2 * p - 1 - Blep(p, dt)); p += dt; if (p >= 1) p -= 1; }
        return o;
    }
    public static float[] Saw(double dur, double f, double ph = 0) => Saw(dur, F.C(f), ph);
    public static float[] Square(double dur, Fn f, double pw = 0.5, double ph = 0)
    {
        int n = A.N(dur); var o = new float[n]; double p = ph - Math.Floor(ph);
        for (int i = 0; i < n; i++)
        {
            double dt = Math.Min(0.45, f(i / (double)A.SR) / A.SR); double v = p < pw ? 1 : -1;
            v += Blep(p, dt); double p2 = p + 1 - pw; if (p2 >= 1) p2 -= 1; v -= Blep(p2, dt);
            o[i] = (float)v; p += dt; if (p >= 1) p -= 1;
        }
        return o;
    }
    public static float[] Square(double dur, double f, double pw = 0.5) => Square(dur, F.C(f), pw);
    public static float[] Tri(double dur, Fn f)
    {
        int n = A.N(dur); var o = new float[n]; double p = 0.25;
        for (int i = 0; i < n; i++) { o[i] = (float)(4 * Math.Abs(p - 0.5) - 1); p += f(i / (double)A.SR) / A.SR; if (p >= 1) p -= 1; }
        return o;
    }

    public static float[] White(Rng r, double dur) { int n = A.N(dur); var o = new float[n]; for (int i = 0; i < n; i++) o[i] = (float)r.S(); return o; }
    /// <summary>Pink noise (Paul Kellet's economy filter), roughly unit peak.</summary>
    public static float[] Pink(Rng r, double dur)
    {
        int n = A.N(dur); var o = new float[n]; double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
        for (int i = -2000; i < n; i++)
        {
            double w = r.S();
            b0 = 0.99886 * b0 + w * 0.0555179; b1 = 0.99332 * b1 + w * 0.0750759; b2 = 0.96900 * b2 + w * 0.1538520;
            b3 = 0.86650 * b3 + w * 0.3104856; b4 = 0.55000 * b4 + w * 0.5329522; b5 = -0.7616 * b5 - w * 0.0168980;
            double p = b0 + b1 + b2 + b3 + b4 + b5 + b6 + w * 0.5362; b6 = w * 0.115926;
            if (i >= 0) o[i] = (float)(p * 0.2);
        }
        return o;
    }
    /// <summary>Brown (red) noise via leaky integrator, roughly unit peak.</summary>
    public static float[] Brown(Rng r, double dur)
    {
        int n = A.N(dur); var o = new float[n]; double s = 0;
        for (int i = -4000; i < n; i++) { s = 0.997 * s + r.S() * 0.06; if (i >= 0) o[i] = (float)s; }
        return o;
    }

    /// <summary>Sum of exponentially decaying sinusoids (modal synthesis). Sine phase start = click free.</summary>
    public static float[] Modes(double dur, IEnumerable<(double f, double t60, double a)> modes, double atk = 0.0)
    {
        int n = A.N(dur); var o = new float[n]; var acc = new double[n];
        foreach (var (f, t60, a) in modes)
        {
            if (f <= 10 || f >= A.SR * 0.47 || a == 0) continue;
            double w = A.TAU * f / A.SR, d = Math.Exp(-6.9078 / (t60 * A.SR));
            double cr = Math.Cos(w) * d, ci = Math.Sin(w) * d, re = 0, im = a;   // im*... start: sin phase 0
            // state (re, im) rotates; output = re where initial (0,a) rotated => a*sin(w n) decaying
            int m = Math.Min(n, (int)(t60 * 1.6 * A.SR) + 8);
            double x = a, y = 0; // x + j y ; output = imaginary part
            for (int i = 0; i < m; i++) { acc[i] += y; double nx = x * cr - y * ci; y = x * ci + y * cr; x = nx; }
        }
        int na = (int)(atk * A.SR);
        for (int i = 0; i < n; i++) o[i] = (float)(i < na ? acc[i] * i / na : acc[i]);
        return o;
    }

    /// <summary>Inharmonic metal/glass partial set: ratios jittered, higher partials decay faster (t60 ~ f^-slope).</summary>
    public static List<(double f, double t60, double a)> PartialSet(Rng r, double f0, double[] ratios, double t60, double slope = 0.7, double tilt = 0.75, double jit = 0.015)
    {
        var l = new List<(double, double, double)>();
        for (int i = 0; i < ratios.Length; i++)
        {
            double f = f0 * ratios[i] * (1 + r.R(-jit, jit));
            l.Add((f, t60 * Math.Pow(ratios[0] / ratios[i], slope) * r.R(0.8, 1.2), Math.Pow(tilt, i) * r.R(0.6, 1.0)));
        }
        return l;
    }
    public static readonly double[] RPlate = { 1, 1.59, 2.14, 2.30, 2.65, 2.92, 3.16, 3.50, 3.60, 4.06, 4.60, 5.12 };   // circular plate / membrane-like
    public static readonly double[] RBar = { 1, 2.756, 5.404, 8.933, 13.34, 18.64 };                                     // free bar
    public static readonly double[] RBell = { 0.5, 1, 1.183, 1.506, 2, 2.514, 2.662, 3.011, 4.166, 5.433, 6.796 };      // church-bell style
    public static readonly double[] RTube = { 1, 2.0, 3.0, 4.2, 5.4, 6.8, 8.21 };                                        // tubular/glock style (near harmonic then stretched)
    public static readonly double[] RGlass = { 1, 2.32, 4.25, 6.63, 9.38, 12.2 };                                        // wine-glass / ice like

    /// <summary>Glottal pulse train (Rosenberg flow, differentiated) with per-cycle jitter and shimmer. Peak ~1.</summary>
    public static float[] Glottal(Rng r, double dur, Fn f0, double jitter = 0.01, double shimmer = 0.05, double openQ = 0.6)
    {
        int n = A.N(dur); var flow = new double[n + 2]; double pos = 0;
        while (pos < n)
        {
            double f = Math.Max(15, f0(pos / A.SR) * (1 + jitter * r.N()));
            double period = A.SR / f, amp = Math.Max(0.1, 1 + shimmer * r.N());
            double nOpen = period * openQ * 0.68, nClose = period * openQ * 0.32;
            int i0 = (int)Math.Ceiling(pos), i1 = Math.Min(n, (int)Math.Ceiling(pos + period));
            for (int i = i0; i < i1; i++)
            {
                double x = i - pos, g = x < nOpen ? 0.5 * (1 - Math.Cos(Math.PI * x / nOpen)) : x < nOpen + nClose ? Math.Cos(0.5 * Math.PI * (x - nOpen) / nClose) : 0;
                flow[i] = amp * g;
            }
            pos += period;
        }
        var o = new float[n]; double pk = 1e-9;
        for (int i = 1; i < n; i++) { double d = flow[i] - flow[i - 1]; o[i] = (float)d; pk = Math.Max(pk, Math.Abs(d)); }
        for (int i = 0; i < n; i++) o[i] = (float)(o[i] / pk);
        return o;
    }

    /// <summary>Stochastic pops: Poisson events (rate/s may vary) each a short decaying resonance; amp = U^skew.</summary>
    public static float[] Crackle(Rng r, double dur, Fn rate, double fLo, double fHi, double t60Lo = 0.002, double t60Hi = 0.012, double skew = 3)
    {
        int n = A.N(dur); var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (r.U() >= rate(i / (double)A.SR) / A.SR) continue;
            double f = r.X(fLo, fHi), t60 = r.X(t60Lo, t60Hi), amp = Math.Pow(r.U(), skew) * (r.Chance(0.5) ? 1 : -1);
            double w = A.TAU * f / A.SR, d = Math.Exp(-6.9078 / (t60 * A.SR)); int m = Math.Min(n - i, (int)(t60 * A.SR * 1.3) + 2);
            double e = amp, ph = Math.PI / 2;   // cosine start = sharp click
            for (int k = 0; k < m; k++) { o[i + k] += (float)(e * Math.Sin(ph)); e *= d; ph += w; }
        }
        return o;
    }
    public static float[] Crackle(Rng r, double dur, double rate, double fLo, double fHi, double t60Lo = 0.002, double t60Hi = 0.012, double skew = 3) => Crackle(r, dur, F.C(rate), fLo, fHi, t60Lo, t60Hi, skew);

    /// <summary>Noise through a swept band-pass with an amplitude arch: air movement / swings.</summary>
    public static float[] Whoosh(Rng r, double dur, Fn fc, double q, Fn env, bool pink = true)
        => (pink ? Pink(r, dur) : White(r, dur)).SvfBP(fc, q).Env(env);

    /// <summary>Wet / granular texture: noise through a resonant band-pass whose centre jumps around fast, with grainy AM.</summary>
    public static float[] Squelch(Rng r, double dur, double fLo, double fHi, double moveHz, double q = 5, double grain = 0.7)
    {
        var fc = F.Wander(r, fLo, fHi, moveHz, dur, true);
        var am = F.Wander(r, 1 - grain, 1, moveHz * 1.7, dur);
        var x = White(r, dur).SvfBP(fc, q);
        for (int i = 0; i < x.Length; i++) { double a = am(i / (double)A.SR); x[i] *= (float)(a * a); }
        return x;
    }

    /// <summary>Sub thump: sine with exponential pitch drop f0-&gt;f1 over sweep seconds and exponential decay.</summary>
    public static float[] Thump(double f0, double f1, double sweep, double t60, double atk = 0.002)
        => Sine(t60 * 1.15 + atk, F.Exp(f0, f1, sweep)).Env(F.AD(atk, t60));

    /// <summary>Short band-limited noise burst (transient / click).</summary>
    public static float[] Click(Rng r, double fc, double q, double t60, double atk = 0.0003)
        => White(r, t60 * 1.3 + 0.004).BP(fc, q).Env(F.AD(atk, t60));

    /// <summary>Band-passed noise body with decay.</summary>
    public static float[] Body(Rng r, double fc, double q, double t60, double atk = 0.001)
        => Pink(r, t60 * 1.2 + 0.01).BP(fc, q).Env(F.AD(atk, t60));
}

/// <summary>Processing extension methods (all return new arrays unless noted).</summary>
public static class Fx
{
    public static float[] Gain(this float[] x, double db) { float g = (float)A.Db(db); var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) o[i] = x[i] * g; return o; }
    public static float[] Mul(this float[] x, double k) { var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) o[i] = (float)(x[i] * k); return o; }
    public static float[] Env(this float[] x, Fn e) { var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) o[i] = (float)(x[i] * e(i / (double)A.SR)); return o; }
    public static float[] Dec(this float[] x, double t60, double atk = 0.001) => x.Env(F.AD(atk, t60));
    public static float[] Fade(this float[] x, double fin, double fout)
    {
        var o = (float[])x.Clone(); int a = Math.Min(o.Length / 2, (int)(fin * A.SR)), b = Math.Min(o.Length / 2, (int)(fout * A.SR));
        for (int i = 0; i < a; i++) o[i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / a));
        for (int i = 0; i < b; i++) o[o.Length - 1 - i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / b));
        return o;
    }
    public static float[] Rev(this float[] x) { var o = (float[])x.Clone(); Array.Reverse(o); return o; }
    public static float[] At(this float[] x, double sec) { int k = (int)Math.Round(sec * A.SR); var o = new float[x.Length + k]; Array.Copy(x, 0, o, k, x.Length); return o; }
    public static float[] Pad(this float[] x, double sec) { var o = new float[x.Length + A.N(sec)]; Array.Copy(x, o, x.Length); return o; }
    public static float[] Cut(this float[] x, double sec) { int n = Math.Min(x.Length, A.N(sec)); var o = new float[n]; Array.Copy(x, o, n); return o; }
    public static float[] Len(this float[] x, int n) { var o = new float[n]; Array.Copy(x, o, Math.Min(n, x.Length)); return o; }
    public static double Peak(this float[] x) { double p = 0; foreach (var v in x) p = Math.Max(p, Math.Abs(v)); return p; }
    public static double Rms(this float[] x) { double s = 0; foreach (var v in x) s += (double)v * v; return Math.Sqrt(s / Math.Max(1, x.Length)); }
    public static float[] Norm(this float[] x, double db = 0) { double p = x.Peak(); return p < 1e-9 ? (float[])x.Clone() : x.Mul(A.Db(db) / p); }

    public static float[] LP(this float[] x, double f, double q = 0.7071) => Biquad.LP(f, q).Run(x);
    public static float[] HP(this float[] x, double f, double q = 0.7071) => Biquad.HP(f, q).Run(x);
    public static float[] BP(this float[] x, double f, double q) => Biquad.BP(f, q).Run(x);
    public static float[] Notch(this float[] x, double f, double q) => Biquad.Notch(f, q).Run(x);
    public static float[] Peq(this float[] x, double f, double q, double db) => Biquad.Peak(f, q, db).Run(x);
    public static float[] LoShelf(this float[] x, double f, double db) => Biquad.LoShelf(f, db).Run(x);
    public static float[] HiShelf(this float[] x, double f, double db) => Biquad.HiShelf(f, db).Run(x);
    public static float[] LP4(this float[] x, double f) => x.LP(f, 0.5412).LP(f, 1.3066);
    public static float[] HP4(this float[] x, double f) => x.HP(f, 0.5412).HP(f, 1.3066);
    public static float[] LP1(this float[] x, double f) { double c = 1 - Math.Exp(-A.TAU * f / A.SR), s = 0; var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) { s += c * (x[i] - s); o[i] = (float)s; } return o; }
    public static float[] HP1(this float[] x, double f) { double c = 1 - Math.Exp(-A.TAU * f / A.SR), s = 0; var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) { s += c * (x[i] - s); o[i] = (float)(x[i] - s); } return o; }

    enum SvfMode { LP, BP, HP, BPn }
    static float[] Svf(float[] x, Fn fc, Fn q, SvfMode mode)
    {
        var o = new float[x.Length]; double ic1 = 0, ic2 = 0, a1 = 0, a2 = 0, a3 = 0, k = 1;
        for (int i = 0; i < x.Length; i++)
        {
            if ((i & 15) == 0)
            {
                double t = i / (double)A.SR, f = A.Clamp(fc(t), 12, A.SR * 0.47); k = 1 / Math.Max(0.3, q(t));
                double g = Math.Tan(Math.PI * f / A.SR); a1 = 1 / (1 + g * (g + k)); a2 = g * a1; a3 = g * a2;
            }
            double v3 = x[i] - ic2, v1 = a1 * ic1 + a2 * v3, v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2 * v1 - ic1; ic2 = 2 * v2 - ic2;
            o[i] = (float)(mode switch { SvfMode.LP => v2, SvfMode.BP => v1, SvfMode.BPn => v1 * k, _ => x[i] - k * v1 - v2 });
        }
        return o;
    }
    /// <summary>Time-varying state-variable filters (TPT). SvfBP has unity peak gain.</summary>
    public static float[] SvfLP(this float[] x, Fn fc, double q = 0.7071) => Svf(x, fc, F.C(q), SvfMode.LP);
    public static float[] SvfHP(this float[] x, Fn fc, double q = 0.7071) => Svf(x, fc, F.C(q), SvfMode.HP);
    public static float[] SvfBP(this float[] x, Fn fc, double q) => Svf(x, fc, F.C(q), SvfMode.BPn);
    public static float[] SvfBP(this float[] x, Fn fc, Fn q) => Svf(x, fc, q, SvfMode.BPn);
    /// <summary>Resonant band (skirt gain, peak grows with Q) – use for ringing resonators.</summary>
    public static float[] SvfRes(this float[] x, Fn fc, double q) => Svf(x, fc, F.C(q), SvfMode.BP);

    /// <summary>Parallel bank of constant-peak band-passes.</summary>
    public static float[] Bank(this float[] x, params (double f, double q, double g)[] bands)
    {
        var o = new float[x.Length];
        foreach (var (f, q, g) in bands) { if (f >= A.SR * 0.47) continue; var b = Biquad.BP(f, q); float gg = (float)g; for (int i = 0; i < x.Length; i++) o[i] += (float)b.Tick(x[i]) * gg; }
        return o;
    }

    public static float[] Drive(this float[] x, double pre, double bias = 0)
    {
        var o = new float[x.Length]; double nrm = 1 / Math.Tanh(pre), b0 = Math.Tanh(pre * bias);
        for (int i = 0; i < x.Length; i++) o[i] = (float)((Math.Tanh(pre * (x[i] + bias)) - b0) * nrm);
        return o;
    }
    public static float[] Fold(this float[] x, double pre) { var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) o[i] = (float)Math.Sin(x[i] * pre * Math.PI / 2); return o; }
    public static float[] Clip(this float[] x, double lim) { var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) o[i] = (float)A.Clamp(x[i], -lim, lim); return o; }
    public static float[] Am(this float[] x, Fn m) => x.Env(m);
    public static float[] Trem(this float[] x, double hz, double depth, double ph = 0) => x.Env(t => 1 - depth * (0.5 + 0.5 * Math.Sin(A.TAU * (hz * t + ph))));
    public static float[] Ring(this float[] x, Fn f) { var s = Gen.Sine(x.Length / (double)A.SR, f); var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) o[i] = x[i] * s[i]; return o; }

    /// <summary>Feedback comb with one-pole damping in the loop. Output length = input + tail.</summary>
    public static float[] Comb(this float[] x, double delay, double fb, double dampHz = 8000, double tail = 0.0, double mix = 1)
    {
        int d = Math.Max(1, (int)Math.Round(delay * A.SR)), n = x.Length + A.N(tail) - (tail > 0 ? 0 : 1); if (tail <= 0) n = x.Length;
        var buf = new double[d]; var o = new float[n]; double c = 1 - Math.Exp(-A.TAU * dampHz / A.SR), lp = 0; int p = 0;
        for (int i = 0; i < n; i++)
        {
            double xin = i < x.Length ? x[i] : 0, y = buf[p]; lp += c * (y - lp);
            buf[p] = xin + lp * fb; if (++p >= d) p = 0;
            o[i] = (float)(xin * (1 - mix) + (xin + y * fb) * mix);
        }
        return o;
    }
    public static float[] Allpass(this float[] x, double delay, double g)
    {
        int d = Math.Max(1, (int)Math.Round(delay * A.SR)); var buf = new double[d]; var o = new float[x.Length]; int p = 0;
        for (int i = 0; i < x.Length; i++) { double v = buf[p], w = x[i] + g * v; buf[p] = w; o[i] = (float)(v - g * w); if (++p >= d) p = 0; }
        return o;
    }
    /// <summary>Modulated delay (chorus / flanger). depth and base in ms.</summary>
    public static float[] Chorus(this float[] x, double baseMs, double depthMs, double rateHz, double mix = 0.5, double fb = 0, double ph = 0)
    {
        int max = (int)((baseMs + depthMs + 2) * A.SR / 1000) + 4; var buf = new double[max]; var o = new float[x.Length]; int w = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double dl = (baseMs + depthMs * (0.5 + 0.5 * Math.Sin(A.TAU * (rateHz * i / A.SR + ph)))) * A.SR / 1000;
            double rp = w - dl; while (rp < 0) rp += max; int i0 = (int)rp; double fr = rp - i0; int i1 = i0 + 1 == max ? 0 : i0 + 1;
            double y = buf[i0] * (1 - fr) + buf[i1] * fr;
            buf[w] = x[i] + y * fb; if (++w >= max) w = 0;
            o[i] = (float)(x[i] * (1 - mix) + y * mix);
        }
        return o;
    }
    public static float[] Echo(this float[] x, double delay, double fb, double mixDb, double lpHz = 4000, int taps = 6)
    {
        var o = x.Pad(delay * taps); var cur = x; double g = A.Db(mixDb);
        for (int k = 1; k <= taps; k++) { cur = cur.LP1(lpHz); int off = (int)(delay * k * A.SR); float gg = (float)(g * Math.Pow(fb, k - 1)); for (int i = 0; i < cur.Length && i + off < o.Length; i++) o[i + off] += cur[i] * gg; }
        return o;
    }

    /// <summary>Resample by ratio (&gt;1 = higher pitch and shorter). Cubic Hermite, with anti-alias LP when pitching up.</summary>
    public static float[] Resample(this float[] x, double ratio)
    {
        if (Math.Abs(ratio - 1) < 1e-6) return (float[])x.Clone();
        var src = ratio > 1.0 ? x.LP4(A.SR * 0.47 / ratio) : x;
        int n = (int)(x.Length / ratio); var o = new float[n];
        for (int i = 0; i < n; i++)
        {
            double p = i * ratio; int i1 = (int)p; double f = p - i1;
            double y0 = src[Math.Max(0, i1 - 1)], y1 = src[i1], y2 = src[Math.Min(src.Length - 1, i1 + 1)], y3 = src[Math.Min(src.Length - 1, i1 + 2)];
            double c0 = y1, c1 = 0.5 * (y2 - y0), c2 = y0 - 2.5 * y1 + 2 * y2 - 0.5 * y3, c3 = 0.5 * (y3 - y0) + 1.5 * (y1 - y2);
            o[i] = (float)(((c3 * f + c2) * f + c1) * f + c0);
        }
        return o;
    }

    /// <summary>Mono reverb (FDN folded to mono). wetDb = wet energy relative to dry energy.</summary>
    public static float[] Reverb(this float[] x, double rt60, double wetDb, double size = 0.6, double dampHz = 5000, double pre = 0.005, double tail = -1, bool dry = true)
    {
        if (tail < 0) tail = rt60 * 0.9;
        var xin = x.Pad(tail); var (l, r) = Verb.Fdn(xin, xin, rt60, size, dampHz, 0.4, pre, 100, 0.2);
        var wet = new float[xin.Length]; double ew = 0, ed = 0;
        for (int i = 0; i < wet.Length; i++) { wet[i] = 0.7071f * (l[i] + r[i]); ew += (double)wet[i] * wet[i]; ed += (double)xin[i] * xin[i]; }
        float g = (float)(Math.Sqrt(ed / Math.Max(ew, 1e-20)) * A.Db(wetDb));
        for (int i = 0; i < wet.Length; i++) wet[i] = wet[i] * g + (dry ? xin[i] : 0);
        return wet;
    }
    /// <summary>Stereo reverb from mono or stereo source; wetDb relative to dry energy.</summary>
    public static St ReverbSt(this St x, double rt60, double wetDb, double size = 1.0, double dampHz = 5000, double pre = 0.012, double tail = -1, bool dry = true)
    {
        if (tail < 0) tail = rt60 * 0.9;
        var il = x.L.Pad(tail); var ir = x.R.Pad(tail); var (l, r) = Verb.Fdn(il, ir, rt60, size, dampHz, 0.4, pre, 100, 0.25);
        double ew = 0, ed = 0; for (int i = 0; i < l.Length; i++) { ew += (double)l[i] * l[i] + (double)r[i] * r[i]; ed += (double)il[i] * il[i] + (double)ir[i] * ir[i]; }
        float g = (float)(Math.Sqrt(ed / Math.Max(ew, 1e-20)) * A.Db(wetDb));
        for (int i = 0; i < l.Length; i++) { l[i] = l[i] * g + (dry ? il[i] : 0); r[i] = r[i] * g + (dry ? ir[i] : 0); }
        return new St(l, r);
    }
    public static St ReverbSt(this float[] x, double rt60, double wetDb, double size = 1.0, double dampHz = 5000, double pre = 0.012, double tail = -1, bool dry = true, double pan = 0)
        => St.Pan(x, pan).ReverbSt(rt60, wetDb, size, dampHz, pre, tail, dry);

    /// <summary>Seamless loop from an event-style buffer (starts from silence): the tail beyond loopLen is wrapped onto the start.</summary>
    public static float[] LoopWrap(this float[] x, int loopLen)
    {
        var o = new float[loopLen];
        for (int i = 0; i < x.Length; i++) o[i % loopLen] += x[i];
        return o;
    }
    /// <summary>Seamless loop from a stationary bed: the extra length is equal-power cross-faded into the head.</summary>
    public static float[] LoopXfade(this float[] x, int loopLen)
    {
        int xf = x.Length - loopLen; if (xf <= 0) throw new ArgumentException("buffer must be longer than loop");
        var o = new float[loopLen]; Array.Copy(x, o, loopLen);
        for (int i = 0; i < xf; i++) { double a = (i + 0.5) / xf * Math.PI / 2; o[i] = (float)(x[i] * Math.Sin(a) + x[loopLen + i] * Math.Cos(a)); }
        return o;
    }
    public static St LoopWrap(this St s, int n) => new St(s.L.LoopWrap(n), s.R.LoopWrap(n));
    public static St LoopXfade(this St s, int n) => new St(s.L.LoopXfade(n), s.R.LoopXfade(n));
}
