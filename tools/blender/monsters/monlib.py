"""Shared modelling / skinning / baking / export toolkit for the Ashen Abyss monsters (Blender 5.2, headless).

Everything is built in armature space of the unmodified UAL rig (T-pose, facing -Y, Z up, metres).
A creature is a list of `Part`s (numpy verts + faces) that are either rigid (bound 100% to one bone) or
skinned (dense weight matrix).  `Creature.finalize()` builds the Blender objects, unwraps to one shared
atlas, bakes procedural looks (looks.py) into base-colour / ORM / normal textures and exports a .glb.
"""
import bpy, bmesh, math, os, sys, json, time
import numpy as np
from mathutils import Vector, Matrix
from mathutils import noise as mnoise
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = r"Y:\assets_src\q"
UAL1 = os.path.join(SRC, "UAL1.glb")
UAL2 = os.path.join(SRC, "UAL2.glb")
OUT_DIR = r"Y:\game\assets\chars"
SHOT_DIR = r"Y:\shots\chars"
CACHE = os.path.join(HERE, "cache")
WORK = os.path.join(HERE, "out")
TEXDIR = os.path.join(WORK, "tex")
for _d in (CACHE, WORK, TEXDIR, SHOT_DIR):
    os.makedirs(_d, exist_ok=True)


def log(*a):
    print("[mon]", *a)
    sys.stdout.flush()


# ------------------------------------------------------------------------------------------------ rig
class Rig:
    def __init__(self, arm):
        self.arm = arm
        bones = arm.data.bones
        self.names = [b.name for b in bones]
        self.idx = {n: i for i, n in enumerate(self.names)}
        self.nb = len(self.names)
        self.head = {b.name: np.array(b.head_local) for b in bones}
        self.tail = {b.name: np.array(b.tail_local) for b in bones}
        self.mat = {b.name: np.array(b.matrix_local) for b in bones}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in bones}
        mi = []
        for n in self.names:
            if n.endswith('_l'):
                m = n[:-2] + '_r'
            elif n.endswith('_r'):
                m = n[:-2] + '_l'
            else:
                m = n
            mi.append(self.idx[m])
        self.mirror_idx = np.array(mi)

    def H(self, name):
        return self.head[name].copy()

    def T(self, name):
        return self.tail[name].copy()

    def chain(self, name):
        """bone + all descendants"""
        out = [name]
        for n in self.names:
            p = self.parent[n]
            while p is not None:
                if p == name:
                    out.append(n)
                    break
                p = self.parent[p]
        return out

    def ids(self, names):
        return [self.idx[n] for n in names]


RIG = None  # set by load_rig()


def mirror_name(n):
    if n is None:
        return None
    if n.endswith('_l'):
        return n[:-2] + '_r'
    if n.endswith('_r'):
        return n[:-2] + '_l'
    return n


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 30
    return bpy.context.scene


def import_gltf(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path, disable_bone_shape=True)
    return [o for o in bpy.data.objects if o not in before]


def load_rig():
    """Canonical UAL armature, untouched; no mannequin, no actions."""
    global RIG
    objs = import_gltf(UAL1)
    arm = next(o for o in objs if o.type == 'ARMATURE')
    for o in objs:
        if o is not arm:
            bpy.data.objects.remove(o, do_unlink=True)
    if arm.animation_data:
        arm.animation_data_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)
    arm.name = "Armature"
    arm.data.name = "Armature"
    for pb in arm.pose.bones:
        pb.custom_shape = None
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)
    for m in list(bpy.data.materials):
        if m.users == 0:
            bpy.data.materials.remove(m)
    RIG = Rig(arm)
    return arm


# ------------------------------------------------------------------------------------------------ math helpers
def V(*a):
    if len(a) == 1:
        return np.array(a[0], dtype=np.float64)
    return np.array(a, dtype=np.float64)


def unit(v):
    v = np.asarray(v, dtype=np.float64)
    n = np.linalg.norm(v)
    return v / n if n > 1e-12 else v


def lerp(a, b, t):
    return np.asarray(a, dtype=np.float64) * (1 - t) + np.asarray(b, dtype=np.float64) * t


def smoothstep(e0, e1, x):
    t = np.clip((np.asarray(x, dtype=np.float64) - e0) / (e1 - e0 if e1 != e0 else 1e-9), 0, 1)
    return t * t * (3 - 2 * t)


def rot_axis(axis, deg):
    """3x3 rotation about axis ('x','y','z' or vector)"""
    if isinstance(axis, str):
        axis = {'x': (1, 0, 0), 'y': (0, 1, 0), 'z': (0, 0, 1)}[axis]
    return np.array(Matrix.Rotation(math.radians(deg), 3, Vector(axis)))


def rot_to(a, b):
    """3x3 rotation taking unit vector a to b"""
    a = Vector(unit(a)); b = Vector(unit(b))
    return np.array(a.rotation_difference(b).to_matrix())


def frame_from(y, zhint=(0, 0, 1)):
    """3x3 matrix whose columns are an orthonormal frame with Y along `y` and Z as close to zhint as possible"""
    y = unit(y)
    z = np.asarray(zhint, dtype=np.float64)
    z = z - y * np.dot(z, y)
    if np.linalg.norm(z) < 1e-6:
        z = np.array([1.0, 0, 0]) - y * y[0]
    z = unit(z)
    x = np.cross(y, z)
    return np.stack([x, y, z], axis=1)


def fbm(p, freq=1.0, seed=0.0, octaves=3):
    q = Vector((p[0] * freq + seed * 13.13, p[1] * freq + seed * 7.71, p[2] * freq + seed * 3.37))
    return mnoise.fractal(q, 1.0, 2.0, octaves)


def bez(p0, p1, p2, p3, n):
    t = np.linspace(0, 1, n)[:, None]
    p0, p1, p2, p3 = (np.asarray(p, dtype=np.float64) for p in (p0, p1, p2, p3))
    return ((1 - t) ** 3) * p0 + 3 * ((1 - t) ** 2) * t * p1 + 3 * (1 - t) * t * t * p2 + (t ** 3) * p3


def polyline(pts, n):
    """resample polyline to n points (Catmull-Rom-ish smooth via cubic interpolation by chord length)"""
    P = np.asarray(pts, dtype=np.float64)
    if len(P) == 2:
        return np.linspace(P[0], P[1], n)
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]
    d /= d[-1]
    t = np.linspace(0, 1, n)
    out = np.zeros((n, 3))
    # catmull-rom
    Pp = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]])
    for k, tt in enumerate(t):
        i = min(np.searchsorted(d, tt, side='right') - 1, len(P) - 2)
        i = max(i, 0)
        u = (tt - d[i]) / max(d[i + 1] - d[i], 1e-9)
        a, b, c, e = Pp[i], Pp[i + 1], Pp[i + 2], Pp[i + 3]
        out[k] = 0.5 * ((2 * b) + (-a + c) * u + (2 * a - 5 * b + 4 * c - e) * u * u + (-a + 3 * b - 3 * c + e) * u ** 3)
    return out


