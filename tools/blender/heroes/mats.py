"""Source materials (Cycles, baked to atlases by finalize.py) and final glTF PBR materials -- realism pass (v2).

What changed against mats_v1.py (kept for reference):
  * every material composites a SCANNED CC0 set (chartex.py: albedo detail, height integrated from the scan normal map,
    roughness), box-projected in object space at a texel scale the atlas can actually hold;
  * heights are in METRES and go through one Bump node at distance 1 (v1: tiny unitless heights * strength 0.2-0.45 *
    distance 0.004 -> flat normal maps). Relief is exaggerated several-fold on purpose (G.RELIEF);
  * a mid-frequency band was added: garment folds / joint wrinkles (G.wrinkles), hammer dents, creases, cracks;
  * finalize bakes a MASK atlas first (R = convex edge from a bevel shader, G = tight cavity AO, B = wide AO) and a per
    vertex 'hem' attribute (distance to the nearest open / sharp edge); the materials read both: edge wear to bright
    metal / scuffed leather / frayed cloth, dirt and rust in cavities, hems, stitches, rolled plate rims, dust on top;
  * roughness and albedo ranges are much wider; ash is a broken-up overlay instead of a grey gradient.
Function names and signatures are those of v1 (plus optional keywords), so the character scripts run unchanged.
"""
import bpy, math, os
import numpy as np
from rigio import *
import chartex as CT

SRCM = {}       # key -> source material
SRCINFO = {}    # key -> dict(color, rough, metal) representative values for fast previews
RELIEF = 1.0    # global multiplier of every baked height (1 = the tuned, deliberately strong, default)
ASH = (0.16, 0.15, 0.14)
DUST = (0.19, 0.165, 0.135)
_IMG = {}


def scan_images(alias):
    """(imgA: RGB albedo detail * 0.5, imgB: R = 0.5 + h/8 (unit-std height), G = roughness, B = aux0), meta"""
    if alias not in _IMG or _IMG[alias][0].name not in bpy.data.images:
        d = CT.load(alias)
        res = d['h'].shape[0]

        def mk(name, rgb):
            img = bpy.data.images.new(name, res, res, alpha=False, float_buffer=True)
            img.colorspace_settings.name = 'Non-Color'
            px = np.ones((res, res, 4), dtype=np.float32)
            px[..., :3] = rgb
            img.pixels.foreach_set(px.ravel())
            img.update()
            return img
        a = mk("ctx_%s_a" % alias, d['det'] * 0.5)
        b = mk("ctx_%s_b" % alias, np.stack([0.5 + np.clip(d['h'], -4, 4) / 8.0, d['rough'], d['aux'][..., 0]], axis=2))
        _IMG[alias] = (a, b, d['meta'])
    return _IMG[alias]


# ============================================================================================ node helper
class Layer:
    """sockets of one sampled scan: det (colour multiplier, mean 1), lum (its luminance), h (metres, zero mean),
    hn (unit-std height), rough (0..1), rdev (roughness deviation in std units), aux (set specific)"""
    pass


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
        self._xyz = None
        self._masks = None
        self._hem = None

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

    def madd(self, a, b, c):
        return self.math('MULTIPLY_ADD', a, b, c)

    def abs(self, a):
        return self.math('ABSOLUTE', a)

    def pow(self, a, b):
        return self.math('POWER', self.math('MAXIMUM', a, 0.0), b)

    def max(self, a, b):
        return self.math('MAXIMUM', a, b)

    def min(self, a, b):
        return self.math('MINIMUM', a, b)

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

    def band(self, x, c, w, soft=None):
        """1 inside |x - c| < w/2 (soft edges)"""
        s = soft if soft is not None else w * 0.35
        d = self.abs(self.sub(x, c))
        return self.sstep(d, w * 0.5 + s * 0.5, w * 0.5 - s * 0.5)

    def gauss(self, x, c, w):
        """exp(-((x-c)/w)^2)"""
        d = self.math('DIVIDE', self.sub(x, c), w)
        return self.math('EXPONENT', self.mul(self.mul(d, d), -1.0))

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

    def tint(self, col, det, k=1.0):
        """col * lerp(1, det, k)   (det = detail multiplier around 1)"""
        return self.mix(k, col, det, 'MULTIPLY')

    def scale_c(self, col, f):
        """colour * scalar socket / value"""
        n = self.node('ShaderNodeVectorMath', operation='SCALE')
        self.put(n.inputs[0], col)
        self.put(n.inputs['Scale'], f)
        return n.outputs[0]

    def dgain(self, det, k):
        """amplify a detail multiplier: 1 + (lum(det) - 1) * k, never below 0.05"""
        return self.max(self.madd(self.lum(det), k, 1.0 - k), 0.05)

    def lum(self, col):
        n = self.node('ShaderNodeRGBToBW')
        self.put(n.inputs[0], col)
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

    def xyz(self):
        if self._xyz is None:
            self._xyz = self.sep(self.pos)
        return self._xyz

    def luv(self):
        """(u, v) of lofted parts (vertex attribute 'luv')"""
        if self._luv is None:
            a = self.node('ShaderNodeAttribute', attribute_name='luv')
            x, y, z = self.sep(a.outputs['Vector'])
            self._luv = (x, y)
        return self._luv

    def hem(self):
        """distance (metres) to the nearest open border / sharp edge of the part (vertex attribute 'hem')"""
        if self._hem is None:
            a = self.node('ShaderNodeAttribute', attribute_name='hem')
            self._hem = a.outputs['Fac']
        return self._hem

    def masks(self):
        """(edge, cavity, open) from the MASK atlas baked by finalize: convex-edge wear, tight cavity, wide AO"""
        if self._masks is None:
            t = self.node('ShaderNodeTexImage', name='MASKS')
            t.interpolation = 'Linear'
            s = self.node('ShaderNodeSeparateColor')
            self.L.new(t.outputs['Color'], s.inputs[0])
            self._masks = (s.outputs[0], s.outputs[1], s.outputs[2])
        return self._masks

    def up(self):
        """0..1 how much the surface faces up (for settled dust)"""
        x, y, z = self.sep(self.geom.outputs['Normal'])
        return self.sstep(z, 0.25, 0.9)

    def edge(self, radius=0.008, gain=3.0):
        """kept for v1 callers: convex edge mask (now read from the baked MASK atlas)"""
        return self.masks()[0]

    def scan(self, alias, tile=None, slope=0.2, off=(0.0, 0.0, 0.0), blend=0.3, vec=None, flat=False, gain=None):
        """sample a chartex set; box projection in object space (or flat UV-like mapping of `vec` when flat=True).
        slope = wanted rms slope of this layer in the baked normal map (0.1 subtle, 0.2 clear, 0.4 strong); the scan
        height is scaled to reach it (gain = explicit multiplier of the natural scan relief instead)."""
        a, b, meta = scan_images(alias)
        if gain is None:
            gain = slope / max(meta['slope_rms'], 1e-4)
        s = CT.SETS[alias]
        tile = tile or s['tile']
        if vec is None:
            vec = self.mapping(scale=(1.0 / tile,) * 3, loc=off)
        out = Layer()
        ta = self.node('ShaderNodeTexImage')
        tb = self.node('ShaderNodeTexImage')
        for t, img in ((ta, a), (tb, b)):
            t.image = img
            t.interpolation = 'Linear'
            t.extension = 'REPEAT'
            if not flat:
                t.projection = 'BOX'
                t.projection_blend = blend
            self.L.new(vec, t.inputs['Vector'])
        out.det = self.scale_c(ta.outputs['Color'], 2.0)
        out.raw = ta.outputs['Color']
        sp = self.node('ShaderNodeSeparateColor')
        self.L.new(tb.outputs['Color'], sp.inputs[0])
        out.hn = self.madd(sp.outputs[0], 8.0, -4.0)
        out.h = self.mul(out.hn, meta['h_std'] * tile * gain * RELIEF)
        out.rough = sp.outputs[1]
        out.rdev = self.math('DIVIDE', self.sub(sp.outputs[1], meta['rough_mean']), max(meta['rough_std'], 0.02))
        out.aux = sp.outputs[2]
        out.meta = meta
        out.tile = tile
        return out

    # ------------------------------------------------------------------------------------- shared look layers
    def wrinkles(self, folds=0.5, tight=False, amp=1.0):
        """Mid-frequency garment relief in metres (the band v1 lacked): hanging folds for loose cloth, ring wrinkles
        round the (T-pose) arms, compression ripples behind knees / at elbows / ankles for fitted parts.
        Returns (height, crease mask 0..1 for darkening the valleys)."""
        x, y, z = self.xyz()
        ax = self.abs(x)
        arm = self.mul(self.mul(self.sstep(ax, 0.20, 0.27), self.sstep(ax, 0.98, 0.88)), self.sstep(z, 1.24, 1.33))
        body = self.inv(arm)
        k = folds * amp * RELIEF

        def ridged(n, p=2.0):
            r = self.inv(self.abs(self.madd(n, 2.0, -1.0)))
            return self.pow(r, p)
        # hanging folds: long in Z, 5-9 cm apart, sharper crests; stronger towards the hem
        v1 = ridged(self.noise(1.0, 2.0, 0.5, 0.7, vec=self.mapping(scale=(13.0, 13.0, 1.5))), 2.2)
        v2 = ridged(self.noise(1.0, 2.0, 0.5, 0.5, vec=self.mapping(scale=(31.0, 31.0, 4.0), loc=(3.1, 1.7, 0.4))), 1.6)
        kz = self.lin(z, 0.85, 1.25, 1.0, 0.5)
        if tight:
            hv = self.mul(self.add(self.mul(v1, 0.0022), self.mul(v2, 0.0012)), kz)
        else:
            hv = self.mul(self.add(self.mul(v1, 0.0075), self.mul(v2, 0.0022)), kz)
        h = self.mul(self.mul(hv, body), k)
        # arms: rings round the arm, bunching at the elbow and the armpit
        a1 = ridged(self.noise(1.0, 2.0, 0.5, 0.8, vec=self.mapping(scale=(34.0, 4.0, 4.0))), 1.8)
        ka = self.add(0.35, self.add(self.mul(self.gauss(ax, 0.47, 0.075), 1.0), self.mul(self.gauss(ax, 0.25, 0.05), 0.6)))
        h = self.add(h, self.mul(self.mul(self.mul(a1, ka), arm), 0.0042 * k))
        cre = self.mul(self.inv(self.add(self.mul(v1, body), self.mul(a1, arm))), 1.0)
        if tight:
            # knees (back of the knee stronger), ankles, crotch-level bunching
            kn = ridged(self.noise(1.0, 2.0, 0.5, 0.8, vec=self.mapping(scale=(5.0, 5.0, 42.0))), 1.8)
            gk = self.add(self.mul(self.gauss(z, 0.50, 0.075), self.lin(y, -0.06, 0.08, 0.55, 1.0)),
                          self.add(self.mul(self.gauss(z, 0.14, 0.05), 0.9), self.mul(self.gauss(z, 0.93, 0.05), 0.5)))
            h = self.add(h, self.mul(self.mul(self.mul(kn, gk), body), 0.0040 * k))
            # small isotropic crumple everywhere
            cr = ridged(self.noise(26.0, 2.0, 0.55, 1.2), 2.5)
            h = self.add(h, self.mul(cr, 0.0010 * k))
        return h, self.clamp(cre)

    def grime(self, col, rgh, amount=1.0, dirt=(0.035, 0.028, 0.022), rough_to=0.92):
        """tight-cavity dirt: darker, rougher, a little matte where surfaces meet"""
        e, cav, opn = self.masks()
        n = self.noise(9.0, 4, 0.6, 0.5)
        c = self.clamp(self.mul(self.mul(self.sstep(cav, 0.12, 0.75), self.lin(n, 0.2, 0.8, 0.55, 1.0)), amount))
        col = self.mix(self.mul(c, 0.8), col, dirt)
        rgh = self.mixf(self.mul(c, 0.7), rgh, rough_to)
        return col, rgh, c

    def dust(self, col, rgh, ash=0.5, ash_h=0.5, micro=None, met=None, colour=None):
        """ash / dust: settles on upward faces everywhere and builds up towards the ground, broken up by noise and
        held in the pits of the micro relief (micro = unit-std height socket) instead of a flat grey film."""
        if ash <= 0:
            return col, rgh, met, None
        x, y, z = self.xyz()
        n1 = self.noise(2.6, 4, 0.6, 0.4)
        n2 = self.noise(17.0, 3, 0.6)
        low = self.mul(self.sstep(z, ash_h, 0.0), self.lin(n1, 0.25, 0.75, 0.35, 1.0))
        top = self.mul(self.up(), self.lin(n1, 0.3, 0.8, 0.25, 0.75))
        a = self.max(self.mul(low, 0.85), self.mul(top, 0.6))
        a = self.mul(a, self.lin(n2, 0.3, 0.75, 0.45, 1.0))
        if micro is not None:
            a = self.mul(a, self.lin(micro, -1.2, 1.0, 1.25, 0.45))
        a = self.clamp(self.mul(a, min(1.0, ash * 1.15)))
        dc = self.mix(self.lin(n1, 0.3, 0.7), colour or ASH, DUST)
        col = self.mix(self.mul(a, 0.82), col, dc)
        rgh = self.mixf(self.mul(a, 0.85), rgh, 0.95)
        if met is not None:
            met = self.mixf(self.mul(a, 0.75), met, 0.0)
        return col, rgh, met, a

    def stitches(self, hem, d0=0.010, width=0.0028, pitch=0.013):
        """dashes of a stitch line running parallel to the part border at distance d0 (3D cells -> works for any
        direction). Returns mask 0..1."""
        line = self.band(hem, d0, width)
        v = self.vor(1.0 / pitch, 'F1', rnd=0.55)
        dash = self.sstep(v, 0.50, 0.36)
        return self.mul(line, dash)

    def finish(self, color, rough, metal, height=None, bump=1.0, bump_dist=1.0, normal=None):
        bs = self.node('ShaderNodeBsdfPrincipled', name='BSDF')
        self.put(bs.inputs['Base Color'], color)
        self.put(bs.inputs['Roughness'], rough)
        self.put(bs.inputs['Metallic'], metal)
        if height is not None:
            bp = self.node('ShaderNodeBump')
            bp.inputs['Strength'].default_value = min(1.0, bump)
            bp.inputs['Distance'].default_value = bump_dist
            self.put(bp.inputs['Height'], height)
            if normal is not None:
                self.L.new(normal, bp.inputs['Normal'])
            self.L.new(bp.outputs[0], bs.inputs['Normal'])
        elif normal is not None:
            self.L.new(normal, bs.inputs['Normal'])
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


