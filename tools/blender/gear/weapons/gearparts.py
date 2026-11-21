"""Shape generators of the gear models (weapon-local space: +Z up the weapon, +X edge / knuckles, +Y palm normal)."""
from gearlib import *
import gearlib
import gearmats


class W(gearlib.W):
    """Builder of one variant: w(geo, "steel", sharp=30) adds a part (the palette entry is created on first use)."""

    def __call__(self, g, src, M=None, **kw):
        gearmats.use(src)
        return gearlib.W.__call__(self, g, src, M, **kw)


# ------------------------------------------------------------------------------------------------ small helpers
def lerp(a, b, t):
    return a + (b - a) * t


def with_luv(g, v_scale=1.0):
    """'luv' for wood grain on parts turned round the Z axis: u = angle fraction, v = metres along Z"""
    v = g["v"]
    out = dict(g)
    out["luv"] = np.stack([np.arctan2(v[:, 1], v[:, 0]) / TAU % 1.0, v[:, 2] * v_scale], axis=1)
    return out


def lathe_w(profile, n=10, **kw):
    return with_luv(g_lathe(profile, n=n, **kw))


def haft(z0, z1, r0, r1=None, n=8, seg=8, wob=0.0, seed=1, oval=1.0, bulge=None):
    """Wooden shaft along Z with grain ('luv'). wob = hand-cut crookedness (m); oval squashes it along Y;
    bulge = [(t, scale)] radius multipliers along the length (swells, waists)."""
    r1 = r0 if r1 is None else r1
    rng = np.random.RandomState(seed)
    zs = np.linspace(z0, z1, seg + 1)
    pts = [(wob * (rng.rand() - 0.5), wob * (rng.rand() - 0.5), z) for z in zs]
    pts[0] = (0.0, 0.0, z0)
    pts[-1] = (0.0, 0.0, z1)
    rad = np.linspace(r0, r1, seg + 1)
    if bulge:
        ts = np.linspace(0, 1, seg + 1)
        rad = rad * np.interp(ts, [b[0] for b in bulge], [b[1] for b in bulge])
    return tube_w(pts, list(rad), n, sx=1.0, sy=oval, up=(1, 0, 0))


def cyl(r, z0, z1, n=10, r1=None):
    return g_cyl(r, z0, z1, n, r1)


def torus(r, wire, n=16, m=6, sx=1.0, sy=1.0):
    """torus in the XY plane (axis Z)"""
    return g_ring(r, wire, n, m, sx, sy)


def helix(r, z0, z1, turns, width, thick, n=4, spt=12, r1=None, phase=0.0, hand=1, lift=0.35):
    """Band wound round the Z axis (grip wraps, cord bindings): `width` along the cylinder, `thick` radial."""
    r1 = r if r1 is None else r1
    m = max(4, int(round(abs(turns) * spt))) + 1
    verts = []
    for i in range(m):
        t = i / (m - 1)
        a = phase + hand * TAU * turns * t
        rr = lerp(r, r1, t) + thick * lift
        rad = np.array((math.cos(a), math.sin(a), 0.0))
        c = rad * rr + np.array((0.0, 0.0, lerp(z0, z1, t)))
        tan = np.array((-math.sin(a) * hand * TAU * turns * rr, math.cos(a) * hand * TAU * turns * rr, (z1 - z0)))
        tan /= max(np.linalg.norm(tan), 1e-9)
        bn = np.cross(tan, rad)
        for k in range(n):
            b = TAU * (k + 0.5) / n
            verts.append(c + rad * (thick / 2 * math.cos(b)) * (1.4142 if n == 4 else 1.0) + bn * (width / 2 * math.sin(b)) * (1.4142 if n == 4 else 1.0))
    faces = []
    for i in range(m - 1):
        for k in range(n):
            k2 = (k + 1) % n
            faces.append((i * n + k, i * n + k2, (i + 1) * n + k2, (i + 1) * n + k))
    faces.append(tuple(range(n)))
    faces.append(tuple(reversed(range((m - 1) * n, m * n))))
    return geo(np.array(verts), faces)


def rings(zs, r, h=0.006, n=10, bulge=1.25, r_fn=None):
    """stack of raised bands round the Z axis (ridged grips, ferrules)"""
    return gjoin(*[g_lathe([(rr, z - h / 2), (rr * bulge, z), (rr, z + h / 2)], n=n)
                   for z in zs for rr in [r_fn(z) if r_fn else r]])


