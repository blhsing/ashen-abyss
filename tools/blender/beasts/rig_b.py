"""Rig B - quadrupeds. Shared skeleton (same names / hierarchy for every creature, proportions from the joint table):
  root > hip > spine1 > spine2 > chest > neck1 > neck2 > head > jaw (+ ear_l/r)
         hip > tail1..n ;  chest > f_upper_s > f_lower_s > f_foot_s > f_toe_s ;  hip > h_upper_s > h_lower_s > h_foot_s > h_toe_s
Clip generators are parametric (speed, period, duty, lift, bob ...); feet are solved by IK against in-place ground
targets, so planted feet do not slide at the reported speed."""
import math
import numpy as np
import blib as B
from blib import M, V, unit, sm, bump, osc, rx, ry, rz, euler, I3, Leg, gait_foot
import bparts as P
from mathutils import Matrix, Quaternion

WALK4 = dict(h_l=0.0, f_l=0.75, h_r=0.5, f_r=0.25)          # 4-beat lateral sequence walk
TROT = dict(f_l=0.0, h_r=0.0, f_r=0.5, h_l=0.5)
GALLOP = dict(h_l=0.0, h_r=0.92, f_l=0.55, f_r=0.47)        # rotary gallop: hind pair, suspension, fore pair
BOUND = dict(h_l=0.0, h_r=0.0, f_l=0.5, f_r=0.5)


def quad_J(L=0.6, hs=0.64, hh=0.62, wf=0.085, wh=0.09, neck=(0.22, 0.16), head=0.28, sag=0.0,
           fleg=(0.80, 0.56, 0.22, 0.055), hleg=(0.84, 0.55, 0.30, 0.055), tail=(0.44, 4, -0.75), sprawl=0.0, bend=1.0, paw=1.0):
    """joint table of an upright quadruped. L = pelvis..chest joint, hs / hh = spine height at shoulders / pelvis,
    neck = (length, rise), fleg / hleg = heights of (top joint, elbow|knee, wrist|hock, toe joint) as fractions of
    hs / hh, tail = (length, bones, slope dz/dy). bend / paw scale the fore-aft zigzag of the legs / the paw length
    (the built-in offsets suit a 0.65 m hound: use ~hs / 0.65 for bigger animals)."""
    if bend != 1.0 or paw != 1.0:
        J = quad_J(L, hs, hh, wf, wh, neck, head, sag, fleg, hleg, tail)
        for key, y0 in (('f', -L / 2), ('h', L / 2)):
            for i, q in enumerate(J[key]):
                q[1] = y0 + (q[1] - y0) * (bend if i < 4 else 1.0)
            J[key][4][1] = J[key][3][1] + (-0.065 * paw)
            J[key + '_c'][1] = J[key][3][1] + (-0.022 * paw)
            J[key + '_c'][0] = J[key][3][0]
        return J
    J = {}
    J['pelvis'] = V(0, L / 2, hh)
    J['chestj'] = V(0, -L / 2, hs)
    J['s1'] = M.lerp(J['pelvis'], J['chestj'], 0.34) + V(0, 0, sag)
    J['s2'] = M.lerp(J['pelvis'], J['chestj'], 0.67) + V(0, 0, sag * 0.7)
    J['n1'] = J['chestj'] + V(0, -neck[0] * 0.55, neck[1] * 0.5)
    J['hj'] = J['chestj'] + V(0, -neck[0], neck[1])
    J['snout'] = J['hj'] + V(0, -head, -head * 0.2)
    J['jawh'] = J['hj'] + V(0, -head * 0.1, -head * 0.16)
    J['chin'] = J['jawh'] + V(0, -head * 0.82, -head * 0.14)
    J['ear'] = (J['hj'] + V(0.05, 0.0, 0.06), J['hj'] + V(0.085, 0.02, 0.2))
    tl, tn, ts = tail
    tb = J['pelvis'] + V(0, 0.07, -0.02)
    J['tail'] = [tb + V(0, tl * i / tn, ts * tl * (i / tn) ** 1.3) for i in range(tn + 1)]
    y = -L / 2
    J['f'] = [V(wf, y - 0.035, fleg[0] * hs), V(wf, y + 0.085, fleg[1] * hs), V(wf * 0.95, y + 0.02, fleg[2] * hs),
              V(wf * 0.95, y + 0.01, fleg[3] * hs), V(wf * 0.95, y - 0.055, 0.018)]
    J['f_c'] = V(wf * 0.95, y - 0.012, 0.0)
    y = L / 2
    J['h'] = [V(wh, y - 0.01, hleg[0] * hh), V(wh * 1.06, y - 0.115, hleg[1] * hh), V(wh, y + 0.075, hleg[2] * hh),
              V(wh, y + 0.04, hleg[3] * hh), V(wh, y - 0.025, 0.018)]
    J['h_c'] = V(wh, y + 0.018, 0.0)
    return J


