"""Batch helpers for the skeleton thrower / wraith / choir: cloth draped over the T-pose arms (poncho), shells with a
per-column hem (veils, mantles), torn hoods pushed back off the face, javelins, legless shroud weights, trailing
shreds, stretched claw hands, halo rings.  Everything returns Parts in rest (T-pose) armature space."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_b as KB2
from monlib import V, tube, ellipsoid, box, join, loft, sheet, lerp, hang, arc_pts, revolve, torus, blade


def wrap_deg(d):
    return (np.asarray(d, dtype=np.float64) + 180.0) % 360.0 - 180.0


def bump_deg(theta, at, width):
    """gaussian bump on a circle (degrees)"""
    return np.exp(-(wrap_deg(np.asarray(theta, dtype=np.float64) - at) / width) ** 2)


def leg_bones():
    return M.RIG.chain('thigh_l') + M.RIG.chain('thigh_r')


def upper_bones():
    lg = set(leg_bones())
    return [n for n in M.RIG.names if n not in lg]


def rag_profile(n, amp, seed=0, spikes=0.3):
    """irregular 0..1.3*amp profile around a ring (smooth waves + deep notches)"""
    return KC.ragged(n, amp, seed, spikes)


def punch(part, G_shape, chance, seed=0, t0=0.55):
    """drop random quads of a sheet built from a (rows+1, n) grid (closed in u) below fraction t0 of its length"""
    rows, n = G_shape[0] - 1, G_shape[1]
    rng = np.random.default_rng(seed)
    keep = []
    for i in range(rows):
        t = (i + 1) / rows
        for j in range(n):
            keep.append(not (t > t0 and rng.uniform() < chance * t))
    q = part.sub(np.array(keep))
    q.smooth = part.smooth
    return q


# ---------------------------------------------------------------------------------------------- poncho
def poncho(X=0.40, Yf=0.165, Yb=0.18, n=44, rows=8, neck=(0.078, 0.088), neck_c=(0.0, 0.012),
           ridge=((0.0, 1.588), (0.07, 1.566), (0.13, 1.518), (0.20, 1.497), (0.46, 1.487)),
           yc=((0.0, 0.012), (0.10, 0.03), (0.20, 0.066), (0.5, 0.068)),
           flat=((0.0, 0.085), (0.12, 0.10), (0.2, 0.048), (0.3, 0.032), (0.5, 0.03)),
           depth_f=((0.0, 0.31), (0.1, 0.27), (0.2, 0.17), (0.3, 0.125), (0.42, 0.07)),
           depth_b=((0.0, 0.40), (0.1, 0.35), (0.2, 0.20), (0.3, 0.13), (0.42, 0.07)),
           power=4.0, rag=0.22, seed=0, curve=1.25, point=0.0):
    """Cloth draped over the shoulders AND the outstretched T-pose arms (so it hangs correctly once the arms come
    down): a height field around the neck hole.  All tables are (|x|, value).  Returns (Part, grid)."""
    def tab(t, x):
        t = np.asarray(t, dtype=np.float64)
        return np.interp(np.abs(x), t[:, 0], t[:, 1])

    def out_y(x, front):
        y0 = Yf if front else Yb
        u = np.clip(np.abs(x) / X, 0, 1)
        return y0 * (1 - u ** power) ** (1.0 / power)

    def height(x, y):
        dy = y - tab(yc, x)
        fr = dy < 0
        Y = np.where(fr, out_y(x, True), out_y(x, False))
        D = np.where(fr, tab(depth_f, x), tab(depth_b, x))
        w = np.minimum(tab(flat, x), Y * 0.85)
        u = np.abs(dy) / np.maximum(Y, 1e-6)
        u0 = w / np.maximum(Y, 1e-6)
        s = np.clip((u - u0) / np.maximum(1 - u0, 1e-6), 0, 1.3) ** curve
        return tab(ridge, x) - D * s

    a = np.linspace(0, 2 * math.pi, n, endpoint=False)
    dirs = np.stack([X * np.cos(a), max(Yf, Yb) * 1.6 * np.sin(a)], axis=1)
    dirs /= np.linalg.norm(dirs, axis=1)[:, None]
    th = np.degrees(np.arctan2(dirs[:, 1], dirs[:, 0]))
    rng = np.random.default_rng(seed)
    rg = rag_profile(n, rag, seed)
    G = np.zeros((rows + 1, n, 3))
    for j in range(n):
        d = dirs[j]
        # inner radius: neck ellipse; outer radius: where the direction leaves the outline (bisection)
        r0 = 1.0 / math.sqrt((d[0] / neck[0]) ** 2 + (d[1] / neck[1]) ** 2)
        lo, hi = r0, 1.5
        for _ in range(40):
            mid = (lo + hi) / 2
            x = neck_c[0] + d[0] * mid; y = neck_c[1] + d[1] * mid
            dy = y - float(tab(yc, x))
            Y = float(out_y(x, dy < 0))
            inside = (abs(x) < X) and (abs(dy) < Y)
            if inside:
                lo = mid
            else:
                hi = mid
        r1 = lo * (1.0 - rg[j])
        if point:
            r1 *= 1.0 + point * float(bump_deg(th[j], -90, 16))
        for i in range(rows + 1):
            t = (i / rows) ** 0.85
            r = r0 + (r1 - r0) * t
            x = neck_c[0] + d[0] * r; y = neck_c[1] + d[1] * r
            G[i, j] = (x, y, float(height(np.array(x), np.array(y))))
    G[0, :, 2] += 0.012
    s = sheet(G, closed_u=True)
    s.smooth = 180.0
    return s, G


# ---------------------------------------------------------------------------------------------- per-column hems
def drape(profile, hem, z_top, n=32, rows=8, power=2.2, rag=0.0, seed=0, curve=1.0, mod=None):
    """shell hanging from a ring at z_top: profile = [(z, cx, cy, rx, ry), ...] interpolated by height, hem(theta_deg
    array) -> hem height per column (theta: -90 front, +90 back, 0 = +X = the creature's left).  Every column runs
    from z_top down to its own hem.  mod(theta_deg, z) -> radial multiplier.  Returns (Part, grid (rows+1, n, 3))."""
    prof = np.array(sorted([tuple(float(q) for q in p[:5]) for p in profile]))
    zs = prof[:, 0]
    rg = M.ring2d(n, power)
    th = np.degrees(np.arctan2(rg[:, 1], rg[:, 0]))
    hz = np.asarray(hem(th), dtype=np.float64).copy()
    if rag:
        hz = hz + rag_profile(n, rag, seed)
    G = np.zeros((rows + 1, n, 3))
    for i in range(rows + 1):
        t = (i / rows) ** curve
        z = z_top + (hz - z_top) * t
        cx = np.interp(z, zs, prof[:, 1]); cy = np.interp(z, zs, prof[:, 2])
        rx = np.interp(z, zs, prof[:, 3]); ry = np.interp(z, zs, prof[:, 4])
        k = np.ones(n) if mod is None else np.asarray(mod(th, z))
        G[i, :, 0] = cx + rx * k * rg[:, 0]
        G[i, :, 1] = cy + ry * k * rg[:, 1]
        G[i, :, 2] = z
    s = sheet(G, closed_u=True)
    s.smooth = 180.0
    return s, G


# ---------------------------------------------------------------------------------------------- hoods
def rag_hood(c=(0.0, -0.008, 1.70), rx=0.106, rz=0.132, open_rx=0.09, open_rz=0.132, front=-0.135, back=0.105, tail=0.16,
             tail_drop=0.15, shear=0.55, n=20, rag=0.014, seed=0, open_dz=-0.02, tip=0.012, power=2.3):
    """cowl whose upper rim is pushed back off the brow (so the face / eyes read from a high camera), ragged rim,
    drooping pointed tail.  Rings lie in XZ planes; the front is open."""
    rng = np.random.default_rng(seed)
    ys = [front, front + 0.018, front + 0.07, c[1], back * 0.6, back, back + tail * 0.5, back + tail]
    rxs = [open_rx * 1.05, open_rx, rx * 0.98, rx, rx * 0.9, rx * 0.62, rx * 0.3, tip]
    rzs = [open_rz * 1.05, open_rz, rz * 0.99, rz, rz * 0.92, rz * 0.62, rz * 0.3, tip]
    czs = [c[2] + open_dz, c[2] + open_dz, c[2] + open_dz * 0.5, c[2] + 0.004, c[2], c[2] - tail_drop * 0.15,
           c[2] - tail_drop * 0.55, c[2] - tail_drop]
    shs = [shear, shear, shear * 0.55, 0, 0, 0, 0, 0]
    rg = M.ring2d(n, power)
    rings = []
    for k in range(len(ys)):
        x = c[0] + rxs[k] * rg[:, 0]
        z = czs[k] + rzs[k] * rg[:, 1]
        y = np.full(n, ys[k]) + shs[k] * np.maximum(z - czs[k], 0.0)
        if k == 0 and rag:
            y = y + rng.uniform(0, rag, n)
        rings.append(np.stack([x, y, z], axis=1))
    h = loft(np.array(rings), cap0=False, cap1=True).recalc()
    h.smooth = 180.0
    return h


# ---------------------------------------------------------------------------------------------- javelins
def javelin(length=1.32, back=0.52, r=0.0105, head=0.21, n=6, wrap=True, ferrule=True):
    """throwing spear along +Y with the origin in the gripping fist (blade width along X, thickness along Z).
    Returns [(Part, kind)] with kind in 'wood' / 'iron' / 'strap'."""
    front = length - back
    ys = front - head
    out = []
    sh = tube([(0, -back, 0), (0, -back * 0.4, 0.0015), (0, ys * 0.5, -0.0015), (0, ys + 0.03, 0)], [r * 0.85, r, r, r * 0.88], n=n)
    sh.smooth = 60.0
    out.append((sh, 'wood'))
    sock = tube([(0, ys - 0.055, 0), (0, ys - 0.045, 0), (0, ys + 0.035, 0)], [r * 1.05, r * 1.5, r * 1.1], n=6)
    sock.smooth = 40.0
    out.append((sock, 'iron'))
    st = [(ys + 0.02, 0.006, 0.006, 0.012), (ys + 0.042, 0.029, 0.029, 0.011), (ys + 0.075, 0.025, 0.025, 0.010),
          (ys + head * 0.62, 0.017, 0.017, 0.008), (ys + head - 0.03, 0.008, 0.008, 0.005), (ys + head, 0.0, 0.001, 0.001)]
    out.append((blade(st), 'iron'))
    if ferrule:
        f = revolve([(0.0, -0.05), (r * 0.9, -0.012), (r * 1.25, 0.0), (r * 1.25, 0.03), (r * 0.9, 0.036)], 6).rot('x', -90).move(0, -back, 0)
        f.smooth = 40.0
        out.append((f, 'iron'))
    if wrap:
        w = tube([(0, -0.075, 0), (0, -0.06, 0), (0, 0.0, 0), (0, 0.06, 0), (0, 0.075, 0)], [r * 1.05, r * 1.45, r * 1.38, r * 1.45, r * 1.05], n=6)
        w.smooth = 60.0
        out.append((w, 'strap'))
    return out


def place(parts, direction, origin, zhint=(0, 1, 0)):
    """orient a list of [(Part, kind)] built along +Y"""
    for p, _ in parts:
        p.orient(direction, zhint, origin)
    return parts


def seat_path(surf, pts_xy, n=14, lift=0.007):
    """polyline in the XY plane dropped onto the top of a surface (KB2.Surf) -> (m,3) points"""
    P = np.asarray(pts_xy, dtype=np.float64)
    P3 = np.c_[P, np.zeros(len(P))]
    R = M.polyline(P3, n)
    out = []
    for q in R:
        hit, nrm = surf.top(q[0], q[1])
        if hit is not None:
            out.append(hit + np.array([0, 0, lift]))
    return np.array(out)


# ---------------------------------------------------------------------------------------------- spectral bodies
def core_body(kind='male', ratio=0.6, head=False, z_cut=1.06, drop_arms=False):
    """legless base body (weights kept): everything weighted to the legs and everything below z_cut is removed,
    optionally the head (above the chin) and the arms too.  Decimated by `ratio`."""
    b = M.load_body(kind)
    drop = b.vmask_faces(b.wsum(leg_bones()) > 0.4, 'any')
    drop |= b.vmask_faces(b.v[:, 2] < z_cut, 'any')
    if not head:
        drop |= b.vmask_faces(b.wsum(['Head']) > 0.35, 'any')
    if drop_arms:
        ab = []
        for s in ('l', 'r'):
            ab += M.RIG.chain('upperarm_' + s)
        drop |= b.vmask_faces(b.wsum(ab) > 0.5, 'all')
    c = b.sub(~drop)
    if ratio < 1.0:
        c = M.decimate(c, ratio)
    c.smooth = 180.0
    return c


def stretch_fingers(body, k=1.5, k_thumb=1.3, sides=('l', 'r')):
    """lengthen the fingers of a skinned body along their own axes (mesh only; the joints stay where they are)"""
    R = M.RIG
    for s in sides:
        for fg in ('index', 'middle', 'ring', 'pinky', 'thumb'):
            names = [n for n in ('%s_0%d_%s' % (fg, j, s) for j in (1, 2, 3))] + ['%s_04_leaf_%s' % (fg, s)]
            names = [n for n in names if n in R.idx]
            K = R.H(names[0])
            d = M.unit(R.T('%s_03_%s' % (fg, s)) - K)
            w = body.wsum(names)
            a = np.maximum((body.v - K) @ d, 0.0)
            kk = k_thumb if fg == 'thumb' else k
            body.v += d[None, :] * ((kk - 1.0) * a * w)[:, None]
    return body


def shroud_weights(part, body=None, smooth=4, z_hip=1.0, blend=0.12):
    """cloth of a legless creature: transferred from the body above the hips (never the legs), pelvis only below"""
    KC.skin_cloth(part, body, smooth=0, limit=upper_bones())
    t = M.smoothstep(z_hip + blend, z_hip - 0.02, part.v[:, 2]).astype(np.float32)
    part.W = part.W * (1 - t[:, None])
    part.W[:, M.RIG.idx['pelvis']] += t
    if smooth:
        M.smooth_weights(part, smooth, 0.6)
    return M.normalize_weights(part)


def sweep_back(part, z0, k=0.5, power=1.4, zmin=0.06):
    """trail the lower part of something backwards (+Y): y += k * (z0 - z)^power below z0; keeps it above zmin"""
    d = np.maximum(z0 - part.v[:, 2], 0.0)
    part.v[:, 1] += k * d ** power
    part.v[:, 2] = np.maximum(part.v[:, 2], zmin)
    return part


def trail(base, direction, length, width=0.05, rows=6, droop=0.5, wave=0.02, seed=0, taper=0.85, side=None, down=(0, 0, -1)):
    """tapering ribbon streaming from `base` along `direction`, sagging by droop*length at its tip towards `down`
    (for limbs: pass the T-pose direction that points to the ground once the limb hangs, e.g. +X for the left arm)"""
    rng = np.random.default_rng(seed)
    base = np.asarray(base, dtype=np.float64)
    d = M.unit(direction)
    dn = M.unit(down)
    s = M.unit(np.cross(d, -dn)) if side is None else M.unit(side)
    up = M.unit(np.cross(s, d))
    ph = rng.uniform(0, 6.28)
    G = np.zeros((rows + 1, 3, 3))
    for i in range(rows + 1):
        t = i / rows
        c = base + d * length * t + dn * droop * length * t ** 1.8 + s * wave * math.sin(ph + t * 5.5) * t
        w = width * (1 - taper * t ** 1.2) / 2
        G[i, 0] = c - s * w
        G[i, 1] = c + up * (0.006 + wave * 0.5 * math.sin(ph * 1.7 + t * 4.0) * t)
        G[i, 2] = c + s * w
    p = sheet(G)
    p.smooth = 180.0
    return p


def ellipse_path(c, rx, ry, n=24, tilt_x=0.0, tilt_y=0.0, a0=0.0, a1=360.0):
    a = np.radians(np.linspace(a0, a1, n))
    pts = np.stack([rx * np.cos(a), ry * np.sin(a), np.zeros(n)], axis=1)
    return pts @ (M.rot_axis('y', tilt_y) @ M.rot_axis('x', tilt_x)).T + np.asarray(c, dtype=np.float64)


def halo(R=0.24, r=0.011, n=28, m=5, rays=0, ray_len=0.05):
    """ring standing in the XZ plane (facing -Y), centred on the origin; optional short rays on the upper half"""
    parts = [torus(R, r, n=n, m=m).rot('x', 90)]
    for k in range(rays):
        a = math.radians(180.0 * (k + 0.5) / rays)
        d = np.array([math.cos(a), 0.0, math.sin(a)])
        sp = M.cone_spike(r * 1.3, ray_len, n=4)
        sp.tf(M.rot_to((0, 0, 1), d)).move(d * (R + r * 0.5))
        parts.append(sp)
    h = join(parts)
    h.smooth = 60.0
    return h


# ---------------------------------------------------------------------------------------------- static vestments
def bell_sleeve(path, r0, r1, hang_d=0.14, n=12, power=1.6, pts=9, flat=0.9, pouch=None):
    """sleeve along a path whose lower edge sags (the top line follows the path).  Default: the sag grows towards
    a hanging bell cuff; pouch=t0 instead hangs a pouch of cloth between fraction t0 and the (narrow) cuff.
    Open at the cuff.  Returns (Part, cuff ring (n,3))."""
    P = M.polyline(path, pts)
    t = np.linspace(0, 1, pts)
    if pouch is None:
        sag = hang_d * t ** power
    else:
        sag = hang_d * np.sin(math.pi * np.clip((t - pouch) / (1.0 - pouch), 0, 1)) ** power
    rv = r0 + (r1 - r0) * t + sag
    rh = (r0 + (r1 - r0) * t) * flat
    Pc = P.copy()
    Pc[:, 2] -= sag
    s = tube(Pc, rv, n=n, r2=rh, cap=False, up=(0, 0, 1))
    s.smooth = 180.0
    return s, s.v[-n:].copy()


def ring_cap(ring, scale=0.97, push=(0, 0, 0)):
    """flat fan closing a ring (dark lining inside a cuff / hem)"""
    R = np.asarray(ring, dtype=np.float64)
    c = R.mean(0)
    v = np.vstack([c + (R - c) * scale, c]) + np.asarray(push, dtype=np.float64)
    n = len(R)
    return M.Part(v, [(n, j, (j + 1) % n) for j in range(n)])


def praying_hand(kind='male', wrist=(0.016, -0.21, 1.29), fingers=(0.0, -0.35, 0.94), ratio=0.7):
    """the LEFT hand of the base body moved rigidly into a prayer position on the centre line: palm facing -X,
    fingers along `fingers`, thumb towards the chest.  Unweighted Part (bind it to a spine bone; mirror for the right)."""
    b = KC.ref_body(kind)
    h = b.sub(b.vmask_faces(b.wsum(M.RIG.chain('hand_l')) > 0.5, 'any'))
    if ratio < 1.0:
        h = M.decimate(h, ratio)
    u = M.unit(fingers)
    ex = np.array([1.0, 0.0, 0.0])
    ey = np.cross(ex, u)
    Rm = np.stack([u, ey, ex], axis=1)            # rest +X -> fingers, rest +Z (back of the hand) -> +X
    Wr = M.RIG.H('hand_l')
    h.v = (h.v - Wr) @ Rm.T + np.asarray(wrist, dtype=np.float64)
    h.W = None
    h.smooth = 180.0
    return h
