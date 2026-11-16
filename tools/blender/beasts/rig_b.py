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
           fleg=(0.80, 0.56, 0.22, 0.055), hleg=(0.84, 0.55, 0.30, 0.055), tail=(0.44, 4, -0.75), sprawl=0.0):
    """joint table of an upright quadruped. L = pelvis..chest joint, hs / hh = spine height at shoulders / pelvis,
    neck = (length, rise), fleg / hleg = heights of (top joint, elbow|knee, wrist|hock, toe joint) as fractions of
    hs / hh, tail = (length, bones, slope dz/dy)."""
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


def quad_skel(J, ears=True, jaw=True, extra=None):
    sk = B.Sk()
    sk.bone('hip', J['pelvis'], J['pelvis'] + V(0, 0.08, 0), 'root')
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
    def __init__(self, C, J, tailn=None):
        self.C = C
        self.J = J
        rg = C.rg
        self.legs = {}
        for pre, key in (('f', 'f_c'), ('h', 'h_c')):
            for s, sg in (('l', 1.0), ('r', -1.0)):
                b = ['%s_%s_%s' % (pre, n, s) for n in ('upper', 'lower', 'foot', 'toe')]
                lg = Leg(b, J[key] * (sg, 1, 1), name='%s_%s' % (pre, s))
                lg.par = 'chest' if pre == 'f' else 'hip'
                lg.glide = 0.07 if pre == 'f' else 0.04
                self.legs[lg.name] = lg
        C.legs = list(self.legs.values())
        self.tail = [n for n in rg.names if n.startswith('tail')]
        self.pivot = V(0, 0, (J['pelvis'][2] + J['chestj'][2]) * 0.5 - 0.08)
        self.has_jaw = 'jaw' in rg.idx
        self.has_ears = 'ear_l' in rg.idx
        self.snout = J['snout'].copy()
        self.leg_h = float(J['f'][0][2])

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
        for b, k in (('spine1', 0.35), ('spine2', 0.35), ('chest', 0.3)):
            p.rot(b, x=flex * k, z=side * k, y=twist * k)

    def neck(self, p, pitch=0.0, yaw=0.0, roll=0.0, head=0.4):
        """pitch > 0 lowers the head; `head` = share taken by the head joint"""
        k = (1.0 - head) / 2
        p.rot('neck1', x=pitch * k, z=yaw * k)
        p.rot('neck2', x=pitch * k, z=yaw * k)
        p.rot('head', x=pitch * head, z=yaw * head, y=roll)

    def jaw(self, p, deg):
        if self.has_jaw and deg:
            p.rot('jaw', x=deg)

    def tailw(self, p, u, amp=10.0, n=1.0, lag=0.12, lift=0.0, ph=0.0, curl=0.0):
        for i, b in enumerate(self.tail):
            p.rot(b, z=amp * osc(u, n, ph - i * lag), x=-lift + curl * i)

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
             ears=0.0, jaw=0.0, pastern=28.0):
        S = v * duty * T
        for k, lg in self.legs.items():
            lg.phase = phases[k]

        def fn(p, u):
            pt = pitch * math.cos(2 * math.pi * (u - pitchph))
            fl = flex * math.cos(2 * math.pi * (u - flexph))
            self.body(p, dz=-crouch + bob * math.cos(2 * math.pi * (bobn * u - bobph)), pitch=lean + pt - fl * 0.35,
                      roll=roll * osc(u, 1, 0.0), yaw=yaw * osc(u, 1, 0.25))
            self.spine(p, flex=fl, side=-yaw * 1.2 * osc(u, 1, 0.25))
            self.neck(p, pitch=headlow - (lean + pt) * 0.8 + nod * math.cos(2 * math.pi * (bobn * u - bobph - 0.15)), yaw=-yaw * 0.8 * osc(u, 1, 0.25))
            self.tailw(p, u, amp=tail, n=1, lift=-pt * 0.5 + fl * 0.3)
            self.ears(p, ears + 4 * osc(u, bobn, 0.1))
            self.jaw(p, jaw)
            for k, lg in self.legs.items():
                Pg, sw, s = gait_foot(lg, u, S, duty, lift if (hlift is None or k[0] == 'f') else hlift)
                ln = pastern * 2.0 * (Pg[1] - lg.home[1]) / max(S, 1e-6) * (1 - sw)
                p.leg(lg, Pg, R3=rx(ln + tuck * sw), R4=rx(tuck * 0.6 * sw))
        c = self.C.clip(name, T, fn)
        self.C.meta['walk_speed' if name == 'Walk' else 'run_speed'] = round(v, 3)
        return c

    def idle(self, dur=3.0, breath=0.008, look=10.0, tail=6.0, shift=0.012, headlow=0.0, sniff=0.0, ear=14.0):
        def fn(p, u):
            b = osc(u, 2)
            self.body(p, dz=breath * b - 0.004, dx=shift * osc(u, 1), pitch=0.6 * b, roll=0.8 * osc(u, 1))
            self.spine(p, flex=1.2 * b)
            self.neck(p, pitch=headlow + 2.5 * osc(u, 2, 0.1) + sniff * bump(u, 0.55, 0.65, 0.8, 0.9) * (1 + 0.3 * osc(u, 12)),
                      yaw=look * osc(u, 1, 0.0) * (0.6 + 0.4 * osc(u, 2, 0.2)))
            self.tailw(p, u, amp=tail, n=2, lift=2 * b)
            self.ears(p, ear * bump(u, 0.3, 0.34, 0.38, 0.44), ear * bump(u, 0.62, 0.66, 0.7, 0.78))
            self.jaw(p, 3 + 3 * b)
            self.plant(p)
        return self.C.clip('Idle', dur, fn)

    # ---- attacks
    def bite(self, name='Attack1', dur=0.8, hit=0.42, reach=0.2, gape=38.0, drop=0.03, down=14.0):
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
            self.plant(p)
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def leap(self, name='Attack2', dur=1.1, hit=0.6, fwd=0.45, up=0.22, gape=40.0, slam=False, crouch=0.1):
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
            self.plant(p, air=dict(f=fa * 0.9, h=ha * 0.8), off=dict(f=(0, -fwd * 0.75 * fl, 0)),
                       tuck=dict(f=(-30, 20, 0), h=(25, -20, 0)))
        c = self.C.clip(name, dur, fn, hit=hit)
        self.reach(name)
        return c

    def hit(self, dur=0.4, back=0.06, turn=18.0):
        def fn(p, u):
            k = bump(u, 0.0, 0.22, 0.3, 1.0)
            self.body(p, dy=back * k, dz=-0.04 * k, roll=7 * k, pitch=-5 * k, yaw=-4 * k)
            self.spine(p, flex=6 * k, side=-8 * k)
            self.neck(p, pitch=-12 * k, yaw=-turn * k, roll=10 * k)
            self.jaw(p, 22 * k)
            self.tailw(p, u, amp=4, n=1, lift=-14 * k)
            self.ears(p, -30 * k)
            self.plant(p)
        return self.C.clip('Hit', dur, fn)

    def death_side(self, dur=1.6, side=1.0, kick=True, half_w=0.1, roll=86.0):
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
                self.plant(p, air=dict(f=sm(u, 0.05, 0.4), h=w),
                           tuck=dict(f_l=(-18 - 10 * kk, 30, 10), f_r=(-28, 38, 10), h_l=(14 + 26 * kk, -24 - 20 * kk, 0), h_r=(22, -30, 0)))
            return fn
        self.C.clip('Death', dur, make(0.0))
        z = self.C.end_minz('Death')
        return self.C.clip('Death', dur, make(-z + 0.004))

    def spawn_rise(self, dur=1.2, depth=None, shake=8.0):
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
            self.plant(p, air=dict(f=1 - sm(u, 0.25, 0.5), h=1 - sm(u, 0.4, 0.66)), tuck=dict(f=(-35, 30, 0), h=(20, -20, 0)))
        return self.C.clip('Spawn', dur, fn, under_ok=True)


