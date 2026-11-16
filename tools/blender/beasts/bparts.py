"""Beast kit: extra material looks (registered into the monsters kit's looks.KINDS) and the geometry part library."""
import math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import blib as B
from blib import M, L, V, unit, rot_axis

_c = L._c
TAU = 2 * math.pi


# ================================================================================================ looks
def _across(N, vec, axis, squash=0.04):
    """coords that vary only ALONG axis (bands / rings across it)"""
    q = Vector(axis).normalized().rotation_difference(Vector((0, 0, 1)))
    a = N.mapping(vec, rot=tuple(q.to_euler()))
    return N.mapping(a, scale=(squash, squash, 1))


def _ybands(N, bands, soft=0.012):
    """mask = 1 inside any of the object-space Y intervals [(lo, hi), ...], edges broken up by noise"""
    _, y, _ = N.sep(N.coord())
    y = N.math('ADD', y, N.math('MULTIPLY', N.math('SUBTRACT', N.noise(14, 3, 0.6), 0.5), 0.03))
    m = None
    for lo, hi in bands:
        w = N.math('MULTIPLY', N.mapr(y, lo - soft, lo + soft), N.mapr(y, hi + soft, hi - soft))
        m = w if m is None else N.math('MAXIMUM', m, w)
    return m


def k_chitin(N, p):
    """insect / crab shell: dark glossy plates, round pits, long growth ridges along `axis`, fine granules, pale
    rubbed edges, dust on top. bands=[(y0,y1)..] + band_color paint cross bands (burying-beetle orange)"""
    col = _c(p, 'color', (0.03, 0.027, 0.025))
    col2 = _c(p, 'color2', tuple(min(1.0, x * 2.0) for x in col))
    edgec = _c(p, 'edge_color', (0.20, 0.15, 0.10))
    r0 = _c(p, 'rough', 0.3)
    axis = _c(p, 'axis', (0, 1, 0))
    P = N.coord()
    big = N.noise(_c(p, 'scale', 6.0), 4, 0.6)
    mid = N.noise(24, 3, 0.6)
    pit = N.mapr(N.voro(_c(p, 'pit', 130.0), 'F1'), 0.0, 0.33, 1.0, 0.0)
    pit = N.math('MULTIPLY', pit, N.mapr(N.noise(8, 3, 0.6, vec=N.mapping(P, loc=(3.1, 4.2, 5.3))), 0.3, 0.62, 0.25, 1.0))
    ridge = N.noise(_c(p, 'ridge', 60.0), 2, 0.5, vec=N.align(N.warp(0.012, 7.0), axis, 0.05))
    gran = N.voro(_c(p, 'grain', 210.0), 'SMOOTH_F1', smooth=0.5)
    sc = N.scan('obsidian', size=0.6, relief=0.14)
    wear = N.scan('leather_old', size=0.5, relief=0.16)
    edge = N.edge(0.28, 0.6, 0.3)
    c = N.mix(N.mapr(big, 0.35, 0.7), col, col2)
    if p.get('bands'):
        bm = _ybands(N, p['bands'])
        bc = _c(p, 'band_color', (0.42, 0.16, 0.03))
        c = N.mix(bm, c, N.mix(N.mapr(mid, 0.3, 0.75), bc, N.mul(bc, 0.55)))
    c = N.mix(N.mapr(mid, 0.5, 0.78, 0.0, 0.35), c, N.mul(c, 0.5))
    c = N.detail(c, sc['det'], 0.6)
    c = N.detail(c, wear['det'], 0.5)
    c = N.shade(c, N.mapr(ridge, 0.35, 0.6, 0.3, 0.0), 0.6)
    c = N.mix(N.math('MULTIPLY', pit, 0.7), c, N.mul(col, 0.3))
    c = N.mix(N.math('MULTIPLY', edge, _c(p, 'edge', 0.6)), c, edgec)
    dust = N.math('MULTIPLY', N.upfacing(0.4, 0.95), N.mapr(N.noise(10, 4, 0.7), 0.35, 0.7))
    c = N.mix(N.math('MULTIPLY', dust, _c(p, 'dust', 0.35)), c, _c(p, 'dust_color', (0.16, 0.14, 0.12)))
    rough = N.math('ADD', N.mapr(mid, 0.25, 0.75, r0 - 0.1, r0 + 0.22), N.math('MULTIPLY', sc['dr'], 0.4))
    rough = N.mixf(pit, rough, 0.8)
    rough = N.mixf(N.math('MULTIPLY', dust, 0.9), rough, 0.92)
    rough = N.mixf(N.math('MULTIPLY', edge, 0.5), rough, 0.45)
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.96), 0.12)
    H = [(ridge, _c(p, 'ridge_h', 0.9)), (pit, -0.7), (gran, 0.35), (sc['h'], 1.0), (wear['h'], 1.0), (N.noise(16, 2, 0.5), 1.6)]
    return L._out(N, p, c, rough, _c(p, 'metal', 0.0), H, 0.4)


