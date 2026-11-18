"""Rig D - serpents. Three sub-rigs, all chains of equal bones:
  Column  (burrowers: eel, ash-worm, cinder-root, pale borer)  root > s1..sN (bottom, buried -> top) > head [> jaws ...]
          The base stays buried; Walk / Run is the leaning, weaving column ploughing through the ground.
  Rearer  (spitter, elder leech)  root > body > neck1..k > head ;  body > tail1..m  (the tail lies on the ground and
          undulates sideways; the reared front stays steady).  Speed = wave speed of the tail.
  Looper  (leech, grave-grub)  root > s1..sN (rear -> front) > head.  Two contact points (rear sucker, mouth) follow
          gait targets, the body between them is a circular arc of fixed length: inchworm loops with planted ends.
"""
import math
import numpy as np
import blib as B
from blib import M, V, unit, sm, bump, osc, rx, ry, rz, euler, I3, Leg, gait_foot
import bparts as P


# ================================================================================================ skeletons
def column_skel(pts, head_tip=None, extra=None):
    """pts: bottom (buried) -> top joint positions; head_tip: end of the head bone (None = no head bone)"""
    sk = B.Sk()
    names = sk.chain('s', pts, 'root')
    if head_tip is not None:
        sk.bone('head', pts[-1], head_tip, names[-1])
    if extra:
        extra(sk, names)
    return sk


def rearer_skel(bend, neck, tail, head_tip, extra=None):
    """bend: the point where the body leaves the ground; neck: joints from bend up to the head joint;
    tail: joints from bend back along the ground to the tail tip"""
    sk = B.Sk()
    sk.bone('body', bend, V(bend) + V(0, -0.1, 0.02), 'root')
    nk = sk.chain('neck', [bend] + list(neck), 'body')
    sk.bone('head', neck[-1], head_tip, nk[-1])
    sk.chain('tail', [bend] + list(tail), 'body')
    if extra:
        extra(sk)
    return sk


def looper_skel(length, n, z, head=0.0, extra=None):
    """straight chain along -Y, centred on the origin, bone axis at height z; head > 0 adds a head bone in front"""
    sk = B.Sk()
    pts = [V(0, length / 2 - length * i / n, z) for i in range(n + 1)]
    names = sk.chain('s', pts, 'root')
    if head:
        sk.bone('head', pts[-1], pts[-1] + V(0, -head, 0), names[-1])
    if extra:
        extra(sk, names)
    return sk


# ================================================================================================ geometry
def chain_tube(pts, rv, rh=None, bones=None, m=None, n=12, up=(0, -1, 0), sharp=3.0, cap0=True, cap1=True, ridges=None, power=2.0):
    """skinned tube through pts. ridges = (count, depth, sharpness 0..1): saw-tooth / ring relief along the tube
    (plates overlapping like roof tiles when sharpness is high)"""
    pts = np.asarray(pts, dtype=float)
    m = m or max(10, 3 * len(pts))
    t = P.tubep(pts, rv, rh, m=m, n=n, up=up, cap0=cap0, cap1=cap1, power=power)
    if ridges:
        cnt, dep, shp = ridges
        # parameter along the tube by nearest path point
        path = M.polyline(pts, 200)
        d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(path, axis=0), axis=1))]
        d /= d[-1]
        idx = np.array([np.argmin(((path - v) ** 2).sum(1)) for v in t.v])
        s = d[idx]
        ph = (s * cnt) % 1.0
        prof = ph ** (1 + 3 * shp) if shp > 0 else 0.5 - 0.5 * np.cos(2 * np.pi * ph)
        ax = path[np.minimum(idx + 1, 199)] - path[np.maximum(idx - 1, 0)]
        ax /= np.maximum(np.linalg.norm(ax, axis=1), 1e-9)[:, None]
        rad = t.v - path[idx]
        rad -= ax * (rad * ax).sum(1)[:, None]
        rn = np.linalg.norm(rad, axis=1)
        ok = rn > 1e-5
        t.v[ok] += rad[ok] / rn[ok][:, None] * (dep * (prof[ok] - 0.5))[:, None]
    if bones:
        B.wbones(t, bones, sharp=sharp)
    t.smooth = 180.0
    return t


