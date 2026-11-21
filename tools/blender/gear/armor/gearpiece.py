"""gearpiece -- armour pieces: which body regions a piece takes over (hides), the skin it carries along, export."""
from gearlib import *
from gearfin import *

EXT_HERO = "EXTH_"      # image name prefixes: textures that stay external (shared files next to the hero .glb files)
EXT_BASE = "EXTB_"
RAY_DIST = {"helm": 0.13, "chest": 0.10, "gloves": 0.07, "pants": 0.045, "boots": 0.11}
HIDE_MIN = 0.15         # an owned skin region is taken over when at least this share of its area is covered
CARRY_MIN = 0.03        # ... and its uncovered remainder is carried when it is at least this share


def import_glb(path, tag=None):
    """-> (armature, {mesh name: object}); images get the prefix `tag` (see uri_rule)"""
    before_o = set(bpy.data.objects)
    before_i = set(bpy.data.images)
    bpy.ops.import_scene.gltf(filepath=path)
    objs = [o for o in bpy.data.objects if o not in before_o]
    arm = next(o for o in objs if o.type == 'ARMATURE')
    meshes = {}
    for o in objs:
        if o.type == 'MESH' and o.parent is arm:
            meshes[o.name.split(".")[0]] = o
        elif o is not arm:
            bpy.data.objects.remove(o, do_unlink=True)
    for pb in arm.pose.bones:
        pb.custom_shape = None
    if tag:
        for im in [im for im in bpy.data.images if im not in before_i]:
            if not im.name.startswith((EXT_HERO, EXT_BASE)):
                im.name = tag + im.name.split(".")[0]
    purge_actions()
    return arm, meshes


def uri_rule(cls):
    """image name -> external uri (relative to game/assets/gear/armor/) for the shared hero / base-body textures"""
    def f(name, mime):
        ext = "png" if "png" in mime else "jpg"
        n = name.split(".")[0]
        if n.startswith(EXT_HERO):
            return "../../chars/hero_%s_%s.%s" % (cls, n[len(EXT_HERO):], ext)
        if n.startswith(EXT_BASE):
            return "../../chars/hero_%s_base_%s.%s" % (cls, n[len(EXT_BASE):], ext)
        return None
    return f


def _tris_of(obj):
    me = obj.data
    me.calc_loop_triangles()
    t = np.empty(len(me.loop_triangles) * 3, dtype=np.int32)
    me.loop_triangles.foreach_get('vertices', t)
    return t.reshape(-1, 3)


def bvh_of(objs):
    from mathutils.bvhtree import BVHTree
    V, F, off = [], [], 0
    for o in objs:
        co = get_co(o)
        if not len(co):
            continue
        V.append(co)
        F.append(_tris_of(o) + off)
        off += len(co)
    if not V:
        return None
    V = np.concatenate(V)
    F = np.concatenate(F)
    return BVHTree.FromPolygons([tuple(map(float, v)) for v in V], [tuple(map(int, f)) for f in F], all_triangles=True)


def coverage(obj, bvh, dist):
    """per-vertex bool: is this (rest pose) vertex of `obj` hidden under the geometry in `bvh`? 5 rays inside a cone
    round the outward normal; covered when at least 4 of them hit something within `dist`"""
    me = obj.data
    co = get_co(obj)
    n = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get('normal', n)
    n = n.reshape(-1, 3)
    out = np.zeros(len(co), dtype=bool)
    if bvh is None:
        return out
    for i in range(len(co)):
        nn = Vector(n[i])
        if nn.length < 0.5:
            continue
        p = Vector(co[i]) + nn * 0.0015
        t1 = nn.orthogonal().normalized()
        t2 = nn.cross(t1)
        hit = 0
        for d in (nn, nn + t1 * 0.6, nn - t1 * 0.6, nn + t2 * 0.6, nn - t2 * 0.6):
            if bvh.ray_cast(p, d.normalized(), dist)[0] is not None:
                hit += 1
        out[i] = hit >= 4
    return out


def _face_areas(obj):
    me = obj.data
    ar = np.empty(len(me.polygons), dtype=np.float32)
    me.polygons.foreach_get('area', ar)
    return ar


def _face_verts3(obj):
    """(F, 3) vertex indices of a triangulated mesh"""
    me = obj.data
    vi = np.empty(len(me.loops), dtype=np.int32)
    me.loops.foreach_get('vertex_index', vi)
    ls = np.empty(len(me.polygons), dtype=np.int32)
    me.polygons.foreach_get('loop_start', ls)
    return np.stack([vi[ls], vi[ls + 1], vi[ls + 2]], axis=1)


def _weld_map(co, tol=1e-5):
    """index of the first vertex at the same position (glTF meshes are split at UV seams: the ring test below has to
    look across those seams)"""
    key = np.round(co / tol).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    return inv.reshape(-1)


