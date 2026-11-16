"""Look library v2 (object-space shader recipes) + atlas unwrap / two-stage bake + export-material assignment.

What changed against looks_v1.py (kept next to this file for reference) -- same function / kind / parameter names,
so creature scripts and the look kinds registered by kit_b / kit_c / kit_ice keep working unchanged:

* scanned CC0 PBR sets (montex.py, prepared by montex_prep.py) are sampled box-projected in object space at life-size
  texel scale: NB.scan(alias, size, relief) -> albedo detail (multiplies the look colour), roughness, height (mm);
* every kind composes a METRIC height field (millimetres): scan micro relief + procedural pores / wrinkles / weave /
  scratches + a 2-6 cm mid-frequency form layer (cloth folds, hammer facets, muscle lumps, rock plates). The Bump node
  runs at strength 1, so the baked tangent normals are what a bake from a displaced high-res copy would give;
* stage 1 of the bake renders geometry masks with Cycles AO nodes (wide AO, tight cavity AO, "inside" AO = convex
  edge) into a 1024 px map; the looks read them back through NB.ao() / NB.cavity() / NB.edge() for crevice grime and
  chipped edge wear (NB.pointy() is redirected to the baked edge mask, the per-vertex Pointiness is no longer used);
* a generic finisher adds cavity grime and slow colour drift to every look, and ENHANCE hooks add scans and
  mid-frequency relief to the externally registered kinds (wraps, glacier, crystal, burlap, sand);
* 2048 px atlas, textures written as JPEG through the bundled OpenImageIO (normal map q95 and ORM q88 with 4:4:4
  chroma, base colour / emission q90 4:2:0) and embedded as-is, tangents exported;
* emissive looks bake an emission texture that carries the emission COLOUR x a pattern (hot core, cooled crust
  islands on lava / ember looks); it is exported with a white emissiveFactor, which is what the game's
  CharModel.Convert / char.gdshader expect (they ADD the emission texture and the emission colour).
"""
import bpy, math, os, warnings
import numpy as np
from mathutils import Vector
import monlib as M
import montex as MT

warnings.filterwarnings("ignore", category=DeprecationWarning)

DIST = 0.004          # bump distance (m) of the v2 kinds: their returned 'height' is in units of DIST (wrappers add to it)
LEGACY_GAIN = 2.2     # relief gain for kinds that still return (strength < 1, distance) bumps (kit_b / kit_c / kit_ice)
HEM_RANGE = 0.05      # the 'hem_d' vertex attribute stores 1 - distance_to_open_border / HEM_RANGE


# ------------------------------------------------------------------------------------------------ images
def _mask_placeholder():
    im = bpy.data.images.get("mon_masks")
    if im is None:
        im = bpy.data.images.new("mon_masks", 8, 8, alpha=False)
        im.colorspace_settings.name = 'Non-Color'
        a = np.empty((64, 4), dtype=np.float32)
        a[:] = (1.0, 0.0, 0.0, 1.0)      # AO open, no cavity, no edge
        im.pixels.foreach_set(a.ravel())
    return im


def _slope_rms(h):
    n = h.shape[0]
    gx = (np.roll(h, -1, 1) - h) * n
    gy = (np.roll(h, -1, 0) - h) * n
    return float(np.sqrt((gx ** 2 + gy ** 2).mean()))


def scan_images(alias, res):
    """(detail image, height/rough/ao image, meta) of a prepared scan at res px; float buffers, Non-Color"""
    nd = "mt_%s_%d_d" % (alias, res)
    nh = "mt_%s_%d_h" % (alias, res)
    d = MT.load(alias)
    imd = bpy.data.images.get(nd)
    imh = bpy.data.images.get(nh)
    if imd is None or imh is None:
        f = d['h'].shape[0] // res
        det = MT.box_down(d['det'], f)
        h = MT.box_down(d['h'], f)
        r = MT.box_down(d['rough'], f)
        ao = MT.box_down(d['ao'], f)
        a = np.ones((res, res, 4), dtype=np.float32)
        a[:, :, :3] = det
        imd = bpy.data.images.new(nd, res, res, alpha=False, float_buffer=True)
        imd.colorspace_settings.name = 'Non-Color'
        imd.pixels.foreach_set(a.ravel())
        b = np.ones((res, res, 4), dtype=np.float32)
        b[:, :, 0] = h; b[:, :, 1] = r; b[:, :, 2] = ao
        imh = bpy.data.images.new(nh, res, res, alpha=False, float_buffer=True)
        imh.colorspace_settings.name = 'Non-Color'
        imh.pixels.foreach_set(b.ravel())
        imh['slope_rms'] = _slope_rms(h)
        imh['rough_mean'] = float(r.mean())
        imh['h_std'] = float(h.std())
    return imd, imh, d['meta']


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
        self._mk = None
        self._hem = None
        self._scans = {}

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

    def scan(self, alias, size=None, relief=None, blend=0.2, loc=(0.0, 0.0, 0.0), res=None, gain=1.0):
        """scanned PBR set, box-projected in object space; one tile = `size` metres (default: its real size).
        relief: target rms slope of the height (None = the scan's own relief * gain).
        -> dict(det=colour (mean 1), lum, h=height in MILLIMETRES, hn=height / its std (local relief: < 0 = pore, crack,
        gap between threads), rough (0..1), dr (rough - mean), ao, meta). Missing sets fall back (montex.FALLBACK)"""
        key = (alias, size, relief, blend, tuple(loc), res, gain)
        if key in self._scans:
            return self._scans[key]
        alias = MT.resolve(alias)
        meta0 = MT.SETS[alias]
        size = float(meta0['size'] if size is None else size)
        if res is None:
            res = 1024 if size >= 0.85 else (512 if size >= 0.42 else 256)
        imd, imh, meta = scan_images(alias, res)
        vec = self.mapping(self.coord(), scale=(1.0 / size,) * 3, loc=tuple(loc))
        nd = self.new('ShaderNodeTexImage', projection='BOX', projection_blend=blend, interpolation='Linear')
        nd.image = imd
        self.set(nd.inputs['Vector'], vec)
        nh = self.new('ShaderNodeTexImage', projection='BOX', projection_blend=blend, interpolation='Cubic')
        nh.image = imh
        self.set(nh.inputs['Vector'], vec)
        sp = self.new('ShaderNodeSeparateColor')
        self.set(sp.inputs[0], nh.outputs[0])
        k = gain if relief is None else relief / max(float(imh['slope_rms']), 1e-6)
        h = self.math('MULTIPLY', sp.outputs[0], size * 1000.0 * k)
        hn = self.math('MULTIPLY', sp.outputs[0], 1.0 / max(float(imh['h_std']), 1e-9))
        out = dict(det=nd.outputs[0], lum=self.lum(nd.outputs[0]), h=h, hn=hn, rough=sp.outputs[1],
                   dr=self.math('SUBTRACT', sp.outputs[1], float(imh['rough_mean'])), ao=sp.outputs[2], meta=meta)
        self._scans[key] = out
        return out

    # --- baked geometry masks (stage 1 of the bake)
    def _masks(self):
        if self._mk is None:
            n = self.new('ShaderNodeTexImage', interpolation='Linear')
            n.name = 'MASK'
            n.image = _mask_placeholder()
            s = self.new('ShaderNodeSeparateColor')
            self.set(s.inputs[0], n.outputs[0])
            self._mk = (s.outputs[0], s.outputs[1], s.outputs[2])
        return self._mk

    def ao(self):
        """wide ambient occlusion (1 = open)"""
        return self._masks()[0]

    def cavity(self, lo=0.0, hi=1.0):
        """tight-AO crevice mask: 0 = open surface .. 1 = deep in a crease / contact line"""
        c = self._masks()[1]
        return c if (lo == 0.0 and hi == 1.0) else self.mapr(c, lo, hi)

    def edge(self, lo=0.28, hi=0.6, breakup=0.3, scale=90.0):
        """convex-edge wear mask from the baked 'inside' AO, chipped by fine noise"""
        e = self._masks()[2]
        if breakup:
            n = self.noise(scale, 3, 0.65)
            e = self.math('ADD', e, self.math('MULTIPLY', self.math('SUBTRACT', n, 0.5), breakup))
        return self.mapr(e, lo, hi)

    def pointy(self, lo=0.47, hi=0.56):
        """legacy name (was Geometry > Pointiness): now the baked convex-edge mask with the same call signature"""
        return self.edge(0.26 + (lo - 0.5) * 1.5, 0.58 + (hi - 0.6) * 1.5)

    def hem(self):
        """distance (m, 0 .. HEM_RANGE) from the nearest open mesh border (cloth hems, torn edges)"""
        if self._hem is None:
            a = self.new('ShaderNodeAttribute', attribute_type='GEOMETRY', attribute_name='hem_d')
            self._hem = self.math('MULTIPLY', self.math('SUBTRACT', 1.0, a.outputs['Fac']), HEM_RANGE)
        return self._hem

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

    def lum(self, col):
        n = self.new('ShaderNodeRGBToBW')
        self.set(n.inputs[0], col)
        return n.outputs[0]

    def detail(self, col, det, amt=1.0):
        """multiply a colour by a scan's albedo detail (mean 1) at strength amt"""
        d = self.mix(amt, (1.0, 1.0, 1.0), det)
        return self.mix(1.0, col, d, 'MULTIPLY')

    def shade(self, col, mask, k):
        """darken (k < 1) or lighten (k > 1) a colour where mask = 1"""
        return self.mix(mask, col, self.mul(col, k))

    def hsum(self, terms):
        """sum of (socket, weight) pairs -> socket (heights in mm)"""
        out = None
        for s, w in terms:
            t = s if w == 1.0 else self.math('MULTIPLY', s, w)
            out = t if out is None else self.math('ADD', out, t)
        return out

    def sep(self, vec):
        n = self.new('ShaderNodeSeparateXYZ')
        self.set(n.inputs[0], vec)
        return n.outputs[0], n.outputs[1], n.outputs[2]

    def upfacing(self, lo=0.35, hi=0.9):
        _, _, z = self.sep(self.geo().outputs['Normal'])
        return self.mapr(z, lo, hi)

    def height_z(self, lo, hi):
        """0 at z<=lo .. 1 at z>=hi (object space)"""
        _, _, z = self.sep(self.coord())
        return self.mapr(z, lo, hi)

    def arm_mask(self):
        """1 on the arms of the T-pose (|x| large, shoulder height): lets cloth folds run round the sleeve"""
        x, _, z = self.sep(self.coord())
        return self.math('MULTIPLY', self.mapr(self.math('ABSOLUTE', x), 0.24, 0.34), self.mapr(z, 1.18, 1.32))