def skin_to(part, bones, sharp=4.0, k=3):
    """weights of a loose part (fin, frill, spikes) from the nearest chain bones"""
    B.wbones(part, bones, sharp=sharp, k=k)
    return part


def fin_strip(path, heights, th=0.006, lean=(0, 0, 0), up=(0, 1, 0), notch=None, seed=0, sub=3):
    """one continuous upright fin along a path (dorsal crest); heights per path point; notch = list of (index, depth
    0..1) tears; returns a closed thin slab"""
    rng = np.random.default_rng(seed)
    path = np.asarray(path, dtype=float)
    k = len(path)
    hs = np.asarray(heights, dtype=float).copy()
    if notch:
        for i, d in notch:
            hs[i] *= (1 - d)
    upv = unit(up)
    rows = sub + 1
    G = np.zeros((k, rows, 3))
    for i in range(k):
        for j in range(rows):
            t = j / sub
            G[i, j] = path[i] - upv * 0.015 * (1 - t) + upv * hs[i] * t + V(lean) * hs[i] * t * t + rng.normal(0, 0.004, 3) * t
    return P.sheet_solid(G, th)


def mound(r0, r1, n=11, seed=0, h=0.07, look=None):
    """ring of broken ground round a burrower's hole (rigid to root): chunks tilted outwards"""
    rng = np.random.default_rng(seed)
    parts = []
    for i in range(n):
        a = 2 * math.pi * (i + rng.uniform(-0.25, 0.25)) / n
        rr = rng.uniform(r0, r1)
        s = rng.uniform(0.7, 1.25)
        c = M.rock(0.07 * s, 0.05 * s, h * s, seed=seed * 31 + i, n=10, jit=0.3)
        c.rot('y', rng.uniform(-25, 25)).rot('x', rng.uniform(-25, 25)).rot('z', math.degrees(a) + rng.uniform(-30, 30))
        c.move(rr * math.cos(a), rr * math.sin(a), h * s * 0.25)
        parts.append(c)
    q = M.join(parts)
    q.smooth = 0.0
    return q


