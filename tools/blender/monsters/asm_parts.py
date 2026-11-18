"""asm_parts.py -- PARTS LIBRARY of the creature assembler (asm.py).

A part is a function  fn(A, **kwargs)  registered with @part; a spec uses it as {p: '<function name>', ...kwargs}.
It builds numpy Parts in REST space (UAL T-pose, facing -Y, Z up, +X = the creature's left) with the kit helpers and
hands them to A.add(part, bone=... | skin=n | like=Part | ride=point, look='<look name>').  A.finish() later applies the
creature's proportions / posture to everything, so a part never needs to know them.  Look names are resolved by A.lk():
a spec override or a preset of asm.PRE with that name -- parts therefore take their looks as keyword arguments with
sensible defaults.  Surfaces other parts may want to sit on are published in A.refs[name] (Parts in rest space).

HOW TO ADD A PART: write a function below (or in another module that imports `part` from here and is listed in
asm_parts.EXTRA), decorate it with @part, build Parts, call A.add().  Keep every piece seated: sink roots a few mm into
what carries them, bind rigid things to ONE bone, give cloth shells weights with skin= / like=.
"""
import math
import numpy as np
import monlib as M
import kit_bones as KB
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_c as KC3
import kit_wisp as KW
import kit_ice as KI
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, hang, arc_pts, band, unit

PARTS = {}
EXTRA = ['asm_parts2', 'asm_parts3', 'asm_parts4', 'asm_parts5', 'asm_parts6', 'asm_parts7', 'asm_parts8', 'asm_parts9']        # more part modules, imported at the bottom if they exist


def part(fn):
    PARTS[fn.__name__] = fn
    return fn


def girth_of(A, on, z):
    """(cx, cy, rx, ry) of the slice of a reference surface ('body' or a published ref) at height z"""
    if on == 'body':
        return KI.girth(A.rest, z, xlim=0.26)
    return KI.girth(A.refs[on], z)


def cyl(p0, p1, r, n=8, r1=None):
    return KC3.cyl(V(p0), V(p1), r, n=n, r1=r1)


# ================================================================================================ robes and cloth
ROBE = np.array([(0.06, 0.02, 0.285, 0.26), (0.30, 0.02, 0.25, 0.23), (0.62, 0.022, 0.215, 0.20), (0.92, 0.03, 0.182, 0.17),
                 (1.07, 0.025, 0.158, 0.142), (1.13, 0.022, 0.162, 0.146), (1.27, 0.02, 0.18, 0.158), (1.38, 0.02, 0.198, 0.165),
                 (1.45, 0.02, 0.185, 0.142), (1.51, 0.012, 0.10, 0.10)])      # z, cy, rx, ry  (the Order's robe)


@part
def robe(A, look='robe', hem=0.06, top=1.51, girth=1.0, flare=1.0, rag=0.05, pleat=0.07, n=36, seed=2, pelvis_mix=0.28,
         crumple=0.004, name='robe', waist=1.0, chest=1.0):
    """lofted gown with irregular pleats (real fold geometry) from `hem` up to `top`; girth scales it, flare widens
    the hem, waist pinches it at the belt.  Publishes A.refs[name] and A.refs[name + '_rings']."""
    zs = [hem] + [z for z in ROBE[:, 0] if hem + 0.05 < z < top - 0.012] + [top]
    secs = []
    for z in zs:
        cy, rx, ry = (np.interp(z, ROBE[:, 0], ROBE[:, k]) for k in (1, 2, 3))
        f = girth * (1 + (flare - 1) * float(M.smoothstep(1.0, 0.06, z)))
        f *= 1 + (waist - 1) * math.exp(-((z - 1.08) / 0.09) ** 2)
        f *= 1 + (chest - 1) * float(M.smoothstep(1.18, 1.32, z))        # chest < 1 tucks it under a cape / mantle
        secs.append((z, 0, cy, rx * f, ry * f))
    mod = KX.pleat_mod(seed=seed, amp=pleat, hem_z=hem, top_z=min(1.02, top - 0.05))
    rb, R = KX.gown2(secs, n=n, mod=mod, rag=rag, seed=seed)
    if crumple:
        KX.crumple(rb, crumple, 9.0, float(seed))
    rb.smooth = 180.0
    KC.skin_cloth(rb, body=A.rest, smooth=5, pelvis_mix=pelvis_mix, mix_top=0.95, mix_bot=0.3)
    A.add(rb, look=look)
    A.refs[name] = rb; A.refs[name + '_rings'] = R
    return rb


