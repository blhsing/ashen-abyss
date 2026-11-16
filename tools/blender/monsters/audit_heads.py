"""Floating-part audit: head close-ups (three-quarter front + side) of exported creatures in rest pose, as contact sheets.
   blender -b --factory-startup --python audit_heads.py -- <id> [<id> ...] [--tag=name] [--res=px] [--pose=Action:t]
The crop is the bounding box of every vertex weighted > 0.5 to Head (so hats, crowns, teeth, eyes, horns are in frame).
Writes Y:\\shots\\chars\\_audit_<tag>.png (per creature: front tile, side tile)."""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S

args = sys.argv[sys.argv.index('--') + 1:]
ids = [a for a in args if not a.startswith('--')]
tag = 'heads'; res = 350; pose = None
for a in args:
    if a.startswith('--tag='):
        tag = a.split('=', 1)[1]
    if a.startswith('--res='):
        res = int(a.split('=', 1)[1])
    if a.startswith('--pose='):
        pose = a.split('=', 1)[1]
tiles = []
for cid in ids:
    M.reset_scene()
    acts = S.load_actions() if pose else None
    arm, meshes = S.import_creature(os.path.join(M.OUT_DIR, cid + ".glb"))
    S.setup_render(res, res, samples=16)
    S.setup_stage()
    cam = S.make_camera()
    if pose:
        a = pose.split(':')
        S.set_pose(arm, acts[a[0]], float(a[1]) if len(a) > 1 else 0.5)
    else:
        S.set_pose(arm, None, 0)
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for o in meshes:
        vg = o.vertex_groups.get('Head')
        if vg is None:
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        Mw = np.array(o.matrix_world)
        for v, ve in zip(o.data.vertices, me.vertices):
            for g in v.groups:
                if g.group == vg.index and g.weight > 0.5:
                    pts.append(Mw[:3, :3] @ np.array(ve.co) + Mw[:3, 3])
                    break
        ev.to_mesh_clear()
    P = np.array(pts)
    lo, hi = np.percentile(P, 2, axis=0), np.percentile(P, 98, axis=0)
    c = (lo + hi) / 2
    c[2] -= 0.02
    r = float(np.clip(np.max(hi - lo) * 0.55, 0.13, 0.30))
    box = np.array([c + r, c - r])
    for k, (yaw, pitch) in enumerate(((22, 4), (88, 2))):
        S.frame(cam, [], yaw, pitch, 70.0, pts=box)
        p = os.path.join(M.WORK, "_audit_%s_%d.png" % (cid, k))
        S.render(p)
        tiles.append(p)
    M.log("AUDIT", cid, "head box centre", np.round(c, 3), "r", round(r, 3))
S.combo(tiles, os.path.join(M.SHOT_DIR, "_audit_%s.png" % tag), cols=4)
M.log("AUDIT OK", tag)
