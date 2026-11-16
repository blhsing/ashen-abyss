"""mon_sand_revenant 砂葬者 — a desiccated blade-dancer of the buried brass city: upright and whip-thin, wound in
sun-bleached linen (torn open over black, dried flesh), a coiled head-wrap with two long tails, loose strip ends that
stream from head, hips and elbows, and brass rings everywhere — stacked on the neck and the left arm, at wrist and
ankles, one big ring over the breastbone, small ones knotted to the strip ends. Curved brass sword with rings along its
spine. Faint violet eyes in the slit of the head-wrap."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_c as K
import kit_wisp as KW
import kit_ice as KI
from monlib import V, tube, ellipsoid, box, join, blade, band, revolve, hang, arc_pts, torus, sheet, unit


def sabre(C, obj='wpn_sabre'):
    """grip space: +Y along the blade, +X = cutting edge, origin in the fist"""
    P = []
    grip = tube([(0, -0.075, 0), (0, -0.02, 0), (0, 0.062, 0)], [0.0125, 0.0138, 0.012], n=7)
    P.append((grip, 'grip', 50))
    pommel = torus(0.024, 0.0065, n=12, m=5).move(0, -0.098, 0)
    P.append((pommel, 'brass', 60))
    guard = ellipsoid(0.040, 0.012, 0.022, n=10, m=5).move(0.004, 0.072, 0)
    P.append((guard, 'brass', 40))
    st = [(0.078, 0.014, 0.020, 0.0075), (0.20, 0.015, 0.026, 0.007), (0.44, 0.016, 0.037, 0.0062), (0.64, 0.018, 0.048, 0.0056),
          (0.75, 0.017, 0.044, 0.005), (0.83, 0.008, 0.022, 0.0035), (0.875, 0.0, 0.002, 0.001)]
    bl = blade(st)
    rings = []
    for y in (0.33, 0.45, 0.57):
        wl = float(np.interp(y, [s[0] for s in st], [s[1] for s in st]))
        rings.append(torus(0.021, 0.0038, n=10, m=4).rot('x', 90).move(-wl - 0.007, y, 0))
    rg = join(rings)
    for p in (bl, rg):
        p.bend('y', 'z', 34.0, origin=0.078)            # sweeps back, away from the edge
    P.append((bl, 'blade', 22))
    P.append((rg, 'brass', 60))
    for p, lk, sm in P:
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=sm)


def section(body, bone, t, dt=0.10, thresh=0.25):
    """centroid (on the plane at fraction t of the bone), radial distances and axis of the body slice there"""
    a, d, ln = KI.limb_frame(bone)
    w = body.W[:, M.RIG.idx[bone]]
    rel = body.v - a
    s = rel @ d / ln
    m = (np.abs(s - t) < dt) & (w > thresh)
    p0 = a + d * ln * t
    if m.sum() < 4:
        return p0, np.array([0.04]), d
    pts = body.v[m]
    cen = pts.mean(0)
    cen = cen - d * ((cen - p0) @ d)
    q = pts - cen
    rad = np.linalg.norm(q - np.outer(q @ d, d), axis=1)
    return cen, rad, d


def bangle(body, bone, t, thick=0.011, pad=0.012, n=12, m=5, pct=92, dt=0.10):
    """loose metal ring round a limb: centred on the slice centroid, wide enough to clear its widest side"""
    cen, rad, d = section(body, bone, t, dt)
    r = float(np.percentile(rad, pct)) + pad + thick
    tr = torus(r, thick, n=n, m=m)
    tr.tf(M.rot_to((0, 0, 1), d))
    tr.move(cen)
    tr.smooth = 60.0
    return tr


def ribbon(body, surf, bone, turns, t0, t1, width=0.034, lift=0.014, phase=0.0):
    """flat bandage wound in a helix round a limb (rigid on its bone); every point is seated on the real surface by a
    ray from the slice centroid, so it also follows limbs whose flesh is not centred on the bone"""
    a, d, ln = KI.limb_frame(bone)
    fr = M.frame_from(d)
    X = fr[:, 0]; Z = fr[:, 2]
    pts = int(10 * turns) + 2
    cs = []; rs = []; dv = []
    for i in range(pts):
        u = i / (pts - 1)
        cen, rad, _ = section(body, bone, t0 + (t1 - t0) * u)
        ang = phase + turns * 2 * math.pi * u
        dr = math.cos(ang) * X + math.sin(ang) * Z
        hit, _ = surf.ray(cen, dr)
        rr = float(np.linalg.norm(hit - cen)) if hit is not None else float(np.median(rad))
        cs.append(cen); rs.append(rr); dv.append(dr)
    rs = np.array(rs)
    med = float(np.median(rs))
    rs = np.clip(rs, med * 0.6, med * 1.6)
    rs = np.convolve(np.r_[rs[0], rs, rs[-1]], [0.25, 0.5, 0.25], mode='valid')
    G = np.zeros((pts, 2, 3))
    for i in range(pts):
        c = cs[i] + dv[i] * (rs[i] + lift)
        G[i, 0] = c - d * width / 2
        G[i, 1] = c + d * width / 2
    p = sheet(G)
    p.bone = bone
    p.smooth = 180.0
    return p

def build():
    C = M.Creature('mon_sand_revenant', 15000)
    C.look('skin', 'skin', 'skin_dry', color=(0.15, 0.10, 0.068), blotch=(0.07, 0.048, 0.036), vein=(0.04, 0.028, 0.024), vein_amt=0.6,
           vein_scale=16.0, sore_amt=0.0, hands_dark=0.5, dark=(0.04, 0.028, 0.024), bump=0.9, rough=0.62)
    wr = dict(color=(0.50, 0.435, 0.315), dark=(0.11, 0.08, 0.055), stain=(0.33, 0.215, 0.10), stain_amt=0.55)
    C.look('wrap_v', 'wraps', 'cloth_wrap', axis=(0.16, 0.05, 1.0), bands=23.0, **wr)
    C.look('wrap_h', 'wraps', 'cloth_wrap', axis=(1.0, 0.12, 0.16), bands=25.0, **wr)
    C.look('roll', 'wraps', 'cloth_wrap', axis=(0.9, 0.2, 0.4), bands=60.0, gap_w=0.16, band_shade=0.5,
           **dict(wr, color=(0.55, 0.48, 0.35)))
    C.look('strip', 'cloth', 'cloth_wrap', color=(0.47, 0.405, 0.29), dirt=(0.27, 0.19, 0.10), hem_lo=0.1, hem_hi=0.9, hem_amt=0.7,
           fold=16.0, stain_amt=0.4, streak=0.5, fray=0.5)
    C.look('brass', 'metal', 'metal_brass', **dict(L.PRESET['brass'], amount=0.38))
    C.look('blade', 'metal', 'metal_brass', **dict(L.PRESET['brass'], amount=0.25, edge=1.0, rough=0.28, color=(0.60, 0.44, 0.16)))
    C.look('grip', 'wraps', 'cloth_wrap', axis=(0.152, 0.987, 0.05), bands=70.0, **dict(wr, color=(0.30, 0.20, 0.12)))
    C.look('eye', 'emis', 'emis_eye', emit=(0.55, 0.28, 1.0), strength=1.9, mottle=0.2, base_mul=0.6)
    R = M.RIG

    # ------------------------------------------------------------------ the dried body (scales only: it stands straight)
    b = M.load_body('male')
    b = M.decimate(b, 0.6)
    KF.gaunt_face(b, 1.1)
    b.dent(KF.FACE['nose'], 0.028, 0.016, direction=(0, 1, 0.2))
    for sx in (1, -1):
        b.dent((sx * 0.088, -0.01, 1.66), 0.036, 0.02, direction=(-sx, 0, 0))
    pose = {
        'pelvis': dict(scale=(0.86, 0.82, 1.0)),
        'spine_01': dict(scale=(0.76, 0.70, 1.0)),
        'spine_02': dict(scale=(0.80, 0.74, 1.0)),
        'spine_03': dict(scale=(0.92, 0.86, 1.0)),
        'neck_01': dict(scale=(0.74, 0.74, 1.0)),
    }
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(scale=(1.0, 0.88, 0.88))
        pose['upperarm_' + s] = dict(scale=(1.0, 0.66, 0.66))
        pose['lowerarm_' + s] = dict(scale=(1.0, 0.66, 0.66))
        pose['thigh_' + s] = dict(scale=(0.70, 0.70, 1.0))
        pose['calf_' + s] = dict(scale=(0.70, 0.70, 1.0))
    M.pose_sculpt(b, pose)
    dz = KA.ground(b)
    v = b.v
    W = b.wsum

    def rig(bone, p, look, smooth=None, mirror=False):
        p.v[:, 2] -= dz
        return C.add(p, bone=bone, look=look, smooth=smooth, mirror=mirror)

    # ------------------------------------------------------------------ wrapped / bare regions
    w_hand = W(R.chain('hand_l')) + W(R.chain('hand_r'))
    w_torso = W(['pelvis', 'spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r'])
    w_legs = W(['thigh_l', 'thigh_r', 'calf_l', 'calf_r'])
    w_arms = W(['upperarm_l', 'upperarm_r', 'lowerarm_l', 'lowerarm_r'])
    nz = np.array([M.fbm(p, 6.5, 4.0) for p in v])
    nz2 = np.array([M.fbm(p, 15.0, 9.0) for p in v])
    wm = np.ones(len(v), dtype=bool)
    wm &= ~(w_hand > 0.5)
    wm &= ~(W(['neck_01']) > 0.55)
    wm &= ~((W(['lowerarm_r']) > 0.5) & (v[:, 0] < -0.535 + 0.02 * nz2))                # bare sword forearm
    wm &= ~((W(['Head']) > 0.5) & (np.abs(v[:, 2] - 1.668) < 0.0155) & (v[:, 1] < -0.045))   # eye slit
    wm &= ~((nz > 0.26) & ((w_torso > 0.5) | (w_legs > 0.5)) & (v[:, 2] > 0.30))      # torn open
    wm &= ~((W(['upperarm_r']) > 0.5) & (nz > 0.12))
    nb = KA.adjacency(b)
    inner = np.array([wm[i] and all(wm[j] for j in nb[i]) for i in range(len(v))])
    skin = b.sub(~b.vmask_faces(inner, 'all'))
    C.add(skin, look='skin', smooth=180)
    wrap = b.sub(b.vmask_faces(wm, 'all'))
    wrap.inflate(0.0065 + 0.003 * (np.array([M.fbm(p, 30.0, 2.0) for p in wrap.v]) > 0))
    wrap.v[:, 2] = np.maximum(wrap.v[:, 2], 0.0015)
    arm_f = wrap.vmask_faces(wrap.wsum(['upperarm_l', 'upperarm_r', 'lowerarm_l', 'lowerarm_r']) > 0.5, 'all')
    C.add(wrap.sub(arm_f), look='wrap_h', smooth=180)
    C.add(wrap.sub(~arm_f), look='wrap_v', smooth=180)

    # eyes deep in the slit
    eyes = join([ellipsoid(0.0095, 0.006, 0.0075, n=8, m=5).move(sx * 0.031, -0.0935, 1.668) for sx in (-1, 1)])
    rig('Head', eyes, 'eye')

    # ------------------------------------------------------------------ head-wrap: three coils, cap, two long tails
    for (z, rx, ry, r, tx, ty) in ((1.716, 0.099, 0.116, 0.0225, 9, 3), (1.750, 0.087, 0.101, 0.021, -8, -4), (1.779, 0.060, 0.070, 0.020, 6, 5)):
        co = torus(rx, r, n=16, m=6, ry=ry).rot('x', tx).rot('y', ty).move(0, -0.012, z)
        rig('Head', co, 'roll', 60)
    rig('Head', ellipsoid(0.052, 0.060, 0.022, n=10, m=5).move(0, -0.012, 1.793), 'roll', 60)
    rig('Head', ellipsoid(0.03, 0.034, 0.03, n=8, m=5).move(0.045, 0.092, 1.715), 'roll', 60)        # knot
    for k, (sx, ln, wd) in enumerate(((1, 0.80, 0.075), (-0.4, 0.62, 0.06))):
        tl = KW.trail((0.045 + sx * 0.018, 0.105, 1.705), (sx * 0.14, 0.24, -0.96), ln, width=wd, rows=9, droop=0.12, wave=0.022,
                      seed=3 + k, taper=0.45)
        M.weights_by(tl, tl.v[:, 2], [(0.95, {'pelvis': 1.0}), (1.15, {'spine_01': 0.5, 'spine_02': 0.5}), (1.38, {'spine_03': 1.0}),
                                      (1.56, {'neck_01': 0.5, 'spine_03': 0.5}), (1.68, {'Head': 1.0})])
        tl.v[:, 2] -= dz
        C.add(tl, look='strip')
        end = tl.v[-2]
        rg = torus(0.024, 0.006, n=10, m=4).rot('y', 90).rot('z', 25 * sx).move(end + np.array([0, 0.004, -0.022]))
        C.add(KA.stick(rg, tl, len(tl.v) - 2), look='brass', smooth=60)
    # face veil: a torn curtain of linen from just under the eye slit to the collar rings (rigid on the head)
    top, nr = arc_pts((0, -0.02, 1.653), 0.092, 0.117, -158, -22, 11)
    vl = hang(top, 0.20, rows=6, out=nr, flare=0.0, rag=0.30, seed=6, wave=0.011, tatter=0.35, taper=0.16)
    rig('Head', vl, 'strip')
    rig('Head', band((0, -0.02, 1.655), 0.096, 0.120, 0.016, 0.006, n=18, a0=-172, a1=-8), 'roll', 60)
    # brass on the head-wrap: a ring standing on the brow, hoops at the temples
    rig('Head', torus(0.024, 0.006, n=12, m=4).rot('x', 90).move(0, -0.137, 1.722), 'brass', 60)
    for sx in (1, -1):
        rig('Head', torus(0.030, 0.0055, n=12, m=4).rot('y', 90).rot('z', sx * 15).move(sx * 0.108, -0.035, 1.672), 'brass', 60)

    # ------------------------------------------------------------------ neck rings (stacked, widening downwards)
    for t in (0.05, 0.30, 0.55, 0.80):
        rig('neck_01', bangle(b, 'neck_01', t, thick=0.0125, pad=0.004 + 0.012 * (1 - t), n=14, m=5, pct=85, dt=0.18), 'brass', 60)

    # ------------------------------------------------------------------ chest: crossed strips and the breast ring; waist sash
    gx, gy, grx, gry = KI.girth(wrap, 1.33, 0.03, xlim=0.20)
    for tl_ in (-29, 29):
        C.add(band((gx, gy, 1.325), grx + 0.012, gry + 0.012, 0.042, 0.007, tilt_y=tl_, n=22), bone='spine_03', look='strip')
    yf = gy - gry - 0.017
    C.add(torus(0.058, 0.011, n=16, m=5).rot('x', 90).move(gx, yf - 0.004, 1.325), bone='spine_03', look='brass', smooth=60)
    C.add(ellipsoid(0.02, 0.012, 0.02, n=8, m=5).move(gx, yf, 1.325), bone='spine_03', look='brass')
    wx, wy, wrx, wry = KI.girth(wrap, 1.03, 0.03, xlim=0.30)
    for (z, r, tx) in ((1.045, 0.021, 5), (1.008, 0.024, -6)):
        C.add(torus(wrx + 0.014, r, n=20, m=6, ry=wry + 0.014).rot('x', tx).move(wx, wy, z), bone='pelvis', look='roll', smooth=60)
    bx = wx - (wrx + 0.02) * 0.62; by = wy - (wry + 0.02) * 0.80
    C.add(torus(0.036, 0.0085, n=14, m=5).rot('x', 90).rot('z', -35).move(bx, by - 0.012, 1.02), bone='pelvis', look='brass', smooth=60)

    # hanging strips: front and back tabs, long side streamers with rings
    top, nr = arc_pts((wx, wy, 1.0), wrx + 0.012, wry + 0.012, -122, -64, 6)
    f1 = hang(top, 0.52, rows=7, out=nr, flare=0.03, rag=0.25, seed=4, wave=0.014, holes=0.12, tatter=0.3, taper=0.25)
    M.weights_by(f1, f1.v[:, 2], [(0.55, {'thigh_l': 0.45, 'thigh_r': 0.45, 'pelvis': 0.1}), (0.98, {'pelvis': 1.0})])
    C.add(f1, look='strip')
    top, nr = arc_pts((wx, wy, 1.0), wrx + 0.012, wry + 0.012, 58, 118, 6)
    f2 = hang(top, 0.66, rows=8, out=nr, flare=0.07, rag=0.3, seed=11, wave=0.016, holes=0.14, tatter=0.35, taper=0.2)
    M.weights_by(f2, f2.v[:, 2], [(0.50, {'thigh_l': 0.45, 'thigh_r': 0.45, 'pelvis': 0.1}), (0.98, {'pelvis': 1.0})])
    C.add(f2, look='strip')
    for k, (ang, ln, sx) in enumerate(((-12, 0.62, 1), (196, 0.50, -1), (150, 0.74, -1))):
        a = math.radians(ang)
        base = np.array([wx + (wrx + 0.022) * math.cos(a), wy + (wry + 0.022) * math.sin(a), 1.015])
        st = KW.trail(base, (math.cos(a) * 0.16, math.sin(a) * 0.16 + 0.10, -0.97), ln, width=0.062, rows=8, droop=0.06, wave=0.02,
                      seed=20 + k, taper=0.4)
        th = 'thigh_l' if sx > 0 else 'thigh_r'
        M.weights_by(st, st.v[:, 2], [(0.50, {th: 0.9, 'pelvis': 0.1}), (0.80, {th: 0.6, 'pelvis': 0.4}), (0.99, {'pelvis': 1.0})])
        C.add(st, look='strip')
        rg = torus(0.022, 0.0055, n=10, m=4).rot('y', 90).rot('z', 40 * k).move(st.v[-2] + np.array([0, 0, -0.02]))
        C.add(KA.stick(rg, st, len(st.v) - 2), look='brass', smooth=60)

    # ------------------------------------------------------------------ limbs: helical bandages, stacked rings, elbow streamers
    surf = KB2.Surf(b)

    for bone, turns, t0, t1, ph in (('upperarm_l', 2.2, 0.30, 0.98, 0.3), ('lowerarm_l', 2.6, 0.04, 0.92, 1.9), ('upperarm_r', 1.8, 0.32, 0.92, 2.5),
                                    ('thigh_l', 2.3, 0.20, 0.95, 0.8), ('thigh_r', 2.1, 0.24, 0.92, 3.6), ('calf_l', 2.4, 0.08, 0.90, 2.2),
                                    ('calf_r', 2.6, 0.06, 0.92, 5.0)):
        rb = ribbon(b, surf, bone, turns, t0, t1, width=0.034, lift=0.014, phase=ph)
        rig(bone, rb, 'strip')
    for t in (0.36, 0.46, 0.56):                                    # left upper arm: three armlets
        rig('upperarm_l', bangle(b, 'upperarm_l', t, thick=0.0115, pad=0.014), 'brass', 60)
    for t in (0.74, 0.84):                                          # bare sword wrist: two bracelets
        rig('lowerarm_r', bangle(b, 'lowerarm_r', t, thick=0.010, pad=0.004), 'brass', 60)
    for s in ('l', 'r'):
        for t in ((0.84, 0.92) if s == 'l' else (0.88,)):
            rig('calf_' + s, bangle(b, 'calf_' + s, t, thick=0.011, pad=0.013), 'brass', 60)
    rig('thigh_r', bangle(b, 'thigh_r', 0.42, thick=0.012, pad=0.014, n=14), 'brass', 60)
    for sx, s, ln in ((1, 'l', 0.46), (-1, 'r', 0.34)):
        e = R.H('lowerarm_' + s)
        st = KW.trail(e + np.array([-sx * 0.02, 0.035, 0.0]), (sx * 0.93, 0.36, -0.05), ln, width=0.055, rows=7, droop=0.10, wave=0.02,
                      seed=30 + (sx > 0), taper=0.5, down=(0, 0.5, -0.86))
        rig('upperarm_' + s, st, 'strip')
        rg = torus(0.020, 0.005, n=10, m=4).rot('x', 90).move(st.v[-2] + np.array([sx * 0.018, 0, 0]))
        rig('upperarm_' + s, rg, 'brass', 60)

    sabre(C)
    return C, ("Whip-thin dried body sculpted from the CC0 base body (scales only, no baked posture). The linen is the body's own faces "
               "lifted off the skin (weights for free) with torn windows and a bare sword forearm, hands and neck; coiled head-wrap, "
               "neck / arm / wrist / ankle rings, breast ring, sash coils and limb bandage spirals are rigid single-bone parts; head-wrap "
               "tails, hip tabs and side streamers are skinned by height; the face veil is rigid on Head (rings on their ends ride on the end vertex weights); elbow "
               "streamers are rigid on the upper arms and point along T-pose +X so they hang when the arm is down. wpn_sabre: curved "
               "brass blade (0.88 m) with three loose rings on the spine and a ring pommel. emis_eye = faint violet eyes in the slit.")
