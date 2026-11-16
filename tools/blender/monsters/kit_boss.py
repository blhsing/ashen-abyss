"""Helpers for the mire / brass monsters and the seven region bosses (kit_a/b/c/... stay read-only, new code goes here):
draped-grid normals, thatch blades (reeds, straw, feathers), irregular pleats that are real fold geometry, hanging
props (cords, fish bones, bladders), woven basket hats, hanging hair, riveted / lamed plate helpers, pole-arm heads.
Everything returns Parts in rest (T-pose) armature space unless stated otherwise."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_b as KB2
import kit_c as K
import kit_a as KA
from monlib import V, tube, ellipsoid, box, join, loft, rings_z, sheet, lerp, hang, arc_pts, revolve, torus, horn, unit


# ---------------------------------------------------------------------------------------------- grids
def grid_normals(G, closed=True):
    """(outward unit normals, unit side vectors along the rings) of a draped grid (rows, cols, 3)"""
    G = np.asarray(G, dtype=np.float64)
    if closed:
        du = np.roll(G, -1, 1) - np.roll(G, 1, 1)
    else:
        du = np.zeros_like(G)
        du[:, 1:-1] = G[:, 2:] - G[:, :-2]
        du[:, 0] = G[:, 1] - G[:, 0]
        du[:, -1] = G[:, -1] - G[:, -2]
    dv = np.zeros_like(G)
    dv[1:-1] = G[2:] - G[:-2]
    dv[0] = G[1] - G[0]
    dv[-1] = G[-1] - G[-2]
    nr = np.cross(du, dv)
    nr /= np.maximum(np.linalg.norm(nr, axis=2, keepdims=True), 1e-9)
    cen = G.reshape(-1, 3).mean(0)
    if ((G - cen) * nr).sum(-1).mean() < 0:
        nr = -nr
    side = du / np.maximum(np.linalg.norm(du, axis=2, keepdims=True), 1e-9)
    return nr, side


def half_shift(G):
    """grid whose columns sit between the columns of a closed grid (second tier of thatch / scales)"""
    return (G + np.roll(G, -1, 1)) / 2.0


def thatch(G, cols=None, i0=0, i1=None, width=0.034, fill=1.25, lift=0.012, over=(0.06, 0.18), seed=0, vee=0.006, taper=0.3,
           flare=0.02, jit=0.25, closed=True, wmin=0.008, down=(0, 0, -1.0), smooth=30.0):
    """flat blades with a V section lying along the columns of a draped grid (reed cloak, straw, feathers, bark strips):
    every blade follows its column from row i0 to i1 lifted off the surface, then hangs free by `over` (min, max) m
    along `down`. Returns a list of unweighted Parts (skin them like the cloth they lie on)."""
    G = np.asarray(G, dtype=np.float64)
    m, n, _ = G.shape
    i1 = m - 1 if i1 is None else i1
    nr, side = grid_normals(G, closed)
    rng = np.random.default_rng(seed)
    dn = unit(down)
    out = []
    for j in (range(n) if cols is None else cols):
        P = G[i0:i1 + 1, j].copy(); Nn = nr[i0:i1 + 1, j].copy(); S = side[i0:i1 + 1, j].copy()
        k = len(P)
        jl = (j - 1) % n if closed else max(j - 1, 0)
        jr = (j + 1) % n if closed else min(j + 1, n - 1)
        sp = np.linalg.norm(G[i0:i1 + 1, jr] - G[i0:i1 + 1, jl], axis=1) / max(1, (1 if jl == j or jr == j else 2))
        w = np.clip(sp * fill, wmin, width) * rng.uniform(0.75, 1.2)
        P = P + Nn * (lift * rng.uniform(0.6, 1.5)) + S * (w * jit * rng.uniform(-1, 1))[:, None]
        Lo = rng.uniform(over[0], over[1])
        o = Nn[-1].copy()
        o = o - dn * np.dot(o, dn)
        e1 = P[-1] + dn * Lo * 0.5 + o * flare * 0.6
        e2 = P[-1] + dn * Lo + o * flare * rng.uniform(0.2, 1.5)
        P = np.vstack([P, e1, e2]); Nn = np.vstack([Nn, Nn[-1], Nn[-1]]); S = np.vstack([S, S[-1], S[-1]])
        w = np.r_[w, w[-1] * 0.8, w[-1] * taper]
        w[0] *= 0.7
        rows = np.stack([P - S * w[:, None] / 2, P + Nn * vee, P + S * w[:, None] / 2], axis=1)
        s = sheet(rows)
        s.smooth = smooth
        out.append(s)
    return out


# ---------------------------------------------------------------------------------------------- folds
def pleat_mod(seed=0, ks=(5, 8, 13), amp=0.07, hem_z=0.05, top_z=1.0, sharp=0.7, base=0.15, twist=0.0):
    """radial multiplier for rings_z / drape: irregular pleats (several incommensurate waves with random phases, valleys
    pinched sharper than the ridges) that deepen from top_z (base x amp) down to hem_z (amp); twist (rad/m) makes the
    folds spiral slightly as they fall -> real fold geometry, the looks only add the weave on top."""
    rng = np.random.default_rng(seed)
    ph = rng.uniform(0, 6.28, len(ks))
    am = rng.uniform(0.55, 1.0, len(ks))
    am = am / am.sum()

    def f(th, z):
        th = np.asarray(th, dtype=np.float64)
        if np.abs(th).max() > 6.5:          # kit_wisp.drape passes degrees
            th = np.radians(th)
        t = np.clip((top_z - z) / max(top_z - hem_z, 1e-6), 0, 1)
        t = base + (1 - base) * t
        s = np.zeros_like(th)
        for k, p, a in zip(ks, ph, am):
            s = s + a * np.sin(k * th + p + twist * (top_z - z) * k * 0.3)
        s = np.clip(s * 1.6, -1, 1)
        s = np.where(s < 0, -np.abs(s) ** sharp, np.abs(s) ** (1.0 / max(sharp, 1e-3)) * 0.8)
        return 1.0 + amp * t * s
    return f


def gown2(sections, n=40, mod=None, rag=0.0, seed=1, cap_top=False, spikes=0.35):
    """lofted gown from hem (low z) to top with a custom fold modulation; returns (Part, rings)"""
    R = rings_z(sections, n, mod=mod)
    if rag > 0:
        R[0, :, 2] += KC.ragged(n, rag, seed, spikes)
        R[1, :, 2] = np.maximum(R[1, :, 2], R[0, :, 2] + 0.02)
    p = loft(R, cap0=False, cap1=cap_top)
    p.smooth = 180.0
    return p, R


def crumple(part, amp=0.006, freq=9.0, seed=0.0, zscale=0.35):
    """low-frequency creasing of a cloth shell along its normals (noise stretched vertically = drag folds)"""
    n = part.normals()
    d = np.array([M.fbm((p[0], p[1], p[2] * zscale), freq, seed, 2) for p in part.v])
    part.v += n * (d * amp)[:, None]
    return part


# ---------------------------------------------------------------------------------------------- hanging props
def cord(a, b, r=0.003, sag=0.0, n=3, pts=4):
    P = KB2.drape_path(a, b, sag, pts) if sag else np.linspace(np.asarray(a, float), np.asarray(b, float), 2)
    c = tube(P, r, n=n)
    c.smooth = 60.0
    return c


def fishbone(Ln=0.17, ribs=6, r=0.0036):
    """fish skeleton hanging head-up: snout at the origin, tail at z = -Ln, ribs spread along X"""
    P = [ellipsoid(Ln * 0.085, Ln * 0.045, Ln * 0.12, n=6, m=4).move(0, 0, -Ln * 0.11)]
    P[0].v[:, 0] *= 1 + 0.5 * np.clip(-(P[0].v[:, 2] + Ln * 0.11) / (Ln * 0.12), -1, 1) * 0.6       # wedge skull, wide gill end
    P.append(tube([(0, 0, -Ln * 0.2), (0, 0, -Ln * 0.6), (0, 0, -Ln * 0.94)], [r, r * 0.8, r * 0.5], n=4))
    for i in range(ribs):
        t = (i + 0.5) / ribs
        z = -Ln * (0.25 + 0.6 * t)
        ln = Ln * 0.27 * (1 - 0.7 * t) + 0.008
        for sx in (-1, 1):
            P.append(horn([(0, 0, z), (sx * ln, 0.004, z - ln * 0.6)], r * 0.75, n=3, r_tip=0.0006, cap0=False))
    for sx in (-1, 1):
        P.append(horn([(0, 0, -Ln * 0.92), (sx * Ln * 0.13, 0, -Ln * 1.1)], r * 0.9, n=3, r_tip=0.0008, cap0=False))
    f = join(P)
    f.smooth = 50.0
    return f


def bladder(r=0.03, n=8):
    """swollen fish bladder / gourd hanging from the origin (neck at the top, belly below)"""
    b = revolve([(0.0, 0.0), (r * 0.22, -r * 0.25), (r * 0.3, -r * 0.7), (r * 0.82, -r * 1.35), (r, -r * 2.0), (r * 0.8, -r * 2.7),
                 (r * 0.3, -r * 3.05), (0.0, -r * 3.1)], n, cap=False)
    b.smooth = 80.0
    return b


def toad_skull(s=0.05):
    """wide flat skull with two big eye holes, facing -Y, centred on the origin. Returns (bone Part, dark socket Part)"""
    sk = ellipsoid(s, s * 0.85, s * 0.42, n=10, m=6)
    t = np.clip(-sk.v[:, 1] / (s * 0.85), 0, 1)
    sk.v[:, 0] *= 1 - 0.35 * t ** 2
    sk.v[:, 2] *= 1 - 0.3 * t
    so = join([ellipsoid(s * 0.3, s * 0.3, s * 0.2, n=7, m=4).move(sx * s * 0.45, -s * 0.18, s * 0.3) for sx in (-1, 1)])
    return sk, so


# ---------------------------------------------------------------------------------------------- basket hat / hair
def basket_hat(R=0.30, h=0.085, n=24, ribs=10, warp=0.014, seed=0):
    """wide, shallow woven disc hat: apex at z = h, rim at z = 0. Returns (shell, ribs + rim Part)"""
    rng = np.random.default_rng(seed)
    prof = [(0.0, h * 0.72), (R * 0.16, h * 0.68), (R * 0.55, h * 0.36), (R * 0.97, -0.006), (R * 1.0, 0.0), (R * 1.005, 0.008),
            (R * 0.55, h * 0.50), (R * 0.16, h * 0.90), (0.0, h)]
    hat = revolve(prof, n, cap=False)
    th = np.arctan2(hat.v[:, 1], hat.v[:, 0])
    rr = np.hypot(hat.v[:, 0], hat.v[:, 1]) / R
    ph = rng.uniform(0, 6.28, 3)
    hat.v[:, 2] += warp * rr ** 2 * (np.sin(2 * th + ph[0]) + 0.6 * np.sin(3 * th + ph[1]) + 0.4 * np.sin(5 * th + ph[2]))
    hat.smooth = 60.0
    extra = []
    for k in range(ribs):
        a = 2 * math.pi * (k + 0.37 * rng.uniform(-0.3, 0.3)) / ribs
        pts = []
        for (r_, z_) in ((0.03, h * 0.995), (R * 0.16, h * 0.905), (R * 0.55, h * 0.505), (R * 1.0, 0.009)):
            x = r_ * math.cos(a); y = r_ * math.sin(a); q = r_ / R
            z = z_ + warp * q ** 2 * (math.sin(2 * a + ph[0]) + 0.6 * math.sin(3 * a + ph[1]) + 0.4 * math.sin(5 * a + ph[2]))
            pts.append((x, y, z + 0.002))
        extra.append(tube(pts, 0.0055, n=3))
    a = np.linspace(0, 2 * math.pi, n, endpoint=False)
    rim = np.stack([R * np.cos(a), R * np.sin(a),
                    0.003 + warp * (np.sin(2 * a + ph[0]) + 0.6 * np.sin(3 * a + ph[1]) + 0.4 * np.sin(5 * a + ph[2]))], axis=1)
    extra.append(tube(rim, 0.009, n=4, closed=True))
    extra.append(ellipsoid(0.03, 0.03, 0.016, n=8, m=4).move(0, 0, h))
    ex = join(extra)
    ex.smooth = 50.0
    return hat, ex, rim


def hair(anchors, outs, lengths, width=0.018, rows=7, seed=0, wave=0.012, flare=0.0, cols=2, bone_top='Head', bone_low='spine_03',
         low=0.45, taper=0.75):
    """hanging hair ribbons, weighted bone_top at the root blending to `low` x bone_low at the tips. Returns Parts."""
    out = []
    A = np.asarray(anchors, dtype=np.float64)
    Ls = np.broadcast_to(np.asarray(lengths, dtype=np.float64), (len(A),))
    for k, s in enumerate(KB2.strands(A, outs, Ls, width=width, rows=rows, seed=seed, wave=wave, taper=taper, cols=cols, flare=flare)):
        t = np.clip((A[k][2] - s.v[:, 2]) / max(Ls[k], 1e-6), 0, 1)
        M.weights_from(s, {bone_top: 1 - low * t, bone_low: low * t})
        out.append(s)
    return out


# ---------------------------------------------------------------------------------------------- weapons
def to_hand(parts, tilt=0.0, side='r', hunch=None):
    """[(Part, look, smooth)] built in weapon space (+Y along the shaft, origin = fist) -> rest-space parts on the hand
    (optionally tilted like a carried staff and moved with a Hunch). Returns the same list."""
    for p, lk, sm in parts:
        if tilt:
            KB2.staff_tilt(p, tilt)
        p.to_grip(side)
        if hunch is not None:
            hunch.rigid(p, 'hand_' + side)
    return parts


def weapon_axis(tilt=0.0, side='r', hunch=None):
    """rest-space direction of the weapon's +Y after to_hand (for wood grain axes)"""
    q = M.Part([(0, 0, 0), (0, 1, 0)], [])
    to_hand([(q, None, 0)], tilt, side, hunch)
    return tuple(float(x) for x in unit(q.v[1] - q.v[0]))


