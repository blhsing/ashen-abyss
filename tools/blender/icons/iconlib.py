"""Icon stage + small modelling / material toolkit for the raster icon set of Ashen Abyss.

Run only through Y:\\tools\\blender\\run_blender.ps1 (see render_icons.py). Everything is modelled at LIFE SIZE (metres) so the
kit looks (Y:\\tools\\blender\\monsters\\looks.py, scanned CC0 detail at real texel scale) land with the right grain.

Stage convention (shared with the UI frames, see Y:\\tools\\icons\\STYLE.md): camera on -Y looking +Y, slightly from above;
X = right, Z = up. Warm key light from the UPPER LEFT (front), cool rim from behind right, weak cool fill, HDRI for
reflections. Film is transparent: the icon is the subject with clean alpha.
"""
import bpy, bmesh, math, os, sys, random
import numpy as np
from mathutils import Vector, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
for _p in (r"Y:\tools\blender\monsters", HERE):
    if _p not in sys.path:
        sys.path.insert(0, _p)
import looks as L          # READ-ONLY kit module (monster look library v2)

RAW = r"Y:\assets_src\icons_render"
CHARS = r"Y:\game\assets\chars"
SKY = r"Y:\game\assets\env\sky\overcast.hdr"
CAM_PITCH = 14.0           # degrees the camera looks down
R = math.radians


# ------------------------------------------------------------------------------------------------ live masks
def _live_masks(self):
    """The kit reads baked AO / cavity / edge masks; icons are rendered live, so feed it Cycles AO nodes instead
    (same remapping as looks.bake_and_assign stage 1, distances shortened for hand-sized props)."""
    if self._mk is None:
        def ao(dist, inside, local):
            a = self.new('ShaderNodeAmbientOcclusion', samples=8, inside=inside, only_local=local)
            a.inputs['Distance'].default_value = dist
            return a.outputs['AO']
        wide = ao(0.12, False, False)
        cav = self.math('SUBTRACT', self.math('MULTIPLY', self.math('SUBTRACT', 1.0, ao(0.014, False, False)), 1.7), 0.06, clamp=True)
        edge = self.math('SUBTRACT', self.math('MULTIPLY', self.math('SUBTRACT', 1.0, ao(0.006, True, True)), 1.15), 0.03, clamp=True)
        self._mk = (wide, cav, edge)
    return self._mk


L.NB._masks = _live_masks      # runtime patch only; the kit file itself is untouched


# ------------------------------------------------------------------------------------------------ scene / stage
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 40
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 6
    sc.cycles.transparent_max_bounces = 8
    sc.cycles.transmission_bounces = 6
    sc.cycles.caustics_reflective = False
    sc.cycles.caustics_refractive = False
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.render.image_settings.color_depth = '8'
    sc.render.resolution_percentage = 100
    for vt, look in (('Standard', 'Medium High Contrast'), ('Standard', 'None')):
        try:
            sc.view_settings.view_transform = vt
            sc.view_settings.look = look
            break
        except Exception:
            continue
    # world: HDRI for reflections and soft ambient
    w = bpy.data.worlds.new("icon_world"); w.use_nodes = True
    nt = w.node_tree
    bg = nt.nodes.get("Background")
    if os.path.isfile(SKY):
        env = nt.nodes.new('ShaderNodeTexEnvironment'); env.image = bpy.data.images.load(SKY)
        tc = nt.nodes.new('ShaderNodeTexCoord'); mp = nt.nodes.new('ShaderNodeMapping')
        mp.inputs['Rotation'].default_value = (0, 0, R(110))
        nt.links.new(tc.outputs['Generated'], mp.inputs['Vector']); nt.links.new(mp.outputs[0], env.inputs['Vector'])
        nt.links.new(env.outputs[0], bg.inputs[0])
        bg.inputs[1].default_value = 0.42
    else:
        bg.inputs[0].default_value = (0.5, 0.58, 0.75, 1); bg.inputs[1].default_value = 0.25
    sc.world = w

    def sun(name, d, col, energy, angle):
        ld = bpy.data.lights.new(name, 'SUN'); ld.energy = energy; ld.color = col; ld.angle = R(angle)
        ob = bpy.data.objects.new(name, ld)
        ob.rotation_mode = 'QUATERNION'
        ob.rotation_quaternion = Vector(d).normalized().to_track_quat('Z', 'Y')
        sc.collection.objects.link(ob)
    sun("key", (-0.62, -0.55, 0.62), (1.0, 0.80, 0.58), 6.2, 7.0)       # warm, upper left, front
    sun("rim", (0.70, 0.62, 0.38), (0.45, 0.68, 1.0), 8.5, 4.0)         # cool, behind right
    sun("fill", (0.55, -0.75, 0.15), (0.62, 0.74, 1.0), 0.5, 30.0)     # weak cool fill from the right front
    cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.clip_start = 0.01; cd.clip_end = 100
    cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    return sc


