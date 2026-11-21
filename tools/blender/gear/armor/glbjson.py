"""Print the structure of a .glb (JSON chunk only): nodes, meshes, primitives, materials, images, skins.
Run with Blender's bundled python (no Blender process):  python.exe -I glbjson.py <file.glb> [more.glb]"""
import json, struct, sys


def read_glb(path):
    with open(path, "rb") as f:
        data = f.read()
    magic, ver, length = struct.unpack_from("<4sII", data, 0)
    assert magic == b"glTF", path
    off = 12
    chunks = []
    while off < length:
        clen, ctype = struct.unpack_from("<I4s", data, off)
        chunks.append((ctype, data[off + 8:off + 8 + clen]))
        off += 8 + clen
    js = json.loads(chunks[0][1].decode("utf8"))
    binc = chunks[1][1] if len(chunks) > 1 else b""
    return js, binc


if __name__ == "__main__":
    for p in sys.argv[1:]:
        js, binc = read_glb(p)
        print("==", p, "bin", len(binc))
        print(" extras/scene:", js.get("scenes"), "ext:", js.get("extensionsUsed"))
        for i, n in enumerate(js.get("nodes", [])):
            if "mesh" in n or "skin" in n or i < 3:
                print("  node", i, n.get("name"), {k: v for k, v in n.items() if k not in ("name", "children", "rotation", "translation", "scale")})
        for i, m in enumerate(js.get("meshes", [])):
            print("  mesh", i, m.get("name"), [(sorted(pr["attributes"].keys()), pr.get("material")) for pr in m["primitives"]], m.get("extras"))
        for i, m in enumerate(js.get("materials", [])):
            print("  mat", i, json.dumps(m)[:400])
        for i, t in enumerate(js.get("textures", [])):
            print("  tex", i, t)
        for i, im in enumerate(js.get("images", [])):
            print("  img", i, {k: (v if k != "uri" or len(str(v)) < 100 else "data...") for k, v in im.items()})
        for i, s in enumerate(js.get("skins", [])):
            print("  skin", i, s.get("name"), len(s["joints"]), "joints", "root", s.get("skeleton"))
        print("  samplers", js.get("samplers"))
        print("  bones:", [js["nodes"][j]["name"] for j in js["skins"][0]["joints"]] if js.get("skins") else None)
