"""Numpy 'sculpt brushes' for dense Parts (the face of Osaros v2 is sculpted from the subdivided CC0 base head with these):
every brush is a smooth falloff around a point or a polyline times a displacement (along a vector or the normals)."""
import math
import numpy as np
import bpy, bmesh
import monlib as M
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def fall(d, r, kind='smooth'):
    """compact falloff: 1 at d = 0 .. 0 at d >= r"""
    t = np.clip(1.0 - np.asarray(d, dtype=np.float64) / r, 0.0, 1.0)
    if kind == 'lin':
        return t
    if kind == 'sharp':
        return t * t
    if kind == 'round':
        return np.sqrt(np.clip(1 - (1 - t) ** 2, 0, 1))
    return t * t * (3 - 2 * t)


def dist_pt(v, c, sc=(1, 1, 1)):
    return np.linalg.norm((v - np.asarray(c, dtype=np.float64)) / np.asarray(sc, dtype=np.float64), axis=1)


def dist_poly(v, pts):
    """(distance to a polyline, parameter 0..1 along it, nearest point)"""
    P = np.asarray(pts, dtype=np.float64)
    seg = np.linalg.norm(np.diff(P, axis=0), axis=1)
    cum = np.r_[0, np.cumsum(seg)]
    best = np.full(len(v), 1e9); bt = np.zeros(len(v)); bp = np.zeros((len(v), 3))
    for i in range(len(P) - 1):
        a = P[i]; b = P[i + 1]
        ab = b - a
        L2 = max(float(ab @ ab), 1e-12)
        t = np.clip(((v - a) @ ab) / L2, 0, 1)
        q = a + t[:, None] * ab
        d = np.linalg.norm(v - q, axis=1)
        m = d < best
        best[m] = d[m]; bt[m] = (cum[i] + t[m] * seg[i]) / max(cum[-1], 1e-9); bp[m] = q[m]
    return best, bt, bp


def mirror_pts(pts):
    P = np.asarray(pts, dtype=np.float64).copy()
    P[..., 0] *= -1
    return P


def grab(p, c, r, vec, kind='smooth', sc=(1, 1, 1), sym=False, mask=None):
    """move the verts around c by vec x falloff (sym: also the mirrored brush at -x with a mirrored vector)"""
    vec = np.asarray(vec, dtype=np.float64)
    w = fall(dist_pt(p.v, c, sc), r, kind)
    if mask is not None:
        w = w * mask
    p.v += w[:, None] * vec
    if sym:
        c2 = np.array(c, dtype=np.float64) * (-1, 1, 1)
        w = fall(dist_pt(p.v, c2, sc), r, kind)
        if mask is not None:
            w = w * mask
        p.v += w[:, None] * (vec * (-1, 1, 1))
    return p


def puff(p, c, r, amt, kind='smooth', sc=(1, 1, 1), sym=False, nrm=None):
    """move along the vertex normals (amt < 0 = inwards)"""
    n = p.normals() if nrm is None else nrm
    for cc in ([c] + ([np.array(c, dtype=np.float64) * (-1, 1, 1)] if sym else [])):
        w = fall(dist_pt(p.v, cc, sc), r, kind)
        p.v += n * (w * amt)[:, None]
    return p


def line(p, pts, r, amt, kind='smooth', sym=False, vec=None, taper=0.0, nrm=None, mask=None):
    """crease (amt < 0) or ridge (amt > 0) along a polyline: along the normals, or along vec. taper: fade towards both ends"""
    n = p.normals() if (nrm is None and vec is None) else nrm
    for k in range(2 if sym else 1):
        P = np.asarray(pts, dtype=np.float64) if k == 0 else mirror_pts(pts)
        d, t, _ = dist_poly(p.v, P)
        w = fall(d, r, kind) * amt
        if taper:
            w = w * np.clip(np.minimum(t, 1 - t) / taper, 0, 1) ** 0.7
        if mask is not None:
            w = w * mask
        if vec is None:
            p.v += n * w[:, None]
        else:
            vv = np.asarray(vec, dtype=np.float64) * ((-1, 1, 1) if k else (1, 1, 1))
            p.v += w[:, None] * vv
    return p


def scale_about(p, c, r, s, kind='smooth', sc=(1, 1, 1)):
    """scale the verts around c towards / away from c by s (vector or scalar) x falloff"""
    c = np.asarray(c, dtype=np.float64)
    s = np.array([s, s, s], dtype=np.float64) if np.isscalar(s) else np.asarray(s, dtype=np.float64)
    w = fall(dist_pt(p.v, c, sc), r, kind)
    p.v = c + (p.v - c) * (1 + (s - 1) * w[:, None])
    return p


def smooth_region(p, c, r, iters=3, fac=0.5):
    w = fall(dist_pt(p.v, c), r)
    return M.smooth_verts(p, iters, fac, mask=w)


def bvh_of(part):
    t = part.tris()
    return BVHTree.FromPolygons([tuple(q) for q in part.v], [tuple(int(i) for i in q) for q in t])


def ray(bvh, o, d):
    hit = bvh.ray_cast(Vector(tuple(float(x) for x in o)), Vector(tuple(float(x) for x in d)))
    if hit[0] is None:
        return None, None
    return np.array(hit[0]), np.array(hit[1])


def nearest(bvh, p):
    loc, nrm, idx, dist = bvh.find_nearest(Vector(tuple(float(x) for x in p)))
    return np.array(loc), np.array(nrm), dist


