"""Currency, materials, containers, menu objects, paragon tokens / glyph stones, class crests."""
import bpy, bmesh, math, random
from mathutils import Vector, Euler, Matrix
from mathutils.geometry import tessellate_polygon
import iconlib as I
from iconlib import R
from registry import icon, reg
import props_items as PI

EMBER = (1.0, 0.42, 0.08)
PI2 = math.pi * 2.0

# COLLIDING IDS.  registry.ICONS is keyed by the id alone, and the map-marker set (props_markers.py, imported after
# this module) also has "quest", "chest" and "stash" (marker/quest.png ...), which would silently replace the misc
# icons of the same name.  They are therefore registered here as "./quest", "./chest", "./stash": a different key,
# the very same output file (<RAW>\misc\./chest.png == <RAW>\misc\chest.png).  Render them with
#   rb.ps1 ./quest ./chest ./stash force      (or  rb.ps1 misc: force  for the whole category)


# ------------------------------------------------------------------------------------------------ small helpers
def xf(o, loc=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
    """bake a transform into the mesh itself: the object stays at the origin, so the kit looks (object-space
    textures) see every part at its own place and no two copies carry the same rust pattern"""
    if isinstance(scale, (int, float)):
        scale = (scale,) * 3
    M = Matrix.LocRotScale(Vector(loc), Euler(tuple(R(a) for a in rot), 'XYZ'), Vector(scale))
    o.data.transform(M)
    o.data.update()
    return o


def pose(parts, yaw=0.0, tilt=0.0, roll=0.0, scale=1.0, loc=(0, 0, 0)):
    """turn the model about its own vertical axis (yaw), then tip its top toward the camera (tilt > 0 shows more of
    the top) and roll it in the picture plane"""
    inner = I.group(parts, "yaw"); I.place(inner, rot=(0, 0, yaw))
    outer = I.group([inner], "pose"); I.place(outer, loc=loc, rot=(tilt, roll, 0), scale=scale)
    return outer


def boxat(c, s, mat, bev=0.0, name="box", rot=None):
    o = I.box(s[0], s[1], s[2], mat, name, 0.0)
    xf(o, c, rot or (0, 0, 0))
    if bev:
        I.bevel(o, bev, 2)
    return o


def void():
    return I.emit((0, 0, 0), 0.0, key="void_black")


def plain(col, rough=0.7, metal=0.0, key=None):
    key = key or "plain%r%r%r" % (col, rough, metal)
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    b.inputs['Base Color'].default_value = tuple(col) + (1,)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    I._MATS[key] = m
    return m


def lum(display, s=2.6):
    """emissive material that lands on the colour `display` on screen: its strongest channel is over-driven s times
    (that is what post.py blooms), the others are scaled so they do NOT clip -- the hue survives instead of washing
    out to white"""
    mx = max(display)
    col = tuple((1.0 if c >= mx - 1e-6 else c / s) for c in display)
    return I.emit(col, s, key="lum%r%r" % (tuple(display), s))


def emberstone(display=(1.0, 0.24, 0.03), s=2.5, scale=45.0, vein=0.11, lo=0.22, key=None):
    """glowing ember: hot veins between cooler, dim-red crust islands.  Hue-safe like lum(): the strongest channel is
    over-driven, the others never clip, so it stays orange / red instead of washing out to yellow-white"""
    key = key or "emberstone%r%r%r%r%r" % (tuple(display), s, scale, vein, lo)
    if key in I._MATS:
        return I._MATS[key]
    mx = max(display)
    col = tuple((1.0 if c >= mx - 1e-6 else c / s) for c in display)
    m, nt, b = I._pbr(key)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    vo = nt.nodes.new('ShaderNodeTexVoronoi'); vo.feature = 'DISTANCE_TO_EDGE'; vo.inputs['Scale'].default_value = scale
    no = nt.nodes.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = scale * 0.7; no.inputs['Detail'].default_value = 3
    nt.links.new(tc.outputs['Object'], vo.inputs['Vector']); nt.links.new(tc.outputs['Object'], no.inputs['Vector'])
    mr = nt.nodes.new('ShaderNodeMapRange'); mr.interpolation_type = 'SMOOTHSTEP'
    mr.inputs[1].default_value = 0.0; mr.inputs[2].default_value = vein
    mr.inputs[3].default_value = s; mr.inputs[4].default_value = s * lo
    nt.links.new(vo.outputs['Distance'], mr.inputs[0])
    nm = nt.nodes.new('ShaderNodeMapRange')
    nm.inputs[1].default_value = 0.3; nm.inputs[2].default_value = 0.7; nm.inputs[3].default_value = 0.7; nm.inputs[4].default_value = 1.0
    nt.links.new(no.outputs[0], nm.inputs[0])
    mu = nt.nodes.new('ShaderNodeMath'); mu.operation = 'MULTIPLY'
    nt.links.new(mr.outputs[0], mu.inputs[0]); nt.links.new(nm.outputs[0], mu.inputs[1])
    b.inputs['Base Color'].default_value = (0.03, 0.012, 0.008, 1)
    b.inputs['Roughness'].default_value = 0.7
    b.inputs['Emission Color'].default_value = tuple(col) + (1,)
    nt.links.new(mu.outputs[0], b.inputs['Emission Strength'])
    bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.5; bump.inputs['Distance'].default_value = 0.003
    nt.links.new(vo.outputs['Distance'], bump.inputs['Height']); nt.links.new(bump.outputs[0], b.inputs['Normal'])
    I._MATS[key] = m
    return m


def orb(col, strength=3.0, key=None):
    """polished glowing cabochon: hot in the middle, darker toward the rim, glassy coat"""
    key = key or "orb%r%r" % (col, strength)
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    lw = nt.nodes.new('ShaderNodeLayerWeight'); lw.inputs[0].default_value = 0.5
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs[1].default_value = 0.0; mr.inputs[2].default_value = 1.0
    mr.inputs[3].default_value = strength; mr.inputs[4].default_value = strength * 0.22
    nt.links.new(lw.outputs['Facing'], mr.inputs[0])
    b.inputs['Base Color'].default_value = (col[0] * 0.35, col[1] * 0.35, col[2] * 0.35, 1)
    b.inputs['Roughness'].default_value = 0.12
    b.inputs['Coat Weight'].default_value = 1.0
    b.inputs['Coat Roughness'].default_value = 0.05
    b.inputs['Emission Color'].default_value = tuple(col) + (1,)
    nt.links.new(mr.outputs[0], b.inputs['Emission Strength'])
    I._MATS[key] = m
    return m


def grain_glow(display, s=2.4, scale=420.0, key=None):
    """glowing powder: fine bright and dim grains (hue-safe like lum())"""
    key = key or "grain%r%r%r" % (display, s, scale)
    if key in I._MATS:
        return I._MATS[key]
    mx = max(display)
    col = tuple((1.0 if c >= mx - 1e-6 else c / s) for c in display)
    m, nt, b = I._pbr(key)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    no = nt.nodes.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = scale; no.inputs['Detail'].default_value = 2
    nt.links.new(tc.outputs['Object'], no.inputs['Vector'])
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs[1].default_value = 0.34; mr.inputs[2].default_value = 0.70
    mr.inputs[3].default_value = s * 0.30; mr.inputs[4].default_value = s
    nt.links.new(no.outputs[0], mr.inputs[0])
    b.inputs['Base Color'].default_value = (display[0] * 0.25, display[1] * 0.25, display[2] * 0.25, 1)
    b.inputs['Roughness'].default_value = 0.9
    b.inputs['Emission Color'].default_value = tuple(col) + (1,)
    nt.links.new(mr.outputs[0], b.inputs['Emission Strength'])
    bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.8; bump.inputs['Distance'].default_value = 0.002
    nt.links.new(no.outputs[0], bump.inputs['Height']); nt.links.new(bump.outputs[0], b.inputs['Normal'])
    I._MATS[key] = m
    return m


def cr(points, sub=6, closed=False):
    """Catmull-Rom resampling of a polyline (2D or 3D tuples)"""
    P = [Vector(p) for p in points]
    n = len(P)
    out = []
    rng = range(n) if closed else range(n - 1)
    for i in rng:
        p0 = P[(i - 1) % n] if closed else P[max(i - 1, 0)]
        p1 = P[i]; p2 = P[(i + 1) % n] if closed else P[min(i + 1, n - 1)]
        p3 = P[(i + 2) % n] if closed else P[min(i + 2, n - 1)]
        for k in range(sub):
            t = k / sub
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    if not closed:
        out.append(P[-1])
    return out


def strap(points, w, t, mat, wax=(1, 0, 0), name="strap", power=5.0, seg=16):
    """flat band along a polyline; wax = direction of its width"""
    pts = [Vector(p) for p in points]
    n = len(pts); secs = []
    for i in range(n):
        tan = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        u = Vector(wax); u = (u - tan * u.dot(tan)).normalized(); v = tan.cross(u)
        secs.append((pts[i], w / 2, t / 2, (u, v), power))
    return I.loft(secs, seg, mat, name, caps=(True, True))


def lathe_w(profile, seg, mat, name="lathe", wob=None, cap=False):
    """lathe whose radius wobbles with the angle (wax blobs, hand-made rims): wob(angle) -> relative offset, applied
    in proportion to (r / rmax)^2"""
    rmax = max(r for r, z in profile) or 1.0
    n = len(profile); vs = []
    for j in range(seg):
        a = PI2 * j / seg
        k = wob(a) if wob else 0.0
        for (r, z) in profile:
            rr = r * (1.0 + k * (r / rmax) ** 2)
            vs.append((rr * math.cos(a), rr * math.sin(a), z))
    fs = []
    for j in range(seg):
        j2 = (j + 1) % seg
        for i in range(n - 1):
            fs.append((j * n + i, j2 * n + i, j2 * n + i + 1, j * n + i + 1))
    o = I.obj(name, vs, fs, mat, True)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    if cap:
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()
    return o


def face(o):
    """turn a lathe (axis +Z) so that its top looks at the camera (-Y)"""
    return xf(o, rot=(90, 0, 0))


def ring_prism(outer, inner, depth, mat, name="ringprism", bev=0.0):
    """plate with a hole: outer / inner are (x, z) outlines with the same point count; thickness along Y"""
    n = len(outer)
    vs = [(x, -depth / 2, z) for (x, z) in outer] + [(x, -depth / 2, z) for (x, z) in inner] + \
         [(x, depth / 2, z) for (x, z) in outer] + [(x, depth / 2, z) for (x, z) in inner]
    fs = []
    for i in range(n):
        j = (i + 1) % n
        fs.append((i, j, n + j, n + i))                       # front
        fs.append((2 * n + i, 3 * n + i, 3 * n + j, 2 * n + j))   # back
        fs.append((i, 2 * n + i, 2 * n + j, j))               # outer wall
        fs.append((n + i, n + j, 3 * n + j, 3 * n + i))       # inner wall
    o = I.obj(name, vs, fs, mat, False)
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    if bev:
        I.bevel(o, bev, 2)
    return o


def arc_slab(r0, r1, x0, x1, a0, a1, seg, mat, name="arc", sq=1.0, bev=0.0):
    """curved board / band: the arc runs in the YZ plane (angle 0 = +Y, 90 = +Z) between radii r0..r1, from x0 to x1"""
    vs = []
    for i in range(seg + 1):
        a = R(a0 + (a1 - a0) * i / seg)
        c, s = math.cos(a), math.sin(a) * sq
        vs += [(x0, r0 * c, r0 * s), (x1, r0 * c, r0 * s), (x1, r1 * c, r1 * s), (x0, r1 * c, r1 * s)]
    fs = []
    for i in range(seg):
        b = i * 4; c = b + 4
        for k in range(4):
            fs.append((b + k, b + (k + 1) % 4, c + (k + 1) % 4, c + k))
    fs.append((0, 1, 2, 3)); fs.append((seg * 4 + 3, seg * 4 + 2, seg * 4 + 1, seg * 4))
    o = I.obj(name, vs, fs, mat, False)
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    if bev:
        I.bevel(o, bev, 2)
    return o


def prism(outline, depth=0.01, mat=None, name="plate", bev=0.0):
    """prism from a 2D outline in the XZ plane (x, z), thickness along Y, centred -- same call as iconlib.extrude, but
    the caps are tessellated with mathutils (iconlib.extrude fills CONCAVE outlines wrongly: notches come out closed)"""
    n = len(outline)
    vs = [(x, -depth / 2, z) for (x, z) in outline] + [(x, depth / 2, z) for (x, z) in outline]
    fs = [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    for t in tessellate_polygon([[Vector((x, z, 0.0)) for (x, z) in outline]]):
        fs.append(tuple(t)); fs.append(tuple(i + n for i in reversed(t)))
    o = I.obj(name, vs, fs, mat, False)
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    if bev:
        I.bevel(o, bev, 2)
    return o


def rivet(p, mat, r=0.006, squash=(1, 1, 1)):
    o = I.ball(r, mat, "rivet", 2)
    xf(o, p, scale=squash)
    return o


def glints(n, lo, hi, r, col, seed=1, strength=6.0):
    return I.sparks(n, lo, hi, r=r, col=col, seed=seed, strength=strength)


# ================================================================================================ A. misc
def coin(mat, r=0.015, t=0.0046):
    h = t / 2; e = h * 0.55
    prof = [(0.0, e), (r * 0.24, e), (r * 0.30, h * 0.95), (r * 0.42, h * 0.95), (r * 0.48, e), (r * 0.74, e), (r * 0.81, h),
            (r * 0.95, h), (r, h * 0.66), (r, -h * 0.66), (r * 0.95, -h), (r * 0.81, -h), (r * 0.74, -e), (0.0, -e)]
    o = I.lathe(prof, 30, mat, "coin")
    I.flat(o, 38)
    return o


def coin_gold():
    return I.gold(color=(0.60, 0.42, 0.12), amount=0.34, scale=38.0, hammer=150.0, rough=0.30, edge_color=(0.98, 0.78, 0.36))


@icon("gold", "misc")
def gold():
    """金幣: two leaning stacks and a few loose coins of worn Sun-Forge gold"""
    gd = coin_gold()
    rnd = random.Random(7)
    parts = []
    t = 0.0046

    def stack(cx, cy, n, lean=(0.0, 0.0)):
        for k in range(n):
            c = coin(gd)
            xf(c, (cx + lean[0] * k + rnd.uniform(-0.0016, 0.0016), cy + lean[1] * k + rnd.uniform(-0.0016, 0.0016), t * (k + 0.5)),
               (rnd.uniform(-2.5, 2.5), rnd.uniform(-2.5, 2.5), rnd.uniform(0, 360)))
            parts.append(c)
    stack(-0.014, 0.012, 7, (0.0005, 0.0003))
    stack(0.019, 0.006, 4, (-0.0004, 0.0002))
    stack(0.003, -0.018, 2)
    for (x, y, z, rx, ry) in ((-0.036, -0.014, t * 0.5, 0, 0), (0.040, -0.016, t * 0.5, 0, 0), (-0.020, -0.026, t * 0.9, 9, -7),
                              (0.024, -0.028, t * 1.3, 14, 10)):
        c = coin(gd); xf(c, (x, y, z), (rx, ry, rnd.uniform(0, 360))); parts.append(c)
    # one coin standing on its edge against the tall stack, face to the viewer
    c = coin(gd); xf(c, (-0.038, 0.004, 0.0142), (72, 0, -28)); parts.append(c)
    pose(parts, yaw=0, tilt=34)
    bpy.context.view_layer.update()
    return dict(fill=0.80, samples=56)


def jag_plate(seed, size, thick, mat, name="scrap", asp=1.3):
    rnd = random.Random(seed)
    n = rnd.randint(6, 9)
    pts = []
    for i in range(n):
        a = PI2 * (i + rnd.uniform(-0.28, 0.28)) / n
        rr = size * rnd.uniform(0.5, 1.0)
        pts.append((rr * math.cos(a) * asp, rr * math.sin(a) / asp))
    return prism(pts, thick, mat, name, bev=thick * 0.22)


@icon("scrap", "misc")
def scrap():
    """碎鐵: a heap of torn, rusted plate, a bent forge nail and the snapped tip of a blade"""
    ir = I.iron(scale=34.0, amount=0.92, edge=0.22); ir2 = I.iron(scale=34.0, amount=0.7, color=(0.16, 0.155, 0.15), edge=0.3)
    dk = I.ashplate(scale=34.0, amount=0.6, edge=0.35, rust=(0.16, 0.075, 0.035))
    st = I.steel(scale=30.0, amount=0.30, edge=0.7)
    parts = []
    rnd = random.Random(23)
    n = 17
    for k in range(n):
        f = (k + 0.5) / n
        rho = 0.062 * math.sqrt(f); a = k * 2.39996 + 0.4
        x, y = rho * math.cos(a) * 1.15, rho * math.sin(a) * 0.85
        z = 0.006 + 0.040 * (1.0 - f) ** 1.3 * (0.75 + 0.25 * rnd.random())
        size = 0.040 - 0.014 * (1.0 - f) + rnd.uniform(-0.004, 0.004)
        pl = jag_plate(40 + k, size, rnd.uniform(0.0065, 0.009), (ir, ir2, ir, dk)[k % 4], asp=rnd.uniform(1.0, 1.5))
        xf(pl, rot=(90, 0, 0))
        lean = 16 + 34 * f
        xf(pl, (x, y, z), (rnd.uniform(-12, 12) - lean * math.sin(a), rnd.uniform(-12, 12) + lean * math.cos(a), rnd.uniform(0, 360)))
        parts.append(pl)
    # curved shell fragment of a breastplate, on the left
    sh = I.lathe([(0.034, 0.0), (0.041, 0.026), (0.036, 0.054)], 10, ir2, "shell", arc=95.0); I.solid(sh, 0.006)
    xf(sh, (-0.058, -0.022, 0.016), (-68, 24, 150)); parts.append(sh)
    # snapped blade tip stuck in the heap, point up-right
    w, Lb = 0.021, 0.125
    out = [(-w, 0.0), (-w * 0.94, Lb * 0.50), (-w * 0.6, Lb * 0.82), (0.0, Lb), (w * 0.6, Lb * 0.82), (w * 0.94, Lb * 0.50), (w, 0.006),
           (w * 0.5, -0.010), (w * 0.1, 0.005), (-w * 0.45, -0.009)]
    bl = prism(out, 0.0085, st, "tip", bev=0.0036)
    ridge = I.tube([(0, -0.0046, 0.006), (0, -0.0046, Lb * 0.78)], [0.0030, 0.0010], 6, st, "fuller")
    for o in (bl, ridge):
        xf(o, (0.012, -0.012, 0.030), (-30, 52, 0))
        parts.append(o)
    # hand-forged nail, square shank, bent, lying in front
    nail = I.tube([(0, 0, 0), (0, 0, 0.038), (0.006, 0, 0.056), (0.022, 0, 0.068), (0.040, 0, 0.072)], [0.0052, 0.0048, 0.0043, 0.0034, 0.0012], 4, ir2, "nail")
    head = I.cone(0.0120, 0.0078, 0.0058, ir2, "head", 8); xf(head, (0, 0, -0.0055))
    for o in (nail, head):
        xf(o, (-0.040, -0.062, 0.008), (84, 0, -74))
        parts.append(o)
    # a broken chain link on the right
    lk = I.torus(0.014, 0.0044, ir, "link", 20, 8, arc=290.0); xf(lk, scale=(1.4, 1, 1))
    xf(lk, (0.068, -0.030, 0.008), (10, -8, 40)); parts.append(lk)
    pose(parts, yaw=0, tilt=32)
    return dict(fill=0.80, samples=48)


def sack(mat, prof, seg=44, folds=9, thick=0.0028):
    """drawstring pouch: rows of (z, radius, wrinkle amount); open at the top, closed at the bottom"""
    vs = [(0.0, 0.0, prof[0][0])]
    for (z, r, w) in prof:
        for j in range(seg):
            a = PI2 * j / seg
            rr = r * (1.0 + w * math.cos(folds * a + z * 55.0) + w * 0.4 * math.sin((folds * 2 - 3) * a))
            vs.append((rr * math.cos(a), rr * math.sin(a), z))
    fs = []
    for j in range(seg):
        fs.append((0, 1 + (j + 1) % seg, 1 + j))
    for k in range(len(prof) - 1):
        for j in range(seg):
            a = 1 + k * seg + j; b = 1 + k * seg + (j + 1) % seg
            fs.append((a, b, b + seg, a + seg))
    o = I.obj("sack", vs, fs, mat, True)
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    I.solid(o, thick, -1.0)
    I.subsurf(o, 1)
    return o


@icon("dust", "misc")
def dust():
    """幽塵: a leather pouch tipped over, softly glowing violet-blue dust spilling from its mouth"""
    col = (0.34, 0.26, 1.0)
    lt = I.leather((0.27, 0.165, 0.085), scale=60.0)
    cord = I.leather((0.36, 0.28, 0.17), stitch=0.0)
    gm = grain_glow(col, 2.4)
    prof = [(0.000, 0.012, 0.0), (0.006, 0.030, 0.0), (0.020, 0.043, 0.01), (0.040, 0.048, 0.03), (0.058, 0.044, 0.07), (0.072, 0.034, 0.14),
            (0.082, 0.024, 0.24), (0.089, 0.0205, 0.26), (0.096, 0.024, 0.22), (0.106, 0.033, 0.15), (0.116, 0.040, 0.11), (0.121, 0.042, 0.10)]
    bag = sack(lt, prof)
    xf(bag, scale=(1.0, 1.0, 1.0))
    tie = I.torus(0.0225, 0.0032, cord, "tie", 40, 8); xf(tie, (0, 0, 0.089))
    knot = I.ball(0.006, cord, "knot", 2); xf(knot, (0.0, -0.025, 0.089))
    t1 = I.tube(cr([(0.002, -0.027, 0.089), (0.012, -0.040, 0.078), (0.020, -0.046, 0.058), (0.018, -0.050, 0.040)], 5), 0.0022, 6, cord, "tail")
    t2 = I.tube(cr([(-0.002, -0.027, 0.089), (-0.012, -0.042, 0.096), (-0.026, -0.048, 0.090), (-0.034, -0.050, 0.074)], 5), 0.0022, 6, cord, "tail")
    inner = I.ball(0.03, gm, "fillin", 3); xf(inner, (0, 0, 0.103), scale=(1.0, 1.0, 0.55))
    pouch = [bag, tie, knot, t1, t2, inner]
    for o in pouch:
        xf(o, scale=(1.0, 0.86, 1.0))                # it lies on its side: slightly flattened
        xf(o, (0, 0, 0.040), (0, 82, 0))
    parts = list(pouch)
    # the spill: a low fan-shaped mound in front of the mouth, loose grains round it
    for (x, y, z, r, sq) in ((0.142, -0.004, 0.0, 0.040, (1.15, 1.0, 0.40)), (0.116, 0.0, 0.004, 0.034, (1.2, 0.9, 0.62)),
                             (0.164, -0.016, 0.0, 0.024, (1.2, 1.0, 0.34)), (0.160, 0.018, 0.0, 0.022, (1.1, 1.0, 0.34))):
        m = I.ball(r, gm, "mound", 3); xf(m, (x, y, z), scale=sq)
        I.displace(m, 0.005, 0.02)
        parts.append(m)
    rnd = random.Random(5)
    for k in range(34):
        a = rnd.uniform(-2.0, 2.0); d = rnd.uniform(0.036, 0.066)
        rr = rnd.uniform(0.0016, 0.0040)
        gb = I.ball(rr, gm if k % 3 else lum(col, 2.6), "grain", 1)
        xf(gb, (0.146 + d * math.cos(a) * 1.1, -0.004 + d * math.sin(a), rr * 0.8))
        parts.append(gb)
    pose(parts, yaw=-20, tilt=24)
    bpy.context.view_layer.update()
    glints(6, (0.04, -0.14, 0.03), (0.15, -0.09, 0.10), 0.0020, (0.42, 0.34, 1.0), seed=3, strength=3.0)
    return dict(fill=0.80, samples=56)


def fib_sphere(n, rnd=None, jit=0.0):
    pts = []
    for i in range(n):
        z = 1 - 2 * (i + 0.5) / n
        r = math.sqrt(max(0.0, 1 - z * z)); a = i * 2.39996323
        v = Vector((r * math.cos(a), r * math.sin(a), z))
        if rnd and jit:
            v = (v + Vector((rnd.uniform(-jit, jit), rnd.uniform(-jit, jit), rnd.uniform(-jit, jit)))).normalized()
        pts.append(v)
    return pts


def cracked_ball(rad, mat, n=10, seed=3, shrink=0.88, samples=260, squash=(1, 1, 1), inner=0.5, bev=0.0016):
    """a boulder broken into n chunks with open gaps between them (something hot can sit inside)"""
    rnd = random.Random(seed)
    seeds = fib_sphere(n, rnd, 0.35)
    sm = fib_sphere(samples, rnd, 0.04)
    cells = [[] for _ in seeds]
    for p in sm:
        k = max(range(n), key=lambda i: p.dot(seeds[i]))
        cells[k].append(p)
    out = []
    for k, cell in enumerate(cells):
        if len(cell) < 4:
            continue
        lift = rnd.uniform(0.90, 1.08)
        pts = [p * rad * lift * rnd.uniform(0.95, 1.04) for p in cell]
        pts += [p * rad * inner for p in cell[::2]]
        cen = sum(pts, Vector()) / len(pts)
        pts = [cen + (p - cen) * shrink for p in pts]
        pts = [Vector((p.x * squash[0], p.y * squash[1], p.z * squash[2])) for p in pts]
        o = I.hull(pts, mat, "chunk")
        if bev:
            I.bevel(o, bev, 2, 25)
        out.append(o)
    return out


@icon("core", "misc")
def core():
    """燼核: a fist of black slag split apart by the molten ember inside it"""
    rk = I.rock(color=(0.04, 0.036, 0.035), scale=42.0, ash_amt=0.4, crack_w=0.03)
    parts = cracked_ball(0.046, rk, n=15, seed=9, shrink=0.948, squash=(1.0, 0.92, 0.9))
    hot = I.ball(0.040, emberstone((1.0, 0.20, 0.02), 2.5, 70.0, 0.16, 0.45), "hot", 3); xf(hot, scale=(1.0, 0.92, 0.9))
    parts.append(hot)
    pose(parts, yaw=20, tilt=14, roll=8)
    bpy.context.view_layer.update()
    glints(6, (-0.05, -0.07, 0.0), (0.06, -0.05, 0.075), 0.0011, (1.0, 0.6, 0.2), seed=4, strength=6.0)
    return dict(fill=0.78, samples=56)


def strap_rivets(parts, pts, mat, r=0.008, squash=(1, 1, 1)):
    for p in pts:
        parts.append(rivet(p, mat, r, squash))


@icon("./stash", "misc")        # see COLLIDING IDS at the top of this file
def stash():
    """a long, low, flat-lidded storage trunk: dark planks bound in riveted iron, hasp shut, bail handles"""
    W, D, Hb, Hl = 0.86, 0.46, 0.33, 0.115
    wd = I.wood((0.135, 0.088, 0.052), axis=(1, 0, 0)); wds = I.wood((0.125, 0.082, 0.05), axis=(0, 1, 0))
    ir = I.iron(amount=0.5, color=(0.20, 0.195, 0.19)); br = I.brass()
    parts = [boxat((0, 0, (Hb + Hl) / 2), (W - 0.04, D - 0.04, Hb + Hl - 0.03), void())]
    n = 3; ph = Hb / n
    for i in range(n):
        z = ph * (i + 0.5)
        for s in (-1, 1):
            parts.append(boxat((0, s * (D / 2 - 0.013), z), (W - 0.006, 0.026, ph - 0.006), wd, 0.005))
            parts.append(boxat((s * (W / 2 - 0.013), 0, z), (0.026, D - 0.056, ph - 0.006), wds, 0.005))
    zl = Hb + 0.004
    for s in (-1, 1):
        parts.append(boxat((0, s * (D / 2 - 0.006), zl + Hl * 0.42), (W + 0.022, 0.028, Hl * 0.84), wd, 0.005))
        parts.append(boxat((s * (W / 2 - 0.004), 0, zl + Hl * 0.42), (0.028, D - 0.04, Hl * 0.84), wds, 0.005))
    m = 4; pd = (D + 0.02) / m
    for j in range(m):
        parts.append(boxat((0, -D / 2 - 0.01 + pd * (j + 0.5), zl + Hl * 0.84 + 0.012), (W + 0.03, pd - 0.006, 0.028), wd, 0.006))
    ztop = zl + Hl * 0.84 + 0.026
    # iron: base shoe, corner posts, lid corner caps, two straps that run up the front and over the lid
    parts.append(boxat((0, 0, 0.004), (W + 0.03, D + 0.03, 0.052), ir, 0.006))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(boxat((sx * (W / 2 - 0.006), sy * (D / 2 - 0.006), Hb / 2 + 0.01), (0.044, 0.044, Hb - 0.02), ir, 0.005))
            parts.append(boxat((sx * (W / 2 + 0.002), sy * (D / 2 + 0.002), zl + Hl * 0.5 + 0.006), (0.066, 0.066, Hl + 0.022), ir, 0.006))
            parts.append(rivet((sx * (W / 2 + 0.002), sy * (D / 2 + 0.002) - 0.034, zl + Hl * 0.5), ir, 0.010))
    for x in (-0.25, 0.25):
        parts.append(boxat((x, -D / 2 - 0.004, Hb / 2 + 0.012), (0.066, 0.010, Hb - 0.02), ir, 0.003))
        parts.append(boxat((x, -D / 2 - 0.012, zl + Hl * 0.44), (0.066, 0.010, Hl * 0.92), ir, 0.003))
        parts.append(boxat((x, 0, ztop + 0.002), (0.066, D + 0.05, 0.010), ir, 0.003))
        strap_rivets(parts, [(x, -D / 2 - 0.010, z) for z in (0.07, 0.17, 0.27)], ir, 0.011)
        strap_rivets(parts, [(x, -D / 2 - 0.018, zl + Hl * 0.45)], ir, 0.011)
        strap_rivets(parts, [(x, y, ztop + 0.007) for y in (-0.17, -0.06, 0.06, 0.17)], ir, 0.011)
    # lock plate, hasp, ring
    parts.append(boxat((0, -D / 2 - 0.008, Hb - 0.075), (0.15, 0.016, 0.15), ir, 0.006))
    parts.append(boxat((0, -D / 2 - 0.022, Hb + 0.012), (0.062, 0.014, 0.15), ir, 0.005))
    kh = I.ball(0.014, void(), "keyhole", 2); xf(kh, (0, -D / 2 - 0.016, Hb - 0.115), scale=(1, 0.5, 1.5)); parts.append(kh)
    stp = I.torus(0.022, 0.0075, br, "staple", 24, 8); xf(stp, (0, -D / 2 - 0.03, Hb - 0.052), (90, 0, 0))
    parts.append(stp)
    strap_rivets(parts, [(sx * 0.058, -D / 2 - 0.016, Hb - 0.075 + sz * 0.056) for sx in (-1, 1) for sz in (-1, 1)], br, 0.008)
    # bail handles on the ends
    for s in (-1, 1):
        h = I.torus(0.062, 0.011, ir, "bail", 28, 8, arc=180.0)
        xf(h, rot=(-90, 0, 90)); xf(h, (s * (W / 2 + 0.024), 0, Hb * 0.66), (0, -s * 16, 0))
        parts.append(h)
        for y in (-0.062, 0.062):
            parts.append(boxat((s * (W / 2 + 0.006), y, Hb * 0.66), (0.022, 0.04, 0.04), ir, 0.005))
    pose(parts, yaw=-30, tilt=13)
    return dict(fill=0.82, samples=48)


@icon("./chest", "misc")        # see COLLIDING IDS at the top of this file
def chest():
    """a small brass-bound treasure chest, barrel lid half open, warm gold light pouring out"""
    W, D, Hb = 0.54, 0.36, 0.23
    Rl = D / 2; sq = 0.74; opn = 46.0
    wd = I.wood((0.16, 0.095, 0.05), axis=(1, 0, 0)); wds = I.wood((0.15, 0.09, 0.05), axis=(0, 1, 0))
    br = I.brass(); gd = coin_gold()
    glow = lum((1.0, 0.74, 0.26), 2.4)
    parts = [boxat((0, 0, Hb * 0.4), (W - 0.05, D - 0.05, Hb * 0.7), void())]
    n = 2; ph = Hb / n
    for i in range(n):
        z = ph * (i + 0.5)
        for s in (-1, 1):
            parts.append(boxat((0, s * (D / 2 - 0.012), z), (W - 0.006, 0.024, ph - 0.005), wd, 0.005))
            parts.append(boxat((s * (W / 2 - 0.012), 0, z), (0.024, D - 0.05, ph - 0.005), wds, 0.005))
    parts.append(boxat((0, 0, 0.0), (W + 0.026, D + 0.026, 0.044), br, 0.006))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(boxat((sx * (W / 2 - 0.005), sy * (D / 2 - 0.005), Hb / 2 + 0.008), (0.04, 0.04, Hb - 0.016), br, 0.005))
    parts.append(boxat((0, -D / 2 - 0.003, Hb - 0.016), (W + 0.012, 0.012, 0.034), br, 0.004))
    for x in (-0.17, 0.17):
        parts.append(boxat((x, -D / 2 - 0.004, Hb / 2), (0.05, 0.010, Hb - 0.01), br, 0.003))
        strap_rivets(parts, [(x, -D / 2 - 0.010, z) for z in (0.06, 0.135)], br, 0.009)
    parts.append(boxat((0, -D / 2 - 0.008, Hb - 0.07), (0.10, 0.016, 0.10), br, 0.005))
    kh = I.ball(0.011, void(), "keyhole", 2); xf(kh, (0, -D / 2 - 0.016, Hb - 0.075), scale=(1, 0.5, 1.5)); parts.append(kh)
    # treasure: a glowing heap with real coins on top
    heap = I.ball(0.2, glow, "heap", 3); xf(heap, (0, 0.0, Hb - 0.035), scale=(1.18, 0.74, 0.34)); I.displace(heap, 0.018, 0.05)
    parts.append(heap)
    rnd = random.Random(3)
    for k in range(16):
        c = coin(gd, 0.026, 0.008)
        x = rnd.uniform(-0.2, 0.2); y = rnd.uniform(-0.12, 0.06)
        z = Hb - 0.035 + 0.066 * math.sqrt(max(0.0, 1 - (x / 0.236) ** 2 - (y / 0.148) ** 2)) + 0.006
        xf(c, (x, y, z), (rnd.uniform(-35, 35), rnd.uniform(-35, 35), rnd.uniform(0, 360)))
        parts.append(c)
    # lid: curved staves between two end boards, three brass bands, hinged at the back
    lid = []
    ns = 7
    for i in range(ns):
        a0 = 180.0 * i / ns; a1 = 180.0 * (i + 1) / ns
        lid.append(arc_slab(Rl - 0.024, Rl, -W / 2 - 0.006, W / 2 + 0.006, a0 + 1.2, a1 - 1.2, 3, wd, "stave", sq, 0.004))
    for s in (-1, 1):
        pts = [((Rl - 0.02) * math.cos(R(a)), (Rl - 0.02) * math.sin(R(a)) * sq) for a in range(0, 181, 12)]
        e = prism(pts, 0.024, wds, "lidend", 0.004); xf(e, rot=(0, 0, 90)); xf(e, (s * (W / 2 - 0.008), 0, 0))
        lid.append(e)
        lid.append(arc_slab(Rl - 0.004, Rl + 0.010, s * (W / 2 + 0.008) - 0.024, s * (W / 2 + 0.008) + 0.024, 0, 180, 18, br, "band", sq, 0.003))
    lid.append(arc_slab(Rl - 0.004, Rl + 0.010, -0.03, 0.03, 0, 180, 18, br, "band", sq, 0.003))
    lid.append(boxat((0, -Rl - 0.004, 0.012), (W + 0.02, 0.012, 0.03), br, 0.004))
    lid.append(boxat((0, -Rl - 0.012, -0.012), (0.056, 0.012, 0.085), br, 0.004))            # hasp hanging from the lid
    lid.append(boxat((0, 0, 0.0), (W - 0.03, D - 0.03, 0.006), wd))                             # underside, catches the glow
    for a in (30, 62, 90, 118, 150):
        for x in (-W / 2 - 0.008, 0.0, W / 2 + 0.008):
            lid.append(rivet((x, (Rl + 0.010) * math.cos(R(a)), (Rl + 0.010) * math.sin(R(a)) * sq), br, 0.008))
    for o in lid:
        xf(o, (0, -Rl, 0)); xf(o, (0, D / 2, Hb + 0.004), (-opn, 0, 0))
    parts += lid
    pose(parts, yaw=-28, tilt=16)
    bpy.context.view_layer.update()
    glints(9, (-0.20, -0.30, 0.22), (0.16, -0.22, 0.46), 0.0045, (1.0, 0.82, 0.45), seed=6, strength=6.0)
    return dict(fill=0.82, samples=64)


@icon("key", "misc")
def key():
    """a heavy gate key of black iron: wheel bow with a small ember stone, banded shaft, deep-cut bit"""
    ir = I.iron(amount=0.5, color=(0.21, 0.205, 0.20), scale=30.0); br = I.brass(scale=30.0)
    parts = []
    zb = -0.052                                   # centre of the bow
    parts.append(xf(I.torus(0.024, 0.0068, ir, "bow", 40, 10), (0, 0, zb), (90, 0, 0)))
    parts.append(xf(I.torus(0.0095, 0.0038, ir, "hub", 24, 8), (0, 0, zb), (90, 0, 0)))
    for k in range(4):
        a = R(45 + 90 * k)
        parts.append(I.tube([(0.0105 * math.cos(a), 0, zb + 0.0105 * math.sin(a)), (0.021 * math.cos(a), 0, zb + 0.021 * math.sin(a))], 0.0034, 8, ir, "spoke"))
    for a in (180, 235, 305, 0):
        parts.append(rivet((0.032 * math.cos(R(a)), 0, zb + 0.032 * math.sin(R(a))), ir, 0.0066))
    st = I.ball(0.0062, orb(EMBER, 3.0), "stone", 2); xf(st, (0, -0.002, zb), scale=(1, 0.7, 1)); parts.append(st)
    # shaft with collars
    parts.append(I.lathe([(0.0, -0.030), (0.0072, -0.030), (0.0062, -0.022), (0.0056, 0.060), (0.0066, 0.064), (0.0066, 0.070), (0.0, 0.074)], 16, ir, "shaft"))
    for z, r in ((-0.022, 0.0098), (-0.012, 0.0086), (0.022, 0.0084)):
        parts.append(xf(I.lathe([(0.0056, -0.004), (r, -0.0028), (r, 0.0028), (0.0056, 0.004)], 16, br if z == -0.022 else ir, "collar"), (0, 0, z)))
    # bit: a thick plate with two wards cut into it
    out = [(0.003, 0.024), (0.040, 0.024), (0.040, 0.034), (0.022, 0.034), (0.022, 0.0415), (0.040, 0.0415), (0.040, 0.050), (0.017, 0.050),
           (0.017, 0.0575), (0.040, 0.0575), (0.040, 0.068), (0.003, 0.068)]
    parts.append(prism(out, 0.0100, ir, "bit", bev=0.0016))
    pose(parts, yaw=0, tilt=-6, roll=44)
    return dict(fill=0.84, samples=48)


def parchment():
    return I.look('leather', color=(0.27, 0.20, 0.11), scuff_color=(0.38, 0.30, 0.18), stitch=0.0, scuff=0.5, scale=30.0, rough=0.76)


def spiral_sheet(L, r0, r1, turns, mat, a_start=0.0, nseg=72, rows=5, ragged=0.004, seed=1, thick=0.0016, name="roll"):
    """rolled sheet: axis along Z, spiral cross-section from radius r0 (inside) to r1 (outside)"""
    rnd = random.Random(seed)
    jl = [0.0]; jh = [0.0]
    for j in range(nseg):
        jl.append(max(-ragged, min(ragged, jl[-1] + rnd.uniform(-ragged, ragged) * 0.6)))
        jh.append(max(-ragged, min(ragged, jh[-1] + rnd.uniform(-ragged, ragged) * 0.6)))
    vs = []
    for j in range(nseg + 1):
        t = j / nseg
        a = a_start + PI2 * turns * t; r = r0 + (r1 - r0) * t
        z0 = -L / 2 + jl[j] + ragged * 2.0 * t; z1 = L / 2 + jh[j] - ragged * 2.0 * t
        for k in range(rows + 1):
            vs.append((r * math.cos(a), r * math.sin(a), z0 + (z1 - z0) * k / rows))
    fs = []
    for j in range(nseg):
        for k in range(rows):
            a = j * (rows + 1) + k; b = a + rows + 1
            fs.append((a, b, b + 1, a + 1))
    o = I.obj(name, vs, fs, mat, True)
    I.solid(o, thick, 0.0)
    return o


# see COLLIDING IDS at the top of this file
@icon("./quest", "misc")
def quest():
    """a rolled parchment order, bound with a red ribbon under a wax seal"""
    pm = parchment()
    red = I.cloth((0.42, 0.035, 0.03)); wax = I.look('wax', color=(0.52, 0.035, 0.028), soot=0.04)
    L = 0.24; r1 = 0.030
    parts = [spiral_sheet(L, 0.007, r1, 3.3, pm, a_start=R(200), seed=2)]
    parts.append(I.lathe([(r1 + 0.0026, -0.014), (r1 + 0.0042, -0.006), (r1 + 0.0042, 0.006), (r1 + 0.0026, 0.014)], 40, red, "ribbon"))
    yf = -r1 - 0.0055
    seal = lathe_w([(0.0, 0.0062), (0.0046, 0.0062), (0.0062, 0.0086), (0.0096, 0.0086), (0.0112, 0.0062), (0.0150, 0.0058), (0.0185, 0.0070), (0.0222, 0.0052),
                    (0.0236, 0.0)], 40, wax, "seal", wob=lambda a: 0.07 * math.sin(3 * a + 0.5) + 0.05 * math.sin(7 * a + 1.3) + 0.03 * math.sin(11 * a))
    face(seal); xf(seal, (0, yf + 0.0035, 0)); parts.append(seal)
    dn = Vector((0.74, 0.0, -0.67))               # "down" in the roll's frame once it is laid on the diagonal
    side = Vector((0.67, 0.0, 0.74))
    for s, ln in ((-1, 0.085), (1, 0.074)):
        p0 = Vector((0, yf + 0.004, 0)) + dn * 0.012
        p1 = p0 + dn * ln * 0.45 + side * s * 0.010 + Vector((0, -0.006, 0))
        p2 = p0 + dn * ln + side * s * 0.026 + Vector((0, 0.002, 0))
        parts.append(strap(cr([p0, p1, p2], 6), 0.017, 0.0016, red, wax=tuple(side), name="tail", power=6.0))
    pose(parts, yaw=0, tilt=20, roll=48)
    return dict(fill=0.84, samples=48)


def ink():
    return plain((0.030, 0.016, 0.010), 0.9, key="ink2")


@icon("map", "misc")
def map_():
    """an unrolled field map: curled ends, a dashed route to an ember-marked place, peaks and a river (no writing)"""
    pm = parchment(); ik = ink()
    Hm = 0.20; xe = 0.112; rc = 0.031

    def curl(side):
        pts = []
        n = 48
        for i in range(n + 1):
            t = i / n
            r = rc - 0.017 * t
            a = R(90) - side * R(600.0) * t
            pts.append((side * xe + r * math.cos(a), -rc + r * math.sin(a)))
        return pts
    prof = list(reversed(curl(-1))) + [(-xe + 2 * xe * i / 16, 0.0) for i in range(1, 16)] + curl(1)
    rnd = random.Random(4)
    rag = 0.005
    vs = []; zl = 0.0; zh = 0.0
    for (x, y) in prof:
        zl = max(-rag, min(rag, zl + rnd.uniform(-rag, rag) * 0.5)); zh = max(-rag, min(rag, zh + rnd.uniform(-rag, rag) * 0.5))
        vs.append((x, y, -Hm / 2 + zl)); vs.append((x, y, 0.0)); vs.append((x, y, Hm / 2 + zh))
    fs = []
    for j in range(len(prof) - 1):
        for k in range(2):
            a = j * 3 + k; b = a + 3
            fs.append((a, b, b + 1, a + 1))
    sheet = I.obj("map", vs, fs, pm, True)
    I.solid(sheet, 0.0022, 0.0)
    parts = [sheet]
    y = -0.0014

    def stroke(pts, r=0.0040, mat=ik):
        parts.append(I.tube([(p[0], y, p[1]) for p in pts], r, 6, mat, "ink"))
    route = cr([(-0.086, -0.066), (-0.060, -0.030), (-0.020, -0.022), (0.012, 0.010), (0.040, 0.020), (0.058, 0.044)], 8)
    for i in range(0, len(route) - 4, 7):
        stroke([route[i], route[i + 4]], 0.0046)
    ring = [(0.070 + 0.017 * math.cos(PI2 * i / 18), 0.058 + 0.017 * math.sin(PI2 * i / 18)) for i in range(19)]
    stroke(ring, 0.0036)
    dot = I.ball(0.0125, lum((1.0, 0.20, 0.02), 3.2), "goal", 2); xf(dot, (0.070, y - 0.002, 0.058), scale=(1, 0.55, 1)); parts.append(dot)
    for (mx, mz, sc) in ((0.022, -0.060, 1.0), (0.058, -0.066, 1.3)):                                                # peaks
        stroke([(mx - 0.016 * sc, mz - 0.013 * sc), (mx, mz + 0.016 * sc), (mx + 0.016 * sc, mz - 0.013 * sc)], 0.0038)
    stroke(cr([(-0.098, 0.074), (-0.074, 0.050), (-0.054, 0.066), (-0.030, 0.042), (-0.010, 0.058)], 6), 0.0036)       # river
    pose(parts, yaw=-20, tilt=-34, roll=9)
    return dict(fill=0.84, samples=48)


@icon("inventory", "misc")
def inventory():
    """a traveller's leather knapsack: buckled flap, two straps, side pocket, bedroll on top"""
    lt = I.leather((0.20, 0.12, 0.062)); lf = I.leather((0.135, 0.082, 0.045)); ls = I.leather((0.09, 0.058, 0.035), stitch=0.0)
    br = I.brass(); cl = I.cloth((0.20, 0.19, 0.165))
    ax = (Vector((1, 0, 0)), Vector((0, 1, 0)))
    S = [((0, 0, 0.000), 0.150, 0.082, 3.2), ((0, 0, 0.012), 0.166, 0.096, 3.4), ((0, -0.004, 0.10), 0.176, 0.108, 3.2), ((0, -0.004, 0.22), 0.172, 0.104, 3.0),
         ((0, 0, 0.31), 0.158, 0.092, 2.8), ((0, 0.004, 0.36), 0.132, 0.074, 2.6), ((0, 0.006, 0.385), 0.090, 0.046, 2.4)]
    body = I.loft([(c, rx, ry, ax, pw) for (c, rx, ry, pw) in S], 44, lt, "body", caps=(True, True))
    parts = [body]
    # flap: over the top and down the front, tongue-shaped lower edge
    nu, nv = 22, 16
    vs = []
    for i in range(nu + 1):
        u = -1.0 + 2.0 * i / nu
        x = u * 0.158
        zend = 0.150 + 0.085 * abs(u) ** 3.2
        path = cr([(0.085, 0.330), (0.060, 0.392), (0.0, 0.408), (-0.070, 0.385), (-0.108, 0.330), (-0.119, 0.26), (-0.122, zend + 0.03), (-0.120, zend)], 2)
        path = path[:nv + 1] if len(path) > nv + 1 else path
        kx = (1.0 - min(0.97, abs(x / 0.182) ** 3.2)) ** (1.0 / 3.2)
        for (py, pz) in path:
            vs.append((x, py * (kx if py < 0 else 1.0), pz))
    npth = len(vs) // (nu + 1)
    fs = []
    for i in range(nu):
        for k in range(npth - 1):
            a = i * npth + k; b = a + npth
            fs.append((a, b, b + 1, a + 1))
    flap = I.obj("flap", vs, fs, lf, True)
    bm = bmesh.new(); bm.from_mesh(flap.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(flap.data); bm.free()
    I.solid(flap, 0.008, 0.0); parts.append(flap)
    # two straps with brass buckles
    for sx in (-0.078, 0.078):
        pts = cr([(sx, -0.072, 0.386), (sx, -0.116, 0.330), (sx, -0.128, 0.24), (sx, -0.130, 0.15), (sx, -0.118, 0.06), (sx, -0.100, 0.018)], 5)
        parts.append(strap(pts, 0.030, 0.006, ls, name="strap"))
        zb = 0.215
        fr = I.tube([(sx - 0.023, -0.134, zb - 0.018), (sx + 0.023, -0.134, zb - 0.018), (sx + 0.023, -0.134, zb + 0.018), (sx - 0.023, -0.134, zb + 0.018)], 0.0048, 8, br, "buckle", closed=True)
        parts.append(fr)
        parts.append(I.tube([(sx, -0.136, zb + 0.018), (sx, -0.139, zb - 0.016)], 0.0034, 6, br, "prong"))
        parts.append(strap([(sx, -0.133, 0.118), (sx, -0.136, 0.128)], 0.036, 0.008, ls, name="keeper"))
    # side pocket (right) and its small flap
    for s in (1,):
        pk = I.loft([((s * 0.178, 0, 0.04), 0.030, 0.058, (Vector((1, 0, 0)), Vector((0, 1, 0))), 3.0), ((s * 0.186, 0, 0.10), 0.036, 0.064, (Vector((1, 0, 0)), Vector((0, 1, 0))), 3.0),
                     ((s * 0.182, 0, 0.165), 0.032, 0.060, (Vector((1, 0, 0)), Vector((0, 1, 0))), 3.0), ((s * 0.176, 0, 0.18), 0.018, 0.045, (Vector((1, 0, 0)), Vector((0, 1, 0))), 3.0)],
                    28, lt, "pocket", caps=(True, True))
        parts.append(pk)
        pf = I.loft([((s * 0.186, 0, 0.13), 0.036, 0.066, (Vector((1, 0, 0)), Vector((0, 1, 0))), 3.0), ((s * 0.186, 0, 0.165), 0.037, 0.066, (Vector((1, 0, 0)), Vector((0, 1, 0))), 3.0),
                     ((s * 0.178, 0, 0.186), 0.022, 0.05, (Vector((1, 0, 0)), Vector((0, 1, 0))), 3.0)], 28, lf, "pocketflap", caps=(True, True))
        parts.append(pf)
        parts.append(rivet((s * 0.222, -0.010, 0.142), br, 0.008))
    # bedroll strapped on top
    roll = spiral_sheet(0.40, 0.012, 0.052, 3.0, cl, a_start=R(250), nseg=60, rows=6, ragged=0.004, seed=6, thick=0.006, name="bedroll")
    xf(roll, rot=(0, 90, 0)); xf(roll, (0, 0.004, 0.440)); parts.append(roll)
    for sx in (-0.105, 0.105):
        t = I.torus(0.056, 0.0052, ls, "rollstrap", 32, 6); xf(t, scale=(1, 1, 2.6)); xf(t, rot=(0, 90, 0)); xf(t, (sx, 0.004, 0.440))
        parts.append(t)
    # carry loop at the back and shoulder straps peeping out at the sides
    parts.append(I.tube(cr([(-0.045, 0.060, 0.375), (-0.03, 0.085, 0.44), (0.03, 0.085, 0.44), (0.045, 0.060, 0.375)], 5), 0.0075, 8, ls, "loop"))
    for s in (-1, 1):
        parts.append(strap(cr([(s * 0.09, 0.070, 0.36), (s * 0.18, 0.112, 0.27), (s * 0.20, 0.118, 0.14), (s * 0.15, 0.085, 0.03)], 6), 0.034, 0.007, ls,
                           wax=(s * 0.5, -1, 0), name="shoulder"))
    pose(parts, yaw=-24, tilt=10)
    return dict(fill=0.82, samples=48)


def gear_outline(teeth, r_root, r_tip, worn=()):
    out = []
    fr = (0.0, 0.17, 0.27, 0.40, 0.60, 0.73, 0.83, 0.92)
    hi = (0, 0, 1, 1, 1, 1, 0, 0)
    for t in range(teeth):
        rt = r_tip - (0.3 * (r_tip - r_root) if t in worn else 0.0)
        for f, h in zip(fr, hi):
            a = PI2 * (t + f) / teeth
            r = rt if h else r_root
            if t in worn and f in (0.60, 0.73):
                r -= 0.25 * (r_tip - r_root)
            out.append((r * math.cos(a), r * math.sin(a)))
    return out


def circle(r, n, c=(0.0, 0.0), a0=0.0):
    return [(c[0] + r * math.cos(a0 + PI2 * i / n), c[1] + r * math.sin(a0 + PI2 * i / n)) for i in range(n)]


@icon("settings", "misc")
def settings():
    """a worn iron cog: nine blunt teeth (one chewed), five spokes, riveted hub"""
    ir = I.iron(amount=0.47, color=(0.23, 0.225, 0.22)); st = I.steel(amount=0.4)
    teeth = 9; n = teeth * 8
    parts = [ring_prism(gear_outline(teeth, 0.082, 0.108, worn=(1,)), circle(0.060, n), 0.034, ir, "rim", 0.003)]
    parts.append(ring_prism(circle(0.034, 40), circle(0.0135, 40), 0.046, ir, "hub", 0.003))
    parts.append(ring_prism(circle(0.066, 48), circle(0.058, 48), 0.040, st, "lip", 0.002))
    for k in range(5):
        a = 90 + 72 * k
        sp = boxat((0.047, 0, 0), (0.034, 0.022, 0.024), ir, 0.004); xf(sp, rot=(0, -a, 0)); parts.append(sp)
        parts.append(rivet((0.0245 * math.cos(R(a + 36)), -0.023, 0.0245 * math.sin(R(a + 36))), st, 0.0048))
    axle = I.lathe([(0.0, 0.03), (0.011, 0.03), (0.0135, 0.026), (0.0135, -0.026), (0.0, -0.026)], 20, st, "axle"); face(axle); parts.append(axle)
    pose(parts, yaw=-30, tilt=-12, roll=8)
    return dict(fill=0.82, samples=48)


@icon("lock", "misc")
def lock():
    """an iron padlock: thick shackle, banded shield-shaped body, brass keyhole plate"""
    ir = I.iron(amount=0.5, color=(0.21, 0.205, 0.20), scale=24.0); st = I.steel(amount=0.35, scale=24.0); br = I.brass(scale=24.0)
    out = []
    for i in range(48):
        a = PI2 * i / 48
        c, s = math.cos(a), math.sin(a)
        x = 0.040 * math.copysign(abs(c) ** 0.62, c); z = 0.036 * math.copysign(abs(s) ** 0.62, s)
        if z < 0:
            x *= 1.0 - 0.30 * (-z / 0.036) ** 2.0
            z *= 1.12
        out.append((x, z))
    parts = [prism(out, 0.032, ir, "body", bev=0.005)]
    parts.append(prism([(-0.0405, 0.004), (0.0405, 0.004), (0.0405, 0.019), (-0.0405, 0.019)], 0.036, st, "band", bev=0.002))
    sh = cr([(-0.024, 0, 0.020), (-0.024, 0, 0.052), (-0.017, 0, 0.070), (0.0, 0, 0.078), (0.017, 0, 0.070), (0.024, 0, 0.052), (0.024, 0, 0.020)], 6)
    parts.append(I.tube(sh, 0.0082, 12, st, "shackle"))
    for sx in (-1, 1):
        parts.append(xf(I.lathe([(0.0115, 0.0), (0.0115, 0.008), (0.009, 0.011)], 16, ir, "ferrule", cap=True), (sx * 0.024, 0, 0.034)))
    esc = I.lathe([(0.0, 0.0030), (0.0135, 0.0030), (0.0160, 0.0), ], 28, br, "plate"); face(esc); xf(esc, (0, -0.0165, -0.010), scale=(1, 1, 1.25)); parts.append(esc)
    kh = I.ball(0.0052, void(), "kh", 2); xf(kh, (0, -0.0196, -0.006), scale=(1, 0.4, 1)); parts.append(kh)
    parts.append(boxat((0, -0.0196, -0.015), (0.0046, 0.002, 0.014), void()))
    for sx in (-1, 1):
        for z in (0.0205, -0.030):
            parts.append(rivet((sx * (0.030 if z > 0 else 0.022), -0.0175, z), st if z > 0 else ir, 0.0036))
    pose(parts, yaw=-24, tilt=-6, roll=-8)
    return dict(fill=0.82, samples=48)


# ================================================================================================ B. paragon: node tokens
def stud(prof, mat, seg=48, name="stud", sharp=None):
    o = I.lathe(prof, seg, mat, name, cap=True)
    if sharp:
        I.flat(o, sharp)
    return face(o)


def tok_pose(parts, yaw=-13, tilt=-7):
    return pose(parts, yaw=yaw, tilt=tilt)


@icon("node_normal", "paragon")
def node_normal():
    """plain dull iron stud"""
    ir = I.iron(amount=0.42, color=(0.25, 0.245, 0.24))
    parts = [stud([(0.0, 0.031), (0.016, 0.0295), (0.030, 0.024), (0.040, 0.015), (0.045, 0.0075), (0.047, 0.006), (0.057, 0.006), (0.060, 0.003),
                   (0.060, -0.006)], ir)]
    tok_pose(parts)
    return dict(fill=0.80, samples=40)


@icon("node_magic", "paragon")
def node_magic():
    """iron stud, raised collar, blue glowing stone"""
    ir = I.iron(amount=0.42, color=(0.25, 0.245, 0.24))
    parts = [stud([(0.0, 0.008), (0.025, 0.008), (0.027, 0.017), (0.033, 0.021), (0.041, 0.019), (0.047, 0.008), (0.057, 0.006), (0.060, 0.003),
                   (0.060, -0.006)], ir)]
    parts.append(stud([(0.0, 0.027), (0.011, 0.0255), (0.020, 0.019), (0.0255, 0.008)], orb((0.10, 0.46, 1.0), 2.3), 40, "stone"))
    tok_pose(parts)
    return dict(fill=0.80, samples=40)


@icon("node_rare", "paragon")
def node_rare():
    """eight-sided bronze stud with a square-cut yellow stone"""
    br = I.brass(amount=0.3, color=(0.46, 0.31, 0.10))
    base = I.lathe([(0.0, 0.009), (0.030, 0.009), (0.034, 0.019), (0.044, 0.017), (0.054, 0.008), (0.064, 0.006), (0.066, 0.0), (0.066, -0.007)], 8, br, "base", cap=True)
    I.flat(base, 20); xf(base, rot=(0, 0, 22.5)); face(base)
    gm = I.hull(PI.cut_round(0.027, 4, table=0.46, crown=0.40, pav=0.35), I.gem((1.0, 0.72, 0.10), 1.72, glow=0.75), "gem")
    xf(gm, rot=(0, 0, 45)); face(gm); xf(gm, (0, -0.016, 0))
    parts = [base, gm]
    tok_pose(parts)
    return dict(fill=0.80, samples=48)


@icon("node_legendary", "paragon")
def node_legendary():
    """black medallion, eight short spikes, gold collar, a strong ember heart"""
    ash = I.ashplate(color=(0.06, 0.06, 0.068)); gd = I.gold()
    parts = [stud([(0.0, 0.010), (0.030, 0.010), (0.033, 0.017), (0.045, 0.015), (0.052, 0.006), (0.052, -0.008)], ash)]
    for k in range(8):
        c = I.cone(0.0125, 0.0015, 0.027, ash, "spike", 4); I.flat(c, 20)
        xf(c, (0, 0, 0.047), (0, 0, 45)); xf(c, rot=(0, 45.0 * k, 0)); parts.append(c)
    ring = I.torus(0.0345, 0.0052, gd, "collar", 40, 10); face(ring); xf(ring, (0, -0.016, 0)); parts.append(ring)
    parts.append(stud([(0.0, 0.031), (0.012, 0.029), (0.022, 0.022), (0.030, 0.010)], emberstone((1.0, 0.22, 0.025), 2.6, 46.0, 0.13, 0.30), 40, "heart"))
    tok_pose(parts)
    return dict(fill=0.80, samples=48)


@icon("node_socket", "paragon")
def node_socket():
    """empty stone socket: an ash-grey ring round a dark hollow"""
    rk = I.rock(color=(0.115, 0.11, 0.105), scale=30.0, ash_amt=0.5, crack_w=0.03)
    parts = [stud([(0.0, 0.0), (0.027, 0.0), (0.030, 0.004), (0.031, 0.019), (0.035, 0.023), (0.048, 0.023), (0.056, 0.017), (0.060, 0.006), (0.060, -0.008)], rk)]
    hole = I.lathe([(0.0, 0.0012), (0.0285, 0.0012)], 40, void(), "hole"); face(hole); parts.append(hole)
    tok_pose(parts)
    return dict(fill=0.80, samples=40)


@icon("node_gate", "paragon")
def node_gate():
    """gold medallion with a raised arch; the doorway glows"""
    gd = I.gold(amount=0.3, color=(0.56, 0.40, 0.12)); gd2 = I.gold(amount=0.2, color=(0.66, 0.47, 0.14))
    parts = [stud([(0.0, 0.008), (0.047, 0.008), (0.050, 0.0145), (0.056, 0.0145), (0.060, 0.008), (0.060, -0.006)], gd)]
    y = -0.011
    arch = [(-0.021, y, -0.030), (-0.021, y, 0.004)] + [(0.021 * math.cos(R(a)), y, 0.004 + 0.021 * math.sin(R(a))) for a in range(170, -1, -10)] + [(0.021, y, -0.030)]
    parts.append(I.tube(arch, 0.0062, 10, gd2, "arch"))
    parts.append(I.tube([(-0.031, y, -0.032), (0.031, y, -0.032)], 0.0058, 10, gd2, "sill"))
    door = [(-0.017, -0.028), (-0.017, 0.004)] + [(0.017 * math.cos(R(a)), 0.004 + 0.017 * math.sin(R(a))) for a in range(170, -1, -10)] + [(0.017, -0.028)]
    d = prism(door, 0.004, lum((1.0, 0.42, 0.07), 2.6), "door"); xf(d, (0, -0.0095, 0)); parts.append(d)
    tok_pose(parts)
    return dict(fill=0.80, samples=40)


@icon("node_start", "paragon")
def node_start():
    """燼種: a glowing ember seed held by four prongs in an iron ring"""
    ir = I.iron(amount=0.45, color=(0.24, 0.235, 0.23))
    ring = I.torus(0.047, 0.0125, ir, "ring", 48, 12); face(ring)
    parts = [ring]
    for k in range(4):
        c = I.cone(0.0075, 0.0028, 0.022, ir, "prong", 8)
        xf(c, (0, 0, -0.040), (0, 0, 0)); xf(c, rot=(0, 45 + 90.0 * k, 0)); parts.append(c)
    seed = I.lathe([(0.0, -0.034), (0.0125, -0.026), (0.0200, -0.010), (0.0190, 0.004), (0.0105, 0.023), (0.0, 0.040)], 28,
                   emberstone((1.0, 0.26, 0.03), 2.6, 60.0, 0.17, 0.42), "seed")
    parts.append(seed)
    tok_pose(parts)
    return dict(fill=0.80, samples=40)


# ================================================================================================ B. paragon: glyph stones
ATTR = dict(str=(1.0, 0.17, 0.02), int=(0.04, 0.24, 1.0), dex=(0.06, 1.0, 0.11), wil=(0.36, 0.045, 1.0))   # before post.py's bloom lifts them


def stone_outline(kind, n=64):
    """unit outlines (x, z) of the glyph stones / slabs"""
    pts = []
    for i in range(n):
        a = PI2 * i / n
        c, s = math.cos(a), math.sin(a)

        def se(rx, rz, p):
            return rx * math.copysign(abs(c) ** (2.0 / p), c), rz * math.copysign(abs(s) ** (2.0 / p), s)
        if kind == 'round':
            x, z = se(1.0, 1.0, 2.0)
        elif kind == 'oval':
            x, z = se(1.18, 0.84, 2.3)
        elif kind == 'egg':
            x, z = se(0.90, 1.10, 2.0); x *= 1.0 - 0.13 * z
        elif kind == 'drop':
            x, z = se(1.0, 1.06, 2.2); x *= 1.0 - 0.26 * z
        elif kind == 'square':
            x, z = se(0.98, 0.98, 4.4)
        elif kind == 'chip':
            x, z = se(0.90, 1.08, 4.2)
            if x + z > 1.30:
                d = (x + z - 1.30) / 2.0; x -= d; z -= d
        elif kind == 'shield':
            x, z = se(0.98, 0.96, 3.8)
            if z < 0:
                t = -z / 0.96; x *= 1.0 - 0.62 * t ** 1.7; z *= 1.16
        elif kind == 'almond':
            x = 1.26 * c; z = 0.82 * math.copysign(abs(s) ** 1.45, s)
        elif kind == 'leaf':
            x0, z0 = 1.22 * c, 0.80 * s
            x = x0 * math.cos(0.42) - z0 * math.sin(0.42); z = x0 * math.sin(0.42) + z0 * math.cos(0.42)
        elif kind == 'tri':
            r = 0.88 + 0.20 * math.sin(3 * a)
            x, z = r * c * 1.10, r * s + 0.10
        elif kind == 'hex':
            r = 1.04 * math.cos(math.pi / 6) / math.cos((a % (math.pi / 3)) - math.pi / 6)
            x, z = r * c, r * s
        elif kind == 'tomb':
            x, z = se(0.94, 1.08, 2.0) if s >= 0 else se(0.94, 0.96, 5.0)
        else:
            x, z = c, s
        pts.append((x, z))
    return pts


def glyph_stone(kind, size=0.046, thick=0.024, mat=None, rough=0.0022):
    """flat rounded stone in the XZ plane, front face flat at y = -thick / 2"""
    out = stone_outline(kind)
    n = len(out)
    prof = [(0.5, 0.80), (0.43, 0.915), (0.24, 1.0), (-0.12, 1.0), (-0.36, 0.955), (-0.5, 0.875)]
    vs = []
    for (yf, sc) in prof:
        for (x, z) in out:
            vs.append((x * size * sc, yf * thick, z * size * sc))
    fs = []
    for k in range(len(prof) - 1):
        for i in range(n):
            a = k * n + i; b = k * n + (i + 1) % n
            fs.append((a, b, b + n, a + n))
    fs.append(tuple(range(n)))
    fs.append(tuple(range((len(prof) - 1) * n, len(prof) * n)))
    o = I.obj("stone", vs, fs, mat, True)
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    I.flat(o, 42)
    if rough:
        I.displace(o, rough, 0.035)
    return o


def groove(stone, parts, pts, r, yf, gmat, closed=False, taper=None):
    """carve a groove along pts (x, z) into the stone's front face (plane y = yf) and lay a glowing bed in it"""
    n = len(pts)
    rad = [r * (taper[i] if taper else 1.0) for i in range(n)]
    P = [Vector((x, 0.0, z)) for (x, z) in pts]
    if not closed:
        d0 = (P[0] - P[1]).normalized(); d1 = (P[-1] - P[-2]).normalized()
        P = [P[0] + d0 * rad[0] * 0.7] + P + [P[-1] + d1 * rad[-1] * 0.7]
        rad = [rad[0] * 0.55] + rad + [rad[-1] * 0.55]
    cut = I.tube([(p.x, yf, p.z) for p in P], list(rad), 10, None, "cut", closed=closed)
    if closed:
        I.weld(cut, 1e-6)
    cut.hide_render = True; cut["nofit"] = 1
    try:
        m = stone.modifiers.new("cut", 'BOOLEAN'); m.operation = 'DIFFERENCE'; m.object = cut
        m.solver = 'EXACT'               # (use_self=True crashed Blender 5.2 on the twelve-cut wall glyph)
    except Exception:
        pass
    glow = I.tube([(p.x, yf + q * 0.95, p.z) for p, q in zip(P, rad)], [q * 0.80 for q in rad], 8, gmat, "glow", closed=closed)
    parts += [cut, glow]


def check_cuts(stone, size):
    """a boolean that chokes on its cutter can eat the whole stone: verify the carved stone and switch off any cut that
    breaks it (its glowing bed then simply lies in the surface)"""
    bools = [m for m in stone.modifiers if m.type == 'BOOLEAN']

    def ok():
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        ev = stone.evaluated_get(dg); me = ev.to_mesh()
        n = len(me.vertices)
        good = False
        if n > 40:
            xs = [v.co.x for v in me.vertices]; zs = [v.co.z for v in me.vertices]
            good = (max(xs) - min(xs)) > size * 1.3 and (max(zs) - min(zs)) > size * 1.1
        ev.to_mesh_clear()
        return good
    if ok():
        return 0
    for m in bools:
        m.show_viewport = False; m.show_render = False
    bad = 0
    for m in bools:
        m.show_viewport = True; m.show_render = True
        if not ok():
            m.show_viewport = False; m.show_render = False; bad += 1
    if bad:
        print("ICON-NOTE groove fallback: %d cut(s) disabled" % bad)
    return bad


def arc2(cx, cz, r, a0, a1, n=20, rz=None):
    rz = r if rz is None else rz
    return [(cx + r * math.cos(R(a0 + (a1 - a0) * i / n)), cz + rz * math.sin(R(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]


def teardrop(cx, cz, w, h, n=28):
    """closed flame / droplet outline, point up"""
    out = []
    for i in range(n):
        t = PI2 * (i + 0.5) / n
        out.append((cx + w * math.sin(t) * math.sin(t / 2) ** 1.3, cz + h * math.cos(t)))
    return out


def segs(pts, closed=False):
    """a polyline with sharp corners as separate straight strokes"""
    out = [dict(p=[pts[i], pts[i + 1]]) for i in range(len(pts) - 1)]
    if closed:
        out.append(dict(p=[pts[-1], pts[0]]))
    return out


def motif(name):
    """the twelve pictograms in unit coordinates: lists of strokes dict(p=points, w=relative width, c=closed, t=taper)"""
    if name == 'steel':        # a blade snapped in two, with its guard
        return [dict(p=[(-0.74, -0.74), (-0.08, -0.08)], w=1.1), dict(p=[(-0.70, -0.20), (-0.20, -0.70)], w=1.0),
                dict(p=[(0.22, 0.02), (0.50, 0.36), (0.76, 0.70)], w=1.1, t=[1.0, 0.85, 0.4])]
    if name == 'kiln':         # the furnace heart: a flame with a coal in it
        half = [(0.0, 0.80)] + [(0.70 * math.sin(t) * math.sin(t / 2) ** 1.3, 0.84 * math.cos(t) - 0.06)
                                for t in [0.42 + (math.pi - 0.42) * i / 14.0 for i in range(15)]]
        return [dict(p=half), dict(p=[(-x, z) for (x, z) in half]), dict(p=[(0.0, -0.52), (0.0, -0.22), (0.0, 0.08)], t=[1.5, 1.1, 0.45])]
    if name == 'bastion':      # a crenellated wall
        w = [(-0.80, -0.56), (-0.80, 0.52), (-0.46, 0.52), (-0.46, 0.14), (-0.16, 0.14), (-0.16, 0.52), (0.16, 0.52), (0.16, 0.14), (0.46, 0.14),
             (0.46, 0.52), (0.80, 0.52), (0.80, -0.56)]
        return segs(w, True)
    if name == 'orbit':        # two orbits and their star
        return [dict(p=arc2(0, 0, 0.80, 95, 300, 26)), dict(p=arc2(0, 0, 0.45, -85, 120, 20)), dict(p=[(0.0, -0.03), (0.0, 0.03)], w=1.7)]
    if name == 'tide':         # three waves
        out = []
        for k, (z0, hw) in enumerate(((0.50, 0.62), (0.0, 0.82), (-0.50, 0.62))):
            out.append(dict(p=[(x / 14.0 * hw, z0 + 0.115 * math.sin(x / 14.0 * hw * 5.7 + k * 0.8)) for x in range(-14, 15)]))
        return out
    if name == 'thrall':       # two chain links
        out = []
        for (cx, cz) in ((-0.25, -0.25), (0.25, 0.25)):
            pts = []
            for i in range(28):
                a = PI2 * i / 28
                x0, z0 = 0.50 * math.cos(a), 0.29 * math.sin(a)
                pts.append((cx + (x0 - z0) * 0.7071, cz + (x0 + z0) * 0.7071))
            out.append(dict(p=pts, c=True))
        return out
    if name == 'eye':          # a hunter's eye
        up = [(x / 12.0 * 0.84, 0.50 * (1 - (x / 12.0) ** 2)) for x in range(-12, 13)]
        lo = [(x, -z) for (x, z) in up]
        return [dict(p=up), dict(p=lo), dict(p=[(0.0, -0.04), (0.0, 0.04)], w=1.9)]
    if name == 'gale':         # three gusts
        return [dict(p=[(-0.62, 0.34), (0.20, 0.34)] + arc2(0.20, 0.56, 0.22, -90, 150, 14)[1:]),
                dict(p=[(-0.84, 0.0), (0.30, 0.0), (0.70, 0.0)], t=[1.0, 1.0, 0.5]),
                dict(p=[(-0.52, -0.34), (0.08, -0.34)] + arc2(0.08, -0.56, 0.22, 90, -150, 14)[1:])]
    if name == 'venom':        # a fang and the drop that falls from it
        return [dict(p=cr([(-0.58, 0.76), (-0.52, 0.22), (-0.22, -0.16), (0.20, -0.34)], 6), t=[1.0] * 13 + [0.9, 0.8, 0.7, 0.6, 0.5, 0.4]),
                dict(p=cr([(0.26, 0.76), (0.06, 0.36), (0.02, 0.06), (0.20, -0.34)], 6), t=[1.0] * 13 + [0.9, 0.8, 0.7, 0.6, 0.5, 0.4]),
                dict(p=[(-0.58, 0.76), (0.26, 0.76)]), dict(p=[(0.16, -0.54), (0.15, -0.66), (0.14, -0.80)], t=[0.5, 1.1, 1.5])]
    if name == 'vigil':        # a watch-flame inside its ring
        return [dict(p=[(0.80 * math.cos(PI2 * i / 36), 0.80 * math.sin(PI2 * i / 36)) for i in range(36)], c=True),
                dict(p=[(0.0, -0.36), (0.0, -0.14), (0.02, 0.14), (0.0, 0.42)], t=[1.5, 2.0, 1.25, 0.4])]
    if name == 'surge':        # a spiral
        pts = []
        for i in range(57):
            t = i / 56.0
            a = PI2 * 2.05 * t + 0.6; r = 0.12 + 0.68 * t
            pts.append((r * math.cos(a), r * math.sin(a)))
        return [dict(p=pts, t=[0.55 + 0.45 * min(1.0, i / 12.0) for i in range(57)])]
    if name == 'reaper':       # a scythe blade over the cut line
        arc = arc2(0.0, -0.44, 0.98, 38, 150, 22)
        return [dict(p=arc, t=[1.45 - 1.1 * (i / 22.0) ** 1.4 for i in range(23)]), dict(p=[(-0.64, -0.60), (0.64, -0.60)])]
    if name == 'paragon':      # board sigil: a hub, three paths to three seats, three wards between them
        out = [dict(p=[(0.20 * math.cos(PI2 * i / 20), 0.20 * math.sin(PI2 * i / 20)) for i in range(20)], c=True)]
        for a in (90, 210, 330):
            ca, sa = math.cos(R(a)), math.sin(R(a))
            out.append(dict(p=[(0.26 * ca, 0.26 * sa), (0.50 * ca, 0.50 * sa), (0.66 * ca, 0.66 * sa)], t=[0.9, 0.9, 1.7]))
        for a in (30, 150, 270):
            out.append(dict(p=arc2(0, 0, 0.60, a - 24, a + 24, 8)))
        return out
    return []


def build_glyph(name, attr, outline, yaw=-16, tilt=-12, roll=0.0, size=0.046, thick=0.024, r=0.0046, ms=0.66):
    rk = I.rock(color=(0.046, 0.043, 0.043), scale=26.0, ash_amt=0.38, crack_w=0.028)
    st = glyph_stone(outline, size, thick, rk)
    gm = lum(ATTR[attr], 2.6)
    parts = [st]
    yf = -thick / 2
    k = size * ms
    for sd in motif(name):
        pts = [(x * k, z * k) for (x, z) in sd['p']]
        groove(st, parts, pts, r * sd.get('w', 1.0), yf, gm, closed=sd.get('c', False), taper=sd.get('t'))
    check_cuts(st, size)
    pose(parts, yaw=yaw, tilt=tilt, roll=roll)
    return dict(fill=0.80, samples=48)


GLYPHS = dict(
    g_steel=('str', 'chip', 0.0), g_kiln=('str', 'drop', 0.0), g_bastion=('str', 'shield', 0.0),
    g_orbit=('int', 'round', 0.0), g_tide=('int', 'oval', 0.0), g_thrall=('int', 'square', 0.0),
    g_eye=('dex', 'almond', 0.0), g_gale=('dex', 'leaf', 0.0), g_venom=('dex', 'tri', 0.0),
    g_vigil=('wil', 'egg', 0.0), g_surge=('wil', 'hex', 0.0), g_reaper=('wil', 'tomb', 0.0))
for _g, (_a, _o, _r) in GLYPHS.items():
    reg(_g, "paragon", (lambda g=_g, a=_a, o=_o, r=_r: build_glyph(g[2:], a, o, roll=r)))


@icon("paragon", "misc")
def paragon():
    """a hexagonal slab of dark stone, the board sigil cut into it and burning ember-orange"""
    rk = I.rock(color=(0.046, 0.043, 0.043), scale=20.0, ash_amt=0.4, crack_w=0.028)
    size, thick = 0.092, 0.036
    st = glyph_stone('hex', size, thick, rk, rough=0.003)
    gm = lum((1.0, 0.20, 0.02), 2.6)
    parts = [st]
    k = size * 0.74
    for sd in motif('paragon'):
        groove(st, parts, [(x * k, z * k) for (x, z) in sd['p']], 0.0062 * sd.get('w', 1.0), -thick / 2, gm, closed=sd.get('c', False), taper=sd.get('t'))
    check_cuts(st, size)
    pose(parts, yaw=-20, tilt=-14, roll=0)
    return dict(fill=0.80, samples=48)


# ================================================================================================ C. class crests + title emblem
CREST = dict(
    emberblade=("greatsword", (1.0, 0.5, 0.15)), astromancer=("staff", (0.4, 0.7, 1.0)), hunter=("crossbow", (0.25, 0.85, 0.7)),
    soulbinder=("scythe", (0.7, 0.9, 0.6)), wildsoul=("club", (0.55, 0.85, 0.4)), forgebody=("gauntlet", (1.0, 0.38, 0.12)),
    lampknight=("swordshield", (1.0, 0.85, 0.45)), pactbinder=("grimoire", (0.9, 0.25, 0.45)))


def saturate(col, k=1.4):
    l = 0.2126 * col[0] + 0.7152 * col[1] + 0.0722 * col[2]
    return tuple(max(0.0, min(1.0, l + (c - l) * k)) for c in col)


def deepen(col, k=0.8):
    """pull the weaker channels down a little: the warm key light and the bloom both wash colours out"""
    mx = max(col)
    return tuple(c if c >= mx - 1e-6 else c * k for c in col)


def enamel(col, glow=0.20, raw=None):
    """vitreous enamel: deep glossy colour, a little light of its own so the class hue survives the warm key light,
    chipped here and there down to the dark metal"""
    key = "enamel%r%r%r" % (tuple(col), glow, raw)
    if key in I._MATS:
        return I._MATS[key]
    c = tuple(raw) if raw else deepen(saturate(col, 1.2), 0.8)
    m, nt, b = I._pbr(key)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    no = nt.nodes.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = 15.0; no.inputs['Detail'].default_value = 6; no.inputs['Roughness'].default_value = 0.68
    nt.links.new(tc.outputs['Object'], no.inputs['Vector'])
    chip = nt.nodes.new('ShaderNodeMapRange'); chip.inputs[1].default_value = 0.635; chip.inputs[2].default_value = 0.665
    nt.links.new(no.outputs[0], chip.inputs[0])
    n2 = nt.nodes.new('ShaderNodeTexNoise'); n2.inputs['Scale'].default_value = 5.0; n2.inputs['Detail'].default_value = 2
    nt.links.new(tc.outputs['Object'], n2.inputs['Vector'])
    dp = nt.nodes.new('ShaderNodeMix'); dp.data_type = 'RGBA'
    nt.links.new(n2.outputs[0], dp.inputs[0])
    dp.inputs[6].default_value = (c[0] * 0.26, c[1] * 0.26, c[2] * 0.26, 1); dp.inputs[7].default_value = (c[0] * 0.46, c[1] * 0.46, c[2] * 0.46, 1)
    mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'
    nt.links.new(chip.outputs[0], mx.inputs[0]); nt.links.new(dp.outputs[2], mx.inputs[6]); mx.inputs[7].default_value = (0.03, 0.03, 0.034, 1)
    nt.links.new(mx.outputs[2], b.inputs['Base Color'])
    ro = nt.nodes.new('ShaderNodeMapRange'); ro.inputs[3].default_value = 0.13; ro.inputs[4].default_value = 0.62
    nt.links.new(chip.outputs[0], ro.inputs[0]); nt.links.new(ro.outputs[0], b.inputs['Roughness'])
    es = nt.nodes.new('ShaderNodeMapRange'); es.inputs[3].default_value = glow; es.inputs[4].default_value = 0.0
    nt.links.new(chip.outputs[0], es.inputs[0]); nt.links.new(es.outputs[0], b.inputs['Emission Strength'])
    b.inputs['Emission Color'].default_value = tuple(c) + (1,)
    b.inputs['Coat Weight'].default_value = 0.8
    b.inputs['Coat Roughness'].default_value = 0.07
    bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.5; bump.inputs['Distance'].default_value = 0.0015; bump.invert = True
    nt.links.new(chip.outputs[0], bump.inputs['Height']); nt.links.new(bump.outputs[0], b.inputs['Normal'])
    I._MATS[key] = m
    return m


def medallion(col, raw=None):
    """the shared crest body, face to the camera (-Y): forged iron field, enamel ring between two gold beads, heavy
    tarnished-gold rim with studs, the class stone in a bezel at the foot"""
    fe = I.iron(amount=0.26, color=(0.050, 0.050, 0.056), rough=0.5, rust=(0.11, 0.075, 0.055), rust2=(0.04, 0.03, 0.026), edge=0.55,
                edge_color=(0.30, 0.30, 0.33))
    bz = I.gold(amount=0.56); bz2 = I.brass(amount=0.4)
    en = enamel(col, raw=raw)
    parts = [stud([(0.0, 0.016), (0.10, 0.0145), (0.170, 0.010), (0.178, 0.005), (0.228, 0.005), (0.228, -0.020), (0.262, -0.020), (0.262, -0.036),
                   (0.246, -0.046)], fe, 80, "disc")]
    rim = I.lathe([(0.226, 0.0), (0.227, 0.024), (0.234, 0.032), (0.251, 0.032), (0.262, 0.023), (0.267, 0.006), (0.267, -0.024), (0.226, -0.024), (0.226, 0.0)],
                  80, bz, "rim"); face(rim); parts.append(rim)
    ring = I.lathe([(0.1805, 0.004), (0.1805, 0.0125), (0.2255, 0.0125), (0.2255, 0.004), (0.1805, 0.004)], 80, en, "enamel"); face(ring); parts.append(ring)
    bead = I.torus(0.1765, 0.0066, bz, "bead", 80, 10); face(bead); xf(bead, (0, -0.0105, 0)); parts.append(bead)
    for k in range(8):
        d = boxat((0.203, -0.0135, 0), (0.046, 0.007, 0.012), bz, 0.002); xf(d, rot=(0, 22.5 + 45.0 * k, 0)); parts.append(d)
    for k in range(16):
        a = 22.5 * k
        if abs(a - 270.0) < 20:
            continue
        parts.append(rivet((0.2425 * math.cos(R(a)), -0.0325, 0.2425 * math.sin(R(a))), bz, 0.0072, (1, 0.6, 1)))
    zc = -0.247
    bez = I.lathe([(0.031, 0.0), (0.043, 0.0), (0.046, 0.011), (0.039, 0.019), (0.031, 0.016), (0.031, 0.0)], 32, bz2, "bezel"); face(bez); xf(bez, (0, -0.030, zc))
    gm = I.hull(PI.cut_round(0.034, 8, table=0.52, crown=0.36, pav=0.30), I.gem(deepen(saturate(col, 1.25), 0.7), 1.72, glow=0.55), "stone"); face(gm); xf(gm, (0, -0.046, zc))
    parts += [bez, gm]
    return parts


def wbounds(o):
    co = [v.co for v in o.data.vertices]
    return [(min(c[i] for c in co), max(c[i] for c in co)) for i in range(3)]


def mount(o, parts, length, widen=1.0, roll=45.0, at=(0.0, 0.0), y0=-0.034, by='z', inflate=0.0):
    """scale a hero weapon (long axis +Z, outward face to -Y) to `length`, thicken it, lay it against the plane
    y = y0 rolled by `roll` degrees; returns the depth it takes up"""
    bx, byy, bz = wbounds(o)
    if inflate:
        I.inflate(o, inflate)
    span = (bz[1] - bz[0]) if by == 'z' else max(bz[1] - bz[0], bx[1] - bx[0])
    k = length / span
    ky = k * widen
    I.place(o, loc=(at[0], y0 - ky * (byy[1] + inflate), at[1]), rot=(0, roll, 0), scale=(ky, ky, k))
    parts.append(o)
    return ky * (byy[1] - byy[0] + 2 * inflate)


def glow_up(o, col, strength):
    """let the emissive marks of a hero prop burn a little brighter in the class colour"""
    for i, m in enumerate(o.data.materials):
        if m is not None and 'emis' in m.name:
            o.data.materials[i] = I.tint_material(m, "crest%r" % (strength,), emit=col, emit_strength=strength)


def crest_weapon(kind, parts, col=(1.0, 0.5, 0.15)):
    main, off = PI.weapon_parts(kind, 3)
    if kind == "greatsword":
        mount(main, parts, 0.64, 2.3)
    elif kind == "staff":
        mount(main, parts, 0.65, 3.0, inflate=0.007)
    elif kind == "scythe":
        mount(main, parts, 0.63, 2.3, inflate=0.006)
    elif kind == "club":
        mount(main, parts, 0.60, 1.7)
    elif kind == "crossbow":
        if off is not None:
            bpy.data.objects.remove(off, do_unlink=True)
        mount(main, parts, 0.52, 1.0, roll=42, by='max', inflate=0.010)
    elif kind == "gauntlet":
        mount(main, parts, 0.37, 1.0, roll=18, at=(0.082, 0.0), y0=-0.019)
        if off is not None:
            mount(off, parts, 0.37, 1.0, roll=-18, at=(-0.082, 0.0), y0=-0.019)
    elif kind == "swordshield":          # the sword lies across the rim, the shield is hung over it
        d = mount(main, parts, 0.68, 1.9, roll=45)
        mount(off, parts, 0.43, 1.0, roll=-7, at=(0.012, -0.004), y0=-0.034 - d - 0.003)
    elif kind == "grimoire":             # the wand lies across the rim, the chained book over it
        d = mount(main, parts, 0.66, 1.5, roll=45)
        if off is not None:
            glow_up(off, col, 1.6)
            mount(off, parts, 0.40, 1.0, roll=-12, at=(0.006, 0.0), y0=-0.034 - d - 0.003)


# enamel / stone tints: the class colours, with the two orange classes pulled apart (amber vs forge red)
TINT = dict(emberblade=(1.0, 0.56, 0.10), forgebody=(1.0, 0.24, 0.05), soulbinder=(0.80, 0.97, 0.74), wildsoul=(0.46, 0.85, 0.16))
RAW = dict(soulbinder=(0.60, 0.86, 0.60), wildsoul=(0.30, 0.78, 0.10))      # enamel colour used as it is (pale ghost green vs leaf green)


def build_crest(cid):
    kind, col = CREST[cid]
    parts = medallion(TINT.get(cid, col), RAW.get(cid))
    crest_weapon(kind, parts, col)
    pose(parts, yaw=-14, tilt=-6)
    return dict(fill=0.86, samples=36, res=1024)


for _c in CREST:
    reg(_c, "crest", (lambda c=_c: build_crest(c)))


@icon("title", "crest")
def title():
    """燼淵 emblem: the dead Sun-Forge -- a cracked iron sun with short blunt rays, ash-grey, the last ember deep in
    its fractured heart"""
    ashm = I.iron(color=(0.125, 0.125, 0.13), amount=0.62, rust=(0.165, 0.16, 0.156), rust2=(0.06, 0.058, 0.056), scale=7.0)
    dark = I.iron(color=(0.085, 0.085, 0.09), amount=0.45, rust=(0.13, 0.125, 0.12), rust2=(0.05, 0.048, 0.046))
    rnd = random.Random(31)
    parts = []
    Rr, Ri = 0.204, 0.160
    parts.append(stud([(0.0, -0.006), (Ri + 0.008, -0.006), (Ri + 0.008, -0.030), (Rr - 0.004, -0.046), (0.12, -0.058)], dark, 72, "back"))
    # the ring, broken into four unequal arcs
    cuts = [24.0, 118.0, 196.0, 291.0]
    ringp = [(Ri, -0.036), (Ri, 0.022), (Ri + 0.008, 0.031), (Rr - 0.012, 0.031), (Rr, 0.020), (Rr, -0.036), (Ri, -0.036)]
    for i, a0 in enumerate(cuts):
        a1 = cuts[(i + 1) % 4] + (360.0 if i == 3 else 0.0)
        seg = I.lathe(ringp, max(8, int((a1 - a0) / 5)), ashm, "ringarc", arc=a1 - a0 - 2.4, cap=True)
        I.flat(seg, 40)
        xf(seg, rot=(0, 0, a0 + 1.2)); face(seg)
        xf(seg, (rnd.uniform(-0.003, 0.003), rnd.uniform(-0.004, 0.003), rnd.uniform(-0.003, 0.003)), (rnd.uniform(-1.5, 1.5), 0, rnd.uniform(-1.5, 1.5)))
        parts.append(seg)
    # sixteen short blunt rays, long and short by turns, tapering like wedges (square ones read as a cog);
    # one snapped off, one knocked askew
    for k in range(16):
        a = 22.5 * k + 11.25
        big = k % 2 == 0
        L = 0.090 if big else 0.048
        wb, wt = (0.036, 0.0110) if big else (0.026, 0.009)
        if k == 6:
            out = [(Rr - 0.010, -wb), (Rr + 0.034, -wb * 0.70), (Rr + 0.022, -0.008), (Rr + 0.040, 0.006), (Rr + 0.026, wb * 0.74), (Rr - 0.010, wb)]
        else:
            out = [(Rr - 0.010, -wb), (Rr + L, -wt), (Rr + L + 0.005, 0.0), (Rr + L, wt), (Rr - 0.010, wb)]
        ray = prism(out, 0.040 if big else 0.032, ashm, "ray", bev=0.006)
        if k == 12:
            xf(ray, (0.008, -0.004, -0.010), (0, 0, 0)); xf(ray, rot=(0, -(a + 6.0), 0))
        else:
            xf(ray, rot=(0, -a, 0))
        parts.append(ray)
    # the broken face: wedge plates between jagged cracks that run out from an off-centre heart
    c0 = Vector((0.010, -0.006)); nc = 7
    Ro = Ri + 0.006
    ang = [PI2 * k / nc + rnd.uniform(-0.24, 0.24) + 0.5 for k in range(nc)]
    cracks = []
    for k in range(nc):
        d = Vector((math.cos(ang[k]), math.sin(ang[k]))); pp = Vector((-d.y, d.x))
        r_in = rnd.uniform(0.046, 0.074)
        r_out = Ro - c0.dot(d)
        line = []
        for j in range(5):
            t = j / 4.0
            p = c0 + d * (r_in + (r_out - r_in) * t) + pp * (rnd.uniform(-0.013, 0.013) if 0 < j < 4 else 0.0)
            line.append(p)
        cracks.append(line)
    for k in range(nc):
        A = cracks[k]; B = cracks[(k + 1) % nc]
        a0 = math.atan2(A[-1].y, A[-1].x); a1 = math.atan2(B[-1].y, B[-1].x)
        if a1 < a0:
            a1 += PI2
        outer = [Vector((Ro * math.cos(a0 + (a1 - a0) * j / 7.0), Ro * math.sin(a0 + (a1 - a0) * j / 7.0))) for j in range(1, 7)]
        mid = (A[0] + B[0]) * 0.5 - c0
        inner = [c0 + mid * rnd.uniform(0.86, 1.22)]
        poly = A + outer + list(reversed(B)) + inner
        cen = sum(poly, Vector((0.0, 0.0))) / len(poly)
        sh = rnd.uniform(0.925, 0.955) - (0.05 if k == 5 else 0.0)
        out = [((p.x - cen.x) * sh, (p.y - cen.y) * sh) for p in poly]
        pl = prism(out, 0.024, ashm, "plate", bev=0.0042)
        ridge = I.tube([(0.108 * math.cos(a0 + (a1 - a0) * (0.12 + 0.76 * j / 8.0)) - cen.x, -0.012, 0.108 * math.sin(a0 + (a1 - a0) * (0.12 + 0.76 * j / 8.0)) - cen.y)
                        for j in range(9)], 0.0056, 8, ashm, "ridge")
        yo = rnd.uniform(-0.004, 0.004) + (0.007 if k == 2 else 0.0) + (0.012 if k == 5 else 0.0)   # one plate has sunk in
        tr = (rnd.uniform(-4.0, 4.0) + (9.0 if k == 5 else 0.0), 0.0, rnd.uniform(-4.0, 4.0))
        for o in (pl, ridge):
            xf(o, (cen.x, -0.016 + yo, cen.y), tr)
            parts.append(o)
    # ember bed under the plates and the heart itself
    bed = I.lathe([(0.0, 0.0), (Ro, 0.0)], 56, emberstone((1.0, 0.16, 0.018), 2.3, 52.0, 0.10, 0.10), "bed"); face(bed); xf(bed, (0, -0.0062, 0)); parts.append(bed)
    heart = I.lathe([(0.0, 0.017), (0.036, 0.015), (0.066, 0.007), (0.086, 0.0)], 40, emberstone((1.0, 0.16, 0.016), 2.4, 62.0, 0.17, 0.26), "heart")
    face(heart); xf(heart, (c0.x, -0.0064, c0.y)); parts.append(heart)
    pose(parts, yaw=-14, tilt=-6)
    bpy.context.view_layer.update()
    glints(5, (-0.10, -0.16, 0.02), (0.14, -0.10, 0.22), 0.0026, (1.0, 0.5, 0.12), seed=8, strength=5.0)
    return dict(fill=0.86, samples=40, res=1024)
