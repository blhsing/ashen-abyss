"""gearfin -- baking / splitting / export of the base bodies and the armour pieces (uses heroes/finalize.py helpers)."""
import struct
from gearlib import *

TEXEL_GEAR = 0.00135          # metres per texel of the armour atlases (heroes: 0.00125)
SIZE_CAP = {"chest": 2048, "pants": 1024, "boots": 1024, "gloves": 1024, "helm": 1024}


# ---------------------------------------------------------------------------------------------- attribute helpers
def face_attr_set(obj, name, values):
    me = obj.data
    a = me.attributes.get(name)
    if a is not None and (a.domain != 'FACE' or a.data_type != 'INT'):
        me.attributes.remove(a)
        a = None
    if a is None:
        a = me.attributes.new(name, 'INT', 'FACE')
    v = np.broadcast_to(np.asarray(values, dtype=np.int32), (len(me.polygons),)).copy()
    a.data.foreach_set('value', v)


def face_attr_get(obj, name, default=0):
    me = obj.data
    a = me.attributes.get(name)
    v = np.full(len(me.polygons), default, dtype=np.int32)
    if a is not None and a.domain == 'FACE':
        a.data.foreach_get('value', v)
    return v


def corner_normals(obj):
    me = obj.data
    cn = np.empty(len(me.loops) * 3, dtype=np.float32)
    me.corner_normals.foreach_get('vector', cn)
    return cn.reshape(-1, 3)


def store_corner_normals(obj, name="ln"):
    """keep the shading normals of every face corner in a corner attribute (survives face deletion / splitting)"""
    me = obj.data
    cn = corner_normals(obj)
    a = me.attributes.get(name)
    if a is not None:
        me.attributes.remove(a)
    a = me.attributes.new(name, 'FLOAT_VECTOR', 'CORNER')
    a.data.foreach_set('vector', cn.ravel())


def restore_corner_normals(obj, name="ln"):
    me = obj.data
    a = me.attributes.get(name)
    if a is None or len(me.loops) == 0:
        return
    cn = np.empty(len(me.loops) * 3, dtype=np.float32)
    a.data.foreach_get('vector', cn)
    cn = cn.reshape(-1, 3)
    ln = np.linalg.norm(cn, axis=1, keepdims=True)
    cn = cn / np.maximum(ln, 1e-9)
    for p in me.polygons:
        p.use_smooth = True
    me.normals_split_custom_set([tuple(map(float, v)) for v in cn])
    me.attributes.remove(me.attributes.get(name))
    me.update()


def split_faces(J, groups, names, arm, remove=True):
    """Split a mesh object into one object per face-group value. groups: int per polygon, names: {value: object name}
    (values not in names are dropped). Shading normals, UVs and weights are kept exactly, so the pieces line up without
    a visible seam when they are shown together. Returns {name: obj}."""
    me = J.data
    store_corner_normals(J)
    face_attr_set(J, "__grp", groups)
    out = {}
    for k in sorted(set(int(v) for v in groups)):
        if k not in names:
            continue
        me2 = me.copy()
        o2 = bpy.data.objects.new(names[k], me2)
        me2.name = names[k]
        bpy.context.scene.collection.objects.link(o2)
        rebind(o2, arm)
        have = {g.name for g in o2.vertex_groups}
        for g in J.vertex_groups:
            if g.name not in have:
                o2.vertex_groups.new(name=g.name)
        assert [g.name for g in o2.vertex_groups] == [g.name for g in J.vertex_groups]
        bm = bmesh.new()
        bm.from_mesh(me2)
        lay = bm.faces.layers.int.get("__grp")
        dead = [f for f in bm.faces if f[lay] != k]
        bmesh.ops.delete(bm, geom=dead, context='FACES')
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
        bm.to_mesh(me2)
        bm.free()
        me2.update()
        restore_corner_normals(o2)
        a = me2.attributes.get("__grp")
        if a is not None:
            me2.attributes.remove(a)
        out[names[k]] = o2
    if remove:
        bpy.data.objects.remove(J, do_unlink=True)
    return out


def strip_obj(obj, keep_props=None):
    fin._strip_layers(obj)
    me = obj.data
    for nm in ("region", "hgrp", "grp", "__grp", "ln", "ovid", "slotf"):
        a = me.attributes.get(nm)
        if a is not None:
            me.attributes.remove(a)
    fin._prune_groups(obj)
    for k, v in (keep_props or {}).items():
        obj[k] = v


