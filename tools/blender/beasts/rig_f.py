"""Rig F - rigid / floating things. Every creature has its own small skeleton (built with blib.Sk directly); this
module only holds the shared motion helpers. Floaters are authored at their float height (meta fly_height,
hover_in_model = true): Death falls to the ground, Spawn drops in. Turrets stand on the ground; their Walk / Run are
copies of Idle (meta walk_speed = run_speed = 0).

Added by helper H4 (all additive, defaults unchanged): Rigid.turn / scale_axes / hide / turret, HangChain (world-space
pendulum chains that lie down on the ground), stepped (ratchet interpolation), flight (ballistic pieces), low_point,
facet_tube (cut-gem loft), core (skinned main body for the audit), from_pose (one-shot clips that start / end in Idle 0),
keyed / clank (lid curves)."""
import math
import numpy as np
import blib as B
from blib import M, V, unit, sm, bump, osc, rx, ry, rz, euler, I3
import bparts as P


class Rigid:
    def __init__(self, C, bone='body', pivot=None, hover=None):
        self.C = C
        self.bone = bone
        rg = C.rg
        self.pivot = (rg.H(bone) + rg.T(bone)) / 2 if pivot is None else V(pivot)
        if hover is not None:
            C.meta.update(fly_height=round(float(hover), 3), hover_in_model=True)

    def body(self, p, dx=0.0, dy=0.0, dz=0.0, pitch=0.0, roll=0.0, yaw=0.0, bone=None, pivot=None):
        """move / turn a bone about a pivot (default: the main body about its middle)"""
        b = bone or self.bone
        R = euler(pitch, roll, yaw)
        h = self.C.rg.H(b)
        pv = self.pivot if pivot is None else np.asarray(pivot, dtype=float)
        p.setR(b, R)
        p.move(b, (pv - h) - R @ (pv - h) + V(dx, dy, dz))

    def dangle(self, p, bones, x=0.0, y=0.0, u=0.0, amp=0.0, n=1.0, lag=0.12, amp_y=None, ph=0.0, grow=0.0):
        """hanging chain: a constant lean (x = trails towards +Y, y = towards -X... degrees, shared over the links)
        plus a pendulum wave that lags down the chain"""
        k = max(len(bones), 1)
        for i, b in enumerate(bones):
            g = 1.0 + grow * i
            p.rot(b, x=x / k + amp * g * osc(u, n, ph - i * lag), y=y / k + (amp if amp_y is None else amp_y) * g * osc(u, n, ph + 0.25 - i * lag))

    def copy_clip(self, src, name, speed_key=None, v=0.0):
        """turrets: Walk / Run are the Idle clip under another name"""
        c = dict(self.C.clips[src])
        self.C.clips[name] = c
        if speed_key:
            self.C.meta[speed_key] = v
        return c

    # ---- H4 additions
    def turn(self, p, R, pivot=None, bone=None, d=(0.0, 0.0, 0.0)):
        """set a bone's rotation to the matrix R about a pivot point (rest coordinates) and shift it by d
        (as body(), for rotations that are not pitch / roll / yaw: orbits, toppling about a line of feet)"""
        b = bone or self.bone
        h = self.C.rg.H(b)
        pv = self.pivot if pivot is None else np.asarray(pivot, dtype=float)
        R = np.asarray(R, dtype=float)
        p.setR(b, R)
        p.move(b, (pv - h) - R @ (pv - h) + V(d))

    def turret(self):
        """Walk and Run of a turret = its Idle clip, speeds 0"""
        self.copy_clip('Idle', 'Walk', 'walk_speed', 0.0)
        self.copy_clip('Idle', 'Run', 'run_speed', 0.0)


def scale_axes(p, bone, sx=1.0, sy=1.0, sz=1.0):
    """scale a bone along the ARMATURE axes of its rest pose (exact for bones that lie along an axis with roll 0;
    the scale is carried along by the bone's own rotation like every bone scale)"""
    R = p.rg.R[p.rg.idx[bone]]
    p.scl(bone, np.abs(R).T @ np.array([sx, sy, sz], dtype=float))


def hide(p, bones, s=0.001):
    """shrink bones to a point (parts that only show in some clips)"""
    for b in ([bones] if isinstance(bones, str) else bones):
        p.scl(b, s)


def fall_curve(u, land):
    """0..1 height factor of a free fall that lands at u = land (1 = still up, 0 = down)"""
    t = min(1.0, u / land)
    return 1.0 - t * t


def chain_parts(C, bones, look, link=0.045, r=0.006, w=0.62, n=6, m=4, extend=0.0):
    """chain links along a bone chain, each link rigid to the bone it hangs on"""
    rg = C.rg
    for i, b in enumerate(bones):
        a = rg.H(b); e = rg.T(b)
        if i == len(bones) - 1 and extend:
            e = e + unit(e - a) * extend
        ch = M.chain([a, e], link=link, r=r, w=w, n=n, m=m)
        C.rigid(ch, b, look, smooth=60)


