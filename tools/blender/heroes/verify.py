"""Contract check of exported character .glb files (JSON level + clean Blender re-import).

blender -b --factory-startup --python verify.py -- [ids...]     (default: every hero_/npc_/form_ file)
Writes Y:\\tools\\blender\\heroes\\_manifest\\verify_<id>.json
"""
import bpy, sys, os, json, struct, glob
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glbinfo
from rigio import *

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ids = args or sorted(os.path.splitext(os.path.basename(p))[0] for p in glob.glob(os.path.join(OUT, "*.glb"))
                     if os.path.basename(p).startswith(("hero_", "npc_", "form_")))
ual = glbinfo.read_gltf_json(os.path.join(SRC, "UAL1.glb"))
sig_ual = glbinfo.skeleton_signature(ual)
PREFIX = ("skin_", "cloth_", "leather_", "metal_", "bone_", "rock_", "hair_", "emis_", "spectral_")      # contract prefixes


def image_sizes(path, js):
    """(name, mime, w, h) of embedded images"""
    out = []
    with open(path, 'rb') as f:
        f.seek(12)
        clen, ctype = struct.unpack('<II', f.read(8))
        f.seek(clen, 1)
        blen, btype = struct.unpack('<II', f.read(8))
        base = f.tell()
        for im in js.get('images', []):
            bv = js['bufferViews'][im['bufferView']]
            f.seek(base + bv.get('byteOffset', 0))
            data = f.read(bv['byteLength'])
            w = h = 0
            if data[:8] == b'\x89PNG\r\n\x1a\n':
                w, h = struct.unpack('>II', data[16:24])
            elif data[:2] == b'\xff\xd8':
                i = 2
                while i < len(data):
                    if data[i] != 0xFF:
                        i += 1
                        continue
                    mk_ = data[i + 1]
                    if mk_ in (0xC0, 0xC1, 0xC2):
                        h, w = struct.unpack('>HH', data[i + 5:i + 9])
                        break
                    ln = struct.unpack('>H', data[i + 2:i + 4])[0]
                    i += 2 + ln
            out.append((im.get('name', '?'), im.get('mimeType', '?'), w, h, len(data)))
    return out


allok = True
for cid in ids:
    path = os.path.join(OUT, cid + ".glb")
    js = glbinfo.read_gltf_json(path)
    problems = []
    sig = glbinfo.skeleton_signature(js)
    d = glbinfo.compare_skeletons(sig_ual, sig)
    if d:
        problems.append("skeleton differs: %s" % d[:5])
    if len(js['skins']) != 1:
        problems.append("skins: %d" % len(js['skins']))
    if js.get('animations'):
        problems.append("has animations")
    # joint order identical to UAL?
    jn = [js['nodes'][j]['name'] for j in js['skins'][0]['joints']]
    ju = [ual['nodes'][j]['name'] for j in ual['skins'][0]['joints']]
    if sorted(jn) != sorted(ju):
        problems.append("joint set differs")
    arm_node = next((n for n in js['nodes'] if n.get('name') == 'Armature'), None)
    if arm_node is None or any(k in arm_node for k in ('translation', 'rotation', 'scale')):
        problems.append("armature node transform not identity: %s" % arm_node)
    mesh_nodes = [n for n in js['nodes'] if 'mesh' in n]
    names = sorted(n['name'] for n in mesh_nodes)
    for n in mesh_nodes:
        if not (n['name'].startswith('body_') or n['name'].startswith('wpn_')):
            problems.append("bad mesh name " + n['name'])
        if 'skin' not in n:
            problems.append("mesh not skinned " + n['name'])
        if any(k in n for k in ('translation', 'rotation', 'scale')):
            problems.append("mesh node has transform " + n['name'])
    mats_ = [m['name'] for m in js.get('materials', [])]
    for m in mats_:
        if not m.startswith(PREFIX):
            problems.append("bad material prefix " + m)
    if len(mats_) > 6:
        problems.append("too many materials %d" % len(mats_))
    for m in js.get('materials', []):
        if m['name'].startswith('emis_'):
            ef = m.get('emissiveFactor', [0, 0, 0])
            st = m.get('extensions', {}).get('KHR_materials_emissive_strength', {}).get('emissiveStrength', 1.0)
            if max(ef) * st <= 0:
                problems.append("emis material without emission " + m['name'])
    imgs = image_sizes(path, js)
    # realism pass: the main atlases may be 2048 px (contract), nothing larger; the 12 MB file limit bounds the total
    big = [i for i in imgs if max(i[2], i[3]) > 2048]
    if big:
        problems.append("textures too large: %s" % big)
    if not all(('TANGENT' in p['attributes']) for me in js['meshes'] for p in me['primitives']
               if 'normalTexture' in js['materials'][p['material']]):
        problems.append("normal-mapped primitive without tangents")
    tris = 0
    for n in mesh_nodes:
        for p in js['meshes'][n['mesh']]['primitives']:
            tris += js['accessors'][p['indices']]['count'] // 3
    limit = 20000 if cid.startswith("npc_") else 35000
    if tris > limit:
        problems.append("triangles %d > %d" % (tris, limit))
    size = os.path.getsize(path)
    if size > 12 * 1024 * 1024:
        problems.append("file too large %d" % size)
    # influences <= 4 (only JOINTS_0)
    for me in js['meshes']:
        for p in me['primitives']:
            if 'JOINTS_1' in p['attributes']:
                problems.append("more than 4 influences in " + me['name'])
                break
    # clean re-import
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    nb = len(arm[0].data.bones) if arm else 0
    bnames = sorted(b.name for b in arm[0].data.bones) if arm else []
    if len(arm) != 1 or nb != 65 or bnames != sorted(sig_ual.keys()):
        problems.append("re-import armature mismatch (%d armatures, %d bones)" % (len(arm), nb))
    if len(bpy.data.actions):
        problems.append("re-import has actions")
    zmin = min((min(v.co.z for v in o.data.vertices) for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('body_')), default=0)
    if abs(zmin) > 0.03:
        problems.append("feet not on ground: min z %.3f" % zmin)
    res = dict(id=cid, ok=not problems, problems=problems, triangles=tris, size_bytes=size, meshes=names, materials=mats_,
               images=[dict(name=i[0], mime=i[1], w=i[2], h=i[3], bytes=i[4]) for i in imgs], bones=nb,
               extensions=js.get('extensionsUsed', []))
    with open(os.path.join(r"Y:\tools\blender\heroes\_manifest", "verify_%s.json" % cid), "w", encoding="utf8") as f:
        json.dump(res, f, indent=1)
    log("VERIFY %-18s %s tris=%d size=%.2fMB meshes=%s mats=%s imgs=%s ext=%s" % (
        cid, "OK" if not problems else "FAIL", tris, size / 1e6, names, mats_, [(i[2], i[3]) for i in imgs], js.get('extensionsUsed', [])))
    for p in problems:
        log("    PROBLEM:", p)
        allok = False
log("ALL OK" if allok else "SOME FAILED")
