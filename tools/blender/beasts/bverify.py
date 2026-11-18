"""Verify exported beast .glb files against the rigs B-F contract. Pure python + numpy (no Blender needed):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\5.2\\python\\bin\\python.exe" -I Y:\\tools\\blender\\beasts\\bverify.py <id|model> ... [--src=dir] [--meta=dir] [--all]
Checks: the 8 clips by exact name, 30 fps keys, loops closed, root unkeyed, no NaN, triangle / bone / size budgets,
material prefixes, embedded JPEGs, <= 4 weights and normalised, ground contact in the rest pose and in every clip
(skinned on the CPU from the file), Death ends on the ground, planted-foot drift at the stated speeds (from the file),
meta fields. Results are merged into <meta dir>\\verify.json."""
import sys, os, json, struct, math
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[1:]
flags = [a for a in args if a.startswith('--')]
ids = [a if a.startswith('mon_') else 'mon_' + a for a in args if not a.startswith('--')]
SRC = r"Y:\game\assets\chars"
META = os.path.join(HERE, "out")
for f in flags:
    if f.startswith('--src='):
        SRC = f[6:]
    if f.startswith('--meta='):
        META = f[7:]
CLIPS = ("Idle", "Walk", "Run", "Attack1", "Attack2", "Hit", "Death", "Spawn")
LOOPS = ("Idle", "Walk", "Run")
PREFIX = ('skin_', 'hair_', 'bone_', 'leather_', 'metal_', 'rock_', 'cloth_', 'emis_', 'spectral_')
CT = {5120: ('b', 1), 5121: ('B', 1), 5122: ('h', 2), 5123: ('H', 2), 5125: ('I', 4), 5126: ('f', 4)}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


class Glb:
    def __init__(self, path):
        d = open(path, 'rb').read()
        assert d[:4] == b'glTF'
        n = struct.unpack('<I', d[12:16])[0]
        self.j = json.loads(d[20:20 + n].decode('utf8'))
        o = 20 + n
        self.bin = d[o + 8:o + 8 + struct.unpack('<I', d[o:o + 4])[0]]
        self.size = len(d)

    def acc(self, i):
        a = self.j['accessors'][i]
        bv = self.j['bufferViews'][a['bufferView']]
        fmt, sz = CT[a['componentType']]
        nc = NC[a['type']]
        off = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
        stride = bv.get('byteStride', 0)
        if stride and stride != sz * nc:
            out = np.zeros((a['count'], nc), dtype=np.dtype('<' + fmt))
            for k in range(a['count']):
                out[k] = np.frombuffer(self.bin, dtype=np.dtype('<' + fmt), count=nc, offset=off + k * stride)
        else:
            out = np.frombuffer(self.bin, dtype=np.dtype('<' + fmt), count=a['count'] * nc, offset=off).reshape(a['count'], nc)
        if a.get('normalized'):
            out = out.astype(np.float64) / {5121: 255.0, 5123: 65535.0}[a['componentType']]
        return out


def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def trs(t, r, s):
    m = np.eye(4)
    m[:3, :3] = qmat(r) * np.asarray(s)[None, :]
    m[:3, 3] = t
    return m


