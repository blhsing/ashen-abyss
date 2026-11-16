"""ARCHIVE: the first boss_osaros (replaced by v2, see boss_osaros.py and the osaros_v2 folder). Its export is kept as out/boss_osaros_v1.glb.
boss_osaros 寂主・奧薩洛斯 — Osaros, Lord of Silence: a towering regal figure in void-black fluted plate. A faceless, smooth,
elongated helm split by one vertical cold-white crack that runs on down gorget, breastplate and belt (the focal motif);
a crown that is a ring of thin spires with the stubs of extinguished candles between them; tall spired pauldrons; a long
ragged double mantle hanging to above the ankles (weighted to spine / hips / thighs); a great blade of dull black glass."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_c as K
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, blade, horn, band, revolve, hang, arc_pts


def glass_blade(C, obj='wpn_blade'):
    P = []
    P.append((tube([(0, -0.15, 0), (0, 0.0, 0), (0, 0.17, 0)], [0.015, 0.0165, 0.014], n=7), 'grip', 50))
    P.append((revolve([(0.0, -0.045), (0.02, -0.036), (0.034, -0.01), (0.03, 0.012), (0.014, 0.03)], 8).rot('x', -90).move(0, -0.175, 0), 'plate', 40))
    for sx in (-1, 1):                                                               # guard: two horns swept towards the blade
        P.append((horn(M.polyline([(sx * 0.02, 0.185, 0), (sx * 0.12, 0.19, 0), (sx * 0.20, 0.235, 0), (sx * 0.235, 0.32, 0)], 7), 0.022, n=6, r_tip=0.004, r2=0.013), 'plate', 40))
    P.append((box(0.07, 0.05, 0.036, bevel=0.008).move(0, 0.19, 0), 'plate', 30))
    rng = np.random.default_rng(7)
    st = []
    ys = np.linspace(0.21, 1.72, 17)
    for i, y in enumerate(ys):
        t = i / 16.0
        w = 0.074 * (1 - 0.25 * t) * (1.0 if i < 15 else (0.55 if i == 15 else 0.0))
        chipl = rng.uniform(0.0, 0.016) if 0 < i < 15 else 0.0                        # conchoidal bites out of both edges
        chipr = rng.uniform(0.0, 0.016) if 0 < i < 15 else 0.0
        st.append((y, max(w - chipl, 0.0), max(w - chipr, 0.003), 0.02 * (1 - 0.6 * t) + 0.003))
    bl = blade(st)
    P.append((bl, 'glass', 14))
    for p, lk, sm in P:
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('boss_osaros_v1', 40000)      # archived v1 (superseded by boss_osaros.py = v2): exports boss_osaros_v1.glb, never the game's file
    R = M.RIG
    vp = dict(color=(0.022, 0.023, 0.028), rust=(0.06, 0.055, 0.06), rust2=(0.02, 0.02, 0.022), edge_color=(0.26, 0.27, 0.31), edge=0.9)
    C.look('plate', 'metal', 'metal_void', **dict(vp, amount=0.3, rough=0.36, bump=0.7))
    C.look('plate2', 'metal', 'metal_void', **dict(vp, amount=0.5, scale=11.0, rough=0.5, bump=0.8))
    C.look('suit', 'cloth', 'cloth_mantle', color=(0.02, 0.02, 0.024), hem_lo=-2, hem_hi=-1, fray=0.1)
    C.look('mantle', 'cloth', 'cloth_mantle', color=(0.045, 0.042, 0.05), weave='wool', dirt=(0.20, 0.19, 0.18), hem_lo=0.15, hem_hi=1.0, hem_amt=0.9, fold=7.0,
           fold_amt=1.0, fray=0.6, streak=0.5, dust=0.5)
    C.look('mantle2', 'cloth', 'cloth_mantle', color=(0.02, 0.02, 0.026), weave='wool_weave', dirt=(0.17, 0.16, 0.16), hem_lo=0.3, hem_hi=1.1, hem_amt=0.8, fold=9.0,
           fold_amt=1.0, fray=0.6)
    C.look('grip', 'leather', 'cloth_mantle', color=(0.03, 0.026, 0.026))
    C.look('glass', 'glass', 'rock_glass', color=(0.012, 0.012, 0.016), band=(0.04, 0.04, 0.055), rough=0.32, bump=0.5)
    C.look('wax', 'wax', 'bone_wax', color=(0.50, 0.47, 0.38), soot=0.8)
    C.look('waxb', 'wax', 'bone_wax', color=(0.06, 0.06, 0.068), soot=0.6)
    C.look('wick', 'flat', 'bone_wax', color=(0.02, 0.02, 0.02), rough=1.0)
    C.look('crack', 'emis', 'emis_crack', emit=(0.86, 0.93, 1.0), strength=4.0, mottle=0.2, base_mul=0.7)

    def add(p, bone, look, smooth=None, mirror=False):
        return C.add(p, bone=bone, look=look, smooth=smooth, mirror=mirror)

    su = K.under_suit(0.36)
    C.add(su.sub(su.vmask_faces(su.v[:, 2] > 0.09, 'any')), look='suit')
    C.add(K.gauntlet_hands(0.005, 0.75), look='plate')

    # ------------------------------------------------------------------ fluted cuirass, plackart, faulds, gorget; the crack runs down them all
    cu = [(1.225, 0, 0.02, 0.18, 0.15), (1.27, 0, 0.02, 0.204, 0.168), (1.34, 0, 0.02, 0.224, 0.182), (1.41, 0, 0.018, 0.22, 0.174), (1.465, 0, 0.012, 0.17, 0.13),
          (1.497, 0, 0.008, 0.112, 0.102)]
    rm = K.ridge_mod(0.07, 0.24)
    add(K.torso_plate(cu, n=22, mod=rm).subdiv(1), 'spine_03', 'plate', 38)
    for sx in (-1, 1):                                                               # gothic fluting fanning from the sternum
        for k, a in enumerate((-70, -52, -34)):
            add(K.surf_strip(cu, [(1.235 + 0.02 * k, -90 + sx * 6), (1.32, -90 + sx * (90 + a) * 0.55), (1.44, -90 + sx * (90 + a))], w=0.012, th=0.008, mod=rm), 'spine_03', 'plate2', 30)
    cr = [(1.492, -90), (1.44, -91.5), (1.395, -88), (1.34, -91), (1.29, -89), (1.235, -90.5)]
    add(K.surf_strip(cu, cr[::-1], w=0.0075, th=0.006, mod=rm, out=0.002), 'spine_03', 'crack', 20)
    add(K.surf_strip(cu, [(1.34, -91), (1.37, -101), (1.36, -110)], w=0.004, th=0.005, mod=rm, out=0.002), 'spine_03', 'crack', 20)
    add(K.surf_strip(cu, [(1.285, -89), (1.30, -80), (1.33, -74)], w=0.004, th=0.005, mod=rm, out=0.002), 'spine_03', 'crack', 20)
    pk = [(1.135, 0, 0.022, 0.18, 0.15), (1.17, 0, 0.02, 0.185, 0.154), (1.205, 0, 0.02, 0.172, 0.144)]
    add(K.torso_plate(pk, n=18, mod=K.ridge_mod(0.04, 0.3)), 'spine_02', 'plate2', 38)
    add(K.surf_strip(pk, [(1.14, -90), (1.17, -88.5), (1.20, -90.5)], w=0.007, th=0.006, mod=K.ridge_mod(0.04, 0.3), out=0.002), 'spine_02', 'crack', 20)
    add(K.torso_plate([(1.062, 0, 0.025, 0.182, 0.152), (1.10, 0, 0.025, 0.184, 0.154), (1.145, 0, 0.022, 0.17, 0.143)], n=18), 'spine_01', 'plate', 38)
    add(K.torso_plate([(0.945, 0, 0.03, 0.22, 0.188), (1.0, 0, 0.03, 0.206, 0.175), (1.072, 0, 0.026, 0.183, 0.154)], n=18), 'pelvis', 'plate2', 38)
    add(band((0, 0.026, 1.062), 0.19, 0.16, 0.036, 0.012), 'pelvis', 'plate', 38)
    add(box(0.05, 0.016, 0.07, bevel=0.007).move(0, -0.138, 1.062), 'pelvis', 'plate2', 30)
    add(box(0.008, 0.008, 0.062).move(0, -0.149, 1.062), 'pelvis', 'crack', 20)
    add(K.torso_plate([(1.482, 0, 0.012, 0.13, 0.12), (1.53, 0, 0.004, 0.10, 0.104), (1.575, 0, -0.004, 0.092, 0.10)], n=14), 'neck_01', 'plate2', 38)
    add(box(0.007, 0.008, 0.085).rot('x', 6).move(0, -0.103, 1.53), 'neck_01', 'crack', 20)

    # ------------------------------------------------------------------ the faceless helm, its crack, the crown of spires and dead candles
    hs = [(1.548, 0, -0.012, 0.096, 0.112), (1.60, 0, -0.016, 0.108, 0.127), (1.68, 0, -0.018, 0.113, 0.134), (1.76, 0, -0.016, 0.106, 0.126), (1.83, 0, -0.012, 0.088, 0.105),
          (1.885, 0, -0.01, 0.058, 0.07), (1.912, 0, -0.01, 0.014, 0.017)]
    add(K.torso_plate(hs, n=24).subdiv(1), 'Head', 'plate', 50)
    hc = [(1.905, -90), (1.86, -92), (1.80, -88.5), (1.74, -91.5), (1.68, -89), (1.62, -91), (1.565, -90)]
    add(K.surf_strip(hs, hc[::-1], w=0.008, th=0.006, out=0.002), 'Head', 'crack', 20)
    add(K.surf_strip(hs, [(1.74, -91.5), (1.76, -103), (1.79, -108)], w=0.004, th=0.005, out=0.002), 'Head', 'crack', 20)
    add(K.surf_strip(hs, [(1.66, -89), (1.645, -78), (1.655, -70)], w=0.004, th=0.005, out=0.002), 'Head', 'crack', 20)
    zc = 1.80
    pb, nb = K.loft_point(hs, zc, 0)
    rxc = float(pb[0]) + 0.006; ryc = rxc * 126.0 / 106.0
    add(band((0.0, -0.014, zc), rxc, ryc, 0.034, 0.012, n=24), 'Head', 'plate2', 38)
    hts = [0.40, 0.25, 0.33, 0.22, 0.36, 0.26, 0.30, 0.22, 0.34, 0.24, 0.31]
    for k in range(11):
        a = math.radians(-90 + 360 * (k + 0.5) / 11)
        base = np.array([rxc * math.cos(a), -0.014 + ryc * math.sin(a), zc + 0.01])
        tp, tw = KC.candle(0.0115, hts[k], seed=60 + k, melt=0.35)                   # the spires are tall tapers of black wax, long since snuffed
        tp.v[:, 0] += 0.012 * np.sin(tp.v[:, 2] / hts[k] * 2.6 + k)                    # none of them stands quite straight
        tw.v[:, 0] += 0.012 * math.sin(2.6 + k)
        for q, lk_, sm_ in ((tp, 'waxb', 60), (tw, 'wick', 40)):
            add(q.rot('x', 7).rot('z', math.degrees(a) + 90).move(base), 'Head', lk_, sm_)
        a2 = math.radians(-90 + 360 * k / 11)                                        # between the spires: a snuffed stub and its wax
        b2 = np.array([(rxc + 0.012) * math.cos(a2), -0.014 + (ryc + 0.012) * math.sin(a2), zc + 0.012])
        c, wick = KC.candle(0.013, 0.03 + 0.05 * ((k * 5) % 4) / 3.0, seed=40 + k, melt=0.9)
        add(c.move(b2), 'Head', 'wax', 60); add(wick.move(b2), 'Head', 'wick', 40)
        if k % 3 == 1:
            add(tube([b2, b2 + (0.006 * math.cos(a2), 0.006 * math.sin(a2), -0.05), b2 + (0.004 * math.cos(a2), 0.004 * math.sin(a2), -0.10)], [0.008, 0.006, 0.009], n=5), 'Head', 'wax', 60)

    # ------------------------------------------------------------------ arms (left, mirrored): spired pauldrons, rerebrace, couter, vambrace
    lm = KX.lame(0.15, 0.10, n=14)
    add(lm.copy().rot('y', 16).move(0.222, 0.07, 1.478), 'clavicle_l', 'plate', 38, mirror=True)
    add(lm.copy().scale((0.80, 0.86, 0.72)).rot('y', 46).move(0.31, 0.076, 1.452), 'upperarm_l', 'plate2', 38, mirror=True)
    add(lm.copy().scale((0.66, 0.78, 0.6)).rot('y', 66).move(0.352, 0.078, 1.428), 'upperarm_l', 'plate', 38, mirror=True)
    add(horn(M.polyline([(0.20, 0.07, 1.54), (0.25, 0.075, 1.63), (0.31, 0.085, 1.73), (0.335, 0.10, 1.82)], 8), 0.05, n=6, r_tip=0.004, r2=0.03, power=0.9), 'clavicle_l', 'plate2', 30, mirror=True)
    add(horn(M.polyline([(0.27, 0.10, 1.52), (0.34, 0.12, 1.60), (0.40, 0.14, 1.70)], 6), 0.032, n=5, r_tip=0.004, r2=0.02), 'clavicle_l', 'plate', 30, mirror=True)
    add(K.limb_plate('upperarm_l', [(0.44, 0.09, 0.088), (0.66, 0.087, 0.085), (0.9, 0.08, 0.078)], n=10, off=(0, 0.012, -0.008), up=(0, 0, 1), lip=0.006), None, 'plate', 38, mirror=True)
    add(ellipsoid(0.068, 0.072, 0.072, n=10, m=6).move(0.468, 0.108, 1.432), 'lowerarm_l', 'plate2', 40, mirror=True)
    add(M.cone_spike(0.03, 0.11, n=6).rot('x', -90).move(0.468, 0.165, 1.432), 'lowerarm_l', 'plate2', 30, mirror=True)
    add(K.limb_plate('lowerarm_l', [(0.1, 0.077, 0.073), (0.4, 0.073, 0.067), (0.76, 0.059, 0.055)], n=10, off=(0, 0.014, -0.008), up=(0, 0, 1), lip=0.005), None, 'plate', 38, mirror=True)
    add(K.limb_plate('lowerarm_l', [(0.74, 0.06, 0.056), (0.86, 0.068, 0.064), (0.985, 0.08, 0.074)], n=10, off=(0, 0.012, -0.006), up=(0, 0, 1)), None, 'plate2', 38, mirror=True)

    # ------------------------------------------------------------------ legs (left, mirrored)
    for (z0, z1, r0, r1, lk) in ((0.945, 0.80, 0.126, 0.118, 'plate'), (0.83, 0.66, 0.115, 0.102, 'plate2')):
        ts = K.shell_arc([(z1, 0.089, -0.004, r1, r1 * 1.06), ((z0 + z1) / 2, 0.089, -0.004, (r0 + r1) / 2 + 0.004, (r0 + r1) / 2 * 1.06 + 0.004), (z0, 0.089, -0.004, r0, r0 * 1.06)],
                         -135, 25, n=8, th=0.012)
        add(ts, 'thigh_l', lk, 38, mirror=True)
    add(K.limb_plate('thigh_l', [(0.10, 0.108, 0.094), (0.5, 0.10, 0.09), (0.88, 0.082, 0.08)], n=10, off=(0.004, -0.004, 0), lip=0.005), None, 'plate', 38, mirror=True)
    add(ellipsoid(0.066, 0.058, 0.08, n=10, m=6).move(0.094, -0.05, 0.535), 'calf_l', 'plate2', 40, mirror=True)
    add(M.cone_spike(0.024, 0.09, n=5).rot('x', 90).rot('x', -25).move(0.094, -0.10, 0.55), 'calf_l', 'plate2', 30, mirror=True)
    add(K.limb_plate('calf_l', [(0.1, 0.08, 0.074), (0.3, 0.086, 0.078), (0.72, 0.064, 0.06), (0.96, 0.062, 0.06)], n=10, off=(0.011, 0.01, 0), lip=0.005), None, 'plate', 38, mirror=True)
    sab = tube([(0.108, 0.088, 0.046), (0.108, 0.02, 0.06), (0.108, -0.06, 0.046), (0.108, -0.116, 0.037)], [0.046, 0.06, 0.046, 0.037], n=10,
               r2=[0.05, 0.058, 0.064, 0.064], up=(0, 0, 1), power=2.8)
    add(sab, 'foot_l', 'plate2', 38, mirror=True)
    toe = tube([(0.108, -0.108, 0.035), (0.108, -0.18, 0.031), (0.108, -0.26, 0.02)], [0.035, 0.031, 0.01], n=10, r2=[0.064, 0.05, 0.012], up=(0, 0, 1), power=2.6)
    add(toe, 'ball_l', 'plate', 38, mirror=True)

    # ------------------------------------------------------------------ tabards and the long ragged double mantle (spine -> hips -> thighs)
    for (a0, a1, ln, sd) in ((-112, -68, 0.62, 2), (62, 118, 0.56, 4)):
        top, nr = arc_pts((0, 0.028, 0.95), 0.225, 0.19, a0, a1, 7)
        fl = hang(top, ln, rows=8, out=nr, flare=0.04, rag=0.35, seed=sd, wave=0.02, holes=0.15, tatter=0.3)
        M.weights_by(fl, fl.v[:, 2], [(0.45, {'thigh_l': 0.4, 'thigh_r': 0.4, 'pelvis': 0.2}), (0.93, {'pelvis': 1.0})])
        C.add(fl, look='mantle2')
    for (wt, wb, ln, y, sd, lk, rg) in ((0.70, 1.22, 1.36, 0.238, 5, 'mantle', 0.42), (0.62, 1.0, 1.02, 0.262, 9, 'mantle2', 0.5)):
        cp = K.cape_pleated(w_top=wt, w_bot=wb, length=ln, z_top=1.52, y=y, cols=19, rows=14, pleats=5.6, amp=0.05, flare=0.26, bow=0.10, rag=rg, seed=sd, holes=0.14, slope=0.04)
        KC.cape_weights(cp, z_top=1.52, z_hip=1.0, z_knee=0.5, thigh=0.45)
        C.add(cp, look=lk)
    sh = KC.capelet(z_top=1.575, z_bot=1.33, r_top=(0.115, 0.125), r_bot=(0.37, 0.27), n=32, rows=6, rag=0.09, seed=8)
    sh = sh.sub(sh.face_centers()[:, 1] > 0.035)                                     # only behind the shoulders: the chest and its crack stay bare
    KC.skin_cloth(sh, smooth=3)
    C.add(sh, look='mantle2')
    for sx in (1, -1):
        add(K.disc(0.036, 0.016, n=10, bevel=0.004).move(sx * 0.20, 0.15, 1.49), 'spine_03', 'plate2', 38)

    glass_blade(C)
    return C, ("Towering regal knight of the Hush, rigid void-black plates (each on one bone) over a dark under-suit: fluted cuirass, "
               "plackart, faulds, gorget, triple pauldrons with tall swept spires, spiked couters / poleyns; a faceless elongated helm "
               "split by one cold-white crack (emis_crack) that continues down gorget, breastplate and belt, with hairline branches; a "
               "crown band of eleven thin spires with snuffed candle stubs and wax between them; tabards; a double ragged mantle to above "
               "the ankles on cape weights (spine_03 / spine_02 / spine_01 / pelvis / thighs); wpn_blade: great chipped blade of dull "
               "black glass with a horned guard.")