def export_glb(path, finals, arm):
    sc = bpy.context.scene
    for k in list(sc.keys()):          # scene extras of imported files must not leak into this one
        del sc[k]
    fin._select_only(finals + [arm], arm)
    bpy.ops.export_scene.gltf(
        filepath=path, export_format='GLB', use_selection=True,
        export_animations=False, export_skins=True, export_yup=True, export_apply=False,
        export_materials='EXPORT', export_image_format='AUTO', export_jpeg_quality=92, export_image_quality=92,
        export_tangents=True, export_normals=True, export_texcoords=True, export_vertex_color='NONE',
        export_all_influences=False, export_influence_nb=4, export_def_bones=False,
        export_rest_position_armature=True, export_attributes=False, export_extras=True, export_morph=False,
        export_cameras=False, export_lights=False)
    return os.path.getsize(path)


# ---------------------------------------------------------------------------------------------- glb post-processing
def glb_read(path):
    with open(path, "rb") as f:
        data = f.read()
    magic, ver, length = struct.unpack_from("<4sII", data, 0)
    assert magic == b"glTF", path
    off = 12
    js, binc = None, b""
    while off < length:
        clen, ctype = struct.unpack_from("<I4s", data, off)
        body = data[off + 8:off + 8 + clen]
        if ctype == b"JSON":
            js = json.loads(body.decode("utf8"))
        elif ctype == b"BIN\x00":
            binc = body
        off += 8 + clen
    return js, binc


def glb_write(path, js, binc):
    jb = json.dumps(js, separators=(",", ":")).encode("utf8")
    jb += b" " * ((4 - len(jb) % 4) % 4)
    binc = binc + b"\x00" * ((4 - len(binc) % 4) % 4)
    total = 12 + 8 + len(jb) + (8 + len(binc) if binc else 0)
    with open(path, "wb") as f:
        f.write(struct.pack("<4sII", b"glTF", 2, total))
        f.write(struct.pack("<I4s", len(jb), b"JSON"))
        f.write(jb)
        if binc:
            f.write(struct.pack("<I4s", len(binc), b"BIN\x00"))
            f.write(binc)
    return total


def glb_externalize(path, uri_for):
    """Replace embedded images by external references. uri_for(image name) -> relative uri or None (keep embedded).
    The image bytes are removed from the binary chunk. Returns the list of (name, uri) replaced."""
    js, binc = glb_read(path)
    imgs = js.get("images", [])
    drop = {}
    done = []
    for im in imgs:
        uri = uri_for(im.get("name", ""), im.get("mimeType", ""))
        if uri and "bufferView" in im:
            drop[im["bufferView"]] = True
            del im["bufferView"]
            im.pop("mimeType", None)
            im["uri"] = uri
            done.append((im.get("name"), uri))
    if not drop:
        return done
    bvs = js["bufferViews"]
    keep = [i for i in range(len(bvs)) if i not in drop]
    remap = {old: new for new, old in enumerate(keep)}
    out = bytearray()
    nb = []
    for i in keep:
        bv = dict(bvs[i])
        s = bv.get("byteOffset", 0)
        chunk = binc[s:s + bv["byteLength"]]
        while len(out) % 4:
            out.append(0)
        bv["byteOffset"] = len(out)
        out += chunk
        nb.append(bv)
    js["bufferViews"] = nb
    for a in js.get("accessors", []):
        if "bufferView" in a:
            a["bufferView"] = remap[a["bufferView"]]
        sp = a.get("sparse")
        if sp:
            sp["indices"]["bufferView"] = remap[sp["indices"]["bufferView"]]
            sp["values"]["bufferView"] = remap[sp["values"]["bufferView"]]
    for im in imgs:
        if "bufferView" in im:
            im["bufferView"] = remap[im["bufferView"]]
    js["buffers"][0]["byteLength"] = len(out) + ((4 - len(out) % 4) % 4)
    glb_write(path, js, bytes(out))
    return done


def glb_set_extras(path, extras):
    """scene-level extras (asset info for the game: hide_body_regions, slot, ...)"""
    js, binc = glb_read(path)
    for sc in js.get("scenes", []):
        sc.setdefault("extras", {}).update(extras)
    glb_write(path, js, binc)