def k_scales(N, p):
    """reptile hide: domed scales with dark gaps, bigger keeled scales in patches, dry scuffed tops, mud low down"""
    col = _c(p, 'color', (0.16, 0.14, 0.09))
    dark = _c(p, 'dark', tuple(x * 0.3 for x in col))
    lite = _c(p, 'light', tuple(min(1.0, x * 1.7) for x in col))
    s = _c(p, 'scale', 62.0)
    wv = N.warp(0.02, 9)
    e1 = N.voro(s, 'DISTANCE_TO_EDGE', vec=wv)
    f1 = N.voro(s, 'F1', vec=wv)
    e2 = N.voro(s * 0.42, 'DISTANCE_TO_EDGE', vec=wv)
    f2 = N.voro(s * 0.42, 'F1', vec=wv)
    patch = N.mapr(N.noise(5.5, 3, 0.6), 0.5, 0.62)                    # where the big keeled scales grow
    gap = N.mixf(patch, N.mapr(e1, 0.0, 0.07, 1.0, 0.0), N.mapr(e2, 0.0, 0.06, 1.0, 0.0))
    dome = N.mixf(patch, N.mapr(f1, 0.0, 0.55, 1.0, 0.0), N.mapr(f2, 0.0, 0.5, 1.0, 0.0))
    sc = N.scan('scales', size=0.4, relief=0.3)
    big = N.noise(_c(p, 'blotch', 4.5), 4, 0.6)
    mid = N.noise(21, 3, 0.6)
    c = N.mix(N.mapr(big, 0.38, 0.66), col, _c(p, 'color2', tuple(x * 0.55 for x in col)))
    c = N.mix(N.mapr(mid, 0.55, 0.8, 0.0, 0.4), c, lite)
    c = N.detail(c, sc['det'], 0.8)
    c = N.mix(N.math('MULTIPLY', gap, 0.85), c, dark)
    c = N.mix(N.math('MULTIPLY', N.mapr(dome, 0.6, 1.0), 0.3), c, lite)
    low = N.math('SUBTRACT', 1.0, N.height_z(_c(p, 'mud_lo', 0.0), _c(p, 'mud_hi', 0.25)))
    c = N.mix(N.math('MULTIPLY', low, N.mapr(N.noise(9, 3, 0.6), 0.25, 0.7, 0.3, 0.9)), c, _c(p, 'mud', (0.09, 0.075, 0.06)))
    r0 = _c(p, 'rough', 0.55)
    rough = N.math('ADD', N.mapr(mid, 0.3, 0.75, r0 - 0.15, r0 + 0.15), N.math('MULTIPLY', sc['dr'], 0.5))
    rough = N.mixf(gap, rough, 0.92)
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.97), 0.18)
    H = [(dome, _c(p, 'dome', 1.6)), (gap, -0.9), (sc['h'], 1.0), (N.noise(14, 2, 0.5), 2.0)]
    return L._out(N, p, c, rough, 0.0, H, 0.5)