# ---------------------------------------------------------------------------------------------- mud / wood props
def surf_path(surf, pts, lift=0.01, n=12):
    """polyline re-sampled to n points and seated on a Surf (nearest point + face normal x lift)"""
    P = M.polyline(pts, n)
    out = []
    for p in P:
        loc, nrm, _ = surf.nearest(p)
        out.append(loc + unit(nrm) * lift)
    return np.array(out)


def lump(rx, ry, rz, seed=0, amp=0.22, freq=9.0, n=10, m=6):
    """clod / boulder of soft matter: roughened ellipsoid centred on the origin"""
    e = ellipsoid(rx, ry, rz, n=n, m=m)
    e.rough(min(rx, ry, rz) * amp, freq, seed=float(seed))
    e.smooth = 180.0
    return e


def plank(Ln=0.9, w=0.16, th=0.034, seed=0, warp=0.02, split=True):
    """weathered board along +Y from the origin (width X, thickness Z) with a splintered far end"""
    rng = np.random.default_rng(seed)
    ys = [0.0, Ln * 0.3, Ln * 0.62, Ln * 0.86, Ln]
    rings = []
    for i, y in enumerate(ys):
        bow = warp * math.sin(math.pi * y / Ln)
        k = 1.0 - 0.05 * i
        rg = [(-w / 2 * k, y, -th / 2 + bow), (w / 2 * k, y, -th / 2 + bow), (w / 2 * k, y, th / 2 + bow), (-w / 2 * k, y, th / 2 + bow)]
        rings.append(rg)
    R = np.array(rings, dtype=np.float64)
    if split:                                   # broken end: one corner long, the other snapped short
        R[-1, 0, 1] -= Ln * rng.uniform(0.08, 0.2); R[-1, 3, 1] -= Ln * rng.uniform(0.08, 0.2)
        R[-1, 1, 1] += Ln * rng.uniform(0.0, 0.06); R[-1, 2, 1] += Ln * rng.uniform(0.0, 0.06)
        R[-2, :2, 0] *= 0.9
    p = loft(R, cap0=True, cap1=True).recalc()
    p.smooth = 25.0
    return p