def clear():
    """remove every subject (keeps lights, camera, world); materials / images stay cached"""
    for o in list(bpy.data.objects):
        if (o.type in ('LIGHT', 'CAMERA') and not o.name.startswith("fx_")) or o.get("glbsrc"):
            continue
        bpy.data.objects.remove(o, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)


def subjects():
    return [o for o in bpy.context.scene.objects if o.type == 'MESH' and not o.hide_render]


def shoot(cat, name, res=512, fill=0.80, samples=None, margin_glow=0.0):
    """fit the orthographic camera to everything in the scene and render RAW\\cat\\name.png (res x res RGBA)"""
    sc = bpy.context.scene
    cam = sc.camera
    p = R(CAM_PITCH)
    d = Vector((0.0, -math.cos(p), math.sin(p)))            # from the subject toward the camera
    right = Vector((1.0, 0.0, 0.0)); up = d.cross(right) * -1.0
    if up.z < 0:
        up = -up
    dg = bpy.context.evaluated_depsgraph_get()
    lo = [1e9, 1e9, 1e9]; hi = [-1e9, -1e9, -1e9]
    for o in subjects():
        if o.get("nofit"):
            continue
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        n = len(me.vertices)
        if n == 0:
            ev.to_mesh_clear(); continue
        co = np.empty(n * 3, dtype=np.float32); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
        Mx = np.array(ev.matrix_world); co = co @ Mx[:3, :3].T + Mx[:3, 3]
        for i, ax in enumerate((right, up, d)):
            v = co @ np.array(ax)
            lo[i] = min(lo[i], float(v.min())); hi[i] = max(hi[i], float(v.max()))
        ev.to_mesh_clear()
    cx, cy = (lo[0] + hi[0]) * 0.5, (lo[1] + hi[1]) * 0.5
    size = max(hi[0] - lo[0], hi[1] - lo[1]) * (1.0 + margin_glow)
    cam.data.ortho_scale = size / fill
    cam.location = right * cx + up * cy + d * (hi[2] + 5.0)
    cam.rotation_mode = 'QUATERNION'
    cam.rotation_quaternion = (-d).to_track_quat('-Z', 'Y')
    sc.render.resolution_x = res; sc.render.resolution_y = res
    if samples:
        sc.cycles.samples = samples
    out = os.path.join(RAW, cat, name + ".png")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    print("ICON %s/%s" % (cat, name))
    return out


# ------------------------------------------------------------------------------------------------ materials
_MATS = {}


def look(kind, key=None, **params):
    """kit look (bone / metal / cloth / leather / rock / wood / ice / wax / glass / emis ...), cached by key"""
    key = key or (kind + repr(sorted(params.items())))
    m = _MATS.get(key)
    if m is None:
        m = L.make_look_material("ic_%d" % len(_MATS), kind, params)
        _MATS[key] = m
    return m


def preset(name, **over):
    p = dict(L.PRESET[name]); p.update(over)
    return look('metal', "preset_" + name + repr(sorted(over.items())), **p)


def steel(**o):     return preset('worn_steel', **o)
def iron(**o):      return preset('rusty_iron', **o)
def brass(**o):     return preset('brass', **o)
def gold(**o):      return preset('gold_tarnish', **o)
def ashplate(**o):  return preset('black_plate', **o)
def silver(**o):    return look('metal', color=(0.52, 0.53, 0.55), amount=0.18, rough=0.3, edge=0.6, edge_color=(0.8, 0.8, 0.82), **o)
def copper(**o):    return look('metal', color=(0.42, 0.20, 0.10), rust=(0.10, 0.22, 0.17), rust2=(0.05, 0.1, 0.08), amount=0.35, rough=0.38, edge_color=(0.85, 0.5, 0.3), **o)
def leather(c=(0.10, 0.065, 0.04), **o): return look('leather', color=c, **o)
def cloth(c=(0.16, 0.155, 0.15), **o):   return look('cloth', color=c, **o)
def bone(**o):      return look('bone', **o)
def wood(c=(0.12, 0.08, 0.05), **o):     return look('wood', color=c, **o)
def rock(**o):      return look('rock', **o)


def _pbr(name):
    m = bpy.data.materials.new(name); m.use_nodes = True
    return m, m.node_tree, m.node_tree.nodes.get("Principled BSDF")


