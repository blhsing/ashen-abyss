"""Post-processing per voice profile. The voice itself (timbre, pace, manner) comes from the VoxCPM2
voice description in voices.py; this file only polishes.

Humans (narrator, town, heroes): light EQ, gentle compression, a little room. No pitch shifting, no
time stretching, no doubles, no saturation.

Bosses: INTELLIGIBILITY FIRST (user's decision after the first listening test: the old chains were too
unintelligible and too coarse). Rules every boss chain follows:
  * the dry consonants stay intact: at most 2.5 semitones of pitch-down, formant-preserving
    (fx.pitch_shift_fp), no time stretching - slowness comes from the voice description;
  * weight comes from a quiet octave-down copy (about -14 dB), low-passed so it cannot smear speech;
  * nothing harsh on the main voice: no ring modulation, comb, growl or distortion. Colour (metal for the
    sand tyrant, breath for the grand hush, grit for the gravedigger) sits on quiet PARALLEL layers;
  * reverb with 60-90 ms pre-delay and the wet signal 15-19 dB under the dry, so the direct sound leads;
  * a gentle presence lift around 3 kHz, gentle compression (ratio 2, slow attack).
scaled(profile, k) gives the same chain at strength k: 0 = dry, 0.5 = "light", 1 = "medium" (default).
"""
from __future__ import annotations


def _rv(rt60, send_db, pre=20, size=1.0, damp=5000, low=150, high=8000):
    return dict(rt60=rt60, send_db=send_db, pre_delay_ms=pre, size=size, damp_hz=damp, low_cut_hz=low, high_cut_hz=high)


HP, LP, LO, HI, PK = "hp", "lp", "lowshelf", "highshelf", "peak"
SOFT = dict(threshold_db=-20, ratio=2.0, attack_ms=10, release_ms=160)          # gentle, no pumping
PRESENCE = (PK, 3000, 2.0, 0.8)
SUB = dict(pitch=-12, gain_db=-14, lp=450)                                       # octave-down weight, speech band untouched