def eel_trap(Ln=0.5, R=0.14, ribs=9, hoops=3, seed=0, r=0.006):
    """broken conical wicker fish trap: mouth ring at the origin, tapering along +Y to a tied tip"""
    rng = np.random.default_rng(seed)
    P = []
    for k in range(ribs):
        a = 2 * math.pi * k / ribs
        cut = rng.uniform(0.35, 0.7) if rng.uniform() < 0.3 else 1.0
        pts = []
        for t in np.linspace(0, cut, 5):
            rr = R * (1 - 0.78 * t ** 1.3) * (1.0 + (0.5 * (t / cut) ** 2 if cut < 1 else 0.0))
            pts.append((rr * math.cos(a), Ln * t, rr * math.sin(a)))
        P.append(tube(pts, r, n=3))
    for h in range(hoops):
        t = h / hoops
        rr = R * (1 - 0.78 * t ** 1.3) + r
        P.append(torus(rr, r * 1.2, n=10, m=3).rot('x', 90).move(0, Ln * t, 0))
    P.append(ellipsoid(R * 0.24, R * 0.2, R * 0.24, n=6, m=4).move(0, Ln, 0))
    t = join(P)
    t.smooth = 50.0
    return t


def patch_shell(body, vmask, offset=0.012, sink=0.004, mode='all'):
    """like kit_flesh.shell but the patch borders dive back under the surface (no floating decal edge): interior verts
    are lifted by `offset`, border verts sunk by `sink`. Keeps the body's weights."""
    s = body.sub(body.vmask_faces(vmask, mode))
    cnt = {}
    for f in s.f:
        for k in range(len(f)):
            e = (min(f[k], f[(k + 1) % len(f)]), max(f[k], f[(k + 1) % len(f)]))
            cnt[e] = cnt.get(e, 0) + 1
    border = np.zeros(len(s.v), dtype=bool)
    for (a, b), c in cnt.items():
        if c == 1:
            border[a] = True; border[b] = True
    d = np.where(border, -sink, offset)
    s.inflate(d)
    s.smooth = 180.0
    return s


