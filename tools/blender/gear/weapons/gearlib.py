"""Gear (weapon / off-hand / jewel) build pipeline for 燼淵 Ashen Abyss  -- ART-W.

Reuses the hero tooling read-only (Y:\\tools\\blender\\heroes: mk generators, kit shapes, mats source materials, the
finalize bake helpers), but exports RIGID meshes without a skeleton:

  * one Blender run builds every variant of one base ("sword1": v1, v2, v3) from parts tagged with a source material;
  * all baked parts of the base are joined, unwrapped and baked into ONE atlas shared by its variants
    (<prefix>_<base>_c.jpg / _n.jpg / _orm.jpg next to the .glb files; the .glb files reference them by relative URI, so
    Godot loads one texture set for the three models instead of three embedded copies);
  * emissive parts are not baked: UV.u = heat along the piece, UV.v = the row of the accent colour in the shared ramp
    texture gear_emis.png (one 64 x 128 px file for every gear model);
  * each variant is exported as <prefix>_<base>_v<k>.glb WITHOUT the Y-up conversion, so the axes in the file (and in
    Godot) are literally the contract's: +Z up the weapon, +X towards the edge / knuckles, +Y the palm normal; origin
    at the grip centre; metres.
"""
import sys, os, math, json, time, struct
HEROES = r"Y:\tools\blender\heroes"
HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (HEROES, HERE):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
import rigio
rigio._LOG = os.path.join(HERE, "_build.log")
from kit_twn import *          # mk generators, kit helper shapes, rigio helpers (log, stage, camera ...)
import mk, mats
import finalize as fin

GEAR_OUT = r"Y:\game\assets\gear\weapons"
GEAR_SHOTS = r"Y:\shots\gear\weapons"
MANI = os.path.join(HERE, "_manifest")
RES = "res://assets/gear/weapons/"
EMIS_FILE = "gear_emis.png"
for _d in (GEAR_OUT, GEAR_SHOTS, MANI):
    os.makedirs(_d, exist_ok=True)

EXPLODE = 6.0        # variants are moved apart for the bake (no occlusion between them)
LIFT = 2.5           # ... and lifted, so the hero materials' ground-dust band (z < ~0.5 m) stays off the weapons

PARTS = []
MATCLS = {}          # source key -> class (metal / leather / wood / bone / cloth / flat / emis)

# ------------------------------------------------------------------------------------------------ emissive accents
# key -> (colour, strength). The row order is the layout of gear_emis.png: never reorder, only append.
EMIS = [
    ("ember", (1.0, 0.32, 0.04), 5.0),
    ("flame", (1.0, 0.66, 0.18), 4.5),
    ("star", (0.10, 0.30, 1.0), 4.5),
    ("frost", (0.42, 0.82, 1.0), 4.0),
    ("storm", (0.70, 0.62, 1.0), 4.0),
    ("soul", (0.10, 1.0, 0.24), 3.4),
    ("moss", (0.38, 1.0, 0.20), 3.2),
    ("pact", (1.0, 0.02, 0.05), 3.6),
    ("void", (0.52, 0.14, 1.0), 4.0),
    ("teal", (0.0, 0.82, 0.50), 3.6),
    ("holy", (1.0, 0.84, 0.46), 4.0),
    ("venom", (0.55, 0.92, 0.16), 3.4),
    ("ruby", (0.95, 0.10, 0.08), 1.6),
    ("sapphire", (0.12, 0.34, 1.0), 1.6),
    ("topaz", (1.0, 0.70, 0.10), 1.6),
    ("emerald", (0.10, 0.90, 0.22), 1.6),
    ("amethyst", (0.58, 0.20, 0.95), 1.6),
    ("bonefire", (0.80, 0.92, 0.70), 2.2),
]
EMIS_ROWS = 32
EMIS_IDX = {k: i for i, (k, c, s) in enumerate(EMIS)}


