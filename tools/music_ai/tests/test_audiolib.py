"""Smoke test of the measurement / loop / mastering code on the procedural baseline tracks (no model needed)."""
import json, os, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import numpy as np
import audiolib as al

src = sys.argv[1]
out_dir = sys.argv[2]
os.makedirs(out_dir, exist_ok=True)
for name, bpm, pulse in (("menu_theme", 54, False), ("boss", 120, True), ("wastes_combat", 80, True)):
    t0 = time.time()
    x, sr = al.read_audio(os.path.join(src, name + ".ogg"))
    m = al.measure(x, sr, bpm=bpm, pulse=pulse)
    print(name, sr, json.dumps(m))
    lp = al.find_loop(x, sr, bpm, 4, (60, 90), pulse)
    print("  loop", json.dumps(lp))
    y, bi = al.build_loop(x, sr, lp["start"], lp["length"], lp["xfade"])
    print("  build", bi, "seam raw", al.seam_metrics(y, sr))
    z, mi = al.master(y, sr, -18.0, True)
    print("  master", json.dumps(mi))
    print("  seam mastered", al.seam_metrics(z, 44100))
    p = os.path.join(out_dir, name + "_looptest.ogg")
    al.write_ogg(p, z, 44100, 0.6)
    d, dsr = al.read_audio(p)
    print("  ogg: %d samples (expected %d), %.0f kbit/s, lufs %.2f, seam %s" % (
        len(d), len(z), os.path.getsize(p) * 8 / (len(d) / dsr) / 1000, al.lufs(d, dsr), al.seam_metrics(d, dsr)))
    print("  %.1f s" % (time.time() - t0))
