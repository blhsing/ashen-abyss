"""Rig E - flyers. Skeleton:
  root > body > head [> jaw, ear_l/r] ;  body > wing_<s>1 > wing_<s>2 > wing_<s>3 ;  body > tail1..n ;  body > leg_<s>
The hover height is INSIDE the model: the rest pose and every clip keep the body `h` metres above the origin;
Death falls and ends as a corpse on z = 0, Spawn drops in from above. meta: fly_height = h, hover_in_model = true.
Wing angles: `up` degrees lift the wing tips (both sides mirrored), `fold` sweeps a wing bone backwards, `twist` pitches
the leading edge down."""
import math
import numpy as np
import blib as B
from blib import M, V, unit, sm, bump, osc, rx, ry, rz, euler, I3
import bparts as P


def flyer_skel(body, head=None, wing=None, tail=None, legs=None, jaw=None, ears=None, extra=None):
    """body = (rear joint, front joint): the bone points forward. wing = left-side joints [shoulder, elbow, wrist, tip]
    (2 to 4 points -> 1 to 3 bones); legs = (hip, foot) left; ears = (base, tip) left; jaw = (joint, tip)"""
    sk = B.Sk()
    sk.bone('body', body[0], body[1], 'root')
    hb = 'body'
    if head is not None:
        sk.bone('head', head[0], head[1], 'body')
        hb = 'head'
    if jaw is not None:
        sk.bone('jaw', jaw[0], jaw[1], hb)
    if ears is not None:
        sk.lr('ear', ears[0], ears[1], hb)
    if wing is not None:
        for s, sg in (('l', 1.0), ('r', -1.0)):
            pr = 'body'
            for i in range(len(wing) - 1):
                n = 'wing_%s%d' % (s, i + 1)
                sk.bone(n, V(wing[i]) * (sg, 1, 1), V(wing[i + 1]) * (sg, 1, 1), pr)
                pr = n
    if tail is not None:
        sk.chain('tail', tail, 'body')
    if legs is not None:
        sk.lr('leg', legs[0], legs[1], 'body')
    if extra:
        extra(sk)
    return sk


