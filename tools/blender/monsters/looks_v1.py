"""Procedural 'looks' (object-space shader recipes) + atlas unwrap/bake + export-material assignment."""
import bpy, math, os, warnings
import numpy as np
from mathutils import Vector
import monlib as M

warnings.filterwarnings("ignore", category=DeprecationWarning)


# ------------------------------------------------------------------------------------------------ node DSL
class NB:
    def __init__(self, mat):
        self.mat = mat
        mat.use_nodes = True
        self.nt = mat.node_tree
        for n in list(self.nt.nodes):
            self.nt.nodes.remove(n)
        self._co = None
        self._geo = None

    def new(self, typ, **props):
        n = self.nt.nodes.new(typ)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def set(self, sock, val):
        if val is None:
            return
        if isinstance(val, bpy.types.NodeSocket):
            self.nt.links.new(val, sock)
            return
        if isinstance(val, (int, float)):
            try:
                sock.default_value = float(val)
            except Exception:
                n = len(sock.default_value)
                sock.default_value = (float(val),) * 3 + ((1.0,) if n == 4 else ())
        else:
            v = tuple(float(x) for x in val)
            try:
                n = len(sock.default_value)
            except Exception:
                sock.default_value = sum(v[:3]) / 3.0
                return
            if n == 4 and len(v) == 3:
                v = v + (1.0,)
            sock.default_value = v[:n]

    # --- inputs
    def coord(self):
        if self._co is None:
            self._co = self.new('ShaderNodeTexCoord').outputs['Object']
        return self._co

    def geo(self):
        if self._geo is None:
            self._geo = self.new('ShaderNodeNewGeometry')
        return self._geo

    def mapping(self, vec, scale=(1, 1, 1), rot=(0, 0, 0), loc=(0, 0, 0)):
        n = self.new('ShaderNodeMapping')
        self.set(n.inputs['Vector'], vec)
        n.inputs['Scale'].default_value = scale
        n.inputs['Rotation'].default_value = rot
        n.inputs['Location'].default_value = loc
        return n.outputs[0]

    def align(self, vec, axis, squash=0.08):
        """coords rotated so `axis` becomes Z and squashed along it (for grain / streaks along axis)"""
        q = Vector(axis).normalized().rotation_difference(Vector((0, 0, 1)))
        a = self.mapping(vec, rot=tuple(q.to_euler()))
        return self.mapping(a, scale=(1, 1, squash))

    # --- textures
    def noise(self, scale, detail=3.0, rough=0.55, vec=None, typ='FBM', dist=0.0, color=False, lac=2.0):
        n = self.new('ShaderNodeTexNoise', noise_dimensions='3D', noise_type=typ)
        self.set(n.inputs['Vector'], self.coord() if vec is None else vec)
        self.set(n.inputs['Scale'], scale); self.set(n.inputs['Detail'], detail)
        self.set(n.inputs['Roughness'], rough); self.set(n.inputs['Distortion'], dist)
        self.set(n.inputs['Lacunarity'], lac)
        return n.outputs[1 if color else 0]

    def voro(self, scale, feature='F1', vec=None, rand=1.0, out='Distance', smooth=0.5):
        n = self.new('ShaderNodeTexVoronoi', voronoi_dimensions='3D', feature=feature)
        self.set(n.inputs['Vector'], self.coord() if vec is None else vec)
        self.set(n.inputs['Scale'], scale); self.set(n.inputs['Randomness'], rand)
        if feature == 'SMOOTH_F1':
            self.set(n.inputs['Smoothness'], smooth)
        return n.outputs[out]

    def warp(self, amount, scale, vec=None):
        """coordinates distorted by colour noise"""
        vec = self.coord() if vec is None else vec
        c = self.noise(scale, 2.0, 0.5, vec=vec, color=True)
        s = self.new('ShaderNodeVectorMath', operation='SUBTRACT')
        self.set(s.inputs[0], c); s.inputs[1].default_value = (0.5, 0.5, 0.5)
        m = self.new('ShaderNodeVectorMath', operation='SCALE')
        self.set(m.inputs[0], s.outputs[0]); m.inputs['Scale'].default_value = amount
        a = self.new('ShaderNodeVectorMath', operation='ADD')
        self.set(a.inputs[0], vec); self.set(a.inputs[1], m.outputs[0])
        return a.outputs[0]

    # --- maths
    def math(self, op, a, b=None, c=None, clamp=False):
        n = self.new('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.set(n.inputs[0], a)
        if b is not None:
            self.set(n.inputs[1], b)
        if c is not None:
            self.set(n.inputs[2], c)
        return n.outputs[0]

    def mapr(self, v, a, b, c=0.0, d=1.0, smooth=True):
        n = self.new('ShaderNodeMapRange', interpolation_type='SMOOTHSTEP' if smooth else 'LINEAR', clamp=True)
        self.set(n.inputs[0], v)
        n.inputs[1].default_value = a; n.inputs[2].default_value = b
        self.set(n.inputs[3], c); self.set(n.inputs[4], d)
        return n.outputs[0]

    def ramp(self, fac, stops, interp='LINEAR'):
        n = self.new('ShaderNodeValToRGB')
        cr = n.color_ramp
        cr.interpolation = interp
        while len(cr.elements) < len(stops):
            cr.elements.new(0.5)
        for e, (pos, col) in zip(cr.elements, stops):
            e.position = pos
            if isinstance(col, (int, float)):
                col = (col, col, col)
            e.color = tuple(col) + (1.0,)
        self.set(n.inputs[0], fac)
        return n.outputs[0]

    def mix(self, fac, a, b, blend='MIX'):
        n = self.new('ShaderNodeMix', data_type='RGBA', blend_type=blend, clamp_factor=True)
        self.set(n.inputs[0], fac); self.set(n.inputs[6], a); self.set(n.inputs[7], b)
        return n.outputs[2]

    def mixf(self, fac, a, b):
        n = self.new('ShaderNodeMix', data_type='FLOAT', clamp_factor=True)
        self.set(n.inputs[0], fac); self.set(n.inputs[2], a); self.set(n.inputs[3], b)
        return n.outputs[0]

    def mul(self, col, f):
        """colour * scalar"""
        return self.mix(1.0, col, f, 'MULTIPLY')

    def sep(self, vec):
        n = self.new('ShaderNodeSeparateXYZ')
        self.set(n.inputs[0], vec)
        return n.outputs[0], n.outputs[1], n.outputs[2]

    def pointy(self, lo=0.47, hi=0.56):
        return self.mapr(self.geo().outputs['Pointiness'], lo, hi)

    def upfacing(self, lo=0.35, hi=0.9):
        _, _, z = self.sep(self.geo().outputs['Normal'])
        return self.mapr(z, lo, hi)

    def height_z(self, lo, hi):
        """0 at z<=lo .. 1 at z>=hi (object space)"""
        _, _, z = self.sep(self.coord())
        return self.mapr(z, lo, hi)


def _c(p, k, default):
    return p.get(k, default)


# ------------------------------------------------------------------------------------------------ look kinds
# every kind returns dict(color, rough, metal, height, bump=(strength, distance))

def k_bone(N, p):
    col = _c(p, 'color', (0.56, 0.50, 0.38))
    stain = _c(p, 'stain', (0.20, 0.14, 0.08))
    big = N.noise(_c(p, 'blotch', 7.0), 4, 0.6)
    fine = N.noise(70, 3, 0.6)
    crack = N.voro(_c(p, 'crack', 26.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.04, 9))
    crack_m = N.mapr(crack, 0.0, 0.035, 1.0, 0.0)
    c = N.mix(N.mapr(big, 0.42, 0.68), col, stain)
    c = N.mix(N.mapr(fine, 0.3, 0.8, 0.0, 0.35), c, N.mul(c, 0.55))
    c = N.mix(N.math('MULTIPLY', crack_m, _c(p, 'crack_amt', 0.6)), c, stain)
    edge = N.pointy()
    c = N.mix(N.math('MULTIPLY', edge, 0.35), c, (0.72, 0.68, 0.56))
    grime = N.height_z(_c(p, 'dirt_lo', -1.0), _c(p, 'dirt_hi', -0.5))  # 0 near ground
    c = N.mix(N.math('SUBTRACT', 1.0, grime), c, N.mix(0.6, c, _c(p, 'dirt', (0.10, 0.085, 0.07))))
    rough = N.mapr(big, 0.3, 0.8, 0.62, 0.86)
    h = N.math('ADD', N.math('MULTIPLY', fine, 0.5), N.math('MULTIPLY', N.mapr(crack, 0.0, 0.05), 0.6))
    return dict(color=c, rough=rough, metal=0.0, height=h, bump=(_c(p, 'bump', 0.35), 0.004))


def k_metal(N, p):
    """worn / corroded metal. color = clean metal, rust = corrosion colour, amount 0..1"""
    col = _c(p, 'color', (0.22, 0.21, 0.20))
    rust = _c(p, 'rust', (0.23, 0.095, 0.035))
    rust2 = _c(p, 'rust2', (0.09, 0.04, 0.025))
    amt = _c(p, 'amount', 0.5)
    big = N.noise(_c(p, 'scale', 8.0), 5, 0.68)
    lo = 0.72 - amt * 0.5
    mask = N.mapr(big, lo, lo + 0.14)
    pits = N.mapr(N.voro(75, 'F1'), 0.0, 0.32, 1.0, 0.0)
    mask = N.math('MAXIMUM', mask, N.math('MULTIPLY', pits, N.mapr(big, lo - 0.2, lo)))
    fine = N.noise(55, 3, 0.6)
    scr = N.noise(40, 2, 0.5, vec=N.mapping(N.coord(), scale=(1, 12, 1), rot=(0.4, 0.2, 0.9)))
    base = N.mix(N.mapr(fine, 0.2, 0.8, 0.0, 0.45), col, N.mul(col, 0.55))
    rc = N.mix(N.mapr(N.noise(23, 3, 0.6), 0.3, 0.7), rust, rust2)
    c = N.mix(mask, base, rc)
    edge = N.math('MULTIPLY', N.pointy(0.50, 0.60), _c(p, 'edge', 0.8))
    c = N.mix(edge, c, _c(p, 'edge_color', (0.46, 0.45, 0.43)))
    metal = N.math('MAXIMUM', N.math('SUBTRACT', 1.0, N.math('MULTIPLY', mask, 0.92)), edge)
    r0 = _c(p, 'rough', 0.42)
    rough = N.mixf(mask, N.mapr(scr, 0.3, 0.75, r0 - 0.08, r0 + 0.2), 0.92)
    rough = N.mixf(edge, rough, 0.3)
    h = N.math('ADD', N.math('MULTIPLY', mask, 0.6), N.math('ADD', N.math('MULTIPLY', fine, 0.25), N.math('MULTIPLY', pits, -0.5)))
    return dict(color=c, rough=rough, metal=metal, height=h, bump=(_c(p, 'bump', 0.4), 0.003))


def k_cloth(N, p):
    col = _c(p, 'color', (0.16, 0.155, 0.15))
    dirt = _c(p, 'dirt', (0.12, 0.105, 0.09))
    big = N.noise(_c(p, 'scale', 5.0), 4, 0.6)
    stain = N.mapr(N.noise(2.6, 3, 0.55), 0.5, 0.75)
    c = N.mix(N.mapr(big, 0.3, 0.75, 0.0, 0.5), col, N.mul(col, 0.45))
    c = N.mix(N.math('MULTIPLY', stain, _c(p, 'stain_amt', 0.5)), c, N.mul(col, 0.3))
    # woven fibres: two anisotropic noises
    w1 = N.noise(160, 2, 0.5, vec=N.mapping(N.coord(), scale=(1, 1, 0.06)))
    w2 = N.noise(160, 2, 0.5, vec=N.mapping(N.coord(), scale=(0.06, 0.06, 1)))
    weave = N.math('ADD', N.math('MULTIPLY', w1, 0.5), N.math('MULTIPLY', w2, 0.5))
    c = N.mix(N.mapr(weave, 0.35, 0.65, 0.0, 0.3), c, N.mul(c, 0.6))
    hem = N.math('SUBTRACT', 1.0, N.height_z(_c(p, 'hem_lo', 0.0), _c(p, 'hem_hi', 0.5)))
    hemn = N.math('MULTIPLY', hem, N.mapr(N.noise(9, 3, 0.6), 0.25, 0.7))
    c = N.mix(N.math('MULTIPLY', hemn, _c(p, 'hem_amt', 0.8)), c, dirt)
    fold = N.noise(_c(p, 'fold', 14.0), 2, 0.4, vec=N.mapping(N.coord(), scale=(1, 1, 0.18)))
    # long vertical drip / wear streaks and soft fold shading baked into the colour
    streak = N.mapr(N.noise(21, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.07))), 0.5, 0.78)
    c = N.mix(N.math('MULTIPLY', streak, _c(p, 'streak', 0.45)), c, N.mul(col, 0.28))
    c = N.mix(N.mapr(fold, 0.25, 0.75, _c(p, 'fold_shade', 0.35), 0.0), c, N.mul(c, 0.5))
    edge = N.pointy(0.5, 0.62)
    c = N.mix(N.math('MULTIPLY', edge, _c(p, 'fray', 0.3)), c, N.mix(0.5, col, (0.45, 0.43, 0.4)))
    h = N.math('ADD', N.math('MULTIPLY', weave, 0.35), N.math('MULTIPLY', fold, _c(p, 'fold_amt', 0.65)))
    return dict(color=c, rough=_c(p, 'rough', 0.93), metal=0.0, height=h, bump=(_c(p, 'bump', 0.45), 0.004))


