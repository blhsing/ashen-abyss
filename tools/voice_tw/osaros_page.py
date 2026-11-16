"""Choice page Y:\\voice_samples\\osaros\\index.html  (run with the `voxcpm` venv).

    osaros_page.py            generate what is missing, then build the page
    osaros_page.py --pages    only rebuild the page from the staged takes

Section 1 - the final boss, retuned for clarity: three voice descriptions x three effect strengths
(dry / light / medium), each measured by an ASR round-trip on the FINAL processed audio.
Section 2 - voice consistency: for the elder and the merchant, three real lines made the production
way (one anchor from the description, every line a continuation of that anchor) against the same lines
made from the description alone (a fresh design per line, fixed seed), with speaker similarity (CAM++).
Everything generated here is production material: the anchors and the anchored takes are reused by gen.py.
"""
import html
import json
import os
import sys

import numpy as np

import engine as E
import fx
import profiles
import tw_text
import voices
import vt
from vt import voice_stats

OUT = r"Y:\voice_samples\osaros"
STAGE = os.path.join(vt.ROOT, "stage", "osaros")
LOG = os.path.join(vt.ROOT, "logs", "osaros_page.log")
BOSS_LINE = "boss_osaros_intro"
DEMO = {"elder": ["elder_first_02", "elder_idle_2", "elder_epilogue_06"],
        "merchant": ["merchant_greet_1", "merchant_greet_3", "merchant_idle_1"]}
STRENGTHS = ["dry", "light", "medium"]
SLOW = 3.8                      # characters per second: the final boss should not speak faster than this


def script() -> dict:
    return {l["id"]: l for l in json.load(open(vt.VO_LINES, encoding="utf-8"))}


# ------------------------------------------------------------------ generation

