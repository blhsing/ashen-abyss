"""Character-level helpers shared by all hero / NPC build scripts: base body setup, grips, common garments."""
import bpy, bmesh, sys, os, math
import numpy as np
from mathutils import Vector, Matrix
from rigio import *
import mk
from mk import *
import mats
import finalize as fin

FAST = "--fast" in sys.argv


class Ctx:
    pass


def start(kind="male"):
    """New scene with the UAL rig + fitted base body. Returns ctx with:
    arm, B (mk.Body reference: full body, never exported), skin (trimmable copy), eyes, brows, kind"""
    reset_scene()
    arm = import_rig()
    mk.begin(arm)
    mk._WCACHE.clear()
    mats.SRCM.clear()
    mats.SRCINFO.clear()
    fn = {"male": "Superhero_Male_FullBody.gltf", "female": "Superhero_Female_FullBody.gltf"}[kind]
    parts = import_skinned(os.path.join(SRC, "base", fn), arm)
    body = parts["SuperHero_Male" if kind == "male" else "Superhero_Female"]
    c = Ctx()
    c.kind = kind
    c.arm = arm
    c.skin = body
    c.eyes = parts["Eyes"]
    c.brows = parts["Eyebrows"]
    ref = body.copy()
    ref.data = body.data.copy()
    ref.name = "BODY_REF"
    bpy.context.scene.collection.objects.link(ref)
    ref.hide_render = True
    c.ref = ref
    c.B = mk.Body(ref)
    for o in (c.skin, c.eyes, c.brows, ref):
        o.data.materials.clear()
    for im in list(bpy.data.images):
        if im.users == 0 or im.size[0] == 0:
            try:
                bpy.data.images.remove(im)
            except Exception:
                pass
    c.hide = np.zeros(len(c.B.co), dtype=bool)     # accumulated mask of skin verts hidden by clothing
    c.hairs = []
    return c


def reshape_body(c, fn):
    """Change body proportions through the mesh only (bones untouched). fn(co, B) -> new co. Applies to the
    reference body and the skin (same vertex order) and to eyes/brows through nearest-vertex offsets."""
    old = c.B.co.copy()
    new = fn(old.copy(), c.B)
    set_co(c.ref, new)
    set_co(c.skin, new)
    from mathutils import kdtree
    kd = kdtree.KDTree(len(old))
    for i, p in enumerate(old):
        kd.insert(Vector(p), i)
    kd.balance()
    for o in (c.eyes, c.brows):
        if o is None:
            continue
        co = get_co(o)
        for i, p in enumerate(co):
            _, idx, _ = kd.find(Vector(p))
            co[i] = p + (new[idx] - old[idx])
        set_co(o, co)
    c.ref.data.update()
    c.B.refresh()


def add_hair(c, name, tex=None, scale=(1, 1, 1), offset=(0, 0, 0), pivot=(0, -0.02, 1.68)):
    """Quaternius hair / beard mesh (rigid to Head), fitted to the UAL head position."""
    parts = import_skinned(os.path.join(SRC, "hair", name + ".gltf"), c.arm)
    o = list(parts.values())[0]
    if tex:
        o["hair_tex"] = tex
    else:
        o["hair_tex"] = 2 if any(m and "Hair_2" in m.name for m in o.data.materials) else 1
    co = get_co(o)
    pv = np.array(pivot)
    co = (co - pv) * np.array(scale) + pv + np.array(offset)
    set_co(o, co)
    o.data.materials.clear()
    c.hairs.append(o)
    return o


# ---------------------------------------------------------------------------------------------- grips
# finger "tunnel" of the closed fist used by the UAL sword / idle / jog clips, in hand-bone local space
GRIP_C = {"r": (-0.031, 0.103, -0.004), "l": (0.031, 0.103, -0.004)}
GRIP_A = {"r": (-0.04, 0.17, 0.99), "l": (0.04, 0.17, 0.99)}


