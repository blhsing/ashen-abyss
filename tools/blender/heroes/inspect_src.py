"""Inspect source assets: armature, bones, transforms, animations, meshes."""
import bpy, sys, os
from mathutils import Vector

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def dump(path, bones=False):
    reset()
    bpy.ops.import_scene.gltf(filepath=path)
    print("=" * 70)
    print("FILE", path)
    for o in bpy.data.objects:
        print("OBJ", repr(o.name), o.type, "parent=", o.parent.name if o.parent else None,
              "loc", tuple(round(v, 4) for v in o.location),
              "rot", tuple(round(v, 4) for v in o.rotation_euler) if o.rotation_mode != 'QUATERNION' else tuple(round(v, 4) for v in o.rotation_quaternion),
              o.rotation_mode,
              "scl", tuple(round(v, 4) for v in o.scale))
        if o.type == 'MESH':
            me = o.data
            me.calc_loop_triangles()
            ws = [o.matrix_world @ v.co for v in me.vertices]
            mn = Vector((min(v.x for v in ws), min(v.y for v in ws), min(v.z for v in ws)))
            mx = Vector((max(v.x for v in ws), max(v.y for v in ws), max(v.z for v in ws)))
            print("   verts", len(me.vertices), "tris", len(me.loop_triangles), "bbox", tuple(round(v, 3) for v in mn), tuple(round(v, 3) for v in mx))
            print("   mats", [m.name if m else None for m in me.materials], "uv", [u.name for u in me.uv_layers], "vgroups", len(o.vertex_groups))
            print("   mods", [(m.type, m.name) for m in o.modifiers])
            print("   colattrs", [a.name for a in me.color_attributes], "shapekeys", bool(me.shape_keys))
        if o.type == 'ARMATURE':
            print("   bones", len(o.data.bones))
            if bones:
                for b in o.data.bones:
                    print("   BONE", b.name, "parent=", b.parent.name if b.parent else None,
                          "head", tuple(round(v, 4) for v in b.head_local), "tail", tuple(round(v, 4) for v in b.tail_local),
                          "deform", b.use_deform)
    print("ACTIONS", len(bpy.data.actions))
    for a in bpy.data.actions:
        fr = a.frame_range
        print("   ACT", a.name, tuple(fr), "slots", [s.name_display for s in a.slots] if hasattr(a, 'slots') else None)
    for m in bpy.data.materials:
        print("MAT", m.name)
        if m.node_tree:
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image:
                    print("    IMG", n.image.name, tuple(n.image.size), n.image.filepath)
    print("FPS", bpy.context.scene.render.fps)

args = sys.argv[sys.argv.index('--') + 1:]
for i, p in enumerate(args):
    dump(p, bones=(i == 0))
