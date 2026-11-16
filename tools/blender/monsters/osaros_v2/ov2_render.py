"""Renders of an exported Osaros .glb (EEVEE, back faces culled and emission clamped like the game's character shader).
   blender -b --factory-startup --python ov2_render.py -- --glb=<file> [--out=<dir>] [--prefix=name] [--res=1024]
        [--hero=<hero glb>] shot [shot ...]
shot = tag=cx,cy,cz,size,yaw,pitch[,light[,Action:t]]     light: ember (default) | studio | rake | game
       (centre and half-size of the framed cube in rest-space metres; yaw 0 = front, + towards his left; pitch + = from above)
or one of the presets below (face_front, face_34, bust, ...)."""
import sys, os, math
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE)); sys.path.insert(0, HERE)
import bpy, numpy as np
from mathutils import Vector
import monlib as M, stage as S

PRESET = {
    'face_front': "0,-0.035,1.722,0.185,0,-5,ember",
    'face_34': "0,-0.035,1.722,0.185,33,-7,ember",
    'face_34r': "0,-0.03,1.700,0.165,-33,-7,ember",
    'face_side': "0,-0.02,1.700,0.165,88,-2,ember",
    'bust': "0,0.0,1.56,0.47,24,-16,ember,Idle_Loop:0.3",
    'bust_rest': "0,0.0,1.56,0.47,24,-16,ember",
    'bust_front': "0,0.0,1.56,0.47,0,-12,ember",
    'face_studio': "0,-0.035,1.722,0.185,24,2,studio",
    'face_rake': "0,-0.035,1.722,0.185,20,2,rake",
    'icon': "0.005,-0.03,1.715,0.20,14,-4,ember",
    'full_front': "0,0,1.05,1.25,0,6,studio",
    'full_34': "0,0,1.05,1.25,35,8,studio",
    'full_side': "0,0,1.05,1.25,90,6,studio",
    'full_back': "0,0,1.05,1.25,180,8,studio",
    'full_34r': "0,0,1.05,1.25,-40,8,studio",
}


def argv():
    a = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    fl = {}
    rest = []
    for x in a:
        if x.startswith('--') and '=' in x:
            k, v = x[2:].split('=', 1); fl[k] = v
        elif x.startswith('--'):
            fl[x[2:]] = '1'
        else:
            if x != 'x':
                rest.append(x)
    return fl, rest


def black_world(amb=0.0):
    sc = bpy.context.scene
    w = bpy.data.worlds.new("black")
    w.use_nodes = True
    bg = w.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.25, 0.3, 0.45, 1)
    bg.inputs[1].default_value = amb
    sc.world = w


def clear_lights():
    for o in list(bpy.data.objects):
        if o.type == 'LIGHT':
            bpy.data.objects.remove(o, do_unlink=True)


def spot(name, pos, at, col, watts, size=0.3, angle=60.0, blend=0.6):
    ld = bpy.data.lights.new(name, 'SPOT')
    ld.energy = watts; ld.color = col; ld.shadow_soft_size = size
    ld.spot_size = math.radians(angle); ld.spot_blend = blend
    ob = bpy.data.objects.new(name, ld)
    ob.location = pos
    d = Vector(at) - Vector(pos)
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = d.to_track_quat('-Z', 'Y')
    bpy.context.scene.collection.objects.link(ob)
    return ob


def light_ember(c, size, yaw):
    """near darkness: an ember rim from low behind on his right, a cold rim from high behind on his left, a breath of
    warm light from below in front (the title screen's set-up, in model units)"""
    clear_lights()
    black_world(0.006)
    c = Vector(c)
    k = (size / 0.165) ** 2
    yw = math.radians(yaw)

    def P(right, back, up):       # offsets relative to the viewer: right of frame, behind the subject, up
        rx = Vector((math.cos(yw), math.sin(yw), 0.0)); bk = Vector((-math.sin(yw), math.cos(yw), 0.0))
        return c + (rx * right + bk * back + Vector((0, 0, 1)) * up) * (size / 0.165)
    spot("ember_rim", P(-0.42, 0.40, -0.22), c, (1.0, 0.33, 0.07), 26.0 * k, 0.05)
    spot("cold_rim", P(0.45, 0.45, 0.42), c, (0.40, 0.53, 0.90), 9.0 * k, 0.05)
    spot("under", P(-0.12, -0.55, -0.40), c, (1.0, 0.47, 0.19), 7.0 * k, 0.12)


