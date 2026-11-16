# Inspect every downloaded Poly Haven glTF: objects, triangles, dimensions, materials.
# run: blender -b --factory-startup --python Y:\tools\env\mdl_inspect.py -- [id ...]
import bpy, os, sys, json
from mathutils import Vector

ROOT = r"Y:\assets_src\env\polyhaven\models"
OUT = r"Y:\assets_src\env\polyhaven\models_inspect.txt"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ids = argv or sorted(d for d in os.listdir(ROOT) if os.path.isdir(os.path.join(ROOT, d)))
lines = []


def tris(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def bbox(objs):
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi


for mid in ids:
    p = os.path.join(ROOT, mid, mid + ".gltf")
    if not os.path.exists(p):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try:
        bpy.ops.import_scene.gltf(filepath=p)
    except Exception as e:
        lines.append("%s: IMPORT FAILED %s" % (mid, e)); continue
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        lines.append("%s: no meshes" % mid); continue
    lo, hi = bbox(meshes)
    d = hi - lo
    mats = sorted(set(s.material.name for o in meshes for s in o.material_slots if s.material))
    minfo = []
    for mn in mats:
        m = bpy.data.materials[mn]
        tex = sorted(set(n.image.name.replace(mid + "_", "") for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image)) if m.node_tree else []
        bm = getattr(m, "blend_method", "?")
        minfo.append("%s<%s|%s>" % (mn.replace(mid + "_", "").replace(mid, "~"), bm[:3], ",".join(t.split(".")[0].replace("_1k", "").replace("_2k", "") for t in tex)))
    tt = sum(tris(o.data) for o in meshes)
    lines.append("%s: %d obj, %d tris, %.2fx%.2fx%.2f (z %.2f..%.2f) mats %s" % (mid, len(meshes), tt, d.x, d.y, d.z, lo.z, hi.z, " ".join(minfo)))
    if len(meshes) > 1:
        for o in sorted(meshes, key=lambda o: o.name)[:16]:
            l2, h2 = bbox([o]); d2 = h2 - l2
            lines.append("    %s: %d t, %.2fx%.2fx%.2f at (%.2f,%.2f,%.2f) m=%s" % (o.name.replace(mid, "~"), tris(o.data), d2.x, d2.y, d2.z, (l2.x + h2.x) / 2, (l2.y + h2.y) / 2, l2.z,
                                                                    ",".join(s.material.name.replace(mid + "_", "").replace(mid, "~") for s in o.material_slots if s.material)))
        if len(meshes) > 16:
            lines.append("    ... +%d more" % (len(meshes) - 16))
    print(lines[-1] if len(meshes) == 1 else mid, flush=True)

mode = "a" if argv else "w"
open(OUT, mode, encoding="utf-8").write("\n".join(lines) + "\n")