class Flyer:
    def __init__(self, C, h):
        self.C = C
        rg = C.rg
        self.h = h
        self.nw = len([n for n in rg.names if n.startswith('wing_l')])
        self.tail = sorted([n for n in rg.names if n.startswith('tail')], key=lambda n: int(n[4:]))
        self.has = lambda n: n in rg.idx
        self.pivot = (rg.H('body') + rg.T('body')) / 2
        self.front = rg.T('head') if self.has('head') else rg.T('body')
        C.meta.update(fly_height=round(float(h), 3), hover_in_model=True)

    # ---- building blocks
    def body(self, p, dx=0.0, dy=0.0, dz=0.0, pitch=0.0, roll=0.0, yaw=0.0):
        R = euler(pitch, roll, yaw)
        h = self.C.rg.H('body')
        pv = self.pivot
        p.setR('body', R)
        p.move('body', (pv - h) - R @ (pv - h) + V(dx, dy, dz))

    def head(self, p, pitch=0.0, yaw=0.0, roll=0.0):
        if self.has('head'):
            p.rot('head', x=pitch, z=yaw, y=roll)

    def jaw(self, p, deg):
        if self.has('jaw'):
            p.rot('jaw', x=deg)

    def ears(self, p, a=0.0, b=None, spread=0.0):
        if self.has('ear_l'):
            p.rot('ear_l', x=a, y=spread)
            p.rot('ear_r', x=a if b is None else b, y=-spread)

    def legs(self, p, a=0.0, spread=0.0):
        if self.has('leg_l'):
            p.rot('leg_l', x=a, y=-spread)
            p.rot('leg_r', x=a, y=spread)

    def tailw(self, p, u=0.0, lift=0.0, amp=0.0, n=1.0, lag=0.12, yaw=0.0):
        for i, b in enumerate(self.tail):
            p.rot(b, x=-lift / max(len(self.tail), 1) + amp * osc(u, n, -i * lag), z=yaw / max(len(self.tail), 1))

    def wings(self, p, up=(0.0, 0.0, 0.0), fold=(0.0, 0.0, 0.0), twist=(0.0, 0.0, 0.0), up_r=None, fold_r=None):
        """set both wings: per wing bone the lift (deg, tips up), the sweep back and the leading-edge-down twist"""
        for s, sg, U, F in (('l', 1.0, up, fold), ('r', -1.0, up if up_r is None else up_r, fold if fold_r is None else fold_r)):
            for i in range(self.nw):
                b = 'wing_%s%d' % (s, i + 1)
                p.rot(b, x=twist[i] if i < len(twist) else 0.0)
                p.rot(b, y=-sg * (U[i] if i < len(U) else 0.0))
                p.rot(b, z=sg * (F[i] if i < len(F) else 0.0))

    def flap(self, p, ph, amp=(40.0, 25.0, 20.0), lag=(0.0, 0.1, 0.2), bias=(8.0, 0.0, -6.0), fold=(0.0, 8.0, 22.0), twist=10.0, k=1.0,
             buzz=None):
        """one wing beat at phase ph (0..1): the tips trail the roots (lag), the hand folds back on the upstroke.
        buzz = frame index: insect wings that simply alternate up / down every frame"""
        if buzz is not None:
            sgn = 1.0 if int(buzz) % 2 == 0 else -1.0
            self.wings(p, up=[bias[i] + amp[i] * sgn * k for i in range(self.nw)], twist=[twist * sgn * k] * self.nw)
            return sgn
        up = []; fo = []; tw = []
        for i in range(self.nw):
            a = math.sin(2 * math.pi * (ph - lag[i]))
            v = math.cos(2 * math.pi * (ph - lag[i]))            # > 0 = moving up
            up.append(bias[i] + amp[i] * a * k)
            fo.append(fold[i] * max(0.0, v) * k)
            tw.append(-twist * v * k * (0.4 + 0.3 * i))
        self.wings(p, up=up, fold=fo, twist=tw)
        return math.cos(2 * math.pi * ph)                         # > 0 while the wing roots travel up

    def reach(self, clip, bone=None, pt=None):
        return self.C.reach(clip, bone or ('head' if self.has('head') else 'body'), self.front if pt is None else pt)

    # ---- stock clips (flap_kw = keyword arguments of flap; extra(p, u, ...) for jaws, ears, tails)
    def hover(self, name='Idle', beats=6, T=0.3333, bob=0.035, pitch=-12.0, sway=0.04, flap_kw=None, tail=6.0, legs=20.0, look=10.0,
              buzz=False, extra=None):
        fk = flap_kw or {}
        dur = max(2, round(T * B.FPS)) / B.FPS * beats
        n = int(round(dur * B.FPS))

        def fn(p, u):
            ph = (u * beats) % 1.0
            f = int(round(u * n))
            v = self.flap(p, ph, buzz=(f if buzz else None), **fk)
            slow = osc(u, 1)
            self.body(p, dz=(0.0 if buzz else -bob * math.sin(2 * math.pi * (ph - 0.25))) + 0.03 * osc(u, 2, 0.1), dx=sway * slow, dy=sway * 0.5 * osc(u, 1, 0.25),
                      pitch=pitch + 2.0 * osc(u, 2), roll=3.0 * slow, yaw=2.0 * osc(u, 1, 0.3))
            self.head(p, pitch=-pitch * 0.6 + 3 * osc(u, 2, 0.2), yaw=look * osc(u, 1, 0.1))
            self.tailw(p, u, lift=-8, amp=tail, n=2)
            self.legs(p, legs + 6 * osc(u, 2, 0.3))
            if extra:
                extra(p, u, ph)
        return self.C.clip(name, dur, fn)

    def fly(self, name, v, beats=2, T=0.2667, bob=0.03, pitch=14.0, flap_kw=None, tail=4.0, legs=55.0, buzz=False, bank=3.0, extra=None):
        fk = flap_kw or {}
        dur = max(2, round(T * B.FPS)) / B.FPS * beats
        n = int(round(dur * B.FPS))

        def fn(p, u):
            ph = (u * beats) % 1.0
            f = int(round(u * n))
            self.flap(p, ph, buzz=(f if buzz else None), **fk)
            self.body(p, dz=(0.0 if buzz else -bob * math.sin(2 * math.pi * (ph - 0.25))), pitch=pitch + 1.5 * math.sin(2 * math.pi * ph),
                      roll=bank * osc(u, 1), dx=0.02 * osc(u, 1, 0.2))
            self.head(p, pitch=-pitch * 0.7)
            self.tailw(p, u, lift=6, amp=tail, n=beats)
            self.legs(p, legs)
            if extra:
                extra(p, u, ph)
        c = self.C.clip(name, dur, fn)
        self.C.meta['walk_speed' if name == 'Walk' else 'run_speed'] = round(v, 3)
        return c

    def swoop(self, name='Attack1', dur=1.0, hit=0.5, fwd=0.7, drop=0.6, pitch=35.0, beats=3, gape=35.0, flap_kw=None, glide=(20.0, 10.0, 0.0),
              buzz=False, extra=None):
        """dive: climb a little, fold into a glide, strike forward and down, pull up and return"""
        fk = flap_kw or {}
        hu = hit / dur
        n = int(round(dur * B.FPS))

        def fn(p, u):
            w = bump(u, 0.0, hu * 0.45, hu * 0.6, hu * 0.9)
            s = bump(u, hu * 0.6, hu, hu + 0.08, 0.95)
            g = bump(u, hu * 0.55, hu * 0.75, hu + 0.02, hu + 0.2)            # gliding, wings held
            ph = (u * beats) % 1.0
            if buzz:
                self.flap(p, ph, buzz=int(round(u * n)), **fk)
            else:
                self.flap(p, ph, k=1.0 - 0.85 * g, **fk)
                self.wings(p, up=[x * g for x in glide], fold=[0, 12 * g, 25 * g])
            self.body(p, dy=0.12 * w - fwd * s, dz=0.12 * w - drop * s, pitch=-10 * w + pitch * s * (1 - sm(u, hu, hu + 0.15)) - 14 * bump(u, hu, hu + 0.1, hu + 0.2, 0.9))
            self.head(p, pitch=8 * w - 10 * s)
            self.jaw(p, gape * bump(u, hu * 0.5, hu * 0.85, hu - 0.02, hu + 0.03))
            self.tailw(p, u, lift=10 * s)
            self.legs(p, 30 + 40 * s)
            if extra:
                extra(p, u, w, s)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def screech(self, name='Attack2', dur=1.2, hit=0.55, back=0.12, beats=4, gape=45.0, shake=3.0, flap_kw=None, buzz=False, extra=None):
        """hovering attack: rears, throws the head forward with the mouth wide open, trembles while it lasts"""
        fk = flap_kw or {}
        hu = hit / dur
        n = int(round(dur * B.FPS))

        def fn(p, u):
            w = bump(u, 0.0, hu * 0.5, hu * 0.7, hu)
            s = bump(u, hu * 0.75, hu, hu + 0.25, 0.97)
            ph = (u * beats) % 1.0
            self.flap(p, ph, buzz=(int(round(u * n)) if buzz else None), **fk)
            self.body(p, dy=back * w - 0.1 * s, dz=0.06 * w + (0.0 if buzz else -0.03 * math.sin(2 * math.pi * (ph - 0.25))), pitch=-22 * w + 4 * s + shake * s * osc(u, 14),
                      roll=shake * s * osc(u, 11, 0.3))
            self.head(p, pitch=-18 * w + 14 * s, roll=shake * s * osc(u, 13))
            self.jaw(p, gape * (0.25 * w + s))
            self.tailw(p, u, lift=-14 * w + 10 * s)
            self.legs(p, 20 + 30 * s)
            if extra:
                extra(p, u, w, s)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def hit(self, dur=0.45, back=0.14, beats=2, flap_kw=None, buzz=False, extra=None):
        fk = flap_kw or {}
        n = int(round(dur * B.FPS))

        def fn(p, u):
            k = bump(u, 0.0, 0.2, 0.32, 1.0)
            ph = (u * beats) % 1.0
            self.flap(p, ph, buzz=(int(round(u * n)) if buzz else None), k=1.0 - 0.5 * k, **fk)
            if not buzz:
                self.wings(p, up=[25 * k, 15 * k, 0], fold=[0, 10 * k, 20 * k])
            self.body(p, dy=back * k, dz=-0.1 * k, pitch=-25 * k, roll=22 * k, yaw=-10 * k)
            self.head(p, pitch=-12 * k, yaw=-15 * k)
            self.jaw(p, 25 * k)
            self.tailw(p, u, lift=-20 * k)
            self.legs(p, 20 - 30 * k, spread=15 * k)
            if extra:
                extra(p, u, k)
        return self.C.clip('Hit', dur, fn)

    def death_fall(self, dur=1.6, spin=160.0, drift=0.25, crumple=(55.0, 25.0, 10.0), rest=(-6.0, -8.0, -6.0), rest_fold=(0.0, 14.0, 30.0),
                   belly=0.08, land=0.5, roll_end=0.0, pitch_end=0.0, extra=None):
        """hit in the air: the wings crumple, it tumbles down, strikes the ground and lies spread out.
        belly = height of the body pivot when it lies on the ground; rest / rest_fold = final wing pose"""
        h = float(self.pivot[2])

        def fn(p, u):
            t = min(1.0, u / land)
            fall = t * t                                           # gravity
            on = sm(u, land - 0.02, land + 0.02)
            bn = bump(u, land, land + 0.06, land + 0.1, land + 0.24)
            cr = bump(u, 0.0, 0.12, land - 0.08, land + 0.05)
            tw = bump(u, land + 0.2, land + 0.26, land + 0.3, land + 0.42) * osc(u, 8)
            self.wings(p, up=[crumple[i] * cr + rest[i] * on + (4 * tw if i == 1 else 0) for i in range(3)],
                       fold=[rest_fold[i] * on + 25 * cr * (i > 0) for i in range(3)], twist=[0, 0, 0])
            self.body(p, dz=-(h - belly) * fall + 0.07 * bn, dy=drift * t, dx=0.1 * t,
                      roll=spin * t * (1 - on) + roll_end * on, pitch=-20 * cr * (1 - on) + pitch_end * on, yaw=40 * t)
            self.head(p, pitch=20 * on, roll=18 * on)
            self.jaw(p, 20 * on + 10 * cr)
            self.tailw(p, u, lift=-20 * cr + 10 * on)
            self.legs(p, 10 + 50 * cr - 30 * on, spread=20 * on)
            self.ears(p, -20 * on)
            if extra:
                extra(p, u, t, on)
        return self.C.clip('Death', dur, fn, floor='body', settle=(land, land + 0.12))

    def spawn_drop(self, dur=1.2, height=3.2, beats=3, flap_kw=None, buzz=False, extra=None):
        """drops in from above with the wings swept back, flares, overshoots a little and takes up its hover"""
        fk = flap_kw or {}
        n = int(round(dur * B.FPS))

        def fn(p, u):
            r = sm(u, 0.0, 0.55)
            d = 1 - r
            fl = bump(u, 0.4, 0.55, 0.6, 0.8)
            ph = (u * beats * 1.3) % 1.0
            self.flap(p, ph, buzz=(int(round(u * n)) if buzz else None), k=(1.0 if buzz else sm(u, 0.45, 0.75)), **fk)
            if not buzz:
                self.wings(p, up=[35 * d + 25 * fl, 10 * d + 10 * fl, 0], fold=[0, 30 * d, 55 * d])
            self.body(p, dz=height * d * d - 0.12 * fl, pitch=40 * d - 22 * fl)
            self.head(p, pitch=-20 * d + 10 * fl)
            self.tailw(p, u, lift=20 * d - 10 * fl)
            self.legs(p, 60 * d + 15)
            if extra:
                extra(p, u, r)
        return self.C.clip('Spawn', dur, fn)


