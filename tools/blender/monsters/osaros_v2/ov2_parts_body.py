"""Osaros v2: the body (atlas 'body': objects body_main + wpn_1) and the cloth (atlas 'cloth': object body_cloth).

An emaciated fired-clay torso inside an iron rib-cage grate, the ember heart hanging in the hollow chest behind it; an
iron yoke round the neck; a stack of charred beams chained to the right shoulder and a broken, tongueless forge bell on
the left; skeletal arms in burnt gauntlets with claws; a stoker's scorched apron over a long trailing soot-black robe;
an ash-heavy mantle; dead lanterns on chains; the furnace rake in the right hand."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_c as K
import kit_b as KB2
import kit_boss as KX
import ov2_sculpt as SC
from monlib import V, tube, ellipsoid, box, join, horn, revolve, loft, lerp, unit, hang, arc_pts, band, sheet, rings_z, torus

HEART = np.array([0.008, -0.046, 1.338])
YOKE_Z = 1.500
RIBS = [1.414, 1.380, 1.346, 1.312, 1.278, 1.244]
TINE_TIPS = []          # filled by the weapon (rest-space points that glow)


# ====================================================================================================== looks
def body_looks(C):
    chest = [(tuple(HEART + (0, -0.04, 0)), 0.06, 0.15)]
    C.look('skinb', 'ash', 'skin_body', color=(0.25, 0.24, 0.225), slag=(0.06, 0.056, 0.054), glow=chest, scorch=[(tuple(HEART + (0, -0.05, 0)), 0.10, 0.22)],
           eslot='emis_ember', strength=3.0, glow_amt=0.8, soot_ao=0.8, dust=0.5, crust_a=0.42, crust_b=0.85, bump=0.5)
    C.look('bowl', 'core', 'skin_body', ao_hi=2.0, ao_lo=1.5, glow=[(tuple(HEART), 0.03, 0.115)], floor=0.0, eslot='emis_ember', strength=3.2, heat=0.36,
           gamma=1.3, scale=46.0)
    C.look('heart', 'core', 'emis_ember', ao_hi=2.0, ao_lo=1.5, scale=58.0, strength=3.4, heat=0.8, gamma=1.0)
    C.look('iron', 'iron', 'metal_iron', amount=0.48, rough=0.56, dust=0.4, hammer=30.0)
    C.look('iron2', 'iron', 'metal_iron', color=(0.05, 0.048, 0.047), amount=0.62, rough=0.62, dust=0.5, hammer=44.0, scale=11.0)
    C.look('bell', 'iron', 'metal_iron', color=(0.055, 0.05, 0.044), rust=(0.12, 0.075, 0.04), amount=0.5, rough=0.5, dust=0.55, hammer=16.0, edge=0.9,
           edge_color=(0.34, 0.30, 0.24))
    C.look('wood', 'char', 'rock_char', axis=(0, 1, 0), ash_amt=0.38, glow=[((-0.31, -0.37, 1.522), 0.02, 0.07), ((-0.405, 0.30, 1.50), 0.02, 0.06)], eslot="emis_ember",
           strength=3.0, heat=0.6)
    C.look('glass', 'glass', 'rock_char', color=(0.012, 0.011, 0.010), band=(0.03, 0.028, 0.025), rough=0.5, bump=0.4)


def cloth_looks(C):
    C.look('robe', 'soot', 'cloth_robe', color=(0.015, 0.0145, 0.0155), ember=1.0, ember_hi=0.42, eslot='emis_hem', strength=3.0, hem_lo=0.0, hem_hi=0.7,
           hem_amt=0.3, fold=7.0, dust=0.3, dirt=(0.05, 0.048, 0.046))
    C.look('mantle', 'soot', 'cloth_robe', color=(0.020, 0.019, 0.020), weave='wool_weave', ember=0.8, ember_lo=0.3, ember_hi=1.0, eslot='emis_hem', strength=3.0,
           hem_lo=0.3, hem_hi=1.1, hem_amt=0.35, fold=9.0, dust=0.18, dark=0.42, dirt=(0.05, 0.048, 0.046), ash=(0.13, 0.125, 0.12))
    C.look('collar', 'fur', 'cloth_robe', color=(0.035, 0.032, 0.03), tip=(0.20, 0.195, 0.19), scale=42.0, rough=0.95)
    C.look('apron', 'leather', 'leather_apron', color=(0.020, 0.015, 0.012), scuff_color=(0.075, 0.062, 0.052), scuff=0.4, rough=0.62, dirt=(0.07, 0.066, 0.062),
           hem_lo=0.1, hem_hi=0.7)


# ====================================================================================================== helpers
def solid_cloth(part, th=0.004):
    """a sheet as a thin closed shell (the game culls back faces: both sides must exist). Keeps the hem distances of the
    sheet's open borders for the looks (Part.hem, written into the 'hem_d' attribute by the creature)."""
    P = part.copy(); P.W = None
    cnt = {}
    for f in P.f:
        for k in range(len(f)):
            e = (min(f[k], f[(k + 1) % len(f)]), max(f[k], f[(k + 1) % len(f)]))
            cnt[e] = cnt.get(e, 0) + 1
    be = np.array([e for e, c in cnt.items() if c == 1], dtype=np.int64)
    A = P.v[be[:, 0]]; B = P.v[be[:, 1]]
    s = P.solidify(th).recalc()
    AB = B - A
    L2 = np.maximum((AB * AB).sum(1), 1e-12)
    best = np.full(len(s.v), 1e9)
    for i in range(0, len(s.v), 256):
        Pp = s.v[i:i + 256]
        t = np.clip(((Pp[:, None, :] - A[None]) * AB[None]).sum(2) / L2[None], 0, 1)
        d = np.linalg.norm(Pp[:, None, :] - (A[None] + AB[None] * t[:, :, None]), axis=2)
        best[i:i + 256] = d.min(axis=1)
    s.hem = np.clip(1.0 - best / L.HEM_RANGE, 0.0, 1.0).astype(np.float32)
    s.smooth = 180.0
    return s


