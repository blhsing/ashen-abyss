"""diagnose stretched geometry: edges much longer in a pose than at rest.
   blender -b --factory-startup --python dbg/stretch.py -- <id> <Action:t> [ratio]"""
import sys, os
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S
args = sys.argv[sys.argv.index('--') + 1:]
cid, pose = args[0], args[1]
ratio = float(args[2]) if len(args) > 2 else 3.0
M.reset_scene()
acts = S.load_actions()
arm, meshes = S.import_creature(os.path.join(M.OUT_DIR, cid + ".glb"))
a, _, t = pose.partition(':')
for o in meshes:
    me = o.data
    rest = np.array([v.co[:] for v in me.vertices])
    S.set_pose(arm, acts[a], float(t))
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg); m2 = ev.to_mesh()
    posed = np.array([v.co[:] for v in m2.vertices])
    ev.to_mesh_clear()
    S.set_pose(arm, None, 0)
    E = np.array([e.vertices[:] for e in me.edges])
    l0 = np.linalg.norm(rest[E[:, 0]] - rest[E[:, 1]], axis=1); l1 = np.linalg.norm(posed[E[:, 0]] - posed[E[:, 1]], axis=1)
    bad = np.where((l1 > ratio * np.maximum(l0, 1e-4)) & (l1 > 0.05))[0]
    M.log("STRETCH", o.name, "edges", len(E), "bad", len(bad))
    gn = {g.index: g.name for g in o.vertex_groups}
    seen = set()
    for e in bad[:40]:
        for vi in E[e]:
            if vi in seen:
                continue
            seen.add(vi)
            v = me.vertices[vi]
            M.log("  v%d rest %s posed %s  %s" % (vi, np.round(rest[vi], 3), np.round(posed[vi], 3), [(gn[g.group], round(g.weight, 2)) for g in v.groups]))
