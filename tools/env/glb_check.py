# Independent check of the exported .glb files (pure Python): header, triangle count, embedded image formats/sizes, alpha modes, node names.
# run: python -I Y:\tools\env\glb_check.py     -> prints problems and a summary; writes Y:\assets_src\env\glb_check.json
import json, os, struct, sys

ROOT = r"Y:\game\assets\env\models"
MAN = os.path.join(ROOT, "manifest.json")
BUDGET = {"small": 3000, "medium": 8000, "large": 20000}


def img_size(b):
    if b[:8] == b"\x89PNG\r\n\x1a\n":
        w, h = struct.unpack(">II", b[16:24]); return "png", w, h
    if b[:2] == b"\xff\xd8":
        i = 2
        while i < len(b) - 9:
            if b[i] != 0xFF:
                i += 1; continue
            m = b[i + 1]
            if m in (0xC0, 0xC1, 0xC2):
                h, w = struct.unpack(">HH", b[i + 5:i + 9]); return "jpeg", w, h
            if m in (0xD8, 0xD9) or 0xD0 <= m <= 0xD7:
                i += 2; continue
            i += 2 + struct.unpack(">H", b[i + 2:i + 4])[0]
    return "?", 0, 0


def check(path):
    data = open(path, "rb").read()
    magic, ver, length = struct.unpack("<4sII", data[:12])
    assert magic == b"glTF" and ver == 2 and length == len(data), "bad header"
    jl, jt = struct.unpack("<II", data[12:20]); assert jt == 0x4E4F534A
    g = json.loads(data[20:20 + jl])
    bo = 20 + jl
    bl, bt = struct.unpack("<II", data[bo:bo + 8]); binb = data[bo + 8: bo + 8 + bl]
    tris = 0
    for m in g.get("meshes", []):
        for p in m["primitives"]:
            assert p.get("mode", 4) == 4, "non-triangle primitive"
            assert "TEXCOORD_0" in p["attributes"] and "NORMAL" in p["attributes"], "missing uv/normal"
            tris += g["accessors"][p["indices"]]["count"] // 3
    imgs = []
    for im in g.get("images", []):
        bv = g["bufferViews"][im["bufferView"]]
        b = binb[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
        imgs.append(img_size(b) + (len(b),))
    mats = [(m.get("name"), m.get("alphaMode", "OPAQUE"), "baseColorTexture" in m.get("pbrMetallicRoughness", {}), "normalTexture" in m,
             "metallicRoughnessTexture" in m.get("pbrMetallicRoughness", {})) for m in g.get("materials", [])]
    nodes = [n.get("name") for n in g.get("nodes", []) if "mesh" in n]
    roots = g["scenes"][g.get("scene", 0)]["nodes"]
    # world bbox from POSITION accessor min/max of root-level mesh nodes (transforms are applied at export except part pivots)
    return {"tris": tris, "images": imgs, "materials": mats, "mesh_nodes": nodes, "roots": len(roots), "bytes": len(data)}


man = {p["name"]: p for p in json.load(open(MAN, encoding="utf-8"))["props"]} if os.path.exists(MAN) else {}
out = {}; problems = []
for d in sorted(os.listdir(ROOT)):
    fp = os.path.join(ROOT, d, d + ".glb")
    if not os.path.isfile(fp):
        continue
    try:
        r = check(fp)
    except Exception as e:
        problems.append("%s: INVALID %s" % (d, e)); continue
    out[d] = r
    m = man.get(d)
    if m is None:
        problems.append("%s: not in manifest" % d); continue
    if r["tris"] != m["triangles"]:
        problems.append("%s: tris %d != manifest %d" % (d, r["tris"], m["triangles"]))
    mx = max([max(i[1], i[2]) for i in r["images"]] or [0])
    if mx > 2048 or (mx > 1024 and r["tris"] <= 8000 and m["category"] not in ("rock", "tree", "structure")):
        problems.append("%s: texture %d px" % (d, mx))
    if r["tris"] > 20000:
        problems.append("%s: %d tris over hero budget" % (d, r["tris"]))
    if not r["images"]:
        problems.append("%s: no embedded textures" % d)
    for mt in r["materials"]:
        if not mt[2]:
            problems.append("%s: material %s has no base colour texture" % (d, mt[0]))
for n in man:
    if n not in out:
        problems.append("%s: in manifest but no glb" % n)
json.dump(out, open(r"Y:\assets_src\env\glb_check.json", "w"), indent=0)
print("glb files: %d  total %.1f MB  tris sum %d" % (len(out), sum(r["bytes"] for r in out.values()) / 1e6, sum(r["tris"] for r in out.values())))
fm = {}
for r in out.values():
    for i in r["images"]:
        fm[i[0]] = fm.get(i[0], 0) + 1
print("image formats:", fm, " alpha modes:", {a: sum(1 for r in out.values() for m in r["materials"] if m[1] == a) for a in ("OPAQUE", "MASK", "BLEND")})
print("problems: %d" % len(problems))
for p in problems:
    print("  ", p)
