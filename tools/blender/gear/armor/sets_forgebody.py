"""Forgebody armour looks t1..t3 (t4 = today's hero, split by split_t4.py).

Identity: branded fist-fighting monk of the dead forge -- heavy bare arms, charcoal and ash-grey cloth, a dull red
sash, black iron, bronze, tiny ember accents. Hands and knuckles stay free for the fist weapons. Hero materials reused:
wrap, char, pants, shoe, strap, iron, bronze, blackiron, ember.
  t1  forge-yard penitent: a dirty bandage bound askew round the brow, bare chest with a doubled rope sling, a torn
      burlap waist cloth with ragged skirt tails under a rope belt, bandaged fists (one forearm bound to the elbow),
      torn knee-length trousers, bandaged feet on rope soles with rope ties.
  t2  novice: grey cloth head wrap with a rolled brow band and a wrapped topknot, sleeveless ash-grey vest crossed left
      over right with dark bindings, a dark red cloth belt and a short charcoal apron, wrapped forearms with strapped
      leather pads, plain baggy trousers gathered at the shin, cloth shoes with cross-tied shin wraps.
  t3  forge master: bronze brow circlet with an ember gem (topknot free), charcoal half-robe over the RIGHT shoulder
      with a brocade edge and a split skirt, a bronze guard strapped on the left shoulder, a broad red sash with a
      bronze clasp, a short string of iron beads, bronze-banded bracers with a thin back-of-hand plate, fine trousers
      with knee pads and an embroidered apron panel, black-iron shin guards over cloth shoes.

Helpers of this file that work round the shared kit are marked in their docstrings (_cut: clean garment edges on the
coarse body mesh, _arm_rings, _psize, _soles, _headband).
"""
from gearkit import *


def _mats():
    mats.cloth("fb_bandage", base=(0.200, 0.160, 0.108), var=0.45, ash=0.35, folds=0.3, weave=1.2, worn=0.7, scan="linen", tight=True)
    mats.cloth("fb_burlap", base=(0.092, 0.074, 0.050), var=0.45, ash=0.5, ash_h=0.6, folds=0.6, worn=0.85, scan="burlap")
    mats.cloth("fb_rope", base=(0.175, 0.14, 0.088), var=0.3, ash=0.4, folds=0.0, scan="rope", hem=False)
    mats.cloth("fb_torn", base=(0.056, 0.050, 0.044), var=0.5, ash=0.65, folds=0.8, worn=0.9, scan="canvas", tight=True)
    mats.cloth("fb_vest", base=(0.086, 0.084, 0.082), var=0.3, ash=0.45, ash_h=0.5, folds=0.5, worn=0.5, scan="twill", tight=True)
    mats.cloth("fb_dark", base=(0.030, 0.028, 0.028), var=0.35, ash=0.45, ash_h=0.9, folds=0.5, worn=0.5, scan="canvas")
    mats.cloth("fb_belt", base=(0.120, 0.034, 0.022), var=0.3, ash=0.35, ash_h=0.8, folds=0.4, worn=0.5, scan="canvas")
    mats.cloth("fb_cap", base=(0.074, 0.072, 0.070), var=0.3, ash=0.4, folds=0.4, worn=0.5, scan="linen", tight=True)
    mats.leather("fb_pad", base=(0.058, 0.037, 0.023), worn=0.6, ash=0.3, scan="leather_b")
    mats.cloth("fb_robe", base=(0.036, 0.031, 0.031), var=0.3, ash=0.35, ash_h=1.0, folds=0.7, worn=0.3, scan="wool")
    mats.cloth("fb_robe_hem", base=(0.036, 0.031, 0.031), bands=[(0.035, 0.115, (0.21, 0.125, 0.042))], var=0.3, ash=0.35, ash_h=1.0, folds=0.8,
               worn=0.3, scan="wool")
    mats.cloth("fb_brocade", base=(0.205, 0.120, 0.040), var=0.35, ash=0.2, folds=0.0, worn=0.35, scan="brocade", hem=False)
    mats.cloth("fb_sash", base=(0.185, 0.038, 0.022), var=0.3, ash=0.25, ash_h=0.9, folds=0.5, worn=0.4, scan="velvet")
    mats.cloth("fb_fine", base=(0.046, 0.041, 0.040), var=0.3, ash=0.4, ash_h=0.5, folds=0.9, worn=0.3, scan="twill")
    mats.cloth("fb_apron", base=(0.040, 0.033, 0.032), bands=[(0.05, 0.20, (0.21, 0.125, 0.042))], var=0.3, ash=0.3, folds=0.4, worn=0.3,
               scan="brocade")


