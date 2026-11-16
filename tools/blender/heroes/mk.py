"""Procedural modelling toolkit for the hero/NPC builds: geometry generators, weights, body shells."""
import bpy, bmesh, math
import numpy as np
from mathutils import Vector, Matrix
from mathutils import interpolate as mu_interp
from rigio import *

PI = math.pi
TAU = 2 * math.pi

PARTS = []      # mesh objects of the character under construction
ARM = None


def begin(arm):
    global ARM
    ARM = arm
    PARTS.clear()


# ============================================================================================ matrices
def T(x=0, y=0, z=0):
    m = np.eye(4)
    m[:3, 3] = (x, y, z)
    return m


def S(x=1, y=None, z=None):
    if y is None:
        y = x
    if z is None:
        z = x
    return np.diag([x, y, z, 1.0])


def R(axis, deg):
    return np.array(Matrix.Rotation(math.radians(deg), 4, axis))


def frame(origin, zdir, ydir=(0, 1, 0)):
    """4x4 with local +Z along zdir, local +Y as close as possible to ydir."""
    z = np.array(zdir, dtype=float)
    z /= np.linalg.norm(z)
    y = np.array(ydir, dtype=float)
    y = y - z * np.dot(y, z)
    if np.linalg.norm(y) < 1e-6:
        y = np.array((1.0, 0, 0)) - z * z[0]
    y /= np.linalg.norm(y)
    x = np.cross(y, z)
    m = np.eye(4)
    m[:3, 0] = x
    m[:3, 1] = y
    m[:3, 2] = z
    m[:3, 3] = origin
    return m


def xf(V, M):
    V = np.asarray(V, dtype=float)
    return V @ M[:3, :3].T + M[:3, 3]


def geo(V, F, luv=None):
    return {"v": np.asarray(V, dtype=float).reshape(-1, 3), "f": [tuple(f) for f in F], "luv": luv}


def gxf(g, M):
    out = dict(g)
    out["v"] = xf(g["v"], M)
    if np.linalg.det(M[:3, :3]) < 0:
        out["f"] = [tuple(reversed(f)) for f in g["f"]]
    return out


def gjoin(*gs):
    V = []
    F = []
    L = []
    off = 0
    any_luv = any(g.get("luv") is not None for g in gs)
    for g in gs:
        V.append(g["v"])
        F += [tuple(i + off for i in f) for f in g["f"]]
        if any_luv:
            L.append(g["luv"] if g.get("luv") is not None else np.zeros((len(g["v"]), 2)))
        off += len(g["v"])
    return {"v": np.concatenate(V), "f": F, "luv": np.concatenate(L) if any_luv else None}


# ============================================================================================ generators
def spl(x, pts):
    """smooth (monotone-ish cubic) interpolation of control points [(x, y), ...] at array x"""
    xs = np.array([p[0] for p in pts], dtype=float)
    ys = np.array([p[1] for p in pts], dtype=float)
    x = np.clip(np.asarray(x, dtype=float), xs[0], xs[-1])
    if len(xs) < 3:
        return np.interp(x, xs, ys)
    # catmull-rom tangents (finite differences), hermite evaluation
    m = np.zeros_like(ys)
    m[1:-1] = (ys[2:] - ys[:-2]) / (xs[2:] - xs[:-2])
    m[0] = (ys[1] - ys[0]) / (xs[1] - xs[0])
    m[-1] = (ys[-1] - ys[-2]) / (xs[-1] - xs[-2])
    i = np.clip(np.searchsorted(xs, x, side='right') - 1, 0, len(xs) - 2)
    h = xs[i + 1] - xs[i]
    t = (x - xs[i]) / h
    h00 = 2 * t**3 - 3 * t**2 + 1
    h10 = t**3 - 2 * t**2 + t
    h01 = -2 * t**3 + 3 * t**2
    h11 = t**3 - t**2
    return h00 * ys[i] + h10 * h * m[i] + h01 * ys[i + 1] + h11 * h * m[i + 1]


