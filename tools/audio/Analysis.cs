// Measurement: peak, RMS, BS.1770 integrated loudness, DC, spectral centroid, clip count, silence, loop seam.
// Mastering helpers: loudness normalise, look-ahead limiter (optionally circular for loops).
namespace AudioTool;

public sealed class Stats
{
    public int Channels, Samples;
    public double Duration, PeakDb, RmsDb, Lufs, Dc, Centroid, LeadSilenceMs, TailDb, SeamRatio, SeamLevelDb;
    public int Clips;
}

public static class Meter
{
    static Biquad KShelf()
    {
        double f0 = 1681.974450955533, G = 3.999843853973347, Q = 0.7071752369554196;
        double K = Math.Tan(Math.PI * f0 / A.SR), Vh = Math.Pow(10, G / 20), Vb = Math.Pow(Vh, 0.4996667741545416), a0 = 1 + K / Q + K * K;
        return new Biquad { b0 = (Vh + Vb * K / Q + K * K) / a0, b1 = 2 * (K * K - Vh) / a0, b2 = (Vh - Vb * K / Q + K * K) / a0, a1 = 2 * (K * K - 1) / a0, a2 = (1 - K / Q + K * K) / a0 };
    }
    static Biquad KHp()
    {
        double f0 = 38.13547087602444, Q = 0.5003270373238773, K = Math.Tan(Math.PI * f0 / A.SR), a0 = 1 + K / Q + K * K;
        return new Biquad { b0 = 1, b1 = -2, b2 = 1, a1 = 2 * (K * K - 1) / a0, a2 = (1 - K / Q + K * K) / a0 };
    }
    static double[] KWeightSq(float[] x, bool circular)
    {
        var s = KShelf(); var h = KHp(); var o = new double[x.Length];
        if (circular) for (int i = Math.Max(0, x.Length - A.SR); i < x.Length; i++) h.Tick(s.Tick(x[i]));   // warm up from loop end
        for (int i = 0; i < x.Length; i++) { double y = h.Tick(s.Tick(x[i])); o[i] = y * y; }
        return o;
    }

    /// <summary>ITU-R BS.1770-4 integrated loudness (400 ms blocks, 75% overlap, -70 abs gate, -10 LU relative gate).
    /// Signals shorter than one block are measured ungated over their whole length.</summary>
    public static double Lufs(float[] l, float[] r = null, bool circular = false)
    {
        var a = KWeightSq(l, circular); var b = r != null ? KWeightSq(r, circular) : null;
        int n = l.Length, blk = (int)(0.4 * A.SR), hop = blk / 4;
        if (n < blk)
        {
            double s = 0; for (int i = 0; i < n; i++) s += a[i] + (b != null ? b[i] : 0);
            return -0.691 + 10 * Math.Log10(Math.Max(s / Math.Max(1, n), 1e-14));
        }
        var cs = new double[n + 1]; for (int i = 0; i < n; i++) cs[i + 1] = cs[i] + a[i] + (b != null ? b[i] : 0);
        var z = new List<double>();
        for (int st = 0; st + blk <= n; st += hop) z.Add((cs[st + blk] - cs[st]) / blk);
        double L(double ms) => -0.691 + 10 * Math.Log10(Math.Max(ms, 1e-14));
        var g1 = z.Where(v => L(v) > -70).ToList(); if (g1.Count == 0) return -70;
        double thr = L(g1.Average()) - 10;
        var g2 = g1.Where(v => L(v) > thr).ToList(); if (g2.Count == 0) return -70;
        return L(g2.Average());
    }
    public static double Lufs(St s, bool circular = false) => Lufs(s.L, s.R, circular);

