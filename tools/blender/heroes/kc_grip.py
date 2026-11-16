"""Caster batch: evaluate hand-held frames (staff / wand / book) in the clips the heroes use.
For each candidate weapon frame G (weapon-local -> armature rest space, rigid to a hand) prints, per pose, the world
direction of the weapon +Z / +X axes, the grip position and the clearance of the shaft from head and torso."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from charlib import *

reset_scene()
arm = import_rig()
mk.begin(arm)
acts = load_actions()
POSES = [("Spell_Simple_Idle_Loop", 0.5), ("Spell_Simple_Shoot", 0.45), ("Idle_Loop", 0.5), ("Walk_Loop", 0.3), ("Jog_Fwd_Loop", 0.3),
         ("Jog_Fwd_Loop", 0.8), ("Sword_Idle", 0.5), ("OverhandThrow", 0.25), ("OverhandThrow", 0.5), ("Interact", 0.5), ("Roll", 0.45)]


def seg_dist(p, a, b):
    ab = b - a
    t = np.clip(np.dot(p - a, ab) / np.dot(ab, ab), 0, 1)
    return float(np.linalg.norm(p - (a + ab * t)))


def report(label, G, side="r", z0=-0.75, z1=1.0):
    hb = "hand_" + side
    rest_inv = np.linalg.inv(bone_frame(hb))
    log("== %s" % label)
    for name, t in POSES:
        set_pose(arm, acts[name], t)
        P = np.array([list(r) for r in arm.pose.bones[hb].matrix])
        M = P @ rest_inv @ G
        o, zx, xx = M[:3, 3], M[:3, 2], M[:3, 0]
        a, b = o + zx * z0, o + zx * z1
        head = np.array(arm.pose.bones["Head"].head) + np.array((0, 0, 0.09))
        chest = np.array(arm.pose.bones["spine_03"].head)
        hip = np.array(arm.pose.bones["pelvis"].head)
        kn = min(seg_dist(np.array(arm.pose.bones[k].head), a, b) for k in ("calf_l", "calf_r"))
        log("   %-22s %.2f grip=(%+.2f %+.2f %.2f) Z=(%+.2f %+.2f %+.2f) X=(%+.2f %+.2f %+.2f) top=(%+.2f %+.2f %.2f) butt_z=%.2f d_head=%.2f d_chest=%.2f d_hip=%.2f d_knee=%.2f" % (
            name, t, o[0], o[1], o[2], zx[0], zx[1], zx[2], xx[0], xx[1], xx[2], b[0], b[1], b[2], a[2],
            seg_dist(head, a, b), seg_dist(chest, a, b), seg_dist(hip, a, b), kn))


args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
mode = args[0] if args else "staff"
if mode == "staff":
    for tilt in (24, 38, 50):
        for lean in (-25, 0, 18, 32):
            for roll in (0,):
                G = grip("r", tilt=tilt) @ R('X', lean)
                report("staff tilt=%d lean=%d" % (tilt, lean), G)
elif mode == "staff2":
    for spec in args[1:]:
        tilt, lean, roll = [float(v) for v in spec.split(",")]
        G = grip("r", tilt=tilt, roll=roll) @ R('X', lean)
        report("staff tilt=%g lean=%g roll=%g" % (tilt, lean, roll), G)
elif mode == "left":
    for spec in args[1:]:
        tilt, lean, roll = [float(v) for v in spec.split(",")]
        G = grip("l", tilt=tilt, roll=roll) @ R('X', lean)
        report("left tilt=%g lean=%g roll=%g" % (tilt, lean, roll), G, side="l", z0=-0.1, z1=0.3)
