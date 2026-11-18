"""Contract check of every exported creature + manifest writer.
   blender -b --factory-startup --python verify.py -- [id ...]      (no ids = all mon_*/boss_* .glb on disk)
Checks against UAL1.glb: joint names / hierarchy / rest TRS / inverse bind matrices, no animations, mesh object
names (body_* / wpn_*), material prefixes (<=5), texture sizes (<=2048, embedded JPEG), triangle budgets, feet on ground,
file size (<=10 MB), tangents present.
Writes Y:\\game\\assets\\chars\\manifest_monsters.json"""
import sys, os, json, struct, glob
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bpy, numpy as np
import monlib as M

PREFIX = ('skin_', 'cloth_', 'leather_', 'metal_', 'bone_', 'rock_', 'emis_', 'spectral_')
# per-creature limits that differ from the defaults (triangles, file size in MB, materials). boss_osaros v2 is the final boss: he
# appears alone and in close-up on the title screen, so the lead gave him 45k triangles, ~12 MB and three atlases (11 materials).
LIMITS = {'boss_osaros': dict(tris=45000, mb=12.6, mats=12)}
# giants of the bestiary (body plan "giant / construct", engine scale >= 1.75): the lead allows them 22,000 triangles
LIMITS.update({k: dict(tris=22000) for k in ('mon_ash_colossus', 'mon_engine_carrier', 'mon_hauler_yoke', 'mon_hauler_hod', 'mon_hauler_collector',
                                             'mon_hauler_stoker')})


def read_glb(path):
    with open(path, 'rb') as f:
        magic, ver, length = struct.unpack('<III', f.read(12))
        clen, ctype = struct.unpack('<II', f.read(8))
        js = json.loads(f.read(clen).decode('utf8'))
        rest = f.read()
    binchunk = b''
    if len(rest) >= 8:
        blen, btype = struct.unpack('<II', rest[:8])
        binchunk = rest[8:8 + blen]
    return js, binchunk


def accessor(js, binchunk, idx):
    a = js['accessors'][idx]
    bv = js['bufferViews'][a['bufferView']]
    off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    n = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}[a['type']]
    dt = {5126: np.float32, 5123: np.uint16, 5125: np.uint32, 5121: np.uint8}[a['componentType']]
    return np.frombuffer(binchunk, dtype=dt, count=a['count'] * n, offset=off).reshape(a['count'], n)


def skel_sig(js, binchunk):
    nodes = js['nodes']
    parent = {}
    for i, n in enumerate(nodes):
        for c in n.get('children', []):
            parent[c] = i
    skin = js['skins'][0]
    ibm = accessor(js, binchunk, skin['inverseBindMatrices'])
    sig = {}
    for k, j in enumerate(skin['joints']):
        n = nodes[j]
        p = parent.get(j)
        sig[n['name']] = (nodes[p]['name'] if p is not None else None, np.array(n.get('translation', (0, 0, 0))),
                          np.array(n.get('rotation', (0, 0, 0, 1))), np.array(n.get('scale', (1, 1, 1))), ibm[k])
    order = [nodes[j]['name'] for j in skin['joints']]
    return sig, order


