"""Articulated skeleton kit: every piece is a rigid Part bound to exactly one bone of the UAL rig.
Used by the Bone family (skeleton, thrower, bishop, frostbound ...)."""
import math
import numpy as np
import monlib as M
from monlib import V, tube, ellipsoid, loft_z, box, revolve, join, rock, horn, lerp

SKULL_ORIGIN = np.array([0.0, -0.015, 1.705])


def seg(p0, p1, r0, r1=None, k0=1.5, k1=1.5, n=5, up=None, r2s=1.0):
    """long bone: shaft p0 -> p1 with swollen, rounded ends"""
    r1 = r0 if r1 is None else r1
    p0 = V(p0); p1 = V(p1)
    ts = [0.0, 0.045, 0.14, 0.5, 0.86, 0.955, 1.0]
    rs = np.array([r0 * k0 * 0.55, r0 * k0, r0 * 1.02, (r0 + r1) / 2 * 0.9, r1 * 1.02, r1 * k1, r1 * k1 * 0.55])
    return tube([lerp(p0, p1, t) for t in ts], rs, n=n, r2=rs * r2s, up=up)


def phal(p0, p1, r, n=4, tip=False):
    p0 = V(p0); p1 = V(p1)
    if tip:
        p1 = p0 + (p1 - p0) * 1.3
        ts = [0.0, 0.2, 0.7, 1.0]; rs = [r * 1.25, r, r * 0.75, r * 0.12]
    else:
        ts = [0.0, 0.15, 0.85, 1.0]; rs = [r * 1.3, r, r * 0.92, r * 1.2]
    return tube([lerp(p0, p1, t) for t in ts], rs, n=n, up=(0, 0, 1))


# ---------------------------------------------------------------------------------------------- skull
def skull_parts(jaw_open=12.0, teeth=True):
    """returns dict of Parts in skull-local space (origin = middle of the head, face towards -Y)"""
    cr = ellipsoid(0.074, 0.090, 0.082, n=16, m=11).move(0, 0.014, 0.012)
    v = cr.v
    z = v[:, 2] - 0.012
    t = np.clip(-z / 0.082, 0, 1)
    v[:, 0] *= (1 - 0.28 * t ** 1.3)
    v[:, 1] = np.where(v[:, 1] > 0.014, 0.014 + (v[:, 1] - 0.014) * (1 - 0.30 * t ** 1.2), v[:, 1])
    # temples
    for sx in (-1, 1):
        d = np.linalg.norm(v - np.array([sx * 0.07, -0.02, 0.0]), axis=1)
        v[:, 0] -= sx * np.exp(-(d / 0.032) ** 2) * 0.008
    # brow ridge
    brow = np.exp(-((v[:, 2] - 0.026) / 0.013) ** 2) * M.smoothstep(-0.03, -0.07, v[:, 1])
    v[:, 1] -= brow * 0.007
    cr.v = v
    face = loft_z([(-0.098, 0, -0.040, 0.031, 0.047), (-0.07, 0, -0.042, 0.038, 0.047), (-0.035, 0, -0.040, 0.056, 0.046, 2.6),
                   (-0.01, 0, -0.038, 0.058, 0.048, 2.4), (0.025, 0, -0.03, 0.055, 0.05)], n=14)
    sk = M.boolean(cr, face, 'UNION')
    cuts = []
    for sx in (-1, 1):
        cuts.append(ellipsoid(0.022, 0.034, 0.021, n=10, m=6).rot('y', sx * 14).move(sx * 0.031, -0.088, -0.004))
    nose = M.prism([(0, 0.019), (-0.013, -0.011), (0, -0.004), (0.013, -0.011)], -0.03, 0.03).rot('x', 90).move(0, -0.085, -0.042)
    cuts.append(nose)
    sk = M.boolean(sk, join(cuts), 'DIFFERENCE').tri()
    sk.smooth = 48.0
    out = dict(skull=sk)

    def arch(a, z):
        a = math.radians(a)
        return np.array([0.030 * math.sin(a), -0.036 - 0.049 * math.cos(a), z])

    tp = []
    if teeth:
        for a in np.linspace(-62, 62, 11):
            tb = box(0.0085, 0.006, 0.013).rot('z', -a).move(arch(a, -0.103))
            tp.append(tb)
        out['teeth'] = join(tp)
    # mandible (closed position), then rotated open about the hinge
    hinge = np.array([0.0, 0.004, -0.048])
    path = [(0.056, 0.006, -0.046), (0.054, 0.004, -0.085), (0.049, -0.006, -0.116)]
    path += [tuple(arch(a, -0.128)) for a in np.linspace(68, -68, 9)]
    path += [(-0.049, -0.006, -0.116), (-0.054, 0.004, -0.085), (-0.056, 0.006, -0.046)]
    path = np.array(path)
    r = np.full(len(path), 0.012); r[0] = r[-1] = 0.008; r[1] = r[-2] = 0.012; r[2] = r[-3] = 0.014
    jaw = tube(path, r, n=6, r2=0.0058, up=(0, 0, 1))
    chin = ellipsoid(0.016, 0.01, 0.014, n=8, m=5).move(0, -0.086, -0.132)
    jp = [jaw, chin]
    if teeth:
        for a in np.linspace(-58, 58, 10):
            jp.append(box(0.008, 0.0055, 0.012).rot('z', -a).move(arch(a, -0.113) * np.array([0.93, 1, 1]) + np.array([0, 0.004, 0])))
    jw = join(jp)
    jw.rot('x', jaw_open, pivot=hinge)
    jw.smooth = 45.0
    out['jaw'] = jw
    out['eyes'] = join([ellipsoid(0.0125, 0.01, 0.0125, n=8, m=5).move(sx * 0.031, -0.064, -0.004) for sx in (-1, 1)])
    return out


