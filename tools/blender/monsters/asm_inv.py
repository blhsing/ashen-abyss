"""Inventory of an existing creature script (no bake, no export): looks, captured sculpt pose, parts grouped by look/bone/obj.
   bl.ps1 asm_inv.py "<id> [<id> ...]" """
import sys, os, importlib
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import numpy as np
import monlib as M

for cid in sys.argv[sys.argv.index('--') + 1:]:
    poses = []
    orig = M.pose_sculpt
    def cap(part, pose, _o=orig):
        poses.append((len(part.v), pose))
        return _o(part, pose)
    M.pose_sculpt = cap
    try:
        C, notes = importlib.import_module(cid).build()
    finally:
        M.pose_sculpt = orig
    M.log("=====", cid, "parts", len(C.parts), "tris", sum(p.ntri for p in C.parts))
    for k, v in C.looks.items():
        M.log("LOOK %-10s %-8s %-16s %s" % (k, v['kind'], v['slot'], {a: (tuple(round(x, 3) for x in b) if isinstance(b, (tuple, list)) else b) for a, b in v['params'].items()}))
    for n, pose in sorted(poses, key=lambda t: -t[0])[:1]:
        M.log("POSE(nv=%d) %s" % (n, pose))
    grp = {}
    for p in C.parts:
        key = (p.look, p.bone or 'SKIN', p.obj)
        g = grp.setdefault(key, [0, 0, np.array([1e9] * 3), -np.array([1e9] * 3)])
        g[0] += 1; g[1] += p.ntri
        g[2] = np.minimum(g[2], p.v.min(0)); g[3] = np.maximum(g[3], p.v.max(0))
    for (lk, bn, ob), g in grp.items():
        M.log("PART %-9s %-12s %-10s n=%3d tris=%5d  bb %s .. %s" % (lk, bn, ob, g[0], g[1], np.round(g[2], 2).tolist(), np.round(g[3], 2).tolist()))
