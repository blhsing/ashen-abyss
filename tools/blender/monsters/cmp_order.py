import sys, os, json, struct
def joints(path):
    with open(path, 'rb') as f:
        f.read(12); clen, ctype = struct.unpack('<II', f.read(8)); js = json.loads(f.read(clen).decode('utf8'))
    return [js['nodes'][j]['name'] for j in js['skins'][0]['joints']], js
a, ja = joints(r"Y:\assets_src\q\UAL1.glb")
b, jb = joints(r"Y:\game\assets\chars\mon_skeleton.glb")
c, jc = joints(r"Y:\game\assets\chars\anim_lib.glb")
print("UAL1 :", a[:14])
print("mine :", b[:14])
print("anim :", c[:14])
print("mine==anim_lib order:", b == c, " mine==UAL1:", a == b)
print("scene nodes mine:", [jb['nodes'][i]['name'] for i in jb['scenes'][0]['nodes']])
arm = [n for n in jb['nodes'] if n['name'] == 'Armature'][0]
print("Armature children:", [jb['nodes'][i]['name'] for i in arm.get('children', [])], {k: v for k, v in arm.items() if k not in ('children', 'name')})
print("materials:", json.dumps(jb['materials'])[:1500])
print("images:", jb.get('images'), "textures:", jb.get('textures'), "samplers", jb.get('samplers'))
print("meshes:", [(m['name'], len(m['primitives'])) for m in jb['meshes']])
print("extensionsUsed:", jb.get('extensionsUsed'))
