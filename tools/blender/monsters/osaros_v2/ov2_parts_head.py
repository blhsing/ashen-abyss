"""Osaros v2: the head group (objects body_face + body_crown, atlas 'head')."""
import math
import numpy as np
import monlib as M
import kit_cloth as KC
import ov2_sculpt as SC
import ov2_dense as DN
from monlib import V, tube, ellipsoid, box, join, horn, revolve, loft, lerp, unit

BREACH_C = DN.BREACH[0][0]
BREACH_R = 0.052
NECK_CUT = 1.500
CAV_FLOOR = (0.040, -0.030, 1.646)

HEAD_S = 1.21                                  # the finished head is drawn over life size (about the base of the neck)
HEAD_PIV = np.array([0.0, 0.004, 1.500])


def hs(pt):
    """a point of the sculpt -> where it is on the scaled head"""
    return tuple(float(x) for x in (HEAD_PIV + (np.asarray(pt, dtype=np.float64) - HEAD_PIV) * HEAD_S))


def head_looks(C, D):
    S = HEAD_S
    bc = hs(BREACH_C)
    glow = [(bc, 0.036 * S, 0.080 * S)]                        # the fissures are alight only round the break
    band = [(bc, BREACH_R * 0.92 * S, BREACH_R * 1.42 * S)]
    scorch = [(bc, 0.058 * S, 0.105 * S), (hs((0.0, -0.105, D['md']['zl'])), 0.022 * S, 0.052 * S), (hs((0.0315, -0.09, 1.667)), 0.012 * S, 0.03 * S),
              (hs((-0.0315, -0.09, 1.667)), 0.012 * S, 0.03 * S), (hs((0.0, 0.075, 1.69)), 0.025 * S, 0.105 * S),   # the back of the skull smoked black
              (hs((0.0, -0.01, 1.80)), 0.02 * S, 0.07 * S)]
    ember = []; eyes = []
    for (sx, r, c, hit) in D['eye_pts']:
        g = 1.0 if sx > 0 else 0.9
        eyes.append((hs(c + np.array([0, -r * 0.8, 0])), 0.24 * r * S, 1.3 * r * S, g))
        ember.append((hs(c), r * 0.9 * S, r * 2.9 * S, 0.42 if sx > 0 else 0.34))
    C.look('ash', 'ash', 'skin_face', glow=glow, band=band, scorch=scorch, ember=ember, eslot='emis_face', strength=3.2, bump=0.6, dust=0.25)
    # the hollow: an ember bed in its floor, the whole wall smouldering.  rim = the broken faces of the shell and the walls of the cracks
    C.look('core', 'core', 'skin_face', ao_hi=2.0, ao_lo=1.5, glow=[(hs(CAV_FLOOR), 0.018 * S, 0.085 * S)], floor=0.06, eslot='emis_face', strength=3.6, heat=1.25,
           gamma=1.5, scale=52.0)
    C.look('rim', 'core', 'skin_face', ao_hi=0.30, ao_lo=0.05, glow=[(bc, 0.05 * S, 0.14 * S)], floor=0.3, eslot='emis_face', strength=3.6, heat=0.55,
           gamma=1.3, scale=90.0, section=(0.03, 0.027, 0.025))
    C.look('coal', 'core', 'emis_face', ao_hi=2.0, ao_lo=1.5, scale=48.0, strength=3.6, heat=1.0, gamma=1.0)
    C.look('glowm', 'core', 'emis_face', ao_hi=2.0, ao_lo=1.5, scale=70.0, strength=3.6, heat=0.8, gamma=1.0)
    C.look('maw', 'rock', 'skin_face', color=(0.022, 0.019, 0.018), ash=(0.05, 0.045, 0.04), ash_amt=0.2, scale=40.0, rough=0.9)
    C.look('tooth', 'bone', 'bone_crown', color=(0.40, 0.335, 0.215), stain=(0.10, 0.065, 0.035), crack_amt=0.9, blotch=40.0, pore=0.3,
           grime=(0.02, 0.014, 0.01), grime_amt=1.0)
    C.look('wax', 'wax', 'bone_crown', color=(0.34, 0.30, 0.22), soot=1.0)
    C.look('wick', 'flat', 'bone_crown', color=(0.012, 0.012, 0.012), rough=1.0)
    C.look('nail', 'iron', 'metal_crown', amount=0.5, rough=0.6, dust=0.3, hammer=55.0)
    C.look('halo', 'iron', 'metal_crown', color=(0.03, 0.03, 0.032), amount=0.6, rough=0.6, dust=0.2, hammer=60.0, edge=0.8)
    C.look('eye', 'eye', 'emis_face', eyes=eyes, strength=3.6)


