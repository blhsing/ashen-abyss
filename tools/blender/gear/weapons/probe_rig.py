"""Probe: grip frames (bone-local), shield mount on lowerarm_l, list of clips, where 'down' points in the weapon frame."""
import bpy, sys, os, math
sys.path.insert(0, r"Y:\tools\blender\heroes")
import rigio
rigio._LOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_probe.log")
from rigio import *
import numpy as np
import mk
from mk import T, R

reset_scene()
arm = import_rig()
mk.begin(arm)
import charlib
from charlib import grip, GRIP_C, GRIP_A, bone_frame
import kit_twn

def fmt(M):
    return "[" + ", ".join("[" + ", ".join("%.5f" % v for v in r) + "]" for r in np.asarray(M)) + "]"

out = {}
for side in ("r", "l"):
    H = bone_frame("hand_" + side)
    G = grip(side)
    L = np.linalg.inv(H) @ G
    log("GRIP hand_%s bone-local L =" % side, fmt(L))
    out["grip_" + side] = L.tolist()
    Fb = bone_frame("lowerarm_" + side)
    log("lowerarm_%s len=%.4f  hand head in lowerarm local = %s" % (side, arm.data.bones["lowerarm_" + side].length,
        np.round(np.linalg.inv(Fb) @ np.append(H[:3, 3], 1.0), 4)))
# shield frame (new contract): origin on the forearm axis at the strap, x elbow->knuckles, y dorsal, z thumb
o, along, dorsal, thumb = kit_twn.hand_axes("l")
S = np.eye(4)
x = np.cross(dorsal, thumb)
S[:3, 0], S[:3, 1], S[:3, 2] = x, dorsal, thumb
S[:3, 3] = o - along * 0.155
Fb = bone_frame("lowerarm_l")
M = np.linalg.inv(Fb) @ S
log("SHIELD lowerarm_l bone-local M =", fmt(M))
out["shield_lowerarm_l"] = M.tolist()
log("hand_axes l: o=%s along=%s dorsal=%s thumb=%s x=%s" % (np.round(o, 3), np.round(along, 3), np.round(dorsal, 3), np.round(thumb, 3), np.round(x, 3)))
acts = load_actions()
log("ACTIONS", len(acts), sorted(acts.keys()))
tests = ["Idle_Loop", "Walk_Loop", "Jog_Fwd_Loop", "Sprint_Loop", "Sword_Idle", "Sword_Regular_A", "Sword_Block", "Idle_Shield_Loop",
         "Spell_Simple_Idle_Loop", "Spell_Simple_Shoot", "Idle_Torch_Loop", "Idle_Lantern_Loop", "Pistol_Idle_Loop", "Pistol_Aim_Neutral"]
for name in tests:
    if name not in acts:
        log("missing clip", name)
        continue
    for t in (0.25, 0.75):
        set_pose(arm, acts[name], t)
        bpy.context.view_layer.update()
        row = []
        for side in ("r", "l"):
            hb = arm.pose.bones["hand_" + side]
            Hp = np.array([list(r) for r in hb.matrix])
            Lg = np.array(out["grip_" + side])
            W = Hp @ Lg
            down = W[:3, :3].T @ np.array((0, 0, -1.0))
            fwd = W[:3, :3].T @ np.array((0, -1.0, 0))
            pos = W[:3, 3]
            row.append("%s: down=(%.2f %.2f %.2f) fwd=(%.2f %.2f %.2f) pos=(%.2f %.2f %.2f)" % (side, *down, *fwd, *pos))
        log("%-24s t=%.2f  %s" % (name, t, "  |  ".join(row)))
import json
with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "mount_frames.json"), "w") as f:
    json.dump(out, f, indent=1)
