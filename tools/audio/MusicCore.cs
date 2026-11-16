// Music engine: a small composing DSL (Part.Play), SoundFont rendering through MeltySynth with per-note
// expression/vibrato curves, blind mixing by measured stem loudness, FDN hall reverb, loop-wrapped tails, mastering.
using MeltySynth;
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public sealed class Nt { public double B, D; public int P; public double V; }

public sealed class Part
{
    public string Name; public List<Nt> Notes = new();
    public int Prog = -1, Bank = 0; public bool Drums;
    public Func<Part, Score, St> Synth;
    /// <summary>Stem loudness target (LUFS, gated) before the master bus; this is how the mix is balanced without ears.</summary>
    public double Lufs = -26;
    public double Pan = 0, Send = -8, Hp = 0, Lp = 0;
    public double TimeJit = 0.007, VelJit = 5, Legato = 0, StaccSec = 0;
    /// <summary>Per-note CC11 arch in dB: start, peak, end and peak position (0..1). Null = flat.</summary>
    public (double s, double p, double e, double pos)? Swell;
    public double Vib = 0; public bool Poly = false; public int Transpose = 0;
    /// <summary>Largest allowed crest factor of the stem (peak dBFS minus stem LUFS); above it the stem is soft-clipped. 0 = off.</summary>
    public double Crest = 0;
    double cur, vel = 82, lastDur = 1; public Score Sc;

    public Part At(double bar, double beat = 0) { cur = (bar - 1) * Sc.BeatsPerBar + beat; return this; }
    public Part Vel(double v) { vel = v; return this; }
    static readonly Dictionary<string, double> Dyn = new() { ["ppp"] = 28, ["pp"] = 40, ["p"] = 54, ["mp"] = 68, ["mf"] = 82, ["f"] = 98, ["ff"] = 112, ["fff"] = 124 };
    public static int Pitch(string s)
    {
        if (string.IsNullOrEmpty(s)) throw new FormatException("empty note name");
        int i = 0; int pc = "c d ef g a b".IndexOf(char.ToLowerInvariant(s[i++])); if (pc < 0 || s[0] == ' ') throw new FormatException($"bad note name '{s}' (expected e.g. d3, f#4, bb2)");
        while (i < s.Length && (s[i] == '#' || s[i] == 'b')) { pc += s[i] == '#' ? 1 : -1; i++; }
        if (!int.TryParse(s.AsSpan(i), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out int oct))
            throw new FormatException($"bad note name '{s}': octave '{s[i..]}' is not a number");
        return 12 * (oct + 1) + pc;
    }
    static double Num(string s, string tok)
    {
        if (!double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v)) throw new FormatException($"bad number '{s}' in token '{tok}'");
        return v;
    }
    /// <summary>Tokens: "!mf" dynamics, "|" bar line (ignored), "r:2" rest, "d3:1.5" note, "d2+a2+d3:4" chord.
    /// Suffixes after the duration: ^ accent, - soft, ' staccato (half length), _ tenuto (overlap).</summary>
    public Part Play(string src)
    {
        foreach (var tok0 in src.Replace("|", " | ").Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var tok = tok0;
            try
            {
                if (tok == "|")
                {
                    double x = cur / Sc.BeatsPerBar;
                    if (Math.Abs(x - Math.Round(x)) > 1e-6) throw new FormatException(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"bar line at beat {cur:F3} (bar {x + 1:F3}) is not on a bar boundary"));
                    continue;
                }
                if (tok[0] == '!')
                {
                    var d = tok[1..]; if (d.Length == 0) throw new FormatException("empty dynamic");
                    if (d[0] == 'v') vel = Num(d[1..], tok); else if (!Dyn.TryGetValue(d, out vel)) throw new FormatException($"unknown dynamic '{tok}'");
                    continue;
                }
                double v = vel, lenMul = 1;
                while (tok.Length > 0 && "^-'_".Contains(tok[^1])) { char c = tok[^1]; tok = tok[..^1]; if (c == '^') v += 15; else if (c == '-') v -= 15; else if (c == '\'') lenMul = 0.5; else lenMul = 1.08; }
                int ci = tok.IndexOf(':'); double dur = lastDur; string names = tok;
                if (ci >= 0) { dur = Num(tok[(ci + 1)..], tok0); names = tok[..ci]; }
                if (dur <= 0) throw new FormatException($"non-positive duration in token '{tok0}'");
                lastDur = dur;
                if (names != "r") foreach (var nm in names.Split('+')) Notes.Add(new Nt { B = cur, D = dur * lenMul, P = Pitch(nm), V = Clamp(v, 1, 127) });
                cur += dur;
            }
            catch (FormatException ex)
            {
                // re-throw with the score, part and place so a typo in the note data is easy to find
                throw new FormatException($"{Sc?.Name}/{Name}: {ex.Message} at token '{tok0}' in \"{(src.Length > 90 ? src[..90] + "..." : src)}\"", ex);
            }
        }
        return this;
    }
    public Part Note(double beat, double dur, int pitch, double v) { Notes.Add(new Nt { B = beat, D = dur, P = pitch, V = Clamp(v, 1, 127) }); return this; }
    /// <summary>Step pattern: 'X' accent, 'x' normal, 'o' soft, '.' rest. One char = step beats.</summary>
    public Part Hits(double bar, string pat, int pitch, double v = 90, double step = 0.25, double dur = 0.2)
    {
        double b0 = (bar - 1) * Sc.BeatsPerBar;
        for (int i = 0; i < pat.Length; i++) { char c = pat[i]; if (c == '.' || c == ' ') continue; Note(b0 + i * step, dur, pitch, c == 'X' ? v + 18 : c == 'o' ? v - 28 : v); }
        return this;
    }
}

