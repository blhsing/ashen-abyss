"""Track list for the Ashen Abyss soundtrack: what to ask the model for and what to check afterwards.

All prompts describe instrumentation, mood, tempo and form in generic musical terms only.  Nothing here names
or imitates an artist, composer, band, game, film or song.  Lyrics input is always the model's instrumental
marker ("[Instrumental]"), so no words are ever supplied.

Caption style.  Measured with prompt_lab.py on 150 s renders (lab/*.json, results in README.md): comma-separated
tag lists that start with broad genre anchors ("epic orchestral, cinematic, film score" for rhythmic pieces,
"dark ambient, film score" for sparse ones) keep this song-trained model instrumental and orchestral far more
reliably than descriptive sentences, section tags in the lyrics field, or "game / boss music" wording, which
drifted towards pop / hip-hop with voices at full track length.

Fields
  gen_s        seconds of audio to generate (longer than the loop, so the loop is cut from the body of the piece
               and the model's natural ending never reaches the game)
  loop_s       (min, max) allowed loop length in seconds; None = not looped (intro_bed)
  bpm, key, ts conditioning metadata given to the model (ts = beats per bar)
  pulse        True for rhythm-driven pieces (tempo is checked and must be clear), False for free-time pieces
  lufs         loudness target of the delivered file
  caption      the text prompt
  describe     short description used for audio-text similarity scoring (CLAP)
  seeds        fixed seeds, one candidate each
"""


def _t(name, gen_s, loop_s, bpm, key, ts, pulse, caption, describe, lufs=-18.0, seeds=None, pair=None, strict=False):
    return dict(name=name, gen_s=gen_s, loop_s=loop_s, bpm=bpm, key=key, ts=ts, pulse=pulse, caption=caption,
                describe=describe, lufs=lufs, seeds=seeds, pair=pair, strict=strict)


# The stricter bar (lead, 2026-11-18) for the last four pieces: the delivered file must be in a minor mode, a combat
# track must share the tonic of its exploration track, and the dynamics must not be flat (produce.py, STRICT_*).


COMBAT = "epic orchestral, cinematic, film score, battle, "
AMBIENT = "dark ambient, film score, "