def spike(r, h, n=6, z0=0.0, r_tip=0.0):
    if r_tip > 0:
        return g_lathe([(0.0, z0), (r, z0), (r_tip, z0 + h), (0.0, z0 + h)], n=n)
    return g_lathe([(0.0, z0), (r, z0), (0.0, z0 + h)], n=n)


def stud(r=0.006, h=None, n=6):
    """rivet head: dome on the XY plane pointing +Z"""
    h = r * 0.7 if h is None else h
    return g_lathe([(r, 0.0), (r * 0.72, h * 0.62), (0.0, h)], n=n)


def studs(pts, dirs, r=0.006, n=6):
    """rivet heads at points, each pointing along its direction"""
    out = []
    for p, d in zip(pts, dirs):
        out.append(gxf(stud(r, n=n), frame(p, d)))
    return gjoin(*out)


def radial(g, count, phase=0.0):
    """copies of g turned round the Z axis"""
    return gjoin(*[gxf(g, R('Z', phase + 360.0 * i / count)) for i in range(count)])


def crystal(r=0.02, h=0.08, n=6, top=0.34, bot=0.22, lean=0.0, sx=1.0, sy=1.0, girdle=0.5):
    """Prismatic crystal along +Z from the origin (base at z=0): short pointed foot, faceted column, pointed top."""
    zf = h * bot
    zt = h * (1 - top)
    prof = [(0.0, 0.0), (r * 0.86, zf), (r, lerp(zf, zt, girdle)), (r * 0.9, zt), (0.0, h)]
    g = g_lathe(prof, n=n, sx=sx, sy=sy)
    if lean:
        v = g["v"].copy()
        v[:, 0] += v[:, 2] * lean
        g = dict(g)
        g["v"] = v
    return g


def shard(r=0.02, h=0.08, n=5, seed=1, skew=0.25):
    """irregular raw crystal shard (uneven facets), base at the origin"""
    rng = np.random.RandomState(seed)
    g = crystal(r, h, n, top=0.25 + 0.2 * rng.rand(), bot=0.12 + 0.12 * rng.rand(), lean=(rng.rand() - 0.5) * skew,
                sx=0.85 + 0.3 * rng.rand(), sy=0.8 + 0.3 * rng.rand())
    return gxf(g, R('Z', rng.rand() * 360))


def plate(outline, thick):
    """flat piece cut from sheet: outline [(x, z)] in the XZ plane, thickness along Y (add with bevel=)"""
    return g_prism(outline, thick)


def arc2(cx, cz, r, a0, a1, n=12):
    """points [(x, z)] on a circle arc in the XZ plane, angles in degrees (0 = +X, 90 = +Z)"""
    return [(cx + r * math.cos(math.radians(a)), cz + r * math.sin(math.radians(a))) for a in np.linspace(a0, a1, n)]


def xz3(pts, y=0.0):
    return [(p[0], y, p[1]) for p in pts]


def smooth_path(P, n=24):
    """resample a polyline (2D or 3D control points) to n points with a Catmull-Rom spline"""
    P = np.asarray(P, float)
    t = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])
    ts = np.linspace(0, t[-1], n)
    return np.stack([spl(ts, list(zip(t, P[:, k]))) for k in range(P.shape[1])], axis=1)


# ------------------------------------------------------------------------------------------------ blade sections
# sec(hw, t, s) -> [(x_rel, y), ...] outline of the section (same vertex count for every station), x_rel from the mid
# line; s = the station's position along the blade (fuller depth / ridge height may be functions of s).
def _val(v, s):
    return v(s) if callable(v) else v


def sec_diamond(ridge=1.0):
    return lambda hw, t, s=0.0: [(hw, 0.0), (0.0, t / 2 * _val(ridge, s)), (-hw, 0.0), (0.0, -t / 2 * _val(ridge, s))]


def sec_hex(bevel=0.008):
    """two flats with bevelled cutting edges on both sides"""
    def f(hw, t, s=0.0):
        b = min(bevel, hw * 0.8)
        return [(hw, 0.0), (hw - b, t / 2), (-(hw - b), t / 2), (-hw, 0.0), (-(hw - b), -t / 2), (hw - b, -t / 2)]
    return f


