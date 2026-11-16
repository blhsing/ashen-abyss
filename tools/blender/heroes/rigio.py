"""Rig / import / render helpers shared by the hero + NPC build scripts (Blender 5.2, headless)."""
import bpy, bmesh, math, os, sys, json
import numpy as np
from mathutils import Vector, Matrix, Quaternion, Euler

SRC = r"Y:\assets_src\q"
OUT = r"Y:\game\assets\chars"
SHOTS = r"Y:\shots\chars"
TEX = r"Y:\tools\blender\heroes\_tex"
ANIM_LIB = os.path.join(OUT, "anim_lib.glb")

for d in (OUT, SHOTS, TEX):
    os.makedirs(d, exist_ok=True)


_LOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_build.log")
_T0 = None


def log(*a):
    """print + append to _build.log (the throttled launcher only shows stdout when Blender exits)"""
    global _T0
    import time as _time
    print("[hero]", *a)
    sys.stdout.flush()
    try:
        if _T0 is None:
            _T0 = _time.time()
            with open(_LOG, "a", encoding="utf8") as f:
                f.write("---- %s  %s\n" % (_time.strftime("%H:%M:%S"), " ".join(sys.argv[-3:])))
        with open(_LOG, "a", encoding="utf8") as f:
            f.write("%6.0fs  %s\n" % (_time.time() - _T0, " ".join(str(x) for x in a)))
    except Exception:
        pass


# ----------------------------------------------------------------------------------------------- scene
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = 30
    return sc


def _import(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def purge_actions():
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)


def import_rig():
    """Canonical UAL armature (unmodified), without mannequin / animations."""
    objs = _import(os.path.join(SRC, "UAL1.glb"))
    arm = next(o for o in objs if o.type == 'ARMATURE')
    for o in objs:
        if o is not arm:
            bpy.data.objects.remove(o, do_unlink=True)
    if arm.animation_data:
        arm.animation_data_clear()
    purge_actions()
    arm.name = "Armature"
    arm.data.name = "Armature"
    for pb in arm.pose.bones:
        pb.custom_shape = None
    return arm


def _check_same_rest(arm, other):
    worst = 0.0
    for b in arm.data.bones:
        b2 = other.data.bones.get(b.name)
        assert b2 is not None, "bone missing in source: " + b.name
        d = max(abs(a - c) for r1, r2 in zip(b.matrix_local, b2.matrix_local) for a, c in zip(r1, r2))
        worst = max(worst, d)
    assert len(other.data.bones) == len(arm.data.bones)
    return worst


def rebind(obj, arm):
    """Parent a skinned mesh to the canonical armature (identity transforms on both sides)."""
    obj.parent = arm
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location = (0, 0, 0)
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = (1, 0, 0, 0)
    obj.scale = (1, 1, 1)
    for m in list(obj.modifiers):
        if m.type == 'ARMATURE':
            obj.modifiers.remove(m)
    md = obj.modifiers.new("Armature", 'ARMATURE')
    md.object = arm
    return obj


_ALIGN_PREFIX = ("upperarm", "lowerarm", "thigh", "calf", "index_", "middle_", "ring_", "pinky_", "thumb_")


def _np_mat(m):
    return np.array([list(r) for r in m], dtype=np.float64)


def fit_matrices(src_arm, arm):
    """Per-bone 4x4 matrices that move geometry modelled on `src_arm`'s rest pose onto the UAL rest pose.

    The Quaternius base bodies / hair use the same bone names + hierarchy as UAL but different proportions,
    so their meshes are re-shaped to fit the (unmodified) UAL skeleton:
      limbs + fingers : align to the UAL bone (rotation) and stretch along the bone to the UAL length
      torso/head/hand : translate so the joint pivot coincides (keeps the mesh upright)
      feet            : translate by the ball-joint offset (keeps soles on the ground)
    """
    mats = {}
    sb = src_arm.data.bones
    ub = arm.data.bones

    def T(v):
        m = np.eye(4)
        m[:3, 3] = v
        return m
    for b in ub:
        s = sb[b.name]
        name = b.name
        if name == 'root':
            mats[name] = np.eye(4)
        elif name.startswith(_ALIGN_PREFIX):
            sc = 1.0
            if len(b.children) == 1 and len(s.children) == 1:
                lu = (b.children[0].head_local - b.head_local).length
                ls = (s.children[0].head_local - s.head_local).length
                if ls > 1e-5:
                    sc = min(1.35, max(0.75, lu / ls))
            S = np.diag([1.0, sc, 1.0, 1.0])
            mats[name] = _np_mat(b.matrix_local) @ S @ np.linalg.inv(_np_mat(s.matrix_local))
        elif name.startswith(("foot", "ball")):
            side = name[-1]
            k = "ball_" + side
            mats[name] = T(np.array(ub[k].head_local) - np.array(sb[k].head_local))
        elif name.startswith("clavicle"):
            side = name[-1]
            k = "upperarm_" + side
            mats[name] = T(np.array(ub[k].head_local) - np.array(sb[k].head_local))
        else:
            mats[name] = T(np.array(b.head_local) - np.array(s.head_local))
    return mats