def emit(col, strength=6.0, key=None):
    key = key or "emit%r%r" % (col, strength)
    if key in _MATS:
        return _MATS[key]
    m, nt, b = _pbr(key)
    b.inputs['Base Color'].default_value = (col[0] * 0.3, col[1] * 0.3, col[2] * 0.3, 1)
    b.inputs['Emission Color'].default_value = tuple(col) + (1,)
    b.inputs['Emission Strength'].default_value = strength
    _MATS[key] = m
    return m


def ember(col=(1.0, 0.42, 0.08), strength=9.0, scale=22.0, crust=0.5, dark=(0.02, 0.018, 0.016)):
    """cooling ember / lava: hot emissive veins between dark crust islands (object-space voronoi)"""
    key = "ember%r%r%r%r" % (col, strength, scale, crust)
    if key in _MATS:
        return _MATS[key]
    m, nt, b = _pbr(key)
    tc = nt.nodes.new('ShaderNodeTexCoord')
    vo = nt.nodes.new('ShaderNodeTexVoronoi'); vo.feature = 'DISTANCE_TO_EDGE'; vo.inputs['Scale'].default_value = scale
    no = nt.nodes.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = scale * 0.6; no.inputs['Detail'].default_value = 4
    nt.links.new(tc.outputs['Object'], vo.inputs['Vector']); nt.links.new(tc.outputs['Object'], no.inputs['Vector'])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.0; ramp.color_ramp.elements[0].color = (1, 1, 1, 1)
    ramp.color_ramp.elements[1].position = 0.02 + 0.2 * crust; ramp.color_ramp.elements[1].color = (0, 0, 0, 1)
    nt.links.new(vo.outputs['Distance'], ramp.inputs[0])
    mul = nt.nodes.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'
    nt.links.new(ramp.outputs[0], mul.inputs[0]); nt.links.new(no.outputs[0], mul.inputs[1])
    st = nt.nodes.new('ShaderNodeMath'); st.operation = 'MULTIPLY'; st.inputs[1].default_value = strength * 2.0
    nt.links.new(mul.outputs[0], st.inputs[0])
    b.inputs['Base Color'].default_value = tuple(dark) + (1,)
    b.inputs['Roughness'].default_value = 0.75
    b.inputs['Emission Color'].default_value = tuple(col) + (1,)
    nt.links.new(st.outputs[0], b.inputs['Emission Strength'])
    bump = nt.nodes.new('ShaderNodeBump'); bump.inputs['Strength'].default_value = 0.6; bump.inputs['Distance'].default_value = 0.004
    nt.links.new(vo.outputs['Distance'], bump.inputs['Height']); nt.links.new(bump.outputs[0], b.inputs['Normal'])
    _MATS[key] = m
    return m


def gem(col, ior=1.75, glow=0.0, rough=0.0, dens=0.28):
    """cut gemstone: every facet gets its own brilliance (white noise on the true normal), sharp coat highlights,
    some real refraction for depth -- reads as a faceted stone even at 64 px"""
    key = "gem%r%r%r%r" % (col, ior, glow, rough)
    if key in _MATS:
        return _MATS[key]
    m, nt, b = _pbr(key)
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    wn = nt.nodes.new('ShaderNodeTexWhiteNoise'); wn.noise_dimensions = '3D'
    sc = nt.nodes.new('ShaderNodeVectorMath'); sc.operation = 'SCALE'; sc.inputs['Scale'].default_value = 7.31
    nt.links.new(geo.outputs['True Normal'], sc.inputs[0]); nt.links.new(sc.outputs[0], wn.inputs['Vector'])
    pw = nt.nodes.new('ShaderNodeMath'); pw.operation = 'POWER'; pw.inputs[1].default_value = 1.8
    nt.links.new(wn.outputs['Value'], pw.inputs[0])
    lw = nt.nodes.new('ShaderNodeLayerWeight'); lw.inputs[0].default_value = 0.45
    mr = nt.nodes.new('ShaderNodeMapRange'); mr.inputs[3].default_value = 0.015 + glow * 0.25; mr.inputs[4].default_value = 0.50 + glow
    nt.links.new(pw.outputs[0], mr.inputs[0])
    fz = nt.nodes.new('ShaderNodeMath'); fz.operation = 'MULTIPLY_ADD'; fz.inputs[1].default_value = -0.55; fz.inputs[2].default_value = 1.0
    nt.links.new(lw.outputs['Facing'], fz.inputs[0])
    st = nt.nodes.new('ShaderNodeMath'); st.operation = 'MULTIPLY'
    nt.links.new(mr.outputs[0], st.inputs[0]); nt.links.new(fz.outputs[0], st.inputs[1])
    deep = (col[0] * 0.42, col[1] * 0.42, col[2] * 0.42)
    b.inputs['Base Color'].default_value = tuple(deep) + (1,)
    b.inputs['Transmission Weight'].default_value = dens
    b.inputs['IOR'].default_value = ior
    b.inputs['Roughness'].default_value = rough
    b.inputs['Coat Weight'].default_value = 1.0
    b.inputs['Coat Roughness'].default_value = 0.03 + rough * 0.6
    b.inputs['Emission Color'].default_value = tuple(col) + (1,)
    nt.links.new(st.outputs[0], b.inputs['Emission Strength'])
    _MATS[key] = m
    return m