public sealed class Score
{
    public string Name; public double Bpm; public int BeatsPerBar = 4, Bars; public bool Loop = true; public double Tail = 9, FadeOut = 0;
    public List<Part> Parts = new();
    public double RevTime = 3.8, RevSize = 1.0, RevDamp = 4500, RevPre = 0.02;
    public ulong Seed;
    public double Spb => 60.0 / Bpm;
    public int Len => (int)Math.Round(Bars * BeatsPerBar * 60.0 / Bpm * SR);
    public int Total => Len + N(Tail);
    public double Sec(double beat) => beat * Spb;
    public double BarBeat(double bar, double beat = 0) => (bar - 1) * BeatsPerBar + beat;

    public Score(string name, double bpm, int bars, int beatsPerBar = 4) { Name = name; Bpm = bpm; Bars = bars; BeatsPerBar = beatsPerBar; Seed = Rng.Hash("music/" + name); }
    Part Mk(string name, double lufs, double pan, double send) { var p = new Part { Name = name, Lufs = lufs, Pan = pan, Send = send, Sc = this }; Parts.Add(p); return p; }
    /// <summary>SoundFont instrument part.</summary>
    public Part Sf(string name, int prog, double lufs, double pan = 0, double send = -8, int bank = 0) { var p = Mk(name, lufs, pan, send); p.Prog = prog; p.Bank = bank; return p; }
    public Part Kit(string name, int kit, double lufs, double pan = 0, double send = -12) { var p = Mk(name, lufs, pan, send); p.Prog = kit; p.Drums = true; p.TimeJit = 0.004; return p; }
    /// <summary>Own-DSP instrument part.</summary>
    public Part Dsp(string name, Func<Part, Score, St> synth, double lufs, double pan = 0, double send = -8) { var p = Mk(name, lufs, pan, send); p.Synth = synth; return p; }
}

public static class Sf
{
    static SoundFont font; static readonly object lk = new();
    public static void Load()
    {
        lock (lk)
        {
            if (font != null) return;
            if (!File.Exists(Program.SfPath)) throw new FileNotFoundException("SoundFont missing: " + Program.SfPath);
            font = new SoundFont(Program.SfPath);
        }
    }
    public static void Info()
    {
        Load();
        foreach (var p in font.Presets.OrderBy(p => p.BankNumber).ThenBy(p => p.PatchNumber))
        {
            int lo = 127, hi = 0; foreach (var pr in p.Regions) foreach (var ir in pr.Instrument.Regions) { lo = Math.Min(lo, Math.Max(pr.KeyRangeStart, ir.KeyRangeStart)); hi = Math.Max(hi, Math.Min(pr.KeyRangeEnd, ir.KeyRangeEnd)); }
            Console.WriteLine($"{p.BankNumber,3}:{p.PatchNumber,3}  {p.Name,-24} keys {lo}-{hi}");
        }
    }
    /// <summary>Renders a 2 s test note for the presets the scores use and prints level / attack so silent or slow patches are caught.</summary>
    public static void Probe()
    {
        Load();
        var tests = new (int prog, int bank, bool drum, int key, string nm)[] {
            (42, 0, false, 50, "cello d3"), (42, 0, false, 38, "cello d2"), (41, 0, false, 62, "viola d4"), (40, 0, false, 74, "violin d5"), (43, 0, false, 31, "contrabass g1"), (48, 0, false, 55, "strings fast g3"),
            (49, 0, false, 55, "strings slow g3"), (44, 0, false, 62, "tremolo d4"), (45, 0, false, 50, "pizz d3"), (46, 0, false, 62, "harp d4"), (47, 0, false, 43, "timpani g2"), (52, 0, false, 55, "choir aahs g3"),
            (53, 0, false, 62, "voice oohs d4"), (14, 0, false, 62, "tubular bell d4"), (116, 0, false, 40, "taiko e2"), (24, 0, false, 59, "nylon b3"), (25, 0, false, 66, "steel f#4"), (8, 0, false, 84, "celesta c6"),
            (9, 0, false, 84, "glock c6"), (19, 0, false, 55, "church organ g3"), (68, 0, false, 62, "oboe d4"), (69, 0, false, 61, "english horn c#4"), (70, 0, false, 46, "bassoon bb2"), (71, 0, false, 53, "clarinet f3"),
            (15, 0, false, 66, "dulcimer f#4"), (107, 0, false, 66, "koto f#4"), (48, 0, true, 36, "orch kit bd"), (48, 0, true, 38, "orch kit snare"), (48, 0, true, 57, "orch kit cymbal"), (0, 0, true, 41, "std kit floor tom"),
            (0, 0, true, 60, "std kit hi bongo"), (0, 0, true, 54, "std kit tambourine"), (117, 0, false, 45, "melodic tom a2"), (32, 0, false, 35, "acoustic bass b1"),
        };
        foreach (var t in tests)
        {
            var sc = new Score("probe", 60, 3); var p = t.drum ? sc.Kit("p", t.prog, -20) : sc.Sf("p", t.prog, -20, 0, -8, t.bank); p.TimeJit = 0; p.VelJit = 0; p.Note(0, 2, t.key, 90);
            var st = RenderPart(p, sc, new Rng(1)); var m = st.Mono(); double pk = m.Peak(); int at = 0; while (at < m.Length && Math.Abs(m[at]) < pk * 0.5) at++;
            double r1 = m.AsSpan(0, SR).ToArray().Rms(), r2 = m.AsSpan(SR, SR).ToArray().Rms(), r3 = m.AsSpan(2 * SR + SR / 4, SR / 2).ToArray().Rms();
            double W(double a, double b) => ToDb(m.AsSpan((int)(a * SR), (int)((b - a) * SR)).ToArray().Rms());
            Console.WriteLine($"{t.nm,-20} peak {ToDb(pk),6:F1} dB  rms 0-30ms {W(0, 0.03),6:F1}  30-60 {W(0.03, 0.06),6:F1}  60-120 {W(0.06, 0.12),6:F1}  120-250 {W(0.12, 0.25),6:F1}  .25-1s {W(0.25, 1),6:F1}  1-2s {ToDb(r2),6:F1}  after-off {ToDb(r3),6:F1}  centroid {Meter.Centroid(m),6:F0} Hz");
        }
    }