def k_ice(N, p):
    """glacier ice / icicles: pale surface, deep blue body, white frost on top and in cracks"""
    col = _c(p, 'color', (0.50, 0.68, 0.80))
    deep = _c(p, 'deep', (0.06, 0.17, 0.30))
    frost = _c(p, 'frost', (0.82, 0.88, 0.92))
    big = N.noise(_c(p, 'scale', 9.0), 4, 0.6)
    c = N.mix(N.mapr(big, 0.35, 0.7), deep, col)
    cr = N.voro(_c(p, 'crack', 20.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.08, 6))
    crack = N.mapr(cr, 0.0, 0.03, 1.0, 0.0)
    c = N.mix(N.math('MULTIPLY', crack, 0.7), c, frost)
    up = N.math('MULTIPLY', N.upfacing(0.2, 0.85), N.mapr(N.noise(14, 4, 0.7), 0.25, 0.65))
    fr = N.math('MAXIMUM', N.math('MULTIPLY', up, _c(p, 'frost_amt', 0.9)), N.math('MULTIPLY', N.pointy(0.5, 0.6), 0.6))
    c = N.mix(fr, c, frost)
    rough = N.mixf(fr, N.mapr(big, 0.3, 0.7, _c(p, 'rough', 0.12), _c(p, 'rough', 0.12) + 0.2), 0.75)
    h = N.math('ADD', N.math('MULTIPLY', N.mapr(cr, 0.0, 0.06), 0.6), N.math('MULTIPLY', N.noise(40, 3, 0.6), 0.4))
    return dict(color=c, rough=rough, metal=0.0, height=h, bump=(_c(p, 'bump', 0.35), 0.004))


