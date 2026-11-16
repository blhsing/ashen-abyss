"""probe: rig joints + base body head stats (no output files besides the log)"""
import sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import bpy, numpy as np
import monlib as M

M.reset_scene()
arm = M.load_rig()
R = M.RIG
print("NB", R.nb)
for n in R.names:
    print("BONE %-16s parent=%-14s head=%s tail=%s" % (n, R.parent[n], np.round(R.head[n], 4), np.round(R.tail[n], 4)))
b = M.load_body('male')
print("BODY verts", len(b.v), "faces", len(b.f), "tris", b.ntri, "bbox", b.bbox())
hw = b.wsum(['Head'])
hm = hw > 0.5
print("HEAD verts", int(hm.sum()), "faces", int(b.vmask_faces(hm, 'all').sum()))
hv = b.v[hm]
print("HEAD bbox", hv.min(0), hv.max(0))
nw = b.wsum(['neck_01'])
print("NECK verts", int((nw > 0.5).sum()), b.v[nw > 0.5].min(0), b.v[nw > 0.5].max(0))
G = M.grip_frame('r')
print("GRIP", np.round(G, 4))
# widths of the head by height
for z in np.arange(1.54, 1.80, 0.02):
    s = hv[np.abs(hv[:, 2] - z) < 0.01]
    if len(s):
        print("z=%.2f  x[%.3f %.3f] y[%.3f %.3f]" % (z, s[:, 0].min(), s[:, 0].max(), s[:, 1].min(), s[:, 1].max()))
