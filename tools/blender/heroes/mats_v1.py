"""Procedural source materials (Cycles, baked to atlases) and final glTF PBR materials."""
import bpy, math, os
import numpy as np
from rigio import *

SRCM = {}       # key -> source material
SRCINFO = {}    # key -> dict(color, rough, metal) representative values for fast previews


# ============================================================================================ node helper
class G:
    def __init__(self, mat):
        self.mat = mat
        self.nt = mat.node_tree
        self.N = self.nt.nodes
        self.L = self.nt.links
        for n in list(self.N):
            self.N.remove(n)
        self.geom = self.N.new('ShaderNodeNewGeometry')
        self.pos = self.geom.outputs['Position']
        self._luv = None

    def put(self, sock, val):
        if val is None:
            return
        if isinstance(val, bpy.types.NodeSocket):
            self.L.new(val, sock)
        else:
            try:
                if hasattr(sock.default_value, '__len__'):
                    n = len(sock.default_value)
                    if np.isscalar(val):
                        val = (val,) * n
                    v = tuple(val)
                    if len(v) == 3 and n == 4:
                        v = v + (1.0,)
                    sock.default_value = v[:n]
                else:
                    sock.default_value = val
            except Exception as e:
                raise RuntimeError("put %s %r: %s" % (sock.name, val, e))

    def node(self, typ, **kw):
        n = self.N.new(typ)
        for k, v in kw.items():
            setattr(n, k, v)
        return n

    def math(self, op, a, b=None, c=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.put(n.inputs[0], a)
        if b is not None:
            self.put(n.inputs[1], b)
        if c is not None:
            self.put(n.inputs[2], c)
        return n.outputs[0]

    def add(self, a, b):
        return self.math('ADD', a, b)

    def mul(self, a, b):
        return self.math('MULTIPLY', a, b)

    def sub(self, a, b):
        return self.math('SUBTRACT', a, b)

    def inv(self, a):
        return self.math('SUBTRACT', 1.0, a)

    def clamp(self, a):
        return self.math('MINIMUM', self.math('MAXIMUM', a, 0.0), 1.0)

    def sstep(self, x, e0, e1):
        n = self.node('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP')
        self.put(n.inputs['Value'], x)
        n.inputs['From Min'].default_value = e0
        n.inputs['From Max'].default_value = e1
        n.inputs['To Min'].default_value = 0.0
        n.inputs['To Max'].default_value = 1.0
        return n.outputs['Result']

    def lin(self, x, e0, e1, o0=0.0, o1=1.0):
        n = self.node('ShaderNodeMapRange', interpolation_type='LINEAR')
        n.clamp = True
        self.put(n.inputs['Value'], x)
        n.inputs['From Min'].default_value = e0
        n.inputs['From Max'].default_value = e1
        n.inputs['To Min'].default_value = o0
        n.inputs['To Max'].default_value = o1
        return n.outputs['Result']

    def mix(self, fac, a, b, blend='MIX'):
        n = self.node('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        n.clamp_factor = True
        self.put(n.inputs[0], fac)
        self.put(n.inputs[6], a)
        self.put(n.inputs[7], b)
        return n.outputs[2]

    def mixf(self, fac, a, b):
        n = self.node('ShaderNodeMix', data_type='FLOAT')
        n.clamp_factor = True
        self.put(n.inputs[0], fac)
        self.put(n.inputs[2], a)
        self.put(n.inputs[3], b)
        return n.outputs[0]

    def mapping(self, vec=None, scale=(1, 1, 1), rot=(0, 0, 0), loc=(0, 0, 0)):
        n = self.node('ShaderNodeMapping')
        self.put(n.inputs['Vector'], vec if vec is not None else self.pos)
        n.inputs['Scale'].default_value = scale
        n.inputs['Rotation'].default_value = rot
        n.inputs['Location'].default_value = loc
        return n.outputs['Vector']

    def noise(self, scale, detail=4.0, rough=0.55, dist=0.0, vec=None, lac=2.0):
        n = self.node('ShaderNodeTexNoise')
        self.put(n.inputs['Vector'], vec if vec is not None else self.pos)
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = rough
        n.inputs['Lacunarity'].default_value = lac
        n.inputs['Distortion'].default_value = dist
        return n.outputs[0]

    def vor(self, scale, feature='F1', out='Distance', vec=None, rnd=1.0, smooth=None):
        n = self.node('ShaderNodeTexVoronoi', feature=feature)
        self.put(n.inputs['Vector'], vec if vec is not None else self.pos)
        n.inputs['Scale'].default_value = scale
        n.inputs['Randomness'].default_value = rnd
        if smooth is not None and 'Smoothness' in n.inputs:
            n.inputs['Smoothness'].default_value = smooth
        return n.outputs[out]

    def wave(self, scale, vec=None, direction='X', dist=0.0):
        n = self.node('ShaderNodeTexWave', wave_type='BANDS', bands_direction=direction, wave_profile='SIN')
        self.put(n.inputs['Vector'], vec if vec is not None else self.pos)
        n.inputs['Scale'].default_value = scale
        n.inputs['Distortion'].default_value = dist
        return n.outputs['Fac']

    def sep(self, vec):
        n = self.node('ShaderNodeSeparateXYZ')
        self.put(n.inputs[0], vec)
        return n.outputs[0], n.outputs[1], n.outputs[2]

    def comb(self, x, y, z):
        n = self.node('ShaderNodeCombineXYZ')
        self.put(n.inputs[0], x)
        self.put(n.inputs[1], y)
        self.put(n.inputs[2], z)
        return n.outputs[0]

    def luv(self):
        """(u, v) of lofted parts (vertex attribute 'luv')"""
        if self._luv is None:
            a = self.node('ShaderNodeAttribute', attribute_name='luv')
            x, y, z = self.sep(a.outputs['Vector'])
            self._luv = (x, y)
        return self._luv

    def edge(self, radius=0.008, gain=3.0):
        """mask of geometric edges (bevel-normal vs geometric normal)"""
        b = self.node('ShaderNodeBevel', samples=6)
        b.inputs['Radius'].default_value = radius
        d = self.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
        self.L.new(b.outputs[0], d.inputs[0])
        self.L.new(self.geom.outputs['Normal'], d.inputs[1])
        return self.clamp(self.mul(self.inv(d.outputs['Value']), gain))

    def finish(self, color, rough, metal, height=None, bump=0.35, bump_dist=0.004):
        bs = self.node('ShaderNodeBsdfPrincipled', name='BSDF')
        self.put(bs.inputs['Base Color'], color)
        self.put(bs.inputs['Roughness'], rough)
        self.put(bs.inputs['Metallic'], metal)
        if height is not None:
            bp = self.node('ShaderNodeBump')
            bp.inputs['Strength'].default_value = bump
            bp.inputs['Distance'].default_value = bump_dist
            self.put(bp.inputs['Height'], height)
            self.L.new(bp.outputs[0], bs.inputs['Normal'])
        cr = self.node('NodeReroute', name='COLOR')
        cn = self.mix(1.0, (0, 0, 0, 1), color)
        self.L.new(cn, cr.inputs[0])
        orm = self.node('ShaderNodeCombineColor', name='ORMC')
        orm.inputs[0].default_value = 1.0
        self.put(orm.inputs[1], rough)
        self.put(orm.inputs[2], metal)
        orr = self.node('NodeReroute', name='ORM')
        self.L.new(orm.outputs[0], orr.inputs[0])
        em = self.node('ShaderNodeEmission', name='EMIT')
        out = self.node('ShaderNodeOutputMaterial', name='OUT')
        self.L.new(bs.outputs[0], out.inputs[0])
        tg = self.node('ShaderNodeTexImage', name='TARGET')
        self.N.active = tg


def _new(key):
    m = bpy.data.materials.new("src_" + key)
    m.use_nodes = True
    SRCM[key] = m
    return m, G(m)


def _lum(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def _scale(c, k):
    return tuple(min(1.0, max(0.0, v * k)) for v in c[:3])


def _mixc(a, b, t):
    return tuple(a[i] * (1 - t) + b[i] * t for i in range(3))


ASH = (0.15, 0.14, 0.13)


# ============================================================================================ materials
def metal(key, base=(0.28, 0.27, 0.265), rough=(0.30, 0.62), edge=None, edge_amt=0.7, rust=(0.13, 0.06, 0.03),
          rust_amt=0.25, pattern='plate', dark=None, ash=0.25, ash_h=0.45, bump=0.3, soot=0.0):
    """Worn steel / iron / brass. pattern: plate | hammered | mail | brushed | scale"""
    m, g = _new(key)
    edge = edge or _scale(base, 2.1)
    dark = dark or _scale(base, 0.45)
    n1 = g.noise(5.0, 5, 0.6)
    n2 = g.noise(38.0, 4, 0.65)
    n3 = g.noise(11.0, 6, 0.7, dist=0.6)
    col = g.mix(g.sstep(n1, 0.35, 0.75), dark, base)
    col = g.mix(g.mul(g.sstep(n2, 0.45, 0.8), 0.35), col, _scale(base, 1.35))
    rgh = g.lin(n1, 0.3, 0.75, rough[1], rough[0])
    rgh = g.add(rgh, g.mul(g.sub(n2, 0.5), 0.25))
    met = 1.0
    height = g.mul(n2, 0.25)
    if pattern == 'hammered':
        d = g.vor(55.0, 'SMOOTH_F1', smooth=0.6)
        height = g.add(height, g.mul(d, 1.6))
        rgh = g.add(rgh, g.mul(g.sub(d, 0.3), 0.12))
    elif pattern == 'brushed':
        s = g.noise(30.0, 3, 0.6, vec=g.mapping(scale=(1, 1, 22)))
        height = g.add(height, g.mul(s, 0.5))
        rgh = g.add(rgh, g.mul(g.sub(s, 0.5), 0.2))
    elif pattern == 'mail':
        d = g.vor(150.0, 'F1')
        ring = g.sstep(d, 0.55, 0.25)
        hole = g.sstep(d, 0.22, 0.05)
        ringm = g.sub(ring, g.mul(hole, 0.8))
        col = g.mix(g.inv(ring), col, _scale(base, 0.22))
        col = g.mix(g.mul(hole, 0.7), col, _scale(base, 0.3))
        height = g.add(height, g.mul(ringm, 2.2))
        rgh = g.add(rgh, g.mul(g.inv(ring), 0.25))
    elif pattern == 'scale':
        x, y, z = g.sep(g.pos)
        d = g.vor(42.0, 'F1', vec=g.mapping(scale=(1, 1, 0.7)))
        sc = g.sstep(d, 0.75, 0.2)
        height = g.add(height, g.mul(sc, 2.0))
        col = g.mix(g.inv(sc), col, _scale(base, 0.35))
    # scratches
    sc1 = g.noise(18.0, 2, 0.5, vec=g.mapping(scale=(1, 14, 1), rot=(0.3, 0.2, 0.5)))
    scm = g.sstep(sc1, 0.68, 0.74)
    col = g.mix(g.mul(scm, 0.35), col, edge)
    rgh = g.sub(rgh, g.mul(scm, 0.12))
    # edge wear
    if edge_amt > 0:
        e = g.mul(g.edge(0.007, 3.5), g.lin(n3, 0.25, 0.7, 0.25, 1.0))
        e = g.clamp(g.mul(e, edge_amt * 1.5))
        col = g.mix(e, col, edge)
        rgh = g.mixf(e, rgh, rough[0] * 0.8)
    # rust / grime patches
    if rust_amt > 0:
        r = g.sstep(n3, 1.0 - rust_amt * 0.9, 1.02 - rust_amt * 0.5)
        r2 = g.mul(r, g.lin(n2, 0.3, 0.7, 0.5, 1.0))
        col = g.mix(r2, col, rust)
        rgh = g.mixf(r2, rgh, 0.85)
        met = g.mixf(r2, 1.0, 0.25)
        height = g.add(height, g.mul(r2, 0.6))
    if soot > 0:
        s = g.mul(g.sstep(g.noise(3.0, 4, 0.6), 0.4, 0.75), soot)
        col = g.mix(s, col, (0.012, 0.011, 0.01))
        rgh = g.mixf(s, rgh, 0.8)
    if ash > 0:
        x, y, z = g.sep(g.pos)
        a = g.mul(g.sstep(z, ash_h, 0.0), g.lin(n3, 0.2, 0.8, 0.3, 1.0))
        a = g.mul(a, ash)
        col = g.mix(a, col, ASH)
        rgh = g.mixf(a, rgh, 0.9)
        met = g.mixf(a, met, 0.3)
    g.finish(col, g.clamp(rgh), met, height, bump)
    SRCINFO[key] = dict(color=base, rough=(rough[0] + rough[1]) / 2, metal=1.0)
    return key


def cloth(key, base=(0.10, 0.03, 0.02), var=0.3, ash=0.5, ash_h=0.55, folds=0.5, weave=1.0, worn=0.4, bands=None,
          rough=(0.82, 0.98), fine=1.0, bump=0.4):
    """Heavy wool / linen. bands = [(v0, v1, colour)] trim bands along the loft v parameter."""
    m, g = _new(key)
    n1 = g.noise(4.0, 5, 0.6)
    n2 = g.noise(26.0, 4, 0.7)
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(base, 1.0 - var), _scale(base, 1.0 + var))
    faded = _mixc(base, (_lum(base) * 1.6,) * 3, 0.55)
    col = g.mix(g.mul(g.sstep(n2, 0.5, 0.85), worn), col, faded)
    n3 = g.noise(13.0, 5, 0.7, dist=0.8)
    col = g.mix(g.mul(g.sstep(n3, 0.5, 0.25), 0.45), col, _scale(base, 0.55))        # darker stains / damp patches
    col = g.mix(g.mul(g.sstep(n3, 0.58, 0.8), 0.25), col, _scale(base, 1.45))
    if bands:
        u, v = g.luv()
        for (v0, v1, c) in bands:
            b = g.mul(g.sstep(v, v0 - 0.004, v0 + 0.004), g.sstep(v, v1 + 0.004, v1 - 0.004))
            cb = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(c, 0.75), _scale(c, 1.2))
            col = g.mix(b, col, cb)
    rgh = g.lin(n2, 0.3, 0.7, rough[0], rough[1])
    # weave + fuzz + folds (weave only in the bump, at a frequency the atlas can resolve)
    w1 = g.wave(120.0 * fine, direction='X', dist=1.5)
    w2 = g.wave(120.0 * fine, direction='Z', dist=1.5)
    w3 = g.wave(120.0 * fine, direction='Y', dist=1.5)
    wv = g.mul(g.add(g.add(w1, w2), w3), 0.035 * weave)
    fz = g.noise(170.0, 2, 0.5)
    height = g.add(wv, g.mul(fz, 0.7))
    col = g.mix(g.mul(g.sstep(fz, 0.35, 0.7), 0.22), col, _scale(base, 0.62))
    if folds > 0:
        f = g.noise(7.0, 3, 0.5, dist=0.4, vec=g.mapping(scale=(1.0, 1.0, 0.12)))
        height = g.add(height, g.mul(f, 9.0 * folds))
        col = g.mix(g.mul(g.sstep(f, 0.55, 0.3), 0.3 * folds), col, _scale(base, 0.55))
    if ash > 0:
        x, y, z = g.sep(g.pos)
        a = g.mul(g.sstep(z, ash_h, 0.02), g.lin(n2, 0.25, 0.8, 0.35, 1.0))
        a = g.mul(a, ash)
        col = g.mix(a, col, ASH)
    g.finish(col, g.clamp(rgh), 0.0, height, bump)
    SRCINFO[key] = dict(color=base, rough=0.9, metal=0.0)
    return key


def leather(key, base=(0.07, 0.04, 0.025), edge=None, crack=1.0, rough=(0.5, 0.78), ash=0.35, ash_h=0.4, bump=0.2,
            worn=0.5):
    m, g = _new(key)
    edge = edge or _mixc(_scale(base, 2.6), (0.30, 0.22, 0.14), 0.4)
    n1 = g.noise(6.0, 5, 0.6)
    n2 = g.noise(45.0, 4, 0.6)
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(base, 0.65), _scale(base, 1.3))
    cr = g.vor(95.0, 'DISTANCE_TO_EDGE')
    crm = g.mul(g.sstep(cr, 0.07, 0.0), crack)
    col = g.mix(g.mul(crm, 0.7), col, _scale(base, 0.3))
    grain = g.vor(260.0, 'F1')
    height = g.add(g.mul(g.inv(crm), 1.0), g.mul(grain, 0.5))
    scuff = g.mul(g.sstep(n2, 0.55, 0.8), worn)
    col = g.mix(g.mul(scuff, 0.5), col, edge)
    rgh = g.lin(n1, 0.3, 0.75, rough[0], rough[1])
    rgh = g.add(rgh, g.mul(scuff, 0.2))
    e = g.clamp(g.mul(g.mul(g.edge(0.006, 3.0), g.lin(n2, 0.3, 0.7, 0.3, 1.0)), 1.2 * worn))
    col = g.mix(e, col, edge)
    if ash > 0:
        x, y, z = g.sep(g.pos)
        a = g.mul(g.mul(g.sstep(z, ash_h, 0.0), g.lin(n2, 0.25, 0.8, 0.35, 1.0)), ash)
        col = g.mix(a, col, ASH)
        rgh = g.mixf(a, rgh, 0.92)
    g.finish(col, g.clamp(rgh), 0.0, height, bump)
    SRCINFO[key] = dict(color=base, rough=0.6, metal=0.0)
    return key


