"""Join parts per material class, unwrap, bake atlases, build final materials, export .glb + manifest entry."""
import bpy, bmesh, math, os, json, time
import numpy as np
from rigio import *
import mk
import mats

BAKED = ("cloth", "leather", "metal", "bone")
ORDER = ("skin", "hair", "cloth", "leather", "metal", "bone", "emis")
MANI = r"Y:\tools\blender\heroes\_manifest"
os.makedirs(MANI, exist_ok=True)
EXPLODE = 6.0
AO_MULT = 0.55
DIRT = 0.45
DIRT_COL = np.array((0.40, 0.31, 0.24))


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


def _triangulate(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


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


def _uv_unwrap(obj, angle=66.0, margin=0.004):
    me = obj.data
    while len(me.uv_layers):
        me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name="UVMap")
    _select_only([obj], obj)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle), island_margin=margin, area_weight=0.0,
                             correct_aspect=True, scale_to_bounds=False)
    try:
        bpy.ops.uv.select_all(action='SELECT')
        bpy.ops.uv.average_islands_scale()
        bpy.ops.uv.pack_islands(margin=margin, rotate=True)
    except Exception as e:
        log("pack_islands:", e)
    bpy.ops.object.mode_set(mode='OBJECT')


def _bake(obj, srcmats, size, kind, samples):
    sc = bpy.context.scene
    img = bpy.data.images.new("bake_%s" % kind, size, size, alpha=False, float_buffer=True)
    img.colorspace_settings.name = 'Non-Color'
    for m in srcmats:
        nt = m.node_tree
        N = nt.nodes
        tg = N['TARGET']
        tg.image = img
        for n in N:
            n.select = False
        tg.select = True
        N.active = tg
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
    sc.cycles.samples = samples
    _select_only([obj], obj)
    typ = {'color': 'EMIT', 'orm': 'EMIT', 'normal': 'NORMAL', 'ao': 'AO'}[kind]
    bpy.ops.object.bake(type=typ, margin=8, use_clear=True, target='IMAGE_TEXTURES')
    px = np.empty(size * size * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(size, size, 4)


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
    sc.world.light_settings.distance = 0.35
    # fixed thread count: unbounded Cycles bakes in several Blender processes at once hung the laptop
    import os as _os
    sc.render.threads_mode = 'FIXED'
    sc.render.threads = max(1, min(32, int(_os.environ.get('ASHEN_BLENDER_THREADS', '12'))))


def _strip_layers(obj, keep_uv=True):
    me = obj.data
    for nm in ("own", "luv", "src_vid"):
        a = me.attributes.get(nm)
        if a is not None:
            me.attributes.remove(a)
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)
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
    for g in list(obj.vertex_groups):
        if g.index not in used:
            pass
    # removing groups re-indexes; do it by name
    names = [g.name for g in obj.vertex_groups if g.index not in used]
    for n in names:
        obj.vertex_groups.remove(obj.vertex_groups[n])


# ---------------------------------------------------------------------------------------------- skin / hair
def make_skin_material(cid, kind, body, eyes=None, size=1024, gain=1.0, sat=1.0, tint=(1, 1, 1), rough_add=0.0,
                       eye_emis=False):
    """Body atlas: source 2048 skin texture squeezed into the lower 90 %, eye texture in the top strip."""
    d = os.path.join(TEX, cid)
    os.makedirs(d, exist_ok=True)
    base = os.path.join(SRC, "base")
    files = {"male": ("T_Superhero_Male_Dark.png", "T_Superhero_Male_Normal.png", "T_Superhero_Male_Roughness.png"),
             "female": ("T_Superhero_Female_Dark_BaseColor.png", "T_Superhero_Female_Normal.png",
                        "T_Superhero_Female_Roughness.png")}[kind]
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
    cpath = mats.save_rgb(canvas, os.path.join(d, "skin_c.jpg"), 92)
    nsz = size
    nrm = mats.read_img(os.path.join(base, files[1]), (nsz, int(nsz * 0.9)))[..., :3]
    ncan = np.empty((nsz, nsz, 3), dtype=np.float32)
    ncan[:] = (0.5, 0.5, 1.0)
    ncan[:int(nsz * 0.9)] = nrm
    npath = mats.save_rgb(ncan, os.path.join(d, "skin_n.jpg"), 93)
    rsz = size // 2
    rgh = mats.read_img(os.path.join(base, files[2]), (rsz, int(rsz * 0.9)))[..., :3]
    rcan = np.zeros((rsz, rsz, 3), dtype=np.float32)
    rcan[..., 0] = 1.0
    rcan[..., 1] = 0.6
    rcan[:int(rsz * 0.9), :, 1] = np.clip(rgh[..., 1] + rough_add, 0, 1)
    opath = mats.save_rgb(rcan, os.path.join(d, "skin_orm.jpg"), 88)
    # UV remap
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
    m = mats.final_material("skin_" + cid, cpath, opath, npath, occlusion=False)
    return m


