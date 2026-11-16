// Loot / UI / world SFX. The loot ladder adds one layer per rarity tier; legendary owns a bell register no combat sound uses.
using static AudioTool.A;
using static AudioTool.F;
using static AudioTool.Gen;

namespace AudioTool;

public static partial class Sfx
{
    static float[] Coin(Rng r, double f, double t60 = 0.18)
        => Mx().Add(Modes(t60 * 1.3, new[] { (f, t60, 1.0), (f * 1.62, t60 * 0.8, 0.55), (f * 2.41, t60 * 0.6, 0.35), (f * 3.33, t60 * 0.45, 0.2) })).Add(Click(r, Math.Min(10000, f * 1.6), 1.5, 0.002), -9).Out();

    static float[] DropThud(Rng r)
        => Mx().Add(Body(r, 240, 1.2, 0.07), 0).Add(Thump(110, 70, 0.02, 0.07), -3).Add(Click(r, 2000, 1, 0.005), -10).Add(Pink(r, 0.07).BP(1800, 0.8).Env(AD(0.005, 0.06)), -12).Out();

    /// <summary>Deep warm bloom used under the legendary / unique drops.</summary>
    static float[] WarmBloom(Rng r, double d)
    {
        var parts = new[] { 73.42, 110.0, 146.83 }.SelectMany(f => Harm(f, 6, 1.5)).ToArray();
        return Tones(r, d, parts, 0.25).SvfLP(PtsE(0, 300, 0.35, 1400, d, 350), 0.9).Env(PtsS(0, 0, 0.3, 1, 0.8, 0.85, d, 0));
    }