def glass(col=(0.75, 0.85, 0.82), rough=0.04, key=None):
    key = key or "glass%r%r" % (col, rough)
    if key in _MATS:
        return _MATS[key]
    m, nt, b = _pbr(key)
    b.inputs['Base Color'].default_value = tuple(col) + (1,)
    b.inputs['Transmission Weight'].default_value = 1.0
    b.inputs['IOR'].default_value = 1.45
    b.inputs['Roughness'].default_value = rough
    _MATS[key] = m
    return m


def fire(hot=(1.0, 0.85, 0.45), cool=(1.0, 0.28, 0.03), strength=14.0, axis='Z', key=None, soft=True):
    """flame / energy surface: emission graded along the object's local axis, fading out toward the tip and rim"""
    key = key or "fire%r%r%r%s%s" % (hot, cool, strength, axis, soft)
    if key in _MATS:
        return _MATS[key]
    m = bpy.data.materials.new(key); m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    tc = nt.nodes.new('ShaderNodeTexCoord'); sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Generated'], sep.inputs[0])
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position = 0.05; ramp.color_ramp.elements[0].color = tuple(hot) + (1,)
    ramp.color_ramp.elements[1].position = 0.85; ramp.color_ramp.elements[1].color = tuple(cool) + (1,)
    nt.links.new(sep.outputs[axis], ramp.inputs[0])
    no = nt.nodes.new('ShaderNodeTexNoise'); no.inputs['Scale'].default_value = 9.0; no.inputs['Detail'].default_value = 5
    nt.links.new(tc.outputs['Object'], no.inputs['Vector'])
    em = nt.nodes.new('ShaderNodeEmission'); em.inputs[1].default_value = strength
    nt.links.new(ramp.outputs[0], em.inputs[0])
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    # alpha: strong at the base, wispy toward the tip, thinner at grazing angles
    fall = nt.nodes.new('ShaderNodeMapRange'); fall.inputs[1].default_value = 0.25; fall.inputs[2].default_value = 1.0
    fall.inputs[3].default_value = 1.0; fall.inputs[4].default_value = 0.0
    nt.links.new(sep.outputs[axis], fall.inputs[0])
    lw = nt.nodes.new('ShaderNodeLayerWeight'); lw.inputs[0].default_value = 0.35
    inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    nt.links.new(lw.outputs['Facing'], inv.inputs[1])
    a1 = nt.nodes.new('ShaderNodeMath'); a1.operation = 'MULTIPLY'
    nt.links.new(fall.outputs[0], a1.inputs[0]); nt.links.new(inv.outputs[0] if soft else fall.outputs[0], a1.inputs[1])
    a2 = nt.nodes.new('ShaderNodeMath'); a2.operation = 'MULTIPLY_ADD'; a2.inputs[2].default_value = 0.0
    nmap = nt.nodes.new('ShaderNodeMapRange'); nmap.inputs[1].default_value = 0.3; nmap.inputs[2].default_value = 0.7
    nmap.inputs[3].default_value = 0.35; nmap.inputs[4].default_value = 1.0
    nt.links.new(no.outputs[0], nmap.inputs[0])
    nt.links.new(a1.outputs[0], a2.inputs[0]); nt.links.new(nmap.outputs[0], a2.inputs[1])
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(a2.outputs[0], mix.inputs[0]); nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])
    _MATS[key] = m
    return m