def _c(p, k, default):
    return p.get(k, default)


def _out(N, p, c, rough, metal, H, default_bump, **extra):
    """pack a v2 kind result: H = list of (socket, mm) height terms"""
    mult = _c(p, 'bump', default_bump) / default_bump
    h = N.math('MULTIPLY', N.hsum(H), 0.001 / DIST * mult) if H else None
    d = dict(color=c, rough=rough, metal=metal, height=h, bump=(1.0, DIST), v2=True)
    d.update(extra)
    return d


def _lumc(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


# ------------------------------------------------------------------------------------------------ look kinds
# every kind returns dict(color, rough, metal, height, bump=(strength, distance)[, emit, emask])

def k_bone(N, p):
    """old bone / horn / tooth / mask: porous (coral-stone scan), hairline cracks, yellowed and stained, dirt in
    recesses, edges rubbed pale and smooth"""
    col = _c(p, 'color', (0.56, 0.50, 0.38))
    stain = _c(p, 'stain', (0.20, 0.14, 0.08))
    big = N.noise(_c(p, 'blotch', 7.0), 4, 0.6)
    mid = N.noise(26, 3, 0.6)
    por = N.scan('coral', size=0.55, relief=_c(p, 'pore', 0.36))
    fin = N.scan('plaster', size=0.45, relief=0.14)
    crack = N.voro(_c(p, 'crack', 26.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.04, 9))
    crack_m = N.mapr(crack, 0.0, 0.05, 1.0, 0.0)
    hair = N.mapr(N.voro(_c(p, 'crack', 26.0) * 2.3, 'DISTANCE_TO_EDGE', vec=N.warp(0.05, 14)), 0.0, 0.05, 1.0, 0.0)
    hair = N.math('MULTIPLY', hair, N.mapr(N.noise(6, 2, 0.5), 0.45, 0.6))
    ca = _c(p, 'crack_amt', 0.6)
    # pores open up in patches (cancellous areas), the rest stays dense
    pm = N.mapr(N.noise(11, 3, 0.6), 0.35, 0.7, 0.35, 1.0)
    c = N.mix(N.mapr(big, 0.42, 0.68), col, stain)
    c = N.mix(N.mapr(mid, 0.5, 0.75, 0.0, 0.4), c, N.mix(0.5, col, stain))
    c = N.detail(c, por['det'], 0.8)
    c = N.shade(c, N.mapr(por['hn'], -0.3, -1.6, 0.0, 0.6), 0.45)            # dirt inside the pores
    c = N.mix(N.math('MULTIPLY', crack_m, ca), c, N.mul(stain, 0.5))
    c = N.mix(N.math('MULTIPLY', hair, ca * 0.8), c, N.mul(stain, 0.6))
    edge = N.pointy()
    c = N.mix(N.math('MULTIPLY', edge, 0.4), c, N.mix(0.6, col, (0.78, 0.74, 0.62)))
    grime = N.height_z(_c(p, 'dirt_lo', -1.0), _c(p, 'dirt_hi', -0.5))  # 0 near ground
    c = N.mix(N.math('SUBTRACT', 1.0, grime), c, N.mix(0.6, c, _c(p, 'dirt', (0.10, 0.085, 0.07))))
    rough = N.math('ADD', N.mapr(big, 0.3, 0.8, 0.5, 0.82), N.math('MULTIPLY', por['dr'], 0.5))
    rough = N.math('ADD', rough, N.math('MULTIPLY', N.math('SUBTRACT', mid, 0.5), 0.3))
    rough = N.mixf(N.math('MULTIPLY', edge, 0.7), rough, 0.34, )
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.95), 0.25)
    H = [(N.math('MULTIPLY', por['h'], pm), 1.0), (fin['h'], 1.0),
         (N.math('MULTIPLY', crack_m, ca), -0.55), (N.math('MULTIPLY', hair, ca), -0.25),
         (N.noise(30, 2, 0.5), 0.9)]
    return _out(N, p, c, rough, 0.0, H, 0.35)


def k_metal(N, p):
    """forged, worn / corroded metal. color = clean metal, rust = corrosion colour, amount 0..1.
    hammer facets and dents, scratches, pitting, rust creeping out of the cavities, bright rubbed edges"""
    col = _c(p, 'color', (0.22, 0.21, 0.20))
    rust = _c(p, 'rust', (0.23, 0.095, 0.035))
    rust2 = _c(p, 'rust2', (0.09, 0.04, 0.025))
    amt = _c(p, 'amount', 0.5)
    r0 = _c(p, 'rough', 0.42)
    P = N.coord()
    big = N.noise(_c(p, 'scale', 8.0), 5, 0.68)
    mid = N.noise(27, 3, 0.6)
    cav = N.cavity()
    plate = N.scan('bronze', size=0.6, relief=0.13)
    rs = N.scan('iron', size=0.8, relief=0.4)
    lo = 0.755 - amt * 0.5
    field = N.math('ADD', big, N.math('ADD', N.math('MULTIPLY', cav, 0.14), N.math('MULTIPLY', N.math('SUBTRACT', mid, 0.5), 0.14)))
    mask = N.mapr(field, lo, lo + 0.10)
    pits = N.mapr(N.voro(75, 'F1'), 0.0, 0.32, 1.0, 0.0)
    mask = N.math('MAXIMUM', mask, N.math('MULTIPLY', pits, N.mapr(field, lo - 0.2, lo)))
    edge = N.math('MULTIPLY', N.edge(0.26, 0.56, 0.35), _c(p, 'edge', 0.8))
    mask = N.math('MULTIPLY', mask, N.math('SUBTRACT', 1.0, N.math('MULTIPLY', edge, 0.8)))     # edges are rubbed clean
    # hammer / planishing facets, sparse dents, scratches in three directions
    ham = N.voro(_c(p, 'hammer', 38.0), 'SMOOTH_F1', vec=N.warp(0.02, 11), smooth=0.8)
    dv = N.voro(12, 'F1', vec=N.warp(0.02, 5))
    dent = N.math('MULTIPLY', N.mapr(dv, 0.0, 0.17, 1.0, 0.0), N.mapr(N.noise(5.0, 2, 0.5, vec=N.mapping(P, loc=(7.3, 1.1, 4.2))), 0.52, 0.6))
    sc = None
    for i, rt in enumerate(((0.4, 0.2, 0.9), (1.3, 0.5, 0.1), (0.2, 1.1, 2.0))):
        s = N.noise(7.0, 1, 0.5, vec=N.mapping(P, scale=(1, 17, 1), rot=rt, loc=(i * 3.7, i * 1.3, 0)))
        s = N.mapr(s, 0.665, 0.70)
        sc = s if sc is None else N.math('MAXIMUM', sc, s)
    scr = N.math('MULTIPLY', sc, N.mapr(N.noise(9, 3, 0.6, vec=N.mapping(P, loc=(2.2, 5.1, 0.3))), 0.35, 0.6))
    clean = N.math('SUBTRACT', 1.0, mask)
    # --- colour
    tint = N.noise(4.5, 2, 0.5, color=True, vec=N.mapping(P, loc=(1.7, 9.2, 3.3)))
    base = N.mix(N.mapr(mid, 0.3, 0.75, 0.0, 0.45), col, N.mul(col, 0.55))
    base = N.mix(0.22, base, N.mix(1.0, base, N.mix(0.5, tint, (0.5, 0.5, 0.5)), 'OVERLAY'))   # heat / oxide tints
    base = N.detail(base, plate['det'], 0.7)
    wear = N.scan('iron_scr', size=0.7, gain=1.0)
    base = N.detail(base, wear['det'], 0.45)
    base = N.shade(base, N.mapr(ham, 0.1, 0.55, 0.0, 0.25), 0.7)
    base = N.mix(N.math('MULTIPLY', scr, 0.6), base, _c(p, 'edge_color', (0.46, 0.45, 0.43)))
    rc = N.mix(N.mapr(N.noise(23, 3, 0.6), 0.3, 0.7), rust, rust2)
    rc = N.mix(N.mapr(N.noise(7.0, 3, 0.6, vec=N.mapping(P, loc=(0.7, 3.9, 6.2))), 0.42, 0.68, 0.0, 0.7), rc, N.mul(rust2, 0.55))   # old, dark scale
    rc = N.detail(rc, rs['det'], 1.0)
    rc = N.shade(rc, N.mapr(rs['hn'], -0.3, -1.6, 0.0, 0.6), 0.4)                         # pits in the rust are darker
    c = N.mix(mask, base, rc)
    c = N.mix(N.math('MULTIPLY', dent, 0.35), c, N.mul(rust2, 0.8))
    c = N.mix(edge, c, _c(p, 'edge_color', (0.46, 0.45, 0.43)))
    metal = N.math('MAXIMUM', N.math('SUBTRACT', 1.0, N.math('MULTIPLY', mask, 0.92)), edge)
    # --- roughness: smeared polish, hammered highs burnished, rust matte, scratches and dents rough, edges bright
    smear = N.noise(6.0, 3, 0.6, vec=N.mapping(P, scale=(1, 1, 0.3), loc=(4.4, 0.6, 2.9)))
    rough = N.math('ADD', N.mapr(smear, 0.25, 0.75, r0 - 0.14, r0 + 0.2), N.math('MULTIPLY', plate['dr'], 0.6))
    rough = N.math('ADD', rough, N.math('MULTIPLY', wear['dr'], 0.55))
    rough = N.math('ADD', rough, N.mapr(ham, 0.1, 0.6, -0.06, 0.08))
    rough = N.math('ADD', rough, N.math('MULTIPLY', scr, 0.22))
    rough = N.mixf(mask, rough, N.math('ADD', 0.86, N.math('MULTIPLY', rs['dr'], 0.4)))
    rough = N.mixf(edge, rough, 0.27)
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.97), 0.16)
    H = [(ham, 1.0), (dent, -1.5), (N.math('MULTIPLY', scr, clean), -0.16), (N.math('MULTIPLY', plate['h'], clean), 1.0),
         (N.math('MULTIPLY', rs['h'], mask), 1.0), (mask, 0.3), (N.math('MULTIPLY', pits, mask), -0.45),
         (N.noise(22, 2, 0.5), 0.8)]
    return _out(N, p, c, rough, metal, H, 0.4)


