"""Emberblade armour looks t1..t3 (t4 = today's hero, split by split_t4.py).

Identity: heavy two-hand warden of the ember beacons -- soot-dark steel, bronze trim, dark blood-red cloth, dark fur,
ember only as tiny accents. Hero materials reused: steel, mail, bronze, iron, leather, ember.
  t1  scavenged beacon-yard gear: dented rusty skull cap over a torn padded coif, soot-stained oxblood gambeson with a
      ragged hem, a dented half breastplate tied on with rope, rope belt, rag hand wraps and one old strapped bracer,
      patched breeches torn at the shin with a knee rag, rag-bound foot wraps on old leather soles.
  t2  beacon-line soldier: banded conical nasal helm over a mail coif, elbow-sleeved hauberk with a mail skirt under a
      plain belted blood-red surcoat split at the front, plain spaulders, long leather gauntlets with steel cuffs, wool
      hose with knee cops, calf boots with strapped shin splints.
  t3  warden-captain: barbute with a bronze brow band (one small ember stone) and a swept horsehair crest,
      bronze-trimmed cuirass with the beacon flame, plackart and fauld over mail, bronze-rimmed pauldrons, a red
      half-cape from the left shoulder, plate gauntlets over mail, cuisses and winged poleyns over mail legs,
      bronze-banded greaves and sabatons.

Helpers of this file that work round the shared kit on the male body are marked in their docstrings (_arm, _psize,
_cuff, _couter, _arm_rings, _soles).
"""
from gearkit import *


def _mats():
    mats.metal("eb_rust", base=(0.150, 0.135, 0.122), rough=(0.45, 0.85), rust_amt=0.62, pattern="hammered", ash=0.25, soot=0.3)
    mats.cloth("eb_gambeson", base=(0.078, 0.036, 0.026), var=0.45, ash=0.45, ash_h=0.6, folds=0.6, worn=0.85, scan="canvas", tight=True)
    mats.cloth("eb_coif", base=(0.170, 0.140, 0.098), var=0.4, ash=0.5, ash_h=0.6, folds=0.4, worn=0.9, scan="canvas", tight=True)
    mats.cloth("eb_footrag", base=(0.088, 0.072, 0.052), var=0.45, ash=0.4, folds=0.5, worn=0.85, scan="burlap", tight=True)
    mats.cloth("eb_rag", base=(0.165, 0.135, 0.095), var=0.45, ash=0.5, folds=0.4, weave=1.0, worn=0.9, scan="linen", tight=True)
    mats.cloth("eb_breeches", base=(0.066, 0.056, 0.046), var=0.5, ash=0.55, folds=0.5, worn=0.85, scan="wool", tight=True)
    mats.leather("eb_oldleather", base=(0.066, 0.041, 0.026), worn=0.9, ash=0.5, scan="leather_old")
    mats.cloth("eb_rope", base=(0.175, 0.14, 0.088), var=0.3, ash=0.4, folds=0.0, scan="rope", hem=False)
    mats.metal("eb_plain", base=(0.215, 0.21, 0.205), rough=(0.34, 0.62), rust_amt=0.2, pattern="plate", ash=0.2, soot=0.15)
    mats.cloth("eb_surcoat", base=(0.110, 0.023, 0.017), var=0.3, folds=0.7, ash=0.5, ash_h=0.5, worn=0.5, scan="wool")
    mats.cloth("eb_hose", base=(0.052, 0.044, 0.040), folds=0.5, ash=0.4, ash_h=0.4, scan="twill", tight=True)
    mats.cloth("eb_cape", base=(0.130, 0.021, 0.016), bands=[(0.03, 0.085, (0.26, 0.16, 0.05))], var=0.3, folds=0.9, ash=0.4, ash_h=0.6,
               worn=0.3, scan="wool")
    mats.cloth("eb_crest", base=(0.150, 0.026, 0.018), var=0.35, folds=0.0, ash=0.1, worn=0.2, scan="fleece", hem=False)


# ---------------------------------------------------------------------------------------------- helpers of this class
FLAME = [(-0.050, -0.055), (0.050, -0.055), (0.050, -0.040), (0.030, -0.040), (0.046, 0.000), (0.030, 0.040), (0.020, 0.012),
         (0.000, 0.075), (-0.020, 0.012), (-0.030, 0.040), (-0.046, 0.000), (-0.030, -0.040), (-0.050, -0.040)]