def hides_and_carry(slot, garments, base, arm, ray=None, force_hide=(), never_hide=(), carry=True, hide_min=None):
    """Which base-body regions does this piece take over, and which skin does it have to bring along?
    garments: the piece's mesh objects in rest pose; base: {mesh name: object} of hero_<class>_base.glb.
    Returns (hides tokens, carried skin object or None, report dict)."""
    dist = ray or RAY_DIST[slot]
    bvh = bvh_of(garments)
    hides, carried, rep = [], [], {}
    for tok in SLOT_OWNS[slot]:
        o = base.get(REGION_MESH[tok])
        if o is None or tok in never_hide:
            continue
        cov = coverage(o, bvh, dist)
        fv = _face_verts3(o)
        ar = _face_areas(o)
        tot = float(ar.sum()) or 1.0
        fcov = cov[fv].all(1)
        frac = float(ar[fcov].sum()) / tot
        rep[tok] = round(frac, 3)
        if tok not in SKIN_REGIONS:
            # hair / beard / brows / wraps are hidden as a whole or not at all
            lim = {"hair": 0.20, "beard": 0.5, "brows": 0.6, "wrap_chest": 0.5, "wrap_loin": 0.5}[tok]
            if frac >= lim or tok in force_hide:
                hides.append(tok)
            continue
        lim = 0.90 if tok == "head" else (hide_min if hide_min is not None else HIDE_MIN)
        if frac < lim and tok not in force_hide:
            continue
        hides.append(tok)
        if tok == "head" or not carry:
            continue
        # keep one ring of skin under the edge of the garment (rings counted across UV seams)
        wm = _weld_map(get_co(o))
        unc_w = np.zeros(wm.max() + 1, dtype=bool)
        unc_w[wm[~cov]] = True
        e = wm[mk.mesh_edges(o)]
        grow = unc_w.copy()
        grow[e[unc_w[e[:, 0]], 1]] = True
        grow[e[unc_w[e[:, 1]], 0]] = True
        keep = grow[wm[fv]].any(1)
        if float(ar[keep].sum()) / tot < CARRY_MIN:
            continue
        res = split_faces(o, keep.astype(np.int32), {1: "carry_" + tok}, arm, remove=False)
        if res:
            carried.append(res["carry_" + tok])
            rep[tok + "_carried"] = round(float(ar[keep].sum()) / tot, 3)
    # the wraps go when the skin they lie on goes
    if slot == "chest" and "torso" in hides and "wrap_chest" not in hides and REGION_MESH["wrap_chest"] in base:
        hides.append("wrap_chest")
    if slot == "pants":
        for tok in ("wrap_loin", "hips"):
            if tok not in hides and tok not in never_hide and REGION_MESH[tok] in base:
                hides.append(tok)
    sk = None
    if carried:
        for o in carried:
            store_corner_normals(o)
        sk = fin.join(carried, "carry_skin")
        restore_corner_normals(sk)
    return hides, sk, rep


def load_base(cls):
    path = os.path.join(OUT_CHARS, "hero_%s_base.glb" % cls)
    arm, meshes = import_glb(path, EXT_BASE)
    for o in meshes.values():
        o.hide_render = True
    return arm, meshes


def write_piece(cls, slot, look, objs, arm, hides, extra=None):
    """export game/assets/gear/armor/arm_<cls>_<slot>_<look>.glb + its manifest fragment in _work/pieces/"""
    pid = "arm_%s_%s_%s" % (cls, slot, look)
    names = []
    tris = 0
    mats_used = []
    mn, mx = np.array([1e9] * 3), np.array([-1e9] * 3)
    for i, o in enumerate(objs):
        strip_obj(o, {"hide_body_regions": ",".join(hides), "slot": slot})
        rebind(o, arm)
        o.hide_render = False
        tris += mk.tri_count(o)
        for m in o.data.materials:
            if m is not None and m.name not in mats_used:
                mats_used.append(m.name)
        co = get_co(o)
        if len(co):
            mn, mx = np.minimum(mn, co.min(0)), np.maximum(mx, co.max(0))
        names.append(o.name)
    path = os.path.join(OUT_ARMOR, pid + ".glb")
    export_glb(path, objs, arm)
    ext = glb_externalize(path, uri_rule(cls))
    glb_set_extras(path, {"hide_body_regions": ",".join(hides), "slot": slot, "class": cls, "look": look})
    ent = dict(id=pid, kind="armor", class_key=cls, slot=slot, look=look,
               file="res://assets/gear/armor/%s.glb" % pid, hides=list(hides), meshes=names, triangles=tris,
               materials=mats_used, shared_textures=sorted({u for _, u in ext}),
               bbox=[[round(float(v), 3) for v in mn], [round(float(v), 3) for v in mx]],
               size_bytes=os.path.getsize(path))
    ent["class"] = CLASS_NAME[cls]
    if extra:
        ent.update(extra)
    os.makedirs(os.path.join(WORK, "pieces"), exist_ok=True)
    with open(os.path.join(WORK, "pieces", pid + ".json"), "w", encoding="utf8") as f:
        json.dump(ent, f, indent=1)
    log("PIECE %s  tris=%d  size=%.2f MB  hides=%s  mats=%s  external=%d" % (pid, tris, ent["size_bytes"] / 1e6, hides, mats_used, len(ext)))
    return ent