def low_skin(D, tris=7800):
    """decimated head skin, split by face class, skinned"""
    skin = D['skin']
    v = skin.v
    # per-vertex protection: the face, the rims of the breach / cracks / mouth
    front = M.smoothstep(-0.035, -0.075, v[:, 1]) * M.smoothstep(1.50, 1.53, v[:, 2]) * M.smoothstep(1.745, 1.72, v[:, 2])
    dsurf = np.array([SC.nearest(D['bvh0'], p)[2] for p in v])
    rimv = np.zeros(len(v))
    inner = dsurf > 0.0012
    for f in skin.f:                                                # verts of faces that touch both an outer and an inner vertex = rims
        a = [inner[i] for i in f]
        if any(a) and not all(a):
            for i in f:
                rimv[i] = 1.0
    for _ in range(3):                                              # widen the protected band a little
        e = set()
        for f in skin.f:
            if any(rimv[i] > 0.5 for i in f):
                for i in f:
                    e.add(i)
        idx = np.array(sorted(e), dtype=np.int64)
        rimv[idx] = np.maximum(rimv[idx], 0.8)
    low_z = M.smoothstep(1.50, 1.47, v[:, 2])
    w = np.clip(0.12 + 0.62 * np.maximum(front, rimv * 1.1), 0, 0.82) * (1 - 0.9 * low_z)
    w = np.where(inner & (rimv < 0.5), 0.40, w)                      # the cavity wall needs few triangles
    ratio = tris / float(skin.ntri)
    low = SC.decimate_w(skin, ratio, weight=w, factor=6.0)
    low = low.tri().cleanup(1e-7)
    # drop what the yoke and the collar hide (the throat is kept lower: the yoke dips in front)
    fc = low.face_centers()
    keep = (fc[:, 2] > NECK_CUT) | ((fc[:, 1] < -0.02) & (fc[:, 2] > 1.466) & (np.abs(fc[:, 0]) < 0.07))
    low = low.sub(keep)
    fc = low.face_centers()
    fn = np.cross(low.v[[f[1] for f in low.f]] - low.v[[f[0] for f in low.f]], low.v[[f[2] for f in low.f]] - low.v[[f[0] for f in low.f]])
    fn /= np.maximum(np.linalg.norm(fn, axis=1), 1e-12)[:, None]
    md = D['md']
    bvh_cav = SC.bvh_of(D['cav'])
    cls = []
    for c, nn in zip(fc, fn):
        q, n, d = SC.nearest(D['bvh0'], c)
        if d < 0.0022 and abs(float(np.dot(unit(n), nn))) > 0.45:
            cls.append('ash')
        elif abs(c[0]) < md['half'] + 0.004 and abs(c[2] - md['zl']) < 0.022 and c[1] < float(np.interp(c[0], md['x'], md['ys'])) + 0.02:
            cls.append('maw')
        else:
            cls.append('core' if SC.nearest(bvh_cav, c)[2] < 0.0028 else 'rim')
    cls = np.array(cls)
    # weights: Head above the jaw line, the neck from the base body
    body = KC.ref_body('male')
    M.transfer_weights(low, body)
    keepb = np.zeros(M.RIG.nb, dtype=bool); keepb[M.RIG.ids(['Head', 'neck_01', 'spine_03'])] = True
    low.W[:, ~keepb] = 0
    M.normalize_weights(low)
    z = low.v[:, 2]; y = low.v[:, 1]
    hd = np.maximum(M.smoothstep(1.545, 1.575, z), M.smoothstep(-0.035, -0.06, y) * M.smoothstep(1.505, 1.53, z))
    low.W = low.W * (1 - hd[:, None])
    low.W[:, M.RIG.idx['Head']] += hd
    M.smooth_weights(low, 3, 0.5, mask=1 - hd)
    M.normalize_weights(low)
    parts = {}
    for k in ('ash', 'core', 'rim', 'maw'):
        m = cls == k
        if m.any():
            s = low.sub(m)
            s.smooth = 55.0 if k == 'ash' else 35.0
            parts[k] = s
    M.log("low head skin: %d tris (ash %d, core %d, rim %d, maw %d); front-face share of the triangles %.0f%%" % (
        low.ntri, int((cls == 'ash').sum()), int((cls == 'core').sum()), int((cls == 'rim').sum()), int((cls == 'maw').sum()),
        100.0 * float(((fc[:, 1] < -0.06) & (fc[:, 2] > 1.53)).mean())))
    return parts