PROFILES = {
    # ------------------------------------------------------------ narrator and town
    "narrator": dict(note="close-miked storyteller, a dark hall far behind", comp=SOFT,
                     eq=[(HP, 55), (LO, 140, 1.5), (PK, 300, -1.0), (PK, 3200, 1.0, 0.9)],
                     reverb=_rv(1.8, -24, 40, 1.3, 3000, 220, 3500)),
    "elder": dict(note="old man by the fire", comp=SOFT,
                  eq=[(HP, 70), (LO, 180, 1.0), (PK, 2600, 1.0), (HI, 7000, -1.0)], reverb=_rv(0.7, -26, 10, 0.6, 4000, 150, 5000)),
    "smith": dict(note="forge, close", comp=SOFT,
                  eq=[(HP, 70), (LO, 150, 1.5), (PK, 2800, 1.0)], reverb=_rv(0.6, -26, 8, 0.5, 5000)),
    "merchant": dict(note="bright, open stall", comp=SOFT,
                     eq=[(HP, 110), (PK, 3500, 1.0), (HI, 8000, 1.0)], reverb=_rv(0.5, -27, 8, 0.45, 7000, 150, 8000)),
    "occultist": dict(note="cool, a stone room", comp=SOFT,
                      eq=[(HP, 100), (PK, 280, -1.5), (PK, 4000, 1.0)], reverb=_rv(1.2, -24, 25, 0.9, 6000, 150, 7000)),
    "jeweler": dict(note="soft, close", comp=SOFT,
                    eq=[(HP, 110), (LO, 200, 1.0), (HI, 8000, 0.5)], reverb=_rv(0.5, -27, 8, 0.45, 6000)),
    "villager_m": dict(note="plain", comp=SOFT, eq=[(HP, 80), (PK, 2500, 1.0)], reverb=_rv(0.45, -27, 6, 0.45)),
    "villager_f": dict(note="plain", comp=SOFT, eq=[(HP, 110), (PK, 2800, 1.0)], reverb=_rv(0.45, -27, 6, 0.45)),

    # ------------------------------------------------------------ heroes (barks: dry and close)
    "hero_emberblade": dict(note="plate warden", comp=SOFT, eq=[(HP, 65), (LO, 150, 1.5), (PK, 2400, 1.0)], reverb=_rv(0.6, -26, 10, 0.6, 4500)),
    "hero_astromancer": dict(note="clear caster", comp=SOFT, eq=[(HP, 110), (PK, 3200, 1.0), (HI, 9000, 1.0)], reverb=_rv(0.9, -25, 20, 0.8, 8000, 150, 8000)),
    "hero_hunter": dict(note="dry, quiet (no room at all)", comp=SOFT, eq=[(HP, 120), (PK, 500, -1.0)], loudness_offset=-1.0),
    "hero_soulbinder": dict(note="hollow hall", comp=SOFT, eq=[(HP, 60), (PK, 420, 1.5, 1.2), (PK, 3200, 1.0)], reverb=_rv(1.5, -22, 25, 1.0, 3500, 150, 4500)),
    "hero_wildsoul": dict(note="outdoors", comp=SOFT, eq=[(HP, 70), (LO, 160, 1.5), (PK, 2600, 1.0)], reverb=_rv(0.6, -26, 10, 0.7, 4500)),
    "hero_forgebody": dict(note="tight and dry", comp=SOFT, eq=[(HP, 80), (PK, 180, 1.0), (PK, 2600, 1.5)], reverb=_rv(0.45, -27, 6, 0.5)),
    "hero_lampknight": dict(note="chapel", comp=SOFT, eq=[(HP, 100), (LO, 220, 1.0), (PK, 2800, 1.0)], reverb=_rv(1.1, -24, 25, 0.9, 5000, 150, 6000)),
    "hero_pactbinder": dict(note="smooth, a study", comp=SOFT, eq=[(HP, 80), (LO, 200, 1.0), (HI, 9000, 0.5)], reverb=_rv(1.0, -24, 20, 0.9, 5000, 150, 6500)),

    # ------------------------------------------------------------ bosses: clear first, menacing second
    "boss_gravedigger": dict(note="very low and slow; a little grit on a parallel layer; damp earth", boss=True,
                             pitch=-2.5, comp=SOFT,
                             layers=[SUB, dict(pitch=-2.5, gain_db=-19, drive=0.6, hp=300, lp=2400)],
                             eq=[(HP, 45), (LO, 110, 2.5), PRESENCE, (HI, 8000, -1.5)],
                             reverb=_rv(2.2, -19, 60, 1.2, 2800, 200, 3500)),
    "boss_rimelord": dict(note="low and cold; an icy shimmer that lives only in the reverb", boss=True,
                          pitch=-2.0, comp=SOFT,
                          layers=[dict(pitch=-12, gain_db=-15, lp=420), dict(pitch=12, gain_db=-12, hp=3500, reverb_only=True)],
                          eq=[(HP, 55), (LO, 130, 1.5), (PK, 320, -1.5), (PK, 3200, 2.5, 0.8), (HI, 8500, 1.5)],
                          reverb=_rv(4.0, -17, 75, 1.7, 9000, 350, 11000)),
    "boss_bishop": dict(note="thin and nasal, a quiet fifth-below double, cathedral", boss=True,
                        pitch=0.0, comp=SOFT,
                        layers=[dict(pitch=-7, gain_db=-15, delay_ms=12, lp=2500)],
                        eq=[(HP, 150), (LO, 250, -2.0), (PK, 1500, 2.0, 1.5), (PK, 3200, 2.0, 0.8)],
                        reverb=_rv(5.0, -16, 90, 1.8, 4500, 250, 6000)),
    "boss_mire_mother": dict(note="female, two quiet detuned doubles (the 'wet' chorus), damp cave", boss=True,
                             pitch=-1.5, comp=SOFT,
                             layers=[dict(pitch=-1.75, gain_db=-13, delay_ms=23), dict(pitch=-1.3, gain_db=-14, delay_ms=41),
                                     dict(pitch=-12, gain_db=-17, lp=380)],
                             eq=[(HP, 70), (LO, 180, 2.0), (PK, 2800, 2.0, 0.8), (HI, 7000, -1.5)],
                             reverb=_rv(2.4, -17, 60, 1.2, 3000, 200, 4500)),
    "boss_sand_tyrant": dict(note="proud king; metal (ring modulation + resonant comb) only on a quiet parallel layer", boss=True,
                             pitch=-2.0, comp=SOFT,
                             layers=[dict(pitch=-2.0, gain_db=-16, delay_ms=9, ring=(140, 1.0), comb=(6.1, 0.6, 0.7), hp=700),
                                     dict(pitch=-12, gain_db=-16, lp=420)],
                             eq=[(HP, 70), (PK, 1800, 1.5, 1.5), (PK, 3400, 2.5, 0.8)],
                             reverb=_rv(1.8, -18, 60, 0.8, 8000, 200, 9000)),
    "boss_grand_hush": dict(note="soft-spoken; a breathy double under the voice and a whisper that lives in a vast reverb", boss=True,
                            pitch=-1.5, comp=SOFT,
                            layers=[dict(pitch=-1.5, gain_db=-15, delay_ms=26, whisper=True, hp=900),
                                    dict(pitch=-1.5, gain_db=-9, whisper=True, hp=700, reverb_only=True)],
                            eq=[(HP, 80), (PK, 300, -2.0), (PK, 3500, 2.5, 0.8), (HI, 8000, 1.0)],
                            reverb=_rv(6.0, -15, 90, 2.0, 6000, 300, 8000)),
    "boss_generic": dict(note="any boss without a chain of its own: two semitones down, quiet octave-down weight, a hall behind the voice", boss=True,
                         pitch=-2.0, comp=SOFT, layers=[SUB],
                         eq=[(HP, 50), (LO, 120, 2.0), PRESENCE, (HI, 9000, -1.0)],
                         reverb=_rv(3.0, -18, 70, 1.5, 4000, 220, 5500)),
    "boss_osaros": dict(note="deepest: 2.5 semitones down, quiet octave-down weight, vast hall well behind the voice", boss=True,
                        pitch=-2.5, comp=SOFT,
                        layers=[SUB],
                        eq=[(HP, 40), (LO, 110, 2.5), PRESENCE, (HI, 9000, -1.0)],
                        reverb=_rv(4.5, -17, 85, 2.0, 3500, 220, 5000)),
}

