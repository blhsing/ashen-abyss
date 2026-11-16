# Render labelled contact sheets of the exported .glb props (re-imported from disk, EEVEE, neutral light).
# run: blender -b --factory-startup --python Y:\tools\env\mdl_sheet.py -- [--out prefix] [name ...]
import bpy, os, sys, json, math
from mathutils import Vector, Euler

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from mdl_spec import SPEC

OUT = r"Y:\game\assets\env\models"
STAT = r"Y:\assets_src\env\models_build"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
prefix = r"Y:\shots\env\props_sheet"
if "--out" in argv:
    i = argv.index("--out"); prefix = argv[i + 1]; del argv[i:i + 2]
names = argv or [s["name"] for s in SPEC if os.path.exists(os.path.join(STAT, s["name"] + ".json"))]
COLS, ROWS, CELL = 6, 4, 2.0
TILT = math.radians(18)


def world_bbox(objs):
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for o in objs:
        if o.type != "MESH":
            continue
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    return lo, hi


def sheet(idx, chunk):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng; break
        except Exception:
            pass
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.42, 0.43, 0.46, 1); bg.inputs[1].default_value = 0.9
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.2; sun.rotation_euler = Euler((math.radians(52), 0, math.radians(-38)))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"
    cam.rotation_euler = Euler((math.pi / 2 - TILT, 0, 0))
    rows = math.ceil(len(chunk) / COLS)
    width = COLS * CELL
    top, bottom = CELL * 1.02, -(rows - 1) * CELL - 0.16
    height = (top - bottom) * math.cos(TILT)
    cx = (COLS - 1) * CELL / 2
    cz = (top + bottom) / 2
    fwd = cam.rotation_euler.to_matrix() @ Vector((0, 0, -1))
    cam.location = Vector((cx, 0, cz)) - fwd * 60
    cam.data.ortho_scale = width * 1.01
    cam.data.clip_end = 200
    sc.render.resolution_x = 1920
    sc.render.resolution_y = int(1920 * height / (width * 1.01))
    font_mat = bpy.data.materials.new("txt"); font_mat.use_nodes = True
    bs = font_mat.node_tree.nodes["Principled BSDF"]
    bs.inputs["Base Color"].default_value = (0, 0, 0, 1)
    bs.inputs["Emission Color"].default_value = (1, 1, 1, 1); bs.inputs["Emission Strength"].default_value = 1.0
    for i, name in enumerate(chunk):
        c, r = i % COLS, i // COLS
        base = Vector((c * CELL, 0, -r * CELL))
        fp = os.path.join(OUT, name, name + ".glb")
        info = json.load(open(os.path.join(STAT, name + ".json")))
        before = set(bpy.data.objects)
        try:
            bpy.ops.import_scene.gltf(filepath=fp)
        except Exception as e:
            print("IMPORT FAIL", name, e); continue
        new = [o for o in bpy.data.objects if o not in before]
        bpy.context.view_layer.update()
        lo, hi = world_bbox(new); d = hi - lo
        emp = bpy.data.objects.new("e_" + name, None); sc.collection.objects.link(emp)
        for o in new:
            if o.parent is None:
                o.parent = emp
        rad = max(math.hypot(d.x, d.y), d.z * 1.15, 1e-4)
        s = 0.80 * CELL / rad
        emp.rotation_euler = Euler((0, 0, math.radians(32)))
        emp.scale = (s, s, s)
        # origin marker = where the prop's origin sits; lift so the lowest point is above the label
        emp.location = base + Vector((0, 0, 0.30 - lo.z * s))
        cu = bpy.data.curves.new("t_" + name, "FONT")
        cu.body = "%s\n%.2fx%.2fx%.2f m  %d t" % (name, info["size"][0], info["size"][1], info["size"][2], info["triangles"])
        cu.size = 0.125; cu.align_x = "CENTER"; cu.space_line = 0.9
        t = bpy.data.objects.new("t_" + name, cu); sc.collection.objects.link(t)
        t.rotation_euler = cam.rotation_euler
        t.location = base + Vector((0, 0, 0.13)) - fwd * 6
        cu.materials.append(font_mat)
    fpout = "%s_%d.png" % (prefix, idx)
    sc.render.filepath = fpout
    sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True)
    print("SHEET", fpout, len(chunk), flush=True)


per = COLS * ROWS
for k in range(0, len(names), per):
    sheet(k // per, names[k:k + per])
