"""Fork C kit: rigid plate armour fitted to the UAL bones, mechanical parts (gears, rivets, pistons), ice,
pole weapons.  Everything returns Parts in rest (T-pose) armature space unless stated otherwise."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_bones as KB
import kit_flesh as KF
from monlib import V, tube, ellipsoid, box, join, loft, loft_z, rings_z, revolve, lerp, blade, horn, hang, arc_pts, band, sheet


def BP(bone, t, off=(0, 0, 0)):
    """point at fraction t along a bone (head -> tail) plus a world offset"""
    return lerp(M.RIG.H(bone), M.RIG.T(bone), t) + np.asarray(off, dtype=np.float64)


def limb_plate(bone, st, n=10, off=(0, 0, 0), up=None, cap=True, power=2.0, lip=0.0, lip_t=0.03, phase=0.0, smooth=38.0):
    """tube plate around a limb bone. st = [(t, r[, r2]), ...]; r is along the `up` hint (default Z for arms,
    front/back for legs), r2 across. lip adds rolled rims at both ends."""
    st = [(s[0], s[1], s[2] if len(s) > 2 else s[1]) for s in st]
    if lip > 0:
        a = st[0]; b = st[-1]
        st = [(a[0], a[1] + lip, a[2] + lip), (a[0] + lip_t, a[1] + lip, a[2] + lip), (a[0] + lip_t * 1.4, a[1], a[2])] + st[1:-1] + \
             [(b[0] - lip_t * 1.4, b[1], b[2]), (b[0] - lip_t, b[1] + lip, b[2] + lip), (b[0], b[1] + lip, b[2] + lip)]
    path = [BP(bone, s[0], off) for s in st]
    p = tube(path, [s[1] for s in st], n=n, r2=[s[2] for s in st], up=up, cap=cap, power=power, phase=phase)
    p.bone = bone
    p.smooth = smooth
    return p


def ridge_mod(amp=0.07, width=0.3, at=-math.pi / 2):
    def f(th, z):
        d = np.arctan2(np.sin(th - at), np.cos(th - at))
        return 1.0 + amp * np.exp(-(d / width) ** 2)
    return f


def torso_plate(secs, n=18, ridge=0.0, ridge_w=0.3, cap0=True, cap1=True, mod=None, smooth=38.0):
    """closed lofted plate from horizontal sections (z, cx, cy, rx, ry[, power])"""
    md = mod if mod is not None else (ridge_mod(ridge, ridge_w) if ridge else None)
    p = M.loft(rings_z(secs, n, md), cap0, cap1).recalc()
    p.smooth = smooth
    return p


def shell_arc(secs, a0, a1, n=8, th=0.012, mod=None, smooth=38.0):
    """curved plate: part of an elliptical loft between angles a0..a1 (deg, 0=+X, -90=front), with thickness"""
    a = np.radians(np.linspace(a0, a1, n))
    rings = []
    for s in secs:
        z, cx, cy, rx, ry = s[:5]
        k = np.ones(n) if mod is None else mod(a, z)
        rings.append(np.stack([cx + rx * k * np.cos(a), cy + ry * k * np.sin(a), np.full(n, z)], axis=1))
    p = sheet(np.array(rings)).solidify(th).recalc()
    p.smooth = smooth
    return p


def loft_point(secs, z, a_deg, mod=None, out=0.0):
    """point (and outward normal) on a sectioned loft at height z and angle a (deg; -90 = front)"""
    zs = [s[0] for s in secs]
    i = int(np.clip(np.searchsorted(zs, z) - 1, 0, len(secs) - 2))
    t = (z - zs[i]) / max(zs[i + 1] - zs[i], 1e-9)
    s = lerp(np.array(secs[i][:5]), np.array(secs[i + 1][:5]), t)
    a = math.radians(a_deg)
    k = 1.0 if mod is None else float(np.asarray(mod(np.array([a]), z))[0])
    nr = M.unit([math.cos(a) / s[3], math.sin(a) / s[4], 0.0])
    p = np.array([s[1] + s[3] * k * math.cos(a), s[2] + s[4] * k * math.sin(a), z]) + nr * out
    return p, nr


def surf_strip(secs, za, w=0.008, th=0.004, mod=None, out=0.001):
    """thin strip (seam / crack / inlay) following a sectioned loft through [(z, angle_deg), ...]"""
    pts = []; nrs = []
    for z, a in za:
        p, nr = loft_point(secs, z, a, mod, out)
        pts.append(p); nrs.append(nr)
    s = tube(np.array(pts), th * 0.7071, n=4, r2=w * 0.7071, up=nrs[0], phase=math.pi / 4)
    s.smooth = 20.0
    return s


def ring_pts(c, rx, ry, count, a0=0.0, a1=360.0, z=None):
    closed = abs((a1 - a0) - 360.0) < 1e-6
    a = np.radians(np.linspace(a0, a1, count, endpoint=not closed))
    pts = np.stack([c[0] + rx * np.cos(a), c[1] + ry * np.sin(a), np.full(count, c[2] if z is None else z)], axis=1)
    nr = np.stack([np.cos(a) / rx, np.sin(a) / ry, np.zeros(count)], axis=1)
    nr /= np.linalg.norm(nr, axis=1)[:, None]
    return pts, nr


def rivet(p, nrm, r=0.008, n=5):
    """low-poly dome rivet at p facing nrm"""
    rv = revolve([(r, 0.0), (r * 0.75, r * 0.45), (0.0, r * 0.7)], n, cap=False)
    rv.tf(M.rot_to((0, 0, 1), nrm)).move(np.asarray(p) - M.unit(nrm) * r * 0.1)
    rv.smooth = 60.0
    return rv


def rivets(pts, nrms, r=0.008, n=5):
    return join([rivet(p, q, r, n) for p, q in zip(pts, nrms)])


def gear(r=0.06, teeth=10, th=0.014, depth=0.2, hub=0.3, flat=0.5):
    """toothed disc in the XZ plane (axis = Y), centred at the origin"""
    out = []
    for i in range(teeth):
        a0 = 2 * math.pi * i / teeth
        da = 2 * math.pi / teeth
        ri = r * (1 - depth)
        for (f, rr) in ((0.0, ri), (0.5 - flat / 2, ri), (0.5 - flat / 2 + 0.06, r), (0.5 + flat / 2 - 0.06, r)):
            a = a0 + da * f
            out.append((rr * math.cos(a), rr * math.sin(a)))
    g = M.prism(out, -th / 2, th / 2).rot('x', 90)
    h = revolve([(r * hub, -th * 0.9), (r * hub, th * 0.9), (r * hub * 0.5, th * 1.2)], 8).rot('x', 90)
    g = join([g, h])
    g.smooth = 25.0
    return g


def disc(r, th, n=14, bevel=0.0):
    """cylinder with axis Y"""
    if bevel > 0:
        prof = [(r - bevel, -th / 2), (r, -th / 2 + bevel), (r, th / 2 - bevel), (r - bevel, th / 2)]
    else:
        prof = [(r, -th / 2), (r, th / 2)]
    d = revolve(prof, n).rot('x', 90)
    d.smooth = 35.0
    return d


def cyl(p0, p1, r, n=8, r1=None, cap=True):
    r1 = r if r1 is None else r1
    c = tube([p0, p1], [r, r1], n=n, cap=cap)
    c.smooth = 40.0
    return c


def icicle(base, length, r=0.012, dirv=(0, 0, -1), n=5, bend=0.15, seed=0):
    rng = np.random.default_rng(seed)
    d = M.unit(dirv)
    side = M.unit(np.cross(d, (0.3, 0.9, 0.2)))
    b = np.asarray(base, dtype=np.float64)
    wob = rng.uniform(-1, 1) * bend * length
    path = [b - d * r * 0.8, b + d * length * 0.45 + side * wob * 0.5, b + d * length]
    ic = horn(M.polyline(path, 4), r, n=n, r_tip=0.0008, power=0.85)
    ic.smooth = 30.0
    return ic


def icicle_row(pts, lengths, r=0.012, dirv=(0, 0, -1), seed=0, n=5):
    rng = np.random.default_rng(seed)
    out = []
    for i, p in enumerate(pts):
        L_ = lengths[i] if hasattr(lengths, '__len__') else lengths * rng.uniform(0.45, 1.0)
        out.append(icicle(p, L_, r * rng.uniform(0.7, 1.1), dirv, n, seed=seed * 31 + i))
    return join(out)


def ice_chunk(rx, ry, rz, seed=0, n=12, jit=0.3):
    c = M.rock(rx, ry, rz, seed=seed, n=n, jit=jit)
    c.smooth = 0.0
    return c


def crystal(r, h, n=5, seed=0, tip=0.55):
    """elongated ice crystal along +Z, base at the origin"""
    c = revolve([(r * 0.75, 0.0), (r, h * tip), (0.0, h)], n, cap=True)
    c.smooth = 0.0
    return c


def polearm(shaft_back=0.6, shaft_front=1.2, r=0.016, n=7, butt=True):
    """shaft along +Y in grip space (origin = fist)"""
    parts = [tube([(0, -shaft_back, 0), (0, -0.2, 0), (0, shaft_front * 0.5, 0), (0, shaft_front, 0)], r, n=n)]
    if butt:
        parts.append(revolve([(r * 1.5, 0), (r * 1.7, 0.02), (r * 1.2, 0.05), (0, 0.11)], 6).rot('x', 90).move(0, -shaft_back + 0.02, 0))
    return parts


def cape_pleated(w_top=0.44, w_bot=0.72, length=1.0, z_top=1.47, y=0.21, cols=13, rows=11, pleats=4.5, amp=0.035, flare=0.18,
                 bow=0.06, rag=0.3, seed=5, holes=0.12, slope=0.03):
    """back cape with vertical pleats that deepen towards a torn hem (unweighted; use KC.cape_weights)"""
    rng = np.random.default_rng(seed)
    u = np.linspace(-1, 1, cols)
    Lc = length * (1 - rag * rng.uniform(0, 1, cols) * (0.35 + 0.65 * np.abs(np.sin(u * 7.0 + seed))))
    G = np.zeros((rows + 1, cols, 3))
    ph = rng.uniform(0, 6.28)
    for i in range(rows + 1):
        t = i / rows
        w = lerp(w_top, w_bot, t ** 0.8)
        G[i, :, 0] = u * w / 2
        G[i, :, 1] = y - bow * (u ** 2) * (1 - 0.6 * t) + flare * t ** 1.3 + amp * t ** 0.7 * np.sin(pleats * math.pi * u + ph)
        G[i, :, 2] = z_top - Lc * t - slope * (u ** 2) * (1 - t)
    s = sheet(G)
    if holes:
        keep = []
        for i in range(rows):
            for j in range(cols - 1):
                t = (i + 1) / rows
                keep.append(not (t > 0.55 and rng.uniform() < holes * t))
        s = s.sub(np.array(keep))
    s.smooth = 180.0
    return s


def under_suit(ratio=0.4, head=False, hands=False, kind='male', body=None):
    """decimated base body (minus head / hands) used as a dark layer under rigid armour"""
    b = KC.ref_body(kind) if body is None else body
    drop = np.zeros(len(b.f), dtype=bool)
    if not head:
        drop |= b.vmask_faces((b.wsum(['Head']) > 0.5) & (b.v[:, 2] > 1.575), 'all')
    if not hands:
        hb = M.RIG.chain('hand_l') + M.RIG.chain('hand_r')
        drop |= b.vmask_faces(b.wsum(hb) > 0.6, 'all')
    s = b.sub(~drop)
    s = M.decimate(s, ratio)
    s.smooth = 180.0
    return s


def gauntlet_hands(inflate=0.004, ratio=0.7, kind='male'):
    b = KC.ref_body(kind)
    hb = M.RIG.chain('hand_l') + M.RIG.chain('hand_r')
    g = b.sub(b.vmask_faces(b.wsum(hb) > 0.4, 'any'))
    if ratio < 1.0:
        g = M.decimate(g, ratio)
    g.inflate(inflate)
    g.smooth = 50.0
    return g


def finger_blocks(C, look, w=0.017, h=0.016, sides=('l', 'r'), taper=0.8, smooth=30.0):
    """rigid box phalanges on every finger bone (constructs)"""
    R = M.RIG
    for side in sides:
        for fg in ('index', 'middle', 'ring', 'pinky', 'thumb'):
            for j in (1, 2, 3):
                bn = '%s_0%d_%s' % (fg, j, side)
                a = R.H(bn); b = R.T(bn)
                L_ = np.linalg.norm(b - a) * (1.25 if j == 3 else 0.92)
                k = taper ** (j - 1) * (1.15 if fg == 'thumb' else 1.0)
                bx = box(w * k, L_, h * k, bevel=0.0025).move(0, L_ / 2, 0)
                if j == 3:
                    sel = bx.v[:, 1] > L_ * 0.8
                    bx.v[sel, 0] *= 0.6; bx.v[sel, 2] *= 0.6
                bx.orient(b - a, (0, 0, 1), a)
                C.add(bx, bone=bn, look=look, smooth=smooth)


# ------------------------------------------------------------------------------------------------ extra look kinds
def k_sand(N, p):
    col = p.get('color', (0.48, 0.38, 0.22))
    big = N.noise(7, 4, 0.6)
    fine = N.noise(160, 2, 0.7)
    rip = N.noise(40, 2, 0.5, vec=N.mapping(N.coord(), scale=(1, 1, 0.1)))
    c = N.mix(N.mapr(big, 0.3, 0.7, 0.0, 0.45), col, N.mul(col, 0.55))
    c = N.mix(N.mapr(fine, 0.3, 0.8, 0.0, 0.35), c, N.mul(c, 1.5))
    c = N.mix(N.mapr(rip, 0.4, 0.75, 0.0, 0.3), c, N.mul(col, 0.6))
    h = N.math('ADD', N.math('MULTIPLY', fine, 0.6), N.math('MULTIPLY', rip, 0.4))
    return dict(color=c, rough=0.95, metal=0.0, height=h, bump=(0.5, 0.003))


L.KINDS['sand'] = k_sand