# ================================================================================================ column
class Column:
    def __init__(self, C, ground=0.0):
        self.C = C
        rg = C.rg
        self.seg = sorted([n for n in rg.names if n.startswith('s') and n[1:].isdigit()], key=lambda n: int(n[1:]))
        self.n = len(self.seg)
        self.has_head = 'head' in rg.idx
        self.top = rg.T('head') if self.has_head else rg.T(self.seg[-1])
        # index of the first segment whose head is above the ground
        self.g = next(i for i, b in enumerate(self.seg) if rg.T(b)[2] > ground + 0.02)

    def lean(self, p, pitch=0.0, side=0.0, twist=0.0, lo=None, hi=None, power=0.0):
        """spread a bend over the segments lo..hi (default: from the ground up). pitch > 0 = forward (-Y),
        side > 0 = towards +X, twist = about the column. power > 0 puts more of it high up, < 0 low down"""
        lo = self.g if lo is None else lo
        hi = self.n if hi is None else hi
        w = np.array([(i - lo + 1.0) ** power for i in range(lo, hi)])
        w /= w.sum()
        for k, i in enumerate(range(lo, hi)):
            p.rot(self.seg[i], x=pitch * w[k], y=side * w[k], z=twist * w[k])

    def wave(self, p, u, amp, n=1.0, k=1.0, ph=0.0, axis='y', lo=None, grow=0.5):
        """travelling wave up the column (zero net bend): segment i turns amp * sin(2 pi (n u - k s + ph))"""
        lo = self.g if lo is None else lo
        m = self.n - lo
        for j, i in enumerate(range(lo, self.n)):
            s = j / max(m - 1, 1)
            a = amp * (1 - grow + grow * s) * math.sin(2 * math.pi * (n * u - k * s + ph))
            p.rot(self.seg[i], **{axis: a})

    def rise(self, p, dz=0.0, dx=0.0, dy=0.0):
        p.move(self.seg[0], dx, dy, dz)

    def head(self, p, pitch=0.0, yaw=0.0, roll=0.0):
        if self.has_head:
            p.rot('head', x=pitch, z=yaw, y=roll)

    def reach(self, clip, bone=None, pt=None):
        return self.C.reach(clip, bone or ('head' if self.has_head else self.seg[-1]), self.top if pt is None else pt)

    # ---- stock clips (extra(p, u, ...) animates jaws, frills, rootlets)
    def idle(self, dur=3.6, sway=5.0, wave=3.0, nod=4.0, look=10.0, extra=None):
        def fn(p, u):
            self.lean(p, pitch=sway * 0.5 * osc(u, 1), side=sway * osc(u, 1, 0.25))
            self.wave(p, u, wave, n=1, k=1.0)
            self.head(p, pitch=nod * osc(u, 2, 0.1), yaw=look * osc(u, 1, 0.1) * (0.6 + 0.4 * osc(u, 2, 0.2)))
            self.rise(p, dz=0.012 * osc(u, 2))
            if extra:
                extra(p, u)
        return self.C.clip('Idle', dur, fn)

    def plough(self, name, v, T, lean=18.0, weave=7.0, bob=0.02, sink=0.06, head=-12.0, extra=None):
        """travelling burrower: the column leans into its course, weaves sideways (a wave running up it) and bobs"""
        def fn(p, u):
            self.lean(p, pitch=lean + 2.5 * osc(u, 2), power=-0.3)
            self.wave(p, u, weave, n=1, k=0.8, axis='y')
            self.wave(p, u, weave * 0.35, n=1, k=0.8, ph=0.25, axis='z')
            self.head(p, pitch=head + 3 * osc(u, 2, 0.2), yaw=-weave * 0.5 * osc(u, 1, 0.1))
            self.rise(p, dz=-sink + bob * osc(u, 2, 0.1), dx=0.02 * osc(u, 1, 0.2))
            if extra:
                extra(p, u)
        c = self.C.clip(name, T, fn)
        self.C.meta['walk_speed' if name == 'Walk' else 'run_speed'] = round(v, 3)
        return c

    def lunge(self, name='Attack1', dur=0.9, hit=0.45, back=22.0, fwd=78.0, head=10.0, power=0.3, twist=0.0, extra=None):
        """rears back, whips forward and down so the head strikes the ground in front, recovers"""
        hu = hit / dur

        def fn(p, u):
            w = bump(u, 0.0, hu * 0.55, hu * 0.7, hu)
            s = bump(u, hu * 0.7, hu, hu + 0.1, 0.95)
            self.lean(p, pitch=-back * w + fwd * s, power=power, twist=twist * s)
            self.wave(p, u, 4 * (1 - s), n=2, k=1)
            self.head(p, pitch=-head * w + head * 1.5 * s)
            self.rise(p, dz=0.05 * w - 0.05 * s)
            if extra:
                extra(p, u, w, s)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def sweep(self, name='Attack2', dur=1.2, hit=0.6, low=70.0, arc=110.0, head=0.0, extra=None):
        """lies down low and sweeps a wide arc round its hole from the creature's right to its left"""
        hu = hit / dur

        def fn(p, u):
            dn = bump(u, 0.0, hu * 0.45, hu + 0.2, 0.95)
            a = -arc + 2 * arc * sm(u, hu * 0.5, hu + (hu * 0.5))
            sw = dn * a * (1 - sm(u, hu + 0.25, 0.98))
            p.rot(self.seg[self.g], z=sw)
            self.lean(p, pitch=low * dn, power=-0.5)
            self.wave(p, u, 5 * dn, n=1.5, k=1, axis='y')
            self.head(p, pitch=head * dn)
            self.rise(p, dz=-0.04 * dn)
            if extra:
                extra(p, u, dn, sw)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def hit(self, dur=0.45, back=16.0, extra=None):
        def fn(p, u):
            k = bump(u, 0.0, 0.2, 0.3, 1.0)
            self.lean(p, pitch=-back * k, side=back * 0.5 * k, power=0.5, twist=14 * k)
            self.wave(p, u, 6 * k, n=3, k=1.5)
            self.head(p, pitch=-10 * k, roll=12 * k)
            self.rise(p, dz=-0.05 * k)
            if extra:
                extra(p, u, k)
        return self.C.clip('Hit', dur, fn)

    def death_topple(self, dur=1.7, side=25.0, total=96.0, radius=0.12, twist=30.0, lo=None, span=2, extra=None):
        """goes rigid, sways, then folds just above the ground and falls its full length (forward and a little aside);
        the column sinks back into its hole far enough for the fallen part to lie on the ground (radius = its
        half thickness)"""
        lo = (self.g + 1) if lo is None else lo
        ref = self.C.rg.H(self.seg[min(lo + span, self.n - 1)])

        def make(sink):
            def fn(p, u):
                st = bump(u, 0.0, 0.12, 0.2, 0.34)
                f = sm(u, 0.22, 0.62) ** 1.6
                bn = bump(u, 0.62, 0.66, 0.7, 0.8)
                self.lean(p, pitch=-10 * st, power=0.5)
                self.lean(p, pitch=(total - 5 * bn) * f, twist=twist * f, lo=lo, hi=lo + span)
                p.rot(self.seg[lo], z=side * f)
                self.lean(p, pitch=8 * f, lo=lo + span, power=1.0)                    # the far end droops on the ground
                self.wave(p, u, 5 * (1 - f) * st, n=3, k=1)
                self.head(p, pitch=10 * f, roll=25 * f)
                self.rise(p, dz=-sink * f)
                if extra:
                    extra(p, u, f)
            return fn
        c = self.C.clip('Death', dur, make(0.0), under_ok=True)
        z = float((c['G'][-1][self.C.rg.idx[self.seg[min(lo + span, self.n - 1)]]] @ np.r_[ref, 1.0])[2])
        return self.C.clip('Death', dur, make(max(0.0, z - radius)), under_ok=True)

    def spawn_erupt(self, dur=1.1, depth=None, extra=None):
        """bursts out of the ground: shoots up past its height, settles, shakes"""
        dep = float(self.top[2]) + 0.25 if depth is None else depth

        def fn(p, u):
            r = sm(u, 0.0, 0.42)
            over = bump(u, 0.3, 0.44, 0.48, 0.7)
            sh = bump(u, 0.45, 0.55, 0.8, 0.95)
            self.rise(p, dz=-dep * (1 - r) + 0.1 * over)
            self.lean(p, pitch=-12 * (1 - r) + 6 * over, twist=120 * (1 - r))
            self.wave(p, u, 6 * sh + 3, n=4, k=1.5)
            self.head(p, pitch=-20 * (1 - r))
            if extra:
                extra(p, u, r)
        return self.C.clip('Spawn', dur, fn, under_ok=True)


