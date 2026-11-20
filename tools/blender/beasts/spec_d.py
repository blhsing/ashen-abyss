"""Rig D creatures (serpents), part 1: the column proof - the mud-eel. Loopers, the other columns and the rearers
are in spec_d2.py."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis, euler
import bparts as P
import rig_d as RD

_c = L._c
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


def k_slick(N, p):
    """slick eel / catfish skin: big mottled blotches and dark spots, a pale belly on the side that faces `front`
    (object space), a mucus sheen broken only by small matte patches; no rings"""
    q = dict(p)
    q['ring'] = 0.0
    belly = q.pop('belly', (0.4, 0.35, 0.22))
    r = L.KINDS['slime'](N, q)
    f = _c(p, 'front', (0.0, -1.0, 0.0))
    nx, ny, nz = N.sep(N.geo().outputs['Normal'])
    d = N.math('ADD', N.math('ADD', N.math('MULTIPLY', nx, f[0]), N.math('MULTIPLY', ny, f[1])), N.math('MULTIPLY', nz, f[2]))
    bm = N.math('MULTIPLY', N.mapr(d, 0.2, 0.8), N.mapr(N.noise(6.0, 3, 0.6, vec=N.mapping(N.coord(), loc=(1.5, 4.5, 2.5))), 0.25, 0.65, 0.55, 1.0))
    spot = N.mapr(N.voro(_c(p, 'spot', 13.0), 'SMOOTH_F1', vec=N.warp(0.09, 4.0), smooth=0.35), 0.30, 0.10)
    spot = N.math('MULTIPLY', spot, N.mapr(N.noise(3.0, 2, 0.5, vec=N.mapping(N.coord(), loc=(7.0, 2.0, 5.0))), 0.35, 0.6))
    c = N.mix(N.math('MULTIPLY', spot, 0.8), r['color'], _c(p, 'spot_color', (0.012, 0.014, 0.012)))
    c = N.mix(bm, c, N.mix(N.math('MULTIPLY', spot, 0.35), belly, N.mul(belly, 0.5)))
    r['color'] = c
    r['rough'] = N.mixf(N.math('MULTIPLY', bm, 0.5), r['rough'], _c(p, 'rough', 0.14) + 0.12)
    return r


L.KINDS.update(slick=k_slick)
L.GRIME.update(slick=((0.02, 0.016, 0.012), 0.5))


# ================================================================================================ lurk_eel
@reg
def lurk_eel(bake=True):
    """Mud-eel: a thick serpent rearing out of the mud in an S-bend - wide flat armoured spade skull, a gaping maw of
    needle teeth whose lower jaw opens as two plates, clouded eyes, barbels, gill slits, a row of torn fin pennants"""
    S = 1.2
    X = V(1.0, 0, 0)
    yz = [(-0.04, -0.42), (-0.03, -0.20), (0.0, -0.01), (0.07, 0.25), (0.115, 0.50), (0.08, 0.75), (-0.03, 0.97), (-0.19, 1.15), (-0.37, 1.27),
          (-0.55, 1.32)]
    pts = [V(0, y, z) * S for y, z in yz]
    hj = pts[-1]
    hd = unit(V(0, -0.95, -0.31))
    upv = unit(np.cross(hd, X))
    if upv[2] < 0:
        upv = -upv
    tip = hj + hd * 0.52 * S
    Rh = np.stack([X, -hd, upv], axis=1)                      # head frame: local +Y back, +Z up

    def hp(f, x=0.0, u=0.0):
        """point in the head frame: f forward, x sideways, u up (metres before scaling)"""
        return hj + (hd * f + X * x + upv * u) * S
    JAW = {}
    BARB = {}
    for s, sg in (('l', 1.0), ('r', -1.0)):
        JAW[s] = (hp(0.04, sg * 0.17, -0.055), hp(0.50, sg * 0.035, -0.10))
        b0 = hp(0.44, sg * 0.205, -0.015)
        b1 = b0 + unit(hd * 0.6 - upv * 0.55 + X * sg * 0.55) * 0.19 * S
        b2 = b1 + unit(hd * 0.25 - upv * 0.95 + X * sg * 0.2) * 0.19 * S
        BARB[s] = (b0, b1, b2)

    def extra(sk, names):
        for s in 'lr':
            sk.bone('jaw_' + s, JAW[s][0], JAW[s][1], 'head')
            sk.bone('barbel_%s1' % s, BARB[s][0], BARB[s][1], 'head')
            sk.bone('barbel_%s2' % s, BARB[s][1], BARB[s][2], 'barbel_%s1' % s)
        sk.bone('rip1', (0, 0, 0), (0, 0.1, 0), 'root')
        sk.bone('rip2', (0, 0, 0), (0, 0.1, 0), 'root')
    C = B.Beast('mon_lurk_eel', RD.column_skel(pts, tip, extra))
    C.meta.update(family='lurk', rig='D', scale=1.0, game_speed=3.0, buried=round(0.42 * S, 2), radius=0.45)
    K = RD.Column(C)
    C.look('skin', 'slick', 'skin_body', color=(0.13, 0.115, 0.06), color2=(0.035, 0.04, 0.03), belly=(0.50, 0.43, 0.26), scale=3.2, wart=38.0,
           wart_h=0.8, rough=0.13, dry=0.18)
    C.look('fin', 'membrane', 'skin_body', color=(0.10, 0.07, 0.045), vein=(0.03, 0.018, 0.012), vein_scale=14.0, rough=0.35)
    C.look('maw', 'slime', 'skin_body', color=(0.42, 0.10, 0.08), color2=(0.16, 0.03, 0.03), ring=0.0, rough=0.1, wart=90.0, dry=0.0)
    C.look('gill', 'flat', 'skin_body', color=(0.07, 0.012, 0.01), rough=0.25)
    C.look('armour', 'chitin', 'bone_head', color=(0.055, 0.05, 0.038), color2=(0.13, 0.11, 0.075), edge_color=(0.36, 0.31, 0.2), rough=0.4,
           pit=80.0, ridge=34.0, ridge_h=1.6, dust=0.3, dust_color=(0.15, 0.13, 0.09), axis=tuple(hd))
    C.look('teeth', 'bone', 'bone_head', color=(0.80, 0.76, 0.6), stain=(0.36, 0.26, 0.14), dirt_lo=-2.0, dirt_hi=-1.5, crack_amt=0.2)
    C.look('eye', 'emis', 'emis_eyes', emit=(0.50, 0.66, 0.42), strength=1.0, mottle=0.9, base_mul=0.8, scale=90.0)
    C.look('mud', 'mud', 'rock_mound', color=(0.06, 0.047, 0.033), dry=(0.10, 0.082, 0.058), scale=3.0)
    seg = K.seg
    # ---- body: thick at the mud, tapering to the neck, lumpy and slick
    rf = [x * S for x in (0.20, 0.20, 0.20, 0.195, 0.184, 0.17, 0.155, 0.14, 0.128, 0.118, 0.105)]
    rs = [x * 1.09 for x in rf]
    body = RD.chain_tube(pts + [hp(0.12)], rf, rs, bones=seg + ['head'], m=58, n=16)
    body.rough(0.013 * S, 4.0, seed=2)
    C.skin(body, 'skin')
    bv = P.bvh_of(body)
    # ---- dorsal fin: torn pennants down the whole back
    bl = back_line(pts, rf[:-1], off=-0.016, per=4, z0=0.16)
    rng = np.random.default_rng(11)
    saw = (0.04, 0.09, 0.14, 0.18, 0.21)                     # each pennant: tall at its upper corner, torn away below
    rows = 4
    G = np.zeros((len(bl), rows + 1, 3))
    for i, (q, nr) in enumerate(bl):
        hgt = saw[i % 5] * S * rng.uniform(0.75, 1.2) * (0.7 + 0.3 * math.sin(i * 0.37))
        if rng.random() < 0.14:
            hgt *= 0.4                                        # a tear
        tl = unit(bl[min(i + 1, len(bl) - 1)][0] - bl[max(i - 1, 0)][0])
        for j in range(rows + 1):
            t = j / float(rows)
            G[i, j] = (q - nr * 0.02 * (1 - t) + nr * hgt * t - tl * hgt * 0.35 * t * t + V(0, 0, -1) * hgt * 0.45 * t * t
                       + rng.normal(0, 0.004, 3) * t)
    fin = P.sheet_solid(G, 0.008)
    C.skin(RD.skin_to(fin, seg + ['head'], sharp=4.0), 'fin', smooth=180)
    # ---- skull: a wide flat armoured spade with a flat palate
    st = (-0.09, 0.02, 0.16, 0.32, 0.45, 0.535)
    skull = P.tubep([hp(t, 0, 0.03) for t in st], [x * S for x in (0.035, 0.09, 0.10, 0.08, 0.052, 0.016)],
                    [x * S for x in (0.07, 0.18, 0.255, 0.275, 0.215, 0.07)], m=18, n=18, up=tuple(upv), power=2.6)
    dv = (skull.v - hj) @ upv
    low = dv < 0.0
    skull.v[low] -= np.outer(dv[low], upv)
    for sg in (1, -1):
        skull.dent(hp(0.31, sg * 0.175, 0.11), 0.075 * S, 0.02 * S)          # brow bosses over the eyes
    skull.dent(hp(0.2, 0, 0.14), 0.12 * S, 0.012 * S)                        # low keel
    C.rigid(skull, 'head', 'armour', smooth=45)
    sb = P.bvh_of(skull)
    C.rigid(P.blob(hp(0.24, 0, -0.002), 0.215 * S, 0.25 * S, 0.03 * S, n=14, m=5, rot=Rh), 'head', 'maw', smooth=180)      # palate
    C.rigid(P.blob(hp(0.19, 0, -0.085), 0.125 * S, 0.21 * S, 0.06 * S, n=12, m=6, rot=Rh), 'head', 'maw', smooth=180)      # throat pouch
    up_t = []
    for a in np.linspace(-118, 118, 19):
        ar = math.radians(a)
        q = hp(0.25 + 0.275 * math.cos(ar), 0.262 * math.sin(ar), 0.004)
        d = unit(-upv + (hd * math.cos(ar) + X * math.sin(ar)) * 0.2)
        ln = (0.065 + 0.055 * math.cos(ar / 2) ** 2) * S * rng.uniform(0.75, 1.15)
        up_t.append(P.seated(sb, q, d, ln, 0.0115 * S, sink=0.35, n=4))
    C.rigid(M.join(up_t), 'head', 'teeth', smooth=50)
    # ---- lower jaw: two plates that open down and apart, needle teeth, a chin barbel each
    for s, sg in (('l', 1.0), ('r', -1.0)):
        a, b = JAW[s]
        a0 = a - hd * 0.04 * S
        jpath = [a0, M.lerp(a, b, 0.35) + X * sg * 0.085 * S, M.lerp(a, b, 0.75) + X * sg * 0.055 * S, b]
        pl = P.tubep(jpath, [x * S for x in (0.03, 0.034, 0.028, 0.012)], [x * S for x in (0.05, 0.062, 0.05, 0.022)], m=10, n=10,
                     up=tuple(upv), power=2.4)
        C.rigid(pl, 'jaw_' + s, 'skin', smooth=180)
        pb = P.bvh_of(pl)
        jp = M.polyline(np.array(jpath), 24)
        lo_t = []
        for t in np.linspace(0.3, 0.97, 8):
            q = jp[int(round(t * 23))] + upv * 0.02 * S + X * sg * 0.02 * S
            lo_t.append(P.seated(pb, q, unit(upv + X * sg * 0.14 + hd * 0.1), rng.uniform(0.05, 0.09) * S, 0.0105 * S, sink=0.4, n=4))
        lo_t.append(P.cone(b - upv * 0.005 * S, unit(-upv * 0.8 + hd * 0.5), 0.17 * S, 0.012 * S, n=5, curl=(0, 0.6, -0.6), seg=3))   # chin barbel
        C.rigid(M.join(lo_t), 'jaw_' + s, 'teeth', smooth=50)
        # clouded eye, upper barbel
        C.rigid(P.blob(hp(0.33, sg * 0.185, 0.105), 0.03 * S, 0.03 * S, 0.026 * S, n=9, m=5), 'head', 'eye', smooth=180)
        b0, b1, b2 = BARB[s]
        bt = P.tubep([b0 - unit(b1 - b0) * 0.03 * S, b0, b1, b2], [x * S for x in (0.017, 0.015, 0.009, 0.003)], m=10, n=6)
        C.skin(B.wbones(bt, ['barbel_%s1' % s, 'barbel_%s2' % s], sharp=3.0), 'skin', smooth=180)
    # ---- gill slits behind the head
    gs = []
    for sg in (1, -1):
        for k in range(4):
            c = M.lerp(pts[-3], pts[-1], 0.22 + 0.2 * k)
            q, nr = P.surf(bv, c + X * sg * 0.8 + V(0, 0, 0.0), (-sg, 0, 0), 1.5)
            if q is None:
                continue
            ta = unit(pts[-1] - pts[-3])
            ci = unit(np.cross(nr, ta))
            gs.append(P.blob(q + nr * 0.003, 0.013 * S, 0.06 * S, 0.005 * S, n=8, m=4, rot=np.stack([ta, ci, nr], axis=1)))
    if gs:
        C.skin(RD.skin_to(M.join(gs), seg + ['head'], sharp=3.0), 'gill', smooth=180)
    # ---- the mud it rears from: a churned lip round the hole, clods, two ripple rings
    def mud_ring(R, r, zs, lobes, seed, n=26):
        """an irregular swell of wet mud round the hole (never a clean hoop)"""
        q = P.ring((0, 0, 0.0), R, r, n=n, m=6)
        rg2 = np.random.default_rng(seed)
        ang = np.arctan2(q.v[:, 1], q.v[:, 0])
        k = 1 + 0.16 * np.sin(ang * lobes + seed) + 0.1 * np.sin(ang * (lobes * 2 + 1) + 2.0 * seed)
        q.v[:, 0] *= k; q.v[:, 1] *= k
        q.v[:, 2] *= zs * (0.6 + 0.8 * (0.5 + 0.5 * np.sin(ang * (lobes + 2) + 1.3 * seed)))
        q.rough(r * 0.3, 5.0, seed=seed)
        q.v[:, 2] += r * zs * 0.55
        return q
    C.rigid(mud_ring(0.30 * S, 0.14 * S, 0.34, 3, 6), 'root', 'mud', smooth=180, free=True)
    C.rigid(RD.mound(0.36 * S, 0.5 * S, n=9, seed=3, h=0.06), 'root', 'mud', free=True)
    for i, b in enumerate(('rip1', 'rip2')):
        C.rigid(mud_ring(0.48 * S, 0.075 * S, 0.14, 4 + i, 9 + i, n=30), b, 'mud', smooth=180, free=True)

    # ---- motion helpers
    def jaws(p, a, sp=0.0):
        """a = gape of the lower jaw (degrees down), sp = the two plates spread apart"""
        p.setR('jaw_l', rot_axis(upv, sp) @ rot_axis(X, a))
        p.setR('jaw_r', rot_axis(upv, -sp) @ rot_axis(X, a))

    def barbels(p, u, amp=8.0, n=2, trail=0.0):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            o = 0.17 if sg > 0 else 0.0
            p.rot('barbel_%s1' % s, x=trail * 0.5 + amp * 0.5 * osc(u, n, 0.1 + o), z=sg * amp * 0.6 * osc(u, n, 0.3 + o))
            p.rot('barbel_%s2' % s, x=trail * 0.5 + amp * osc(u, n, -0.05 + o), z=sg * amp * osc(u, n, 0.15 + o))

    def ripple(p, u=0.0, n=1, static=False, fade=1.0):
        """rings of mud running out from the hole: each grows, then sinks back into the ground"""
        for b, o in (('rip1', 0.0), ('rip2', 0.5)):
            ph = o if static else (n * u + o) % 1.0
            p.scl(b, max(0.001, (0.7 + 1.0 * ph) * fade))
            p.move(b, 0, 0, -0.05 * (1 - sm(ph, 0.0, 0.12)) - 0.05 * sm(ph, 0.8, 1.0) - 0.06 * (1 - fade))

    K.idle(dur=4.0, sway=4.0, wave=3.0, nod=3.0, look=14.0,
           extra=lambda p, u: (jaws(p, 12 + 4 * osc(u, 2), 1.5), barbels(p, u, 8, 2), ripple(p, u, 1)))

    def travel(name, v, T, lean, weave, lunge, sink, gape):
        """ploughs through the mud: a lateral wave runs up the body, the head stays level on its course and lunges"""
        def fn(p, u):
            lg = osc(u, 2, 0.1)
            K.lean(p, pitch=lean, power=-0.3)
            K.wave(p, u, weave, n=1, k=0.9, axis='y', grow=0.3)
            K.wave(p, u, weave * 0.3, n=1, k=0.9, ph=0.25, axis='z', grow=0.3)
            K.lean(p, pitch=lunge * lg, lo=K.n - 3)
            K.rise(p, dz=-sink + 0.02 * osc(u, 2, 0.2), dx=0.03 * osc(u, 1, 0.2))
            p.aim_world('head', euler(6 + 5 * lg, 0, 0))
            jaws(p, gape + 8 * (0.5 + 0.5 * lg), 2.0)
            barbels(p, u, 10, 2, trail=lean * 0.8)
            ripple(p, u, 2 if name == 'Run' else 1)
        C.clip(name, T, fn)
        C.meta['walk_speed' if name == 'Walk' else 'run_speed'] = v
    travel('Walk', 1.1, 1.2, 8.0, 8.0, 5.0, 0.05, 10.0)
    travel('Run', 3.0, 0.7, 16.0, 12.0, 9.0, 0.1, 18.0)
    K.lunge(dur=0.9, hit=0.45, back=18.0, fwd=50.0, head=20.0, power=0.6,
            extra=lambda p, u, w, s: (jaws(p, 12 + 50 * bump(u, 0.05, 0.3, 0.46, 0.53), 1.5 + 16 * bump(u, 0.05, 0.3, 0.46, 0.53)),
                                      barbels(p, u, 12, 3, trail=-20 * w + 25 * s), ripple(p, static=True)))
    K.sweep(dur=1.2, hit=0.6, low=60.0, arc=100.0, head=-12.0,
            extra=lambda p, u, dn, sw: (jaws(p, 12 + 30 * dn, 1.5 + 8 * dn), barbels(p, u, 12, 3), ripple(p, static=True)))
    K.hit(extra=lambda p, u, k: (jaws(p, 12 + 32 * k, 1.5 + 8 * k), barbels(p, u, 14, 2), ripple(p, static=True)))
    K.death_topple(radius=0.19 * S, extra=lambda p, u, f: (jaws(p, 12 + 26 * f, 1.5 + 5 * f), barbels(p, u, 6 * (1 - f), 2, trail=20 * f),
                                                           ripple(p, static=True, fade=1 - sm(u, 0.3, 0.6))))
    K.spawn_erupt(extra=lambda p, u, r: (jaws(p, 12 + 40 * (1 - r), 1.5 + 10 * (1 - r)), barbels(p, u, 12, 3), ripple(p, u, 2)))
    return C.finalize(bake=bake, notes="rig D proof (column)")