def emis_ramp(color):
    """64 texel heat ramp of one accent (dim deep colour -> the accent -> a whiter, hotter core), linear RGB"""
    c = np.array(color[:3], dtype=np.float32)
    lo = c * np.array((0.42, 0.20, 0.20), dtype=np.float32) * 0.6 if c[0] > c[2] else c * 0.22
    hot = np.clip(c + (1.0 - c) * 0.38, 0, 1)
    t = np.linspace(0, 1, 64, dtype=np.float32)[:, None]
    return np.where(t < 0.62, lo + (c - lo) * (t / 0.62) ** 1.3, c + (hot - c) * (np.clip(t - 0.62, 0, 1) / 0.38) ** 1.5)


def write_emis_texture():
    h = EMIS_ROWS * 4
    img = np.zeros((h, 64, 3), dtype=np.float32)
    for i, (k, c, s) in enumerate(EMIS):
        img[i * 4:(i + 1) * 4] = emis_ramp(c)[None, :, :]
    p = os.path.join(GEAR_OUT, EMIS_FILE)
    mats.save_rgb(mats.lin2srgb(img), p)
    write_import(p, kind="emis")
    return p


def emis_v(key):
    """UV.v of an accent row (image row 0 = bottom of the picture as saved from a Blender pixel buffer)"""
    return (EMIS_IDX[key] + 0.5) / EMIS_ROWS


# ------------------------------------------------------------------------------------------------ parts
def begin():
    reset_scene()
    PARTS.clear()
    MATCLS.clear()
    mats.SRCM.clear()
    mats.SRCINFO.clear()
    mk._WCACHE.clear()
    mats._IMG.clear()          # scan images of the previous scene are gone after reset_scene()


def reg(cls, key):
    MATCLS[key] = cls
    return key


def gadd(owner, g, src, name=None, sharp=40, bevel=0.0, bevel_seg=1, subsurf=0, solidify=0.0, solid_offset=1.0,
         recalc=True, smooth=True, weld=0.0, hem=None, heat=None):
    """Create a part of variant `owner` from generator output g (weapon-local space). src = palette key.
    sharp = shading split angle; hem = the angle above which an edge counts as a 'sharp edge' for the materials' hem
    effects (honed cutting edges, rolled rims, stitches); default = sharp. Blades use hem=60: bevel shoulders stay
    crisp (sharp=14) but only the real cutting edge is honed bright."""
    assert src in MATCLS, "unknown material key '%s'" % src
    name = name or "%s_p%03d" % (owner, len(PARTS))
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(map(float, v)) for v in g["v"]], [], g["f"])
    me.update()
    if g.get("luv") is not None:
        a = me.attributes.new("luv", 'FLOAT2', 'POINT')
        a.data.foreach_set('vector', np.asarray(g["luv"], dtype=np.float32).ravel())
    if recalc or weld:
        bm = bmesh.new()
        bm.from_mesh(me)
        if weld:
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=weld)
        if recalc:
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    obj["cls"] = MATCLS[src]
    obj["src"] = src
    obj["owner"] = owner
    obj["sharp"] = float(sharp)
    if hem is not None:
        obj["hem_sharp"] = float(hem)
    if heat is not None:
        obj["heat"] = float(heat)          # emissive parts: fixed position on the heat ramp (0 dim and deep .. 1 white hot)
    if subsurf:
        m = obj.modifiers.new("sub", 'SUBSURF')
        m.levels = subsurf
        m.render_levels = subsurf
    if solidify:
        m = obj.modifiers.new("solid", 'SOLIDIFY')
        m.thickness = solidify
        m.offset = solid_offset
        m.use_even_offset = True
        m.use_quality_normals = True
    if bevel:
        m = obj.modifiers.new("bevel", 'BEVEL')
        m.width = bevel
        m.segments = bevel_seg
        m.limit_method = 'ANGLE'
        m.angle_limit = math.radians(30)
        m.harden_normals = False
    PARTS.append(obj)
    return obj