def grip(side="r", tilt=0.0, roll=0.0, slide=0.0, push=0.0):
    """Weapon-local -> armature rest space for an object held in the fist.
    weapon local axes: +Z up the weapon (comes out of the thumb/index side of the fist), +X towards the knuckles
    (edge / 'forward' of the weapon), +Y = palm normal (flat of a blade). Origin = centre of the grip.
    tilt: degrees about weapon Y... rotates the +Z end towards the wrist (positive) ; roll: spin about Z;
    slide: move along Z; push: move along +X (towards knuckles)."""
    hb = mk.ARM.data.bones["hand_" + side]
    H = np.array([list(r) for r in hb.matrix_local])
    z = np.array(GRIP_A[side], dtype=float)
    z /= np.linalg.norm(z)
    x = np.array((0.0, 1.0, 0.0))
    x = x - z * np.dot(x, z)
    x /= np.linalg.norm(x)
    y = np.cross(z, x)
    L = np.eye(4)
    L[:3, 0], L[:3, 1], L[:3, 2], L[:3, 3] = x, y, z, GRIP_C[side]
    return H @ L @ R('Y', -tilt) @ R('Z', roll) @ T(push, 0, slide)


def bone_frame(name):
    b = mk.ARM.data.bones[name]
    return np.array([list(r) for r in b.matrix_local])


def joint(name):
    return np.array(mk.ARM.data.bones[name].head_local)


# ---------------------------------------------------------------------------------------------- masks
def m_legs(B, lo=-1.0, hi=1.02):
    return (B.w(*BONES_LEG, *BONES_FOOT, "pelvis", "spine_01") > 0.5) & (B.z > lo) & (B.z < hi) & \
        (B.w(*BONES_ARM, *BONES_HAND) < 0.3)


def m_feet(B, top=0.40):
    return (B.w(*BONES_FOOT, "calf") > 0.5) & (B.z < top)


def m_hands(B, cuff=0.70):
    return (B.w(*BONES_HAND) > 0.5) | ((B.w("lowerarm", *BONES_HAND) > 0.5) & (np.abs(B.x) > cuff))


def m_torso(B, lo=0.92, hi=1.50, sleeve=0.72):
    arms = (B.w(*BONES_ARM) > 0.5) & (np.abs(B.x) < sleeve)
    core = (B.w(*BONES_TORSO, "clavicle", "neck_01", "thigh") > 0.5) & (B.z > lo) & (B.z < hi) & (B.w(*BONES_HAND) < 0.3)
    return (arms & (B.w(*BONES_HAND) < 0.3)) | core


def m_arms(B, x0=0.20, x1=0.72):
    return (B.w("upperarm", "lowerarm") > 0.5) & (np.abs(B.x) > x0) & (np.abs(B.x) < x1)


def m_head(B):
    return B.w("Head") > 0.5


def hide(c, mask):
    c.hide |= mask


def finish_skin(c, shrink=1):
    """delete the skin faces hidden by clothing (call once, after all shells are made)"""
    if c.skin is None:
        return
    trim_skin(c.skin, c.B, c.hide, shrink)


def drop_skin(c):
    """character shows no skin at all (full armour / beast)"""
    for o in (c.skin, c.eyes, c.brows):
        if o is not None:
            bpy.data.objects.remove(o, do_unlink=True)
    c.skin = c.eyes = c.brows = None


