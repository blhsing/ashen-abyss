"""Dry run of specs (geometry only, nothing baked or written): catches spec / part errors and reports triangles, materials
and the bounding box of every creature in ONE Blender process.
   bl.ps1 asm_dry.py -a "<id> [<id> ...]"      (or  --family=<fam>)"""
import sys, os, traceback
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import numpy as np
import monlib as M
import asm, asm_specs

args = sys.argv[sys.argv.index('--') + 1:]
ids = []
for a in args:
    if a.startswith('--family='):
        ids += [k for k, v in asm_specs.SPECS.items() if v.get('family') == a.split('=', 1)[1]]
    else:
        ids.append(a)
for cid in ids:
    try:
        C, notes = asm.build(asm_specs.get(cid))
        mn = np.min([p.v.min(0) for p in C.parts], axis=0); mx = np.max([p.v.max(0) for p in C.parts], axis=0)
        tris = sum(p.ntri for p in C.parts)
        per = {}
        for p in C.parts:
            per[p.look] = per.get(p.look, 0) + p.ntri
        top = sorted(per.items(), key=lambda t: -t[1])[:6]
        M.log("DRY OK %-22s tris %5d %s bbox z %.3f..%.3f x %.2f..%.2f y %.2f..%.2f  %s" % (
            cid, tris, "OVER" if tris > 15000 else "", mn[2], mx[2], mn[0], mx[0], mn[1], mx[1], top))
    except Exception:
        traceback.print_exc()
        M.log("DRY FAILED", cid)
