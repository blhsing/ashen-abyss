"""Look kinds of Osaros v2 (registered into looks.KINDS; same object-space / metric-height conventions as looks.py):
  ash     fired-clay skin: a pale ash crust crazed like dried earth (scanned crack networks at two scales) over dark slag,
          spalled patches, soot in the hollows, and, near the given centres, fine fissures that still glow (the emission is
          a colour ramp: pale core, orange, dull red)
  core    the inside of the fired shell and the faces of its fractures: clinker that glows hotter the deeper it lies
  char    charred timber: alligatored charcoal blocks along the grain, grey ash on top, embers in the deep checks
  iron    forge iron: black fire-scale, a little rust, hammered; optional heat (dull red -> orange) around given points
  soot    wraps looks.k_cloth: heavy soot-black wool, ash on everything that faces up, hems eaten by embers that still glow
Glow that is a colour ramp is returned as 'emit_rgb' (a socket carrying colour x mask); make_look_material() below is
looks.make_look_material extended for it (the emission texture then carries the whole colour, exported with a white factor).
"""
import math
import bpy
import monlib as M
import looks as L

DIST = L.DIST


def _dist(N, pt):
    n = N.new('ShaderNodeVectorMath', operation='DISTANCE')
    N.set(n.inputs[0], N.coord())
    n.inputs[1].default_value = tuple(float(x) for x in pt)
    return n.outputs['Value']


def region(N, centres):
    """0..1 field: 1 within r0 of any centre, 0 beyond r1.  centres = [((x, y, z), r0, r1), ...]"""
    out = None
    for (pt, r0, r1) in centres:
        m = N.mapr(_dist(N, pt), r1, r0)
        out = m if out is None else N.math('MAXIMUM', out, m)
    return out if out is not None else 0.0


def _sepc(N, col):
    s = N.new('ShaderNodeSeparateColor')
    N.set(s.inputs[0], col)
    return s.outputs[0], s.outputs[1], s.outputs[2]


def _mul(N, a, b):
    return N.math('MULTIPLY', a, b)


def _max(N, a, b):
    return N.math('MAXIMUM', a, b)


def _heat_ramp(N, t, hot=(1.0, 0.78, 0.40), org=(1.0, 0.33, 0.05), red=(0.36, 0.028, 0.0)):
    return N.ramp(t, [(0.0, (0.0, 0.0, 0.0)), (0.2, tuple(x * 0.4 for x in red)), (0.45, red), (0.75, org), (1.0, hot)])