def k_moss(N, p):
    """moss / wet vegetation / lichen clumps"""
    col = _c(p, 'color', (0.085, 0.12, 0.04))
    lite = _c(p, 'light', (0.19, 0.20, 0.07))
    dark = _c(p, 'dark', (0.025, 0.035, 0.018))
    big = N.noise(_c(p, 'scale', 10.0), 4, 0.65)
    clump = N.voro(_c(p, 'clump', 38.0), 'F1', vec=N.warp(0.05, 10))
    c = N.mix(N.mapr(big, 0.35, 0.7), dark, col)
    c = N.mix(N.mapr(clump, 0.05, 0.45, _c(p, 'tips', 0.6), 0.0), c, lite)
    fine = N.noise(90, 3, 0.7)
    c = N.mix(N.mapr(fine, 0.3, 0.8, 0.0, 0.35), c, N.mul(c, 0.5))
    hang = N.noise(26, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.1)))
    h = N.math('ADD', N.math('MULTIPLY', N.mapr(clump, 0.0, 0.5, 1.0, 0.0), 0.6), N.math('ADD', N.math('MULTIPLY', fine, 0.3), N.math('MULTIPLY', hang, 0.3)))
    return dict(color=c, rough=_c(p, 'rough', 0.95), metal=0.0, height=h, bump=(_c(p, 'bump', 0.8), 0.006))