def verify(model):
    path = os.path.join(SRC, model + ".glb")
    res = dict(model=model, fail=[], warn=[])
    F = res['fail'].append
    W = res['warn'].append
    if not os.path.isfile(path):
        F("file missing")
        return res
    meta = {}
    mp = os.path.join(META, model + ".meta.json")
    if os.path.isfile(mp):
        meta = json.load(open(mp, encoding='utf8'))
    else:
        F("meta missing")
    g = Glb(path)
    j = g.j
    res['mb'] = round(g.size / 1048576.0, 2)
    b_mb = meta.get('budget_mb', 6.0); b_tri = meta.get('budget_tris', 15000); b_bone = meta.get('budget_bones', 60)
    if res['mb'] > b_mb:
        F("size %.2f MB > %.1f" % (res['mb'], b_mb))
    nodes = j['nodes']
    par = {}
    for i, n in enumerate(nodes):
        for c in n.get('children', []):
            par[c] = i
    if len(j.get('skins', [])) != 1:
        F("skins: %d" % len(j.get('skins', [])))
        return res
    skin = j['skins'][0]
    joints = skin['joints']
    res['bones'] = len(joints)
    if len(joints) > b_bone:
        F("bones %d > %d" % (len(joints), b_bone))
    ibm = g.acc(skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1).astype(np.float64)
    jname = [nodes[n].get('name', '') for n in joints]
    # ---- meshes
    tris = 0
    Vs = []; Js = []; Ws = []
    for mi, m in enumerate(j['meshes']):
        for p in m['primitives']:
            a = p['attributes']
            pos = g.acc(a['POSITION']).astype(np.float64)
            tris += (j['accessors'][p['indices']]['count'] if 'indices' in p else len(pos)) // 3
            if 'JOINTS_0' not in a:
                F("primitive without skin in mesh %s" % m.get('name'))
                continue
            if 'JOINTS_1' in a:
                F("more than 4 weights per vertex")
            for k in ('NORMAL', 'TANGENT', 'TEXCOORD_0'):
                if k not in a:
                    (W if k == 'TANGENT' else F)("no %s" % k)
            jj = g.acc(a['JOINTS_0']).astype(np.int64)
            ww = g.acc(a['WEIGHTS_0']).astype(np.float64)
            if not np.isfinite(pos).all() or not np.isfinite(ww).all():
                F("NaN in mesh data")
            if np.abs(ww.sum(1) - 1).max() > 0.01:
                F("weights not normalised (max dev %.3f)" % np.abs(ww.sum(1) - 1).max())
            Vs.append(pos); Js.append(jj); Ws.append(ww)
            mat = j['materials'][p['material']]['name'] if 'material' in p else '(none)'
            if not mat.startswith(PREFIX):
                F("material prefix: %s" % mat)
    res['tris'] = tris
    if tris > b_tri:
        F("triangles %d > %d" % (tris, b_tri))
    res['materials'] = sorted(set(m['name'] for m in j.get('materials', [])))
    for im in j.get('images', []):
        if im.get('mimeType') != 'image/jpeg' or 'bufferView' not in im:
            F("image %s is not an embedded JPEG" % im.get('name'))
    if not j.get('images'):
        F("no textures")
    V0 = np.vstack(Vs); J0 = np.vstack(Js); W0 = np.vstack(Ws)
    Vh = np.c_[V0, np.ones(len(V0))]

    # ---- animation
    anims = {a['name']: a for a in j.get('animations', [])}
    res['clips'] = {}
    for c in CLIPS:
        if c not in anims:
            F("clip missing: %s" % c)
    for c in anims:
        if c not in CLIPS:
            F("extra clip: %s" % c)
    order = []

    def visit(i):
        if i in order:
            return
        if i in par:
            visit(par[i])
        order.append(i)
    for i in range(len(nodes)):
        visit(i)
    base = [(np.array(n.get('translation', (0, 0, 0)), dtype=float), np.array(n.get('rotation', (0, 0, 0, 1)), dtype=float),
             np.array(n.get('scale', (1, 1, 1)), dtype=float)) for n in nodes]
    root_i = [n for n in joints if nodes[n].get('name') == 'root']
    if not root_i:
        F("no bone named root")

    def globals_at(chan, t):
        G = [None] * len(nodes)
        for i in order:
            T, R, S = base[i]
            c = chan.get(i)
            if c:
                if 'translation' in c:
                    ti, v = c['translation']; T = np.array([np.interp(t, ti, v[:, k]) for k in range(3)])
                if 'scale' in c:
                    ti, v = c['scale']; S = np.array([np.interp(t, ti, v[:, k]) for k in range(3)])
                if 'rotation' in c:
                    ti, v = c['rotation']
                    k = int(np.clip(np.searchsorted(ti, t, side='right') - 1, 0, len(ti) - 1))
                    k2 = min(k + 1, len(ti) - 1)
                    u = 0.0 if k2 == k else (t - ti[k]) / (ti[k2] - ti[k])
                    q0 = v[k]; q1 = v[k2]
                    if (q0 * q1).sum() < 0:
                        q1 = -q1
                    R = q0 * (1 - u) + q1 * u
                    R = R / np.linalg.norm(R)
            m = trs(T, R, S)
            G[i] = m if i not in par else G[par[i]] @ m
        return G

    def skinned(G):
        JM = np.array([G[n] @ ibm[k] for k, n in enumerate(joints)])
        out = np.zeros((len(V0), 3))
        for k in range(4):
            w = W0[:, k:k + 1]
            m = JM[J0[:, k]]
            out += w * np.einsum('nij,nj->ni', m, Vh)[:, :3]
        return out
    G0 = globals_at({}, 0.0)
    rest = skinned(G0)
    res['rest_min_y'] = round(float(rest[:, 1].min()), 3)
    hover = bool(meta.get('hover_in_model'))
    buried = bool(meta.get('buried'))               # burrowers keep their base below the ground in every clip
    res['hover'] = hover
    C = {}
    for name, a in anims.items():
        chan = {}
        tmax = 0.0
        ok30 = True
        for ch in a['channels']:
            s = a['samplers'][ch['sampler']]
            ti = g.acc(s['input']).astype(np.float64).ravel()
            v = g.acc(s['output']).astype(np.float64)
            if not np.isfinite(v).all() or not np.isfinite(ti).all():
                F("NaN in clip %s" % name)
            if s.get('interpolation', 'LINEAR') not in ('LINEAR', 'STEP'):
                F("%s: interpolation %s" % (name, s.get('interpolation')))
            tmax = max(tmax, float(ti[-1]))
            if abs(ti[0]) > 1e-4:
                F("%s: first key at %.3f s" % (name, ti[0]))
            fr = ti * 30.0
            if np.abs(fr - np.round(fr)).max() > 0.02:
                ok30 = False
            chan.setdefault(ch['target']['node'], {})[ch['target']['path']] = (ti, v)
        if not ok30:
            F("%s: keys are not on 30 fps frames" % name)
        C[name] = (chan, tmax)
        res['clips'][name] = round(tmax, 3)
        md = meta.get('clips', {}).get(name)
        if md is not None and abs(md - tmax) > 0.02:
            F("%s: duration %.3f differs from meta %.3f" % (name, tmax, md))
        for ri in root_i:
            for pth, (ti, v) in chan.get(ri, {}).items():
                if np.abs(v - v[0]).max() > 1e-5:
                    F("%s: root bone is keyed (%s)" % (name, pth))
        for n in chan:
            if n not in joints:
                W("%s: channel on non-joint node %s" % (name, nodes[n].get('name')))
        if name in LOOPS:
            worst = 0.0
            for n, c in chan.items():
                for pth, (ti, v) in c.items():
                    d = np.abs(v[0] - v[-1]).max()
                    if pth == 'rotation':
                        d = min(d, np.abs(v[0] + v[-1]).max())
                    worst = max(worst, float(d))
            if worst > 2e-3:
                F("%s: loop not closed (%.4f)" % (name, worst))
    # ---- ground / bounds per clip
    res['min_y'] = {}
    for name in CLIPS:
        if name not in C:
            continue
        chan, tmax = C[name]
        n = int(round(tmax * 30))
        lo = 9.0
        for f in sorted(set(int(round(x)) for x in np.linspace(0, n, 9))):
            P = skinned(globals_at(chan, f / 30.0))
            lo = min(lo, float(P[:, 1].min()))
            if f == n and name == 'Death':
                res['death_end_min_y'] = round(float(P[:, 1].min()), 3)
                res['death_end_max_y'] = round(float(P[:, 1].max()), 3)
            if f == 0 and name == 'Idle':
                res['idle_min_y'] = round(float(P[:, 1].min()), 3)
                res['idle_max_y'] = round(float(P[:, 1].max()), 3)
        res['min_y'][name] = round(lo, 3)
        if name != 'Spawn' and lo < -0.035 and not meta.get('under_ok', {}).get(name) and not buried:
            F("%s: mesh goes %.3f m below the ground" % (name, -lo))
    if 'idle_min_y' in res:
        if hover:
            fh = meta.get('fly_height', 0.0)
            if res['idle_min_y'] < 0.05:
                F("hover_in_model but Idle touches the ground (min y %.3f)" % res['idle_min_y'])
            if not fh:
                F("meta fly_height missing")
        elif abs(res['idle_min_y']) > 0.02 and not buried:
            F("Idle not on the ground: min y %.3f" % res['idle_min_y'])
    if 'death_end_min_y' in res and abs(res['death_end_min_y']) > 0.03 and not meta.get('death_floats') and not buried:
        F("Death does not end on the ground: min y %.3f" % res['death_end_min_y'])
    # ---- planted feet
    res['foot_drift'] = {}
    cons = meta.get('contacts', [])
    for name, key in (('Walk', 'walk_speed'), ('Run', 'run_speed')):
        if name not in C or not cons:
            continue
        v = float(meta.get(key, 0.0))
        chan, tmax = C[name]
        n = int(round(tmax * 30))
        Gs = [globals_at(chan, f / 30.0) for f in range(n + 1)]
        worst = 0.0
        for c in cons:
            if c['bone'] not in jname:
                F("contact bone %s not in the file" % c['bone'])
                continue
            k = jname.index(c['bone'])
            x, y, z = c['p']
            ph = np.array([x, z, -y, 1.0])
            P = np.array([(G[joints[k]] @ ibm[k] @ ph)[:3] for G in Gs])
            Pw = P.copy()
            Pw[:, 2] += v * np.arange(n + 1) / 30.0
            st = P[:, 1] < P[:, 1].min() + 0.012
            f = 0
            while f <= n:
                if st[f]:
                    e = f
                    while e + 1 <= n and st[e + 1]:
                        e += 1
                    if e > f:
                        seg = Pw[f:e + 1][:, [0, 2]]
                        worst = max(worst, float(np.linalg.norm(seg.max(0) - seg.min(0))))
                    f = e + 1
                else:
                    f += 1
        res['foot_drift'][name] = round(worst, 4)
        if worst > 0.03:
            F("%s: planted feet slide %.3f m at %.2f m/s" % (name, worst, v))
    for k in ('walk_speed', 'run_speed', 'attack1_hit_time', 'attack2_hit_time', 'attack_reach', 'radius', 'height'):
        if k not in meta:
            F("meta: %s missing" % k)
    for k, c in (('attack1_hit_time', 'Attack1'), ('attack2_hit_time', 'Attack2')):
        if k in meta and c in res['clips'] and not (0.05 <= meta[k] <= res['clips'][c]):
            F("meta %s %.2f outside the clip (%.2f s)" % (k, meta[k], res['clips'][c]))
    return res


if '--all' in flags:
    ids = sorted(f[:-10] for f in os.listdir(META) if f.endswith('.meta.json'))
allres = {}
vp = os.path.join(META, "verify.json")
if os.path.isfile(vp):
    allres = json.load(open(vp, encoding='utf8'))
for m in ids:
    try:
        r = verify(m)
    except Exception as e:
        import traceback
        traceback.print_exc()
        r = dict(model=m, fail=["exception %r" % e], warn=[])
    allres[m] = r
    print("VERIFY %-24s %s tris=%s bones=%s MB=%s drift=%s minY=%s" % (m, "PASS" if not r['fail'] else "FAIL", r.get('tris'), r.get('bones'),
                                                                  r.get('mb'), r.get('foot_drift'), r.get('idle_min_y')))
    for x in r['fail']:
        print("    FAIL:", x)
    for x in r['warn'][:6]:
        print("    warn:", x)
with open(vp, 'w', encoding='utf8') as f:
    json.dump(allres, f, indent=1)
