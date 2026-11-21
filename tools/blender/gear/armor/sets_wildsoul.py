"""Wildsoul armour looks t1..t3 (t4 = today's hero, split by split_t4.py).

Identity: huge shape-changing wild man of the dusk woods, long grey hair and beard -- wolf pelt, dark and tan hides,
bark, old bone, feathers, undyed wool, small moss-green spirit lights. Hero materials reused: pelt, pelt_dark, wrap,
wool, feather, feather_dark, hide, darkhide, strap, tan, bark, wood, bone, oldbone, moss.
  t1  outcast: rawhide headband with two feathers and a fang (hair and beard free), bare chest under a ragged, mangy
      pelt half-cape slung over the left shoulder on a fang-studded strap, rope belt with torn hide flaps, hide wrist
      wraps, short ragged hide breeches, hide foot wraps tied with cord.
  t2  tribesman: tall round fur hat with ear flaps and a bone pin, sleeveless laced hide tunic with a brown fur collar,
      a bone toggle and a belt with a wooden ring, laced leather bracers, pale wool trousers with a leather thigh wrap,
      fur-trimmed boots.
  t3  spirit-walker: stag skull with antlers worn on the brow of a dark pelt cowl, a mantle of crow feathers over a
      hide jerkin, a harness of lashed bark slabs with moss runes, bone-and-fang necklace, woven loin panels, bark-slat
      bracers, leggings under a tan hip wrap with bone charms, tall pale fur boots with bone toggles.
"""
from gearkit import *


# ---------------------------------------------------------------------------------------------- helpers (private)
def _on(host, pts, off=0.0):
    """guide points projected on the nearest surface of `host` and pushed `off` out of it"""
    if any(m.type != 'ARMATURE' for m in host.modifiers):
        apply_mods(host)
    out = []
    for p in pts:
        ok, loc, nrm, fi = host.closest_point_on_mesh(Vector(p))
        out.append(np.array(loc) + np.array(nrm) * off)
    return np.array(out)


def _belt_pt(K, z, a_deg, gap):
    """(point, outward direction) on the waist ring at height z, `gap` off the body. 0 deg = left side, -90 = front"""
    r = K.ring(z, gap)
    a = math.radians(a_deg)
    cs, sn = math.cos(a), math.sin(a)
    e = 2.0 / 2.3
    ry = r[4] if sn > 0 else r[3]
    p = np.array((r[2] * math.copysign(abs(cs) ** e, cs), r[1] + ry * math.copysign(abs(sn) ** e, sn), z))
    n = np.array((cs / r[2], sn / ry, 0.0))
    return p, n / np.linalg.norm(n)


def _panel_gaps(z_top, z_bot, fn):
    """per-station gaps for Kit.panel (same station heights as it uses): fn(z) -> gap"""
    n = max(3, int(round((z_top - z_bot) / 0.12)) + 2)
    return np.array([fn(z) for z in np.linspace(z_bot, z_top, n)])


def _head_ring(K, z=1.74, slope=0.0, n=28, pad=0.004, band=0.018, hair=True, cy=-0.015):
    """closed path round the head -- and round the hair mesh when hair=True (Kit.headband follows the scalp skin and
    would run inside the hair) -- at band height: per azimuth the outermost point inside the band, smoothed.
    Height of the path: z + slope * (y - cy). Returns (n, 3) points, index 0 at the right ear going round the front."""
    B = K.B
    P = [B.co[B.w("Head") > 0.5]]
    if hair:
        P += [get_co(h) for h in K.c.hairs] + [_hair_points(K)]
    P = np.concatenate(P)
    P = P[np.abs(P[:, 2] - (z + slope * (P[:, 1] - cy))) < band]
    ang = np.arctan2(P[:, 1] - cy, P[:, 0])
    rad = np.hypot(P[:, 0], P[:, 1] - cy)
    aa = np.linspace(-math.pi, math.pi, n, endpoint=False)
    rr = np.full(n, np.nan)
    for i, a in enumerate(aa):
        d = np.abs((ang - a + math.pi) % TAU - math.pi)
        sel = d < TAU / n
        if sel.any():
            rr[i] = rad[sel].max()
    ok = ~np.isnan(rr)
    rr = np.interp(np.arange(n), np.nonzero(ok)[0], rr[ok], period=n)
    rs = rr.copy()
    for _ in range(2):
        rs = (np.roll(rs, 1) + rs * 2 + np.roll(rs, -1)) / 4
    rr = np.maximum(rs, rr - 0.004) + pad
    return np.stack([rr * np.cos(aa), cy + rr * np.sin(aa), z + slope * rr * np.sin(aa)], axis=1)


def _hair_points(K):
    """rest-space points of the base body's scalp hair. build_base.py gives the wildsoul base 'Hair_Long'; today's hero
    (what the Kit sees) wears a wolf hood and has only the beard, so the mesh is fetched here, measured and removed."""
    if getattr(K, "_hairpts", None) is None:
        c = K.c
        o = add_hair(c, "Hair_Long")
        K._hairpts = get_co(o).copy()
        c.hairs.remove(o)
        bpy.data.objects.remove(o, do_unlink=True)
    return K._hairpts