def k_mud(N, p):
    """wet mud / silt with glossy patches and drying, cracked crust on top"""
    col = _c(p, 'color', (0.065, 0.05, 0.035))
    dry = _c(p, 'dry', (0.17, 0.14, 0.10))
    big = N.noise(_c(p, 'scale', 6.0), 4, 0.65)
    wet = N.mapr(big, 0.45, 0.62)
    cr = N.voro(24, 'DISTANCE_TO_EDGE', vec=N.warp(0.07, 7))
    up = N.math('MULTIPLY', N.upfacing(0.1, 0.8), N.mapr(N.noise(8, 3, 0.6), 0.3, 0.65))
    c = N.mix(N.math('MULTIPLY', up, _c(p, 'dry_amt', 0.75)), col, dry)
    c = N.mix(N.math('MULTIPLY', N.mapr(cr, 0.0, 0.035, 1.0, 0.0), up), c, N.mul(col, 0.5))
    drip = N.noise(18, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.12)))
    c = N.mix(N.mapr(drip, 0.5, 0.8, 0.0, 0.45), c, N.mul(col, 0.45))
    rough = N.mixf(N.math('MULTIPLY', wet, N.math('SUBTRACT', 1.0, up)), 0.9, _c(p, 'wet_rough', 0.22))
    h = N.math('ADD', N.math('MULTIPLY', big, 0.6), N.math('ADD', N.math('MULTIPLY', drip, 0.4), N.math('MULTIPLY', N.mapr(cr, 0.0, 0.06), 0.3)))
    return dict(color=c, rough=rough, metal=0.0, height=h, bump=(_c(p, 'bump', 0.7), 0.008))


def k_fur(N, p):
    """shaggy pelt: strands hanging along -Z, lighter tips, optional frost on top"""
    col = _c(p, 'color', (0.13, 0.10, 0.075))
    tipc = _c(p, 'tip', (0.30, 0.25, 0.19))
    st = N.noise(_c(p, 'scale', 55.0), 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.06)))
    st2 = N.noise(22, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.1)))
    big = N.noise(5, 3, 0.6)
    c = N.mix(N.mapr(st, 0.35, 0.7), N.mul(col, 0.4), col)
    c = N.mix(N.mapr(st2, 0.5, 0.8, 0.0, 0.7), c, tipc)
    c = N.mix(N.mapr(big, 0.4, 0.75, 0.0, 0.45), c, N.mul(c, 0.45))
    fr = _c(p, 'frost', 0.0)
    if fr > 0:
        up = N.math('MULTIPLY', N.upfacing(0.15, 0.8), N.mapr(N.noise(12, 4, 0.7), 0.25, 0.6))
        c = N.mix(N.math('MULTIPLY', up, fr), c, (0.80, 0.86, 0.90))
    h = N.math('ADD', N.math('MULTIPLY', st, 0.6), N.math('MULTIPLY', st2, 0.6))
    return dict(color=c, rough=_c(p, 'rough', 0.9), metal=0.0, height=h, bump=(_c(p, 'bump', 0.9), 0.007))


def k_leather(N, p):
    col = _c(p, 'color', (0.10, 0.065, 0.04))
    wv = N.warp(0.05, 7)
    cr = N.voro(_c(p, 'scale', 42.0), 'DISTANCE_TO_EDGE', vec=wv)
    crease = N.mapr(cr, 0.0, 0.06, 1.0, 0.0)
    big = N.noise(6, 4, 0.6)
    scuff = N.mapr(N.noise(18, 4, 0.7), 0.58, 0.8)
    c = N.mix(N.mapr(big, 0.3, 0.7, 0.0, 0.5), col, N.mul(col, 0.5))
    c = N.mix(N.math('MULTIPLY', crease, 0.6), c, N.mul(col, 0.25))
    c = N.mix(N.math('MULTIPLY', scuff, _c(p, 'scuff', 0.45)), c, _c(p, 'scuff_color', (0.24, 0.19, 0.15)))
    edge = N.pointy(0.5, 0.6)
    c = N.mix(N.math('MULTIPLY', edge, 0.4), c, _c(p, 'scuff_color', (0.24, 0.19, 0.15)))
    hem = N.math('SUBTRACT', 1.0, N.height_z(_c(p, 'hem_lo', -1.0), _c(p, 'hem_hi', -0.5)))
    c = N.mix(N.math('MULTIPLY', hem, N.mapr(N.noise(8, 3, 0.6), 0.25, 0.7)), c, _c(p, 'dirt', (0.10, 0.085, 0.07)))
    rough = N.mixf(scuff, N.mapr(big, 0.3, 0.7, _c(p, 'rough', 0.5), _c(p, 'rough', 0.5) + 0.2), 0.85)
    h = N.math('ADD', N.math('MULTIPLY', N.mapr(cr, 0.0, 0.08), 0.7), N.math('MULTIPLY', N.noise(60, 2, 0.5), 0.3))
    return dict(color=c, rough=rough, metal=0.0, height=h, bump=(_c(p, 'bump', 0.4), 0.004))


