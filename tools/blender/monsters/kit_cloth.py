"""Robes, sleeves, hoods, capes: cloth shells built around the T-pose body and skinned by nearest-surface
weight transfer from the re-fitted base body (plus smoothing / pelvis damping for skirts)."""
import math
import numpy as np
import monlib as M
from monlib import V, tube, ellipsoid, box, join, loft, rings_z, sheet, lerp, hang, arc_pts

_BODY = {}


def ref_body(kind='male'):
    if kind not in _BODY:
        _BODY[kind] = M.load_body(kind)
    return _BODY[kind]


def pleat(theta, z, hem_z=0.1, top_z=1.0, amp=0.035, k1=7, k2=11, ph=1.3):
    t = np.clip((top_z - z) / max(top_z - hem_z, 1e-6), 0, 1)
    return 1.0 + t * (amp * np.sin(k1 * theta + ph) + amp * 0.55 * np.sin(k2 * theta + ph * 0.3))


def robe(sections, n=32, hem_z=0.1, pleat_top=1.05, pleat_amp=0.035, cap_top=False, rag=0.0, seed=1, k1=7, k2=11):
    """lofted gown: sections = [(z, cx, cy, rx, ry[, power]), ...] from hem (low z) to the top. Open at the hem."""
    R = rings_z(sections, n, mod=lambda th, z: pleat(th, z, hem_z, pleat_top, pleat_amp, k1, k2))
    if rag > 0:
        R[0, :, 2] += ragged(n, rag, seed)
        R[1, :, 2] = np.maximum(R[1, :, 2], R[0, :, 2] + 0.02)
    p = loft(R, cap0=False, cap1=cap_top)
    p.smooth = 180.0
    return p


def skin_cloth(part, body=None, smooth=4, pelvis_mix=0.0, mix_top=1.0, mix_bot=0.3, bone='pelvis', limit=None):
    """weights for a cloth shell: transfer from the body, optionally damp the leg split below the hips by mixing
    in the pelvis (pelvis_mix at z<=mix_bot .. 0 at z>=mix_top), then smooth."""
    body = ref_body() if body is None else body
    M.transfer_weights(part, body)
    if limit:
        keep = np.zeros(M.RIG.nb, dtype=bool); keep[M.RIG.ids(limit)] = True
        part.W[:, ~keep] = 0
        M.normalize_weights(part)
    if pelvis_mix > 0:
        t = np.clip((mix_top - part.v[:, 2]) / max(mix_top - mix_bot, 1e-6), 0, 1) * pelvis_mix
        part.W = part.W * (1 - t[:, None])
        part.W[:, M.RIG.idx[bone]] += t
    if smooth:
        M.smooth_weights(part, smooth, 0.6)
    return M.normalize_weights(part)


def sleeve(r_sh=0.078, r_el=0.062, r_cuff=0.085, n=14, x0=0.15, x1=0.725, droop=0.03, rings=9, flat=1.12, bell=2.0):
    """left sleeve along the arm (T-pose). Open cuff. droop: cuff hangs lower than the wrist."""
    R = M.RIG
    S = R.H('upperarm_l'); W = R.H('hand_l')
    xs = np.linspace(x0, x1, rings)
    t = (xs - x0) / (x1 - x0)
    r = np.where(t < 0.5, lerp(r_sh, r_el, 1) * (t / 0.5) + r_sh * (1 - t / 0.5), r_el + (r_cuff - r_el) * ((t - 0.5) / 0.5) ** bell)
    path = np.stack([xs, np.full(rings, S[1]) + 0.004, S[2] - droop * t ** 2 - 0.006], axis=1)
    p = tube(path, r * flat, n=n, r2=r, cap=False, up=(0, 0, 1))
    p.smooth = 180.0
    return p


