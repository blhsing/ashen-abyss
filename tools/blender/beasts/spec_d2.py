"""Rig D creatures, part 2: loopers (leech, grave-grub), columns (ash-worm, cinder-root, pale borer) and rearers
(miasma-spitter, elder leech). The column proof (mud-eel) is in spec_d.py."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis
import bparts as P
import rig_d as RD
import rig_b as RB
from spec_d import back_line, col_pts

_c = L._c
BUILD = {}


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


# ================================================================================================ looks
def k_frond(N, p):
    """gill frond: membrane whose tips glow (distance from `center` in object space, r0 .. r1)"""
    r = L.KINDS['membrane'](N, p)
    e = _c(p, 'emit', (0.75, 0.9, 0.2))
    vm = N.new('ShaderNodeVectorMath', operation='DISTANCE')
    N.set(vm.inputs[0], N.coord())
    N.set(vm.inputs[1], tuple(float(x) for x in p['center']))
    m = N.mapr(vm.outputs['Value'], p['r0'], p['r1'])
    m = N.math('MULTIPLY', m, N.mapr(N.noise(40, 2, 0.5), 0.2, 0.7, 0.55, 1.0))
    r['color'] = N.mix(N.math('MULTIPLY', m, 0.85), r['color'], tuple(x * 0.6 for x in e))
    r['emit'] = (e, _c(p, 'strength', 1.6))
    r['emask'] = m
    return r


def k_sglow(N, p):
    """wet skin with warmth glowing through it where the surface faces -Y (the belly of a reared animal) between the
    heights z0 .. z1: slime + a clotted, veined emission"""
    r = L.KINDS['slime'](N, p)
    e = _c(p, 'emit', (1.0, 0.40, 0.07))
    _, ny, _ = N.sep(N.geo().outputs['Normal'])
    facing = N.mapr(N.math('MULTIPLY', ny, -1.0), 0.3, 0.95)
    z0 = _c(p, 'z0', 0.3); z1 = _c(p, 'z1', 1.3)
    hz = N.math('MULTIPLY', N.height_z(z0, z0 + 0.25), N.math('SUBTRACT', 1.0, N.height_z(z1 - 0.25, z1)))
    vm = N.mapr(N.voro(_c(p, 'vein_scale', 8.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.1, 5)), 0.0, 0.05, 1.0, 0.0)
    clot = N.mapr(N.noise(4.0, 3, 0.6, vec=N.mapping(N.coord(), loc=(2.0, 5.0, 3.0))), 0.3, 0.75, 0.3, 1.0)
    em = N.math('MULTIPLY', N.math('MULTIPLY', facing, hz), N.math('MULTIPLY', clot, N.math('SUBTRACT', 1.0, N.math('MULTIPLY', vm, 0.8))))
    em = N.math('MULTIPLY', em, _c(p, 'gain', 1.0))
    r['color'] = N.mix(N.math('MULTIPLY', em, 0.5), r['color'], tuple(x * 0.3 for x in e))
    r['color'] = N.mix(N.math('MULTIPLY', N.math('MULTIPLY', vm, N.math('MULTIPLY', facing, hz)), 0.5), r['color'], (0.05, 0.02, 0.015))
    r['emit'] = (e, _c(p, 'strength', 1.5))
    r['emask'] = em
    return r


L.KINDS.update(frond=k_frond, sglow=k_sglow)
L.GRIME.update(frond=((0.02, 0.014, 0.012), 0.5), sglow=((0.02, 0.016, 0.012), 0.6))


# ================================================================================================ shared parts
def tiled_column(pts, rf, rs, rows=14, n=16, per=5, depth=0.035, gap=0.012, up=(0, -1, 0)):
    """column of overlapping plates (roof tiles): `rows` courses up the path, each course n / 2 tiles round, the
    courses staggered; every tile is thickest at its lower edge and tucks under the course above"""
    m = rows * per + 1
    t = P.tubep(pts, rf, rs, m=m, n=n, up=up)
    path = M.polyline(np.asarray(pts, dtype=float), m)
    v = t.v
    for k in range(m * n):
        i, j = divmod(k, n)
        row = min(i // per, rows - 1)
        ph = (i - row * per) / float(per)                  # 0 at the lower edge of the course .. 1 at its top
        rad = v[k] - path[i]
        rn = float(np.linalg.norm(rad))
        if rn < 1e-6:
            continue
        d = depth * (1.0 - ph) ** 1.5 - depth * 0.35
        if (j + row) % 2 == 0:                             # the joints between neighbouring tiles of a course
            d -= gap * (0.4 + 0.6 * (1.0 - ph))
        v[k] = v[k] + rad / rn * d
    t.v = v
    t.smooth = 38.0
    return t


def col_blast(K, name='Attack2', dur=1.5, hit=0.8, back=16.0, fwd=24.0, swell=0.12, head=18.0, shake=2.0, extra=None):
    """column attack: rears back and swells (inhale), then throws its front forward and blows, trembling"""
    hu = hit / dur
    sb = K.seg[min(K.g + 1, K.n - 1)]

    def fn(p, u):
        w = bump(u, 0.0, hu * 0.6, hu * 0.85, hu)
        s = bump(u, hu * 0.85, hu, hu + 0.25, 0.97)
        K.lean(p, pitch=-back * w + fwd * s, power=0.4)
        K.wave(p, u, shake * s, n=10, k=2)
        p.scl(sb, 1 + swell * w - swell * 0.5 * s)
        K.head(p, pitch=-head * w + head * s, roll=shake * s * osc(u, 12))
        K.rise(p, dz=0.05 * w - 0.03 * s)
        if extra:
            extra(p, u, w, s)
    c = K.C.clip(name, dur, fn, hit=hit)
    K.reach(name)
    return c


# ================================================================================================ loopers
def looper_clips(C, K, walk, run, bite=0.3, leap=(0.8, 0.8), mouth=None, idle_lift=0.14, nose=25.0):
    """the clip set of the inchworm rigs. walk / run = (v, T, duty, lift, lift_rear, nose pitch in the swing);
    bite = how far the mouth strikes beyond its resting place, leap = (forward, up) of the latch, all x body length;
    mouth(p, open 0..1)"""
    Lh = K.L
    rr0, fr0 = K.rest_ends(0.40)
    mo = mouth or (lambda p, a: None)
    k0 = 0.5 + 0.5 * osc(0.0, 2, 0.3)
    q0 = fr0 + V(0.0, 0.05 * Lh * k0, idle_lift * Lh * k0)
    pf0 = nose * k0

    def head(p, x=0.0, z=0.0):
        if K.has_head:
            p.rot('head', x=-10 * k0 + x, z=z)

    def idle(p, u):
        k = 0.5 + 0.5 * osc(u, 2, 0.3)
        q = fr0 + V(0.06 * Lh * osc(u, 1), 0.05 * Lh * k, idle_lift * Lh * k)
        K.place(p, rr0, q, pitch_f=nose * k)
        if K.has_head:
            p.rot('head', x=-10 * k, z=12 * osc(u, 1))
        mo(p, 0.25 + 0.2 * osc(u, 3))
    C.clip('Idle', 2.4, idle)
    K.gait('Walk', walk[0], walk[1], walk[2], lift=walk[3], lift_r=walk[4], nose=walk[5], extra=lambda p, u, a, b: mo(p, 0.2))
    K.gait('Run', run[0], run[1], run[2], lift=run[3], lift_r=run[4], nose=run[5], head=-15.0, extra=lambda p, u, a, b: mo(p, 0.3 + 0.5 * a))

    def bite_fn(p, u):
        hu = 0.5
        w = bump(u, 0.0, hu * 0.55, hu * 0.7, hu)
        s = bump(u, hu * 0.7, hu, hu + 0.1, 0.95)
        q = q0 + V(0, 0.14 * Lh * w - bite * Lh * s, 0.22 * Lh * w - 0.05 * Lh * s)
        K.place(p, rr0, q, pitch_f=pf0 + 35 * w - 30 * s)
        head(p, x=-20 * w + 15 * s)
        mo(p, 0.25 + 0.75 * bump(u, hu * 0.2, hu * 0.7, hu - 0.02, hu + 0.04))
    C.clip('Attack1', 0.7, bite_fn, hit=0.35)
    K.reach('Attack1')

    def latch(p, u):
        hu = 0.5 / 1.1
        cr = bump(u, 0.0, hu * 0.4, hu * 0.5, hu * 0.7)                     # gathers into a tight loop
        fl = bump(u, hu * 0.5, hu, hu + 0.22, hu + 0.42)                    # in the air / latched on
        arc = math.sin(math.pi * min(1.0, max(0.0, (u - hu * 0.5) / max(hu * 0.5, 1e-3)))) if u < hu else 0.0
        wr = bump(u, hu, hu + 0.03, hu + 0.2, hu + 0.26) * osc(u, 9)       # wriggles while it hangs on
        f = q0 + V(0.04 * Lh * wr, 0.08 * Lh * cr - leap[0] * Lh * fl, leap[1] * Lh * fl + 0.2 * Lh * arc - 0.08 * Lh * cr)
        r = rr0 + V(0.0, -0.3 * Lh * cr * (1 - fl) - leap[0] * 0.85 * Lh * fl, max(0.0, leap[1] - 0.3) * Lh * fl + 0.25 * Lh * arc)
        K.place(p, r, f, yaw=8 * wr, pitch_f=pf0 * (1 - fl) + 15 * fl, pitch_r=25 * fl)
        head(p, x=-30 * cr + 15 * fl)
        mo(p, 0.25 + 0.75 * bump(u, hu * 0.5, hu * 0.9, hu - 0.01, hu + 0.03) + 0.4 * abs(wr))
    C.clip('Attack2', 1.1, latch, hit=0.5)
    K.reach('Attack2')

    def hit(p, u):
        k = bump(u, 0.0, 0.2, 0.3, 1.0)
        K.place(p, rr0 + V(0, 0.03 * Lh * k, 0), q0 + V(0.12 * Lh * k, 0.25 * Lh * k, 0.25 * Lh * k), roll=25 * k, pitch_f=pf0 + 30 * k)
        head(p, x=-35 * k)
        mo(p, 0.25 + 0.6 * k)
    C.clip('Hit', 0.4, hit)

    def death(p, u):
        a = bump(u, 0.0, 0.15, 0.3, 0.5)                                    # rears and writhes
        f = sm(u, 0.25, 0.6)                                                 # rolls over into a limp curl
        tw = (1 - f) * a * osc(u, 6)
        r = rr0 + V(0.1 * Lh * f, -0.12 * Lh * f, 0)
        q = q0 + V(0.34 * Lh * f + 0.1 * Lh * tw, 0.3 * Lh * a * (1 - f) + 0.26 * Lh * f, 0.35 * Lh * a * (1 - f) - (q0[2] - K.z) * f)
        K.place(p, r, q, roll=90 * f, pitch_f=pf0 * (1 - f) + 30 * a * (1 - f))
        head(p, x=-30 * a * (1 - f) + 10 * k0 * f, z=20 * f)
        mo(p, 0.25 + 0.5 * a + 0.3 * f)
    C.clip('Death', 1.4, death, floor=K.seg[0], settle=(0.5, 0.65))

    def spawn(p, u):
        r = sm(u, 0.0, 0.55)
        d = 1 - r
        sh = bump(u, 0.55, 0.65, 0.8, 0.95)
        K.place(p, rr0 + V(0, -0.3 * Lh * d, -0.75 * Lh * d),
                q0 + V(0.05 * Lh * sh * osc(u, 7), 0.25 * Lh * d, -0.35 * Lh * d + 0.3 * Lh * bump(u, 0.1, 0.4, 0.5, 0.75)),
                pitch_f=pf0 + 50 * d, pitch_r=60 * d)
        head(p, x=-30 * d)
        mo(p, 0.25 + 0.75 * d)
    C.clip('Spawn', 1.1, spawn, under_ok=True)


@reg
def lurk_leech(bake=True):
    """Weeper: arm-length flattened leech, leaf-shaped, round sucker mouth, a row of gill-flutes down each side;
    moves in fast vertical loops"""
    Lh, n, z = 1.0, 8, 0.042
    C = B.Beast('mon_lurk_leech', RD.looper_skel(Lh, n, z, head=0.07), budget=4000, bone_budget=28, mb=2.5, tex=1024)
    C.meta.update(family='lurk', rig='D', scale=0.6, game_speed=5.0)
    K = RD.Looper(C)
    C.look('skin', 'slime', 'skin_body', color=(0.17, 0.125, 0.10), color2=(0.06, 0.05, 0.05), belly=(0.42, 0.21, 0.09), axis=(0, 1, 0),
           ring=46.0, ring_h=1.0, wart=80.0, rough=0.14, dry=0.15)
    C.look('lip', 'slime', 'skin_body', color=(0.58, 0.38, 0.32), color2=(0.36, 0.2, 0.17), ring=0.0, wart=120.0, rough=0.14, dry=0.0)
    C.look('maw', 'slime', 'skin_body', color=(0.32, 0.10, 0.08), color2=(0.12, 0.03, 0.03), ring=0.0, rough=0.12)
    C.look('hole', 'flat', 'skin_body', color=(0.008, 0.006, 0.006), rough=0.25)
    C.look('teeth', 'bone', 'skin_body', color=(0.62, 0.58, 0.48), dirt_lo=-2.0, dirt_hi=-1.5)
    ys = np.linspace(Lh / 2, -Lh / 2, 9)
    pts = [V(0, y, z) for y in ys]
    rv = [0.014, 0.030, 0.040, 0.043, 0.041, 0.036, 0.031, 0.029, 0.03]
    rh = [0.034, 0.078, 0.104, 0.114, 0.106, 0.090, 0.070, 0.055, 0.05]
    bones = K.seg + ['head']
    body = RD.chain_tube(pts + [V(0, -Lh / 2 - 0.03, z)], rv + [0.022], rh + [0.035], bones=bones, m=40, n=12, up=(0, 0, 1), ridges=(34, 0.004, 0.0))
    body.v[:, 2] = np.maximum(body.v[:, 2], 0.004)                    # flat sole
    C.skin(body, 'skin')
    # sucker mouth: a fleshy ring round a dark throat with a circle of denticles
    hy = -Lh / 2 - 0.035
    C.rigid(P.ring((0, hy, z + 0.004), 0.038, 0.018, axis=(0, 1, 0), n=14, m=6), 'head', 'lip', smooth=180)      # the pale sucker lip: what reads on dark ground
    C.rigid(P.blob((0, hy + 0.012, z), 0.04, 0.012, 0.04, n=10, m=5), 'head', 'maw', smooth=180)
    den = [P.cone(V(0.03 * math.cos(a), hy + 0.006, z + 0.03 * math.sin(a)), (-math.cos(a) * 0.6, -1, -math.sin(a) * 0.6), 0.016, 0.004, n=4)
           for a in np.linspace(0, 2 * math.pi, 9)[:-1]]
    C.rigid(M.join(den), 'head', 'teeth', smooth=50)
    C.rigid(P.ring((0, Lh / 2 - 0.014, z - 0.01), 0.024, 0.011, axis=(0, 0.8, -0.6), n=10, m=5), K.seg[0], 'skin', smooth=180)   # rear sucker
    # gill-flutes: a row of dark holes down each flank
    bv = P.bvh_of(body)
    holes = []
    for sg in (1, -1):
        for y in np.linspace(0.30, -0.36, 9):
            x = sg * float(np.interp(y, ys[::-1], rh[::-1])) * 0.72
            q, nr = P.surf(bv, (x, y, 0.4), (0, 0, -1), 1.0)
            if q is not None:
                holes.append(P.blob(q + nr * 0.001, 0.010, 0.016, 0.004, n=6, m=4, rot=M.rot_to((0, 0, 1), nr)))
    C.skin(RD.skin_to(M.join(holes), bones, sharp=3.0), 'hole', smooth=180)

    def mouth(p, a):
        p.scl('head', 1 + 0.45 * a)
    # run: 8-frame bounding loops, each end planted for 2 frames; walk: 14-frame inchworm
    looper_clips(C, K, walk=(1.5, 14 / 30.0, 0.5, 0.05, 0.035, 10.0), run=(8.333, 8 / 30.0, 0.125, 0.2, 0.15, 20.0), mouth=mouth)
    C.rescale(1.3)
    return C.finalize(bake=bake, notes="looper")


@reg
def sexton_grub(bake=True):
    """Grave-grub (listed under rig C, built on the looper): fat ridged larva, a string of pale beads behind a hard
    black head-capsule with two short hook jaws; humping inchworm arcs"""
    import rig_c
    Lh, n, z = 0.62, 7, 0.068
    hy = -Lh / 2

    def extra(sk, names):
        sk.lr('mandible', (0.03, hy - 0.075, z - 0.012), (0.012, hy - 0.135, z - 0.02), 'head')
    C = B.Beast('mon_sexton_grub', RD.looper_skel(Lh, n, z, head=0.09, extra=extra), budget=4000, bone_budget=28, mb=2.5, tex=1024)
    C.meta.update(family='sexton', rig='C', scale=0.8, game_speed=4.8)
    K = RD.Looper(C)
    C.look('skin', 'slime', 'skin_body', color=(0.50, 0.45, 0.34), color2=(0.30, 0.25, 0.17), belly=(0.56, 0.50, 0.38), axis=(0, 1, 0),
           ring=60.0, ring_h=0.8, wart=120.0, wart_h=0.5, rough=0.2, dry=0.3)
    C.look('head', 'chitin', 'bone_head', color=(0.02, 0.017, 0.015), rough=0.25, dust=0.2)
    C.look('leg', 'chitin', 'bone_head', color=(0.10, 0.07, 0.04), rough=0.4, dust=0.0)
    ys = np.linspace(Lh / 2, -Lh / 2, 8)
    pts = [V(0, y, z) for y in ys]
    r = [0.035, 0.066, 0.074, 0.076, 0.075, 0.072, 0.066, 0.055]
    bones = K.seg + ['head']
    body = RD.chain_tube(pts + [V(0, hy - 0.03, z - 0.006)], [x * 0.96 for x in r] + [0.045], r + [0.048], bones=bones, m=44, n=12,
                         up=(0, 0, 1), ridges=(9, 0.024, 0.0))
    C.skin(body, 'skin')
    cap = P.blob((0, hy - 0.045, z - 0.008), 0.052, 0.05, 0.048, n=12, m=7)
    C.rigid(cap, 'head', 'head', smooth=180)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(rig_c.mandible((sg * 0.03, hy - 0.072, z - 0.012), (sg * 0.01, hy - 0.14, z - 0.022), 0.012, curve=0.3, side=sg, teeth=1),
                'mandible_' + s, 'leg')
        C.rigid(P.blob((sg * 0.032, hy - 0.07, z + 0.02), 0.008, 0.008, 0.008, n=6, m=4), 'head', 'leg', smooth=180)      # ocellus
    # three pairs of stub legs under the front segments
    stubs = []
    for k in range(3):
        y = hy + 0.05 + 0.075 * k
        for sg in (1, -1):
            stubs.append(P.cone((sg * 0.035, y, 0.04), (sg * 0.5, -0.2, -1), 0.04, 0.012, n=5, curl=(0, -1, 0), seg=2))
    C.skin(RD.skin_to(M.join(stubs), bones, sharp=3.0), 'leg', smooth=50)

    def mouth(p, a):
        p.rot('mandible_l', z=30 * a); p.rot('mandible_r', z=-30 * a)
    looper_clips(C, K, walk=(1.2, 12 / 30.0, 0.5, 0.045, 0.03, 12.0), run=(6.0, 8 / 30.0, 0.125, 0.14, 0.1, 20.0), mouth=mouth, bite=0.3,
                 leap=(1.3, 1.0))
    C.rescale(1.25)
    return C.finalize(bake=bake, notes="looper (rig C family)")


# ================================================================================================ columns
@reg
def ash_worm(bake=True):
    """Ash-sifter: a worm as thick as a barrel made of baked ash plates like roof tiles, rising out of the ground;
    its front end is a flat round sieve-plate full of holes with a dull red glow behind them"""
    yz = [(-0.05, -0.45), (-0.04, -0.22), (0.0, -0.01), (0.08, 0.24), (0.13, 0.48), (0.10, 0.72), (0.0, 0.94), (-0.14, 1.12), (-0.30, 1.25),
          (-0.45, 1.31)]                                   # rears out of the ash in an S-bend
    pts = col_pts(yz)
    hj = pts[-1]
    hd = unit(V(0, -0.9, -0.3))                            # the sieve looks forward and a little down
    tip = hj + hd * 0.26
    C = B.Beast('mon_ash_worm', RD.column_skel(pts, tip))
    C.meta.update(family='ash', rig='D', scale=1.2, game_speed=3.0, buried=0.45)
    K = RD.Column(C)
    C.look('plate', 'rock', 'rock_plates', color=(0.115, 0.108, 0.10), ash=(0.36, 0.34, 0.32), scale=24.0, crack_w=0.03, ash_amt=0.8, rough=0.85)
    C.look('sieve', 'rock', 'rock_plates', color=(0.06, 0.056, 0.052), ash=(0.30, 0.28, 0.26), scale=34.0, crack_w=0.025, ash_amt=0.45)
    C.look('holes', 'emis', 'emis_holes', emit=(1.0, 0.15, 0.035), strength=1.5, mottle=0.9, base_mul=0.4, crust=0.5, scale=40.0)
    C.look('ash', 'mud', 'rock_plates', color=(0.15, 0.14, 0.13), dry=(0.36, 0.34, 0.32))
    seg = K.seg
    rf = [0.25, 0.25, 0.25, 0.245, 0.235, 0.222, 0.208, 0.195, 0.185, 0.178, 0.172]
    rs = [x * 1.07 for x in rf]
    body = tiled_column(pts + [hj + hd * 0.10], rf, rs, rows=13, n=16, per=5, depth=0.04, gap=0.014)
    B.wbones(body, seg + ['head'], sharp=3.0)
    C.skin(body, 'plate')
    # the sieve: a thick round plate wider than the body, raised rim, nineteen holes that glow from behind
    R0 = M.rot_to((0, 0, 1), hd)
    cz = hj + hd * 0.18
    disc = P.lathe([(0.20, -0.075), (0.31, -0.05), (0.33, -0.02), (0.33, 0.025), (0.315, 0.05)], n=22)
    disc.tf(R0).move(*cz)
    disc.smooth = 35.0
    C.rigid(disc, 'head', 'sieve')
    C.rigid(P.ring(cz + hd * 0.05, 0.305, 0.026, axis=hd, n=22, m=6), 'head', 'sieve', smooth=60)
    ex = unit(np.cross(hd, (0, 0, 1))); ey = np.cross(ex, hd)
    holes = []
    rims = []
    for rr, cnt, a0 in ((0.0, 1, 0.0), (0.105, 6, 0.0), (0.205, 12, 0.26)):
        for k in range(cnt):
            a = a0 + 2 * math.pi * k / cnt
            c = cz + hd * 0.051 + ex * (rr * math.cos(a)) + ey * (rr * math.sin(a))
            holes.append(P.blob(c, 0.036, 0.036, 0.005, n=8, m=4, rot=R0))
            rims.append(P.ring(c + hd * 0.002, 0.04, 0.008, axis=hd, n=8, m=4))
    C.rigid(M.join(holes), 'head', 'holes', smooth=180)
    C.rigid(M.join(rims), 'head', 'sieve', smooth=60)
    C.rigid(RD.mound(0.33, 0.46, n=14, seed=5, h=0.08), 'root', 'ash', free=True)

    K.idle(dur=4.0, sway=4.0, wave=2.5, nod=5.0, look=8.0)
    K.plough('Walk', 0.9, 1.4, lean=10.0, weave=5.0, sink=0.05, head=-8.0)
    K.plough('Run', 2.5, 0.9, lean=18.0, weave=7.0, bob=0.03, sink=0.1, head=-12.0)
    K.lunge(dur=1.0, hit=0.5, back=16.0, fwd=52.0, head=8.0, power=0.4)
    col_blast(K, dur=1.5, hit=0.8, back=18.0, fwd=22.0, swell=0.12, head=20.0)
    K.hit(back=12.0)
    K.death_topple(radius=0.25, total=94.0)
    K.spawn_erupt(dur=1.2)
    return C.finalize(bake=bake, notes="column")


@reg
def char_root(bake=True):
    """Cinder-root: a single thick charred taproot standing two metres tall like a rearing snake, tapering to a
    splintered, hooked tip, with a fringe of fine rootlets; orange ember veins"""
    yz = [(0.10, -0.45), (0.08, -0.2), (0.05, 0.05), (0.02, 0.3), (0.0, 0.55), (-0.01, 0.8), (-0.01, 1.05), (-0.04, 1.28), (-0.11, 1.49),
          (-0.23, 1.65), (-0.38, 1.72), (-0.52, 1.68), (-0.61, 1.57)]
    pts = col_pts(yz)
    C = B.Beast('mon_char_root', RD.column_skel(pts))
    C.meta.update(family='char', rig='D', scale=1.0, game_speed=5.0, buried=0.45)
    K = RD.Column(C)
    C.look('bark', 'bark', 'emis_root', color=(0.03, 0.026, 0.024), ash=(0.17, 0.16, 0.15), axis=(0, 0, 1), scale=20.0, heat=(1.0, 0.30, 0.04),
           heat_amt=0.55, strength=1.7, ash_amt=0.35)
    C.look('rootlet', 'bark', 'emis_root', color=(0.055, 0.042, 0.032), ash=(0.15, 0.13, 0.11), scale=70.0, ash_amt=0.15, depth=1.5)
    C.look('soil', 'mud', 'rock_mound', color=(0.05, 0.04, 0.03), dry=(0.13, 0.11, 0.085))
    r = [0.17, 0.17, 0.168, 0.158, 0.146, 0.132, 0.117, 0.102, 0.087, 0.072, 0.058, 0.044, 0.028]
    body = RD.chain_tube(pts, r, [x * 0.9 for x in r], bones=K.seg, m=70, n=12)
    body.rough(0.014, 5.0, seed=4)
    rng = np.random.default_rng(8)
    for k in range(7):                                        # knots and burls
        i = int(rng.integers(3, 10))
        a = rng.uniform(0, 2 * math.pi)
        body.dent(pts[i] + V(math.cos(a), math.sin(a), rng.uniform(-0.1, 0.1)) * r[i], 0.07, 0.03)
    B.wbones(body, K.seg, sharp=3.0)
    C.skin(body, 'bark')
    # splintered tip
    td = unit(pts[-1] - pts[-2])
    spl = []
    for k in range(6):
        d = unit(td + rng.normal(0, 0.35, 3))
        spl.append(P.cone(pts[-1] - td * 0.05 + rng.normal(0, 0.008, 3), d, rng.uniform(0.09, 0.2), 0.013, n=4))
    C.rigid(M.join(spl), K.seg[-1], 'bark', smooth=30)
    # fringe of fine rootlets down its length
    tf = P.tufts(body, 90, length=(0.10, 0.26), radius=(0.006, 0.011), seed=7, mask=lambda c, n: (c[:, 2] > 0.12) & (np.abs(n[:, 2]) < 0.8),
                 flow=(0, 0.3, -1), lay=0.25, sink=0.15, sides=3, jitter=0.6, droop=0.7)
    C.skin(tf, 'rootlet', smooth=180)
    C.rigid(RD.mound(0.22, 0.34, n=12, seed=9), 'root', 'soil', free=True)
    hi = K.n - 5

    def twitch(p, u, k=1.0):
        K.wave(p, u, 1.6 * k, n=7, k=3.0, axis='z')
    K.idle(dur=3.6, sway=6.0, wave=4.0, nod=0.0, look=0.0, extra=lambda p, u: twitch(p, u))
    K.plough('Walk', 1.6, 1.0, lean=10.0, weave=8.0, sink=0.06, extra=lambda p, u: twitch(p, u))
    K.plough('Run', 5.0, 0.6, lean=20.0, weave=12.0, bob=0.03, sink=0.12, extra=lambda p, u: twitch(p, u))
    K.lunge(dur=0.8, hit=0.4, back=26.0, fwd=58.0, power=0.9, extra=lambda p, u, w, s: K.lean(p, pitch=-30 * w + 18 * s, lo=hi, power=1.0))
    K.sweep(dur=1.3, hit=0.65, low=58.0, arc=120.0, extra=lambda p, u, dn, sw: K.lean(p, pitch=45 * dn, lo=hi, power=1.0))
    K.hit(extra=lambda p, u, k: twitch(p, u, 3 * k))
    K.death_topple(radius=0.15, total=92.0)
    K.spawn_erupt(dur=1.0)
    return C.finalize(bake=bake, notes="column")


@reg
def hush_serpent(bake=True):
    """Pale Borer: a white ribbon-serpent, flat as a strap and as wide as a door plank, standing in a slow corkscrew;
    no head - the front end narrows to a point with a hole near the tip like the eye of a needle"""
    H0, H1, turns, rad = -0.42, 1.74, 1.6, 0.085
    nseg = 12

    def cen(t):
        z = H0 + (H1 - H0) * t
        r = rad * sm(z, -0.15, 0.35) * (1 - 0.7 * sm(t, 0.8, 1.0))
        th = 2 * math.pi * turns * t + 0.5
        return V(r * math.cos(th), r * math.sin(th), z), th
    pts = [cen(i / float(nseg))[0] for i in range(nseg + 1)]
    C = B.Beast('mon_hush_serpent', RD.column_skel(pts))
    C.meta.update(family='hush', rig='D', scale=1.2, game_speed=3.0, buried=0.42)
    K = RD.Column(C)
    C.look('white', 'chalk', 'skin_ribbon', color=(0.78, 0.78, 0.77), shade=(0.56, 0.57, 0.60), crack=30.0)
    C.look('void', 'flat', 'skin_ribbon', color=(0.004, 0.004, 0.006), rough=0.6)
    m, n = 76, 10
    rg = M.ring2d(n, 4.0)

    def frame(t):
        c, th = cen(t)
        T = unit(cen(min(1.0, t + 0.004))[0] - cen(max(0.0, t - 0.004))[0])
        Nr = V(math.cos(th), math.sin(th), 0.0)
        Nr = unit(Nr - T * float(Nr @ T))
        return c, T, Nr, np.cross(T, Nr)

    def half(t):
        k = (1 - sm(t, 0.86, 1.0)) ** 0.8
        return 0.105 * k + 0.008, 0.017 * (0.45 + 0.55 * k)
    rings = np.zeros((m, n, 3))
    for i in range(m):
        t = i / (m - 1.0)
        c, T, Nr, Br = frame(t)
        hw, ht = half(t)
        rings[i] = c + ht * rg[:, 0:1] * Nr + hw * rg[:, 1:2] * Br
    body = M.loft(rings, cap0=True, cap1=True).recalc()
    # the needle-eye
    te = 0.925
    ce, Te, Ne, Be = frame(te)
    hw_e, ht_e = half(te)
    er = hw_e * 0.42
    holed = False
    try:
        cyl = M.revolve([(er, -0.12), (er, 0.12)], n=14, cap=True)
        cyl.tf(M.rot_to((0, 0, 1), Ne)).move(*ce)
        b2 = M.boolean(body, cyl, 'DIFFERENCE')
        if b2.ntri > body.ntri * 0.9 and len(b2.v) > len(body.v) * 0.9:
            body = b2.recalc()
            holed = True
    except Exception as e:
        B.log("needle-eye boolean failed:", e)
    body.smooth = 50.0
    B.wbones(body, K.seg, sharp=3.0)
    C.skin(body, 'white')
    if holed:
        lining = P.ring(ce, er + 0.006, min(ht_e * 0.8, 0.012), axis=Ne, n=14, m=5)
    else:
        lining = M.join([P.blob(ce + Ne * (sg * (ht_e + 0.0005)), er, er, 0.002, n=12, m=4, rot=M.rot_to((0, 0, 1), Ne)) for sg in (1, -1)])
    C.skin(RD.skin_to(lining, K.seg, sharp=3.0), 'void', smooth=180)
    C.meta['needle_eye_is_a_hole'] = holed

    def spin(p, u, n=1.0):
        p.rot(K.seg[0], z=360.0 * n * u)
    K.idle(dur=4.0, sway=3.0, wave=2.0, nod=0.0, look=0.0, extra=lambda p, u: spin(p, u))
    K.plough('Walk', 0.9, 1.4, lean=9.0, weave=4.0, sink=0.05, extra=lambda p, u: spin(p, u))
    K.plough('Run', 2.5, 0.8, lean=18.0, weave=6.0, bob=0.03, sink=0.1, extra=lambda p, u: spin(p, u))
    K.lunge(dur=0.8, hit=0.4, back=18.0, fwd=76.0, power=0.5, twist=140.0)

    def coil(p, u):                       # Attack2: winds itself tight and low, then lets go: the pulse
        hu = 0.7 / 1.3
        w = bump(u, 0.0, hu * 0.85, hu * 0.95, hu)
        s = bump(u, hu * 0.95, hu, hu + 0.12, 0.96)
        K.lean(p, twist=260 * w - 70 * s, pitch=4 * w - 6 * s)
        K.rise(p, dz=-0.22 * w + 0.1 * s)
        K.wave(p, u, 3 * s, n=9, k=2)
        p.scl(K.seg[K.g], 1 + 0.12 * s)
    C.clip('Attack2', 1.3, coil, hit=0.7)
    C.meta['attack_reach'] = max(C.meta.get('attack_reach', 0.0), 1.5)          # the ring it sends out
    K.hit()
    K.death_topple(radius=0.04, twist=70.0, total=95.0)
    K.spawn_erupt(dur=1.3)
    return C.finalize(bake=bake, notes="column (twisted strap)")


# ================================================================================================ rearers
def rearer_body(R, bend, neck, tail, head_end, rv, rh, m=70, n=12, ridges=None):
    """one skin from the tail tip along the ground, up the reared front, to the head: radii from the tail tip on"""
    pts = list(reversed(tail)) + [bend] + list(neck) + [head_end]
    bones = list(reversed(R.tail)) + ['body'] + R.neck + ['head']
    return RD.chain_tube(pts, rv, rh, bones=bones, m=m, n=n, up=(0, 0, 1), ridges=ridges), pts


@reg
def lurk_spitter(bake=True):
    """Miasma-spitter: a slim serpent that rears upright and spreads a wide frill of feathery gill-fronds round its
    neck like an opened fan; small head with a tube mouth; toxic yellow-green frond tips"""
    bend = V(0, 0.0, 0.085)
    neck = [V(0, 0.10, 0.30), V(0, 0.15, 0.56), V(0, 0.08, 0.82), V(0, -0.06, 1.03), V(0, -0.20, 1.17)]     # an S: back, then forward
    tail = [V(0.10, 0.23, 0.085), V(0.20, 0.45, 0.08), V(0.14, 0.69, 0.072), V(-0.05, 0.86, 0.062), V(-0.19, 1.05, 0.046), V(-0.14, 1.29, 0.026)]
    hj = neck[-1]
    hd = unit(V(0, -1, -0.22))
    fc = neck[-2] + (neck[-1] - neck[-2]) * 0.5              # centre of the frill
    fn_ = unit(V(0, -1, 0.3))                                # the fan faces forward and a little up
    e1 = V(1.0, 0, 0)
    e2 = unit(np.cross(fn_, e1))
    if e2[2] < 0:
        e2 = -e2
    grp = {}                                                 # frill bone -> (mean direction, fold axis)

    def fdir(a):
        return e1 * math.sin(a) + e2 * math.cos(a)

    def extra(sk):
        for s, sg in (('l', 1), ('r', -1)):
            for k, a in enumerate((25.0, 75.0, 125.0)):
                d = fdir(math.radians(a) * sg)
                nme = 'frill%d_%s' % (k + 1, s)
                sk.bone(nme, fc + d * 0.05, fc + d * 0.2, 'neck5')
                grp[nme] = (d, unit(np.cross(fn_, d)))
    C = B.Beast('mon_lurk_spitter', RD.rearer_skel(bend, neck, tail, hj + hd * 0.30, extra))
    C.meta.update(family='lurk', rig='D', scale=1.05, game_speed=3.4)
    R = RD.Rearer(C)
    C.look('skin', 'slime', 'skin_body', color=(0.10, 0.105, 0.06), color2=(0.035, 0.045, 0.03), belly=(0.27, 0.25, 0.13), ring=0.0, wart=65.0,
           rough=0.2, dry=0.3)
    C.look('frond', 'frond', 'emis_frill', color=(0.11, 0.035, 0.045), vein=(0.03, 0.008, 0.012), vein_scale=30.0, rough=0.4,
           center=fc, r0=0.27, r1=0.43, emit=(0.62, 0.85, 0.10), strength=1.3)
    C.look('maw', 'slime', 'skin_body', color=(0.24, 0.20, 0.06), color2=(0.08, 0.07, 0.02), ring=0.0, rough=0.12)
    C.look('eye', 'emis', 'emis_frill', emit=(0.62, 0.85, 0.10), strength=1.2, mottle=0.4, base_mul=0.7, scale=60.0)
    rv = [0.012, 0.034, 0.052, 0.066, 0.076, 0.084, 0.09, 0.09, 0.084, 0.074, 0.064, 0.056, 0.05]
    body, bpts = rearer_body(R, bend, neck, tail, hj + hd * 0.06, rv, [x * 1.12 for x in rv], m=72, n=12, ridges=(64, 0.004, 0.0))
    C.skin(body, 'skin')
    # head: small skull, tube mouth with a fleshy lip
    C.rigid(P.blob(hj + hd * 0.07 + V(0, 0, 0.015), 0.078, 0.115, 0.07, n=12, m=7, rot=M.rot_to((0, -1, 0), hd)), 'head', 'skin', smooth=180)
    C.rigid(P.tubep([hj + hd * 0.12, hj + hd * 0.23, hj + hd * 0.30], [0.048, 0.037, 0.034], m=6, n=10), 'head', 'skin', smooth=180)
    C.rigid(P.ring(hj + hd * 0.30, 0.034, 0.013, axis=hd, n=10, m=5), 'head', 'maw', smooth=180)
    C.rigid(P.blob(hj + hd * 0.302, 0.028, 0.008, 0.028, n=8, m=4), 'head', 'maw', smooth=180)
    for sg in (1, -1):
        C.rigid(P.blob(hj + hd * 0.10 + V(sg * 0.062, 0, 0.04), 0.02, 0.02, 0.02, n=8, m=5), 'head', 'eye', smooth=180)
    # the frill: long fronds with shorter ones between them, each rigid to one of six fold bones
    rng = np.random.default_rng(4)
    fronds = {}
    angs = [(a, 1.0) for a in np.linspace(-146, 146, 15)] + [(a, 0.6) for a in np.linspace(-136, 136, 14)]
    for a, k in angs:
        a = a + rng.uniform(-4, 4)
        d = fdir(math.radians(a))
        ln = (0.44 if k == 1.0 else 0.29) * rng.uniform(0.78, 1.12)
        back = 0.0 if k == 1.0 else 0.02                      # the short ones sit a little behind
        base = fc + d * 0.045 - fn_ * back
        mid = fc + d * ln * 0.55 - fn_ * (back + 0.03 * ln)
        tipp = fc + d * ln - fn_ * (back + 0.3 * ln)          # swept back like a ruff, not a flat disc
        wd = 0.02 * (0.8 + 0.5 * k)
        fr = M.join([RB.ear_sheet(base, mid, wd, cup=0.008, th=0.005, rows=3, cols=3, sink=0.03, shape=0.5, face=fn_),
                     RB.ear_sheet(mid - unit(mid - base) * 0.03, tipp, wd * 0.8, cup=0.006, th=0.004, rows=3, cols=3, sink=0.0, shape=0.6, face=fn_)])
        # barbs: two small side leaves give the frond a feathered edge
        sd = unit(np.cross(d, fn_))
        for sg2 in (1, -1):
            fr = M.join([fr, RB.ear_sheet(mid, mid + (d * 0.55 + sd * sg2 * 0.8) * 0.07 * (0.6 + k), wd * 0.6, cup=0.004, th=0.004, rows=2,
                                          cols=3, sink=0.01, shape=0.6, face=fn_)])
        sgn = 'l' if a >= 0 else 'r'
        nme = 'frill%d_%s' % (1 if abs(a) < 50 else (2 if abs(a) < 100 else 3), sgn)
        fronds.setdefault(nme, []).append(fr)
    for nme, lst in fronds.items():
        C.rigid(M.join(lst), nme, 'frond', smooth=180)
    C.rigid(P.ring(fc, 0.07, 0.022, axis=unit(neck[-1] - neck[-2]), n=12, m=5), 'neck5', 'skin', smooth=180)     # collar the fronds grow from

    def fold(p, a, tr=0.0):
        """a = degrees folded back along the neck (0 = wide open)"""
        for i, (nme, (d, ax)) in enumerate(sorted(grp.items())):
            p.setR(nme, rot_axis(ax, a + tr * (1 if i % 2 else -1)))
    lam = R.tail_len / 1.2
    R.idle(dur=3.6, sway=5.0, tail=6.0, look=14.0, extra=lambda p, u: fold(p, 30 + 8 * osc(u, 2)))
    R.slither('Walk', 1.1, lam / 1.1, amp=13.0, waves=1.2, lean=4.0, extra=lambda p, u: fold(p, 48 + 4 * osc(u, 2)))
    R.slither('Run', 3.238, lam / 3.238, amp=19.0, waves=1.2, lean=14.0, sway=0.04, extra=lambda p, u: fold(p, 62 + 4 * osc(u, 2)))
    R.strike(dur=0.9, hit=0.45, back=24.0, fwd=24.0, head=16.0, power=1.0,
             extra=lambda p, u, w, s: (fold(p, 36 + 30 * w - 26 * s), p.scl('head', 1 + 0.3 * bump(u, 0.4, 0.5, 0.55, 0.7))))

    def flare(p, u):                      # Attack2: the frill snaps wide open and trembles (the cloud)
        hu = 0.75 / 1.4
        w = bump(u, 0.0, hu * 0.5, hu * 0.72, hu)
        s = bump(u, hu * 0.75, hu, 0.82, 0.97)
        R.body(p, dz=0.0)
        R.tailwave(p, u, 5 + 7 * s, n=2, waves=1.0)
        R.necks(p, pitch=12 * w - 12 * s, power=0.5)
        R.neckwave(p, u, 1.5 * s, n=9, k=2)
        R.head(p, pitch=10 * w - 6 * s)
        fold(p, 36 + 40 * w - 36 * s * (1 + 0.0), tr=4 * s * osc(u, 16))
    C.clip('Attack2', 1.4, flare, hit=0.75)
    R.reach('Attack2')
    R.hit(extra=lambda p, u, k: fold(p, 36 + 40 * k))
    R.death_fall(dur=1.8, total=104.0, span=3, extra=lambda p, u, f: fold(p, 36 + 50 * f))
    R.spawn_rise(dur=1.3, extra=lambda p, u, r: fold(p, 36 + 50 * (1 - r)))
    return C.finalize(bake=bake, notes="rearer")


def hand_lobe(c, out, up, size=1.0, seed=0):
    """a small pale feeler-lobe like a hand: palm + three finger stubs, pointing along `out`"""
    rng = np.random.default_rng(seed)
    out = unit(out); up = unit(up - out * float(np.dot(up, out)))
    side = np.cross(out, up)
    Rm = np.stack([side, out, up], axis=1)
    parts = [P.blob(V(c) + out * 0.03 * size, 0.034 * size, 0.045 * size, 0.017 * size, n=8, m=5, rot=Rm)]
    for k in (-1, 0, 1):
        d = unit(out + side * 0.45 * k + up * rng.uniform(-0.15, 0.25))
        parts.append(P.cone(V(c) + out * 0.055 * size + side * 0.02 * k * size, d, rng.uniform(0.06, 0.085) * size, 0.011 * size, n=5,
                            curl=up, seg=2))
    q = M.join(parts)
    q.smooth = 180.0
    return q


@reg
def lurk_elder(bake=True):
    """Elder Leech: a leech as thick as a wine-tun rearing up, its front end a circular mouth ringed with small pale
    feeler-lobes like hands; rows of flute-holes down the flanks, smaller leeches clinging to its sides; dull orange
    warmth glowing through the belly skin"""
    bend = V(0, 0.0, 0.27)
    neck = [V(0, -0.09, 0.52), V(0, -0.15, 0.78), V(0, -0.18, 1.04), V(0, -0.20, 1.28), V(0, -0.25, 1.47)]
    tail = [V(0, 0.30, 0.25), V(0, 0.60, 0.21), V(0, 0.88, 0.16), V(0, 1.12, 0.10), V(0, 1.32, 0.05)]
    hj = neck[-1]
    hd = unit(V(0, -0.62, 0.78))                             # the mouth looks up and forward
    C = B.Beast('mon_lurk_elder', RD.rearer_skel(bend, neck, tail, hj + hd * 0.2), budget=22000)
    C.meta.update(family='lurk', rig='D', scale=1.8, game_speed=2.4)
    R = RD.Rearer(C)
    C.look('skin', 'sglow', 'skin_body', color=(0.075, 0.062, 0.055), color2=(0.028, 0.03, 0.032), belly=(0.20, 0.11, 0.06), axis=(0, 0, 1),
           ring=14.0, ring_h=1.6, wart=40.0, wart_h=1.5, rough=0.18, dry=0.3, z0=0.36, z1=1.36, emit=(0.95, 0.26, 0.03), strength=1.0, gain=0.42,
           vein_scale=11.0)
    C.look('lip', 'slime', 'skin_body', color=(0.16, 0.09, 0.075), color2=(0.07, 0.035, 0.03), ring=0.0, wart=90.0, rough=0.15)
    C.look('maw', 'slime', 'skin_body', color=(0.26, 0.07, 0.05), color2=(0.06, 0.015, 0.012), ring=0.0, rough=0.12)
    C.look('lobe', 'slime', 'skin_body', color=(0.46, 0.40, 0.33), color2=(0.30, 0.24, 0.20), ring=0.0, wart=150.0, rough=0.25)
    C.look('teeth', 'bone', 'skin_body', color=(0.62, 0.58, 0.46), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('hole', 'flat', 'skin_body', color=(0.008, 0.006, 0.006), rough=0.3)
    C.look('leech', 'slime', 'skin_body', color=(0.05, 0.045, 0.04), color2=(0.02, 0.02, 0.022), belly=(0.2, 0.1, 0.05), axis=(0, 0, 1), ring=50.0,
           rough=0.14)
    rh = [0.04, 0.11, 0.18, 0.24, 0.28, 0.30, 0.31, 0.31, 0.295, 0.27, 0.245, 0.265]
    body, bpts = rearer_body(R, bend, neck, tail, hj + hd * 0.13, [x * 0.88 for x in rh], rh, m=84, n=20, ridges=(30, 0.014, 0.0))
    body.v[:, 2] = np.maximum(body.v[:, 2], 0.003)
    C.skin(body, 'skin')
    bv = P.bvh_of(body)
    # the mouth: a fleshy ring, dark throat, a circle of small teeth, ten pale lobes like hands round the rim
    mc = hj + hd * 0.17
    ex = V(1.0, 0, 0); ey = unit(np.cross(hd, ex))
    C.rigid(P.ring(mc, 0.245, 0.06, axis=hd, n=24, m=8), 'head', 'lip', smooth=180)
    C.rigid(P.blob(mc - hd * 0.03, 0.235, 0.235, 0.035, n=18, m=5, rot=M.rot_to((0, 0, 1), hd)), 'head', 'maw', smooth=180)
    teeth = []
    for k in range(18):
        a = 2 * math.pi * k / 18
        o = ex * math.cos(a) + ey * math.sin(a)
        teeth.append(P.cone(mc + o * 0.2 + hd * 0.005, unit(-o * 0.9 + hd * 0.5), 0.065, 0.016, n=4))
    C.rigid(M.join(teeth), 'head', 'teeth', smooth=50)
    lobes = []
    for k in range(10):
        a = 2 * math.pi * (k + 0.5) / 10
        o = ex * math.cos(a) + ey * math.sin(a)
        lobes.append(hand_lobe(mc + o * 0.275 + hd * 0.01, unit(o + hd * 0.55), hd, size=1.25, seed=k))
    C.rigid(M.join(lobes), 'head', 'lobe', smooth=180)
    # flute-holes down the flanks and small leeches clinging on
    nz = [q[2] for q in [bend] + neck]; ny = [q[1] for q in [bend] + neck]
    allb = list(reversed(R.tail)) + ['body'] + R.neck + ['head']
    holes = []
    for sg in (1, -1):
        for z in np.linspace(0.46, 1.24, 6):
            for dy in (-0.03, 0.11):
                q, nr = P.surf(bv, (sg * 0.9, float(np.interp(z, nz, ny)) + dy, z + (0.06 if dy > 0 else 0.0)), (-sg, 0, 0), 1.5)
                if q is not None:
                    holes.append(P.blob(q + nr * 0.002, 0.022, 0.038, 0.006, n=8, m=4, rot=M.rot_to((0, 0, 1), nr)))
    C.skin(RD.skin_to(M.join(holes), allb, sharp=3.0), 'hole', smooth=180)
    cl = []
    for sg, z, dy in ((1, 0.62, 0.16), (-1, 0.80, 0.02), (1, 1.02, -0.06), (-1, 1.12, 0.17), (-1, 0.45, 0.2), (1, 1.2, 0.2)):
        path = []
        nav = V(0.0, 0, 0)
        for dz in (0.13, 0.05, -0.04, -0.14):
            q, nr = P.surf(bv, (sg * 0.9, float(np.interp(z + dz, nz, ny)) + dy, z + dz), (-sg, 0, 0), 1.5)
            if q is not None:
                path.append(q + nr * 0.018); nav = nav + nr
        if len(path) == 4:
            cl.append(P.tubep(path, [0.012, 0.022, 0.02, 0.008], [0.028, 0.048, 0.042, 0.014], m=9, n=8, up=tuple(unit(nav))))
    if cl:
        C.skin(RD.skin_to(M.join(cl), allb, sharp=3.0), 'leech', smooth=180)

    def mouth(p, a):
        p.scl('head', 1 + a)
    lam = R.tail_len / 1.0
    R.idle(dur=4.0, sway=3.0, tail=4.0, look=8.0, extra=lambda p, u: mouth(p, 0.04 * osc(u, 2)))
    R.slither('Walk', 0.5, lam / 0.5, amp=9.0, waves=1.0, lean=3.0, sway=0.02, bob=0.006, extra=lambda p, u: mouth(p, 0.03 * osc(u, 2)))
    R.slither('Run', 1.333, lam / 1.333, amp=13.0, waves=1.0, lean=8.0, sway=0.03, extra=lambda p, u: mouth(p, 0.05 * osc(u, 2)))
    R.strike(dur=1.3, hit=0.7, back=16.0, fwd=80.0, head=22.0, power=0.15, slide=0.1,
             extra=lambda p, u, w, s: mouth(p, 0.18 * w - 0.05 * s))

    def inhale(p, u):                     # Attack2: the mouth gapes and it draws breath, then snaps forward and shuts
        hu = 1.0 / 1.6
        w = bump(u, 0.0, hu * 0.35, hu * 0.8, hu * 0.96)
        s = bump(u, hu * 0.82, hu, hu + 0.1, 0.96)
        R.body(p, dy=0.04 * w - 0.12 * s)
        R.tailwave(p, u, 3 + 5 * s, n=2, waves=1.0)
        R.necks(p, pitch=14 * w + 44 * s, power=0.4)
        R.neckwave(p, u, 1.2 * w, n=10, k=2)
        R.head(p, pitch=24 * w + 20 * s)
        mouth(p, 0.34 * w * (1 + 0.06 * osc(u, 12)) - 0.14 * bump(u, hu - 0.03, hu, hu + 0.06, hu + 0.2))
    C.clip('Attack2', 1.6, inhale, hit=1.0)
    R.reach('Attack2')
    R.hit(back=10.0, extra=lambda p, u, k: mouth(p, 0.15 * k))
    R.death_fall(dur=2.0, total=86.0, extra=lambda p, u, f: mouth(p, 0.12 * f))
    R.spawn_rise(dur=1.5, extra=lambda p, u, r: mouth(p, 0.2 * (1 - r)))
    return C.finalize(bake=bake, notes="rearer (giant)")
