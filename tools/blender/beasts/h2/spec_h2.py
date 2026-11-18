"""Rig B creatures built by helper H2 (quadrupeds): hound variants, the boar group, the sprawlers.
One function per bestiary type id, registered in BUILD.  Build:
  bb.ps1 bbuild.py "<ids> --path=Y:\\tools\\blender\\beasts\\h2 --spec=spec_h2 --out=Y:\\tools\\blender\\beasts\\h2\\out [--nobake]"
Shared helpers of this module are in h2lib.py (same folder)."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis
import bparts as P
import rig_b as RB
import h2lib as H
from h2lib import std_meta, surf_top, surf_bottom, seat_rock, bar, hoop, flat_horn, DragChain

BUILD = {}


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


# ================================================================================================ dusk_boar
@reg
def dusk_boar(bake=True):
    """Ploughtusk: low wedge of a boar, head a third of its length, two flat ploughshare tusks, clinker hump,
    tiny hindquarters; ember glow between the jaws"""
    J = RB.quad_J(L=0.62, hs=0.60, hh=0.46, wf=0.125, wh=0.095, neck=(0.14, -0.04), head=0.42, fleg=(0.72, 0.50, 0.20, 0.06),
                  hleg=(0.80, 0.52, 0.28, 0.065), tail=(0.20, 2, -1.0))
    J['snout'] = V(0, -0.90, 0.335)
    J['jawh'] = V(0, -0.49, 0.37)
    J['chin'] = V(0, -0.88, 0.225)
    J['ear'] = (V(0.095, -0.50, 0.62), V(0.175, -0.44, 0.74))
    C = B.Beast('mon_dusk_boar', RB.quad_skel(J))
    std_meta(C, 'dusk', 1.2, 3.8)
    Q = RB.Quad(C, J, glide=(0.08, 0.06))
    C.look('hide', 'skin', 'skin_hide', color=(0.36, 0.335, 0.31), blotch=(0.21, 0.18, 0.17), vein=(0.15, 0.11, 0.12), rough=0.6,
           sinew=0.8, scar=1.4, dry=0.5, feet_dirt=0.9, feet_hi=0.32, sore_amt=0.25, wrinkle=1.6, vein_amt=0.5)
    C.look('bristle', 'bfur', 'skin_hide', color=(0.06, 0.055, 0.05), tip=(0.19, 0.18, 0.17), ash=0.9, flow=(0, 0.8, 0.5), scale=70.0)
    C.look('clinker', 'rock', 'skin_hide', color=(0.05, 0.045, 0.042), ash=(0.22, 0.21, 0.2), scale=22.0, ash_amt=0.8,
           heat=(1.0, 0.3, 0.05), heat_amt=0.3)
    C.look('tusk', 'bone', 'bone_tusk', color=(0.58, 0.53, 0.42), stain=(0.25, 0.17, 0.09), dirt_lo=-2.0, dirt_hi=-1.5, crack_amt=0.8)
    C.look('hoof', 'bone', 'bone_tusk', color=(0.07, 0.06, 0.055), stain=(0.03, 0.025, 0.02), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('ember', 'emis', 'emis_mouth', emit=(1.0, 0.33, 0.05), strength=1.7, mottle=0.7, base_mul=0.5, scale=40.0)

    def sculpt(body):
        body.dent((0.115, -0.60, 0.50), 0.05, -0.012)        # hollows where the eyes would be
        body.dent((-0.115, -0.60, 0.50), 0.05, -0.012)
        body.dent((0.0, -0.74, 0.50), 0.07, -0.01)           # dished bridge of the snout
        body.dent((0.14, 0.12, 0.42), 0.09, -0.012)          # hollow flank in front of the small haunch
        body.dent((-0.14, 0.12, 0.42), 0.09, -0.012)

    S = dict(
        torso=[(0.44, 0.38, 0.06, 0.06), (0.32, 0.365, 0.135, 0.115), (0.12, 0.395, 0.185, 0.16), (-0.10, 0.455, 0.265, 0.21),
               (-0.28, 0.52, 0.33, 0.245), (-0.40, 0.52, 0.29, 0.22), (-0.48, 0.51, 0.21, 0.18)],
        neck=[(-0.34, 0.55, 0.28, 0.21), (-0.44, 0.535, 0.23, 0.185), (-0.52, 0.51, 0.20, 0.165)],
        head=[P.tubep([(0, -0.42, 0.55), (0, -0.56, 0.50), (0, -0.72, 0.42), (0, -0.86, 0.355), (0, -0.915, 0.335)],
                      [0.17, 0.16, 0.115, 0.075, 0.062], [0.15, 0.14, 0.10, 0.07, 0.065], m=10, n=12),
              P.blob((0, -0.922, 0.335), 0.075, 0.024, 0.068),                                   # the rooting disc
              P.blob((0.105, -0.56, 0.44), 0.055, 0.10, 0.10), P.blob((-0.105, -0.56, 0.44), 0.055, 0.10, 0.10)],
        fleg=[(0.12, 0.085), (0.12, 0.09), (0.085, 0.07), (0.055, 0.05), (0.05, 0.05)],
        hleg=[(0.10, 0.07), (0.10, 0.075), (0.07, 0.058), (0.045, 0.04), (0.042, 0.042)],
        fpaw=(0.048, 0.06, 0.035), hpaw=(0.042, 0.052, 0.032),
        blobs=[((0.0, -0.24, 0.80), (0.13, 0.20, 0.12), ['chest', 'spine2'], False),
               ((0.085, 0.30, 0.36), (0.06, 0.10, 0.11), ['hip', 'h_upper_l'], True),
               ((0.13, -0.30, 0.42), (0.07, 0.11, 0.14), ['chest', 'f_upper_l'], True)],
        voxel=0.014, tris=5600, sculpt=sculpt)
    body = RB.quad_flesh(C, J, S)
    C.skin(body, 'hide')
    bvh = P.bvh_of(body)
    RB.add_tail(C, J, 0.02, 0.008, 'hide', n=6)
    C.rigid(P.blob(J['tail'][-1] + V(0, 0.01, -0.02), 0.022, 0.03, 0.045, n=6, m=4), 'tail2', 'bristle', smooth=180)
    # lower jaw fitted under the skull (2 cm gap in front), a slab of ember light inside the gap, the ploughshare tusks
    zb = lambda y: surf_bottom(bvh, 0.0, y, default=0.30)
    jy = [-0.62, -0.78, -0.885]
    jr = [0.045, 0.038, 0.03]
    jz = [zb(y) - 0.016 - r for y, r in zip(jy, jr)]
    jaw = P.tubep([(0, -0.47, 0.375)] + [(0, y, z) for y, z in zip(jy, jz)], [0.06] + jr, [0.115, 0.10, 0.078, 0.058], m=9, n=10)
    C.rigid(jaw, 'jaw', 'hide', smooth=180)
    gy = [-0.60, -0.72, -0.86]
    glow = P.tubep([(0, y, zb(y) - 0.006) for y in gy], [0.024, 0.024, 0.02], [0.046, 0.036, 0.024], m=7, n=8)
    C.rigid(glow, 'head', 'ember', smooth=180)
    tz = jz[1] - 0.237
    for sg in (1, -1):
        tk = flat_horn([(sg * 0.055, -0.70, 0.25 + tz), (sg * 0.105, -0.80, 0.25 + tz), (sg * 0.15, -0.90, 0.27 + tz),
                        (sg * 0.185, -0.985, 0.32 + tz), (sg * 0.20, -1.04, 0.39 + tz)], 0.072, 0.018, m=9, n=8, power=0.55,
                       up=(sg * 0.8, 0, 0.6))
        C.rigid(tk, 'jaw', 'tusk', smooth=45)
        up = P.seated(bvh, (sg * 0.07, -0.86, 0.33), (sg * 0.9, -0.3, 0.5), 0.07, 0.016, sink=0.4, n=5, curl=(0, 0, 1), seg=3)
        C.rigid(up, 'head', 'tusk', smooth=50)
        e = RB.ear_sheet(J['ear'][0] * (sg, 1, 1), J['ear'][1] * (sg, 1, 1), 0.045, cup=0.02, th=0.008, sink=0.035, rows=4, cols=4,
                         face=(sg * 0.7, -0.7, 0))
        C.rigid(e, 'ear_l' if sg > 0 else 'ear_r', 'hide', smooth=180)
    H.hooves(C, J, 'hoof', rf=(0.052, 0.064), rh=(0.046, 0.056), h=0.055)
    # the hump: bristle with clinker fused into it
    def hump(c, n):
        return (n[:, 2] > 0.3) & (np.abs(c[:, 0]) < 0.14) & (c[:, 1] > -0.50) & (c[:, 1] < 0.12) & (c[:, 2] > 0.62 - 0.25 * np.clip(c[:, 1], 0, 1))
    tf = P.tufts(body, 150, length=(0.07, 0.15), radius=(0.014, 0.026), seed=4, mask=hump, flow=(0, 1, 0.9), lay=0.4, sink=0.3,
                 sides=3, jitter=0.35)
    C.skin(tf, 'bristle', smooth=180)
    rng = np.random.default_rng(11)
    for i in range(9):
        x = rng.uniform(-0.10, 0.10); y = rng.uniform(-0.40, -0.06)
        s = rng.uniform(0.04, 0.07)
        seat_rock(C, bvh, (x, y), (s * 1.2, s * 1.5, s), 'clinker', 'chest' if y < -0.12 else 'spine2', seed=20 + i, sink=0.45)

    Q.idle(dur=3.2, look=6.0, tail=12.0, sniff=6.0, ear=10.0)
    Q.gait('Walk', 0.85, 0.567, 0.62, RB.WALK4, lift=0.05, bob=0.008, crouch=0.035, nod=3.0, headlow=2.0, tail=10.0, tuck=30.0, roll=1.8, yaw=2.0)
    Q.gait('Run', 3.8 / 1.2, 0.4, 0.34, RB.TROT, lift=0.09, bob=0.02, bobn=2, bobph=0.85, crouch=0.04, pitch=1.5, nod=4.0, tail=6.0,
           tuck=55.0, headlow=3.0, roll=1.5, yaw=1.2, ears=-15.0, jaw=6.0)
    Q.gore(dur=0.9, hit=0.45, reach=0.13, low=14.0, toss=34.0, gape=16.0)
    Q.charge(dur=1.3, hit=0.8, fwd=0.6, low=12.0, toss=28.0, gape=10.0)
    Q.hit()
    Q.death_side(dur=1.7, half_w=0.2)
    Q.spawn_rise(dur=1.3)
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2")


# ================================================================================================ dusk_lizard
@reg
def dusk_lizard(bake=True):
    """Brass-basker: flat sprawling monitor, notched sail of verdigris plates, glowing throat sack, long tail"""
    J = RB.sprawl_J(L=0.56, h=0.14, wf=0.115, wh=0.095, rf=0.21, rh=0.23, neck=(0.17, 0.015), head=0.27, tail=(0.98, 6, -0.105), up=0.045)
    thr = V(0, -0.47, 0.108)

    def extra(sk):
        sk.bone('throat', thr, thr + V(0, 0, -0.07), 'neck2')
    C = B.Beast('mon_dusk_lizard', RB.quad_skel(J, ears=False, extra=extra))
    std_meta(C, 'dusk', 1.0, 3.6)
    Q = RB.Quad(C, J, glide=(0.05, 0.05))
    C.look('hide', 'scales', 'skin_hide', color=(0.17, 0.16, 0.135), color2=(0.075, 0.07, 0.06), light=(0.33, 0.31, 0.26), scale=70.0,
           mud_hi=0.12, rough=0.5)
    C.look('maw', 'skin', 'skin_hide', color=(0.20, 0.07, 0.06), blotch=(0.10, 0.03, 0.03), rough=0.3, sore_amt=0.0, scar=0.0)
    C.look('claw', 'bone', 'skin_hide', color=(0.45, 0.42, 0.34), stain=(0.18, 0.13, 0.08), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('sail', 'metal', 'metal_sail', color=(0.36, 0.26, 0.09), rust=(0.10, 0.33, 0.24), rust2=(0.04, 0.15, 0.12), amount=0.92,
           edge_color=(0.62, 0.50, 0.24), rough=0.5, hammer=60.0)
    C.look('sack', 'glow', 'emis_throat', color=(0.13, 0.20, 0.15), emit=(0.25, 0.95, 0.6), strength=1.9, vein=(0.03, 0.06, 0.04), vein_scale=18.0)
    tl = J['tail']
    tail_a = P.tubep([tl[0] + V(0, -0.10, 0.0)] + tl[:4], [0.085, 0.082, 0.066, 0.05, 0.036], [0.10, 0.085, 0.062, 0.045, 0.032], m=12, n=12)
    B.wbones(tail_a, ['hip', 'tail1', 'tail2', 'tail3', 'tail4'], sharp=4.0)

    def sculpt(body):
        body.dent((0.0, -0.60, 0.19), 0.05, -0.006)

    S = dict(
        torso=[(0.36, 0.125, 0.085, 0.105), (0.26, 0.127, 0.10, 0.14), (0.05, 0.13, 0.115, 0.17), (-0.15, 0.135, 0.115, 0.165),
               (-0.28, 0.14, 0.10, 0.135), (-0.36, 0.145, 0.078, 0.10)],
        neck=[(-0.30, 0.148, 0.088, 0.105), (-0.38, 0.155, 0.072, 0.085), (-0.46, 0.158, 0.064, 0.078)],
        head=[P.tubep([(0, -0.43, 0.16), (0, -0.52, 0.162), (0, -0.62, 0.148), (0, -0.725, 0.132)], [0.055, 0.05, 0.036, 0.022],
                      [0.075, 0.082, 0.058, 0.036], m=9, n=12, power=2.5)],
        fleg=[(0.07, 0.055), (0.06, 0.05), (0.046, 0.04), (0.031, 0.03), (0.026, 0.026)],
        hleg=[(0.08, 0.06), (0.068, 0.055), (0.05, 0.042), (0.033, 0.031), (0.028, 0.028)],
        f_root=(-0.07, 0, 0.01), h_root=(-0.06, 0, 0.01),
        fpaw=(0.045, 0.055, 0.017), hpaw=(0.047, 0.06, 0.017),
        extra=[tail_a], voxel=0.0095, tris=5200, torso_power=2.3, sculpt=sculpt)
    body = RB.quad_flesh(C, J, S)
    C.skin(body, 'hide')
    bvh = P.bvh_of(body)
    tip = P.tubep([tl[3] + V(0, -0.06, 0.004)] + tl[3:], [0.03, 0.034, 0.024, 0.015, 0.006], [0.027, 0.03, 0.021, 0.013, 0.005], m=12, n=8)
    B.wbones(tip, ['tail3', 'tail4', 'tail5', 'tail6'], sharp=4.0)
    C.skin(tip, 'hide', smooth=180)
    jaw = P.tubep([(0, -0.45, 0.125), (0, -0.56, 0.108), (0, -0.66, 0.10), (0, -0.715, 0.098)], [0.03, 0.026, 0.02, 0.014],
                  [0.07, 0.072, 0.052, 0.032], m=8, n=10, power=2.5)
    C.rigid(jaw, 'jaw', 'hide', smooth=180)
    tongue = P.tubep([(0, -0.50, 0.122), (0, -0.60, 0.116), (0, -0.69, 0.108)], [0.008, 0.008, 0.005], [0.045, 0.036, 0.02], m=6, n=8)
    C.rigid(tongue, 'jaw', 'maw', smooth=180)
    for sg in (1, -1):
        ys = np.linspace(-0.705, -0.55, 8)
        up = [(sg * (0.028 + 0.045 * (y + 0.705) / 0.155), y, 0.118 + 0.012 * (y + 0.705) / 0.155) for y in ys]
        C.rigid(P.tooth_row(bvh, up, (sg * 0.15, 0, -1), 0.014, 0.004, seed=3 + sg), 'head', 'claw')
    RB.add_claws(C, bvh, J, 'claw', n=4, length=0.04, r=0.007, spread=0.022, drop=0.25, sides=4)
    # throat sack (swells before the spit)
    sack = P.blob(thr + V(0, -0.005, -0.035), 0.055, 0.08, 0.045, n=12, m=7)
    C.rigid(sack, 'throat', 'sack', smooth=180)
    # the sail: separate plates, each rigid to the spine bone under it
    ys = np.linspace(-0.31, 0.43, 10)
    hs = [0.10, 0.17, 0.225, 0.26, 0.27, 0.25, 0.215, 0.17, 0.12, 0.075]
    for i, (y, h) in enumerate(zip(ys, hs)):
        z = surf_top(bvh, 0.0, y)
        pl = H.sail_plate((0, y, z - 0.03), 0.088, h + 0.03, th=0.009, lean=0.28, notch=0.35, seed=i)
        bone = 'chest' if y < -0.095 else ('spine2' if y < 0.09 else ('spine1' if y < 0.30 else 'tail1'))
        C.rigid(pl, bone, 'sail', smooth=30)

    def throat(k):
        def fn(p, u):
            p.scl('throat', 1.0 + k * osc(u, 2, 0.1))
        return fn
    Q.idle(dur=3.6, breath=0.004, look=12.0, tail=5.0, shift=0.006, ear=0.0, tail_lift=0.0, extra=throat(0.08))
    Q.gait('Walk', 1.0, 0.6, 0.6, RB.TROT, lift=0.035, bob=0.003, roll=2.0, yaw=5.0, side=10.0, headyaw=4.0, nod=1.0, tail=20.0,
           taillag=0.16, tailph=0.65, tuck=12.0, pastern=10.0, extra=throat(0.04))
    Q.gait('Run', 3.6, 0.267, 0.36, RB.TROT, lift=0.05, bob=0.006, roll=3.0, yaw=8.0, side=16.0, headyaw=6.4, nod=1.5, tail=26.0,
           taillag=0.16, tailph=0.65, tuck=18.0, pastern=12.0, extra=throat(0.04))

    def mk_spit(gape):
        def spit(p, u):
            hu = 0.5 / 1.0
            w = bump(u, 0.0, hu * 0.6, hu * 0.78, hu)
            s = bump(u, hu * 0.78, hu, hu + 0.1, 0.95)
            up = bump(u, 0.0, hu * 0.6, hu + 0.12, 0.92)     # the head stays raised through the spit: the jaw needs the room
            Q.body(p, dy=0.04 * w - 0.08 * s, dz=0.02 * up, pitch=-7 * up)
            Q.spine(p, flex=-7 * w - 3 * s)
            Q.neck(p, pitch=-24 * w - 12 * s, head=0.4)
            Q.jaw(p, gape * bump(u, hu * 0.82, hu, hu + 0.06, hu + 0.25))
            p.scl('throat', 1.0 + 0.75 * bump(u, 0.02, hu * 0.7, hu * 0.92, hu + 0.03) - 0.2 * bump(u, hu, hu + 0.04, hu + 0.1, hu + 0.4))
            Q.tailw(p, u, amp=4, n=2)
            p.aim_world('tail1', I3)                          # the long tail stays flat on the ground while the body rears
            Q.plant(p)
        return spit
    C.clip('Attack1', 1.0, mk_spit(Q.fit(mk_spit, 38.0, [0.5, 0.53, 0.56], lo=4.0, zmin=0.008)), hit=0.5)
    Q.reach('Attack1')
    tip_pt = tl[-1].copy()

    def whip(p, u):
        hu = 0.5 / 1.1
        w = bump(u, 0.0, hu * 0.4, hu * 0.55, hu * 0.8)
        k = sm(u, hu * 0.5, hu * 1.02) * (1 - sm(u, hu + 0.2, 0.93))
        hop = 0.07 * bump(u, hu * 0.5, hu * 0.7, hu * 0.95, hu * 1.15) + 0.05 * bump(u, hu + 0.2, hu + 0.3, 0.8, 0.93)
        Q.body(p, yaw=-6 * w + 118 * k, dz=hop)
        Q.spine(p, side=14 * w - 16 * k)
        Q.neck(p, yaw=-10 * w - 34 * k, pitch=-4 * k)
        Q.jaw(p, 14 * k)
        c = -9 * w - 10 * bump(u, hu * 0.5, hu * 0.62, hu * 0.72, hu * 0.92) + 17 * bump(u, hu * 0.72, hu, hu + 0.08, hu + 0.3)
        for i, b in enumerate(Q.tail):
            p.rot(b, z=c * (0.75 + 0.1 * i), x=1.5 * bump(u, hu * 0.5, hu * 0.7, hu + 0.1, hu + 0.3))
        air = sm(u, hu * 0.46, hu * 0.56) * (1 - sm(u, 0.9, 0.97))
        tk = 180.0 * hop                                      # the legs are drawn up only while it is off the ground
        Q.plant(p, air=air, tuck=dict(f={'bend': (0, tk, 0)}, h={'bend': (0, tk, 0)}))
    C.clip('Attack2', 1.1, whip, hit=0.5)
    C.reach('Attack2', Q.tail[-1], tip_pt)
    Q.hit(back=0.04, drop=-0.006, roll=3.0, pitch=0.0, extra=throat(0.0))
    Q.death_belly(dur=1.6, roll=5.0, throe=12.0, planted=True, splay=0.06, hyaw=24.0, jaw=26.0, drop=0.05, tail_side=9.0,
                  extra=lambda p, u: p.scl('throat', 1.0 - 0.5 * sm(u, 0.2, 0.8)))
    Q.spawn_rise(dur=1.2, depth=0.38)
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2: sprawl table")


# ================================================================================================ forge_hound
@reg
def forge_hound(bake=True):
    """Slag-hound (swarm budget): no skin - an iron spine, a cage of hoops with a burning coal in it, clinker for
    shoulders and hips, rod legs, a hinged jaw-trap for a head and the tether chain dragging from its collar"""
    J = RB.quad_J(L=0.56, hs=0.62, hh=0.60, wf=0.16, wh=0.11, neck=(0.22, 0.13), head=0.26, sag=0.0, tail=(0.1, 1, -1.0))
    J['tail'] = None
    HG = V(0, -0.515, 0.722)                       # the hinge of the trap
    J['jawh'] = HG
    J['snout'] = V(0, -0.785, 0.70)
    J['chin'] = V(0, -0.772, 0.664)
    cp = [V(0, -0.368, 0.604), V(0.02, -0.39, 0.035), V(0.03, -0.08, 0.03), V(-0.03, 0.26, 0.03), V(0.02, 0.60, 0.03)]

    def extra(sk):
        sk.chain('chain', cp, 'chest')
    C = B.Beast('mon_forge_hound', RB.quad_skel(J, ears=False, lite=True, extra=extra), budget=4000, bone_budget=28, mb=2.5, tex=1024)
    std_meta(C, 'forge', 1.0, 6.0)
    Q = RB.Quad(C, J)
    rg = C.rg
    C.look('iron', 'metal', 'metal_iron', color=(0.14, 0.135, 0.13), rust=(0.21, 0.09, 0.03), rust2=(0.07, 0.035, 0.02), amount=0.7, rough=0.55,
           edge_color=(0.34, 0.32, 0.30))
    C.look('clinker', 'rock', 'rock_clinker', color=(0.055, 0.048, 0.044), ash=(0.21, 0.2, 0.19), scale=24.0, ash_amt=0.6,
           heat=(1.0, 0.3, 0.05), heat_amt=0.5)
    C.look('ember_coal', 'emis', 'emis_coal', emit=(1.0, 0.38, 0.06), strength=2.0, mottle=0.8, base_mul=0.5, scale=26.0)
    pel, mid, chj = rg.H('spine1'), rg.H('chest'), rg.T('chest')
    d1 = unit(mid - pel); d2 = unit(chj - mid)
    C.rigid(bar(pel - d1 * 0.05, mid + d1 * 0.03, 0.032, 0.024), 'spine1', 'iron')
    C.rigid(bar(mid - d2 * 0.03, chj + d2 * 0.05, 0.032, 0.024), 'chest', 'iron')
    # deep narrow ribcage of hoops (the legs swing outside it) + sternum bar; the coal lies in it
    ys = [-0.245, -0.165, -0.085, 0.0, 0.085, 0.165]
    Rs = [0.085, 0.095, 0.095, 0.085, 0.07, 0.055]
    Rzs = [0.15, 0.17, 0.165, 0.14, 0.11, 0.085]
    bot = []
    for y, R, Rz in zip(ys, Rs, Rzs):
        t = (y - mid[1]) / (chj[1] - mid[1]) if y < mid[1] else (y - mid[1]) / (pel[1] - mid[1])
        zs = (mid[2] + (chj[2] - mid[2]) * t) if y < mid[1] else (mid[2] + (pel[2] - mid[2]) * t)
        C.rigid(hoop((0, y, zs - Rz + 0.006), R, Rz, 0.0115, n=12, m=4), 'chest' if y < mid[1] else 'spine1', 'iron', smooth=60)
        bot.append(V(0, y, zs - 2 * Rz + 0.012))
    C.rigid(M.join([bar(bot[i] + V(0, -0.02, 0), bot[i + 1] + V(0, 0.02, 0), 0.026, 0.016) for i in (0, 1)]), 'chest', 'iron')
    C.rigid(M.join([bar(bot[i] + V(0, -0.02, 0), bot[i + 1] + V(0, 0.02, 0), 0.026, 0.016) for i in (2, 3, 4)]), 'spine1', 'iron')
    coal = M.rock(0.072, 0.105, 0.078, seed=5, n=20, jit=0.22)
    coal.move(0, -0.125, bot[1][2] + 0.072)
    C.rigid(coal, 'chest', 'ember_coal', smooth=0)
    # shoulders and hips: an iron axle with a clinker block on the spine
    fz = float(J['f'][0][2]); fy = float(J['f'][0][1]); hz = float(J['h'][0][2]); hy = float(J['h'][0][1])
    C.rigid(H.block(0.42, 0.13, 0.16, (0, fy, fz + 0.045), seed=2, jit=0.02, bevel=0.035), 'chest', 'clinker')
    C.rigid(H.block(0.31, 0.13, 0.16, (0, hy, hz + 0.04), seed=9, jit=0.02, bevel=0.035), 'hip', 'clinker')
    # legs: clinker thighs, iron rods below, clinker pads
    for pre in ('f', 'h'):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            pts = [q * (sg, 1, 1) for q in J[pre]]
            b = ['%s_%s_%s' % (pre, n, s) for n in RB.LEGN]
            up = P.seg_tube(pts[0], pts[1], 0.04, 0.03, n=5, bulge=1.3)
            up.rough(0.006, 30.0, seed=3.0 + sg)
            C.rigid(up, b[0], 'clinker', smooth=30)
            C.rigid(P.seg_tube(pts[1], pts[2], 0.026, 0.018, n=5, bulge=1.45), b[1], 'iron', smooth=50)
            C.rigid(P.seg_tube(pts[2], pts[3], 0.02, 0.016, n=4, bulge=1.5), b[2], 'iron', smooth=50)
            pad = M.rock(0.036, 0.055, 0.025, seed=int(7 + sg + (0 if pre == 'f' else 4)), n=12, jit=0.2)
            pad.move(pts[3] + V(0, -0.03, -0.012))
            pad.v[:, 2] = np.maximum(pad.v[:, 2], 0.0)
            C.rigid(pad, b[3], 'clinker', smooth=0)
    # neck of heavy links, collar with the tether ring under it
    H.chain_on(C, ['neck1', 'neck2'], 'iron', link=0.095, r=0.012, n=6, m=3)
    nd = unit(rg.T('neck1') - rg.H('neck1'))
    cc = rg.H('neck1') + nd * 0.07
    C.rigid(P.ring(cc, 0.05, 0.013, axis=nd, n=10, m=4), 'chest', 'iron', smooth=60)
    C.rigid(P.ring(cp[0] + V(0, 0, 0.004), 0.02, 0.007, axis=(1, 0, 0), n=8, m=3), 'chest', 'iron', smooth=60)
    # the trap
    for bone, tipp, tdir, sd in (('head', J['snout'], V(0, 0.1, -1.0), 0), ('jaw', J['chin'], V(0, 0.1, 1.0), 1)):
        C.rigid(H.trap_jaw(HG, tipp, 0.06, tdir, nteeth=9 - sd, tooth=0.058, bar_w=0.036, bar_t=0.018, seed=sd), bone, 'iron', smooth=30)
    C.rigid(M.tube(np.array([HG + V(-0.08, 0, 0), HG + V(0.08, 0, 0)]), 0.016, n=6, up=(0, 0, 1)), 'head', 'iron', smooth=60)
    C.rigid(bar(HG + V(0, 0.01, 0.0), HG + V(0, -0.12, 0.05), 0.04, 0.01), 'head', 'iron')
    # the tether chain
    H.chain_on(C, ['chain1', 'chain2', 'chain3', 'chain4'], 'iron', link=0.078, r=0.0088, n=6, m=3)
    DC = DragChain(C, ['chain1', 'chain2', 'chain3', 'chain4'], 'chest')

    def drag(wave=0.0, n=1):
        def fn(p, u):
            DC.pose(p, u, wave=wave, n=n)
        return fn
    Q.idle(dur=3.0, look=9.0, tail=0.0, sniff=5.0, extra=drag(1.5))
    Q.gait('Walk', 1.2, 0.66, 0.64, RB.WALK4, lift=0.06, bob=0.008, crouch=0.03, nod=2.5, headlow=8.0, tuck=35.0, extra=drag(5.0, 2))
    Q.gait('Run', 6.0, 0.40, 0.22, RB.GALLOP, lift=0.14, bob=0.03, bobn=1, bobph=0.35, pitch=6.0, pitchph=0.55, roll=0.6, yaw=0.8,
           flex=10.0, flexph=0.2, crouch=0.06, nod=3.0, tuck=70.0, headlow=10.0, jaw=10.0, extra=drag(9.0, 2))
    Q.bite(dur=0.8, hit=0.42, reach=0.22, gape=46.0, extra=drag())
    Q.leap(dur=1.1, hit=0.6, fwd=0.5, up=0.22, gape=50.0, extra=drag())
    Q.hit(extra=drag())
    Q.death_side(dur=1.6, half_w=0.12, kick=False, extra=drag())
    Q.spawn_rise(dur=1.2, extra=drag())
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2: rigid parts only (no skinned body)")


# ================================================================================================ dusk_lynx
@reg
def dusk_lynx(bake=True):
    """Crevice-cat: stilt-legged white cat, snowshoe paws, very short body, ears as long as its head with dark
    tufts, a thick club tail held straight up; two pale green eye-shines"""
    J = RB.quad_J(L=0.40, hs=0.80, hh=0.74, wf=0.075, wh=0.08, neck=(0.16, 0.10), head=0.16, fleg=(0.84, 0.55, 0.20, 0.05),
                  hleg=(0.86, 0.55, 0.28, 0.05), tail=(0.1, 1, -1.0), bend=1.15, paw=1.3)
    J['tail'] = [V(0, 0.235, 0.70), V(0, 0.27, 0.84), V(0, 0.275, 0.99), V(0, 0.265, 1.14)]
    hj = J['hj']
    J['ear'] = (hj + V(0.052, 0.012, 0.05), hj + V(0.095, 0.05, 0.235))
    C = B.Beast('mon_dusk_lynx', RB.quad_skel(J))
    std_meta(C, 'dusk', 1.0, 6.4)
    Q = RB.Quad(C, J)
    Q.tail_axis = 'y'
    C.look('fur', 'wfur', 'hair_coat', color=(0.74, 0.74, 0.72), tip=(0.95, 0.95, 0.93), ash=0.12, ash_color=(0.36, 0.36, 0.36),
           flow=(0, 0.5, -0.8), scale=110.0, lock=46.0, rough=0.9, bump=0.42, lift=0.55, grime_amt=0.3)
    C.look('fluff', 'wfur', 'hair_coat', color=(0.82, 0.82, 0.80), tip=(1.0, 1.0, 0.98), flow=(0, 0.3, -1.0), scale=120.0, lock=60.0,
           rough=0.92, bump=0.3, lift=0.7, grime_amt=0.0, drift=0.1)
    C.look('dark', 'bfur', 'hair_coat', color=(0.05, 0.05, 0.05), tip=(0.16, 0.16, 0.16), flow=(0, 0.2, 1.0), scale=80.0)
    C.look('ear', 'membrane', 'hair_coat', color=(0.34, 0.27, 0.25), vein=(0.14, 0.07, 0.07), vein_scale=18.0)
    C.look('maw', 'skin', 'hair_coat', color=(0.18, 0.06, 0.055), blotch=(0.08, 0.03, 0.03), rough=0.3, sore_amt=0.0, scar=0.0)
    C.look('claw', 'bone', 'hair_coat', color=(0.50, 0.47, 0.40), stain=(0.2, 0.15, 0.09), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('eyes', 'emis', 'emis_eyes', emit=(0.30, 1.0, 0.42), strength=1.5, mottle=0.1, base_mul=0.6)
    tl = J['tail']
    club = P.tubep([tl[0] + V(0, -0.04, -0.06)] + tl + [tl[-1] + V(0, -0.004, 0.035)], [0.04, 0.038, 0.046, 0.064, 0.062, 0.03], m=14, n=10)
    B.wbones(club, ['hip', 'tail1', 'tail2', 'tail3'], sharp=4.0)
    S = dict(
        torso=[(0.30, 0.70, 0.06, 0.055), (0.20, 0.70, 0.105, 0.085), (0.02, 0.705, 0.10, 0.08), (-0.14, 0.72, 0.125, 0.09),
               (-0.24, 0.74, 0.12, 0.085), (-0.31, 0.77, 0.08, 0.06)],
        neck=[(-0.20, 0.80, 0.085, 0.065), (-0.29, 0.86, 0.065, 0.055), (-0.36, 0.905, 0.06, 0.055)],
        head=[P.blob((0, -0.395, 0.915), 0.082, 0.085, 0.076),
              P.tubep([(0, -0.42, 0.895), (0, -0.48, 0.878), (0, -0.518, 0.87)], [0.05, 0.04, 0.032], [0.058, 0.048, 0.04], m=7, n=10),
              P.blob((0.06, -0.415, 0.88), 0.04, 0.052, 0.046), P.blob((-0.06, -0.415, 0.88), 0.04, 0.052, 0.046)],
        fleg=[(0.06, 0.04), (0.06, 0.042), (0.04, 0.032), (0.026, 0.024), (0.032, 0.032)],
        hleg=[(0.085, 0.045), (0.085, 0.05), (0.05, 0.036), (0.026, 0.024), (0.032, 0.032)],
        fpaw=(0.066, 0.088, 0.034), hpaw=(0.06, 0.082, 0.034),
        blobs=[((0.07, 0.19, 0.62), (0.05, 0.10, 0.12), ['hip', 'h_upper_l'], True),
               ((0.07, -0.20, 0.66), (0.04, 0.07, 0.10), ['chest', 'f_upper_l'], True)],
        extra=[club], voxel=0.011, tris=4600)
    body = RB.quad_flesh(C, J, S)
    C.skin(body, 'fur')
    bvh = P.bvh_of(body)
    jaw = P.tubep([(0, -0.40, 0.862), (0, -0.47, 0.848), (0, -0.512, 0.842)], [0.022, 0.019, 0.014], [0.048, 0.04, 0.03], m=7, n=10)
    C.rigid(jaw, 'jaw', 'fur', smooth=180)
    jb = P.bvh_of(jaw)
    C.rigid(P.tubep([(0, -0.42, 0.868), (0, -0.47, 0.859), (0, -0.50, 0.853)], [0.006, 0.006, 0.004], [0.022, 0.018, 0.012], m=5, n=8),
            'jaw', 'maw', smooth=180)
    for sg in (1, -1):
        C.rigid(P.seated(bvh, (sg * 0.022, -0.508, 0.856), (sg * 0.1, -0.1, -1), 0.03, 0.0055, curl=(0, 1, 0), seg=3), 'head', 'claw', smooth=50)
        C.rigid(P.seated(jb, (sg * 0.018, -0.502, 0.85), (sg * 0.12, -0.1, 1), 0.022, 0.0045, curl=(0, 1, 0), seg=3), 'jaw', 'claw', smooth=50)
        q, nrm, _ = P.nearest(bvh, (sg * 0.04, -0.49, 0.94))
        C.rigid(P.blob(q - nrm * 0.004, 0.0125, 0.0125, 0.0125, n=8, m=5), 'head', 'eyes', smooth=180)
        eb, et = J['ear'][0] * (sg, 1, 1), J['ear'][1] * (sg, 1, 1)
        e = H.tri_ear(eb, et, 0.05, cup=0.022, th=0.007, sink=0.03, face=(sg * 0.4, -1, 0))
        C.rigid(e, 'ear_l' if sg > 0 else 'ear_r', 'ear', smooth=180)
        C.rigid(P.cone(et - unit(et - eb) * 0.03, unit(et - eb) + V(0, 0.15, 0), 0.085, 0.011, n=4), 'ear_l' if sg > 0 else 'ear_r', 'dark', smooth=180)

    def shag(c, n):
        tail = (c[:, 2] > 0.86) & (c[:, 1] > 0.17)
        cheek = (c[:, 1] < -0.36) & (c[:, 1] > -0.46) & (np.abs(c[:, 0]) > 0.045) & (c[:, 2] > 0.83) & (c[:, 2] < 0.92)
        belly = (n[:, 2] < -0.25) & (c[:, 2] > 0.56) & (c[:, 1] > -0.32) & (c[:, 1] < 0.26)
        return tail | cheek | belly

    def flow(p):
        if p[2] > 0.86 and p[1] > 0.17:
            return (0.0, 0.25, -1.0)
        if p[1] < -0.37:
            return (np.sign(p[0]) * 0.8, 0.5, -0.6)
        return (0.0, 0.3, -1.0)
    tf = P.tufts(body, 190, length=(0.05, 0.095), radius=(0.016, 0.028), seed=7, mask=shag, flow=flow, lay=0.72, sink=0.35, sides=4, jitter=0.25)
    C.skin(tf, 'fluff', smooth=180)

    Q.idle(dur=3.4, look=14.0, tail=8.0, ear=18.0)
    Q.gait('Walk', 1.2, 0.667, 0.62, RB.WALK4, lift=0.08, bob=0.008, crouch=0.03, nod=2.5, headlow=6.0, tail=5.0, tuck=40.0)
    Q.gait('Run', 6.4, 0.40, 0.22, RB.GALLOP, lift=0.16, bob=0.035, bobn=1, bobph=0.35, pitch=7.0, pitchph=0.55, roll=0.6, yaw=0.8,
           flex=16.0, flexph=0.2, crouch=0.07, nod=3.0, tail=4.0, tuck=75.0, headlow=8.0, ears=-20.0, jaw=6.0)
    Q.swipe(dur=0.7, hit=0.34, reach=0.34, up=0.34)
    Q.leap(dur=1.1, hit=0.6, fwd=0.62, up=0.32, gape=36.0)
    Q.hit()
    Q.death_side(dur=1.6, half_w=0.085)
    Q.spawn_rise(dur=1.2)
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2")


# ================================================================================================ hush_beast
@reg
def hush_beast(bake=True):
    """Hush-hound: smooth dead-white quadruped, long fore legs and a hyena slope, no ears, no tail, no mouth; the
    head is a blunt wedge with a round hole through it from side to side"""
    J = RB.quad_J(L=0.62, hs=0.88, hh=0.60, wf=0.10, wh=0.085, neck=(0.26, 0.04), head=0.30, fleg=(0.80, 0.55, 0.22, 0.05),
                  hleg=(0.84, 0.55, 0.30, 0.06), tail=(0.1, 1, -1.0), bend=1.2, paw=1.1)
    J['tail'] = None
    J['snout'] = V(0, -0.885, 0.845)
    C = B.Beast('mon_hush_beast', RB.quad_skel(J, ears=False, jaw=False))
    std_meta(C, 'hush', 1.1, 6.4)
    Q = RB.Quad(C, J)
    C.look('hush', 'chalk', 'skin_hush', color=(0.78, 0.78, 0.77), shade=(0.56, 0.57, 0.60), crack=30.0)
    S = dict(
        torso=[(0.40, 0.56, 0.06, 0.055), (0.30, 0.565, 0.115, 0.095), (0.10, 0.63, 0.125, 0.10), (-0.12, 0.72, 0.155, 0.115),
               (-0.28, 0.78, 0.175, 0.125), (-0.38, 0.81, 0.14, 0.10), (-0.44, 0.84, 0.09, 0.07)],
        neck=[(-0.32, 0.86, 0.13, 0.095), (-0.44, 0.895, 0.10, 0.08), (-0.54, 0.915, 0.09, 0.075)],
        head=[P.tubep([(0, -0.50, 0.925), (0, -0.62, 0.915), (0, -0.76, 0.885), (0, -0.86, 0.855), (0, -0.895, 0.845)],
                      [0.105, 0.125, 0.105, 0.07, 0.05], [0.08, 0.085, 0.075, 0.06, 0.045], m=10, n=14, power=2.6)],
        fleg=[(0.075, 0.05), (0.075, 0.052), (0.05, 0.04), (0.03, 0.028), (0.03, 0.03)],
        hleg=[(0.085, 0.05), (0.085, 0.055), (0.052, 0.04), (0.028, 0.026), (0.028, 0.028)],
        fpaw=(0.04, 0.058, 0.028), hpaw=(0.036, 0.052, 0.026),
        blobs=[((0.075, 0.30, 0.50), (0.05, 0.10, 0.12), ['hip', 'h_upper_l'], True),
               ((0.09, -0.31, 0.72), (0.045, 0.08, 0.12), ['chest', 'f_upper_l'], True)],
        voxel=0.013, tris=4800, smooth=10)
    body = RB.quad_flesh(C, J, S)
    hole = M.tube(np.array([(-0.4, -0.655, 0.915), (0.4, -0.655, 0.915)]), 0.05, n=20, up=(0, 0, 1))
    cut = H.clean_mesh(M.boolean(body, hole, 'DIFFERENCE'))
    M.transfer_weights(cut, body, smooth=1)
    cut.smooth = 50.0
    C.skin(cut, 'hush')

    Q.idle(dur=3.6, breath=0.005, look=6.0, tail=0.0, ear=0.0)
    Q.gait('Walk', 1.1, 0.733, 0.62, RB.WALK4, lift=0.07, bob=0.008, crouch=0.03, nod=2.0, headlow=6.0, tuck=35.0)
    Q.gait('Run', 6.4 / 1.1, 0.40, 0.22, RB.GALLOP, lift=0.15, bob=0.03, bobn=1, bobph=0.35, pitch=5.0, pitchph=0.55, roll=0.6, yaw=0.8,
           flex=12.0, flexph=0.2, crouch=0.07, nod=3.0, tuck=70.0, headlow=10.0)
    Q.swipe(dur=0.75, hit=0.36, reach=0.38, up=0.36, gape=0.0)
    Q.leap(dur=1.1, hit=0.6, fwd=0.65, up=0.28, gape=0.0)
    Q.hit()
    Q.death_side(dur=1.7, half_w=0.11, kick=False)
    hh = V(C.rg.H('hip'))

    def spawn(p, u):
        """swells out of nothing from a point on the ground"""
        s = 0.03 + 0.97 * sm(u, 0.0, 0.55) + 0.06 * bump(u, 0.45, 0.56, 0.6, 0.8)
        Q.body(p, roll=4 * bump(u, 0.55, 0.62, 0.7, 0.9) * osc(u, 6))
        p.scl('hip', s)
        p.move('hip', (s - 1.0) * hh)
        Q.neck(p, pitch=20 * (1 - sm(u, 0.2, 0.7)))
        Q.plant(p, air=1 - sm(u, 0.6, 0.88))
    C.clip('Spawn', 1.2, spawn)
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2: the hole is a boolean cut after the remesh")


# ================================================================================================ effigy_hound
@reg
def effigy_hound(bake=True):
    """Foot-hound: the stone hound carved lying at the barrow-lord's feet - a low brick of a dog with a flat underside,
    a hollow worn across its back, a carved collar with a glowing ring, ears chipped to stumps"""
    J = RB.quad_J(L=0.46, hs=0.42, hh=0.42, wf=0.17, wh=0.17, neck=(0.15, 0.10), head=0.22, fleg=(0.74, 0.46, 0.20, 0.07),
                  hleg=(0.74, 0.46, 0.24, 0.07), tail=(0.16, 2, -0.5), bend=0.7, paw=0.85)
    C = B.Beast('mon_effigy_hound', RB.quad_skel(J, ears=False))
    std_meta(C, 'effigy', 1.0, 5.4)
    Q = RB.Quad(C, J, glide=(0.04, 0.03))
    C.look('stone', 'rock', 'rock_stone', color=(0.22, 0.215, 0.20), ash=(0.36, 0.355, 0.33), ash_amt=0.55, scale=9.0, crack_w=0.02, rough=0.85,
           bump=0.5)
    C.look('ring', 'emis', 'emis_ring', emit=(0.30, 1.0, 0.38), strength=1.5, mottle=0.4, base_mul=0.5)
    ZF = 0.255

    def carve(body):
        v = body.v
        under = (np.abs(v[:, 0]) < 0.10) & (v[:, 1] > -0.34) & (v[:, 1] < 0.36)
        v[under, 2] = np.maximum(v[under, 2], ZF)                                  # the flat underside (it was part of the lid)
        top = (v[:, 2] > 0.40) & (v[:, 1] > -0.22) & (v[:, 1] < 0.30)
        v[top, 2] -= 0.09 * np.exp(-((v[top, 1] - 0.03) / 0.07) ** 2)              # the hollow worn by the lord's heels
        body.v = v
        body.dent((0.0, -0.50, 0.555), 0.05, -0.008)

    S = dict(
        torso=[(0.345, 0.378, 0.095, 0.09), (0.30, 0.378, 0.123, 0.115), (0.0, 0.378, 0.123, 0.115), (-0.28, 0.378, 0.123, 0.115),
               (-0.335, 0.385, 0.10, 0.095)],
        neck=[(-0.26, 0.43, 0.115, 0.10), (-0.33, 0.475, 0.105, 0.095), (-0.39, 0.51, 0.10, 0.09)],
        head=[P.tubep([(0, -0.36, 0.525), (0, -0.44, 0.52), (0, -0.52, 0.497), (0, -0.605, 0.478)], [0.085, 0.085, 0.06, 0.048],
                      [0.085, 0.085, 0.062, 0.052], m=9, n=12, power=3.5),
              P.blob((0, -0.43, 0.59), 0.07, 0.05, 0.03)],
        fleg=[(0.075, 0.06), (0.07, 0.056), (0.055, 0.046), (0.042, 0.04), (0.042, 0.042)],
        hleg=[(0.08, 0.06), (0.075, 0.058), (0.055, 0.046), (0.042, 0.04), (0.042, 0.042)],
        fpaw=(0.046, 0.062, 0.03), hpaw=(0.046, 0.06, 0.03),
        blobs=[((0.15, 0.22, 0.36), (0.06, 0.10, 0.10), ['hip', 'h_upper_l'], True),
               ((0.15, -0.25, 0.36), (0.055, 0.09, 0.10), ['chest', 'f_upper_l'], True)],
        voxel=0.01, tris=4400, torso_power=6.0, neck_power=3.0, leg_power=3.0, smooth=3, sculpt=carve)
    body = RB.quad_flesh(C, J, S)
    C.skin(body, 'stone')
    bvh = P.bvh_of(body)
    RB.add_tail(C, J, 0.036, 0.022, 'stone', n=6, sink=0.05)
    jaw = P.tubep([(0, -0.40, 0.47), (0, -0.50, 0.455), (0, -0.588, 0.445)], [0.03, 0.026, 0.02], [0.07, 0.058, 0.045], m=7, n=10, power=3.0)
    C.rigid(jaw, 'jaw', 'stone', smooth=180)
    for sg in (1, -1):                                                              # ears chipped to stumps
        q, nrm, _ = P.nearest(bvh, (sg * 0.062, -0.375, 0.61))
        C.rigid(M.rock(0.022, 0.02, 0.032, seed=3 + sg, n=10, jit=0.3).move(q - nrm * 0.006), 'head', 'stone', smooth=0)
    # carved collar and its ring
    nd = unit(C.rg.T('neck1') - C.rg.H('neck1'))
    cc = C.rg.H('neck1') + nd * 0.05
    col = M.tube(np.array([cc - nd * 0.022, cc + nd * 0.022]), 0.128, n=18, r2=0.118, up=(0, 0, 1))
    col.smooth = 30.0
    C.rigid(col, 'neck1', 'stone')
    side = unit(np.cross(nd, V(1, 0, 0)))                                           # points up-forward in the collar plane
    rp = cc + side * 0.128 + V(0.05, 0, 0)
    C.rigid(M.box(0.03, 0.03, 0.03).move(cc + side * 0.12 + V(0.05, 0, 0)), 'neck1', 'stone', smooth=0)
    C.rigid(P.ring(rp + side * 0.035, 0.036, 0.011, axis=nd, n=14, m=6), 'neck1', 'ring', smooth=60)

    Q.idle(dur=4.0, breath=0.002, look=5.0, tail=0.0, shift=0.004, ear=0.0)
    Q.gait('Walk', 0.8, 0.5, 0.6, RB.WALK4, lift=0.035, bob=0.004, crouch=0.012, nod=1.0, tuck=12.0, roll=2.5, yaw=1.0, tail=0.0, pastern=14.0)
    Q.gait('Run', 5.4, 0.2, 0.21, RB.BOUND, lift=0.06, bob=0.02, bobn=2, bobph=0.2, pitch=9.0, pitchph=0.6, roll=0.0, yaw=0.0, crouch=0.045,
           nod=2.0, tuck=14.0, tail=0.0, pastern=16.0, jaw=6.0)
    Q.bite(dur=0.8, hit=0.42, reach=0.14, gape=30.0, drop=0.01, down=8.0)
    Q.leap(dur=1.1, hit=0.6, fwd=0.45, up=0.2, gape=30.0, crouch=0.05)
    Q.hit(back=0.04, turn=10.0)
    Q.death_side(dur=1.4, half_w=0.115, kick=False)
    Q.spawn_stand(dur=1.5, drop=ZF - 0.02, fold=dict(f=(40, -118, 15), h=(-40, 95, -80)), shake=3.0, hold=0.18)
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2: Spawn frame 0 = the carved lying pose (Dormant)")


# ================================================================================================ dusk_yak
@reg
def dusk_yak(bake=True):
    """Rimeback: a moving haystack - massive ox whose ice-matted coat hangs to the ground in ropes, one wide pair of
    horns curving down and forward past the jaw, a drift of snow on its back. The legs work under the coat."""
    J = RB.quad_J(L=0.80, hs=0.82, hh=0.78, wf=0.17, wh=0.16, neck=(0.22, -0.10), head=0.34, fleg=(0.78, 0.52, 0.22, 0.06),
                  hleg=(0.80, 0.52, 0.28, 0.06), tail=(0.30, 2, -1.6), bend=1.3, paw=1.4)
    J['snout'] = V(0, -0.94, 0.50)
    J['jawh'] = V(0, -0.66, 0.60)
    J['chin'] = V(0, -0.92, 0.44)
    J['ear'] = (V(0.13, -0.61, 0.73), V(0.25, -0.58, 0.68))
    HL = V(0.30, 0.0, 0.36)                       # hinge of the flank skirts (the ropes are rooted along it)
    HF = V(0, -0.52, 0.40)
    HB = V(0, 0.64, 0.38)

    def extra(sk):
        sk.lr('skirt', HL, HL + V(0.05, 0, -0.30), 'spine2')
        sk.bone('skirt_f', HF, HF + V(0, -0.05, -0.30), 'chest')
        sk.bone('skirt_b', HB, HB + V(0, 0.05, -0.30), 'hip')
    C = B.Beast('mon_dusk_yak', RB.quad_skel(J, extra=extra))
    std_meta(C, 'dusk', 1.5, 3.0)
    Q = RB.Quad(C, J, glide=(0.08, 0.06))
    C.look('coat', 'bfur', 'hair_coat', color=(0.085, 0.07, 0.06), tip=(0.23, 0.20, 0.17), flow=(0, 0.1, -1.0), scale=45.0, lock=16.0,
           frost=0.55, rough=0.92)
    C.look('horn', 'bone', 'bone_horn', color=(0.30, 0.27, 0.22), stain=(0.10, 0.08, 0.06), dirt_lo=-2.0, dirt_hi=-1.5, crack_amt=0.8, crack=40.0)
    C.look('hoof', 'bone', 'bone_horn', color=(0.06, 0.055, 0.05), stain=(0.03, 0.025, 0.02), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('ice', 'ice', 'rock_ice', color=(0.55, 0.70, 0.80), deep=(0.16, 0.30, 0.42), frost_amt=0.8)
    C.look('snow', 'ice', 'rock_ice', color=(0.80, 0.85, 0.90), deep=(0.55, 0.65, 0.75), frost_amt=1.0, rough=0.6, crack=6.0)
    S = dict(
        torso=[(0.66, 0.50, 0.14, 0.14), (0.52, 0.56, 0.36, 0.30), (0.25, 0.60, 0.41, 0.37), (-0.05, 0.62, 0.43, 0.39),
               (-0.32, 0.65, 0.44, 0.38), (-0.50, 0.66, 0.36, 0.30), (-0.60, 0.68, 0.22, 0.20)],
        neck=[(-0.40, 0.78, 0.30, 0.25), (-0.50, 0.75, 0.24, 0.21), (-0.60, 0.72, 0.20, 0.17)],
        head=[P.tubep([(0, -0.56, 0.74), (0, -0.68, 0.69), (0, -0.82, 0.60), (0, -0.93, 0.515)], [0.17, 0.16, 0.12, 0.09],
                      [0.15, 0.14, 0.105, 0.09], m=9, n=12),
              P.blob((0, -0.95, 0.50), 0.085, 0.045, 0.07)],
        fleg=[(0.11, 0.10), (0.10, 0.09), (0.08, 0.075), (0.065, 0.06), (0.065, 0.065)],
        hleg=[(0.11, 0.10), (0.10, 0.09), (0.08, 0.075), (0.065, 0.06), (0.065, 0.065)],
        fpaw=(0.07, 0.08, 0.04), hpaw=(0.066, 0.076, 0.04),
        voxel=0.02, tris=4600, torso_power=2.3, smooth=6)
    body = RB.quad_flesh(C, J, S)
    C.skin(body, 'coat')
    bvh = P.bvh_of(body)
    SP = ['hip', 'spine1', 'spine2', 'chest', 'neck1', 'neck2', 'head']
    RB.add_tail(C, J, 0.04, 0.03, 'coat', n=6)
    jaw = P.tubep([(0, -0.66, 0.60), (0, -0.80, 0.515), (0, -0.925, 0.445)], [0.07, 0.055, 0.04], [0.11, 0.09, 0.07], m=7, n=10)
    C.rigid(jaw, 'jaw', 'coat', smooth=180)
    for sg in (1, -1):
        hn = H.round_horn([(sg * 0.10, -0.64, 0.80), (sg * 0.30, -0.64, 0.845), (sg * 0.44, -0.70, 0.77), (sg * 0.475, -0.84, 0.59),
                           (sg * 0.43, -0.98, 0.45), (sg * 0.37, -1.07, 0.405)], 0.062, m=12, n=8, power=0.8, tip=0.006)
        C.rigid(hn, 'head', 'horn', smooth=60)
        e = RB.ear_sheet(J['ear'][0] * (sg, 1, 1), J['ear'][1] * (sg, 1, 1), 0.05, cup=0.02, th=0.01, sink=0.04, rows=4, cols=4, face=(0, -0.6, -0.8))
        C.rigid(e, 'ear_l' if sg > 0 else 'ear_r', 'coat', smooth=180)
    H.hooves(C, J, 'hoof', rf=(0.075, 0.085), rh=(0.07, 0.08), h=0.07)
    # matted locks over the whole coat (they lie on the body and move with the spine only)
    def flank(c, n):
        return (c[:, 2] > 0.48) & (c[:, 2] < 1.0) & (np.abs(n[:, 2]) < 0.8) & (c[:, 1] > -0.62)

    def face(c, n):
        return (c[:, 1] < -0.56) & (c[:, 2] > 0.70) & (n[:, 2] > -0.2)
    tf = P.tufts(body, 230, length=(0.16, 0.30), radius=(0.035, 0.06), seed=3, mask=flank, flow=(0, 0.05, -1.0), lay=0.9, sink=0.25,
                 sides=4, jitter=0.15, droop=0.25)
    C.skin(H.keep_bones(tf, SP, 'spine2'), 'coat', smooth=180)
    fl = P.tufts(body, 36, length=(0.10, 0.20), radius=(0.03, 0.045), seed=8, mask=face, flow=(0, -0.5, -1.0), lay=0.75, sink=0.3,
                 sides=4, jitter=0.2, droop=0.3)
    C.skin(H.keep_bones(fl, SP, 'head'), 'coat', smooth=180)
    # the fringe: ropes with icy ends, rigid to the four skirt bones (they sway, lag and splay when it falls)
    rng = np.random.default_rng(5)

    def fringe(root, tip, bone, i):
        r0 = rng.uniform(0.045, 0.06)
        bw = rng.uniform(-0.02, 0.02)
        C.rigid(H.rope(root, tip, r0, r0 * 0.25, sides=5, segs=3, bow=bw, seed=i, t0=0.0, t1=0.66), bone, 'coat', smooth=60)
        C.rigid(H.rope(root, tip, r0 * 1.04, r0 * 0.2, sides=5, segs=2, bow=bw, seed=i, t0=0.6, t1=1.0), bone, 'ice', smooth=60)
    k = 0
    for sg in (1, -1):
        for y in np.linspace(-0.47, 0.58, 15):
            xs = H.surf_side(bvh, y, HL[2], sg, default=sg * 0.3)
            root = V(xs - sg * 0.05, y, HL[2] + rng.uniform(-0.02, 0.03))
            tip = V(xs + sg * rng.uniform(0.02, 0.09), y + rng.uniform(-0.03, 0.03), rng.uniform(0.075, 0.115))
            fringe(root, tip, 'skirt_l' if sg > 0 else 'skirt_r', k); k += 1
    for x in np.linspace(-0.24, 0.24, 7):
        q, _ = P.surf(bvh, (x, -2.0, HF[2]), (0, 1, 0), dist=4.0)
        yf = float(q[1]) if q is not None else -0.55
        fringe(V(x, yf + 0.05, HF[2] + rng.uniform(-0.02, 0.03)), V(x * 1.1, yf - rng.uniform(0.0, 0.05), rng.uniform(0.10, 0.14)), 'skirt_f', k); k += 1
        q, _ = P.surf(bvh, (x, 2.0, HB[2]), (0, -1, 0), dist=4.0)
        yb = float(q[1]) if q is not None else 0.66
        fringe(V(x, yb - 0.05, HB[2] + rng.uniform(-0.02, 0.03)), V(x * 1.1, yb + rng.uniform(0.0, 0.05), rng.uniform(0.10, 0.14)), 'skirt_b', k); k += 1
    # the snow drift on its back
    zt = surf_top(bvh, 0.0, 0.0)
    snow = P.blob((0, 0.03, zt - 0.03), 0.31, 0.52, 0.10, n=16, m=8)
    snow.v[:, 2] -= 1.35 * snow.v[:, 0] ** 2 + 0.25 * np.maximum(0, np.abs(snow.v[:, 1] - 0.03) - 0.3) ** 2 * 6
    snow.v[:, 0] *= 1.0 + 0.18 * np.sin(snow.v[:, 1] * 9.0)
    snow.rough(0.02, 5.0, seed=2.0)
    B.wbones(snow, ['hip', 'spine1', 'spine2', 'chest'], sharp=3.0)
    C.skin(snow, 'snow', smooth=180)
    horn_tip = V(0.37, -1.07, 0.405)

    def skirt(sway=0.0, n=1, lag=0.0, splay=None, rear=None, flare=None):
        def fn(p, u):
            a = sway * osc(u, n, 0.15)
            s = splay(u) if splay else 0.0
            r = rear(u) if rear else 0.0
            fl = flare(u) if flare else 0.0                    # the coat flies out when it lands / swings its head
            p.rot('skirt_l', y=-a - 72 * s - 6 * r - 14 * fl)
            p.rot('skirt_r', y=-a + 72 * s + 6 * r + 14 * fl)
            p.rot('skirt_f', x=lag + sway * 0.6 * osc(u, n * 2, 0.3) - 68 * s + 8 * r - 30 * fl)
            p.rot('skirt_b', x=lag + sway * 0.6 * osc(u, n * 2, 0.5) + 68 * s + 34 * r + 10 * fl)
        return fn
    Q.idle(dur=4.0, breath=0.012, look=5.0, tail=4.0, ear=8.0, extra=skirt(1.2, 1))
    Q.gait('Walk', 0.7, 1.0, 0.64, RB.WALK4, lift=0.06, bob=0.012, crouch=0.02, nod=3.0, tail=4.0, tuck=25.0, roll=2.5, yaw=1.5,
           extra=skirt(3.5, 1, lag=3.0))
    Q.gait('Run', 3.0 / 1.5, 0.6, 0.42, RB.TROT, lift=0.10, bob=0.03, bobn=2, bobph=0.85, crouch=0.03, pitch=1.5, nod=4.0, tail=4.0,
           tuck=45.0, roll=2.0, yaw=1.0, jaw=5.0, extra=skirt(6.0, 1, lag=8.0))
    Q.sweep(dur=1.1, hit=0.55, arc=38.0, low=8.0, reach=0.2, drop=0.01, extra=skirt(3.0, 2, flare=lambda u: 0.6 * bump(u, 0.3, 0.5, 0.6, 0.85)))
    C.reach('Attack1', 'head', horn_tip)
    hu = 0.8 / 1.4
    Q.rear(name='Attack2', dur=1.4, hit=0.8, pitch=18.0, fwd=0.2, slam=0.035, headup=10.0, dip=1.0,
           extra=skirt(2.0, 3, rear=lambda u: bump(u, 0.04, hu * 0.55, hu * 0.82, hu + 0.05),
                       flare=lambda u: bump(u, hu - 0.03, hu + 0.02, hu + 0.1, hu + 0.3)))
    Q.hit(roll=4.0, drop=0.015, extra=skirt(4.0, 2))
    Q.death_belly(dur=2.0, roll=6.0, head_max=50.0, fold=dict(f=(35, -110, 10), h=(-70, 140, -90)),
                  extra=skirt(0.0, 1, splay=lambda u: sm(u, 0.12, 0.55)))
    Q.spawn_rise(dur=1.5, depth=1.25, extra=skirt(5.0, 5))
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2: 4 skirt bones carry the fringe of the coat")


# ================================================================================================ rime_packbeast
@reg
def rime_packbeast(bake=True):
    """Frozen Pack-mule: a dead mule still walking its route, head low, under a pack-saddle with two cracked kegs of
    frozen lamp-oil (blue-white light inside) and a rack of spears standing up like a comb; icicles on belly and jaw"""
    J = RB.quad_J(L=0.82, hs=1.02, hh=1.02, wf=0.11, wh=0.11, neck=(0.39, -0.22), head=0.38, sag=-0.02, fleg=(0.80, 0.56, 0.24, 0.05),
                  hleg=(0.84, 0.56, 0.32, 0.05), tail=(0.22, 3, -2.4), bend=1.5, paw=1.3)
    hj = J['hj']
    J['snout'] = V(0, -0.955, 0.53)
    J['jawh'] = V(0, -0.785, 0.77)
    J['chin'] = V(0, -0.895, 0.555)
    J['ear'] = (hj + V(0.055, 0.03, 0.065), hj + V(0.21, 0.13, 0.04))

    def extra(sk):
        sk.bone('load', (0, 0.0, 1.14), (0, 0.0, 1.40), 'spine2')
    C = B.Beast('mon_rime_packbeast', RB.quad_skel(J, extra=extra))
    std_meta(C, 'frost', 1.15, 3.4)
    Q = RB.Quad(C, J, glide=(0.08, 0.06))
    C.look('hide', 'bfur', 'hair_hide', color=(0.15, 0.135, 0.125), tip=(0.30, 0.28, 0.26), flow=(0, 0.6, -0.7), scale=90.0, frost=0.75,
           rough=0.85)
    C.look('mane', 'bfur', 'hair_hide', color=(0.05, 0.045, 0.04), tip=(0.16, 0.15, 0.14), flow=(0, 0.3, 1.0), scale=80.0, frost=0.9)
    C.look('hoof', 'bone', 'hair_hide', color=(0.07, 0.06, 0.055), stain=(0.03, 0.025, 0.02), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('ice', 'ice', 'hair_hide', color=(0.55, 0.72, 0.82), deep=(0.14, 0.28, 0.42), frost_amt=0.7)
    C.look('pad', 'leather', 'leather_pack', color=(0.10, 0.07, 0.048), scuff_color=(0.26, 0.22, 0.18), rough=0.6, scuff=0.6)
    C.look('wood', 'wood', 'leather_pack', color=(0.14, 0.10, 0.065), axis=(1, 0, 0.6))
    C.look('shaft', 'wood', 'leather_pack', color=(0.12, 0.085, 0.055), axis=(0, 0, 1))
    C.look('iron', 'metal', 'leather_pack', color=(0.16, 0.155, 0.15), rust=(0.20, 0.09, 0.03), amount=0.6, rough=0.5, edge_color=(0.42, 0.42, 0.42))
    C.look('keg', 'crackwood', 'emis_keg', color=(0.13, 0.09, 0.06), axis=(0, 1, 0), emit=(0.32, 0.62, 1.0), strength=1.6, crack=7.0, crack_w=0.016,
           halo=0.15, patch=3.0)
    C.look('core', 'emis', 'emis_keg', emit=(0.32, 0.62, 1.0), strength=1.6, mottle=0.9, base_mul=0.6, scale=12.0, crust=0.6)

    def gaunt(body):
        v = body.v
        n = body.normals()
        m = (np.clip((np.abs(v[:, 0]) - 0.08) / 0.04, 0, 1) * np.clip((v[:, 1] + 0.34) / 0.06, 0, 1) * np.clip((0.14 - v[:, 1]) / 0.07, 0, 1)
             * np.clip((v[:, 2] - 0.72) / 0.05, 0, 1) * np.clip((1.0 - v[:, 2]) / 0.06, 0, 1))
        ph = (v[:, 1] + 0.30 * (v[:, 2] - 0.85)) / 0.075
        body.v = v + n * (0.009 * m * np.maximum(0, np.cos(2 * np.pi * ph)) ** 1.5 - 0.003 * m)[:, None]
        body.dent((0.14, 0.26, 0.93), 0.09, -0.02)           # hollow flanks, hip bones showing
        body.dent((-0.14, 0.26, 0.93), 0.09, -0.02)
        body.dent((0.065, -0.85, 0.78), 0.04, -0.012)        # sunken eye sockets
        body.dent((-0.065, -0.85, 0.78), 0.04, -0.012)

    S = dict(
        torso=[(0.52, 0.94, 0.07, 0.06), (0.42, 0.93, 0.21, 0.155), (0.15, 0.88, 0.215, 0.175), (-0.15, 0.88, 0.235, 0.18),
               (-0.36, 0.92, 0.245, 0.165), (-0.48, 0.95, 0.17, 0.12), (-0.55, 0.98, 0.10, 0.08)],
        neck=[(-0.42, 0.99, 0.155, 0.09), (-0.60, 0.905, 0.10, 0.068), (-0.75, 0.835, 0.088, 0.066)],
        head=[P.tubep([(0, -0.765, 0.855), (0, -0.825, 0.76), (0, -0.885, 0.65), (0, -0.94, 0.55)], [0.10, 0.105, 0.078, 0.06],
                      [0.078, 0.08, 0.06, 0.054], m=9, n=12),
              P.blob((0, -0.79, 0.735), 0.066, 0.075, 0.09),                                   # the deep jowl
              P.blob((0, -0.95, 0.535), 0.058, 0.05, 0.052)],
        fleg=[(0.10, 0.06), (0.10, 0.065), (0.06, 0.05), (0.036, 0.034), (0.038, 0.038)],
        hleg=[(0.13, 0.07), (0.13, 0.075), (0.07, 0.055), (0.038, 0.035), (0.038, 0.038)],
        fpaw=(0.04, 0.05, 0.035), hpaw=(0.04, 0.05, 0.035),
        blobs=[((0.10, 0.36, 0.86), (0.07, 0.14, 0.16), ['hip', 'h_upper_l'], True),
               ((0.10, -0.40, 0.88), (0.05, 0.10, 0.15), ['chest', 'f_upper_l'], True)],
        voxel=0.016, tris=4800, sculpt=gaunt)
    body = RB.quad_flesh(C, J, S)
    C.skin(body, 'hide')
    bvh = P.bvh_of(body)
    RB.add_tail(C, J, 0.03, 0.012, 'mane', n=6)
    jaw = P.tubep([(0, -0.77, 0.785), (0, -0.82, 0.675), (0, -0.885, 0.57)], [0.036, 0.032, 0.026], [0.052, 0.048, 0.042], m=7, n=10)
    C.rigid(jaw, 'jaw', 'hide', smooth=180)
    for sg in (1, -1):
        e = RB.ear_sheet(J['ear'][0] * (sg, 1, 1), J['ear'][1] * (sg, 1, 1), 0.04, cup=0.018, th=0.008, sink=0.03, rows=5, cols=4, face=(0, -1, 0.3))
        C.rigid(e, 'ear_l' if sg > 0 else 'ear_r', 'hide', smooth=180)
    H.hooves(C, J, 'hoof', rf=(0.045, 0.055), rh=(0.045, 0.055), h=0.06, cloven=False)

    def crest(c, n):
        return (n[:, 2] > 0.5) & (np.abs(c[:, 0]) < 0.04) & (c[:, 1] < -0.42) & (c[:, 1] > -0.75)
    mn = P.tufts(body, 46, length=(0.05, 0.09), radius=(0.012, 0.02), seed=4, mask=crest, flow=(0, 0.6, 1.0), lay=0.35, sink=0.3, sides=3, jitter=0.3)
    if mn is not None:
        C.skin(mn, 'mane', smooth=180)
    # ---- the load (everything rigid to the bone 'load')
    pad = P.tubep([(0, -0.29, 0.90), (0, 0.0, 0.885), (0, 0.29, 0.90)], [0.238, 0.238, 0.238], [0.205, 0.205, 0.205], m=8, n=16, power=2.6)
    pad.v[:, 2] = np.maximum(pad.v[:, 2], 0.86)
    C.rigid(pad, 'load', 'pad', smooth=40)
    trees = []
    for y in (-0.2, 0.2):
        for sg in (1, -1):
            trees.append(bar((-sg * 0.21, y, 1.03), (sg * 0.11, y, 1.31), 0.045, 0.035, up=(0, 1, 0)))
    trees.append(bar((0, -0.27, 1.245), (0, 0.27, 1.245), 0.05, 0.04))
    C.rigid(M.join(trees), 'load', 'wood', smooth=25)
    KR, KL = 0.165, 0.50
    for sg in (1, -1):
        kc = V(sg * (0.205 + KR), 0.0, 0.93)
        wood, hoops, core = H.keg(kc, KR, KL, seed=3 + sg, missing=(1, 4) if sg > 0 else (8, 10))
        C.rigid(wood, 'load', 'keg')
        C.rigid(hoops, 'load', 'iron')
        C.rigid(core, 'load', 'core')
        ties = [H.rope((sg * 0.20, y, 1.07), kc + V(sg * 0.02, y, KR + 0.01), 0.014, 0.014, sides=4, segs=2) for y in (-0.2, 0.2)]
        C.rigid(M.join(ties), 'load', 'pad', smooth=60)
        ic = [P.cone(kc + V(rng_x, yy, -KR + 0.02), (0, 0, -1), ln, 0.016, n=5)
              for rng_x, yy, ln in ((0.03, -0.17, 0.13), (-0.02, -0.05, 0.19), (0.04, 0.08, 0.10), (-0.03, 0.18, 0.15))]
        C.rigid(M.join(ic), 'load', 'ice', smooth=50)
    sh = []; bl = []
    rng = np.random.default_rng(9)
    for i, y in enumerate(np.linspace(-0.24, 0.24, 7)):
        top = V(rng.uniform(-0.03, 0.03), y + rng.uniform(-0.03, 0.03), rng.uniform(1.84, 2.0))
        a, b = H.spear((rng.uniform(-0.015, 0.015), y, 1.05), top, r=0.014, head=0.21, hw=0.034)
        sh.append(a); bl.append(b)
    C.rigid(M.join(sh), 'load', 'shaft', smooth=60)
    C.rigid(M.join(bl), 'load', 'iron', smooth=25)
    C.rigid(M.join([bar((-0.03, -0.27, 1.50), (-0.03, 0.27, 1.50), 0.02, 0.03), bar((0.03, -0.27, 1.50), (0.03, 0.27, 1.50), 0.02, 0.03)]),
            'load', 'pad', smooth=25)
    # ---- icicles on belly and jaw
    for i, (x, y, ln) in enumerate(((0.05, -0.30, 0.10), (-0.06, -0.22, 0.15), (0.02, -0.12, 0.20), (-0.04, -0.02, 0.12), (0.07, 0.06, 0.17),
                                    (-0.02, 0.13, 0.22), (0.04, 0.19, 0.11), (-0.06, -0.22, 0.14))):
        zb = surf_bottom(bvh, x, y, default=0.66)
        C.rigid(P.cone((x, y, zb + 0.05), (0.0, 0.05, -1.0), ln + 0.03, 0.02, n=5), H.host_bone(body, (x, y, zb), among=('spine1', 'spine2', 'chest')), 'ice', smooth=50)
    C.rigid(M.join([P.cone((x, y, z), (0, 0.05, -1), ln, 0.011, n=5) for x, y, z, ln in ((0.02, -0.80, 0.70, 0.09), (-0.02, -0.845, 0.63, 0.12),
                                                                                     (0.015, -0.88, 0.57, 0.07))]), 'jaw', 'ice', smooth=50)

    def load(a=1.0, b=1.0, n=1):
        def fn(p, u):
            p.rot('load', x=a * osc(u, 2 * n, 0.2), y=b * osc(u, n, 0.1))
        return fn
    Q.idle(dur=4.0, breath=0.006, look=5.0, tail=5.0, ear=6.0, extra=load(0.3, 0.5))
    Q.gait('Walk', 1.0, 0.9, 0.62, RB.WALK4, lift=0.07, bob=0.012, crouch=0.02, nod=4.0, tail=5.0, tuck=35.0, roll=1.5, yaw=1.5,
           extra=load(1.2, 2.0))
    Q.gait('Run', 3.4 / 1.15, 0.5, 0.4, RB.TROT, lift=0.12, bob=0.03, bobn=2, bobph=0.85, crouch=0.03, pitch=1.5, nod=5.0, tail=4.0,
           tuck=60.0, roll=1.2, yaw=1.0, ears=-10.0, jaw=5.0, extra=load(2.5, 3.0))
    Q.rear(name='Attack1', dur=1.0, hit=0.52, pitch=17.0, fwd=0.3, strike='l', headup=12.0, gape=14.0, extra=load(2.0, 2.0))
    Q.charge(dur=1.3, hit=0.8, fwd=0.7, low=16.0, toss=12.0, gape=12.0, extra=load(2.5, 2.0, 2))
    Q.hit(extra=load(3.0, 4.0))
    Q.death_belly(dur=1.9, roll=7.0, fold=dict(f=(35, -110, 10), h=(-70, 140, -90)),
                  extra=lambda p, u: p.rot('load', y=5 * sm(u, 0.4, 0.7), x=3 * bump(u, 0.4, 0.5, 0.55, 0.75)))
    Q.spawn_stand(dur=1.6, shake=4.0, extra=load(1.0, 2.0, 2))
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2: the whole load is rigid to the bone 'load'")


# ================================================================================================ fiend_maw
@reg
def fiend_maw(bake=True):
    """Furnace-maw: a four-legged slab of basalt whose whole front is a firebox behind a door that swings open
    sideways; no eyes, heavy fore legs, small hind legs, a row of chimney stubs down the spine"""
    J = RB.quad_J(L=0.66, hs=0.73, hh=0.50, wf=0.34, wh=0.17, neck=(0.12, 0.0), head=0.20, fleg=(0.80, 0.5, 0.2, 0.07),
                  hleg=(0.80, 0.5, 0.28, 0.08), tail=(0.1, 1, -1.0), bend=1.2, paw=1.3)
    J['tail'] = None
    YD = -0.715                                    # plane of the door
    ZD = 0.645                                     # middle of the door / the firebox
    J['snout'] = V(0, -0.72, ZD)
    J['jawh'] = V(0.285, YD, ZD)                    # the hinge: at its left, vertical
    J['chin'] = V(-0.28, YD, ZD)
    C = B.Beast('mon_fiend_maw', RB.quad_skel(J, ears=False))
    std_meta(C, 'fiend', 1.3, 3.8)
    Q = RB.Quad(C, J, glide=(0.08, 0.05))
    Q.jaw_axis = 'z'
    C.look('basalt', 'rock', 'rock_basalt', color=(0.045, 0.04, 0.04), ash=(0.18, 0.17, 0.16), ash_amt=0.5, scale=11.0, crack_w=0.05,
           heat=(1.0, 0.32, 0.04), heat_amt=0.7)
    C.look('iron', 'metal', 'rock_basalt', color=(0.05, 0.05, 0.055), rust=(0.14, 0.07, 0.035), rust2=(0.04, 0.03, 0.025), amount=0.5,
           rough=0.5, edge_color=(0.30, 0.30, 0.32))
    C.look('lava_core', 'emis', 'emis_maw', emit=(1.0, 0.36, 0.05), strength=2.2, mottle=0.8, base_mul=0.5, scale=14.0)

    def crag(body):
        body.rough(0.014, 4.5, seed=3.0, octaves=3)

    S = dict(
        torso=[(0.52, 0.43, 0.10, 0.12), (0.42, 0.44, 0.19, 0.20), (0.15, 0.51, 0.26, 0.27), (-0.15, 0.59, 0.34, 0.33),
               (-0.40, 0.645, 0.40, 0.36), (-0.56, 0.655, 0.41, 0.37)],
        neck=[(-0.50, 0.655, 0.41, 0.37), (-0.68, 0.655, 0.41, 0.37)],
        fleg=[(0.16, 0.13), (0.155, 0.125), (0.125, 0.105), (0.095, 0.09), (0.09, 0.09)],
        blobs=[((0.33, -0.36, 0.60), (0.10, 0.16, 0.20), ['chest', 'f_upper_l'], True)],
        hleg=[(0.10, 0.08), (0.095, 0.075), (0.07, 0.06), (0.05, 0.045), (0.05, 0.05)],
        fpaw=(0.10, 0.11, 0.055), hpaw=(0.06, 0.07, 0.04),
        voxel=0.018, tris=5000, torso_power=4.0, neck_power=4.0, leg_power=3.0, smooth=4, sculpt=crag)
    body = RB.quad_flesh(C, J, S)
    cav = M.box(0.48, 0.44, 0.54).move(0, -0.58, ZD)                             # the firebox
    cut = H.clean_mesh(M.boolean(body, cav, 'DIFFERENCE'))
    M.transfer_weights(cut, body, smooth=1)
    cut.smooth = 50.0
    C.skin(cut, 'basalt')
    bvh = P.bvh_of(cut)
    core = M.box(0.474, 0.20, 0.534).move(0, -0.465, ZD)                          # the fire at the back of it
    C.rigid(core, 'head', 'lava_core', smooth=0)
    rng = np.random.default_rng(4)
    for i in range(6):                                                           # clinker lying in front of the fire
        s = rng.uniform(0.05, 0.085)
        rk = M.rock(s * 1.2, s, s * 0.9, seed=30 + i, n=12, jit=0.25).move(rng.uniform(-0.17, 0.17), rng.uniform(-0.62, -0.57), ZD - 0.27 + s * 0.5)
        C.rigid(rk, 'head', 'lava_core' if i % 3 == 0 else 'basalt', smooth=0)
    # the door (bone 'jaw', hinged at the creature's left) with iron bands, glowing slits and a latch
    door = M.box(0.565, 0.06, 0.585, bevel=0.012).move(0, YD, ZD)
    door.jitter(0.004, seed=2)
    C.rigid(door, 'jaw', 'basalt', smooth=20)
    C.rigid(M.join([M.box(0.585, 0.022, 0.055).move(0, YD - 0.036, ZD + z) for z in (-0.17, 0.17)] +
                   [M.box(0.05, 0.03, 0.10).move(-0.25, YD - 0.04, ZD)]), 'jaw', 'iron', smooth=25)
    C.rigid(M.join([M.box(0.30, 0.014, 0.024).move(-0.02, YD - 0.031, ZD + z) for z in (-0.07, 0.0, 0.07)]), 'jaw', 'lava_core', smooth=0)
    C.rigid(M.tube(np.array([(0.285, YD, ZD - 0.27), (0.285, YD, ZD + 0.27)]), 0.034, n=8, up=(0, 1, 0)), 'jaw', 'iron', smooth=60)
    C.rigid(M.join([bar((0.30, -0.56, ZD + z), (0.30, YD + 0.005, ZD + z), 0.07, 0.05, up=(1, 0, 0)) for z in (-0.19, 0.19)]), 'head', 'iron')
    # chimney stubs down the spine
    for i, (y, r, h) in enumerate(((-0.46, 0.068, 0.17), (-0.24, 0.064, 0.16), (-0.02, 0.058, 0.14), (0.20, 0.05, 0.12), (0.38, 0.042, 0.10))):
        zt = surf_top(bvh, 0.0, y, default=0.8)
        bone = 'chest' if y < -0.11 else ('spine2' if y < 0.106 else ('spine1' if y < 0.33 else 'hip'))
        st = M.loft_z([(zt - 0.06, 0, y, r * 1.12, r * 1.12), (zt + h * 0.5, 0, y + 0.012, r, r), (zt + h, 0, y + 0.025, r * 0.94, r * 0.94)], n=10)
        st.rough(0.006, 14.0, seed=float(i))
        C.rigid(st, bone, 'basalt', smooth=35)
        C.rigid(P.ring((0, y + 0.025, zt + h), r * 0.94, 0.016, axis=(0, 0, 1), n=10, m=4), bone, 'basalt', smooth=50)
        C.rigid(M.loft_z([(zt + h - 0.03, 0, y + 0.025, r * 0.78, r * 0.78), (zt + h + 0.004, 0, y + 0.025, r * 0.78, r * 0.78)], n=10),
                bone, 'lava_core', smooth=0)

    def door_idle(p, u):
        p.rot('jaw', z=4 + 4 * osc(u, 2, 0.3))
    Q.idle(dur=3.6, breath=0.008, look=4.0, tail=0.0, ear=0.0, extra=door_idle)
    Q.gait('Walk', 0.8, 0.7, 0.64, RB.WALK4, lift=0.05, bob=0.012, crouch=0.02, nod=1.5, tuck=22.0, roll=2.2, yaw=1.5, jaw=5.0)
    Q.gait('Run', 3.8 / 1.3, 0.4, 0.36, RB.TROT, lift=0.09, bob=0.025, bobn=2, bobph=0.85, crouch=0.035, pitch=1.5, nod=2.5, tuck=45.0,
           roll=1.6, yaw=1.0, jaw=16.0)

    def belch(p, u):
        """the door is thrown open, the slab leans into the blast, the door slams"""
        hu = 0.5 / 1.1
        w = bump(u, 0.0, hu * 0.5, hu * 0.7, hu)
        s = bump(u, hu * 0.7, hu, hu + 0.2, 0.95)
        Q.body(p, dy=0.06 * w - 0.13 * s, dz=-0.03 * w, pitch=-4 * w + 3 * s)
        Q.spine(p, flex=4 * w - 3 * s)
        Q.neck(p, pitch=-5 * w + 3 * s)
        Q.jaw(p, 112 * bump(u, hu * 0.3, hu * 0.85, hu + 0.22, hu + 0.4) + 5 * osc(u, 9) * s)
        Q.plant(p)
    C.clip('Attack1', 1.1, belch, hit=0.5)
    Q.reach('Attack1')
    Q.charge(dur=1.3, hit=0.8, fwd=0.6, low=6.0, toss=8.0, gape=40.0, paw=None)
    Q.hit(roll=5.0)
    Q.death_belly(dur=1.8, roll=4.0, head=0.0, hyaw=3.0, jaw=104.0)
    Q.spawn_rise(dur=1.3)
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2: door = bone 'jaw' turning about Z (Quad.jaw_axis)")


# ================================================================================================ bog_toad
@reg
def bog_toad(bake=True):
    """Hearth-toad: a cart-wheel of a toad, wide flat warty bun over a translucent glowing belly sack, tiny eyes on
    top, a reed harness with a clay hearth-pot on its back; the tongue is three bones"""
    J = RB.sprawl_J(L=0.36, h=0.26, wf=0.30, wh=0.30, rf=0.16, rh=0.14, neck=(0.10, -0.02), head=0.22, tail=(0, 0, 0), up=0.02)
    J['f'] = [V(0.30, -0.18, 0.20), V(0.41, -0.12, 0.17), V(0.44, -0.22, 0.06), V(0.45, -0.26, 0.02), V(0.44, -0.33, 0.012)]
    J['f_c'] = V(0.45, -0.28, 0.0)
    J['h'] = [V(0.30, 0.18, 0.20), V(0.50, -0.02, 0.16), V(0.46, 0.22, 0.06), V(0.50, 0.06, 0.02), V(0.53, -0.06, 0.012)]
    J['h_c'] = V(0.51, 0.02, 0.0)
    J['snout'] = V(0, -0.50, 0.215)
    J['jawh'] = V(0, -0.29, 0.18)
    J['chin'] = V(0, -0.50, 0.15)
    TZ = 0.193
    tp = [V(0, -0.30, TZ), V(0, -0.36, TZ), V(0, -0.42, TZ), V(0, -0.44, TZ)]
    BH = V(0, 0.0, 0.22)

    def extra(sk):
        sk.bone('belly', BH, BH + V(0, 0, -0.17), 'spine1')
        sk.bone('pot', (0, 0.02, 0.49), (0, 0.02, 0.70), 'spine2')
        sk.chain('tongue', tp, 'head')
    C = B.Beast('mon_bog_toad', RB.quad_skel(J, ears=False, extra=extra))
    std_meta(C, 'mire', 1.2, 2.6)
    Q = RB.Quad(C, J, glide=(0.05, 0.05))
    C.look('skin', 'slime', 'skin_toad', color=(0.10, 0.115, 0.065), color2=(0.04, 0.05, 0.03), ring=0.0, wart=42.0, wart_h=2.4, rough=0.3,
           dry=0.6, scale=4.0)
    C.look('chin', 'slime', 'skin_toad', color=(0.26, 0.27, 0.16), color2=(0.14, 0.15, 0.08), ring=0.0, wart=80.0, rough=0.25)
    C.look('maw', 'slime', 'skin_toad', color=(0.30, 0.10, 0.09), color2=(0.14, 0.04, 0.04), ring=0.0, rough=0.12, wart=120.0)
    C.look('eye', 'glass', 'skin_toad', color=(0.01, 0.01, 0.008), rough=0.08)
    C.look('reed', 'cloth', 'skin_toad', color=(0.30, 0.25, 0.12), weave='burlap', dirt=(0.10, 0.09, 0.05), fold=30.0, rough=0.85)
    C.look('clay', 'bone', 'skin_toad', color=(0.33, 0.19, 0.12), stain=(0.10, 0.07, 0.055), crack_amt=0.9, dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('belly', 'glow', 'emis_belly', color=(0.24, 0.24, 0.10), emit=(0.68, 0.88, 0.16), strength=1.5, vein=(0.07, 0.05, 0.02), vein_scale=9.0,
           scale=5.0)
    S = dict(
        torso=[(0.42, 0.20, 0.10, 0.20), (0.30, 0.24, 0.20, 0.40), (0.05, 0.27, 0.235, 0.47), (-0.18, 0.26, 0.21, 0.44), (-0.34, 0.24, 0.16, 0.34)],
        neck=[(-0.24, 0.25, 0.19, 0.40), (-0.32, 0.245, 0.15, 0.34)],
        head=[P.tubep([(0, -0.30, 0.285), (0, -0.42, 0.265), (0, -0.505, 0.235)], [0.10, 0.075, 0.045], [0.30, 0.24, 0.16], m=8, n=14, power=2.3),
              P.blob((0.14, -0.36, 0.365), 0.046, 0.05, 0.04), P.blob((-0.14, -0.36, 0.365), 0.046, 0.05, 0.04)],
        fleg=[(0.07, 0.06), (0.06, 0.055), (0.045, 0.04), (0.035, 0.03), (0.03, 0.03)],
        hleg=[(0.10, 0.08), (0.085, 0.07), (0.06, 0.05), (0.045, 0.035), (0.035, 0.03)],
        f_root=(-0.10, 0.02, 0.02), h_root=(-0.10, -0.02, 0.02),
        fpaw=(0.06, 0.07, 0.016), hpaw=(0.075, 0.10, 0.016),
        blobs=[((0.20, 0.15, 0.44), (0.05, 0.05, 0.035), ['spine1'], True), ((0.28, -0.05, 0.40), (0.045, 0.045, 0.03), ['spine2'], True),
               ((0.12, -0.20, 0.42), (0.04, 0.04, 0.03), ['chest'], True), ((0.10, 0.30, 0.40), (0.045, 0.045, 0.03), ['hip'], True),
               ((0.33, 0.14, 0.33), (0.04, 0.04, 0.03), ['spine1'], True)],
        voxel=0.014, tris=5000, torso_power=2.2)
    body = RB.quad_flesh(C, J, S)
    C.skin(body, 'skin')
    bvh = P.bvh_of(body)
    sack = P.blob((0, 0.07, 0.165), 0.50, 0.42, 0.14, n=18, m=9)
    C.rigid(sack, 'belly', 'belly', smooth=180)
    jaw = P.tubep([(0, -0.29, 0.15), (0, -0.42, 0.145), (0, -0.512, 0.15)], [0.055, 0.05, 0.036], [0.285, 0.232, 0.156], m=7, n=14, power=2.3)
    C.rigid(jaw, 'jaw', 'chin', smooth=180)
    C.rigid(P.tubep([(0, -0.31, 0.19), (0, -0.42, 0.183), (0, -0.49, 0.176)], [0.012, 0.012, 0.008], [0.22, 0.18, 0.11], m=6, n=10), 'jaw', 'maw', smooth=180)
    # the tongue: a tube whose weights run linearly along the three bones (it stretches evenly when they move apart)
    tg = P.tubep([tp[0] + V(0, 0.03, 0), tp[1], tp[2], tp[3]], [0.016, 0.015, 0.014, 0.014], [0.034, 0.03, 0.026, 0.024], m=13, n=8)
    M.weights_by(tg, -tg.v[:, 1], [(0.30, {'tongue1': 1.0}), (0.36, {'tongue2': 1.0}), (0.42, {'tongue3': 1.0})])
    C.skin(tg, 'maw', smooth=180)
    C.rigid(P.blob(tp[3] + V(0, 0.004, 0), 0.032, 0.028, 0.02, n=10, m=6), 'tongue3', 'maw', smooth=180)
    for sg in (1, -1):
        q, nrm, _ = P.nearest(bvh, (sg * 0.145, -0.39, 0.40))
        C.rigid(P.blob(q - nrm * 0.008, 0.02, 0.02, 0.02, n=8, m=5), 'head', 'eye', smooth=180)
    RB.add_claws(C, bvh, J, 'chin', n=4, length=0.05, r=0.011, spread=0.03, drop=0.25, sides=4)
    # reed harness: two bands across the back, two along it, and the hearth-pot tied where they cross
    for y in (-0.13, 0.17):
        st = H.strap_over(bvh, y, -0.46, 0.46, w=0.034, th=0.008, n=17)
        if st is not None:
            M.transfer_weights(st, body)
            C.skin(st, 'reed', smooth=30)
    for x in (-0.17, 0.17):
        st = H.strap_over(bvh, 0.0, -0.22, 0.33, w=0.034, th=0.008, n=11, along='y', other=x)
        if st is not None:
            M.transfer_weights(st, body)
            C.skin(st, 'reed', smooth=30)
    zt = surf_top(bvh, 0.0, 0.02, default=0.5)
    C.rigid(H.pot((0, 0.02, zt - 0.015), s=1.0), 'pot', 'clay')
    C.rigid(M.join([H.rope((sx * 0.105, 0.02 + sy * 0.105, zt + 0.185), (sx * 0.17, 0.02 + sy * 0.16, surf_top(bvh, sx * 0.17, 0.02 + sy * 0.16, default=0.45) - 0.01),
                           0.009, 0.009, sides=4, segs=2) for sx in (1, -1) for sy in (1, -1)]), 'pot', 'reed', smooth=60)
    C.rigid(P.ring((0, 0.02, zt + 0.185), 0.108, 0.01, axis=(0, 0, 1), n=12, m=4), 'pot', 'reed', smooth=60)
    tip_pt = tp[3].copy()

    def slosh(a=1.0, n=1, sc=0.03):
        def fn(p, u):
            p.rot('belly', y=3 * a * osc(u, n, 0.3), x=2 * a * osc(u, n, 0.05))
            p.scl('belly', 1 + sc * osc(u, 2 * n, 0.2))
            p.rot('pot', y=-1.5 * a * osc(u, n, 0.4), x=-a * osc(u, n, 0.15))
        return fn
    Q.idle(dur=3.6, breath=0.01, look=6.0, tail=0.0, shift=0.006, ear=0.0, extra=slosh(0.6, 1, 0.04))
    Q.gait('Walk', 0.6, 0.8, 0.6, RB.TROT, lift=0.04, bob=0.006, roll=2.5, yaw=4.0, side=7.0, headyaw=2.4, nod=1.0, tuck=10.0, pastern=8.0,
           extra=slosh(1.5, 2))
    Q.gait('Run', 2.6 / 1.2, 0.5, 0.26, RB.BOUND, lift=0.09, hlift=0.07, bob=0.05, bobn=2, bobph=0.76, crouch=-0.05, pitch=3.0, pitchph=0.6,
           roll=0.0, yaw=0.0, side=0.0, headyaw=0.0, nod=2.0, tuck=25.0, pastern=5.0, extra=slosh(3.0, 2, 0.03))

    def lash(p, u):
        """gulps, gapes, and the tongue is out and back in a fifth of a second"""
        hu = 0.45 / 1.0
        w = bump(u, 0.0, hu * 0.5, hu * 0.7, hu * 0.9)
        o = bump(u, hu * 0.55, hu * 0.82, hu + 0.24, hu + 0.42)
        t = sm(u, hu * 0.8, hu) * (1 - sm(u, hu + 0.07, hu + 0.3))
        Q.body(p, dy=0.04 * w - 0.05 * t, dz=-0.008 * w, pitch=-4 * o)
        Q.neck(p, pitch=-9 * o)
        Q.jaw(p, 28 * o)
        p.move('tongue1', 0, -0.12 * t, 0)
        p.move('tongue2', 0, -0.66 * t, 0)
        p.move('tongue3', 0, -0.66 * t, 0)
        p.rot('belly', x=3 * w)
        p.scl('belly', 1 + 0.08 * w - 0.05 * t)
        Q.plant(p)
    C.clip('Attack1', 1.0, lash, hit=0.45)
    C.reach('Attack1', 'tongue3', tip_pt)

    def slam(p, u):
        """squats, throws itself up and forward and comes down flat on its belly"""
        hu = 0.7 / 1.2
        c = bump(u, 0.0, hu * 0.3, hu * 0.42, hu * 0.55)
        ta = min(1.0, max(0.0, (u - hu * 0.45) / (hu * 0.55)))
        arc = 0.36 * math.sin(math.pi * ta) if u < hu else 0.0
        k = sm(u, hu * 0.45, hu) * (1 - sm(u, hu + 0.16, 0.94))
        s = bump(u, hu - 0.02, hu, hu + 0.08, 0.9)
        Q.body(p, dz=-0.015 * c + arc, dy=-0.28 * k, pitch=-12 * math.sin(2 * math.pi * ta) * (1.0 if u < hu else 0.0) + 2 * s)
        Q.neck(p, pitch=-8 * c + 6 * s)
        Q.jaw(p, 18 * s)
        p.scl('belly', 1 + 0.05 * c - 0.04 * s)
        p.rot('pot', x=-6 * s * osc(u, 5))
        air = max(sm(u, hu * 0.42, hu * 0.52) * (1 - sm(u, hu - 0.03, hu + 0.02)), 0.5 * bump(u, hu + 0.16, hu + 0.3, 0.84, 0.94))
        tk = 30.0 * math.sin(math.pi * ta) if u < hu else 0.0
        Q.plant(p, air=air, off=(0, -0.28 * k, 0), tuck=dict(f={'bend': (0, tk, 0)}, h={'bend': (0, tk * 0.5, 0)}))
    C.clip('Attack2', 1.2, slam, hit=0.7)
    Q.reach('Attack2')
    Q.hit(back=0.04, drop=-0.012, roll=1.5, pitch=-1.0, extra=slosh(3.0, 2, 0.03))
    # the wide jaw stays almost shut and the sack keeps its size: an open jaw or a shrunk sack would leave it standing
    Q.death_belly(dur=1.7, roll=3.0, planted=True, splay=0.09, head=0.0, drop=0.03, jaw=7.0, hyaw=6.0,
                  extra=lambda p, u: (p.scl('belly', 1 - 0.03 * sm(u, 0.15, 0.8)), p.rot('pot', y=14 * sm(u, 0.35, 0.6))))
    Q.spawn_rise(dur=1.3, depth=0.62, extra=slosh(2.0, 3))
    H.report_low(C)
    return C.finalize(bake=bake, notes="h2: sprawl table with frog hind legs; tongue1..3 stretch by translation")
