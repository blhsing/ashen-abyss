"""Calibrate the word detector: print what the speech recogniser hears in a few files."""
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import audiolib as al
from speech_check import WordDetector
wd = WordDetector()
for path in sys.argv[1:]:
    x, sr = al.read_audio(path)
    t = time.time()
    r = wd.check(x, sr)
    print("%s: %d/%d windows with words (%.1f s)" % (os.path.basename(path), r["word_windows"], r["windows"], time.time() - t))
    for d in r["detail"]:
        print("    nospeech %.2f  logp %5.2f  words %2d  %s  %r" % (d["no_speech"], d["avg_logprob"], d["words"], "REAL" if d["real"] else "    ", d["text"][:90]))
