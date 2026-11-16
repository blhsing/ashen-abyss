"""Confirmation page Y:\\voice_samples\\elder\\index.html  (run with the `voxcpm` venv).

    elder_page.py            make what is missing (new elder anchor + his first three lines, the final boss's lines
                             in the chosen voice), audit, process, deliver those lines, build the page
    elder_page.py --pages    only rebuild the page

The first elder was designed at 172 Hz and was heard as an elderly woman. The new one is selected by
measurement: several designs, median pitch 95-140 Hz on the anchor and on three lines, and the male / female
classifier of gender.py. The page plays the new anchor, two of his real lines, the rejected old take for
comparison, and one line of 寂主・奧薩洛斯 in the voice and strength the user chose (low_soft, medium).
"""
import html
import json
import os
import sys

import numpy as np

import engine as E
import fx
import gen
import profiles
import voices
import vt

OUT = r"Y:\voice_samples\elder"
OLD = os.path.join(vt.ROOT, "stage", "elder")
LOG = gen.LOG
ELDER_SHOW = ["elder_first_02", "elder_first_01"]
BOSS_SHOW = "boss_osaros_phase"


def generate():
    lines = gen.script()
    E.running_file("elder_page.py (new elder voice, final boss in the chosen voice)", LOG)
    eng = E.Engine(LOG)
    try:
        el = [l for l in lines if l["voice"] == "elder"]
        gen.designed_voice(eng, "elder", el)                        # anchor (several designs) + three lines, audited
        bl = [l for l in lines if l["voice"] == "boss_osaros"]
        gen.designed_voice(eng, "boss_osaros", bl)
        gen.do_lines(eng, bl)
        gen.do_fx(eng.asr, el[:3] + bl)
        gen.retake_locked(eng, eng.asr, bl)
        gen.do_deliver(el[:3] + bl)
    finally:
        E.done()
    return eng.asr


def facts(wav: str) -> dict:
    x, sr = vt.read_wav(wav)
    st = vt.voice_stats(x, sr)
    return dict(f0=st["f0"], hnr=st["hnr"], p_male=E.sex_score(wav), seconds=round(len(x) / sr, 1))


