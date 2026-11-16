"""Beast kit core (rigs B-F of the Ashen Abyss bestiary): custom skeletons, numeric pose engine with IK, procedural
clips baked to plain bone keyframes, looks/bake from the monsters kit, .glb export WITH embedded animations, meta.

Space: Blender armature space, creature faces -Y (= +Z in glTF / the game), +X is its left, Z up, metres.
A pose is a set of per-bone deltas D (rotation R about the bone's rest head + translation t + bone-local scale s),
expressed in rest coordinates and inherited by children:  G[b] = G[parent] @ D[b];  posed vertex = sum w G[b] v_rest.
Blender / glTF channels follow from  basis[b] = rest[b]^-1 D[b] rest[b].
"""
import bpy, bmesh, math, os, sys, json, time
import numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, r"Y:\tools\blender\monsters")
import monlib as M
import looks as L

OUT_DIR = r"Y:\game\assets\chars"
WORK = os.path.join(HERE, "out")
M.WORK = WORK
M.TEXDIR = os.path.join(WORK, "tex")
for _d in (WORK, M.TEXDIR):
    os.makedirs(_d, exist_ok=True)
FPS = 30
CLIPS = ("Idle", "Walk", "Run", "Attack1", "Attack2", "Hit", "Death", "Spawn")
LOOPS = ("Idle", "Walk", "Run")
V = M.V
unit = M.unit
rot_axis = M.rot_axis
I3 = np.eye(3)


def log(*a):
    print("[beast]", *a)
    sys.stdout.flush()


# ------------------------------------------------------------------------------------------------ small maths
def sm(u, a, b):
    """smoothstep 0..1 while u goes a..b"""
    if a == b:
        return 1.0 if u >= b else 0.0
    t = min(1.0, max(0.0, (u - a) / (b - a)))
    return t * t * (3 - 2 * t)


def bump(u, a, b, c, d):
    """0 -> 1 over a..b, hold, 1 -> 0 over c..d"""
    return sm(u, a, b) * (1.0 - sm(u, c, d))


def osc(u, n=1.0, ph=0.0):
    return math.sin(2 * math.pi * (n * u + ph))


def rx(d):
    return rot_axis('x', d)


def ry(d):
    return rot_axis('y', d)


def rz(d):
    return rot_axis('z', d)


def euler(x=0.0, y=0.0, z=0.0):
    """rotation: first about X (pitch: +x lifts the nose of a -Y facing creature... see Pose.rot), then Y, then Z"""
    R = I3
    if x:
        R = rx(x) @ R
    if y:
        R = ry(y) @ R
    if z:
        R = rz(z) @ R
    return R


def seg_dist(P, a, b):
    """distance of points P (n,3) to segment a-b"""
    ab = b - a
    L2 = max(float(ab @ ab), 1e-12)
    t = np.clip(((P - a) @ ab) / L2, 0, 1)
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)


# ------------------------------------------------------------------------------------------------ skeleton
class Sk:
    """skeleton description; root bone 'root' sits at the origin and is never animated"""

    def __init__(self):
        self.names = []
        self.head = {}
        self.tail = {}
        self.parent = {}
        self.bone('root', (0, 0, 0), (0, 0.12, 0), None)

    def bone(self, name, head, tail, parent='root'):
        assert name not in self.head, name
        assert parent is None or parent in self.head, parent
        self.names.append(name)
        self.head[name] = V(head).astype(float)
        self.tail[name] = V(tail).astype(float)
        self.parent[name] = parent
        return name

    def lr(self, name, head, tail, parent):
        """mirrored pair name_l (+X) / name_r; parent may contain %s for the side letter"""
        out = []
        for s, sg in (('l', 1.0), ('r', -1.0)):
            p = parent % s if '%s' in parent else parent
            out.append(self.bone(name + '_' + s, V(head) * (sg, 1, 1), V(tail) * (sg, 1, 1), p))
        return out

    def chain(self, prefix, pts, parent, suffix=''):
        names = []
        for i in range(len(pts) - 1):
            n = '%s%d%s' % (prefix, i + 1, suffix)
            self.bone(n, pts[i], pts[i + 1], parent)
            parent = n
            names.append(n)
        return names

    def chain_lr(self, prefix, pts, parent):
        """mirrored chains prefix1_l.. / prefix1_r.."""
        out = {}
        for s, sg in (('l', 1.0), ('r', -1.0)):
            p = parent % s if '%s' in parent else parent
            out[s] = self.chain(prefix, [V(q) * (sg, 1, 1) for q in pts], p, '_' + s)
        return out

    def build(self):
        ad = bpy.data.armatures.new("Armature")
        arm = bpy.data.objects.new("Armature", ad)
        bpy.context.scene.collection.objects.link(arm)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode='EDIT')
        eb = {}
        for n in self.names:
            b = ad.edit_bones.new(n)
            h = self.head[n]; t = self.tail[n]
            if np.linalg.norm(t - h) < 0.01:
                t = h + V(0, 0, 0.02)
            b.head = tuple(h); b.tail = tuple(t); b.roll = 0.0
            b.use_connect = False
            if self.parent[n]:
                b.parent = eb[self.parent[n]]
            eb[n] = b
        bpy.ops.object.mode_set(mode='OBJECT')
        for pb in arm.pose.bones:
            pb.rotation_mode = 'QUATERNION'
        M.RIG = M.Rig(arm)
        return arm