def _weave_for(col, p):
    w = p.get('weave')
    if w:
        return w
    lum = _lumc(col)
    if lum < 0.05:
        return 'wool'
    if col[0] > col[2] * 1.35 and lum < 0.3:
        return 'burlap'
    return 'linen'


# weave -> (tile size in m, rms slope).  The tiles are ~1.6x life size so one thread still covers 3-4 atlas texels
_WEAVE = dict(wool=(0.44, 0.26), linen=(0.45, 0.27), burlap=(0.45, 0.28), wool_weave=(0.5, 0.28), fleece=(0.6, 0.3))


def _cloth_folds(N, p, fs, fa):
    """mid-frequency form of hanging cloth: vertical drape folds, a diagonal tension set, sleeve rings, crumple.
    -> (list of height terms in mm, 0..1 fold field (0 = valley))"""
    P = N.coord()
    f1 = N.noise(fs, 2, 0.45, vec=N.mapping(N.warp(0.03, 3.0), scale=(1, 1, 0.16)))
    ring = N.noise(fs * 1.5, 2, 0.5, vec=N.mapping(N.warp(0.02, 4.0), scale=(1, 0.22, 0.22)))
    f = N.mapr(N.mixf(N.arm_mask(), f1, ring), 0.3, 0.7)
    f2 = N.mapr(N.noise(fs * 2.4, 2, 0.5, vec=N.mapping(P, scale=(1, 1, 0.3), rot=(0.3, 0.12, 0.0))), 0.3, 0.7)
    cr = N.voro(34, 'SMOOTH_F1', vec=N.warp(0.05, 7), smooth=0.7)
    amp = 110.0 / fs                      # mm: ~8 mm deep at a 7 cm fold spacing, 12 mm at 11 cm
    return [(f, amp * fa), (f2, 0.3 * amp * fa), (cr, 1.3 * fa)], f


def _hem_terms(N, strength=1.0):
    """stitched, doubled hem + a frayed outer edge along open mesh borders -> (height terms, stitch mask, fray mask, band)"""
    d = N.hem()
    band = N.mapr(d, 0.013, 0.017, 1.0, 0.0)                      # doubled cloth within ~15 mm of the border
    line = N.math('MULTIPLY', N.mapr(d, 0.0075, 0.0095), N.mapr(d, 0.0125, 0.0105))      # stitch row at ~10 mm
    dash = N.mapr(N.voro(150, 'F1'), 0.2, 0.34, 1.0, 0.0)
    st = N.math('MULTIPLY', line, dash)
    fray = N.math('MULTIPLY', N.mapr(d, 0.006, 0.0005), N.mapr(N.noise(170, 2, 0.6), 0.25, 0.6))
    H = [(band, 0.7 * strength), (st, -0.45 * strength), (N.math('MULTIPLY', fray, N.noise(240, 1, 0.5)), 0.9 * strength)]
    return H, st, fray, band


def k_cloth(N, p):
    """woven cloth: scanned weave (wool / linen / burlap chosen from the colour or `weave=`), drape folds, pilling,
    stitched hems and frayed borders, stains, drip streaks, dust on upward faces, dirt towards the hem"""
    col = _c(p, 'color', (0.16, 0.155, 0.15))
    dirt = _c(p, 'dirt', (0.12, 0.105, 0.09))
    r0 = _c(p, 'rough', 0.93)
    wname = _weave_for(col, p)
    wsize, wrel = _WEAVE.get(wname, (0.56, 0.28))
    wv = N.scan(wname, size=wsize * _c(p, 'weave_size', 1.0), relief=wrel * _c(p, 'weave_amt', 1.0), blend=0.12, res=512)
    big = N.noise(_c(p, 'scale', 5.0), 4, 0.6)
    mid = N.noise(23, 3, 0.6)
    stain = N.mapr(N.noise(2.6, 3, 0.55), 0.5, 0.75)
    HF, fold = _cloth_folds(N, p, _c(p, 'fold', 14.0), _c(p, 'fold_amt', 0.65) / 0.65)
    HH, stitch, fray, band = _hem_terms(N)
    c = N.mix(N.mapr(big, 0.3, 0.75, 0.0, 0.5), col, N.mul(col, 0.45))
    c = N.mix(N.mapr(mid, 0.52, 0.78, 0.0, 0.4), c, N.mix(0.35, N.mul(col, 1.5), (0.4, 0.38, 0.35)))    # faded, worn patches
    c = N.mix(N.math('MULTIPLY', stain, _c(p, 'stain_amt', 0.5)), c, N.mul(col, 0.3))
    c = N.detail(c, wv['det'], 0.7)
    c = N.shade(c, N.mapr(wv['hn'], -0.3, -1.8, 0.0, 0.3), 0.55)                                         # shadow between threads
    hem = N.math('SUBTRACT', 1.0, N.height_z(_c(p, 'hem_lo', 0.0), _c(p, 'hem_hi', 0.5)))
    hemn = N.math('MULTIPLY', hem, N.mapr(N.noise(9, 3, 0.6), 0.25, 0.7))
    c = N.mix(N.math('MULTIPLY', hemn, _c(p, 'hem_amt', 0.8)), c, dirt)
    # long vertical drip / wear streaks and fold shading baked into the colour
    streak = N.mapr(N.noise(21, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.07))), 0.5, 0.78)
    c = N.mix(N.math('MULTIPLY', streak, _c(p, 'streak', 0.45)), c, N.mul(col, 0.28))
    c = N.shade(c, N.mapr(fold, 0.55, 0.05, 0.0, _c(p, 'fold_shade', 0.35) * 1.5), 0.45)
    dust = N.math('MULTIPLY', N.upfacing(0.45, 0.95), N.mapr(N.noise(8, 3, 0.6), 0.3, 0.7))
    c = N.mix(N.math('MULTIPLY', dust, _c(p, 'dust', 0.3)), c, N.mix(0.5, dirt, (0.36, 0.33, 0.29)))
    pill = N.math('MULTIPLY', N.mapr(N.voro(230, 'F1'), 0.0, 0.3, 1.0, 0.0), N.mapr(N.noise(10, 2, 0.5), 0.5, 0.68))
    pale = N.mix(0.5, col, (0.45, 0.43, 0.4))
    c = N.mix(N.math('MULTIPLY', pill, 0.35), c, pale)
    edge = N.pointy(0.5, 0.62)
    c = N.mix(N.math('MULTIPLY', N.math('MAXIMUM', edge, fray), _c(p, 'fray', 0.3) * 1.6), c, pale)
    c = N.mix(N.math('MULTIPLY', stitch, 0.7), c, N.mul(pale, 1.2))
    rough = N.math('ADD', N.mapr(mid, 0.3, 0.75, r0 - 0.24, r0 + 0.05), N.math('MULTIPLY', wv['dr'], 0.6))
    rough = N.mixf(N.math('MULTIPLY', stain, 0.7), rough, r0 - 0.38)                                    # greasy stains
    rough = N.mixf(N.math('MULTIPLY', edge, 0.5), rough, r0 - 0.3)                                      # threadbare, polished wear
    rough = N.mixf(N.math('MULTIPLY', dust, 0.8), rough, 1.0)
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 1.0), 0.4)
    H = HF + HH + [(wv['h'], 1.0), (pill, 0.3), (N.noise(60, 2, 0.5), 0.5)]
    return _out(N, p, c, rough, 0.0, H, 0.45)


def k_ice(N, p):
    """glacier ice / icicles: pale surface, deep blue body, white frost on top and in cracks"""
    col = _c(p, 'color', (0.50, 0.68, 0.80))
    deep = _c(p, 'deep', (0.06, 0.17, 0.30))
    frost = _c(p, 'frost', (0.82, 0.88, 0.92))
    big = N.noise(_c(p, 'scale', 9.0), 4, 0.6)
    ic = N.scan('ice', size=0.7, relief=0.22)
    c = N.mix(N.mapr(big, 0.35, 0.7), deep, col)
    c = N.detail(c, ic['det'], 0.6)
    cr = N.voro(_c(p, 'crack', 20.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.08, 6))
    crack = N.mapr(cr, 0.0, 0.03, 1.0, 0.0)
    c = N.mix(N.math('MULTIPLY', crack, 0.7), c, frost)
    up = N.math('MULTIPLY', N.upfacing(0.2, 0.85), N.mapr(N.noise(14, 4, 0.7), 0.25, 0.65))
    fr = N.math('MAXIMUM', N.math('MULTIPLY', up, _c(p, 'frost_amt', 0.9)), N.math('MULTIPLY', N.pointy(0.5, 0.6), 0.6))
    c = N.mix(fr, c, frost)
    r0 = _c(p, 'rough', 0.12)
    rough = N.mixf(fr, N.math('ADD', N.mapr(big, 0.3, 0.7, r0, r0 + 0.2), N.math('MULTIPLY', ic['dr'], 0.5)), 0.75)
    rough = N.math('MAXIMUM', rough, 0.05)
    sn = N.scan('snow', size=0.9, relief=0.3)
    H = [(N.mapr(cr, 0.0, 0.06), 1.4), (ic['h'], 1.0), (N.math('MULTIPLY', sn['h'], fr), 1.0), (N.noise(24, 3, 0.6), 1.2)]
    return _out(N, p, c, rough, 0.0, H, 0.35)


