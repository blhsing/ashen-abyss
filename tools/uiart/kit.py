"""Blender-side toolkit for the UI frame art of Ashen Abyss.

Everything is authored in TEXTURE PIXELS of the 2x sheet (2 texture px = 1 design px at 1080p): x to the right, y DOWN
from the top-left corner of the piece being built, z = relief height above the panel field, also in px. One texture
pixel is 0.01 Blender units. The camera is orthographic and looks straight down, the key light is a sun from the
top-left, so shading does not depend on where a piece sits: edges tile and pieces can be cropped anywhere.

Used by render.py (inside Blender only). No piece definitions live here: see pieces.py.
"""
import bpy, bmesh, math, os

PX = 0.01
_W = _H = 512          # size of the piece being built (texture px)
_objs = []             # objects of the piece being built
MATS = {}              # material cache (per run)
SCAN_DIRS = [r"Y:\assets_src\montex", r"Y:\assets_src\chartex"]


# ───────────────────────────────────────── scene ─────────────────────────────────────────
def setup(samples=96):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    cy = sc.cycles
    cy.device = 'CPU'
    cy.samples = samples
    cy.use_adaptive_sampling = True
    cy.adaptive_threshold = 0.015
    cy.use_denoising = True
    try:
        cy.denoiser = 'OPENIMAGEDENOISE'
    except Exception as e:
        print("uiart: denoiser", e)
    cy.max_bounces = 5
    cy.diffuse_bounces = 2
    cy.glossy_bounces = 3
    cy.transmission_bounces = 3
    cy.transparent_max_bounces = 4
    cy.caustics_reflective = False
    cy.caustics_refractive = False
    try:
        cy.pixel_filter_type = 'BLACKMAN_HARRIS'
        cy.filter_width = 1.5
    except Exception as e:
        print("uiart: filter", e)
    r = sc.render
    r.film_transparent = True
    r.image_settings.file_format = 'PNG'
    r.image_settings.color_mode = 'RGBA'
    r.image_settings.color_depth = '16'
    r.image_settings.compression = 30
    r.resolution_percentage = 100
    r.use_persistent_data = False
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except Exception as e:
        print("uiart: view transform", e)
    sc.view_settings.exposure = 0.0
    sc.view_settings.gamma = 1.0
    # camera
    cam_d = bpy.data.cameras.new("uicam")
    cam_d.type = 'ORTHO'
    cam_d.clip_start = 0.01
    cam_d.clip_end = 100.0
    cam = bpy.data.objects.new("uicam", cam_d)
    sc.collection.objects.link(cam)
    cam.location = (0.0, 0.0, 20.0)
    sc.camera = cam
    # key light: sun from the top-left, slightly soft
    sun_d = bpy.data.lights.new("key", 'SUN')
    sun_d.energy = 2.2
    sun_d.angle = math.radians(9.0)
    sun_d.color = (1.0, 0.965, 0.92)
    sun = bpy.data.objects.new("key", sun_d)
    sc.collection.objects.link(sun)
    # a sun shines along its local -Z: aim it from (-0.62, 0.62, 0.95) toward the origin
    from mathutils import Vector
    d = Vector((0.62, -0.62, -0.95)).normalized()
    sun.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    _world()
    return sc


def _world():
    """Studio environment built from dot products: a broad warm soft box toward the top-left (what the bevels and
    the worn edges mirror), a dim cool rim from the bottom-right, a weak overhead fill."""
    w = bpy.data.worlds.new("uiworld")
    bpy.context.scene.world = w
    w.use_nodes = True
    t = w.node_tree
    for n in list(t.nodes):
        t.nodes.remove(n)
    out = t.nodes.new('ShaderNodeOutputWorld')
    tc = t.nodes.new('ShaderNodeTexCoord')

    def lobe(direction, a0, a1, color, strength):
        dn = t.nodes.new('ShaderNodeVectorMath'); dn.operation = 'DOT_PRODUCT'
        L = math.sqrt(sum(c * c for c in direction))
        dn.inputs[1].default_value = tuple(c / L for c in direction)
        t.links.new(tc.outputs['Generated'], dn.inputs[0])
        mr = t.nodes.new('ShaderNodeMapRange'); mr.interpolation_type = 'SMOOTHSTEP'
        mr.inputs['From Min'].default_value = a0; mr.inputs['From Max'].default_value = a1
        mr.inputs['To Min'].default_value = 0.0; mr.inputs['To Max'].default_value = strength
        t.links.new(dn.outputs['Value'], mr.inputs['Value'])
        bg = t.nodes.new('ShaderNodeBackground')
        bg.inputs['Color'].default_value = (*color, 1.0)
        t.links.new(mr.outputs['Result'], bg.inputs['Strength'])
        return bg

    parts = [
        lobe((-1.0, 1.0, 0.75), 0.50, 0.97, (1.0, 0.95, 0.88), 3.6),      # key soft box, top-left
        lobe((-1.0, 1.0, 0.75), -0.5, 0.7, (1.0, 0.97, 0.94), 0.14),      # its wide skirt
        lobe((1.0, -1.0, 0.35), 0.55, 0.98, (0.62, 0.74, 1.0), 0.55),     # cool rim, bottom-right
        lobe((0.0, 0.0, 1.0), 0.3, 1.0, (0.9, 0.92, 1.0), 0.05),          # overhead fill
    ]
    cur = parts[0]
    for p in parts[1:]:
        a = t.nodes.new('ShaderNodeAddShader')
        t.links.new(cur.outputs[0], a.inputs[0]); t.links.new(p.outputs[0], a.inputs[1])
        cur = a
    t.links.new(cur.outputs[0], out.inputs['Surface'])


