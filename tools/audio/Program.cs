// Ashen Abyss audio generator.
//   dotnet run -c Release --project Y:\tools\audio -- all            regenerate everything + manifest + analysis
//   ... -- sfx | amb | music                                          one category
//   ... -- hit_flesh wastes_base amb_crypt                            single items (pair members regenerate their pair)
//   ... -- verify                                                     re-measure files on disk, rewrite manifest + analysis only
//   ... -- list | sfinfo                                              list items / SoundFont presets
//   ... -- check                                                      build every score without rendering: note counts, pitch classes, base/combat bass comparison
//   ... -- selftest                                                   regression checks for the note parser and number handling
//   ... -- seam <file.ogg> ...                                        decoded length and the samples either side of a loop point
// Options: --root <dir> (default Y:\)   --jobs <n>
// Generation is CPU-heavy: run one category per invocation (sfx, then amb, then music), never several processes side by side.
// Exit codes: 0 ok, 1 bad arguments, 2 an item failed to generate or a score failed validation (details are printed in full).
// File names: sfx are always <group>_<n>.wav with n from 1; every run ends by re-measuring all files and rewriting
// game/assets/audio/manifest_audio.json and tools/audio/analysis.txt.
using System.Diagnostics;
using System.Text.Json;

namespace AudioTool;

public static class Program
{
    public static string Root = @"Y:\";
    public static string AudioDir => Path.Combine(Root, "game", "assets", "audio");
    public static string SfPath => Path.Combine(Root, "assets_src", "soundfont", "MuseScore_General.sf2");
    static readonly object logLock = new();
    public static void Log(string s) { lock (logLock) Console.WriteLine(s); }

    static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>Prints an exception with every inner exception and stack trace (Parallel loops wrap theirs in AggregateException).</summary>
    public static void PrintException(Exception ex, string what = "FATAL")
    {
        lock (logLock)
        {
            var list = ex is AggregateException ag ? ag.Flatten().InnerExceptions.ToList() : new List<Exception> { ex };
            foreach (var e in list)
            {
                Console.Error.WriteLine($"{what}: {e.GetType().FullName}: {e.Message}");
                Console.Error.WriteLine(e.ToString());
            }
            Console.Error.Flush();
        }
    }

    public static int Main(string[] args)
    {
        // every number in this tool is parsed and formatted with the invariant culture, whatever the machine locale is
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = Inv; System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = Inv;
        System.Globalization.CultureInfo.CurrentCulture = Inv; System.Globalization.CultureInfo.CurrentUICulture = Inv;
        AppDomain.CurrentDomain.UnhandledException += (_, e) => { if (e.ExceptionObject is Exception x) PrintException(x, "UNHANDLED"); else Console.Error.WriteLine("UNHANDLED: " + e.ExceptionObject); };
        try { return Run(args); }
        catch (Exception ex) { PrintException(ex); return 2; }
    }