def g_grid(fn, nu, nv, close_u=True, cap0=False, cap1=False, u0=0.0, u1=1.0):
    """Parametric surface. fn(U, V) -> (nv, nu, 3). u in [u0,u1) (closed) or [u0,u1] (open), v in [0,1]."""
    us = np.linspace(u0, u1, nu, endpoint=not close_u)
    vs = np.linspace(0, 1, nv)
    U, Vv = np.meshgrid(us, vs)
    P = np.asarray(fn(U, Vv), dtype=float)
    verts = P.reshape(-1, 3)
    luv = np.stack([U.ravel(), Vv.ravel()], axis=1)
    faces = []
    nui = nu if close_u else nu - 1
    for j in range(nv - 1):
        for i in range(nui):
            a = j * nu + i
            b = j * nu + (i + 1) % nu
            faces.append((a, b, b + nu, a + nu))
    verts = list(verts)
    luv = list(luv)
    if cap0:
        c = np.mean(P[0], axis=0)
        verts.append(c)
        luv.append((0.5, 0.0))
        ci = len(verts) - 1
        for i in range(nui):
            faces.append((ci, (i + 1) % nu, i))
    if cap1:
        c = np.mean(P[-1], axis=0)
        verts.append(c)
        luv.append((0.5, 1.0))
        ci = len(verts) - 1
        o = (nv - 1) * nu
        for i in range(nui):
            faces.append((ci, o + i, o + (i + 1) % nu))
    return geo(np.array(verts), faces, np.array(luv))


def ell(U, cx, cy, rx, ryf, ryb=None, p=2.0):
    """Ellipse / superellipse section in the XY plane. u=0 -> +X (character's left), 0.25 -> +Y (back),
    0.5 -> -X (right), 0.75 -> -Y (front). ryf/ryb = front/back radii."""
    if ryb is None:
        ryb = ryf
    a = U * TAU
    c = np.cos(a)
    s = np.sin(a)
    e = 2.0 / p
    ry = np.where(s < 0, ryf, ryb)
    x = cx + rx * np.sign(c) * np.abs(c) ** e
    y = cy + ry * np.sign(s) * np.abs(s) ** e
    return x, y


def g_stations(st, nu=24, nv=12, p=2.0, close=True, u0=0.0, u1=1.0, cap0=False, cap1=False, mod=None):
    """Garment-like loft around the Z axis through stations [(z, cx, cy, rx, ry_front, ry_back), ...] (smoothly
    interpolated). `mod(U, V, x, y, z)` may return modified (x, y, z)."""
    st = sorted(st, key=lambda s: s[0])
    zs = [s[0] for s in st]
    z0, z1 = zs[0], zs[-1]

    def col(i):
        return [(s[0], s[i]) for s in st]

    def fn(U, V):
        z = z0 + (z1 - z0) * V
        cx = spl(z, col(1))
        cy = spl(z, col(2))
        rx = spl(z, col(3))
        ryf = spl(z, col(4))
        ryb = spl(z, col(5))
        x, y = ell(U, cx, cy, rx, ryf, ryb, p)
        if mod is not None:
            x, y, z = mod(U, V, x, y, z)
        return np.stack([x, y, z], axis=-1)
    return g_grid(fn, nu, nv, close_u=close, u0=u0, u1=u1, cap0=cap0, cap1=cap1)


def g_lathe(profile, n=16, sx=1.0, sy=1.0, arc=1.0, start=0.0):
    """Surface of revolution about Z. profile = [(r, z), ...]; r == 0 collapses to a pole vertex.
    arc < 1 gives an open partial revolution starting at fraction `start`."""
    verts = []
    faces = []
    rows = []
    closed = arc >= 0.999
    nn = n if closed else n + 1
    for (r, z) in profile:
        if r <= 1e-7:
            rows.append([len(verts)])
            verts.append((0, 0, z))
        else:
            row = []
            for i in range(nn):
                a = TAU * (start + arc * i / n)
                row.append(len(verts))
                verts.append((r * math.cos(a) * sx, r * math.sin(a) * sy, z))
            rows.append(row)
    for j in range(len(rows) - 1):
        a, b = rows[j], rows[j + 1]
        cnt = n if closed else n
        for i in range(cnt):
            i2 = (i + 1) % nn if closed else i + 1
            if len(a) == 1 and len(b) == 1:
                continue
            if len(a) == 1:
                faces.append((a[0], b[i2], b[i]))
            elif len(b) == 1:
                faces.append((a[i], a[i2], b[0]))
            else:
                faces.append((a[i], a[i2], b[i2], b[i]))
    return geo(verts, faces)


