"""Astromancer armour looks t1..t3 and the two NEW t4 pieces `t4x` (the rest of t4 = today's hero, split by split_t4.py).

Identity: slim star-reader mage -- night-blue cloth over a grey-blue under-robe, plum stole, silver and blue-black
star-metal, a few small cold-blue star emissives. Hero materials reused: stole, paper, starmetal, silver, iron, boot,
belt, tome, star.
  t1  scavenged: threadbare plum-grey wool shawl worn as a hood with a ragged shoulder drape, patched grey-blue wool
      dress torn off at the knee with ripped short sleeves, sackcloth apron, rope belt with a rolled star chart,
      fingerless wool mitts over rag wrist wraps, holed leggings, cloth shoes with foot wraps.
  t2  acolyte: pale linen wimple with a round bib and a blue brow band, plain mid-blue robe to the ankle with long tight
      sleeves, leather belt with a small book and a satchel, thin cloth gloves, linen under-trousers, ankle shoes.
  t3  astrologer: tall back-swept pointed hat with a padded silver roll, one star and a plum neck veil; deep indigo
      velvet robe with silver brocade hem and wide bell sleeves, silver-trimmed shoulder capelet, plum stole with small
      stars, grey girdle and a slung silver chain; long gloves with silver rings, fine leggings with brocade garters,
      strapped boots with silver buckles.
  t4x helm = open star-metal diadem worn over the hair (crescent above, star drop on the brow); gloves = star-metal
      bracelets with a rune and a silver finger-loop chain (hands bare).
"""
from gearkit import *

SILV = (0.27, 0.28, 0.31)       # old silver thread (brocade bands)
PLUM = (0.085, 0.030, 0.065)
BODY_BONES = ["pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r", "neck_01"]


def _mats():
    # ---- t1
    mats.cloth("as_shawl", base=(0.066, 0.046, 0.056), var=0.45, ash=0.6, ash_h=0.9, folds=0.6, worn=0.92, scan="wool", tight=False)
    mats.cloth("as_dress1", base=(0.078, 0.090, 0.112), var=0.45, ash=0.65, ash_h=0.6, folds=0.6, worn=0.9, scan="wool", tight=True)
    mats.cloth("as_dress1s", base=(0.078, 0.090, 0.112), var=0.45, ash=0.7, ash_h=0.5, folds=0.9, worn=0.9, scan="wool", tight=False)
    mats.cloth("as_sack", base=(0.150, 0.122, 0.085), var=0.4, ash=0.6, ash_h=0.5, folds=0.5, worn=0.9, scan="burlap", tight=False)
    mats.cloth("as_patch", base=(0.110, 0.070, 0.075), var=0.4, ash=0.6, folds=0.3, worn=0.9, scan="burlap", tight=True)
    mats.cloth("as_rope", base=(0.20, 0.16, 0.10), var=0.3, ash=0.4, folds=0.0, scan="rope", hem=False)
    mats.cloth("as_mitt", base=(0.062, 0.050, 0.058), var=0.45, ash=0.5, folds=0.3, worn=0.9, scan="wool", tight=True)
    mats.cloth("as_rag", base=(0.170, 0.152, 0.125), var=0.4, ash=0.6, folds=0.5, worn=0.9, scan="linen", tight=True)
    mats.cloth("as_legging1", base=(0.046, 0.048, 0.058), var=0.5, ash=0.6, folds=0.5, worn=0.9, scan="wool", tight=True)
    mats.cloth("as_shoe1", base=(0.100, 0.082, 0.066), var=0.4, ash=0.6, folds=0.3, worn=0.9, scan="canvas", tight=True)
    # ---- t2
    mats.cloth("as_linen", base=(0.36, 0.34, 0.29), var=0.25, ash=0.35, ash_h=0.4, folds=0.5, worn=0.45, scan="linen", tight=True)
    mats.cloth("as_band2", base=(0.040, 0.062, 0.125), var=0.3, ash=0.2, folds=0.2, worn=0.4, scan="twill", tight=True)
    mats.cloth("as_robe2_top", base=(0.040, 0.060, 0.118), var=0.3, ash=0.3, ash_h=0.4, folds=0.5, worn=0.45, scan="twill", tight=True)
    mats.cloth("as_robe2", base=(0.040, 0.060, 0.118), var=0.3, ash=0.5, ash_h=0.45, folds=0.9, worn=0.45, scan="twill", tight=False)
    mats.cloth("as_glove2", base=(0.105, 0.112, 0.130), var=0.3, ash=0.3, folds=0.3, worn=0.45, scan="linen", tight=True)
    mats.cloth("as_hose2", base=(0.150, 0.150, 0.150), var=0.3, ash=0.45, folds=0.5, worn=0.5, scan="linen", tight=True)
    # ---- t3
    mats.cloth("as_velvet_top", base=(0.022, 0.026, 0.074), var=0.3, ash=0.12, folds=0.4, worn=0.25, scan="velvet", tight=True)
    mats.cloth("as_velvet", base=(0.022, 0.026, 0.074), var=0.3, ash=0.35, ash_h=0.35, folds=0.9, worn=0.25, scan="velvet", tight=False,
               bands=[(0.0, 0.070, SILV), (0.086, 0.098, PLUM)])
    mats.cloth("as_velvet_sleeve", base=(0.022, 0.026, 0.074), var=0.3, ash=0.1, folds=0.4, worn=0.25, scan="velvet", tight=False,
               bands=[(0.84, 1.0, SILV), (0.79, 0.815, PLUM)])
    mats.cloth("as_capelet", base=(0.036, 0.040, 0.098), var=0.35, ash=0.15, folds=0.3, worn=0.3, scan="brocade", tight=False)
    mats.cloth("as_hat", base=(0.024, 0.028, 0.080), var=0.3, ash=0.1, folds=0.15, worn=0.3, scan="brocade", tight=False, hem=False,
               bands=[(0.30, 0.325, SILV)])
    mats.cloth("as_roll", base=(0.20, 0.21, 0.235), var=0.3, ash=0.2, folds=0.0, worn=0.35, scan="brocade", hem=False)
    mats.cloth("as_veil", base=PLUM, var=0.3, ash=0.2, folds=0.6, worn=0.3, scan="linen", tight=False)
    mats.cloth("as_stole3", base=PLUM, var=0.3, ash=0.15, folds=0.2, worn=0.3, scan="velvet", tight=True, hem=True)
    mats.cloth("as_stole3_tail", base=PLUM, var=0.3, ash=0.2, folds=0.3, worn=0.3, scan="velvet", tight=False,
               bands=[(0.90, 1.0, SILV)])
    mats.cloth("as_girdle", base=(0.115, 0.118, 0.140), var=0.3, ash=0.2, folds=0.2, worn=0.35, scan="brocade", hem=True)
    mats.cloth("as_glove3", base=(0.030, 0.026, 0.060), var=0.3, ash=0.1, folds=0.3, worn=0.3, scan="velvet", tight=True)
    mats.cloth("as_legging3", base=(0.030, 0.034, 0.070), var=0.3, ash=0.3, folds=0.5, worn=0.3, scan="twill", tight=True)
    mats.cloth("as_broc", base=(0.20, 0.21, 0.235), var=0.35, ash=0.2, folds=0.1, worn=0.35, scan="brocade", tight=True)
    mats.leather("as_boot3", base=(0.030, 0.026, 0.034), worn=0.35, ash=0.3, ash_h=0.25, scan="leather_b")


