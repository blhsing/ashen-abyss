"""Estimate the finger 'tunnel' (grip centre + axis) in hand-bone local space for key animations."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np

reset_scene()
arm = import_rig()
acts = load_actions()
tests = [("Sword_Idle", 0.5), ("Sword_Regular_A", 0.2), ("Sword_Regular_A", 0.8), ("Sword_Attack", 0.4), ("Sword_Block", 0.5),
         ("Sword_Heavy_Combo", 0.3), ("Spell_Simple_Idle_Loop", 0.5), ("Spell_Simple_Shoot", 0.4), ("Pistol_Aim_Neutral", 0.5),
         ("Pistol_Shoot", 0.3), ("Pistol_Idle_Loop", 0.5), ("Idle_Loop", 0.5), ("Jog_Fwd_Loop", 0.3), ("Idle_Torch_Loop", 0.5),
         ("Idle_Lantern_Loop", 0.5), ("Walk_Loop", 0.3), ("Idle_Talking_Loop", 0.3), ("Roll", 0.5)]
fingers = ("index", "middle", "ring", "pinky")
for name, t in tests:
    set_pose(arm, acts[name], t)
    for side in ("r", "l"):
        hb = arm.pose.bones["hand_" + side]
        inv = hb.matrix.inverted()
        cents = []
        curl = []
        for f in fingers:
            pts = [arm.pose.bones["%s_0%d_%s" % (f, i, side)].head for i in (1, 2, 3)]
            pts.append(arm.pose.bones["%s_03_%s" % (f, side)].tail)
            c = sum((inv @ p for p in pts), Vector()) / 4
            cents.append(c)
            # curl measure: distance knuckle->tip relative to chain length
            chain = sum((pts[i + 1] - pts[i]).length for i in range(3))
            curl.append((pts[3] - pts[0]).length / chain)
        C = sum(cents, Vector()) / 4
        A = (cents[0] - cents[3]).normalized()   # pinky -> index
        Aw = (hb.matrix.to_3x3() @ A).normalized()
        thumb_tip = inv @ arm.pose.bones["thumb_03_" + side].tail
        log("%-24s t=%.1f hand_%s curl=%.2f C=(%.3f %.3f %.3f) A=(%.2f %.2f %.2f) A_world=(%.2f %.2f %.2f) thumbtip=(%.3f %.3f %.3f)" % (
            name, t, side, sum(curl) / 4, C.x, C.y, C.z, A.x, A.y, A.z, Aw.x, Aw.y, Aw.z, thumb_tip.x, thumb_tip.y, thumb_tip.z))
# rest info
for side in ("r", "l"):
    b = arm.data.bones["hand_" + side]
    m = b.matrix_local
    log("REST hand_%s head=%s x=%s y=%s z=%s" % (side, tuple(round(v, 3) for v in b.head_local),
        tuple(round(v, 2) for v in m.col[0][:3]), tuple(round(v, 2) for v in m.col[1][:3]), tuple(round(v, 2) for v in m.col[2][:3])))