# ---------------------------------------------------------------------------------------------- skin bake (regions)
def _inpaint(img, mask):
    """fill the masked pixels of (H, W, C) from their surroundings (multi-scale normalised blur), feathered"""
    out = img.astype(np.float32).copy()
    valid = (~mask).astype(np.float32)
    for r in (2, 4, 8, 16, 32, 64, 128, 256):
        todo = valid < 0.5
        if not todo.any():
            break
        num = mats.blur(out * valid[..., None], r)
        den = mats.blur(valid, r)
        new = todo & (den > 0.04)
        out[new] = num[new] / den[new][:, None]
        valid[new] = 1.0
    soft = np.clip(mats.blur(mask.astype(np.float32), 5) * 1.6, 0, 1)[..., None]
    return out * (1 - soft) + mats.blur(out, 7) * soft


def bare_skin_files(kind):
    """(colour, normal, roughness) paths of the painted Quaternius skin with the painted-on underwear (dark grey
    trunks / bikini) replaced by skin: the base bodies wear modelled linen wraps instead. Cached in _work."""
    base = os.path.join(SRC, "base")
    cpath, npath, rpath = (os.path.join(base, f) for f in fin.SKIN_FILES[kind])
    oc = os.path.join(WORK, "skin_%s_bare2_c.png" % kind)
    orr = os.path.join(WORK, "skin_%s_bare2_r.png" % kind)
    if not (os.path.isfile(oc) and os.path.isfile(orr)):
        col = mats.read_img(cpath)[..., :3]
        h, w = col.shape[:2]
        mx, mn = col.max(-1), col.min(-1)
        sat = (mx - mn) / np.maximum(mx, 1e-4)
        m = (sat < 0.16) & (mx < 0.42)
        yy, xx = np.mgrid[0:h, 0:w]
        m &= ~((xx < 0.37 * w) & (yy > 0.64 * h))          # the face (eye lids are grey too); image rows run bottom-up
        m = mats.blur(m.astype(np.float32), 16) > 0.004          # wide: the painted contact shadow round the cloth goes too
        log("bare skin %s: %.1f %% of the texture was painted underwear" % (kind, 100.0 * m.mean()))
        mats.save_rgb(_inpaint(col, m), oc)
        rgh = mats.read_img(rpath, (w, h))[..., :3]
        mats.save_rgb(_inpaint(rgh, m), orr)
    return oc, npath, orr


def bake_skin_regions(cid, J, kind, lm, size, look, scar_pts=None):
    """fin.bake_skin with one change: the atlas islands are cut along the body-region borders (face attribute
    'region'), so each region mesh gets the same tangent frames alone as it had in the joined bake -> no seam."""
    base = os.path.join(SRC, "base")
    files = bare_skin_files(kind)
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
    nscar = fin._heat_attr(J, scar_pts)
    fin._atlas_layer(J)
    uv, tri, ar, mi = fin._uv_arrays(J)
    reg = face_attr_get(J, "region")
    for k in range(3):
        uv[tri[:, k], 0] += 3.0 * reg
    me.uv_layers.active.data.foreach_set('uv', uv.ravel())
    if eye_slot is not None:
        uv, tri, ar, mi = fin._uv_arrays(J)
        ua = fin._uv_area(uv, tri)
        co = get_co(J)
        vi = np.empty(len(me.loops), dtype=np.int32)
        me.loops.foreach_get('vertex_index', vi)
        zc = co[vi[tri]][:, :, 2].mean(1)
        face = (mi != eye_slot) & (zc > lm['cz'])
        eyes = mi == eye_slot
        if face.any() and eyes.any() and ua[eyes].sum() > 0:
            dens_f = ua[face].sum() / max(ar[face].sum(), 1e-9)
            dens_e = ua[eyes].sum() / max(ar[eyes].sum(), 1e-9)
            fin._scale_islands(J, eye_slot, math.sqrt(dens_f * 2.6 / dens_e))
    # the face is what the player looks at in the portrait: give the head islands more texels than the limbs
    hk = look.get("head_texels", 1.0)
    if hk != 1.0:
        uv, tri, ar, mi = fin._uv_arrays(J)
        sel = np.unique(tri[(reg == SKIN_REGIONS.index("head")) & (mi != (eye_slot if eye_slot is not None else -1))].ravel())
        c0 = uv[sel].mean(0)
        uv[sel] = (uv[sel] - c0) * hk + c0
        me.uv_layers.active.data.foreach_set('uv', uv.ravel())
    tu = time.time()
    fin._repack(J)
    log("repack skin: %.0fs" % (time.time() - tu))
    P = fin._bake_set(J, size, "skin", edge=False)
    col = P['color']
    sat, tint, gain = look.get("sat", 1.0), look.get("tint", (1, 1, 1)), look.get("gain", 1.0)
    lum = (col * np.array((0.2126, 0.7152, 0.0722), dtype=np.float32)).sum(-1, keepdims=True)
    col = lum + (col - lum) * sat
    P['color'] = np.clip(col * np.array(tint, dtype=np.float32) * gain, 0, 1).astype(np.float32)
    log("skin: %d welt points on the skin, atlas %d" % (nscar, size))
    return fin.Atlas(cid, "skin", P, 1.0)


