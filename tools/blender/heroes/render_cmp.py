"""Close-up look-dev renders under raking light (HDRI + side key), for BEFORE/AFTER comparisons.

blender -b --factory-startup --python render_cmp.py -- <glb path> <id> <tag> [shot names...]
Writes Y:\\shots\\chars\\_cmp\\<id>_<shot>_<tag>.png
"""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *
import numpy as np

# shot = (name, action, t, weapon tier, bone, offset, yaw, pitch, dist, lens)
CMP = {
    "hero_emberblade": [("plate", "Sword_Idle", 0.5, 3, "spine_03", (0.0, 0.0, 0.04), 30, 6, 1.55, 70),
                        ("fur", "Sword_Idle", 0.5, 3, "neck_01", (0.0, 0.0, -0.02), 150, 14, 1.00, 70),
                        ("tabard", "Idle_Loop", 0.5, 0, "pelvis", (0.0, 0.0, -0.20), 6, 2, 1.30, 70),
                        ("helm", "Sword_Idle", 0.5, 3, "Head", (0.0, 0.0, 0.10), 35, 4, 0.75, 70)],
    "hero_astromancer": [("face", "Idle_Loop", 0.5, 3, "Head", (0.0, 0.0, 0.085), 22, 3, 0.58, 80),
                         ("hair", "Idle_Loop", 0.5, 3, "Head", (0.0, 0.0, 0.15), -42, 30, 0.72, 80),
                         ("robes", "Idle_Loop", 0.5, 3, "spine_01", (0.0, 0.0, -0.06), 26, 4, 1.20, 70)],
    "hero_forgebody": [("skin", "Idle_Loop", 0.5, 3, "spine_03", (-0.06, 0.0, 0.02), -28, 6, 0.85, 70),
                       ("scars", "Idle_Loop", 0.5, 3, "upperarm_r", (0.0, 0.0, -0.06), -58, 8, 0.60, 70),
                       ("wraps", "Idle_Loop", 0.5, 3, "lowerarm_l", (0.0, 0.0, -0.10), 48, 10, 0.62, 70),
                       ("face", "Idle_Loop", 0.5, 3, "Head", (0.0, 0.0, 0.085), 20, 3, 0.58, 80)],
    "hero_wildsoul": [("fur", "Sword_Idle", 0.5, 3, "Head", (0.0, 0.0, 0.04), 32, 8, 0.95, 70),
                      ("bark", "Sword_Idle", 0.5, 3, "upperarm_r", (0.0, 0.0, 0.10), -52, 30, 0.90, 70),
                      ("hide", "Idle_Loop", 0.5, 0, "spine_01", (0.0, 0.0, -0.06), 10, 4, 1.25, 70)],
    "npc_villager_f": [("skin", "Idle_Loop", 0.5, 0, "Head", (0.0, 0.0, 0.085), 18, 3, 0.58, 80),
                       ("clothes", "Idle_Loop", 0.5, 0, "spine_02", (0.0, 0.0, 0.04), 12, 5, 1.45, 70),
                       ("skirt", "Idle_Loop", 0.5, 0, "pelvis", (0.0, 0.0, -0.28), 16, 3, 1.35, 70)],
    "npc_villager_m": [("skin", "Idle_Loop", 0.5, 0, "Head", (0.0, 0.0, 0.085), 18, 3, 0.58, 80),
                       ("clothes", "Idle_Loop", 0.5, 0, "spine_02", (0.0, 0.0, 0.02), 26, 5, 1.10, 70)],
    "hero_hunter": [("upper", "Pistol_Idle_Loop", 0.5, 3, "spine_03", (0.0, 0.0, 0.06), 28, 6, 1.05, 70),
                    ("legs", "Pistol_Idle_Loop", 0.5, 3, "pelvis", (0.0, 0.0, -0.25), 24, 4, 1.20, 70)],
    "hero_soulbinder": [("upper", "Idle_Loop", 0.5, 3, "spine_03", (0.0, 0.0, 0.08), 26, 6, 1.05, 70),
                        ("robes", "Idle_Loop", 0.5, 3, "pelvis", (0.0, 0.0, -0.2), 24, 4, 1.20, 70)],
    "hero_pactbinder": [("upper", "Idle_Loop", 0.5, 3, "spine_03", (0.0, 0.0, 0.08), 26, 6, 1.05, 70),
                        ("coat", "Idle_Loop", 0.5, 3, "pelvis", (0.0, 0.0, -0.2), 24, 4, 1.20, 70)],
}
GAME = ("game", None, 0.5, 1, None, (0, 0, 0.85), 35, 52, 20.0, 300)
IDLE_FOR = {"hero_emberblade": "Sword_Idle", "hero_astromancer": "Spell_Simple_Idle_Loop", "hero_hunter": "Pistol_Idle_Loop",
            "hero_soulbinder": "Spell_Simple_Idle_Loop", "hero_wildsoul": "Sword_Idle", "hero_forgebody": "Idle_Loop",
            "hero_pactbinder": "Spell_Simple_Idle_Loop"}


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    path, cid, tag = args[0], args[1], args[2]
    want = set(args[3:])
    out = os.path.join(SHOTS, "_cmp")
    os.makedirs(out, exist_ok=True)
    reset_scene()
    acts = load_actions()
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
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
    shots = list(CMP.get(cid, [])) + [GAME]
    for (name, act, t, tier, bone, off, yaw, pitch, dist, lens) in shots:
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
        render_to(os.path.join(out, "%s_%s_%s.png" % (cid, name, tag)))


main()
