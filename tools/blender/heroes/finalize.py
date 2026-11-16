"""Join parts per material class, unwrap, bake atlases, build final materials, export .glb + manifest entry.

Realism pass (v2; the previous version is kept as finalize_v1.py):
  * UV: Smart-Project islands are re-flattened (minimum stretch) and packed by their real outlines with small gutters
    (v1: raw Smart Project + box packing -> ~40 % of the atlas used); atlas size follows the surface area (2048 for the
    main classes, limited by the 12 MB file budget);
  * every part gets a per-vertex 'hem' attribute (surface distance to the nearest open / sharp edge);
  * bake in two stages: MASKS (convex edges from a bevel shader, tight cavity AO, wide AO), which the source materials
    read back for edge wear and cavity dirt, then colour / ORM / tangent normals with several samples per texel;
  * skin and hair are baked too: the painted source textures are re-atlased onto tightly packed UVs with object-space
    micro detail from mats.skin_src / hair_src composited on top (normals combined in the shader, not by image maths);
  * emissive parts get a small heat-gradient texture (hot core, cooler ends);
  * tangents are exported; textures are fitted into the file-size budget (quality steps, then atlas halving).
"""
import bpy, bmesh, math, os, json, time, heapq
import numpy as np
from mathutils import Vector, kdtree
from rigio import *
import mk
import mats

BAKED = ("cloth", "leather", "metal", "bone")
ORDER = ("skin", "hair", "cloth", "leather", "metal", "bone", "emis")
MANI = r"Y:\tools\blender\heroes\_manifest"
os.makedirs(MANI, exist_ok=True)
EXPLODE = 6.0
AO_MULT = 0.42          # wide AO multiplied into the albedo (the game also gets it in ORM.r)
AO_DIST = 0.30
CAV_DIST = 0.024
TEXEL = 0.00125         # target metres per texel of the baked classes
SKIN_TEXEL = 0.00070
MAX_BYTES = int(12 * 1024 * 1024 * 0.975)
QUAL = ((92, 95, 92), (90, 94, 90), (88, 92, 88), (85, 90, 85), (82, 89, 80))      # (colour, normal, orm) JPEG quality steps
MAX_2K = 3              # at most this many 2048 atlases per character (the file budget may still halve one)
SMP = dict(ao=32, cav=24, mask=8, color=6, orm=4, normal=8)      # bake samples per texel
PREVIEW = bool(os.environ.get('ASHEN_PREVIEW'))                   # look-dev iteration: 1K atlases, fewer samples
if PREVIEW:
    SMP = dict(ao=12, cav=8, mask=4, color=3, orm=2, normal=4)


# ---------------------------------------------------------------------------------------------- helpers
def _select_only(objs, active=None):
    bpy.context.view_layer.update()
    for o in list(bpy.context.view_layer.objects):
        if o is not None:
            try:
                o.select_set(False)
            except Exception:
                pass
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def join(objs, name):
    objs = [o for o in objs if o is not None]
    if len(objs) == 1:
        objs[0].name = name
        objs[0].data.name = name
        return objs[0]
    _select_only(objs, objs[0])
    with bpy.context.temp_override(active_object=objs[0], selected_objects=objs, selected_editable_objects=objs):
        bpy.ops.object.join()
    o = objs[0]
    o.name = name
    o.data.name = name
    return o


def _set_int_attr(obj, name, value):
    me = obj.data
    a = me.attributes.get(name)
    if a is None:
        a = me.attributes.new(name, 'INT', 'POINT')
    a.data.foreach_set('value', np.full(len(me.vertices), value, dtype=np.int32))


def _get_int_attr(obj, name):
    me = obj.data
    a = me.attributes[name]
    v = np.empty(len(me.vertices), dtype=np.int32)
    a.data.foreach_get('value', v)
    return v


def _set_float_attr(obj, name, values):
    me = obj.data
    a = me.attributes.get(name)
    if a is not None and (a.data_type != 'FLOAT' or a.domain != 'POINT'):
        me.attributes.remove(a)
        a = None
    if a is None:
        a = me.attributes.new(name, 'FLOAT', 'POINT')
    v = np.broadcast_to(np.asarray(values, dtype=np.float32), (len(me.vertices),)).copy()
    a.data.foreach_set('value', v)


