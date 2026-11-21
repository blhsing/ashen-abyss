"""dbg_parts.py <class> <look>: build a look without baking and list its parts (slot, class, src, tris, bbox)."""
import sys, os, importlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gearlib import *
from gearkit import *
cls, look = args()[0], args()[1]
cap = capture_hero(cls)
c = cap["c"]
drop_parts()
K = Kit(c, cls, look)
getattr(importlib.import_module("sets_" + cls), "t4x" if look == "t4" else look)(K)
tot = {}
for o in mk.PARTS:
    mk.apply_mods(o)
    co = get_co(o)
    if not len(co):
        log("EMPTY PART", o["slot"], o.name)
        continue
    t = mk.tri_count(o)
    tot[o["slot"]] = tot.get(o["slot"], 0) + t
    log("%-6s %-22s %-7s %-14s tris %5d  x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (o["slot"], o.name, o["cls"], o["src"], t,
        co[:, 0].min(), co[:, 0].max(), co[:, 1].min(), co[:, 1].max(), co[:, 2].min(), co[:, 2].max()))
log("tris per slot", tot, "total", sum(tot.values()))