def k_skin(N, p):
    col = _c(p, 'color', (0.34, 0.345, 0.32))
    blotch = _c(p, 'blotch', (0.20, 0.17, 0.19))
    vein = _c(p, 'vein', (0.10, 0.09, 0.15))
    big = N.noise(_c(p, 'scale', 6.5), 4, 0.62)
    c = N.mix(N.mapr(big, 0.4, 0.68), col, blotch)
    mot = N.noise(38, 3, 0.6)
    c = N.mix(N.mapr(mot, 0.35, 0.75, 0.0, 0.35), c, N.mul(c, 0.6))
    vd = N.voro(_c(p, 'vein_scale', 11.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.12, 5))
    vm = N.math('MULTIPLY', N.mapr(vd, 0.0, 0.03, 1.0, 0.0), N.mapr(N.noise(3.5, 2, 0.5), 0.42, 0.62))
    c = N.mix(N.math('MULTIPLY', vm, _c(p, 'vein_amt', 0.7)), c, vein)
    sore = N.mapr(N.noise(13, 4, 0.7, typ='RIDGED_MULTIFRACTAL'), _c(p, 'sore_lo', 0.72), 0.9)
    c = N.mix(N.math('MULTIPLY', sore, _c(p, 'sore_amt', 0.6)), c, _c(p, 'sore', (0.16, 0.035, 0.03)))
    hd = _c(p, 'hands_dark', 0.0)
    if hd > 0:   # necrotic extremities: darker towards the hands (|x| large in the T-pose) and the feet
        x, _, _ = N.sep(N.coord())
        m = N.mapr(N.math('ABSOLUTE', x), _c(p, 'hands_x0', 0.42), _c(p, 'hands_x1', 0.82))
        c = N.mix(N.math('MULTIPLY', m, hd), c, _c(p, 'dark', (0.06, 0.05, 0.055)))
    fd = _c(p, 'feet_dirt', 0.0)
    if fd > 0:
        m = N.math('SUBTRACT', 1.0, N.height_z(0.0, _c(p, 'feet_hi', 0.5)))
        c = N.mix(N.math('MULTIPLY', m, fd), c, _c(p, 'dirt', (0.075, 0.062, 0.05)))
    wr = N.noise(48, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 3.0)), typ='RIDGED_MULTIFRACTAL')
    h = N.math('ADD', N.math('MULTIPLY', wr, 0.3), N.math('ADD', N.math('MULTIPLY', mot, 0.3), N.math('MULTIPLY', vm, 0.5)))
    rough = N.mapr(big, 0.3, 0.75, _c(p, 'rough', 0.5), _c(p, 'rough', 0.5) + 0.22)
    return dict(color=c, rough=rough, metal=0.0, height=h, bump=(_c(p, 'bump', 0.45), 0.004))


def k_rock(N, p):
    col = _c(p, 'color', (0.04, 0.037, 0.036))
    ash = _c(p, 'ash', (0.19, 0.18, 0.17))
    sc = _c(p, 'scale', 15.0)
    wv = N.warp(0.06, 6)
    cell = N.voro(sc, 'F1', vec=wv, out='Color')
    cellv = N.new('ShaderNodeSeparateColor'); N.set(cellv.inputs[0], cell)
    edge = N.voro(sc, 'DISTANCE_TO_EDGE', vec=wv)
    crack = N.mapr(edge, 0.0, _c(p, 'crack_w', 0.045), 1.0, 0.0)
    fine = N.noise(48, 4, 0.65)
    c = N.mix(N.mapr(cellv.outputs[0], 0.0, 1.0, 0.0, 0.6), col, N.mul(col, 2.3))
    c = N.mix(N.mapr(fine, 0.25, 0.8, 0.0, 0.5), c, N.mul(c, 0.45))
    c = N.mix(N.math('MULTIPLY', crack, 0.85), c, N.mul(col, 0.2))
    up = N.math('MULTIPLY', N.upfacing(0.3, 0.9), N.mapr(N.noise(11, 4, 0.7), 0.3, 0.7))
    c = N.mix(N.math('MULTIPLY', up, _c(p, 'ash_amt', 0.7)), c, ash)
    pe = N.pointy(0.5, 0.62)
    c = N.mix(N.math('MULTIPLY', pe, 0.35), c, N.mix(0.5, ash, col))
    heat = _c(p, 'heat', None)
    if heat is not None:   # warm tint in the cracks (reads as heat even without emission)
        c = N.mix(N.math('MULTIPLY', crack, _c(p, 'heat_amt', 0.5)), c, heat)
    rough = N.mapr(fine, 0.2, 0.8, _c(p, 'rough', 0.78), 0.96)
    h = N.math('ADD', N.math('MULTIPLY', N.mapr(edge, 0.0, 0.12), 0.8),
               N.math('ADD', N.math('MULTIPLY', fine, 0.35), N.math('MULTIPLY', cellv.outputs[1], 0.3)))
    return dict(color=c, rough=rough, metal=0.0, height=h, bump=(_c(p, 'bump', 0.7), 0.006))


