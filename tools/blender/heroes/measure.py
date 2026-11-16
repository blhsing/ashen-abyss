"""Print measurements of the fitted base bodies (armature space, Z up, facing -Y)."""
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import mk
import numpy as np

for kind, fn, bn in (("male", "Superhero_Male_FullBody.gltf", "SuperHero_Male"), ("female", "Superhero_Female_FullBody.gltf", "Superhero_Female")):
    reset_scene()
    arm = import_rig()
    mk.begin(arm)
    parts = import_skinned(os.path.join(SRC, "base", fn), arm)
    B = mk.Body(parts[bn])
    log("=====", kind, "verts", len(B.co), "bbox", B.co.min(0).round(3), B.co.max(0).round(3))
    for z in (0.86, 0.92, 0.98, 1.04, 1.10, 1.16, 1.22, 1.28, 1.34, 1.40, 1.46, 1.50, 1.54):
        s = B.slice(z)
        if s:
            log("torso z=%.2f cx=%.3f cy=%.3f rx=%.3f ryf=%.3f ryb=%.3f" % ((z,) + tuple(s)))
    # head
    hm = B.w("Head") > 0.9
    log("head bbox", B.co[hm].min(0).round(3), B.co[hm].max(0).round(3))
    for z in (1.58, 1.62, 1.66, 1.70, 1.74):
        m = hm & (np.abs(B.z - z) < 0.012)
        if m.any():
            log(" head z=%.2f x[%.3f %.3f] y[%.3f %.3f]" % (z, B.x[m].min(), B.x[m].max(), B.y[m].min(), B.y[m].max()))
    nm = (B.w("neck_01") > 0.5)
    log("neck bbox", B.co[nm].min(0).round(3), B.co[nm].max(0).round(3))
    # arm sections (left)
    for x in (0.22, 0.28, 0.34, 0.40, 0.46, 0.52, 0.58, 0.64, 0.70, 0.74):
        m = (np.abs(B.x - x) < 0.012) & (B.w(*mk.BONES_ARM, *mk.BONES_HAND) > 0.5)
        if m.any():
            log(" arm x=%.2f y[%.3f %.3f] z[%.3f %.3f]" % (x, B.y[m].min(), B.y[m].max(), B.z[m].min(), B.z[m].max()))
    hm = (B.w(*mk.BONES_HAND) > 0.9) & (B.x > 0)
    log("hand_l bbox", B.co[hm].min(0).round(3), B.co[hm].max(0).round(3))
    for z in (0.85, 0.78, 0.70, 0.62, 0.54, 0.46, 0.38, 0.30, 0.22, 0.14, 0.08):
        m = (np.abs(B.z - z) < 0.012) & (B.x > 0) & (B.w(*mk.BONES_LEG, *mk.BONES_FOOT, "pelvis") > 0.5)
        if m.any():
            log(" leg z=%.2f x[%.3f %.3f] y[%.3f %.3f]" % (z, B.x[m].min(), B.x[m].max(), B.y[m].min(), B.y[m].max()))
    fm = (B.w(*mk.BONES_FOOT) > 0.9) & (B.x > 0)
    log("foot_l bbox", B.co[fm].min(0).round(3), B.co[fm].max(0).round(3))
