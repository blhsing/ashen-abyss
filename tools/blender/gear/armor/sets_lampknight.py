"""Lampknight armour looks t1..t3 (t4 = today's hero, split by split_t4.py).

Identity: beacon-keeper knight -- steel, ivory cloth, gilt, the lantern glyph. Hero materials reused: mail, gilt, strap,
glove, boot, emblem, flame.
  t1  scavenged watch gear: dented rusty kettle hat over a padded coif, stained gambeson with one strapped shoulder
      plate and a rope belt, rag wraps, patched hose, worn shoes.
  t2  beacon-watch soldier: open sallet with a mail coif, mail hauberk under a plain belted ivory tabard with the lantern
      glyph, steel spaulders, leather gauntlets with steel cuffs, knee cops, shin-plated boots.
  t3  gilded warden: plumed open bascinet with a gilt brow band, gilt-rimmed cuirass with fauld and layered pauldrons, a
      long ivory cape, plate gauntlets, cuisses and winged poleyns over mail, greaves and sabatons.
"""
from gearkit import *


def _mats():
    mats.metal("lk_rust", base=(0.15, 0.125, 0.105), rough=(0.55, 0.85), rust_amt=0.8, pattern="hammered", ash=0.3, soot=0.2)
    mats.cloth("lk_gambeson", base=(0.215, 0.18, 0.125), var=0.4, ash=0.6, ash_h=0.6, folds=0.6, worn=0.9, scan="canvas", tight=True)
    mats.cloth("lk_rag", base=(0.19, 0.17, 0.14), var=0.4, ash=0.6, folds=0.4, weave=1.7, worn=0.9, tight=True)
    mats.cloth("lk_patch_hose", base=(0.11, 0.095, 0.08), var=0.45, ash=0.55, folds=0.5, worn=0.85, scan="wool", tight=True)
    mats.leather("lk_oldleather", base=(0.075, 0.048, 0.03), worn=0.9, ash=0.5, scan="leather_old")
    mats.cloth("lk_rope", base=(0.20, 0.16, 0.10), var=0.3, ash=0.4, folds=0.0, scan="rope", hem=False)
    mats.metal("lk_steel", base=(0.30, 0.298, 0.29), rough=(0.30, 0.58), rust_amt=0.16, pattern="plate", ash=0.2)
    mats.cloth("lk_tabard", base=(0.40, 0.35, 0.265), folds=0.6, ash=0.4, ash_h=0.5, worn=0.5, scan="linen")
    mats.cloth("lk_hose", base=(0.085, 0.07, 0.056), folds=0.5, ash=0.4, ash_h=0.4, scan="twill", tight=True)
    mats.metal("lk_bright", base=(0.40, 0.395, 0.38), rough=(0.20, 0.46), rust_amt=0.05, pattern="plate", ash=0.12)
    mats.cloth("lk_cape", base=(0.46, 0.405, 0.30), bands=[(0.03, 0.10, (0.42, 0.26, 0.06))], folds=0.9, ash=0.35, ash_h=0.6, worn=0.3, scan="wool")
    mats.cloth("lk_plume", base=(0.42, 0.17, 0.045), var=0.3, folds=0.0, ash=0.1, worn=0.2, scan="fleece", hem=False)
    mats.cloth("lk_apron", base=(0.44, 0.385, 0.285), bands=[(0.04, 0.13, (0.42, 0.26, 0.06))], folds=0.6, ash=0.3, worn=0.3, scan="linen")