# ================================================================================================ geometry
def membrane(lead, trail, nu=14, nv=5, camber=0.03, th=0.004, scallop=None, tears=0.0, seed=0, droop=0.0):
    """wing sheet between a leading-edge polyline and a trailing-edge polyline (both from the body outwards), as a
    closed thin slab. scallop = (count, depth): the trailing edge is cut into arcs between the finger tips;
    tears = random extra notches"""
    rng = np.random.default_rng(seed)
    Lp = M.polyline(np.asarray(lead, dtype=float), nu)
    Tp = M.polyline(np.asarray(trail, dtype=float), nu)
    G = np.zeros((nu, nv, 3))
    for i in range(nu):
        u = i / (nu - 1)
        cut = 0.0
        if scallop:
            cnt, dep = scallop
            cut = dep * abs(math.sin(math.pi * u * cnt)) ** 0.7
        if tears:
            cut += tears * max(0.0, rng.normal(0.0, 1.0)) * 0.5
        cut = min(cut, 0.8)
        for j in range(nv):
            v = j / (nv - 1)
            q = Lp[i] * (1 - v * (1 - cut)) + Tp[i] * (v * (1 - cut))
            q = q + V(0, 0, camber * math.sin(math.pi * v) * (0.5 + 0.5 * math.sin(math.pi * u)) - droop * v * v)
            G[i, j] = q
    return P.sheet_solid(G, th)


