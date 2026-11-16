import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np
cid = sys.argv[sys.argv.index('--') + 1]
reset_scene()
bpy.ops.import_scene.gltf(filepath=os.path.join(OUT, cid + ".glb"))
for o in bpy.data.objects:
    if o.type != 'MESH' or not o.vertex_groups:
        continue
    co = get_co(o)
    names = [g.name for g in o.vertex_groups]
    bad = []
    for v in o.data.vertices:
        x = co[v.index, 0]
        for g in v.groups:
            n = names[g.group]
            if g.weight > 0.3 and ((x < -0.12 and n.endswith("_l")) or (x > 0.12 and n.endswith("_r"))):
                bad.append((v.index, n))
                break
    log(o.name, "wrong-side verts:", len(bad))
    if bad:
        idx = np.array([b[0] for b in bad])
        log("   bbox", co[idx].min(0).round(3), co[idx].max(0).round(3), "bones", sorted(set(b[1] for b in bad)))