def k_glass(N, p):
    """dull black volcanic glass"""
    col = _c(p, 'color', (0.012, 0.012, 0.016))
    smudge = N.noise(9, 4, 0.7)
    flow = N.noise(22, 2, 0.5, vec=N.mapping(N.coord(), scale=(4, 0.3, 4)))
    c = N.mix(N.mapr(flow, 0.45, 0.8, 0.0, 0.6), col, _c(p, 'band', (0.035, 0.035, 0.05)))
    edge = N.pointy(0.5, 0.6)
    c = N.mix(N.math('MULTIPLY', edge, 0.6), c, (0.12, 0.13, 0.16))
    rough = N.mapr(smudge, 0.3, 0.75, _c(p, 'rough', 0.16), _c(p, 'rough', 0.16) + 0.3)
    chip = N.mapr(N.voro(30, 'F1'), 0.0, 0.5)
    h = N.math('ADD', N.math('MULTIPLY', flow, 0.5), N.math('MULTIPLY', chip, 0.4))
    return dict(color=c, rough=rough, metal=_c(p, 'metal', 0.0), height=h, bump=(_c(p, 'bump', 0.2), 0.003))


def k_wax(N, p):
    col = _c(p, 'color', (0.62, 0.56, 0.42))
    big = N.noise(9, 3, 0.5)
    c = N.mix(N.mapr(big, 0.3, 0.7, 0.0, 0.5), col, N.mul(col, 0.6))
    soot = N.math('MULTIPLY', N.upfacing(0.5, 0.95), _c(p, 'soot', 0.5))
    c = N.mix(soot, c, (0.05, 0.045, 0.04))
    drip = N.noise(30, 2, 0.5, vec=N.mapping(N.coord(), scale=(1, 1, 0.15)))
    return dict(color=c, rough=N.mapr(big, 0.3, 0.7, 0.35, 0.55), metal=0.0, height=drip, bump=(0.3, 0.004))


def k_wood(N, p):
    col = _c(p, 'color', (0.12, 0.08, 0.05))
    axis = _c(p, 'axis', (0, 0, 1))
    g = N.noise(60, 3, 0.6, vec=N.align(N.coord(), axis, 0.05))
    big = N.noise(6, 3, 0.6)
    c = N.mix(N.mapr(g, 0.3, 0.7), N.mul(col, 0.5), col)
    c = N.mix(N.mapr(big, 0.4, 0.8, 0.0, 0.5), c, N.mul(c, 0.5))
    edge = N.pointy(0.5, 0.6)
    c = N.mix(N.math('MULTIPLY', edge, 0.35), c, (0.26, 0.2, 0.14))
    return dict(color=c, rough=N.mapr(g, 0.3, 0.7, 0.62, 0.85), metal=0.0, height=g, bump=(_c(p, 'bump', 0.5), 0.003))


def k_emis(N, p):
    """glowing accent. emit=(r,g,b), strength; base colour is a dimmed, mottled version"""
    e = _c(p, 'emit', (1.0, 0.5, 0.1))
    n = N.noise(_c(p, 'scale', 18.0), 3, 0.6)
    base = tuple(min(1.0, x * _c(p, 'base_mul', 0.55)) for x in e)
    c = N.mix(N.mapr(n, 0.3, 0.75, 0.0, _c(p, 'mottle', 0.5)), base, tuple(x * 0.25 for x in base))
    return dict(color=c, rough=_c(p, 'rough', 0.6), metal=0.0, height=n, bump=(0.2, 0.003),
                emit=(e, _c(p, 'strength', 4.0)))


def k_spectral(N, p):
    col = _c(p, 'color', (0.42, 0.55, 0.58))
    dark = _c(p, 'dark', (0.07, 0.11, 0.14))
    st = N.noise(14, 4, 0.65, vec=N.mapping(N.coord(), scale=(1, 1, 0.12)))
    big = N.noise(3.5, 3, 0.55)
    c = N.mix(N.mapr(st, 0.3, 0.72), dark, col)
    c = N.mix(N.mapr(big, 0.35, 0.75, 0.0, 0.6), c, dark)
    low = N.math('SUBTRACT', 1.0, N.height_z(_c(p, 'fade_lo', 0.2), _c(p, 'fade_hi', 1.0)))
    c = N.mix(N.math('MULTIPLY', low, 0.55), c, _c(p, 'wisp', (0.62, 0.80, 0.82)))
    return dict(color=c, rough=0.9, metal=0.0, height=st, bump=(0.5, 0.005))


def k_flat(N, p):
    return dict(color=_c(p, 'color', (0.5, 0.5, 0.5)), rough=_c(p, 'rough', 0.7), metal=_c(p, 'metal', 0.0),
                height=None, bump=(0, 0))