def sprawl_J(L=0.55, h=0.15, wf=0.10, wh=0.09, rf=0.24, rh=0.26, neck=(0.16, 0.01), head=0.26, tail=(0.95, 6, -0.11),
             up=0.05, sag=0.0):
    """joint table of a SPRAWLER (lizard, toad, newt): belly near the ground, limbs out sideways. Same keys and bone
    names as quad_J, so quad_skel, Quad (gait, clips) and quad_flesh work unchanged. h = spine height, wf / wh = half
    width of the body at the limb roots, rf / rh = sideways distance limb root -> foot, up = how far the elbows /
    knees rise above the limb roots. Elbows point back-out, knees forward-out; the 3rd bone is the wrist / ankle
    segment, the 4th the toes lying on the ground."""
    J = {}
    J['pelvis'] = V(0, L / 2, h)
    J['chestj'] = V(0, -L / 2, h)
    J['s1'] = M.lerp(J['pelvis'], J['chestj'], 0.34) + V(0, 0, sag)
    J['s2'] = M.lerp(J['pelvis'], J['chestj'], 0.67) + V(0, 0, sag * 0.7)
    J['n1'] = J['chestj'] + V(0, -neck[0] * 0.55, neck[1] * 0.5)
    J['hj'] = J['chestj'] + V(0, -neck[0], neck[1])
    J['snout'] = J['hj'] + V(0, -head, -head * 0.12)
    J['jawh'] = J['hj'] + V(0, -head * 0.08, -head * 0.14)
    J['chin'] = J['jawh'] + V(0, -head * 0.86, -head * 0.05)
    J['ear'] = (J['hj'] + V(0.04, 0.0, 0.03), J['hj'] + V(0.06, 0.02, 0.08))
    tl, tn, ts = tail
    if tn:
        tb = J['pelvis'] + V(0, 0.06, -0.01)
        J['tail'] = [tb + V(0, tl * i / tn, ts * tl * (i / tn) ** 0.8) for i in range(tn + 1)]
    zr = h - 0.03
    y = -L / 2
    J['f'] = [V(wf, y, zr), V(wf + rf * 0.55, y + 0.05, zr + up), V(wf + rf * 0.88, y - 0.03, 0.055),
              V(wf + rf, y - 0.07, 0.02), V(wf + rf + 0.02, y - 0.14, 0.012)]
    J['f_c'] = V(wf + rf, y - 0.09, 0.0)
    y = L / 2
    J['h'] = [V(wh, y, zr), V(wh + rh * 0.55, y - 0.08, zr + up), V(wh + rh * 0.9, y + 0.05, 0.055),
              V(wh + rh + 0.01, y + 0.0, 0.02), V(wh + rh + 0.04, y - 0.09, 0.012)]
    J['h_c'] = V(wh + rh + 0.02, y - 0.03, 0.0)
    return J


def quad_skel(J, ears=True, jaw=True, extra=None, lite=False):
    """lite = swarm budget (<= 28 bones): no spine2 (give the joint table a 2-bone tail as well: 31 -> 28 bones)"""
    sk = B.Sk()
    sk.bone('hip', J['pelvis'], J['pelvis'] + V(0, 0.08, 0), 'root')
    if lite:
        mid = (J['s1'] + J['s2']) / 2
        sk.bone('spine1', J['pelvis'], mid, 'hip')
        sk.bone('chest', mid, J['chestj'], 'spine1')
    else:
        sk.bone('spine1', J['pelvis'], J['s1'], 'hip')
        sk.bone('spine2', J['s1'], J['s2'], 'spine1')
        sk.bone('chest', J['s2'], J['chestj'], 'spine2')
    sk.bone('neck1', J['chestj'], J['n1'], 'chest')
    sk.bone('neck2', J['n1'], J['hj'], 'neck1')
    sk.bone('head', J['hj'], J['snout'], 'neck2')
    if jaw:
        sk.bone('jaw', J['jawh'], J['chin'], 'head')
    if ears:
        sk.lr('ear', J['ear'][0], J['ear'][1], 'head')
    if J.get('tail'):
        sk.chain('tail', J['tail'], 'hip')
    names = ('upper', 'lower', 'foot', 'toe')
    for pre, key, par in (('f', 'f', 'chest'), ('h', 'h', 'hip')):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            pr = par
            pts = [q * (sg, 1, 1) for q in J[key]]
            for i in range(4):
                n = '%s_%s_%s' % (pre, names[i], s)
                sk.bone(n, pts[i], pts[i + 1], pr)
                pr = n
    if extra:
        extra(sk)
    return sk