def fur(key, base=(0.06, 0.045, 0.035), tip=(0.22, 0.18, 0.14), strand=1.0, bump=0.8):
    m, g = _new(key)
    s1 = g.noise(16.0, 3, 0.6, dist=0.5, vec=g.mapping(scale=(14.0, 14.0, 1.0)))
    s2 = g.noise(40.0, 2, 0.5, vec=g.mapping(scale=(9.0, 9.0, 0.8)))
    cl = g.noise(7.0, 4, 0.6)
    st = g.add(g.mul(s1, 0.65), g.mul(s2, 0.35))
    col = g.mix(g.sstep(st, 0.35, 0.72), base, tip)
    col = g.mix(g.mul(g.sstep(cl, 0.55, 0.3), 0.6), col, _scale(base, 0.5))
    rgh = g.lin(st, 0.3, 0.7, 0.95, 0.72)
    g.finish(col, rgh, 0.0, g.mul(st, 3.0 * strand), bump, 0.006)
    SRCINFO[key] = dict(color=_mixc(base, tip, 0.4), rough=0.85, metal=0.0)
    return key


def bone(key, base=(0.50, 0.44, 0.33), stain=(0.16, 0.10, 0.05), rough=(0.42, 0.7), cracks=1.0, bump=0.3):
    m, g = _new(key)
    n1 = g.noise(7.0, 5, 0.65)
    n2 = g.noise(60.0, 3, 0.6)
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(base, 0.72), _scale(base, 1.12))
    col = g.mix(g.mul(g.sstep(n1, 0.52, 0.25), 0.55), col, stain)
    cr = g.noise(9.0, 4, 0.7, dist=1.5, vec=g.mapping(scale=(6.0, 6.0, 0.6)))
    crm = g.mul(g.sstep(g.math('ABSOLUTE', g.sub(cr, 0.5)), 0.012, 0.0), cracks)
    col = g.mix(g.mul(crm, 0.8), col, _scale(stain, 0.5))
    pores = g.sstep(g.vor(220.0, 'F1'), 0.25, 0.1)
    col = g.mix(g.mul(pores, 0.25), col, stain)
    rgh = g.lin(n2, 0.3, 0.7, rough[0], rough[1])
    height = g.add(g.mul(g.inv(crm), 1.0), g.mul(n2, 0.4))
    g.finish(col, rgh, 0.0, height, bump)
    SRCINFO[key] = dict(color=base, rough=0.55, metal=0.0)
    return key


