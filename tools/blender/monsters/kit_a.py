"""Fork-A helpers: surface crack ribbons (lava fissures / split skin), scattering props on a skinned body,
fungi, roots, tails.  Everything keeps the body's skin weights so it follows the deformation."""
import math
import numpy as np
import monlib as M
import looks as L
from monlib import V, tube, ellipsoid, join, horn, lerp, loft, unit, revolve


def adjacency(part):
    nb = [set() for _ in range(len(part.v))]
    for f in part.f:
        k = len(f)
        for i in range(k):
            a, b = f[i], f[(i + 1) % k]
            nb[a].add(b); nb[b].add(a)
    return [sorted(s) for s in nb]


def crack_paths(part, mask, count=20, steps=(6, 14), seed=0, dir_hint=None, hint_w=0.5, wander=0.7, branch=0.3):
    """random walks along mesh edges inside `mask` (bool per vertex) -> list of vertex index paths"""
    rng = np.random.default_rng(seed)
    nb = adjacency(part); vn = part.normals(); v = part.v
    cand = np.where(mask)[0]
    paths = []; taken = set()
    hint = None if dir_hint is None else unit(dir_hint)

    def walk(start, d, n):
        path = [start]; cur = start
        for _ in range(n):
            best = None; bs = -1e9
            for q in nb[cur]:
                if q in path or q in taken or not mask[q]:
                    continue
                e = unit(v[q] - v[cur])
                sc = float(e @ d) + rng.uniform(-wander, wander)
                if hint is not None:
                    sc += hint_w * float(e @ hint)
                if sc > bs:
                    bs = sc; best = q
            if best is None:
                break
            d = unit(0.55 * d + 0.45 * unit(v[best] - v[cur]))
            path.append(best); cur = best
        return path, d

    tries = 0
    while len(paths) < count and tries < count * 8 and len(cand):
        tries += 1
        s = int(rng.choice(cand))
        if s in taken:
            continue
        r = rng.normal(size=3)
        d = unit(r - vn[s] * (r @ vn[s]))
        if hint is not None:
            d = unit(d + hint_w * hint)
        n = int(rng.integers(steps[0], steps[1] + 1))
        p, d2 = walk(s, d, n)
        if len(p) >= 3:
            paths.append(p); taken.update(p)
            if rng.uniform() < branch and len(p) > 4:
                k = int(rng.integers(1, len(p) - 2))
                side = unit(np.cross(vn[p[k]], unit(v[p[k + 1]] - v[p[k - 1]]))) * (1 if rng.uniform() < 0.5 else -1)
                bp, _ = walk(p[k], unit(side + 0.5 * d2), max(3, n // 2))
                if len(bp) >= 3:
                    paths.append(bp); taken.update(bp)
    return paths


def ribbons(part, paths, width=0.016, height=0.004, sink=0.003, seed=0, taper=0.3, wmin=0.35, smooth=25.0):
    """thin raised ridges (closed 3-sided prisms) along vertex paths of a skinned Part; weights copied from the
    path vertices.  Use an emis_ look for lava fissures, a dark red look for torn skin."""
    rng = np.random.default_rng(seed)
    vn = part.normals(); v = part.v
    out = []
    for path in paths:
        idx = np.array(path)
        P = v[idx]; Nn = vn[idx]; m = len(idx)
        T = np.zeros_like(P)
        T[1:-1] = P[2:] - P[:-2]; T[0] = P[1] - P[0]; T[-1] = P[-1] - P[-2]
        S = np.cross(Nn, T)
        S /= np.maximum(np.linalg.norm(S, axis=1), 1e-9)[:, None]
        t = np.linspace(0, 1, m)
        prof = np.minimum(1.0, np.minimum(t, 1 - t) / max(taper, 1e-6))
        w = width * (wmin + (1 - wmin) * prof) * rng.uniform(0.7, 1.25, m)
        w[0] *= 0.2; w[-1] *= 0.2
        rings = np.stack([P - S * (w[:, None] / 2) - Nn * sink, P + Nn * height, P + S * (w[:, None] / 2) - Nn * sink], axis=1)
        r = loft(rings, cap0=True, cap1=True).recalc()
        d = np.linalg.norm(r.v[:, None, :] - P[None, :, :], axis=2)
        r.W = part.W[idx[d.argmin(1)]].copy()
        out.append(r)
    if not out:
        return None
    j = join(out)
    j.smooth = smooth
    return j


def add_cracks(C, body, mask, look, count=24, steps=(6, 14), seed=0, width=0.018, height=0.004, sink=0.003, **kw):
    paths = crack_paths(body, mask, count=count, steps=steps, seed=seed, **kw)
    r = ribbons(body, paths, width=width, height=height, sink=sink, seed=seed)
    if r is not None:
        C.add(r, look=look)
    return paths


def scatter(body, mask, n, seed=0, min_dist=0.05):
    """pick up to n vertex indices inside mask, at least min_dist apart"""
    rng = np.random.default_rng(seed)
    cand = np.where(mask)[0]
    rng.shuffle(cand)
    chosen = []
    v = body.v
    for c in cand:
        if all(np.linalg.norm(v[c] - v[k]) >= min_dist for k in chosen):
            chosen.append(int(c))
        if len(chosen) >= n:
            break
    return chosen


def stick(part, body, vi):
    """give a prop the skin weights of body vertex vi (it then rides on the skin like a rigid lump)"""
    part.W = np.tile(body.W[vi], (len(part.v), 1)).astype(np.float32)
    part.bone = None
    return part


def to_normal(part, n, origin, zhint=None):
    """rotate local +Z onto n and move to origin"""
    part.v = part.v @ M.rot_to((0, 0, 1), n).T + np.asarray(origin, dtype=np.float64)
    return part


def blobs(C, body, idxs, look, r=(0.015, 0.04), seed=0, sink=0.3, squash=0.85, n=8, m=5, smooth=80.0):
    """half-embedded lumps (pustules, boils, barnacles, spores) riding on the skin"""
    rng = np.random.default_rng(seed)
    vn = body.normals()
    for vi in idxs:
        rad = rng.uniform(r[0], r[1])
        e = ellipsoid(rad, rad * rng.uniform(0.85, 1.1), rad * squash, n=n, m=m)
        to_normal(e, vn[vi], body.v[vi] + vn[vi] * rad * squash * (1 - 2 * sink))
        stick(e, body, vi)
        C.add(e, look=look, smooth=smooth)


def mushroom(r=0.06, h=0.04, stalk=0.05, sr=0.014, n=10, tilt=0.0):
    """cap + stalk, base at origin, +Z up. returns (cap, stalk)"""
    prof = [(0.0, h), (r * 0.45, h * 0.9), (r * 0.85, h * 0.5), (r, h * 0.08), (r * 0.9, -h * 0.1), (r * 0.35, -h * 0.02), (0.0, h * 0.05)]
    cap = revolve(prof, n).move(0, 0, stalk)
    st = tube([(0, 0, -0.01), (0, 0, stalk * 0.5), (0, 0, stalk + h * 0.1)], [sr * 1.3, sr, sr * 0.9], n=6)
    if tilt:
        cap.rot('x', tilt); st.rot('x', tilt)
    return cap, st


def shelf(r=0.09, th=0.03, n=12):
    """bracket fungus: half disc growing out along local +Y from a surface at origin (flat side at y=0), +Z up"""
    e = ellipsoid(r, r * 0.9, th, n=n, m=6)
    e.v[:, 2] = np.where(e.v[:, 2] < 0, e.v[:, 2] * 0.45, e.v[:, 2])     # flatter underside
    e.move(0, r * 0.45, 0)
    return e


def root(path, r0, r1=0.004, n=6, wob=0.012, seed=0, pts=10):
    """gnarled tapering root along a polyline"""
    rng = np.random.default_rng(seed)
    P = M.polyline(path, pts)
    P[1:-1] += rng.uniform(-wob, wob, (pts - 2, 3))
    t = np.linspace(0, 1, pts)
    rr = lerp(r0, r1, 1) * t ** 0.8 + r0 * (1 - t ** 0.8)
    rr = rr * (1 + 0.18 * np.sin(t * 17 + seed))
    p = tube(P, rr, n=n)
    p.smooth = 50.0
    return p


def spiral(c0, c1, r0, r1, turns=2.0, thick=0.012, n=5, pts=18, phase=0.0, thick1=None):
    """helix wrapped around the axis c0->c1 (limb wrap: roots, chains, bandage rolls)"""
    c0 = V(c0); c1 = V(c1)
    ax = unit(c1 - c0)
    fr = M.frame_from(ax)
    X = fr[:, 0]; Z = fr[:, 2]
    t = np.linspace(0, 1, pts)
    a = phase + t * turns * 2 * math.pi
    R = r0 + (r1 - r0) * t
    P = c0[None] + np.outer(t, c1 - c0) + (np.cos(a) * R)[:, None] * X + (np.sin(a) * R)[:, None] * Z
    th = thick if thick1 is None else thick + (thick1 - thick) * t
    p = tube(P, th, n=n)
    p.smooth = 50.0
    return p


def limb_axis(bone):
    R = M.RIG
    return R.H(bone), R.T(bone)


def hand_scale(pose, s, sides=('l', 'r')):
    """enlarge whole hands (hand + all finger bones scaled about their own heads)"""
    for sd in sides:
        for b in M.RIG.chain('hand_' + sd):
            e = pose.setdefault(b, {})
            e['scale'] = s
    return pose


def ground(part, extra=()):
    """shift a body (and companion parts) so the lowest point sits at z=0"""
    dz = part.v[:, 2].min()
    part.v[:, 2] -= dz
    for p in extra:
        p.v[:, 2] -= dz
    return dz
