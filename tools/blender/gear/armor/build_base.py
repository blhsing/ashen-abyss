"""build_base.py <class> [--preview]: barely-clad base body hero_<class>_base.glb, split into body region meshes.

The body (proportions), face, eyes, brows and hair come from today's hero build script, which is run up to its
finalize() call; its clothing is then thrown away and replaced by a linen loincloth (+ a chest wrap on female bodies).
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gearlib import *
import gearbase as gb

if "--preview" in sys.argv:
    fin.PREVIEW = True
    fin.SMP = dict(ao=12, cav=8, mask=4, color=3, orm=2, normal=4)

# hair: extra Quaternius hair meshes for heroes whose scalp was hidden under a helm / hood so far;
# skin: added to the hero's own skin look (gain / sat / tint come from the hero script)
BASE = {
    "emberblade": dict(hair=[("Hair_Buzzed", {}), ("Hair_Beard", {})], hair_tint=(0.075, 0.058, 0.046), hair_gain=1.0,
                       skin=dict(gain=0.93, sat=0.80, scars=0.8, grime=1.5, stubble=0.8, age=0.75), linen=(0.27, 0.235, 0.18)),
    "astromancer": dict(hair=[], skin=dict(scars=0.12, grime=0.9), linen=(0.33, 0.31, 0.28)),
    "hunter": dict(hair=[("Hair_BuzzedFemale", {})], skin=dict(scars=0.45, grime=1.4), linen=(0.20, 0.185, 0.16)),
    "soulbinder": dict(hair=[], skin=dict(scars=0.35, grime=1.3, age=0.9), linen=(0.25, 0.235, 0.23)),
    "wildsoul": dict(hair=[("Hair_Long", {})], skin=dict(scars=0.7, grime=1.7), linen=(0.25, 0.20, 0.14)),
    "forgebody": dict(hair=[], skin=dict(scars=0.5, grime=1.2), linen=(0.30, 0.25, 0.19)),
    "lampknight": dict(hair=[("Hair_Buns", {})], skin=dict(scars=0.3, grime=1.1), linen=(0.36, 0.325, 0.26)),
    "pactbinder": dict(hair=[], skin=dict(scars=0.4, grime=1.2), linen=(0.24, 0.215, 0.19)),
}


def crotch_z(B):
    m = (np.abs(B.x) < 0.012) & (B.z > 0.70) & (B.z < 1.0) & (B.w("pelvis", "spine_01") > 0.6)
    return float(B.z[m].min())


def body_y_at(c, x, z, front=True):
    o = Vector((float(x), -1.0 if front else 1.0, float(z)))
    d = Vector((0.0, 1.0 if front else -1.0, 0.0))
    ok, loc, nrm, fi = c.ref.ray_cast(o, d)
    return float(loc[1]) if ok else None


def hanging_panel(c, name, x0, x1, z_top, z_bot, src, front=True, margin=0.014, nu=9, nv=10, thick=0.004, folds=((2.3, 0.006, 0.2),),
                  flare=0.08, ragged=0.012, seed=1, cls="cloth", lift=0.0):
    """cloth panel hanging plumb from the body surface at z_top (front or back); luv v = 0 at the hem"""
    rng = np.random.RandomState(seed)
    sgn = -1.0 if front else 1.0
    us = np.linspace(0, 1, nu)
    vs = np.linspace(0, 1, nv)
    jag = rng.rand(nu)
    P = np.zeros((nv, nu, 3))
    for i, u in enumerate(us):
        yk = None
        for j in range(nv - 1, -1, -1):          # from the top row (v = 1) down to the hem
            v = vs[j]
            hem = 1.0 - v
            x = x0 + (x1 - x0) * u
            xm = (x0 + x1) / 2
            x = xm + (x - xm) * (1 + flare * hem)
            z = z_bot + (z_top - z_bot) * v + ragged * (jag[i] - 0.5) * 2 * hem ** 4
            yb = body_y_at(c, x, z, front)
            if yb is not None:
                yb = yb + sgn * margin
                yk = yb if yk is None else (min(yk, yb) if front else max(yk, yb))
            if yk is None:
                yk = 0.0
            d = fold_sum(np.array(u), hem, folds, 1.2, 0.15)
            P[j, i] = (x, yk + sgn * (float(d) + abs(float(d)) * 0.35) + sgn * lift * hem, z)
    U, V = np.meshgrid(us, vs)
    faces = []
    for j in range(nv - 1):
        for i in range(nu - 1):
            a = j * nu + i
            faces.append((a, a + 1, a + 1 + nu, a + nu))
    g = geo(P.reshape(-1, 3), faces, np.stack([U.ravel(), V.ravel()], axis=1))
    o = add(name, g, cls, src, solidify=thick, sharp=180, double_sided=True)

    def wf(co):
        z, x = co[:, 2], co[:, 0]
        t = sstep(0.99 - z, 0.0, 0.30) * 0.85
        l = sstep(x, -0.05, 0.05)
        return {"pelvis": 1 - t, "thigh_l": t * l, "thigh_r": t * (1 - l)}
    w_func(o, wf)
    return o


def loincloth(c, src="linen", roll="linen_roll", female=False, seed=3):
    """wrapped linen loincloth: a band round the hips, a cloth between the legs (shell of the body, so it deforms like
    the skin), a rolled waist cord with a knot and short hanging flaps front and back"""
    B = c.B
    cz = crotch_z(B)
    noarm = B.w(*BONES_ARM, *BONES_HAND) < 0.3
    zt = 1.002 if female else 0.992
    band_lo = 0.925
    band = noarm & (B.z > band_lo) & (B.z < zt)
    wv = 0.040 + (0.150 if female else 0.135) * sstep(B.z, cz + 0.005, band_lo + 0.01) ** 1.25
    back = B.y > 0.03
    wv = np.where(back, wv + 0.022 * sstep(B.z, cz, band_lo), wv)                # the seat is cut a little wider
    panel = noarm & (B.z <= band_lo) & (B.z > cz - 0.012) & (np.abs(B.x) < wv)
    m = band | panel
    o = shell(B, m, 0.0065, "loin_wrap", "cloth", src, smooth=3, sharp=180)
    wrinkle_shell(o, 0.0022, 46.0, "z", seed, mask=lambda co: 0.35 + 0.65 * sstep(co[:, 2], cz + 0.02, band_lo))
    o["grp"] = "wrap_loin"
    log("loincloth: crotch z %.3f, shell verts %d" % (cz, len(o.data.vertices)))
    # rolled waist cord, slung a little lower in front
    cx, cy, rx, ryf, ryb = body_ring(c, zt - 0.02, 0.0)
    pts = []
    for i in range(30):
        a = TAU * i / 30
        s_, c_ = math.sin(a), math.cos(a)
        pts.append((rx * 1.15 * c_, cy + (ryb if s_ > 0 else ryf) * 1.15 * s_, zt - 0.022 + 0.012 * s_))
    r, Q, N = ribbon(c, pts, roll, width=0.026, thick=0.011, offset=0.0075, name="loin_roll", cls="cloth", closed=True, step=0.02,
                     only=["pelvis", "spine_01", "thigh_l", "thigh_r"], sharp=180)
    r["grp"] = "wrap_loin"
    # knot on the left hip + two short ends
    k = int(np.argmax(Q[:, 0]))
    kp = Q[k] + N[k] * 0.012
    kn = add("loin_knot", gxf(g_sphere(1.0, 8, 5), T(*kp) @ S(0.020, 0.022, 0.018)), "cloth", roll, sharp=180)
    w_transfer(kn, r, smooth=0)
    kn["grp"] = "wrap_loin"
    for j, (dx, dy, ln) in enumerate(((0.012, -0.018, 0.105), (0.016, 0.014, 0.075))):
        path = [kp, kp + np.array((dx * 0.6, dy * 0.5, -ln * 0.35)), kp + np.array((dx, dy, -ln * 0.7)), kp + np.array((dx * 0.8, dy * 1.2, -ln))]
        e = add("loin_end_%d" % j, g_tube(path, [1.0, 1.1, 1.25, 1.35], n=5, sx=0.004, sy=0.013, up=(1, 0, 0)), "cloth", src, sharp=180)
        w_func(e, lambda co: {"pelvis": 0.8, "thigh_l": 0.2})
        e["grp"] = "wrap_loin"
    # hanging flaps
    f = hanging_panel(c, "loin_flap_f", -0.068, 0.068, zt - 0.030, cz - 0.105, src, front=True, margin=0.013, seed=seed,
                      folds=((1.6, 0.0055, 0.15), (3.3, 0.0025, 0.6)))
    f["grp"] = "wrap_loin"
    b = hanging_panel(c, "loin_flap_b", -0.060, 0.060, zt - 0.020, cz - 0.045, src, front=False, margin=0.013, seed=seed + 4,
                      folds=((1.8, 0.005, 0.4),), ragged=0.016)
    b["grp"] = "wrap_loin"
    return o


def chest_wrap(c, src="linen", seed=5):
    """bandeau of wound linen strips (shell of the body, bridged across the chest), knotted at the back"""
    B = c.B
    noarm = B.w("upperarm", "lowerarm", *BONES_HAND) < 0.35
    zb, zt = 1.240, 1.392
    m = noarm & (B.z > zb) & (B.z < zt) & (np.abs(B.x) < 0.215) & (B.w("Head", "neck_01") < 0.2)
    o = shell(B, m, 0.006, "chest_wrap", "cloth", src, smooth=2, sharp=180)
    co = get_co(o)
    front = co[:, 1] < 0.0
    # bridge: nothing in the middle lies behind the line that joins the fullest points left and right of it
    zs = np.linspace(zb, zt, 9)
    for k in range(len(zs) - 1):
        row = front & (co[:, 2] >= zs[k] - 0.012) & (co[:, 2] < zs[k + 1] + 0.012)
        side = row & (np.abs(co[:, 0]) > 0.045) & (np.abs(co[:, 0]) < 0.10)
        if not side.any():
            continue
        yb = float(np.percentile(co[side, 1], 12))
        mid = front & (co[:, 2] >= zs[k]) & (co[:, 2] < zs[k + 1]) & (np.abs(co[:, 0]) < 0.075)
        w = 1.0 - sstep(np.abs(co[mid, 0]), 0.035, 0.075)
        co[mid, 1] = np.minimum(co[mid, 1], co[mid, 1] * (1 - w) + (yb + 0.004) * w)
    set_co(o, co)
    keep = get_co(o)
    sm = laplacian(keep, mesh_edges(o), 3, 0.5)
    d = sm - keep
    # smoothing may only move outwards / sideways (never back into the body)
    me = o.data
    n = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get('normal', n)
    n = n.reshape(-1, 3)
    inward = (d * n).sum(1) < 0
    d[inward] -= n[inward] * (d[inward] * n[inward]).sum(1)[:, None]
    set_co(o, keep + d)
    wrinkle_shell(o, 0.0024, 52.0, "z", seed, mask=lambda q: 0.55 + 0.45 * np.sin(q[:, 0] * 9.0) ** 2)
    o["grp"] = "wrap_chest"
    # knot between the shoulder blades
    cx, cy, rx, ryf, ryb = body_ring(c, (zb + zt) / 2, 0.0)
    kp = np.array((0.012, cy + ryb + 0.010, (zb + zt) / 2 + 0.01))
    ok, loc, nrm, fi = c.ref.closest_point_on_mesh(Vector(kp))
    kp = np.array(loc) + np.array(nrm) * 0.014
    kn = add("chest_knot", gxf(g_sphere(1.0, 8, 5), T(*kp) @ S(0.022, 0.016, 0.020)), "cloth", src, sharp=180)
    kn["grp"] = "wrap_chest"
    w_rigid(kn, "spine_03")
    for j, (dx, ln) in enumerate(((-0.020, 0.085), (0.024, 0.060))):
        path = [kp, kp + np.array((dx * 0.5, 0.006, -ln * 0.4)), kp + np.array((dx, 0.004, -ln))]
        e = add("chest_end_%d" % j, g_tube(path, [1.0, 1.15, 1.3], n=5, sx=0.004, sy=0.012, up=(0, 1, 0)), "cloth", src, sharp=180)
        w_rigid(e, "spine_03")
        e["grp"] = "wrap_chest"
    return o


def main():
    cls = args()[0]
    spec = BASE[cls]
    cap = capture_hero(cls)
    c = cap["c"]
    kw = cap["kw"]
    look = dict(kw.get("skin") or {})
    look.update(spec.get("skin", {}))
    look.setdefault("head_texels", 1.35)
    # ember brands / welts of today's hero that lie on the skin leave old burn scars on the naked body
    scar_pts = None
    if cls == "forgebody":
        scar_pts = fin._emis_points([o for o in mk.PARTS if o["cls"] == "emis" and o["owner"] == "body"
                                     and o.name.startswith(("scar_", "brand_"))])
    topknot = [o for o in mk.PARTS if o.name.startswith("topknot_")] if cls == "forgebody" else []
    # the welts themselves stay in the scene as occluders of the skin bake (the charred crust of the scars is driven by
    # the baked contact shadow); they are not exported with the base body (the glowing welts are part of the t4 chest)
    ghosts = [o for o in mk.PARTS if cls == "forgebody" and o["cls"] == "emis" and o["owner"] == "body" and o.name.startswith(("scar_", "brand_"))]
    for o in ghosts:
        mk.PARTS.remove(o)
    drop_parts(keep=topknot)
    for o in topknot:
        o["grp"] = "hair"
    c.hide[:] = False
    for name, hk in spec["hair"]:
        add_hair(c, name, **hk)
    lin = spec["linen"]
    mats.cloth("linen", base=tuple(v * 0.78 for v in lin), var=0.45, ash=0.55, ash_h=0.6, folds=0.55, weave=1.6, worn=0.8, tight=True)
    mats.cloth("linen_roll", base=tuple(v * 0.62 for v in lin), var=0.4, ash=0.5, folds=0.2, weave=1.8, worn=0.8, tight=True)
    female = c.kind == "female"
    loincloth(c, female=female)
    if female:
        chest_wrap(c)
    report_unweighted()
    hair_tint = spec.get("hair_tint", kw.get("hair_tint", (0.10, 0.075, 0.055)))
    gb.finalize_base(cls, c, look, hair_tint, spec.get("hair_gain", kw.get("hair_gain", 1.0)), scar_pts)


main()