# ---------------------------------------------------------------------------------------------- garments
def boots(c, src, top=0.40, offset=0.012, cls="leather", smooth=3, name="boots", cuff=0.0, toe_cap=True, **kw):
    B = c.B
    m = m_feet(B, top)
    o = shell(B, m, offset, name, cls, src, smooth=smooth, sharp=180, **kw)
    co = get_co(o)
    if toe_cap:
        # wrap the bare toes in a rounded toe box (ellipsoid), blended in from the instep forward
        for s in (1, -1):
            cen = np.array((s * 0.105, -0.100, 0.036))
            rad = np.array((0.058 + offset, 0.092 + offset, 0.046 + offset))
            side = (co[:, 0] * s > 0) & (co[:, 1] < -0.045) & (co[:, 2] < 0.12)
            d = co[side] - cen
            k = np.sqrt(((d / rad) ** 2).sum(1)) + 1e-9
            target = cen + d / k[:, None]
            w = sstep(-co[side, 1], 0.05, 0.10)[:, None]
            grow = (k < 1.0)[:, None]
            co[side] = np.where(grow, co[side] * (1 - w) + target * w, co[side])
    # flatten the sole
    low = co[:, 2] < 0.012
    co[low, 2] = -0.004
    set_co(o, co)
    if toe_cap:
        e = mesh_edges(o)
        co = get_co(o)
        fore = co[:, 1] < -0.05
        sm = laplacian(co, e, 3, 0.5)
        co[fore] = sm[fore]
        co[co[:, 2] < 0.004, 2] = -0.004
        set_co(o, co)
    hide(c, m & (B.z < top - 0.02))
    if cuff:
        for s in (1, -1):
            st = [(top - 0.05, s * 0.089, 0.045, 0.062 + cuff, 0.062 + cuff, 0.068 + cuff),
                  (top + 0.01, s * 0.089, 0.045, 0.070 + cuff, 0.070 + cuff, 0.074 + cuff)]
            g = g_stations(st, nu=12, nv=3)
            q = add("%s_cuff_%s" % (name, "l" if s > 0 else "r"), g, cls, src, solidify=0.008,
                    bone="calf_l" if s > 0 else "calf_r")
    return o


def gloves(c, src, cuff=0.70, offset=0.004, cls="leather", smooth=0, name="gloves", **kw):
    B = c.B
    m = m_hands(B, cuff)
    o = shell(B, m, offset, name, cls, src, smooth=smooth, sharp=180, **kw)
    hide(c, m & (np.abs(B.x) > cuff + 0.02))
    return o


def trousers(c, src, top=1.02, bottom=0.10, offset=0.012, cls="cloth", smooth=3, name="trousers", **kw):
    B = c.B
    m = m_legs(B, bottom, top)
    o = shell(B, m, offset, name, cls, src, smooth=smooth, sharp=180, **kw)
    hide(c, m & (B.z > bottom + 0.03) & (B.z < top - 0.03))
    return o


def shirt(c, src, bottom=0.92, top=1.50, sleeve=0.72, offset=0.012, cls="cloth", smooth=3, name="shirt", **kw):
    B = c.B
    m = m_torso(B, bottom, top, sleeve)
    o = shell(B, m, offset, name, cls, src, smooth=smooth, sharp=180, **kw)
    hide(c, m & (B.z > bottom + 0.03) & (np.abs(B.x) < sleeve - 0.03) & (B.z < top - 0.02))
    return o


def body_ring(c, z, gap=0.012):
    """(cx, cy, rx, ry_front, ry_back) of the torso at height z, grown by gap"""
    s = c.B.slice(z, 0.02)
    cx, cy, rx, ryf, ryb = s
    return (0.0, cy, rx + gap, ryf + gap, ryb + gap)


def belt(c, z, src, width=0.05, thick=0.010, gap=0.016, cls="leather", name="belt", buckle=None, tilt=0.0,
         rx=None, ry=None, bone=None):
    """Belt hugging the torso at height z. buckle = source material key for a metal buckle (front)."""
    cx, cy, brx, bryf, bryb = body_ring(c, z, gap)
    if rx:
        brx = rx
    if ry:
        bryf = bryb = ry

    def mod(U, V, x, y, zz):
        return x, y, zz + tilt * x
    st = [(z - width / 2, 0, cy, brx, bryf, bryb), (z + width / 2, 0, cy, brx, bryf, bryb)]
    g = g_stations(st, nu=28, nv=2, p=2.4, mod=mod)
    o = add(name, g, cls, src, solidify=thick, solid_offset=1.0, sharp=50)
    if bone:
        w_rigid(o, bone)
    else:
        w_transfer(o, c.ref, smooth=2, only=["pelvis", "spine_01", "spine_02", "spine_03"])
    if buckle:
        gb = g_box(0.06, 0.016, width * 1.25)
        gb = gxf(gb, T(0, cy - bryf - thick - 0.004, z))
        ob = add(name + "_buckle", gb, "metal", buckle, bevel=0.004, sharp=35)
        w_transfer(ob, o, smooth=0)
    return o


