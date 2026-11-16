"""Fork-B helpers (robed / spectral creatures): surface queries, gowns with derived strips, the Order's mark,
tall cowl, rigid chains, stitches, lantern, bell, fungus cap, hair / wisp strands."""
import math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import monlib as M
import looks as L
import kit_cloth as KC
from monlib import V, tube, ellipsoid, box, join, loft, rings_z, sheet, lerp, hang, arc_pts, revolve, torus


# ---------------------------------------------------------------------------------------------- surface queries
class Surf:
    """BVH over a Part: nearest point / normal / dominant bone, ray casts (for seating props on cloth or bodies)"""

    def __init__(self, part):
        self.p = part
        self.t = part.tris()
        self.bvh = BVHTree.FromPolygons([tuple(q) for q in part.v], [tuple(int(i) for i in q) for q in self.t])

    def nearest(self, p):
        loc, nrm, idx, dist = self.bvh.find_nearest(Vector(tuple(p)))
        return np.array(loc), np.array(nrm), idx

    def ray(self, origin, direction):
        hit = self.bvh.ray_cast(Vector(tuple(origin)), Vector(tuple(direction)))
        if hit[0] is None:
            return None, None
        return np.array(hit[0]), np.array(hit[1])

    def front(self, x, z, y_from=-3.0):
        """first surface point met coming from the front (-Y side)"""
        return self.ray((x, y_from, z), (0, 1, 0))

    def back(self, x, z, y_from=3.0):
        return self.ray((x, y_from, z), (0, -1, 0))

    def top(self, x, y, z_from=3.0):
        return self.ray((x, y, z_from), (0, 0, -1))

    def bone_at(self, p, allowed=None):
        loc, nrm, idx = self.nearest(p)
        a, b, c = self.t[idx]
        w = self.p.W[a] + self.p.W[b] + self.p.W[c]
        if allowed is not None:
            ids = M.RIG.ids(allowed)
            return allowed[int(np.argmax(w[ids]))]
        return M.RIG.names[int(np.argmax(w))]


# ---------------------------------------------------------------------------------------------- gowns
def gown(sections, n=36, hem_z=0.1, pleat_top=1.05, pleat_amp=0.05, rag=0.0, seed=1, k1=7, k2=11, cap_top=False):
    """like KC.robe but also returns the ring array so stoles / seams / panels can be derived from it"""
    R = rings_z(sections, n, mod=lambda th, z: KC.pleat(th, z, hem_z, pleat_top, pleat_amp, k1, k2))
    if rag > 0:
        R[0, :, 2] += KC.ragged(n, rag, seed)
        R[1, :, 2] = np.maximum(R[1, :, 2], R[0, :, 2] + 0.02)
    p = loft(R, cap0=False, cap1=cap_top)
    p.smooth = 180.0
    return p, R


def col_of(n, deg):
    """ring column index nearest to an angle (deg; -90 = front, 0 = +X (left side), 90 = back)"""
    return int(round((deg % 360.0) / 360.0 * n)) % n


def strip(R, j0, j1, z0=None, z1=None, offset=0.008, subdiv=3, rag=0.0, seed=0, i0=None, i1=None):
    """panel lying on a gown: ring columns j0..j1 (increasing, wraps), limited to heights z0..z1, pushed outwards
    by `offset`. Rows are re-sampled along each column so the ends can sit at exact heights."""
    m, n, _ = R.shape
    cols = []
    j = j0
    while True:
        cols.append(j % n)
        if j % n == j1 % n:
            break
        j += 1
    cen = R.mean(axis=1)
    G = []
    zlo = R[:, :, 2].min() if z0 is None else z0
    zhi = R[:, :, 2].max() if z1 is None else z1
    rows = max(2, int(round((zhi - zlo) / 0.07)) + 1) if subdiv else 2
    rng = np.random.default_rng(seed)
    for cj, j in enumerate(cols):
        col = R[:, j, :]
        zc = col[:, 2]
        lo = zlo + (rng.uniform(0, rag) if rag else 0.0)
        zs = np.linspace(lo, zhi, rows)
        pts = np.stack([np.interp(zs, zc, col[:, 0]), np.interp(zs, zc, col[:, 1]), zs], axis=1)
        cx = np.interp(zs, cen[:, 2], cen[:, 0]); cy = np.interp(zs, cen[:, 2], cen[:, 1])
        d = pts[:, :2] - np.stack([cx, cy], axis=1)
        d /= np.maximum(np.linalg.norm(d, axis=1), 1e-9)[:, None]
        pts[:, 0] += d[:, 0] * offset; pts[:, 1] += d[:, 1] * offset
        G.append(pts)
    G = np.array(G).transpose(1, 0, 2)
    s = sheet(G)
    s.smooth = 180.0
    return s


