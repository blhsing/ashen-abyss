"""Private helpers of the caster batch (hero_astromancer / hero_soulbinder / hero_pactbinder).
The shared kit modules (charlib, mk, mats, finalize, rigio) stay untouched; small helpers that first appeared in the
previous artist's fork modules (xa_common / xb_common) are copied here so this batch does not depend on them."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from charlib import *


# ---------------------------------------------------------------------------------------------- paths / tubes
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


def zline(z0, z1, n=6):
    return [(0.0, 0.0, z0 + (z1 - z0) * i / (n - 1)) for i in range(n)]


def gnarl(path, amp=0.012, seed=1):
    """slightly crooked version of a straight path (hand-cut staff)"""
    rng = np.random.RandomState(seed)
    P = np.asarray(path, dtype=float).copy()
    P[1:-1, 0] += (rng.rand(len(P) - 2) - 0.5) * 2 * amp
    P[1:-1, 1] += (rng.rand(len(P) - 2) - 0.5) * 2 * amp
    return P


def resample(P, step):
    P = np.asarray(P, float)
    seg = np.linalg.norm(np.diff(P, axis=0), axis=1)
    L = np.concatenate([[0.0], np.cumsum(seg)])
    n = max(2, int(round(L[-1] / step)) + 1)
    s = np.linspace(0, L[-1], n)
    return np.stack([np.interp(s, L, P[:, k]) for k in range(3)], axis=1)


def sag(a, b, drop, n=9):
    """hanging curve from a to b (parabolic sag of `drop` metres)"""
    a, b = np.asarray(a, float), np.asarray(b, float)
    return [a + (b - a) * t - np.array((0, 0, drop)) * 4 * t * (1 - t) for t in np.linspace(0, 1, n)]


def band(z, r, h=0.022, n=8, bulge=1.12):
    return g_lathe([(r, z - h / 2), (r * bulge, z), (r, z + h / 2)], n=n)


def g_link(length, width, wire, seg=6, n=3):
    pts = [(width / 2 * math.cos(TAU * i / seg), 0.0, length / 2 * math.sin(TAU * i / seg)) for i in range(seg)]
    return g_tube(pts, wire, n=n, closed=True)


def g_chain(path, link=0.036, wire=0.0045, width=0.62, seg=6, n=3, up=(0, 0, 1), pitch=0.70):
    """Chain of interlocked oval links along a polyline (alternate links turned 90 degrees)."""
    P = resample(path, link * pitch)
    gs = []
    for i in range(len(P) - 1):
        a, b = P[i], P[i + 1]
        M = frame((a + b) / 2, b - a, up) @ R('Z', 90.0 * (i % 2))
        gs.append(gxf(g_link(link, link * width, wire, seg, n), M))
    return gjoin(*gs)


# ---------------------------------------------------------------------------------------------- 2D outlines
def star4(r, a=0.28):
    k = r * a
    return [(0, r), (k, k), (r, 0), (k, -k), (0, -r), (-k, -k), (-r, 0), (-k, k)]


def rhomb(w, h):
    return [(0, h), (w, 0), (0, -h), (-w, 0)]


def chevron(w, h, t):
    """V-shaped stroke, point down"""
    return [(-w, h), (-w + t, h), (0, -h + t * 1.5), (w - t, h), (w, h), (0, -h)]


def bar(w, h):
    return [(-w, h), (w, h), (w, -h), (-w, -h)]


def hook(w, h, t):
    """angular hook / half-box stroke"""
    return [(-w, h), (w, h), (w, -h), (w - t, -h), (w - t, h - t), (-w, h - t)]


def fork(w, h, t):
    """three-tined mark (tines up)"""
    return [(-w, h), (-w + t, h), (-w + t, -h + t * 2), (-t / 2, -h + t * 2), (-t / 2, h), (t / 2, h), (t / 2, -h + t * 2),
            (w - t, -h + t * 2), (w - t, h), (w, h), (w, -h + t), (t / 2, -h + t), (t / 2, -h - t), (-t / 2, -h - t), (-t / 2, -h + t),
            (-w, -h + t)]


def crescent(r_out, r_in, off, n=9, a0=-2.3, a1=2.3):
    """crescent outline in (x, z): horns pointing +z. off = shift of the inner circle upward."""
    out = []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        out.append((r_out * math.sin(a), -r_out * math.cos(a)))
    inner = []
    for i in range(n + 1):
        a = a1 * 0.93 + (a0 * 0.93 - a1 * 0.93) * i / n
        inner.append((r_in * math.sin(a), off - r_in * math.cos(a)))
    return out + inner[1:-1]


# ---------------------------------------------------------------------------------------------- surface decals
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


def ray_frame(host, p, d, up=(0, 0, 1), lift=0.004):
    """like surf_frame, but the surface point is found by casting a ray from p along d (host local space)"""
    ok, loc, nrm, fi = host.ray_cast(Vector(p), Vector(d).normalized())
    if not ok:
        return surf_frame(host, p, up, lift)
    n = np.array(nrm, dtype=float)
    n /= np.linalg.norm(n)
    if np.dot(n, np.array(d, float)) > 0:
        n = -n
    u = np.array(up, dtype=float)
    z = u - n * np.dot(u, n)
    if np.linalg.norm(z) < 1e-5:
        z = np.array((1.0, 0, 0)) - n * n[0]
    z /= np.linalg.norm(z)
    m = np.eye(4)
    m[:3, 0], m[:3, 1], m[:3, 2] = np.cross(n, z), n, z
    m[:3, 3] = np.array(loc) + n * lift
    return m


def decal(name, outline, host, p, cls, src, depth=0.004, up=(0, 0, 1), lift=0.0035, bone=None, owner="body", flip=False,
          out=None, ray=None):
    """Small plate (rune / emblem / patch) glued on the host surface near p; weights copied from the host.
    out = optional reference direction: the plate normal is flipped to point along it (hosts with inward normals).
    ray = optional direction: place the plate where a ray from p along it hits the host (instead of the closest point)."""
    if any(m.type != 'ARMATURE' for m in host.modifiers):
        apply_mods(host)
    M = ray_frame(host, p, ray, up, lift) if ray is not None else surf_frame(host, p, up, lift)
    if out is not None and np.dot(M[:3, 1], np.array(out, float)) < 0:
        n = -M[:3, 1]
        M[:3, 3] = M[:3, 3] + n * 2 * lift
        M[:3, 1] = n
        M[:3, 0] = -M[:3, 0]
    if flip:
        M = M @ R('Z', 180)
    o = add(name, gxf(g_prism(outline, depth), M), cls, src, sharp=30, owner=owner, double_sided=True)
    if bone:
        w_rigid(o, bone)
    else:
        w_transfer(o, host, smooth=0)
    return o


def panel(name, x0, x1, z_top, z_bot, yfn, cls, src, nu=5, nv=10, thick=0.005, taper=0.0, point=0.0, zmod=None, **kw):
    """Hanging cloth strip (stole / tabard / apron) in front of the body. yfn(x, z) -> y of the surface."""
    def fn(U, V):
        z = z_top + (z_bot - z_top) * V
        xm, hw = (x0 + x1) / 2, (x1 - x0) / 2
        x = xm + (U * 2 - 1) * hw * (1 + taper * V)
        z = z - point * (1 - np.abs(U * 2 - 1)) * V ** 3
        if zmod is not None:
            z = zmod(U, V, z)
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


def back_y(stations, margin=0.02):
    st = sorted(stations, key=lambda q: q[0])

    def f(x, z):
        cy = spl(z, [(q[0], q[2]) for q in st])
        rx = spl(z, [(q[0], q[3]) for q in st])
        ryb = spl(z, [(q[0], q[5]) for q in st])
        return cy + ryb * np.sqrt(np.clip(1 - (x / rx) ** 2, 0.02, 1)) + margin
    return f


def tails_mod(k=7, depth=0.18, seed=1, power=2.5, lens=None, notch=0.9, uneven=0.6, extra=None):
    """Hem of a g_stations / robe loft torn into k pointed tails (hem = V 0). The stations' lowest z is the tip of the
    longest tail; notches between the tails rise by depth*notch, shorter tails by depth*uneven*(1-len)."""
    rng = np.random.RandomState(seed)
    ln = np.array(lens, float) if lens is not None else (0.25 + 0.75 * rng.rand(k))
    ph = rng.rand()

    def mod(U, V, x, y, z):
        q = U * k + ph
        t = q - np.floor(q)
        idx = np.floor(q).astype(int) % k
        tri = 1 - np.abs(2 * t - 1)
        rise = depth * ((1 - tri ** 0.75) * notch + (1 - ln[idx]) * uneven)
        z = z + rise * (1 - V) ** power
        if extra is not None:
            x, y, z = extra(U, V, x, y, z)
        return x, y, z
    return mod


# ---------------------------------------------------------------------------------------------- weapons / props
def staff_grip(tilt=46.0, lean=30.0, slide=0.0, roll=0.0, side="r"):
    """Staff frame in the fist: tilt = more upright (tip towards the forearm side), lean = tip away from the body.
    The defaults keep a long staff upright-ish and clear of head / torso in Idle, Walk, Jog, Sword_Idle and the
    Spell_Simple clips (see kc_grip.py)."""
    return grip(side, tilt=tilt, slide=slide, roll=roll) @ R('X', lean if side == "r" else -lean)


def wpn(owner, G, parts, bone="hand_r"):
    """parts = [(geo, cls, src, kwargs), ...] in weapon-local space"""
    out = []
    for i, (g_, cls, src, kw) in enumerate(parts):
        out.append(add("%s_p%02d" % (owner, i), gxf(g_, G), cls, src, owner=owner, bone=bone, **kw))
    return out


def props(prefix, M, parts, bone):
    """rigid body props: parts = [(geo, cls, src, kwargs)] in prop-local space placed by M"""
    out = []
    for i, (g_, cls, src, kw) in enumerate(parts):
        out.append(add("%s_%02d" % (prefix, i), gxf(g_, M), cls, src, bone=bone, **kw))
    return out


def shaft(z0, z1, r0, r1, wob=0.0, n=9, seed=1):
    rng = np.random.RandomState(seed)
    zs = np.linspace(z0, z1, n)
    pts = [(wob * (rng.rand() - 0.5), wob * (rng.rand() - 0.5), z) for z in zs]
    pts[0] = (0, 0, z0)
    return pts, list(np.linspace(r0, r1, n))


def g_tooth(length=0.06, r=0.012, bend=0.012, n=6):
    """curved fang / claw / bone spike along +Z, bending towards +X"""
    path = [(bend * (t ** 2), 0.0, length * t) for t in (0.0, 0.3, 0.6, 0.85, 1.0)]
    return g_tube(path, [r, r * 0.85, r * 0.6, r * 0.32, r * 0.05], n=n)


def g_beads(path, r=0.012, nu=6, nv=4, sz=1.0):
    return gjoin(*[gxf(g_sphere(r, nu, nv, sz=sz), T(*p)) for p in path])


def g_bone(length=0.2, r=0.012, knob=1.9, n=6):
    """long bone along +Z from the origin: shaft with a knuckle at both ends"""
    k = r * knob
    prof = [(0.0, -k * 0.5), (k * 0.8, -k * 0.2), (k, k * 0.3), (r * 1.1, k * 1.0), (r, length * 0.3), (r, length * 0.7),
            (r * 1.1, length - k * 1.0), (k, length - k * 0.3), (k * 0.8, length + k * 0.2), (0.0, length + k * 0.5)]
    return g_lathe(prof, n=n)


def g_book(w=0.15, h=0.20, t=0.05, ct=0.007):
    """Closed book in the XZ plane (w along X, h along Z, thickness along Y), spine on the -X side.
    Returns (cover geometry, page-block geometry)."""
    cover = gjoin(g_box(w, ct, h, (0, -t / 2 + ct / 2, 0)), g_box(w, ct, h, (0, t / 2 - ct / 2, 0)),
                  g_box(ct, t, h, (-w / 2 + ct / 2, 0, 0)))
    pages = g_box(w - ct - 0.008, t - 2 * ct, h - 0.016, (ct / 2 - 0.004, 0, 0))
    return cover, pages


def g_corner(s=0.03, t=0.003):
    """flat triangular corner fitting in the XZ plane (right angle at the origin, legs along +X and +Z)"""
    return g_prism([(0, 0), (s, 0), (s * 0.55, s * 0.18), (s * 0.18, s * 0.55), (0, s)], t)


def g_scroll(length=0.2, r=0.024, cap=0.03, n=8):
    """scroll case along Z, centred: returns (tube, caps)"""
    h = length / 2
    tube = g_lathe([(0, -h), (r, -h), (r, h), (0, h)], n=n)
    c1 = g_lathe([(0, -h - 0.006), (r * 1.18, -h - 0.006), (r * 1.22, -h + cap * 0.5), (r * 1.14, -h + cap)], n=n)
    c2 = g_lathe([(r * 1.14, h - cap), (r * 1.22, h - cap * 0.5), (r * 1.18, h + 0.006), (r * 0.5, h + 0.02), (0, h + 0.022)], n=n)
    return tube, gjoin(c1, c2)


def g_antler(scale=1.0, tines=3, n=6):
    """Left antler: main beam rising from the origin, sweeping out (+X) and back (+Y), tines pointing up / forward."""
    s = scale
    beam = np.array([(0.0, 0.0, 0.0), (0.035, 0.012, 0.07), (0.085, 0.035, 0.14), (0.145, 0.06, 0.20), (0.20, 0.07, 0.275),
                     (0.225, 0.055, 0.36), (0.215, 0.03, 0.43)]) * s
    rad = np.array([0.021, 0.019, 0.017, 0.015, 0.013, 0.010, 0.004]) * s
    parts = [g_tube(beam, rad, n=n)]
    spec = [(1, (-0.01, -0.07, 0.09), 0.015), (2, (0.01, -0.09, 0.12), 0.014), (3, (0.03, -0.08, 0.14), 0.013),
            (4, (0.06, -0.05, 0.13), 0.011)]
    for (i, d, r) in spec[:tines]:
        a = beam[i]
        d = np.array(d) * s
        mid = a + d * 0.5 + np.array((0, 0, 0.012)) * s
        parts.append(g_tube([a, mid, a + d + np.array((0, 0, 0.03)) * s], [r * s, r * s * 0.7, r * s * 0.12], n=n))
    return gjoin(*parts)


def mirror_x(g):
    return gxf(g, S(-1, 1, 1))


def g_scythe(L=0.45, w0=0.085, drop=0.22, rise=0.05, n=10, sharp=0.7):
    """Scythe blade outline in the XZ plane: root on the shaft at the origin, sweeping towards +X and hooking down.
    Returns (outline [(x, z)], back polyline, edge polyline) for g_prism / trims."""
    ts = np.linspace(0, 1, n)
    xb = L * ts
    zb = rise * np.sin(np.pi * ts * 0.9) - drop * ts ** 2.0
    w = w0 * (1 - ts) ** sharp
    back = [(float(x), float(z)) for x, z in zip(xb, zb)]
    edge = [(float(x), float(z - ww)) for x, z, ww in zip(xb, zb, w)]
    return back + edge[::-1][1:], back, edge


def strip_along(line, h, up=True):
    """thin band outline following a polyline [(x, z)] (h = height, grown upwards or downwards)"""
    s = h if up else -h
    return [(x, z) for x, z in line] + [(x, z + s) for x, z in line[::-1]]


def chew_edge(obj, rounds=2, p=0.35, seed=1, keep=None):
    """Ragged border: randomly delete faces touching the open boundary. keep(co) -> True protects a vertex."""
    rng = np.random.RandomState(seed)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for _ in range(rounds):
        dead = []
        for f in bm.faces:
            if any(e.is_boundary for e in f.edges):
                if keep is not None and any(keep(v.co) for v in f.verts):
                    continue
                if rng.rand() < p:
                    dead.append(f)
        if dead:
            bmesh.ops.delete(bm, geom=dead, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return obj


def gaunt(c, chest=0.10, arms=0.14):
    """slimmer, less heroic build through the mesh only (bones untouched)"""
    def fn(co, B):
        k = sstep(co[:, 2], 1.05, 1.30) * (1 - sstep(co[:, 2], 1.50, 1.56))
        arm = B.w(*BONES_ARM, *BONES_HAND)
        co[:, 1] = co[:, 1] * (1 - chest * k * (1 - arm)) + 0.004 * k
        a = np.clip(arm, 0, 1) * arms * (1 - B.w(*BONES_HAND))
        co[:, 1] = co[:, 1] + (0.068 - co[:, 1]) * a
        co[:, 2] = co[:, 2] + (1.44 - co[:, 2]) * a
        return co
    reshape_body(c, fn)


def drop_face_parts(c, eyes=True, brows=True):
    if eyes and c.eyes is not None:
        bpy.data.objects.remove(c.eyes, do_unlink=True)
        c.eyes = None
    if brows and c.brows is not None:
        bpy.data.objects.remove(c.brows, do_unlink=True)
        c.brows = None


def g_cowl(st, chin=1.575, brow=1.76, open_deg=58.0, peak=0.03, nu=28, nv=16, chin_close=0.045):
    """Hood lofted through stations [(z, cy, rx, ry_front, ry_back)] (last one = apex with zero radii). Rings are
    C-shaped (open at the face) between chin and brow, closed below the chin (collar) and above the brow (dome)."""
    st = sorted(st)
    z0, z1 = st[0][0], st[-1][0]

    def col(i):
        return [(s[0], s[i]) for s in st]

    def fn(U, V):
        z = z0 + (z1 - z0) * V
        cy, rx, ryf, ryb = spl(z, col(1)), spl(z, col(2)), spl(z, col(3)), spl(z, col(4))
        rx, ryf, ryb = np.clip(rx, 0, None), np.clip(ryf, 0, None), np.clip(ryb, 0, None)
        up = np.clip((brow - z) / (brow - chin), 0, 1) ** 0.6
        lo = sstep(z, chin - chin_close, chin + 0.01)
        op = math.radians(open_deg) * up * lo
        a = op + (TAU - 2 * op) * U
        s, co = np.sin(a), np.cos(a)
        ry = np.where(co > 0, ryf, ryb)
        rimness = np.clip(1 - np.minimum(U, 1 - U) / 0.16, 0, 1)
        hz = np.exp(-((z - brow) / 0.07) ** 2)
        y = cy - ry * co - peak * rimness * hz - 0.010 * rimness * (op > 1e-4)
        return np.stack([rx * s, y, z], axis=-1)
    return g_grid(fn, nu, nv, close_u=False, cap1=False)


def cowl(c, src, st, name="hood", cls="cloth", thick=0.008, head_z=1.60, **kw):
    """Raised hood from g_cowl stations; follows the head above head_z, neck / chest below. Returns (object, geo)."""
    g = g_cowl(st, **kw)
    o = add(name, g, cls, src, sharp=180)
    weld(o)
    m = o.modifiers.new("solid", 'SOLIDIFY')
    m.thickness = thick
    m.offset = 1.0
    z0 = min(s[0] for s in st)

    def wf(co):
        h = sstep(co[:, 2], z0 + 0.05, head_z)
        return {"Head": h, "neck_01": (1 - h) * 0.5, "spine_03": (1 - h) * 0.5}
    w_func(o, wf)
    return o, g


def ring_of(g, nu, row, grow=0.0):
    """closed path from one row of an open-u g_grid surface (drops the duplicated seam point), pushed outwards"""
    P = np.array(rim_of(g, nu, row), dtype=float)[:-1]
    cen = P.mean(0)
    d = P - cen
    d[:, 2] = 0
    d /= np.maximum(np.linalg.norm(d, axis=1, keepdims=True), 1e-9)
    return P + d * grow