def tint_material(src, key, mul=(1, 1, 1), rough_add=0.0, emit=None, emit_strength=0.0, metal=None):
    """copy of an imported (baked-texture) material with its base colour multiplied / emission replaced"""
    k = "tint_%s_%s" % (src.name, key)
    if k in _MATS:
        return _MATS[k]
    m = src.copy(); m.name = k
    nt = m.node_tree
    b = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b is not None:
        s = b.inputs['Base Color']
        if s.is_linked:
            frm = s.links[0].from_socket
            mx = nt.nodes.new('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.blend_type = 'MULTIPLY'
            mx.inputs[0].default_value = 1.0
            nt.links.new(frm, mx.inputs[6]); mx.inputs[7].default_value = tuple(mul) + (1,)
            nt.links.new(mx.outputs[2], s)
        else:
            c = s.default_value; s.default_value = (c[0] * mul[0], c[1] * mul[1], c[2] * mul[2], 1)
        if rough_add:
            r = b.inputs['Roughness']
            if r.is_linked:
                frm = r.links[0].from_socket
                ad = nt.nodes.new('ShaderNodeMath'); ad.operation = 'ADD'; ad.use_clamp = True; ad.inputs[1].default_value = rough_add
                nt.links.new(frm, ad.inputs[0]); nt.links.new(ad.outputs[0], r)
            else:
                r.default_value = min(1.0, max(0.0, r.default_value + rough_add))
        if metal is not None and not b.inputs['Metallic'].is_linked:
            b.inputs['Metallic'].default_value = metal
        if emit is not None:
            e = b.inputs['Emission Color']
            for l in list(e.links):
                nt.links.remove(l)
            e.default_value = tuple(emit) + (1,)
            es = b.inputs['Emission Strength']
            for l in list(es.links):
                nt.links.remove(l)
            es.default_value = emit_strength
    _MATS[k] = m
    return m


# ------------------------------------------------------------------------------------------------ mesh helpers
def obj(name, verts, faces, mat=None, smooth=True, edges=()):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], list(edges), [tuple(f) for f in faces])
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    if mat is not None:
        me.materials.append(mat)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return o


def setmat(o, mat):
    o.data.materials.clear(); o.data.materials.append(mat)
    return o


def place(o, loc=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
    o.location = loc
    o.rotation_euler = Euler((R(rot[0]), R(rot[1]), R(rot[2])), 'XYZ')
    o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
    return o


def mod(o, typ, **kw):
    m = o.modifiers.new(typ.lower(), typ)
    for k, v in kw.items():
        setattr(m, k, v)
    return o


def bevel(o, w=0.002, seg=2, angle=35):
    return mod(o, 'BEVEL', width=w, segments=seg, limit_method='ANGLE', angle_limit=R(angle), harden_normals=False)


def subsurf(o, lv=2):
    return mod(o, 'SUBSURF', levels=lv, render_levels=lv)


def solid(o, t=0.003, offset=-1.0):
    return mod(o, 'SOLIDIFY', thickness=t, offset=offset)


def displace(o, strength=0.004, scale=0.08, seed=0):
    tx = bpy.data.textures.new("dn", 'CLOUDS'); tx.noise_scale = scale; tx.noise_depth = 3
    return mod(o, 'DISPLACE', texture=tx, strength=strength, texture_coords='GLOBAL' if seed == 0 else 'LOCAL')


def flat(o, angle=30):
    """sharp facets beyond `angle` degrees"""
    try:
        o.data.set_sharp_from_angle(angle=R(angle))
    except Exception:
        for p in o.data.polygons:
            p.use_smooth = False
    return o


def weld(o, dist=1e-5, angle=42):
    """merge the split vertices of an imported mesh (so it can be inflated) and re-shade it by angle"""
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
    bm.to_mesh(o.data); bm.free()
    for pl in o.data.polygons:
        pl.use_smooth = True
    try:
        o.data.set_sharp_from_angle(angle=R(angle))
    except Exception:
        pass
    return o


def inflate(o, d=0.005):
    """push the surface out along its normals: thickens wire-thin parts so they survive at 64 px"""
    weld(o)
    return mod(o, 'DISPLACE', strength=d, mid_level=0.0)


def apply(o):
    """bake modifiers + transform into the mesh"""
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(o.evaluated_get(dg))
    me.transform(o.matrix_world)
    old = o.data
    o.modifiers.clear(); o.data = me; o.matrix_world = Matrix.Identity(4)
    if old.users == 0:
        bpy.data.meshes.remove(old)
    return o


def join(objs, name="grp"):
    objs = [o for o in objs if o is not None]
    bpy.context.view_layer.update()
    for o in objs:
        apply(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = objs[0]; o.name = name
    return o


def group(objs, name="grp"):
    """parent objects to an empty so the whole thing can be posed with place()"""
    e = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(e)
    for o in objs:
        if o is not None:
            o.parent = e
    return e


def lathe(profile, seg=40, mat=None, name="lathe", smooth=True, arc=360.0, cap=False):
    """revolve (radius, z) points round Z"""
    n = len(profile); full = arc >= 359.9
    cols = seg if full else seg + 1
    vs = []
    for j in range(cols):
        a = R(arc) * j / seg
        for (r, z) in profile:
            vs.append((r * math.cos(a), r * math.sin(a), z))
    fs = []
    for j in range(seg):
        j2 = (j + 1) % cols if full else j + 1
        for i in range(n - 1):
            fs.append((j * n + i, j2 * n + i, j2 * n + i + 1, j * n + i + 1))
    o = obj(name, vs, fs, mat, smooth)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    if cap:
        bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()
    return o


def ring_pts(c, rx, ry, seg, axes=None, power=2.0):
    """super-ellipse cross-section in the plane spanned by axes (u, v)"""
    u, v = axes if axes else (Vector((1, 0, 0)), Vector((0, 1, 0)))
    pts = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ca, sa = math.cos(a), math.sin(a)
        e = 2.0 / power
        x = math.copysign(abs(ca) ** e, ca) * rx; y = math.copysign(abs(sa) ** e, sa) * ry
        pts.append(Vector(c) + u * x + v * y)
    return pts


def loft(sections, seg=24, mat=None, name="loft", caps=(False, False), smooth=True):
    """skin a list of cross-sections: (centre, rx, ry) or (centre, rx, ry, (u, v)) or (centre, rx, ry, (u, v), power)"""
    vs = []
    for s in sections:
        axes = s[3] if len(s) > 3 else None
        pw = s[4] if len(s) > 4 else 2.0
        vs += ring_pts(s[0], s[1], s[2], seg, axes, pw)
    fs = []
    for k in range(len(sections) - 1):
        for i in range(seg):
            a = k * seg + i; b = k * seg + (i + 1) % seg
            fs.append((a, b, b + seg, a + seg))
    if caps[0]:
        fs.append(tuple(reversed(range(seg))))
    if caps[1]:
        base = (len(sections) - 1) * seg
        fs.append(tuple(range(base, base + seg)))
    o = obj(name, vs, fs, mat, smooth)
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    return o


def tube(points, radius=0.005, seg=10, mat=None, name="tube", caps=True, closed=False):
    """sweep a circle along a polyline (radius: number or list per point)"""
    pts = [Vector(p) for p in points]
    n = len(pts)
    rad = radius if isinstance(radius, (list, tuple)) else [radius] * n
    secs = []
    prev_u = None
    for i in range(n):
        if closed:
            t = (pts[(i + 1) % n] - pts[i - 1]).normalized()
        else:
            t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        ref = prev_u if prev_u is not None else (Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0)))
        u = (ref - t * ref.dot(t)).normalized(); v = t.cross(u)
        prev_u = u
        secs.append((pts[i], rad[i], rad[i], (u, v)))
    if closed:
        secs.append(secs[0])
    return loft(secs, seg, mat, name, caps=(caps and not closed, caps and not closed))


