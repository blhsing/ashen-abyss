import bpy, sys
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
print("ENGINES", [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items])
try:
    sc.render.engine = 'BLENDER_EEVEE'
    print("EEVEE id ok BLENDER_EEVEE")
except Exception as e:
    print("ERR", e)
    sc.render.engine = 'BLENDER_EEVEE_NEXT'
print("EXPORT PROPS")
props = bpy.ops.export_scene.gltf.get_rna_type().properties
for p in props:
    if p.identifier == 'rna_type':
        continue
    d = getattr(p, 'default', None)
    extra = ''
    if p.type == 'ENUM':
        extra = str([e.identifier for e in p.enum_items])
        d = p.default
    print("  ", p.identifier, p.type, d, extra)
print("IMPORT PROPS")
for p in bpy.ops.import_scene.gltf.get_rna_type().properties:
    if p.identifier == 'rna_type':
        continue
    d = getattr(p, 'default', None)
    extra = ''
    if p.type == 'ENUM':
        extra = str([e.identifier for e in p.enum_items])
    print("  ", p.identifier, p.type, d, extra)
print("BAKE types", [e.identifier for e in bpy.types.BakeSettings.bl_rna.properties['pass_filter'].enum_items] if 'pass_filter' in bpy.types.BakeSettings.bl_rna.properties else None)
prefs = bpy.context.preferences.addons.get('cycles')
print("cycles addon", prefs)
if prefs:
    cp = prefs.preferences
    for t in ('HIP', 'OPTIX', 'CUDA', 'ONEAPI', 'METAL'):
        try:
            cp.compute_device_type = t
            cp.get_devices()
            print("  devtype", t, [(d.name, d.type) for d in cp.devices])
        except Exception as e:
            print("  devtype", t, "ERR", e)
import multiprocessing
print("CPUS", multiprocessing.cpu_count())
