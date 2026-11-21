"""Pactbinder armour looks t1..t3 (t4 = today's hero, split by split_t4.py).

Identity: warlock bound by contract -- near-black oxblood, blood red, black iron chains and shackles, brass, parchment
and wax seals, tiny red emissives; the LEFT arm is the pact arm. Hero materials reused: boot, belt, hose, paper, wax,
iron, brass, darksteel, pact.
  t1  escaped debtor: ragged pointed sackcloth hood with a torn shoulder cape, prisoner's tunic with one sleeve ripped
      off, rusty iron collar with a broken chain, rope belt, wrist shackles over rag wraps (the left wrap scorched, one
      faint brand on the hand), trousers torn at the knee, foot wraps with one ankle shackle.
  t2  clerk of contracts: wide-brimmed travelling hat over a linen coif, plain long buttoned wool coat with a low
      collar, linen cuffs (the right one ink-stained), belt with a sealed scroll case and a pouch, leather gloves,
      plain trousers, calf boots.
  t3  magistrate of the pact: dark-steel brow mask with two short swept-back horns under a tall flaring collar; long
      oxblood velvet coat open over a brocade waistcoat, studded half-mantle on the left shoulder with chains draped
      across the chest, parchment writs with wax seals at the belt; clawed dark-steel gauntlet on the left hand and a
      fine glove on the right, fine trousers with a thigh belt, tall boots with iron toe and heel.
"""
from gearkit import *

GOLD = (0.26, 0.17, 0.06)       # old brass thread (brocade bands)
BODY_BONES = ["pelvis", "spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r", "neck_01"]


def _mats():
    # ---- t1
    mats.cloth("pb_hood", base=(0.072, 0.060, 0.048), var=0.45, ash=0.6, ash_h=0.9, folds=0.6, worn=0.92, scan="burlap", tight=False)
    mats.cloth("pb_sack", base=(0.130, 0.108, 0.082), var=0.45, ash=0.65, ash_h=0.6, folds=0.6, worn=0.92, scan="burlap", tight=True)
    mats.cloth("pb_sack_s", base=(0.130, 0.108, 0.082), var=0.45, ash=0.7, ash_h=0.5, folds=0.9, worn=0.92, scan="burlap", tight=False)
    mats.cloth("pb_rope", base=(0.20, 0.16, 0.10), var=0.3, ash=0.4, folds=0.0, scan="rope", hem=False)
    mats.cloth("pb_rag", base=(0.150, 0.130, 0.105), var=0.4, ash=0.6, folds=0.4, weave=1.7, worn=0.9, tight=True)
    mats.cloth("pb_scorch", base=(0.030, 0.025, 0.023), var=0.5, ash=0.7, ash_h=0.6, folds=0.4, weave=1.7, worn=0.95, tight=True)
    mats.metal("pb_rust", base=(0.135, 0.108, 0.090), rough=(0.55, 0.88), rust_amt=0.8, pattern="hammered", ash=0.3, soot=0.25)
    mats.cloth("pb_trouser1", base=(0.050, 0.042, 0.037), var=0.5, ash=0.6, folds=0.5, worn=0.9, scan="wool", tight=True)
    # ---- t2
    mats.cloth("pb_hat", base=(0.026, 0.021, 0.020), var=0.3, ash=0.35, ash_h=0.9, folds=0.0, worn=0.5, scan="wool", hem=False)
    mats.cloth("pb_coif2", base=(0.250, 0.225, 0.180), var=0.3, ash=0.4, folds=0.4, worn=0.5, scan="linen", tight=True)
    mats.cloth("pb_coat2_top", base=(0.052, 0.022, 0.020), var=0.3, ash=0.3, ash_h=0.4, folds=0.5, worn=0.5, scan="twill", tight=True)
    mats.cloth("pb_coat2", base=(0.052, 0.022, 0.020), var=0.3, ash=0.5, ash_h=0.45, folds=0.9, worn=0.5, scan="twill", tight=False)
    mats.cloth("pb_cuff", base=(0.290, 0.260, 0.200), var=0.3, ash=0.3, folds=0.4, worn=0.5, scan="linen", tight=True)
    mats.cloth("pb_ink", base=(0.030, 0.030, 0.046), var=0.6, ash=0.2, folds=0.4, worn=0.7, scan="linen", tight=True)
    mats.leather("pb_glove2", base=(0.046, 0.028, 0.018), worn=0.55, ash=0.3, scan="leather_b")
    # ---- t3
    mats.cloth("pb_velvet_top", base=(0.046, 0.011, 0.013), var=0.3, ash=0.15, folds=0.4, worn=0.25, scan="velvet", tight=True)
    mats.cloth("pb_velvet", base=(0.046, 0.011, 0.013), var=0.3, ash=0.35, ash_h=0.35, folds=0.9, worn=0.25, scan="velvet", tight=False,
               bands=[(0.0, 0.055, GOLD), (0.072, 0.084, (0.012, 0.010, 0.010))])
    mats.cloth("pb_brocade", base=(0.135, 0.030, 0.020), var=0.4, ash=0.15, folds=0.3, worn=0.3, scan="brocade", tight=True)
    mats.leather("pb_collar", base=(0.030, 0.013, 0.012), edge=(0.14, 0.06, 0.04), worn=0.3, ash=0.2, scan="leather")
    mats.leather("pb_glove3", base=(0.050, 0.014, 0.012), worn=0.3, ash=0.15, scan="leather_b")
    mats.cloth("pb_trouser3", base=(0.026, 0.022, 0.026), var=0.3, ash=0.3, folds=0.5, worn=0.3, scan="twill", tight=True)


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