# ------------------------------------------------------------------------------------------------ H4 additions
def core(C, part, bone, look, smooth=180):
    """the main body as SKINNED geometry on one bone: the audit grows its attachment tree from skinned geometry
    (same trick as helper H1's core())"""
    part.W = M.rigid_W(len(part.v), bone)
    return C.skin(part, look, smooth=smooth)


def stepped(u, vals, move=0.3, over=0.0, rings=2.0):
    """ratchet interpolation through `vals` (evenly spaced over u = 0..1; for a loop the last value = the first):
    each slot moves during its first `move` fraction and then holds, ringing a little (`over` = fraction of the step)
    after it stops - clicking machinery"""
    n = len(vals) - 1
    x = min(max(u, 0.0), 1.0) * n
    i = min(int(x), n - 1)
    f = x - i
    a, b = vals[i], vals[i + 1]
    t = sm(f, 0.0, move)
    v = a + (b - a) * t
    if over and f > move and a != b:
        g = (f - move) / max(1e-6, 1.0 - move)
        v += (b - a) * over * math.exp(-5.0 * g) * math.sin(2 * math.pi * rings * g)
    return v


def keyed(u, ts, vs):
    """value at u of a curve through the keys (ts ascending, vs their values) that eases in and out of every key"""
    if u <= ts[0]:
        return vs[0]
    for i in range(len(ts) - 1):
        if u <= ts[i + 1]:
            return vs[i] + (vs[i + 1] - vs[i]) * sm(u, ts[i], ts[i + 1])
    return vs[-1]


def clank(u, t0, rate=40.0, freq=25.0):
    """0..1 rattle of something that has just dropped shut at u = t0: decaying bounces (never negative)"""
    if u <= t0:
        return 0.0
    return math.exp(-rate * (u - t0)) * abs(math.sin(2 * math.pi * freq * (u - t0)))


def flight(t, v, z0, zl, g=9.8):
    """ballistic offset at time t (s) of a piece thrown with velocity v (m/s) from height z0 that lands on height
    zl and stays there -> (offset (3,), time of landing)"""
    tl = (v[2] + math.sqrt(max(v[2] * v[2] + 2.0 * g * (z0 - zl), 0.0))) / g
    tt = min(max(t, 0.0), tl)
    off = V(v[0] * tt, v[1] * tt, v[2] * tt - 0.5 * g * tt * tt)
    if t >= tl:
        off[2] = zl - z0
    return off, tl


def low_point(part, R, pivot):
    """lowest z (relative to the pivot) of a Part turned by the matrix R about the pivot: how high the pivot of a
    fallen piece has to end so that the piece lies on the ground"""
    q = (part.v - np.asarray(pivot, dtype=float)) @ np.asarray(R, dtype=float).T
    return float(q[:, 2].min())


class HangChain:
    """chain / rope / limp limb hanging by gravity from its first bone's head (a point on `parent`): the links are
    aimed in WORLD space, so they stay plumb when the parent tilts, swing as a lagging pendulum and lie down on the
    ground when they reach it (the bell's chains in its drop-slam, corpses). Call pose() after the parent is posed."""

    def __init__(self, C, bones, parent, ground=0.015):
        rg = C.rg
        self.b = list(bones)
        self.parent = parent
        self.h0 = rg.H(bones[0])
        self.d0 = [unit(rg.T(b) - rg.H(b)) for b in bones]
        self.l = [float(np.linalg.norm(rg.T(b) - rg.H(b))) for b in bones]
        self.ground = ground

    def pose(self, p, sx=0.0, sy=0.0, u=0.0, amp=0.0, n=1.0, lag=0.12, ph=0.0, amp_x=None, trail=(0.0, 1.0), fold=0.0,
             grow=0.0, down=(0.0, 0.0, -1.0), w=1.0, off=(0.0, 0.0, 0.0), bend=None):
        """sx / sy = lean (tangent of the angle: + = towards +X / +Y) of every link; amp = pendulum wave (same unit)
        that lags down the chain (amp_x = sideways part, default = amp); trail = horizontal direction the links lie
        along on the ground (fold 0..1 = every second link lies the other way round, turned there through
        180 x fold degrees: a limb that crumples under its own weight); down = the direction of "gravity" (rest pose:
        straight down); w < 1 blends towards the rest pose; off = the hanging point shifted in the parent's rest
        space (a body that slides out of its fastening); bend = extra lean per link [(sx, sy), ...]"""
        off = V(off)
        if float(np.abs(off).max()) > 0.0:
            p.move(self.b[0], off)
        h = p.pt(self.parent, self.h0 + off)
        tr = unit(V(trail[0], trail[1], 0.0))
        dn = unit(V(down))
        for i, b in enumerate(self.b):
            g = 1.0 + grow * i
            ax = sx + (amp if amp_x is None else amp_x) * g * osc(u, n, ph + 0.25 - i * lag)
            ay = sy + amp * g * osc(u, n, ph - i * lag)
            if bend is not None and i < len(bend):
                ax += bend[i][0]; ay += bend[i][1]
            d = unit(dn + V(ax, ay, 0.0))
            l = self.l[i]
            end = h[2] + d[2] * l
            if end < self.ground:
                dz = h[2] - self.ground
                t = (rz(180.0 * float(fold)) @ tr) if (fold and i % 2) else tr
                pen = (self.ground - end) / l
                hd = V(d[0], d[1], 0.0) + t * (0.02 + 2.5 * pen)
                hd = unit(hd)
                if dz <= 1e-4:
                    d = unit(V(hd[0], hd[1], min(0.4, -dz / l)))
                else:
                    dz = min(dz, l)
                    k = math.sqrt(max(l * l - dz * dz, 0.0))
                    d = V(hd[0] * k, hd[1] * k, -dz) / l
            Rw = M.rot_to(self.d0[i], d)
            wi = float(w) if np.isscalar(w) else float(w[i])        # w may be one value or one per link (a stalk that wilts from the tip)
            if wi < 1.0:
                Rw = B.slerpR(Rw, max(wi, 0.0))
            p.aim_world(b, Rw)
            h = h + (Rw @ self.d0[i]) * l


