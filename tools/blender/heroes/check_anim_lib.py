"""Re-import anim_lib.glb into a clean scene; list animations; compare skeleton with UAL1.glb."""
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glbinfo

OUT = r"Y:\game\assets\chars\anim_lib.glb"
SRC1 = r"Y:\assets_src\q\UAL1.glb"
SRC2 = r"Y:\assets_src\q\UAL2.glb"

js = glbinfo.read_gltf_json(OUT)
j1 = glbinfo.read_gltf_json(SRC1)
j2 = glbinfo.read_gltf_json(SRC2)
names = [a['name'] for a in js['animations']]
src_names = [a['name'] for a in j1['animations']] + [a['name'] for a in j2['animations'] if a['name'] != 'A_TPose']
print("JSON animation count:", len(names), "expected", len(src_names))
print("missing:", sorted(set(src_names) - set(names)))
print("extra:", sorted(set(names) - set(src_names)))
print("dupes:", sorted({n for n in names if names.count(n) > 1}))
sig = glbinfo.skeleton_signature(js)
sig1 = glbinfo.skeleton_signature(j1)
d = glbinfo.compare_skeletons(sig1, sig)
print("skeleton diffs vs UAL1:", len(d), d[:10])
print("joints:", len(js['skins'][0]['joints']), "UAL1 joints:", len(j1['skins'][0]['joints']))
# armature object node transform
for n in js['nodes']:
    if n.get('name') == 'Armature':
        print("Armature node:", {k: v for k, v in n.items() if k != 'children'})
for n in j1['nodes']:
    if n.get('name') == 'Armature':
        print("UAL1 Armature node:", {k: v for k, v in n.items() if k != 'children'})

# durations: compare each animation's max input time with the source
def durations(j):
    out = {}
    for a in j['animations']:
        mx = 0.0
        for s in a['samplers']:
            acc = j['accessors'][s['input']]
            mx = max(mx, acc['max'][0])
        out[a['name']] = (mx, len(a['channels']))
    return out
dn = durations(js); d1 = durations(j1); d2 = durations(j2)
src = dict(d2); src.update(d1)
bad = 0
for k, (t, ch) in dn.items():
    if abs(src[k][0] - t) > 1e-3:
        bad += 1
        print("DURATION MISMATCH", k, src[k][0], t)
print("duration mismatches:", bad, " sample channel counts:", dn['Idle_Loop'][1], "src", src['Idle_Loop'][1])

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps = 30
bpy.ops.import_scene.gltf(filepath=OUT)
acts = sorted(a.name for a in bpy.data.actions)
print("BLENDER re-import action count:", len(acts))
print(", ".join(acts))
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
print("bones:", len(arm.data.bones), "arm loc/rot/scale", tuple(arm.location), tuple(arm.rotation_quaternion), tuple(arm.scale))