def _sat(c, s):
    l = _lum(c)
    return tuple(min(1.0, max(0.0, l + (v - l) * s)) for v in c[:3])


def _has(key, *words):
    k = key.lower()
    return any(w in k for w in words)


def plain_material():
    """trivial diffuse material with a bake TARGET node (used for the AO bakes)"""
    m = bpy.data.materials.get("src__plain")
    if m is not None:
        return m
    m = bpy.data.materials.new("src__plain")
    m.use_nodes = True
    g = G(m)
    bs = g.node('ShaderNodeBsdfDiffuse')
    out = g.node('ShaderNodeOutputMaterial', name='OUT')
    g.L.new(bs.outputs[0], out.inputs[0])
    tg = g.node('ShaderNodeTexImage', name='TARGET')
    g.N.active = tg
    return m


def mask_material():
    """emits the convex/concave edge signal (bevelled normal vs shading normal) for the MASK bake of finalize"""
    m = bpy.data.materials.get("src__mask")
    if m is not None:
        return m
    m = bpy.data.materials.new("src__mask")
    m.use_nodes = True
    g = G(m)
    b = g.node('ShaderNodeBevel', samples=4)
    b.inputs['Radius'].default_value = 0.0065
    d = g.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
    g.L.new(b.outputs[0], d.inputs[0])
    g.L.new(g.geom.outputs['Normal'], d.inputs[1])
    e = g.sstep(g.inv(d.outputs['Value']), 0.015, 0.22)
    b2 = g.node('ShaderNodeBevel', samples=4)
    b2.inputs['Radius'].default_value = 0.018
    d2 = g.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
    g.L.new(b2.outputs[0], d2.inputs[0])
    g.L.new(g.geom.outputs['Normal'], d2.inputs[1])
    e2 = g.sstep(g.inv(d2.outputs['Value']), 0.02, 0.30)
    c = g.comb(e, e2, 0.0)
    em = g.node('ShaderNodeEmission', name='EMIT')
    g.L.new(c, em.inputs['Color'])
    out = g.node('ShaderNodeOutputMaterial', name='OUT')
    g.L.new(em.outputs[0], out.inputs[0])
    tg = g.node('ShaderNodeTexImage', name='TARGET')
    g.N.active = tg
    return m


