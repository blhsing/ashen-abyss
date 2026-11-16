"""Probe for the javelin grip: how the right hand moves in OverhandThrow, and renders of the mannequin with
candidate javelin axes.  blender -b --factory-startup --python probe_throw.py [-- render]"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, numpy as np
from mathutils import Vector, Matrix
import monlib as M, stage as S

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
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
M.RIG = rig
print("ACTIONS", sorted(acts))
Hr = rig.mat['hand_r']


def hand(act, t):
    S.set_pose(arm, acts[act], t)
    pb = arm.pose.bones
    Hm = np.array(pb['hand_r'].matrix)
    cs = []
    for fg in ('index', 'middle', 'ring', 'pinky'):
        pts = [np.array(pb['%s_0%d_r' % (fg, k)].head) for k in (1, 2, 3)]
        pts.append(np.array(pb['%s_03_r' % fg].tail))
        cs.append(np.mean(pts, axis=0))
    cs = np.array(cs)
    c = cs.mean(0)
    tipd = np.linalg.norm(np.array(pb['middle_03_r'].tail) - np.array(pb['hand_r'].head))
    fore = M.unit(np.array(pb['hand_r'].head) - np.array(pb['lowerarm_r'].head))
    return Hm, c, tipd, fore


def to_rest(Hm, d):
    return Hr[:3, :3] @ (np.linalg.inv(Hm)[:3, :3] @ np.asarray(d, dtype=float))


for act in ('OverhandThrow',):
    a = acts[act]
    print("ACT", act, "frames", tuple(a.frame_range))
    ts = np.linspace(0, 1, 41)
    pos = []; mats = []
    for t in ts:
        Hm, c, tipd, fore = hand(act, t)
        pos.append(Hm[:3, 3].copy()); mats.append((Hm.copy(), c, tipd, fore))
    pos = np.array(pos)
    vel = np.gradient(pos, axis=0)
    sp = np.linalg.norm(vel, axis=1)
    for i, t in enumerate(ts):
        Hm, c, tipd, fore = mats[i]
        fwd = to_rest(Hm, (0, -1, 0))
        up = to_rest(Hm, (0, 0, 1))
        vd = to_rest(Hm, M.unit(vel[i])) if sp[i] > 1e-5 else np.zeros(3)
        fr = to_rest(Hm, fore)
        print("T %.3f pos %s speed %.3f tipd %.3f | fwd_rest %s | vel_rest %s | forearm_rest %s" % (
            t, np.round(pos[i], 2), sp[i], tipd, np.round(fwd, 2), np.round(vd, 2), np.round(fr, 2)))

if 'render' in args:
    cands = {}
    for s in args:
        if s.startswith('axis='):
            nm, vals = s[5:].split(':')
            cands[nm] = M.unit([float(x) for x in vals.split(',')])
    G = M.grip_frame('r')[:3, 3]
    cols = dict(a=(0.9, 0.15, 0.1), b=(0.1, 0.8, 0.2), c=(0.15, 0.3, 1.0), d=(0.9, 0.8, 0.1))
    rods = []
    for nm, ax in cands.items():
        rod = M.tube([G - ax * 0.55, G + ax * 0.72], 0.012, n=6)
        tip = M.cone_spike(0.03, 0.14, n=6)
        tip.tf(M.rot_to((0, 0, 1), ax)).move(G + ax * 0.72)
        ob = M._temp_object(M.join([rod, tip]), 'rod_' + nm)
        vg = ob.vertex_groups.new(name='hand_r'); vg.add(list(range(len(ob.data.vertices))), 1.0, 'REPLACE')
        ob.parent = arm
        md = ob.modifiers.new("Armature", 'ARMATURE'); md.object = arm
        m = bpy.data.materials.new("rodm" + nm); m.use_nodes = True
        m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = cols.get(nm, (1, 1, 1)) + (1,)
        ob.data.materials.append(m)
        rods.append(ob)
    S.setup_render(520, 520, samples=8)
    S.setup_stage()
    cam = S.make_camera()
    paths = []
    shots = [("OverhandThrow", 0.10, 60, 12), ("OverhandThrow", 0.22, 60, 12), ("OverhandThrow", 0.30, 60, 12), ("OverhandThrow", 0.38, 60, 12),
             ("OverhandThrow", 0.46, 60, 12), ("OverhandThrow", 0.6, 60, 12), ("Jog_Fwd_Loop", 0.3, 40, 12), ("Idle_Loop", 0.3, 30, 12),
             ("Sword_Idle", 0.3, 30, 12)]
    for s in args:
        if s.startswith('shots='):
            shots = []
            for q in s[6:].split(';'):
                a_, t_, y_, p_ = q.split(':')
                shots.append((a_, float(t_), float(y_), float(p_)))
    for i, (act, t, yaw, pitch) in enumerate(shots):
        S.set_pose(arm, acts[act], t)
        S.frame(cam, [man] + rods, yaw, pitch)
        p = os.path.join(M.WORK, "throw_%d.png" % i)
        S.render(p); paths.append(p)
    S.combo(paths, os.path.join(M.SHOT_DIR, "_explore", "probe_throw.png"), cols=3)
print("PROBE DONE")