def k_moss(N, p):
    """moss / wet vegetation / lichen clumps"""
    col = _c(p, 'color', (0.085, 0.12, 0.04))
    lite = _c(p, 'light', (0.19, 0.20, 0.07))
    dark = _c(p, 'dark', (0.025, 0.035, 0.018))
    big = N.noise(_c(p, 'scale', 10.0), 4, 0.65)
    clump = N.voro(_c(p, 'clump', 38.0), 'F1', vec=N.warp(0.05, 10))
    fl = N.scan('fleece', size=0.5, relief=0.4)
    c = N.mix(N.mapr(big, 0.35, 0.7), dark, col)
    c = N.mix(N.mapr(clump, 0.05, 0.45, _c(p, 'tips', 0.6), 0.0), c, lite)
    c = N.detail(c, fl['det'], 0.8)
    fine = N.noise(90, 3, 0.7)
    c = N.mix(N.mapr(fine, 0.3, 0.8, 0.0, 0.35), c, N.mul(c, 0.5))
    hang = N.noise(26, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.1)))
    rough = N.math('ADD', _c(p, 'rough', 0.95) - 0.1, N.math('MULTIPLY', fl['dr'], 0.4))
    H = [(N.mapr(clump, 0.0, 0.5, 1.0, 0.0), 3.5), (fl['h'], 1.0), (hang, 1.6), (fine, 0.4)]
    return _out(N, p, c, rough, 0.0, H, 0.8)


def k_mud(N, p):
    """wet mud / silt with glossy patches and drying, cracked crust on top"""
    col = _c(p, 'color', (0.065, 0.05, 0.035))
    dry = _c(p, 'dry', (0.17, 0.14, 0.10))
    big = N.noise(_c(p, 'scale', 6.0), 4, 0.65)
    wet = N.mapr(big, 0.45, 0.62)
    ck = N.scan('mud_crk', size=0.55, relief=0.5)
    gr = N.scan('burnt', size=0.6, relief=0.3)
    up = N.math('MULTIPLY', N.upfacing(0.1, 0.8), N.mapr(N.noise(8, 3, 0.6), 0.3, 0.65))
    c = N.mix(N.math('MULTIPLY', up, _c(p, 'dry_amt', 0.75)), col, dry)
    c = N.detail(c, N.mix(up, gr['det'], ck['det']), 0.9)
    c = N.shade(c, N.math('MULTIPLY', N.mapr(ck['hn'], -0.3, -1.6), up), 0.4)
    drip = N.noise(18, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.12)))
    c = N.mix(N.mapr(drip, 0.5, 0.8, 0.0, 0.45), c, N.mul(col, 0.45))
    rough = N.mixf(N.math('MULTIPLY', wet, N.math('SUBTRACT', 1.0, up)), 0.9, _c(p, 'wet_rough', 0.22))
    H = [(big, 4.0), (drip, 2.5), (N.math('MULTIPLY', ck['h'], up), 1.0),
         (N.math('MULTIPLY', gr['h'], N.math('SUBTRACT', 1.0, up)), 1.0), (N.noise(30, 2, 0.5), 1.5)]
    return _out(N, p, c, rough, 0.0, H, 0.7)


def k_fur(N, p):
    """shaggy pelt: strands hanging along -Z, lighter tips, optional frost on top"""
    col = _c(p, 'color', (0.13, 0.10, 0.075))
    tipc = _c(p, 'tip', (0.30, 0.25, 0.19))
    st = N.noise(_c(p, 'scale', 55.0), 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.06)))
    st2 = N.noise(22, 3, 0.6, vec=N.mapping(N.coord(), scale=(1, 1, 0.1)))
    lock = N.voro(30, 'SMOOTH_F1', vec=N.mapping(N.warp(0.04, 8), scale=(1, 1, 0.3)), smooth=0.6)
    fl = N.scan('fleece', size=0.55, relief=0.45)
    big = N.noise(5, 3, 0.6)
    c = N.mix(N.mapr(st, 0.35, 0.7), N.mul(col, 0.4), col)
    c = N.mix(N.mapr(st2, 0.5, 0.8, 0.0, 0.7), c, tipc)
    c = N.shade(c, N.mapr(lock, 0.5, 0.15, 0.0, 0.5), 0.45)          # dark partings between the locks
    c = N.detail(c, fl['det'], 0.7)
    c = N.mix(N.mapr(big, 0.4, 0.75, 0.0, 0.45), c, N.mul(c, 0.45))
    fr = _c(p, 'frost', 0.0)
    if fr > 0:
        up = N.math('MULTIPLY', N.upfacing(0.15, 0.8), N.mapr(N.noise(12, 4, 0.7), 0.25, 0.6))
        c = N.mix(N.math('MULTIPLY', up, fr), c, (0.80, 0.86, 0.90))
    rough = N.math('ADD', _c(p, 'rough', 0.9) - 0.12, N.mapr(st2, 0.3, 0.7, 0.0, 0.2))
    H = [(st, 1.4), (st2, 2.2), (N.math('SUBTRACT', 0.6, lock), 4.0), (fl['h'], 1.0)]
    return _out(N, p, c, rough, 0.0, H, 0.9)


def k_leather(N, p):
    """tanned leather / hide: scanned grain, creases, cracks in the dry patches, scuffs, burnished lighter edges,
    a stitch row just inside the edges"""
    col = _c(p, 'color', (0.10, 0.065, 0.04))
    scol = _c(p, 'scuff_color', (0.24, 0.19, 0.15))
    r0 = _c(p, 'rough', 0.5)
    gr = N.scan('leather', size=0.5, relief=0.3)
    old = N.scan('leather_old', size=0.6, relief=0.22)
    wv = N.warp(0.05, 7)
    cr = N.voro(_c(p, 'scale', 42.0), 'DISTANCE_TO_EDGE', vec=N.mapping(wv, scale=(1, 1, 1.7)))
    crease = N.mapr(cr, 0.0, 0.07, 1.0, 0.0)
    big = N.noise(6, 4, 0.6)
    mid = N.noise(25, 3, 0.6)
    drym = N.mapr(N.noise(7, 3, 0.6, vec=N.mapping(N.coord(), loc=(5.5, 2.5, 8.1))), 0.5, 0.68)
    ck = N.mapr(N.voro(95, 'DISTANCE_TO_EDGE', vec=N.warp(0.03, 18)), 0.0, 0.07, 1.0, 0.0)
    ck = N.math('MULTIPLY', ck, drym)
    scuff = N.mapr(N.noise(18, 4, 0.7), 0.58, 0.8)
    em = N._masks()[2]
    edge = N.pointy(0.5, 0.6)
    stitch = N.math('MULTIPLY', N.math('MULTIPLY', N.mapr(em, 0.10, 0.16), N.mapr(em, 0.27, 0.21)),
                    N.mapr(N.voro(150, 'F1'), 0.2, 0.34, 1.0, 0.0))
    stitch = N.math('MULTIPLY', stitch, _c(p, 'stitch', 1.0))
    c = N.mix(N.mapr(big, 0.3, 0.7, 0.0, 0.5), col, N.mul(col, 0.5))
    c = N.mix(N.mapr(mid, 0.5, 0.75, 0.0, 0.35), c, N.mix(0.4, col, scol))
    c = N.detail(c, gr['det'], 0.9)
    c = N.detail(c, old['det'], 0.6)
    c = N.shade(c, N.mapr(gr['hn'], -0.2, -1.6, 0.0, 0.5), 0.5)
    c = N.mix(N.math('MULTIPLY', crease, 0.6), c, N.mul(col, 0.25))
    c = N.mix(N.math('MULTIPLY', ck, 0.7), c, N.mul(col, 0.2))
    c = N.mix(N.math('MULTIPLY', scuff, _c(p, 'scuff', 0.45)), c, scol)
    c = N.mix(N.math('MULTIPLY', edge, 0.55), c, scol)
    c = N.mix(N.math('MULTIPLY', stitch, 0.7), c, N.mix(0.5, scol, (0.5, 0.45, 0.36)))
    hem = N.math('SUBTRACT', 1.0, N.height_z(_c(p, 'hem_lo', -1.0), _c(p, 'hem_hi', -0.5)))
    c = N.mix(N.math('MULTIPLY', hem, N.mapr(N.noise(8, 3, 0.6), 0.25, 0.7)), c, _c(p, 'dirt', (0.10, 0.085, 0.07)))
    rough = N.math('ADD', N.mapr(mid, 0.3, 0.72, r0 - 0.14, r0 + 0.16), N.math('MULTIPLY', gr['dr'], 0.6))
    rough = N.mixf(scuff, rough, 0.86)
    rough = N.mixf(N.math('MULTIPLY', edge, 0.7), rough, 0.3)                   # burnished edges
    rough = N.mixf(N.math('MAXIMUM', ck, crease), rough, 0.9)
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.97), 0.2)
    H = [(gr['h'], 1.0), (old['h'], 1.0), (crease, -0.55), (ck, -0.4), (stitch, 0.45), (scuff, -0.12),
         (N.noise(21, 2, 0.5), 1.4)]
    return _out(N, p, c, rough, 0.0, H, 0.4)