class Rg:
    """numeric copy of the rest armature"""

    def __init__(self, arm):
        bones = list(arm.data.bones)
        self.names = [b.name for b in bones]
        self.idx = {n: i for i, n in enumerate(self.names)}
        self.nb = len(bones)
        self.par = np.array([self.idx[b.parent.name] if b.parent else -1 for b in bones])
        self.head = np.array([b.head_local[:] for b in bones], dtype=float)
        self.tail = np.array([b.tail_local[:] for b in bones], dtype=float)
        self.R = np.array([np.array(b.matrix_local)[:3, :3] for b in bones], dtype=float)
        # parents before children
        order = []
        seen = set()

        def visit(i):
            if i in seen:
                return
            if self.par[i] >= 0:
                visit(int(self.par[i]))
            seen.add(i); order.append(i)
        for i in range(self.nb):
            visit(i)
        self.order = order

    def H(self, b):
        return self.head[self.idx[b]].copy()

    def T(self, b):
        return self.tail[self.idx[b]].copy()


class Pose:
    def __init__(self, rg):
        self.rg = rg
        n = rg.nb
        self.Rd = np.tile(I3, (n, 1, 1))
        self.t = np.zeros((n, 3))
        self.s = np.ones((n, 3))
        self.u = 0.0
        self.err = 0.0      # worst IK reach error (m)

    # ---- setters (rotations in degrees about the armature axes, in the parent's posed frame)
    def rot(self, bone, x=0.0, y=0.0, z=0.0):
        """compose a rotation onto the bone: x = pitch (+ nose DOWN for a -Y facing spine... right-hand about +X),
        y = roll, z = yaw (+ turns the -Y nose towards +X = the creature's left)"""
        i = self.rg.idx[bone]
        self.Rd[i] = euler(x, y, z) @ self.Rd[i]
        return self

    def setR(self, bone, R):
        self.Rd[self.rg.idx[bone]] = R
        return self

    def move(self, bone, x=0.0, y=0.0, z=0.0):
        if not np.isscalar(x):
            x, y, z = x
        self.t[self.rg.idx[bone]] += (x, y, z)
        return self

    def scl(self, bone, s):
        i = self.rg.idx[bone]
        self.s[i] = self.s[i] * (np.array([s, s, s], dtype=float) if np.isscalar(s) else np.asarray(s, dtype=float))
        return self

    # ---- forward kinematics
    def Dm(self, i):
        rg = self.rg
        A = self.Rd[i]
        if not np.allclose(self.s[i], 1.0):
            A = A @ (rg.R[i] @ np.diag(self.s[i]) @ rg.R[i].T)
        Mx = np.eye(4)
        Mx[:3, :3] = A
        h = rg.head[i]
        Mx[:3, 3] = h - A @ h + self.t[i]
        return Mx

    def G(self, bone):
        """posed global delta matrix of a bone (4x4)"""
        i = bone if isinstance(bone, (int, np.integer)) else self.rg.idx[bone]
        ch = []
        while i >= 0:
            ch.append(i); i = int(self.rg.par[i])
        G = np.eye(4)
        for j in reversed(ch):
            G = G @ self.Dm(j)
        return G

    def fk(self):
        rg = self.rg
        G = np.zeros((rg.nb, 4, 4))
        for i in rg.order:
            D = self.Dm(i)
            G[i] = D if rg.par[i] < 0 else G[rg.par[i]] @ D
        return G

    def pt(self, bone, p):
        """where a rest-space point rigid to `bone` is now"""
        return (self.G(bone) @ np.r_[np.asarray(p, dtype=float), 1.0])[:3]

    def rotw(self, bone):
        """posed world rotation delta of a bone (scale removed)"""
        A = self.G(bone)[:3, :3]
        u, _, vt = np.linalg.svd(A)
        return u @ vt

    def aim_world(self, bone, Rw=I3):
        """make the bone's world rotation delta equal Rw (I3 = its rest orientation in armature space)"""
        i = self.rg.idx[bone]
        pr = int(self.rg.par[i])
        Rp = self.rotw(pr) if pr >= 0 else I3
        self.Rd[i] = Rp.T @ Rw
        return self

    # ---- inverse kinematics
    def ik2(self, upper, lower, end_rest, target, pole=None, axis=None):
        """two-bone IK: rotate `upper` and `lower` so the rest point end_rest (rigid to lower) reaches target"""
        rg = self.rg
        iu, il = rg.idx[upper], rg.idx[lower]
        self.Rd[iu] = I3; self.Rd[il] = I3
        pu = int(rg.par[iu])
        Gp = self.G(pu) if pu >= 0 else np.eye(4)
        Gp = Gp @ self._Tonly(iu)
        Gi = np.linalg.inv(Gp)
        T = (Gi @ np.r_[np.asarray(target, dtype=float), 1.0])[:3]
        hu, hl = rg.head[iu], rg.head[il]
        e = np.asarray(end_rest, dtype=float)
        a = np.linalg.norm(hl - hu); b = np.linalg.norm(e - hl)
        dv = T - hu
        d0 = np.linalg.norm(dv)
        d = min(max(d0, abs(a - b) + 1e-4), a + b - 1e-4)
        self.err = max(self.err, abs(d0 - d))
        u = (hl - hu) / a; l = (e - hl) / b
        n = np.cross(u, l)
        if np.linalg.norm(n) < 1e-4:
            n = np.asarray(axis if axis is not None else (1.0, 0, 0), dtype=float)
        n = unit(n)
        beta0 = math.acos(max(-1.0, min(1.0, float(u @ l))))
        phi = math.acos(max(-1.0, min(1.0, (a * a + b * b - d * d) / (2 * a * b))))
        beta = math.pi - phi
        R2 = rot_axis(n, math.degrees(beta - beta0))
        e2 = hl + R2 @ (e - hl)
        R1 = M.rot_to(e2 - hu, dv if d0 > 1e-9 else e2 - hu)
        if pole is not None:
            ax = unit(dv)
            k = R1 @ (hl - hu)
            kp = k - ax * (k @ ax)
            pw = Gi[:3, :3] @ np.asarray(pole, dtype=float)
            pp = pw - ax * (pw @ ax)
            if np.linalg.norm(kp) > 1e-5 and np.linalg.norm(pp) > 1e-5:
                kp = unit(kp); pp = unit(pp)
                ang = math.atan2(float(np.cross(kp, pp) @ ax), float(kp @ pp))
                R1 = rot_axis(ax, math.degrees(ang)) @ R1
        self.Rd[iu] = R1
        self.Rd[il] = R2
        return self

    def _Tonly(self, i):
        Mx = np.eye(4)
        Mx[:3, 3] = self.t[i]
        return Mx

    def leg(self, lg, P, R3=I3, R4=I3, pole=None):
        """solve a Leg so its contact point reaches P (armature space). R3 / R4 = world rotation deltas of the 3rd /
        4th bone (I3 = as in the rest pose)"""
        rg = self.rg
        b = lg.b
        P = np.asarray(P, dtype=float)
        if len(b) == 2:
            self.ik2(b[0], b[1], lg.c, P, pole=pole)
        elif len(b) == 3:
            A = P - R3 @ (lg.c - rg.H(b[2]))
            self.ik2(b[0], b[1], rg.H(b[2]), A, pole=pole)
            self.aim_world(b[2], R3)
        else:
            J = P - R4 @ (lg.c - rg.H(b[3]))
            A = J - R3 @ (rg.H(b[3]) - rg.H(b[2]))
            self._glide(lg, A, rg.H(b[2]))
            self.ik2(b[0], b[1], rg.H(b[2]), A, pole=pole)
            self.aim_world(b[2], R3)
            self.aim_world(b[3], R4)
        return self

    def _glide(self, lg, A, end_rest):
        """shift the root of the leg (shoulder blade / hip) towards a target that is out of reach, up to lg.glide"""
        g = getattr(lg, 'glide', 0.0)
        iu = self.rg.idx[lg.b[0]]
        self.t[iu] = 0.0
        if g <= 0:
            return
        rg = self.rg
        pu = int(rg.par[iu])
        Gp = self.G(pu) if pu >= 0 else np.eye(4)
        hs = (Gp @ np.r_[rg.head[iu], 1.0])[:3]
        a = np.linalg.norm(rg.head[rg.idx[lg.b[1]]] - rg.head[iu])
        b = np.linalg.norm(np.asarray(end_rest) - rg.head[rg.idx[lg.b[1]]])
        dv = np.asarray(A, dtype=float) - hs
        d = np.linalg.norm(dv)
        over = d - 0.96 * (a + b)
        if over > 0:
            sh = min(g, over) * dv / d
            self.t[iu] = Gp[:3, :3].T @ sh


