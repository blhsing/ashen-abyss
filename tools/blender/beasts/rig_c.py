"""Rig C - arthropods (beetles, spiders, crabs, scorpion, woodlouse). Skeleton:
  root > body > head > mandible_l/r, antenna_l/r     body > abdomen1 > abdomen2 ...     body > tail1..n (scorpion)
  body > leg<i>_<s>_a > _b > _c   (i = 1.. from the front; _c is left out for two-bone swarm legs)   body > claw_<s>_a > _b > _c
Legs are rigid chitin segments, one per bone (a knuckle hides each joint). IK runs on a + b towards the ankle, the
tarsus (c) keeps its world angle, so planted feet do not slide at the reported speed."""
import math
import numpy as np
import blib as B
from blib import M, V, unit, sm, bump, osc, rx, ry, rz, euler, I3, Leg, gait_foot
import bparts as P


def leg_def(root, knee, ankle, toe=None, r=(0.022, 0.016, 0.01), y_knee=None):
    """one leg of the LEFT (+X) side: joint positions in armature space; toe=None -> two-bone leg (ankle = foot tip)"""
    return dict(root=V(root), knee=V(knee), ankle=V(ankle), toe=None if toe is None else V(toe), r=r)


def radial_legs(n, y0, y1, x_root, z_root, x_foot, knee_h, fan=(-55, 55), knee_out=0.55, toe=0.05, ankle_h=0.045, r=(0.022, 0.016, 0.01), two=False):
    """n legs a side fanned between the angles fan[0] (front, deg from the X axis towards -Y) and fan[1] (rear);
    roots on the line y0..y1 at (x_root, z_root); feet on a circle x_foot from the root"""
    out = []
    for i in range(n):
        t = i / max(n - 1, 1)
        a = math.radians(fan[0] + (fan[1] - fan[0]) * t)
        d = V(math.cos(a), math.sin(a), 0)
        root = V(x_root, y0 + (y1 - y0) * t, z_root)
        knee = root + d * (x_foot * knee_out) + V(0, 0, knee_h - z_root)
        if two:
            out.append(leg_def(root, knee, root + d * x_foot - V(0, 0, z_root), None, r))
        else:
            ankle = root + d * (x_foot - toe) + V(0, 0, ankle_h - z_root)
            out.append(leg_def(root, knee, ankle, root + d * x_foot - V(0, 0, z_root), r))
    return out


def arth_skel(body, head, legs, abd=None, mand=None, ant=None, tail=None, claws=None, extra=None):
    """body = (head pos, tail pos) of the body bone; head = (joint, tip) or None; abd = [points] chain from the body;
    mand = (joint, tip) left side; ant = [points] left side chain; tail = [points]; claws = leg_def (left side)"""
    sk = B.Sk()
    sk.bone('body', body[0], body[1], 'root')
    hb = 'body'
    if head is not None:
        sk.bone('head', head[0], head[1], 'body')
        hb = 'head'
    if mand is not None:
        sk.lr('mandible', mand[0], mand[1], hb)
    if ant is not None:
        if len(ant) == 2:
            sk.lr('antenna', ant[0], ant[1], hb)
        else:
            sk.chain_lr('antenna', ant, hb)
    if abd is not None:
        sk.chain('abdomen', abd, 'body')
    if tail is not None:
        sk.chain('tail', tail, 'abdomen%d' % (len(abd) - 1) if abd is not None else 'body')
    for i, lg in enumerate(legs):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            m = (sg, 1, 1)
            n = 'leg%d_%s' % (i + 1, s)
            sk.bone(n + '_a', lg['root'] * m, lg['knee'] * m, 'body')
            sk.bone(n + '_b', lg['knee'] * m, lg['ankle'] * m, n + '_a')
            if lg['toe'] is not None:
                sk.bone(n + '_c', lg['ankle'] * m, lg['toe'] * m, n + '_b')
    if claws is not None:
        for s, sg in (('l', 1.0), ('r', -1.0)):
            m = (sg, 1, 1)
            n = 'claw_' + s
            sk.bone(n + '_a', claws['root'] * m, claws['knee'] * m, 'body')
            sk.bone(n + '_b', claws['knee'] * m, claws['ankle'] * m, n + '_a')
            sk.bone(n + '_c', claws['ankle'] * m, claws['toe'] * m, n + '_b')      # the moving finger
    if extra:
        extra(sk)
    return sk


