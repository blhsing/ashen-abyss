"""hero_hunter -- hooded skirmisher: back-swept pointed hood over a half mask, ragged shoulder cape that drapes the
left arm, fitted leather jerkin with a tabbed peplum, bracers, knee cops, bandolier and a back quiver of teal-fletched
bolts. Hand crossbow in the right hand (wpn_N) + dagger carried in a reverse grip in the left (wpn_N_off)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_mel import *

CID = "hero_hunter"
c = start("female")
B = c.B

# ------------------------------------------------------------------ source materials
mats.cloth("hood", base=(0.026, 0.040, 0.046), folds=0.5, ash=0.15, ash_h=0.6, worn=0.5, var=0.35)
mats.cloth("cloak", base=(0.030, 0.045, 0.050), folds=0.9, ash=0.55, ash_h=1.0, worn=0.6, var=0.35)
mats.cloth("under", base=(0.075, 0.072, 0.064), folds=0.5, ash=0.2, worn=0.5)
mats.cloth("hose", base=(0.028, 0.038, 0.044), folds=0.5, ash=0.35, ash_h=0.4)
mats.cloth("mask", base=(0.020, 0.024, 0.027), folds=0.2, ash=0.0, worn=0.3)
mats.leather("jerkin", base=(0.058, 0.038, 0.024), worn=0.55, ash=0.2, ash_h=0.5)
mats.leather("dark", base=(0.030, 0.021, 0.016), ash=0.25, ash_h=0.22)
mats.leather("strap", base=(0.085, 0.052, 0.028), worn=0.7)
mats.wood("wood", base=(0.090, 0.052, 0.028))
mats.wood("woodbox", base=(0.090, 0.052, 0.028), luv_grain=False)
mats.wood("darkwood", base=(0.030, 0.022, 0.020))
mats.wood("darkbox", base=(0.030, 0.022, 0.020), luv_grain=False)
mats.flat("rope", (0.20, 0.16, 0.10), rough=0.9)
mats.metal("iron", base=(0.16, 0.155, 0.15), rough=(0.45, 0.7), rust_amt=0.4, ash=0.0, pattern="hammered")
mats.metal("steel", base=(0.40, 0.40, 0.41), rough=(0.22, 0.45), rust_amt=0.1, ash=0.0, pattern="brushed", edge_amt=1.0)
mats.metal("brass", base=(0.32, 0.21, 0.07), rough=(0.3, 0.55), rust=(0.04, 0.07, 0.05), rust_amt=0.2, ash=0.0)
mats.metal("darksteel", base=(0.075, 0.085, 0.095), rough=(0.25, 0.5), rust_amt=0.06, ash=0.0, edge=(0.45, 0.52, 0.55), edge_amt=1.0)
mats.emis("teal", (0.0, 0.82, 0.50), strength=3.6)

# ------------------------------------------------------------------ under layer
shirt(c, "under", bottom=1.0, top=1.50, sleeve=0.72, offset=0.006, smooth=2, name="undershirt")
trousers(c, "hose", top=1.05, bottom=0.40, offset=0.009, smooth=3)
toe_box(boots(c, "dark", top=0.47, offset=0.013, cuff=0.007, toe_cap=False))
gk = g_sphere(1.0, 10, 6)
kc = add("kneecop_l", gxf(gk, T(0.083, -0.016, 0.537) @ S(0.060, 0.050, 0.070)), "leather", "dark", sharp=60)
w_func(kc, lambda co: {"thigh_l": 0.45, "calf_l": 0.55})
mirrored(kc)

# ------------------------------------------------------------------ fitted jerkin + tabbed peplum
vneck = (np.abs(B.x) < 0.010 + 0.22 * (B.z - 1.30)) & (B.y < 0.0) & (B.z > 1.30)
mj = (B.w(*BONES_TORSO, "clavicle") > 0.5) & (B.z > 1.04) & (B.z < 1.47) & (np.abs(B.x) < 0.19) & ~vneck
jer = shell(B, mj, 0.016, "jerkin", "leather", "jerkin", smooth=6, sharp=180)
PEP = [(0.75, 0, 0.0, 0.226, 0.172, 0.162), (0.90, 0, 0.016, 0.202, 0.152, 0.170), (1.068, 0, 0.014, 0.168, 0.118, 0.120)]
pep = robe(c, "jerkin", PEP, nu=32, nv=8, name="peplum", cls="leather", thick=0.006, u0=0.75 + 0.045, u1=1.75 - 0.045, close=False,
           calf_to_thigh=0.0, mod=tails_mod(k=6, depth=0.12, seed=3, power=2.0, notch=1.0, uneven=0.35))
BZ = 1.064
belt(c, BZ, "strap", width=0.052, thick=0.009, gap=0.027, buckle="iron")
# belt pouches (left hip) and a glowing vial (right front)
props("pouch_a", T(0.172, -0.035, 0.985) @ R('Z', 22), [(g_box(0.066, 0.046, 0.088), "leather", "dark", dict(bevel=0.011, bevel_seg=2, sharp=60)),
                                                        (g_box(0.070, 0.050, 0.030, (0, 0, 0.034)), "leather", "strap", dict(bevel=0.006, sharp=60))], "pelvis")
props("pouch_b", T(0.118, -0.098, 0.990) @ R('Z', 52), [(g_box(0.054, 0.040, 0.070), "leather", "dark", dict(bevel=0.010, bevel_seg=2, sharp=60))], "pelvis")
VP = wrap_path(c, [(-0.105, -0.4, 0.972)], 0.058)[0]
props("vial", T(*VP), [(g_cyl(0.0135, -0.034, 0.022, 8), "emis", "teal", dict(sharp=60)),
                       (g_lathe([(0.0145, 0.020), (0.0155, 0.030), (0.008, 0.040), (0.008, 0.052), (0.0, 0.054)], n=8), "metal", "brass", dict(sharp=50)),
                       (g_tube([(0, 0.0, 0.045), (0, 0.012, 0.075), (0, 0.022, 0.095)], 1.0, n=6, sx=0.003, sy=0.006), "leather", "strap", dict(sharp=60))],
      "pelvis")

# ------------------------------------------------------------------ bandolier (right shoulder -> left hip) with spare bolts
guide = [(-0.115, 0.03, 1.50), (-0.095, -0.10, 1.43), (-0.035, -0.14, 1.33), (0.045, -0.12, 1.21), (0.115, -0.09, 1.11), (0.155, 0.0, 1.085),
         (0.115, 0.10, 1.13), (0.035, 0.13, 1.26), (-0.055, 0.13, 1.40), (-0.105, 0.10, 1.48)]
BONES_BAND = ["spine_01", "spine_02", "spine_03", "clavicle_r", "clavicle_l", "neck_01", "pelvis"]
bando, BQ, BN = ribbon(c, guide, "strap", width=0.044, thick=0.008, offset=0.007, name="bandolier", only=BONES_BAND, host=jer)
idx = [i for i in range(len(BQ)) if BQ[i][1] < -0.04 and 1.20 < BQ[i][2] < 1.40]
sp, lp = [], []
for i in [idx[int(round(k))] for k in np.linspace(1, len(idx) - 2, 3)]:
    t = BQ[(i + 1) % len(BQ)] - BQ[i - 1]
    bn = np.cross(t, BN[i])
    bn = bn / np.linalg.norm(bn) * (1 if bn[2] > 0 else -1)
    M = frame(BQ[i] + BN[i] * 0.017, bn, BN[i])
    sp.append(gxf(gjoin(g_cyl(0.0068, -0.060, 0.040, 6), g_lathe([(0.0, 0.036), (0.0125, 0.042), (0.0, 0.078)], n=4)), M))
    lp.append(gxf(g_box(0.026, 0.022, 0.030), M))
spo = add("spare_bolts", gjoin(*sp), "metal", "iron", sharp=50)
w_transfer(spo, c.ref, smooth=0, only=["spine_02", "spine_03"])
lpo = add("bolt_loops", gjoin(*lp), "leather", "dark", sharp=50)
w_transfer(lpo, c.ref, smooth=0, only=["spine_02", "spine_03"])

# ------------------------------------------------------------------ ragged shoulder cape: draped over the left arm, cut short on the right
armL = (B.x > 0) & (B.w("upperarm", "clavicle") > 0.5) & (B.x < 0.435) & (B.z > 1.418)
armR = (B.x < 0) & (B.w("upperarm", "clavicle") > 0.5) & (B.x > -0.275) & (B.z > 1.40)
zmin = 1.365 - 0.55 * B.y
core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zmin) & (B.z < 1.545) & (np.abs(B.x) < 0.20)
mm = (core | armL | armR) & (B.w(*BONES_HAND, "lowerarm") < 0.3)
mantle = shell(B, mm, 0.030, "mantle", "cloth", "cloak", smooth=4, sharp=180, solidify=0.006)
chew_edge(mantle, rounds=2, p=0.36, seed=3, keep=lambda co: co.z > 1.47 and abs(co.x) < 0.13)

# short back cloak with a diagonal torn hem (long on the left)
CK = [(0.80, 0, 0.02, 0.235, 0.18, 0.222), (1.10, 0, 0.012, 0.188, 0.14, 0.166), (1.30, 0, 0.0, 0.180, 0.14, 0.150), (1.43, 0, 0.012, 0.168, 0.13, 0.140)]
def diag(U, V, x, y, z):
    return x, y, z + 0.20 * sstep(U, 0.14, 0.40) * (1 - V) ** 1.2
robe(c, "cloak", CK, nu=24, nv=14, name="backcloak", pleats=9, pleat_amp=0.05, thick=0.006, ragged=0.02, u0=0.085, u1=0.415, close=False,
     calf_to_thigh=0.6, mod=tails_mod(k=4, depth=0.20, seed=7, power=1.8, extra=diag))

# ------------------------------------------------------------------ hood (pointed, swept back), cowl roll, half mask
HST = [(1.455, 0.030, 0.165, 0.122, 0.150), (1.52, 0.020, 0.112, 0.098, 0.122), (1.56, 0.004, 0.098, 0.106, 0.108),
       (1.62, -0.012, 0.106, 0.128, 0.124), (1.69, -0.012, 0.115, 0.142, 0.140), (1.75, -0.006, 0.108, 0.136, 0.146),
       (1.80, 0.008, 0.088, 0.112, 0.144), (1.84, 0.034, 0.060, 0.076, 0.122), (1.872, 0.074, 0.030, 0.038, 0.076),
       (1.892, 0.125, 0.0, 0.0, 0.0)]
hd, gh = cowl(c, "hood", HST, name="hood", thick=0.008, chin=1.585, brow=1.752, open_deg=56.0, peak=0.046, nu=28, nv=20)
path = []
for i in range(18):
    a = TAU * i / 18
    s_, c_ = math.sin(a), math.cos(a)
    path.append((0.112 * c_, 0.030 + (0.100 * s_ if s_ > 0 else 0.112 * s_), 1.512 + (0.014 * s_ if s_ > 0 else 0.052 * s_)))
sc = fur_roll(path, 0.033, "hood", name="cowl_roll", noise=0.006, scale=40.0, n=8)
w_func(sc, lambda co: {"neck_01": 0.4, "spine_03": 0.6})
mk_ = (B.w("Head", "neck_01") > 0.5) & (B.z < 1.664) & (B.z > 1.50) & (B.y < 0.012)
mask = shell(B, mk_, 0.007, "mask", "cloth", "mask", smooth=2, sharp=180)
face = (B.w("Head") > 0.5) & (B.z > 1.652) & (B.z < 1.775) & (B.y < -0.02)
hide(c, ~face)
log("eyes bbox", np.round(get_co(c.eyes).min(0), 3), np.round(get_co(c.eyes).max(0), 3))
# cloak clasp at the throat
CP = wrap_path(c, [(0.0, -0.4, 1.425)], 0.046)[0]
props("clasp", frame(CP, (0, -1, 0.25), (0, 0, 1)), [
    (g_lathe([(0.0, 0.010), (0.016, 0.009), (0.034, 0.003), (0.038, -0.004), (0.0, -0.004)], n=10), "metal", "iron", dict(sharp=40)),
    (gxf(g_gem(0.015, 0.02, 6, 0.6), T(0, 0, 0.008)), "emis", "teal", dict(sharp=20)),
], "spine_03")

# ------------------------------------------------------------------ bracers + gloves
mb = (B.w("lowerarm") > 0.5) & (np.abs(B.x) > 0.495) & (np.abs(B.x) < 0.708)
brc = shell(B, mb, 0.012, "bracers", "leather", "dark", smooth=2, sharp=180)
def arm_band(x, r, w=0.022, t=0.006, sx=1.0, sy=1.0):
    return gxf(g_lathe([(r, -w / 2), (r + t, -w / 2), (r + t, w / 2), (r, w / 2), (r, -w / 2)], n=12, sx=sx, sy=sy), frame((x, 0.0675, 1.4385), (1, 0, 0), (0, 1, 0)))
ab = add("bracer_bands_l", gjoin(arm_band(0.525, 0.0615, 0.024, 0.0045, 1.0, 0.90), arm_band(0.612, 0.0535, 0.018, 0.004, 1.0, 0.92)), "metal", "iron",
         bone="lowerarm_l", sharp=40)
mirrored(ab)
gloves(c, "dark", cuff=0.70, offset=0.004)

# ------------------------------------------------------------------ back quiver with teal-fletched bolts (rides on the bandolier)
QA, QB = np.array((0.050, 0.212, 1.135)), np.array((-0.125, 0.208, 1.455))
QL = float(np.linalg.norm(QB - QA))
QM = frame(QA, QB - QA, (0, 1, 0))
qparts = [
    (g_lathe([(0.0, -0.022), (0.016, -0.018), (0.026, 0.0), (0.033, 0.06), (0.044, QL - 0.05), (0.052, QL), (0.046, QL), (0.040, QL - 0.03), (0.0, QL - 0.04)], n=10, sy=0.74),
     "leather", "jerkin", dict(sharp=50)),
    (g_lathe([(0.0, -0.030), (0.014, -0.026), (0.028, -0.004), (0.031, 0.022), (0.026, 0.022)], n=10, sy=0.74), "metal", "iron", dict(sharp=50)),
    (g_lathe([(0.049, QL - 0.040), (0.056, QL - 0.040), (0.058, QL + 0.005), (0.050, QL + 0.005), (0.049, QL - 0.040)], n=10, sy=0.74), "leather", "dark", dict(sharp=40)),
    (g_lathe([(0.036, 0.13), (0.042, 0.13), (0.043, 0.155), (0.037, 0.155), (0.036, 0.13)], n=10, sy=0.74), "leather", "strap", dict(sharp=40)),
]
rng = np.random.RandomState(4)
shafts, fletch, nocks = [], [], []
for i, (bx, by, bh) in enumerate(((0.0, 0.004, 0.115), (0.022, 0.010, 0.085), (-0.022, 0.006, 0.100), (0.010, -0.012, 0.070), (-0.012, -0.012, 0.092))):
    top = QL + bh
    shafts.append(gxf(g_cyl(0.005, QL - 0.06, top, 5), T(bx, by, 0)))
    nocks.append(gxf(g_cyl(0.0065, top, top + 0.010, 5), T(bx, by, 0)))
    ang = rng.rand() * 180
    for k in range(2):
        fletch.append(gxf(g_feather(0.092, 0.046, 0.003), T(bx, by, top - 0.004) @ R('Z', ang + 90 * k) @ R('X', 180)))
qparts += [(gjoin(*shafts), "leather", "wood", dict(sharp=60)), (gjoin(*nocks), "metal", "iron", dict(sharp=60)),
           (gjoin(*fletch), "emis", "teal", dict(sharp=30, double_sided=True))]
props("quiver", QM, qparts, "spine_03")

# ------------------------------------------------------------------ hand crossbows (right hand). local: +Y barrel, +Z up, +X lateral
GX = pistol_grip("r")
YZ = R('X', -90)       # lathe axis (+Z) -> barrel (+Y)
def along(g, y=0.0, z=0.0, x=0.0):
    return gxf(g, T(x, y, z) @ YZ)
def prod(span, sweep, y0, z, r0, r1, n=11, recurve=0.0, sx=1.3, sy=1.0):
    pts, rad = [], []
    for t in np.linspace(-1, 1, n):
        a = abs(t)
        pts.append((span * t, y0 - sweep * a ** 1.7 + recurve * max(0.0, a - 0.72) ** 2 * 12.0, z))
        rad.append(r0 + (r1 - r0) * a)
    return pts, rad
def handle(src, cap=None):
    out = [(tube_w([(0, 0.010, 0.060), (0, 0.004, 0.030), (0, 0.0, 0.0), (0, -0.010, -0.050), (0, -0.016, -0.076)], [0.0135, 0.015, 0.0155, 0.016, 0.0185], 8),
            "leather", src, dict(sharp=60))]
    if cap:
        out.append((gxf(g_lathe([(0.0, -0.014), (0.016, -0.010), (0.021, 0.0), (0.019, 0.010), (0.0, 0.012)], n=8), T(0, -0.017, -0.080) @ R('X', 12)), "metal", cap, dict(sharp=50)))
    return out
def bolt(y0, y1, z, shaft_cls, shaft_src, head_src, r=0.0055, head=0.042):
    return [(along(g_cyl(r, 0.0, y1 - y0, 6), y0, z), shaft_cls, shaft_src, dict(sharp=60)),
            (along(g_lathe([(0.0, 0.0), (0.013, 0.006), (0.0, head)], n=4), y1 - 0.004, z), "metal", head_src, dict(sharp=30))]

# tier 1: hand-cut wooden tiller and prod, rope string, iron-tipped bolt
p1, r1 = prod(0.185, 0.075, 0.272, 0.066, 0.0125, 0.007)
wpn("wpn_1", GX, handle("wood") + [
    (g_box(0.032, 0.345, 0.034, (0, 0.118, 0.066)), "leather", "woodbox", dict(bevel=0.005, sharp=40)),
    (tube_w(p1, r1, 6, up=(0, 0, 1), sx=1.25, sy=1.0), "leather", "wood", dict(sharp=60)),
    (g_box(0.040, 0.030, 0.042, (0, 0.268, 0.066)), "leather", "strap", dict(bevel=0.004, sharp=40)),
    (g_tube([p1[0], (0, 0.040, 0.088), p1[-1]], 0.0032, n=4), "cloth", "rope", dict(sharp=60)),
    (g_box(0.014, 0.020, 0.016, (0, 0.040, 0.088)), "metal", "iron", dict(sharp=40)),
] + bolt(0.045, 0.305, 0.090, "leather", "wood", "iron"))

# tier 2: steel prod with recurved tips, brass-bound dark tiller, stirrup, broadhead bolt
p2, r2 = prod(0.205, 0.070, 0.280, 0.068, 0.0105, 0.0065, recurve=0.045)
wpn("wpn_2", GX, handle("darkwood", "brass") + [
    (g_box(0.034, 0.36, 0.036, (0, 0.122, 0.068)), "leather", "darkbox", dict(bevel=0.005, sharp=40)),
    (g_tube(p2, r2, 6, up=(0, 0, 1), sx=1.6, sy=0.75), "metal", "steel", dict(sharp=50)),
    (g_box(0.042, 0.034, 0.046, (0, 0.276, 0.068)), "metal", "brass", dict(bevel=0.004, sharp=40)),
    (g_box(0.039, 0.022, 0.041, (0, 0.130, 0.068)), "metal", "brass", dict(bevel=0.003, sharp=40)),
    (g_box(0.039, 0.030, 0.041, (0, -0.040, 0.068)), "metal", "brass", dict(bevel=0.003, sharp=40)),
    (gxf(g_ring(0.030, 0.0048, 12, 5, sx=0.85), T(0, 0.326, 0.068)), "metal", "steel", dict(sharp=60)),
    (g_tube([p2[0], (0, 0.040, 0.092), p2[-1]], 0.0028, n=4), "metal", "iron", dict(sharp=60)),
    (g_box(0.016, 0.024, 0.018, (0, 0.040, 0.092)), "metal", "brass", dict(sharp=40)),
    (g_tube([(0, 0.21, 0.086), (0, 0.21, 0.118), (0, 0.19, 0.118)], 0.0035, n=4), "metal", "brass", dict(sharp=60)),
] + bolt(0.045, 0.325, 0.094, "leather", "darkwood", "steel"))

# tier 3: dark steel double prod with talon tips, raven-beak muzzle, teal-lit string and bolt
p3, r3 = prod(0.225, 0.085, 0.288, 0.070, 0.0115, 0.006, recurve=0.055)
p3b, r3b = prod(0.150, 0.070, 0.238, 0.098, 0.0085, 0.004, recurve=0.03)
talons = []
for s in (1, -1):
    tip = np.array(p3[-1 if s > 0 else 0])
    talons.append(gxf(g_tooth(0.075, 0.0105, 0.030, 5), frame(tip, (s * 0.6, 0.8, 0.0), (0, 0, 1)) @ R('Z', 90 - 90 * s)))
    mid = np.array(p3[8 if s > 0 else 2])
    talons.append(gxf(g_tooth(0.055, 0.009, 0.018, 5), frame(mid, (s * 0.15, 1.0, 0.0), (0, 0, 1)) @ R('Z', 90 - 90 * s)))
beak = [gxf(g_tooth(0.085, 0.013, 0.022, 5), frame((0, 0.300, 0.070 + 0.016 * s), (0, 1, 0), (1, 0, 0)) @ R('Z', 90 + 90 * s)) for s in (1, -1)]
wpn("wpn_3", GX, handle("darkwood", "brass") + [
    (g_box(0.036, 0.37, 0.038, (0, 0.126, 0.070)), "leather", "darkbox", dict(bevel=0.005, sharp=40)),
    (g_tube(p3, r3, 6, up=(0, 0, 1), sx=1.7, sy=0.8), "metal", "darksteel", dict(sharp=50)),
    (g_tube(p3b, r3b, 6, up=(0, 0, 1), sx=1.5, sy=0.8), "metal", "darksteel", dict(sharp=50)),
    (gjoin(*talons), "metal", "darksteel", dict(sharp=40)),
    (gjoin(*beak), "metal", "brass", dict(sharp=40)),
    (g_box(0.044, 0.040, 0.050, (0, 0.282, 0.072)), "metal", "darksteel", dict(bevel=0.004, sharp=40)),
    (g_box(0.041, 0.022, 0.043, (0, 0.150, 0.070)), "metal", "brass", dict(bevel=0.003, sharp=40)),
    (g_box(0.041, 0.030, 0.043, (0, -0.042, 0.070)), "metal", "brass", dict(bevel=0.003, sharp=40)),
    (g_tube([p3[0], (0, 0.040, 0.096), p3[-1]], 0.0042, n=4), "emis", "teal", dict(sharp=60)),
    (g_tube([p3b[0], (0, 0.080, 0.110), p3b[-1]], 0.0030, n=4), "emis", "teal", dict(sharp=60)),
    (g_box(0.018, 0.026, 0.020, (0, 0.040, 0.096)), "metal", "brass", dict(sharp=40)),
    (gxf(g_gem(0.013, 0.018, 6, 0.6), T(0.019, 0.060, 0.070) @ R('Y', 90)), "emis", "teal", dict(sharp=20)),
    (gxf(g_gem(0.013, 0.018, 6, 0.6), T(-0.019, 0.060, 0.070) @ R('Y', -90)), "emis", "teal", dict(sharp=20)),
    (along(g_cyl(0.0065, 0.0, 0.29, 6), 0.045, 0.098), "emis", "teal", dict(sharp=60)),
    (along(g_lathe([(0.0, 0.0), (0.015, 0.008), (0.0, 0.055)], n=4), 0.331, 0.098), "metal", "darksteel", dict(sharp=30)),
])

# ------------------------------------------------------------------ daggers (left hand, reverse grip: blade out of the little-finger side)
GD = grip("l") @ R('X', 180) @ T(0, 0, 0.012)
def hilt(src, r=0.0135):
    return (tube_w([(0, 0, -0.050), (0, 0, 0.0), (0, 0, 0.052)], [r * 0.95, r, r * 0.92], 8), "leather", src, dict(sharp=60))
# tier 1: plain single-edged iron knife, cord grip
k1 = [(-0.013, 0.052), (0.015, 0.052), (0.020, 0.15), (0.014, 0.235), (-0.004, 0.295), (-0.013, 0.262)]
wpn("wpn_1_off", GD, [
    hilt("strap"),
    (g_prism(k1, 0.006), "metal", "iron", dict(sharp=30)),
    (g_cyl(0.019, 0.046, 0.056, 8), "metal", "iron", dict(sharp=40)),
    (g_cyl(0.016, -0.060, -0.048, 8), "metal", "iron", dict(sharp=40)),
], bone="hand_l")
# tier 2: double-edged steel dagger, brass guard and ring pommel
wpn("wpn_2_off", GD, [
    hilt("dark"),
    (g_blade([(0.058, 0.021, 0.008), (0.19, 0.018, 0.007), (0.285, 0.009, 0.004), (0.325, 0, 0)]), "metal", "steel", dict(sharp=30)),
    (g_tube([(-0.050, 0, 0.066), (-0.026, 0, 0.056), (0, 0, 0.052), (0.026, 0, 0.056), (0.050, 0, 0.066)], [0.006, 0.008, 0.010, 0.008, 0.006], n=6), "metal", "brass", dict(sharp=50)),
    (gxf(g_ring(0.015, 0.0055, 10, 5), T(0, 0, -0.066) @ R('X', 90)), "metal", "brass", dict(sharp=60)),
    (band(0.0, 0.0138, 0.010, 8), "metal", "brass", dict(sharp=50)),
], bone="hand_l")
# tier 3: dark talon blade with a teal-lit fuller, fanged guard, gem pommel
k3 = [(-0.016, 0.054), (0.020, 0.054), (0.030, 0.10), (0.031, 0.17), (0.022, 0.25), (0.002, 0.32), (-0.022, 0.365), (-0.014, 0.30), (-0.008, 0.22),
      (-0.010, 0.13)]
f3 = [(0.002, 0.085), (0.010, 0.10), (0.012, 0.17), (0.006, 0.24), (-0.004, 0.295), (-0.001, 0.22), (0.002, 0.14)]
wpn("wpn_3_off", GD, [
    hilt("dark", 0.014),
    (g_prism(k3, 0.007), "metal", "darksteel", dict(sharp=30)),
    (g_prism(f3, 0.0095), "emis", "teal", dict(sharp=30)),
    (gxf(g_tooth(0.060, 0.0095, 0.020, 5), T(0.006, 0, 0.052) @ R('Y', 78) @ R('Z', 180)), "metal", "brass", dict(sharp=40)),
    (gxf(g_tooth(0.045, 0.0085, 0.015, 5), T(-0.006, 0, 0.052) @ R('Y', -78)), "metal", "brass", dict(sharp=40)),
    (g_cyl(0.018, 0.045, 0.058, 8), "metal", "brass", dict(sharp=40)),
    (gxf(g_gem(0.014, 0.034, 6, 0.45), T(0, 0, -0.058) @ R('X', 180)), "emis", "teal", dict(sharp=20)),
    (band(0.0, 0.0145, 0.010, 8), "metal", "brass", dict(sharp=50)),
], bone="hand_l")

finalize(c, CID, hair_tint=(0.05, 0.04, 0.035), skin=dict(gain=0.95, sat=0.8),
         tex={"cloth": 1024, "leather": 1024, "metal": 512},
         notes="Pose-tested: Pistol_Aim_Neutral, Pistol_Shoot, Pistol_Idle_Loop, Sword_Regular_Combo, Sword_Dash, Jog_Fwd_Loop. "
               "wpn_N = hand crossbow skinned 100% to hand_r (barrel level in the Pistol_Aim clips); wpn_N_off = dagger skinned 100% "
               "to hand_l in a reverse grip (blade leaves the little-finger side, so it hangs below the supporting hand in the "
               "two-handed Pistol clips). Hair material holds the eyebrows only (the hood hides the scalp).")