class Leg:
    def __init__(self, bones, contact, phase=0.0, name=None, group=0):
        self.b = list(bones)
        self.c = np.asarray(contact, dtype=float)       # rest contact point (rigid to the last bone)
        self.home = self.c.copy()
        self.phase = phase
        self.name = name or bones[-1]
        self.group = group


def gait_foot(lg, u, stride, duty, lift, speed_sign=1.0, lift_pow=1.0, back=0.0):
    """in-place foot target for cycle phase u (0..1): stance = slides back (+Y) by `stride` at constant speed,
    swing = returns forward through an arc of height `lift`.  -> (P, swing 0..1 (0 in stance), sw phase)"""
    ph = (u + lg.phase) % 1.0
    P = lg.home.copy()
    P[1] += back
    if ph < duty:
        s = ph / duty
        P[1] += (-0.5 + s) * stride * speed_sign
        return P, 0.0, 0.0
    s = (ph - duty) / (1.0 - duty)
    e = 0.5 - 0.5 * math.cos(math.pi * s)          # ease in/out
    P[1] += (0.5 - e) * stride * speed_sign
    P[2] += lift * math.sin(math.pi * s) ** lift_pow
    return P, math.sin(math.pi * s), s


def slerpR(R, w):
    """rotation matrix R scaled to the fraction w of its angle"""
    q = Quaternion().slerp(Matrix(np.asarray(R).tolist()).to_quaternion(), float(w))
    return np.array(q.to_matrix())