def tripod(n):
    """alternating gait for n leg pairs: neighbours on a side and the two legs of a pair are half a cycle apart"""
    return {'leg%d_%s' % (i + 1, s): (0.0 if (i + (s == 'r')) % 2 == 0 else 0.5) for i in range(n) for s in 'lr'}


def wave(n, lag=0.14):
    """metachronal wave running forward along each side (woodlouse, many legs); sides half a cycle apart"""
    return {'leg%d_%s' % (i + 1, s): ((n - 1 - i) * lag + (0.5 if s == 'r' else 0.0)) % 1.0 for i in range(n) for s in 'lr'}


class Arth:
    def __init__(self, C, legs, pivot=None):
        self.C = C
        rg = C.rg
        self.defs = legs
        self.legs = {}
        for i, lg in enumerate(legs):
            for s, sg in (('l', 1.0), ('r', -1.0)):
                n = 'leg%d_%s' % (i + 1, s)
                three = lg['toe'] is not None
                bones = [n + '_a', n + '_b'] + ([n + '_c'] if three else [])
                tip = (lg['toe'] if three else lg['ankle']) * (sg, 1, 1)
                L = Leg(bones, (tip[0], tip[1], 0.0), name=n)
                L.par = 'body'
                L.side = sg
                L.idx = i
                self.legs[n] = L
        C.legs = list(self.legs.values())
        self.n = len(legs)
        b0 = rg.H('body'); b1 = rg.T('body')
        self.pivot = (b0 + b1) / 2 if pivot is None else V(pivot)
        self.has = lambda n: n in rg.idx
        self.abd = [n for n in rg.names if n.startswith('abdomen')]
        self.tail = [n for n in rg.names if n.startswith('tail')]
        self.ant = [n for n in rg.names if n.startswith('antenna')]
        self.front = rg.T('head') if self.has('head') else b1

    # ---- building blocks
    def body(self, p, dx=0.0, dy=0.0, dz=0.0, pitch=0.0, roll=0.0, yaw=0.0, pivot=None):
        R = euler(pitch, roll, yaw)
        h = self.C.rg.H('body')
        pv = self.pivot if pivot is None else np.asarray(pivot, dtype=float)
        p.setR('body', R)
        p.move('body', (pv - h) - R @ (pv - h) + V(dx, dy, dz))

    def head(self, p, pitch=0.0, yaw=0.0, roll=0.0):
        if self.has('head'):
            p.rot('head', x=pitch, z=yaw, y=roll)

    def jaws(self, p, deg):
        """deg > 0 opens the mandibles sideways"""
        if self.has('mandible_l'):
            p.rot('mandible_l', z=deg)
            p.rot('mandible_r', z=-deg)

    def feel(self, p, u, amp=10.0, n=2.0, lift=0.0, spread=0.0):
        """antennae: waving, lifted, spread"""
        for b in self.ant:
            sg = 1.0 if b.endswith('_l') else -1.0
            k = 1.0
            p.rot(b, x=-lift * k + amp * 0.6 * osc(u, n, 0.13 if sg > 0 else 0.4), z=sg * (spread + amp * osc(u, n, 0.0 if sg > 0 else 0.27)))

    def abdo(self, p, pitch=0.0, yaw=0.0, roll=0.0):
        for b in self.abd:
            p.rot(b, x=pitch / len(self.abd), z=yaw / len(self.abd), y=roll / len(self.abd))

    def tailc(self, p, curl=0.0, yaw=0.0, u=0.0, sway=0.0):
        """scorpion tail: curl > 0 = tighter over the back"""
        n = max(len(self.tail), 1)
        for i, b in enumerate(self.tail):
            p.rot(b, x=curl / n, z=yaw / n + sway * osc(u, 1, -0.1 * i))

    def claws(self, p, open=0.0, spread=0.0, lift=0.0, fold=0.0, side=None):
        """pincer arms claw_<s>_a/b/c: open = the moving finger swings outwards (deg), spread = the arms swing outwards
        (negative = across the front), lift = the arms are raised, fold = the forearm bends inwards; side 'l' / 'r' / None"""
        for s, sg in (('l', 1.0), ('r', -1.0)):
            if side and s != side:
                continue
            p.rot('claw_%s_a' % s, x=-lift, z=sg * spread)
            p.rot('claw_%s_b' % s, z=-sg * fold)
            p.rot('claw_%s_c' % s, z=sg * open)

    def plant(self, p, air=None, off=None, tuck=None):
        B.plant_legs(p, self.legs, air, off, tuck)

    def reach(self, clip, bone=None, pt=None):
        return self.C.reach(clip, bone or ('head' if self.has('head') else 'body'), self.front if pt is None else pt)

    # ---- locomotion
    def gait(self, name, v, T, duty, phases, lift=0.05, bob=0.004, roll=1.0, yaw=1.5, pitch=0.0, crouch=0.0, feel=8.0, jaw=4.0,
             abd=2.0, tail=None, swing_tilt=18.0, sway=0.0, extra=None):
        T = max(2, round(T * B.FPS)) / B.FPS
        S = v * duty * T
        for k, lg in self.legs.items():
            lg.phase = phases[k]

        def fn(p, u):
            self.body(p, dz=-crouch + bob * math.cos(4 * math.pi * u), dx=sway * osc(u, 1), pitch=pitch + 0.6 * osc(u, 2, 0.1),
                      roll=roll * osc(u, 1, 0.1), yaw=yaw * osc(u, 1, 0.3))
            self.head(p, yaw=-yaw * 0.8 * osc(u, 1, 0.3))
            self.jaws(p, jaw + 2 * osc(u, 2))
            self.feel(p, u, amp=feel, n=1)
            self.abdo(p, pitch=abd * osc(u, 2, 0.2), yaw=-yaw * 1.5 * osc(u, 1, 0.45))
            if tail is not None:
                self.tailc(p, curl=tail[0] + tail[1] * osc(u, 2, 0.3), u=u, sway=tail[2] if len(tail) > 2 else 0.0)
            for k, lg in self.legs.items():
                Pg, sw, s = gait_foot(lg, u, S, duty, lift)
                if len(lg.b) == 3:
                    p.leg(lg, Pg, R3=rx(swing_tilt * sw))
                else:
                    p.leg(lg, Pg)
            if extra:
                extra(p, u)
        c = self.C.clip(name, T, fn)
        self.C.meta['walk_speed' if name == 'Walk' else 'run_speed'] = round(v, 3)
        return c

    def idle(self, dur=3.0, breath=0.006, feel=14.0, jaw=6.0, sway=0.006, abd=3.0, tail=None, extra=None, look=6.0):
        def fn(p, u):
            b = osc(u, 2)
            self.body(p, dz=breath * b, dx=sway * osc(u, 1), pitch=0.8 * b, roll=0.6 * osc(u, 1, 0.2))
            self.head(p, pitch=1.5 * osc(u, 2, 0.1), yaw=look * osc(u, 1) * (0.6 + 0.4 * osc(u, 2, 0.2)))
            self.jaws(p, jaw * (0.5 + 0.5 * osc(u, 4)) * bump(u, 0.3, 0.4, 0.7, 0.8) + 2)
            self.feel(p, u, amp=feel, n=2)
            self.abdo(p, pitch=abd * b)
            if tail is not None:
                self.tailc(p, curl=tail[0] + tail[1] * b, u=u, sway=tail[2] if len(tail) > 2 else 0.0)
            self.plant(p)
            if extra:
                extra(p, u)
        return self.C.clip('Idle', dur, fn)

    # ---- attacks
    def snap(self, name='Attack1', dur=0.7, hit=0.36, reach=0.18, gape=38.0, lift=6.0, drop=0.02, extra=None):
        """mandible snap: rear back with the jaws wide, lunge, shut"""
        hu = hit / dur

        def fn(p, u):
            w = bump(u, 0.0, hu * 0.55, hu * 0.7, hu)
            s = bump(u, hu * 0.7, hu, hu + 0.12, 0.92)
            self.body(p, dy=0.05 * w - reach * s, dz=0.015 * w - drop * s, pitch=-lift * w + 5 * s)
            self.head(p, pitch=-8 * w + 6 * s)
            self.jaws(p, gape * bump(u, hu * 0.15, hu * 0.6, hu - 0.03, hu + 0.02) + 2)
            self.feel(p, u, amp=6, n=3, lift=20 * w, spread=-10 * s)
            self.abdo(p, pitch=-6 * w + 4 * s)
            self.plant(p)
            if extra:
                extra(p, u, w, s)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def ram(self, name='Attack2', dur=1.0, hit=0.55, reach=0.4, lower=10.0, back=0.1, gape=10.0, extra=None):
        """shell ram: back up and lower the front, drive forward with the whole body, recoil"""
        hu = hit / dur

        def fn(p, u):
            w = bump(u, 0.0, hu * 0.5, hu * 0.72, hu * 0.95)
            s = bump(u, hu * 0.72, hu, hu + 0.1, 0.95)
            k = bump(u, hu, hu + 0.04, hu + 0.08, hu + 0.2)            # the knock
            self.body(p, dy=back * w - reach * s, dz=-0.02 * w - 0.015 * s + 0.01 * k, pitch=lower * (0.6 * w + s) - 3 * k)
            self.head(p, pitch=6 * s)
            self.jaws(p, gape * s + 2)
            self.feel(p, u, amp=4, n=3, lift=-25 * (w + s), spread=14 * s)
            self.abdo(p, pitch=-5 * s + 4 * k)
            self.plant(p)
            if extra:
                extra(p, u, w, s)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def hop(self, name='Attack2', dur=0.9, hit=0.5, fwd=0.4, up=0.25, gape=30.0, pitch=18.0, extra=None):
        """crouch, hop forward with all legs off the ground, land biting"""
        hu = hit / dur

        def fn(p, u):
            cr = bump(u, 0.0, hu * 0.45, hu * 0.55, hu * 0.7)
            fl = bump(u, hu * 0.55, hu * 0.8, hu + 0.02, hu + 0.3)
            arc = math.sin(math.pi * min(1.0, max(0.0, (u - hu * 0.55) / max(hu * 0.45, 1e-3)))) if u < hu else 0.0
            ln = bump(u, hu - 0.03, hu, hu + 0.08, 0.95)
            self.body(p, dy=0.04 * cr - fwd * fl, dz=-0.04 * cr + up * arc - 0.02 * ln, pitch=-pitch * 0.5 * cr - pitch * fl * (1 - ln) + 8 * ln)
            self.head(p, pitch=6 * ln)
            self.jaws(p, gape * bump(u, hu * 0.5, hu * 0.85, hu - 0.01, hu + 0.04) + 2)
            self.feel(p, u, amp=5, n=3, lift=-20 * fl)
            self.abdo(p, pitch=-8 * fl + 6 * ln)
            air = bump(u, hu * 0.58, hu * 0.7, hu - 0.05, hu + 0.02)
            self.plant(p, air=air * 0.92, off={k: (0, -fwd * fl, 0) for k in self.legs}, tuck=self.bend(-12, 35, 0))
            if extra:
                extra(p, u, cr, fl)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def hit(self, dur=0.4, back=0.05, extra=None):
        def fn(p, u):
            k = bump(u, 0.0, 0.2, 0.3, 1.0)
            self.body(p, dy=back * k, dz=-0.025 * k, roll=6 * k, pitch=-6 * k, yaw=-5 * k)
            self.head(p, pitch=-8 * k, yaw=-10 * k)
            self.jaws(p, 20 * k + 2)
            self.feel(p, u, amp=10 * k, n=3, lift=-30 * k, spread=20 * k)
            self.abdo(p, pitch=8 * k)
            self.plant(p)
            if extra:
                extra(p, u, k)
        return self.C.clip('Hit', dur, fn)

    def bend(self, a, b=0.0, c=0.0, only=None):
        """tuck table for plant(): every leg flexed about its own knee axis (a = femur, b = tibia, c = tarsus; + = flex)"""
        return {k: dict(bend=(a, b, c)) for k in self.legs if only is None or only(self.legs[k])}

    def death_flip(self, dur=1.5, side=1.0, curl=(95.0, 75.0, 30.0), up=0.22, back_h=None, extra=None, roll=178.0):
        """the classic insect death: jerks into the air, lands on its back, the legs curl in over the belly and twitch.
        back_h = height of the body pivot when it lies on its back (default: same as standing)"""
        zc = float(self.pivot[2])

        def fn(p, u):
            f = sm(u, 0.05, 0.5)
            arc = math.sin(math.pi * sm(u, 0.0, 0.5)) * up
            tw = bump(u, 0.6, 0.66, 0.7, 0.85) * osc(u, 9)
            self.body(p, dz=arc + (0 if back_h is None else (back_h - zc) * f), roll=side * roll * f, pitch=6 * f, dx=side * 0.1 * f)
            self.head(p, pitch=10 * f)
            self.jaws(p, 25 * f + 2)
            self.feel(p, u, amp=8 * (1 - f), n=3, lift=-30 * f, spread=25 * f)
            self.abdo(p, pitch=-10 * f)
            if self.tail:
                self.tailc(p, curl=-40 * f)
            c = sm(u, 0.2, 0.75)
            tk = {}
            for k, lg in self.legs.items():
                j = c * (1 + 0.1 * tw * (1 if lg.idx % 2 else -1))
                tk[k] = dict(bend=(curl[0] * j, curl[1] * j, curl[2] * j))
            self.plant(p, air=sm(u, 0.02, 0.2), tuck=tk)
            if extra:
                extra(p, u, f)
        return self.C.clip('Death', dur, fn, floor='body', settle=(0.4, 0.6))

    def death_drop(self, dur=1.3, splay=0.12, tilt=14.0, drop=None, extra=None, pitch=3.0):
        """legs give way: the body drops on its belly, tilts, legs splay and go slack"""
        zc = float(self.pivot[2])

        def fn(p, u):
            a = sm(u, 0.0, 0.25)
            f = sm(u, 0.15, 0.6)
            st = bump(u, 0.6, 0.66, 0.7, 0.8)
            self.body(p, dz=-(zc if drop is None else drop) * f + 0.03 * a * (1 - f) + 0.012 * st, roll=tilt * f, pitch=5 * a * (1 - f) + pitch * f, yaw=6 * f)
            self.head(p, pitch=14 * f, roll=-8 * f)
            self.jaws(p, 22 * f + 2)
            self.feel(p, u, amp=10 * (1 - f), n=3, lift=-35 * f, spread=30 * f)
            self.abdo(p, pitch=8 * f)
            if self.tail:
                self.tailc(p, curl=-70 * f, yaw=30 * f)
            self.plant(p, off={k: (lg.side * splay * f, 0.0, 0.0) for k, lg in self.legs.items()})
            if extra:
                extra(p, u, f)
        return self.C.clip('Death', dur, fn, floor='body', settle=(0.4, 0.6))

    def spawn_dig(self, dur=1.2, depth=None, shake=7.0, extra=None):
        """digs itself out: rises front first, shakes the soil off"""
        dep = (float(self.C.rg.H('body')[2]) + 0.35) if depth is None else depth

        def fn(p, u):
            r = sm(u, 0.0, 0.6)
            sh = bump(u, 0.6, 0.68, 0.84, 0.95)
            self.body(p, dz=-dep * (1 - r), pitch=-24 * (1 - r), roll=shake * sh * osc(u, 9), yaw=shake * 0.5 * sh * osc(u, 9, 0.25))
            self.head(p, pitch=-10 * (1 - r))
            self.jaws(p, 16 * (1 - r) + 2)
            self.feel(p, u, amp=14 * sh + 6, n=5, lift=-20 * (1 - r))
            self.abdo(p, pitch=6 * (1 - r))
            if self.tail:
                self.tailc(p, curl=-30 * (1 - r))
            self.plant(p, air={k: 1 - sm(u, 0.2 + 0.25 * lg.idx / max(self.n - 1, 1), 0.5 + 0.25 * lg.idx / max(self.n - 1, 1))
                               for k, lg in self.legs.items()},
                       tuck=self.bend(-20, 45, 0))
            if extra:
                extra(p, u, r)
        return self.C.clip('Spawn', dur, fn, under_ok=True)

    def spawn_drop(self, dur=1.0, height=2.5, land=0.6, line=False, squash=0.06, tuck=None, extra=None):
        """comes down from above (spiders): legs drawn in, they open and catch the ground, the body dips and recovers.
        line=False: a free fall; True: lowered steadily on a thread. extra(p, u, t) with t = 0..1 until the landing"""
        def fn(p, u):
            t = min(1.0, u / land)
            h = height * ((1 - sm(t, 0.0, 1.0) ** 0.8) if line else (1 - t * t))
            dip = bump(u, land - 0.02, land + 0.07, land + 0.09, min(0.98, land + 0.33))
            self.body(p, dz=h - squash * dip, pitch=-6 * (1 - t) + 3 * dip)
            self.head(p, pitch=8 * dip)
            self.jaws(p, 10 * (1 - t) + 2)
            self.feel(p, u, amp=6, n=3, lift=-15 * (1 - t))
            self.abdo(p, pitch=-8 * dip)
            self.plant(p, air=1 - sm(u, land * 0.72, land), tuck=tuck or self.bend(-25, 55, 0))
            if extra:
                extra(p, u, t)
        return self.C.clip('Spawn', dur, fn)


