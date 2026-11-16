"""Render a character playing a sequence of animations to numbered PNG frames (for animated previews).

  run_blender.ps1 -A @('-b','--factory-startup','--python','Y:\\tools\\blender\\anim_frames.py','--',
                       '<char id>', '<out dir>', '<fps>', '<size px>', '<weapon tier>', 'Action[:seconds|:xN]', ...)
Each clip is 'ActionName' (played once), 'ActionName:1.5' (looped/cut to 1.5 s) or 'ActionName:x2' (played twice).
"""
import bpy, sys, os, math
sys.path.insert(0, r"Y:\tools\blender\heroes")
from rigio import *
import numpy as np


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


def main():
    a = sys.argv[sys.argv.index('--') + 1:]
    cid, out, fps, size, tier = a[0], a[1], int(a[2]), int(a[3]), int(a[4])
    clips = a[5:]
    os.makedirs(out, exist_ok=True)
    reset_scene()
    acts = load_actions()
    arm, meshes = load_char(cid)
    for o in meshes:
        if o.name.startswith("wpn_"):
            vis = tier != 0 and o.name.split("_")[1] == str(tier)
            o.hide_render = not vis
            o.hide_viewport = not vis
    setup_render(size, size, samples=16)
    setup_stage()
    cam = make_camera()
    set_pose(arm, None)
    mn, mx = world_bbox([o for o in meshes if not o.name.startswith("wpn_")])
    h = float(mx[2])
    aim_camera(cam, (0.0, 0.0, h * 0.52), 30, 14, max(5.4, h * 3.0), lens=62)
    scene_fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
    n = 0
    for clip in clips:
        name, _, arg = clip.partition(':')
        if name not in acts:
            log("missing action", name)
            continue
        act = acts[name]
        f0, f1 = act.frame_range
        dur = max(0.05, (f1 - f0) / scene_fps)
        total = dur
        if arg.startswith('x'):
            total = dur * float(arg[1:])
        elif arg:
            total = float(arg)
        frames = max(1, int(round(total * fps)))
        for i in range(frames):
            t = (i / fps) / dur
            t = t - math.floor(t) if total > dur + 1e-4 or 'Loop' in name else min(t, 1.0)
            set_pose(arm, act, t)
            render_to(os.path.join(out, "f_%04d.png" % n))
            n += 1
        log("clip", name, "dur %.2fs" % dur, "frames", frames)
    log("FRAMES", n)


main()