def check(path, ref_sig, ref_order, ref_bones):
    cid = os.path.splitext(os.path.basename(path))[0]
    errs = []; warn = []
    js, binchunk = read_glb(path)
    size = os.path.getsize(path)
    lim = LIMITS.get(cid, {})
    if size > lim.get('mb', 10) * 1024 * 1024:
        errs.append("file larger than %s MB: %d" % (lim.get('mb', 10), size))
    if js.get('animations'):
        errs.append("contains animations")
    if len(js.get('skins', [])) != 1:
        errs.append("expected exactly 1 skin, got %d" % len(js.get('skins', [])))
    sig, order = skel_sig(js, binchunk)
    if set(sig) != set(ref_sig):
        errs.append("joint set differs: missing %s extra %s" % (sorted(set(ref_sig) - set(sig)), sorted(set(sig) - set(ref_sig))))
    worst_t = worst_r = worst_i = 0.0
    for k, (p, t, r, s, ibm) in ref_sig.items():
        if k not in sig:
            continue
        p2, t2, r2, s2, ibm2 = sig[k]
        if p != p2:
            errs.append("parent differs for %s: %s vs %s" % (k, p, p2))
        worst_t = max(worst_t, float(np.abs(t - t2).max()), float(np.abs(s - s2).max()))
        worst_r = max(worst_r, float(min(np.abs(r - r2).max(), np.abs(r + r2).max())))
        worst_i = max(worst_i, float(np.abs(ibm - ibm2).max()))
    if order != ref_order:
        warn.append("skin joint order differs from UAL1.glb (names/hierarchy identical)")
    if worst_t > 1e-4 or worst_r > 1e-4 or worst_i > 1e-3:
        errs.append("rest pose differs from UAL: dT=%.2g dR=%.2g dIBM=%.2g" % (worst_t, worst_r, worst_i))
    # armature node transform
    for n in js['nodes']:
        if n.get('name') == 'Armature':
            if any(k in n for k in ('translation', 'rotation', 'scale', 'matrix')):
                tr = np.array(n.get('translation', (0, 0, 0))); sc = np.array(n.get('scale', (1, 1, 1))); ro = np.array(n.get('rotation', (0, 0, 0, 1)))
                if np.abs(tr).max() > 1e-6 or np.abs(sc - 1).max() > 1e-6 or np.abs(np.abs(ro) - np.array([0, 0, 0, 1])).max() > 1e-6:
                    errs.append("Armature node has a non-identity transform")
    mats = [m['name'] for m in js.get('materials', [])]
    for m in mats:
        if not m.startswith(PREFIX):
            errs.append("material prefix: " + m)
    if len(mats) > lim.get('mats', 5):
        errs.append("more than %d materials: %s" % (lim.get('mats', 5), mats))
    emis = [m for m in js.get('materials', []) if m['name'].startswith('emis_')]
    for m in emis:
        ef = m.get('emissiveFactor', [0, 0, 0])
        if max(ef) <= 0:
            errs.append("emis material without emission: " + m['name'])
    for im in js.get('images', []):
        if 'uri' in im:
            errs.append("texture not embedded: %s" % im['uri'])
        elif im.get('mimeType') != 'image/jpeg':
            warn.append("texture is not JPEG: %s (%s)" % (im.get('name'), im.get('mimeType')))
    notan = sum(1 for me in js.get('meshes', []) for pr in me.get('primitives', []) if 'TANGENT' not in pr.get('attributes', {}))
    if notan:
        warn.append("%d primitives without tangents" % notan)
    # the game shader ADDS emission texture and emission colour: with a texture the factor must be white (see looks.py)
    for m in emis:
        if 'emissiveTexture' in m and min(m.get('emissiveFactor', [0, 0, 0])) < 0.99:
            errs.append("emis material with an emission texture AND a coloured emissiveFactor: " + m['name'])
    # the normal map must keep full chroma resolution (JPEG 4:4:4)
    nimg = set()
    for m in js.get('materials', []):
        t = m.get('normalTexture', {}).get('index')
        if t is not None:
            nimg.add(js['textures'][t].get('source'))
    for i in nimg:
        im = js['images'][i]
        if 'bufferView' in im and im.get('mimeType') == 'image/jpeg':
            bv = js['bufferViews'][im['bufferView']]
            data = binchunk[bv.get('byteOffset', 0):bv.get('byteOffset', 0) + bv['byteLength']]
            k = 2
            while k + 4 <= len(data) and data[k] == 0xFF:
                mk = data[k + 1]
                ln = struct.unpack('>H', data[k + 2:k + 4])[0]
                if mk in (0xC0, 0xC1, 0xC2):
                    if data[k + 11] != 0x11:
                        warn.append("normal map %s is chroma-subsampled (JPEG sampling %02x)" % (im.get('name'), data[k + 11]))
                    break
                k += 2 + ln
    # ---- re-import in a clean scene
    M.reset_scene()
    objs = M.import_gltf(path)
    arms = [o for o in objs if o.type == 'ARMATURE']
    meshes = [o for o in objs if o.type == 'MESH']
    if len(arms) != 1:
        errs.append("armatures: %d" % len(arms))
    names = [b.name for b in arms[0].data.bones]
    if names != ref_bones:
        if set(names) != set(ref_bones):
            errs.append("blender bone names differ")
        else:
            warn.append("bone order differs (names identical)")
    if len(bpy.data.actions):
        errs.append("actions after import: %d" % len(bpy.data.actions))
    tris = 0
    mn = np.array([1e9] * 3); mx = -mn
    mobj = {}
    for o in meshes:
        if not (o.name.startswith('body_') or o.name.startswith('wpn_')):
            errs.append("mesh object name: " + o.name)
        me = o.data
        me.calc_loop_triangles()
        t = len(me.loop_triangles)
        tris += t
        co = np.empty(len(me.vertices) * 3, dtype=np.float32)
        me.vertices.foreach_get('co', co)
        co = co.reshape(-1, 3)
        Mw = np.array(o.matrix_world)
        co = co @ Mw[:3, :3].T + Mw[:3, 3]
        mn = np.minimum(mn, co.min(0)); mx = np.maximum(mx, co.max(0))
        mobj[o.name] = dict(triangles=t, materials=[m.name for m in me.materials])
        if not any(md.type == 'ARMATURE' for md in o.modifiers):
            errs.append("mesh without armature modifier: " + o.name)
        # every vertex weighted
        ng = np.array([len(v.groups) for v in me.vertices])
        if (ng == 0).any():
            errs.append("%s: %d unweighted vertices" % (o.name, int((ng == 0).sum())))
        if o.name.startswith('wpn_'):
            gs = set()
            for v in me.vertices:
                for g in v.groups:
                    if g.weight > 1e-4:
                        gs.add(o.vertex_groups[g.group].name)
            if len(gs) != 1:
                warn.append("%s bound to %s" % (o.name, sorted(gs)))
            else:
                mobj[o.name]['bone'] = list(gs)[0]
    budget = lim.get('tris', 40000 if cid.startswith('boss_') else 15000)
    if tris > budget:
        errs.append("triangles %d > %d" % (tris, budget))
    if mn[2] < -0.01 or mn[2] > 0.03:
        if not cid in ('mon_wraith', 'mon_choir'):
            errs.append("feet not on the ground: min z %.3f" % mn[2])
    imgs = []
    for im in bpy.data.images:
        if im.size[0] == 0:
            continue
        imgs.append([im.name, im.size[0], im.size[1]])
        if max(im.size) > 2048:
            errs.append("texture larger than 2048: %s %s" % (im.name, tuple(im.size)))
    notes = ""
    wj = os.path.join(M.WORK, cid + ".json")
    if os.path.exists(wj):
        with open(wj, 'r', encoding='utf8') as f:
            notes = json.load(f).get('notes', "")
    entry = dict(id=cid, file=cid + ".glb", bytes=size, triangles=tris, materials=mats, meshes=mobj,
                 height_m=round(float(mx[2]), 3),
                 bbox_min=[round(float(x), 3) for x in mn], bbox_max=[round(float(x), 3) for x in mx],
                 textures=imgs, bones=len(names), notes=notes)
    return entry, errs, warn