def ragged(n, amp, seed=0, spikes=0.35):
    """irregular 0..amp profile for torn hems (smooth waves + a few deep notches)"""
    rng = np.random.default_rng(seed)
    a = np.linspace(0, 2 * math.pi, n, endpoint=False)
    w = 0.5 + 0.25 * np.sin(3 * a + rng.uniform(0, 6.28)) + 0.25 * np.sin(5 * a + rng.uniform(0, 6.28))
    w = w * rng.uniform(0.4, 1.0, n)
    notch = rng.uniform(0, 1, n) < spikes
    w = np.where(notch, np.minimum(w + rng.uniform(0.3, 0.8, n), 1.3), w * 0.6)
    return np.clip(w, 0, 1.3) * amp


def hood(c=(0, -0.01, 1.682), open_rx=0.088, open_rz=0.118, rx=0.118, rz=0.136, front=-0.150, back=0.105, tail=0.10,
         tail_drop=0.07, n=20, tip=0.012, lift=0.0):
    """cowl lofted from an oval face opening (towards -Y) back around the head into a pointed, drooping tail.
    Rings lie in XZ planes; the front is open."""
    ys = [front, front + 0.02, front + 0.07, c[1] + 0.0, back * 0.6, back, back + tail * 0.5, back + tail]
    rxs = [open_rx * 1.04, open_rx, rx * 0.97, rx, rx * 0.9, rx * 0.62, rx * 0.3, tip]
    rzs = [open_rz * 1.04, open_rz, rz * 0.98, rz, rz * 0.92, rz * 0.62, rz * 0.3, tip]
    czs = [c[2] - 0.004, c[2] - 0.004, c[2], c[2] + 0.004 + lift * 0.5, c[2] + lift, c[2] - tail_drop * 0.15 + lift,
           c[2] - tail_drop * 0.55 + lift * 0.6, c[2] - tail_drop]
    rings = []
    rg = M.ring2d(n, 2.3)
    for y, a, b, cz in zip(ys, rxs, rzs, czs):
        rings.append(np.stack([c[0] + a * rg[:, 0], np.full(n, y), cz + b * rg[:, 1]], axis=1))
    h = loft(np.array(rings), cap0=False, cap1=True).recalc()
    h.smooth = 180.0
    return h


def rest_on(surface, x, y, z_from=3.0):
    """z of the top surface of a Part below (x, y) (for seating props on cloth / armour)"""
    from mathutils.bvhtree import BVHTree
    from mathutils import Vector
    t = surface.tris()
    bvh = BVHTree.FromPolygons([tuple(p) for p in surface.v], [tuple(int(i) for i in q) for q in t])
    hit = bvh.ray_cast(Vector((x, y, z_from)), Vector((0, 0, -1)))
    return float(hit[0][2]) if hit[0] is not None else None


def capelet(z_top=1.565, z_bot=1.39, r_top=(0.085, 0.09), r_bot=(0.285, 0.185), n=28, rows=5, cy=0.015, rag=0.02, seed=3):
    """short shoulder cape / mantle from the neck down over the shoulders"""
    secs = []
    for i in range(rows + 1):
        t = i / rows
        z = lerp(z_bot, z_top, t)
        s = (1 - t) ** 0.6
        secs.append((z, 0, cy, lerp(r_top[0], r_bot[0], s), lerp(r_top[1], r_bot[1], s)))
    R = rings_z(secs, n)
    if rag:
        R[0, :, 2] -= ragged(n, rag, seed)
    p = loft(R, cap0=False, cap1=False)
    p.smooth = 180.0
    return p


def cape(width=0.36, length=1.0, z_top=1.46, y=0.13, rows=10, cols=9, flare=0.18, rag=0.25, seed=5, holes=0.12, tatter=0.15,
         bow=0.05):
    """back cape hanging from the shoulders (weights: spine -> pelvis -> thighs by height)"""
    xs = np.linspace(-width / 2, width / 2, cols)
    top = np.stack([xs, y + bow * (1 - (xs / (width / 2)) ** 2) * 0.0 + 0.0 * xs, np.full(cols, z_top)], axis=1)
    top[:, 1] = y - bow * (np.abs(xs) / (width / 2)) ** 2
    out = np.stack([xs / (width / 2) * 0.5, np.ones(cols), np.zeros(cols)], axis=1)
    out /= np.linalg.norm(out, axis=1)[:, None]
    p = hang(top, length, rows=rows, out=out, flare=flare, rag=rag, seed=seed, wave=0.02, holes=holes, tatter=tatter)
    return p