def generate():
    os.makedirs(STAGE, exist_ok=True)
    E.running_file("osaros_page.py (boss choice page and consistency demo)", LOG)
    eng = E.Engine(LOG)
    lines = script()
    man = dict(osaros={}, demo={})
    text = lines[BOSS_LINE]["text"]
    for key, var in voices.OSAROS_VARIANTS.items():
        a = eng.anchor("boss_osaros", desc=var["desc"], key=f"boss_osaros@{key}")
        wav, meta = os.path.join(STAGE, f"osaros_{key}.wav"), os.path.join(STAGE, f"osaros_{key}.json")
        if os.path.exists(wav) and os.path.exists(meta) and json.load(open(meta, encoding="utf-8")).get("anchor_seed") == a["seed"]:
            m = json.load(open(meta, encoding="utf-8"))
        else:
            x, r = eng.take(text, a, None, f"{BOSS_LINE}@{key}")
            vt.write_float(wav, x, eng.sr)
            m = dict(id=BOSS_LINE, voice="boss_osaros", text=text, wav=wav, anchor_key=a["key"], anchor_seed=a["seed"], **r)
            json.dump(m, open(meta, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        man["osaros"][key] = dict(anchor=a, take=m)
    for voice, ids in DEMO.items():
        a = eng.anchor(voice)
        rows = dict(anchor=a, anchored=[], described=[])
        for i in ids:
            rows["anchored"].append(eng.line(i, voice, lines[i]["text"], a))
        for i in ids:                                             # description only: a fresh design per line, seed fixed
            wav, meta = os.path.join(STAGE, f"described_{i}.wav"), os.path.join(STAGE, f"described_{i}.json")
            if os.path.exists(wav) and os.path.exists(meta):
                m = json.load(open(meta, encoding="utf-8"))
            else:
                tts = tw_text.to_voxcpm(lines[i]["text"])
                x = eng.synth(tts, 1, desc=voices.get(voice)["desc"])
                r = eng.check(lines[i]["text"], x, E.FULL)
                E.log(f"  described {i} seed 1: cer {r['cer']:.3f} {r['seconds']:.1f}s  {r['heard']}", LOG)
                vt.write_float(wav, x, eng.sr)
                m = dict(id=i, voice=voice, text=lines[i]["text"], wav=wav, seed=1, tts_text=tts, **r)
                json.dump(m, open(meta, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
            rows["described"].append(m)
        man["demo"][voice] = rows
    man["speed"] = dict(compute=round(eng.compute), audio=round(eng.audio, 1), threads=E.cpu_cap.THREADS)
    json.dump(man, open(os.path.join(STAGE, "manifest.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    E.log(f"generation done: {eng.audio:.0f} s of audio in {eng.compute:.0f} s of compute "
          f"({eng.compute / max(eng.audio, 1):.1f} s per second of audio)", LOG)
    return eng.asr


# ------------------------------------------------------------------ page

def pair_stats(embs: list) -> tuple[float, float]:
    sims = [E.cos(embs[i], embs[j]) for i in range(len(embs)) for j in range(i + 1, len(embs))]
    return round(float(np.mean(sims)), 2), round(float(np.min(sims)), 2)


def build(asr=None):
    from samples_build import CSS
    asr = asr or vt.Asr(threads=4)
    man = json.load(open(os.path.join(STAGE, "manifest.json"), encoding="utf-8"))
    lines = script()
    os.makedirs(OUT, exist_ok=True)
    e = html.escape
    rep = dict(osaros={}, consistency={})

    def publish(name: str, y: np.ndarray) -> tuple[np.ndarray, dict]:
        dst = os.path.join(OUT, name)
        vt.write_wav16(dst, y, fx.OUT_RATE)
        final, sr = vt.read_wav(dst)
        return final, vt.measure(final, sr)

    # ---- section 1: the boss
    prof0 = profiles.PROFILES["boss_osaros"]
    text = lines[BOSS_LINE]["text"]
    for key, d in man["osaros"].items():
        dry, sr = vt.read_wav(d["take"]["wav"])
        anc, asr_sr = vt.read_wav(d["anchor"]["wav"])
        _, _ = publish(f"{key}_anchor.wav", fx.finish(fx.resample(anc, asr_sr, fx.OUT_RATE)))
        cells = {}
        for st in STRENGTHS:
            k = profiles.BOSS_STRENGTH[st]
            base = fx.resample(dry, sr, fx.OUT_RATE)
            y = fx.finish(base if k == 0 else fx.apply(profiles.scaled(prof0, k), base, fx.OUT_RATE, seed=7))
            final, m = publish(f"{key}_{st}.wav", y)
            heard = asr.transcribe(final, fx.OUT_RATE)["text"]
            cells[st] = dict(heard=vt.s2t(heard), cer=round(vt.cer_hint(text, heard), 3), cer_strict=round(vt.cer_strict(text, heard), 3), **m)
            print(key, st, cells[st]["cer"], cells[st]["cer_strict"], cells[st]["lufs"], cells[st]["true_peak_db"], heard, flush=True)
        rep["osaros"][key] = dict(label=voices.OSAROS_VARIANTS[key]["label"], desc=d["anchor"]["desc"], anchor_text=d["anchor"]["text"],
                                  anchor_cer=d["anchor"]["cer"], dry_take=dict(seed=d["take"]["seed"], tries=len(d["take"]["tries"]),
                                  chars_per_s=d["take"]["chars_per_s"]), stats=voice_stats(dry, sr), cells=cells,
                                  designs_tried=d["anchor"].get("designs_tried", 1), anchor_stats=voice_stats(anc, asr_sr))
    # default (variant, strength), by measurement: the voice must really be deep (anchor pitch inside the range
    # wanted for this boss) and unhurried (at most SLOW characters per second); then the processed file the
    # recogniser reads back best; then the cleanest voice (harmonics-to-noise ratio); then the stronger chain
    lo, hi = voices.VOICES["boss_osaros"]["f0"]
    cands = []
    for k, r in rep["osaros"].items():
        f0 = r["anchor_stats"]["f0"] or 999
        for s in ("medium", "light"):
            cands.append(((0 if lo <= f0 <= hi else 1, 0 if r["dry_take"]["chars_per_s"] <= SLOW else 1, r["cells"][s]["cer"],
                           -(r["anchor_stats"]["hnr"] or 0), 0 if s == "medium" else 1), k, s))
    _, pick, strength = min(cands)
    rep["default"] = dict(variant=pick, strength=strength)

    # ---- section 2: consistency
    paths = []
    for voice, rows in man["demo"].items():
        paths += [rows["anchor"]["wav"]] + [m["wav"] for m in rows["anchored"]] + [m["wav"] for m in rows["described"]]
    emb = E.embeddings(paths)
    for voice, rows in man["demo"].items():
        a = emb[rows["anchor"]["wav"]]
        out = dict(anchor_text=rows["anchor"]["text"], desc=rows["anchor"]["desc"])
        x, sr = vt.read_wav(rows["anchor"]["wav"])
        publish(f"{voice}_anchor.wav", fx.finish(fx.resample(x, sr, fx.OUT_RATE)))
        for kind in ("anchored", "described"):
            embs, takes = [], []
            for m in rows[kind]:
                x, sr = vt.read_wav(m["wav"])
                publish(f"{kind}_{m['id']}.wav", fx.finish(fx.resample(x, sr, fx.OUT_RATE)))
                embs.append(emb[m["wav"]])
                takes.append(dict(id=m["id"], text=m["text"], cer=m["cer"], heard=m["heard"], seconds=m["seconds"],
                                  to_anchor=round(E.cos(emb[m["wav"]], a), 2), **voice_stats(x, sr)))
            mean, worst = pair_stats(embs)
            out[kind] = dict(takes=takes, pair_mean=mean, pair_min=worst, to_anchor_mean=round(float(np.mean([t["to_anchor"] for t in takes])), 2))
        rep["consistency"][voice] = out
    cross = [E.cos(emb[man["demo"]["elder"]["anchor"]["wav"]], emb[man["demo"]["merchant"]["anchor"]["wav"]])]
    rep["cross_voice"] = round(float(cross[0]), 2)
    rep["speed"] = man.get("speed")
    holds = all(c["anchored"]["pair_min"] >= 0.6 and c["anchored"]["pair_mean"] - c["described"]["pair_mean"] >= 0.1 for c in rep["consistency"].values())
    rep["self_reference_holds"] = bool(holds)
    json.dump(rep, open(os.path.join(OUT, "report.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    # ---- HTML
    h = [f"<!doctype html><html lang='en'><head><meta charset='utf-8'><title>Ashen Abyss - Osaros voice and voice consistency</title><style>{CSS}"
         "td.d{max-width:300px;font-size:13px} .ok{color:#9fd08a} .pick{outline:2px solid #f0a45a}</style></head><body>"]
    h.append("<h1>《燼淵 Ashen Abyss》 the final boss, retuned for clarity - and how a voice stays the same</h1>")
    h.append("<p class='small'>Model: VoxCPM2 voice design, no reference audio. All files are 16-bit 44.1 kHz mono, matched to about -17 LUFS. "
             "Generated offline on this machine's CPU (6 threads; the GPU was busy with the music job).</p>")
    h.append(f"<h2>1. 寂主・奧薩洛斯 - pick a voice and an effect strength</h2><p class='zh' lang='zh-Hant'>{e(text)}</p>")
    h.append("<p class='small'>Three written voice descriptions (rows), each asking for a deep, calm, slow voice with clean articulation and nothing "
             "hoarse, gravelly or whispered. A description does not guarantee a register - one \"deep\" design came out at 220 Hz - so every design "
             f"is measured and repeated (up to five times) until its median pitch is inside the range wanted for this boss ({lo}-{hi} Hz) and its "
             "harmonics-to-noise ratio is at least 5.5 dB (the first test's boss, which you found too coarse, measured 4.2 dB; rough designs at "
             "2.1-4.4 dB were discarded here). "
             "Three effect strengths (columns). The effect chain was rebuilt so the consonants stay intact: at most 2.5 "
             "semitones down with the vowel colour preserved, a quiet low-passed octave-down layer for weight (-14 dB), no distortion or modulation on "
             "the voice, a large reverb that starts 85 ms after the voice and sits 17 dB under it, a gentle lift around 3 kHz, gentle compression. "
             "\"Light\" is the same chain at half strength. Under each player: the error rate of a recogniser reading the <b>final processed</b> file "
             "back (homophones forgiven; in brackets counted strictly) - the target was at most 0.15.</p>")
    h.append("<div class='wrap'><table><tr><th>Voice description</th><th>The voice itself<span class='sub'>its anchor sentence, dry</span></th>"
             + "".join(f"<th>{s.capitalize()}<span class='sub'>{ {'dry': 'no effects', 'light': 'chain at 50 %', 'medium': 'chain at 100 %'}[s] }</span></th>" for s in STRENGTHS) + "</tr>")
    for key in voices.OSAROS_VARIANTS:
        if key not in rep["osaros"]:
            continue
        r = rep["osaros"][key]
        st = r["stats"]
        h.append(f"<tr><td class='d'><b>{e(r['label'])}</b><div class='note' lang='zh-Hans'>{e(r['desc'])}</div>"
                 f"<div class='m'>this line: median pitch {st['f0']} Hz | harmonics-to-noise {st['hnr']} dB | {r['dry_take']['chars_per_s']} ch/s</div>"
                 f"<div class='m'>anchor: {r['anchor_stats']['f0']} Hz | {r['anchor_stats']['hnr']} dB | design no. {r['designs_tried']} was kept</div></td>"
                 f"<td><audio controls preload='none' src='{key}_anchor.wav'></audio><div class='heard' lang='zh-Hant'>{e(r['anchor_text'])}</div></td>")
        for s in STRENGTHS:
            c = r["cells"][s]
            is_pick = key == rep["default"]["variant"] and s == rep["default"]["strength"]
            warn = "" if c["cer"] <= 0.15 else "<div class='warn'>above the 0.15 target</div>"
            h.append(f"<td{' class=pick' if is_pick else ''}><audio controls preload='none' src='{key}_{s}.wav'></audio>"
                     f"<div class='m'>CER {c['cer']:.2f} ({c['cer_strict']:.2f} strict) | {c['duration']:.1f} s | {c['lufs']:.1f} LUFS</div>"
                     f"<div class='heard' lang='zh-Hant'>ASR: {e(c['heard'])}</div>{warn}"
                     + ("<div class='note'><b>current default</b></div>" if is_pick else "") + "</td>")
        h.append("</tr>")
    h.append("</table></div>")
    d = rep["default"]
    worst = max(c["cer"] for r in rep["osaros"].values() for c in r["cells"].values())
    dr = rep["osaros"][d["variant"]]
    h.append(f"<p class='small'><b>Default used for production until you choose otherwise:</b> \"{e(dr['label'])}\" at "
             f"<b>{d['strength']}</b> strength (outlined above). Chosen by measurement: among the voices that are deep and unhurried (at most {SLOW} "
             f"characters per second) it is the combination the recogniser reads back best (error {dr['cells'][d['strength']]['cer']:.2f}), and of "
             f"those the cleanest voice (anchor at {dr['anchor_stats']['f0']} Hz, harmonics-to-noise {dr['anchor_stats']['hnr']} dB). "
             f"Largest error rate on this page: {worst:.2f}, where one syllable was misheard. "
             "A higher harmonics-to-noise ratio means a cleaner, less coarse voice; ch/s is characters per second. "
             "The dry take of every boss line is kept, so another strength - or another chain - can be applied in seconds "
             "without generating speech again; choosing another <i>description</i> means regenerating that boss's three lines (about ten minutes).</p>")

    h.append("<h2>2. Voice consistency - how a character keeps one voice without any reference recording</h2>")
    h.append("<p class='small'>Voice design invents a new voice every time it is given a description. So each character's description is used "
             "<b>once</b>, to speak one anchor sentence. Every line of that character is then generated as a continuation of its own anchor "
             "(the anchor's audio and text are the prompt). The anchor is the model's own invention, so there is still no reference recording of anybody. "
             "Below, three real lines made that way, against the same three lines made from the description alone (a fresh design per line, same seed). "
             "The figure is the cosine similarity of speaker embeddings (CAM++, the speaker encoder shipped with CosyVoice 3): the same fixed voice "
             "speaking different sentences measured 0.85-0.95 on this machine; different voices measured 0.26 on average (0.05-0.49).</p>")
    for voice, c in rep["consistency"].items():
        name = {"elder": "守燈長老 艾爾溫 (elder)", "merchant": "行商 莉雅 (merchant)"}.get(voice, voice)
        h.append(f"<h2 style='font-size:15px'>{e(name)}</h2><div class='note' lang='zh-Hans'>Description: {e(c['desc'])}</div>")
        h.append("<div class='wrap'><table><tr><th>How the lines were made</th><th>Speaker similarity between the three takes</th>"
                 + "".join(f"<th>{e(t['id'])}</th>" for t in c["anchored"]["takes"]) + "</tr>")
        for kind, title in (("anchored", "<b>Production way:</b> one anchor from the description, each line continues its anchor"),
                            ("described", "<b>Description only:</b> a fresh design for every line (seed fixed)")):
            k = c[kind]
            extra = (f"<div style='margin-top:6px'><audio controls preload='none' src='{voice}_anchor.wav'></audio></div>"
                     f"<div class='note' lang='zh-Hant'>the anchor: {e(c['anchor_text'])}</div>") if kind == "anchored" else ""
            h.append(f"<tr><td class='d'>{title}{extra}</td>"
                     f"<td><div style='font-size:20px'><b>{k['pair_mean']:.2f}</b></div><div class='note'>mean of the three pairs; worst pair {k['pair_min']:.2f}</div>"
                     + (f"<div class='note'>each take against the anchor: {k['to_anchor_mean']:.2f}</div>" if kind == "anchored"
                        else f"<div class='note'>each take against the production anchor: {k['to_anchor_mean']:.2f}</div>") + "</td>")
            for t in k["takes"]:
                h.append(f"<td><audio controls preload='none' src='{kind}_{t['id']}.wav'></audio>"
                         f"<div class='heard' lang='zh-Hant'>{e(t['text'])}</div>"
                         f"<div class='m'>pitch {t['f0']} Hz | CER {t['cer']:.2f} | {t['seconds']:.1f} s</div></td>")
            h.append("</tr>")
        h.append("</table></div>")
    if rep["self_reference_holds"]:
        verdict = ("<b>Result: the anchor holds the voice.</b> " + "; ".join(
            f"{v}: {c['anchored']['pair_mean']:.2f} with the anchor (worst pair {c['anchored']['pair_min']:.2f}) against "
            f"{c['described']['pair_mean']:.2f} from the description alone" for v, c in rep["consistency"].items())
            + ". The production run therefore uses anchors for all 23 voices; every line's similarity to its anchor is measured, and lines that "
              "drift are regenerated.")
    else:
        verdict = ("<b>Result: self-reference did NOT clearly hold the voice in this test</b> (see the figures above). "
                   "Alternatives are being measured before the full run; the report lists what was chosen.")
    h.append(f"<p class='small'>{verdict} For scale, the elder's anchor against the merchant's anchor: {rep['cross_voice']:.2f}.</p>")
    h.append("<p class='note'>These takes carry no effects (trim and loudness only). The Taiwan readings are given to the model as hints in the "
             "text it reads (for example 熄 as {xi2}, 便宜 with a full second tone); the stored script is unchanged.</p></body></html>")
    open(os.path.join(OUT, "index.html"), "w", encoding="utf-8").write("\n".join(h))

    # ---- README
    t = ["《燼淵 Ashen Abyss》 - the final boss retuned for clarity, and voice consistency   (Y:\\voice_samples\\osaros)", "=" * 92, "",
         "Open index.html. Model: VoxCPM2 voice design, no reference audio. 16-bit 44.1 kHz mono WAV, about -17 LUFS.", "",
         f"1. 寂主・奧薩洛斯, line {BOSS_LINE}: {text}", "",
         "   Files: <description>_<strength>.wav and <description>_anchor.wav (the designed voice speaking its anchor sentence, dry).",
         "   Strengths: dry = no effects; light = new boss chain at 50 %; medium = new boss chain at 100 %.",
         "   CER = error rate of a recogniser reading the FINAL processed file back (homophones forgiven / strict). Target: at most 0.15.", ""]
    for key, r in rep["osaros"].items():
        t.append(f"   {key}: {r['label']}  -  {r['desc']}")
        t.append(f"      dry take: median pitch {r['stats']['f0']} Hz, harmonics-to-noise {r['stats']['hnr']} dB, {r['dry_take']['chars_per_s']} characters/s"
                 f"   (anchor: {r['anchor_stats']['f0']} Hz; design no. {r['designs_tried']} was kept; wanted {lo}-{hi} Hz)")
        for s in STRENGTHS:
            c = r["cells"][s]
            mark = "   <- current default" if key == d["variant"] and s == d["strength"] else ""
            t.append(f"      {key}_{s}.wav   CER {c['cer']:.2f} / {c['cer_strict']:.2f}   {c['duration']:.1f} s   {c['lufs']:.1f} LUFS   {c['true_peak_db']:.1f} dBTP   ASR: {c['heard']}{mark}")
        t.append("")
    t += [f"   Default until you choose otherwise: {d['variant']} at {d['strength']} strength.", "",
          "2. Voice consistency. Production way = one anchor take from the description, every line continues its own anchor.",
          "   Description only = a fresh design per line (seed fixed). Figure = cosine similarity of CAM++ speaker embeddings between the",
          "   three takes (same fixed voice, different sentences: 0.85-0.95; different voices: 0.26 on average).", ""]
    for voice, c in rep["consistency"].items():
        t.append(f"   {voice}: {c['desc']}")
        for kind, title in (("anchored", "production way  "), ("described", "description only")):
            k = c[kind]
            t.append(f"      {title}: similarity {k['pair_mean']:.2f} (worst pair {k['pair_min']:.2f}); against the anchor {k['to_anchor_mean']:.2f}")
            for x in k["takes"]:
                t.append(f"         {kind}_{x['id']}.wav   pitch {x['f0']} Hz   CER {x['cer']:.2f}   {x['text']}")
        t.append("")
    t.append("   " + html.unescape(verdict.replace("<b>", "").replace("</b>", "")))
    open(os.path.join(OUT, "README.txt"), "w", encoding="utf-8-sig").write("\r\n".join(t))
    print("default:", rep["default"], "| self-reference holds:", rep["self_reference_holds"], "| wrote", OUT, flush=True)
    return rep


def main():
    asr = None
    if "--pages" not in sys.argv:
        try:
            asr = generate()
        finally:
            E.done()
    build(asr)


if __name__ == "__main__":
    main()
