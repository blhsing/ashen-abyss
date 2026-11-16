"""mon_cultist 寂滅教徒 — hooded ash-grey robes, blank mask, rope belt, curved dagger, candle wax on the shoulders."""
import math
import numpy as np
import monlib as M
import kit_cloth as KC
from monlib import V, tube, ellipsoid, box, join, blade, hang, arc_pts


def dagger(C, look_blade='iron', look_grip='cord', obj='wpn_dagger'):
    grip = tube([(0, -0.052, 0), (0, 0.0, 0), (0, 0.046, 0)], [0.0115, 0.013, 0.0115], n=7)
    pommel = M.torus(0.016, 0.006, n=10, m=5).rot('y', 90).move(0, -0.07, 0)
    guard = box(0.07, 0.014, 0.022, bevel=0.004).move(0.004, 0.054, 0)
    st = [(0.058, 0.019, 0.019, 0.006), (0.12, 0.021, 0.019, 0.0058), (0.2, 0.019, 0.016, 0.005), (0.28, 0.013, 0.011, 0.004),
          (0.33, 0.006, 0.005, 0.003), (0.36, 0.0, 0.001, 0.001)]
    bl = blade(st)
    bl.bend('y', 'z', -150.0, origin=0.058)
    for p, lk in ((grip, look_grip), (pommel, look_blade), (guard, look_blade), (bl, look_blade)):
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=(22 if p is bl else 40))