def k_skin(N, p):
    """(dead) flesh: scanned hide grain for pores, cross-hatched fine wrinkle lines, deep creases in the joints,
    sinews and veins standing out under thin skin, bruised mottling, dry cracked patches, sores, scars, oily vs
    dry roughness.  Extra params: pore, wrinkle, sinew, dry, scar, bruise"""
    col = _c(p, 'color', (0.34, 0.345, 0.32))
    blotch = _c(p, 'blotch', (0.20, 0.17, 0.19))
    vein = _c(p, 'vein', (0.10, 0.09, 0.15))
    bruise = _c(p, 'bruise', (blotch[0] * 0.8, blotch[1] * 0.55, blotch[2] * 0.78))
    r0 = _c(p, 'rough', 0.5)
    wr = _c(p, 'wrinkle', 1.0)
    sn = _c(p, 'sinew', 1.0)
    P = N.coord()
    arm = N.arm_mask()
    big = N.noise(_c(p, 'scale', 6.5), 4, 0.62)
    mid = N.noise(24, 3, 0.6)
    mot = N.noise(47, 3, 0.6, vec=N.mapping(P, loc=(3.1, 1.7, 0.4)))
    hide = N.scan('hide', size=0.45, relief=_c(p, 'pore', 0.22), res=512)
    cav = N.cavity()

    def iso(n, w):
        """thin lines where a noise crosses its mid level"""
        return N.mapr(N.math('ABSOLUTE', N.math('SUBTRACT', n, 0.5)), 0.0, w, 1.0, 0.0)
    # fine wrinkles: two sets of long wandering lines crossing at ~60 deg (the diamond micro relief of old skin)
    l1 = iso(N.noise(15, 2, 0.5, vec=N.mapping(N.warp(0.03, 7.0), scale=(1, 1, 5.0), rot=(0.35, 0.0, 0.2))), 0.05)
    l2 = iso(N.noise(15, 2, 0.5, vec=N.mapping(N.warp(0.03, 7.0), scale=(1, 1, 5.0), rot=(-0.6, 0.25, 1.1), loc=(3.3, 1.1, 2.7))), 0.05)
    fine = N.math('MAXIMUM', l1, N.math('MULTIPLY', l2, 0.8))
    fine = N.math('MULTIPLY', fine, N.mapr(N.noise(9, 2, 0.5, vec=N.mapping(P, loc=(5.0, 5.0, 5.0))), 0.3, 0.6, 0.35, 1.0))
    # deep creases: ~2 cm polygons, only where the mesh folds (armpits, groin, neck, between fingers, under ribs)
    w1 = N.voro(50, 'DISTANCE_TO_EDGE', vec=N.mapping(N.warp(0.05, 8), scale=(1, 1, 2.0)))
    cr1 = N.math('MULTIPLY', N.mapr(w1, 0.0, 0.10, 1.0, 0.0), N.math('ADD', 0.12, N.mapr(cav, 0.08, 0.45)))
    # sinews / tendons under the skin: long ridges down the body, along the arms in the T-pose
    s1 = N.noise(34, 2, 0.5, vec=N.mapping(N.warp(0.02, 5), scale=(1, 1, 0.1)))
    s2 = N.noise(34, 2, 0.5, vec=N.mapping(N.warp(0.02, 5), scale=(0.1, 1, 1)))
    sin = N.mapr(N.mixf(arm, s1, s2), 0.36, 0.66)
    lump = N.noise(15, 2, 0.5)
    # --- colour
    c = N.mix(N.mapr(big, 0.4, 0.68), col, blotch)
    c = N.mix(N.mapr(mid, 0.5, 0.72, 0.0, 0.5), c, bruise)
    c = N.mix(N.mapr(mot, 0.56, 0.8, 0.0, 0.3), c, N.mul(col, 1.3))
    c = N.mix(N.mapr(mot, 0.42, 0.22, 0.0, 0.25), c, N.mul(blotch, 0.75))
    c = N.detail(c, hide['det'], 1.0)
    c = N.shade(c, N.mapr(hide['hn'], -0.2, -1.6, 0.0, 0.4), 0.6)                        # pores hold dirt
    c = N.shade(c, N.mapr(sin, 0.5, 0.0, 0.0, 0.3 * sn), 0.62)                           # hollows between the sinews
    vd = N.voro(_c(p, 'vein_scale', 11.0), 'DISTANCE_TO_EDGE', vec=N.warp(0.12, 5))
    vm = N.math('MULTIPLY', N.mapr(vd, 0.0, 0.035, 1.0, 0.0), N.mapr(N.noise(3.5, 2, 0.5), 0.42, 0.62))
    c = N.mix(N.math('MULTIPLY', vm, _c(p, 'vein_amt', 0.7)), c, vein)
    c = N.shade(c, N.math('MULTIPLY', fine, 0.3), 0.55)
    c = N.shade(c, N.math('MULTIPLY', cr1, 0.6), 0.4)
    # dry, cracked patches (scan of cracked mud at ~2 cm plates)
    dm = N.math('MULTIPLY', N.mapr(N.noise(4.5, 3, 0.6, vec=N.mapping(P, loc=(8.3, 4.1, 6.6))), 0.5, 0.66), _c(p, 'dry', 0.7))
    ck = N.scan('mud_crk', size=0.4, relief=0.5)
    c = N.mix(N.math('MULTIPLY', dm, 0.35), c, N.mix(0.5, col, (0.42, 0.40, 0.36)))
    c = N.shade(c, N.math('MULTIPLY', N.mapr(ck['hn'], -0.4, -1.6), dm), 0.3)
    srn = N.noise(13, 4, 0.7, typ='RIDGED_MULTIFRACTAL')
    slo = _c(p, 'sore_lo', 0.72)
    sore = N.mapr(srn, slo, 0.9)
    sa = _c(p, 'sore_amt', 0.6)
    rim = N.math('MULTIPLY', N.mapr(srn, slo - 0.12, slo), sa)
    c = N.mix(N.math('MULTIPLY', rim, 0.5), c, N.mix(0.5, bruise, (0.30, 0.20, 0.10)))
    c = N.mix(N.math('MULTIPLY', sore, sa), c, _c(p, 'sore', (0.16, 0.035, 0.03)))
    # scars: a few long pale welts
    sv = N.voro(8.0, 'DISTANCE_TO_EDGE', vec=N.mapping(N.warp(0.08, 3.0), scale=(1, 0.3, 0.3), rot=(0.4, 0.5, 0.7)))
    scar = N.math('MULTIPLY', N.mapr(sv, 0.0, 0.011, 1.0, 0.0), N.mapr(N.noise(2.6, 2, 0.5, vec=N.mapping(P, loc=(1.2, 6.6, 3.9))), 0.56, 0.62))
    scar = N.math('MULTIPLY', scar, _c(p, 'scar', 1.0))
    c = N.mix(N.math('MULTIPLY', scar, 0.75), c, N.mix(0.6, N.mul(col, 1.5), (0.55, 0.42, 0.40)))
    hd = _c(p, 'hands_dark', 0.0)
    if hd > 0:   # necrotic extremities: darker towards the hands (|x| large in the T-pose) and the feet
        x, _, _ = N.sep(P)
        m = N.mapr(N.math('ABSOLUTE', x), _c(p, 'hands_x0', 0.42), _c(p, 'hands_x1', 0.82))
        c = N.mix(N.math('MULTIPLY', m, hd), c, N.detail(_c(p, 'dark', (0.06, 0.05, 0.055)), hide['det'], 1.0))
    fd = _c(p, 'feet_dirt', 0.0)
    if fd > 0:
        m = N.math('SUBTRACT', 1.0, N.height_z(0.0, _c(p, 'feet_hi', 0.5)))
        m = N.math('MULTIPLY', m, N.mapr(N.noise(11, 3, 0.6), 0.2, 0.75, 0.55, 1.0))
        c = N.mix(N.math('MULTIPLY', m, fd), c, N.detail(_c(p, 'dirt', (0.075, 0.062, 0.05)), hide['det'], 1.0))
    edge = N.edge(0.3, 0.62, 0.25)
    c = N.mix(N.math('MULTIPLY', edge, 0.22), c, N.mul(col, 1.35))          # skin drawn tight over bone goes pale
    # --- roughness: oily patches vs dry, cracked and crusted areas; sores are wet
    rough = N.math('ADD', N.mapr(mid, 0.28, 0.75, r0 - 0.1, r0 + 0.3), N.math('MULTIPLY', hide['dr'], 0.8))
    rough = N.math('ADD', rough, N.math('MULTIPLY', fine, 0.12))
    rough = N.mixf(N.math('MULTIPLY', dm, 0.8), rough, 0.92)
    rough = N.mixf(N.math('MULTIPLY', sore, sa), rough, 0.2)
    rough = N.mixf(N.math('MULTIPLY', scar, 0.7), rough, 0.36)
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.96), 0.18)
    H = [(hide['h'], 1.0), (fine, -0.2 * wr), (cr1, -0.7 * wr), (vm, 0.8 * _c(p, 'vein_amt', 0.7) / 0.7),
         (sin, 1.7 * sn), (lump, 2.4), (N.math('MULTIPLY', ck['h'], dm), 1.0), (N.math('MULTIPLY', sore, sa), -0.9),
         (N.math('MULTIPLY', rim, 0.6), 0.5), (scar, 0.4)]
    return _out(N, p, c, rough, 0.0, H, 0.45)