def from_pose(C, fn, ref, a=0.1, b=0.1):
    """wrap a one-shot clip function so that it starts (over its first `a`) and ends (over its last `b`) exactly in
    the pose ref(p) - normally Idle at u = 0 (`lambda p: idle(p, 0.0)`). a = 0 / b = 0 leaves that end alone
    (Death: b = 0, Spawn: a = 0)"""
    rg = C.rg
    q = B.Pose(rg)
    q.u = 0.0; q.dur = 1.0; q.time = 0.0
    ref(q)

    def g(p, u):
        fn(p, u)
        k = max((1.0 - sm(u, 0.0, a)) if a > 0 else 0.0, sm(u, 1.0 - b, 1.0) if b > 0 else 0.0)
        if k <= 0.0:
            return
        for i in range(rg.nb):
            if k >= 1.0:
                p.Rd[i] = q.Rd[i]
            else:
                D = p.Rd[i].T @ q.Rd[i]
                if not np.allclose(D, I3, atol=1e-9):
                    p.Rd[i] = p.Rd[i] @ B.slerpR(D, k)
        p.t = p.t * (1.0 - k) + q.t * k
        p.s = p.s * (1.0 - k) + q.s * k
    return g


def facet_tube(path, rv, rh=None, n=6, m=None, twist=0.0, up=(0, -1, 0), jitter=0.0, seed=0):
    """cut-gem tube: rings of n points along a path, every other ring turned by half a step and joined by triangles
    (antiprism strips), closed with points at both ends. rv / rh = radii at the control points of `path` (frame
    normal / binormal); m = number of rings (default: one per control point)"""
    ctrl = np.asarray(path, dtype=float)
    m = len(ctrl) if m is None else m
    Pth = M.polyline(ctrl, m) if m != len(ctrl) else ctrl
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(ctrl, axis=0), axis=1))]
    d /= d[-1]
    t = np.linspace(0, 1, m) if m != len(ctrl) else d
    a = np.interp(t, d, np.broadcast_to(np.asarray(rv, dtype=float), (len(ctrl),)))
    b = a if rh is None else np.interp(t, d, np.broadcast_to(np.asarray(rh, dtype=float), (len(ctrl),)))
    T, N, Bn = M._frames(Pth, np.asarray(up, dtype=float), False)
    rng = np.random.default_rng(seed)
    vs = [Pth[0]]
    for i in range(1, m - 1):
        ph = twist * i / (m - 1) + (math.pi / n if i % 2 else 0.0)
        for j in range(n):
            an = ph + 2 * math.pi * j / n
            k = 1.0 + (rng.uniform(-jitter, jitter) if jitter else 0.0)
            vs.append(Pth[i] + (N[i] * math.cos(an) * a[i] + Bn[i] * math.sin(an) * b[i]) * k)
    vs.append(Pth[-1])
    f = []
    last = len(vs) - 1

    def ring(i):
        return 1 + (i - 1) * n
    for j in range(n):
        f.append((0, ring(1) + (j + 1) % n, ring(1) + j))
    for i in range(1, m - 2):
        r0, r1 = ring(i), ring(i + 1)
        for j in range(n):
            j2 = (j + 1) % n
            if i % 2:           # ring i is the turned one: its point j sits between j and j+1 of ring i+1
                f.append((r0 + j, r1 + j2, r1 + j))
                f.append((r0 + j, r0 + j2, r1 + j2))
            else:
                f.append((r0 + j, r0 + j2, r1 + j))
                f.append((r0 + j2, r1 + j2, r1 + j))
    r0 = ring(m - 2)
    for j in range(n):
        f.append((r0 + j, r0 + (j + 1) % n, last))
    q = M.Part(np.array(vs), f)
    q.recalc()
    q.smooth = 0.0
    return q