# ------------------------------------------------------------------------------------------------ Part
class Part:
    def __init__(self, v, f, W=None):
        self.v = np.asarray(v, dtype=np.float64).reshape(-1, 3).copy()
        self.f = [tuple(int(i) for i in x) for x in f]
        self.W = W
        self.bone = None
        self.look = None
        self.obj = 'body_main'
        self.smooth = 40.0

    # -- basic
    def copy(self):
        p = Part(self.v, self.f, None if self.W is None else self.W.copy())
        p.bone, p.look, p.obj, p.smooth = self.bone, self.look, self.obj, self.smooth
        return p

    @property
    def ntri(self):
        return sum(len(f) - 2 for f in self.f)

    def tris(self):
        t = []
        for f in self.f:
            for k in range(1, len(f) - 1):
                t.append((f[0], f[k], f[k + 1]))
        return np.array(t, dtype=np.int64).reshape(-1, 3)

    def bbox(self):
        return self.v.min(0), self.v.max(0)

    def center(self):
        mn, mx = self.bbox()
        return (mn + mx) / 2

    # -- transforms
    def tf(self, M):
        M = np.asarray(M, dtype=np.float64)
        if M.shape == (3, 3):
            A = M; t = np.zeros(3)
        else:
            A = M[:3, :3]; t = M[:3, 3]
        self.v = self.v @ A.T + t
        if np.linalg.det(A) < 0:
            self.f = [tuple(reversed(f)) for f in self.f]
        return self

    def move(self, x=0.0, y=0.0, z=0.0):
        if not np.isscalar(x):
            x, y, z = x
        self.v += np.array([x, y, z], dtype=np.float64)
        return self

    def rot(self, axis, deg, pivot=(0, 0, 0)):
        R = rot_axis(axis, deg)
        pv = np.asarray(pivot, dtype=np.float64)
        self.v = (self.v - pv) @ R.T + pv
        return self

    def scale(self, s, pivot=(0, 0, 0)):
        s = np.array([s, s, s], dtype=np.float64) if np.isscalar(s) else np.asarray(s, dtype=np.float64)
        pv = np.asarray(pivot, dtype=np.float64)
        self.v = (self.v - pv) * s + pv
        if s[0] * s[1] * s[2] < 0:
            self.f = [tuple(reversed(f)) for f in self.f]
        return self

    def orient(self, y, zhint=(0, 0, 1), origin=(0, 0, 0)):
        """map local +Y onto direction y (local Z towards zhint) and move to origin"""
        self.v = self.v @ frame_from(y, zhint).T + np.asarray(origin, dtype=np.float64)
        return self

    def mirror(self):
        self.v[:, 0] *= -1
        self.f = [tuple(reversed(f)) for f in self.f]
        self.bone = mirror_name(self.bone)
        if self.W is not None:
            self.W = self.W[:, RIG.mirror_idx]
        return self

    # -- shape
    def normals(self):
        t = self.tris()
        vn = np.zeros_like(self.v)
        if len(t):
            fn = np.cross(self.v[t[:, 1]] - self.v[t[:, 0]], self.v[t[:, 2]] - self.v[t[:, 0]])
            for k in range(3):
                np.add.at(vn, t[:, k], fn)
        l = np.linalg.norm(vn, axis=1)
        l[l < 1e-12] = 1
        return vn / l[:, None]

    def inflate(self, d, mask=None):
        n = self.normals()
        d = np.asarray(d, dtype=np.float64)
        if d.ndim == 0:
            d = np.full(len(self.v), float(d))
        if mask is not None:
            d = d * mask
        self.v += n * d[:, None]
        return self

    def rough(self, amp, freq, seed=0.0, octaves=3, along_normal=True):
        """fractal noise displacement"""
        if along_normal:
            n = self.normals()
            d = np.array([fbm(p, freq, seed, octaves) for p in self.v])
            self.v += n * (d * amp)[:, None]
        else:
            for k in range(3):
                d = np.array([fbm(p, freq, seed + 11.0 * (k + 1), octaves) for p in self.v])
                self.v[:, k] += d * amp
        return self

    def jitter(self, amp, seed=0):
        rng = np.random.default_rng(seed)
        self.v += rng.uniform(-amp, amp, self.v.shape)
        return self

    def displace(self, fn):
        """fn(v (n,3), normals (n,3)) -> delta (n,3)"""
        self.v += fn(self.v, self.normals())
        return self

    def dent(self, center, radius, amount, direction=None, mask=None):
        """gaussian push: along `direction` (vector) or along the vertex normals (amount<0 = inwards)"""
        d = np.linalg.norm(self.v - np.asarray(center, dtype=np.float64), axis=1)
        w = np.exp(-(d / radius) ** 2) * amount
        if mask is not None:
            w = w * mask
        if direction is None:
            self.v += self.normals() * w[:, None]
        else:
            self.v += unit(direction) * w[:, None]
        return self

    def bend(self, axis_len='z', axis_rot='x', deg_per_m=30.0, origin=0.0):
        """bend the part into an arc: cross-sections along axis_len are rotated about axis_rot"""
        ai = 'xyz'.index(axis_len)
        out = np.empty_like(self.v)
        for i, p in enumerate(self.v):
            s = p[ai] - origin
            off = p.copy(); off[ai] = 0.0
            out[i] = _ax(ai) * origin + _arc(ai, axis_rot, s, deg_per_m) + rot_axis(axis_rot, s * deg_per_m) @ off
        self.v = out
        return self

    def to_grip(self, side='r'):
        """weapon-local frame (+Y along the blade/shaft, +X leading edge, origin = centre of the fist) -> hand"""
        G = grip_frame(side)
        self.tf(G)
        self.bone = 'hand_' + side
        self.W = None
        return self

    # -- topology helpers
    def cleanup(self, eps=1e-6):
        q = np.round(self.v / eps).astype(np.int64)
        _, first, inv = np.unique(q, axis=0, return_index=True, return_inverse=True)
        inv = inv.reshape(-1)
        order = np.argsort(first)
        rank = np.empty_like(order); rank[order] = np.arange(len(order))
        newidx = rank[inv]
        self.v = self.v[first[order]]
        if self.W is not None:
            self.W = self.W[first[order]]
        nf = []
        for f in self.f:
            g = []
            for i in f:
                j = int(newidx[i])
                if not g or g[-1] != j:
                    g.append(j)
            if len(g) > 1 and g[0] == g[-1]:
                g.pop()
            if len(set(g)) >= 3 and len(set(g)) == len(g):
                nf.append(tuple(g))
        self.f = nf
        return self

    def to_bm(self):
        bm = bmesh.new()
        vs = [bm.verts.new(p) for p in self.v]
        for f in self.f:
            try:
                bm.faces.new([vs[i] for i in f])
            except ValueError:
                pass
        bm.verts.index_update()
        return bm

    def from_bm(self, bm):
        bm.verts.index_update()
        self.v = np.array([v.co[:] for v in bm.verts], dtype=np.float64).reshape(-1, 3)
        self.f = [tuple(v.index for v in f.verts) for f in bm.faces]
        self.W = None
        bm.free()
        return self

    def recalc(self):
        """make face winding consistent / outward (keeps vertex order and weights)"""
        bm = self.to_bm()
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        self.f = [tuple(v.index for v in f.verts) for f in bm.faces]
        bm.free()
        return self

    def flip(self):
        self.f = [tuple(reversed(f)) for f in self.f]
        return self

    def bevel(self, width, segs=1, angle=30.0, profile=0.5):
        bm = self.to_bm()
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        es = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle() > math.radians(angle)]
        if es:
            bmesh.ops.bevel(bm, geom=es, offset=width, offset_type='OFFSET', segments=segs, profile=profile,
                            affect='EDGES', clamp_overlap=True)
        return self.from_bm(bm)

    def subdiv(self, cuts=1, smooth=0.0):
        bm = self.to_bm()
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True, smooth=smooth)
        return self.from_bm(bm)

    def solidify(self, th):
        bm = self.to_bm()
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=th)
        return self.from_bm(bm)

    def weld(self, dist=1e-4):
        bm = self.to_bm()
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=dist)
        return self.from_bm(bm)

    def tri(self):
        bm = self.to_bm()
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        return self.from_bm(bm)

    # -- selections
    def vmask_faces(self, vmask, mode='all'):
        vmask = np.asarray(vmask, dtype=bool)
        if mode == 'all':
            return np.array([all(vmask[i] for i in f) for f in self.f], dtype=bool)
        return np.array([any(vmask[i] for i in f) for f in self.f], dtype=bool)

    def sub(self, fmask):
        """new Part made of the faces where fmask is True"""
        fmask = np.asarray(fmask, dtype=bool)
        used = np.zeros(len(self.v), dtype=bool)
        fs = [f for f, m in zip(self.f, fmask) if m]
        for f in fs:
            used[list(f)] = True
        remap = -np.ones(len(self.v), dtype=np.int64)
        remap[used] = np.arange(used.sum())
        p = Part(self.v[used], [tuple(int(remap[i]) for i in f) for f in fs], None if self.W is None else self.W[used].copy())
        p.bone, p.look, p.obj, p.smooth = self.bone, self.look, self.obj, self.smooth
        return p

    def wsum(self, bones):
        """sum of weights over bones (skinned parts)"""
        return self.W[:, RIG.ids(bones)].sum(1)

    def face_centers(self):
        return np.array([self.v[list(f)].mean(0) for f in self.f])