@part
def sleeves(A, look='robe', r_sh=0.08, r_el=0.066, r_cuff=0.095, droop=0.035, x1=0.725, side='both', lining=True, rag=0.0, seed=3):
    """sleeves along the arms (KC.sleeve); wide r_cuff + droop = deacon sleeves; x1 < 0.5 = cut off above the elbow"""
    for sx in A.sides(side):
        sl = KC.sleeve(r_sh=r_sh, r_el=r_el, r_cuff=r_cuff, droop=droop, x1=x1)
        if rag:
            sl.v[-14:, 0] -= KC.ragged(14, rag, seed + (sx < 0))
        A.add(sl, look=look, skin=2, flip=sx < 0)
        if lining:
            w = A.R.H('hand_l')
            c = ellipsoid(0.02, r_cuff * 0.53, r_cuff * 0.53, n=8, m=5).move(x1 - 0.036, w[1] + 0.004, w[2] - droop - 0.02)
            A.add(c, bone='lowerarm_l' if x1 > 0.47 else 'upperarm_l', look='void', flip=sx < 0)


@part
def hood(A, look='hood', style='cowl', void=True, seed=3, **kw):
    """head cover rigid on Head: cowl (KC.hood: tail, tail_drop, lift ...) | rag (KW.rag_hood) | tall (KB2.tall_cowl) |
    sack (KX.sack_hood).  void = dark ellipsoid inside the opening (no face needed)"""
    if style == 'cowl':
        kw.setdefault('tail', 0.13); kw.setdefault('tail_drop', 0.11)
        hd = KC.hood(**kw)
    elif style == 'rag':
        hd = KW.rag_hood(seed=seed, **kw)
    elif style == 'tall':
        hd = KB2.tall_cowl(**kw)
    else:
        hd, cord = KX.sack_hood(seed=seed, **kw)
        A.add(cord, bone='Head', look='rope')
    A.add(hd, bone='Head', look=look)
    A.refs['hood'] = hd
    if void:
        A.add(ellipsoid(0.085, 0.095, 0.108, n=12, m=8).move(0, 0.0, 1.675), bone='Head', look='void')


@part
def mask(A, look='mask', slit='glow', w=0.060, h=0.094, y=-0.112, z=1.664, taper=0.32, tilt=4.0, depth=0.02):
    """blank face plate that wraps the face (the Order's mask); slit = look of the vertical hairline mark or None"""
    mc = np.array([0, y, z])
    m = ellipsoid(w, depth, h, n=16, m=9)
    t = np.clip(-m.v[:, 2] / h, 0, 1)
    m.v[:, 0] *= (1 - taper * t ** 1.5)
    m.v[:, 1] += 1.1 * m.v[:, 0] ** 2 / 0.06 - 0.004 * np.exp(-(m.v[:, 0] / 0.012) ** 2)
    m.move(mc).rot('x', tilt, pivot=mc)
    A.add(m, bone='Head', look=look, smooth=70)
    if slit:
        s = box(0.0034, 0.006, h * 0.75).move(mc + np.array([0, -depth + 0.0005, 0.012])).rot('x', tilt, pivot=mc)
        A.add(s, bone='Head', look=slit)


@part
def capelet(A, look='hood', z_top=1.585, z_bot=1.375, r_top=(0.092, 0.105), r_bot=(0.29, 0.19), rag=0.045, seed=3, name='mantle',
            n=28, rows=5):
    """short shoulder mantle from the neck over the shoulders; publishes A.refs[name]"""
    cp = KC.capelet(z_top=z_top, z_bot=z_bot, r_top=r_top, r_bot=r_bot, rag=rag, seed=seed, n=n, rows=rows)
    A.add(cp, look=look, skin=3)
    A.refs[name] = cp
    return cp


