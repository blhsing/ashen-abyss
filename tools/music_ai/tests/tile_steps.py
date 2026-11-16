"""Print the tile-boundary step statistic for every raw candidate (48 kHz):  python tile_steps.py <cand dir>"""
import glob, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import audiolib as al
for f in sorted(glob.glob(os.path.join(sys.argv[1], "*.flac"))):
    x, sr = al.read_audio(f)
    ej = f[:-5] + ".eval.json"
    old = json.load(open(ej)) if os.path.exists(ej) else {}
    print("%-26s step %5.2f   old click dB %s  %s" % (os.path.basename(f), al.boundary_steps(x, 384 * 1920, sr),
          old.get("measure", {}).get("tile_click_db"), [r for r in old.get("reject", []) if "tile" in r]), flush=True)