def rim_split(part, R, frac=0.62, c=(0.0, 0.0), under=True):
    """split a fungus cap / bracket (built around local +Z, before placing) into (dark crown, lit margin): faces whose
    centre lies beyond frac x R from c in XY, and (under=True) every face on the underside, go to the margin"""
    fc = part.face_centers()
    far = np.hypot(fc[:, 0] - c[0], fc[:, 1] - c[1]) > frac * R
    if under:
        far |= fc[:, 2] < -1e-4
    a = part.sub(~far); b = part.sub(far)
    a.smooth = b.smooth = part.smooth
    return a, b


# ---------------------------------------------------------------------------------------------- riveted plate work
def ball(c, r, n=10, m=6):
    b = ellipsoid(r, r, r, n=n, m=m).move(c)
    b.smooth = 60.0
    return b


def riveted_shell(secs, n=24, seams=(), hoops=(), seam_w=0.018, th=0.006, rivet_r=0.008, per_seam=5, per_hoop=14, mod=None, hoop_w=0.03,
                  cap0=True, cap1=True, zpad=0.025):
    """closed lofted plate (K.torso_plate sections) with raised strap seams at the given angles (deg, -90 = front), hoop
    bands at the given heights and dome rivets riding on both. Returns (shell, straps Part or None, rivets Part or None)."""
    shell = K.torso_plate(secs, n=n, mod=mod, cap0=cap0, cap1=cap1)
    z0 = secs[0][0]; z1 = secs[-1][0]
    if z0 > z1:
        z0, z1 = z1, z0
    straps = []; rv = []
    for a in seams:
        zs = np.linspace(z0 + zpad, z1 - zpad, 7)
        straps.append(K.surf_strip(secs if secs[0][0] < secs[-1][0] else secs[::-1], [(z, a) for z in zs], w=seam_w, th=th, mod=mod))
        for z in np.linspace(z0 + zpad * 1.6, z1 - zpad * 1.6, per_seam):
            p, nr = K.loft_point(secs if secs[0][0] < secs[-1][0] else secs[::-1], z, a, mod, out=th * 0.9)
            rv.append(K.rivet(p, nr, rivet_r))
    for z in hoops:
        sc = secs if secs[0][0] < secs[-1][0] else secs[::-1]
        straps.append(K.surf_strip(sc, [(z, a) for a in np.linspace(-180, 180, n + 1)], w=hoop_w, th=th, mod=mod))
        for a in np.linspace(-180, 180, per_hoop, endpoint=False) + 180.0 / per_hoop:
            p, nr = K.loft_point(sc, z, a, mod, out=th * 0.9)
            rv.append(K.rivet(p, nr, rivet_r))
    st = join(straps) if straps else None
    if st is not None:
        st.smooth = 30.0
    return shell, st, (join(rv) if rv else None)