def get_weights(obj, bone_names=None):
    """dense (nverts, nbones) weight matrix + list of bone names (columns)"""
    names = bone_names or [g.name for g in obj.vertex_groups]
    col = {n: i for i, n in enumerate(names)}
    gi = {g.index: col[g.name] for g in obj.vertex_groups if g.name in col}
    W = np.zeros((len(obj.data.vertices), len(names)), dtype=np.float32)
    for v in obj.data.vertices:
        for g in v.groups:
            c = gi.get(g.group)
            if c is not None:
                W[v.index, c] = g.weight
    return W, names


def set_weights(obj, W, names, limit=4, eps=0.01):
    """replace all vertex groups from a dense matrix (normalised, limited to `limit` influences)"""
    obj.vertex_groups.clear()
    W = np.array(W, dtype=np.float64)
    if limit and W.shape[1] > limit:
        idx = np.argsort(-W, axis=1)[:, limit:]
        np.put_along_axis(W, idx, 0.0, axis=1)
    W[W < eps] = 0.0
    s = W.sum(1, keepdims=True)
    s[s == 0] = 1.0
    W = W / s
    for c, n in enumerate(names):
        col = W[:, c]
        nz = np.nonzero(col)[0]
        if len(nz) == 0:
            continue
        vg = obj.vertex_groups.new(name=n)
        # group by equal weight would be slow; add individually
        for i in nz:
            vg.add([int(i)], float(col[i]), 'REPLACE')


def get_co(obj):
    me = obj.data
    co = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get('co', co)
    return co.reshape(-1, 3).astype(np.float64)


def set_co(obj, co):
    obj.data.vertices.foreach_set('co', np.asarray(co, dtype=np.float32).ravel())
    obj.data.update()


def clear_custom_normals(obj):
    me = obj.data
    for nm in ("custom_normal", "sharp_edge", "sharp_face"):
        a = me.attributes.get(nm)
        if a is not None:
            try:
                me.attributes.remove(a)
            except Exception:
                pass
    for p in me.polygons:
        p.use_smooth = True
    me.update()


def fit_mesh(obj, mats):
    W, names = get_weights(obj)
    s = W.sum(1, keepdims=True)
    s[s == 0] = 1.0
    W = W / s
    P = get_co(obj)
    Ph = np.concatenate([P, np.ones((len(P), 1))], axis=1)
    out = np.zeros((len(P), 3))
    for c, n in enumerate(names):
        if n not in mats or not W[:, c].any():
            continue
        out += W[:, c:c + 1] * (Ph @ mats[n].T)[:, :3]
    nowt = (W.sum(1) == 0)
    out[nowt] = P[nowt]
    set_co(obj, out)
    clear_custom_normals(obj)