def make_hair_material(cid, objs, tint=(0.12, 0.09, 0.07), size=512, rough=0.62, gain=1.0):
    """Hair atlas: T_Hair_1 in the left half, T_Hair_2 in the right half, tinted."""
    d = os.path.join(TEX, cid)
    os.makedirs(d, exist_ok=True)
    hd = os.path.join(SRC, "hair")
    canvas = np.zeros((size, size * 2, 3), dtype=np.float32)
    ncan = np.zeros((size, size * 2, 3), dtype=np.float32)
    for i in (1, 2):
        c = mats.read_img(os.path.join(hd, "T_Hair_%d_BaseColor.png" % i), (size, size))[..., :3]
        lum = (c * np.array((0.2126, 0.7152, 0.0722))).sum(-1, keepdims=True)
        lum = np.clip((lum - 0.25) / 0.6, 0.05, 1.0) ** 1.2          # greyscale strand shading
        tl = mats.lin2srgb(np.array(tint) * gain)
        canvas[:, (i - 1) * size:i * size] = np.clip(lum * tl * 1.6, 0, 1)
        n = mats.read_img(os.path.join(hd, "T_Hair_%d_Normal.png" % i), (size, size))[..., :3]
        ncan[:, (i - 1) * size:i * size] = n
    cpath = mats.save_rgb(canvas, os.path.join(d, "hair_c.jpg"), 90)
    npath = mats.save_rgb(ncan, os.path.join(d, "hair_n.jpg"), 90)
    for o in objs:
        me = o.data
        which = 2 if any(m and "Hair_2" in m.name for m in me.materials) else 1
        if "hair_tex" in o.keys():
            which = int(o["hair_tex"])
        while len(me.uv_layers) > 1:
            me.uv_layers.remove(me.uv_layers[len(me.uv_layers) - 1])
        uv = np.empty(len(me.loops) * 2, dtype=np.float32)
        me.uv_layers[0].data.foreach_get('uv', uv)
        uv = uv.reshape(-1, 2)
        uv = uv - np.floor(uv.min(0))            # bring into 0..1 if offset by whole tiles
        uv = np.clip(uv, 0.002, 0.998)
        uv[:, 0] = (uv[:, 0] + (which - 1)) * 0.5
        me.uv_layers[0].data.foreach_set('uv', uv.astype(np.float32).ravel())
    m = mats.final_material("hair_" + cid, cpath, None, npath, rough=rough, double_sided=True, occlusion=False)
    return m


# ---------------------------------------------------------------------------------------------- main
def _decimate(obj, ratio):
    if ratio >= 0.999:
        return
    m = obj.modifiers.new("dec", 'DECIMATE')
    m.decimate_type = 'COLLAPSE'
    m.ratio = ratio
    m.use_collapse_triangulate = True
    mk.apply_mods(obj)