    public static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++) { int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit; j ^= bit; if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); } }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -A.TAU / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int j = 0; j < len / 2; j++)
                {
                    int u = i + j, v = u + len / 2; double tr = re[v] * cr - im[v] * ci, ti = re[v] * ci + im[v] * cr;
                    re[v] = re[u] - tr; im[v] = im[u] - ti; re[u] += tr; im[u] += ti;
                    double t = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = t;
                }
            }
        }
    }

    /// <summary>Average power spectrum (Hann, 4096, 50% overlap). Returns bins 0..2048.</summary>
    public static double[] Spectrum(float[] x)
    {
        const int NF = 4096; var acc = new double[NF / 2 + 1]; var re = new double[NF]; var im = new double[NF];
        int frames = 0, maxFrames = 400, hop = NF / 2; int total = Math.Max(1, (x.Length - NF) / hop + 1); int step = Math.Max(1, total / maxFrames);
        for (int f = 0; f < total; f += step)
        {
            int st = f * hop; Array.Clear(im);
            for (int i = 0; i < NF; i++) { double v = st + i < x.Length ? x[st + i] : 0; re[i] = v * (0.5 - 0.5 * Math.Cos(A.TAU * i / NF)); }
            Fft(re, im);
            for (int k = 0; k <= NF / 2; k++) acc[k] += re[k] * re[k] + im[k] * im[k];
            frames++;
        }
        for (int k = 0; k < acc.Length; k++) acc[k] /= Math.Max(1, frames);
        return acc;
    }
    public static double Centroid(float[] x)
    {
        var p = Spectrum(x); double num = 0, den = 0;
        for (int k = 1; k < p.Length; k++) { double f = k * (double)A.SR / 4096; num += f * p[k]; den += p[k]; }
        return den > 0 ? num / den : 0;
    }
    /// <summary>Fraction of spectral energy in [f0,f1].</summary>
    public static double BandShare(float[] x, double f0, double f1)
    {
        var p = Spectrum(x); double num = 0, den = 0;
        for (int k = 1; k < p.Length; k++) { double f = k * (double)A.SR / 4096; den += p[k]; if (f >= f0 && f <= f1) num += p[k]; }
        return den > 0 ? num / den : 0;
    }

    public static Stats Measure(float[] l, float[] r, bool loop)
    {
        var s = new Stats { Channels = r == null ? 1 : 2, Samples = l.Length, Duration = l.Length / (double)A.SR };
        double pk = 0, sq = 0, dc = 0; int clips = 0; long cnt = 0;
        foreach (var ch in r == null ? new[] { l } : new[] { l, r })
            foreach (var v in ch) { double a = Math.Abs(v); if (a > pk) pk = a; sq += (double)v * v; dc += v; if (a >= 0.99997) clips++; cnt++; }
        s.PeakDb = A.ToDb(pk); s.RmsDb = A.ToDb(Math.Sqrt(sq / Math.Max(1, cnt))); s.Dc = dc / Math.Max(1, cnt); s.Clips = clips;
        s.Lufs = Lufs(l, r, loop);
        var mono = r == null ? l : new St(l, r).Mono();
        s.Centroid = Centroid(mono);
        // leading silence: first sample above -50 dB re peak
        double thr = pk * A.Db(-50); int first = 0; while (first < mono.Length && Math.Abs(l[first]) < thr && (r == null || Math.Abs(r[first]) < thr)) first++;
        s.LeadSilenceMs = first * 1000.0 / A.SR;
        // level of the final 2 ms relative to peak (non-loops should have faded out)
        int tn = Math.Min(mono.Length, 88); double tp = 0; for (int i = mono.Length - tn; i < mono.Length; i++) { tp = Math.Max(tp, Math.Abs(l[i])); if (r != null) tp = Math.Max(tp, Math.Abs(r[i])); }
        s.TailDb = A.ToDb(tp) - s.PeakDb;
        // loop seam: jump across the wrap vs. the typical sample-to-sample step near the seam; and level difference of 50 ms either side
        (s.SeamRatio, s.SeamLevelDb) = Seam(l, r);
        return s;
    }

    /// <summary>Returns (seam jump / 99.9th percentile of local adjacent-sample steps, |level(end 50ms) - level(start 50ms)| dB).</summary>
    public static (double ratio, double levelDb) Seam(float[] l, float[] r)
    {
        int n = l.Length; if (n < 4000) return (0, 0);
        double worst = 0;
        foreach (var ch in r == null ? new[] { l } : new[] { l, r })
        {
            // second difference across the seam catches slope breaks as well as value jumps
            double jump = Math.Abs(ch[0] - ch[n - 1]);
            double bend = Math.Abs((ch[1] - ch[0]) - (ch[0] - ch[n - 1])) * 0.5 + Math.Abs((ch[0] - ch[n - 1]) - (ch[n - 1] - ch[n - 2])) * 0.5;
            int w = Math.Min(n / 2, 8820); var steps = new List<double>(w * 2);
            for (int i = 1; i < w; i++) { steps.Add(Math.Abs(ch[i] - ch[i - 1])); steps.Add(Math.Abs(ch[n - i] - ch[n - i - 1])); }
            steps.Sort(); double p999 = Math.Max(steps[(int)(steps.Count * 0.999)], 1e-7);
            var bends = new List<double>(w * 2);
            for (int i = 2; i < w; i++) { bends.Add(Math.Abs(ch[i] - 2 * ch[i - 1] + ch[i - 2])); bends.Add(Math.Abs(ch[n - i + 1] - 2 * ch[n - i] + ch[n - i - 1])); }
            bends.Sort(); double b999 = Math.Max(bends[(int)(bends.Count * 0.999)], 1e-7);
            worst = Math.Max(worst, Math.Max(jump / p999, bend / b999));
        }
        int k = Math.Min(n / 2, 2205); double ea = 0, eb = 0;
        for (int i = 0; i < k; i++) { ea += (double)l[i] * l[i]; eb += (double)l[n - 1 - i] * l[n - 1 - i]; if (r != null) { ea += (double)r[i] * r[i]; eb += (double)r[n - 1 - i] * r[n - 1 - i]; } }
        return (worst, Math.Abs(10 * Math.Log10((ea + 1e-12) / (eb + 1e-12))));
    }
}

