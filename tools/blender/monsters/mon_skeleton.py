"""mon_skeleton 骸骨兵 — articulated skeleton, dented kettle cap, one pauldron, notched rusty sword."""
import math
import numpy as np
import monlib as M
import kit_bones as KB
from monlib import V, tube, ellipsoid, box, revolve, join, blade, hang, band, arc_pts


def sword(C, look_blade='blade', look_hilt='rust', look_grip='strap', obj='wpn_sword', length=0.80):
    grip = tube([(0, -0.072, 0), (0, -0.02, 0), (0, 0.058, 0)], [0.0125, 0.0135, 0.0115], n=7)
    pommel = ellipsoid(0.025, 0.016, 0.021, n=10, m=6).move(0, -0.086, 0)
    guard = box(0.19, 0.02, 0.024, bevel=0.005).move(0.012, 0.068, 0)
    guard.v[:, 1] += 0.5 * np.abs(guard.v[:, 0]) ** 2 * 6.0     # ends curve towards the blade
    L = length
    st = [(0.076, 0.027, 0.027, 0.0075), (0.19, 0.0265, 0.0265, 0.0072), (0.205, 0.0265, 0.015, 0.007), (0.225, 0.026, 0.026, 0.007),
          (0.40, 0.0245, 0.0245, 0.0066), (0.415, 0.013, 0.0245, 0.0064), (0.44, 0.024, 0.024, 0.0064),
          (0.56, 0.022, 0.022, 0.006), (0.575, 0.022, 0.012, 0.006), (0.59, 0.0215, 0.021, 0.0058),
          (L - 0.09, 0.018, 0.018, 0.005), (L - 0.03, 0.0105, 0.012, 0.004), (L, 0.0, 0.002, 0.001)]
    bl = blade(st)
    for p, lk in ((grip, look_grip), (pommel, look_hilt), (guard, look_hilt), (bl, look_blade)):
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=(22 if p is bl else 40))


def build():
    C = M.Creature('mon_skeleton', 15000)
    C.look('bone', 'bone', 'bone_skel', dirt_lo=0.0, dirt_hi=0.55)
    C.look('rust', 'metal', 'metal_rust', amount=0.85, color=(0.19, 0.18, 0.17))
    C.look('blade', 'metal', 'metal_rust', amount=0.6, color=(0.27, 0.26, 0.25), edge=1.0)
    C.look('strap', 'leather', 'leather_strap', color=(0.085, 0.055, 0.035))
    C.look('rag', 'cloth', 'cloth_rag', color=(0.115, 0.075, 0.06), hem_lo=0.5, hem_hi=1.0)
    C.look('eye', 'emis', 'emis_eye', emit=(0.30, 1.0, 0.45), strength=3.0)

    import kit_boss as KX
    KX.add_skull_seated(C, jaw_open=14.0)            # kit skull with the tooth row and jaw seated (they floated below / beside it)
    KB.add_skeleton(C, jaw_open=14.0, thick=1.3, skull=False)

    # --- dented kettle cap
    prof = [(0.132, 0.0), (0.135, 0.005), (0.104, 0.017), (0.094, 0.028), (0.088, 0.046), (0.07, 0.066), (0.04, 0.079), (0.0, 0.084)]
    cap = revolve(prof, n=16).scale((0.93, 1.08, 1.0))
    d = np.linalg.norm(cap.v - np.array([0.045, -0.04, 0.07]), axis=1)
    cap.v[:, 2] -= np.exp(-(d / 0.035) ** 2) * 0.016
    d = np.linalg.norm(cap.v - np.array([-0.11, 0.05, 0.0]), axis=1)        # bent brim
    cap.v[:, 2] -= np.exp(-(d / 0.06) ** 2) * 0.022
    cap.rot('y', 7).rot('x', -6).move(0.004, -0.004, 1.752)
    C.add(cap, bone='Head', look='rust', smooth=38)

    # --- pauldron on the left shoulder, held by a chest strap
    dome = revolve([(0.0, 0.052), (0.03, 0.048), (0.062, 0.034), (0.082, 0.012), (0.088, 0.0)], n=12).scale((1.0, 0.92, 1.0))
    p1 = dome.copy().rot('y', 22).move(0.196, 0.052, 1.474)
    C.add(p1, bone='clavicle_l', look='rust', smooth=38)
    p2 = dome.copy().scale((0.82, 0.86, 0.75)).rot('y', 52).move(0.262, 0.056, 1.452)
    C.add(p2, bone='upperarm_l', look='rust', smooth=38)
    rim = M.torus(0.066, 0.006, n=12, m=4, ry=0.058).rot('y', 52).move(0.262, 0.056, 1.452)
    C.add(rim, bone='upperarm_l', look='rust')
    spike = M.cone_spike(0.018, 0.06, n=6).rot('y', 22).move(0.215, 0.052, 1.52)
    C.add(spike, bone='clavicle_l', look='rust', smooth=30)
    C.add(band((0, 0.0, 1.36), 0.166, 0.13, 0.034, 0.008, tilt_y=-31, n=22), bone='spine_03', look='strap')

    # --- belt + rags
    C.add(band((0, 0.02, 1.043), 0.158, 0.112, 0.036, 0.009, tilt_y=5, n=22), bone='pelvis', look='strap')
    buckle = box(0.05, 0.012, 0.05, bevel=0.006).move(0.02, -0.094, 1.045)
    C.add(buckle, bone='pelvis', look='rust')
    top, nr = arc_pts((0, 0.02, 1.035), 0.160, 0.114, -150, -62, 7)       # front-right hip
    rag = hang(top, 0.42, rows=6, out=nr, flare=0.03, rag=0.35, seed=4, wave=0.012, holes=0.25, tatter=0.18)
    M.weights_by(rag, rag.v[:, 2], [(0.62, {'thigh_r': 0.85, 'pelvis': 0.15}), (0.9, {'thigh_r': 0.5, 'pelvis': 0.5}), (1.03, {'pelvis': 1.0})])
    C.add(rag, look='rag')
    top, nr = arc_pts((0, 0.02, 1.035), 0.160, 0.114, 40, 115, 6)          # back-left
    rag2 = hang(top, 0.30, rows=5, out=nr, flare=0.02, rag=0.4, seed=9, wave=0.012, holes=0.3, tatter=0.2)
    M.weights_by(rag2, rag2.v[:, 2], [(0.72, {'thigh_l': 0.7, 'pelvis': 0.3}), (1.03, {'pelvis': 1.0})])
    C.add(rag2, look='rag')

    sword(C)
    return C, "Articulated rigid-part skeleton (every bone piece bound to one joint); kettle cap, left pauldron, hip rags; wpn_sword in right hand."