    public static St RenderPart(Part p, Score sc, Rng rng)
    {
        Load();
        int total = sc.Total; var l = new float[total]; var r = new float[total];
        var synth = new Synthesizer(font, new SynthesizerSettings(SR) { EnableReverbAndChorus = false, MaximumPolyphony = 128 }) { MasterVolume = 1.0f };
        var ev = new List<(int smp, int ord, Action act)>();
        int[] chans = p.Drums ? new[] { 9 } : p.Poly ? Enumerable.Range(0, 16).Where(c => c != 9).ToArray() : new[] { 0 };
        foreach (int c in chans)
        {
            int ch = c;
            ev.Add((0, 0, () =>
            {
                if (!p.Drums) synth.ProcessMidiMessage(ch, 0xB0, 0, p.Bank);
                synth.ProcessMidiMessage(ch, 0xC0, p.Prog, 0);
                synth.ProcessMidiMessage(ch, 0xB0, 7, 110); synth.ProcessMidiMessage(ch, 0xB0, 10, 64); synth.ProcessMidiMessage(ch, 0xB0, 11, 127);
                synth.ProcessMidiMessage(ch, 0xB0, 91, 0); synth.ProcessMidiMessage(ch, 0xB0, 93, 0); synth.ProcessMidiMessage(ch, 0xB0, 1, 0);
            }
            ));
        }
        var freeAt = new double[16];
        foreach (var n in p.Notes.OrderBy(n => n.B).ThenBy(n => n.P))
        {
            double tOn = Math.Max(0, sc.Sec(n.B) + Clamp(rng.N() * p.TimeJit, -2.5 * p.TimeJit, 2.5 * p.TimeJit));
            double dur = p.StaccSec > 0 ? Math.Min(p.StaccSec, sc.Sec(n.D)) : sc.Sec(n.D) + p.Legato;
            double tOff = tOn + Math.Max(0.03, dur);
            int vel = (int)Clamp(Math.Round(n.V + rng.N() * p.VelJit), 1, 127), key = n.P + p.Transpose, ch = chans[0];
            if (p.Poly)
            {
                ch = -1; double best = double.MaxValue;
                foreach (int c in chans) { if (freeAt[c] <= tOn - 0.05) { ch = c; break; } if (freeAt[c] < best) { best = freeAt[c]; ch = c; } }
                freeAt[ch] = tOff + 0.8;
            }
            int cch = ch;
            if (p.Swell is { } sw)
            {
                double pos = Clamp(sw.pos, 0.05, 0.95), nd = tOff - tOn;
                for (double t = -0.012; t <= nd + 0.06; t += 0.012)
                {
                    double x = Clamp(t / nd, 0, 1), db = x < pos ? Lerp(sw.s, sw.p, Smooth(x / pos)) : Lerp(sw.p, sw.e, Smooth((x - pos) / (1 - pos)));
                    int val = (int)Clamp(Math.Round(127 * Math.Pow(10, db / 40)), 1, 127); int smp = (int)((tOn + t) * SR);
                    ev.Add((Math.Max(0, smp), 1, () => synth.ProcessMidiMessage(cch, 0xB0, 11, val)));
                }
            }
            if (p.Vib > 0)
            {
                double nd = tOff - tOn; ev.Add((Math.Max(0, (int)((tOn - 0.01) * SR)), 1, () => synth.ProcessMidiMessage(cch, 0xB0, 1, 0)));
                if (nd > 0.45) for (double t = 0.25; t <= Math.Min(nd, 0.75); t += 0.05) { int val = (int)(p.Vib * Smooth((t - 0.25) / 0.5)); ev.Add(((int)((tOn + t) * SR), 1, () => synth.ProcessMidiMessage(cch, 0xB0, 1, val))); }
            }
            ev.Add(((int)(tOn * SR), 3, () => synth.NoteOn(cch, key, vel)));
            ev.Add(((int)(tOff * SR), 2, () => synth.NoteOff(cch, key)));
        }
        ev.Sort((a, b) => a.smp != b.smp ? a.smp.CompareTo(b.smp) : a.ord.CompareTo(b.ord));
        int posS = 0, ei = 0;
        while (posS < total)
        {
            while (ei < ev.Count && ev[ei].smp <= posS) ev[ei++].act();
            int next = ei < ev.Count ? Math.Min(total, ev[ei].smp) : total; if (next <= posS) next = Math.Min(total, posS + 1);
            synth.Render(l.AsSpan(posS, next - posS), r.AsSpan(posS, next - posS)); posS = next;
        }
        return new St(l, r);
    }
}

