"""debug: radius profile of a mesh of an exported character around the head axis"""
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np
args = sys.argv[sys.argv.index('--') + 1:]
cid, mesh = args[0], args[1]
z0, z1 = float(args[2]), float(args[3])
reset_scene()
bpy.ops.import_scene.gltf(filepath=os.path.join(OUT, cid + ".glb"))
o = bpy.data.objects[mesh]
co = get_co(o)
M = np.array(o.matrix_world)
co = co @ M[:3, :3].T + M[:3, 3]
log("mesh", mesh, "verts", len(co), "bbox", co.min(0).round(3), co.max(0).round(3))
r = np.sqrt(co[:, 0] ** 2 + (co[:, 1] + 0.02) ** 2)
for z in np.arange(z0, z1, 0.02):
    m = (np.abs(co[:, 2] - z) < 0.01) & (r < 0.3)
    if m.any():
        log("z=%.2f n=%d r[min %.3f max %.3f] x[%.3f %.3f] y[%.3f %.3f]" % (z, m.sum(), r[m].min(), r[m].max(), co[m, 0].min(), co[m, 0].max(),
                                                                    co[m, 1].min(), co[m, 1].max()))