def _ax(i):
    a = np.zeros(3); a[i] = 1
    return a


def _arc(ai, axis_rot, s, deg_per_m):
    # position along a circular arc of curvature deg_per_m starting along axis ai
    if abs(deg_per_m) < 1e-9:
        return _ax(ai) * s
    k = math.radians(deg_per_m)
    r = 1.0 / k
    ang = s * k
    ri = 'xyz'.index(axis_rot)
    oi = 3 - ai - ri
    sign = np.cross(_ax(ri), _ax(ai))[oi]
    return _ax(ai) * (r * math.sin(ang)) + _ax(oi) * (sign * r * (1 - math.cos(ang)))


def grip_frame(side='r'):
    """4x4 rest-space frame of a handle held in the closed fist of the UAL weapon animations
    (measured from Sword_Idle / Sword_Attack / Sword_Regular_*: all share one fist pose).
    +Y = pinky->index (towards the blade), +X = towards the knuckles (leading edge), origin = fist centre."""
    G = np.array([-0.8424, 0.0698, 1.41])
    A = unit([-0.152, -0.987, -0.05])
    E = np.array([-1.0, 0.0, 0.0])
    if side == 'l':
        G[0] = -G[0]; A[0] = -A[0]; E[0] = -E[0]
    E = unit(E - A * np.dot(E, A))
    Z = np.cross(E, A)
    Mx = np.eye(4)
    Mx[:3, 0] = E; Mx[:3, 1] = A; Mx[:3, 2] = Z; Mx[:3, 3] = G
    return Mx


def join(parts):
    vs = []; fs = []; off = 0
    anyW = any(p.W is not None for p in parts)
    Ws = []
    for p in parts:
        vs.append(p.v)
        fs += [tuple(i + off for i in f) for f in p.f]
        off += len(p.v)
        if anyW:
            if p.W is not None:
                Ws.append(p.W)
            else:
                w = np.zeros((len(p.v), RIG.nb), dtype=np.float32)
                w[:, RIG.idx[p.bone]] = 1.0
                Ws.append(w)
    q = Part(np.vstack(vs) if vs else np.zeros((0, 3)), fs, np.vstack(Ws) if anyW else None)
    q.bone, q.look, q.obj, q.smooth = parts[0].bone, parts[0].look, parts[0].obj, parts[0].smooth
    return q


# ------------------------------------------------------------------------------------------------ primitives
def _frames(P, up=None, closed=False):
    m = len(P)
    T = np.zeros_like(P)
    if closed:
        T = np.roll(P, -1, 0) - np.roll(P, 1, 0)
    else:
        T[1:-1] = P[2:] - P[:-2]
        T[0] = P[1] - P[0]
        T[-1] = P[-1] - P[-2]
    T /= np.maximum(np.linalg.norm(T, axis=1), 1e-12)[:, None]
    if up is None:
        up = np.array([0, 0, 1.0]) if abs(T[0][2]) < 0.9 else np.array([0, -1.0, 0])
    up = np.asarray(up, dtype=np.float64)
    N = np.zeros_like(P); B = np.zeros_like(P)
    n = up - T[0] * np.dot(up, T[0])
    if np.linalg.norm(n) < 1e-6:
        n = np.array([1.0, 0, 0]) - T[0] * T[0][0]
    N[0] = unit(n); B[0] = np.cross(T[0], N[0])
    for i in range(1, m):
        v = np.cross(T[i - 1], T[i]); s = np.linalg.norm(v); c = np.dot(T[i - 1], T[i])
        if s < 1e-9:
            nn = N[i - 1].copy()
        else:
            R = np.array(Matrix.Rotation(math.atan2(s, c), 3, Vector(v / s)))
            nn = R @ N[i - 1]
        nn -= T[i] * np.dot(nn, T[i])
        N[i] = unit(nn); B[i] = np.cross(T[i], N[i])
    return T, N, B