def _ring_loft(rings, cap=0.012):
    """surface through closed rings of equal point count (bottom to top), closed with a fan `cap` above the last one"""
    n = len(rings[0])
    V = [np.asarray(p, float) for r in rings for p in r]
    F = []
    for j in range(len(rings) - 1):
        for i in range(n):
            a, b = j * n + i, j * n + (i + 1) % n
            F.append((a, b, b + n, a + n))
    V.append(np.mean(np.asarray(rings[-1], float), axis=0) + np.array((0.0, 0.0, cap)))
    o = (len(rings) - 1) * n
    for i in range(n):
        F.append((len(V) - 1, o + i, o + (i + 1) % n))
    return geo(np.array(V), F)


def _ring_at(path, a_deg, cy=-0.015):
    """index of the point of a _head_ring path nearest to an azimuth (0 = left ear, -90 = brow, 90 = nape)"""
    a = np.arctan2(path[:, 1] - cy, path[:, 0])
    d = np.abs((a - math.radians(a_deg) + math.pi) % TAU - math.pi)
    return int(np.argmin(d))


def _feathers(K, name, vid, off_out, rows, seed=1, srcs=("feather_dark", "feather_dark", "feather")):
    """feathers sewn on a body shell: vid = the body vertices under the shell, rows = [(fraction of the way from the
    neck to the rim, count, length, width)]. Every feather lies down and outwards on the surface and is rigid with the
    skin weights of the vertex it is rooted on. One object per source material."""
    B = K.B
    vid = np.asarray(vid, dtype=np.int64)
    co, nr = B.co[vid], B.nrm[vid]
    phi = np.arctan2(co[:, 1] - 0.03, co[:, 0])
    rho = np.hypot(co[:, 0], co[:, 1] - 0.03) + np.clip(1.52 - co[:, 2], 0, None) * 0.9
    rng = np.random.RandomState(seed)
    geo_, wrow = {s: [] for s in set(srcs)}, {s: [] for s in set(srcs)}
    k = 0
    for (frac, cnt, ln, wd) in rows:
        for a in np.linspace(-math.pi, math.pi, cnt, endpoint=False) + rng.rand() * 0.2:
            d = np.abs((phi - a + math.pi) % TAU - math.pi)
            sel = np.nonzero(d < 1.15 * math.pi / cnt)[0]
            if len(sel) < 2:
                continue
            tgt = frac * rho[sel].max()
            j = sel[np.argmin(np.abs(rho[sel] - tgt) + 0.35 * d[sel] * rho[sel])]
            if abs(rho[j] - tgt) > 0.05:
                continue
            n_ = nr[j] / max(np.linalg.norm(nr[j]), 1e-9)
            rad = np.array((co[j, 0], co[j, 1] - 0.03, 0.0))
            rad /= max(np.linalg.norm(rad), 1e-9)
            dd = rad * 0.72 + np.array((0.0, 0.0, -0.70))
            dd = dd - n_ * float(np.dot(dd, n_))
            if np.linalg.norm(dd) < 0.2:
                continue
            dd = dd / np.linalg.norm(dd) + n_ * 0.14
            L = ln * (0.85 + 0.3 * rng.rand())
            g = gxf(g_feather(L, wd * (0.85 + 0.3 * rng.rand()), 0.004),
                    frame(co[j] + n_ * (off_out + 0.002 + 0.004 * (k % 2)), dd, n_) @ R('Y', float(rng.randn() * 9.0)))
            s = srcs[k % len(srcs)]
            geo_[s].append(g)
            wrow[s] += [B.W[vid[j]]] * len(g["v"])
            k += 1
    out = []
    for s, gs in geo_.items():
        if not gs:
            continue
        o = add("%s_%s" % (name, s), gjoin(*gs), "cloth", s, sharp=30)
        set_weights(o, np.array(wrow[s]), B.names, 4)
        out.append(o)
    return out


def _fur_tops(K, name, src, at, radius):
    """fur rolls round both boot tops (Kit.fur_cuffs with fewer segments: it costs 900 triangles a leg)"""
    out = []
    for side in (1, -1):
        out.append(fur_roll(K.leg_ring_path(at, 0.016, 10, side), radius, src, name="%s_%s" % (name, "l" if side > 0 else "r"), cls="cloth",
                            bone="calf_l" if side > 0 else "calf_r", noise=0.010, n=6))
    return out


def _fur_collar(K, name, src, radius=0.030, wide=0.128, depth=0.108, n=18):
    """fur roll lying ON the shoulders round the neck (Kit.fur_collar is a fixed ring: on this broad back it floats)"""
    c = K.c
    guide = []
    for a in np.linspace(0, TAU, n, endpoint=False):
        s_ = math.sin(a)
        guide.append((wide * math.cos(a), 0.035 + depth * s_, 1.505 + (0.060 * s_ if s_ < 0 else 0.0)))
    path = wrap_path(c, guide, radius * 0.62)
    for _ in range(2):
        path = (np.roll(path, 1, 0) + path * 2 + np.roll(path, -1, 0)) / 4
    o = fur_roll(path, radius, src, name=name, cls="cloth", noise=0.014, n=8)
    w_transfer(o, c.ref, smooth=2, only=["spine_03", "clavicle_l", "clavicle_r", "neck_01"])
    return o


