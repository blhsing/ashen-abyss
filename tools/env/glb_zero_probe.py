# Report which bufferViews / accessors of a .glb cover all-zero 4 KB blocks (to tell a damaged file from legitimate zero data).
# run: python -I Y:\tools\env\glb_zero_probe.py <file.glb>
import json, struct, sys

data = open(sys.argv[1], "rb").read()
jl = struct.unpack("<I", data[12:16])[0]
g = json.loads(data[20:20 + jl])
bo = 20 + jl + 8
blocks = [i for i in range(0, len(data) - 4095, 4096) if not any(data[i:i + 4096])]
print("zero blocks at file offsets:", blocks)
for off in blocks:
    rel = off - bo
    for i, bv in enumerate(g["bufferViews"]):
        s = bv.get("byteOffset", 0)
        if s <= rel + 4095 and rel < s + bv["byteLength"]:
            users = [(k, a["type"], a["componentType"], a["count"]) for k, a in enumerate(g["accessors"]) if a.get("bufferView") == i]
            attrs = [(mi, an) for mi, m in enumerate(g["meshes"]) for p in m["primitives"] for an, ai in list(p["attributes"].items()) + [("indices", p.get("indices"))] if any(ai == u[0] for u in users)]
            imgs = [im.get("name") for im in g.get("images", []) if im.get("bufferView") == i]
            print("  offset %d -> bufferView %d (%d..%d) accessors %s attrs %s images %s" % (off, i, s, s + bv["byteLength"], users, attrs, imgs))