def ring2d(n, p=2.0, phase=0.0):
    """unit superellipse ring (n,2), CCW"""
    a = np.linspace(0, 2 * math.pi, n, endpoint=False) + phase
    c, s = np.cos(a), np.sin(a)
    if p != 2.0:
        c = np.sign(c) * np.abs(c) ** (2.0 / p)
        s = np.sign(s) * np.abs(s) ** (2.0 / p)
    return np.stack([c, s], axis=1)


def loft(rings, cap0=True, cap1=True, closed_ring=True, closed_path=False):
    R = np.asarray(rings, dtype=np.float64)
    m, n, _ = R.shape
    v = R.reshape(-1, 3).copy()
    f = []
    jn = n if closed_ring else n - 1
    im = m if closed_path else m - 1
    for i in range(im):
        i2 = (i + 1) % m
        for j in range(jn):
            j2 = (j + 1) % n
            f.append((i * n + j, i * n + j2, i2 * n + j2, i2 * n + j))
    if closed_ring and not closed_path:
        if cap0:
            c = len(v); v = np.vstack([v, R[0].mean(0)])
            f += [(c, (j + 1) % n, j) for j in range(n)]
        if cap1:
            c = len(v); v = np.vstack([v, R[-1].mean(0)])
            b = (m - 1) * n
            f += [(c, b + j, b + (j + 1) % n) for j in range(n)]
    return Part(v, f).cleanup()


def tube(path, r, n=8, r2=None, cap=True, cap0=None, cap1=None, up=None, closed=False, twist=0.0, phase=0.0, power=2.0):
    """sweep an (super)elliptic ring along a path. r: radius along the frame normal (`up` hint), r2: along binormal."""
    P = np.asarray(path, dtype=np.float64)
    m = len(P)
    r = np.broadcast_to(np.asarray(r, dtype=np.float64), (m,))
    r2 = r if r2 is None else np.broadcast_to(np.asarray(r2, dtype=np.float64), (m,))
    T, N, B = _frames(P, up, closed)
    rings = np.zeros((m, n, 3))
    for i in range(m):
        rg = ring2d(n, power, phase + twist * (i / max(m - 1, 1)))
        rings[i] = P[i] + r[i] * rg[:, 0:1] * N[i] + r2[i] * rg[:, 1:2] * B[i]
    c0 = cap if cap0 is None else cap0
    c1 = cap if cap1 is None else cap1
    return loft(rings, cap0=c0, cap1=c1, closed_path=closed)


def rings_z(sections, n=12, mod=None, phase=0.0):
    """sections: (z, cx, cy, rx, ry[, power]) -> rings (m,n,3) in horizontal planes, CCW seen from +Z.
    mod(theta (n,), z) -> radial multiplier (n,)"""
    out = []
    for s in sections:
        z, cx, cy, rx, ry = s[:5]
        p = s[5] if len(s) > 5 else 2.0
        rg = ring2d(n, p, phase)
        if mod is not None:
            th = np.arctan2(rg[:, 1], rg[:, 0])
            rg = rg * np.asarray(mod(th, z))[:, None]
        out.append(np.stack([cx + rx * rg[:, 0], cy + ry * rg[:, 1], np.full(n, z)], axis=1))
    return np.array(out)


def loft_z(sections, n=12, cap0=True, cap1=True, mod=None, phase=0.0):
    return loft(rings_z(sections, n, mod, phase), cap0, cap1).recalc()


def revolve(profile, n=12, cap=True):
    """lathe (r, z) profile around Z"""
    secs = [(z, 0, 0, max(r, 0.0), max(r, 0.0)) for r, z in profile]
    return loft(rings_z(secs, n), cap0=cap, cap1=cap).recalc()


def ellipsoid(rx, ry=None, rz=None, n=12, m=7):
    ry = rx if ry is None else ry
    rz = rx if rz is None else rz
    prof = [(math.sin(t), -math.cos(t)) for t in np.linspace(0, math.pi, m)]
    prof[0] = (0.0, -1.0); prof[-1] = (0.0, 1.0)
    return revolve(prof, n, cap=False).scale((rx, ry, rz))


def box(sx, sy, sz, bevel=0.0, segs=1):
    x, y, z = sx / 2, sy / 2, sz / 2
    v = [(-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z), (-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z)]
    f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    p = Part(v, f)
    if bevel > 0:
        p.bevel(bevel, segs)
    return p


def prism(outline, z0, z1):
    """extrude a 2D polygon (n,2) (in XY) from z0 to z1"""
    o = np.asarray(outline, dtype=np.float64)
    n = len(o)
    v = [(x, y, z0) for x, y in o] + [(x, y, z1) for x, y in o]
    f = [tuple(range(n))[::-1], tuple(range(n, 2 * n))]
    for j in range(n):
        j2 = (j + 1) % n
        f.append((j, j2, n + j2, n + j))
    return Part(v, f).recalc()


def blade(stations, sharp_tip=True):
    """flat blade along +Y, width along X, thickness along Z. stations: (y, w_left, w_right, thick)."""
    rings = []
    for (y, wl, wr, t) in stations:
        rings.append([(-wl, y, 0), (0, y, t / 2), (wr, y, 0), (0, y, -t / 2)])
    p = loft(np.array(rings), cap0=True, cap1=True).recalc()
    p.smooth = 20.0
    return p


def sheet(grid, closed_u=False):
    """grid (m,n,3) -> quad sheet (open). closed_u closes along n"""
    G = np.asarray(grid, dtype=np.float64)
    m, n, _ = G.shape
    f = []
    jn = n if closed_u else n - 1
    for i in range(m - 1):
        for j in range(jn):
            j2 = (j + 1) % n
            f.append((i * n + j, i * n + j2, (i + 1) * n + j2, (i + 1) * n + j))
    return Part(G.reshape(-1, 3), f)


def hang(top, length, rows=6, out=None, flare=0.0, rag=0.3, seed=0, wave=0.0, holes=0.0, taper=0.0, curve=1.3,
         tatter=0.0):
    """cloth hanging from a polyline `top` (n,3): rows go down -Z by `length` (scalar or per column).
    out: outward unit vectors per column (n,3) used by flare (pushes the hem outwards) and wave.
    rag: random shortening of columns (0..1); tatter: extra saw-tooth on the hem; holes: chance of missing quads
    near the hem; taper: pull the hem towards the strip centre."""
    top = np.asarray(top, dtype=np.float64)
    n = len(top)
    rng = np.random.default_rng(seed)
    L = np.broadcast_to(np.asarray(length, dtype=np.float64), (n,)).copy()
    L *= (1 - rag * rng.uniform(0, 1, n))
    if tatter:
        L *= 1 - tatter * (np.arange(n) % 2)
    if out is None:
        out = np.zeros((n, 3))
    out = np.asarray(out, dtype=np.float64)
    cen = top.mean(0)
    ph = rng.uniform(0, 6.28, n)
    G = np.zeros((rows + 1, n, 3))
    for i in range(rows + 1):
        t = i / rows
        p = top + np.array([0, 0, -1.0]) * (L * t)[:, None] + out * (flare * t ** curve)
        if wave:
            p = p + out * (wave * np.sin(ph + t * 5.0) * t)[:, None]
        if taper:
            p[:, :2] = p[:, :2] + (cen[:2] - p[:, :2]) * (taper * t)
        G[i] = p
    s = sheet(G)
    if holes:
        keep = []
        k = 0
        for i in range(rows):
            for j in range(n - 1):
                t = (i + 1) / rows
                keep.append(not (t > 0.5 and rng.uniform() < holes * t))
                k += 1
        s = s.sub(np.array(keep))
    s.smooth = 180.0
    return s