def tatter_cape(w_top, w_bot, length, z_top, y, cols=27, rows=17, pleats=6.0, amp=0.05, flare=0.26, bow=0.10, seed=5, strips=6, slope=0.05):
    """back cape torn into long tongues: every strip has its own length, long in its middle and ripped short at its sides
    (deep V tears between the strips), pleats deepening towards the hem. Unweighted sheet."""
    rng = np.random.default_rng(seed)
    u = np.linspace(-1, 1, cols)
    b = np.sort(rng.uniform(-0.8, 0.8, strips - 1))
    b = np.r_[-1.0, b, 1.0]
    Lc = np.zeros(cols)
    for j, uu in enumerate(u):
        k = int(np.clip(np.searchsorted(b, uu, side='right') - 1, 0, strips - 1))
        s = (uu - b[k]) / max(b[k + 1] - b[k], 1e-6)
        Lmax = length * (0.60 + 0.40 * ((k * 7 + seed * 3) % 5) / 4.0)
        Lc[j] = Lmax * (1 - 0.42 * abs(2 * s - 1) ** 2.6)
    Lc *= rng.uniform(0.80, 1.0, cols)
    G = np.zeros((rows + 1, cols, 3))
    ph = rng.uniform(0, 6.28)
    for i in range(rows + 1):
        t = i / rows
        w = lerp(w_top, w_bot, t ** 0.8)
        G[i, :, 0] = u * w / 2
        G[i, :, 1] = y - bow * (u ** 2) * (1 - 0.6 * t) + flare * t ** 1.3 + amp * t ** 0.7 * np.sin(pleats * math.pi * u + ph)
        G[i, :, 2] = z_top - Lc * t - slope * (u ** 2) * (1 - t)
    s = sheet(G)
    s.smooth = 180.0
    return s


def beam(Ln, w, h, seed=0, broken=(True, False)):
    """charred timber along +Y, centred on the origin: bowed, checked, one or both ends burnt to a stump"""
    rng = np.random.default_rng(seed)
    ys = np.linspace(-Ln / 2, Ln / 2, 8)
    rg = M.ring2d(8, 5.0, math.pi / 8)
    rings = []
    for i, y in enumerate(ys):
        t = i / 7.0
        k = 1.0
        if broken[0] and i == 0:
            k = 0.45
        if broken[0] and i == 1:
            k = 0.82
        if broken[1] and i == 7:
            k = 0.5
        if broken[1] and i == 6:
            k = 0.85
        bow = 0.012 * math.sin(math.pi * t) * rng.uniform(-1, 1)
        jx = rng.uniform(-0.004, 0.004); jz = rng.uniform(-0.004, 0.004)
        rings.append(np.stack([rg[:, 0] * w / 2 * k * rng.uniform(0.94, 1.05) + jx + bow, np.full(8, y + (rng.uniform(-0.02, 0.02) if i in (0, 7) else 0.0)),
                               rg[:, 1] * h / 2 * k * rng.uniform(0.94, 1.05) + jz], axis=1))
    R = np.array(rings)
    if broken[0]:
        R[0, :, 1] += rng.uniform(-0.03, 0.035, 8)
    if broken[1]:
        R[-1, :, 1] += rng.uniform(-0.035, 0.03, 8)
    b = loft(R, cap0=True, cap1=True).recalc()
    b.smooth = 38.0
    return b


def bars(surf_bvh, c_xy, z, a0, a1, n, out, droop=0.02, r=0.0085, r2=0.0052, skip=None):
    """a flat iron bar hugging the torso at height z from angle a0 to a1 (deg, -90 = front), `out` off the skin"""
    pts = []
    for a in np.linspace(a0, a1, n):
        if skip is not None and skip[0] < a < skip[1]:
            continue
        ar = math.radians(a)
        d = np.array([math.cos(ar), math.sin(ar), 0.0])
        zz = z + droop * (1 - math.cos(ar + math.pi / 2))
        o = np.array([c_xy[0], c_xy[1], zz]) + d * 0.6
        hit, nrm = SC.ray(surf_bvh, o, -d)
        if hit is None or abs(hit[0]) > 0.185:
            continue
        pts.append(hit + d * out)
    return np.array(pts)


def bar_tube(pts, r=0.0085, r2=0.0052):
    t = tube(pts, r, n=4, r2=r2, up=(0, 0, 1), phase=math.pi / 4)
    t.smooth = 30.0
    return t


