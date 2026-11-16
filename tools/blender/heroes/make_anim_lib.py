"""Build anim_lib.glb: UAL armature + all animations of UAL1 and UAL2 (duplicate A_TPose dropped).

Run: blender -b --factory-startup --python make_anim_lib.py
"""
import bpy, bmesh, sys, os, json, struct

SRC1 = r"Y:\assets_src\q\UAL1.glb"
SRC2 = r"Y:\assets_src\q\UAL2.glb"
OUT = r"Y:\game\assets\chars\anim_lib.glb"

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30  # source animations are authored at 30 fps -> keys land on whole frames


def import_glb(path):
    before = set(bpy.data.objects)
    acts_before = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before], [a for a in bpy.data.actions if a not in acts_before]


objs1, acts1 = import_glb(SRC1)
arm = next(o for o in objs1 if o.type == 'ARMATURE')
objs2, acts2 = import_glb(SRC2)
arm2 = next(o for o in objs2 if o.type == 'ARMATURE')

# sanity: identical rest pose between the two libraries
for b in arm.data.bones:
    b2 = arm2.data.bones[b.name]
    d = max(abs(a - c) for r1, r2 in zip(b.matrix_local, b2.matrix_local) for a, c in zip(r1, r2))
    assert d < 1e-5, (b.name, d)
    assert (b.parent.name if b.parent else None) == (b2.parent.name if b2.parent else None)
print("REST POSES MATCH", len(arm.data.bones))

names1 = {a.name for a in acts1}
keep2 = []
for a in acts2:
    base = a.name.split('.')[0] if a.name.endswith(('.001', '.002')) else a.name
    if base in names1:
        print("DROP duplicate from UAL2:", a.name)
        continue
    keep2.append(a)

# snap key times to whole frames (30 fps source; float error otherwise drops the last frame of some clips)
import numpy as np
snapped = 0
for a in acts1 + keep2:
    for layer in a.layers:
        for strip in layer.strips:
            for cb in strip.channelbags:
                for fc in cb.fcurves:
                    n = len(fc.keyframe_points)
                    co = np.empty(n * 2, dtype=np.float32)
                    fc.keyframe_points.foreach_get('co', co)
                    x = co[0::2]
                    rx = np.round(x)
                    assert np.abs(x - rx).max() < 0.02, (a.name, fc.data_path, float(np.abs(x - rx).max()))
                    if np.abs(x - rx).max() > 0:
                        snapped += 1
                        co[0::2] = rx
                        fc.keyframe_points.foreach_set('co', co)
                    fc.update()
print("fcurves snapped:", snapped)

# report key timing
frac = 0
for a in acts1 + keep2:
    fr = a.frame_range
    if abs(fr[1] - round(fr[1])) > 1e-3 or abs(fr[0]) > 1e-3:
        frac += 1
        print("NON-INTEGER RANGE", a.name, tuple(fr))
print("non integer ranges:", frac)

# the importer made one NLA track per animation on each armature; add UAL2's actions as tracks on armature 1
ad = arm.animation_data
existing_tracks = {t.name for t in ad.nla_tracks}
print("tracks on arm1:", len(existing_tracks))
for a in keep2:
    tr = ad.nla_tracks.new()
    tr.name = a.name
    st = tr.strips.new(a.name, 0, a)
    st.name = a.name
    try:
        if st.action_slot is None and len(a.slots):
            st.action_slot = a.slots[0]
    except Exception as e:
        print("slot assign", a.name, e)
    tr.mute = True
ad.action = None

# remove everything but armature 1
for o in list(bpy.data.objects):
    if o is not arm:
        bpy.data.objects.remove(o, do_unlink=True)
for a in list(bpy.data.actions):
    if a not in acts1 and a not in keep2:
        bpy.data.actions.remove(a)

# tiny skinned placeholder mesh (one triangle on the root) so importers build a skin/skeleton
me = bpy.data.meshes.new("placeholder")
me.from_pydata([(0, 0, 0), (0.01, 0, 0), (0, 0, 0.01)], [], [(0, 1, 2)])
ph = bpy.data.objects.new("placeholder", me)
scene.collection.objects.link(ph)
ph.parent = arm
vg = ph.vertex_groups.new(name="root")
vg.add([0, 1, 2], 1.0, 'REPLACE')
md = ph.modifiers.new("Armature", 'ARMATURE')
md.object = arm
# make sure every bone ends up as a joint of the skin: add zero-ish weights is not needed, exporter writes all bones

print("tracks total:", len(ad.nla_tracks), "actions:", len(bpy.data.actions))

bpy.ops.export_scene.gltf(
    filepath=OUT, export_format='GLB',
    export_animations=True, export_animation_mode='ACTIONS', export_nla_strips=True,
    export_force_sampling=True, export_frame_step=1,
    export_optimize_animation_size=False,
    export_anim_single_armature=True, export_reset_pose_bones=True,
    export_rest_position_armature=True,
    export_skins=True, export_def_bones=False, export_leaf_bone=False,
    export_yup=True, export_materials='NONE', export_cameras=False, export_lights=False,
)
print("WROTE", OUT, os.path.getsize(OUT))
