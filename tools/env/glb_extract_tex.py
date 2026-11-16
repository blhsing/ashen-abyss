# Extract the base-colour (and optionally normal) texture of a prop .glb for a quick look without rendering.
# run: python -I Y:\tools\env\glb_extract_tex.py <out_dir> <name> [<name> ...]   (add --normal to also dump the normal map)
import json, os, struct, sys

ROOT = r"Y:\game\assets\env\models"
args = [a for a in sys.argv[1:] if not a.startswith("--")]
out = args[0]
os.makedirs(out, exist_ok=True)
for name in args[1:]:
    data = open(os.path.join(ROOT, name, name + ".glb"), "rb").read()
    jl = struct.unpack("<I", data[12:16])[0]
    g = json.loads(data[20:20 + jl])
    binb = data[20 + jl + 8:]
    for mi, m in enumerate(g.get("materials", [])):
        want = [("albedo", m.get("pbrMetallicRoughness", {}).get("baseColorTexture"))]
        if "--normal" in sys.argv:
            want.append(("normal", m.get("normalTexture")))
        for tag, t in want:
            if not t:
                continue
            im = g["images"][g["textures"][t["index"]]["source"]]
            bv = g["bufferViews"][im["bufferView"]]
            b = binb[bv.get("byteOffset", 0): bv.get("byteOffset", 0) + bv["byteLength"]]
            ext = ".png" if b[:4] == b"\x89PNG" else ".jpg"
            fp = os.path.join(out, "%s_m%d_%s%s" % (name, mi, tag, ext))
            open(fp, "wb").write(b)
            print(fp, len(b))