# ============================================================================================ materials
def metal(key, base=(0.28, 0.27, 0.265), rough=(0.30, 0.62), edge=None, edge_amt=0.7, rust=(0.13, 0.06, 0.03),
          rust_amt=0.25, pattern='plate', dark=None, ash=0.25, ash_h=0.45, bump=0.3, soot=0.0, scan=None, rim=None,
          relief=1.0):
    """Worn steel / iron / brass. pattern: plate | hammered | mail | brushed | scale.
    scan: chartex set for pitting / scratches (default by colour: steel, iron, bronze). rim: rolled plate rim + border
    groove along sharp edges (default on for plate / hammered)."""
    m, g = _new(key)
    edge = edge or _scale(base, 2.1)
    dark = dark or _scale(base, 0.45)
    lumb = _lum(base)
    warm = base[0] > base[2] * 1.6            # brass / bronze / gold
    if scan is None:
        scan = 'bronze' if warm else ('iron' if lumb < 0.14 else 'steel')
    if rim is None:
        rim = pattern in ('plate', 'hammered')
    R = relief
    e, cav, opn = g.masks()
    hem = g.hem()
    n1 = g.noise(4.2, 5, 0.6, 0.3)
    n2 = g.noise(31.0, 4, 0.65)
    n3 = g.noise(9.0, 6, 0.7, dist=0.7)
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.55, 0.0), base, dark)
    col = g.mix(g.mul(g.sstep(n2, 0.5, 0.85), 0.3), col, _scale(base, 1.3))
    rgh = g.lin(n1, 0.25, 0.8, rough[1], rough[0])
    met = 1.0
    height = None
    if pattern == 'mail':
        sc = g.scan('mail', slope=0.75 * R, blend=0.2)
        gap = sc.aux                                   # 1 in the holes
        col = g.tint(col, g.lum(sc.det), 1.0)
        col = g.mix(g.mul(gap, 0.92), col, _scale(base, 0.10))
        height = sc.h
        rgh = g.add(rgh, g.mul(g.sub(gap, 0.35), 0.35))
        # rusty, dull gaps; bright rubbed ring tops where the mail is exposed
        ru = g.mul(g.sstep(n3, 0.75 - rust_amt * 0.9, 0.95 - rust_amt * 0.5), g.lin(gap, 0.1, 0.7, 0.35, 1.0))
        col = g.mix(g.mul(ru, 0.85), col, rust)
        rgh = g.mixf(ru, rgh, 0.88)
        met = g.mixf(g.max(ru, g.mul(gap, 0.8)), 1.0, 0.25)
        top = g.mul(g.sstep(sc.hn, 0.6, 1.5), g.lin(n2, 0.3, 0.7, 0.3, 1.0))
        col = g.mix(g.mul(top, 0.55), col, _scale(base, 2.6))
        rgh = g.mixf(g.mul(top, 0.7), rgh, rough[0] * 0.75)
        fold, cre = g.wrinkles(0.35, tight=True)
        height = g.add(height, fold)
    else:
        sc = g.scan(scan, slope=0.085 * R)
        s2 = g.scan('iron' if scan != 'iron' else 'steel', tile=0.37, slope=0.05 * R, off=(0.31, 0.17, 0.53))
        dk = dict(steel=6.0, iron=2.5, bronze=1.6).get(scan, 2.0)
        col = g.scale_c(col, g.dgain(sc.det, dk))
        col = g.scale_c(col, g.dgain(s2.det, 2.0))
        col = g.scale_c(col, g.lin(sc.rdev, -2.0, 2.0, 1.16, 0.84))
        rgh = g.add(rgh, g.add(g.mul(sc.rdev, 0.085), g.mul(s2.rdev, 0.05)))
        height = g.add(sc.h, s2.h)
        if pattern == 'hammered':
            d = g.vor(46.0, 'SMOOTH_F1', smooth=0.55)
            d2 = g.vor(95.0, 'SMOOTH_F1', smooth=0.6, vec=g.mapping(loc=(0.37, 0.11, 0.73)))
            height = g.add(height, g.add(g.mul(d, 0.0016 * R), g.mul(d2, 0.0006 * R)))
            rgh = g.add(rgh, g.mul(g.sub(d, 0.3), 0.16))
            col = g.mix(g.mul(g.sstep(d, 0.05, 0.55), 0.28), col, _scale(base, 1.28))
        elif pattern == 'brushed':
            s = g.noise(1.0, 3, 0.6, vec=g.mapping(scale=(140.0, 140.0, 6.0)))
            height = g.add(height, g.mul(s, 0.00035 * R))
            rgh = g.add(rgh, g.mul(g.sub(s, 0.5), 0.22))
            col = g.mix(g.mul(g.sstep(s, 0.45, 0.8), 0.25), col, _scale(base, 1.3))
        elif pattern == 'scale':
            d = g.vor(42.0, 'F1', vec=g.mapping(scale=(1, 1, 0.7)))
            scl = g.sstep(d, 0.75, 0.2)
            height = g.add(height, g.mul(scl, 0.0030 * R))
            col = g.mix(g.inv(scl), col, _scale(base, 0.35))
        # shallow battle dents + long-wave unevenness of hand-forged plate
        dn = g.noise(11.0, 2, 0.5, 0.3)
        height = g.add(height, g.mul(dn, 0.0022 * R))
        # scratches: a few directions, thin bright grooves
        for i, (rot, sc_, thr) in enumerate((((0.3, 0.2, 0.5), 15.0, 0.70), ((1.1, -0.4, 2.0), 21.0, 0.72), ((-0.6, 1.3, 0.9), 27.0, 0.74))):
            s1 = g.noise(sc_, 2, 0.5, vec=g.mapping(scale=(1, 16, 1), rot=rot))
            scm = g.mul(g.sstep(s1, thr, thr + 0.035), g.lin(n2, 0.3, 0.7, 0.4, 1.0))
            col = g.mix(g.mul(scm, 0.5), col, edge)
            rgh = g.sub(rgh, g.mul(scm, 0.16))
            height = g.sub(height, g.mul(scm, 0.00030 * R))
        # pitting
        pv = g.vor(210.0, 'F1')
        pit = g.mul(g.sstep(pv, 0.30, 0.12), g.sstep(g.noise(6.0, 3, 0.6), 0.45, 0.7))
        col = g.mix(g.mul(pit, 0.75), col, _scale(dark, 0.4))
        rgh = g.add(rgh, g.mul(pit, 0.3))
        height = g.sub(height, g.mul(pit, 0.00045 * R))
        if rim:
            # rolled rim along the sharp plate edges and an engraved border line just inside it
            roll = g.sstep(hem, 0.0075, 0.0015)
            grv = g.band(hem, 0.0135, 0.0036)
            height = g.add(height, g.sub(g.mul(roll, 0.0011 * R), g.mul(grv, 0.0009 * R)))
            col = g.mix(g.mul(grv, 0.7), col, _scale(dark, 0.45))
            rgh = g.add(rgh, g.mul(grv, 0.25))
        if pattern == 'brushed':
            # honed cutting edge: a bright, smooth bevel along the sharp edges with small nicks
            hone = g.mul(g.sstep(hem, 0.0065, 0.003), g.lin(g.noise(60.0, 2, 0.6), 0.3, 0.7, 0.6, 1.0))
            col = g.mix(g.mul(hone, 0.8), col, _scale(edge, 1.0))
            rgh = g.mixf(g.mul(hone, 0.85), rgh, max(0.08, rough[0] * 0.55))
            height = g.sub(height, g.mul(g.sstep(hem, 0.006, 0.0), 0.0005 * R))
    # edge wear to bright metal (baked bevel mask, convex only), broken up
    if edge_amt > 0:
        ew = g.clamp(g.mul(g.mul(e, g.lin(n3, 0.2, 0.7, 0.35, 1.0)), edge_amt * 1.7))
        if height is not None and pattern != 'mail':
            ew = g.max(ew, g.mul(g.mul(g.sstep(sc.hn, 0.9, 2.0), g.lin(n2, 0.35, 0.75, 0.0, 1.0)), edge_amt * 0.45))
        col = g.mix(g.mul(ew, 0.9), col, edge)
        rgh = g.mixf(ew, rgh, rough[0] * 0.7)
    # rust / verdigris: in cavities and in noise patches, with its own scanned texture
    if rust_amt > 0:
        rs = g.scan('rust_b', tile=0.42, slope=0.20 * R, off=(0.13, 0.57, 0.29))
        pat = g.sstep(n3, 1.0 - rust_amt * 0.95, 1.03 - rust_amt * 0.5)
        cv = g.mul(g.sstep(cav, 0.15, 0.7), min(1.0, 0.55 + rust_amt))
        r = g.clamp(g.max(g.mul(pat, g.lin(n2, 0.3, 0.7, 0.45, 1.0)), g.mul(cv, g.lin(n2, 0.25, 0.75, 0.5, 1.0))))
        r = g.mul(r, g.inv(g.mul(e, 0.8))) if edge_amt > 0 else r
        rc = g.tint(rust, g.lum(rs.det), 0.9)
        rc = g.mix(g.lin(n1, 0.3, 0.7), rc, _scale(rust, 0.45))
        col = g.mix(g.mul(r, 0.92), col, rc)
        rgh = g.mixf(r, rgh, 0.86)
        met = g.mixf(g.mul(r, 0.85), met, 0.0)
        height = g.add(height, g.mul(r, g.add(g.mul(rs.h, 1.0), 0.00025 * R)))
    # soot + cavity grime
    if soot > 0:
        s = g.mul(g.sstep(g.noise(3.0, 4, 0.6), 0.4, 0.75), soot)
        s = g.max(s, g.mul(g.sstep(cav, 0.1, 0.6), min(1.0, soot * 1.6)))
        col = g.mix(g.mul(s, 0.9), col, (0.010, 0.009, 0.008))
        rgh = g.mixf(s, rgh, 0.82)
        met = g.mixf(g.mul(s, 0.6), met, 0.0)
    col, rgh, gm = g.grime(col, rgh, 0.9, dirt=_scale(dark, 0.22))
    met = g.mixf(g.mul(gm, 0.5), met, 0.0)
    col, rgh, met, _a = g.dust(col, rgh, ash, ash_h, met=met)
    g.finish(col, g.clamp(rgh), met, height)
    SRCINFO[key] = dict(color=base, rough=(rough[0] + rough[1]) / 2, metal=1.0)
    return key


def _cloth_scan(key, weave):
    if _has(key, 'velvet', 'stole'):
        return 'velvet'
    if _has(key, 'emblem', 'brocade'):
        return 'brocade'
    if _has(key, 'char', 'shroud', 'sack', 'burlap', 'rag'):
        return 'burlap'
    if weave >= 1.5 or _has(key, 'bandage', 'apron'):
        return 'linen'
    if _has(key, 'coat', 'vest', 'hose', 'sleeve', 'wrap'):
        return 'twill'
    if _has(key, 'cloak', 'hood', 'cowl', 'wool', 'dress', 'scarf', 'shawl', 'tabard'):
        return 'wool'
    if _has(key, 'sash', 'shoe', 'canvas', 'mask', 'pants'):
        return 'canvas'
    return 'linen'


_CLOTH_SLOPE = dict(wool=0.17, linen=0.22, twill=0.24, brocade=0.30, burlap=0.30, velvet=0.15, canvas=0.17, fleece=0.30)


