"""Dynamics of mastered finals / candidates: PLR (true peak - integrated LUFS) and LRA (EBU R128 loudness range:
10th-95th percentile of gated 3 s short-term loudness).   python dyn_probe.py <ogg|flac> ..."""
import os, sys
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import audiolib as al

for f in sys.argv[1:]:
    x, sr = al.read_audio(f)
    d = al.dynamics(x, sr)
    print("%-28s LUFS %6.1f  PLR %5.1f  LRA %5.1f  key %s" % (os.path.basename(f), d["lufs"], d["plr"], d["lra"], "-"))