def arc_pts(c, rx, ry, a0, a1, n, z=None):
    """points on a horizontal ellipse arc (angles in degrees; 0 = +X, -90 = front (-Y)); returns pts, outward normals"""
    a = np.radians(np.linspace(a0, a1, n))
    c = np.asarray(c, dtype=np.float64)
    pts = np.stack([c[0] + rx * np.cos(a), c[1] + ry * np.sin(a), np.full(n, c[2] if z is None else z)], axis=1)
    nr = np.stack([np.cos(a) / max(rx, 1e-9), np.sin(a) / max(ry, 1e-9), np.zeros(n)], axis=1)
    nr /= np.linalg.norm(nr, axis=1)[:, None]
    return pts, nr


def band(c, rx, ry, w, th, tilt_x=0.0, tilt_y=0.0, n=20, a0=0.0, a1=360.0):
    """flat strap/belt loop around an ellipse (width w vertical, thickness th radial); tilt in degrees"""
    closed = abs((a1 - a0) - 360.0) < 1e-6
    a = np.radians(np.linspace(a0, a1, n, endpoint=not closed))
    pts = np.stack([rx * np.cos(a), ry * np.sin(a), np.zeros(len(a))], axis=1)
    pts = pts @ (rot_axis('y', tilt_y) @ rot_axis('x', tilt_x)).T + np.asarray(c, dtype=np.float64)
    return tube(pts, w * 0.7071, n=4, r2=th * 0.7071, up=(0, 0, 1), closed=closed, phase=math.pi / 4)


def hull(points):
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in points]
    bmesh.ops.convex_hull(bm, input=vs)
    for v in [v for v in bm.verts if not v.link_faces]:
        bm.verts.remove(v)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    p = Part([], []).from_bm(bm)
    return p


def rock(rx, ry, rz, seed=0, n=14, jit=0.25, flat=True):
    """faceted convex chunk"""
    rng = np.random.default_rng(seed)
    pts = rng.normal(size=(n, 3))
    pts /= np.linalg.norm(pts, axis=1)[:, None]
    pts *= (1 + rng.uniform(-jit, jit, (n, 1)))
    pts *= np.array([rx, ry, rz])
    p = hull(pts.tolist())
    p.smooth = 0.0 if flat else 35.0
    return p


def ico(r=1.0, sub=1):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    return Part([], []).from_bm(bm)


def torus(R, r, n=10, m=5, ry=None):
    a = np.linspace(0, 2 * math.pi, n, endpoint=False)
    path = np.stack([R * np.cos(a), (ry if ry else R) * np.sin(a), np.zeros(n)], axis=1)
    return tube(path, r, n=m, closed=True, up=(0, 0, 1))


def cone_spike(r, h, n=6, bend=0.0):
    """spike along +Z from base radius r"""
    return revolve([(r, 0), (r * 0.55, h * 0.45), (r * 0.18, h * 0.85), (0, h)], n, cap=True)


def horn(path, r0, n=6, r_tip=0.0015, up=None, r2=None, power=1.0, cap0=True):
    P = np.asarray(path, dtype=np.float64)
    m = len(P)
    t = np.linspace(0, 1, m)
    r = r0 * (1 - t) ** power + r_tip
    rr2 = None if r2 is None else r2 * (1 - t) ** power + r_tip
    return tube(P, r, n=n, r2=rr2, up=up, cap0=cap0, cap1=True)


def chain(path, link=0.035, r=0.006, w=0.6, n=6, m=4):
    """chain of torus links along a polyline"""
    P = np.asarray(path, dtype=np.float64)
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]
    L = d[-1]
    step = link * 0.72
    k = max(2, int(L / step))
    parts = []
    for i in range(k + 1):
        s = L * i / k
        j = min(np.searchsorted(d, s, side='right') - 1, len(P) - 2)
        u = (s - d[j]) / max(d[j + 1] - d[j], 1e-9)
        p = P[j] * (1 - u) + P[j + 1] * u
        tdir = unit(P[j + 1] - P[j])
        t = torus(link * 0.5, r, n=n, m=m, ry=link * 0.5 * w)  # long axis X
        # torus lies in XY plane with long axis X; map X->tdir
        fr = frame_from(tdir, (0, 0, 1) if abs(tdir[2]) < 0.9 else (1, 0, 0))
        M = np.stack([fr[:, 1], fr[:, 0], fr[:, 2]], axis=1)  # X->tdir, Y->side, Z->up
        if i % 2:
            M = M @ rot_axis('x', 90)
        t.v = t.v @ M.T + p
        if np.linalg.det(M) < 0:
            t.flip()
        parts.append(t)
    return join(parts)


# ------------------------------------------------------------------------------------------------ blender-modifier round trip
def _temp_object(part, name='tmp'):
    me = bpy.data.meshes.new(name)
    me.from_pydata(part.v.tolist(), [], part.f)
    me.update()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if part.W is not None:
        _write_groups(ob, part.W)
    return ob


def _write_groups(ob, W, eps=1e-4):
    used = np.where(W.max(0) > eps)[0]
    for b in used:
        vg = ob.vertex_groups.new(name=RIG.names[b])
        col = W[:, b]
        nz = np.where(col > eps)[0]
        ones = nz[col[nz] > 0.9999]
        if len(ones):
            vg.add(ones.tolist(), 1.0, 'REPLACE')
        for i in nz[col[nz] <= 0.9999]:
            vg.add([int(i)], float(col[i]), 'REPLACE')


def _read_mesh(me, groups=None):
    n = len(me.vertices)
    co = np.empty(n * 3, dtype=np.float32)
    me.vertices.foreach_get('co', co)
    v = co.reshape(-1, 3).astype(np.float64)
    f = [tuple(p.vertices) for p in me.polygons]
    W = None
    if groups is not None:
        W = np.zeros((n, RIG.nb), dtype=np.float32)
        gmap = {g.index: RIG.idx[g.name] for g in groups if g.name in RIG.idx}
        for vert in me.vertices:
            for g in vert.groups:
                if g.group in gmap:
                    W[vert.index, gmap[g.group]] = g.weight
    return v, f, W