def skin_like(part, ref, smooth=1):
    """weights copied from an already skinned Part (e.g. a stole lying on its robe)"""
    M.transfer_weights(part, ref)
    if smooth:
        M.smooth_weights(part, smooth, 0.5)
    return M.normalize_weights(part)


# ---------------------------------------------------------------------------------------------- the Order's mark
def order_mark(H=0.16, depth=0.008, w=0.075):
    """The Hushed Order's mark, in the XZ plane facing -Y, centred on the origin: a vertical stroke crossed by a
    short hooked 'snuffed flame' curve. Returns one Part."""
    hw = w * H * 0.5
    o = [(0, -0.5 * H), (hw, -0.36 * H), (hw * 0.8, 0.40 * H), (0, 0.5 * H), (-hw * 0.8, 0.40 * H), (-hw, -0.36 * H)]
    st = M.prism(o, -depth / 2, depth / 2).rot('x', 90)
    pth = np.array([(-0.20, 0.235), (-0.27, 0.15), (-0.20, 0.07), (-0.06, 0.09), (0.07, 0.17), (0.20, 0.25), (0.30, 0.22)]) * H
    p3 = np.stack([pth[:, 0], np.zeros(len(pth)), pth[:, 1]], axis=1)
    p3 = M.polyline(p3, 14)
    r2 = np.r_[np.linspace(0.5, 1.0, 5), np.linspace(1.0, 0.4, 9)] * hw * 1.1 * 1.414
    hook = tube(p3, depth * 0.7071, n=4, r2=r2, up=(0, 1, 0), phase=math.pi / 4)
    mk = join([st, hook])
    mk.smooth = 30.0
    return mk


def place_front(part, surf, x, z, lift=0.002, tilt=True):
    """seat a flat -Y-facing Part on the front of a surface at (x, z); returns (part, hit point)"""
    hit, nrm = surf.front(x, z)
    if hit is None:
        return part, None
    if tilt and nrm is not None:
        nrm = M.unit(nrm)
        if nrm[1] > 0:
            nrm = -nrm
        part.tf(M.rot_to((0, -1, 0), nrm))
    part.move(hit + np.array([0, -lift, 0]))
    return part, hit


# ---------------------------------------------------------------------------------------------- hoods
def tall_cowl(n=20, height=0.46, lean=0.20, tip_drop=0.05, open_rx=0.088, open_rz=0.12, c=(0, -0.008, 1.69), scale=1.0):
    """tall pointed cowl: oval face opening towards -Y, sweeps back over the head, rises into a long cone whose tip
    bends over backwards. Returns a Part (bind rigidly to Head or skin Head/neck)."""
    cx, cy, cz = c
    top = cz + 0.09 + height
    head = np.array([
        (cx, cy - 0.142, cz - 0.012), (cx, cy - 0.122, cz - 0.012), (cx, cy - 0.062, cz - 0.006), (cx, cy + 0.003, cz + 0.010),
        (cx, cy + 0.043, cz + 0.075), (cx, cy + 0.058, cz + 0.16)])
    # the cone: rises, then curls over backwards and down like a crook
    cone = M.polyline([(cx, cy + 0.058, cz + 0.16), (cx, cy + 0.07, cz + 0.09 + height * 0.5), (cx, cy + 0.085 + lean * 0.22, top - 0.03),
                       (cx, cy + 0.085 + lean * 0.62, top), (cx, cy + 0.085 + lean * 0.95, top - tip_drop * 0.45),
                       (cx, cy + 0.085 + lean, top - tip_drop)], 9)
    P = np.vstack([head, cone[1:]])
    tc = np.linspace(0, 1, 9)[1:]
    rz = np.r_[[open_rz * 1.05, open_rz, 0.134, 0.138, 0.126, 0.102], 0.102 * (1 - tc) ** 0.85 + 0.004]
    rx = np.r_[[open_rx * 1.05, open_rx, 0.116, 0.120, 0.113, 0.094], 0.094 * (1 - tc) ** 0.85 + 0.004]
    T, N, B = M._frames(P, up=(0, 0, 1))
    rg = M.ring2d(n, 2.3)
    rings = []
    for i in range(len(P)):
        rings.append(P[i] + rz[i] * scale * rg[:, 0:1] * N[i] + rx[i] * scale * rg[:, 1:2] * B[i])
    h = loft(np.array(rings), cap0=False, cap1=True).recalc()
    h.smooth = 180.0
    return h