# ---------------------------------------------------------------------------------------------- helpers of this file
def _var_off(lo, off, arm_off, x_a=0.235, x_b=0.40, hem=0.0165):
    """shell offset: `off` on the trunk, thinning to `arm_off` along the arms, at least `hem` at the waist edge"""
    def f(co):
        ax = np.abs(co[:, 0])
        o = off + (arm_off - off) * sstep(ax, x_a, x_b)
        return o + np.clip(hem - o, 0.0, None) * (1 - sstep(co[:, 2], lo, lo + 0.07))
    return f


def _wrinkle2(K, o, body=0.0022, arm=0.0015):
    """compression folds: across the height on the trunk, across the length on the sleeves"""
    wrinkle_shell(o, body, 24.0, "z", K.rs(), mask=lambda co: 1 - sstep(np.abs(co[:, 0]), 0.20, 0.27))
    wrinkle_shell(o, arm, 80.0, "x", K.rs(), mask=lambda co: sstep(np.abs(co[:, 0]), 0.22, 0.30))
    return o


def _cape(K, name, src, zmin=1.38, arm_x=0.27, off=0.030, thick=0.006, chew=0, slope=0.5, back_drop=0.0, z_top=1.545, cls="cloth", arm_z=1.40):
    """K.capelet with a plain (not 'even') solidify, so a torn edge cannot shoot spikes"""
    B = K.B
    arms = (B.w("upperarm", "clavicle") > 0.5) & (np.abs(B.x) < arm_x) & (B.z > arm_z)
    zlim = zmin - slope * B.y - back_drop * sstep(B.y, 0.0, 0.10)
    core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zlim) & (B.z < z_top) & (np.abs(B.x) < 0.20)
    mm = (core | arms) & (B.w(*BONES_HAND, "lowerarm") < 0.3)
    o = shell(B, mm, off, name, cls, src, smooth=4, sharp=180)
    if chew:
        chew_edge(o, rounds=int(chew), p=0.36, seed=K.rs(), keep=lambda co: co.z > 1.47 and abs(co.x) < 0.13)
    return solid(o, thick)


def _yoke(K, name, src, r=0.168, off=0.019, thick=0.004, cen=(0.0, 0.02, 1.53), z_hi=1.535, cls="cloth"):
    """round bib / yoke: the trunk surface within r of the base of the neck (shell)"""
    B = K.B
    d = np.sqrt(B.x ** 2 + (B.y - cen[1]) ** 2 + (B.z - cen[2]) ** 2)
    m = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (d < r) & (B.z < z_hi) & (B.w(*BONES_HAND, "lowerarm", "upperarm") < 0.3)
    o = shell(B, m, off, name, cls, src, smooth=4, sharp=180)
    return solid(o, thick)


def _rim_paths(o):
    """ordered boundary loops (vertex coordinates) of a sheet, read before its solidify modifier is applied"""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    nxt = {}
    for e in bm.edges:
        if e.is_boundary:
            a, b = e.verts[0].index, e.verts[1].index
            nxt.setdefault(a, []).append(b)
            nxt.setdefault(b, []).append(a)
    co = np.array([v.co[:] for v in bm.verts])
    bm.free()
    loops, seen = [], set()
    for s in sorted(nxt):
        if s in seen:
            continue
        loop, prev, cur = [s], None, s
        seen.add(s)
        while True:
            cand = [n for n in nxt[cur] if n != prev and n not in seen]
            if not cand:
                break
            prev, cur = cur, cand[0]
            loop.append(cur)
            seen.add(cur)
        if len(loop) >= 8:
            loops.append(co[loop])
    return loops