def _frames(path, closed=False, up=(0, 0, 1)):
    P = np.asarray(path, dtype=float)
    n = len(P)
    tang = np.zeros_like(P)
    for i in range(n):
        if closed:
            d = P[(i + 1) % n] - P[(i - 1) % n]
        else:
            d = P[min(i + 1, n - 1)] - P[max(i - 1, 0)]
        tang[i] = d / max(np.linalg.norm(d), 1e-9)
    up = np.array(up, dtype=float)
    nrm = up - tang[0] * np.dot(up, tang[0])
    if np.linalg.norm(nrm) < 1e-4:
        nrm = np.array((1.0, 0, 0)) - tang[0] * tang[0][0]
    nrm /= np.linalg.norm(nrm)
    N = [nrm]
    for i in range(1, n):
        v = N[-1] - tang[i] * np.dot(N[-1], tang[i])
        v /= max(np.linalg.norm(v), 1e-9)
        N.append(v)
    N = np.array(N)
    B = np.cross(tang, N)
    return P, tang, N, B


def g_tube(path, radius, n=8, closed=False, caps=True, up=(0, 0, 1), sx=1.0, sy=1.0, twist=0.0):
    """Tube swept along a path (parallel-transport frames). radius: float or per-point list.
    sx/sy scale the section along the normal/binormal. """
    P, tang, N, B = _frames(path, closed, up)
    m = len(P)
    rad = np.full(m, radius, dtype=float) if np.isscalar(radius) else np.asarray(radius, dtype=float)
    verts = []
    for i in range(m):
        tw = twist * i / max(m - 1, 1)
        for k in range(n):
            a = TAU * k / n + tw
            verts.append(P[i] + (N[i] * math.cos(a) * sx + B[i] * math.sin(a) * sy) * rad[i])
    faces = []
    segs = m if closed else m - 1
    for i in range(segs):
        i2 = (i + 1) % m
        for k in range(n):
            k2 = (k + 1) % n
            faces.append((i * n + k, i * n + k2, i2 * n + k2, i2 * n + k))
    if caps and not closed:
        verts.append(P[0])
        c0 = len(verts) - 1
        verts.append(P[-1])
        c1 = len(verts) - 1
        for k in range(n):
            k2 = (k + 1) % n
            faces.append((c0, k2, k))
            faces.append((c1, (m - 1) * n + k, (m - 1) * n + k2))
    return geo(verts, faces)