def wrap_path(c, pts, offset=0.012):
    """project path points onto the reference body surface and push out by offset"""
    out = []
    for p in pts:
        ok, loc, nrm, fi = c.ref.closest_point_on_mesh(Vector(p))
        out.append(np.array(loc) + np.array(nrm) * offset)
    return np.array(out)


def strap(c, pts, src, width=0.035, thick=0.008, offset=0.014, cls="leather", name="strap", closed=True, n=6,
          smooth=2, only=None):
    """Flat strap following the body through the given guide points (closed loop by default)."""
    P = wrap_path(c, pts, offset)
    # smooth the path a little
    for _ in range(2):
        if closed:
            P = (np.roll(P, 1, 0) + P * 2 + np.roll(P, -1, 0)) / 4
        else:
            P[1:-1] = (P[:-2] + P[1:-1] * 2 + P[2:]) / 4
    g = g_tube(P, 1.0, n=n, closed=closed, sx=thick / 2, sy=width / 2, up=(0, -1, 0.2))
    o = add(name, g, cls, src, sharp=60)
    w_transfer(o, c.ref, smooth=smooth, only=only)
    return o


def ellipse_pts(center, a, b, n=32, plane_n=(0, 0, 1), plane_u=(1, 0, 0)):
    """points on an ellipse lying in the plane with normal plane_n; a along plane_u"""
    nrm = np.array(plane_n, dtype=float)
    nrm /= np.linalg.norm(nrm)
    u = np.array(plane_u, dtype=float)
    u = u - nrm * np.dot(u, nrm)
    u /= np.linalg.norm(u)
    v = np.cross(nrm, u)
    return [np.array(center) + u * a * math.cos(TAU * i / n) + v * b * math.sin(TAU * i / n) for i in range(n)]


def g_hood(cx=0.0, cy=0.0, zc=1.68, rx=0.118, ryb=0.135, ryf=0.150, top=0.135, z0=1.50, brow=1.755, chin=1.56,
           open_deg=62.0, peak=0.03, flare=0.05, nu=26, nv=14, drape=0.0):
    """Raised hood around the head. Rings are C-shaped (open at the face) below `brow`, closed above it.
    z0 = bottom edge (flares by `flare` onto the shoulders), zc = centre of the dome, top = dome height.
    peak pushes the brow rim forward; drape widens the bottom sideways."""
    z1 = zc + top

    def fn(U, V):
        z = z0 + (z1 - z0) * V
        # dome radius factor
        k = np.where(z > zc, np.sqrt(np.clip(1 - ((z - zc) / top) ** 2, 0, 1)), 1.0)
        fl = np.clip((zc - 0.06 - z) / max(zc - 0.06 - z0, 1e-4), 0, 1) ** 1.5
        k = k + fl * flare / rx
        op = math.radians(open_deg) * np.clip((brow - z) / (brow - chin), 0, 1) ** 0.6
        # azimuth from the front (-Y) going through +X (left) ... full circle minus the opening
        a = op + (TAU - 2 * op) * U
        s = np.sin(a)
        co = np.cos(a)
        ry = np.where(co > 0, ryf, ryb)
        x = cx + (rx + fl * drape) * k * s
        y = cy - ry * k * co
        # push the rim forward near the opening (peak) around brow height
        rimness = np.clip(1 - np.minimum(U, 1 - U) / 0.16, 0, 1)
        hz = np.exp(-((z - brow) / 0.07) ** 2)
        y = y - peak * rimness * hz - 0.012 * rimness
        return np.stack([x, y, z], axis=-1)
    g = g_grid(fn, nu, nv, close_u=False, cap1=False)
    return g


def weld(obj, dist=0.0015):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return obj


def hood(c, src, cls="cloth", name="hood", thick=0.007, bones=("Head", "neck_01", "spine_03"), **kw):
    g = g_hood(**kw)
    o = add(name, g, cls, src, sharp=180)
    weld(o)
    m = o.modifiers.new("solid", 'SOLIDIFY')
    m.thickness = thick
    m.offset = 1.0
    zc = kw.get("zc", 1.68)
    z0 = kw.get("z0", 1.50)

    def wf(co):
        z = co[:, 2]
        h = sstep(z, z0 + 0.04, 1.60)
        return {"Head": h, "neck_01": (1 - h) * 0.5, "spine_03": (1 - h) * 0.5}
    w_func(o, wf)
    return o