def _edge_trim(K, name, o, src, r=0.005, pick=None, lift=0.004, cls="metal", n=5, smooth=2):
    """round trim along the open rim(s) of a body shell (weights from the body, like the shell itself)"""
    out = []
    for i, L in enumerate(_rim_paths(o)):
        if pick is not None and not pick(L):
            continue
        P = np.array(L, float)
        for _ in range(smooth):
            P = (np.roll(P, 1, 0) + 2 * P + np.roll(P, -1, 0)) / 4
        Q = []
        for p in P:
            ok, loc, nrm, fi = K.c.ref.closest_point_on_mesh(Vector(p))
            d = p - np.array(loc)
            ln = np.linalg.norm(d)
            Q.append(p + (d / ln if ln > 1e-6 else np.array(nrm)) * lift)
        t = add("%s_%d" % (name, i), g_tube(Q, r, n=n, closed=True), cls, src, sharp=60)
        w_transfer(t, K.c.ref, smooth=1)
        out.append(t)
    return out


def _hang_on(name, host, src, x0, x1, z_top, z_bot, lift=0.008, thick=0.004, nu=6, nv=12, point=0.0, taper=0.0, front=True, cls="cloth"):
    """cloth strip lying on a lofted garment (skirt): its surface is found with rays, its weights are the host's, so
    it follows every fold and moves exactly like the garment"""
    if any(m.type != 'ARMATURE' for m in host.modifiers):
        apply_mods(host)
    bpy.context.view_layer.update()
    s = -1.0 if front else 1.0
    last = {}

    def fn(U, V):
        t = U * 2 - 1
        z = z_top + (z_bot - z_top) * V - point * (1 - np.abs(t)) * V ** 3
        x = (x0 + x1) / 2 + t * (x1 - x0) / 2 * (1 + taper * V)
        y = np.zeros_like(x)
        for idx in np.ndindex(x.shape):
            ok, loc, nrm, fi = host.ray_cast(Vector((float(x[idx]), s * 0.9, float(z[idx]))), Vector((0.0, -s, 0.0)))
            if ok:
                y[idx] = loc[1] + s * lift
                last[idx[1]] = y[idx]
            else:
                y[idx] = last.get(idx[1], s * 0.2)
        return np.stack([x, y, z], axis=-1)
    o = add(name, g_grid(fn, nu, nv, close_u=False), cls, src, sharp=180)
    w_transfer(o, host, smooth=1)
    return solid(o, thick)


def _wrap_on(o, pts, lift, cen=(0.0, -0.012, 1.69)):
    """project guide points on a mesh (the hair) and lift them away from the head centre"""
    out = []
    for p in pts:
        ok, loc, nrm, fi = o.closest_point_on_mesh(Vector(p))
        loc = np.array(loc)
        d = loc - np.array(cen)
        out.append(loc + d / max(np.linalg.norm(d), 1e-9) * lift)
    return np.array(out)


def _sring(K, z, gap, a, p=2.2):
    """point on the superellipse the skirts are lofted through, at height z and angle a (0 = left, pi/2 = back)"""
    rg = K.ring(z, gap)
    e = 2.0 / p
    ca, sa = math.cos(a), math.sin(a)
    return np.array((rg[2] * math.copysign(abs(ca) ** e, ca), rg[1] + (rg[4] if sa > 0 else rg[3]) * math.copysign(abs(sa) ** e, sa), z))


def _skirt(K, name, src, zs, gaps, cls="cloth", nu=30, nv=10, thick=0.005, folds=((7, 0.028, 0.15), (13, 0.010, 0.6)), tails=0, tail_depth=0.16,
           ragged=0.0, u0=0.0, u1=1.0, close=True, calf=0.5, **kw):
    """K.skirt with its stations and gaps given one by one: the linear gap of K.skirt is too tight round the hips of
    this body (the fronts of the thighs and the seat come through at z 0.85-0.95 as soon as pants are worn)"""
    st = K.stations(zs, gaps, plumb=True)
    cy = st[-1][2]
    if tails:
        mod = tails_mod(k=tails, depth=tail_depth, seed=K.rs(), extra=fold_mod(list(folds), cy=cy) if folds else None)
    else:
        mod = fold_mod(list(folds), cy=cy) if folds else None
    o = robe(K.c, src, st, nu=nu, nv=nv, name=name, cls=cls, thick=0.0, ragged=ragged, u0=u0, u1=u1, close=close, calf_to_thigh=calf, mod=mod,
             seed=K.rs(), **kw)
    return solid(o, thick)


