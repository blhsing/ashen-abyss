"""Step 1 of the listening test: reference clips and the Kokoro baseline.

  * kokoro_<role>.wav  synthetic reference clips (Kokoro speakers reading a neutral sentence)
  * twb_<role>.wav     the Taiwanese example clip bundled with the official BreezyVoice repository,
                       first sentence only, pitch/formant-shifted and slowed per role (samples_spec.TW_SHIFT)
  * the Kokoro baseline takes of the sample lines
"""
import json
import os
import subprocess
import sys

import samples_spec as S
import vt


def make_tw_refs(asr):
    """Per-role variants of the bundled Taiwanese example (needs the BreezyVoice clone under %VT_ROOT%/src)."""
    import fx
    src = os.path.join(vt.ROOT, "src", "BreezyVoice", "data", "example.wav")
    if not os.path.exists(src):
        print("skipping twb_* reference clips: BreezyVoice repository not cloned yet")
        return
    x, sr = vt.read_wav(src)
    clip = x[int(S.BUNDLED_CUT[0] * sr):int(S.BUNDLED_CUT[1] * sr)]
    vt.write_float(os.path.join(S.REFS, "bundled_tw.wav"), clip, sr)
    for v, (semitones, tempo) in S.TW_SHIFT.items():
        y = fx.pitch_shift(clip, semitones, sr)
        if tempo != 1.0:
            y = fx.speed(y, tempo, sr)
        y = y / max(1e-6, float(abs(y).max())) * 0.7
        vt.write_float(os.path.join(S.REFS, f"twb_{v}.wav"), y, sr)
        h = asr.transcribe(y, sr)
        print(f"twb_{v}.wav {semitones:+.1f} st x{tempo} {len(y) / sr:.2f}s CER", round(vt.cer_strict(S.BUNDLED_TEXT, h["text"]), 3))


def main():
    os.makedirs(S.REFS, exist_ok=True)
    jobs = []
    for v, d in S.VOICES.items():
        jobs.append(dict(out=os.path.join(S.REFS, f"kokoro_{v}.wav"), text=S.REF_TEXT, sid=d["kokoro_sid"], speed=d["ref_speed"]))
    texts = S.line_texts()
    for s in S.SAMPLES:
        if s.get("same_as"):
            continue
        d = S.VOICES[s["voice"]]
        jobs.append(dict(out=os.path.join(S.STAGE, "kokoro", s["key"] + ".wav"), text=texts[s["key"]], sid=d["kokoro_sid"], speed=d["base_speed"]))
    jf = os.path.join(vt.ROOT, "stage", "jobs_kokoro.json")
    json.dump(jobs, open(jf, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    here = os.path.dirname(os.path.abspath(__file__))
    r = subprocess.run([sys.executable, os.path.join(here, "kokoro_tts.py"), jf], capture_output=True, text=True, encoding="utf-8")
    print(r.stdout[-3000:], r.stderr[-2000:])
    rows = [json.loads(l) for l in r.stdout.splitlines() if l.startswith("{")]
    json.dump(rows, open(os.path.join(S.STAGE, "kokoro", "timing.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    # sanity: what does the recogniser hear in the reference clips?
    asr = vt.Asr()
    make_tw_refs(asr)
    for j in jobs:
        x, sr = vt.read_wav(j["out"])
        h = asr.transcribe(x, sr)
        print(os.path.basename(j["out"]), f"{len(x)/sr:.2f}s", "CER", round(vt.cer_strict(j["text"], h["text"]), 3), h["text"])


if __name__ == "__main__":
    main()