def k_rock(N, p):
    """charred hide / basalt: tilted plates with deep cracks between them, scanned cracked-rock surface, strata,
    chipped pale edges, ash on upward faces; `heat` gives the cracks a white-hot -> red -> crust gradient"""
    col = _c(p, 'color', (0.04, 0.037, 0.036))
    ash = _c(p, 'ash', (0.19, 0.18, 0.17))
    sc = _c(p, 'scale', 15.0)
    wv = N.warp(0.06, 6)
    cell = N.voro(sc, 'F1', vec=wv, out='Color')
    cellv = N.new('ShaderNodeSeparateColor'); N.set(cellv.inputs[0], cell)
    edge = N.voro(sc, 'DISTANCE_TO_EDGE', vec=wv)
    cw = _c(p, 'crack_w', 0.045)
    crack = N.mapr(edge, 0.0, cw, 1.0, 0.0)
    rk = N.scan('rock_crk', size=0.7, relief=0.42)
    bt = N.scan('burnt', size=0.5, relief=0.3)
    bs = N.scan('basalt', size=1.3, relief=0.3)
    fine = N.noise(48, 4, 0.65)
    mid = N.noise(26, 3, 0.6)
    strata = N.noise(70, 2, 0.5, vec=N.mapping(N.warp(0.03, 5), scale=(0.12, 0.12, 1)))
    c = N.mix(N.mapr(cellv.outputs[0], 0.0, 1.0, 0.0, 0.6), col, N.mul(col, 2.3))
    c = N.mix(N.mapr(mid, 0.5, 0.75, 0.0, 0.4), c, N.mix(0.5, col, ash))
    c = N.detail(c, rk['det'], 0.9)
    c = N.detail(c, bt['det'], 0.5)
    c = N.shade(c, N.mapr(rk['hn'], -0.3, -1.8, 0.0, 0.7), 0.35)
    c = N.shade(c, N.mapr(strata, 0.35, 0.6, 0.25, 0.0), 0.6)
    c = N.mix(N.math('MULTIPLY', crack, 0.85), c, N.mul(col, 0.2))
    up = N.math('MULTIPLY', N.upfacing(0.3, 0.9), N.mapr(N.noise(11, 4, 0.7), 0.3, 0.7))
    up = N.math('MULTIPLY', up, N.mapr(rk['hn'], -0.8, 0.5, 1.0, 0.45))                       # ash settles in the lows
    c = N.mix(N.math('MULTIPLY', up, _c(p, 'ash_amt', 0.7)), c, ash)
    pe = N.pointy(0.5, 0.62)
    chip = N.math('MULTIPLY', N.mapr(edge, cw * 2.6, cw * 1.2), N.mapr(fine, 0.5, 0.7))      # chipped plate rims
    c = N.mix(N.math('MULTIPLY', N.math('MAXIMUM', pe, chip), 0.4), c, N.mix(0.5, ash, col))
    heat = _c(p, 'heat', None)
    if heat is not None:   # heat gradient in the cracks: pale core, the heat colour, dull red out to ~3 crack widths
        ha = _c(p, 'heat_amt', 0.5)
        glow = N.mapr(edge, cw * 2.4, 0.0)
        hmask = N.math('MULTIPLY', N.math('POWER', glow, 1.6), N.mapr(N.noise(2.6, 3, 0.6, vec=N.mapping(N.coord(), loc=(4.2, 7.7, 1.9))), 0.47, 0.68))
        hc = N.ramp(glow, [(0.0, tuple(x * 0.10 for x in heat)), (0.6, tuple(x * 0.45 for x in heat)), (0.9, heat),
                           (1.0, (min(1.0, heat[0] * 1.7), min(1.0, heat[1] * 3.6), min(1.0, heat[2] * 9.0)))])
        c = N.mix(N.math('MULTIPLY', hmask, min(1.0, ha * 1.7)), c, hc)
    rough = N.math('ADD', N.mapr(fine, 0.2, 0.8, _c(p, 'rough', 0.78) - 0.12, 0.96), N.math('MULTIPLY', rk['dr'], 0.5))
    rough = N.mixf(N.mapr(cellv.outputs[2], 0.45, 0.8, 0.0, 0.7), rough, 0.36)               # glassy, fused plates
    rough = N.mixf(N.math('MULTIPLY', up, 0.8), rough, 0.97)                                 # ash is dead matte
    rough = N.math('MAXIMUM', N.math('MINIMUM', rough, 0.98), 0.25)
    tilt = N.math('SUBTRACT', cellv.outputs[1], 0.5)
    H = [(N.mapr(edge, 0.0, cw * 2.2), 3.4), (tilt, 2.2), (rk['h'], 1.0), (bt['h'], 1.0), (bs['h'], 1.0), (strata, 0.7), (chip, -0.6),
         (N.noise(18, 2, 0.5), 2.0)]
    return _out(N, p, c, rough, 0.0, H, 0.7)


def k_glass(N, p):
    """dull black volcanic glass: conchoidal chips, flow banding, scuffed dull patches"""
    col = _c(p, 'color', (0.012, 0.012, 0.016))
    smudge = N.noise(9, 4, 0.7)
    flow = N.noise(22, 2, 0.5, vec=N.mapping(N.coord(), scale=(4, 0.3, 4)))
    ob = N.scan('obsidian', size=0.8, relief=0.16)
    c = N.mix(N.mapr(flow, 0.45, 0.8, 0.0, 0.6), col, _c(p, 'band', (0.035, 0.035, 0.05)))
    c = N.detail(c, ob['det'], 0.6)
    edge = N.pointy(0.5, 0.6)
    c = N.mix(N.math('MULTIPLY', edge, 0.6), c, (0.12, 0.13, 0.16))
    r0 = _c(p, 'rough', 0.16)
    rough = N.math('ADD', N.mapr(smudge, 0.3, 0.75, r0, r0 + 0.34), N.math('MULTIPLY', ob['dr'], 0.4))
    rough = N.mixf(N.math('MULTIPLY', edge, 0.6), rough, 0.6)
    rough = N.math('MAXIMUM', rough, 0.06)
    chip = N.voro(26, 'SMOOTH_F1', vec=N.warp(0.04, 6), smooth=0.4)
    H = [(flow, 0.6), (chip, 1.6), (ob['h'], 1.0)]
    return _out(N, p, c, rough, _c(p, 'metal', 0.0), H, 0.2)


def k_wax(N, p):
    """tallow candle wax: runs and drips, fingerprints of soot, matte bloom vs glossy fresh melt"""
    col = _c(p, 'color', (0.62, 0.56, 0.42))
    big = N.noise(9, 3, 0.5)
    mid = N.noise(30, 3, 0.6)
    c = N.mix(N.mapr(big, 0.3, 0.7, 0.0, 0.5), col, N.mul(col, 0.6))
    c = N.mix(N.mapr(mid, 0.5, 0.75, 0.0, 0.3), c, N.mul(col, 1.25))
    soot = N.math('MULTIPLY', N.upfacing(0.5, 0.95), _c(p, 'soot', 0.5))
    soot = N.math('MAXIMUM', soot, N.math('MULTIPLY', N.mapr(N.noise(40, 3, 0.7), 0.6, 0.78), _c(p, 'soot', 0.5) * 0.7))
    c = N.mix(soot, c, (0.05, 0.045, 0.04))
    drip = N.noise(30, 2, 0.5, vec=N.mapping(N.coord(), scale=(1, 1, 0.15)))
    run = N.voro(45, 'SMOOTH_F1', vec=N.mapping(N.coord(), scale=(1, 1, 0.2)), smooth=0.6)
    rough = N.math('ADD', N.mapr(big, 0.3, 0.7, 0.28, 0.6), N.math('MULTIPLY', N.math('SUBTRACT', mid, 0.5), 0.25))
    H = [(drip, 1.6), (N.math('SUBTRACT', 0.6, run), 1.6), (mid, 0.5)]
    return _out(N, p, c, rough, 0.0, H, 0.3)


def k_wood(N, p):
    """old handled wood: grain grooves and long splits along `axis`, dark dirt in the grain, polished grey wear"""
    col = _c(p, 'color', (0.12, 0.08, 0.05))
    axis = _c(p, 'axis', (0, 0, 1))
    al = N.align(N.warp(0.012, 5.0), axis, 0.05)
    g = N.noise(60, 3, 0.6, vec=al)
    g2 = N.noise(150, 2, 0.6, vec=al)
    split = N.mapr(N.voro(30, 'DISTANCE_TO_EDGE', vec=N.align(N.warp(0.01, 9.0), axis, 0.035)), 0.0, 0.06, 1.0, 0.0)
    split = N.math('MULTIPLY', split, N.mapr(N.noise(5, 2, 0.5), 0.45, 0.6))
    big = N.noise(6, 3, 0.6)
    bk = N.scan('bark', size=0.7, relief=0.2)
    c = N.mix(N.mapr(g, 0.3, 0.7), N.mul(col, 0.5), col)
    c = N.mix(N.mapr(g2, 0.35, 0.7, 0.0, 0.35), c, N.mul(col, 1.3))
    c = N.mix(N.mapr(big, 0.4, 0.8, 0.0, 0.5), c, N.mul(c, 0.5))
    c = N.detail(c, bk['det'], 0.35)
    c = N.mix(N.math('MULTIPLY', split, 0.8), c, N.mul(col, 0.18))
    edge = N.pointy(0.5, 0.6)
    worn = N.math('MAXIMUM', N.math('MULTIPLY', edge, 0.5), N.mapr(N.noise(4.0, 3, 0.6, vec=N.mapping(N.coord(), loc=(2.0, 3.0, 5.0))), 0.55, 0.75, 0.0, 0.35))
    c = N.mix(worn, c, (0.26, 0.2, 0.14))
    rough = N.mixf(worn, N.mapr(g, 0.3, 0.7, 0.58, 0.88), 0.36)
    H = [(g, 1.1), (g2, 0.35), (split, -0.9), (bk['h'], 1.0), (N.noise(24, 2, 0.5), 0.8)]
    return _out(N, p, c, rough, 0.0, H, 0.5)


def k_emis(N, p):
    """glowing accent. emit=(r,g,b), strength; base colour is a dimmed, mottled version. `mottle` also drives the
    emission mask: hot core with cooled, darker crust flecks (lava) -- small values keep eyes / sigils clean"""
    e = _c(p, 'emit', (1.0, 0.5, 0.1))
    mo = _c(p, 'mottle', 0.5)
    n = N.noise(_c(p, 'scale', 18.0), 3, 0.6)
    base = tuple(min(1.0, x * _c(p, 'base_mul', 0.55)) for x in e)
    c = N.mix(N.mapr(n, 0.3, 0.75, 0.0, mo), base, tuple(x * 0.25 for x in base))
    # emission mask. lava / ember looks (by name, or crust=) get slow hot-cold pulsing along the seam, irregular
    # islands of cooled crust and cooler rims where the seam dives under the plates; other looks stay almost even
    nm = str(p.get('_name', ''))
    cr = _c(p, 'crust', 1.0 if any(k in nm for k in ('lava', 'ember', 'magma', 'molten', 'fire')) else 0.0)
    if cr > 0:
        sc = _c(p, 'scale', 18.0)
        isl = N.mapr(N.noise(sc * 3.2, 3, 0.65, vec=N.warp(0.015, 40)), 0.56, 0.70)
        em = N.mapr(n, 0.25, 0.8, 0.78, 1.0)
        em = N.math('MULTIPLY', em, N.mixf(N.math('MULTIPLY', isl, min(1.0, cr)), 1.0, 0.14))
        em = N.math('MULTIPLY', em, N.mapr(N.cavity(), 0.45, 0.95, 1.0, 0.72))
        c = N.mix(N.math('MULTIPLY', isl, min(1.0, cr)), c, (0.035, 0.02, 0.015))
    else:
        em = N.mapr(n, 0.3, 0.75, 1.0, 1.0 - 0.12 * min(1.0, mo))
    return dict(color=c, rough=_c(p, 'rough', 0.6), metal=0.0, height=n, bump=(0.2, 0.003),
                emit=(e, _c(p, 'strength', 4.0)), emask=em)