def g_box(sx, sy, sz, center=(0, 0, 0)):
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    v = [(-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
         (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz)]
    f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    return geo(np.array(v) + np.array(center), f)


def g_prism(outline, depth, taper=1.0):
    """Extrude a 2D outline (list of (x, z)) along Y by `depth` (centred). Result lies in the XZ plane.
    taper scales the back/front outline is not used (kept 1) - bevel modifier gives the edge."""
    n = len(outline)
    h = depth / 2
    v = [(x, -h, z) for (x, z) in outline] + [(x, h, z) for (x, z) in outline]
    f = [tuple(range(n)), tuple(reversed(range(n, 2 * n)))]
    for i in range(n):
        j = (i + 1) % n
        f.append((i, i + n, j + n, j))
    return geo(v, f)


def g_blade(stations, ridge=1.0):
    """Double-edged blade along +Z with diamond section. stations = [(z, half_width, thickness), ...];
    width along X, thickness along Y. Last station may have half_width 0 (point)."""
    verts = []
    rows = []
    for (z, w, t) in stations:
        if w <= 1e-6:
            rows.append([len(verts)])
            verts.append((0, 0, z))
        else:
            rows.append(list(range(len(verts), len(verts) + 4)))
            verts += [(w, 0, z), (0, t / 2 * ridge, z), (-w, 0, z), (0, -t / 2 * ridge, z)]
    faces = []
    for j in range(len(rows) - 1):
        a, b = rows[j], rows[j + 1]
        for i in range(4):
            i2 = (i + 1) % 4
            if len(a) == 4 and len(b) == 4:
                faces.append((a[i], a[i2], b[i2], b[i]))
            elif len(a) == 4:
                faces.append((a[i], a[i2], b[0]))
            elif len(b) == 4:
                faces.append((a[0], b[i2], b[i]))
    if len(rows[0]) == 4:
        faces.append(tuple(reversed(rows[0])))
    if len(rows[-1]) == 4:
        faces.append(tuple(rows[-1]))
    return geo(verts, faces)


def g_sphere(r=1.0, nu=12, nv=8, sx=1, sy=1, sz=1):
    prof = [(0, -r)] + [(r * math.sin(PI * j / nv), -r * math.cos(PI * j / nv)) for j in range(1, nv)] + [(0, r)]
    g = g_lathe(prof, nu)
    g["v"] = g["v"] * np.array((sx, sy, sz))
    return g


def g_gem(r=0.05, h=0.12, n=6, top=0.6):
    """Faceted crystal along Z, centred at origin: bottom point, girdle, top point."""
    return g_lathe([(0, -h * (1 - top)), (r, 0), (r * 0.55, h * top * 0.7), (0, h * top)], n)


# ============================================================================================ objects
def add(name, g, cls, src, owner="body", bone=None, smooth=True, sharp=40, bevel=0.0, bevel_seg=1, subsurf=0,
        solidify=0.0, solid_offset=1.0, recalc=True, double_sided=False):
    """Create a part object from generator output `g`.
    cls: material class (cloth/leather/metal/bone/emis/skin/hair), src: source material key (see mats.py)."""
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(map(float, v)) for v in g["v"]], [], g["f"])
    me.update()
    if g.get("luv") is not None:
        a = me.attributes.new("luv", 'FLOAT2', 'POINT')
        a.data.foreach_set('vector', np.asarray(g["luv"], dtype=np.float32).ravel())
    if recalc:
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    obj["cls"] = cls
    obj["src"] = src
    obj["owner"] = owner
    obj["sharp"] = float(sharp)
    obj["ds"] = bool(double_sided)
    rebind(obj, ARM)
    if subsurf:
        m = obj.modifiers.new("sub", 'SUBSURF')
        m.levels = subsurf
        m.render_levels = subsurf
    if solidify:
        m = obj.modifiers.new("solid", 'SOLIDIFY')
        m.thickness = solidify
        m.offset = solid_offset
        m.use_even_offset = True
        m.use_quality_normals = True
    if bevel:
        m = obj.modifiers.new("bevel", 'BEVEL')
        m.width = bevel
        m.segments = bevel_seg
        m.limit_method = 'ANGLE'
        m.angle_limit = math.radians(30)
        m.harden_normals = False
    # keep armature modifier last
    am = next(m for m in obj.modifiers if m.type == 'ARMATURE')
    while obj.modifiers[-1] != am:
        idx = list(obj.modifiers).index(am)
        obj.modifiers.move(idx, len(obj.modifiers) - 1)
    if bone:
        w_rigid(obj, bone)
    PARTS.append(obj)
    return obj


def apply_mods(obj):
    """Apply every modifier except the armature."""
    mods = [m for m in obj.modifiers if m.type != 'ARMATURE']
    if not mods:
        return obj
    arm_mods = [m for m in obj.modifiers if m.type == 'ARMATURE']
    for m in arm_mods:
        m.show_viewport = False
    dg = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
    old = obj.data
    me.name = old.name
    for m in mods:
        obj.modifiers.remove(m)
    obj.data = me
    for m in arm_mods:
        m.show_viewport = True
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return obj