# ---------------------------------------------------------------------------------------------- helpers of this class
def _cut(o, co, no, sel=None, drop=None):
    """cut the faces of a body shell along a plane (the new vertices get interpolated weights) and optionally delete
    faces: sel(centre) limits the cut to some faces, drop(centre) -> True deletes a face afterwards. Gives clean
    straight garment edges (necklines, armholes, hems) on the coarse body mesh."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    faces = [f for f in bm.faces if sel is None or sel(f.calc_center_median())]
    geom = set(faces)
    for f in faces:
        geom.update(f.edges)
        geom.update(f.verts)
    if geom:
        bmesh.ops.bisect_plane(bm, geom=list(geom), dist=1e-5, plane_co=Vector(co), plane_no=Vector(no).normalized())
    if drop is not None:
        dead = [f for f in bm.faces if drop(f.calc_center_median())]
        if dead:
            bmesh.ops.delete(bm, geom=dead, context='FACES')
        loose = [v for v in bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(o.data)
    bm.free()
    o.data.update()
    return o


def _front(K, x, z, lift=0.016, front=True):
    """point `lift` above the skin where a ray along Y from in front of (behind) the body meets it (None: no hit)"""
    ok, loc, nrm, _f = K.c.ref.ray_cast(Vector((x, -1.0 if front else 1.0, z)), Vector((0, 1.0 if front else -1.0, 0)))
    return (np.array(loc) + np.array(nrm) * lift) if ok else None


def _path(K, guide, off, closed=False, step=0.014, cover=None, bare=None, smooth=2, tuck=0.0):
    """path lying on the body through the guide points -> (points, outward normals). `off` above the skin, or `bare`
    where the nearest skin vertex is not in the `cover` mask (a strap that runs from cloth on to bare skin)"""
    c, B = K.c, K.B
    G = [np.array(p, float) for p in guide]
    P = resample(G + ([G[0]] if closed else []), step)
    if closed:
        P = P[:-1]
    n = len(P)

    def proj(X):
        S_, N_ = [], []
        for p in X:
            ok, loc, nrm, fi = c.ref.closest_point_on_mesh(Vector(p))
            S_.append(np.array(loc))
            N_.append(np.array(nrm))
        return np.array(S_), np.array(N_)

    def sm(X):
        if closed:
            return (np.roll(X, 1, 0) + X * 2 + np.roll(X, -1, 0)) / 4
        Y = X.copy()
        Y[1:-1] = (X[:-2] + X[1:-1] * 2 + X[2:]) / 4
        return Y
    S_, N_ = proj(P)
    offs = np.full(n, float(off))
    if cover is not None:
        idx = [int(np.argmin(((B.co - p) ** 2).sum(1))) for p in S_]
        offs = np.where(cover[idx], float(off), float(bare))
        for _ in range(3):
            offs = sm(offs[:, None])[:, 0]
    if tuck and not closed:
        t = np.linspace(0, 1, n)
        offs = offs - tuck * (1 - sstep(t, 0.0, 0.10)) - tuck * sstep(t, 0.90, 1.0)
    Q = S_ + N_ * offs[:, None]
    for _ in range(smooth):
        Q = sm(Q)
    S2, N2 = proj(Q)
    d = ((Q - S2) * N2).sum(1)
    Q = Q + N2 * np.clip(offs - d, 0, None)[:, None]
    for _ in range(3):
        N2 = sm(N2)
    N2 /= np.linalg.norm(N2, axis=1, keepdims=True)
    return Q, N2


BODY_BONES = ("spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r")


def _rope(K, name, src, guide, r=0.0075, off=0.010, closed=False, only=BODY_BONES, cls="cloth", **kw):
    """round rope / cord lying on the body"""
    Q, N = _path(K, guide, off, closed=closed, **kw)
    o = add(name, g_tube(Q, r, n=6, closed=closed), cls, src, sharp=180)
    w_transfer(o, K.c.ref, smooth=2, only=list(only))
    return o


def _band(K, name, src, guide, width=0.03, thick=0.005, off=0.018, cls="cloth", closed=False, only=BODY_BONES, sharp=60, step=0.02, **kw):
    """flat band lying on the body (like kit_mel.ribbon, but the offset may differ between cloth and bare skin)"""
    Q, N = _path(K, guide, off, closed=closed, step=step, **kw)
    n = len(Q)
    verts = []
    for i in range(n):
        a = Q[(i - 1) % n] if (closed or i > 0) else Q[i]
        b = Q[(i + 1) % n] if (closed or i < n - 1) else Q[i]
        t = b - a
        t /= max(np.linalg.norm(t), 1e-9)
        bn = np.cross(t, N[i])
        bn /= max(np.linalg.norm(bn), 1e-9)
        nn = np.cross(bn, t)
        hw = width / 2
        verts += [Q[i] - bn * hw, Q[i] + bn * hw, Q[i] + bn * hw + nn * thick, Q[i] - bn * hw + nn * thick]
    faces = []
    for i in range(n if closed else n - 1):
        j = (i + 1) % n
        for k in range(4):
            k2 = (k + 1) % 4
            faces.append((i * 4 + k, j * 4 + k, j * 4 + k2, i * 4 + k2))
    if not closed:
        faces.append((0, 1, 2, 3))
        faces.append(((n - 1) * 4 + 3, (n - 1) * 4 + 2, (n - 1) * 4 + 1, (n - 1) * 4))
    o = add(name, geo(np.array(verts), faces), cls, src, sharp=sharp)
    w_transfer(o, K.c.ref, smooth=2, only=list(only) if only else None)
    return o


def _psize(K, radius):
    """size argument of K.pauldron that gives a shoulder plate of the given radius (m) on this body (the Kit scales its
    pauldrons by upperarm_r, the largest distance from the nominal arm axis: far more than the real radius here)"""
    return radius / (0.122 * max(1.0, K.upperarm_r / 0.060) ** 0.6)


def _arm_rings(K, name, src, xs, width=0.018, off=0.016, thick=0.004, cls="metal", n=16):
    """rigid hoops round both forearms at the stations xs, shaped by the real section of the arm there (the Kit's
    arm_rings / limb_straps start from forearm_r, which is far too wide on this body, and come out loose or wavy)"""
    c, B = K.c, K.B
    gs = []
    for x in xs:
        m = (B.w("upperarm", "lowerarm") > 0.5) & (B.x > 0) & (np.abs(B.x - x) < 0.03)
        cen = np.array((x, (B.y[m].min() + B.y[m].max()) / 2, (B.z[m].min() + B.z[m].max()) / 2))
        ang = np.linspace(0, TAU, n, endpoint=False)
        rad = []
        for xx in (x - width / 2, x + width / 2):
            row = []
            for a in ang:
                d = np.array((0.0, math.cos(a), math.sin(a)))
                p0 = np.array((xx, cen[1], cen[2]))
                ok, loc, nrm, fi = c.ref.ray_cast(Vector(p0 + d * 0.25), Vector(-d))
                row.append(float(np.linalg.norm(np.array(loc) - p0)) if ok else 0.06)
            rad.append(row)
        r = np.maximum(np.array(rad[0]), np.array(rad[1]))
        r = (np.roll(r, 1) + 2 * r + np.roll(r, -1)) / 4 + off
        V, F = [], []
        for j, (xx, bulge) in enumerate(((x - width / 2, 0.0), (x, 0.0018), (x + width / 2, 0.0))):
            V += [(xx, cen[1] + (r[i] + bulge) * math.cos(a), cen[2] + (r[i] + bulge) * math.sin(a)) for i, a in enumerate(ang)]
        for j in range(2):
            F += [(j * n + i, j * n + (i + 1) % n, (j + 1) * n + (i + 1) % n, (j + 1) * n + i) for i in range(n)]
        gs.append(geo(V, F))
    o = add(name + "_l", gjoin(*gs), cls, src, bone="lowerarm_l", sharp=50, solidify=thick)
    return [o, mirrored(o)]


def _headband(K, name, src, z=1.742, width=0.034, off=0.007, tilt=0.010, roll=0.0, knot_a=math.pi, tails=(0.12, 0.08), knot_r=0.016,
              roll_r=0.0, shell_band=True):
    """cloth band round the brow, cut out of the head's own surface so that it lies on it. tilt lifts the front, roll
    lifts the left side; roll_r > 0 adds a rolled edge (turban roll) along its middle; the knot sits at angle knot_a
    (0 = front, pi = back, measured towards the character's left) with short hanging ends"""
    c, B = K.c, K.B
    nrm_p = np.array((-roll / 0.105, tilt / 0.115, 1.0))
    nrm_p /= np.linalg.norm(nrm_p)
    p0 = np.array((0.0, -0.020, z))

    def at(a):
        return (0.105 * math.sin(a), -0.020 - 0.115 * math.cos(a), z + tilt * math.cos(a) + roll * math.sin(a))
    out = []
    if shell_band:
        d = (B.co - p0) @ nrm_p
        m = (B.w("Head") > 0.5) & (np.abs(d) < width / 2 + 0.022)
        o = K.shell(name, m, src, "cloth", off=off, smooth=2)
        hw = width / 2
        _cut(o, p0 + nrm_p * hw, nrm_p, drop=lambda p: (np.array(p) - p0) @ nrm_p > hw)
        _cut(o, p0 - nrm_p * hw, nrm_p, drop=lambda p: (np.array(p) - p0) @ nrm_p < -hw)
        w_rigid(o, "Head")
        out.append(o)
    if roll_r:
        S_ = wrap_path(c, [at(a) for a in np.linspace(0, TAU, 26, endpoint=False)], off + roll_r * 0.55)
        for _ in range(2):
            S_ = (np.roll(S_, 1, 0) + S_ * 2 + np.roll(S_, -1, 0)) / 4
        r_ = add(name + "_roll", g_tube(S_, roll_r, n=7, closed=True, sx=1.15, up=(0, 0, 1)), "cloth", src, bone="Head", sharp=180)
        displace_noise(r_, 0.0025, 60.0, 4)
        out.append(r_)
    ok, loc, nrm, fi = c.ref.closest_point_on_mesh(Vector(at(knot_a)))
    nrm = np.array(nrm)
    kb = np.array(loc) + nrm * (off + 0.007 + roll_r * 0.6)
    tg = np.cross(nrm, np.array((0.0, 0.0, 1.0)))
    tg /= max(np.linalg.norm(tg), 1e-9)
    Mk = np.eye(4)
    Mk[:3, 0], Mk[:3, 1], Mk[:3, 2], Mk[:3, 3] = tg, nrm, np.cross(tg, nrm), kb
    out.append(add(name + "_knot", gxf(g_sphere(1.0, 7, 5), Mk @ S(knot_r * 1.25, knot_r * 0.85, knot_r)), "cloth", src, sharp=180, bone="Head"))
    for i, ln in enumerate(tails):
        sd = 1.0 if i == 0 else -1.0
        path = [kb, kb + nrm * 0.010 + tg * sd * 0.010 + np.array((0, 0, -ln * 0.3)), kb + nrm * 0.014 + tg * sd * 0.020 + np.array((0, 0, -ln * 0.7)),
                kb + nrm * 0.012 + tg * sd * 0.024 + np.array((0, 0, -ln))]
        t = add("%s_tail_%d" % (name, i), g_tube(path, [0.9, 1.0, 1.1, 1.25], n=5, sx=0.004, sy=width * 0.42, up=tuple(nrm)), "cloth", src, sharp=180)

        def wf(co):
            dd = sstep(kb[2] - co[:, 2], 0.05, 0.22)
            return {"Head": 1 - dd * 0.75, "neck_01": dd * 0.35, "spine_03": dd * 0.40}
        w_func(t, wf)
        out.append(t)
    return out


def _panel(K, name, src, x0, x1, z_top, z_bot, gap_top, gap_bot, front=True, z_full=0.86, **kw):
    """hanging panel whose distance from the body grows from gap_top (under a belt / hem) to gap_bot at z_full and below"""
    kw.setdefault("folds", ((1.6, 0.006, 0.2),))
    n = max(4, int(round((z_top - z_bot) / 0.09)) + 2)
    zs = np.linspace(z_bot, z_top, n)
    gaps = gap_bot + (gap_top - gap_bot) * sstep(zs, z_full, z_top)
    st = K.stations(zs, gaps, plumb=True)
    yfn = front_y(st, 0.0) if front else back_y(st, 0.0)
    o = drape(name, x0, x1, z_top, z_bot, yfn, "cloth", src, out=-1.0 if front else 1.0, **kw)
    w_func(o, hang_w)
    return o


def _baggy(K, name, src, top=1.02, bottom=0.27, off=L2, thigh=0.020, knee=0.020, calf=0.014, gather=0.39, **kw):
    """trousers with room round the thigh, the knee and the calf (each its own amount); below `gather` they lie close
    (that part goes under shin wraps / boot shafts)"""
    B = K.B
    if gather > bottom + 0.02:
        zk = [bottom, gather, gather + 0.07, 0.53, 0.66, 0.84, 0.965, 1.03]
        vk = [0.0, 0.0008, calf, knee, thigh, thigh, 0.003, 0.0]
    else:
        zk = [bottom, bottom + 0.03, 0.53, 0.66, 0.84, 0.965, 1.03]
        vk = [0.0, calf, knee, thigh, thigh, 0.003, 0.0]
    o = shell(B, m_legs(B, bottom, min(top, 1.03)), lambda co: off + np.interp(co[:, 2], zk, vk), name, "cloth", src, smooth=kw.pop("smooth", 4),
              sharp=180)
    if kw.get("chew"):
        chew_edge(o, rounds=int(kw["chew"]), p=0.4, seed=K.rs())
    if kw.get("wrinkle"):
        # no wrinkles on the part that lies under shin wraps / boot shafts (it has to stay within the 11 mm pants layer)
        wm = (lambda co: sstep(co[:, 2], gather - 0.03, gather + 0.05)) if gather > bottom + 0.02 else None
        wrinkle_shell(o, kw["wrinkle"], kw.get("wfreq", 22.0), "z", K.rs(), mask=wm)
    return o


def _hull(P):
    """convex hull of 2D points, counter-clockwise"""
    pts = sorted(set(map(tuple, np.round(np.asarray(P, float), 5))))

    def cr(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for p in pts:
        while len(lo) >= 2 and cr(lo[-2], lo[-1], p) <= 0:
            lo.pop()
        lo.append(p)
    for p in reversed(pts):
        while len(up) >= 2 and cr(up[-2], up[-1], p) <= 0:
            up.pop()
        up.append(p)
    return np.array(lo[:-1] + up[:-1])


def _soles(K, boot, name, src, cls="cloth"):
    """sole slab under a K.boots shell. K.boots flattens its sole at z = -0.004, but the feet of the body reach down to
    z = -0.010: without the slab the bare toe and heel pads hang out under the boot (and are carried along as visible
    skin), which shows whenever the sole is seen (roll)"""
    B = K.B
    co = get_co(boot)
    zb = float(B.z.min()) - 0.0025
    gs = []
    for s in (1, -1):
        h = _hull(co[(co[:, 0] * s > 0) & (co[:, 2] < 0.012)][:, :2])
        cen = h.mean(0)
        n = len(h)
        V = [(x, y, -0.0025) for x, y in h] + [(cen[0] + (x - cen[0]) * 0.95, cen[1] + (y - cen[1]) * 0.975, zb) for x, y in h]
        F = [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)] + [tuple(range(2 * n - 1, n - 1, -1))]
        gs.append(geo(V, F))
    o = add(name, gjoin(*gs), cls, src, sharp=50)
    w_transfer(o, K.c.ref, smooth=0)
    return o


def _leg_c(K, z, side=1, dz=0.02):
    B = K.B
    m = (B.x * side > 0) & (np.abs(B.z - z) < dz) & (B.w(*BONES_LEG, *BONES_FOOT) > 0.5)
    return float((B.x[m].min() + B.x[m].max()) / 2), float((B.y[m].min() + B.y[m].max()) / 2)


def _leg_ray(K, p, cen, off):
    d = np.array((cen[0] - p[0], cen[1] - p[1], 0.0))
    d /= np.linalg.norm(d)
    ok, loc, nrm, fi = K.c.ref.ray_cast(Vector(p), Vector(d))
    if ok:
        return np.array(loc) - d * off
    ok, loc, nrm, fi = K.c.ref.closest_point_on_mesh(Vector(p))
    return np.array(loc) + np.array(nrm) * off


def _leg_strip(K, z0, z1, a0, a1, off, nu=3, nv=7):
    """strip lying `off` above the skin of the LEFT leg between two heights and two angles round the leg
    (fractions of a turn: 0 = outside, .25 = back, .75 = front)"""
    zs = np.linspace(min(z0, z1), max(z0, z1), 6)
    cen = np.array([_leg_c(K, z) for z in zs])

    def fn(U, V):
        z = z0 + (z1 - z0) * V
        a = TAU * (a0 + (a1 - a0) * U)
        return np.stack([np.interp(z, zs, cen[:, 0]) + 0.095 * np.cos(a), np.interp(z, zs, cen[:, 1]) + 0.095 * np.sin(a), z], axis=-1)
    g = g_grid(fn, nu, nv, close_u=False)
    v = g["v"].copy()
    for i, p in enumerate(v):
        v[i] = _leg_ray(K, p, (np.interp(p[2], zs, cen[:, 0]), np.interp(p[2], zs, cen[:, 1])), off)
    g["v"] = v
    return g


def _leg_cord(K, name, src, z0, z1, turns, off, r=0.0045, phase=0.0, cls="leather"):
    """cord wound round both shins (every point is found by a ray towards the leg's axis, so it lies on the leg)"""
    n = int(16 * abs(turns)) + 8
    zs = np.linspace(z0, z1, 6)
    cen = np.array([_leg_c(K, z) for z in zs])
    P = []
    for t in np.linspace(0, 1, n):
        z = z0 + (z1 - z0) * t
        a = TAU * turns * t + phase
        cx, cy = np.interp(z, zs, cen[:, 0]), np.interp(z, zs, cen[:, 1])
        P.append(_leg_ray(K, np.array((cx + 0.095 * math.cos(a), cy + 0.095 * math.sin(a), z)), (cx, cy), off))
    o = add("%s_l" % name, g_tube(P, r, n=5), cls, src, bone="calf_l", sharp=180)
    return [o, mirrored(o)]


def _robe_mask(K, lo=1.0, margin=0.0):
    """one-shoulder robe: RIGHT shoulder down to the left flank (today's hero wears his over the left shoulder)"""
    B = K.B
    ztop = 1.52 + 1.25 * (-B.x - 0.08) + margin
    tor = (B.w(*BONES_TORSO, "clavicle", "neck_01", "thigh") > 0.5) & (B.z > lo) & (B.z < np.minimum(ztop, 1.535)) & (B.w("upperarm") < 0.45) & \
        (np.abs(B.x) < 0.225) & (B.w(*BONES_HAND, "lowerarm") < 0.3)
    cap = (B.x < -0.09) & (B.x > -0.262 - margin) & (B.w("clavicle", "upperarm", "spine_03") > 0.5) & (B.z > 1.395 - margin * 0.5) & (B.w("lowerarm") < 0.2)
    return tor | cap


def _robe_edge(K, drop=0.020, lift=0.02):
    """closed guide line along the diagonal edge of the robe (front, left flank, back, right shoulder)"""
    xs = np.linspace(-0.088, 0.198, 18)
    zh = [min(1.52 + 1.25 * (-x - 0.08), 1.530) - drop for x in xs]
    g = ([_front(K, x, z, lift, True) for x, z in zip(xs, zh)] + list(wrap_path(K.c, [(0.26, 0.03, 1.165 - drop)], lift)) +
         [_front(K, x, z, lift, False) for x, z in zip(xs[::-1], zh[::-1])] + list(wrap_path(K.c, [(-0.100, 0.03, 1.62)], lift)))
    return [q for q in g if q is not None]


# ---------------------------------------------------------------------------------------------- t1 forge-yard penitent
def t1(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        _headband(K, "brow_rag", "fb_bandage", z=1.746, width=0.036, off=0.0065, tilt=0.006, roll=0.012, knot_a=math.pi + 0.95, tails=(0.11, 0.065))
    with K.slot("chest"):
        m = (B.w(*BONES_TORSO, "thigh") > 0.5) & (B.z > 1.0) & (B.z < 1.125) & (B.w(*BONES_ARM, *BONES_HAND) < 0.3)
        K.shell("waist_cloth", m, "fb_burlap", off=0.0165, smooth=3, hem=(1.0, 1.07, 0.0175), chew=1, chew_p=0.35, keep=lambda co: co.z < 1.085)
        K.skirt("rag_skirt", "fb_burlap", z_top=1.05, z_bot=0.70, gap_top=0.026, gap_bot=0.062, tails=5, tail_depth=0.19, ragged=0.04,
                folds=((5, 0.035, 0.1), (9, 0.014, 0.5)), u0=0.045, u1=0.955, close=False, calf=0.0)
        K.rope("rope_belt", "fb_rope", z=1.066, gap=0.032, r=0.009, cls="cloth", turns=2)
        sling = [(0.135, -0.10, 1.455), (0.03, -0.15, 1.30), (-0.14, -0.11, 1.13), (-0.19, 0.03, 1.10), (-0.10, 0.15, 1.17), (0.05, 0.16, 1.34),
                 (0.14, 0.09, 1.46), (0.150, 0.0, 1.50)]
        _rope(K, "sling_0", "fb_rope", sling, r=0.0075, off=0.0085, closed=True)
        _rope(K, "sling_1", "fb_rope", [(x + 0.017, y, z + 0.006) for x, y, z in sling], r=0.0075, off=0.0085, closed=True)
        kp = wrap_path(c, [(0.055, -0.2, 1.335)], 0.016)[0]
        kn = add("sling_knot", gxf(g_sphere(1.0, 7, 5), T(*kp) @ R('Z', 30) @ S(0.024, 0.016, 0.019)), "cloth", "fb_rope", sharp=180)
        w_transfer(kn, c.ref, smooth=0, only=["spine_02", "spine_03"])
    with K.slot("gloves"):
        K.sleeves("wrap_l", "fb_bandage", x0=0.495, x1=0.740, off=L1, sides="l", wrinkle=0.002, waxis="x", wfreq=95.0, chew=1)
        K.sleeves("wrap_r", "fb_bandage", x0=0.600, x1=0.740, off=L1, sides="r", wrinkle=0.002, waxis="x", wfreq=95.0, chew=1)
        K.palm_wrap("palm_wraps", "fb_bandage")
    with K.slot("pants"):
        _baggy(K, "trousers", "fb_torn", top=1.02, bottom=0.425, thigh=0.012, knee=0.012, calf=0.010, gather=0.0, wrinkle=0.003, chew=2)
    with K.slot("boots"):
        bt = K.boots("foot_wraps", "fb_bandage", top=0.325, off=0.0125, cls="cloth", toe_h=0.041, toe_grow=0.007, wrinkle=0.0025, wfreq=70.0, chew=1)
        _soles(K, bt, "sole", "fb_rope")
        _leg_cord(K, "ankle_rope", "fb_rope", 0.135, 0.275, 2.0, off=0.0165, r=0.0052, cls="cloth")


# ---------------------------------------------------------------------------------------------- t2 novice
def t2(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.cap("headwrap", "fb_cap", z_brow=1.722, ears=False, off=0.008, cls="cloth", nape=1.615, wrinkle=0.0020, wfreq=55.0)
        _headband(K, "wrap", "fb_cap", z=1.740, off=0.008, tilt=0.010, knot_a=math.pi, tails=(0.085, 0.06), knot_r=0.015, roll_r=0.0135,
                  shell_band=False)
        bun = lumpy(gxf(g_sphere(1.0, 10, 6), T(0.0, 0.030, 1.822) @ S(0.039, 0.042, 0.035)), 0.07, 55.0, 4, (0.0, 0.030, 1.822))
        add("bun", bun, "cloth", "fb_cap", bone="Head", sharp=180)
        add("bun_tie", gxf(g_lathe([(0.0255, -0.006), (0.0300, 0.0), (0.0255, 0.006)], n=10), T(0.0, 0.030, 1.797)), "cloth", "fb_belt", bone="Head",
            sharp=180)
    with K.slot("chest"):
        vest = K.shell("vest", K.torso_mask(1.0, 1.545, 0.275), "fb_vest", off=0.014, smooth=3, hem=(1.0, 1.07, 0.0165), wrinkle=0.0022, wfreq=22.0)
        for s in (1.0, -1.0):                                         # straight armholes
            _cut(vest, (s * 0.216, 0, 0), (s, 0, 0), sel=lambda p: s * p.x > 0.14 and p.z > 1.28, drop=lambda p: s * p.x > 0.216 and p.z > 1.28)
        # neckline: the left front crosses over the right one (wedge between the two lapel lines is cut away)
        xl = lambda z: 0.070 + (z - 1.535) * (0.100 / 0.210)
        xr = lambda z: -0.070 - (z - 1.535) * (0.040 / 0.210)
        fr = lambda p: p.y < 0.045 and p.z > 1.30
        _cut(vest, (0.070, 0, 1.535), (-0.210, 0, 0.100), sel=fr)
        _cut(vest, (-0.070, 0, 1.535), (0.210, 0, 0.040), sel=fr)
        _cut(vest, (0, 0, 1.325), (0, 0, 1), sel=lambda p: p.y < 0.045 and abs(p.x) < 0.09 and p.z > 1.28)
        nk = lambda p: math.hypot(p.x, p.y - 0.035) < 0.090                    # round the neck only (the shoulders stay covered)
        _cut(vest, (0, 0, 1.530), (0, 0, 1), sel=lambda p: nk(p) and p.z > 1.49, drop=lambda p: nk(p) and p.z > 1.530)
        _cut(vest, (0, 0, 0), (0, 0, 0.0001), drop=lambda p: p.y < 0.045 and p.z > 1.325 and xr(p.z) < p.x < xl(p.z))
        la = [_front(K, xr(z) - 0.012, z) for z in (1.315, 1.36, 1.41, 1.46, 1.505)] + [(-0.080, 0.02, 1.540), (-0.058, 0.11, 1.530), (0.0, 0.135, 1.525),
                                                                                       (0.058, 0.11, 1.530), (0.080, 0.02, 1.540)]
        lb = [(0.080, 0.03, 1.540)] + [_front(K, xl(z) + 0.012, z) for z in (1.505, 1.46, 1.41, 1.36, 1.31, 1.26, 1.21, 1.16, 1.115, 1.085)]
        _band(K, "binding_r", "fb_dark", la, width=0.028, thick=0.004, off=0.0160, only=BODY_BONES + ("neck_01",))
        _band(K, "binding_l", "fb_dark", lb, width=0.028, thick=0.005, off=0.0178)
        K.skirt("peplum", "fb_vest", z_top=1.05, z_bot=0.885, gap_top=0.023, gap_bot=0.052, nv=5, folds=((6, 0.022, 0.2),), calf=0.0)
        _panel(K, "apron", "fb_dark", -0.088, 0.088, 1.045, 0.715, 0.030, 0.064, front=True, flare=0.10, nu=9, nv=9)
        K.sash("belt", "fb_belt", z=1.066, width=0.062, gap=0.034, tails=(0.16, 0.11), side=-1)
    with K.slot("gloves"):
        K.sleeves("wraps", "wrap", x0=0.495, x1=0.740, off=L1, wrinkle=0.0018, waxis="x", wfreq=95.0)
        K.palm_wrap("palm_wraps", "wrap")
        K.vambrace("pad", "fb_pad", x0=0.512, x1=0.668, off=0.0115, cls="leather", top_only=True)
        _arm_rings(K, "pad_strap", "strap", (0.545, 0.632), width=0.017, off=0.0128, thick=0.004, cls="leather")
    with K.slot("pants"):
        _baggy(K, "trousers", "pants", top=1.02, bottom=0.27, thigh=0.027, knee=0.030, calf=0.027, wrinkle=0.0035, wfreq=18.0)
    with K.slot("boots"):
        bt = K.boots("shoes", "shoe", top=0.170, off=0.0125, cls="cloth", toe_h=0.044)
        _soles(K, bt, "sole", "shoe")
        K.leg_band("shin_wraps", "fb_vest", 0.072, 0.385, off=L3, cls="cloth", wrinkle=0.002, wfreq=85.0)
        _leg_cord(K, "tie_a", "strap", 0.150, 0.345, 1.5, off=0.0185, r=0.0045)
        _leg_cord(K, "tie_b", "strap", 0.150, 0.345, -1.5, off=0.0185, r=0.0045, phase=math.pi)


# ---------------------------------------------------------------------------------------------- t3 forge master
def t3(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        o, P = K.circlet("circlet", "bronze", z=1.750, arc=104.0, r=0.0085, lift=0.0068)
        mid = P[len(P) // 2]
        ends = gjoin(*[gxf(g_sphere(0.0105, 6, 4), T(*P[i])) for i in (0, len(P) - 1)])
        plate = gxf(g_prism([(0.0, 0.034), (0.011, 0.020), (0.024, 0.016), (0.015, 0.0), (0.019, -0.012), (0.0, -0.022),
                             (-0.019, -0.012), (-0.015, 0.0), (-0.024, 0.016), (-0.011, 0.020)], 0.005), T(mid[0], mid[1] - 0.005, mid[2] + 0.004) @ R('X', -8))
        add("circlet_plate", gjoin(ends, plate), "metal", "bronze", bone="Head", sharp=40)
        add("circlet_gem", gxf(g_gem(0.0075, 0.014, 6, 0.5), frame((mid[0], mid[1] - 0.008, mid[2] + 0.005), (0, -1, 0.15))), "emis", "ember",
            bone="Head", sharp=30)
    with K.slot("chest"):
        rm = _robe_mask(K)
        robe = K.shell("robe", _robe_mask(K, margin=0.05), "fb_robe", off=0.016, smooth=5, hem=(1.0, 1.07, 0.0175), wrinkle=0.002, wfreq=20.0)
        # clean diagonal edge (right shoulder -> left flank, front and back alike) and a straight cap sleeve
        _cut(robe, (-0.08, 0, 1.52), (1.25, 0, 1.0), sel=lambda p: p.x > -0.13, drop=lambda p: p.x > -0.092 and p.z > 1.52 + 1.25 * (-p.x - 0.08))
        _cut(robe, (-0.266, 0, 0), (-1, 0, 0), sel=lambda p: p.x < -0.20 and p.z > 1.30, drop=lambda p: p.x < -0.266 and p.z > 1.30)
        _band(K, "robe_edge", "fb_brocade", _robe_edge(K), width=0.042, thick=0.006, off=0.0185, closed=True,
              only=BODY_BONES + ("neck_01",))
        K.skirt("robe_skirt", "fb_robe_hem", z_top=1.05, z_bot=0.665, gap_top=0.026, gap_bot=0.066, nv=10, folds=((5, 0.035, 0.15), (9, 0.012, 0.6)),
                calf=0.3, u0=0.75 + 0.075, u1=1.75 - 0.075, close=False)
        # sash + clasp
        st = K.stations([1.012, 1.078, 1.150], [0.031, 0.026, 0.025])

        def sash_mod(U, V, x, y, z):
            return x, y, z + 0.004 * np.sin(TAU * 3 * U + 9 * V) + 0.012 * (V - 0.5) * np.sin(TAU * (U - 0.1))
        sash = add("sash", g_stations(st, nu=30, nv=6, p=2.3, mod=sash_mod), "cloth", "fb_sash", solidify=0.009, sharp=180, double_sided=True)
        w_transfer(sash, c.ref, smooth=2, only=["pelvis", "spine_01", "spine_02"])
        yf = st[1][2] - st[1][4] - 0.009
        Mc = T(0.0, yf - 0.001, 1.080) @ R('X', 90)
        cl = add("clasp", gxf(gjoin(g_lathe([(0.0, 0.010), (0.022, 0.009), (0.030, 0.004), (0.047, 0.006), (0.052, 0.0), (0.052, -0.004)], n=12, sx=1.22),
                                    g_lathe([(0.040, 0.004), (0.043, 0.010), (0.046, 0.004)], n=12, sx=1.22)), Mc), "metal", "bronze", sharp=40)
        gm = add("clasp_gem", gxf(g_gem(0.009, 0.015, 6, 0.5), T(0.0, yf - 0.010, 1.080) @ R('X', 90)), "emis", "ember", sharp=30)
        for q in (cl, gm):
            w_func(q, lambda co: {"spine_01": 0.6, "spine_02": 0.4})
        # bronze guard strapped on the bare left shoulder, over a leather pad
        mp = (B.x > 0.118) & (B.x < 0.305) & (B.w("clavicle", "upperarm", "spine_03") > 0.5) & (B.z > 1.385) & (B.w("lowerarm") < 0.2)
        K.shell("guard_pad", mp, "strap", "leather", off=0.0085, smooth=3)
        K.pauldron("guard", "bronze", size=_psize(K, 0.119), trim="blackiron", tilt=15.0, sides="l", th_out=94.0, th_in=44.0)
        strap = [(0.175, 0.03, 1.56), (0.165, -0.085, 1.485), (0.085, -0.2, 1.405), (-0.03, -0.2, 1.335), (-0.14, -0.2, 1.275), (-0.225, 0.03, 1.255),
                 (-0.13, 0.25, 1.295), (0.02, 0.25, 1.375), (0.135, 0.16, 1.465)]
        _band(K, "guard_strap", "strap", strap, width=0.030, thick=0.005, off=0.0215, cls="leather", closed=True, cover=rm | mp, bare=0.0035)
        # short string of iron beads
        bp = []
        for t in np.linspace(0, TAU, 60, endpoint=False):
            k = ((1 - math.cos(t)) / 2) ** 1.5
            bp.append((0.112 * math.sin(t), 0.035 + 0.090 * math.cos(t) - 0.03 * k, 1.522 - 0.118 * k))
        Q, N = _path(K, bp, 0.0300, closed=True, step=0.012, cover=rm, bare=0.0125, smooth=3)
        BE = resample(list(Q) + [Q[0]], 0.0235)[:-1]
        beads = add("beads", g_beads(BE, 0.0108, 6, 4), "metal", "iron", sharp=60)
        w_transfer(beads, c.ref, smooth=0, only=["spine_03", "neck_01", "clavicle_l", "clavicle_r"])
        low = BE[int(np.argmin(BE[:, 2]))]
        props("bead_tag", T(low[0], low[1] - 0.003, low[2] - 0.016), [
            (gxf(g_sphere(0.015, 7, 4), T(0, 0, 0)), "metal", "bronze", dict(sharp=60)),
            (gxf(g_prism([(-0.011, 0.0), (0.011, 0.0), (0.014, -0.028), (0.0, -0.042), (-0.014, -0.028)], 0.006), T(0, -0.002, -0.014)), "metal", "bronze",
             dict(bevel=0.0015, sharp=40))], "spine_03")
    with K.slot("gloves"):
        K.sleeves("bracers", "strap", x0=0.495, x1=0.700, off=0.0120, cls="leather")
        _arm_rings(K, "bracer_band", "bronze", (0.530, 0.592, 0.652), width=0.018, off=0.0150, thick=0.004)
        K.palm_wrap("palm_wraps", "char")
        mh = (B.w("hand") > 0.55) & (np.abs(B.x) > 0.752) & (np.abs(B.x) < 0.842) & (B.nrm[:, 2] > 0.35)
        K.shell("hand_plate", mh, "bronze", "metal", off=0.0068, smooth=1)
    with K.slot("pants"):
        _baggy(K, "trousers", "fb_fine", top=1.02, bottom=0.27, thigh=0.018, knee=0.005, calf=0.016, wrinkle=0.003, wfreq=18.0)
        kp = K.leg_band("knee_pads", "fb_pad", 0.435, 0.632, off=L2 + 0.0170, cls="leather")
        _cut(kp, (0, 0, 0.602), (0, 0, 1), drop=lambda p: p.z > 0.602)
        _cut(kp, (0, 0, 0.466), (0, 0, 1), drop=lambda p: p.z < 0.466)
        K.rivets("knee_studs", "bronze", [(s * (0.089 + dx), -0.2, z) for s in (1, -1) for dx, z in ((-0.03, 0.565), (0.03, 0.565), (0.0, 0.50))], r=0.008)
        _panel(K, "apron", "fb_apron", -0.074, 0.074, 1.012, 0.655, 0.0125, 0.040, front=True, z_full=0.87, flare=0.05, point=0.05, nu=9, nv=10)
    with K.slot("boots"):
        bt = K.boots("shoes", "shoe", top=0.170, off=0.0125, cls="cloth", toe_h=0.044)
        _soles(K, bt, "sole", "shoe")
        K.leg_band("shin_wraps", "char", 0.072, 0.385, off=L3, cls="cloth", wrinkle=0.0018, wfreq=85.0)
        gr = K.leg_plate("shin_guard", "blackiron", bone="calf", z0=0.155, z1=0.435, off=0.0235, y_max=0.040)
        bands = gjoin(_leg_strip(K, 0.185, 0.207, 0.585, 0.915, 0.0262, nu=9, nv=2), _leg_strip(K, 0.375, 0.397, 0.585, 0.915, 0.0262, nu=9, nv=2))
        bo = add("shin_band_l", bands, "metal", "bronze", bone="calf_l", sharp=50, solidify=0.004)
        mirrored(bo)
        K.limb_straps("shin_straps", "strap", "calf", (0.196, 0.386), width=0.016, off=L3 + 0.0015, thick=0.004)