def k_spectral(N, p):
    """ghost cloth: long wispy strands with real relief, thin gauze weave, fading towards the hem"""
    col = _c(p, 'color', (0.42, 0.55, 0.58))
    dark = _c(p, 'dark', (0.07, 0.11, 0.14))
    st = N.noise(14, 4, 0.65, vec=N.mapping(N.coord(), scale=(1, 1, 0.12)))
    st2 = N.noise(40, 3, 0.6, vec=N.mapping(N.warp(0.02, 5), scale=(1, 1, 0.08)))
    big = N.noise(3.5, 3, 0.55)
    wv = N.scan('linen', size=0.7, relief=0.16, blend=0.12)
    c = N.mix(N.mapr(st, 0.3, 0.72), dark, col)
    c = N.mix(N.mapr(st2, 0.35, 0.7, 0.0, 0.35), c, N.mul(c, 0.6))
    c = N.mix(N.mapr(big, 0.35, 0.75, 0.0, 0.6), c, dark)
    c = N.detail(c, wv['det'], 0.5)
    low = N.math('SUBTRACT', 1.0, N.height_z(_c(p, 'fade_lo', 0.2), _c(p, 'fade_hi', 1.0)))
    c = N.mix(N.math('MULTIPLY', low, 0.55), c, _c(p, 'wisp', (0.62, 0.80, 0.82)))
    rough = N.mapr(st, 0.3, 0.7, 0.72, 0.95)
    H = [(N.mapr(st, 0.3, 0.7), 3.5), (st2, 1.4), (wv['h'], 1.0)]
    return _out(N, p, c, rough, 0.0, H, 0.5)


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


# ------------------------------------------------------------------------------------------------ enhancers / finisher
def _add_h(N, r, terms):
    """add metric height terms (mm) to a kind result whatever its bump convention"""
    if r.get('height') is None:
        return
    st, dist = r['bump']
    unit = dist if r.get('v2') else dist * max(st, 1e-3) * LEGACY_GAIN      # metres per unit of r['height']
    r['height'] = N.math('ADD', r['height'], N.math('MULTIPLY', N.hsum(terms), 0.001 / unit))


def _enh_wraps(N, p, r):
    wv = N.scan('linen', size=0.5, relief=0.3, blend=0.12)
    mid = N.noise(26, 3, 0.6)
    r['color'] = N.detail(r['color'], wv['det'], 1.0)
    r['color'] = N.shade(r['color'], N.mapr(wv['hn'], -0.2, -1.6, 0.0, 0.5), 0.5)
    r['color'] = N.shade(r['color'], N.mapr(mid, 0.5, 0.75, 0.0, 0.35), 0.62)
    r['rough'] = N.math('ADD', N.mapr(mid, 0.3, 0.7, -0.14, 0.04), r['rough'], clamp=True)
    _add_h(N, r, [(wv['h'], 1.0), (N.noise(20, 2, 0.5), 2.2), (N.voro(36, 'SMOOTH_F1', vec=N.warp(0.05, 7), smooth=0.7), 1.0)])


def _enh_burlap(N, p, r):
    wv = N.scan('burlap', size=0.62, relief=0.36, blend=0.12)
    r['color'] = N.detail(r['color'], wv['det'], 1.0)
    r['color'] = N.shade(r['color'], N.mapr(wv['hn'], -0.2, -1.6, 0.0, 0.55), 0.45)
    HF, fold = _cloth_folds(N, p, 13.0, 0.8)
    r['color'] = N.shade(r['color'], N.mapr(fold, 0.5, 0.05, 0.0, 0.45), 0.5)
    _add_h(N, r, HF + [(wv['h'], 1.0)])


def _enh_ice(N, p, r):
    ic = N.scan('ice', size=0.6, relief=0.2)
    sn = N.scan('snow', size=0.8, relief=0.22)
    mid = N.noise(24, 3, 0.6)
    r['color'] = N.detail(r['color'], ic['det'], 0.45)
    r['color'] = N.shade(r['color'], N.mapr(mid, 0.5, 0.74, 0.0, 0.3), 0.72)
    r['rough'] = N.math('ADD', r['rough'], N.math('ADD', N.math('MULTIPLY', ic['dr'], 0.5), N.mapr(mid, 0.3, 0.7, -0.05, 0.1)), clamp=True)
    up = N.upfacing(0.2, 0.9)
    _add_h(N, r, [(ic['h'], 1.0), (N.math('MULTIPLY', sn['h'], up), 1.0), (mid, 1.0)])


def _enh_sand(N, p, r):
    sn = N.scan('snow', size=0.6, relief=0.3)
    r['color'] = N.detail(r['color'], sn['det'], 0.6)
    _add_h(N, r, [(sn['h'], 1.0), (N.noise(22, 2, 0.5), 1.5)])


ENHANCE = dict(wraps=_enh_wraps, burlap=_enh_burlap, glacier=_enh_ice, crystal=_enh_ice, sand=_enh_sand)

# cavity grime per kind family: (colour, amount).  Looks can override with grime=(r,g,b), grime_amt=
GRIME = dict(
    skin=((0.045, 0.028, 0.024), 0.75), bone=((0.06, 0.042, 0.026), 0.9), metal=((0.022, 0.017, 0.013), 0.85),
    cloth=((0.035, 0.03, 0.025), 0.6), leather=((0.03, 0.022, 0.016), 0.75), rock=((0.012, 0.011, 0.01), 0.7),
    wood=((0.03, 0.022, 0.015), 0.8), wax=((0.05, 0.04, 0.03), 0.7), fur=((0.02, 0.016, 0.012), 0.6),
    ice=((0.02, 0.07, 0.16), 0.45), glacier=((0.012, 0.05, 0.13), 0.45), crystal=((0.015, 0.06, 0.16), 0.3),
    glass=((0.03, 0.03, 0.03), 0.4), moss=((0.012, 0.016, 0.008), 0.6), mud=((0.02, 0.016, 0.012), 0.5),
    wraps=((0.07, 0.05, 0.03), 0.7), burlap=((0.05, 0.035, 0.022), 0.7), sand=((0.16, 0.11, 0.06), 0.4),
    spectral=((0.02, 0.035, 0.045), 0.35),
)
NO_FINISH = ('emis', 'flat')


def finish(N, kind, p, r):
    """applied to every look after its kind: grime where the tight AO finds a crevice, slow colour drift"""
    if kind in NO_FINISH:
        return r
    base = kind[6:] if kind.startswith('frost_') else kind
    gc, ga = GRIME.get(base, ((0.03, 0.025, 0.02), 0.6))
    if kind.startswith('frost_'):
        ga *= 0.45
    gc = p.get('grime', gc); ga = p.get('grime_amt', ga)
    if ga > 0:
        cav = N.cavity()
        n = N.noise(19, 4, 0.7, vec=N.mapping(N.coord(), loc=(6.1, 2.3, 9.4)))
        m = N.math('MULTIPLY', N.math('MULTIPLY', cav, N.mapr(n, 0.2, 0.75, 0.55, 1.5)), ga, clamp=True)
        r['color'] = N.mix(m, r['color'], N.mix(0.25, gc, r['color']))
        r['rough'] = N.mixf(N.math('MULTIPLY', m, 0.85), r['rough'], 0.93)
        if isinstance(r['metal'], bpy.types.NodeSocket) or r['metal'] > 0:
            r['metal'] = N.mixf(m, r['metal'], 0.0)
    dv = p.get('drift', 0.22)
    if dv > 0:   # no surface is one flat tone: slow hue / value drift over 30-50 cm
        d = N.noise(2.6, 2, 0.5, color=True, vec=N.mapping(N.coord(), loc=(11.0, 4.0, 7.0)))
        d = N.mix(0.62, d, N.lum(d))                               # mostly a value drift, a little hue
        f = N.mix(dv, (1.0, 1.0, 1.0), N.mix(1.0, d, (2.0, 2.0, 2.0), 'MULTIPLY'))
        r['color'] = N.mix(1.0, r['color'], f, 'MULTIPLY')
    return r


def make_look_material(name, kind, params):
    mat = bpy.data.materials.new("look_" + name)
    N = NB(mat)
    params = dict(params, _name=name)        # kinds may key behaviour on the look name (k_emis: lava-like looks)
    r = KINDS[kind](N, params)
    if kind in ENHANCE:
        ENHANCE[kind](N, params, r)
    r = finish(N, kind, params, r)
    P = N.new('ShaderNodeBsdfPrincipled'); P.name = 'P'
    N.set(P.inputs['Base Color'], r['color'])
    N.set(P.inputs['Roughness'], r['rough'])
    N.set(P.inputs['Metallic'], r['metal'])
    if r.get('height') is not None and r['bump'][0] > 0:
        st, dist = r['bump']
        b = N.new('ShaderNodeBump')
        b.inputs['Strength'].default_value = 1.0
        b.inputs['Distance'].default_value = dist if r.get('v2') else dist * st * LEGACY_GAIN
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
    # ORM pass: R = emission mask (1 on emissive looks, scaled by their pattern), G = roughness, B = metallic
    comb = N.new('ShaderNodeCombineColor')
    N.set(comb.inputs[0], r.get('emask', 1.0) if em else 0.0)
    N.set(comb.inputs[1], r['rough']); N.set(comb.inputs[2], r['metal'])
    eo = N.new('ShaderNodeEmission'); eo.name = 'E_ORM'
    N.set(eo.inputs[0], comb.outputs[0])
    # emission pass: emission COLOUR x pattern (black on non-emissive looks). The game shader adds the emission
    # texture to the emission colour and expects the texture to carry the colour (white emissiveFactor).
    ee = N.new('ShaderNodeEmission'); ee.name = 'E_EMI'
    N.set(ee.inputs[0], N.mul(em[0], r.get('emask', 1.0)) if em else (0.0, 0.0, 0.0))
    # stage-1 geometry masks: R = wide AO, G = tight AO, B = "inside" AO (convex edges, thin parts)
    def ao(dist, inside, local, samples=12):
        a = N.new('ShaderNodeAmbientOcclusion', samples=samples, inside=inside, only_local=local)
        a.inputs['Distance'].default_value = dist
        return a.outputs['AO']
    cm = N.new('ShaderNodeCombineColor')
    N.set(cm.inputs[0], ao(0.30, False, False)); N.set(cm.inputs[1], ao(0.03, False, False)); N.set(cm.inputs[2], ao(0.011, True, True))
    emk = N.new('ShaderNodeEmission'); emk.name = 'E_MASK'
    N.set(emk.inputs[0], cm.outputs[0])
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