def plant_legs(p, legs, air=None, off=None, tuck=None):
    """legs: {name: Leg with .par}. Feet stay on their ground homes; air {name or first letter: 0..1} lets a leg
    follow the body instead (1 = hangs as in the rest pose, folded by tuck {name: (upper, lower, foot) degrees about
    X}); off {name: (dx,dy,dz)} shifts a planted foot. Blending is done on the foot target, so there are no pops."""
    def get(d, name, default):
        if d is None:
            return default
        if not isinstance(d, dict):
            return d
        return d.get(name, d.get(name[0], default))
    rg = p.rg
    for name, lg in legs.items():
        w = float(get(air, name, 0.0))
        o = V(get(off, name, (0, 0, 0)))
        tk = get(tuck, name, None)
        if w <= 0.0:
            p.leg(lg, lg.home + o)
            continue
        for b in lg.b:
            p.Rd[rg.idx[b]] = I3
        if tk:
            for b, a in zip(lg.b, tk):
                if a:
                    p.rot(b, x=a)
        if w >= 0.999:
            continue
        Pf = p.pt(lg.b[-1], lg.c)
        R3 = slerpR(p.rotw(lg.b[2]), w) if len(lg.b) > 2 else I3
        R4 = slerpR(p.rotw(lg.b[3]), w) if len(lg.b) > 3 else I3
        p.leg(lg, (lg.home + o) * (1 - w) + Pf * w, R3=R3, R4=R4)


# ------------------------------------------------------------------------------------------------ weights
def wbones(part, bones, sharp=4.0, k=3, bias=None):
    """skin a Part to `bones` by inverse-power distance to their segments. bias {bone: factor>1 = stronger}"""
    R = M.RIG
    D = np.stack([seg_dist(part.v, R.head[b], R.tail[b]) for b in bones], axis=1)
    w = 1.0 / np.maximum(D, 1e-4) ** sharp
    if bias:
        for j, b in enumerate(bones):
            if b in bias:
                w[:, j] *= bias[b]
    if len(bones) > k:
        thr = -np.partition(-w, k - 1, axis=1)[:, k - 1:k]
        w = np.where(w >= thr, w, 0)
    w /= w.sum(1, keepdims=True)
    W = np.zeros((len(part.v), R.nb), dtype=np.float32)
    for j, b in enumerate(bones):
        W[:, R.idx[b]] += w[:, j].astype(np.float32)
    part.W = W
    part.bone = None
    return part


def wrigid(part, bone):
    part.bone = bone
    part.W = None
    return part


def two_sided(part, gap=0.002):
    """thin sheet -> front and back faces (the game shader culls back faces)"""
    n = part.normals()
    a = part.copy(); a.v = part.v + n * gap * 0.5
    b = part.copy(); b.v = part.v - n * gap * 0.5; b.flip()
    q = M.join([a, b])
    q.bone, q.look, q.obj, q.smooth = part.bone, part.look, part.obj, part.smooth
    if part.W is None:
        q.W = None
    q.two = True
    return q