def _rope(K, name, src, pts, r=0.0065, off=0.021, closed=False, step=0.014,
          only=("spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r"), tuck=0.0):
    """round rope lying on the body through the guide points; tuck = how far the two ends dive towards the skin"""
    c = K.c
    G = [np.array(p, float) for p in pts]
    P = resample(G + ([G[0]] if closed else []), step)
    if closed:
        P = P[:-1]
    n = len(P)
    offs = np.full(n, off)
    if tuck and not closed:
        t = np.linspace(0, 1, n)
        offs = off - tuck * (1 - sstep(t, 0.0, 0.10)) - tuck * sstep(t, 0.90, 1.0)
    S_, N_ = [], []
    for p in P:
        ok, loc, nrm, fi = c.ref.closest_point_on_mesh(Vector(p))
        S_.append(np.array(loc))
        N_.append(np.array(nrm))
    Q = np.array(S_) + np.array(N_) * offs[:, None]
    for _ in range(2):
        if closed:
            Q = (np.roll(Q, 1, 0) + Q * 2 + np.roll(Q, -1, 0)) / 4
        else:
            Q[1:-1] = (Q[:-2] + Q[1:-1] * 2 + Q[2:]) / 4
    for i in range(n):                                   # smoothing cuts corners: restore the clearance
        ok, loc, nrm, fi = c.ref.closest_point_on_mesh(Vector(Q[i]))
        d = float(np.dot(Q[i] - np.array(loc), np.array(nrm)))
        if d < offs[i]:
            Q[i] = Q[i] + np.array(nrm) * (offs[i] - d)
    o = add(name, g_tube(Q, r, n=6, closed=closed), "cloth", src, sharp=180)
    w_transfer(o, c.ref, smooth=2, only=list(only))
    return o


def _arm(K, x, dx=0.025):
    """(cy, cz, ry, rz) of the LEFT arm round station x (the Kit's forearm_r / upperarm_r are the largest distance from
    the nominal arm axis, which is far more than the real radius on the male body)"""
    B = K.B
    m = (B.w("upperarm", "lowerarm") > 0.5) & (B.x > 0) & (np.abs(B.x - x) < dx)
    y0, y1, z0, z1 = B.y[m].min(), B.y[m].max(), B.z[m].min(), B.z[m].max()
    return float((y0 + y1) / 2), float((z0 + z1) / 2), float((y1 - y0) / 2), float((z1 - z0) / 2)


def _psize(K, radius):
    """size argument of K.pauldron that gives a shoulder plate of the given radius (m) on this body"""
    return radius / (0.122 * max(1.0, K.upperarm_r / 0.060) ** 0.6)


def _cuff(K, name, src, x=0.60, length=0.085, flare=0.016, clear=0.014, trim=None):
    """flared gauntlet cuff fitted to the real forearm section (wide end towards the elbow), rigid to the forearm"""
    cy, cz, ry, rz = _arm(K, x + length * 0.25)
    cy1, cz1, ry1, rz1 = _arm(K, x + length)
    rm = (ry + rz) / 2 + clear
    r1 = (ry1 + rz1) / 2 + clear * 0.75
    M = frame((x, cy, cz), (1, 0, 0))
    kw = dict(n=14, sx=(rz + clear) / rm, sy=(ry + clear) / rm)
    out = [add(name + "_l", gxf(g_lathe([(rm + flare, 0.0), (rm + flare * 0.40, length * 0.45), (r1, length)], **kw), M), "metal", src,
               solidify=0.004, bone="lowerarm_l", sharp=50, bevel=0.0015)]
    if trim:
        out.append(add(name + "_trim_l", gxf(g_lathe([(rm + flare + 0.002, -0.004), (rm + flare + 0.0055, 0.004), (rm + flare + 0.001, 0.012)], **kw), M),
                       "metal", trim, bone="lowerarm_l", sharp=60))
    return out + [mirrored(q) for q in list(out)]


def _couter(K, name, src, wing=None, clear=0.013):
    """elbow cop fitted to the real elbow section"""
    cy, cz, ry, rz = _arm(K, 0.468, 0.02)
    o = add(name + "_l", gxf(g_sphere(1.0, 10, 6), T(0.468, cy + 0.006, cz) @ S(0.058, ry + clear, rz + clear)), "metal", src, sharp=60)
    w_func(o, lambda co: {"upperarm_l": 0.5, "lowerarm_l": 0.5})
    out = [o]
    if wing:
        w = add(name + "_wing_l", ydome([(0.0, 0.011), (0.019, 0.009), (0.033, 0.0), (0.033, -0.006), (0.0, -0.006)], n=8, x0=0.468,
                                        y0=cy + 0.006 + (ry + clear) * 0.985, z0=cz), "metal", wing, sharp=50)
        w_func(w, lambda co: {"upperarm_l": 0.5, "lowerarm_l": 0.5})
        out.append(w)
    return out + [mirrored(q) for q in list(out)]