def begin(w, h):
    """Start a new piece of w x h texture px: clears the previous piece's objects."""
    global _W, _H
    _W, _H = int(w), int(h)
    for o in list(_objs):
        try:
            data = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if data is not None and data.users == 0:
                if isinstance(data, bpy.types.Mesh): bpy.data.meshes.remove(data)
                elif isinstance(data, bpy.types.Curve): bpy.data.curves.remove(data)
        except Exception as e:
            print("uiart: cleanup", e)
    _objs.clear()
    sc = bpy.context.scene
    sc.render.resolution_x = _W
    sc.render.resolution_y = _H
    sc.camera.data.ortho_scale = max(_W, _H) * PX


def render(path, samples=None):
    sc = bpy.context.scene
    if samples: sc.cycles.samples = samples
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def P(x, y, z=0.0):
    """Texture px (y down) -> Blender coordinates."""
    return ((x - _W * 0.5) * PX, (_H * 0.5 - y) * PX, z * PX)


def _link(o, mat):
    bpy.context.scene.collection.objects.link(o)
    _objs.append(o)
    if mat is not None:
        o.data.materials.append(mat if not isinstance(mat, str) else MATS[mat])
    return o


# ───────────────────────────────────────── 2D outlines ─────────────────────────────────────────
def area(poly):
    a = 0.0
    for i in range(len(poly)):
        x0, y0 = poly[i]; x1, y1 = poly[(i + 1) % len(poly)]
        a += x0 * y1 - x1 * y0
    return a * 0.5


def offset(poly, d):
    """Inward offset of a simple polygon by d px (miter joins). Keeps the vertex count."""
    if abs(d) < 1e-9: return list(poly)
    n = len(poly)
    sgn = 1.0 if area(poly) > 0 else -1.0
    out = []
    for i in range(n):
        p0 = poly[(i - 1) % n]; p1 = poly[i]; p2 = poly[(i + 1) % n]
        e1 = (p1[0] - p0[0], p1[1] - p0[1]); e2 = (p2[0] - p1[0], p2[1] - p1[1])
        l1 = math.hypot(*e1) or 1e-9; l2 = math.hypot(*e2) or 1e-9
        # inward normals (for positive area in y-down image space the interior is to the left of the edge → (-ey, ex))
        n1 = (-e1[1] / l1 * sgn, e1[0] / l1 * sgn); n2 = (-e2[1] / l2 * sgn, e2[0] / l2 * sgn)
        bx, by = n1[0] + n2[0], n1[1] + n2[1]
        bl = math.hypot(bx, by)
        if bl < 1e-6:
            out.append((p1[0] + n1[0] * d, p1[1] + n1[1] * d)); continue
        bx /= bl; by /= bl
        cosh = max(0.2, bx * n1[0] + by * n1[1])       # cos of half the turning angle; clamp long miters
        out.append((p1[0] + bx * d / cosh, p1[1] + by * d / cosh))
    return out