def coals(D, seed=8):
    """clinker glowing in the bottom of the hollow, seen through the break: lumps half sunk in the cavity wall"""
    rng = np.random.default_rng(seed)
    bvh = SC.bvh_of(D['cav'])
    c0 = np.array([0.03, -0.02, 1.70])
    out = []
    for k in range(11):
        d = unit(np.array([rng.uniform(-0.3, 0.75), rng.uniform(-0.7, 0.5), rng.uniform(-1.0, -0.55)]))
        hit, _ = SC.ray(bvh, c0, d)
        if hit is None:
            continue
        r = rng.uniform(0.009, 0.0165)
        lump = M.rock(r, r * rng.uniform(0.8, 1.1), r * rng.uniform(0.7, 1.0), seed=seed * 5 + k, n=12, jit=0.25, flat=False)
        lump.move(hit - d * r * 0.55)
        out.append(lump)
    p = join(out)
    p.smooth = 35.0
    return p


def add_head(C, D=None, tris=7800):
    D = DN.build_dense() if D is None else D
    head_looks(C, D)

    def sz(p):
        return p.scale(HEAD_S, pivot=HEAD_PIV)
    parts = low_skin(D, tris)
    for k, p in parts.items():
        C.add(sz(p), look=k, obj='body_face')
    hp = sz(D['skin'].copy())
    hp.smooth = 50.0
    hp.cage = 0.003 * HEAD_S; hp.ray = 0.009 * HEAD_S
    C.hi['body_face'] = hp
    C.relaxed["body_face"] = ["ash"]
    C.weld["body_face"] = 2e-6
    C.obj_texel["body_face"] = 2.6
    # the back of the head and the nape get a third of the texel area of the face
    C.uv_split["body_face"] = (lambda fc: fc[:, 1] > 0.045, 0.55)

    def crown(p, look, smooth=None, texel=1.0):
        p.texel = texel
        return C.add(sz(p), bone='Head', look=look, obj='body_crown', smooth=smooth)
    crown(D['teeth'], 'tooth', 50, 1.5)
    crown(D['glow'].solidify(0.0012).recalc(), 'glowm', 180, 0.7)
    crown(D['staples'], 'nail', 28, 1.3)
    crown(D["nails"], "nail", 25, 0.6)
    crown(D["wax"], "wax", 75, 0.75)
    crown(D['wick'], 'wick', 40, 0.5)
    crown(D['wick_ember'], 'coal', 180, 0.4)
    crown(D["halo"], "halo", 24, 0.42)
    crown(D['eyes'], 'eye', 180, 1.6)
    crown(coals(D), 'coal', 35, 0.7)
    return D