def _band(K, name, src, z, width, gap, cls="leather", thick=0.008, buckle=None):
    """belt / girdle that narrows with the waist (K.belt is a straight hoop: on this waist it stands off at the top)"""
    r0, r1 = K.ring(z - width / 2, gap), K.ring(z + width / 2, gap)
    st = [(z - width / 2, 0, r0[1], r0[2], r0[3], r0[4]), (z + width / 2, 0, r1[1], r1[2], r1[3], r1[4])]
    o = add(name, g_stations(st, nu=28, nv=2, p=2.2), cls, src, solidify=thick, sharp=50)
    w_transfer(o, K.c.ref, smooth=2, only=["pelvis", "spine_01", "spine_02"])
    if buckle:
        ob = add(name + "_buckle", gxf(g_box(0.052, 0.016, width * 1.25), T(0, (r0[1] - r0[3] + r1[1] - r1[3]) / 2 - thick - 0.004, z)), "metal", buckle,
                 bevel=0.004, sharp=35)
        w_transfer(ob, o, smooth=0)
    return o


def _on_skirt(host, a, z, lift=0.0, cy=0.02):
    """(point `lift` outside a lofted garment, outward direction) on the ray towards its axis at angle a (0 = left,
    pi/2 = back) and height z"""
    if any(m.type != 'ARMATURE' for m in host.modifiers):
        apply_mods(host)
    bpy.context.view_layer.update()
    d = np.array((math.cos(a), math.sin(a), 0.0))
    ok, loc, nrm, fi = host.ray_cast(Vector((0.8 * d[0], cy + 0.8 * d[1], z)), Vector((-d[0], -d[1], 0.0)))
    p = np.array(loc) if ok else np.array((0.2 * d[0], cy + 0.2 * d[1], z))
    return p + d * lift, d


def _leg_straps(K, name, src, at, width=0.02, off=L3 + 0.004, cls="leather", thick=0.005, sides="lr"):
    """K.limb_straps for the legs, safe at heights where the body mesh has no edge loop (the shin loops are far apart)"""
    B = K.B
    out = []
    for side in ((1, -1) if sides == "lr" else ((1,) if sides == "l" else (-1,))):
        sfx = "l" if side > 0 else "r"
        for i, z in enumerate(at):
            leg = (B.x * side > 0) & (B.w(*BONES_LEG, *BONES_FOOT) > 0.5)
            for dz in (0.02, 0.04, 0.07, 0.11):
                m = leg & (np.abs(B.z - z) < dz)
                if m.sum() >= 8:
                    break
            cx, cy = float((B.x[m].min() + B.x[m].max()) / 2), float((B.y[m].min() + B.y[m].max()) / 2)
            rx, ry = float(B.x[m].max() - B.x[m].min()) / 2 + 0.03, float(B.y[m].max() - B.y[m].min()) / 2 + 0.03
            path = [(cx + rx * math.cos(a), cy + ry * math.sin(a), z) for a in np.linspace(0, TAU, 14, endpoint=False)]
            o, Q, N = ribbon(K.c, path, src, width=width, thick=thick, offset=off, name="%s_%d_%s" % (name, i, sfx), cls=cls, closed=True, step=0.018,
                             only=["thigh_" + sfx, "calf_" + sfx], smooth=1)
            out.append(o)
    return out


def _cone_hat(name, src, M, h=0.25, rx=0.086, ry=0.098, lean=0.07, nu=18, nv=10):
    """tall pointed cap, bent back towards the tip (hat local space: base ring in the XY plane, +Z up, front = -Y)"""
    def fn(U, V):
        a = U * TAU
        k = np.maximum((1 - V) ** 0.92 - 0.07 * np.sin(np.pi * V), 0.03)
        return np.stack([rx * k * np.cos(a), ry * k * np.sin(a) + lean * V ** 1.7, h * V], axis=-1)
    o = add(name, gxf(g_grid(fn, nu, nv, close_u=True, cap1=True), M), "cloth", src, bone="Head", sharp=180)
    return solid(o, 0.005)


