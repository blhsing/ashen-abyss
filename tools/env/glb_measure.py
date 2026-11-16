# Measure every exported prop .glb straight from the file (pure Python, no Blender):
#   triangles, vertices, world bounding box in Godot axes (glTF is already +Y up), materials, embedded images, node names.
# Also validates structure: header, chunk lengths, accessor/bufferView ranges, every primitive has a material + UVs + normals,
# every material has a base-colour texture, every image is a decodable JPEG/PNG header.
# run: python -I Y:\tools\env\glb_measure.py            -> Y:\assets_src\env\glb_measure.json + summary on stdout
import json, os, struct, sys, array

ROOT = r"Y:\game\assets\env\models"
OUT = r"Y:\assets_src\env\glb_measure.json"
COMP = {5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2), 5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4)}
NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


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
            if m in (0xD8, 0xD9) or 0xD0 <= m <= 0xD7 or m == 0xFF:
                i += 2 if m != 0xFF else 1; continue
            i += 2 + struct.unpack(">H", b[i + 2:i + 4])[0]
    return "?", 0, 0


def mat_mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def node_matrix(n):
    if "matrix" in n:
        m = n["matrix"]  # column-major
        return [[m[c * 4 + r] for c in range(4)] for r in range(4)]
    t = n.get("translation", [0, 0, 0]); q = n.get("rotation", [0, 0, 0, 1]); s = n.get("scale", [1, 1, 1])
    x, y, z, w = q
    r = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
         [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
         [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
    return [[r[0][0] * s[0], r[0][1] * s[1], r[0][2] * s[2], t[0]],
            [r[1][0] * s[0], r[1][1] * s[1], r[1][2] * s[2], t[1]],
            [r[2][0] * s[0], r[2][1] * s[1], r[2][2] * s[2], t[2]],
            [0, 0, 0, 1]]


IDENT = [[1 if i == j else 0 for j in range(4)] for i in range(4)]


def measure(path):
    data = open(path, "rb").read()
    prob = []
    magic, ver, length = struct.unpack("<4sII", data[:12])
    if magic != b"glTF" or ver != 2:
        raise ValueError("not a glTF 2 binary")
    if length != len(data):
        prob.append("header length %d != file size %d" % (length, len(data)))
    jl, jt = struct.unpack("<II", data[12:20])
    if jt != 0x4E4F534A:
        raise ValueError("first chunk is not JSON")
    g = json.loads(data[20:20 + jl].decode("utf-8"))
    bo = 20 + jl
    bl, bt = struct.unpack("<II", data[bo:bo + 8])
    if bt != 0x004E4942:
        raise ValueError("second chunk is not BIN")
    binb = data[bo + 8: bo + 8 + bl]
    if len(binb) != bl:
        raise ValueError("BIN chunk truncated")
    for i, bv in enumerate(g.get("bufferViews", [])):
        if bv.get("byteOffset", 0) + bv["byteLength"] > bl:
            prob.append("bufferView %d out of range" % i)

    def acc_floats(ai):
        a = g["accessors"][ai]
        bv = g["bufferViews"][a["bufferView"]]
        fmt, sz = COMP[a["componentType"]]
        nc = NCOMP[a["type"]]
        off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
        stride = bv.get("byteStride", sz * nc)
        if stride == sz * nc:
            arr = array.array(fmt); arr.frombytes(binb[off: off + a["count"] * sz * nc])
            return arr, nc
        out = array.array(fmt)
        for k in range(a["count"]):
            out.frombytes(binb[off + k * stride: off + k * stride + sz * nc])
        return out, nc

    tris = verts = 0
    lo = [1e18] * 3; hi = [-1e18] * 3
    no_mat = no_uv = no_nrm = 0
    mesh_nodes = []
    vcol = []
    scene = g["scenes"][g.get("scene", 0)]

    def walk(ni, parent):
        nonlocal tris, verts, no_mat, no_uv, no_nrm
        n = g["nodes"][ni]
        m = mat_mul(parent, node_matrix(n))
        if "mesh" in n:
            ident = all(abs(m[i][j] - IDENT[i][j]) < 1e-9 for i in range(4) for j in range(4))
            mesh_nodes.append({"name": n.get("name"), "translation": [round(m[0][3], 4), round(m[1][3], 4), round(m[2][3], 4)], "transformed": not ident})
            for p in g["meshes"][n["mesh"]]["primitives"]:
                if p.get("mode", 4) != 4:
                    prob.append("non-triangle primitive"); continue
                if "material" not in p:
                    no_mat += 1
                if "TEXCOORD_0" not in p["attributes"]:
                    no_uv += 1
                if "NORMAL" not in p["attributes"]:
                    no_nrm += 1
                for an in p["attributes"]:
                    if an.startswith("COLOR_"):
                        ca = g["accessors"][p["attributes"][an]]
                        cv, cn = acc_floats(p["attributes"][an])
                        div = {5121: 255.0, 5123: 65535.0}.get(ca["componentType"], 1.0)
                        vcol.append({"attr": an, "min": [round(min(cv[k::cn]) / div, 3) for k in range(cn)], "max": [round(max(cv[k::cn]) / div, 3) for k in range(cn)]})
                pa = g["accessors"][p["attributes"]["POSITION"]]
                verts += pa["count"]
                tris += (g["accessors"][p["indices"]]["count"] if "indices" in p else pa["count"]) // 3
                pos, nc = acc_floats(p["attributes"]["POSITION"])
                xs = pos[0::3]; ys = pos[1::3]; zs = pos[2::3]
                if ident:
                    for k, c in enumerate((xs, ys, zs)):
                        lo[k] = min(lo[k], min(c)); hi[k] = max(hi[k], max(c))
                else:
                    for x, y, z in zip(xs, ys, zs):
                        for k in range(3):
                            v = m[k][0] * x + m[k][1] * y + m[k][2] * z + m[k][3]
                            if v < lo[k]: lo[k] = v
                            if v > hi[k]: hi[k] = v
        for c in n.get("children", []):
            walk(c, m)

    for r in scene["nodes"]:
        walk(r, IDENT)
    if no_mat:
        prob.append("%d primitive(s) without material" % no_mat)
    if no_uv:
        prob.append("%d primitive(s) without UVs" % no_uv)
    if no_nrm:
        prob.append("%d primitive(s) without normals" % no_nrm)
    imgs = []
    for i, im in enumerate(g.get("images", [])):
        if "bufferView" not in im:
            prob.append("image %d is external (%s)" % (i, im.get("uri", "?")[:40])); continue
        bv = g["bufferViews"][im["bufferView"]]
        b = binb[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
        f, w, h = img_size(b)
        if f == "?" or not w or not h:
            prob.append("image %d not a readable JPEG/PNG" % i)
        imgs.append([f, w, h, len(b)])
    mats = []
    for mt in g.get("materials", []):
        pbr = mt.get("pbrMetallicRoughness", {})
        e = {"name": mt.get("name"), "alpha": mt.get("alphaMode", "OPAQUE"), "base": "baseColorTexture" in pbr, "normal": "normalTexture" in mt,
             "orm": "metallicRoughnessTexture" in pbr, "emissive": "emissiveTexture" in mt or any(mt.get("emissiveFactor", [0, 0, 0])),
             "double_sided": bool(mt.get("doubleSided"))}
        if not e["base"]:
            e["base_color"] = [round(v, 3) for v in pbr.get("baseColorFactor", [1, 1, 1, 1])]
        mats.append(e)
    if not mats:
        prob.append("no materials")
    for e in mats:
        if not e["base"] and "glass" not in (e["name"] or "").lower():
            prob.append("material %s has no base colour texture" % e["name"])
    if not tris:
        prob.append("no triangles")
    for c in vcol:
        if c["attr"] == "COLOR_0" and max(c["max"][:3]) < 0.05:
            prob.append("COLOR_0 vertex colours are black (Godot multiplies albedo by COLOR_0)")
    return {"triangles": tris, "vertices": verts, "vertex_colors": vcol,
            "min": [round(v, 4) for v in lo], "max": [round(v, 4) for v in hi],
            "size": [round(hi[k] - lo[k], 3) for k in range(3)],
            "materials": mats, "images": imgs, "max_texture_px": max([max(i[1], i[2]) for i in imgs] or [0]),
            "mesh_nodes": mesh_nodes, "bytes": len(data), "problems": prob}


def main():
    out = {}; broken = []
    names = [a for a in sys.argv[1:] if not a.startswith("-")]
    for d in sorted(os.listdir(ROOT)):
        if names and d not in names:
            continue
        if not os.path.isdir(os.path.join(ROOT, d)):
            continue
        fp = os.path.join(ROOT, d, d + ".glb")
        if not os.path.isfile(fp):
            broken.append((d, "folder has no %s.glb" % d)); continue
        try:
            out[d] = measure(fp)
        except Exception as e:
            broken.append((d, "INVALID: %s" % e)); continue
        for p in out[d]["problems"]:
            broken.append((d, p))
    if not names:
        json.dump(out, open(OUT, "w", encoding="utf-8"), indent=0)
    else:
        for n, r in out.items():
            print(n, json.dumps({k: r[k] for k in ("triangles", "size", "min", "max", "max_texture_px", "bytes", "problems")}))
    print("glb measured: %d  total %.1f MB  triangles %d  max tris %d" % (
        len(out), sum(r["bytes"] for r in out.values()) / 1e6, sum(r["triangles"] for r in out.values()), max([r["triangles"] for r in out.values()] or [0])))
    print("problems: %d" % len(broken))
    for n, p in broken:
        print("   %s: %s" % (n, p))


main()