def _arm_rings(K, name, src, xs, width=0.018, off=0.016, thick=0.004, cls="leather", n=16, sides="lr"):
    """rigid hoops round the forearm at the stations xs, shaped by the real section of the arm there (K.arm_rings /
    K.limb_straps start from forearm_r, which is far too wide on the male body, and come out loose or wavy)"""
    c, B = K.c, K.B
    gs = []
    for x in xs:
        cy, cz, ry, rz = _arm(K, x, 0.03)
        ang = np.linspace(0, TAU, n, endpoint=False)
        rad = []
        for xx in (x - width / 2, x + width / 2):
            row = []
            for a in ang:
                d = np.array((0.0, math.cos(a), math.sin(a)))
                p0 = np.array((xx, cy, cz))
                ok, loc, nrm, fi = c.ref.ray_cast(Vector(p0 + d * 0.25), Vector(-d))
                row.append(float(np.linalg.norm(np.array(loc) - p0)) if ok else 0.06)
            rad.append(row)
        r = np.maximum(np.array(rad[0]), np.array(rad[1]))
        r = (np.roll(r, 1) + 2 * r + np.roll(r, -1)) / 4 + off
        V, F = [], []
        for xx, bulge in ((x - width / 2, 0.0), (x, 0.0018), (x + width / 2, 0.0)):
            V += [(xx, cy + (r[i] + bulge) * math.cos(a), cz + (r[i] + bulge) * math.sin(a)) for i, a in enumerate(ang)]
        for j in range(2):
            F += [(j * n + i, j * n + (i + 1) % n, (j + 1) * n + (i + 1) % n, (j + 1) * n + i) for i in range(n)]
        gs.append(geo(V, F))
    o = add(name + "_l", gjoin(*gs), cls, src, bone="lowerarm_l", sharp=50, solidify=thick)
    if sides == "l":
        return [o]
    r_ = mirrored(o)
    if sides == "r":
        remove_part(o)
        return [r_]
    return [o, r_]


def _hull(P):
    """convex hull of 2D points, counter-clockwise"""
    pts = sorted(set(map(tuple, np.round(np.asarray(P, float), 5))))

    def cr(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for p in pts:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], p) <= 0:
            lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(up) >= 2 and cr(up[-2], up[-1], p) <= 0:
            up.pop()
        up.append(p)
    return np.array(lo[:-1] + up[:-1])


def _soles(K, boot, name, src, cls):
    """sole slab under a K.boots shell. K.boots flattens its sole at z = -0.004, but the feet of the body reach down to
    z = -0.010: without the slab the bare toe and heel pads hang out under the boot (and are carried along as visible
    skin), which shows whenever the sole is seen (roll)"""
    B = K.B
    co = get_co(boot)
    zb = float(B.z.min()) - 0.0025
    gs = []
    for s in (1, -1):
        h = _hull(co[(co[:, 0] * s > 0) & (co[:, 2] < 0.012)][:, :2])
        cen = h.mean(0)
        n = len(h)
        V = [(x, y, -0.0025) for x, y in h] + [(cen[0] + (x - cen[0]) * 0.95, cen[1] + (y - cen[1]) * 0.975, zb) for x, y in h]
        F = [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)] + [tuple(range(2 * n - 1, n - 1, -1))]
        gs.append(geo(V, F))
    o = add(name, gjoin(*gs), cls, src, sharp=50)
    w_transfer(o, K.c.ref, smooth=0)
    return o


def _leg(K, z, side=1, dz=0.02):
    """(cx, cy) of one leg at height z"""
    B = K.B
    m = (B.x * side > 0) & (np.abs(B.z - z) < dz) & (B.w(*BONES_LEG, *BONES_FOOT) > 0.5)
    return float((B.x[m].min() + B.x[m].max()) / 2), float((B.y[m].min() + B.y[m].max()) / 2)


def _leg_strip(K, z0, z1, a0, a1, off, nu=3, nv=7):
    """strip of plate lying `off` above the skin of the LEFT leg between two heights and two angles round the leg
    (fractions of a turn: 0 = outside, .25 = back, .75 = front)"""
    c = K.c
    zs = np.linspace(z0, z1, 6)
    cen = np.array([_leg(K, z) for z in zs])

    def fn(U, V):
        z = z0 + (z1 - z0) * V
        cx = np.interp(z, zs, cen[:, 0])
        cy = np.interp(z, zs, cen[:, 1])
        a = TAU * (a0 + (a1 - a0) * U)
        return np.stack([cx + 0.2 * np.cos(a), cy + 0.2 * np.sin(a), z], axis=-1)
    g = g_grid(fn, nu, nv, close_u=False)
    v = g["v"].copy()
    for i, p in enumerate(v):
        # closest skin point of the same height band (looking inwards along the radius)
        cx, cy = np.interp(p[2], zs, cen[:, 0]), np.interp(p[2], zs, cen[:, 1])
        d = np.array((cx - p[0], cy - p[1], 0.0))
        d /= np.linalg.norm(d)
        ok, loc, nrm, fi = c.ref.ray_cast(Vector(p), Vector(d))
        if ok:
            v[i] = np.array(loc) - d * off
        else:
            ok, loc, nrm, fi = c.ref.closest_point_on_mesh(Vector(p))
            v[i] = np.array(loc) + np.array(nrm) * off
    g["v"] = v
    return g


