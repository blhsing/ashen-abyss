"""Town / lampknight / forms batch probe: hand frames in the clips these characters use (shield arm, lantern hand)."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_mel import *

c = start("male")
arm = c.arm
acts = load_actions()
tests = [("Idle_Shield_Loop", 0.5), ("Sword_Block", 0.5), ("Shield_Dash", 0.5), ("Shield_OneShot", 0.5), ("Shield_OneShot", 0.3),
         ("Sword_Regular_A", 0.55), ("Idle_Loop", 0.5), ("Jog_Fwd_Loop", 0.3), ("Idle_Lantern_Loop", 0.5), ("Idle_Torch_Loop", 0.5),
         ("Idle_Talking_Loop", 0.4), ("Idle_FoldArms_Loop", 0.5), ("Zombie_Idle_Loop", 0.5), ("Zombie_Scratch", 0.5),
         ("Sprint_Loop", 0.3), ("NinjaJump_Start", 0.5), ("Melee_Hook", 0.5), ("Punch_Cross", 0.45)]
fingers = ("index", "middle", "ring", "pinky")
AX = {s: hand_axes(s) for s in ("r", "l")}
for name, t in tests:
    if name not in acts:
        log("MISSING", name)
        continue
    set_pose(arm, acts[name], t)
    for side in ("l", "r"):
        pb = arm.pose.bones["hand_" + side]
        Mp = np.array([list(r) for r in pb.matrix])
        Mr = bone_frame("hand_" + side)
        D = Mp @ np.linalg.inv(Mr)              # rest space -> posed space
        Rm = D[:3, :3]
        o, along, dorsal, thumb = AX[side]
        curl = []
        for f in fingers:
            pts = [arm.pose.bones["%s_0%d_%s" % (f, i, side)].head for i in (1, 2, 3)]
            pts.append(arm.pose.bones["%s_03_%s" % (f, side)].tail)
            chain = sum((pts[i + 1] - pts[i]).length for i in range(3))
            curl.append((pts[3] - pts[0]).length / chain)
        la = arm.pose.bones["lowerarm_" + side]
        fa = np.array(la.tail - la.head)
        fa /= np.linalg.norm(fa)
        down_rest = Rm.T @ np.array((0, 0, -1.0))
        fwd_rest = Rm.T @ np.array((0, -1.0, 0))
        log("%-20s %.2f %s wrist=%s fore=%s along=%s dorsal=%s thumb=%s curl=%.2f | rest-space down=%s fwd=%s" % (
            name, t, side, np.round(Mp[:3, 3], 2), np.round(fa, 2), np.round(Rm @ along, 2), np.round(Rm @ dorsal, 2),
            np.round(Rm @ thumb, 2), sum(curl) / 4, np.round(down_rest, 3), np.round(fwd_rest, 3)))
    hd = arm.pose.bones["Head"]
    log("      head=%s pelvis=%s" % (np.round(np.array(hd.head), 2), np.round(np.array(arm.pose.bones["pelvis"].head), 2)))
for side in ("l", "r"):
    o, along, dorsal, thumb = AX[side]
    log("REST hand_%s o=%s along=%s dorsal=%s thumb=%s gripC=%s" % (side, np.round(o, 3), np.round(along, 2), np.round(dorsal, 2), np.round(thumb, 2),
                                                                    np.round(xf(np.array([GRIP_C[side]]), bone_frame("hand_" + side))[0], 3)))