def build(asr=None):
    from samples_build import CSS
    asr = asr or vt.Asr(threads=4)
    lines = {l["id"]: l for l in gen.script()}
    os.makedirs(OUT, exist_ok=True)
    e = html.escape
    au = (gen.load_json(gen.AUDIT, {}) or {})
    a = gen.load_json(os.path.join(E.ANCHORS, "elder.json"))
    rows = []

    def add(name, label, wav, text, note="", processed=None):
        x, sr = vt.read_wav(wav)
        y = processed if processed is not None else fx.finish(fx.resample(x, sr, fx.OUT_RATE))
        vt.write_wav16(os.path.join(OUT, name), y, fx.OUT_RATE)
        heard = asr.transcribe(y, fx.OUT_RATE)["text"]
        rows.append(dict(file=name, label=label, text=text, note=note, heard=vt.s2t(heard), cer=round(vt.cer_hint(text, heard), 3), **facts(wav)))

    add("elder_anchor_new.wav", "NEW elder - the anchor (the designed voice itself)", a["wav"], a["text"],
        f"design no. {a.get('design_kept')} of {a.get('designs_tried')} tried")
    for i in ELDER_SHOW:
        add(f"{i}_new.wav", f"NEW elder - {i}", os.path.join(E.DRY, i + ".wav"), lines[i]["text"], "a continuation of the new anchor")
    old_meta = gen.load_json(os.path.join(OLD, "old_elder_first_02.json")) or {}
    add("elder_first_02_old_rejected.wav", "REJECTED old elder - elder_first_02", os.path.join(OLD, "old_elder_first_02.wav"),
        lines["elder_first_02"]["text"], "the take you heard as an elderly woman")
    add("elder_anchor_old_rejected.wav", "REJECTED old elder - the anchor", os.path.join(OLD, "old_elder_anchor.wav"),
        (gen.load_json(os.path.join(OLD, "old_elder_anchor.json")) or {}).get("text", ""), "")
    # the final boss, chosen voice and strength, as delivered (dry take -> medium chain)
    bw = os.path.join(E.DRY, BOSS_SHOW + ".wav")
    x, sr = vt.read_wav(bw)
    strength = gen.boss_strength("boss_osaros")
    proc = fx.finish(fx.apply(profiles.scaled(profiles.PROFILES["boss_osaros"], profiles.BOSS_STRENGTH[strength]), fx.resample(x, sr, fx.OUT_RATE), fx.OUT_RATE, seed=7))
    add(f"{BOSS_SHOW}_low_soft_{strength}.wav", f"寂主・奧薩洛斯 - {BOSS_SHOW}, \"low and soft-spoken\" voice, {strength} effect chain",
        bw, lines[BOSS_SHOW]["text"], "pitch and classifier are measured on the dry take; the player is the processed file", processed=proc)

    r = au.get("elder", {})
    lo, hi = voices.VOICES["elder"]["f0"]
    rep = dict(rows=rows, audit=r, description=a["desc"], classifier=(gen.load_json(os.path.join(E.HERE, "gender_model.json"), {}) or {}).get("info"))
    json.dump(rep, open(os.path.join(OUT, "report.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

    h = [f"<!doctype html><html lang='en'><head><meta charset='utf-8'><title>Ashen Abyss - the elder, redesigned</title><style>{CSS}"
         "td.d{max-width:330px;font-size:13px} tr.old td{opacity:.75}</style></head><body>"]
    h.append("<h1>《燼淵 Ashen Abyss》 守燈長老 艾爾溫 - redesigned as an old MAN, selected by measurement</h1>")
    h.append("<p class='small'>The first elder voice was designed at a median pitch of 172 Hz and you heard it as an elderly woman. "
             f"The new one was chosen from {a.get('designs_tried')} designs of the description below. A design had to: read its anchor sentence back exactly, "
             f"have a median pitch of {lo}-{hi} Hz, and score as male on a male / female classifier; then three of his real lines, generated from "
             "that anchor, had to pass the same two measurements (lines may sit 10 % above the range, i.e. at most 154 Hz). "
             "Files are 16-bit 44.1 kHz mono, about -17 LUFS; the elder takes carry no effects.</p>")
    h.append(f"<p class='note' lang='zh-Hans'>Description: {e(a['desc'])}</p>")
    h.append("<div class='wrap'><table><tr><th>What</th><th>Listen</th><th>Median pitch</th><th>p(male)</th><th>Harmonics-to-noise</th><th>Reads back</th></tr>")
    for x in rows:
        old = "REJECTED" in x["label"]
        h.append(f"<tr{' class=old' if old else ''}><td class='d'><b>{e(x['label'])}</b><div class='zh' lang='zh-Hant' style='font-size:14px'>{e(x['text'])}</div>"
                 f"<div class='note'>{e(x['note'])}</div></td><td><audio controls preload='none' src='{x['file']}'></audio></td>"
                 f"<td style='font-size:18px'>{x['f0']} Hz</td><td style='font-size:18px'>{x['p_male']}</td><td>{x['hnr']} dB</td>"
                 f"<td class='m'>CER {x['cer']:.2f}<div class='heard' lang='zh-Hant'>{e(x['heard'])}</div></td></tr>")
    h.append("</table></div>")
    g = rep["classifier"] or {}
    h.append("<p class='small'><b>How to read the figures.</b> Median pitch: the 45 male Kokoro voices measure 105-203 Hz (median 136), the 55 female ones "
             f"142-324 Hz (median 242) with the same code. p(male): a classifier on speaker embeddings (CAM++), calibrated on those 100 labelled "
             f"voices ({g.get('cross_validation_accuracy', '?')} cross-validated accuracy there). On VoxCPM2 voices its scores bunch towards the middle: voices "
             f"heard as women score 0.28-0.42, clear men 0.68-0.87, so a man must reach {E.SEX_M_MIN} and a woman stay under {E.SEX_F_MAX}. "
             "The final boss's \"low and soft-spoken\" voice is the one you chose by ear; it passes the pitch gate and sits between the two groups on the "
             "classifier, so it is kept on your decision and marked as such in the audit.</p>")
    if r:
        h.append(f"<p class='small'><b>Audit of the new elder:</b> anchor {r.get('anchor_f0')} Hz, his first three lines {r.get('lines_f0')} Hz "
                 f"(wanted {lo}-{hi}), p(male) {r.get('anchor_p_male')} on the anchor and {r.get('lines_p_male')} on the lines: "
                 f"<b>{'pass' if r.get('passed') else 'NOT passed - see the action in the audit table'}</b>. "
                 "All 66 of his lines are being regenerated from this anchor; the same audit runs on every other character "
                 "(table in <code>Y:\\tools\\voice_tw\\DECISION.md</code>).</p>")
    h.append("</body></html>")
    open(os.path.join(OUT, "index.html"), "w", encoding="utf-8").write("\n".join(h))
    t = ["《燼淵 Ashen Abyss》 - 守燈長老 艾爾溫 redesigned as an old man (Y:\\voice_samples\\elder)", "=" * 80, "",
         "Open index.html. 16-bit 44.1 kHz mono WAV, about -17 LUFS.", f"Description: {a['desc']}", ""]
    for x in rows:
        t.append(f"{x['file']}")
        t.append(f"    {x['label']}: median pitch {x['f0']} Hz, p(male) {x['p_male']}, harmonics-to-noise {x['hnr']} dB, CER {x['cer']:.2f}")
        t.append(f"    {x['text']}")
    if r:
        t += ["", f"Audit of the new elder: anchor {r.get('anchor_f0')} Hz, first three lines {r.get('lines_f0')} Hz (wanted {lo}-{hi}), "
                  f"p(male) {r.get('anchor_p_male')} / {r.get('lines_p_male')}: {'pass' if r.get('passed') else 'NOT passed'}"]
    open(os.path.join(OUT, "README.txt"), "w", encoding="utf-8-sig").write("\r\n".join(t))
    for x in rows:
        print(x["file"], x["f0"], "Hz p(male)", x["p_male"], "hnr", x["hnr"], "cer", x["cer"], flush=True)
    print("wrote", OUT, flush=True)


def main():
    asr = None
    if "--pages" not in sys.argv:
        asr = generate()
    build(asr)


if __name__ == "__main__":
    main()