def _splints(K, name, src, z0=0.165, z1=0.405, off=0.0225, angs=(0.648, 0.75, 0.852), aw=0.070):
    out = []
    gs = [_leg_strip(K, z0 + 0.012 * abs(i - 1), z1 - 0.014 * abs(i - 1), a - aw / 2, a + aw / 2, off) for i, a in enumerate(angs)]
    o = add(name + "_l", gjoin(*gs), "metal", src, bone="calf_l", sharp=50, solidify=0.004)
    return [o, mirrored(o)]


def _dent(o, n=3, depth=0.008, radius=0.04, seed=1, zmin=None):
    """push a few shallow dents into a plate (towards its own centre line)"""
    co = get_co(o)
    rng = np.random.RandomState(seed)
    cen = co.mean(0)
    pick = np.arange(len(co)) if zmin is None else np.nonzero(co[:, 2] > zmin)[0]
    for _ in range(n):
        p = co[pick[rng.randint(len(pick))]]
        d = np.linalg.norm(co - p, axis=1)
        k = depth * np.exp(-(d / radius) ** 2)
        dirn = cen - co
        dirn /= np.maximum(np.linalg.norm(dirn, axis=1, keepdims=True), 1e-9)
        co = co + dirn * k[:, None]
    set_co(o, co)
    return o


def _skullcap(K, name, src, rim_src=None, rx=0.1045, ryf=0.123, ryb=0.119, top=0.116, dents=3):
    """shallow open skull cap that ends above the ears; dented"""
    gh = g_barbute(rx=rx, ryf=ryf, ryb=ryb, top=top, eye_deg=80.0, chin_deg=80.0, z_rim=1.694, tail=0.034, z_eye0=1.69, z_eye1=1.70,
                   brow=1.724, flare=0.004, comb=0.0)
    o = add(name, gh, "metal", src, bone="Head", sharp=50, solidify=0.004)
    weld(o)
    _dent(o, dents, 0.007, 0.038, K.rs(), zmin=1.74)
    # rolled rim (sides, back and across the brow) and a row of rivets
    rows = grid_rows(gh, 30)
    rim = [rows[j, 0] for j in (5, 4, 3, 2, 1)] + [rows[0, i] for i in range(30)] + [rows[j, 29] for j in (1, 2, 3, 4)]
    riv = gjoin(*[gxf(g_sphere(0.0048, 5, 3), T(*(rows[3, i] + (rows[3, i] - np.array((0.0, -0.012, 1.70))) * 0.012))) for i in range(2, 29, 3)])
    add(name + "_rim", gjoin(g_tube(rim, 0.0046, n=5, closed=True), riv), "metal", rim_src or src, bone="Head", sharp=60)
    return o, gh


def _conical(K, name, src, rib_src, z_rim=1.700, apex=1.900, ry=0.1255, rx=0.1075, cy=-0.012, tilt=-5.0):
    """banded conical nasal helm (four-plate skull with rim band, ribs, finial and nasal), rigid to the head"""
    h = apex - z_rim
    prof = [(1.000, 0.0), (1.004, 0.10), (0.978, 0.27), (0.890, 0.48), (0.715, 0.68), (0.480, 0.85), (0.235, 0.955), (0.0, 1.0)]
    k = rx / ry
    M = T(0, cy, 1.70) @ R('X', tilt) @ T(0, 0, -1.70)
    out = []
    g = g_lathe([(ry * r, z_rim + h * t) for r, t in prof], n=22, sx=k)
    out.append(add(name, gxf(g, M), "metal", src, bone="Head", sharp=50, solidify=0.004, solid_offset=-1.0))
    band = g_lathe([(ry + 0.0016, z_rim - 0.004), (ry + 0.0040, z_rim + 0.010), (ry * 1.003 + 0.0020, z_rim + 0.026)], n=22, sx=k)
    ribs = []
    for ang in (0.0, 90.0, 180.0, 270.0):
        ca, sa = math.cos(math.radians(ang)), math.sin(math.radians(ang))
        path = [((ry * r + 0.0015) * ca * k, (ry * r + 0.0015) * sa, z_rim + 0.02 + (h - 0.02) * t) for r, t in prof[:-1]]
        ribs.append(g_tube(path, 1.0, n=4, sx=0.0035, sy=0.0105, up=(ca, sa, 0.0), caps=True))
    fin_ = g_lathe([(0.020, apex - 0.030), (0.013, apex - 0.012), (0.016, apex - 0.002), (0.010, apex + 0.012), (0.0, apex + 0.020)], n=8)
    nasal = g_tube([(0.0, -ry - 0.001, z_rim + 0.024), (0.0, -ry - 0.005, z_rim - 0.006), (0.0, -ry - 0.012, z_rim - 0.040),
                    (0.0, -ry - 0.016, z_rim - 0.076)], [1.0, 1.0, 1.05, 1.25], n=6, sx=0.0042, sy=0.0105, up=(0, -1, 0))
    out.append(add(name + "_bands", gxf(gjoin(band, fin_, nasal, *ribs), M), "metal", rib_src, bone="Head", sharp=50))
    return out