def _cape(K, name, src, zmin=1.38, arm_x=0.27, off=0.030, thick=0.006, chew=0, slope=0.5, back_drop=0.0, z_top=1.545, cls="cloth", extra=None,
          keep=None):
    """K.capelet with a plain (not 'even') solidify, so a torn edge cannot shoot spikes. extra = further mask"""
    B = K.B
    arms = (B.w("upperarm", "clavicle") > 0.5) & (np.abs(B.x) < arm_x) & (B.z > 1.40)
    zlim = zmin - slope * B.y - back_drop * sstep(B.y, 0.0, 0.10)
    core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zlim) & (B.z < z_top) & (np.abs(B.x) < 0.20)
    mm = (core | arms) & (B.w(*BONES_HAND, "lowerarm") < 0.3)
    if extra is not None:
        mm &= extra
    o = shell(B, mm, off, name, cls, src, smooth=4, sharp=180)
    if chew:
        chew_edge(o, rounds=int(chew), p=0.36, seed=K.rs(), keep=keep or (lambda co: co.z > 1.49 and abs(co.x) < 0.13))
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


def _edge_trim(K, name, o, src, r=0.005, pick=None, lift=0.004, cls="metal", n=5, smooth=2, bone=None):
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
        if bone:
            w_rigid(t, bone)
        else:
            w_transfer(t, K.c.ref, smooth=1)
        out.append(t)
    return out


def _hang_on(name, host, src, x0, x1, z_top, z_bot, lift=0.008, thick=0.004, nu=6, nv=12, point=0.0, taper=0.0, front=True, cls="cloth"):
    """strip lying on a lofted garment (skirt): its surface is found with rays, its weights are the host's, so it
    follows every fold and moves exactly like the garment"""
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


def _sring(K, z, gap, a, p=2.2):
    """point on the superellipse the skirts are lofted through, at height z and angle a (0 = left, pi/2 = back)"""
    rg = K.ring(z, gap)
    e = 2.0 / p
    ca, sa = math.cos(a), math.sin(a)
    return np.array((rg[2] * math.copysign(abs(ca) ** e, ca), rg[1] + (rg[4] if sa > 0 else rg[3]) * math.copysign(abs(sa) ** e, sa), z))


