// Original compositions (1): main menu theme, town, intro bed, boss, final boss, plus the SoundFont stingers.
// All note data is written here by hand; nothing is quoted from existing music.
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public static partial class Music
{
    // ---------- instrument presets (General MIDI programs in MuseScore_General.sf2) ----------
    static Part Solo(Score s, string n, int prog, double lufs, double pan = 0, double send = -7)
    { var p = s.Sf(n, prog, lufs, pan, send); p.Swell = (-9, 0, -7, 0.4); p.Vib = 45; p.Legato = 0.03; p.TimeJit = 0.012; return p; }
    static Part PadS(Score s, string n, int prog, double lufs, double pan = 0, double send = -6)
    { var p = s.Sf(n, prog, lufs, pan, send); p.Swell = (-12, 0, -9, 0.5); p.Poly = true; p.Legato = 0.12; p.TimeJit = 0.015; return p; }
    static Part Plk(Score s, string n, int prog, double lufs, double pan = 0, double send = -5)
    { var p = s.Sf(n, prog, lufs, pan, send); p.TimeJit = 0.011; p.VelJit = 7; p.Legato = 0.3; return p; }
    static Part Perc(Score s, string n, int prog, double lufs, double pan = 0, double send = -10)
    { var p = s.Sf(n, prog, lufs, pan, send); p.TimeJit = 0.005; p.VelJit = 6; return p; }
    static Part Stac(Score s, string n, int prog, double lufs, double pan = 0, double send = -11, double len = 0.17)
    { var p = s.Sf(n, prog, lufs, pan, send); p.StaccSec = len; p.TimeJit = 0.004; p.VelJit = 6; return p; }
    static Part DrumSyn(Score s, string n, double lufs, double send = -12) { var p = s.Dsp(n, Syn.Drums(), lufs, 0, send); return p; }

    // scale-degree helpers for ostinati
    static readonly int[] Aeolian = { 0, 2, 3, 5, 7, 8, 10 }, Phrygian = { 0, 1, 3, 5, 7, 8, 10 }, PhrygDom = { 0, 1, 4, 5, 7, 8, 10 }, Dorian = { 0, 2, 3, 5, 7, 9, 10 };
    static int Deg(int[] scale, int tonic, int degree) { int o = (int)Math.Floor(degree / 7.0), d = ((degree % 7) + 7) % 7; return tonic + 12 * o + scale[d]; }
    /// <summary>Ostinato bar: rhythm chars (X accent, x normal, o soft, . rest) and per-step scale-degree offsets from rootDeg.</summary>
    static void Ost(Part p, double bar, int[] scale, int tonic, int rootDeg, string rhythm, int[] degs, double step, double vel, double dur = -1, double accent = 16)
    {
        double b0 = (bar - 1) * p.Sc.BeatsPerBar;
        for (int i = 0; i < rhythm.Length; i++)
        {
            char c = rhythm[i]; if (c == '.') continue;
            p.Note(b0 + i * step, dur > 0 ? dur : step * 0.9, Deg(scale, tonic, rootDeg + degs[i % degs.Length]), c == 'X' ? vel + accent : c == 'o' ? vel - 22 : vel);
        }
    }
    static void Roll(Part p, double beat, double beats, int pitch, double v0, double v1, double step = 0.25)
    { int n = (int)Math.Round(beats / step); for (int i = 0; i < n; i++) p.Note(beat + i * step, step, pitch, Lerp(v0, v1, n == 1 ? 1 : i / (double)(n - 1))); }

    // =====================================================================================================
    //  MENU THEME - D minor, 54 BPM, 36 bars (2:40). The "ember" theme: a rising minor seventh that sighs back
    //  down (D-C-Bb-A), answered by a Phrygian (Eb) cadence. Solo cello; viola takes the middle strain; one build.
    // =====================================================================================================
    public const string ThemeA = "d3:2 c4:2 | bb3:1.5 a3:.5 a3:2 | g3:1 a3:1 f3:2 | e3:3 r:1 | d3:2 c4:2 | d4:1.5 c4:.5 a3:2 | bb3:1 g3:1 eb3:2 | d3:4 |";
    static Score MenuTheme()
    {
        var s = new Score("menu_theme", 54, 36) { RevTime = 4.4, RevDamp = 4200 };
        var drone = s.Dsp("drone", Syn.Drone(150, 460, 7, 0.6, 5), -27, 0, -14);
        drone.Note(0, 144, 38, 100).Note(0, 144, 45, 70);
        var lowch = s.Dsp("lowchoir", Syn.Choir(Sfx.VU, Sfx.VO, 3, 1.0), -28, 0, -3);
        lowch.At(1).Play("!mp d2+a2:16 |").At(13).Play("!mp f2+c3:4 | bb1+f2:4 | g2+d3:4 | a1+e2:4 | f2+c3:4 | c2+g2:2 bb1+f2:2 | g2+d3:2 eb2+bb2:2 | a1+e2:4 |")
             .At(21).Play("!f d2+a2:4 | d2+bb2:2 d2+a2:2 | g2+d3:2 f2+c3:2 | a1+e2:4 | d2+a2:4 | bb1+f2:2 a1+f2:2 | g1+g2:2 eb2+bb2:2 | d2+a2:4 |").At(29).Play("!p d2+a2:32 |");

        const string padIntro = "d2+a2+d3:4 | d2+a2+d3+e3:4 | d2+bb2+f3:4 | a1+e2+a2+e3:4 |";
        const string padA = "d2+a2+d3+f3:4 | d2+bb2+d3+f3:2 d2+a2+d3+f3:2 | g2+bb2+d3:2 f2+a2+c3:2 | a1+e2+a2+c3+e3:4 | d2+a2+d3+f3:4 | bb1+f2+bb2+d3:2 a1+f2+a2+c3:2 | g1+g2+bb2+d3:2 eb2+g2+bb2+eb3:2 | d2+a2+d3:4 |";
        const string padB = "f2+c3+f3+a3:4 | bb1+f2+bb2+d3:4 | g2+d3+g3+bb3:4 | a1+e2+a2+c3:2 a1+e2+a2+e3:2 | f2+c3+f3+a3:4 | c2+g2+c3+e3:2 bb1+f2+bb2+d3:2 | g1+g2+bb2+d3:2 eb2+g2+bb2+eb3:2 | a1+e2+a2+e3:4 |";
        const string padCoda = "d2+a2+d3+f3:4 | d2+a2+d3+f3:4 | d2+bb2+d3+f3:2 d2+a2+d3+f3:2 | g2+bb2+d3:2 eb2+g2+bb2+f3:2 | g2+bb2+d3:2 f2+a2+c3:2 | a1+e2+a2+c3:2 d2+a2+d3:2 | d2+a2+d3:4 | d2+a2+d3+e3:4 |";
        var str = PadS(s, "strings", 49, -25);
        str.At(1).Play("!pp " + padIntro).Play("!p " + padA).Play("!mp " + padB).Play("!f " + padA).Play("!p " + padCoda);
        var hi = PadS(s, "histrings", 48, -25, 0.25);
        hi.At(21).Play("!f f4+a4:4 | f4+bb4:2 f4+a4:2 | g4+bb4:2 f4+a4:2 | e4+a4:4 | f4+a4+d5:4 | f4+bb4+d5:2 f4+a4+c5:2 | g4+bb4+d5:2 g4+bb4+eb5:2 | a4+d5:4 |");
        var choir = PadS(s, "choir", 52, -26, 0, -3);
        choir.At(3).Play("!pp d3+f3+bb3:4 | e3+a3:4 |")
             .At(9).Play("!p a3+d4+f4:4 | bb3+d4+f4:2 a3+c4+f4:2 | g3+bb3+d4:2 g3+bb3+eb4:2 | a3+d4:4 |")
             .At(17).Play("!mp a3+c4+f4:4 | g3+c4+e4:2 f3+bb3+d4:2 | g3+bb3+d4:2 g3+bb3+eb4:2 | a3+e4:4 |")
             .At(21).Play("!f a3+d4+f4:4 | bb3+d4+f4:2 a3+d4+f4:2 | g3+bb3+d4:2 f3+a3+c4:2 | e3+a3+c4+e4:4 | a3+d4+f4:4 | bb3+d4+f4:2 a3+c4+f4:2 | g3+bb3+d4:2 g3+bb3+eb4:2 | a3+d4:4 |")
             .At(29).Play("!pp a3+d4:4 | a3+d4+f4:4 |").At(35).Play("!pp a3+d4:4 | a3+e4:4 |");

        var cello = Solo(s, "cello", 42, -20.5, -0.12);
        cello.At(5).Play("!mf " + ThemeA)
             .At(13).Play("!mp a2:4 | bb2:2 d3:2 | d3:2 bb2:2 | e3:2 c3:2 | c3:4 | e3:2 d3:2 | bb2:2 g2:2 | a2:2 e3:1 a3:1 |")
             .At(21).Play("!f " + ThemeA)
             .At(30).Play("!p d3:2 c4:2 | bb3:1.5 a3:.5 a3:2 |").At(33).Play("!pp g3:1 a3:1 f3:2 | e3:2 d3:2 | d3:4 |");
        var viola = Solo(s, "viola", 41, -21.5, 0.15);
        viola.At(13).Play("!mf f4:2 a4:1 c5:1 | d5:3 c5:1 | bb4:1 a4:1 g4:2 | a4:4 | f4:2 a4:1 c5:1 | e5:2 d5:2 | c5:1 bb4:1 a4:1 g4:1 | a4:4 |")
             .At(21).Play("!f d4:2 c5:2 | bb4:1.5 a4:.5 a4:2 | g4:1 a4:1 f4:2 | e4:3 r:1 | d4:2 c5:2 | d5:1.5 c5:.5 a4:2 | bb4:1 g4:1 eb4:2 | d4:4 |");
        var bass = s.Sf("bass", 43, -25, 0, -13); bass.Swell = (-8, 0, -6, 0.4); bass.Legato = 0.05;
        const string bassA = "d2:4 | d2:4 | g1:2 f1:2 | a1:4 | d2:4 | bb1:2 a1:2 | g1:2 eb2:2 | d2:4 |";
        bass.At(5).Play("!p " + bassA).Play("!mp f1:4 | bb1:4 | g1:4 | a1:4 | f1:4 | c2:2 bb1:2 | g1:2 eb2:2 | a1:4 |").Play("!f " + bassA)
            .Play("!p d2:4 | d2:4 | d2:4 | g1:2 eb2:2 | g1:2 f1:2 | a1:2 d2:2 | d2:4 |");
        var harp = Plk(s, "harp", 46, -25, 0.3);
        const string harpA = "d3:1 a3:1 d4:2 | r:4 | g2:1 d3:1 bb3:2 | r:4 | d3:1 a3:1 f4:2 | r:4 | g2:1 bb3:1 eb4:2 | d3:.5 a3:.5 d4:.5 f4:.5 a4:2 |";
        harp.At(2).Play("!p d3:.5 a3:.5 d4:.5 e4:.5 a4:2 |").At(4).Play("!p a2:.5 e3:.5 a3:.5 d4:.5 e4:2 |").At(5).Play("!p " + harpA)
            .At(13).Play("!mp f2:.5 c3:.5 f3:.5 a3:.5 c4:2 | bb2:.5 f3:.5 bb3:.5 d4:.5 f4:2 | g2:.5 d3:.5 g3:.5 bb3:.5 d4:2 | a2:.5 e3:.5 a3:.5 c4:.5 e4:2 | f2:.5 c3:.5 f3:.5 a3:.5 c4:2 | c3:.5 g3:.5 c4:.5 e4:.5 bb2:.5 f3:.5 bb3:.5 d4:.5 | g2:.5 d3:.5 g3:.5 bb3:.5 eb3:.5 bb3:.5 eb4:.5 g4:.5 | a2:.5 e3:.5 a3:.5 e4:.5 a4:2 |")
            .At(21).Play("!f " + harpA).At(32).Play("!p g3:.5 bb3:.5 d4:.5 g4:.5 eb4:.5 g4:.5 bb4:1 |").At(36).Play("!pp d3:.5 a3:.5 d4:.5 a4:2.5 |");
        var timp = Perc(s, "timpani", 47, -23);
        Roll(timp, s.BarBeat(20), 4, 45, 38, 100);
        timp.Note(s.BarBeat(21), 2, 38, 116).Note(s.BarBeat(23), 2, 43, 96).Note(s.BarBeat(24), 1, 45, 100); Roll(timp, s.BarBeat(24, 2), 2, 45, 60, 104);
        timp.Note(s.BarBeat(25), 2, 38, 116).Note(s.BarBeat(27), 2, 43, 100).Note(s.BarBeat(27, 2), 2, 39, 96).Note(s.BarBeat(28), 1, 38, 110); Roll(timp, s.BarBeat(28, 1), 3, 38, 70, 30);
        var taiko = Perc(s, "taiko", 116, -23, 0, -8);
        taiko.Note(s.BarBeat(21), 1, 38, 120).Note(s.BarBeat(23), 1, 38, 100).Note(s.BarBeat(25), 1, 38, 120).Note(s.BarBeat(27), 1, 38, 104).Note(s.BarBeat(27, 2), 1, 41, 92);
        var bell = Perc(s, "bell", 14, -25, -0.2, -3);
        bell.Note(s.BarBeat(1), 4, 62, 60).Note(s.BarBeat(13), 4, 57, 70).Note(s.BarBeat(21), 4, 62, 104).Note(s.BarBeat(25), 4, 62, 92).Note(s.BarBeat(29), 4, 62, 58);
        var bowed = s.Dsp("bowed", Syn.Bowed(new[] { 1, 2.76, 5.4 }, 2.5, 3.5, 0.2), -32, 0, -2);
        bowed.Note(s.BarBeat(1), 16, 69, 90).Note(s.BarBeat(29), 30, 74, 90);
        return s;
    }

    // =====================================================================================================
    //  TOWN (Lastlamp) - B minor turning to D major, 6/8, dotted quarter = 48 (eighth = 144), 54 bars (2:15).
    //  Finger-picked nylon guitar under a steel-string / harp melody; viola answers; a soft frame drum.
    // =====================================================================================================
    static readonly Dictionary<string, (string arp, string pad, string bass)> TownCh = new()
    {
        ["Bm"] = ("b2 f#3 b3 d4 b3 f#3", "b2+f#3+d4", "b1"), ["G"] = ("g2 d3 g3 b3 g3 d3", "g2+d3+b3", "g1"), ["D/F#"] = ("f#2 d3 a3 d4 a3 d3", "f#2+d3+a3", "f#1"),
        ["Em"] = ("e2 b2 e3 g3 e3 b2", "e2+b2+g3", "e1"), ["Bm/D"] = ("d3 f#3 b3 d4 b3 f#3", "d3+f#3+b3", "d2"), ["F#m"] = ("f#2 c#3 f#3 a3 f#3 c#3", "f#2+c#3+a3", "f#1"),
        ["D"] = ("d3 a3 d4 f#4 d4 a3", "d2+a2+f#3", "d2"), ["A"] = ("a2 e3 a3 c#4 a3 e3", "a2+e3+c#4", "a1"), ["Asus4"] = ("a2 e3 a3 d4 a3 e3", "a2+e3+d4", "a1"),
        ["A/C#"] = ("c#3 e3 a3 c#4 a3 e3", "c#3+e3+a3", "c#2"), ["Em7"] = ("e2 b2 d3 g3 d3 b2", "e2+d3+g3", "e1"), ["Bm/A"] = ("a2 f#3 b3 d4 b3 f#3", "a2+f#3+d4", "a1"),
        ["F#sus4"] = ("f#2 c#3 f#3 b3 f#3 c#3", "f#2+c#3+b3", "f#1"), ["Bm9"] = ("b2 f#3 b3 c#4 d4 f#4", "b2+f#3+c#4+d4", "b1"),
    };
    static Score Town()
    {
        var s = new Score("town", 144, 54, 6) { RevTime = 3.4, RevDamp = 5000 };
        string[] plan = ("Bm Bm G D/F# Em Bm/D F#m Bm " + "Bm Bm G D Em G A Asus4 " + "D A/C# Bm G D/F# G Em7 F#m " + "Bm Bm G D/F# Em Bm/D F#m Bm " +
                         "G A Bm Bm/A G Em F#sus4 F#m " + "D A/C# Bm G Em G A D " + "G Em Bm G F#m Bm9").Split(' ');
        if (plan.Length != 54) throw new Exception("town plan length " + plan.Length);
        var gtr = Plk(s, "guitar_arp", 24, -24, -0.3, -7); gtr.Legato = 0.5;
        var pad = PadS(s, "strings", 49, -28, 0.1); var bass = s.Sf("bass", 43, -27, 0, -13); bass.Swell = (-8, 0, -8, 0.3);
        for (int b = 0; b < 54; b++)
        {
            var (arp, pd, bs) = TownCh[plan[b]]; var notes = arp.Split(' '); bool soft = b < 2 || b >= 48;
            for (int i = 0; i < 6; i++) gtr.Note(b * 6 + i, i == 0 ? 3 : 1.5, Part.Pitch(notes[i]), (i == 0 ? 78 : i == 3 ? 66 : 58) - (soft ? 8 : 0));
            if (b >= 8 && b < 52) pad.At(b + 1).Play((b >= 16 && b < 24 || b >= 40 && b < 48 ? "!mp " : "!p ") + pd + ":6");
            if (b >= 16 && b < 48) bass.At(b + 1).Play("!p " + bs + ":6");
        }
        var mel = Plk(s, "guitar_mel", 25, -20.5, 0.15, -4); mel.Legato = 0.6;
        const string melA = "f#4:3 b4:2 a4:1 | f#4:4 e4:1 f#4:1 | g4:3 d5:2 b4:1 | a4:6 | g4:3 e4:2 g4:1 | f#4:3 d4:3 | e4:2 f#4:1 c#4:3 | b3:6 |";
        mel.At(1).Play("!mf " + melA)
           .Play("f#4:3 b4:2 a4:1 | f#4:4 e4:1 f#4:1 | g4:3 d5:2 b4:1 | a4:3 f#4:3 | g4:3 b4:2 a4:1 | g4:3 b4:3 | a4:4 b4:1 c#5:1 | e5:6 |")
           .Play("!f f#5:3 e5:2 d5:1 | e5:4 c#5:2 | d5:3 b4:2 d5:1 | b4:4 g4:2 | a4:3 d5:2 f#5:1 | g5:3 f#5:2 e5:1 | d5:3 e5:3 | c#5:6 |")
           .Play("!mf " + melA)
           .At(41).Play("!f f#5:3 e5:2 d5:1 | e5:4 c#5:2 | d5:3 b4:2 d5:1 | b4:4 g4:2 | g4:3 b4:2 e5:1 | d5:3 b4:2 g4:1 | a4:3 c#5:2 e5:1 | d5:6 |")
           .Play("!mp b4:3 g4:3 | g4:3 e4:3 | f#4:6 | d4:3 g4:2 b4:1 | a4:2 f#4:1 c#4:3 | b3:6 |");
        var harp = Plk(s, "harp", 46, -24, 0.4, -4);
        harp.At(17).Play("!mp f#4:3 e4:2 d4:1 | e4:4 c#4:2 | d4:3 b3:2 d4:1 | b3:4 g3:2 | a3:3 d4:2 f#4:1 | g4:3 f#4:2 e4:1 | d4:3 e4:3 | c#4:6 |")
            .At(41).Play("!mp f#4:3 e4:2 d4:1 | e4:4 c#4:2 | d4:3 b3:2 d4:1 | b3:4 g3:2 | g3:3 b3:2 e4:1 | d4:3 b3:2 g3:1 | a3:3 c#4:2 e4:1 | d4:1 f#4:1 a4:1 d5:3 |")
            .At(49).Play("!p g3:1 b3:1 d4:1 g4:3 | e3:1 g3:1 b3:1 e4:3 |").At(54).Play("!pp b3:1 d4:1 f#4:1 b4:3 |");
        var viola = Solo(s, "viola", 41, -22.5, -0.15);
        viola.At(25).Play("!mp d4:6 | d4:3 c#4:3 | b3:6 | a3:3 f#4:3 | e4:6 | d4:6 | c#4:6 | b3:6 |")
             .Play("!mf b4:3 a4:2 g4:1 | a4:4 e4:2 | f#4:3 d4:2 f#4:1 | f#4:3 e4:3 | d4:3 g4:3 | g4:2 f#4:1 e4:3 | f#4:6 | f#4:3 e4:1 f#4:1 a4:1 |")
             .At(45).Play("!mp b3:6 | b3:3 d4:3 | c#4:3 e4:3 | f#4:6 |");
        var fr = DrumSyn(s, "framedrum", -29, -9);
        for (int b = 16; b < 48; b++) { fr.Note(b * 6, 1, 38, 80).Note(b * 6 + 3, 1, 40, 52); if (b % 4 == 3) fr.Note(b * 6 + 5, 1, 40, 46); }
        var drone = s.Dsp("drone", Syn.Drone(200, 520, 6, 0.4, 5), -32, 0, -12); drone.Note(0, 324, 35, 90).Note(0, 324, 42, 70);
        return s;
    }

    // =====================================================================================================
    //  INTRO BED - D minor, free time (written at 60), 60 s + 8 s ring-out, not looped. Quiet drone and far choir
    //  under the narration, a distant hint of the theme, a soft swell at the end.
    // =====================================================================================================
    static Score IntroBed()
    {
        var s = new Score("intro_bed", 60, 15) { Loop = false, Tail = 8, FadeOut = 5, RevTime = 5.0, RevDamp = 3800 };
        var drone = s.Dsp("drone", Syn.Drone(140, 380, 7, 0.6, 6), -24, 0, -12); drone.Note(0, 58, 38, 90).Note(0, 58, 45, 60).Note(48, 10, 26, 90);
        var lowch = s.Dsp("lowchoir", Syn.Choir(Sfx.VU, Sfx.VO, 3, 1.0, 9, 2.0, 2.5), -25, 0, 0);
        lowch.At(3).Play("!p d3+a3:8 |").Play("d3+f3+bb3:8 |").Play("d3+g3+bb3:8 |").Play("!mp d3+a3+e4:8 |").Play("!mf d3+f3+bb3:8 |").Play("!f d3+a3+d4:8 |");
        var choir = PadS(s, "choir", 53, -27, 0, 2); choir.Lp = 2500;
        choir.At(5).Play("!pp f4+bb4:8 |").Play("g4+bb4:8 |").Play("!p a4+e5:8 |").Play("!mp f4+bb4+d5:8 |").Play("!mf a4+d5:8 |");
        var str = PadS(s, "strings", 49, -27); str.Swell = (-16, 0, -12, 0.6);
        str.At(11).Play("!mp bb1+f2+bb2+d3:8 |").Play("!f d2+a2+d3+a3:8 |");
        var viola = Solo(s, "viola", 41, -30, 0.2, 4); viola.Lp = 3000;
        viola.At(9).Play("!p d4:2 c5:2 | bb4:1.5 a4:.5 a4:2 |");
        var bowed = s.Dsp("bowed", Syn.Bowed(new[] { 1, 2.76, 5.4 }, 4, 4, 0.2), -32, 0, -2); bowed.Note(4, 50, 69, 90);
        var bell = Perc(s, "bell", 14, -30, 0, 0); bell.Note(s.BarBeat(13), 4, 62, 60);
        var timp = Perc(s, "timpani", 47, -30, 0, -4); Roll(timp, s.BarBeat(12, 2), 2, 38, 30, 62); timp.Note(s.BarBeat(13), 2, 38, 74);
        return s;
    }

    // =====================================================================================================
    //  BOSS - G Phrygian, 12/8, dotted quarter = 108 (eighth = 324), 54 bars (2:00). Relentless low-string
    //  ostinato, taiko and timpani, choir stabs; a breakdown in hemiola; climax; vamp back to the top.
    // =====================================================================================================
    static Score Boss()
    {
        var s = new Score("boss", 324, 54, 12) { RevTime = 2.8, RevDamp = 5000, RevSize = 0.9 };
        int G = 43; int[] sc = Phrygian;
        // root degree per bar: 0=G 1=Ab 2=Bb 3=C 4=D -1=F -2=Eb
        int[] roots = { 0,0,0,0, 0,0,1,0,  0,0,1,0, 2,0,1,0,  3,3,5,3, 1,2,3,4,  0,0,1,0, 2,0,1,0,  0,0,0,0,0,0,  0,0,1,0, 2,0,1,0,  0,0,0,0, 1,0,1,0 };
        if (roots.Length != 54) throw new Exception("boss roots " + roots.Length);
        int[] degs = { 0, 0, 1, 0, 0, 2, 0, 0, 1, 0, -1, -3 };
        // ostinato = solo celli (body) + pizzicato (attack) + basses on the dotted beats + a filtered pulse synth for definition
        var vc = Stac(s, "celli", 42, -21.5, -0.2, -11, 0.17); var pzo = Stac(s, "pizz", 45, -22.5, 0.2, -10, 0.3); var cb = Stac(s, "basses", 43, -22.5, 0.1, -13, 0.3); var pulse = s.Dsp("pulse", Syn.Pulse(1100, 0.13), -26, 0, -16);
        for (int b = 0; b < 54; b++)
        {
            bool brk = b >= 32 && b < 38; double v = b < 4 ? 88 : b >= 50 ? 100 - (b - 50) * 3 : 104;
            if (brk)
            {   // hemiola: three groups of four eighths
                int rb = Deg(sc, G, 0); double bb = b * 12;
                foreach (var (o, pq) in new[] { (0, rb), (4, rb), (8, rb + 1) })
                {
                    vc.Note(bb + o, 3, pq, 100 + (b - 32) * 3); cb.Note(bb + o, 3, pq - 12, 100 + (b - 32) * 3); pzo.Note(bb + o, 1, pq, 104);
                    if (b >= 34) { vc.Note(bb + o + 2, 1, pq, 84); vc.Note(bb + o + 3, 1, pq, 88); pzo.Note(bb + o + 2, 1, pq, 80); pzo.Note(bb + o + 3, 1, pq, 86); }
                }
                continue;
            }
            Ost(vc, b + 1, sc, G, roots[b], "XxxXxxXxxXxx", degs, 1, v); Ost(pzo, b + 1, sc, G, roots[b], "XxxXxxXxxXxx", degs, 1, v - 6); Ost(cb, b + 1, sc, G - 12, roots[b], "X..X..X..X..", degs, 1, v, 1.5);
            if (b >= 4) Ost(pulse, b + 1, sc, G - 12, roots[b], "XxxXxxXxxXxx", degs, 1, 90);
        }
        // melody (violas + celli section in octaves), sections B (9-16), D (25-32) and F (39-46)
        const string melB = "g3:3 bb3:3 ab3:3 g3:3 | f3:3 ab3:3 g3:6 | ab3:3 c4:3 bb3:3 ab3:3 | g3:6 d4:6 | f4:3 eb4:3 d4:3 c4:3 | d4:6 bb3:3 c4:3 | ab3:3 c4:3 eb4:6 | d4:9 r:3 |";
        const string melC = "c4:3 eb4:3 d4:3 c4:3 | bb3:3 d4:3 c4:6 | eb4:3 g4:3 f4:3 eb4:3 | d4:6 g4:6 | ab4:6 g4:3 f4:3 | f4:3 d4:3 bb3:6 | c4:3 eb4:3 g4:6 | ab4:6 d4:6 |";
        // (the ensemble patches speak too slowly for this tempo, so the line is carried by solo viola + cello with the ensemble as body)
        var mel = s.Sf("melody_vla", 41, -20.5, 0.1, -8); mel.Swell = (-4, 0, -4, 0.25); mel.Vib = 40; mel.Legato = 0.02;
        var mel2 = s.Sf("melody_vc", 42, -21.5, -0.25, -8); mel2.Swell = (-4, 0, -4, 0.25); mel2.Vib = 40; mel2.Transpose = -12;
        var mel3 = s.Sf("melody_ens", 48, -25, 0, -7); mel3.Swell = (-5, 0, -4, 0.3); mel3.Poly = true; mel3.Legato = 0.1;
        var mel4 = s.Sf("melody_vln", 40, -21.5, 0.3, -8); mel4.Swell = (-4, 0, -4, 0.25); mel4.Vib = 45; mel4.Transpose = 12;
        foreach (var m in new[] { mel, mel2, mel3 }) m.At(9).Play("!f " + melB).Play("!f " + melC).Play("!ff " + melB).At(39).Play("!ff " + melB);
        mel4.At(39).Play("!ff " + melB);
        var hi = s.Sf("violins", 48, -23.5, 0.3, -7); hi.Swell = (-6, 0, -5, 0.4); hi.Poly = true;
        const string counter = "d5:12 | c5:6 d5:6 | eb5:12 | d5:12 | f5:6 d5:6 | d5:12 | eb5:6 c5:6 | d5:12 |";
        hi.At(25).Play("!f " + counter);
        var hi2 = s.Sf("violins_hi", 44, -24, 0.3, -7); hi2.Poly = true; hi2.Transpose = 12;   // tremolo strings an octave above the counter line for the climax
        hi2.At(39).Play("!ff " + counter);
        // choir: stabs in B/D, sustained in the breakdown and climax
        // (stabs use the fast-speaking "oohs" patch plus a hard-attack formant choir; the "aahs" patch is kept for sustained writing)
        var choir = s.Sf("choir_stab", 53, -22, 0, -5); choir.Poly = true; choir.TimeJit = 0.006;
        var stabSyn = s.Dsp("choir_stab_syn", Syn.Choir(Sfx.VA, Sfx.VA, 3, 1.0, 10, 0.03, 0.3, 0.12), -24, 0, -5);
        string[] stab = { "g3+bb3+d4", "", "ab3+c4+eb4", "", "f3+bb3+d4", "", "ab3+c4+eb4", "g3+d4+g4" };
        foreach (int st in new[] { 8, 24 }) for (int i = 0; i < 8; i++) if (stab[i] != "") { choir.At(st + i + 1).Play($"!ff {stab[i]}:3 r:9 |"); stabSyn.At(st + i + 1).Play($"!ff {stab[i]}:2.5 r:9.5 |"); }
        string[] stabC = { "g3+c4+eb4", "", "g3+bb3+eb4", "", "ab3+c4+eb4", "f3+bb3+d4", "g3+c4+eb4", "f3+ab3+d4" };
        for (int i = 0; i < 8; i++) if (stabC[i] != "") { choir.At(17 + i).Play($"!ff {stabC[i]}:3 r:9 |"); stabSyn.At(17 + i).Play($"!ff {stabC[i]}:2.5 r:9.5 |"); }
        var chs = PadS(s, "choir_sus", 52, -23, 0, -4);
        chs.At(35).Play("!mp g3+ab3+d4:12 | !mf g3+ab3+d4:12 | !f g3+ab3+d4+g4:12 | !ff g3+bb3+d4+g4:12 |")
           .Play("!ff g3+bb3+d4:12 | g3+bb3+d4:12 | ab3+c4+eb4:12 | g3+bb3+d4:12 | f3+bb3+d4:12 | g3+bb3+d4:12 | ab3+c4+eb4:12 | g3+d4+g4:12 |");
        var lowch = s.Dsp("lowchoir", Syn.Choir(Sfx.VO, Sfx.VA, 3, 0.95, 10, 0.3, 0.8), -25, 0, -6);
        lowch.At(7).Play("!f g2+ab2+d3:24 |").At(33).Play("!mf g2+d3:24 | !f g2+ab2+d3:24 | !ff g2+ab2+d3:24 |").At(39).Play("!f g2+d3:24 | ab2+eb3:12 g2+d3:12 | bb2+f3:12 g2+d3:12 | ab2+eb3:12 g2+d3:12 |");
        // drums
        var taiko = Perc(s, "taiko", 116, -19.5, 0, -10); var tom = s.Kit("toms", 0, -24, 0, -10); var timp = Perc(s, "timpani", 47, -22.5, 0.15, -9);
        var big = DrumSyn(s, "bigdrum", -21, -13); var cym = s.Kit("cymbal", 48, -27, 0, -6);
        for (int b = 0; b < 54; b++)
        {
            bool brk = b >= 32 && b < 38, full = b >= 4 && !brk, fill = b % 4 == 3; double bb = b * 12;
            if (brk)
            {
                big.Note(bb, 1, 36, 120); taiko.Note(bb, 1, 38, 118); if (b >= 34) { taiko.Note(bb + 4, 1, 38, 96); taiko.Note(bb + 8, 1, 38, 100); }
                if (b >= 36) Roll(timp, bb, 12, 43, 50 + (b - 36) * 25, 78 + (b - 36) * 30, 1); if (b == 37) big.Note(bb, 12, 52, 110);
                continue;
            }
            taiko.Hits(b + 1, fill ? "X..x..X..xxx" : "X..x..X..x..", 38, b < 4 ? 84 : 100, 1, 1);
            if (full) { taiko.Hits(b + 1, "..x..x..x.xx", 45, 80, 1, 1); big.Hits(b + 1, "X.....x.....", 36, 104, 1, 1); big.Hits(b + 1, "xxxxxxxxxxxx", 42, 58, 1, 1); }
            if (full && fill) tom.Hits(b + 1, ".........xxx", 41, 96, 1, 1);
            if (b >= 4) timp.Note(bb, 3, Deg(sc, G, roots[b]) + (Deg(sc, G, roots[b]) > 50 ? -12 : 0), 104);
            if (full && b % 2 == 1) timp.Note(bb + 9, 1.5, 50, 88);
        }
        foreach (int b in new[] { 8, 16, 24, 38, 46 }) cym.Note(b * 12, 6, 57, 100);
        var bell = Perc(s, "bell", 14, -25, -0.2, -4); foreach (int b in new[] { 24, 28, 38, 42 }) bell.Note(b * 12, 12, 55, 108);
        return s;
    }

    // =====================================================================================================
    //  BOSS FINAL (Osaros) - D Phrygian, 112 BPM, 70 bars (2:30). The menu theme turned upside down: its rising
    //  seventh becomes a falling one (D down to Eb) that creeps back up. Augmented in choir and bells, then at
    //  speed in the violins; the true theme surfaces in the breakdown and as counterpoint in the last statement.
    // =====================================================================================================
    public const string ThemeInv1 = "d5:2 eb4:2 | f4:1.5 g4:.5 g4:2 | a4:1 g4:1 bb4:2 | c5:3 r:1 |", ThemeInv2 = "d5:2 eb4:2 | d4:1.5 eb4:.5 g4:2 | f4:1 a4:1 c5:2 | d5:4 |";
    static Score BossFinal()
    {
        var s = new Score("boss_final", 112, 70) { RevTime = 3.0, RevDamp = 5000, RevSize = 0.95 };
        int D = 38; int[] sc = Phrygian;
        // root degrees: 0=D 1=Eb 2=F 3=G 4=A 5=Bb 6=C (below: -1=C, -2=Bb ...)
        int[] roots = { 0,0,0,0,0,0,1,0,                               // I
                        0,1, 5,3, 2,3, -1,1,  0,1, 3,1, 2,-1, 0,0,      // II (two bars per theme bar)
                        5,5,6,6, 3,3,1,1,                               // III
                        0,5,2,-1, 0,3,2,0,  0,5,2,-1, 0,3,2,0,          // IV
                        1,1,0,0, 5,5,4,4,                               // V
                        0,1,3,1, 2,-1,0,0,  0,1,0,1, 0,0 };             // VI
        if (roots.Length != 70) throw new Exception("boss_final roots " + roots.Length);
        int[] degs = { 0, 0, 1, 0, 0, 2, 0, -1 };
        var vc = Stac(s, "celli", 42, -21.5, -0.2, -11, 0.2); var pzo = Stac(s, "pizz", 45, -22.5, 0.2, -10, 0.3); var cb = Stac(s, "basses", 43, -22.5, 0.1, -13, 0.35); var pulse = s.Dsp("pulse", Syn.Pulse(1200, 0.1), -25.5, 0, -16);
        for (int b = 0; b < 70; b++)
        {
            bool brk = b >= 48 && b < 54; double v = b < 2 ? 88 : b >= 68 ? 92 : 104; int r = roots[b];
            if (brk) { cb.Note(b * 4, 3.5, Deg(sc, D - 12, r), 100); continue; }
            Ost(vc, b + 1, sc, D + 12, r, "XxxXxxXx", degs, 0.5, v); Ost(pzo, b + 1, sc, D + 12, r, "XxxXxxXx", degs, 0.5, v - 6); Ost(cb, b + 1, sc, D, r, "X..X..X.", degs, 0.5, v, 0.75);
            if (b >= 4 && b < 68) Ost(pulse, b + 1, sc, D, r, "X.xxX.xxX.xxX.xx", new[] { 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 2, 0, -1, 0 }, 0.25, b >= 32 ? 96 : 84);
        }
        // II: augmented inverted theme - choir, low strings in octaves, bells
        const string aug1 = "d4:4 eb3:4 | f3:3 g3:1 g3:4 | a3:2 g3:2 bb3:4 | c4:6 r:2 |", aug2 = "d4:4 eb3:4 | d3:3 eb3:1 g3:4 | f3:2 a3:2 c4:4 | d4:8 |";
        var choir = s.Sf("choir", 52, -21, 0, -4); choir.Swell = (-6, 0, -5, 0.3); choir.Poly = true; choir.Legato = 0.05;
        var lowstr = s.Sf("lowstrings", 48, -22, -0.15, -8); lowstr.Swell = (-6, 0, -5, 0.3); lowstr.Poly = true; lowstr.Transpose = -12;
        choir.At(9).Play("!ff " + aug1 + aug2); lowstr.At(9).Play("!ff " + aug1 + aug2);
        var lowch = s.Dsp("lowchoir", Syn.Choir(Sfx.VO, Sfx.VA, 3, 0.95, 10, 0.4, 0.9), -24, 0, -6);
        lowch.At(7).Play("!f d2+eb2+a2:8 |").At(9).Play("!f d2+a2:8 | bb1+f2:4 g2+d3:4 | f2+c3:4 g2+d3:4 | c2+g2:4 eb2+bb2:4 | d2+a2:8 | g2+d3:4 eb2+bb2:4 | f2+c3:4 c2+g2:4 | d2+a2:8 |");
        var bell = Perc(s, "bell", 14, -24, -0.2, -4);
        foreach (int b in new[] { 8, 16, 32, 40, 56, 60 }) bell.Note(b * 4, 8, 50, 112);
        // III: bridge - tremolo line climbing, stabs
        var trem = s.Sf("tremolo", 44, -22.5, 0.25, -6); trem.Swell = (-8, 0, -3, 0.8); trem.Poly = true;
        trem.At(25).Play("!f d5+f5:8 | eb5+g5:8 | f5+bb5:8 | g5+bb5:4 a5+c6:4 |");
        var stabs = s.Sf("choir_stab", 53, -21.5, 0, -5); stabs.Poly = true; stabs.TimeJit = 0.006;
        var stabSyn = s.Dsp("choir_stab_syn", Syn.Choir(Sfx.VA, Sfx.VA, 3, 1.0, 10, 0.03, 0.3, 0.12), -23.5, 0, -5);
        const string stabIII = "!ff bb3+d4+f4:1 r:7 | c4+eb4+g4:1 r:7 | g3+bb3+d4:1 r:3 g3+bb3+d4:1 r:3 | eb4+g4+bb4:1 r:1 eb4+g4+bb4:1 r:1 eb4+g4+a4:1 r:1 eb4+g4+a4:2 |";
        stabs.At(25).Play(stabIII); stabSyn.At(25).Play(stabIII);
        // IV: inverted theme at speed in the violins (twice); second time the celli answer with the true theme
        // (solo violin + viola carry the line because the ensemble patch speaks slowly; the ensemble adds body)
        var vln = s.Sf("violin", 40, -20, 0.2, -7); vln.Swell = (-4, 0, -4, 0.25); vln.Vib = 45; vln.Legato = 0.02;
        var vla = s.Sf("viola", 41, -21.5, -0.1, -7); vla.Swell = (-4, 0, -4, 0.25); vla.Vib = 40; vla.Transpose = -12;
        var ens = s.Sf("violins_ens", 48, -24.5, 0.1, -7); ens.Swell = (-5, 0, -4, 0.3); ens.Poly = true; ens.Legato = 0.1;
        vln.At(33).Play("!ff " + ThemeInv1 + ThemeInv2 + ThemeInv1 + ThemeInv2); vla.At(33).Play("!f " + ThemeInv1 + ThemeInv2 + ThemeInv1 + ThemeInv2); ens.At(33).Play("!f " + ThemeInv1 + ThemeInv2 + ThemeInv1 + ThemeInv2);
        var csolo = s.Sf("celli_theme", 42, -21, -0.3, -7); csolo.Swell = (-4, 0, -4, 0.3); csolo.Vib = 40;
        csolo.At(41).Play("!ff d3:2 c4:2 | bb3:1.5 a3:.5 a3:2 | g3:1 a3:1 f3:2 | eb3:3 r:1 | d3:2 c4:2 | d4:1.5 c4:.5 a3:2 | bb3:1 g3:1 eb3:2 | d3:4 |");
        choir.At(41).Play("!f d4+a4:4 | d4+f4+bb4:4 | c4+f4+a4:2 d4+g4+bb4:2 | c4+eb4+g4:4 | d4+a4:4 | d4+g4+bb4:2 eb4+g4+bb4:2 | c4+f4+a4:4 | d4+a4+d5:4 |");
        // V: half-time breakdown - the true theme, alone, over an Eb pedal
        var cello = Solo(s, "cello_solo", 42, -19.5, -0.1, -4);
        cello.At(51).Play("!f d3:2 c4:2 | bb3:1.5 a3:.5 a3:2 | d3:2 c4:2 | d4:1.5 c4:.5 a3:2 |");
        lowch.At(49).Play("!mf eb2+bb2:16 | !mf bb1+f2:8 | !f a1+e2+bb2:8 |");
        var pad = PadS(s, "pad", 49, -24); pad.At(49).Play("!mp eb2+bb2+eb3+g3:8 | d2+a2+d3+f3:8 | bb1+f2+bb2+d3:8 | !f a1+e2+a2+bb2+e3:8 |");
        // VI: last statement - augmented inversion below, true theme above
        choir.At(57).Play("!ff " + aug2); lowstr.At(57).Play("!ff " + aug2);
        const string trueHi = "!ff d5:2 c6:2 | bb5:1.5 a5:.5 a5:2 | g5:1 a5:1 f5:2 | eb5:3 r:1 | d5:2 c6:2 | d6:1.5 c6:.5 a5:2 | bb5:1 g5:1 eb5:2 | d5:4 |";
        vln.At(57).Play(trueHi); vla.At(57).Play(trueHi); ens.At(57).Play(trueHi);
        lowch.At(57).Play("!ff d2+a2:8 | g2+d3:4 eb2+bb2:4 | f2+c3:4 c2+g2:4 | d2+a2:8 |");
        const string stabVI = "!ff d4+a4+d5:1 r:3 | eb4+g4+bb4:1 r:3 | d4+a4+d5:1 r:3 | eb4+g4+bb4:1 r:3 |";
        stabs.At(65).Play(stabVI); stabSyn.At(65).Play(stabVI);
        // drums
        var taiko = Perc(s, "taiko", 116, -19.5, 0, -10); var timp = Perc(s, "timpani", 47, -22, 0.15, -9); var big = DrumSyn(s, "bigdrum", -20.5, -13);
        var kit = s.Kit("orch", 48, -24, 0, -8); var tom = s.Kit("toms", 0, -24.5, 0, -10);
        for (int b = 0; b < 70; b++)
        {
            bool brk = b >= 48 && b < 54, pre = b >= 54 && b < 56, intro = b < 2, outro = b >= 68, fill = b % 4 == 3; double bb = b * 4;
            if (intro) continue;
            if (brk) { big.Note(bb, 1, 36, 118); big.Note(bb, 1, 46, 110); taiko.Note(bb, 1, 38, 110); if (b % 2 == 1) { taiko.Note(bb + 2.5, 0.5, 38, 70); taiko.Note(bb + 3, 0.5, 38, 84); } continue; }
            if (pre) { Roll(timp, bb, 4, 45, 50 + (b - 54) * 28, 76 + (b - 54) * 34); Roll(kit, bb, 4, 38, 40 + (b - 54) * 30, 66 + (b - 54) * 40); if (b == 54) big.Note(bb, 8, 52, 115); taiko.Hits(b + 1, "X..x..x.", 38, 96, 0.5, 0.5); continue; }
            taiko.Hits(b + 1, fill ? "X..x..xx" : "X..x..x.", 38, outro ? 84 : 102, 0.5, 0.5);
            if (b >= 4 && !outro)
            {
                taiko.Hits(b + 1, "..x..x.x", 45, 82, 0.5, 0.5); big.Hits(b + 1, "X.....x.", 36, 106, 0.5, 0.5); big.Hits(b + 1, "x.xxx.xxx.xxx.xx", 42, b >= 32 ? 64 : 54, 0.25, 0.25);
                int off = ((Deg(sc, 0, roots[b]) % 12) + 12) % 12; timp.Note(bb, 1.5, 38 + off, 104);   // D2..C#3
                if (fill) { tom.Hits(b + 1, "......xx", 41, 96, 0.5, 0.5); tom.Hits(b + 1, "....x...", 45, 90, 0.5, 0.5); Roll(kit, bb + 3, 1, 38, 60, 100); }
                if (b >= 32 && b < 48 || b >= 56) kit.Hits(b + 1, "..x...x.", 38, 62, 0.5, 0.5);
            }
        }
        foreach (int b in new[] { 8, 24, 32, 40, 56, 64 }) kit.Note(b * 4, 4, 57, 104);
        return s;
    }

    static void Pieces1()
    {
        AddSingle("menu_theme", "Main theme: mournful solo cello over drone and choir, one build, loop", "D minor (Aeolian, Phrygian cadence)", 150, 180, MenuTheme);
        AddSingle("town", "Lastlamp hub: finger-picked guitar and harp melody, warm and melancholy, loop", "B minor / D major", 120, 150, Town, true, "6/8");
        AddSingle("intro_bed", "Opening narration bed: drone, distant choir, soft swell at the end (not looped)", "D minor", 60, 70, IntroBed, false);
        AddSingle("boss", "Zone boss: relentless 12/8 ostinato, choir stabs, big drums, loop", "G Phrygian", 105, 135, Boss, true, "12/8");
        AddSingle("boss_final", "Final boss: the main theme inverted and darkened, loop", "D Phrygian", 150, 150, BossFinal);
    }
}