# ====================================================================================================== body
def thin_body():
    b = M.load_body('male')
    b = M.decimate(b, 0.55)
    pose = {'pelvis': dict(scale=(0.86, 0.80, 1.0)), 'spine_01': dict(scale=(0.72, 0.62, 1.0)), 'spine_02': dict(scale=(0.74, 0.64, 1.0)),
            'spine_03': dict(scale=(0.95, 0.84, 1.0)), 'neck_01': dict(scale=(0.84, 0.84, 1.0))}
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(scale=(1.0, 0.9, 0.92))
        pose['upperarm_' + s] = dict(scale=(1.0, 0.58, 0.58))
        pose['lowerarm_' + s] = dict(scale=(1.0, 0.60, 0.60))
        pose['hand_' + s] = dict(scale=(1.0, 0.92, 0.85))
        pose['thigh_' + s] = dict(scale=(0.62, 0.62, 1.0))
        pose['calf_' + s] = dict(scale=(0.60, 0.60, 1.0))
        pose['foot_' + s] = dict(scale=(0.85, 1.0, 0.9))
    M.pose_sculpt(b, pose)
    dz = float(b.v[:, 2].min())
    b.v[:, 2] -= dz
    M.log("thin body: %d tris, ground shift %.4f" % (b.ntri, dz))
    return b, pose, dz


def add_torso(C, b):
    """the skinned flesh (chest opened), the hollow, the heart, the grate, the yoke. Returns BVH of the closed torso."""
    bvh = SC.bvh_of(b)
    fc = b.face_centers()
    fn = b.normals()
    fny = np.array([fn[list(f)].mean(0)[1] for f in b.f])
    neck = np.array([all(b.v[i][2] > 1.492 and abs(b.v[i][0]) < 0.09 for i in f) for f in b.f])
    op = (np.abs(fc[:, 0]) < 0.097) & (fc[:, 2] > 1.240) & (fc[:, 2] < 1.428) & (fc[:, 1] < -0.02) & (fny < -0.25)
    body = b.sub(~(neck | op))
    body.smooth = 180.0
    C.add(body, look='skinb', obj='body_main')
    # the hollow chest: a bowl open to the front, its mouth reaching out to the skin
    prof = [(0.0, 0.050), (0.045, 0.044), (0.082, 0.022), (0.102, -0.012), (0.106, -0.045), (0.108, -0.082)]
    bowl = revolve([(r, y) for r, y in prof], 18, cap=False)
    bowl.rot('x', -90)                         # the lathe axis (local +Z) -> rest +Y: the profile's second coordinate is y

    bowl.scale((1.0, 1.0, 0.97)).move(0.0, 0.0, 1.334)
    bowl = bowl.solidify(0.006).recalc()

    bowl.smooth = 60.0
    C.add(bowl, bone='spine_03', look='bowl', obj='body_main')
    # the ember heart: a clinker in the shape of what it was, hung on four iron pins
    hp = ellipsoid(0.034, 0.030, 0.043, n=12, m=8)
    k = np.clip((0.02 - hp.v[:, 2]) / 0.06, 0, 1)
    hp.v[:, 0] *= 1 - 0.55 * k ** 1.3; hp.v[:, 1] *= 1 - 0.5 * k ** 1.3
    hp.v[:, 0] += 0.006 * np.sin(hp.v[:, 2] * 60)
    hp.rough(0.0035, 45.0, seed=4.0)
    hp.rot('y', 14).rot('x', -8).move(HEART)
    hp.smooth = 60.0
    C.add(hp, bone='spine_03', look='heart', obj='body_main')
    for dv, r in (((-0.022, 0.006, 0.038), 0.0075), ((0.004, 0.010, 0.044), 0.0085), ((0.024, 0.0, 0.034), 0.0065)):
        st = tube([HEART + np.array(dv) * 0.6, HEART + np.array(dv) * 1.35 + (0, 0, 0.012)], [r, r * 0.75], n=6)
        C.add(st, bone='spine_03', look='heart', obj='body_main', smooth=60)
    for dv in ((-0.9, 0.35, 0.5), (0.95, 0.3, 0.4), (-0.6, 0.5, -0.75), (0.7, 0.45, -0.7), (0.1, 0.9, 0.9)):
        d = unit(dv)
        C.add(tube([HEART + d * 0.022, HEART + d * 0.125], 0.003, n=4), bone='spine_03', look='iron', obj='body_main', smooth=30)
    # the grate: ribs of flat iron round the chest, the third one snapped in front of the heart; a sternum bar above and below
    cxy = (0.0, 0.01)
    for k, z in enumerate(RIBS):
        if k == 2:
            for (a0, a1, bend) in ((-140, -112, 1), (-68, 32, -1)):
                pts = bars(bvh, cxy, z, a0, a1, 9, 0.012)
                if bend > 0:
                    pts[-1] += np.array([0.004, -0.016, 0.01]); pts[-2] += np.array([0.0, -0.006, 0.003])
                else:
                    pts[0] += np.array([-0.004, -0.020, -0.008]); pts[1] += np.array([0.0, -0.007, -0.002])
                C.add(bar_tube(pts), bone='spine_03', look='iron', obj='body_main')
            continue
        a0, a1, npt = ((-152, -28, 14) if k == 0 else ((-170, -10, 18) if k == 1 else (-212, 32, 26)))
        pts = bars(bvh, cxy, z, a0, a1, npt, 0.012)
        C.add(bar_tube(pts), bone='spine_03', look='iron', obj='body_main')
    for (z0, z1) in ((RIBS[1] - 0.004, YOKE_Z - 0.03), (RIBS[5] - 0.03, RIBS[3] + 0.004)):
        pts = []
        for z in np.linspace(z0, z1, 6):
            hit, nrm = SC.ray(bvh, (0.0, -0.6, z), (0, 1, 0))
            pts.append(hit + np.array([0, -0.021, 0]))
        st = tube(np.array(pts), 0.006, n=4, r2=0.016, up=(0, -1, 0), phase=math.pi / 4)
        C.add(st, bone='spine_03', look='iron2', obj='body_main', smooth=30)
        for z in (z0 + 0.012, z1 - 0.012):
            hit, nrm = SC.ray(bvh, (0.0, -0.6, z), (0, 1, 0))
            C.add(K.rivet(hit + np.array([0, -0.027, 0]), (0, -1, 0), 0.0075), bone='spine_03', look='iron', obj='body_main')
    pts = bars(bvh, cxy, RIBS[5] - 0.036, -150, -30, 12, 0.014, droop=0.0)
    C.add(bar_tube(pts, 0.011, 0.006), bone='spine_03', look='iron2', obj='body_main')
    # hinge straps down both flanks, riveted to every rib
    for a in (-160, -20):
        pts = []
        for z in np.linspace(RIBS[5] - 0.03, RIBS[0] + 0.03, 6):
            ar = math.radians(a)
            d = np.array([math.cos(ar), math.sin(ar), 0.0])
            zz = z + 0.02 * (1 - math.cos(ar + math.pi / 2))
            hit, nrm = SC.ray(bvh, np.array([cxy[0], cxy[1], zz]) + d * 0.6, -d)
            pts.append(hit + d * 0.021)
        st = tube(np.array(pts), 0.005, n=4, r2=0.014, up=(math.cos(math.radians(a)), math.sin(math.radians(a)), 0), phase=math.pi / 4)
        C.add(st, bone='spine_03', look='iron2', obj='body_main', smooth=30)
    # the yoke: a heavy ring round the base of the neck (it hides where the head joins the body)
    yk = torus(0.082, 0.0175, n=20, m=7, ry=0.078)
    yk.rot("x", 14).move(0.0, 0.004, YOKE_Z)
    yk.rough(0.0012, 40.0, seed=2.0)
    yk.smooth = 60.0
    C.add(yk, bone='spine_03', look='iron2', obj='body_main')
    for a in (-90, 35, 145):
        ar = math.radians(a)
        p = np.array([0.082 * math.cos(ar), 0.004 + 0.078 * math.sin(ar) * math.cos(math.radians(14)), YOKE_Z + 0.078 * math.sin(ar) * math.sin(math.radians(14))])
        lug = box(0.034, 0.03, 0.04, bevel=0.006).rot('z', a + 90).move(p + np.array([math.cos(ar), math.sin(ar), 0]) * 0.014)
        C.add(lug, bone='spine_03', look='iron', obj='body_main', smooth=30)
    return bvh