def sec_fuller(bevel=0.008, fw=0.45, fd=0.5):
    """double-edged with a central fuller (groove) on both flats"""
    def f(hw, t, s=0.0):
        b = min(bevel, hw * 0.6)
        fx = fw * (hw - b)
        d = t / 2 * (1 - _val(fd, s))
        top = [(hw - b, t / 2), (fx, t / 2), (0.0, d), (-fx, t / 2), (-(hw - b), t / 2)]
        return [(hw, 0.0)] + top + [(-hw, 0.0)] + [(x, -y) for x, y in reversed(top)]
    return f


def sec_ridge(bevel=0.008, ridge=1.5):
    """bevelled edges and a raised mid rib (stiff thrusting blades)"""
    def f(hw, t, s=0.0):
        b = min(bevel, hw * 0.6)
        top = [(hw - b, t / 2), (0.0, t / 2 * _val(ridge, s)), (-(hw - b), t / 2)]
        return [(hw, 0.0)] + top + [(-hw, 0.0)] + [(x, -y) for x, y in reversed(top)]
    return f


def sec_single(bevel=0.012, spine=1.0):
    """single-edged: cutting edge on the +x side, flat spine on the -x side"""
    def f(hw, t, s=0.0):
        b = min(bevel, hw * 1.5)
        return [(hw, 0.0), (hw - b, t / 2), (-hw, t / 2 * spine), (-hw, -t / 2 * spine), (hw - b, -t / 2)]
    return f


def sec_single_fuller(bevel=0.012, spine=1.0, fd=0.5):
    """single-edged with a groove under the spine"""
    def f(hw, t, s=0.0):
        b = min(bevel, hw * 1.2)
        x0, x1 = -hw, hw - b
        a, m, c = lerp(x0, x1, 0.14), lerp(x0, x1, 0.36), lerp(x0, x1, 0.58)
        top = [(x1, t / 2), (c, t / 2), (m, t / 2 * (1 - _val(fd, s))), (a, t / 2), (x0, t / 2 * spine)]
        return [(hw, 0.0)] + top + [(x, -y) for x, y in reversed(top)]
    return f


def sec_wedge(bevel=0.014, shoulder=0.35):
    """axe / cleaver: thick at the -x side (eye), thinning to the shoulder of the edge bevel, then to the edge"""
    def f(hw, t, s=0.0):
        b = min(bevel, hw)
        return [(hw, 0.0), (hw - b, t / 2 * shoulder), (-hw, t / 2), (-hw, -t / 2), (hw - b, -t / 2 * shoulder)]
    return f


def sec_lens(n=8):
    def f(hw, t, s=0.0):
        return [(hw * math.cos(TAU * k / n), t / 2 * math.sin(TAU * k / n)) for k in range(n)]
    return f


def ribbon(st, sec, path=None, cap0=True, cap1=True):
    """Blade lofted through stations.
    st = [(s, a, b, t), ...]: s = distance along the blade (along +Z, or along `path`), a < b = signed offsets of the
    two borders across the blade (the +b side is the cutting edge of single-edged sections), t = thickness.
    A station with b - a < 1e-5 collapses to a point (tip / tang).
    sec: section factory result. path: None (straight, width along +X) or a polyline [(x, z), ...]: the blade follows it
    (s = arc length), its width lies across it in the XZ plane (+b to the right of the direction of travel... i.e. towards
    +X when the path runs up +Z)."""
    if path is not None:
        P = np.asarray(path, float)
        L = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])

        def at(s):
            s = min(max(s, 0.0), L[-1])
            x, z = np.interp(s, L, P[:, 0]), np.interp(s, L, P[:, 1])
            e = L[-1] * 0.01
            s0, s1 = max(s - e, 0.0), min(s + e, L[-1])
            tx = np.interp(s1, L, P[:, 0]) - np.interp(s0, L, P[:, 0])
            tz = np.interp(s1, L, P[:, 1]) - np.interp(s0, L, P[:, 1])
            k = math.hypot(tx, tz) or 1.0
            return np.array((x, 0.0, z)), np.array((tz / k, 0.0, -tx / k))
    else:
        def at(s):
            return np.array((0.0, 0.0, s)), np.array((1.0, 0.0, 0.0))
    yv = np.array((0.0, 1.0, 0.0))
    verts, rows = [], []
    for (s, a, b, t) in st:
        p, nrm = at(s)
        mid, hw = (a + b) / 2, (b - a) / 2
        if hw < 1e-5:
            rows.append([len(verts)])
            verts.append(p + nrm * mid)
        else:
            ring = sec(hw, t, s)
            rows.append(list(range(len(verts), len(verts) + len(ring))))
            verts += [p + nrm * (mid + x) + yv * y for x, y in ring]
    faces = []
    for j in range(len(rows) - 1):
        ra, rb = rows[j], rows[j + 1]
        m = max(len(ra), len(rb))
        for i in range(m):
            i2 = (i + 1) % m
            if len(ra) > 1 and len(rb) > 1:
                faces.append((ra[i], ra[i2], rb[i2], rb[i]))
            elif len(ra) > 1:
                faces.append((ra[i], ra[i2], rb[0]))
            elif len(rb) > 1:
                faces.append((ra[0], rb[i2], rb[i]))
    if cap0 and len(rows[0]) > 1:
        faces.append(tuple(reversed(rows[0])))
    if cap1 and len(rows[-1]) > 1:
        faces.append(tuple(rows[-1]))
    return geo(np.array(verts), faces)


