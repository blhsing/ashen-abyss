"""Show the progress of chunked downloads (fetch.py) below a folder:  python -I dlstatus.py <folder>"""
import json, os, sys
for root, _, files in os.walk(sys.argv[1]):
    for f in files:
        if f.endswith(".chunks.json"):
            try:
                st = json.load(open(os.path.join(root, f), encoding="utf-8"))
            except Exception:
                continue
            done = min(st["size"], len(st["done"]) * st["chunk"])
            print("%-70s %6.0f / %6.0f MB (%4.1f%%)" % (f[:-12][-70:], done / 1e6, st["size"] / 1e6, 100.0 * done / st["size"]))