def _up2(a):
    """nearest 2x upsample followed by a light blur (mask maps 1024 -> 2048)"""
    return _blur(np.repeat(np.repeat(a, 2, axis=0), 2, axis=1), 1)


def _srgb2lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def _lin2srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


_OIIO_WARNED = [False]


def _write_jpeg_oiio(path, arr, quality, full_chroma):
    """JPEG through the OpenImageIO module bundled with Blender: lets us choose the chroma subsampling. Blender's own
    JPEG writer always uses 4:2:0, which halves the resolution of two thirds of a normal map's signal."""
    try:
        import OpenImageIO as oiio
        a8 = np.ascontiguousarray((np.clip(arr[::-1], 0, 1) * 255.0 + 0.5).astype(np.uint8))     # row 0 = top
        h, w = a8.shape[:2]
        spec = oiio.ImageSpec(w, h, 3, 'uint8')
        spec.attribute('Compression', 'jpeg:%d' % int(quality))
        spec.attribute('jpeg:subsampling', '4:4:4' if full_chroma else '4:2:0')
        out = oiio.ImageOutput.create(path)
        if out is None:
            raise RuntimeError(oiio.geterror())
        if not out.open(path, spec):
            raise RuntimeError(out.geterror())
        ok = out.write_image(a8)
        out.close()
        if not ok or not os.path.isfile(path):
            raise RuntimeError("write_image failed")
        return True
    except Exception as e:
        if not _OIIO_WARNED[0]:
            M.log("NOTE: OpenImageIO JPEG writer not usable (%s); using Blender's writer (4:2:0 chroma)" % e)
            _OIIO_WARNED[0] = True
        return False


def _save_jpg(name, arr, noncolor, quality, full_chroma=False):
    """write an (h,w,3) float array as a JPEG in M.TEXDIR and return it re-loaded from that file (so the glTF
    exporter embeds the very same bytes instead of re-encoding). full_chroma = 4:4:4 (normal map, ORM)."""
    h, w = arr.shape[:2]
    path = os.path.join(M.TEXDIR, name + ".jpg")
    if not _write_jpeg_oiio(path, arr, quality, full_chroma):
        old = bpy.data.images.get("tmp_" + name)
        if old:
            bpy.data.images.remove(old)
        im = bpy.data.images.new("tmp_" + name, w, h, alpha=False)
        if noncolor:
            im.colorspace_settings.name = 'Non-Color'
        a = np.ones((h, w, 4), dtype=np.float32)
        a[:, :, :3] = np.clip(arr, 0, 1)
        im.pixels.foreach_set(a.ravel())
        im.filepath_raw = path
        im.file_format = 'JPEG'
        try:
            im.save(filepath=path, quality=quality)
        except TypeError:
            sc = bpy.context.scene
            sc.render.image_settings.file_format = 'JPEG'
            sc.render.image_settings.quality = quality
            im.save_render(path, scene=sc)
        bpy.data.images.remove(im)
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    out = bpy.data.images.load(path, check_existing=False)
    out.name = name
    if noncolor:
        out.colorspace_settings.name = 'Non-Color'
    out.pack()
    return out


def bake_and_assign(C, objs, size=2048, ao_strength=0.5, mask_size=1024):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    # fixed thread count: unbounded Cycles bakes in several Blender processes at once hung the laptop
    import os as _os
    sc.render.threads_mode = 'FIXED'
    sc.render.threads = max(1, min(32, int(_os.environ.get('ASHEN_BLENDER_THREADS', '12'))))
    try:
        sc.cycles.use_denoising = False
        sc.cycles.use_adaptive_sampling = False
    except Exception:
        pass
    if sc.world is None:
        sc.world = bpy.data.worlds.new("bakeworld")
    mats = list(C.look_mats.values())
    bk = sc.render.bake
    bk.use_selected_to_active = False
    bk.use_clear = False
    bk.target = 'IMAGE_TEXTURES'

    def run(im, shader_node, typ, samples, margin, **kw):
        for m in mats:
            nt = m.node_tree
            n = nt.nodes['BAKE']
            n.image = im
            for x in nt.nodes:
                x.select = False
            n.select = True
            nt.nodes.active = n
            nt.links.new(nt.nodes[shader_node].outputs[0], nt.nodes['OUT'].inputs[0])
        sc.cycles.samples = samples
        bk.margin = margin
        _select(objs)
        bpy.ops.object.bake(type=typ, margin=margin, use_clear=False, **kw)

    import time
    t = time.time()
    # ---- stage 1: geometry masks (AO nodes) at mask_size, denoised, handed to the looks through their MASK node
    mk = _img("bk_mask", mask_size, (1.0, 1.0, 1.0), True)
    run(mk, 'E_MASK', 'EMIT', 6, 12)
    a = _np(mk)[:, :, :3]
    ao_w = np.clip(_blur(a[:, :, 0], 2), 0, 1)
    cav = np.clip((1.0 - _blur(a[:, :, 1], 1)) * 1.7 - 0.06, 0, 1)
    edge = np.clip((1.0 - _blur(a[:, :, 2], 1)) * 1.15 - 0.03, 0, 1)
    old = bpy.data.images.get("mon_masks_baked")
    if old:
        bpy.data.images.remove(old)
    mb = bpy.data.images.new("mon_masks_baked", mask_size, mask_size, alpha=False, float_buffer=True)
    mb.colorspace_settings.name = 'Non-Color'
    b = np.ones((mask_size, mask_size, 4), dtype=np.float32)
    b[:, :, 0] = ao_w; b[:, :, 1] = cav; b[:, :, 2] = edge
    mb.pixels.foreach_set(b.ravel())
    for m in mats:
        n = m.node_tree.nodes.get('MASK')
        if n is not None:
            n.image = mb
    bpy.data.images.remove(mk)
    t1 = time.time()
    # ---- stage 2: colour / emission-mask + roughness + metal / tangent normals at full size
    imgs = dict(col=_img("bk_col", size, (0.2, 0.2, 0.2), False),
                orm=_img("bk_orm", size, (0.0, 0.8, 0.0), True),
                nrm=_img("bk_nrm", size, (0.5, 0.5, 1.0), True))
    mg = max(8, size // 128)
    run(imgs['col'], 'E_COL', 'EMIT', 3, mg)
    run(imgs['orm'], 'E_ORM', 'EMIT', 3, mg)
    run(imgs['nrm'], 'P', 'NORMAL', 6, mg, normal_space='TANGENT')
    has_em = any('mon_emit' in m.keys() for m in mats)
    if has_em:
        imgs['emi'] = _img("bk_emi", size, (0.0, 0.0, 0.0), False)
        run(imgs['emi'], 'E_EMI', 'EMIT', 3, mg)
    M.log("bake masks %.1fs, maps %.1fs" % (t1 - t, time.time() - t1))

    col = _np(imgs['col'])[:, :, :3]
    orm = _np(imgs['orm'])[:, :, :3].copy()
    nrm = _np(imgs['nrm'])[:, :, :3].copy()
    f = size // mask_size
    ao = ao_w; cv = cav
    while f > 1:
        ao = _up2(ao); cv = _up2(cv); f //= 2
    emk = orm[:, :, 0].copy()                               # emission mask from the ORM pass (0 on non-emissive looks)
    is_em = np.clip(emk * 4.0, 0, 1)
    # wide AO shades the base colour (form), kept away from emissive texels; occluded areas go slightly warm-dark
    k = (1.0 - ao_strength) + ao_strength * np.clip(ao, 0, 1) ** 1.25
    k = k * (1.0 - 0.22 * cv)
    k = 1.0 - (1.0 - k) * (1.0 - is_em)
    tint = np.stack([k ** 0.94, k, k ** 1.06], axis=2)
    bc = _lin2srgb(_srgb2lin(col) * tint)
    occ = np.clip(ao * (1.0 - 0.45 * cv), 0, 1)
    orm[:, :, 0] = occ
    orm[:, :, 1] = np.clip(orm[:, :, 1] + (1 - k) * 0.12, 0.04, 1)   # crevices a little rougher
    # normals: renormalise
    n = nrm * 2.0 - 1.0
    n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
    nrm = n * 0.5 + 0.5
    dev = np.degrees(np.arccos(np.clip(n[:, :, 2], -1, 1)))
    cover = np.abs(_np(imgs['col'])[:, :, :3] - 0.2).sum(axis=2) > 1e-3
    if cover.any():
        dd = dev[cover]
        rr = orm[:, :, 1][cover]
        M.log("normal map deviation: median %.1f deg, 90th pct %.1f deg; roughness std %.3f; atlas coverage %.0f%%" % (
            float(np.median(dd)), float(np.percentile(dd, 90)), float(rr.std()), 100.0 * cover.mean()))

    # base colour / emission: 4:2:0 chroma is fine.  Normal map and ORM: full chroma (4:4:4), the data is not a picture
    T = dict(bc=_save_jpg(C.id + "_bc", bc, False, 90), orm=_save_jpg(C.id + "_orm", orm, True, 88, full_chroma=True),
             nrm=_save_jpg(C.id + "_nrm", nrm, True, 95, full_chroma=True))
    if has_em:
        e = _np(imgs['emi'])[:, :, :3]                      # sRGB-encoded emission colour x pattern, black elsewhere
        if e.max() > 0.02:
            T['em'] = _save_jpg(C.id + "_em", e, False, 90)
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
        if tex and tex.get('em') is not None:
            # the emission texture carries the colour (exported as emissiveTexture + a WHITE emissiveFactor + emissive
            # strength). Do not multiply it by a colour factor: Y:\game\src\CharModel.cs feeds char.gdshader, which ADDS
            # texture and emission colour and treats "white factor + texture" as "the texture is the colour".
            te = nt.nodes.new('ShaderNodeTexImage'); te.image = tex['em']
            nt.links.new(te.outputs[0], P.inputs['Emission Color'])
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
