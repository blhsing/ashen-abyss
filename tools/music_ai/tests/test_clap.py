"""Calibration run of the CLAP scorer on existing audio: prints similarity to each piece's description and the flag sets."""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import audiolib as al
import tracks as T
from clap_score import ClapScorer

src = sys.argv[1]
names = sys.argv[2].split(",")
t = time.time()
sc = ClapScorer()
print("loaded in %.1f s on %s, logit scale %.1f" % (time.time() - t, sc.device, sc.scale))
for n in names:
    x, sr = al.read_audio(os.path.join(src, n + ".ogg"))
    t = time.time()
    r = sc.score(x, sr, T.BY_NAME[n]["describe"], T.FLAG_SETS)
    print("%-14s sim %.3f (min %.3f, %d win) %.1f s" % (n, r["sim"], r["sim_min"], r["windows"], time.time() - t))
    for k, f in r["flags"].items():
        print("    %-6s bad_mean %.2f bad_max %.2f top=%s" % (k, f["bad_mean"], f["bad_max"], f["top"]))
        print("           ", {kk[:28]: vv for kk, vv in f["probs"].items()})
# cross-check: every description against every piece (does the text score discriminate?)
desc = [T.BY_NAME[n]["describe"] for n in names]
import numpy as np
embs = []
for n in names:
    x, sr = al.read_audio(os.path.join(src, n + ".ogg"))
    embs.append(sc.audio(x, sr).mean(0))
import torch
A = torch.stack(embs); A = A / A.norm(dim=1, keepdim=True)
M = (A @ sc.text(desc).T).numpy()
print("rows = audio, cols = description of", names)
print(np.round(M, 3))