def fan_axe(R=0.36, r0=0.05, a0=-52.0, a1=52.0, th=0.018, segs=10, c=(0.0, 0.0, 0.0), flute=0.45, scallop=0.05):
    """fan-shaped axe blade in weapon space: a fluted circular sector opening towards +X around c (shaft along +Y),
    thick at the socket, thin scalloped edge. Returns one closed Part."""
    rings = []
    for k in range(segs + 1):
        u = k / segs
        a = math.radians(a0 + (a1 - a0) * u)
        er = np.array([math.cos(a), math.sin(a), 0.0])
        rib = 1.0 if k % 2 == 0 else flute
        Rk = R * (1.0 - scallop * (1.0 if k % 2 else 0.0)) * (0.93 if k in (0, segs) else 1.0)
        rm = r0 + (Rk - r0) * 0.55
        t0 = th * 0.5; tm = th * 0.5 * rib
        rings.append([np.asarray(c) + er * r0 + (0, 0, t0), np.asarray(c) + er * rm + (0, 0, tm), np.asarray(c) + er * Rk,
                      np.asarray(c) + er * rm - (0, 0, tm), np.asarray(c) + er * r0 - (0, 0, t0)])
    p = loft(np.array(rings), cap0=True, cap1=True).recalc()
    p.smooth = 24.0
    return p


def lame(r=0.13, h=0.085, n=12, squash=0.95):
    """shoulder lame: shallow dome with a turned rim, apex +Z, centred on the origin"""
    d = revolve([(0.0, h), (r * 0.38, h * 0.92), (r * 0.76, h * 0.64), (r * 0.96, h * 0.24), (r, 0.0), (r * 1.03, -h * 0.06), (r * 0.9, -h * 0.02)], n=n)
    d.scale((1.0, squash, 1.0))
    d.smooth = 40.0
    return d


# ---------------------------------------------------------------------------------------------- posture / chains / boss props
def hunch_all(C, pose, skip=()):
    """bake a posture (rot / move entries of a pose_sculpt dict) into every part already added to the creature: rigid
    parts move with their bone, skinned parts through their weights. Build the creature upright in rest space with all
    kit helpers, then call this once at the end. Returns the Hunch."""
    H = KB2.Hunch(pose)
    for p in C.parts:
        if p in skip:
            continue
        if p.bone is not None:
            H.rigid(p, p.bone)
        else:
            H.skinned(p)
    return H