def dish(c, axis, r, depth=0.06, n=12, m=5, th=0.006, squash=1.0, upv=(0, 0, 1)):
    """concave dish (bat ear) centred at c, opening along `axis`; squash < 1 narrows it sideways"""
    ax = unit(axis)
    u = np.cross(ax, upv)
    if np.linalg.norm(u) < 1e-3:
        u = np.cross(ax, (0, 1, 0))
    u = unit(u); w = np.cross(u, ax)
    G = np.zeros((m + 1, n, 3))
    for i in range(m + 1):
        t = i / m
        rr = r * t
        for j in range(n):
            a = 2 * math.pi * j / n
            G[i, j] = np.asarray(c, dtype=float) + u * (rr * math.cos(a) * squash) + w * (rr * math.sin(a)) + ax * (depth * (t * t - 1.0))
    s = M.sheet(G, closed_u=True)
    # close the centre: the first ring is a point
    s.cleanup(1e-7)
    s.solidify(th)
    s.smooth = 180.0
    return s


# ------------------------------------------------------------------------------------------------ additions by H3
def panes(lead, trail, sub=5, nv=5, th=0.008, notch=0.25, camber=0.0, droop=0.0):
    """wing of flat panes with STRAIGHT edges (glass, slag, metal leaf): lead[k] / trail[k] are matching stations from
    the body outwards (the same number of points on both edges); between two stations the trailing edge is cut back
    towards the leading edge in a V (notch = fraction of the chord, one value or one per interval), so the trailing
    edge is a row of points. Closed thin slab."""
    Lp = [np.asarray(q, dtype=float) for q in lead]
    Tp = [np.asarray(q, dtype=float) for q in trail]
    assert len(Lp) == len(Tp) and len(Lp) >= 2
    k = len(Lp) - 1
    nt = np.broadcast_to(np.asarray(notch, dtype=float), (k,))
    cols = []
    for i in range(k):
        for j in range(sub + (1 if i == k - 1 else 0)):
            t = j / sub
            a = Lp[i] * (1 - t) + Lp[i + 1] * t
            b = Tp[i] * (1 - t) + Tp[i + 1] * t
            b = b + (a - b) * (nt[i] * (1.0 - abs(2 * t - 1)))
            cols.append((a, b, (i + t) / k))
    G = np.zeros((len(cols), nv, 3))
    for c, (a, b, u) in enumerate(cols):
        for j in range(nv):
            v = j / (nv - 1)
            G[c, j] = a * (1 - v) + b * v + V(0, 0, camber * math.sin(math.pi * v) * (0.5 + 0.5 * math.sin(math.pi * u)) - droop * v * v)
    s = P.sheet_solid(G, th)
    s.smooth = 40.0
    s.grid = G.reshape(-1, 3).copy()                                   # for pane_weights
    s.gu = np.repeat(np.array([c[2] * k for c in cols]), nv)
    s.gv = np.tile(np.linspace(0.0, 1.0, nv), len(cols))
    return s