class Quad:
    # leg fold of an animal lying on its belly (degrees about X for upper, lower, foot: + swings the lower end back):
    # fore legs stretched forward like a sphinx, hind legs tucked alongside
    FOLD = dict(f=(35, -110, 10), h=(-40, 95, -80))

    def __init__(self, C, J, tailn=None, glide=(0.07, 0.04)):
        self.C = C
        self.J = J
        rg = C.rg
        self.legs = {}
        for pre, key in (('f', 'f_c'), ('h', 'h_c')):
            for s, sg in (('l', 1.0), ('r', -1.0)):
                b = ['%s_%s_%s' % (pre, n, s) for n in ('upper', 'lower', 'foot', 'toe')]
                lg = Leg(b, J[key] * (sg, 1, 1), name='%s_%s' % (pre, s))
                lg.par = 'chest' if pre == 'f' else 'hip'
                lg.glide = glide[0] if pre == 'f' else glide[1]
                self.legs[lg.name] = lg
        C.legs = list(self.legs.values())
        self.tail = [n for n in rg.names if n.startswith('tail')]
        self.pivot = V(0, 0, (J['pelvis'][2] + J['chestj'][2]) * 0.5 - 0.08)
        self.has_jaw = 'jaw' in rg.idx
        self.has_ears = 'ear_l' in rg.idx
        self.snout = J['snout'].copy()
        self.leg_h = float(J['f'][0][2])
        self.jaw_axis = 'x'         # 'z' = a door hinged at its side
        self.tail_axis = 'z'        # 'y' for a tail that stands upright (its wag is then a sideways sway)
        self._vw = None

    # ---- numeric helpers on the finished geometry (call after the parts are added)
    def _verts(self):
        C = self.C
        if self._vw is None or self._vw[0] != len(C.parts):
            V0, W, _, _ = C._collect()
            rg = C.rg
            hb = set()
            for i, n in enumerate(rg.names):
                j = i
                while j >= 0:
                    if rg.names[j] == 'neck2':
                        hb.add(i)
                        break
                    j = int(rg.par[j])
            head = W[:, sorted(hb)].sum(1) > 0.5 if hb else np.zeros(len(V0), dtype=bool)
            tb = [rg.idx[b] for b in self.tail]
            tail = W[:, tb].sum(1) > 0.5 if tb else np.zeros(len(V0), dtype=bool)
            lb = [i for i, n in enumerate(rg.names) if n[:2] in ('f_', 'h_')]
            leg = W[:, lb].sum(1) > 0.5
            self._vw = (len(C.parts), V0, W, head, tail, leg)
        return self._vw

    def minz(self, fn, u=1.0, part='all'):
        """lowest point (m) of the posed mesh when clip function fn is evaluated at u; part = 'all' | 'head' (head,
        jaw, ears, upper neck and everything on them) | 'tail' | 'body' (all but the head) | 'trunk' (all but head
        and tail) | 'core' (trunk without the legs)"""
        _, V0, W, head, tail, leg = self._verts()
        p = B.Pose(self.C.rg)
        p.u = u
        fn(p, u)
        z = self.C.posed(V0, W, p.fk())[:, 2]
        sel = dict(head=head, tail=tail, body=~head, trunk=~(head | tail), core=~(head | tail | leg)).get(part)
        if sel is None:
            return float(z.min())
        return float(z[sel].min()) if sel.any() else 9.0

    def fit(self, make, val, us, lo=0.0, part='head', zmin=0.012):
        """make(x) -> clip function. The value between lo and val closest to val (x = a 'head down' angle, a tail
        lift ...) that keeps `part` above zmin at the normalised times us; lo must be the safe end"""
        def ok(x):
            f = make(x)
            return all(self.minz(f, u, part) >= zmin for u in us)
        if ok(val):
            return val
        if not ok(lo):
            return lo
        a, b = lo, val            # a is fine, b is not
        for _ in range(12):
            m = 0.5 * (a + b)
            if ok(m):
                a = m
            else:
                b = m
        return a

    # ---- building blocks
    def body(self, p, dx=0.0, dy=0.0, dz=0.0, pitch=0.0, roll=0.0, yaw=0.0, pivot=None):
        """move / rotate the whole body about a pivot (default: its middle)"""
        R = euler(pitch, roll, yaw)
        h = self.C.rg.H('hip')
        pv = self.pivot if pivot is None else np.asarray(pivot, dtype=float)
        p.setR('hip', R)
        p.move('hip', (pv - h) - R @ (pv - h) + V(dx, dy, dz))

    def spine(self, p, flex=0.0, side=0.0, twist=0.0):
        """flex > 0 arches the back (nose end down), side > 0 bends the front towards +X"""
        bs = [(b, k) for b, k in (('spine1', 0.35), ('spine2', 0.35), ('chest', 0.3)) if b in self.C.rg.idx]
        tot = sum(k for b, k in bs)
        for b, k in bs:
            p.rot(b, x=flex * k / tot, z=side * k / tot, y=twist * k / tot)

    def neck(self, p, pitch=0.0, yaw=0.0, roll=0.0, head=0.4):
        """pitch > 0 lowers the head; `head` = share taken by the head joint"""
        k = (1.0 - head) / 2
        p.rot('neck1', x=pitch * k, z=yaw * k)
        p.rot('neck2', x=pitch * k, z=yaw * k)
        p.rot('head', x=pitch * head, z=yaw * head, y=roll)

    def jaw(self, p, deg):
        if self.has_jaw and deg:
            if self.jaw_axis == 'x':
                p.rot('jaw', x=deg)
            else:
                p.rot('jaw', **{self.jaw_axis: deg})

    def tailw(self, p, u, amp=10.0, n=1.0, lag=0.12, lift=0.0, ph=0.0, curl=0.0):
        k = (4.0 / max(len(self.tail), 1)) ** 0.5          # short chains swing each bone further
        for i, b in enumerate(self.tail):
            if self.tail_axis == 'z':
                p.rot(b, z=amp * k * osc(u, n, ph - i * lag * k), x=(-lift + curl * i) * k)
            else:
                p.rot(b, y=amp * k * osc(u, n, ph - i * lag * k), x=(-lift + curl * i) * k)

    def ears(self, p, a=0.0, b=None):
        if self.has_ears:
            p.rot('ear_l', x=a, y=a * 0.5)
            p.rot('ear_r', x=a if b is None else b, y=-(a if b is None else b) * 0.5)

    def plant(self, p, air=None, off=None, tuck=None):
        B.plant_legs(p, self.legs, air, off, tuck)

    def reach(self, clip, bone='head', pt=None):
        return self.C.reach(clip, bone, self.snout if pt is None else pt)

    # ---- locomotion
    def gait(self, name, v, T, duty, phases, lift=0.07, bob=0.012, bobn=2, bobph=0.0, pitch=0.0, pitchph=0.0, roll=1.2,
             yaw=1.5, flex=0.0, flexph=0.0, crouch=0.0, nod=3.0, tail=8.0, tuck=40.0, lean=0.0, headlow=0.0, hlift=None,
             ears=0.0, jaw=0.0, pastern=28.0, side=None, headyaw=None, taillag=0.12, tailph=0.0, extra=None):
        """side = sideways bend of the spine (degrees, default 1.2 x yaw) in counter-phase to the yaw of the hips:
        sprawlers use yaw = Y, side = 2 Y (shoulders and hips swing against each other) with TROT phases;
        headyaw = yaw of the neck that keeps the head pointing ahead; extra(p, u) poses additional bones"""
        T = max(2, round(T * B.FPS)) / B.FPS        # whole frames: the stride below must match the baked period
        S = v * duty * T
        for k, lg in self.legs.items():
            lg.phase = phases[k]
        sd = yaw * 1.2 if side is None else side
        hy = -yaw * 0.8 if headyaw is None else headyaw

        def fn(p, u):
            pt = pitch * math.cos(2 * math.pi * (u - pitchph))
            fl = flex * math.cos(2 * math.pi * (u - flexph))
            self.body(p, dz=-crouch + bob * math.cos(2 * math.pi * (bobn * u - bobph)), pitch=lean + pt - fl * 0.35,
                      roll=roll * osc(u, 1, 0.0), yaw=yaw * osc(u, 1, 0.25))
            self.spine(p, flex=fl, side=-sd * osc(u, 1, 0.25))
            self.neck(p, pitch=headlow - (lean + pt) * 0.8 + nod * math.cos(2 * math.pi * (bobn * u - bobph - 0.15)), yaw=hy * osc(u, 1, 0.25))
            self.tailw(p, u, amp=tail, n=1, lift=-pt * 0.5 + fl * 0.3, lag=taillag, ph=tailph)
            self.ears(p, ears + 4 * osc(u, bobn, 0.1))
            self.jaw(p, jaw)
            if extra:
                extra(p, u)
            for k, lg in self.legs.items():
                Pg, sw, s = gait_foot(lg, u, S, duty, lift if (hlift is None or k[0] == 'f') else hlift)
                ln = pastern * 2.0 * (Pg[1] - lg.home[1]) / max(S, 1e-6) * (1 - sw)
                p.leg(lg, Pg, R3=rx(ln + tuck * sw), R4=rx(tuck * 0.6 * sw))
        c = self.C.clip(name, T, fn)
        self.C.meta['walk_speed' if name == 'Walk' else 'run_speed'] = round(v, 3)
        return c

    def idle(self, dur=3.0, breath=0.008, look=10.0, tail=6.0, shift=0.012, headlow=0.0, sniff=0.0, ear=14.0, extra=None, tail_lift=2.0):
        def fn(p, u):
            b = osc(u, 2)
            self.body(p, dz=breath * b - 0.004, dx=shift * osc(u, 1), pitch=0.6 * b, roll=0.8 * osc(u, 1))
            self.spine(p, flex=1.2 * b)
            self.neck(p, pitch=headlow + 2.5 * osc(u, 2, 0.1) + sniff * bump(u, 0.55, 0.65, 0.8, 0.9) * (1 + 0.3 * osc(u, 12)),
                      yaw=look * osc(u, 1, 0.0) * (0.6 + 0.4 * osc(u, 2, 0.2)))
            self.tailw(p, u, amp=tail, n=2, lift=tail_lift * b)
            self.ears(p, ear * bump(u, 0.3, 0.34, 0.38, 0.44), ear * bump(u, 0.62, 0.66, 0.7, 0.78))
            self.jaw(p, 3 + 3 * b)
            if extra:
                extra(p, u)
            self.plant(p)
        return self.C.clip('Idle', dur, fn)

    # ---- attacks
    def bite(self, name='Attack1', dur=0.8, hit=0.42, reach=0.2, gape=38.0, drop=0.03, down=14.0, extra=None):
        hu = hit / dur

        def fn(p, u):
            w = bump(u, 0.0, hu * 0.6, hu * 0.72, hu)
            s = bump(u, hu * 0.72, hu, hu + 0.14, 0.92)
            self.body(p, dy=0.07 * w - reach * 0.7 * s, dz=-0.035 * w - drop * s, pitch=-5 * w + 7 * s)
            self.spine(p, flex=6 * w - 5 * s)
            self.neck(p, pitch=-16 * w + down * s, head=0.35)
            self.jaw(p, gape * bump(u, hu * 0.25, hu * 0.7, hu - 0.02, hu + 0.03))
            self.tailw(p, u, amp=5, n=2, lift=10 * s)
            self.ears(p, -20 * s)
            if extra:
                extra(p, u)
            self.plant(p)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def leap(self, name='Attack2', dur=1.1, hit=0.6, fwd=0.45, up=0.22, gape=40.0, slam=False, crouch=0.1, extra=None):
        """crouch, spring forward off the hind legs, land on the fore paws biting (or slamming), hop back"""
        hu = hit / dur

        def fn(p, u):
            cr = bump(u, 0.0, hu * 0.5, hu * 0.6, hu * 0.72)
            fl = bump(u, hu * 0.6, hu * 0.85, hu + 0.02, hu + 0.3)          # in the air / stretched out
            ln = bump(u, hu - 0.03, hu, hu + 0.1, 0.95)                      # landing weight on the fore end
            arc = math.sin(math.pi * min(1.0, max(0.0, (u - hu * 0.6) / max(hu * 0.45, 1e-3)))) if u < hu + 0.02 else 0.0
            self.body(p, dy=0.06 * cr - fwd * fl, dz=-crouch * cr + up * arc - 0.05 * ln, pitch=-10 * cr - 12 * fl * (1 - ln) + 14 * ln)
            self.spine(p, flex=14 * cr - 10 * fl)
            self.neck(p, pitch=-10 * cr - 8 * fl + (26 if not slam else 10) * ln, head=0.35)
            self.jaw(p, gape * bump(u, hu * 0.5, hu * 0.85, hu - 0.01, hu + 0.04))
            self.tailw(p, u, amp=4, n=2, lift=18 * fl - 8 * cr)
            self.ears(p, -25 * fl)
            fa = bump(u, hu * 0.62, hu * 0.74, hu - 0.06, hu)                # fore paws leave the ground and reach
            ha = bump(u, hu * 0.74, hu * 0.9, hu + 0.05, hu + 0.25)
            if extra:
                extra(p, u)
            self.plant(p, air=dict(f=fa * 0.9, h=ha * 0.8), off=dict(f=(0, -fwd * 0.75 * fl, 0)),
                       tuck=dict(f=(-30, 20, 0), h=(25, -20, 0)))
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def hit(self, dur=0.4, back=0.06, turn=18.0, extra=None, drop=0.04, roll=7.0, pitch=-5.0):
        """drop / roll / pitch: flat sprawlers and long-tailed animals use small values (the belly and the tail must
        stay above the ground)"""
        def fn(p, u):
            k = bump(u, 0.0, 0.22, 0.3, 1.0)
            self.body(p, dy=back * k, dz=-drop * k, roll=roll * k, pitch=pitch * k, yaw=-4 * k)
            self.spine(p, flex=6 * k, side=-8 * k)
            self.neck(p, pitch=-12 * k, yaw=-turn * k, roll=10 * k)
            self.jaw(p, 22 * k)
            self.tailw(p, u, amp=4, n=1, lift=-14 * k)
            self.ears(p, -30 * k)
            if extra:
                extra(p, u)
            self.plant(p)
        return self.C.clip('Hit', dur, fn)

    def death_side(self, dur=1.6, side=1.0, kick=True, half_w=0.1, roll=86.0, extra=None, settle=(0.5, 0.62)):
        """buckles in front, drops on its side, kicks once and lies still"""
        zc = float(self.pivot[2])

        def make(fix):
            def fn(p, u):
                a = sm(u, 0.0, 0.3)             # fore end gives way
                f = sm(u, 0.18, 0.55)           # roll over / fall
                st = bump(u, 0.55, 0.62, 0.66, 0.76)
                kk = bump(u, 0.72, 0.78, 0.8, 0.9) if kick else 0.0
                self.body(p, dz=-(zc - half_w) * f - 0.1 * a * (1 - f) + 0.025 * st + fix * f, dy=0.04 * a, dx=side * 0.12 * f,
                          pitch=12 * a * (1 - f), roll=side * roll * f, yaw=-side * 8 * f)
                self.spine(p, flex=8 * a - 4 * f, side=side * 6 * f)
                self.neck(p, pitch=20 * a + 8 * f, yaw=side * 12 * f, roll=-side * 10 * f)
                self.jaw(p, 26 * sm(u, 0.1, 0.5))
                self.tailw(p, u, amp=3 * (1 - f), n=2, lift=-20 * f)
                self.ears(p, -35 * f)
                w = sm(u, 0.12, 0.5)
                if extra:
                    extra(p, u)
                self.plant(p, air=dict(f=sm(u, 0.05, 0.4), h=w),
                           tuck=dict(f_l=(-18 - 10 * kk, 30, 10), f_r=(-28, 38, 10), h_l=(14 + 26 * kk, -24 - 20 * kk, 0), h_r=(22, -30, 0)))
            return fn
        self.C.clip('Death', dur, make(0.0))
        z = self.C.end_minz('Death')
        return self.C.clip('Death', dur, make(-z + 0.004), floor='hip', settle=settle)

    def spawn_rise(self, dur=1.2, depth=None, shake=8.0, extra=None):
        """digs itself out of the ground: rises nose first, hauls out, shakes the ash off"""
        dep = (float(self.J['chestj'][2]) + 0.25) if depth is None else depth

        def fn(p, u):
            r = sm(u, 0.0, 0.6)
            sh = bump(u, 0.6, 0.68, 0.84, 0.95)
            self.body(p, dz=-dep * (1 - r) + 0.03 * bump(u, 0.5, 0.62, 0.66, 0.8), pitch=-28 * (1 - r), roll=shake * sh * osc(u, 9))
            self.spine(p, flex=-8 * (1 - r), side=shake * 0.6 * sh * osc(u, 9, 0.2))
            self.neck(p, pitch=-18 * (1 - r) + 6 * sh, yaw=shake * sh * osc(u, 9, 0.35))
            self.jaw(p, 14 * (1 - r))
            self.tailw(p, u, amp=14 * sh, n=6, lift=6 * (1 - r))
            self.ears(p, -20 * (1 - r))
            if extra:
                extra(p, u)
            self.plant(p, air=dict(f=1 - sm(u, 0.25, 0.5), h=1 - sm(u, 0.4, 0.66)), tuck=dict(f=(-35, 30, 0), h=(20, -20, 0)))
        return self.C.clip('Spawn', dur, fn, under_ok=True)


    # ---- more attacks (h2: boars, cats, pack animals)
    def gore(self, name='Attack1', dur=0.9, hit=0.45, reach=0.25, low=22.0, toss=30.0, gape=14.0, twist=0.0, extra=None):
        """head goes down and back, then rips forward and UP (tusks, horns). `low` is reduced automatically until the
        head stays above the ground"""
        hu = hit / dur

        def make(lw):
            def fn(p, u):
                w = bump(u, 0.0, hu * 0.55, hu * 0.72, hu)
                s = bump(u, hu * 0.72, hu, hu + 0.12, 0.95)
                k = lw / max(low, 1e-6)
                self.body(p, dy=0.08 * w - reach * s, dz=-0.05 * k * w + 0.02 * s, pitch=6 * k * w - 7 * s, roll=twist * 0.3 * s)
                self.spine(p, flex=5 * k * w - 6 * s)
                self.neck(p, pitch=lw * w - toss * s, roll=twist * s, head=0.45)
                self.jaw(p, gape * s)
                self.tailw(p, u, amp=6, n=3, lift=-12 * s)
                self.ears(p, -20 * w)
                if extra:
                    extra(p, u)
                self.plant(p)
            return fn
        fn = make(self.fit(make, low, [hu * 0.5, hu * 0.64, hu * 0.8]))
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def sweep(self, name='Attack1', dur=1.0, hit=0.5, arc=40.0, low=16.0, reach=0.15, gape=0.0, extra=None, drop=0.04):
        """head lowered, swung from its right to its left across the front (horn sweep): hits a cone"""
        hu = hit / dur

        def make(lw):
            def fn(p, u):
                w = bump(u, 0.0, hu * 0.55, hu * 0.7, hu)
                s = sm(u, hu * 0.7, hu + 0.1) * (1 - sm(u, hu + 0.25, 0.96))
                lo = max(w, min(1.0, s * 1.5))
                k = lw / max(low, 1e-6)
                yw = -arc * w + arc * s
                self.body(p, dy=0.05 * w - reach * s, dz=-drop * k * lo, yaw=yw * 0.22, roll=-yw * 0.08, pitch=3 * k * lo)
                self.spine(p, side=yw * 0.3, flex=4 * k * lo)
                self.neck(p, pitch=lw * lo, yaw=yw * 0.55, roll=-yw * 0.35, head=0.35)
                self.jaw(p, gape * s)
                self.tailw(p, u, amp=6, n=2, lift=-8 * s)
                self.ears(p, -20 * lo)
                if extra:
                    extra(p, u)
                self.plant(p)
            return fn
        fn = make(self.fit(make, low, [hu * 0.6, hu * 0.85, hu, hu + 0.1]))
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def charge(self, name='Attack2', dur=1.2, hit=0.72, fwd=0.55, low=20.0, toss=24.0, gape=0.0, paw='l', extra=None):
        """paws the ground with its head down, hurls itself forward and tosses the head on impact, shuffles back"""
        hu = hit / dur

        def make(lw):
            def fn(p, u):
                w = bump(u, 0.0, hu * 0.35, hu * 0.6, hu * 0.8)
                r = bump(u, hu * 0.6, hu, hu + 0.05, 0.96)
                t = bump(u, hu - 0.03, hu + 0.03, hu + 0.1, hu + 0.3)
                k = lw / max(low, 1e-6)
                self.body(p, dy=0.10 * w - fwd * r, dz=-0.06 * k * w - 0.03 * k * r + 0.03 * t, pitch=6 * k * w + 4 * k * r - 9 * t)
                self.spine(p, flex=7 * k * w - 5 * k * r)
                self.neck(p, pitch=lw * max(w, r) * (1 - t) - toss * t, head=0.4)
                self.jaw(p, gape * t)
                self.tailw(p, u, amp=6, n=4, lift=-14 * r)
                self.ears(p, -25 * max(w, r))
                st = bump(u, hu * 0.6, hu * 0.72, hu - 0.05, hu)
                back = bump(u, hu + 0.12, hu + 0.2, 0.86, 0.96)
                air = dict(f=max(st * 0.85, back * 0.35), h=max(bump(u, hu * 0.7, hu * 0.84, hu - 0.02, hu + 0.06) * 0.7, back * 0.3))
                if paw:
                    pw = bump(u, hu * 0.06, hu * 0.18, hu * 0.22, hu * 0.36) + bump(u, hu * 0.34, hu * 0.44, hu * 0.48, hu * 0.6)
                    air['f_' + paw] = max(air['f'], min(1.0, pw) * 0.8)
                if extra:
                    extra(p, u)
                self.plant(p, air=air, off=dict(f=(0, -fwd * 0.8 * r, 0), h=(0, -fwd * 0.5 * r, 0)), tuck=dict(f=(-25, -35, 40), h=(15, 30, -20)))
            return fn
        fn = make(self.fit(make, low, [hu * 0.4, hu * 0.6, hu * 0.8, hu - 0.02]))
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def swipe(self, name='Attack1', dur=0.7, hit=0.34, side='l', reach=0.3, up=0.28, gape=24.0, extra=None):
        """one fore paw is raised and raked forward and across; the body follows it"""
        hu = hit / dur
        lg = self.legs['f_' + side]
        sg = 1.0 if side == 'l' else -1.0

        def fn(p, u):
            w = bump(u, 0.0, hu * 0.6, hu * 0.78, hu)
            s = bump(u, hu * 0.75, hu, hu + 0.08, 0.92)
            self.body(p, dy=0.05 * w - reach * 0.45 * s, dz=0.015 * w - 0.04 * s, pitch=-7 * w + 6 * s, roll=-sg * 5 * w + sg * 4 * s,
                      yaw=sg * 6 * w - sg * 10 * s)
            self.spine(p, flex=-4 * w + 6 * s, side=sg * 5 * w - sg * 8 * s)
            self.neck(p, pitch=-6 * w + 10 * s, yaw=-sg * 6 * w + sg * 8 * s)
            self.jaw(p, gape * bump(u, hu * 0.3, hu * 0.8, hu, hu + 0.2))
            self.tailw(p, u, amp=8, n=2, lift=10 * w)
            self.ears(p, -25 * max(w, s))
            if extra:
                extra(p, u)
            self.plant(p)
            st = bump(u, hu * 0.78, hu, hu + 0.05, hu + 0.3)
            P = lg.home + V(sg * 0.10 * w - sg * 0.13 * st, 0.07 * w - reach * st, up * w + 0.10 * st + 0.05 * bump(u, hu + 0.05, hu + 0.2, hu + 0.25, 0.9))
            p.leg(lg, P, R3=rx(-45 * w + 25 * st), R4=rx(-35 * w + 30 * st))
        c = self.C.clip(name, dur, fn, hit=hit)
        self.C.reach(name, lg.b[-1], lg.c)
        return c

    def rear(self, name='Attack2', dur=1.2, hit=0.66, pitch=26.0, fwd=0.2, strike=None, slam=0.05, gape=0.0, headup=14.0, extra=None, dip=4.0):
        """rears up on the hind legs and comes down on both fore feet (stomp); strike = 'l' / 'r': that fore leg
        lashes out forward at the hit instead (a horse's strike)"""
        hu = hit / dur
        pv = V(self.J['pelvis'])

        def fn(p, u):
            if strike is None:
                up = bump(u, 0.04, hu * 0.55, hu * 0.82, hu)
                dn = bump(u, hu - 0.04, hu, hu + 0.1, 0.95)
                fa = bump(u, 0.04, hu * 0.35, hu - 0.07, hu)
            else:
                up = bump(u, 0.04, hu * 0.6, hu + 0.08, hu + 0.32)
                dn = 0.0
                fa = bump(u, 0.04, hu * 0.4, hu + 0.14, hu + 0.34)
            k = bump(u, hu * 0.72, hu, hu + 0.04, hu + 0.2)
            self.body(p, pitch=-pitch * up + dip * dn, dz=-slam * dn - 0.02 * up, dy=0.04 * up - fwd * 0.5 * dn - (fwd * 0.4 * k if strike else 0.0), pivot=pv)
            self.spine(p, flex=-7 * up + 5 * dn)
            self.neck(p, pitch=headup * up - 6 * dn)
            self.jaw(p, gape * up)
            self.tailw(p, u, amp=5, n=2, lift=-10 * up)
            self.ears(p, -20 * up)
            tk = dict(f=(20, -75, 30), h=(0, 0, 0))
            off = None
            if strike:
                tk['f_' + strike] = (20 - 80 * k, -75 + 72 * k, 30 - 30 * k)
            else:
                off = dict(f=(0, -fwd * sm(u, hu * 0.5, hu) * (1 - sm(u, hu + 0.25, 0.97)), 0))
            if extra:
                extra(p, u)
            self.plant(p, air=dict(f=fa), tuck=tk, off=off)
        c = self.C.clip(name, dur, fn, hit=hit)
        lg = self.legs['f_' + (strike or 'l')]
        self.C.reach(name, lg.b[-1], lg.c)
        return c

    # ---- lying down: deaths on the belly, getting up
    def death_belly(self, dur=1.7, drop=None, roll=8.0, side=1.0, fold=None, head=None, head_max=60.0, hyaw=16.0, jaw=20.0, throe=0.0,
                    settle=(0.46, 0.6), tail_lift=None, tail_side=0.0, extra=None, planted=False, splay=0.05):
        """legs give way in front, then behind; drops on its belly with the legs folded under / beside it and the head
        on the ground (for animals that carry something on their backs, and for anything too flat to fall over).
        planted=True (sprawlers): the feet stay on the ground and slide outwards by `splay` while the belly sinks.
        The drop and the head angle are solved on the finished mesh: add the geometry before calling this.
        throe = degrees the fore end rears up first"""
        D0 = self.leg_h * 0.8 if drop is None else drop
        fold = self.FOLD if fold is None else fold
        pv = V(self.J['pelvis'])

        def make(fix, hp, tlift=0.0):
            def fn(p, u):
                a = sm(u, 0.0, 0.32)
                b = sm(u, 0.14, 0.5)
                f = sm(u, 0.08, 0.5)
                st = bump(u, 0.5, 0.56, 0.6, 0.72)
                th = throe * bump(u, 0.0, 0.14, 0.2, 0.36)
                self.body(p, dz=(-D0 + fix) * f + 0.02 * st, dy=-0.04 * a, pitch=9 * a * (1 - b) - th, roll=side * roll * sm(u, 0.3, 0.62), yaw=-side * 4 * f)
                self.spine(p, flex=6 * a - 3 * f - th * 0.4, side=side * 5 * f)
                self.neck(p, pitch=hp * sm(u, 0.18, 0.58) - th * 0.8, yaw=side * hyaw * sm(u, 0.3, 0.7), roll=-side * 12 * sm(u, 0.3, 0.7))
                self.jaw(p, jaw * sm(u, 0.05, 0.5))
                self.tailw(p, u, amp=3 * (1 - f) + 5 * bump(u, 0.3, 0.4, 0.5, 0.75), n=3, lift=0.0)
                if self.tail:
                    p.rot(self.tail[0], x=tlift * f, z=tail_side * side * f)
                    for b in self.tail[1:]:
                        p.rot(b, z=tail_side * side * f * 0.6)
                self.ears(p, -30 * f)
                if extra:
                    extra(p, u)
                if planted:
                    k = splay * sm(u, 0.05, 0.5)
                    self.plant(p, off=dict(f_l=(k, -0.3 * k, 0), f_r=(-k, -0.3 * k, 0), h_l=(k, 0.3 * k, 0), h_r=(-k, 0.3 * k, 0)))
                else:
                    self.plant(p, air=dict(f=sm(u, 0.02, 0.36), h=sm(u, 0.14, 0.5)), tuck=fold)
            return fn
        h0 = 20.0 if head is None else head
        fix = -self.minz(make(0.0, h0), 1.0, 'core' if planted else 'trunk') + 0.004
        tl0 = 0.0 if tail_lift is None else tail_lift
        if tail_lift is None and self.tail:
            tl0 = self.fit(lambda x: make(fix, h0, x), -30.0, [1.0], lo=60.0, part='tail', zmin=0.003)
        if head is None:
            lo, hi = -25.0, head_max
            if self.minz(make(fix, hi, tl0), 1.0, 'head') > 0.006:
                lo = hi
            else:
                for _ in range(14):
                    mid = 0.5 * (lo + hi)
                    if self.minz(make(fix, mid, tl0), 1.0, 'head') > 0.006:
                        lo = mid
                    else:
                        hi = mid
            h0 = lo
        return self.C.clip('Death', dur, make(fix, h0, tl0), floor='hip', settle=settle)

    def spawn_stand(self, dur=1.4, drop=None, fold=None, shake=5.0, hold=0.12, headrest=0.0, extra=None):
        """lies on its belly with folded legs (asleep / as carved), props itself up in front, then behind, and
        shakes. Frame 0 rests exactly on the ground: add the geometry before calling this"""
        D0 = self.leg_h * 0.8 if drop is None else drop
        fold = self.FOLD if fold is None else fold
        pv = V(self.J['pelvis'])
        Lb = max(0.05, float(abs(self.J['chestj'][1] - self.J['pelvis'][1])))

        def make(fix):
            D = D0 - fix

            def fn(p, u):
                a = sm(u, hold, 0.5)
                b = sm(u, 0.32, 0.72)
                sh = bump(u, 0.72, 0.78, 0.88, 0.97)
                pt = math.degrees(math.asin(min(0.8, max(0.0, D * (a - b) / Lb))))
                self.body(p, dz=-D * (1 - b), pitch=-pt, roll=shake * sh * osc(u, 8), pivot=pv)
                self.spine(p, flex=-4 * a * (1 - b), side=shake * 0.5 * sh * osc(u, 8, 0.2))
                self.neck(p, pitch=headrest * (1 - a) + pt * 0.7 + 4 * sh, yaw=shake * sh * osc(u, 8, 0.35))
                self.tailw(p, u, amp=10 * sh, n=6, lift=-8 * (1 - b))
                self.ears(p, -20 * (1 - a))
                if extra:
                    extra(p, u)
                self.plant(p, air=dict(f=1 - sm(u, hold, 0.42), h=1 - sm(u, 0.32, 0.64)), tuck=fold)
            return fn
        fix = -self.minz(make(0.0), 0.0, 'body') + 0.003
        return self.C.clip('Spawn', dur, make(fix), under_ok=True)