def light_studio():
    clear_lights()
    for w in list(bpy.data.worlds):
        pass
    S.setup_stage(floor=False)


def make_floor(z=0.0):
    me = bpy.data.meshes.new("floor")
    s = 60
    me.from_pydata([(-s, -s, z), (s, -s, z), (s, s, z), (-s, s, z)], [], [(0, 1, 2, 3)])
    fl = bpy.data.objects.new("stage_floor", me)
    m = bpy.data.materials.new("stage_floor")
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (0.045, 0.045, 0.05, 1)
    b.inputs["Roughness"].default_value = 0.95
    me.materials.append(m)
    bpy.context.scene.collection.objects.link(fl)
    return fl


def game_like(meshes, energy=2.2):
    for o in meshes:
        for m in o.data.materials:
            if m is None:
                continue
            m.use_backface_culling = True
            if m.use_nodes:
                for n in m.node_tree.nodes:
                    if n.type == 'BSDF_PRINCIPLED':
                        es = n.inputs.get('Emission Strength')
                        if es is not None and es.default_value > 0:
                            es.default_value = min(es.default_value, energy) if m.name.startswith('emis') else 0.0


def main():
    fl, shots = argv()
    glb = fl.get('glb', os.path.join(HERE, "out", "boss_osaros.glb"))
    out = fl.get('out', os.path.join(HERE, "shots"))
    prefix = fl.get('prefix', 'r')
    res = int(fl.get('res', '1024'))
    os.makedirs(out, exist_ok=True)
    M.reset_scene()
    acts = None
    if any(':' in s for s in shots) or any(':' in PRESET.get(s, '') for s in shots):
        acts = S.load_actions()
    arm, meshes = S.import_creature(glb)
    game_like(meshes)
    hero = None
    if fl.get('hero'):
        harm, hmeshes = S.import_creature(fl['hero'])
        k = 1.0 / float(fl.get('scale', '2.4'))
        harm.scale = (k, k, k)
        harm.location = (float(fl.get('hx', '0.75')), float(fl.get('hy', '-0.35')), 0.0)
        game_like(hmeshes)
        hero = (harm, hmeshes)
    S.setup_render(res, res, samples=int(fl.get('samples', '48')))
    sc = bpy.context.scene
    try:
        sc.view_settings.view_transform = fl.get('view', 'Standard')
    except Exception:
        pass
    cam = S.make_camera()
    floor = None
    written = []
    for s in shots:
        if '=' in s:
            tag, spec = s.split('=', 1)
        else:
            tag, spec = s, PRESET[s]
        q = spec.split(',')
        cx, cy, cz, size, yaw, pitch = (float(x) for x in q[:6])
        light = q[6] if len(q) > 6 and q[6] else 'ember'
        act = None; t = 0.0
        if len(q) > 7 and q[7]:
            a = q[7].split(':')
            act = acts[a[0]]; t = float(a[1]) if len(a) > 1 else 0.5
        S.set_pose(arm, act, t)
        if hero:
            S.set_pose(hero[0], acts.get(fl.get('hero_act', 'Sword_Idle')) if acts else None, 0.3)
        c = np.array([cx, cy, cz])
        pts = np.array([c + size, c - size])
        if light == 'ember':
            if floor is not None:
                bpy.data.objects.remove(floor, do_unlink=True); floor = None
            light_ember(c, size, yaw)
        elif light == 'rake':
            clear_lights()
            S.setup_rake_stage(floor=False)
            S.rake_lights(yaw, pitch, graze=22.0, side=(-1.0 if yaw < 0 else 1.0))
        else:
            light_studio()
            if floor is None and light in ('studio', 'game') and size > 0.6:
                floor = make_floor()
        S.frame(cam, [], yaw, pitch, float(fl.get('lens', '85')), pts=pts, margin=1.0)
        p = os.path.join(out, "%s_%s.png" % (prefix, tag))
        S.render(p)
        written.append(p)
        M.log("RENDER", p)
    if fl.get('sheet') and written:
        S.combo(written, os.path.join(out, "%s_sheet.png" % prefix), cols=int(fl.get('cols', '3')))
    M.log("RENDER OK", prefix)


main()