# ------------------------------------------------------------------------------------------------ ash crust
def k_ash(N, p):
    col = p.get('color', (0.37, 0.355, 0.33))
    slag = p.get('slag', (0.075, 0.070, 0.068))
    clay = p.get('clay', (0.17, 0.125, 0.10))
    soot = p.get('soot', (0.016, 0.0145, 0.0135))
    char = (0.024, 0.021, 0.019)
    P = N.coord()
    G = region(N, p.get('glow', []))
    S = region(N, p.get('scorch', [])) if p.get('scorch') else G
    band = region(N, p['band']) if p.get('band') else None           # the burnt rim of the break: black, nothing glows in it
    big = N.noise(6.5, 4, 0.62)
    mid = N.noise(23, 3, 0.6)
    # the crust: scanned cracked earth at two scales (real crack networks), which of the two changes from place to place
    ca = N.scan('mud_crk', size=p.get('crust_a', 0.34), relief=0.62, res=1024)
    cb_ = N.scan('mud_crk2', size=p.get('crust_b', 0.72), relief=0.44, res=1024, loc=(0.31, 0.17, 0.53))
    cf = N.scan('mud_crk', size=p.get('crust_f', 0.15), relief=0.34, res=1024, loc=(0.57, 0.23, 0.11))   # hairline crazing, ~6 mm cells
    grain = N.scan('plaster', size=0.4, relief=0.2)
    burnt = N.scan('burnt', size=0.5, relief=0.3)
    sel = N.mapr(N.noise(5.0, 3, 0.6, vec=N.mapping(P, loc=(2.7, 4.4, 8.3))), 0.42, 0.6)
    crack = N.mixf(sel, N.mapr(ca['hn'], -0.85, -2.1), N.mapr(cb_['hn'], -0.85, -2.1))
    patch = N.mapr(N.noise(7.5, 3, 0.6, vec=N.mapping(P, loc=(6.2, 0.4, 3.1))), 0.35, 0.62, 0.3, 1.0)
    crack = _mul(N, crack, patch)
    hair = _mul(N, N.mapr(cf['hn'], -0.9, -2.0), N.mapr(N.noise(9.0, 3, 0.6, vec=N.mapping(P, loc=(1.2, 7.7, 4.1))), 0.3, 0.6, 0.25, 1.0))
    pits = _mul(N, N.mapr(N.voro(430, 'F1', vec=N.warp(0.004, 60)), 0.20, 0.07), N.mapr(N.noise(12, 3, 0.6, vec=N.mapping(P, loc=(8.8, 1.3, 5.2))), 0.4, 0.62))
    wv = N.warp(0.05, 7)
    sc = p.get('plate', 40.0)
    cr, cg, cb = _sepc(N, N.voro(sc, 'F1', vec=wv, out='Color'))
    fl_n = N.noise(11, 4, 0.66, vec=N.mapping(P, loc=(3.3, 7.1, 1.9)))
    flake = _mul(N, N.mapr(fl_n, 0.535, 0.56), p.get('flake', 1.0))                  # spalled patches: a step down to the slag
    lip = _mul(N, _mul(N, N.mapr(fl_n, 0.505, 0.532), N.mapr(fl_n, 0.56, 0.536)), p.get('flake', 1.0))   # the curled edge of the ash round them
    f2_n = N.noise(27, 4, 0.7, vec=N.mapping(P, loc=(5.9, 2.6, 7.4)))
    flake2 = _mul(N, N.mapr(f2_n, 0.56, 0.575), p.get('flake', 1.0))                 # small chips out of the crust
    # ---- colour
    c = N.mix(N.mapr(big, 0.36, 0.72, 0.0, 0.42), col, N.mix(0.5, col, slag))
    c = N.mix(N.mapr(cr, 0.0, 1.0, 0.0, 0.2), c, N.mul(c, 0.68))                     # patches fired a little differently
    c = N.mix(N.mapr(mid, 0.52, 0.80, 0.0, 0.3), c, N.mul(col, 1.25))
    c = N.mix(N.mapr(cb, 0.80, 0.97, 0.0, 0.4), c, N.mix(0.6, col, clay))            # here and there the fired clay under the ash
    c = N.mix(_mul(N, flake, 0.85), c, N.mix(N.mapr(mid, 0.3, 0.7), slag, N.mix(0.5, slag, clay)))
    c = N.mix(_mul(N, flake2, 0.7), c, N.mix(0.4, slag, col))
    c = N.mix(_mul(N, lip, 0.5), c, N.mul(col, 1.35))
    c = N.detail(c, N.mix(sel, ca['det'], cb_['det']), 0.85)
    c = N.detail(c, grain['det'], 0.6)
    c = N.mix(_mul(N, crack, 0.85), c, N.mul(slag, 0.25))
    c = N.mix(_mul(N, hair, 0.6), c, N.mul(slag, 0.4))
    c = N.mix(_mul(N, pits, 0.75), c, N.mul(slag, 0.3))
    drip = N.mapr(N.noise(19, 3, 0.6, vec=N.mapping(P, scale=(1, 1, 0.08))), 0.55, 0.8)       # soot streaks running down
    c = N.mix(_mul(N, drip, p.get('streak', 0.3)), c, N.mul(slag, 0.5))
    up = _mul(N, N.upfacing(0.35, 0.95), N.mapr(N.noise(10, 3, 0.6), 0.3, 0.7))
    c = N.mix(_mul(N, up, p.get('dust', 0.6)), c, p.get('ash_top', (0.43, 0.42, 0.40)))
    so = N.mapr(N.ao(), p.get('soot_ao', 0.76), 0.30)                                # soot gathers where the form closes in
    c = N.mix(_mul(N, so, p.get('soot_amt', 0.85)), c, soot)
    sn = N.mapr(N.noise(13, 3, 0.6, vec=N.mapping(P, loc=(1.5, 8.0, 2.5))), 0.25, 0.7, 0.5, 1.0)
    Sn = _mul(N, S, sn)
    if band is not None:
        Sn = _max(N, Sn, band)
    c = N.mix(_mul(N, Sn, 0.94), c, N.detail(char, burnt['det'], 1.0))               # scorched towards the break
    # ---- the fissures that still glow: the cracks of the crust itself, alight near the break; a few wider ones run further
    fs = p.get('fissure', 27.0)
    fe = N.voro(fs, 'DISTANCE_TO_EDGE', vec=N.warp(0.10, 5.5))
    gate = N.mapr(N.noise(8.5, 2, 0.5, vec=N.mapping(P, loc=(9.1, 2.2, 5.7))), 0.42, 0.58)
    amt = _mul(N, G, gate)
    near = N.mapr(G, 0.45, 0.95)
    if band is not None:
        free = N.math('SUBTRACT', 1.0, band)
        amt = _mul(N, amt, free)
        near = _mul(N, near, free)
    ga = p.get('glow_amt', 1.0)
    line = _mul(N, N.mapr(ca['hn'], -1.25, -2.4), N.mapr(ca['hn'], -5.0, -3.2))            # 0..1 down in the fine cracks of the crust (not its broad hollows)
    core = N.mapr(fe, 0.010, 0.003)
    body = N.mapr(fe, 0.026, 0.007)
    halo = N.mapr(fe, 0.09, 0.02)
    heat = _max(N, _max(N, _mul(N, halo, 0.12), _mul(N, body, 0.5)), _max(N, _mul(N, core, 0.9), _mul(N, line, 0.8)))
    heat = _mul(N, heat, amt)
    heat = _max(N, heat, _mul(N, _mul(N, line, near), 0.95))
    heat = _mul(N, heat, ga)
    heat = _mul(N, heat, N.mapr(N.noise(30, 2, 0.5, vec=N.mapping(P, loc=(2.0, 2.0, 2.0))), 0.2, 0.75, 0.55, 1.0))
    for (pt, r0, r1, g) in p.get('ember', []):                                         # what an ember lights round itself (the orbits)
        e = _mul(N, N.math('POWER', N.mapr(_dist(N, pt), r1, r0), 1.7), g)
        heat = _max(N, heat, e)
    ecol = _heat_ramp(N, heat)
    c = N.mix(_mul(N, _mul(N, halo, amt), 0.8), c, char)
    c = N.mix(N.mapr(heat, 0.3, 0.8, 0.0, 0.8), c, (0.30, 0.07, 0.01))               # the ember shows in the albedo too (dim scenes)
    # ---- roughness
    rough = N.math('ADD', N.mapr(mid, 0.3, 0.75, 0.82, 0.97), _mul(N, N.mixf(sel, ca['dr'], cb_['dr']), 0.5))
    rough = N.mixf(N.mapr(cg, 0.6, 0.92, 0.0, 0.5), rough, 0.45)                     # some patches fused glassy
    rough = N.mixf(_mul(N, flake, 0.8), rough, N.mapr(mid, 0.3, 0.7, 0.34, 0.6))     # the slag under the ash is glassier
    rough = N.mixf(_mul(N, flake2, 0.6), rough, 0.5)
    rough = N.mixf(_mul(N, up, 0.8), rough, 1.0)
    rough = N.mixf(_mul(N, so, 0.9), rough, 0.97)
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.98), 0.25)
    tilt = N.math('SUBTRACT', cg, 0.5)
    H = [(tilt, 0.45), (N.mixf(sel, ca['h'], cb_['h']), 1.0), (cf['h'], 1.0), (grain['h'], 1.0), (flake, -0.75), (lip, 0.3), (flake2, -0.4),
         (crack, -0.35), (hair, -0.15), (pits, -0.4), (_mul(N, burnt['h'], Sn), 1.0), (_mul(N, body, amt), -0.9), (N.noise(15, 2, 0.5), 1.4)]
    return L._out(N, p, c, rough, 0.0, H, 0.5, emit=((1.0, 1.0, 1.0), p.get('strength', 3.0)), emask=N.mapr(heat, 0.0, 0.25), emit_rgb=ecol)


