"""Private helpers of the town / lampknight / spirit-form batch (hero_lampknight, npc_elder, npc_smith, npc_merchant,
npc_occultist, form_bear, form_wolf). The shared kit modules stay untouched; this module only adds to them."""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_mel import *


# ---------------------------------------------------------------------------------------------- cloth with real folds
def fold_sum(U, hem, folds, hem_pow=1.2, top=0.12):
    """sum of irregular pleat waves across u; folds = [(count, amplitude m, phase)], growing towards the hem"""
    d = 0.0
    for (n, amp, ph) in folds:
        d = d + amp * np.sin(TAU * (n * U + ph) + 0.7 * np.sin(TAU * (U * 1.31 + ph * 2.3)))
    return d * (top + (1 - top) * hem ** hem_pow)


def drape(name, x0, x1, z_top, z_bot, yfn, cls, src, nu=13, nv=10, thick=0.005, out=-1.0, folds=((3.0, 0.012, 0.3),),
          hem_pow=1.2, top=0.12, flare=0.0, point=0.0, zmod=None, xgather=0.0, **kw):
    """Hanging cloth panel with fold geometry (front panel: out=-1, back panel: out=+1). yfn(x, z) -> y of the surface
    it lies on. luv v = 0 at the hem (so mats.cloth bands sit at the hem)."""
    xm, hw = (x0 + x1) / 2, (x1 - x0) / 2

    def fn(U, V):
        hem = 1.0 - V
        z = z_bot + (z_top - z_bot) * V
        t = U * 2 - 1
        x = xm + t * hw * (1 + flare * hem)
        z = z - point * (1 - np.abs(t)) * hem ** 3
        if zmod is not None:
            z = zmod(U, V, z)
        d = fold_sum(U, hem, folds, hem_pow, top)
        if xgather:
            x = x + xgather * hem * np.cos(TAU * folds[0][0] * U + TAU * folds[0][2]) * hw / max(folds[0][0], 1.0)
        y = yfn(x, z) + out * (d + np.abs(d) * 0.35)
        return np.stack([x, y, z], axis=-1)
    return add(name, g_grid(fn, nu, nv, close_u=False), cls, src, solidify=thick, sharp=180, double_sided=True, **kw)


def fold_mod(folds, cy=0.02, hem_pow=1.3, top=0.10, sag=0.0, extra=None):
    """mod for g_stations / robe lofts (hem = V 0): radial pleats of irregular width and depth, plus an optional hem sag
    between the pleats. folds = [(count, relative amplitude, phase)]."""
    def mod(U, V, x, y, z):
        hem = 1.0 - V
        f = 0.0
        for (n, amp, ph) in folds:
            f = f + amp * np.sin(TAU * (n * U + ph) + 0.8 * np.sin(TAU * (U * 2.0 + ph * 1.7)))
        k = (top + (1 - top) * hem ** hem_pow)
        s = 1.0 + f * k
        x = x * s
        y = cy + (y - cy) * s
        if sag:
            z = z + sag * f / max(abs(folds[0][1]), 1e-6) * hem ** 4
        if extra is not None:
            x, y, z = extra(U, V, x, y, z)
        return x, y, z
    return mod


def wrinkle_shell(o, amp=0.004, freq=22.0, axis="z", seed=1, mask=None):
    """Mid-frequency wrinkle geometry on a fitted garment shell: ridges running across the given axis direction
    (sleeve / trouser-leg compression folds), irregular, displaced along the vertex normals."""
    apply_mods(o)
    co = get_co(o)
    me = o.data
    n = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get('normal', n)
    n = n.reshape(-1, 3)
    rng = np.random.RandomState(seed)
    ph = rng.rand(4) * TAU
    k = {"x": 0, "y": 1, "z": 2}[axis]
    oth = [i for i in range(3) if i != k]
    s = co[:, k] * freq
    w = np.sin(s + ph[0] + 1.3 * np.sin(co[:, oth[0]] * freq * 0.55 + ph[1]) + 1.1 * np.sin(co[:, oth[1]] * freq * 0.47 + ph[2]))
    w = w + 0.5 * np.sin(s * 2.3 + ph[3] + co[:, oth[0]] * freq * 0.9)
    d = amp * w
    if mask is not None:
        d = d * mask(co)
    set_co(o, co + n * d[:, None])
    return o


