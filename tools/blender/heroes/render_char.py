"""Preview renders of an exported character .glb (end-to-end check of the file the game will load).

blender -b --factory-startup --python render_char.py -- <id> [shot names...]
Writes Y:\\shots\\chars\\<id>_<shot>.png and a montage <id>_sheet.png
"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import mats
import numpy as np

# shot = (name, action or None, t, weapon tier, camera preset)
MELEE = [("rest", None, 0, 3, "rest"), ("game", "Sword_Idle", 0.5, 1, "game"), ("idle", "Idle_Loop", 0.5, 2, "front"),
         ("attack", "Sword_Regular_A", 0.55, 3, "front"), ("jog", "Jog_Fwd_Loop", 0.3, 2, "side"),
         ("roll", "Roll", 0.45, 1, "front"), ("back", "Sword_Idle", 0.5, 3, "back"), ("wpn", None, 0, 0, "wpn")]
CASTER = [("rest", None, 0, 3, "rest"), ("game", "Spell_Simple_Idle_Loop", 0.5, 1, "game"), ("idle", "Idle_Loop", 0.5, 2, "front"),
          ("cast", "Spell_Simple_Shoot", 0.45, 3, "front"), ("jog", "Jog_Fwd_Loop", 0.3, 2, "side"),
          ("roll", "Roll", 0.45, 1, "front"), ("back", "Spell_Simple_Idle_Loop", 0.5, 3, "back"), ("wpn", None, 0, 0, "wpn")]
HUNTER = [("rest", None, 0, 3, "rest"), ("game", "Pistol_Idle_Loop", 0.5, 1, "game"), ("idle", "Idle_Loop", 0.5, 2, "front"),
          ("aim", "Pistol_Aim_Neutral", 0.5, 3, "front"), ("jog", "Jog_Fwd_Loop", 0.3, 2, "side"),
          ("roll", "Roll", 0.45, 1, "front"), ("back", "Pistol_Idle_Loop", 0.5, 3, "back"), ("wpn", None, 0, 0, "wpn")]
FIST = [("rest", None, 0, 3, "rest"), ("game", "Idle_Loop", 0.5, 1, "game"), ("jab", "Punch_Jab", 0.45, 2, "front"),
        ("cross", "Punch_Cross", 0.45, 3, "front"), ("hook", "Melee_Hook", 0.5, 3, "side"), ("jog", "Jog_Fwd_Loop", 0.3, 2, "side"),
        ("back", "Idle_Loop", 0.5, 3, "back"), ("wpn", None, 0, 0, "wpn")]
SHIELD = [("rest", None, 0, 3, "rest"), ("game", "Idle_Shield_Loop", 0.5, 1, "game"), ("block", "Sword_Block", 0.5, 2, "front"),
          ("attack", "Sword_Regular_A", 0.55, 3, "front"), ("dash", "Shield_Dash", 0.5, 2, "side"),
          ("oneshot", "Shield_OneShot", 0.5, 3, "front"), ("jog", "Jog_Fwd_Loop", 0.3, 1, "side"), ("back", "Idle_Shield_Loop", 0.5, 3, "back"),
          ("wpn", None, 0, 0, "wpn")]
BEAST = [("rest", None, 0, 0, "rest"), ("game", "Idle_Loop", 0.5, 0, "game"), ("hook", "Melee_Hook", 0.5, 0, "front"),
         ("cross", "Punch_Cross", 0.45, 0, "front"), ("scratch", "Zombie_Scratch", 0.5, 0, "front"), ("jog", "Jog_Fwd_Loop", 0.3, 0, "side"),
         ("back", "Idle_Loop", 0.5, 0, "back"), ("roll", "Roll", 0.45, 0, "front")]
NPC = [("rest", None, 0, 0, "rest"), ("game", "Idle_Loop", 0.5, 0, "game"), ("talk", "Idle_Talking_Loop", 0.4, 0, "front"),
       ("walk", "Walk_Loop", 0.3, 0, "side"), ("back", "Idle_Loop", 0.5, 0, "back"), ("interact", "Interact", 0.5, 0, "front")]
SHOTSETS = {
    "hero_emberblade": MELEE, "hero_astromancer": CASTER, "hero_hunter": HUNTER, "hero_soulbinder": CASTER,
    "hero_wildsoul": MELEE, "hero_forgebody": FIST, "hero_lampknight": SHIELD, "hero_pactbinder": CASTER,
    "form_bear": BEAST, "form_wolf": BEAST,
    "npc_elder": [("rest", None, 0, 0, "rest"), ("game", "Idle_Torch_Loop", 0.5, 0, "game"), ("torch", "Idle_Torch_Loop", 0.5, 0, "front"),
                  ("talk", "Idle_Talking_Loop", 0.4, 0, "front"), ("walk", "Walk_Loop", 0.3, 0, "side"), ("back", "Idle_Torch_Loop", 0.5, 0, "back")],
    "npc_smith": NPC[:3] + [("chop", "TreeChopping_Loop", 0.4, 0, "front"), ("fold", "Idle_FoldArms_Loop", 0.5, 0, "front"), ("back", "Idle_Loop", 0.5, 0, "back")],
}
IDLE_FOR = {"hero_emberblade": "Sword_Idle", "hero_astromancer": "Spell_Simple_Idle_Loop", "hero_hunter": "Pistol_Idle_Loop",
            "hero_soulbinder": "Spell_Simple_Idle_Loop", "hero_wildsoul": "Sword_Idle", "hero_forgebody": "Idle_Loop",
            "hero_lampknight": "Idle_Shield_Loop", "hero_pactbinder": "Spell_Simple_Idle_Loop", "npc_elder": "Idle_Torch_Loop"}


def load_char(cid):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(OUT, cid + ".glb"))
    objs = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in objs if o.type == 'ARMATURE')
    meshes = [o for o in objs if o.type == 'MESH' and o.parent is arm]
    for o in objs:
        if o is not arm and o not in meshes:
            bpy.data.objects.remove(o, do_unlink=True)
    for pb in arm.pose.bones:
        pb.custom_shape = None
    return arm, meshes


def show_tier(meshes, tier):
    for o in meshes:
        if o.name.startswith("wpn_"):
            vis = (tier != 0) and o.name.split("_")[1] == str(tier)
            if tier == -1:
                vis = True
            o.hide_render = not vis
            o.hide_viewport = not vis


def frame_cam(cam, meshes, preset, w, h):
    mn, mx = world_bbox([o for o in meshes if not o.hide_render])
    c = (mn + mx) / 2
    size = float(np.linalg.norm(mx - mn))
    if preset == "game":
        # true game framing: pitch 52 deg down, 20 m away; narrow lens so the figure fills ~55 % of the frame
        aim_camera(cam, (c[0], c[1], 0.85), 35, 52, 20.0, lens=165.0)
        return
    if preset == "rest":
        aim_camera(cam, (0, 0, 0.98), 28, 10, 4.4, lens=62)
        return
    if preset == "close":
        h = ARM_OBJ.pose.bones["Head"].head
        aim_camera(cam, (h[0], h[1], h[2] - 0.22), 24, 8, 2.2, lens=62)
        return
    yaw = {"front": 28, "side": 62, "back": 207}.get(preset, 28)
    dist = max(3.2, size * 1.9)
    aim_camera(cam, c, yaw, 12, dist, lens=62)


def montage(paths, out, cols=4, cell=(560, 640)):
    tiles = []
    for p in paths:
        a = mats.read_img(p, (cell[0], cell[1]))[..., :3]
        tiles.append(a)
    rows = (len(tiles) + cols - 1) // cols
    can = np.zeros((rows * cell[1], cols * cell[0], 3), dtype=np.float32)
    for i, a in enumerate(tiles):
        r, c = divmod(i, cols)
        y0 = (rows - 1 - r) * cell[1]
        can[y0:y0 + cell[1], c * cell[0]:(c + 1) * cell[0]] = a
    mats.save_rgb(can, out, 90)


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    cid = args[0]
    want = set(args[1:])
    reset_scene()
    acts = load_actions()
    arm, meshes = load_char(cid)
    global ARM_OBJ
    ARM_OBJ = arm
    setup_render(840, 960, samples=24)
    setup_stage()
    cam = make_camera()
    shots = list(SHOTSETS.get(cid, NPC))
    idle = IDLE_FOR.get(cid, "Idle_Loop")
    tier3 = 3 if cid.startswith("hero_") else 0
    shots.insert(1, ("close", idle, 0.5, tier3, "close"))
    custom = [a for a in want if ":" in a]          # name:Action:t:tier:preset
    if custom:
        shots = []
        for cs in custom:
            nm, act, t, tier, preset = cs.split(":")
            shots.append((nm, None if act in ("", "None") else act, float(t), int(tier), preset))
        want = {s_[0] for s_ in shots}
    done = []
    wpns = sorted(o.name for o in meshes if o.name.startswith("wpn_"))
    for (name, act, t, tier, preset) in shots:
        if want and name not in want:
            continue
        if name == "wpn":
            if not wpns:
                continue
            set_pose(arm, None)
            for o in meshes:
                o.hide_render = not o.name.startswith("wpn_")
                if o.name.startswith("wpn_"):
                    k = int(o.name.split("_")[1])
                    o.location = (0.0, 0.0, 0.0)
                    o.delta_location = (0.0, 0.0, -0.55 * (k - 1))
            mn, mx = world_bbox([o for o in meshes if not o.hide_render])
            c = (mn + mx) / 2
            size = float(np.linalg.norm(mx - mn))
            aim_camera(cam, c, 68, 22, max(1.6, size * 1.5), lens=62)
            render_to(os.path.join(SHOTS_DIR, "%s_%s.png" % (cid, name)))
            for o in meshes:
                o.delta_location = (0, 0, 0)
                o.hide_render = False
        else:
            show_tier(meshes, tier)
            set_pose(arm, acts[act] if act else None, t)
            frame_cam(cam, meshes, preset, 840, 960)
            render_to(os.path.join(SHOTS_DIR, "%s_%s.png" % (cid, name)))
        done.append(os.path.join(SHOTS_DIR, "%s_%s.png" % (cid, name)))
    if not want or len(done) > 3:
        montage(done, os.path.join(SHOTS_DIR, "%s_sheet.jpg" % cid))
        log("montage", os.path.join(SHOTS_DIR, "%s_sheet.jpg" % cid))


SHOTS_DIR = SHOTS
ARM_OBJ = None
main()
