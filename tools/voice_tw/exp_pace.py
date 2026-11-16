"""Experiment: can a continuation take be slowed down by a style hint? (run with the `voxcpm` venv)

The boss lines came out at a normal speaking pace when continuing their anchor. Tested on one boss line:
  A  prompt + reference (production way), no hint
  B  prompt + reference, text prefixed with a style hint in parentheses
  C  reference only ("controllable cloning"), style hint
Reports duration, error rate (does the hint leak into the speech?) and speaker similarity to the anchor.
"""
import json
import os
import random
import sys

import numpy as np
import torch

import engine as E
import tw_text
import vt

LOG = os.path.join(vt.ROOT, "logs", "exp_pace.log")


def main():
    key = sys.argv[1] if len(sys.argv) > 1 else "boss_osaros@deep_clear"
    line_id = sys.argv[2] if len(sys.argv) > 2 else "boss_osaros_phase"
    lines = {l["id"]: l for l in json.load(open(vt.VO_LINES, encoding="utf-8"))}
    text = lines[line_id]["text"]
    a = json.load(open(os.path.join(E.ANCHORS, key + ".json"), encoding="utf-8"))
    eng = E.Engine(LOG)
    tts = tw_text.to_voxcpm(text)
    hint = "语速非常缓慢，一字一顿，沉稳"
    out = os.path.join(vt.ROOT, "stage", "exp")
    cases = [("A_plain", dict(prompt_wav_path=a["wav"], prompt_text=a["tts_text"], reference_wav_path=a["wav"]), tts),
             ("B_prompt_hint", dict(prompt_wav_path=a["wav"], prompt_text=a["tts_text"], reference_wav_path=a["wav"]), f"({hint}){tts}"),
             ("C_ref_hint", dict(reference_wav_path=a["wav"]), f"({hint}){tts}")]
    paths = [a["wav"]]
    rows = []
    for name, kw, t in cases:
        torch.manual_seed(1); np.random.seed(1); random.seed(1)
        with torch.inference_mode():
            x = E.trim(np.asarray(eng.model.generate(text=t, cfg_value=2.0, inference_timesteps=10, **kw), dtype=np.float32).reshape(-1), eng.sr)
        r = eng.check(text, x, E.FULL)
        p = os.path.join(out, f"pace_{name}.wav")
        vt.write_float(p, x, eng.sr)
        paths.append(p)
        rows.append((name, r))
        E.log(f"{name}: {r['seconds']:.1f}s {r['chars_per_s']:.1f} ch/s cer {r['cer']:.3f}  {r['heard']}", LOG)
    emb = E.embeddings(paths)
    for (name, r), p in zip(rows, paths[1:]):
        E.log(f"{name}: similarity to anchor {E.cos(emb[p], emb[paths[0]]):.2f}", LOG)


if __name__ == "__main__":
    main()