def arc_pts(r, a0, a1, n=24, z=0.0, plane='XZ', c=(0, 0, 0)):
    out = []
    for i in range(n + 1):
        a = R(a0 + (a1 - a0) * i / n)
        x, y = r * math.cos(a), r * math.sin(a)
        p = (x, 0, y) if plane == 'XZ' else ((x, y, 0) if plane == 'XY' else (0, x, y))
        out.append(Vector(c) + Vector(p) + (Vector((0, 0, z)) if plane == 'XY' else Vector((0, z, 0)) if plane == 'XZ' else Vector((z, 0, 0))))
    return out


def extrude(outline, depth=0.01, mat=None, name="plate", bev=0.0, smooth=False):
    """prism from a 2D outline given in the XZ plane (x, z); thickness along Y, centred"""
    n = len(outline)
    vs = [(x, -depth / 2, z) for (x, z) in outline] + [(x, depth / 2, z) for (x, z) in outline]
    fs = [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    o = obj(name, vs, fs, mat, smooth)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    for side in (0, 1):
        vv = [bm.verts[i + side * n] for i in range(n)]
        try:
            f = bm.faces.new(vv)
            bmesh.ops.triangulate(bm, faces=[f])
        except Exception:
            pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()
    if bev:
        bevel(o, bev, 2)
    return o


def box(sx, sy, sz, mat=None, name="box", bev=0.0):
    x, y, z = sx / 2, sy / 2, sz / 2
    vs = [(-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z), (-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z)]
    fs = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    o = obj(name, vs, fs, mat, False)
    if bev:
        bevel(o, bev, 2)
    return o


def ball(r=0.01, mat=None, name="ball", sub=3, squash=(1, 1, 1)):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    for p in me.polygons:
        p.use_smooth = True
    if mat is not None:
        me.materials.append(mat)
    o.scale = squash
    return o


def torus(Rm, r, mat=None, name="torus", seg=48, rseg=14, arc=360.0):
    pts = arc_pts(Rm, 0, arc, seg, plane='XY')
    if arc >= 359.9:
        pts = pts[:-1]
    return tube(pts, r, rseg, mat, name, caps=arc < 359.9, closed=arc >= 359.9)


def cone(r0, r1, h, mat=None, name="cone", seg=24, cap=True):
    return lathe([(0.0, 0.0), (r0, 0.0), (r1, h), (0.0, h)] if cap else [(r0, 0.0), (r1, h)], seg, mat, name)


def hull(points, mat=None, name="hull", smooth=False):
    """convex hull of a point set (faceted stones, shards, crystals)"""
    bm = bmesh.new()
    for p in points:
        bm.verts.new(tuple(p))
    bmesh.ops.convex_hull(bm, input=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    if mat is not None:
        me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = smooth
    return o


def crystal(r=0.01, h=0.04, sides=6, tip=0.35, mat=None, name="crystal", jitter=0.0, seed=1, base_tip=0.15):
    """prismatic crystal: polygonal shaft with a pointed top (and a short pointed base)"""
    rnd = random.Random(seed)
    pts = []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        rr = r * (1 + rnd.uniform(-jitter, jitter))
        pts.append((rr * math.cos(a), rr * math.sin(a), h * base_tip))
        pts.append((rr * math.cos(a), rr * math.sin(a), h * (1 - tip)))
    pts.append((r * rnd.uniform(-jitter, jitter), r * rnd.uniform(-jitter, jitter), h))
    pts.append((0, 0, 0))
    return hull(pts, mat, name)


def shard(r=0.02, mat=None, name="shard", seed=1, n=14, squash=(1, 1, 1.4)):
    rnd = random.Random(seed)
    pts = []
    for i in range(n):
        v = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, 1))).normalized() * r * rnd.uniform(0.75, 1.0)
        pts.append((v.x * squash[0], v.y * squash[1], v.z * squash[2]))
    return hull(pts, mat, name)


