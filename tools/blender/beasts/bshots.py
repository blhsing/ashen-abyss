"""Renders of the exported beast files (the .glb as the game gets it, embedded clips included).
  bb.ps1 bshots.py "<id|model> ... [--src=<dir of the .glb>] [--out=<work dir>] [--tile] [--game] [--strip] [--dbg] [--webp]"
default = tile + game + strip + dbg.  Outputs (work dir = out\\shots unless --out):
  <model>_tile.png   3/4 view, Idle           (family sheets are composed from these with Y:\\tools\\sheet.ps1)
  <model>_game.png   the game camera at true 1080p pixel size (52 deg down, 24 m; 512 px = 6.1 m)
  <model>_s0..s8.png strip tiles: Walk x3, Run x3, Attack1 hit, Attack2 hit, Death end  (bstrip.ps1 labels them)
  <model>_dbg1.jpg   Idle 2, Walk 4, Run 4, Hit 2;  <model>_dbg2.jpg  Attack1 3, Attack2 3, Death 3, Spawn 3
  <model>_webp\\f_*.png  frames for make_webp.py (all clips in a row; the floor scrolls at the stated speed)"""
import bpy, sys, os, math, json
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, r"Y:\tools\blender\monsters")
import monlib as M
import stage as ST
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
flags = [a for a in args if a.startswith('--')]
ids = [a if a.startswith('mon_') else 'mon_' + a for a in args if not a.startswith('--')]
SRC = r"Y:\game\assets\chars"
OUT = os.path.join(HERE, "out", "shots")
META = os.path.join(HERE, "out")
for f in flags:
    if f.startswith('--src='):
        SRC = f[6:]
    if f.startswith('--out='):
        OUT = f[6:]
    if f.startswith('--meta='):
        META = f[7:]
os.makedirs(OUT, exist_ok=True)
want = [f[2:] for f in flags if f[2:] in ('tile', 'game', 'strip', 'dbg', 'webp')]
if not want and not any(f.startswith('--view=') for f in flags):
    want = ['tile', 'game', 'strip', 'dbg']
YAW_TILE, PITCH_TILE = 38.0, 16.0
YAW_SIDE, PITCH_SIDE = 62.0, 14.0


def floor(speed_node=True):
    sc = bpy.context.scene
    me = bpy.data.meshes.new("floor")
    s = 60
    me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    fl = bpy.data.objects.new("stage_floor", me)
    m = bpy.data.materials.new("stage_floor")
    m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes.get("Principled BSDF")
    tc = nt.nodes.new('ShaderNodeTexCoord')
    mp = nt.nodes.new('ShaderNodeMapping'); mp.name = 'SCROLL'
    ck = nt.nodes.new('ShaderNodeTexChecker')
    ck.inputs['Scale'].default_value = 4.0          # 0.25 m squares
    ck.inputs['Color1'].default_value = (0.040, 0.040, 0.044, 1)
    ck.inputs['Color2'].default_value = (0.060, 0.060, 0.066, 1)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    nt.links.new(mp.outputs[0], ck.inputs['Vector'])
    nt.links.new(ck.outputs['Color'], b.inputs['Base Color'])
    b.inputs["Roughness"].default_value = 0.95
    me.materials.append(m)
    sc.collection.objects.link(fl)
    return fl, mp


def scroll(mp, dist):
    """the creature has travelled `dist` towards -Y: the floor pattern slides towards +Y"""
    mp.inputs['Location'].default_value = (0.013, -dist + 0.017, 0.0)


def actions_of(arm):
    acts = {}
    for a in bpy.data.actions:
        nm = a.name
        if len(nm) > 4 and nm[-4] == '.' and nm[-3:].isdigit():
            nm = nm[:-4]
        acts.setdefault(nm, a)
    return acts


def pose(arm, act, t):
    """t in seconds"""
    sc = bpy.context.scene
    ad = arm.animation_data or arm.animation_data_create()
    for tr in list(ad.nla_tracks):
        ad.nla_tracks.remove(tr)
    if ad.action is not act:
        ad.action = act
        try:
            if ad.action_slot is None and len(act.slots):
                ad.action_slot = act.slots[0]
        except Exception:
            pass
    f0, f1 = act.frame_range
    fr = min(f1, f0 + t * 30.0)
    sc.frame_set(int(math.floor(fr)), subframe=fr - math.floor(fr))
    bpy.context.view_layer.update()