def sym(z, hw, t):
    """symmetric station for ribbon()"""
    return (z, -hw, hw, t)


def dense(st, n=24, cols=(1, 2, 3)):
    """resample ribbon stations to n rows with smooth (spline) borders; a zero-width last station is kept as the tip"""
    st = [tuple(q) for q in st]
    tip = st[-1] if (st[-1][2] - st[-1][1]) < 1e-5 else None
    s0, s1 = st[0][0], st[-1][0]
    ss = np.linspace(s0, s1, n)
    out = []
    for s in (ss[:-1] if tip else ss):
        row = [float(s)]
        for c in (1, 2, 3):
            pts = [(q[0], q[c]) for q in st]
            row.append(float(spl(s, pts)) if c in cols else float(np.interp(s, [p[0] for p in pts], [p[1] for p in pts])))
        out.append(tuple(row))
    if tip:
        out.append(tip)
    return out


# ------------------------------------------------------------------------------------------------ hilts
def grip_core(z0, z1, r, n=10, swell=1.08, taper=0.94, oval=0.86):
    """handle core (slightly oval, widest in the middle); 'luv' for wood"""
    zs = np.linspace(z0, z1, 7)
    ts = np.linspace(0, 1, 7)
    rad = r * (taper + (swell - taper) * np.sin(ts * PI))
    return tube_w([(0.0, 0.0, z) for z in zs], list(rad), n, sx=1.0, sy=oval, up=(1, 0, 0))


def grip_wrap(z0, z1, r, turns=None, width=0.0075, thick=0.0026, oval=0.86, hand=1, n=4, gap=0.9):
    """leather / cord strip wound round a grip (scaled to the oval of grip_core)"""
    if turns is None:
        turns = (z1 - z0) / (width * (1 + gap) * 0.62)
    g = helix(r, z0, z1, turns, width, thick, n=n, spt=10, hand=hand)
    v = g["v"].copy()
    v[:, 1] *= oval
    g = dict(g)
    g["v"] = v
    return g


def wheel_pommel(r, t, z, n=14, hub=0.45):
    """disc pommel facing +-Y with a raised hub"""
    prof = [(0.0, -t * 0.62), (r * hub, -t * 0.62), (r * hub * 1.25, -t / 2), (r * 0.92, -t / 2), (r, -t * 0.2), (r, t * 0.2),
            (r * 0.92, t / 2), (r * hub * 1.25, t / 2), (r * hub, t * 0.62), (0.0, t * 0.62)]
    return gxf(g_lathe(prof, n=n), T(0, 0, z) @ R('X', 90))


def quillon(pts, rad, n=8, flat=0.8):
    """curved guard arm through 3D points (x, y, z)"""
    P = smooth_path(pts, max(len(pts) * 3, 9))
    r = np.interp(np.linspace(0, 1, len(P)), np.linspace(0, 1, len(rad)), rad)
    return g_tube(P, list(r), n=n, sy=flat, up=(0, 1, 0))


def mirror_x_g(g):
    return gxf(g, S(-1, 1, 1))


def mirror_y_g(g):
    return gxf(g, S(1, -1, 1))


def both_x(g):
    return gjoin(g, mirror_x_g(g))


def both_y(g):
    return gjoin(g, mirror_y_g(g))