# ---------------------------------------------------------------------------------------------- chains / stitches
def links(path, link=0.035, r=0.006, w=0.6, n=6, m=4):
    """list of (link Part, centre) along a polyline (same construction as monlib.chain, kept separate per link)"""
    P = np.asarray(path, dtype=np.float64)
    d = np.r_[0, np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]
    Ln = d[-1]
    k = max(2, int(Ln / (link * 0.72)))
    out = []
    for i in range(k + 1):
        s = Ln * i / k
        j = min(np.searchsorted(d, s, side='right') - 1, len(P) - 2)
        u = (s - d[j]) / max(d[j + 1] - d[j], 1e-9)
        p = P[j] * (1 - u) + P[j + 1] * u
        tdir = M.unit(P[j + 1] - P[j])
        t = torus(link * 0.5, r, n=n, m=m, ry=link * 0.5 * w)
        fr = M.frame_from(tdir, (0, 0, 1) if abs(tdir[2]) < 0.9 else (1, 0, 0))
        Mx = np.stack([fr[:, 1], fr[:, 0], fr[:, 2]], axis=1)
        if i % 2:
            Mx = Mx @ M.rot_axis('x', 90)
        t.v = t.v @ Mx.T + p
        if np.linalg.det(Mx) < 0:
            t.flip()
        out.append((t, p))
    return out


def add_chain(C, path, look, bone=None, surf=None, allowed=None, link=0.035, r=0.006, obj=None, n=6, m=4):
    """chain whose every link is rigid: all on `bone`, or each on the dominant bone of the nearest point of `surf`"""
    for t, p in links(path, link, r, n=n, m=m):
        b = bone if bone is not None else surf.bone_at(p, allowed)
        C.add(t, bone=b, look=look, obj=obj, smooth=60)


def drape_path(a, b, sag=0.08, n=8):
    """catenary-ish hanging curve between two points"""
    a = np.asarray(a, dtype=float); b = np.asarray(b, dtype=float)
    t = np.linspace(0, 1, n)
    P = a[None, :] * (1 - t[:, None]) + b[None, :] * t[:, None]
    P[:, 2] -= sag * 4 * t * (1 - t)
    return P


def stitches(C, surf, pts, look, bone=None, length=0.03, r=0.0035, lift=0.003, allowed=None, cross=False, every=1):
    """stitch bars across a seam given as surface-near points; each bar is rigid on the local dominant bone"""
    P = np.asarray(pts, dtype=float)
    for i in range(0, len(P), every):
        loc, nrm, _ = surf.nearest(P[i])
        nrm = M.unit(nrm)
        if np.dot(nrm, P[i] - loc) < 0:
            nrm = -nrm
        tg = M.unit(P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)])
        side = M.unit(np.cross(nrm, tg))
        c = loc + nrm * lift
        dirs = [side] if not cross else [M.unit(side + tg), M.unit(side - tg)]
        for dv in dirs:
            bar = tube([c - dv * length / 2 - nrm * lift, c - dv * length * 0.25 + nrm * lift * 0.6, c + dv * length * 0.25 + nrm * lift * 0.6,
                        c + dv * length / 2 - nrm * lift], r, n=4)
            b = bone if bone is not None else surf.bone_at(c, allowed)
            C.add(bar, bone=b, look=look, smooth=60)


def seam_pts(surf, a, b, n, from_dir=(0, -1, 0), y_from=None):
    """n points along the straight line a->b projected onto the surface by rays along from_dir"""
    a = np.asarray(a, dtype=float); b = np.asarray(b, dtype=float)
    out = []
    d = M.unit(from_dir)
    for t in np.linspace(0, 1, n):
        p = a * (1 - t) + b * t
        hit, _ = surf.ray(p - d * 3.0, d)
        out.append(hit if hit is not None else p)
    return np.array(out)