def cloth(key, base=(0.10, 0.03, 0.02), var=0.3, ash=0.5, ash_h=0.55, folds=0.5, weave=1.0, worn=0.4, bands=None,
          rough=(0.82, 0.98), fine=1.0, bump=0.4, scan=None, tight=None, hem=True, relief=1.0):
    """Wool / linen / velvet / burlap. bands = [(v0, v1, colour)] trim bands along the loft v parameter.
    scan: chartex fabric set (default from the key name), tight: fitted garment (joint wrinkles instead of drapery)."""
    m, g = _new(key)
    scan = scan or _cloth_scan(key, weave)
    if tight is None:
        tight = _has(key, 'hose', 'pants', 'trouser', 'legging', 'shirt', 'under', 'wrap', 'bandage', 'glove', 'shoe', 'mask',
                     'inner', 'vest', 'bodice', 'linen')
    R = relief
    e, cav, opn = g.masks()
    hm = g.hem()
    n1 = g.noise(3.4, 5, 0.6, 0.3)
    n2 = g.noise(23.0, 4, 0.7)
    n3 = g.noise(11.0, 5, 0.7, dist=0.8)
    sc = g.scan(scan, tile={'wool': 0.36, 'canvas': 0.45}.get(scan), slope=_CLOTH_SLOPE.get(scan, 0.22) * (0.6 + 0.4 * min(weave, 2.5)) * R)
    tw = g.scan('twill', tile=0.47, slope=0.13 * R, off=(0.19, 0.37, 0.71)) if scan == 'wool' else None
    # ---- colour: large-scale value / hue drift, sun-faded upper faces, stains, threadbare patches
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(base, 1.0 - var), _scale(base, 1.0 + var * 0.9))
    faded = _mixc(_sat(base, 0.55), (_lum(base) * 1.9 + 0.02,) * 3, 0.4)
    wornm = g.mul(g.sstep(n2, 0.5, 0.85), worn)
    col = g.mix(g.mul(g.up(), 0.35), col, faded)
    col = g.mix(wornm, col, faded)
    col = g.mix(g.mul(g.sstep(n3, 0.5, 0.22), 0.5), col, _scale(_sat(base, 1.15), 0.48))          # damp / greasy stains
    col = g.mix(g.mul(g.sstep(n3, 0.6, 0.82), 0.25), col, _scale(base, 1.4))
    col = g.tint(col, sc.det, 0.95)
    mh = sc.h
    if tw is not None:
        col = g.tint(col, g.lum(tw.det), 0.6)
        mh = g.add(mh, tw.h)
    micro = g.mul(mh, g.lin(wornm, 0.0, 1.0, 1.0, 0.45))                                        # threadbare = flatter
    col = g.mix(g.mul(g.sstep(sc.hn, -0.4, -1.8), 0.45), col, _scale(base, 0.35))               # shadowed weave pits
    rgh = g.lin(n2, 0.3, 0.7, rough[0] - 0.14, rough[1])
    rgh = g.add(rgh, g.mul(sc.rdev, 0.05))
    rgh = g.sub(rgh, g.mul(wornm, 0.16))                                                        # rubbed shiny
    height = micro
    # ---- trim bands (lofted parts): brocade relief, corded borders
    if bands:
        u, v = g.luv()
        br = g.scan('brocade', tile=0.16, slope=0.34 * R, off=(0.21, 0.43, 0.11))
        for (v0, v1, c) in bands:
            b = g.mul(g.sstep(v, v0 - 0.003, v0 + 0.003), g.sstep(v, v1 + 0.003, v1 - 0.003))
            w = max(v1 - v0, 1e-3)
            cord = g.max(g.band(v, v0 + w * 0.10, w * 0.12), g.band(v, v1 - w * 0.10, w * 0.12))
            cb = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(c, 0.7), _scale(c, 1.25))
            cb = g.tint(cb, g.lum(br.det), 1.0)
            cb = g.mix(g.mul(g.sstep(br.hn, 0.3, 1.5), 0.45), cb, _scale(c, 1.7))
            cb = g.mix(g.mul(cord, 0.5), cb, _scale(c, 0.55))
            col = g.mix(b, col, cb)
            height = g.add(g.mul(height, g.lin(b, 0, 1, 1.0, 0.35)), g.mul(b, g.add(br.h, g.add(0.0006 * R, g.mul(cord, 0.0009 * R)))))
            rgh = g.mixf(g.mul(b, 0.8), rgh, g.lin(br.hn, -1.0, 1.5, 0.78, 0.48))
    # ---- folds / wrinkles (3-15 cm band) with darker valleys
    if folds > 0:
        fold, cre = g.wrinkles(folds, tight)
        height = g.add(height, fold)
        col = g.mix(g.mul(cre, 0.16 * min(1.0, folds + 0.3)), col, _scale(base, 0.5))
    # ---- hems: folded double-thickness border with a stitch line, or a frayed pale edge on worn cloth
    if hem:
        if worn >= 0.55:
            fr = g.mul(g.sstep(hm, 0.010, 0.0), g.lin(g.noise(55.0, 3, 0.7), 0.3, 0.7, 0.2, 1.0))
            col = g.mix(g.mul(fr, 0.7), col, faded)
            height = g.add(height, g.mul(g.mul(fr, g.sub(g.noise(140.0, 2, 0.6), 0.5)), 0.0016 * R))
            rgh = g.add(rgh, g.mul(fr, 0.06))
        else:
            hb = g.sstep(hm, 0.015, 0.011)
            st = g.stitches(hm, 0.009, 0.0030, 0.012)
            height = g.add(height, g.add(g.mul(hb, 0.0009 * R), g.mul(st, 0.0006 * R)))
            col = g.mix(g.mul(g.band(hm, 0.0145, 0.003), 0.45), col, _scale(base, 0.4))
            col = g.mix(g.mul(st, 0.65), col, _mixc(_scale(base, 1.9), (0.30, 0.26, 0.20), 0.5))
    # ---- wear on exposed edges, dirt in contact creases, ash / dust
    ew = g.mul(g.mul(e, g.lin(n2, 0.3, 0.7, 0.3, 1.0)), 0.5 + worn * 0.6)
    col = g.mix(g.mul(ew, 0.55), col, faded)
    col, rgh, gm = g.grime(col, rgh, 1.0, dirt=_scale(_sat(base, 0.6), 0.16))
    col, rgh, _m, _a = g.dust(col, rgh, ash, ash_h, micro=sc.hn)
    g.finish(col, g.clamp(rgh), 0.0, height)
    SRCINFO[key] = dict(color=base, rough=0.9, metal=0.0)
    return key


def leather(key, base=(0.07, 0.04, 0.025), edge=None, crack=1.0, rough=(0.5, 0.78), ash=0.35, ash_h=0.4, bump=0.2,
            worn=0.5, scan=None, tight=None, relief=1.0):
    """Leather / hide. scan: 'leather' (creased), 'leather_b' (garment), 'hide' (pale pebbled) - default from the key."""
    m, g = _new(key)
    edge = edge or _mixc(_scale(base, 2.6), (0.30, 0.22, 0.14), 0.4)
    if scan is None:
        scan = 'hide' if _has(key, 'hide', 'tan') else ('leather' if _has(key, 'coat', 'jerkin', 'tome', 'char') else 'leather_old')
    if tight is None:
        tight = True
    R = relief
    e, cav, opn = g.masks()
    hm = g.hem()
    n1 = g.noise(5.0, 5, 0.6, 0.3)
    n2 = g.noise(38.0, 4, 0.6)
    n3 = g.noise(13.0, 4, 0.65, 0.6)
    sc = g.scan(scan, slope=dict(leather=0.20, leather_b=0.16, hide=0.16, leather_old=0.20).get(scan, 0.18) * R)
    gr = g.scan('hide', tile=0.33, slope=0.13 * R, off=(0.41, 0.23, 0.67))                  # pebble grain everywhere
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(base, 0.6), _scale(_sat(base, 1.1), 1.35))
    col = g.tint(col, sc.det, 0.8)
    col = g.tint(col, g.lum(gr.det), 0.6)
    height = g.add(sc.h, gr.h)
    rgh = g.lin(n1, 0.25, 0.8, rough[0] - 0.12, rough[1])
    rgh = g.add(rgh, g.mul(sc.rdev, 0.06))
    # flex creases + cracks of old dry leather: dark, recessed lines in a cell network, strongest where it bends
    fold, cre = g.wrinkles(0.55, tight)
    height = g.add(height, fold)
    cr = g.vor(62.0, 'DISTANCE_TO_EDGE', vec=g.mapping(scale=(1.0, 1.0, 1.6)))
    cr2 = g.vor(150.0, 'DISTANCE_TO_EDGE', vec=g.mapping(loc=(0.3, 0.7, 0.1)))
    zone = g.sstep(n3, 0.42, 0.68)
    crm = g.mul(g.max(g.mul(g.sstep(cr, 0.055, 0.0), zone), g.mul(g.sstep(cr2, 0.07, 0.0), g.mul(zone, 0.6))), min(1.0, crack))
    col = g.mix(g.mul(crm, 0.8), col, _scale(base, 0.22))
    height = g.sub(height, g.mul(crm, 0.00055 * R))
    rgh = g.add(rgh, g.mul(crm, 0.22))
    # scuffs: pale, dry, rough patches; polish: darker glossy high spots where hands and straps rub
    scuff = g.mul(g.sstep(n2, 0.55, 0.8), worn)
    col = g.mix(g.mul(scuff, 0.55), col, edge)
    rgh = g.add(rgh, g.mul(scuff, 0.18))
    pol = g.mul(g.mul(g.sstep(n3, 0.6, 0.3), g.sstep(sc.hn, -0.2, 1.2)), 0.7)
    rgh = g.sub(rgh, g.mul(pol, 0.26))
    col = g.mix(g.mul(pol, 0.25), col, _scale(base, 0.7))
    # edges: worn pale raw leather on convex edges; burnished dark cut edge; saddle stitch along the border
    ew = g.clamp(g.mul(g.mul(e, g.lin(n2, 0.25, 0.7, 0.35, 1.0)), 0.9 + worn * 0.9))
    col = g.mix(g.mul(ew, 0.85), col, edge)
    rgh = g.add(rgh, g.mul(ew, 0.12))
    cut = g.sstep(hm, 0.0035, 0.0010)
    col = g.mix(g.mul(cut, 0.6), col, _scale(base, 0.35))
    st = g.stitches(hm, 0.0095, 0.0030, 0.0125)
    grv = g.band(hm, 0.0095, 0.0050)
    height = g.add(height, g.sub(g.mul(st, 0.0008 * R), g.mul(grv, 0.00035 * R)))
    col = g.mix(g.mul(st, 0.8), col, _mixc(_scale(base, 2.2), (0.36, 0.30, 0.20), 0.6))
    rgh = g.add(rgh, g.mul(st, 0.15))
    col, rgh, gm = g.grime(col, rgh, 1.0, dirt=_scale(base, 0.14))
    col, rgh, _m, _a = g.dust(col, rgh, ash, ash_h, micro=gr.hn)
    g.finish(col, g.clamp(rgh), 0.0, height)
    SRCINFO[key] = dict(color=base, rough=0.6, metal=0.0)
    return key