def k_slime(N, p):
    """wet mucous skin (leeches, eels, toads, grubs): annular folds across `axis` (ring=0 for none), warts, a glossy
    film broken by matte drying patches, mottling, dirt in the folds"""
    col = _c(p, 'color', (0.10, 0.09, 0.075))
    col2 = _c(p, 'color2', tuple(x * 0.45 for x in col))
    belly = _c(p, 'belly', None)
    axis = _c(p, 'axis', (0, 1, 0))
    ringf = _c(p, 'ring', 26.0)
    P = N.coord()
    big = N.noise(_c(p, 'scale', 5.0), 4, 0.62)
    mid = N.noise(22, 3, 0.6)
    hide = N.scan('hide', size=0.4, relief=_c(p, 'pore', 0.2), res=512)
    wart = N.mapr(N.voro(_c(p, 'wart', 70.0), 'SMOOTH_F1', vec=N.warp(0.03, 9), smooth=0.6), 0.0, 0.4, 1.0, 0.0)
    wart = N.math('MULTIPLY', wart, N.mapr(N.noise(6, 3, 0.6, vec=N.mapping(P, loc=(2.2, 7.1, 1.3))), 0.4, 0.65))
    c = N.mix(N.mapr(big, 0.36, 0.68), col, col2)
    c = N.mix(N.mapr(mid, 0.55, 0.8, 0.0, 0.4), c, N.mul(col, 1.6))
    if belly is not None:
        _, _, nz = N.sep(N.geo().outputs['Normal'])
        c = N.mix(N.mapr(nz, -0.1, -0.75), c, belly)
    c = N.detail(c, hide['det'], 0.9)
    H = [(hide['h'], 1.0), (wart, _c(p, 'wart_h', 1.2)), (N.noise(13, 2, 0.5), 2.2)]
    fold = None
    if ringf:
        rn = N.noise(ringf, 2, 0.5, vec=_across(N, N.warp(0.01, 5.0), axis, 0.03))
        fold = N.mapr(N.math('ABSOLUTE', N.math('SUBTRACT', rn, 0.5)), 0.0, 0.09, 1.0, 0.0)      # thin grooves between rings
        c = N.shade(c, N.math('MULTIPLY', fold, 0.7), 0.4)
        H += [(fold, -_c(p, 'ring_h', 1.4)), (rn, 1.2)]
    c = N.mix(N.math('MULTIPLY', wart, 0.35), c, N.mul(col, 1.5))
    dry = N.mapr(N.noise(4.2, 3, 0.6, vec=N.mapping(P, loc=(6.3, 1.1, 8.6))), 0.52, 0.68)
    dry = N.math('MULTIPLY', dry, _c(p, 'dry', 0.5))
    c = N.mix(N.math('MULTIPLY', dry, 0.3), c, N.mix(0.5, col, (0.3, 0.29, 0.26)))
    r0 = _c(p, 'rough', 0.2)
    rough = N.math('ADD', N.mapr(mid, 0.3, 0.75, r0 - 0.06, r0 + 0.18), N.math('MULTIPLY', hide['dr'], 0.5))
    rough = N.mixf(dry, rough, 0.8)
    if fold is not None:
        rough = N.mixf(N.math('MULTIPLY', fold, 0.6), rough, 0.7)
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.95), 0.08)
    return L._out(N, p, c, rough, 0.0, H, 0.45)


def k_bfur(N, p):
    """pelt lying along `flow` (object-space direction of the hair): strands, parted locks, lighter tips, optional
    clots of ash (ash=0..1, ash_color) and frost / snow on top (frost=0..1)"""
    col = _c(p, 'color', (0.12, 0.10, 0.08))
    tipc = _c(p, 'tip', tuple(min(1.0, x * 2.2) for x in col))
    flow = _c(p, 'flow', (0, 0.8, -0.6))
    st = N.noise(_c(p, 'scale', 60.0), 3, 0.6, vec=N.align(N.warp(0.02, 9), flow, 0.05))
    st2 = N.noise(24, 3, 0.6, vec=N.align(N.coord(), flow, 0.09))
    lock = N.voro(_c(p, 'lock', 30.0), 'SMOOTH_F1', vec=N.align(N.warp(0.04, 8), flow, 0.28), smooth=0.6)
    fl = N.scan('fleece', size=0.5, relief=0.45)
    big = N.noise(5, 3, 0.6)
    c = N.mix(N.mapr(st, 0.35, 0.7), N.mul(col, 0.4), col)
    c = N.mix(N.mapr(st2, 0.5, 0.8, 0.0, 0.7), c, tipc)
    c = N.shade(c, N.mapr(lock, 0.5, 0.15, 0.0, 0.55), 0.4)
    c = N.detail(c, fl['det'], 0.7)
    c = N.mix(N.mapr(big, 0.4, 0.75, 0.0, 0.45), c, N.mul(c, 0.45))
    H = [(st, 1.4), (st2, 2.2), (N.math('SUBTRACT', 0.6, lock), 4.0), (fl['h'], 1.0)]
    rough = N.math('ADD', _c(p, 'rough', 0.88) - 0.12, N.mapr(st2, 0.3, 0.7, 0.0, 0.2))
    ash = _c(p, 'ash', 0.0)
    if ash > 0:
        cl = N.mapr(N.voro(34, 'SMOOTH_F1', vec=N.warp(0.05, 11), smooth=0.5), 0.42, 0.12)
        cl = N.math('MULTIPLY', cl, N.mapr(N.noise(7, 3, 0.6, vec=N.mapping(N.coord(), loc=(4.4, 2.2, 7.7))), 0.35, 0.6))
        am = N.math('MULTIPLY', cl, ash)
        c = N.mix(am, c, _c(p, 'ash_color', (0.27, 0.26, 0.25)))
        rough = N.mixf(am, rough, 0.98)
        H.append((cl, 2.5 * ash))
    fr = _c(p, 'frost', 0.0)
    if fr > 0:
        up = N.math('MULTIPLY', N.upfacing(0.1, 0.8), N.mapr(N.noise(12, 4, 0.7), 0.2, 0.6))
        c = N.mix(N.math('MULTIPLY', up, fr), c, (0.80, 0.86, 0.90))
    return L._out(N, p, c, rough, 0.0, H, 0.9)