def chain(points, link=0.012, wire=0.0022, mat=None, name="chain"):
    """chain of torus links laid along a polyline (links alternate by 90 degrees)"""
    pts = [Vector(p) for p in points]
    # resample by arc length
    seglen = [(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)]
    total = sum(seglen); step = link * 1.35
    out = []; k = 0
    d = 0.0
    while d <= total and k < 400:
        acc = 0.0
        for i, sl in enumerate(seglen):
            if acc + sl >= d:
                t = (d - acc) / max(sl, 1e-9)
                p = pts[i].lerp(pts[i + 1], t); tan = (pts[i + 1] - pts[i]).normalized()
                break
            acc += sl
        else:
            p = pts[-1]; tan = (pts[-1] - pts[-2]).normalized()
        o = torus(link * 0.5, wire, mat, name, seg=14, rseg=6)
        o.scale = (1.35, 1.0, 1.0)
        q = tan.to_track_quat('X', 'Z')
        o.rotation_mode = 'QUATERNION'
        o.rotation_quaternion = q @ Euler((R(90) if k % 2 else 0.0, 0, 0)).to_quaternion()
        o.location = p
        out.append(o)
        d += step; k += 1
    return out


def flame(h=0.06, r=0.02, mat=None, name="flame", seed=1, lean=(0, 0, 0)):
    """teardrop flame tongue with a noisy silhouette"""
    rnd = random.Random(seed)
    prof = []
    n = 12
    for i in range(n + 1):
        t = i / n
        rad = r * math.sin(math.pi * min(1.0, t * 1.25) ** 0.75) * (1 - t) ** 0.55 if t < 1 else 0.0
        prof.append((max(rad, 0.0), h * t))
    o = lathe(prof, 16, mat or fire(), name)
    displace(o, r * 0.5, r * 1.6, seed)
    o.rotation_euler = Euler((R(lean[0] + rnd.uniform(-6, 6)), R(lean[1] + rnd.uniform(-6, 6)), R(rnd.uniform(0, 360))))
    return o


def sparks(n=14, box_min=(-0.05, -0.02, 0.0), box_max=(0.05, 0.02, 0.1), r=0.0012, col=(1.0, 0.55, 0.15), seed=1, strength=7.0):
    rnd = random.Random(seed)
    m = emit(col, strength)
    out = []
    for i in range(n):
        p = [rnd.uniform(box_min[k], box_max[k]) for k in range(3)]
        o = ball(r * rnd.uniform(0.6, 1.6), m, "spark", sub=1)
        o.location = p
        o["nofit"] = 1
        out.append(o)
    return out


# ------------------------------------------------------------------------------------------------ imported models
_GLB = {}