def cut_keep(part, co, no):
    """bisect by a plane, drop the side the normal points to, cap the cut"""
    bm = part.to_bm()
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    r = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-6, plane_co=Vector(co), plane_no=Vector(no), clear_outer=True, clear_inner=False)
    edges = [e for e in r['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
    if edges:
        bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=edges)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    q = M.Part([], []).from_bm(bm)
    q.smooth = part.smooth
    return q


def is_closed(part):
    cnt = {}
    for f in part.f:
        for k in range(len(f)):
            e = (min(f[k], f[(k + 1) % len(f)]), max(f[k], f[(k + 1) % len(f)]))
            cnt[e] = cnt.get(e, 0) + 1
    bad = sum(1 for c in cnt.values() if c != 2)
    return bad == 0, bad


def decimate_w(part, ratio, weight=None, factor=4.0):
    """collapse-decimate; `weight` (per vertex 0..1) protects detail: high weight = keep more triangles there"""
    ob = M._temp_object(part, 'dec')
    md = ob.modifiers.new('DECIMATE', 'DECIMATE')
    md.ratio = ratio
    if weight is not None:
        vg = ob.vertex_groups.new(name='keep')
        w = np.clip(np.asarray(weight, dtype=np.float64), 0, 1)
        for i in np.where(w > 1e-3)[0]:
            vg.add([int(i)], float(w[i]), 'REPLACE')
        md.vertex_group = 'keep'
        md.invert_vertex_group = True          # the group marks what to decimate LESS
        md.vertex_group_factor = factor
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    me2 = ev.to_mesh()
    v, f, _ = M._read_mesh(me2, None)
    ev.to_mesh_clear()
    me = ob.data
    bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.meshes.remove(me)
    q = M.Part(v, f)
    q.bone, q.look, q.obj, q.smooth = part.bone, part.look, part.obj, part.smooth
    return q


def boolean_multi(a, cutters, op='DIFFERENCE'):
    """EXACT boolean of a closed Part with several closed cutter Parts at once (collection operand). Weights are dropped."""
    a = a.copy(); a.W = None
    oa = M._temp_object(a, 'ba')
    col = bpy.data.collections.new('ov2_cut')
    bpy.context.scene.collection.children.link(col)
    obs = []
    for k, c in enumerate(cutters):
        me = bpy.data.meshes.new('cut%d' % k)
        me.from_pydata(c.v.tolist(), [], c.f)
        me.update()
        ob = bpy.data.objects.new('cut%d' % k, me)
        col.objects.link(ob)
        obs.append(ob)
    m = oa.modifiers.new('bool', 'BOOLEAN')
    m.operation = op
    m.operand_type = 'COLLECTION'
    m.collection = col
    m.solver = 'EXACT'
    dg = bpy.context.evaluated_depsgraph_get()
    ev = oa.evaluated_get(dg)
    me2 = ev.to_mesh()
    v, f, _ = M._read_mesh(me2, None)
    ev.to_mesh_clear()
    for o in [oa] + obs:
        me = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.meshes.remove(me)
    bpy.data.collections.remove(col)
    q = M.Part(v, f)
    q.bone, q.look, q.obj, q.smooth = a.bone, a.look, a.obj, a.smooth
    return q.cleanup(1e-6)


def walk(bvh, start, d0, length, step=0.005, wander=0.5, seed=0, pull=None, pull_w=0.0):
    """polyline crawling over a surface: from `start` along d0, turning at random (wander), re-seated on the surface every
    step. Returns (points (k,3), outward normals (k,3))."""
    rng = np.random.default_rng(seed)
    p, n, _ = nearest(bvh, start)
    d = np.asarray(d0, dtype=np.float64)
    pts = [p]; nrs = [n]
    k = max(2, int(length / step))
    for i in range(k):
        d = d - n * np.dot(d, n)
        d = M.unit(d)
        side = np.cross(n, d)
        d = M.unit(d + side * rng.normal(0, wander) * 0.5)
        if pull is not None and pull_w:
            d = M.unit(d + M.unit(np.asarray(pull, dtype=np.float64)) * pull_w)
        q, n2, _ = nearest(bvh, p + d * step)
        if np.linalg.norm(q - p) < step * 0.2:
            break
        p, n = q, n2
        pts.append(p); nrs.append(n)
    return np.array(pts), np.array(nrs)


def wedge(pts, nrs, width, depth, lift=0.002, taper=0.6, w_min=0.15):
    """closed triangular blade along a surface polyline: base `lift` above the surface, apex `depth` below it; width and
    depth shrink towards the far end (taper = share of the length that tapers)"""
    P = np.asarray(pts, dtype=np.float64); N = np.asarray(nrs, dtype=np.float64)
    k = len(P)
    t = np.linspace(0, 1, k)
    sc = np.where(t > 1 - taper, w_min + (1 - w_min) * (1 - (t - (1 - taper)) / max(taper, 1e-6)), 1.0)
    T = np.zeros_like(P)
    T[1:-1] = P[2:] - P[:-2]; T[0] = P[1] - P[0]; T[-1] = P[-1] - P[-2]
    T /= np.maximum(np.linalg.norm(T, axis=1), 1e-12)[:, None]
    rings = []
    for i in range(k):
        n = M.unit(N[i] - T[i] * np.dot(N[i], T[i]))
        b = np.cross(T[i], n)
        w = width * sc[i]; dp = depth * (0.35 + 0.65 * sc[i])
        rings.append([P[i] + n * lift - b * w / 2, P[i] + n * lift + b * w / 2, P[i] - n * dp])
    return M.loft(np.array(rings), cap0=True, cap1=True).recalc()
