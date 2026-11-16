import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np
args = sys.argv[sys.argv.index('--') + 1:]
cid = args[0]
pts = [tuple(float(v) for v in a.split(',')) for a in args[1:]]
reset_scene()
bpy.ops.import_scene.gltf(filepath=os.path.join(OUT, cid + ".glb"))
for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    co = get_co(o)
    names = [g.name for g in o.vertex_groups]
    # verts without any weights
    nw = sum(1 for v in o.data.vertices if sum(g.weight for g in v.groups) < 0.5)
    log(o.name, "verts", len(co), "groups", len(names), "verts with weight sum < 0.5:", nw)
    for p in pts:
        d = np.linalg.norm(co - np.array(p), axis=1)
        idx = np.argsort(d)[:3]
        for i in idx:
            if d[i] < 0.06:
                v = o.data.vertices[int(i)]
                log("   near", p, "v", int(i), co[i].round(3), [(names[g.group], round(g.weight, 2)) for g in v.groups])