def fur(key, base=(0.06, 0.045, 0.035), tip=(0.22, 0.18, 0.14), strand=1.0, bump=0.8, scan=None, relief=1.0, locks=1.0):
    """Pelt: layered locks (synthesised lock tile: lightness / tip-ness / cavity + height), dark roots, pale tips,
    clumps at two scales; raw hide and thin wispy hair along the pelt border."""
    m, g = _new(key)
    scan = scan or ('fur_long' if _has(key, 'pelt', 'wolf', 'mane') else 'fur')
    R = relief
    e, cav, opn = g.masks()
    hm = g.hem()
    sc = g.scan(scan, slope=0.55 * strand * R, blend=0.22)
    s2 = g.scan(scan, tile=CT.SETS[scan]['tile'] * 2.3, slope=0.30 * strand * R, off=(0.37, 0.61, 0.13), blend=0.22)
    lum, tipn, cv = g.sep(sc.det)
    lum2, tipn2, cv2 = g.sep(s2.det)
    n1 = g.noise(4.5, 4, 0.6, 0.4)
    n2 = g.noise(19.0, 3, 0.6)
    t = g.clamp(g.add(g.mul(tipn, 0.8), g.mul(tipn2, 0.35)))
    col = g.mix(g.pow(t, 1.25), _scale(base, 0.75), tip)
    col = g.mix(g.lin(n1, 0.3, 0.75, 0.0, 0.55), col, _scale(_mixc(base, tip, 0.3), 0.8))            # darker / greyer patches
    col = g.scale_c(col, g.mul(lum, g.lin(lum2, 0.5, 1.5, 0.75, 1.2)))
    deep = g.clamp(g.add(g.mul(cv, 0.9), g.mul(cv2, 0.6)))
    col = g.mix(g.mul(deep, 0.85), col, _scale(base, 0.22))
    height = g.add(sc.h, s2.h)
    height = g.add(height, g.mul(g.sub(g.noise(13.0, 2, 0.5, 0.5), 0.5), 0.006 * R))                 # tufty lumps
    rgh = g.lin(t, 0.0, 1.0, 0.92, 0.58)
    rgh = g.add(rgh, g.mul(deep, 0.1))
    # border: locks thin out to bare hide
    bare = g.mul(g.sstep(hm, 0.012, 0.002), g.lin(n2, 0.3, 0.7, 0.4, 1.0))
    col = g.mix(g.mul(bare, 0.6), col, _mixc(base, (0.20, 0.15, 0.10), 0.6))
    col, rgh, gm = g.grime(col, rgh, 0.8, dirt=_scale(base, 0.2))
    col, rgh, _m, _a = g.dust(col, rgh, 0.3, 0.35, micro=sc.hn)
    # free-standing locks (finalize._fur_fins, vertex attribute 'fin': 0.15 at the root, 1 at the tip): the pelt's own
    # mid tone at the root -> pale wispy tip, a few strands, so each triangle reads as a tapering lock of the same fur
    fa = g.node('ShaderNodeAttribute', attribute_name='fin').outputs['Fac']
    isfin = g.sstep(fa, 0.05, 0.13)
    ft = g.clamp(g.math('DIVIDE', g.sub(fa, 0.15), 0.85))
    fcol = g.mix(g.pow(ft, 1.1), _mixc(base, tip, 0.42), _scale(tip, 1.0))
    fcol = g.scale_c(fcol, g.mul(g.lin(lum, 0.6, 1.4, 0.8, 1.2), g.lin(n2, 0.3, 0.7, 0.88, 1.12)))
    col = g.mix(isfin, col, fcol)
    rgh = g.mixf(isfin, rgh, g.lin(ft, 0.0, 1.0, 0.86, 0.6))
    height = g.mul(height, g.lin(isfin, 0.0, 1.0, 1.0, 0.45))
    g.finish(col, g.clamp(rgh), 0.0, height)
    long_ = scan == 'fur_long'
    SRCINFO[key] = dict(color=_mixc(base, tip, 0.4), rough=0.85, metal=0.0,
                        fur=dict(density=(620.0 if long_ else 760.0) * locks, length=(0.028, 0.056) if long_ else (0.024, 0.044),
                                 width=0.0085 if long_ else 0.0080) if locks > 0 else None)
    return key


def bone(key, base=(0.50, 0.44, 0.33), stain=(0.16, 0.10, 0.05), rough=(0.42, 0.7), cracks=1.0, bump=0.3, relief=1.0):
    """Old bone / horn / antler: porous, longitudinal cracks, brown stains held in pits and joints, polished high spots."""
    m, g = _new(key)
    R = relief
    e, cav, opn = g.masks()
    n1 = g.noise(6.0, 5, 0.65, 0.3)
    n2 = g.noise(52.0, 3, 0.6)
    sc = g.scan('bone', tile=0.30, slope=0.14 * R)
    s2 = g.scan('bronze', tile=0.22, slope=0.09 * R, off=(0.2, 0.5, 0.8))                    # fine pitting
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(base, 0.68), _scale(base, 1.15))
    col = g.tint(col, g.lum(sc.det), 0.55)
    col = g.tint(col, g.lum(s2.det), 0.5)
    col = g.mix(g.mul(g.sstep(n1, 0.55, 0.25), 0.6), col, stain)
    height = g.add(sc.h, s2.h)
    # cracks: long, wandering, along the part (stretched domain), plus short cross-checks
    crn = g.noise(1.0, 4, 0.7, 1.4, vec=g.mapping(scale=(9.0, 9.0, 1.6)))
    crm = g.mul(g.sstep(g.abs(g.sub(crn, 0.5)), 0.016, 0.0), min(1.0, cracks))
    cr2 = g.mul(g.sstep(g.vor(70.0, 'DISTANCE_TO_EDGE', vec=g.mapping(scale=(1.0, 1.0, 0.35))), 0.035, 0.0),
                g.mul(g.sstep(n1, 0.5, 0.7), 0.7 * min(1.0, cracks)))
    crm = g.max(crm, cr2)
    col = g.mix(g.mul(crm, 0.9), col, _scale(stain, 0.35))
    height = g.sub(height, g.mul(crm, 0.0008 * R))
    # pores
    pv = g.vor(240.0, 'F1')
    pores = g.mul(g.sstep(pv, 0.3, 0.1), g.sstep(n2, 0.35, 0.6))
    col = g.mix(g.mul(pores, 0.7), col, _scale(stain, 0.6))
    height = g.sub(height, g.mul(pores, 0.0004 * R))
    rgh = g.lin(n2, 0.3, 0.7, rough[0] - 0.08, rough[1] + 0.08)
    rgh = g.add(rgh, g.add(g.mul(crm, 0.2), g.mul(pores, 0.2)))
    # handled / exposed ridges polish up and bleach; joints and pits hold brown dirt
    ew = g.mul(e, g.lin(n2, 0.3, 0.7, 0.4, 1.0))
    col = g.mix(g.mul(ew, 0.5), col, _scale(base, 1.35))
    rgh = g.sub(rgh, g.mul(ew, 0.2))
    col, rgh, gm = g.grime(col, rgh, 1.0, dirt=_scale(stain, 0.45), rough_to=0.85)
    col, rgh, _m, _a = g.dust(col, rgh, 0.25, 0.4)
    g.finish(col, g.clamp(rgh), 0.0, height)
    SRCINFO[key] = dict(color=base, rough=0.55, metal=0.0)
    return key


def wood(key, base=(0.085, 0.05, 0.028), dark=None, rough=(0.55, 0.8), bump=0.45, luv_grain=True, scan=None, relief=1.0):
    """Staff / haft wood and bark. Grain follows the tube direction through the 'luv' attribute (u=angle, v=metres);
    keys containing 'bark' use the bark scans with deep furrows."""
    m, g = _new(key)
    dark = dark or _scale(base, 0.35)
    R = relief
    barky = _has(key, 'bark')
    e, cav, opn = g.masks()
    n1 = g.noise(8.0, 4, 0.6, 0.3)
    n2 = g.noise(40.0, 3, 0.6)
    if barky:
        sc = g.scan('bark', tile=0.42, slope=0.55 * R)
        s2 = g.scan('bark_b', tile=0.30, slope=0.30 * R, off=(0.3, 0.1, 0.6))
        col = g.mix(g.sstep(sc.hn, -1.2, 1.2), _scale(dark, 0.7), _scale(base, 1.25))
        col = g.tint(col, sc.det, 0.75)
        col = g.tint(col, g.lum(s2.det), 0.5)
        height = g.add(sc.h, s2.h)
        rgh = g.lin(sc.hn, -1.0, 1.5, rough[1], rough[0])
        # lichen / moss dust on the upper faces, pale dead wood on worn ridges
        col = g.mix(g.mul(g.mul(g.up(), g.sstep(n1, 0.45, 0.7)), 0.4), col, (0.060, 0.075, 0.035))
        ew = g.mul(e, g.lin(n2, 0.3, 0.7, 0.4, 1.0))
        col = g.mix(g.mul(ew, 0.6), col, _mixc(_scale(base, 2.2), (0.30, 0.25, 0.18), 0.5))
    else:
        if luv_grain:
            u, v = g.luv()
            vec = g.comb(g.mul(u, 0.35), g.mul(v, 2.4), 0.0)
            sc = g.scan('wood', slope=0.26 * R, vec=vec, flat=True)
            gr = g.noise(4.0, 5, 0.65, dist=0.8, vec=g.comb(g.mul(u, 9.0), g.mul(v, 3.0), 0.0))
        else:
            sc = g.scan('wood', tile=0.36, slope=0.26 * R)
            gr = g.noise(1.0, 5, 0.65, dist=0.8, vec=g.mapping(scale=(36.0, 36.0, 3.0)))
        col = g.mix(g.sstep(gr, 0.35, 0.7), dark, base)
        col = g.tint(col, sc.det, 0.8)
        col = g.mix(g.mul(g.sstep(n1, 0.55, 0.8), 0.35), col, _scale(base, 1.5))
        height = g.add(sc.h, g.mul(gr, 0.0009 * R))
        rgh = g.lin(gr, 0.3, 0.7, rough[1], rough[0] - 0.1)
        rgh = g.add(rgh, g.mul(sc.rdev, 0.05))
        # dents and nicks; hand-polished dark sheen in patches; pale splintered edges
        nk = g.sstep(g.vor(55.0, 'F1'), 0.16, 0.05)
        nk = g.mul(nk, g.sstep(n1, 0.5, 0.7))
        height = g.sub(height, g.mul(nk, 0.0012 * R))
        col = g.mix(g.mul(nk, 0.6), col, _scale(dark, 0.6))
        pol = g.sstep(g.noise(5.0, 3, 0.6), 0.5, 0.75)
        rgh = g.sub(rgh, g.mul(pol, 0.22))
        col = g.mix(g.mul(pol, 0.3), col, _scale(base, 0.7))
        ew = g.mul(e, g.lin(n2, 0.3, 0.7, 0.4, 1.0))
        col = g.mix(g.mul(ew, 0.7), col, _scale(base, 2.0))
    col, rgh, gm = g.grime(col, rgh, 1.0, dirt=_scale(dark, 0.3))
    col, rgh, _m, _a = g.dust(col, rgh, 0.2, 0.35)
    g.finish(col, g.clamp(rgh), 0.0, height)
    SRCINFO[key] = dict(color=base, rough=0.7, metal=0.0)
    return key