/// <summary>Own-DSP instruments. Each returns a stereo stem of sc.Total samples.</summary>
public static class Syn
{
    static IEnumerable<(double t, double d, double f, double v, Nt n)> Ev(Part p, Score sc)
        => p.Notes.Select(n => (sc.Sec(n.B), sc.Sec(n.D), Mtof(n.P + p.Transpose), n.V / 127.0, n));
    static Rng R(Part p, Score sc) => new Rng(sc.Seed ^ Rng.Hash(p.Name));

    /// <summary>Formant choir pad: several glottal voices per note through slowly morphing vowels.</summary>
    public static Func<Part, Score, St> Choir(Vowel a, Vowel b, int voices = 3, double scale = 1.0, double detuneCents = 9, double atk = 0.9, double rel = 1.6, double breath = 0.07)
        => (p, sc) =>
        {
            var st = new St(sc.Total); var r = R(p, sc);
            foreach (var (t, d, f, v, _) in Ev(p, sc))
            {
                double a1 = Math.Min(atk, d * 0.45), dd = d + rel; Fn env = PtsS(0, 0, a1, 1, d, 0.9, dd, 0);
                for (int k = 0; k < voices; k++)
                {
                    double det = Math.Pow(2, r.R(-detuneCents, detuneCents) / 1200), vr = r.R(4.6, 5.8), vd = r.R(0.003, 0.006), sc2 = scale * (f > 300 ? 1.12 : 1) * r.R(0.97, 1.03);
                    var vo = Sfx.Voice(r, dd, tt => f * det * (1 + vd * Smooth(tt / 0.8) * Math.Sin(TAU * vr * tt)), new[] { (0.0, a), (dd * 0.5, b), (dd, a) }, sc2, breath, 0.006, 0.04, 0, 40, 0, 2200).Env(env);
                    st.Raw(vo, ToDb(v * v) - 6, t, voices == 1 ? 0 : -0.7 + 1.4 * k / (voices - 1));
                }
            }
            return st.Resize(sc.Total);
        };

    /// <summary>Detuned saw drone through a slowly breathing low-pass, plus sub sine. Long fades (wrap makes it continuous).</summary>
    public static Func<Part, Score, St> Drone(double cutLo = 180, double cutHi = 520, double detuneCents = 8, double sub = 0.5, double fade = 4.0, int saws = 6)
        => (p, sc) =>
        {
            var st = new St(sc.Total); var r = R(p, sc);
            foreach (var (t, d, f, v, _) in Ev(p, sc))
            {
                // equal-power fades: when the release wraps onto the next loop's attack the drone level stays constant
                double a1 = Math.Min(fade, d * 0.45), dd = d + fade;
                Fn env = tt => tt < a1 ? Math.Sin(Math.PI / 2 * tt / a1) : tt < d ? 1 : tt < dd ? Math.Cos(Math.PI / 2 * (tt - d) / fade) : 0;
                for (int side = 0; side < 2; side++)
                {
                    var m = new float[N(dd)];
                    for (int k = 0; k < saws / 2; k++) { var s = Saw(dd, f * Math.Pow(2, r.R(-detuneCents, detuneCents) / 1200), r.U()); for (int i = 0; i < m.Length; i++) m[i] += s[i]; }
                    var cut = Wander(r, cutLo, cutHi, 0.12, dd, true);
                    var y = m.SvfLP(cut, 0.9).Env(env);
                    st.Raw(y, ToDb(v * v) - 12, t, side == 0 ? -0.75 : 0.75);
                }
                if (sub > 0) st.Raw(Sine(dd, f * 0.5).Env(env), ToDb(v * v * sub) - 6, t, 0);
            }
            return st.Resize(sc.Total);
        };