def k_membrane(N, p):
    """wing / fin / ear membrane: thin hide, raised branching veins, fine cross wrinkles, darker torn rims"""
    col = _c(p, 'color', (0.09, 0.07, 0.065))
    vein = _c(p, 'vein', tuple(x * 0.45 for x in col))
    hide = N.scan('hide', size=0.35, relief=0.2, res=512)
    old = N.scan('leather_old', size=0.6, relief=0.22)
    vd = N.voro(_c(p, 'vein_scale', 9.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.1, 4))
    vm = N.mapr(vd, 0.0, 0.03, 1.0, 0.0)
    vd2 = N.voro(_c(p, 'vein_scale', 9.0) * 3.1, 'DISTANCE_TO_EDGE', vec=N.warp(0.06, 9))
    vm2 = N.math('MULTIPLY', N.mapr(vd2, 0.0, 0.04, 1.0, 0.0), 0.5)
    wr = N.noise(48, 2, 0.5, vec=N.mapping(N.warp(0.02, 6), scale=(1, 5, 1)))
    wrl = N.mapr(N.math('ABSOLUTE', N.math('SUBTRACT', wr, 0.5)), 0.0, 0.06, 1.0, 0.0)
    big = N.noise(6, 4, 0.6)
    c = N.mix(N.mapr(big, 0.35, 0.7), col, N.mul(col, 1.7))
    c = N.detail(c, hide['det'], 0.9)
    c = N.detail(c, old['det'], 0.5)
    c = N.mix(N.math('MAXIMUM', N.math('MULTIPLY', vm, 0.8), vm2), c, vein)
    c = N.shade(c, N.math('MULTIPLY', wrl, 0.4), 0.6)
    hem = N.mapr(N.hem(), 0.0, 0.03, 1.0, 0.0)
    c = N.mix(N.math('MULTIPLY', hem, 0.6), c, N.mul(col, 0.3))
    r0 = _c(p, 'rough', 0.5)
    rough = N.math('ADD', N.mapr(big, 0.3, 0.7, r0 - 0.12, r0 + 0.15), N.math('MULTIPLY', hide['dr'], 0.6))
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.95), 0.2)
    H = [(hide['h'], 1.0), (old['h'], 1.0), (vm, 1.1), (vm2, 0.5), (wrl, -0.35), (N.noise(20, 2, 0.5), 1.5)]
    return L._out(N, p, c, rough, 0.0, H, 0.4)


def k_chalk(N, p):
    """the Pale Hush: dead-matte white like unfired porcelain - hairline crazing, faint grey bruises, fine tooth"""
    col = _c(p, 'color', (0.74, 0.74, 0.73))
    shade = _c(p, 'shade', (0.50, 0.51, 0.54))
    pl = N.scan('plaster', size=0.6, relief=0.16)
    big = N.noise(3.5, 4, 0.6)
    mid = N.noise(17, 3, 0.6)
    cr = N.voro(_c(p, 'crack', 38.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.05, 9))
    crack = N.math('MULTIPLY', N.mapr(cr, 0.0, 0.035, 1.0, 0.0), N.mapr(N.noise(5, 3, 0.6, vec=N.mapping(N.coord(), loc=(3, 5, 7))), 0.4, 0.62))
    c = N.mix(N.mapr(big, 0.4, 0.72, 0.0, 0.8), col, shade)
    c = N.mix(N.mapr(mid, 0.55, 0.8, 0.0, 0.25), c, N.mul(shade, 0.8))
    c = N.detail(c, pl['det'], 0.7)
    c = N.mix(N.math('MULTIPLY', crack, 0.75), c, N.mul(shade, 0.35))
    c = N.mix(N.math('MULTIPLY', N.edge(0.3, 0.62, 0.2), 0.5), c, (0.9, 0.9, 0.9))
    r0 = _c(p, 'rough', 0.72)
    rough = N.math('ADD', N.mapr(mid, 0.3, 0.75, r0 - 0.1, r0 + 0.15), N.math('MULTIPLY', pl['dr'], 0.5))
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.98), 0.3)
    H = [(pl['h'], 1.0), (crack, -0.5), (N.noise(11, 2, 0.5), 1.4), (N.noise(70, 2, 0.5), 0.25)]
    return L._out(N, p, c, rough, 0.0, H, 0.3)


