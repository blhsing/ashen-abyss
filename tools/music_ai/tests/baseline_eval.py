"""Score the 19 procedural tracks with the same audio-text model (reference numbers for the report)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import tracks as T
import produce
for tr in T.TRACKS:
    b = produce.baseline_flags(tr)
    print("%-15s sim %.3f  vocal %.2f  genre %.2f  mood %.2f" % (tr["name"], b.get("sim", 0), b.get("vocal", 0), b.get("genre", 0), b.get("mood", 0)))
