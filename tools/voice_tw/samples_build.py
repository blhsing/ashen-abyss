"""Step 3 of the listening test: finish, measure and publish.

    <venvs\\common python> samples_build.py            process every staged column, then write the pages
    <venvs\\common python> samples_build.py --pages    only rewrite index.html / README.txt from report.json

For every candidate column under %VT_ROOT%\\stage\\samples: human lines are only resampled to
44.1 kHz, trimmed and loudness-matched (-17 LUFS) - NO effects, so the models can be compared;
the boss line is published dry (e) and with the boss effect chain (f). Each dry take is transcribed
by SenseVoice and compared with the script. Output: Y:\\voice_samples\\<column>\\<key>.wav (16-bit mono
44.1 kHz), index.html, README.txt, report.json.
"""
import html
import json
import os
import statistics
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import accent_probe
import fx
import profiles
import samples_spec as S
import vt

TWB = ("the example prompt bundled with the official BreezyVoice repository (data/example.wav, first sentence, a Taiwanese "
       "female speaker, 16 kHz), pitch/formant-shifted and slowed per role with our own DSP (narrator -5.5, elder -4.6, "
       "merchant +0.8, hero -3.8, boss -6.5 semitones). All five roles are therefore variants of ONE speaker, and the male "
       "roles are a female voice shifted down")

# column id -> facts shown on the page. "order" fixes the column order.
MODELS = {
    "kokoro": dict(
        order=0, name="Kokoro v1.1-zh", sub="current fallback, baseline",
        size="82 M parameters, about 0.35 GB, 24 kHz", licence="Apache-2.0", url="https://huggingface.co/hexgrad/Kokoro-82M-v1.1-zh",
        accent="None. Mainland Mandarin pronunciation and readings - this is what the other columns should improve on.",
        voice="Kokoro's built-in speakers: narrator 63, elder 93, merchant 44, hero 70, boss 68."),
    "breezyvoice": dict(
        order=1, name="BreezyVoice", sub="synthetic timbre (Kokoro clip)",
        size="about 300 M parameters (CosyVoice-300M fine-tuned on Taiwanese Mandarin), 2.3 GB download, 22 kHz",
        licence="Apache-2.0 (code and weights)", url="https://github.com/mtkresearch/BreezyVoice",
        accent="MediaTek Research trained this model on Taiwanese Mandarin. Here its language model gets NO audio prompt, only "
               "the text, so readings, tones and rhythm come from the model itself; Taiwan readings are also forced with its "
               "bopomofo syntax (寂[:ㄐㄧ2] 血[:ㄒㄧㄝ3] 熄[:ㄒㄧ2] 息[:ㄒㄧ2]). The timbre, and with it the colour of the "
               "consonants, is taken from a synthetic Kokoro clip.",
        voice="Timbre reference: clips we synthesised ourselves with Kokoro (no real person) - the same five speakers as the Kokoro column."),
    "breezyvoice_tw": dict(
        order=2, name="BreezyVoice", sub="official zero-shot, bundled Taiwanese prompt",
        size="as above", licence="Apache-2.0", url="https://github.com/mtkresearch/BreezyVoice",
        accent="The model's official mode: the language model continues from the reference clip, so accent AND timbre follow "
               "the clip. The clip is the Taiwanese example that ships with the repository, so this is BreezyVoice as its "
               "authors demonstrate it.",
        voice="Reference: " + TWB + ". The clip is only 16 kHz, so this column sounds duller."),
    "breezyvoice_tw2": dict(
        order=3, name="BreezyVoice", sub="no continuation, bundled Taiwanese timbre",
        size="as above", licence="Apache-2.0", url="https://github.com/mtkresearch/BreezyVoice",
        accent="A mix of the two columns before it: the language model is not prompted with audio (readings follow the text and "
               "the bopomofo hints strictly), the timbre and consonant colour come from the bundled Taiwanese example.",
        voice="Timbre reference: " + TWB + "."),
    "cosyvoice3": dict(
        order=4, name="Fun-CosyVoice3-0.5B", sub="zero-shot, bundled Taiwanese prompt",
        size="0.5 B parameters, 4.4 GB download, 24 kHz", licence="Apache-2.0 (code and weights)", url="https://github.com/FunAudioLLM/CosyVoice",
        accent="CosyVoice 3 has no Taiwanese-Mandarin instruction (its dialect list only has 閩南話); in zero-shot mode the accent "
               "follows the reference clip, so it gets the same Taiwanese clip as the BreezyVoice columns. Taiwan readings are "
               "forced with its pinyin tokens (寂 -> [j][í], 血 -> [x][iě], 熄/息 -> [x][í]).",
        voice="Reference: " + TWB + "."),
    "voxcpm2_design": dict(
        order=5, name="VoxCPM2", sub="voice design, no reference audio",
        size="2 B parameters, 5.0 GB download, 48 kHz", licence="Apache-2.0 (code and weights)", url="https://github.com/OpenBMB/VoxCPM",
        accent="No reference audio at all: each voice is created from a written description that asks for 台灣口音的國語 "
               "(for example the narrator: 中年男性說書人，台灣口音的國語，聲音低沉渾厚，語速緩慢…). Taiwan readings are "
               "forced with pinyin in braces (寂 -> {ji2}, 血 -> {xie3}, 熄/息 -> {xi2}); without them VoxCPM2 reads 寂 the mainland way "
               "even when the description asks for a Taiwanese accent (measured).",
        voice="Designed voices, one text description per role. Nobody's voice is cloned."),
    "voxcpm2_clone": dict(
        order=6, name="VoxCPM2", sub="continuation, bundled Taiwanese prompt",
        size="as above", licence="Apache-2.0", url="https://github.com/OpenBMB/VoxCPM",
        accent="Continues from a reference clip plus its transcript, so accent and timbre follow the clip - the same Taiwanese "
               "clip as the other reference-driven columns. Readings forced with {pinyin}, as in the design column.",
        voice="Reference: " + TWB + "."),
}