# ---------------------------------------------------------------------------------------------- t1 scavenged
def t1(K):
    _mats()
    c, B = K.c, K.B
    K.opts["helm"] = dict(force_hide=("hair",))
    with K.slot("helm"):
        st = [(1.490, 0.022, 0.118, 0.076, 0.118), (1.545, 0.006, 0.100, 0.104, 0.106), (1.61, -0.012, 0.101, 0.114, 0.112),
              (1.69, -0.016, 0.107, 0.116, 0.121), (1.75, -0.016, 0.099, 0.110, 0.117), (1.792, -0.012, 0.074, 0.086, 0.092),
              (1.814, -0.006, 0.038, 0.046, 0.050), (1.823, 0.0, 0.0, 0.0, 0.0)]
        cowl(c, "as_shawl", st, name="shawl_hood", thick=0.007, chin=1.570, brow=1.742, open_deg=62.0, peak=0.016, nu=28, nv=16)
        _cape(K, "shawl_drape", "as_shawl", zmin=1.372, arm_x=0.275, off=0.029, thick=0.006, chew=2, slope=0.5, back_drop=0.05)
    with K.slot("chest"):
        K.tunic("dress_top", "as_dress1", lo=0.995, hi=1.495, sleeve=0.300, off=0.012, wrinkle=0.0026, wfreq=24.0, chew=2, chew_p=0.34,
                keep=lambda co: co.z > 1.10 and abs(co.x) < 0.235)
        tm = K.torso_mask(1.0, 1.50, 0.215)
        K.shell("patch_belly", tm & (B.y < 0.0) & (B.x > 0.015) & (B.x < 0.125) & (B.z > 1.125) & (B.z < 1.235), "as_patch", off=0.0162, smooth=2,
                chew=1, chew_p=0.25)
        K.shell("patch_back", tm & (B.y > 0.02) & (B.x < 0.0) & (B.x > -0.145) & (B.z > 1.27) & (B.z < 1.405), "as_sack", off=0.0162, smooth=2,
                chew=1, chew_p=0.25)
        sk = _skirt(K, "dress_skirt", "as_dress1s", (1.045, 1.0, 0.95, 0.88, 0.78, 0.62, 0.43), (0.020, 0.030, 0.040, 0.044, 0.048, 0.052, 0.054),
                    nu=30, nv=10, tails=7, tail_depth=0.13, ragged=0.03, folds=((6, 0.032, 0.1), (11, 0.012, 0.5)), calf=0.3)
        _hang_on("apron", sk, "as_sack", -0.128, 0.030, 1.038, 0.615, lift=0.008, thick=0.004, nu=7, nv=10, point=0.035, taper=0.10)
        K.rope("rope_belt", "as_rope", z=1.058, gap=0.027, r=0.008, cls="cloth", turns=2)
        # a rolled star chart tied to the rope (the one thing she kept)
        ca = TAU * 0.565
        cpt, cd = _on_skirt(sk, ca, 0.955, lift=0.024)
        SM = T(*cpt) @ R('Z', math.degrees(ca) + 90) @ R('Y', 76)
        ch = props("chart", SM, [(g_roll(0.20, 0.019, n=8), "cloth", "paper", dict(sharp=60)),
                                 (g_tube(ring_path(0.021, 8, "yz", (0.050, 0, 0)), 0.003, n=4, closed=True), "cloth", "as_rope", dict(sharp=180)),
                                 (g_tube(ring_path(0.021, 8, "yz", (-0.055, 0, 0)), 0.003, n=4, closed=True), "cloth", "as_rope", dict(sharp=180))],
                   "pelvis")
        ctop = xf(np.array((-0.055, 0.0, 0.0)), SM) + cd * 0.020
        ch.append(add("chart_cord", g_tube([ctop, (ctop + _sring(K, 1.058, 0.036, ca)) / 2 + cd * 0.006, _sring(K, 1.058, 0.036, ca)], 0.003, n=4),
                      "cloth", "as_rope", sharp=180))
        for o in ch:
            w_func(o, lambda co: {"pelvis": 0.6, "spine_01": 0.4})
    with K.slot("gloves"):
        K.gloves("mitts", "as_mitt", cuff=0.705, off=0.0045, cls="cloth", fingerless=True, chew=1, chew_p=0.3, keep=lambda co: abs(co.x) < 0.80)
        K.sleeves("wrap_l", "as_rag", x0=0.505, x1=0.728, off=0.0100, sides="l", wrinkle=0.0018, waxis="x", wfreq=95.0, chew=1)
        K.sleeves("wrap_r", "as_rag", x0=0.610, x1=0.728, off=0.0100, sides="r", wrinkle=0.0018, waxis="x", wfreq=95.0, chew=1)
    with K.slot("pants"):
        m = m_legs(B, 0.245, 1.02)
        m &= ~((B.x < -0.03) & (B.y < 0.012) & (B.z > 0.475) & (B.z < 0.605))        # torn right knee
        m &= ~((B.x > 0) & (B.z < 0.335))                                             # left leg torn off higher
        K.shell("leggings", m, "as_legging1", off=L2, smooth=3, wrinkle=0.003, chew=2, chew_p=0.4, keep=lambda co: co.z > 0.86)
    with K.slot("boots"):
        K.boots("shoes", "as_shoe1", top=0.165, off=L3, cls="cloth", toe_h=0.044)
        K.leg_band("foot_wraps", "as_rag", 0.125, 0.30, off=L3 + 0.002, cls="cloth", wrinkle=0.0022, wfreq=70.0, chew=1)