# ------------------------------------------------------------------------------------------------ embers for eyes
def k_eye(N, p):
    """an ember seen end-on: a small pale core, orange round it, dull red at the rim, flecks of cooled crust.
    eyes = [((x, y, z) of the core, r_core, r_out, gain), ...]"""
    heat = None
    for (pt, r0, r1, g) in p['eyes']:
        e = _mul(N, N.math('POWER', N.mapr(_dist(N, pt), r1, r0), 1.5), g)
        heat = e if heat is None else _max(N, heat, e)
    fleck = N.mapr(N.noise(260, 2, 0.6), 0.52, 0.7)
    heat = _mul(N, heat, N.mixf(fleck, 1.0, 0.55))
    ecol = _heat_ramp(N, heat, hot=(1.0, 0.86, 0.58), org=(1.0, 0.38, 0.06), red=(0.40, 0.03, 0.0))
    c = N.mix(N.mapr(heat, 0.2, 0.8), (0.03, 0.018, 0.012), (0.42, 0.11, 0.02))
    return dict(color=c, rough=0.7, metal=0.0, height=N.noise(200, 2, 0.5), bump=(0.2, 0.002), emit=((1.0, 1.0, 1.0), p.get('strength', 3.6)),
                emask=N.mapr(heat, 0.0, 0.15), emit_rgb=ecol)