def wood(key, base=(0.085, 0.05, 0.028), dark=None, rough=(0.55, 0.8), bump=0.45, luv_grain=True):
    """Staff / haft wood. Grain follows the tube direction through the 'luv' attribute (u=angle, v=metres)."""
    m, g = _new(key)
    dark = dark or _scale(base, 0.35)
    if luv_grain:
        u, v = g.luv()
        vec = g.comb(g.mul(u, 9.0), g.mul(v, 3.0), 0.0)
    else:
        vec = g.mapping(scale=(40.0, 40.0, 3.0))
    gr = g.noise(4.0, 5, 0.65, dist=0.8, vec=vec)
    n1 = g.noise(9.0, 4, 0.6)
    col = g.mix(g.sstep(gr, 0.35, 0.7), dark, base)
    col = g.mix(g.mul(g.sstep(n1, 0.55, 0.8), 0.4), col, _scale(base, 1.6))
    rgh = g.lin(gr, 0.3, 0.7, rough[1], rough[0])
    e = g.clamp(g.mul(g.edge(0.006, 3.0), 0.8))
    col = g.mix(e, col, _scale(base, 1.9))
    g.finish(col, rgh, 0.0, g.mul(gr, 2.0), bump)
    SRCINFO[key] = dict(color=base, rough=0.7, metal=0.0)
    return key