def add_spine_knobs(C, bvh):
    """vertebrae standing out of the starved back and belly (they show where the mantle is torn)"""
    for z in np.linspace(1.10, 1.22, 5):
        hit, nrm = SC.ray(bvh, (0.0, 0.6, z), (0, -1, 0))
        if hit is None:
            continue
        kb = ellipsoid(0.016, 0.014, 0.012, n=8, m=5).move(hit + np.array([0, -0.004, 0]))
        C.add(kb, bone='spine_01' if z < 1.17 else 'spine_02', look='skinb', obj='body_main', smooth=90)


def add_arms(C, b):
    """burnt gauntlets: flared vambraces, spiked couters, hand plates, every finger a clawed iron sheath"""
    R = M.RIG

    def add(p, bone, look='iron', smooth=None, mirror=True):
        return C.add(p, bone=bone, look=look, obj='body_main', smooth=smooth, mirror=mirror)
    add(K.limb_plate('lowerarm_l', [(0.05, 0.079, 0.074), (0.15, 0.066, 0.062), (0.5, 0.054, 0.050), (0.97, 0.045, 0.042)], n=10, off=(0, 0.004, 0.0),
                     up=(0, 0, 1), lip=0.006), None, 'iron', 38)
    for (t, r) in ((0.32, 0.059), (0.62, 0.053)):
        ring = K.limb_plate('lowerarm_l', [(t - 0.035, r, r - 0.004), (t + 0.035, r - 0.002, r - 0.006)], n=10, off=(0, 0.004, 0.0), up=(0, 0, 1))
        add(ring, None, 'iron2', 38)
    E = R.H('lowerarm_l')
    add(ellipsoid(0.058, 0.062, 0.06, n=10, m=6).move(E + np.array([0.004, 0.012, 0.0])), 'lowerarm_l', 'iron2', 50)
    add(M.cone_spike(0.03, 0.15, n=6).rot('x', -90).rot('z', -12).move(E + np.array([0.0, 0.06, 0.004])), 'lowerarm_l', 'iron2', 30)
    add(K.limb_plate('upperarm_l', [(0.72, 0.046, 0.046), (0.9, 0.056, 0.054), (1.0, 0.058, 0.056)], n=10, off=(0, 0.004, 0), up=(0, 0, 1)), None, 'iron', 38)
    W = R.H('hand_l')
    hp = tube([W + np.array([-0.012, 0.004, 0.006]), W + np.array([0.055, 0.004, 0.008]), W + np.array([0.125, 0.006, 0.006])], [0.026, 0.024, 0.019], n=8,
              r2=[0.046, 0.052, 0.050], up=(0, 0, 1), power=3.0)
    add(hp, 'hand_l', 'iron', 38)
    for k, fg in enumerate(('index', 'middle', 'ring', 'pinky', 'thumb')):
        rr = [0.0122, 0.0126, 0.0118, 0.0106, 0.0132][k]
        for j in (1, 2, 3):
            bn = '%s_0%d_l' % (fg, j)
            a = R.H(bn); t = R.T(bn)
            d = unit(t - a)
            Ln = float(np.linalg.norm(t - a))
            if j < 3:
                seg = tube([a - d * 0.004, a + d * Ln * 0.5, t + d * 0.002], [rr * 1.08, rr, rr * 0.96], n=5, up=(0, 0, 1))
                add(seg, bn, 'iron' if j == 1 else 'iron2', 40)
            else:
                cl = 0.058 if fg != 'thumb' else 0.046
                down = np.array([0, 0, -1.0]) if fg != 'thumb' else unit(np.cross(d, (0, 0, 1)) * -0.2 + np.array([0, -0.3, -0.9]))
                path = M.polyline([a - d * 0.004, a + d * Ln, a + d * (Ln + cl * 0.55) + down * cl * 0.14, a + d * (Ln + cl) + down * cl * 0.46], 6)
                claw = horn(path, rr * 1.02, n=5, r_tip=0.0009, power=0.85, up=(0, 0, 1))
                add(claw, bn, 'iron2', 34)