def add_skull(C, look='bone', eye_look='eye', jaw_open=12.0, origin=None, tilt=0.0, scale=1.0):
    o = SKULL_ORIGIN if origin is None else np.asarray(origin, dtype=float)
    P = skull_parts(jaw_open)
    for k, p in P.items():
        if scale != 1.0:
            p.scale(scale)
        if tilt:
            p.rot('x', tilt)
        p.move(o)
        C.add(p, bone='Head', look=eye_look if k == 'eyes' else look)
    return P


# ---------------------------------------------------------------------------------------------- spine / ribs
def vertebra(r=0.022, h=0.022, sp=0.03, wings=0.0, n=7):
    body = revolve([(r * 0.86, -h / 2), (r, -h * 0.3), (r * 0.8, 0), (r, h * 0.3), (r * 0.86, h / 2)], n)
    parts = [body, horn([(0, r * 0.6, 0), (0, r + sp * 0.6, -h * 0.25), (0, r + sp, -h * 0.7)], r * 0.45, n=4, r_tip=0.003)]
    if wings > 0:
        for sx in (-1, 1):
            parts.append(horn([(sx * r * 0.6, r * 0.4, 0), (sx * (r + wings), r * 0.7, 0)], r * 0.36, n=4, r_tip=0.003))
    return join(parts)


SPINE_CTRL = [(0, 0.062, 1.005), (0, 0.036, 1.10), (0, 0.048, 1.21), (0, 0.072, 1.33), (0, 0.060, 1.45), (0, 0.028, 1.525), (0, 0.006, 1.60)]


def spine_bone(z):
    if z < 1.055:
        return 'pelvis'
    if z < 1.13:
        return 'spine_01'
    if z < 1.205:
        return 'spine_02'
    if z < 1.475:
        return 'spine_03'
    if z < 1.575:
        return 'neck_01'
    return 'Head'