def via_mods(part, mods):
    """run a Part through Blender modifiers [(type, {prop: value}), ...]; weights survive."""
    ob = _temp_object(part)
    for typ, props in mods:
        m = ob.modifiers.new(typ, typ)
        for k, val in props.items():
            setattr(m, k, val)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    me2 = ev.to_mesh()
    v, f, W = _read_mesh(me2, ob.vertex_groups if part.W is not None else None)
    ev.to_mesh_clear()
    me = ob.data
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me)
    q = Part(v, f, W)
    q.bone, q.look, q.obj, q.smooth = part.bone, part.look, part.obj, part.smooth
    if W is not None:
        normalize_weights(q)
    return q


def boolean(a, b, op='DIFFERENCE', solver='EXACT'):
    """boolean of two closed Parts (weights are dropped)"""
    a = a.copy(); a.W = None
    b = b.copy(); b.W = None
    oa = _temp_object(a, 'ba'); ob = _temp_object(b, 'bb')
    m = oa.modifiers.new('bool', 'BOOLEAN')
    m.operation = op; m.object = ob
    try:
        m.solver = solver
    except Exception:
        pass
    dg = bpy.context.evaluated_depsgraph_get()
    ev = oa.evaluated_get(dg)
    me2 = ev.to_mesh()
    v, f, _ = _read_mesh(me2, None)
    ev.to_mesh_clear()
    for o in (oa, ob):
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.meshes.remove(me)
    q = Part(v, f)
    q.bone, q.look, q.obj, q.smooth = a.bone, a.look, a.obj, a.smooth
    return q.cleanup(1e-6)


def decimate(part, ratio):
    return via_mods(part, [('DECIMATE', dict(ratio=ratio))])


def subsurf(part, levels=1):
    return via_mods(part, [('SUBSURF', dict(levels=levels, render_levels=levels))])


def remesh(parts, voxel=0.012, smooth_iter=4, ratio=None, adaptivity=0.0):
    p = join(parts) if isinstance(parts, (list, tuple)) else parts
    p.W = None
    mods = [('REMESH', dict(mode='VOXEL', voxel_size=voxel, adaptivity=adaptivity))]
    if smooth_iter:
        mods.append(('SMOOTH', dict(factor=0.6, iterations=smooth_iter)))
    if ratio:
        mods.append(('DECIMATE', dict(ratio=ratio)))
    return via_mods(p, mods)


# ------------------------------------------------------------------------------------------------ weights
def normalize_weights(part, k=4):
    W = part.W
    if W.shape[1] > k:
        thr = -np.partition(-W, k - 1, axis=1)[:, k - 1:k]
        W = np.where(W >= thr, W, 0)
    s = W.sum(1, keepdims=True)
    bad = (s[:, 0] < 1e-6)
    if bad.any():
        W[bad] = 0
        W[bad, RIG.idx['pelvis']] = 1.0
        s = W.sum(1, keepdims=True)
    part.W = (W / s).astype(np.float32)
    return part


def rigid_W(n, bone):
    W = np.zeros((n, RIG.nb), dtype=np.float32)
    W[:, RIG.idx[bone]] = 1
    return W


def weights_from(part, d):
    """d: {bone: array(n) or scalar}"""
    W = np.zeros((len(part.v), RIG.nb), dtype=np.float32)
    for b, a in d.items():
        W[:, RIG.idx[b]] = a
    part.W = W
    return normalize_weights(part)


def weights_by(part, coord, stops):
    """piecewise-linear bone-weight dicts along a scalar coordinate.
    coord: array(n) ; stops: [(value, {bone: w}), ...] sorted ascending"""
    x = np.asarray(coord, dtype=np.float64)
    W = np.zeros((len(part.v), RIG.nb), dtype=np.float32)
    xs = [s[0] for s in stops]
    for i, (xv, d) in enumerate(stops):
        # hat function
        if i == 0:
            left = np.ones_like(x) * (x <= xv)
        else:
            left = np.clip((x - xs[i - 1]) / max(xv - xs[i - 1], 1e-9), 0, 1) * (x <= xv)
        if i == len(stops) - 1:
            right = np.ones_like(x) * (x > xv)
        else:
            right = np.clip((xs[i + 1] - x) / max(xs[i + 1] - xv, 1e-9), 0, 1) * (x > xv)
        h = left + right
        for b, w in d.items():
            W[:, RIG.idx[b]] += (h * w).astype(np.float32)
    part.W = W
    return normalize_weights(part)


def transfer_weights(part, ref, smooth=0, max_dist=None):
    """nearest-surface interpolated weights from a skinned reference Part"""
    tris = ref.tris()
    bvh = BVHTree.FromPolygons([tuple(p) for p in ref.v], [tuple(int(i) for i in t) for t in tris])
    W = np.zeros((len(part.v), RIG.nb), dtype=np.float32)
    rv = ref.v
    for i, p in enumerate(part.v):
        loc, nrm, idx, dist = bvh.find_nearest(Vector(p))
        a, b, c = tris[idx]
        pa, pb, pc = rv[a], rv[b], rv[c]
        q = np.array(loc)
        v0 = pb - pa; v1 = pc - pa; v2 = q - pa
        d00 = v0 @ v0; d01 = v0 @ v1; d11 = v1 @ v1; d20 = v2 @ v0; d21 = v2 @ v1
        den = d00 * d11 - d01 * d01
        if abs(den) < 1e-14:
            u = v = 1 / 3.0; w = 1 / 3.0
        else:
            v = (d11 * d20 - d01 * d21) / den
            w = (d00 * d21 - d01 * d20) / den
            u = 1 - v - w
        W[i] = u * ref.W[a] + v * ref.W[b] + w * ref.W[c]
    part.W = W
    if smooth:
        smooth_weights(part, smooth)
    return normalize_weights(part)


def smooth_weights(part, iters=2, fac=0.5, mask=None):
    e = set()
    for f in part.f:
        for k in range(len(f)):
            a, b = f[k], f[(k + 1) % len(f)]
            e.add((min(a, b), max(a, b)))
    e = np.array(sorted(e), dtype=np.int64)
    if len(e) == 0:
        return part
    W = part.W.astype(np.float64)
    deg = np.zeros(len(part.v)); np.add.at(deg, e[:, 0], 1); np.add.at(deg, e[:, 1], 1)
    deg[deg == 0] = 1
    for _ in range(iters):
        acc = np.zeros_like(W)
        np.add.at(acc, e[:, 0], W[e[:, 1]])
        np.add.at(acc, e[:, 1], W[e[:, 0]])
        avg = acc / deg[:, None]
        if mask is None:
            W = W * (1 - fac) + avg * fac
        else:
            mk = (np.asarray(mask, dtype=np.float64) * fac)[:, None]
            W = W * (1 - mk) + avg * mk
    part.W = W.astype(np.float32)
    return part


