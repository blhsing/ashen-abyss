"""Rig F creatures built by helper H4: the six rigid / floating things after the wraith_gasp proof
(cf_flicker, dusk_moths, bell_hanging, lamp_lure, engine_heliostat, forge_eye).
One function per bestiary type id, registered in BUILD. Shared helpers of this module are at the top."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis, euler
import bparts as P
import rig_f as RF

BUILD = {}
TAU = 2 * math.pi


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


def std_meta(C, family, scale, speed, **kw):
    C.meta.update(family=family, rig='F', scale=scale, game_speed=speed)
    C.meta.update(kw)


def nrm(v):
    return float(np.linalg.norm(v))


def enlarge(sk, K):
    """scale a finished skeleton description by K about the origin (a creature that turned out too small on screen)"""
    for n in sk.names:
        sk.head[n] = sk.head[n] * K
        sk.tail[n] = sk.tail[n] * K


def grow(C, K):
    """every part added from now on is scaled by K about the origin (as helper H1's grow(): build the geometry in
    the brief's numbers, deliver it K times larger; weights must be set before the part is added)"""
    r0, s0 = C.rigid, C.skin

    def rigid(part, bone, look, smooth=None, free=False):
        part.v = part.v * K
        return r0(part, bone, look, smooth=smooth, free=free)

    def skin(part, look, smooth=None):
        part.v = part.v * K
        return s0(part, look, smooth=smooth)
    C.rigid, C.skin = rigid, skin


# ================================================================================================ looks
def _dist(N, c):
    """distance (m, object space) from the point c"""
    d = N.new('ShaderNodeVectorMath', operation='DISTANCE')
    N.set(d.inputs[0], N.coord())
    d.inputs[1].default_value = tuple(float(x) for x in c)
    return d.outputs['Value']


def k_cfglass(N, p):
    """frozen flame (helper H1's `shard` with a second colour): blue glass (the ice look) lit from inside more and
    more towards the tip (object z0 .. z1, never below `floor`), plus warm light 'deep inside' painted where the
    glass is nearest to the points `cores` (a hot pinpoint with a soft halo). The emission mask is a COLOUR."""
    r = L.KINDS['ice'](N, p)
    e = L._c(p, 'emit', (0.32, 0.70, 1.0))
    warm = L._c(p, 'warm', (1.0, 0.42, 0.07))
    g = N.height_z(L._c(p, 'z0', 0.3), L._c(p, 'z1', 0.6))
    blue = N.math('MAXIMUM', N.math('POWER', g, 1.6), L._c(p, 'floor', 0.0))
    blue = N.math('MULTIPLY', blue, N.mapr(N.noise(26, 3, 0.6), 0.25, 0.75, 0.55, 1.0))
    blue = N.math('MULTIPLY', blue, L._c(p, 'gain', 1.0))
    r['color'] = N.mix(N.math('MULTIPLY', blue, 0.5), r['color'], tuple(min(1.0, x * 0.9) for x in e))
    em = N.mul(e, blue)
    spot = None
    for c in p.get('cores', ()):
        d = _dist(N, c)
        s = N.math('ADD', N.math('MULTIPLY', N.math('POWER', N.mapr(d, L._c(p, 'halo', 0.085), 0.0), 2.0), 0.55),
                   N.mapr(d, L._c(p, 'pin', 0.034), L._c(p, 'pin', 0.034) * 0.35), clamp=True)
        spot = s if spot is None else N.math('MAXIMUM', spot, s)
    if spot is not None:
        r['color'] = N.mix(N.math('MULTIPLY', spot, 0.75), r['color'], tuple(min(1.0, x * 0.85) for x in warm))
        em = N.mix(1.0, N.mix(spot, em, (0.0, 0.0, 0.0)), N.mul(warm, spot), 'ADD')
    r['emit'] = ((1.0, 1.0, 1.0), L._c(p, 'strength', 1.4))
    r['emask'] = em
    return r


def k_mothwing(N, p):
    """dusty moth wing: pale ash-grey to warm cream from moth to moth, a darker dusty margin (distance to the edge of
    the wing), fine scale speckle; a soft sheen so the faces catch nearby light"""
    col = L._c(p, 'color', (0.60, 0.585, 0.55))
    warm = L._c(p, 'warm', (0.74, 0.63, 0.46))
    dark = L._c(p, 'dark', (0.17, 0.15, 0.13))
    tone = N.noise(L._c(p, 'scale', 6.0), 1, 0.5)
    c = N.mix(N.mapr(tone, 0.38, 0.66), col, warm)
    c = N.mix(N.mapr(N.noise(2.2, 2, 0.5, vec=N.mapping(N.coord(), loc=(3.1, 7.7, 1.9))), 0.45, 0.7, 0.0, 0.45), c, N.mul(col, 0.62))
    blotch = N.mapr(N.noise(L._c(p, 'blotch', 34.0), 2, 0.55, vec=N.mapping(N.coord(), loc=(5.3, 2.1, 8.8))), 0.56, 0.70)
    c = N.mix(N.math('MULTIPLY', blotch, L._c(p, 'margin', 0.55)), c, dark)      # (every wing vertex is on its border: no hem mask here)
    speck = N.noise(210, 2, 0.6)
    c = N.shade(c, N.mapr(speck, 0.5, 0.72), 0.78)
    rough = N.mapr(speck, 0.3, 0.7, L._c(p, 'rough', 0.34), L._c(p, 'rough', 0.34) + 0.2)
    return L._out(N, p, c, rough, 0.0, [(speck, 0.25), (N.noise(60, 2, 0.5), 0.3)], 0.3)


L.KINDS.update(cfglass=k_cfglass, mothwing=k_mothwing)
L.GRIME.update(cfglass=((0.02, 0.07, 0.16), 0.3), mothwing=((0.05, 0.045, 0.04), 0.2))


# ================================================================================================ cf_flicker
@reg
def cf_flicker(bake=True):
    """Frozen Flicker: a knee-high candle flame of faceted blue glass hovering a hand above the ground - a teardrop
    whose tip forks into three curled tongues (the middle one tallest), a pinpoint of warm orange deep in the belly.
    Bursts into shards (Attack2 and Death): ten loose shards are hidden inside the flame (bones s1..s10, scale 0.001)."""
    sk = B.Sk()
    sk.bone('body', (0, 0, 0.12), (0, 0, 0.40), 'root')
    sk.bone('glass', (0, 0, 0.27), (0, 0, 0.38), 'body')
    tm = sk.chain('tm', [(0, 0, 0.38), (0, 0, 0.50), (0.005, 0.005, 0.615), (0.03, 0.035, 0.735)], 'glass')
    TL = [(0.070, 0, 0.29), (0.118, 0, 0.41), (0.150, 0.012, 0.560)]
    TRr = [(-0.070, 0, 0.29), (-0.114, 0, 0.395), (-0.146, -0.010, 0.515)]
    tl = sk.chain('tl', TL, 'glass')
    tr = sk.chain('tr', TRr, 'glass')
    sk.bone('tail', (0, 0, 0.19), (0, 0, 0.10), 'glass')
    SC = [(0.0, 0.0, 0.185), (0.045, 0.02, 0.24), (-0.045, -0.02, 0.265), (0.0, 0.045, 0.30), (0.0, -0.04, 0.335), (0.02, 0.0, 0.40),
          (0.0, 0.0, 0.47), (0.088, 0.0, 0.335), (-0.086, 0.0, 0.325), (0.004, 0.004, 0.545)]
    SS = [(0.034, 0.016, 0.022), (0.036, 0.015, 0.024), (0.034, 0.017, 0.022), (0.03, 0.016, 0.02), (0.03, 0.014, 0.022), (0.03, 0.014, 0.02),
          (0.026, 0.012, 0.018), (0.024, 0.012, 0.018), (0.024, 0.012, 0.018), (0.02, 0.010, 0.014)]
    SH = [sk.bone('s%d' % (i + 1), c, V(c) + V(0, 0, 0.04), 'body') for i, c in enumerate(SC)]
    K = 1.14                                # built 1.14 x the numbers below (a 0.72 m flame: 1.4 x "knee-high"): at 0.64 m it was a speck in the game view
    enlarge(sk, K)
    C = B.Beast('mon_cf_flicker', sk, budget=4000, bone_budget=28, mb=2.5, tex=1024)       # swarmer
    std_meta(C, 'coldfire', 1.0, 5.2)
    grow(C, K)
    R = RF.Rigid(C, 'body', pivot=(0, 0, 0.27 * K), hover=round(0.27 * K, 2))
    GL = dict(color=(0.16, 0.42, 0.74), deep=(0.015, 0.07, 0.22), frost=(0.70, 0.84, 0.94), frost_amt=0.3, rough=0.1, emit=(0.32, 0.70, 1.0))
    C.look('glass', 'cfglass', 'emis_flame', strength=1.4, z0=0.26 * K, z1=0.74 * K, floor=0.12, halo=0.085 * K, pin=0.034 * K,
           cores=[(0.0, -0.118 * K, 0.262 * K), (0.0, 0.118 * K, 0.262 * K)], **GL)
    C.look('shard', 'cfglass', 'emis_flame', strength=1.4, z0=-2.0, z1=-1.0, gain=0.5, grime_amt=0.0, **GL)
    # ---- the flame: a cut-gem teardrop running up into the tall middle tongue
    main = RF.facet_tube([(0, 0, 0.10), (0, 0, 0.135), (0, 0, 0.195), (0, 0, 0.262), (0, 0, 0.330), (0, 0, 0.395), (0, 0, 0.460),
                          (0.002, 0.002, 0.530), (0.006, 0.008, 0.600), (0.016, 0.02, 0.668), (0.03, 0.035, 0.735)],
                         [0.0, 0.066, 0.108, 0.125, 0.112, 0.088, 0.066, 0.050, 0.035, 0.019, 0.0], n=7, twist=1.3, jitter=0.05, seed=3)
    M.weights_by(main, main.v[:, 2], [(0.11, {'tail': 1.0}), (0.215, {'glass': 1.0}), (0.39, {'glass': 1.0}), (0.47, {'tm1': 1.0}),
                                      (0.575, {'tm2': 1.0}), (0.68, {'tm3': 1.0})])
    side = []
    for sg, pts, ch, tw, sd in ((1, TL, tl, 0.9, 5), (-1, TRr, tr, -1.1, 8)):
        a, b, c = (V(q) for q in pts)
        path = [V(sg * 0.035, 0, 0.205), V(sg * 0.088, 0, 0.262), a + V(sg * 0.042, 0, 0.045), b + V(sg * 0.006, 0, 0.0), (b + c) / 2 + V(-sg * 0.006, 0, 0.0),
                c]
        q = RF.facet_tube(path, [0.0, 0.050, 0.055, 0.043, 0.026, 0.0], [0.0, 0.056, 0.060, 0.046, 0.028, 0.0], n=5, twist=tw, jitter=0.05, seed=sd)
        z = q.v[:, 2]
        M.weights_by(q, z, [(0.27, {'glass': 1.0}), (float(a[2]) + 0.075, {ch[0]: 1.0}), (float(b[2]) + 0.075, {ch[1]: 1.0})])
        side.append(q)
    flame = M.join([main] + side)
    C.skin(flame, 'glass', smooth=0)
    # ---- loose shards for the burst (hidden inside the flame: scale 0.001 in every clip but Attack2 / Death)
    SP = []
    for i, (c, s) in enumerate(zip(SC, SS)):
        q = M.rock(s[0], s[1], s[2], seed=20 + i, n=9, jit=0.3).rot('z', 37.0 * i).move(*c)
        SP.append(C.rigid(q, SH[i], 'shard', smooth=0, free=True))
    rng = np.random.default_rng(11)
    SD = []                                 # burst data per shard: direction, reach, tumble axis, turns, final yaw
    for i, c in enumerate(SC):
        az = TAU * (i * 0.618 + rng.uniform(-0.05, 0.05))
        el = rng.uniform(0.05, 0.6)
        d = V(math.cos(az) * math.cos(el), math.sin(az) * math.cos(el), math.sin(el))
        SD.append(dict(d=d, reach=rng.uniform(0.55, 0.85), ax=unit(V(-d[1], d[0], rng.uniform(-0.3, 0.3))), turns=int(rng.integers(1, 3)),
                       yaw=rng.uniform(0, 360), v=rng.uniform(1.6, 2.6)))
    tip = V(0.03, 0.035, 0.735) * K
    SCK = [C.rg.H(b) for b in SH]           # the shards' pivots as built (scaled)

    def shards_off(p):
        RF.hide(p, SH)

    def lick(p, u, amp=5.0, n=2, back=0.0, spread=0.0, curl=0.0):
        """the tongues lick: back = they trail towards +Y (degrees over the whole tongue), spread = the side tongues
        open, curl = the tips curl further"""
        for i, b in enumerate(tm):
            g = 0.55 + 0.45 * i
            p.rot(b, x=-back * (0.22 + 0.10 * i) + amp * g * osc(u, n, -0.13 * i), y=0.7 * amp * g * osc(u, n, 0.3 - 0.13 * i) + curl * 0.5 * i)
        for sg, ch in ((1, tl), (-1, tr)):
            for i, b in enumerate(ch):
                p.rot(b, x=-back * 0.4 + 0.8 * amp * osc(u, n + 1, 0.2 * sg - 0.15 * i),
                      y=sg * (spread * (0.6 if i == 0 else 0.4) + curl * i) + amp * osc(u, n, 0.1 + 0.25 * sg - 0.15 * i))
        p.rot('tail', x=back * 0.5 + 0.6 * amp * osc(u, n, 0.4), y=0.5 * amp * osc(u, n, 0.15))

    def breathe(p, k):
        """k > 0 = taller and thinner"""
        p.scl('glass', (1 - 0.5 * k, 1 + k, 1 - 0.5 * k))

    def idle(p, u):
        R.body(p, dz=0.02 * osc(u, 1), dx=0.012 * osc(u, 1, 0.25), pitch=2.0 * osc(u, 1, 0.1), roll=2.5 * osc(u, 2, 0.3), yaw=8 * osc(u, 1))
        breathe(p, 0.03 * osc(u, 2))
        lick(p, u, 5.0, 2, curl=2.0 * osc(u, 1, 0.2))
        shards_off(p)
    C.clip('Idle', 3.0, idle)
    idle0 = lambda p: idle(p, 0.0)

    def travel(lean, back, amp, bob):
        def fn(p, u):
            R.body(p, dz=bob * osc(u, 2), pitch=lean + 2.5 * osc(u, 2, 0.1), roll=3 * osc(u, 1), yaw=4 * osc(u, 1, 0.25))
            breathe(p, 0.02 + 0.03 * osc(u, 2, 0.2) + lean * 0.002)
            lick(p, u, amp, 2, back=back)
            shards_off(p)
        return fn
    C.clip('Walk', 1.0, travel(9.0, 16.0, 5.0, 0.014)); C.meta['walk_speed'] = 1.8
    C.clip('Run', 20 / 30.0, travel(25.0, 44.0, 7.0, 0.018)); C.meta['run_speed'] = 5.2

    def lunge(p, u):                        # Attack1: draws back and squats, then darts forward like a thrown blade
        hu = 0.4 / 0.8
        w = bump(u, 0.0, hu * 0.55, hu * 0.78, hu)
        s = bump(u, hu * 0.78, hu, hu + 0.1, 0.92)
        R.body(p, dy=0.15 * w - 0.62 * s, dz=0.03 * w - 0.03 * s, pitch=-16 * w + 36 * s)
        breathe(p, -0.14 * w + 0.22 * s)
        lick(p, u, 3.0, 4, back=-20 * w + 50 * s, spread=12 * w - 8 * s)
        shards_off(p)
    C.clip('Attack1', 0.8, RF.from_pose(C, lunge, idle0, 0.1, 0.12), hit=0.4)
    C.reach('Attack1', 'tm3', tip)

    def burst_pose(p, i, k, sc, drop=0.0):
        """shard i thrown out by k (0 = in the flame .. 1 = at its reach), shown at scale sc"""
        D = SD[i]
        p.setR(SH[i], rz(D['yaw'] * k) @ rot_axis(D['ax'], 360.0 * D['turns'] * k))
        p.move(SH[i], D['d'] * D['reach'] * k + V(0, 0, -drop))
        p.scl(SH[i], sc)

    def shatter(p, u):                      # Attack2: swells, trembles and bursts into a ring of shards that fly back together
        hu = 0.6 / 1.2
        g = sm(u, 0.0, hu - 0.09)
        out = sm(u, hu - 0.075, hu - 0.02)
        back = sm(u, 0.80, 0.90)
        tr_ = g * (1 - out)
        R.body(p, dz=0.05 * g * (1 - sm(u, hu, 0.9)), pitch=4 * tr_ * osc(u, 19), roll=4 * tr_ * osc(u, 23, 0.2), yaw=5 * tr_ * osc(u, 13, 0.4))
        gs = (1 + 0.20 * g * g) * (1 - out) + 0.001 * out * (1 - back) + (1 + 0.14 * bump(u, 0.80, 0.90, 0.90, 1.0)) * back
        p.scl('glass', gs)
        lick(p, u, 3.0 + 5.0 * tr_, 6, spread=18 * g * (1 - back) + 10 * bump(u, 0.8, 0.9, 0.9, 1.0), back=0.0)
        fly = (1 - (1 - sm(u, hu - 0.07, hu + 0.12)) ** 2) * (1 - sm(u, 0.62, 0.86) ** 1.4)
        vis = sm(u, hu - 0.075, hu - 0.03) * (1 - sm(u, 0.82, 0.88))
        for i in range(len(SH)):
            burst_pose(p, i, fly, 0.001 + 1.9 * vis, drop=0.10 * bump(u, hu + 0.1, 0.62, 0.66, 0.84))
    C.clip('Attack2', 1.2, RF.from_pose(C, shatter, idle0, 0.08, 0.08), hit=0.6)
    C.meta['attack_reach'] = max(C.meta.get('attack_reach', 0.0), 0.8)

    def hit(p, u):
        k = bump(u, 0.0, 0.2, 0.3, 1.0)
        R.body(p, dy=0.16 * k, dz=-0.02 * k, pitch=-24 * k, yaw=32 * k, roll=10 * k)
        breathe(p, -0.12 * k)
        lick(p, u, 2.0 + 6 * k, 3, back=-34 * k, spread=14 * k)
        shards_off(p)
    C.clip('Hit', 0.4, RF.from_pose(C, hit, idle0, 0.1, 0.15))

    DUR_D = 1.5
    ub = 0.24                               # the moment it bursts
    SDS = 1.8                               # size of the shards on the ground
    for i, D in enumerate(SD):
        Rf = rz(D['yaw'])
        D['zl'] = -RF.low_point(SP[i], Rf, SCK[i]) * SDS + 0.004
        D['v3'] = V(D['d'][0] * D['v'], D['d'][1] * D['v'], 1.0 + 2.2 * D['d'][2])

    def death(p, u):                        # cracks, flashes and bursts: the shards rain down and lie still
        g = sm(u, 0.0, ub - 0.02)
        out = sm(u, ub - 0.02, ub + 0.015)
        R.body(p, dz=0.04 * g, pitch=5 * g * (1 - out) * osc(u, 26), roll=5 * g * (1 - out) * osc(u, 31, 0.2))
        p.scl('glass', (1 + 0.24 * g * g) * (1 - out) + 0.001 * out)
        lick(p, u, 3.0 + 6.0 * g, 8, spread=20 * g)
        t = (u - ub) * DUR_D
        for i, D in enumerate(SD):
            if t <= 0.0:
                p.scl(SH[i], 0.001 + SDS * out)
                continue
            off, tl_ = RF.flight(t, D['v3'], SCK[i][2] + 0.04, D['zl'])     # the body bone (their parent) hangs 0.04 higher by now
            k = min(t / tl_, 1.0)
            p.setR(SH[i], rz(D['yaw'] * k) @ rot_axis(D['ax'], 360.0 * D['turns'] * k))
            p.move(SH[i], off)
            p.scl(SH[i], 0.001 + SDS * out)
    # the shard heights are exact (zl): no floor / settle needed, the body bone stays where it is
    C.clip('Death', DUR_D, RF.from_pose(C, death, idle0, 0.06, 0.0))

    def spawn(p, u):                        # drifts down as a tight bud of glass and unfurls
        r = sm(u, 0.0, 0.62)
        d = 1 - r
        sw = bump(u, 0.5, 0.68, 0.72, 1.0)
        R.body(p, dz=2.4 * d * d, pitch=10 * d, yaw=200 * d)
        p.scl('glass', 0.3 + 0.7 * sm(u, 0.15, 0.7) + 0.12 * sw)
        lick(p, u, 3.0, 3, spread=-14 * d + 14 * sw, curl=-6 * d)
        shards_off(p)
    C.clip('Spawn', 1.0, RF.from_pose(C, spawn, idle0, 0.0, 0.15))
    return C.finalize(bake=bake, notes="swarm budget; bones glass (scale it to hide the flame), tm1-3 / tl1-2 / tr1-2 tongues, tail, s1-10 loose shards "
                                       "(scale 0.001 except in Attack2 and Death, where the flame bursts); Death leaves ten shards on the ground")


# ================================================================================================ dusk_moths
@reg
def dusk_moths(bake=True):
    """Flame-moths: several hundred pale moths drawn as ONE actor - a churning ball three metres across, denser at
    the core. Eight clusters (bones c1..c8) orbit a centre bone; every moth is a two-sided V (two quads) whose body
    rides on its cluster bone and whose wing tips ride on one of the cluster's two flap bones (f<i>a / f<i>b beat in
    antiphase, four frames per beat). No body: all geometry is skinned, there is nothing for the floating audit."""
    HC = 1.36
    C0 = V(0, 0, HC)
    NCL = 8
    PER = 28                                # moths per flap bone -> 8 x 2 x 28 = 448 moths
    LAY = [(0.20, 25, 0.10, 0.27), (0.24, 205, -0.10, 0.27), (0.60, 80, 0.20, 0.34), (0.64, 195, -0.20, 0.34), (0.62, 315, 0.04, 0.34),
           (0.98, 35, -0.10, 0.34), (1.00, 150, 0.14, 0.34), (0.97, 268, -0.03, 0.34)]
    cen = [C0 + V(r * math.cos(math.radians(a)), r * math.sin(math.radians(a)), dz) for r, a, dz, s in LAY]
    rng = np.random.default_rng(7)
    K = [unit(V(rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 1.0)) for i in range(NCL)]      # orbit axes: near vertical
    sd = [1.0, -1.0, 1.0, 1.0, -1.0, 1.0, -1.0, 1.0]
    tn = [2, 2, 1, 1, 2, 1, 1, 1]           # whole turns per Idle loop
    sk = B.Sk()
    sk.bone('body', C0, C0 + V(0, -0.2, 0), 'root')
    CL, FA, FB = [], [], []
    for i in range(NCL):
        c = cen[i]
        CL.append(sk.bone('c%d' % (i + 1), c, c + V(0, -0.15, 0), 'body'))
        FA.append(sk.bone('f%da' % (i + 1), c + V(0.03, 0, 0.08), c + V(0.03, -0.1, 0.08), CL[i]))
        FB.append(sk.bone('f%db' % (i + 1), c + V(-0.03, 0, 0.08), c + V(-0.03, -0.1, 0.08), CL[i]))
    C = B.Beast('mon_dusk_moths', sk, budget=4000, bone_budget=28, mb=2.5, tex=1024)        # swarm
    std_meta(C, 'dusk', 1.0, 4.6, swarm_radius=1.5)
    R = RF.Rigid(C, 'body', hover=HC)
    C.look('moth', 'mothwing', 'skin_moth')
    rig = M.RIG
    vs, fs, ws = [], [], []
    top = [-9.0] * NCL
    bot = [9.0] * NCL
    EX, EZ = 1.5, 1.14                       # the ball: 3 m across, a little flattened so it clears the ground
    for i in range(NCL):
        sg_ = LAY[i][3]
        for h, fb in enumerate((FA[i], FB[i])):
            for k in range(PER):
                for tries in range(30):
                    o = rng.normal(0.0, sg_, 3) * (1.0, 1.0, 0.82)
                    m = cen[i] + o
                    rel = m - C0
                    if (rel[0] / EX) ** 2 + (rel[1] / EX) ** 2 + (rel[2] / EZ) ** 2 <= 1.0:
                        break
                else:
                    rel = rel / math.sqrt((rel[0] / EX) ** 2 + (rel[1] / EX) ** 2 + (rel[2] / EZ) ** 2)
                    m = C0 + rel
                up = unit(V(0, 0, 1.0) + rng.normal(0.0, 0.22, 3))
                tg = np.cross(K[i], rel) * sd[i]
                if nrm(tg) < 0.05:
                    tg = V(rng.normal(), rng.normal(), 0.0)
                hd = unit(tg) + rng.normal(0.0, 0.45, 3)
                hd = unit(hd - up * float(hd @ up))
                rt = np.cross(hd, up)
                Wm = rng.uniform(0.105, 0.16)
                ph = math.radians(24.0)
                n_ = m + hd * 0.17 * Wm
                t_ = m - hd * 0.24 * Wm
                o0 = len(vs)
                vs += [n_, t_]
                wb = np.zeros(rig.nb, dtype=np.float32); wb[rig.idx[CL[i]]] = 1.0
                wf = np.zeros(rig.nb, dtype=np.float32); wf[rig.idx[fb]] = 1.0
                wr = np.zeros(rig.nb, dtype=np.float32); wr[rig.idx[fb]] = 0.6; wr[rig.idx[CL[i]]] = 0.4
                ws += [wb, wb]
                for s_ in (1.0, -1.0):
                    f_ = m + rt * (s_ * 0.56 * Wm * math.cos(ph)) + up * (0.56 * Wm * math.sin(ph)) + hd * 0.10 * Wm      # pointed fore-wing tip
                    r_ = m + rt * (s_ * 0.17 * Wm * math.cos(ph)) + up * (0.17 * Wm * math.sin(ph)) - hd * 0.37 * Wm      # hind corner, close to the body
                    vs += [f_, r_]
                    ws += [wf, wr]
                fs.append((o0, o0 + 2, o0 + 3, o0 + 1))
                fs.append((o0, o0 + 1, o0 + 5, o0 + 4))
                zz = [q[2] for q in vs[o0:o0 + 6]]
                top[i] = max(top[i], max(zz) + 0.06); bot[i] = min(bot[i], min(zz) - 0.06)
    moths = M.Part(np.array(vs), fs, np.array(ws, dtype=np.float32))
    moths.smooth = 0.0
    moths = B.two_sided(moths, gap=0.003)
    C.skin(moths, 'moth', smooth=0)
    # mean heading of every cluster (to turn the moths into the wind in the comet)
    AL = []
    for i in range(NCL):
        hb = np.cross(K[i], cen[i] - C0) * sd[i]
        AL.append(math.degrees(math.atan2(-hb[0], -hb[1])) if nrm(hb[:2]) > 1e-3 else 0.0)     # yaw that turns hb onto -Y
    rank = [0, 1, 2, 4, 3, 6, 5, 7]         # place in the comet (0 = head)

    def flap(p, amp=0.055, calm=1.0):
        f = int(round(p.time * 30.0))
        for i in range(NCL):
            z = (1.0, 0.0, -1.0, 0.0)[(f + i) % 4] * amp * calm
            p.move(FA[i], 0, 0, z)
            p.move(FB[i], 0, 0, -z)

    def churn(p, u, i, turns=None, squeeze=1.0, off=(0, 0, 0), scale=1.0, wob=1.0, ang=None, bdz=0.0):
        """cluster i on its orbit about the centre: `turns` whole turns over u = 0..1 (or the angle `ang`);
        bdz = how far the centre bone is shifted in z (the cluster is kept clear of the ground)"""
        a = 360.0 * sd[i] * (tn[i] if turns is None else turns) * u if ang is None else ang
        Rm = rot_axis(K[i], a)
        rel = cen[i] - C0
        pos = C0 + (Rm @ rel) * squeeze * (1.0 + 0.10 * wob * osc(u, 2 + i % 2, i * 0.13)) + V(off)
        pos[2] = max(pos[2], 0.10 - bdz + 1.15 * scale * (cen[i][2] - bot[i]))
        R.turn(p, Rm, pivot=cen[i], bone=CL[i], d=pos - cen[i])
        if scale != 1.0:
            p.scl(CL[i], scale)

    def idle(p, u):
        R.body(p, dz=0.06 * osc(u, 1), dx=0.05 * osc(u, 1, 0.25), dy=0.04 * osc(u, 2))
        for i in range(NCL):
            churn(p, u, i)
        flap(p)
    C.clip('Idle', 96 / 30.0, idle)
    idle0 = lambda p: idle(p, 0.0)

    def comet(Ln, turns, lean):
        """the ball streams out behind (+Y): a dense head, a tapering tail that corkscrews"""
        def fn(p, u):
            R.body(p, dz=0.05 * osc(u, 1), pitch=0.0)
            for i in range(NCL):
                j = rank[i] / (NCL - 1.0)
                a = 360.0 * sd[i] * turns * u + 75.0 * rank[i]
                rho = (0.30 if rank[i] < 2 else 0.52) * (1.0 - 0.72 * j) * (1.0 + 0.12 * osc(u, 2, i * 0.17))
                y = Ln * j ** 1.15 - 0.15
                th = math.radians(a)
                pos = C0 + V(rho * math.cos(th), y, 0.85 * rho * math.sin(th) - lean * j)
                Rm = ry(a) @ rz(AL[i])
                R.turn(p, Rm, pivot=cen[i], bone=CL[i], d=pos - cen[i])
                p.scl(CL[i], (1.0 - 0.45 * j) * (0.92 if rank[i] < 2 else 1.0))
            flap(p)
        return fn
    C.clip('Walk', 48 / 30.0, comet(1.7, 1, 0.10)); C.meta['walk_speed'] = 1.6
    C.clip('Run', 24 / 30.0, comet(3.0, 1, 0.22)); C.meta['run_speed'] = 4.6

    def engulf(p, u):                       # Attack1: the cloud pulls tight, then bursts open past its size over the target
        hu = 0.6 / 1.2
        w = bump(u, 0.0, hu * 0.7, hu * 0.85, hu)
        s = bump(u, hu * 0.85, hu, hu + 0.12, 0.95)
        bdz = 0.05 * w - 0.06 * s
        R.body(p, dy=0.15 * w - 0.55 * s, dz=bdz)
        S = sm(u, 0.0, 1.0)
        for i in range(NCL):
            churn(p, S, i, turns=1, squeeze=1.0 - 0.52 * w + 0.42 * s, scale=1.0 - 0.28 * w + 0.22 * s, wob=1.0 - w, bdz=bdz)
        flap(p)
    C.clip('Attack1', 1.2, RF.from_pose(C, engulf, idle0, 0.08, 0.08), hit=0.6)

    def scatter(p, u):                      # Attack2: gathers, explodes outward in all directions, then swirls back together
        hu = 0.7 / 1.6
        w = bump(u, 0.0, hu * 0.6, hu * 0.85, hu)
        s = bump(u, hu * 0.85, hu + 0.03, 0.62, 0.95)
        bdz = 0.10 * w + 0.15 * s
        R.body(p, dz=bdz)
        S = sm(u, 0.0, 1.0) ** 0.8
        for i in range(NCL):
            lift = 0.25 * s * math.sin(i * 2.4)
            churn(p, S, i, turns=2, squeeze=1.0 - 0.35 * w + 1.5 * s, scale=1.0 - 0.15 * w + 0.5 * s, off=(0, 0, lift), wob=1.0 - w, bdz=bdz)
        flap(p)
    C.clip('Attack2', 1.6, RF.from_pose(C, scatter, idle0, 0.06, 0.06), hit=0.7)
    C.meta['attack_reach'] = 1.5            # the cloud overlaps its target: both attacks are the cloud itself

    def hit(p, u):
        k = bump(u, 0.0, 0.2, 0.3, 1.0)
        R.body(p, dy=0.3 * k, dz=0.05 * k)
        for i in range(NCL):
            churn(p, 0.0, i, squeeze=1.0 + 0.28 * k, scale=1.0 + 0.1 * k, ang=sd[i] * 22.0 * k)
        flap(p)
    C.clip('Hit', 0.4, RF.from_pose(C, hit, idle0, 0.1, 0.12))

    DUR_D = 2.4
    ZG = 0.012
    GF = 4.2                                # they flutter down: slower than a stone
    TD = [0.1 + 0.12 * ((i * 5) % NCL) for i in range(NCL)]     # the clusters let go one after another

    def death(p, u):                        # the moths rain down, cluster after cluster, and lie on the ground
        t = u * DUR_D
        R.body(p, dx=0.05 * (1 - sm(u, 0.0, 0.2)))
        for i in range(NCL):
            e = sm(t, TD[i] - 0.1, TD[i] + 0.9)
            rel = cen[i] - C0
            drop = 0.5 * GF * max(0.0, t - TD[i]) ** 2
            sz = min(1.0, max(0.011, (top[i] - drop - ZG) / (top[i] - bot[i])))
            cz = (cen[i][2] - drop) if (bot[i] - drop) > ZG else ZG + sz * (cen[i][2] - bot[i])
            Rm = rz(sd[i] * 38.0 * e)
            pos = C0 + (Rm @ V(rel[0], rel[1], 0.0)) * (1.0 + 0.3 * e)
            pos[2] = cz
            R.turn(p, Rm, pivot=cen[i], bone=CL[i], d=pos - cen[i])
            RF.scale_axes(p, CL[i], 1.0 + 0.12 * e, 1.0 + 0.12 * e, sz)
        f = int(round(p.time * 30.0))
        for i in range(NCL):                # the beat dies as each cluster lands
            drop = 0.5 * GF * max(0.0, t - TD[i]) ** 2
            calm = min(1.0, max(0.0, (top[i] - drop - ZG) / (top[i] - bot[i]))) ** 0.5
            z = (1.0, 0.0, -1.0, 0.0)[(f + i) % 4] * 0.055 * calm
            p.move(FA[i], 0, 0, z)
            p.move(FB[i], 0, 0, -z)
    C.clip('Death', DUR_D, RF.from_pose(C, death, idle0, 0.05, 0.0))

    def spawn(p, u):                        # gathers out of a wide scatter into the ball
        r = 1.0 - (1.0 - sm(u, 0.0, 0.8)) ** 2
        d = 1.0 - r
        pull = bump(u, 0.6, 0.8, 0.82, 1.0)
        R.body(p, dz=0.45 * d)
        for i in range(NCL):
            churn(p, r, i, turns=1, squeeze=1.0 + 1.9 * d - 0.14 * pull, scale=1.0 + 0.55 * d - 0.06 * pull, off=(0, 0, 0.22 * d * math.sin(i * 2.1)), wob=r,
                  bdz=0.45 * d)
        flap(p)
    C.clip('Spawn', 1.6, RF.from_pose(C, spawn, idle0, 0.0, 0.1))
    return C.finalize(bake=bake, notes="swarm budget; 448 moths, all geometry skinned (bodies on the cluster bones c1-8, wing tips on the flap bones "
                                       "f<i>a / f<i>b): no rigid parts, the floating-part audit does not apply; Walk / Run = comet trailing towards +Y "
                                       "(head at the origin); Death flattens the clusters onto the ground with a non-uniform bone scale")


# ================================================================================================ bell_hanging
def beam_x(x0, x1, yh, z0, z1, n=7, arch=0.0, cham=0.02, jag0=0.0, seed=0):
    """timber beam along X (2 yh wide, z0 .. z1 high, chamfered corners; the top rises by `arch` in the middle).
    jag0 = the x0 end is broken off: its ring is pushed in and out by up to jag0"""
    rng = np.random.default_rng(seed)
    rings = []
    for k, x in enumerate(np.linspace(x0, x1, n)):
        t = (x - x0) / (x1 - x0)
        zt = z1 + arch * math.sin(math.pi * t)
        c = cham
        ring = [(x, -yh + c, z0), (x, yh - c, z0), (x, yh, z0 + c), (x, yh, zt - c), (x, yh - c, zt), (x, -yh + c, zt), (x, -yh, zt - c), (x, -yh, z0 + c)]
        if k == 0 and jag0:
            ring = [(xx + rng.uniform(-jag0, jag0 * 0.3), y, z) for (xx, y, z) in ring]
        rings.append(ring)
    q = M.loft(np.array(rings), cap0=True, cap1=True).recalc()
    q.smooth = 30.0
    return q


def band_x(x, yh, z0, z1, w=0.05, th=0.012):
    """iron band round a beam that runs along X (four flat bars)"""
    zc, zh = (z0 + z1) / 2, (z1 - z0) / 2
    q = M.join([M.box(w, 2 * yh + 2 * th, th).move(x, 0, z1 + th / 2), M.box(w, 2 * yh + 2 * th, th).move(x, 0, z0 - th / 2),
                M.box(w, th, 2 * zh).move(x, yh + th / 2, zc), M.box(w, th, 2 * zh).move(x, -yh - th / 2, zc)])
    q.smooth = 30.0
    return q


@reg
def bell_hanging(bake=True):
    """Hung Bell: a great bronze bell floating mouth-down, its lip 0.83 m (1 m in the game at scale 1.2) above the
    ground, under the broken stub of its timber headstock; a pair of bound legs hangs out of the mouth where the
    clapper was; two chains trail from the headstock. Bones: body (the bell), leg1-3 (the legs as one clapper),
    cl1-5 / cr1-3 (chains)."""
    ZL, Rb, Hb = 0.83, 0.46, 0.92
    ZC = ZL + Hb                            # top of the crown
    ZB0, ZB1 = ZC + 0.085, ZC + 0.285       # the headstock beam
    SW = V(0, 0, (ZB0 + ZB1) / 2)           # the axis it swings about
    XL, XR = 0.62, -0.50
    sk = B.Sk()
    sk.bone('body', (0, 0, ZL), (0, 0, ZC), 'root')
    sk.bone('trunk', (0, 0, 1.38), (0, 0, 1.00), 'body')           # the ringer's body up inside the bell; the legs hang from its end
    sk.bone('rag', (0, 0, ZL + 0.035), (0, 0, ZL - 0.16), 'body')  # the shroud collar (squashed up into the mouth when the bell sits on the ground)
    lg = sk.chain('leg', [(0, 0, 1.00), (0, 0, 0.62), (0, 0, 0.26), (0, -0.035, 0.095)], 'trunk')
    cl = sk.chain('cl', [(XL, 0, ZB0 - 0.01 - 0.27 * i - (0.10 if i == 5 else 0.0)) for i in range(6)], 'body')     # the last link bone also carries the hook
    cr = sk.chain('cr', [(XR, 0, ZB0 - 0.01 - 0.28 * i) for i in range(4)], 'body')
    C = B.Beast('mon_bell_hanging', sk)
    std_meta(C, 'bell', 1.2, 1.6)
    R = RF.Rigid(C, 'body', pivot=SW, hover=round(ZL + Hb / 2, 2))
    C.look('bronze', 'metal', 'metal_bell', color=(0.22, 0.15, 0.07), rust=(0.07, 0.16, 0.13), rust2=(0.04, 0.08, 0.06), amount=0.5,
           edge_color=(0.62, 0.48, 0.25), rough=0.42, hammer=14.0)
    C.look('iron', 'metal', 'metal_bell', **L.PRESET['rusty_iron'])
    C.look('void', 'flat', 'metal_bell', color=(0.006, 0.006, 0.007), rough=0.95)
    C.look('rim', 'emis', 'emis_rim', emit=(0.40, 0.47, 0.58), strength=1.0, mottle=0.7, base_mul=0.8, scale=30.0)     # faint: the toll ring is a game effect
    C.look('timber', 'wood', 'leather_timber', color=(0.11, 0.075, 0.045), axis=(1, 0, 0))
    C.look('rope', 'cloth', 'leather_timber', color=(0.30, 0.235, 0.14), hem_lo=-2, hem_hi=-1, scale=30.0, bump=0.8)
    C.look('legs', 'skin', 'skin_legs', color=(0.33, 0.34, 0.33), blotch=(0.17, 0.15, 0.19), rough=0.55)
    C.look('shroud', 'cloth', 'cloth_shroud', color=(0.36, 0.34, 0.30), dirt=(0.12, 0.10, 0.08), hem_lo=0.7, hem_hi=0.95, stain_amt=0.7)
    # ---- the bell
    prof = [(0.972 * Rb, 0.0), (1.0 * Rb, 0.016), (0.995 * Rb, 0.045), (0.93 * Rb, 0.10 * Hb), (0.81 * Rb, 0.25 * Hb), (0.705 * Rb, 0.44 * Hb),
            (0.635 * Rb, 0.63 * Hb), (0.595 * Rb, 0.79 * Hb), (0.53 * Rb, 0.89 * Hb), (0.39 * Rb, 0.955 * Hb), (0.17 * Rb, 0.992 * Hb), (0.0, Hb)]
    bell = M.revolve(prof, n=32, cap=False).move(0, 0, ZL)
    bell.dent((0.30, -0.26, ZL + 0.42), 0.10, -0.018)
    bell.dent((-0.33, 0.20, ZL + 0.2), 0.08, -0.012)
    RF.core(C, bell, 'body', 'bronze', smooth=60)
    inner = M.revolve([(0.972 * Rb, 0.0), (0.90 * Rb, 0.035), (0.76 * Rb, 0.28 * Hb), (0.56 * Rb, 0.62 * Hb), (0.30 * Rb, 0.80 * Hb), (0.0, 0.84 * Hb)],
                      n=24, cap=False).move(0, 0, ZL)
    inner.recalc(); inner.flip(); inner.two = True                  # faces look into the mouth
    C.rigid(inner, 'body', 'void', smooth=60)
    wires = [P.ring((0, 0, ZL + 0.245 * Hb), 0.822 * Rb, 0.009, n=32, m=5), P.ring((0, 0, ZL + 0.275 * Hb), 0.80 * Rb, 0.009, n=32, m=5),
             P.ring((0, 0, ZL + 0.885 * Hb), 0.545 * Rb, 0.010, n=28, m=5), P.ring((0, 0, ZL + 0.07), 0.965 * Rb, 0.008, n=32, m=5)]
    C.rigid(M.join(wires), 'body', 'bronze', smooth=60)
    C.rigid(P.ring((0, 0, ZL + 0.014), 1.004 * Rb, 0.0095, n=36, m=6), 'body', 'rim', smooth=60)
    crown = [M.revolve([(0.17 * Rb, 0.0), (0.21 * Rb, 0.02), (0.21 * Rb, 0.075), (0.13 * Rb, 0.10)], n=12).move(0, 0, ZC - 0.012),
             P.ring((0, 0, ZC + 0.055), 0.075, 0.024, axis=(1, 0, 0), n=14, m=6), P.ring((0, 0, ZC + 0.055), 0.075, 0.024, axis=(0, 1, 0), n=14, m=6)]
    C.rigid(M.join(crown), 'body', 'bronze', smooth=50)
    # ---- the headstock: a timber beam broken off on its right, with its iron straps and one gudgeon pin left
    YH = 0.10
    beam = beam_x(-0.58, 0.70, YH, ZB0, ZB1, n=8, arch=0.035, jag0=0.07, seed=4)
    C.rigid(beam, 'body', 'timber')
    rng = np.random.default_rng(9)
    spl = []
    for k in range(7):
        b0 = V(-0.56, rng.uniform(-YH * 0.8, YH * 0.8), rng.uniform(ZB0 + 0.03, ZB1 - 0.03))
        spl.append(P.cone(b0 + V(0.06, 0, 0), V(-1.0, rng.uniform(-0.25, 0.25), rng.uniform(-0.2, 0.3)), rng.uniform(0.12, 0.26), rng.uniform(0.014, 0.024), n=4))
    C.rigid(M.join(spl), 'body', 'timber', smooth=20)
    iron = [band_x(0.17, YH, ZB0, ZB1 + 0.02), band_x(-0.17, YH, ZB0, ZB1 + 0.02), band_x(0.64, YH, ZB0, ZB1 + 0.004, w=0.06),
            M.tube(np.array([(0.69, 0, SW[2]), (0.86, 0, SW[2])]), 0.034, n=10), M.tube(np.array([(0.84, 0, SW[2]), (0.875, 0, SW[2])]), 0.05, n=10)]
    for sx in (0.17, -0.17):                # the straps run down to the crown
        for sy in (1, -1):
            iron.append(M.box(0.05, 0.012, ZB0 - ZC + 0.11).move(sx, sy * (YH + 0.006), (ZB0 + ZC - 0.11) / 2))
            iron.append(P.blob((sx, sy * (YH + 0.016), ZB0 + 0.06), 0.014, 0.010, 0.014, n=6, m=4))
            iron.append(P.blob((sx, sy * (YH + 0.016), ZB1 - 0.05), 0.014, 0.010, 0.014, n=6, m=4))
    for x_ in (XL, XR):                     # staples the chains hang from
        iron.append(P.ring((x_, 0, ZB0 - 0.005), 0.03, 0.009, axis=(0, 1, 0), n=10, m=5))
    q = M.join(iron); q.smooth = 40.0
    C.rigid(q, 'body', 'iron')
    RF.chain_parts(C, cl[:-1], 'iron', link=0.078, r=0.0115)
    RF.chain_parts(C, cr, 'iron', link=0.078, r=0.0115)
    end_l = cl[-1]
    hk = C.rg.T(end_l) + V(0, 0, 0.10)      # the hook hangs inside the last bone's length (so that it lies down with the chain)
    C.rigid(M.chain(np.array([C.rg.H(end_l), hk]), link=0.078, r=0.0115), end_l, 'iron', smooth=60)
    C.rigid(P.tubep([hk + V(0, 0, 0.02), hk + V(0, 0, -0.045), hk + V(0, 0.03, -0.085), hk + V(0, 0.065, -0.06)], [0.011, 0.011, 0.009, 0.004], m=9, n=6),
            end_l, 'iron', smooth=60)
    # ---- the legs: hung inside by the hips, bound together at the knees and ankles
    legs = []
    for sg in (1, -1):                      # they hang 0.7 m out of the mouth (longer and heavier than life: they are the creature's mark)
        th = P.tubep([(sg * 0.095, 0.0, 1.03), (sg * 0.090, -0.014, 0.86), (sg * 0.083, -0.020, 0.70), (sg * 0.080, -0.018, 0.62), (sg * 0.078, 0.004, 0.50),
                      (sg * 0.074, 0.006, 0.38), (sg * 0.070, 0.0, 0.285), (sg * 0.069, -0.004, 0.25)], [0.090, 0.084, 0.066, 0.062, 0.068, 0.054, 0.042, 0.040],
                     [0.086, 0.078, 0.062, 0.060, 0.062, 0.050, 0.038, 0.036], m=17, n=10, up=(0, -1, 0))
        ft = P.tubep([(sg * 0.069, 0.004, 0.29), (sg * 0.068, -0.008, 0.225), (sg * 0.066, -0.030, 0.155), (sg * 0.064, -0.052, 0.095)],
                     [0.042, 0.040, 0.028, 0.016], [0.040, 0.047, 0.052, 0.038], m=8, n=9, up=(0, -1, 0))
        legs += [th, ft]
    legs = M.join(legs)
    M.weights_by(legs, legs.v[:, 2], [(0.22, {'leg3': 1.0}), (0.29, {'leg2': 1.0}), (0.585, {'leg2': 1.0}), (0.655, {'leg1': 1.0})])
    C.skin(legs, 'legs', smooth=180)
    trunk = P.blob((0, 0.0, 1.16), 0.175, 0.125, 0.21, n=12, m=8)      # the rest of the ringer: a bundle in its shroud, up inside the bell
    trunk.rough(0.012, 9.0, seed=2)
    trunk.W = M.rigid_W(len(trunk.v), 'trunk')
    C.skin(trunk, 'shroud', smooth=180)
    rp = []
    for z0, rx_, ry_ in ((0.595, 0.160, 0.082), (0.570, 0.161, 0.083), (0.545, 0.160, 0.082), (0.345, 0.128, 0.060), (0.322, 0.125, 0.058), (0.30, 0.122, 0.056)):
        rp.append(M.torus(rx_, 0.013, n=18, m=5, ry=ry_).rot('x', 4.0 * math.sin(z0 * 60)).move(0, 0.0, z0))
    rp.append(P.tubep([(0.0, -0.085, 0.57), (0.02, -0.11, 0.51), (0.03, -0.105, 0.42)], [0.012, 0.011, 0.013], m=6, n=6))     # the loose end of the knot
    q = M.join(rp); q.smooth = 60.0
    C.rigid(q, 'leg2', 'rope')
    # ---- wet shroud stuffed into the mouth: a torn collar of rag round the thighs
    top, out = M.arc_pts((0, 0, ZL + 0.035), 0.385, 0.385, -40.0, 220.0, 22)
    rag = M.hang(top, 0.19, rows=4, out=out, flare=-0.035, rag=0.6, seed=6, wave=0.012, tatter=0.35, taper=0.05)
    rag.W = M.rigid_W(len(rag.v), 'rag')
    rag = B.two_sided(rag, gap=0.004)
    C.skin(rag, 'shroud', smooth=180)
    lip_f = V(0, -Rb, ZL)

    LG = RF.HangChain(C, lg, 'trunk', ground=0.10)
    CHL = RF.HangChain(C, cl, 'body', ground=0.055)
    CHR = RF.HangChain(C, cr, 'body', ground=0.055)

    def hang(p, u, legs_y=0.0, legs_x=0.0, la=0.03, ln=1, ch_y=0.0, ca=0.03, cn=2, fold=1.0, off=(0, 0, 0), trail=(0, 1), rag=1.0):
        """legs and chains by gravity: legs_y / ch_y = lean (tangent, + = towards +Y), la / ca = their pendulum wave;
        off = the body slides inside the bell (rest space of the bell); rag < 1 = the shroud collar is pushed up"""
        p.move('trunk', off)
        if rag != 1.0:
            RF.scale_axes(p, 'rag', 1.0, 1.0, max(rag, 0.03))
        LG.pose(p, sx=legs_x, sy=legs_y, u=u, amp=la, n=ln, lag=0.05, ph=-0.18, amp_x=la * 0.4, fold=fold, trail=trail)
        CHL.pose(p, sy=ch_y, u=u, amp=ca, n=cn, lag=0.11, amp_x=ca * 0.6, trail=(1.0, 0.45))
        CHR.pose(p, sy=ch_y, u=u, amp=ca, n=cn, lag=0.13, ph=0.3, amp_x=ca * 0.6, trail=(-1.0, 0.45))

    def idle(p, u):
        R.body(p, dz=0.035 * osc(u, 1, 0.25), pitch=2.5 * osc(u, 1), yaw=4 * osc(u, 1, 0.1), roll=1.2 * osc(u, 2))
        hang(p, u, la=0.20, ln=2, ca=0.035, cn=2)
    C.clip('Idle', 4.0, idle)
    idle0 = lambda p: idle(p, 0.0)

    def travel(lean, trail_, sw, bob):
        def fn(p, u):
            R.body(p, dz=bob * osc(u, 2), pitch=lean + sw * osc(u, 1), yaw=3 * osc(u, 1, 0.25), roll=1.5 * osc(u, 1, 0.1))
            hang(p, u, legs_y=trail_, la=0.14, ln=1, ch_y=trail_ * 0.9, ca=0.05, cn=2)
        return fn
    C.clip('Walk', 2.0, travel(4.0, 0.10, 3.0, 0.03)); C.meta['walk_speed'] = 0.5
    C.clip('Run', 1.4, travel(9.0, 0.24, 4.0, 0.035)); C.meta['run_speed'] = round(1.6 / 1.2, 3)

    def toll(p, u):                         # Attack1: heaves its mouth back, swings it at the target - the legs strike the lip
        hu = 0.75 / 1.5
        w = sm(u, 0.0, 0.30)
        f = sm(u, 0.34, hu)
        tau = max(0.0, (u - hu) / (1.0 - hu))
        ring = math.exp(-3.2 * tau)
        th = 16.0 * w * (1 - f) - 28.0 * f * (ring * math.cos(TAU * 1.25 * tau) if u > hu else 1.0)
        ly = -0.30 * w * (1 - sm(u, 0.3, 0.42)) + 0.78 * sm(u, 0.38, hu) * ((ring * math.cos(TAU * 1.5 * tau)) if u > hu else 1.0)
        jolt = bump(u, hu, hu + 0.015, hu + 0.03, hu + 0.12)
        R.body(p, dz=0.06 * w * (1 - f) - 0.03 * jolt, dy=-0.10 * f * ring, pitch=th, roll=1.6 * ring * osc(tau, 9) * (u > hu), yaw=0.0)
        hang(p, u, legs_y=ly, la=0.02, ln=3, ch_y=-0.011 * th, ca=0.04 + 0.05 * ring * (u > hu), cn=4)
    C.clip('Attack1', 1.5, RF.from_pose(C, toll, idle0, 0.08, 0.12), hit=0.75)
    C.reach('Attack1', 'body', lip_f)

    DROP = ZL - 0.012

    def slam(p, u):                         # Attack2: rises, hangs, and drops mouth-down on the ground; then lifts off again
        hu = 0.7 / 1.6
        up_ = sm(u, 0.0, 0.26)
        fall = sm(u, 0.33, hu) ** 2.2
        rise = sm(u, 0.62, 0.95)
        dz = 0.30 * up_ * (1 - fall) - DROP * fall * (1 - rise)
        sh = bump(u, hu, hu + 0.01, hu + 0.03, 0.6)
        R.body(p, dz=dz + 0.02 * sh * abs(osc(u, 14)), pitch=-5 * up_ * (1 - fall) + 1.5 * sh * osc(u, 17), roll=1.5 * sh * osc(u, 13, 0.2),
               yaw=10 * up_ * (1 - rise))
        hang(p, u, legs_y=0.05 * up_ * (1 - fall), la=0.06 * (1 - fall * (1 - rise)), ln=2, ca=0.03 + 0.06 * rise * (1 - sm(u, 0.9, 1.0)), cn=3, fold=1.0,
             off=(0, 0, 0.15 * fall * (1 - rise)), rag=1.0 - 0.96 * sm(u, 0.36, hu) * (1 - rise))
    C.clip('Attack2', 1.6, RF.from_pose(C, slam, idle0, 0.06, 0.08), hit=0.7)
    C.reach('Attack2', 'body', lip_f)

    def hit(p, u):
        k = bump(u, 0.0, 0.18, 0.3, 1.0)
        R.body(p, dy=0.12 * k, dz=-0.03 * k, pitch=-9 * k, yaw=12 * k, roll=5 * k)
        hang(p, u, legs_y=-0.22 * k, legs_x=0.08 * k, la=0.03, ln=2, ch_y=-0.16 * k, ca=0.05, cn=2)
    C.clip('Hit', 0.5, RF.from_pose(C, hit, idle0, 0.1, 0.15))

    land = 0.26

    def death(p, u):                        # falls, lands on its lip with one last clear note, tips over and lies on its side
        f = 1 - RF.fall_curve(u, land)
        bn = bump(u, land, land + 0.03, land + 0.05, land + 0.12)
        tip_ = sm(u, 0.38, 0.72) ** 1.7
        rock = bump(u, 0.72, 0.76, 0.80, 0.97)
        ang = 103.0 * tip_ + 3.0 * rock * math.cos(TAU * 2.0 * (u - 0.72) / 0.25)
        Rm = rx(ang) @ rz(14.0 * tip_)
        R.turn(p, Rm, pivot=lip_f + V(0, 0, 0.0), d=(0, 0, -DROP * f + 0.05 * bn))
        out_ = sm(u, 0.5, 0.9)
        hang(p, u, la=0.10 * (1 - f), ln=2, ca=0.04 * (1 - f), cn=3, fold=1.0 - sm(u, 0.40, 0.64),
             off=(0, -0.29 * out_, 0.15 * f * (1 - sm(u, 0.4, 0.6)) - 0.22 * out_), trail=(0.25, 1.0),
             rag=1.0 - 0.96 * sm(u, land * 0.55, land) * (1 - sm(u, 0.45, 0.7)) - 0.45 * sm(u, 0.6, 0.9))
    C.clip('Death', 2.3, RF.from_pose(C, death, idle0, 0.05, 0.0), floor='body', settle=(0.74, 0.84))

    def spawn(p, u):                        # sinks in from above, swinging, and steadies
        r = sm(u, 0.0, 0.62)
        d = 1 - r
        sw = bump(u, 0.3, 0.6, 0.66, 1.0)
        th = 14 * d * math.cos(TAU * 1.5 * u) + 6 * sw * osc(u, 3)
        R.body(p, dz=3.0 * d * d - 0.06 * bump(u, 0.58, 0.66, 0.68, 0.85), pitch=th, yaw=70 * d)
        hang(p, u, legs_y=-0.014 * th, la=0.10, ln=3, ch_y=-0.012 * th, ca=0.06, cn=3)
    C.clip('Spawn', 1.6, RF.from_pose(C, spawn, idle0, 0.0, 0.12))
    return C.finalize(bake=bake, notes="floater (lip 0.83 m above the ground = 1 m at scale 1.2); the bell swings about its headstock; legs and chains hang "
                                       "in world space (RF.HangChain) and lie down on the ground in Attack2 / Death; Death ends as a bell on its side")


# ================================================================================================ lamp_lure
@reg
def lamp_lure(bake=True):
    """False Lamp-post: a fungal stalk in the exact shape of a roadside lantern-post - a leaning pole, a crook at the
    top, a hanging cap-cluster shaped like a lantern with four glowing "panes"; gills under the lantern's roof, a
    ring on the stalk and a skirt of roots at its foot give it away up close. Turret (stands on the ground).
    Bones: base, st1-5 (stalk), hk1-3 (crook), lamp (the lantern: hangs by gravity), cap (roof), sac (panes), r1-6."""
    BY = 0.16
    PTS = [(0, BY, 0.10), (0, 0.125, 0.50), (0, 0.09, 0.90), (0, 0.055, 1.30), (0, 0.02, 1.65), (0, -0.01, 1.98)]
    HK = [(0, -0.01, 1.98), (0, -0.10, 2.27), (0, -0.30, 2.37), (0, -0.50, 2.17)]
    TIP = V(HK[-1])
    LX, ZT = float(TIP[1]), float(TIP[2])
    ZR = ZT - 0.218                         # rim of the lantern's roof
    sk = B.Sk()
    sk.bone('base', (0, BY, 0.0), (0, BY, 0.10), 'root')
    st = sk.chain('st', PTS, 'base')
    hk = sk.chain('hk', HK, st[-1])
    sk.bone('lamp', TIP, TIP + V(0, 0, -0.56), hk[-1])
    sk.bone('cap', (0, LX, ZR + 0.008), (0, LX, ZR + 0.13), 'lamp')
    sk.bone('sac', (0, LX, ZR), (0, LX, ZR - 0.33), 'lamp')
    RA = [-62.0, -8.0, 48.0, 112.0, 172.0, 236.0]
    RL = [0.50, 0.42, 0.56, 0.44, 0.52, 0.40]
    RT, RD = [], []
    for i, (a, ln) in enumerate(zip(RA, RL)):
        d = V(math.cos(math.radians(a)), math.sin(math.radians(a)), 0.0)
        RD.append(d)
        RT.append(sk.bone('r%d' % (i + 1), V(0, BY, 0.06) + d * 0.05, V(0, BY, 0.016) + d * ln, 'base'))
    C = B.Beast('mon_lamp_lure', sk)
    std_meta(C, 'lamp', 1.0, 0.0)
    R = RF.Rigid(C, 'base')
    C.look('stalk', 'leather', 'leather_stalk', color=(0.19, 0.145, 0.09), scuff=0.25, scale=20.0, rough=0.72)
    C.look('cap', 'leather', 'leather_stalk', color=(0.30, 0.22, 0.12), scuff=0.2, scale=18.0, rough=0.7)
    C.look('root', 'leather', 'leather_stalk', color=(0.12, 0.095, 0.065), scuff=0.15, scale=24.0, rough=0.8)
    C.look('gill', 'leather', 'leather_stalk', color=(0.50, 0.40, 0.24), scuff=0.1, scale=30.0, rough=0.75)
    C.look('pane', 'glow', 'emis_lamp', color=(0.42, 0.27, 0.09), emit=(1.0, 0.60, 0.18), strength=1.6, vein=(0.16, 0.08, 0.02), vein_scale=17.0,
           vein_w=0.05, scale=9.0)

    def py(z):                              # centre line of the leaning pole
        zs = [q[2] for q in PTS]
        ys = [q[1] for q in PTS]
        return float(np.interp(z, zs, ys))
    # ---- stalk and crook: one skin
    ctrl = [(0, BY, -0.004), (0, BY, 0.05), (0, 0.155, 0.16), PTS[1], PTS[2], PTS[3], PTS[4], PTS[5], (0, -0.045, 2.16), HK[1], (0, -0.20, 2.345),
            HK[2], (0, -0.41, 2.33), (0, -0.48, 2.255), HK[3]]
    rad = [0.135, 0.125, 0.088, 0.066, 0.058, 0.052, 0.047, 0.042, 0.039, 0.037, 0.034, 0.031, 0.028, 0.025, 0.021]
    stalk = P.tubep(ctrl, rad, m=46, n=10, up=(1, 0, 0))
    stalk.rough(0.004, 11.0, seed=3)
    B.wbones(stalk, ['base'] + st + hk, sharp=4.0, k=2)
    stalk.v[:, 2] = np.maximum(stalk.v[:, 2], 0.0)
    C.skin(stalk, 'stalk', smooth=180)
    # the ring on the stalk (a lamp-post's collar) and three bracket shelves (its "fittings")
    yr = py(1.52)
    ringp = M.revolve([(0.040, 1.565), (0.072, 1.54), (0.108, 1.49), (0.104, 1.474), (0.072, 1.498), (0.040, 1.505)], n=14).move(0, yr, 0)
    ringp.rough(0.004, 20.0, seed=5)
    C.rigid(ringp, st[3], 'cap', smooth=60)
    for z, sx, bn, s in ((0.36, -1, st[0], 0.8), (0.74, 1, st[1], 1.0), (1.14, -1, st[2], 0.85)):
        br = P.blob((sx * 0.055, py(z) - 0.012, z), 0.078 * s, 0.062 * s, 0.020 * s, n=10, m=5)
        br.v[:, 2] += 0.02 * s * np.abs(br.v[:, 0] - sx * 0.055) / 0.078
        C.rigid(br, bn, 'cap', smooth=60)
    # ---- the skirt of roots
    for i, (d, ln) in enumerate(zip(RD, RL)):
        sd_ = V(-d[1], d[0], 0.0) * (0.035 if i % 2 else -0.03)
        b0 = V(0, BY, 0.0)
        rt = P.tubep([b0 + d * 0.01 + V(0, 0, 0.075), b0 + d * 0.15 + V(0, 0, 0.055), b0 + d * ln * 0.6 + sd_ + V(0, 0, 0.03), b0 + d * ln + V(0, 0, 0.016)],
                     [0.046, 0.040, 0.026, 0.013], m=8, n=7)
        C.rigid(rt, RT[i], 'root', smooth=180)
    # ---- the lantern
    def sq(z, r, pw=4.0):
        return (z, 0.0, LX, r, r, pw)
    stem = M.tube(np.array([(0, LX, ZT + 0.015), (0, LX, ZT - 0.125)]), 0.0135, n=6)
    C.rigid(stem, 'lamp', 'stalk', smooth=60)
    roof = M.loft_z([sq(ZR + 0.008, 0.002, 3.0), sq(ZR + 0.002, 0.13, 3.0), sq(ZR - 0.006, 0.195, 3.0), sq(ZR + 0.001, 0.213, 3.0), sq(ZR + 0.017, 0.205, 3.0),
                     sq(ZR + 0.058, 0.150, 3.0), sq(ZR + 0.098, 0.085, 2.6), sq(ZR + 0.126, 0.035, 2.2), sq(ZR + 0.140, 0.004, 2.0)], n=20)
    roof.rough(0.004, 16.0, seed=8)
    C.rigid(roof, 'cap', 'cap', smooth=60)
    gl = []
    for k in range(20):
        g_ = M.box(0.078, 0.005, 0.02).move(0.158, 0, 0).rot('z', 18.0 * k + 9.0).move(0, LX, ZR - 0.008)
        gl.append(g_)
    q = M.join(gl); q.smooth = 20.0
    C.rigid(q, 'cap', 'gill')
    body = M.loft_z([sq(ZR, 0.110), sq(ZR - 0.022, 0.122), sq(ZR - 0.092, 0.128), sq(ZR - 0.192, 0.123), sq(ZR - 0.292, 0.106), sq(ZR - 0.327, 0.095)], n=20)
    C.rigid(body, 'sac', 'pane', smooth=60)
    zs = [ZR - 0.005, ZR - 0.022, ZR - 0.092, ZR - 0.192, ZR - 0.292, ZR - 0.325]
    rs = [0.112, 0.124, 0.130, 0.125, 0.108, 0.097]
    ribs = []
    for sx in (1, -1):
        for sy in (1, -1):
            ribs.append(M.tube(np.array([(sx * 0.841 * r_, LX + sy * 0.841 * r_, z_) for z_, r_ in zip(zs, rs)]), 0.0135, n=6, up=(sx, sy, 0)))
    for z_, r_, th in ((ZR - 0.008, 0.116, 0.013), (ZR - 0.150, 0.128, 0.008), (ZR - 0.318, 0.100, 0.013)):
        ribs.append(M.tube(M.rings_z([sq(z_, r_)], 24)[0], th, n=5, closed=True, up=(0, 0, 1)))
    for a in (0, 90):                       # a muntin down the middle of every pane
        for sgn in (1, -1):
            ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
            ribs.append(M.tube(np.array([(sgn * ca * (r_ + 0.002), LX + sgn * sa * (r_ + 0.002), z_) for z_, r_ in zip(zs, rs)]), 0.006, n=4, up=(ca, sa, 0)))
    q = M.join(ribs); q.smooth = 60.0
    C.rigid(q, 'sac', 'cap')
    fin = M.loft_z([sq(ZR - 0.318, 0.085, 3.0), sq(ZR - 0.340, 0.112, 3.0), sq(ZR - 0.354, 0.104, 3.0), sq(ZR - 0.392, 0.055, 2.5), sq(ZR - 0.442, 0.022, 2.0),
                    sq(ZR - 0.485, 0.004, 2.0)], n=16)
    C.rigid(fin, 'sac', 'cap', smooth=60)
    low = V(0, LX, ZR - 0.485)

    LAMP = RF.HangChain(C, ['lamp'], hk[-1], ground=0.15)
    WST = RF.HangChain(C, st, 'base', ground=0.062)
    WHK = RF.HangChain(C, hk, st[-1], ground=0.135)
    RAX = [np.cross(d, V(0, 0, 1.0)) for d in RD]

    def bend(p, x=0.0, y=0.0, u=0.0, amp=0.0, n=1, hookx=0.0):
        """x = the stalk bows forward (degrees over its whole length), hookx = the crook closes"""
        for i, b in enumerate(st):
            p.rot(b, x=x / 5.0 + amp * osc(u, n, -0.06 * i), y=y / 5.0 + 0.5 * amp * osc(u, n, 0.25 - 0.06 * i))
        for b in hk:
            p.rot(b, x=hookx / 3.0)

    def lantern(p, u, sy=0.0, sx=0.0, puff=0.0, flare=0.0, tilt=0.0):
        """the lantern hangs by gravity (sy / sx = its swing, tangent); puff = the panes swell, flare / tilt = the roof"""
        LAMP.pose(p, sx=sx, sy=sy, trail=(0.0, -1.0))
        if puff:
            RF.scale_axes(p, 'sac', 1 + puff, 1 + puff, 1 - 0.3 * puff)
        if tilt:
            p.rot('cap', x=tilt)
        if flare:
            RF.scale_axes(p, 'cap', 1 + flare, 1 + flare, 1 - 0.5 * flare)

    def roots(p, k):
        """k > 0 = the roots lift and clench"""
        for i, b in enumerate(RT):
            p.setR(b, rot_axis(RAX[i], k * (1.0 if i % 2 else 0.7)))

    def idle(p, u):                         # a prop: the faintest sway of the hanging lantern
        bend(p, u=u, amp=0.10, n=1)
        lantern(p, u, sy=0.022 * osc(u, 1, 0.15), sx=0.012 * osc(u, 2, 0.4))
    C.clip('Idle', 4.0, idle)
    R.turret()
    idle0 = lambda p: idle(p, 0.0)

    def bolt(p, u):                         # Attack1: rears back, then whips the lantern forward and puffs a bolt of spores
        hu = 0.6 / 1.2
        w = bump(u, 0.0, 0.30, 0.38, hu)
        s = sm(u, 0.38, hu)
        tau = max(0.0, (u - hu) / (1.0 - hu))
        sw = s * (math.exp(-3.5 * tau) * math.cos(TAU * 2.0 * tau) if u > hu else 1.0)
        pf = bump(u, hu - 0.05, hu, hu + 0.02, hu + 0.25)
        bend(p, x=-12 * w + 16 * sw, u=u, hookx=-10 * w + 14 * sw)
        lantern(p, u, sy=0.40 * w - 0.95 * sw, puff=-0.10 * w + 0.28 * pf, flare=0.18 * pf, tilt=22 * pf)
        roots(p, 6 * w)
    C.clip('Attack1', 1.2, RF.from_pose(C, bolt, idle0, 0.08, 0.1), hit=0.6)
    C.reach('Attack1', 'sac', low)

    def cloud(p, u):                        # Attack2: squats, the lantern swells and bursts into a cloud; the roof flips up
        hu = 0.7 / 1.4
        g = sm(u, 0.0, hu - 0.05)
        pop = sm(u, hu - 0.025, hu + 0.02)
        out = bump(u, hu - 0.025, hu + 0.01, hu + 0.05, hu + 0.32)
        rec = sm(u, hu + 0.1, 0.95)
        sq_ = g * (1 - pop)
        bend(p, x=-9 * sq_ + 8 * out, y=1.6 * sq_ * osc(u, 17), u=u, hookx=10 * sq_ - 12 * out)
        for b in st:
            RF.scale_axes(p, b, 1 + 0.022 * sq_, 1 + 0.022 * sq_, 1 - 0.03 * sq_)
        lantern(p, u, sy=0.06 * sq_ * osc(u, 11), puff=0.58 * sq_ * (1 + 0.08 * osc(u, 9)) - 0.18 * pop * (1 - rec), flare=0.34 * out, tilt=-22 * out)
        roots(p, 14 * sq_ - 5 * out)
    C.clip('Attack2', 1.4, RF.from_pose(C, cloud, idle0, 0.06, 0.08), hit=0.7)
    C.meta['attack_reach'] = max(C.meta.get('attack_reach', 0.0), 0.6)

    def hit(p, u):
        k = bump(u, 0.0, 0.18, 0.3, 1.0)
        bend(p, x=-13 * k, y=5 * k, u=u, hookx=-6 * k)
        lantern(p, u, sy=0.5 * k * math.cos(TAU * 1.5 * u), sx=0.12 * k, tilt=-10 * k)
        roots(p, 5 * k)
    C.clip('Hit', 0.45, RF.from_pose(C, hit, idle0, 0.1, 0.15))

    def death(p, u):                        # the light is pinched out by the game; the stalk wilts from the crook down and collapses
        c = sm(u, 0.04, 0.82) ** 1.15
        nl = len(st) + len(hk)
        w = [sm(1.75 * c - 0.75 * (1 - i / (nl - 1.0)), 0.0, 1.0) for i in range(nl)]
        WST.pose(p, down=(0.0, -1.0, -0.22), trail=(0.05, -1.0), w=w[:len(st)], bend=[(0.0, 0), (0.16, 0), (-0.22, 0), (0.2, 0), (-0.12, 0)])
        WHK.pose(p, down=(0.0, -1.0, -0.22), trail=(0.05, -1.0), w=w[len(st):], bend=[(0.25, 0), (0.1, 0), (-0.3, 0)])
        for b in st:
            RF.scale_axes(p, b, 1 - 0.03 * c, 1 - 0.03 * c, 1.0)
        LAMP.pose(p, trail=(0.08, -1.0))
        RF.scale_axes(p, 'sac', 1 - 0.34 * c, 1 - 0.34 * c, 1 - 0.14 * c)
        RF.scale_axes(p, 'cap', 1 - 0.36 * c, 1 - 0.36 * c, 1 + 0.1 * c)
        p.rot('cap', x=10 * c)
        roots(p, 16 * sm(u, 0.1, 0.6))
    C.clip('Death', 2.0, RF.from_pose(C, death, idle0, 0.05, 0.0), floor='base', settle=(0.82, 0.94))

    def spawn(p, u):                        # a fiddlehead pushes out of the ground, uncurls from the foot up and inflates its lantern
        r = sm(u, 0.0, 0.8)
        p.scl('base', 0.04 + 0.96 * sm(u, 0.0, 0.62) + 0.05 * bump(u, 0.5, 0.62, 0.66, 0.86))
        for i, b in enumerate(st):
            p.rot(b, x=58.0 * (1 - sm(r, 0.10 * i, 0.10 * i + 0.5)))
        for i, b in enumerate(hk):
            p.rot(b, x=42.0 * (1 - sm(r, 0.5 + 0.1 * i, 0.92 + 0.02 * i)))
        lantern(p, u, sy=0.10 * bump(u, 0.6, 0.8, 0.82, 1.0) * osc(u, 5))
        p.scl('lamp', 0.22 + 0.78 * sm(u, 0.58, 0.88) + 0.14 * bump(u, 0.78, 0.88, 0.9, 1.0))
        roots(p, 30 * (1 - sm(u, 0.0, 0.5)))
    C.clip('Spawn', 1.8, RF.from_pose(C, spawn, idle0, 0.0, 0.1), under_ok=True)
    return C.finalize(bake=bake, notes="turret (Walk / Run = Idle, speeds 0); the lantern keeps its amber in every clip (the brief's yellow-green attack "
                                       "colour cannot be animated: emission is baked); Spawn grows out of the ground (under_ok); Death lies wilted along -Y")


# ================================================================================================ engine_heliostat
def gear_ring(R, root, rin, z0, z1, teeth=24):
    """toothed ring in the XY plane between z0 and z1 (the rim of helper H1's cog): closed on all sides"""
    out, inn = [], []
    for i in range(teeth):
        a0 = TAU * i / teeth
        for f, rr in ((0.0, root), (0.14, R), (0.50, R), (0.64, root)):
            a = a0 + TAU / teeth * f
            out.append((rr * math.cos(a), rr * math.sin(a)))
            inn.append((rin * math.cos(a), rin * math.sin(a)))
    n = len(out)
    v = [(x, y, z0) for x, y in out] + [(x, y, z1) for x, y in out] + [(x, y, z0) for x, y in inn] + [(x, y, z1) for x, y in inn]
    f = []
    for j in range(n):
        k = (j + 1) % n
        f += [(j, k, n + k, n + j), (2 * n + k, 2 * n + j, 3 * n + j, 3 * n + k), (n + j, n + k, 3 * n + k, 3 * n + j), (k, j, 2 * n + j, 2 * n + k)]
    q = M.Part(v, f)
    q.recalc()
    q.smooth = 30.0
    return q


def facing(part, zsign):
    """open surface of revolution: make its faces look towards +Z (zsign = 1) or -Z, and keep that winding"""
    t = part.tris()
    nn = np.cross(part.v[t[:, 1]] - part.v[t[:, 0]], part.v[t[:, 2]] - part.v[t[:, 0]])
    if float(nn[:, 2].sum()) * zsign < 0:
        part.flip()
    part.two = True
    return part


@reg
def engine_heliostat(bake=True):
    """Heliostat: a tripod of telescoping brass legs taller than a man; on its head a toothed turntable and a bowed
    yoke that carries a concave mirror-dish two metres across in a gimbal ring; a counterweight arm behind the dish,
    a sighting vane on its upper rim, a plumb-bob under the head. Turret. The dish faces up and forward (the game
    camera looks into it). Bones: frame (head), leg1-3 a / b (1 = back, 2 = front right, 3 = front left), yaw, dish
    (pitch about X), cw, vane, pl1-2 (plumb line)."""
    HZ = 1.85
    PC = V(0, 0, 2.52)                      # centre of the dish = its pitch axis (high enough for the dish to clear the turntable at 47 deg)
    T0 = 30.0                               # tilt of the dish from horizontal at rest
    cT, sT = math.cos(math.radians(T0)), math.sin(math.radians(T0))
    n0 = V(0, -sT, cT)                      # the way the dish faces
    u0 = V(0, cT, sT)                       # "up" in the plane of the dish (towards its upper rim)
    Rd, Dd, Rr = 1.0, 0.17, 1.07            # dish radius / depth, gimbal ring radius
    c30 = math.cos(math.radians(30))
    LD = [V(0, 1, 0), V(-c30, -0.5, 0), V(c30, -0.5, 0)]
    HG = [d * 0.115 + V(0, 0, 1.80) for d in LD]
    FT = [d * 1.0 + V(0, 0, 0.03) for d in LD]
    MD = [(h + f) / 2 for h, f in zip(HG, FT)]
    AL = [unit(f - h) for h, f in zip(HG, FT)]
    sk = B.Sk()
    sk.bone('frame', (0, 0, 1.62), (0, 0, HZ), 'root')
    for i in range(3):
        sk.bone('leg%da' % (i + 1), HG[i], MD[i], 'frame')
        sk.bone('leg%db' % (i + 1), MD[i], FT[i], 'leg%da' % (i + 1))
    sk.bone('yaw', (0, 0, 1.93), (0, 0, 2.05), 'frame')
    sk.bone('dish', PC, PC + n0 * 0.3, 'yaw')
    ZB0 = -Dd - 0.048                       # back of the dish at its centre (dish space)
    BC = PC + n0 * (ZB0 - 0.05)
    sk.bone('cw', BC, BC - n0 * 0.85, 'dish')
    RT = PC + u0 * Rr
    sk.bone('vane', RT, RT + n0 * 0.24, 'dish')
    pl = sk.chain('pl', [(0, 0, 1.70), (0, 0, 1.42), (0, 0, 1.14)], 'frame')
    C = B.Beast('mon_engine_heliostat', sk)
    std_meta(C, 'engine', 1.0, 0.0)
    R = RF.Rigid(C, 'frame', pivot=(0, 0, 0))
    C.look('brass', 'metal', 'metal_brass', **L.PRESET['brass'])
    C.look('mirror', 'metal', 'metal_brass', color=(0.78, 0.66, 0.36), rust=(0.3, 0.3, 0.2), rust2=(0.2, 0.2, 0.12), amount=0.07, edge_color=(0.95, 0.85, 0.55),
           rough=0.13, hammer=6.0, edge=0.2)
    C.look('dark', 'metal', 'metal_brass', **L.PRESET['black_plate'])
    C.look('spot', 'emis', 'emis_focus', emit=(0.86, 0.78, 1.0), strength=1.2, mottle=0.25, base_mul=0.6, scale=40.0)
    # ---- tripod
    head = M.revolve([(0.0, 1.66), (0.07, 1.68), (0.13, 1.73), (0.155, 1.79), (0.155, 1.865), (0.12, 1.90), (0.12, 1.93), (0.0, 1.93)], n=16)
    RF.core(C, head, 'frame', 'brass', smooth=50)
    fr = []
    for i, d in enumerate(LD):
        ang = math.degrees(math.atan2(d[1], d[0]))
        fr.append(M.box(0.11, 0.085, 0.10, bevel=0.01).rot('z', ang).move(*(d * 0.15 + V(0, 0, 1.80))))
        fr.append(M.tube(np.array([HG[i] + np.cross(d, V(0, 0, 1)) * 0.06, HG[i] - np.cross(d, V(0, 0, 1)) * 0.06]) + d * 0.03, 0.02, n=8))     # hinge pin
    fr.append(M.tube(np.array([(0, 0.13, 1.87), (0, 0.265, 1.95)]), 0.014, n=6))         # the pawl that clicks on the turntable
    fr.append(M.box(0.03, 0.035, 0.05).move(0, 0.265, 1.95))
    q = M.join(fr); q.smooth = 40.0
    C.rigid(q, 'frame', 'brass')
    for i in range(3):
        a = AL[i]
        side = np.cross(a, V(0, 0, 1)); side = unit(side)
        up_ = M.join([M.tube(np.array([HG[i] - a * 0.015, MD[i] + a * 0.05]), 0.055, n=10),
                      M.tube(np.array([MD[i] - a * 0.07, MD[i] + a * 0.06]), 0.075, n=10),
                      M.tube(np.array([MD[i] - a * 0.01, MD[i] - a * 0.01 + side * 0.10]), 0.013, n=6),
                      P.blob(MD[i] - a * 0.01 + side * 0.11, 0.028, 0.028, 0.012, n=8, m=4, rot=M.rot_to((0, 0, 1), side))])
        up_.smooth = 50.0
        C.rigid(up_, 'leg%da' % (i + 1), 'brass')
        lo = M.join([M.tube(np.array([MD[i] - a * 0.12, FT[i] - a * 0.005]), 0.042, n=10),
                     M.revolve([(0.0, 0.0), (0.112, 0.0), (0.12, 0.014), (0.09, 0.036), (0.05, 0.055), (0.0, 0.06)], n=12).move(FT[i][0], FT[i][1], 0.0)])
        lo.smooth = 50.0
        C.rigid(lo, 'leg%db' % (i + 1), 'brass')
        C.rigid(P.cone(FT[i] + LD[i] * 0.05 + V(0, 0, 0.01), V(LD[i][0], LD[i][1], -0.6), 0.06, 0.016, n=5), 'leg%db' % (i + 1), 'dark', smooth=30)     # spur
    # ---- turntable and yoke
    hub = M.revolve([(0.0, 1.925), (0.165, 1.925), (0.165, 1.96), (0.10, 1.975), (0.10, 2.03), (0.0, 2.03)], n=18)
    gear = gear_ring(0.24, 0.212, 0.15, 1.932, 1.972, teeth=24)
    yoke = P.tubep([(-1.15, 0, PC[2] + 0.01), (-1.135, 0, 2.32), (-0.86, 0, 2.10), (0, 0, 2.005), (0.86, 0, 2.10), (1.135, 0, 2.32), (1.15, 0, PC[2] + 0.01)], 0.034, 0.056, m=27, n=8,
                   power=4.0, up=(0, 1, 0))
    brg = [M.tube(np.array([(sx * 1.10, 0, PC[2]), (sx * 1.215, 0, PC[2])]), 0.078, n=12) for sx in (1, -1)]
    q = M.join([hub, gear, yoke] + brg); q.smooth = 40.0
    C.rigid(q, 'yaw', 'brass')
    # ---- the dish (built facing +Z about the origin, then tilted into place)
    Tm = M.rot_to((0, 0, 1), n0)

    def zf(r):
        return -Dd * (1.0 - (r / Rd) ** 2)

    def zb(r):
        return zf(r) - (0.028 + 0.02 * (1.0 - r / Rd))

    def put(part, bone, look, smooth=None):
        part.tf(Tm).move(*PC)
        return C.rigid(part, bone, look, smooth=smooth)
    front = facing(M.revolve([(r, zf(r)) for r in (0.0, 0.12, 0.28, 0.45, 0.62, 0.78, 0.91, 1.0)], n=48, cap=False), 1.0)
    put(front, 'dish', 'mirror', smooth=60)
    back = facing(M.revolve([(Rd, 0.0), (Rd + 0.022, -0.006), (Rd + 0.022, -0.034)] + [(r, zb(r)) for r in (0.97, 0.8, 0.58, 0.34, 0.12, 0.0)], n=48, cap=False), -1.0)
    put(back, 'dish', 'brass', smooth=40)
    seams = []
    for k in range(12):
        a = TAU * (k + 0.5) / 12
        seams.append(P.tubep([(r * math.cos(a), r * math.sin(a), zf(r) + 0.002) for r in (0.10, 0.4, 0.7, 0.99)], 0.0055, m=7, n=4))
    for rr in (0.46, 0.76):
        seams.append(M.torus(rr, 0.0055, n=40, m=4).move(0, 0, zf(rr) + 0.002))
    q = M.join(seams); q.smooth = 60.0
    put(q, 'dish', 'dark')
    put(P.blob((0, 0, zf(0) + 0.012), 0.05, 0.05, 0.024, n=12, m=6), 'dish', 'spot', smooth=180)
    bk = [M.torus(0.062, 0.014, n=14, m=5).move(0, 0, zf(0) + 0.008), M.tube(np.array([(0, 0, ZB0 + 0.02), (0, 0, ZB0 - 0.075)]), 0.105, n=14),
          M.torus(Rr, 0.034, n=48, m=6).move(0, 0, -0.012)]
    for k in range(8):
        a = TAU * k / 8
        bk.append(P.tubep([(r * math.cos(a), r * math.sin(a), zb(r) - 0.008) for r in (0.08, 0.4, 0.72, 1.0)], 0.02, 0.014, m=7, n=4, up=(0, 0, 1)))
        a2 = a + TAU / 16
        bk.append(M.box(0.085, 0.045, 0.03).rot('z', math.degrees(a2)).move(1.045 * math.cos(a2), 1.045 * math.sin(a2), -0.014))
    for k in range(16):
        a = TAU * (k + 0.5) / 16
        bk.append(P.blob(((Rd + 0.024) * math.cos(a), (Rd + 0.024) * math.sin(a), -0.02), 0.014, 0.014, 0.014, n=6, m=4))
    for sx in (1, -1):
        bk.append(M.tube(np.array([(sx * 1.03, 0, 0), (sx * 1.20, 0, 0)]), 0.032, n=10))
    q = M.join(bk); q.smooth = 40.0
    put(q, 'dish', 'brass')
    # counterweight arm (hinged on the boss behind the dish) and the sighting vane on the upper rim
    zc = ZB0 - 0.05
    cwp = M.join([M.tube(np.array([(0, 0, zc + 0.03), (0, 0, zc - 0.85)]), 0.026, n=8),
                  M.revolve([(0.0, -0.065), (0.12, -0.055), (0.165, -0.012), (0.165, 0.012), (0.12, 0.055), (0.0, 0.065)], n=16).move(0, 0, zc - 0.62),
                  P.blob((0, 0, zc - 0.85), 0.045, 0.045, 0.045, n=8, m=5), M.box(0.11, 0.07, 0.07, bevel=0.008).move(0, 0, zc + 0.01)])
    cwp.smooth = 50.0
    put(cwp, 'cw', 'brass')
    vn = M.join([M.tube(np.array([(0, Rr, -0.03), (0, Rr, 0.215)]), 0.011, n=6), M.box(0.005, 0.18, 0.085).move(0, Rr + 0.10, 0.15),
                 P.ring((0, Rr, 0.245), 0.032, 0.006, axis=(0, 1, 0), n=12, m=4)])
    vn.smooth = 40.0
    put(vn, 'vane', 'brass')
    # plumb line under the head
    RF.chain_parts(C, [pl[0]], 'dark', link=0.04, r=0.005)
    C.rigid(M.join([M.tube(np.array([(0, 0, 1.43), (0, 0, 1.29)]), 0.004, n=4), M.revolve([(0.0, 1.31), (0.012, 1.305), (0.046, 1.25), (0.036, 1.19), (0.0, 1.13)], n=10)]),
            pl[1], 'brass', smooth=50)

    PL = RF.HangChain(C, pl, 'frame', ground=0.06)
    LIMP = RF.HangChain(C, ['leg3a', 'leg3b'], 'frame', ground=0.06)
    LA = nrm(MD[0] - HG[0])
    LB = nrm(FT[0] - MD[0])
    ALF = math.degrees(math.acos(-AL[0][2]))

    def works(p, yaw=0.0, tilt=0.0, cw=0.0, vane=0.0):
        """yaw of the turntable, tilt of the dish (+ = it tips down on to the target), swing of the counterweight
        arm, spin of the vane on its post"""
        p.rot('yaw', z=yaw)
        p.rot('dish', x=tilt)
        p.rot('cw', x=cw)
        p.setR('vane', rot_axis(n0, vane))

    def click(u, n, move=0.28):
        """what rings after each of n ratchet steps over u = 0..1 (-1 .. 1)"""
        x = min(max(u, 0.0), 0.9999) * n
        f = x - math.floor(x)
        g = (f - move) / (1.0 - move)
        return math.exp(-5.0 * g) * math.sin(TAU * 3.0 * g) if g > 0 else 0.0

    SCAN = [0, 12, 24, 12, 0, -12, -24, -12, 0]

    def idle(p, u):                         # a slow clicking scan: the turntable steps, everything rings a little after each click
        wb = click(u, 8)
        works(p, yaw=RF.stepped(u, SCAN, move=0.28, over=0.10), tilt=1.3 * wb, cw=-4.0 * wb + 1.0 * osc(u, 4), vane=7 * osc(u, 2) + 12 * wb)
        PL.pose(p, u=u, amp=0.03, n=3, sx=0.05 * wb)
    C.clip('Idle', 6.0, idle)
    R.turret()
    idle0 = lambda p: idle(p, 0.0)
    rim_lo = PC - u0 * Rr

    def beam(p, u):                         # Attack1: ratchets the dish down on to the target, holds it trembling, fires (the recoil kicks it back)
        hu = 1.2 / 2.0
        t1 = RF.stepped(min(u / 0.30, 1.0), [0, 6, 12, 17], move=0.55, over=0.08)
        hold = sm(u, 0.30, hu) * (1 - sm(u, hu, hu + 0.02))
        rec = bump(u, hu, hu + 0.015, hu + 0.04, hu + 0.18)
        back = sm(u, 0.74, 0.95)
        tilt = t1 * (1 - back) + 0.4 * hold * osc(u, 37) - 6.0 * rec
        works(p, yaw=0.45 * hold * osc(u, 43, 0.2) + 1.5 * rec * osc(u, 9), tilt=tilt,
              cw=-0.35 * tilt + 7 * rec * math.cos(TAU * 6 * (u - hu)) + 2.5 * click(u / 0.30, 3, 0.55) * (u < 0.30), vane=22 * sm(u, 0.05, 0.3) * (1 - back) + 5 * rec)
        PL.pose(p, u=u, amp=0.02 + 0.06 * rec, n=6)
    C.clip('Attack1', 2.0, RF.from_pose(C, beam, idle0, 0.05, 0.06), hit=1.2)
    C.reach('Attack1', 'dish', rim_lo)

    def flash(p, u):                        # Attack2: winds itself round and back click by click, then snaps wide across the front
        hu = 0.8 / 1.4
        ws = RF.stepped(sm(u, 0.0, hu - 0.09), [0, 0.34, 0.67, 1.0], move=0.5, over=0.06)
        snap = sm(u, hu - 0.05, hu)
        tau = max(0.0, (u - hu) / (1.0 - hu))
        ro = math.exp(-5.0 * tau) * math.sin(TAU * 3.0 * tau) if u > hu else 0.0
        back = sm(u, 0.72, 0.96)
        tilt = (-5.0 * ws * (1 - snap) + snap * (14.0 + 3.0 * ro)) * (1 - back)
        works(p, yaw=(-30.0 * ws * (1 - snap) + snap * (30.0 + 6.0 * ro)) * (1 - back), tilt=tilt, cw=-0.4 * tilt + 11 * ro, vane=360.0 * sm(u, hu - 0.05, hu + 0.3))
        PL.pose(p, u=u, amp=0.02 + 0.07 * snap * (1 - back), n=5)
    C.clip('Attack2', 1.4, RF.from_pose(C, flash, idle0, 0.05, 0.06), hit=0.8)
    C.reach('Attack2', 'dish', rim_lo)

    def hit(p, u):
        k = bump(u, 0.0, 0.15, 0.25, 1.0)
        wob = math.sin(TAU * 3 * u) * k
        works(p, yaw=7 * k + 2 * wob, tilt=-5 * k + 3 * wob, cw=12 * wob, vane=40 * k)
        R.body(p, pitch=-0.8 * k, roll=0.5 * wob)
        PL.pose(p, u=u, sy=0.2 * k, amp=0.08 * k, n=3)
    C.clip('Hit', 0.5, RF.from_pose(C, hit, idle0, 0.1, 0.15))

    # ---- Death: the front-left leg gives way and the whole frame topples over the line of the other two feet
    AX = unit(FT[1] - FT[0])
    hd0 = V(0, 0, HZ)
    SG = 1.0 if nrm(FT[0] + rot_axis(AX, 5.0) @ (hd0 - FT[0]) - FT[2]) < nrm(hd0 - FT[2]) else -1.0
    FY, FTI, FCW = 20.0, 12.0, -30.0        # where the works end up: yaw, tilt, counterweight
    probe = [('dish', PC + Tm @ V((Rr + 0.034) * math.cos(a), (Rr + 0.034) * math.sin(a), -0.012)) for a in np.linspace(0, TAU, 24, endpoint=False)]
    probe += [('yaw', V(sx * 1.215, 0, PC[2] + sz * 0.078)) for sx in (1, -1) for sz in (1, -1)] + [('frame', V(0.16, 0, 1.87)), ('frame', V(-0.16, 0, 1.87))]
    probe += [('cw', PC + Tm @ V(0.17 * math.cos(a), 0.17 * math.sin(a), zc - 0.62)) for a in np.linspace(0, TAU, 8, endpoint=False)]

    def lowest(th):
        q = C.pose()
        R.turn(q, rot_axis(AX, SG * th), pivot=FT[0])
        works(q, yaw=FY, tilt=FTI, cw=FCW)
        return min(float(q.pt(b, pt)[2]) for b, pt in probe)
    lo_, hi_ = 10.0, 100.0
    for _ in range(18):
        mid = (lo_ + hi_) / 2
        if lowest(mid) > 0.02:
            lo_ = mid
        else:
            hi_ = mid
    TH = lo_
    B.log("heliostat topples by %.1f deg" % TH)
    out3 = (float(LD[2][0]), float(LD[2][1]))

    def death(p, u):
        fall = sm(u, 0.16, 0.56) ** 2.0
        bn = bump(u, 0.56, 0.585, 0.60, 0.80)
        th = TH * (fall - 0.06 * bn * abs(math.sin(TAU * 3.0 * (u - 0.56) / 0.24)))
        R.turn(p, rot_axis(AX, SG * th), pivot=FT[0])
        sw = sm(u, 0.1, 0.56)
        works(p, yaw=FY * sw, tilt=12 * sm(u, 0.04, 0.2) * (1 - sw) + FTI * sw + 3 * bn * osc(u, 7), cw=FCW * sw + 14 * bn * osc(u, 9), vane=200 * fall)
        LIMP.pose(p, w=[sm(u, 0.13, 0.36), sm(u, 0.05, 0.2)], trail=out3, sx=0.25 * out3[0] * sm(u, 0.05, 0.3), sy=0.25 * out3[1] * sm(u, 0.05, 0.3))
        PL.pose(p, u=u, amp=0.06 * (1 - fall), n=4, trail=out3)
    C.clip('Death', 2.4, RF.from_pose(C, death, idle0, 0.04, 0.0), floor='frame', settle=(0.6, 0.72))

    def spawn(p, u):                        # unfolds: the legs telescope out and spread, the dish ratchets up, the arm and the vane swing out
        e = 0.22 + 0.78 * sm(u, 0.0, 0.34)
        s = sm(u, 0.16, 0.52)
        al = math.radians(ALF * s)
        for i in range(3):
            tgt = V(LD[i][0] * math.sin(al), LD[i][1] * math.sin(al), -math.cos(al))
            p.setR('leg%da' % (i + 1), M.rot_to(AL[i], tgt))
            p.move('leg%db' % (i + 1), -AL[i] * LB * (1 - e))
        R.body(p, dz=FT[0][2] - HG[0][2] + (LA + LB * e) * math.cos(al) + 0.02 * bump(u, 0.50, 0.54, 0.56, 0.66))
        up_ = RF.stepped(sm(u, 0.42, 0.80), [0, 0.3, 0.55, 0.8, 1.0], move=0.5, over=0.06)
        works(p, yaw=-36.0 * (1 - RF.stepped(sm(u, 0.76, 0.97), [0, 0.5, 1.0], move=0.5, over=0.1)), tilt=-22.0 * (1 - up_), cw=62.0 * (1 - sm(u, 0.5, 0.8)))
        p.setR('vane', rot_axis(V(1, 0, 0), -84.0 * (1 - sm(u, 0.72, 0.88))))
        PL.pose(p, u=u, amp=0.05, n=4)
    C.clip('Spawn', 2.0, RF.from_pose(C, spawn, idle0, 0.0, 0.06))
    return C.finalize(bake=bake, notes="turret (Walk / Run = Idle, speeds 0); the dish is tilted 30 deg from horizontal (faces up and forward, 3.1 m to its top); "
                                       "Idle = stepped yaw scan +-24 deg inside the model; the focus light is only a small emissive boss in the dish centre")


# ================================================================================================ forge_eye
def k_furnace(N, p):
    """the inside of a furnace: white-hot at `center`, through yellow and lava orange to dull red `radius` away;
    flecks of cooled crust away from the hottest part. The emission mask is a COLOUR."""
    d = _dist(N, p['center'])
    t = N.mapr(d, 0.0, L._c(p, 'radius', 0.5), 0.0, 1.0, smooth=False)
    hot = N.ramp(t, [(0.0, (1.0, 0.95, 0.82)), (0.30, (1.0, 0.88, 0.58)), (0.55, (1.0, 0.62, 0.16)), (0.82, (0.95, 0.32, 0.05)), (1.0, (0.60, 0.12, 0.02))])
    n = N.noise(14, 3, 0.6)
    crust = N.mapr(N.noise(30, 3, 0.65, vec=N.warp(0.02, 30)), 0.54, 0.70)
    crust = N.math('MULTIPLY', crust, N.mapr(t, 0.45, 0.9))
    inten = N.math('MULTIPLY', N.mapr(n, 0.25, 0.8, 0.75, 1.0), N.math('SUBTRACT', 1.0, N.math('MULTIPLY', crust, 0.85)))
    return dict(color=N.mix(crust, N.mul(hot, 0.6), (0.03, 0.018, 0.012)), rough=0.7, metal=0.0, height=n, bump=(0.2, 0.003),
                emit=((1.0, 1.0, 1.0), L._c(p, 'strength', 1.5)), emask=N.mul(hot, inten))


L.KINDS.update(furnace=k_furnace)
L.GRIME.update(furnace=((0.02, 0.012, 0.008), 0.3))


@reg
def forge_eye(bake=True):
    """Forge-eye: a round iron furnace door on a squat chimney-stump bolted to the floor. The door looks forward and a
    little up; its lid is hinged at the top and swings up like an eyelid on a lever that carries a hanging
    counterweight and a cross-bar with two chains. Shut (rest pose, Idle) it is a black disc; open it shows the
    white-hot inside. Turret. Bones: body, arm (lid + lever; x < 0 opens), cw (the weight), chl1-3 / chr1-3."""
    E = 40.0                                # the eye looks this far up: the game camera (52 deg down) sees into it under the raised lid
    cE, sE = math.cos(math.radians(E)), math.sin(math.radians(E))
    ax = V(0, -cE, sE)                      # the way the eye looks
    yv = V(0, sE, cE)                       # "up" across the door
    DC = V(0, -0.16, 1.16)
    FC = DC + ax * 0.26                     # centre of the door's face
    Mx = np.eye(4)
    Mx[:3, 0] = (1, 0, 0); Mx[:3, 1] = yv; Mx[:3, 2] = ax; Mx[:3, 3] = FC      # eye space (x, up, out) -> armature
    RO, RH = 0.61, 0.43                     # drum radius, opening radius
    HH = FC + yv * 0.62 + ax * 0.085        # the hinge line (along X)
    LV = V(0, 0.05, 0.50)                   # the lever: a mast from the hinge ...
    TIP = HH + LV
    TIP2 = TIP + V(0, 0.34, 0)              # ... and an arm back from its top, where the counterweight hangs
    CB = HH + LV * 0.5                      # cross-bar
    XC = 0.72
    BR = 0.135                              # the counterweight ball
    BCT = TIP2 + V(0, 0, -0.25)
    sk = B.Sk()
    sk.bone('body', (0, 0, 0), (0, 0, 0.5), 'root')
    sk.bone('arm', HH, TIP, 'body')
    sk.bone('cw', TIP2, TIP2 + V(0, 0, -0.27), 'arm')
    chl = sk.chain('chl', [V(XC, CB[1], CB[2] - 0.02 - 0.29 * i) for i in range(4)], 'arm')
    chr_ = sk.chain('chr', [V(-XC, CB[1], CB[2] - 0.02 - 0.29 * i) for i in range(4)], 'arm')
    C = B.Beast('mon_forge_eye', sk)
    std_meta(C, 'forge', 1.0, 0.0)
    R = RF.Rigid(C, 'body', pivot=(0, 0, 0))
    C.look('iron', 'metal', 'metal_iron', **L.PRESET['black_plate'])
    C.look('rust', 'metal', 'metal_iron', **L.PRESET['rusty_iron'])
    C.look('hot', 'furnace', 'emis_fire', center=tuple(float(x) for x in (FC - ax * 0.36)), radius=0.66, strength=1.5)

    def eye(part):
        return part.tf(Mx)
    # ---- the stump: a short fat flue on a bolted flange (it ends inside the furnace head, below the hollow)
    stump = M.revolve([(0.0, 0.0), (0.66, 0.0), (0.66, 0.05), (0.58, 0.065), (0.565, 0.10), (0.552, 0.45), (0.536, 0.78), (0.0, 0.78)], n=28)
    RF.core(C, stump, 'body', 'iron', smooth=40)
    det = [M.torus(0.578, 0.022, n=28, m=5).move(0, 0, 0.16), M.torus(0.566, 0.022, n=28, m=5).move(0, 0, 0.44), M.torus(0.552, 0.024, n=28, m=5).move(0, 0, 0.70)]
    for k in range(8):
        a = TAU * (k + 0.5) / 8
        det.append(M.tube(np.array([(0.615 * math.cos(a), 0.615 * math.sin(a), 0.04), (0.615 * math.cos(a), 0.615 * math.sin(a), 0.085)]), 0.04, n=6))
        det.append(M.tube(np.array([(0.615 * math.cos(a), 0.615 * math.sin(a), 0.08), (0.615 * math.cos(a), 0.615 * math.sin(a), 0.105)]), 0.018, n=6))
    for k in range(14):
        a = TAU * k / 14
        det.append(P.blob((0.572 * math.cos(a), 0.572 * math.sin(a), 0.70), 0.016, 0.016, 0.016, n=6, m=4))
    det.append(M.box(0.27, 0.05, 0.19, bevel=0.012).move(0, -0.556, 0.30))                # ash door low on the front
    det.append(P.ring((0, -0.588, 0.30), 0.04, 0.009, axis=(1, 0, 0), n=10, m=4))
    q = M.join(det); q.smooth = 40.0
    C.rigid(q, 'body', 'rust')
    # ---- the furnace head: a drum with the round door frame on its face, the glowing hollow inside
    drum = M.revolve([(RH, 0.0), (RH + 0.02, 0.03), (0.57, 0.03), (RO, 0.01), (RO, -0.20), (RO + 0.022, -0.21), (RO + 0.022, -0.27), (RO, -0.28), (RO, -0.50),
                      (0.0, -0.50)], n=36, cap=False)
    C.rigid(eye(drum), 'body', 'iron', smooth=40)
    hollow = facing(M.revolve([(RH, 0.0), (RH - 0.01, -0.08), (0.37, -0.28), (0.0, -0.36)], n=30, cap=False), 1.0)
    C.rigid(eye(hollow), 'body', 'hot', smooth=60)
    fr = [M.torus(0.585, 0.045, n=36, m=6).move(0, 0, 0.012)]
    for k in range(12):
        a = TAU * (k + 0.5) / 12
        fr.append(P.blob((0.505 * math.cos(a), 0.505 * math.sin(a), 0.032), 0.02, 0.02, 0.014, n=6, m=4))
    for sx in (1, -1):
        fr.append(M.box(0.11, 0.13, 0.11, bevel=0.01).move(sx * 0.31, 0.615, 0.045))
    q = M.join(fr); q.smooth = 40.0
    C.rigid(eye(q), 'body', 'rust')
    # ---- the lid on its lever
    lidp = M.revolve([(0.0, 0.092), (0.20, 0.088), (0.42, 0.070), (0.50, 0.050), (0.508, 0.034), (0.0, 0.034)], n=36)
    C.rigid(eye(lidp), 'arm', 'iron', smooth=40)
    lf = [M.torus(0.488, 0.016, n=36, m=5).move(0, 0, 0.058), M.revolve([(0.0, 0.118), (0.06, 0.112), (0.095, 0.085), (0.0, 0.085)], n=12),
          M.box(0.13, 0.64, 0.028).move(0, 0.32, 0.098), M.tube(np.array([(-0.33, 0.62, 0.085), (0.33, 0.62, 0.085)]), 0.04, n=10)]
    for k in range(8):
        a = TAU * (k + 0.25) / 8
        lf.append(P.blob((0.40 * math.cos(a), 0.40 * math.sin(a), 0.078), 0.018, 0.018, 0.012, n=6, m=4))
    q = M.join(lf); q.smooth = 40.0
    C.rigid(eye(q), 'arm', 'rust')
    lv = [M.tube(np.array([HH - unit(LV) * 0.02, TIP + unit(LV) * 0.03]), 0.065, n=4, r2=0.03, up=(1, 0, 0), phase=math.pi / 4),
          M.tube(np.array([TIP + V(0, -0.03, 0), TIP2 + V(0, 0.035, 0)]), 0.058, n=4, r2=0.03, up=(1, 0, 0), phase=math.pi / 4),
          M.tube(np.array([TIP + V(0, 0.02, -0.19), TIP + V(0, 0.21, 0.0)]), 0.016, n=6),          # a stay under the arm
          M.tube(np.array([CB + V(-XC - 0.05, 0, 0), CB + V(XC + 0.05, 0, 0)]), 0.027, n=8),
          P.ring(TIP2 + V(0, 0, -0.03), 0.036, 0.010, axis=(1, 0, 0), n=10, m=5)]
    for sx in (1, -1):
        lv.append(P.ring(CB + V(sx * XC, 0, -0.03), 0.03, 0.008, axis=(0, 1, 0), n=10, m=4))
    q = M.join(lv); q.smooth = 30.0
    C.rigid(q, 'arm', 'rust')
    ball = P.blob(BCT, BR, BR, BR * 0.93, n=14, m=8)
    wt = M.join([M.chain(np.array([TIP2 + V(0, 0, -0.03), TIP2 + V(0, 0, -0.125)]), link=0.07, r=0.011), ball, M.torus(BR * 0.99, 0.012, n=16, m=4).move(*BCT)])
    wt.smooth = 60.0
    C.rigid(wt, 'cw', 'iron', free=True)    # (comes off its link in Death)
    RF.chain_parts(C, chl, 'rust', link=0.075, r=0.011)
    RF.chain_parts(C, chr_, 'rust', link=0.075, r=0.011)

    CW = RF.HangChain(C, ['cw'], 'arm', ground=0.16)
    CHL = RF.HangChain(C, chl, 'arm', ground=0.05)
    CHR = RF.HangChain(C, chr_, 'arm', ground=0.05)
    SUB = [RF.HangChain(C, b, 'arm', ground=-9.0) for b in (['cw'], chl, chr_)]        # the same without a floor (Spawn: it comes up through it)

    def lid(p, a):
        """a = degrees the lid is open"""
        p.rot('arm', x=-max(a, 0.0))

    def hang(p, u, sy=0.0, amp=0.02, n=2, cw_y=0.0, cw_amp=0.02, weight=True, sub=False):
        cw_, l_, r_ = SUB if sub else (CW, CHL, CHR)
        if weight:
            cw_.pose(p, sy=cw_y, u=u, amp=cw_amp, n=n, ph=0.1, amp_x=cw_amp * 0.4)
        l_.pose(p, sy=sy, u=u, amp=amp, n=n, lag=0.12, amp_x=amp * 0.5, trail=(1.0, 0.3))
        r_.pose(p, sy=sy, u=u, amp=amp, n=n, lag=0.14, ph=0.35, amp_x=amp * 0.5, trail=(-1.0, 0.3))

    def idle(p, u):                         # shut: a black disc. Now and then the lid lifts a crack and drops back with a clank
        a = 8.0 * bump(u, 0.30, 0.37, 0.40, 0.425) * (1 + 0.12 * osc(u, 40)) + 1.6 * RF.clank(u, 0.425) + 4.5 * bump(u, 0.72, 0.755, 0.77, 0.79) + 1.0 * RF.clank(u, 0.79)
        lid(p, a)
        hang(p, u, amp=0.02, n=3, cw_amp=0.025)
    C.clip('Idle', 5.0, idle)
    R.turret()
    idle0 = lambda p: idle(p, 0.0)
    OPEN = 74.0

    def beam(p, u):                         # Attack1: cranks the lid open in three heaves, glares, fires (recoil), stays open, slams shut
        hu = 1.2 / 2.4
        op = RF.stepped(sm(u, 0.03, 0.26), [0, 0.30, 0.62, 1.0], move=0.55, over=0.07)
        cl = sm(u, 0.80, 0.90) ** 2
        heat = sm(u, 0.26, hu) * (1 - sm(u, hu, hu + 0.03))
        rec = bump(u, hu, hu + 0.012, hu + 0.03, hu + 0.14)
        lid(p, OPEN * op * (1 - cl) + 5.0 * rec + 2.0 * RF.clank(u, 0.90))
        R.body(p, pitch=-1.8 * rec + 0.25 * heat * osc(u, 47), roll=0.25 * heat * osc(u, 41, 0.3))
        hang(p, u, amp=0.02 + 0.05 * rec, n=6, cw_amp=0.03 + 0.08 * rec)
    C.clip('Attack1', 2.4, RF.from_pose(C, beam, idle0, 0.03, 0.05), hit=1.2)
    C.reach('Attack1', 'body', FC + ax * 0.1)

    def spray(p, u):                        # Attack2: the lid flaps open and shut, wider each time, and throws embers on the last
        k = bump(u, 0.05, 0.2, 0.7, 0.9)
        lid(p, RF.keyed(u, [0.0, 0.12, 0.21, 0.33, 0.42, 0.8 / 1.4, 0.70, 0.84], [0, 40, 6, 54, 8, OPEN, OPEN - 5, 0]) + 2.0 * RF.clank(u, 0.84))
        R.body(p, pitch=-1.2 * k * osc(u, 4, 0.1), roll=0.8 * k * osc(u, 4, 0.35))
        hang(p, u, amp=0.03 + 0.05 * k, n=5, cw_amp=0.05 + 0.08 * k)
    C.clip('Attack2', 1.4, RF.from_pose(C, spray, idle0, 0.03, 0.05), hit=0.8)
    C.reach('Attack2', 'body', FC + ax * 0.1)

    def hit(p, u):
        k = bump(u, 0.0, 0.15, 0.25, 1.0)
        lid(p, 7.0 * k * abs(math.sin(TAU * 3 * u)))
        R.body(p, pitch=-1.6 * k, roll=0.8 * k * math.sin(TAU * 2 * u))
        hang(p, u, sy=0.12 * k, amp=0.06 * k, n=3, cw_y=0.15 * k, cw_amp=0.08 * k)
    C.clip('Hit', 0.45, RF.from_pose(C, hit, idle0, 0.1, 0.15))

    DUR_D = 2.0
    snap = 0.30                             # the weight's link parts
    lean = unit(V(0.45, 0.89, 0.0))         # the way the stump slumps
    kax = np.cross(V(0, 0, 1.0), lean)
    vel = V(-0.55, 1.0, 0.5)
    zland = BR * 0.93 + 0.004

    def death(p, u):                        # one last glare, the lid slams shut for good, the counterweight drops off, the stump slumps
        sl = sm(u, 0.36, 0.62) ** 1.5
        jolt = RF.clank(u, 0.62, rate=14.0, freq=7.0)
        R.turn(p, rot_axis(kax, 9.0 * sl + 0.8 * jolt), pivot=lean * 0.66)
        lid(p, 32.0 * bump(u, 0.04, 0.16, 0.19, 0.25) + 3.0 * RF.clank(u, 0.25) + 2.0 * jolt)
        hang(p, u, amp=0.03 + 0.05 * jolt, n=4, cw_amp=0.05, weight=(u < snap))
        if u >= snap:
            off, tl_ = RF.flight((u - snap) * DUR_D, vel, BCT[2], zland)
            k = min((u - snap) * DUR_D / tl_, 1.0)
            cur = p.pt('arm', BCT)
            Ra = p.rotw('arm')
            R.turn(p, Ra.T @ rx(-75.0 * k) @ ry(40.0 * k), pivot=BCT, bone='cw', d=Ra.T @ (BCT + off - cur))
    C.clip('Death', DUR_D, RF.from_pose(C, death, idle0, 0.04, 0.0), floor='body', settle=(0.62, 0.72))

    def spawn(p, u):                        # shoulders its way up out of the floor, shudders, and tries its lid once
        d = 1 - sm(u, 0.0, 0.7)
        sh = bump(u, 0.05, 0.2, 0.6, 0.72)
        R.body(p, dz=-2.6 * d + 0.012 * sh * abs(osc(u, 23)), roll=0.6 * sh * osc(u, 9), pitch=0.5 * sh * osc(u, 7, 0.2))
        lid(p, 22.0 * bump(u, 0.74, 0.82, 0.86, 0.9) + 2.0 * RF.clank(u, 0.9))
        hang(p, u, amp=0.03 + 0.04 * sh, n=4, sub=True)
    C.clip('Spawn', 1.6, RF.from_pose(C, spawn, idle0, 0.0, 0.06), under_ok=True)
    return C.finalize(bake=bake, notes="turret (Walk / Run = Idle, speeds 0); rest pose and Idle = shut (the glowing hollow is hidden behind the lid); "
                                       "bone `arm` opens the lid with a negative x rotation (74 deg in Attack1); Spawn rises out of the floor (under_ok); "
                                       "Death: lid shut, counterweight on the ground, stump tilted 9 deg")
