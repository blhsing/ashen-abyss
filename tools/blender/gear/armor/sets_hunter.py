"""Hunter armour looks t1..t3 (t4 = today's hero, split by split_t4.py).

Identity: lean stalker in dark blue-grey cloth and brown leather, hooded, quiet colours, small teal glints.
Hero materials reused: under, hose, dark, jerkin, strap, iron, brass, cloak, hood, mask, darksteel, teal.
  t1  poacher's rags: knotted headscarf, torn shirt under a patched hide vest, rope belt, rag wraps on one arm, patched
      trousers torn at the shin, low worn boots.
  t2  scout: leather cap with ear flaps and a rolled neck scarf, shirt + plain leather jerkin with a short peplum, belt
      with pouch and a bandolier, gloves with banded bracers, trousers with leather knee pads, knee boots.
  t3  master stalker: feathered wide hat over a half mask, long fitted leather coat split in front with a fur collar and
      a one-sided shoulder cape, brass-banded bracers, strapped fitted pants, thigh-high boots.
"""
from gearkit import *


def _mats():
    mats.cloth("hn_rag", base=(0.105, 0.105, 0.09), var=0.4, ash=0.6, folds=0.5, weave=1.7, worn=0.9, tight=True)
    mats.cloth("hn_scarf", base=(0.060, 0.070, 0.066), var=0.4, ash=0.4, folds=0.5, worn=0.8, scan="burlap", tight=True)
    mats.leather("hn_hide", base=(0.085, 0.060, 0.038), worn=0.9, ash=0.5, scan="hide")
    mats.cloth("hn_patch", base=(0.050, 0.052, 0.050), var=0.5, ash=0.6, folds=0.6, worn=0.9, scan="wool", tight=True)
    mats.cloth("hn_rope", base=(0.20, 0.16, 0.10), var=0.3, ash=0.4, folds=0.0, scan="rope", hem=False)
    mats.leather("hn_cap", base=(0.050, 0.034, 0.022), worn=0.6, ash=0.3, scan="leather_b")
    mats.leather("hn_coat", base=(0.034, 0.030, 0.028), edge=(0.15, 0.11, 0.08), worn=0.4, ash=0.3, ash_h=0.5, scan="leather")
    mats.fur("hn_fur", base=(0.030, 0.027, 0.025), tip=(0.17, 0.16, 0.15))
    mats.cloth("hn_hat", base=(0.022, 0.030, 0.034), var=0.3, ash=0.3, ash_h=0.9, folds=0.0, worn=0.5, scan="wool", hem=False)
    mats.cloth("hn_feather", base=(0.050, 0.110, 0.105), var=0.4, folds=0.0, ash=0.0, worn=0.2, scan="fleece", hem=False)
    mats.cloth("hn_cape", base=(0.026, 0.040, 0.046), folds=0.8, ash=0.4, ash_h=0.9, worn=0.4, var=0.35, scan="wool")