def porcelain(key, base=(0.62, 0.60, 0.55), line=(0.10, 0.07, 0.05), rough=(0.16, 0.38), crackle=1.0, relief=1.0):
    m, g = _new(key)
    R = relief
    e, cav, opn = g.masks()
    n1 = g.noise(6.0, 4, 0.6)
    cr = g.vor(48.0, 'DISTANCE_TO_EDGE')
    crm = g.mul(g.sstep(cr, 0.03, 0.0), crackle)
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(base, 0.82), _scale(base, 1.05))
    col = g.mix(g.mul(crm, 0.85), col, line)
    col = g.mix(g.mul(g.sstep(n1, 0.6, 0.85), 0.35), col, _mixc(base, line, 0.5))
    chip = g.mul(e, g.sstep(g.noise(30.0, 3, 0.6), 0.5, 0.7))
    col = g.mix(g.mul(chip, 0.8), col, _scale(base, 0.55))
    rgh = g.lin(n1, 0.3, 0.75, rough[0], rough[1])
    rgh = g.add(rgh, g.add(g.mul(crm, 0.4), g.mul(chip, 0.5)))
    height = g.sub(g.mul(g.noise(60.0, 2, 0.5), 0.0003 * R), g.add(g.mul(crm, 0.0005 * R), g.mul(chip, 0.0008 * R)))
    col, rgh, gm = g.grime(col, rgh, 1.0, dirt=_scale(line, 0.6))
    g.finish(col, g.clamp(rgh), 0.0, height)
    SRCINFO[key] = dict(color=base, rough=0.25, metal=0.0)
    return key


def flat(key, color, rough=0.6, metal=0.0, relief=1.0):
    """Small props of one colour. The substance is taken from the key: rope / cord / knot (twisted strands), paper /
    parchment (fibres, foxing), wax, feather (barbs), gem / garnet (polished), otherwise a mottled matte solid."""
    m, g = _new(key)
    R = relief
    e, cav, opn = g.masks()
    n1 = g.noise(18.0, 4, 0.6)
    n2 = g.noise(5.0, 4, 0.6, 0.4)
    col = g.mix(g.lin(n1, 0.25, 0.8, 0.0, 1.0), _scale(color, 0.72), _scale(color, 1.22))
    rgh = g.lin(n1, 0.3, 0.7, rough * 0.82, min(1.0, rough * 1.15))
    height = g.mul(n1, 0.0004 * R)
    met = metal
    if _has(key, 'rope', 'cord', 'knot', 'twine'):
        sc = g.scan('rope', tile=0.11, slope=0.45 * R)
        col = g.tint(col, g.lum(sc.det), 1.0)
        col = g.mix(g.mul(g.sstep(sc.hn, -0.3, -1.6), 0.6), col, _scale(color, 0.3))
        height = sc.h
        rgh = g.add(rgh, g.mul(sc.rdev, 0.08))
    elif _has(key, 'paper', 'parchment', 'scroll', 'page'):
        sc = g.scan('linen', tile=0.5, slope=0.10 * R)
        col = g.tint(col, g.lum(sc.det), 0.6)
        fox = g.sstep(g.noise(34.0, 4, 0.7), 0.62, 0.8)
        col = g.mix(g.mul(fox, 0.55), col, _mixc(color, (0.18, 0.10, 0.04), 0.7))
        col = g.mix(g.mul(g.sstep(n2, 0.55, 0.3), 0.4), col, _scale(color, 0.62))
        col = g.mix(g.mul(e, 0.6), col, _mixc(color, (0.10, 0.06, 0.03), 0.6))                 # browned edges
        height = g.add(sc.h, g.mul(g.sub(g.noise(9.0, 3, 0.6), 0.5), 0.0012 * R))
    elif _has(key, 'wax'):
        dr = g.noise(1.0, 3, 0.6, 0.6, vec=g.mapping(scale=(30.0, 30.0, 6.0)))
        height = g.mul(dr, 0.0012 * R)
        col = g.mix(g.sstep(dr, 0.4, 0.7), _scale(color, 0.7), _scale(color, 1.25))
        rgh = g.lin(dr, 0.3, 0.7, rough * 1.2, rough * 0.6)
    elif _has(key, 'feather'):
        b1 = g.wave(260.0, vec=g.mapping(rot=(0.0, 0.6, 0.5)), direction='X', dist=1.2)
        b2 = g.noise(1.0, 3, 0.6, vec=g.mapping(scale=(5.0, 60.0, 60.0), rot=(0.0, 0.6, 0.5)))
        col = g.mix(g.sstep(b1, 0.2, 0.9), _scale(color, 0.62), _scale(color, 1.2))
        col = g.mix(g.mul(g.sstep(b2, 0.55, 0.75), 0.6), col, _scale(color, 0.45))                # splits / darker bars
        col = g.mix(g.mul(g.sstep(n2, 0.5, 0.75), 0.45), col, _scale(color, 1.5))
        height = g.add(g.mul(b1, 0.0005 * R), g.mul(b2, 0.0006 * R))
        rgh = g.lin(b1, 0.2, 0.9, min(1.0, rough * 1.1), rough * 0.62)
    elif _has(key, 'garnet', 'gem', 'glass', 'crystal'):
        sm = g.sstep(g.noise(40.0, 3, 0.6), 0.45, 0.7)
        col = g.mix(g.lin(n2, 0.3, 0.7), _scale(color, 0.55), _scale(color, 1.6))
        rgh = g.mixf(sm, max(0.04, rough * 0.5), min(1.0, rough * 2.2))
        height = g.mul(g.sstep(g.noise(90.0, 2, 0.5), 0.6, 0.75), -0.00025 * R)
    else:
        sc = g.scan('hide', tile=0.4, slope=0.10 * R)
        col = g.tint(col, g.lum(sc.det), 0.7)
        height = g.add(height, sc.h)
    col = g.mix(g.mul(e, 0.3), col, _scale(color, 1.5))
    col, rgh, gm = g.grime(col, rgh, 1.0, dirt=_scale(color, 0.2))
    g.finish(col, g.clamp(rgh), met, height)
    SRCINFO[key] = dict(color=color, rough=rough, metal=metal)
    return key


def emis(key, color, strength=6.0, base=None):
    """Emissive accent (not baked)."""
    SRCINFO[key] = dict(color=base or _scale(color, 0.25), rough=0.5, metal=0.0, emis=color, strength=strength)
    return key


# ============================================================================================ skin / eyes / hair sources
def _uvtex(g, uvn, path, cs):
    t = g.node('ShaderNodeTexImage')
    t.image = load_img(path, cs)
    g.L.new(uvn.outputs[0], t.inputs['Vector'])
    return t