def _triangulate(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def _face_vert0(obj):
    """index of the first vertex of every polygon"""
    me = obj.data
    ls = np.empty(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get('loop_start', ls)
    vi = np.empty(len(me.loops), dtype=np.int32)
    me.loops.foreach_get('vertex_index', vi)
    return vi[ls]


def _set_face_int(obj, name, value):
    me = obj.data
    a = me.attributes.get(name)
    if a is None:
        a = me.attributes.new(name, 'INT', 'FACE')
    a.data.foreach_set('value', np.full(len(me.polygons), value, dtype=np.int32))


def _area(obj):
    return float(sum(p.area for p in obj.data.polygons))


def _hem_attr(o, cap=0.30):
    """per-vertex 'hem' = distance along the surface (metres) to the nearest open border or sharp edge
    (dihedral above the part's smoothing angle, at most 70 deg - catches the rim of solidified sheets, plate edges,
    bevelled corners, cutting edges). Used by the materials for hems, stitches, rolled rims, honed edges."""
    me = o.data
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    thr = math.radians(min(float(o.get("sharp", 40)), 70.0))
    n = len(bm.verts)
    dist = [cap] * n
    heap = []
    for e in bm.edges:
        lf = e.link_faces
        feat = len(lf) != 2
        if not feat:
            try:
                feat = e.calc_face_angle() >= thr
            except ValueError:
                feat = True
        if feat:
            for v in e.verts:
                if dist[v.index] > 0.0:
                    dist[v.index] = 0.0
                    heap.append((0.0, v.index))
    adj = [[] for _ in range(n)]
    for e in bm.edges:
        a, b = e.verts[0].index, e.verts[1].index
        ln = e.calc_length()
        adj[a].append((b, ln))
        adj[b].append((a, ln))
    bm.free()
    heapq.heapify(heap)
    while heap:
        d, i = heapq.heappop(heap)
        if d > dist[i]:
            continue
        for j, ln in adj[i]:
            nd = d + ln
            if nd < dist[j]:
                dist[j] = nd
                heapq.heappush(heap, (nd, j))
    _set_float_attr(o, "hem", np.array(dist, dtype=np.float32))


FIN_SLOTS = 16


def _fur_fins(J, slot, budget, density=460.0, length=(0.034, 0.066), width=0.017, lift=0.30, seed=0):
    """Break the silhouette of a pelt: scatter single-triangle fur locks over the faces of material slot `slot` of the
    joined class mesh (called after decimation, so the locks survive). Each lock starts on the surface, runs downhill
    (gravity projected on the surface) and lifts off by 10-20 deg, so the outline becomes wispy instead of a smooth
    blob while the cost stays at one triangle per lock. The locks take the skin weights / owner of the face they grow
    from, are textured by the same box-projected lock tiles as the pelt (mats.fur, vertex attribute 'fin') and are
    shaded with the pelt's normal (custom normals), so they read as tufts and not as shards.
    Returns the number of triangles added (never more than `budget`)."""
    me = J.data
    if budget <= 0:
        return 0
    rng = np.random.RandomState(seed + 7 * slot + len(me.vertices))
    bm = bmesh.new()
    bm.from_mesh(me)
    dl = bm.verts.layers.deform.verify()
    hem = bm.verts.layers.float.get("hem")
    ownl = bm.verts.layers.int.get("own")
    finl = bm.verts.layers.float.get("fin") or bm.verts.layers.float.new("fin")
    finf = bm.faces.layers.int.get("finf") or bm.faces.layers.int.new("finf")
    faces = [f for f in bm.faces if f.material_index == slot and f[finf] == 0]
    if not faces:
        bm.free()
        return 0
    ar = np.array([f.calc_area() for f in faces])
    tot = float(ar.sum())
    n = min(int(tot * density), int(budget))
    if n < 1 or tot <= 0:
        bm.free()
        return 0
    pick = rng.choice(len(faces), size=n, p=ar / tot)
    made = 0
    shade = []
    for fi in pick:
        f = faces[fi]
        vs = f.verts
        w = rng.rand(len(vs)) + 0.05
        w /= w.sum()
        pnt = Vector((0.0, 0.0, 0.0))
        sn = Vector((0.0, 0.0, 0.0))
        for wi, v in zip(w, vs):
            pnt += v.co * float(wi)
            sn += v.normal * float(wi)
        nrm = f.normal.copy()
        if sn.length < 1e-4:
            sn = nrm.copy()
        sn.normalize()
        d = Vector((0.0, 0.0, -1.0))
        d = d - nrm * d.dot(nrm)
        if d.length < 0.3:
            r = Vector((pnt.x - round(pnt.x / EXPLODE) * EXPLODE, pnt.y - 0.03, 0.0))      # (weapons are exploded along X)
            if r.length < 1e-4:
                r = Vector((1.0, 0.0, 0.0))
            d = r.normalized() - nrm * r.normalized().dot(nrm)
            if d.length < 1e-3:
                continue
        d.normalize()
        side = nrm.cross(d)
        d = (d + side * float(rng.randn() * 0.28)).normalized()
        side = nrm.cross(d)
        L = length[0] + rng.rand() * (length[1] - length[0])
        wd = width * (0.7 + 0.6 * rng.rand())
        a = lift * (0.55 + 0.75 * rng.rand()) * (1.0 - 0.65 * max(0.0, nrm.z))
        if nrm.z > 0.6:
            L *= 0.7
        base = pnt - nrm * 0.0025
        p0 = base - side * (wd * 0.5)
        p1 = base + side * (wd * 0.5)
        p2 = pnt + d * (L * math.cos(a)) + nrm * (L * math.sin(a))
        nv = [bm.verts.new(q) for q in (p1, p0, p2)]
        src = vs[int(np.argmax(w))]
        for j, v in enumerate(nv):
            for k, wt in src[dl].items():
                v[dl][k] = wt
            if hem is not None:
                v[hem] = 0.30
            if ownl is not None:
                v[ownl] = src[ownl]
            v[finl] = 1.0 if j == 2 else 0.15
        try:
            nf = bm.faces.new(nv)
        except ValueError:
            continue
        nf.smooth = True
        nf.material_index = slot
        nf[finf] = 1 + (made % FIN_SLOTS)
        made += 1
        shade.append((sn + d * 0.25).normalized())
    bm.to_mesh(me)
    bm.free()
    me.update()
    if made:
        # shade the locks like the pelt they grow from (keeps every other corner normal as it is)
        try:
            nl = len(me.loops)
            cn = np.empty(nl * 3, dtype=np.float32)
            me.corner_normals.foreach_get('vector', cn)
            cn = cn.reshape(-1, 3)
            npol = len(me.polygons)
            ls = np.empty(npol, dtype=np.int32)
            me.polygons.foreach_get('loop_start', ls)
            for i, sn in enumerate(shade):
                l0 = int(ls[npol - made + i])
                cn[l0:l0 + 3] = (sn.x, sn.y, sn.z)
            me.normals_split_custom_set([tuple(map(float, v)) for v in cn])
            me.update()
        except Exception as e:
            log("fur lock normals not set:", str(e)[:120])
    return made


def _prep_part(o):
    mk.apply_mods(o)
    me = o.data
    for p in me.polygons:
        p.use_smooth = True
    ang = float(o.get("sharp", 40))
    for nm in ("sharp_edge", "sharp_face", "custom_normal"):
        a = me.attributes.get(nm)
        if a is not None:
            me.attributes.remove(a)
    if ang < 179:
        try:
            me.set_sharp_from_angle(angle=math.radians(ang))
        except Exception as e:
            log("set_sharp_from_angle failed", e)
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)
    # make sure every vertex has a weight
    if len(o.vertex_groups) == 0:
        raise RuntimeError("part without weights: " + o.name)


def _uv_arrays(obj, layer=None):
    """(uv (L,2), tri loop index (T,3), tri 3D area (T,), tri material index (T,)) of a triangulated mesh"""
    me = obj.data
    lay = me.uv_layers[layer] if layer is not None else me.uv_layers.active
    uv = np.empty(len(me.loops) * 2, dtype=np.float32)
    lay.data.foreach_get('uv', uv)
    uv = uv.reshape(-1, 2)
    ls = np.empty(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get('loop_start', ls)
    tri = np.stack([ls, ls + 1, ls + 2], axis=1)
    ar = np.empty(len(me.polygons), dtype=np.float32)
    me.polygons.foreach_get('area', ar)
    mi = np.empty(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get('material_index', mi)
    return uv, tri, ar, mi


def _uv_area(uv, tri):
    a, b, c = uv[tri[:, 0]], uv[tri[:, 1]], uv[tri[:, 2]]
    return 0.5 * np.abs((b[:, 0] - a[:, 0]) * (c[:, 1] - a[:, 1]) - (c[:, 0] - a[:, 0]) * (b[:, 1] - a[:, 1]))


def _uv_coverage(obj):
    uv, tri, ar, mi = _uv_arrays(obj)
    return float(_uv_area(uv, tri).sum())


def _pack(margin):
    bpy.ops.uv.select_all(action='SELECT')
    for kw in (dict(margin=margin, rotate=True, shape_method='CONCAVE', margin_method='FRACTION', scale=True),
               dict(margin=margin, rotate=True, shape_method='CONCAVE'),
               dict(margin=margin, rotate=True)):
        try:
            bpy.ops.uv.pack_islands(**kw)
            return
        except Exception as e:
            log("pack_islands retry:", str(e)[:120])


def _exposure(J, dist=0.06):
    """per-face 0..1: share of 5 rays (along the normal and 35 deg off it) that leave the surface without hitting
    anything within `dist`. Inner sides of solidified shells and layers buried under armour get ~0."""
    sc = bpy.context.scene
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = J.data
    n = len(me.polygons)
    cen = np.empty(n * 3, dtype=np.float32)
    nor = np.empty(n * 3, dtype=np.float32)
    me.polygons.foreach_get('center', cen)
    me.polygons.foreach_get('normal', nor)
    cen = cen.reshape(-1, 3)
    nor = nor.reshape(-1, 3)
    out = np.zeros(n, dtype=np.float32)
    for i in range(n):
        nn = Vector(nor[i])
        if nn.length < 0.5:
            out[i] = 1.0
            continue
        c = Vector(cen[i]) + nn * 0.0015
        t1 = nn.orthogonal().normalized()
        t2 = nn.cross(t1)
        free = 0
        for d in (nn, nn + t1 * 0.7, nn - t1 * 0.7, nn + t2 * 0.7, nn - t2 * 0.7):
            if not sc.ray_cast(dg, c, d.normalized(), distance=dist)[0]:
                free += 1
        out[i] = free / 5.0
    return out


def _weight_islands(J, face_w, lo=0.40):
    """scale every UV island by the visibility of its faces: islands that are at least half exposed keep the full
    texel density, hidden ones drop to `lo` (linear), so the atlas is spent on what can be seen"""
    from bpy_extras import mesh_utils
    me = J.data
    uv, tri, ar, mi = _uv_arrays(J)
    n_lo = 0
    ua = _uv_area(uv, tri)
    for isl in mesh_utils.mesh_linked_uv_islands(me):
        idx = np.asarray(isl, dtype=np.int64)
        a = ar[idx]
        w = float((face_w[idx] * a).sum() / max(float(a.sum()), 1e-12))
        sc = lo + (1.0 - lo) * min(1.0, w / 0.5)
        li = np.unique(tri[idx].ravel())
        # thin rings / hoops (rims of solidified bands): they need few texels but their bounding box blocks a big
        # square of the atlas -> halve them
        ext = uv[li].max(0) - uv[li].min(0)
        fill = float(ua[idx].sum()) / max(float(ext[0] * ext[1]), 1e-12)
        if fill < 0.22 and float(a.sum()) < 0.03:
            sc *= 0.5
        if sc < 0.995:
            c = uv[li].mean(0)
            uv[li] = (uv[li] - c) * sc + c
            n_lo += 1
    me.uv_layers.active.data.foreach_set('uv', uv.ravel())
    return n_lo


def _select_faces(obj, mask):
    me = obj.data
    me.polygons.foreach_set('select', np.asarray(mask, dtype=bool))
    me.update()


def _uv_unwrap(obj, angle=72.0, margin=0.0019, face_w=None):
    """Smart-Project islands (72 deg; 88 deg for small props, which need few texels but would otherwise shatter into
    dozens of slivers) -> seams -> each island re-flattened with minimum stretch -> same texel density everywhere ->
    islands scaled by visibility -> packed by true outline with small gutters."""
    me = obj.data
    while len(me.uv_layers):
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name="UVMap")
    small = np.zeros(len(me.polygons), dtype=bool)
    a = me.attributes.get("small")
    if a is not None and a.domain == 'FACE':
        v = np.zeros(len(me.polygons), dtype=np.int32)
        a.data.foreach_get('value', v)
        small = v > 0
    fin = np.zeros(len(me.polygons), dtype=np.int32)
    a = me.attributes.get("finf")
    if a is not None and a.domain == 'FACE':
        a.data.foreach_get('value', fin)
    isfin = fin > 0
    _select_only([obj], obj)
    bpy.context.tool_settings.mesh_select_mode = (False, False, True)
    for mask, ang in ((~small & ~isfin, angle), (small & ~isfin, 88.0)):
        if not mask.any():
            continue
        bpy.ops.object.mode_set(mode='OBJECT')
        _select_faces(obj, mask)
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(ang), island_margin=margin, area_weight=0.0,
                                 correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode='OBJECT')
    _select_faces(obj, ~isfin)
    bpy.ops.object.mode_set(mode='EDIT')
    try:
        bpy.ops.uv.select_all(action='SELECT')
        bpy.ops.mesh.mark_seam(clear=True)
        bpy.ops.uv.seams_from_islands(mark_seams=True, mark_sharp=False)
        try:
            bpy.ops.uv.unwrap(method='MINIMUM_STRETCH', margin=margin)
        except Exception:
            bpy.ops.uv.unwrap(method='ANGLE_BASED', margin=margin)
    except Exception as e:
        log("unwrap relax failed:", str(e)[:160])
    try:
        bpy.ops.uv.select_all(action='SELECT')
        bpy.ops.uv.average_islands_scale()
    except Exception as e:
        log("average_islands_scale:", e)
    if face_w is not None:
        bpy.ops.object.mode_set(mode='OBJECT')
        nlo = _weight_islands(obj, face_w)
        log("   uv: %d islands shrunk (hidden surfaces), exposed area share %.0f %%" % (
            nlo, 100.0 * float((face_w > 0.3).mean())))
        _select_faces(obj, ~isfin)
        bpy.ops.object.mode_set(mode='EDIT')
    _pack(margin)
    bpy.ops.object.mode_set(mode='OBJECT')
    if isfin.any():
        # texel density of the packed islands -> size of a lock in UV units; pelt atlas shrinks 1.4 % to free a strip
        uv, tri, ar, mi = _uv_arrays(obj)
        ua = _uv_area(uv, tri)
        body = ~isfin & (ar > 0)
        r = math.sqrt(float(ua[body].sum()) / max(float(ar[body].sum()), 1e-9)) if body.any() else 0.4
        strip = 0.014
        li = np.unique(tri[~isfin].ravel())
        uv[li] *= (1.0 - strip)
        L, W = min(0.050 * r, 0.05), min(0.012 * r, strip - 0.006)
        step = max(L + 0.004, 1.0 / FIN_SLOTS * 0.5)
        for k in range(1, FIN_SLOTS + 1):
            t = tri[fin == k]
            if not len(t):
                continue
            x0 = 0.004 + (k - 1) * step
            y0 = 1.0 - strip + 0.003
            uv[t[:, 0]] = (x0, y0 + W)
            uv[t[:, 1]] = (x0, y0)
            uv[t[:, 2]] = (x0 + L, y0 + W * 0.5)
        me.uv_layers.active.data.foreach_set('uv', uv.ravel())


def _repack(obj, margin=0.0035):
    """pack the existing islands of the active UV layer (no re-unwrap, relative island scale kept)"""
    _select_only([obj], obj)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    _pack(margin)
    bpy.ops.object.mode_set(mode='OBJECT')


def _bake(obj, size, kind, samples, ao_dist=None):
    """kind: color | orm | normal | ao | mask.  -> (size, size, 4) float array"""
    sc = bpy.context.scene
    img = bpy.data.images.new("bake_%s" % kind, size, size, alpha=False, float_buffer=True)
    img.colorspace_settings.name = 'Non-Color'
    me = obj.data
    saved = None
    if kind in ('mask', 'ao'):
        # AO needs no surface shader: baking it through the heavy source materials (bump is evaluated for every AO
        # sample) made the AO passes the slowest part of the whole bake -> swap in a trivial material
        saved = list(me.materials)
        mm = mats.mask_material() if kind == 'mask' else mats.plain_material()
        for i in range(len(me.materials)):
            me.materials[i] = mm
    for m in {m for m in me.materials if m is not None}:
        nt = m.node_tree
        N = nt.nodes
        tg = N['TARGET']
        tg.image = img
        for n in N:
            n.select = False
        tg.select = True
        N.active = tg
        if kind in ('mask', 'ao'):
            continue
        out, em, bs = N['OUT'], N['EMIT'], N['BSDF']
        for l in list(out.inputs[0].links):
            nt.links.remove(l)
        if kind in ('color', 'orm'):
            src = N['COLOR' if kind == 'color' else 'ORM']
            for l in list(em.inputs['Color'].links):
                nt.links.remove(l)
            nt.links.new(src.outputs[0], em.inputs['Color'])
            nt.links.new(em.outputs[0], out.inputs[0])
        else:
            nt.links.new(bs.outputs[0], out.inputs[0])
    if ao_dist is not None:
        sc.world.light_settings.distance = ao_dist
    sc.cycles.samples = samples
    _select_only([obj], obj)
    typ = {'color': 'EMIT', 'orm': 'EMIT', 'mask': 'EMIT', 'normal': 'NORMAL', 'ao': 'AO'}[kind]
    tb = time.time()
    bpy.ops.object.bake(type=typ, margin=max(3, size // 512), use_clear=True, target='IMAGE_TEXTURES')
    log("   bake %-6s %4d px %2d spp: %.0fs" % (kind, size, samples, time.time() - tb))
    px = np.empty(size * size * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    if saved is not None:
        for i, m in enumerate(saved):
            me.materials[i] = m
    return px.reshape(size, size, 4)


def _sstep(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def _bake_set(J, size, cls, edge=True):
    """two-stage bake of one atlas -> dict(color (linear), orm, normal) float32 (size, size, 3)"""
    tb = time.time()
    ao = np.clip(_bake(J, size, 'ao', SMP['ao'], AO_DIST)[..., 0], 0, 1)
    occ = 1.0 - np.clip(_bake(J, size, 'ao', SMP['cav'], CAV_DIST)[..., 0], 0, 1)
    if edge:
        ed = np.clip(_bake(J, size, 'mask', SMP['mask'])[..., :2], 0, 1)
        conv = 1.0 - _sstep(occ, 0.10, 0.40)
        e = np.clip(np.maximum(ed[..., 0], ed[..., 1] * 0.55) * conv, 0, 1)
    else:
        e = np.zeros_like(ao)
    fa_ = J.data.attributes.get("finf")
    if fa_ is not None and fa_.domain == 'FACE':
        # the shared texels of the fur locks (top strip of the atlas, see _uv_unwrap) carry no baked occlusion
        r0 = int(size * (1.0 - 0.014))
        ao[r0:] = 1.0
        occ[r0:] = 0.0
        e[r0:] = 0.0
    masks = mats.blur(np.stack([e, occ, ao], axis=2), 1 if size <= 1024 else 2)
    mimg = bpy.data.images.new("masks_%s" % cls, size, size, alpha=False, float_buffer=True)
    mimg.colorspace_settings.name = 'Non-Color'
    px = np.ones((size, size, 4), dtype=np.float32)
    px[..., :3] = masks
    mimg.pixels.foreach_set(px.ravel())
    mimg.update()
    for m in {m for m in J.data.materials if m is not None}:
        n = m.node_tree.nodes.get('MASKS')
        if n is not None:
            n.image = mimg
    t1 = time.time()
    col = np.clip(_bake(J, size, 'color', SMP['color'])[..., :3], 0, 1)
    orm = np.clip(_bake(J, size, 'orm', SMP['orm'])[..., :3], 0, 1)
    nrm = np.clip(_bake(J, size, 'normal', SMP['normal'])[..., :3], 0, 1)
    bpy.data.images.remove(mimg)
    col = col * (1.0 - AO_MULT * (1.0 - ao[..., None]))
    orm = orm.copy()
    orm[..., 0] = np.clip(ao ** 0.9 * (1.0 - 0.30 * occ), 0, 1)
    log("baked %-7s %4d px  masks %.0fs + maps %.0fs  tris %d  uv coverage %.0f %%  normal rms tilt %.3f" % (
        cls, size, t1 - tb, time.time() - t1, mk.tri_count(J), _uv_coverage(J) * 100,
        float(np.sqrt((((nrm[..., :2] - 0.5) * 2) ** 2).sum(-1)[ao > 0.02].mean())) if (ao > 0.02).any() else 0.0))
    return dict(color=mats.lin2srgb(col).astype(np.float32), orm=orm.astype(np.float32), normal=nrm.astype(np.float32))


def _half(a):
    h, w = a.shape[:2]
    return a.reshape(h // 2, 2, w // 2, 2, a.shape[2]).mean(axis=(1, 3))


class Atlas:
    """baked maps of one material class + the image datablocks of its final material (re-saved to fit the budget)"""

    def __init__(self, cid, cls, P, need):
        self.cid, self.cls, self.P, self.need = cid, cls, P, need
        self.dir = os.path.join(TEX, cid)
        self.imgs = []
        self.size = P['color'].shape[0]

    def paths(self):
        return {k: os.path.join(self.dir, "%s_%s.jpg" % (self.cls, s)) for k, s in (('color', 'c'), ('normal', 'n'), ('orm', 'orm'))}

    def save(self, q):
        p = self.paths()
        mats.save_rgb(self.P['color'], p['color'], q[0])
        mats.save_rgb(self.P['normal'], p['normal'], q[1])
        mats.save_rgb(self.P['orm'], p['orm'], q[2])
        for im in self.imgs:
            im.reload()
        return sum(os.path.getsize(v) for v in p.values())

    def halve(self):
        self.P = {k: _half(v) for k, v in self.P.items()}
        n = self.P['normal'] * 2.0 - 1.0
        n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
        self.P['normal'] = (n * 0.5 + 0.5).astype(np.float32)
        self.size //= 2

    def material(self, name, double_sided=False):
        p = self.paths()
        m = mats.final_material(name, p['color'], p['orm'], p['normal'], double_sided=double_sided)
        self.imgs = [n.image for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image is not None]
        return m


def _setup_cycles():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.use_denoising = False
    sc.cycles.use_adaptive_sampling = False
    sc.render.bake.use_selected_to_active = False
    sc.render.bake.normal_space = 'TANGENT'
    if sc.world is None:
        sc.world = bpy.data.worlds.new("bake_world")
    sc.world.light_settings.distance = AO_DIST
    # fixed thread count: unbounded Cycles bakes in several Blender processes at once hung the laptop
    import os as _os
    sc.render.threads_mode = 'FIXED'
    sc.render.threads = max(1, min(32, int(_os.environ.get('ASHEN_BLENDER_THREADS', '12'))))


def _strip_layers(obj, keep_uv=True):
    me = obj.data
    for nm in ("own", "luv", "src_vid", "hem", "heat_d", "small", "fin", "finf"):
        a = me.attributes.get(nm)
        if a is not None:
            me.attributes.remove(a)
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)
    src = me.uv_layers.get("uv_src")
    if src is not None and len(me.uv_layers) > 1:
        me.uv_layers.remove(src)
    while len(me.uv_layers) > 1:
        me.uv_layers.remove(me.uv_layers[len(me.uv_layers) - 1])
    for k in list(obj.keys()):
        del obj[k]


def _split_by_owner(obj, owners, cls):
    """-> {owner: object}"""
    own = _get_int_attr(obj, "own")
    present = sorted(set(int(v) for v in own))
    out = {}
    if len(present) == 1:
        out[owners[present[0]]] = obj
        return out
    for k in present:
        me = obj.data.copy()
        o2 = bpy.data.objects.new("%s_%s" % (owners[k], cls), me)
        bpy.context.scene.collection.objects.link(o2)
        rebind(o2, mk.ARM)
        have = {g.name for g in o2.vertex_groups}
        for g in obj.vertex_groups:
            if g.name not in have:
                o2.vertex_groups.new(name=g.name)
        assert [g.name for g in o2.vertex_groups] == [g.name for g in obj.vertex_groups]
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.verts.ensure_lookup_table()
        dead = [v for v in bm.verts if own[v.index] != k]
        bmesh.ops.delete(bm, geom=dead, context='VERTS')
        bm.to_mesh(me)
        bm.free()
        out[owners[k]] = o2
    bpy.data.objects.remove(obj, do_unlink=True)
    return out


def _prune_groups(obj):
    """remove unused vertex groups (keeps export small)"""
    used = set()
    for v in obj.data.vertices:
        for g in v.groups:
            if g.weight > 0:
                used.add(g.group)
    names = [g.name for g in obj.vertex_groups if g.index not in used]
    for n in names:
        obj.vertex_groups.remove(obj.vertex_groups[n])


# ---------------------------------------------------------------------------------------------- skin / hair (fast path)
def make_skin_material(cid, kind, body, eyes=None, size=1024, gain=1.0, sat=1.0, tint=(1, 1, 1), rough_add=0.0,
                       eye_emis=False, **_ignored):
    """FAST preview only (v1 behaviour): source skin texture squeezed into the lower 90 %, eye texture in the top strip.
    Full builds bake the skin instead (bake_skin)."""
    d = os.path.join(TEX, cid)
    os.makedirs(d, exist_ok=True)
    base = os.path.join(SRC, "base")
    files = SKIN_FILES[kind]
    bh = int(size * 0.9)
    E = int(size * 0.09)
    pad = max(2, size // 256)
    col = mats.read_img(os.path.join(base, files[0]), (size, bh))[..., :3]
    lum = (col * np.array((0.2126, 0.7152, 0.0722))).sum(-1, keepdims=True)
    col = lum + (col - lum) * sat
    col = np.clip(col * np.array(tint) * gain, 0, 1)
    canvas = np.empty((size, size, 3), dtype=np.float32)
    canvas[:] = col.reshape(-1, 3).mean(0)
    canvas[:bh] = col
    if eyes is not None:
        eye = mats.read_img(os.path.join(base, "T_Eye_Brown.png"), (E, E))[..., :3]
        canvas[bh + pad:bh + pad + E, pad:pad + E] = eye
    cpath = mats.save_rgb(canvas, os.path.join(d, "skin_fast_c.jpg"), 90)

    def remap(obj, fn):
        me = obj.data
        while len(me.uv_layers) > 1:
            me.uv_layers.remove(me.uv_layers[len(me.uv_layers) - 1])
        uv = np.empty(len(me.loops) * 2, dtype=np.float32)
        me.uv_layers[0].data.foreach_get('uv', uv)
        uv = uv.reshape(-1, 2)
        lo, hi = uv.min(0), uv.max(0)
        if lo.min() < -0.01 or hi.max() > 1.01:
            uv = uv - np.floor(uv)          # tiled (e.g. second eye): wrap into 0..1
        uv = fn(np.clip(uv, 0.002, 0.998))
        me.uv_layers[0].data.foreach_set('uv', uv.astype(np.float32).ravel())
    remap(body, lambda uv: uv * np.array((1.0, bh / size)))
    if eyes is not None:
        remap(eyes, lambda uv: (uv * E + np.array((pad, bh + pad))) / size)
    return mats.final_material("skin_" + cid, cpath, None, None, occlusion=False)


def make_hair_material(cid, objs, tint=(0.12, 0.09, 0.07), size=512, rough=0.62, gain=1.0, **_ignored):
    """FAST preview only: flat tinted hair. Full builds bake the hair (bake_hair)."""
    return mats.final_material("hair_" + cid, color=mats._scale(tint, gain * 1.2), rough=rough, double_sided=True, occlusion=False)


SKIN_FILES = {"male": ("T_Superhero_Male_Dark.png", "T_Superhero_Male_Normal.png", "T_Superhero_Male_Roughness.png"),
              "female": ("T_Superhero_Female_Dark_BaseColor.png", "T_Superhero_Female_Normal.png",
                         "T_Superhero_Female_Roughness.png")}


# ---------------------------------------------------------------------------------------------- skin / hair (baked)
def _name_src_uv(o):
    me = o.data
    while len(me.uv_layers) > 1:
        me.uv_layers.remove(me.uv_layers[len(me.uv_layers) - 1])
    me.uv_layers[0].name = "uv_src"


def _atlas_layer(J):
    """add + activate the atlas UV layer 'UVMap' as a copy of 'uv_src'"""
    me = J.data
    me.uv_layers.active = me.uv_layers["uv_src"]
    lay = me.uv_layers.new(name="UVMap")
    me.uv_layers.active = lay
    lay.active_render = True
    return lay


def _scale_islands(J, mat_index, factor):
    """scale the atlas UVs of all triangles of one material slot about their centroid"""
    me = J.data
    uv, tri, ar, mi = _uv_arrays(J)
    sel = np.unique(tri[mi == mat_index].ravel())
    if len(sel) == 0:
        return
    c = uv[sel].mean(0)
    uv[sel] = (uv[sel] - c) * factor + c
    me.uv_layers.active.data.foreach_set('uv', uv.ravel())


def _heat_attr(J, pts, reach=0.009, cap=0.5):
    """'heat_d' on the skin = distance to emissive geometry that lies on the skin (ember welts / brands)"""
    n = len(J.data.vertices)
    if pts is None or len(pts) == 0:
        _set_float_attr(J, "heat_d", cap)
        return 0
    near = []
    for p in pts:
        ok, loc, nrm, fi = J.closest_point_on_mesh(Vector(p))
        if ok and (Vector(p) - loc).length < reach:
            near.append(np.array(loc))
    if not near:
        _set_float_attr(J, "heat_d", cap)
        return 0
    kd = kdtree.KDTree(len(near))
    for i, p in enumerate(near):
        kd.insert(Vector(p), i)
    kd.balance()
    co = get_co(J)
    d = np.array([kd.find(Vector(c))[2] for c in co], dtype=np.float32)
    _set_float_attr(J, "heat_d", np.minimum(d, cap))
    return len(near)


def _emis_points(objs):
    """vertices + edge midpoints (+ thirds) of the emissive body parts, rest space"""
    out = []
    for o in objs:
        if o.get("owner", "body") != "body":
            continue
        co = get_co(o)
        e = mk.mesh_edges(o)
        out.append(co)
        if len(e):
            for t in (0.33, 0.5, 0.67):
                out.append(co[e[:, 0]] * (1 - t) + co[e[:, 1]] * t)
    return np.concatenate(out) if out else None


def bake_skin(cid, J, kind, lm, size, look, scar_pts=None):
    """J = joined body (+ eyes) mesh with layer 'uv_src' and material slots [skin, eyes]. -> Atlas"""
    base = os.path.join(SRC, "base")
    files = tuple(os.path.join(base, f) for f in SKIN_FILES[kind])
    kw = {k: look[k] for k in ("stubble", "scars", "grime", "pores", "age", "rough_add") if k in look}
    mats.skin_src("__skin", files, "uv_src", lm, male=(kind == "male"), **kw)
    mats.eye_src("__eye", os.path.join(base, "T_Eye_Brown.png"), os.path.join(base, "T_Eye_Normal.png"), "uv_src")
    me = J.data
    eye_slot = None
    for i, m in enumerate(list(me.materials)):
        if m is not None and m.name.startswith("__tag_eye"):
            me.materials[i] = mats.SRCM["__eye"]
            eye_slot = i
        else:
            me.materials[i] = mats.SRCM["__skin"]
    nscar = _heat_attr(J, scar_pts)
    _atlas_layer(J)
    if eye_slot is not None:
        # eyes used their own texture: bring them to ~1.6x the texel density of the face before packing
        uv, tri, ar, mi = _uv_arrays(J)
        ua = _uv_area(uv, tri)
        co = get_co(J)
        vi = np.empty(len(me.loops), dtype=np.int32)
        me.loops.foreach_get('vertex_index', vi)
        zc = co[vi[tri]][:, :, 2].mean(1)
        face = (mi != eye_slot) & (zc > lm['cz'])
        eyes = mi == eye_slot
        if face.any() and eyes.any() and ua[eyes].sum() > 0:
            dens_f = ua[face].sum() / max(ar[face].sum(), 1e-9)
            dens_e = ua[eyes].sum() / max(ar[eyes].sum(), 1e-9)
            _scale_islands(J, eye_slot, math.sqrt(dens_f * 2.6 / dens_e))
    tu = time.time()
    _repack(J)
    log("repack skin: %.0fs" % (time.time() - tu))
    P = _bake_set(J, size, "skin", edge=False)
    # colour grade of the character (same meaning as v1: saturation / tint / gain on the display-encoded colour)
    col = P['color']
    sat, tint, gain = look.get("sat", 1.0), look.get("tint", (1, 1, 1)), look.get("gain", 1.0)
    lum = (col * np.array((0.2126, 0.7152, 0.0722), dtype=np.float32)).sum(-1, keepdims=True)
    col = lum + (col - lum) * sat
    P['color'] = np.clip(col * np.array(tint, dtype=np.float32) * gain, 0, 1).astype(np.float32)
    log("skin: %d welt points on the skin, atlas %d" % (nscar, size))
    return Atlas(cid, "skin", P, 1.0)


def bake_hair(cid, J, size, look, center):
    """J = joined hair / brow meshes with 'uv_src' and tag materials __tag_hair1 / __tag_hair2. -> Atlas"""
    hd = os.path.join(SRC, "hair")
    me = J.data
    for i, m in enumerate(list(me.materials)):
        w = 2 if (m is not None and m.name.startswith("__tag_hair2")) else 1
        key = "__hair%d" % w
        if key not in mats.SRCM:
            mats.hair_src(key, os.path.join(hd, "T_Hair_%d_BaseColor.png" % w), os.path.join(hd, "T_Hair_%d_Normal.png" % w),
                          "uv_src", look.get("tint", (0.12, 0.09, 0.07)), look.get("gain", 1.0), look.get("rough", 0.5), center)
        me.materials[i] = mats.SRCM[key]
    _atlas_layer(J)
    tu = time.time()
    _repack(J)
    log("repack hair: %.0fs" % (time.time() - tu))
    P = _bake_set(J, size, "hair", edge=False)
    return Atlas(cid, "hair", P, 0.5)


def _tag_material(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    return m


# ---------------------------------------------------------------------------------------------- emissive heat gradient
def _emis_heat(J, cid, color):
    """UV.u = heat along each connected piece (hot middle, cooler tapering ends, small pieces fully hot) and a tiny
    gradient texture: dim deep colour -> the accent colour -> a whiter, hotter core."""
    me = J.data
    if not len(me.uv_layers):
        me.uv_layers.new(name="UVMap")
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    n = len(bm.verts)
    comp = -np.ones(n, dtype=np.int32)
    k = 0
    for v in bm.verts:
        if comp[v.index] >= 0:
            continue
        stack = [v]
        comp[v.index] = k
        while stack:
            a = stack.pop()
            for e in a.link_edges:
                b = e.other_vert(a)
                if comp[b.index] < 0:
                    comp[b.index] = k
                    stack.append(b)
        k += 1
    bm.free()
    co = get_co(J)
    heat = np.full(n, 0.70, dtype=np.float32)
    for c in range(k):
        idx = np.nonzero(comp == c)[0]
        p = co[idx]
        cen = p.mean(0)
        if len(idx) < 4:
            continue
        w, v = np.linalg.eigh(np.cov((p - cen).T))
        ax = v[:, -1]
        t = (p - cen) @ ax
        ext = t.max() - t.min()
        if ext < 0.05:
            continue
        tn = np.abs(t - (t.max() + t.min()) / 2) / (ext / 2)
        heat[idx] = 0.86 - 0.80 * tn ** 1.3
    heat = np.clip(heat + 0.08 * np.sin(co[:, 0] * 173.0 + co[:, 1] * 131.0 + co[:, 2] * 211.0), 0.03, 0.97)
    vi = np.empty(len(me.loops), dtype=np.int32)
    me.loops.foreach_get('vertex_index', vi)
    uv = np.empty((len(me.loops), 2), dtype=np.float32)
    uv[:, 0] = heat[vi]
    uv[:, 1] = 0.5
    me.uv_layers[0].data.foreach_set('uv', uv.ravel())
    c = np.array(color[:3], dtype=np.float32)
    lo = c * np.array((0.42, 0.20, 0.20), dtype=np.float32) * 0.6 if c[0] > c[2] else c * 0.22
    hot = np.clip(c + (1.0 - c) * 0.38, 0, 1)
    t = np.linspace(0, 1, 64, dtype=np.float32)[None, :, None]
    ramp = np.where(t < 0.62, lo + (c - lo) * (t / 0.62) ** 1.3, c + (hot - c) * ((t - 0.62) / 0.38) ** 1.5)
    d = os.path.join(TEX, cid)
    os.makedirs(d, exist_ok=True)
    return mats.save_rgb(mats.lin2srgb(np.repeat(ramp, 4, axis=0)), os.path.join(d, "emis_g.png"))


# ---------------------------------------------------------------------------------------------- main
def _decimate(obj, ratio):
    if ratio >= 0.999:
        return
    m = obj.modifiers.new("dec", 'DECIMATE')
    m.decimate_type = 'COLLAPSE'
    m.ratio = ratio
    m.use_collapse_triangulate = True
    mk.apply_mods(obj)


def _auto_size(area, texel, cap=2048, cover=0.62):
    need = math.sqrt(max(area, 1e-6) / cover) / texel
    size = 256
    while size < need * 0.74 and size < cap:
        size *= 2
    return size, need


def _export(path, finals, arm):
    _select_only(finals + [arm], arm)
    bpy.ops.export_scene.gltf(
        filepath=path, export_format='GLB', use_selection=True,
        export_animations=False, export_skins=True, export_yup=True, export_apply=False,
        export_materials='EXPORT', export_image_format='AUTO', export_jpeg_quality=92, export_image_quality=92,
        export_tangents=True, export_normals=True, export_texcoords=True, export_vertex_color='NONE',
        export_all_influences=False, export_influence_nb=4, export_def_bones=False,
        export_rest_position_armature=True, export_attributes=False, export_extras=False, export_morph=False,
        export_cameras=False, export_lights=False)
    return os.path.getsize(path)


def finalize(cid, arm, skin=None, hair=None, tex=None, fast=False, notes="", emis_strength=None, extra=None,
             max_tris=35000, decimate=None):
    """skin: dict(objs=[body, eyes], kind=, landmarks=, look=dict(gain, sat, tint, ...)[, material= for fast builds])
    hair: dict(objs=[...], look=dict(tint, gain, rough)[, material=]).  tex: {cls: atlas size} = upper limit per class
    (the size itself follows the surface area).  Returns the manifest dict."""
    t0 = time.time()
    tex = dict(tex or {})
    decimate = dict(decimate or {})
    parts = list(mk.PARTS)
    owners = ["body"] + sorted({o["owner"] for o in parts if o["owner"] != "body"})
    oidx = {n: i for i, n in enumerate(owners)}
    nfin = 0
    for o in parts:
        _prep_part(o)
        if not fast and o["cls"] in BAKED:
            _hem_attr(o)
        _set_int_attr(o, "own", oidx[o["owner"]])
        if not fast and o["cls"] in BAKED:
            co_ = get_co(o)
            dim = float(np.linalg.norm(co_.max(0) - co_.min(0))) if len(co_) else 0.0
            _set_face_int(o, "small", 1 if (_area(o) < 0.012 or dim < 0.09) else 0)
    # triangle estimate of the finished character (decimation ratios applied) -> budget for the fur locks
    est = sum(mk.tri_count(o) * decimate.get(o["cls"], 1.0) for o in parts)
    for dct, k in ((skin, "skin"), (hair, "hair")):
        if dct:
            est += sum(mk.tri_count(o) for o in dct["objs"] if o is not None) * decimate.get(k, 1.0)
    fin_budget = int(max_tris - 450 - est)
    final_mats = {}
    frags = {}      # owner -> [objects]
    ds_flag = {}
    by_cls = {}
    for o in parts:
        by_cls.setdefault(o["cls"], []).append(o)
        ds_flag[o["cls"]] = ds_flag.get(o["cls"], False) or bool(o.get("ds", False))
    for cls in by_cls:
        assert cls in ORDER, "unknown class " + cls
    if not fast:
        _setup_cycles()
    d = os.path.join(TEX, cid)
    os.makedirs(d, exist_ok=True)
    scar_pts = _emis_points(by_cls.get("emis", [])) if (not fast and skin) else None

    # ---- atlas sizes from the surface areas (largest classes get the 2048 px atlases)
    sizes, needs = {}, {}
    if not fast:
        areas = {cls: sum(_area(o) for o in by_cls[cls]) * decimate.get(cls, 1.0) ** 0.15 for cls in BAKED if by_cls.get(cls)}
        if skin:
            areas["skin"] = sum(_area(o) for o in skin["objs"] if o is not None)
        if hair:
            areas["hair"] = sum(_area(o) for o in hair["objs"] if o is not None)
        for cls, a in areas.items():
            texel = SKIN_TEXEL if cls in ("skin", "hair") else TEXEL
            cap = 1024 if cls == "hair" else 2048
            sizes[cls], needs[cls] = _auto_size(a, texel, cap)
            if cls in ("skin", "hair"):
                sizes[cls] = max(sizes[cls], 512)
        big = sorted([c for c in sizes if sizes[c] >= 2048], key=lambda c: -needs[c])
        for c in big[MAX_2K:]:
            sizes[c] = 1024
        for c in tex:
            if c in sizes and isinstance(tex[c], (tuple, list)):        # tex={"cloth": ("max", 1024)} caps a class
                sizes[c] = min(sizes[c], int(tex[c][1]))
        if PREVIEW:
            sizes = {c: min(v, 1024) for c, v in sizes.items()}
        log("areas m2:", {k: round(v, 2) for k, v in areas.items()}, "atlas:", sizes)
    atlases = []

    for cls in BAKED:
        objs = by_cls.get(cls)
        if not objs:
            continue
        # source materials per object
        keys = []
        for o in objs:
            k = o["src"]
            if fast:
                nm = "%s_%s" % (cls, k)
                fm = bpy.data.materials.get(nm)
                if fm is None:
                    info = mats.SRCINFO[k]
                    fm = mats.final_material(nm, color=info["color"], rough=info["rough"], metal=info["metal"],
                                             double_sided=True)
                o.data.materials.clear()
                o.data.materials.append(fm)
            else:
                assert k in mats.SRCM, "no source material '%s' for part %s" % (k, o.name)
                o.data.materials.clear()
                o.data.materials.append(mats.SRCM[k])
            if k not in keys:
                keys.append(k)
        J = join(objs, "J_" + cls)
        _triangulate(J)
        if decimate and cls in decimate:
            _decimate(J, decimate[cls])
        if not fast:
            size = sizes[cls]
            own = _get_int_attr(J, "own")
            co = get_co(J)
            co[:, 0] += own * EXPLODE
            set_co(J, co)
            tu = time.time()
            fw = _exposure(J)
            fw = np.where(own[_face_vert0(J)] > 0, np.maximum(fw, 0.5) * 0.9, fw)      # weapons: a little less
            # fur locks: after decimation and after the visibility pass (they must not shade the pelt under them)
            made = 0
            for slot, m_ in enumerate(list(J.data.materials)):
                fur = mats.SRCINFO.get(m_.name[4:] if m_ is not None else "", {}).get("fur")
                if fur:
                    made += _fur_fins(J, slot, fin_budget - nfin - made, **fur)
            if made:
                nfin += made
                fw = np.concatenate([fw, np.ones(made, dtype=np.float32)])
                own = _get_int_attr(J, "own")
                log("fur locks added to %s: %d triangles (budget left %d)" % (cls, made, fin_budget - nfin))
            _uv_unwrap(J, face_w=fw)
            log("unwrap %s: %.0fs" % (cls, time.time() - tu))
            P = _bake_set(J, size, cls)
            co = get_co(J)
            co[:, 0] -= own * EXPLODE
            set_co(J, co)
            at = Atlas(cid, cls, P, needs[cls])
            at.save(QUAL[0])
            fm = at.material("%s_%s" % (cls, cid), double_sided=ds_flag[cls] or cls == "cloth")
            atlases.append(at)
            J.data.materials.clear()
            J.data.materials.append(fm)
        for ow, o in _split_by_owner(J, owners, cls).items():
            frags.setdefault(ow, []).append((cls, o))

    # emissive parts (not baked): heat gradient along each piece
    if by_cls.get("emis"):
        objs = by_cls["emis"]
        info = mats.SRCINFO[objs[0]["src"]]
        strength = emis_strength or info.get("strength", 6.0)
        for o in objs:
            o.data.materials.clear()
        J = join(objs, "J_emis")
        _triangulate(J)
        gpath = None if fast else _emis_heat(J, cid, info["emis"])
        fm = mats.final_material("emis_" + cid, color=info["color"], rough=0.4, metal=0.0, emis=info["emis"],
                                 strength=strength, double_sided=True, emis_path=gpath)
        J.data.materials.clear()
        J.data.materials.append(fm)
        if not len(J.data.uv_layers):
            J.data.uv_layers.new(name="UVMap")
        for ow, o in _split_by_owner(J, owners, "emis").items():
            frags.setdefault(ow, []).append(("emis", o))

    if skin:
        objs = [o for o in skin["objs"] if o is not None]
        if fast:
            for o in objs:
                o.data.materials.clear()
                o.data.materials.append(skin["material"])
        else:
            for i, o in enumerate(objs):
                _name_src_uv(o)
                o.data.materials.clear()
                o.data.materials.append(_tag_material("__tag_eye" if i > 0 else "__tag_skin"))
        for o in objs:
            clear_custom_normals(o)
            _set_int_attr(o, "own", 0)
        J = join(objs, "J_skin")
        _triangulate(J)
        if decimate and "skin" in decimate:
            _decimate(J, decimate["skin"])
        if not fast:
            at = bake_skin(cid, J, skin["kind"], skin["landmarks"], sizes["skin"], skin.get("look", {}), scar_pts)
            at.save(QUAL[0])
            fm = at.material("skin_" + cid)
            atlases.append(at)
            J.data.materials.clear()
            J.data.materials.append(fm)
        frags.setdefault("body", []).append(("skin", J))
    if hair:
        objs = [o for o in hair["objs"] if o is not None]
        if fast:
            for o in objs:
                o.data.materials.clear()
                o.data.materials.append(hair["material"])
        else:
            for o in objs:
                which = int(o["hair_tex"]) if "hair_tex" in o.keys() else 1
                _name_src_uv(o)
                o.data.materials.clear()
                o.data.materials.append(_tag_material("__tag_hair%d" % which))
        for o in objs:
            clear_custom_normals(o)
            _set_int_attr(o, "own", 0)
        J = join(objs, "J_hair")
        _triangulate(J)
        if decimate and "hair" in decimate:
            _decimate(J, decimate["hair"])
        if not fast:
            at = bake_hair(cid, J, sizes["hair"], hair.get("look", {}), hair.get("center", (0.0, 0.0)))
            at.save(QUAL[0])
            fm = at.material("hair_" + cid, double_sided=True)
            atlases.append(at)
            J.data.materials.clear()
            J.data.materials.append(fm)
        frags.setdefault("body", []).append(("hair", J))

    # name final objects
    finals = []
    for ow, lst in frags.items():
        lst.sort(key=lambda t: ORDER.index(t[0]))
        if ow == "body":
            for cls, o in lst:
                o.name = "body_" + cls
                o.data.name = o.name
                finals.append(o)
        else:
            o = join([o for _, o in lst], ow)
            finals.append(o)
    for o in finals:
        _strip_layers(o)
        _prune_groups(o)
        rebind(o, arm)
    # stats
    info = dict(id=cid, file="%s.glb" % cid, meshes=[], materials=[], triangles=0, notes=notes)
    allco = []
    for o in sorted(finals, key=lambda o: o.name):
        tc = mk.tri_count(o)
        info["meshes"].append(dict(name=o.name, triangles=tc, materials=[m.name for m in o.data.materials]))
        for m in o.data.materials:
            if m.name not in info["materials"]:
                info["materials"].append(m.name)
        if o.name.startswith("body_"):
            allco.append(get_co(o))
            info["triangles_body"] = info.get("triangles_body", 0) + tc
        info["triangles"] += tc
    co = np.concatenate(allco)
    mn, mx = co.min(0), co.max(0)
    # glTF axes: +Y up, character faces +Z (Blender -Y)
    info["height_m"] = round(float(mx[2]), 3)
    info["bbox_blender_zup"] = dict(min=[round(float(v), 3) for v in mn], max=[round(float(v), 3) for v in mx])
    info["bbox_gltf_yup"] = dict(min=[round(float(mn[0]), 3), round(float(mn[2]), 3), round(float(-mx[1]), 3)],
                                 max=[round(float(mx[0]), 3), round(float(mx[2]), 3), round(float(-mn[1]), 3)])
    info["bbox_note"] = "rest pose (T-pose, arms out), body_* meshes only (weapons excluded)"
    if extra:
        info.update(extra)
    # export; fit the textures into the file budget (JPEG quality steps, then halve the least needed big atlas)
    for o in list(bpy.data.objects):
        if o is not arm and o not in finals:
            o.hide_render = True
    path = os.path.join(OUT, cid + ".glb")
    size_b = _export(path, finals, arm)
    step = 0
    while size_b > MAX_BYTES and atlases and step < 8:
        step += 1
        if step < len(QUAL):
            for at in atlases:
                at.save(QUAL[step])
            log("over budget (%.2f MB): JPEG quality step %d %s" % (size_b / 1e6, step, QUAL[step]))
        else:
            cand = [a for a in atlases if a.size > 512]
            if not cand:
                break
            a = max(cand, key=lambda a: (a.size, -a.need))
            a.halve()
            a.save(QUAL[-1])
            log("over budget (%.2f MB): %s atlas halved to %d px" % (size_b / 1e6, a.cls, a.size))
        size_b = _export(path, finals, arm)
    info["size_bytes"] = size_b
    info["textures"] = {a.cls: a.size for a in atlases}
    info["fast_preview_build"] = bool(fast)
    with open(os.path.join(MANI, cid + ".json"), "w", encoding="utf8") as f:
        json.dump(info, f, indent=1)
    log("EXPORTED %s  tris=%d (body %d)  mats=%d  size=%.2f MB  tex=%s  time=%.0fs" % (
        path, info["triangles"], info.get("triangles_body", 0), len(info["materials"]), info["size_bytes"] / 1e6,
        info["textures"], time.time() - t0))
    for m in info["meshes"]:
        log("   mesh %-12s tris=%-6d mats=%s" % (m["name"], m["triangles"], m["materials"]))
    if info["triangles"] > max_tris:
        log("WARNING: triangle budget exceeded", info["triangles"], ">", max_tris)
    return info