def build():
    C = M.Creature('mon_cultist', 15000)
    C.look('robe', 'cloth', 'cloth_robe', color=(0.205, 0.20, 0.195), dirt=(0.10, 0.09, 0.08), hem_lo=0.05, hem_hi=0.65, fold=9.0)
    C.look('hood', 'cloth', 'cloth_robe', color=(0.125, 0.125, 0.13), hem_lo=-2, hem_hi=-1, fold=12.0)
    C.look('rope', 'cloth', 'cloth_robe', color=(0.30, 0.235, 0.14), hem_lo=-2, hem_hi=-1, scale=30.0, bump=0.8)
    C.look('void', 'flat', 'cloth_robe', color=(0.008, 0.008, 0.01), rough=1.0)
    C.look('mask', 'bone', 'bone_mask', color=(0.66, 0.64, 0.58), stain=(0.30, 0.26, 0.21), crack=13.0, crack_amt=0.4, blotch=11.0)
    C.look('wax', 'wax', 'bone_mask', color=(0.60, 0.55, 0.42), soot=0.25)
    C.look('wick', 'flat', 'bone_mask', color=(0.02, 0.02, 0.02), rough=1.0)
    C.look('skin', 'skin', 'skin_ash', color=(0.29, 0.285, 0.275), blotch=(0.15, 0.14, 0.14), hands_dark=0.0, vein_amt=0.4)
    C.look('iron', 'metal', 'metal_dagger', amount=0.3, color=(0.27, 0.27, 0.285), edge=1.0, rough=0.34)
    C.look('cord', 'leather', 'metal_dagger', color=(0.05, 0.04, 0.035))
    C.look('mark', 'emis', 'emis_mark', emit=(0.80, 0.90, 1.0), strength=3.5, mottle=0.1)

    # ---- robe
    secs = [(0.06, 0, 0.02, 0.285, 0.26), (0.30, 0, 0.02, 0.25, 0.23), (0.62, 0, 0.022, 0.215, 0.20), (0.92, 0, 0.03, 0.182, 0.17),
            (1.07, 0, 0.025, 0.158, 0.142), (1.13, 0, 0.022, 0.162, 0.146), (1.27, 0, 0.02, 0.18, 0.158), (1.38, 0, 0.02, 0.198, 0.165),
            (1.45, 0, 0.02, 0.185, 0.142), (1.51, 0, 0.012, 0.10, 0.10)]
    rb = KC.robe(secs, n=36, hem_z=0.06, pleat_top=1.02, pleat_amp=0.07, rag=0.05, seed=2)
    KC.skin_cloth(rb, smooth=5, pelvis_mix=0.28, mix_top=0.95, mix_bot=0.3)
    C.add(rb, look='robe')
    sl = KC.sleeve(r_sh=0.08, r_el=0.066, r_cuff=0.095, droop=0.035)
    KC.skin_cloth(sl, smooth=2)
    C.add(sl, look='robe', mirror=True)
    # dark lining disc inside the cuff so we do not look down an empty sleeve
    R = M.RIG
    cuff = ellipsoid(0.02, 0.05, 0.05, n=8, m=5).move(R.H('hand_l') + V(-0.05, 0.004, -0.02))
    C.add(cuff, bone='lowerarm_l', look='void', mirror=True)

    # ---- hood, mantle, void head, mask
    hd = KC.hood(tail=0.13, tail_drop=0.11)
    hd.W = M.rigid_W(len(hd.v), 'Head')
    C.add(hd, look='hood')
    cp = KC.capelet(z_top=1.585, z_bot=1.375, r_top=(0.092, 0.105), r_bot=(0.29, 0.19), rag=0.045)
    KC.skin_cloth(cp, smooth=3)
    C.add(cp, look='hood')
    C.add(ellipsoid(0.085, 0.095, 0.108, n=12, m=8).move(0, 0.0, 1.675), bone='Head', look='void')
    mc = np.array([0, -0.112, 1.664])
    mask = ellipsoid(0.060, 0.02, 0.094, n=16, m=9)
    t = np.clip(-mask.v[:, 2] / 0.094, 0, 1)
    mask.v[:, 0] *= (1 - 0.32 * t ** 1.5)                       # tapers to the chin
    mask.v[:, 1] += 1.1 * mask.v[:, 0] ** 2 / 0.06 - 0.004 * np.exp(-(mask.v[:, 0] / 0.012) ** 2)   # wraps the face, faint ridge
    mask.move(mc).rot('x', 4, pivot=mc)
    C.add(mask, bone='Head', look='mask', smooth=70)
    slit = box(0.0034, 0.006, 0.07).move(mc + np.array([0, -0.0195, 0.012])).rot('x', 4, pivot=mc)
    C.add(slit, bone='Head', look='mark')

    # ---- hands and wrapped feet
    C.add(KC.body_piece(KC.hand_bones('l') + KC.hand_bones('r')), look='skin')
    bd = KC.ref_body()
    ft = bd.sub(bd.vmask_faces((bd.wsum(['foot_l', 'foot_r', 'ball_l', 'ball_r', 'calf_l', 'calf_r']) > 0.5) & (bd.v[:, 2] < 0.22), 'all'))
    ft.inflate(0.005)
    ft.v[:, 2] = np.maximum(ft.v[:, 2], 0.0)       # the inflated soles sank 13 mm below the ground (verify: feet on ground)
    C.add(ft, look='hood')

    # ---- rope belt
    loop, tails = KC.rope_belt(z=1.085, rx=0.178, ry=0.158, cy=0.024, r=0.013, tail=0.36, side=1)
    C.add(loop, bone='spine_01', look='rope')
    M.weights_by(tails, tails.v[:, 2], [(0.72, {'thigh_l': 0.6, 'pelvis': 0.4}), (1.0, {'pelvis': 1.0}), (1.09, {'spine_01': 1.0})])
    C.add(tails, look='rope')

    # ---- candle stubs and wax runs on the shoulders
    for (x, y, h, sd) in ((0.150, 0.03, 0.06, 1), (0.212, 0.05, 0.034, 2), (-0.170, 0.02, 0.045, 3)):
        z = KC.rest_on(cp, x, y) - 0.004
        c, wick = KC.candle(0.015, h, seed=sd)
        bn = 'clavicle_l' if x > 0 else 'clavicle_r'
        C.add(c.move(x, y, z), bone=bn, look='wax', smooth=50)
        C.add(wick.move(x, y, z), bone=bn, look='wick')
        sx = 1 if x > 0 else -1
        for k, (dx, dy) in enumerate(((0.05, -0.085), (0.05, 0.07), (0.075, -0.01))):
            pts = []
            for s in np.linspace(0, 1, 5):          # wax run following the mantle surface
                px = x + sx * dx * s; py = y + dy * s
                zz = KC.rest_on(cp, px, py)
                pts.append((px, py, (zz if zz is not None else z - 0.1) + 0.003))
            d = tube(np.array(pts), [0.013, 0.009, 0.007, 0.008, 0.012], n=5, r2=[0.006, 0.005, 0.004, 0.005, 0.007], up=(0, 0, 1))
            C.add(d, bone=bn, look='wax', smooth=60)

    dagger(C)
    return C, "Robe/hood/mantle shells skinned by weight transfer from the base body; blank bone mask with a hairline emis_ mark; wpn_dagger in right hand."
