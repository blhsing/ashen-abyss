"""Hand-mount check against today's heroes (Blender side; the in-game mounting is RENDER's):

    blender -b --python mount_test.py -- <out name> <shot> [<shot> ...]
    shot = hero:action:t:preset:item[,item...]     item = <model id>@<mount>[@tilt/lean]
           preset = front | side | back | game      mount = hand_r | hand_l | forearm_l

Each gear .glb is placed on the posed bone with the bone-local frames of mount_frames.json (the numbers published in
PROGRESS_ARTW.md); the hero's own baked wpn_* meshes are hidden. Writes Y:\\shots\\gear\\weapons\\<out name>.jpg
"""
import sys, os, json, math
HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from gearlib import *
import sheet

with open(os.path.join(HERE, "mount_frames.json")) as f:
    FR = {k: np.array(v) for k, v in json.load(f).items()}
BONE = {"hand_r": ("hand_r", "grip_r"), "hand_l": ("hand_l", "grip_l"), "forearm_l": ("lowerarm_l", "shield_lowerarm_l")}
CHARS = r"Y:\game\assets\chars"


def load_char(cid):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(CHARS, cid + ".glb"))
    objs = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in objs if o.type == 'ARMATURE')
    meshes = [o for o in objs if o.type == 'MESH' and o.parent is arm]
    for o in objs:
        if o is not arm and o not in meshes:
            bpy.data.objects.remove(o, do_unlink=True)
    for pb in arm.pose.bones:
        pb.custom_shape = None
    for o in meshes:
        if o.name.startswith("wpn_"):
            o.hide_render = True
    return arm, meshes


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    out, shots = args[0], args[1:]
    tmp = os.path.join(GEAR_SHOTS, "_tmp")
    os.makedirs(tmp, exist_ok=True)
    cells = []
    for si, sh in enumerate(shots):
        cid, act, t, preset, items = sh.split(":")
        reset_scene()
        acts = load_actions()
        arm, meshes = load_char(cid)
        set_pose(arm, acts[act] if act not in ("", "None") else None, float(t))
        bpy.context.view_layer.update()
        gear = []
        for it in items.split(","):
            parts = it.split("@")
            mid, mount = parts[0], parts[1]
            bone, key = BONE[mount]
            M = np.array([list(r) for r in arm.matrix_world]) @ np.array([list(r) for r in arm.pose.bones[bone].matrix]) @ FR[key]
            if len(parts) > 2:
                tilt, lean = [float(v) for v in parts[2].split("/")]
                M = M @ R('Y', -tilt) @ R('X', lean if mount != "hand_l" else -lean)
            for o in sheet.load_glb(mid):
                o.matrix_world = Matrix(M.tolist()) @ o.matrix_world
                gear.append(o)
        setup_render(700, 800, samples=24)
        setup_stage()
        cam = make_camera()
        vis = [o for o in meshes if not o.hide_render] + gear
        mn, mx = world_bbox(vis)
        c = (mn + mx) / 2
        size = float(np.linalg.norm(mx - mn))
        if preset == "game":
            aim_camera(cam, (c[0], c[1], 0.85), 35, 52, 20.0, lens=165.0)
        else:
            yaw = {"front": 28, "side": 62, "back": 207, "left": -50}.get(preset, 28)
            aim_camera(cam, c, yaw, 12, max(3.2, size * 1.9), lens=62)
        p = os.path.join(tmp, "mount_%s_%d.png" % (out, si))
        render_to(p)
        cells.append(mats.read_img(p)[..., :3])
    rows = [sheet._hcat(cells[i:i + 4]) for i in range(0, len(cells), 4)]
    mats.save_rgb(sheet._stack(rows), os.path.join(GEAR_SHOTS, out + ".jpg"), 88)
    log("MOUNT SHEET", os.path.join(GEAR_SHOTS, out + ".jpg"))


main()