    /// <summary>Regression checks for the note parser (the "bad note |d4" FormatException that used to kill every run) and number handling.</summary>
    static int SelfTest()
    {
        int fail = 0;
        void Check(string what, bool ok) { Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {what}"); if (!ok) fail++; }
        string Err(string src) { var sc = new Score("selftest", 60, 4); var p = sc.Sf("p", 42, -20); try { p.At(1).Play(src); return null; } catch (FormatException e) { return e.Message; } }
        int Count(string src) { var sc = new Score("selftest", 60, 4); var p = sc.Sf("p", 42, -20); p.At(1).Play(src); return p.Notes.Count; }
        Check("bar line glued to the next note (\"r:2 d3:2 |d4:4 |\") parses", Err("r:2 d3:2 |d4:4 |") == null && Count("r:2 d3:2 |d4:4 |") == 2);
        Check("two phrase strings concatenated without a space parse", Err("d4:4 |" + "eb3:4 |") == null);
        Check("decimal durations use the invariant culture (\".5\", \"1.5\")", Count("d3:.5 e3:1.5 f3:2 |") == 3);
        Check("chords and flats/sharps parse (\"bb1+f#2+d3:4\")", Count("bb1+f#2+d3:4 |") == 3 && Part.Pitch("bb1") == 34 && Part.Pitch("f#2") == 42 && Part.Pitch("c4") == 60);
        string e1 = Err("d3:2 x9:2 |"); Check("unknown note name is reported with part and token: " + e1, e1 != null && e1.Contains("x9") && e1.Contains("selftest/p"));
        string e2 = Err("d3:3 | e3:4 |"); Check("bar line off the grid is reported: " + e2, e2 != null && e2.Contains("bar line"));
        string e3 = Err("d3:abc |"); Check("bad duration is reported: " + e3, e3 != null && e3.Contains("abc"));
        string e4 = Err("!loud d3:4 |"); Check("unknown dynamic is reported: " + e4, e4 != null && e4.Contains("!loud"));
        string e5 = Err("d:4 |"); Check("missing octave is reported: " + e5, e5 != null && e5.Contains("octave"));
        Check("number formatting is culture-independent", string.Create(Inv, $"{-18.5:F2}") == "-18.50" && $"{1234.5:F1}" == "1234.5");
        try { L(4, "x.x"); Check("step pattern length check", false); } catch (FormatException) { Check("step pattern length check", true); }
        Check("all registered scores built", Music.BuildErrors.Count == 0);
        Console.WriteLine(fail == 0 ? "selftest passed" : $"selftest: {fail} check(s) FAILED");
        return fail == 0 ? 0 : 2;
    }
    static string L(int n, string s) => Music.PatternCheck(n, s);

    static int Run(string[] args)
    {
        var sw = Stopwatch.StartNew(); var names = new List<string>(); int jobs = Math.Max(2, Math.Min(12, Environment.ProcessorCount / 2));
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--root" || a == "--jobs")
            {
                if (i + 1 >= args.Length) { Console.Error.WriteLine($"ERROR: option {a} needs a value"); return 1; }
                string val = args[++i];
                if (a == "--root") Root = val;
                else if (!int.TryParse(val, System.Globalization.NumberStyles.Integer, Inv, out jobs) || jobs < 1) { Console.Error.WriteLine($"ERROR: --jobs expects a positive integer, got '{val}'"); return 1; }
            }
            else names.Add(a.ToLowerInvariant());
        }
        if (names.Count == 0) { Console.WriteLine("usage: all | sfx | amb | music | verify | check | list | sfinfo | <names...>   [--root <dir>] [--jobs <n>]"); return 1; }
        Sfx.Build(); Amb.Build(); Music.Build();
        foreach (var (piece, err) in Music.BuildErrors) PrintException(err, $"MUSIC SCORE ERROR in '{piece}' (piece skipped)");
        if (names.Contains("check")) { Music.Lint(); Console.WriteLine(Music.BuildErrors.Count == 0 ? $"All {Music.All.Count} scores build cleanly." : $"{Music.BuildErrors.Count} score(s) failed to build."); return Music.BuildErrors.Count == 0 ? 0 : 2; }
        if (names.Contains("selftest")) return SelfTest();
        if (names.Count >= 2 && names[0] == "seam")
        {   // diagnostic: decoded length and the samples either side of the loop point of OGG files
            foreach (var f in args.Skip(1).Where(a => a.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)))
            {
                var st = Io.ReadOgg(f); long gran = Io.LastGranule(File.ReadAllBytes(f)); int n = st.Len; var (ratio, lvl) = Meter.Seam(st.L, st.R);
                Console.WriteLine($"{Path.GetFileName(f)}: decoded {n} samples ({Io.LastRawDecoded} before end trim), last granule {gran}, seam ratio {ratio:F2}, level step {lvl:F2} dB");
                Console.WriteLine("   end   L: " + string.Join(" ", Enumerable.Range(n - 6, 6).Select(i => st.L[i].ToString("F5", Inv))) + "   | start L: " + string.Join(" ", Enumerable.Range(0, 6).Select(i => st.L[i].ToString("F5", Inv))));
                Console.WriteLine("   end   R: " + string.Join(" ", Enumerable.Range(n - 6, 6).Select(i => st.R[i].ToString("F5", Inv))) + "   | start R: " + string.Join(" ", Enumerable.Range(0, 6).Select(i => st.R[i].ToString("F5", Inv))));
            }
            return 0;
        }
        if (names.Contains("sfinfo")) { Sf.Info(); return 0; }
        if (names.Contains("probe")) { Sf.Probe(); return 0; }
        if (names.Contains("list"))
        {
            foreach (var d in Sfx.All) Console.WriteLine($"sfx/{d.Cat,-9} {d.Name,-18} x{d.Vars} {(d.Loop ? "loop " : "")}{(d.Stereo ? "stereo " : "")}{d.Desc}");
            foreach (var d in Amb.All) Console.WriteLine($"amb           {d.Name,-18} {d.Seconds}s {d.Desc}");
            foreach (var d in Music.All) Console.WriteLine($"music         {string.Join("+", d.Outputs),-28} {d.Desc}");
            return 0;
        }
        bool all = names.Contains("all"), verifyOnly = names.Count == 1 && names[0] == "verify";
        int failed = 0;
        if (!verifyOnly)
        {
            var sfx = Sfx.All.Where(d => all || names.Contains("sfx") || names.Contains(d.Name)).ToList();
            var amb = Amb.All.Where(d => all || names.Contains("amb") || names.Contains(d.Name)).ToList();
            var mus = Music.All.Where(d => all || names.Contains("music") || d.Outputs.Any(names.Contains)).ToList();
            var known = new HashSet<string>(Sfx.All.Select(d => d.Name).Concat(Amb.All.Select(d => d.Name)).Concat(Music.All.SelectMany(d => d.Outputs)).Concat(new[] { "all", "sfx", "amb", "music", "verify" }));
            if (names.Contains("verify") && names.Count > 1) names.Remove("verify");   // "<items> verify" just means generate, then verify (which always runs)
            foreach (var n in names.Where(n => !known.Contains(n))) Console.WriteLine($"WARNING: unknown item '{n}'");
            Log($"Generating: {sfx.Sum(d => d.Vars)} sfx files, {amb.Count} ambience beds, {mus.Sum(m => m.Outputs.Length)} music files (jobs={jobs})");
            var po = new ParallelOptions { MaxDegreeOfParallelism = jobs };
            // stingers need the SoundFont; force single load before parallel section
            if (sfx.Any(d => d.Name.EndsWith("_sting")) || mus.Count > 0) Sf.Load();
            // one failing item must not take the rest of the run down: report it in full and carry on
            void Guard(string what, Action act) { try { act(); } catch (Exception ex) { Interlocked.Increment(ref failed); PrintException(ex, $"ERROR while generating {what}"); } }
            Parallel.ForEach(sfx, po, d => Guard("sfx " + d.Name, () => WriteSfxGroup(d)));
            Log($"  sfx done at {sw.Elapsed.TotalSeconds:F1}s");
            Parallel.ForEach(amb, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(jobs, 5) }, d => Guard("amb " + d.Name, () => { var s = d.Render(); Io.WriteOgg(Path.Combine(AudioDir, "amb", d.Name + ".ogg"), s, 0.5f, d.Name, true); Log($"  amb {d.Name} written"); }));
            Log($"  amb done at {sw.Elapsed.TotalSeconds:F1}s");
            // each piece renders up to four stems at once, so only a few pieces run side by side
            Parallel.ForEach(mus, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Math.Min(jobs / 3, 4)) }, d => Guard("music " + d.Key, () =>
            {
                var outs = d.Render();
                foreach (var (name, s) in outs) Io.WriteOgg(Path.Combine(AudioDir, "music", name + ".ogg"), s, 0.55f, name, d.Loop);
                Log($"  music {string.Join("+", d.Outputs)} written at {sw.Elapsed.TotalSeconds:F1}s");
            }));
        }
        int bad = Verify.Run();
        Log($"Total time {sw.Elapsed.TotalSeconds:F1}s. {(bad == 0 ? "All files within target ranges." : bad + " file(s) flagged or missing - see analysis table.")}{(failed > 0 ? $" {failed} item(s) FAILED to generate." : "")}");
        return failed > 0 || Music.BuildErrors.Count > 0 ? 2 : 0;
    }

    /// <summary>Evenly spread, deterministically shuffled offsets in [-range, range] for the variations of one sound.</summary>
    public static double VarOffset(string name, int vars, int idx, double range)
    {
        if (vars <= 1 || range <= 0) return 0;
        var r = new Rng("var/" + name); var perm = Enumerable.Range(0, vars).ToArray();
        for (int i = vars - 1; i > 0; i--) { int j = r.I(i + 1); (perm[i], perm[j]) = (perm[j], perm[i]); }
        return range * (2 * (perm[idx] + 0.5) / vars - 1);
    }

    static float[] HpCircular(float[] x, double f)
    {
        var b = Biquad.HP(f); foreach (var v in x) b.Tick(v);
        var o = new float[x.Length]; for (int i = 0; i < x.Length; i++) o[i] = (float)b.Tick(x[i]); return o;
    }

    /// <summary>Renders every variation of one sound, evens out their loudness, and writes them as <name>_<n>.wav.
    /// Variations are peak-normalised first (that is what the per-sound targets mean), but a variation whose peak is one thin
    /// spike can come out 5-6 LU quieter than its siblings; the game picks variations at random, so each one is then moved
    /// toward the group's mean loudness (within 1 LU), never above the peak target and never more than 2.3 dB below it.</summary>
    public static void WriteSfxGroup(SfxDef d)
    {
        var bufs = new (float[] l, float[] r)[d.Vars];
        for (int i = 0; i < d.Vars; i++) bufs[i] = RenderSfx(d, i);
        if (d.Vars > 1 && !d.Loop)
        {
            var lu = bufs.Select(b => Meter.Lufs(b.l, b.r, false)).ToArray(); double mean = lu.Average(), cap = A.Db(d.PeakDb);
            for (int i = 0; i < d.Vars; i++)
            {
                double delta = mean - lu[i]; if (Math.Abs(delta) <= 1.0) continue;
                double pk = A.ToDb(Math.Max(bufs[i].l.Peak(), bufs[i].r?.Peak() ?? 0));
                double gain = A.Clamp(delta - Math.Sign(delta) * 1.0, (d.PeakDb - 2.3) - pk, 4.0); if (gain > 0 && gain < 0.05 || gain < 0 && gain > -0.05) continue;
                float[] Fix(float[] x)
                {
                    if (x == null) return null; var y = x.Mul(A.Db(gain)); double k = 0.6 * cap, span = cap - k;
                    for (int j = 0; j < y.Length; j++) { double a = Math.Abs(y[j]); if (a > k) y[j] = (float)(Math.Sign(y[j]) * (k + span * Math.Tanh((a - k) / span))); }   // only matters when a boost would pass the peak target
                    return y;
                }
                bool needsClip = pk + gain > d.PeakDb;
                bufs[i] = needsClip ? (Fix(bufs[i].l), Fix(bufs[i].r)) : (bufs[i].l.Mul(A.Db(gain)), bufs[i].r?.Mul(A.Db(gain)));
            }
        }
        for (int i = 0; i < d.Vars; i++) { string file = $"{d.Name}_{i + 1}.wav"; Io.WriteWav(Path.Combine(AudioDir, "sfx", file), bufs[i].l, bufs[i].r, d.Loop, Rng.Hash(file)); }
    }

    static (float[] l, float[] r) RenderSfx(SfxDef d, int idx)
    {
        var v = new V(d.Name, idx); object o = d.Gen(v);
        float[] l, r = null; if (o is St s) { l = s.L; r = s.R; } else l = (float[])o;
        if (d.Stereo && r == null) r = (float[])l.Clone();
        if (!d.Stereo && r != null) { l = new St(l, r).Mono(); r = null; }
        if (d.PitchVar > 0 && !d.Loop) { double ratio = A.Semi(VarOffset(d.Name, d.Vars, idx, d.PitchVar)); l = l.Resample(ratio); r = r?.Resample(ratio); }
        if (d.Loop) { l = HpCircular(l, 22); if (r != null) r = HpCircular(r, 22); }
        else
        {
            l = l.HP(22); r = r?.HP(22);
            double pk = Math.Max(l.Peak(), r?.Peak() ?? 0); if (pk < 1e-9) throw new Exception($"{d.Name}: silent output");
            double th0 = pk * A.Db(-50), th1 = pk * A.Db(-62); int n = l.Length, first = 0, last = n - 1;
            bool Loud(int i, double th) => Math.Abs(l[i]) > th || (r != null && Math.Abs(r[i]) > th);
            while (first < n - 1 && !Loud(first, th0)) first++;
            while (last > first && !Loud(last, th1)) last--;
            int pre = Math.Min(first, 44), start = first - pre, end = Math.Min(n, last + 1 + 441), len = end - start;
            bool capped = d.MaxDur > 0 && len > (int)(d.MaxDur * A.SR); if (capped) len = (int)(d.MaxDur * A.SR);
            float[] Cut(float[] x)
            {
                var y = new float[len]; Array.Copy(x, start, y, 0, len);
                for (int i = 0; i < pre; i++) y[i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / pre));
                int fo = capped ? Math.Min(len / 2, (int)(0.3 * d.MaxDur * A.SR)) : Math.Min(len / 2, 530); for (int i = 0; i < fo; i++) y[len - 1 - i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / fo));
                return y;
            }
            l = Cut(l); if (r != null) r = Cut(r);
        }
        double peak = Math.Max(l.Peak(), r?.Peak() ?? 0);
        double target = d.PeakDb - (d.Vars > 1 ? (VarOffset(d.Name + "/gain", d.Vars, idx, 1.0) + 1.0) * (idx == 0 ? 0 : 1) : 0);   // up to -2 dB on later variations
        double g = A.Db(target) / peak; l = l.Mul(g); r = r?.Mul(g);
        return (l, r);   // written by WriteSfxGroup as <name>_<n>.wav, n from 1 (the game's naming contract)
    }
}