public static class Master
{
    /// <summary>Scale to target integrated loudness. Returns applied gain (dB).</summary>
    public static double ToLufs(St s, double target, bool circular)
    {
        double cur = Meter.Lufs(s, circular), g = target - cur; if (cur < -69) return 0;
        s.Gain(g); return g;
    }

    /// <summary>Stereo-linked look-ahead peak limiter. circular=true treats the buffer as a loop so the gain curve is seamless.</summary>
    public static double Limit(St s, double ceilDb, bool circular, double lookMs = 5, double releaseMs = 120)
    {
        int n = s.Len; double ceil = A.Db(ceilDb);
        var g = new float[n]; bool any = false;
        for (int i = 0; i < n; i++) { double a = Math.Max(Math.Abs(s.L[i]), Math.Abs(s.R[i])); if (a > ceil) { g[i] = (float)(ceil / a); any = true; } else g[i] = 1; }
        if (!any) return 0;
        return ApplyGain(g, s, circular, lookMs, releaseMs);
    }

    /// <summary>Limits layer c so that fixedLayer + c never exceeds the ceiling; fixedLayer is left untouched. Used for the adaptive
    /// music pairs: the exploration track keeps its own dynamics and the combat layer gives way for the few milliseconds where
    /// both peak together. Returns the largest reduction applied to c (dB).</summary>
    public static double LimitAgainst(St fixedLayer, St c, double ceilDb, bool circular, double lookMs = 5, double releaseMs = 120)
    {
        int n = c.Len; double ceil = A.Db(ceilDb); if (fixedLayer.Len != n) throw new ArgumentException("layers differ in length");
        var g = new float[n]; bool any = false;
        for (int i = 0; i < n; i++)
        {
            double need = 1;
            for (int ch = 0; ch < 2; ch++)
            {
                double b = ch == 0 ? fixedLayer.L[i] : fixedLayer.R[i], x = ch == 0 ? c.L[i] : c.R[i];
                if (Math.Abs(b + x) <= ceil) continue;
                need = Math.Min(need, Math.Max(0, (ceil - Math.Abs(b)) / Math.Max(Math.Abs(x), 1e-9)));   // |b + g x| <= |b| + g|x| = ceil
            }
            g[i] = (float)need; if (need < 1) any = true;
        }
        if (!any) return 0;
        return ApplyGain(g, c, circular, lookMs, releaseMs);
    }