def chain_on(C, path, look, surf, allowed, link=0.06, r=0.01, n=7, m=4, obj=None, smooth=60):
    """chain lying on a skinned surface: every link is rigid on the dominant bone of the nearest surface point"""
    for t, p in KB2.links(path, link, r, n=n, m=m):
        C.add(t, bone=surf.bone_at(p, allowed), look=look, obj=obj, smooth=smooth)


def chain_rigid(path, link=0.06, r=0.01, n=7, m=4):
    c = join([t for t, _ in KB2.links(path, link, r, n=n, m=m)])
    c.smooth = 60.0
    return c


def headstone(w=0.50, h=0.80, th=0.10, seed=0, arch=0.5):
    """weathered grave slab standing on the origin (width X, thickness Y, height Z) with a round-arched top, chipped
    corners and three incised lines + ring. Returns (stone Part, incised Part)."""
    rng = np.random.default_rng(seed)
    hw = w / 2
    ha = h - hw * arch * 2 * 0.5
    rings = []
    for x in np.linspace(-hw, hw, 11):
        top = ha + hw * arch * math.sqrt(max(0.0, 1 - (x / hw) ** 2))
        zs = np.linspace(0, top, 7)
        rings.append([(x, -th / 2, z) for z in zs] + [(x, th / 2, z) for z in zs[::-1]])
    s = loft(np.array(rings), cap0=True, cap1=True).recalc()
    s.bevel(th * 0.14, 1, 40)
    for k in range(7):                                    # knocked-off corners and edge chips
        c = np.array([rng.choice([-hw, hw]) * rng.uniform(0.85, 1.0), rng.choice([-1, 1]) * th / 2, rng.uniform(0.05, h)])
        s.dent(c, rng.uniform(0.05, 0.11), -rng.uniform(0.02, 0.05))
    s.rough(0.006, 9.0, seed=float(seed))
    s.smooth = 32.0
    inc = []
    for k, z in enumerate((ha * 0.36, ha * 0.48, ha * 0.60)):
        inc.append(box(w * (0.56 - 0.1 * k), 0.012, 0.016).move(0, -th / 2 - 0.001, z))
    inc.append(torus(w * 0.15, 0.011, n=14, m=4, ry=w * 0.15).rot('x', 90).move(0, -th / 2 - 0.001, ha * 0.84))
    return s, join(inc)


def spade(Ln=0.56, w=0.38, dish=0.045, th=0.01, cols=9, rows=8, seed=0):
    """digging blade in weapon space: socket end at the origin, point towards +Y, dished about the shaft axis, a raised
    spine, worn / chipped tip. Returns one closed Part."""
    rng = np.random.default_rng(seed)
    G = np.zeros((rows + 1, cols, 3))
    for i in range(rows + 1):
        t = i / rows
        y = Ln * t
        hw = (w / 2) * (1.0 - 0.10 * t) * (1.0 if t < 0.62 else math.sqrt(max(0.0, 1 - ((t - 0.62) / 0.40) ** 2)))
        hw = max(hw, 0.012)
        for j in range(cols):
            u = j / (cols - 1) * 2 - 1
            x = u * hw
            z = -dish * (1 - u * u) * (0.35 + 0.65 * math.sin(math.pi * min(1.0, t * 1.2))) + 0.012 * math.exp(-(u / 0.16) ** 2) * (1 - t)
            G[i, j] = (x, y + (0.035 * (1 - abs(u)) if i == rows else 0.0), z)
    G[-1, :, 1] += rng.uniform(-0.02, 0.012, cols)                 # chewed edge
    G[0, :, 1] += 0.03 * (np.abs(np.linspace(-1, 1, cols)) ** 2)    # shoulders rise to the foot-rests
    p = sheet(G).solidify(th).recalc()
    p.smooth = 30.0
    return p