    /// <summary>Bowed metal / glass: inharmonic partials with slow independent swells and a thread of bow noise.</summary>
    public static Func<Part, Score, St> Bowed(double[] ratios, double atk = 2.0, double rel = 3.0, double noise = 0.25, double rough = 0.5)
        => (p, sc) =>
        {
            var st = new St(sc.Total); var r = R(p, sc);
            foreach (var (t, d, f, v, _) in Ev(p, sc))
            {
                double a1 = Math.Min(atk, d * 0.5), dd = d + rel; Fn env = PtsS(0, 0, a1, 1, d, 0.8, dd, 0);
                for (int k = 0; k < ratios.Length; k++)
                {
                    double fk = f * ratios[k] * r.R(0.997, 1.003); if (fk > 15000) continue;
                    var w = Wander(r, 1 - rough, 1, r.R(0.15, 0.6), dd); double amp = Math.Pow(0.72, k), ph = r.U();
                    var s = Sine(dd, tt => fk * (1 + 0.0015 * Math.Sin(TAU * (0.3 * tt + ph)))).Env(tt => env(tt) * w(tt));
                    double pan = r.R(-0.8, 0.8);
                    st.Raw(s, ToDb(v * v * amp) - 10, t, pan);
                    if (noise > 0) st.Raw(White(r, dd).BP(fk, 60).Env(tt => env(tt) * w(tt)), ToDb(v * v * amp * noise) + 14, t, -pan);
                }
            }
            return st.Resize(sc.Total);
        };

    /// <summary>Soft additive organ pad (drawbar-like harmonics with a slow chorus).</summary>
    public static Func<Part, Score, St> Organ(double[] bars = null, double atk = 0.25, double rel = 0.9)
        => (p, sc) =>
        {
            bars ??= new[] { 1.0, 0.7, 0.35, 0.5, 0.0, 0.25, 0.0, 0.18 }; var st = new St(sc.Total); var r = R(p, sc);
            foreach (var (t, d, f, v, _) in Ev(p, sc))
            {
                double dd = d + rel; Fn env = PtsS(0, 0, Math.Min(atk, d * 0.4), 1, d, 1, dd, 0);
                for (int side = 0; side < 2; side++)
                {
                    var parts = new List<(double, double)>(); double det = side == 0 ? 0.9985 : 1.0015;
                    for (int k = 0; k < bars.Length; k++) if (bars[k] > 0) parts.Add((f * (k + 1) * det, bars[k]));
                    st.Raw(Sfx.Tones(r, dd, parts, 0.06).Env(env), ToDb(v * v) - 14, t, side == 0 ? -0.6 : 0.6);
                }
            }
            return st.Resize(sc.Total);
        };

    /// <summary>Short filtered saw/square pluck for ostinato definition.</summary>
    public static Func<Part, Score, St> Pulse(double cut = 900, double len = 0.16)
        => (p, sc) =>
        {
            var st = new St(sc.Total); var r = R(p, sc);
            foreach (var (t, d, f, v, _) in Ev(p, sc))
            {
                double dd = Math.Min(len, d) + 0.05;
                var y = Mx().Add(Saw(dd, f)).Add(Square(dd, f * 0.5, 0.4), -4).Out().SvfLP(Exp(cut * (0.6 + v), cut * 0.25, dd), 1.4).Env(AD(0.003, dd));
                st.Raw(y, ToDb(v * v) - 8, Math.Max(0, t + r.N() * 0.003), 0);
            }
            return st.Resize(sc.Total);
        };

