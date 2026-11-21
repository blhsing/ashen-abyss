"""gearbase -- bake + export of a barely-clad base body (hero_<class>_base.glb) split into body-region meshes."""
from gearlib import *
from gearfin import *


def finalize_base(cls, c, look, hair_tint, hair_gain=1.0, scar_pts=None, notes=""):
    """Bake + export Y:/game/assets/chars/hero_<cls>_base.glb.
    mk.PARTS = the base garments (baked classes); each part has o["grp"] = name of the final mesh it belongs to
    (wrap_loin / wrap_chest / hair: 'hair' parts ride with the scalp hair mesh, e.g. a bound topknot).
    c.hairs entries may carry o["hgrp"] = 'hair' | 'beard' (default from the mesh name)."""
    t0 = time.time()
    cid = "hero_%s_base" % cls
    arm = c.arm
    fin._setup_cycles()
    lm = face_landmarks(c)
    fr, vr = face_regions(c.B, c.skin)
    if c.ref is not None:
        bpy.data.objects.remove(c.ref, do_unlink=True)
        c.ref = None
    finals = []
    info = dict(id=cid, file="res://assets/chars/%s.glb" % cid, cls=cls, kind=c.kind, meshes={}, textures={}, notes=notes)
    # ---- garments (one atlas)
    parts = [o for o in mk.PARTS if o["cls"] in fin.BAKED]
    extra_hair = None
    if parts:
        gnames = sorted({o["grp"] for o in parts})
        for o in parts:
            mk.apply_mods(o)
            face_attr_set(o, "grp", gnames.index(o["grp"]))
        J, at, fm = bake_parts(cid, parts, "cloth", size_cap=1024)
        info["textures"]["cloth"] = at.size
        res = split_faces(J, face_attr_get(J, "grp"), {i: n for i, n in enumerate(gnames)}, arm)
        for n, o in res.items():
            if n == "hair":
                extra_hair = o
            else:
                finals.append(o)
    # ---- skin
    face_attr_set(c.skin, "region", fr)
    objs = [c.skin] + ([c.eyes] if c.eyes is not None else [])
    if c.eyes is not None:
        face_attr_set(c.eyes, "region", SKIN_REGIONS.index("head"))
    for i, o in enumerate(objs):
        fin._name_src_uv(o)
        o.data.materials.clear()
        o.data.materials.append(fin._tag_material("__tag_eye" if i > 0 else "__tag_skin"))
        clear_custom_normals(o)
        fin._set_int_attr(o, "own", 0)
    J = fin.join(objs, "J_skin")
    fin._triangulate(J)
    at = bake_skin_regions(cid, J, c.kind, lm, 1024 if fin.PREVIEW else 2048, look, scar_pts)
    at.save(fin.QUAL[1])
    fm = at.material("skin_" + cid)
    info["textures"]["skin"] = at.size
    J.data.materials.clear()
    J.data.materials.append(fm)
    res = split_faces(J, face_attr_get(J, "region"), {i: "body_" + n for i, n in enumerate(SKIN_REGIONS)}, arm)
    finals += list(res.values())
    # ---- hair / beard / brows
    hobjs = list(c.hairs) + ([c.brows] if c.brows is not None else [])
    if c.brows is not None:
        c.brows["hair_tex"] = 1 if c.kind == "male" else 2
        c.brows["hgrp"] = "brows"
    if hobjs:
        hn = ["hair", "beard", "brows"]
        area = 0.0
        for o in hobjs:
            which = int(o["hair_tex"]) if "hair_tex" in o.keys() else 1
            g = o["hgrp"] if "hgrp" in o.keys() else ("beard" if "beard" in o.name.lower() else "hair")
            fin._name_src_uv(o)
            o.data.materials.clear()
            o.data.materials.append(fin._tag_material("__tag_hair%d" % which))
            clear_custom_normals(o)
            fin._set_int_attr(o, "own", 0)
            face_attr_set(o, "hgrp", hn.index(g))
            area += fin._area(o)
        J = fin.join(hobjs, "J_hair")
        fin._triangulate(J)
        size, need = fin._auto_size(area, fin.SKIN_TEXEL, 1024)
        size = max(size, 512)
        if fin.PREVIEW:
            size = 512
        at = fin.bake_hair(cid, J, size, dict(tint=hair_tint, gain=hair_gain), (0.0, lm["ey"] + 0.095))
        at.save(fin.QUAL[1])
        fm = at.material("hair_" + cid, double_sided=True)
        info["textures"]["hair"] = at.size
        J.data.materials.clear()
        J.data.materials.append(fm)
        res = split_faces(J, face_attr_get(J, "hgrp"), {i: n for i, n in enumerate(hn)}, arm)
        if extra_hair is not None and "hair" in res:
            store_corner_normals(res["hair"])
            store_corner_normals(extra_hair)
            res["hair"] = fin.join([res["hair"], extra_hair], "hair")
            restore_corner_normals(res["hair"])
            extra_hair = None
        finals += list(res.values())
    if extra_hair is not None:
        extra_hair.name = "hair"
        finals.append(extra_hair)
    # ---- export
    tris = 0
    for o in finals:
        tok = o.name[5:] if o.name.startswith("body_") else o.name
        strip_obj(o, {"region": tok})
        rebind(o, arm)
        o.data.name = o.name
        tc = mk.tri_count(o)
        tris += tc
        co = get_co(o)
        info["meshes"][o.name] = dict(region=tok, triangles=tc, materials=[m.name for m in o.data.materials],
                                      bbox=[[round(float(v), 3) for v in co.min(0)], [round(float(v), 3) for v in co.max(0)]])
    for o in list(bpy.data.objects):
        if o is not arm and o not in finals:
            o.hide_render = True
    path = os.path.join(OUT_CHARS, cid + ".glb")
    export_glb(path, finals, arm)
    glb_set_extras(path, {"body_regions": ",".join(sorted(info["meshes"].keys())), "class": cls})
    info["triangles"] = tris
    info["size_bytes"] = os.path.getsize(path)
    info["landmarks"] = {k: round(float(v), 4) for k, v in lm.items()}
    with open(os.path.join(WORK, "base_%s.json" % cls), "w", encoding="utf8") as f:
        json.dump(info, f, indent=1)
    log("EXPORTED %s  tris=%d  size=%.2f MB  tex=%s  time=%.0fs" % (path, tris, info["size_bytes"] / 1e6, info["textures"], time.time() - t0))
    for n, m in sorted(info["meshes"].items()):
        log("   mesh %-16s tris=%-6d mats=%s" % (n, m["triangles"], m["materials"]))
    return info