# ------------------------------------------------------------------------------------------------ the hot inside
def k_core(N, p):
    P = N.coord()
    char = p.get('color', (0.028, 0.025, 0.023))
    sect = p.get('section', (0.15, 0.14, 0.13))                                      # the broken edge of the shell near the outside
    depth = N.mapr(N.ao(), p.get('ao_hi', 0.80), p.get('ao_lo', 0.30))               # 0 at the open surface .. 1 deep inside
    if p.get('glow'):
        depth = _mul(N, depth, _max(N, region(N, p['glow']), p.get('floor', 0.0)))
    burnt = N.scan('burnt', size=0.45, relief=0.34)
    wv = N.warp(0.03, 14)
    sc = p.get('scale', 62.0)
    ve = N.voro(sc, 'DISTANCE_TO_EDGE', vec=wv)
    lump = N.voro(sc, 'SMOOTH_F1', vec=wv, smooth=0.5)
    vein = N.mapr(ve, 0.13, 0.0)                                                     # the gaps between the clinker lumps burn hottest
    blot = N.mapr(N.noise(sc * 0.42, 3, 0.6, vec=N.mapping(N.warp(0.02, 20), loc=(1.0, 6.0, 4.0))), 0.3, 0.75)
    isl = N.mapr(N.noise(sc * 0.55, 3, 0.65, vec=N.warp(0.015, 40)), 0.47, 0.63)     # cooled crust
    flick = N.mapr(N.noise(7, 2, 0.5, vec=N.mapping(P, loc=(5.0, 3.0, 1.0))), 0.25, 0.75, 0.6, 1.0)
    heat = _mul(N, N.math('POWER', depth, p.get('gamma', 1.6)), N.math('ADD', N.math('ADD', 0.22, _mul(N, blot, 0.45)), _mul(N, vein, 0.36)))
    heat = _mul(N, heat, N.mixf(isl, 1.0, 0.18))
    heat = _mul(N, _mul(N, heat, flick), p.get('heat', 1.0))
    ecol = _heat_ramp(N, heat)
    c = N.mix(N.mapr(depth, 0.0, 0.5), sect, char)
    c = N.detail(c, burnt['det'], 1.0)
    c = N.shade(c, N.mapr(burnt['hn'], -0.3, -1.6, 0.0, 0.6), 0.4)
    c = N.mix(N.mapr(heat, 0.25, 0.8, 0.0, 0.85), c, (0.32, 0.075, 0.012))
    rough = N.mixf(isl, 0.7, 0.95)
    H = [(N.mapr(lump, 0.0, 0.6, 1.0, 0.0), 1.8), (burnt['h'], 1.0), (isl, 0.5), (N.noise(40, 2, 0.5), 0.8)]
    return L._out(N, p, c, rough, 0.0, H, 0.5, emit=((1.0, 1.0, 1.0), p.get('strength', 3.5)), emask=N.mapr(heat, 0.0, 0.2), emit_rgb=ecol)


