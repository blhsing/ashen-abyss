"""Helpers for the map-marker tokens (props_markers.py): display-true glow colours, opaque fire, light stone, lamps.

Why its own glow materials: the stage renders with the Standard view transform, which clips every channel on its own.
iconlib.emit(colour, 4) therefore bleaches ember orange to pale yellow -- fine on an item, wrong on a marker whose
whole job is its colour code. Here a glow is given as the sRGB colour it must SHOW; only its dominant channel is
driven above 1 (it clips back to 1), so the hue survives and the neighbours still get coloured light.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Euler
import iconlib as I
from iconlib import R

V = Vector


# ------------------------------------------------------------------------------------------------ colour
def lin(c):
    """sRGB -> scene linear"""
    return tuple((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c[:3])


def pw(c, p):
    """push an sRGB colour toward its dominant channel (p > 1: deeper) or toward white (p < 1: hotter)"""
    return tuple(max(0.0, min(1.0, x)) ** p for x in c[:3])


def ecol(srgb, strength=4.0):
    """emission COLOUR that, multiplied by `strength`, displays as srgb (dominant channel = strength, rest as shown)"""
    l = lin(srgb); mx = max(max(l), 1e-6)
    e = [x / mx for x in l]
    return tuple((1.0 if x > 0.999 else x / strength) for x in e)


def _math(nt, op, a, b=None, c=None, clamp=False):
    n = nt.nodes.new('ShaderNodeMath'); n.operation = op; n.use_clamp = clamp
    for i, v in enumerate((a, b, c)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            n.inputs[i].default_value = float(v)
        else:
            nt.links.new(v, n.inputs[i])
    return n.outputs[0]


def _ramp(nt, fac, stops):
    n = nt.nodes.new('ShaderNodeValToRGB'); cr = n.color_ramp
    while len(cr.elements) < len(stops):
        cr.elements.new(0.5)
    for e, (p, c) in zip(cr.elements, stops):
        e.position = p; e.color = tuple(lin(c)) + (1.0,)
    if isinstance(fac, (int, float)):
        n.inputs[0].default_value = fac
    else:
        nt.links.new(fac, n.inputs[0])
    return n.outputs[0]


def _blank(key):
    m = bpy.data.materials.new(key); m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    return m, nt, nt.nodes.new('ShaderNodeOutputMaterial')


def _emit_out(nt, out, col, strength=1.0):
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs[1].default_value = strength
    nt.links.new(col, em.inputs[0]); nt.links.new(em.outputs[0], out.inputs[0])


# ------------------------------------------------------------------------------------------------ materials
def glow(srgb, strength=4.0, key=None):
    """flat glow that shows as `srgb` and lights its surroundings with `strength`"""
    key = key or "mk_glow%r%r" % (tuple(srgb), strength)
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    b.inputs['Base Color'].default_value = (0, 0, 0, 1)
    b.inputs['Roughness'].default_value = 0.6
    b.inputs['Emission Color'].default_value = ecol(srgb, strength) + (1,)
    b.inputs['Emission Strength'].default_value = strength
    I._MATS[key] = m
    return m


def void():
    return I.emit((0, 0, 0), 0.0, key="void_black")


def lit(mat, srgb, strength=0.3):
    """copy of a kit look with a little self-light in its own colour ('enamelled': keeps its hue in shadow)"""
    k = "mk_lit_%s_%r_%r" % (mat.name, tuple(srgb), strength)
    if k in I._MATS:
        return I._MATS[k]
    m = mat.copy(); m.name = k
    b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b is not None:
        for nm in ('Emission Color', 'Emission Strength'):
            for l in list(b.inputs[nm].links):
                m.node_tree.links.remove(l)
        b.inputs['Emission Color'].default_value = tuple(lin(srgb)) + (1,)
        b.inputs['Emission Strength'].default_value = strength
    I._MATS[k] = m
    return m


def fire_mat(srgb, zfall=0.75, stops=None, key=None, strength=1.0):
    """opaque fire / energy: the given colour over most of the tongue, deeper at the silhouette and the tip, a pale
    hot heart low in the middle. View-dependent (layer weight), so it reads the same from every side."""
    key = key or "mk_fire%r%r%r%r" % (tuple(srgb), zfall, stops, strength)
    if key in I._MATS:
        return I._MATS[key]
    m, nt, out = _blank(key)
    tc = nt.nodes.new('ShaderNodeTexCoord'); sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    lw = nt.nodes.new('ShaderNodeLayerWeight'); lw.inputs[0].default_value = 0.5
    a = _math(nt, 'SUBTRACT', 1.0, lw.outputs['Facing'], clamp=True)      # 1 in the middle, 0 at the silhouette
    b = _math(nt, 'MULTIPLY_ADD', sep.outputs[2], -zfall, 1.0)            # 1 at the base .. 1 - zfall at the tip
    h = _math(nt, 'MULTIPLY', a, b, clamp=True)
    st = stops or [(0.0, pw(srgb, 2.4)), (0.50, srgb), (0.86, pw(srgb, 0.62)), (1.0, pw(srgb, 0.40))]
    _emit_out(nt, out, _ramp(nt, h, st), strength)
    I._MATS[key] = m
    return m


def orb_mat(srgb, key=None):
    """soft glowing ball: pale heart, coloured rim"""
    return fire_mat(srgb, 0.0, [(0.0, pw(srgb, 2.0)), (0.50, srgb), (0.86, pw(srgb, 0.62)), (1.0, pw(srgb, 0.40))], key)


def gradient(key, stops, axis=2, strength=1.0):
    """emission ramp along a Generated axis (0 = X, 1 = Y, 2 = Z); stops = [(pos, sRGB)]"""
    key = "mk_grad_" + key
    if key in I._MATS:
        return I._MATS[key]
    m, nt, out = _blank(key)
    tc = nt.nodes.new('ShaderNodeTexCoord'); sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    _emit_out(nt, out, _ramp(nt, sep.outputs[axis], stops), strength)
    I._MATS[key] = m
    return m


KEY_DIR = (-0.62, -0.55, 0.62)      # toward the stage's key light (iconlib.reset)


def _facing_key(nt):
    """-1 .. 1: how squarely the true (flat) face normal meets the key light"""
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    dot = nt.nodes.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
    nt.links.new(geo.outputs['True Normal'], dot.inputs[0])
    dot.inputs[1].default_value = tuple(V(KEY_DIR).normalized())
    return dot.outputs['Value']


def facet(key, stops, base=(0.20, 0.12, 0.02), rough=0.12, strength=1.0):
    """cut stone that glows facet by facet: every face takes its tone from how squarely it meets the key light, under
    a glossy coat -- a faceted read that survives at 16 px (iconlib.gem's per-facet noise came out uniform here)"""
    key = "mk_facet_" + key
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    f = _math(nt, 'MULTIPLY_ADD', _facing_key(nt), 0.5, 0.5, clamp=True)
    b.inputs['Base Color'].default_value = tuple(base) + (1,)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Coat Weight'].default_value = 1.0
    b.inputs['Coat Roughness'].default_value = 0.05
    nt.links.new(_ramp(nt, f, stops), b.inputs['Emission Color'])
    b.inputs['Emission Strength'].default_value = strength
    I._MATS[key] = m
    return m


def radial(key, stops, radius, base=None, strength=1.0, noise=0.0, facet=0.0):
    """emission ramp over the distance from the object's origin (0 .. radius). base=(colour, metallic, roughness) puts
    it on a lit surface (hot iron); without it the material is pure light (lantern horn)"""
    key = "mk_rad_" + key
    if key in I._MATS:
        return I._MATS[key]
    if base is None:
        m, nt, out = _blank(key)
    else:
        m, nt, b = I._pbr(key)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    ln = nt.nodes.new('ShaderNodeVectorMath'); ln.operation = 'LENGTH'
    nt.links.new(tc.outputs['Object'], ln.inputs[0])
    f = _math(nt, 'DIVIDE', ln.outputs['Value'], radius)
    if noise:
        no = nt.nodes.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = 26.0; no.inputs['Detail'].default_value = 3.0
        nt.links.new(tc.outputs['Object'], no.inputs['Vector'])
        f = _math(nt, 'ADD', f, _math(nt, 'MULTIPLY', _math(nt, 'SUBTRACT', no.outputs[0], 0.5), noise))
    if facet:                                                      # faces turned to the key light look hotter
        f = _math(nt, 'SUBTRACT', f, _math(nt, 'MULTIPLY', _facing_key(nt), facet))
    col = _ramp(nt, f, stops)
    if base is None:
        _emit_out(nt, out, col, strength)
    else:
        b.inputs['Base Color'].default_value = tuple(base[0]) + (1,)
        b.inputs['Metallic'].default_value = base[1]
        b.inputs['Roughness'].default_value = base[2]
        nt.links.new(col, b.inputs['Emission Color'])
        b.inputs['Emission Strength'].default_value = strength
    I._MATS[key] = m
    return m


def swirl(key, deep, col, pale, radius, arms=3, twist=7.0):
    """spiral vortex on a disc lying in its local XY plane: pale arms winding into a bright heart"""
    key = "mk_swirl_" + key
    if key in I._MATS:
        return I._MATS[key]
    m, nt, out = _blank(key)
    tc = nt.nodes.new('ShaderNodeTexCoord'); sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Object'], sep.inputs[0])
    ln = nt.nodes.new('ShaderNodeVectorMath'); ln.operation = 'LENGTH'
    nt.links.new(tc.outputs['Object'], ln.inputs[0])
    r = _math(nt, 'DIVIDE', ln.outputs['Value'], radius, clamp=True)
    th = _math(nt, 'ARCTAN2', sep.outputs[1], sep.outputs[0])
    no = nt.nodes.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = 9.0; no.inputs['Detail'].default_value = 2.0
    nt.links.new(tc.outputs['Object'], no.inputs['Vector'])
    ph = _math(nt, 'ADD', _math(nt, 'MULTIPLY', th, float(arms)), _math(nt, 'MULTIPLY', r, twist))
    ph = _math(nt, 'ADD', ph, _math(nt, 'MULTIPLY', no.outputs[0], 1.6))
    s = _math(nt, 'MULTIPLY_ADD', _math(nt, 'SINE', ph), 0.5, 0.5)                    # 0..1 arms
    inv = _math(nt, 'SUBTRACT', 1.0, r, clamp=True)
    core = _math(nt, 'POWER', inv, 2.6)
    v = _math(nt, 'ADD', _math(nt, 'MULTIPLY', s, _math(nt, 'MULTIPLY_ADD', inv, 0.45, 0.42)), _math(nt, 'MULTIPLY', core, 0.9), clamp=True)
    _emit_out(nt, out, _ramp(nt, v, [(0.0, deep), (0.34, col), (0.66, pale), (1.0, pw(pale, 0.25))]), 1.0)
    I._MATS[key] = m
    return m


def mist(key, deep, col, pale, scale=7.0):
    """slow smoke inside a crystal ball: cloudy emission, darker toward the rim so the ball keeps its volume"""
    key = "mk_mist_" + key
    if key in I._MATS:
        return I._MATS[key]
    m, nt, out = _blank(key)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    no = nt.nodes.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = scale; no.inputs['Detail'].default_value = 4.0
    no.inputs['Distortion'].default_value = 2.2
    nt.links.new(tc.outputs['Object'], no.inputs['Vector'])
    lw = nt.nodes.new('ShaderNodeLayerWeight'); lw.inputs[0].default_value = 0.5
    a = _math(nt, 'MULTIPLY_ADD', lw.outputs['Facing'], -0.45, 1.0)
    v = _math(nt, 'MULTIPLY', _math(nt, 'MULTIPLY_ADD', no.outputs[0], 1.9, -0.42, clamp=True), a, clamp=True)
    _emit_out(nt, out, _ramp(nt, v, [(0.0, deep), (0.40, col), (0.78, pale), (1.0, pw(pale, 0.3))]), 1.0)
    I._MATS[key] = m
    return m


def stone(tone=1.0, **o):
    """worn grey masonry (the kit rock is charred near-black by default: invisible on the map panel)"""
    p = dict(color=(0.115 * tone, 0.11 * tone, 0.105 * tone), ash=(0.36 * min(tone, 1.25), 0.35 * min(tone, 1.25), 0.33 * min(tone, 1.25)),
             scale=11.0, crack_w=0.03, ash_amt=0.5)
    p.update(o)
    return I.look('rock', **p)


def lamp(loc, srgb, watts=6.0, r=0.03):
    """small coloured point light ('fx_' objects are removed by iconlib.clear between icons)"""
    l = lin(srgb); mx = max(max(l), 1e-6)
    ld = bpy.data.lights.new("fx_lamp", 'POINT'); ld.energy = watts; ld.color = tuple(x / mx for x in l)
    ld.shadow_soft_size = r
    ob = bpy.data.objects.new("fx_lamp", ld); bpy.context.scene.collection.objects.link(ob)
    ob.location = loc
    return ob


def noshadow(objs):
    for o in objs:
        o.visible_shadow = False
    return objs


# ------------------------------------------------------------------------------------------------ shapes
def tongue(h, r, mat, seed=1, bend=0.0, name="fl"):
    """one flame tongue: a narrow foot, the belly a fifth of the way up, a long taper to a point that leans and flicks"""
    rnd = random.Random(seed)
    n = 16
    ph = rnd.uniform(0.0, 6.28)
    secs = []
    for i in range(n + 1):
        t = i / n
        rad = r * (0.42 + 0.58 * (t / 0.2) ** 0.8) if t < 0.2 else r * ((1.0 - t) / 0.8) ** 1.12
        ox = bend * h * t * t * (1.0 - 0.55 * t) + 0.035 * h * t * math.sin(5.0 * t + ph)
        oy = 0.02 * h * t * math.cos(4.0 * t + ph)
        secs.append(((ox, oy, h * t), max(rad, r * 0.015), max(rad * 0.86, r * 0.015)))
    return I.loft(secs, 14, mat, name, caps=(True, False))


def fire(loc, h, r, srgb, n=3, seed=1, spread=0.62, mat=None):
    """a cluster of flame tongues standing at loc (world space, not parented): one tall, the others lower beside it"""
    rnd = random.Random(seed)
    m = mat or fire_mat(srgb)
    out = [tongue(h, r, m, seed, rnd.uniform(-0.10, 0.10))]
    out[0].location = loc
    for i in range(n - 1):
        side = -1 if i % 2 == 0 else 1
        k = 1 + i // 2
        hh = h * rnd.uniform(0.52, 0.70) / (k ** 0.45); rr = r * rnd.uniform(0.58, 0.72)
        g = tongue(hh, rr, m, seed + i + 1, side * (0.16 + 0.12 * k))
        g.location = (loc[0] + side * r * spread * k, loc[1] - r * (0.30 if i % 2 else 0.12), loc[2])
        out.append(g)
    return noshadow(out)


def spline(pts, n=24):
    """Catmull-Rom through the points -> n + 1 samples"""
    P = [V(p) for p in pts]
    P = [P[0] * 2 - P[1]] + P + [P[-1] * 2 - P[-2]]
    segs = len(P) - 3
    out = []
    for i in range(n + 1):
        u = i / n * segs; k = min(int(u), segs - 1); t = u - k
        p0, p1, p2, p3 = P[k], P[k + 1], P[k + 2], P[k + 3]
        out.append(((p1 * 2) + (p2 - p0) * t + (p0 * 2 - p1 * 5 + p2 * 4 - p3) * (t * t) + (p1 * 3 - p0 - p2 * 3 + p3) * (t ** 3)) * 0.5)
    return out


def taper(radii, n=24):
    m = len(radii) - 1
    out = []
    for i in range(n + 1):
        u = i / n * m; k = min(int(u), m - 1); t = u - k
        out.append(radii[k] * (1 - t) + radii[k + 1] * t)
    return out


def limb(pts, radii, mat, n=20, seg=12, name="limb"):
    """smooth tapering tube through a few control points (horns, claws, bars)"""
    return I.tube(spline(pts, n), taper(radii, n), seg, mat, name)


def rz(p, deg):
    c, s = math.cos(R(deg)), math.sin(R(deg))
    return (p[0] * c - p[1] * s, p[0] * s + p[1] * c, p[2])


def bounds(o):
    bpy.context.view_layer.update()
    cs = [o.matrix_world @ v.co for v in o.data.vertices]
    lo = V((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs)))
    hi = V((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs)))
    return lo, hi