def load_timing(column: str) -> dict:
    p = os.path.join(S.STAGE, column, "timing.json")
    if not os.path.exists(p):
        return {}
    d = json.load(open(p, encoding="utf-8"))
    rows = d["rows"] if isinstance(d, dict) else d
    return {os.path.splitext(os.path.basename(r["out"]))[0]: r for r in rows if "out" in r}


def process(column: str, asr: vt.Asr, texts: dict) -> dict:
    res = {}
    timing = load_timing(column)
    for s in S.SAMPLES:
        key = s["key"]
        src_key = s.get("same_as", key)
        src = os.path.join(S.STAGE, column, src_key + ".wav")
        if not os.path.exists(src):
            continue
        dry, sr = vt.read_wav(src)
        text = texts[src_key]
        n_han = len(vt.for_compare(text))
        strength = None
        if s["fx"]:
            # boss chain; if the recogniser can no longer follow it (sound CER > 0.5), pull the chain back
            _, prof0 = profiles.resolve(s["fx"])
            base = fx.resample(dry, sr, fx.OUT_RATE)
            for strength in (1.0, 0.7, 0.45):
                prof = profiles.scaled(prof0, strength)
                y = fx.finish(fx.apply(prof, fx.speed(base, prof.get("rate", 1.0), fx.OUT_RATE), fx.OUT_RATE, seed=7), fx.TARGET_LUFS)
                heard = asr.transcribe(y, fx.OUT_RATE)
                if vt.cer_sound(text, heard["text"]) <= 0.5:
                    break
        else:
            y = fx.finish(fx.resample(dry, sr, fx.OUT_RATE), fx.TARGET_LUFS)
            heard = asr.transcribe(dry, sr)
        dst = os.path.join(S.OUT_DIR, column, key + ".wav")
        vt.write_wav16(dst, y, fx.OUT_RATE)
        final, fsr = vt.read_wav(dst)
        m = vt.measure(final, fsr)
        speech = len(dry) / sr
        t = timing.get(src_key, {})
        probe = None if s["fx"] else accent_probe.probe(asr, text, dry, sr)
        res[key] = dict(text=text, heard=heard["text"], lang=heard.get("lang", ""), accent=probe,
                        cer=round(vt.cer_strict(text, heard["text"]), 3), cer_sound=round(vt.cer_sound(text, heard["text"]), 3),
                        chars_per_s=round(n_han / max(m["duration"] - 2 * fx.EDGE_SILENCE, 0.1), 2),
                        dry_seconds=round(speech, 2), native_rate=sr, rtf=t.get("rtf"), compute=t.get("compute"),
                        tts_text=t.get("tts_text"), fx_strength=strength, **m)
        print(column, key, res[key]["cer"], res[key]["cer_sound"], res[key]["lufs"], res[key]["true_peak_db"], heard["text"], flush=True)
    return res