def _skirt(K, name, src, zs, gaps, cls="cloth", nu=30, nv=10, thick=0.005, folds=((7, 0.028, 0.15), (13, 0.010, 0.6)), tails=0, tail_depth=0.16,
           ragged=0.0, u0=0.0, u1=1.0, close=True, calf=0.5, **kw):
    """K.skirt with its stations and gaps given one by one (K.skirt's linear gap is too tight round the hips: the
    fronts of the thighs and the seat come through as soon as pants are worn)"""
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
    """belt that follows the waist (two measured rings instead of K.belt's straight hoop)"""
    r0, r1 = K.ring(z - width / 2, gap), K.ring(z + width / 2, gap)
    st = [(z - width / 2, 0, r0[1], r0[2], r0[3], r0[4]), (z + width / 2, 0, r1[1], r1[2], r1[3], r1[4])]
    o = add(name, g_stations(st, nu=28, nv=2, p=2.2), cls, src, solidify=thick, sharp=50)
    w_transfer(o, K.c.ref, smooth=2, only=["pelvis", "spine_01", "spine_02"])
    if buckle:
        ob = add(name + "_buckle", gxf(g_box(0.058, 0.016, width * 1.25), T(0, (r0[1] - r0[3] + r1[1] - r1[3]) / 2 - thick - 0.004, z)), "metal", buckle,
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


def _front_y(K, x, z, default=-0.10):
    """y of the front of the body at (x, z)"""
    ok, loc, nrm, fi = K.c.ref.ray_cast(Vector((float(x), -0.8, float(z))), Vector((0.0, 1.0, 0.0)))
    return float(loc[1]) if ok else default


def _leg_straps(K, name, src, at, width=0.02, off=L3 + 0.004, cls="leather", thick=0.005, sides="lr"):
    """K.limb_straps for the legs, safe at heights where the body mesh has no edge loop"""
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


def _sub_piece(o, name, sel, push, cls, src):
    """part of an existing shell (boot toe, heel ...) pushed out along its normals: a cap that fits it exactly"""
    apply_mods(o)
    me = o.data
    co = get_co(o)
    n = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get('normal', n)
    n = n.reshape(-1, 3)
    m = sel(co)
    faces = [tuple(p.vertices) for p in me.polygons if all(m[v] for v in p.vertices)]
    used = sorted({i for f in faces for i in f})
    rm = {u: k for k, u in enumerate(used)}
    V = co[used] + n[used] * push
    q = add(name, geo(V, [tuple(rm[i] for i in f) for f in faces]), cls, src, recalc=False, sharp=60)
    w_transfer(q, o, smooth=0)
    return q


def _shackle(r, w=0.03, t=0.008, n=12, sx=1.0):
    return g_lathe([(r, -w / 2), (r + t, -w / 2), (r + t, w / 2), (r, w / 2), (r, -w / 2)], n=n, sx=sx)


def _seal(zc, r=0.025):
    return [(gxf(g_lathe([(0.0, 0.008), (0.013, 0.006), (0.017, 0.0), (0.0, -0.002)], n=7), T(0, -r - 0.004, zc) @ R('X', 90)), "cloth", "wax", dict(sharp=50)),
            (g_tube(ring_path(r + 0.004, 10, "xy", (0, 0, zc)), 1.0, n=4, closed=True, sx=0.003, sy=0.008), "leather", "belt", dict(sharp=60))]


def _disc(r, n=8):
    return [(r * math.cos(TAU * i / n), r * math.sin(TAU * i / n)) for i in range(n)]


# ---------------------------------------------------------------------------------------------- t1 escaped debtor
def t1(K):
    _mats()
    c, B = K.c, K.B
    K.opts["helm"] = dict(force_hide=("hair",))
    with K.slot("helm"):
        st = [(1.515, 0.024, 0.125, 0.092, 0.122), (1.56, 0.004, 0.100, 0.112, 0.108), (1.62, -0.012, 0.106, 0.128, 0.122),
              (1.69, -0.012, 0.114, 0.138, 0.136), (1.75, -0.006, 0.107, 0.132, 0.142), (1.80, 0.008, 0.088, 0.110, 0.140),
              (1.84, 0.034, 0.060, 0.076, 0.120), (1.872, 0.074, 0.030, 0.038, 0.076), (1.892, 0.125, 0.0, 0.0, 0.0)]
        cowl(c, "pb_hood", st, name="hood", thick=0.007, chin=1.565, brow=1.748, open_deg=58.0, peak=0.020, nu=28, nv=18)
        _cape(K, "hood_cape", "pb_hood", zmin=1.445, arm_x=0.275, off=0.030, thick=0.006, chew=2, slope=0.45, back_drop=0.04, z_top=1.565)
    with K.slot("chest"):
        m = K.torso_mask(1.0, 1.52, 0.33, vneck=0.11, vwidth=0.05) & (B.x < 0.232)               # the left sleeve is ripped off
        K.shell("tunic", m, "pb_sack", "cloth", 0.012, smooth=3, hem=(1.0, 1.07, 0.0165), wrinkle=0.0026, wfreq=24.0, chew=2, chew_p=0.35,
                keep=lambda co: co.z > 1.10 and -0.25 < co.x < 0.17)
        _skirt(K, "tunic_tail", "pb_sack_s", (1.045, 1.0, 0.95, 0.88, 0.78, 0.69), (0.021, 0.028, 0.036, 0.040, 0.043, 0.045), nu=28, nv=8, tails=6,
               tail_depth=0.12, ragged=0.03, folds=((6, 0.03, 0.1), (11, 0.012, 0.5)), calf=0.0)
        K.rope("rope_belt", "pb_rope", z=1.06, gap=0.028, r=0.0085, cls="cloth", turns=2)
        # rusty iron collar, a hasp at the throat and a few links of the chain he broke
        CY, CZ, SL = 0.018, 1.560, 0.28

        def tilt(U, V, x, y, z):
            return x, y, z + SL * (y - CY)
        gcol = g_stations([(CZ - 0.016, 0, CY, 0.078, 0.086, 0.082), (CZ + 0.016, 0, CY, 0.078, 0.086, 0.082)], nu=20, nv=2, p=2.0, mod=tilt)
        col = add("iron_collar", gcol, "metal", "pb_rust", solidify=0.007, sharp=40, bevel=0.0015)
        fy, fz = CY - 0.086 - 0.007, CZ - SL * 0.086
        hasp = add("collar_hasp", gjoin(g_box(0.024, 0.012, 0.036, (0.0, fy - 0.004, fz)),
                                        gxf(g_ring(0.011, 0.0032, n=8, m=4), T(0.0, fy - 0.008, fz - 0.024) @ R('Y', 90))), "metal", "pb_rust", bevel=0.002,
                   sharp=40)
        path = [np.array((0.0, fy - 0.008, fz - 0.034))]
        for i, z in enumerate((1.475, 1.435, 1.395, 1.355)):
            x = 0.004 + 0.004 * i
            path.append(np.array((x, min(_front_y(K, x, z) - 0.012 - 0.016, path[-1][1] + 0.004), z)))
        chn = add("collar_chain", g_chain(path, link=0.034, wire=0.0048, seg=5), "metal", "pb_rust", sharp=60)
        for o in (col, hasp):
            w_func(o, lambda co: {"neck_01": 0.45, "spine_03": 0.55})
        w_func(chn, lambda co: {"neck_01": 0.45 * sstep(co[:, 2], 1.47, 1.52), "spine_03": 1 - 0.45 * sstep(co[:, 2], 1.47, 1.52)})
    with K.slot("gloves"):
        K.sleeves("wrap_l", "pb_scorch", x0=0.500, x1=0.728, off=L1, sides="l", wrinkle=0.002, waxis="x", wfreq=95.0, chew=1)
        K.sleeves("wrap_r", "pb_rag", x0=0.575, x1=0.728, off=L1, sides="r", wrinkle=0.002, waxis="x", wfreq=95.0, chew=1)
        hm = (B.w("hand") > 0.55) & (np.abs(B.x) > 0.745)
        pl = K.shell("palm_l", hm & (B.x > 0), "pb_scorch", "cloth", 0.004, smooth=1)
        K.shell("palm_r", hm & (B.x < 0), "pb_rag", "cloth", 0.004, smooth=1)
        sh = add("shackle_l", gjoin(gxf(_shackle(0.0400, 0.034, 0.008), frame((0.700, 0.0705, 1.434), (1, 0, 0), (0, 1, 0))),
                                    g_box(0.022, 0.026, 0.020, (0.700, 0.0705, 1.434 - 0.054)),
                                    gxf(g_ring(0.010, 0.003, n=8, m=4), T(0.700, 0.0705, 1.434 - 0.070))),
                 "metal", "pb_rust", bone="lowerarm_l", sharp=40, bevel=0.0015)
        mirrored(sh)
        decal("hand_brand", chevron(0.012, 0.011, 0.005), pl, (0.800, 0.066, 1.60), "emis", "pact", depth=0.003, up=(1, 0, 0), ray=(0, 0, -1), lift=0.002)
    with K.slot("pants"):
        m = m_legs(B, 0.27, 1.02)
        m &= ~((B.x > 0.03) & (B.y < 0.005) & (B.z > 0.465) & (B.z < 0.605))         # torn left knee
        m &= ~((B.x < 0) & (B.z < 0.37))                                              # right leg torn off higher
        K.shell("trousers", m, "pb_trouser1", off=L2, smooth=3, wrinkle=0.003, chew=2, chew_p=0.4, keep=lambda co: co.z > 0.86)
    with K.slot("boots"):
        K.boots("foot_wraps", "pb_rag", top=0.17, off=L3, cls="cloth", toe_h=0.046)
        K.leg_band("shin_wraps", "pb_rag", 0.125, 0.335, off=L3 + 0.002, cls="cloth", wrinkle=0.0022, wfreq=70.0, chew=1)
        # one ankle shackle (right leg) with the stump of its chain
        lg = (B.x < 0) & (np.abs(B.z - 0.20) < 0.03) & (B.w(*BONES_LEG, *BONES_FOOT) > 0.5)
        acx, acy = float(B.x[lg].min() + B.x[lg].max()) / 2, float(B.y[lg].min() + B.y[lg].max()) / 2
        arx, ary = float(B.x[lg].max() - B.x[lg].min()) / 2 + 0.022, float(B.y[lg].max() - B.y[lg].min()) / 2 + 0.022
        log("ankle shackle: centre", round(acx, 3), round(acy, 3), "radii", round(arx, 3), round(ary, 3))
        add("ankle_shackle", gjoin(gxf(_shackle(ary, 0.036, 0.009, 14, arx / ary), T(acx, acy, 0.200)),
                                   g_box(0.024, 0.022, 0.030, (acx - arx - 0.012, acy, 0.200))), "metal", "pb_rust", bone="calf_r", sharp=40, bevel=0.0015)
        add("ankle_chain", g_chain([(acx - arx - 0.022, acy, 0.186), (acx - arx - 0.026, acy + 0.004, 0.140), (acx - arx - 0.024, acy + 0.010, 0.092)],
                                   link=0.036, wire=0.005, seg=5), "metal", "pb_rust", bone="calf_r", sharp=60)


# ---------------------------------------------------------------------------------------------- t2 clerk of contracts
def t2(K):
    _mats()
    c, B = K.c, K.B
    K.opts["helm"] = dict(force_hide=("hair",))
    with K.slot("helm"):
        K.cap("coif", "pb_coif2", z_brow=1.722, ears=True, off=0.008, cls="cloth", nape=1.60, wrinkle=0.0012, wfreq=45.0)
        K.strap("coif_tie", "pb_coif2", [(0.092, -0.01, 1.69), (0.084, -0.04, 1.62), (0.045, -0.085, 1.565), (0.0, -0.098, 1.553),
                                         (-0.045, -0.085, 1.565), (-0.084, -0.04, 1.62), (-0.092, -0.01, 1.69)],
                width=0.014, thick=0.004, off=0.006, cls="cloth", closed=False, only=["Head"])
        HM = T(0.0, -0.004, 1.760) @ R('X', -5)
        hat = add("hat", gxf(hat_wide(brim=0.200, crown=0.106, height=0.104, n=22, wave=0.016, droop=0.030), HM), "cloth", "pb_hat", bone="Head", sharp=180)
        solid(hat, 0.006)
        add("hat_band", gxf(g_lathe([(0.110, 0.008), (0.112, 0.020), (0.108, 0.032)], n=22, sx=0.94, sy=1.06), HM), "leather", "belt", bone="Head", sharp=60,
            solidify=0.003)
    with K.slot("chest"):
        coat = K.shell("coat", K.torso_mask(1.02, 1.525, 0.705, vneck=0.07, vwidth=0.045), "pb_coat2_top", "cloth",
                       _var_off(1.02, 0.016, 0.0068, x_a=0.30, x_b=0.50), smooth=3)
        _wrinkle2(K, coat, 0.002, 0.0014)
        # linen shirt cuffs showing at the wrists: the writing hand's is black with ink
        K.limb_straps("cuff", "pb_cuff", "lowerarm", (0.690,), width=0.036, off=0.0078, cls="cloth", sides="l", thick=0.003)
        K.limb_straps("cuff", "pb_ink", "lowerarm", (0.690,), width=0.036, off=0.0078, cls="cloth", sides="r", thick=0.003)
        sk = _skirt(K, "coat_skirt", "pb_coat2", (1.055, 1.0, 0.95, 0.88, 0.78, 0.62, 0.40), (0.024, 0.032, 0.040, 0.044, 0.048, 0.052, 0.056), nu=30, nv=10,
                    folds=((5, 0.03, 0.1), (9, 0.012, 0.5)), calf=0.35, u0=0.75 + 0.035, u1=1.75 - 0.035, close=False)
        _band(K, "belt", "belt", 1.072, 0.050, 0.032, buckle="iron")
        K.rivets("buttons", "brass", [(0.012, -0.20, z) for z in (1.135, 1.205, 1.275, 1.345, 1.415)], r=0.0075, host=coat)
        gcol = g_stations([(1.485, 0, 0.032, 0.128, 0.108, 0.118), (1.53, 0, 0.022, 0.106, 0.100, 0.106), (1.568, 0, 0.017, 0.113, 0.105, 0.113)],
                          nu=20, nv=4, close=False, u0=0.86, u1=1.64)
        col = add("collar", gcol, "cloth", "pb_coat2_top", solidify=0.006, sharp=180, double_sided=True)
        w_func(col, lambda co: {"neck_01": sstep(co[:, 2], 1.50, 1.57) * 0.6, "spine_03": 1 - sstep(co[:, 2], 1.50, 1.57) * 0.6})
        # one sealed scroll case on the left hip, an ink pouch on the right
        a = TAU * 0.870
        p, d = _on_skirt(sk, a, 0.935, lift=0.034)
        SA = T(*p) @ R('Z', math.degrees(a) + 90) @ R('X', -7) @ R('Y', -8)
        tube, caps = g_scroll(0.20, 0.024)
        sa = props("scroll", SA, [(tube, "cloth", "paper", dict(sharp=60)), (caps, "metal", "brass", dict(sharp=50))] + _seal(0.02, 0.024), "pelvis")
        top = xf(np.array((0.0, 0.0, 0.085)), SA)
        sa.append(add("scroll_strap", g_tube([top, (top + _sring(K, 1.072, 0.042, a)) / 2 + d * 0.006, _sring(K, 1.072, 0.042, a)], 1.0, n=6,
                                             sx=0.004, sy=0.010, up=(0, 1, 0)), "leather", "belt", sharp=60))
        for o in sa:
            w_func(o, lambda co: {"pelvis": 0.6, "thigh_l": 0.4})
        pa = TAU * 0.615
        pp, pd = _on_skirt(sk, pa, 0.985, lift=0.014)
        props("pouch", T(*pp) @ R('Z', math.degrees(pa) - 90), [(g_box(0.078, 0.040, 0.084), "leather", "belt", dict(bevel=0.008, sharp=40)),
                                                                 (g_box(0.083, 0.045, 0.032, (0, 0.002, 0.030)), "leather", "boot", dict(bevel=0.006, sharp=40)),
                                                                 (g_box(0.014, 0.008, 0.056, (0, 0.006, 0.060)), "leather", "belt", dict(sharp=40))], "pelvis")
    with K.slot("gloves"):
        K.gloves("gloves", "pb_glove2", cuff=0.665, off=0.0055)
        K.limb_straps("glove_cuffs", "pb_glove2", "lowerarm", (0.668,), width=0.056, off=0.0125, cls="leather", thick=0.004)
    with K.slot("pants"):
        K.pants("trousers", "hose", top=1.02, bottom=0.12, wrinkle=0.003)
    with K.slot("boots"):
        K.boots("boots", "boot", top=0.37, off=L3, cuff=0.005, toe_h=0.048)


# ---------------------------------------------------------------------------------------------- t3 magistrate of the pact
def t3(K):
    _mats()
    c, B = K.c, K.B
    lm = face_landmarks(c)
    with K.slot("helm"):
        # dark-steel brow mask (eye slits, short nasal), brass rim, two short swept-back horns
        ax = np.abs(B.x)
        m = m_head(B) & (B.y < -0.048) & (B.z < 1.744) & (ax < 0.084) & ((B.z > lm["ez"] - 0.021) | ((ax < 0.016) & (B.z > lm["nz"] - 0.006)))
        m &= ~((np.abs(ax - lm["ex"]) < 0.017) & (np.abs(B.z - lm["ez"]) < 0.0085))
        mask = K.shell("brow_mask", m, "darksteel", "metal", 0.0075, smooth=2)
        w_rigid(mask, "Head")
        _edge_trim(K, "brow_rim", mask, "brass", r=0.0032, n=4, lift=0.002, pick=lambda L: len(L) > 24, bone="Head")
        for s in (1, -1):
            add("horn_%s" % ("l" if s > 0 else "r"), gxf(g_tooth(0.082, 0.0135, 0.036, 6), T(s * 0.079, -0.040, 1.728) @ R('Y', 24 * s) @ R('Z', 90)),
                "metal", "iron", bone="Head", sharp=40)
        # tall stiff collar standing on the shoulders, open at the throat, highest behind the head
        U0, U1 = 0.885, 1.615

        def collar_fn(U, V):
            a = TAU * (U0 + (U1 - U0) * U)
            ca, sa = np.cos(a), np.sin(a)
            back = np.clip(0.5 + 0.5 * sa, 0, 1)
            z0 = 1.470 + 0.036 * np.abs(ca) ** 1.2
            z1 = 1.595 + 0.115 * back ** 1.3
            k = 1.0 + 0.24 * V ** 1.5
            ry = np.where(sa < 0, 0.120, 0.136)
            return np.stack([0.140 * k * ca, 0.030 + ry * k * sa, z0 + (z1 - z0) * V], axis=-1)
        gc = g_grid(collar_fn, 25, 6, close_u=False)
        col = add("tall_collar", gc, "leather", "pb_collar", sharp=180, double_sided=True)
        solid(col, 0.007)
        ct = add("tall_collar_trim", g_tube(rim_of(gc, 25), 0.0052, n=5, closed=False), "metal", "brass", sharp=60)
        for o in (col, ct):
            w_func(o, lambda co: {"neck_01": 0.22 * sstep(co[:, 2], 1.52, 1.66), "spine_03": 1 - 0.22 * sstep(co[:, 2], 1.52, 1.66)})
    with K.slot("chest"):
        vest = K.tunic("waistcoat", "pb_brocade", lo=1.0, hi=1.52, sleeve=0.215, off=0.0095, cls="cloth")
        coat = K.shell("coat", K.torso_mask(1.03, 1.525, 0.660, vneck=0.42, vwidth=0.075), "pb_velvet_top", "cloth",
                       _var_off(1.03, 0.0175, 0.0068, x_a=0.30, x_b=0.50), smooth=3)
        _wrinkle2(K, coat, 0.0018, 0.0013)
        sk = _skirt(K, "coat_skirt", "pb_velvet", (1.058, 1.0, 0.95, 0.88, 0.78, 0.62, 0.45, 0.30), (0.026, 0.034, 0.042, 0.048, 0.052, 0.056, 0.060, 0.062),
                    nu=30, nv=11, folds=((5, 0.032, 0.1), (9, 0.012, 0.5)), calf=0.4, u0=0.75 + 0.045, u1=1.75 - 0.045, close=False)
        _band(K, "belt", "belt", 1.074, 0.056, 0.034, buckle="brass")
        K.rivets("vest_buttons", "brass", [(0.0, -0.20, z) for z in (1.14, 1.20, 1.26, 1.32, 1.38)], r=0.0065, host=vest)
        # writs hung from the belt: parchment strips with wax seals
        for i, (x0, x1, zb) in enumerate(((0.074, 0.106, 0.835), (0.116, 0.150, 0.775), (-0.128, -0.094, 0.805))):
            stp = _hang_on("writ_%d" % i, sk, "paper", x0, x1, 1.048, zb, lift=0.007, thick=0.003, nu=3, nv=7)
            decal("writ_seal_%d" % i, _disc(0.0145), stp, ((x0 + x1) / 2, -0.7, zb + 0.030), "cloth", "wax", depth=0.007, lift=0.003, ray=(0, 1, 0))
        # studded leather half-mantle on the left (pact) shoulder, chains draped across the chest from it
        mant = _cape(K, "half_mantle", "pb_collar", zmin=1.335, arm_x=0.365, off=0.031, thick=0.006, slope=0.5, back_drop=0.03, z_top=1.565,
                     cls="leather", extra=(B.x > 0.028))
        _edge_trim(K, "mantle_rim", mant, "iron", r=0.0048, n=4, pick=lambda L: len(L) > 20)
        boss = (-0.128, _front_y(K, -0.128, 1.472) - 0.0175, 1.472)
        K.rivets("chain_boss", "brass", [boss], r=0.015, host=coat)
        for i, (z0, drop) in enumerate(((1.452, 0.045), (1.420, 0.105))):
            a0 = np.array((0.050, 0.0, z0))
            b0 = np.array((boss[0] + 0.006, 0.0, boss[2] - 0.006))
            pth = []
            for t in np.linspace(0, 1, 12):
                q = a0 + (b0 - a0) * t - np.array((0, 0, drop)) * 4 * t * (1 - t)
                q[1] = _front_y(K, q[0], q[2]) - 0.034
                pth.append(q)
            P = np.array(pth)
            P[1:-1, 1] = (P[:-2, 1] + 2 * P[1:-1, 1] + P[2:, 1]) / 4
            ch = add("chest_chain_%d" % i, g_chain(P, link=0.030, wire=0.0042, seg=5), "metal", "iron", sharp=60)
            w_transfer(ch, c.ref, smooth=2, only=["spine_02", "spine_03", "clavicle_l", "clavicle_r"])
    with K.slot("gloves"):
        K.gloves("gauntlet", "darksteel", cuff=0.700, off=0.006, cls="metal", smooth=1, sides="l")
        K.vambrace("vambrace", "darksteel", x0=0.500, x1=0.722, off=0.016, sides="l")
        K.limb_straps("vambrace_bands", "brass", "lowerarm", (0.540, 0.690), width=0.012, off=0.0172, cls="metal", sides="l", thick=0.004)
        for f in ("index", "middle", "ring", "pinky", "thumb"):
            b = mk.ARM.data.bones[f + "_03_l"]
            h, t = np.array(b.head_local), np.array(b.tail_local)
            d = (t - h) / np.linalg.norm(t - h)
            add("claw_" + f, gxf(g_tooth(0.034, 0.0078, 0.011, 5), frame(t - d * 0.006, d, (0, 1, 0))), "metal", "darksteel", bone=f + "_03_l", sharp=40)
        K.gloves("glove_r", "pb_glove3", cuff=0.665, off=0.0055, sides="r")
        K.limb_straps("glove_cuff_r", "pb_glove3", "lowerarm", (0.662,), width=0.070, off=0.0125, cls="leather", sides="r", thick=0.004)
        K.limb_straps("glove_band_r", "brass", "lowerarm", (0.634,), width=0.010, off=0.0170, cls="metal", sides="r", thick=0.003)
    with K.slot("pants"):
        K.pants("trousers", "pb_trouser3", top=1.02, bottom=0.12, wrinkle=0.0025)
        for o in K.limb_straps("thigh_belt", "belt", "thigh", (0.70,), width=0.024, off=L2 + 0.004, sides="r"):
            apply_mods(o)
            cen = get_co(o).mean(0)
            decal("thigh_buckle", bar(0.014, 0.0125), o, (cen[0] - 0.3, cen[1] - 0.2, cen[2]), "metal", "brass", depth=0.005, lift=0.003, out=(-1, -0.6, 0))
    with K.slot("boots"):
        bt = K.boots("boots", "boot", top=0.47, off=L3, cuff=0.006, toe_h=0.048)
        _sub_piece(bt, "toe_caps", lambda co: (co[:, 1] < -0.100) & (co[:, 2] < 0.078), 0.0036, "metal", "iron")
        _sub_piece(bt, "heel_caps", lambda co: (co[:, 1] > 0.040) & (co[:, 2] < 0.062), 0.0036, "metal", "iron")
        _leg_straps(K, "boot_straps", "belt", (0.40,), width=0.020, off=L3 + 0.004)