# ------------------------------------------------------------------------------------------------ charred timber
def k_char(N, p):
    col = p.get('color', (0.020, 0.018, 0.017))
    ashc = p.get('ash', (0.12, 0.115, 0.11))
    axis = p.get('axis', (0, 1, 0))
    al = N.align(N.warp(0.012, 6.0), axis, 0.42)                                     # alligatoring: blocks about twice as long as wide
    sc = p.get('scale', 34.0)
    cr, cg, cb = _sepc(N, N.voro(sc, 'F1', vec=al, out='Color'))
    edge = N.voro(sc, 'DISTANCE_TO_EDGE', vec=al)
    check = N.mapr(edge, 0.0, 0.10, 1.0, 0.0)
    dome = N.mapr(edge, 0.0, 0.42)
    g = N.noise(70, 3, 0.6, vec=N.align(N.warp(0.01, 6.0), axis, 0.05))
    bk = N.scan('bark', size=0.6, relief=0.3)
    bt = N.scan('burnt', size=0.5, relief=0.3)
    mid = N.noise(21, 3, 0.6)
    c = N.mix(N.mapr(cr, 0.0, 1.0, 0.0, 0.6), col, N.mul(col, 2.6))
    c = N.mix(N.mapr(g, 0.35, 0.7, 0.0, 0.35), c, N.mul(col, 2.0))
    c = N.mix(N.mapr(mid, 0.55, 0.8, 0.0, 0.35), c, p.get('brown', (0.055, 0.036, 0.024)))  # where the wood only scorched
    c = N.detail(c, bt['det'], 0.9)
    c = N.detail(c, bk['det'], 0.4)
    c = N.mix(_mul(N, check, 0.9), c, (0.006, 0.006, 0.006))
    up = _mul(N, N.upfacing(0.25, 0.9), N.mapr(N.noise(9, 4, 0.7), 0.3, 0.65))
    up = _mul(N, up, N.mapr(dome, 0.2, 0.9, 0.5, 1.0))
    c = N.mix(_mul(N, up, p.get('ash_amt', 0.75)), c, ashc)
    edgew = N.edge(0.3, 0.6, 0.3)
    c = N.mix(_mul(N, edgew, 0.35), c, N.mix(0.5, ashc, col))
    rough = N.mixf(N.mapr(cg, 0.5, 0.9, 0.0, 0.6), 0.92, 0.62)                       # charcoal faces have a faint graphite sheen
    rough = N.mixf(_mul(N, up, 0.85), rough, 1.0)
    H = [(dome, 2.6), (check, -1.6), (g, 0.5), (bk['h'], 1.0), (bt['h'], 1.0), (N.noise(16, 2, 0.5), 1.5)]
    r = L._out(N, p, c, rough, 0.0, H, 0.6)
    if p.get('glow'):
        G = region(N, p['glow'])
        gate = N.mapr(N.noise(10, 2, 0.5, vec=N.mapping(N.coord(), loc=(7.0, 1.0, 3.0))), 0.36, 0.6)
        heat = _mul(N, _mul(N, N.mapr(edge, 0.16, 0.0), G), gate)
        heat = _max(N, heat, _mul(N, _mul(N, N.mapr(N.cavity(), 0.2, 0.7), G), 0.5))
        heat = _mul(N, heat, p.get('heat', 1.0))
        r['color'] = N.mix(N.mapr(heat, 0.3, 0.8, 0.0, 0.8), r['color'], (0.30, 0.07, 0.01))
        r['emit'] = ((1.0, 1.0, 1.0), p.get('strength', 3.0))
        r['emask'] = N.mapr(heat, 0.0, 0.25)
        r['emit_rgb'] = _heat_ramp(N, heat)
    return r