class W:
    """Builder of one variant: w(geo, "steel", sharp=30) adds a part; w.set(mount=..., grip2=...) sets manifest data."""

    def __init__(self, base, v):
        self.base, self.v = base, v
        self.owner = "v%d" % v
        self.info = dict(mount="hand_r", grip="r", hands=1, grip2=None)

    def __call__(self, g, src, M=None, **kw):
        if isinstance(g, (list, tuple)):
            g = gjoin(*g)
        if M is not None:
            g = gxf(g, M)
        return gadd(self.owner, g, src, **kw)

    def set(self, **kw):
        self.info.update(kw)
        return self


# ------------------------------------------------------------------------------------------------ finalize
def _prep(o):
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


def _emis_uv(o, key):
    """UV.u = heat along each connected piece (hot middle, cooler ends; small pieces fully hot), UV.v = accent row"""
    me = o.data
    while len(me.uv_layers):
        me.uv_layers.remove(me.uv_layers[0])
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
    co = get_co(o)
    heat = np.full(n, 0.72, dtype=np.float32)
    for c in range(k):
        idx = np.nonzero(comp == c)[0]
        p = co[idx]
        if len(idx) < 4:
            continue
        cen = p.mean(0)
        w, v = np.linalg.eigh(np.cov((p - cen).T))
        t = (p - cen) @ v[:, -1]
        ext = t.max() - t.min()
        if ext < 0.05:
            continue
        tn = np.abs(t - (t.max() + t.min()) / 2) / (ext / 2)
        heat[idx] = 0.88 - 0.62 * tn ** 1.6
    heat = np.clip(heat + 0.06 * np.sin(co[:, 0] * 173.0 + co[:, 1] * 131.0 + co[:, 2] * 211.0), 0.04, 0.96)
    if "heat" in o.keys():
        heat[:] = float(o["heat"])
    vi = np.empty(len(me.loops), dtype=np.int32)
    me.loops.foreach_get('vertex_index', vi)
    uv = np.empty((len(me.loops), 2), dtype=np.float32)
    uv[:, 0] = heat[vi]
    uv[:, 1] = emis_v(key)
    me.uv_layers[0].data.foreach_set('uv', uv.ravel())


def _plain_material(name, color=(0.5, 0.5, 0.5), rough=0.6, metal=0.0, emis=None, strength=0.0):
    m = bpy.data.materials.get(name)
    if m is not None:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bs = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value = tuple(color[:3]) + (1.0,)
    bs.inputs['Roughness'].default_value = rough
    bs.inputs['Metallic'].default_value = metal
    if emis is not None:
        bs.inputs['Emission Color'].default_value = tuple(emis[:3]) + (1.0,)
        bs.inputs['Emission Strength'].default_value = strength
    m.use_backface_culling = True
    return m


def _split_by_own(J, n_own):
    own = fin._get_int_attr(J, "own")
    out = {}
    for k in sorted(set(int(v) for v in own)):
        me = J.data.copy()
        o2 = bpy.data.objects.new("split_%d" % k, me)
        bpy.context.scene.collection.objects.link(o2)
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.verts.ensure_lookup_table()
        dead = [v for v in bm.verts if own[v.index] != k]
        bmesh.ops.delete(bm, geom=dead, context='VERTS')
        bm.to_mesh(me)
        bm.free()
        out[k] = o2
    bpy.data.objects.remove(J, do_unlink=True)
    return out


def _strip(obj):
    me = obj.data
    for nm in ("own", "luv", "hem", "small", "sharp_face"):
        a = me.attributes.get(nm)
        if a is not None:
            me.attributes.remove(a)
    for a in list(me.color_attributes):
        me.color_attributes.remove(a)
    while len(me.uv_layers) > 1:
        me.uv_layers.remove(me.uv_layers[len(me.uv_layers) - 1])
    for k in list(obj.keys()):
        del obj[k]


