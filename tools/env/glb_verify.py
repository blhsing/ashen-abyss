# Re-import every exported prop .glb with Blender's glTF importer (no rendering) to prove it opens, and record what the importer sees:
# mesh objects, triangles, bounding box (converted to Godot axes), materials and whether each has a readable base-colour image.
# run (through the wrapper): blender -b --factory-startup --python Y:\tools\env\glb_verify.py -- [name ...]
#   -> Y:\assets_src\env\glb_verify.json and a summary on stdout
import bpy, os, sys, json
from mathutils import Vector

ROOT = r"Y:\game\assets\env\models"
OUT = r"Y:\assets_src\env\glb_verify.json"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
names = argv or sorted(d for d in os.listdir(ROOT) if os.path.isdir(os.path.join(ROOT, d)))
res = {}; bad = []
for name in names:
    fp = os.path.join(ROOT, name, name + ".glb")
    if not os.path.isfile(fp):
        bad.append((name, "missing .glb")); continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.gltf(filepath=fp)
    except Exception as e:
        bad.append((name, "IMPORT FAILED: %s" % str(e)[:160])); continue
    bpy.context.view_layer.update()
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        bad.append((name, "no mesh objects")); continue
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3); tris = 0
    for o in meshes:
        tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    mats = []
    probs = []
    for o in meshes:
        if not o.material_slots or any(s.material is None for s in o.material_slots):
            probs.append("object %s has an empty material slot" % o.name)
        if not o.data.uv_layers:
            probs.append("object %s has no UVs" % o.name)
    for m in bpy.data.materials:
        if m.users == 0 or not m.node_tree:
            continue
        bs = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        img = None
        if bs and bs.inputs["Base Color"].is_linked:
            src = bs.inputs["Base Color"].links[0].from_node
            hops = 0
            while src and src.type != "TEX_IMAGE" and hops < 6 and any(i.is_linked for i in src.inputs):
                src = next(i for i in src.inputs if i.is_linked).links[0].from_node; hops += 1
            if src and src.type == "TEX_IMAGE":
                img = src.image
        ok = bool(img and img.size[0] > 0 and img.size[1] > 0)
        glass = "glass" in m.name.lower()
        if not bs:
            probs.append("material %s has no Principled BSDF" % m.name)
        elif not ok and not glass:
            probs.append("material %s has no readable base-colour image" % m.name)
        mats.append({"name": m.name, "base_image": list(img.size) if ok else None, "normal": bool(bs and bs.inputs["Normal"].is_linked)})
    if not mats:
        probs.append("no materials")
    d = hi - lo
    res[name] = {"objects": [o.name for o in meshes], "triangles": tris,
                 "size_godot": [round(d.x, 3), round(d.z, 3), round(d.y, 3)],
                 "min_godot": [round(lo.x, 4), round(lo.z, 4), round(-hi.y, 4)], "max_godot": [round(hi.x, 4), round(hi.z, 4), round(-lo.y, 4)],
                 "materials": mats, "problems": probs}
    for p in probs:
        bad.append((name, p))
if not argv:
    json.dump(res, open(OUT, "w", encoding="utf-8"), indent=0)
print("VERIFY imported %d of %d  triangles %d" % (len(res), len(names), sum(r["triangles"] for r in res.values())))
print("VERIFY problems: %d" % len(bad))
for n, p in bad:
    print("VERIFY   %s: %s" % (n, p))
