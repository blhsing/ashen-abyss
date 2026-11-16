// 16-line feedback delay network reverb (Hadamard feedback matrix, two-band absorption, input diffusion,
// light delay modulation). Wet only; callers mix dry themselves. Deterministic.
namespace AudioTool;

public static class Verb
{
    static readonly double[] BaseMs = { 31.3, 34.1, 37.3, 40.1, 43.7, 47.3, 51.1, 55.3, 59.9, 64.3, 69.1, 74.3, 79.9, 85.7, 91.3, 97.1 };
    static readonly double[] ApMsL = { 4.77, 3.59, 12.73, 9.31 }, ApMsR = { 5.03, 3.37, 11.97, 9.83 };

    static bool IsPrime(int n) { if (n < 2) return false; for (int i = 2; (long)i * i <= n; i++) if (n % i == 0) return false; return true; }
    static int NextPrime(int n) { while (!IsPrime(n)) n++; return n; }

    static float[] Diffuse(float[] x, double[] ms, double g, double scale)
    {
        var y = x;
        foreach (var m in ms) y = y.Allpass(m * scale / 1000.0, g);
        return y;
    }

    /// <param name="rt60">decay time (s) at low/mid frequencies</param>
    /// <param name="size">scales all delay lengths (1 = large hall, 0.4 = room)</param>
    /// <param name="dampHz">crossover above which decay is rt60*hfRatio</param>
    /// <param name="lowCutHz">high-pass on the reverb input (keeps sub out of the tail)</param>
    public static (float[] L, float[] R) Fdn(float[] inL, float[] inR, double rt60, double size = 1.0, double dampHz = 5000, double hfRatio = 0.4,
        double preDelay = 0.015, double lowCutHz = 120, double modMs = 0.25)
    {
        const int NL = 16;
        int n = inL.Length;
        var xl = Diffuse(inL.HP(lowCutHz), ApMsL, 0.62, Math.Max(0.5, size));
        var xr = Diffuse(inR.HP(lowCutHz), ApMsR, 0.62, Math.Max(0.5, size));
        int pre = (int)(preDelay * A.SR);

        var len = new int[NL]; var buf = new float[NL][]; var pos = new int[NL];
        var gL = new float[NL]; var gH = new float[NL]; var lp = new float[NL];
        int maxMod = (int)(modMs * A.SR / 1000) + 2;
        for (int i = 0; i < NL; i++)
        {
            len[i] = NextPrime((int)(BaseMs[i] * size * A.SR / 1000));
            buf[i] = new float[len[i] + maxMod + 2];
            gL[i] = (float)Math.Pow(10, -3.0 * len[i] / (rt60 * A.SR));
            gH[i] = (float)Math.Pow(10, -3.0 * len[i] / (rt60 * hfRatio * A.SR));
        }
        float c = (float)(1 - Math.Exp(-A.TAU * dampHz / A.SR));
        var oL = new float[n]; var oR = new float[n];
        var d = new float[NL];
        // modulated lines: every 4th line
        var modRate = new double[NL]; var modPh = new double[NL];
        for (int i = 0; i < NL; i++) { modRate[i] = 0.11 + 0.047 * i; modPh[i] = i * 0.37; }
        const float outNorm = 0.3536f; // 1/sqrt(8)
        for (int s = 0; s < n; s++)
        {
            float il = s >= pre ? xl[s - pre] : 0, ir = s >= pre ? xr[s - pre] : 0;
            float sl = 0, sr = 0;
            for (int i = 0; i < NL; i++)
            {
                var b = buf[i]; int bl = b.Length; float v;
                if ((i & 3) == 1 && modMs > 0)
                {
                    double dl = len[i] + maxMod * 0.5 * (1 + Math.Sin(A.TAU * (modRate[i] * s / A.SR + modPh[i]))) * (modMs * A.SR / 1000) / maxMod;
                    double rp = pos[i] - dl; while (rp < 0) rp += bl; int i0 = (int)rp; float fr = (float)(rp - i0); int i1 = i0 + 1 >= bl ? 0 : i0 + 1;
                    v = b[i0] * (1 - fr) + b[i1] * fr;
                }
                else { int rp = pos[i] - len[i]; if (rp < 0) rp += bl; v = b[rp]; }
                // output taps: even lines -> L, odd lines -> R, alternating sign
                float sg = ((i >> 1) & 1) == 0 ? 1f : -1f;
                if ((i & 1) == 0) sl += v * sg; else sr += v * sg;
                // absorption (two band)
                lp[i] += c * (v - lp[i]);
                d[i] = gH[i] * v + (gL[i] - gH[i]) * lp[i];
            }
            // fast Walsh-Hadamard transform (orthogonal after *0.25)
            for (int h = 1; h < NL; h <<= 1)
                for (int i = 0; i < NL; i += h << 1)
                    for (int j = i; j < i + h; j++) { float a = d[j], b2 = d[j + h]; d[j] = a + b2; d[j + h] = a - b2; }
            for (int i = 0; i < NL; i++)
            {
                float inj = (i & 1) == 0 ? il : ir; if (((i >> 2) & 1) == 1) inj = -inj;
                var b = buf[i]; b[pos[i]] = inj * 0.5f + d[i] * 0.25f + 1e-20f;
                if (++pos[i] >= b.Length) pos[i] = 0;
            }
            oL[s] = sl * outNorm; oR[s] = sr * outNorm;
        }
        return (oL, oR);
    }
}
