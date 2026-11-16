"""npc_occultist -- 祕術師 薇菈, the masked enchantress of Lastlamp: a blank porcelain mask (slit eyes, one violet tear line)
inside a deep hood, a brass charm-hoop standing behind the head hung with violet crystals and porcelain tablets, a
two-tier shoulder tippet, an ash-lilac over-robe with hanging bell sleeves and a tailed, open skirt over a dark plum
gown, a wide sash with charm cords and a chained inscription tome. No skin shows (wimple, wrapped hands)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_twn import *

CID = "npc_occultist"
c = start("female")
B = c.B

# ------------------------------------------------------------------ source materials
mats.cloth("gown", base=(0.042, 0.028, 0.048), folds=0.9, ash=0.55, ash_h=0.5, worn=0.5, var=0.3)
mats.cloth("gown_under", base=(0.042, 0.028, 0.048), folds=0.4, ash=0.1, worn=0.4, var=0.3)
mats.cloth("robe_wool", base=(0.185, 0.170, 0.205), bands=[(0.0, 0.05, (0.075, 0.045, 0.11))], folds=0.9, ash=0.5, ash_h=0.6, worn=0.6, var=0.35)
mats.cloth("robe_wool_top", base=(0.185, 0.170, 0.205), folds=0.5, ash=0.15, worn=0.6, var=0.35)
mats.cloth("sleeve_wool", base=(0.185, 0.170, 0.205), bands=[(0.88, 1.0, (0.075, 0.045, 0.11))], folds=0.5, ash=0.15, worn=0.6, var=0.35)
mats.cloth("hood_tippet", base=(0.070, 0.050, 0.100), folds=0.6, ash=0.3, ash_h=0.5, worn=0.6, var=0.35)
mats.cloth("cowl_pale", base=(0.150, 0.130, 0.175), folds=0.5, ash=0.25, worn=0.6, var=0.3)
mats.cloth("sash_velvet", base=(0.090, 0.030, 0.110), folds=0.3, ash=0.15, worn=0.4)
mats.cloth("wrap_dark", base=(0.030, 0.022, 0.034), folds=0.2, ash=0.1, worn=0.4, weave=2.0)
mats.cloth("void", base=(0.006, 0.005, 0.008), folds=0.0, ash=0.0, worn=0.0)
mats.flat("cord", (0.11, 0.08, 0.13), rough=0.9)
mats.flat("paper", (0.36, 0.31, 0.22), rough=0.85)
mats.leather("shoe", base=(0.030, 0.022, 0.026), ash=0.5, ash_h=0.3)
mats.leather("tome", base=(0.050, 0.030, 0.070), ash=0.0, worn=0.7)
mats.porcelain("porcelain", base=(0.66, 0.63, 0.57), crackle=1.0)
mats.porcelain("tablet", base=(0.52, 0.49, 0.44), crackle=1.0)
mats.metal("brass", base=(0.34, 0.225, 0.07), rough=(0.30, 0.56), rust=(0.04, 0.07, 0.05), rust_amt=0.3, ash=0.1)
mats.emis("violet", (0.50, 0.16, 1.0), strength=4.2)

# ------------------------------------------------------------------ nothing bare: wimple, fitted gown top, wrapped hands, shoes
mwi = (B.w("Head", "neck_01") > 0.5) & (B.z > 1.47) & (B.y < -0.012)
shell(B, mwi, 0.006, "wimple", "cloth", "wrap_dark", smooth=1, sharp=180)
mu = (B.w("lowerarm", "upperarm") > 0.5) & (np.abs(B.x) > 0.44) & (np.abs(B.x) < 0.72) & (B.w(*BONES_HAND) < 0.5)
shell(B, mu, 0.007, "undersleeves", "cloth", "gown_under", smooth=2, sharp=180)
gloves(c, "wrap_dark", cuff=0.70, offset=0.004, cls="cloth", name="hand_wraps", smooth=0)
toe_box(boots(c, "shoe", top=0.20, offset=0.012, toe_cap=False))
drop_skin(c)

# ------------------------------------------------------------------ dark gown skirt (deep folds) under the open, tailed over-robe
GOWN = [(0.055, 0, 0.02, 0.246, 0.200, 0.226), (0.50, 0, 0.012, 0.214, 0.180, 0.196), (0.92, 0, 0.02, 0.196, 0.166, 0.172),
        (1.06, 0, 0.012, 0.152, 0.114, 0.120), (1.12, 0, 0.0, 0.134, 0.098, 0.102)]
robe(c, "gown", GOWN, nu=44, nv=14, name="gown_skirt", calf_to_thigh=0.55, ragged=0.012,
     mod=fold_mod([(8, 0.085, 0.3), (13, 0.028, 0.1)], cy=0.015, hem_pow=1.0, top=0.05, sag=0.008))
mc = (B.w(*BONES_TORSO, "clavicle") > 0.5) & (B.z > 1.06) & (B.z < 1.485) & (np.abs(B.x) < 0.19)
rt = shell(B, mc, 0.026, "robe_top", "cloth", "robe_wool_top", smooth=9, sharp=180)
wrinkle_shell(rt, 0.004, 34.0, "z", 2, mask=lambda co: sstep(co[:, 2], 1.28, 1.15))
OUT = [(0.20, 0, 0.02, 0.290, 0.240, 0.268), (0.55, 0, 0.012, 0.252, 0.212, 0.232), (0.92, 0, 0.02, 0.222, 0.190, 0.196),
       (1.075, 0, 0.012, 0.176, 0.136, 0.142), (1.14, 0, 0.0, 0.158, 0.120, 0.124)]
GAPU = 0.052
robe(c, "robe_wool", OUT, nu=34, nv=13, name="robe_skirt", thick=0.006, u0=0.75 + GAPU, u1=1.75 - GAPU, close=False, calf_to_thigh=0.55,
     mod=tails_mod(k=5, depth=0.26, seed=4, power=1.7, notch=1.0, uneven=0.45, extra=fold_mod([(6, 0.06, 0.4), (11, 0.02, 0.2)], cy=0.015, top=0.05)))
sl = sleeve(c, "sleeve_wool", [(0.165, 0.0, 0.0, 0.080, 0.074, 0.074), (0.32, 0.010, 0.0, 0.076, 0.068, 0.068), (0.47, 0.034, 0.0, 0.094, 0.074, 0.074),
                               (0.60, 0.052, 0.0, 0.120, 0.084, 0.084), (0.675, 0.078, 0.0, 0.152, 0.090, 0.090)], n=16, nv=11, mirror=False)
wrinkle_shell(sl, 0.0055, 30.0, "x", 6, mask=lambda co: 0.4 + 0.6 * np.exp(-((co[:, 0] - 0.45) / 0.09) ** 2))
solidify(sl, 0.006)
mirrored(sl)

# chest plastron: a stiff embroidered panel that hangs plumb from the bust, so the torso reads as layered cloth
PL = [(z,) + body_ring(c, z, 0.030) for z in (1.06, 1.12, 1.18, 1.24, 1.30, 1.36, 1.42, 1.47)]
yb = min(q[2] - q[4] for q in PL)
PL = [q if q[0] > 1.34 else (q[0], q[1], q[2], q[3], max(q[4], q[2] - yb - 0.012 * (1.34 - q[0]) / 0.28), q[5]) for q in PL]
pl = drape("plastron", -0.088, 0.088, 1.462, 1.135, front_y(PL, 0.004), "cloth", "sash_velvet", nu=9, nv=9, thick=0.005, folds=((1.5, 0.004, 0.3),),
           top=0.6, hem_pow=1.0, flare=-0.14, point=0.035)
w_func(pl, lambda co: {"spine_03": sstep(co[:, 2], 1.22, 1.30), "spine_02": 1 - sstep(co[:, 2], 1.22, 1.30)})
apply_mods(pl)
for k, zz in enumerate((1.40, 1.31, 1.22)):
    decal("plastron_rune_%d" % k, rhomb(0.010 + 0.003 * (k == 1), 0.022 + 0.006 * (k == 1)), pl, (0.0, -0.6, zz), "emis", "violet", ray=(0, 1, 0),
          depth=0.004, lift=0.002)

# ------------------------------------------------------------------ sash, clasp, charm cords, chained tome
SZ = 1.09
scy = body_ring(c, SZ)[1]
srx, sry = body_ring(c, SZ, 0.034)[2], body_ring(c, SZ, 0.034)[3]
belt(c, SZ, "sash_velvet", width=0.10, thick=0.008, cls="cloth", name="sash", rx=srx, ry=sry)
props("clasp", frame((0.0, scy - sry - 0.012, SZ), (0, -1, 0), (0, 0, 1)), [
    (g_lathe([(0.0, 0.012), (0.020, 0.010), (0.040, 0.003), (0.044, -0.004), (0.0, -0.004)], n=12), "metal", "brass", dict(sharp=40)),
    (gxf(g_gem(0.016, 0.022, 6, 0.6), T(0, 0, 0.010)), "emis", "violet", dict(sharp=20)),
], "spine_01")
def hip_w(side):
    def f(co):
        t = sstep(1.03 - co[:, 2], 0.0, 0.28) * 0.8
        return {"pelvis": 1 - t, "thigh_" + side: t}
    return f
rng = np.random.RandomState(7)
for i, (ad, ln) in enumerate(((-128, 0.20), (-118, 0.29), (-108, 0.16), (-97, 0.25), (-64, 0.22), (-52, 0.14))):
    a = math.radians(ad)
    p0 = np.array((srx * math.cos(a), scy + sry * math.sin(a), SZ - 0.045)) + np.array((math.cos(a), math.sin(a), 0)) * 0.012
    p1 = p0 + np.array((math.cos(a) * 0.012, math.sin(a) * 0.012, -ln))
    gs = [g_tube([p0, (p0 + p1) / 2 + np.array((0, 0, 0.004)), p1], 0.0032, n=4)]
    o = add("charm_cord_%d" % i, gjoin(*gs), "cloth", "cord", sharp=180)
    w_func(o, hip_w("r" if ad < -90 else "l"))
    if i % 2 == 0:
        ch = add("charm_gem_%d" % i, gxf(g_gem(0.013, 0.050, 6, 0.45), T(*p1) @ R('X', 180) @ T(0, 0, -0.018)), "emis", "violet", sharp=20)
    else:
        ch = add("charm_tab_%d" % i, gxf(g_box(0.026, 0.006, 0.046), T(*(p1 - np.array((0, 0, 0.024)))) @ R('Z', math.degrees(a) + 90)), "bone", "tablet",
                 bevel=0.002, sharp=40)
    w_func(ch, hip_w("r" if ad < -90 else "l"))
cover, pages = g_book(0.118, 0.158, 0.042)
BK = T(-srx - 0.030, scy + 0.035, 0.900) @ R('Z', -92) @ R('Y', 6)
for o in props("tome", BK, [
        (cover, "leather", "tome", dict(bevel=0.003, sharp=40)), (pages, "cloth", "paper", dict(sharp=40)),
        (gxf(g_prism(rhomb(0.014, 0.024), 0.004), T(-0.008, -0.0225, 0.0)), "emis", "violet", dict(sharp=20)),
        (gjoin(*[gxf(g_corner(0.028), T(0.059 * sx_, -0.0222, 0.079 * sz_) @ R('Y', {(1, 1): 180, (1, -1): -90, (-1, 1): 90, (-1, -1): 0}[(sx_, sz_)]))
                 for sx_ in (1, -1) for sz_ in (1, -1)]), "metal", "brass", dict(sharp=30))], "pelvis"):
    w_func(o, hip_w("r"))
add("tome_chain", g_chain([(-srx - 0.030, scy + 0.035, 0.985), (-srx - 0.022, scy + 0.02, 1.03), (-srx - 0.004, scy + 0.005, SZ - 0.03)], link=0.022, wire=0.003),
    "metal", "brass", bone="pelvis", sharp=60)

# ------------------------------------------------------------------ two-tier tippet with radiating folds
def tier(name, src, z0, slope, xmax, off, seed, amp):
    zmin = z0 - slope * B.y
    core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zmin) & (B.z < 1.545) & (np.abs(B.x) < 0.21)
    arms = (B.w("upperarm", "clavicle") > 0.5) & (np.abs(B.x) < xmax) & (B.z > 1.37)
    m = (core | arms) & (B.w(*BONES_HAND, "lowerarm") < 0.3)
    o = shell(B, m, lambda co: off + 0.014 * sstep(np.abs(co[:, 0]), 0.15, 0.22), name, "cloth", src, smooth=6, sharp=180)
    co = get_co(o)
    ang = np.arctan2(co[:, 1] - 0.02, co[:, 0])
    rr = np.maximum(np.hypot(co[:, 0], co[:, 1] - 0.02), 1e-6)
    d = amp * np.sin(11 * ang + seed + 1.3 * np.sin(3 * ang + seed)) * sstep(1.51 - co[:, 2], 0.0, 0.10)
    co[:, 0] += co[:, 0] / rr * d
    co[:, 1] += (co[:, 1] - 0.02) / rr * d
    set_co(o, co)
    return o
tier("tippet_low", "hood_tippet", 1.165, 0.45, 0.385, 0.040, 1.0, 0.008)
tier("tippet_high", "cowl_pale", 1.335, 0.32, 0.300, 0.056, 2.4, 0.006)

# ------------------------------------------------------------------ deep hood, porcelain mask, charm hoop
HST = [(1.470, 0.030, 0.186, 0.126, 0.166), (1.530, 0.020, 0.132, 0.110, 0.140), (1.580, 0.000, 0.118, 0.122, 0.128),
       (1.640, -0.012, 0.123, 0.146, 0.133), (1.700, -0.018, 0.129, 0.158, 0.141), (1.760, -0.018, 0.123, 0.154, 0.139),
       (1.815, -0.012, 0.102, 0.126, 0.122), (1.858, -0.004, 0.066, 0.080, 0.086), (1.884, 0.0, 0.0, 0.0, 0.0)]
hd, gh = cowl(c, "hood_tippet", HST, name="hood", thick=0.008, chin=1.548, brow=1.782, open_deg=62.0, peak=0.026, nu=28, nv=18)
MW, MZ0, MZ1, MZC = 0.074, 1.558, 1.772, 1.672
def mask_w(V):
    z = MZ0 + (MZ1 - MZ0) * np.asarray(V, float)
    lo = np.clip((MZC - z) / (MZC - MZ0), 0, 1)
    hi = np.clip((z - MZC) / (MZ1 - MZC), 0, 1)
    return np.maximum(MW * (1 - 0.62 * lo ** 2.0) * (1 - 0.42 * hi ** 2.4), 0.012)
def mask_y(x, z):
    x, z = np.asarray(x, float), np.asarray(z, float)
    return (-0.150 + 0.030 * (x / MW) ** 2 + 0.030 * ((z - MZC) / 0.107) ** 2
            - 0.013 * np.exp(-(x / 0.013) ** 2) * np.exp(-((z - 1.652) / 0.036) ** 2)
            + 0.006 * np.exp(-((np.abs(x) - 0.032) / 0.016) ** 2) * np.exp(-((z - 1.694) / 0.016) ** 2))
def mask_fn(U, V):
    z = MZ0 + (MZ1 - MZ0) * V
    x = (2 * U - 1) * mask_w(V)
    return np.stack([x, mask_y(x, z), z], axis=-1)
mask = add("mask", g_grid(mask_fn, 13, 15, close_u=False), "bone", "porcelain", solidify=0.006, bone="Head", sharp=180)
apply_mods(mask)
eye = [(-0.019, 0.001), (-0.007, 0.0052), (0.010, 0.0046), (0.019, -0.0012), (0.006, -0.0044), (-0.009, -0.0038)]
for s in (1, -1):
    decal("mask_eye_%d" % s, [(s * x, z) for (x, z) in eye][::s], mask, (s * 0.033, -0.5, 1.694), "cloth", "void", ray=(0, 1, 0), depth=0.003, lift=0.0008, bone="Head")
decal("mask_tear", [(-0.0026, 0.030), (0.0026, 0.030), (0.0018, -0.034), (-0.0018, -0.034)], mask, (0.033, -0.5, 1.652), "emis", "violet", ray=(0, 1, 0),
      depth=0.003, lift=0.001, bone="Head")
decal("mask_brow", rhomb(0.006, 0.012), mask, (0.0, -0.5, 1.742), "emis", "violet", ray=(0, 1, 0), depth=0.003, lift=0.001, bone="Head")
HC, HR = np.array((0.0, 0.132, 1.712)), 0.196
HM = T(*HC) @ R('X', -8)
halo = [(gxf(g_ring(HR, 0.0062, n=26, m=5), R('X', 90)), "metal", "brass", dict(sharp=60)),
        (gjoin(*[g_tube([(s * 0.060, -0.030, 0.030), (s * 0.105, -0.006, 0.085), (s * 0.140, 0.0, 0.137)], 0.0055, n=5) for s in (-1, 1)]), "metal", "brass", dict(sharp=60)),
        (gxf(g_gem(0.016, 0.050, 6, 0.6), T(0, 0, HR + 0.012)), "emis", "violet", dict(sharp=20)),
        (gxf(g_lathe([(0.0, 0.0), (0.016, 0.004), (0.020, 0.014), (0.012, 0.022)], n=6), T(0, 0, HR - 0.008)), "metal", "brass", dict(sharp=50))]
cords, gems, tabs = [], [], []
for i, (ad, ln) in enumerate(((-26, 0.060), (-10, 0.050), (6, 0.085), (22, 0.055), (37, 0.045), (143, 0.045), (158, 0.060), (174, 0.085), (190, 0.050), (206, 0.060))):
    a = math.radians(ad)
    p0 = np.array((HR * math.cos(a), 0.0, HR * math.sin(a)))
    p1 = p0 + np.array((0.0, 0.004, -ln))
    cords.append(g_tube([p0, p1], 0.0026, n=4))
    if i % 2 == 0:
        gems.append(gxf(g_gem(0.0105, 0.040, 6, 0.45), T(*p1) @ R('X', 180) @ T(0, 0, -0.014)))
    else:
        tabs.append(gxf(g_box(0.020, 0.005, 0.034), T(*(p1 - np.array((0, 0, 0.018)))) @ R('Z', 10 * (i - 4))))
halo += [(gjoin(*cords), "cloth", "cord", dict(sharp=180)), (gjoin(*gems), "emis", "violet", dict(sharp=20)),
         (gjoin(*tabs), "bone", "tablet", dict(bevel=0.0015, sharp=40))]
props("halo", HM, halo, "Head")

report_unweighted()
tri_report()
finalize(c, CID, max_tris=20000, decimate={"cloth": 0.80},
         notes="Pose-tested: Idle_Loop, Idle_FoldArms_Loop, Idle_Talking_Loop, Walk_Loop. No skin or hair material: the porcelain mask and charm tablets "
               "are bone_, the charm hoop is metal_ and rigid to Head, the crystals / tear line are emis_ (violet).")
