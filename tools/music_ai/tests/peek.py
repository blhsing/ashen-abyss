"""Measure one raw candidate without the audio-text model (safe to run while the GPU is busy)."""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import numpy as np
import audiolib as al
import tracks as T
for path in sys.argv[1:]:
    name = os.path.basename(path).split("__")[0]
    tr = T.BY_NAME[name]
    x, sr = al.read_audio(path)
    t = time.time()
    m = al.measure(x, sr, bpm=tr["bpm"], pulse=tr["pulse"])
    print(os.path.basename(path), "%.1f s" % (time.time() - t))
    print("  ", json.dumps({k: m[k] for k in ("seconds", "lufs", "peak_db", "lra_db", "silent_frac", "dropouts", "dropout_times", "zero_runs", "holes_hz", "resonance_db", "resonance_hz", "rolloff99_hz", "centroid_hz", "bandwidth_hz", "stereo_width", "tempo", "tempo_near", "pulse_near", "tempo_global", "pulse_global", "tempo_err", "perc_ratio", "key_est", "key_conf", "reject")}))
    print("   tile click dB:", al.boundary_clicks(x, sr, 384 / 25.0), " usable span:", al._usable_span(x, sr))
    lev, hop = al._frame_rms_db(x.mean(axis=1), sr, 1.0, 10.0)
    print("   level every 10 s (dBFS):", " ".join("%.0f" % v for v in lev))
    if tr["loop_s"]:
        lp = al.find_loop(x, sr, tr["bpm"], tr["ts"], tr["loop_s"], tr["pulse"])
        print("   loop", json.dumps(lp))