def import_skinned(path, arm, fit=True):
    """Import a glTF rigged to a same-named skeleton, re-shape its meshes onto the UAL rest pose and bind them
    to `arm`; returns ({name: obj}, head_matrix) where head_matrix is the rigid fit used for the Head bone."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path, merge_vertices=True)
    objs = [o for o in bpy.data.objects if o not in before]
    other = next(o for o in objs if o.type == 'ARMATURE')
    assert [b.name for b in other.data.bones] == [b.name for b in arm.data.bones], "bone names differ: " + path
    for b in arm.data.bones:
        b2 = other.data.bones[b.name]
        assert (b.parent.name if b.parent else None) == (b2.parent.name if b2.parent else None), b.name
    mats = fit_matrices(other, arm)
    out = {}
    for o in objs:
        if o.type == 'MESH' and o.parent is other:
            if fit:
                fit_mesh(o, mats)
            rebind(o, arm)
            out[o.name] = o
        elif o is not other:
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.objects.remove(other, do_unlink=True)
    purge_actions()
    return out


def load_actions():
    """Load every UAL animation as an action (from anim_lib.glb); returns {name: action}."""
    objs = _import(ANIM_LIB)
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    return {a.name: a for a in bpy.data.actions}


def set_pose(arm, action, t=0.5):
    """Pose `arm` at fraction t (0..1) of `action` (None -> rest pose)."""
    sc = bpy.context.scene
    if action is None:
        if arm.animation_data:
            arm.animation_data.action = None
        for pb in arm.pose.bones:
            pb.location = (0, 0, 0)
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.rotation_euler = (0, 0, 0)
            pb.scale = (1, 1, 1)
        sc.frame_set(0)
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


# ----------------------------------------------------------------------------------------------- render
def setup_render(width=1100, height=1100, samples=32, transparent=False):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x = width
    sc.render.resolution_y = height
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA' if transparent else 'RGB'
    try:
        sc.eevee.taa_render_samples = samples
    except Exception:
        pass
    for attr, val in (("use_shadows", True), ("use_gtao", True), ("use_raytracing", False)):
        if hasattr(sc.eevee, attr):
            try:
                setattr(sc.eevee, attr, val)
            except Exception:
                pass
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    sc.display_settings.display_device = 'sRGB'
    return sc


SKY = r"Y:\game\assets\env\sky"


def stage_world(hdri="dusk", strength=0.55, backdrop=(0.030, 0.032, 0.038), rot_deg=200.0, ambient=0.035):
    """World for previews: an HDRI lights the character and shows in reflections (metals need something to mirror),
    while camera rays still see a plain dark backdrop. hdri=None -> the old flat ambient colour."""
    world = bpy.data.worlds.new("stage")
    world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes.get("Background")
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_WORLD')
    path = os.path.join(SKY, "%s.hdr" % hdri) if hdri else None
    if not path or not os.path.isfile(path):
        bg.inputs[0].default_value = (0.42, 0.46, 0.55, 1)
        bg.inputs[1].default_value = ambient
        return world
    tc = nt.nodes.new('ShaderNodeTexCoord')
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Rotation'].default_value = (0, 0, math.radians(rot_deg))
    env = nt.nodes.new('ShaderNodeTexEnvironment')
    env.image = bpy.data.images.load(path, check_existing=True)
    nt.links.new(tc.outputs['Generated'], mp.inputs['Vector'])
    nt.links.new(mp.outputs['Vector'], env.inputs['Vector'])
    nt.links.new(env.outputs['Color'], bg.inputs[0])
    bg.inputs[1].default_value = strength
    bg2 = nt.nodes.new('ShaderNodeBackground')
    bg2.inputs[0].default_value = tuple(backdrop) + (1.0,)
    bg2.inputs[1].default_value = 1.0
    lp = nt.nodes.new('ShaderNodeLightPath')
    mx = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(lp.outputs['Is Camera Ray'], mx.inputs[0])
    nt.links.new(bg.outputs[0], mx.inputs[1])
    nt.links.new(bg2.outputs[0], mx.inputs[2])
    nt.links.new(mx.outputs[0], out.inputs['Surface'])
    return world


def stage_sun(name, direction, col, energy, angle=6.0):
    """sun lamp shining ALONG `direction` (world vector)"""
    sc = bpy.context.scene
    ld = bpy.data.lights.new(name, 'SUN')
    ld.energy = energy
    ld.color = col
    ld.angle = math.radians(angle)
    ob = bpy.data.objects.new(name, ld)
    ob.rotation_mode = 'QUATERNION'
    ob.rotation_quaternion = Vector(direction).normalized().to_track_quat('-Z', 'Y')
    sc.collection.objects.link(ob)
    return ob


def rake_dir(cam_yaw_deg, side_deg=68.0, elev_deg=32.0):
    """direction of a raking key light for a camera at yaw `cam_yaw_deg` (see aim_camera): it comes from `side_deg`
    to the camera's left and `elev_deg` above, so it grazes the surfaces the camera looks at."""
    a = math.radians(cam_yaw_deg - side_deg)
    e = math.radians(elev_deg)
    src = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))     # where the light sits
    return -src