def _hung(prefix, parts, wts):
    out = props(prefix, np.eye(4), parts, "pelvis")
    for o in out:
        w_func(o, lambda co: dict(wts))
    return out


def _stag_skull():
    """stag skull in skull space: +Z runs from the back of the brain case down the muzzle, +Y is the top of the skull.
    Returns (bone geometry, dark socket geometry)."""
    sk = g_stations([(0.000, 0, 0.000, 0.030, 0.020, 0.024), (0.034, 0, 0.004, 0.055, 0.028, 0.040), (0.072, 0, 0.006, 0.060, 0.030, 0.044),
                     (0.110, 0, 0.002, 0.050, 0.026, 0.036), (0.150, 0, -0.004, 0.035, 0.020, 0.027), (0.195, 0, -0.008, 0.026, 0.016, 0.020),
                     (0.228, 0, -0.010, 0.019, 0.012, 0.014)], nu=12, nv=9, p=2.3, cap0=True, cap1=True)
    brow = [gxf(g_sphere(1.0, 6, 4), T(s * 0.046, 0.030, 0.092) @ S(0.020, 0.014, 0.024)) for s in (1, -1)]
    ped = [g_tube([(s * 0.030, 0.036, 0.040), (s * 0.036, 0.062, 0.030)], [0.014, 0.011], n=6) for s in (1, -1)]
    teeth = [gxf(g_tooth(0.014, 0.0042, 0.0, 4), T(s * 0.022, -0.016, z_) @ R('X', 90)) for s in (1, -1) for z_ in (0.128, 0.142, 0.156)]
    sock = [gxf(g_sphere(1.0, 8, 4), frame((s * 0.0500, 0.020, 0.104), (s * 0.92, 0.38, 0.12)) @ S(0.0150, 0.0165, 0.0050)) for s in (1, -1)]
    nose = gxf(g_prism(rhomb(0.0095, 0.024), 0.006), T(0, 0.0085, 0.203))
    return gjoin(sk, *brow, *ped, *teeth), gjoin(nose, *sock)


def _mats():
    # t1
    mats.leather("ws_rawhide", base=(0.092, 0.066, 0.041), worn=0.92, crack=1.0, rough=(0.70, 0.95), ash=0.5, ash_h=0.5, scan="hide")
    mats.leather("ws_breech", base=(0.060, 0.044, 0.030), worn=0.9, crack=1.0, ash=0.55, ash_h=0.5, scan="leather_old")
    mats.cloth("ws_cord", base=(0.15, 0.12, 0.075), var=0.3, ash=0.5, folds=0.0, scan="rope", hem=False)
    mats.fur("ws_fur_scrap", base=(0.036, 0.031, 0.026), tip=(0.16, 0.14, 0.115))
    # t2
    mats.fur("ws_fur", base=(0.040, 0.028, 0.018), tip=(0.205, 0.148, 0.092))
    mats.leather("ws_tunic", base=(0.108, 0.076, 0.045), worn=0.6, crack=0.8, rough=(0.68, 0.94), ash=0.4, ash_h=0.6, scan="hide")
    mats.leather("ws_bracer", base=(0.052, 0.034, 0.021), worn=0.6, ash=0.3, scan="leather")
    mats.cloth("ws_wool_pale", base=(0.150, 0.132, 0.100), var=0.35, ash=0.45, ash_h=0.4, folds=0.6, worn=0.5, scan="wool", tight=True)
    # t3
    mats.cloth("ws_weave", base=(0.046, 0.040, 0.034), bands=[(0.03, 0.085, (0.150, 0.105, 0.060)), (0.11, 0.128, (0.150, 0.105, 0.060))],
               var=0.3, ash=0.4, ash_h=0.4, folds=0.6, worn=0.4, scan="wool")
    mats.leather("ws_legging", base=(0.040, 0.030, 0.022), worn=0.5, ash=0.4, ash_h=0.4, scan="leather_b")
    mats.flat("ws_void", (0.008, 0.007, 0.006), rough=0.9)