def mirrored(obj, name=None):
    """Mirror a part across X (new object); swaps _l/_r vertex groups."""
    apply_mods(obj)
    me = obj.data.copy()
    co = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get('co', co)
    co = co.reshape(-1, 3)
    co[:, 0] *= -1
    me.vertices.foreach_set('co', co.ravel())
    me.flip_normals()
    me.update()
    nm = name or (obj.name[:-2] + "_r" if obj.name.endswith("_l") else obj.name + "_m")
    o2 = bpy.data.objects.new(nm, me)
    me.name = nm
    bpy.context.scene.collection.objects.link(o2)
    for k in ("cls", "src", "owner", "sharp", "ds"):
        o2[k] = obj[k]
    rebind(o2, ARM)
    # vertex group names live in the mesh datablock (copied above); rename them in place (same indices)
    names = [g.name for g in obj.vertex_groups]
    if len(o2.vertex_groups) == 0:
        for n in names:
            o2.vertex_groups.new(name=n)
    assert [g.name for g in o2.vertex_groups] == names, (names, [g.name for g in o2.vertex_groups])
    for i, g in enumerate(o2.vertex_groups):
        g.name = "__tmp_%d" % i
    for i, g in enumerate(o2.vertex_groups):
        n = names[i]
        if n.endswith("_l"):
            n = n[:-2] + "_r"
        elif n.endswith("_r"):
            n = n[:-2] + "_l"
        g.name = n
    PARTS.append(o2)
    return o2


def remove_part(obj):
    if obj in PARTS:
        PARTS.remove(obj)
    bpy.data.objects.remove(obj, do_unlink=True)


# ============================================================================================ weights
def w_rigid(obj, bone):
    assert bone in ARM.data.bones, bone
    obj.vertex_groups.clear()
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    return obj


def mesh_edges(obj):
    me = obj.data
    e = np.empty(len(me.edges) * 2, dtype=np.int32)
    me.edges.foreach_get('vertices', e)
    return e.reshape(-1, 2)


def laplacian(X, edges, iters=1, fac=0.5, pinned=None):
    X = np.array(X, dtype=np.float64)
    n = len(X)
    deg = np.zeros(n)
    np.add.at(deg, edges[:, 0], 1)
    np.add.at(deg, edges[:, 1], 1)
    deg[deg == 0] = 1
    for _ in range(iters):
        acc = np.zeros_like(X)
        np.add.at(acc, edges[:, 0], X[edges[:, 1]])
        np.add.at(acc, edges[:, 1], X[edges[:, 0]])
        avg = acc / deg[:, None]
        Xn = X + (avg - X) * fac
        if pinned is not None:
            Xn[pinned] = X[pinned]
        X = Xn
    return X


_WCACHE = {}


def src_weights(src):
    k = src.name
    if k not in _WCACHE:
        names = [b.name for b in ARM.data.bones]
        W, _ = get_weights(src, names)
        _WCACHE[k] = (W, names)
    return _WCACHE[k]


def w_transfer(obj, src, smooth=3, limit=4, only=None, remap=None):
    """Copy weights from the nearest surface point of `src` (interpolated), then smooth.
    only: optional list of bone names to keep (others dropped, renormalised).
    remap: optional {bone: bone} to fold weights (e.g. fingers -> hand)."""
    if any(m.type != 'ARMATURE' for m in src.modifiers):
        apply_mods(src)
        _WCACHE.pop(src.name, None)
    W, names = src_weights(src)
    sme = src.data
    sco = get_co(src)
    co = get_co(obj)
    out = np.zeros((len(co), len(names)))
    for i, c in enumerate(co):
        ok, loc, nrm, fi = src.closest_point_on_mesh(Vector(c))
        if not ok:
            continue
        vids = list(sme.polygons[fi].vertices)
        ws = mu_interp.poly_3d_calc([Vector(sco[v]) for v in vids], loc)
        for v, w in zip(vids, ws):
            out[i] += W[v] * w
    col = {n: i for i, n in enumerate(names)}
    if remap:
        for a, b in remap.items():
            if a in col:
                out[:, col[b]] += out[:, col[a]]
                out[:, col[a]] = 0
    if only is not None:
        keep = np.zeros(len(names), dtype=bool)
        for n in only:
            keep[col[n]] = True
        out[:, ~keep] = 0
        e = mesh_edges(obj)
        for _ in range(12):
            zero = out.sum(1) < 1e-4
            if not zero.any():
                break
            sm = laplacian(out, e, 1, 1.0)
            out[zero] = sm[zero]
        zero = out.sum(1) < 1e-4
        if zero.any():
            out[zero, col[only[0]]] = 1.0
    if smooth:
        out = laplacian(out, mesh_edges(obj), smooth, 0.5)
    set_weights(obj, out, names, limit)
    return obj