# ---------------------------------------------------------------------------------------------- plate
def arm_lame(x0, x1, r0, r1, arc=0.62, center=0.5, n=10, axis_y=0.066, axis_z=1.435, side=1):
    """Curved lame round the T-pose arm (axis along X): partial tube from x0 to x1 with radii r0 -> r1.
    center = where the arc is centred (0.5 = top, 0.25 = back, 0.75 = front)."""
    g = g_lathe([(r0, 0.0), (r1, abs(x1 - x0))], n=n, arc=arc, start=center - arc / 2)
    return gxf(g, frame((x0, axis_y, axis_z), (1 if x1 > x0 else -1, 0, 0), (0, 1, 0)))


def leg_lame(z0, z1, r0, r1, cx=0.086, cy=0.0, arc=0.56, center=0.75, n=10, sy=1.0):
    """Curved lame round the leg (axis Z), centred on the front (0.75) by default."""
    g = g_lathe([(r0, z0), (r1, z1)], n=n, arc=arc, start=center - arc / 2, sy=sy)
    return gxf(g, T(cx, cy, 0))


def g_barbute(cy=-0.012, rx=0.108, ryf=0.127, ryb=0.124, z_rim=1.555, tail=0.035, zc=1.70, top=0.145, brow=1.728,
              chin_deg=13.0, eye_deg=46.0, z_eye0=1.615, z_eye1=1.685, nu=30, nv=16, flare=0.006, comb=0.004):
    """Barbute skull with a Y-shaped face opening (narrow slot at the mouth widening to the eyes, closed above the
    brow). Rings are C-shaped below the brow. Returns the open-u grid geometry (weld it)."""
    zt = zc + top

    def fn(U, V):
        zn = z_rim + (zt - z_rim) * V
        op = np.radians(chin_deg + (eye_deg - chin_deg) * sstep(zn, z_eye0, z_eye1)) * (1 - sstep(zn, brow - 0.004, brow + 0.012))
        a = op + (TAU - 2 * op) * U
        ca, sa = np.cos(a), np.sin(a)
        back = (1 - ca) / 2
        z = zn - tail * back ** 2 * (1 - V) ** 2
        k = np.where(z > zc, np.sqrt(np.clip(1 - ((z - zc) / top) ** 2, 0, 1)), 1.0)
        k = k + flare / rx * np.clip((zc - 0.08 - z) / 0.07, 0, 1)
        k = k + comb / rx * np.exp(-(sa * rx / 0.014) ** 2) * sstep(zn, brow - 0.01, brow + 0.03) * np.clip(k, 0, 1) ** 0.5
        ry = np.where(ca > 0, ryf, ryb)
        return np.stack([rx * k * sa, cy - ry * k * ca, z], axis=-1)
    return g_grid(fn, nu, nv, close_u=False, cap1=False)