def combo(tiles, cols, path, q=88):
    rows = (len(tiles) + cols - 1) // cols
    h, w = tiles[0].shape[:2]
    out = np.zeros((rows * h, cols * w, 4), dtype=np.float32); out[:, :, 3] = 1
    for i, t in enumerate(tiles):
        r = rows - 1 - i // cols
        c = i % cols
        out[r * h:(r + 1) * h, c * w:(c + 1) * w] = t
    im = bpy.data.images.new("combo", out.shape[1], out.shape[0], alpha=False)
    im.pixels.foreach_set(out.ravel())
    sc = bpy.context.scene
    sc.render.image_settings.file_format = 'JPEG'
    sc.render.image_settings.quality = q
    im.save_render(path, scene=sc)
    sc.render.image_settings.file_format = 'PNG'
    bpy.data.images.remove(im)


def one(model):
    path = os.path.join(SRC, model + ".glb")
    M.reset_scene()
    sc = ST.setup_render(700, 700, 16)
    ST.setup_stage(floor=False, key=3.4, rim=3.5, fill=0.6, hdri_strength=0.3)
    fl, mp = floor()
    scroll(mp, 0.0)
    arm, meshes = ST.import_creature(path)
    acts = actions_of(arm)
    meta = {}
    mpth = os.path.join(META, model + ".meta.json")
    if os.path.isfile(mpth):
        meta = json.load(open(mpth, encoding='utf8'))
    dur = {k: (a.frame_range[1] - a.frame_range[0]) / 30.0 for k, a in acts.items()}
    cam = ST.make_camera()
    hit1 = meta.get('attack1_hit_time', dur.get('Attack1', 1) * 0.5)
    hit2 = meta.get('attack2_hit_time', dur.get('Attack2', 1) * 0.5)
    ws = meta.get('walk_speed', 0.0); rs = meta.get('run_speed', 0.0)

    def pts_at(clip, t):
        pose(arm, acts[clip], t)
        return ST.posed_points(meshes)
    # ---- bounds: idle and "everything"
    P_idle = pts_at('Idle', 0.0)
    allp = [P_idle]
    for clip in ('Walk', 'Run', 'Attack1', 'Attack2', 'Death'):
        for k in (0.25, 0.5, 0.75, 1.0):
            q = pts_at(clip, dur[clip] * k)[::7]
            allp.append(q)
            if '--bbox' in flags:
                print("[beast] bbox", clip, k, q.min(0).round(2), q.max(0).round(2))
    for clip, t in (('Attack1', hit1), ('Attack2', hit2)):
        allp.append(pts_at(clip, t)[::7])
    P_all = np.vstack(allp)
    P_all = P_all[P_all[:, 2] > -0.02]
    P_loco = np.vstack([P_idle] + [pts_at(c, dur[c] * k)[::7] for c in ('Walk', 'Run') for k in (0.0, 0.25, 0.5, 0.75)])
    if 'tile' in want:
        pose(arm, acts['Idle'], 0.0)
        sc.render.resolution_x = sc.render.resolution_y = 700
        ST.frame(cam, meshes, YAW_TILE, PITCH_TILE, lens=70.0, margin=1.02, pts=P_idle)
        ST.render(os.path.join(OUT, model + "_tile.png"))
    for f in flags:
        if f.startswith('--view='):      # --view=name:yaw:pitch:tx:ty:tz:dist[:clip:time]
            q = f[7:].split(':')
            clip = q[7] if len(q) > 7 else 'Idle'
            pose(arm, acts[clip], float(q[8]) if len(q) > 8 else 0.0)
            sc.render.resolution_x = sc.render.resolution_y = 900
            ST.aim(cam, (float(q[3]), float(q[4]), float(q[5])), float(q[1]), float(q[2]), float(q[6]), lens=70.0)
            ST.render(os.path.join(OUT, "%s_v_%s.png" % (model, q[0])))
    if 'game' in want:
        pose(arm, acts['Idle'], 0.0)
        # the game camera (pitch 52 deg, 24 m, FOV 30 deg at 1080p = 84 px per metre): this 512 px tile shows 6.1 m, so
        # the creature has the pixel size it has on a 1080p screen
        sc.render.resolution_x = sc.render.resolution_y = 512
        ST.aim(cam, (0, 0, 0.6), 25.0, 52.0, 24.0, lens=141.0)
        ST.render(os.path.join(OUT, model + "_game.png"))
    if 'strip' in want:
        sc.render.resolution_x = sc.render.resolution_y = 460
        ST.frame(cam, meshes, YAW_SIDE, PITCH_SIDE, lens=70.0, margin=1.0, pts=P_all)
        shots = [('Walk', 0.0, ws), ('Walk', dur['Walk'] / 3, ws), ('Walk', dur['Walk'] * 2 / 3, ws),
                 ('Run', 0.0, rs), ('Run', dur['Run'] / 3, rs), ('Run', dur['Run'] * 2 / 3, rs),
                 ('Attack1', hit1, 0), ('Attack2', hit2, 0), ('Death', dur['Death'], 0)]
        for i, (clip, t, v) in enumerate(shots):
            pose(arm, acts[clip], t)
            scroll(mp, v * t)
            ST.render(os.path.join(OUT, "%s_s%d.png" % (model, i)))
        scroll(mp, 0.0)
    if 'dbg' in want:
        sc.render.resolution_x = sc.render.resolution_y = 450
        sc.eevee.taa_render_samples = 8
        plans = [[('Idle', (0.0, 0.5)), ('Walk', (0.0, 0.25, 0.5, 0.75)), ('Run', (0.0, 0.25, 0.5, 0.75)), ('Hit', (0.25, 0.6))],
                 [('Attack1', (0.5 * hit1 / dur['Attack1'], hit1 / dur['Attack1'], 0.5 + 0.5 * hit1 / dur['Attack1'])),
                  ('Attack2', (0.5 * hit2 / dur['Attack2'], hit2 / dur['Attack2'], 0.5 + 0.5 * hit2 / dur['Attack2'])),
                  ('Death', (0.3, 0.6, 1.0)), ('Spawn', (0.1, 0.4, 0.75))]]
        tmp = os.path.join(OUT, "_tmp_%s.png" % model)
        for pi, plan in enumerate(plans):
            ST.frame(cam, meshes, YAW_SIDE, PITCH_SIDE, lens=70.0, margin=0.98, pts=(P_loco if pi == 0 else P_all))
            tiles = []
            for clip, us in plan:
                v = ws if clip == 'Walk' else (rs if clip == 'Run' else 0.0)
                for u in us:
                    pose(arm, acts[clip], dur[clip] * u)
                    scroll(mp, v * dur[clip] * u)
                    ST.render(tmp)
                    tiles.append(ST.load_px(tmp))
            combo(tiles, 4, os.path.join(OUT, "%s_dbg%d.jpg" % (model, pi + 1)))
        try:
            os.remove(tmp)
        except OSError:
            pass
        scroll(mp, 0.0)
        sc.eevee.taa_render_samples = 16
    if 'webp' in want:
        d = os.path.join(OUT, model + "_webp")
        os.makedirs(d, exist_ok=True)
        for f in os.listdir(d):
            os.remove(os.path.join(d, f))
        sc.render.resolution_x = sc.render.resolution_y = 420
        sc.eevee.taa_render_samples = 8
        ST.frame(cam, meshes, 48.0, 18.0, lens=70.0, margin=1.0, pts=P_all)
        seq = [('Spawn', 1), ('Idle', 1), ('Walk', max(2, int(round(2.0 / dur['Walk'])))), ('Run', max(3, int(round(2.0 / dur['Run'])))),
               ('Attack1', 1), ('Attack2', 1), ('Hit', 1), ('Idle', 1), ('Death', 1)]
        fps = 20.0
        n = 0
        travelled = 0.0
        for clip, reps in seq:
            v = ws if clip == 'Walk' else (rs if clip == 'Run' else 0.0)
            T = dur[clip] * reps
            k = int(round(T * fps))
            for i in range(k):
                t = (i / fps) % dur[clip] if reps > 1 else i / fps
                pose(arm, acts[clip], t)
                scroll(mp, travelled + v * i / fps)
                ST.render(os.path.join(d, "f_%04d.png" % n))
                n += 1
            travelled += v * T
            if clip == 'Death':
                for i in range(int(fps * 0.7)):
                    ST.render(os.path.join(d, "f_%04d.png" % n)); n += 1
        print("[beast] webp frames", n, d)
    print("[beast] SHOTS OK", model, "clips", sorted(acts))


for m in ids:
    try:
        one(m)
    except Exception:
        import traceback
        traceback.print_exc()
        print("[beast] SHOTS FAILED", m)