def finalize(cid, arm, skin=None, hair=None, tex=None, fast=False, notes="", emis_strength=None, extra=None,
             max_tris=35000, decimate=None):
    """skin: dict(objs=[body, eyes], material=...) ; hair: dict(objs=[...], material=...)
    tex: {cls: atlas size}. Returns manifest dict."""
    t0 = time.time()
    tex = dict({"cloth": 1024, "leather": 512, "metal": 1024, "bone": 512}, **(tex or {}))
    decimate = dict(decimate or {})
    parts = list(mk.PARTS)
    owners = ["body"] + sorted({o["owner"] for o in parts if o["owner"] != "body"})
    oidx = {n: i for i, n in enumerate(owners)}
    for o in parts:
        _prep_part(o)
        _set_int_attr(o, "own", oidx[o["owner"]])
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
            size = tex[cls]
            own = _get_int_attr(J, "own")
            co = get_co(J)
            co[:, 0] += own * EXPLODE
            set_co(J, co)
            _uv_unwrap(J)
            srcm = [mats.SRCM[k] for k in keys]
            tb = time.time()
            P = {}
            for kind, smp in (("color", 12), ("orm", 8), ("normal", 1), ("ao", 48)):
                P[kind] = _bake(J, srcm, size, kind, smp)
            log("baked", cls, size, "in %.1fs" % (time.time() - tb), "tris", mk.tri_count(J))
            co = get_co(J)
            co[:, 0] -= own * EXPLODE
            set_co(J, co)
            ao = np.clip(P["ao"][..., 0:1], 0, 1)
            col = np.clip(P["color"][..., :3], 0, 1)
            cav = (1.0 - ao) ** 1.5
            col = col * (1 - DIRT * cav) + col * DIRT_COL * 2.0 * DIRT * cav
            col = col * (1.0 - AO_MULT * (1.0 - ao))
            cpath = mats.save_rgb(mats.lin2srgb(col), os.path.join(d, cls + "_c.jpg"), 92)
            orm = np.clip(P["orm"][..., :3], 0, 1).copy()
            orm[..., 0] = np.clip(ao[..., 0] ** 0.8, 0, 1)
            opath = mats.save_rgb(orm, os.path.join(d, cls + "_orm.jpg"), 90)
            npath = mats.save_rgb(np.clip(P["normal"][..., :3], 0, 1), os.path.join(d, cls + "_n.jpg"), 95)
            fm = mats.final_material("%s_%s" % (cls, cid), cpath, opath, npath,
                                     double_sided=ds_flag[cls] or cls == "cloth")
            J.data.materials.clear()
            J.data.materials.append(fm)
        for ow, o in _split_by_owner(J, owners, cls).items():
            frags.setdefault(ow, []).append((cls, o))

    # emissive parts (not baked)
    if by_cls.get("emis"):
        objs = by_cls["emis"]
        info = mats.SRCINFO[objs[0]["src"]]
        strength = emis_strength or info.get("strength", 6.0)
        fm = mats.final_material("emis_" + cid, color=info["color"], rough=0.4, metal=0.0, emis=info["emis"],
                                 strength=strength, double_sided=True)
        for o in objs:
            o.data.materials.clear()
            o.data.materials.append(fm)
        J = join(objs, "J_emis")
        _triangulate(J)
        if not len(J.data.uv_layers):
            J.data.uv_layers.new(name="UVMap")
        for ow, o in _split_by_owner(J, owners, "emis").items():
            frags.setdefault(ow, []).append(("emis", o))

    if skin:
        objs = [o for o in skin["objs"] if o is not None]
        for o in objs:
            o.data.materials.clear()
            o.data.materials.append(skin["material"])
            clear_custom_normals(o)
            _set_int_attr(o, "own", 0)
        J = join(objs, "J_skin")
        _triangulate(J)
        if decimate and "skin" in decimate:
            _decimate(J, decimate["skin"])
        frags.setdefault("body", []).append(("skin", J))
    if hair:
        objs = [o for o in hair["objs"] if o is not None]
        for o in objs:
            o.data.materials.clear()
            o.data.materials.append(hair["material"])
            clear_custom_normals(o)
            _set_int_attr(o, "own", 0)
        J = join(objs, "J_hair")
        _triangulate(J)
        if decimate and "hair" in decimate:
            _decimate(J, decimate["hair"])
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
    # export
    for o in list(bpy.data.objects):
        if o is not arm and o not in finals:
            o.hide_render = True
    _select_only(finals + [arm], arm)
    path = os.path.join(OUT, cid + ".glb")
    bpy.ops.export_scene.gltf(
        filepath=path, export_format='GLB', use_selection=True,
        export_animations=False, export_skins=True, export_yup=True, export_apply=False,
        export_materials='EXPORT', export_image_format='AUTO', export_jpeg_quality=92, export_image_quality=92,
        export_tangents=False, export_normals=True, export_texcoords=True, export_vertex_color='NONE',
        export_all_influences=False, export_influence_nb=4, export_def_bones=False,
        export_rest_position_armature=True, export_attributes=False, export_extras=False, export_morph=False,
        export_cameras=False, export_lights=False)
    info["size_bytes"] = os.path.getsize(path)
    info["fast_preview_build"] = bool(fast)
    with open(os.path.join(MANI, cid + ".json"), "w", encoding="utf8") as f:
        json.dump(info, f, indent=1)
    log("EXPORTED %s  tris=%d (body %d)  mats=%d  size=%.2f MB  time=%.0fs" % (
        path, info["triangles"], info.get("triangles_body", 0), len(info["materials"]), info["size_bytes"] / 1e6,
        time.time() - t0))
    for m in info["meshes"]:
        log("   mesh %-12s tris=%-6d mats=%s" % (m["name"], m["triangles"], m["materials"]))
    if info["triangles"] > max_tris:
        log("WARNING: triangle budget exceeded", info["triangles"], ">", max_tris)
    return info