@part
def poncho(A, look='madder', seed=11, rag=0.2, point=0.10, holes=0.16, name='mantle', **kw):
    """cloth draped over shoulders and upper arms (KW.poncho) with punched holes; publishes A.refs[name]"""
    sh, G = KW.poncho(seed=seed, rag=rag, point=point, **kw)
    if holes:
        sh = KW.punch(sh, G.shape, holes, seed=seed % 7 + 1, t0=0.6)
    A.add(sh, look=look, skin=3)
    A.refs[name] = sh
    return sh


@part
def cape(A, look='rag', width=0.36, length=1.0, z_top=1.46, y=0.13, rag=0.25, holes=0.12, tatter=0.15, seed=5, flare=0.18):
    """back cape hanging from the shoulders (spine -> pelvis -> thighs by height)"""
    p = KC.cape(width=width, length=length, z_top=z_top, y=y, rag=rag, holes=holes, tatter=tatter, seed=seed, flare=flare)
    KC.cape_weights(p, z_top=z_top)
    A.add(p, look=look)


@part
def rags(A, look='rag', z=0.99, arcs=((-128, -52, 0.36), (60, 125, 0.30)), rx=0.125, ry=0.10, cy=0.015, holes=0.3, seed=2):
    """torn strips hanging from the hips: arcs = [(angle0, angle1, length)] in degrees (-90 = front, 90 = back)"""
    for k, (a0, a1, ln) in enumerate(arcs):
        top, nr = arc_pts((0, cy, z), rx, ry, a0, a1, max(4, int(abs(a1 - a0) / 13)))
        r = hang(top, ln, rows=6, out=nr, flare=0.025, rag=0.4, seed=seed + 5 * k, wave=0.012, holes=holes, tatter=0.25)
        am = math.radians((a0 + a1) / 2)
        th = 'thigh_l' if math.cos(am) > 0 else 'thigh_r'
        M.weights_by(r, r.v[:, 2], [(z - ln + 0.06, {th: 0.75, 'pelvis': 0.25}), (z - 0.01, {'pelvis': 1.0})])
        A.add(r, look=look)


@part
def shell(A, look='rag', regions=('torso',), z0=-1.0, z1=3.0, offset=0.011, noise=0.0, freq=9.0, seed=3.0, x0=None, helix=None,
          sink=True):
    """skin-tight layer made from the body's own faces (bandages, hose, shirts, bindings): regions / bones, a height
    range with a ragged border (noise), optionally a helical bandage mask helix=(bone, turns, duty)"""
    b = A.rest
    nz = np.array([M.fbm(p, freq, seed) for p in b.v]) if noise else np.zeros(len(b.v))
    from asm import region_bones
    m = (b.wsum(region_bones(regions)) > 0.5) & (b.v[:, 2] > z0 + noise * nz) & (b.v[:, 2] < z1 + noise * nz)
    if x0 is not None:
        m &= np.abs(b.v[:, 0]) > x0
    if helix:
        m &= KI.helix_mask(b, helix[0], helix[1], duty=helix[2] if len(helix) > 2 else 0.6, jitter=0.15, seed=seed)
    if not m.any():
        return None
    p = KX.patch_shell(b, m, offset=offset) if sink else KF.shell(b, m, offset)
    A.add(p, look=look, smooth=180)
    return p


@part
def foot_wraps(A, look='hood', z=0.22):
    """cloth-bound feet (inflated copy of the body's feet and ankles)"""
    bd = A.rest
    ft = bd.sub(bd.vmask_faces((bd.wsum(['foot_l', 'foot_r', 'ball_l', 'ball_r', 'calf_l', 'calf_r']) > 0.5) & (bd.v[:, 2] < z), 'all'))
    ft.inflate(0.005)
    ft.v[:, 2] = np.maximum(ft.v[:, 2], 0.0)
    A.add(ft, look=look)