def inlay(st, lift=0.0006, wide=1.0):
    """thin glowing strip lying in a fuller / on the flat of a blade, both faces:
    st = [(z, half width, y of the surface[, x of the centre line])]"""
    out = []
    for sgn in (1, -1):
        v, f = [], []
        for i, q in enumerate(st):
            z, hw, y = q[0], q[1], q[2]
            xc = q[3] if len(q) > 3 else 0.0
            yy = sgn * (y + lift)
            v += [(xc - hw * wide, yy, z), (xc + hw * wide, yy, z), (xc + hw * wide * 0.6, yy - sgn * 0.004, z), (xc - hw * wide * 0.6, yy - sgn * 0.004, z)]
        for i in range(len(st) - 1):
            for k in range(4):
                k2 = (k + 1) % 4
                f.append((i * 4 + k, i * 4 + k2, (i + 1) * 4 + k2, (i + 1) * 4 + k))
        f.append((0, 1, 2, 3))
        f.append(((len(st) - 1) * 4 + 3, (len(st) - 1) * 4 + 2, (len(st) - 1) * 4 + 1, (len(st) - 1) * 4))
        out.append(geo(np.array(v), f))
    return gjoin(*out)


# ------------------------------------------------------------------------------------------------ heads
def plen(path):
    P = np.asarray(path, float)
    return float(np.linalg.norm(np.diff(P, axis=0), axis=1).sum())


def along(path, fr):
    """ribbon stations given by fraction of the path length: fr = [(f, a, b, t), ...] -> [(s, a, b, t), ...]"""
    L = plen(path)
    return [(f * L, a, b, t) for (f, a, b, t) in fr]


def slices(edge, back, thick, n=22, smooth_back=False):
    """ribbon stations of an axe-like head from horizontal slices: edge / back / thick = [(z, value), ...] control
    curves (edge = the cutting border, +x side; back = the border towards the haft). The first and last z collapse to
    points where edge == back."""
    z0, z1 = edge[0][0], edge[-1][0]
    zs = sorted(set([round(float(z), 5) for z in np.linspace(z0, z1, n)] + [round(p[0], 5) for p in back] + [round(p[0], 5) for p in thick]))
    out = []
    for z in zs:
        b = float(spl(z, edge))
        a = float(spl(z, back)) if smooth_back else float(np.interp(z, [p[0] for p in back], [p[1] for p in back]))
        t = float(np.interp(z, [p[0] for p in thick], [p[1] for p in thick]))
        if b - a < 1e-4:
            a = b = (a + b) / 2
        out.append((z, a, b, t))
    return out


def fib_dirs(n, seed=0.0):
    """n roughly even directions on the unit sphere"""
    out = []
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(max(0.0, 1 - z * z))
        a = i * 2.39996323 + seed
        out.append(np.array((r * math.cos(a), r * math.sin(a), z)))
    return out


def spiked_ball(r, n_sp=14, sp_r=0.009, sp_h=0.028, center=(0, 0, 0), nu=12, nv=8, n=5, seed=0.0, skip=None):
    """(ball geometry, spikes geometry)"""
    c = np.array(center, float)
    ball = gxf(g_sphere(r, nu, nv), T(*c))
    sp = []
    for d in fib_dirs(n_sp, seed):
        if skip is not None and skip(d):
            continue
        sp.append(gxf(spike(sp_r, sp_h, n, z0=r * 0.86), frame(c, d)))
    return ball, gjoin(*sp)


def tube(path, radius, n=6, **kw):
    return g_tube(path, radius, n=n, **kw)


def box(sx, sy, sz, c=(0, 0, 0)):
    return g_box(sx, sy, sz, c)


def langets(z0, z1, r, width=0.012, thick=0.0035, sides=((0, 1), (0, -1))):
    """iron straps down a haft (boxes lying on the haft surface)"""
    out = []
    for (dx, dy) in sides:
        if dy:
            out.append(g_box(width, thick, z1 - z0, (0, dy * (r + thick * 0.3), (z0 + z1) / 2)))
        else:
            out.append(g_box(thick, width, z1 - z0, (dx * (r + thick * 0.3), 0, (z0 + z1) / 2)))
    return gjoin(*out)


def rivet_row(zs, r_at, d=(0, 1, 0), size=0.0035):
    """rivet heads along a haft at heights zs on the side d (unit vector in XY)"""
    d = np.array(d, float)
    return studs([d * r_at + np.array((0, 0, z)) for z in zs], [d] * len(zs), size)


# ------------------------------------------------------------------------------------------------ crude bindings
def rope_lash(z0, z1, r, d=0.0048, oval=1.0, hand=1, turns=None):
    """rope wound tightly round a haft (round strand, six-sided section)"""
    turns = turns or (z1 - z0) / (d * 1.12)
    g = helix(r, z0, z1, turns, d, d * 0.92, n=5, spt=8, hand=hand)
    v = g["v"].copy()
    v[:, 1] *= oval
    g = dict(g)
    g["v"] = v
    return g


