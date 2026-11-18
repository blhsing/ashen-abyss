"""Rig C creatures built by helper H1: the 13 arthropods after the sexton_beetle proof.
One function per bestiary type id, registered in BUILD. Shared helpers of this module are at the top."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis, euler
import bparts as P
import rig_c as RC

BUILD = {}
TAU = 2 * math.pi


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


def std_meta(C, family, scale, speed, **kw):
    C.meta.update(family=family, rig='C', scale=scale, game_speed=speed)
    C.meta.update(kw)


# ================================================================================================ helpers
def core(C, part, bone, look, smooth=180):
    """the main body of a creature as SKINNED geometry on one bone: the audit takes skinned geometry as the host that
    everything else has to hang on (otherwise it starts from whatever rigid piece is largest)"""
    part.W = M.rigid_W(len(part.v), bone)
    return C.skin(part, look, smooth=smooth)


def enlarge(sk, legs, K):
    """scale a finished skeleton description and its leg definitions by K (creatures that turned out too small on
    screen). Returns the UNSCALED leg definitions: build the geometry in the unscaled numbers, grow() scales it"""
    legs0 = [dict(lg) for lg in legs]
    for n in sk.names:
        sk.head[n] = sk.head[n] * K
        sk.tail[n] = sk.tail[n] * K
    for lg in legs:
        for k in ('root', 'knee', 'ankle', 'toe'):
            if lg[k] is not None:
                lg[k] = lg[k] * K
        lg['r'] = tuple(x * K for x in lg['r'])
    return legs0


def grow(C, K):
    """every part added from now on is scaled by K about the origin (weights must not come from bone distances)"""
    r0, s0 = C.rigid, C.skin

    def rigid(part, bone, look, smooth=None, free=False):
        part.v = part.v * K
        return r0(part, bone, look, smooth=smooth, free=free)

    def skin(part, look, smooth=None):
        part.v = part.v * K
        return s0(part, look, smooth=smooth)
    C.rigid, C.skin = rigid, skin


def path_t(part, path, n=64):
    """0..1 position of every vertex of a Part along a polyline (nearest of n samples)"""
    path = np.asarray(path, dtype=float)
    Pd = M.polyline(path, n)
    d = np.linalg.norm(part.v[:, None, :] - Pd[None, :, :], axis=2)
    return d.argmin(1) / float(n - 1)


def lock(path, wide, thin, m=7, up=(0, 0, 1)):
    """flat tapering strand (hair lock, rag, feather): `wide` across the `up` side of the path, `thin` the other way"""
    q = P.tubep(path, wide, thin, m=m, n=4, up=up)
    q.smooth = 180.0
    return q


def wpath(part, path, stops):
    """skin a Part along a path: stops = [(t, {bone: w}), ...]"""
    return M.weights_by(part, path_t(part, path), stops)


def domed_plate(outline, height, rings=5, th=0.012, power=1.0, top=0.12, flare=0.0):
    """armour plate from a 2D outline (x, y): a rolled rim of thickness th, then rings shrinking towards the centroid
    while they rise to `height` (a dome that keeps the outline's notches). Origin height 0 = underside of the rim"""
    o = np.asarray(outline, dtype=float)
    c = o.mean(0)
    R = [[(c[0] + (x - c[0]) * (1 + flare), c[1] + (y - c[1]) * (1 + flare), 0.0) for x, y in o],
         [(x, y, th) for x, y in o]]
    for k in range(1, rings + 1):
        a = (math.pi / 2) * k / rings
        s = max(top, math.cos(a) ** power) if k == rings else math.cos(a) ** power
        s = max(s, top)
        R.append([(c[0] + (x - c[0]) * s, c[1] + (y - c[1]) * s, th + height * math.sin(a)) for x, y in o])
    p = M.loft(np.array(R), cap0=True, cap1=True)
    p.smooth = 50.0
    return p


def mirror_outline(half):
    """half outline (x >= 0, from the front centre to the rear centre) -> closed outline"""
    return list(half) + [(-x, y) for x, y in reversed(half[1:-1])]


def helm_shell(c, rx_, ry_, rz_, ncol=20, th=0.012, face_w=48.0, face_h=30.0, rim=0.055, neck=0.05, dents=(), rows=6):
    """open helmet: a dome on the brow line z = c[2], a short skirt below it at the sides and a longer neck guard behind,
    an arched face opening cut up into the dome at the front (-Y). Closed slab of thickness th"""
    cx, cy, z0 = c
    rs = 2
    G = np.zeros((rows + rs + 1, ncol, 3))
    for j in range(ncol):
        a = TAU * j / ncol
        cs, sn = math.cos(a), math.sin(a)
        df = min(1.0, abs(((math.degrees(a) + 90 + 180) % 360) - 180) / face_w)       # 0 = front centre .. 1 = outside the opening
        ph_end = math.radians(face_h) * (1 - sm(df, 0.55, 1.0))
        back = max(0.0, sn) ** 6
        k = sm(df, 0.75, 1.0)
        depth = (rim + neck * back) * k
        for i in range(rows + 1):
            ph = math.pi / 2 + (ph_end - math.pi / 2) * (i / rows)
            G[i, j] = (cx + rx_ * math.cos(ph) * cs, cy + ry_ * math.cos(ph) * sn, z0 + rz_ * math.sin(ph))
        e = G[rows, j].copy()
        for q in range(1, rs + 1):
            f = q / rs
            fl = 1.0 + (0.12 * back + 0.03) * f * k
            G[rows + q, j] = (cx + (e[0] - cx) * fl, cy + (e[1] - cy) * fl, e[2] - depth * f)
    p = M.sheet(G, closed_u=True).cleanup()
    cen = V(cx, cy, z0 + rz_ * 0.3)
    for dc, rad, amt in dents:
        p.dent(dc, rad, amt, direction=cen - V(dc))
    p.solidify(th)
    p.smooth = 60.0
    return p


def dome_pt(c, rx_, ry_, rz_, a_deg, ph_deg, out=0.0):
    """point on the helmet dome: a = angle round Z (0 = +X, -90 = front), ph = latitude"""
    a = math.radians(a_deg); ph = math.radians(ph_deg)
    n = unit(V(math.cos(ph) * math.cos(a) / rx_, math.cos(ph) * math.sin(a) / ry_, math.sin(ph) / rz_))
    return V(c[0] + rx_ * math.cos(ph) * math.cos(a), c[1] + ry_ * math.cos(ph) * math.sin(a), c[2] + rz_ * math.sin(ph)) + n * out


# ================================================================================================ rust_helm
@reg
def rust_helm(bake=True):
    """Helm-hermit: a dented brass soldier's helmet with a nose-guard and a tattered horsehair crest, running on six
    thin legs; two small pincers poke out of the face opening"""
    K = 1.4                                # built in helmet-size numbers, then enlarged: at life size it was a dot on screen
    legs = RC.radial_legs(3, -0.11, 0.11, 0.045, 0.18, 0.32, 0.245, fan=(-28, 34), knee_out=0.58, toe=0.035, ankle_h=0.04,
                          r=(0.021, 0.015, 0.0095))
    hc = (0.0, 0.02, 0.30)                 # helmet: centre of the brow line
    hr = (0.20, 0.24, 0.25)

    def extra(sk):
        sk.bone('helm', (0, 0.02, 0.33), (0, 0.02, 0.53), 'body')
        sk.bone('crest1', (0, 0.0, 0.56), (0, 0.10, 0.70), 'helm')
        sk.bone('crest2', (0, 0.24, 0.46), (0, 0.36, 0.24), 'helm')
    sk = RC.arth_skel(body=((0, 0.12, 0.24), (0, -0.14, 0.24)), head=None, legs=legs,
                      mand=((0.055, -0.165, 0.225), (0.03, -0.38, 0.19)), extra=extra)
    legs0 = enlarge(sk, legs, K)
    C = B.Beast('mon_rust_helm', sk, budget=4000, bone_budget=28, mb=2.5, tex=1024)
    std_meta(C, 'rust', 0.7, 5.2)
    grow(C, K)
    A = RC.Arth(C, legs, pivot=(0, 0.0, 0.22 * K))
    C.look('brass', 'metal', 'metal_brass', color=(0.36, 0.25, 0.08), rust=(0.06, 0.16, 0.12), rust2=(0.035, 0.07, 0.055), amount=0.5,
           edge_color=(0.72, 0.56, 0.26), rough=0.38, hammer=30.0)
    C.look('limb', 'chitin', 'bone_body', color=(0.20, 0.15, 0.09), color2=(0.30, 0.24, 0.15), edge_color=(0.42, 0.34, 0.22), rough=0.4,
           dust=0.5, dust_color=(0.30, 0.25, 0.17), axis=(1, 0, 0))
    C.look('soft', 'slime', 'bone_body', color=(0.07, 0.055, 0.05), color2=(0.03, 0.025, 0.025), ring=0.0, rough=0.3)
    C.look('eye', 'glass', 'bone_body', color=(0.02, 0.015, 0.03), rough=0.08)
    C.look('crest', 'bfur', 'hair_crest', color=(0.17, 0.035, 0.025), tip=(0.30, 0.10, 0.06), flow=(0, 0.6, -0.8), scale=80.0, ash=0.5,
           ash_color=(0.30, 0.26, 0.20))
    # ---- the helmet
    dents = [((0.15, 0.10, 0.46), 0.07, 0.022), ((-0.12, -0.08, 0.49), 0.06, 0.018), ((-0.17, 0.14, 0.37), 0.05, 0.014),
             ((0.05, 0.22, 0.40), 0.05, 0.012)]
    C.rigid(helm_shell(hc, *hr, ncol=20, th=0.012, face_w=42.0, face_h=26.0, rim=0.028, neck=0.075, dents=dents), 'helm', 'brass', smooth=60)
    band = M.band((hc[0], hc[1], hc[2] + 0.004), hr[0] + 0.004, hr[1] + 0.004, 0.036, 0.012, n=22, a0=-38.0, a1=218.0)
    C.rigid(band, 'helm', 'brass', smooth=30)
    nose = P.tubep([dome_pt(hc, *hr, -90, 34), dome_pt(hc, *hr, -90, 20, 0.006), (0, -0.238, 0.30), (0, -0.246, 0.205)],
                   [0.009, 0.009, 0.009, 0.008], [0.04, 0.03, 0.027, 0.038], m=7, n=4)
    nose.smooth = 30.0
    C.rigid(nose, 'helm', 'brass')
    # crest: a low brass comb fore and aft over the crown, horsehair standing on it and hanging down behind
    ridge = [dome_pt(hc, *hr, -90, 52, -0.004), dome_pt(hc, *hr, -90, 72, -0.004), dome_pt(hc, *hr, 90, 90, -0.004),
             dome_pt(hc, *hr, 90, 66, -0.004), dome_pt(hc, *hr, 90, 40, -0.004), dome_pt(hc, *hr, 90, 22, -0.004)]
    comb = P.tubep(ridge, 0.03, 0.011, m=10, n=4)
    comb.smooth = 30.0
    C.rigid(comb, 'helm', 'brass')
    rp = M.polyline(np.array(ridge), 40)
    rng = np.random.default_rng(7)
    locks = []
    for i in range(22):                                                  # the brush standing on the comb
        if i in (6, 7, 15):
            continue                                                    # gaps: the crest is moth-eaten
        t = i / 21.0
        b_ = rp[int(round(t * 30))] + V(rng.uniform(-0.006, 0.006), 0, 0.012)
        ln = (0.11 + 0.05 * math.sin(t * math.pi)) * rng.uniform(0.6, 1.15)
        d0 = unit(V(rng.uniform(-0.18, 0.18), 0.35 + 0.5 * t, 1.0 - 0.7 * t))
        p1 = b_ + d0 * ln * 0.55
        p2 = p1 + unit(d0 + V(rng.uniform(-0.2, 0.2), 0.7, -0.35)) * ln * 0.5
        pth = [b_ - d0 * 0.02, p1, p2]
        q = lock(pth, [0.017, 0.019, 0.003], [0.007, 0.007, 0.002], m=5)
        wpath(q, pth, [(0.0, {'helm': 1.0}), (0.3, {'helm': 0.6, 'crest1': 0.4}), (1.0, {'crest1': 1.0})])
        locks.append(q)
    for i in range(9):                                                   # the long tail hanging down behind
        b_ = rp[31 + i] + V(rng.uniform(-0.015, 0.015), 0, 0.008)
        ln = rng.uniform(0.16, 0.27)
        sx = rng.uniform(-0.05, 0.05)
        pth = [b_ - V(0, 0.01, 0.01), b_ + V(sx * 0.4, 0.07, 0.03), b_ + V(sx * 0.8, 0.13, -ln * 0.45), b_ + V(sx, 0.15 + 0.04 * rng.random(), -ln)]
        q = lock(pth, [0.016, 0.02, 0.016, 0.003], [0.007, 0.008, 0.006, 0.002], m=7)
        wpath(q, pth, [(0.0, {'helm': 1.0}), (0.25, {'helm': 0.6, 'crest2': 0.4}), (0.7, {'crest2': 1.0})])
        locks.append(q)
    cr = M.join(locks)
    cr.smooth = 180.0
    C.skin(cr, 'crest')
    # ---- the hermit inside
    core(C, P.blob((0, 0.0, 0.27), 0.165, 0.2, 0.14, n=12, m=7), 'body', 'soft')
    C.rigid(P.blob((0, 0.02, 0.44), 0.10, 0.12, 0.105, n=10, m=6), 'helm', 'soft', smooth=180)        # its back, wedged into the crown
    tip = V(0.03, -0.38, 0.19)                 # unscaled (geometry), tipK = on the enlarged skeleton
    tipK = tip * K
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(P.blob((sg * 0.045, -0.162, 0.335), 0.02, 0.02, 0.02, n=7, m=5), 'body', 'eye', smooth=180)
        C.rigid(RC.mandible((sg * 0.055, -0.165, 0.225), tip * (sg, 1, 1), 0.021, curve=0.26, n=6, teeth=2, side=sg), 'mandible_' + s, 'limb')
    RC.add_legs(C, legs0, 'limb', n=5, claw=0.4)

    # ---- clips
    def crest(p, u, lean=0.0, amp=3.0, n=2):
        p.rot('crest1', x=-lean + amp * osc(u, n, 0.1), y=amp * 0.7 * osc(u, n, 0.35))
        p.rot('crest2', x=lean * 1.4 + amp * 1.6 * osc(u, n, -0.1), y=amp * osc(u, n, 0.2))

    def idle_x(p, u):
        p.rot('helm', x=2.5 * osc(u, 2, 0.1), y=1.5 * osc(u, 1, 0.3))
        crest(p, u, 0.0, 3.0, 2)
    A.idle(dur=2.6, breath=0.008, feel=0.0, jaw=12.0, sway=0.006, extra=idle_x)
    A.gait('Walk', 2.3, 0.2667, 0.5, RC.tripod(3), lift=0.07, bob=0.007, roll=2.0, yaw=2.5, jaw=5.0,
           extra=lambda p, u: (p.rot('helm', x=2.0 * osc(u, 2, 0.2), y=3.0 * osc(u, 1, 0.15)), crest(p, u, 6.0, 5.0, 2)))
    A.gait('Run', round(5.2 / 0.7, 3), 0.2, 0.24, RC.tripod(3), lift=0.10, bob=0.011, roll=2.5, yaw=2.0, pitch=4.0, crouch=0.017, jaw=3.0,
           extra=lambda p, u: (p.rot('helm', x=-4.0 + 3.0 * osc(u, 2, 0.2), y=3.0 * osc(u, 1, 0.15)), crest(p, u, 22.0, 7.0, 2)))
    A.snap(dur=0.6, hit=0.3, reach=0.24, gape=34.0, lift=6.0, drop=0.014,
           extra=lambda p, u, w, s: (p.rot('helm', x=-8 * w + 9 * s), crest(p, u, 10 * s - 8 * w, 2.0, 3)))
    C.reach('Attack1', 'mandible_l', tipK)

    def clang(p, u, cr, fl):
        hu = 0.48 / 0.85
        ln = bump(u, hu - 0.03, hu + 0.02, hu + 0.06, hu + 0.3)
        rt = bump(u, hu, hu + 0.04, hu + 0.1, 0.98) * osc(u, 7)
        p.rot('helm', x=-10 * fl * (1 - ln) + 17 * ln + 5 * rt, y=4 * rt)
        crest(p, u, 18 * fl - 14 * ln, 3.0, 3)
    A.hop('Attack2', dur=0.85, hit=0.48, fwd=0.50, up=0.36, gape=22.0, pitch=16.0, extra=clang)
    C.reach('Attack2', 'mandible_l', tipK)
    A.hit(dur=0.4, back=0.085, extra=lambda p, u, k: (p.rot('helm', x=-9 * k, y=9 * k), crest(p, u, -8 * k, 4.0, 3)))

    def die(p, u, f):                       # the brass drops with a clang and rocks on its side, the crest goes limp
        rk = bump(u, 0.5, 0.56, 0.6, 0.95) * osc(u, 5)
        p.rot('body', y=5 * rk)
        p.rot('helm', x=4 * f)
        p.rot('crest1', y=-30 * f, x=10 * f)
        p.rot('crest2', y=-45 * f)
    A.death_flip(dur=1.3, side=1.0, roll=112.0, up=0.2, curl=(100.0, 80.0, 30.0), extra=die)
    A.spawn_dig(dur=1.0, depth=1.15, shake=9.0)
    return C.finalize(bake=bake, notes="swarm budget; built 1.4 x helmet size (0.47 m long in the game) to read on screen; the helmet is its own bone (helm), the crest has two (crest1 standing, crest2 tail)")


# ================================================================================================ shroud_weaver
def spider_front(C, c, r, look, eye, bone='body', eyes=True):
    """cephalothorax: a flat carapace with a raised head region and (optionally) eight eyes in two rows"""
    cx, cy, cz = c
    C.rigid(P.blob(c, r[0], r[1], r[2], n=14, m=7), bone, look, smooth=180)
    hy = cy - r[1] * 0.55
    C.rigid(P.blob((cx, hy, cz + r[2] * 0.25), r[0] * 0.62, r[1] * 0.45, r[2] * 0.85, n=12, m=6), bone, look, smooth=180)
    if eyes:
        e = []
        fy = hy - r[1] * 0.40
        for sg in (1, -1):
            for fx, dy, fz, er in ((0.14, 0.0, 0.55, 0.095), (0.36, 0.09, 0.42, 0.08), (0.17, 0.143, 0.86, 0.072), (0.42, 0.186, 0.72, 0.068)):
                q = er * r[0]
                e.append(P.blob((cx + sg * r[0] * fx, fy + dy * r[1], cz + r[2] * fz), q, q, q, n=7, m=5))
        C.rigid(M.join(e), bone, eye, smooth=180)


def fangs(C, joint, tip, r, look, fang=0.09, bone='mandible'):
    """chelicerae: a stout base from the joint to the tip (left side given) and a curved fang folding inwards"""
    j = V(joint); t = V(tip)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        m = V(sg, 1, 1)
        base = P.seg_tube(j * m - unit(t - j) * m * r * 0.8, t * m, r, r * 0.8, n=7, bulge=1.15)
        fg = P.cone(t * m - V(0, 0, -0.3 * r), V(-sg * 0.5, 0.25, -1.0), fang, r * 0.5, n=5, curl=V(-sg, 0.3, 0.0), seg=3)
        C.rigid(M.join([base, fg]), '%s_%s' % (bone, s), look, smooth=50)


@reg
def shroud_weaver(bake=True):
    """Shroud-weaver: wolf-sized spider, a long narrow abdomen held up and forward over its back like a distaff with
    the spinnerets pointing at the target; the two front legs are flat combs holding a half-wound grey shroud"""
    R = (0.045, 0.032, 0.017)
    legs = [RC.leg_def((0.16, -0.13, 0.47), (0.46, -0.37, 0.92), (0.775, -0.61, 0.08), (0.815, -0.645, 0.0), r=R),
            RC.leg_def((0.19, 0.0, 0.47), (0.576, 0.054, 0.90), (0.982, 0.111, 0.08), (1.03, 0.118, 0.0), r=R),
            RC.leg_def((0.16, 0.13, 0.47), (0.406, 0.445, 0.94), (0.677, 0.792, 0.08), (0.708, 0.831, 0.0), r=R)]
    AB = [(0, 0.18, 0.52), (0, 0.38, 0.74), (0, 0.33, 1.02), (0, 0.10, 1.22), (0, -0.20, 1.27)]
    sh0, el0, tp0 = V(0.15, -0.20, 0.50), V(0.33, -0.44, 0.70), V(0.25, -0.82, 0.56)

    def extra(sk):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            m = (sg, 1, 1)
            sk.bone('comb_%s_a' % s, sh0 * m, el0 * m, 'body')
            sk.bone('comb_%s_b' % s, el0 * m, tp0 * m, 'comb_%s_a' % s)
        sk.bone('shroud1', (0, -0.70, 0.60), (0, -0.70, 0.36), 'body')
        sk.bone('shroud2', (0, -0.70, 0.36), (0, -0.70, 0.10), 'shroud1')
    sk = RC.arth_skel(body=((0, 0.18, 0.50), (0, -0.30, 0.50)), head=None, legs=legs, abd=AB,
                      mand=((0.06, -0.30, 0.45), (0.05, -0.37, 0.32)), ant=[(0.11, -0.28, 0.47), (0.155, -0.45, 0.40)], extra=extra)
    C = B.Beast('mon_shroud_weaver', sk)
    std_meta(C, 'shroud', 1.0, 4.0)
    A = RC.Arth(C, legs, pivot=(0, 0.0, 0.48))
    A.jaws = lambda p, deg: (p.rot('mandible_l', x=-0.5 * deg, y=-deg), p.rot('mandible_r', x=-0.5 * deg, y=deg))     # fangs hang down: they open sideways
    C.look('carap', 'chitin', 'bone_spider', color=(0.028, 0.025, 0.027), color2=(0.065, 0.055, 0.055), rough=0.32, dust=0.5,
           dust_color=(0.20, 0.19, 0.18), edge=0.5)
    C.look('limb', 'chitin', 'bone_spider', color=(0.035, 0.03, 0.03), edge_color=(0.26, 0.23, 0.20), rough=0.36, pit=170.0, axis=(1, 0, 0),
           dust=0.3, dust_color=(0.20, 0.19, 0.18))
    C.look('pelt', 'bfur', 'bone_spider', color=(0.06, 0.055, 0.055), tip=(0.21, 0.20, 0.19), flow=(0, 0.3, -1.0), scale=95.0, ash=0.7,
           ash_color=(0.26, 0.25, 0.24))
    C.look('eye', 'glass', 'bone_spider', color=(0.012, 0.012, 0.016), rough=0.06)
    C.look('silk', 'cloth', 'cloth_shroud', color=(0.40, 0.39, 0.36), dirt=(0.14, 0.13, 0.12), weave='linen', fold=18.0, hem_hi=0.3)
    C.look('spin', 'glow', 'emis_spin', color=(0.26, 0.30, 0.25), emit=(0.62, 0.86, 0.66), strength=1.6, vein_scale=30.0, scale=14.0)
    # ---- body
    spider_front(C, (0, -0.05, 0.50), (0.21, 0.28, 0.13), 'carap', 'eye')
    fangs(C, (0.06, -0.30, 0.45), (0.05, -0.37, 0.32), 0.036, 'limb')
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(RC.antenna([(sg * 0.11, -0.27, 0.47), (sg * 0.145, -0.37, 0.455), (sg * 0.155, -0.45, 0.40)], 0.022, 0.015, n=6), 'antenna_' + s, 'limb')
    # the distaff: abdomen up and forward over the back, wound with silk, spinnerets at the tip
    ab = P.tubep(AB, [0.05, 0.085, 0.105, 0.095, 0.045], [0.055, 0.09, 0.11, 0.10, 0.045], m=18, n=12)
    abn = ['body', 'abdomen1', 'abdomen2', 'abdomen3', 'abdomen4']
    B.wbones(ab, abn, sharp=3.0, k=3)
    ab.smooth = 180.0
    C.skin(ab, 'pelt')
    Pa = M.polyline(np.array(AB, dtype=float), 41)
    Ra = P.prof([0.05, 0.085, 0.105, 0.095, 0.045], 41)           # only approximately the tube's own radii: generous margin
    bulge = np.array([0.02, 0.06, 0.075, 0.07, 0.05, 0.02])
    wd = P.tubep(Pa[14:32:3], list(Ra[14:32:3] + bulge), list(Ra[14:32:3] + bulge + 0.008), m=44, n=12)
    tw = path_t(wd, Pa[14:32:3], 200)
    wd.inflate(0.005 * np.sin(tw * 15 * TAU) * np.clip(np.minimum(tw, 1 - tw) / 0.08, 0, 1))        # turns of thread
    wd.rough(0.004, 22.0, seed=2)
    B.wbones(wd, abn, sharp=3.0, k=3)
    wd.smooth = 180.0
    C.skin(wd, 'silk')
    rng = np.random.default_rng(11)
    th = []
    for i in range(7):                                             # loose ends hanging from the winding
        b = Pa[int(rng.integers(20, 30))] + V(rng.uniform(-0.12, 0.12), rng.uniform(-0.04, 0.04), -0.10)
        ln = rng.uniform(0.14, 0.3)
        q = lock([b + V(0, 0, 0.06), b - V(0.01, -0.02, ln * 0.5), b - V(-0.01, -0.05, ln)], [0.014, 0.012, 0.003], [0.004, 0.004, 0.002], m=5)
        B.wbones(q, abn, sharp=3.0, k=2)
        th.append(q)
    C.skin(M.join(th), 'silk', smooth=180)
    tipc = V(AB[-1])
    sp = [P.cone(tipc + V(dx, 0.03, dz), V(dx * 3, -1.0, dz * 3), 0.10, 0.02, n=5) for dx, dz in ((0.022, 0.012), (-0.022, 0.012), (0.0, -0.022))]
    C.rigid(M.join(sp), 'abdomen4', 'limb', smooth=50)
    C.rigid(P.blob(tipc + V(0, -0.02, 0.0), 0.034, 0.045, 0.034, n=10, m=6), 'abdomen4', 'spin', smooth=180)
    spin_tip = tipc + V(0, -0.07, 0)
    # ---- the combs (front legs) and the shroud they hold
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        m = V(sg, 1, 1)
        C.rigid(P.seg_tube(sh0 * m - unit(el0 - sh0) * m * 0.03, el0 * m, 0.04, 0.032, n=7), 'comb_%s_a' % s, 'limb')
        d = unit(tp0 - el0) * m
        spine = P.tubep([el0 * m - d * 0.025, (el0 * 0.5 + tp0 * 0.5) * m + V(0, 0, 0.012), tp0 * m], [0.03, 0.022, 0.01], [0.022, 0.016, 0.008], m=8, n=6)
        teeth = []
        for k in range(10):
            t = 0.16 + 0.085 * k
            q = (el0 * (1 - t) + tp0 * t) * m
            teeth.append(P.cone(q + V(sg * 0.01, 0, 0.0), V(-sg * 1.0, -0.15, -0.38), 0.13 - 0.05 * t, 0.0075, n=4))
        cb = M.join([spine] + teeth)
        cb.smooth = 40.0
        C.rigid(cb, 'comb_%s_b' % s, 'limb')
    # carded silk between the combs
    st = []
    for k in range(7):
        t = 0.2 + 0.11 * k
        a = el0 * (1 - t) + tp0 * t + V(-0.10 + 0.04 * t, -0.012, -0.04 + 0.016 * t)
        b_ = a * V(-1, 1, 1)
        mid = (a + b_) / 2 + V(0, 0.01 * math.sin(k * 2.1), -0.025 - 0.01 * (k % 3))
        st.append(P.tubep([a + V(0.03, 0, 0.01), mid, b_ + V(-0.03, 0, 0.01)], 0.0045, 0.0045, m=6, n=3))
    web = M.join(st)
    x = web.v[:, 0]
    M.weights_by(web, x, [(-0.16, {'comb_r_b': 1.0}), (0.16, {'comb_l_b': 1.0})])
    C.skin(web, 'silk', smooth=180)
    # the half-wound shroud: a roll on the left comb, the loose end hanging between the combs
    ra, rb = el0 * 0.68 + tp0 * 0.32, el0 * 0.2 + tp0 * 0.8
    roll = P.tubep([ra, (ra + rb) / 2, rb], [0.052, 0.058, 0.05], [0.05, 0.056, 0.048], m=6, n=9)
    roll.rough(0.006, 30.0, seed=3)
    C.rigid(roll, 'comb_l_b', 'silk', smooth=180)
    nx, nz = 9, 9
    G = np.zeros((nz, nx, 3))
    rng = np.random.default_rng(5)
    hemz = 0.17 + 0.10 * rng.random(nx)
    hemz[6:] -= 0.07
    for j in range(nx):
        fx = j / (nx - 1)
        xx = 0.20 - 0.40 * fx
        top = V(xx, -0.70 - 0.02 * math.sin(fx * math.pi), 0.575 - 0.03 * math.sin(fx * math.pi))
        for i in range(nz):
            f = i / (nz - 1)
            z = top[2] + (hemz[j] - top[2]) * f
            G[i, j] = (xx * (1 - 0.12 * f) + 0.012 * math.sin(3.1 * i + j), top[1] - 0.05 * math.sin(f * math.pi) + 0.014 * math.sin(2.3 * j + 1.7 * i) * f, z)
    cl = P.sheet_solid(G, 0.006)
    v = cl.v
    wt = np.clip((v[:, 2] - 0.47) / 0.09, 0, 1)                       # top: carried by the combs
    wl = np.clip((v[:, 0] + 0.2) / 0.4, 0, 1)
    w2 = 1 - np.clip((v[:, 2] - 0.2) / 0.22, 0, 1)                   # hem: second cloth bone
    W = np.zeros((len(v), C.rig.nb), dtype=np.float32)
    W[:, C.rig.idx['comb_l_b']] = wt * wl
    W[:, C.rig.idx['comb_r_b']] = wt * (1 - wl)
    W[:, C.rig.idx['shroud1']] = (1 - wt) * (1 - w2)
    W[:, C.rig.idx['shroud2']] = (1 - wt) * w2
    cl.W = W
    C.skin(cl, 'silk', smooth=180)
    RC.add_legs(C, legs, 'limb', n=7, claw=0.4)

    # ---- clips
    def combs(p, u, card=0.0, spread=0.0, lift=0.0, n=2):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            c = card * osc(u, n, 0.0 if sg > 0 else 0.5)
            p.rot('comb_%s_a' % s, x=-lift + c, z=sg * spread)
            p.rot('comb_%s_b' % s, x=-0.6 * c)

    def cloth(p, u, lean=0.0, amp=3.0, n=2):
        p.rot('shroud1', x=lean + amp * osc(u, n, 0.1), y=0.5 * amp * osc(u, n, 0.3))
        p.rot('shroud2', x=0.8 * lean + 1.5 * amp * osc(u, n, -0.05), y=0.7 * amp * osc(u, n, 0.15))

    A.idle(dur=3.2, breath=0.008, feel=10.0, jaw=7.0, sway=0.008, abd=4.0,
           extra=lambda p, u: (combs(p, u, card=7.0, n=4), cloth(p, u, 0.0, 2.5, 2), A.abdo(p, yaw=5 * osc(u, 1, 0.2))))
    A.gait('Walk', 1.4, 0.5333, 0.62, RC.tripod(3), lift=0.10, bob=0.008, roll=1.5, yaw=2.0, abd=3.0, feel=8.0,
           extra=lambda p, u: (combs(p, u, card=4.0, n=2), cloth(p, u, 6.0, 4.0, 2)))
    A.gait('Run', 4.0, 0.3333, 0.42, RC.tripod(3), lift=0.16, bob=0.012, roll=2.0, yaw=2.5, pitch=3.0, crouch=0.03, abd=4.0, feel=5.0,
           extra=lambda p, u: (combs(p, u, card=3.0, spread=8.0, lift=10.0, n=2), cloth(p, u, 28.0, 6.0, 2), A.abdo(p, pitch=10.0)))

    def spit(p, u):                         # web spit: the distaff rears back, then whips forward and casts
        hu = 0.5
        w = bump(u, 0.0, hu * 0.6, hu * 0.8, hu)
        s = bump(u, hu * 0.8, hu, hu + 0.08, 0.9)
        A.body(p, dy=0.05 * w - 0.10 * s, dz=-0.02 * w - 0.02 * s, pitch=-5 * w + 4 * s)
        A.jaws(p, 10 * s + 2)
        A.feel(p, u, amp=5, n=3, lift=10 * w)
        A.abdo(p, pitch=-34 * w + 52 * s)
        combs(p, u, spread=14 * w + 24 * s, lift=6 * w + 4 * s)
        cloth(p, u, lean=6 * w + 10 * s, amp=3.0, n=3)
        A.plant(p)
    C.clip('Attack1', 1.0, spit, hit=0.5)
    C.reach('Attack1', 'abdomen4', spin_tip)
    A.snap('Attack2', dur=0.7, hit=0.34, reach=0.30, gape=30.0, lift=8.0, drop=0.03,
           extra=lambda p, u, w, s: (combs(p, u, spread=30 * w + 12 * s, lift=26 * w - 4 * s), cloth(p, u, -8 * w + 14 * s, 3.0, 3)))
    C.reach('Attack2', 'mandible_l', (0.05, -0.38, 0.25))
    A.hit(dur=0.45, back=0.07, extra=lambda p, u, k: (combs(p, u, spread=16 * k, lift=22 * k), cloth(p, u, -16 * k, 3.0, 3)))

    def die(p, u, f):                       # on its back: the distaff falls straight behind it, the combs fold, the cloth drops
        A.abdo(p, pitch=-160 * f)
        combs(p, u, spread=-10 * f, lift=-30 * f)
        p.rot('shroud1', x=-80 * f)
    A.death_flip(dur=1.6, side=-1.0, curl=(105.0, 40.0, 20.0), up=0.3, extra=die)
    A.spawn_dig(dur=1.3, depth=1.6, shake=6.0)
    return C.finalize(bake=bake, notes="walks on three pairs; the front pair are the combs (comb_<s>_a/b); cloth bones shroud1/2")


# ================================================================================================ rust_stinger
@reg
def rust_stinger(bake=True):
    """Rust-tail: pony-sized scorpion with a brass breastplate strapped over its back, the tail threaded through brass
    rings and tipped with a corroded spearhead held high over the body, claws like sugar-tongs"""
    R = (0.046, 0.033, 0.018)
    legs = [RC.leg_def((0.17, -0.24, 0.27), (0.42, -0.40, 0.52), (0.60, -0.56, 0.07), (0.635, -0.59, 0.0), r=R),
            RC.leg_def((0.19, -0.08, 0.27), (0.50, -0.12, 0.54), (0.72, -0.16, 0.07), (0.765, -0.165, 0.0), r=R),
            RC.leg_def((0.19, 0.08, 0.27), (0.50, 0.16, 0.54), (0.72, 0.26, 0.07), (0.765, 0.275, 0.0), r=R),
            RC.leg_def((0.17, 0.24, 0.27), (0.42, 0.46, 0.52), (0.60, 0.70, 0.07), (0.63, 0.74, 0.0), r=R)]
    claw = RC.leg_def((0.15, -0.36, 0.30), (0.42, -0.52, 0.38), (0.38, -0.80, 0.33), (0.345, -1.10, 0.31), r=(0.05, 0.045, 0.02))
    TL = [(0, 0.60, 0.32), (0, 0.84, 0.42), (0, 1.00, 0.64), (0, 1.02, 0.92), (0, 0.90, 1.16), (0, 0.66, 1.30), (0, 0.26, 1.16)]
    sk = RC.arth_skel(body=((0, 0.10, 0.30), (0, -0.38, 0.30)), head=None, legs=legs,
                      abd=[(0, 0.10, 0.30), (0, 0.36, 0.30), (0, 0.60, 0.32)], tail=TL,
                      mand=((0.05, -0.39, 0.27), (0.04, -0.50, 0.225)), claws=claw)
    C = B.Beast('mon_rust_stinger', sk)
    std_meta(C, 'rust', 1.4, 3.6)
    A = RC.Arth(C, legs, pivot=(0, 0.0, 0.3))
    C.look('brass', 'metal', 'metal_brass', color=(0.36, 0.25, 0.08), rust=(0.06, 0.16, 0.12), rust2=(0.035, 0.07, 0.055), amount=0.5,
           edge_color=(0.72, 0.56, 0.26), rough=0.38, hammer=26.0)
    C.look('verd', 'metal', 'metal_brass', color=(0.20, 0.18, 0.09), rust=(0.05, 0.30, 0.21), rust2=(0.03, 0.16, 0.12), amount=0.97, rough=0.6,
           edge_color=(0.45, 0.42, 0.24), edge=0.5)
    C.look('shell', 'chitin', 'bone_chitin', color=(0.075, 0.035, 0.02), color2=(0.15, 0.075, 0.035), edge_color=(0.36, 0.21, 0.10), rough=0.3,
           dust=0.5, dust_color=(0.30, 0.24, 0.16), ridge=40.0)
    C.look('limb', 'chitin', 'bone_chitin', color=(0.10, 0.05, 0.025), color2=(0.17, 0.09, 0.04), edge_color=(0.38, 0.24, 0.12), rough=0.34,
           dust=0.35, dust_color=(0.30, 0.24, 0.16), axis=(1, 0, 0))
    C.look('strap', 'leather', 'bone_chitin', color=(0.07, 0.045, 0.03))
    C.look('eye', 'glass', 'bone_chitin', color=(0.012, 0.012, 0.016), rough=0.06)
    # ---- body under the armour
    C.rigid(P.blob((0, -0.14, 0.29), 0.24, 0.28, 0.10, n=14, m=7), 'body', 'shell', smooth=180)
    C.rigid(P.blob((0, 0.22, 0.30), 0.25, 0.19, 0.11, n=14, m=7), 'abdomen1', 'shell', smooth=180)
    C.rigid(P.blob((0, 0.49, 0.31), 0.20, 0.16, 0.10, n=12, m=6), 'abdomen2', 'shell', smooth=180)
    ey = [P.blob((sg * 0.03, -0.385, 0.348), 0.014, 0.014, 0.014, n=7, m=5) for sg in (1, -1)]
    ey += [P.blob((sg * (0.13 + 0.02 * k), -0.345 + 0.02 * k, 0.325), 0.012, 0.012, 0.012, n=6, m=4) for sg in (1, -1) for k in range(2)]
    C.rigid(M.join(ey), 'body', 'eye', smooth=180)
    fangs(C, (0.05, -0.39, 0.27), (0.04, -0.50, 0.225), 0.034, 'limb', fang=0.06)
    # the breastplate: neck notch to the front, armholes where the claw arms come out, a ridge down the middle
    half = [(0.0, -0.32), (0.06, -0.35), (0.12, -0.45), (0.20, -0.46), (0.225, -0.37), (0.195, -0.27), (0.255, -0.17), (0.305, 0.0),
            (0.315, 0.2), (0.29, 0.40), (0.22, 0.54), (0.10, 0.60), (0.0, 0.61)]
    bp = domed_plate(mirror_outline(half), 0.13, rings=5, th=0.016, power=0.75, top=0.10, flare=0.03)
    bp.subdiv(1)
    z = bp.v[:, 2]
    bp.v[:, 2] += 0.028 * np.exp(-(bp.v[:, 0] / 0.035) ** 2) * np.clip((z - 0.02) / 0.05, 0, 1)
    for dc, rad, amt in (((0.16, 0.2, 0.12), 0.07, -0.02), ((-0.12, -0.05, 0.13), 0.06, -0.016), ((-0.2, 0.33, 0.09), 0.05, -0.012)):
        bp.dent(dc, rad, amt, direction=(0, 0, 1))
    bp.move(0, 0, 0.335)
    bp.smooth = 50.0
    C.rigid(bp, 'body', 'brass')
    for y in (-0.06, 0.30):
        C.rigid(M.band((0, y, 0.30), 0.27, 0.125, 0.05, 0.012, tilt_x=90.0, n=18), 'body', 'strap', smooth=30)
    # ---- tail: armoured segments threaded through brass rings, the sting is a spearhead
    tr = [0.085, 0.08, 0.075, 0.07, 0.064]
    for i in range(5):
        a, b_ = V(TL[i]), V(TL[i + 1])
        d = unit(b_ - a)
        sgm = P.seg_tube(a - d * tr[i] * 0.55, b_ + d * 0.01, tr[i], tr[i] * 0.86, n=9, bulge=1.18)
        C.rigid(sgm, 'tail%d' % (i + 1), 'shell')
        C.rigid(P.ring(a + (b_ - a) * 0.52, tr[i] * 1.12, 0.017, axis=d, n=14, m=6), 'tail%d' % (i + 1), 'brass', smooth=60)
    a, b_ = V(TL[5]), V(TL[6])
    d = unit(b_ - a)
    C.rigid(P.blob(a + d * 0.04, 0.07, 0.07, 0.07, n=10, m=6), 'tail6', 'shell', smooth=180)
    C.rigid(P.tubep([a + d * 0.05, a + d * 0.15], [0.036, 0.03], [0.036, 0.03], m=3, n=8), 'tail6', 'brass', smooth=40)
    spear = M.blade([(0.0, 0.03, 0.03, 0.04), (0.05, 0.075, 0.075, 0.03), (0.13, 0.085, 0.085, 0.024), (0.24, 0.05, 0.05, 0.016), (0.31, 0.004, 0.004, 0.004)])
    spear.orient(d, (0, 0, 1), a + d * 0.12)
    C.rigid(spear, 'tail6', 'verd', smooth=25)
    spear_tip = a + d * 0.43
    # ---- claws like sugar-tongs
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        m = V(sg, 1, 1)
        r0, kn, an, to = claw['root'] * m, claw['knee'] * m, claw['ankle'] * m, claw['toe'] * m
        C.rigid(P.seg_tube(r0 - unit(kn - r0) * 0.05, kn, 0.052, 0.044, n=8), 'claw_%s_a' % s, 'limb')
        fd = unit(to - an)
        inn = V(-sg, 0, 0)
        hand = M.join([P.seg_tube(kn - unit(an - kn) * 0.03, an, 0.046, 0.05, n=8),
                       P.blob(an + fd * 0.02, 0.062, 0.075, 0.05, n=10, m=6),
                       lock([an + inn * 0.03, an + inn * 0.08 + fd * 0.15, to + inn * 0.045 - fd * 0.03, to + inn * 0.016], [0.03, 0.026, 0.024, 0.02],
                            [0.02, 0.016, 0.014, 0.012], m=8),
                       P.blob(to + inn * 0.02, 0.022, 0.035, 0.03, n=8, m=5)])
        C.rigid(hand, 'claw_%s_b' % s, 'limb', smooth=50)
        fing = M.join([P.blob(an - inn * 0.035 + fd * 0.01, 0.03, 0.035, 0.028, n=8, m=5),
                       lock([an - inn * 0.03, an - inn * 0.09 + fd * 0.15, to - inn * 0.06 - fd * 0.03, to - inn * 0.03], [0.03, 0.026, 0.024, 0.02],
                            [0.02, 0.016, 0.014, 0.012], m=8),
                       P.blob(to - inn * 0.034, 0.022, 0.035, 0.03, n=8, m=5)])
        C.rigid(fing, 'claw_%s_c' % s, 'limb', smooth=50)
    RC.add_legs(C, legs, 'limb', n=8, spur=0.03)

    # ---- clips
    def pinch(p, u, k=1.0):
        A.claws(p, open=k * (6 + 9 * bump((u * 2) % 1.0, 0.1, 0.25, 0.4, 0.6)), spread=2 * osc(u, 1, 0.2), lift=2 * osc(u, 2))
    A.idle(dur=3.4, breath=0.006, jaw=8.0, sway=0.006, abd=2.0, tail=(0.0, 5.0, 1.4), extra=pinch)
    A.gait('Walk', 0.9, 0.6667, 0.64, RC.tripod(4), lift=0.07, bob=0.005, roll=1.2, yaw=1.6, abd=1.5, tail=(4.0, 3.0, 1.0),
           extra=lambda p, u: A.claws(p, open=8, spread=3 * osc(u, 1), lift=3 + 2 * osc(u, 2)))
    A.gait('Run', round(3.6 / 1.4, 3), 0.4, 0.45, RC.tripod(4), lift=0.10, bob=0.008, roll=1.6, yaw=2.2, pitch=2.0, crouch=0.01, abd=1.5,
           tail=(12.0, 5.0, 1.4), extra=lambda p, u: A.claws(p, open=22, spread=10 + 3 * osc(u, 1), lift=10 + 3 * osc(u, 2)))

    def sweep(p, u):                        # claw sweep: both tongs wide open, then scythed across the front
        hu = 0.45 / 0.95
        w = bump(u, 0.0, hu * 0.55, hu * 0.75, hu)
        s = bump(u, hu * 0.75, hu, hu + 0.1, 0.92)
        A.body(p, dy=0.06 * w - 0.22 * s, dz=-0.02 * s, pitch=-3 * w + 4 * s, yaw=10 * w - 13 * s)
        A.jaws(p, 14 * w + 2)
        A.abdo(p, yaw=-6 * w + 8 * s)
        A.tailc(p, curl=-12 * w + 10 * s, u=u, sway=1.0)
        A.claws(p, open=40 * w + 4 * s, spread=40 * w - 34 * s, lift=10 * w - 4 * s, side='l')
        A.claws(p, open=40 * w + 4 * s, spread=30 * w - 8 * s, lift=10 * w + 6 * s, side='r')
        A.plant(p)
    C.clip('Attack1', 0.95, sweep, hit=0.45)
    C.reach('Attack1', 'claw_l_c', claw['toe'])

    def sting(p, u):                        # the tail rears back, then stabs down over the head
        hu = 0.5 / 1.05
        w = bump(u, 0.0, hu * 0.6, hu * 0.8, hu)
        s = bump(u, hu * 0.82, hu, hu + 0.07, 0.9)
        A.body(p, dy=0.08 * w - 0.20 * s, dz=0.02 * w - 0.03 * s, pitch=-5 * w + 6 * s)
        A.jaws(p, 10 * s + 2)
        A.abdo(p, pitch=-4 * w + 6 * s)
        for b, cw, cs in zip(A.tail, (-10, -10, -15, -20, -20, -15), (66, -1, -7, -6, -13, -25)):
            p.rot(b, x=cw * w + cs * s)
        A.claws(p, open=20 * w, spread=22 * w + 16 * s, lift=4 * w)
        A.plant(p)
    C.clip('Attack2', 1.05, sting, hit=0.5)
    C.reach('Attack2', 'tail6', spear_tip)
    A.hit(dur=0.45, back=0.06, extra=lambda p, u, k: (A.claws(p, open=20 * k, spread=12 * k, lift=16 * k), A.tailc(p, curl=-22 * k)))

    def die(p, u, f):                       # the legs give way, the tail topples sideways, the tongs fall open
        g = sm(u, 0.2, 0.7)
        p.rot('tail1', y=84 * g)
        A.tailc(p, curl=40 * g)
        A.claws(p, open=30 * f, spread=18 * f, lift=-8 * f)
    A.death_drop(dur=1.5, splay=0.14, tilt=9.0, extra=die)
    A.spawn_dig(dur=1.4, depth=1.5, shake=6.0)
    return C.finalize(bake=bake, notes="claws claw_<s>_a/b/c (c = the moving finger); tail1-5 segments, tail6 = the spearhead")


# ================================================================================================ the other shroud spiders
def spider_jaws(A):
    """fangs that hang down open sideways (and swing forward) instead of yawing"""
    A.jaws = lambda p, deg: (p.rot('mandible_l', x=-0.5 * deg, y=-deg), p.rot('mandible_r', x=-0.5 * deg, y=deg))


def hide_in(C, bones, keep=()):
    """every clip except those named in `keep` scales the given bones to nothing (parts that only show in one clip)"""
    clip0 = C.clip

    def clip(name, dur, fn, **kw):
        def f2(p, u):
            fn(p, u)
            if name not in keep:
                for b in bones:
                    p.scl(b, 0.001)
        return clip0(name, dur, f2, **kw)
    C.clip = clip


SPIDER_PH4 = {'leg1_l': 0.0, 'leg2_l': 0.5, 'leg3_l': 0.25, 'leg4_l': 0.75, 'leg1_r': 0.5, 'leg2_r': 0.0, 'leg3_r': 0.75, 'leg4_r': 0.25}


@reg
def shroud_ling(bake=True):
    """Shroudling: a small spider whose round pale abdomen is wrapped in its own first silk like a tiny bandaged ball,
    short legs held high"""
    K = 1.25                               # enlarged: at "a hand and a half across" it was a speck on screen
    legs = RC.radial_legs(4, -0.13, -0.02, 0.055, 0.165, 0.26, 0.34, fan=(-55, 38), knee_out=0.45, two=True, r=(0.018, 0.013, 0.008))
    sk = RC.arth_skel(body=((0, 0.02, 0.17), (0, -0.19, 0.17)), head=None, legs=legs, abd=[(0, 0.0, 0.19), (0, 0.36, 0.27)],
                      mand=((0.03, -0.175, 0.15), (0.025, -0.20, 0.07)), ant=[(0.05, -0.165, 0.165), (0.075, -0.27, 0.13)])
    legs0 = enlarge(sk, legs, K)
    C = B.Beast('mon_shroud_ling', sk, budget=4000, bone_budget=28, mb=2.5, tex=1024)
    std_meta(C, 'shroud', 0.6, 6.0)
    grow(C, K)
    A = RC.Arth(C, legs, pivot=(0, 0.0, 0.17 * K))
    spider_jaws(A)
    C.look('carap', 'chitin', 'bone_spider', color=(0.03, 0.027, 0.028), color2=(0.07, 0.06, 0.055), rough=0.32, dust=0.4,
           dust_color=(0.22, 0.21, 0.20))
    C.look('limb', 'chitin', 'bone_spider', color=(0.04, 0.034, 0.032), edge_color=(0.28, 0.25, 0.22), rough=0.36, axis=(1, 0, 0), dust=0.25,
           dust_color=(0.22, 0.21, 0.20))
    C.look('eye', 'glass', 'bone_spider', color=(0.012, 0.012, 0.016), rough=0.06)
    C.look('silk', 'cloth', 'cloth_silk', color=(0.50, 0.48, 0.43), dirt=(0.20, 0.18, 0.15), weave='linen', fold=34.0, hem_hi=0.05, stain_amt=0.3,
           dust=0.15)
    spider_front(C, (0, -0.08, 0.17), (0.085, 0.11, 0.06), 'carap', 'eye')
    fangs(C, (0.03, -0.175, 0.15), (0.025, -0.20, 0.07), 0.02, 'limb', fang=0.04)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(RC.antenna([(sg * 0.05, -0.16, 0.165), (sg * 0.068, -0.22, 0.16), (sg * 0.075, -0.27, 0.13)], 0.013, 0.009, n=5), 'antenna_' + s, 'limb')
    bc = V(0, 0.20, 0.26)
    br = 0.178
    C.rigid(P.blob(bc, br, br, br, n=14, m=8), 'abdomen1', 'silk', smooth=180)
    C.rigid(P.blob((0, 0.03, 0.19), 0.045, 0.05, 0.045, n=8, m=5), 'abdomen1', 'carap', smooth=180)
    for tx, ty, tz in ((70, 10, 0), (95, -35, 20), (60, 50, -10), (18, 0, 30), (110, 80, 0)):       # turns of bandage
        bnd = M.band((0, 0, 0), br * 1.005, br * 1.005, 0.055, 0.014, n=16)
        bnd.rot('x', tx).rot('y', ty).rot('z', tz).move(*bc)
        C.rigid(bnd, 'abdomen1', 'silk', smooth=40)
    ends = [lock([bc + V(0.05, 0.12, 0.05), bc + V(0.07, 0.18, 0.0), bc + V(0.08, 0.21, -0.10)], [0.02, 0.018, 0.004], [0.005, 0.005, 0.002], m=5),
            lock([bc + V(-0.10, 0.06, -0.06), bc + V(-0.15, 0.08, -0.10), bc + V(-0.16, 0.10, -0.19)], [0.018, 0.016, 0.004], [0.005, 0.005, 0.002], m=5)]
    C.rigid(M.join(ends), 'abdomen1', 'silk', smooth=180)
    RC.add_legs(C, legs0, 'limb', n=5)
    ft = V(0.025, -0.2, 0.07) * K
    A.idle(dur=2.4, breath=0.006, feel=12.0, jaw=8.0, sway=0.005, abd=5.0)
    A.gait('Walk', 3.0, 0.2667, 0.5, RC.tripod(4), lift=0.075, bob=0.006, roll=2.0, yaw=2.5, abd=4.0)
    A.gait('Run', 10.0, 0.2, 0.2, SPIDER_PH4, lift=0.10, bob=0.0075, roll=2.0, yaw=2.0, pitch=3.0, crouch=0.012, abd=5.0)
    A.snap(dur=0.5, hit=0.25, reach=0.19, gape=30.0, lift=8.0, drop=0.02)
    C.reach('Attack1', 'mandible_l', ft)
    A.hop('Attack2', dur=0.8, hit=0.45, fwd=0.6, up=0.45, gape=30.0, pitch=18.0)
    C.reach('Attack2', 'mandible_l', ft)
    A.hit(dur=0.35, back=0.075)
    A.death_flip(dur=1.2, side=1.0, curl=(105.0, 45.0, 0.0), up=0.37)
    A.spawn_drop(dur=0.9, height=2.4, land=0.55, squash=0.06)
    return C.finalize(bake=bake, notes="swarm budget, two-bone legs; built 1.25 x the brief size to read on screen; Spawn drops in from 2.4 m above")


@reg
def shroud_dropper(bake=True):
    """Vault-dropper: a small tight body under a cage of very long thin legs that meet the ground in a wide circle;
    two fangs as long as daggers point straight down; it comes down on a visible thread"""
    R = (0.03, 0.021, 0.012)
    legs = []
    for a in (-64, -24, 20, 62):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        legs.append(RC.leg_def((0.08 * ca, 0.08 * sa - 0.02, 0.63), (0.42 * ca, 0.42 * sa, 1.36), (0.93 * ca, 0.93 * sa, 0.07), (0.96 * ca, 0.96 * sa, 0.0), r=R))
    FJ, FT = V(0.05, -0.13, 0.57), V(0.045, -0.15, 0.17)
    sk = RC.arth_skel(body=((0, 0.14, 0.62), (0, -0.18, 0.62)), head=None, legs=legs, abd=[(0, 0.10, 0.63), (0, 0.36, 0.68)],
                      mand=(FJ, FT), ant=[(0.09, -0.14, 0.60), (0.12, -0.27, 0.52)],
                      extra=lambda sk: sk.bone('thread', (0, 0.18, 0.70), (0, 0.18, 6.7), 'body'))
    C = B.Beast('mon_shroud_dropper', sk)
    std_meta(C, 'shroud', 1.0, 5.2)
    A = RC.Arth(C, legs, pivot=(0, 0.0, 0.62))
    spider_jaws(A)
    hide_in(C, ['thread'], keep=('Spawn',))
    C.look('carap', 'chitin', 'bone_spider', color=(0.04, 0.03, 0.027), color2=(0.085, 0.065, 0.05), rough=0.3, dust=0.4, dust_color=(0.22, 0.21, 0.20))
    C.look('limb', 'chitin', 'bone_spider', color=(0.05, 0.038, 0.032), color2=(0.10, 0.08, 0.06), edge_color=(0.32, 0.28, 0.24), rough=0.34,
           axis=(0, 0, 1), dust=0.2, dust_color=(0.22, 0.21, 0.20))
    C.look('pelt', 'bfur', 'bone_spider', color=(0.055, 0.045, 0.04), tip=(0.20, 0.18, 0.16), flow=(0, 0.6, -0.8), scale=95.0, ash=0.5,
           ash_color=(0.26, 0.25, 0.24))
    C.look('eye', 'glass', 'bone_spider', color=(0.012, 0.012, 0.016), rough=0.06)
    C.look('fang', 'bone', 'bone_spider', color=(0.34, 0.31, 0.25), stain=(0.10, 0.08, 0.05), crack_amt=0.3, dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('silk', 'cloth', 'bone_spider', color=(0.50, 0.49, 0.45), weave='linen', fold=40.0)
    C.look('venom', 'emis', 'emis_fang', emit=(0.30, 0.85, 0.40), strength=1.5, mottle=0.3, base_mul=0.5)
    spider_front(C, (0, -0.02, 0.62), (0.15, 0.17, 0.11), 'carap', 'eye')
    C.rigid(P.blob((0, 0.24, 0.675), 0.15, 0.18, 0.14, n=14, m=8), 'abdomen1', 'pelt', smooth=180)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        m = V(sg, 1, 1)
        base = P.seg_tube(FJ * m + V(0, 0.02, 0.03), V(0.048, -0.14, 0.45) * m, 0.04, 0.03, n=7, bulge=1.15)
        bl = M.blade([(0.0, 0.022, 0.022, 0.03), (0.05, 0.03, 0.03, 0.024), (0.2, 0.02, 0.02, 0.015), (0.29, 0.003, 0.003, 0.004)])
        bl.orient(unit(V(0, -0.03, -1.0)), (0, -1, 0), V(0.048, -0.14, 0.47) * m)
        bl.smooth = 25.0
        C.rigid(base, 'mandible_' + s, 'limb')
        C.rigid(bl, 'mandible_' + s, 'fang')
        C.rigid(P.cone(V(0.0475, -0.147, 0.26) * m, V(0, -0.03, -1.0), 0.095, 0.018, n=5), 'mandible_' + s, 'venom', smooth=50)
        C.rigid(RC.antenna([(sg * 0.09, -0.13, 0.60), (sg * 0.11, -0.21, 0.58), (sg * 0.12, -0.27, 0.52)], 0.02, 0.014, n=6), 'antenna_' + s, 'limb')
    C.rigid(P.tubep([(0, 0.18, 0.68), (0, 0.18, 6.7)], 0.007, 0.007, m=2, n=4), 'thread', 'silk', smooth=180, free=True)   # free: it is scaled away
    RC.add_legs(C, legs, 'limb', n=6, claw=0.4)

    A.idle(dur=3.0, breath=0.012, feel=10.0, jaw=4.0, sway=0.012, abd=4.0)
    A.gait('Walk', 1.7, 0.6, 0.62, RC.tripod(4), lift=0.14, bob=0.012, roll=1.5, yaw=2.0, abd=3.0)
    A.gait('Run', 5.2, 0.3667, 0.42, RC.tripod(4), lift=0.24, bob=0.02, roll=2.5, yaw=2.5, pitch=3.0, crouch=0.05, abd=4.0)

    def stab(p, u):                         # rises on its legs, then drops on the target like a plumb, fangs first
        hu = 0.42 / 0.9
        w = bump(u, 0.0, hu * 0.55, hu * 0.75, hu)
        s = bump(u, hu * 0.78, hu, hu + 0.1, 0.92)
        A.body(p, dy=0.06 * w - 0.38 * s, dz=0.18 * w - 0.15 * s, pitch=-10 * w + 2 * s)
        A.jaws(p, 24 * w - 4 * s + 2)
        A.feel(p, u, amp=5, n=3, lift=14 * w)
        A.abdo(p, pitch=-10 * w + 8 * s)
        A.plant(p)
    C.clip('Attack1', 0.9, stab, hit=0.42)
    C.reach('Attack1', 'mandible_l', FT)

    def strike(p, u):                       # fangs thrown up and forward, then hooked down into the target
        hu = 0.36 / 0.8
        w = bump(u, 0.0, hu * 0.5, hu * 0.7, hu)
        s = bump(u, hu * 0.7, hu, hu + 0.1, 0.92)
        A.body(p, dy=0.10 * w - 0.42 * s, dz=0.05 * w - 0.10 * s, pitch=-12 * w + 6 * s)
        for sg, b in ((1, 'mandible_l'), (-1, 'mandible_r')):
            p.rot(b, x=-75 * w - 30 * s, y=-sg * (22 * w - 4 * s))
        A.feel(p, u, amp=5, n=3, lift=20 * w)
        A.abdo(p, pitch=-8 * w + 6 * s)
        A.plant(p)
    C.clip('Attack2', 0.8, strike, hit=0.36)
    C.reach('Attack2', 'mandible_l', FT)
    A.hit(dur=0.45, back=0.08)
    A.death_flip(dur=1.6, side=1.0, curl=(115.0, 40.0, 20.0), up=0.35)
    A.spawn_drop(dur=1.6, height=4.0, land=0.7, line=True, squash=0.14, tuck=A.bend(-30, 60, 10),
                 extra=lambda p, u, t: p.scl('thread', max(0.001, 1 - sm(u, 0.72, 0.95))))
    return C.finalize(bake=bake, notes="bone `thread` (6 m of silk above the body) is scaled to nothing in every clip but Spawn, where the spider "
                                       "is lowered on it from 4 m; the rest pose shows the thread")


def burial_mask(w=0.17, h=0.23, depth=0.055):
    """a human face as a burial mask: built facing +Z (x across, y up), floor at z = 0. -> (mask Part, hole Part)"""
    d = P.dome(w / 2, h / 2, depth, n=24, m=7, power=2.4)
    d.subdiv(1)
    k = h / 0.23
    d.v[:, 0] *= 1 - 0.3 * np.clip(-d.v[:, 1] / (h / 2), 0, 1) ** 1.5                 # narrow chin
    up = (0, 0, 1)
    for (x, y, rad, amt) in ((0, 0.012, 0.015, 0.018), (0, -0.008, 0.017, 0.03), (0, -0.026, 0.02, 0.042), (0, 0.035, 0.017, 0.012),     # nose
                             (0.042, 0.052, 0.03, 0.014), (-0.042, 0.052, 0.03, 0.014),                            # brow
                             (0.043, 0.026, 0.023, -0.034), (-0.043, 0.026, 0.023, -0.034),                        # eye sockets
                             (0.05, -0.03, 0.028, 0.012), (-0.05, -0.03, 0.028, 0.012),                            # cheekbones
                             (0, -0.066, 0.03, -0.014), (0, -0.052, 0.013, 0.012), (0, -0.081, 0.014, 0.011)):    # mouth, lips
        d.dent((x * k, y * k, depth * 0.85), rad * k, amt * k, direction=up)
    d.smooth = 50.0
    bv = P.bvh_of(d)
    hl = []
    for (x, y, rx_, ry_) in ((0.043, 0.026, 0.02, 0.011), (-0.043, 0.026, 0.02, 0.011), (0, -0.066, 0.026, 0.006)):
        q, n_ = P.surf(bv, (x * k, y * k, 1.0), (0, 0, -1), 2.0)
        hl.append(P.blob(q + V(0, 0, 0.002), rx_ * k, ry_ * k, 0.006, n=10, m=4))
    eyes = M.join(hl)
    eyes.smooth = 180.0
    return d, eyes


@reg
def shroud_matron(bake=True):
    """Cocoon-mother: a huge spider whose swollen abdomen is plastered with dozens of silk cocoons, thick armoured front
    legs, a human burial mask stuck over her eye cluster; sickly green light between the cocoons"""
    R1 = (0.085, 0.07, 0.038)
    R2 = (0.06, 0.045, 0.025)
    legs = [RC.leg_def((0.15, -0.58, 0.47), (0.403, -0.956, 0.95), (0.646, -1.346, 0.12), (0.684, -1.406, 0.0), r=R1),
            RC.leg_def((0.17, -0.47, 0.47), (0.666, -0.618, 1.0), (1.142, -0.773, 0.11), (1.209, -0.794, 0.0), r=R2),
            RC.leg_def((0.17, -0.34, 0.47), (0.655, -0.16, 1.0), (1.118, 0.027, 0.11), (1.183, 0.054, 0.0), r=R2),
            RC.leg_def((0.15, -0.24, 0.47), (0.488, 0.122, 1.02), (0.849, 0.523, 0.11), (0.903, 0.582, 0.0), r=R2)]
    AC, AR = V(0, 0.54, 0.64), V(0.50, 0.58, 0.50)
    sk = RC.arth_skel(body=((0, -0.12, 0.50), (0, -0.66, 0.50)), head=((0, -0.64, 0.52), (0, -0.84, 0.52)), legs=legs,
                      abd=[(0, -0.10, 0.56), (0, 1.08, 0.64)], mand=((0.07, -0.72, 0.44), (0.06, -0.80, 0.26)),
                      ant=[(0.14, -0.70, 0.46), (0.19, -0.92, 0.36)],
                      extra=lambda sk: sk.bone('sac', (0, 0.98, 0.40), (0, 1.18, 0.32), 'abdomen1'))
    C = B.Beast('mon_shroud_matron', sk, budget=22000)
    std_meta(C, 'shroud', 1.7, 2.8)
    A = RC.Arth(C, legs, pivot=(0, -0.3, 0.5))
    spider_jaws(A)
    hide_in(C, ['sac'], keep=('Attack2',))
    C.look('carap', 'chitin', 'bone_spider', color=(0.028, 0.025, 0.027), color2=(0.065, 0.055, 0.055), rough=0.32, dust=0.5,
           dust_color=(0.20, 0.19, 0.18), edge=0.5)
    C.look('limb', 'chitin', 'bone_spider', color=(0.035, 0.03, 0.03), edge_color=(0.26, 0.23, 0.20), rough=0.36, pit=170.0, axis=(1, 0, 0),
           dust=0.3, dust_color=(0.20, 0.19, 0.18))
    C.look('plate', 'chitin', 'bone_spider', color=(0.022, 0.02, 0.022), color2=(0.05, 0.045, 0.045), edge_color=(0.34, 0.30, 0.26), rough=0.28,
           edge=0.8, dust=0.3, dust_color=(0.20, 0.19, 0.18))
    C.look('mask', 'bone', 'bone_spider', color=(0.60, 0.56, 0.46), stain=(0.25, 0.18, 0.10), crack_amt=0.8, dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('void', 'flat', 'bone_spider', color=(0.006, 0.006, 0.008), rough=0.5)
    C.look('silk', 'cloth', 'cloth_cocoon', color=(0.46, 0.45, 0.41), dirt=(0.16, 0.15, 0.13), weave='linen', fold=30.0, hem_hi=0.2, stain_amt=0.6)
    C.look('abd', 'glow', 'emis_abd', color=(0.07, 0.08, 0.05), emit=(0.36, 0.72, 0.20), strength=1.3, vein=(0.015, 0.022, 0.015), vein_scale=8.0,
           vein_w=0.10, scale=6.0)
    # ---- front: carapace, the head under the mask, fangs
    spider_front(C, (0, -0.40, 0.50), (0.27, 0.33, 0.16), 'carap', 'eye', eyes=False)
    C.rigid(P.blob((0, -0.70, 0.53), 0.16, 0.14, 0.13, n=12, m=7), 'head', 'carap', smooth=180)
    mk, holes = burial_mask(0.25, 0.33, 0.085)
    for q, lk in ((mk, 'mask'), (holes, 'void')):
        q.rot('x', 90).rot('x', -20, pivot=(0, 0, 0)).move(0, -0.80, 0.60)
        C.rigid(q, 'head', lk)
    rng = np.random.default_rng(21)
    glue = []
    for i in range(7):                                                  # strands of silk that hold the mask on
        a = TAU * (i + 0.3 * rng.random()) / 7
        e = V(0.125 * math.cos(a), -0.80 + 0.05 * math.sin(a), 0.60 + 0.15 * math.sin(a))
        glue.append(lock([e + V(0, -0.02, 0), e * V(1.5, 1, 1) + V(0, 0.07, 0.03 * math.sin(a)), V(e[0] * 1.5, -0.66, 0.53 + 0.12 * math.sin(a))],
                         [0.012, 0.014, 0.01], [0.004, 0.004, 0.003], m=5, up=(0, 1, 0)))
    C.rigid(M.join(glue), 'head', 'silk', smooth=180)
    fangs(C, (0.07, -0.72, 0.44), (0.06, -0.80, 0.26), 0.06, 'limb', fang=0.15)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(RC.antenna([(sg * 0.14, -0.69, 0.46), (sg * 0.175, -0.82, 0.44), (sg * 0.19, -0.92, 0.36)], 0.034, 0.024, n=7), 'antenna_' + s, 'limb')
    # ---- the abdomen and its cocoons
    ab = P.blob(AC, AR[0], AR[1], AR[2], n=26, m=14)
    ab.rough(0.02, 3.0, seed=4)
    C.rigid(ab, 'abdomen1', 'abd', smooth=180)
    C.rigid(P.blob((0, -0.03, 0.56), 0.14, 0.13, 0.13, n=10, m=6), 'abdomen1', 'carap', smooth=180)     # pedicel
    femur4 = [(legs[3]['root'] * V(sg, 1, 1), legs[3]['knee'] * V(sg, 1, 1)) for sg in (1, -1)]
    cs = []
    coc = []
    tries = 0
    while len(coc) < 84 and tries < 9000:
        tries += 1
        d = unit(rng.normal(size=3))
        if d[2] < -0.5 or (d[1] < -0.55 and d[2] < 0.35):
            continue
        ps = AC + d * AR
        if any(np.linalg.norm(ps - q) < 0.125 for q in cs) or any(B.seg_dist(ps[None, :], a_, b_)[0] < 0.25 for a_, b_ in femur4):
            continue
        cs.append(ps)
        n = unit(d / AR)
        big = rng.random() < 0.22
        ln = rng.uniform(0.17, 0.24) if big else rng.uniform(0.08, 0.13)
        t = unit(np.cross(n, rng.normal(size=3)))
        q = P.blob((0, 0, 0), ln * 0.5, ln, ln * 0.46, n=9, m=6, rot=M.frame_from(t, n))
        q.rough(0.006, 14.0, seed=len(coc))
        q.move(*(ps + n * ln * 0.14))
        coc.append(q)
    cq = M.join(coc)
    cq.smooth = 180.0
    C.rigid(cq, 'abdomen1', 'silk')
    C.rigid(P.blob((0, 1.06, 0.36), 0.13, 0.15, 0.12, n=10, m=6), 'sac', 'silk', smooth=180, free=True)       # free: it is laid (drops off)
    # ---- legs: the front pair armoured with overlapping plates
    RC.add_legs(C, legs, 'limb', n=8, spur=0.05)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        m = V(sg, 1, 1)
        for bn, a_, b_, rr in (('a', legs[0]['root'], legs[0]['knee'], R1[0]), ('b', legs[0]['knee'], legs[0]['ankle'], R1[1])):
            a_, b_ = a_ * m, b_ * m
            d = unit(b_ - a_)
            out = unit(np.cross(d, np.cross(V(0, 0, 1), d)) + V(sg * 0.4, -0.3, 0))
            pl = []
            for k in range(3 if bn == 'a' else 4):
                t = 0.24 + (0.24 if bn == 'a' else 0.2) * k
                c = a_ + (b_ - a_) * t + out * rr * 0.55
                pl.append(P.blob(c, rr * 1.25, np.linalg.norm(b_ - a_) * 0.17, rr * 0.75, n=8, m=5, rot=M.frame_from(d, out)))
                pl.append(P.cone(c + out * rr * 0.5, out + d * 0.5, rr * 1.3, rr * 0.4, n=4))
            C.rigid(M.join(pl), 'leg1_%s_%s' % (s, bn), 'plate', smooth=50)

    A.idle(dur=3.6, breath=0.012, feel=8.0, jaw=5.0, sway=0.01, abd=2.5, look=5.0)
    A.gait('Walk', 0.6, 0.9333, 0.66, RC.tripod(4), lift=0.12, bob=0.012, roll=1.6, yaw=1.6, abd=2.0)
    A.gait('Run', round(2.8 / 1.7, 3), 0.6, 0.5, RC.tripod(4), lift=0.18, bob=0.02, roll=2.4, yaw=2.2, pitch=1.5, abd=2.5)
    L1 = [A.legs['leg1_l'], A.legs['leg1_r']]

    def slam(p, u):                         # rears up with both armoured legs high, then drives them into the ground ahead
        hu = 0.62 / 1.25
        w = bump(u, 0.0, hu * 0.6, hu * 0.8, hu)
        s = bump(u, hu * 0.82, hu, hu + 0.12, 0.92)
        A.body(p, dy=0.10 * w - 0.20 * s, dz=0.10 * w - 0.06 * s, pitch=-16 * w + 6 * s)
        A.head(p, pitch=-10 * w + 8 * s)
        A.jaws(p, 20 * w + 2)
        A.feel(p, u, amp=4, n=3, lift=15 * w)
        A.abdo(p, pitch=5 * w - 3 * s)
        A.plant(p)
        up = bump(u, hu * 0.1, hu * 0.6, hu * 0.85, hu)
        fw = sm(u, hu * 0.3, hu) * (1 - sm(u, hu + 0.15, 0.95))
        for lg in L1:
            p.leg(lg, lg.home + V(lg.side * 0.10 * up, -0.30 * fw - 0.10 * up, 0.85 * up))
    C.clip('Attack1', 1.25, slam, hit=0.62)
    C.reach('Attack1', 'leg1_l_c', legs[0]['toe'])

    def lay(p, u):                          # lifts her front, presses the abdomen down and pushes a sac out
        hu = 0.75 / 1.5
        w = bump(u, 0.0, hu * 0.5, hu * 0.9, hu + 0.05)
        k = bump(u, hu - 0.05, hu, hu + 0.05, hu + 0.25)
        A.body(p, dy=-0.05 * w, dz=0.08 * w - 0.01 * k, pitch=-3 * w - 1 * k)
        A.head(p, pitch=6 * w)
        A.jaws(p, 8 * w + 2)
        A.feel(p, u, amp=5, n=4)
        A.abdo(p, pitch=-2 * w - 2 * k)
        pulse = w * osc(u, 6)
        p.scl('abdomen1', (1 + 0.025 * pulse, 1 - 0.03 * pulse + 0.04 * k, 1 + 0.025 * pulse))
        g = sm(u, hu * 0.35, hu * 0.95)
        drop = sm(u, hu, hu + 0.12)
        gone = sm(u, hu + 0.3, hu + 0.42)
        p.scl('sac', max(0.001, g * (1 - gone)))
        p.move('sac', 0, 0.10 * drop, -0.09 * drop)
        A.plant(p)
    C.clip('Attack2', 1.5, lay, hit=0.75)
    C.reach('Attack2', 'head', (0, -0.9, 0.5))
    A.hit(dur=0.5, back=0.05)

    def die(p, u, f):                       # sinks on her belly and face: the fangs fold forward out of the way
        for sg, bn in ((1, 'mandible_l'), (-1, 'mandible_r')):
            p.rot(bn, x=-72 * f)
    A.death_drop(dur=1.8, splay=0.30, tilt=8.0, pitch=12.0, extra=die)
    A.spawn_dig(dur=1.6, depth=1.5, shake=4.0)
    return C.finalize(bake=bake, notes="giant budget; bone `sac` (the egg sac she lays) only shows in Attack2; Attack1 reach = where the front legs land")


# ================================================================================================ sexton variants
def ring_prism(outer, inner, z0, z1):
    """wall between two outlines with the same number of points (x, y), from z0 to z1: closed on all sides"""
    o = np.asarray(outer, dtype=float)
    i_ = np.asarray(inner, dtype=float)
    n = len(o)
    v = [(x, y, z0) for x, y in o] + [(x, y, z1) for x, y in o] + [(x, y, z0) for x, y in i_] + [(x, y, z1) for x, y in i_]
    f = []
    for j in range(n):
        k = (j + 1) % n
        f += [(j, k, n + k, n + j), (2 * n + k, 2 * n + j, 3 * n + j, 3 * n + k), (n + j, n + k, 3 * n + k, 3 * n + j), (k, j, 2 * n + j, 2 * n + k)]
    p = M.Part(v, f)
    p.smooth = 30.0
    return p


def scaled(outline, s, c=None):
    o = np.asarray(outline, dtype=float)
    c = o.mean(0) if c is None else np.asarray(c, dtype=float)
    return c + (o - c) * s


def ubar(w, drop, r, n=6):
    """U-shaped bail handle hanging from two points w apart: local frame, top centre at the origin, hanging towards -Z, across X"""
    h = w / 2
    path = [(-h, 0, 0.0), (-h, 0, -drop * 0.7), (-h * 0.65, 0, -drop), (h * 0.65, 0, -drop), (h, 0, -drop * 0.7), (h, 0, 0.0)]
    q = P.tubep(path, r, r, m=16, n=n)
    eyes = [P.blob((sg * h, 0, 0.0), r * 2.0, r * 2.2, r * 2.0, n=6, m=4) for sg in (1, -1)]
    q = M.join([q] + eyes)
    q.smooth = 50.0
    return q


def grub(path, r, n=8, m=15, seed=0):
    """fat segmented larva along a path (head end last): bulging rings, a small hard head"""
    path = np.asarray(path, dtype=float)
    Pp = M.polyline(path, m)
    t = np.linspace(0, 1, m)
    rr = r * (0.55 + 0.45 * np.sin(np.pi * np.clip(t * 1.05, 0, 1)) ** 0.5) * (1 + 0.16 * np.cos(t * (m - 1) * math.pi))
    q = M.tube(Pp, rr, n=n, up=(0, 0, 1), cap0=True, cap1=True)
    q.smooth = 180.0
    hd = P.blob(Pp[-1], r * 0.55, r * 0.55, r * 0.55, n=7, m=5)
    return q, hd


def spade_hand(length=0.34, width=0.22, th=0.032, teeth=4):
    """a digging hand: wrist at the origin, the toothed edge towards +Y, back of the hand up (+Z)"""
    w = width / 2
    out = [(-w * 0.5, 0.0), (-w, length * 0.36), (-w, length * 0.66)]
    for i in range(teeth):
        x0 = -w + width * i / teeth
        x1 = -w + width * (i + 1) / teeth
        out.append((x0 + (x1 - x0) * 0.35, length * (1.0 - 0.05 * abs(i - (teeth - 1) / 2))))
        out.append((x1 - (x1 - x0) * 0.08, length * 0.68))
    out[-1] = (w, length * 0.64)
    out += [(w, length * 0.3), (w * 0.5, 0.0)]
    p = RC.plate(out[::-1], th=th, z=0.0, dome=th * 0.6, bevel=0.45)
    p.smooth = 35.0
    return p


@reg
def sexton_digger(bake=True):
    """Undertaker: a longer, flatter burying beetle with enormous spade forelegs held out in front like a mole's hands,
    almost no visible head, a ridged wing-case caked with grave soil and a coffin handle hooked on one leg"""
    R = (0.044, 0.029, 0.015)
    legs = [RC.leg_def((0.15, -0.14, 0.15), (0.36, -0.20, 0.32), (0.50, -0.24, 0.055), (0.55, -0.25, 0.0), r=R),
            RC.leg_def((0.15, 0.18, 0.15), (0.36, 0.36, 0.33), (0.46, 0.50, 0.055), (0.49, 0.555, 0.0), r=R)]
    SH, WR, TP = V(0.18, -0.29, 0.15), V(0.33, -0.50, 0.155), V(0.45, -0.85, 0.125)
    fm = (legs[1]['root'] + legs[1]['knee']) / 2                       # the handle hangs on the left hind femur
    fd = unit(legs[1]['knee'] - legs[1]['root'])
    hp = fm + V(0, 0, 0.047)

    def extra(sk):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            m = (sg, 1, 1)
            sk.bone('spade_%s_a' % s, SH * m, WR * m, 'body')
            sk.bone('spade_%s_b' % s, WR * m, TP * m, 'spade_%s_a' % s)
        sk.bone('handle', hp, hp - V(0, 0, 0.14), 'leg2_l_a')
    sk = RC.arth_skel(body=((0, 0.05, 0.20), (0, -0.30, 0.20)), head=((0, -0.36, 0.15), (0, -0.50, 0.13)), legs=legs,
                      abd=[(0, 0.03, 0.20), (0, 0.68, 0.18)], mand=((0.035, -0.45, 0.115), (0.012, -0.55, 0.10)),
                      ant=[(0.07, -0.43, 0.15), (0.14, -0.50, 0.17)], extra=extra)
    C = B.Beast('mon_sexton_digger', sk)
    std_meta(C, 'sexton', 1.05, 4.4)
    A = RC.Arth(C, legs, pivot=(0, 0.05, 0.2))
    C.look('shell', 'chitin', 'bone_shell', color=(0.02, 0.018, 0.017), color2=(0.05, 0.04, 0.03), rough=0.34, dust=0.95,
           dust_color=(0.13, 0.10, 0.07), edge=0.5, ridge=40.0, ridge_h=1.4)
    C.look('limb', 'chitin', 'bone_shell', color=(0.03, 0.024, 0.02), rough=0.34, pit=170.0, dust=0.5, dust_color=(0.13, 0.10, 0.07), axis=(1, 0, 0))
    C.look('belly', 'chitin', 'bone_shell', color=(0.05, 0.035, 0.025), rough=0.45, ridge=30.0, ridge_h=1.6, dust=0.0)
    C.look('soil', 'mud', 'bone_shell', color=(0.06, 0.046, 0.032), dry=(0.13, 0.105, 0.075), dry_amt=0.6)
    C.look('iron', 'metal', 'metal_handle', color=(0.18, 0.17, 0.16), rust=(0.20, 0.09, 0.035), rust2=(0.07, 0.035, 0.02), amount=0.8, rough=0.55)
    # ---- body: belly (the host of the legs), ridged wing-case, wide thorax shield over a stub of a head
    core(C, P.blob((0, 0.0, 0.15), 0.21, 0.36, 0.075, n=14, m=7), 'body', 'belly')
    C.rigid(P.blob((0, 0.38, 0.15), 0.18, 0.31, 0.065, n=14, m=6), 'abdomen1', 'belly', smooth=180)
    el = RC.shell(0.80, 0.46, 0.185, n=40, m=7, power=2.2, front=0.95, rear=0.5, groove=0.008)
    x = el.v[:, 0]
    el.v[:, 2] += 0.013 * np.cos(x / 0.23 * math.pi * 3.0) ** 2 * np.clip(el.v[:, 2] / 0.06, 0, 1) * np.clip(1.15 - np.abs(x) / 0.23, 0, 1) ** 0.5
    el.move(0, 0.31, 0.135)
    C.rigid(el, 'abdomen1', 'shell', smooth=60)
    pr = RC.shell(0.37, 0.50, 0.17, n=22, m=6, power=2.6, front=0.6, rear=1.0).move(0, -0.235, 0.145)
    C.rigid(pr, 'body', 'shell', smooth=60)
    bv = P.bvh_of(M.join([el.copy(), pr.copy()]))
    rng = np.random.default_rng(9)
    clods = []
    for i in range(20):                                                  # grave soil caked on the back
        px, py = rng.uniform(-0.17, 0.17), rng.uniform(-0.36, 0.62)
        q, n_ = P.surf(bv, (px, py, 1.0), (0, 0, -1), 2.0)
        if q is None:
            continue
        s = rng.uniform(0.03, 0.06)
        rk = P.blob((0, 0, 0), s * 1.1, s * 1.35, s * 0.75, n=8, m=5)
        rk.rough(s * 0.28, 16.0, seed=30 + i)
        rk.rot('z', rng.uniform(0, 180)).move(*(q + V(0, 0, s * 0.12)))
        clods.append((py, rk))
    for bn, sel in (('body', [r_ for y_, r_ in clods if y_ < -0.07]), ('abdomen1', [r_ for y_, r_ in clods if y_ >= -0.07])):
        if sel:
            cl = M.join(sel)
            cl.smooth = 60.0
            C.rigid(cl, bn, 'soil')
    C.rigid(P.blob((0, -0.40, 0.135), 0.085, 0.085, 0.05, n=10, m=6), 'head', 'limb', smooth=180)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        m = V(sg, 1, 1)
        C.rigid(P.blob((sg * 0.07, -0.43, 0.15), 0.017, 0.02, 0.016, n=7, m=5), 'head', 'limb', smooth=180)
        C.rigid(RC.mandible((sg * 0.035, -0.45, 0.115), (sg * 0.012, -0.55, 0.10), 0.016, curve=0.28, side=sg), 'mandible_' + s, 'limb')
        C.rigid(RC.antenna([(sg * 0.065, -0.425, 0.15), (sg * 0.10, -0.47, 0.165), (sg * 0.14, -0.50, 0.17)], 0.008, 0.007, club=0.05, n=5), 'antenna_' + s, 'limb')
        # the spade foreleg: a thick arm and a toothed digging hand
        arm = M.join([P.seg_tube(SH * m - unit(WR - SH) * m * 0.05, WR * m, 0.058, 0.05, n=8), P.blob(WR * m, 0.06, 0.06, 0.05, n=8, m=5)])
        C.rigid(arm, 'spade_%s_a' % s, 'limb', smooth=50)
        hd = spade_hand(float(np.linalg.norm(TP - WR)) * 1.02, 0.23, 0.034, teeth=4)
        if sg < 0:
            hd.scale((-1, 1, 1))
        hd.orient(unit(TP - WR) * m, unit(V(-sg * 0.45, 0.0, 0.9)), WR * m - unit(TP - WR) * m * 0.02)
        C.rigid(hd, 'spade_%s_b' % s, 'limb')
    RC.add_legs(C, legs, 'limb', n=7, spur=0.035)
    hb = ubar(0.15, 0.13, 0.009)
    plate_ = M.box(0.19, 0.012, 0.03, bevel=0.003).move(0, 0, 0.002)
    hd_ = M.join([hb, plate_])
    hd_.tf(M.frame_from(unit(V(fd[0], fd[1], 0)), (0, 0, 1)))
    hd_.move(*hp)
    hd_.smooth = 40.0
    C.rigid(hd_, 'handle', 'iron')

    # ---- clips
    def spades(p, u, dig=0.0, spread=0.0, lift=0.0, n=2, alt=True):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            c = dig * osc(u, n, 0.0 if (sg > 0 or not alt) else 0.5)
            p.rot('spade_%s_a' % s, x=-lift + 0.6 * c, z=sg * spread)
            p.rot('spade_%s_b' % s, x=c)

    def swing(p, u, amp=6.0, n=2, lean=0.0):
        p.rot('handle', x=lean + amp * osc(u, n, 0.15), y=0.6 * amp * osc(u, n, 0.4))
    A.idle(dur=3.0, feel=12.0, jaw=6.0, extra=lambda p, u: (spades(p, u, dig=3.0, n=2), swing(p, u, 3.0, 1)))
    A.gait('Walk', 1.5, 0.4, 0.62, RC.tripod(2), lift=0.045, bob=0.004, roll=1.6, yaw=2.2, feel=8.0,
           extra=lambda p, u: (spades(p, u, dig=7.0, n=1), swing(p, u, 9.0, 2)))
    A.gait('Run', round(4.4 / 1.05, 3), 0.2667, 0.36, RC.tripod(2), lift=0.07, bob=0.006, roll=2.0, yaw=2.6, feel=5.0, pitch=2.0, crouch=0.01,
           extra=lambda p, u: (spades(p, u, dig=5.0, spread=6.0, lift=8.0, n=1), swing(p, u, 14.0, 2, 10.0)))

    def emerge(p, u):                       # sinks, then bursts up and forward with the spades flung apart, and bites
        hu = 0.42 / 0.9
        d = bump(u, 0.0, hu * 0.4, hu * 0.55, hu * 0.8)
        s = bump(u, hu * 0.6, hu, hu + 0.1, 0.95)
        A.body(p, dy=0.05 * d - 0.20 * s, dz=-0.06 * d + 0.06 * s, pitch=6 * d - 12 * s)
        A.head(p, pitch=-6 * s)
        A.jaws(p, 40 * bump(u, hu * 0.5, hu * 0.85, hu - 0.02, hu + 0.04) + 2)
        A.feel(p, u, amp=5, n=3, lift=20 * s)
        A.abdo(p, pitch=5 * s)
        for sd, sg in (('l', 1.0), ('r', -1.0)):
            p.rot('spade_%s_a' % sd, x=5 * d - 22 * s, z=sg * (-12 * d + 40 * s))
            p.rot('spade_%s_b' % sd, x=7 * d - 10 * s)
        swing(p, u, 8.0, 3)
        A.plant(p)
    C.clip('Attack1', 0.9, emerge, hit=0.42, under_ok=True)          # it sinks into the soil before it bursts out
    C.reach('Attack1', 'mandible_l', (0.012, -0.55, 0.10))

    def undermine(p, u):                    # hunkers over the spot and digs it out from under the target
        hu = 0.8 / 1.3
        g = bump(u, 0.0, 0.12, hu, hu + 0.2)
        k = bump(u, hu - 0.04, hu, hu + 0.06, hu + 0.25)
        A.body(p, dy=-0.10 * g, dz=-0.03 * g - 0.02 * k, pitch=6 * g + 2 * k, roll=2.5 * g * osc(u, 5))
        A.head(p, pitch=8 * g)
        A.jaws(p, 10 * g + 2)
        A.feel(p, u, amp=6, n=5, lift=-15 * g)
        for sd, sg in (('l', 1.0), ('r', -1.0)):
            c = g * osc(u, 5, 0.0 if sg > 0 else 0.5)
            p.rot('spade_%s_a' % sd, x=3 * g + 8 * c, z=-sg * 10 * g)
            p.rot('spade_%s_b' % sd, x=4 * g + 14 * c)
        swing(p, u, 10.0 * g, 5)
        A.plant(p)
    C.clip('Attack2', 1.3, undermine, hit=0.8, under_ok=True)        # the spades dig into the ground
    C.reach('Attack2', 'spade_l_b', TP)
    A.hit(extra=lambda p, u, k: (spades(p, u, spread=14 * k, lift=16 * k), swing(p, u, 12 * k, 3)))

    def die(p, u, f):
        for sd, sg in (('l', 1.0), ('r', -1.0)):
            p.rot('spade_%s_a' % sd, z=-sg * 30 * f, x=-20 * f)
            p.rot('spade_%s_b' % sd, x=-25 * f)
        p.rot('handle', x=-40 * f)
    A.death_flip(dur=1.5, back_h=0.24, extra=die)
    A.spawn_dig(dur=1.3, extra=lambda p, u, r: (spades(p, u, dig=16 * (1 - r), spread=10 * (1 - r), n=5), swing(p, u, 8.0, 4)))
    return C.finalize(bake=bake, notes="walks on two leg pairs; the spade forelegs are spade_<s>_a/b (not in the gait); bone `handle` = the coffin handle")


@reg
def sexton_matron(bake=True):
    """Coffin-mother: an ox-sized burying beetle carrying a whole coffin on her back, lashed on with roots and her own
    hardened secretion; the lid is ajar and grubs spill from the gap, pale green light inside; small shovel head"""
    R = (0.066, 0.046, 0.024)
    legs = [RC.leg_def((0.17, -0.28, 0.24), (0.43, -0.46, 0.48), (0.58, -0.63, 0.085), (0.63, -0.70, 0.0), r=R),
            RC.leg_def((0.19, -0.02, 0.24), (0.52, -0.03, 0.51), (0.70, 0.03, 0.085), (0.77, 0.04, 0.0), r=R),
            RC.leg_def((0.17, 0.24, 0.24), (0.43, 0.48, 0.48), (0.59, 0.72, 0.085), (0.63, 0.80, 0.0), r=R)]
    CY = 0.245                                                           # the coffin: centre of its length (world y), floor height, box top
    Z0, Z1 = 0.45, 0.69
    HEX = np.array([(-0.13, -0.625), (0.13, -0.625), (0.225, -0.30), (0.105, 0.625), (-0.105, 0.625), (-0.225, -0.30)]) + (0, CY)
    GR = [[(0.10, -0.19, 0.63), (0.155, -0.18, 0.70), (0.205, -0.17, 0.705), (0.235, -0.16, 0.63)],          # hangs over the left wall
          [(0.05, -0.27, 0.63), (0.085, -0.33, 0.70), (0.105, -0.375, 0.705), (0.12, -0.41, 0.64)],          # climbs out at the corner
          [(0.245, 0.02, 0.66), (0.243, 0.05, 0.60), (0.24, 0.085, 0.53), (0.237, 0.12, 0.47)],              # down the side of the box
          [(0.185, -0.33, 0.47), (0.215, -0.28, 0.47), (0.24, -0.22, 0.445), (0.255, -0.16, 0.42)]]          # on her thorax

    def extra(sk):
        sk.bone('coffin', (0, CY, Z0), (0, CY, Z1), 'body')
        sk.bone('lid', (-0.20, CY + 0.3, Z1 + 0.01), (-0.20, CY - 0.3, Z1 + 0.01), 'coffin')
        for i, g in enumerate(GR):
            sk.bone('grub%d' % (i + 1), g[0], g[-1], 'coffin' if i < 3 else 'body')
    sk = RC.arth_skel(body=((0, 0.10, 0.30), (0, -0.36, 0.30)), head=((0, -0.40, 0.26), (0, -0.70, 0.22)), legs=legs,
                      abd=[(0, 0.08, 0.30), (0, 0.78, 0.28)], mand=((0.06, -0.60, 0.16), (0.025, -0.75, 0.14)),
                      ant=[(0.13, -0.52, 0.27), (0.28, -0.66, 0.32), (0.33, -0.76, 0.34)], extra=extra)
    C = B.Beast('mon_sexton_matron', sk, budget=22000)
    std_meta(C, 'sexton', 1.7, 2.8)
    A = RC.Arth(C, legs, pivot=(0, 0.1, 0.28))
    C.look('shell', 'chitin', 'bone_shell', color=(0.016, 0.015, 0.016), bands=[(0.10, 0.19)], band_color=(0.46, 0.17, 0.03), rough=0.26, dust=0.45,
           edge=0.5)
    C.look('limb', 'chitin', 'bone_shell', color=(0.03, 0.024, 0.02), rough=0.34, pit=170.0, dust=0.2, axis=(1, 0, 0))
    C.look('belly', 'chitin', 'bone_shell', color=(0.05, 0.035, 0.025), rough=0.45, ridge=30.0, ridge_h=1.6, dust=0.0)
    C.look('grub', 'slime', 'bone_shell', color=(0.50, 0.45, 0.34), color2=(0.30, 0.25, 0.17), belly=(0.56, 0.50, 0.38), ring=0.0, rough=0.22)
    C.look('wood', 'wood', 'leather_coffin', color=(0.085, 0.06, 0.04), axis=(0, 1, 0))
    C.look('root', 'bark', 'leather_coffin', color=(0.05, 0.038, 0.028), ash=(0.16, 0.14, 0.12), ash_amt=0.3, axis=(1, 0, 0), scale=40.0)
    C.look('resin', 'wax', 'leather_coffin', color=(0.30, 0.19, 0.07), soot=0.25)
    C.look('iron', 'metal', 'metal_iron', color=(0.18, 0.17, 0.16), rust=(0.20, 0.09, 0.035), rust2=(0.07, 0.035, 0.02), amount=0.8, rough=0.55)
    C.look('light', 'glow', 'emis_coffin', color=(0.16, 0.20, 0.11), emit=(0.55, 0.92, 0.45), strength=1.8, vein=(0.03, 0.05, 0.02), vein_scale=16.0,
           scale=9.0)
    # ---- the beetle
    core(C, P.blob((0, 0.02, 0.25), 0.27, 0.50, 0.10, n=16, m=7), 'body', 'belly')
    C.rigid(P.blob((0, 0.38, 0.30), 0.29, 0.45, 0.13, n=16, m=7), 'abdomen1', 'belly', smooth=180)
    pr = RC.shell(0.40, 0.58, 0.215, n=22, m=6, power=2.6, front=0.72, rear=1.0).move(0, -0.25, 0.27)
    C.rigid(pr, 'body', 'shell', smooth=60)
    for sg in (1, -1):                                                   # vestigial wing-cases: two flaps under the box
        fl = RC.shell(0.36, 0.21, 0.07, n=14, m=5, power=2.3, front=0.9, rear=0.5)
        fl.rot('y', sg * 16).rot('z', -sg * 24).move(sg * 0.25, 0.17, 0.345)
        C.rigid(fl, 'abdomen1', 'shell', smooth=60)
    C.rigid(P.blob((0, -0.55, 0.20), 0.12, 0.17, 0.07, n=12, m=6), 'head', 'limb', smooth=180)
    half = [(0.0, -0.42), (0.10, -0.43), (0.17, -0.48), (0.205, -0.56), (0.175, -0.655), (0.09, -0.705), (0.0, -0.72)]
    sh = RC.plate(mirror_outline(half)[::-1], th=0.03, z=0.275, dome=0.02, tilt=0.0, bevel=0.35)
    sh.subdiv(1)
    sh.v[:, 2] -= 0.17 * np.clip((-sh.v[:, 1] - 0.42), 0, 1) + 0.3 * np.abs(sh.v[:, 0]) ** 2
    sh.v[:, 2] += 0.014 * np.exp(-(sh.v[:, 0] / 0.05) ** 2)
    C.rigid(sh, 'head', 'shell', smooth=45)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(P.blob((sg * 0.125, -0.57, 0.235), 0.028, 0.036, 0.026, n=8, m=5), 'head', 'limb', smooth=180)
        C.rigid(RC.mandible((sg * 0.06, -0.60, 0.16), (sg * 0.025, -0.75, 0.14), 0.028, curve=0.28, side=sg), 'mandible_' + s, 'limb')
        C.rigid(RC.antenna([(sg * 0.12, -0.52, 0.26), (sg * 0.20, -0.59, 0.295), (sg * 0.28, -0.66, 0.32)], 0.012, 0.009, n=5), 'antenna1_' + s, 'limb')
        C.rigid(RC.antenna([(sg * 0.275, -0.655, 0.318), (sg * 0.31, -0.71, 0.33)], 0.009, 0.009, club=0.09, n=5), 'antenna2_' + s, 'limb')
    RC.add_legs(C, legs, 'limb', n=8, spur=0.05)
    # ---- the coffin: floor, walls, the glowing mass inside, a lid pushed askew
    cc = (0, CY)
    box = [M.prism(HEX, Z0, Z0 + 0.06), ring_prism(HEX, scaled(HEX, (0.867, 0.952), cc), Z0 + 0.06, Z1)]
    for q in box:
        C.rigid(q, 'coffin', 'wood', smooth=25)
    fill = domed_plate(scaled(HEX, (0.875, 0.955), cc), 0.05, rings=3, th=0.01, power=0.8, top=0.3)
    fill.subdiv(1)
    fill.rough(0.012, 9.0, seed=5)
    fill.move(0, 0, Z1 - 0.12)
    C.rigid(fill, 'coffin', 'light', smooth=180)
    pv = (0, CY + 0.625, 0)
    lid = [M.prism(scaled(HEX, 1.035, cc), Z1 + 0.004, Z1 + 0.034), M.prism(scaled(HEX, 0.76, cc), Z1 + 0.03, Z1 + 0.048)]
    for q in lid:
        q.rot('z', -8.5, pivot=pv).rot('y', -2.5, pivot=(-0.2, 0, Z1))
        C.rigid(q, 'lid', 'wood', smooth=25, free=True)                # free: it flies open and slides off in Death
    hs = []
    for sg in (1, -1):
        for yl, xw in ((-0.05, 0.193), (0.38, 0.137)):
            h_ = ubar(0.13, 0.085, 0.009)
            h_.rot('z', 90 + sg * 7.4).rot('y', -sg * 12, pivot=(0, 0, 0)).move(sg * (xw + 0.012), CY + yl, Z0 + 0.15)
            hs.append(h_)
    C.rigid(M.join(hs), 'coffin', 'iron', smooth=40)
    # roots lashing the box to her, her own secretion hardened where it sits
    rts = []
    for i, (yw, r_) in enumerate(((0.08, 0.024), (0.50, 0.02), (0.76, 0.022), (0.14, 0.013), (0.58, 0.012))):
        w = 0.225 - 0.12 * (yw - CY + 0.30) / 0.925                    # half width of the box here
        hl = [(0.0, 0.147), (0.19, 0.15), (0.295, 0.21), (0.31, 0.31), (0.29, 0.41), (w + 0.045, 0.475), (w + r_ * 0.8, 0.58), (w + r_ * 0.8, 0.69),
              (w - 0.03, 0.748), (0.0, 0.752)]
        loop = hl + [(-x, z) for x, z in reversed(hl[1:-1])]
        ctrl = np.array([(x, yw + 0.02 * math.sin(2.3 * k + i), z) for k, (x, z) in enumerate(loop)] + [(loop[0][0], yw + 0.02 * math.sin(i), loop[0][1])])
        path = M.polyline(ctrl, 41)[:-1]
        t = M.tube(path, r_, n=6, closed=True, up=(0, 1, 0))
        t.rough(0.006, 9.0, seed=i)
        rts.append(t)
    rt = M.join(rts)
    rt.smooth = 60.0
    C.rigid(rt, 'coffin', 'root')
    gl = [P.blob((sg * xw, yw, Z0 - 0.02), 0.07, 0.10, 0.05, n=8, m=5) for sg in (1, -1) for yw, xw in ((-0.10, 0.17), (0.25, 0.15), (0.60, 0.11))]
    gl.append(P.blob((0, -0.28, Z0 + 0.01), 0.12, 0.09, 0.05, n=8, m=5))
    gq = M.join(gl)
    gq.rough(0.01, 12.0, seed=2)
    C.rigid(gq, 'coffin', 'resin', smooth=180)
    for i, g in enumerate(GR):
        bd, hd = grub(g, 0.034, n=8, m=13)
        C.rigid(bd, 'grub%d' % (i + 1), 'grub', free=True)             # free: loose animals, they pour out in Attack2
        C.rigid(hd, 'grub%d' % (i + 1), 'limb', smooth=180, free=True)
    GB = ['grub%d' % (i + 1) for i in range(4)]

    # ---- clips
    def load(p, u, sway=0.0, rattle=0.0, n=2, wrig=6.0):
        p.rot('coffin', y=sway * osc(u, max(1, n // 2), 0.2))
        p.rot('lid', y=-rattle * max(0.0, osc(u, n, 0.1)))
        for i, b in enumerate(GB):
            p.rot(b, z=wrig * osc(u, n + 1, 0.23 * i), x=0.6 * wrig * osc(u, n, 0.31 * i))
    A.idle(dur=3.6, breath=0.008, feel=12.0, jaw=6.0, sway=0.006, abd=2.0, extra=lambda p, u: load(p, u, 0.6, 2.0 * bump(u, 0.55, 0.6, 0.7, 0.8), 6, 7.0))
    A.gait('Walk', 0.6, 0.9333, 0.66, RC.tripod(3), lift=0.07, bob=0.008, roll=2.2, yaw=2.0, feel=8.0, abd=1.0,
           extra=lambda p, u: load(p, u, 1.5, 2.5, 2, 6.0))
    A.gait('Run', round(2.8 / 1.7, 3), 0.5333, 0.5, RC.tripod(3), lift=0.10, bob=0.014, roll=3.0, yaw=2.4, feel=5.0, pitch=1.5, abd=1.0,
           extra=lambda p, u: load(p, u, 1.8, 5.0, 2, 8.0))
    A.ram('Attack1', dur=1.2, hit=0.65, reach=0.30, lower=6.0, back=0.10,
          extra=lambda p, u, w, s: (p.rot('head', x=-8 * s - 4 * w), p.move('body', 0, 0, 0.03 * s), p.rot('lid', y=-9 * bump(u, 0.52, 0.58, 0.62, 0.8)),
                                    p.rot('coffin', x=3 * s - 2 * w)))

    def spill(p, u):                        # heaves her load over to the left: the lid flies open and the brood pours out
        hu = 0.8 / 1.5
        w = bump(u, 0.0, hu * 0.55, hu + 0.15, 0.95)
        k = bump(u, hu * 0.6, hu, hu + 0.1, hu + 0.3)
        shk = w * osc(u, 7)
        A.body(p, dx=0.03 * w, dz=0.02 * w, roll=12 * w + 1.5 * shk, pitch=-3 * w)
        A.head(p, pitch=6 * w, yaw=-8 * w)
        A.jaws(p, 12 * w + 2)
        A.feel(p, u, amp=8, n=4, lift=10 * w)
        p.rot('coffin', y=5 * w + shk)
        p.rot('lid', y=-34 * k - 4 * w)
        for i, b in enumerate(GB):
            g = sm(u, hu * 0.7 + 0.03 * i, hu + 0.05 * i) * (1 - sm(u, 0.82, 0.97))
            p.move(b, 0.24 * g, -0.03 * i * g, -0.28 * g)
            p.rot(b, z=14 * osc(u, 5, 0.2 * i), y=40 * g)
        A.plant(p)
    C.clip('Attack2', 1.5, spill, hit=0.8)
    A.hit(dur=0.5, back=0.05, extra=lambda p, u, k: (p.rot('lid', y=-12 * k), p.rot('coffin', x=-3 * k, y=3 * k)))

    def die(p, u, f):                       # the lid slides off, the box tips, the brood spills
        g = sm(u, 0.25, 0.7)
        p.rot('coffin', y=3 * g)
        p.move('lid', 0.16 * g, 0.0, -0.05 * g)
        p.rot('lid', y=24 * g)
        for i, b in enumerate(GB):
            p.move(b, 0.12 * g, 0.0, -0.08 * g)
            p.rot(b, z=10 * (1 - g) * osc(u, 6, 0.2 * i))
    A.death_drop(dur=1.7, splay=0.16, tilt=9.0, extra=die)
    A.spawn_dig(dur=1.5, depth=1.35, shake=4.0, extra=lambda p, u, r: load(p, u, 1.0, 6.0 * (1 - r), 4, 8.0))
    return C.finalize(bake=bake, notes="giant budget; bones coffin, lid (hinged on its right edge), grub1-4; Attack2 = the lid opens and the grubs pour out to her left")


# ================================================================================================ rust_gear
def cog(R, root, rim_in, hub_out, hub_in, th, hub_th, teeth=18, spokes=5):
    """cog wheel in the XY plane (axle = Z) centred on the origin: toothed rim, hub ring, spokes (three Parts)"""
    out, inn = [], []
    for i in range(teeth):
        a0 = TAU * i / teeth
        for f, rr in ((0.0, root), (0.13, R), (0.50, R), (0.63, root)):
            a = a0 + TAU / teeth * f
            out.append((rr * math.cos(a), rr * math.sin(a)))
            inn.append((rim_in * math.cos(a), rim_in * math.sin(a)))
    rim = ring_prism(out, inn, -th / 2, th / 2)

    def cir(r_, n):
        return [(r_ * math.cos(TAU * j / n), r_ * math.sin(TAU * j / n)) for j in range(n)]
    hub = ring_prism(cir(hub_out, 20), cir(hub_in, 20), -hub_th / 2, hub_th / 2)
    sp = []
    for i in range(spokes):
        b = M.box(rim_in - hub_out + 0.04, 0.075, th * 0.72).move((rim_in + hub_out) / 2, 0, 0)
        b.rot('z', 360.0 * (i + 0.5) / spokes)
        sp.append(b)
    parts = [rim, hub, M.join(sp)]
    for q in parts:
        q.smooth = 30.0
    return parts


@reg
def rust_gear(bake=True):
    """Cog-roller: a toothed brass cog-wheel a metre across with a soft many-legged hermit in its hub. It walks with the
    wheel tilted on its back; Attack2 tips the wheel upright and pulls the body into the hub, Run holds that pose"""
    RW = 0.5
    TILT = 30.0                                                          # rest tilt of the axle from the vertical, towards +X
    WC = V(0.03, 0.12, 0.47)                                             # wheel centre in the walking pose
    BC = V(0.0, -0.04, 0.15)                                             # centre of the hermit's body
    nrm = V(math.sin(math.radians(TILT)), 0, math.cos(math.radians(TILT)))
    legs = RC.radial_legs(4, -0.20, 0.10, 0.09, 0.13, 0.25, 0.22, fan=(-50, 45), knee_out=0.5, toe=0.035, ankle_h=0.04, r=(0.026, 0.019, 0.011))
    sk = RC.arth_skel(body=((0, 0.10, 0.15), (0, -0.20, 0.15)), head=((0, -0.22, 0.14), (0, -0.36, 0.12)), legs=legs,
                      mand=((0.04, -0.33, 0.11), (0.02, -0.45, 0.09)), ant=[(0.05, -0.30, 0.17), (0.12, -0.50, 0.24)],
                      extra=lambda sk: sk.bone('wheel', WC, WC + nrm * 0.2, 'body'))
    C = B.Beast('mon_rust_gear', sk)
    std_meta(C, 'rust', 1.0, 3.0, wheel_radius=RW, wheel_center_run=[0.0, 0.0, RW])
    A = RC.Arth(C, legs, pivot=(0, -0.04, 0.15))
    C.look('brass', 'metal', 'metal_brass', color=(0.32, 0.22, 0.07), rust=(0.06, 0.16, 0.12), rust2=(0.035, 0.07, 0.055), amount=0.55,
           edge_color=(0.74, 0.58, 0.27), rough=0.4, hammer=22.0, edge=1.0)
    C.look('limb', 'chitin', 'bone_body', color=(0.20, 0.15, 0.09), color2=(0.30, 0.24, 0.15), edge_color=(0.42, 0.34, 0.22), rough=0.4,
           dust=0.5, dust_color=(0.30, 0.25, 0.17), axis=(1, 0, 0))
    C.look('soft', 'slime', 'bone_body', color=(0.34, 0.29, 0.24), color2=(0.17, 0.13, 0.11), belly=(0.42, 0.37, 0.30), ring=30.0, rough=0.28,
           axis=(0, 1, 0))
    C.look('eye', 'glass', 'bone_body', color=(0.02, 0.015, 0.03), rough=0.08)
    C.look('hub', 'glow', 'emis_hub', color=(0.20, 0.16, 0.24), emit=(0.60, 0.42, 1.0), strength=1.5, vein=(0.05, 0.03, 0.07), vein_scale=14.0,
           scale=9.0)
    # ---- the hermit: body with the legs, a neck going up into the hub, a bulb that fills the hub
    core(C, P.blob(BC, 0.14, 0.25, 0.10, n=14, m=7), 'body', 'soft')
    npth = [(0, 0.0, 0.18), (0.01, 0.07, 0.32), tuple(WC)]
    nk = P.tubep(npth, [0.11, 0.10, 0.11], [0.12, 0.11, 0.12], m=9, n=10)
    wpath(nk, npth, [(0.0, {'body': 1.0}), (1.0, {'wheel': 1.0})])
    C.skin(nk, 'soft', smooth=180)
    bulb = P.blob((0, 0, 0), 0.155, 0.155, 0.115, n=14, m=8)
    bulb.rough(0.012, 9.0, seed=3)
    bulb.tf(M.rot_to((0, 0, 1), nrm)).move(*WC)
    C.rigid(bulb, 'wheel', 'hub', smooth=180)
    C.rigid(P.blob((0, -0.27, 0.135), 0.085, 0.105, 0.07, n=10, m=6), 'head', 'limb', smooth=180)
    tipm = V(0.02, -0.45, 0.09)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(M.join([P.tubep([(sg * 0.045, -0.31, 0.17), (sg * 0.06, -0.33, 0.215)], 0.01, 0.01, m=2, n=5),
                        P.blob((sg * 0.062, -0.333, 0.225), 0.017, 0.017, 0.017, n=7, m=5)]), 'head', 'eye', smooth=180)
        C.rigid(RC.mandible((sg * 0.04, -0.33, 0.11), tipm * (sg, 1, 1), 0.02, curve=0.26, n=6, teeth=2, side=sg), 'mandible_' + s, 'limb')
        C.rigid(RC.antenna([(sg * 0.05, -0.30, 0.165), (sg * 0.09, -0.40, 0.21), (sg * 0.12, -0.50, 0.24)], 0.009, 0.006, n=5), 'antenna_' + s, 'limb')
    for q in cog(RW, 0.43, 0.345, 0.215, 0.15, 0.07, 0.12, teeth=18, spokes=5):
        q.tf(M.rot_to((0, 0, 1), nrm)).move(*WC)
        C.rigid(q, 'wheel', 'brass')
    RC.add_legs(C, legs, 'limb', n=6, claw=0.4)

    # ---- clips
    def tucked(p, k):
        """k = 0: the walking pose .. 1: the wheel upright on the origin with the hermit pulled into its hub"""
        Rk = B.ry(60.0 * k)
        Wt = WC + k * (V(0, 0, RW) - WC)
        tr = Wt - WC - (Rk @ (BC - WC)) * sm(k, 0.3, 1.0)
        A.body(p, dx=tr[0], dy=tr[1], dz=tr[2], roll=60.0 * k, pivot=WC)
        Gb = p.G('body')
        cur = (Gb @ np.r_[WC, 1.0])[:3]
        p.move('wheel', np.linalg.solve(Gb[:3, :3], Wt - cur))
        A.head(p, pitch=35 * k)
        A.jaws(p, 2 + 6 * k * (1 - k) * 4)

    def tk(k):
        return {kk: dict(bend=(40 * k, 60 * k, 20 * k)) for kk in A.legs}

    def wob(p, u, x=0.0, y=0.0, n=2):
        p.rot('wheel', x=x * osc(u, n, 0.15), y=y * osc(u, max(1, n // 2), 0.05))
    A.idle(dur=3.2, breath=0.006, feel=12.0, jaw=8.0, sway=0.006, extra=lambda p, u: wob(p, u, 1.2, 1.8, 2))
    A.gait('Walk', 1.0, 0.4, 0.6, RC.tripod(4), lift=0.05, bob=0.008, roll=4.0, yaw=3.0, sway=0.012, feel=8.0,
           extra=lambda p, u: wob(p, u, 2.5, -3.5, 2))

    def roll_fn(p, u):                      # the tucked wheel: the game spins the mesh; the legs keep scrabbling in the hub
        tucked(p, 1.0)
        A.feel(p, u, amp=3, n=2, lift=-40)
        A.plant(p, air=1.0, tuck=tk(1.0))
        for i, lg in enumerate(A.legs.values()):
            Pf = p.pt(lg.b[-1], lg.c)
            R3 = p.rotw(lg.b[2])
            pole = p.pt(lg.b[1], C.rg.H(lg.b[1])) - p.pt(lg.b[0], C.rg.H(lg.b[0]))
            p.leg(lg, Pf + V(0, 0, 0.06 * math.cos(TAU * (u + (i % 4) / 4.0))), R3=R3, pole=pole)
    C.clip('Run', 8 / 30.0, roll_fn)
    C.meta['run_speed'] = 3.0
    A.snap(dur=0.6, hit=0.3, reach=0.16, gape=32.0, lift=5.0, drop=0.01, extra=lambda p, u, w, s: p.rot('wheel', x=-3 * w + 5 * s))
    C.reach('Attack1', 'mandible_l', tipm)

    def tuck_fn(kf, wf):
        def fn(p, u):
            k = kf(u)
            tucked(p, k)
            p.rot('wheel', y=wf(u))
            A.feel(p, u, amp=4, n=3, lift=-40 * k)
            A.plant(p, air=sm(k, 0.0, 0.35), tuck=tk(k))
        return fn
    C.clip('Attack2', 1.2, tuck_fn(lambda u: sm(u, 0.05, 0.40) * (1 - sm(u, 0.86, 1.0)), lambda u: 2.5 * bump(u, 0.38, 0.45, 0.55, 0.8) * osc(u, 6)),
           hit=0.5, floor='body')
    C.reach('Attack2', 'wheel', WC + V(0, -RW, 0))
    A.hit(dur=0.4, back=0.05, extra=lambda p, u, k: p.rot('wheel', x=-6 * k, y=5 * k))

    def die(p, u, f):                       # the brass drops flat over the body and rings like a coin
        g = sm(u, 0.2, 0.6)
        w_ = bump(u, 0.55, 0.62, 0.7, 0.98) * osc(u, 6)
        p.rot('wheel', x=3 * w_, y=-TILT * g + 3 * w_)
        p.move('wheel', -0.03 * g, 0.0, -0.21 * g)
    A.death_drop(dur=1.5, splay=0.10, tilt=0.0, extra=die)
    C.clip('Spawn', 1.2, tuck_fn(lambda u: 1 - sm(u, 0.25, 0.8), lambda u: 3.0 * bump(u, 0.0, 0.08, 0.2, 0.3) * osc(u, 5)), floor='body')
    return C.finalize(bake=bake, notes="Run = the tucked wheel (centre on the origin at z 0.5, axle = X: spin it about that axle); Attack2 is fully tucked "
                                       "from 0.5 s to 1.03 s (cut to Run at the hit time); Spawn = the standing wheel unfolds; bone `wheel`")


# ================================================================================================ rust_jar
@reg
def rust_jar(bake=True):
    """Jar-crab: a tall brass storm-jar lying tilted on eight splayed legs, mouth forward like a cannon, two copper rods
    out of the neck as feelers, a tangle of wire dragging behind; violet-white light in the mouth"""
    legs = RC.radial_legs(4, -0.24, 0.24, 0.13, 0.20, 0.50, 0.40, fan=(-52, 52), knee_out=0.5, toe=0.05, ankle_h=0.05, r=(0.036, 0.026, 0.014))
    ang = math.radians(22.0)
    d = V(0, -math.cos(ang), math.sin(ang))                             # jar axis: foot -> mouth
    upv = V(0, math.sin(ang), math.cos(ang))
    F = V(0, 0.44, 0.33)

    def jp(s, x=0.0, up=0.0):
        return F + d * s + V(x, 0, 0) + upv * up
    Mo = jp(1.0)
    WP = [(0.0, 0.46, 0.30), (0.02, 0.66, 0.06), (0.0, 1.0, 0.015), (0.03, 1.38, 0.015)]

    def extra(sk):
        sk.bone('jar', jp(0.45), jp(0.75), 'body')
        sk.lr('antenna', jp(0.86, 0.035), jp(1.42, 0.17, 0.04), 'jar')
        sk.chain('wire', WP, 'root')
    sk = RC.arth_skel(body=((0, 0.16, 0.22), (0, -0.20, 0.22)), head=None, legs=legs, mand=((0.10, -0.29, 0.20), (0.07, -0.50, 0.14)), extra=extra)
    C = B.Beast('mon_rust_jar', sk)
    std_meta(C, 'rust', 1.0, 2.8)
    A = RC.Arth(C, legs, pivot=(0, 0.0, 0.22))
    C.look('jar', 'metal', 'metal_jar', color=(0.26, 0.18, 0.07), rust=(0.05, 0.15, 0.11), rust2=(0.03, 0.065, 0.05), amount=0.6,
           edge_color=(0.66, 0.50, 0.24), rough=0.45, hammer=18.0)
    C.look('copper', 'metal', 'metal_jar', color=(0.42, 0.17, 0.08), rust=(0.07, 0.22, 0.16), rust2=(0.04, 0.10, 0.07), amount=0.35,
           edge_color=(0.86, 0.46, 0.28), rough=0.4)
    C.look('shell', 'chitin', 'bone_crab', color=(0.16, 0.10, 0.055), color2=(0.26, 0.18, 0.10), edge_color=(0.42, 0.32, 0.20), rough=0.36,
           dust=0.5, dust_color=(0.30, 0.25, 0.17))
    C.look('limb', 'chitin', 'bone_crab', color=(0.19, 0.13, 0.07), color2=(0.30, 0.22, 0.13), edge_color=(0.44, 0.34, 0.22), rough=0.4,
           dust=0.45, dust_color=(0.30, 0.25, 0.17), axis=(1, 0, 0))
    C.look('eye', 'glass', 'bone_crab', color=(0.02, 0.015, 0.03), rough=0.08)
    C.look('arc', 'emis', 'emis_arc', emit=(0.58, 0.40, 1.0), strength=1.6, mottle=0.5, base_mul=0.6, scale=40.0)
    # ---- the crab
    core(C, P.blob((0, 0.0, 0.22), 0.25, 0.33, 0.09, n=14, m=7), 'body', 'shell')
    C.rigid(RC.shell(0.62, 0.48, 0.085, n=18, m=5, power=2.4, front=0.8, rear=0.7).move(0, 0.0, 0.235), 'body', 'shell', smooth=60)
    tipm = V(0.07, -0.50, 0.14)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(M.join([P.tubep([(sg * 0.07, -0.28, 0.26), (sg * 0.085, -0.31, 0.34)], 0.012, 0.012, m=2, n=5),
                        P.blob((sg * 0.087, -0.313, 0.35), 0.022, 0.022, 0.022, n=7, m=5)]), 'body', 'eye', smooth=180)
        C.rigid(M.join([P.seg_tube(V(sg * 0.10, -0.26, 0.21), V(sg * 0.10, -0.33, 0.185), 0.035, 0.03, n=7),
                        RC.mandible((sg * 0.10, -0.31, 0.19), tipm * (sg, 1, 1), 0.03, curve=0.3, n=6, teeth=2, side=sg)]), 'mandible_' + s, 'limb', smooth=50)
    RC.add_legs(C, legs, 'limb', n=7, spur=0.03)
    # ---- the storm-jar
    prof = [(0.075, 0.0), (0.105, 0.04), (0.17, 0.18), (0.225, 0.36), (0.24, 0.48), (0.225, 0.60), (0.16, 0.72), (0.105, 0.80), (0.095, 0.88),
            (0.105, 0.94), (0.145, 0.985), (0.14, 1.0), (0.11, 1.0), (0.085, 0.93), (0.085, 0.86)]
    Rj = M.rot_to((0, 0, 1), d)
    jar = M.revolve(prof, n=18, cap=True)
    for dc, rad, amt in (((0.2, 0.05, 0.42), 0.09, -0.02), ((-0.16, -0.12, 0.55), 0.07, -0.016), ((0.05, 0.2, 0.3), 0.07, -0.014)):
        jar.dent(dc, rad, amt)
    jar.tf(Rj).move(*F)
    C.rigid(jar, 'jar', 'jar', smooth=60)
    hoops = [P.ring(jp(s), R_ + 0.004, 0.013, axis=d, n=18, m=6) for s, R_ in ((0.20, 0.178), (0.48, 0.24), (0.70, 0.172))]
    C.rigid(M.join(hoops), 'jar', 'copper', smooth=60)
    coil = [P.ring(jp(s), 0.103, 0.008, axis=d, n=16, m=5) for s in (0.835, 0.862, 0.889)]
    lugs = [P.tubep([jp(0.83, sg * 0.095), jp(0.815, sg * 0.20), jp(0.72, sg * 0.24), jp(0.65, sg * 0.20)], 0.02, 0.02, m=9, n=6) for sg in (1, -1)]
    C.rigid(M.join(coil + lugs), 'jar', 'copper', smooth=60)
    glow = P.blob((0, 0, 0), 0.083, 0.083, 0.016, n=12, m=5)
    glow.tf(Rj).move(*jp(0.872))
    C.rigid(glow, 'jar', 'arc', smooth=180)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        a_, b_ = jp(0.84, sg * 0.03), jp(1.42, sg * 0.17, 0.04)
        rod = M.join([P.tubep([a_, b_], 0.011, 0.011, m=2, n=6), P.blob(b_, 0.028, 0.028, 0.028, n=8, m=5)])
        C.rigid(rod, 'antenna_' + s, 'copper', smooth=60)
    # ---- the wire it drags
    wp = [(0.0, 0.44, 0.32), (0.03, 0.52, 0.20), (0.02, 0.66, 0.05), (-0.05, 0.82, 0.016), (0.07, 0.98, 0.016), (-0.03, 1.14, 0.016), (0.05, 1.30, 0.016),
          (0.03, 1.38, 0.02)]
    wr = P.tubep(wp, 0.009, 0.009, m=30, n=5)
    wpath(wr, wp, [(0.0, {'jar': 1.0}), (0.14, {'wire1': 1.0}), (0.42, {'wire2': 1.0}), (1.0, {'wire3': 1.0})])
    ws = [wr]
    for (cx, cy, rr, bn, tl) in ((0.02, 0.90, 0.075, 'wire2', 8.0), (-0.03, 1.13, 0.055, 'wire3', -10.0), (0.05, 1.02, 0.045, 'wire2', 14.0)):
        pth = np.array([(rr * math.cos(TAU * j / 12), rr * math.sin(TAU * j / 12), 0.0) for j in range(12)])
        lp = M.tube(pth, 0.008, n=5, closed=True, up=(0, 0, 1))
        lp.rot('x', tl).move(cx, cy, 0.016 + rr * abs(math.sin(math.radians(tl))))
        lp.W = M.rigid_W(len(lp.v), bn)
        ws.append(lp)
    wq = M.join(ws)
    C.skin(wq, 'copper', smooth=60)

    # ---- clips
    def jar_(p, u, aim=0.0, shake=0.0, n=14):
        p.rot('jar', x=-aim + shake * osc(u, n), z=0.6 * shake * osc(u, n, 0.25))

    def wire(p, u, amp=2.0, n=2):
        p.rot('wire1', z=amp * osc(u, n, 0.1))
        p.rot('wire2', z=-1.6 * amp * osc(u, n, 0.3))
        p.rot('wire3', z=2.0 * amp * osc(u, n, 0.5))
    A.idle(dur=3.4, breath=0.008, feel=7.0, jaw=10.0, sway=0.006, extra=lambda p, u: (jar_(p, u, aim=1.5 * osc(u, 1)), wire(p, u, 1.0, 1)))
    A.gait('Walk', 0.95, 0.6667, 0.62, RC.tripod(4), lift=0.08, bob=0.006, roll=1.5, yaw=2.0, feel=6.0,
           extra=lambda p, u: (jar_(p, u, aim=2.0 * osc(u, 2, 0.1)), wire(p, u, 5.0, 1)))
    A.gait('Run', 2.8, 0.4, 0.45, RC.tripod(4), lift=0.12, bob=0.01, roll=2.2, yaw=2.6, pitch=1.0, feel=4.0,
           extra=lambda p, u: (jar_(p, u, aim=3.0 * osc(u, 2, 0.1)), wire(p, u, 8.0, 1)))

    def bolt(p, u):                         # arc bolt: the rods spread and hum, the jar fires and kicks back
        hu = (14 / 30.0) / 0.9
        w = bump(u, 0.0, hu * 0.6, hu * 0.9, hu)
        k = bump(u, hu + 0.005, hu + 0.05, hu + 0.10, hu + 0.36)
        A.body(p, dy=0.10 * k - 0.03 * w, dz=-0.04 * w + 0.02 * k, pitch=-2 * w - 5 * k)
        jar_(p, u, aim=7 * w + 12 * k, shake=1.2 * w, n=16)
        A.feel(p, u, amp=5 * w, n=16, lift=6 * w, spread=14 * w - 10 * k)
        A.jaws(p, 14 * w + 2)
        wire(p, u, 4 * k, 3)
        A.plant(p)
    C.clip('Attack1', 0.9, bolt, hit=14 / 30.0)
    C.reach('Attack1', 'jar', Mo)

    def overcharge(p, u):                   # plants its legs wide, the jar tips up and shakes harder and harder, then discharges
        hu = 1.0 / 1.6
        g = bump(u, 0.0, 0.2, hu, hu + 0.25)
        ramp = sm(u, 0.1, hu) * (1 - sm(u, hu, hu + 0.06))
        k = bump(u, hu - 0.01, hu + 0.03, hu + 0.07, hu + 0.3)
        A.body(p, dz=-0.09 * g + 0.05 * k, pitch=-3 * g, roll=1.5 * ramp * osc(u, 18))
        jar_(p, u, aim=13 * g + 6 * k, shake=2.2 * ramp, n=22)
        A.feel(p, u, amp=9 * ramp, n=22, lift=10 * g, spread=24 * g - 16 * k)
        A.jaws(p, 20 * g + 2)
        wire(p, u, 6 * ramp, 9)
        A.plant(p, off={kk: (lg.side * 0.09 * g, 0.0, 0.0) for kk, lg in A.legs.items()})
    C.clip('Attack2', 1.6, overcharge, hit=1.0)
    A.hit(dur=0.45, back=0.06, extra=lambda p, u, k: (jar_(p, u, aim=-9 * k, shake=2 * k, n=5), wire(p, u, 5 * k, 3)))

    def die(p, u, f):                       # the jar slumps off its back, mouth to the ground
        g = sm(u, 0.2, 0.7)
        p.rot('jar', x=27 * g, z=24 * g)
        p.move('jar', 0.10 * g, -0.05 * g, -0.08 * g)
    A.death_drop(dur=1.5, splay=0.14, tilt=6.0, extra=die)
    A.spawn_dig(dur=1.3, depth=1.15, shake=5.0, extra=lambda p, u, r: p.scl('wire1', max(0.001, sm(u, 0.3, 0.75))))
    return C.finalize(bake=bake, notes="bone `jar` (aims / recoils), antenna_l/r = the copper rods, wire1-3 = the dragged wire; small pincers = mandible_l/r")


# ================================================================================================ cf_mite
def k_shard(N, p):
    """frozen flame: blue glass (the ice look), lit from inside more and more towards the tip (object z0 .. z1)"""
    r = L.KINDS['ice'](N, p)
    e = L._c(p, 'emit', (0.35, 0.72, 1.0))
    g = N.height_z(L._c(p, 'z0', 0.3), L._c(p, 'z1', 0.6))
    em = N.math('MULTIPLY', N.math('POWER', g, 1.6), N.mapr(N.noise(26, 3, 0.6), 0.25, 0.75, 0.55, 1.0))
    r['color'] = N.mix(N.math('MULTIPLY', g, 0.5), r['color'], tuple(min(1.0, x * 0.9) for x in e))
    r['emit'] = (e, L._c(p, 'strength', 1.8))
    r['emask'] = em
    return r


L.KINDS.update(shard=k_shard)
L.GRIME.update(shard=((0.02, 0.07, 0.16), 0.3))


def flame(base, h, lean, r, rng, n=5):
    """one glass flame tongue standing on `base`: swells above the root, leans out, the tip curls back"""
    lean = V(lean)
    side = V(rng.uniform(-1, 1), rng.uniform(-1, 1), 0) * 0.05 * h
    pts = [V(base) - V(0, 0, 0.03), V(base) + V(0, 0, 0.30 * h) + lean * 0.10 * h, V(base) + V(0, 0, 0.66 * h) + lean * 0.17 * h + side,
           V(base) + V(0, 0, h) + lean * 0.06 * h - side]
    q = P.tubep(pts, [0.75 * r, r, 0.6 * r, 0.03 * r], [0.5 * r, 0.62 * r, 0.36 * r, 0.02 * r], m=7, n=n, twist=rng.uniform(-0.6, 0.6))
    q.smooth = 22.0
    return q


@reg
def cf_mite(bake=True):
    """Glass-mite: a flat crab-like crawler whose shell carries a cluster of blue glass flame-shards like a crown of
    candles; six needle legs, two flat plough-jaws; ice-blue light in the shard tips"""
    legs = RC.radial_legs(3, -0.12, 0.12, 0.10, 0.13, 0.36, 0.27, fan=(-38, 38), knee_out=0.5, toe=0.04, ankle_h=0.045, r=(0.022, 0.015, 0.008))
    MJ, MT = V(0.07, -0.20, 0.125), V(0.04, -0.41, 0.07)
    sk = RC.arth_skel(body=((0, 0.12, 0.16), (0, -0.16, 0.16)), head=None, legs=legs, mand=(MJ, MT),
                      extra=lambda sk: sk.bone('crown', (0, 0.02, 0.24), (0, 0.02, 0.52), 'body'))
    C = B.Beast('mon_cf_mite', sk)
    std_meta(C, 'coldfire', 0.9, 4.2)
    A = RC.Arth(C, legs, pivot=(0, 0.0, 0.16))
    C.look('carap', 'chitin', 'bone_shell', color=(0.03, 0.04, 0.06), color2=(0.06, 0.09, 0.13), edge_color=(0.35, 0.45, 0.55), rough=0.3,
           dust=0.75, dust_color=(0.50, 0.58, 0.66))
    C.look('limb', 'chitin', 'bone_shell', color=(0.04, 0.05, 0.07), color2=(0.08, 0.11, 0.15), edge_color=(0.40, 0.50, 0.60), rough=0.32,
           dust=0.4, dust_color=(0.50, 0.58, 0.66), axis=(1, 0, 0))
    C.look('eye', 'glass', 'bone_shell', color=(0.01, 0.015, 0.03), rough=0.06)
    C.look('shard', 'shard', 'emis_shard', color=(0.16, 0.42, 0.74), deep=(0.015, 0.07, 0.22), frost=(0.70, 0.84, 0.94), frost_amt=0.35, rough=0.1,
           emit=(0.32, 0.70, 1.0), strength=1.8, z0=0.30, z1=0.62)
    core(C, P.blob((0, 0.0, 0.16), 0.27, 0.25, 0.075, n=16, m=7), 'body', 'carap')
    sh = RC.shell(0.52, 0.56, 0.085, n=20, m=5, power=2.4, front=0.85, rear=0.85).move(0, 0.01, 0.175)
    C.rigid(sh, 'body', 'carap', smooth=60)
    rimb = [P.blob((0.265 * math.cos(a), 0.01 + 0.245 * math.sin(a), 0.18), 0.03, 0.03, 0.022, n=6, m=4) for a in np.linspace(0, TAU, 14, endpoint=False)]
    C.rigid(M.join(rimb), 'body', 'carap', smooth=180)                   # knobs round the rim of the disc
    bvs = P.bvh_of(sh)
    rng = np.random.default_rng(12)
    sp = []
    spots = [(0.0, 0.02, 0.42, 0.05)] + [(0.085 * math.cos(a), 0.02 + 0.085 * math.sin(a), rng.uniform(0.26, 0.34), 0.042) for a in np.linspace(0.4, TAU + 0.4, 5, endpoint=False)]
    spots += [(0.165 * math.cos(a), 0.02 + 0.16 * math.sin(a), rng.uniform(0.13, 0.22), 0.034) for a in np.linspace(0.1, TAU + 0.1, 9, endpoint=False)]
    for (px, py, hh, rr) in spots:
        q, n_ = P.surf(bvs, (px, py, 1.0), (0, 0, -1), 2.0)
        if q is None:
            continue
        ln = unit(V(px, py - 0.02, 0.0)) if abs(px) + abs(py - 0.02) > 1e-6 else V(0, 0, 0)
        fq = flame(q, hh, ln, rr, rng)
        zz = np.clip((fq.v[:, 2] - q[2]) / hh, 0, 1)
        M.weights_by(fq, zz, [(0.05, {'body': 1.0}), (0.75, {'crown': 1.0})])
        sp.append(fq)
    C.skin(M.join(sp), 'shard', smooth=22)
    crown_tip = V(0.0, 0.02, 0.69)
    half = [(-0.035, 0.0), (-0.048, 0.11), (-0.02, 0.205), (0.03, 0.195), (0.05, 0.10), (0.036, 0.0)]
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        m = V(sg, 1, 1)
        pj = RC.plate(half, th=0.016, z=0.0, dome=0.006, bevel=0.5)
        if sg < 0:
            pj.scale((-1, 1, 1))
        pj.orient(unit(MT - MJ) * m, unit(V(-sg * 0.25, 0.3, 1.0)), MJ * m - unit(MT - MJ) * m * 0.03)
        C.rigid(pj, 'mandible_' + s, 'limb', smooth=35)
        C.rigid(M.join([P.tubep([(sg * 0.07, -0.19, 0.22), (sg * 0.085, -0.225, 0.275)], 0.009, 0.009, m=2, n=5),
                        P.blob((sg * 0.087, -0.228, 0.283), 0.015, 0.015, 0.015, n=7, m=5)]), 'body', 'eye', smooth=180)
    RC.add_legs(C, legs, 'limb', n=6, claw=0.25)

    def crown(p, u, tip=0.0, amp=1.5, n=2):
        p.rot('crown', x=tip + amp * osc(u, n, 0.1), y=0.7 * amp * osc(u, n, 0.35))
    A.idle(dur=2.8, breath=0.006, feel=0.0, jaw=12.0, sway=0.005, extra=lambda p, u: crown(p, u, 0.0, 1.5, 2))
    A.gait('Walk', 1.6, 0.4, 0.6, RC.tripod(3), lift=0.05, bob=0.005, roll=1.6, yaw=2.2, jaw=6.0, extra=lambda p, u: crown(p, u, 0.0, 2.5, 2))
    A.gait('Run', round(4.2 / 0.9, 3), 0.2333, 0.31, RC.tripod(3), lift=0.07, bob=0.007, roll=2.2, yaw=2.4, pitch=2.0, crouch=0.01, jaw=4.0,
           extra=lambda p, u: crown(p, u, -3.0, 4.0, 2))

    def flick(p, u):                        # shard flick: rears back, then whips the crown forward and looses a shard
        hu = 0.4 / 0.8
        w = bump(u, 0.0, hu * 0.6, hu * 0.8, hu)
        s = bump(u, hu * 0.8, hu, hu + 0.08, 0.9)
        A.body(p, dy=0.05 * w - 0.08 * s, dz=0.01 * w - 0.02 * s, pitch=-13 * w + 9 * s)
        A.jaws(p, 16 * w + 2)
        crown(p, u, -22 * w + 46 * s, 2.0, 5)
        A.plant(p)
    C.clip('Attack1', 0.8, flick, hit=0.4)
    C.reach('Attack1', 'crown', crown_tip)
    A.snap('Attack2', dur=0.6, hit=0.3, reach=0.17, gape=34.0, lift=5.0, drop=0.015, extra=lambda p, u, w, s: crown(p, u, -8 * w + 8 * s, 2.0, 3))
    C.reach('Attack2', 'mandible_l', MT)
    A.hit(dur=0.4, back=0.05, extra=lambda p, u, k: crown(p, u, -14 * k, 6 * k, 4))

    def die(p, u, f):                       # the flame gutters: the crown sags over and sinks
        g = sm(u, 0.3, 0.8)
        p.rot('crown', x=22 * g, y=34 * g)
        p.scl('crown', (1.0, 1.0 - 0.25 * g, 1.0))
    A.death_drop(dur=1.4, splay=0.12, tilt=8.0, extra=die)
    A.spawn_dig(dur=1.1, depth=0.95, shake=7.0)
    return C.finalize(bake=bake, notes="bone `crown` carries the glass flames (Attack1 whips it forward); look kind `shard` (ice + light towards the tips)")


# ================================================================================================ lamp_crawler
def tergite(y0, y1, w0, w1, h0, h1, z0=0.065, th=0.012, n=11, lift=0.012, skirt=0.035):
    """one arched back plate of a woodlouse between y0 (front) and y1 (rear): w = full width, h = arch height at the two
    edges; the rear edge is raised by `lift` so that it laps over the next plate; a flared skirt at both sides"""
    G = np.zeros((3, n + 2, 3))
    rows = ((y0, w0, h0, 0.0), ((y0 + y1) / 2, (w0 + w1) / 2 * 1.02, (h0 + h1) / 2 * 1.03, lift * 0.6), (y1, w1, h1, lift))
    for i, (y, w, h, dz) in enumerate(rows):
        for j in range(n):
            a = math.pi * j / (n - 1)
            G[i, j + 1] = (w / 2 * math.cos(a), y, z0 + dz + h * max(0.0, math.sin(a)) ** 0.8)
        G[i, 0] = (w / 2 + skirt, y, z0 + dz - 0.016)
        G[i, n + 1] = (-w / 2 - skirt, y, z0 + dz - 0.016)
    p = P.sheet_solid(G, th)
    p.smooth = 50.0
    return p


@reg
def lamp_crawler(bake=True):
    """Fungus-back: a flat armoured woodlouse the size of a shield, its plates overgrown with a row of small upright
    glowing caps like a tray of candles; many short legs under a skirt of plates"""
    legs = RC.radial_legs(5, -0.12, 0.10, 0.06, 0.07, 0.22, 0.145, fan=(-20, 20), knee_out=0.45, two=True, r=(0.014, 0.011, 0.007))

    def extra(sk):
        sk.bone('fore', (0, -0.13, 0.12), (0, -0.31, 0.11), 'body')
        sk.bone('head', (0, -0.31, 0.11), (0, -0.42, 0.09), 'fore')
    sk = RC.arth_skel(body=((0, 0.08, 0.12), (0, -0.13, 0.12)), head=None, legs=legs,
                      abd=[(0, 0.07, 0.12), (0, 0.24, 0.115), (0, 0.33, 0.10), (0, 0.42, 0.08)], extra=extra)
    C = B.Beast('mon_lamp_crawler', sk, budget=4000, bone_budget=28, mb=2.5, tex=1024)
    std_meta(C, 'lamp', 0.8, 5.0)
    A = RC.Arth(C, legs, pivot=(0, 0.0, 0.1))
    C.look('plate', 'chitin', 'bone_plates', color=(0.055, 0.05, 0.035), color2=(0.11, 0.10, 0.065), edge_color=(0.30, 0.27, 0.17), rough=0.42,
           dust=0.8, dust_color=(0.09, 0.12, 0.05), ridge=45.0, axis=(1, 0, 0))
    C.look('under', 'chitin', 'bone_plates', color=(0.09, 0.075, 0.05), color2=(0.15, 0.12, 0.08), rough=0.5, dust=0.0, axis=(1, 0, 0))
    C.look('lamp', 'glow', 'emis_caps', color=(0.42, 0.27, 0.09), emit=(1.0, 0.60, 0.18), strength=2.0, vein=(0.16, 0.08, 0.02), vein_scale=40.0,
           vein_w=0.03, scale=20.0)
    CH = ['head', 'fore', 'body', 'abdomen1', 'abdomen2', 'abdomen3']
    und = P.blob((0, 0.0, 0.085), 0.17, 0.37, 0.045, n=12, m=9)
    B.wbones(und, CH, sharp=4.0, k=2)
    und.smooth = 180.0
    C.skin(und, 'under')

    def Wd(y):
        return 0.50 * max(0.0, 1 - (abs(y) / 0.47) ** 2.2) ** 0.5

    def Hh(y):
        return 0.03 + 0.11 * max(0.0, 1 - (abs(y) / 0.47) ** 2.0) ** 0.6
    yb = [-0.30, -0.21, -0.12, -0.02, 0.08, 0.17, 0.25, 0.32]
    tb = ['fore', 'fore', 'body', 'body', 'abdomen1', 'abdomen1', 'abdomen2']
    for i in range(7):
        y0, y1 = yb[i], yb[i + 1] + 0.028
        C.rigid(tergite(y0, y1, Wd(y0), Wd(y1), Hh(y0), Hh(y1)), tb[i], 'plate')
    C.rigid(RC.shell(0.19, Wd(-0.33) * 1.04, Hh(-0.33) + 0.022, n=14, m=4, power=2.3, front=0.6, rear=1.0).move(0, -0.335, 0.05), 'head', 'plate', smooth=60)
    C.rigid(RC.shell(0.16, Wd(0.345) * 1.04, Hh(0.345) + 0.005, n=14, m=4, power=2.3, front=1.0, rear=0.4).move(0, 0.385, 0.05), 'abdomen3', 'plate', smooth=60)
    for sg in (1, -1):
        C.rigid(RC.antenna([(sg * 0.05, -0.40, 0.085), (sg * 0.10, -0.47, 0.10), (sg * 0.15, -0.51, 0.07)], 0.008, 0.005, n=4), 'head', 'under')

    def cap(x, y, h, r):
        a = max(0.0, 1 - (x / max(Wd(y) / 2, 1e-3)) ** 2) ** 0.4
        zt = 0.065 + 0.012 + Hh(y) * a
        q = P.lathe([(r * 0.30, -0.02), (r * 0.26, h * 0.62), (r * 0.9, h * 0.66), (r, h * 0.76), (r * 0.62, h * 0.93), (0.0, h)], n=6)
        q.rot('x', 6 * math.sin(40 * y)).rot('y', 8 * math.sin(30 * y + x * 50)).move(x, y, zt)
        q.smooth = 60.0
        return q
    rows = [(-0.255, 'fore', 0.075), (-0.165, 'fore', 0.10), (-0.07, 'body', 0.125), (0.03, 'body', 0.135), (0.125, 'abdomen1', 0.115), (0.21, 'abdomen1', 0.09),
            (0.285, 'abdomen2', 0.065)]
    for y, bn, h in rows:
        ps = [cap(0.0, y, h, 0.036)]
        if bn in ('body', 'abdomen1') or y == -0.165:
            ps += [cap(sg * 0.105, y + 0.02 * sg, h * 0.6, 0.026) for sg in (1, -1)]
        C.rigid(M.join(ps), bn, 'lamp')
    RC.add_legs(C, legs, 'under', n=4)

    def curl(p, c):
        p.rot('fore', x=46 * c)
        p.rot('head', x=38 * c)
        for b in ('abdomen1', 'abdomen2', 'abdomen3'):
            p.rot(b, x=-42 * c)

    def ripple(p, u, amp=2.0, n=1):
        p.rot('fore', z=amp * osc(u, n, 0.0), x=0.6 * amp * osc(u, 2 * n, 0.1))
        p.rot('head', z=amp * osc(u, n, 0.12))
        p.rot('abdomen1', z=-amp * osc(u, n, 0.2))
        p.rot('abdomen2', z=-amp * osc(u, n, 0.32))
        p.rot('abdomen3', z=-amp * osc(u, n, 0.44))
    A.idle(dur=2.6, breath=0.004, feel=0.0, jaw=0.0, sway=0.003, abd=0.0, look=4.0, extra=lambda p, u: ripple(p, u, 1.2, 1))
    A.gait('Walk', 2.0, 0.2667, 0.4, RC.wave(5, 0.125), lift=0.04, bob=0.003, roll=1.0, yaw=1.5, abd=0.0, extra=lambda p, u: ripple(p, u, 3.0, 1))
    A.gait('Run', round(5.0 / 0.8, 3), 0.2, 0.18, RC.wave(5, 1 / 6.0), lift=0.05, bob=0.004, roll=1.2, yaw=1.5, abd=0.0, extra=lambda p, u: ripple(p, u, 4.0, 1))
    A.snap(dur=0.5, hit=0.25, reach=0.15, gape=0.0, lift=5.0, drop=0.0, extra=lambda p, u, w, s: (p.rot('fore', x=-14 * w + 2 * s), p.rot('head', x=-10 * w + 4 * s)))

    def puff(p, u):                         # curls up into a ball of plates, swells and puffs its spores, uncurls
        hu = 0.55 / 1.1
        c = bump(u, 0.0, hu * 0.7, hu + 0.12, 0.92)
        k = bump(u, hu - 0.06, hu, hu + 0.03, hu + 0.16)
        A.body(p, dz=0.02 * c, pitch=0.0)
        curl(p, c)
        p.scl('body', 1.0 + 0.10 * k)
        A.plant(p, air=sm(c, 0.0, 0.4), tuck=A.bend(30, 40, 0))
    C.clip('Attack2', 1.1, puff, hit=0.55, floor='body')
    C.reach('Attack2', 'body', (0, -0.25, 0.1))
    A.hit(dur=0.35, back=0.04, extra=lambda p, u, k: (p.move('body', 0, 0, 0.03 * k), ripple(p, u, 6.0 * k, 2)))

    def die(p, u):                          # the lamps are pinched out by the game; the body curls up tight and tips over
        c = sm(u, 0.05, 0.5)
        r_ = sm(u, 0.35, 0.8)
        tw = bump(u, 0.55, 0.6, 0.66, 0.8) * osc(u, 8)
        A.body(p, dz=0.05 * math.sin(math.pi * sm(u, 0.0, 0.4)), roll=74 * r_ + 3 * tw)
        curl(p, c * (1 + 0.04 * tw))
        A.plant(p, air=sm(u, 0.02, 0.2), tuck=A.bend(40 * c, 50 * c, 0))
    C.clip('Death', 1.3, die, floor='body', settle=(0.3, 0.6))
    A.spawn_dig(dur=1.0, depth=0.5, shake=6.0, extra=lambda p, u, r: curl(p, 0.5 * (1 - r)))
    return C.finalize(bake=bake, notes="swarm budget: 5 leg pairs of two bones (hidden under the skirt), spine head / fore / body / abdomen1-3 for the curl")


# ================================================================================================ hush_ribwalker
@reg
def hush_ribwalker(bake=True):
    """Rib-walker: a white ribcage with no spine walking on the tips of its own ribs - eight curved prongs - nothing
    inside, no head, the sternum a smooth keel on top. Dead-matte void white"""
    KZ = 0.555
    ys = (-0.24, -0.08, 0.08, 0.24)
    fans = (-14.0, -4.0, 4.0, 14.0)
    legs = []
    for y, a in zip(ys, fans):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        legs.append(RC.leg_def((0.03, y, KZ), (0.03 + 0.35 * ca, y + 0.35 * sa, 0.30), (0.03 + 0.24 * ca, y + 0.24 * sa, 0.0), None, r=(0.024, 0.018, 0.006)))
    sk = RC.arth_skel(body=((0, 0.30, KZ), (0, -0.30, KZ)), head=None, legs=legs)
    C = B.Beast('mon_hush_ribwalker', sk, budget=4000, bone_budget=28, mb=2.5, tex=1024)
    std_meta(C, 'hush', 0.8, 5.6)
    A = RC.Arth(C, legs, pivot=(0, 0.0, KZ))
    C.look('chalk', 'chalk', 'bone_hush', color=(0.76, 0.76, 0.75), shade=(0.52, 0.53, 0.56), crack=30.0)
    keel = P.tubep([(0, -0.36, KZ - 0.03), (0, -0.24, KZ + 0.012), (0, 0.0, KZ + 0.02), (0, 0.24, KZ + 0.012), (0, 0.36, KZ - 0.03)],
                   [0.012, 0.042, 0.048, 0.042, 0.012], [0.01, 0.05, 0.056, 0.05, 0.01], m=16, n=10)
    fin = P.tubep([(0, -0.30, KZ + 0.03), (0, -0.15, KZ + 0.07), (0, 0.05, KZ + 0.082), (0, 0.22, KZ + 0.066), (0, 0.31, KZ + 0.03)],
                  [0.012, 0.03, 0.034, 0.028, 0.01], [0.006, 0.011, 0.012, 0.011, 0.005], m=14, n=6)
    kq = M.join([keel, fin])
    kq.smooth = 180.0
    core(C, kq, 'body', 'chalk')
    for i, (y, a) in enumerate(zip(ys, fans)):
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        for s, sg in (('l', 1.0), ('r', -1.0)):
            nme = 'leg%d_%s' % (i + 1, s)
            pts = [(0.0, 0.0, KZ), (0.17, 0.0, KZ + 0.012), (0.31, 0.0, 0.46), (0.375, 0.0, 0.30), (0.34, 0.0, 0.13), (0.27, 0.0, 0.0)]
            pts = [V(sg * (0.0 + x * ca), y + x * sa, z) for x, _, z in pts]
            rib = P.tubep(pts, [0.03, 0.026, 0.024, 0.022, 0.016, 0.004], [0.022, 0.019, 0.018, 0.016, 0.012, 0.004], m=14, n=6, up=(0, 1, 0))
            t = path_t(rib, pts)
            M.weights_by(rib, t, [(0.0, {'body': 1.0}), (0.12, {nme + '_a': 1.0}), (0.50, {nme + '_a': 1.0}), (0.66, {nme + '_b': 1.0})])
            rib.smooth = 180.0
            C.skin(rib, 'chalk')
    L1 = [A.legs['leg1_l'], A.legs['leg1_r']]
    A.idle(dur=3.6, breath=0.016, feel=0.0, jaw=0.0, sway=0.0, abd=0.0, look=0.0)
    A.gait('Walk', 2.2, 0.4, 0.55, RC.tripod(4), lift=0.09, bob=0.0, roll=0.0, yaw=0.0, jaw=0.0, feel=0.0, abd=0.0)
    A.gait('Run', round(5.6 / 0.8, 3), 0.2, 0.22, SPIDER_PH4, lift=0.12, bob=0.0, roll=0.0, yaw=0.0, pitch=0.0, jaw=0.0, feel=0.0, abd=0.0)

    def stab(p, u):                         # the two front ribs are lifted like a mantis's arms and driven into the target
        hu = 0.36 / 0.75
        w = bump(u, 0.0, hu * 0.6, hu * 0.8, hu)
        s = bump(u, hu * 0.8, hu, hu + 0.1, 0.92)
        A.body(p, dy=0.06 * w - 0.20 * s, dz=0.03 * w - 0.05 * s, pitch=-9 * w + 10 * s)
        A.plant(p)
        up = bump(u, hu * 0.08, hu * 0.6, hu * 0.86, hu)
        fw = sm(u, hu * 0.3, hu) * (1 - sm(u, hu + 0.14, 0.95))
        for lg in L1:
            p.leg(lg, lg.home + V(-lg.side * 0.10 * fw + lg.side * 0.06 * up, -0.38 * fw - 0.06 * up, 0.42 * up))
    C.clip('Attack1', 0.75, stab, hit=0.36)
    C.reach('Attack1', 'leg1_l_b', legs[0]['ankle'])
    A.hop('Attack2', dur=0.8, hit=0.45, fwd=0.45, up=0.30, gape=0.0, pitch=12.0)
    C.reach('Attack2', 'leg1_l_b', legs[0]['ankle'])
    A.hit(dur=0.4, back=0.06)
    A.death_drop(dur=1.4, splay=0.36, tilt=0.0, drop=0.5, pitch=0.0)

    def rise(p, u):                         # opens out of a flat white line on the ground, like an eye
        r = sm(u, 0.1, 0.8)
        A.body(p, dz=-0.5 * (1 - r) + 0.02 * bump(u, 0.75, 0.82, 0.86, 0.98))
        A.plant(p, off={kk: (lg.side * 0.36 * (1 - r), 0.0, 0.0) for kk, lg in A.legs.items()})
    C.clip('Spawn', 1.1, rise, floor='body')
    return C.finalize(bake=bake, notes="swarm budget: the eight ribs are the legs (leg<i>_<s>_a/b, skinned so they bend smoothly); Spawn = the flat cage opens, "
                                       "Death = it closes flat")