# ================================================================================================ t1  outcast
def t1(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.opts["helm"] = dict(never_hide=("hair", "beard", "brows", "head"))
        path = _head_ring(K, z=1.738, slope=-0.16, pad=0.0035)
        add("headband", g_tube(path, 1.0, n=6, closed=True, sx=0.015, sy=0.0036, up=(0, 0, 1)), "leather", "ws_rawhide", bone="Head", sharp=60)
        kb = path[_ring_at(path, 100)] + np.array((0.0, 0.006, 0.0))
        tl = [g_tube([kb, kb + np.array((0.020 * s, 0.014, -0.045)), kb + np.array((0.034 * s, 0.022, -0.100 - 0.03 * (s > 0)))], [0.9, 1.0, 1.25],
                     n=5, sx=0.003, sy=0.011, up=(0, 1, 0)) for s in (1, -1)]
        add("headband_knot", gjoin(gxf(g_sphere(1.0, 7, 5), T(*kb) @ S(0.017, 0.012, 0.015)), *tl), "leather", "ws_rawhide", bone="Head", sharp=180)
        # two feathers tucked in over the left ear, fanned out and back, their flats turned to the front
        p = path[_ring_at(path, 22)]
        rad = np.array((p[0], p[1] + 0.015, 0.0))
        rad /= np.linalg.norm(rad)
        for k, (d, ln, wd, src) in enumerate((((0.50, 0.30, 0.82), 0.225, 0.056, "feather"), ((0.80, 0.42, 0.50), 0.175, 0.050, "feather_dark"))):
            add("feather_%d" % k, gxf(g_feather(ln, wd, 0.004), frame(p + rad * (0.006 + 0.005 * k) - np.array((0, 0.004 * k, 0.022)), d, (0.25, -1.0, 0.25))),
                "cloth", src, bone="Head", sharp=30)
        p = path[_ring_at(path, -158)]
        rad = np.array((p[0], p[1] + 0.015, 0.0))
        rad /= np.linalg.norm(rad)
        add("fang", gjoin(gxf(g_tooth(0.042, 0.0070, 0.006, 5), frame(p + rad * 0.004 - np.array((0, 0, 0.030)), np.array((0, 0, -1.0)) + rad * 0.12, rad)),
                          g_tube([p + rad * 0.004 - np.array((0, 0, 0.010)), p + rad * 0.004 - np.array((0, 0, 0.032))], 0.0022, n=4)),
            "bone", "oldbone", bone="Head", sharp=40)
    with K.slot("chest"):
        # ragged, mangy pelt half-cape slung over the left shoulder
        zl = 1.275 - 0.30 * B.y - 0.10 * sstep(B.y, 0.0, 0.10)
        core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zl) & (B.z < 1.548) & (np.abs(B.x) < 0.215)
        arms = (B.w("upperarm", "clavicle") > 0.5) & (B.x > 0.0) & (B.x < 0.425) & (B.z > 1.395)
        mm = (core | arms) & (B.w(*BONES_HAND, "lowerarm") < 0.3) & (B.x > -0.055 + 0.36 * (1.53 - B.z))
        cape = shell(B, mm, 0.026, "half_cape", "cloth", "ws_fur_scrap", smooth=4, sharp=180)
        chew_edge(cape, rounds=2, p=0.40, seed=K.rs(), keep=lambda co: co.z > 1.49 and abs(co.x) < 0.12)
        shag(cape, 0.007, 30.0, 4)
        solid(cape, 0.007)
        # the strap that holds it: left shoulder to right hip, three fangs sewn on
        K.bandolier("cape_strap", "strap", width=0.044, off=0.0055)
        tp = wrap_path(c, [(0.085, -0.3, 1.385), (0.050, -0.3, 1.335), (0.015, -0.3, 1.285)], 0.0145)
        o = add("strap_fangs", gjoin(*[gxf(g_tooth(0.046 - 0.006 * i, 0.0082, 0.008, 5), frame(q, (-0.45, -0.12, -1.0), (0, -1, 0))) for i, q in enumerate(tp)]),
                "bone", "oldbone", sharp=40)
        w_transfer(o, c.ref, smooth=0, only=["spine_03", "spine_02"])
        K.rope("rope_belt", "ws_cord", z=1.013, gap=0.0205, r=0.0085, cls="cloth", turns=2)
        tear = lambda U, V, z: z + 0.028 * np.sin(U * 17.0 + 1.3) * np.sin(U * 7.1 + 0.4) * (1 - V) ** 3
        gp = lambda z: 0.030 + 0.012 * sstep(1.0 - z, 0.0, 0.3)
        K.panel("loin_flap", "ws_rawhide", -0.110, 0.050, 1.032, 0.70, front=True, cls="leather", flare=0.04, point=0.07, thick=0.006,
                folds=((1.4, 0.007, 0.2),), zmod=tear, gap=_panel_gaps(1.032, 0.70, gp))
        K.panel("back_flap", "ws_rawhide", -0.040, 0.125, 1.032, 0.76, front=False, cls="leather", flare=0.04, point=0.05, thick=0.006,
                folds=((1.6, 0.007, 0.5),), zmod=tear, gap=_panel_gaps(1.032, 0.76, gp))
    with K.slot("gloves"):
        K.sleeves("wrist_wraps", "ws_rawhide", x0=0.555, x1=0.735, off=L2, cls="leather", wrinkle=0.0016, waxis="x", wfreq=90.0)
        K.limb_straps("wrap_cords", "ws_cord", "lowerarm", (0.625, 0.695), width=0.010, off=L2 + 0.0025, cls="cloth", thick=0.004)
    with K.slot("pants"):
        m = m_legs(B, 0.565, 1.03) & ~((B.x < 0) & (B.z < 0.655))
        K.shell("breeches", m, "ws_breech", cls="leather", off=L2, smooth=3, chew=3, chew_p=0.42, keep=lambda co: co.z > 0.80, wrinkle=0.0026)
        K.limb_straps("leg_cord", "ws_cord", "thigh", (0.715,), width=0.010, off=L2 + 0.0035, cls="cloth", sides="r", thick=0.004)
    with K.slot("boots"):
        K.boots("foot_wraps", "ws_rawhide", top=0.205, off=L3, cls="leather", toe_h=0.044, toe_grow=0.007, wrinkle=0.003, wfreq=60.0, chew=2)
        ms = (B.w(*BONES_LEG) > 0.5) & (B.x > 0) & (B.z > 0.185) & (B.z < 0.365)
        K.shell("shin_wrap", ms, "ws_rawhide", cls="leather", off=L3 + 0.001, smooth=2, chew=1, wrinkle=0.002, wfreq=70.0)
        K.limb_straps("foot_cords", "ws_cord", "calf", (0.130, 0.178), width=0.011, off=L3 + 0.004, cls="cloth", thick=0.005)
        K.limb_straps("shin_cords", "ws_cord", "calf", (0.245, 0.320), width=0.010, off=L3 + 0.005, cls="cloth", sides="l", thick=0.005)


