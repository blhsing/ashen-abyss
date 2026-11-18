"""Rig D creatures (serpents): columns (eel, ash-worm, cinder-root, pale borer), rearers (spitter, elder leech) and
the looping leech."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis
import bparts as P
import rig_d as RD

BUILD = {}


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


def col_pts(yz):
    return [V(0, y, z) for y, z in yz]


def back_line(pts, radii, off=0.0, per=3, z0=0.06, side=1.0):
    """points along the back (+Y side, or the belly with side=-1) of a column: `per` samples per segment, above z0"""
    path = M.polyline(np.asarray(pts, dtype=float), (len(pts) - 1) * per + 1)
    r = np.interp(np.linspace(0, 1, len(path)), np.linspace(0, 1, len(radii)), radii)
    out = []
    for i in range(len(path)):
        t = unit(path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)])
        nrm = unit(np.cross((1.0, 0, 0), t)) * (-side)          # perpendicular to the column, pointing to +Y for an upright column
        if nrm[1] * side < 0:
            nrm = -nrm
        q = path[i] + nrm * (r[i] + off)
        if q[2] >= z0:
            out.append((q, nrm))
    return out


# ================================================================================================ lurk_eel
@reg
def lurk_eel(bake=True):
    """Mud-eel: thick column rearing out of the ground, blunt armoured spade head, jaw of two side plates,
    tattered dorsal fin like a row of torn pennants"""
    yz = [(0.13, -0.42), (0.11, -0.22), (0.08, -0.02), (0.04, 0.18), (0.01, 0.38), (-0.01, 0.58), (-0.01, 0.78), (-0.04, 0.97),
          (-0.10, 1.14), (-0.19, 1.28)]
    pts = col_pts(yz)
    hj = pts[-1]
    tip = hj + V(0, -0.40, -0.10)
    hd = unit(tip - hj)
    up = unit(np.cross(hd, (1.0, 0, 0)))            # normal of the spade (pointing up / back)
    if up[2] < 0:
        up = -up

    def extra(sk, names):
        for s, sg in (('l', 1), ('r', -1)):
            sk.bone('jaw_' + s, hj + hd * 0.06 + V(sg * 0.085, 0, 0) - up * 0.045, hj + hd * 0.38 + V(sg * 0.04, 0, 0) - up * 0.06, 'head')
    C = B.Beast('mon_lurk_eel', RD.column_skel(pts, tip, extra))
    C.meta.update(family='lurk', rig='D', scale=1.0, game_speed=3.0, buried=0.42)
    K = RD.Column(C)
    C.look('skin', 'slime', 'skin_body', color=(0.085, 0.075, 0.05), color2=(0.03, 0.035, 0.03), belly=(0.20, 0.17, 0.11), axis=(0, 0, 1),
           ring=18.0, wart=55.0, rough=0.22, dry=0.35)
    C.look('fin', 'membrane', 'skin_body', color=(0.07, 0.055, 0.04), vein=(0.02, 0.015, 0.012), vein_scale=14.0, rough=0.4)
    C.look('maw', 'slime', 'skin_body', color=(0.22, 0.07, 0.06), color2=(0.10, 0.03, 0.03), ring=0.0, rough=0.15, wart=90.0)
    C.look('armour', 'chitin', 'bone_head', color=(0.05, 0.045, 0.035), color2=(0.11, 0.095, 0.07), edge_color=(0.30, 0.26, 0.18), rough=0.42,
           pit=90.0, ridge=40.0, ridge_h=1.4, dust=0.5, dust_color=(0.13, 0.11, 0.08), axis=tuple(hd))
    C.look('teeth', 'bone', 'bone_head', color=(0.60, 0.55, 0.42), stain=(0.25, 0.17, 0.09), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('mud', 'mud', 'rock_mound', color=(0.06, 0.048, 0.035), dry=(0.15, 0.125, 0.09))
    seg = K.seg
    # body
    rf = [0.150, 0.150, 0.150, 0.148, 0.145, 0.140, 0.135, 0.128, 0.120, 0.112, 0.10]
    rs = [0.165, 0.165, 0.165, 0.162, 0.158, 0.152, 0.146, 0.140, 0.134, 0.128, 0.11]
    bpts = pts + [hj + hd * 0.10]
    body = RD.chain_tube(bpts, rf, rs, bones=seg + ['head'], m=44, n=14, ridges=(26, 0.008, 0.0))
    body.rough(0.006, 9.0, seed=2)
    C.skin(body, 'skin')
    # dorsal fin: torn pennants down the whole back
    bl = back_line(pts, rf[:-1], off=-0.012, per=4, z0=0.10)
    path = [q for q, n in bl]
    nrm = np.mean([n for q, n in bl], axis=0)
    rng = np.random.default_rng(11)
    hs = [0.13 + 0.07 * math.sin(i * 0.9) + rng.uniform(-0.02, 0.04) for i in range(len(path))]
    hs[0] *= 0.3; hs[-1] *= 0.4
    notch = [(i, rng.uniform(0.6, 0.93)) for i in range(2, len(path) - 1) if (i % 3 == 0 or rng.random() < 0.18)]
    fin = RD.fin_strip(path, hs, th=0.007, up=(0, 1, 0.1), lean=(0, 0.05, -0.25), notch=notch, seed=5)
    C.skin(RD.skin_to(fin, seg + ['head'], sharp=4.0), 'fin', smooth=180)
    # head: armoured spade + two jaw plates that open sideways
    shield = P.tubep([hj - hd * 0.10, hj - hd * 0.05, hj + hd * 0.10, hj + hd * 0.25, hj + hd * 0.36, hj + hd * 0.41],
                     [0.02, 0.075, 0.085, 0.07, 0.045, 0.012], [0.04, 0.125, 0.17, 0.195, 0.16, 0.06], m=14, n=14, up=tuple(up), power=2.7)
    shield.v += up * 0.02
    shield.dent(hj + hd * 0.2 + up * 0.09, 0.09, 0.012)          # brow boss
    C.rigid(shield, 'head', 'armour', smooth=50)
    C.rigid(P.blob(hj + hd * 0.10 - up * 0.05, 0.10, 0.12, 0.06, n=10, m=6, rot=M.rot_to((0, -1, 0), hd)), 'head', 'maw', smooth=180)
    sb = None
    for s, sg in (('l', 1), ('r', -1)):
        a = hj + hd * 0.03 + V(sg * 0.075, 0, 0) - up * 0.055
        b = hj + hd * 0.385 + V(sg * 0.045, 0, 0) - up * 0.062
        pl = P.tubep([a, M.lerp(a, b, 0.35) + V(sg * 0.035, 0, 0), M.lerp(a, b, 0.75) + V(sg * 0.03, 0, 0), b],
                     [0.05, 0.052, 0.04, 0.012], [0.075, 0.085, 0.07, 0.03], m=10, n=12, up=tuple(up), power=2.5)
        C.rigid(pl, 'jaw_' + s, 'armour', smooth=50)
        pb = P.bvh_of(pl)
        gum = [M.lerp(a, b, t) + V(-sg * 0.045, 0, 0) + up * 0.03 for t in np.linspace(0.3, 0.92, 6)]
        C.rigid(P.tooth_row(pb, gum, V(-sg * 0.9, 0, 0) + up * 0.5, 0.03, 0.008, seed=3 + sg, sink=0.4), 'jaw_' + s, 'teeth')
        C.rigid(P.blob(hj + hd * 0.17 + V(sg * 0.105, 0, 0) + up * 0.085, 0.014, 0.014, 0.012, n=7, m=4), 'head', 'teeth', smooth=180)   # tiny pale eye
    # churned ground round the hole
    C.rigid(RD.mound(0.20, 0.30, n=12, seed=3), 'root', 'mud', free=True)

    def jaws(p, a):
        p.setR('jaw_l', rot_axis(up, a)); p.setR('jaw_r', rot_axis(up, -a))
    K.idle(extra=lambda p, u: jaws(p, 3 + 5 * bump(u, 0.5, 0.58, 0.66, 0.78)))
    K.plough('Walk', 1.1, 1.2, lean=14.0, weave=6.0, extra=lambda p, u: jaws(p, 3))
    K.plough('Run', 3.0, 0.8, lean=24.0, weave=9.0, bob=0.03, sink=0.1, head=-16.0, extra=lambda p, u: jaws(p, 8 + 4 * osc(u, 2)))
    K.lunge(dur=0.9, hit=0.45, back=20.0, fwd=74.0, head=8.0,
            extra=lambda p, u, w, s: jaws(p, 40 * bump(u, 0.08, 0.3, 0.47, 0.52) + 3))
    K.sweep(dur=1.2, hit=0.6, low=74.0, arc=100.0, head=-6.0, extra=lambda p, u, dn, sw: jaws(p, 3 + 14 * dn))
    K.hit(extra=lambda p, u, k: jaws(p, 3 + 22 * k))
    K.death_topple(radius=0.15, extra=lambda p, u, f: jaws(p, 3 + 24 * f))
    K.spawn_erupt(extra=lambda p, u, r: jaws(p, 3 + 30 * (1 - r)))
    return C.finalize(bake=bake, notes="rig D proof (column)")


# ================================================================================================ loopers
def looper_clips(C, K, walk, run, bite_reach=0.72, leap=(1.15, 0.5), mouth=None, death_roll=90.0, idle_lift=0.12):
    """the shared clip set of the inchworm rigs. walk / run = (v, T, duty, lift, lift_rear); mouth(p, open 0..1)"""
    Lh = K.L
    rr0, fr0 = K.rest_ends(0.42)
    mo = mouth or (lambda p, a: None)

    def idle(p, u):
        q = fr0 + V(0.05 * Lh * osc(u, 1), 0.06 * Lh * (0.5 + 0.5 * osc(u, 2, 0.1)) + 0.02 * Lh, idle_lift * Lh * (0.5 + 0.5 * osc(u, 2, 0.3)))
        K.place(p, rr0, q)
        if K.has_head:
            p.rot('head', x=-25 * (0.5 + 0.5 * osc(u, 2, 0.3)), z=12 * osc(u, 1))
        mo(p, 0.25 + 0.25 * osc(u, 3))
    C.clip('Idle', 2.4, idle)
    K.gait('Walk', walk[0], walk[1], walk[2], lift=walk[3], lift_r=walk[4], extra=lambda p, u, a, b: mo(p, 0.2))
    K.gait('Run', run[0], run[1], run[2], lift=run[3], lift_r=run[4], head=-20.0, extra=lambda p, u, a, b: mo(p, 0.3 + 0.5 * a))

    def bite(p, u):
        hu = 0.35 / 0.7
        w = bump(u, 0.0, hu * 0.55, hu * 0.7, hu)
        s = bump(u, hu * 0.7, hu, hu + 0.1, 0.95)
        q = fr0 + V(0, 0.18 * Lh * w - (bite_reach - 0.42) * Lh * s, 0.42 * Lh * w + 0.14 * Lh * s)
        K.place(p, rr0, q)
        if K.has_head:
            p.rot('head', x=-50 * w + 25 * s)
        mo(p, bump(u, hu * 0.2, hu * 0.7, hu - 0.02, hu + 0.04))
    C.clip('Attack1', 0.7, bite, hit=0.35)
    K.reach('Attack1')

    def latch(p, u):
        hu = 0.5 / 1.1
        cr = bump(u, 0.0, hu * 0.4, hu * 0.5, hu * 0.7)                     # gathers into a tight loop
        fl = bump(u, hu * 0.5, hu, hu + 0.22, hu + 0.42)                    # in the air / latched
        arc = math.sin(math.pi * min(1.0, max(0.0, (u - hu * 0.5) / max(hu * 0.5, 1e-3)))) if u < hu else 0.0
        wr = bump(u, hu, hu + 0.03, hu + 0.2, hu + 0.26) * osc(u, 9)
        f = fr0 + V(0, 0.2 * Lh * cr - (leap[0] - 0.42) * Lh * fl, leap[1] * Lh * fl + 0.25 * Lh * arc)
        r = rr0 + V(0.05 * Lh * wr, -0.3 * Lh * cr - (leap[0] - 0.42 - 0.25) * Lh * fl, (leap[1] - 0.22) * Lh * fl + 0.3 * Lh * arc)
        K.place(p, r, f, yaw=10 * wr)
        if K.has_head:
            p.rot('head', x=-30 * cr + 15 * fl)
        mo(p, bump(u, hu * 0.5, hu * 0.9, hu - 0.01, hu + 0.03) + 0.4 * abs(wr))
    C.clip('Attack2', 1.1, latch, hit=0.5)
    K.reach('Attack2')

    def hit(p, u):
        k = bump(u, 0.0, 0.2, 0.3, 1.0)
        K.place(p, rr0 + V(0, 0.03 * Lh * k, 0), fr0 + V(0.12 * Lh * k, 0.3 * Lh * k, 0.3 * Lh * k), roll=25 * k)
        if K.has_head:
            p.rot('head', x=-35 * k)
        mo(p, 0.8 * k)
    C.clip('Hit', 0.4, hit)

    def death(p, u):
        a = bump(u, 0.0, 0.15, 0.3, 0.5)                                    # rears and writhes
        f = sm(u, 0.25, 0.6)                                                 # rolls over into a limp curl
        tw = (1 - f) * a * osc(u, 6)
        r = rr0 + V(0.1 * Lh * f, -0.12 * Lh * f, 0)
        q = fr0 + V(0.34 * Lh * f + 0.1 * Lh * tw, 0.3 * Lh * a + 0.28 * Lh * f, 0.4 * Lh * a * (1 - f))
        K.place(p, r, q, roll=death_roll * f)
        if K.has_head:
            p.rot('head', x=-30 * a + 10 * f, z=20 * f)
        mo(p, 0.6 * a + 0.5 * f)
    C.clip('Death', 1.4, death, floor=K.seg[0], settle=(0.5, 0.65))

    def spawn(p, u):
        r = sm(u, 0.0, 0.55)
        sh = bump(u, 0.55, 0.65, 0.8, 0.95)
        dz = -0.6 * Lh * (1 - r)
        K.place(p, rr0 + V(0, -0.25 * Lh * (1 - r), dz * 1.2),
                fr0 + V(0.05 * Lh * sh * osc(u, 7), 0.25 * Lh * (1 - r), dz * 0.5 + 0.35 * Lh * bump(u, 0.1, 0.4, 0.5, 0.75)))
        if K.has_head:
            p.rot('head', x=-40 * (1 - r))
        mo(p, 1 - r)
    C.clip('Spawn', 1.1, spawn, under_ok=True)


@reg
def lurk_leech(bake=True):
    """Weeper: arm-length flattened leech, leaf-shaped, round sucker mouth, a row of gill-flutes down each side;
    moves in fast vertical loops"""
    Lh, n, z = 1.0, 8, 0.04
    C = B.Beast('mon_lurk_leech', RD.looper_skel(Lh, n, z, head=0.07), budget=4000, bone_budget=28, mb=2.5, tex=1024)
    C.meta.update(family='lurk', rig='D', scale=0.6, game_speed=5.0)
    K = RD.Looper(C)
    C.look('skin', 'slime', 'skin_body', color=(0.075, 0.062, 0.055), color2=(0.025, 0.028, 0.03), belly=(0.24, 0.13, 0.07), axis=(0, 1, 0),
           ring=46.0, ring_h=1.0, wart=80.0, rough=0.16, dry=0.2)
    C.look('maw', 'slime', 'skin_body', color=(0.30, 0.10, 0.08), color2=(0.12, 0.03, 0.03), ring=0.0, rough=0.12)
    C.look('hole', 'flat', 'skin_body', color=(0.008, 0.006, 0.006), rough=0.25)
    C.look('teeth', 'bone', 'skin_body', color=(0.62, 0.58, 0.48), dirt_lo=-2.0, dirt_hi=-1.5)
    ys = np.linspace(Lh / 2, -Lh / 2, 9)
    pts = [V(0, y, z) for y in ys]
    rv = [0.012, 0.030, 0.040, 0.043, 0.041, 0.036, 0.031, 0.029, 0.03]
    rh = [0.030, 0.072, 0.097, 0.107, 0.100, 0.085, 0.066, 0.052, 0.048]
    bones = K.seg + ['head']
    body = RD.chain_tube(pts + [V(0, -Lh / 2 - 0.03, z)], rv + [0.022], rh + [0.035], bones=bones, m=40, n=12, up=(0, 0, 1), ridges=(34, 0.004, 0.0))
    body.v[:, 2] = np.maximum(body.v[:, 2], 0.004)                    # flat sole
    C.skin(body, 'skin')
    # sucker mouth: a fleshy ring round a dark throat with a circle of denticles
    hy = -Lh / 2 - 0.035
    C.rigid(P.ring((0, hy, z), 0.042, 0.016, axis=(0, 1, 0), n=14, m=6), 'head', 'skin', smooth=180)
    C.rigid(P.blob((0, hy + 0.012, z), 0.04, 0.012, 0.04, n=10, m=5), 'head', 'maw', smooth=180)
    den = [P.cone(V(0.03 * math.cos(a), hy + 0.006, z + 0.03 * math.sin(a)), (-math.cos(a) * 0.6, -1, -math.sin(a) * 0.6), 0.016, 0.004, n=4)
           for a in np.linspace(0, 2 * math.pi, 9)[:-1]]
    C.rigid(M.join(den), 'head', 'teeth', smooth=50)
    C.rigid(P.ring((0, Lh / 2 - 0.012, z - 0.012), 0.024, 0.01, axis=(0, 0.8, -0.6), n=10, m=5), K.seg[0], 'skin', smooth=180)   # rear sucker
    # gill-flutes: a row of dark holes down each flank
    bv = P.bvh_of(body)
    holes = []
    for sg in (1, -1):
        for y in np.linspace(0.30, -0.36, 9):
            x = sg * float(np.interp(y, ys[::-1], rh[::-1])) * 0.74
            q, nr = P.surf(bv, (x, y, 0.4), (0, 0, -1), 1.0)
            if q is not None:
                holes.append(P.blob(q + nr * 0.001, 0.009, 0.014, 0.004, n=6, m=4, rot=M.rot_to((0, 0, 1), nr)))
    hp = M.join(holes)
    C.skin(RD.skin_to(hp, bones, sharp=3.0), 'hole', smooth=180)

    def mouth(p, a):
        p.scl('head', 1 + 0.45 * a)
    looper_clips(C, K, walk=(1.7, 0.4, 0.5, 0.012, 0.012), run=(8.333, 0.2667, 0.24, 0.26, 0.2), mouth=mouth)
    return C.finalize(bake=bake, notes="looper")


@reg
def sexton_grub(bake=True):
    """Grave-grub (listed under rig C, built on the looper): fat ridged larva, a string of pale beads behind a hard
    black head-capsule with two short hook jaws; humping inchworm arcs"""
    import rig_c
    Lh, n, z = 0.62, 7, 0.075
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
            stubs.append(P.cone((sg * 0.035, y, 0.03), (sg * 0.5, -0.2, -1), 0.04, 0.012, n=5, curl=(0, -1, 0), seg=2))
    st = M.join(stubs)
    C.skin(RD.skin_to(st, bones, sharp=3.0), 'leg', smooth=50)

    def mouth(p, a):
        p.rot('mandible_l', z=30 * a); p.rot('mandible_r', z=-30 * a)
    looper_clips(C, K, walk=(1.25, 0.3333, 0.5, 0.01, 0.01), run=(6.0, 0.2333, 0.24, 0.17, 0.13), mouth=mouth, bite_reach=0.7, leap=(1.5, 0.75))
    return C.finalize(bake=bake, notes="looper (rig C family)")
