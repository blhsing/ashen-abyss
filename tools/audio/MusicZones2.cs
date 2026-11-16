// Original compositions (3): adaptive zone pairs for the Rustsand Ruins, the Hushed Sanctum and the Ember Abyss.
// Same contract as MusicZones.cs: base and combat share one grid and one bar-by-bar harmony.
// All note data is written here by hand; nothing is quoted from existing music.
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public static partial class Music
{
    // =====================================================================================================
    //  RUSTSAND RUINS - E Phrygian, 104 BPM, 4/4, 52 bars (2:00). Dry: almost no reverb, a low plucked riff that
    //  leans on the flat second, a lonely English horn above it, a dulcimer that answers, a quiet hand drum.
    //  Cadences brighten to E major (the raised third) and fall straight back.
    //  Intro (1-4), A horn (5-12), B horn higher (13-20), C plucked duet (21-28), D horn (29-36), E climax (37-44), F riff (45-52).
    // =====================================================================================================
    // root per bar as a degree above E: 0=E 1=F 2=G 3=A 5=C 6=D
    static readonly int[] SandsRoots = { 0,0,0,0,  0,0,1,0,3,2,1,0,  3,3,6,2,5,1,6,0,  0,1,2,3,1,2,3,0,  0,0,1,0,3,2,1,0,  5,6,0,3,1,2,1,0,  0,0,0,0,0,0,0,0 };
    static bool SandsFlat2(int b) => b == 2 || b == 45 || b == 48;   // bars where F sounds over the E pedal
    static Score SandsNew(string n) => new Score(n, 104, 52) { RevTime = 1.9, RevDamp = 5000, RevSize = 0.7, Tail = 6 };

    static Score SandsBase()
    {
        var s = SandsNew("sands_base"); var sc = Phrygian; const int T = 40;
        if (SandsRoots.Length != 52) throw new FormatException("sands roots " + SandsRoots.Length);
        s.Dsp("sandhiss", Syn.Bed((r, d) => White(r, d).SvfBP(Wander(r, 4000, 8000, 0.3, d, true), 0.8).Env(Wander(r, 0.05, 1, 0.3, d))), -37, 0, -30);
        var drone = s.Dsp("drone", Syn.Drone(160, 500, 5, 0.3, 5), -31, 0, -16); drone.Note(0, 208, 40, 100).Note(0, 208, 47, 60);
        var gtr = Plk(s, "guitar", 24, -22, -0.25, -10); gtr.Legato = 0.4;
        var hand = DrumSyn(s, "handdrum", -28.5, -14); Tame(hand);
        for (int b = 0; b < 52; b++)
        {
            double bb = b * 4; bool edge = b < 4 || b >= 44, turn = b % 4 == 3 && !edge;
            // the riff: root, fifth, octave, the note under the octave, octave
            Fig(gtr, bb, sc, T, SandsRoots[b], L(8, turn ? "X.xx.xxx" : "X.xx.xx."), new[] { 0, 0, 4, 7, 0, SandsFlat2(b) ? 8 : 6, 7, 4 }, 0.5, edge ? 70 : 78, 1.0, 40);
            if (edge) { Pat(hand, bb, L(16, "x..............."), 38, 76); if (b % 2 == 1) Pat(hand, bb, L(16, "............o..."), 40, 76); }
            else { Pat(hand, bb, L(16, "X.........x....."), 38, 78); Pat(hand, bb, L(16, "....x..o....x.o."), 40, 72); }
        }
        var horn = Solo(s, "english_horn", 69, -20.5, 0.1, -9); horn.TimeJit = 0.015;
        horn.At(5).Play("!mf b4:2 e5:2 | d5:1 e5:.5 d5:.5 b4:2 | c5:2 a4:1 c5:1 | b4:3 r:1 | a4:1 c5:1 e5:2 | d5:1.5 c5:.5 b4:2 | a4:1 c5:1 a4:1 f4:1 | e4:4 |")
            .Play("!mf e5:2 a5:2 | g5:1 a5:.5 g5:.5 e5:2 | f5:2 d5:1 f5:1 | d5:3 r:1 | c5:1 e5:1 g5:2 | f5:1.5 e5:.5 c5:2 | d5:1 f5:1 e5:1 d5:1 | e5:1 d5:.5 c5:.5 b4:2 |")
            .At(29).Play("!mf b4:2 e5:2 | g5:1 f5:.5 e5:.5 d5:2 | c5:2 f5:1 a5:1 | g5:3 r:1 | e5:1 c5:1 a4:2 | b4:1.5 c5:.5 d5:2 | c5:1 a4:1 f4:2 | e4:4 |")
            .Play("!f g5:2 e5:1 g5:1 | a5:2 f5:2 | g5:1 a5:.5 g5:.5 e5:2 | e5:3 r:1 | a5:2 f5:1 c5:1 | d5:1 g5:2 f5:1 | f5:1 e5:.5 d5:.5 c5:1 a4:1 | g#4:1 b4:1 e5:2 |")
            .At(47).Play("!p r:2 b4:2 | e5:3 r:1 | f5:1.5 e5:.5 c5:2 | b4:4 |");
        var dul = Plk(s, "dulcimer", 15, -23.5, 0.3, -9);
        dul.At(8).Play("!p r:2.5 b4:.5 e5:1 |").At(12).Play("!p r:1 e4:.5 g#4:.5 b4:2 |").At(16).Play("!p r:3 d5:.5 b4:.5 |").At(20).Play("!p r:2 e4:1 b4:1 |")
           .At(21).Play("!mf e4:.5 g4:.5 b4:1 a4:.5 g4:.5 e4:1 | f4:.5 a4:.5 c5:1 a4:1 f4:1 | g4:.5 b4:.5 d5:1 c5:.5 b4:.5 g4:1 | a4:1 c5:1 e5:2 | c5:.5 a4:.5 f4:1 a4:1 c5:1 | d5:.5 b4:.5 g4:1 b4:1 d5:1 | e5:1 c5:1 a4:1 c5:1 | b4:1 g#4:1 e4:2 |")
           .At(32).Play("!p r:3 b4:.5 e5:.5 |").At(36).Play("!p r:1 e4:.5 g#4:.5 b4:2 |").At(44).Play("!p r:2.5 b4:.5 e5:1 |").At(51).Play("!pp e4:1 b4:1 e5:2 |");
        var harp = Plk(s, "harp", 46, -25, -0.35, -9);
        string[][] arp = { new[] { "c3", "g3", "c4", "e4", "g4" }, new[] { "d3", "a3", "d4", "f4", "a4" }, new[] { "e3", "b3", "e4", "g4", "b4" }, new[] { "a2", "e3", "a3", "c4", "e4" },
                           new[] { "f3", "c4", "f4", "a4", "c5" }, new[] { "g2", "d3", "g3", "b3", "d4" }, new[] { "f3", "c4", "f4", "a4", "c5" }, new[] { "e3", "b3", "e4", "g#4", "b4" } };
        for (int k = 0; k < 8; k++) { int[] ord = { 0, 1, 2, 3, 4, 3, 2, 1 }; for (int i = 0; i < 8; i++) harp.Note((36 + k) * 4 + i * 0.5, 1, Part.Pitch(arp[k][ord[i]]), i == 0 ? 70 : 56); }
        var str = PadS(s, "strings", 49, -28.5, 0, -9);
        str.At(29).Play("!pp e3+b3:16 | e3+b3:16 |").Play("!mp c3+g3+e4:4 | d3+a3+f4:4 | e3+b3+g4:4 | e3+a3+c4:4 | f3+a3+c4:4 | g3+b3+d4:4 | f3+a3+c4:4 | e3+g#3+b3:4 |");
        return s;
    }

    static Score SandsCombat()
    {
        var s = SandsNew("sands_combat"); const int T = 40; var sc = Phrygian;
        var vc = Stac(s, "celli", 42, -21, -0.2, -13, 0.13); var cb = Stac(s, "basses", 43, -22.5, 0.1, -15, 0.28); var pz = Stac(s, "pizz", 45, -23.5, 0.25, -12, 0.3);
        var dar = DrumSyn(s, "goblet_drum", -21.5, -13); var bon = s.Kit("bongos", 0, -25.5, 0.3, -12); var big = DrumSyn(s, "bigdrum", -22.5, -14); var taiko = Perc(s, "taiko", 116, -22.5, 0, -12);
        Tame(dar, bon, big, taiko);
        int[] dg = { 0, 0, 0, 1, 0, 0, 2, 1, 0, 0, 0, 1, 0, -1, 0, 1 };
        for (int b = 0; b < 52; b++)
        {
            int r = SandsRoots[b]; double bb = b * 4; bool fill = b % 4 == 3, thin = b == 27;
            double v = b < 4 ? 90 : b >= 36 && b < 44 ? 106 : b >= 44 ? 94 : 100;
            Fig(cb, bb, sc, T, r, L(16, "X.....x.X.....x."), One, 0.25, v, 0.5, 28);
            Pat(dar, bb, L(16, "X.....X...X....."), 38, 96); Pat(dar, bb, L(16, fill ? "..x.x...x...xxxx" : "..x.x...x...x.xx"), 40, 86);
            if (thin) { big.Note(bb, 4, 52, 100); continue; }
            Fig(vc, bb, sc, T, r, L(16, "X.xxX.xxX.xxX.xx"), dg, 0.25, v, -1, 36);
            Fig(pz, bb, sc, T, r, L(16, "X..x..x...x.x..."), new[] { 0, 0, 0, 4, 0, 0, 7, 0, 0, 0, 6, 0, 7, 0, 0, 0 }, 0.25, v - 8, -1, 48);
            Pat(dar, bb, L(16, "....X.......X..."), 54, 90);
            Pat(bon, bb, L(16, "x.xx.xx.x.xx.xx."), 60, 72); Pat(bon, bb, L(16, ".......x.......x"), 61, 78); Pat(bon, bb, L(16, "x.x.x.x.x.x.x.x."), 54, 58);
            Pat(big, bb, L(16, "X.......x......."), 36, 100);
            Pat(taiko, bb, L(16, fill ? "X.....x...x.xxxx" : "X.....x...x....."), 38, 94);
        }
        return s;
    }

    // =====================================================================================================
    //  HUSHED SANCTUM - G Aeolian, 64 BPM, 4/4, 32 bars (2:00). A slow chorale for organ and wordless choir with
    //  bells tolling through it; the harmony moves in half bars and bows to A-flat (the Phrygian second) before
    //  each close. A voices humming (1-8), B full choir (9-16), C highest and darkest (17-24), D the first
    //  phrase again with a single high voice above it (25-32).
    // =====================================================================================================
    // root per HALF bar as a degree above G: 0=G 2=Bb 3=C 4=D 5=Eb 6=F, 10 = A-flat (outside the mode)
    static readonly int[] SanctumA = { 0,0, 5,6, 0,2, 6,4, 5,0, 10,0, 3,4, 0,0 }, SanctumB = { 2,4, 3,0, 6,2, 5,5, 3,10, 0,6, 5,3, 4,4 }, SanctumC = { 0,0, 2,5, 0,10, 0,0, 3,2, 10,5, 3,4, 0,0 };
    static readonly int[] SanctumRoots = SanctumA.Concat(SanctumB).Concat(SanctumC).Concat(SanctumA).ToArray();
    const string SanChA = "bb3+d4+g4:4 | eb4+g4+bb4:2 c4+f4+a4:2 | d4+g4+bb4:2 f4+bb4+d5:2 | f4+a4+c5:2 d4+f4+a4:2 | bb3+eb4+g4:2 d4+g4+bb4:2 | eb4+ab4+c5:2 d4+g4+bb4:2 | c4+eb4+g4:2 d4+f4+a4:2 | bb3+d4+g4:4 | ";
    const string SanChB = "f4+bb4+d5:2 a4+d5+f5:2 | g4+c5+eb5:2 g4+bb4+d5:2 | f4+a4+c5:2 f4+bb4+d5:2 | eb4+g4+bb4:4 | eb4+g4+c5:2 ab4+c5+eb5:2 | g4+bb4+d5:2 f4+a4+c5:2 | eb4+g4+bb4:2 eb4+g4+c5:2 | d4+g4+a4:2 d4+f4+a4:2 | ";
    const string SanChC = "bb4+d5:2 bb4+d5+g5:2 | bb4+d5+f5:2 g4+bb4+eb5:2 | g4+bb4+d5:2 eb4+ab4+c5:2 | g4+bb4+d5:4 | g4+c5+eb5:2 f4+bb4+d5:2 | eb4+ab4+c5:2 eb4+g4+bb4:2 | eb4+g4+c5:2 d4+f4+a4:2 | bb3+d4+g4:4 | ";
    const string SanBsA = "g2:4 | eb2:2 f2:2 | g2:2 bb2:2 | f2:2 d2:2 | eb2:2 g2:2 | ab2:2 g2:2 | c3:2 d3:2 | g2:4 | ";
    const string SanBsB = "bb2:2 d3:2 | c3:2 g2:2 | f2:2 bb2:2 | eb2:4 | c3:2 ab2:2 | g2:2 f2:2 | eb2:2 c2:2 | d2:4 | ";
    const string SanBsC = "g2:4 | bb2:2 eb2:2 | g2:2 ab2:2 | g2:4 | c3:2 bb2:2 | ab2:2 eb2:2 | c3:2 d3:2 | g2:4 | ";
    static Score SanctumNew(string n) => new Score(n, 64, 32) { RevTime = 6.5, RevDamp = 3600, RevSize = 1.3, Tail = 11 };

    static Score SanctumBase()
    {
        var s = SanctumNew("sanctum_base");
        var drone = s.Dsp("drone", Syn.Drone(120, 340, 6, 0.6, 5), -31, 0, -14); drone.Note(0, 128, 31, 100).Note(0, 128, 38, 60);
        var organ = s.Dsp("organ", Syn.Organ(null, 0.3, 1.2), -23.5, 0, -6);
        organ.At(1).Play("!p " + SanBsA + "!mp " + SanBsB + "!mf " + SanBsC + "!p " + SanBsA);
        organ.At(1).Play("!p " + SanChA + "!mp " + SanChB + "!mf " + SanChC + "!p " + SanChA);
        var oohs = PadS(s, "choir_hum", 53, -25, 0, -3);
        oohs.At(1).Play("!p " + SanChA).At(17).Play("!mp " + SanChC).At(25).Play("!p " + SanChA);
        var aahs = PadS(s, "choir", 52, -23, 0, -3);
        aahs.At(9).Play("!mp " + SanChB + "!mf " + SanChC);
        var lowch = s.Dsp("lowchoir", Syn.Choir(Sfx.VU, Sfx.VO, 3, 1.0, 8, 0.7, 1.8), -25.5, 0, -4);
        lowch.At(1).Play("!mp " + SanBsA + "!mf " + SanBsB + SanBsC + "!mp " + SanBsA);
        var bass = s.Sf("bass", 43, -27, 0, -12); bass.Swell = (-8, 0, -8, 0.4); bass.Legato = 0.05;
        bass.At(9).Play("!p " + SanBsB + "!mp " + SanBsC);
        var pedal = s.Sf("organ_pedal", 19, -28, 0, -8); pedal.Transpose = -12; pedal.Legato = 0.05;
        pedal.At(17).Play("!mp " + SanBsC);
        var solo = Solo(s, "descant", 53, -23.5, 0.1, -3);
        solo.At(25).Play("!mp d5:4 | eb5:2 c5:2 | d5:2 f5:2 | f5:2 d5:2 | eb5:2 d5:2 | eb5:2 d5:2 | eb5:2 d5:2 | d5:4 |");
        var tub = Perc(s, "bells", 14, -25.5, -0.25, -2);
        for (int b = 1; b <= 32; b++)
        {
            bool c = b >= 17 && b <= 24;
            if (c) tub.Note(s.BarBeat(b), 4, b % 2 == 1 ? 55 : 62, 92);   // every bar in the third phrase
            else if (b % 2 == 1) tub.Note(s.BarBeat(b), 4, 55, b <= 8 ? 72 : 80);
            if (b % 8 == 4) tub.Note(s.BarBeat(b, 2), 4, 62, 62);
        }
        var toll = s.Dsp("deepbell", Syn.Bell(8, 0.7), -26, 0.15, -2);
        foreach (var (bar, v) in new[] { (1, 100), (9, 96), (17, 110), (21, 104), (25, 96) }) toll.Note(s.BarBeat(bar), 4, 43, v);
        return s;
    }

    static Score SanctumCombat()
    {
        var s = SanctumNew("sanctum_combat"); const int T = 43; const double st = 1 / 6.0;   // sextuplet steps: a compound pulse under the slow chorale
        var vc = Stac(s, "celli", 42, -21, -0.2, -11, 0.13); var va = Stac(s, "violas", 41, -24.5, 0.25, -10, 0.12); var cb = Stac(s, "basses", 43, -22.5, 0.1, -13, 0.3);
        var taiko = Perc(s, "taiko", 116, -21, 0, -10); var big = DrumSyn(s, "bigdrum", -22, -13); var timp = Perc(s, "timpani", 47, -22.5, 0.15, -9);
        var snare = s.Kit("snare", 48, -27, 0.2, -8); var bell = Perc(s, "bell", 14, -28, -0.2, -3);
        Tame(taiko, big, timp, snare);
        int[] dg = { 0, 0, 1, 0, 0, 0, 0, 2, 1, 0, 0, -1 };
        for (int hb = 0; hb < 64; hb++)
        {
            int r = SanctumRoots[hb], bar = hb / 2, sec = bar / 8; var scl = r == 10 ? Phrygian : Aeolian; int deg = r == 10 ? 1 : r;
            double bb = hb * 2; bool first = hb % 2 == 0, fill = bar % 4 == 3 && !first;
            double v = sec == 0 ? 92 : sec == 2 ? 106 : sec == 3 ? 96 : 100; int tp = FoldTo(Deg(scl, T, deg), 40);
            Fig(vc, bb, scl, T, deg, L(12, "XxxX.xXxxX.x"), dg, st, v, -1, 36);
            Fig(va, bb, scl, T, deg, L(12, "..x..x..x..x"), new[] { 2 }, st, v - 10, -1, 48);
            Fig(cb, bb, scl, T, deg, L(12, "X.....x....."), One, st, v, 0.6, 28);
            timp.Note(bb, 1, tp, first ? 102 : 90);
            Pat(taiko, bb, L(12, fill ? "X..x..xxxxxx" : first ? "X.....x..x.." : "x.....x.x..x"), 38, 96, st);
            Pat(snare, bb, L(12, "..x..x.xx..x"), 38, 58, st);
            if (first) { big.Note(bb, 1, 36, 118); if (bar % 4 == 0) big.Note(bb, 1, 46, 100); if (bar % 2 == 0) bell.Note(bb, 4, 55, 92); }
        }
        return s;
    }

    // =====================================================================================================
    //  THE EMBER ABYSS - D Phrygian with a flattened fifth, 96 BPM, 4/4, 48 bars (2:00). Menacing: bowed metal
    //  over a sub drone, one heavy drum that never hurries, low strings in unison. In the middle a solo cello
    //  plays the main theme upside down and twice as slow (the shape the final boss music is built on).
    //  A drone and drum (1-8), B low strings (9-16), C inverted theme (17-24), D full (25-32),
    //  E breakdown (33-40), F low strings again (41-48).
    // =====================================================================================================
    // root per bar as a degree above D: 0=D 1=Eb 2=F 3=G 5=Bb 6=C
    static readonly int[] AbyssRoots = { 0,0,0,0,0,0,0,0,  0,0,1,0,6,5,1,0,  0,1,2,3,0,5,6,6,  0,0,1,0,5,3,1,0,  0,0,0,0,0,0,0,0,  0,0,1,0,6,5,1,0 };
    const string AbyssLow = "d2:3 f2:1 | eb2:2 d2:2 | g2:2 bb2:2 | a2:3 r:1 | c3:2 eb3:1 g2:1 | bb2:2 d3:2 | eb3:1.5 d3:.5 bb2:1 g2:1 | a2:1 d2:3 |";
    static Score AbyssNew(string n) => new Score(n, 96, 48) { RevTime = 4.5, RevDamp = 3000, RevSize = 1.2 };

    static Score AbyssBase()
    {
        var s = AbyssNew("abyss_base"); var sc = Phrygian; const int T = 38;
        if (AbyssRoots.Length != 48) throw new FormatException("abyss roots " + AbyssRoots.Length);
        var drone = s.Dsp("drone", Syn.Drone(80, 260, 9, 0.8, 5), -26, 0, -14); drone.Note(0, 192, 38, 100).Note(0, 192, 45, 55);
        var metal = s.Dsp("bowed_metal", Syn.Bowed(RPlate, 2.5, 4, 0.3, 0.7), -25.5, 0, -4);
        metal.Note(0, 16, 50, 100).Note(16, 16, 56, 90).Note(32, 32, 50, 70).Note(s.BarBeat(21), 16, 45, 70).Note(s.BarBeat(25), 16, 62, 96).Note(s.BarBeat(29), 16, 63, 96)
             .Note(s.BarBeat(33), 16, 50, 104).Note(s.BarBeat(37), 16, 56, 104).Note(s.BarBeat(41), 32, 50, 80);
        var bar2 = s.Dsp("bowed_bar", Syn.Bowed(RBar, 3, 4, 0.2, 0.8), -32, 0, -2);
        bar2.Note(s.BarBeat(5), 12, 69, 90).Note(s.BarBeat(29), 12, 75, 90).Note(s.BarBeat(35), 16, 74, 90).Note(s.BarBeat(45), 12, 69, 90);
        var lowch = s.Dsp("lowchoir", Syn.Choir(Sfx.VO, Sfx.VU, 3, 0.9, 14, 1.2, 2.5), -25.5, 0, -4);
        lowch.At(1).Play("!mp d2+a2:8 | eb2+bb2:4 | d2+a2:4 | d2+a2:8 | eb2+bb2:4 | d2+a2:4 |")
             .At(25).Play("!f d3+a3:8 | eb3+bb3:4 | d3+a3:4 | bb2+f3:4 | g2+d3:4 | eb3+bb3:4 | d3+a3:4 |")
             .Play("!p d2+a2:16 | d2+ab2:8 | d2+a2:8 |")
             .Play("!mf d2+a2:8 | eb2+bb2:4 | d2+a2:4 | c2+g2:4 | bb1+f2:4 | eb2+bb2:4 | d2+a2:4 |");
        // B and F: the low-string line (ensemble for weight, solo cello for the edge, basses underneath)
        var lowstr = s.Sf("lowstrings", 48, -22.5, -0.1, -8); lowstr.Swell = (-6, 0, -5, 0.3); lowstr.Poly = true; lowstr.Legato = 0.08;
        var edge = s.Sf("celli", 42, -24, -0.3, -8); edge.Swell = (-5, 0, -5, 0.3); edge.Vib = 30; edge.Legato = 0.03;
        var basses = s.Sf("basses", 43, -24.5, 0.1, -12); basses.Swell = (-6, 0, -6, 0.3); basses.Legato = 0.05;
        foreach (var p in new[] { lowstr, edge, basses }) p.At(9).Play("!mf " + AbyssLow).At(41).Play("!mp " + AbyssLow);
        // C: the main theme inverted and augmented
        var cello = Solo(s, "cello_solo", 42, -20.5, -0.1, -5);
        cello.At(17).Play("!mf d4:4 | eb3:4 | f3:3 g3:1 | g3:4 | a3:2 g3:2 | bb3:4 | c4:4 | c4:2 r:2 |")
             .Play("!mf d3:4 | d3:2 c3:2 | bb2:2 eb3:2 | d3:4 | bb2:2 d3:2 | g2:2 bb2:2 | eb3:2 g3:2 | f3:2 d3:2 |")
             .At(37).Play("!pp r:2 d3:2 | eb3:3 d3:1 |");
        var pad = PadS(s, "strings", 49, -25, 0, -6);
        pad.At(17).Play("!mp d2+a2+d3+f3:4 | eb2+bb2+eb3+g3:4 | f2+c3+f3+a3:4 | g2+d3+g3+bb3:4 | d2+a2+d3+f3:4 | bb1+f2+bb2+d3:4 | c2+g2+c3+eb3:8 |")
           .Play("!mf d2+a2+d3+f3:8 | eb2+bb2+eb3+g3:4 | d2+a2+d3+f3:4 | bb1+f2+bb2+d3:4 | g2+d3+g3+bb3:4 | eb2+bb2+eb3+g3:4 | d2+a2+d3+f3:4 |");
        // D: the upper strings, leaning on A-flat
        var his = s.Sf("violins", 48, -23, 0.2, -7); his.Swell = (-6, 0, -5, 0.35); his.Poly = true; his.Legato = 0.1;
        var hiv = s.Sf("viola", 41, -23.5, 0.3, -7); hiv.Swell = (-5, 0, -5, 0.3); hiv.Vib = 40; hiv.Legato = 0.03;
        const string melD = "!f d4:2 f4:2 | ab4:3 g4:1 | g4:2 bb4:2 | a4:2 f4:2 | d5:3 bb4:1 | bb4:2 g4:2 | g4:1 bb4:1 eb5:2 | d5:4 |";
        his.At(25).Play(melD); hiv.At(25).Play(melD);
        var trem = PadS(s, "tremolo", 44, -27, 0.2, -5);
        trem.At(41).Play("!p d3+a3:8 | eb3+bb3:4 | d3+a3:4 | c3+g3:4 | bb2+f3:4 | eb3+bb3:4 | d3+a3:4 |");
        // the heavy drum
        var big = DrumSyn(s, "heavydrum", -22, -9); var taiko = Perc(s, "taiko", 116, -24, 0, -8); var timp = Perc(s, "timpani", 47, -25, 0.15, -8);
        var anvil = DrumSyn(s, "anvil", -29, -3); var bell = Perc(s, "bell", 14, -27, -0.2, -2);
        Tame(big, taiko, timp, anvil);
        for (int b = 0; b < 48; b++)
        {
            int sec = b / 8; double bb = b * 4; int tp = FoldTo(Deg(sc, T, AbyssRoots[b]), 38);
            switch (sec)
            {
                case 0:
                    if (b % 2 == 0) big.Note(bb, 1, 36, 112).Note(bb, 1, 46, 100); else taiko.Note(bb + 3.5, 0.5, 38, 70);
                    break;
                case 1: case 5:
                    if (b % 2 == 0) big.Note(bb, 1, 36, 108).Note(bb, 1, 46, 94); else taiko.Note(bb + 2.5, 0.5, 38, 64).Note(bb + 3.5, 0.5, 38, 76);
                    timp.Note(bb, 2, tp, 84);
                    break;
                case 2:
                    if (b % 4 == 0) { big.Note(bb, 1, 36, 108).Note(bb, 1, 46, 96); anvil.Note(bb, 1, 44, 96); }
                    if (b % 2 == 0) timp.Note(bb, 2, tp, 72);
                    break;
                case 3:
                    big.Note(bb, 1, 36, 110); taiko.Note(bb + 2, 1, 38, 86); if (b % 2 == 1) taiko.Note(bb + 3.5, 0.5, 38, 78);
                    timp.Note(bb, 2, tp, 92); if (b % 4 == 0) { big.Note(bb, 1, 46, 100); anvil.Note(bb, 1, 44, 100); }
                    break;
                case 4:
                    if (b % 2 == 0) { big.Note(bb, 1, 36, 104).Note(bb, 1, 46, 100); big.Note(bb + 0.75, 1, 36, 78); }
                    if (b % 4 == 0) bell.Note(bb, 4, 50, 90);
                    break;
            }
        }
        Roll(timp, s.BarBeat(24, 2), 2, 38, 40, 100); Roll(timp, s.BarBeat(28, 3), 1, 38, 60, 100);
        Roll(timp, s.BarBeat(40), 4, 38, 30, 96); Roll(timp, s.BarBeat(48, 2), 2, 38, 50, 104);
        return s;
    }

    static Score AbyssCombat()
    {
        var s = AbyssNew("abyss_combat"); const int T = 38; var sc = Phrygian;
        var vc = Stac(s, "celli", 42, -21, -0.2, -11, 0.13); var va = Stac(s, "violas", 41, -24.5, 0.25, -10, 0.12); var cb = Stac(s, "basses", 43, -22, 0.1, -13, 0.28);
        var pulse = s.Dsp("pulse", Syn.Pulse(1100, 0.1), -26.5, 0, -16);
        var taiko = Perc(s, "taiko", 116, -20, 0, -10); var big = DrumSyn(s, "bigdrum", -21, -13); var forge = DrumSyn(s, "forge", -25, -7); var tom = s.Kit("toms", 0, -24.5, 0, -10);
        var timp = Perc(s, "timpani", 47, -23, 0.15, -9);
        Tame(taiko, big, forge, tom, timp);
        int[] dg = { 0, 0, 1, 0, 0, 2, 0, 0, 1, 0, 0, -1, 0, 0, 1, -1 };
        for (int b = 0; b < 48; b++)
        {
            int r = AbyssRoots[b], sec = b / 8; double bb = b * 4; bool fill = b % 4 == 3, thin = b == 22 || b == 23;
            double v = sec == 0 ? 94 : sec == 3 ? 108 : sec == 4 ? 98 : 102; int tp = FoldTo(Deg(sc, T, r), 38);
            // accents in 3+3+3+3+2+2 over running sixteenths
            Fig(cb, bb, sc, T, r, L(16, "X..X..X..X..X.X."), One, 0.25, v, 0.4, 28);
            Pat(taiko, bb, L(16, fill ? "X..X..X..X..XXXX" : "X..X..X..X..X.X."), 38, thin ? 84 : 96);
            if (thin) { if (b == 23) { big.Note(bb, 4, 52, 108); Roll(timp, bb + 2, 2, tp, 50, 104); } continue; }
            Fig(vc, bb, sc, T, r, L(16, "XxxXxxXxxXxxXxxx"), dg, 0.25, v, -1, 36);
            Fig(va, bb, sc, T, r, L(16, "..x..x..x..x..xx"), new[] { 2 }, 0.25, v - 10, -1, 48);
            if (b >= 4) Fig(pulse, bb, sc, T, r, L(16, "X.xxX.xxX.xxX.xx"), dg, 0.25, 86, -1, 36);
            Pat(big, bb, L(16, "X.....X.....X..."), 36, 104); if (b % 4 == 0) big.Note(bb, 1, 46, 100);
            Pat(big, bb, L(16, "x.xxx.xxx.xxx.xx"), 42, 50);
            Pat(forge, bb, L(16, "....X.......X..."), 44, 90);   // the dead forge: anvil on two and four
            if (fill) { Pat(tom, bb, L(16, "............x.xx"), 41, 94); Pat(tom, bb, L(16, "..........x....."), 45, 88); }
            timp.Note(bb, 1.5, tp, 104); timp.Note(bb + 2.5, 1, tp, 86);
        }
        return s;
    }

    static void Zones2()
    {
        AddPair("sands", "Rustsand Ruins: dry plucked riff on the flat second, lonely English horn, dulcimer answers, hand drum / combat layer: string ostinato, goblet-drum pattern, bongos, tambourine", "E Phrygian (major-third cadences)", SandsBase, SandsCombat);
        AddPair("sanctum", "Hushed Sanctum: slow chorale for organ and wordless choir, tolling bells, a high voice in the last phrase / combat layer: compound-time strings, timpani, taiko, processional snare", "G Aeolian (Phrygian cadences)", SanctumBase, SanctumCombat);
        AddPair("abyss", "Ember Abyss: bowed metal over a sub drone, heavy slow drum, low strings, the main theme inverted on solo cello / combat layer: 3+3+3+3+2+2 strings, heavy drums, forge anvil", "D Phrygian (flat fifth)", AbyssBase, AbyssCombat);
    }
}