def add_legs(C):
    def add(p, bone, look='iron', smooth=38):
        return C.add(p, bone=bone, look=look, obj='body_main', smooth=smooth, mirror=True)
    sab = tube([(0.089, 0.085, 0.045), (0.089, 0.02, 0.058), (0.089, -0.06, 0.045), (0.089, -0.116, 0.036)], [0.044, 0.056, 0.044, 0.036], n=10,
               r2=[0.046, 0.052, 0.058, 0.058], up=(0, 0, 1), power=2.8)
    add(sab, 'foot_l', 'iron2')
    toe = tube([(0.089, -0.108, 0.034), (0.089, -0.18, 0.030), (0.089, -0.275, 0.018)], [0.034, 0.030, 0.008], n=10, r2=[0.058, 0.046, 0.01], up=(0, 0, 1), power=2.6)
    add(toe, 'ball_l', 'iron')
    add(K.limb_plate('calf_l', [(0.55, 0.05, 0.048), (0.8, 0.046, 0.044), (0.99, 0.05, 0.05)], n=10, off=(0.0, 0.006, 0), lip=0.004), None, 'iron')


# ====================================================================================================== pauldrons
def add_firewood(C):
    """right shoulder: charred beams stacked and chained, the fuel he carried all his life"""
    def add(p, look, smooth=None):
        return C.add(p, bone='spine_03', look=look, obj='body_main', smooth=smooth)   # rigid on the chest: on the clavicle it swung over the face in the lunges
    sad = KX.lame(0.19, 0.08, n=14)
    sad.scale((1.0, 1.15, 1.0)).rot("y", -14).move(-0.312, 0.05, 1.446)
    add(sad, 'iron2', 40)
    rows = [((-0.225, 1.512), 0.66, 21, (True, False), 4), ((-0.320, 1.500), 0.76, 22, (False, True), -3), ((-0.415, 1.480), 0.58, 23, (True, True), 5),
            ((-0.290, 1.586), 0.62, 24, (False, True), -5), ((-0.385, 1.572), 0.70, 25, (True, False), 3)]
    ends = []
    for (x, z), Ln, sd, brk, yaw in rows:
        bm = beam(Ln, 0.092, 0.080, seed=sd, broken=brk)
        ep = M.Part(np.array([[0.0, -Ln / 2, 0.0], [0.0, Ln / 2, 0.0]]), [])
        for q in (bm, ep):
            q.rot('y', (sd * 37) % 23 - 11).rot('z', yaw).rot('x', (sd % 3 - 1) * 2.0)
            q.move(x, 0.035 + ((sd * 13) % 7 - 3) * 0.012, z)
        for i, b_ in enumerate(brk):
            if b_:
                ends.append((tuple(float(t) for t in ep.v[i]), 0.035, 0.11))         # the burnt-off ends still smoulder
        add(bm, 'wood')
    C.look('wood', 'char', 'rock_char', axis=(0, 1, 0), ash_amt=0.3, glow=ends, eslot="emis_ember", strength=3.2, heat=0.9)
    loop = [(-0.150, 1.488), (-0.160, 1.585), (-0.235, 1.650), (-0.350, 1.690), (-0.428, 1.635), (-0.468, 1.555), (-0.470, 1.462), (-0.38, 1.438), (-0.22, 1.444),
            (-0.150, 1.488)]
    ch = M.chain(M.polyline([(x, -0.125, z - 0.02) for x, z in loop], 28), link=0.08, r=0.0105, n=6, m=4)
    ch.smooth = 60.0
    add(ch, 'iron')
    # a dead lantern on a chain behind the stack
    top = np.array([-0.395, 0.24, 1.462])
    add(M.chain([top, top + (0.0, 0.03, -0.13)], link=0.05, r=0.0065, n=6, m=4), 'iron', 60)
    cage, glow = KB2.lantern(h=0.17, r=0.05, bars=6, n=8)
    for q, lk in ((cage, 'iron'), (glow, 'glass')):
        q.move(top + (0.0, 0.03, -0.12))
        add(q, lk, 50)