    /// <summary>Synth percussion. Pitch selects the drum: 36 big drum, 38 frame low (dum), 40 frame high (tek), 42 tick/shaker, 44 metal anvil,
    /// 46 sub boom, 48 wet thud, 50 wet drop, 52 riser (duration = note length), 54 clap-like slap.</summary>
    public static Func<Part, Score, St> Drums()
        => (p, sc) =>
        {
            var st = new St(sc.Total); var r = R(p, sc);
            foreach (var (t, d, _, v, n) in Ev(p, sc))
            {
                float[] y; double pan = 0, db = ToDb(v * v);
                switch (n.P)
                {
                    case 36:
                        y = Mx().Add(Thump(r.R(96, 104), 46, 0.07, 0.75), 0).Add(Modes(0.5, new[] { (72.0, 0.5, 1.0), (115.0, 0.35, 0.6), (154.0, 0.25, 0.4), (204.0, 0.2, 0.25) }), -5)
                            .Add(Click(r, 2200, 0.8, 0.012), -9 + 6 * v).Add(Body(r, 180, 0.9, 0.12), -6).Out(); break;
                    case 38:
                        y = Mx().Add(Thump(r.R(118, 126), 82, 0.03, 0.4), 0).Add(Modes(0.35, new[] { (95.0, 0.3, 1.0), (151.0, 0.22, 0.5), (203.0, 0.16, 0.3) }), -5)
                            .Add(Click(r, 1500, 0.8, 0.01), -14).Out(); pan = -0.15; break;
                    case 40:
                        y = Mx().Add(Modes(0.2, new[] { (r.R(300, 320), 0.12, 1.0), (478.0, 0.09, 0.6), (700.0, 0.06, 0.4) }), 0).Add(Click(r, 2800, 1, 0.006), -5)
                            .Add(Body(r, 900, 1.5, 0.03), -8).Out(); pan = 0.2; db -= 3; break;
                    case 42: y = White(r, 0.05).HP(5500).Env(AD(0.001, 0.035)); pan = 0.35; db -= 8; break;
                    case 44:
                        y = Mx().Add(Sfx.Metal(r, r.R(480, 520), 0.5, 9), 0).Add(Sfx.Metal(r, 1700, 0.3, 5, RBar), -8).Add(Click(r, 4000, 0.8, 0.005), -4).Out(); pan = -0.3; db -= 3; break;
                    case 46: y = Mx().Add(Thump(78, 30, 0.2, 1.6), 0).Add(Brown(r, 0.6).LP(90).Env(AD(0.01, 0.5)), -8).Out(); break;
                    case 48:
                        y = Mx().Add(Thump(105, 60, 0.04, 0.3), 0).Add(Squelch(r, 0.1, 300, 1800, 50, 4).Env(AD(0.004, 0.09)), -8).Add(Body(r, 240, 1.2, 0.07), -5).Out(); pan = -0.2; break;
                    case 50: y = Mx().Add(Sfx.Bubble(r, r.X(380, 900), r.R(0.04, 0.07), 1.7), 0).Add(Click(r, 3000, 2, 0.003), -12).Out(); pan = r.R(-0.6, 0.6); db -= 4; break;
                    case 52:
                        { double dd = Math.Max(0.4, d); y = Mx().Add(White(r, dd).SvfBP(PtsE(0, 500, dd, 7000), 1.5).Env(tt => Math.Pow(tt / dd, 2.2)), 0).Add(Pink(r, dd).LP(400).Env(tt => Math.Pow(tt / dd, 2)), -8).Out().Fade(0, 0.02); db -= 4; break; }
                    default: y = Mx().Add(White(r, 0.06).BP(1500, 0.8).Env(AD(0.001, 0.05)), 0).Add(Body(r, 500, 1.5, 0.04), -4).Out(); pan = 0.25; db -= 4; break;
                }
                st.Add(y, db - 6, Math.Max(0, t + r.N() * 0.003), pan);
            }
            return st.Resize(sc.Total);
        };

    /// <summary>Continuous stereo noise bed (wind / rumble) that is loop-seamless by cross-fade.</summary>
    public static Func<Part, Score, St> Bed(Func<Rng, double, float[]> gen, double xfade = 6)
        => (p, sc) =>
        {
            var r = R(p, sc); double d = sc.Len / (double)SR;
            if (!sc.Loop) { var a = gen(r.Fork(), d).Fade(4, 4); var b = gen(r.Fork(), d).Fade(4, 4); return new St(a, b).Resize(sc.Total); }
            var l = gen(r.Fork(), d + xfade).Len(sc.Len + N(xfade)).LoopXfade(sc.Len); var rr = gen(r.Fork(), d + xfade).Len(sc.Len + N(xfade)).LoopXfade(sc.Len);
            return new St(l, rr).Resize(sc.Total);
        };

    /// <summary>Glassy sustained tone (sine + soft harmonics, slow attack, chorus by detuned twin).</summary>
    public static Func<Part, Score, St> Glass(double atk = 1.2, double rel = 2.5)
        => (p, sc) =>
        {
            var st = new St(sc.Total); var r = R(p, sc);
            foreach (var (t, d, f, v, _) in Ev(p, sc))
            {
                double dd = d + rel; Fn env = PtsS(0, 0, Math.Min(atk, d * 0.5), 1, d, 0.85, dd, 0);
                for (int side = 0; side < 2; side++)
                {
                    double det = side == 0 ? 0.9988 : 1.0012;
                    st.Raw(Sfx.Tones(r, dd, new[] { (f * det, 1.0), (f * 2 * det, 0.22), (f * 3.01 * det, 0.08), (f * 4.2 * det, 0.03) }, 0.35).Env(env), ToDb(v * v) - 10, t, side == 0 ? -0.6 : 0.6);
                }
            }
            return st.Resize(sc.Total);
        };

    /// <summary>Hollow breathy pipe pad (band-passed noise on a harmonic stack).</summary>
    public static Func<Part, Score, St> Breath(double q = 40, double atk = 1.5, double rel = 2.5)
        => (p, sc) =>
        {
            var st = new St(sc.Total); var r = R(p, sc);
            foreach (var (t, d, f, v, _) in Ev(p, sc))
            {
                double dd = d + rel; Fn env = PtsS(0, 0, Math.Min(atk, d * 0.5), 1, d, 0.85, dd, 0);
                for (int side = 0; side < 2; side++)
                    st.Raw(White(r, dd).Bank((f, q, 1), (f * 2, q, 0.5), (f * 3, q, 0.35), (f * 4, q, 0.15), (f * 5, q, 0.1)).Env(env), ToDb(v * v) + 6, t, side == 0 ? -0.5 : 0.5);
            }
            return st.Resize(sc.Total);
        };