# ================================================================================================ geometry
def add_legs(C, legs, look, n=7, spur=0.0, claw=0.5, hair=None, radii=None, skip=()):
    """rigid chitin segments for every leg: a (coxa + femur), b (tibia), c (tarsus with a claw tip)"""
    for i, lg in enumerate(legs):
        if i in skip:
            continue
        r = lg['r'] if radii is None else radii[i]
        for s, sg in (('l', 1.0), ('r', -1.0)):
            m = (sg, 1, 1)
            nme = 'leg%d_%s' % (i + 1, s)
            root, knee, ankle = lg['root'] * m, lg['knee'] * m, lg['ankle'] * m
            # sink the root of the femur into the body
            C.rigid(P.seg_tube(root - unit(knee - root) * r[0] * 1.2, knee, r[0], r[0] * 0.82, n=n, bulge=1.2), nme + '_a', look)
            if lg['toe'] is not None:
                toe = lg['toe'] * m
                C.rigid(P.seg_tube(knee - unit(ankle - knee) * r[1] * 0.7, ankle, r[1] * 1.05, r[1] * 0.7, n=n, bulge=1.35), nme + '_b', look)
                t = P.seg_tube(ankle - unit(toe - ankle) * r[2] * 0.7, toe + V(0, 0, r[2] * 0.3), r[2], r[2] * claw, n=max(5, n - 2), bulge=1.4)
                C.rigid(t, nme + '_c', look)
            else:
                C.rigid(P.seg_tube(knee - unit(ankle - knee) * r[1] * 0.7, ankle + V(0, 0, 0.002), r[1] * 1.05, r[1] * 0.25, n=n, bulge=1.35), nme + '_b', look)
            if spur:
                d = unit(ankle - knee)
                side = unit(np.cross(d, (0, 0, 1)))
                sp = []
                for k in range(3):
                    q = knee + (ankle - knee) * (0.3 + 0.22 * k)
                    sp.append(P.cone(q - side * sg * 0.0 + V(0, 0, 0), unit(side * sg * 0.3 - d * 0.3 + V(0, 0, 0.9)), spur, r[1] * 0.45, n=4))
                C.rigid(M.join(sp), nme + '_b', look, smooth=50)


