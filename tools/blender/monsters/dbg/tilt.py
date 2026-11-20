"""How far do the spine bones tip in the clips the giants use?  (decides which bone a carried load is bound to)
   blender -b --factory-startup --python dbg/tilt.py -- <Clip> [<Clip> ...]
Per clip: the largest tilt from upright (deg) of pelvis / spine_01 / spine_02 / spine_03 / Head over the clip, the largest
twist about the vertical, and how far the left forearm swings (fore-aft angle range of lowerarm_l)."""
import sys, os, math
HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S

clips = sys.argv[sys.argv.index('--') + 1:]
M.reset_scene()
acts = S.load_actions()
arm, meshes = S.import_creature(os.path.join(M.OUT_DIR, "mon_husk.glb"))
BONES = ('pelvis', 'spine_01', 'spine_02', 'spine_03', 'Head')
rest = {b: np.array(arm.data.bones[b].matrix_local)[:3, :3] for b in BONES + ('lowerarm_l', 'upperarm_l')}
for c in clips:
    if c not in acts:
        M.log("TILT missing clip", c)
        continue
    tilt = {b: 0.0 for b in BONES}
    twist = {b: 0.0 for b in BONES}
    fa = []
    for t in np.linspace(0.0, 0.98, 25):
        S.set_pose(arm, acts[c], float(t))
        for b in BONES:
            D = np.array(arm.pose.bones[b].matrix)[:3, :3] @ rest[b].T          # armature-space delta rotation
            up = D @ np.array([0.0, 0.0, 1.0])
            tilt[b] = max(tilt[b], math.degrees(math.acos(max(-1.0, min(1.0, up[2])))))
            fw = D @ np.array([0.0, -1.0, 0.0])
            twist[b] = max(twist[b], abs(math.degrees(math.atan2(fw[0], -fw[1]))))
        ax = np.array(arm.pose.bones['lowerarm_l'].matrix)[:3, 1]              # bone Y axis = along the forearm
        fa.append(math.degrees(math.atan2(-ax[1], -ax[2])))                     # fore-aft angle from straight down
    M.log("TILT %-22s tilt %s | twist %s | forearm_l fore-aft %.0f..%.0f" % (
        c, " ".join("%s %2.0f" % (b[-8:], tilt[b]) for b in BONES), " ".join("%2.0f" % twist[b] for b in BONES), min(fa), max(fa)))
