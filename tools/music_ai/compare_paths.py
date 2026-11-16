"""Compare renders of the same piece and seed made on different devices (CPU reference first).

    python compare_paths.py <reference.flac> <other.flac> [<other.flac> ...]

Because the initial noise is identical, a correct accelerator path reproduces the CPU render almost sample for
sample; this prints correlation, loudness and spectrum differences plus the timings stored next to each file.
"""
import json
import os
import sys

import numpy as np
import scipy.signal as sps

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import audiolib as al


def bands(x, sr):
    f, p = sps.welch(x.mean(axis=1), sr, nperseg=8192)
    edges = [20, 80, 250, 1000, 4000, 12000, 22000]
    return [10 * np.log10(max(p[(f >= a) & (f < b)].sum(), 1e-20)) for a, b in zip(edges[:-1], edges[1:])]


ref, sr = al.read_audio(sys.argv[1])
rb = bands(ref, sr)
print("%-34s %8s %8s %7s %7s %9s  %s" % ("file", "corr", "snr dB", "LUFS", "peak", "s/s audio", "band diff vs ref dB (20-80,80-250,250-1k,1-4k,4-12k,12-22k)"))
for path in sys.argv[1:]:
    x, sr2 = al.read_audio(path)
    n = min(len(x), len(ref))
    a, b = ref[:n].ravel(), x[:n].ravel()
    corr = float(np.corrcoef(a, b)[0, 1])
    err = a - b
    snr = 10 * np.log10(np.sum(a * a) / max(np.sum(err * err), 1e-20))
    meta = {}
    mj = path[:-5] + ".json"
    if os.path.exists(mj):
        meta = json.load(open(mj, encoding="utf-8"))
    bd = [round(float(v - r), 1) for v, r in zip(bands(x, sr2), rb)]
    finite = bool(np.isfinite(x).all())
    print("%-34s %8.5f %8.1f %7.2f %7.2f %9s  %s%s" % (os.path.basename(path), corr, snr, al.lufs(x, sr2), al.db(np.max(np.abs(x))),
                                                   meta.get("compute_per_audio_s", "-"), bd, "" if finite else "  NaN!"))
    if meta:
        print("      %s | %s | denoise %.1f s (%s per step) | decode %.1f s | load %.0f s | GPU mem %s GB" % (
            meta["device"], meta["dtype"], meta["denoise_s"], meta["step_s"][1:4], meta["decode_s"], meta["load_s"], meta.get("gpu_peak_mem_gb")))