def _lock(p, d, length, r, bend, n=5):
    """horsehair lock: full in the middle, cut square-ish at the end"""
    d = np.array(d, float)
    d /= np.linalg.norm(d)
    p = np.array(p, float)
    pts = [p + d * length * t + np.array((0.0, 0.0, -bend)) * t * t for t in (0.0, 0.3, 0.65, 1.0)]
    return g_tube(pts, [r * 0.75, r, r * 0.92, r * 0.42], n=n)


def _crest(K, name, src, rail_src, cy=-0.012, ryf=0.127, ryb=0.124, zc=1.70, top=0.145):
    """swept horsehair crest on a bronze rail along the comb of a barbute, with a tail down the back"""
    pts, nrm = [], []
    for ph in np.radians(np.linspace(-36.0, 80.0, 11)):
        ry = ryb if ph > 0 else ryf
        pts.append(np.array((0.0, cy + (ry + 0.005) * math.sin(ph), zc + (top + 0.005) * math.cos(ph))))
        n_ = np.array((0.0, math.sin(ph) / ry, math.cos(ph) / top))
        nrm.append(n_ / np.linalg.norm(n_))
    rail = g_tube(pts, 1.0, n=5, sx=0.0085, sy=0.0075, up=(0, 0, 1))
    add(name + "_rail", rail, "metal", rail_src, bone="Head", sharp=50)
    locks = []
    m = len(pts)
    for i in range(m):
        t = i / (m - 1)
        for sx_ in (-1, 1):
            p = pts[i] + np.array((sx_ * 0.008, 0.0, 0.0)) + nrm[i] * 0.002
            d = nrm[i] * (1.0 - 0.60 * t) + np.array((sx_ * 0.16, 0.45 + 0.75 * t, 0.10 - 1.0 * t ** 2))
            ln = 0.072 + 0.040 * math.sin(math.pi * min(t * 1.25, 1.0)) + (0.12 * max(t - 0.72, 0.0) / 0.28)
            locks.append(_lock(p, d, ln, 0.0195, 0.012 + 0.06 * t))
    o = add(name, gjoin(*locks), "cloth", src, sharp=180)

    def wf(co):
        dn = sstep(1.66 - co[:, 2], 0.0, 0.14)
        return {"Head": 1 - dn * 0.6, "neck_01": dn * 0.3, "spine_03": dn * 0.3}
    w_func(o, wf)
    return o


def _front_plate(K, name, src, z0=1.165, z1=1.470, gap=0.027, half=0.140, dent=0.007, bone="spine_03"):
    """dented half breastplate (front only)"""
    zs = sorted(set([z0, z1] + [z for z in (1.25, 1.32, 1.39, 1.43) if z0 + 0.02 < z < z1 - 0.02]))
    st = K.stations(zs, gap)
    rng = np.random.RandomState(K.rs())
    dents = [(0.75 + (rng.rand() - 0.5) * 1.3 * half, 0.15 + 0.7 * rng.rand()) for _ in range(4)]

    def mod(U, V, x, y, z):
        t = (U - 0.75) / half
        y = y - 0.011 * np.exp(-(t / 0.20) ** 2) * (0.35 + 0.65 * V)             # medial ridge, stronger at the top
        z = z - 0.095 * (V ** 1.5) * np.abs(t) ** 2.4                             # armholes
        z = z - 0.030 * (V ** 4) * np.exp(-(t / 0.40) ** 2)                      # neck scoop
        z = z + 0.032 * ((1 - V) ** 2) * np.abs(t) ** 2.0                        # rounded lower edge
        for (u_, v_) in dents:
            y = y + dent * np.exp(-(((U - u_) / (0.30 * half)) ** 2 + ((V - v_) / 0.20) ** 2))
        return x, y, z
    g = g_stations(st, nu=17, nv=10, p=2.3, close=False, u0=0.75 - half, u1=0.75 + half, mod=mod)
    o = add(name, g, "metal", src, bone=bone, sharp=50)
    solid(o, 0.005)
    rows = g["v"].reshape(10, 17, 3)
    rim = list(rows[0, :]) + list(rows[1:, 16]) + list(rows[9, ::-1][1:]) + list(rows[::-1, 0][1:-1])
    add(name + "_rim", g_tube(rim, 0.0052, n=5, closed=True), "metal", src, bone=bone, sharp=60)
    return o, g