def rect(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def chamfer_rect(x0, y0, x1, y1, c):
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


def round_rect(x0, y0, x1, y1, r, seg=6):
    pts = []
    for cx, cy, a0 in ((x1 - r, y0 + r, -90), (x1 - r, y1 - r, 0), (x0 + r, y1 - r, 90), (x0 + r, y0 + r, 180)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90.0 * i / seg)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def ngon(cx, cy, r, n, rot=0.0, ry=None):
    ry = r if ry is None else ry
    return [(cx + r * math.cos(rot + 2 * math.pi * i / n), cy + ry * math.sin(rot + 2 * math.pi * i / n)) for i in range(n)]


def rhombus(cx, cy, rx, ry):
    return [(cx, cy - ry), (cx + rx, cy), (cx, cy + ry), (cx - rx, cy)]


def mirror_x(poly, cx):
    return [(2 * cx - x, y) for x, y in reversed(poly)]


def mirror_y(poly, cy):
    return [(x, 2 * cy - y) for x, y in reversed(poly)]


def xf(poly, dx=0.0, dy=0.0, rot=0.0, sx=1.0, sy=1.0):
    c, s = math.cos(rot), math.sin(rot)
    return [((x * sx) * c - (y * sy) * s + dx, (x * sx) * s + (y * sy) * c + dy) for x, y in poly]


# ───────────────────────────────────────── meshes ─────────────────────────────────────────
def loft(rings, mat, cap=True, smooth=False, name="loft"):
    """rings: [(poly, z), ...] with equal vertex counts, outermost first. Builds the skin between them and (cap) a
    face over the last ring. Faces are oriented upward."""
    n = len(rings[0][0])
    verts = []
    for poly, z in rings:
        assert len(poly) == n, "ring vertex counts differ"
        for (x, y) in poly: verts.append(P(x, y, z))
    faces = []
    ccw = area(rings[0][0]) < 0       # y-down: negative area = counter-clockwise when seen from +Z in Blender space
    for r in range(len(rings) - 1):
        a = r * n; b = (r + 1) * n
        for i in range(n):
            j = (i + 1) % n
            f = (a + i, a + j, b + j, b + i)
            faces.append(f if not ccw else f[::-1])
    if cap:
        a = (len(rings) - 1) * n
        f = tuple(a + i for i in range(n))
        faces.append(f[::-1] if not ccw else f)
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    _fix_normals(me)
    if smooth:
        me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    o = bpy.data.objects.new(name, me)
    return _link(o, mat)


def _fix_normals(me):
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # an open shell seen from above: make the majority face +Z
    up = sum(f.normal.z * f.calc_area() for f in bm.faces)
    if up < 0:
        for f in bm.faces: f.normal_flip()
    bm.to_mesh(me); bm.free(); me.update()


def solid(poly, profile, mat, cap=True, smooth=False, name="solid"):
    """A bevelled plate (cap=True) or a frame bar (cap=False) from an outline and a cross-section.
    profile: [(inset_px, z_px), ...] from the outer edge inward."""
    rings = [(offset(poly, d), z) for d, z in profile]
    return loft(rings, mat, cap=cap, smooth=smooth, name=name)


def plate(poly, z0, z1, bevel, mat, name="plate", lip=None):
    """Flat plate from z0 up to z1 with a chamfer of `bevel` px; optional raised lip: (width, extra height)."""
    pr = [(0.0, z0), (0.0, z1 - bevel), (bevel, z1)]
    if lip:
        w, h = lip
        pr = [(0.0, z0), (0.0, z1 + h - bevel), (bevel, z1 + h), (bevel + w, z1 + h), (bevel + w + h, z1)]
    return solid(poly, pr, mat, cap=True, name=name)


def dome(cx, cy, r, h, mat, z=0.0, seg=20, rings=6, name="dome", ry=None):
    """Rivet head / cabochon: a spherical cap."""
    ry = r if ry is None else ry
    rs = []
    for k in range(rings):
        t = k / rings                                  # 0 rim .. 1 top
        a = t * math.pi * 0.5
        rs.append((ngon(cx, cy, r * math.cos(a), seg, ry=ry * math.cos(a)), z + h * math.sin(a)))
    rs.append((ngon(cx, cy, r * 0.02, seg, ry=ry * 0.02), z + h))
    return loft(rs, mat, cap=True, smooth=True, name=name)


def pyramid(cx, cy, r, h, mat, z=0.0, rot=math.pi / 4, flat=0.25, name="stud", n=4):
    """Forged nail head: a low pyramid with a small flat top."""
    return loft([(ngon(cx, cy, r, n, rot), z), (ngon(cx, cy, r * flat, n, rot), z + h)], mat, cap=True, name=name)


def gem(poly, h, mat, z=0.0, table=0.45, name="gem", girdle=0.0):
    """Faceted stone: outline at z, crown rising to a flat table."""
    c = (sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))
    tb = [(c[0] + (p[0] - c[0]) * table, c[1] + (p[1] - c[1]) * table) for p in poly]
    rings = [(poly, z)]
    if girdle > 0: rings.append((poly, z + girdle))
    rings.append((tb, z + h))
    return loft(rings, mat, cap=True, name=name)