# ---------------------------------------------------------------------------------------------- generic class bake
def bake_parts(cid, objs, name, size_cap=2048, texel=TEXEL_GEAR, double_sided=True, decimate=None, isolate=None,
               fur_budget=0, size_min=256):
    """Join baked-class parts (cloth / leather / metal / bone, any mix) into ONE mesh with ONE atlas and bake it.
    isolate: objects that must not shade the bake (hidden from rendering meanwhile). Returns (object, atlas, material)."""
    for o in objs:
        fin._prep_part(o)
        fin._hem_attr(o)
        co_ = get_co(o)
        dim = float(np.linalg.norm(co_.max(0) - co_.min(0))) if len(co_) else 0.0
        fin._set_face_int(o, "small", 1 if (fin._area(o) < 0.012 or dim < 0.09) else 0)
        fin._set_int_attr(o, "own", 0)
        k = o["src"]
        assert k in mats.SRCM, "no source material '%s' for part %s" % (k, o.name)
        o.data.materials.clear()
        o.data.materials.append(mats.SRCM[k])
    area = sum(fin._area(o) for o in objs)
    J = fin.join(objs, "J_" + name)
    fin._triangulate(J)
    if decimate and decimate < 0.999:
        fin._decimate(J, decimate)
    hidden = []
    for o in (isolate or []):
        if o is not J and not o.hide_render:
            o.hide_render = True
            hidden.append(o)
    size, need = fin._auto_size(area, texel, size_cap)
    size = max(size, size_min)
    if fin.PREVIEW:
        size = min(size, 512)
    fw = fin._exposure(J)
    made = 0
    if fur_budget > 0:
        for slot, m_ in enumerate(list(J.data.materials)):
            fur = mats.SRCINFO.get(m_.name[4:] if m_ is not None else "", {}).get("fur")
            if fur:
                made += fin._fur_fins(J, slot, fur_budget - made, **fur)
        if made:
            fw = np.concatenate([fw, np.ones(made, dtype=np.float32)])
    fin._uv_unwrap(J, face_w=fw)
    P = fin._bake_set(J, size, name)
    at = fin.Atlas(cid, name, P, need)
    at.save(fin.QUAL[1])
    fm = at.material("%s_%s" % (name, cid), double_sided=double_sided)
    J.data.materials.clear()
    J.data.materials.append(fm)
    for o in hidden:
        o.hide_render = False
    log("   %s: area %.2f m2 -> atlas %d (need %.0f), %d tris%s" % (name, area, size, need, mk.tri_count(J),
                                                                 ", %d fur locks" % made if made else ""))
    return J, at, fm


def emis_parts(cid, objs, name="emis"):
    """join emissive parts (not baked): heat-gradient texture + emissive material. Returns object or None"""
    if not objs:
        return None
    info = mats.SRCINFO[objs[0]["src"]]
    for o in objs:
        fin._prep_part(o)
        o.data.materials.clear()
    J = fin.join(objs, "J_" + name)
    fin._triangulate(J)
    gpath = fin._emis_heat(J, cid, info["emis"])
    fm = mats.final_material("emis_" + cid, color=info["color"], rough=0.4, metal=0.0, emis=info["emis"],
                             strength=info.get("strength", 6.0), double_sided=True, emis_path=gpath)
    J.data.materials.clear()
    J.data.materials.append(fm)
    if not len(J.data.uv_layers):
        J.data.uv_layers.new(name="UVMap")
    return J