def skin_src(key, files, uv, lm, male=True, stubble=None, scars=0.3, grime=0.6, pores=1.0, age=None, relief=1.0,
             rough_add=0.0):
    """Skin: the painted Quaternius textures (colour / normal / roughness on the original UV set `uv`) with real micro
    relief and variation layered on top in object space: polygonal micro-crease network + pores (finer on the face),
    forehead / neck / knuckle wrinkles, crumpled elbows and knees, colour zones (redder cheeks, nose, ears, knuckles,
    paler brow, darker eye sockets), mottling, freckles and moles, veins on forearms and hands, stubble, old scars with
    raised edges, oilier T-zone, grime in creases; and around ember welts ('heat_d' vertex attribute = distance to
    emissive geometry lying on the skin) a charred crust with a swollen rim and inflamed skin.
    lm: face landmarks dict(ex, ey, ez, ny, nz, cz) in rest space."""
    m, g = _new(key)
    R = relief
    if stubble is None:
        stubble = 0.6 if male else 0.0
    if age is None:
        age = 0.6 if male else 0.3
    if not male:
        pores = pores * 0.8
    uvn = g.node('ShaderNodeUVMap')
    uvn.uv_map = uv
    tc = _uvtex(g, uvn, files[0], 'sRGB')
    tn = _uvtex(g, uvn, files[1], 'Non-Color')
    tr = _uvtex(g, uvn, files[2], 'Non-Color')
    nm = g.node('ShaderNodeNormalMap')
    nm.uv_map = uv
    nm.inputs['Strength'].default_value = 1.3
    g.L.new(tn.outputs['Color'], nm.inputs['Color'])
    col = tc.outputs['Color']
    x, y, z = g.xyz()
    ax = g.abs(x)
    e, cav, opn = g.masks()
    ex, ey, ez, ny, nz, cz = lm['ex'], lm['ey'], lm['ez'], lm['ny'], lm['nz'], lm['cz']
    # ---- regions
    head = g.sstep(z, cz - 0.03, cz + 0.0)
    front = g.sstep(y, ey + 0.095, ey + 0.035)
    armz = g.mul(g.sstep(z, 1.22, 1.32), g.sstep(ax, 0.22, 0.30))
    hand = g.mul(g.sstep(ax, 0.705, 0.745), g.sstep(z, 1.20, 1.30))
    fingers = g.mul(g.sstep(ax, 0.795, 0.825), g.sstep(z, 1.20, 1.30))
    cheek = g.mul(g.mul(g.gauss(ax, ex + 0.016, 0.027), g.gauss(z, ez - 0.042, 0.024)), g.mul(front, head))
    nose = g.mul(g.mul(g.gauss(x, 0.0, 0.019), g.gauss(z, nz + 0.008, 0.021)), g.mul(g.sstep(y, ny + 0.04, ny + 0.008), head))
    brow = g.mul(g.mul(g.sstep(z, ez + 0.018, ez + 0.042), g.sstep(ax, 0.080, 0.050)), g.mul(front, head))
    chin = g.mul(g.mul(g.gauss(x, 0.0, 0.030), g.gauss(z, cz + 0.022, 0.020)), g.mul(front, head))
    socket = g.mul(g.mul(g.gauss(ax, ex, 0.021), g.gauss(z, ez, 0.014)), g.mul(front, head))
    mouth = g.mul(g.mul(g.gauss(x, 0.0, 0.028), g.gauss(z, nz - 0.031, 0.011)), g.mul(front, head))
    ears = g.mul(g.mul(g.sstep(ax, ex + 0.030, ex + 0.046), g.gauss(z, ez - 0.005, 0.032)), g.mul(g.gauss(y, ey + 0.105, 0.035), head))
    neck = g.mul(g.sstep(z, cz - 0.11, cz - 0.03), g.mul(g.sstep(z, cz + 0.005, cz - 0.015), g.sstep(ax, 0.10, 0.07)))
    elbow = g.mul(g.mul(g.gauss(ax, 0.47, 0.038), g.sstep(y, 0.06, 0.10)), armz)
    knee = g.mul(g.mul(g.gauss(z, 0.505, 0.045), g.sstep(y, 0.015, -0.03)), g.sstep(ax, 0.22, 0.18))
    jaw = g.mul(g.mul(g.sstep(z, ez - 0.030, ez - 0.058), g.sstep(y, ey + 0.115, ey + 0.075)),
                g.mul(head, g.mul(g.inv(g.clamp(g.mul(mouth, 1.6))), g.inv(g.clamp(g.mul(nose, 1.4))))))
    # ---- colour zones and variation
    red = g.clamp(g.add(g.add(g.mul(cheek, 0.60), g.mul(nose, 0.70)), g.add(g.add(g.mul(ears, 0.7), g.mul(chin, 0.25)),
                  g.add(g.mul(hand, 0.32), g.add(g.mul(elbow, 0.6), g.mul(knee, 0.6))))))
    col = g.mix(g.mul(red, 0.42), col, (1.20, 0.84, 0.80), 'MULTIPLY')
    col = g.mix(g.mul(brow, 0.35), col, (1.07, 1.04, 0.96), 'MULTIPLY')
    col = g.mix(g.mul(socket, 0.30), col, (0.82, 0.76, 0.84), 'MULTIPLY')
    m1 = g.noise(46.0, 3, 0.6, 0.4)
    m2 = g.noise(9.0, 4, 0.6, 0.5)
    col = g.mix(g.lin(m1, 0.35, 0.72, 0.0, 0.34), col, (1.12, 0.92, 0.89), 'MULTIPLY')
    col = g.mix(g.lin(m2, 0.3, 0.7, 0.0, 0.28), col, (0.95, 0.99, 0.91), 'MULTIPLY')
    col = g.mix(g.lin(m2, 0.7, 0.35, 0.0, 0.18), col, (0.90, 0.88, 0.88), 'MULTIPLY')
    # ---- micro relief: crease network (two scales, finer on the face), pores
    netf = g.vor(840.0, 'DISTANCE_TO_EDGE')
    netb = g.vor(430.0, 'DISTANCE_TO_EDGE')
    gr1 = g.mixf(head, g.sstep(netb, 0.10, 0.0), g.sstep(netf, 0.10, 0.0))
    gr2 = g.sstep(g.vor(185.0, 'DISTANCE_TO_EDGE', vec=g.mapping(loc=(0.3, 0.6, 0.2))), 0.05, 0.0)
    pvf = g.vor(1250.0, 'F1')
    pvb = g.vor(650.0, 'F1')
    pit = g.mixf(head, g.sstep(pvb, 0.30, 0.10), g.sstep(pvf, 0.32, 0.10))
    pk = g.mul(g.add(0.35, g.add(g.mul(nose, 1.3), g.add(g.mul(cheek, 1.0), g.mul(chin, 0.6)))), pores)
    # the coarse network only shows where skin is thick and creased (hands, elbows, knees); the face keeps the fine one
    coarse = g.add(0.12, g.mul(g.clamp(g.add(g.add(elbow, knee), g.mul(hand, 0.8))), 0.88))
    gr2 = g.mul(gr2, coarse)
    gr1 = g.mul(gr1, g.lin(g.noise(60.0, 2, 0.5), 0.3, 0.7, 0.35, 1.0))
    height = g.mul(g.add(g.mul(gr1, g.mixf(head, 0.00017, 0.00011)), g.add(g.mul(gr2, 0.00024), g.mul(g.mul(pit, pk), 0.00020))),
                   -R * pores)
    height = g.add(height, g.mul(g.sub(g.noise(900.0, 2, 0.6), 0.5), 0.00014 * R))
    col = g.mix(g.mul(g.max(g.mul(gr1, 0.4), gr2), 0.14), col, (0.74, 0.64, 0.60), 'MULTIPLY')
    col = g.mix(g.mul(g.mul(pit, pk), 0.20), col, (0.68, 0.57, 0.52), 'MULTIPLY')

    def ridged(n, p=2.0):
        return g.pow(g.inv(g.abs(g.madd(n, 2.0, -1.0))), p)
    # ---- wrinkles: forehead, neck rings, knuckles / finger joints, crumpled elbows and knees
    fl = ridged(g.noise(1.0, 2, 0.5, 0.5, vec=g.mapping(scale=(9.0, 9.0, 150.0))), 3.0)
    nl = ridged(g.noise(1.0, 2, 0.5, 0.4, vec=g.mapping(scale=(6.0, 6.0, 105.0))), 3.0)
    kl = ridged(g.noise(1.0, 2, 0.5, 0.6, vec=g.mapping(scale=(310.0, 34.0, 34.0))), 2.2)
    cr = ridged(g.noise(62.0, 2, 0.55, 1.1), 2.5)
    wr = g.add(g.add(g.mul(g.mul(fl, brow), 0.00028 * age), g.mul(g.mul(nl, neck), 0.00040 * age)),
               g.add(g.mul(g.mul(kl, g.max(fingers, g.mul(hand, 0.45))), 0.00042), g.mul(g.mul(cr, g.clamp(g.add(elbow, knee))), 0.00060)))
    height = g.sub(height, g.mul(wr, R))
    rough_zone = g.clamp(g.add(g.add(elbow, knee), g.mul(fingers, 0.6)))
    col = g.mix(g.mul(rough_zone, 0.22), col, (0.80, 0.74, 0.72), 'MULTIPLY')
    # ---- freckles, moles
    fv = g.node('ShaderNodeTexVoronoi', feature='F1')
    fv.inputs['Scale'].default_value = 330.0
    g.L.new(g.pos, fv.inputs['Vector'])
    frz = g.clamp(g.add(g.add(g.mul(cheek, 1.2), g.mul(nose, 1.0)), g.add(g.mul(g.mul(g.up(), g.sstep(z, 1.30, 1.42)), 0.8), g.mul(armz, 0.5))))
    frk = g.mul(g.mul(g.sstep(fv.outputs['Distance'], 0.24, 0.10), g.sstep(g.lum(fv.outputs['Color']), 0.52, 0.66)), frz)
    col = g.mix(g.mul(frk, 0.42), col, (0.62, 0.48, 0.38), 'MULTIPLY')
    mv = g.node('ShaderNodeTexVoronoi', feature='F1')
    mv.inputs['Scale'].default_value = 48.0
    g.L.new(g.pos, mv.inputs['Vector'])
    mole = g.mul(g.sstep(mv.outputs['Distance'], 0.050, 0.030), g.sstep(g.lum(mv.outputs['Color']), 0.62, 0.66))
    col = g.mix(g.mul(mole, 0.75), col, (0.36, 0.24, 0.18), 'MULTIPLY')
    height = g.add(height, g.mul(mole, 0.00030 * R))
    # ---- veins (forearms, backs of the hands)
    vn = g.noise(1.0, 2, 0.5, 1.6, vec=g.mapping(scale=(8.0, 27.0, 27.0)))
    vd = g.abs(g.sub(vn, 0.5))
    vz = g.mul(g.sstep(ax, 0.48, 0.58), armz)
    col = g.mix(g.mul(g.mul(g.sstep(vd, 0.030, 0.0), vz), 0.30), col, (0.80, 0.93, 1.02), 'MULTIPLY')
    height = g.add(height, g.mul(g.mul(g.sstep(vd, 0.05, 0.0), vz), 0.00040 * R))
    # ---- stubble
    if stubble > 0:
        sv = g.vor(1350.0, 'F1')
        dots = g.mul(g.sstep(sv, 0.34, 0.12), jaw)
        col = g.mix(g.mul(jaw, 0.22 * stubble), col, (0.78, 0.82, 0.86), 'MULTIPLY')
        col = g.mix(g.mul(dots, 0.60 * stubble), col, (0.30, 0.28, 0.27), 'MULTIPLY')
        height = g.add(height, g.mul(dots, 0.00016 * R * stubble))
    # ---- old scars: pale, shiny, raised edges round a sunken line
    scar = None
    if scars > 0:
        sn = g.noise(1.0, 1, 0.5, 0.35, vec=g.mapping(scale=(7.0, 7.0, 31.0), rot=(0.5, 0.3, 0.4)))
        sz = g.mul(g.sstep(g.noise(3.1, 2, 0.5), 0.65, 0.70), min(1.0, scars * 1.6))
        core = g.mul(g.band(sn, 0.5, 0.010), sz)
        wide = g.mul(g.band(sn, 0.5, 0.030), sz)
        scar = wide
        col = g.mix(g.mul(wide, 0.55), col, (1.30, 1.10, 1.08), 'MULTIPLY')
        col = g.mix(g.mul(core, 0.45), col, (1.05, 0.70, 0.68), 'MULTIPLY')
        height = g.add(height, g.sub(g.mul(wide, 0.00040 * R), g.mul(core, 0.00042 * R)))
    # ---- roughness: painted map, oilier T-zone and lips, drier joints, matte creases
    rgh = g.madd(g.lum(tr.outputs['Color']), 0.90, rough_add + 0.02)
    tz = g.clamp(g.add(g.add(g.mul(brow, 0.8), g.mul(nose, 1.0)), g.mul(chin, 0.6)))
    rgh = g.sub(rgh, g.add(g.mul(tz, 0.15), g.mul(mouth, 0.16)))
    rgh = g.add(rgh, g.add(g.mul(rough_zone, 0.13), g.add(g.mul(g.max(g.mul(gr1, 0.5), gr2), 0.09), g.mul(g.mul(pit, pk), 0.08))))
    rgh = g.add(rgh, g.mul(g.sub(m1, 0.5), 0.10))
    if scar is not None:
        rgh = g.sub(rgh, g.mul(scar, 0.12))
    # ---- ember welts: charred crust, swollen rim, inflamed halo
    hd = g.node('ShaderNodeAttribute', attribute_name='heat_d').outputs['Fac']
    bn = g.noise(120.0, 3, 0.7)
    bn2 = g.noise(24.0, 3, 0.6, 0.6)
    gate = g.sstep(hd, 0.085, 0.040)                 # coarse (per-vertex) region round the welts
    near = g.clamp(g.add(g.mul(cav, gate), g.mul(g.mul(g.sub(bn2, 0.5), 0.30), gate)))      # baked contact mask, ragged
    char = g.mul(g.sstep(near, 0.40, 0.72), g.lin(bn, 0.25, 0.7, 0.6, 1.0))
    infl = g.mul(g.clamp(g.add(g.mul(g.sstep(near, 0.03, 0.40), 0.85), g.mul(gate, 0.30))), g.inv(char))
    col = g.mix(g.mul(infl, 0.62), col, (1.30, 0.66, 0.56), 'MULTIPLY')
    col = g.mix(g.mul(g.sstep(near, 0.12, 0.45), 0.40), col, (1.25, 0.62, 0.30), 'MULTIPLY')        # warm bounce of the glow
    col = g.mix(g.mul(char, 0.92), col, g.mix(bn, (0.012, 0.009, 0.008), (0.055, 0.030, 0.020)))
    rim = g.mul(g.band(near, 0.34, 0.20), gate)
    height = g.add(height, g.add(g.mul(rim, 0.00090 * R), g.mul(g.mul(char, g.sub(bn, 0.5)), 0.0012 * R)))
    rgh = g.mixf(char, g.sub(rgh, g.mul(infl, 0.12)), g.lin(bn, 0.3, 0.7, 0.95, 0.62))
    # ---- grime: dirt in contact cavities and in the creases of hands, feet, neck; ash dust
    col, rgh, gm = g.grime(col, rgh, grime * 1.2, dirt=(0.060, 0.042, 0.030), rough_to=0.85)
    dz = g.clamp(g.add(g.add(g.mul(hand, 0.55), g.mul(g.sstep(z, 0.40, 0.02), 0.9)), g.add(g.mul(neck, 0.25), g.mul(rough_zone, 0.3))))
    dm = g.mul(g.mul(dz, g.lin(g.noise(28.0, 4, 0.65), 0.3, 0.7, 0.15, 1.0)), g.add(0.45, g.mul(g.max(gr1, gr2), 0.9)))
    col = g.mix(g.clamp(g.mul(dm, 0.75 * grime)), col, (0.52, 0.44, 0.38), 'MULTIPLY')
    rgh = g.add(rgh, g.mul(dm, 0.10 * grime))
    col, rgh, _m, _a = g.dust(col, rgh, 0.30 * grime, 0.45)
    g.finish(col, g.clamp(rgh), 0.0, height, normal=nm.outputs[0])
    SRCINFO[key] = dict(color=(0.5, 0.35, 0.28), rough=0.6, metal=0.0)
    return key