def rag_wrap(z0, z1, r, oval=1.0, width=0.0125, hand=1, turns=None):
    """wide strip of cloth wound round a grip"""
    turns = turns or (z1 - z0) / (width * 0.82)
    return grip_wrap(z0, z1, r, turns=turns, width=width, thick=0.0030, oval=oval, hand=hand, n=4)


def gscale(g, k, pivot=(0, 0, 0)):
    k = (k, k, k) if np.isscalar(k) else k
    return gxf(g, T(*pivot) @ S(*k) @ T(-pivot[0], -pivot[1], -pivot[2]))


def boulder(rx, ry, rz, amp=0.10, freq=30.0, seed=1, nu=12, nv=8, boxy=0.55):
    """rough stone block: a rounded cube (sphere blended towards its bounding cube) with knobbly noise"""
    g = g_sphere(1.0, nu, nv)
    p = g["v"]
    n = p / np.maximum(np.linalg.norm(p, axis=1, keepdims=True), 1e-9)
    c = n / np.maximum(np.abs(n).max(1, keepdims=True), 1e-9)
    g = dict(g)
    g["v"] = (n * (1 - boxy) + c * boxy) * np.array((rx, ry, rz))
    return lumpy(g, amp, freq, seed, (0, 0, 0))


def animal_skull(s=1.0):
    """small beast skull in profile: cranium at the origin, snout towards +X, jawless. Returns
    [(geo, kind)] with kind 'bone' or 'dark' (sockets / nose)."""
    out = []
    out.append((gxf(g_sphere(1.0, 12, 8), S(0.036 * s, 0.029 * s, 0.030 * s)), 'bone'))
    snout = g_tube([(0.016 * s, 0, -0.004 * s), (0.050 * s, 0, -0.012 * s), (0.082 * s, 0, -0.020 * s), (0.094 * s, 0, -0.024 * s)],
                   [0.0235 * s, 0.0175 * s, 0.0125 * s, 0.0085 * s], n=8, sy=0.82, up=(0, 1, 0))
    out.append((snout, 'bone'))
    brow = gxf(g_sphere(1.0, 8, 6), T(0.022 * s, 0, 0.016 * s) @ S(0.020 * s, 0.030 * s, 0.010 * s))
    out.append((brow, 'bone'))
    for sy_ in (1, -1):
        eye = gxf(g_lathe([(0.0, 0.0030 * s), (0.0098 * s, 0.0030 * s), (0.0110 * s, 0.0)], n=8), T(0.020 * s, sy_ * 0.0235 * s, 0.002 * s) @ R('X', -90 * sy_) @ R('Y', 20 * sy_))
        out.append((eye, 'dark'))
        nose = gxf(g_lathe([(0.0, 0.0020 * s), (0.0040 * s, 0.0020 * s), (0.0046 * s, 0.0)], n=6, sy=1.6), T(0.078 * s, sy_ * 0.0092 * s, -0.016 * s) @ R('X', -90 * sy_) @ R('Y', 30 * sy_))
        out.append((nose, 'dark'))
    teeth = gjoin(*[gxf(g_cone(0.0030 * s, 0.0080 * s, 4), T((0.040 + 0.0085 * i) * s, sy_ * (0.0125 - 0.0009 * i) * s, (-0.026 - 0.0018 * i) * s) @ R('X', 180))
                    for i in range(6) for sy_ in (1, -1)])
    out.append((teeth, 'bone'))
    return out