KINDS = dict(bone=k_bone, metal=k_metal, cloth=k_cloth, leather=k_leather, skin=k_skin, rock=k_rock,
             glass=k_glass, wax=k_wax, wood=k_wood, emis=k_emis, spectral=k_spectral, flat=k_flat,
             ice=k_ice, moss=k_moss, mud=k_mud, fur=k_fur)

# ready-made parameter sets (use as C.look(name, kind, slot, **PRESET['brass']))
PRESET = dict(
    brass=dict(color=(0.40, 0.28, 0.09), rust=(0.07, 0.17, 0.13), rust2=(0.04, 0.08, 0.06), amount=0.45,
               edge_color=(0.78, 0.62, 0.30), rough=0.36),
    black_plate=dict(color=(0.035, 0.036, 0.042), rust=(0.10, 0.075, 0.06), rust2=(0.03, 0.025, 0.022), amount=0.4,
                     edge_color=(0.30, 0.31, 0.34), rough=0.4, edge=0.9),
    rusty_iron=dict(color=(0.19, 0.18, 0.17), amount=0.85),
    worn_steel=dict(color=(0.27, 0.26, 0.25), amount=0.5, edge=1.0),
    gold_tarnish=dict(color=(0.46, 0.33, 0.10), rust=(0.12, 0.09, 0.05), rust2=(0.05, 0.04, 0.03), amount=0.5,
                      edge_color=(0.85, 0.68, 0.30), rough=0.32),
)


def make_look_material(name, kind, params):
    mat = bpy.data.materials.new("look_" + name)
    N = NB(mat)
    r = KINDS[kind](N, params)
    P = N.new('ShaderNodeBsdfPrincipled'); P.name = 'P'
    N.set(P.inputs['Base Color'], r['color'])
    N.set(P.inputs['Roughness'], r['rough'])
    N.set(P.inputs['Metallic'], r['metal'])
    if r.get('height') is not None and r['bump'][0] > 0:
        b = N.new('ShaderNodeBump')
        b.inputs['Strength'].default_value = r['bump'][0]
        b.inputs['Distance'].default_value = r['bump'][1]
        N.set(b.inputs['Height'], r['height'])
        N.set(P.inputs['Normal'], b.outputs[0])
    em = r.get('emit')
    if em:
        N.set(P.inputs['Emission Color'], em[0])
        P.inputs['Emission Strength'].default_value = em[1]
        mat['mon_emit'] = [float(em[0][0]), float(em[0][1]), float(em[0][2]), float(em[1])]
    out = N.new('ShaderNodeOutputMaterial'); out.name = 'OUT'
    N.nt.links.new(P.outputs[0], out.inputs[0])
    ec = N.new('ShaderNodeEmission'); ec.name = 'E_COL'
    N.set(ec.inputs[0], r['color'])
    comb = N.new('ShaderNodeCombineColor')
    comb.inputs[0].default_value = 1.0
    N.set(comb.inputs[1], r['rough']); N.set(comb.inputs[2], r['metal'])
    eo = N.new('ShaderNodeEmission'); eo.name = 'E_ORM'
    N.set(eo.inputs[0], comb.outputs[0])
    img = N.new('ShaderNodeTexImage'); img.name = 'BAKE'
    return mat


