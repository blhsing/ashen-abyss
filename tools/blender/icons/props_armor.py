"""Armour tiers, jewellery tiers and the eight unique items.

Shared helpers live here (Shell = stacked super-elliptic rings with UVs, mail(), surf(), tongue() ...); the slots are in
props_armor_body.py (gloves / pants / boots), props_armor_jewel.py (amulets / rings) and props_armor_unique.py.
Front of every model is -Y (toward the camera before posing); the groups are then yawed for a three-quarter view.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Euler, Matrix
import iconlib as I
from iconlib import R
from registry import icon, reg
import props_items as PI

EMBER = (1.0, 0.42, 0.08)
TAN = (0.21, 0.125, 0.065)          # tier-1 leather: lighter than the kit default so it carries on the near-black slot
LINEN = (0.42, 0.35, 0.25)


def sm(a, b, x):
    """smoothstep: 0 at a .. 1 at b (a may be > b)"""
    t = (x - a) / (b - a)
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def _sp(v, e):
    return math.copysign(abs(v) ** e, v)


def dark5():
    """tier-5 plate: warm soot-dark iron (tier 4 is the colder blue-black ash plate)"""
    return I.ashplate(color=(0.070, 0.058, 0.052), edge_color=(0.42, 0.35, 0.26))


def iron2(**o):
    """tier-2 iron: dark grey with rust creeping in patches (the kit preset is almost all rust)"""
    p = dict(amount=0.50, color=(0.14, 0.136, 0.134), edge_color=(0.34, 0.33, 0.32), rough=0.56); p.update(o)
    return I.iron(**p)


def steel3(**o):
    """tier-3 steel: cleaner and brighter than the kit preset (which rusts brown wherever lames overlap)"""
    p = dict(amount=0.26, color=(0.34, 0.335, 0.33)); p.update(o)
    return I.steel(**p)


def void():
    """pure black without any reflection (the kit's dark inside is a glossy principled and catches the key light)"""
    key = "void_matte"
    if key in I._MATS:
        return I._MATS[key]
    m = bpy.data.materials.new(key); m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial'); em = nt.nodes.new('ShaderNodeEmission')
    em.inputs[0].default_value = (0, 0, 0, 1); em.inputs[1].default_value = 0.0
    nt.links.new(em.outputs[0], out.inputs[0])
    I._MATS[key] = m
    return m


def inside(r=0.09, loc=(0, 0, 0.02), squash=(1, 1, 1.1)):
    o = I.ball(r, void(), "inside", 3, squash); o.location = loc
    return o


def seam(strength=3.0):
    """ember light in a seam: deep orange so it stays orange (not yellow-white) once the red channel clips"""
    return I.emit((1.0, 0.13, 0.012), strength, key="seam%r" % strength)


def hot(strength=4.0, scale=40.0, crust=0.3, base=1.2, col=(1.0, 0.30, 0.035), deep=(1.0, 0.085, 0.008), key=None):
    """glowing ember stone: the whole body glows deep orange (base), hotter veins run between darker crust islands"""
    key = key or "hot%r%r%r%r%r%r" % (strength, scale, crust, base, col, deep)
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    N = nt.nodes; lk = nt.links.new
    tc = N.new('ShaderNodeTexCoord')
    vo = N.new('ShaderNodeTexVoronoi'); vo.feature = 'DISTANCE_TO_EDGE'; vo.inputs['Scale'].default_value = scale
    no = N.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = scale * 0.45; no.inputs['Detail'].default_value = 3
    lk(tc.outputs['Object'], vo.inputs['Vector']); lk(tc.outputs['Object'], no.inputs['Vector'])
    vein = N.new('ShaderNodeMapRange'); vein.interpolation_type = 'SMOOTHSTEP'
    vein.inputs[1].default_value = 0.0; vein.inputs[2].default_value = 0.03 + 0.22 * crust; vein.inputs[3].default_value = 1.0; vein.inputs[4].default_value = 0.0
    lk(vo.outputs['Distance'], vein.inputs[0])
    bs = N.new('ShaderNodeMapRange'); bs.inputs[1].default_value = 0.25; bs.inputs[2].default_value = 0.75
    bs.inputs[3].default_value = base * 0.45; bs.inputs[4].default_value = base * 1.3
    lk(no.outputs[0], bs.inputs[0])
    st = N.new('ShaderNodeMath'); st.operation = 'MULTIPLY_ADD'; st.inputs[1].default_value = strength
    lk(vein.outputs[0], st.inputs[0]); lk(bs.outputs[0], st.inputs[2])
    cm = N.new('ShaderNodeMix'); cm.data_type = 'RGBA'
    cm.inputs[6].default_value = tuple(deep) + (1,); cm.inputs[7].default_value = tuple(col) + (1,)
    lk(vein.outputs[0], cm.inputs[0])
    b.inputs['Base Color'].default_value = (0.03, 0.014, 0.01, 1)
    b.inputs['Roughness'].default_value = 0.6
    lk(cm.outputs[2], b.inputs['Emission Color']); lk(st.outputs[0], b.inputs['Emission Strength'])
    bp = N.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = 0.7; bp.inputs['Distance'].default_value = 0.12 / scale
    lk(vo.outputs['Distance'], bp.inputs['Height']); lk(bp.outputs[0], b.inputs['Normal'])
    I._MATS[key] = m
    return m


def void_disc(c, axis, r, squash=0.3):
    """black lens that closes a sleeve / cuff opening"""
    o = I.ball(r, void(), "void", 2, (1, 1, squash))
    o.location = c
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = Vector(axis).normalized().to_track_quat('Z', 'Y')
    return o


# ------------------------------------------------------------------------------------------------ grid surface with UVs
def surf(rows, mat=None, name="surf", closed=False, smooth=True):
    """quad grid through rows of points (all rows the same length). UVs are metric: u along a row, v across the rows"""
    nr = len(rows); nc = len(rows[0])
    rows = [[Vector(p) for p in r] for r in rows]
    vs = [tuple(p) for r in rows for p in r]
    ncf = nc if closed else nc - 1
    fs = []
    for k in range(nr - 1):
        for i in range(ncf):
            i2 = (i + 1) % nc
            fs.append((k * nc + i, k * nc + i2, (k + 1) * nc + i2, (k + 1) * nc + i))
    o = I.obj(name, vs, fs, mat, smooth)
    me = o.data
    U = [0.0]
    for i in range(ncf):
        U.append(U[-1] + sum((rows[k][(i + 1) % nc] - rows[k][i]).length for k in range(nr)) / nr)
    V = [0.0]
    for k in range(nr - 1):
        V.append(V[-1] + sum((rows[k + 1][i] - rows[k][i]).length for i in range(nc)) / nc)
    uvl = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        fi = p.index % ncf
        for li in p.loop_indices:
            vi = me.loops[li].vertex_index
            k = vi // nc; i = vi % nc
            if closed and i == 0 and fi == nc - 1:
                i = nc
            uvl.data[li].uv = (U[i], V[k])
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    return o


def tube_surf(c0, c1, r0, r1, mat=None, n=10, seg=28, name="sleeve", thick=0.004, rings=0, puff=0.0, flat=1.0, bend=0.0):
    """tube from c0 to c1 with UVs (mail sleeves); `rings` quilted rolls swell the radius (padded sleeves)"""
    c0, c1 = Vector(c0), Vector(c1)
    ax = (c1 - c0).normalized()
    u = Vector((0, 1, 0)); v = ax.cross(u).normalized(); u = v.cross(ax).normalized()
    rows = []
    for k in range(n + 1):
        t = k / n
        r = r0 + (r1 - r0) * t
        if rings:
            r *= 1 + puff * abs(math.sin(math.pi * rings * t)) ** 0.6
        c = c0.lerp(c1, t) + v * (bend * math.sin(math.pi * t))
        rows.append([c + u * (r * flat * math.cos(a)) + v * (r * math.sin(a)) for a in (2 * math.pi * i / seg for i in range(seg))])
    o = surf(rows, mat, name, closed=True)
    if thick:
        I.solid(o, thick)
    return o


def herm(L, x, linear=False):
    """interpolate the columns 1.. of a table sorted by column 0 (cubic Hermite with finite-difference tangents)"""
    if x <= L[0][0]:
        return list(L[0][1:])
    if x >= L[-1][0]:
        return list(L[-1][1:])
    i = 0
    while not (L[i][0] <= x <= L[i + 1][0]):
        i += 1
    x0, x1 = L[i][0], L[i + 1][0]; h = x1 - x0; t = (x - x0) / h
    out = []
    for c in range(1, len(L[i])):
        p0, p1 = L[i][c], L[i + 1][c]
        if linear:
            out.append(p0 + (p1 - p0) * t); continue
        m0 = (p1 - L[i - 1][c]) / (x1 - L[i - 1][0]) if i > 0 else (p1 - p0) / h
        m1 = (L[i + 2][c] - p0) / (L[i + 2][0] - x0) if i + 2 < len(L) else (p1 - p0) / h
        t2, t3 = t * t, t * t * t
        out.append((2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * h * m0 + (-2 * t3 + 3 * t2) * p1 + (t3 - t2) * h * m1)
    return out


class Shell:
    """stacked super-elliptic rings along Z.  levels: (z, rx, ry[, cy[, cx]]);  phi = 0 is the FRONT (-Y), +90 is +X.
    bump(phi, z) -> extra radial offset (quilting, drape, a prow ...)"""

    def __init__(self, levels, power=2.0, bump=None, linear=False):
        self.L = sorted([tuple(l) + (0.0,) * (5 - len(l)) for l in levels], key=lambda l: l[0])
        self.power = power; self.bump = bump; self.linear = linear

    def lv(self, z):
        out = herm(self.L, z, self.linear)
        out[0] = max(out[0], 0.0); out[1] = max(out[1], 0.0)
        return out

    def nrm(self, phi, z):
        rx, ry, cy, cx = self.lv(z)
        a = R(phi); e = 2.0 / self.power; p = self.power
        x = _sp(math.sin(a), e); y = -_sp(math.cos(a), e)
        n = Vector((_sp(x, p - 1) / max(rx, 1e-6), _sp(y, p - 1) / max(ry, 1e-6), 0.0))
        return n.normalized() if n.length > 1e-9 else Vector((math.sin(a), -math.cos(a), 0.0))

    def pt(self, phi, z, off=0.0, follow=True):
        rx, ry, cy, cx = self.lv(z)
        a = R(phi); e = 2.0 / self.power
        p = Vector((cx + _sp(math.sin(a), e) * rx, cy - _sp(math.cos(a), e) * ry, z))
        if follow and self.bump is not None:
            off = off + self.bump(phi, z)
        if off:
            p += self.nrm(phi, z) * off
        return p

    def zs(self, sub=4, z0=None, z1=None):
        L = self.L; out = []
        for i in range(len(L) - 1):
            for j in range(sub):
                out.append(L[i][0] + (L[i + 1][0] - L[i][0]) * j / sub)
        out.append(L[-1][0])
        if z0 is not None:
            out = [z0] + [z for z in out if z > z0 + 1e-6]
        if z1 is not None:
            out = [z for z in out if z < z1 - 1e-6] + [z1]
        return out

    def mesh(self, mat=None, seg=48, sub=4, thick=0.004, open_fn=None, name="shell", z0=None, z1=None, a0=-180.0, a1=180.0,
             off=0.0, zs=None, follow=True):
        zs = sorted(zs) if zs else self.zs(sub, z0, z1)
        full = open_fn is None and a1 - a0 >= 359.9
        rows = []
        for z in zs:
            lo, hi = a0, a1
            if open_fn is not None:
                t = open_fn(z); lo, hi = t, 360.0 - t
            n = seg if full else seg + 1
            rows.append([self.pt(lo + (hi - lo) * i / seg, z, off, follow) for i in range(n)])
        o = surf(rows, mat, name, closed=full)
        if thick:
            I.solid(o, thick)
        return o

    def band(self, z0, z1, mat=None, off=0.004, thick=0.004, a0=-180.0, a1=180.0, seg=48, rows=2, name="band", follow=True):
        """strip of plate / leather lying on the surface between two heights (outer face at `off`)"""
        zs = [z0 + (z1 - z0) * k / rows for k in range(rows + 1)]
        return self.mesh(mat, seg, thick=thick, name=name, a0=a0, a1=a1, off=off, zs=zs, follow=follow)

    def path(self, pts, off=0.0, n=10, follow=True):
        P = []
        for i in range(len(pts) - 1):
            (p0, z0), (p1, z1) = pts[i], pts[i + 1]
            for j in range(n):
                t = j / n
                P.append(self.pt(p0 + (p1 - p0) * t, z0 + (z1 - z0) * t, off, follow))
        P.append(self.pt(pts[-1][0], pts[-1][1], off, follow))
        return P

    def line(self, pts, r=0.003, mat=None, off=0.0, n=10, name="line", seg=8, follow=True):
        """tube lying on the surface through (phi, z) control points"""
        return I.tube(self.path(pts, off, n, follow), r, seg, mat, name)

    def ring(self, z, r=0.004, mat=None, off=0.0, a0=-180.0, a1=180.0, n=64, name="ring", seg=8, follow=True):
        full = a1 - a0 >= 359.9
        P = [self.pt(a0 + (a1 - a0) * i / n, z, off, follow) for i in range(n if full else n + 1)]
        return I.tube(P, r, seg, mat, name, closed=full)

    def rivets(self, z, n, mat, r=0.004, a0=-180.0, a1=180.0, off=0.0005, follow=True):
        out = []
        for i in range(n):
            o = I.ball(r, mat, "rivet", 2)
            o.location = self.pt(a0 + (a1 - a0) * (i + 0.5) / n, z, off, follow)
            out.append(o)
        return out

    def put(self, o, phi, z, off=0.0, tilt=0.0, roll=0.0, follow=True):
        """sit an object on the surface: its local -Y looks along the outward normal, local Z stays up.
        tilt > 0 leans its top outward, roll turns it in the surface plane"""
        n = self.nrm(phi, z)
        o.location = self.pt(phi, z, off, follow)
        o.rotation_euler = Euler((R(tilt), R(roll), math.atan2(n.x, -n.y)), 'XYZ')
        return o

    def rim(self, open_fn, zs, r=0.006, mat=None, off=0.0, name="rim", seg=8):
        """rolled edge along a front opening made with mesh(open_fn=...)"""
        zs = sorted(zs); zz = [z for z in zs if open_fn(z) > 1e-6]
        if not zz:
            return []
        top = zz[-1] < zs[-1] - 1e-9; bot = zz[0] > zs[0] + 1e-9
        Lf = [self.pt(open_fn(z), z, off) for z in zz]; Rt = [self.pt(-open_fn(z), z, off) for z in zz]
        ptop = self.pt(0, zs[zs.index(zz[-1]) + 1], off) if top else None
        pbot = self.pt(0, zs[zs.index(zz[0]) - 1], off) if bot else None
        if top and bot:
            return [I.tube([pbot] + Lf + [ptop] + Rt[::-1], r, seg, mat, name, closed=True)]
        if top:
            return [I.tube(Lf + [ptop] + Rt[::-1], r, seg, mat, name)]
        if bot:
            return [I.tube(Lf[::-1] + [pbot] + Rt, r, seg, mat, name)]
        return [I.tube(Lf, r, seg, mat, name), I.tube(Rt, r, seg, mat, name)]

    def stitch(self, pts, mat, off=0.001, dash=0.011, gap=0.009, r=0.0016, n=16, follow=True):
        """running stitch: short thread dashes along a (phi, z) path"""
        return dashes(self.path(pts, off, n, follow), mat, dash, gap, r)

    def girth(self, z, n=48):
        P = [self.pt(360.0 * i / n, z, follow=False) for i in range(n)]
        return sum((P[i] - P[i - 1]).length for i in range(n))


def dashes(P, mat, dash=0.011, gap=0.009, r=0.0016):
    P = [Vector(p) for p in P]
    cum = [0.0]
    for i in range(len(P) - 1):
        cum.append(cum[-1] + (P[i + 1] - P[i]).length)

    def at(s):
        for i in range(len(P) - 1):
            if cum[i + 1] >= s:
                d = cum[i + 1] - cum[i]
                return P[i].lerp(P[i + 1], (s - cum[i]) / d if d > 1e-12 else 0.0)
        return P[-1]
    out = []; s = gap * 0.5
    while s + dash <= cum[-1]:
        out.append(I.tube([at(s), at(s + dash * 0.5), at(s + dash)], r, 5, mat, "stitch"))
        s += dash + gap
    return out


def arch(half, zc, hh, pw=0.5):
    """front opening: half-angle `half` (degrees) at height zc, closing elliptically at zc -+ hh"""
    def f(z):
        u = (z - zc) / hh
        return half * (1 - u * u) ** pw if abs(u) < 0.9999 else 0.0
    return f


def arch_zs(zc, hh, n=14):
    return [zc + hh * math.sin(-math.pi / 2 + math.pi * i / n) for i in range(n + 1)]


def zs_with(shell, sub, extra):
    return sorted(set(round(z, 5) for z in shell.zs(sub) + list(extra)))


# ------------------------------------------------------------------------------------------------ materials of my own
def mail(col=(0.36, 0.36, 0.38), cell=0.016, rust=0.3, key=None):
    """riveted mail: two interleaved lattices of rings in UV space (metric UVs from surf / Shell), dark padding in
    the eyes of the rings, ring relief as bump, rust drifting in patches"""
    key = key or "mail%r%r%r" % (col, cell, rust)
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    N = nt.nodes; lk = nt.links.new
    tc = N.new('ShaderNodeTexCoord')
    sc = N.new('ShaderNodeVectorMath'); sc.operation = 'SCALE'; sc.inputs['Scale'].default_value = 1.0 / cell
    lk(tc.outputs['UV'], sc.inputs[0])

    def lattice(shift):
        ad = N.new('ShaderNodeVectorMath'); ad.operation = 'ADD'; ad.inputs[1].default_value = (shift, shift, 0.0)
        lk(sc.outputs[0], ad.inputs[0])
        fr = N.new('ShaderNodeVectorMath'); fr.operation = 'FRACTION'; lk(ad.outputs[0], fr.inputs[0])
        sb = N.new('ShaderNodeVectorMath'); sb.operation = 'SUBTRACT'; sb.inputs[1].default_value = (0.5, 0.5, 0.0)
        lk(fr.outputs[0], sb.inputs[0])
        ln = N.new('ShaderNodeVectorMath'); ln.operation = 'LENGTH'; lk(sb.outputs[0], ln.inputs[0])
        d = N.new('ShaderNodeMath'); d.operation = 'SUBTRACT'; d.inputs[1].default_value = 0.35; lk(ln.outputs['Value'], d.inputs[0])
        ab = N.new('ShaderNodeMath'); ab.operation = 'ABSOLUTE'; lk(d.outputs[0], ab.inputs[0])
        mr = N.new('ShaderNodeMapRange'); mr.interpolation_type = 'SMOOTHSTEP'
        mr.inputs[1].default_value = 0.02; mr.inputs[2].default_value = 0.19; mr.inputs[3].default_value = 1.0; mr.inputs[4].default_value = 0.0
        lk(ab.outputs[0], mr.inputs[0])
        return mr.outputs[0]
    mx = N.new('ShaderNodeMath'); mx.operation = 'MAXIMUM'
    lk(lattice(0.0), mx.inputs[0]); lk(lattice(0.5), mx.inputs[1])
    # metal / padding mask
    mk = N.new('ShaderNodeMapRange'); mk.interpolation_type = 'SMOOTHSTEP'
    mk.inputs[1].default_value = 0.08; mk.inputs[2].default_value = 0.45
    lk(mx.outputs[0], mk.inputs[0])
    no = N.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = 9.0; no.inputs['Detail'].default_value = 4.0
    lk(tc.outputs['Object'], no.inputs['Vector'])
    rm = N.new('ShaderNodeMapRange'); rm.interpolation_type = 'SMOOTHSTEP'
    rm.inputs[1].default_value = 0.50; rm.inputs[2].default_value = 0.72; rm.inputs[4].default_value = rust
    lk(no.outputs[0], rm.inputs[0])
    c1 = N.new('ShaderNodeMix'); c1.data_type = 'RGBA'
    c1.inputs[6].default_value = tuple(col) + (1,); c1.inputs[7].default_value = (0.17, 0.085, 0.04, 1)
    lk(rm.outputs[0], c1.inputs[0])
    c2 = N.new('ShaderNodeMix'); c2.data_type = 'RGBA'
    c2.inputs[6].default_value = (0.022, 0.018, 0.015, 1)
    lk(mk.outputs[0], c2.inputs[0]); lk(c1.outputs[2], c2.inputs[7])
    lk(c2.outputs[2], b.inputs['Base Color'])
    mt = N.new('ShaderNodeMath'); mt.operation = 'MULTIPLY'
    inv = N.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    lk(rm.outputs[0], inv.inputs[1]); lk(mk.outputs[0], mt.inputs[0]); lk(inv.outputs[0], mt.inputs[1])
    lk(mt.outputs[0], b.inputs['Metallic'])
    rg = N.new('ShaderNodeMapRange'); rg.inputs[3].default_value = 0.85; rg.inputs[4].default_value = 0.34
    lk(mt.outputs[0], rg.inputs[0]); lk(rg.outputs[0], b.inputs['Roughness'])
    bp = N.new('ShaderNodeBump'); bp.inputs['Strength'].default_value = 1.0; bp.inputs['Distance'].default_value = cell * 0.32
    lk(mx.outputs[0], bp.inputs['Height']); lk(bp.outputs[0], b.inputs['Normal'])
    I._MATS[key] = m
    return m


# ------------------------------------------------------------------------------------------------ small parts
def tongue(w, h, t, mat, point=0.0, n=8, name="scale", bev=None):
    """flat hanging plate (scale / strap end / pteruge): square top edge at z = 0, rounded or pointed end at z = -h"""
    hw = w / 2
    zr = -(h - hw * (1 + point))
    pts = [(-hw, 0.0), (hw, 0.0)]
    for i in range(n + 1):
        a = math.pi * i / n
        pts.append((hw * math.cos(a), zr - hw * (1 + point) * math.sin(a)))
    return I.extrude(pts, t, mat, name, bev=min(t * 0.4, 0.002) if bev is None else bev)


def clone(src, name="clone", mat=None):
    """another object on the same (already applied) mesh; with mat: on a copy of it"""
    me = src.data
    if mat is not None:
        me = me.copy(); me.materials.clear(); me.materials.append(mat)
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def baked(o):
    bpy.context.view_layer.update()
    return I.apply(o)


def buckle(mat, w=0.030, h=0.038, r=0.0032):
    """rectangular buckle frame in the XZ plane (faces -Y) with its prong"""
    pts = []
    for (x, z) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        for k in range(3):
            a = math.atan2(z, x) + R(-30 + 30 * k)
            pts.append((x * (w / 2 - r * 2) + r * 2 * math.cos(a) * 1.0, 0.0, z * (h / 2 - r * 2) + r * 2 * math.sin(a)))
    fr = I.tube(pts, r, 8, mat, "buckle", closed=True)
    pr = I.tube([(-w / 2, -r * 0.8, 0.0), (w / 2, -r * 1.2, 0.0)], r * 0.75, 6, mat, "prong")
    return [fr, pr]


def on(shell, objs, phi, z, off=0.0, tilt=0.0, roll=0.0, follow=True):
    """group objects and sit the group on a shell"""
    g = I.group(objs, "on")
    shell.put(g, phi, z, off, tilt, roll, follow)
    return g


def pose(parts, rot=(0, 0, 28), scale=1.0):
    g = I.group([p for p in parts if p is not None])
    I.place(g, rot=rot, scale=scale)
    return g


def flatten(seq):
    out = []
    for p in seq:
        if isinstance(p, (list, tuple)):
            out += flatten(p)
        elif p is not None:
            out.append(p)
    return out


def pauldron(mat, side=1, r=0.085, z=0.335, x=0.205, lames=3, trim=None, thick=0.0062):
    """layered shoulder cap (as PI.pauldron, but thick enough to keep dark plate dark; every lame can carry a trim)"""
    out = []
    for k in range(lames):
        s = 1.0 - 0.13 * k
        o = I.lathe([(0.0, r * 0.62), (r * 0.5, r * 0.55), (r * 0.9, r * 0.25), (r, 0.0)], 28, mat, "pauldron")
        I.solid(o, thick)
        o.scale = (s * 1.05, s * 0.95, s * 0.8)
        o.location = (side * (x + 0.012 * k), 0, z - 0.035 * k)
        o.rotation_euler = (0, R(side * (28 + 16 * k)), 0)
        out.append(o)
        if trim is not None:
            t = I.torus(r * 0.99, 0.0048 if k == 0 else 0.0036, trim, "ptrim", 36, 8)
            t.scale = o.scale; t.location = o.location; t.rotation_euler = o.rotation_euler
            out.append(t)
    return out


# ================================================================================================ HELMS
@icon("helm_1")
def helm_1():
    """皮兜帽: soft stitched leather hood with an open face, a short shoulder mantle and a drawstring"""
    lt = I.leather(TAN, scale=64.0, bump=0.24); dk = I.leather(); th = I.cloth((0.50, 0.44, 0.33)); bn = I.bone()

    def soft(phi, z):
        return 0.008 * math.sin(R(phi * 7 + 20)) * sm(-0.125, -0.21, z) + 0.003 * math.sin(R(phi * 3) + z * 40)
    S = Shell([(-0.215, 0.182, 0.160, 0.012), (-0.170, 0.148, 0.134, 0.008), (-0.125, 0.101, 0.106, 0.0), (-0.070, 0.107, 0.117, 0.0),
               (0.000, 0.115, 0.125, 0.004), (0.060, 0.108, 0.120, 0.012), (0.105, 0.084, 0.100, 0.028), (0.137, 0.050, 0.066, 0.052),
               (0.156, 0.016, 0.022, 0.086)], bump=soft)
    op = arch(55, -0.026, 0.078, 0.55)
    zs = zs_with(S, 4, arch_zs(-0.026, 0.078))
    parts = [S.mesh(lt, 56, thick=0.005, open_fn=op, zs=zs, name="hood")]
    parts += S.rim(op, zs, 0.0095, lt, 0.002)
    tip = I.ball(0.020, lt, "tip", 2, (1, 1.25, 0.9)); tip.location = (0, 0.094, 0.156); parts.append(tip)
    parts.append(inside(0.099, (0, 0.006, -0.018), (1.0, 1.08, 1.12)))
    # welted seam over the crown, stitched on both sides
    parts.append(S.line([(0, 0.058), (0, 0.105), (0, 0.137), (0, 0.156)], 0.0048, lt, 0.001, name="welt"))
    for s in (-1, 1):
        parts += S.stitch([(s * 7, 0.060), (s * 8, 0.105), (s * 12, 0.137)], th, 0.003, 0.012, 0.010, 0.0019)
    parts += S.stitch([(a, -0.200) for a in range(-150, 91, 20)], th, 0.003, 0.014, 0.012, 0.0021)
    # drawstring round the neck, knot and two toggled ends
    parts.append(S.ring(-0.122, 0.0048, dk, 0.004))
    k = S.pt(22, -0.122, 0.008)
    kn = I.ball(0.010, dk, "knot", 2, (1.2, 1, 0.9)); kn.location = k; parts.append(kn)
    for j, (dx, ln) in enumerate(((-0.018, 0.070), (0.020, 0.088))):
        e = k + Vector((dx, -0.020, -ln))
        parts.append(I.tube([k, k + Vector((dx * 0.5, -0.016, -ln * 0.45)), e], 0.0036, 6, dk, "lace"))
        tg = I.tube([e + Vector((0, 0, 0.004)), e + Vector((0, 0, -0.020))], 0.0062, 8, bn, "toggle"); parts.append(tg)
    pose(parts, (0, 0, 30))
    return dict(fill=0.82)


@icon("helm_2")
def helm_2():
    """鐵盔: riveted iron cap of four plates with a nasal bar and a mail aventail"""
    ir = iron2(); bd = I.iron(amount=0.42, color=(0.20, 0.195, 0.19)); ml = mail(cell=0.0115); dk = I.leather()
    C = Shell([(0.0, 0.110, 0.118), (0.035, 0.108, 0.116), (0.075, 0.095, 0.102), (0.112, 0.068, 0.073), (0.142, 0.034, 0.037), (0.160, 0.008, 0.009)])
    parts = [C.mesh(ir, 48, thick=0.0062, name="cap")]
    parts.append(C.band(-0.008, 0.030, bd, 0.005, 0.0062, name="brow"))
    for a in (0, 90, 180, -90):
        parts.append(C.band(0.028, 0.158, bd, 0.0045, 0.0062, a - 8, a + 8, 6, 10, "spangen"))
        for z in (0.052, 0.088, 0.120):
            for s in (-1, 1):
                rv = I.ball(0.0038, bd, "rivet", 2); rv.location = C.pt(a + s * 4.5, z, 0.005); parts.append(rv)
    parts += C.rivets(0.011, 18, bd, 0.0042, off=0.0055)
    fin = I.lathe([(0.0, 0.0), (0.016, 0.0), (0.012, 0.012), (0.006, 0.020), (0.008, 0.028), (0.0, 0.034)], 16, bd, "finial")
    fin.location = (0, 0, 0.156); parts.append(fin)
    nas = I.extrude([(-0.014, 0.012), (0.014, 0.012), (0.014, -0.050), (0.019, -0.080), (0.0, -0.098), (-0.019, -0.080), (-0.014, -0.050)], 0.007, bd, "nasal", bev=0.002)
    I.place(nas, loc=(0, -0.127, 0.004), rot=(-6, 0, 0)); parts.append(nas)
    for z in (0.004, -0.012):
        rv = I.ball(0.0038, ir, "rivet", 2); rv.location = (0, -0.131, z + 0.004); parts.append(rv)
    # aventail: mail curtain hung from the rim, open round the face, closing under the chin, flaring on the shoulders
    A = Shell([(-0.205, 0.176, 0.162, 0.016), (-0.140, 0.133, 0.133, 0.010), (-0.070, 0.113, 0.121, 0.0), (0.004, 0.108, 0.116, 0.0)],
              bump=lambda phi, z: 0.008 * math.sin(R(phi * 7)) * sm(-0.09, -0.19, z))

    def op(z):
        if z >= -0.040:
            return 60.0
        u = (-0.040 - z) / 0.078
        return 60.0 * math.sqrt(1 - u * u) if u < 0.9999 else 0.0
    zs = zs_with(A, 5, [-0.040 - 0.078 * math.sin(math.pi / 2 * i / 8) for i in range(9)])
    parts.append(A.mesh(ml, 72, thick=0.0035, open_fn=op, zs=zs, name="aventail"))
    parts += A.rim(op, zs, 0.0052, dk, 0.001)
    parts.append(A.ring(-0.205, 0.0045, dk, 0.0))
    parts.append(inside(0.100, (0, 0.004, -0.03), (1.0, 1.05, 1.2)))
    pose(parts, (0, 0, 32))
    return dict(fill=0.82)


@icon("helm_4")
def helm_4():
    """灰鑄戰盔: ash-forged great helm, flat crown, prowed face, T-slit with a dim ember glow inside, two swept fins"""
    ash = I.ashplate(); sm_ = seam(3.0)

    def prow(phi, z):
        a = abs(((phi + 180.0) % 360.0) - 180.0)
        return 0.030 * max(0.0, 1 - a / 46.0) ** 1.3 * sm(0.118, 0.050, z)
    H = Shell([(-0.160, 0.122, 0.132), (-0.100, 0.112, 0.124), (-0.020, 0.113, 0.125), (0.055, 0.109, 0.121), (0.095, 0.100, 0.112),
               (0.118, 0.084, 0.095), (0.132, 0.048, 0.055), (0.138, 0.002, 0.002)], power=2.5, bump=prow)
    sh = H.mesh(ash, 72, sub=5, thick=0.007, name="greathelm")
    cut = []
    for (sx, sz, z) in ((0.190, 0.024, 0.006), (0.020, 0.112, -0.050)):
        c = I.box(sx, 0.24, sz); c.location = (0, -0.140, z); c.hide_render = True; c["nofit"] = 1
        I.mod(sh, 'BOOLEAN', operation='DIFFERENCE', object=c, solver='EXACT')
        cut.append(c)
    parts = [sh] + cut
    # ember-lit lining just behind the face plate, black everywhere else
    parts.append(H.mesh(I.emit((1.0, 0.13, 0.012), 1.35, key="slitglow"), 40, thick=0, off=-0.013, a0=-80, a1=80,
                        zs=[-0.125 + 0.165 * i / 12 for i in range(13)], name="lining"))
    parts.append(inside(0.094, (0, 0.012, -0.02), (1.0, 1.0, 1.25)))
    # brow reinforce, bottom rim, rivets, ember seams
    parts.append(H.band(0.028, 0.056, ash, 0.0045, 0.0062, name="browband"))
    parts.append(H.band(-0.160, -0.130, ash, 0.0045, 0.0062, name="rimband"))
    parts += H.rivets(-0.145, 16, ash, 0.0042, off=0.005)
    parts.append(H.ring(0.063, 0.0022, sm_, 0.0008))
    parts.append(H.ring(-0.124, 0.0022, sm_, 0.0008))
    parts.append(H.line([(0, 0.066), (0, 0.100), (0, 0.122), (0, 0.137), (180, 0.122), (180, 0.066)], 0.0070, ash, 0.001, name="comb"))
    # swept fins: broad blades rising behind the temples, notched trailing edge
    out = [(0.0, -0.050), (0.070, -0.066), (0.140, -0.046), (0.200, 0.016), (0.244, 0.098), (0.262, 0.182), (0.214, 0.130), (0.170, 0.106),
           (0.136, 0.116), (0.092, 0.090), (0.050, 0.098), (0.0, 0.078)]
    for s in (-1, 1):
        f = I.extrude(out, 0.011, ash, "fin", bev=0.0038)
        f.location = (s * 0.104, -0.040, 0.020)
        f.rotation_euler = Euler((R(s * 16), 0, R(90 - s * 20)), 'XYZ')
        parts.append(f)
        for y in (-0.0062, 0.0062):
            e = I.tube([(0.006, y, -0.012), (0.070, y, -0.026), (0.136, y, -0.008), (0.190, y, 0.045), (0.228, y, 0.110), (0.248, y, 0.160)], 0.0021, 6, sm_, "finseam")
            e.location = f.location; e.rotation_euler = f.rotation_euler; parts.append(e)
    pose(parts, (0, 0, 34))
    return dict(fill=0.84)


@icon("helm_5")
def helm_5():
    """燼冕: open-faced dark helm under a heavy gold circlet from which tall jagged ember spikes rise"""
    dk = dark5(); gd = I.gold(); emb = hot(3.6, 34.0, 0.32, 1.25)
    H = Shell([(-0.142, 0.106, 0.120), (-0.070, 0.112, 0.125), (0.0, 0.114, 0.126), (0.060, 0.106, 0.118), (0.105, 0.080, 0.090),
               (0.130, 0.042, 0.048), (0.139, 0.002, 0.002)])

    def op(z):
        if z <= -0.045:
            return 37.0 + 7.0 * sm(-0.045, -0.142, z)
        u = (z + 0.045) / 0.062
        return 37.0 * math.sqrt(1 - u * u) if u < 0.9999 else 0.0
    zs = zs_with(H, 4, [-0.045 + 0.062 * math.sin(math.pi / 2 * i / 8) for i in range(9)])
    parts = [H.mesh(dk, 56, thick=0.0062, open_fn=op, zs=zs, name="helm")]
    parts += H.rim(op, zs, 0.0066, gd, 0.001)
    parts.append(H.ring(-0.142, 0.0060, gd, 0.001, 46, 314))
    parts.append(inside(0.099, (0, 0.006, -0.02), (1.0, 1.08, 1.15)))
    parts.append(H.band(0.012, 0.056, gd, 0.010, 0.009, name="circlet"))
    for z in (0.012, 0.056):
        parts.append(H.ring(z, 0.0046, gd, 0.011))
    spikes = [(0, 0.205, 0.028), (47, 0.140, 0.023), (-47, 0.150, 0.024), (94, 0.178, 0.026), (-94, 0.165, 0.025), (140, 0.130, 0.022),
              (-140, 0.138, 0.022), (180, 0.172, 0.025)]
    for i, (a, h, r) in enumerate(spikes):
        c = I.crystal(r, h, 5, 0.78, emb, "spike", jitter=0.30, seed=11 + i, base_tip=0.02)
        c.scale = (1.0, 0.62, 1.0)
        H.put(c, a, 0.044, 0.000, tilt=12, roll=(i * 37) % 17 - 8); parts.append(c)
        c2 = I.crystal(r * 0.60, h * 0.46, 4, 0.72, emb, "spike2", jitter=0.3, seed=31 + i, base_tip=0.02)
        H.put(c2, a + 16, 0.046, 0.004, tilt=22, roll=16); parts.append(c2)
        cup = I.cone(r * 1.22, r * 0.95, 0.020, gd, "cup", 10); cup.scale = (1.0, 0.7, 1.0)
        H.put(cup, a, 0.040, 0.003, tilt=12); parts.append(cup)
    parts += H.rivets(0.034, 16, dk, 0.0046, off=0.011)
    pose(parts, (0, 0, 30))
    return dict(fill=0.84)


# ================================================================================================ CHESTS
TORSO = [(0.00, 0.150, 0.108, 0.0), (0.09, 0.158, 0.115, 0.0), (0.20, 0.186, 0.132, -0.006), (0.30, 0.190, 0.126, -0.004),
         (0.365, 0.150, 0.100, 0.0), (0.40, 0.088, 0.078, 0.004), (0.425, 0.074, 0.070, 0.006)]


@icon("chest_1")
def chest_1():
    """襯墊外衣: quilted linen gambeson, stand collar, padded short sleeves, bone toggles, leather belt"""
    cl = I.cloth(LINEN, hem_lo=-0.40, hem_hi=-0.05); lt = I.leather(); bn = I.bone(); ir = iron2()
    NQ = 26

    def quilt(phi, z):
        return 0.012 * abs(math.sin(R(phi) * NQ / 2)) ** 0.55 * sm(0.41, 0.335, z)
    S = Shell([(-0.180, 0.208, 0.146, 0.0), (-0.085, 0.176, 0.126, 0.0)] + TORSO[:5] + [(0.40, 0.090, 0.080, 0.004), (0.428, 0.079, 0.073, 0.006), (0.468, 0.083, 0.077, 0.006)],
              power=2.5, bump=quilt)
    parts = [S.mesh(cl, 208, sub=4, thick=0.006, open_fn=lambda z: 17.0 * sm(0.405, 0.468, z), name="gambeson")]
    parts.append(inside(0.068, (0, 0.006, 0.445), (1, 1, 0.45)))
    for s in (-1, 1):
        c0 = Vector((s * 0.150, 0, 0.328)); c1 = Vector((s * 0.318, 0, 0.232))
        parts.append(tube_surf(c0, c1, 0.090, 0.078, cl, 18, 32, "sleeve", 0.006, rings=3, puff=0.10, flat=0.9))
        parts.append(void_disc(c1 - (c1 - c0).normalized() * 0.010, c1 - c0, 0.066))
    # belt with buckle and hanging tongue
    parts.append(S.band(-0.008, 0.032, lt, 0.016, 0.005, name="belt", follow=False))
    parts.append(on(S, buckle(ir, 0.036, 0.050, 0.0038), -16, 0.012, 0.019, follow=False))
    parts.append(S.line([(-24, 0.010), (-30, -0.03), (-32, -0.085)], 0.0085, lt, 0.019, follow=False, name="tongue"))
    # bone toggles down the front seam
    for z in (0.085, 0.155, 0.225, 0.295):
        p = S.pt(0, z, 0.012)
        parts.append(I.tube([p + Vector((-0.020, 0, 0)), p + Vector((0.020, 0, 0))], 0.0072, 8, bn, "toggle"))
        parts.append(I.tube([p + Vector((0.020, 0.004, 0)), p + Vector((0.040, 0.006, 0.004))], 0.0028, 6, lt, "loop"))
    parts.append(S.ring(-0.168, 0.0034, lt, 0.002))
    pose(parts, (0, 0, 26))
    return dict(fill=0.84)


@icon("chest_2")
def chest_2():
    """鎖環甲: mail hauberk with wide short sleeves and a split skirt, worn over a padded collar, belted"""
    ml = mail(cell=0.0185); cl = I.cloth(LINEN, hem_lo=-1.0, hem_hi=-0.8); lt = I.leather(); ir = iron2()

    def drape(phi, z):
        return 0.011 * math.sin(R(phi * 9 + 40)) * sm(-0.02, -0.22, z)
    S = Shell([(-0.255, 0.218, 0.152, 0.0), (-0.120, 0.186, 0.131, 0.0), (0.0, 0.153, 0.110, 0.0)] + TORSO[1:5] + [(0.395, 0.100, 0.088, 0.004), (0.414, 0.096, 0.086, 0.004)],
              power=2.5, bump=drape)

    def op(z):
        return 6.0 * sm(-0.06, -0.10, z) + 13.0 * sm(0.345, 0.414, z)
    parts = [S.mesh(ml, 96, sub=4, thick=0.004, open_fn=op, name="hauberk")]
    # padded collar and hem of the gambeson underneath
    U = Shell([(0.385, 0.090, 0.080, 0.004), (0.420, 0.080, 0.073, 0.006), (0.452, 0.082, 0.076, 0.006)],
              bump=lambda phi, z: 0.006 * abs(math.sin(R(phi) * 9)) ** 0.6)
    parts.append(U.mesh(cl, 108, thick=0.005, name="collar"))
    parts.append(inside(0.066, (0, 0.006, 0.438), (1, 1, 0.4)))
    parts.append(S.mesh(cl, 96, thick=0.004, zs=[-0.290, -0.270, -0.245], off=-0.007, name="underhem"))
    parts.append(S.ring(0.414, 0.0056, lt, 0.001, name="neckbind"))
    for s in (-1, 1):
        c0 = Vector((s * 0.150, 0, 0.326)); c1 = Vector((s * 0.335, 0, 0.212)); ax = (c1 - c0).normalized()
        parts.append(tube_surf(c0, c1, 0.088, 0.096, ml, 10, 36, "sleeve", 0.004, flat=0.9))
        parts.append(tube_surf(c1 - ax * 0.03, c1 + ax * 0.030, 0.080, 0.078, cl, 6, 30, "undersleeve", 0.005, rings=1, puff=0.08, flat=0.9))
        parts.append(void_disc(c1 + ax * 0.022, ax, 0.066))
    parts.append(S.band(0.000, 0.036, lt, 0.007, 0.005, name="belt", follow=False))
    parts.append(on(S, buckle(ir, 0.034, 0.046, 0.0036), -14, 0.018, 0.010, follow=False))
    parts.append(S.line([(-22, 0.016), (-28, -0.03), (-30, -0.10)], 0.0080, lt, 0.012, follow=False, name="tongue"))
    pose(parts, (0, 0, 26))
    return dict(fill=0.84)


@icon("chest_3")
def chest_3():
    """鱗片胸甲: rows of overlapping steel scales on a leather coat, brass hem row and trim, shoulder caps, leather pteruges"""
    st = I.steel(); br = I.brass(); lt = I.leather((0.125, 0.078, 0.045)); dk = I.leather()
    S = Shell(TORSO, power=2.5)
    parts = [S.mesh(lt, 48, thick=0.005, name="coat"), inside(0.07, (0, 0.004, 0.40), (1, 1, 0.5))]
    w, h = 0.054, 0.070
    proto = baked(tongue(w, h, 0.0045, st, 0.25, 6))
    proto_b = None
    for k in range(7):
        z = 0.070 + 0.0405 * k
        n = max(8, int(round(S.girth(z - h * 0.5) / (w * 0.84))))
        for i in range(n):
            if k == 0:
                if proto_b is None:
                    proto_b = clone(proto, "scale_b", br); o = proto_b
                else:
                    o = clone(proto_b)
            else:
                o = clone(proto)
            S.put(o, 360.0 * (i + 0.5 * (k % 2)) / n, z, 0.0035, tilt=-8.5)
            parts.append(o)
    S.put(proto, 0.0, 0.070 + 0.0405 * 2, 0.0035, tilt=-8.5); parts.append(proto)
    # leather yoke over the top row, brass edged
    parts.append(S.band(0.300, 0.405, dk, 0.011, 0.006, rows=6, name="yoke"))
    parts.append(S.ring(0.302, 0.0048, br, 0.012))
    parts += S.rivets(0.322, 14, br, 0.0048, a0=-110, a1=110, off=0.012)
    parts.append(S.ring(0.423, 0.0075, br, 0.002, name="neck"))
    for s in (-1, 1):
        parts += PI.pauldron(st, s, r=0.088, z=0.338, x=0.206, lames=2, trim=br)
    # belt and pteruges
    parts.append(S.band(-0.006, 0.036, dk, 0.012, 0.006, name="belt"))
    parts += S.rivets(0.015, 22, br, 0.0048, off=0.0125)
    plate = I.box(0.052, 0.008, 0.058, br, "beltplate", 0.003); S.put(plate, 0, 0.015, 0.013); parts.append(plate)
    pt_proto = baked(tongue(0.062, 0.125, 0.007, dk, 0.1, 6, "pteruge"))
    npt = 15
    for i in range(npt):
        o = clone(pt_proto) if i else pt_proto
        a = 360.0 * (i + 0.5) / npt
        S.put(o, a, 0.004, 0.004, tilt=-13); parts.append(o)
        sd = I.ball(0.0075, br, "stud", 2); S.put(sd, a, 0.004, 0.004, tilt=-13)
        sd.location = S.pt(a, -0.085, 0.004 + 0.022 + 0.004); parts.append(sd)
    pose(parts, (0, 0, 26))
    return dict(fill=0.84)


@icon("chest_5")
def chest_5():
    """燼誓鎧: dark harness edged in tarnished gold, high gorget, crested pauldrons, a great ember heart in the breast"""
    dk = dark5(); gd = I.gold(); emb = hot(4.4, 30.0, 0.30, 2.3)
    S = Shell(TORSO, power=2.5)
    parts = [S.mesh(dk, 48, thick=0.0062, name="cuirass")]
    G = Shell([(0.372, 0.124, 0.102, 0.002), (0.420, 0.096, 0.088, 0.006), (0.475, 0.100, 0.094, 0.008), (0.520, 0.124, 0.112, 0.010)], power=2.2)
    gop = lambda z: 16.0 * sm(0.425, 0.520, z)
    gzs = G.zs(5)
    parts.append(G.mesh(dk, 48, thick=0.0062, open_fn=gop, zs=gzs, name="gorget"))
    parts += G.rim(gop, gzs, 0.0060, gd, 0.001)
    parts.append(G.ring(0.520, 0.0066, gd, 0.001, 16, 344))
    parts.append(G.ring(0.376, 0.0060, gd, 0.003))
    parts.append(inside(0.088, (0, 0.008, 0.485), (1, 1, 0.5)))
    # ember heart: big jagged stone sunk in a gold bezel, held by claws
    c = S.pt(0, 0.232)
    heart = I.shard(0.070, emb, "heart", seed=7, n=20, squash=(0.95, 0.40, 1.20)); heart.location = c + Vector((0, 0.008, 0)); parts.append(heart)
    bez = I.torus(0.070, 0.0105, gd, "bezel", 40, 10); I.place(bez, loc=c + Vector((0, 0.008, 0)), rot=(90, 0, 0), scale=(1.0, 1.20, 0.8)); parts.append(bez)
    for i in range(6):
        a = R(30 + 60 * i)
        p0 = c + Vector((0.072 * math.cos(a), -0.002, 0.086 * math.sin(a)))
        p1 = c + Vector((0.044 * math.cos(a), -0.024, 0.053 * math.sin(a)))
        parts.append(I.tube([p0, p0.lerp(p1, 0.6) + Vector((0, -0.007, 0)), p1], [0.0070, 0.0058, 0.0034], 6, gd, "claw"))
    # gold lines: breast ridge, sweeps to the armholes, waist band
    parts.append(S.line([(0, 0.030), (0, 0.135)], 0.0062, gd, 0.001, name="ridge"))
    for s in (-1, 1):
        parts.append(S.line([(s * 16, 0.330), (s * 42, 0.350), (s * 74, 0.332)], 0.0055, gd, 0.001, name="sweep"))
        parts.append(S.line([(s * 20, 0.140), (s * 48, 0.112), (s * 82, 0.106)], 0.0050, gd, 0.001, name="sweep2"))
    parts.append(S.band(-0.004, 0.032, gd, 0.008, 0.007, name="waist"))
    # fauld of three lames, each gold edged
    F = Shell([(-0.156, 0.202, 0.146), (-0.104, 0.184, 0.133), (-0.052, 0.169, 0.123), (0.0, 0.152, 0.110)], power=2.5, linear=True)
    for k in range(3):
        parts.append(F.band(-0.052 * (k + 1) - 0.004, -0.052 * k + 0.006, dk, 0.004 + 0.0035 * k, 0.0062, name="fauld"))
        parts.append(F.ring(-0.052 * (k + 1) - 0.004, 0.0044 if k < 2 else 0.0064, gd, 0.005 + 0.0035 * k))
    # pauldrons with standing crest plates
    crest = [(-0.090, 0.0), (0.090, 0.0), (0.110, 0.050), (0.084, 0.124), (0.040, 0.082), (-0.030, 0.062), (-0.090, 0.040)]
    for s in (-1, 1):
        parts += pauldron(dk, s, r=0.100, z=0.338, x=0.212, lames=3, trim=gd)
        cp = I.extrude(crest, 0.009, dk, "crest", bev=0.003)
        cp.location = (s * 0.190, 0.0, 0.376); cp.rotation_euler = Euler((R(s * 26), 0, R(90)), 'XYZ'); parts.append(cp)
        ed = I.tube([(x, 0, z) for (x, z) in crest[1:] + [crest[0]]], 0.0054, 6, gd, "crestedge")
        ed.location = cp.location; ed.rotation_euler = cp.rotation_euler; parts.append(ed)
    pose(parts, (0, 0, 26))
    return dict(fill=0.86)


# ================================================================================================ the other slots
for _m in ("props_armor_body", "props_armor_jewel", "props_armor_unique"):
    try:
        __import__(_m)
    except Exception:
        import traceback
        print("ICON-MODULE-FAIL %s" % _m); traceback.print_exc()