def k_bark(N, p):
    """burnt / cracked bark and root: deep fissures along `axis`, scaly charcoal plates, grey ash on top.
    heat=(r,g,b) lights the fissures like embers (heat_amt 0..1)"""
    col = _c(p, 'color', (0.035, 0.03, 0.027))
    ash = _c(p, 'ash', (0.2, 0.19, 0.18))
    axis = _c(p, 'axis', (0, 0, 1))
    al = N.align(N.warp(0.03, 5.0), axis, 0.12)
    fis = N.voro(_c(p, 'scale', 26.0), 'DISTANCE_TO_EDGE', vec=al)
    cw = _c(p, 'crack_w', 0.07)
    crack = N.mapr(fis, 0.0, cw, 1.0, 0.0)
    cell = N.voro(_c(p, 'scale', 26.0), 'F1', vec=al, out='Color')
    cv = N.new('ShaderNodeSeparateColor'); N.set(cv.inputs[0], cell)
    bk = N.scan('bark', size=0.6, relief=0.5)
    bt = N.scan('burnt', size=0.5, relief=0.3)
    mid = N.noise(24, 3, 0.6)
    c = N.mix(N.mapr(cv.outputs[0], 0.0, 1.0, 0.0, 0.6), col, N.mul(col, 2.4))
    c = N.detail(c, bk['det'], 0.9)
    c = N.detail(c, bt['det'], 0.6)
    c = N.shade(c, N.mapr(bk['hn'], -0.3, -1.7, 0.0, 0.6), 0.4)
    c = N.mix(N.math('MULTIPLY', crack, 0.85), c, N.mul(col, 0.2))
    up = N.math('MULTIPLY', N.upfacing(0.25, 0.9), N.mapr(N.noise(10, 4, 0.7), 0.3, 0.7))
    c = N.mix(N.math('MULTIPLY', up, _c(p, 'ash_amt', 0.5)), c, ash)
    c = N.mix(N.math('MULTIPLY', N.edge(0.3, 0.62, 0.3), 0.35), c, N.mix(0.5, ash, col))
    extra = {}
    heat = _c(p, 'heat', None)
    if heat is not None:
        ha = _c(p, 'heat_amt', 0.6)
        glow = N.mapr(fis, cw * 1.6, 0.0)
        hm = N.math('MULTIPLY', N.math('POWER', glow, 1.5), N.mapr(N.noise(3.0, 3, 0.6, vec=N.mapping(N.coord(), loc=(4.2, 7.7, 1.9))), 0.42 + 0.2 * (1 - ha), 0.7))
        hc = N.ramp(glow, [(0.0, tuple(x * 0.08 for x in heat)), (0.6, tuple(x * 0.5 for x in heat)), (1.0, heat)])
        c = N.mix(hm, c, hc)
        extra = dict(emit=(heat, _c(p, 'strength', 3.0)), emask=hm)
    rough = N.math('ADD', N.mapr(mid, 0.2, 0.8, 0.7, 0.96), N.math('MULTIPLY', bk['dr'], 0.4))
    rough = N.mixf(N.mapr(cv.outputs[2], 0.5, 0.85, 0.0, 0.5), rough, 0.42)        # glazed charcoal facets
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.98), 0.25)
    H = [(N.mapr(fis, 0.0, cw * 2.5), _c(p, 'depth', 4.0)), (bk['h'], 1.0), (bt['h'], 1.0), (N.math('SUBTRACT', cv.outputs[1], 0.5), 2.0),
         (N.noise(15, 2, 0.5), 2.0)]
    return L._out(N, p, c, rough, 0.0, H, 0.7, **extra)


def k_glow(N, p):
    """translucent skin lit from inside (toad belly, gadfly globe, leech gut, lure cap): skin colour with a dark vein
    net; the emission is the inner light broken by veins, clots and thicker skin"""
    col = _c(p, 'color', (0.22, 0.20, 0.10))
    e = _c(p, 'emit', (0.75, 0.9, 0.2))
    hide = N.scan('hide', size=0.4, relief=0.18, res=512)
    vd = N.voro(_c(p, 'vein_scale', 12.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.1, 5))
    vm = N.mapr(vd, 0.0, _c(p, 'vein_w', 0.045), 1.0, 0.0)
    clot = N.mapr(N.noise(_c(p, 'scale', 7.0), 4, 0.65), 0.35, 0.75)
    thick = N.mapr(N.noise(19, 3, 0.6, vec=N.mapping(N.coord(), loc=(5, 3, 1))), 0.3, 0.8, 0.55, 1.0)
    em = N.math('MULTIPLY', N.math('MULTIPLY', N.mapr(clot, 0.0, 1.0, 0.25, 1.0), thick), N.math('SUBTRACT', 1.0, N.math('MULTIPLY', vm, 0.85)))
    c = N.mix(clot, N.mul(col, 0.5), col)
    c = N.detail(c, hide['det'], 0.8)
    c = N.mix(N.math('MULTIPLY', vm, 0.8), c, _c(p, 'vein', (0.06, 0.03, 0.02)))
    c = N.mix(N.math('MULTIPLY', em, 0.4), c, tuple(min(1.0, x * 0.7) for x in e))
    rough = N.mapr(clot, 0.2, 0.8, _c(p, 'rough', 0.25), _c(p, 'rough', 0.25) + 0.3)
    r = L._out(N, p, c, rough, 0.0, [(hide['h'], 1.0), (vm, 0.9), (N.noise(14, 2, 0.5), 1.8)], 0.4)
    r['emit'] = (e, _c(p, 'strength', 3.0))
    r['emask'] = em
    return r


