"""debug helper (H1): which parts come close to the pieces rigid on a bone.
  bb.ps1 dbg_parts.py "<id> <bone> [<max distance>]" -Dir Y:\\tools\\blender\\beasts\\h1"""
import sys
sys.path.insert(0, r"Y:\tools\blender\beasts")
sys.path.insert(0, r"Y:\tools\blender\beasts\h1")
import numpy as np
import blib as B
import bparts as P
B.set_work(r"Y:\tools\blender\beasts\h1\out")
import spec_h1 as S

args = sys.argv[sys.argv.index('--') + 1:]
cid, bone = args[0], args[1]
lim = float(args[2]) if len(args) > 2 else 0.03


def fake(self, bake=True, notes=""):
    tgt = [p for p in self.parts if p.bone == bone]
    for i, q in enumerate(self.parts):
        if q.bone == bone:
            continue
        bv = P.bvh_of(q)
        for t in tgt:
            d = min(P.nearest(bv, v)[2] for v in t.v)
            if d < lim:
                print("[beast] near part %d bone=%s look=%s verts=%d dist=%.4f centre=%s" % (i, q.bone, q.look, len(q.v), d, np.round(q.v.mean(0), 2)))
    return {}


B.Beast.finalize = fake
S.BUILD[cid](False)