def cloak(c, src, z_top=1.50, z_bot=0.72, width=0.26, depth_top=0.15, depth_bot=0.22, arc=0.5, cls="cloth",
          name="cloak", thick=0.006, nu=18, nv=12, wrap=0.0, flare=0.04, ragged=0.0, seed=1, cy=0.03, solid=True,
          pleats=5.0, pleat_amp=0.05, top_taper=0.55, taper_len=0.16):
    """Back cloak hanging from the shoulders. arc = fraction of the full circle covered (centred on the back),
    wrap adds extra width at the top (over the shoulders). Weighted down the spine chain.
    pleats = number of vertical folds across the cloak, pleat_amp = their relative depth at the hem."""
    u0 = 0.25 - arc / 2
    u1 = 0.25 + arc / 2
    rng = np.random.RandomState(seed)
    jag = rng.rand(nu) if ragged else None

    def fn(U, V):
        z = z_top + (z_bot - z_top) * V
        t = (U - u0) / (u1 - u0)
        fold = 1.0 + pleat_amp * (V ** 1.3) * np.sin(TAU * pleats * t + seed)
        # the top edge tucks in towards the neck (top_taper = fraction of the size at the very top)
        tp = top_taper + (1 - top_taper) * sstep(V, 0.0, taper_len)
        w = (width + wrap * (1 - V) ** 2 + flare * V) * fold * tp
        d = (depth_top + (depth_bot - depth_top) * V) * fold * (0.5 + 0.5 * tp)
        x, y = ell(U, 0.0, cy, w, d, d, 2.2)
        if ragged:
            j = np.interp(U[0], np.linspace(u0, u1, nu), jag)
            z = z + (V ** 6) * ragged * (j[None, :] - 0.5) * 2
        return np.stack([x, y, z], axis=-1)
    g = g_grid(fn, nu, nv, close_u=False, u0=u0, u1=u1)
    o = add(name, g, cls, src, sharp=180, solidify=thick if solid else 0.0, double_sided=True)

    def wf(co):
        z = co[:, 2]
        a = sstep(z, 1.26, 1.40)
        b = sstep(z, 1.10, 1.24) * (1 - a)
        cc = sstep(z, 0.98, 1.10) * (1 - a) * (1 - sstep(z, 1.10, 1.24))
        d = 1 - a - b - cc
        return {"spine_03": a, "spine_02": b, "spine_01": cc, "pelvis": np.clip(d, 0, 1)}
    w_func(o, wf, smooth=2)
    return o


ROBE_BONES = ["spine_03", "spine_02", "spine_01", "pelvis", "thigh_l", "thigh_r", "calf_l", "calf_r", "neck_01",
              "clavicle_l", "clavicle_r"]