def _export_glb(path, obj):
    fin._select_only([obj], obj)
    bpy.ops.export_scene.gltf(
        filepath=path, export_format='GLB', use_selection=True,
        export_animations=False, export_skins=False, export_yup=False, export_apply=False,
        export_materials='EXPORT', export_image_format='NONE',
        export_tangents=True, export_normals=True, export_texcoords=True, export_vertex_color='NONE',
        export_attributes=False, export_extras=False, export_morph=False,
        export_cameras=False, export_lights=False)


def patch_glb(path, mat_tex, extras):
    """Rewrite the JSON chunk of an exported .glb: external (shared) texture files per material + node extras.
    mat_tex: {material name: dict(c=, n=, orm=, e=, strength=)} (file names relative to the .glb)."""
    with open(path, 'rb') as f:
        data = f.read()
    magic, ver, total = struct.unpack_from('<4sII', data, 0)
    assert magic == b'glTF'
    off = 12
    chunks = []
    while off < total:
        ln, typ = struct.unpack_from('<I4s', data, off)
        chunks.append([typ, data[off + 8:off + 8 + ln]])
        off += 8 + ln
    js = json.loads(chunks[0][1].decode('utf8'))
    images, textures = [], []

    def tex(uri):
        if uri not in images:
            images.append(uri)
            textures.append({"sampler": 0, "source": len(images) - 1})
        return {"index": images.index(uri)}
    used_ext = set(js.get("extensionsUsed", []))
    for m in js.get("materials", []):
        t = mat_tex.get(m["name"])
        if not t:
            continue
        pbr = m.setdefault("pbrMetallicRoughness", {})
        if t.get("c"):
            pbr["baseColorTexture"] = tex(t["c"])
            pbr["baseColorFactor"] = [1.0, 1.0, 1.0, 1.0]
        if t.get("orm"):
            pbr["metallicRoughnessTexture"] = tex(t["orm"])
            pbr["metallicFactor"] = 1.0
            pbr["roughnessFactor"] = 1.0
            m["occlusionTexture"] = tex(t["orm"])
        if t.get("n"):
            m["normalTexture"] = tex(t["n"])
        if t.get("e"):
            m["emissiveTexture"] = tex(t["e"])
            m["emissiveFactor"] = [1.0, 1.0, 1.0]
            m.setdefault("extensions", {})["KHR_materials_emissive_strength"] = {"emissiveStrength": float(t.get("strength", 1.0))}
            used_ext.add("KHR_materials_emissive_strength")
        m["doubleSided"] = False
    if images:
        js["samplers"] = [{"magFilter": 9729, "minFilter": 9987, "wrapS": 10497, "wrapT": 10497}]
        js["images"] = [{"uri": u} for u in images]
        js["textures"] = textures
    if used_ext:
        js["extensionsUsed"] = sorted(used_ext)
    for nd in js.get("nodes", []):
        if "mesh" in nd:
            nd["extras"] = extras
    js.setdefault("asset", {})["generator"] = "Ashen Abyss gear pipeline (Blender glTF exporter + gearlib.patch_glb)"
    jb = json.dumps(js, separators=(',', ':')).encode('utf8')
    jb += b' ' * ((4 - len(jb) % 4) % 4)
    chunks[0][1] = jb
    body = b''.join(struct.pack('<I4s', len(c), t) + c for t, c in chunks)
    with open(path, 'wb') as f:
        f.write(struct.pack('<4sII', b'glTF', 2, 12 + len(body)) + body)
    return os.path.getsize(path)


_IMPORT_TMPL = """[remap]

importer="texture"
type="CompressedTexture2D"

[deps]

source_file="%(res)s"

[params]

compress/mode=%(mode)d
compress/high_quality=%(hq)s
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=%(nm)d
compress/channel_pack=0
mipmaps/generate=%(mips)s
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=false
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=0
"""