    /// <summary>Deep struck bell with inharmonic church-bell partials (for tolls).</summary>
    public static Func<Part, Score, St> Bell(double t60 = 6, double bright = 0.7)
        => (p, sc) =>
        {
            var st = new St(sc.Total); var r = R(p, sc);
            foreach (var (t, d, f, v, _) in Ev(p, sc)) st.Raw(Mx().Add(Sfx.BellTone(r, f, t60, RBell, bright), 0).Add(Click(r, Math.Min(4000, f * 8), 0.8, 0.01), -14).Out(), ToDb(v * v) - 6, t, r.R(-0.2, 0.2));
            return st.Resize(sc.Total);
        };
}

public sealed class MusicDef
{
    public string Key, Desc, KeyName, Meter = "4/4"; public string[] Outputs; public bool Loop = true; public double Bpm, MinSec, MaxSec; public int Bars, Samples;
    public Func<List<(string, St)>> Render;
    /// <summary>Score builders (one, or base + combat) so the note data can be inspected without rendering.</summary>
    public Func<Score>[] Builders;
    public double Lufs(string name) => name.EndsWith("_combat") ? -19 : -18;
}

public static partial class Music
{
    public static readonly List<MusicDef> All = new();
    /// <summary>Pieces whose note data failed validation (bad token, bar line off the grid, pair mismatch). They are skipped, not fatal.</summary>
    public static readonly List<(string piece, Exception err)> BuildErrors = new();
    static bool built;
    public static void Build() { if (built) return; built = true; Pieces1(); Zones1(); Zones2(); }

    static St FitBus(St s, int n) => s.Len == n ? s : s.Resize(n);