# ------------------------------------------------------------------------------------------------ unwrap / bake
def _select(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def unwrap(objs, margin=0.0035):
    for o in objs:
        if not o.data.uv_layers:
            o.data.uv_layers.new(name="UVMap")
    _select(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=margin, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')


def _img(name, size, fill, noncolor):
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    im = bpy.data.images.new(name, size, size, alpha=False)
    if noncolor:
        im.colorspace_settings.name = 'Non-Color'
    a = np.empty((size * size, 4), dtype=np.float32)
    a[:] = np.array(tuple(fill) + (1.0,), dtype=np.float32)
    im.pixels.foreach_set(a.ravel())
    return im


def _np(im):
    w, h = im.size
    a = np.empty(w * h * 4, dtype=np.float32)
    im.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def _blur(a, it=2):
    for _ in range(it):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.0
    return a


def _save(im, path):
    im.filepath_raw = path
    im.file_format = 'PNG'
    im.save()


def bake_and_assign(C, objs, size=1024, ao_strength=0.72):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    # fixed thread count: unbounded Cycles bakes in several Blender processes at once hung the laptop
    import os as _os
    sc.render.threads_mode = 'FIXED'
    sc.render.threads = max(1, min(32, int(_os.environ.get('ASHEN_BLENDER_THREADS', '12'))))
    try:
        sc.cycles.use_denoising = False
    except Exception:
        pass
    if sc.world is None:
        sc.world = bpy.data.worlds.new("bakeworld")
    try:
        sc.world.light_settings.distance = 0.22
    except Exception:
        pass
    mats = list(C.look_mats.values())
    imgs = dict(col=_img("bk_col", size, (0.2, 0.2, 0.2), False),
                orm=_img("bk_orm", size, (1.0, 0.8, 0.0), True),
                nrm=_img("bk_nrm", size, (0.5, 0.5, 1.0), True),
                ao=_img("bk_ao", size, (1.0, 1.0, 1.0), True))
    bk = sc.render.bake
    bk.use_selected_to_active = False
    bk.use_clear = False
    bk.margin = 10
    bk.target = 'IMAGE_TEXTURES'

    def run(key, shader_node, typ, samples, **kw):
        for m in mats:
            nt = m.node_tree
            n = nt.nodes['BAKE']
            n.image = imgs[key]
            for x in nt.nodes:
                x.select = False
            n.select = True
            nt.nodes.active = n
            nt.links.new(nt.nodes[shader_node].outputs[0], nt.nodes['OUT'].inputs[0])
        sc.cycles.samples = samples
        _select(objs)
        bpy.ops.object.bake(type=typ, margin=10, use_clear=False, **kw)

    import time
    t = time.time()
    run('col', 'E_COL', 'EMIT', 4)
    run('orm', 'E_ORM', 'EMIT', 4)
    run('nrm', 'P', 'NORMAL', 4, normal_space='TANGENT')
    run('ao', 'P', 'AO', 40)
    M.log("bake %.1fs" % (time.time() - t))

    col = _np(imgs['col'])[:, :, :3]
    orm = _np(imgs['orm'])[:, :, :3]
    nrm = _np(imgs['nrm'])[:, :, :3]
    ao = _blur(_np(imgs['ao'])[:, :, 0], 2)
    k = (1.0 - ao_strength) + ao_strength * np.clip(ao, 0, 1) ** 1.3
    # occluded areas go slightly warm-dark (grime) rather than pure black
    tint = np.stack([k ** 0.92, k, k ** 1.08], axis=2)
    bc = np.clip(col * tint, 0, 1)
    orm[:, :, 0] = np.clip(ao, 0, 1)
    orm[:, :, 1] = np.clip(orm[:, :, 1] + (1 - k) * 0.25, 0.04, 1)   # crevices rougher

    def final(name, arr, noncolor):
        im = _img(name, size, (0, 0, 0), noncolor)
        a = np.ones((size, size, 4), dtype=np.float32)
        a[:, :, :3] = arr
        im.pixels.foreach_set(a.ravel())
        p = os.path.join(M.TEXDIR, name + ".png")
        _save(im, p)
        im.pack()
        return im

    T = dict(bc=final(C.id + "_bc", bc, False), orm=final(C.id + "_orm", orm, True), nrm=final(C.id + "_nrm", nrm, True))
    for im in imgs.values():
        bpy.data.images.remove(im)
    _assign_slots(C, objs, T)


def _slot_material(slot, tex, emit):
    mat = bpy.data.materials.new(slot)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    P = nt.nodes.new('ShaderNodeBsdfPrincipled')
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(P.outputs[0], out.inputs[0])
    if tex:
        tb = nt.nodes.new('ShaderNodeTexImage'); tb.image = tex['bc']
        nt.links.new(tb.outputs[0], P.inputs['Base Color'])
        to = nt.nodes.new('ShaderNodeTexImage'); to.image = tex['orm']
        sp = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(to.outputs[0], sp.inputs[0])
        nt.links.new(sp.outputs[1], P.inputs['Roughness'])
        nt.links.new(sp.outputs[2], P.inputs['Metallic'])
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = tex['nrm']
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(tn.outputs[0], nm.inputs['Color'])
        nt.links.new(nm.outputs[0], P.inputs['Normal'])
    if emit:
        P.inputs['Emission Color'].default_value = (emit[0], emit[1], emit[2], 1.0)
        P.inputs['Emission Strength'].default_value = emit[3]
    pre = slot.split('_')[0]
    mat.use_backface_culling = pre not in ('cloth', 'spectral', 'leather')
    return mat


def _assign_slots(C, objs, tex):
    slot_mats = {}
    look_of_mat = {m.name: l for l, m in C.look_mats.items()}
    for ob in objs:
        me = ob.data
        looks = [look_of_mat[m.name] for m in me.materials]
        slots = []
        for l in looks:
            s = C.looks[l]['slot']
            if s not in slots:
                slots.append(s)
        idx = np.empty(len(me.polygons), dtype=np.int32)
        me.polygons.foreach_get('material_index', idx)
        remap = np.array([slots.index(C.looks[l]['slot']) for l in looks], dtype=np.int32)
        new_idx = remap[idx]
        for s in slots:
            if s not in slot_mats:
                emit = None
                for l, d in C.looks.items():
                    if d['slot'] == s and l in C.look_mats and 'mon_emit' in C.look_mats[l].keys():
                        emit = list(C.look_mats[l]['mon_emit'])
                        break
                if s.startswith('emis_') and emit is None:
                    emit = [1.0, 0.6, 0.2, 3.0]
                slot_mats[s] = _slot_material(s, tex, emit)
        me.materials.clear()
        for s in slots:
            me.materials.append(slot_mats[s])
        me.polygons.foreach_set('material_index', new_idx)
        me.update()
    for m in C.look_mats.values():
        if m.users == 0:
            bpy.data.materials.remove(m)
    C.slot_mats = slot_mats
    ok = ('skin_', 'cloth_', 'leather_', 'metal_', 'bone_', 'rock_', 'emis_', 'spectral_')
    for s in slot_mats:
        if not s.startswith(ok):
            M.log("WARNING: bad material prefix", s)
    if len(slot_mats) > 5:
        M.log("WARNING: more than 5 materials:", list(slot_mats))
    M.log("materials:", list(slot_mats))


def assign_preview_slots(C, objs):
    """no-bake mode: keep the procedural look materials (for quick EEVEE look-dev only)"""
    C.slot_mats = {}
