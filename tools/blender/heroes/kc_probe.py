"""Caster batch probe: hand curl + where the fist axis / palm normal point in the clips the caster heroes use."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np
from mathutils import Vector

reset_scene()
arm = import_rig()
acts = load_actions()
tests = [("Spell_Simple_Idle_Loop", 0.5), ("Spell_Simple_Enter", 0.5), ("Spell_Simple_Shoot", 0.2), ("Spell_Simple_Shoot", 0.45),
         ("Spell_Simple_Shoot", 0.8), ("OverhandThrow", 0.25), ("OverhandThrow", 0.5), ("OverhandThrow", 0.75), ("Sword_Idle", 0.5),
         ("Interact", 0.3), ("Interact", 0.5), ("Idle_Loop", 0.5), ("Jog_Fwd_Loop", 0.3), ("Walk_Loop", 0.3), ("Roll", 0.45),
         ("Sword_Regular_A", 0.55)]
GA = {"r": Vector((-0.04, 0.17, 0.99)).normalized(), "l": Vector((0.04, 0.17, 0.99)).normalized()}
fingers = ("index", "middle", "ring", "pinky")
for name, t in tests:
    if name not in acts:
        log("MISSING", name)
        continue
    set_pose(arm, acts[name], t)
    for side in ("r", "l"):
        hb = arm.pose.bones["hand_" + side]
        R3 = hb.matrix.to_3x3()
        curl = []
        for f in fingers:
            pts = [arm.pose.bones["%s_0%d_%s" % (f, i, side)].head for i in (1, 2, 3)] + [arm.pose.bones["%s_03_%s" % (f, side)].tail]
            chain = sum((pts[i + 1] - pts[i]).length for i in range(3))
            curl.append((pts[3] - pts[0]).length / chain)
        ax = (R3 @ GA[side]).normalized()            # fist tunnel axis (pinky -> index) in world
        fw = (R3 @ Vector((0, 1, 0))).normalized()   # along the hand (wrist -> knuckles)
        pn = (R3 @ Vector((1, 0, 0))).normalized()   # hand local X
        h = hb.head
        log("%-24s t=%.2f %s curl=%.2f head=(%+.2f %+.2f %.2f) axis=(%+.2f %+.2f %+.2f) fwd=(%+.2f %+.2f %+.2f) lx=(%+.2f %+.2f %+.2f)" % (
            name, t, side, sum(curl) / 4, h.x, h.y, h.z, ax.x, ax.y, ax.z, fw.x, fw.y, fw.z, pn.x, pn.y, pn.z))
for side in ("r", "l"):
    b = arm.data.bones["hand_" + side]
    m = b.matrix_local
    log("REST hand_%s head=%s x=%s y=%s z=%s" % (side, tuple(round(v, 3) for v in b.head_local),
        tuple(round(v, 2) for v in m.col[0][:3]), tuple(round(v, 2) for v in m.col[1][:3]), tuple(round(v, 2) for v in m.col[2][:3])))
for bn in ("Head", "neck_01", "spine_03", "spine_02", "spine_01", "pelvis", "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l", "thigh_l",
           "calf_l", "foot_l"):
    b = arm.data.bones[bn]
    log("REST %-12s head=(%+.3f %+.3f %.3f) tail=(%+.3f %+.3f %.3f)" % (bn, *b.head_local, *b.tail_local))
log("ACTIONS", ", ".join(sorted(acts.keys())))