# ---------------------------------------------------------------------------------------------- t2 acolyte
def t2(K):
    _mats()
    c, B = K.c, K.B
    K.opts["helm"] = dict(force_hide=("hair",))
    with K.slot("helm"):
        w = 0.040 + 0.024 * sstep(B.z, 1.565, 1.615)
        face = m_head(B) & (B.y < -0.035) & (B.z > 1.548) & (B.z < 1.733) & (np.abs(B.x) < w)
        # (the ears need more room than the rest of the head, or their rims come through the linen)
        K.shell("wimple", (B.w("Head", "neck_01") > 0.5) & ~face & (B.z > 1.50), "as_linen", "cloth",
                lambda co: 0.007 + 0.0045 * sstep(np.abs(co[:, 0]), 0.060, 0.076) * sstep(co[:, 2], 1.595, 1.625) * (1 - sstep(co[:, 2], 1.705, 1.73)),
                smooth=2, wrinkle=0.0012, wfreq=42.0)
        _yoke(K, "wimple_bib", "as_linen", r=0.174, off=0.019, thick=0.004)
        K.headband("band", "as_band2", z=1.737, width=0.030, tails=(0.13, 0.09), off=0.0115)
    with K.slot("chest"):
        top = K.shell("robe_top", K.torso_mask(0.995, 1.505, 0.715), "as_robe2_top", "cloth", _var_off(1.0, 0.012, 0.0062), smooth=3)
        _wrinkle2(K, top)
        sk = _skirt(K, "robe_skirt", "as_robe2", (1.066, 1.01, 0.95, 0.88, 0.78, 0.62, 0.45, 0.29, 0.135),
                    (0.020, 0.030, 0.040, 0.042, 0.044, 0.050, 0.056, 0.062, 0.066), nu=30, nv=12, folds=((7, 0.030, 0.15), (13, 0.011, 0.6)), calf=0.5)
        _band(K, "belt", "belt", 1.072, 0.042, 0.027, buckle="iron")
        # a small book hanging from the belt on the left hip, a satchel on the right
        ba = -0.10
        bp_, bd = _on_skirt(sk, ba, 0.905, lift=0.022)
        cover, pages = g_book(0.105, 0.145, 0.036)
        BK = T(*bp_) @ R('Z', math.degrees(ba) - 90)
        bk = props("book", BK, [(cover, "leather", "tome", dict(bevel=0.003, sharp=40)), (pages, "cloth", "paper", dict(sharp=40)),
                                (g_box(0.010, 0.040, 0.149, (0.030, 0, 0)), "leather", "belt", dict(sharp=40))], "pelvis")
        btop = bp_ + np.array((0, 0, 0.072))
        add("book_strap", g_tube([btop, (btop + _sring(K, 1.072, 0.030, ba)) / 2 + bd * 0.008, _sring(K, 1.072, 0.030, ba)], 1.0, n=6, sx=0.004, sy=0.010,
                                 up=(0, 1, 0)), "leather", "belt", bone="pelvis", sharp=60)
        sa = TAU * 0.625
        sp, sd = _on_skirt(sk, sa, 0.972, lift=0.014)
        SP = T(*sp) @ R('Z', math.degrees(sa) - 90)
        props("satchel", SP, [(g_box(0.080, 0.040, 0.086), "leather", "belt", dict(bevel=0.008, sharp=40)),
                              (g_box(0.085, 0.045, 0.034, (0, 0.002, 0.030)), "leather", "boot", dict(bevel=0.006, sharp=40)),
                              (g_box(0.014, 0.008, 0.060, (0, 0.006, 0.062)), "leather", "belt", dict(sharp=40))], "pelvis")
    with K.slot("gloves"):
        K.gloves("gloves", "as_glove2", cuff=0.705, off=0.0045, cls="cloth")
        K.sleeves("glove_cuffs", "as_glove2", x0=0.650, x1=0.735, off=0.0105, cls="cloth", wrinkle=0.0012, waxis="x", wfreq=110.0)
    with K.slot("pants"):
        K.pants("trousers", "as_hose2", top=1.02, bottom=0.115, wrinkle=0.003)
    with K.slot("boots"):
        K.boots("shoes", "boot", top=0.205, off=L3, toe_h=0.046)
        _leg_straps(K, "shoe_cuffs", "belt", (0.192,), width=0.030, off=L3 + 0.003, thick=0.005)


