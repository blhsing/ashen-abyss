// Score inspection without rendering ("check" command): per-part note counts, ranges and pitch classes, and for
// the adaptive pairs a bar-by-bar comparison of the exploration bass against the combat layer's bass.
using static AudioTool.A;

namespace AudioTool;

public static partial class Music
{
    static readonly string[] PcName = { "C", "C#", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B" };
    static bool IsPerc(Part p) => p.Drums || new[] { "drum", "perc", "anvil", "taiko", "wood", "bones", "snare", "toms", "bongos", "forge", "cymbal", "orch" }.Any(k => p.Name.Contains(k));

    public static void Lint()
    {
        foreach (var d in All)
        {
            var scores = d.Builders.Select(b => b()).ToArray();
            foreach (var sc in scores)
            {
                double end = sc.Bars * sc.BeatsPerBar;
                Console.WriteLine($"{sc.Name}: {sc.Bpm} BPM, {sc.Bars} bars of {sc.BeatsPerBar}, {sc.Len / (double)SR:F3} s, {sc.Parts.Count} parts, {sc.Parts.Sum(p => p.Notes.Count)} notes  [{d.KeyName}]");
                foreach (var p in sc.Parts)
                {
                    if (p.Notes.Count == 0) { Console.WriteLine($"    {p.Name,-16} {(p.Synth != null ? "(continuous bed)" : "(EMPTY PART)")}"); continue; }
                    bool perc = IsPerc(p);
                    string pcs = perc ? "(percussion)" : string.Join(" ", p.Notes.Select(n => ((n.P + p.Transpose) % 12 + 12) % 12).Distinct().OrderBy(x => x).Select(x => PcName[x]));
                    double first = p.Notes.Min(n => n.B), last = p.Notes.Max(n => n.B + n.D), lastOn = p.Notes.Max(n => n.B);
                    Console.WriteLine($"    {p.Name,-16} notes {p.Notes.Count,4}  keys {p.Notes.Min(n => n.P + p.Transpose),3}-{p.Notes.Max(n => n.P + p.Transpose),3}  beats {first,6:F1}-{last,6:F1}  {pcs}{(lastOn >= end - 1e-6 ? "   !! NOTE STARTS AT OR AFTER THE END" : "")}");
                }
            }
            if (scores.Length != 2) continue;
            // adaptive pair: the lowest sounding exploration note against the combat basses, twice per bar
            var bs = scores[0]; var cb = scores[1].Parts.FirstOrDefault(p => p.Name == "basses"); if (cb == null) continue;
            var tonal = bs.Parts.Where(p => !IsPerc(p) && p.Name != "drone" && p.Notes.Count > 0).ToList(); int diff = 0, cmp = 0; var lines = new List<string>();
            for (double t = 0; t < bs.Bars * bs.BeatsPerBar; t += bs.BeatsPerBar / 2.0)
            {
                var low = tonal.SelectMany(p => p.Notes.Where(n => n.B <= t + 0.05 && t + 0.05 < n.B + n.D).Select(n => n.P + p.Transpose)).DefaultIfEmpty(-1).Min();
                var cn = cb.Notes.Where(n => n.B >= t - 1e-6 && n.B < t + 0.3).Select(n => n.P + cb.Transpose).DefaultIfEmpty(-1).Min();
                if (low < 0 || cn < 0) continue; cmp++;
                if (low % 12 != cn % 12) { diff++; lines.Add($"bar {(int)(t / bs.BeatsPerBar) + 1}{(t % bs.BeatsPerBar > 0 ? "+" : "")}: {PcName[low % 12]} / {PcName[cn % 12]}"); }
            }
            Console.WriteLine($"  pair {d.Key}: exploration bass vs combat bass differ at {diff} of {cmp} points{(diff > 0 ? " (base/combat): " + string.Join(", ", lines) : "")}");
        }
    }
}
