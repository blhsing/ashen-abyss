"""mon_skeleton_thrower 骸骨擲矛手 — light articulated skeleton under a torn madder-red hood and shawl, a quiver of
javelins slung across its back (heads fanning up over the left shoulder) and one javelin in the right hand."""
import math
import numpy as np
import monlib as M
import kit_bones as KB
import kit_cloth as KC
import kit_b as KB2
import kit_c as KC3
import kit_wisp as KW
from monlib import V, tube, ellipsoid, box, revolve, join, torus

LOOK = dict(wood='wood', iron='iron', strap='strap')


def build():
    C = M.Creature('mon_skeleton_thrower', 15000)
    C.look('bone', 'bone', 'bone_skel', dirt_lo=0.0, dirt_hi=0.55)
    C.look('iron', 'metal', 'metal_rust', amount=0.55, color=(0.27, 0.26, 0.25), edge=1.0)
    C.look('wood', 'wood', 'leather_strap', color=(0.115, 0.078, 0.048), axis=(0, 1, 0))
    C.look('wood_b', 'wood', 'leather_strap', color=(0.115, 0.078, 0.048), axis=(0.39, 0, 0.92))
    C.look('strap', 'leather', 'leather_strap', color=(0.085, 0.055, 0.035))
    C.look('shawl', 'cloth', 'cloth_shawl', color=(0.25, 0.085, 0.06), dirt=(0.10, 0.085, 0.07), hem_lo=1.2, hem_hi=1.46, hem_amt=0.75,
           fold=11.0, stain_amt=0.6, streak=0.5)
    C.look('hood', 'cloth', 'cloth_shawl', color=(0.19, 0.062, 0.048), hem_lo=-2, hem_hi=-1, fold=12.0, stain_amt=0.6)
    C.look('eye', 'emis', 'emis_eye', emit=(0.30, 1.0, 0.45), strength=3.0)

    import kit_boss as KX
    KX.add_skull_seated(C, jaw_open=9.0)             # kit skull with the tooth row and jaw seated (they floated below / beside it)
    KB.add_skeleton(C, jaw_open=9.0, thick=1.0, skull=False)

    # ---- shawl draped over shoulders and upper arms, hood pushed back off the brow
    sh, G = KW.poncho(seed=11, rag=0.2, point=0.10)
    sh = KW.punch(sh, G.shape, 0.16, seed=5, t0=0.6)
    KC.skin_cloth(sh, smooth=3)
    C.add(sh, look='shawl')
    surf = KB2.Surf(sh)
    hd = KW.rag_hood(seed=3)
    C.add(hd, bone='Head', look='hood')

    # ---- quiver of javelins across the back, heads up over the left shoulder
    axis = M.unit(V(0.30, -0.012, 2.02) - V(-0.13, 0.0, 0.99))
    base = V(-0.125, 0.232, 0.99)
    side = M.unit(np.cross(axis, (0, 1, 0)))        # across the bundle (in the back plane)
    for k, (dx, da, ds, ln) in enumerate(((-0.040, -5.5, 0.0, 1.10), (0.0, 0.0, 0.075, 1.12), (0.040, 6.0, 0.02, 1.06))):
        d = M.rot_axis('y', da) @ axis
        jv = KW.javelin(length=ln, back=0.0, wrap=False, ferrule=False, head=0.2)
        KW.place(jv, d, base + side * dx + axis * ds + V(0, 0.004 * (k % 2), 0), zhint=(0, 1, 0))
        for p, kind in jv:
            C.add(p, bone='spine_03', look='wood_b' if kind == 'wood' else LOOK[kind])
    q0 = base - axis * 0.03; q1 = base + axis * 0.2; q2 = base + axis * 0.43
    quiver = tube([q0, q0 + axis * 0.02, q1, q2 - axis * 0.02, q2], [0.022, 0.028, 0.03, 0.031, 0.034], n=10,
                  r2=[0.05, 0.062, 0.072, 0.078, 0.082], up=(0, 1, 0), cap0=True, cap1=False)
    quiver.smooth = 60.0
    C.add(quiver, bone='spine_03', look='strap')
    a = np.linspace(0, 2 * math.pi, 12, endpoint=False)
    rim = tube(q2[None, :] + np.cos(a)[:, None] * side * 0.088 + np.sin(a)[:, None] * V(0, 0.04, 0), 0.0085, n=5, closed=True)
    C.add(rim, bone='spine_03', look='strap', smooth=60)
    # stitched seam down the quiver and a patch
    for s in np.linspace(0.02, 0.38, 7):
        c = base + axis * s + V(0, 0.031 + 0.004 * s, 0) + side * 0.012
        C.add(tube([c - side * 0.016 - axis * 0.012, c + V(0, 0.004, 0), c + side * 0.016 + axis * 0.012], 0.0035, n=4), bone='spine_03', look='bone', smooth=60)
    for s in (0.10, 0.36):
        c = base + axis * s
        cord = tube([c - side * 0.09, c - side * 0.05 + V(0, 0.034, 0), c + V(0, 0.04, 0), c + side * 0.05 + V(0, 0.034, 0), c + side * 0.09,
                     c + side * 0.05 - V(0, 0.034, 0), c - V(0, 0.04, 0), c - side * 0.05 - V(0, 0.034, 0)], 0.007, n=5, closed=True)
        C.add(cord, bone='spine_03', look='bone' if s > 0.3 else 'strap', smooth=60)
    # carrying strap: from the quiver mouth up the back, over the left shoulder, down to the front hem (rides on the shawl)
    pts = KW.seat_path(surf, [(0.0, 0.176), (0.045, 0.15), (0.095, 0.09), (0.118, 0.02), (0.112, -0.05), (0.092, -0.115), (0.078, -0.148)], n=16, lift=0.006)
    T, N, B = M._frames(pts, up=(0, 0, 1))
    st = tube(pts, 0.0045, n=4, r2=0.021, up=(0, 0, 1), phase=math.pi / 4)
    st.smooth = 30.0
    KB2.skin_like(st, sh, smooth=0)
    C.add(st, look='strap')
    C.add(tube([pts[0] + V(0, 0.0, 0.01), q2 + V(0.03, -0.01, -0.03)], 0.0045, n=4, r2=0.02, up=(0, 1, 0), phase=math.pi / 4),
          bone='spine_03', look='strap', smooth=30)
    # old chest strap of the family (shows on the ribs below the shawl)
    C.add(M.band((0, 0.0, 1.30), 0.160, 0.128, 0.032, 0.008, tilt_y=-24, n=22), bone='spine_03', look='strap')

    # ---- leather wrap on the throwing forearm, cord round the hips with two bone toggles
    br = KC3.limb_plate('lowerarm_r', [(0.30, 0.024, 0.036), (0.55, 0.022, 0.033), (0.86, 0.020, 0.031)], n=9, lip=0.004)
    C.add(br, bone='lowerarm_r', look='strap', smooth=50)
    C.add(M.band((0, 0.02, 1.04), 0.150, 0.108, 0.018, 0.008, tilt_y=-4, n=20), bone='pelvis', look='strap')
    for (ang, L_) in ((-118, 0.11), (-100, 0.08)):
        a = math.radians(ang)
        p0 = V(0.152 * math.cos(a), 0.02 + 0.110 * math.sin(a), 1.036)
        tg = tube([p0, p0 + V(0, -0.004, -L_ * 0.5), p0 + V(0, 0, -L_)], 0.004, n=4)
        tok = KB.seg(p0 + V(0, -0.002, -L_), p0 + V(0, -0.002, -L_ - 0.055), 0.0065, 0.0065, 1.5, 1.5, n=5)
        C.add(tg, bone='pelvis', look='strap')
        C.add(tok, bone='pelvis', look='bone', smooth=50)

    # ---- the javelin in hand
    for p, kind in KW.javelin(length=1.34, back=0.50):
        p.to_grip('r')
        C.add(p, look=LOOK[kind], obj='wpn_javelin', smooth=(22 if kind == 'iron' else 50))
    return C, ("Light articulated skeleton (thick=1.0) under a torn madder-red shawl draped over shoulders and upper arms (skinned by weight "
               "transfer) and a hood pushed back off the brow (rigid on Head); quiver with three spare javelins rigid on spine_03, heads "
               "fanning above the left shoulder; wpn_javelin in the right hand along the fist axis (tip = index side). In OverhandThrow the "
               "hand opens at about 22% of the clip (frame 9 of 40): hide wpn_javelin from there until ~85% and spawn the projectile.")