TRACKS = [
    # ------------------------------------------------------------------ core pieces
    _t("menu_theme", 196, (150, 180), 60, "D minor", 4, False,
       "orchestral, cinematic, film score, main theme, mournful solo cello melody, slow, solemn, grave, memorable, "
       "low strings drone, distant wordless choir, sparse, one slow build then calm, minor key, Aeolian, Phrygian, "
       "spacious hall reverb, instrumental",
       "a slow mournful solo cello melody over a low drone with a distant wordless choir, dark and solemn"),
    _t("town", 170, (120, 150), 76, "B minor", 4, False,
       "medieval folk, acoustic, film score, warm, melancholy, gentle, intimate, hopeful, fingerpicked nylon string "
       "guitar melody, harp, soft low strings, slow, calm, tender, bittersweet, minor key, close warm sound, instrumental",
       "gentle fingerpicked acoustic guitar and harp melody with soft low strings, warm, melancholy and hopeful"),
    _t("intro_bed", 68, None, 60, "D minor", 4, False,
       AMBIENT + "underscore, very quiet, soft deep drone, distant wordless choir pad, still, hushed, slowly evolving, "
       "ancient, sorrowful, beatless, static, minimal, minor key, instrumental",
       "very quiet dark ambient drone with a distant wordless choir pad, still and hushed", lufs=-22.0),
    _t("boss", 150, (110, 130), 116, "G minor", 4, True,
       COMBAT + "dark, ominous, intense, driving, low strings ostinato, staccato strings, cellos, double basses, "
       "taiko drums, timpani, wordless choir, minor key, symphony orchestra, instrumental",
       "relentless low string ostinato with choir stabs and big pounding drums, aggressive dark orchestral battle music"),
    _t("boss_final", 180, (140, 160), 120, "D minor", 4, True,
       "epic orchestral, cinematic, film score, final battle, darkest, apocalyptic, vast, towering, relentless, "
       "overwhelming, thunderous taiko ensemble, deep war drums, massive wordless choir, pounding low strings ostinato, "
       "cellos, double basses, bowed metal, sub bass rumble, minor key, symphony orchestra, instrumental",
       "vast heavy apocalyptic battle music with thunderous war drums, massive wordless choir and pounding low strings"),

    # ------------------------------------------------------------------ regions: exploration (base) and combat
    _t("wastes_base", 165, (120, 150), 56, "E minor", 4, False,
       AMBIENT + "solo viola, lonely, slow, sparse, cold, desolate, bleak, mournful, breathy flute textures, wind, "
       "low drone, minor key, long reverb, minimal, instrumental",
       "a lonely slow solo viola over breathy airy textures and wind, cold, sparse and desolate ambient music",
       pair="wastes"),
    _t("wastes_combat", 150, (110, 130), 112, "E minor", 4, True,
       COMBAT + "cold, bleak, tense, driving, percussive, urgent solo viola, low strings ostinato, frame drums, "
       "low toms, breathy flute textures, wind, minor key, instrumental",
       "urgent viola and low string ostinato with pounding frame drums, cold bleak driving percussive battle music",
       pair="wastes"),
    _t("frost_base", 165, (120, 150), 54, "F# minor", 4, False,
       AMBIENT + "icy, glassy, thin, wide, frozen, crystalline, fragile, high bowed string harmonics, shimmering glass "
       "tones, sparse celesta notes, faint cold drone, very slow, vast empty space, long reverb, minor key, minimal, "
       "instrumental",
       "high glassy bowed string harmonics and sparse celesta notes, thin, icy, wide and empty ambient music",
       pair="frost"),
    _t("frost_combat", 150, (110, 130), 108, "F# minor", 4, True,
       COMBAT + "icy, cold, crystalline, urgent, driving, percussive, fast tremolo high strings, glassy string "
       "harmonics, celesta ostinato, glockenspiel, pounding frame drums, taiko drums, low string stabs, minor key, "
       "instrumental",
       "fast icy tremolo strings and celesta ostinato with pounding drums, cold crystalline driving battle music",
       pair="frost"),
    _t("crypt_base", 165, (120, 150), 52, "C minor", 4, False,
       AMBIENT + "hollow, cavernous, eerie, funereal, subterranean, deep wordless male choir drones, distant tolling "
       "bells, low string swells, very slow, empty stone hall, long dark reverb, minor key, minimal, instrumental",
       "deep wordless male choir drones and distant tolling bells in a huge stone hall, hollow eerie dark ambient",
       pair="crypt"),
    _t("crypt_combat", 150, (110, 130), 104, "C minor", 4, True,
       COMBAT + "grim, funereal, menacing, driving, percussive, deep wordless male choir, tolling bells, anvil "
       "strikes, pounding deep drums, frame drums, low strings ostinato, cavernous reverb, minor key, Phrygian, "
       "instrumental",
       "deep male choir, tolling bells and pounding deep drums with a low string ostinato, grim driving battle music",
       pair="crypt"),
    _t("mire_base", 165, (120, 150), 50, "Bb minor", 4, False,
       AMBIENT + "murky, swampy, slow, heavy, queasy, sickly, dissonant, bass clarinet, contrabassoon, detuned low "
       "wordless choir, wet dripping percussion, waterphone, soft log drums, fog, minor key, minimal, instrumental",
       "murky bass clarinet and contrabassoon with detuned low choir and wet dripping percussion, slow queasy swamp ambient",
       pair="mire"),
    _t("mire_combat", 150, (110, 130), 100, "Bb minor", 4, True,
       COMBAT + "murky, swampy, queasy, dissonant, tense, driving, percussive, bass clarinet ostinato, "
       "contrabassoon, detuned low choir, wet hand percussion, log drums, deep toms, minor key, instrumental",
       "bass clarinet and contrabassoon ostinato with wet hand percussion and log drums, murky dissonant driving battle music",
       pair="mire"),
    _t("sands_base", 165, (120, 150), 58, "A minor", 4, False,
       "ambient, film score, desert, ancient, dry, lonely, mysterious, slow, sparse, double-reed woodwind melody, "
       "plucked lute, harp, soft hand drum, hot empty air, Phrygian, minor key, minimal, instrumental",
       "a lonely double-reed woodwind line over sparse plucked lute and harp with soft hand drum, dry ancient desert ambient",
       pair="sands"),
    # v2 caption (2026-11-18): the v1 caption (fast lute ostinato, wailing double-reed melody) gave bright / comedic
    # or major-key results for all four seeds (v1 candidates: stage/ace_xl/cand/_v1_sands_combat/).
    _t("sands_combat", 150, (110, 130), 116, "A minor", 4, True,
       COMBAT + "tense, menacing, dark, desert ruins, ancient, driving, percussive, low frame drums, taiko drums, "
       "deep war drums, heavy hand percussion, distorted low brass, growling brass drone, low strings ostinato, "
       "Phrygian, harmonic minor, minor key, instrumental",
       "tense menacing desert ruins battle music with low taiko-like drums, heavy hand percussion, a growling low brass "
       "drone and a dark low string ostinato in a minor key",
       seeds=[7540, 7541, 7542, 7543], pair="sands", strict=True),
    _t("sanctum_base", 165, (120, 150), 55, "G minor", 4, False,
       AMBIENT + "sacred, solemn, austere, reverent, mournful, cold, slow pipe organ pads, wordless choir, long "
       "sustained vowels, distant tolling church bells, very slow, vast cathedral reverb, minor key, minimal, instrumental",
       "slow solemn pipe organ pads with wordless choir and distant tolling church bells, austere sacred ambient",
       pair="sanctum"),
    _t("sanctum_combat", 150, (110, 130), 110, "G minor", 4, True,
       COMBAT + "solemn, wrathful, austere, sacred, driving, percussive, pipe organ ostinato, powerful wordless "
       "choir, tolling church bells, pounding timpani, deep drums, marcato low strings, cathedral reverb, minor key, "
       "instrumental",
       "pipe organ ostinato with powerful wordless choir, tolling bells and pounding timpani, solemn wrathful battle music",
       pair="sanctum", strict=True),
    _t("abyss_base", 165, (120, 150), 60, "D minor", 4, False,
       AMBIENT + "menacing, ominous, infernal, immense, threatening, bowed metal scrapes, deep sub bass rumble, "
       "distant heavy drum hits, low dissonant string clusters, slow, vast cavern reverb, minor key, minimal, instrumental",
       "bowed metal scrapes, deep rumble and distant heavy drum hits with low dissonant strings, menacing infernal dark ambient",
       pair="abyss", strict=True),
    _t("abyss_combat", 150, (110, 130), 120, "D minor", 4, True,
       COMBAT + "infernal, menacing, relentless, brutal, driving, percussive, heavy war drums, taiko drums, bowed "
       "metal, anvil strikes, sub bass rumble, low strings ostinato, dissonant clusters, minor key, instrumental",
       "heavy pounding war drums with bowed metal shrieks, deep rumble and a brutal low string ostinato, menacing battle music",
       pair="abyss", strict=True),
]