# ================================================================================================ flesh
LEGN = ('upper', 'lower', 'foot', 'toe')


def quad_flesh(C, J, S):
    """organic body of an upright quadruped from a shape table S:
      torso / neck = [(y, z centre, vertical radius, side radius), ...] from the rump to the breast / chest to head
      head = [closed Parts] (skull, snout ... already in place; they move with the head bone)
      fleg / hleg = [(fore-aft radius, side radius)] at [root inside the body, top joint, elbow|knee, wrist|hock, toe joint]
      fpaw / hpaw = (half width, half length, half height);  blobs = [(centre, (rx, ry, rz), [bones], mirror)]
      voxel, tris, sculpt(part)"""
    parts = []
    t = S['torso']
    torso = P.tubep([(0, a[0], a[1]) for a in t], [a[2] for a in t], [a[3] for a in t], m=S.get('torso_m', 16), n=16)
    B.wbones(torso, ['hip', 'spine1', 'spine2', 'chest'], sharp=3.0)
    parts.append(torso)
    t = S.get('neck')
    if t:
        neck = P.tubep([(0, a[0], a[1]) for a in t], [a[2] for a in t], [a[3] for a in t], m=9, n=12)
        B.wbones(neck, ['chest', 'neck1', 'neck2', 'head'], sharp=3.0)
        parts.append(neck)
    for hp in S.get('head', []):
        B.wbones(hp, ['head'])
        parts.append(hp)
    for pre, key, par in (('f', 'fleg', 'chest'), ('h', 'hleg', 'hip')):
        rad = S[key]
        pw = S[pre + 'paw']
        for s, sg in (('l', 1.0), ('r', -1.0)):
            pts = [J[pre][0] + V(0, 0, S.get(pre + '_up', 0.07))] + list(J[pre][:4])
            pts = [q * (sg, 1, 1) for q in pts]
            leg = P.tubep(pts, [r[0] for r in rad], [r[1] for r in rad], m=14, n=10, up=(0, -1, 0))
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
    body = P.organic(parts, voxel=S.get('voxel', 0.011), tris=S.get('tris', 6500), sculpt=S.get('sculpt'), smooth=S.get('smooth', 3))
    return body


def add_tail(C, J, r0, r1, look, n=7, sink=0.05, rh=None):
    pts = [J['tail'][0] + unit(J['tail'][0] - J['tail'][1]) * sink] + list(J['tail'])
    k = len(pts)
    rr = [r0] + list(np.linspace(r0, r1, k - 1))
    tl = P.tubep(pts, rr, None if rh is None else [x * rh for x in rr], m=max(8, 3 * k), n=n)
    B.wbones(tl, ['hip'] + [b for b in C.rg.names if b.startswith('tail')], sharp=4.0)
    return C.skin(tl, look)


def add_claws(C, bvh, J, look, n=3, length=0.03, r=0.007, spread=0.016, drop=-0.3):
    for pre in ('f', 'h'):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            tip = J[pre][4] * (sg, 1, 1)
            out = []
            for k in range(n):
                dx = spread * (k - (n - 1) / 2)
                base = tip + V(dx, 0.012, 0.004)
                out.append(P.seated(bvh, base, (dx * 6, -1.0, drop), length, r, sink=0.4, n=5, curl=(0, 0, -1), seg=2))
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