def smooth_verts(part, iters=2, fac=0.5, mask=None):
    e = set()
    for f in part.f:
        for k in range(len(f)):
            a, b = f[k], f[(k + 1) % len(f)]
            e.add((min(a, b), max(a, b)))
    e = np.array(sorted(e), dtype=np.int64)
    deg = np.zeros(len(part.v)); np.add.at(deg, e[:, 0], 1); np.add.at(deg, e[:, 1], 1)
    deg[deg == 0] = 1
    v = part.v
    for _ in range(iters):
        acc = np.zeros_like(v)
        np.add.at(acc, e[:, 0], v[e[:, 1]])
        np.add.at(acc, e[:, 1], v[e[:, 0]])
        avg = acc / deg[:, None]
        if mask is None:
            v = v * (1 - fac) + avg * fac
        else:
            mk = (np.asarray(mask, dtype=np.float64) * fac)[:, None]
            v = v * (1 - mk) + avg * mk
    part.v = v
    return part


# ------------------------------------------------------------------------------------------------ base body
def load_body(kind='male'):
    """Quaternius Superhero base body re-fitted to the UAL rest pose (see cache_body.py). Skinned Part."""
    d = np.load(os.path.join(CACHE, 'body_%s.npz' % kind), allow_pickle=True)
    names = [str(n) for n in d['bones']]
    assert names == RIG.names, "bone order mismatch in body cache"
    fl = d['faces_len']; ff = d['faces_flat']
    f = []; o = 0
    for n in fl:
        f.append(tuple(int(i) for i in ff[o:o + n])); o += n
    p = Part(d['v'], f, d['W'].astype(np.float32))
    p.smooth = 180.0
    return p


def pose_sculpt(part, pose):
    """Re-proportion a skinned Part by posing its bones and freezing the result as the new rest shape.
    pose: {bone: dict(rot=[(axis, deg), ...] (world axes, about the bone head, inherited by children),
                      move=(x,y,z) (world, inherited), scale=(sx,sy,sz) (world axes about the bone head, NOT inherited))}
    """
    G = {}
    S = np.zeros((RIG.nb, 4, 4))
    for i, n in enumerate(RIG.names):
        par = RIG.parent[n]
        Gp = G[par] if par else np.eye(4)
        D = np.eye(4)
        e = pose.get(n)
        h = RIG.head[n]
        Sc = np.eye(4)
        if e:
            R = np.eye(3)
            rots = e.get('rot', [])
            if rots and not isinstance(rots[0], (tuple, list)):
                rots = [rots]
            for ax, deg in rots:
                R = rot_axis(ax, deg) @ R
            D[:3, :3] = R
            D[:3, 3] = h - R @ h + np.asarray(e.get('move', (0, 0, 0)), dtype=np.float64)
            sc = e.get('scale')
            if sc is not None:
                sc = np.array([sc, sc, sc], dtype=np.float64) if np.isscalar(sc) else np.asarray(sc, dtype=np.float64)
                Sc[:3, :3] = np.diag(sc)
                Sc[:3, 3] = h - sc * h
        G[n] = Gp @ D
        S[i] = G[n] @ Sc
    vh = np.c_[part.v, np.ones(len(part.v))]
    out = np.zeros((len(part.v), 3))
    used = np.where(part.W.max(0) > 0)[0]
    for b in used:
        out += part.W[:, b:b + 1] * (vh @ S[b].T)[:, :3]
    part.v = out
    return part


def pose_part(part, pose, bone):
    """move a rest-space rigid Part the same way pose_sculpt(pose) moved the flesh attached to `bone`"""
    _, G = posed_point(pose, bone, (0, 0, 0))
    part.tf(G)
    return part


def posed_point(pose, bone, p):
    """where a rest-space point rigidly attached to `bone` ends up after pose_sculpt(pose) (rot/move only)"""
    chain_ = []
    n = bone
    while n:
        chain_.append(n); n = RIG.parent[n]
    G = np.eye(4)
    for n in reversed(chain_):
        D = np.eye(4)
        e = pose.get(n)
        if e:
            R = np.eye(3)
            rots = e.get('rot', [])
            if rots and not isinstance(rots[0], (tuple, list)):
                rots = [rots]
            for ax, deg in rots:
                R = rot_axis(ax, deg) @ R
            h = RIG.head[n]
            D[:3, :3] = R
            D[:3, 3] = h - R @ h + np.asarray(e.get('move', (0, 0, 0)), dtype=np.float64)
        G = G @ D
    return (G @ np.r_[np.asarray(p, dtype=np.float64), 1.0])[:3], G


