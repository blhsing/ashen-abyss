"""Frost / sand family helpers (frostbound, rime stalker, sand revenant): frosted and glacier look kinds, crystal
clusters, icicle fringes, bands fitted to a sculpted body, flat blade claws, helical wrap masks, rings.
Everything returns Parts in rest (T-pose) armature space unless stated otherwise."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_c as K
from monlib import V, tube, ellipsoid, join, revolve, blade, horn, band, torus


# ---------------------------------------------------------------------------------------------- look kinds
def frosted(base):
    """wrap an existing look kind with hoar frost: on up-facing surfaces, in large rime patches and on edges.
    extra params: frost (colour), frost_amt, rime (patch strength), rime_lo (patch threshold), frost_scale"""
    def k(N, p):
        r = L.KINDS[base](N, p)
        fc = p.get('frost', (0.80, 0.86, 0.90))
        amt = p.get('frost_amt', 0.8)
        up = N.math('MULTIPLY', N.upfacing(p.get('frost_lo', 0.1), 0.85), N.mapr(N.noise(13, 4, 0.7), 0.25, 0.6))
        lo = p.get('rime_lo', 0.52)
        patch = N.math('MULTIPLY', N.mapr(N.noise(p.get('frost_scale', 6.0), 4, 0.65), lo, lo + 0.14), p.get('rime', 0.6))
        edge = N.math('MULTIPLY', N.pointy(0.5, 0.6), p.get('frost_edge', 0.7))
        m = N.math('MULTIPLY', N.math('MAXIMUM', N.math('MAXIMUM', up, patch), edge), amt, clamp=True)
        grain = N.noise(120, 2, 0.6)
        fcol = N.mix(N.mapr(grain, 0.3, 0.75, 0.0, 0.4), fc, N.mul(fc, 0.72))
        out = dict(r)
        out['color'] = N.mix(m, r['color'], fcol)
        out['rough'] = N.mixf(m, r['rough'], 0.82)
        out['metal'] = N.mixf(m, r['metal'], 0.0)
        if r.get('height') is not None:
            out['height'] = N.math('ADD', r['height'], N.math('MULTIPLY', N.math('MULTIPLY', m, grain), 0.8))
        return out
    return k


for _k in ('skin', 'metal', 'cloth', 'leather', 'bone', 'wood'):
    L.KINDS['frost_' + _k] = frosted(_k)


def k_glacier(N, p):
    """hide like cracked glacier ice: pale plates, deep blue crevasses between them, white hairline fractures,
    hoar frost on top. ext_dark > 0 darkens the extremities (|x| large in the T-pose) towards `dark`."""
    col = p.get('color', (0.46, 0.59, 0.68))
    lite = p.get('light', (0.72, 0.82, 0.88))
    deep = p.get('deep', (0.02, 0.075, 0.16))
    sc = p.get('scale', 13.0)
    wv = N.warp(0.07, 5)
    cell = N.voro(sc, 'F1', vec=wv, out='Color')
    cv = N.new('ShaderNodeSeparateColor'); N.set(cv.inputs[0], cell)
    edge = N.voro(sc, 'DISTANCE_TO_EDGE', vec=wv)
    crack = N.mapr(edge, 0.0, p.get('crack_w', 0.05), 1.0, 0.0)
    hair = N.mapr(N.voro(sc * 2.7, 'DISTANCE_TO_EDGE', vec=N.warp(0.05, 9)), 0.0, 0.02, 1.0, 0.0)
    big = N.noise(p.get('blotch', 4.0), 4, 0.6)
    fine = N.noise(60, 3, 0.6)
    c = N.mix(N.mapr(cv.outputs[0], 0.0, 1.0, 0.0, 0.75), col, lite)
    c = N.mix(N.mapr(big, 0.38, 0.7, 0.0, p.get('blotch_amt', 0.6)), c, p.get('dark_ice', (0.13, 0.24, 0.34)))
    c = N.mix(N.mapr(fine, 0.3, 0.8, 0.0, 0.25), c, N.mul(c, 0.7))
    c = N.mix(N.math('MULTIPLY', hair, 0.4), c, lite)
    # plates go bluer and clearer towards their edges (thin ice), then drop into the crevasse
    halo = N.mapr(edge, 0.0, p.get('halo_w', 0.16), 1.0, 0.0)
    c = N.mix(N.math('MULTIPLY', halo, p.get('halo_amt', 0.6)), c, p.get('halo', (0.09, 0.28, 0.52)))
    c = N.mix(N.math('MULTIPLY', crack, 0.95), c, deep)
    ed = p.get('ext_dark', 0.0)
    if ed > 0:
        x, _, _ = N.sep(N.coord())
        m = N.mapr(N.math('ABSOLUTE', x), p.get('ext_x0', 0.45), p.get('ext_x1', 0.8))
        c = N.mix(N.math('MULTIPLY', m, ed), c, p.get('dark', (0.03, 0.07, 0.13)))
    up = N.math('MULTIPLY', N.upfacing(0.25, 0.9), N.mapr(N.noise(12, 4, 0.7), 0.3, 0.65))
    fr = N.math('MULTIPLY', up, p.get('frost_amt', 0.5))
    c = N.mix(fr, c, (0.82, 0.88, 0.92))
    r0 = p.get('rough', 0.3)
    rough = N.mixf(crack, N.mapr(cv.outputs[1], 0.0, 1.0, r0, r0 + 0.28), 0.9)
    rough = N.mixf(fr, rough, 0.8)
    h = N.math('ADD', N.math('MULTIPLY', N.mapr(edge, 0.0, 0.10), 0.9), N.math('MULTIPLY', fine, 0.2))
    return dict(color=c, rough=rough, metal=0.0, height=h, bump=(p.get('bump', 0.9), 0.008))


L.KINDS['glacier'] = k_glacier


def k_wraps(N, p):
    """sun-bleached bandage cloth: strong horizontal-ish banding (strip edges), frayed dark gaps, sand staining"""
    col = p.get('color', (0.52, 0.45, 0.33))
    dark = p.get('dark', (0.16, 0.12, 0.08))
    axis = p.get('axis', (0, 0, 1))
    freq = p.get('bands', 26.0)
    al = N.align(N.warp(p.get('wob', 0.035), 4.0), axis, 1.0)
    _, _, z = N.sep(al)
    saw = N.math('FRACT', N.math('MULTIPLY', z, freq))
    gap = N.mapr(saw, 0.0, p.get('gap_w', 0.10), 1.0, 0.0)
    shade = N.mapr(saw, 0.1, 1.0, 0.0, 1.0)
    big = N.noise(5, 4, 0.6)
    w1 = N.noise(150, 2, 0.5, vec=N.mapping(N.coord(), scale=(1, 1, 0.06)))
    w2 = N.noise(150, 2, 0.5, vec=N.mapping(N.coord(), scale=(0.06, 0.06, 1)))
    weave = N.math('ADD', N.math('MULTIPLY', w1, 0.5), N.math('MULTIPLY', w2, 0.5))
    c = N.mix(N.mapr(big, 0.3, 0.72, 0.0, 0.5), col, N.mul(col, 0.55))
    c = N.mix(N.mapr(shade, 0.0, 1.0, 0.0, p.get('band_shade', 0.35)), c, N.mul(c, 0.55))
    c = N.mix(N.mapr(weave, 0.35, 0.65, 0.0, 0.3), c, N.mul(c, 0.6))
    stain = N.mapr(N.noise(3.0, 3, 0.6), 0.5, 0.74)
    c = N.mix(N.math('MULTIPLY', stain, p.get('stain_amt', 0.55)), c, p.get('stain', (0.20, 0.13, 0.07)))
    c = N.mix(N.math('MULTIPLY', gap, 0.9), c, dark)
    edge = N.pointy(0.5, 0.62)
    c = N.mix(N.math('MULTIPLY', edge, 0.3), c, N.mix(0.5, col, (0.7, 0.66, 0.55)))
    h = N.math('ADD', N.math('MULTIPLY', weave, 0.3), N.math('MULTIPLY', N.mapr(saw, 0.0, 0.2), 0.8))
    return dict(color=c, rough=p.get('rough', 0.92), metal=0.0, height=h, bump=(p.get('bump', 0.8), 0.006))


L.KINDS['wraps'] = k_wraps


# ---------------------------------------------------------------------------------------------- fitting
def girth(part, z, dz=0.02, xlim=None):
    """(cx, cy, rx, ry) of the horizontal slice of a Part around height z (|x| < xlim keeps the arms out)"""
    v = part.v
    m = np.abs(v[:, 2] - z) < dz
    if xlim is not None:
        m &= np.abs(v[:, 0]) < xlim
    s = v[m]
    if len(s) < 4:
        return 0.0, 0.02, 0.16, 0.12
    mn, mx = s.min(0), s.max(0)
    return (mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, (mx[0] - mn[0]) / 2, (mx[1] - mn[1]) / 2


def fit_band(part, z, w, th, pad=0.004, tilt_x=0.0, tilt_y=0.0, n=22, xlim=None, dz=0.02):
    """belt / strap loop sized to the slice of `part` at height z"""
    cx, cy, rx, ry = girth(part, z, dz, xlim)
    return band((cx, cy, z), rx + pad + th * 0.5, ry + pad + th * 0.5, w, th, tilt_x=tilt_x, tilt_y=tilt_y, n=n)


def limb_frame(bone):
    """origin, unit axis (head -> tail), length of a bone"""
    a = M.RIG.H(bone); b = M.RIG.T(bone)
    d = b - a
    ln = float(np.linalg.norm(d))
    return a, d / ln, ln


def limb_radius(body, bone, t, dt=0.08, thresh=0.3):
    """mean distance from the bone axis of the body vertices near fraction t of the bone"""
    a, d, ln = limb_frame(bone)
    w = body.W[:, M.RIG.idx[bone]]
    rel = body.v - a
    s = rel @ d / ln
    m = (np.abs(s - t) < dt) & (w > thresh)
    if m.sum() < 3:
        return 0.04
    rad = np.linalg.norm(rel[m] - np.outer(rel[m] @ d, d), axis=1)
    return float(np.percentile(rad, 80))


# ---------------------------------------------------------------------------------------------- ice
def crystal(base, dirv, h, r, n=5, tip=0.55, sink=0.2, spin=0.0):
    """faceted ice crystal growing from `base` along dirv (its foot is sunk by sink*h below the base point)"""
    c = K.crystal(r, h * (1 + sink), n=n, tip=tip)
    if spin:
        c.rot('z', spin)
    c.move(0, 0, -h * sink)
    c.tf(M.rot_to((0, 0, 1), M.unit(dirv)))
    c.move(np.asarray(base, dtype=np.float64))
    c.smooth = 0.0
    return c


def cluster(c, axis=(0, 0, 1), count=5, spread=38.0, h=(0.08, 0.2), r=(0.02, 0.035), radius=0.04, seed=0, n=5, tip=0.5,
            big=1.0):
    """list of crystals fanning out of a point: the first one is the leader (longest, along the axis)"""
    rng = np.random.default_rng(seed)
    ax = M.unit(axis)
    fr = M.frame_from(ax)
    X = fr[:, 0]; Z = fr[:, 2]
    out = []
    for i in range(count):
        if i == 0:
            d = ax; hh = h[1] * big; rr = r[1]; off = np.zeros(3)
        else:
            a = rng.uniform(0, 2 * math.pi)
            t = math.radians(spread) * rng.uniform(0.45, 1.0)
            side = math.cos(a) * X + math.sin(a) * Z
            d = M.unit(ax * math.cos(t) + side * math.sin(t))
            hh = rng.uniform(h[0], h[1] * 0.8)
            rr = r[0] + (r[1] - r[0]) * (hh - h[0]) / max(h[1] - h[0], 1e-6)
            off = side * radius * rng.uniform(0.4, 1.0)
        out.append(crystal(np.asarray(c, dtype=np.float64) + off, d, hh, rr, n=n, tip=tip * rng.uniform(0.75, 1.15),
                           spin=rng.uniform(0, 72)))
    return out


def fringe(pts, dirv, lengths, r=0.011, seed=0, n=4, jitter=10.0):
    """icicles hanging from points along dirv; lengths: scalar (randomised) or one per point. Returns list of Parts."""
    rng = np.random.default_rng(seed)
    d0 = M.unit(dirv)
    out = []
    pts = np.asarray(pts, dtype=np.float64)
    Ls = [lengths[i] if hasattr(lengths, '__len__') else lengths * rng.uniform(0.4, 1.0) for i in range(len(pts))]
    mx = max(Ls)
    for i, p in enumerate(pts):
        d = M.unit(d0 + rng.normal(0, math.tan(math.radians(jitter)) * 0.6, 3))
        rr = r * (0.55 + 0.45 * Ls[i] / mx) * rng.uniform(0.85, 1.15)
        ic = K.icicle(p, Ls[i], rr, d, n=n, seed=seed * 31 + i)
        out.append(ic)
    return out


def line_pts(a, b, n, jit=0.0, seed=0):
    rng = np.random.default_rng(seed)
    a = np.asarray(a, dtype=np.float64); b = np.asarray(b, dtype=np.float64)
    t = np.linspace(0, 1, n)[:, None]
    P = a * (1 - t) + b * t
    if jit:
        P = P + rng.uniform(-jit, jit, P.shape)
    return P


# ---------------------------------------------------------------------------------------------- claws / blades
def talon(length, width, thick=0.008, curve=0.35, n=7, hook=1.6):
    """flat curved blade claw in a local frame: grows along +Y from the origin, curls towards -Z (the palm side),
    blade plane = YZ (thin along X).  Faceted (ice) or smooth (horn) depending on the smooth angle set by the caller."""
    t = np.linspace(0, 1, n)
    y = length * t
    z = -length * curve * t ** hook
    w = width * (1 - t) ** 0.75 * (0.75 + 0.25 * np.sin(np.clip(t * 3.2, 0, math.pi / 2))) + 0.0015
    th = thick * (1 - 0.8 * t) + 0.0008
    rings = []
    for i in range(n):
        c = np.array([0.0, y[i], z[i]])
        # tangent in the YZ plane
        j0 = max(i - 1, 0); j1 = min(i + 1, n - 1)
        tg = M.unit(np.array([0.0, y[j1] - y[j0], z[j1] - z[j0]]))
        up = np.array([0.0, -tg[2], tg[1]])            # in-plane normal (towards the back of the blade)
        rings.append([c + up * w[i] * 0.45, c + np.array([th[i] / 2, 0, 0]), c - up * w[i] * 0.55, c - np.array([th[i] / 2, 0, 0])])
    p = M.loft(np.array(rings), cap0=True, cap1=True).recalc()
    p.smooth = 20.0
    return p


def place_talon(tal, origin, direction, palm=(0, 0, -1)):
    """move a talon() so that it grows along `direction` and curls towards `palm`"""
    d = M.unit(direction)
    z = -np.asarray(palm, dtype=np.float64)
    z = M.unit(z - d * np.dot(z, d))
    x = np.cross(d, z)
    Mx = np.stack([x, d, z], axis=1)
    tal.v = tal.v @ Mx.T + np.asarray(origin, dtype=np.float64)
    if np.linalg.det(Mx) < 0:
        tal.flip()
    return tal


# ---------------------------------------------------------------------------------------------- wraps / rings
def helix_mask(body, bone, turns, duty=0.6, phase=0.0, t0=-0.2, t1=1.2, jitter=0.0, seed=0.0):
    """per-vertex mask of a bandage wound round a limb bone: True where frac(turns*s + angle/2pi + phase) < duty"""
    a, d, ln = limb_frame(bone)
    fr = M.frame_from(d)
    X = fr[:, 0]; Z = fr[:, 2]
    rel = body.v - a
    s = rel @ d / ln
    ang = np.arctan2(rel @ Z, rel @ X) / (2 * math.pi)
    f = turns * s + ang + phase
    if jitter:
        f = f + jitter * np.array([M.fbm(p, 9.0, seed, 2) for p in body.v])
    f = f - np.floor(f)
    return (f < duty) & (s > t0) & (s < t1)


def limb_ring(bone, t, r, thick=0.012, n=12, m=5, off=(0, 0, 0), squash=1.0):
    """torus round a limb bone at fraction t (rigid on that bone)"""
    a, d, ln = limb_frame(bone)
    tr = torus(r, thick, n=n, m=m, ry=r * squash)
    tr.tf(M.rot_to((0, 0, 1), d))
    tr.move(a + d * ln * t + np.asarray(off, dtype=np.float64))
    tr.bone = bone
    tr.smooth = 60.0
    return tr


def k_crystal(N, p):
    """clear blue ice for crystals / icicles: deep body colour, pale fracture veins and milky bands, hoar frost only
    on near-horizontal tops (the stock 'ice' kind turns sharp low-poly crystals almost white)"""
    col = p.get('color', (0.24, 0.46, 0.70))
    deep = p.get('deep', (0.02, 0.10, 0.28))
    frost = p.get('frost', (0.72, 0.86, 1.0))
    big = N.noise(p.get('scale', 9.0), 4, 0.6)
    c = N.mix(N.mapr(big, 0.3, 0.7), deep, col)
    cr = N.voro(p.get('crack', 18.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.08, 6))
    vein = N.mapr(cr, 0.0, 0.025, 1.0, 0.0)
    c = N.mix(N.math('MULTIPLY', vein, p.get('vein_amt', 0.45)), c, frost)
    milk = N.mapr(N.noise(22, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.25))), 0.55, 0.8)
    c = N.mix(N.math('MULTIPLY', milk, 0.35), c, N.mix(0.5, col, frost))
    up = N.math('MULTIPLY', N.upfacing(p.get('frost_lo', 0.45), 0.95), N.mapr(N.noise(14, 4, 0.7), 0.3, 0.65))
    fr = N.math('MULTIPLY', up, p.get('frost_amt', 0.6))
    c = N.mix(fr, c, frost)
    edge = N.math('MULTIPLY', N.pointy(0.52, 0.64), p.get('edge', 0.3))
    c = N.mix(edge, c, frost)
    r0 = p.get('rough', 0.14)
    rough = N.mixf(N.math('MAXIMUM', fr, N.math('MULTIPLY', vein, 0.5)), N.mapr(big, 0.3, 0.7, r0, r0 + 0.18), 0.7)
    h = N.math('ADD', N.math('MULTIPLY', N.mapr(cr, 0.0, 0.06), 0.5), N.math('MULTIPLY', N.noise(45, 3, 0.6), 0.3))
    return dict(color=c, rough=rough, metal=0.0, height=h, bump=(p.get('bump', 0.25), 0.003))


L.KINDS['crystal'] = k_crystal
