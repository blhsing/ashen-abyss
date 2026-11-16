import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, monlib as M, kit_ice as KI, kit_cloth as KC
M.reset_scene(); M.load_rig(); R = M.RIG
for b in ['root','pelvis','spine_01','spine_02','spine_03','neck_01','Head','clavicle_l','upperarm_l','lowerarm_l','hand_l','index_01_l','middle_03_l','thumb_01_l','thigh_l','calf_l','foot_l','ball_l']:
    M.log("BONE %-12s head %s tail %s" % (b, np.round(R.H(b), 3).tolist(), np.round(R.T(b), 3).tolist()))
for kind in ('male', 'female'):
    b = KC.ref_body(kind)
    M.log("BODY", kind, "verts", len(b.v), "tris", b.ntri, "bbox", np.round(b.v.min(0), 3).tolist(), np.round(b.v.max(0), 3).tolist())
    for z in (0.1, 0.3, 0.5, 0.7, 0.9, 1.0, 1.08, 1.2, 1.3, 1.4, 1.5, 1.56, 1.62, 1.7, 1.78):
        M.log("  girth z=%.2f cx,cy,rx,ry = %s" % (z, np.round(KI.girth(b, z, xlim=0.26), 3).tolist()))
    hv = b.v[b.wsum(['Head']) > 0.6]
    M.log("  head bbox", np.round(hv.min(0), 3).tolist(), np.round(hv.max(0), 3).tolist())
    hv = b.v[b.wsum(R.chain('hand_l')) > 0.6]
    M.log("  hand_l bbox", np.round(hv.min(0), 3).tolist(), np.round(hv.max(0), 3).tolist())
    hv = b.v[b.wsum(['foot_l', 'ball_l']) > 0.6]
    M.log("  foot_l bbox", np.round(hv.min(0), 3).tolist(), np.round(hv.max(0), 3).tolist())
    for bn, t in (('upperarm_l', 0.5), ('lowerarm_l', 0.5), ('lowerarm_l', 0.9), ('thigh_l', 0.5), ('calf_l', 0.5), ('calf_l', 0.9), ('neck_01', 0.5)):
        M.log("  limb radius %s t=%.1f: %.3f" % (bn, t, KI.limb_radius(b, bn, t)))