/// <summary>Reads every expected file back from disk, measures it, prints the analysis table, writes manifest + CSV.</summary>
public static class Verify
{
    sealed class Row { public string Path, Cat, Group, Name; public int Var; public bool Loop; public Stats S; public string Flags = ""; public double Target; public string Pair; public double SumPeak = double.NaN; public string Desc, Profile; public double MinWin = 0; public Dictionary<string, object> Extra; }

    /// <summary>RMS level (dBFS) of consecutive 5 s windows, and the quietest 2 s window: shows the shape of a piece and catches dead air.</summary>
    static (string profile, double minWin) Shape(St st)
    {
        var parts = new List<string>(); int w = 5 * A.SR, n = st.Len;
        for (int at = 0; at < n; at += w) { double e = 0; int m = Math.Min(w, n - at); for (int i = at; i < at + m; i++) e += (double)st.L[i] * st.L[i] + (double)st.R[i] * st.R[i]; parts.Add(Math.Round(A.ToDb(Math.Sqrt(e / (2.0 * m)))).ToString("F0", System.Globalization.CultureInfo.InvariantCulture)); }
        double min = 0; int w2 = 2 * A.SR, hop = A.SR / 2;
        for (int at = 0; at + w2 <= n; at += hop) { double e = 0; for (int i = at; i < at + w2; i++) e += (double)st.L[i] * st.L[i] + (double)st.R[i] * st.R[i]; min = Math.Min(min, A.ToDb(Math.Sqrt(e / (2.0 * w2)))); }
        return (string.Join(" ", parts), min);
    }

