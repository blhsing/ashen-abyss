"""Fork C probe: where do rest-space axes of the left forearm / hands point in the shield + punch clips."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np
from mathutils import Vector

reset_scene()
arm = import_rig()
acts = load_actions()
tests = [("Idle_Shield_Loop", 0.5), ("Sword_Block", 0.5), ("Shield_Dash", 0.5), ("Shield_OneShot", 0.5), ("Sword_Regular_A", 0.55),
         ("Jog_Fwd_Loop", 0.3), ("Idle_Loop", 0.5), ("Sword_Idle", 0.5), ("Idle_Shield_Break", 0.5),
         ("Punch_Jab", 0.45), ("Punch_Cross", 0.45), ("Melee_Hook", 0.5), ("TreeChopping_Loop", 0.4), ("Idle_FoldArms_Loop", 0.5)]
fingers = ("index", "middle", "ring", "pinky")
for name, t in tests:
    set_pose(arm, acts[name], t)
    for bn in ("lowerarm_l", "hand_l", "hand_r"):
        pb = arm.pose.bones[bn]
        M = pb.matrix @ arm.data.bones[bn].matrix_local.inverted()
        R3 = M.to_3x3()
        up = R3 @ Vector((0, 0, 1))
        fw = R3 @ Vector((0, -1, 0))
        out = R3 @ Vector((1, 0, 0))
        curl = []
        if bn.startswith("hand"):
            side = bn[-1]
            for f in fingers:
                pts = [arm.pose.bones["%s_0%d_%s" % (f, i, side)].head for i in (1, 2, 3)] + [arm.pose.bones["%s_03_%s" % (f, side)].tail]
                chain = sum((pts[i + 1] - pts[i]).length for i in range(3))
                curl.append((pts[3] - pts[0]).length / chain)
        log("%-18s %-10s head=(%.2f %.2f %.2f) restUp->(%.2f %.2f %.2f) restFwd->(%.2f %.2f %.2f) restX->(%.2f %.2f %.2f) %s" % (
            name, bn, pb.head.x, pb.head.y, pb.head.z, up.x, up.y, up.z, fw.x, fw.y, fw.z, out.x, out.y, out.z,
            ("curl=%.2f" % (sum(curl) / 4)) if curl else ""))