# ================================================================================================ rearer
class Rearer:
    def __init__(self, C):
        self.C = C
        rg = C.rg
        num = lambda pre: sorted([n for n in rg.names if n.startswith(pre) and n[len(pre):].isdigit()], key=lambda n: int(n[len(pre):]))
        self.neck = num('neck')
        self.tail = num('tail')
        self.top = rg.T('head')
        self.tail_len = float(sum(np.linalg.norm(rg.T(b) - rg.H(b)) for b in self.tail))

    def body(self, p, dx=0.0, dy=0.0, dz=0.0, yaw=0.0, pitch=0.0, roll=0.0):
        p.setR('body', euler(pitch, roll, yaw))
        p.move('body', dx, dy, dz)

    def necks(self, p, pitch=0.0, side=0.0, twist=0.0, power=0.0, lo=0):
        w = np.array([(i + 1.0) ** power for i in range(len(self.neck) - lo)])
        w /= w.sum()
        for k, b in enumerate(self.neck[lo:]):
            p.rot(b, x=pitch * w[k], y=side * w[k], z=twist * w[k])

    def neckwave(self, p, u, amp, n=1.0, k=1.0, ph=0.0, axis='y'):
        m = len(self.neck)
        for i, b in enumerate(self.neck):
            p.rot(b, **{axis: amp * math.sin(2 * math.pi * (n * u - k * i / max(m - 1, 1) + ph))})

    def head(self, p, pitch=0.0, yaw=0.0, roll=0.0):
        p.rot('head', x=pitch, z=yaw, y=roll)

    def tailwave(self, p, u, amp, n=1.0, waves=1.2, ph=0.0, grow=0.6, lift=0.0):
        """lateral undulation running from the body to the tail tip; the first tail bone counter-turns so the wave
        has no net swing; amp in degrees per joint"""
        m = len(self.tail)
        tot = 0.0
        for i, b in enumerate(self.tail):
            s = i / max(m - 1, 1)
            a = amp * (1 - grow + grow * s) * math.sin(2 * math.pi * (n * u + waves * s + ph))
            if i == 0:
                a0 = a
            p.rot(b, z=a, x=-lift * s)
            tot += a * (m - i) / m
        p.rot(self.tail[0], z=-tot * 0.5)

    def reach(self, clip, pt=None):
        return self.C.reach(clip, 'head', self.top if pt is None else pt)

    def slither(self, name, v, T, amp=16.0, waves=1.2, sway=0.03, lean=6.0, bob=0.01, extra=None):
        """travel: the tail wave carries the animal; the wave speed (wavelength / period) is the stated speed"""
        T = max(2, round(T * B.FPS)) / B.FPS

        def fn(p, u):
            self.body(p, dx=sway * osc(u, 1, 0.1), dz=bob * osc(u, 2), yaw=3.0 * osc(u, 1, 0.3), pitch=0.0)
            self.tailwave(p, u, amp, n=1, waves=waves)
            self.necks(p, pitch=lean + 1.5 * osc(u, 2), side=-3.0 * osc(u, 1, 0.1))
            self.neckwave(p, u, 2.5, n=1, k=0.8)
            p.aim_world('head', euler(lean * 0.6 + 2 * osc(u, 2, 0.2), 0, 0))            # steady head
            if extra:
                extra(p, u)
        c = self.C.clip(name, T, fn)
        self.C.meta['walk_speed' if name == 'Walk' else 'run_speed'] = round(v, 3)
        self.C.meta.setdefault('wave', {})[name] = dict(wavelength=round(self.tail_len / waves, 3), period=round(T, 3),
                                                        wave_speed=round(self.tail_len / waves / T, 3))
        return c

    def idle(self, dur=3.6, sway=4.0, tail=5.0, look=12.0, extra=None):
        def fn(p, u):
            self.body(p, dz=0.006 * osc(u, 2))
            self.tailwave(p, u, tail, n=1, waves=0.8)
            self.necks(p, pitch=sway * 0.6 * osc(u, 1), side=sway * osc(u, 1, 0.25))
            self.neckwave(p, u, 2.0, n=1, k=1)
            self.head(p, pitch=3 * osc(u, 2, 0.1), yaw=look * osc(u, 1, 0.1) * (0.6 + 0.4 * osc(u, 2, 0.2)))
            if extra:
                extra(p, u)
        return self.C.clip('Idle', dur, fn)

    def strike(self, name='Attack1', dur=0.9, hit=0.45, back=20.0, fwd=70.0, head=10.0, power=0.5, slide=0.0, extra=None):
        hu = hit / dur

        def fn(p, u):
            w = bump(u, 0.0, hu * 0.55, hu * 0.7, hu)
            s = bump(u, hu * 0.7, hu, hu + 0.1, 0.95)
            self.body(p, dy=0.05 * w - slide * s)
            self.tailwave(p, u, 6 + 6 * s, n=2, waves=1.0)
            self.necks(p, pitch=-back * w + fwd * s, power=power)
            self.head(p, pitch=-head * w + head * 1.5 * s)
            if extra:
                extra(p, u, w, s)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def hit(self, dur=0.45, back=16.0, extra=None):
        def fn(p, u):
            k = bump(u, 0.0, 0.2, 0.3, 1.0)
            self.body(p, dy=0.04 * k)
            self.tailwave(p, u, 10 * k, n=2, waves=1.0)
            self.necks(p, pitch=-back * k, side=back * 0.5 * k, twist=14 * k, power=0.5)
            self.head(p, pitch=-10 * k, roll=12 * k)
            if extra:
                extra(p, u, k)
        return self.C.clip('Hit', dur, fn)

    def death_fall(self, dur=1.8, side=1.0, total=92.0, lo=0, span=2, floor=True, extra=None):
        """the reared front sways, folds at the bend and falls sideways-forward; the tail thrashes and goes slack"""
        def fn(p, u):
            st = bump(u, 0.0, 0.12, 0.2, 0.34)
            f = sm(u, 0.22, 0.62) ** 1.6
            self.body(p, yaw=side * 20 * f)
            self.tailwave(p, u, 14 * bump(u, 0.05, 0.2, 0.5, 0.85), n=3, waves=1.0)
            self.necks(p, pitch=-10 * st, power=0.5)
            w = np.zeros(len(self.neck)); w[lo:lo + span] = 1.0 / span
            for k, b in enumerate(self.neck):
                p.rot(b, x=total * f * w[k], y=side * 14 * f * w[k])
            self.necks(p, pitch=12 * f, lo=min(lo + span, len(self.neck) - 1), power=1.0)
            self.head(p, pitch=10 * f, roll=side * 30 * f)
            if extra:
                extra(p, u, f)
        return self.C.clip('Death', dur, fn, floor='body' if floor else None, settle=(0.5, 0.64))

    def spawn_rise(self, dur=1.3, depth=None, extra=None):
        dep = float(self.top[2]) + 0.3 if depth is None else depth

        def fn(p, u):
            r = sm(u, 0.0, 0.5)
            sh = bump(u, 0.5, 0.6, 0.8, 0.95)
            self.body(p, dz=-dep * (1 - r), yaw=60 * (1 - r))
            self.tailwave(p, u, 6 + 8 * sh, n=3, waves=1.0)
            self.necks(p, pitch=-14 * (1 - r), twist=40 * (1 - r))
            self.neckwave(p, u, 5 * sh, n=4, k=1.5)
            self.head(p, pitch=-16 * (1 - r))
            if extra:
                extra(p, u, r)
        return self.C.clip('Spawn', dur, fn, under_ok=True)


