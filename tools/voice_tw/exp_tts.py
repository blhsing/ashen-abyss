"""Experiment helper: speak arbitrary TTS-input strings as continuations of an anchor and print what the
recogniser hears (run with the `voxcpm` venv). Used to compare ways of writing a reading hint.

    exp_tts.py <anchor key> "<expected text>" "<tts input 1>" "<tts input 2>" ...
"""
import json
import os
import sys

import engine as E
import vt

LOG = os.path.join(vt.ROOT, "logs", "exp_tts.log")


def main():
    key, expected, variants = sys.argv[1], sys.argv[2], sys.argv[3:]
    a = json.load(open(os.path.join(E.ANCHORS, key + ".json"), encoding="utf-8"))
    eng = E.Engine(LOG)
    for n, tts in enumerate(variants):
        for seed in (1,):
            x = eng.synth(tts, seed, anchor=a)
            r = eng.check(expected, x, E.FULL)
            vt.write_float(os.path.join(vt.ROOT, "stage", "exp", f"tts_{key}_{n}_{seed}.wav"), x, eng.sr)
            E.log(f"[{tts}] seed {seed}: {r['seconds']:.1f}s cer {r['cer']:.3f}  heard: {r['heard']}", LOG)


if __name__ == "__main__":
    main()