def add_spine(C, look='bone', thick=1.0, n=17, zmin=0.0, zmax=9.0):
    pts = M.polyline(SPINE_CTRL, n)
    for i, p in enumerate(pts):
        z = p[2]
        if z < zmin or z > zmax:
            continue
        if z < 1.2:
            vb = vertebra(0.026 * thick, 0.027, 0.03, wings=0.028)
        elif z < 1.47:
            vb = vertebra(0.021 * thick, 0.022, 0.034, wings=0.012)
        else:
            vb = vertebra(0.0165 * thick, 0.017, 0.02, wings=0.012)
        tg = pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]
        vb.tf(M.rot_to((0, 0, 1), tg)).move(p)
        C.add(vb, bone=spine_bone(z), look=look, smooth=45)


RIB_A = [0.066, 0.100, 0.124, 0.139, 0.147, 0.150, 0.146, 0.136]
RIB_B = [0.070, 0.090, 0.104, 0.112, 0.116, 0.116, 0.110, 0.100]
RIB_END = [-78, -80, -80, -80, -78, -72, -46, -20]


def rib_path(k, sx=1, npts=10, wide=1.0, deep=1.0):
    zb = 1.447 - 0.031 * k
    ph = np.radians(np.linspace(80, RIB_END[k], npts))
    s = np.linspace(0, 1, npts)
    drop = 0.034 + 0.006 * k
    return np.stack([sx * RIB_A[k] * wide * np.cos(ph), RIB_B[k] * deep * np.sin(ph), zb - drop * s ** 1.2], axis=1)


def add_ribcage(C, look='bone', thick=1.0, wide=1.0, deep=1.0, bone='spine_03', skip=()):
    for k in range(8):
        for sx in (1, -1):
            if (k, sx) in skip:
                continue
            pts = rib_path(k, sx, 10, wide, deep)
            r = np.full(10, 0.0088 * thick); r[0] = 0.007 * thick; r[-1] = 0.006 * thick
            rib = tube(pts, r, n=5, r2=0.0052 * thick, up=(0, 0, 1))
            C.add(rib, bone=bone, look=look, smooth=50)
    st = tube([(0, -0.064 * deep, 1.43), (0, -0.088 * deep, 1.365), (0, -0.111 * deep, 1.285), (0, -0.117 * deep, 1.215)],
              [0.021, 0.019, 0.016, 0.01], n=6, r2=0.0065, up=(1, 0, 0))
    C.add(st, bone=bone, look=look, smooth=50)


def add_shoulder_girdle(C, look='bone', thick=1.0):
    cl = tube(M.polyline([(0.018, -0.068, 1.428), (0.085, -0.075, 1.447), (0.14, -0.02, 1.458), (0.178, 0.03, 1.46)], 7),
              [0.011, 0.008, 0.0075, 0.0075, 0.008, 0.009, 0.011], n=5)
    C.add(cl, bone='clavicle_l', look=look, mirror=True)
    sc = M.prism([(0.075, 1.455), (0.175, 1.47), (0.16, 1.43), (0.105, 1.30), (0.082, 1.36)], 0.0, 0.009)
    # outline given as (x, z); extrude along y -> rotate so local y becomes world z
    sc.v = np.stack([sc.v[:, 0], 0.112 + sc.v[:, 2] - 0.45 * (sc.v[:, 0] - 0.09), sc.v[:, 1]], axis=1)
    sc.recalc()
    sc.smooth = 30
    C.add(sc, bone='clavicle_l', look=look, mirror=True)


