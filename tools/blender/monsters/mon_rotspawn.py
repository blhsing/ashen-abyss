"""mon_rotspawn 腐泥巨怪 — a mound of black peat that grew round a sleeper: a neckless, pear-shaped heap of mud, moss
and roots, heaviest at the bottom; arms like wet sacks that end in root-knots trailing tendrils; a drowned boardwalk
plank and a broken eel-trap stuck in its back among glowing bracket fungi; the sleeper's pale face shows low in the
chest."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_wisp as KW
import kit_ice as KI
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, hang, arc_pts, torus, horn, unit


def build():
    C = M.Creature('mon_rotspawn', 15000)
    R = M.RIG
    C.look('mud', 'mud', 'rock_mud', color=(0.048, 0.038, 0.028), dry=(0.135, 0.11, 0.075), dry_amt=0.65, bump=1.1, wet_rough=0.18)
    C.look('peat', 'mud', 'cloth_moss', color=(0.055, 0.042, 0.028), dry=(0.11, 0.09, 0.06), dry_amt=0.4, scale=9.0, bump=1.1)
    C.look('peat2', 'mud', 'cloth_moss', color=(0.04, 0.032, 0.024), dry=(0.15, 0.125, 0.085), dry_amt=0.7, scale=7.0, bump=1.2, wet_rough=0.16)
    C.look('moss', 'moss', 'cloth_moss', color=(0.042, 0.062, 0.022), light=(0.115, 0.135, 0.04), dark=(0.014, 0.02, 0.01), tips=0.45)
    C.look('root', 'wood', 'leather_root', color=(0.07, 0.052, 0.034), axis=(1, 0.05, 0.1), bump=1.2)
    C.look('rootv', 'wood', 'leather_root', color=(0.08, 0.06, 0.04), axis=(0.05, 0.3, 1), bump=1.2)
    pdir = unit((0.28, 0.55, 0.78)); tdir = unit((0.3, -0.75, -0.4))
    C.look('plank', 'wood', 'leather_root', color=(0.15, 0.125, 0.09), axis=tuple(pdir), bump=1.2)
    C.look('wicker', 'wood', 'leather_root', color=(0.19, 0.145, 0.07), axis=tuple(tdir))
    C.look('stalk', 'wood', 'leather_root', color=(0.30, 0.29, 0.19), axis=(0, 0, 1))
    C.look('nail', 'metal', 'leather_root', color=(0.12, 0.11, 0.10), amount=0.9)
    C.look('pale', 'skin', 'skin_sleeper', color=(0.58, 0.61, 0.55), blotch=(0.36, 0.41, 0.41), vein=(0.13, 0.19, 0.24), vein_amt=0.9, sore_amt=0.15,
           dry=0.3, grime=(0.03, 0.035, 0.02), grime_amt=0.55)
    C.look('void', 'flat', 'skin_sleeper', color=(0.012, 0.012, 0.01), rough=0.5)
    C.look('glow', 'emis', 'emis_fungus', emit=(0.78, 1.0, 0.16), strength=2.2, mottle=0.3, base_mul=0.7)
    C.look('capd', 'leather', 'leather_root', color=(0.085, 0.07, 0.03), scuff_color=(0.19, 0.20, 0.075), rough=0.34, scuff=0.6, stitch=0.0, scale=60.0)
    C.look('mudw', 'mud', 'rock_mud', color=(0.042, 0.034, 0.026), dry=(0.11, 0.09, 0.065), dry_amt=0.22, bump=1.2, wet_rough=0.15, scale=9.0)

    # ------------------------------------------------------------------ the heap (sculpted base body, weights kept)
    b = M.decimate(M.load_body('male'), 0.40)
    M.smooth_verts(b, 14, 0.6, mask=np.clip(b.wsum(['Head', 'neck_01']) * 1.6, 0, 1))          # the head melts into a dome
    M.smooth_verts(b, 3, 0.5, mask=np.clip(b.wsum(R.chain('hand_l') + R.chain('hand_r')), 0, 1))
    M.smooth_verts(b, 26, 0.6, mask=np.clip(b.wsum(['foot_l', 'foot_r', 'ball_l', 'ball_r']) * 2.0, 0, 1))      # feet become stumps
    pose = {
        'pelvis': dict(scale=(2.0, 2.05, 1.0)),
        'spine_01': dict(scale=(2.05, 2.3, 1.0)),
        'spine_02': dict(scale=(1.85, 2.05, 1.0), rot=('x', 6)),
        'spine_03': dict(scale=(1.7, 1.9, 1.0), rot=('x', 10)),
        'neck_01': dict(scale=(2.7, 2.4, 1.0), move=(0, 0, -0.07), rot=('x', 10)),
        'Head': dict(scale=(1.6, 1.5, 0.85), move=(0, 0, -0.07), rot=('x', -8)),
    }
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(rot=('x', -14), scale=(1.0, 1.9, 1.85))
        pose['upperarm_' + s] = dict(scale=(1.0, 2.15, 2.15))
        pose['lowerarm_' + s] = dict(scale=(1.0, 2.7, 2.7))
        pose['thigh_' + s] = dict(scale=(1.75, 1.85, 1.0))
        pose['calf_' + s] = dict(scale=(1.95, 1.95, 1.0))
        pose['foot_' + s] = dict(scale=(1.75, 1.4, 1.0))
    KA.hand_scale(pose, 1.55)
    M.pose_sculpt(b, pose)
    b.dent((0, -0.28, 0.98), 0.30, 0.13, direction=(0, -1, -0.45))                # sagging gut
    b.dent((0, 0.32, 1.42), 0.28, 0.17, direction=(0, 0.5, 0.85))                 # hump
    for sx in (-1, 1):
        b.dent((sx * 0.30, 0.10, 1.52), 0.20, 0.13, direction=(sx * 0.25, 0.1, 1))    # shoulders heaped above the head
        b.dent((sx * 0.62, 0.07, 1.36), 0.16, 0.07, direction=(0, 0, -1))             # the sack-arms sag
        b.dent((sx * 0.30, 0.0, 0.92), 0.22, 0.07, direction=(sx, 0, -0.3))           # hips spread
    b.rough(0.024, 5.0, seed=3)
    b.rough(0.010, 13.0, seed=5)
    dz = KA.ground(b)
    C.add(b, look='mud', smooth=180)
    S = KB2.Surf(b)
    H = KB2.Hunch(pose)

    def top(x, y, d=0.0):
        hit, _ = S.top(x, y)
        return np.array([x, y, (hit[2] if hit is not None else 1.5) + d])

    def back(x, z, d=0.0):
        hit, _ = S.back(x, z)
        return np.array([x, (hit[1] if hit is not None else 0.4) + d, z])

    # ------------------------------------------------------------------ clods heaped on shoulders and back (rigid, rounded)
    clods = []
    for (xy, r, bone, sd) in (((0.30, 0.10), (0.20, 0.19, 0.07), 'clavicle_l', 1), ((-0.31, 0.10), (0.21, 0.18, 0.075), 'clavicle_r', 2),
                              ((0.03, 0.30), (0.25, 0.21, 0.11), 'spine_03', 3), ((0.20, 0.36), (0.16, 0.14, 0.085), 'spine_03', 4),
                              ((-0.22, 0.34), (0.17, 0.15, 0.08), 'spine_02', 5)):
        c = top(xy[0], xy[1], -r[2] * 0.4)
        e = KX.lump(r[0], r[1], r[2], seed=sd, amp=0.45, freq=6.0)
        e.rough(0.012, 15.0, seed=sd + 9.0)
        C.add(e.move(c), bone=bone, look='mudw', smooth=180)
        clods.append((c, r, bone))

    # ------------------------------------------------------------------ moss: mats on everything that faces up, tufts hanging off the arms
    vn = b.normals()
    nz = np.array([M.fbm(p, 6.0, 4.0) for p in b.v])
    mm = (vn[:, 2] > 0.25 + 0.3 * nz) & (b.v[:, 2] > 0.95)
    C.add(KX.patch_shell(b, mm, 0.016), look='moss')
    mm2 = (nz > 0.18) & (b.v[:, 2] > 0.5) & (b.v[:, 2] < 1.3) & (b.v[:, 1] > -0.05)
    C.add(KX.patch_shell(b, mm2, 0.014), look='moss')

    # ------------------------------------------------------------------ slumped peat in two tiers: the heap is heaviest at the bottom
    cx, cy, rx, ry = KI.girth(b, 1.0, 0.05, xlim=0.6)
    rgs = np.random.default_rng(2)

    def tier(z_top, z_hem, k_top, k_bot, seed, rag, look, rows=6):
        prof = [(z_hem - 0.1, cx, cy, rx * k_bot, ry * k_bot), ((z_top + z_hem) / 2, cx, cy, rx * (k_top + k_bot) / 2 * 1.02, ry * (k_top + k_bot) / 2 * 1.02),
                (z_top, cx, cy, rx * k_top, ry * k_top)]
        hem = lambda th: z_hem + 0.05 * np.sin(np.radians(th) * 2 + seed) + 0.04 * np.sin(np.radians(th) * 5 + 2.0 * seed)
        sk, Gs = KW.drape(prof, hem, z_top, n=36, rows=rows, power=2.2, rag=rag, seed=seed, curve=1.0,
                          mod=KX.pleat_mod(seed=6 + seed, ks=(3, 5, 8), amp=0.10, hem_z=z_hem, top_z=z_top, base=0.5, sharp=1.0))
        sk.v[:36, 2] += rgs.uniform(-0.05, 0.05, 36)                               # no clean line: the top edge wanders
        KX.crumple(sk, 0.03, 5.0, float(seed), zscale=0.6)
        sk.rough(0.012, 14.0, seed=7 + seed)
        hemrow = sk.v[-36:]                                                         # thick, swollen lower lip
        hemrow[:, :2] += (hemrow[:, :2] - np.array([cx, cy])) * 0.05
        KC.skin_cloth(sk, body=b, smooth=4, pelvis_mix=0.55, mix_top=1.0, mix_bot=0.4)
        C.add(sk, look=look)
        return sk, sk.v.reshape(Gs.shape)

    sk0, G0 = tier(0.80, 0.14, 1.16, 1.34, 3, 0.07, 'peat', rows=5)
    sk1, G1 = tier(1.15, 0.60, 0.98, 1.27, 5, 0.22, 'peat2', rows=5)
    for sk, Gk, nr_ in ((sk0, G0, 5), (sk1, G1, 5)):
        for j in range(36):
            if rgs.uniform() < 0.4:                                                # matted moss strips slumping down the tiers
                i0 = int(rgs.integers(0, 3)); i1 = int(min(nr_, i0 + rgs.integers(2, 5)))
                for bl in KX.thatch(Gk, cols=[j], i0=i0, i1=i1, width=0.09, fill=1.3, lift=0.016, over=(0.02, 0.10), seed=j, vee=0.012, flare=0.0):
                    KB2.skin_like(bl, sk, smooth=0)
                    C.add(bl, look='moss', smooth=180)
    for k, a in enumerate((-70, -20, 35, 100, 160, 215)):                          # roots crawling down the skirt
        j = int(round(((a % 360) / 360.0) * 36)) % 36
        pts = [G1[i, j] for i in (0, 2, 4, 5)] + [G0[i, (j + (1 if k % 2 else -1)) % 36] for i in (3, 4, 5)]
        nrm = unit(np.array([pts[2][0] - cx, pts[2][1] - cy, 0.0]))
        rt = KA.root([p + nrm * 0.012 for p in pts], 0.026, 0.008, n=5, wob=0.01, seed=40 + k, pts=10)
        KB2.skin_like(rt, sk0, smooth=1)
        C.add(rt, look='rootv', smooth=60)

    # ------------------------------------------------------------------ the sleeper's face, low in the chest
    fb = M.load_body('male')
    KF.gaunt_face(fb, 0.7)
    fm = (fb.wsum(['Head']) > 0.5) & (fb.v[:, 1] < -0.052) & (fb.v[:, 2] > 1.568) & (fb.v[:, 2] < 1.752) & (np.abs(fb.v[:, 0]) < 0.074)
    face = M.decimate(fb.sub(fb.vmask_faces(fm, 'all')), 0.55)
    face.W = None
    face.move(0, 0.105, -1.655).scale(1.22).rot('x', -16).rot('z', 9).rot('y', -7)
    hit, nrm = S.front(0.035, 1.34)
    fc = hit + np.array([0.0, -0.028, 0.0])
    C.add(face.move(fc), bone='spine_02', look='pale', smooth=180)
    ring = tube(KX.surf_path(S, [fc + (0.118 * math.cos(a), -0.03, 0.142 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 9)], 0.002, 14),
                [0.026, 0.02, 0.028, 0.019, 0.026, 0.02, 0.03, 0.02, 0.025, 0.019, 0.028, 0.02, 0.024, 0.026], n=6)
    ring.rough(0.006, 20.0, seed=2)
    C.add(ring, bone='spine_02', look='mud', smooth=180)
    for sx in (-1, 1):
        C.add(ellipsoid(0.017, 0.012, 0.009, n=7, m=4).rot('x', -16).move(fc + (sx * 0.038, 0.004, 0.016)), bone='spine_02', look='void')

    # ------------------------------------------------------------------ roots: over the shoulders, round the arms, knotted fists, tendrils
    def root_on(pts, r0, r1, sd, n=6, npt=13, look='rootv', lift=None):
        P = KX.surf_path(S, pts, r0 * 0.55 if lift is None else lift, npt)
        rt = KA.root(P, r0, r1, n=n, wob=r0 * 0.25, seed=sd, pts=npt)
        KB2.skin_like(rt, b, smooth=1)
        C.add(rt, look=look, smooth=60)

    for sx in (-1, 1):
        root_on([(sx * 0.16, 0.50, 1.20), (sx * 0.30, 0.30, 1.72), (sx * 0.30, -0.15, 1.62), (sx * 0.20, -0.42, 1.30), (sx * 0.07, -0.50, 0.98)],
                0.034, 0.012, 3 + sx)
        root_on([(sx * 0.05, 0.55, 1.05), (sx * 0.36, 0.38, 1.02), (sx * 0.50, 0.0, 0.92), (sx * 0.34, -0.40, 0.84)], 0.03, 0.01, 7 + sx)
        root_on([(sx * 0.24, 0.35, 1.50), (sx * 0.45, 0.22, 1.52), (sx * 0.62, 0.12, 1.60), (sx * 0.78, -0.05, 1.50)], 0.03, 0.012, 11 + sx, look='root')
    root_on([(-0.20, -0.42, 1.36), (-0.05, -0.50, 1.22), (0.16, -0.5, 1.20), (0.28, -0.42, 1.02)], 0.022, 0.008, 21)     # across the sleeper's brow
    for sx, s in ((1, 'l'), (-1, 'r')):
        la = 'lowerarm_' + s
        a0 = R.H(la) + (0, 0, -dz); a1 = R.T(la) + (0, 0, -dz)
        r0 = KI.limb_radius(b, la, 0.2); r1 = KI.limb_radius(b, la, 0.85)
        sp = KA.spiral(a0 + (a1 - a0) * 0.08, a1 + (a1 - a0) * 0.05, r0 + 0.006, r1 + 0.008, turns=1.7 * sx, thick=0.026, thick1=0.014, n=6, pts=22, phase=1.0)
        sp.rough(0.006, 20.0, seed=3)
        C.add(sp, bone=la, look='root', smooth=60)
        hc = R.H('hand_' + s) + np.array([sx * 0.085, 0.004, -0.012 - dz])
        C.add(KX.lump(0.125, 0.115, 0.105, seed=8 + sx, amp=0.3, freq=14.0).move(hc), bone='hand_' + s, look='root', smooth=180)   # the root-knot
        for k in range(5):                                                         # trailing tendrils (hang down when the arm does)
            a = 1.3 * k + 0.4
            d = np.array([0.0, 0.07 * math.cos(a), 0.07 * math.sin(a)])
            ln = 0.16 + 0.05 * ((k * 7) % 3)
            t = KA.root([hc + d * 0.6, hc + d + (sx * ln * 0.5, 0, -0.02), hc + d * 0.7 + (sx * ln, 0.01 * k, -0.05)], 0.02, 0.004, n=5, wob=0.012, seed=k + 5, pts=7)
            C.add(t, bone='hand_' + s, look='root', smooth=60)
    KF.claws(C, 'root', body=b, length=0.13, curl=0.9, n=5)
    KF.toe_claws(C, 'rootv', length=0.07, r=0.022)

    # ------------------------------------------------------------------ what the mire left in its back: plank, eel-trap, lit fungi
    pb = back(0.14, 1.42)
    pl = KX.plank(0.98, 0.17, 0.036, seed=2).move(0, -0.30, 0)
    pl.orient(pdir, (0.5, -0.6, 0.4), pb + (0, -0.10, 0.02))
    C.add(pl, bone='spine_03', look='plank', smooth=25)
    for t_ in (0.12, 0.42):
        nl = tube([(0, 0, 0), (0, 0, 0.03)], 0.011, n=5).move(0.03, -0.30 + 0.98 * t_ + 0.3, 0.017)
        nl.orient(pdir, (0.5, -0.6, 0.4), pb + (0, -0.10, 0.02))
        C.add(nl, bone='spine_03', look='nail', smooth=40)
    tb = back(-0.24, 1.22)
    tr = KX.eel_trap(0.52, 0.15, ribs=10, hoops=3, seed=4)
    tr.orient(tdir, (0, 0, 1), tb + (-0.14, 0.34, 0.22))
    C.add(tr, bone='spine_02', look='wicker', smooth=50)
    # bracket fungi climbing the hump (their lit rims face up and back) + tall caps on the heap
    rng = np.random.default_rng(5)
    for k, (x, z, r) in enumerate(((-0.10, 1.52, 0.13), (0.14, 1.44, 0.105), (-0.02, 1.34, 0.14), (-0.22, 1.62, 0.09), (0.06, 1.20, 0.10))):
        p0 = back(x, z, -0.03)
        ax_, az_ = rng.uniform(4, 20), rng.uniform(-25, 25)
        bn_ = 'spine_03' if z > 1.3 else 'spine_02'
        for p_, lk_ in zip(KX.rim_split(KA.shelf(r, r * 0.26, n=10), r, 0.66, c=(0, r * 0.45)), ('capd', 'glow')):
            C.add(p_.rot('x', ax_).rot('z', az_).move(p0), bone=bn_, look=lk_, smooth=60)
    for k, (ci, off, Rc, st) in enumerate(((2, (0.04, -0.03), 0.105, 0.20), (2, (-0.12, 0.06), 0.07, 0.11), (3, (0.03, 0.03), 0.085, 0.15),
                                           (1, (-0.02, 0.08), 0.06, 0.10))):
        c, r, bone = clods[ci]
        base = c + np.array([off[0], off[1], r[2] * 0.7])
        cap, gill = KB2.fungus_cap(R=Rc, h=Rc * 0.36, n=12, lobes=0.16, seed=k)
        cap.rough(0.004, 22.0, seed=float(k))
        stalk = tube([(0, 0, -st - 0.03), (0.012, 0.006, -st * 0.5), (0, 0, -0.01)], [Rc * 0.24, Rc * 0.15, Rc * 0.26], n=6)
        tl = M.rot_axis('x', rng.uniform(-16, 16)) @ M.rot_axis('y', rng.uniform(-16, 16))
        capd, capg = KX.rim_split(cap, Rc, 0.70, under=False)
        for p, lk in ((capd, 'capd'), (capg, 'glow'), (gill, 'glow'), (stalk, 'stalk')):
            C.add(p.tf(tl).move(base + (0, 0, st)), bone=bone, look=lk, smooth=60)
    return C, ("Peat-mound hulk sculpted from the CC0 base body (weights kept; head melted into a neckless dome, pear-shaped trunk, sack "
               "arms with root-knot fists and trailing tendrils), rounded clods heaped on shoulders and hump, moss mats and a slumped peat "
               "skirt, roots seated on the surface, the sleeper's pale face (skin_sleeper) low in the chest; a boardwalk plank, a broken "
               "wicker eel-trap and glowing bracket fungi / caps (emis_fungus) on the back. No weapon.")
