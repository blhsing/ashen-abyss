"""probe.py <class>: capture today's hero, dump its parts and the rig landmarks (-> _work/parts_<class>.json)"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gearlib import *

cls = args()[0]
cap = capture_hero(cls)
c = cap["c"]
B = c.B
log("kw", {k: v for k, v in cap["kw"].items() if k != "notes"}, "dropped", cap["dropped"], "drop_face", cap["drop_face"])
log("marks", {k: [round(float(q), 3) for q in v] for k, v in rig_marks().items()})
for z in (0.10, 0.2, 0.3, 0.44, 0.5, 0.6, 0.7, 0.8, 0.86, 0.9, 0.95, 1.0, 1.05, 1.1, 1.2, 1.3, 1.4, 1.5, 1.55):
    s = B.slice(z, 0.012)
    log("slice z=%.2f" % z, None if s is None else [round(float(q), 3) for q in s])
fr, vr = face_regions(B)
for i, n in enumerate(SKIN_REGIONS):
    m = vr == i
    log("region %-10s verts %5d  bbox %s .. %s" % (n, m.sum(), np.round(B.co[m].min(0), 3), np.round(B.co[m].max(0), 3)))
log("hairs", [o.name for o in c.hairs], "eyes", c.eyes is not None, "brows", c.brows is not None, "skin verts", len(c.skin.data.vertices),
    "hide", int(c.hide.sum()), "/", len(c.hide))
out = []
for o in mk.PARTS:
    mk.apply_mods(o)
    co = get_co(o)
    W, names = get_weights(o)
    tot = W.sum(0)
    top = sorted(zip(names, tot), key=lambda t: -t[1])[:3]
    d = dict(name=o.name, cls=o["cls"], src=o["src"], owner=o["owner"], tris=mk.tri_count(o), verts=len(co),
             mn=[round(float(v), 3) for v in co.min(0)], mx=[round(float(v), 3) for v in co.max(0)],
             bones=[(n, round(float(w / max(tot.sum(), 1e-9)), 2)) for n, w in top])
    out.append(d)
    log("part %-22s %-7s %-12s %-8s tris %5d  z %.2f..%.2f x %.2f..%.2f  %s" % (d["name"], d["cls"], d["src"], d["owner"], d["tris"],
        d["mn"][2], d["mx"][2], d["mn"][0], d["mx"][0], d["bones"]))
with open(os.path.join(WORK, "parts_%s.json" % cls), "w") as f:
    json.dump(out, f, indent=1)