def pane_weights(part, lead_bones, trail_bones):
    """skin a panes() wing so that it stays on its spars: lead_bones[i] = the bone the leading edge of interval i rides
    on; trail_bones[k] = the bone (or {bone: weight}) of trail station k. Weights run linearly along the trailing edge
    and from the leading edge to the trailing edge."""
    R = M.RIG
    k = len(trail_bones) - 1
    assert len(lead_bones) == k

    def vec(b):
        w = np.zeros(R.nb, dtype=np.float32)
        for n, x in (b.items() if isinstance(b, dict) else [(b, 1.0)]):
            w[R.idx[n]] = x
        return w
    T = [vec(b) for b in trail_bones]
    Lb = [vec(b) for b in lead_bones]
    Wg = np.zeros((len(part.grid), R.nb), dtype=np.float32)
    for q in range(len(part.grid)):
        i = min(int(part.gu[q] + 1e-9), k - 1)
        t = part.gu[q] - i
        Wg[q] = Lb[i] * (1 - part.gv[q]) + (T[i] * (1 - t) + T[i + 1] * t) * part.gv[q]
    d = np.linalg.norm(part.v[:, None, :] - part.grid[None, :, :], axis=2)
    part.W = Wg[d.argmin(1)]
    part.bone = None
    return part


