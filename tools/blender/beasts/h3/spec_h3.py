"""Rig E creatures built by helper H3: the five flyers after the dusk_bat proof (dusk_crow, dusk_gadfly, rust_locust,
fiend_wing, hush_wing). One function per bestiary type id, registered in BUILD. Shared helpers are at the top.
The hover height is inside the model (meta fly_height / hover_in_model)."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis, euler
import bparts as P
import rig_e as RE

BUILD = {}
TAU = 2 * math.pi
_c = L._c


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


def std_meta(C, family, scale, speed, **kw):
    C.meta.update(family=family, rig='E', scale=scale, game_speed=speed)
    C.meta.update(kw)


def mirror_pts(pts, sg):
    return [V(q) * (sg, 1, 1) for q in pts]


def core(C, part, bone, look, smooth=180):
    """main body as SKINNED geometry on one bone (the audit grows its attachment tree from skinned geometry)"""
    part.W = M.rigid_W(len(part.v), bone)
    return C.skin(part, look, smooth=smooth)


# ================================================================================================ looks
def k_feather(N, p):
    """flight / contour feathers: fine barbs as streaks along `axis` (the direction the feathers lie in), lanes of
    blue-black sheen, patches singed to brown-grey ash (singe 0..1)"""
    col = _c(p, 'color', (0.028, 0.028, 0.034))
    sheen = _c(p, 'sheen', (0.06, 0.07, 0.10))
    ash = _c(p, 'ash', (0.24, 0.21, 0.18))
    axis = _c(p, 'axis', (0, 1, 0))
    r0 = _c(p, 'rough', 0.5)
    barb = N.noise(_c(p, 'scale', 150.0), 2, 0.5, vec=N.align(N.warp(0.012, 8.0), axis, 0.04))
    lane = N.noise(38.0, 3, 0.6, vec=N.align(N.coord(), axis, 0.07))
    big = N.noise(6.0, 3, 0.6)
    c = N.mix(N.mapr(barb, 0.3, 0.72), N.mul(col, 0.45), col)
    c = N.mix(N.mapr(lane, 0.52, 0.8, 0.0, 0.7), c, sheen)
    c = N.mix(N.mapr(big, 0.4, 0.75, 0.0, 0.4), c, N.mul(c, 0.5))
    sg = N.noise(_c(p, 'singe_scale', 7.0), 4, 0.7, vec=N.mapping(N.coord(), loc=(3.7, 1.9, 5.3)))
    sgm = N.math('MULTIPLY', N.mapr(sg, 0.5, 0.7), _c(p, 'singe', 0.6))
    c = N.mix(sgm, c, N.mix(N.mapr(barb, 0.3, 0.7), ash, N.mul(ash, 0.45)))
    rough = N.math('ADD', N.mapr(lane, 0.3, 0.75, r0 - 0.12, r0 + 0.18), N.math('MULTIPLY', sgm, 0.3))
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.96), 0.2)
    H = [(barb, 0.9), (lane, 1.6), (sgm, -0.5), (N.noise(18, 2, 0.5), 1.0)]
    return L._out(N, p, c, rough, 0.0, H, 0.5)


def k_iwing(N, p):
    """insect wing: a dry pale membrane with long veins running along `axis` (root to tip), a faint net of cross
    veins, smoky stains"""
    col = _c(p, 'color', (0.40, 0.38, 0.32))
    vein = _c(p, 'vein', (0.10, 0.08, 0.06))
    axis = _c(p, 'axis', (1, 0.3, 0))
    # long narrow cells lying along the axis: their borders are the long veins and the few cross veins
    ln = N.voro(_c(p, 'vein_scale', 30.0), 'DISTANCE_TO_EDGE', vec=N.align(N.warp(0.008, 6.0), axis, 0.13))
    lv = N.mapr(ln, 0.0, 0.05, 1.0, 0.0)
    cv = N.voro(_c(p, 'cell', 75.0), 'DISTANCE_TO_EDGE', vec=N.align(N.warp(0.01, 9), axis, 0.4))
    cross = N.math('MULTIPLY', N.mapr(cv, 0.0, 0.05, 1.0, 0.0), 0.22)
    big = N.noise(5.0, 3, 0.6)
    hide = N.scan('hide', size=0.3, relief=0.12, res=512)
    c = N.mix(N.mapr(big, 0.35, 0.7), col, N.mul(col, 0.68))
    c = N.detail(c, hide['det'], 0.5)
    vm = N.math('MAXIMUM', lv, cross)
    c = N.mix(N.math('MULTIPLY', vm, 0.75), c, vein)
    r0 = _c(p, 'rough', 0.55)
    rough = N.math('ADD', N.mapr(big, 0.3, 0.7, r0 - 0.1, r0 + 0.12), N.math('MULTIPLY', vm, 0.1))
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.95), 0.2)
    H = [(hide['h'], 1.0), (lv, 0.6), (cross, 0.4), (N.noise(30, 2, 0.5), 0.6)]
    return L._out(N, p, c, rough, 0.0, H, 0.3)


def k_slag(N, p):
    """thin black slag-glass whose cracks leak the fire inside; the cracks are denser and brighter towards the leading
    edge of the wing. lead = [(|x|, y), ...]: the leading edge in object space (from the body outwards); fall = metres
    behind it over which the glow fades to `far` (0..1)"""
    r = L.KINDS['glass'](N, p)
    e = _c(p, 'emit', (1.0, 0.36, 0.05))
    cw = _c(p, 'crack_w', 0.03)
    lead = p['lead']
    xm = float(lead[-1][0])
    ys = [float(q[1]) for q in lead]
    y0, y1 = min(ys), max(ys)
    x, y, z = N.sep(N.coord())
    ly = N.ramp(N.math('MULTIPLY', N.math('ABSOLUTE', x), 1.0 / xm), [(float(q[0]) / xm, (float(q[1]) - y0) / max(y1 - y0, 1e-6)) for q in lead])
    d = N.math('SUBTRACT', y, N.math('ADD', N.math('MULTIPLY', ly, y1 - y0), y0))        # metres behind the leading edge
    near = N.mapr(d, 0.0, _c(p, 'fall', 0.5), 1.0, _c(p, 'far', 0.22))
    cr = N.voro(_c(p, 'crack', 12.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.07, 6))
    line = N.mapr(cr, 0.0, cw, 1.0, 0.0)
    halo = N.math('POWER', N.mapr(cr, 0.0, cw * 5, 1.0, 0.0), 2.0)
    cr2 = N.voro(_c(p, 'crack', 12.0) * 2.6, 'DISTANCE_TO_EDGE', vec=N.warp(0.05, 11))
    fine = N.math('MULTIPLY', N.mapr(cr2, 0.0, cw * 0.7, 1.0, 0.0), N.mapr(d, 0.3, 0.04))
    band = N.math('MULTIPLY', N.mapr(d, 0.09, 0.0), 0.55)
    patch = N.mapr(N.noise(_c(p, 'patch', 3.2), 3, 0.6, vec=N.mapping(N.coord(), loc=(3.3, 1.7, 6.1))), 0.34, 0.6, _c(p, 'patch_lo', 0.3), 1.0)
    em = N.math('ADD', N.math('ADD', line, N.math('MULTIPLY', halo, _c(p, 'halo', 0.3))), N.math('MULTIPLY', fine, 0.8))
    em = N.math('MULTIPLY', N.math('MULTIPLY', em, near), patch)
    em = N.math('MULTIPLY', N.math('ADD', em, band), _c(p, 'gain', 1.0), clamp=True)
    r['color'] = N.mix(N.math('MULTIPLY', em, 0.7), r['color'], tuple(v * 0.6 for v in e))
    r['emit'] = (e, _c(p, 'strength', 1.4))
    r['emask'] = em
    return r


L.KINDS.update(feather=k_feather, iwing=k_iwing, slag=k_slag)
L.GRIME.update(feather=((0.015, 0.013, 0.012), 0.7), iwing=((0.05, 0.04, 0.03), 0.4), slag=((0.02, 0.012, 0.008), 0.4))


# ================================================================================================ parts
def feather(base, tip, w, th=0.004, up=(0, 0, 1), nu=6, camber=0.22, bow=0.0, droop=0.0, quill=0.3, tipw=0.3, rag=None):
    """one feather as a closed thin slab from `base` to `tip`, `w` wide at its widest, its top facing `up`.
    camber = arch across the blade (x half width), bow = sideways curve (m), droop = the tip sags (m),
    quill / tipw = relative width at the root / the tip, rag = per-station multipliers of the width (burnt notches)"""
    base = V(base); tip = V(tip)
    ax = tip - base
    Ln = float(np.linalg.norm(ax))
    ax = ax / Ln
    upv = V(up)
    upv = unit(upv - ax * float(upv @ ax))
    side = np.cross(ax, upv)
    G = np.zeros((nu, 3, 3))
    for i in range(nu):
        t = i / (nu - 1)
        pr = quill + (1 - quill) * sm(t, 0.0, 0.3)
        pr *= 1.0 - (1.0 - tipw) * sm(t, 0.68, 1.0)
        if rag is not None:
            pr *= rag[i]
        hw = 0.5 * w * pr
        c = base + ax * (Ln * t) + side * (bow * math.sin(math.pi * t)) - upv * (droop * t * t)
        G[i, 0] = c - side * hw
        G[i, 1] = c + upv * (camber * hw)
        G[i, 2] = c + side * hw
    return P.sheet_solid(G, th)


def rag_profile(rng, nu, amount):
    """width multipliers of a singed feather: random bites out of the vane, worse towards the tip"""
    r = np.ones(nu)
    for i in range(1, nu):
        t = i / (nu - 1)
        r[i] = 1.0 - amount * t * rng.uniform(0.0, 1.0)
    return r


# ================================================================================================ dusk_crow
@reg
def dusk_crow(bake=True):
    """Ember-pecker: ragged oversized crow, heavy pick-axe beak, bare scorched head and throat, wing feathers singed
    to uneven fingers, a stolen ember in its beak (the only emissive point)"""
    h = 1.6
    W = [(0.09, -0.07, h + 0.05), (0.34, -0.03, h + 0.09), (0.56, -0.14, h + 0.07), (0.79, -0.04, h + 0.03)]
    beak_tip = V(0, -0.535, h + 0.005)
    sk = RE.flyer_skel(body=((0, 0.17, h), (0, -0.13, h + 0.025)), head=((0, -0.17, h + 0.06), (0, -0.33, h + 0.065)),
                       jaw=((0, -0.262, h + 0.04), (0, -0.50, h + 0.0)), wing=W,
                       tail=[(0, 0.17, h - 0.005), (0, 0.34, h - 0.03)], legs=((0.05, 0.08, h - 0.07), (0.065, 0.21, h - 0.20)))
    C = B.Beast('mon_dusk_crow', sk)
    std_meta(C, 'dusk', 1.0, 6.5)
    F = RE.Flyer(C, h)
    C.look('plume', 'bfur', 'hair_feather', color=(0.06, 0.06, 0.075), tip=(0.20, 0.20, 0.25), flow=(0, 1, -0.15), ash=0.5, scale=70.0, lock=34.0)
    C.look('quill', 'feather', 'hair_feather', axis=(0, 1, -0.05), singe=0.6, color=(0.05, 0.05, 0.064), sheen=(0.12, 0.14, 0.21),
           ash=(0.34, 0.30, 0.26))
    # the singed primaries still smoulder at their tips (the lead's review: one bright accent, readable on dark ground)
    for s_, sg_ in (('l', 1.0), ('r', -1.0)):
        C.look('prim_' + s_, 'tipglow', 'hair_feather', base='feather', axis=(sg_, 0.75, 0), singe=0.85, color=(0.05, 0.05, 0.064),
               sheen=(0.12, 0.14, 0.21), ash=(0.36, 0.31, 0.26), center=(0.0, 0.0, h), r0=0.62, r1=1.05, emit=(1.0, 0.36, 0.05),
               strength=1.2, gain=0.9)
    C.look('bare', 'skin', 'skin_head', color=(0.24, 0.105, 0.085), blotch=(0.065, 0.04, 0.035), vein=(0.05, 0.02, 0.02), rough=0.45,
           sore_amt=0.45, sore=(0.30, 0.07, 0.03), scar=0.6, wrinkle=1.5, dry=0.9)
    C.look('shank', 'scales', 'skin_head', color=(0.075, 0.07, 0.065), scale=110.0, mud_hi=-1.0, mud_lo=-2.0)
    C.look('beak', 'bone', 'bone_beak', color=(0.13, 0.115, 0.10), stain=(0.03, 0.026, 0.024), blotch=5.0, crack_amt=0.5, dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('eye', 'bone', 'bone_beak', color=(0.50, 0.48, 0.42), stain=(0.25, 0.22, 0.17), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('ember', 'emis', 'emis_ember', emit=(1.0, 0.40, 0.07), strength=1.6, mottle=0.5, base_mul=0.55, scale=60.0, crust=0.3)
    # ---- feathered body (one skin on the body bone = the audit root)
    torso = P.blob((0, 0.03, h), 0.098, 0.19, 0.10, n=14, m=8)
    chest = P.blob((0, -0.07, h - 0.008), 0.105, 0.11, 0.108, n=12, m=7)
    rump = P.tubep([(0, 0.12, h), (0, 0.22, h - 0.005), (0, 0.275, h - 0.012)], [0.07, 0.045, 0.02], [0.075, 0.052, 0.03], m=6, n=10)
    for q in (torso, chest, rump):
        B.wbones(q, ['body'])
    body = P.organic([torso, chest, rump], voxel=0.009, tris=2000, smooth=6)
    C.skin(body, 'plume')
    bvh = P.bvh_of(body)
    tf = P.tufts(body, 110, length=(0.05, 0.10), radius=(0.012, 0.022), seed=4, flow=(0, 1, -0.15), lay=0.88, sides=3, jitter=0.22,
                 mask=lambda c, n: c[:, 1] > -0.08)
    C.skin(tf, 'plume', smooth=180)
    ruff = P.tufts(body, 60, length=(0.05, 0.09), radius=(0.011, 0.02), seed=9, flow=(0, 1, 0.1), lay=0.55, sides=3, jitter=0.35,
                   mask=lambda c, n: (c[:, 1] < -0.07) & (n[:, 1] < 0.2))
    C.skin(ruff, 'plume', smooth=180)
    # ---- bare scorched neck, throat pouch and head (one skin: body / head)
    neck = P.tubep([(0, -0.085, h + 0.008), (0, -0.17, h + 0.045), (0, -0.235, h + 0.066)], [0.062, 0.05, 0.045], [0.058, 0.047, 0.044], m=7, n=10)
    B.wbones(neck, ['body', 'head'], sharp=3.0)
    skull = P.blob((0, -0.255, h + 0.072), 0.056, 0.08, 0.056, n=12, m=7)
    B.wbones(skull, ['head'])
    pouch = P.blob((0, -0.215, h + 0.022), 0.036, 0.055, 0.034, n=10, m=6)
    B.wbones(pouch, ['head'])
    bare = P.organic([neck, skull, pouch], voxel=0.007, tris=1300, smooth=5)
    C.skin(bare, 'bare')
    hb = P.bvh_of(bare)
    for sg in (1, -1):
        C.rigid(P.blob((sg * 0.047, -0.283, h + 0.088), 0.0105, 0.0105, 0.0105, n=8, m=5), 'head', 'eye', smooth=180)
    # ---- the pick-axe beak (upper on the head, lower on the jaw) and the ember it carries
    up_b = P.tubep([(0, -0.285, h + 0.07), (0, -0.38, h + 0.068), (0, -0.47, h + 0.048), (0, -0.52, h + 0.02), beak_tip],
                   [0.052, 0.048, 0.034, 0.017, 0.004], [0.037, 0.031, 0.021, 0.011, 0.004], m=10, n=8, power=1.35)
    up_b.smooth = 60.0
    C.rigid(up_b, 'head', 'beak')
    lo_b = P.tubep([(0, -0.265, h + 0.03), (0, -0.37, h + 0.02), (0, -0.46, h + 0.008), (0, -0.505, h - 0.004)],
                   [0.022, 0.02, 0.013, 0.004], [0.031, 0.026, 0.016, 0.004], m=8, n=8, power=1.5)
    lo_b.smooth = 60.0
    C.rigid(lo_b, 'jaw', 'beak')
    emb = M.rock(0.03, 0.034, 0.027, seed=3, n=18, jit=0.18, flat=False).move(0, -0.482, h + 0.018)
    emb.smooth = 40.0
    C.rigid(emb, 'jaw', 'ember')
    # ---- wings: a rigid feathered arm on every wing bone carrying its flight feathers and coverts
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        rng = np.random.default_rng(21 if sg > 0 else 37)
        Wp = mirror_pts(W, sg)
        sh, el, wr, tp = Wp
        prim_look = 'prim_' + s
        arms = [(sh, el, 0.05, [0.03, 0.022], [0.042, 0.032]), (el, wr, 0.014, [0.022, 0.017], [0.032, 0.024]), (wr, tp, 0.012, [0.017, 0.008], [0.024, 0.012])]
        for i, (a, b, back, rv, rh) in enumerate(arms):
            d = unit(b - a)
            C.rigid(P.tubep([a - d * back, b + d * 0.008], rv, rh, m=5, n=8), 'wing_%s%d' % (s, i + 1), 'plume', smooth=180)

        def row(a, b, ts, dirs, lens, w, z, tilt, ragamt, nu=6, **kw):
            out = []
            for t, dv, Ln in zip(ts, dirs, lens):
                dv = unit(dv)
                root = a + (b - a) * t - dv * 0.018 + V(0, 0, z)
                side = unit(np.cross(dv, (0, 0, 1)))
                upv = unit(V(0, 0, 1) + side * tilt * sg)
                out.append(feather(root, root + dv * (Ln + 0.018), w, up=upv, nu=nu, rag=rag_profile(rng, nu, ragamt), **kw))
            return M.join(out)
        # inner flight feathers (wing 1), secondaries (wing 2): trail straight back, fanning a little outwards
        ts = [0.2, 0.4, 0.6, 0.8, 0.97]
        C.rigid(row(sh, el, ts, [(sg * (0.05 + 0.1 * t), 1, -0.07) for t in ts], [0.37 + 0.05 * t + rng.uniform(-0.03, 0.02) for t in ts], 0.15, -0.006, 0.14, 0.22,
                    droop=0.012, quill=0.55, tipw=0.55), 'wing_%s1' % s, 'quill', smooth=50)
        ts = [0.1, 0.27, 0.44, 0.61, 0.78, 0.95]
        C.rigid(row(el, wr, ts, [(sg * (0.14 + 0.3 * t), 1, -0.06) for t in ts], [0.43 + rng.uniform(-0.06, 0.02) for t in ts], 0.135, -0.006, 0.14, 0.3,
                    droop=0.012, quill=0.55, tipw=0.5), 'wing_%s2' % s, 'quill', smooth=50)
        # primaries (wing 3): narrow, spread like fingers and burnt to uneven lengths
        n3 = 7
        ts = [0.02 + 0.93 * k / (n3 - 1) for k in range(n3)]
        angs = [math.radians(26 + 60 * (k / (n3 - 1)) ** 0.9) for k in range(n3)]
        burn = [1.0, 0.72, 0.95, 0.55, 0.9, 0.62, 0.8] if sg > 0 else [0.9, 1.0, 0.6, 0.85, 0.5, 0.95, 0.7]
        C.rigid(row(wr, tp, ts, [(sg * math.sin(a), math.cos(a), -0.05) for a in angs], [(0.50 - 0.06 * k / (n3 - 1)) * burn[k] for k in range(n3)], 0.088, -0.006,
                    0.2, 0.6, nu=7, tipw=0.22, quill=0.5, droop=0.02), 'wing_%s3' % s, prim_look, smooth=50)
        # coverts: a shorter, overlapping row on top
        ts = [0.15, 0.45, 0.75, 0.98]
        C.rigid(row(sh, el, ts, [(sg * 0.08, 1, -0.03)] * 4, [0.22, 0.25, 0.25, 0.23], 0.16, 0.009, 0.1, 0.3, nu=6, tipw=0.4, quill=0.7), 'wing_%s1' % s, 'quill', smooth=50)
        ts = [0.08, 0.3, 0.52, 0.74, 0.96]
        C.rigid(row(el, wr, ts, [(sg * (0.12 + 0.25 * t), 1, -0.03) for t in ts], [0.25, 0.22, 0.26, 0.23, 0.24], 0.14, 0.009, 0.1, 0.3, nu=6, tipw=0.4, quill=0.7),
                'wing_%s2' % s, 'quill', smooth=50)
        ts = [0.1, 0.4, 0.7]
        C.rigid(row(wr, tp, ts, [(sg * math.sin(math.radians(35 + 40 * t)), math.cos(math.radians(35 + 40 * t)), -0.03) for t in ts], [0.21, 0.19, 0.15], 0.10, 0.009,
                    0.1, 0.3, nu=6, tipw=0.35, quill=0.7), 'wing_%s3' % s, prim_look, smooth=50)
        # the skin of the wing under the feather roots (closes the gaps between the quills)
        lead = [sh + V(-sg * 0.04, 0.0, -0.012), el + V(0, 0, -0.01), wr + V(0, 0, -0.008), tp + V(0, 0, -0.004)]
        trail = [V(sg * 0.07, 0.13, h + 0.01), el + V(sg * 0.02, 0.2, -0.03), wr + V(sg * 0.06, 0.18, -0.025), tp + V(sg * 0.02, 0.04, -0.006)]
        pat = RE.membrane(lead, trail, nu=14, nv=4, camber=0.012, th=0.005)
        B.wbones(pat, ['body', 'wing_%s1' % s, 'wing_%s2' % s, 'wing_%s3' % s], sharp=3.0, bias={'body': 0.6})
        C.skin(pat, 'plume', smooth=180)
        # legs: thin shanks, hooked toes
        hp = V(sg * 0.05, 0.08, h - 0.07); ft = V(sg * 0.065, 0.21, h - 0.20)
        leg = [P.tubep([hp - unit(ft - hp) * 0.04, ft], [0.02, 0.009], m=5, n=6)]
        for k in (-1, 0, 1):
            leg.append(P.cone(ft - V(0, 0.004, 0), (k * 0.5, 0.45, -1), 0.05, 0.007, n=4, curl=(0, -1, 0.2), seg=3))
        leg.append(P.cone(ft, (0, -0.8, -0.6), 0.035, 0.006, n=4, curl=(0, 0.6, -0.6), seg=2))
        C.rigid(M.join(leg), 'leg_' + s, 'shank', smooth=180)
    # ---- tail fan (burnt short on one side)
    tb = V(0, 0.15, h - 0.008)
    rng = np.random.default_rng(5)
    fan = []
    lens = [0.28, 0.37, 0.33, 0.41, 0.40, 0.29, 0.35]
    for k in range(7):
        a = math.radians(-33 + 11 * k)
        dv = unit(V(math.sin(a), math.cos(a), -0.12))
        side = unit(np.cross(dv, (0, 0, 1)))
        fan.append(feather(tb + V(math.sin(a) * 0.03, 0, -0.002 * abs(k - 3)), tb + dv * lens[k], 0.10, up=unit(V(0, 0, 1) + side * (0.14 if k >= 3 else -0.14)),
                           nu=6, rag=rag_profile(rng, 6, 0.4), tipw=0.45, droop=0.01))
    C.rigid(M.join(fan), 'tail1', 'quill', smooth=50)

    # ---- clips
    fk = dict(amp=(42.0, 22.0, 16.0), lag=(0.0, 0.09, 0.18), bias=(8.0, 0.0, -6.0), fold=(0.0, 10.0, 26.0), twist=10.0)

    def beak(p, a):
        F.jaw(p, a)
    F.hover('Idle', beats=6, T=0.4, bob=0.05, pitch=-9.0, flap_kw=fk, tail=5.0, legs=24.0, look=14.0,
            extra=lambda p, u, ph: beak(p, 3 + 2 * osc(u, 3)))
    F.fly('Walk', 2.3, beats=2, T=0.4, pitch=4.0, flap_kw=fk, legs=42.0, extra=lambda p, u, ph: beak(p, 3))
    F.fly('Run', 6.5, beats=2, T=0.3, pitch=17.0, bob=0.03, flap_kw=dict(fk, amp=(48.0, 24.0, 18.0)), legs=62.0, extra=lambda p, u, ph: beak(p, 2))

    def peck(p, u, w, s):
        hu = 0.5
        st = bump(u, hu - 0.07, hu, hu + 0.05, hu + 0.2)          # the stab itself
        p.rot('head', x=-16 * w + 34 * st)
        p.move('head', 0, -0.05 * st, 0)
    F.swoop(dur=1.0, hit=0.5, fwd=0.85, drop=0.75, pitch=34.0, beats=3, gape=6.0, flap_kw=fk, extra=peck)
    F.reach('Attack1', 'head', beak_tip)

    def caw(p, u, w, s):
        p.rot('head', x=-10 * s + 5 * s * osc(u, 9))
        p.rot('tail1', x=-18 * s)
    F.screech(dur=1.3, hit=0.6, beats=4, gape=30.0, shake=2.5, flap_kw=fk, extra=caw)
    F.reach('Attack2', 'head', beak_tip)
    F.hit(flap_kw=fk)

    def kick(p, u, t, on):
        k = bump(u, 0.66, 0.70, 0.74, 0.84)                      # an animal death: it drops and kicks once
        p.rot('leg_l', x=-45 * k)
        p.rot('leg_r', x=-30 * k)
    F.death_fall(dur=1.7, belly=0.10, extra=kick)
    F.spawn_drop(dur=1.2, flap_kw=fk)
    return C.finalize(bake=bake, notes="H3: ragged crow, feather slabs rigid on the wing bones, ember on the jaw bone")


# ================================================================================================ insects (gadfly, locust)
def dangle(p, legs, sw=0.0, sp=0.0, u=0.0, amp=0.0, n=2.0, curl=0.0, lag=0.11):
    """hanging insect legs. legs = [(upper bone, lower bone, side sign, index)]: sw = swing back (degrees, the tips
    trail towards +Y), sp = spread sideways, curl = the lower segment folds inwards, plus a pendulum wave of `amp`
    degrees that lags from pair to pair"""
    for a, b, sg, i in legs:
        ph = -i * lag + (0.07 if sg < 0 else 0.0)
        p.rot(a, x=sw + amp * osc(u, n, ph), y=-sg * sp)
        p.rot(b, x=0.5 * sw + 1.6 * amp * osc(u, n, ph - 0.12), y=sg * curl)


def lathe_on(profile, origin, axis, n=18, m=16):
    """smooth body of revolution: (r, z) profile (r = 0 at both ends) resampled to m rings, its +Z laid along `axis`
    from `origin`"""
    pr = M.polyline([(r, 0.0, z) for r, z in profile], m)
    pts = [(max(float(q[0]), 0.0), float(q[2])) for q in pr]
    pts[0] = (0.0, pts[0][1]); pts[-1] = (0.0, pts[-1][1])
    g = M.revolve(pts, n=n, cap=False)
    g.tf(M.rot_to((0, 0, 1), axis))
    g.move(*origin)
    g.smooth = 180.0
    return g


# ================================================================================================ dusk_gadfly
@reg
def dusk_gadfly(bake=True):
    """Warmth-fly: bloated horsefly the size of a dog, a translucent glowing abdomen like a hanging lamp-globe, long
    straight needle proboscis, legs dangling in a bunch, blurred wings"""
    h = 1.5
    Wg = [V(0.06, -0.015, h + 0.085), V(0.47, 0.17, h + 0.13)]
    ab0 = V(0, 0.10, h - 0.03); abd = unit(V(0, 0.72, -0.69)); ab1 = ab0 + abd * 0.46
    pr0 = V(0, -0.21, h - 0.07); pr1 = V(0, -0.53, h - 0.225)
    prd = unit(pr1 - pr0)
    LEGS = []
    for i, y in enumerate((-0.09, -0.03, 0.03)):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            rt = V(sg * 0.045, y, h - 0.085)
            kn = V(sg * (0.135 - 0.012 * i), y - 0.03 + 0.03 * i, h - 0.215)
            tp = V(sg * 0.06, y + 0.02 * i, h - 0.46 + 0.03 * (i == 1))
            LEGS.append((i, s, sg, rt, kn, tp))

    def extra(sk):
        sk.bone('prob', pr0, pr1, 'head')
        sk.bone('abdomen', ab0, ab1, 'body')
        for i, s, sg, rt, kn, tp in LEGS:
            sk.bone('leg%d_%s_a' % (i, s), rt, kn, 'body')
            sk.bone('leg%d_%s_b' % (i, s), kn, tp, 'leg%d_%s_a' % (i, s))
    sk = RE.flyer_skel(body=((0, 0.11, h), (0, -0.10, h)), head=((0, -0.12, h - 0.012), (0, -0.25, h - 0.03)), wing=Wg, extra=extra)
    C = B.Beast('mon_dusk_gadfly', sk)
    std_meta(C, 'dusk', 1.1, 5.6)
    F = RE.Flyer(C, h)
    C.look('chitin', 'chitin', 'bone_chitin', color=(0.05, 0.042, 0.036), color2=(0.11, 0.09, 0.07), edge_color=(0.22, 0.17, 0.12), rough=0.42, pit=160.0, dust=0.5)
    C.look('eye', 'chitin', 'bone_chitin', color=(0.15, 0.03, 0.02), color2=(0.27, 0.07, 0.03), rough=0.16, pit=260.0, grain=420.0, dust=0.0, edge=0.0, ridge_h=0.0)
    C.look('bristle', 'bfur', 'hair_bristle', color=(0.10, 0.085, 0.065), tip=(0.30, 0.26, 0.20), flow=(0, 1, 0.2), ash=0.3, scale=90.0)
    for s_, sg_ in (('l', 1.0), ('r', -1.0)):
        C.look('wing_' + s_, 'iwing', 'skin_wing', color=(0.42, 0.40, 0.34), vein=(0.13, 0.11, 0.08), axis=(sg_ * 0.9, 0.42, 0.1), rough=0.6)
    C.look('globe', 'glow', 'emis_globe', color=(0.26, 0.25, 0.10), emit=(0.72, 0.90, 0.20), strength=1.3, vein=(0.07, 0.05, 0.02), vein_scale=8.0, vein_w=0.05, scale=5.0)
    # ---- thorax + head (one skin)
    thorax = P.blob((0, 0.0, h), 0.10, 0.135, 0.105, n=14, m=8)
    hump = P.blob((0, 0.025, h + 0.04), 0.08, 0.095, 0.075, n=12, m=7)
    for q in (thorax, hump):
        B.wbones(q, ['body'])
    neck = P.tubep([(0, -0.09, h - 0.008), (0, -0.16, h - 0.016)], [0.045, 0.04], m=4, n=10)
    B.wbones(neck, ['body', 'head'], sharp=3.0)
    skull = P.blob((0, -0.175, h - 0.02), 0.07, 0.065, 0.068, n=12, m=7)
    B.wbones(skull, ['head'])
    body = P.organic([thorax, hump, neck, skull], voxel=0.008, tris=2200, smooth=5)
    C.skin(body, 'chitin')
    bvh = P.bvh_of(body)
    br = P.tufts(body, 150, length=(0.025, 0.055), radius=(0.004, 0.008), seed=6, flow=(0, 1, 0.25), lay=0.5, sides=3, jitter=0.4,
                 mask=lambda c, n: (c[:, 1] > -0.09) & (n[:, 2] > -0.5))
    C.skin(br, 'bristle', smooth=180)
    for sg in (1, -1):
        C.rigid(P.blob((sg * 0.05, -0.19, h + 0.0), 0.044, 0.05, 0.056, n=12, m=7), 'head', 'eye', smooth=180)
        C.rigid(P.seated(bvh, (sg * 0.018, -0.235, h + 0.01), (sg * 0.3, -1, 0.5), 0.05, 0.007, seg=1), 'head', 'chitin', smooth=50)
    # ---- needle proboscis in its sheath
    C.rigid(P.tubep([pr0 - prd * 0.035, pr0 + prd * 0.05, pr0 + prd * 0.10, pr1], [0.024, 0.02, 0.008, 0.0025], m=10, n=7), 'prob', 'chitin', smooth=180)
    # ---- the lamp-globe abdomen under a dark collar
    prof = [(0.0, -0.012), (0.05, 0.0), (0.095, 0.035), (0.15, 0.10), (0.195, 0.18), (0.212, 0.265), (0.20, 0.34), (0.158, 0.41), (0.09, 0.452), (0.0, 0.465)]
    C.rigid(lathe_on(prof, ab0, abd, n=20, m=18), 'abdomen', 'globe', smooth=180)
    cprof = [(0.0, -0.03), (0.06, -0.02), (0.105, 0.012), (0.132, 0.05), (0.142, 0.078), (0.126, 0.08), (0.10, 0.04), (0.06, 0.008), (0.03, 0.0), (0.0, -0.004)]
    col = M.revolve(cprof, n=20, cap=False)
    col.tf(M.rot_to((0, 0, 1), abd)); col.move(*ab0)
    col.smooth = 50.0
    C.rigid(col, 'abdomen', 'chitin')
    C.rigid(P.cone(ab0 + abd * 0.45, abd, 0.05, 0.022, n=7), 'abdomen', 'chitin', smooth=50)
    # ---- wings: every wing is a fan of three thin blades (they buzz: the fan reads as the blur)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        root, tipp = Wg[0] * (sg, 1, 1), Wg[1] * (sg, 1, 1)
        d = unit(tipp - root)
        fanb = []
        for a, k in ((0.0, 1.0), (9.0, 0.88), (-9.0, 0.88)):
            bl = feather(root - d * 0.03, root + d * (np.linalg.norm(tipp - root) * (1.0 if not a else 0.94)), 0.125 * k, th=0.003, up=(0, 0, 1), nu=8, camber=0.05,
                         quill=0.3, tipw=0.6)
            if a:
                bl.rot('y', a, pivot=root)
            fanb.append(bl)
        C.rigid(M.join(fanb), 'wing_%s1' % s, 'wing_' + s, smooth=60)
    # ---- six dangling legs
    LG = []
    for i, s, sg, rt, kn, tp in LEGS:
        a, b = 'leg%d_%s_a' % (i, s), 'leg%d_%s_b' % (i, s)
        LG.append((a, b, sg, i))
        C.rigid(P.seg_tube(rt - unit(kn - rt) * 0.035, kn, 0.017, 0.012, n=6), a, 'chitin')
        low = [P.seg_tube(kn, tp, 0.012, 0.005, n=6), P.cone(tp, (-sg * 0.5, -0.4, -1), 0.035, 0.005, n=4, curl=(-sg, 0, 0.5), seg=2)]
        C.rigid(M.join(low), b, 'chitin')

    # ---- clips (buzzing wings: every one-shot clip has an even number of frames so it ends on the Idle wing phase)
    fk = dict(amp=(9.0,), bias=(3.0,), twist=7.0)

    def sway(p, u, a=4.0, back=0.0, sc=1.0):
        p.rot('abdomen', x=back + a * osc(u, 2, 0.2), y=0.7 * a * osc(u, 1, 0.05))
        if sc != 1.0:
            p.scl('abdomen', sc)

    def idle_x(p, u, ph):
        sway(p, u, 4.0, sc=1 + 0.02 * osc(u, 3))
        dangle(p, LG, sw=4, sp=6, u=u, amp=5, n=2)
        p.rot('prob', x=3 * osc(u, 3, 0.1))
        p.move('body', 0.012 * osc(u, 7), 0, 0.01 * osc(u, 5, 0.3))
    F.hover('Idle', beats=6, T=0.3333, pitch=-8.0, sway=0.05, flap_kw=fk, buzz=True, look=8.0, extra=idle_x)
    F.fly('Walk', 1.8, beats=2, T=0.4, pitch=8.0, flap_kw=fk, buzz=True, extra=lambda p, u, ph: (sway(p, u, 3.0, back=12), dangle(p, LG, sw=22, sp=4, u=u, amp=4, n=1)))
    F.fly('Run', round(5.6 / 1.1, 3), beats=2, T=0.3333, pitch=22.0, flap_kw=fk, buzz=True,
          extra=lambda p, u, ph: (sway(p, u, 2.5, back=28), dangle(p, LG, sw=50, sp=2, u=u, amp=3, n=1, curl=10)))

    def stab(p, u, w, s):
        hu = 0.5
        st = bump(u, hu - 0.09, hu, hu + 0.06, hu + 0.2)
        p.rot('prob', x=-20 * w - 8 * st)
        p.move('prob', 0, -0.022 * st, -0.01 * st)
        p.move('head', 0, -0.05 * st, -0.015 * st)
        sway(p, u, 0.0, back=-10 * w + 26 * s)
        dangle(p, LG, sw=-8 * w + 34 * s, sp=12 * w, u=u)
    F.swoop(dur=0.8, hit=0.4, fwd=0.62, drop=0.42, pitch=24.0, beats=3, flap_kw=fk, buzz=True, extra=stab)
    F.reach('Attack1', 'prob', pr1)

    def drain(p, u):
        dur, hit = 1.8, 0.6
        hu = hit / dur
        F.flap(p, 0.0, buzz=int(round(u * dur * B.FPS)), **fk)
        w = bump(u, 0.0, hu * 0.45, hu * 0.6, hu)                 # backs off and lifts
        g = bump(u, hu * 0.6, hu, 0.8, 0.97)                      # latched on
        dr = bump(u, hu, hu + 0.05, 0.74, 0.8)                    # draining
        gulp = dr * (0.5 - 0.5 * math.cos(TAU * 4 * (u - hu) / (0.78 - hu)))
        full = sm(u, hu, 0.74) * (1 - sm(u, 0.84, 1.0))
        F.body(p, dy=0.12 * w - 0.55 * g, dz=0.08 * w - 0.45 * g + 0.012 * gulp, pitch=-10 * w + 30 * g + 3 * gulp, roll=2 * dr * osc(u, 9))
        F.head(p, pitch=6 * w - 12 * g)
        p.rot('prob', x=-16 * w + 8 * g)
        p.move('prob', 0, -0.02 * g, -0.01 * g)
        p.rot('abdomen', x=-8 * w + 16 * g + 4 * gulp)
        p.scl('abdomen', 1 + 0.3 * full + 0.05 * gulp)
        dangle(p, LG, sw=-30 * g + 10 * w, sp=24 * w - 6 * g, u=u, curl=28 * g)
    C.clip('Attack2', 1.8, drain, hit=0.6)
    F.reach('Attack2', 'prob', pr1)
    F.hit(dur=0.4667, flap_kw=fk, buzz=True, extra=lambda p, u, k: (dangle(p, LG, sw=-22 * k, sp=26 * k, u=u), p.rot('abdomen', x=-24 * k)))

    def dead(p, u, t, on):
        cr = bump(u, 0.0, 0.12, 0.42, 0.55)
        dangle(p, LG, sw=30 * cr, sp=10 * cr + 80 * on, u=u, curl=40 * cr - 10 * on)
        p.rot('abdomen', x=14 * cr + 46 * on)
        p.scl('abdomen', 1 - 0.2 * sm(u, 0.55, 1.0))
        p.rot('prob', x=-24 * on)
    F.death_fall(dur=1.7, belly=0.17, spin=130.0, crumple=(40.0, 0.0, 0.0), rest=(-30.0, 0.0, 0.0), rest_fold=(8.0, 0.0, 0.0), pitch_end=12.0, extra=dead)
    F.spawn_drop(dur=1.2, flap_kw=fk, buzz=True, extra=lambda p, u, r: (sway(p, u, 0.0, back=30 * (1 - r)), dangle(p, LG, sw=45 * (1 - r), sp=8 * (1 - r), u=u)))
    return C.finalize(bake=bake, notes="H3: globe abdomen on the bone `abdomen` (Attack2 swells it by bone scale); each wing is a fan of three blades")


# ================================================================================================ rust_locust
def ringed_tube(pts, rv, rh, bones, m=28, n=10, rings=8, depth=0.06):
    """segmented abdomen: a tube whose radius dips `rings` times, skinned along `bones`"""
    q = P.tubep(pts, rv, rh, m=m, n=n)
    path = M.polyline(np.asarray(pts, dtype=float), 64)
    d = np.linalg.norm(q.v[:, None, :] - path[None, :, :], axis=2)
    k = d.argmin(1)
    t = k / 63.0
    c = path[k]
    f = 1.0 - depth * (0.5 - 0.5 * np.cos(TAU * rings * t)) ** 3
    q.v = c + (q.v - c) * f[:, None]
    B.wbones(q, bones, sharp=3.0)
    q.smooth = 180.0
    return q


@reg
def rust_locust(bake=True):
    """Brass-wing: a locust as long as an arm; its forewings are two stiff leaves of beaten brass held out flat like a
    biplane (they do not flap; small hind wings buzz under them), saw-edged hind legs trailing"""
    h = 1.5
    FW = [V(0.05, -0.15, h + 0.075), V(0.70, -0.13, h + 0.125)]               # brass forewing: root, tip
    HW = [V(0.045, -0.05, h + 0.06), V(0.52, 0.13, h + 0.05)]                 # buzzing hind wing
    AB = [V(0, 0.0, h), V(0, 0.16, h - 0.004), V(0, 0.31, h - 0.014), V(0, 0.46, h - 0.03)]
    hip = V(0.055, -0.04, h - 0.035); knee = V(0.15, 0.30, h + 0.10); shin = V(0.17, 0.72, h - 0.10); toe = V(0.175, 0.82, h - 0.14)
    SM = []
    for i, (rt, kn, tp) in enumerate((((0.04, -0.21, h - 0.045), (0.115, -0.26, h - 0.10), (0.085, -0.29, h - 0.20)),
                                      ((0.045, -0.10, h - 0.06), (0.13, -0.10, h - 0.115), (0.10, -0.06, h - 0.22)))):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            SM.append((i, s, sg, V(rt) * (sg, 1, 1), V(kn) * (sg, 1, 1), V(tp) * (sg, 1, 1)))

    def extra(sk):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            m = V(sg, 1, 1)
            sk.bone('fore_' + s, FW[0] * m, FW[1] * m, 'body')
            sk.bone('hind_%s_a' % s, hip * m, knee * m, 'body')
            sk.bone('hind_%s_b' % s, knee * m, shin * m, 'hind_%s_a' % s)
            sk.bone('ant_' + s, V(0.028, -0.345, h + 0.05) * m, V(0.085, -0.52, h + 0.12) * m, 'head')
        for i, s, sg, rt, kn, tp in SM:
            sk.bone('leg%d_%s_a' % (i, s), rt, kn, 'body')
            sk.bone('leg%d_%s_b' % (i, s), kn, tp, 'leg%d_%s_a' % (i, s))
    sk = RE.flyer_skel(body=((0, 0.0, h), (0, -0.23, h)), head=((0, -0.25, h + 0.0), (0, -0.37, h - 0.03)),
                       jaw=((0, -0.33, h - 0.08), (0, -0.355, h - 0.15)), wing=HW, tail=AB, extra=extra)
    C = B.Beast('mon_rust_locust', sk)
    std_meta(C, 'rust', 0.9, 7.0)
    F = RE.Flyer(C, h)
    C.look('chitin', 'chitin', 'bone_chitin', color=(0.17, 0.10, 0.045), color2=(0.30, 0.20, 0.09), edge_color=(0.42, 0.32, 0.18), rough=0.45, pit=150.0,
           dust=0.45, dust_color=(0.30, 0.24, 0.16), axis=(0, 1, 0))
    C.look('dark', 'chitin', 'bone_chitin', color=(0.045, 0.03, 0.02), color2=(0.09, 0.06, 0.035), edge_color=(0.25, 0.18, 0.10), rough=0.3, dust=0.2)
    C.look('eye', 'chitin', 'bone_chitin', color=(0.02, 0.015, 0.012), color2=(0.05, 0.035, 0.02), rough=0.14, pit=280.0, grain=420.0, dust=0.0, edge=0.0, ridge_h=0.0)
    # faint violet static towards the tips of the brass leaves (the brief's storm accent)
    C.look('brass', 'tipglow', 'metal_brass', base='metal', center=(0.0, 0.0, h), r0=0.42, r1=0.8, emit=(0.55, 0.35, 1.0), strength=1.0,
           gain=0.55, glow_scale=40.0,
           **dict(L.PRESET['brass'], color=(0.62, 0.40, 0.10), edge_color=(0.90, 0.72, 0.34), hammer=30.0, amount=0.2, rough=0.36))
    for s_, sg_ in (('l', 1.0), ('r', -1.0)):
        C.look('wing_' + s_, 'iwing', 'skin_wing', color=(0.30, 0.25, 0.17), vein=(0.10, 0.07, 0.04), axis=(sg_ * 0.94, 0.34, 0.0), rough=0.55)
    # ---- thorax with its saddle shield + tall head (one skin)
    thorax = P.blob((0, -0.115, h), 0.085, 0.15, 0.09, n=14, m=8)
    saddle = P.blob((0, -0.165, h + 0.028), 0.092, 0.11, 0.084, n=12, m=7)
    for q in (thorax, saddle):
        B.wbones(q, ['body'])
    skull = P.blob((0, -0.312, h - 0.015), 0.068, 0.062, 0.094, n=12, m=7)
    B.wbones(skull, ['head'])
    neck = P.tubep([(0, -0.22, h), (0, -0.29, h - 0.005)], [0.066, 0.06], m=4, n=10)
    B.wbones(neck, ['body', 'head'], sharp=3.0)
    body = P.organic([thorax, saddle, neck, skull], voxel=0.008, tris=2200, smooth=5)
    C.skin(body, 'chitin')
    bvh = P.bvh_of(body)
    C.skin(ringed_tube(AB[:1] + [AB[0] + V(0, 0.02, 0)] + AB[1:] + [AB[3] + V(0, 0.03, -0.004)], [0.07, 0.08, 0.072, 0.058, 0.036, 0.008], [0.066, 0.076, 0.068, 0.054, 0.034, 0.008],
                       ['body', 'tail1', 'tail2', 'tail3'], rings=9), 'chitin')
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        m = V(sg, 1, 1)
        C.rigid(P.blob((sg * 0.057, -0.325, h + 0.035), 0.027, 0.034, 0.042, n=10, m=6), 'head', 'eye', smooth=180)
        a0, a1 = V(0.028, -0.345, h + 0.05) * m, V(0.085, -0.52, h + 0.12) * m
        C.rigid(P.tubep([a0 - unit(a1 - a0) * 0.03, a1], [0.009, 0.003], m=6, n=5), 'ant_' + s, 'dark', smooth=180)
        C.rigid(P.cone(V(sg * 0.034, -0.335, h - 0.085), (-sg * 0.25, -0.25, -1), 0.08, 0.018, n=5, curl=(-sg, 0, 0), seg=3), 'jaw', 'dark', smooth=50)
        # brass forewing: a long beaten leaf on a chitin spar
        r0, r1 = FW[0] * m, FW[1] * m
        d = unit(r1 - r0)
        rng = np.random.default_rng(3 if sg > 0 else 8)
        leaf = feather(r0 + d * 0.04, r1, 0.235, th=0.006, up=(0, 0, 1), nu=12, camber=0.07, quill=0.5, tipw=0.5,
                       rag=[1.0] + [1 - 0.1 * rng.uniform(0, 1) for _ in range(11)])
        leaf.rough(0.006, 9.0, seed=2 + sg)
        C.rigid(leaf, 'fore_' + s, 'brass', smooth=40)
        C.rigid(P.tubep([r0 - d * 0.035, r0 + d * 0.16, r0 + d * 0.40], [0.017, 0.011, 0.004], [0.02, 0.013, 0.005], m=6, n=6), 'fore_' + s, 'chitin', smooth=180)
        # hind wing: a fan of three pale blades that buzz under the brass
        w0, w1 = HW[0] * m, HW[1] * m
        dw = unit(w1 - w0)
        fanb = []
        for a, k in ((0.0, 1.0), (9.0, 0.9), (-9.0, 0.9)):
            bl = feather(w0 - dw * 0.03, w1, 0.17 * k, th=0.003, up=(0, 0, 1), nu=8, camber=0.05, quill=0.3, tipw=0.55)
            if a:
                bl.rot('y', a, pivot=w0)
            fanb.append(bl)
        C.rigid(M.join(fanb), 'wing_%s1' % s, 'wing_' + s, smooth=60)
        # hind leg: drumstick thigh, saw-edged shin, hooked foot
        hp, kn, sh_, to = hip * m, knee * m, shin * m, toe * m
        df = unit(kn - hp)
        C.rigid(P.tubep([hp - df * 0.03, hp + df * 0.10, hp + df * 0.22, kn], [0.034, 0.056, 0.04, 0.018], [0.026, 0.04, 0.03, 0.016], m=9, n=8), 'hind_%s_a' % s, 'chitin',
                smooth=180)
        dt = unit(sh_ - kn)
        low = [P.tubep([kn - dt * 0.008, kn + dt * 0.04, sh_], [0.02, 0.016, 0.01], m=6, n=6),
               P.tubep([sh_ - dt * 0.01, to], [0.01, 0.006], m=3, n=5), P.cone(to, (0, 0.5, -1), 0.04, 0.007, n=4, curl=(0, -1, 0), seg=2)]
        nt = 9
        for k in range(nt):
            b0 = kn + (sh_ - kn) * (0.14 + 0.82 * k / (nt - 1))
            low.append(P.cone(b0 + V(0, 0, 0.006), (0, 0.45, -1), 0.05 - 0.012 * k / (nt - 1), 0.011, n=4))
        C.rigid(M.join(low), 'hind_%s_b' % s, 'dark', smooth=50)
    # ---- four small legs tucked under the chest
    LG = []
    for i, s, sg, rt, kn, tp in SM:
        a, b = 'leg%d_%s_a' % (i, s), 'leg%d_%s_b' % (i, s)
        LG.append((a, b, sg, i))
        C.rigid(P.seg_tube(rt - unit(kn - rt) * 0.03, kn, 0.017, 0.012, n=6), a, 'chitin')
        C.rigid(M.join([P.seg_tube(kn, tp, 0.012, 0.006, n=6), P.cone(tp, (-sg * 0.4, -0.5, -1), 0.03, 0.005, n=4)]), b, 'dark')
    shin_tip = shin.copy()

    # ---- clips: the brass leaves never flap; the hind wings buzz (even frame counts, see the gadfly)
    fk = dict(amp=(9.0,), bias=(-2.0,), twist=6.0)

    def fore(p, tilt=0.0, up=0.0, sweep=0.0, buzz=0.0, f=0, up_r=None):
        z = buzz * (1.0 if f % 2 == 0 else -1.0)
        p.rot('fore_l', x=tilt + z, y=-up, z=sweep)
        p.rot('fore_r', x=tilt + z, y=(up if up_r is None else up_r), z=-sweep)

    def hind(p, a=0.0, b=0.0, sp=0.0, u=0.0, amp=0.0, n=1, a_r=None, b_r=None):
        """a < 0 swings the thighs down and forward, b < 0 folds the shins under them"""
        for s, sg, aa, bb in (('l', 1.0, a, b), ('r', -1.0, a if a_r is None else a_r, b if b_r is None else b_r)):
            p.rot('hind_%s_a' % s, x=aa + amp * osc(u, n, 0.08 * sg), z=sg * sp)
            p.rot('hind_%s_b' % s, x=bb + 1.5 * amp * osc(u, n, -0.1 + 0.08 * sg))

    def ant(p, a=0.0, u=0.0, amp=0.0):
        p.rot('ant_l', x=a + amp * osc(u, 3), z=amp * osc(u, 2, 0.2))
        p.rot('ant_r', x=a + amp * osc(u, 3, 0.4), z=-amp * osc(u, 2, 0.5))

    def idle_x(p, u, ph):
        fore(p, tilt=2 * osc(u, 2), up=2 + 1.5 * osc(u, 2, 0.25))
        hind(p, a=-7, b=-3, u=u, amp=3, n=2)
        dangle(p, LG, sw=6, sp=4, u=u, amp=4, n=2)
        ant(p, 0, u, 6)
        F.jaw(p, 5 + 5 * osc(u, 4))
        p.move('body', 0.01 * osc(u, 5), 0, 0.012 * osc(u, 7, 0.3))
    F.hover('Idle', beats=6, T=0.3333, pitch=-6.0, sway=0.04, flap_kw=fk, buzz=True, tail=3.0, look=6.0, extra=idle_x)
    F.fly('Walk', 2.7, beats=2, T=0.4, pitch=5.0, flap_kw=fk, buzz=True, tail=2.0, bank=4.0,
          extra=lambda p, u, ph: (fore(p, tilt=-2, up=2 + osc(u, 1), sweep=2), hind(p, a=-6, b=-2, u=u, amp=2), dangle(p, LG, sw=24, u=u, amp=3, n=1), ant(p, 14, u, 3)))
    F.fly('Run', round(7.0 / 0.9, 3), beats=2, T=0.3333, pitch=12.0, flap_kw=dict(fk, amp=(11.0,)), buzz=True, tail=1.5, bank=5.0,
          extra=lambda p, u, ph: (fore(p, tilt=-4, up=4 + osc(u, 1), sweep=7), hind(p, a=5, b=4, u=u, amp=1.5), dangle(p, LG, sw=50, u=u, amp=2, n=1, curl=12), ant(p, 30, u, 2)))

    def slash(p, u, w, s):
        hu = 0.5
        st = bump(u, hu - 0.07, hu, hu + 0.06, hu + 0.22)
        cock = bump(u, 0.08, hu - 0.12, hu - 0.07, hu)
        fore(p, tilt=-6 * w + 10 * s, up=6 * s, sweep=10 * s)
        hind(p, a=-70 * cock - 128 * st, b=-95 * cock + 18 * st, sp=4 * st)
        dangle(p, LG, sw=-10 * w + 30 * s, sp=10 * w, u=u)
        ant(p, 25 * s)
        F.jaw(p, 20 * st)
    F.swoop(dur=1.0, hit=0.5, fwd=0.75, drop=0.55, pitch=-24.0, beats=3, gape=0.0, flap_kw=fk, buzz=True, extra=slash)
    C.reach('Attack1', 'hind_l_b', shin_tip)

    def stridulate(p, u, w, s):
        f = int(round(u * 42))
        alt = 1.0 if f % 2 == 0 else -1.0
        p.rot('body', x=-13 * s)                                   # rears while it sings
        fore(p, tilt=-24 * s - 5 * w, up=26 * s, sweep=-20 * s, buzz=5.0 * s, f=f)
        hind(p, a=26 * s - 8 * w + 5 * s * alt, b=-16 * s - 4 * s * alt, sp=-3 * s)
        dangle(p, LG, sw=-12 * s, sp=14 * s, u=u)
        ant(p, -20 * s, u, 4 * s)
    F.screech(dur=1.4, hit=0.6, beats=4, gape=22.0, shake=3.0, flap_kw=dict(fk, amp=(14.0,)), buzz=True, extra=stridulate)
    C.meta['attack_reach'] = max(C.meta.get('attack_reach', 0.0), 0.9)
    F.hit(dur=0.4667, flap_kw=fk, buzz=True, extra=lambda p, u, k: (fore(p, tilt=-14 * k, up=10 * k, up_r=-16 * k), hind(p, a=-35 * k, b=-30 * k, sp=10 * k),
                                                                     dangle(p, LG, sw=-20 * k, sp=24 * k, u=u), ant(p, -30 * k)))

    def dead(p, u, t, on):
        cr = bump(u, 0.0, 0.12, 0.42, 0.55)
        kick = bump(u, 0.66, 0.70, 0.74, 0.86)
        fore(p, tilt=-10 * cr + 4 * on, up=22 * cr + 5 * on, up_r=30 * cr - 3 * on, sweep=8 * on)
        hind(p, a=-40 * cr - 4 * on - 30 * kick, b=-50 * cr + 12 * on + 40 * kick, sp=6 * on, a_r=-40 * cr - 2 * on)
        dangle(p, LG, sw=20 * cr, sp=75 * on, u=u, curl=30 * cr)
        ant(p, -25 * on)
    F.death_fall(dur=1.7, belly=0.075, spin=150.0, crumple=(30.0, 0.0, 0.0), rest=(-8.0, 0.0, 0.0), rest_fold=(24.0, 0.0, 0.0), extra=dead)
    F.spawn_drop(dur=1.2, flap_kw=fk, buzz=True, extra=lambda p, u, r: (fore(p, tilt=-18 * (1 - r), up=14 * (1 - r)), hind(p, a=-30 * (1 - r), b=-20 * (1 - r)),
                                                                        dangle(p, LG, sw=40 * (1 - r), u=u), ant(p, 30 * (1 - r))))
    return C.finalize(bake=bake, notes="H3: brass forewings on the bones fore_l / fore_r (they never flap); hind wings = fans of three blades on wing_l1 / wing_r1")


# ================================================================================================ fiend_wing
@reg
def fiend_wing(bake=True):
    """Cinder-wing: nearly all wing - two broad wings of thin black slag-glass with glowing lava cracks (brightest
    along the leading edges), a tiny horned body between them, a long whip tail ending in a heavy cinder that it drops.
    Wingspan 2.5 m in the file (3 m in the game). A wide flat "M" with a weighted tail."""
    h = 1.7
    W = [(0.07, -0.08, h + 0.03), (0.34, -0.24, h + 0.10), (0.58, -0.44, h + 0.14), (1.25, 0.06, h - 0.02)]
    TL = [V(0, 0.20, h - 0.02), V(0, 0.40, h - 0.06), V(0, 0.60, h - 0.14), V(0, 0.78, h - 0.26), V(0, 0.93, h - 0.42), V(0, 1.04, h - 0.60), V(0, 1.10, h - 0.78)]
    cin0 = TL[-1].copy()
    hipj = V(0.04, 0.12, h - 0.035); footj = V(0.07, 0.23, h - 0.21)

    def extra(sk):
        sk.bone('cinder', cin0, cin0 + V(0, 0.02, -0.2), 'tail6')
    sk = RE.flyer_skel(body=((0, 0.20, h), (0, -0.12, h + 0.01)), head=((0, -0.13, h + 0.03), (0, -0.27, h + 0.02)),
                       jaw=((0, -0.165, h - 0.008), (0, -0.265, h - 0.03)), wing=W, tail=TL, legs=(hipj, footj), extra=extra)
    C = B.Beast('mon_fiend_wing', sk)
    std_meta(C, 'fiend', 1.2, 6.4)
    F = RE.Flyer(C, h)
    LEAD = [(0.0, -0.10), (0.07, -0.08), (0.34, -0.24), (0.58, -0.44), (1.25, 0.06)]
    C.look('slag', 'slag', 'emis_lava', color=(0.012, 0.011, 0.013), band=(0.04, 0.03, 0.03), rough=0.14, emit=(1.0, 0.36, 0.05), strength=1.4, lead=LEAD,
           fall=0.6, far=0.12, crack=8.5, crack_w=0.024, halo=0.26, gain=1.0, patch_lo=0.06)
    C.look('spar', 'bark', 'emis_lava', color=(0.03, 0.027, 0.025), axis=(1, 0.2, 0), scale=30.0, crack_w=0.10, heat=(1.0, 0.36, 0.05), heat_amt=1.0, strength=1.4,
           ash_amt=0.15)
    C.look('cinder', 'bark', 'emis_lava', color=(0.045, 0.04, 0.035), axis=(0, 0, 1), scale=13.0, crack_w=0.12, heat=(1.0, 0.40, 0.06), heat_amt=1.0, strength=1.4,
           ash_amt=0.35)
    C.look('eye', 'emis', 'emis_lava', emit=(1.0, 0.5, 0.1), strength=1.4, mottle=0.1, base_mul=0.6)
    C.look('hide', 'rock', 'rock_body', color=(0.04, 0.036, 0.034), scale=24.0, heat=(1.0, 0.33, 0.05), heat_amt=0.45, ash_amt=0.4, crack_w=0.05)
    C.look('horn', 'bone', 'rock_body', color=(0.075, 0.065, 0.058), stain=(0.02, 0.018, 0.016), crack_amt=0.8, dirt_lo=-2.0, dirt_hi=-1.5)
    # ---- the tiny horned body (one skin)
    torso = P.blob((0, 0.04, h), 0.08, 0.17, 0.075, n=14, m=8)
    chest = P.blob((0, -0.05, h + 0.0), 0.09, 0.10, 0.085, n=12, m=7)
    for q in (torso, chest):
        B.wbones(q, ['body'])
    neck = P.tubep([(0, -0.10, h + 0.015), (0, -0.17, h + 0.03)], [0.05, 0.045], m=4, n=10)
    B.wbones(neck, ['body', 'head'], sharp=3.0)
    skull = P.blob((0, -0.20, h + 0.035), 0.055, 0.075, 0.052, n=12, m=7)
    snout = P.tubep([(0, -0.23, h + 0.03), (0, -0.285, h + 0.02), (0, -0.305, h + 0.014)], [0.036, 0.026, 0.016], [0.042, 0.03, 0.02], m=6, n=10)
    for q in (skull, snout):
        B.wbones(q, ['head'])
    body = P.organic([torso, chest, neck, skull, snout], voxel=0.008, tris=2200, smooth=5)
    C.skin(body, 'hide')
    bvh = P.bvh_of(body)
    jaw = P.tubep([(0, -0.175, h - 0.008), (0, -0.25, h - 0.018), (0, -0.295, h - 0.022)], [0.016, 0.013, 0.008], [0.036, 0.028, 0.016], m=6, n=8)
    C.rigid(jaw, 'jaw', 'hide', smooth=180)
    for sg in (1, -1):
        C.rigid(P.seated(bvh, (sg * 0.034, -0.175, h + 0.08), (sg * 0.55, 0.5, 0.7), 0.2, 0.024, n=6, curl=(sg * 0.5, 0.9, -0.5), seg=5), 'head', 'horn', smooth=60)
        C.rigid(P.blob((sg * 0.04, -0.245, h + 0.052), 0.012, 0.014, 0.011, n=8, m=5), 'head', 'eye', smooth=180)
        C.rigid(P.seated(bvh, (sg * 0.02, -0.295, h + 0.004), (sg * 0.1, -0.2, -1), 0.035, 0.007, curl=(0, 1, 0), seg=3), 'head', 'horn', smooth=50)
    for k, y in enumerate((-0.06, 0.02, 0.10, 0.17)):
        C.rigid(P.seated(bvh, (0, y, h + 0.09), (0, 0.5, 1), 0.075 - 0.01 * k, 0.018, n=5, curl=(0, 1, -0.3), seg=3), 'body', 'horn', smooth=50)
    # ---- wings: slag spars (arm, three fingers, an elbow strut) and glass panes that ride on them
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        w1, w2, w3 = 'wing_%s1' % s, 'wing_%s2' % s, 'wing_%s3' % s
        sh, el, wr, tp = mirror_pts(W, sg)
        f2 = V(sg * 1.03, 0.46, h - 0.03); f3 = V(sg * 0.70, 0.58, h - 0.02); f4 = V(sg * 0.38, 0.44, h - 0.02); hipw = V(sg * 0.06, 0.24, h - 0.02)
        C.rigid(P.tubep([sh - unit(el - sh) * 0.05, el], [0.034, 0.026], m=5, n=5), w1, 'spar', smooth=30)
        C.rigid(P.tubep([el, f4], [0.016, 0.005], m=5, n=5), w1, 'spar', smooth=30)
        C.rigid(P.tubep([el - unit(wr - el) * 0.012, wr], [0.026, 0.022], m=5, n=5), w2, 'spar', smooth=30)
        fing = [P.tubep([wr - unit(q - wr) * 0.01, q], [r0, 0.005], m=6, n=5) for q, r0 in ((tp, 0.022), (f2, 0.015), (f3, 0.015))]
        fing.append(P.cone(wr, (sg * 0.3, -1, 0.3), 0.11, 0.02, n=5, curl=(0, 0, -1), seg=3))                  # wrist claw
        C.rigid(M.join(fing), w3, 'spar', smooth=30)
        mid = M.lerp(wr, tp, 0.5)
        lead = [sh + V(-sg * 0.03, 0, -0.008), el, wr, mid, tp]
        trail = [hipw, f4, f3, f2, tp + V(-sg * 0.03, 0.04, 0)]
        mem = RE.panes(lead, trail, sub=6, nv=6, th=0.008, notch=[0.16, 0.2, 0.3, 0.34])
        RE.pane_weights(mem, [w1, w2, w3, w3], ['body', w1, w3, w3, w3])
        C.skin(mem, 'slag', smooth=40)
        # small clawed feet for the dive
        hp, ft = hipj * (sg, 1, 1), footj * (sg, 1, 1)
        leg = [P.tubep([hp - unit(ft - hp) * 0.04, ft], [0.022, 0.011], m=5, n=6)]
        for k in (-1, 0, 1):
            leg.append(P.cone(ft - V(0, 0.004, 0), (k * 0.5, 0.2, -1), 0.07, 0.01, n=4, curl=(0, -1, 0.2), seg=3))
        C.rigid(M.join(leg), 'leg_' + s, 'hide', smooth=50)
    # ---- whip tail of knuckles, three hooks at its end, the cinder they hold
    tail = ringed_tube([TL[0] - V(0, 0.06, 0)] + TL, [0.036, 0.035, 0.031, 0.027, 0.023, 0.02, 0.017, 0.015], [0.036, 0.035, 0.031, 0.027, 0.023, 0.02, 0.017, 0.015],
                       ['body'] + F.tail, m=52, n=8, rings=17, depth=0.3)
    C.skin(tail, 'hide')
    for a in (90, 210, 330):
        dv = V(math.cos(math.radians(a)), math.sin(math.radians(a)), 0)
        C.rigid(P.cone(cin0 + V(0, -0.004, 0.02) + dv * 0.008, dv * 0.9 + V(0, 0, -0.5), 0.10, 0.012, n=4, curl=V(0, 0, -1) - dv * 0.6, seg=3), 'tail6', 'horn', smooth=50)
    cin = M.rock(0.115, 0.125, 0.105, seed=4, n=24, jit=0.2, flat=True).move(*(cin0 + V(0, 0.01, -0.105)))
    C.rigid(cin, 'cinder', 'cinder', free=True)
    hc = C.rg.H('cinder')

    # ---- clips: slow shallow beats (thin glass), the whip tail lags behind everything
    fk = dict(amp=(11.0, 7.0, 5.0), lag=(0.0, 0.09, 0.18), bias=(5.0, 2.0, -5.0), fold=(0.0, 3.0, 8.0), twist=6.0)
    TB = F.tail
    seg = [math.degrees(math.atan2(TL[i + 1][2] - TL[i][2], TL[i + 1][1] - TL[i][1])) for i in range(6)]      # rest pitch of every tail bone (negative = down)
    flat = [(0.0 if i == 0 else seg[i - 1]) - seg[i] for i in range(6)]                                       # rotation that lays the tail out level

    def whip(p, u=0.0, lift=0.0, amp=0.0, n=1, swing=0.0):
        """on top of the stock tail motion: lift > 0 streams the tail up and back, a lagging wave of `amp` degrees"""
        for i, b in enumerate(TB):
            p.rot(b, x=lift / len(TB) + amp * osc(u, n, -0.3 - i * 0.13), z=swing / len(TB))

    def sway(p, u, a=5.0, n=2):
        p.rot('cinder', x=a * osc(u, n, -0.95), y=0.7 * a * osc(u, n / 2 if n >= 2 else 1, -0.4))
    F.hover('Idle', beats=3, T=0.6667, bob=0.05, pitch=-8.0, sway=0.05, flap_kw=fk, tail=4.0, legs=20.0, look=8.0, extra=lambda p, u, ph: sway(p, u, 6.0))
    F.fly('Walk', 1.9, beats=1, T=0.8, pitch=3.0, flap_kw=fk, tail=3.0, legs=40.0, bank=3.0, extra=lambda p, u, ph: (whip(p, u, lift=12), sway(p, u, 4.0, 1)))
    F.fly('Run', round(6.4 / 1.2, 3), beats=2, T=0.5, pitch=9.0, flap_kw=dict(fk, amp=(15.0, 9.0, 6.0)), tail=3.0, legs=60.0, bank=4.0,
          extra=lambda p, u, ph: (whip(p, u, lift=34), sway(p, u, 3.0)))

    # Attack1 strafe-drop: a low pass, the tail is whipped forward under the body and lets the cinder go; it falls,
    # lies burning where the pass went, shrinks away, and a new one swells at the tail tip before the clip ends
    A1, REL, HIT = 1.4, 0.36, 0.72
    ur, uh = REL / A1, HIT / A1

    def strafe_pose(p, u):
        w = bump(u, 0.0, ur * 0.5, ur * 0.7, ur)
        s = bump(u, ur * 0.5, ur + 0.02, uh, 0.95)
        fl = bump(u, ur - 0.07, ur, ur + 0.05, ur + 0.22)
        F.flap(p, (u * 2) % 1.0, k=1.0 - 0.6 * s, **fk)
        F.wings(p, up=[10 * s, 5 * s, 0], fold=[0, 4 * s, 8 * s])
        F.body(p, dy=0.15 * w - 0.55 * s, dz=0.12 * w - 0.45 * s, pitch=-8 * w + 12 * s - 10 * fl, roll=5 * s * osc(u, 1))
        F.head(p, pitch=6 * w - 8 * s)
        F.jaw(p, 24 * fl)
        F.legs(p, 30 + 30 * s)
        for i, b in enumerate(TB):
            p.rot(b, x=(34 * w - 64 * fl - 12 * s) / len(TB) + 3 * osc(u, 2, -i * 0.13) * (1 - s))

    def held(u):
        q = C.pose()
        strafe_pose(q, u)
        return q.pt('tail6', hc)
    Xr = held(ur)
    Xland = V(Xr[0], Xr[1] - 0.5, 0.235)

    def strafe(p, u):
        strafe_pose(p, u)
        if u <= ur:
            return
        if u < uh + 0.2:
            s2 = min(1.0, (u - ur) / (uh - ur))
            X = V(Xr[0] + (Xland[0] - Xr[0]) * s2, Xr[1] + (Xland[1] - Xr[1]) * s2, Xr[2] + (Xland[2] - Xr[2]) * s2 * s2)
            p.move('cinder', (np.linalg.inv(p.G('tail6')) @ np.r_[X, 1.0])[:3] - hc)
            p.aim_world('cinder', euler(360.0 * s2, 0.0, 90.0 * s2))
            p.scl('cinder', max(0.001, 1.0 - sm(u, uh + 0.1, uh + 0.19)))
        else:
            p.scl('cinder', max(0.001, sm(u, uh + 0.24, 0.97)))
    C.clip('Attack1', A1, strafe, hit=HIT)
    C.reach('Attack1', 'cinder', hc + V(0, 0, -0.1))

    def keep_clear(p, zmin=0.12):
        """curl the whip up just enough that the cinder clears the ground (bisection: continuous in time)"""
        idx = [C.rg.idx[b] for b in TB]
        base = p.Rd[idx].copy()

        def low(lift):
            p.Rd[idx] = base
            for b in TB:
                p.rot(b, x=lift / len(TB))
            return p.pt('cinder', hc + V(0, 0.01, -0.105))[2] - 0.16
        if low(0.0) >= zmin:
            return
        a, b = 0.0, 160.0
        for _ in range(12):
            mid = 0.5 * (a + b)
            if low(mid) >= zmin:
                b = mid
            else:
                a = mid
        low(b)

    def claw(p, u, w, s):
        hu = 0.5
        st = bump(u, hu - 0.1, hu, hu + 0.06, hu + 0.24)
        for b in ('leg_l', 'leg_r'):
            p.rot(b, x=-70 * s - 85 * st)
        whip(p, u, lift=40 * s - 10 * w)
        sway(p, u, 4.0)
        keep_clear(p)
    F.swoop('Attack2', dur=1.1, hit=0.55, fwd=0.9, drop=0.95, pitch=30.0, beats=2, gape=36.0, flap_kw=fk, glide=(22.0, 10.0, 0.0), extra=claw)
    F.reach('Attack2', 'leg_l', footj + V(0, 0, -0.07))
    F.hit(flap_kw=fk, extra=lambda p, u, k: (whip(p, u, lift=-30 * k, swing=25 * k), sway(p, u, 10 * k)))

    def dead(p, u, t, on):
        for i, b in enumerate(TB):
            p.rot(b, x=flat[i] * on + 6 * (1 - on) * t)
        p.rot('cinder', x=-12 * on)
    F.death_fall(dur=1.8, belly=0.09, spin=120.0, crumple=(35.0, 18.0, 8.0), rest=(-3.0, -5.0, -4.0), rest_fold=(0.0, 8.0, 18.0), extra=dead)
    F.spawn_drop(dur=1.3, flap_kw=fk, extra=lambda p, u, r: whip(p, u, lift=50 * (1 - r)))
    return C.finalize(bake=bake, notes="H3: glass panes skinned to ride on rigid spars; bone `cinder` (child of tail6) carries the cinder: Attack1 lets it fall "
                                       "(bone translation), hides it by bone scale 0.001 and grows a new one; meta attack_reach = where it lands")


# ================================================================================================ hush_wing
@reg
def hush_wing(bake=True):
    """Hush-wing: a flat white kite, a diamond 2.5 m across in the file (3 m in the game) with no body, flexing gently
    along its diagonals like a ray in water, a round hole in the centre, two long ribbon tails. It never flaps."""
    h = 1.6
    HALF, FRONT, BACK, HOLE = 1.25, 0.95, 1.15, 0.27
    RB = {}
    for s, sg in (('l', 1.0), ('r', -1.0)):
        d = unit(V(sg * 0.17, 1.0, -0.2))
        RB['rib_' + s] = ([V(sg * 0.035, BACK - 0.05, h) + d * (0.27 * i) for i in range(7)], 'fb3')
    slope = math.degrees(math.atan2(0.2, math.hypot(0.17, 1.0)))             # the ribbons hang this far below level at rest
    sk = RE.kite_skel(h, HALF, FRONT, BACK, HOLE, nx=3, nf=2, nb=3, ribbons=RB)
    C = B.Beast('mon_hush_wing', sk)
    std_meta(C, 'hush', 1.2, 6.0, radius=1.15)                               # the diamond without its ribbons
    K = RE.Kite(C, h, rib={'l': 'rib_l', 'r': 'rib_r'})
    C.look('hush', 'chalk', 'skin_hush', color=(0.80, 0.80, 0.79), shade=(0.58, 0.59, 0.62), crack=26.0)
    flexb = K.fx['l'] + K.fx['r'] + K.ff + K.fb

    def rim(a):
        c, sn = math.cos(a), math.sin(a)
        R = 1.0 / (abs(c) / HALF + abs(sn) / (FRONT if sn < 0 else BACK))
        return R * (1.0 - 0.05 * math.sin(2 * a) ** 2)                       # edges drawn in a touch: four clear points

    def dome(t):
        return 0.045 * (1.0 - t) ** 1.3
    n, m = 72, 9
    G = np.zeros((m + 1, n, 3))
    for j in range(n):
        a = TAU * j / n
        R = rim(a)
        for i in range(m + 1):
            t = i / m
            r = HOLE + (R - HOLE) * t ** 1.1
            G[i, j] = (r * math.cos(a), r * math.sin(a), h + dome(t))
    sheet = M.sheet(G, closed_u=True)
    sheet.solidify(0.016)
    B.wbones(sheet, ['body'] + flexb, sharp=2.6, k=3)
    C.skin(sheet, 'hush', smooth=60)
    lip = P.ring((0, 0, h + dome(0.0) - 0.004), HOLE + 0.01, 0.026, axis=(0, 0, 1), n=44, m=8)
    core(C, lip, 'body', 'hush')
    # a low spine along every arm, from the lip to the corner
    for ch, tip in ((K.fx['l'], V(HALF, 0, 0)), (K.fx['r'], V(-HALF, 0, 0)), (K.ff, V(0, -FRONT, 0)), (K.fb, V(0, BACK, 0))):
        dv = unit(tip)
        Ln = float(np.linalg.norm(tip))
        pts = [dv * (HOLE + 0.01 + (Ln - HOLE - 0.03) * t) + V(0, 0, h + dome(t) + 0.004) for t in (0.0, 0.3, 0.65, 1.0)]
        sp = P.tubep(pts, [0.02, 0.016, 0.011, 0.005], [0.05, 0.036, 0.022, 0.008], m=14, n=6, up=(0, 0, 1))
        B.wbones(sp, ['body'] + ch, sharp=4.0, k=2)
        C.skin(sp, 'hush', smooth=180)
    # the two ribbon tails
    for s, sg in (('l', 1.0), ('r', -1.0)):
        pts = RB['rib_' + s][0]
        d = unit(pts[-1] - pts[0])
        side = unit(np.cross(d, (0, 0, 1)))
        nu = 19
        Gr = np.zeros((nu, 3, 3))
        for i in range(nu):
            t = i / (nu - 1)
            c = pts[0] - d * 0.05 + (pts[-1] - pts[0] + d * 0.05) * t
            hw = 0.5 * (0.085 - 0.045 * t)
            Gr[i, 0] = c - side * hw
            Gr[i, 1] = c + V(0, 0, 0.006)
            Gr[i, 2] = c + side * hw
        rb = P.sheet_solid(Gr, 0.007)
        B.wbones(rb, ['fb3'] + K.rib[s], sharp=3.5, k=2)
        C.skin(rb, 'hush', smooth=60)

    # ---- clips: no wing beat anywhere; it flexes, banks and trails its ribbons
    def ib(u, k=1.0):
        return dict(dz=0.05 * k * osc(u, 1), dx=0.04 * k * osc(u, 1, 0.25), pitch=k * (-3 + 2 * osc(u, 1, 0.1)), roll=2.5 * k * osc(u, 1, 0.3),
                    yaw=4 * k * osc(u, 1, 0.55))

    def soft(p, u, k=1.0):
        K.wave(p, u, n=1, side=9 * k, fore=7 * k, lag=0.12)
        K.ribbons(p, u, n=2, amp=4.5 * k, lag=0.22, lift=-4 * k)

    def body(p, base, **kw):
        d = dict(base)
        for a, v in kw.items():
            d[a] = d.get(a, 0.0) + v
        K.body(p, **d)

    def idle(p, u):
        K.body(p, **ib(u))
        soft(p, u)
    C.clip('Idle', 3.0, idle)

    def glide(pitch, side, fore, lift, amp, bank):
        def fn(p, u):
            K.body(p, dz=0.03 * osc(u, 1), pitch=pitch + 1.5 * osc(u, 1, 0.2), roll=bank * osc(u, 1), dx=0.03 * osc(u, 1, 0.2))
            K.wave(p, u, n=1, side=side, fore=fore, lag=0.16, skew=0.04)
            K.ribbons(p, u, n=2, amp=amp, lag=0.22, lift=lift)
        return fn
    C.clip('Walk', 1.6, glide(5.0, 8.0, 6.0, 6.0, 4.0, 3.0)); C.meta['walk_speed'] = 1.75
    C.clip('Run', 1.0, glide(11.0, 12.0, 6.0, slope, 3.5, 5.0)); C.meta['run_speed'] = round(6.0 / 1.2, 3)

    def glide_pass(p, u):                      # Attack1: banks and sweeps low, the left corner leading like a blade
        hu = 0.55 / 1.2
        k = 1 - bump(u, 0.0, 0.1, 0.9, 1.0)
        w = bump(u, 0.0, hu * 0.45, hu * 0.6, hu * 0.9)
        s = bump(u, hu * 0.55, hu, hu + 0.1, 0.94)
        body(p, ib(0.0, k), dy=0.2 * w - 1.0 * s, dz=0.15 * w - 0.55 * s, pitch=-14 * w + 14 * s * (1 - sm(u, hu, hu + 0.2)) - 8 * bump(u, hu, hu + 0.12, hu + 0.25, 0.9),
             roll=-8 * w + 24 * s, yaw=-6 * w + 12 * s)
        soft(p, 0.0, k)
        K.flex(p, l=12 * w - 15 * s, r=12 * w + 12 * s, f=10 * w - 12 * s, b=6 * w + 12 * s)
        K.ribbons(p, u, n=2, amp=5 * (1 - k), lift=-8 * w + 18 * s, spread=6 * s)
    C.clip('Attack1', 1.2, glide_pass, hit=0.55)
    C.reach('Attack1', K.fx['l'][-1], V(HALF, 0, h))
    C.reach('Attack1', K.ff[-1], V(0, -FRONT, h))

    def pulse(p, u):                           # Attack2: the corners gather upwards, then it snaps flat and past it
        hu = 0.7 / 1.5
        k = 1 - bump(u, 0.0, 0.1, 0.9, 1.0)
        g = sm(u, 0.0, hu - 0.05) * (1 - sm(u, hu - 0.03, hu + 0.02))
        b = bump(u, hu - 0.02, hu + 0.02, hu + 0.08, hu + 0.3)
        ring = bump(u, hu, hu + 0.04, hu + 0.12, 0.9)
        body(p, ib(0.0, k), dz=0.14 * g - 0.08 * b, pitch=-4 * g, yaw=14 * g * g, roll=1.5 * g * osc(u, 9))
        soft(p, 0.0, k)
        K.flex(p, l=42 * g - 16 * b + 5 * ring * osc(u, 6), f=36 * g - 14 * b + 5 * ring * osc(u, 6, 0.25), b=36 * g - 14 * b + 5 * ring * osc(u, 6, 0.5))
        p.scl('body', 1 - 0.06 * g + 0.09 * b)
        K.ribbons(p, u, n=3, amp=(4 + 10 * b) * (1 - k), lift=-14 * g + 16 * b, spread=-6 * g + 22 * b)
    C.clip('Attack2', 1.5, pulse, hit=0.7)
    C.reach('Attack2', K.ff[-1], V(0, -FRONT, h))

    def hit(p, u):
        k = bump(u, 0.0, 0.2, 0.32, 1.0)
        body(p, ib(0.0, 1 - k), dy=0.16 * k, dz=-0.08 * k, pitch=-18 * k, roll=14 * k, yaw=-8 * k)
        soft(p, 0.0, 1 - k)
        K.flex(p, l=-10 * k, r=16 * k, f=22 * k, b=-12 * k)
        K.wave(p, u, n=3, side=5 * k, fore=4 * k, lag=0.2)
        K.ribbons(p, u, n=2, amp=6 * k, lag=0.22, lift=14 * k, spread=10 * k)
    C.clip('Hit', 0.4667, hit)

    land = 0.5

    def death(p, u):                           # sinks like a falling sheet while it closes, like an eye, to a white line
        k = 1 - sm(u, 0.0, 0.15)
        tt = min(1.0, u / land)
        fall = tt ** 1.6
        on = sm(u, land - 0.03, land + 0.03)
        air = sm(u, 0.0, 0.18) * (1 - on)
        close = sm(u, 0.2, 0.8)
        sw = math.sin(TAU * 1.5 * tt)
        body(p, ib(0.0, k), dz=-(h - 0.03) * fall, dx=0.12 * sw * air, roll=12 * sw * air, pitch=-6 * air, yaw=20 * fall)
        soft(p, 0.0, k)
        K.flex(p, l=16 * air, f=10 * air, b=10 * air)
        p.scl('body', (1.0, 1.0 - 0.96 * close, 1.0))
        K.ribbons(p, u, n=2, amp=8 * air, lift=30 * air + (slope + 6 * k) * on)
    C.clip('Death', 1.8, death, floor='body', settle=(land, land + 0.12))

    def spawn(p, u):                           # a white line high up opens like an eye and settles into its glide
        k = sm(u, 0.78, 1.0)
        op = sm(u, 0.05, 0.55)
        r = sm(u, 0.15, 0.8)
        d = 1 - r
        fl = bump(u, 0.45, 0.62, 0.66, 0.95)
        body(p, ib(0.0, k), dz=1.3 * d * d - 0.07 * fl, pitch=-7 * fl, yaw=-30 * d)
        soft(p, 0.0, k)
        p.scl('body', (1.0, 0.04 + 0.96 * op, 1.0))
        K.flex(p, l=-12 * fl, f=12 * fl, b=12 * fl)
        K.ribbons(p, u, n=2, amp=7 * r * (1 - k), lift=slope * (1 - op) + 12 * fl)
    C.clip('Spawn', 1.3, spawn)
    return C.finalize(bake=bake, notes="H3: no wing beat in any clip (flex chains fx_* / ff* / fb* + ribbon chains rib_*); Death and Spawn scale the bone `body` "
                                       "along its length (the kite closes to a line); every part is skinned")