def robe(c, src, stations, nu=28, nv=14, p=2.2, name="robe", cls="cloth", thick=0.0, smooth=4, pleats=0.0,
         pleat_amp=0.0, u0=0.0, u1=1.0, close=True, ragged=0.0, seed=2, calf_to_thigh=0.5, only=ROBE_BONES, mod=None,
         sharp=180, double_sided=True, front_split=0.0):
    """Robe / skirt / tunic lofted around the body through stations [(z, cx, cy, rx, ry_front, ry_back), ...].
    Weights are transferred from the body (torso + leg bones only) and smoothed.
    pleats = number of vertical folds around the garment (amplitude pleat_amp grows towards the hem),
    ragged = hem raggedness in metres, calf_to_thigh = fraction of calf weight folded back to the thighs (keeps the
    hem from following the shins too closely), front_split = width (in u) of an open slit at the front hem."""
    st = sorted(stations, key=lambda q: q[0])
    z0, z1 = st[0][0], st[-1][0]
    rng = np.random.RandomState(seed)
    jag = rng.rand(nu + 1)

    def m(U, V, x, y, z):
        hem = (1 - V)                       # 1 at the bottom
        if pleats:
            cx = spl(z, [(q[0], q[1]) for q in st])
            cy = spl(z, [(q[0], q[2]) for q in st])
            f = 1.0 + pleat_amp * hem ** 1.5 * np.sin(TAU * pleats * U + seed)
            x = cx + (x - cx) * f
            y = cy + (y - cy) * f
        if ragged:
            j = np.interp(U[0], np.linspace(u0, u1, nu + 1), jag)
            z = z + hem ** 8 * ragged * (j[None, :] - 0.5) * 2
        if mod is not None:
            x, y, z = mod(U, V, x, y, z)
        return x, y, z
    g = g_stations(st, nu=nu, nv=nv, p=p, close=close, u0=u0, u1=u1, mod=m)
    o = add(name, g, cls, src, sharp=sharp, solidify=thick, double_sided=double_sided)
    w_transfer(o, c.ref, smooth=smooth, only=list(only))
    if calf_to_thigh:
        def fold(W, col, co):
            for sd in ("l", "r"):
                mv = W[:, col["calf_" + sd]] * calf_to_thigh
                W[:, col["calf_" + sd]] -= mv
                W[:, col["thigh_" + sd]] += mv
            return W
        w_mix(o, fold)
    return o


def sleeve(c, src, stations, n=14, nv=8, name="sleeve_l", cls="cloth", thick=0.0, smooth=3, mirror=True, p=2.0,
           only=("spine_03", "clavicle_l", "upperarm_l", "lowerarm_l"), axis_y=0.068, axis_z=1.44, sharp=180):
    """Sleeve around the LEFT arm (T-pose, arm along +X). stations = [(x, drop, cy, r_vertical, r_front, r_back)]
    where drop = how far the section centre hangs below the arm axis. Mirrored to the right arm by default."""
    st = [(q[0], q[1], q[2], q[3], q[4], q[5]) for q in stations]
    g = g_stations(st, nu=n, nv=nv, p=p)
    g = gxf(g, frame((0.0, axis_y, axis_z), (1, 0, 0), (0, 1, 0)))
    o = add(name, g, cls, src, sharp=sharp, solidify=thick, double_sided=True)
    w_transfer(o, c.ref, smooth=smooth, only=list(only))
    if mirror:
        return o, mirrored(o)
    return o


def limb_plate(c, name, mask, bone, off=0.018, sm=4, src="steel", cls="metal", sharp=60, mirror=True):
    """Rigid plate made from the body surface under `mask` (left side), pushed out and smoothed."""
    o = shell(c.B, mask, off, name, cls, src, smooth=sm, sharp=sharp)
    w_rigid(o, bone)
    if mirror:
        return o, mirrored(o)
    return o


PAULDRON_ROWS = ((0.0, 1.0), (0.15, 1.0), (0.30, 1.0), (0.44, 1.0), (0.41, 1.09), (0.57, 1.09), (0.72, 1.09), (0.69, 1.18),
                 (0.85, 1.18), (1.0, 1.18))


def g_pauldron(rx=0.16, ry=0.155, rz=0.10, th_out=100.0, th_in=58.0, th_fb=86.0, nu=16, rows=PAULDRON_ROWS):
    """Layered shoulder plate: an ellipsoid cap whose radius steps outward (overlapping lames).
    Local frame: +X outward (away from the neck), +Z up. th_* = how far (degrees from the top) the plate reaches
    on the outer / inner (neck) / front-back sides. rows = [(v, radial scale)] per ring."""
    rows = list(rows)
    nv = len(rows)
    vv = np.array([r[0] for r in rows])
    sc = np.array([r[1] for r in rows])

    def fn(U, V):
        idx = np.clip(np.rint(V * (nv - 1)).astype(int), 0, nv - 1)
        v = vv[idx]
        s = sc[idx]
        a = U * TAU
        ca, sa = np.cos(a), np.sin(a)
        thmax = np.radians(th_fb + (th_out - th_fb) * np.clip(ca, 0, 1) ** 1.5 + (th_in - th_fb) * np.clip(-ca, 0, 1) ** 1.5)
        th = thmax * v
        return np.stack([rx * s * np.sin(th) * ca, ry * s * np.sin(th) * sa, rz * (0.6 + 0.4 * s) * np.cos(th)], axis=-1)
    return g_grid(fn, nu, nv, close_u=True)