def kite_skel(h, half, front, back, hole, nx=3, nf=2, nb=3, ribbons=None, extra=None):
    """flat kite / ray without a body: `body` at the centre (h above the origin), flex chains from the rim of the central
    hole (radius `hole`) to the four corners: fx_<s>1..nx (side corners at x = +-half), ff1..nf (front corner at
    y = -front), fb1..nb (back corner at y = +back). ribbons = {name: (points, parent bone)} -> chains <name>1.."""
    sk = B.Sk()
    sk.bone('body', (0, 0.5 * hole, h), (0, -0.5 * hole, h), 'root')
    for s, sg in (('l', 1.0), ('r', -1.0)):
        sk.chain('fx_%s' % s, [(sg * (hole + (half - hole) * i / nx), 0, h) for i in range(nx + 1)], 'body')
    sk.chain('ff', [(0, -(hole + (front - hole) * i / nf), h) for i in range(nf + 1)], 'body')
    sk.chain('fb', [(0, hole + (back - hole) * i / nb, h) for i in range(nb + 1)], 'body')
    for name, (pts, par) in (ribbons or {}).items():
        sk.chain(name, pts, par)
    if extra:
        extra(sk)
    return sk


class Kite(Flyer):
    """motion blocks of a kite that NEVER flaps: it flexes along its diagonals (the four arms curl up or down) and
    trails ribbon chains. Bones as kite_skel makes them; rib = {key: chain prefix} names the ribbon chains."""

    def __init__(self, C, h, rib=None):
        Flyer.__init__(self, C, h)
        names = C.rg.names

        def chain(prefix):
            out = [n for n in names if n.startswith(prefix) and n[len(prefix):].isdigit()]
            return sorted(out, key=lambda n: int(n[len(prefix):]))
        self.fx = {'l': chain('fx_l'), 'r': chain('fx_r')}
        self.ff = chain('ff')
        self.fb = chain('fb')
        self.rib = {k: chain(v) for k, v in (rib or {}).items()}

    @staticmethod
    def _share(n, grow):
        w = np.array([(i + 1.0) ** grow for i in range(n)])
        return w / w.sum()

    def flex(self, p, l=0.0, r=None, f=0.0, b=0.0, grow=1.0):
        """curl the arms: degrees by which the left / right side corner, the front and the back corner turn towards
        +Z (negative = down); the bend is shared over the chain, more towards the tip (grow)"""
        r = l if r is None else r
        for n, w in zip(self.fx['l'], self._share(len(self.fx['l']), grow)):
            p.rot(n, y=-l * w)
        for n, w in zip(self.fx['r'], self._share(len(self.fx['r']), grow)):
            p.rot(n, y=r * w)
        for n, w in zip(self.ff, self._share(len(self.ff), grow)):
            p.rot(n, x=-f * w)
        for n, w in zip(self.fb, self._share(len(self.fb), grow)):
            p.rot(n, x=b * w)

    def wave(self, p, u, n=1, side=10.0, fore=7.0, lag=0.12, ph=0.0, skew=0.0):
        """the ray-like undulation: a wave runs out along the side arms (both in phase, `skew` shifts left against
        right) while the front / back arms move against it (a slow saddle). Degrees per bone at the tip."""
        for s, sg in (('l', 1.0), ('r', -1.0)):
            ch = self.fx[s]
            for i, bn in enumerate(ch):
                a = side * (i + 1.0) / len(ch) * osc(u, n, ph - i * lag + skew * sg)
                p.rot(bn, y=-sg * a)
        for i, bn in enumerate(self.ff):
            p.rot(bn, x=-fore * (i + 1.0) / len(self.ff) * osc(u, n, ph + 0.5 - i * lag))
        for i, bn in enumerate(self.fb):
            p.rot(bn, x=fore * (i + 1.0) / len(self.fb) * osc(u, n, ph + 0.5 - i * lag))

    def ribbons(self, p, u=0.0, n=2, amp=9.0, amp_z=None, lag=0.16, lift=0.0, spread=0.0, ph=0.0, curl=0.0):
        """ribbon tails (chains pointing back): lift = degrees the whole ribbon rises at its root, spread = degrees it
        swings outwards at its root, curl = extra rise per link, plus a wave that grows towards the tip"""
        for k, key in enumerate(sorted(self.rib)):
            ch = self.rib[key]
            sg = 1.0 if self.C.rg.H(ch[0])[0] >= 0 else -1.0
            for i, bn in enumerate(ch):
                g = 0.4 + 0.6 * i / max(len(ch) - 1, 1)
                az = amp if amp_z is None else amp_z
                p.rot(bn, x=(lift if i == 0 else 0.0) + curl + amp * g * osc(u, n, ph + 0.13 * k - i * lag),
                      z=(-sg * spread if i == 0 else 0.0) + az * g * osc(u, n, ph + 0.25 + 0.21 * k - i * lag))
