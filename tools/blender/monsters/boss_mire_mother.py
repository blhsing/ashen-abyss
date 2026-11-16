"""boss_mire_mother 沼母・艾絲珂 — Esko the Mire-mother: a towering matriarch rooted where she stands. A gown of drowned cloth
that spreads into buttress roots at the base, a veil of moss over her eyes, long thin arms ending in root-claws, bark
creeping over pale drowned skin, and above her a crown of arching roots from which glowing fungal lanterns dangle (the
focal motif) - more of them hang from her shoulders and waist."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_wisp as KW
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, unit, horn, hang, arc_pts, band


def lantern(r=0.05, seed=0):
    """fungal lantern hanging from the origin: swollen lit bladder, dark cap, four dark veins. [(Part, look, smooth)]"""
    rng = np.random.default_rng(seed)
    bl = KX.bladder(r, n=9)
    cap, gill = KB2.fungus_cap(R=r * 0.95, h=r * 0.5, n=9, lobes=0.12, seed=seed)
    out = [(bl.move(0, 0, -r * 0.2), 'lamp', 80), (cap.move(0, 0, -r * 0.55), 'capd', 60)]
    for k in range(4):
        a = 1.57 * k + rng.uniform(-0.3, 0.3)
        pts = [(rr * r * math.cos(a), rr * r * math.sin(a), -r * 0.2 + z * r) for rr, z in ((0.34, -0.7), (0.86, -1.35), (1.04, -2.0), (0.84, -2.7), (0.2, -3.12))]
        out.append((tube(pts, r * 0.07, n=3), 'capd', 40))
    return out


def build():
    C = M.Creature('boss_mire_mother', 40000)
    R = M.RIG
    C.look('skin', 'skin', 'skin_drowned', color=(0.40, 0.45, 0.40), blotch=(0.20, 0.27, 0.25), vein=(0.07, 0.15, 0.13), vein_amt=1.0, vein_scale=9.0,
           wrinkle=1.3, sinew=1.4, sore_amt=0.3, hands_dark=0.8, dark=(0.05, 0.045, 0.03), hands_x0=0.5, hands_x1=0.85, bump=0.7, rough=0.38)
    C.look('gown', 'cloth', 'cloth_drown', color=(0.055, 0.075, 0.07), weave='linen', dirt=(0.05, 0.04, 0.028), hem_lo=0.0, hem_hi=0.9, hem_amt=0.95, fold=6.0,
           fold_amt=0.9, stain_amt=0.8, streak=0.7, fray=0.5, rough=0.7)
    C.look('wrap', 'cloth', 'cloth_drown', color=(0.13, 0.135, 0.10), weave='burlap', dirt=(0.05, 0.04, 0.028), hem_lo=0.9, hem_hi=1.5, fold=18.0, stain_amt=0.8)
    C.look('moss', 'moss', 'cloth_drown', color=(0.042, 0.062, 0.022), light=(0.115, 0.135, 0.04), dark=(0.014, 0.02, 0.01), tips=0.45)
    C.look('moss2', 'moss', 'cloth_drown', color=(0.07, 0.085, 0.03), light=(0.17, 0.18, 0.06), dark=(0.02, 0.03, 0.012), clump=60.0)
    C.look('root', 'wood', 'leather_root', color=(0.075, 0.055, 0.036), axis=(0.1, 0.1, 1), bump=1.3)
    C.look('rooth', 'wood', 'leather_root', color=(0.07, 0.052, 0.034), axis=(1, 0.05, 0.1), bump=1.3)
    C.look('capd', 'leather', 'leather_root', color=(0.085, 0.07, 0.03), scuff_color=(0.19, 0.20, 0.075), rough=0.34, scuff=0.6, stitch=0.0, scale=60.0)
    C.look('lamp', 'emis', 'emis_lantern', emit=(0.30, 0.60, 0.07), strength=2.6, mottle=0.9, base_mul=0.9, scale=40.0, rough=0.3)
    C.look('glow', 'emis', 'emis_lantern', emit=(0.80, 1.0, 0.18), strength=2.6, mottle=0.3, base_mul=0.7)
    C.look('void', 'flat', 'skin_drowned', color=(0.012, 0.01, 0.01), rough=0.5)
    fb = KC.ref_body('female')

    # ------------------------------------------------------------------ what shows of her: lower face, shoulders, long arms, clawed hands
    b = M.decimate(M.load_body('female'), 0.62)
    KF.gaunt_face(b, 0.8)
    KW.stretch_fingers(b, 1.75, 1.45)
    legs = R.chain('thigh_l') + R.chain('thigh_r')
    b = b.sub(b.vmask_faces((b.wsum(legs) < 0.5) & (b.v[:, 2] > 1.0), 'any'))
    sp = {}
    for s in ('l', 'r'):
        sp['upperarm_' + s] = dict(scale=(1.0, 0.84, 0.84)); sp['lowerarm_' + s] = dict(scale=(1.0, 0.78, 0.78))
    KA.hand_scale(sp, 1.18)
    M.pose_sculpt(b, sp)
    C.add(b, look='skin', smooth=180)
    KF.claws(C, 'rooth', body=b, length=0.17, curl=0.8, n=5)
    nz = np.array([M.fbm(p, 9.0, 3.0) for p in b.v])
    bark = (nz > 0.12) & (b.wsum(['Head']) < 0.3) & (b.wsum(R.chain('hand_l') + R.chain('hand_r')) < 0.5)
    C.add(KX.patch_shell(b, bark, 0.008, 0.003), look='rooth')                       # bark creeping over the skin
    C.add(ellipsoid(0.024, 0.01, 0.007, n=8, m=4).move(0, -0.113, 1.607), bone='Head', look='void')   # the mouth, a dark seam
    for sx, s in ((1, 'l'), (-1, 'r')):
        la = 'lowerarm_' + s
        a0 = R.H(la); a1 = R.T(la)
        sp_ = KA.spiral(a0 + (a1 - a0) * 0.1, a1, 0.042, 0.034, turns=2.4 * sx, thick=0.011, thick1=0.006, n=5, pts=26, phase=0.5)
        C.add(sp_, bone=la, look='rooth', smooth=60)

    # ------------------------------------------------------------------ the gown: drowned cloth pinched at the waist, spreading into buttress roots
    secs = [(0.0, 0, 0.02, 0.66, 0.62), (0.05, 0, 0.02, 0.54, 0.50), (0.22, 0, 0.02, 0.39, 0.36), (0.55, 0, 0.02, 0.275, 0.25), (0.85, 0, 0.025, 0.205, 0.182),
            (1.02, 0, 0.025, 0.152, 0.13), (1.12, 0, 0.02, 0.150, 0.126), (1.27, 0, 0.012, 0.168, 0.134)]
    n = 48
    pm = KX.pleat_mod(seed=5, ks=(6, 9, 14), amp=0.20, hem_z=0.0, top_z=1.0, base=0.06, sharp=0.55, twist=0.5)
    gown, Rg = KX.gown2(secs, n=n, mod=pm, rag=0.0)
    gown = M.subsurf(gown, 1)
    KX.crumple(gown, 0.008, 10.0, 2.0)
    gown.v[:, 2] = np.maximum(gown.v[:, 2], 0.0)
    KC.skin_cloth(gown, body=fb, smooth=5, pelvis_mix=0.75, mix_top=1.0, mix_bot=0.5)
    C.add(gown, look='gown')
    cen = np.array([0.0, 0.02])
    rng = np.random.default_rng(4)
    waist_pts = []
    for k in range(13):                                                             # roots down the gown and out over the ground
        j = int(round(k * n / 13 + rng.integers(-1, 2))) % n
        col = [Rg[i, (j + (1 if i in (2, 4) and k % 2 else 0)) % n].copy() for i in range(7)]
        pts = []
        for i, p in enumerate(col):
            d = unit(np.array([p[0] - cen[0], p[1] - cen[1], 0.0]))
            pts.append(p + d * 0.028)
        d0 = unit(np.array([col[0][0], col[0][1] - 0.02, 0.0]))
        ext = rng.uniform(0.10, 0.26)
        pts = [col[0] + d0 * ext + (0, 0, 0.012), col[0] + d0 * ext * 0.45 + (0, 0, 0.03)] + pts
        P = M.polyline(pts, 14)
        P[:, 2] = np.maximum(P[:, 2], 0.014)
        rr = np.interp(np.linspace(0, 1, 14), [0, 0.12, 0.3, 0.7, 1.0], [0.012, 0.04, 0.05, 0.028, 0.012]) * rng.uniform(0.75, 1.15)
        rt = tube(P, rr * (1 + 0.15 * np.sin(np.arange(14) * 2.1 + k)), n=6)
        rt.v[:, 2] = np.maximum(rt.v[:, 2], 0.0)
        KB2.skin_like(rt, gown, smooth=1)
        C.add(rt, look='root', smooth=60)
        waist_pts.append(pts[-2])
    for a, z, tl in ((20, 1.10, 12), (-18, 1.16, -10), (8, 1.22, 6)):               # root girdle
        C.add(band((0, 0.022, z), 0.162, 0.138, 0.03, 0.022, tilt_x=a * 0.3, tilt_y=tl, n=20), bone='spine_01', look='root', smooth=60)
    wr = (b.wsum(['spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r']) > 0.5) & (np.abs(b.v[:, 0]) < 0.19)
    C.add(KX.patch_shell(b, wr & (b.v[:, 2] < 1.455 + 0.035 * nz) & (b.v[:, 2] > 1.0), 0.011, 0.002), look='wrap')    # drowned bodice

    # ------------------------------------------------------------------ the veil of moss (covers the eyes; long down the back)
    def hem(th):
        d = lambda at, w: np.exp(-((((th - at + 180) % 360) - 180) / w) ** 2)
        return 1.53 + 0.15 * d(-90, 34) - 0.62 * d(90, 62)
    prof = [(0.8, 0, 0.03, 0.30, 0.26), (1.30, 0, 0.02, 0.27, 0.215), (1.46, 0, 0.012, 0.235, 0.18), (1.56, 0, -0.01, 0.135, 0.145), (1.68, 0, -0.02, 0.112, 0.128),
            (1.78, 0, -0.02, 0.085, 0.10), (1.83, 0, -0.02, 0.03, 0.035)]
    vl, Gv = KW.drape(prof, hem, 1.83, n=40, rows=10, power=2.0, rag=0.07, seed=3, curve=0.9,
                      mod=KX.pleat_mod(seed=2, ks=(7, 11), amp=0.07, hem_z=0.9, top_z=1.7, base=0.1))
    KX.crumple(vl, 0.006, 16.0, 4.0)
    wz = [(1.0, {'spine_01': 1.0}), (1.2, {'spine_02': 1.0}), (1.38, {'spine_03': 1.0}), (1.56, {'neck_01': 0.5, 'Head': 0.5}), (1.66, {'Head': 1.0})]
    M.weights_by(vl, vl.v[:, 2], wz)
    C.add(vl, look='moss')
    Gw = vl.v.reshape(Gv.shape)
    for bl in KX.thatch(Gw, i0=2, width=0.05, fill=1.3, lift=0.012, over=(0.02, 0.09), seed=5, vee=0.008, flare=0.01, taper=0.15):
        M.weights_by(bl, bl.v[:, 2], wz)
        C.add(bl, look='moss2' if rng.uniform() < 0.5 else 'moss', smooth=180)

    # ------------------------------------------------------------------ the crown of roots and its lanterns
    hc = np.array([0.0, -0.02, 1.76])
    C.add(torus(0.105, 0.02, n=16, m=5, ry=0.12).rot('x', -6).move(hc + (0, 0, 0.01)), bone='Head', look='root', smooth=60)
    C.add(torus(0.098, 0.016, n=16, m=5, ry=0.112).rot('x', 9).rot('z', 40).move(hc + (0, 0, 0.035)), bone='Head', look='root', smooth=60)

    def hang_lantern(p0, ln, r, bone, seed):
        p1 = np.asarray(p0, dtype=float) + np.array([0.0, 0.0, -ln])
        C.add(KX.cord(p0, p1, 0.0045), bone=bone, look='root', smooth=60)
        for q, lk, sm in lantern(r, seed):
            C.add(q.rot('z', seed * 37.0).move(p1), bone=bone, look=lk, smooth=sm)

    for k in range(7):
        a = math.radians(51.4 * k + 12 + rng.uniform(-8, 8))
        d = np.array([math.cos(a), math.sin(a), 0.0])
        out = rng.uniform(0.40, 0.62); up = rng.uniform(0.42, 0.72)
        pts = [hc + d * 0.07 + (0, 0, 0.02), hc + d * out * 0.3 + (0, 0, up * 0.55), hc + d * out * 0.72 + (0, 0, up), hc + d * out * 0.98 + (0, 0, up * 0.93),
               hc + d * out * 1.08 + (0, 0, up * 0.80)]
        C.add(KA.root(pts, 0.032, 0.008, n=6, wob=0.02, seed=20 + k, pts=13), bone='Head', look='root', smooth=60)
        hang_lantern(pts[-1], rng.uniform(0.06, 0.42), rng.uniform(0.042, 0.066), 'Head', k)
        tw = hc + d * out * 0.55 + (0, 0, up * 0.86)                                # a side twig with a lit bud
        e = tw + M.rot_axis('z', 55 if k % 2 else -55) @ d * 0.16 + (0, 0, 0.10)
        C.add(KA.root([tw, (tw + e) / 2 + (0, 0, 0.03), e], 0.014, 0.004, n=5, wob=0.008, seed=40 + k, pts=6), bone='Head', look='root', smooth=60)
        C.add(ellipsoid(0.016, 0.016, 0.022, n=7, m=4).move(e), bone='Head', look='glow', smooth=80)
        if k % 3 == 0:
            hang_lantern((tw + e) / 2, rng.uniform(0.10, 0.25), 0.036, 'Head', 10 + k)
    for k, p in enumerate(waist_pts[::2]):                                           # lanterns at the waist and shoulders
        d = unit(np.array([p[0], p[1] - 0.02, 0.0]))
        hang_lantern(np.array([p[0], p[1], 1.06]) + d * 0.05, rng.uniform(0.10, 0.34), rng.uniform(0.04, 0.058), 'pelvis', 20 + k)
    for sx, s in ((1, 'l'), (-1, 'r')):
        hang_lantern((sx * 0.215, 0.10, 1.40), 0.16, 0.046, 'clavicle_' + s, 30 + sx)
        hang_lantern((sx * 0.60, 0.068, 1.40), 0.10 + 0.05 * sx, 0.04, 'lowerarm_' + s, 33 + sx)
    return C, ("Towering rooted matriarch: head / shoulders / long thin arms of the CC0 female base body (weights kept, fingers stretched) "
               "with bark patches and long root claws, a pinched gown of drowned cloth whose pleats spread into buttress folds with root "
               "tubes running down and out over the ground (mostly pelvis-weighted), drowned bodice, root girdle, a veil of moss in tufts "
               "covering the eyes and falling down the back, and a crown of arching roots (rigid on Head) hung with glowing fungal "
               "lanterns (emis_lantern); more lanterns at waist, shoulders and forearms. No weapon.")