# ------------------------------------------------------------------------------------------------ forge iron
def k_iron(N, p):
    q = dict(color=(0.040, 0.039, 0.040), rust=(0.105, 0.055, 0.032), rust2=(0.028, 0.02, 0.017), amount=0.42, rough=0.52,
             edge_color=(0.27, 0.265, 0.26), edge=0.75, hammer=30.0)
    q.update(p)
    r = L.k_metal(N, q)
    P = N.coord()
    # fire-scale: blue-black flaking skin
    sv = N.voro(58, 'DISTANCE_TO_EDGE', vec=N.warp(0.04, 12))
    scale_m = _mul(N, N.mapr(N.noise(8, 3, 0.6, vec=N.mapping(P, loc=(2.2, 6.1, 0.4))), 0.44, 0.6), q.get('scale_amt', 0.8))
    r['color'] = N.mix(_mul(N, scale_m, 0.7), r['color'], (0.022, 0.024, 0.030))
    r['color'] = N.mix(_mul(N, _mul(N, N.mapr(sv, 0.05, 0.0), scale_m), 0.8), r['color'], (0.10, 0.06, 0.04))
    r['rough'] = N.mixf(_mul(N, scale_m, 0.7), r['rough'], 0.62)
    r['metal'] = N.mixf(_mul(N, scale_m, 0.75), r['metal'], 0.25)
    L._add_h(N, r, [(_mul(N, N.mapr(sv, 0.0, 0.08), scale_m), 0.5)])
    up = _mul(N, N.upfacing(0.5, 0.95), N.mapr(N.noise(9, 3, 0.6), 0.35, 0.7))
    r['color'] = N.mix(_mul(N, up, q.get('dust', 0.45)), r['color'], (0.27, 0.26, 0.25))        # ash lies on everything
    r['rough'] = N.mixf(_mul(N, up, q.get('dust', 0.45)), r['rough'], 1.0)
    r['metal'] = N.mixf(_mul(N, up, q.get('dust', 0.45)), r['metal'], 0.0)
    if q.get('glow'):
        G = region(N, q['glow'])
        n = N.mapr(N.noise(26, 3, 0.6), 0.2, 0.8, 0.7, 1.0)
        heat = _mul(N, _mul(N, G, n), q.get('heat', 1.0))
        r['color'] = N.mix(N.mapr(heat, 0.1, 0.7, 0.0, 0.9), r['color'], (0.20, 0.035, 0.008))
        r['metal'] = N.mixf(N.mapr(heat, 0.1, 0.6), r['metal'], 0.0)
        r['rough'] = N.mixf(N.mapr(heat, 0.1, 0.6), r['rough'], 0.8)
        r['emit'] = ((1.0, 1.0, 1.0), q.get('strength', 3.0))
        r['emask'] = N.mapr(heat, 0.0, 0.2)
        r['emit_rgb'] = _heat_ramp(N, heat)
    return r


