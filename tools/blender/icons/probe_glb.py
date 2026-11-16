"""Probe: list weapon meshes / structure of hero and monster GLBs (run through run_blender.ps1)."""
import bpy, sys, os
from mathutils import Vector
names = sys.argv[sys.argv.index("--") + 1:]
for nm in names:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    p = r"Y:\game\assets\chars\%s.glb" % nm
    bpy.ops.import_scene.gltf(filepath=p)
    print("=== %s" % nm)
    for o in bpy.data.objects:
        if o.type == 'ARMATURE':
            print(" ARM %s bones=%d scale=%s" % (o.name, len(o.data.bones), tuple(round(x, 3) for x in o.scale)))
        if o.type != 'MESH':
            continue
        me = o.data
        bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
        mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
        mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
        vg = [g.name for g in o.vertex_groups]
        mats = [m.name for m in me.materials if m]
        par = o.parent.name if o.parent else None
        print(" MESH %-22s v=%-6d par=%s pb=%s vg=%d %s mats=%s bbmin=%s bbmax=%s" % (
            o.name, len(me.vertices), par, o.parent_bone or '-', len(vg), vg[:3] if o.name.startswith('wpn') else '',
            mats, tuple(round(x, 2) for x in mn), tuple(round(x, 2) for x in mx)))
    for m in bpy.data.materials:
        if m.use_nodes:
            imgs = [n.image.name + ":%dx%d" % tuple(n.image.size) for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]
            print(" MAT %s %s" % (m.name, imgs))
