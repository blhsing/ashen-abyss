"""Renders the close-up of 寂主・奧薩洛斯 (boss_osaros.glb) that the application icon is built from.

    run_blender.ps1 -A @('-b','--factory-startup','--python','Y:\\tools\\appicon\\render_boss_face.py','--',
                         '<out.png>', '[mode]', '[size]')

mode "icon" (default): the head and crown framed tightly, ember under-light, a cold rim from behind, the crack and
eyes glowing, transparent background, square <size> px (default 1024).
mode "probe": a wider front view in neutral light, to look at the model while tuning.
The model stands in its rest pose (the GLB carries the skeleton, the shared idle clips live in anim_lib.glb, whose
first frame of Idle_Loop is applied when the bone names match).
"""
import sys, math
import bpy
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else r"Y:\shots\title\icon_render.png"
MODE = argv[1] if len(argv) > 1 else "icon"
SIZE = int(argv[2]) if len(argv) > 2 else 1024
GLB = r"Y:\game\assets\chars\boss_osaros.glb"
LIB = r"Y:\game\assets\chars\anim_lib.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]

# the idle pose: borrow the first frame of Idle_Loop from the shared library when its bones match
def apply_idle():
    if arm is None:
        return
    before = set(bpy.data.objects)
    try:
        bpy.ops.import_scene.gltf(filepath=LIB)
    except Exception as e:
        print("idle: anim_lib import failed:", e)
        return
    new = [o for o in bpy.data.objects if o not in before]
    names = [a.name for a in bpy.data.actions]
    print("idle: actions", [n for n in names if "idle" in n.lower()])
    act = next((a for a in bpy.data.actions if a.name.lower().split("|")[-1] in ("idle_loop", "idle")), None) or next((a for a in bpy.data.actions if a.name.lower().startswith("idle")), None)
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    if act is None:
        print("idle: no Idle_Loop action, rest pose kept")
        return
    arm.animation_data_create()
    arm.animation_data.action = act
    try:
        slot = act.slots[0] if hasattr(act, "slots") and len(act.slots) else None
        if slot is not None:
            arm.animation_data.action_slot = slot
    except Exception as e:
        print("idle: slot", e)
    bpy.context.scene.frame_set(int(act.frame_range[0]) + 1)
    print("idle: applied", act.name)

if MODE != "rest":
    apply_idle()
bpy.context.view_layer.update()

for o in meshes:
    if o.name.lower().startswith("wpn_"):
        o.hide_render = True

# the head: the Head bone's world position, the extent from vertices that ride on it (crown included)
deps = bpy.context.evaluated_depsgraph_get()
head = None
if arm is not None and "Head" in arm.pose.bones:
    pb = arm.pose.bones["Head"]
    head = arm.matrix_world @ pb.head
pts = []
for o in meshes:
    if o.hide_render:
        continue
    ev = o.evaluated_get(deps)
    me = ev.to_mesh()
    mw = o.matrix_world
    for v in me.vertices:
        pts.append(mw @ v.co)
    ev.to_mesh_clear()
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
print("bounds", tuple(round(c, 3) for c in lo), tuple(round(c, 3) for c in hi), "head", head)
if head is None:
    head = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, hi.z - 0.25 * (hi.z - lo.z)))
above = [p for p in pts if p.z > head.z - 0.08 and abs(p.x - head.x) < 0.45]
top = max(p.z for p in above) if above else hi.z
print("head top", round(top, 3))

# the glow: the crack (emis_ material) brighter and a touch warm-white
for m in bpy.data.materials:
    if not m.use_nodes:
        continue
    bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        continue
    if m.name.lower().startswith("emis"):
        bsdf.inputs["Emission Color"].default_value = (1.0, 0.86, 0.62, 1)
        bsdf.inputs["Emission Strength"].default_value = 14.0 if MODE == "icon" else 3.0

scn = bpy.context.scene
scn.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE_NEXT"
scn.render.resolution_x = SIZE
scn.render.resolution_y = SIZE
scn.render.film_transparent = MODE == "icon"
scn.render.image_settings.file_format = "PNG"
scn.render.image_settings.color_mode = "RGBA"
try:
    scn.view_settings.view_transform = "AgX"
    scn.view_settings.look = "AgX - High Contrast" if MODE == "icon" else "None"