def t1(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.cap("scarf", "hn_scarf", z_brow=1.728, ears=False, off=0.007, cls="cloth", nape=1.63, wrinkle=0.002, wfreq=60.0)
        K.headband("scarf_band", "hn_scarf", z=1.742, width=0.030, tails=(0.20, 0.13), off=0.010)
    with K.slot("chest"):
        K.tunic("shirt", "hn_rag", lo=1.0, hi=1.49, sleeve=0.31, off=0.009, vneck=0.10, vwidth=0.05, wrinkle=0.0022, chew=2, chew_p=0.35,
                keep=lambda co: 1.10 < co.z < 1.40 and abs(co.x) < 0.22)
        K.shell("vest", K.torso_mask(1.04, 1.47, 0.20, vneck=0.20, vwidth=0.075), "hn_hide", cls="leather", off=0.018, smooth=3, chew=2, chew_p=0.3,
                keep=lambda co: 1.12 < co.z < 1.36)
        K.rope("rope_belt", "hn_rope", z=1.055, gap=0.030, r=0.008, cls="cloth", turns=1)
        K.bandolier("sling", "strap", width=0.030, off=0.027)
    with K.slot("gloves"):
        K.sleeves("wrap", "hn_rag", x0=0.50, x1=0.72, off=L1, sides="l", wrinkle=0.002, waxis="x", wfreq=95.0, chew=1)
        K.palm_wrap("palm_wraps", "hn_rag")
        K.limb_straps("wrist_strap", "strap", "lowerarm", (0.67,), width=0.018, off=0.008, sides="r")
    with K.slot("pants"):
        K.pants("trousers", "hn_patch", top=1.02, bottom=0.27, wrinkle=0.003, chew=2)
        K.limb_straps("thigh_strap", "strap", "thigh", (0.70,), width=0.018, off=L2 + 0.004, sides="l")
    with K.slot("boots"):
        K.boots("boots", "dark", top=0.215, off=L3, toe_h=0.046, wrinkle=0.0025, chew=1)


def t2(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.cap("cap", "hn_cap", z_brow=1.722, ears=True, off=0.009, cls="leather", nape=1.60)
        K.strap("cap_strap", "strap", [(0.092, -0.01, 1.69), (0.084, -0.04, 1.62), (0.044, -0.086, 1.565), (0.0, -0.099, 1.553),
                                       (-0.044, -0.086, 1.565), (-0.084, -0.04, 1.62), (-0.092, -0.01, 1.69)],
                width=0.014, thick=0.004, off=0.006, closed=False, only=["Head"])
        # rolled scarf round the neck (the hood worn down)
        path = [(0.105 * math.cos(a), 0.018 + 0.108 * math.sin(a), 1.512 + (0.012 * math.sin(a) if math.sin(a) > 0 else 0.030 * math.sin(a)))
                for a in np.linspace(0, TAU, 18, endpoint=False)]
        sc = add("scarf_roll", g_tube(path, 0.034, n=8, closed=True, sy=0.8), "cloth", "hood", sharp=180, subsurf=1)
        w_func(sc, lambda co: {"spine_03": 0.6, "neck_01": 0.4})
    with K.slot("chest"):
        K.tunic("shirt", "under", lo=1.01, hi=1.50, sleeve=0.71, off=0.0075, wrinkle=0.002)
        K.shell("jerkin", K.torso_mask(1.03, 1.47, 0.215, vneck=0.09, vwidth=0.05), "jerkin", cls="leather", off=0.0175, smooth=3)
        K.skirt("peplum", "jerkin", z_top=1.055, z_bot=0.86, gap_top=0.022, gap_bot=0.040, cls="leather", thick=0.006, nv=6, tails=6,
                tail_depth=0.07, folds=((6, 0.02, 0.2),), calf=0.0, u0=0.75 + 0.04, u1=1.75 - 0.04, close=False)
        K.belt("belt", "strap", z=1.065, width=0.045, gap=0.032, buckle="iron")
        K.pouch("pouch", "dark", side=1, z=0.99, flap="strap")
        K.bandolier("bandolier", "strap", width=0.036, off=0.026)
    with K.slot("gloves"):
        K.gloves("gloves", "dark", cuff=0.665, off=0.0055)
        K.sleeves("bracers", "dark", x0=0.505, x1=0.68, off=0.0115, cls="leather")
        K.arm_rings("bracer_bands", "iron", xs=(0.535, 0.615), width=0.018, grow=0.015)
    with K.slot("pants"):
        K.pants("trousers", "hose", top=1.02, bottom=0.12, wrinkle=0.003)
        K.kneecop("kneepad", "dark", cls="leather", size=0.92)
        K.limb_straps("thigh_strap", "strap", "thigh", (0.72,), width=0.016, off=L2 + 0.004, sides="r")
    with K.slot("boots"):
        K.boots("boots", "dark", top=0.44, off=L3, cuff=0.006, toe_h=0.048)


def t3(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.cap("hat_liner", "mask", z_brow=1.728, ears=False, off=0.006, cls="cloth", nape=1.62)
        hat = add("hat", gxf(hat_wide(brim=0.215, crown=0.108, height=0.120, n=22, wave=0.026, droop=0.022), T(0.0, -0.006, 1.762) @ R('X', -6)),
                  "cloth", "hn_hat", bone="Head", sharp=180)
        solid(hat, 0.006)
        add("hat_band", gxf(g_lathe([(0.112, 0.010), (0.114, 0.022), (0.110, 0.034)], n=22, sx=0.94, sy=1.06), T(0.0, -0.006, 1.762) @ R('X', -6)),
            "leather", "strap", bone="Head", sharp=60, solidify=0.003)
        for i, (ang, ln) in enumerate(((0, 0.24), (9, 0.19))):
            add("feather_%d" % i, gxf(g_feather(ln, 0.050, 0.004), T(0.092, 0.020 + 0.012 * i, 1.800) @ R('Y', 62 - ang) @ R('Z', 80)), "cloth", "hn_feather",
                bone="Head", sharp=60)
        add("hat_pin", gxf(g_gem(0.011, 0.024, 6, 0.5), T(0.104, -0.005, 1.792)), "emis", "teal", bone="Head", sharp=30)
        K.face_mask("mask", "mask", z_top=1.650, z_bot=1.535)
    with K.slot("chest"):
        K.tunic("coat", "hn_coat", lo=1.02, hi=1.52, sleeve=0.63, off=0.016, cls="leather", wrinkle=0.0018, smooth=3)
        K.skirt("coat_tails", "hn_coat", z_top=1.05, z_bot=0.50, gap_top=0.024, gap_bot=0.055, cls="leather", thick=0.006, nv=10,
                folds=((5, 0.03, 0.1), (9, 0.012, 0.5)), calf=0.35, u0=0.75 + 0.05, u1=1.75 - 0.05, close=False)
        K.fur_collar("fur_collar", "hn_fur", radius=0.036, z=1.535, wide=0.150)
        cp = K.capelet("cape", "hn_cape", zmin=1.33, arm_x=0.44, off=0.030, chew=1)
        co = get_co(cp)
        K.belt("belt", "strap", z=1.075, width=0.050, gap=0.034, buckle="brass")
        K.bandolier("bandolier", "dark", width=0.040, off=0.040, flip=True)
        K.pouch("pouch", "dark", side=-1, z=0.99, flap="strap")
        add("clasp", gxf(g_lathe([(0.0, 0.010), (0.016, 0.008), (0.022, 0.0)], n=8), frame((0.0, -0.118, 1.455), (0, -1, 0.35))), "metal", "brass",
            bone="spine_03", sharp=50)
        add("clasp_gem", gxf(g_gem(0.008, 0.016, 6, 0.5), frame((0.0, -0.128, 1.458), (0, -1, 0.35))), "emis", "teal", bone="spine_03", sharp=30)
    with K.slot("gloves"):
        K.gloves("gloves", "hn_cap", cuff=0.62, off=0.0055)
        K.sleeves("bracers", "dark", x0=0.515, x1=0.675, off=0.020, cls="leather")
        K.arm_rings("bracer_bands", "brass", xs=(0.53, 0.59, 0.65), width=0.014, grow=0.0235)
    with K.slot("pants"):
        K.pants("pants", "hn_coat", top=1.02, bottom=0.12, cls="leather", wrinkle=0.0025)
        K.limb_straps("thigh_straps", "strap", "thigh", (0.66, 0.74), width=0.014, off=L2 + 0.004)
        K.rivets("strap_studs", "brass", [(s * 0.165, -0.02, z) for s in (1, -1) for z in (0.66, 0.74)], r=0.007)
    with K.slot("boots"):
        K.boots("boots", "dark", top=0.455, off=L3, toe_h=0.048)
        K.leg_band("boot_tops", "dark", 0.44, 0.63, off=L3 + 0.001, cls="leather")
        K.limb_straps("boot_straps", "strap", "calf", (0.20, 0.38), width=0.014, off=L3 + 0.005)
        K.rivets("boot_buckles", "brass", [(s * 0.17, 0.02, z) for s in (1, -1) for z in (0.20, 0.38)], r=0.008)
