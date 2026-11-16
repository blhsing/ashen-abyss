"""Exploration renders of the source assets (base bodies, hair, mannequin poses)."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rigio import *

OUTD = r"Y:\shots\chars\_explore"
os.makedirs(OUTD, exist_ok=True)
what = sys.argv[sys.argv.index('--') + 1:]

if 'bodies' in what:
    for kind, fn in (("male", "Superhero_Male_FullBody.gltf"), ("female", "Superhero_Female_FullBody.gltf")):
        reset_scene()
        arm = import_rig()
        parts = import_skinned(os.path.join(SRC, "base", fn), arm)
        log(kind, list(parts))
        setup_render(1000, 1300)
        setup_stage()
        cam = make_camera()
        aim_camera(cam, (0, 0, 0.95), 25, 8, 6.2, lens=70)
        render_to(os.path.join(OUTD, "base_%s_full.png" % kind))
        setup_render(900, 900)
        aim_camera(cam, (0, 0, 1.66), 25, 5, 1.6, lens=70)
        render_to(os.path.join(OUTD, "base_%s_face.png" % kind))
        acts = load_actions()
        setup_render(1000, 1300)
        for an, t in (("Idle_Loop", 0.5), ("Jog_Fwd_Loop", 0.3), ("Sword_Regular_A", 0.6)):
            set_pose(arm, acts[an], t)
            aim_camera(cam, (0, 0, 0.95), 25, 8, 6.2, lens=70)
            render_to(os.path.join(OUTD, "base_%s_%s.png" % (kind, an)))
        set_pose(arm, None)
        hairs = ["Hair_Beard", "Hair_Buns", "Hair_Buzzed", "Hair_BuzzedFemale", "Hair_Long", "Hair_SimpleParted"]
        setup_render(700, 700)
        for h in hairs:
            hp = import_skinned(os.path.join(SRC, "hair", h + ".gltf"), arm)
            for o in hp.values():
                log(" hair", h, o.name, len(o.data.vertices), [m.name for m in o.data.materials],
                    [g.name for g in o.vertex_groups if any(True for v in o.data.vertices[:50] for gg in v.groups if gg.group == g.index and gg.weight > 0.01)][:6])
            aim_camera(cam, (0, 0, 1.64), 35, 12, 2.2, lens=70)
            render_to(os.path.join(OUTD, "hair_%s_%s.png" % (kind, h)))
            for o in hp.values():
                bpy.data.objects.remove(o, do_unlink=True)

if 'poses' in what:
    reset_scene()
    arm = import_rig()
    # mannequin for hand reference
    objs_before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(SRC, "UAL1.glb"))
    new = [o for o in bpy.data.objects if o not in objs_before]
    arm2 = next(o for o in new if o.type == 'ARMATURE')
    man = next(o for o in new if o.type == 'MESH' and o.parent is arm2)
    rebind(man, arm)
    for o in new:
        if o is not man:
            bpy.data.objects.remove(o, do_unlink=True)
    purge_actions()
    acts = load_actions()
    setup_render(900, 900)
    setup_stage()
    cam = make_camera()
    shots = [("Sword_Idle", 0.5), ("Sword_Regular_A", 0.5), ("Sword_Attack", 0.4), ("Spell_Simple_Idle_Loop", 0.5),
             ("Spell_Simple_Shoot", 0.4), ("Pistol_Aim_Neutral", 0.5), ("Pistol_Idle_Loop", 0.5), ("Idle_Loop", 0.5),
             ("Idle_Lantern_Loop", 0.5), ("Idle_Torch_Loop", 0.5), ("Jog_Fwd_Loop", 0.3), ("Roll", 0.5)]
    for name, t in shots:
        set_pose(arm, acts[name], t)
        mn, mx = world_bbox([man])
        c = (mn + mx) / 2
        for yaw in (25, -65):
            aim_camera(cam, c, yaw, 10, 5.2, lens=70)
            render_to(os.path.join(OUTD, "pose_%s_y%d.png" % (name, yaw)))
        # report hand frames
        for hb in ("hand_r", "hand_l"):
            pb = arm.pose.bones[hb]
            log("POSE", name, hb, "head", tuple(round(v, 3) for v in pb.head), "y-axis", tuple(round(v, 3) for v in pb.y_axis), "z-axis", tuple(round(v, 3) for v in pb.z_axis))