# ------------------------------------------------------------------ per-column summaries

def dry(col_res: dict) -> dict:
    return {k: r for k, r in col_res.items() if not k.startswith("f_")}


def cpu_speed(col_res: dict) -> str:
    rows = [(r["compute"], r["dry_seconds"]) for r in dry(col_res).values() if r.get("compute")]
    if not rows:
        return "n/a"
    return f"{sum(a for a, _ in rows) / sum(b for _, b in rows):.1f}"


def mean_cer(col_res: dict, field: str = "cer_sound") -> float:
    return statistics.mean(r[field] for r in dry(col_res).values())


def accent_summary(col_res: dict) -> dict:
    return accent_probe.summarise([r["accent"] for r in col_res.values() if r.get("accent")])


def ji_text(a: dict) -> str:
    if not a["ji"]:
        return "n/a"
    rising = sum(1 for m in a["ji"] if m > 0.5)
    return f"{rising} of {len(a['ji'])} rising (" + ", ".join(f"{m:+.1f}" for m in a["ji"]) + " st)"


def retro_text(a: dict) -> str:
    if "retroflex_index" not in a:
        return f"too few syllables ({a['n_retro']}/{a['n_dental']})"
    return f"{a['retroflex_index']:.2f}"


def flags(key: str, r: dict) -> list[str]:
    out = []
    lim = 0.5 if key.startswith("f_") else 0.2
    if r["cer_sound"] > lim:
        out.append(f"recogniser error high ({r['cer_sound']:.2f})")
    if r["true_peak_db"] > fx.MAX_TRUE_PEAK_DB + 0.05 or r["clipped"]:
        out.append("peak too high")
    if abs(r["lufs"] - fx.TARGET_LUFS) > 1.0:
        out.append(f"loudness {r['lufs']:.1f} LUFS")
    if not key.startswith("f_") and not (2.5 <= r["chars_per_s"] <= 6.5):
        out.append(f"{'slow' if r['chars_per_s'] < 2.5 else 'fast'}: {r['chars_per_s']} characters/s including pauses")
    if r["nan"]:
        out.append("NaN in audio")
    if r.get("fx_strength") and r["fx_strength"] < 1.0:
        out.append(f"unintelligible at full effect strength; chain reduced to {int(r['fx_strength'] * 100)} %")
    return out


def all_flags(report: dict) -> list[str]:
    out = []
    for c in sorted(report, key=lambda c: MODELS[c]["order"]):
        for key, r in report[c].items():
            for w in flags(key, r):
                out.append(f"{c}/{key}: {w}")
    return out


def notes_module():
    """samples_notes.py holds the prose (what to listen for, observations, recommendation); optional."""
    try:
        import samples_notes
        return samples_notes
    except ImportError:
        return None


# ------------------------------------------------------------------ pages

