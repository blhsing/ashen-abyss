"""debug: which part is the lowest in a clip (and when).
  bb.ps1 dbg_low.py "<id> <clip> [limit]" -Dir Y:\\tools\\blender\\beasts\\h4\\scratch
builds the creature without bake / export and lists, per frame where the mesh is below `limit`, the lowest part."""
import sys, os
import numpy as np
BE = r"Y:\tools\blender\beasts"
sys.path.insert(0, BE)
sys.path.insert(0, os.path.join(BE, "h4"))
args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
cid, clip = args[0], args[1]
limit = float(args[2]) if len(args) > 2 else -0.02
import blib as B
B.set_work(os.path.join(BE, "h4", "out", "dbg"))
import bparts
import spec_h4

keep = {}
orig = B.Beast.finalize


def fin(self, bake=True, export=True, notes=""):
    keep['C'] = self
    return {}


B.Beast.finalize = fin
spec_h4.BUILD[cid](False)
C = keep['C']
V0, W, comp, tris = C._collect()
c = C.clips[clip]
seen = {}
for f in range(c['n'] + 1):
    P = C.posed(V0, W, c['G'][f])
    z = P[:, 2]
    for k in np.unique(comp[z < limit]):
        zz = float(z[comp == k].min())
        if zz < seen.get(int(k), (0.0, 0))[0]:
            seen[int(k)] = (zz, f)
for k, (zz, f) in sorted(seen.items(), key=lambda kv: kv[1][0]):
    p = C.parts[k]
    print("[beast] LOW %s %s: part %d look=%s bone=%s tris=%d  min z %.3f at frame %d/%d" % (cid, clip, k, p.look, p.bone, p.ntri, zz, f, c['n']))
print("[beast] LOW done", cid, clip, "parts below", limit, ":", len(seen))