# ================================================================================================ belts, straps, hung things
@part
def rope_belt(A, look='rope', z=1.085, on='robe', pad=0.02, r=0.013, tail=0.36, side=1):
    """rope round the waist with a knot and two hanging tails"""
    cx, cy, rx, ry = girth_of(A, on, z) if on in A.refs or on == 'body' else (0, 0.024, 0.158, 0.138)
    loop, tails = KC.rope_belt(z=z, rx=rx + pad, ry=ry + pad, cy=cy, r=r, tail=tail, side=side)
    A.add(loop, bone='spine_01', look=look)
    M.weights_by(tails, tails.v[:, 2], [(z - tail, {'thigh_l' if side > 0 else 'thigh_r': 0.6, 'pelvis': 0.4}), (z - 0.085, {'pelvis': 1.0}),
                                        (z + 0.005, {'spine_01': 1.0})])
    A.add(tails, look=look)


@part
def belt(A, look='strap', z=1.043, w=0.036, th=0.009, on='body', pad=0.008, tilt=4.0, buckle='rust', bone='pelvis'):
    """flat belt sized to the body (or a published surface) at height z, with a bevelled buckle"""
    cx, cy, rx, ry = girth_of(A, on, z)
    A.add(band((0, cy, z), rx + pad, ry + pad, w, th, tilt_y=tilt, n=22), bone=bone, look=look)
    if buckle:
        A.add(box(0.05, 0.012, w * 1.35, bevel=0.006).move(0.02, cy - ry - pad - 0.004, z), bone=bone, look=buckle)
    A.refs['belt'] = (cx, cy, rx + pad, ry + pad, z)


@part
def bandolier(A, look='strap', z=1.29, tilt=-30.0, w=0.04, on='body', pad=0.012, items=None, count=6, item_look='wood',
              tip_look='linen', bone='spine_03', rx=None, ry=None):
    """diagonal chest strap (high on the creature's left for tilt < 0); items: 'bolts' (fletched crossbow bolts),
    'coil' (a thick roll of wet shroud instead of a strap), 'vials'"""
    cx, cy, gx, gy = girth_of(A, on, z)
    rx = (gx if rx is None else rx) + pad; ry = (gy if ry is None else ry) + pad
    if items == 'coil':
        a = np.radians(np.linspace(0, 360, 26, endpoint=False))
        pts = np.stack([rx * np.cos(a), ry * np.sin(a), np.zeros(len(a))], axis=1) @ M.rot_axis('y', tilt).T + V(0, cy, z)
        for k in range(3):
            q = pts + V(0, 0, 0.026 * (k - 1)) + np.stack([np.cos(a), np.sin(a), 0 * a], axis=1) * (0.012 if k == 1 else 0.0)
            A.add(tube(q, 0.019, n=6, closed=True), bone=bone, look=look, smooth=60)
        return
    A.add(band((0, cy, z), rx, ry, w, 0.008, tilt_y=tilt, n=24), bone=bone, look=look)
    if items:
        Rm = M.rot_axis('y', tilt)
        up = Rm @ V(0, 0, 1)
        for a in np.linspace(-150, -38, count):
            ar = math.radians(a)
            p = Rm @ V(rx * math.cos(ar), ry * math.sin(ar), 0) + V(0, cy, z)
            out = unit(V(math.cos(ar) / rx, math.sin(ar) / ry, 0))
            p = p + out * 0.012
            if items == 'bolts':
                A.add(cyl(p - up * 0.075, p + up * 0.06, 0.0055, n=5), bone=bone, look=item_look)
                A.add(box(0.004, 0.022, 0.045).tf(M.rot_to((0, 1, 0), out)).move(p + up * 0.05), bone=bone, look=tip_look)
            elif items == 'lanterns':               # thumb-sized dead lanterns strung on the strap
                for q, lk in item_parts(A, 'lantern', 0.34, lit=False):
                    A.add(q.move(p + V(0, 0, -0.012)), bone=bone, look=lk, smooth=50)
            else:
                A.add(revolve([(0.0, -0.05), (0.013, -0.045), (0.015, 0.0), (0.008, 0.02), (0.008, 0.035), (0.0, 0.037)], n=6)
                      .tf(M.rot_to((0, 0, 1), up)).move(p), bone=bone, look=item_look, smooth=60)