def shell(length, width, height, n=16, m=6, power=2.4, front=1.0, rear=1.0, flat=0.0, z0=0.0, groove=0.0):
    """domed carapace with its long axis on Y (front at -Y), origin at the centre of its floor.
    front / rear < 1 narrow that end; flat pushes the top down; groove = depth of a seam along the midline"""
    d = P.dome(width / 2, length / 2, height, n=n, m=m, z0=z0, power=power)
    v = d.v
    t = v[:, 1] / (length / 2)
    k = np.where(t < 0, 1 - (1 - front) * np.clip(-t, 0, 1) ** 1.5, 1 - (1 - rear) * np.clip(t, 0, 1) ** 1.5)
    v[:, 0] *= k
    if flat:
        v[:, 2] = z0 + (v[:, 2] - z0) * (1 - flat * np.clip((v[:, 2] - z0) / height, 0, 1))
    if groove:
        g = np.exp(-(v[:, 0] / (width * 0.035)) ** 2) * ((v[:, 2] - z0) > height * 0.08)
        v[:, 2] -= groove * g
    d.v = v
    return d


def plate(outline, th=0.012, z=0.0, dome=0.0, tilt=0.0, bevel=0.3):
    """flat shield from a 2D outline (x, y) points (CCW), thickness th, optionally domed towards its middle"""
    o = np.asarray(outline, dtype=float)
    c = o.mean(0)
    rings = []
    for k, zz in ((1.0, -th / 2), (1.0, 0.0), (1.0 - bevel * 0.4, th / 2)):
        rings.append([(c[0] + (x - c[0]) * k, c[1] + (y - c[1]) * k, zz + (dome if zz > 0 else 0.0)) for x, y in o])
    p = M.loft(np.array(rings), cap0=True, cap1=True).recalc()
    p.v[:, 2] += z
    if tilt:
        p.rot('x', tilt, pivot=(0, c[1], z))
    p.smooth = 30.0
    return p


