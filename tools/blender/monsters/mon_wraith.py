"""mon_wraith 縛魂 — a soul the Hushed Order chained to its own shroud: legless spectral torso whose shredded
winding-sheet trails backwards into wisps, gaunt dark arms ending in long pale claws, a hollow cowl with two slanted
soul-lights, a rusted chain cinched twice round the waist under a seal bearing the Order's mark."""
import math
import numpy as np
import monlib as M
import kit_cloth as KC
import kit_flesh as KF
import kit_b as KB2
import kit_c as KC3
import kit_wisp as KW
from monlib import V, tube, ellipsoid, box, join, torus


def build():
    C = M.Creature('mon_wraith', 15000)
    C.look('shroud', 'spectral', 'spectral_shroud', color=(0.34, 0.47, 0.50), dark=(0.06, 0.10, 0.125), wisp=(0.60, 0.80, 0.84),
           fade_lo=0.15, fade_hi=1.05)
    C.look('cowl', 'spectral', 'spectral_shroud', color=(0.22, 0.31, 0.35), dark=(0.035, 0.06, 0.08), fade_lo=-2.0, fade_hi=-1.0)
    C.look('shade', 'skin', 'skin_shade', color=(0.075, 0.095, 0.105), blotch=(0.03, 0.04, 0.05), vein=(0.16, 0.30, 0.34), vein_amt=0.5,
           sore_amt=0.0, hands_dark=0.7, dark=(0.012, 0.016, 0.02), hands_x0=0.55, hands_x1=0.9)
    C.look('void', 'flat', 'skin_shade', color=(0.004, 0.005, 0.007), rough=1.0)
    C.look('claw', 'bone', 'skin_shade', color=(0.50, 0.56, 0.56), stain=(0.10, 0.16, 0.18), crack_amt=0.25)
    C.look('iron', 'metal', 'metal_chain', amount=0.75, color=(0.17, 0.17, 0.18), rough=0.5)
    C.look('soul', 'emis', 'emis_soul', emit=(0.35, 0.88, 1.0), strength=5.0, mottle=0.1)

    # ---- dark core: legless gaunt torso, thin arms, stretched fingers with long claws
    b = KW.core_body('male', ratio=0.6, z_cut=1.08)
    KW.stretch_fingers(b, 1.45, 1.25)
    pose = {
        'spine_01': dict(scale=(0.74, 0.72, 1.0)),
        'spine_02': dict(scale=(0.74, 0.70, 1.0)),
        'spine_03': dict(scale=(0.80, 0.76, 1.0)),
        'neck_01': dict(scale=(0.7, 0.7, 1.0)),
    }
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(scale=(1.0, 0.8, 0.85))
        pose['upperarm_' + s] = dict(scale=(1.0, 0.60, 0.60))
        pose['lowerarm_' + s] = dict(scale=(1.0, 0.58, 0.58))
    M.pose_sculpt(b, pose)
    C.add(b, look='shade', smooth=180)
    KF.claws(C, 'claw', body=b, length=0.08, curl=0.45, n=5)

    # ---- hollow cowl with two soul-lights
    C.add(ellipsoid(0.08, 0.088, 0.118, n=12, m=8).move(0, -0.004, 1.676), bone='Head', look='void')
    for sx in (1, -1):
        e = ellipsoid(0.021, 0.011, 0.0115, n=8, m=5).rot('y', -sx * 20).move(sx * 0.035, -0.086, 1.70)
        C.add(e, bone='Head', look='soul', smooth=90)
    hd = KW.rag_hood(c=(0, -0.006, 1.70), rx=0.118, rz=0.142, open_rx=0.102, open_rz=0.14, front=-0.155, back=0.115, tail=0.10,
                     tail_drop=0.12, shear=0.5, n=22, rag=0.03, seed=7)
    C.add(hd, bone='Head', look='cowl')
    for k, (bx, bz, L_, ang, wd) in enumerate(((0.012, 1.755, 0.30, 7, 0.06), (-0.055, 1.675, 0.24, -24, 0.05))):
        d = (math.sin(math.radians(ang)), math.cos(math.radians(ang)), 0.05)
        C.add(KW.trail(V(bx, 0.095, bz), d, L_, width=wd, rows=6, droop=0.95, wave=0.02, seed=40 + k), bone='Head', look='cowl')

    # ---- winding-sheet: gown tapering backwards into a tail, ragged mantle over shoulders and upper arms, torn sleeves
    secs = [(0.40, 0, 0.30, 0.055, 0.055), (0.52, 0, 0.235, 0.078, 0.074), (0.66, 0, 0.17, 0.102, 0.094), (0.80, 0, 0.115, 0.124, 0.108),
            (0.94, 0, 0.06, 0.140, 0.120), (1.06, 0, 0.032, 0.140, 0.120), (1.16, 0, 0.024, 0.138, 0.118), (1.28, 0, 0.02, 0.162, 0.135),
            (1.40, 0, 0.02, 0.186, 0.145), (1.46, 0, 0.02, 0.180, 0.13), (1.53, 0, 0.012, 0.095, 0.095)]
    gw, R = KB2.gown(secs, n=32, hem_z=0.40, pleat_top=1.12, pleat_amp=0.11, rag=0.14, seed=5)
    KW.shroud_weights(gw, smooth=5)
    C.add(gw, look='shroud')
    mt, G = KW.poncho(X=0.36, Yf=0.17, Yb=0.19, n=44, rows=8,
                      ridge=((0.0, 1.60), (0.07, 1.578), (0.13, 1.538), (0.20, 1.518), (0.46, 1.505)),
                      flat=((0.0, 0.095), (0.12, 0.112), (0.2, 0.07), (0.3, 0.048), (0.5, 0.04)),
                      depth_f=((0.0, 0.30), (0.1, 0.27), (0.2, 0.19), (0.3, 0.14), (0.42, 0.09)),
                      depth_b=((0.0, 0.46), (0.1, 0.42), (0.2, 0.26), (0.3, 0.16), (0.42, 0.09)),
                      rag=0.3, seed=21)
    mt = KW.punch(mt, G.shape, 0.07, seed=4, t0=0.7)
    KC.skin_cloth(mt, smooth=3, limit=KW.upper_bones())
    C.add(mt, look='cowl')
    n_ = 12
    sl = KC.sleeve(r_sh=0.066, r_el=0.058, r_cuff=0.064, n=n_, x0=0.15, x1=0.53, droop=0.01, rings=8)
    sl.v[-n_:, 0] -= KC.ragged(n_, 0.08, 9)
    KC.skin_cloth(sl, smooth=2)
    C.add(sl, look='shroud', mirror=True)
    # sleeve shreds: built for hanging arms (T-pose +X is "down" once the arm hangs), streaming backwards
    for k, (x, z, L_, wd, dv, dr) in enumerate(((0.30, 1.40, 0.20, 0.07, (0.55, 0.8, -0.2), 0.5), (0.40, 1.425, 0.27, 0.06, (0.25, 0.95, 0.1), 0.75),
                                                (0.50, 1.41, 0.17, 0.05, (0.6, 0.75, -0.25), 0.4))):
        t = KW.trail(V(x, 0.112, z), dv, L_, width=wd, rows=5, droop=dr, wave=0.02, seed=30 + k, down=(1, 0, 0), side=(0, 0, 1))
        C.add(t, bone='upperarm_l' if x < 0.45 else 'lowerarm_l', look='shroud', mirror=True)

    # ---- trailing shreds: a skirt of rags from under the chain and the wisps of the tail (pelvis / lower spine only)
    for w in KB2.wisps((0, 0.028, 1.10), 0.147, 0.127, count=8, lmin=0.28, lmax=0.48, width=0.12, rows=7, seed=8, swirl=18, flare=0.03,
                       wave=0.03, back_bias=0.3):
        KW.sweep_back(w, 1.0, k=0.38, power=1.5)
        M.weights_by(w, w.v[:, 2], [(0.9, {'pelvis': 1.0}), (1.02, {'pelvis': 0.6, 'spine_01': 0.4}), (1.1, {'spine_01': 1.0})])
        C.add(w, look='shroud')
    for w in KB2.wisps((0, 0.19, 0.62), 0.10, 0.09, count=9, lmin=0.22, lmax=0.42, width=0.11, rows=7, seed=3, swirl=30, flare=0.05,
                       wave=0.03, back_bias=0.25):
        KW.sweep_back(w, 0.62, k=0.55, power=1.3, zmin=0.07)
        M.weights_from(w, {'pelvis': 1.0})
        C.add(w, look='shroud')

    # ---- the binding: chain twice round the waist, seal with the Order's mark, a hanging end, broken manacles
    surf = KB2.Surf(gw)
    allowed = ['spine_01', 'spine_02', 'pelvis']
    KB2.add_chain(C, KW.ellipse_path((0, 0.026, 1.168), 0.152, 0.131, n=28, tilt_x=7, a0=-84, a1=264), 'iron', surf=surf, allowed=allowed,
                  link=0.058, r=0.0095)
    KB2.add_chain(C, KW.ellipse_path((0, 0.028, 1.108), 0.154, 0.133, n=28, tilt_x=-6, tilt_y=5, a0=-84, a1=264), 'iron', surf=surf,
                  allowed=allowed, link=0.058, r=0.0095)
    sc = V(0.0, -0.122, 1.14)
    C.add(KC3.disc(0.052, 0.016, n=10, bevel=0.004).move(sc), bone='spine_01', look='iron')
    C.add(KB2.order_mark(H=0.075, depth=0.007, w=0.11).move(sc + V(0, -0.0095, 0)), bone='spine_01', look='soul')
    KB2.add_chain(C, [sc + V(0.012, -0.004, -0.05), V(0.02, -0.124, 0.92), V(0.012, -0.11, 0.74)], 'iron', bone='pelvis', link=0.058, r=0.0095)
    C.add(torus(0.036, 0.011, n=10, m=5).rot('x', 90).move(0.012, -0.108, 0.70), bone='pelvis', look='iron', smooth=60)
    for s, sx in (('l', 1), ('r', -1)):
        c = KC3.BP('lowerarm_' + s, 0.9)
        C.add(torus(0.033, 0.0115, n=10, m=5).rot('y', 90).move(c), bone='lowerarm_' + s, look='iron', smooth=60)
        KB2.add_chain(C, [c + V(0, 0.036, -0.004), c + V(0, 0.10, -0.05), c + V(0, 0.15, -0.13)], 'iron', bone='lowerarm_' + s, link=0.05, r=0.008)

    return C, ("Legless spectral caster: dark gaunt core (base body without legs/head, thinned arms, fingers stretched 1.45x, rigid claws) under a "
               "spectral_ winding-sheet (gown + mantle draped over the T-pose arms + torn sleeves) whose tail and shreds are weighted to pelvis / "
               "spine only and swept backwards; hollow cowl rigid on Head with two emis_ soul-lights; rigid-link chain belt, seal with an emis_ "
               "Order mark, broken manacles. Floats: lowest shreds end about 7-20 cm above the ground. No weapon.")