# ------------------------------------------------------------------------------------------------ creature builder
class Creature:
    def __init__(self, cid, tri_budget=15000):
        self.id = cid
        self.budget = tri_budget
        self.scene = reset_scene()
        self.arm = load_rig()
        self.rig = RIG
        self.parts = []
        self.looks = {}
        self.notes = []
        self.t0 = time.time()

    def look(self, name, kind, slot, **params):
        self.looks[name] = dict(kind=kind, slot=slot, params=params)
        return name

    def add(self, part, bone=None, look=None, obj=None, smooth=None, mirror=False):
        if bone is not None:
            part.bone = bone
            part.W = None
        if look is not None:
            part.look = look
        if obj is not None:
            part.obj = obj
        if smooth is not None:
            part.smooth = float(smooth)
        assert part.look in self.looks, "unknown look %r" % part.look
        assert part.bone is not None or part.W is not None, "part has neither bone nor weights"
        if part.bone is not None:
            assert part.bone in RIG.idx, part.bone
        self.parts.append(part)
        if mirror:
            self.parts.append(part.copy().mirror())
        return part

    def tri_report(self):
        per = {}
        for p in self.parts:
            per[p.obj] = per.get(p.obj, 0) + p.ntri
        return per

    # ---- build blender objects
    def _make_objects(self):
        import looks as L
        objs = []
        names = []
        for p in self.parts:
            if p.obj not in names:
                names.append(p.obj)
        self.look_mats = {}
        for name in names:
            parts = [p for p in self.parts if p.obj == name]
            Vs = []; Fs = []; Ws = []; fl = []; fa = []
            off = 0
            for p in parts:
                Vs.append(p.v)
                Fs += [tuple(i + off for i in f) for f in p.f]
                off += len(p.v)
                if p.bone is not None:
                    Ws.append(rigid_W(len(p.v), p.bone))
                else:
                    q = Part(p.v, [], p.W.copy()); normalize_weights(q); Ws.append(q.W)
                fl += [p.look] * len(p.f)
                fa += [p.smooth] * len(p.f)
            Vall = np.vstack(Vs); Wall = np.vstack(Ws)
            me = bpy.data.meshes.new(name)
            me.from_pydata(Vall.tolist(), [], Fs)
            me.update()
            ob = bpy.data.objects.new(name, me)
            self.scene.collection.objects.link(ob)
            used = []
            for l in fl:
                if l not in used:
                    used.append(l)
            for l in used:
                if l not in self.look_mats:
                    d = self.looks[l]
                    self.look_mats[l] = L.make_look_material(l, d['kind'], d['params'])
                me.materials.append(self.look_mats[l])
            li = {l: i for i, l in enumerate(used)}
            me.polygons.foreach_set('material_index', np.array([li[l] for l in fl], dtype=np.int32))
            # shading
            bm = bmesh.new(); bm.from_mesh(me)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
            bm.faces.ensure_lookup_table()
            fa = np.array(fa)
            for f in bm.faces:
                f.smooth = bool(fa[f.index] > 0.5)
            for e in bm.edges:
                lf = e.link_faces
                if len(lf) == 2:
                    thr = float(min(fa[lf[0].index], fa[lf[1].index]))
                    e.smooth = bool(e.calc_face_angle(0.0) < math.radians(thr))
                else:
                    e.smooth = True
            # triangulate now: the baked tangent space then matches the exported triangles exactly
            bmesh.ops.triangulate(bm, faces=bm.faces[:])
            bm.to_mesh(me); bm.free()
            me.update()
            _write_hem(me)
            _write_groups(ob, Wall)
            ob.parent = self.arm
            md = ob.modifiers.new("Armature", 'ARMATURE')
            md.object = self.arm
            objs.append(ob)
        self.objs = objs
        return objs

    def stats(self):
        st = {}
        mn = np.array([1e9] * 3); mx = -mn
        total = 0
        for ob in self.objs:
            me = ob.data
            me.calc_loop_triangles()
            n = len(me.loop_triangles)
            total += n
            co = np.empty(len(me.vertices) * 3, dtype=np.float32)
            me.vertices.foreach_get('co', co)
            co = co.reshape(-1, 3)
            mn = np.minimum(mn, co.min(0)); mx = np.maximum(mx, co.max(0))
            st[ob.name] = dict(triangles=n, vertices=len(me.vertices), materials=[m.name for m in me.materials])
        return dict(id=self.id, triangles=total, objects=st, bbox_min=[round(float(x), 4) for x in mn],
                    bbox_max=[round(float(x), 4) for x in mx], height=round(float(mx[2]), 3))

    def finalize(self, bake=True, export=True, size=2048, notes=""):
        import looks as L
        per = self.tri_report()
        tot = sum(per.values())
        log("triangles per object:", per, "total", tot, "(budget %d)" % self.budget)
        if tot > self.budget:
            log("WARNING: OVER TRIANGLE BUDGET by", tot - self.budget)
        objs = self._make_objects()
        st = self.stats()
        log("bbox", st['bbox_min'], st['bbox_max'])
        if st['bbox_min'][2] < -0.004 or st['bbox_min'][2] > 0.02:
            log("WARNING: feet not on ground, min z =", st['bbox_min'][2])
        L.unwrap(objs)
        if bake:
            L.bake_and_assign(self, objs, size=size)
        else:
            L.assign_preview_slots(self, objs)
        st = self.stats()
        st['notes'] = notes
        st['looks'] = {k: v['slot'] for k, v in self.looks.items() if k in self.look_mats}
        if export:
            path = os.path.join(OUT_DIR, self.id + ".glb")
            export_glb(path, self.arm, objs)
            st['file'] = path
            st['bytes'] = os.path.getsize(path)
            log("WROTE", path, st['bytes'], "bytes")
        with open(os.path.join(WORK, self.id + ".json"), 'w', encoding='utf8') as f:
            json.dump(st, f, indent=1)
        log("done %s in %.1fs  tris=%d  height=%.2f" % (self.id, time.time() - self.t0, st['triangles'], st['height']))
        return st


def _write_hem(me, rng=0.05):
    """float point attribute 'hem_d' = 1 - (distance to the nearest open mesh border / rng), clamped to 0..1
    (0 = far from any border or closed mesh). looks.NB.hem() turns it into stitched hems and frayed cloth edges."""
    nv = len(me.vertices)
    val = np.zeros(nv, dtype=np.float32)
    ne = len(me.edges)
    if nv and ne:
        ev = np.empty(ne * 2, dtype=np.int32)
        me.edges.foreach_get('vertices', ev)
        ev = ev.reshape(-1, 2)
        nl = len(me.loops)
        le = np.empty(nl, dtype=np.int32)
        me.loops.foreach_get('edge_index', le)
        cnt = np.bincount(le, minlength=ne)
        be = ev[cnt == 1]
        if len(be):
            co = np.empty(nv * 3, dtype=np.float32)
            me.vertices.foreach_get('co', co)
            co = co.reshape(-1, 3).astype(np.float64)
            A = co[be[:, 0]]; B = co[be[:, 1]]
            AB = B - A
            L2 = np.maximum((AB * AB).sum(1), 1e-12)
            mid = (A + B) / 2
            hl = np.sqrt(L2) / 2
            best = np.full(nv, 1e9)
            for s in range(0, nv, 512):
                Pp = co[s:s + 512]
                # coarse reject by segment mid-points, then exact point-segment distance
                dm = np.linalg.norm(Pp[:, None, :] - mid[None, :, :], axis=2) - hl[None, :]
                near = dm < rng
                for i in np.where(near.any(axis=1))[0]:
                    j = np.where(near[i])[0]
                    t = np.clip(((Pp[i] - A[j]) * AB[j]).sum(1) / L2[j], 0, 1)
                    d = np.linalg.norm(Pp[i] - (A[j] + AB[j] * t[:, None]), axis=1)
                    best[s + i] = d.min()
            val = np.clip(1.0 - best / rng, 0.0, 1.0).astype(np.float32)
    at = me.attributes.get('hem_d')
    if at is None:
        at = me.attributes.new('hem_d', 'FLOAT', 'POINT')
    at.data.foreach_set('value', val)


def export_glb(path, arm, objs):
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.gltf(
        filepath=path, export_format='GLB', use_selection=True,
        export_animations=False, export_skins=True, export_def_bones=False, export_leaf_bone=False,
        export_rest_position_armature=True, export_yup=True, export_apply=True,
        # textures are JPEG files written by looks._save_jpg (normal map at quality 96) and packed: the exporter
        # embeds those bytes as they are (the quality below only applies if it ever has to re-encode an image)
        export_materials='EXPORT', export_image_format='JPEG', export_jpeg_quality=95, export_image_quality=95,
        export_tangents=True, export_normals=True, export_texcoords=True,
        export_cameras=False, export_lights=False, export_morph=False, export_extras=False,
        export_vertex_color='NONE', export_all_vertex_colors=False,
    )