L.KINDS.update(chitin=k_chitin, scales=k_scales, slime=k_slime, bfur=k_bfur, membrane=k_membrane, chalk=k_chalk,
               bark=k_bark, glow=k_glow)
L.GRIME.update(chitin=((0.035, 0.028, 0.02), 0.8), scales=((0.03, 0.024, 0.016), 0.7), slime=((0.02, 0.016, 0.012), 0.6),
               bfur=((0.02, 0.016, 0.012), 0.6), membrane=((0.02, 0.014, 0.012), 0.6), chalk=((0.3, 0.3, 0.33), 0.5),
               bark=((0.01, 0.009, 0.008), 0.6), glow=((0.04, 0.03, 0.01), 0.3))


# ================================================================================================ geometry
def prof(vals, m):
    """resample a list of values (given at evenly spaced control stations) to m stations"""
    vals = np.asarray(vals, dtype=float)
    if vals.ndim == 0:
        return np.full(m, float(vals))
    return np.interp(np.linspace(0, 1, m), np.linspace(0, 1, len(vals)), vals)


def tubep(ctrl, rv, rh=None, m=12, n=10, power=2.0, up=(0, 0, 1), cap0=True, cap1=True, twist=0.0):
    """smooth tube through control points; rv = vertical (along `up`) radii at the control points, rh = sideways"""
    ctrl = np.asarray(ctrl, dtype=float)
    P = M.polyline(ctrl, m)
    # radii follow the same chord-length parameter as the path
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(ctrl, axis=0), axis=1))]
    d /= d[-1]
    t = np.linspace(0, 1, m)
    a = np.interp(t, d, np.broadcast_to(np.asarray(rv, dtype=float), (len(ctrl),)))
    b = a if rh is None else np.interp(t, d, np.broadcast_to(np.asarray(rh, dtype=float), (len(ctrl),)))
    return M.tube(P, a, n=n, r2=b, up=up, cap0=cap0, cap1=cap1, power=power, twist=twist)


def blob(c, rx, ry=None, rz=None, n=12, m=7, rot=None):
    p = M.ellipsoid(rx, ry, rz, n=n, m=m)
    if rot is not None:
        p.tf(rot)
    return p.move(*c)


def cone(base, direction, length, r, n=5, tip=0.0006, curl=None, seg=1):
    """spike from `base` along `direction` (closed base). curl = vector the tip bends towards (seg > 1)"""
    d = unit(direction)
    if curl is None or seg <= 1:
        path = [np.asarray(base, dtype=float), np.asarray(base, dtype=float) + d * length]
        seg = 1
    else:
        cu = unit(curl)
        path = [np.asarray(base, dtype=float) + d * length * (i / seg) + cu * length * 0.4 * (i / seg) ** 2 for i in range(seg + 1)]
    t = np.linspace(0, 1, seg + 1)
    rr = r * (1 - t) ** 0.9 + tip
    upv = (0, 0, 1) if abs(d[2]) < 0.9 else (0, 1, 0)
    p = M.tube(np.array(path), rr, n=n, up=upv, cap0=True, cap1=True)
    p.smooth = 50.0
    return p


def bvh_of(part):
    return BVHTree.FromPolygons([tuple(x) for x in part.v], [tuple(int(i) for i in t) for t in part.tris()])


def surf(bvh, origin, direction, dist=2.0):
    """first hit of a ray on a host -> (point, normal) or (None, None)"""
    loc, nrm, idx, d = bvh.ray_cast(Vector(origin), Vector(unit(direction)), dist)
    if loc is None:
        return None, None
    return np.array(loc), np.array(nrm)