CSS = """
body{font-family:"Segoe UI","Microsoft JhengHei","PingFang TC","Noto Sans TC",sans-serif;background:#16171b;color:#e8e6e1;margin:24px;line-height:1.55}
h1{font-size:22px;margin-bottom:4px} h2{font-size:17px;margin-top:30px} a{color:#f0a45a}
p,li{max-width:1100px} .wrap{overflow-x:auto}
table{border-collapse:collapse} th,td{border:1px solid #3a3c44;padding:8px;vertical-align:top}
th{background:#23252b;font-weight:600;font-size:14px} th .sub{display:block;font-weight:400;font-size:12px;color:#a9a59c}
td.line{min-width:250px;max-width:330px;background:#1c1e23}
.zh{font-size:17px;margin:4px 0} .note{font-size:12px;color:#a9a59c} .m{font-size:11px;color:#9a968d;font-family:Consolas,monospace}
.warn{color:#ff8a6b;font-size:12px} audio{width:220px;height:34px} .heard{font-size:12px;color:#c9c5bb;max-width:220px}
.card{background:#1c1e23;border:1px solid #3a3c44;padding:10px 14px;margin:8px 0;max-width:1100px} .small{font-size:13px;color:#cfcbc2}
table.sum td,table.sum th{font-size:13px;padding:5px 9px} table.sum td.n{text-align:right;font-family:Consolas,monospace}
"""


def build_html(report: dict, texts: dict) -> str:
    cols = sorted(report, key=lambda c: MODELS[c]["order"])
    notes = notes_module()
    e = html.escape
    h = [f"<!doctype html><html lang='en'><head><meta charset='utf-8'><title>Ashen Abyss - voice model listening test</title><style>{CSS}</style></head><body>"]
    h.append("<h1>《燼淵 Ashen Abyss》 voice-over: listening test of the candidate models</h1>")
    h.append("<p class='small'>The same six lines from <code>vo_lines.json</code> on every candidate. Columns are models (or two ways of driving one model), "
             "rows are lines. The four human lines (a-d) have <b>no effects</b>: they are only trimmed and matched in loudness (about -17 LUFS), so what you "
             "hear is the model. Row e is the boss line as the model produced it, row f is the same take through the boss effect chain. "
             "Everything was generated offline on this machine's CPU; no model is chosen yet and nothing has been written into the game.</p>")
    if notes:
        h.append("<h2>What to listen for</h2><ul class='small'>" + "".join(f"<li>{x}</li>" for x in notes.LISTEN) + "</ul>")
    h.append("<h2>Samples</h2><div class='wrap'><table><tr><th>Line</th>" +
             "".join(f"<th>{e(MODELS[c]['name'])}<span class='sub'>{e(MODELS[c]['sub'])}</span></th>" for c in cols) + "</tr>")
    for s in S.SAMPLES:
        key = s["key"]
        text = texts[s.get("same_as", key)]
        h.append(f"<tr><td class='line'><div class='note'><b>{key[0]}</b> - {e(s['role'])} - {e(', '.join(s['ids']))}</div>"
                 f"<div class='zh' lang='zh-Hant'>{e(text)}</div><div class='note'>{e(s['note'])}</div></td>")
        for c in cols:
            r = report[c].get(key)
            if not r:
                h.append("<td class='note'>not generated</td>")
                continue
            w = flags(key, r)
            speed = f" | compute {r['rtf']:.1f}x" if r.get("rtf") and not key.startswith("f_") else ""
            h.append(f"<td><audio controls preload='none' src='{c}/{key}.wav'></audio>"
                     f"<div class='m'>CER {r['cer']:.2f} (sound {r['cer_sound']:.2f}) | {r['duration']:.1f} s | {r['chars_per_s']:.1f} ch/s{speed}</div>"
                     f"<div class='heard' lang='zh-Hant'>ASR: {e(vt.s2t(r['heard']))}</div>"
                     + (f"<div class='warn'>Note: {e('; '.join(w))}</div>" if w else "") + "</td>")
        h.append("</tr>")
    h.append("</table></div>")
    h.append("<p class='note'>Under each player: CER = character error rate of an offline recogniser (SenseVoice) reading the sample back, against the script; "
             "\"sound\" = the same with homophones counted as correct (a recogniser cannot know that ㄐㄧㄣˋ is written 燼); duration; characters per second "
             "including pauses; compute = seconds of CPU work per second of audio. \"ASR\" is what the recogniser wrote down.</p>")

    h.append("<h2>Measurements per column</h2><div class='wrap'><table class='sum'><tr><th>Column</th><th>CPU compute per 1 s of audio</th><th>Mean CER (sound)</th>"
             "<th>Boss line after effects, CER</th><th>寂 spoken with Taiwan tone (rising)</th><th>Retroflex index</th><th>Native rate</th></tr>")
    for c in cols:
        a = accent_summary(report[c])
        fxr = report[c].get("f_boss_fx", {})
        rate = sorted({r["native_rate"] for r in report[c].values()})[0]
        h.append(f"<tr><td>{e(MODELS[c]['name'])} <span class='note'>{e(MODELS[c]['sub'])}</span></td><td class='n'>{cpu_speed(report[c])} s</td>"
                 f"<td class='n'>{mean_cer(report[c], 'cer'):.3f} ({mean_cer(report[c]):.3f})</td>"
                 f"<td class='n'>{fxr.get('cer_sound', float('nan')):.2f}{'' if (fxr.get('fx_strength') or 1) >= 1 else ' at %d %%' % int(fxr['fx_strength'] * 100)}</td>"
                 f"<td>{e(ji_text(a))}</td><td class='n'>{e(retro_text(a))}</td><td class='n'>{rate // 1000 if rate % 1000 == 0 else rate / 1000:g} kHz</td></tr>")
    h.append("</table></div>")
    if notes:
        h.append("<ul class='small'>" + "".join(f"<li>{x}</li>" for x in notes.MEASURE_NOTES) + "</ul>")

    h.append("<h2>The candidates</h2>")
    for c in cols:
        m = MODELS[c]
        obs = (notes.OBSERVED.get(c) if notes else None)
        h.append(f"<div class='card'><b>{e(m['name'])}</b> - {e(m['sub'])} (<a href='{m['url']}'>{m['url']}</a>)<br><span class='small'>"
                 f"<b>Size:</b> {e(m['size'])}. <b>Licence:</b> {e(m['licence'])}. <b>CPU speed measured:</b> {cpu_speed(report[c])} s of compute per second of audio.<br>"
                 f"<b>Where the Taiwanese accent comes from:</b> {e(m['accent'])}<br><b>Voice used in these samples:</b> {e(m['voice'])}"
                 + (f"<br><b>Observed:</b> {obs}" if obs else "") + "</span></div>")
    if notes:
        h.append("<h2>Findings and recommendation</h2><ul class='small'>" + "".join(f"<li>{x}</li>" for x in notes.FINDINGS) + "</ul>")
        h.append("".join(f"<p class='small'>{x}</p>" for x in notes.RECOMMENDATION))
        h.append("<h2>Not done yet, and candidates left out</h2><ul class='small'>" + "".join(f"<li>{x}</li>" for x in notes.NOT_DONE) + "</ul>")
    h.append("</body></html>")
    return "\n".join(h)