# ------------------------------------------------------------------------------------------------ soot cloth
def k_soot(N, p):
    q = dict(color=(0.022, 0.021, 0.022), weave='wool', dirt=(0.16, 0.155, 0.15), dust=0.75, fold=8.0, fold_amt=1.0, fray=0.55, streak=0.5,
             hem_lo=0.0, hem_hi=0.6, hem_amt=0.55)
    q.update(p)
    dust_amt = q.get('dust', 0.5)
    q = dict(q, dust=0.0)
    r = L.k_cloth(N, q)
    r['color'] = N.mul(r['color'], q.get('dark', 0.6))                                # soot-black, not grey
    upd = _mul(N, N.upfacing(0.35, 0.95), N.mapr(N.noise(8, 3, 0.6), 0.3, 0.7))
    r['color'] = N.mix(_mul(N, upd, dust_amt), r['color'], q.get('ash', (0.21, 0.205, 0.20)))   # ash lies on every fold that faces up
    r['rough'] = N.mixf(_mul(N, upd, dust_amt), r['rough'], 1.0)
    P = N.coord()
    d = N.hem()                                                                       # metres from the nearest open border
    lo = q.get('ember_lo', -1.0); hi = q.get('ember_hi', 0.45)
    zone = N.math('SUBTRACT', 1.0, N.height_z(lo + (hi - lo) * 0.6, hi)) if q.get('ember', 1.0) > 0 else 0.0
    burn = _mul(N, N.mapr(d, 0.030, 0.004), N.mapr(N.noise(14, 3, 0.65), 0.3, 0.6))    # charred, brittle border
    r['color'] = N.mix(_mul(N, burn, 0.9), r['color'], (0.008, 0.0075, 0.007))
    r['rough'] = N.mixf(_mul(N, burn, 0.6), r['rough'], 0.8)
    if q.get('ember', 1.0) > 0:
        gate = N.mapr(N.noise(5.5, 3, 0.6, vec=N.mapping(P, loc=(3.0, 9.0, 1.0))), 0.46, 0.62)
        line = N.mapr(d, 0.0075, 0.0012)
        spark = N.mapr(N.noise(120, 2, 0.6), 0.35, 0.75, 0.35, 1.0)
        heat = _mul(N, _mul(N, _mul(N, line, gate), zone), _mul(N, spark, q.get('ember', 1.0)))
        r['color'] = N.mix(N.mapr(heat, 0.2, 0.8, 0.0, 0.85), r['color'], (0.30, 0.06, 0.008))
        r['emit'] = ((1.0, 1.0, 1.0), q.get('strength', 3.0))
        r['emask'] = N.mapr(heat, 0.0, 0.2)
        r['emit_rgb'] = _heat_ramp(N, heat)
    return r


L.KINDS.update(ash=k_ash, core=k_core, char=k_char, iron=k_iron, soot=k_soot, eye=k_eye)
L.GRIME.update(ash=((0.014, 0.012, 0.011), 0.8), char=((0.006, 0.006, 0.006), 0.6), iron=L.GRIME['metal'], soot=L.GRIME['cloth'])
L.NO_FINISH = tuple(x for x in L.NO_FINISH if x not in ('core', 'eye')) + ('core', 'eye')


# ------------------------------------------------------------------------------------------------ material builder
def make_look_material(name, kind, params):
    """looks.make_look_material + colour-ramp emission ('emit_rgb') + an E_ONE pass (coverage masks)"""
    holder = {}
    orig = L.KINDS[kind]

    def wrapped(N, p):
        r = orig(N, p)
        holder['r'] = r
        return r
    L.KINDS[kind] = wrapped
    try:
        mat = _ORIG_MAKE(name, kind, params)
    finally:
        L.KINDS[kind] = orig
    r = holder['r']
    nt = mat.node_tree
    if r.get('emit_rgb') is not None:
        ee = nt.nodes['E_EMI']
        for l in list(ee.inputs[0].links):
            nt.links.remove(l)
        nt.links.new(r['emit_rgb'], ee.inputs[0])
        P = nt.nodes['P']
        for l in list(P.inputs['Emission Color'].links):
            nt.links.remove(l)
        nt.links.new(r['emit_rgb'], P.inputs['Emission Color'])
    one = nt.nodes.new('ShaderNodeEmission'); one.name = 'E_ONE'
    one.inputs[0].default_value = (1.0, 1.0, 1.0, 1.0)
    return mat


if not hasattr(L, '_ov2_orig_make'):
    L._ov2_orig_make = L.make_look_material
_ORIG_MAKE = L._ov2_orig_make
L.make_look_material = make_look_material
