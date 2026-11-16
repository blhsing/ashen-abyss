"""Close-ups under raking light for the town / lampknight / spirit-form batch (same stage as render_cmp.py, own shot table).

blender -b --factory-startup --python twn_cmp.py -- <id> [shot names...]
Writes Y:\\shots\\chars\\_cmp\\<id>_<shot>_after.png
"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np

# shot = (name, action, t, weapon tier, bone, offset, yaw, pitch, dist, lens)
CMP = {
    "hero_lampknight": [("helm", "Idle_Loop", 0.5, 0, "Head", (0.0, 0.0, 0.07), 26, 4, 0.78, 70),
                        ("plate", "Idle_Loop", 0.5, 0, "spine_03", (0.10, 0.0, 0.04), 52, 8, 1.15, 70),
                        ("tabard", "Idle_Loop", 0.5, 0, "pelvis", (0.0, 0.0, -0.16), 14, 2, 1.30, 70),
                        ("shield", "Idle_Shield_Loop", 0.5, 2, "hand_l", (0.05, 0.0, -0.05), 8, 4, 1.15, 70),
                        ("hip", "Idle_Loop", 0.5, 0, "pelvis", (0.10, 0.0, -0.05), 48, 6, 1.25, 70)],
    "npc_elder": [("face", "Idle_Loop", 0.5, 0, "Head", (0.0, 0.0, 0.07), 20, 3, 0.62, 80),
                  ("robe", "Idle_Loop", 0.5, 0, "spine_02", (0.0, 0.0, 0.0), 22, 5, 1.25, 70),
                  ("hem", "Idle_Loop", 0.5, 0, "pelvis", (0.0, 0.0, -0.50), 16, 3, 1.35, 70),
                  ("lantern", "Idle_Lantern_Loop", 0.5, 0, "hand_r", (0.0, 0.0, -0.16), -14, 4, 0.85, 70)],
    "npc_smith": [("face", "Idle_Loop", 0.5, 0, "Head", (0.0, 0.0, 0.07), 20, 3, 0.62, 80),
                  ("apron", "Idle_Loop", 0.5, 0, "spine_02", (0.0, 0.0, -0.02), 18, 5, 1.25, 70),
                  ("arm", "Idle_Loop", 0.5, 0, "lowerarm_r", (0.0, 0.0, 0.0), -40, 8, 0.75, 70)],
    "npc_merchant": [("face", "Idle_Loop", 0.5, 0, "Head", (0.0, 0.0, 0.08), 20, 3, 0.70, 80),
                     ("cloak", "Idle_Loop", 0.5, 0, "spine_02", (0.0, 0.0, 0.02), 24, 5, 1.30, 70),
                     ("pack", "Idle_Loop", 0.5, 0, "spine_03", (0.0, 0.0, 0.02), 150, 12, 1.30, 70)],
    "npc_occultist": [("mask", "Idle_Loop", 0.5, 0, "Head", (0.0, 0.0, 0.07), 18, 3, 0.62, 80),
                      ("robes", "Idle_Loop", 0.5, 0, "spine_02", (0.0, 0.0, 0.0), 24, 5, 1.25, 70),
                      ("charms", "Idle_Loop", 0.5, 0, "pelvis", (0.0, 0.0, -0.10), 12, 3, 1.05, 70)],
    "form_bear": [("head", "Zombie_Idle_Loop", 0.5, 0, "Head", (0.0, 0.0, 0.05), 28, 4, 1.00, 70),
                  ("fur", "Zombie_Idle_Loop", 0.5, 0, "spine_03", (0.12, 0.0, 0.0), 55, 8, 1.25, 70),
                  ("claws", "Zombie_Idle_Loop", 0.5, 0, "hand_l", (0.0, 0.0, -0.02), 40, 8, 0.80, 70)],
    "form_wolf": [("head", "Zombie_Idle_Loop", 0.5, 0, "Head", (0.0, 0.0, 0.03), 40, 4, 0.95, 70),
                  ("fur", "Zombie_Idle_Loop", 0.5, 0, "spine_03", (0.10, 0.0, 0.0), 55, 8, 1.20, 70),
                  ("legs", "Idle_Loop", 0.5, 0, "pelvis", (0.0, 0.0, -0.45), 60, 4, 1.50, 70)],
}
GAME = ("game", None, 0.5, 1, None, (0, 0, 0.85), 35, 52, 20.0, 300)
IDLE_FOR = {"hero_lampknight": "Idle_Shield_Loop", "npc_elder": "Idle_Lantern_Loop", "npc_smith": "Idle_FoldArms_Loop",
            "npc_merchant": "Idle_Talking_Loop", "npc_occultist": "Idle_Loop", "form_bear": "Zombie_Idle_Loop", "form_wolf": "Idle_Loop"}


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    cid = args[0]
    want = set(args[1:])
    out = os.path.join(SHOTS, "_cmp")
    os.makedirs(out, exist_ok=True)
    reset_scene()
    acts = load_actions()
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
    setup_render(840, 960, samples=32)
    setup_stage()
    cam = make_camera()
    for (name, act, t, tier, bone, off, yaw, pitch, dist, lens) in list(CMP.get(cid, [])) + [GAME]:
        if want and name not in want:
            continue
        if name == "game":
            act = IDLE_FOR.get(cid, "Idle_Loop")
            tier = 1 if cid.startswith("hero_") else 0
        for o in meshes:
            if o.name.startswith("wpn_"):
                vis = (tier != 0) and o.name.split("_")[1] == str(tier)
                o.hide_render = not vis
                o.hide_viewport = not vis
        set_pose(arm, acts[act] if act else None, t)
        if bone:
            h = arm.matrix_world @ arm.pose.bones[bone].head
            tgt = (h[0] + off[0], h[1] + off[1], h[2] + off[2])
        else:
            mn, mx = world_bbox([o for o in meshes if not o.hide_render])
            c = (mn + mx) / 2
            tgt = (c[0], c[1], off[2])
        aim_camera(cam, tgt, yaw, pitch, dist, lens=lens)
        aim_key(yaw)
        render_to(os.path.join(out, "%s_%s_after.png" % (cid, name)))
        log("rendered", name)


main()