def porcelain(key, base=(0.62, 0.60, 0.55), line=(0.10, 0.07, 0.05), rough=(0.16, 0.38), crackle=1.0):
    m, g = _new(key)
    n1 = g.noise(6.0, 4, 0.6)
    cr = g.vor(48.0, 'DISTANCE_TO_EDGE')
    crm = g.mul(g.sstep(cr, 0.035, 0.0), crackle)
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(base, 0.82), _scale(base, 1.05))
    col = g.mix(g.mul(crm, 0.85), col, line)
    col = g.mix(g.mul(g.sstep(n1, 0.6, 0.85), 0.35), col, _mixc(base, line, 0.5))
    rgh = g.lin(n1, 0.3, 0.75, rough[0], rough[1])
    rgh = g.add(rgh, g.mul(crm, 0.4))
    g.finish(col, g.clamp(rgh), 0.0, g.inv(crm), 0.25)
    SRCINFO[key] = dict(color=base, rough=0.25, metal=0.0)
    return key


def flat(key, color, rough=0.6, metal=0.0):
    """Plain coloured material with only faint variation (rope, wax, paper, glass-ish props)."""
    m, g = _new(key)
    n1 = g.noise(20.0, 4, 0.6)
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(color, 0.75), _scale(color, 1.2))
    g.finish(col, g.lin(n1, 0.3, 0.7, rough * 0.85, min(1.0, rough * 1.15)), metal, g.mul(n1, 1.0), 0.2)
    SRCINFO[key] = dict(color=color, rough=rough, metal=metal)
    return key


