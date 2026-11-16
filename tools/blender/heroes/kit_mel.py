"""Private helpers of the melee / skirmisher batch (hero_hunter / hero_wildsoul / hero_forgebody).
The shared kit modules (charlib, mk, mats, finalize, rigio, kit_cst) stay untouched; this module only adds to them."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_cst import *


# ---------------------------------------------------------------------------------------------- frames
def pistol_grip(side="r", pitch=0.0, yaw=0.0, back=0.0, up=0.0):
    """Crossbow-local -> armature rest space. local +Y = barrel (level in Pistol_Aim_Neutral), +Z = up through the
    fist (the pistol grip runs along -Z), +X = lateral (limbs). pitch raises the muzzle (deg), back slides the
    weapon towards the wrist, up lifts it out of the fist."""
    H = bone_frame("hand_" + side)
    y = np.array((0.03 if side == "r" else -0.03, 0.98, -0.18))
    y /= np.linalg.norm(y)
    z = np.array(GRIP_A[side], dtype=float)
    z = z - y * np.dot(z, y)
    z /= np.linalg.norm(z)
    x = np.cross(y, z)
    L = np.eye(4)
    L[:3, 0], L[:3, 1], L[:3, 2], L[:3, 3] = x, y, z, GRIP_C[side]
    return H @ L @ R('X', pitch) @ R('Z', yaw) @ T(0, -back, up)


def hand_axes(side="r"):
    """(origin, along, dorsal, thumb) unit vectors of the hand in armature rest space:
    along = wrist -> knuckles, dorsal = out of the back of the hand, thumb = pinky -> index side."""
    H = bone_frame("hand_" + side)
    o = H[:3, 3]
    along = H[:3, 1]
    thumb = H[:3, :3] @ (np.array(GRIP_A[side]) / np.linalg.norm(GRIP_A[side]))
    thumb = thumb - along * np.dot(thumb, along)
    thumb /= np.linalg.norm(thumb)
    palm = H[:3, :3] @ np.array((GRIP_C[side][0], 0.0, 0.0))
    palm /= np.linalg.norm(palm)
    dorsal = -palm
    dorsal = dorsal - along * np.dot(dorsal, along) - thumb * np.dot(dorsal, thumb)
    dorsal /= np.linalg.norm(dorsal)
    return o, along, dorsal, thumb


def hand_frame(side="r"):
    """4x4: local +X = along the hand (wrist -> knuckles), +Y = thumb side, +Z = dorsal (out of the back of the hand).
    Origin at the wrist joint. (For the left hand the frame is left-handed mirrored into a proper rotation by
    flipping +Y, so always model symmetric parts or use the mirror helpers.)"""
    o, along, dorsal, thumb = hand_axes(side)
    m = np.eye(4)
    y = np.cross(dorsal, along)
    m[:3, 0], m[:3, 1], m[:3, 2], m[:3, 3] = along, y, dorsal, o
    return m


# ---------------------------------------------------------------------------------------------- shapes
def g_feather(length=0.14, width=0.03, thick=0.003):
    """feather in the XZ plane, quill at the origin, vane towards +Z"""
    w = width / 2
    k = length / 0.14
    outl = [(0.0, 0.0), (0.004, 0.02 * k), (w, 0.30 * length), (w * 0.9, 0.62 * length), (w * 0.45, 0.9 * length),
            (0.0, length), (-w * 0.5, 0.86 * length), (-w * 0.95, 0.58 * length), (-w, 0.28 * length), (-0.004, 0.02 * k)]
    return g_prism(outl, thick)


def g_cone(r, h, n=6, base=0.0):
    return g_lathe([(0.0, base), (r, base), (0.0, base + h)], n=n)


def g_cyl(r, z0, z1, n=8, r1=None):
    r1 = r if r1 is None else r1
    return g_lathe([(0.0, z0), (r, z0), (r1, z1), (0.0, z1)], n=n)


def g_ring(r, wire, n=14, m=5, sx=1.0, sy=1.0):
    """torus in the XY plane"""
    return g_tube([(r * math.cos(TAU * i / n) * sx, r * math.sin(TAU * i / n) * sy, 0.0) for i in range(n)], wire, n=m, closed=True)


def g_slab(outline, depth):
    """g_prism with the thickness along Z instead (outline in XY)"""
    return gxf(g_prism(outline, depth), R('X', 90))


def arc_pts(r, a0, a1, n=9, plane="xy", center=(0, 0, 0), sx=1.0, sy=1.0):
    out = []
    for i in range(n):
        a = math.radians(a0 + (a1 - a0) * i / (n - 1))
        u, v = r * math.cos(a) * sx, r * math.sin(a) * sy
        p = {"xy": (u, v, 0), "xz": (u, 0, v), "yz": (0, u, v)}[plane]
        out.append(np.array(p, float) + np.array(center, float))
    return out


def helix_on_arm(c, x0, x1, turns, r_guide=0.11, n=30, phase=0.0, side=1, offset=0.008, axis_y=0.068, axis_z=1.435):
    """spiral path wound round the (T-pose) arm between x0 and x1, projected on the body surface"""
    pts = []
    for t in np.linspace(0, 1, n):
        a = TAU * turns * t + phase
        pts.append((side * (x0 + (x1 - x0) * t), axis_y + r_guide * math.cos(a), axis_z + r_guide * math.sin(a)))
    return wrap_path(c, pts, offset)


def helix_on_leg(c, z0, z1, turns, cx=0.09, cy=0.02, r_guide=0.14, n=30, phase=0.0, side=1, offset=0.01):
    pts = []
    for t in np.linspace(0, 1, n):
        a = TAU * turns * t + phase
        pts.append((side * cx + r_guide * math.cos(a), cy + r_guide * math.sin(a), z0 + (z1 - z0) * t))
    return wrap_path(c, pts, offset)


def surf_line(c, pts, offset=0.004, step=0.012, smooth=1):
    """polyline following the reference body surface through the guide points (resampled, projected, lifted)"""
    P = resample(pts, step)
    P = wrap_path(c, P, offset)
    for _ in range(smooth):
        P[1:-1] = (P[:-2] + P[1:-1] * 2 + P[2:]) / 4
    return P


def scar(c, name, pts, src, width=0.007, thick=0.004, offset=0.0025, cls="emis", only=None, taper=True, step=0.012):
    """Thin raised welt following the skin (brand scars). Weights are copied from the body, so it deforms with it."""
    P = surf_line(c, pts, offset, step)
    n = len(P)
    if taper:
        k = np.sin(np.linspace(0.12, math.pi - 0.12, n)) ** 0.5
    else:
        k = np.ones(n)
    g = g_tube(P, list(k), n=4, sx=thick / 2, sy=width / 2, up=(0.3, -0.5, 0.8))
    o = add(name, g, cls, src, sharp=180)
    w_transfer(o, c.ref, smooth=0, only=only)
    return o


def shell_band(c, mask, offset, name, cls, src, smooth=2, hide_margin=None, **kw):
    """body shell + hide of the covered skin (mask shrunk by one ring)"""
    o = shell(c.B, mask, offset, name, cls, src, smooth=smooth, sharp=180, **kw)
    hide(c, c.B.shrink(mask, 1) if hide_margin is None else hide_margin)
    return o


def body_bulk(c, spec=None, torso=None, cy=0.02):
    """Bulk limbs radially around their bones (spec {bone: factor}) and scale the trunk by height
    (torso [(z, kx, ky)]). Mesh only, bones untouched."""
    def fn(co, B):
        out = co.copy()
        if spec:
            for name, k in spec.items():
                b = mk.ARM.data.bones[name]
                h = np.array(b.head_local)
                a = np.array(b.tail_local) - h
                a /= np.linalg.norm(a)
                w = B.W[:, B.col[name]]
                rel = co - h
                radial = rel - (rel @ a)[:, None] * a
                out += radial * ((k - 1.0) * w)[:, None]
        if torso:
            w = np.clip(B.w("pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "clavicle"), 0, 1)
            zs = [p[0] for p in torso]
            kx = np.interp(co[:, 2], zs, [p[1] for p in torso])
            ky = np.interp(co[:, 2], zs, [p[2] for p in torso])
            out[:, 0] = out[:, 0] * (1 + (kx - 1) * w)
            out[:, 1] = cy + (out[:, 1] - cy) * (1 + (ky - 1) * w)
        return out
    reshape_body(c, fn)


def tuft(p, d, length=0.07, r=0.02, bend=0.02, n=5):
    """fur lock / spike starting at p, pointing along d, curling downwards"""
    d = np.array(d, float)
    d /= np.linalg.norm(d)
    p = np.array(p, float)
    pts = [p + d * length * t + np.array((0, 0, -bend)) * t * t for t in (0.0, 0.35, 0.7, 1.0)]
    return g_tube(pts, [r, r * 0.8, r * 0.48, r * 0.06], n=n)


def fur_fringe(path, out_fn, length=0.07, r=0.02, bend=0.03, every=1, seed=1, jitter=0.35):
    """row of fur locks along a path. out_fn(p) -> direction of the locks."""
    rng = np.random.RandomState(seed)
    gs = []
    for i, p in enumerate(path):
        if i % every:
            continue
        k = 1.0 + (rng.rand() - 0.5) * 2 * jitter
        gs.append(tuft(p, out_fn(np.array(p, float)), length * k, r * (0.8 + 0.4 * rng.rand()), bend * k))
    return gjoin(*gs)


def set_uv_const(obj, u, v=0.5):
    me = obj.data
    if not len(me.uv_layers):
        me.uv_layers.new(name="UVMap")
    uv = np.empty(len(me.loops) * 2, dtype=np.float32)
    uv[0::2] = u
    uv[1::2] = v
    me.uv_layers[0].data.foreach_set('uv', uv)


# ---------------------------------------------------------------------------------------------- straps / boots
def _proj(c, P, host=None):
    S, N = [], []
    h = host or c.ref
    for p in P:
        ok, loc, nrm, fi = h.closest_point_on_mesh(Vector(p))
        S.append(np.array(loc))
        N.append(np.array(nrm))
    return np.array(S), np.array(N)


def ribbon(c, guide, src, width=0.04, thick=0.008, offset=0.02, name="strap", cls="leather", closed=True, step=0.025,
           smooth=2, only=None, wsmooth=2, sharp=50, host=None):
    """Flat strap lying on the body: the guide is resampled densely, projected on the reference body and kept at
    `offset` above it; the section is oriented by the surface normal (no twisting like a swept tube).
    Returns (object, path points, path normals)."""
    G = [np.array(p, float) for p in guide]
    P = resample(G + ([G[0]] if closed else []), step)
    if closed:
        P = P[:-1]
    if host is not None and any(m.type != 'ARMATURE' for m in host.modifiers):
        apply_mods(host)
    S, N = _proj(c, P, host)
    Q = S + N * offset

    def sm(X):
        if closed:
            return (np.roll(X, 1, 0) + X * 2 + np.roll(X, -1, 0)) / 4
        Y = X.copy()
        Y[1:-1] = (X[:-2] + X[1:-1] * 2 + X[2:]) / 4
        return Y
    for _ in range(smooth):
        Q = sm(Q)
    S2, N2 = _proj(c, Q, host)
    d = ((Q - S2) * N2).sum(1)
    Q = Q + N2 * np.clip(offset - d, 0, None)[:, None]
    for _ in range(3):
        N2 = sm(N2)
    N2 /= np.linalg.norm(N2, axis=1, keepdims=True)
    n = len(Q)
    verts = []
    for i in range(n):
        a = Q[(i - 1) % n] if (closed or i > 0) else Q[i]
        b = Q[(i + 1) % n] if (closed or i < n - 1) else Q[i]
        t = b - a
        t /= max(np.linalg.norm(t), 1e-9)
        bn = np.cross(t, N2[i])
        bn /= max(np.linalg.norm(bn), 1e-9)
        nn = np.cross(bn, t)
        hw = width / 2
        verts += [Q[i] - bn * hw, Q[i] + bn * hw, Q[i] + bn * hw + nn * thick, Q[i] - bn * hw + nn * thick]
    faces = []
    for i in range(n if closed else n - 1):
        j = (i + 1) % n
        for k in range(4):
            k2 = (k + 1) % 4
            faces.append((i * 4 + k, j * 4 + k, j * 4 + k2, i * 4 + k2))
    if not closed:
        faces.append((0, 1, 2, 3))
        faces.append(((n - 1) * 4 + 3, (n - 1) * 4 + 2, (n - 1) * 4 + 1, (n - 1) * 4))
    o = add(name, geo(np.array(verts), faces), cls, src, sharp=sharp)
    w_transfer(o, c.ref, smooth=wsmooth, only=only)
    return o, Q, N2


def toe_box(o, height=0.046, length=0.105, grow=0.008, ahead=0.026, p=2.6):
    """Round boot toe fitted to the actual foot of this body (charlib.boots' toe cap is sized for the male base):
    wraps the separate toes of a foot shell in an ellipsoid, flattens the sole and smooths the forefoot."""
    co = get_co(o)
    fore_all = np.zeros(len(co), dtype=bool)
    for s in (1, -1):
        side = (co[:, 0] * s > 0) & (co[:, 2] < 0.13)
        y0 = co[side, 1].min()
        sel = side & (co[:, 1] < y0 + length)
        xs = co[sel, 0]
        cen = np.array(((xs.min() + xs.max()) / 2, y0 + length - 0.004, 0.034))
        rad = np.array(((xs.max() - xs.min()) / 2 + grow, length + ahead, height))
        d = co[sel] - cen
        k = ((np.abs(d / rad) ** p).sum(1)) ** (1.0 / p) + 1e-9
        target = cen + d / k[:, None]
        w = sstep((y0 + length) - co[sel, 1], 0.0, 0.055)[:, None]
        co[sel] = np.where((k < 1.0)[:, None], co[sel] * (1 - w) + target * w, co[sel])
        fore_all |= side & (co[:, 1] < y0 + length * 0.8)
    co[co[:, 2] < 0.012, 2] = -0.004
    set_co(o, co)
    sm = laplacian(co, mesh_edges(o), 3, 0.5)
    co[fore_all] = sm[fore_all]
    co[co[:, 2] < 0.004, 2] = -0.004
    set_co(o, co)
    return o


# ---------------------------------------------------------------------------------------------- fur / organic
def shag(o, amp=0.012, scale=30.0, seed=1):
    """bake the modifiers of a fur part and roughen it (clumpy pelt surface)"""
    apply_mods(o)
    displace_noise(o, amp, scale, seed)
    return o


def edge_tufts(c, o, src, name, length=0.06, r=0.018, p=0.5, seed=1, cls="cloth", down=1.0, out=0.5, only=None, keep=None):
    """fur locks hanging from the open border of a shell (pelt fringe); weights copied from the body"""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    pts = [(np.array(v.co), np.array(v.normal)) for v in bm.verts if v.is_boundary]
    bm.free()
    rng = np.random.RandomState(seed)
    gs = []
    for (q, n_) in pts:
        if rng.rand() > p or (keep is not None and not keep(q)):
            continue
        d = n_ * out + np.array((0.0, 0.0, -down))
        k = 0.7 + 0.6 * rng.rand()
        gs.append(tuft(q - n_ * 0.004, d, length * k, r * (0.8 + 0.4 * rng.rand()), bend=0.02 * k))
    if not gs:
        return None
    t = add(name, gjoin(*gs), cls, src, sharp=180)
    w_transfer(t, c.ref, smooth=0, only=only)
    return t


def lumpy(g, amp=0.12, freq=38.0, seed=1, center=None):
    """knotty deformation of a convex blob (burl / root / rough stone), deterministic"""
    v = g["v"].copy()
    cen = v.mean(0) if center is None else np.array(center, float)
    d = v - cen
    ph = np.random.RandomState(seed).rand(3) * TAU
    n1 = np.sin(v[:, 0] * freq + ph[0]) * np.sin(v[:, 1] * freq * 1.13 + ph[1]) * np.sin(v[:, 2] * freq * 0.9 + ph[2])
    n2 = np.sin(v[:, 0] * freq * 2.1 + ph[1]) * np.sin(v[:, 2] * freq * 1.7 + ph[0])
    out = dict(g)
    out["v"] = cen + d * (1 + amp * n1 + amp * 0.4 * n2)[:, None]
    return out


def hang(p, radial, lean=0.25):
    """frame for a charm hanging from p: local +Z points down (leaning outwards along `radial`), +Y = radial"""
    r = np.array(radial, float)
    r /= np.linalg.norm(r)
    return frame(p, np.array((0.0, 0.0, -1.0)) + r * lean, r)


def g_plank(path, width, thick, up=(0, 0, 1), taper=None):
    """rectangular board swept along a path: width across (binormal), thickness along the path normal (`up` side)"""
    P, tang, N, Bn = mk._frames(path, False, up)
    m = len(P)
    verts = []
    for i in range(m):
        w = width / 2 * (taper[i] if taper is not None else 1.0)
        verts += [P[i] - Bn[i] * w, P[i] + Bn[i] * w, P[i] + Bn[i] * w + N[i] * thick, P[i] - Bn[i] * w + N[i] * thick]
    faces = []
    for i in range(m - 1):
        for k in range(4):
            k2 = (k + 1) % 4
            faces.append((i * 4 + k, (i + 1) * 4 + k, (i + 1) * 4 + k2, i * 4 + k2))
    faces.append((0, 1, 2, 3))
    faces.append(((m - 1) * 4 + 3, (m - 1) * 4 + 2, (m - 1) * 4 + 1, (m - 1) * 4))
    return geo(np.array(verts), faces)


# ---------------------------------------------------------------------------------------------- scars / gauntlets
def welt(c, name, pts, src, width=0.012, cls="emis", only=None, step=0.012, closed=False, sink=0.0, n=5):
    """Raised round welt on the skin following guide points (half buried). Weights copied from the body."""
    G = [np.array(p, float) for p in pts]
    if closed:
        G = G + [G[0]]
    P = resample(G, step)
    P = wrap_path(c, P, -sink)
    m = len(P)
    if closed:
        P = P[:-1]
        rad = width / 2
    else:
        rad = list((width / 2) * np.clip(np.sin(np.linspace(0.10, math.pi - 0.10, m)) * 1.6, 0.25, 1.0))
    o = add(name, g_tube(P, rad, n=n, closed=closed), cls, src, sharp=180)
    w_transfer(o, c.ref, smooth=0, only=only)
    return o


def crack_paths(start, direction, length, seed=1, amp=0.016, seg=0.032, branches=2, side_hint=(0, 0, 1)):
    """jagged main line + short branches (cooling-metal crack). Returns list of point lists."""
    rng = np.random.RandomState(seed)
    d = np.array(direction, float)
    d /= np.linalg.norm(d)
    s = np.cross(d, np.array(side_hint, float))
    if np.linalg.norm(s) < 1e-4:
        s = np.cross(d, np.array((0.0, 1.0, 0.0)))
    s /= np.linalg.norm(s)
    start = np.array(start, float)
    n = max(2, int(round(length / seg)))
    main = [start]
    for i in range(1, n + 1):
        main.append(start + d * seg * i + s * (rng.rand() - 0.5) * 2 * amp)
    out = [main]
    for b in range(branches):
        i = 1 + int(rng.rand() * (n - 2)) if n > 2 else 1
        sg = 1 if rng.rand() > 0.5 else -1
        bd = d * 0.6 + s * sg * 0.8
        bd /= np.linalg.norm(bd)
        bl = length * (0.22 + 0.2 * rng.rand())
        k = max(2, int(round(bl / seg)))
        out.append([main[i] + bd * seg * j + d * (rng.rand() - 0.5) * amp * (j > 0) for j in range(k + 1)])
    return out


def cracks(c, name, start, direction, length, src, width=0.012, **kw):
    objs = []
    for i, pth in enumerate(crack_paths(start, direction, length, **kw)):
        objs.append(welt(c, "%s_%d" % (name, i), pth, src, width=width * (1.0 if i == 0 else 0.75)))
    return objs


def g_shell_plate(x0, x1, hw0, hw1, zc, bulge=0.006, drop=0.006, yc=-0.004, nu=7, nv=5, arch=0.0):
    """curved plate over the back of the hand (hand-local: x along, y across, z dorsal)"""
    def fn(U, V):
        x = x0 + (x1 - x0) * V
        hw = hw0 + (hw1 - hw0) * V
        t = U * 2 - 1
        y = yc + t * hw
        z = zc + bulge * (1 - t * t) - drop * t * t + arch * math.pi * 0 + arch * np.sin(V * math.pi)
        return np.stack([x, y, z], axis=-1)
    return g_grid(fn, nu, nv, close_u=False)


def loop_yz(x, ys, zs, n=6, width=0.03, thick=0.005):
    """flat band circling the hand at station x through (y, z) corner points"""
    return g_tube([(x, y, z) for y, z in zip(ys, zs)], 1.0, n=n, closed=True, sx=width / 2, sy=thick / 2, up=(1, 0, 0))


def half_cuff(x0, x1, r0, r1, yc=-0.004, zc=-0.008, arc=0.56, n=10, lip=0.0):
    """dorsal half-pipe guard behind the wrist (axis along the hand's x): radius r0 at x0 (rear) -> r1 at x1"""
    prof = [(r0 + lip, x0 - 0.004), (r0, x0), (r1, x1)] if lip else [(r0, x0), (r1, x1)]
    return gxf(g_lathe(prof, n=n, arc=arc, start=0.5 - arc / 2), T(0, yc, zc) @ R('Y', 90))


def mirror_y(g):
    return gxf(g, S(1, -1, 1))


def report_unweighted(fix_bone=None):
    """log parts that contain vertices without any bone weight (the glTF exporter would add a 'neutral_bone' for them);
    optionally bind those vertices to fix_bone"""
    bad = 0
    for o in mk.PARTS:
        vg = {g.index: g.name for g in o.vertex_groups}
        idx = [v.index for v in o.data.vertices if sum(g.weight for g in v.groups if g.group in vg) <= 1e-6]
        if idx:
            bad += len(idx)
            co = get_co(o)[idx]
            log("UNWEIGHTED %s: %d verts, bbox %s .. %s" % (o.name, len(idx), np.round(co.min(0), 3), np.round(co.max(0), 3)))
            if fix_bone:
                g = o.vertex_groups.get(fix_bone) or o.vertex_groups.new(name=fix_bone)
                g.add(idx, 1.0, 'REPLACE')
    return bad
