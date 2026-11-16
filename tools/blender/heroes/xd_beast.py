"""Private helpers of fork D (beast forms): body bulking through the mesh, fur blobs / tufts, claws, glow strips."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from charlib import *

FINGERS = ("index", "middle", "ring", "pinky", "thumb")


def axis_bulk(co, B, spec):
    """Radial scale around bone axes, weighted by each bone's skin weight. spec = {bone name: factor}."""
    out = co.copy()
    for name, k in spec.items():
        b = mk.ARM.data.bones[name]
        h = np.array(b.head_local)
        a = np.array(b.tail_local) - h
        a /= np.linalg.norm(a)
        w = B.W[:, B.col[name]]
        rel = co - h
        radial = rel - (rel @ a)[:, None] * a
        out += radial * ((k - 1.0) * w)[:, None]
    return out


def torso_scale(co, B, prof, cy=0.02, bones=("pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "clavicle")):
    """prof = [(z, kx, ky)]: widen (x) / deepen (y) the trunk by height, weighted by the trunk bones."""
    w = np.clip(B.w(*bones), 0, 1)
    zs = [p[0] for p in prof]
    kx = np.interp(co[:, 2], zs, [p[1] for p in prof])
    ky = np.interp(co[:, 2], zs, [p[2] for p in prof])
    out = co.copy()
    out[:, 0] = co[:, 0] * (1 + (kx - 1) * w)
    out[:, 1] = cy + (co[:, 1] - cy) * (1 + (ky - 1) * w)
    return out


def finger_spec(k, thumb=None):
    d = {}
    for s in ("l", "r"):
        for f in FINGERS:
            for i in (1, 2, 3):
                d["%s_0%d_%s" % (f, i, s)] = (thumb if (thumb and f == "thumb") else k)
    return d


def blob(name, center, radii, src, wts, noise=0.012, scale=26.0, seed=1, nu=12, nv=8, cls="cloth", rot=None, sub=1,
         sharp=180):
    g = g_sphere(1.0, nu, nv, sx=radii[0], sy=radii[1], sz=radii[2])
    M = T(*center) if rot is None else T(*center) @ rot
    o = add(name, gxf(g, M), cls, src, sharp=sharp, subsurf=sub)
    apply_mods(o)
    if noise:
        displace_noise(o, noise, scale, seed)
    if isinstance(wts, str):
        w_rigid(o, wts)
    else:
        w_func(o, lambda co: dict(wts))
    return o


def default_flow(p):
    """direction fur hangs in the T-pose rest: down on the trunk / legs, towards the hand on the arms"""
    if abs(p[0]) > 0.24 and p[2] > 1.25:
        return np.array((1.0 if p[0] > 0 else -1.0, 0.0, -0.25))
    return np.array((0.0, 0.0, -1.0))


def tufts(src, n, mat, name, length=(0.05, 0.09), radius=(0.016, 0.028), seed=1, gravity=0.55, mask=None, flow=None,
          cls="cloth", sink=0.35, jitter=0.2, sides=4):
    """Scatter cone-shaped fur clumps over `src` (weights copied from the surface they grow on)."""
    apply_mods(src)
    me = src.data
    W, names = w_get(src)
    rng = np.random.RandomState(seed)
    cen = np.array([tuple(p.center) for p in me.polygons])
    nrm = np.array([tuple(p.normal) for p in me.polygons])
    area = np.array([p.area for p in me.polygons])
    ok = np.ones(len(cen), dtype=bool) if mask is None else mask(cen, nrm)
    idx = np.nonzero(ok)[0]
    if len(idx) == 0:
        return None
    pr = area[idx] / area[idx].sum()
    pick = rng.choice(idx, size=n, p=pr)
    V, F, rows = [], [], []
    for fi in pick:
        p = cen[fi]
        f = (flow or default_flow)(p)
        d = nrm[fi] * (1 - gravity) + np.asarray(f, dtype=float) * gravity + (rng.rand(3) - 0.5) * jitter
        d /= np.linalg.norm(d)
        L = rng.uniform(*length)
        r = rng.uniform(*radius)
        b = p - d * L * sink
        tip = p + d * L * (1 - sink)
        u = np.cross(d, (0.0, 0.0, 1.0))
        if np.linalg.norm(u) < 1e-3:
            u = np.cross(d, (1.0, 0.0, 0.0))
        u /= np.linalg.norm(u)
        v = np.cross(d, u)
        o = len(V)
        for k in range(sides):
            a = TAU * k / sides
            V.append(b + (u * math.cos(a) + v * math.sin(a)) * r)
        V.append(tip)
        for k in range(sides):
            F.append((o + k, o + (k + 1) % sides, o + sides))
        vid = me.polygons[fi].vertices[0]
        rows += [W[vid]] * (sides + 1)
    o = add(name, geo(np.array(V), F), cls, mat, sharp=180)
    set_weights(o, np.array(rows), names, 4)
    return o


def on_surface(obj, pts, offset=0.008):
    apply_mods(obj)
    out, nrm = [], []
    for p in pts:
        ok, loc, n, fi = obj.closest_point_on_mesh(Vector(tuple(float(v) for v in p)))
        out.append(np.array(loc) + np.array(n) * offset)
        nrm.append(np.array(n))
    return np.array(out), np.array(nrm)


def glow_strip(name, pts, onto, width=0.02, thick=0.006, offset=0.012, closed=False, mat="spirit", taper=True,
               wsrc=None, bone=None):
    """Thin emissive ribbon lying on the surface of `onto` along the guide points."""
    P, N = on_surface(onto, pts, offset)
    for _ in range(1):
        if closed:
            P = (np.roll(P, 1, 0) + P * 2 + np.roll(P, -1, 0)) / 4
        elif len(P) > 2:
            P[1:-1] = (P[:-2] + P[1:-1] * 2 + P[2:]) / 4
    m = len(P)
    rad = np.ones(m)
    if taper and not closed and m > 2:
        t = np.linspace(0, 1, m)
        rad = 0.35 + 0.65 * np.sin(np.pi * t) ** 0.6
    g = g_tube(P, rad, n=4, closed=closed, sx=thick / 2, sy=width / 2, up=tuple(N[0]))
    o = add(name, g, "emis", mat, sharp=60, double_sided=True)
    if bone:
        w_rigid(o, bone)
    else:
        w_transfer(o, wsrc or onto, smooth=0)
    return o


def claw(base, direction, curl, length=0.06, radius=0.012, seg=4, n=5):
    """curved claw geometry: starts at base, grows along direction, bends towards curl"""
    d = np.asarray(direction, dtype=float)
    d /= np.linalg.norm(d)
    c = np.asarray(curl, dtype=float)
    c /= np.linalg.norm(c)
    path, rad = [], []
    for i in range(seg + 1):
        t = i / seg
        path.append(np.asarray(base) + d * length * t + c * length * 0.45 * t * t)
        rad.append(radius * (1 - t) ** 0.8 + 0.0008)
    return g_tube(path, rad, n=n, caps=True, up=tuple(c))


def hand_claws(mat="claw", length=0.06, radius=0.012, back=0.012, name="claws_hand", cls="bone", thumb_scale=0.85):
    """claws on every finger tip of the LEFT hand (palm faces down in the T-pose), mirrored to the right"""
    made = []
    for f in FINGERS:
        b = mk.ARM.data.bones["%s_03_l" % f]
        h, t = np.array(b.head_local), np.array(b.tail_local)
        d = (t - h) / np.linalg.norm(t - h)
        k = thumb_scale if f == "thumb" else 1.0
        g = claw(t - d * back + np.array((0, 0, 0.004)), d + np.array((0, 0, 0.12)), (0, 0, -1), length * k, radius * k)
        o = add("%s_%s_l" % (name, f), g, cls, mat, bone="%s_03_l" % f, sharp=50)
        made.append(o)
        made.append(mirrored(o))
    return made


def foot_claws(xs, y, z=0.03, mat="claw", length=0.055, radius=0.013, name="claws_foot", cls="bone", cx=0.105):
    made = []
    for i, dx in enumerate(xs):
        g = claw((cx + dx, y, z), (dx * 1.2, -1.0, 0.25), (0, 0, -1), length, radius)
        o = add("%s_%d_l" % (name, i), g, cls, mat, bone="ball_l", sharp=50)
        made.append(o)
        made.append(mirrored(o))
    return made


def muzzle(origin, direction, stations, nu=14, nv=6, p=2.2, cap=True):
    """loft along `direction` (local z); stations [(dist, rx, r_down, r_up)], local x = world x, local y = up"""
    st = [(d, 0.0, 0.0, rx, rd, ru) for (d, rx, rd, ru) in stations]
    g = g_stations(st, nu=nu, nv=nv, p=p, cap1=cap)
    return gxf(g, frame(origin, direction, (0, 0, 1)))


def plate(origin, normal, up, outline, thick=0.006):
    """flat prism (outline in local x / z) lying in the plane with the given normal"""
    g = g_prism(outline, thick)
    return gxf(g, frame(origin, up, normal))