# ---------------------------------------------------------------------------------------------- limbs
def add_arms(C, look='bone', thick=1.0, hands=True, claws=1.0, sides=('l', 'r')):
    R = M.RIG
    S = R.H('upperarm_l'); E = R.H('lowerarm_l'); W = R.H('hand_l')
    parts = []
    parts.append((seg(S + V(-0.012, 0, 0), E, 0.0155 * thick, 0.0145 * thick, 1.75, 1.8, n=6), 'upperarm_l'))
    parts.append((seg(E + V(0.012, -0.015, 0), W + V(-0.004, -0.017, 0), 0.0095 * thick, 0.0115 * thick, 1.5, 1.55, n=5), 'lowerarm_l'))
    parts.append((seg(E + V(-0.022, 0.014, -0.002), W + V(-0.004, 0.017, 0), 0.0105 * thick, 0.008 * thick, 1.7, 1.5, n=5), 'lowerarm_l'))
    if hands:
        carp = rock(0.021, 0.03, 0.011, seed=3, n=14); carp.smooth = 40
        parts.append((carp.move(W + V(0.02, -0.002, 0)), 'hand_l'))
        for fg, r in (('index', 0.0058), ('middle', 0.006), ('ring', 0.0056), ('pinky', 0.005)):
            k = R.H('%s_01_l' % fg)
            base = W + V(0.036, (k[1] - W[1]) * 0.5, 0)
            parts.append((seg(base, k, r * thick, r * 1.05 * thick, 1.2, 1.55, n=4), 'hand_l'))
            for j in (1, 2, 3):
                bn = '%s_0%d_l' % (fg, j)
                a = R.H(bn); b = R.T(bn)
                p = phal(a, b, r * thick * (1.0 if j < 3 else 0.95), tip=(j == 3))
                if j == 3 and claws != 1.0:
                    p.scale(claws, pivot=a)
                parts.append((p, bn))
        a = R.H('thumb_01_l')
        parts.append((seg(W + V(0.012, -0.018, -0.006), R.T('thumb_01_l'), 0.0068 * thick, 0.0066 * thick, 1.2, 1.45, n=4), 'thumb_01_l'))
        parts.append((phal(R.H('thumb_02_l'), R.T('thumb_02_l'), 0.0064 * thick), 'thumb_02_l'))
        parts.append((phal(R.H('thumb_03_l'), R.T('thumb_03_l'), 0.006 * thick, tip=True), 'thumb_03_l'))
    for p, b in parts:
        if 'l' in sides:
            C.add(p, bone=b, look=look, smooth=50)
        if 'r' in sides:
            C.add(p.copy().mirror(), look=look, smooth=50)


def add_pelvis(C, look='bone', thick=1.0):
    sac = loft_z([(0.905, 0, 0.092, 0.010, 0.009), (0.95, 0, 0.086, 0.028, 0.017), (1.0, 0, 0.074, 0.044, 0.021), (1.028, 0, 0.066, 0.04, 0.02)], n=8)
    C.add(sac, bone='pelvis', look=look, smooth=45)
    il = ellipsoid(0.066, 0.014, 0.064, n=10, m=6)
    # cup the blade a little (concave towards the inside/front)
    il.v[:, 1] += 0.9 * (il.v[:, 0] ** 2) / 0.066 * 0.35
    il.rot('z', -38).rot('y', 20).move(0.088, 0.036, 1.0)
    C.add(il, bone='pelvis', look=look, smooth=50, mirror=True)
    isch = M.torus(0.034, 0.0105 * thick, n=9, m=5, ry=0.026).rot('x', 62).rot('z', 18).move(0.052, -0.006, 0.9)
    C.add(isch, bone='pelvis', look=look, smooth=50, mirror=True)
    ace = ellipsoid(0.03, 0.03, 0.03, n=8, m=5).move(0.086, 0.004, 0.935)
    C.add(ace, bone='pelvis', look=look, mirror=True)
    pub = ellipsoid(0.016, 0.012, 0.022, n=8, m=5).move(0, -0.046, 0.905)
    C.add(pub, bone='pelvis', look=look)


