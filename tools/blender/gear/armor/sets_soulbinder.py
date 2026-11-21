"""Soulbinder armour looks t1..t3 (t4 = today's hero, split by split_t4.py).

Identity: gaunt, pale, bald necromancer who binds souls into bone -- dusty violet-grey and near-black cloth, grave linen,
old bone, dark leather, pin-points of sickly soul-green. Hero materials reused: bandage, boot, belt, bone, oldbone, iron,
soul.
  t1  grave robber: bald head wound in grave bandages (jaw binding, a band over one eye), bandaged ribs under a torn
      burial shroud slung over one shoulder, rope belt with looted bones, short shredded skirt, bandaged forearms and
      palms, trousers torn off at the knee, foot wraps tied with cord.
  t2  undertaker's acolyte: deep plain round cowl, long dark belted robe-coat with tight sleeves and bone toggles, a
      leather apron, crypt key and sexton's spade charm on the belt, fingerless gloves with leather cuffs, plain
      trousers, low strapped boots.
  t3  ossuary priest: pointed velvet cowl with a jaw-less skull frontlet and a diadem of vertebrae, violet funeral
      vestment with wide sleeves, a leather mantle plated with fanned ribs, a scapula pectoral, a pale stole with tiny
      soul glyphs, a spine sewn down the back, bone-splinted gloves, trousers with bone knee caps and thigh charms,
      tall boots with lashed shin bones.
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


def _wrinkle_dir(o, amp, freq, d, seed=1):
    """wrinkle_shell with the ridges running across an arbitrary direction d (diagonal drapery)"""
    apply_mods(o)
    co = get_co(o)
    me = o.data
    n = np.empty(len(me.vertices) * 3, dtype=np.float32)
    me.vertices.foreach_get('normal', n)
    n = n.reshape(-1, 3)
    d = np.array(d, float)
    d /= np.linalg.norm(d)
    ph = np.random.RandomState(seed).rand(3) * TAU
    s = (co @ d) * freq
    w = np.sin(s + ph[0] + 1.2 * np.sin(co[:, 2] * freq * 0.5 + ph[1])) + 0.5 * np.sin(s * 2.3 + ph[2])
    set_co(o, co + n * (amp * w)[:, None])
    return o


def _torso_band(B, lo, hi, xmax=0.215):
    """torso between two heights without the shoulder caps m_torso always adds"""
    return (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > lo) & (B.z < hi) & (np.abs(B.x) < xmax) & \
        (B.w("upperarm", "lowerarm", *BONES_HAND) < 0.35)


def _diag_mask(K, keep="l", lo=1.03, width=0.27, arm_x=0.265, x_neck=0.085, z_neck=1.52, slope=0.75):
    """cloth slung over one shoulder: a broad diagonal band from the kept shoulder down to the other hip (front and
    back) plus a short cap over the kept upper arm. (Kit.torso_mask(one_shoulder=) cuts the wrong way round.)"""
    B = K.B
    s = 1.0 if keep == "l" else -1.0
    sx = s * B.x
    zu = z_neck + slope * (sx - x_neck)
    core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > lo) & (B.z < 1.555) & (B.w("upperarm", "lowerarm", *BONES_HAND) < 0.35)
    core &= ((sx > x_neck) | (B.z < zu)) & (B.z > zu - width)
    arm = (B.w("upperarm", "clavicle") > 0.5) & (sx > 0.10) & (sx < arm_x) & (B.w("lowerarm", *BONES_HAND) < 0.3)
    return core | arm


def _coat_off(body=0.013, wrist=0.0065, lo=1.0, hem=0.0165):
    """shell offset of a coat: roomy on the trunk, tight towards the wrists, lifted over the pants at the waist"""
    def f(co):
        ax = np.abs(co[:, 0])
        o = body + (wrist - body) * sstep(ax, 0.24, 0.50)
        return o + np.maximum(hem - o, 0.0) * (1 - sstep(co[:, 2], lo, lo + 0.07)) * (ax < 0.24)
    return f


def _panel_gaps(z_top, z_bot, fn):
    """per-station gaps for Kit.panel (same station heights as it uses): fn(z) -> gap"""
    n = max(3, int(round((z_top - z_bot) / 0.12)) + 2)
    return np.array([fn(z) for z in np.linspace(z_bot, z_top, n)])


def _ride(c, o, host, z0=0.99, z1=1.07):
    """a hanging panel takes the weights of the skirt `host` it lies on below the waist -- so the two can never part:
    with Kit.panel's own hang weights the skirt swallows the panel in a run -- and plain body weights above it"""
    w_transfer(o, host, smooth=0)
    W1, names = w_get(o)
    w_transfer(o, c.ref, smooth=3, only=list(ROBE_BONES))
    W2, _ = w_get(o)
    k = sstep(get_co(o)[:, 2], z0, z1)[:, None]
    set_weights(o, W1 * (1 - k) + W2 * k, names, 4)
    return o