# ------------------------------------------------------------------------------------------------ creature
class Beast(M.Creature):
    def __init__(self, cid, sk, budget=15000, bone_budget=60, mb=6.0, tex=2048):
        self.id = cid
        self.budget = budget
        self.bone_budget = bone_budget
        self.mb = mb
        self.tex = tex
        self.scene = M.reset_scene()
        self.arm = sk.build()
        self.rig = M.RIG
        self.rig.idx.setdefault('pelvis', self.rig.idx.get('body', 0))     # monlib's fallback bone for stray vertices
        self.rg = Rg(self.arm)
        self.parts = []
        self.looks = {}
        self.notes = []
        self.t0 = time.time()
        self.clips = {}
        self.legs = []
        self.meta = dict(model=cid)
        self.free = []          # parts that are meant to be detached (audit skips them)

    def rigid(self, part, bone, look, smooth=None, free=False):
        part.bone = bone; part.W = None
        if not getattr(part, 'two', False):
            part.recalc()
        p = self.add(part, look=look, smooth=smooth)
        if free:
            self.free.append(p)
        return p

    def skin(self, part, look, smooth=None):
        assert part.W is not None
        part.bone = None
        if not getattr(part, 'two', False):
            part.recalc()
        return self.add(part, look=look, smooth=smooth)

    def _make_objects(self):
        """as monlib.Creature._make_objects, but face orientation comes from the parts (recalculated when they are
        added); a mesh-wide recalc would flip one side of the two-sided sheets"""
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
                    Ws.append(M.rigid_W(len(p.v), p.bone))
                else:
                    q = M.Part(p.v, [], p.W.copy()); M.normalize_weights(q); Ws.append(q.W)
                fl += [p.look] * len(p.f)
                fa += [p.smooth] * len(p.f)
            Vall = np.vstack(Vs); Wall = np.vstack(Ws)
            me = bpy.data.meshes.new(name)
            me.from_pydata(Vall.tolist(), [], Fs)
            me.update()
            assert len(me.polygons) == len(Fs), "%s: %d faces lost building the mesh" % (name, len(Fs) - len(me.polygons))
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
            bm = bmesh.new(); bm.from_mesh(me)
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
            bmesh.ops.triangulate(bm, faces=bm.faces[:])
            bm.to_mesh(me); bm.free()
            me.update()
            M._write_hem(me)
            M._write_groups(ob, Wall)
            ob.parent = self.arm
            md = ob.modifiers.new("Armature", 'ARMATURE')
            md.object = self.arm
            objs.append(ob)
        self.objs = objs
        return objs

    def pose(self):
        return Pose(self.rg)

    # ---- clips
    def clip(self, name, dur, fn, hit=None, under_ok=False):
        """fn(p, u): fill Pose p for normalised time u (0..1). Looping clips get frame N = frame 0."""
        n = max(2, int(round(dur * FPS)))
        loop = name in LOOPS
        nb = self.rg.nb
        Q = np.zeros((n + 1, nb, 4)); T = np.zeros((n + 1, nb, 3)); S = np.ones((n + 1, nb, 3))
        Gs = np.zeros((n + 1, nb, 4, 4))
        err = 0.0
        rg = self.rg
        for f in range(n + 1):
            u = f / n
            if loop and f == n:
                u = 0.0
            p = Pose(rg)
            p.u = u; p.dur = dur; p.time = u * dur
            fn(p, u)
            err = max(err, p.err)
            Gs[f] = p.fk()
            for i in range(nb):
                Rb = rg.R[i].T @ p.Rd[i] @ rg.R[i]
                q = Matrix(Rb.tolist()).to_quaternion()
                Q[f, i] = (q.w, q.x, q.y, q.z)
                T[f, i] = rg.R[i].T @ p.t[i]
            S[f] = p.s
        for f in range(1, n + 1):                       # keep quaternion signs continuous
            flip = (Q[f] * Q[f - 1]).sum(1) < 0
            Q[f][flip] *= -1
        if loop:
            flip = (Q[n] * Q[0]).sum(1) < 0
            # a sign flip between first and last frame is the same rotation; make the keys identical for the importer
            Q[n] = Q[0]
        assert np.isfinite(Q).all() and np.isfinite(T).all() and np.isfinite(S).all(), "NaN in clip " + name
        self.clips[name] = dict(dur=n / FPS, n=n, Q=Q, T=T, S=S, G=Gs, hit=hit, err=err, under_ok=under_ok)
        if err > 0.02:
            log("NOTE %s %s: IK target out of reach by %.3f m" % (self.id, name, err))
        return self.clips[name]

    def _write_actions(self):
        arm = self.arm
        if arm.animation_data is None:
            arm.animation_data_create()
        A = arm.animation_data
        rg = self.rg
        for name in CLIPS:
            c = self.clips[name]
            act = bpy.data.actions.new(name)
            A.action = act
            n = c['n']
            fr = np.arange(n + 1, dtype=np.float32)

            def put(path, idx, vals):
                fc = act.fcurve_ensure_for_datablock(arm, path, index=idx)
                fc.keyframe_points.add(n + 1)
                co = np.empty((n + 1) * 2, dtype=np.float32)
                co[0::2] = fr; co[1::2] = vals
                fc.keyframe_points.foreach_set('co', co)
                fc.update()
            for i, bn in enumerate(rg.names):
                base = 'pose.bones["%s"].' % bn
                for k in range(4):
                    put(base + 'rotation_quaternion', k, c['Q'][:, i, k])
                if np.abs(c['T'][:, i]).max() > 1e-6:
                    for k in range(3):
                        put(base + 'location', k, c['T'][:, i, k])
                if np.abs(c['S'][:, i] - 1).max() > 1e-6:
                    for k in range(3):
                        put(base + 'scale', k, c['S'][:, i, k])
            A.action = None
            tr = A.nla_tracks.new(); tr.name = name
            st = tr.strips.new(name, 0, act); st.name = name
            try:
                if st.action_slot is None and len(act.slots):
                    st.action_slot = act.slots[0]
            except Exception as e:
                log("slot", e)
            tr.mute = True

    # ---- skinning on the numeric side (audit, bounds, foot check)
    def _collect(self):
        Vs = []; Ws = []; comp = []; tris = []; off = 0
        R = self.rig
        for k, p in enumerate(self.parts):
            Vs.append(p.v)
            if p.bone is not None:
                Ws.append(M.rigid_W(len(p.v), p.bone))
            else:
                q = M.Part(p.v, [], p.W.copy()); M.normalize_weights(q); Ws.append(q.W)
            comp += [k] * len(p.v)
            t = p.tris()
            tris.append(t + off)
            off += len(p.v)
        return np.vstack(Vs), np.vstack(Ws).astype(np.float64), np.array(comp), np.vstack(tris)

    def posed(self, V0, W, G):
        vh = np.c_[V0, np.ones(len(V0))]
        used = np.where(W.max(0) > 0)[0]
        out = np.zeros((len(V0), 3))
        for b in used:
            out += W[:, b:b + 1] * (vh @ G[b].T)[:, :3]
        return out

    def audit(self, frames_per_clip=5):
        """floating-part audit (every connected piece of every rigid part must touch or enter a host: skinned geometry
        or a clearly larger rigid piece) in the rest pose and in sampled frames of every clip + ground bounds"""
        V0, W, comp0, tris = self._collect()
        R = self.rig
        free = set(id(p) for p in self.free)
        # connected pieces
        piece = np.zeros(len(V0), dtype=np.int64)
        info = []           # per piece: (part index, bone or None, size, skip)
        off = 0
        for k, p in enumerate(self.parts):
            n = len(p.v)
            par = np.arange(n)

            def find(i):
                while par[i] != i:
                    par[i] = par[par[i]]; i = par[i]
                return i
            if p.bone is not None:
                for f in p.f:
                    r0 = find(f[0])
                    for j in f[1:]:
                        rj = find(j)
                        if rj != r0:
                            par[rj] = r0
                roots = np.array([find(i) for i in range(n)])
            else:
                roots = np.zeros(n, dtype=np.int64)
            for r in np.unique(roots):
                ids = np.where(roots == r)[0]
                piece[off + ids] = len(info)
                bb = p.v[ids]
                info.append((k, p.bone, float(np.linalg.norm(bb.max(0) - bb.min(0))), id(p) in free or p.bone is None))
            off += n
        tpiece = piece[tris[:, 0]]
        size = np.array([x[2] for x in info]); skinned = np.array([x[1] is None for x in info])
        rng = np.random.default_rng(3)

        def near(bvh, P, k):
            best = 9.0
            bi = None
            for loc, nrm, ti, dist in bvh.find_nearest_range(Vector(P), 0.06):
                j = tpiece[ti]
                if j == k or not (skinned[j] or size[j] > 1.2 * size[k]):
                    continue
                if dist < best:
                    best = dist; bi = (loc, nrm)
            if bi is not None and (Vector(P) - bi[0]).dot(bi[1]) < -1e-5:
                return 0.0
            return best

        def mk_bvh(P):
            return BVHTree.FromPolygons([tuple(x) for x in P], [tuple(int(i) for i in t) for t in tris])
        bvh0 = mk_bvh(V0)
        cps = {}
        g0 = {}
        res = dict(rest_float=[], clip_float={}, minz={}, maxz={})
        off = 0
        offs = []
        for p in self.parts:
            offs.append(off); off += len(p.v)
        for k, (pi, bone, sz, skip) in enumerate(info):
            if skip:
                continue
            p = self.parts[pi]
            ids = np.where(piece[offs[pi]:offs[pi] + len(p.v)] == k)[0]
            idset = set(ids.tolist())
            pts = [p.v[ids]]
            es = set()
            for f in p.f:
                if f[0] in idset:
                    for a_ in range(len(f)):
                        i, j = f[a_], f[(a_ + 1) % len(f)]
                        es.add((min(i, j), max(i, j)))
            if es:
                es = np.array(sorted(es))
                for tt in (0.25, 0.5, 0.75):
                    pts.append(p.v[es[:, 0]] * (1 - tt) + p.v[es[:, 1]] * tt)
            pts = np.vstack(pts)
            if len(pts) > 300:
                pts = pts[rng.choice(len(pts), 300, replace=False)]
            d = np.array([near(bvh0, q, k) for q in pts])
            g0[k] = float(d.min())
            cps[k] = pts[np.argsort(d)[:12]]
            if g0[k] > 0.006:
                res['rest_float'].append((bone, p.look, round(g0[k], 3), [round(float(x), 2) for x in pts.mean(0)]))
        res['rest_minz'] = float(V0[:, 2].min())
        for name in CLIPS:
            c = self.clips[name]
            n = c['n']
            fs = sorted(set(int(round(x)) for x in np.linspace(0, n, frames_per_clip)))
            worst = {}
            mz = 9.0; Mz = -9.0; endz = 0.0
            for f in fs:
                P = self.posed(V0, W, c['G'][f])
                mz = min(mz, float(P[:, 2].min())); Mz = max(Mz, float(P[:, 2].max()))
                if f == n:
                    endz = float(P[:, 2].min())
                bvh = mk_bvh(P)
                for k, cp in cps.items():
                    if g0[k] > 0.006:
                        continue
                    Gb = c['G'][f][R.idx[info[k][1]]]
                    q = np.c_[cp, np.ones(len(cp))] @ Gb.T
                    d = min(near(bvh, x[:3], k) for x in q)
                    if d > 0.012:
                        worst[k] = max(worst.get(k, 0), d)
            res['clip_float'][name] = [(info[k][1], self.parts[info[k][0]].look, round(d, 3)) for k, d in worst.items()]
            res['minz'][name] = round(mz, 3); res['maxz'][name] = round(Mz, 3)
            if name == 'Death':
                res['death_end_minz'] = round(endz, 3)
        self.audit_res = res
        return res

    def end_minz(self, clip, frame=-1):
        """lowest point of the posed mesh at a frame of a clip (default: the last)"""
        V0, W, comp, tris = self._collect()
        c = self.clips[clip]
        P = self.posed(V0, W, c['G'][frame])
        return float(P[:, 2].min())

    def reach(self, clip, bone, pt):
        """how far forward of the origin (m) a striker point (rest coords, rigid to bone) is at the hit frame"""
        c = self.clips[clip]
        f = min(c['n'], int(round(c['hit'] * FPS)))
        q = c['G'][f][self.rg.idx[bone]] @ np.r_[np.asarray(pt, dtype=float), 1.0]
        r = round(float(-q[1]), 3)
        self.meta['attack_reach'] = max(self.meta.get('attack_reach', 0.0), r)
        return r

    def foot_check(self):
        """planted-foot drift (m) in Walk / Run when the root travels at the stated speed"""
        out = {}
        for name, key in (("Walk", "walk_speed"), ("Run", "run_speed")):
            c = self.clips[name]
            v = self.meta.get(key, 0.0)
            worst = 0.0
            for lg in self.legs:
                i = self.rg.idx[lg.b[-1]]
                ph = np.r_[lg.c, 1.0]
                P = np.array([(c['G'][f][i] @ ph)[:3] for f in range(c['n'] + 1)])
                t = np.arange(c['n'] + 1) / FPS
                Pw = P.copy(); Pw[:, 1] -= v * t            # the creature walks towards -Y
                zmin = P[:, 2].min()
                st = P[:, 2] < zmin + 0.012
                # split the planted frames into runs and measure the drift inside each run
                f = 0
                while f <= c['n']:
                    if st[f]:
                        g = f
                        while g + 1 <= c['n'] and st[g + 1]:
                            g += 1
                        if g > f:
                            seg = Pw[f:g + 1, :2]
                            worst = max(worst, float(np.linalg.norm(seg.max(0) - seg.min(0))))
                        f = g + 1
                    else:
                        f += 1
            out[name] = round(worst, 4)
        return out

    # ---- finish
    def finalize(self, bake=True, export=True, notes=""):
        t0 = time.time()
        missing = [c for c in CLIPS if c not in self.clips]
        assert not missing, "missing clips: %s" % missing
        per = self.tri_report()
        tot = sum(per.values())
        log("%s triangles %d (budget %d), bones %d (budget %d)" % (self.id, tot, self.budget, self.rg.nb, self.bone_budget))
        if tot > self.budget:
            log("WARNING: OVER TRIANGLE BUDGET by", tot - self.budget)
        if self.rg.nb > self.bone_budget:
            log("WARNING: OVER BONE BUDGET by", self.rg.nb - self.bone_budget)
        au = self.audit()
        if au['rest_float']:
            log("WARNING floating in rest pose:", au['rest_float'])
        for k, v in au['clip_float'].items():
            if v:
                log("WARNING detaches in %s:" % k, v)
        fc = self.foot_check() if self.legs else {}
        log("ground: rest min z %.3f; per clip min z %s; death end min z %.3f; foot drift %s" % (
            au['rest_minz'], au['minz'], au.get('death_end_minz', 0.0), fc))
        V0, W, comp, tris = self._collect()
        idle = self.posed(V0, W, self.clips['Idle']['G'][0])
        mn, mx = idle.min(0), idle.max(0)
        m = self.meta
        m.setdefault('radius', round(float((mx[0] - mn[0]) + (mx[1] - mn[1])) / 4.0, 3))
        m.setdefault('height', round(float(mx[2]), 3))
        m['attack1_hit_time'] = round(float(self.clips['Attack1']['hit']), 3)
        m['attack2_hit_time'] = round(float(self.clips['Attack2']['hit']), 3)
        m['clips'] = {k: round(self.clips[k]['dur'], 3) for k in CLIPS}
        m['triangles'] = tot; m['bones'] = self.rg.nb
        m['bbox_min'] = [round(float(x), 3) for x in mn]; m['bbox_max'] = [round(float(x), 3) for x in mx]
        m['foot_drift'] = fc
        m['audit'] = dict(rest_float=au['rest_float'], clip_float={k: v for k, v in au['clip_float'].items() if v},
                          minz=au['minz'], death_end_minz=au.get('death_end_minz'))
        m['notes'] = notes
        objs = self._make_objects()
        if bake:
            L.unwrap(objs)
            qn = self.meta.pop('_qn', 90)
            _orig = L._save_jpg

            def _sj(name, arr, noncolor, quality, full_chroma=False):
                if name.endswith('_nrm'):
                    quality = qn
                elif name.endswith('_orm'):
                    quality = 84
                elif name.endswith('_bc') or name.endswith('_em'):
                    quality = 88
                return _orig(name, arr, noncolor, quality, full_chroma)
            L._save_jpg = _sj
            try:
                L.bake_and_assign(self, objs, size=self.tex, mask_size=max(512, self.tex // 2))
            finally:
                L._save_jpg = _orig
        else:
            L.assign_preview_slots(self, objs)
        self._write_actions()
        m['materials'] = sorted(getattr(self, 'slot_mats', {}).keys())
        if export:
            path = os.path.join(OUT_DIR if bake else WORK, self.id + ".glb")
            export_glb(path, self.arm, objs)
            m['bytes'] = os.path.getsize(path)
            log("WROTE %s %.2f MB" % (path, m['bytes'] / 1048576.0))
            if m['bytes'] > self.mb * 1048576:
                log("WARNING: OVER SIZE BUDGET %.2f MB > %.1f" % (m['bytes'] / 1048576.0, self.mb))
        with open(os.path.join(WORK, self.id + ".meta.json"), 'w', encoding='utf8') as f:
            json.dump(m, f, indent=1)
        log("done %s in %.1fs (finalize %.1fs) tris=%d bones=%d" % (self.id, time.time() - self.t0, time.time() - t0, tot, self.rg.nb))
        return m


def export_glb(path, arm, objs):
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.context.scene.frame_set(0)
    bpy.ops.export_scene.gltf(
        filepath=path, export_format='GLB', use_selection=True,
        export_animations=True, export_animation_mode='ACTIONS', export_nla_strips=True,
        export_force_sampling=True, export_frame_step=1, export_optimize_animation_size=True,
        export_optimize_animation_keep_anim_armature=True,
        export_anim_single_armature=True, export_reset_pose_bones=True, export_rest_position_armature=True,
        export_skins=True, export_def_bones=False, export_leaf_bone=False, export_yup=True, export_apply=True,
        export_materials='EXPORT', export_image_format='JPEG', export_jpeg_quality=92, export_image_quality=92,
        export_tangents=True, export_normals=True, export_texcoords=True,
        export_cameras=False, export_lights=False, export_morph=False, export_extras=False,
        export_vertex_color='NONE', export_all_vertex_colors=False,
    )
