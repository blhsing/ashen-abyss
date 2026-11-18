"""Review sheet per creature, bigger and more complete than the family tiles (one Blender run for several ids):
   blender -b --factory-startup --python asm_review.py -- <id> [<id> ...] [--res=420] [--atk=Clip:t] [--src=<dir>]
Writes Y:\\shots\\chars\\asm\\review_<id>.png, 4 x 2 tiles:
   game camera (idle, engine scale) | front 3/4 idle | back 3/4 idle | side idle
   attack early                     | attack late (other side) | walk | chest + head close-up under raking light (rest pose)
(always through run_blender.ps1; see asm_review.ps1)"""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M, stage as S
import asm_specs as AS

args = sys.argv[sys.argv.index('--') + 1:]
ids = [a for a in args if not a.startswith('--')]


def flag(name, default):
    for f in args:
        if f.startswith('--' + name + '='):
            return f.split('=', 1)[1]
    return default


RES = int(flag('res', '420'))
SRC = flag('src', '')
ATK = flag('atk', '')
OUT = os.path.join(M.SHOT_DIR, 'asm')
os.makedirs(OUT, exist_ok=True)

for cid in ids:
    try:
        _, scale, poses, path = AS.member(cid)
    except Exception:
        scale, poses, path = 1.0, list(AS.POSES['sword']), os.path.join(AS.CHARS, cid + ".glb")
    if SRC:
        path = os.path.join(SRC, cid + ".glb")
    if not os.path.isfile(path):
        M.log("REVIEW MISSING", path)
        continue
    M.reset_scene()
    acts = S.load_actions()
    arm, meshes = S.import_creature(path)
    arm.scale = (scale, scale, scale)
    S.setup_render(RES, RES, samples=24)
    S.setup_stage()
    cam = S.make_camera()

    def pose(s, dt=0.0):
        a, _, t = s.partition(':')
        if a not in acts:
            M.log("MISSING action", a, "for", cid)
            S.set_pose(arm, None, 0)
        else:
            S.set_pose(arm, acts[a], min(0.98, max(0.0, float(t or 0.5) + dt)))

    tiles = []

    def shot(tag):
        p = os.path.join(M.WORK, "_rev_%s_%s.png" % (cid, tag))
        S.render(p)
        tiles.append(p)

    atk = ATK or poses[1]
    pose(poses[0])
    k = max(scale, 1.0)
    vol = np.array([[x * k, y * k, z * k] for x in (-0.75, 0.75) for y in (-0.75, 0.75) for z in (0.0, 2.2)])
    S.frame(cam, [], 35, 52, 100.0, pts=vol); shot('game')
    S.frame(cam, meshes, 28, 10); shot('front')
    S.frame(cam, meshes, 152, 14); shot('back')
    S.frame(cam, meshes, 90, 6); shot('side')
    pose(atk, -0.17); S.frame(cam, meshes, 30, 14); shot('atk1')
    pose(atk, 0.08); S.frame(cam, meshes, -50, 20); shot('atk2')
    pose(poses[2]); S.frame(cam, meshes, -40, 14); shot('walk')
    S.set_pose(arm, None, 0)
    for o in list(bpy.data.objects):
        if o.type == 'LIGHT':
            bpy.data.objects.remove(o, do_unlink=True)
    S.setup_rake_stage()
    c = np.array([0.0, -0.03, 1.50]) * scale
    r = 0.36 * scale
    box = np.array([c + (dx, dy, dz) for dx in (-r, r) for dy in (-r * 0.4, r * 0.4) for dz in (-r, r)])
    S.rake_lights(24, 6)
    S.frame(cam, [], 24, 6, 70.0, pts=box); shot('bust')
    out = os.path.join(OUT, "review_%s.png" % cid)
    S.combo(tiles, out, cols=4)
    M.log("REVIEW", out)