def sack_hood(c=(0.0, -0.02, 1.675), rx=0.128, ry=0.142, rz=0.165, n=20, seed=0, skirt=0.10):
    """sack pulled over a head: sagging bag with two tied corners, gathered at the neck, short ragged skirt below the cord.
    Returns (sack Part, cord Part)."""
    rng = np.random.default_rng(seed)
    prof = [(rx * 1.12, -rz * 1.22 - skirt), (rx * 0.98, -rz * 1.22 - skirt * 0.45), (rx * 0.74, -rz * 1.12), (rx * 0.80, -rz * 0.98), (rx * 1.0, -rz * 0.55),
            (rx * 1.04, -rz * 0.05), (rx * 0.98, rz * 0.42), (rx * 0.80, rz * 0.78), (rx * 0.42, rz * 0.98), (0.0, rz * 1.02)]
    secs = [(z, 0, 0, r, r * ry / rx) for r, z in prof]
    mod = pleat_mod(seed=seed, ks=(4, 7, 11), amp=0.07, hem_z=-rz * 1.22 - skirt, top_z=rz * 0.9, base=0.25)
    R = rings_z(secs, n, mod=mod)
    R[0, :, 2] -= KC.ragged(n, 0.05, seed)
    s = loft(R, cap0=False, cap1=True)
    crumple(s, 0.007, 22.0, float(seed), zscale=1.0)
    for sx in (-1, 1):                                              # the sack's two corners, tied off
        s.dent((sx * rx * 0.72, 0.0, rz * 0.86), rx * 0.42, rx * 0.5, direction=(sx * 0.8, 0, 0.7))
    s.move(c)
    s.smooth = 180.0
    cord = torus(rx * 0.80, 0.012, n=14, m=5, ry=rx * 0.80 * ry / rx).move(c[0], c[1], c[2] - rz * 1.10)
    return s, cord


def key_ring(n_keys=4, R=0.045, seed=0):
    """iron ring hanging from the origin (in the XZ plane) with long grave keys. Returns one Part."""
    rng = np.random.default_rng(seed)
    P = [torus(R, 0.006, n=12, m=4).rot('x', 90).move(0, 0, -R)]
    for k in range(n_keys):
        a = math.radians(-90 + (k - (n_keys - 1) / 2) * 24)
        p0 = np.array([R * math.cos(a), rng.uniform(-0.008, 0.008), -R + R * math.sin(a)])
        ln = rng.uniform(0.11, 0.17)
        d = np.array([math.cos(a) * 0.35, 0, -1.0]); d /= np.linalg.norm(d)
        P.append(tube([p0, p0 + d * ln], 0.0055, n=4))
        P.append(torus(0.014, 0.0045, n=6, m=3).rot('x', 90).move(p0 + d * 0.012))
        P.append(box(0.03, 0.006, 0.022).move(p0 + d * (ln - 0.012) + (0.015, 0, 0)))
    k_ = join(P)
    k_.smooth = 40.0
    return k_


def rope_coil(R=0.11, r=0.016, turns=4, n=14):
    """coil of rope hanging flat against a hip: axis Y, centred on the origin"""
    P = []
    for k in range(turns):
        P.append(torus(R - 0.006 * (k % 2), r, n=n, m=5).rot('x', 90).move(0, (k - (turns - 1) / 2) * r * 1.5, 0.004 * ((k * 3) % 2)))
    P.append(tube([(0, -0.03, R), (0.01, -0.03, R * 0.2), (0, -0.03, -R * 1.4)], r * 0.9, n=5))
    c = join(P)
    c.smooth = 60.0
    return c


# ---------------------------------------------------------------------------------------------- prelate / robes
def open_front(G, gap=4, front=-90.0):
    """columns of a closed draped grid with 2*gap+1 columns removed around the front (theta of column j taken from its
    hem position): returns an open grid starting at one front edge and ending at the other."""
    m, n, _ = G.shape
    c = G[-1].mean(0)
    th = np.degrees(np.arctan2(G[-1, :, 1] - c[1], G[-1, :, 0] - c[0]))
    jf = int(np.argmin(np.abs(((th - front + 180) % 360) - 180)))
    return np.roll(G, -(jf + gap + 1), axis=1)[:, : n - (2 * gap + 1)]


def rot_holes(part, zmax, chance=0.25, seed=0, zmin=None):
    """moth / rot holes: drop random faces of a sheet, more of them towards zmin (the hem)"""
    rng = np.random.default_rng(seed)
    fc = part.face_centers()
    z0 = fc[:, 2].min() if zmin is None else zmin
    t = np.clip((zmax - fc[:, 2]) / max(zmax - z0, 1e-6), 0, 1)
    keep = rng.uniform(0, 1, len(fc)) > chance * t ** 1.5
    q = part.sub(keep)
    q.smooth = part.smooth
    return q


def mitre(c=(0.0, -0.015, 1.77), rx=0.098, ry=0.112, H=0.40, n=22, rows=8, side=0.5, pinch=0.6):
    """two-horned mitre: an oval band that rises into a front and a back gothic point with a cleft between them.
    Returns (Part, list of the front ridge points from brow to tip)."""
    rg = M.ring2d(n, 2.0)
    th = np.arctan2(rg[:, 1], rg[:, 0])
    rings = []
    for i in range(rows + 1):
        t = i / rows
        fb = np.abs(np.sin(th))                                   # 1 at front / back, 0 at the sides
        x = rx * rg[:, 0] * (1 + 0.24 * math.sin(math.pi * min(1.0, t * 1.4))) * (1 - 0.97 * t ** 2.6)
        y = np.sign(rg[:, 1]) * ry * np.abs(rg[:, 1]) ** 0.7 * (1 - pinch * t)
        z = H * t * (side + (1 - side) * fb ** 0.8)
        rings.append(np.stack([c[0] + x, c[1] + y, c[2] + z], axis=1))
    p = loft(np.array(rings), cap0=False, cap1=True).recalc()
    p.smooth = 50.0
    jf = int(np.argmin(th + math.pi / 2 if False else np.abs(th + math.pi / 2)))
    ridge = [rings[i][jf] for i in range(rows + 1)]
    return p, ridge


