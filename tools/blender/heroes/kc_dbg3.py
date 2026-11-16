"""debug: list connected islands (bbox centres) of one mesh of an exported character inside a z range"""
import bpy, bmesh, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np
args = sys.argv[sys.argv.index('--') + 1:]
cid, mesh, z0, z1 = args[0], args[1], float(args[2]), float(args[3])
reset_scene()
bpy.ops.import_scene.gltf(filepath=os.path.join(OUT, cid + ".glb"), merge_vertices=True)
o = bpy.data.objects[mesh]
bm = bmesh.new()
bm.from_mesh(o.data)
seen = set()
for v in bm.verts:
    if v.index in seen:
        continue
    stack, isl = [v], []
    seen.add(v.index)
    while stack:
        a = stack.pop()
        isl.append(a)
        for e in a.link_edges:
            b = e.other_vert(a)
            if b.index not in seen:
                seen.add(b.index)
                stack.append(b)
    P = np.array([tuple(q.co) for q in isl])
    cz = P[:, 2].mean()
    if z0 <= cz <= z1:
        nrm = np.mean([tuple(f.normal) for q in isl for f in q.link_faces], axis=0)
        log("island n=%d centre=%s size=%s avg_normal=%s" % (len(isl), P.mean(0).round(3), (P.max(0) - P.min(0)).round(3), nrm.round(2)))