def cape_weights(p, z_top=1.46, z_hip=1.0, z_knee=0.55, thigh=0.5):
    z = p.v[:, 2]
    x = p.v[:, 0]
    W = np.zeros((len(p.v), M.RIG.nb), dtype=np.float32)
    I = M.RIG.idx
    a = M.smoothstep(z_top - 0.22, z_top - 0.02, z)          # spine_03 near the top
    b = M.smoothstep(z_hip, z_top - 0.2, z) * (1 - a)         # spine_02/01 mid
    c = 1 - a - b                                             # pelvis + thighs below
    W[:, I['spine_03']] = a
    W[:, I['spine_02']] = b * 0.6
    W[:, I['spine_01']] = b * 0.4
    low = M.smoothstep(z_hip, z_knee, z) * thigh
    sl = M.smoothstep(-0.12, 0.12, x)
    W[:, I['pelvis']] = c * (1 - low)
    W[:, I['thigh_l']] = c * low * sl
    W[:, I['thigh_r']] = c * low * (1 - sl)
    p.W = W
    return M.normalize_weights(p)


def body_piece(bones, look=None, kind='male', thresh=0.5, body=None):
    """the part of the base body weighted to the given bones (e.g. hands sticking out of sleeves)"""
    b = ref_body(kind) if body is None else body
    m = b.wsum(bones) > thresh
    p = b.sub(b.vmask_faces(m, 'any'))
    p.smooth = 180.0
    return p


def hand_bones(side):
    return M.RIG.chain('hand_' + side)


def candle(r=0.013, h=0.04, n=8, melt=0.5, seed=0):
    """a stub of candle with a melted rim and drips; base at origin, +Z up"""
    rng = np.random.default_rng(seed)
    prof = [(r * 1.5, 0.0), (r * 1.25, h * 0.12), (r, h * 0.3), (r, h * 0.92), (r * 1.12, h), (r * 0.6, h * 0.94), (0.0, h * 0.9)]
    c = M.revolve(prof, n)
    parts = [c]
    for k in range(3):
        a = rng.uniform(0, 6.28)
        L = h * rng.uniform(0.4, 1.0) * melt * 2
        d = np.array([math.cos(a), math.sin(a), 0]) * r * 1.02
        parts.append(tube([d + (0, 0, h * 0.98), d * 1.1 + (0, 0, h - L * 0.5), d * 1.12 + (0, 0, h - L)], [r * 0.3, r * 0.28, r * 0.36], n=5))
    wick = tube([(0, 0, h * 0.88), (r * 0.15, 0, h * 1.08)], r * 0.11, n=4)
    return join(parts), wick


def rope_belt(z=1.08, rx=0.17, ry=0.15, cy=0.02, r=0.012, tail=0.3, side=-1, n=22):
    """rope around the waist with a knot and two hanging tails; returns (loop, tails) parts (unweighted)"""
    a = np.linspace(0, 2 * math.pi, n, endpoint=False)
    path = np.stack([rx * np.cos(a), cy + ry * np.sin(a), np.full(n, z) + 0.008 * np.sin(a * 3)], axis=1)
    loop = tube(path, r, n=6, closed=True)
    kx = side * rx * 0.55; ky = cy - ry * 0.86
    knot = ellipsoid(r * 2.1, r * 1.8, r * 2.0, n=8, m=5).move(kx, ky, z)
    t1 = tube(M.polyline([(kx, ky, z), (kx + side * 0.015, ky - 0.02, z - tail * 0.5), (kx + side * 0.01, ky - 0.012, z - tail)], 6), r * 0.85, n=5)
    t2 = tube(M.polyline([(kx, ky, z), (kx - side * 0.02, ky - 0.018, z - tail * 0.4), (kx - side * 0.03, ky - 0.01, z - tail * 0.72)], 5), r * 0.85, n=5)
    fr = ellipsoid(r * 1.5, r * 1.5, r * 2.4, n=6, m=4).move(kx + side * 0.01, ky - 0.012, z - tail)
    return join([loop, knot]), join([t1, t2, fr])