# ---------------------------------------------------------------------------------------------- props
def lantern(h=0.22, r=0.062, bars=6, n=8):
    """hanging lantern: hook ring at the origin, body hanging along -Z. Returns (metal Part, glow Part)."""
    parts = [torus(0.02, 0.005, n=8, m=4).rot('x', 90).move(0, 0, -0.02)]
    z0 = -0.045
    parts.append(revolve([(0.008, z0 + 0.01), (r * 0.45, z0 - 0.005), (r * 1.08, z0 - 0.05), (r * 1.12, z0 - 0.06), (r * 0.95, z0 - 0.062)], n))
    zb = z0 - 0.06 - h
    parts.append(revolve([(r * 0.95, zb + 0.004), (r * 1.1, zb), (r * 1.08, zb - 0.014), (r * 0.5, zb - 0.03), (0.0, zb - 0.034)], n))
    for k in range(bars):
        a = 2 * math.pi * k / bars
        x, y = r * math.cos(a), r * math.sin(a)
        parts.append(tube([(x, y, z0 - 0.058), (x * 1.08, y * 1.08, (z0 - 0.06 + zb) / 2), (x, y, zb + 0.004)], 0.0055, n=4))
    for zz in (z0 - 0.06 - h * 0.33, z0 - 0.06 - h * 0.66):
        parts.append(torus(r * 1.06, 0.004, n=n, m=4).move(0, 0, zz))
    cage = join(parts)
    cage.smooth = 50
    glow = ellipsoid(r * 0.62, r * 0.62, h * 0.36, n=8, m=6).move(0, 0, z0 - 0.06 - h * 0.52)
    flame = revolve([(0, 0), (r * 0.3, h * 0.08), (r * 0.2, h * 0.25), (0, h * 0.42)], 6).move(0, 0, z0 - 0.06 - h * 0.5)
    return cage, join([glow, flame])


def bell(h=0.12, R=0.055, n=14, thick=0.12, clapper=True):
    """bell with its crown at the origin and its mouth at z = -h (open, with inner wall). Returns (bell, clapper)."""
    prof = [(0.0, 0.0), (0.20 * R, 0.0), (0.34 * R, -0.04 * h), (0.44 * R, -0.14 * h), (0.52 * R, -0.36 * h), (0.62 * R, -0.64 * h),
            (0.80 * R, -0.88 * h), (1.0 * R, -1.0 * h), (1.0 * R - thick * R * 0.5, -1.0 * h - 0.004 * 0),
            ((0.80 - thick) * R, -0.86 * h), ((0.62 - thick) * R, -0.62 * h), ((0.52 - thick) * R, -0.34 * h), (0.3 * R, -0.16 * h), (0.0, -0.13 * h)]
    b = revolve(prof, n, cap=False)
    b.smooth = 50
    cl = None
    if clapper:
        cl = join([tube([(0, 0, -0.13 * h), (0, 0, -0.8 * h)], R * 0.06, n=5), ellipsoid(R * 0.2, R * 0.2, R * 0.24, n=8, m=5).move(0, 0, -0.86 * h)])
    return b, cl


def fungus_cap(R=0.09, h=0.05, n=12, lobes=0.12, seed=0):
    """mushroom cap centred on the origin (rim at z=0, dome above): returns (cap top Part, glowing gill underside Part)"""
    rng = np.random.default_rng(seed)
    ph = rng.uniform(0, 6.28)
    mod = lambda th, z: 1.0 + lobes * np.sin(3 * th + ph) + lobes * 0.5 * np.sin(5 * th + ph * 2)
    secs = [(-0.012, 0, 0, R * 0.92, R * 0.92), (0.0, 0, 0, R, R), (h * 0.35, 0, 0, R * 0.9, R * 0.9), (h * 0.7, 0, 0, R * 0.62, R * 0.62),
            (h * 0.93, 0, 0, R * 0.3, R * 0.3), (h, 0, 0, 0.001, 0.001)]
    cap = loft(rings_z(secs, n, mod=mod), cap0=False, cap1=True).recalc()
    cap.smooth = 60
    gs = [(-0.012, 0, 0, R * 0.915, R * 0.915), (-0.022, 0, 0, R * 0.6, R * 0.6), (-0.012, 0, 0, R * 0.2, R * 0.2), (-0.03, 0, 0, 0.001, 0.001)]
    gill = loft(rings_z(gs, n, mod=mod), cap0=False, cap1=True).recalc()
    gill.smooth = 60
    return cap, gill


def strands(anchors, outs, lengths, width=0.02, rows=6, seed=0, wave=0.012, taper=0.7, cols=2, flare=0.0, curve=1.3, rag=0.0):
    """separate hanging ribbons (hair, reeds, wisps): one per anchor. anchors (k,3), outs (k,3) outward dirs.
    Returns list of Parts (unweighted)."""
    rng = np.random.default_rng(seed)
    A = np.asarray(anchors, dtype=float); O = np.asarray(outs, dtype=float)
    Ls = np.broadcast_to(np.asarray(lengths, dtype=float), (len(A),))
    res = []
    for k in range(len(A)):
        o = M.unit(O[k])
        side = M.unit(np.cross((0, 0, 1), o)) if abs(o[2]) < 0.95 else np.array([1.0, 0, 0])
        ws = np.linspace(-width / 2, width / 2, cols)
        top = A[k][None, :] + side[None, :] * ws[:, None]
        s = hang(top, Ls[k], rows=rows, out=np.tile(o, (cols, 1)), flare=flare, rag=rag, seed=int(rng.integers(1 << 30)), wave=wave,
                 taper=taper, curve=curve)
        res.append(s)
    return res