def _skirt(K, name, src, z_top, z_bot, gapf, cls="cloth", nu=30, nv=10, thick=0.005, folds=((7, 0.028, 0.15), (13, 0.010, 0.6)), tails=0,
           tail_depth=0.16, ragged=0.0, u0=0.0, u1=1.0, close=True, calf=0.5, **kw):
    """Kit.skirt with a free gap profile gapf(z). The Kit's gap grows linearly from the waist to the hem, which leaves a
    long skirt only ~25 mm of room at the hips (today's heroes have 40): the thighs come through it in a run."""
    n = max(4, int(round((z_top - z_bot) / 0.14)) + 2)
    zs = np.linspace(z_bot, z_top, n)
    st = K.stations(zs, [gapf(z) for z in zs], plumb=True)
    cy = st[-1][2]
    if tails:
        mod = tails_mod(k=tails, depth=tail_depth, seed=K.rs(), extra=fold_mod(list(folds), cy=cy) if folds else None)
    else:
        mod = fold_mod(list(folds), cy=cy) if folds else None
    o = robe(K.c, src, st, nu=nu, nv=nv, name=name, cls=cls, thick=0.0, ragged=ragged, u0=u0, u1=u1, close=close, calf_to_thigh=calf, mod=mod,
             seed=K.rs(), **kw)
    return solid(o, thick)


def _armpit_fill(B, m):
    """m_torso / Kit.torso_mask leave holes in the armpits (vertices weighted to arm and trunk alike): close them"""
    zone = (np.abs(B.x) > 0.13) & (np.abs(B.x) < 0.30) & (B.z > 1.27) & (B.z < 1.47)
    return m | (B.grow(m, 1) & zone)


def _vertebra(r=0.012, spine=True, lo=False):
    """one vertebra: local X = side to side, Y = out of the back, Z = along the spine"""
    gs = [gxf(g_sphere(1.0, 5 if lo else 6, 3 if lo else 4), S(r, r * 0.82, r * 0.62)),
          g_tube([(-r * 1.75, 0, 0), (0, r * 0.2, 0), (r * 1.75, 0, 0)], [r * 0.14, r * 0.42, r * 0.14], n=3 if lo else 4)]
    if spine:
        gs.append(gxf(g_lathe([(r * 0.36, 0.0), (0.0, r * 1.25)], n=4), T(0, r * 0.5, 0) @ R('X', -90)))
    return gjoin(*gs)


def _hung(prefix, parts, wts):
    out = props(prefix, np.eye(4), parts, "pelvis")
    for o in out:
        w_func(o, lambda co: dict(wts))
    return out


def _skull_frontlet():
    """jaw-less human skull worn as a frontlet. Local X = side, Y = out of the surface it sits on, Z = up.
    Returns (bone geometry, dark socket geometry, [eye points])."""
    k = 1.14
    cran = gxf(g_sphere(1.0, 12, 8), T(0, 0.002, 0.030) @ S(0.058, 0.046, 0.054))
    face = gxf(g_sphere(1.0, 8, 6), T(0, 0.010, -0.038) @ S(0.033, 0.025, 0.030))
    cheeks = [gxf(g_sphere(1.0, 6, 4), T(s * 0.043, 0.016, -0.020) @ S(0.015, 0.016, 0.010)) for s in (1, -1)]
    brow = g_tube([(-0.054, 0.022, 0.018), (-0.028, 0.043, 0.016), (0.0, 0.044, 0.003), (0.028, 0.043, 0.016), (0.054, 0.022, 0.018)],
                  [0.005, 0.0080, 0.0065, 0.0080, 0.005], n=5)
    teeth = [gxf(g_tooth(0.020, 0.0046, 0.0, 4), T(x, 0.029 - abs(x) * 0.34, -0.062) @ R('X', 180)) for x in (-0.024, -0.0145, -0.005, 0.005, 0.0145, 0.024)]
    sockets = [gxf(g_sphere(1.0, 8, 4), T(s * 0.0250, 0.0350, -0.004) @ R('Y', -14 * s) @ S(0.0190, 0.0045, 0.0160)) for s in (1, -1)]
    nose = gxf(g_prism([(0.0, 0.014), (0.0095, -0.012), (0.0, -0.007), (-0.0095, -0.012)], 0.006), T(0, 0.0335, -0.036))
    eyes = [np.array((s * 0.0250, 0.0385, -0.005)) * k for s in (1, -1)]
    return gxf(gjoin(cran, face, brow, *cheeks, *teeth), S(k)), gxf(gjoin(nose, *sockets), S(k)), eyes