def add_legs(C, look='bone', thick=1.0, feet=True, sides=('l', 'r')):
    R = M.RIG
    Hh = R.H('thigh_l'); K = R.H('calf_l'); A = R.H('foot_l'); B = R.H('ball_l')
    parts = []
    fem = tube(M.polyline([(0.122, 0.006, 0.928), (0.108, 0.004, 0.86), (0.094, 0.001, 0.72), (0.089, 0.0, 0.60), (0.089, 0.001, 0.548)], 8),
               np.array([0.02, 0.019, 0.0165, 0.0155, 0.0155, 0.0175, 0.024, 0.027]) * thick, n=6)
    neck = seg((0.118, 0.004, 0.925), (0.076, 0.0, 0.946), 0.0125 * thick, 0.0125 * thick, 1.0, 1.9, n=5)
    cond = join([ellipsoid(0.02, 0.03, 0.022, n=8, m=5).move(0.089 + sx * 0.019, 0.006, 0.54) for sx in (-1, 1)])
    parts += [(fem, 'thigh_l'), (neck, 'thigh_l'), (cond, 'thigh_l')]
    parts.append((ellipsoid(0.019, 0.009, 0.021, n=8, m=5).move(0.089, -0.036, 0.538), 'calf_l'))
    tib = tube(M.polyline([(0.089, 0.004, 0.518), (0.089, 0.006, 0.49), (0.089, 0.016, 0.36), (0.089, 0.03, 0.17), (0.089, 0.036, 0.108)], 7),
               np.array([0.03, 0.022, 0.0165, 0.014, 0.0125, 0.0145, 0.021]) * thick, n=5, phase=math.pi / 2)
    fib = seg((0.119, 0.02, 0.50), (0.116, 0.044, 0.10), 0.0062 * thick, 0.006 * thick, 1.7, 1.9, n=4)
    parts += [(tib, 'calf_l'), (fib, 'calf_l')]
    if feet:
        tal = ellipsoid(0.024, 0.03, 0.022, n=8, m=5).move(0.089, 0.03, 0.078)
        heel = ellipsoid(0.021, 0.042, 0.027, n=8, m=5).move(0.089, 0.078, 0.027)
        tars = rock(0.03, 0.03, 0.02, seed=5, n=14).move(0.089, -0.02, 0.05); tars.smooth = 40
        parts += [(tal, 'foot_l'), (heel, 'foot_l'), (tars, 'foot_l')]
        xs = [-0.027, -0.013, 0.001, 0.014, 0.026]      # big toe on the inner (smaller x) side of the left foot
        rr = [0.0085, 0.0068, 0.0065, 0.006, 0.0058]
        ln = [0.07, 0.066, 0.058, 0.05, 0.042]
        for i in range(5):
            x0 = 0.089 + xs[i] * 0.75; x1 = 0.089 + xs[i] * 1.15
            parts.append((seg((x0, -0.038, 0.047), (x1, -0.116, 0.0135 + rr[i] * 0.4), rr[i] * thick, rr[i] * thick, 1.2, 1.5, n=4), 'foot_l'))
            x2 = 0.089 + xs[i] * 1.3
            toe = tube([(x1, -0.118, 0.0125), (lerp(x1, x2, 0.5), -0.118 - ln[i] * 0.5, 0.0105), (x2, -0.118 - ln[i], 0.006)],
                       [rr[i] * 1.25 * thick, rr[i] * 0.95 * thick, rr[i] * 0.45 * thick], n=4, up=(0, 0, 1))
            parts.append((toe, 'ball_l'))
    for p, b in parts:
        if 'l' in sides:
            C.add(p, bone=b, look=look, smooth=50)
        if 'r' in sides:
            C.add(p.copy().mirror(), look=look, smooth=50)


def add_skeleton(C, look='bone', eye_look='eye', thick=1.0, jaw_open=12.0, legs=True, skull=True):
    if skull:
        add_skull(C, look, eye_look, jaw_open)
    add_spine(C, look, thick)
    add_ribcage(C, look, thick)
    add_shoulder_girdle(C, look, thick)
    add_arms(C, look, thick)
    add_pelvis(C, look, thick)
    if legs:
        add_legs(C, look, thick)