def add_bell(C):
    """left shoulder: a forge bell, broken and without its tongue"""
    bell, _ = KB2.bell(h=0.30, R=0.212, n=24, thick=0.07, clapper=False)
    cut = []
    for (c, r, sd) in (((-0.20, 0.05, -0.25), 0.12, 2), ((0.20, -0.13, -0.30), 0.125, 3), ((0.17, 0.17, -0.32), 0.085, 4),
                       ((-0.12, 0.20, -0.32), 0.07, 6), ((0.235, 0.02, -0.21), 0.05, 7)):
        rk = M.rock(r, r, r * 0.9, seed=sd + 10, n=12, jit=0.2)
        rk.move(c)
        cut.append(rk)
    # the big break in the front wall: a tall jagged gap from the lip up past the waist of the bell, a chip beside it
    rk = M.rock(0.075, 0.11, 0.17, seed=31, n=14, jit=0.28).rot('y', 12).move(0.01, -0.21, -0.19)
    cut.append(rk)
    cut.append(M.rock(0.04, 0.06, 0.05, seed=32, n=10, jit=0.3).move(-0.10, -0.19, -0.09))
    try:
        bell = SC.boolean_multi(bell, cut).recalc()
    except Exception as e:
        M.log("WARNING: bell boolean failed:", e)
    bell.smooth = 40.0
    lp = torus(0.03, 0.009, n=10, m=5).rot('x', 90).move(0, 0, 0.022)
    P = [bell, lp]
    for p in P:
        p.rot("y", -36).rot("z", 8).move(0.262, 0.058, 1.648)
        C.add(p, bone='spine_03', look='bell', obj='body_main')
    # the strap it is bolted to
    st = tube([(0.085, 0.03, 1.505), (0.17, 0.045, 1.60), (0.245, 0.056, 1.655)], 0.006, n=4, r2=0.02, up=(0, -1, 0), phase=math.pi / 4)
    C.add(st, bone='spine_03', look='iron2', obj='body_main', smooth=30)


# ====================================================================================================== cloth
def robe_weights(robe, b):
    """a long robe must not follow the shins and feet (in the deep lunges the hem then stretches into a ball): above the
    hips it takes the body's weights, below them only the pelvis and the thighs, the thigh share growing to the knee
    and held there, each side to its own thigh with a soft seam down the middle front and back; the train stays on
    the pelvis."""
    KC.skin_cloth(robe, body=b, smooth=4)
    z = robe.v[:, 2]; x = robe.v[:, 0]; y = robe.v[:, 1]
    I = M.RIG.idx
    L_ = np.zeros_like(robe.W)
    th = 0.62 * M.smoothstep(1.00, 0.55, z)                          # thigh share: 0 at the hips .. 0.62 from the knee down
    th = th * (1 - 0.75 * M.smoothstep(0.10, 0.30, y))                # the back panel and the train hang from the pelvis
    sl = M.smoothstep(-0.07, 0.07, x)
    L_[:, I['pelvis']] = 1 - th
    L_[:, I['thigh_l']] = th * sl
    L_[:, I['thigh_r']] = th * (1 - sl)
    m = M.smoothstep(1.02, 0.86, z)[:, None]                         # hand-over from the body's weights below the hips
    robe.W = robe.W * (1 - m) + L_ * m
    M.smooth_weights(robe, 3, 0.5)
    M.normalize_weights(robe)