def item_parts(A, kind, size=1.0, lit=True):
    """hanging props, hook at the origin, body along -Z: [(Part, look)]"""
    s = size
    if kind == 'lantern':
        mt, gl = KB2.lantern(h=0.22 * s, r=0.062 * s)
        return [(mt, 'iron'), (gl, 'glow' if lit else 'void')]
    if kind == 'bell':
        b, c = KB2.bell(h=0.12 * s, R=0.055 * s)
        return [(b, 'brass'), (c, 'iron')]
    if kind == 'censer':
        r = 0.06 * s
        bowl = revolve([(0.0, -2.05 * r), (0.45 * r, -2.0 * r), (0.95 * r, -1.55 * r), (1.0 * r, -1.1 * r), (0.9 * r, -0.95 * r)], n=12)
        lid = revolve([(0.92 * r, -0.9 * r), (0.8 * r, -0.55 * r), (0.45 * r, -0.2 * r), (0.16 * r, -0.05 * r), (0.1 * r, 0.03), (0.0, 0.035)], n=12)
        ember = revolve([(0.9 * r, -0.96 * r), (0.93 * r, -0.925 * r), (0.9 * r, -0.89 * r)], n=12, cap=False)
        ch = [cyl((0.86 * r * math.cos(a), 0.86 * r * math.sin(a), -1.0 * r), (0, 0, 0.03), 0.0035, n=4) for a in (0.5, 2.6, 4.7)]
        foot = revolve([(0.0, -2.3 * r), (0.5 * r, -2.28 * r), (0.3 * r, -2.05 * r)], n=8)
        return [(bowl, 'silver'), (lid, 'silver'), (join(ch + [foot]), 'silver'), (ember, 'glow' if lit else 'void')]
    if kind == 'keys':
        return [(KX.key_ring(4, 0.045 * s, seed=3), 'iron')]
    if kind == 'bladder':
        return [(KX.bladder(0.03 * s), 'hide')]
    if kind == 'fishbone':
        return [(KX.fishbone(0.17 * s), 'bone')]
    if kind == 'seal':                                  # lead seal: a stamped disc
        d = KC3.disc(0.035 * s, 0.012 * s, n=10, bevel=0.003).move(0, 0, -0.04 * s)
        return [(d, 'lead'), (cyl((0, 0, 0), (0, 0, -0.012), 0.004, n=4), 'lead')]
    if kind == 'skull':
        P = KB.skull_parts(8.0, teeth=False)
        sk = join([P['skull'], P['jaw']]).scale(0.62 * s).rot('x', -12).move(0, 0, -0.085 * s)
        return [(sk, 'bone')]
    if kind == 'pouch':
        return [(ellipsoid(0.04 * s, 0.028 * s, 0.05 * s, n=8, m=6).move(0, 0, -0.055 * s), 'hide')]
    if kind == 'bones':
        ps = [KB.seg((dx, 0, -0.01), (dx * 1.6, 0.004 * k, -0.01 - L_), 0.0065, 0.0065, 1.5, 1.5, n=5)
              for k, (dx, L_) in enumerate(((-0.014, 0.085), (0.0, 0.11), (0.015, 0.075)))]
        return [(join(ps), 'bone')]
    if kind == 'lid':                                   # urn lid hanging by its knob
        return [(revolve([(0.0, -0.04 * s), (0.07 * s, -0.047 * s), (0.105 * s, -0.06 * s), (0.11 * s, -0.07 * s), (0.0, -0.072 * s)], n=12), 'clay'),
                (ellipsoid(0.018 * s, 0.018 * s, 0.024 * s, n=7, m=5).move(0, 0, -0.026 * s), 'clay')]
    raise KeyError("unknown item %r" % kind)


@part
def hang_item(A, item='lantern', bone='spine_01', at=(0.12, -0.12, 1.04), cord=0.08, cord_look='rope', size=1.0, lit=True, tilt=0.0,
              looks=None, cord_r=0.004):
    """an item hanging on a cord from a point; everything rigid on `bone` (it does not swing).  looks = {default look:
    replacement} re-colours the item"""
    at = V(at)
    if cord > 0:
        A.add(cyl(at, at - V(0, 0, cord), cord_r, n=4), bone=bone, look=cord_look)
    for p, lk in item_parts(A, item, size, lit):
        p.rot('x', tilt).move(at - V(0, 0, cord))
        A.add(p, bone=bone, look=(looks or {}).get(lk, lk), smooth=50)