    static void Ui()
    {
        Cat("ui");
        Add("gold_pickup", 3, -9, v =>
        {
            var r = v.R; var m = Mx(); double t = 0; int n = r.I(2, 3);
            for (int k = 0; k < n; k++) { m.Add(Coin(r, r.X(3200, 5200)), k == 0 ? 0 : r.R(-5, -1), t); t += r.R(0.035, 0.065); }
            return m.Out();
        }, pv: 1.0, desc: "gold picked up: bright coin clinks");
        Add("drop_common", 1, -12, v => DropThud(v.R).ReverbSt(0.3, -18, 0.4), stereo: true, desc: "loot tier 1: soft thud");
        Add("drop_magic", 1, -10, v =>
        {
            var r = v.R;
            return Mx().Add(DropThud(r), -3).Add(BellTone(r, 1174.66, 0.7), 0, 0.03).Out().ReverbSt(0.7, -13, 0.6);
        }, stereo: true, desc: "loot tier 2: thud + single high chime");
        Add("drop_rare", 1, -8, v =>
        {
            var r = v.R;
            return Mx().Add(DropThud(r), -4).Add(BellTone(r, 1174.66, 1.0), 0, 0.03).Add(BellTone(r, 587.33, 1.1), -5, 0.03).Add(BellTone(r, 880, 1.0), -8, 0.03)
                .Add(White(r, 0.7).HP(5000).Env(Bell(0.7, 0.25)), -20, 0.03).Out().ReverbSt(1.1, -11, 0.8);
        }, stereo: true, desc: "loot tier 3: thud + chime with octave/fifth body + shimmer");
        Add("drop_legendary", 1, -3, v =>
        {
            var r = v.R; var st = new St(N(3.0));
            st.Add(DropThud(r), -10).Add(WarmBloom(r, 2.6), -7, 0.02)
              .Add(BellTone(r, 880, 2.4), -3, 0.06, -0.25).Add(BellTone(r, 1174.66, 2.8), 0, 0.30, 0.25)
              .Add(Tones(r, 2.2, new[] { (2349.3, 1.0), (3520.0, 0.6), (4698.6, 0.35) }, 0.7).Env(Bell(2.2, 0.3)), -20, 0.3)
              .Add(Crackle(r, 2.0, t => 35 * Bell(2.0, 0.3)(t), 4500, 11000, 0.01, 0.05, 1.3), -24, 0.3, 0.4);
            return st.ReverbSt(2.4, -8, 1.0, 6000);
        }, stereo: true, desc: "loot tier 4 (signature): rising bell pair A5->D6 over a deep warm bloom");
        Add("drop_unique", 1, -4, v =>
        {
            var r = v.R; var st = new St(N(3.6));
            st.Add(Mx().Add(Thump(80, 36, 0.08, 0.5)).Add(DropThud(r), -6).Out(), -6).Add(WarmBloom(r, 3.2), -6, 0.02)
              .Add(BellTone(r, 880, 2.4), -4, 0.06, -0.3).Add(BellTone(r, 1174.66, 2.8), -2, 0.30, 0.2).Add(BellTone(r, 1479.98, 3.0), 0, 0.56, 0.0);
            double[] ch = { 293.66, 440.0, 369.99 }; double[] pans = { -0.5, 0.5, 0 };
            for (int i = 0; i < 3; i++) st.Add(Voice(r, 3.0, C(ch[i] * r.R(0.998, 1.002)), K((0, VO), (1.2, VA), (3.0, VO)), 1.1, 0.06, 0.008, 0.05).Env(PtsS(0, 0, 0.9, 1, 1.6, 0.8, 3.0, 0)), -15, 0.3, pans[i]);
            st.Add(Tones(r, 2.6, new[] { (2349.3, 1.0), (2959.96, 0.7), (3520.0, 0.6), (4698.6, 0.35) }, 0.7).Env(Bell(2.6, 0.35)), -19, 0.5)
              .Add(Crackle(r, 2.4, t => 45 * Bell(2.4, 0.3)(t), 4500, 11000, 0.01, 0.05, 1.3), -23, 0.4, -0.4);
            return st.ReverbSt(2.8, -7, 1.0, 6000);
        }, stereo: true, desc: "loot tier 5: legendary layers + third rising tone, choir and low hit");
        Add("item_pickup", 2, -12, v =>
        {
            var r = v.R;
            return Mx().Add(Whoosh(r, 0.12, PtsE(0, 1200, 0.06, 3500, 0.12, 2000), 1.5, Bell(0.12, 0.45)), 0).Add(Click(r, 2500, 1.5, 0.004), -6, 0.01).Add(Body(r, 400, 1.5, 0.03), -6).Out();
        }, desc: "item grabbed");
        Add("equip_armor", 2, -8, v =>
        {
            var r = v.R;
            return Mx().Add(Body(r, 200, 1, 0.09), 0).Add(Squelch(r, 0.15, 300, 1200, 20, 4).Env(Bell(0.15, 0.4)), -10)
                .Add(Shards(r, 0.35, 22, 2500, 8000, 0.02, 0.06, RPlate.Take(3).ToArray()), -7).Add(Click(r, 3000, 2, 0.004), -8, 0.12).Out();
        }, desc: "armour equipped: thump, leather, mail jingle");
        Add("equip_weapon", 2, -8, v =>
        {
            var r = v.R;
            return Mx().Add(White(r, 0.3).SvfBP(PtsE(0, 2500, 0.25, 7000), 4).Env(Bell(0.3, 0.6)), -5).Add(Metal(r, r.R(2400, 3000), 0.7, 5, RBar, 0.4), -3, 0.12)
                .Add(Body(r, 300, 1.2, 0.05), -10).Out();
        }, desc: "weapon equipped: steel slide and ring");
        Add("equip_jewel", 1, -12, v =>
        {
            var r = v.R;
            return Mx().Add(Metal(r, 3500, 0.5, 4, RGlass, 0.5), 0).Add(Metal(r, 5250, 0.4, 3, RGlass, 0.5), -6, 0.07).Add(Click(r, 5000, 2, 0.002), -10).Out();
        }, desc: "ring / amulet equipped: small crystalline ting");
        Add("ui_click", 2, -14, v =>
        {
            var r = v.R; double x = v.Idx == 0 ? 1 : 0.84;
            return Mx().Add(Click(r, 1700 * x, 2.5, 0.007), 0).Add(Sine(0.03, Exp(1000 * x, 700 * x, 0.02)).Env(AD(0.001, 0.025)), -5).Add(Thump(200, 140, 0.008, 0.02), -10).Out();
        }, stereo: true, pv: 0, desc: "UI click (2 = lower 'back' flavour)");
        Add("ui_hover", 1, -24, v => Mx().Add(Sine(0.025, 1500).Env(AD(0.002, 0.02)), 0).Add(Click(v.R, 3000, 3, 0.003), -12).Out().LP(4000), stereo: true, desc: "UI hover: very soft tick");
        Add("ui_open", 1, -14, v =>
        {
            var r = v.R;
            return Mx().Add(Whoosh(r, 0.22, PtsE(0, 300, 0.2, 1500), 1.2, Bell(0.22, 0.6)), 0).Add(Squelch(r, 0.2, 2000, 7000, 40, 1.5, 0.9).Env(Bell(0.2, 0.5)), -10)
                .Add(Body(r, 180, 1, 0.05), -6, 0.16).Out();
        }, stereo: true, desc: "panel opens: parchment + up-sweep");
        Add("ui_close", 1, -14, v =>
        {
            var r = v.R;
            return Mx().Add(Whoosh(r, 0.18, PtsE(0, 1400, 0.18, 280), 1.2, Bell(0.18, 0.35)), 0).Add(Body(r, 160, 1, 0.06), -3, 0.1).Out();
        }, stereo: true, desc: "panel closes: down-sweep + thump");
        Add("ui_error", 1, -12, v =>
        {
            float[] Blip(double f) => Mx().Add(Square(0.08, f).LP(700)).Add(Sine(0.08, f * 0.667), -3).Out().Env(AD(0.003, 0.065));
            return Mx().Add(Blip(155), 0).Add(Blip(138), -1, 0.1).Out();
        }, stereo: true, desc: "denied: two dull low blips");
        Add("skill_point", 1, -9, v =>
        {
            var r = v.R; var st = new St(N(1.0));
            st.Add(Whoosh(r, 0.25, PtsE(0, 400, 0.25, 3000), 1.5, Bell(0.25, 0.7)), -8)
              .Add(Tones(r, 0.8, Harm(587.33, 5, 1.3).Concat(Harm(880, 4, 1.4, 0.6)), 0.5).Env(PtsS(0, 0, 0.08, 1, 0.3, 0.7, 0.8, 0)), 0, 0.15)
              .Add(Crackle(r, 0.6, 50, 4000, 10000, 0.01, 0.04, 1.4).Env(Bell(0.6, 0.3)), -18, 0.2, 0.3);
            return st.ReverbSt(0.8, -10, 0.7);
        }, stereo: true, desc: "skill point spent: ember ignites");
        Add("quest_accept", 1, -8, v =>
        {
            var r = v.R; var st = new St(N(1.4));
            st.Add(Mx().Add(Thump(110, 70, 0.03, 0.25)).Add(Body(r, 180, 1, 0.12), -4).Out(), -4)
              .Add(Pluck(r, 220, 1.2), -2, 0.0, -0.2).Add(Pluck(r, 293.66, 1.4), 0, 0.22, 0.2)
              .Add(Squelch(r, 0.25, 2000, 7000, 40, 1.5, 0.9).Env(Bell(0.25, 0.4)), -16);
            return st.ReverbSt(1.2, -9, 0.9);
        }, stereo: true, desc: "quest accepted: frame-drum + rising fourth");
        Add("quest_complete", 1, -7, v =>
        {
            var r = v.R; var st = new St(N(2.4));
            st.Add(Mx().Add(Thump(100, 60, 0.04, 0.3)).Add(Body(r, 170, 1, 0.14), -4).Out(), -4);
            double[] f = { 293.66, 440.0, 587.33 };
            for (int i = 0; i < 3; i++) { st.Add(Pluck(r, f[i], 1.5), -3 + i, i * 0.18, -0.3 + 0.3 * i); st.Add(BellTone(r, f[i] * 2, 1.2), -12 + i, i * 0.18, 0.3 - 0.3 * i); }
            st.Add(Tones(r, 2.0, new[] { 146.83, 220.0, 293.66, 369.99, 329.63 }.SelectMany(q => Harm(q, 4, 1.5)), 0.3).Env(PtsS(0, 0, 0.6, 1, 1.1, 0.8, 2.0, 0)), -10, 0.3);
            return st.ReverbSt(1.8, -8, 1.0);
        }, stereo: true, desc: "quest complete: three rising tones into a warm chord");
        Add("portal_open", 1, -5, v =>
        {
            var r = v.R; double t0 = 1.15; Fn rise = t => Math.Pow(Clamp(t / t0, 0, 1), 1.8);
            var swirl = White(r, t0).SvfBP(t => 400 * Math.Pow(2, 3 * t / t0) * (1 + 0.15 * Math.Sin(TAU * 9 * t)), 4).Env(rise);
            var tone = Mx().Add(Sine(t0, Exp(110, 440, t0))).Add(Sine(t0, Exp(165, 660, t0)), -4).Out().Env(rise);
            var pre = Mx().Add(swirl, 0).Add(tone, -6).Out().Chorus(3, 2.5, 0.6, 0.5, 0.5).Fade(0, 0.006);
            var m = Mx().Add(pre, -3).Add(Thump(90, 40, 0.08, 0.4), 0, t0).Add(White(r, 0.4).SvfBP(PtsE(0, 3000, 0.4, 500), 1.2).Env(AD(0.005, 0.35)), -4, t0)
                .Add(Metal(r, 1100, 0.7, 5, RGlass, 0.5), -10, t0).Out();
            return m.Reverb(0.9, -10, 0.8);
        }, desc: "portal tears open");
        Add("portal_loop", 1, -12, v =>
        {
            var r = v.R; double L = 3.0, d = L + 0.4; int n = N(L);
            // tonal bed uses whole cycles per loop so it is exactly periodic
            double Q(double f) => Math.Round(f * L) / L;
            var hum = Tones(new Rng(7), L, new[] { (Q(82.4), 1.0), (Q(82.4) + 1 / L, 0.8), (Q(123.5), 0.6), (Q(123.5) + 2 / L, 0.5), (Q(164.8), 0.35), (Q(329.6) + 1 / L, 0.12) }, 0).Len(n);
            var swirl = Pink(r, d).SvfBP(t => 700 * Math.Pow(2, 1.2 * Math.Sin(TAU * t * 2 / L)), 3).Env(t => 0.6 + 0.4 * Math.Sin(TAU * t * 4 / L)).LoopXfade(n);
            var sh = White(r, d).HP(6000).Env(Wander(r, 0.2, 1, 5, d)).LoopXfade(n);
            return Mx().Add(hum, 0).Add(swirl, -6).Add(sh, -18).Out().Len(n);
        }, loop: true, desc: "open portal hum and swirl, loop");
        Add("chest_open", 2, -7, v =>
        {
            var r = v.R;
            return Mx().Add(Mx().Add(Click(r, 2200, 2, 0.006)).Add(Metal(r, 1800, 0.06, 4), -3).Out(), -3)
                .Add(Creak(r, 0.42, PtsE(0, r.R(80, 100), 0.42, r.R(150, 180)), new[] { 600.0, 1100, 1900 }).Env(Bell(0.42, 0.5, 0.7)), -4, 0.05)
                .Add(Mx().Add(Body(r, 170, 1.2, 0.09)).Add(Thump(100, 65, 0.02, 0.08), -3).Out(), 0, 0.5)
                .Add(Shards(r, 0.3, 6, 4000, 9000, 0.05, 0.12), -19, 0.5).Out();
        }, desc: "chest: latch, creak, lid thump");
        Add("door_open", 1, -6, v =>
        {
            var r = v.R;
            var m = Mx().Add(Mx().Add(Body(r, 220, 1.2, 0.07)).Add(Click(r, 1800, 2, 0.008), -4).Out(), -2)
                .Add(Creak(r, 0.9, PtsE(0, 35, 0.45, 80, 0.9, 50), new[] { 320.0, 700, 1300 }, 12).Env(Bell(0.9, 0.5, 0.6)), -3, 0.1)
                .Add(Brown(r, 1.0).LP(120).Env(Bell(1.0, 0.4)), -8, 0.1).Add(Thump(80, 50, 0.03, 0.15), -4, 1.1).Out();
            return m.Reverb(0.6, -14, 0.7);
        }, desc: "heavy door: clunk, long creak, thud");
        Add("shrine", 1, -5, v =>
        {
            var r = v.R; double d = 2.0; var m = Mx();
            m.Add(Tones(r, d, new[] { 293.66, 440.0, 587.33, 739.99 }.SelectMany(q => Harm(q, 5, 1.4)), 0.4).Env(PtsS(0, 0, 0.5, 1, 1.0, 0.8, d, 0)), 0);
            var ch = Mx(); foreach (var f in new[] { 146.83, 220.0, 293.66 }) ch.Add(Voice(r, d, C(f), K((0, VO), (0.8, VA), (d, VO)), 1, 0.06, 0.008, 0.05).Env(PtsS(0, 0, 0.6, 1, 1.2, 0.8, d, 0)));
            m.Add(ch.Out(), -6).Add(BellTone(r, 880, 1.6), -8, 0.45).Add(Pink(r, 0.7).SvfBP(PtsE(0, 300, 0.5, 1500, 0.7, 700), 1).Env(Bell(0.7, 0.4)), -12);
            return m.Out().Reverb(1.6, -8, 1.0);
        }, desc: "shrine activated: warm chord, choir, bell");
        Add("anvil", 3, -3, v =>
        {
            var r = v.R; double f = r.R(1900, 2400); double[] ra = { 1, 1.19, 1.71, 2.28, 2.74, 3.76, 4.07, 5.31 };
            return Mx().Add(Metal(r, f, r.R(0.6, 0.9), 8, ra, 0.5, 0.8), 0).Add(Metal(r, f * 0.45, 0.15, 6), -6).Add(Click(r, 5000, 0.8, 0.004), -1)
                .Add(Body(r, 700, 2, 0.03), -8).Add(Thump(180, 110, 0.01, 0.04), -10).Out();
        }, pv: 0.8, desc: "smith's hammer on anvil");
        Add("enchant", 1, -6, v =>
        {
            var r = v.R; var m = Mx(); double[] f = { 587.33, 698.46, 880, 1174.66, 1760 };
            for (int i = 0; i < f.Length; i++) m.Add(Metal(r, f[i], 0.9, 5, RGlass, 0.4), -8 + i * 1.5, i * 0.12 + (i == 4 ? 0.02 : 0));
            m.Add(White(r, 0.6).SvfBP(PtsE(0, 500, 0.6, 6000), 2).Env(t => Math.Pow(t / 0.6, 2)).Fade(0, 0.01), -10)
             .Add(Tones(r, 1.4, Harm(110, 5, 1.4).Concat(Harm(165, 4, 1.5)), 0.3).Env(Bell(1.4, 0.35)), -10).Add(Shards(r, 0.6, 25, 4000, 11000, 0.03, 0.1), -8, 0.6);
            return m.Out().Reverb(1.2, -8, 0.9);
        }, desc: "enchanting: rising glass arpeggio and sparkle");
        Add("salvage", 1, -7, v =>
        {
            var r = v.R;
            return Mx().Add(Crackle(r, 0.25, Exp(1500, 100, 0.25), 700, 5000, 0.003, 0.015, 1.5), 0).Add(Metal(r, 700, 0.2, 8), -4).Add(Click(r, 2500, 1, 0.008), -3)
                .Add(Shards(r, 0.5, 8, 2500, 6000, 0.02, 0.06, RPlate.Take(3).ToArray()), -8, 0.15).Add(White(r, 0.32).BP(3000, 0.7).Env(AD(0.02, 0.3)), -12, 0.1)
                .Add(Thump(120, 70, 0.02, 0.08), -6).Out();
        }, desc: "item salvaged: crunch, clinks, ash puff");
        Add("gem_socket", 1, -9, v =>
        {
            var r = v.R;
            return Mx().Add(Click(r, 3500, 2, 0.004), 0).Add(Metal(r, 5200, 0.4, 3, RGlass, 0.5), -3, 0.01).Add(Metal(r, 1800, 0.1, 5), -8)
                .Add(Sine(0.4, 2600).Env(Bell(0.4, 0.2)), -16, 0.02).Out();
        }, desc: "gem seated in a socket");
        Add("coins", 3, -9, v =>
        {
            var r = v.R; var m = Mx(); int n = r.I(9, 14);
            for (int k = 0; k < n; k++) m.Add(Coin(r, r.X(2800, 6000), r.R(0.1, 0.2)), ToDb(r.R(0.3, 1)), 0.5 * Math.Pow(r.U(), 1.3));
            return m.Add(Body(r, 300, 1, 0.05), -10).Out();
        }, pv: 1.0, desc: "handful of coins (vendor / gold spill)");
        Add("break_wood", 3, -4, v =>
        {
            var r = v.R; var m = Mx(); double f = r.R(180, 260);
            m.Add(Click(r, r.R(1500, 2500), 1, 0.012), 0).Add(Click(r, r.R(1500, 2500), 1, 0.012), -3, r.R(0.02, 0.05)).Add(Click(r, r.R(1500, 2500), 1, 0.012), -5, r.R(0.07, 0.12));
            m.Add(Modes(0.25, new[] { 1, 1.6, 2.4, 3.3, 4.5 }.Select((q, i) => (f * q, r.R(0.06, 0.1), Math.Pow(0.7, i)))), 0).Add(Clack(r, r.R(320, 450), 0.07), -3, 0.03)
             .Add(Crackle(r, 0.4, Exp(700, 20, 0.4), 800, 5000, 0.003, 0.012, 2), -7, 0.01).Add(Thump(120, 65, 0.03, 0.12), -4);
            for (int k = 0; k < 3; k++) m.Add(Clack(r, r.R(500, 900), 0.04), r.R(-14, -9), r.R(0.2, 0.5));
            return m.Out();
        }, desc: "crate / barrel smashed");
        Add("break_pot", 3, -5, v =>
        {
            var r = v.R;
            return Mx().Add(Click(r, 4500, 1, 0.005), 0).Add(Click(r, 2200, 1.5, 0.01), -3).Add(Sine(0.05, Exp(380, 300, 0.04)).Env(AD(0.001, 0.045)), -4)
                .Add(Shards(r, 0.45, r.I(20, 26), 1500, 6500, 0.02, 0.07, new[] { 1, 1.83, 2.9 }), -2, 0.005)
                .Add(Crackle(r, 0.35, Exp(300, 20, 0.35), 2000, 7000, 0.002, 0.006), -12, 0.1).Add(Thump(140, 90, 0.02, 0.06), -8).Out();
        }, desc: "clay pot shatters");
        Add("torch_loop", 1, -14, v => FireBed(v.R, 3.1, 14, 260, 12, -3, -4, -20).LoopXfade(N(2.6)), loop: true, desc: "wall torch flame, mono loop");
        Add("lava_loop", 1, -10, v =>
        {
            var r = v.R; double L = 4.0, d = L + 0.6; int n = N(L);
            var roar = Brown(r, d).LP(180).Env(Wander(r, 0.6, 1, 1.5, d)).LoopXfade(n);
            var hiss = White(r, d).HP(3000).Env(Wander(r, 0.1, 1, 0.8, d)).LoopXfade(n);
            var ev = Mx(); for (int k = 0; k < 14; k++) ev.Add(Bubble(r, r.X(70, 200), r.R(0.08, 0.16), r.R(1.5, 2.0)), r.R(-8, 0), r.R(0, L));
            return Mx().Add(roar, 0).Add(ev.Out().LoopWrap(n), -5).Add(hiss, -22).Out().Len(n);
        }, loop: true, desc: "lava flow: thick roar and bubbling, mono loop");
        Add("screenshot", 1, -14, v =>
        {
            var r = v.R;
            float[] Sh(double x) => Mx().Add(Click(r, 2200 * x, 1.5, 0.006)).Add(Body(r, 900 * x, 2, 0.012), -4).Out();
            return Mx().Add(Sh(1), 0).Add(Sh(0.85), -2, 0.055).Add(White(r, 0.05).BP(4000, 3).Env(Bell(0.05, 0.5)), -14, 0.005).Out();
        }, stereo: true, desc: "soft camera shutter");
        Add("menu_whoosh", 1, -12, v =>
        {
            var r = v.R; double d = 0.6; Fn fc = PtsE(0, 300, 0.3, 2200, d, 700); Fn env = Bell(d, 0.45, 1.2);
            var l = Pink(r, d).SvfBP(fc, 1).Env(t => env(t) * Math.Cos(Clamp(t / d, 0, 1) * Math.PI / 2));
            var rr = Pink(r, d).SvfBP(fc, 1).Env(t => env(t) * Math.Sin(Clamp(t / d, 0, 1) * Math.PI / 2));
            var st = new St(l, rr); st.Add(Crackle(r, d, 30, 3000, 9000, 0.005, 0.02, 2).Env(env), -26, 0, 0.2);
            return st;
        }, stereo: true, desc: "menu transition whoosh (pans left to right)");
        Add("death_sting", 1, -3, v => Stingers.Death(v), stereo: true, desc: "player death stinger: low hit, falling line, bell toll");
        Add("victory_sting", 1, -3, v => Stingers.Victory(v), stereo: true, min: 4, max: 6, desc: "boss defeated stinger (4-6 s): main-theme motif resolving warm");
    }
}