# ================================================================================================ t2  tribesman
def t2(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.opts["helm"] = dict(force_hide=("hair",), never_hide=("beard", "head"))
        # round fur hat: a liner with ear and neck flaps, the tall fur crown lofted over it
        cap = K.cap("cap_liner", "ws_fur", z_brow=1.722, ears=True, off=0.012, cls="cloth", nape=1.585)
        shag(cap, 0.004, 34.0, 3)
        r0 = _head_ring(K, z=1.716, slope=-0.12, n=20, pad=0.013, hair=False)
        r1 = _head_ring(K, z=1.736, slope=-0.12, n=20, pad=0.034, hair=False)
        cen = r1.mean(0)
        up = lambda s, dz: cen + (r1 - cen) * np.array((s, s, 1.0)) + np.array((0.0, 0.0, dz))
        hat = add("fur_hat", _ring_loft([r0, r1, up(1.03, 0.034), up(0.94, 0.066), up(0.66, 0.086)], cap=0.010), "cloth", "ws_fur", bone="Head",
                  sharp=180, subsurf=1)
        shag(hat, 0.007, 30.0, 6)
        # a bone pin through the front of the fur
        pf = r1[_ring_at(r1, -90)]
        add("hat_pin", gxf(g_bone(0.052, 0.0050, 1.7, 5), frame(pf + np.array((-0.026, -0.004, 0.016)), (1.0, 0.0, 0.10))), "bone", "oldbone", bone="Head",
            sharp=50)
    with K.slot("chest"):
        K.tunic("tunic", "ws_tunic", lo=1.0, hi=1.50, sleeve=0.224, off=0.015, cls="leather", vneck=0.150, vwidth=0.058, wrinkle=0.002, smooth=4)
        K.skirt("tunic_skirt", "ws_tunic", z_top=1.05, z_bot=0.745, gap_top=0.018, gap_bot=0.048, cls="leather", thick=0.006, nv=7, ragged=0.018, smooth=2,
                folds=((5, 0.030, 0.2), (9, 0.010, 0.6)), calf=0.0)
        _fur_collar(K, "fur_collar", "ws_fur", radius=0.030)
        # lacing across the open neck, closed with a bone toggle
        lace = []
        for i, z in enumerate((1.385, 1.425, 1.465)):
            w = 0.058 * min((z - 1.35) / 0.15, 1.3) + 0.014
            a_, b_ = wrap_path(c, [(-w, -0.3, z - 0.014), (w, -0.3, z + 0.014)], 0.0185)
            c_, d_ = wrap_path(c, [(-w, -0.3, z + 0.014), (w, -0.3, z - 0.014)], 0.0185)
            lace += [g_tube([a_, (a_ + b_) / 2 + np.array((0, -0.003, 0)), b_], 0.0030, n=4), g_tube([c_, (c_ + d_) / 2 + np.array((0, -0.006, 0)), d_], 0.0030, n=4)]
        o = add("lacing", gjoin(*lace), "leather", "strap", sharp=180)
        w_transfer(o, c.ref, smooth=0, only=["spine_03", "spine_02"])
        q = wrap_path(c, [(0.0, -0.3, 1.338)], 0.022)[0]
        o = add("toggle", gxf(g_bone(0.056, 0.0058, 1.7, 6), frame(q - np.array((0.028, 0, 0)), (1, 0, 0.12))), "bone", "oldbone", sharp=50)
        w_transfer(o, c.ref, smooth=0, only=["spine_03", "spine_02"])
        K.belt("belt", "strap", z=1.072, width=0.058, gap=0.031)
        r = K.ring(1.072, 0.031 + 0.010)
        BK = frame((0.0, r[1] - r[3] - 0.004, 1.072), (0, -1, 0), (0, 0, 1))
        props("buckle", BK, [
            (g_ring(0.030, 0.0075, n=12, m=5), "leather", "wood", dict(sharp=60)),
            (gxf(g_tooth(0.085, 0.0075, 0.010, 5), T(-0.040, 0.004, 0.006) @ R('Y', 90)), "bone", "oldbone", dict(sharp=40)),
        ], "spine_01")
        K.pouch("pouch", "darkhide", side=-1, z=0.985, flap="strap")
        # a feather and a fang on a thong (left front)
        p, n = _belt_pt(K, 1.045, -50, 0.046)
        _hung("charm", [
            (gxf(g_feather(0.150, 0.046, 0.004), hang(p, n, 0.20) @ R('Z', 10)), "cloth", "feather_dark", dict(sharp=30)),
            (gxf(g_tooth(0.050, 0.0080, 0.008, 5), hang(p + np.array((0.022, 0.014, 0.0)), n, 0.18) @ T(0, 0, 0.022)), "bone", "oldbone", dict(sharp=40)),
            (g_tube([p + np.array((0.022, 0.014, 0.004)), p + np.array((0.024, 0.014, -0.022))], 0.0024, n=4), "leather", "strap", dict(sharp=180)),
        ], {"pelvis": 0.6, "thigh_l": 0.4})
    with K.slot("gloves"):
        K.sleeves("bracers", "ws_bracer", x0=0.500, x1=0.708, off=0.0115, cls="leather", wrinkle=0.0012, waxis="x", wfreq=80.0)
        K.limb_straps("bracer_laces", "strap", "lowerarm", (0.535, 0.600, 0.665), width=0.011, off=0.0135, thick=0.004)
    with K.slot("pants"):
        K.pants("trousers", "ws_wool_pale", top=1.03, bottom=0.12, baggy=0.005, wrinkle=0.0032)
        mt = (B.w(*BONES_LEG) > 0.5) & (B.x > 0) & (B.z > 0.600) & (B.z < 0.775)
        K.shell("thigh_wrap", mt, "ws_bracer", cls="leather", off=L2 + 0.0085, smooth=3)
        K.limb_straps("thigh_ties", "strap", "thigh", (0.635, 0.735), width=0.012, off=L2 + 0.011, sides="l", thick=0.004)
    with K.slot("boots"):
        K.boots("boots", "darkhide", top=0.335, off=L3, toe_h=0.048, wrinkle=0.0022)
        _fur_tops(K, "boot_fur", "ws_fur", 0.338, 0.021)
        K.limb_straps("boot_ties", "strap", "calf", (0.165, 0.245), width=0.013, off=L3 + 0.004)


# ================================================================================================ t3  spirit-walker
def t3(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.opts["helm"] = dict(force_hide=("hair",), never_hide=("beard", "head"))
        hd, gh = K.hood("cowl", "pelt_dark", style="round", thick=0.012, chin=1.535, brow=1.768, open_deg=72.0, peak=0.016)
        shag(hd, 0.006, 34.0, 2)
        # stag skull worn on the brow, muzzle down over the forehead
        SM = frame((0.0, -0.022, 1.915), (0.0, -0.80, -0.60), (0.0, 0.0, 1.0)) @ S(1.22)
        g_b, g_d = _stag_skull()
        sk = add("stag_skull", gxf(g_b, SM), "bone", "bone", bone="Head", sharp=60)
        add("stag_sockets", gxf(g_d, SM), "bone", "ws_void", bone="Head", sharp=60)
        ant = g_antler(0.70, tines=4)
        ab = xf(np.array([(0.036, 0.060, 0.030)]), SM)[0]
        add("antler_l", gxf(ant, T(abs(ab[0]), ab[1], ab[2] - 0.006) @ R('Y', 7)), "bone", "oldbone", bone="Head", sharp=60)
        add("antler_r", gxf(mirror_x(ant), T(-abs(ab[0]), ab[1], ab[2] - 0.006) @ R('Y', -7)), "bone", "oldbone", bone="Head", sharp=60)
        decal("skull_rune", chevron(0.011, 0.012, 0.0048), sk, tuple(xf(np.array([(0.0, 0.30, 0.062)]), SM)[0]), "emis", "moss",
              ray=tuple(-SM[:3, 1]), up=tuple(-SM[:3, 2]), depth=0.003, lift=0.0012, bone="Head")
    with K.slot("chest"):
        K.tunic("jerkin", "darkhide", lo=1.0, hi=1.50, sleeve=0.224, off=0.014, cls="leather", vneck=0.105, vwidth=0.050, wrinkle=0.0018, smooth=4)
        # mantle of crow feathers on a hide yoke, open at the throat
        zl = 1.390 - 0.55 * B.y - 0.06 * sstep(B.y, 0.0, 0.10)
        core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zl) & (B.z < 1.548) & (np.abs(B.x) < 0.215)
        arms = (B.w("upperarm", "clavicle") > 0.5) & (np.abs(B.x) < 0.325) & (B.z > 1.400)
        ym = (core | arms) & (B.w(*BONES_HAND, "lowerarm") < 0.3) & ~((B.y < 0.0) & (np.abs(B.x) < 0.045 + 0.5 * np.clip(1.50 - B.z, 0, 1)))
        yoke = shell(B, ym, 0.026, "mantle_yoke", "leather", "darkhide", smooth=4, sharp=180)
        solid(yoke, 0.006)
        _feathers(K, "mantle", list(yoke["src_vid"]), 0.026 + 0.006, [(0.93, 28, 0.175, 0.050), (0.70, 24, 0.150, 0.048)], seed=7,
                  srcs=("feather_dark", "feather_dark", "feather_dark", "feather"))
        # harness of bark slabs lashed over the chest
        slabs = []
        for nm, xc, wd, z1, z0, tp in (("c", 0.0, 0.084, 1.348, 1.108, [0.80, 1.0, 1.0, 0.72]), ("l", 0.100, 0.086, 1.392, 1.150, [0.74, 1.0, 0.96, 0.66]),
                                       ("r", -0.100, 0.086, 1.392, 1.150, [0.74, 1.0, 0.96, 0.66])):
            pth = wrap_path(c, [(xc, -0.3, z) for z in np.linspace(z1, z0, 4)], 0.029)
            pth[:, 0] = xc
            o = add("bark_" + nm, g_plank(pth, wd, 0.015, up=(0, -1, 0), taper=tp), "leather", "bark", bevel=0.004, sharp=40)
            w_func(o, lambda co: {"spine_03": sstep(co[:, 2], 1.17, 1.30), "spine_02": 1 - sstep(co[:, 2], 1.17, 1.30)})
            slabs.append(o)
        cd = []
        for z in (1.195, 1.318):
            pts = [wrap_path(c, [(x, -0.3, z + 0.004 * math.sin(9 * x))], 0.049 if abs(x) < 0.15 else 0.017)[0] for x in (-0.178, -0.140, -0.098, -0.050, 0.0, 0.050, 0.098, 0.140, 0.178)]
            cd.append(g_tube(pts, 0.0052, n=5))
        o = add("bark_cords", gjoin(*cd), "leather", "strap", sharp=180)
        w_func(o, lambda co: {"spine_03": sstep(co[:, 2], 1.17, 1.30), "spine_02": 1 - sstep(co[:, 2], 1.17, 1.30)})
        for i, z in enumerate((1.195, 1.318)):
            rg = K.ring(z, 0.03)
            K.strap("harness_%d" % i, "strap", [(rg[2] * math.cos(a), rg[1] + (rg[4] if math.sin(a) > 0 else rg[3]) * math.sin(a), z)
                                                for a in np.linspace(0, TAU, 14, endpoint=False)], width=0.030, thick=0.006, off=0.0165,
                    only=["spine_02", "spine_03"])
        decal("bark_rune_0", chevron(0.020, 0.018, 0.0075), slabs[0], (0.0, -0.6, 1.268), "emis", "moss", ray=(0, 1, 0), depth=0.005, lift=0.002)
        decal("bark_rune_1", bar(0.016, 0.0042), slabs[0], (0.0, -0.6, 1.232), "emis", "moss", ray=(0, 1, 0), depth=0.005, lift=0.002)
        # bone-and-fang necklace
        npts = [(0.105 * t, -0.30, 1.476 - 0.072 * (1 - t * t)) for t in np.linspace(-1, 1, 15)]
        NP = wrap_path(c, npts, 0.021)
        neck = [g_tube(NP, 0.0036, n=3)]
        for i in range(1, 14):
            t = abs(i - 7) / 7.0
            if i % 2:
                neck.append(gxf(g_tooth(0.044 - 0.018 * t, 0.0080, 0.007, 4), frame(NP[i] + np.array((0, -0.004, -0.002)), (0, -0.18, -1.0), (0, -1, 0))))
            else:
                neck.append(gxf(g_bone(0.026 - 0.008 * t, 0.0040, 1.6, 4), frame(NP[i] + np.array((0, -0.004, -0.002)), (0, -0.14, -1.0), (0, -1, 0))))
        nk = add("necklace", gjoin(*neck), "bone", "bone", sharp=50)
        w_transfer(nk, c.ref, smooth=0, only=["spine_03", "spine_02"])
        # woven loin panels under a broad belt with a bone ring
        gp = lambda z: 0.034 + 0.014 * sstep(1.0 - z, 0.0, 0.4)
        K.panel("loin_front", "ws_weave", -0.098, 0.098, 1.062, 0.54, front=True, flare=0.10, point=0.06, thick=0.006, folds=((1.5, 0.008, 0.2),),
                gap=_panel_gaps(1.062, 0.54, gp))
        K.panel("loin_back", "ws_weave", -0.125, 0.125, 1.062, 0.51, front=False, flare=0.12, point=0.03, thick=0.006, folds=((2.2, 0.009, 0.4),),
                gap=_panel_gaps(1.062, 0.51, gp))
        K.belt("belt", "strap", z=1.074, width=0.064, gap=0.041)
        r = K.ring(1.074, 0.041 + 0.010)
        BK = frame((0.0, r[1] - r[3] - 0.004, 1.074), (0, -1, 0), (0, 0, 1))
        props("buckle", BK, [
            (g_ring(0.033, 0.0090, n=12, m=5), "bone", "oldbone", dict(sharp=60)),
            (gxf(g_antler(0.20, tines=2), T(-0.046, 0.0, 0.008) @ R('Y', 78) @ R('Z', 90)), "bone", "bone", dict(sharp=50)),
        ], "spine_01")
        p, n = _belt_pt(K, 1.040, -42, 0.062)
        _hung("charm_l", [
            (gxf(g_feather(0.170, 0.050, 0.004), hang(p, n, 0.20) @ R('Z', 8)), "cloth", "feather_dark", dict(sharp=30)),
            (gxf(g_feather(0.135, 0.044, 0.004), hang(p + np.array((0.020, 0.012, 0.0)), n, 0.24) @ R('Z', -10)), "cloth", "feather", dict(sharp=30)),
        ], {"pelvis": 0.6, "thigh_l": 0.4})
        p, n = _belt_pt(K, 1.040, -138, 0.062)
        _hung("charm_r", [
            (gxf(g_tooth(0.085, 0.0120, 0.022, 6), hang(p, n, 0.16) @ T(0, 0, 0.012)), "bone", "oldbone", dict(sharp=40)),
            (g_tube([p + np.array((0, 0, 0.022)), p - np.array((0, 0, 0.016))], 0.0026, n=4), "leather", "strap", dict(sharp=180)),
        ], {"pelvis": 0.6, "thigh_r": 0.4})
    with K.slot("gloves"):
        K.sleeves("wraps", "darkhide", x0=0.495, x1=0.728, off=L2, cls="leather", wrinkle=0.0014, waxis="x", wfreq=85.0)
        # three bark slats lashed over each forearm, a moss rune cut in the middle one
        sl = []
        for th in (-40.0, 0.0, 40.0):
            sy_, cz_ = math.sin(math.radians(th)), math.cos(math.radians(th))
            pth = wrap_path(c, [(x, 0.080 + 0.11 * sy_, 1.432 + 0.11 * cz_) for x in (0.520, 0.580, 0.640, 0.700)], 0.0125)
            sl.append(g_plank(pth, 0.031, 0.0085, up=(0, sy_, cz_), taper=[0.85, 1.0, 1.0, 0.8]))
        sl_o = add("bark_slats_l", gjoin(*sl), "leather", "bark", bone="lowerarm_l", bevel=0.003, sharp=40)
        rn = [decal("slat_rune_a_l", chevron(0.0095, 0.010, 0.0042), sl_o, (0.588, 0.080, 1.80), "emis", "moss", up=(1, 0, 0), ray=(0, 0, -1), depth=0.004,
                    lift=0.0015, bone="lowerarm_l"),
              decal("slat_rune_b_l", bar(0.0095, 0.0032), sl_o, (0.630, 0.080, 1.80), "emis", "moss", up=(0, 1, 0), ray=(0, 0, -1), depth=0.004, lift=0.0015,
                    bone="lowerarm_l")]
        cds = []
        for x in (0.540, 0.682):
            angs = np.linspace(0, TAU, 14, endpoint=False)
            gpts = [(x, 0.080 + 0.12 * math.cos(a), 1.432 + 0.12 * math.sin(a)) for a in angs]
            kk = sstep(np.sin(angs), -0.45, 0.20)[:, None]
            cds.append(g_tube(wrap_path(c, gpts, 0.0125) * (1 - kk) + wrap_path(c, gpts, 0.0245) * kk, 0.0046, n=5, closed=True))
        cd_o = add("slat_cords_l", gjoin(*cds), "leather", "strap", bone="lowerarm_l", sharp=180)
        for o in [sl_o, cd_o] + rn:
            mirrored(o)
    with K.slot("pants"):
        K.pants("leggings", "ws_legging", top=1.03, bottom=0.12, off=0.0072, cls="leather", wrinkle=0.0022)
        K.shell("hip_wrap", m_legs(B, 0.735, 1.03), "tan", cls="leather", off=0.0108, smooth=3, chew=2, chew_p=0.4, keep=lambda co: co.z > 0.87)
        K.limb_straps("thigh_cord", "strap", "thigh", (0.668,), width=0.011, off=0.0072 + 0.0035, sides="r", thick=0.005)
        ch = []
        for k, (ang, ln) in enumerate(((242.0, 0.058), (208.0, 0.080), (172.0, 0.060))):
            a = math.radians(ang)
            rad = np.array((math.cos(a), math.sin(a), 0.0))
            q = wrap_path(c, [(-0.089 + 0.20 * rad[0], 0.20 * rad[1], 0.662)], 0.0072 + 0.012)[0]
            ch.append(gxf(g_bone(ln, 0.0052, 1.7, 5) if k != 1 else g_tooth(ln, 0.0078, 0.010, 5), hang(q, rad, 0.10)))
        add("thigh_charms", gjoin(*ch), "bone", "oldbone", bone="thigh_r", sharp=50)
    with K.slot("boots"):
        K.decimate["boots"] = 0.85
        K.boots("boots", "darkhide", top=0.205, off=L3, toe_h=0.048)
        fs = K.leg_band("fur_shaft", "pelt", 0.150, 0.468, off=L3 + 0.006, cls="cloth")
        shag(fs, 0.005, 36.0, 5)
        _fur_tops(K, "boot_top", "pelt", 0.462, 0.023)
        K.limb_straps("boot_cords", "strap", "calf", (0.250, 0.385), width=0.011, off=L3 + 0.013, thick=0.005)
        tg = []
        for z in (0.250, 0.385):
            q = wrap_path(c, [(0.30, 0.045, z)], L3 + 0.021)[0]
            tg.append(gxf(g_bone(0.044, 0.0052, 1.7, 5), frame(q - np.array((0, 0.006, 0.022)), (0.0, 0.25, 1.0))))
        o = add("boot_toggles_l", gjoin(*tg), "bone", "oldbone", bone="calf_l", sharp=50)
        mirrored(o)