def add_cloth(C, b, bvh):
    def cl(p, look, texel=1.0):
        p.texel = texel
        return C.add(p, look=look, obj='body_cloth')
    # ---- the robe: waist to floor, dragging a train
    n = 56
    mod = KX.pleat_mod(seed=4, ks=(5, 9, 14), amp=0.085, hem_z=0.03, top_z=1.02, sharp=0.7, base=0.18, twist=0.25)
    secs = [(0.035, 0, 0.060, 0.37, 0.345), (0.20, 0, 0.045, 0.325, 0.292), (0.42, 0, 0.032, 0.275, 0.235), (0.64, 0, 0.024, 0.235, 0.195),
            (0.82, 0, 0.020, 0.205, 0.168), (0.95, 0, 0.018, 0.176, 0.142), (1.04, 0, 0.016, 0.150, 0.118), (1.095, 0, 0.014, 0.138, 0.108)]
    Rg = rings_z(secs, n, mod=mod)
    # more rows: resample the rings along z
    zs = np.array([0.035, 0.09, 0.15, 0.22, 0.30, 0.39, 0.48, 0.57, 0.66, 0.75, 0.84, 0.92, 0.99, 1.045, 1.095])
    zc = np.array([s[0] for s in secs])
    Rr = np.zeros((len(zs), n, 3))
    for j in range(n):
        for k in range(3):
            Rr[:, j, k] = np.interp(zs, zc, Rg[:, j, k])
    Rr[:, :, 2] = zs[:, None]
    th = np.arctan2(Rr[0, :, 1] - 0.06, Rr[0, :, 0])
    backf = M.smoothstep(0.15, 0.95, np.sin(th))                     # 1 behind him
    rag = KC.ragged(n, 0.085, 7, 0.4)
    Rr[0, :, 2] += rag * (1 - backf)
    Rr[1, :, 2] = np.maximum(Rr[1, :, 2], Rr[0, :, 2] + 0.03)
    # the train: two more rings lying on the floor behind
    od = np.stack([np.cos(th), np.sin(th), np.zeros(n)], axis=1)
    rng = np.random.default_rng(5)
    t1 = Rr[0] + od * (0.16 * backf)[:, None]; t1[:, 2] = (Rr[0, :, 2] - 0.006) * (1 - backf) + 0.022 * backf
    t2 = Rr[0] + od * ((0.42 + 0.14 * rng.uniform(-1, 1, n)) * backf ** 1.5)[:, None]; t2[:, 2] = (Rr[0, :, 2] - 0.012) * (1 - backf) + 0.012 * backf
    G = np.vstack([t2[None], t1[None], Rr])
    robe = loft(G, cap0=False, cap1=False)
    # tears: drop some quads of the two lowest rows of the front and sides
    fcn = robe.face_centers()
    keep = np.ones(len(robe.f), dtype=bool)
    rr2 = np.random.default_rng(9)
    for i, c in enumerate(fcn):
        if c[2] < 0.13 and c[1] < 0.15 and rr2.uniform() < 0.16:
            keep[i] = False
    robe = robe.sub(keep)
    robe.smooth = 180.0
    robe_weights(robe, b)
    cl(robe, 'robe')
    surf_robe = KB2.Surf(robe)
    # ---- the stoker's apron: scorched leather from the belt to the shins
    cols = 9; rows = 12
    A = np.zeros((rows + 1, cols, 3))
    for i in range(rows + 1):
        t = i / rows
        z = 1.088 - t * 0.80
        w = lerp(0.20, 0.31, t ** 0.8)
        for j in range(cols):
            u = j / (cols - 1) * 2 - 1
            x = u * w / 2
            hit, nrm = surf_robe.ray((x, -1.0, z), (0, 1, 0))
            y = (hit[1] if hit is not None else -0.2) - 0.016 - 0.012 * t + 0.008 * math.sin(u * 3.0 + t * 5.0) * t
            A[i, j] = (x, y, z - 0.03 * (u * u) * (t > 0.9))
    ap = sheet(A)
    ap = KX.rot_holes(ap, 0.55, chance=0.22, seed=3, zmin=0.3)
    ap = solid_cloth(ap, 0.005)
    KB2.skin_like(ap, robe, smooth=2)
    cl(ap, 'apron')
    # ---- belt with an iron buckle (rigid on the lowest spine bone, over the top of robe and apron)
    blt = band((0, 0.016, 1.082), 0.158, 0.128, 0.052, 0.014, n=24)
    C.add(blt, bone='spine_01', look='apron', obj='body_cloth', smooth=38)
    C.add(box(0.07, 0.016, 0.066, bevel=0.007).move(0.0, -0.118, 1.082), bone='spine_01', look='iron2', obj='body_main', smooth=30)
    C.add(box(0.03, 0.012, 0.04, bevel=0.004).move(0.0, -0.127, 1.082), bone='spine_01', look='iron', obj='body_main', smooth=30)
    # ---- the mantle: a heavy cape from the shoulders to the calves, a short ragged capelet over it, a roll of ash-grey fleece behind the neck
    cp = tatter_cape(w_top=0.50, w_bot=0.74, length=1.34, z_top=1.50, y=0.115, cols=41, rows=19, pleats=9.0, amp=0.075, flare=0.17, bow=0.13, seed=5, strips=8)
    cp = solid_cloth(cp, 0.006)
    KC.cape_weights(cp, z_top=1.50, z_hip=1.0, z_knee=0.5, thigh=0.45)
    cl(cp, 'mantle')
    cp2 = tatter_cape(w_top=0.46, w_bot=0.66, length=0.84, z_top=1.51, y=0.132, cols=33, rows=12, pleats=7.0, amp=0.06, flare=0.15, bow=0.12, seed=9, strips=7)
    cp2 = solid_cloth(cp2, 0.006)
    KC.cape_weights(cp2, z_top=1.51, z_hip=1.0, z_knee=0.5, thigh=0.35)
    cl(cp2, 'mantle')
    sh = KC.capelet(z_top=1.535, z_bot=1.375, r_top=(0.105, 0.11), r_bot=(0.27, 0.225), n=36, rows=6, rag=0.10, seed=8)
    fcs = sh.face_centers()
    ang = np.degrees(np.arctan2(fcs[:, 1] - 0.015, fcs[:, 0]))
    sh = sh.sub(~((ang > -142) & (ang < -38)))                       # open in front: the grate and the heart stay bare
    sh = solid_cloth(sh, 0.007)
    KC.skin_cloth(sh, body=b, smooth=4)
    cl(sh, 'mantle')
    path = M.polyline([(-0.165, -0.065, 1.468), (-0.150, 0.03, 1.512), (-0.085, 0.105, 1.538), (0.0, 0.128, 1.548), (0.085, 0.105, 1.538), (0.150, 0.03, 1.512),
                       (0.165, -0.065, 1.468)], 15)
    rr = np.array([0.02, 0.03, 0.036, 0.04, 0.043, 0.045, 0.046, 0.047, 0.046, 0.045, 0.043, 0.04, 0.036, 0.03, 0.02])
    roll = tube(path, rr, n=9, r2=rr * 1.1, up=(0, 0, 1))
    roll.rough(0.006, 30.0, seed=6.0)
    roll.smooth = 180.0
    C.add(roll, bone='spine_03', look='collar', obj='body_cloth')
    # ---- hip chain and a dead lantern (rigid: pelvis, then the thigh it hangs against)
    p0 = np.array([-0.150, -0.045, 1.07])
    C.add(M.chain([p0, p0 + (-0.045, -0.012, -0.11)], link=0.052, r=0.0068, n=6, m=4), bone='pelvis', look='iron', obj='body_main', smooth=60)
    p1 = p0 + np.array([-0.05, -0.014, -0.12])
    C.add(M.chain([p1, p1 + (-0.012, 0.0, -0.12)], link=0.052, r=0.0068, n=6, m=4), bone='thigh_r', look='iron', obj='body_main', smooth=60)
    cage, glow = KB2.lantern(h=0.19, r=0.055, bars=6, n=8)
    for q, lk in ((cage, 'iron'), (glow, 'glass')):
        q.move(p1 + (-0.014, 0.0, -0.215))
        C.add(q, bone='thigh_r', look=lk, obj='body_main', smooth=50)


