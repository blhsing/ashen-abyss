"""Pipeline self-test without the music model: turns procedural tracks into fake 48 kHz 'candidates' so that
produce.py --only-eval can be exercised end to end (evaluate -> select -> loop/master -> report)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import scipy.signal as sps
import audiolib as al
import tracks as T

stage = sys.argv[1]
os.makedirs(os.path.join(stage, "cand"), exist_ok=True)
for name in sys.argv[2].split(","):
    x, sr = al.read_audio(os.path.join(r"Y:\music_backup\procedural", name + ".ogg"))
    y = sps.resample_poly(x, 160, 147, axis=0)
    for k, seed in enumerate(T.BY_NAME[name]["seeds"][:2]):
        al.write_flac(os.path.join(stage, "cand", "%s__s%d.flac" % (name, seed)), y * (1.0 - 0.1 * k), 48000)
    print("fake candidates for", name, len(y) / 48000.0, "s")