# ================================================================================================ flesh
LEGN = ('upper', 'lower', 'foot', 'toe')


def quad_flesh(C, J, S):
    """organic body of an upright quadruped from a shape table S:
      torso / neck = [(y, z centre, vertical radius, side radius), ...] from the rump to the breast / chest to head
      head = [closed Parts] (skull, snout ... already in place; they move with the head bone)
      fleg / hleg = [(fore-aft radius, side radius)] at [root inside the body, top joint, elbow|knee, wrist|hock, toe joint]
      fpaw / hpaw = (half width, half length, half height);  blobs = [(centre, (rx, ry, rz), [bones], mirror)]
      voxel, tris, sculpt(part); optional: torso_power / neck_power (superellipse exponent of the cross-section:
      4 = boxy), torso_n, f_root / h_root (vector from the top joint to the limb root inside the body: sprawlers
      use (-x, 0, 0); default (0, 0, f_up / h_up)), leg_m, extra = [more skinned closed Parts to fuse in]"""
    parts = []
    t = S['torso']
    torso = P.tubep([(0, a[0], a[1]) for a in t], [a[2] for a in t], [a[3] for a in t], m=S.get('torso_m', 16), n=S.get('torso_n', 16),
                    power=S.get('torso_power', 2.0))
    B.wbones(torso, [b for b in ('hip', 'spine1', 'spine2', 'chest') if b in C.rg.idx], sharp=3.0)
    parts.append(torso)
    t = S.get('neck')
    if t:
        neck = P.tubep([(0, a[0], a[1]) for a in t], [a[2] for a in t], [a[3] for a in t], m=9, n=12, power=S.get('neck_power', 2.0))
        B.wbones(neck, ['chest', 'neck1', 'neck2', 'head'], sharp=3.0)
        parts.append(neck)
    for hp in S.get('head', []):
        B.wbones(hp, ['head'])
        parts.append(hp)
    for pre, key, par in (('f', 'fleg', 'chest'), ('h', 'hleg', 'hip')):
        rad = S[key]
        pw = S[pre + 'paw']
        for s, sg in (('l', 1.0), ('r', -1.0)):
            ro = S.get(pre + '_root')
            pts = [J[pre][0] + (V(0, 0, S.get(pre + '_up', 0.07)) if ro is None else V(ro))] + list(J[pre][:4])
            pts = [q * (sg, 1, 1) for q in pts]
            leg = P.tubep(pts, [r[0] for r in rad], [r[1] for r in rad], m=S.get('leg_m', 14), n=10, up=(0, -1, 0), power=S.get('leg_power', 2.0))
            bones = [par] + ['%s_%s_%s' % (pre, n, s) for n in LEGN]
            B.wbones(leg, bones, sharp=4.0)
            parts.append(leg)
            c = J[pre][3] * (sg, 1, 1) + V(0, -pw[1] * 0.5, 0)
            c[2] = pw[2] - 0.004
            paw = P.blob(c, pw[0], pw[1], pw[2], n=10, m=6)
            B.wbones(paw, bones[3:], bias={bones[4]: 2.5})
            parts.append(paw)
    for c, r, bones, mirror in S.get('blobs', []):
        for sg in ((1.0, -1.0) if mirror else (1.0,)):
            bl = P.blob(V(c) * (sg, 1, 1), r[0], r[1], r[2])
            bb = [(b[:-1] + ('l' if sg > 0 else 'r')) if (mirror and b.endswith('_l')) else b for b in bones]
            B.wbones(bl, bb, sharp=3.0)
            parts.append(bl)
    parts += list(S.get('extra', []))
    body = P.organic(parts, voxel=S.get('voxel', 0.011), tris=S.get('tris', 6500), sculpt=S.get('sculpt'), smooth=S.get('smooth', 8),
                     inflate=S.get('inflate'))
    return body


