import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, numpy as np
import monlib as M
M.reset_scene(); M.load_rig()
for kind in ('male', 'female'):
    b = M.load_body(kind)
    v = b.v
    hw = b.wsum(['Head'])
    hv = v[hw > 0.5]
    print(kind, "HEAD verts", len(hv), "bbox", hv.min(0).round(3), hv.max(0).round(3))
    print(" front profile (x~0): z, ymin ; half-width xmax ; back ymax")
    for z in np.arange(1.50, 1.80, 0.012):
        sl = v[(np.abs(v[:, 2] - z) < 0.007) & (np.abs(v[:, 0]) < 0.2) & (np.abs(v[:, 1]) < 0.2) & (hw + b.wsum(['neck_01']) > 0.3)]
        if len(sl):
            c = sl[np.abs(sl[:, 0]) < 0.012]
            print("   z=%.3f ymin=%.3f  xmax=%.3f ymax=%.3f  n=%d" % (z, c[:, 1].min() if len(c) else 9, sl[:, 0].max(), sl[:, 1].max(), len(sl)))
    # torso sections
    print(" torso sections: z, xmax(|y|<.2, torso weights), ymin, ymax")
    tw = b.wsum(['pelvis', 'spine_01', 'spine_02', 'spine_03', 'thigh_l', 'thigh_r'])
    for z in np.arange(0.85, 1.50, 0.05):
        sl = v[(np.abs(v[:, 2] - z) < 0.02) & (tw > 0.6)]
        if len(sl):
            print("   z=%.2f xmax=%.3f ymin=%.3f ymax=%.3f" % (z, sl[:, 0].max(), sl[:, 1].min(), sl[:, 1].max()))
    # limb radii
    for bn in ('upperarm_l', 'lowerarm_l', 'hand_l', 'thigh_l', 'calf_l', 'foot_l'):
        w = b.W[:, M.RIG.idx[bn]]
        sl = v[w > 0.7]
        print("  ", bn, "bbox", sl.min(0).round(3), sl.max(0).round(3))
    print("  tris", b.ntri)