def _mats():
    # t1
    mats.cloth("sb_gravewrap", base=(0.160, 0.140, 0.108), var=0.45, ash=0.6, ash_h=0.5, folds=0.25, weave=2.0, worn=0.92, scan="linen", tight=True)
    mats.cloth("sb_shroud_rag", base=(0.074, 0.066, 0.078), var=0.5, ash=0.75, ash_h=0.6, folds=0.7, worn=0.95, scan="burlap")
    mats.cloth("sb_rag_hose", base=(0.044, 0.039, 0.042), var=0.5, ash=0.7, ash_h=0.5, folds=0.6, worn=0.9, scan="wool", tight=True)
    mats.cloth("sb_rope", base=(0.17, 0.14, 0.09), var=0.3, ash=0.5, folds=0.0, scan="rope", hem=False)
    # t2
    mats.cloth("sb_coat", base=(0.040, 0.036, 0.047), var=0.3, ash=0.4, ash_h=0.5, folds=0.7, worn=0.5, scan="wool")
    mats.cloth("sb_cowl2", base=(0.029, 0.027, 0.034), var=0.3, ash=0.35, ash_h=0.7, folds=0.6, worn=0.5, scan="wool")
    mats.cloth("sb_trouser", base=(0.040, 0.036, 0.040), var=0.3, folds=0.5, ash=0.5, ash_h=0.4, worn=0.5, scan="twill", tight=True)
    mats.leather("sb_glove", base=(0.032, 0.025, 0.022), worn=0.6, ash=0.3, scan="leather_b")
    mats.leather("sb_apron", base=(0.058, 0.040, 0.027), worn=0.75, ash=0.45, ash_h=0.5, scan="leather_old")
    # t3
    mats.cloth("sb_velvet", base=(0.030, 0.022, 0.040), var=0.25, ash=0.25, ash_h=0.7, folds=0.6, worn=0.3, scan="velvet")
    mats.cloth("sb_vestment", base=(0.052, 0.034, 0.064), bands=[(0.03, 0.10, (0.19, 0.16, 0.115))], var=0.3, ash=0.3, ash_h=0.4, folds=0.8,
               worn=0.3, scan="velvet")
    mats.cloth("sb_under", base=(0.036, 0.031, 0.040), var=0.3, ash=0.3, folds=0.4, worn=0.4, scan="twill", tight=True)
    mats.cloth("sb_stole", base=(0.175, 0.150, 0.105), bands=[(0.0, 0.07, (0.075, 0.042, 0.092))], var=0.3, ash=0.3, ash_h=0.3, folds=0.3,
               worn=0.35, scan="brocade")
    mats.leather("sb_mantle", base=(0.028, 0.023, 0.027), worn=0.45, ash=0.3, ash_h=0.7, scan="leather")
    mats.flat("sb_void", (0.007, 0.006, 0.007), rough=0.9)