def w_func(obj, fn, limit=4, smooth=0):
    """fn(co (N,3)) -> {bone: weights array}"""
    co = get_co(obj)
    d = fn(co)
    names = list(d.keys())
    for n in names:
        assert n in ARM.data.bones, n
    W = np.stack([np.broadcast_to(np.asarray(d[n], dtype=float), (len(co),)) for n in names], axis=1)
    W = np.clip(W, 0, None)
    if smooth:
        W = laplacian(W, mesh_edges(obj), smooth, 0.5)
    set_weights(obj, W, names, limit)
    return obj


def w_get(obj):
    names = [b.name for b in ARM.data.bones]
    W, _ = get_weights(obj, names)
    return W, names


def w_mix(obj, fn):
    """Modify existing weights: fn(W, col, co) -> W"""
    W, names = w_get(obj)
    col = {n: i for i, n in enumerate(names)}
    W = fn(W.astype(np.float64), col, get_co(obj))
    set_weights(obj, W, names, 4)
    return obj


def sstep(x, a, b):
    t = np.clip((np.asarray(x, dtype=float) - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


# ============================================================================================ body
BONES_ARM = ("clavicle", "upperarm", "lowerarm")
BONES_HAND = ("hand", "index_", "middle_", "ring_", "pinky_", "thumb_")
BONES_LEG = ("thigh", "calf")
BONES_FOOT = ("foot", "ball")
BONES_TORSO = ("pelvis", "spine_01", "spine_02", "spine_03")
BONES_HEAD = ("neck_01", "Head")


class Body:
    """Reference body (fitted to the UAL rig) used for shells, weight transfer and measurements."""

    def __init__(self, obj):
        self.obj = obj
        self.names = [b.name for b in ARM.data.bones]
        self.W, _ = get_weights(obj, self.names)
        s = self.W.sum(1, keepdims=True)
        s[s == 0] = 1
        self.W = self.W / s
        self.col = {n: i for i, n in enumerate(self.names)}
        self.refresh()

    def refresh(self):
        self.co = get_co(self.obj)
        me = self.obj.data
        n = np.empty(len(me.vertices) * 3, dtype=np.float32)
        me.vertices.foreach_get('normal', n)
        self.nrm = n.reshape(-1, 3).astype(np.float64)
        self.x, self.y, self.z = self.co[:, 0], self.co[:, 1], self.co[:, 2]
        self.edges = mesh_edges(self.obj)

    def w(self, *pats):
        """summed weight of all bones whose name starts with any of the given prefixes (or equals it)"""
        idx = [i for i, n in enumerate(self.names) if any(n == p or n.startswith(p) for p in pats)]
        return self.W[:, idx].sum(1)

    def grow(self, mask, n=1):
        m = mask.copy()
        for _ in range(n):
            a = m[self.edges[:, 0]] | m[self.edges[:, 1]]
            m2 = m.copy()
            m2[self.edges[a, 0]] = True
            m2[self.edges[a, 1]] = True
            m = m2
        return m

    def shrink(self, mask, n=1):
        return ~self.grow(~mask, n)

    def slice(self, z, dz=0.015, arms=False):
        """(cx, cy, rx, ry_front, ry_back) bounding ellipse of the torso/legs around height z"""
        m = np.abs(self.z - z) < dz
        if not arms:
            m &= self.w(*BONES_ARM, *BONES_HAND) < 0.5
        if not m.any():
            return None
        x0, x1 = self.x[m].min(), self.x[m].max()
        y0, y1 = self.y[m].min(), self.y[m].max()
        cy = (y0 + y1) / 2
        return ((x0 + x1) / 2, cy, (x1 - x0) / 2, cy - y0, y1 - cy)


def shell(body, mask, offset, name, cls, src, smooth=0, smooth_fac=0.5, pin_boundary=False, min_clear=0.75, **kw):
    """New part made from the body faces whose vertices are all in `mask`, pushed out along normals.
    Keeps the body's skin weights. smooth = Laplacian iterations on the offset shell."""
    me = body.obj.data
    vid = np.nonzero(mask)[0]
    remap = -np.ones(len(body.co), dtype=np.int64)
    remap[vid] = np.arange(len(vid))
    faces = []
    for p in me.polygons:
        vs = p.vertices
        if all(mask[v] for v in vs):
            faces.append(tuple(int(remap[v]) for v in vs))
    used = np.zeros(len(vid), dtype=bool)
    for f in faces:
        used[list(f)] = True
    # drop unused verts
    keep = np.nonzero(used)[0]
    rm2 = -np.ones(len(vid), dtype=np.int64)
    rm2[keep] = np.arange(len(keep))
    faces = [tuple(int(rm2[i]) for i in f) for f in faces]
    vid = vid[keep]
    off = offset(body.co[vid]) if callable(offset) else offset
    if not np.isscalar(off):
        off = np.asarray(off)[:, None]
    V = body.co[vid] + body.nrm[vid] * off
    obj = add(name, geo(V, faces), cls, src, recalc=False, **kw)
    if smooth:
        e = mesh_edges(obj)
        pinned = None
        if pin_boundary:
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            pinned = np.array([v.index for v in bm.verts if v.is_boundary], dtype=np.int64)
            bm.free()
        sm_co = laplacian(get_co(obj), e, smooth, smooth_fac, pinned)
        # smoothing shrinks convex areas (elbows, shoulders, knees): keep a minimum clearance above the body
        offv = np.full(len(vid), off) if np.isscalar(off) else np.asarray(off).reshape(-1)
        d = ((sm_co - body.co[vid]) * body.nrm[vid]).sum(1)
        lack = np.clip(offv * min_clear - d, 0, None)
        sm_co = sm_co + body.nrm[vid] * lack[:, None]
        set_co(obj, sm_co)
    set_weights(obj, body.W[vid], body.names, 4)
    obj["src_vid"] = [int(v) for v in vid] if len(vid) < 20000 else []
    return obj


def trim_skin(skin, body, hide_mask, shrink=1):
    """Delete skin faces hidden under clothing. `skin` must still have the same vertex order as body.obj."""
    m = body.shrink(hide_mask, shrink) if shrink else hide_mask
    bm = bmesh.new()
    bm.from_mesh(skin.data)
    bm.verts.ensure_lookup_table()
    dead = [f for f in bm.faces if all(m[v.index] for v in f.verts)]
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(skin.data)
    bm.free()
    skin.data.update()
    return skin


def displace_noise(obj, strength, scale, seed=0, along_normal=True):
    """cheap value-noise displacement (deterministic) along vertex normals"""
    co = get_co(obj)
    me = obj.data
    n = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get('normal', n)
    n = n.reshape(-1, 3)
    from mathutils import noise as mnoise
    d = np.array([mnoise.noise(Vector(c) * scale + Vector((seed * 7.3, seed * 3.1, seed * 1.7))) for c in co])
    if along_normal:
        co = co + n * (d[:, None] * strength)
    else:
        co = co + d[:, None] * strength
    set_co(obj, co)
    return obj


def tri_count(obj):
    me = obj.data
    return int(sum(len(p.vertices) - 2 for p in me.polygons))