def human_skull(s=1.0):
    """small jawless human skull facing +Y (charms, the skull gem): cranium at the origin.
    Returns [(geo, kind)] with kind 'bone', 'jaw', 'dark' (eye sockets) or 'nose'."""
    out = []
    out.append((gxf(g_sphere(1.0, 16, 12), T(0, -0.004 * s, 0.007 * s) @ S(0.0300 * s, 0.0360 * s, 0.0310 * s)), 'bone'))
    # face: cheeks narrowing to the upper jaw
    face = g_lathe([(0.0, -0.034 * s), (0.0150 * s, -0.034 * s), (0.0172 * s, -0.026 * s), (0.0215 * s, -0.014 * s), (0.0262 * s, -0.002 * s),
                    (0.0240 * s, 0.006 * s)], n=12, sy=0.86)
    out.append((gxf(face, T(0, 0.0065 * s, 0.0)), 'jaw'))
    out.append((gxf(g_sphere(1.0, 10, 6), T(0, 0.0255 * s, 0.0095 * s) @ S(0.0262 * s, 0.0085 * s, 0.0050 * s)), 'bone'))
    for sx_ in (1, -1):
        out.append((ydome([(0.0, 0.0024 * s), (0.0092 * s, 0.0024 * s), (0.0104 * s, 0.0)], n=10, x0=sx_ * 0.0122 * s, y0=0.0268 * s, z0=-0.0045 * s, sz=0.88), 'dark'))
        out.append((gxf(torus(0.0102 * s, 0.0020 * s, 10, 4, sy=0.88), T(sx_ * 0.0122 * s, 0.0272 * s, -0.0045 * s) @ R('X', 90)), 'bone'))
        out.append((gxf(g_sphere(1.0, 8, 5), T(sx_ * 0.0225 * s, 0.0160 * s, -0.0130 * s) @ S(0.0070 * s, 0.0110 * s, 0.0058 * s)), 'bone'))
    out.append((gxf(g_prism([(-0.0040 * s, -0.0075 * s), (0.0040 * s, -0.0075 * s), (0.0, 0.0030 * s)], 0.0040 * s), T(0, 0.0260 * s, -0.0155 * s)), 'nose'))
    teeth = gjoin(*[gxf(g_box(0.0038 * s, 0.0040 * s, 0.0085 * s), T((-0.0120 + 0.0048 * i) * s, (0.0205 - 0.0036 * abs(i - 2.5) ** 1.3 * 0.55) * s, -0.0350 * s))
                    for i in range(6)])
    out.append((teeth, 'bone'))
    return out


# ------------------------------------------------------------------------------------------------ glow helpers
def path_pts(path, fracs, off=0.0):
    """points [(x, z)] on a polyline path (XZ plane) at the given fractions of its length, shifted sideways by `off`
    (same side convention as ribbon(): positive = right of the direction of travel)"""
    P = np.asarray(path, float)
    L = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])
    out = []
    for f in fracs:
        s = f * L[-1]
        x, z = np.interp(s, L, P[:, 0]), np.interp(s, L, P[:, 1])
        e = L[-1] * 0.01
        s0, s1 = max(s - e, 0.0), min(s + e, L[-1])
        tx = np.interp(s1, L, P[:, 0]) - np.interp(s0, L, P[:, 0])
        tz = np.interp(s1, L, P[:, 1]) - np.interp(s0, L, P[:, 1])
        k = math.hypot(tx, tz) or 1.0
        o = off(f) if callable(off) else off
        out.append((float(x + tz / k * o), float(z - tx / k * o)))
    return out


def glow_line(pts, y, r=0.0022, n=4, taper=True, flat=0.55):
    """glowing vein lying on both faces of a flat part: pts = [(x, z), ...], y = half thickness of the part there
    (a number, or a function (x, z) -> half thickness, e.g. wedge_y(...))"""
    out = []
    m = len(pts)
    rad = [r * (0.45 + 0.55 * math.sin(PI * (i + 0.5) / m)) for i in range(m)] if taper else r
    for sgn in (1, -1):
        P = [(x, sgn * ((y(x, z) if callable(y) else y) + r * flat * 0.35), z) for i, (x, z) in enumerate(pts)]
        out.append(g_tube(P, rad, n=n, sx=1.0, sy=flat, up=(0, 0, 1)))
    return gjoin(*out)


def star_core(r=0.02, long=3.0, n=4):
    """six-pointed spark: three crossed bipyramids"""
    g = g_gem(r, r * long * 2, n, 0.5)
    return gjoin(g, gxf(g, R('X', 90)), gxf(g, R('Y', 90)))


def wedge_y(st, bevel=0.016, shoulder=0.34):
    """half thickness (x, z) -> y of a ribbon(st, sec_wedge(bevel, shoulder)) head built from straight slices"""
    zs = [q[0] for q in st]

    def f(x, z):
        a = float(np.interp(z, zs, [q[1] for q in st]))
        b = float(np.interp(z, zs, [q[2] for q in st]))
        t = float(np.interp(z, zs, [q[3] for q in st]))
        xs = b - min(bevel, b - a)
        if x >= xs:
            return t / 2 * shoulder * max(0.0, b - x) / max(b - xs, 1e-6)
        return t / 2 * (shoulder + (1 - shoulder) * (xs - x) / max(xs - a, 1e-6))
    return f