def t1(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.cap("coif_pad", "lk_gambeson", z_brow=1.722, ears=True, off=0.009, cls="cloth")
        K.kettle("kettle", "lk_rust", brim=0.178, crown=0.113, height=0.118, z=1.748, dent=0.010)
        K.strap("chin_strap", "lk_oldleather", [(0.094, -0.01, 1.70), (0.086, -0.04, 1.62), (0.045, -0.085, 1.565), (0.0, -0.098, 1.553),
                                               (-0.045, -0.085, 1.565), (-0.086, -0.04, 1.62), (-0.094, -0.01, 1.70)],
                width=0.016, thick=0.004, off=0.006, closed=False, only=["Head"])
    with K.slot("chest"):
        K.tunic("gambeson", "lk_gambeson", lo=0.995, hi=1.51, sleeve=0.345, off=0.014, wrinkle=0.0028, wfreq=24.0, chew=1, chew_p=0.3,
                keep=lambda co: co.z > 1.10 and abs(co.x) < 0.25)
        K.skirt("gambeson_skirt", "lk_gambeson", z_top=1.045, z_bot=0.80, gap_top=0.020, gap_bot=0.038, tails=6, tail_depth=0.10, ragged=0.03,
                folds=((6, 0.03, 0.1), (11, 0.012, 0.5)))
        K.rope("rope_belt", "lk_rope", z=1.06, gap=0.026, r=0.0085, cls="cloth", turns=2)
        K.pauldron("plate", "lk_rust", size=0.86, sides="l", tilt=14.0, th_out=92.0)
        K.strap("plate_strap", "lk_oldleather", [(0.20, -0.02, 1.50), (0.10, -0.13, 1.36), (-0.06, -0.14, 1.22), (-0.16, -0.02, 1.16), (-0.08, 0.14, 1.24),
                                                (0.08, 0.15, 1.38), (0.19, 0.08, 1.49)], width=0.030, off=0.020,
                only=["spine_02", "spine_03", "clavicle_l", "clavicle_r"])
    with K.slot("gloves"):
        K.sleeves("arm_wraps", "lk_rag", x0=0.50, x1=0.725, off=L1, wrinkle=0.002, waxis="x", wfreq=95.0, chew=1)
        K.palm_wrap("palm_wraps", "lk_rag")
    with K.slot("pants"):
        K.pants("hose", "lk_patch_hose", top=1.02, bottom=0.30, wrinkle=0.003, chew=2)
        K.leg_band("knee_patch", "lk_oldleather", 0.47, 0.60, off=L2 + 0.004, chew=1)
    with K.slot("boots"):
        K.boots("shoes", "lk_oldleather", top=0.19, off=L3, toe_h=0.046)
        K.leg_band("ankle_wraps", "lk_rag", 0.165, 0.33, off=L3 + 0.002, cls="cloth", wrinkle=0.002, wfreq=70.0, chew=1)


def t2(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.coif("coif", "mail", off=0.007, z_lo=1.50)
        parts, gh = K.helm("sallet", "lk_steel", style="sallet", trim=None)
        K.nasal("nasal", "lk_steel", z0=1.742, z1=1.668)
    with K.slot("chest"):
        K.tunic("hauberk", "mail", lo=1.0, hi=1.50, sleeve=0.60, off=0.008, cls="metal", smooth=2)
        K.skirt("mail_skirt", "mail", z_top=1.05, z_bot=0.70, gap_top=0.016, gap_bot=0.026, cls="metal", thick=0.004, nv=7, calf=0.0,
                folds=((6, 0.03, 0.15), (11, 0.012, 0.6)))
        K.shell("tab_yoke", K.torso_mask(1.385, 1.52, 0.20) & (np.abs(B.x) < 0.165), "lk_tabard", off=0.021, smooth=3)
        f = K.panel("tab_front", "lk_tabard", -0.118, 0.118, 1.43, 0.58, front=True, gap=0.024, flare=0.16,
                    folds=((1.6, 0.010, 0.15), (3.2, 0.004, 0.6)))
        K.panel("tab_back", "lk_tabard", -0.125, 0.125, 1.44, 0.56, front=False, gap=0.024, flare=0.16, folds=((2.2, 0.011, 0.3),))
        K.belt("belt", "strap", z=1.075, width=0.048, gap=0.040, buckle="lk_steel")
        lamp_sigil(f, 1.27, (0, 1, 0), "sigil")
        K.pauldron("spaulder", "lk_steel", size=0.84, tilt=16.0, th_out=90.0)
    with K.slot("gloves"):
        K.gloves("gloves", "glove", cuff=0.575, off=0.0105)
        K.cuff("cuff", "lk_steel", x=0.635, length=0.075, flare=0.014)
    with K.slot("pants"):
        K.pants("hose", "lk_hose", top=1.02, bottom=0.12, wrinkle=0.003)
        K.kneecop("kneecop", "lk_steel")
    with K.slot("boots"):
        K.boots("boots", "boot", top=0.345, off=L3, cuff=0.004)
        K.leg_plate("shin", "lk_steel", bone="calf", z0=0.15, z1=0.42, off=0.023, y_max=0.045)


def t3(K):
    _mats()
    c, B = K.c, K.B
    with K.slot("helm"):
        K.coif("coif", "mail", off=0.007, z_lo=1.505)
        parts, gh = K.helm("bascinet", "lk_bright", style="open", trim="gilt")
        band, P = K.circlet("brow_band", "gilt", z=1.752, arc=180.0, r=0.0075, lift=0.129 - 0.108 + 0.004, rise=0.0)
        # plume: a swept crest of feather locks from the crown to the nape
        locks = []
        for i, t in enumerate(np.linspace(0.0, 1.0, 9)):
            a = math.radians(-18 + 118 * t)
            p = np.array((0.0, -0.012 + 0.118 * math.sin(a), 1.70 + 0.150 * math.cos(a)))
            d = np.array((0.0, 0.55 + 0.5 * t, 0.9 - 0.9 * t))
            locks.append(tuft(p, d, length=0.13 + 0.05 * math.sin(3.1 * t), r=0.024, bend=0.05, n=6))
        add("plume", gjoin(*locks), "cloth", "lk_plume", bone="Head", sharp=180)
        add("plume_base", gxf(g_lathe([(0.016, 0.0), (0.013, 0.02), (0.008, 0.034)], n=8), T(0.0, -0.030, 1.842)), "metal", "gilt", bone="Head", sharp=50)
    with K.slot("chest"):
        K.tunic("mail", "mail", lo=1.0, hi=1.50, sleeve=0.505, off=0.008, cls="metal", smooth=2)
        cu = K.cuirass("cuirass", "lk_bright", z0=1.19, z1=1.52, gap=0.025)
        K.band_plate("plackart", "lk_bright", 1.055, 1.215, gap0=0.024, gap1=0.026, bone="spine_02")
        K.band_plate("fauld1", "lk_bright", 0.975, 1.06, gap0=0.040, gap1=0.026, bone="spine_01")
        K.band_plate("fauld2", "lk_bright", 0.895, 1.0, gap0=0.052, gap1=0.040, bone="pelvis")
        st = K.stations([1.19, 1.215], 0.025)
        gg = g_stations(st, nu=28, nv=2, p=2.3)
        K.trim("cuirass_trim", "gilt", rim_of(gg, 28, 0), r=0.0065, bone="spine_03")
        st2 = K.stations([0.895, 0.92], [0.052, 0.050])
        K.trim("fauld_trim", "gilt", rim_of(g_stations(st2, nu=28, nv=2, p=2.3), 28, 0), r=0.006, bone="pelvis")
        K.pauldron("pauldron", "lk_bright", size=1.08, trim="gilt", tilt=20.0)
        K.arm_lames("lame", "lk_bright")
        K.cloak("cape", "lk_cape", z_top=1.47, z_bot=0.44, width=0.215, arc=0.40, pleats=6.0, pleat_amp=0.07, top_taper=0.9)
        K.panel("apron", "lk_apron", -0.085, 0.085, 0.99, 0.60, front=True, gap=0.060, flare=0.10, point=0.07, folds=((1.5, 0.007, 0.2),))
        K.belt("belt", "strap", z=1.07, width=0.045, gap=0.034, buckle="gilt")
        lamp_sigil(cu, 1.33, (0, 1, 0), "sigil", s=0.9, cls="metal", src="gilt", bone="spine_03")
    with K.slot("gloves"):
        K.gloves("gauntlets", "lk_bright", cuff=0.70, off=0.006, cls="metal", smooth=1)
        K.vambrace("vambrace", "lk_bright", x0=0.515, x1=0.70, off=0.016)
        K.cuff("cuff", "lk_bright", x=0.64, flare=0.020, trim="gilt")
        K.couter("couter", "lk_bright", wing="gilt")
    with K.slot("pants"):
        K.pants("mail_legs", "mail", top=1.02, bottom=0.12, off=0.0085, cls="metal", smooth=2)
        K.leg_plate("cuisse", "lk_bright", bone="thigh", z0=0.61, z1=0.87, off=0.018)
        K.kneecop("poleyn", "lk_bright", wing="gilt")
    with K.slot("boots"):
        K.boots("sabatons", "lk_bright", top=0.17, off=0.017, cls="metal", smooth=2, toe_h=0.050, toe_grow=0.012)
        K.leg_plate("greave", "lk_bright", bone="calf", z0=0.135, z1=0.455, off=0.019, front_only=False)
