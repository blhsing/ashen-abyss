// Original compositions (2): adaptive zone pairs for the Ashen Wastes, Frostspine Pass, Slumbering Catacombs and
// Weeping Mire. Every pair is two scores on ONE grid (same tempo, metre, bar count, key and bar-by-bar harmony):
//   <zone>_base    exploration music, complete on its own
//   <zone>_combat  ONLY the added driving layer (ostinato strings + percussion); it follows the same root per bar,
//                  so the two files can be started together and cross-faded freely.
// All note data is written here by hand; nothing is quoted from existing music.
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public static partial class Music
{
    // ---------- helpers shared by the zone scores ----------
    /// <summary>Checks a step pattern's length when the score is built, so a typo cannot silently spill into the next bar.</summary>
    static string L(int n, string s) { if (s.Length != n) throw new FormatException($"step pattern \"{s}\" should have {n} steps but has {s.Length}"); return s; }
    public static string PatternCheck(int n, string s) => L(n, s);
    /// <summary>Moves a pitch by octaves into [lo, lo+12).</summary>
    static int FoldTo(int p, int lo) { while (p < lo) p += 12; while (p >= lo + 12) p -= 12; return p; }

    /// <summary>Ostinato figure from an absolute beat: rhythm chars (X accent, x normal, o soft, . rest), one scale-degree offset
    /// per step relative to the chord root. foldLo keeps the root inside one octave [foldLo, foldLo+12) whatever the degree is.</summary>
    static void Fig(Part p, double beat0, int[] scale, int tonic, int rootDeg, string rhythm, int[] degs, double step, double vel, double dur = -1, int foldLo = -1, double accent = 14)
    {
        int root = Deg(scale, tonic, rootDeg), shift = foldLo >= 0 ? FoldTo(root, foldLo) - root : 0;
        for (int i = 0; i < rhythm.Length; i++)
        {
            char c = rhythm[i]; if (c == '.' || c == ' ') continue;
            p.Note(beat0 + i * step, dur > 0 ? dur : step * 0.9, Deg(scale, tonic, rootDeg + degs[i % degs.Length]) + shift, c == 'X' ? vel + accent : c == 'o' ? vel - 22 : vel);
        }
    }
    /// <summary>Drum step pattern from an absolute beat (X accent, x normal, o ghost, . rest).</summary>
    static void Pat(Part p, double beat0, string pat, int pitch, double v, double step = 0.25)
    {
        for (int i = 0; i < pat.Length; i++) { char c = pat[i]; if (c == '.' || c == ' ') continue; p.Note(beat0 + i * step, step, pitch, c == 'X' ? v + 16 : c == 'o' ? v - 26 : v); }
    }
    static readonly int[] One = { 0 };
    /// <summary>Combat percussion: cap the crest factor so coinciding hits do not make the bus limiter pump.</summary>
    static void Tame(params Part[] ps) { foreach (var p in ps) p.Crest = 17; }

    // =====================================================================================================
    //  ASHEN WASTES - E Aeolian, 80 BPM, 4/4, 40 bars (2:00). Cold, sparse, windy: a wind bed and an open-fifth
    //  drone, a lonely viola that climbs a fifth and sighs back, the cello answering below, a harp that only
    //  speaks every other bar. Sections of eight bars: A alone, B with strings, C cello, D duet, E thinning out.
    // =====================================================================================================
    // root per bar as a scale degree above E: 0=E 2=G 3=A 4=B 5=C 6=D
    static readonly int[] WastesRoots = { 0,0,5,0,3,2,6,0,  0,6,5,4,3,5,6,4,  5,2,3,0,5,6,0,0,  0,6,5,4,3,2,5,0,  0,0,5,0,3,0,0,0 };
    static Score WastesNew(string n) => new Score(n, 80, 40) { RevTime = 4.2, RevDamp = 3800, RevSize = 1.1 };

    static Score WastesBase()
    {
        var s = WastesNew("wastes_base");
        s.Dsp("wind", Syn.Bed((r, d) => Sfx.Wind(r, d, 260, 1200, 0.22, 0.5)), -28.5, 0, -24);
        var drone = s.Dsp("drone", Syn.Drone(140, 420, 7, 0.5, 5), -28, 0, -14); drone.Note(0, 160, 40, 100).Note(0, 160, 47, 62);
        var viola = Solo(s, "viola", 41, -20.5, 0.15);
        viola.At(3).Play("!mp r:1 b3:1 e4:2 | f#4:1 g4:2.5 f#4:.5 | e4:3 d4:1 | b3:4 | r:1 a3:1 d4:1 f#4:1 | e4:4 |")
             .At(9).Play("!mf g4:2 b4:2 | a4:3 f#4:1 | g4:2 e4:1 g4:1 | f#4:3 r:1 | e4:1 a4:1 c5:2 | b4:1.5 a4:.5 g4:2 | a4:2 f#4:1 d4:1 | f#4:2 b3:2 |")
             .At(21).Play("!p g4:4 | a4:4 | g4:2 f#4:2 | e4:4 |")
             .At(25).Play("!mf b4:1 e5:3 | d5:1.5 c5:.5 a4:2 | g4:1 c5:1 e5:2 | d5:2 b4:1 f#4:1 | a4:2 c5:1 e5:1 | d5:3 b4:1 | c5:1 e5:1 d5:1 g4:1 | b4:4 |")
             .At(35).Play("!p r:2 e4:2 | b3:4 |").At(38).Play("!p r:1 b3:1 e4:2 | f#4:4 |");
        var cello = Solo(s, "cello", 42, -21.5, -0.2);
        cello.At(17).Play("!mp e3:2 g3:2 | d3:3 b2:1 | c3:2 e3:1 a3:1 | g3:3 r:1 | e3:1 g3:1 c4:2 | a3:1.5 f#3:.5 d3:2 | e3:2 b2:2 | e3:4 |")
             .At(25).Play("!mp e3:2 g3:2 | f#3:2 d3:2 | e3:2 c3:2 | b2:2 d3:2 | c3:2 a2:2 | b2:2 g2:2 | c3:2 e3:2 | e3:4 |");
        var str = PadS(s, "strings", 49, -27, 0, -5);
        str.At(9).Play("!p e2+b2+g3+b3+e4:4 | d2+a2+f#3+a3+d4:4 | c2+g2+g3+c4+e4:4 | b1+f#2+f#3+b3+d4:4 | a1+e2+e3+a3+c4:4 | c2+g2+e3+g3+c4:4 | d2+a2+f#3+a3+d4:4 | b1+f#2+f#3+b3+d4:4 |")
           .At(21).Play("!pp c2+g2+e3+g3:4 | d2+a2+f#3+a3:4 | e2+b2+e3+g3:8 |")
           .At(25).Play("!mp e2+b2+g3+b3+e4:4 | d2+a2+f#3+a3+d4:4 | c2+g2+g3+c4+e4:4 | b1+f#2+f#3+b3+d4:4 | a1+e2+e3+a3+c4:4 | g1+d2+d3+g3+b3:4 | c2+g2+e3+g3+c4:4 | e2+b2+e3+g3+b3:4 |")
           .At(33).Play("!pp e2+b2+e3+b3:8 | c2+g2+e3+g3:4 | e2+b2+e3+b3:4 |");
        var harp = Plk(s, "harp", 46, -25, 0.35, -4);
        harp.At(1).Play("!p e2:.5 b2:.5 e3:.5 b3:.5 e4:2 | r:2 b3:1 g3:1 | c3:.5 g3:.5 e4:3 | r:4 | a2:.5 e3:.5 a3:.5 c4:.5 e4:2 | r:2 d4:1 b3:1 | d3:.5 a3:.5 d4:3 | e3:1 b3:1 e4:2 |")
            .Play("!p e2:1 b2:1 g3:2 | r:4 | c3:1 g3:1 e4:2 | r:4 | a2:1 e3:1 c4:2 | r:4 | d3:1 a3:1 f#4:2 | b2:1 f#3:1 b3:2 |")
            .Play("!p g4:1 e4:1 c4:1 e4:1 | g4:1 d4:1 b3:1 d4:1 | a4:1 e4:1 c4:1 e4:1 | g4:1 e4:1 b3:1 e4:1 | g4:1 e4:1 c4:1 e4:1 | a4:1 f#4:1 d4:1 f#4:1 | g4:1 e4:1 b3:1 e4:1 | e4:1 b3:1 e3:2 |")
            .Play("!mp e2:.5 b2:.5 e3:.5 g3:.5 b3:2 | d2:.5 a2:.5 d3:.5 f#3:.5 a3:2 | c2:.5 g2:.5 c3:.5 e3:.5 g3:2 | b1:.5 f#2:.5 b2:.5 d3:.5 f#3:2 | a1:.5 e2:.5 a2:.5 c3:.5 e3:2 | g1:.5 d2:.5 g2:.5 b2:.5 d3:2 | c2:.5 g2:.5 c3:.5 e3:.5 g3:2 | e2:.5 b2:.5 e3:.5 b3:.5 e4:2 |")
            .Play("!p r:4 | r:2 b3:1 g3:1 | c3:.5 g3:.5 e4:3 | r:4 | a2:.5 e3:.5 c4:3 | r:4 | e3:1 b3:1 f#4:2 | r:4 |");
        var glass = s.Dsp("bowedglass", Syn.Bowed(RGlass, 3, 4, 0.2, 0.6), -34, 0, -2);
        glass.Note(0, 16, 71, 90).Note(s.BarBeat(17), 16, 76, 90).Note(s.BarBeat(33), 24, 71, 90);
        var star = s.Dsp("glass", Syn.Glass(0.8, 3.0), -32, 0.3, -2);
        star.Note(s.BarBeat(2, 2), 5, 83, 80).Note(s.BarBeat(6), 5, 79, 70).Note(s.BarBeat(14, 2), 5, 84, 70).Note(s.BarBeat(34, 2), 5, 83, 80).Note(s.BarBeat(38), 6, 78, 70);
        var far = DrumSyn(s, "fardrum", -31, -3);
        foreach (int b in new[] { 1, 17, 25, 33 }) far.Note(s.BarBeat(b), 1, 46, 100);
        return s;
    }

    static Score WastesCombat()
    {
        var s = WastesNew("wastes_combat"); const int T = 40; var sc = Aeolian;
        var vc = Stac(s, "celli", 42, -21, -0.2, -11, 0.15); var va = Stac(s, "violas", 41, -24.5, 0.25, -10, 0.13); var cb = Stac(s, "basses", 43, -22.5, 0.1, -13, 0.3);
        var pz = Stac(s, "pizz", 45, -24, 0.2, -10, 0.3); var pulse = s.Dsp("pulse", Syn.Pulse(1000, 0.12), -27.5, 0, -16);
        var taiko = Perc(s, "taiko", 116, -20.5, 0, -10); var frame = DrumSyn(s, "framedrums", -24.5, -11); var big = DrumSyn(s, "bigdrum", -22, -13); var timp = Perc(s, "timpani", 47, -24, 0.15, -9);
        Tame(taiko, frame, big, timp);
        int[] dg = { 0, 0, 0, 2, 0, 0, 1, 0, 0, 0, 0, 2, 0, -1, 0, 1 };
        for (int b = 0; b < 40; b++)
        {
            int r = WastesRoots[b]; double bb = b * 4; bool thin = b == 22 || b == 23, fill = b % 4 == 3, peak = b >= 24 && b < 32;
            double v = b < 8 ? 92 : peak ? 106 : b >= 32 ? 96 : 100; int tp = FoldTo(Deg(sc, T, r), 40);
            Fig(cb, bb, sc, T, r, L(16, thin ? "X.......x......." : "X.......X..x...."), One, 0.25, v, 0.5, 28);
            Pat(taiko, bb, L(16, fill ? "X.....x.X...xxxx" : "X.....x.X...x..."), 38, thin ? 82 : 98);
            if (thin) { if (b == 23) { big.Note(bb, 4, 52, 104); Roll(timp, bb + 2, 2, tp, 50, 100); } continue; }   // two bars held back, then the riser into the duet
            Fig(vc, bb, sc, T, r, L(16, "X.xxX.xxX.xxX.xx"), dg, 0.25, v, -1, 36);
            Fig(va, bb, sc, T, r, L(16, "..x...x...x...x."), new[] { 4 }, 0.25, v - 10, -1, 48);
            Fig(pz, bb, sc, T, r, L(16, "X...x...X...x..."), new[] { 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0 }, 0.25, v - 8, -1, 48);
            if (b >= 8) Fig(pulse, bb, sc, T, r, L(16, "X.xxX.xxX.xxX.xx"), dg, 0.25, 84, -1, 36);
            Pat(frame, bb, L(16, "x..x..x.x..x..x."), 38, 84); Pat(frame, bb, L(16, "..o...o...o...o."), 40, 84); Pat(frame, bb, L(16, "x.x.x.x.x.x.x.x."), 42, 52);
            Pat(big, bb, L(16, "X.......x......."), 36, 102);
            if (b % 8 == 7) Pat(taiko, bb, L(16, "..........x.x.xx"), 45, 90);
            timp.Note(bb, 2, tp, 100); if (b % 2 == 1) timp.Note(bb + 2.5, 1, tp, 82);
        }
        return s;
    }

    // =====================================================================================================
    //  FROSTSPINE PASS - F# Aeolian, 96 BPM, 3/4, 64 bars (2:00). Thin, glassy, wide: a glass pad with no bass
    //  under it, a solo violin high and unhurried, celesta drops, harp on the downbeats. Harmony moves every two
    //  bars. A (1-16) violin alone, B (17-32) celesta figure, C (33-48) violin and viola, D (49-64) thinning.
    // =====================================================================================================
    static readonly Dictionary<string, (string pad, int root, string[] harp, string bass, string[] top)> FrostCh = new()
    {
        ["F#m"] = ("f#3+c#4+a4", 0, new[] { "f#3", "c#4", "a4" }, "f#2", new[] { "f#6", "c#6", "a5" }),
        ["D"] = ("f#3+d4+a4", 5, new[] { "d3", "a3", "f#4" }, "d2", new[] { "f#6", "d6", "a5" }),
        ["A"] = ("e3+c#4+a4", 2, new[] { "a2", "e3", "c#4" }, "a1", new[] { "e6", "c#6", "a5" }),
        ["E"] = ("e3+b3+g#4", 6, new[] { "e3", "b3", "g#4" }, "e2", new[] { "g#6", "e6", "b5" }),
        ["C#m"] = ("e3+c#4+g#4", 4, new[] { "c#3", "g#3", "e4" }, "c#2", new[] { "g#6", "e6", "c#6" }),
        ["Bm"] = ("f#3+d4+b4", 3, new[] { "b2", "f#3", "d4" }, "b1", new[] { "f#6", "d6", "b5" }),
        ["F#m/A"] = ("a3+c#4+f#4", 2, new[] { "a2", "f#3", "c#4" }, "a1", new[] { "f#6", "c#6", "a5" }),
        ["A/C#"] = ("e3+c#4+a4", 4, new[] { "c#3", "a3", "e4" }, "c#2", new[] { "e6", "c#6", "a5" }),
    };
    // one chord per two bars
    static readonly string[] FrostPlan = ("F#m F#m D D F#m A E C#m " + "Bm F#m/A D E Bm D C#m C#m " + "D E F#m F#m D A/C# Bm C#m " + "F#m F#m D D F#m E F#m F#m").Split(' ');
    static Score FrostNew(string n) => new Score(n, 96, 64, 3) { RevTime = 5.0, RevDamp = 6000, RevSize = 1.2 };

    static Score FrostBase()
    {
        var s = FrostNew("frost_base");
        if (FrostPlan.Length != 32) throw new FormatException("frost plan has " + FrostPlan.Length + " chords, expected 32");
        s.Dsp("wind", Syn.Bed((r, d) => Sfx.Wind(r, d, 700, 2800, 0.25, 0.6, 2.0)), -31, 0, -22);
        var pad = s.Dsp("glasspad", Syn.Glass(1.6, 3.0), -25.5, 0, -5);
        var cel = Plk(s, "celesta", 8, -25, -0.35, -3); var harp = Plk(s, "harp", 46, -25.5, 0.4, -4);
        var shim = PadS(s, "shimmer", 44, -30, 0.3, -3); var bass = s.Sf("bass", 43, -28, 0, -12); bass.Swell = (-8, 0, -8, 0.4);
        for (int k = 0; k < 32; k++)
        {
            var c = FrostCh[FrostPlan[k]]; int sec = k / 8, bar = k * 2 + 1; double b0 = k * 6;
            int t0 = Part.Pitch(c.top[0]), t1 = Part.Pitch(c.top[1]), t2 = Part.Pitch(c.top[2]), h0 = Part.Pitch(c.harp[0]), h1 = Part.Pitch(c.harp[1]), h2 = Part.Pitch(c.harp[2]);
            pad.At(bar).Play((sec == 2 ? "!mp " : sec == 1 ? "!p " : "!pp ") + c.pad + ":6");
            if (sec == 1)
            {   // the celesta figure: three falling drops and a held one
                cel.Note(b0, 1.5, t0, 62).Note(b0 + 1, 1.5, t2, 50).Note(b0 + 2, 1.5, t1, 54).Note(b0 + 3, 3, k % 2 == 0 ? t0 : t1, 58);
                harp.Note(b0, 3, h0, 58);
            }
            else if (sec == 2)
            {   // the harp walks in quarters, strings shimmer above, a bass finally arrives
                harp.Note(b0, 1.5, h0, 66).Note(b0 + 1, 1.5, h1, 54).Note(b0 + 2, 1.5, h2, 58).Note(b0 + 3, 1.5, h1, 60).Note(b0 + 4, 1.5, h2, 52).Note(b0 + 5, 1.5, h1, 50);
                if (k % 2 == 1) cel.Note(b0 + 1, 2, t1, 48);
                shim.At(bar).Play("!p " + c.top[1] + "+" + c.top[0] + ":6"); shim.Notes[^1].P -= 12; shim.Notes[^2].P -= 12;
                bass.At(bar).Play("!p " + c.bass + ":6");
            }
            else if (k < 30)
            {   // A and D: a bass note and its fifth, one celesta drop
                harp.Note(b0, 2, h0, 56).Note(b0 + 1, 3, h1, 46);
                if (k % 2 == 0) cel.Note(b0 + 2, 3, t1, 46); else cel.Note(b0 + 4, 2, t2, 40);
            }
            else cel.Note(b0 + 1, 3, t1, 40);
        }
        var violin = Solo(s, "violin", 40, -21, 0.2, -5);
        violin.At(5).Play("!mp c#5:3 | a4:2 f#4:1 | a4:3 | r:3 | c#5:2 e5:1 | f#5:3 | e5:2 c#5:1 | a4:3 | b4:2 e5:1 | g#4:3 | e4:1 g#4:2 | c#5:3 |")
              .Play("!mp d5:3 | f#5:2 d5:1 | c#5:3 | a4:3 | f#5:2 e5:1 | d5:3 | e5:2 b4:1 | g#4:3 | b4:1 d5:1 f#5:1 | b5:3 | a5:2 f#5:1 | d5:3 | e5:2 c#5:1 | g#4:3 | c#5:3 | r:3 |")
              .Play("!mf f#5:2 a5:1 | a5:2 f#5:1 | g#5:2 e5:1 | b4:3 | c#5:1 f#5:1 a5:1 | g#5:2 f#5:1 | c#5:3 | r:3 | d5:2 f#5:1 | a5:3 | e5:2 a5:1 | c#6:3 | b5:2 f#5:1 | d5:3 | e5:2 c#5:1 | g#4:3 |")
              .At(51).Play("!p c#5:3 | a4:2 f#4:1 | a4:3 |").At(57).Play("!pp g#4:3 | c#5:3 | b4:3 |");
        var viola = Solo(s, "viola", 41, -23, -0.25, -5);
        viola.At(25).Play("!pp f#4:6 | f#4:6 | e4:6 | e4:3 r:3 |")
             .Play("!mp a4:3 | f#4:3 | e4:3 | g#4:3 | a4:3 | c#5:2 a4:1 | f#4:2 g#4:1 | a4:3 | f#4:3 | d4:2 f#4:1 | a4:3 | e4:3 | d4:2 f#4:1 | b4:3 | g#4:3 | e4:3 |");
        var bowed = s.Dsp("bowedglass", Syn.Bowed(RGlass, 3, 4, 0.15, 0.6), -33, 0, -2);
        bowed.Note(0, 24, 78, 90).Note(s.BarBeat(33), 24, 85, 80).Note(s.BarBeat(49), 30, 78, 90);
        return s;
    }

    static Score FrostCombat()
    {
        var s = FrostNew("frost_combat"); const int T = 42; var sc = Aeolian;
        var vc = Stac(s, "celli", 42, -21, -0.2, -11, 0.13); var va = Stac(s, "violas", 41, -24.5, 0.25, -10, 0.12); var cb = Stac(s, "basses", 43, -22.5, 0.1, -13, 0.28);
        var pz = Stac(s, "pizz", 45, -23.5, 0.35, -8, 0.25);
        var taiko = Perc(s, "taiko", 116, -20.5, 0, -10); var tick = DrumSyn(s, "ticks", -31, -10); var ice = DrumSyn(s, "icemetal", -27, -6); var big = DrumSyn(s, "bigdrum", -22, -13); var timp = Perc(s, "timpani", 47, -24, 0.15, -9);
        var wood = s.Kit("woodblock", 0, -28.5, -0.3, -9); Tame(taiko, tick, ice, big, timp, wood);
        int[] dg = { 0, 0, 0, 2, 0, 0, 1, 0, 0, 0, -1, 0 };
        for (int b = 0; b < 64; b++)
        {
            int r = FrostCh[FrostPlan[b / 2]].root, sec = b / 16; double bb = b * 3; bool fill = b % 4 == 3, thin = b == 30 || b == 31;
            double v = sec == 0 ? 94 : sec == 2 ? 104 : 98; int tp = FoldTo(Deg(sc, T, r), 40);
            Fig(cb, bb, sc, T, r, L(12, "X.....x....."), One, 0.25, v, 0.5, 28);
            Pat(taiko, bb, L(12, fill ? "X.....x.xxxx" : "X.....x..x.."), 38, thin ? 82 : 98);
            if (thin) { if (b == 31) big.Note(bb, 3, 52, 100); continue; }
            Fig(vc, bb, sc, T, r, L(12, "X.xxX.xxX.xx"), dg, 0.25, v, -1, 36);
            Fig(va, bb, sc, T, r, L(12, "..x...x...x."), new[] { 4 }, 0.25, v - 10, -1, 48);
            // pizzicato in dotted eighths: four against the bar's three, like ice ticking
            Fig(pz, bb, sc, T, r, L(12, "x..x..x..x.."), new[] { 7, 0, 0, 4, 0, 0, 7, 0, 0, 4, 0, 0 }, 0.25, v - 8, -1, 60);
            Pat(tick, bb, L(12, "x.x.x.x.x.x."), 42, 70); if (b % 4 == 0) ice.Note(bb, 1, 44, 96); if (b % 4 == 2) ice.Note(bb, 1, 40, 80);
            Pat(wood, bb, L(12, "..x..x..x..x"), 76, 70);
            Pat(big, bb, L(12, "X..........."), 36, 102); if (sec >= 1) Pat(big, bb, L(12, "......x....."), 36, 80);
            timp.Note(bb, 1.5, tp, 100); if (fill) Roll(timp, bb + 2, 1, tp, 60, 100);
        }
        return s;
    }

    // =====================================================================================================
    //  SLUMBERING CATACOMBS - C Phrygian, 72 BPM, 4/4, 36 bars (2:00). Hollow: a breathy pipe pad in bare fifths,
    //  low choir drones, bells. The cello line keeps falling back through Db to C (the Phrygian cadence).
    //  Intro (1-4), A cello (5-12), B viola + choir (13-20), C duet (21-28), D bells and fragments (29-36).
    // =====================================================================================================
    // root per bar as a degree above C: 0=C 1=Db 2=Eb 3=F 5=Ab 6=Bb
    static readonly int[] CryptRoots = { 0,0,0,0,  0,0,1,0,6,5,1,0,  3,0,1,2,3,5,6,0,  5,6,0,0,1,6,1,0,  0,0,0,0,0,0,0,0 };
    static Score CryptNew(string n) => new Score(n, 72, 36) { RevTime = 5.5, RevDamp = 3200, RevSize = 1.2 };

    static Score CryptBase()
    {
        var s = CryptNew("crypt_base");
        var drone = s.Dsp("drone", Syn.Drone(110, 300, 6, 0.6, 5), -28.5, 0, -14); drone.Note(0, 144, 36, 100).Note(0, 144, 43, 60);
        var lowch = s.Dsp("lowchoir", Syn.Choir(Sfx.VU, Sfx.VO, 3, 1.0, 9, 1.5, 2.5), -24.5, 0, -3);
        lowch.At(1).Play("!mp c2+g2:16 |")
             .Play("c2+g2:8 | db2+ab2:4 | c2+g2:4 | bb1+f2:4 | ab1+eb2:4 | db2+ab2:4 | c2+g2:4 |")
             .Play("!mf f2+c3:4 | c2+g2:4 | db2+ab2:4 | eb2+bb2:4 | f2+c3:4 | ab1+eb2:4 | bb1+f2:4 | c2+g2:4 |")
             .Play("!mf ab1+eb2:4 | bb1+f2:4 | c2+g2:8 | db2+ab2:4 | bb1+f2:4 | db2+ab2:4 | c2+g2:4 |")
             .Play("!p c2+g2:4 | c2+ab2:4 | c2+g2:8 | c2+ab2:4 | c2+g2:12 |");
        var breath = s.Dsp("pipes", Syn.Breath(40, 1.5, 2.5), -28, 0, -4);
        breath.At(1).Play("!mp c3+g3:16 |").Play("c3+g3:8 | db3+ab3:4 | c3+g3:4 | bb2+f3:4 | ab2+eb3:4 | db3+ab3:4 | c3+g3:4 |")
              .At(29).Play("!mp c3+g3:4 | db3+ab3:4 | c3+g3:8 | db3+ab3:4 | c3+g3:12 |");
        var choir = PadS(s, "choir", 52, -25, 0, -3);
        choir.At(13).Play("!mp f3+ab3+c4:4 | eb3+g3+c4:4 | f3+ab3+db4:4 | g3+bb3+eb4:4 | ab3+c4+f4:4 | ab3+c4+eb4:4 | f3+bb3+db4:4 | g3+c4:4 |")
             .Play("!mf ab3+c4+eb4:4 | bb3+db4+f4:4 | g3+c4+eb4:8 | ab3+db4+f4:4 | bb3+db4+f4:4 | ab3+db4+f4:4 | g3+c4+eb4:4 |");
        var cello = Solo(s, "cello", 42, -20.5, -0.15);
        cello.At(5).Play("!mp c3:2 eb3:1 g3:1 | g3:2.5 f3:.5 eb3:1 | f3:2 ab3:2 | g3:3 r:1 | f3:1 bb3:1 db4:2 | c4:1.5 bb3:.5 ab3:2 | ab3:1 f3:1 db3:2 | c3:4 |")
             .At(21).Play("!mp ab2:2 c3:2 | bb2:2 db3:2 | c3:2 eb3:2 | g2:2 c3:2 | db3:2 f3:2 | bb2:2 db3:2 | db3:2 ab2:2 | c3:4 |")
             .At(30).Play("!p r:2 ab3:2 | g3:4 |").At(33).Play("!p f3:2 db3:2 | c3:4 |");
        var viola = Solo(s, "viola", 41, -21.5, 0.2);
        viola.At(13).Play("!mf ab3:2 c4:2 | eb4:3 c4:1 | db4:2 f4:2 | eb4:1 g4:1 bb4:2 | ab4:2 f4:1 c4:1 | eb4:2 c4:2 | db4:1.5 c4:.5 bb3:2 | c4:2 g3:2 |")
             .Play("!mf c5:2 ab4:2 | bb4:2 f4:2 | g4:3 eb4:1 | c4:4 | f4:1 ab4:1 db5:2 | c5:1.5 bb4:.5 f4:2 | ab4:2 f4:1 db4:1 | c4:4 |");
        var bass = s.Sf("bass", 43, -27, 0, -13); bass.Swell = (-8, 0, -8, 0.4);
        bass.At(13).Play("!p f2:4 | c2:4 | db2:4 | eb2:4 | f2:4 | ab1:4 | bb1:4 | c2:4 | ab1:4 | bb1:4 | c2:8 | db2:4 | bb1:4 | db2:4 | c2:4 |");
        var tub = Perc(s, "bells", 14, -26, -0.25, -3);
        foreach (var (bar, beat, p, v) in new[] { (3, 0, "g4", 62), (7, 0, "ab4", 58), (8, 0, "g4", 54), (12, 0, "c4", 66), (16, 2, "bb4", 60), (20, 0, "g4", 70), (24, 0, "c5", 74), (27, 2, "db5", 64), (28, 0, "c5", 78), (31, 0, "g4", 56), (35, 0, "c4", 50) })
            tub.Note(s.BarBeat(bar, beat), 4, Part.Pitch(p), v);
        var toll = s.Dsp("deepbell", Syn.Bell(7, 0.65), -26.5, 0.1, -3);
        foreach (var (bar, p, v) in new[] { (1, 48, 104), (13, 48, 92), (21, 44, 98), (29, 48, 104), (33, 48, 74) }) toll.Note(s.BarBeat(bar), 4, p, v);
        return s;
    }

    static Score CryptCombat()
    {
        var s = CryptNew("crypt_combat"); const int T = 36; var sc = Phrygian;
        var vc = Stac(s, "celli", 42, -21, -0.2, -11, 0.16); var va = Stac(s, "violas", 41, -24.5, 0.25, -10, 0.14); var cb = Stac(s, "basses", 43, -22.5, 0.1, -13, 0.3);
        var pulse = s.Dsp("pulse", Syn.Pulse(900, 0.13), -27.5, 0, -16);
        var taiko = Perc(s, "taiko", 116, -20.5, 0, -10); var bones = s.Kit("bones", 0, -27, 0.3, -8); var big = DrumSyn(s, "bigdrum", -22, -13); var metal = DrumSyn(s, "anvil", -27, -6);
        var timp = Perc(s, "timpani", 47, -23.5, 0.15, -9);
        Tame(taiko, bones, big, metal, timp);
        int[] dg = { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, -1, 0, 0, 2, 1, 0 };
        for (int b = 0; b < 36; b++)
        {
            int r = CryptRoots[b]; double bb = b * 4; bool fill = b % 4 == 3, thin = b == 19;
            double v = b < 4 ? 90 : b >= 20 && b < 28 ? 106 : b >= 28 ? 96 : 100; int tp = FoldTo(Deg(sc, T, r), 40);
            Fig(cb, bb, sc, T, r, L(16, "X.....X.X.....X."), One, 0.25, v, 0.5, 28);
            Pat(taiko, bb, L(16, fill ? "X..x..x.X..xxxxx" : "X..x..x.X..x..x."), 38, thin ? 84 : 98);
            if (thin) { big.Note(bb, 4, 52, 104); Roll(timp, bb + 2, 2, tp, 50, 100); continue; }
            // 3+3+2 accents over running sixteenths
            Fig(vc, bb, sc, T, r, L(16, "X.xX.xX.X.xX.xXx"), dg, 0.25, v, -1, 36);
            Fig(va, bb, sc, T, r, L(16, "..x...x...x...x."), new[] { 4 }, 0.25, v - 10, -1, 48);
            if (b >= 4) Fig(pulse, bb, sc, T, r, L(16, "X.xX.xX.X.xX.xXx"), dg, 0.25, 84, -1, 36);
            Pat(bones, bb, L(16, "..x.....x.x....."), 75, 84); Pat(bones, bb, L(16, "......x.......x."), 77, 72);
            Pat(big, bb, L(16, "X.......X......."), 36, 102); if (b % 4 == 0) big.Note(bb, 1, 46, 96);
            if (b % 2 == 0) metal.Note(bb, 1, 44, 92);
            timp.Note(bb, 2, tp, 100); if (b % 2 == 1) timp.Note(bb + 2.5, 1, tp, 84);
        }
        return s;
    }

    // =====================================================================================================
    //  WEEPING MIRE - A Phrygian, 80 BPM, 5/4 (3+2), 32 bars (2:00). Murky: a low choir tuned a third of a
    //  semitone apart, a bassoon that never quite climbs out, dull string pads, drops of wet percussion.
    //  A bassoon (1-8), B bassoon higher (9-16), C clarinet over bassoon (17-24), D fragments (25-32).
    // =====================================================================================================
    // root per bar as a degree above A: 0=A 1=Bb 3=D 4=E 5=F 6=G
    static readonly int[] MireRoots = { 0,0,0,0,3,0,1,0,  5,6,0,0,3,5,1,0,  3,3,1,5,6,3,4,0,  0,0,0,0,5,1,0,0 };
    static Score MireNew(string n) => new Score(n, 80, 32, 5) { RevTime = 2.8, RevDamp = 2600, RevSize = 0.9 };

    static Score MireBase()
    {
        var s = MireNew("mire_base");
        var drone = s.Dsp("drone", Syn.Drone(90, 240, 14, 0.7, 5), -27.5, 0, -14); drone.Note(0, 160, 33, 100).Note(0, 160, 40, 60);
        var lowch = s.Dsp("lowchoir", Syn.Choir(Sfx.VU, Sfx.VO, 4, 0.92, 32, 1.6, 2.6, 0.1), -23.5, 0, -5);
        lowch.At(1).Play("!mp a1+e2:10 | a1+f2+bb2:5 | a1+e2:5 | d2+a2:5 | a1+e2:5 | bb1+f2:5 | a1+e2:5 |")
             .Play("!mf f2+c3:5 | g2+d3:5 | a1+e2:10 | d2+a2:5 | f2+c3:5 | bb1+f2:5 | a1+e2:5 |")
             .Play("!mf d2+a2:10 | bb1+f2:5 | f2+c3:5 | g2+d3:5 | d2+a2:5 | e2+bb2:5 | a1+e2:5 |")
             .Play("!mp a1+e2:5 | a1+f2+bb2:5 | a1+e2:10 | f2+c3:5 | bb1+f2:5 | a1+e2:10 |");
        var str = PadS(s, "strings", 49, -27, 0, -6); str.Lp = 1400;
        str.At(9).Play("!p f2+c3+a3:5 | g2+d3+bb3:5 | a2+e3+c4:10 | a2+d3+f3:5 | a2+c3+f3:5 | bb2+d3+f3:5 | a2+c3+e3:5 |")
           .Play("!mp a2+d3+f3:10 | bb2+d3+f3:5 | a2+c3+f3:5 | bb2+d3+g3:5 | a2+d3+f3:5 | bb2+d3+e3+g3:5 | a2+c3+e3:5 |");
        var bsn = Solo(s, "bassoon", 70, -20.5, -0.15, -8);
        bsn.At(3).Play("!mp bb2:3 d3:2 | c3:2 a2:3 | d3:1 f3:2 e3:2 | e3:3 c3:2 | d3:1.5 c3:.5 bb2:1 f3:2 | e3:5 |")
           .Play("!mf f3:3 a3:2 | g3:2 bb3:1 d4:2 | c4:3 a3:2 | e3:5 | f3:1 a3:2 d4:2 | c4:3 a3:2 | bb3:1.5 a3:.5 f3:1 d3:2 | e3:3 a2:2 |")
           .Play("!mp d3:5 | d3:3 a2:2 | bb2:5 | f2:3 a2:2 | g2:3 bb2:2 | d3:3 f3:2 | e3:3 bb2:2 | a2:5 |")
           .At(26).Play("!p bb2:3 d3:2 | c3:2 a2:3 |").At(29).Play("!p r:3 c3:2 | d3:3 bb2:2 | a2:5 |");
        var cl = Solo(s, "clarinet", 71, -21.5, 0.2, -7);
        cl.At(17).Play("!mf a3:3 d4:2 | f4:2 e4:1 d4:2 | d4:3 bb3:2 | c4:2 a3:3 | bb3:1 d4:2 g4:2 | f4:3 d4:2 | e4:1.5 d4:.5 bb3:1 g3:2 | a3:5 |");
        var pizz = Plk(s, "basspizz", 45, -26.5, 0.1, -9); pizz.Legato = 0.2;
        var vib = Plk(s, "vibes", 11, -30, 0.4, -2);
        var wet = DrumSyn(s, "wetperc", -31, -6); var rnd = new Rng("music/mire/drops");
        for (int b = 0; b < 32; b++)
        {
            int root = FoldTo(Deg(Phrygian, 33, MireRoots[b]), 40); double bb = b * 5; bool outer = b < 8 || b >= 24;
            if (!outer || b % 2 == 0) pizz.Note(bb, 2, root, 70);
            if (!outer) pizz.Note(bb + 3, 1.5, root + (MireRoots[b] == 4 ? 6 : 7), 58);   // the bar's 3+2 split; E takes its flat fifth
            if (b % 2 == 0) wet.Note(bb, 1, 48, 74);
            int drops = rnd.I(1, 2); for (int k = 0; k < drops; k++) wet.Note(bb + rnd.I(20) * 0.25, 0.5, 50, rnd.R(40, 72));
        }
        foreach (var (bar, beat, p, v) in new[] { (2, 3, 76, 54), (4, 1, 82, 44), (6, 3, 76, 50), (8, 2, 84, 46), (16, 3, 76, 50), (25, 3, 76, 54), (27, 1, 82, 44), (28, 3, 81, 48), (32, 2, 76, 46) })
            vib.Note(s.BarBeat(bar, beat), 3, p, v);
        return s;
    }

    static Score MireCombat()
    {
        var s = MireNew("mire_combat"); const int T = 33; var sc = Phrygian;
        var vc = Stac(s, "celli", 42, -21, -0.2, -11, 0.15); var va = Stac(s, "violas", 41, -24.5, 0.25, -10, 0.13); var cb = Stac(s, "basses", 43, -22.5, 0.1, -13, 0.3);
        var pulse = s.Dsp("pulse", Syn.Pulse(800, 0.13), -27.5, 0, -16);
        var taiko = Perc(s, "taiko", 116, -21, 0, -10); var wet = DrumSyn(s, "wetperc", -23.5, -10); var big = DrumSyn(s, "bigdrum", -23, -13); var tom = s.Kit("toms", 0, -25, 0, -10);
        var timp = Perc(s, "timpani", 47, -24, 0.15, -9);
        Tame(taiko, wet, big, tom, timp);
        int[] dg = { 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 1, 0, 0, 0, 0, -1, 0, 0, 1, 0 };
        for (int b = 0; b < 32; b++)
        {
            int r = MireRoots[b]; double bb = b * 5; bool fill = b % 4 == 3;
            double v = b < 8 ? 92 : b >= 16 && b < 24 ? 104 : 98; int tp = FoldTo(Deg(sc, T, r), 40);
            Fig(vc, bb, sc, T, r, L(20, "X.xxx.xxx.xxX.xxx.xx"), dg, 0.25, v, -1, 36);
            Fig(va, bb, sc, T, r, L(20, "..x...x...x...x...x."), new[] { 4 }, 0.25, v - 10, -1, 48);
            Fig(cb, bb, sc, T, r, L(20, "X.........x.X.....x."), One, 0.25, v, 0.5, 28);
            if (b >= 8) Fig(pulse, bb, sc, T, r, L(20, "X.xxx.xxx.xxX.xxx.xx"), dg, 0.25, 84, -1, 36);
            Pat(wet, bb, L(20, "X...........X......."), 48, 98); Pat(wet, bb, L(20, "..x..x....x...x..x.."), 50, 80); Pat(wet, bb, L(20, "x.x.x.x.x.x.x.x.x.x."), 42, 50);
            Pat(big, bb, L(20, "X...........x......."), 36, 100);
            Pat(taiko, bb, L(20, fill ? "X.....x.....X...xxxx" : "X.....x.....X...x..."), 38, 96);
            if (b % 8 == 7) Pat(tom, bb, L(20, "................x.x."), 43, 90);
            timp.Note(bb, 2, tp, 98); timp.Note(bb + 3, 1.5, tp, 84);
        }
        return s;
    }

    static void Zones1()
    {
        AddPair("wastes", "Ashen Wastes: wind, open-fifth drone, lonely viola answered by cello, sparse harp / combat layer: galloping low strings, taiko, frame drums", "E Aeolian", WastesBase, WastesCombat);
        AddPair("frost", "Frostspine Pass: glass pad, high solo violin, celesta drops, harp / combat layer: string ostinato in three, pizzicato cross-rhythm, icy metal and wood", "F# Aeolian", FrostBase, FrostCombat);
        AddPair("crypt", "Slumbering Catacombs: hollow pipes, choir drones, bells, cello falling through the Phrygian second / combat layer: 3+3+2 strings, taiko, bone clacks, anvil", "C Phrygian", CryptBase, CryptCombat);
        AddPair("mire", "Weeping Mire: detuned low choir, bassoon and clarinet, dull strings, wet drops, in five / combat layer: low-string ostinato in 3+2, wet percussion, taiko", "A Phrygian", MireBase, MireCombat);
    }
}