@part
def chain(A, path, look='rust', bone='pelvis', link=0.05, r=0.008, final=False, noground=False):
    """chain of torus links along a polyline, rigid on one bone"""
    c = M.chain(np.asarray(path, float), link=link, r=r)
    A.add(c, bone=bone, look=look, smooth=60, final=final, noground=noground)


@part
def candles(A, on='mantle', look='wax', spots=((0.150, 0.03, 0.06), (0.212, 0.05, 0.034), (-0.170, 0.02, 0.045)), runs=True, r=0.015):
    """candle stubs with wax runs standing on a published surface (shoulders of a mantle)"""
    ref = A.refs[on]
    for sd, (x, y, h) in enumerate(spots, 1):
        z0 = KC.rest_on(ref, x, y)
        if z0 is None:
            continue
        z = z0 - 0.004
        c, wick = KC.candle(r, h, seed=sd)
        bn = 'clavicle_l' if x > 0 else 'clavicle_r'
        A.add(c.move(x, y, z), bone=bn, look=look, smooth=50)
        A.add(wick.move(x, y, z), bone=bn, look='wick')
        sx = 1 if x > 0 else -1
        for (dx, dy) in ((0.05, -0.085), (0.05, 0.07), (0.075, -0.01)) if runs else ():
            pts = []
            for s in np.linspace(0, 1, 5):
                px = x + sx * dx * s; py = y + dy * s
                zz = KC.rest_on(ref, px, py)
                pts.append((px, py, (zz if zz is not None else z - 0.1) + 0.003))
            d = tube(np.array(pts), [0.013, 0.009, 0.007, 0.008, 0.012], n=5, r2=[0.006, 0.005, 0.004, 0.005, 0.007], up=(0, 0, 1))
            A.add(d, bone=bn, look=look, smooth=60)


# ================================================================================================ armour pieces
@part
def kettle_cap(A, look='rust', z=1.752, tilt=(7, -6), dents=True, scale=1.0):
    """dented kettle hat with a bent brim"""
    prof = [(0.132, 0.0), (0.135, 0.005), (0.104, 0.017), (0.094, 0.028), (0.088, 0.046), (0.07, 0.066), (0.04, 0.079), (0.0, 0.084)]
    cap = revolve(prof, n=16).scale((0.93 * scale, 1.08 * scale, scale))
    if dents:
        d = np.linalg.norm(cap.v - np.array([0.045, -0.04, 0.07]), axis=1)
        cap.v[:, 2] -= np.exp(-(d / 0.035) ** 2) * 0.016
        d = np.linalg.norm(cap.v - np.array([-0.11, 0.05, 0.0]), axis=1)
        cap.v[:, 2] -= np.exp(-(d / 0.06) ** 2) * 0.022
    cap.rot('y', tilt[0]).rot('x', tilt[1]).move(0.004, -0.004, z)
    A.add(cap, bone='Head', look=look, smooth=38)


@part
def pauldron(A, look='rust', side='l', lift=0.0, out=0.0, scale=1.0, spike=True, lames=2):
    """domed shoulder plates (clavicle + upper arm) with a rolled rim and an optional spike"""
    for sx in A.sides(side):
        dome = revolve([(0.0, 0.052), (0.03, 0.048), (0.062, 0.034), (0.082, 0.012), (0.088, 0.0), (0.080, -0.004)], n=12).scale((scale, 0.92 * scale, scale))
        A.add(dome.copy().rot('y', 22).move(0.196 + out, 0.052, 1.474 + lift), bone='clavicle_l', look=look, smooth=38, flip=sx < 0)
        if lames >= 2:
            A.add(dome.copy().scale((0.82, 0.86, 0.75)).rot('y', 52).move(0.262 + out, 0.056, 1.452 + lift), bone='upperarm_l', look=look,
                  smooth=38, flip=sx < 0)
            A.add(torus(0.066 * scale, 0.006, n=12, m=4, ry=0.058 * scale).rot('y', 52).move(0.262 + out, 0.056, 1.452 + lift),
                  bone='upperarm_l', look=look, flip=sx < 0)
        if spike:
            A.add(M.cone_spike(0.018, 0.06, n=6).rot('y', 22).move(0.215 + out, 0.052, 1.52 + lift), bone='clavicle_l', look=look, smooth=30,
                  flip=sx < 0)