def grid_rows(g, nu):
    v = np.asarray(g["v"])
    return v[:(len(v) // nu) * nu].reshape(-1, nu, 3)


# ---------------------------------------------------------------------------------------------- shield
def shield_frame(side="l", back=0.07, out=0.072, up=0.0):
    """Shield-local -> armature rest space for a shield strapped to the forearm and gripped in the fist.
    Shield local: +Y = face normal (out of the back of the hand), +Z = up the shield (thumb side of the fist),
    +X = along the forearm (elbow -> knuckles for the left arm). Origin: `back` behind the wrist on the forearm axis,
    lifted `out` off the arm. The clips Idle_Shield_Loop / Shield_Dash / Shield_OneShot hold the forearm level across
    the chest with the back of the hand forward and the thumb up, so the shield then stands upright facing forward."""
    o, along, dorsal, thumb = hand_axes(side)
    M = np.eye(4)
    x = np.cross(dorsal, thumb)
    M[:3, 0], M[:3, 1], M[:3, 2] = x, dorsal, thumb
    M[:3, 3] = o - along * back + dorsal * out + thumb * up
    return M


def heater_w(W, straight=0.36, p=2.0, q=0.62, tip=0.012):
    def w(V):
        t = 1.0 - np.asarray(V, float)
        s = np.clip((t - straight) / (1 - straight), 0, 1)
        return np.maximum(W * (1 - s ** p) ** q, tip)
    return w


def kite_w(W, head=0.24, p=1.25, tip=0.012, top=0.10):
    def w(V):
        t = 1.0 - np.asarray(V, float)
        a = np.clip((head - t) / head, 0, 1)
        up = np.sqrt(np.clip(1 - (a * (1 - top)) ** 2, 0, 1))
        s = np.clip((t - head) / (1 - head), 0, 1)
        return np.maximum(W * np.where(t < head, up, 1 - s ** p), tip)
    return w


def shield_y(W, depth=0.045, H=0.6, zc=0.0, vdepth=0.0):
    """curved shield face: y(x, z), convex towards +Y"""
    def f(x, z):
        return -depth * (np.asarray(x) / W) ** 2 - vdepth * ((np.asarray(z) - zc) / (H / 2)) ** 2
    return f

def g_shield_board(wfn, yfn, z_bot, z_top, nu=9, nv=12):
    """board surface with +Y face normals (use add(..., recalc=False, solidify=t, solid_offset=-1))"""
    def fn(U, V):
        z = z_bot + (z_top - z_bot) * V
        x = -(2 * U - 1) * wfn(V)
        return np.stack([x, yfn(x, z), z], axis=-1)
    return g_grid(fn, nu, nv, close_u=False)


def face_strip(x0, x1, z0, z1, yfn, n=8, lift=0.0035):
    """band lying on the curved shield face (+Y normals; add with recalc=False, solidify=t, solid_offset=-1)"""
    def fn(U, V):
        x = x1 + (x0 - x1) * U
        z = z0 + (z1 - z0) * V
        return np.stack([x, yfn(x, z) + lift, z], axis=-1)
    return g_grid(fn, n + 1, 2, close_u=False)


def shield_outline(wfn, yfn, z_bot, z_top, nv=12, dy=-0.006, inset=0.0):
    """closed outline path of the board (for rim bands), going up the +X side and down the -X side"""
    vs = np.linspace(0, 1, nv)
    zs = z_bot + (z_top - z_bot) * vs
    ws = np.maximum(wfn(vs) - inset, 0.004)
    R_ = [(w, float(yfn(w, z)) + dy, z) for w, z in zip(ws, zs)]
    L_ = [(-w, float(yfn(-w, z)) + dy, z) for w, z in zip(ws[::-1], zs[::-1])]
    return R_ + L_


def conform(g, yfn, lift=0.0):
    """bend a flat piece (XZ plane, thickness along Y) onto the shield face"""
    out = dict(g)
    v = g["v"].copy()
    v[:, 1] = v[:, 1] + yfn(v[:, 0], v[:, 2]) + lift
    out["v"] = v
    return out


def ydome(profile, n=12, y0=0.0, z0=0.0, x0=0.0, sx=1.0, sz=1.0):
    """lathe whose axis points along +Y (boss / rivet / disc on a shield face); profile [(r, height)]"""
    g = g_lathe(profile, n=n, sx=sx, sy=sz)
    return gxf(g, T(x0, y0, z0) @ R('X', -90))


def shield_back(yfn, back, out, up, arm_r=0.050, thick=0.012, src="strap", pad_src=None):
    """enarmes: forearm loop, hand bar on two brackets and an arm pad behind the board (shield local space)"""
    parts = []
    ring = [(-0.085, -out + (arm_r + 0.004) * math.cos(a), -up + (arm_r + 0.004) * math.sin(a)) for a in np.linspace(0, TAU, 10, endpoint=False)]
    parts.append((g_tube(ring, 1.0, n=6, closed=True, sx=0.017, sy=0.0045, up=(1, 0, 0)), "leather", src, dict(sharp=60)))
    gx, gy, gz = back + 0.103, -out - 0.031, -up + 0.004
    parts.append((g_tube([(gx, gy, gz - 0.075), (gx, gy, gz + 0.075)], 0.0105, n=6), "leather", src, dict(sharp=60)))
    yb = float(yfn(gx, gz)) - thick
    for s in (-1, 1):
        parts.append((g_box(0.020, abs(gy - yb) + 0.012, 0.016, (gx, (gy + yb) / 2 - 0.004, gz + s * 0.072)), "leather", src, dict(sharp=40)))
    y1 = min(float(yfn(-0.125, -up)), float(yfn(0.065, -up))) - thick - 0.002
    y0 = -out + arm_r - 0.004
    if y1 - y0 > 0.006:
        parts.append((g_box(0.19, y1 - y0, 0.10, (-0.03, (y0 + y1) / 2, -up)), "leather", pad_src or src, dict(bevel=0.006, sharp=50)))
    return parts


# ---------------------------------------------------------------------------------------------- lantern
def lantern_parts(h=0.15, r=0.045, n=6, frame_src="iron", flame_src="flame", wax_cls="cloth", wax_src="wax", ring=0.02,
                  flame=1.0, bars=True, roof=1.0):
    """Caged lantern in lantern-local space: hang point at the origin, body hanging towards -Z.
    Returns parts [(geo, cls, src, kw)]: ring, vented roof, corner posts, hoops, base, candle stub, flame (emis)."""
    parts = []
    if ring:
        parts.append((gxf(g_ring(ring, ring * 0.2, n=10, m=4), T(0, 0, -ring) @ R('X', 90)), "metal", frame_src, dict(sharp=60)))
    zc = -2 * ring if ring else 0.0
    rh = 0.05 * roof * (r / 0.045)
    parts.append((g_lathe([(0.0, zc + 0.004), (r * 0.20, zc), (r * 0.26, zc - rh * 0.30), (r * 0.62, zc - rh * 0.55), (r * 1.20, zc - rh),
                           (r * 1.20, zc - rh - 0.006), (0.0, zc - rh - 0.006)], n=n), "metal", frame_src, dict(sharp=40)))
    zt = zc - rh - 0.006
    zb = zt - h
    if bars:
        posts = []
        for i in range(n):
            a = TAU * (i + 0.5) / n
            x, y = r * math.cos(a), r * math.sin(a)
            posts.append(g_tube([(x, y, zt + 0.002), (x, y, zb - 0.002)], 0.0042 * (r / 0.045) ** 0.5, n=4))
        parts.append((gjoin(*posts), "metal", frame_src, dict(sharp=60)))
        hoop = [(r * 1.02 * math.cos(TAU * (i + 0.5) / n), r * 1.02 * math.sin(TAU * (i + 0.5) / n), zb + h * 0.5) for i in range(n)]
        parts.append((g_tube(hoop, 0.0034, n=4, closed=True), "metal", frame_src, dict(sharp=60)))
    parts.append((g_lathe([(0.0, zb), (r * 1.16, zb), (r * 1.16, zb - 0.010), (r * 0.70, zb - 0.020), (0.0, zb - 0.020)], n=n), "metal", frame_src,
                  dict(sharp=40)))
    parts.append((g_cyl(r * 0.30, zb, zb + h * 0.20, 6), wax_cls, wax_src, dict(sharp=60)))
    f0 = zb + h * 0.20
    fr, fh = r * 0.50 * flame, h * 0.62 * flame
    parts.append((g_lathe([(0.0, f0 - 0.002), (fr * 0.75, f0 + fh * 0.16), (fr, f0 + fh * 0.36), (fr * 0.62, f0 + fh * 0.68), (0.0, f0 + fh)], n=6),
                  "emis", flame_src, dict(sharp=180)))
    return parts


def rest_dir_for(arm, acts, clip, t, bone, world_dir):
    """direction in armature REST space that points along `world_dir` when `bone` is posed at fraction t of `clip`
    (e.g. world down for something that must hang plumb in an idle clip). Leaves the armature in the rest pose."""
    set_pose(arm, acts[clip], t)
    pb = arm.pose.bones[bone]
    Mp = np.array([list(r) for r in pb.matrix])
    D = Mp @ np.linalg.inv(bone_frame(bone))
    d = D[:3, :3].T @ np.array(world_dir, float)
    set_pose(arm, None)
    return d / np.linalg.norm(d)


def tri_report(top=16):
    """log the heaviest parts of the character under construction (modifiers evaluated)"""
    dg = bpy.context.evaluated_depsgraph_get()
    rows_ = []
    for o in mk.PARTS:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        rows_.append((int(sum(len(p.vertices) - 2 for p in me.polygons)), o.name, o["owner"]))
        ev.to_mesh_clear()
    rows_.sort(reverse=True)
    log("TRIS total %d : %s" % (sum(r[0] for r in rows_), ", ".join("%s=%d" % (r[1], r[0]) for r in rows_[:top])))


# ---------------------------------------------------------------------------------------------- shared motifs / props
def lamp_sigil(host, zc, ray, name, s=1.0, cls="cloth", src="emblem", x=0.0, depth=0.004, bone=None):
    """Beacon-keepers' glyph (lantern): roof chevron over a flame lozenge on a base bar, with two ray ticks.
    Glued on `host` where rays along `ray` from in front of / behind the body hit it."""
    roof = [(-0.034 * s, -0.016 * s), (0.0, 0.018 * s), (0.034 * s, -0.016 * s), (0.024 * s, -0.016 * s), (0.0, 0.006 * s), (-0.024 * s, -0.016 * s)]
    y0 = -0.6 if ray[1] > 0 else 0.6
    kw = dict(ray=ray, depth=depth, lift=0.002, bone=bone)
    out = [decal(name + "_roof", roof, host, (x, y0, zc + 0.052 * s), cls, src, **kw),
           decal(name + "_flame", rhomb(0.017 * s, 0.032 * s), host, (x, y0, zc), cls, src, **kw),
           decal(name + "_base", bar(0.030 * s, 0.006 * s), host, (x, y0, zc - 0.048 * s), cls, src, **kw)]
    for sx in (-1, 1):
        out.append(decal(name + "_ray%d" % sx, rhomb(0.006 * s, 0.014 * s), host, (x + sx * 0.040 * s, y0, zc), cls, src, **kw))
    return out


def solidify(o, t, offset=1.0):
    m = o.modifiers.new("solid", 'SOLIDIFY')
    m.thickness = t
    m.offset = offset
    m.use_even_offset = True
    return o


def hand_hang_frame(side, down_rest, ring=0.03):
    """frame for something hanging from the closed fist: local -Z = `down_rest` (the rest-space direction that is plumb
    in the clip the prop is made for), local Y = along the hand (so a bail ring in the local XZ plane threads the fist).
    Origin = grip centre lifted by the ring radius, so the top of the ring lies in the finger tunnel."""
    gc = xf(np.array([GRIP_C[side]]), bone_frame("hand_" + side))[0]
    d = np.array(down_rest, float)
    d /= np.linalg.norm(d)
    o, along, dorsal, thumb = hand_axes(side)
    return frame(gc, -d, along)


def cord_ring(z, rx, ry, cy=0.0, n=26, wob=0.004, seed=1):
    rng = np.random.RandomState(seed)
    return [(rx * math.cos(a), cy + ry * math.sin(a), z + wob * math.sin(3 * a + seed) + (rng.rand() - 0.5) * wob * 0.5)
            for a in np.linspace(0, TAU, n, endpoint=False)]


def g_key(length=0.085, r=0.012):
    """simple key in the XZ plane hanging from its bow at the origin (shaft towards -Z)"""
    bow = g_tube([(r * math.cos(a), 0.0, -r + r * math.sin(a)) for a in np.linspace(0, TAU, 8, endpoint=False)], 0.0028, n=4, closed=True)
    shaft = g_box(0.005, 0.004, length - 2 * r, (0, 0, -2 * r - (length - 2 * r) / 2))
    bit = g_box(0.016, 0.004, 0.018, (0.009, 0, -length + 0.012))
    return gjoin(bow, shaft, bit)


def g_bottle(r=0.035, h=0.11, neck=0.012, n=8):
    return g_lathe([(0.0, 0.0), (r * 0.8, 0.0), (r, h * 0.18), (r, h * 0.55), (r * 0.55, h * 0.78), (neck, h * 0.86), (neck, h), (0.0, h)], n=n)


def _sp(v, e):
    return np.sign(v) * np.abs(v) ** e


def g_pillow(rx, ry, rz, e=0.6, nu=12, nv=8, t0=0.0, t1=1.0):
    """superellipsoid (stuffed sack / cushion / pouch); t0..t1 = polar range (0 = top pole, 1 = bottom pole)"""
    def fn(U, V):
        ph = TAU * U
        th = math.pi * (t0 + (t1 - t0) * V)
        st, ct = np.sin(th), np.cos(th)
        return np.stack([rx * _sp(st, e) * _sp(np.cos(ph), e), ry * _sp(st, e) * _sp(np.sin(ph), e), rz * _sp(ct, e)], axis=-1)
    return g_grid(fn, nu, nv, close_u=True)


def g_roll(length, r, n=10, turns=True):
    """rolled cloth / carpet lying along X, centred: outer tube with 'luv' (v = metres along the roll) and recessed
    spiral-like end faces"""
    h = length / 2
    g = tube_w([(-h, 0, 0), (-h * 0.34, 0, 0), (h * 0.34, 0, 0), (h, 0, 0)], [r, r * 1.03, r * 1.03, r], n, caps=False, up=(0, 0, 1))
    ends = []
    for s in (-1, 1):
        prof = [(r, 0.0), (r * 0.72, 0.012), (r * 0.70, 0.004), (r * 0.42, 0.016), (r * 0.40, 0.008), (0.0, 0.018)]
        e = g_lathe(prof, n=n)
        ends.append(gxf(e, T(s * (h - 0.016), 0, 0) @ R('Y', 90 * s)))
    ge = gjoin(*ends)
    ge["luv"] = np.zeros((len(ge["v"]), 2)) + np.array((0.5, 10.0))
    return gjoin(g, ge) if turns else g


def hat_wide(brim=0.275, crown=0.104, height=0.134, n=22, sx=0.94, sy=1.06, wave=0.022, droop=0.030):
    """wide-brimmed travelling hat in hat-local space (origin = centre of the brim plane, +Z up, front = -Y)"""
    prof = [(brim, -droop), (brim * 0.955, -droop * 0.8), (brim * 0.70, -droop * 0.27), (crown * 1.08, 0.0), (crown, 0.012),
            (crown * 0.94, height * 0.45), (crown * 0.83, height * 0.78), (crown * 0.53, height * 0.955), (0.0, height)]
    g = g_lathe(prof, n=n, sx=sx, sy=sy)
    v = g["v"].copy()
    r = np.hypot(v[:, 0], v[:, 1])
    a = np.arctan2(v[:, 1], v[:, 0])
    b = np.clip((r - crown * 1.06) / (brim - crown * 1.06), 0, 1)
    v[:, 2] += wave * np.sin(2 * a + 0.6) * b ** 1.5 + wave * 0.5 * np.sin(5 * a + 1.1) * b ** 2
    top = np.clip((v[:, 2] - height * 0.7) / (height * 0.3), 0, 1) * (r < crown)
    v[:, 2] -= 0.018 * top * np.exp(-(v[:, 0] / 0.03) ** 2)                    # crease along the crown
    g["v"] = v
    return g