# ------------------------------------------------------------------------------------------------ faceted stones
def tones(key):
    """(dark, mid, light) palette keys of a faceted stone: facets baked in three tones read as cut crystal"""
    return (key + "_lo", key, key + "_hi")


def subgeo(g, faces):
    used = sorted({i for f in faces for i in f})
    remap = {o: k for k, o in enumerate(used)}
    return geo(g["v"][used], [tuple(remap[i] for i in f) for f in faces])


def by_tone(g, tone_of, orient=True):
    """split a geometry into {tone: geometry}; tone_of(face index, face centre) -> tone.
    The pieces are open surfaces, so Blender cannot work out their outside: with orient=True every face is wound to
    point away from the centre of the whole (convex) stone here - add the pieces with recalc=False."""
    V = g["v"]
    cen = V.mean(0)
    groups = {}
    for fi, f in enumerate(g["f"]):
        P = V[list(f)]
        c = P.mean(0)
        if orient:
            nrm = np.cross(P[1] - P[0], P[2] - P[0])
            if np.dot(nrm, c - cen) < 0:
                f = tuple(reversed(f))
        groups.setdefault(tone_of(fi, c), []).append(f)
    return {t: subgeo(g, fs) for t, fs in groups.items()}


def sector_tones(g, n, pattern=(1, 2), rot=0.0, z_split=None, low=(0, 1)):
    """tones of a lathe-like crystal (axis Z, n sides): alternate sides get alternate tones; below z_split the `low`
    pattern is used (darker foot)"""
    def tone_of(fi, c):
        s = int(math.floor(((math.atan2(c[1], c[0]) - rot) % TAU) / (TAU / n) + 1e-6)) % n
        pat = low if (z_split is not None and c[2] < z_split) else pattern
        return pat[s % len(pat)]
    return by_tone(g, tone_of)


def add_tones(w, parts, key, M=None, sharp=8, **kw):
    """add {tone: geometry} with the three tone materials of `key` (pass recalc=False for by_tone / sector_tones pieces)"""
    ks = tones(key)
    for t, g_ in parts.items():
        w(g_ if M is None else gxf(g_, M), ks[t], sharp=sharp, **kw)


def icosphere(level=1):
    """(unit vertices (N, 3), triangle list) of a subdivided icosahedron"""
    t = (1 + 5 ** 0.5) / 2
    V = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t), (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    F = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
         (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    V = [np.array(p, float) / np.linalg.norm(p) for p in V]
    for _ in range(level):
        cache, F2 = {}, []

        def mid(a, b):
            k = (min(a, b), max(a, b))
            if k not in cache:
                m = V[a] + V[b]
                V.append(m / np.linalg.norm(m))
                cache[k] = len(V) - 1
            return cache[k]
        for (a, b, c) in F:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            F2 += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        F = F2
    return np.array(V), F


def crystal_plates(Rr, level=1, shrink=0.90, depth=0.004, seed=1, weights=(0.30, 0.45, 0.25)):
    """Orb built from fitted crystal plates with thin gaps between them (a core placed inside shows through the gaps as
    glowing seams). Returns {tone: geometry} centred at the origin."""
    V, F = icosphere(level)
    rng = np.random.RandomState(seed)
    out = {0: ([], []), 1: ([], []), 2: ([], [])}
    for (a, b, c) in F:
        P = np.array([V[a], V[b], V[c]])
        cen = P.mean(0)
        outer = (cen + (P - cen) * shrink) * Rr
        inner = (cen + (P - cen) * shrink * 0.86) * (Rr - depth)
        tone = int(rng.choice(3, p=weights))
        vs, fs = out[tone]
        o = len(vs)
        vs += [tuple(p) for p in outer] + [tuple(p) for p in inner]
        fs += [(o, o + 1, o + 2), (o + 5, o + 4, o + 3)]
        for i in range(3):
            j = (i + 1) % 3
            fs.append((o + i, o + 3 + i, o + 3 + j, o + j))
    return {t: geo(np.array(vs), fs) for t, (vs, fs) in out.items() if vs}


def glint(r, y, x=0.0, z=0.0, thick=0.0014, a=0.20):
    """four-point sparkle lying in the XZ plane at height y (thin plate facing +-Y)"""
    return gxf(g_prism(star4(r, a), thick), T(x, y, z))