def rim_of(g, nu, row=-1):
    """the vertices of one ring (default: last) of a g_grid surface, e.g. to sweep a trim tube along a rim"""
    v = g["v"]
    n_rows = len(v) // nu
    r = row % n_rows
    return v[r * nu:(r + 1) * nu]


def fur_roll(path, radius, src, name="fur", cls="cloth", closed=True, n=10, noise=0.018, scale=28.0, seed=3,
             bone=None, sy=1.0):
    g = g_tube(path, radius, n=n, closed=closed, sy=sy)
    o = add(name, g, cls, src, sharp=180, subsurf=1)
    apply_mods(o)
    displace_noise(o, noise, scale, seed)
    if bone:
        w_rigid(o, bone)
    return o


def face_landmarks(c):
    """Rest-space face landmarks for the skin look (colour zones, stubble, wrinkles): eye centre (ex, ey, ez), nose tip
    (ny, nz), chin height cz. Measured on the reference body / eye meshes of this character."""
    lm = dict(ex=0.032, ey=-0.085, ez=1.690, ny=-0.125, nz=1.650, cz=1.565)
    try:
        B = c.B
        head = B.w("Head") > 0.5
        if c.eyes is not None:
            e = get_co(c.eyes)
            lm["ex"] = float(np.abs(e[:, 0]).mean())
            lm["ey"] = float(e[:, 1].mean())
            lm["ez"] = float(e[:, 2].mean())
        mid = head & (np.abs(B.x) < 0.012) & (B.z < lm["ez"] - 0.012) & (B.z > lm["ez"] - 0.075)
        if mid.any():
            i = np.nonzero(mid)[0][np.argmin(B.y[mid])]
            lm["ny"], lm["nz"] = float(B.y[i]), float(B.z[i])
        fr = head & (np.abs(B.x) < 0.02) & (B.y < lm["ey"] + 0.03) & (B.z < lm["nz"] - 0.03)
        if fr.any():
            lm["cz"] = float(B.z[fr].min())
        log("face landmarks", {k: round(v, 3) for k, v in lm.items()})
    except Exception as ex_:
        log("face_landmarks fallback:", ex_)
    return lm


def finalize(c, cid, hair_tint=(0.10, 0.075, 0.055), skin=None, tex=None, notes="", max_tris=35000, extra=None,
             eyes_emis=False, hair_gain=1.0, decimate=None):
    """skin: dict(gain=, sat=, tint=[, stubble=, scars=, grime=, pores=, age=]) or None for the default skin look."""
    skin_d = None
    hair_d = None
    lm = face_landmarks(c)
    if c.skin is not None:
        finish_skin(c)
        skin_d = dict(objs=[c.skin, c.eyes], kind=c.kind, landmarks=lm, look=dict(skin or {}))
        if FAST:
            skin_d["material"] = fin.make_skin_material(cid, c.kind, c.skin, c.eyes, **(skin or {}))
    hobjs = list(c.hairs) + ([c.brows] if c.brows is not None else [])
    if hobjs:
        if c.brows is not None:
            c.brows["hair_tex"] = 1 if c.kind == "male" else 2
        hair_d = dict(objs=hobjs, look=dict(tint=hair_tint, gain=hair_gain), center=(0.0, lm["ey"] + 0.095))
        if FAST:
            hair_d["material"] = fin.make_hair_material(cid, hobjs, tint=hair_tint, gain=hair_gain)
    if c.ref is not None:
        bpy.data.objects.remove(c.ref, do_unlink=True)
        c.ref = None
    return fin.finalize(cid, c.arm, skin=skin_d, hair=hair_d, tex=tex, fast=FAST, notes=notes, max_tris=max_tris,
                        extra=extra, decimate=decimate)
