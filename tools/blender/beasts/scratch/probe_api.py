"""API probe (Blender 5.2): action/fcurve creation, NLA export, clip names in the .glb."""
import bpy, sys, os, json, struct, numpy as np
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.fps = 30
ad = bpy.data.armatures.new("Armature")
arm = bpy.data.objects.new("Armature", ad)
sc.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = {}
for n, h, t, p in (("root", (0, 0, 0), (0, 0.1, 0), None), ("body", (0, 0, 0.5), (0, -0.3, 0.5), "root"), ("head", (0, -0.3, 0.5), (0, -0.5, 0.5), "body")):
    b = ad.edit_bones.new(n); b.head = h; b.tail = t; b.roll = 0
    if p: b.parent = eb[p]
    eb[n] = b
bpy.ops.object.mode_set(mode='OBJECT')
me = bpy.data.meshes.new("m")
me.from_pydata([(0, 0, 0.5), (0.1, -0.4, 0.5), (0, -0.4, 0.6)], [], [(0, 1, 2)])
ob = bpy.data.objects.new("body_main", me); sc.collection.objects.link(ob); ob.parent = arm
for i, n in enumerate(("root", "body", "head")):
    vg = ob.vertex_groups.new(name=n); vg.add([i], 1.0, 'REPLACE')
md = ob.modifiers.new("Armature", 'ARMATURE'); md.object = arm
for pb in arm.pose.bones: pb.rotation_mode = 'QUATERNION'
arm.animation_data_create()
A = arm.animation_data
print("PROBE action attrs:", [x for x in dir(bpy.types.Action) if 'fcurve' in x or 'slot' in x or 'layer' in x])
def mk(name, nf, amp):
    act = bpy.data.actions.new(name)
    A.action = act
    way = None
    for bn in ("body", "head"):
        for path, n, vals in (("rotation_quaternion", 4, None), ("location", 3, None), ("scale", 3, None)):
            for i in range(n):
                dp = 'pose.bones["%s"].%s' % (bn, path)
                fc = None
                if hasattr(act, 'fcurve_ensure_for_datablock'):
                    fc = act.fcurve_ensure_for_datablock(arm, dp, index=i); way = 'ensure'
                else:
                    fc = act.fcurves.new(dp, index=i, action_group=bn); way = 'legacy'
                fc.keyframe_points.add(nf)
                co = np.zeros(nf * 2, dtype=np.float32)
                co[0::2] = np.arange(nf)
                t = np.arange(nf) / (nf - 1)
                if path == "rotation_quaternion":
                    ang = amp * np.sin(t * 2 * np.pi)
                    co[1::2] = np.cos(ang / 2) if i == 0 else (np.sin(ang / 2) if i == 1 else 0)
                elif path == "scale":
                    co[1::2] = 1.0 + (0.3 * np.sin(t * 2 * np.pi) if bn == "head" else 0)
                else:
                    co[1::2] = 0.1 * np.sin(t * 2 * np.pi) if i == 2 else 0
                fc.keyframe_points.foreach_set('co', co)
                for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'
                fc.update()
    print("PROBE", name, "way", way, "slots", len(act.slots), "range", tuple(act.frame_range), "slot", A.action_slot)
    slot = A.action_slot
    A.action = None
    tr = A.nla_tracks.new(); tr.name = name
    st = tr.strips.new(name, 0, act); st.name = name
    try:
        if st.action_slot is None and len(act.slots): st.action_slot = act.slots[0]
    except Exception as e: print("PROBE slot", e)
    tr.mute = True
    return act
mk("Idle", 61, 0.3); mk("Walk", 31, 0.6); mk("Attack1", 25, 1.0)
out = r"Y:\tools\blender\beasts\scratch\probe.glb"
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); ob.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', use_selection=True,
    export_animations=True, export_animation_mode='ACTIONS', export_nla_strips=True,
    export_force_sampling=True, export_frame_step=1, export_optimize_animation_size=True,
    export_optimize_animation_keep_anim_armature=True,
    export_anim_single_armature=True, export_reset_pose_bones=True, export_rest_position_armature=True,
    export_skins=True, export_def_bones=False, export_leaf_bone=False, export_yup=True, export_apply=True,
    export_materials='EXPORT', export_tangents=False)
with open(out, 'rb') as f:
    f.read(12); clen, _ = struct.unpack('<II', f.read(8)); js = json.loads(f.read(clen).decode('utf8'))
for a in js.get('animations', []):
    ch = {}
    for c in a['channels']:
        ch.setdefault(js['nodes'][c['target']['node']]['name'], []).append(c['target']['path'])
    lens = set(js['accessors'][s['input']]['count'] for s in a['samplers'])
    print("PROBE anim", a['name'], ch, "key counts", lens, "max t", max(js['accessors'][s['input']]['max'][0] for s in a['samplers']))
print("PROBE nodes", [(n['name'], n.get('translation'), n.get('rotation')) for n in js['nodes']])
print("PROBE DONE")
