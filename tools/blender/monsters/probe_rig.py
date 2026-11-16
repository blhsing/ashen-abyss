"""print the UAL rest joints and a few base-body statistics.  blender -b --factory-startup --python probe_rig.py"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, numpy as np
import monlib as M
M.reset_scene(); M.load_rig()
R = M.RIG
for n in R.names:
    print("BONE %-16s parent %-14s head %s tail %s" % (n, R.parent[n], np.round(R.head[n], 3), np.round(R.tail[n], 3)))
print("HAND_R MAT", np.round(R.mat['hand_r'], 3).tolist())
print("HAND_L MAT", np.round(R.mat['hand_l'], 3).tolist())
print("GRIP_R", np.round(M.grip_frame('r'), 3).tolist())
for kind in ('male', 'female'):
    b = M.load_body(kind)
    print("BODY", kind, "tris", b.ntri, "verts", len(b.v))
    for nm, bones in (('head', ['Head']), ('neck', ['neck_01']), ('hand_l', R.chain('hand_l')), ('arm_l', ['upperarm_l', 'lowerarm_l']),
                      ('legs', R.chain('thigh_l') + R.chain('thigh_r')), ('torso', ['pelvis', 'spine_01', 'spine_02', 'spine_03'])):
        m = b.wsum(bones) > 0.5
        s = b.sub(b.vmask_faces(m, 'all'))
        print("   part %-8s tris %5d bbox %s %s" % (nm, s.ntri, np.round(s.v.min(0), 3), np.round(s.v.max(0), 3)))
    v = b.v
    hw = b.wsum(R.chain('hand_l'))
    hv = v[hw > 0.6]
    print("   hand_l verts bbox", np.round(hv.min(0), 3), np.round(hv.max(0), 3))
    # palm normal estimate: average normal of hand verts with lowest z
    n = b.normals()[hw > 0.6]
    print("   hand_l mean normal of lower verts", np.round(n[hv[:, 2] < np.median(hv[:, 2])].mean(0), 2))
print("PROBE DONE")