# ====================================================================================================== weapon
def add_weapon(C):
    """the furnace rake: a twisted iron shaft, a toothed crossbar, a poker spike and its hook. Still hot at the points."""
    C.look('shaft', 'iron', 'metal_iron', amount=0.5, rough=0.58, dust=0.2, hammer=50.0)
    P = []
    P.append((tube([(0, -0.56, 0), (0, -0.2, 0), (0, 0.5, 0), (0, 1.1, 0), (0, 1.60, 0)], [0.0175, 0.0185, 0.0185, 0.0175, 0.019], n=4, twist=7.0,
                   phase=math.pi / 4), 30))
    P.append((revolve([(0.018, 0.0), (0.03, 0.012), (0.03, 0.03), (0.018, 0.045)], 8).rot('x', -90).move(0, -0.11, 0), 40))       # hand stop below the fist
    P.append((revolve([(0.018, 0.0), (0.028, 0.012), (0.028, 0.028), (0.018, 0.04)], 8).rot('x', -90).move(0, 0.10, 0), 40))
    P.append((torus(0.04, 0.0095, n=10, m=5).rot('y', 90).move(0, -0.60, 0), 50))                                                  # butt ring
    # socket + crossbar (along Z) + tines (towards +X, the leading edge)
    P.append((box(0.06, 0.09, 0.07, bevel=0.01).move(0, 1.60, 0), 30))
    P.append((tube([(0, 1.60, -0.27), (0, 1.605, -0.1), (0, 1.605, 0.1), (0, 1.60, 0.27)], 0.02, n=4, r2=0.014, up=(1, 0, 0), phase=math.pi / 4), 30))
    tips = []
    for k, z in enumerate(np.linspace(-0.245, 0.245, 6)):
        Ln = 0.19 + 0.02 * ((k * 3) % 2)
        path = M.polyline([(0.0, 1.602, z), (0.07, 1.610, z), (0.14, 1.585, z * 1.02), (Ln, 1.52, z * 1.03)], 6)
        P.append((horn(path, 0.0135, n=4, r_tip=0.0022, power=0.8, up=(0, 1, 0)), 30))
        tips.append((Ln, 1.52, z * 1.03))
    # the poker: straight spike with a back hook
    P.append((horn(M.polyline([(0, 1.62, 0), (0, 1.78, 0), (0, 1.97, 0)], 5), 0.022, n=4, r_tip=0.0025, power=0.9), 30))
    tips.append((0.0, 1.97, 0.0))
    P.append((horn(M.polyline([(0, 1.70, 0), (-0.06, 1.74, 0), (-0.11, 1.70, 0), (-0.12, 1.63, 0)], 7), 0.016, n=4, r_tip=0.003, power=0.9, up=(0, 0, 1)), 30))
    tips.append((-0.12, 1.63, 0.0))
    # braces from the shaft to the crossbar
    for sz in (-1, 1):
        P.append((tube([(0, 1.44, 0), (0, 1.52, sz * 0.09), (0, 1.595, sz * 0.2)], 0.008, n=4, phase=math.pi / 4), 30))
    G = M.grip_frame('r')
    glow = []
    for t in tips:
        q = (G @ np.array([t[0], t[1], t[2], 1.0]))[:3]
        glow.append((tuple(float(x) for x in q), 0.035, 0.15))
    C.look('rake', 'iron', 'metal_iron', amount=0.45, rough=0.6, dust=0.15, hammer=40.0, glow=glow, eslot='emis_ember', strength=3.2, heat=1.0)
    for i, (p, sm) in enumerate(P):
        p.to_grip('r')
        C.add(p, look='shaft' if i < 4 else 'rake', obj='wpn_1', smooth=sm)


# ====================================================================================================== assembly
def add_body(C, D=None, only=('body', 'cloth', 'weapon')):
    b, pose, dz = thin_body()
    if 'body' in only:
        body_looks(C)
        bvh = add_torso(C, b)
        add_spine_knobs(C, bvh)
        add_arms(C, b)
        add_legs(C)
        add_firewood(C)
        add_bell(C)
    else:
        bvh = SC.bvh_of(b)
    if 'cloth' in only:
        if 'body' not in only:
            body_looks(C)
        cloth_looks(C)
        add_cloth(C, b, bvh)
    if 'weapon' in only:
        if 'body' not in only and 'cloth' not in only:
            body_looks(C)
        add_weapon(C)