def _half_cape(K, name, src, z_top=1.475, z_bot=0.78, ua=0.105, ub=0.365, width=0.215, flare=0.05, drop=0.10, pleats=3.5, pleat_amp=0.075):
    """short cape hanging from the left shoulder over the left half of the back (hem lower at the outer side)"""
    r_t, r_b = K.ring(1.40, 0.0), K.ring(1.00, 0.0)
    cy = 0.03
    d_top = r_t[1] + r_t[4] - cy + 0.045
    d_bot = max(r_b[1] + r_b[4] - cy + 0.075, d_top + 0.03)

    def fn(U, V):
        hem = 1.0 - V
        u = ua + (ub - ua) * U
        z = z_bot + (z_top - z_bot) * V - drop * (1 - U) ** 1.5 * hem
        fold = 1.0 + pleat_amp * (hem ** 1.3) * np.sin(TAU * pleats * U + 1.0)
        tp = 0.90 + 0.10 * sstep(hem, 0.0, 0.16)
        w = (width + flare * hem) * fold * tp
        d = (d_top + (d_bot - d_top) * hem) * fold * (0.5 + 0.5 * tp)
        x, y = ell(u, 0.0, cy, w, d, d, 2.2)
        return np.stack([x, y, z], axis=-1)
    o = add(name, g_grid(fn, 15, 12, close_u=False), "cloth", src, sharp=180, double_sided=True)
    solid(o, 0.006)

    def wf(co):
        z, x = co[:, 2], co[:, 0]
        a = sstep(z, 1.26, 1.40)
        b = sstep(z, 1.10, 1.24) * (1 - a)
        cc = sstep(z, 0.98, 1.10) * (1 - a) * (1 - sstep(z, 1.10, 1.24))
        d = np.clip(1 - a - b - cc, 0, 1)
        t = sstep(0.95 - z, 0.0, 0.3) * 0.35
        l = sstep(x, -0.06, 0.06)
        return {"spine_03": a, "spine_02": b, "spine_01": cc, "pelvis": d * (1 - t), "thigh_l": d * t * l, "thigh_r": d * t * (1 - l)}
    w_func(o, wf, smooth=2)
    return o


def _emboss(name, outline, host, zc, src, s=1.0, x0=0.0, depth=0.004, bone="spine_03"):
    """flat ornament conformed to the front of a plate (every vertex is dropped on the host along +Y)"""
    apply_mods(host)
    g = gxf(g_prism([(x * s, z * s) for x, z in outline], depth), T(x0, 0.0, zc))
    v = g["v"].copy()
    for i, p in enumerate(v):
        ok, loc, nrm, fi = host.ray_cast(Vector((p[0], -1.0, p[2])), Vector((0, 1, 0)))
        if ok:
            v[i, 1] = loc[1] + (-depth if p[1] < 0 else 0.0005)
    g["v"] = v
    return add(name, g, "metal", src, bone=bone, sharp=40)