except Exception:
    try: scn.view_settings.view_transform = "Filmic"
    except Exception: pass
ee = scn.eevee
for attr, val in (("taa_render_samples", 64), ("use_gtao", True), ("use_shadows", True)):
    try: setattr(ee, attr, val)
    except Exception: pass

world = bpy.data.worlds.new("W")
scn.world = world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (0.004, 0.005, 0.008, 1) if MODE == "icon" else (0.25, 0.25, 0.27, 1)
bg.inputs[1].default_value = 1.0

def light(name, kind, loc, aim, energy, color, size=0.3, spot=None):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = color
    if hasattr(ld, "shadow_soft_size"): ld.shadow_soft_size = size
    if kind == "AREA": ld.size = size
    if spot and kind == "SPOT":
        ld.spot_size = math.radians(spot); ld.spot_blend = 0.6
    ob = bpy.data.objects.new(name, ld)
    scn.collection.objects.link(ob)
    ob.location = loc
    d = (Vector(aim) - Vector(loc)).normalized()
    ob.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    return ob

# the front of the model: glTF +Z forward becomes Blender -Y
front = Vector((0, -1, 0))
side = Vector((1, 0, 0))
hz = (top - head.z)
centre = Vector((head.x, head.y, head.z + hz * 0.38))

cam_d = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_d)
scn.collection.objects.link(cam)
scn.camera = cam

if MODE == "probe":
    span = (hi.z - lo.z)
    c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z + span * 0.62))
    cam.location = c + front * span * 1.4 + side * span * 0.35
    cam_d.lens = 50
    cam.rotation_euler = (c - cam.location).normalized().to_track_quat("-Z", "Y").to_euler()
    light("Key", "SUN", c + front * 3 + side * 2 + Vector((0, 0, 3)), c, 3.0, (1, 1, 1))
    light("Fill", "SUN", c + front * 3 - side * 2, c, 1.0, (0.8, 0.85, 1))
else:
    # a little from his right and from below: the crown and pauldron spires stand against the dark
    import os
    yaw = math.radians(float(os.environ.get("ICON_YAW", "14")))
    lift = float(os.environ.get("ICON_LIFT", "-0.10"))
    dist = float(os.environ.get("ICON_DIST", "2.4"))
    fit = float(os.environ.get("ICON_FIT", "1.0"))
    d = (front * math.cos(yaw) + side * math.sin(yaw)).normalized()
    cam.location = centre + d * dist + Vector((0, 0, lift * dist))
    cam.rotation_euler = (centre - cam.location).normalized().to_track_quat("-Z", "Y").to_euler()
    span = (top - head.z) * 1.45 + 0.25
    cam_d.lens_unit = "FOV"
    cam_d.angle = 2 * math.atan(span * 0.5 * fit / dist)
    s = hz + 0.3
    # ember under-light from below and in front, a warm low side rim, a cold rim from behind
    light("Under", "AREA", centre + front * 0.9 + Vector((0, 0, -1.1)), centre + Vector((0, 0, 0.1)), 160 * s, (1.0, 0.42, 0.12), 0.6)
    light("Ember", "SPOT", centre + front * 0.4 - side * 1.3 + Vector((0, 0, -0.8)), centre, 260 * s, (1.0, 0.36, 0.08), 0.15, 50)
    light("Cold", "SPOT", centre - front * 1.6 + side * 0.9 + Vector((0, 0, 1.2)), centre, 380 * s, (0.45, 0.6, 1.0), 0.2, 45)
    light("Cold2", "SPOT", centre - front * 1.6 - side * 1.0 + Vector((0, 0, 0.6)), centre, 220 * s, (0.5, 0.62, 1.0), 0.2, 45)
    print("cam", tuple(round(c, 3) for c in cam.location), "centre", tuple(round(c, 3) for c in centre), "fov", math.degrees(cam_d.angle))

scn.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("rendered", OUT)