def tube(points, radius, mat, z=0.0, taper=None, name="wire", res=3, cyclic=False):
    """Round wire along a 2D path (filigree). taper: per-point radius factors, or 'in' / 'out' / 'both'."""
    cu = bpy.data.curves.new(name, 'CURVE')
    cu.dimensions = '3D'
    cu.bevel_depth = radius * PX
    cu.bevel_resolution = res
    cu.use_fill_caps = True
    sp = cu.splines.new('POLY')
    sp.points.add(len(points) - 1)
    n = len(points)
    for i, p in enumerate(points):
        x, y, zz = P(p[0], p[1], z if len(p) < 3 else p[2])
        sp.points[i].co = (x, y, zz, 1.0)
        t = i / max(1, n - 1)
        if taper is None: k = 1.0
        elif taper == 'out': k = 1.0 - 0.85 * t ** 1.5
        elif taper == 'in': k = 0.15 + 0.85 * t ** 0.7
        elif taper == 'both': k = 0.15 + 0.85 * math.sin(math.pi * t) ** 0.8
        else: k = taper[i]
        sp.points[i].radius = k
    sp.use_cyclic_u = cyclic
    sp.use_smooth = True
    o = bpy.data.objects.new(name, cu)
    return _link(o, mat)


def path(fn, t0, t1, n=40):
    return [fn(t0 + (t1 - t0) * i / (n - 1)) for i in range(n)]


def spiral(cx, cy, r0, r1, a0, a1, n=48):
    """Scroll from radius r0 at angle a0 to r1 at a1 (radians, y-down so positive is clockwise on screen)."""
    pts = []
    for i in range(n):
        t = i / (n - 1)
        r = r0 + (r1 - r0) * t
        a = a0 + (a1 - a0) * t
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def bez(p0, p1, p2, p3, n=24):
    pts = []
    for i in range(n):
        t = i / (n - 1); u = 1 - t
        pts.append((u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0],
                    u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1]))
    return pts


def catcher(margin=0.0, z=0.0):
    """Shadow-catcher plane = the panel field: receives the frame's cast shadow and contact occlusion as alpha."""
    v = [P(-margin, -margin, z), P(_W + margin, -margin, z), P(_W + margin, _H + margin, z), P(-margin, _H + margin, z)]
    me = bpy.data.meshes.new("catch")
    me.from_pydata(v, [], [(0, 3, 2, 1)])
    me.update()
    o = bpy.data.objects.new("catch", me)
    _link(o, mat_plain("catch_mat", (0.5, 0.5, 0.5), 0.9))
    o.is_shadow_catcher = True
    return o


def floor(poly, mat, z=0.0, name="floor"):
    return loft([(poly, z)], mat, cap=True, name=name)