# Fixed seeds, derived from the position in the list so they never change: 3 candidates for free-time pieces,
# 4 for rhythmic ones (their results vary more from seed to seed).
# The three pilot pieces also keep their first three seeds, which were rendered during the pilot with the earlier
# sentence-style captions (the exact caption of every candidate is stored next to it in <track>__s<seed>.json);
# their tag-style candidates use the seeds after those.
PILOT = ["menu_theme", "wastes_base", "boss"]
for _i, _tr in enumerate(TRACKS):
    if _tr["seeds"] is None:
        _n = 4 if _tr["pulse"] else 3
        _first = 3 if _tr["name"] in PILOT else 0
        _tr["seeds"] = [7100 + 10 * _i + k for k in range(_first + _n)]

BY_NAME = {t["name"]: t for t in TRACKS}

# label sets for zero-shot flags (audio-text similarity).  The first `ok` entries are acceptable content.
FLAG_SETS = {
    "vocal": dict(ok=3, labels=[
        "instrumental music with no voice",
        "a wordless choir singing sustained vowels",
        "a large choir singing dramatic chords with an orchestra",
        "a singer singing a song with lyrics",
        "a person speaking",
        "rap vocals"]),
    "genre": dict(ok=4, labels=[
        "dark orchestral film score",
        "dark ambient soundscape",
        "medieval folk music with acoustic instruments",
        "ancient desert folk music with plucked strings and hand drums",
        "a pop song",
        "electronic dance music with synthesizers",
        "rock music with electric guitars and a drum kit",
        "a hip hop beat",
        "jazz music"]),
    "mood": dict(ok=3, labels=[
        "dark, ominous, mournful music",
        "calm, melancholy music",
        "intense, aggressive, epic battle music",
        "happy, bright, cheerful music",
        "funny, comedic, playful music"]),
}
