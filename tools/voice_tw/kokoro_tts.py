"""Kokoro v1.1-zh (Apache-2.0, already on disk) through sherpa-onnx: the baseline voice, and the
source of SYNTHETIC reference clips for the zero-shot models (no real person's voice is cloned).

    python kokoro_tts.py jobs.json      jobs = [{"out": wav, "text": zh-TW text, "sid": 63, "speed": 1.0}, ...]
"""
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")   # never depend on the console code page
import time

import numpy as np

import vt


def make_tts():
    import sherpa_onnx
    d = vt.KOKORO_DIR
    cfg = sherpa_onnx.OfflineTtsConfig(
        model=sherpa_onnx.OfflineTtsModelConfig(
            kokoro=sherpa_onnx.OfflineTtsKokoroModelConfig(
                model=os.path.join(d, "model.onnx"), voices=os.path.join(d, "voices.bin"),
                tokens=os.path.join(d, "tokens.txt"), data_dir=os.path.join(d, "espeak-ng-data"),
                dict_dir=os.path.join(d, "dict"),
                lexicon=",".join(os.path.join(d, f) for f in ("lexicon-us-en.txt", "lexicon-zh.txt"))),
            num_threads=vt.THREADS, provider="cpu"),
        rule_fsts=",".join(os.path.join(d, f) for f in ("phone-zh.fst", "date-zh.fst", "number-zh.fst")),
        max_num_sentences=1)
    return sherpa_onnx.OfflineTts(cfg)


def main():
    jobs = json.load(open(sys.argv[1], encoding="utf-8"))
    tts = make_tts()
    for j in jobs:
        t0 = time.time()
        audio = tts.generate(vt.t2s(j["text"]), sid=int(j["sid"]), speed=float(j.get("speed", 1.0)))
        x = np.asarray(audio.samples, dtype=np.float32)
        vt.write_float(j["out"], x, audio.sample_rate)
        dur = len(x) / audio.sample_rate
        el = time.time() - t0
        print(json.dumps(dict(out=j["out"], seconds=round(dur, 2), compute=round(el, 2), rtf=round(el / max(dur, 1e-6), 3)), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
