"""Spirit-beast helpers of the town / forms batch (form_bear, form_wolf): beast proportions through the mesh only,
fur shell, claws, tufts, muzzles, glowing markings. Builds on kit_twn; the shared kit modules stay untouched."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_twn import *

FINGERS = ("index", "middle", "ring", "pinky", "thumb")


def finger_spec(k, thumb=None):
    d = {}
    for s in ("l", "r"):
        for f in FINGERS:
            for i in (1, 2, 3):
                d["%s_0%d_%s" % (f, i, s)] = (thumb if (thumb and f == "thumb") else k)
    return d


def beast_reshape(c, spec=None, torso=None, cy=0.02, hand_len=1.0, extra=None):
    """Beast proportions through the mesh only (bones untouched): radial bulk round the bones (spec {bone: factor}),
    trunk scale by height (torso [(z, kx, ky)]), paws lengthened along the arm from the wrist (hand_len), then
    extra(co, B) -> co for anything else (hunch, hocks ...)."""
    def fn(co, B):
        out = co.copy()
        if spec:
            for name, k in spec.items():
                b = mk.ARM.data.bones[name]
                h = np.array(b.head_local)
                a = np.array(b.tail_local) - h
                a /= np.linalg.norm(a)
                w = B.W[:, B.col[name]]
                rel = co - h
                radial = rel - (rel @ a)[:, None] * a
                out += radial * ((k - 1.0) * w)[:, None]
        if torso:
            w = np.clip(B.w("pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "clavicle"), 0, 1)
            zs = [p[0] for p in torso]
            kx = np.interp(co[:, 2], zs, [p[1] for p in torso])
            ky = np.interp(co[:, 2], zs, [p[2] for p in torso])
            out[:, 0] = out[:, 0] * (1 + (kx - 1) * w)
            out[:, 1] = cy + (out[:, 1] - cy) * (1 + (ky - 1) * w)
        if hand_len != 1.0:
            wh = np.clip(B.w(*BONES_HAND), 0, 1)
            for s, sg in (("l", 1.0), ("r", -1.0)):
                wx = float(mk.ARM.data.bones["hand_" + s].head_local[0])
                side = (co[:, 0] * sg > 0)
                out[side, 0] = out[side, 0] + (co[side, 0] - wx) * (hand_len - 1.0) * wh[side]
        if extra is not None:
            out = extra(out, B)
        return out
    reshape_body(c, fn)


def claw_g(base, direction, curl, length=0.06, radius=0.012, seg=4, n=5):
    """curved claw: starts at base, grows along direction, bends towards curl"""
    d = np.asarray(direction, dtype=float)
    d /= np.linalg.norm(d)
    cu = np.asarray(curl, dtype=float)
    cu /= np.linalg.norm(cu)
    path, rad = [], []
    for i in range(seg + 1):
        t = i / seg
        path.append(np.asarray(base, float) + d * length * t + cu * length * 0.45 * t * t)
        rad.append(radius * (1 - t) ** 0.8 + 0.0008)
    return g_tube(path, rad, n=n, caps=True, up=tuple(cu))


def hand_claws(c, src="claw", length=0.06, radius=0.012, back=0.012, cls="bone", thumb_scale=0.85, lift=0.004):
    """claws on every finger tip (tips measured on the reshaped body: farthest vertex of each last phalanx along its
    bone), rigid to the last finger bones; left hand built, right mirrored"""
    B = c.B
    made = []
    for f in FINGERS:
        b = mk.ARM.data.bones["%s_03_l" % f]
        h, t = np.array(b.head_local), np.array(b.tail_local)
        d = (t - h) / np.linalg.norm(t - h)
        m = B.W[:, B.col["%s_03_l" % f]] > 0.5
        tip = t
        if m.any():
            s = (B.co[m] - h) @ d
            tip = h + d * float(s.max())
        k = thumb_scale if f == "thumb" else 1.0
        g = claw_g(tip - d * back + np.array((0, 0, lift)), d + np.array((0, 0, 0.10)), (0, 0, -1), length * k, radius * k)
        o = add("claw_%s_l" % f, g, cls, src, bone="%s_03_l" % f, sharp=50)
        made.append(o)
        made.append(mirrored(o))
    return made


def foot_claws(c, n=4, src="claw", length=0.055, radius=0.013, cls="bone", z=0.028, spread=0.085):
    """toe claws along the front of the (reshaped) left foot, rigid to ball_l, mirrored"""
    B = c.B
    m = (B.w("ball_l", "foot_l") > 0.5) & (B.z < 0.10)
    y0 = float(B.y[m].min())
    xs = B.x[m & (B.y < y0 + 0.05)]
    cx = float((xs.min() + xs.max()) / 2)
    made = []
    for i in range(n):
        dx = spread * (i / (n - 1) - 0.5)
        g = claw_g((cx + dx, y0 + 0.012 + 0.012 * abs(dx) / (spread / 2), z), (dx * 1.4, -1.0, 0.18), (0, 0, -1), length, radius)
        o = add("claw_foot%d_l" % i, g, cls, src, bone="ball_l", sharp=50)
        made.append(o)
        made.append(mirrored(o))
    return made


def scatter_tufts(src_obj, n, mat, name, length=(0.05, 0.09), radius=(0.016, 0.028), seed=1, gravity=0.55, mask=None, flow=None,
                  cls="cloth", sink=0.35, jitter=0.2, sides=4):
    """Scatter cone-shaped fur clumps over a (skinned) surface; each clump takes the weights of the face it grows on.
    mask(centers, normals) -> bool array, flow(p) -> direction the fur lies along."""
    apply_mods(src_obj)
    me = src_obj.data
    W, names = w_get(src_obj)
    rng = np.random.RandomState(seed)
    cen = np.array([tuple(p.center) for p in me.polygons])
    nrm = np.array([tuple(p.normal) for p in me.polygons])
    area = np.array([p.area for p in me.polygons])
    ok = np.ones(len(cen), dtype=bool) if mask is None else mask(cen, nrm)
    idx = np.nonzero(ok)[0]
    if len(idx) == 0:
        return None
    pick = rng.choice(idx, size=n, p=area[idx] / area[idx].sum())
    V, F, rows = [], [], []
    for fi in pick:
        p = cen[fi]
        f = np.asarray((flow(p) if flow else (0.0, 0.0, -1.0)), dtype=float)
        # lay the lock along the surface (flow projected on the tangent plane) and lift it a little, so the pelt reads
        # as overlapping shaggy locks instead of spikes
        ft = f - nrm[fi] * float(np.dot(f, nrm[fi]))
        if np.linalg.norm(ft) < 0.2:
            ft = np.cross(nrm[fi], (1.0, 0.0, 0.0))
        ft /= max(np.linalg.norm(ft), 1e-9)
        d = nrm[fi] * (1 - gravity) + ft * gravity + (rng.rand(3) - 0.5) * jitter
        d /= np.linalg.norm(d)
        L = rng.uniform(*length)
        r = rng.uniform(*radius)
        b = p - d * L * sink
        tip = p + d * L * (1 - sink)
        u = np.cross(d, (0.0, 0.0, 1.0))
        if np.linalg.norm(u) < 1e-3:
            u = np.cross(d, (1.0, 0.0, 0.0))
        u /= np.linalg.norm(u)
        v = np.cross(d, u)
        o = len(V)
        for k in range(sides):
            a = TAU * k / sides
            V.append(b + (u * math.cos(a) + v * math.sin(a)) * r)
        V.append(tip)
        for k in range(sides):
            F.append((o + k, o + (k + 1) % sides, o + sides))
        vid = me.polygons[fi].vertices[0]
        rows += [W[vid]] * (sides + 1)
    o = add(name, geo(np.array(V), F), cls, mat, sharp=180)
    set_weights(o, np.array(rows), names, 4)
    return o


def beast_flow(p):
    """fur direction in the T-pose rest: down on trunk / legs, towards the hand on the arms"""
    if abs(p[0]) > 0.26 and p[2] > 1.22:
        return np.array((1.0 if p[0] > 0 else -1.0, 0.0, -0.3))
    return np.array((0.0, 0.25, -1.0))


def muzzle_g(origin, direction, stations, nu=14, nv=7, p=2.2, cap=True):
    """loft along `direction`; stations [(dist, half width, depth below the axis, depth above the axis)]"""
    st = [(d, 0.0, 0.0, rx, ru, rd) for (d, rx, rd, ru) in stations]
    g = g_stations(st, nu=nu, nv=nv, p=p, cap1=cap)
    return gxf(g, frame(origin, direction, (0, 0, -1)))


def fur_blob(name, center, radii, src, wts, noise=0.012, scale=24.0, seed=1, nu=12, nv=8, rot=None, e=0.9):
    g = g_pillow(radii[0], radii[1], radii[2], e=e, nu=nu, nv=nv)
    M = T(*center) if rot is None else T(*center) @ rot
    o = add(name, gxf(g, M), "cloth", src, sharp=180)
    weld(o, 0.002)
    if noise:
        displace_noise(o, noise, scale, seed)
    if isinstance(wts, str):
        w_rigid(o, wts)
    else:
        w_func(o, lambda co: dict(wts))
    return o


def spirit_line(c, host, name, pts, src="spirit", width=0.018, thick=0.005, offset=0.007, only=None, step=0.03):
    """glowing marking lying on the fur: flat ribbon projected on `host`, weights copied from the body"""
    o, _, _ = ribbon(c, pts, src, width=width, thick=thick, offset=offset, name=name, cls="emis", closed=False, step=step, host=host, only=only,
                     wsmooth=0, sharp=180)
    return o


def spirit_mark(host, name, outline, p, ray, src="spirit", up=(0, 0, 1), depth=0.006, lift=0.004, bone=None):
    """glowing glyph glued on the fur where a ray from p along `ray` hits `host`"""
    return decal(name, outline, host, p, "emis", src, ray=ray, up=up, depth=depth, lift=lift, bone=bone)