# ---------------------------------------------------------------------------------------------- t3 astrologer
def t3(K):
    _mats()
    c, B = K.c, K.B
    K.opts["helm"] = dict(force_hide=("hair",))
    with K.slot("helm"):
        K.cap("hat_liner", "as_velvet_top", z_brow=1.722, ears=True, off=0.007, cls="cloth", nape=1.595)
        HRX, HRY = 0.087, 0.099
        HM = T(0.0, 0.008, 1.722) @ R('X', -9)
        _cone_hat("hat", "as_hat", HM, h=0.255, rx=HRX, ry=HRY, lean=0.075)
        roll = [xf(np.array(((HRX + 0.005) * math.cos(a), (HRY + 0.005) * math.sin(a), 0.010)), HM) for a in np.linspace(0, TAU, 20, endpoint=False)]
        add("hat_roll", g_tube(roll, 0.0125, n=7, closed=True, sy=0.85), "cloth", "as_roll", bone="Head", sharp=180)
        props("hat_star", HM @ T(0.0, -(HRY + 0.0195), 0.013) @ R('X', 9), [
            (g_prism(rhomb(0.015, 0.022), 0.005), "metal", "starmetal", dict(bevel=0.0015, sharp=40)),
            (gxf(g_prism(star4(0.0135), 0.004), T(0, -0.0042, 0)), "emis", "star", dict(sharp=20))], "Head")
        vst = [(1.38, 0, 0.0, 0.27, 0.10, 0.150), (1.42, 0, 0.0, 0.27, 0.10, 0.142), (1.48, 0, 0.0, 0.26, 0.10, 0.124), (1.54, 0, 0.0, 0.22, 0.10, 0.112),
               (1.62, 0, 0.0, 0.16, 0.10, 0.105), (1.74, 0, 0.0, 0.13, 0.10, 0.100)]
        veil = drape("veil", -0.074, 0.074, 1.722, 1.415, back_y(vst, 0.013), "cloth", "as_veil", nu=9, nv=9, thick=0.004, out=1.0,
                     folds=((2.5, 0.008, 0.2),), flare=0.55, point=0.03)

        def vw(co):
            d = sstep(1.67 - co[:, 2], 0.0, 0.17)
            return {"Head": 1 - 0.85 * d, "neck_01": 0.30 * d, "spine_03": 0.55 * d}
        w_func(veil, vw)
    with K.slot("chest"):
        top = K.tunic("robe_top", "as_velvet_top", lo=0.995, hi=1.505, sleeve=0.310, off=0.012, wrinkle=0.0018)
        r0 = K.upperarm_r
        sleeve(c, "as_velvet_sleeve", [(0.235, 0.0, 0.0, r0 + 0.008, r0 + 0.004, r0 + 0.004), (0.36, 0.004, 0.0, r0 + 0.006, r0 + 0.002, r0 + 0.002),
                                       (0.50, 0.016, 0.0, 0.080, 0.068, 0.068), (0.648, 0.044, 0.0, 0.120, 0.092, 0.092)],
               n=12, nv=8, name="sleeve_l", cls="cloth", thick=0.006)
        sk = _skirt(K, "robe_skirt", "as_velvet", (1.066, 1.01, 0.95, 0.88, 0.78, 0.62, 0.45, 0.29, 0.125),
                    (0.019, 0.031, 0.045, 0.053, 0.061, 0.073, 0.085, 0.094, 0.100), nu=28, nv=11, folds=((8, 0.036, 0.1), (15, 0.012, 0.5)), calf=0.5)
        # plum stole: round the neck, down both sides of the front, the ends lie on the skirt
        half = [(0.088, -0.13, 1.078), (0.090, -0.15, 1.20), (0.092, -0.16, 1.31), (0.096, -0.12, 1.42), (0.100, -0.02, 1.52), (0.070, 0.09, 1.50)]
        guide = half + [(0.0, 0.12, 1.485)] + [(-x, y, z) for (x, y, z) in half[::-1]]
        K.strap("stole_top", "as_stole3", guide, width=0.060, thick=0.005, off=0.0195, cls="cloth", closed=False, only=BODY_BONES)
        for s in (1, -1):
            tl = _hang_on("stole_%s" % ("l" if s > 0 else "r"), sk, "as_stole3_tail", s * 0.058, s * 0.118, 1.060, 0.615, lift=0.008, thick=0.005,
                          nu=5, nv=11, point=0.045)
            for i, (zz, shp) in enumerate(((0.93, star4(0.015)), (0.80, rhomb(0.007, 0.014)))):
                decal("stole_star_%d_%d" % (s, i), shp, tl, (s * 0.088, -0.6, zz), "emis", "star", depth=0.003, lift=0.0025, ray=(0, 1, 0))
        cp = _cape(K, "capelet", "as_capelet", zmin=1.365, arm_x=0.295, off=0.036, thick=0.006, slope=0.45, back_drop=0.03, arm_z=1.436)
        _edge_trim(K, "capelet_trim", cp, "silver", r=0.0048, n=4, pick=lambda L: L[:, 2].mean() < 1.50)
        props("capelet_clasp", frame((0.0, -0.137, 1.418), (0, -1, 0.22), (0, 0, 1)), [
            (g_lathe([(0.0, 0.007), (0.012, 0.006), (0.019, 0.001), (0.019, -0.003), (0.0, -0.003)], n=10), "metal", "silver", dict(sharp=40)),
            (gxf(g_prism(star4(0.012), 0.004), T(0, 0, 0.008) @ R('X', 90)), "metal", "starmetal", dict(sharp=20))], "spine_03")
        _band(K, "girdle", "as_girdle", 1.074, 0.050, 0.0265, cls="cloth", thick=0.007)
        # silver chain slung low on the hips (it lies on the skirt), a star charm hanging from its low point
        A_LO = 4.05
        P = np.array([_on_skirt(sk, a, 0.998 - 0.026 * math.cos(a - A_LO), lift=0.0115)[0] for a in np.linspace(0, TAU, 40, endpoint=False)])
        for _ in range(2):
            P = (np.roll(P, 1, 0) + 2 * P + np.roll(P, -1, 0)) / 4
        chn = add("chain", g_chain(list(P) + [P[0]], link=0.038, wire=0.0038, seg=4), "metal", "silver", sharp=60)
        tail = [_on_skirt(sk, A_LO, z, lift=0.012)[0] for z in (0.968, 0.930, 0.890, 0.852)]
        d = _on_skirt(sk, A_LO, 0.83, lift=0.012)[1]
        chn2 = add("chain_tail", g_chain(tail, link=0.026, wire=0.003, seg=4), "metal", "silver", sharp=60)
        chm = add("chain_charm", gxf(g_prism(star4(0.022), 0.004), frame(_on_skirt(sk, A_LO, 0.828, lift=0.012)[0], (0, 0, 1), -d)), "metal", "silver", sharp=20)
        for o in (chn, chn2, chm):
            w_transfer(o, sk, smooth=1)
    with K.slot("gloves"):
        K.gloves("gloves", "as_glove3", cuff=0.545, off=lambda co: 0.0030 + 0.0015 * sstep(np.abs(co[:, 0]), 0.69, 0.745), cls="cloth")
        K.limb_straps("glove_rings", "silver", "lowerarm", (0.570, 0.695), width=0.011, off=0.0075, cls="metal", thick=0.004)
    with K.slot("pants"):
        K.pants("leggings", "as_legging3", top=1.02, bottom=0.115, wrinkle=0.002)
        _leg_straps(K, "garters", "as_broc", (0.452,), width=0.034, off=L2 + 0.003, cls="cloth", thick=0.004)
    with K.slot("boots"):
        K.boots("boots", "as_boot3", top=0.40, off=L3, toe_h=0.046)
        _leg_straps(K, "boot_cuffs", "as_boot3", (0.378,), width=0.046, off=L3 + 0.0035, thick=0.005)
        for o in _leg_straps(K, "boot_straps", "belt", (0.180, 0.292), width=0.016, off=L3 + 0.005):
            apply_mods(o)
            cen = get_co(o).mean(0)
            s = 1.0 if cen[0] > 0 else -1.0
            decal(o.name + "_buckle", bar(0.011, 0.0095), o, (cen[0] + s * 0.3, cen[1] - 0.12, cen[2]), "metal", "silver", depth=0.005, lift=0.003,
                  out=(s, -0.4, 0))