# ================================================================================================ t1  grave robber
def t1(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.cap("scalp_wrap", "sb_gravewrap", z_brow=1.744, ears=False, off=0.0055, cls="cloth", nape=1.612, chew=1, chew_p=0.3,
              wrinkle=0.0015, waxis="z", wfreq=95.0)
        # the binding that keeps a corpse's jaw shut
        jb = [(0.0, -0.020, 1.790), (0.062, -0.028, 1.762), (0.086, -0.034, 1.700), (0.083, -0.045, 1.630), (0.060, -0.065, 1.577),
              (0.0, -0.078, 1.540)]
        K.strap("jaw_band", "sb_gravewrap", jb + [(-x, y, z) for (x, y, z) in jb[-2:0:-1]], width=0.032, thick=0.003, off=0.0065,
                cls="cloth", only=["Head"])
        # a band wound over the left eye (eye centre: x 0.034, z 1.667)
        K.strap("eye_band", "sb_gravewrap", [(-0.062, -0.086, 1.748), (-0.004, -0.118, 1.700), (0.036, -0.104, 1.666), (0.070, -0.066, 1.630),
                                             (0.087, 0.004, 1.604), (0.052, 0.070, 1.610), (-0.020, 0.088, 1.652), (-0.072, 0.048, 1.702),
                                             (-0.088, -0.022, 1.738)], width=0.046, thick=0.003, off=0.0085, cls="cloth", only=["Head"])
        K.headband("brow_band", "sb_gravewrap", z=1.752, width=0.024, tails=(0.19, 0.12), off=0.0085, tilt=0.010)
    with K.slot("chest"):
        # grave bandages wound round the ribs and belly
        wrapm = _torso_band(B, 1.0, 1.50) & (B.z < 1.372 + 0.22 * B.x)
        K.shell("torso_wrap", wrapm, "sb_gravewrap", off=0.008, smooth=3, hem=(1.0, 1.07, 0.0165), chew=2, chew_p=0.35,
                keep=lambda co: co.z < 1.28, wrinkle=0.0022, waxis="z", wfreq=72.0)
        # torn burial shroud slung over the left shoulder
        for i, (z, dz, ph) in enumerate(((1.112, 0.030, 0.0), (1.212, 0.034, math.pi), (1.300, 0.024, 1.0))):
            rg = K.ring(z, 0.0)
            K.strap("wrap_band_%d" % i, "sb_gravewrap", [(rg[2] * math.cos(a), rg[1] + (rg[4] if math.sin(a) > 0 else rg[3]) * math.sin(a),
                                                          z + dz * math.cos(a + ph)) for a in np.linspace(0, TAU, 14, endpoint=False)],
                    width=0.048, thick=0.003, off=0.0105, cls="cloth", only=["spine_01", "spine_02", "spine_03"])
        # torn burial shroud slung over the left shoulder
        sh = K.shell("shroud", _diag_mask(K, "l", lo=1.03, width=0.33), "sb_shroud_rag", off=0.019, smooth=4, chew=2, chew_p=0.34,
                     keep=lambda co: (co.x > 0.06 and co.z > 1.44) or co.z < 1.07)
        _wrinkle_dir(sh, 0.0038, 34.0, (0.79, 0.0, -0.62), K.rs())
        kp = wrap_path(c, [(0.168, -0.060, 1.500)], 0.030)[0]
        kn = add("shroud_knot", gjoin(gxf(g_sphere(1.0, 8, 5), T(*kp) @ S(0.027, 0.024, 0.019)),
                                      g_tube([kp, kp + np.array((0.012, -0.040, -0.055)), kp + np.array((0.020, -0.062, -0.135))], [1.0, 1.1, 1.35],
                                             n=6, sx=0.006, sy=0.024, up=(1, 0, 0)),
                                      g_tube([kp, kp + np.array((-0.022, -0.034, -0.040)), kp + np.array((-0.034, -0.050, -0.095))], [1.0, 1.0, 1.25],
                                             n=6, sx=0.006, sy=0.020, up=(1, 0, 0))), "cloth", "sb_shroud_rag", sharp=180)
        w_func(kn, lambda co: {"clavicle_l": 0.5, "spine_03": 0.5})
        K.skirt("rag_skirt", "sb_shroud_rag", z_top=1.05, z_bot=0.60, gap_top=0.021, gap_bot=0.040, tails=7, tail_depth=0.20, ragged=0.03,
                folds=((6, 0.03, 0.1), (11, 0.012, 0.5)), calf=0.0)
        K.rope("rope_belt", "sb_rope", z=1.055, gap=0.0235, r=0.0085, cls="cloth", turns=2)
        # looted bones on a cord (left front)
        p, n = _belt_pt(K, 1.045, -40, 0.038)
        _hung("grave_bones", [
            (gxf(g_bone(0.105, 0.0075, 1.7), hang(p, n, 0.16)), "bone", "oldbone", dict(sharp=50)),
            (gxf(g_bone(0.072, 0.0060, 1.7), hang(p + np.array((0.020, 0.012, 0.0)), n, 0.24) @ R('Z', 35)), "bone", "bone", dict(sharp=50)),
            (g_tube([p + np.array((-0.012, 0.004, 0.022)), p + n * 0.006, p + np.array((0.022, 0.014, 0.020))], 0.0035, n=4), "cloth", "sb_rope",
             dict(sharp=180)),
        ], {"pelvis": 0.6, "thigh_l": 0.4})
        K.limb_straps("arm_rag", "sb_gravewrap", "upperarm", (0.325, 0.352), width=0.030, off=0.0045, cls="cloth", sides="r", thick=0.003)
    with K.slot("gloves"):
        K.sleeves("arm_wraps", "sb_gravewrap", x0=0.488, x1=0.728, off=L1, wrinkle=0.002, waxis="x", wfreq=95.0, chew=1)
        K.palm_wrap("palm_wraps", "sb_gravewrap")
    with K.slot("pants"):
        m = m_legs(B, 0.30, 1.03) & ~((B.x < 0) & (B.z < 0.475))
        K.shell("trousers", m, "sb_rag_hose", off=L2, smooth=3, chew=3, chew_p=0.42, keep=lambda co: co.z > 0.62, wrinkle=0.003)
        mk_ = (B.w(*BONES_LEG) > 0.5) & (B.x < 0) & (B.z > 0.345) & (B.z < 0.455)
        K.shell("shin_bandage", mk_, "sb_gravewrap", off=0.007, smooth=2, chew=1, wrinkle=0.0016, wfreq=90.0)
    with K.slot("boots"):
        K.boots("foot_wraps", "sb_gravewrap", top=0.215, off=L3, cls="cloth", toe_h=0.042, toe_grow=0.006, wrinkle=0.0025, wfreq=75.0, chew=2)
        K.limb_straps("wrap_cords", "sb_rope", "calf", (0.135, 0.180), width=0.011, off=L3 + 0.004, cls="cloth", thick=0.005)


# ================================================================================================ t2  undertaker's acolyte
def t2(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.hood("cowl", "sb_cowl2", style="round", peak=0.044, open_deg=55.0, brow=1.742)
    with K.slot("chest"):
        coat = K.shell("coat", _armpit_fill(B, K.torso_mask(1.0, 1.55, 0.685)), "sb_coat", off=_coat_off(), smooth=3)
        wrinkle_shell(coat, 0.0022, 24.0, "z", K.rs(), mask=lambda co: 1 - sstep(np.abs(co[:, 0]), 0.20, 0.26))
        wrinkle_shell(coat, 0.0015, 85.0, "x", K.rs(), mask=lambda co: sstep(np.abs(co[:, 0]), 0.25, 0.33))
        gf = lambda z: float(np.interp(z, [0.32, 0.93, 1.05], [0.064, 0.046, 0.021]))
        sk = _skirt(K, "coat_skirt", "sb_coat", 1.05, 0.32, gf, nv=12, folds=((5, 0.035, 0.1), (9, 0.014, 0.5)), calf=0.30, u0=0.75 + 0.03,
                    u1=1.75 - 0.03, close=False)
        ap = K.panel("apron", "sb_apron", -0.105, 0.105, 1.07, 0.60, front=True, cls="leather", flare=0.05, thick=0.006, folds=((1.2, 0.006, 0.2),),
                      gap=_panel_gaps(1.07, 0.60, lambda z: gf(z) + 0.0065 + 0.007 * sstep(1.03 - z, 0.0, 0.40)))
        _ride(c, ap, sk)
        K.belt("belt", "belt", z=1.068, width=0.050, gap=0.0305, buckle="iron")
        # bone toggles down the front of the coat
        tg = []
        for z in (1.415, 1.335, 1.255, 1.175):
            P = wrap_path(c, [(0.0, -0.3, z)], 0.0165)[0]
            tg.append(gxf(g_bone(0.030, 0.0042, 1.6, 5), frame(P - np.array((0.015, 0, 0)), (1, 0, 0))))
        o = add("toggles", gjoin(*tg), "bone", "oldbone", sharp=50)
        w_transfer(o, c.ref, smooth=0, only=["spine_01", "spine_02", "spine_03"])
        # crypt key and a sexton's spade charm on a ring (right front), a bound soul in a vial (left front)
        p, n = _belt_pt(K, 1.045, -128, 0.046)
        spade = [(-0.002, 0.0), (0.002, 0.0), (0.002, -0.036), (0.014, -0.039), (0.014, -0.062), (0.0, -0.084), (-0.014, -0.062), (-0.014, -0.039),
                 (-0.002, -0.036)]
        MK = frame(p, (0, 0, 1), n)
        _hung("keys", [
            (gxf(g_ring(0.016, 0.0028, n=10, m=4), MK @ T(0, 0, -0.012) @ R('X', 90)), "metal", "iron", dict(sharp=60)),
            (gxf(g_key(0.092, 0.012), MK @ T(-0.010, 0.003, -0.024) @ R('Y', 8)), "metal", "iron", dict(sharp=40)),
            (gxf(g_prism(spade, 0.004), MK @ T(0.014, 0.006, -0.026) @ R('Y', -10)), "metal", "iron", dict(sharp=40)),
        ], {"pelvis": 0.6, "thigh_r": 0.4})
        p, n = _belt_pt(K, 1.040, -52, 0.046)
        MV = frame(p, (0, 0, 1), n)
        _hung("vial", [
            (gxf(g_tube([(0, 0, 0.0), (0, 0, -0.030)], 0.0028, n=4), MV), "leather", "belt", dict(sharp=180)),
            (gxf(g_lathe([(0.0, -0.026), (0.0075, -0.030), (0.0095, -0.038), (0.0060, -0.042)], n=6), MV), "bone", "oldbone", dict(sharp=50)),
            (gxf(g_gem(0.0095, 0.040, 6, 0.3), MV @ T(0, 0, -0.068) @ R('X', 180)), "emis", "soul", dict(sharp=20)),
        ], {"pelvis": 0.6, "thigh_l": 0.4})
        K.pouch("satchel", "sb_apron", side=1, z=0.985, size=1.05, flap="belt", y=0.02)
    with K.slot("gloves"):
        K.gloves("gloves", "sb_glove", cuff=0.705, off=0.0055, fingerless=True)
        mc = (B.w("lowerarm", "hand") > 0.5) & (np.abs(B.x) > 0.612) & (np.abs(B.x) < 0.752) & (B.w("thumb_", "index_", "middle_", "ring_", "pinky_") < 0.3)
        K.shell("cuffs", mc, "sb_glove", cls="leather", off=0.0105, smooth=2)
        K.limb_straps("cuff_straps", "belt", "lowerarm", (0.642,), width=0.014, off=0.0125, thick=0.004)
    with K.slot("pants"):
        K.pants("trousers", "sb_trouser", top=1.03, bottom=0.12, wrinkle=0.003)
    with K.slot("boots"):
        K.boots("boots", "boot", top=0.265, off=L3, cuff=0.005, toe_h=0.046, wrinkle=0.002)
        K.limb_straps("ankle_straps", "belt", "calf", (0.165,), width=0.016, off=L3 + 0.004)
        K.rivets("ankle_buckles", "iron", [(s * 0.165, 0.03, 0.165) for s in (1, -1)], r=0.008)


# ================================================================================================ t3  ossuary priest
def t3(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        hd, gh = K.hood("cowl", "sb_velvet", style="point", peak=0.028, open_deg=62.0, brow=1.752)
        apply_mods(hd)
        # jaw-less skull worn as a frontlet on the slope of the hood
        SM = ray_frame(hd, (0.0, -0.6, 1.800), (0, 1, 0), up=(0, 0, 1), lift=0.0)
        g_b, g_d, eyes = _skull_frontlet()
        add("skull", gxf(g_b, SM), "bone", "bone", bone="Head", sharp=60)
        add("skull_sockets", gxf(g_d, SM), "bone", "sb_void", bone="Head", sharp=60)
        add("skull_eyes", gjoin(*[gxf(g_sphere(0.0030, 6, 4), SM @ T(*e)) for e in eyes]), "emis", "soul", bone="Head", sharp=60)
        # diadem of vertebrae round the hood, two short strands at the temples
        BP = ring_of(gh, 28, 12, grow=0.011)
        vs = []
        for i in range(3, len(BP) - 2):
            p = BP[i]
            out = np.array((p[0], p[1] + 0.02, 0.0))
            out /= max(np.linalg.norm(out), 1e-6)
            tan = BP[(i + 1) % len(BP)] - BP[i - 1]
            vs.append(gxf(_vertebra(0.0115, spine=(i % 2 == 0), lo=True), frame(p, tan, out)))
        for i in (3, len(BP) - 3):
            p = BP[i]
            sg = 1.0 if p[0] > 0 else -1.0
            for k in range(1, 5):
                q = p + np.array((sg * 0.004 * k, -0.004 * k, -0.021 * k))
                vs.append(gxf(_vertebra(0.0095 - 0.0008 * k, spine=False, lo=True), T(*q) @ R('Z', -90 * sg)))
        add("diadem", gjoin(*vs), "bone", "oldbone", bone="Head", sharp=50)
    with K.slot("chest"):
        K.decimate["chest"] = 0.90
        K.shell("vestment", _armpit_fill(B, K.torso_mask(1.0, 1.55, 0.27)), "sb_vestment", off=0.013, hem=(1.0, 1.07, 0.0165), smooth=3, wrinkle=0.002)
        K.sleeves("under_sleeves", "sb_under", x0=0.23, x1=0.685, off=L1, wrinkle=0.0014, waxis="x", wfreq=85.0)
        # wide sleeves (own stations: Kit.bell_sleeves sizes them from an arm radius measured off-centre, far too wide here)
        sleeve(c, "sb_vestment", [(0.170, 0.000, 0.012, 0.078, 0.074, 0.074), (0.300, 0.004, 0.012, 0.070, 0.066, 0.066),
                                  (0.425, 0.010, 0.012, 0.071, 0.064, 0.064), (0.535, 0.022, 0.012, 0.082, 0.066, 0.066)],
               n=14, nv=8, name="sleeve_l", cls="cloth", thick=0.006)
        gf = lambda z: float(np.interp(z, [0.43, 0.93, 1.05], [0.076, 0.048, 0.022]))
        sk = _skirt(K, "vest_skirt", "sb_vestment", 1.05, 0.43, gf, nv=10, folds=((6, 0.040, 0.1), (11, 0.014, 0.5)), calf=0.5)
        # stole: two pale bands down the front with tiny soul glyphs
        gp = lambda z: 0.0215 if z > 1.08 else gf(z) + 0.0075 + 0.016 * sstep(1.03 - z, 0.0, 0.50)
        for s, nm in ((1, "l"), (-1, "r")):
            x0, x1 = (0.050, 0.112) if s > 0 else (-0.112, -0.050)
            st_ = K.panel("stole_" + nm, "sb_stole", x0, x1, 1.44, 0.47, front=True, gap=_panel_gaps(1.44, 0.47, gp), flare=0.0, point=0.03,
                          thick=0.005, nu=5, folds=((1.0, 0.004, 0.1),))
            _ride(c, st_, sk)
            for k, (z, shp) in enumerate(((1.290, chevron(0.011, 0.011, 0.0045)), (0.900, rhomb(0.0065, 0.013)),
                                          (0.700, hook(0.010, 0.011, 0.0042) if s > 0 else fork(0.011, 0.010, 0.0036)))):
                decal("glyph_%s%d" % (nm, k), shp, st_, (s * 0.081, -0.6, z), "emis", "soul", ray=(0, 1, 0), depth=0.003, lift=0.0015)
        K.belt("belt", "belt", z=1.068, width=0.046, gap=0.0315)
        r = K.ring(1.068, 0.0315 + 0.010)
        BK = frame((0.0, r[1] - r[3] - 0.004, 1.068), (0, -1, 0), (0, 0, 1))
        props("buckle", BK, [
            (g_lathe([(0.011, 0.006), (0.024, 0.008), (0.029, 0.0), (0.024, -0.004), (0.011, -0.004), (0.011, 0.006)], n=10), "bone", "oldbone", dict(sharp=50)),
            (gxf(_vertebra(0.010, spine=False), T(0, 0, 0.007) @ R('X', 90)), "bone", "bone", dict(sharp=50)),
        ], "spine_01")
        # leather mantle plated with fanned ribs
        zl = 1.385 - 0.5 * B.y
        mm = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zl) & (B.z < 1.535) & (np.abs(B.x) < 0.20)
        mm = (mm | ((B.w("upperarm", "clavicle") > 0.5) & (np.abs(B.x) < 0.335) & (B.z > 1.40))) & (B.w(*BONES_HAND, "lowerarm") < 0.3)
        # thin where the cowl lies on it (so the two do not fight in motion), full thickness out on the shoulders
        cp = shell(B, mm, lambda co: 0.0155 + 0.0135 * sstep(np.hypot(co[:, 0], co[:, 1] - 0.03), 0.12, 0.20), "mantle", "leather", "sb_mantle",
                   smooth=4, sharp=180)
        solid(cp, 0.007)
        apply_mods(cp)
        ribs = []
        for s in (1, -1):
            for k, phi in enumerate((-58.0, -24.0, 8.0, 40.0, 72.0)):
                a = math.radians(phi)
                r1 = 0.315 if abs(phi) < 30 else (0.27 if abs(phi) < 50 else 0.215)
                guide = []
                for t in np.linspace(0.0, 1.0, 8):
                    rr = 0.150 + (r1 - 0.150) * t
                    guide.append((s * rr * math.cos(a), 0.030 + rr * math.sin(a), 1.60 - 0.42 * (rr - 0.10)))
                P = _on(cp, guide, 0.0045)
                ribs.append(g_tube(P, [0.0080, 0.0090, 0.0088, 0.0082, 0.0074, 0.0064, 0.0050, 0.0022], n=6, sx=1.0, sy=0.8))
        rb = add("mantle_ribs", gjoin(*ribs), "bone", "bone", sharp=60)
        w_transfer(rb, cp, smooth=1)
        # scapula pectoral on a cord, cut with a soul rune
        PM = ray_frame(c.ref, (0.0, -0.6, 1.305), (0, 1, 0), up=(0, 0, 1), lift=0.0185)
        outl = [(-0.036, 0.034), (0.036, 0.034), (0.031, 0.004), (0.011, -0.044), (0.0, -0.051), (-0.011, -0.044), (-0.031, 0.004)]
        pec = add("pectoral", gjoin(gxf(g_prism(outl, 0.008), PM),
                                    gxf(g_tube([(-0.038, 0.004, 0.034), (0.0, 0.006, 0.037), (0.038, 0.004, 0.034)], [0.005, 0.0062, 0.005], n=5), PM)),
                  "bone", "oldbone", bevel=0.002, sharp=40)
        w_transfer(pec, c.ref, smooth=0, only=["spine_03", "spine_02"])
        cords = []
        for s in (1, -1):
            a_ = xf(np.array([(s * 0.034, 0.0, 0.034)]), PM)[0]
            cords.append(g_tube([a_] + list(wrap_path(c, [(s * 0.060, -0.3, 1.40), (s * 0.085, -0.3, 1.47)], 0.0165)), 0.003, n=4))
        cd = add("pectoral_cord", gjoin(*cords), "leather", "belt", sharp=180)
        w_transfer(cd, c.ref, smooth=0, only=["spine_03", "spine_02"])
        decal("pectoral_rune", chevron(0.011, 0.011, 0.0045), pec, tuple(xf(np.array([(0.0, 0.3, 0.004)]), PM)[0]), "emis", "soul", ray=(0, 1, 0),
              depth=0.003, lift=0.0012)
        # a spine sewn down the back of the vestment
        vb = []
        for z in np.linspace(1.300, 1.090, 7):
            P = wrap_path(c, [(0.0, 0.22, z)], 0.0185)[0]
            vb.append(gxf(_vertebra(0.0160), frame(P, (0, 0, 1), (0, 1, 0))))
        sp = add("back_spine", gjoin(*vb), "bone", "oldbone", sharp=50)
        w_transfer(sp, c.ref, smooth=0, only=["spine_01", "spine_02", "spine_03"])
    with K.slot("gloves"):
        K.gloves("gloves", "sb_glove", cuff=0.66, off=0.0055)
        K.sleeves("bracers", "belt", x0=0.540, x1=0.692, off=0.0115, cls="leather")
        # bone splints lashed along the forearm, finger bones over the back of the hand
        bs = []
        for th, r_ in ((-32.0, 0.0068), (0.0, 0.0080), (32.0, 0.0068)):
            sy_, cz_ = 0.10 * math.sin(math.radians(th)), 0.10 * math.cos(math.radians(th))
            a_, b_ = wrap_path(c, [(0.550, 0.080 + sy_, 1.432 + cz_), (0.686, 0.080 + sy_, 1.432 + cz_)], 0.0115 + r_ * 1.15)
            bs.append(gxf(g_bone(float(np.linalg.norm(b_ - a_)), r_, 1.6, 5), frame(a_, b_ - a_)))
        o = add("splints_l", gjoin(*bs), "bone", "oldbone", bone="lowerarm_l", sharp=50)
        mirrored(o)
        hb = []
        w0 = joint("hand_l")
        for f in ("index", "middle", "ring", "pinky"):
            nm = "%s_01_l" % f
            if nm not in mk.ARM.data.bones:
                continue
            k0 = joint(nm)
            a_, b_ = wrap_path(c, [tuple(w0 + (k0 - w0) * 0.30 + np.array((0, 0, 0.05))), tuple(w0 + (k0 - w0) * 0.97 + np.array((0, 0, 0.05)))], 0.0085)
            hb.append(gxf(g_bone(float(np.linalg.norm(b_ - a_)), 0.0034, 1.55, 4), frame(a_, b_ - a_)))
        if hb:
            o = add("hand_bones_l", gjoin(*hb), "bone", "bone", bone="hand_l", sharp=50)
            mirrored(o)
    with K.slot("pants"):
        K.pants("trousers", "sb_under", top=1.03, bottom=0.12, wrinkle=0.0028)
        K.kneecop("knee_bone", "oldbone", cls="bone", size=0.80)
        K.limb_straps("thigh_cord", "belt", "thigh", (0.715,), width=0.011, off=L2 + 0.004, sides="l", thick=0.005)
        ch = []
        for k, (ang, ln) in enumerate(((-62.0, 0.060), (-28.0, 0.078), (8.0, 0.055))):
            a = math.radians(ang)
            q = wrap_path(c, [(0.089 + 0.20 * math.cos(a), 0.0 + 0.20 * math.sin(a), 0.708)], L2 + 0.012)[0]
            rad = np.array((math.cos(a), math.sin(a), 0.0))
            ch.append(gxf(g_bone(ln, 0.0052, 1.7, 5) if k != 1 else g_tooth(ln, 0.0075, 0.010, 5), hang(q, rad, 0.10)))
        add("thigh_charms", gjoin(*ch), "bone", "bone", bone="thigh_l", sharp=50)
    with K.slot("boots"):
        K.boots("boots", "boot", top=0.445, off=L3, toe_h=0.048, wrinkle=0.0018)
        K.limb_straps("boot_straps", "belt", "calf", (0.185, 0.375), width=0.016, off=L3 + 0.004)
        # a shin bone lashed down the front of each boot
        a_, b_ = wrap_path(c, [(0.089, -0.085, 0.405), (0.093, -0.050, 0.165)], L3 + 0.0125)
        g = [gxf(g_bone(float(np.linalg.norm(b_ - a_)), 0.0105, 1.75, 6), frame(a_, b_ - a_))]
        d_ = (b_ - a_) / np.linalg.norm(b_ - a_)
        for z in (0.375, 0.185):
            q = a_ + d_ * ((a_[2] - z) / max(-d_[2], 1e-6))
            g.append(gxf(g_ring(0.0150, 0.0032, n=8, m=4), frame(q, d_)))
        o = add("shin_bone_l", gjoin(*g[:1]), "bone", "oldbone", bone="calf_l", sharp=50)
        mirrored(o)
        o = add("shin_lash_l", gjoin(*g[1:]), "leather", "belt", bone="calf_l", sharp=180)
        mirrored(o)