    /// <summary>Soft clip: linear up to 60 % of the cap, then a tanh knee that never passes the cap. For percussion stems, where
    /// shaving the first millisecond of a hit is inaudible but saves the bus limiter from ducking everything else.</summary>
    public static double SoftClip(St s, double capDb)
    {
        double cap = A.Db(capDb), k = 0.6 * cap, span = cap - k, before = s.Peak(); if (before <= k) return 0;
        float F(float x) { double a = Math.Abs(x); return a <= k ? x : (float)(Math.Sign(x) * (k + span * Math.Tanh((a - k) / span))); }
        for (int i = 0; i < s.Len; i++) { s.L[i] = F(s.L[i]); s.R[i] = F(s.R[i]); }
        return A.ToDb(before) - A.ToDb(s.Peak());
    }

    /// <summary>Smooths a per-sample required-gain curve (look-ahead minimum, slow recovery, short averaging ramp) and applies it.
    /// The applied gain is never above the required gain at any sample.</summary>
    [ThreadStatic] public static double LastOver1, LastOver3;   // share of samples the last ApplyGain reduced by more than 1 dB / 3 dB
    static double ApplyGain(float[] g, St s, bool circular, double lookMs, double releaseMs)
    {
        long o1 = 0, o3 = 0;
        int n = s.Len, w = Math.Max(2, (int)(lookMs * A.SR / 1000));
        // sliding minimum over [i, i+w) (look-ahead), circular if requested
        var m = new float[n]; var ge = new float[n + w];
        for (int j = 0; j < n + w; j++) ge[j] = circular ? g[j % n] : (j < n ? g[j] : 1f);
        var dq = new int[n + w]; int head = 0, tail = 0;
        for (int j = 0; j < n + w - 1; j++)
        {
            while (tail > head && ge[dq[tail - 1]] >= ge[j]) tail--;
            dq[tail++] = j;
            int i = j - w + 1;
            if (i >= 0) { while (dq[head] < i) head++; m[i] = ge[dq[head]]; }
        }
        // release smoothing (gain may only recover slowly), then moving average over w (attack ramp)
        double k = 1 - Math.Exp(-1.0 / (releaseMs * A.SR / 1000)); var rel = new float[n]; double cur = 1;
        int passes = circular ? 2 : 1;
        for (int p = 0; p < passes; p++) for (int i = 0; i < n; i++) { cur = Math.Min(m[i], cur + (1 - cur) * k); rel[i] = (float)cur; }
        // m[i] = min g over [i, i+w): gain applied at i is mean of rel over [i-w+1, i] => every term <= g[i]
        var cs = new double[n * 2 + 1]; for (int i = 0; i < 2 * n; i++) cs[i + 1] = cs[i] + rel[i % n];
        double maxRed = 0;
        for (int i = 0; i < n; i++)
        {
            double avg;
            if (circular) { int hi = i + n + 1, lo = hi - w; avg = (cs[hi] - cs[lo]) / w; }
            else { int lo = Math.Max(0, i - w + 1); avg = (cs[i + 1] - cs[lo]) / (i + 1 - lo); }
            s.L[i] = (float)(s.L[i] * avg); s.R[i] = (float)(s.R[i] * avg); maxRed = Math.Max(maxRed, -A.ToDb(avg));
            if (avg < 0.8913) o1++; if (avg < 0.7079) o3++;
        }
        LastOver1 = o1 / (double)n; LastOver3 = o3 / (double)n;
        return maxRed;
    }
}