def antenna(pts, r0, r1, club=0.0, n=5, club_n=3):
    """thin segmented feeler along pts; club > 0 adds a lamellate club (beetles) at the tip"""
    pts = [V(q) for q in pts]
    parts = [P.tubep(pts, list(np.linspace(r0, r1, len(pts))), m=max(6, 3 * len(pts)), n=n)]
    if club:
        d = unit(pts[-1] - pts[-2])
        side = unit(np.cross(d, (0, 0, 1)))
        for k in range(club_n):
            c = pts[-1] + d * club * (0.12 + 0.36 * k)
            parts.append(P.blob(c, club * 0.26, club * 0.26, club * 0.5, n=6, m=4, rot=M.rot_to((0, 0, 1), unit(side + d * 0.2))))
    q = M.join(parts)
    q.smooth = 60.0
    return q


def mandible(joint, tip, r, curve=0.3, n=6, teeth=2, side=1.0):
    """curved pincer from joint to tip, bowed outwards (side = +1 for the left one), with inner teeth"""
    j = V(joint); t = V(tip)
    d = t - j
    out = unit(np.cross((0, 0, 1), d)) * (-side)
    Ln = np.linalg.norm(d)
    path = [j + d * s + out * (math.sin(math.pi * s) * curve * Ln) for s in np.linspace(0, 1, 7)]
    rr = [r * (1 - s) ** 0.7 + 0.0012 for s in np.linspace(0, 1, 7)]
    parts = [M.tube(np.array(path), np.array(rr), n=n, r2=np.array(rr) * 0.6, up=(0, 0, 1), cap0=True, cap1=True)]
    for k in range(teeth):
        s = 0.35 + 0.25 * k
        q = j + d * s + out * (math.sin(math.pi * s) * curve * Ln)
        parts.append(P.cone(q, -out + unit(d) * 0.3, r * 1.2, r * 0.45, n=4))
    p = M.join(parts)
    p.smooth = 50.0
    return p