def write_import(path, kind):
    """Godot import settings of a gear texture, written only when the file has none yet (Godot keeps the params and
    adds uid / cache paths on its first import). VRAM-compressed like the environment sets: colour BPTC, ORM S3TC,
    normal RGTC; the tiny emissive ramp stays lossless and without mipmaps (its rows must not bleed)."""
    ip = path + ".import"
    if os.path.isfile(ip):
        return
    d = dict(res=RES + os.path.basename(path), mode=2, hq="false", nm=0, mips="true")
    if kind == "c":
        d["hq"] = "true"
    elif kind == "n":
        d["nm"] = 1
    elif kind == "emis":
        d.update(mode=0, mips="false")
    with open(ip, "w", encoding="utf8", newline="\n") as f:
        f.write(_IMPORT_TMPL % d)


def finish(base, builders, prefix="wpn", texel=0.0011, cap=1024, min_size=256, kind="weapon", notes="", ids=None, tex_name=None,
           entry_base=None):
    """Bake the shared atlas of the base and export every variant. builders = [W, ...] (their parts are in PARTS).
    ids: optional {variant: model id} (default <prefix>_<base>_v<k>). Returns the list of manifest entries."""
    t0 = time.time()
    vidx = {b.owner: i for i, b in enumerate(builders)}
    for o in PARTS:
        _prep(o)
        fin._set_int_attr(o, "own", vidx[o["owner"]])
    baked = [o for o in PARTS if o["cls"] != "emis"]
    emis = [o for o in PARTS if o["cls"] == "emis"]
    tex_base = tex_name or "%s_%s" % (prefix, base)
    paths = {k: os.path.join(GEAR_OUT, "%s_%s.jpg" % (tex_base, s)) for k, s in (('color', 'c'), ('normal', 'n'), ('orm', 'orm'))}
    for o in baked:
        real = o["sharp"]
        o["sharp"] = float(o.get("hem_sharp", real))
        fin._hem_attr(o)
        o["sharp"] = real
        co_ = get_co(o)
        dim = float(np.linalg.norm(co_.max(0) - co_.min(0))) if len(co_) else 0.0
        fin._set_face_int(o, "small", 1 if (fin._area(o) < 0.012 or dim < 0.09) else 0)
        assert o["src"] in mats.SRCM, "no source material '%s'" % o["src"]
        o.data.materials.clear()
        o.data.materials.append(mats.SRCM[o["src"]])
    fin._setup_cycles()
    J = fin.join(baked, "J_" + base)
    fin._triangulate(J)
    area = fin._area(J)
    size, need = fin._auto_size(area, texel, cap)
    size = max(size, min_size)
    own = fin._get_int_attr(J, "own")
    co = get_co(J)
    co[:, 0] += own * EXPLODE
    co[:, 2] += LIFT
    set_co(J, co)
    tu = time.time()
    fw = fin._exposure(J)
    fin._uv_unwrap(J, face_w=fw)
    log("unwrap %s: %.0fs  area %.3f m2 -> atlas %d px (need %.0f)" % (base, time.time() - tu, area, size, need))
    P = fin._bake_set(J, size, base)
    co = get_co(J)
    co[:, 0] -= own * EXPLODE
    co[:, 2] -= LIFT
    set_co(J, co)
    mats.save_rgb(P['color'], paths['color'], 92)
    mats.save_rgb(P['normal'], paths['normal'], 95)
    mats.save_rgb(P['orm'], paths['orm'], 92)
    write_import(paths['color'], "c")
    write_import(paths['normal'], "n")
    write_import(paths['orm'], "orm")
    if not os.path.isfile(os.path.join(GEAR_OUT, EMIS_FILE)):
        write_emis_texture()
    fm = _plain_material(tex_base)
    J.data.materials.clear()
    J.data.materials.append(fm)
    split = _split_by_own(J, len(builders))
    mat_tex = {tex_base: dict(c=os.path.basename(paths['color']), n=os.path.basename(paths['normal']),
                              orm=os.path.basename(paths['orm']))}
    for o in emis:
        key = o["src"]
        fin._triangulate(o)
        _emis_uv(o, key)
        col, strength = next((c, s) for k, c, s in EMIS if k == key)
        nm = "emis_%s_%s" % (base, key)
        em = _plain_material(nm, color=tuple(v * 0.25 for v in col), rough=0.4, emis=col, strength=strength)
        o.data.materials.clear()
        o.data.materials.append(em)
        mat_tex[nm] = dict(e=EMIS_FILE, strength=strength)
    entries = []
    emis_of = {}                       # owner -> its emissive objects (looked up before any join removes objects)
    for o in emis:
        emis_of.setdefault(o["owner"], []).append(o)
    for b in builders:
        k = vidx[b.owner]
        mid = (ids or {}).get(b.v, "%s_%s_v%d" % (prefix, base, b.v))
        objs = ([split[k]] if k in split else []) + emis_of.get(b.owner, [])
        has_emis = bool(emis_of.get(b.owner))
        obj = fin.join(objs, mid)
        obj.name = "model"          # Godot names the scene root after the file; a child of the same name would get a suffix
        _strip(obj)
        co = get_co(obj)
        mn, mx = co.min(0), co.max(0)
        ext = mx - mn
        length = float(ext.max())
        tris = mk.tri_count(obj)
        info = dict(b.info)
        extras = dict(id=mid, base=(entry_base or {}).get(b.v, base), grip=info.get("grip", "r"), mount=info.get("mount", "hand_r"),
                      length_m=round(length, 4))
        if info.get("grip2") is not None:
            extras["grip2_offset"] = [round(float(v), 4) for v in info["grip2"]]
        path = os.path.join(GEAR_OUT, mid + ".glb")
        _export_glb(path, obj)
        sz = patch_glb(path, mat_tex, extras)
        e = dict(id=mid, kind=kind, base=(entry_base or {}).get(b.v, base), file=RES + mid + ".glb", mount=info.get("mount", "hand_r"),
                 grip2=extras.get("grip2_offset"), length=round(length, 3),
                 variant=b.v, hands=info.get("hands", 1), grip=info.get("grip", "r"), tris=tris, emissive=has_emis,
                 bbox=[[round(float(v), 4) for v in mn], [round(float(v), 4) for v in mx]],
                 textures=tex_base, atlas_px=size)
        for extra_key in ("hint", "file_l", "display_scale", "name"):
            if info.get(extra_key) is not None:
                e[extra_key] = info[extra_key]
        entries.append(e)
        log("EXPORTED %-22s tris=%-5d len=%.3f m  size=%.0f kB  mats=%s" % (mid, tris, length, sz / 1024.0, [m.name for m in obj.data.materials]))
        if tris > 6000 or tris < 1000:
            log("   NOTE: %s is outside the 1-6k triangle budget (%d)" % (mid, tris))
    with open(os.path.join(MANI, "%s_%s.json" % (prefix, base)), "w", encoding="utf8") as f:
        json.dump(dict(base=base, prefix=prefix, atlas_px=size, area_m2=round(area, 4), notes=notes, entries=entries,
                       built=time.strftime("%Y-%m-%d %H:%M:%S")), f, indent=1)
    log("DONE %s: %d models, atlas %d px, %.0fs" % (base, len(entries), size, time.time() - t0))
    return entries


# ------------------------------------------------------------------------------------------------ fast preview
def fast_materials():
    """flat colours instead of the bake (shape look-dev): every part gets a plain material from SRCINFO"""
    for o in PARTS:
        mk.apply_mods(o)
        _prep(o)
        k = o["src"]
        info = mats.SRCINFO.get(k, dict(color=(0.5, 0.5, 0.5), rough=0.6, metal=0.0))
        if o["cls"] == "emis":
            col, strength = next((c, s) for kk, c, s in EMIS if kk == k)
            fm = _plain_material("fast_" + k, color=tuple(v * 0.25 for v in col), rough=0.4, emis=col, strength=strength)
        else:
            fm = _plain_material("fast_" + k, color=info["color"], rough=info["rough"], metal=info["metal"])
        o.data.materials.clear()
        o.data.materials.append(fm)