def setup_stage(ambient=0.035, key=4.0, rim=5.0, fill=0.6, floor=True, hdri="dusk", hdri_strength=0.55, rake=True):
    """Dark backdrop, HDRI ambient + reflections, warm raking key light, cool rim light (rake=False: old 3-sun rig)."""
    sc = bpy.context.scene
    sc.world = stage_world(hdri, hdri_strength, ambient=ambient)
    made = []
    if rake and hdri:
        k = stage_sun("key", rake_dir(28.0), (1.0, 0.84, 0.68), key * 0.95, 4.0)
        made.append(k)
        made.append(stage_sun("rim", (0.42, -0.72, -0.55), (0.55, 0.72, 1.0), rim * 0.55, 3.0))
        made.append(stage_sun("fill", (-0.75, 0.55, -0.35), (0.7, 0.8, 1.0), fill * 0.5, 25.0))
        sc["stage_key"] = k.name
        if floor:
            made.append(_stage_floor())
        return made

    def sun(name, rot, col, energy, angle=6.0):
        ld = bpy.data.lights.new(name, 'SUN')
        ld.energy = energy
        ld.color = col
        ld.angle = math.radians(angle)
        ob = bpy.data.objects.new(name, ld)
        ob.rotation_euler = rot
        sc.collection.objects.link(ob)
        made.append(ob)
        return ob
    # characters face -Y. key from front-left-above, rim from behind-right, weak fill from front-right
    sun("key", (math.radians(52), 0, math.radians(-38)), (1.0, 0.80, 0.60), key)
    sun("rim", (math.radians(-62), 0, math.radians(-25)), (0.55, 0.72, 1.0), rim, 3.0)
    sun("fill", (math.radians(70), 0, math.radians(60)), (0.7, 0.8, 1.0), fill, 20.0)
    if floor:
        made.append(_stage_floor())
    return made


def _stage_floor():
    sc = bpy.context.scene
    me = bpy.data.meshes.new("floor")
    s = 60
    me.from_pydata([(-s, -s, 0), (s, -s, 0), (s, s, 0), (-s, s, 0)], [], [(0, 1, 2, 3)])
    fl = bpy.data.objects.new("stage_floor", me)
    m = bpy.data.materials.new("stage_floor")
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (0.012, 0.012, 0.013, 1)
    b.inputs["Roughness"].default_value = 0.95
    me.materials.append(m)
    sc.collection.objects.link(fl)
    return fl


def aim_key(cam_yaw_deg, side_deg=68.0, elev_deg=32.0):
    """re-aim the stage key light so it rakes across what a camera at this yaw sees"""
    sc = bpy.context.scene
    k = bpy.data.objects.get(sc.get("stage_key", ""))
    if k is not None:
        k.rotation_mode = 'QUATERNION'
        k.rotation_quaternion = rake_dir(cam_yaw_deg, side_deg, elev_deg).normalized().to_track_quat('-Z', 'Y')


def make_camera(name="cam"):
    cd = bpy.data.cameras.new(name)
    cam = bpy.data.objects.new(name, cd)
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    return cam


def aim_camera(cam, target, yaw_deg, pitch_deg, dist, lens=50.0, ortho_scale=None):
    """yaw 0 = camera in front of the character (at -Y looking +Y); positive yaw moves it to the character's left
    (+X). pitch = degrees looking down."""
    yaw = math.radians(yaw_deg)
    pitch = math.radians(pitch_deg)
    t = Vector(target)
    d = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    cam.location = t + d * dist
    cam.rotation_mode = 'QUATERNION'
    cam.rotation_quaternion = (-d).to_track_quat('-Z', 'Y')
    if ortho_scale:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho_scale
    else:
        cam.data.type = 'PERSP'
        cam.data.lens = lens
    cam.data.clip_start = 0.05
    cam.data.clip_end = 500


def render_to(path):
    sc = bpy.context.scene
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    log("rendered", path)


def world_bbox(objs, depsgraph=None):
    """bbox of evaluated (posed) meshes"""
    dg = depsgraph or bpy.context.evaluated_depsgraph_get()
    mn = np.array([1e9] * 3)
    mx = -mn
    for o in objs:
        if o.type != 'MESH' or o.hide_render:
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        n = len(me.vertices)
        if n:
            co = np.empty(n * 3, dtype=np.float32)
            me.vertices.foreach_get('co', co)
            co = co.reshape(-1, 3)
            M = np.array(ev.matrix_world)
            w = co @ M[:3, :3].T + M[:3, 3]
            mn = np.minimum(mn, w.min(0))
            mx = np.maximum(mx, w.max(0))
        ev.to_mesh_clear()
    return mn, mx