def block(pts, mat, name="block", rough=0.0, seed=0):
    """faceted stone block from its corner points (convex hull), optionally roughened"""
    o = I.hull(pts, mat, name)
    if rough:
        I.displace(o, rough, 0.12, seed)
    return o


def frustum(w0, d0, w1, d1, h, mat, name="frustum", z=0.0):
    """box that tapers from w0 x d0 at the bottom to w1 x d1 at the top"""
    pts = [(sx * w0 / 2, sy * d0 / 2, z) for sx in (-1, 1) for sy in (-1, 1)] + [(sx * w1 / 2, sy * d1 / 2, z + h) for sx in (-1, 1) for sy in (-1, 1)]
    return I.hull(pts, mat, name)


def arch_outline(ro, ri, hs, n=14):
    """closed outline of an arch (an upside-down U): outer radius ro, inner radius ri, springing height hs"""
    outer = [(ro * math.cos(R(180 - 180 * i / n)), hs + ro * math.sin(R(180 - 180 * i / n))) for i in range(n + 1)]
    inner = [(ri * math.cos(R(180 * i / n)), hs + ri * math.sin(R(180 * i / n))) for i in range(n + 1)]
    return [(-ro, 0.0)] + outer + [(ro, 0.0), (ri, 0.0)] + inner + [(-ri, 0.0)]


def door_outline(hw, hs, n=10, sq=1.0):
    """filled round-topped door shape: half width hw, springing height hs (sq < 1 flattens the arch)"""
    return [(-hw, 0.0)] + [(hw * math.cos(R(180 - 180 * i / n)), hs + sq * hw * math.sin(R(180 - 180 * i / n))) for i in range(n + 1)] + [(hw, 0.0)]