def emis(key, color, strength=6.0, base=None):
    """Emissive accent (not baked)."""
    SRCINFO[key] = dict(color=base or _scale(color, 0.25), rough=0.5, metal=0.0, emis=color, strength=strength)
    return key


# ============================================================================================ final materials
def _occlusion_group():
    ng = bpy.data.node_groups.get("glTF Material Output")
    if ng is None:
        ng = bpy.data.node_groups.new("glTF Material Output", 'ShaderNodeTree')
        ng.interface.new_socket("Occlusion", in_out='INPUT', socket_type='NodeSocketFloat')
        ng.nodes.new('NodeGroupInput')
    return ng


def load_img(path, colorspace='sRGB'):
    img = bpy.data.images.load(path, check_existing=False)
    img.colorspace_settings.name = colorspace
    return img


def final_material(name, color_path=None, orm_path=None, normal_path=None, color=(0.5, 0.5, 0.5), rough=0.6,
                   metal=0.0, emis=None, strength=0.0, double_sided=False, occlusion=True, normal_strength=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    bs = nt.nodes.new('ShaderNodeBsdfPrincipled')
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(bs.outputs[0], out.inputs[0])
    bs.inputs['Base Color'].default_value = tuple(color[:3]) + (1.0,)
    bs.inputs['Roughness'].default_value = rough
    bs.inputs['Metallic'].default_value = metal
    if color_path:
        t = nt.nodes.new('ShaderNodeTexImage')
        t.image = load_img(color_path, 'sRGB')
        nt.links.new(t.outputs['Color'], bs.inputs['Base Color'])
    if orm_path:
        t = nt.nodes.new('ShaderNodeTexImage')
        t.image = load_img(orm_path, 'Non-Color')
        sp = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(t.outputs['Color'], sp.inputs[0])
        nt.links.new(sp.outputs[1], bs.inputs['Roughness'])
        nt.links.new(sp.outputs[2], bs.inputs['Metallic'])
        if occlusion:
            gn = nt.nodes.new('ShaderNodeGroup')
            gn.node_tree = _occlusion_group()
            nt.links.new(sp.outputs[0], gn.inputs[0])
    if normal_path:
        t = nt.nodes.new('ShaderNodeTexImage')
        t.image = load_img(normal_path, 'Non-Color')
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nm.inputs['Strength'].default_value = normal_strength
        nt.links.new(t.outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs[0], bs.inputs['Normal'])
    if emis is not None:
        bs.inputs['Emission Color'].default_value = tuple(emis[:3]) + (1.0,)
        bs.inputs['Emission Strength'].default_value = strength
    m.use_backface_culling = not double_sided
    return m


# ============================================================================================ image utils
def lin2srgb(x):
    x = np.clip(x, 0.0, 1.0)
    return np.where(x <= 0.0031308, x * 12.92, 1.055 * np.power(x, 1 / 2.4) - 0.055)


def srgb2lin(x):
    x = np.clip(x, 0.0, 1.0)
    return np.where(x <= 0.04045, x / 12.92, np.power((x + 0.055) / 1.055, 2.4))


def save_rgb(arr, path, quality=92):
    """arr (H, W, 3) floats 0..1 already encoded for display (sRGB or data) -> jpg/png file"""
    h, w = arr.shape[:2]
    img = bpy.data.images.new("tmp_save", w, h, alpha=False, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color'   # store values verbatim
    px = np.ones((h, w, 4), dtype=np.float32)
    px[..., :3] = np.clip(arr[..., :3], 0, 1)
    img.pixels.foreach_set(px.ravel())
    ext = os.path.splitext(path)[1].lower()
    img.filepath_raw = path
    img.file_format = 'JPEG' if ext in ('.jpg', '.jpeg') else 'PNG'
    try:
        img.save(filepath=path, quality=quality)
    except TypeError:
        img.save()
    bpy.data.images.remove(img)
    return path


def read_img(path, size=None):
    """-> (H, W, 4) float array of stored (display-encoded) values; optional resize to (w, h)"""
    img = bpy.data.images.load(path, check_existing=False)
    img.colorspace_settings.name = 'Non-Color'
    if size is not None and (img.size[0] != size[0] or img.size[1] != size[1]):
        img.scale(size[0], size[1])
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)