# ───────────────────────────────────────── node helper ─────────────────────────────────────────
class NT:
    def __init__(self, name):
        self.m = bpy.data.materials.new(name)
        self.m.use_nodes = True
        self.t = self.m.node_tree
        for n in list(self.t.nodes): self.t.nodes.remove(n)
        self.out = self.t.nodes.new('ShaderNodeOutputMaterial')
        self.tc = self.t.nodes.new('ShaderNodeTexCoord')
        self.co = self.tc.outputs['Object']

    def node(self, kind, **kw):
        n = self.t.nodes.new(kind)
        for k, v in kw.items(): setattr(n, k, v)
        return n

    def put(self, sock, v):
        if v is None: return
        if isinstance(v, bpy.types.NodeSocket): self.t.links.new(v, sock)
        elif isinstance(v, (int, float)):
            try: sock.default_value = float(v)
            except TypeError: sock.default_value = (float(v),) * 3 + ((1.0,) if len(sock.default_value) == 4 else ())
        else:
            v = tuple(v)
            if len(sock.default_value) == 4 and len(v) == 3: v = v + (1.0,)
            sock.default_value = v

    def math(self, op, a, b=None, c=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.put(n.inputs[0], a)
        if b is not None: self.put(n.inputs[1], b)
        if c is not None: self.put(n.inputs[2], c)
        return n.outputs[0]

    def add(self, a, b): return self.math('ADD', a, b)
    def sub(self, a, b): return self.math('SUBTRACT', a, b)
    def mul(self, a, b): return self.math('MULTIPLY', a, b)
    def clamp(self, a): return self.math('MAXIMUM', self.math('MINIMUM', a, 1.0), 0.0)
    def inv(self, a): return self.math('SUBTRACT', 1.0, a)
    def max(self, a, b): return self.math('MAXIMUM', a, b)

    def ramp(self, x, a, b, lo=0.0, hi=1.0, smooth=True):
        n = self.node('ShaderNodeMapRange')
        n.interpolation_type = 'SMOOTHSTEP' if smooth else 'LINEAR'
        self.put(n.inputs['Value'], x)
        n.inputs['From Min'].default_value = a; n.inputs['From Max'].default_value = b
        n.inputs['To Min'].default_value = lo; n.inputs['To Max'].default_value = hi
        return n.outputs['Result']

    def mix(self, f, a, b):
        n = self.node('ShaderNodeMix', data_type='RGBA')
        self.put(n.inputs[0], f); self.put(n.inputs[6], a); self.put(n.inputs[7], b)
        return n.outputs[2]

    def mixf(self, f, a, b):
        n = self.node('ShaderNodeMix', data_type='FLOAT')
        self.put(n.inputs[0], f); self.put(n.inputs[2], a); self.put(n.inputs[3], b)
        return n.outputs[0]

    def mapping(self, scale=(1, 1, 1), loc=(0, 0, 0), rot=(0, 0, 0)):
        n = self.node('ShaderNodeMapping')
        n.inputs['Location'].default_value = loc; n.inputs['Rotation'].default_value = rot; n.inputs['Scale'].default_value = scale
        self.t.links.new(self.co, n.inputs['Vector'])
        return n.outputs[0]

    def noise(self, scale, detail=4.0, rough=0.55, vec=None, dist=0.0):
        n = self.node('ShaderNodeTexNoise')
        self.t.links.new(vec if vec is not None else self.co, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale; n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = rough; n.inputs['Distortion'].default_value = dist
        return n.outputs[0]

    def voronoi(self, scale, feature='SMOOTH_F1', smooth=0.6, vec=None):
        n = self.node('ShaderNodeTexVoronoi', feature=feature)
        self.t.links.new(vec if vec is not None else self.co, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        if feature == 'SMOOTH_F1': n.inputs['Smoothness'].default_value = smooth
        return n.outputs[0]

    def image(self, path, scale, colorspace='Non-Color', loc=(0, 0, 0)):
        img = bpy.data.images.load(path, check_existing=True)
        img.colorspace_settings.name = colorspace
        n = self.node('ShaderNodeTexImage', projection='BOX')
        n.projection_blend = 0.25
        n.image = img
        self.t.links.new(self.mapping(scale=(scale, scale, scale), loc=loc), n.inputs['Vector'])
        return n.outputs['Color']

    def lum(self, col):
        n = self.node('ShaderNodeRGBToBW')
        self.put(n.inputs[0], col)
        return n.outputs[0]

    def edge(self, radius_px=1.6, samples=4):
        """(bevelled normal, edge mask 0..1): how far the rounded normal leans away from the face normal."""
        bv = self.node('ShaderNodeBevel', samples=samples)
        bv.inputs['Radius'].default_value = radius_px * PX
        geo = self.node('ShaderNodeNewGeometry')
        d = self.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
        self.t.links.new(bv.outputs[0], d.inputs[0]); self.t.links.new(geo.outputs['Normal'], d.inputs[1])
        return bv.outputs[0], self.ramp(d.outputs['Value'], 0.995, 0.86, 0.0, 1.0)

    def ao(self, dist_px=10.0, samples=6):
        n = self.node('ShaderNodeAmbientOcclusion', samples=samples, only_local=False)
        n.inputs['Distance'].default_value = dist_px * PX
        return n.outputs['AO']

    def bump(self, height, normal=None, dist_px=1.0, strength=1.0):
        n = self.node('ShaderNodeBump')
        n.inputs['Strength'].default_value = strength
        n.inputs['Distance'].default_value = dist_px * PX
        self.put(n.inputs['Height'], height)
        if normal is not None: self.t.links.new(normal, n.inputs['Normal'])
        return n.outputs[0]

    def principled(self, color, rough, metal=0.0, normal=None, **extra):
        p = self.node('ShaderNodeBsdfPrincipled')
        self.put(p.inputs['Base Color'], color); self.put(p.inputs['Roughness'], rough); self.put(p.inputs['Metallic'], metal)
        if normal is not None: self.t.links.new(normal, p.inputs['Normal'])
        for k, v in extra.items():
            try: self.put(p.inputs[k], v)
            except KeyError: print("uiart: principled has no input", k)
        self.t.links.new(p.outputs[0], self.out.inputs['Surface'])
        return p


def scan(alias, kind):
    """Path of a scanned CC0 map. kind: color | rough | disp."""
    names = {'color': ("_Color.jpg", "diff_1k.jpg"), 'rough': ("_Roughness.jpg", "rough_1k.jpg"), 'disp': ("_Displacement.jpg", "disp_1k.jpg")}[kind]
    for root in SCAN_DIRS:
        d = os.path.join(root, alias)
        if not os.path.isdir(d): continue
        for f in os.listdir(d):
            if f.endswith(names[0]) or f == names[1]: return os.path.join(d, f)
    env = os.path.join(r"Y:\game\assets\env\tex", alias)
    if os.path.isdir(env):
        f = {'color': 'albedo.jpg', 'rough': 'arm.jpg', 'disp': 'height.jpg'}[kind]
        if os.path.exists(os.path.join(env, f)): return os.path.join(env, f)
    raise FileNotFoundError(alias + " " + kind)


def _sc(c, k): return tuple(min(1.0, v * k) for v in c)


# ───────────────────────────────────────── materials ─────────────────────────────────────────
def mat_plain(name, color, rough=0.6, metal=0.0):
    if name in MATS: return MATS[name]
    g = NT(name)
    g.principled(color, rough, metal)
    MATS[name] = g.m
    return g.m


def mat_metal(name, base, rough=(0.36, 0.62), edge=None, edge_amt=0.8, dark=None, soot=0.75, hammer=1.0, scan_set='Metal038',
              patina=None, patina_amt=0.0, bump=1.0, scan_px=420.0, metal=1.0, spec=None):
    """Hand-forged metal: hammer facets, scanned micro scratches, bright rubbed edges, soot (or patina) in the recesses.
    base/edge/dark: linear RGB. patina: colour that grows in cavities and in patches (verdigris, rust)."""
    if name in MATS: return MATS[name]
    g = NT(name)
    edge = edge or _sc(base, 4.0)
    dark = dark or _sc(base, 0.45)
    n1 = g.noise(5.5, 5, 0.6, dist=0.4)                    # large tonal drift
    n2 = g.noise(38.0, 4, 0.65)                            # fine break-up
    n3 = g.noise(13.0, 5, 0.7, dist=0.8)                   # wear patches
    nrm, e = g.edge(1.7)
    ao = g.ao(11.0)
    cav = g.ramp(ao, 0.92, 0.45, 0.0, 1.0)
    col = g.mix(g.ramp(n1, 0.3, 0.75), dark, base)
    col = g.mix(g.mul(g.ramp(n2, 0.5, 0.8), 0.25), col, _sc(base, 1.35))
    rgh = g.ramp(n1, 0.3, 0.75, rough[1], rough[0])
    height = g.mul(n2, 0.12 * bump)
    if scan_set:
        k = 100.0 / scan_px                                # one scan tile covers scan_px texture px
        sr = g.lum(g.image(scan(scan_set, 'rough'), k))
        sd = g.lum(g.image(scan(scan_set, 'disp'), k, loc=(0.37, 0.11, 0.0)))
        rgh = g.add(rgh, g.mul(g.sub(sr, 0.5), 0.35))
        col = g.mix(g.ramp(sr, 0.35, 0.8, 0.0, 0.35), col, dark)
        height = g.add(height, g.mul(sd, 0.5 * bump))
    if hammer > 0:
        hv = g.voronoi(6.5, 'SMOOTH_F1', 0.75, vec=g.mapping(scale=(1.0, 1.25, 1.0)))
        hv2 = g.voronoi(15.0, 'SMOOTH_F1', 0.6, vec=g.mapping(loc=(0.4, 0.7, 0.1)))
        height = g.add(height, g.add(g.mul(hv, 2.6 * hammer * bump), g.mul(hv2, 0.8 * hammer * bump)))
        col = g.mix(g.mul(g.ramp(hv, 0.1, 0.6), 0.22), col, _sc(base, 1.3))
        rgh = g.add(rgh, g.mul(g.sub(hv, 0.3), 0.12))
    # rubbed bright edges (convex only: masked by exposure), broken up
    ew = g.clamp(g.mul(g.mul(g.mul(e, g.ramp(n3, 0.25, 0.7, 0.35, 1.0)), g.ramp(ao, 0.6, 0.95)), edge_amt * 1.6))
    col = g.mix(ew, col, edge)
    rgh = g.mixf(ew, rgh, rough[0] * 0.6)
    met = g.mixf(ew, metal, 1.0)
    if patina is not None and patina_amt > 0:
        pt = g.clamp(g.max(g.mul(cav, 1.2), g.ramp(n3, 1.0 - patina_amt, 1.05 - patina_amt * 0.5)))
        pt = g.mul(pt, g.inv(ew))
        col = g.mix(g.mul(pt, 0.9), col, g.mix(g.ramp(n2, 0.3, 0.7), _sc(patina, 0.55), patina))
        rgh = g.mixf(pt, rgh, 0.85)
        met = g.mixf(g.mul(pt, 0.8), met, 0.0)
    if soot > 0:
        s = g.clamp(g.mul(cav, soot * 1.5))
        col = g.mix(g.mul(s, 0.92), col, (0.006, 0.005, 0.005))
        rgh = g.mixf(s, rgh, 0.9)
        met = g.mixf(g.mul(s, 0.7), met, 0.0)
    nb = g.bump(height, nrm, dist_px=1.0, strength=0.8)
    if spec is None: g.principled(col, g.clamp(rgh), met, nb)
    else: g.principled(col, g.clamp(rgh), met, nb, **{'Specular IOR Level': spec})
    MATS[name] = g.m
    return g.m


def mat_stone(name, tint=(0.30, 0.295, 0.29), scan_set='dark_rock', rough=0.86, chip=(0.55, 0.54, 0.52), dust=0.5, scan_px=520.0, bump=1.0):
    if name in MATS: return MATS[name]
    g = NT(name)
    k = 100.0 / scan_px
    sc_ = g.image(scan(scan_set, 'color'), k, 'sRGB')
    sd = g.lum(g.image(scan(scan_set, 'disp'), k))
    l = g.lum(sc_)
    n1 = g.noise(6.0, 5, 0.6, dist=0.5)
    n3 = g.noise(15.0, 5, 0.7)
    nrm, e = g.edge(2.2)
    ao = g.ao(12.0)
    cav = g.ramp(ao, 0.9, 0.4)
    col = g.mix(g.ramp(l, 0.0, 0.5), _sc(tint, 0.35), _sc(tint, 1.5))
    col = g.mix(g.ramp(n1, 0.3, 0.8, 0.0, 0.35), col, _sc(tint, 0.6))
    ew = g.clamp(g.mul(g.mul(e, g.ramp(n3, 0.3, 0.7, 0.3, 1.0)), 1.3))
    col = g.mix(g.mul(ew, 0.8), col, chip)
    col = g.mix(g.mul(cav, 0.85), col, _sc(tint, 0.12))
    if dust > 0:
        up = g.ramp(g.noise(9.0, 4, 0.6), 0.45, 0.8, 0.0, dust * 0.5)
        col = g.mix(up, col, (0.42, 0.40, 0.37))
    height = g.add(g.mul(sd, 1.6 * bump), g.mul(g.noise(60.0, 3, 0.6), 0.25 * bump))
    nb = g.bump(height, nrm, dist_px=1.2, strength=0.8)
    g.principled(col, rough, 0.0, nb, **{'Specular IOR Level': 0.22})
    MATS[name] = g.m
    return g.m


def mat_leather(name, tint=(0.085, 0.045, 0.028), scan_set='Leather014', rough=(0.42, 0.68), scan_px=380.0, bump=1.0, worn=(0.30, 0.19, 0.12)):
    if name in MATS: return MATS[name]
    g = NT(name)
    k = 100.0 / scan_px
    sc_ = g.image(scan(scan_set, 'color'), k, 'sRGB')
    sd = g.lum(g.image(scan(scan_set, 'disp'), k))
    sr = g.lum(g.image(scan(scan_set, 'rough'), k))
    l = g.lum(sc_)
    n1 = g.noise(5.0, 5, 0.6, dist=0.5)
    nrm, e = g.edge(2.0)
    ao = g.ao(10.0)
    cav = g.ramp(ao, 0.9, 0.45)
    col = g.mix(g.ramp(l, 0.02, 0.35), _sc(tint, 0.4), _sc(tint, 1.7))
    col = g.mix(g.ramp(n1, 0.35, 0.8, 0.0, 0.4), col, _sc(tint, 0.55))
    ew = g.clamp(g.mul(g.mul(e, g.ramp(g.noise(14.0, 4, 0.7), 0.3, 0.7, 0.3, 1.0)), 1.2))
    col = g.mix(g.mul(ew, 0.7), col, worn)
    col = g.mix(g.mul(cav, 0.8), col, _sc(tint, 0.1))
    rgh = g.add(g.ramp(sr, 0.2, 0.9, rough[0], rough[1]), g.mul(ew, -0.1))
    height = g.add(g.mul(sd, 1.5 * bump), g.mul(g.noise(70.0, 3, 0.6), 0.15 * bump))
    nb = g.bump(height, nrm, dist_px=1.0, strength=0.7)
    g.principled(col, g.clamp(rgh), 0.0, nb, **{'Specular IOR Level': 0.32})
    MATS[name] = g.m
    return g.m


def mat_ember(name, color=(1.0, 0.36, 0.06), strength=3.2, core=(1.0, 0.78, 0.35), body=(0.16, 0.02, 0.004)):
    """Ember stone: dark garnet body with glowing veins, glassy skin."""
    if name in MATS: return MATS[name]
    g = NT(name)
    n = g.noise(17.0, 5, 0.7, dist=2.2)
    v = g.ramp(n, 0.42, 0.74)
    v = g.mul(v, v)
    hot = g.mix(g.ramp(n, 0.6, 0.84), color, core)
    p = g.principled(body, 0.10, 0.0, None)
    g.put(p.inputs['Emission Color'], hot)
    # facets that lean away from the eye glow less: the table reads as the hot heart of the stone
    lw = g.node('ShaderNodeLayerWeight')
    lw.inputs['Blend'].default_value = 0.5
    face = g.ramp(lw.outputs['Facing'], 0.02, 0.55, 1.0, 0.22)
    g.put(p.inputs['Emission Strength'], g.mul(g.mul(g.add(v, 0.10), strength), face))
    try:
        p.inputs['Coat Weight'].default_value = 1.0
        p.inputs['Coat Roughness'].default_value = 0.06
    except KeyError:
        pass
    MATS[name] = g.m
    return g.m


def mat_glow(name, color, strength=2.0):
    """Self-lit inlay (hover rims, focus lines): gold that shines by itself."""
    if name in MATS: return MATS[name]
    g = NT(name)
    p = g.principled(color, 0.35, 1.0, None)
    g.put(p.inputs['Emission Color'], color)
    g.put(p.inputs['Emission Strength'], strength)
    MATS[name] = g.m
    return g.m


def mat_enamel(name, color=(0.80, 0.80, 0.80), rough=0.32):
    """Neutral, softly glossy enamel for layers that are tinted in the engine (rarity bands, node fills)."""
    if name in MATS: return MATS[name]
    g = NT(name)
    nrm, e = g.edge(1.4)
    n = g.noise(20.0, 3, 0.6)
    col = g.mix(g.ramp(n, 0.3, 0.8, 0.0, 0.12), color, _sc(color, 0.8))
    p = g.principled(col, rough, 0.0, nrm)
    try:
        p.inputs['Coat Weight'].default_value = 0.5
        p.inputs['Coat Roughness'].default_value = 0.15
    except KeyError:
        pass
    MATS[name] = g.m
    return g.m


def mat_wax(name, color=(0.80, 0.70, 0.52)):
    if name in MATS: return MATS[name]
    g = NT(name)
    nrm, e = g.edge(2.5)
    ao = g.ao(9.0)
    n = g.noise(9.0, 4, 0.6)
    col = g.mix(g.ramp(n, 0.3, 0.8, 0.0, 0.3), color, _sc(color, 0.7))
    col = g.mix(g.ramp(ao, 0.9, 0.4, 0.0, 0.8), col, (0.05, 0.035, 0.025))
    p = g.principled(col, 0.42, 0.0, g.bump(g.mul(n, 0.4), nrm, 1.0, 0.4))
    try:
        p.inputs['Subsurface Weight'].default_value = 0.55
        p.inputs['Subsurface Radius'].default_value = (0.04, 0.02, 0.01)
        p.inputs['Subsurface Scale'].default_value = 0.6
    except KeyError:
        pass
    MATS[name] = g.m
    return g.m


def mat_field(name, color=(0.02, 0.019, 0.02), rough=0.8, scan_set='slate_floor', scan_px=640.0, bump=0.6, spec=0.12):
    """Opaque dark backing rendered with the piece (slot floors, bar troughs)."""
    if name in MATS: return MATS[name]
    g = NT(name)
    k = 100.0 / scan_px
    try:
        sd = g.lum(g.image(scan(scan_set, 'disp'), k))
    except FileNotFoundError:
        sd = g.noise(20.0, 5, 0.6)
    n = g.noise(7.0, 5, 0.6)
    col = g.mix(g.ramp(n, 0.3, 0.8), _sc(color, 0.7), _sc(color, 1.4))
    col = g.mix(g.ramp(sd, 0.3, 0.8, 0.0, 0.5), col, _sc(color, 1.9))
    g.principled(col, rough, 0.0, g.bump(sd, None, 1.0, bump), **{'Specular IOR Level': spec})
    MATS[name] = g.m
    return g.m
