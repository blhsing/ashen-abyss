"""Estimate where a held handle sits relative to the hand bones in the UAL weapon animations, and render the
mannequin with a test rod.  blender -b --factory-startup --python calib_grip.py"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, numpy as np
from mathutils import Vector, Matrix
import monlib as M, stage as S

M.reset_scene()
objs = M.import_gltf(M.UAL1)
arm = next(o for o in objs if o.type == 'ARMATURE')
man = next(o for o in objs if o.type == 'MESH' and o.name == 'Mannequin')
for o in objs:
    if o not in (arm, man):
        bpy.data.objects.remove(o, do_unlink=True)
for pb in arm.pose.bones:
    pb.custom_shape = None
acts = {}
for a in bpy.data.actions:
    acts[a.name] = a
before = set(bpy.data.actions)
o2 = M.import_gltf(M.UAL2)
for o in o2:
    bpy.data.objects.remove(o, do_unlink=True)
for a in bpy.data.actions:
    if a not in before and a.name not in acts:
        acts[a.name.split('.')[0]] = a
rig = M.Rig(arm)


def est(side, action, t):
    S.set_pose(arm, acts[action], t)
    pb = arm.pose.bones
    cs = []
    for fg in ('index', 'middle', 'ring', 'pinky'):
        pts = [np.array(pb['%s_0%d_%s' % (fg, k, side)].head) for k in (1, 2, 3)]
        pts.append(np.array(pb['%s_03_%s' % (fg, side)].tail))
        cs.append(np.mean(pts, axis=0))
    cs = np.array(cs)
    c = cs.mean(0)
    u, s, vt = np.linalg.svd(cs - c)
    ax = vt[0]
    if np.dot(ax, cs[0] - cs[3]) < 0:
        ax = -ax
    Hm = np.array(pb['hand_' + side].matrix)     # armature space pose matrix
    Hr = rig.mat['hand_' + side]
    loc = np.linalg.inv(Hm) @ np.r_[c, 1]
    lax = np.linalg.inv(Hm)[:3, :3] @ ax
    rest_c = (Hr @ loc)[:3]
    rest_ax = Hr[:3, :3] @ lax
    # curl measure
    return rest_c, rest_ax


for side, lst in (('r', [('Sword_Idle', 0.3), ('Sword_Attack', 0.2), ('Sword_Attack', 0.5), ('Sword_Regular_A', 0.5),
                         ('Sword_Regular_B', 0.5), ('Sword_Block', 0.5), ('OverhandThrow', 0.3), ('Idle_Torch_Loop', 0.3),
                         ('Idle_Lantern_Loop', 0.3), ('Zombie_Idle_Loop', 0.3), ('Spell_Simple_Idle_Loop', 0.3)]),
                  ('l', [('Sword_Idle', 0.3), ('Sword_Attack', 0.5), ('Idle_Torch_Loop', 0.3), ('Idle_Lantern_Loop', 0.3),
                         ('Spell_Simple_Idle_Loop', 0.3), ('Idle_Shield_Loop', 0.3), ('Sword_Block', 0.5)])):
    for act, t in lst:
        c, ax = est(side, act, t)
        print("GRIP %s %-24s t=%.1f  centre(rest)=%s  axis(rest, pinky->index)=%s" % (
            side, act, t, np.round(c, 4), np.round(ax, 3)))

# test rod on the right hand using the Sword_Idle estimate
c, ax = est('r', 'Sword_Idle', 0.3)
G = c
rod = M.tube([G - ax * 0.12, G + ax * 0.75], 0.016, n=8)
guard = M.box(0.02, 0.02, 0.16).orient(ax, (0, 0, 1), G + ax * 0.09)
M.RIG = rig
ob = M._temp_object(M.join([rod, guard]), 'rod')
vg = ob.vertex_groups.new(name='hand_r'); vg.add(list(range(len(ob.data.vertices))), 1.0, 'REPLACE')
ob.parent = arm
md = ob.modifiers.new("Armature", 'ARMATURE'); md.object = arm
m = bpy.data.materials.new("rodm"); m.use_nodes = True
m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.8, 0.2, 0.1, 1)
ob.data.materials.append(m)

S.setup_render(700, 700)
S.setup_stage()
cam = S.make_camera()
paths = []
for i, (act, t, yaw, pitch) in enumerate((("Sword_Idle", 0.3, 20, 10), ("Sword_Attack", 0.45, 30, 15),
                                           ("Sword_Regular_A", 0.5, 20, 15), ("OverhandThrow", 0.35, 40, 10),
                                           ("Spell_Simple_Idle_Loop", 0.3, 20, 10), ("Idle_Lantern_Loop", 0.3, 20, 10))):
    S.set_pose(arm, acts[act], t)
    S.frame(cam, [man, ob], yaw, pitch)
    p = os.path.join(M.WORK, "grip_%d.png" % i)
    S.render(p); paths.append(p)
S.combo(paths, os.path.join(M.SHOT_DIR, "_dbg_grip.png"), cols=3)
# close-up on the hand
paths = []
for i, (act, t, yaw, pitch) in enumerate((("Sword_Idle", 0.3, 60, 5), ("Sword_Idle", 0.3, -60, 20), ("Sword_Attack", 0.45, 100, 10))):
    S.set_pose(arm, acts[act], t)
    h = np.array(arm.pose.bones['hand_r'].head)
    pts = np.array([h + d for d in ((0.2, 0.2, 0.2), (-0.2, -0.2, -0.2))])
    S.frame(cam, [], yaw, pitch, pts=pts)
    p = os.path.join(M.WORK, "gripc_%d.png" % i)
    S.render(p); paths.append(p)
S.combo(paths, os.path.join(M.SHOT_DIR, "_dbg_grip_close.png"), cols=3)