def eye_src(key, cpath, npath, uv):
    """Eyeball: source eye texture, wet (low roughness), shaded towards the lids."""
    m, g = _new(key)
    uvn = g.node('ShaderNodeUVMap')
    uvn.uv_map = uv
    tc = _uvtex(g, uvn, cpath, 'sRGB')
    col = tc.outputs['Color']
    e, cav, opn = g.masks()
    col = g.mix(g.mul(g.sstep(cav, 0.2, 0.8), 0.45), col, (0.75, 0.55, 0.52), 'MULTIPLY')
    nm = None
    if npath and os.path.isfile(npath):
        tn = _uvtex(g, uvn, npath, 'Non-Color')
        n = g.node('ShaderNodeNormalMap')
        n.uv_map = uv
        g.L.new(tn.outputs['Color'], n.inputs['Color'])
        nm = n.outputs[0]
    g.finish(col, 0.10, 0.0, None, normal=nm)
    SRCINFO[key] = dict(color=(0.4, 0.4, 0.4), rough=0.1, metal=0.0)
    return key


def hair_src(key, cpath, npath, uv, tint, gain=1.0, rough=0.5, center=(0.0, 0.0), relief=1.0):
    """Hair: the sculpted lock shading of the source texture (as value), tinted, with real strand structure on top:
    strands radiate from the crown and run down the head (cylindrical coordinates round `center`), each with its own
    lightness, a few grey / sun-bleached ones, strong strand relief and strand-wise roughness."""
    m, g = _new(key)
    R = relief
    uvn = g.node('ShaderNodeUVMap')
    uvn.uv_map = uv
    tc = _uvtex(g, uvn, cpath, 'Non-Color')
    tn = _uvtex(g, uvn, npath, 'Non-Color')
    nm = g.node('ShaderNodeNormalMap')
    nm.uv_map = uv
    nm.inputs['Strength'].default_value = 1.4
    g.L.new(tn.outputs['Color'], nm.inputs['Color'])
    e, cav, opn = g.masks()
    x, y, z = g.xyz()
    th = g.math('ARCTAN2', g.sub(x, center[0]), g.sub(y, center[1]))
    v1 = g.comb(g.mul(th, 34.0), g.mul(z, 5.0), 0.0)
    v2 = g.comb(g.mul(th, 88.0), g.mul(z, 9.0), 3.7)
    s1 = g.noise(1.0, 2, 0.55, 0.25, vec=v1)
    s2 = g.noise(1.0, 2, 0.6, 0.2, vec=v2)
    st = g.add(g.mul(s1, 0.62), g.mul(s2, 0.38))
    lum = g.pow(g.lin(g.lum(tc.outputs['Color']), 0.25, 0.85, 0.05, 1.0), 2.4)
    tl = _scale(tint, gain * 2.6)
    col = g.scale_c(tl, g.mul(lum, g.lin(st, 0.25, 0.75, 0.58, 1.38)))
    pale = g.sstep(s2, 0.70, 0.78)                                             # stray bleached / grey strands
    col = g.mix(g.mul(pale, 0.5), col, _scale(_mixc(tint, (0.5, 0.47, 0.42), 0.5), gain * 3.2))
    col = g.mix(g.mul(g.sstep(cav, 0.15, 0.7), 0.6), col, _scale(tint, gain * 0.35))
    height = g.add(g.mul(g.sub(s1, 0.5), 0.0011 * R), g.mul(g.sub(s2, 0.5), 0.0006 * R))
    rgh = g.lin(st, 0.25, 0.75, min(1.0, rough + 0.22), max(0.2, rough - 0.2))
    g.finish(col, g.clamp(rgh), 0.0, height, normal=nm.outputs[0])
    SRCINFO[key] = dict(color=_scale(tint, gain), rough=rough, metal=0.0)
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
                   metal=0.0, emis=None, strength=0.0, double_sided=False, occlusion=True, normal_strength=1.0,
                   emis_path=None):
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
        if emis_path:
            t = nt.nodes.new('ShaderNodeTexImage')
            t.image = load_img(emis_path, 'sRGB')
            t.extension = 'EXTEND'
            nt.links.new(t.outputs['Color'], bs.inputs['Emission Color'])
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


def blur(a, r):
    """separable box blur (radius r px, 2 passes ~ gaussian) of (H, W[, C]); edges clamped"""
    if r <= 0:
        return a
    out = a.astype(np.float32)
    k = 2 * r + 1
    for _ in range(2):
        for ax in (0, 1):
            pad = [(0, 0)] * out.ndim
            pad[ax] = (r + 1, r)
            c = np.cumsum(np.pad(out, pad, mode='edge'), axis=ax, dtype=np.float64)
            hi = np.take(c, np.arange(k, k + out.shape[ax]), axis=ax)
            lo = np.take(c, np.arange(0, out.shape[ax]), axis=ax)
            out = ((hi - lo) / k).astype(np.float32)
    return out