# ---------------------------------------------------------------------------------------------- t1 scavenged
def t1(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.cap("coif_pad", "eb_coif", z_brow=1.722, ears=True, off=0.009, cls="cloth", nape=1.585, chew=1, chew_p=0.3,
              keep=lambda co: co.z > 1.66)
        _skullcap(K, "skullcap", "eb_rust", "iron")
        K.strap("chin_strap", "eb_oldleather", [(0.094, -0.01, 1.70), (0.086, -0.04, 1.62), (0.045, -0.085, 1.565), (0.0, -0.098, 1.553),
                                               (-0.045, -0.085, 1.565), (-0.086, -0.04, 1.62), (-0.094, -0.01, 1.70)],
                width=0.016, thick=0.004, off=0.006, closed=False, only=["Head"])
    with K.slot("chest"):
        K.tunic("gambeson", "eb_gambeson", lo=0.995, hi=1.51, sleeve=0.335, off=0.014, wrinkle=0.0028, wfreq=24.0, chew=2, chew_p=0.3,
                keep=lambda co: co.z > 1.10 and abs(co.x) < 0.26)
        K.skirt("gambeson_skirt", "eb_gambeson", z_top=1.045, z_bot=0.76, gap_top=0.020, gap_bot=0.040, tails=7, tail_depth=0.13, ragged=0.03,
                folds=((6, 0.03, 0.1), (11, 0.012, 0.5)))
        K.rope("rope_belt", "eb_rope", z=1.062, gap=0.026, r=0.0085, cls="cloth", turns=2)
        pl, g = _front_plate(K, "halfplate", "eb_rust")
        # the plate is tied on: two ropes round the back, two over the shoulders
        rows = g["v"].reshape(10, 17, 3)
        for i, r in enumerate((2, 6)):
            a, b = rows[r, 16], rows[r, 0]                           # left and right edge of the plate
            zb = float(a[2]) + 0.01
            pts = [a + np.array((-0.03, 0.0, 0.0)), a, (0.20, 0.03, zb), (0.14, 0.16, zb + 0.01), (0.0, 0.19, zb + 0.015), (-0.14, 0.16, zb + 0.01),
                   (-0.20, 0.03, zb), b, b + np.array((0.03, 0.0, 0.0))]
            _rope(K, "plate_rope_%d" % i, "eb_rope", pts, r=0.006, off=0.0215, tuck=0.004, only=("spine_02", "spine_03"))
        knots = []
        for s in (1, -1):                                             # shoulder ropes: plate top -> knotted on the upper back rope
            top = rows[9, 8 + 5 * s]
            mid, end = wrap_path(c, [(s * 0.092, 0.30, zb + 0.065), (s * 0.086, 0.30, zb + 0.012)], 0.0215)
            pts = [top + np.array((0.0, 0.0, -0.03)), top, (s * 0.105, -0.05, 1.525), (s * 0.110, 0.04, 1.545), (s * 0.100, 0.13, 1.50), mid, end]
            _rope(K, "shoulder_rope_%d" % s, "eb_rope", pts, r=0.006, off=0.0215, tuck=0.004)
            knots.append(gxf(g_sphere(1.0, 6, 4), T(*wrap_path(c, [(s * 0.086, 0.30, zb + 0.012)], 0.0235)[0]) @ S(0.0125, 0.0100, 0.0125)))
        kn = add("rope_knots", gjoin(*knots), "cloth", "eb_rope", sharp=180)
        w_transfer(kn, c.ref, smooth=0, only=["spine_02", "spine_03"])
    with K.slot("gloves"):
        K.sleeves("wraps", "eb_rag", x0=0.600, x1=0.740, off=L1, wrinkle=0.002, waxis="x", wfreq=95.0, chew=1)
        K.palm_wrap("palm_wraps", "eb_rag")
        K.sleeves("bracer", "eb_oldleather", x0=0.495, x1=0.665, off=0.0105, cls="leather", sides="l")
        _arm_rings(K, "bracer_strap", "leather", (0.532, 0.615), width=0.017, off=0.0118, sides="l")
    with K.slot("pants"):
        K.pants("breeches", "eb_breeches", top=1.02, bottom=0.265, wrinkle=0.003, chew=2)
        m = (B.w(*BONES_LEG) > 0.5) & (B.z > 0.465) & (B.z < 0.605) & (B.x > 0)
        K.shell("knee_rag", m, "eb_rag", "cloth", off=L2 + 0.0045, smooth=2, chew=1, wrinkle=0.002, wfreq=70.0)
    with K.slot("boots"):
        bt = K.boots("foot_rags", "eb_footrag", top=0.245, off=L3, cls="cloth", toe_h=0.044, wrinkle=0.003, wfreq=60.0, chew=1)
        _soles(K, bt, "sole", "eb_oldleather", "leather")
        K.limb_straps("rag_ties", "eb_oldleather", "calf", (0.135, 0.205), width=0.013, off=L3 + 0.003)


# ---------------------------------------------------------------------------------------------- t2 line soldier
def t2(K):
    _mats()
    c, B = K.c, K.B
    folds = ((6, 0.030, 0.15), (11, 0.012, 0.6))
    with K.slot("helm"):
        K.coif("coif", "mail", off=0.007, z_lo=1.50)
        _conical(K, "helm", "eb_plain", "iron")
    with K.slot("chest"):
        K.tunic("hauberk", "mail", lo=1.0, hi=1.50, sleeve=0.50, off=0.008, cls="metal", smooth=2)
        K.skirt("mail_skirt", "mail", z_top=1.05, z_bot=0.735, gap_top=0.016, gap_bot=0.026, cls="metal", thick=0.004, nv=7, calf=0.0, folds=folds)
        K.shell("surcoat", K.torso_mask(1.03, 1.515, 0.205, vneck=0.065, vwidth=0.06), "eb_surcoat", off=0.0195, smooth=3,
                wrinkle=0.002, wfreq=20.0)
        K.skirt("surcoat_skirt", "eb_surcoat", z_top=1.058, z_bot=0.60, gap_top=0.027, gap_bot=0.046, nv=11, folds=folds, calf=0.4,
                u0=0.75 + 0.022, u1=1.75 - 0.022, close=False)
        K.belt("belt", "leather", z=1.075, width=0.050, gap=0.039, buckle="iron")
        K.pauldron("spaulder", "eb_plain", size=_psize(K, 0.110), tilt=16.0, th_out=90.0)
    with K.slot("gloves"):
        K.gloves("gloves", "leather", cuff=0.62, off=0.0055)
        K.sleeves("gauntlet_sleeve", "leather", x0=0.488, x1=0.70, off=0.0105, cls="leather")
        _cuff(K, "cuff", "eb_plain", x=0.585, length=0.078, flare=0.008, clear=0.0125)
    with K.slot("pants"):
        K.pants("hose", "eb_hose", top=1.02, bottom=0.12, wrinkle=0.003)
        K.kneecop("kneecop", "eb_plain")
        K.limb_straps("knee_straps", "leather", "thigh", (0.615,), width=0.018, off=L2 + 0.003)
    with K.slot("boots"):
        bt = K.boots("boots", "leather", top=0.43, off=L3, cuff=0.004, toe_h=0.048)
        _soles(K, bt, "sole", "leather", "leather")
        K.limb_straps("splint_straps", "leather", "calf", (0.195, 0.375), width=0.018, off=L3 + 0.0015, thick=0.004)
        _splints(K, "splint", "eb_plain")


# ---------------------------------------------------------------------------------------------- t3 warden-captain
def t3(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.decimate["helm"] = 0.9
        K.coif("coif", "mail", off=0.007, z_lo=1.505)
        parts, gh = K.helm("barbute", "steel", style="barbute", trim="bronze")
        bp = ring_of(gh, 30, 10, 0.0015)
        add("brow_band", g_tube(bp, 1.0, n=4, closed=True, sx=0.012, sy=0.0042, up=(0, 0, 1)), "metal", "bronze", bone="Head", sharp=50)
        fc = bp[0]                                                    # front centre of the band: a small ember stone in a bronze boss
        add("brow_boss", gxf(g_lathe([(0.0, 0.0065), (0.0105, 0.0055), (0.0150, 0.0)], n=6), frame((fc[0], fc[1] - 0.0035, fc[2]), (0, -1, 0.12))),
            "metal", "bronze", bone="Head", sharp=40)
        add("brow_gem", gxf(g_gem(0.0065, 0.012, 6, 0.5), frame((fc[0], fc[1] - 0.0085, fc[2]), (0, -1, 0.12))), "emis", "ember", bone="Head", sharp=30)
        _crest(K, "crest", "eb_crest", "bronze")
    with K.slot("chest"):
        K.decimate["chest"] = 0.88
        K.tunic("mail", "mail", lo=1.0, hi=1.50, sleeve=0.475, off=0.0075, cls="metal", smooth=2)
        cu = K.cuirass("cuirass", "steel", z0=1.19, z1=1.52, gap=0.026, ridge=0.009)
        K.band_plate("plackart", "steel", 1.055, 1.215, gap0=0.025, gap1=0.027, bone="spine_02", nu=22)
        K.band_plate("fauld1", "steel", 0.975, 1.06, gap0=0.040, gap1=0.027, bone="spine_01", nu=22)
        K.band_plate("fauld2", "steel", 0.895, 1.0, gap0=0.052, gap1=0.040, bone="pelvis", nu=22)
        gg = g_stations(K.stations([1.19, 1.215], 0.026), nu=28, nv=2, p=2.3)
        K.trim("cuirass_trim", "bronze", rim_of(gg, 28, 0), r=0.0065, bone="spine_03")
        gg = g_stations(K.stations([0.895, 0.92], [0.052, 0.050]), nu=28, nv=2, p=2.3)
        K.trim("fauld_trim", "bronze", rim_of(gg, 28, 0), r=0.006, bone="pelvis")
        K.pauldron("pauldron", "steel", size=_psize(K, 0.127), trim="bronze", tilt=20.0)
        _half_cape(K, "half_cape", "eb_cape")
        K.belt("belt", "leather", z=1.07, width=0.045, gap=0.035, buckle="bronze")
        _emboss("flame", FLAME, cu, 1.345, "bronze", s=0.62)
    with K.slot("gloves"):
        K.decimate["gloves"] = 0.9
        K.gloves("gauntlets", "steel", cuff=0.70, off=0.006, cls="metal", smooth=1)
        K.sleeves("mail_arm", "mail", x0=0.455, x1=0.725, off=0.0098, cls="metal")
        K.vambrace("vambrace", "steel", x0=0.495, x1=0.715, off=0.016)
        _cuff(K, "cuff", "steel", x=0.615, length=0.085, flare=0.020, clear=0.018, trim="bronze")
        _couter(K, "couter", "steel", wing="bronze")
    with K.slot("pants"):
        K.pants("mail_legs", "mail", top=1.02, bottom=0.12, off=0.0085, cls="metal", smooth=2)
        K.leg_plate("cuisse", "steel", bone="thigh", z0=0.61, z1=0.87, off=0.018)
        K.kneecop("poleyn", "steel", wing="bronze")
    with K.slot("boots"):
        bt = K.boots("sabatons", "steel", top=0.21, off=0.017, cls="metal", smooth=2, toe_h=0.050, toe_grow=0.012)
        _soles(K, bt, "sole", "steel", "metal")
        K.leg_plate("greave", "steel", bone="calf", z0=0.125, z1=0.455, off=0.019, front_only=False)
        K.limb_straps("greave_band", "bronze", "calf", (0.425,), width=0.015, off=0.0205, cls="metal", thick=0.0045)