def strip_tags(x: str) -> str:
    import re
    return html.unescape(re.sub(r"<[^>]+>", "", x))


def wrap(text: str, first: str, rest: str, width: int = 112) -> list[str]:
    """Word-wrap one paragraph for the plain-text README (CJK characters count as two columns)."""
    import unicodedata
    lines, cur, cur_w = [], first, len(first)
    for word in text.split(" "):
        w = sum(2 if unicodedata.east_asian_width(c) in "WF" else 1 for c in word)
        if cur_w + w + 1 > width and cur.strip():
            lines.append(cur.rstrip())
            cur, cur_w = rest, len(rest)
        cur += word + " "
        cur_w += w + 1
    lines.append(cur.rstrip())
    return lines


def build_readme(report: dict, texts: dict) -> str:
    cols = sorted(report, key=lambda c: MODELS[c]["order"])
    notes = notes_module()
    t = ["《燼淵 Ashen Abyss》 voice-over: listening test of the candidate models  (Y:\\voice_samples)", "=" * 78, "",
         "Open index.html to play everything in a table. One sub-folder per column; files a-f are the same six lines:", ""]
    for s in S.SAMPLES:
        t.append(f"  {s['key']:<11} {s['role']}  ({', '.join(s['ids'])})")
        t.append(f"              {texts[s.get('same_as', s['key'])]}")
        t.append(f"              {s['note']}")
    t += ["", "Lines a-d have no effects (trim + loudness match to about -17 LUFS only); e is the boss line dry, f is the same take",
          "through the boss effect chain. Format: 16-bit, 44.1 kHz, mono WAV. Generated offline on this machine's CPU.",
          "No model is chosen yet; nothing has been written into the game.", ""]
    if notes:
        t += ["WHAT TO LISTEN FOR", "-" * 18] + [l for x in notes.LISTEN for l in wrap(strip_tags(x), "  * ", "    ")] + [""]
    t += ["THE COLUMNS", "-" * 11]
    for c in cols:
        m = MODELS[c]
        a = accent_summary(report[c])
        t += [f"[{c}]  {m['name']} - {m['sub']}", f"  Source:   {m['url']}", f"  Size:     {m['size']}", f"  Licence:  {m['licence']}"]
        t += wrap(m["accent"], "  Accent:   ", "            ") + wrap(m["voice"], "  Voice:    ", "            ")
        t += [f"  CPU:      {cpu_speed(report[c])} s of compute per second of audio"]
        t += wrap(f"mean CER {mean_cer(report[c], 'cer'):.3f} (homophones forgiven {mean_cer(report[c]):.3f}); "
                  f"寂 with rising (Taiwan) tone: {ji_text(a)}; retroflex index {retro_text(a)}", "  Measured: ", "            ")
        if notes and notes.OBSERVED.get(c):
            t += wrap(strip_tags(notes.OBSERVED[c]), "  Observed: ", "            ")
        for key, r in report[c].items():
            w = flags(key, r)
            t.append(f"    {key:<11} CER {r['cer']:.2f} (sound {r['cer_sound']:.2f})  {r['duration']:5.1f} s  {r['chars_per_s']:.1f} ch/s  "
                     f"{r['lufs']:.1f} LUFS  {r['true_peak_db']:.1f} dBTP  ASR: {vt.s2t(r['heard'])}" + (f"   NOTE: {'; '.join(w)}" if w else ""))
        t.append("")
    if notes:
        bullets = lambda xs: [l for x in xs for l in wrap(strip_tags(x), "  * ", "    ")]
        t += ["HOW TO READ THE MEASUREMENTS", "-" * 28] + bullets(notes.MEASURE_NOTES) + [""]
        t += ["FINDINGS", "-" * 8] + bullets(notes.FINDINGS) + [""]
        t += ["RECOMMENDATION", "-" * 14] + [l for x in notes.RECOMMENDATION for l in wrap(strip_tags(x), "  ", "  ") + [""]]
        t += ["NOT DONE YET, AND CANDIDATES LEFT OUT", "-" * 37] + bullets(notes.NOT_DONE) + [""]
    return "\r\n".join(t)


def main():
    texts = S.line_texts()
    rp = os.path.join(S.OUT_DIR, "report.json")
    if "--pages" in sys.argv:
        report = json.load(open(rp, encoding="utf-8"))
    else:
        asr = vt.Asr()
        report = {}
        for column in sorted(os.listdir(S.STAGE)):
            if column not in MODELS or not os.path.isdir(os.path.join(S.STAGE, column)):
                continue
            r = process(column, asr, texts)
            if r:
                report[column] = r
        os.makedirs(S.OUT_DIR, exist_ok=True)
        json.dump(report, open(rp, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    open(os.path.join(S.OUT_DIR, "index.html"), "w", encoding="utf-8").write(build_html(report, texts))
    open(os.path.join(S.OUT_DIR, "README.txt"), "w", encoding="utf-8-sig").write(build_readme(report, texts))
    for w in all_flags(report):
        print("FLAG", w)
    print("wrote", S.OUT_DIR)


if __name__ == "__main__":
    main()