@part
def bracer(A, look='strap', side='r', r=0.036, t0=0.30, t1=0.86, lip=0.004, bone='lowerarm'):
    """tube plate / wrap round a limb segment (forearm by default; bone='calf' = greave, 'upperarm', 'thigh')"""
    for sx in A.sides(side):
        bn = bone + ('_l' if sx > 0 else '_r')
        st = [(t0, r * 0.98, r * 1.06), ((t0 + t1) / 2, r * 0.92, r), (t1, r * 0.80, r * 0.88)]
        A.add(KC3.limb_plate(bn, st, n=9, lip=lip), bone=bn, look=look, smooth=50)


@part
def collar(A, look='rust', z=1.535, r=0.082, w=0.03, ring=True, bone='neck_01', cy=-0.012):
    """iron collar round the neck with a hasp and an optional ring at the front"""
    A.add(band((0, cy, z), r, r * 1.08, w, 0.009, tilt_x=-8, n=18), bone=bone, look=look)
    A.add(box(0.03, 0.016, w * 1.2, bevel=0.004).move(0, cy - r * 1.08 - 0.004, z - 0.003), bone=bone, look=look)
    if ring:
        A.add(torus(0.022, 0.005, n=10, m=4).rot('x', 90).rot('x', -25).move(0, cy - r * 1.08 - 0.018, z - 0.03), bone=bone, look=look)


# ================================================================================================ growths
@part
def crystals(A, bone='clavicle_l', at=(0.2, 0.02, 1.5), axis=(0.3, 0, 1), count=5, h=(0.08, 0.2), r=(0.02, 0.035), look='ice', seed=1,
             spread=38.0, big=1.0):
    """cluster of faceted crystals growing out of a point (ice, salt, glass)"""
    for k, c in enumerate(KI.cluster(V(at), axis=axis, count=count, spread=spread, h=h, r=r, seed=seed, big=big)):
        A.add(c, bone=bone, look=look, smooth=20)


@part
def icicles(A, bone='lowerarm_l', a=(0.5, 0.07, 1.41), b=(0.72, 0.07, 1.41), count=5, length=0.09, look='ice2', seed=1):
    """row of icicles hanging from a line"""
    pts = KI.line_pts(V(a), V(b), count, jit=0.01, seed=seed)
    for p in KI.fringe(pts, (0, 0, -1), length, seed=seed):
        A.add(p, bone=bone, look=look, smooth=20)


@part
def horns(A, look='horn', style='nub', side='both', length=0.12, r=0.022, at=(0.062, -0.035, 1.755), sweep=(0.6, -0.2, 1.0),
          curl=0.5, n=6):
    """a pair of horns on the skull: nub | curved (sweep = growth direction, curl bends them inwards / forwards)"""
    for sx in A.sides(side):
        p0 = V(at)
        d = unit(V(sweep))
        if style == 'nub':
            pts = [p0 - d * 0.015, p0 + d * length * 0.5, p0 + d * length]
        else:
            c1 = p0 + d * length * 0.45
            c2 = c1 + unit(d + V(-0.6 * curl, -0.5 * curl, 0.3)) * length * 0.4
            c3 = c2 + unit(V(-0.9 * curl, -0.6 * curl, 0.6)) * length * 0.3
            pts = M.polyline([p0 - d * 0.02, c1, c2, c3], 9)
        A.add(M.horn(np.array(pts), r, n=n), bone='Head', look=look, smooth=50, flip=sx < 0)


for _m in EXTRA:
    try:
        __import__(_m)
    except ModuleNotFoundError as e:
        if e.name != _m:
            raise