BOSS_STRENGTH = {"dry": 0.0, "light": 0.5, "medium": 1.0}


def resolve(voice: str | None) -> tuple[str, dict]:
    """Profile for a vo_lines.json "voice" value; unknown names fall back to the narrator."""
    v = (voice or "").strip().lower().replace("-", "_").replace(" ", "_")
    if v in PROFILES:
        return v, PROFILES[v]
    if v.startswith("boss_"):
        return "boss_generic", PROFILES["boss_generic"]
    if v.startswith("hero_"):
        return "hero_emberblade", PROFILES["hero_emberblade"]
    return "narrator", PROFILES["narrator"]


def scaled(profile: dict, k: float) -> dict:
    """The same chain at strength k (1 = as designed, 0 = dry: no pitch, no layers, no reverb, no EQ)."""
    if k >= 0.999:
        return profile
    if k <= 0.001:
        return dict(note="dry", comp=dict(threshold_db=0, ratio=1.0), loudness_offset=profile.get("loudness_offset", 0.0))
    p = dict(profile)
    back = 1.0 - k
    p["pitch"] = profile.get("pitch", 0.0) * k
    p["layers"] = [dict(l, gain_db=l.get("gain_db", -12.0) - 8.0 * back,
                        pitch=l.get("pitch", 0.0) if abs(l.get("pitch", 0.0)) >= 6.5 else l.get("pitch", 0.0) * k)
                   for l in profile.get("layers", [])]
    p["eq"] = [(b[0], b[1]) + ((b[2] * k,) + tuple(b[3:]) if len(b) > 2 else ()) for b in profile.get("eq", [])]
    if profile.get("reverb"):
        r = dict(profile["reverb"])
        r["send_db"] = r.get("send_db", -20.0) - 6.0 * back
        p["reverb"] = r
    return p