def nearest(bvh, p):
    loc, nrm, idx, d = bvh.find_nearest(Vector(p))
    return np.array(loc), np.array(nrm), d


def seated(bvh, p, direction, length, r, sink=0.3, n=5, curl=None, seg=1):
    """a spike whose root is sunk into the host surface nearest to p"""
    q, nrm, d = nearest(bvh, p)
    dv = unit(direction)
    return cone(q - dv * length * sink, dv, length * (1 + sink), r, n=n, curl=curl, seg=seg)


def tooth_row(bvh, pts, direction, length, r, sink=0.35, n=4, jitter=0.15, seed=0, curl=None):
    """teeth along a polyline of gum points, each seated on the host; direction may be one vector or one per point"""
    rng = np.random.default_rng(seed)
    out = []
    pts = np.asarray(pts, dtype=float)
    dirs = np.broadcast_to(np.asarray(direction, dtype=float), pts.shape)
    for p, dv in zip(pts, dirs):
        k = 1 + rng.uniform(-jitter, jitter)
        out.append(seated(bvh, p, dv, length * k, r * k, sink, n, curl=curl, seg=2 if curl is not None else 1))
    q = M.join(out)
    q.smooth = 50.0
    return q


def tufts(host, n, length=(0.05, 0.09), radius=(0.012, 0.02), seed=1, mask=None, flow=(0, 1, -0.3), lay=0.6, sink=0.3,
          sides=3, jitter=0.2, droop=0.0):
    """cone locks scattered over a skinned host Part (weights copied from the host). mask(centres, normals) -> bool;
    flow = direction the hair lies along (vector or f(p)); lay 0 = stands on the normal .. 1 = flat along the flow"""
    rng = np.random.default_rng(seed)
    tr = host.tris()
    a, b, c = host.v[tr[:, 0]], host.v[tr[:, 1]], host.v[tr[:, 2]]
    cen = (a + b + c) / 3.0
    nn = np.cross(b - a, c - a)
    area = np.linalg.norm(nn, axis=1) / 2
    nrm = nn / np.maximum(np.linalg.norm(nn, axis=1), 1e-12)[:, None]
    ok = np.ones(len(cen), dtype=bool) if mask is None else np.asarray(mask(cen, nrm), dtype=bool)
    idx = np.nonzero(ok & (area > 0))[0]
    if len(idx) == 0:
        return None
    pick = rng.choice(idx, size=n, p=area[idx] / area[idx].sum())
    Vv = []; F = []; Ws = []
    ns = 2 if droop else 1
    for fi in pick:
        w = rng.dirichlet((1, 1, 1))
        p = a[fi] * w[0] + b[fi] * w[1] + c[fi] * w[2]
        f = np.asarray(flow(p) if callable(flow) else flow, dtype=float)
        ft = f - nrm[fi] * float(f @ nrm[fi])
        if np.linalg.norm(ft) < 0.15:
            ft = np.cross(nrm[fi], (1.0, 0.0, 0.0))
        ft = unit(ft)
        d = unit(nrm[fi] * (1 - lay) + ft * lay + (rng.random(3) - 0.5) * jitter)
        Ln = rng.uniform(*length); r = rng.uniform(*radius)
        base = p - d * Ln * sink
        u = np.cross(d, (0.0, 0.0, 1.0))
        if np.linalg.norm(u) < 1e-3:
            u = np.cross(d, (1.0, 0.0, 0.0))
        u = unit(u); v = np.cross(d, u)
        o = len(Vv)
        for k in range(sides):
            an = TAU * k / sides
            Vv.append(base + (u * math.cos(an) + v * math.sin(an)) * r)
        if ns == 2:
            midp = base + d * Ln * 0.6
            for k in range(sides):
                an = TAU * k / sides
                Vv.append(midp + (u * math.cos(an) + v * math.sin(an)) * r * 0.6)
            tip = base + d * Ln * (1 + sink) + V(0, 0, -1) * Ln * droop
            Vv.append(tip)
            for k in range(sides):
                k2 = (k + 1) % sides
                F.append((o + k, o + k2, o + sides + k2, o + sides + k))
                F.append((o + sides + k, o + sides + k2, o + 2 * sides))
            cnt = 2 * sides + 1
        else:
            Vv.append(base + d * Ln * (1 + sink))
            for k in range(sides):
                F.append((o + k, o + (k + 1) % sides, o + sides))
            cnt = sides + 1
        wrow = host.W[tr[fi, 0]] * w[0] + host.W[tr[fi, 1]] * w[1] + host.W[tr[fi, 2]] * w[2]
        Ws += [wrow] * cnt
    q = M.Part(np.array(Vv), F, np.array(Ws, dtype=np.float32))
    q.smooth = 180.0
    return q