    public static int Run()
    {
        var rows = new List<Row>(); var missing = new List<string>();
        var jobs = new List<Action>(); var lk = new object();
        foreach (var d in Sfx.All)
        {
            int i = 0;
            foreach (var f in d.Files())
            {
                int idx = i++; string p = Path.Combine(Program.AudioDir, "sfx", f);
                jobs.Add(() =>
                {
                    if (!File.Exists(p)) { lock (lk) missing.Add("sfx/" + f); return; }
                    var (l, r, hasLoop) = Io.ReadWav(p); var s = Meter.Measure(l, r, d.Loop);
                    var row = new Row { Path = "assets/audio/sfx/" + f, Cat = "sfx/" + d.Cat, Group = d.Name, Name = Path.GetFileNameWithoutExtension(f), Var = idx + 1, Loop = d.Loop, S = s, Target = d.PeakDb, Desc = d.Desc };
                    var fl = new List<string>();
                    if (s.Clips > 0) fl.Add($"CLIP({s.Clips})");
                    if (s.PeakDb > -0.9) fl.Add("PEAK>-1"); if (s.PeakDb > d.PeakDb + 0.3 || s.PeakDb < d.PeakDb - 2.4) fl.Add("PEAK!=TARGET");
                    if (Math.Abs(s.Dc) > 0.003) fl.Add("DC");
                    if (s.Channels != (d.Stereo ? 2 : 1)) fl.Add("CHANNELS");
                    if (!d.Loop && s.LeadSilenceMs > 5) fl.Add("LEAD>5ms"); if (!d.Loop && s.TailDb > -40) fl.Add("TAIL");
                    if (d.Loop && s.SeamRatio > 2.0) fl.Add("SEAM"); if (d.Loop != hasLoop) fl.Add("SMPL"); if (d.MaxDur > 0 && s.Duration > d.MaxDur + 0.01) fl.Add("DUR"); if (d.MinDur > 0 && s.Duration < d.MinDur - 0.01) fl.Add("DUR");
                    row.Flags = string.Join(",", fl); lock (lk) rows.Add(row);
                });
            }
        }
        foreach (var d in Amb.All)
        {
            string p = Path.Combine(Program.AudioDir, "amb", d.Name + ".ogg");
            jobs.Add(() =>
            {
                if (!File.Exists(p)) { lock (lk) missing.Add("amb/" + d.Name + ".ogg"); return; }
                var st = Io.ReadOgg(p); var s = Meter.Measure(st.L, st.R, true);
                var row = new Row { Path = $"assets/audio/amb/{d.Name}.ogg", Cat = "amb", Group = d.Name, Name = d.Name, Loop = true, S = s, Target = -24, Desc = d.Desc };
                var fl = new List<string>();
                if (Math.Abs(s.Lufs + 24) > 1.0) fl.Add("LUFS"); if (s.PeakDb > -1.0) fl.Add("PEAK>-1"); if (s.Clips > 0) fl.Add("CLIP");
                if (s.Duration < 45 || s.Duration > 60.01) fl.Add("DUR"); if (Math.Abs(s.Samples - A.N(d.Seconds)) > 0) fl.Add("LEN");
                if (s.SeamRatio > 2.0) fl.Add("SEAM"); if (Math.Abs(s.Dc) > 0.003) fl.Add("DC");
                (row.Profile, row.MinWin) = Shape(st); if (row.MinWin < -60) fl.Add("SILENCE");
                row.Flags = string.Join(",", fl); lock (lk) rows.Add(row);
            });
        }
        var decoded = new Dictionary<string, St>();
        foreach (var d in Music.All)
            foreach (var name in d.Outputs)
            {
                string p = Path.Combine(Program.AudioDir, "music", name + ".ogg");
                jobs.Add(() =>
                {
                    if (!File.Exists(p)) { lock (lk) missing.Add("music/" + name + ".ogg"); return; }
                    var st = Io.ReadOgg(p); var s = Meter.Measure(st.L, st.R, d.Loop);
                    var row = new Row { Path = $"assets/audio/music/{name}.ogg", Cat = "music", Group = d.Outputs.Length > 1 ? d.Key : name, Name = name, Loop = d.Loop, S = s, Target = d.Lufs(name), Desc = d.Desc };
                    row.Extra = new() { ["bpm"] = d.Bpm, ["bars"] = d.Bars, ["key"] = d.KeyName, ["meter"] = d.Meter };
                    if (d.Outputs.Length > 1) { row.Pair = d.Outputs.First(o => o != name); row.Extra["role"] = name.EndsWith("_combat") ? "combat_layer" : "base"; lock (lk) decoded[name] = st; }
                    var fl = new List<string>();
                    if (Math.Abs(s.Lufs - row.Target) > 1.0) fl.Add("LUFS"); if (s.PeakDb > -1.0) fl.Add("PEAK>-1"); if (s.Clips > 0) fl.Add("CLIP");
                    if (s.Duration < d.MinSec - 0.01 || s.Duration > d.MaxSec + 0.01) fl.Add("DUR"); if (s.Samples != d.Samples) fl.Add("LEN");
                    if (d.Loop && s.SeamRatio > 2.0) fl.Add("SEAM"); if (Math.Abs(s.Dc) > 0.003) fl.Add("DC");
                    if (!d.Loop && s.TailDb > -40) fl.Add("TAIL");
                    (row.Profile, row.MinWin) = Shape(st); if (d.Loop && row.MinWin < -55) fl.Add("SILENCE");
                    row.Flags = string.Join(",", fl); lock (lk) rows.Add(row);
                });
            }
        Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount - 2) }, j => j());
        // adaptive pairs: identical length, and the sum must not clip
        foreach (var d in Music.All.Where(m => m.Outputs.Length > 1))
        {
            if (!decoded.TryGetValue(d.Outputs[0], out var a) || !decoded.TryGetValue(d.Outputs[1], out var b)) continue;
            var ra = rows.First(x => x.Name == d.Outputs[0]); var rb = rows.First(x => x.Name == d.Outputs[1]);
            if (a.Len != b.Len) { ra.Flags += ",PAIRLEN"; rb.Flags += ",PAIRLEN"; continue; }
            double pk = 0; for (int i = 0; i < a.Len; i++) pk = Math.Max(pk, Math.Max(Math.Abs(a.L[i] + b.L[i]), Math.Abs(a.R[i] + b.R[i])));
            ra.SumPeak = rb.SumPeak = A.ToDb(pk);
            var sum = new St(a.Len); for (int i = 0; i < a.Len; i++) { sum.L[i] = a.L[i] + b.L[i]; sum.R[i] = a.R[i] + b.R[i]; }
            ra.Extra["pair_sum_lufs"] = rb.Extra["pair_sum_lufs"] = Math.Round(Meter.Lufs(sum, true), 2);
            if (pk > A.Db(-0.3)) { ra.Flags += ",SUMCLIP"; rb.Flags += ",SUMCLIP"; }
        }
        foreach (var r in rows) r.Flags = r.Flags.Trim(',');
        rows = rows.OrderBy(r => r.Cat.StartsWith("sfx") ? 0 : r.Cat == "amb" ? 1 : 2).ThenBy(r => r.Cat).ThenBy(r => r.Path, StringComparer.Ordinal).ToList();

        // ---- table ----
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{"file",-26}{"cat",-14}{"ch",3}{"dur s",8}{"peak dB",9}{"rms dB",8}{"LUFS",8}{"DC",9}{"centroid",9}{"lead ms",8}{"tail dB",8}{"seam",6}{"sum pk",8}  flags");
        foreach (var r in rows)
        {
            var s = r.S;
            sb.AppendLine($"{r.Name,-26}{r.Cat,-14}{s.Channels,3}{s.Duration,8:F2}{s.PeakDb,9:F2}{s.RmsDb,8:F1}{s.Lufs,8:F1}{s.Dc,9:F5}{s.Centroid,9:F0}{(r.Loop ? "-" : s.LeadSilenceMs.ToString("F1")),8}{(r.Loop ? "-" : s.TailDb.ToString("F0")),8}{(r.Loop ? s.SeamRatio.ToString("F2") : "-"),6}{(double.IsNaN(r.SumPeak) ? "-" : r.SumPeak.ToString("F2")),8}  {r.Flags}");
        }
        foreach (var m in missing) sb.AppendLine($"MISSING {m}");
        if (rows.Any(r => r.Profile != null))
        {
            sb.AppendLine(); sb.AppendLine("level over time (dBFS RMS per 5 s window; quietest 2 s window in brackets)");
            foreach (var r in rows.Where(r => r.Profile != null)) sb.AppendLine($"{r.Name,-16}[{r.MinWin,6:F1}]  {r.Profile}");
        }
        // ---- summary ----
        sb.AppendLine();
        long bytes = 0;
        foreach (var grp in rows.GroupBy(r => r.Cat))
        {
            long b = grp.Sum(r => new FileInfo(Path.Combine(Program.Root, "game", r.Path.Replace('/', Path.DirectorySeparatorChar))).Length); bytes += b;
            sb.AppendLine($"{grp.Key,-14} files {grp.Count(),4}   groups {grp.Select(r => r.Group).Distinct().Count(),4}   {b / 1048576.0,8:F2} MB   total {grp.Sum(r => r.S.Duration),8:F1} s   flagged {grp.Count(r => r.Flags != "")}");
        }
        sb.AppendLine($"TOTAL          files {rows.Count,4}   {bytes / 1048576.0:F2} MB   missing {missing.Count}   flagged {rows.Count(r => r.Flags != "")}");
        string table = sb.ToString(); Console.WriteLine(table);
        File.WriteAllText(Path.Combine(Program.Root, "tools", "audio", "analysis.txt"), table);

        // ---- manifest ----
        var files = rows.Select(r =>
        {
            var o = new Dictionary<string, object>
            {
                ["path"] = r.Path, ["category"] = r.Cat, ["group"] = r.Group, ["variation"] = r.Var, ["duration"] = Math.Round(r.S.Duration, 3), ["channels"] = r.S.Channels,
                ["sample_rate"] = A.SR, ["loop"] = r.Loop, ["peak_db"] = Math.Round(r.S.PeakDb, 2), ["lufs"] = Math.Round(r.S.Lufs, 2), ["rms_db"] = Math.Round(r.S.RmsDb, 2),
                ["centroid_hz"] = Math.Round(r.S.Centroid), ["samples"] = r.S.Samples, ["dc"] = Math.Round(r.S.Dc, 6), ["desc"] = r.Desc ?? "",
            };
            if (r.Loop) o["loop_seam_ratio"] = Math.Round(r.S.SeamRatio, 2);
            o["bytes"] = new FileInfo(Path.Combine(Program.Root, "game", r.Path.Replace('/', Path.DirectorySeparatorChar))).Length;
            if (r.Pair != null) { o["pair"] = r.Pair; o["pair_sum_peak_db"] = Math.Round(r.SumPeak, 2); }
            if (r.Extra != null) foreach (var kv in r.Extra) o[kv.Key] = kv.Value;
            if (r.Flags != "") o["flags"] = r.Flags;
            return o;
        }).ToList();
        var manifest = new Dictionary<string, object>
        {
            ["generator"] = "Y:/tools/audio (dotnet run -c Release --project Y:\\tools\\audio -- all)",
            ["base_path"] = "res://", ["disk_root"] = "Y:/game",
            ["naming"] = "sfx: assets/audio/sfx/<group>_<n>.wav with n from 1 (single sounds are <group>_1.wav); amb: assets/audio/amb/<name>.ogg; music: assets/audio/music/<name>.ogg",
            ["targets"] = new Dictionary<string, object> { ["sfx"] = "44.1 kHz 16-bit PCM WAV, mono (UI/stingers stereo), per-sound peak target", ["amb"] = "44.1 kHz stereo OGG Vorbis, -24 LUFS, seamless loop", ["music"] = "44.1 kHz stereo OGG Vorbis, -18 LUFS (combat layers -19), seamless loop unless loop=false" },
            ["counts"] = rows.GroupBy(r => r.Cat).ToDictionary(g => g.Key, g => (object)g.Count()),
            ["totals"] = rows.GroupBy(r => r.Cat.StartsWith("sfx") ? "sfx" : r.Cat).ToDictionary(g => g.Key, g => (object)new Dictionary<string, object>
            {
                ["files"] = g.Count(), ["groups"] = g.Select(r => r.Group).Distinct().Count(), ["seconds"] = Math.Round(g.Sum(r => r.S.Duration), 1),
                ["bytes"] = g.Sum(r => new FileInfo(Path.Combine(Program.Root, "game", r.Path.Replace('/', Path.DirectorySeparatorChar))).Length), ["flagged"] = g.Count(r => r.Flags != ""),
            }),
            ["group_info"] = Sfx.All.ToDictionary(d => d.Name, d => (object)new Dictionary<string, object> { ["category"] = d.Cat, ["variations"] = d.Vars, ["loop"] = d.Loop, ["stereo"] = d.Stereo, ["peak_target_db"] = d.PeakDb, ["desc"] = d.Desc ?? "" }),
            ["sfx_loops"] = Sfx.All.Where(d => d.Loop).Select(d => d.Name).ToList(),
            ["amb"] = rows.Where(r => r.Cat == "amb").ToDictionary(r => r.Name, r => (object)new Dictionary<string, object> { ["path"] = r.Path, ["loop"] = true, ["duration"] = Math.Round(r.S.Duration, 3), ["lufs"] = Math.Round(r.S.Lufs, 2), ["peak_db"] = Math.Round(r.S.PeakDb, 2) }),
            ["music"] = rows.Where(r => r.Cat == "music").ToDictionary(r => r.Name, r => (object)new Dictionary<string, object> { ["path"] = r.Path, ["loop"] = r.Loop, ["duration"] = Math.Round(r.S.Duration, 3), ["lufs"] = Math.Round(r.S.Lufs, 2), ["peak_db"] = Math.Round(r.S.PeakDb, 2), ["pair"] = r.Pair ?? "" }),
            ["groups"] = rows.Where(r => r.Cat.StartsWith("sfx")).GroupBy(r => r.Group).ToDictionary(g => g.Key, g => (object)g.Select(r => r.Path).ToList()),
            ["music_pairs"] = Music.All.Where(m => m.Outputs.Length > 1).ToDictionary(m => m.Key, m => (object)new Dictionary<string, object> { ["base"] = $"assets/audio/music/{m.Outputs[0]}.ogg", ["combat"] = $"assets/audio/music/{m.Outputs[1]}.ogg", ["bpm"] = m.Bpm, ["bars"] = m.Bars, ["meter"] = m.Meter, ["key"] = m.KeyName, ["seconds"] = Math.Round(m.Samples / (double)A.SR, 3), ["samples"] = m.Samples }),
            ["files"] = files,
        };
        File.WriteAllText(Path.Combine(Program.AudioDir, "manifest_audio.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        return rows.Count(r => r.Flags != "") + missing.Count;
    }
}

