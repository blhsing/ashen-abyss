"""Cache the Quaternius Superhero base bodies re-fitted to the UAL rest pose (their own armature has slightly
different proportions).  v' = sum_b w_b * (R_ual_b * R_src_b^-1) v   ->  cache/body_<kind>.npz
Also renders a sanity preview.   blender -b --factory-startup --python cache_body.py
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, numpy as np
import monlib as M, stage as S


def grab(kind, path):
    M.reset_scene()
    arm = M.load_rig(); rig = M.RIG
    objs = M.import_gltf(path)
    oarm = next(o for o in objs if o.type == 'ARMATURE')
    ob = next(o for o in objs if o.type == 'MESH' and o.name.lower().startswith('superhero'))
    v, f, W = M._read_mesh(ob.data, ob.vertex_groups)
    Mw = np.array(ob.matrix_world)
    v = v @ Mw[:3, :3].T + Mw[:3, 3]
    p = M.Part(v, f, W)
    M.normalize_weights(p, k=8)
    out = np.zeros_like(p.v)
    vh = np.c_[p.v, np.ones(len(p.v))]
    for i, b in enumerate(rig.names):
        if p.W[:, i].max() <= 0:
            continue
        Sb = rig.mat[b] @ np.linalg.inv(np.array(oarm.data.bones[b].matrix_local))
        out += p.W[:, i:i + 1] * (vh @ Sb.T)[:, :3]
    p.v = out
    n0 = len(p.v)
    p.cleanup(eps=2e-5)
    # ground the feet
    zmin = p.v[:, 2].min()
    M.log(kind, "verts", n0, "->", len(p.v), "tris", p.ntri, "zmin", zmin, "bbox", p.bbox())
    fl = np.array([len(x) for x in p.f], dtype=np.int32)
    ff = np.array([i for x in p.f for i in x], dtype=np.int32)
    np.savez_compressed(os.path.join(M.CACHE, 'body_%s.npz' % kind), v=p.v, W=p.W, faces_len=fl, faces_flat=ff,
                        bones=np.array(rig.names))
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    return arm, p


def preview(kind, arm, p):
    ob = M._temp_object(p, 'body')
    for pl in ob.data.polygons:
        pl.use_smooth = True
    ob.parent = arm
    md = ob.modifiers.new("Armature", 'ARMATURE'); md.object = arm
    m = bpy.data.materials.new("clay"); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.55, 0.5, 0.45, 1)
    ob.data.materials.append(m)
    acts = S.load_actions()
    S.setup_render(700, 700)
    S.setup_stage()
    cam = S.make_camera()
    paths = []
    for name, act, t, yaw, pitch in (("rest", None, 0, 25, 10), ("idle", "Zombie_Idle_Loop", 0.3, 25, 10),
                                     ("atk", "Sword_Attack", 0.45, 30, 15), ("walk", "Zombie_Walk_Fwd_Loop", 0.3, -35, 12)):
        S.set_pose(arm, acts[act] if act else None, t)
        S.frame(cam, [ob], yaw, pitch)
        pth = os.path.join(M.WORK, "body_%s_%s.png" % (kind, name))
        S.render(pth); paths.append(pth)
    S.combo(paths, os.path.join(M.SHOT_DIR, "_dbg_body_%s.png" % kind))


for kind, fn in (("male", "base/Superhero_Male_FullBody.gltf"), ("female", "base/Superhero_Female_FullBody.gltf")):
    arm, p = grab(kind, os.path.join(M.SRC, fn))
    preview(kind, arm, p)