def organic(parts, voxel=0.012, tris=6000, smooth=3, sculpt=None):
    """union + voxel remesh of closed skinned Parts into one skin; weights come back from the parts by nearest
    surface. sculpt(part) may displace the dense mesh before it is decimated to `tris`"""
    ref = M.join([p.copy() for p in parts])
    body = M.remesh([p.copy() for p in parts], voxel=voxel, smooth_iter=smooth)
    if sculpt is not None:
        sculpt(body)
    if tris and body.ntri > tris:
        body = M.decimate(body, tris / float(body.ntri))
    M.transfer_weights(body, ref, smooth=2)
    body.smooth = 180.0
    return body


def lathe(profile, n=14, axis='z'):
    """revolve (r, h) pairs round Z"""
    p = M.revolve(profile, n=n, cap=True)
    return p


def sheet_solid(grid, th=0.004):
    """(m,n,3) grid -> closed thin slab (both sides visible in the game)"""
    s = M.sheet(np.asarray(grid, dtype=float))
    s.solidify(th)
    s.smooth = 180.0
    return s


def fan(center, pts, th=0.004):
    """flat fan of triangles from a centre to a polyline rim, as a thin closed slab"""
    v = [np.asarray(center, dtype=float)] + [np.asarray(q, dtype=float) for q in pts]
    f = [(0, i, i + 1) for i in range(1, len(pts))]
    s = M.Part(np.array(v), f)
    s.solidify(th)
    s.smooth = 180.0
    return s


def plate_row(path, heights, width, th=0.006, lean=0.0, notch=0.0, seed=0, up=(0, 0, 1)):
    """row of thin upright plates (sail / crest / fin) standing on a path; heights per plate, width along the path"""
    rng = np.random.default_rng(seed)
    P = np.asarray(path, dtype=float)
    out = []
    upv = np.asarray(up, dtype=float)
    for i in range(len(P)):
        t = unit(P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)])
        h = heights[i] if not np.isscalar(heights) else heights
        if h <= 0:
            continue
        b0 = P[i] - t * width * 0.5 - upv * 0.02
        b1 = P[i] + t * width * 0.5 - upv * 0.02
        top = P[i] + upv * h + t * (lean * h + rng.uniform(-0.2, 0.2) * width)
        mid0 = b0 + upv * (h * 0.6 + 0.02) - t * width * 0.08
        mid1 = b1 + upv * (h * (0.6 - notch) + 0.02) + t * width * 0.08
        v = [b0, b1, mid1, top, mid0]
        s = M.Part(np.array(v), [(0, 1, 2, 3, 4)])
        s.solidify(th)
        out.append(s)
    q = M.join(out)
    q.smooth = 30.0
    return q


def ring(c, R, r, axis=(0, 0, 1), n=14, m=6):
    """torus at c with its hole along axis"""
    t = M.torus(R, r, n=n, m=m)
    t.tf(M.rot_to((0, 0, 1), axis))
    return t.move(*c)


def chain_links(path, link=0.05, r=0.008, n=6, m=4):
    return M.chain(np.asarray(path, dtype=float), link=link, r=r, n=n, m=m)


def seg_tube(a, b, r0, r1, n=7, bulge=1.25, joint=True):
    """rigid limb segment a->b with a knuckle at its root (arthropod legs)"""
    a = np.asarray(a, dtype=float); b = np.asarray(b, dtype=float)
    d = b - a
    Ln = np.linalg.norm(d)
    ts = [0.0, 0.08, 0.2, 0.55, 0.9, 1.0]
    rr = [r0 * 0.7, r0 * bulge, r0 * 0.92, (r0 + r1) * 0.5 * 1.05, r1, r1 * 0.75]
    path = np.array([a + d * t for t in ts])
    upv = (0, 0, 1) if abs(d[2] / max(Ln, 1e-9)) < 0.9 else (0, 1, 0)
    p = M.tube(path, np.array(rr), n=n, up=upv, cap0=True, cap1=True)
    p.smooth = 50.0
    return p


def dome(rx, ry, rz, n=16, m=6, z0=0.0, power=2.0):
    """upper half of a (super)ellipsoid, open rim closed by a floor"""
    secs = []
    for i in range(m + 1):
        a = (math.pi / 2) * i / m
        k = math.cos(a) ** (2.0 / power) if power != 2.0 else math.cos(a)
        secs.append((z0 + rz * math.sin(a), 0, 0, max(rx * k, 1e-4), max(ry * k, 1e-4)))
    p = M.loft_z(secs, n=n, cap0=True, cap1=True)
    return p