# ================================================================================================ looper
class Looper:
    """inchworm: the rear end (head of s1) and the front end (tail of sN) are placed on targets; the chain between
    them is a circular arc of the chain's fixed length"""

    def __init__(self, C, lift_r=0.0):
        self.C = C
        rg = C.rg
        self.seg = sorted([n for n in rg.names if n.startswith('s') and n[1:].isdigit()], key=lambda n: int(n[1:]))
        self.n = len(self.seg)
        self.rear0 = rg.H(self.seg[0])
        self.front0 = rg.T(self.seg[-1])
        self.L = float(np.linalg.norm(self.front0 - self.rear0))
        self.ls = self.L / self.n
        self.z = float(self.rear0[2])
        self.has_head = 'head' in rg.idx
        self.fr = Leg([self.seg[-1]], self.front0, name='front')
        self.rr = Leg([self.seg[0]], self.rear0, name='rear')
        self.fr.home = V(0, -self.L * 0.42, self.z)
        self.rr.home = V(0, self.L * 0.42, self.z)
        C.legs = [self.fr, self.rr]
        # contact points for the verifier: the ends of the chain (axis points)
        self.fr.c = self.front0.copy(); self.rr.c = self.rear0.copy()
        self.top = rg.T('head') if self.has_head else self.front0

    def _delta(self, d):
        """joint angle (rad) that makes a chain of n links of length ls span the chord d"""
        n, ls = self.n, self.ls
        d = min(d, n * ls * 0.9995)
        lo, hi = 1e-6, 2 * math.pi / n * 0.98

        def chord(dl):
            return ls * abs(math.sin(n * dl / 2) / math.sin(dl / 2))
        if d <= chord(hi):
            return hi
        for _ in range(50):
            mid = (lo + hi) / 2
            if chord(mid) > d:
                lo = mid
            else:
                hi = mid
        return (lo + hi) / 2

    def place(self, p, rear, front, roll=0.0, yaw=0.0, skew=0.0, flat=0.0):
        """rear / front: world targets of the two ends (bone axis). roll turns the plane of the loop about the
        chord (90 = the loop lies on its side); skew shifts curvature towards the front (+) or rear (-);
        flat 0..1 flattens the arc into an S (less height)"""
        rear = np.asarray(rear, dtype=float); front = np.asarray(front, dtype=float)
        ch = front - rear
        d = float(np.linalg.norm(ch))
        self.err = max(0.0, d - self.L)
        p.err = max(p.err, self.err)
        dl = self._delta(d)
        n = self.n
        w = np.ones(n - 1)
        if skew:
            w = 1 + skew * np.linspace(-1, 1, n - 1)
        # direction angles of the links relative to the chord (symmetric arc), then per-joint turns
        turns = dl * w * (n - 1) / w.sum()
        ang = np.zeros(n)
        ang[0] = 0.0
        for i in range(1, n):
            ang[i] = ang[i - 1] - turns[i - 1]
        # chord of this (possibly skewed) arc in its own frame -> rotate so it matches the wanted chord
        py = -np.cos(ang).sum() * self.ls; pz = np.sin(ang).sum() * self.ls
        a_arc = math.atan2(pz, -py)
        d_arc = math.hypot(py, pz)
        el = math.atan2(ch[2], math.hypot(ch[0], ch[1]))
        az = math.degrees(math.atan2(-ch[0], -ch[1])) if (abs(ch[0]) + abs(ch[1])) > 1e-9 else 0.0   # yaw of the chord from -Y towards... +X is negative
        a0 = el - a_arc
        b0 = self.seg[0]
        R = rz(-az + yaw) @ ry(roll) @ rx(-math.degrees(a0))
        p.setR(b0, R)
        p.move(b0, rear - self.rear0)
        for i in range(1, n):
            p.rot(self.seg[i], x=math.degrees(turns[i - 1]))
        # exact fix of the chord length error introduced by the skew (rarely needed)
        return math.degrees(dl)

    def targets(self, u, S, duty, lift, lift_r=None, phase_r=0.5):
        self.fr.phase = 0.0; self.rr.phase = phase_r
        Pf, swf, _ = gait_foot(self.fr, u, S, duty, lift)
        Pr, swr, _ = gait_foot(self.rr, u, S, duty, lift if lift_r is None else lift_r)
        return Pf, Pr, swf, swr

    def set_homes(self, S, duty, phase_r=0.5, slack=0.97, lift=0.0, lift_r=None):
        """choose the home distance so the largest span of the cycle is slack * chain length"""
        worst = -9.0; least = 9.0
        self.fr.home = V(0, 0, self.z); self.rr.home = V(0, 0, self.z)
        for i in range(240):
            Pf, Pr, _, _ = self.targets(i / 240.0, S, duty, lift, lift_r, phase_r)
            e = float(np.linalg.norm(Pr - Pf)) * (1.0 if Pr[1] >= Pf[1] else -1.0)
            worst = max(worst, e); least = min(least, e)
        h = slack * self.L - worst
        self.fr.home = V(0, -h / 2, self.z); self.rr.home = V(0, h / 2, self.z)
        return h, h + least

    def gait(self, name, v, T, duty, lift=0.02, lift_r=None, phase_r=0.5, head=0.0, sway=0.0, extra=None, slack=0.97):
        T = max(2, round(T * B.FPS)) / B.FPS
        S = v * duty * T
        h, mn = self.set_homes(S, duty, phase_r, slack, lift, lift_r)
        if mn < 0.12 * self.L:
            B.log("NOTE %s %s: looper span gets as short as %.2f of its length (stride %.2f)" % (self.C.id, name, mn / self.L, S))
        homes = (self.fr.home.copy(), self.rr.home.copy())

        def fn(p, u):
            self.fr.home, self.rr.home = homes[0].copy(), homes[1].copy()
            Pf, Pr, swf, swr = self.targets(u, S, duty, lift, lift_r, phase_r)
            self.place(p, Pr, Pf, yaw=sway * osc(u, 1))
            if self.has_head:
                p.rot('head', x=head * swf)
            if extra:
                extra(p, u, swf, swr)
        c = self.C.clip(name, T, fn)
        self.C.meta['walk_speed' if name == 'Walk' else 'run_speed'] = round(v, 3)
        self.fr.home, self.rr.home = V(0, -self.L * 0.42, self.z), V(0, self.L * 0.42, self.z)
        return c

    def rest_ends(self, k=0.42):
        return V(0, self.L * k, self.z), V(0, -self.L * k, self.z)

    def reach(self, clip, pt=None):
        return self.C.reach(clip, 'head' if self.has_head else self.seg[-1], self.top if pt is None else pt)