def add_tail(C, J, r0, r1, look, n=7, sink=0.05, rh=None):
    pts = [J['tail'][0] + unit(J['tail'][0] - J['tail'][1]) * sink] + list(J['tail'])
    k = len(pts)
    rr = [r0] + list(np.linspace(r0, r1, k - 1))
    tl = P.tubep(pts, rr, None if rh is None else [x * rh for x in rr], m=max(8, 3 * k), n=n)
    B.wbones(tl, ['hip'] + [b for b in C.rg.names if b.startswith('tail')], sharp=4.0)
    return C.skin(tl, look)


def add_claws(C, bvh, J, look, n=3, length=0.03, r=0.007, spread=0.016, drop=-0.3, sides=5):
    for pre in ('f', 'h'):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            tip = J[pre][4] * (sg, 1, 1)
            out = []
            for k in range(n):
                dx = spread * (k - (n - 1) / 2)
                base = tip + V(dx, 0.012, 0.004)
                out.append(P.seated(bvh, base, (dx * 6, -1.0, drop), length, r, sink=0.4, n=sides, curl=(0, 0, -1), seg=2))
            C.rigid(M.join(out), '%s_toe_%s' % (pre, s), look, smooth=50)


def ear_sheet(base, tip, width, cup=0.02, th=0.005, rows=6, cols=5, sink=0.02, shape=0.7, face=(0, -1, 0)):
    """leaf-shaped ear from base to tip; `face` = the direction its hollow looks"""
    base = V(base); tip = V(tip)
    up = unit(tip - base)
    Ln = np.linalg.norm(tip - base)
    f = V(face) - up * float(V(face) @ up)
    f = unit(f)
    side = np.cross(up, f)
    G = np.zeros((rows + 1, cols, 3))
    for i in range(rows + 1):
        t = i / rows
        h = -sink + (Ln + sink) * t
        w = width * max(0.04, math.sin(math.pi * (0.12 + 0.88 * t)) ** shape) * (1.0 if t < 0.95 else 0.3)
        for j in range(cols):
            sx = -1 + 2 * j / (cols - 1)
            G[i, j] = base + up * h + side * (sx * w) + f * (cup * (sx * sx - 0.5) * math.sin(math.pi * min(1.0, t + 0.15)))
    return P.sheet_solid(G, th)