def crook(R0=0.17, R1=0.045, turns=1.35, r0=0.024, r1=0.009, n=7, pts=26, c=(0.12, 0.0, 0.0)):
    """crozier volute in weapon space (plane XY): rises from the shaft top at the origin, sweeps out over +X and curls in
    on itself. Returns (Part, centre of the eye)."""
    a0 = math.pi                                                  # start on the shaft axis, left of the centre
    P = []
    for k in range(pts):
        u = k / (pts - 1)
        a = a0 - u * turns * 2 * math.pi
        R = R0 + (R1 - R0) * u ** 0.8
        P.append((c[0] + R * math.cos(a) + (R0 - c[0]) * (1 - u) ** 3 * 0.0, c[1] + R0 * 0.0 + R * math.sin(a) + R0 * 0.85, 0.0))
    P = np.array(P)
    P[:, 0] -= P[0, 0]
    P[:, 1] -= P[0, 1]
    rr = r0 + (r1 - r0) * np.linspace(0, 1, pts)
    t = tube(P, rr, n=n)
    t.smooth = 50.0
    eye = P[-1] * 0.4 + P[-6] * 0.6
    return t, P, eye


# ---------------------------------------------------------------------------------------------- seated skull
def add_skull_seated(C, look='bone', eye_look='eye', jaw_open=12.0, origin=None, tilt=0.0, scale=1.0):
    """kit_bones.add_skull with the floating parts seated (kit_bones itself stays untouched): the upper tooth row is moved
    so that its roots sit 4 mm inside the real underside of the maxilla (measured on the booleaned skull, it used to hang
    in the air below it), the mandible follows it and its rami lean in against the cranium instead of standing clear of
    it, and a condyle knob + a zygomatic arch on each side close the joint. Same arguments and return value."""
    import kit_bones as KBn
    o = KBn.SKULL_ORIGIN if origin is None else np.asarray(origin, dtype=float)
    P = KBn.skull_parts(jaw_open)
    sk = P['skull']
    zb = float(sk.v[sk.v[:, 1] < -0.05][:, 2].min())                       # underside of the face block
    yf = float(sk.v[sk.v[:, 2] < zb + 0.012][:, 1].min())                  # its front
    dz = dy = 0.0
    if 'teeth' in P:
        t = P['teeth']
        dz = (zb + 0.004) - float(t.v[:, 2].max())
        dy = (yf + 0.0015) - float(t.v[:, 1].min())
        t.v[:, 2] += dz; t.v[:, 1] += dy
    jw = P['jaw']
    jw.v[:, 2] += dz; jw.v[:, 1] += dy
    hz = -0.046 + dz
    near = (np.abs(sk.v[:, 2] - hz) < 0.014) & (np.abs(sk.v[:, 1] - 0.006) < 0.035)
    hw = float(np.abs(sk.v[near][:, 0]).max()) if near.any() else 0.045    # cranium half-width at the jaw joint
    k = np.clip((jw.v[:, 2] - (hz - 0.06)) / 0.05, 0, 1)
    jw.v[:, 0] *= 1 + (min(1.0, (hw + 0.004) / 0.056) - 1) * k
    ex = []
    for sx in (-1, 1):
        ex.append(ellipsoid(0.0115, 0.0115, 0.0115, n=7, m=4).move(sx * (hw + 0.002), 0.006 + dy, hz))
        ex.append(tube([(sx * 0.047, -0.060, -0.020), (sx * (hw + 0.009), -0.028, -0.030), (sx * (hw + 0.003), 0.004 + dy, hz + 0.006)],
                       [0.0075, 0.006, 0.0075], n=5, r2=0.0048, up=(0, 0, 1)))
    P['joint'] = join(ex)
    P['joint'].smooth = 50.0
    M.log("skull seated: face underside z=%.4f, teeth dz=%.4f dy=%.4f, cranium half-width at joint %.4f" % (zb, dz, dy, hw))
    for name, p in P.items():
        if scale != 1.0:
            p.scale(scale)
        if tilt:
            p.rot('x', tilt)
        p.move(o)
        C.add(p, bone='Head', look=eye_look if name == 'eyes' else look)
    return P