def main():
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    write = '--nomanifest' not in args
    ids = [a for a in args if not a.startswith('--')]
    if ids:
        paths = [os.path.join(M.OUT_DIR, i + ".glb") for i in ids]
    else:
        paths = sorted(glob.glob(os.path.join(M.OUT_DIR, "mon_*.glb")) + glob.glob(os.path.join(M.OUT_DIR, "boss_*.glb")))
    js, bc = read_glb(M.UAL1)
    ref_sig, ref_order = skel_sig(js, bc)
    M.reset_scene()
    arm = M.load_rig()
    ref_bones = [b.name for b in arm.data.bones]
    entries = []
    bad = 0
    for p in paths:
        e, errs, warn = check(p, ref_sig, ref_order, ref_bones)
        entries.append(e)
        st = "OK " if not errs else "FAIL"
        bad += bool(errs)
        print("VERIFY %s %-22s tris=%5d  %.2f MB  h=%.2f  mats=%s  objs=%s" % (
            st, e['id'], e['triangles'], e['bytes'] / 1048576.0, e['height_m'], ",".join(e['materials']), ",".join(e['meshes'])))
        for x in errs:
            print("    ERROR:", x)
        for x in warn:
            print("    warn:", x)
    if write and not ids:
        out = dict(rig="Quaternius Universal Animation Library armature (65 bones, unmodified)", units="metres, +Y up (glTF)",
                   facing="+Z (glTF) / -Y (Blender), same as the UAL mannequin",
                   material_prefixes=list(PREFIX), creatures=entries)
        with open(os.path.join(M.OUT_DIR, "manifest_monsters.json"), 'w', encoding='utf8') as f:
            json.dump(out, f, indent=1, ensure_ascii=False)
        print("MANIFEST written with", len(entries), "creatures")
    print("VERIFY DONE: %d checked, %d failed" % (len(entries), bad))


main()
