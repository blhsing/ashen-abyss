"""look-dev driver for the head sculpt: clay renders.  blender -b --python dev_head.py -- [stage] [--tag=x] [--res=n]"""
import sys, os, importlib
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE)); sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S
import ov2_clay as CL, ov2_head as HD, ov2_sculpt as SC, ov2_dense as DN

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
stage = next((a for a in args if not a.startswith('--')), 'sculpt')
tag = next((a.split('=', 1)[1] for a in args if a.startswith('--tag=')), stage)
res = int(next((a.split('=', 1)[1] for a in args if a.startswith('--res=')), '760'))
OUT = os.path.join(HERE, "shots"); os.makedirs(OUT, exist_ok=True)

M.reset_scene(); M.load_rig()
h = HD.base_head(2)
M.log("dense head", len(h.v), "verts", h.ntri, "tris")
parts = []
if stage == 'base':
    parts = [(h, None)]
elif stage == 'sculpt':
    HD.sculpt(h); HD.wrinkles(h)
    parts = [(h, None)]
else:
    D = DN.build_dense()
    parts = D[stage] if stage in D else D['all']
for k, (p, col) in enumerate(parts):
    CL.part_object(p, "p%d" % k, color=col or (0.55, 0.53, 0.50))
shots = [("front", 0, 0), ("q34", 32, 4), ("side", 88, 2), ("low", 18, -24), ("q34r", -34, 6), ("back", 150, 8)]
if stage not in ("base", "sculpt"):
    out = CL.views(os.path.join(OUT, "clayw_" + tag), (0, -0.0, 1.72), 0.26, [("wfront", 0, 0), ("wq34", 32, 6), ("wside", 90, 2)], res=res)
    S.combo(out, os.path.join(OUT, "clayw_%s_sheet.png" % tag), cols=3)
out = CL.views(os.path.join(OUT, "clay_" + tag), (0, -0.035, 1.665), 0.118, shots, res=res)
S.combo(out, os.path.join(OUT, "clay_%s_sheet.png" % tag), cols=3)
M.log("DEV OK", tag)
