"""Private helpers of fork A (astromancer / pactbinder / occultist / merchant). Shared libs stay untouched."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from charlib import *


def tube_w(path, radius, n=8, **kw):
    """g_tube + 'luv' (u = angle fraction, v = metres along the path) so mats.wood grain follows the shaft."""
    g = g_tube(path, radius, n=n, **kw)
    P = np.asarray(path, float)
    L = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])
    luv = np.zeros((len(g["v"]), 2))
    for i in range(len(P)):
        for k in range(n):
            luv[i * n + k] = (k / n, L[i])
    if len(g["v"]) > len(P) * n:
        luv[len(P) * n] = (0.5, 0.0)
        luv[len(P) * n + 1] = (0.5, L[-1])
    g["luv"] = luv
    return g


def ring_path(r, n=20, plane="xz", center=(0, 0, 0), sx=1.0, sy=1.0):
    pts = []
    for i in range(n):
        a = TAU * i / n
        u, v = r * math.cos(a) * sx, r * math.sin(a) * sy
        if plane == "xz":
            p = (u, 0, v)
        elif plane == "yz":
            p = (0, u, v)
        else:
            p = (u, v, 0)
        pts.append(np.array(p) + np.array(center))
    return pts


def star4(r, a=0.28):
    k = r * a
    return [(0, r), (k, k), (r, 0), (k, -k), (0, -r), (-k, -k), (-r, 0), (-k, k)]


def rhomb(w, h):
    return [(0, h), (w, 0), (0, -h), (-w, 0)]


def crescent(r_out, r_in, off, n=9, a0=-2.3, a1=2.3):
    """crescent outline in (x, z): horns pointing +z. off = shift of the inner circle upward."""
    out = []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        out.append((r_out * math.sin(a), -r_out * math.cos(a)))
    # inner arc back (reverse), slightly shifted up -> thicker belly, pointed horns
    inner = []
    for i in range(n + 1):
        a = a1 * 0.93 + (a0 * 0.93 - a1 * 0.93) * i / n
        inner.append((r_in * math.sin(a), off - r_in * math.cos(a)))
    return out + inner[1:-1]


def surf_frame(host, p, up=(0, 0, 1), lift=0.004):
    """4x4 placing a g_prism (XZ plane, thickness along Y) flat on the host surface nearest to p.
    Prism local Y -> surface normal (outward), local Z -> 'up' projected on the surface."""
    ok, loc, nrm, fi = host.closest_point_on_mesh(Vector(p))
    n = np.array(nrm, dtype=float)
    n /= np.linalg.norm(n)
    u = np.array(up, dtype=float)
    z = u - n * np.dot(u, n)
    if np.linalg.norm(z) < 1e-5:
        z = np.array((1.0, 0, 0)) - n * n[0]
    z /= np.linalg.norm(z)
    x = np.cross(n, z)
    m = np.eye(4)
    m[:3, 0], m[:3, 1], m[:3, 2] = x, n, z
    m[:3, 3] = np.array(loc) + n * lift
    return m


def decal(name, outline, host, p, cls, src, depth=0.004, up=(0, 0, 1), lift=0.0035, bone=None, owner="body", flip=False):
    """Small plate (rune / emblem / patch) glued on the host surface near p; weights copied from the host."""
    if any(m.type != 'ARMATURE' for m in host.modifiers):
        apply_mods(host)
    M = surf_frame(host, p, up, lift)
    if flip:
        M = M @ R('Z', 180)
    o = add(name, gxf(g_prism(outline, depth), M), cls, src, sharp=30, owner=owner, double_sided=True)
    if bone:
        w_rigid(o, bone)
    else:
        w_transfer(o, host, smooth=0)
    return o


def panel(name, x0, x1, z_top, z_bot, yfn, cls, src, nu=5, nv=10, thick=0.005, taper=0.0, point=0.0, **kw):
    """Hanging cloth strip (stole / tabard / apron) in front of the body. yfn(x, z) -> y of the surface."""
    def fn(U, V):
        z = z_top + (z_bot - z_top) * V
        xm, hw = (x0 + x1) / 2, (x1 - x0) / 2
        x = xm + (U * 2 - 1) * hw * (1 + taper * V)
        z = z - point * (1 - np.abs(U * 2 - 1)) * V ** 3
        y = yfn(x, z)
        return np.stack([x, y, z], axis=-1)
    return add(name, g_grid(fn, nu, nv, close_u=False), cls, src, solidify=thick, sharp=180, double_sided=True, **kw)


def front_y(stations, margin=0.02):
    """y of the front surface of a g_stations loft (stations [(z,cx,cy,rx,ryf,ryb)]) as function (x, z)"""
    st = sorted(stations, key=lambda q: q[0])

    def f(x, z):
        cy = spl(z, [(q[0], q[2]) for q in st])
        rx = spl(z, [(q[0], q[3]) for q in st])
        ryf = spl(z, [(q[0], q[4]) for q in st])
        return cy - ryf * np.sqrt(np.clip(1 - (x / rx) ** 2, 0.02, 1)) - margin
    return f


def staff_grip(tilt=25.0, lean=18.0, slide=0.0):
    """right-hand staff frame: tilt = more upright (tip towards the forearm side), lean = tip away from the body"""
    return grip("r", tilt=tilt, slide=slide) @ R('X', lean)


def wpn(owner, G, parts, bone="hand_r"):
    """parts = [(geo, cls, src, kwargs), ...] in weapon-local space"""
    out = []
    for i, (g_, cls, src, kw) in enumerate(parts):
        out.append(add("%s_p%02d" % (owner, i), gxf(g_, G), cls, src, owner=owner, bone=bone, **kw))
    return out