def _load_glb(name):
    """append a character GLB once; returns {object name: (mesh data, world matrix, armature object)} (objects are removed)"""
    if name in _GLB:
        return _GLB[name]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(CHARS, name + ".glb"))
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        o["glbsrc"] = 1; o["nofit"] = 1; o.hide_render = True
    arm = next((o for o in new if o.type == 'ARMATURE'), None)
    bones = {}
    if arm is not None:
        for b in arm.data.bones:
            bones[b.name] = (arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local)
    d = {"__bones__": bones}
    for o in new:
        if o.type == 'MESH' and o.name != "Icosphere" and not o.name.startswith("Icosphere"):
            me = o.data.copy(); me.transform(o.matrix_world); me.use_fake_user = True
            groups = [g.name for g in o.vertex_groups]
            wsum = {}
            if o.name.startswith("wpn"):
                for v in o.data.vertices:
                    for g in v.groups:
                        wsum[groups[g.group]] = wsum.get(groups[g.group], 0.0) + g.weight
            d[o.name.split(".")[0]] = (me, groups, wsum, o)
    _GLB[name] = (d, new)
    return _GLB[name]


def glb_part(char, part, hide_src=True):
    """a static copy of one mesh of a character GLB (world space, rest pose)"""
    d, new = _load_glb(char)
    for o in new:
        o.hide_render = True
        o["nofit"] = 1
    if part not in d:
        return None
    me = d[part][0].copy()
    o = bpy.data.objects.new("%s_%s" % (char, part), me)
    bpy.context.scene.collection.objects.link(o)
    return o


def glb_bones(char):
    return _load_glb(char)[0]["__bones__"]


def glb_cleanup():
    """drop the hidden source objects of imported GLBs from the scene (their mesh copies stay cached)"""
    for name, (d, new) in list(_GLB.items()):
        for o in new:
            try:
                bpy.data.objects.remove(o, do_unlink=True)
            except Exception:
                pass
        _GLB[name] = (d, [])


def weapon(char, part="wpn_1", tip_up=True):
    """hero weapon mesh re-centred: long axis along +Z (the end far from the gripping hand is up), its outward face
    (the side away from the hand) turned to the camera (-Y), width along X"""
    o = glb_part(char, part)
    if o is None:
        return None
    d, _ = _load_glb(char)
    wsum = d[part][2]; bones = d["__bones__"]
    hand = max(wsum, key=wsum.get) if wsum else None
    me = o.data
    n = len(me.vertices)
    co = np.empty(n * 3, dtype=np.float64); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
    c = (co.min(axis=0) + co.max(axis=0)) * 0.5
    w, v = np.linalg.eigh(np.cov((co - co.mean(axis=0)).T))
    ax_long = v[:, 2]; ax_thin = v[:, 0]
    grip = np.array(bones[hand][0]) if hand in bones else co.mean(axis=0)
    proj = (co - grip) @ ax_long
    if abs(proj.min()) > abs(proj.max()):
        ax_long = -ax_long
    if not tip_up:
        ax_long = -ax_long
    if (co.mean(axis=0) - grip) @ ax_thin < 0:
        ax_thin = -ax_thin                               # ax_thin now points out of the weapon's outward face
    ay = -ax_thin                                        # outward face looks at the camera (-Y)
    ax = np.cross(ay, ax_long); ax /= np.linalg.norm(ax)
    ay = np.cross(ax_long, ax)
    Rm = np.stack([ax, ay, ax_long])
    lo = ((co - c) @ ax_long).min(); hi = ((co - c) @ ax_long).max()
    centre = c + ax_long * (lo + hi) * 0.5
    M4 = Matrix.Identity(4)
    for r_ in range(3):
        for c_ in range(3):
            M4[r_][c_] = Rm[r_][c_]
    me.transform(M4 @ Matrix.Translation(-Vector(centre)))
    me.update()
    o["length"] = float(hi - lo)
    o["hand"] = hand or ""
    return o


def head_of(char, part="body_main", bones_kw=("head", "neck", "jaw", "skull"), thresh=0.45):
    """the head of a character mesh (vertices mostly weighted to head bones), re-centred"""
    o = glb_part(char, part)
    if o is None:
        return None
    d, _ = _load_glb(char)
    groups = d[part][1]; src = d[part][3]
    idx = {i for i, g in enumerate(groups) if any(k in g.lower() for k in bones_kw)}
    keep = set()
    for v in src.data.vertices:
        w = sum(g.weight for g in v.groups if g.group in idx)
        if w >= thresh:
            keep.add(v.index)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
    if len(bm.verts) == 0:
        bm.free(); return None
    c = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    bmesh.ops.translate(bm, verts=bm.verts, vec=-c)
    bm.to_mesh(o.data); bm.free()
    return o
