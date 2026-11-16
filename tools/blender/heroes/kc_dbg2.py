"""debug: which vertices of an exported character are bound to the exporter's 'neutral_bone' (= had no weights)"""
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np
cid = sys.argv[sys.argv.index('--') + 1]
reset_scene()
bpy.ops.import_scene.gltf(filepath=os.path.join(OUT, cid + ".glb"))
for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    vg = o.vertex_groups.get("neutral_bone")
    if vg is None:
        continue
    pts = []
    for v in o.data.vertices:
        for g in v.groups:
            if g.group == vg.index and g.weight > 0:
                pts.append(tuple(v.co))
    if pts:
        P = np.array(pts)
        log("NEUTRAL", o.name, len(pts), "bbox", P.min(0).round(3), P.max(0).round(3))
        for p in P[:12]:
            log("    ", p.round(3))
log("done")
