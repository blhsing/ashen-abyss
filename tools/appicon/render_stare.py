"""The application icon's render: 寂主・奧薩洛斯 in a "Kubrick stare" — head tilted down, facing the lens square-on,
the ember eyes glaring up from under the heavy brow; camera at about eye level; ember under-light, cold rim behind,
everything else falling off into black. Same game-like materials as ov2_render.py (back faces culled, emission
clamped like char.gdshader).

    run_blender.ps1 -A @('-b','--factory-startup','--python','Y:\\tools\\appicon\\render_stare.py','--',
                         '<out.png>', '[tilt_deg=20]', '[res=1536]', '[half_size=0.27]', '[cam_pitch=2]')

tilt: how far the chin is lowered (60 % on the head bone, 40 % on the neck); half_size: half the side of the
framed square in metres (rest-space model units) around the posed face; cam_pitch: + = camera above, looking down.
"""
import sys, os, math
sys.path.insert(0, r"Y:\tools\blender\monsters")
import bpy, numpy as np
from mathutils import Vector, Quaternion, Matrix
import monlib as M, stage as S

a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = a[0] if a else r"Y:\tools\appicon\renders\stare.png"
TILT = float(a[1]) if len(a) > 1 else 20.0
RES = int(a[2]) if len(a) > 2 else 1536
HALF = float(a[3]) if len(a) > 3 else 0.27
PITCH = float(a[4]) if len(a) > 4 else 2.0
GLB = r"Y:\game\assets\chars\boss_osaros.glb"
FACE = Vector((0.0, -0.118, 1.708))      # rest-space point between the eyes (the title's EyeMid)
IDLE_T = float(os.environ.get('STARE_IDLE_T', '0.5'))   # where in Idle_Loop the still is taken (-1 = rest pose)

M.reset_scene()
arm, meshes = S.import_creature(GLB)
for o in meshes:
    if o.name.lower().startswith("wpn"):
        o.hide_render = True
    for m in o.data.materials:
        if m is None:
            continue
        m.use_backface_culling = True
        if m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == 'BSDF_PRINCIPLED':
                    es = n.inputs.get('Emission Strength')
                    if es is not None and es.default_value > 0:
                        es.default_value = min(es.default_value, 2.6) if m.name.startswith('emis') else 0.0
acts = S.load_actions() if IDLE_T >= 0 else {}
S.set_pose(arm, acts.get('Idle_Loop'), IDLE_T) if acts.get('Idle_Loop') else S.set_pose(arm, None)
# freeze the idle frame as a plain pose (an active action would overwrite the nod at render time)
if arm.animation_data and arm.animation_data.action:
    keep = {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy()) for pb in arm.pose.bones}
    arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.location, pb.rotation_quaternion, pb.scale = keep[pb.name]
bpy.context.view_layer.update()
names = [b.name for b in arm.pose.bones]
M.log("bones", [n for n in names if "head" in n.lower() or "neck" in n.lower()])


def nod(name, deg):
    """rotate a pose bone about the world X axis (+ = top of the head forward, chin down)"""
    pb = arm.pose.bones.get(name)
    if pb is None:
        M.log("no bone", name); return
    rw = Quaternion(Vector((1, 0, 0)), math.radians(deg))
    pb.rotation_mode = 'QUATERNION'
    rest = (arm.matrix_world.to_quaternion() @ pb.bone.matrix_local.to_quaternion())
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = rest.inverted() @ rw @ rest @ pb.rotation_quaternion


head = next((n for n in names if n.lower() == "head"), None)
neck = next((n for n in names if n.lower().startswith("neck")), None)
if neck:
    nod(neck, TILT * 0.4)
bpy.context.view_layer.update()
if head:
    nod(head, TILT * 0.6)
bpy.context.view_layer.update()

# where the face went: the rest-space point carried by the head bone's pose
pb = arm.pose.bones[head]
mw = arm.matrix_world @ pb.matrix @ pb.bone.matrix_local.inverted() @ arm.matrix_world.inverted()
face = mw @ FACE
M.log("face", tuple(round(v, 3) for v in face))

# lights: near darkness, ember under-light from low in front, ember rim low behind on his right, cold rim high behind
for o in list(bpy.data.objects):
    if o.type == 'LIGHT':
        bpy.data.objects.remove(o, do_unlink=True)
w = bpy.data.worlds.new("black"); w.use_nodes = True
bg = w.node_tree.nodes.get("Background"); bg.inputs[0].default_value = (0.25, 0.3, 0.45, 1); bg.inputs[1].default_value = 0.004
bpy.context.scene.world = w
U = HALF / 0.165                          # the light set-up is tuned at a half-size of 0.165 m


def P(right, back, up):                   # offsets from the face: right of frame, behind him, up
    return face + Vector((right, back, up)) * U


def spot(name, pos, col, watts, size=0.05, angle=60.0, blend=0.6, at=None):
    ld = bpy.data.lights.new(name, 'SPOT')
    ld.energy = watts * U * U; ld.color = col; ld.shadow_soft_size = size
    ld.spot_size = math.radians(angle); ld.spot_blend = blend
    ob = bpy.data.objects.new(name, ld)
    ob.location = pos
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = ((at or face) - pos).to_track_quat('-Z', 'Y')
    bpy.context.scene.collection.objects.link(ob)


E = [float(x) for x in os.environ.get("STARE_LIGHTS", "6,22,9,10").split(",")]
# the key: a low ember light in front and below, catching cheekbones, teeth and the underside of the brow ridge only
spot("under", P(-0.10, -0.60, -0.55), (1.0, 0.46, 0.18), E[0], 0.08)
# ember rim low behind on his right (frame left), cold rim high behind on his left: they draw skull, crown and nails
spot("ember_rim", P(-0.45, 0.42, -0.15), (1.0, 0.33, 0.07), E[1])
spot("cold_rim", P(0.45, 0.45, 0.42), (0.40, 0.53, 0.90), E[2])
spot("crown_rim", P(0.0, 0.55, 0.75), (0.45, 0.55, 0.85), E[3], at=face + Vector((0, 0, 0.18)))

S.setup_render(RES, RES, samples=64)
sc = bpy.context.scene
try:
    sc.view_settings.view_transform = 'Standard'
except Exception:
    pass
cam = S.make_camera()
c = np.array(face) - np.array((0.0, 0.0, HALF * float(os.environ.get('STARE_DROP', '0.16'))))
S.frame(cam, [], 0.0, PITCH, 85.0, pts=np.array([c + HALF, c - HALF]), margin=1.0)
S.render(OUT)
M.log("RENDER", OUT)