/// <summary>Short SoundFont-scored stingers delivered as stereo SFX.</summary>
public static class Stingers
{
    public static St Victory(V v)
    {
        var s = new Score("victory_sting", 100, 2) { Loop = false, Tail = 1.1, FadeOut = 1.6, RevTime = 2.6 };
        var cello = s.Sf("cello", 42, -19, -0.15, -6); cello.Swell = (-6, 0, -6, 0.35); cello.Vib = 45; cello.Legato = 0.03;
        var viola = s.Sf("viola", 41, -21, 0.15, -6); viola.Swell = (-6, 0, -6, 0.35); viola.Vib = 45; viola.Legato = 0.03;
        cello.At(1).Play("!f d3:1 c4:1 bb3:.75 a3:.25 a3:1 | d4:4 |"); viola.At(1).Play("!f d4:1 c5:1 bb4:.75 a4:.25 a4:1 | f#4:4 |");
        var str = s.Sf("strings", 49, -22, 0, -5); str.Swell = (-12, 0, -8, 0.4); str.Poly = true;
        str.At(1).Play("!mf d2+a2+d3:2 d2+bb2+f3:1 a1+e2+a2:1 | !f d2+a2+d3+f#3+a3:4 |");
        var choir = s.Sf("choir", 52, -24, 0, -3); choir.Swell = (-12, 0, -8, 0.4); choir.Poly = true; choir.At(2).Play("!f a3+d4+f#4:4 |");
        var timp = s.Sf("timpani", 47, -21, 0, -8); timp.Note(0, 2, 38, 112).Note(3, 1, 45, 84).Note(3.5, 0.5, 45, 96).Note(4, 3, 38, 118);
        var taiko = s.Sf("taiko", 116, -22, 0, -8); taiko.Note(0, 1, 38, 118).Note(4, 1, 38, 122);
        var harp = s.Sf("harp", 46, -23, 0.3, -4); harp.TimeJit = 0.008; harp.Legato = 0.5;
        harp.At(2).Play("!f d3:.25 a3:.25 d4:.25 f#4:.25 a4:.25 d5:.25 f#5:.25 a5:2.25 |");
        var bell = s.Sf("bell", 14, -24, -0.2, -3); bell.Note(4, 4, 62, 104);
        return Music.Mix(s, false);
    }
    public static St Death(V v)
    {
        var s = new Score("death_sting", 104, 2) { Loop = false, Tail = 0.9, FadeOut = 1.5, RevTime = 3.2 };
        var cello = s.Sf("cello", 42, -19, -0.1, -5); cello.Swell = (-6, 0, -9, 0.3); cello.Vib = 40; cello.Legato = 0.04;
        cello.At(1).Play("!mf a3:.75 g3:.75 f3:.75 eb3:.75 d3:5 |");
        var str = s.Sf("strings", 49, -22, 0, -5); str.Swell = (-10, 0, -10, 0.3); str.Poly = true;
        str.At(1).Play("!mf d2+a2+f3:1.5 d2+bb2+eb3:1.5 d2+a2+d3:5 |");
        var lowch = s.Dsp("lowchoir", Syn.Choir(Sfx.VU, Sfx.VO, 3, 1.0, 9, 0.5, 1.2), -24, 0, -3); lowch.At(1).Play("!mf d2+a2:3 d2+a2:5 |");
        var timp = s.Sf("timpani", 47, -20, 0, -8); timp.Note(0, 2, 38, 116).Note(3, 3, 38, 100);
        var big = s.Dsp("bigdrum", Syn.Drums(), -21, 0, -10); big.Note(0, 1, 36, 120).Note(0, 1, 46, 110);
        var bell = s.Sf("bell", 14, -22, 0, -2); bell.Note(0, 4, 50, 100).Note(3, 4, 50, 112);
        return Music.Mix(s, false);
    }
}