    /// <summary>Renders all parts to stems, balances by loudness, adds hall reverb, wraps the tail for loops. Returns un-mastered mix (Len samples for loops).</summary>
    public static St Mix(Score sc, bool log = true)
    {
        int total = sc.Total; var dry = new St(total); var send = new St(total); var info = new List<string>();
        var stems = new St[sc.Parts.Count];
        Parallel.For(0, sc.Parts.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            var p = sc.Parts[i]; if (p.Notes.Count == 0 && p.Prog >= 0) { stems[i] = null; return; }
            var rng = new Rng(sc.Seed ^ Rng.Hash("part/" + p.Name));
            var st = p.Synth != null ? p.Synth(p, sc) : Sf.RenderPart(p, sc, rng);
            st = FitBus(st, total);
            if (p.Hp > 0) st = st.Map(x => x.HP(p.Hp)); if (p.Lp > 0) st = st.Map(x => x.LP(p.Lp));
            double lu = Meter.Lufs(st);
            if (lu < -65) { stems[i] = null; lock (info) info.Add($"    !! {sc.Name}/{p.Name}: SILENT stem"); return; }
            st.Gain(p.Lufs - lu);
            if (p.Crest > 0) Master.SoftClip(st, p.Lufs + p.Crest);
            if (p.Pan != 0) { double a = (p.Pan + 1) * Math.PI / 4; float gl = (float)(Math.Cos(a) * 1.4142), gr = (float)(Math.Sin(a) * 1.4142); for (int k = 0; k < total; k++) { st.L[k] *= gl; st.R[k] *= gr; } }
            stems[i] = st;
            lock (info) info.Add($"    {sc.Name}/{p.Name,-14} notes {p.Notes.Count,4}  raw {lu,6:F1} LUFS -> {p.Lufs,5:F1}  peak {ToDb(st.Peak()),6:F1} dB");
        });
        for (int i = 0; i < stems.Length; i++)
        {
            var st = stems[i]; if (st == null) continue; float sg = (float)Db(sc.Parts[i].Send);
            for (int k = 0; k < total; k++) { dry.L[k] += st.L[k]; dry.R[k] += st.R[k]; send.L[k] += st.L[k] * sg; send.R[k] += st.R[k] * sg; }
            stems[i] = null;
        }
        if (log) foreach (var s in info.OrderBy(x => x)) Program.Log(s);
        var (wl, wr) = Verb.Fdn(send.L, send.R, sc.RevTime, sc.RevSize, sc.RevDamp, 0.38, sc.RevPre, 140, 0.3);
        // calibrate the wet bus so "Send = 0 dB" means reverb as loud as its dry source
        double ls = Meter.Lufs(send), lw = Meter.Lufs(wl, wr); float wg = lw > -69 && ls > -69 ? (float)Db(ls - lw) : 0;
        for (int k = 0; k < total; k++) { dry.L[k] += wl[k] * wg; dry.R[k] += wr[k] * wg; }
        if (sc.Loop) return dry.LoopWrap(sc.Len);
        // one-shot: fade the tail
        int fo = N(sc.FadeOut > 0 ? sc.FadeOut : 3.0);
        for (int k = 0; k < fo; k++) { float g = (float)(0.5 - 0.5 * Math.Cos(Math.PI * k / fo)); dry.L[total - 1 - k] *= g; dry.R[total - 1 - k] *= g; }
        return dry;
    }

    static St HpLoop(St s, double f, bool loop)
    {
        float[] H(float[] x) { var b = Biquad.HP(f); if (loop) foreach (var v in x) b.Tick(v); var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) o[i] = (float)b.Tick(x[i]); return o; }
        return new St(H(s.L), H(s.R));
    }

    /// <summary>Loudness-normalise and limit. Iterates once so the post-limiter loudness lands on target.</summary>
    public static St MasterTo(St mix, double lufs, double ceilDb, bool loop, string name)
    {
        var s = HpLoop(mix, 28, loop); double red = 0;
        for (int it = 0; it < 3; it++) { Master.ToLufs(s, lufs, loop); red = Math.Max(red, Master.Limit(s, ceilDb, loop)); }
        Program.Log($"    master {name}: {Meter.Lufs(s, loop):F2} LUFS, peak {ToDb(s.Peak()):F2} dBFS, max limiting {red:F1} dB");
        return s;
    }

    static void AddSingle(string name, string desc, string key, double minSec, double maxSec, Func<Score> build, bool loop = true, string meter = "4/4")
    {
        Score probe; try { probe = build(); } catch (Exception ex) { BuildErrors.Add((name, ex)); return; }
        All.Add(new MusicDef
        {
            Key = name, Outputs = new[] { name }, Desc = desc, KeyName = key, Meter = meter, Loop = loop, Bpm = probe.Bpm, Bars = probe.Bars, MinSec = minSec, MaxSec = maxSec, Samples = loop ? probe.Len : probe.Total, Builders = new[] { build },
            Render = () => { var sc = build(); sc.Loop = loop; var m = MasterTo(Mix(sc), -18, -1.5, loop, name); return new List<(string, St)> { (name, m) }; }
        });
    }

    /// <summary>Adaptive pair: base + combat layer share tempo, bar count and key; mastered so their sum cannot clip.</summary>
    static void AddPair(string zone, string desc, string key, Func<Score> buildBase, Func<Score> buildCombat)
    {
        Score pb, pc;
        try
        {
            pb = buildBase(); pc = buildCombat();
            if (pb.Len != pc.Len || pb.Bpm != pc.Bpm || pb.BeatsPerBar != pc.BeatsPerBar || pb.Bars != pc.Bars) throw new InvalidOperationException($"{zone}: base/combat grid mismatch ({pb.Bars}x{pb.BeatsPerBar} @ {pb.Bpm} vs {pc.Bars}x{pc.BeatsPerBar} @ {pc.Bpm})");
        }
        catch (Exception ex) { BuildErrors.Add((zone, ex)); return; }
        All.Add(new MusicDef
        {
            Key = zone, Outputs = new[] { zone + "_base", zone + "_combat" }, Desc = desc, KeyName = key, Meter = pb.BeatsPerBar + "/4", Loop = true, Bpm = pb.Bpm, Bars = pb.Bars, MinSec = 120, MaxSec = 120, Samples = pb.Len, Builders = new[] { buildBase, buildCombat },
            Render = () =>
            {
                // Each layer is mastered on its own with moderate headroom; then the combat layer alone is limited against the
                // exploration track, so base + combat can never pass -1.3 dBFS (the margin covers Vorbis decode overshoot).
                var b = MasterTo(Mix(buildBase()), -18, -3.0, true, zone + "_base");
                var c = MasterTo(Mix(buildCombat()), -19, -3.5, true, zone + "_combat");
                if (b.Len != c.Len) throw new InvalidOperationException($"{zone}: base and combat rendered to different lengths ({b.Len} vs {c.Len})");
                double duck = 0, o1 = 0, o3 = 0;
                for (int it = 0; it < 3; it++) { Master.ToLufs(c, -19, true); Master.Limit(c, -3.5, true); double dk = Master.LimitAgainst(b, c, -1.3, true); if (it == 0) { duck = dk; o1 = dk > 0 ? Master.LastOver1 : 0; o3 = dk > 0 ? Master.LastOver3 : 0; } }
                double pk = 0; for (int i = 0; i < b.Len; i++) pk = Math.Max(pk, Math.Max(Math.Abs(b.L[i] + c.L[i]), Math.Abs(b.R[i] + c.R[i])));
                Program.Log($"    {zone}: combat {Meter.Lufs(c, true):F2} LUFS after pair limiting (max {duck:F1} dB against the base; reduced >1 dB for {o1 * 100:F2} % and >3 dB for {o3 * 100:F2} % of the time); base+combat sum peak {ToDb(pk):F2} dBFS");
                return new List<(string, St)> { (zone + "_base", b), (zone + "_combat", c) };
            }
        });
    }
}