# ---------------------------------------------------------------------------------------------- t4x: NEW helm + gloves
def t4x(K):
    c, B = K.c, K.B
    with K.slot("helm"):
        hair = c.hairs[0]
        bp = [(0.150 * math.sin(a), -0.020 - 0.165 * math.cos(a), 1.752 + 0.022 * abs(math.sin(a))) for a in np.radians(np.linspace(-100, 100, 23))]
        P = _wrap_on(hair, bp, 0.0045)
        for _ in range(2):
            P[1:-1] = (P[:-2] + 2 * P[1:-1] + P[2:]) / 4
        add("diadem", g_tube(P, 0.0062, n=6, sx=1.3, sy=0.6, up=(0, 0, 1)), "metal", "starmetal", bone="Head", sharp=50)
        fc = P[len(P) // 2]
        log("diadem front", np.round(fc, 3), "ends", np.round(P[0], 3), np.round(P[-1], 3))
        props("diadem_front", T(fc[0], fc[1] - 0.006, fc[2]) @ R('X', -10), [
            (gxf(g_prism(crescent(0.025, 0.020, 0.010, 9), 0.0045), T(0, 0, 0.036)), "metal", "silver", dict(bevel=0.001, sharp=35)),
            (gxf(g_prism(rhomb(0.010, 0.014), 0.006), T(0, 0, 0.002)), "metal", "starmetal", dict(bevel=0.001, sharp=40)),
            (gxf(g_prism(rhomb(0.012, 0.021), 0.005), T(0, 0.0, -0.028) @ R('X', 16)), "metal", "starmetal", dict(bevel=0.001, sharp=40)),
            (gxf(g_prism(star4(0.0125), 0.004), T(0, -0.004, -0.028) @ R('X', 16)), "emis", "star", dict(sharp=20))], "Head")
        for i in (len(P) // 2 - 5, len(P) // 2 + 5):
            q = P[i]
            d = q - np.array((0.0, -0.012, 1.69))
            add("diadem_stud_%d" % i, gxf(g_gem(0.0065, 0.012, 4, 0.6), frame(q + d / np.linalg.norm(d) * 0.003, d)), "metal", "silver", bone="Head", sharp=30)
    with K.slot("gloves"):
        K.limb_straps("bracelet", "starmetal", "lowerarm", (0.700,), width=0.026, off=0.0085, cls="metal", thick=0.005)
        K.limb_straps("bracelet_rim", "silver", "lowerarm", (0.6855, 0.7145), width=0.0055, off=0.0092, cls="metal", thick=0.0052)
        # finger-loop chain: bracelet -> back of the hand -> ring on the middle finger
        fm = (B.w("middle_01") > 0.5) & (B.x > 0) & (np.abs(B.x - 0.878) < 0.014)
        fy, fz = float(B.y[fm].min() + B.y[fm].max()) / 2, float(B.z[fm].min() + B.z[fm].max()) / 2
        fry, frz = float(B.y[fm].max() - B.y[fm].min()) / 2 + 0.0022, float(B.z[fm].max() - B.z[fm].min()) / 2 + 0.0022
        log("middle finger ring: centre", round(fy, 4), round(fz, 4), "radii", round(fry, 4), round(frz, 4), "verts", int(fm.sum()))
        gp = list(wrap_path(c, [(0.716, 0.068, 1.50), (0.745, 0.067, 1.50), (0.775, 0.066, 1.50), (0.805, 0.065, 1.50), (0.835, 0.063, 1.50),
                                (0.860, 0.061, 1.50)], 0.0034)) + [np.array((0.878, fy, fz + frz + 0.001))]
        ch = add("hand_chain_l", g_chain(gp, link=0.0125, wire=0.0016, seg=5), "metal", "silver", sharp=60)
        w_transfer(ch, c.ref, smooth=0)
        rp = [(0.878, fy + fry * math.cos(a), fz + frz * math.sin(a)) for a in np.linspace(0, TAU, 12, endpoint=False)]
        rg = add("finger_ring_l", g_tube(rp, 0.0021, n=5, closed=True), "metal", "silver", bone="middle_01_l", sharp=60)
        st = add("hand_star_l", gxf(g_prism(star4(0.0095), 0.003), T(*(gp[3] + np.array((0.0, 0.0, 0.0015)))) @ R('X', 90)), "metal", "starmetal", sharp=20)
        w_transfer(st, c.ref, smooth=0)
        for o in (ch, rg, st):
            mirrored(o)
        # a rune on top of each bracelet (placed with a ray from above)
        for s in (1, -1):
            host = next(o for o in mk.PARTS if o.name == "bracelet_0_%s" % ("l" if s > 0 else "r"))
            decal("bracelet_rune_%s" % ("l" if s > 0 else "r"), rhomb(0.0055, 0.0105), host, (s * 0.700, 0.068, 1.60), "emis", "star", depth=0.003,
                  up=(1, 0, 0), ray=(0, 0, -1), lift=0.002, bone="lowerarm_%s" % ("l" if s > 0 else "r"))