def wisps(c, rx, ry, count=9, lmin=0.3, lmax=0.65, width=0.11, rows=7, seed=0, swirl=25.0, flare=0.05, wave=0.03, cols=3,
          a0=0.0, a1=360.0, taper=0.92, back_bias=0.0):
    """ring of tapering, curling shreds hanging from a horizontal ellipse (the trailing end of a shroud).
    back_bias > 0 makes the shreds at the back (+Y) longer. Returns list of Parts (unweighted)."""
    rng = np.random.default_rng(seed)
    res = []
    full = abs((a1 - a0) - 360.0) < 1e-6
    angs = np.linspace(a0, a1, count, endpoint=not full) + rng.uniform(-6, 6, count)
    for k, a in enumerate(angs):
        ar = math.radians(a)
        half = math.degrees(width / 2 / max(rx, ry))
        top, nr = arc_pts(c, rx, ry, a - half, a + half, cols)
        Lk = rng.uniform(lmin, lmax) * (1.0 + back_bias * max(0.0, math.sin(ar)))
        s = hang(top, Lk, rows=rows, out=nr, flare=flare * rng.uniform(0.3, 1.4), rag=0.0, seed=int(rng.integers(1 << 30)), wave=wave,
                 taper=taper, curve=1.6)
        if swirl:
            sw = swirl * rng.uniform(0.4, 1.0) * (1 if rng.uniform() < 0.7 else -1)
            t = np.clip((c[2] - s.v[:, 2]) / max(Lk, 1e-6), 0, 1)
            ang = np.radians(sw * t ** 1.5)
            x = s.v[:, 0] - c[0]; y = s.v[:, 1] - c[1]
            s.v[:, 0] = c[0] + x * np.cos(ang) - y * np.sin(ang)
            s.v[:, 1] = c[1] + x * np.sin(ang) + y * np.cos(ang)
        res.append(s)
    return res


class Hunch:
    """bake a posture (rotations / moves of pose_sculpt) into parts that were built around the rest pose"""

    def __init__(self, pose):
        self.pose = {k: {kk: vv for kk, vv in v.items() if kk in ('rot', 'move')} for k, v in pose.items()}

    def skinned(self, part):
        return M.pose_sculpt(part, self.pose)

    def rigid(self, part, bone):
        return M.pose_part(part, self.pose, bone)

    def point(self, bone, p):
        return M.posed_point(self.pose, bone, p)[0]


def weights_z(part, stops):
    return M.weights_by(part, part.v[:, 2], stops)


def staff_tilt(part, deg):
    """rotate a weapon built along local +Y so that, in the UAL rest fist, its top leans towards the wrist side
    (i.e. upwards when the arm hangs): call before to_grip()."""
    return part.rot('z', deg)


# ---------------------------------------------------------------------------------------------- extra look kinds
def k_burlap(N, p):
    """coarse sackcloth: visible weave, stains"""
    col = L._c(p, 'color', (0.27, 0.22, 0.14))
    w1 = N.noise(95, 2, 0.5, vec=N.mapping(N.coord(), scale=(1, 1, 0.08)))
    w2 = N.noise(95, 2, 0.5, vec=N.mapping(N.coord(), scale=(0.08, 0.08, 1)))
    weave = N.math('ADD', N.math('MULTIPLY', w1, 0.5), N.math('MULTIPLY', w2, 0.5))
    big = N.noise(5, 4, 0.65)
    c = N.mix(N.mapr(big, 0.3, 0.72, 0.0, 0.6), col, N.mul(col, 0.4))
    c = N.mix(N.mapr(weave, 0.3, 0.7, 0.0, 0.5), c, N.mul(c, 0.5))
    stain = N.mapr(N.noise(3.1, 3, 0.6), 0.52, 0.72)
    c = N.mix(N.math('MULTIPLY', stain, L._c(p, 'stain_amt', 0.6)), c, L._c(p, 'stain', (0.07, 0.05, 0.035)))
    return dict(color=c, rough=0.95, metal=0.0, height=weave, bump=(L._c(p, 'bump', 0.9), 0.005))


L.KINDS['burlap'] = k_burlap
