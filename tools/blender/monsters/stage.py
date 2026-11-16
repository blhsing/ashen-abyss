"""Preview stage: lights, cameras, pose sampling, renders, contact-sheet compositing."""
import bpy, math, os, sys
import numpy as np
from mathutils import Vector, Matrix
import monlib as M


def setup_render(w=700, h=700, samples=24):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x = w
    sc.render.resolution_y = h
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGB'
    sc.eevee.taa_render_samples = samples
    for attr, val in (("use_shadows", True), ("use_raytracing", False), ("use_fast_gi", False)):
        if hasattr(sc.eevee, attr):
            try:
                setattr(sc.eevee, attr, val)
            except Exception:
                pass
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except Exception:
        pass
    return sc


SKY_DIR = r"Y:\game\assets\env\sky"


def hdri_world(name="overcast", strength=0.35, backdrop=(0.045, 0.047, 0.055), rot_z=0.0):
    """world lit by one of the game's HDRIs (so metal / ice / wet surfaces have something to reflect) while the
    camera still sees a flat dark backdrop. Falls back to a flat-colour world if the file is missing."""
    sc = bpy.context.scene
    world = bpy.data.worlds.new("stage_hdri")
    world.use_nodes = True
    nt = world.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputWorld')
    path = os.path.join(SKY_DIR, name + ".hdr")
    bg = nt.nodes.new('ShaderNodeBackground')
    if os.path.isfile(path):
        env = nt.nodes.new('ShaderNodeTexEnvironment')
        env.image = bpy.data.images.load(path, check_existing=True)
        tc = nt.nodes.new('ShaderNodeTexCoord')
        mp = nt.nodes.new('ShaderNodeMapping')
        mp.inputs['Rotation'].default_value = (0, 0, rot_z)
        nt.links.new(tc.outputs['Generated'], mp.inputs['Vector'])
        nt.links.new(mp.outputs[0], env.inputs['Vector'])
        nt.links.new(env.outputs[0], bg.inputs[0])
        bg.inputs[1].default_value = strength
    else:
        bg.inputs[0].default_value = (0.55, 0.62, 0.8, 1)
        bg.inputs[1].default_value = 0.05
    flat = nt.nodes.new('ShaderNodeBackground')
    flat.name = "stage_flat"
    flat.inputs[0].default_value = tuple(backdrop) + (1.0,)
    flat.inputs[1].default_value = 1.0
    lp = nt.nodes.new('ShaderNodeLightPath')
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(lp.outputs['Is Camera Ray'], mix.inputs[0])
    nt.links.new(bg.outputs[0], mix.inputs[1])
    nt.links.new(flat.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    sc.world = world
    return world


def _sun(name, direction, col, energy, angle=1.0):
    """sun whose light travels along -direction (direction = unit vector from the subject towards the light)"""
    sc = bpy.context.scene
    old = bpy.data.objects.get(name)
    if old is not None:
        bpy.data.objects.remove(old, do_unlink=True)
    ld = bpy.data.lights.new(name, 'SUN')
    ld.energy = energy; ld.color = col; ld.angle = math.radians(angle)
    ob = bpy.data.objects.new(name, ld)
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = Vector(direction).normalized().to_track_quat('Z', 'Y')
    sc.collection.objects.link(ob)
    return ob


def setup_rake_stage(hdri="overcast", strength=0.22, floor=True):
    """look-dev stage for surface relief: HDRI at low strength + lights placed per shot by rake_lights()"""
    hdri_world(hdri, strength)
    sc = bpy.context.scene
    if floor:
        me = bpy.data.meshes.new("floor")
        s = 80
        me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
        fl = bpy.data.objects.new("stage_floor", me)
        m = bpy.data.materials.new("stage_floor")
        m.use_nodes = True
        b = m.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = (0.05, 0.05, 0.055, 1)
        b.inputs["Roughness"].default_value = 0.95
        me.materials.append(m)
        sc.collection.objects.link(fl)


def rake_lights(yaw_deg, pitch_deg, graze=12.0, side=1.0, key=8.0, fill=0.55, rim=1.6):
    """hard key light that grazes the surfaces facing a camera at (yaw, pitch): it comes from the side and above,
    only `graze` degrees in front of the plane perpendicular to the view. Weak fill from the camera, cool rim."""
    yaw = math.radians(yaw_deg); pitch = math.radians(pitch_deg)
    to_cam = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    right = Vector((math.cos(yaw), math.sin(yaw), 0.0)) * side
    up = to_cam.cross(Vector((math.cos(yaw), math.sin(yaw), 0.0))) * -1.0
    if up.z < 0:
        up = -up
    g = math.radians(graze)
    kd = (right * 0.80 + up * 0.60).normalized() * math.cos(g) + to_cam * math.sin(g)
    _sun("rake_key", kd, (1.0, 0.96, 0.9), key, 0.6)
    _sun("rake_fill", to_cam + up * 0.3, (0.75, 0.82, 1.0), fill, 25.0)
    _sun("rake_rim", (-right * 0.7 + up * 0.5 - to_cam * 0.6), (0.6, 0.75, 1.0), rim, 3.0)


def setup_stage(ambient=0.05, key=3.6, rim=4.5, fill=0.5, floor=True, hdri="overcast", hdri_strength=0.3):
    """dark-neutral backdrop, warm key (front-left-above), cool rim (behind-right); the world is one of the game's
    HDRIs at low strength (hdri=None restores the old flat-colour world at `ambient`)"""
    sc = bpy.context.scene
    if hdri:
        hdri_world(hdri, hdri_strength, backdrop=(0.028, 0.031, 0.04))
    else:
        world = bpy.data.worlds.new("stage")
        world.use_nodes = True
        bg = world.node_tree.nodes.get("Background")
        bg.inputs[0].default_value = (0.55, 0.62, 0.8, 1)
        bg.inputs[1].default_value = ambient
        sc.world = world

    def sun(name, rot, col, energy, angle=6.0):
        ld = bpy.data.lights.new(name, 'SUN')
        ld.energy = energy; ld.color = col; ld.angle = math.radians(angle)
        ob = bpy.data.objects.new(name, ld)
        ob.rotation_euler = rot
        sc.collection.objects.link(ob)
        return ob
    sun("key", (math.radians(52), 0, math.radians(-38)), (1.0, 0.80, 0.60), key)
    sun("rim", (math.radians(-62), 0, math.radians(-25)), (0.55, 0.72, 1.0), rim, 3.0)
    sun("fill", (math.radians(70), 0, math.radians(60)), (0.7, 0.8, 1.0), fill, 20.0)
    if floor:
        me = bpy.data.meshes.new("floor")
        s = 80
        me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
        fl = bpy.data.objects.new("stage_floor", me)
        m = bpy.data.materials.new("stage_floor")
        m.use_nodes = True
        b = m.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = (0.05, 0.05, 0.055, 1)
        b.inputs["Roughness"].default_value = 0.95
        me.materials.append(m)
        sc.collection.objects.link(fl)
        return fl
    return None


def make_camera(name="cam"):
    cd = bpy.data.cameras.new(name)
    cam = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    cd.clip_start = 0.05; cd.clip_end = 500
    return cam


def aim(cam, target, yaw_deg, pitch_deg, dist, lens=50.0, ortho=None):
    """yaw 0 = camera in front of the creature (at -Y looking +Y); +yaw moves it towards +X. pitch = looking down."""
    yaw = math.radians(yaw_deg); pitch = math.radians(pitch_deg)
    t = Vector(target)
    d = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    cam.location = t + d * dist
    cam.rotation_mode = 'QUATERNION'
    cam.rotation_quaternion = (-d).to_track_quat('-Z', 'Y')
    if ortho:
        cam.data.type = 'ORTHO'; cam.data.ortho_scale = ortho
    else:
        cam.data.type = 'PERSP'; cam.data.lens = lens


def posed_points(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    for o in objs:
        if o.type != 'MESH':
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        n = len(me.vertices)
        if n:
            co = np.empty(n * 3, dtype=np.float32)
            me.vertices.foreach_get('co', co)
            co = co.reshape(-1, 3)
            Mx = np.array(ev.matrix_world)
            pts.append(co @ Mx[:3, :3].T + Mx[:3, 3])
        ev.to_mesh_clear()
    return np.vstack(pts) if pts else np.zeros((1, 3))


def frame(cam, objs, yaw, pitch, lens=70.0, margin=1.12, pts=None):
    """aim + dolly so the posed meshes fill the frame"""
    sc = bpy.context.scene
    P = posed_points(objs) if pts is None else pts
    mn, mx = P.min(0), P.max(0)
    c = (mn + mx) / 2
    aim(cam, c, yaw, pitch, 10.0, lens)
    bpy.context.view_layer.update()
    R = np.array(cam.matrix_world)[:3, :3]
    rel = (P - c) @ R          # camera-space offsets (x right, y up, -z forward)
    asp = sc.render.resolution_x / sc.render.resolution_y
    sw = cam.data.sensor_width
    tx = (sw / 2) / lens * (1.0 if asp >= 1 else asp)
    ty = tx / asp if asp >= 1 else (sw / 2) / lens
    # shift target to centre the projection
    cx = (rel[:, 0].max() + rel[:, 0].min()) / 2; cy = (rel[:, 1].max() + rel[:, 1].min()) / 2
    c2 = c + R[:, 0] * cx + R[:, 1] * cy
    rx = np.abs(rel[:, 0] - cx); ry = np.abs(rel[:, 1] - cy)
    dist = max((rx / tx * margin + rel[:, 2]).max(), (ry / ty * margin + rel[:, 2]).max())
    aim(cam, c2, yaw, pitch, dist, lens)
    return c2, dist


def render(path):
    sc = bpy.context.scene
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def load_actions():
    """all UAL1 + UAL2 actions (objects discarded)"""
    acts = {}
    for src in (M.UAL1, M.UAL2):
        before = set(bpy.data.actions)
        objs = M.import_gltf(src)
        for o in objs:
            bpy.data.objects.remove(o, do_unlink=True)
        for a in bpy.data.actions:
            if a not in before:
                nm = a.name.split('.')[0] if a.name[-4:-3] == '.' else a.name
                if nm not in acts:
                    acts[nm] = a
    return acts


def set_pose(arm, action, t=0.5):
    sc = bpy.context.scene
    if action is None:
        if arm.animation_data:
            arm.animation_data.action = None
        for pb in arm.pose.bones:
            pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
        sc.frame_set(0)
        bpy.context.view_layer.update()
        return
    if arm.animation_data is None:
        arm.animation_data_create()
    ad = arm.animation_data
    for tr in list(ad.nla_tracks):
        ad.nla_tracks.remove(tr)
    ad.action = action
    if ad.action_slot is None and len(action.slots):
        ad.action_slot = action.slots[0]
    f0, f1 = action.frame_range
    fr = f0 + (f1 - f0) * t
    sc.frame_set(int(math.floor(fr)), subframe=fr - math.floor(fr))
    bpy.context.view_layer.update()


def import_creature(path):
    objs = M.import_gltf(path)
    arm = next(o for o in objs if o.type == 'ARMATURE')
    meshes = [o for o in objs if o.type == 'MESH']
    for pb in arm.pose.bones:
        pb.custom_shape = None
    for o in objs:
        if o.type not in ('ARMATURE', 'MESH'):
            bpy.data.objects.remove(o, do_unlink=True)
    return arm, meshes


def silhouette_mode(on, bg=(0.82, 0.82, 0.82)):
    sc = bpy.context.scene
    vl = bpy.context.view_layer
    if on:
        m = bpy.data.materials.get("sil_black")
        if m is None:
            m = bpy.data.materials.new("sil_black")
            m.use_nodes = True
            nt = m.node_tree
            for n in list(nt.nodes):
                nt.nodes.remove(n)
            e = nt.nodes.new('ShaderNodeEmission'); e.inputs[0].default_value = (0, 0, 0, 1)
            o = nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(e.outputs[0], o.inputs[0])
        vl.material_override = m
        w = sc.world.node_tree.nodes.get("stage_flat") or sc.world.node_tree.nodes.get("Background")
        sc['_bg'] = list(w.inputs[0].default_value) + [w.inputs[1].default_value]
        w.inputs[0].default_value = tuple(bg) + (1,); w.inputs[1].default_value = 1.0
    else:
        vl.material_override = None
        w = sc.world.node_tree.nodes.get("stage_flat") or sc.world.node_tree.nodes.get("Background")
        b = sc.get('_bg')
        if b:
            w.inputs[0].default_value = b[:4]; w.inputs[1].default_value = b[4]


def load_px(path):
    im = bpy.data.images.load(path, check_existing=False)
    w, h = im.size
    a = np.empty(w * h * 4, dtype=np.float32)
    im.pixels.foreach_get(a)
    a = a.reshape(h, w, 4)
    bpy.data.images.remove(im)
    return a


def save_px(a, path):
    h, w = a.shape[:2]
    im = bpy.data.images.new("combo", w, h, alpha=False)
    try:
        im.colorspace_settings.name = 'sRGB'
    except Exception:
        pass
    im.pixels.foreach_set(a.astype(np.float32).ravel())
    im.filepath_raw = path
    im.file_format = 'PNG'
    im.save()
    bpy.data.images.remove(im)


def combo(paths, out, cols=2):
    tiles = [load_px(p) for p in paths]
    h, w = tiles[0].shape[:2]
    rows = (len(tiles) + cols - 1) // cols
    canvas = np.zeros((rows * h, cols * w, 4), dtype=np.float32)
    canvas[:, :, 3] = 1
    for i, t in enumerate(tiles):
        r = rows - 1 - i // cols   # image rows are bottom-up
        c = i % cols
        canvas[r * h:(r + 1) * h, c * w:(c + 1) * w] = t[:h, :w]
    save_px(canvas, out)
