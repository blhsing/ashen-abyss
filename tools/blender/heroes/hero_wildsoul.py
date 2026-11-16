"""hero_wildsoul -- shapeshifting hermit of the dead forest: grey wolf pelt worn as a mantle with the wolf's head as a
headdress (moss-lit eyes), bark slabs lashed on the right shoulder / left forearm / thighs, hide tunic and a kilt of
hide strips under a fur hip wrap, fang necklace, feather and bone charms, bandaged forearms, fur-topped boots.
Totem war-clubs in the right hand, held like a sword."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_mel import *

CID = "hero_wildsoul"
c = start("male")
body_bulk(c, spec={"upperarm_l": 1.10, "upperarm_r": 1.10, "lowerarm_l": 1.08, "lowerarm_r": 1.08},
          torso=[(0.90, 1.0, 1.0), (1.05, 1.05, 1.08), (1.25, 1.06, 1.08), (1.42, 1.05, 1.05), (1.52, 1.0, 1.0)])
B = c.B

# ------------------------------------------------------------------ source materials
mats.fur("pelt", base=(0.062, 0.058, 0.054), tip=(0.36, 0.34, 0.30))
mats.fur("pelt_dark", base=(0.026, 0.022, 0.018), tip=(0.125, 0.10, 0.075))
mats.cloth("wrap", base=(0.165, 0.135, 0.095), folds=0.2, ash=0.3, worn=0.6, weave=2.0)
mats.cloth("wool", base=(0.050, 0.044, 0.032), folds=0.6, ash=0.6, ash_h=0.5)
mats.flat("feather", (0.24, 0.22, 0.18), rough=0.8)
mats.flat("feather_dark", (0.045, 0.04, 0.035), rough=0.8)
mats.leather("hide", base=(0.066, 0.052, 0.038), worn=0.7, crack=1.0, ash=0.3, ash_h=0.5)
mats.leather("darkhide", base=(0.036, 0.026, 0.018), ash=0.5, ash_h=0.3)
mats.leather("strap", base=(0.070, 0.044, 0.024), worn=0.7)
mats.leather("tan", base=(0.150, 0.105, 0.060), worn=0.8, crack=1.0, ash=0.45, ash_h=0.75)
mats.wood("bark", base=(0.082, 0.066, 0.050), luv_grain=False, bump=1.0, rough=(0.75, 0.95))
mats.wood("wood", base=(0.090, 0.055, 0.030))
mats.wood("woodbox", base=(0.090, 0.055, 0.030), luv_grain=False)
mats.wood("darkwood", base=(0.036, 0.026, 0.020))
mats.bone("bone")
mats.bone("oldbone", base=(0.36, 0.30, 0.20), stain=(0.12, 0.08, 0.04))
mats.emis("moss", (0.36, 1.0, 0.22), strength=3.0)

# ------------------------------------------------------------------ legs: wool leggings, fur-topped hide boots with cross garters
trousers(c, "wool", top=1.06, bottom=0.34, offset=0.010, smooth=3)
boots(c, "darkhide", top=0.40, offset=0.015)
ring = [(0.100 + 0.072 * math.cos(a), 0.040 + 0.080 * math.sin(a), 0.405) for a in np.linspace(0, TAU, 14, endpoint=False)]
bf = fur_roll(ring, 0.032, "pelt_dark", name="boot_fur_l", noise=0.012, scale=30.0, n=8, bone="calf_l")
mirrored(bf)
gar = add("garter_l", g_tube(helix_on_leg(c, 0.13, 0.36, 2.0, cx=0.098, cy=0.045, r_guide=0.085, offset=0.021, n=34), 0.0065, n=5),
          "leather", "strap", bone="calf_l", sharp=180)
mirrored(gar)

# ------------------------------------------------------------------ hide tunic (open to the sternum), kilt of hide strips, fur hip wrap
vneck = (np.abs(B.x) < 0.012 + 0.26 * (B.z - 1.24)) & (B.y < 0.02) & (B.z > 1.24)
mt = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > 1.055) & (B.z < 1.50) & (np.abs(B.x) < 0.215) & ~vneck & (B.w("upperarm") < 0.4)
tunic = shell(B, mt, 0.019, "tunic", "leather", "hide", smooth=9, sharp=180, pin_boundary=True)
hide(c, B.shrink(mt, 1))
KS = [(0.50, 0, 0.02, 0.250, 0.205, 0.215), (0.75, 0, 0.02, 0.228, 0.188, 0.202), (0.95, 0, 0.022, 0.202, 0.164, 0.174),
      (1.08, 0, 0.012, 0.176, 0.138, 0.138)]
kilt = robe(c, "tan", KS, nu=40, nv=12, name="kilt", cls="leather", thick=0.007, ragged=0.012, calf_to_thigh=1.0,
            mod=tails_mod(k=8, depth=0.30, seed=5, power=1.6, notch=1.0, uneven=0.5))
FW = [(0.74, 0, 0.02, 0.244, 0.202, 0.214), (0.95, 0, 0.022, 0.214, 0.176, 0.186), (1.085, 0, 0.012, 0.182, 0.144, 0.144)]
def fw_mod(U, V, x, y, z):
    return x, y, z + 0.13 * (0.5 + 0.5 * np.cos(TAU * U)) * (1 - V) ** 1.2
fw = robe(c, "pelt_dark", FW, nu=32, nv=7, name="furwrap", cls="cloth", thick=0.012, ragged=0.035, calf_to_thigh=1.0, mod=fw_mod)
shag(fw, 0.010, 30.0, 3)
BZ, BRX, BRY = 1.082, 0.204, 0.166
bcy = body_ring(c, BZ)[1]
belt(c, BZ, "strap", width=0.074, thick=0.010, rx=BRX, ry=BRY)
BK = frame((0.0, bcy - BRY - 0.013, BZ), (0, -1, 0), (0, 0, 1))
props("buckle", BK, [
    (g_lathe([(0.016, 0.008), (0.040, 0.010), (0.048, 0.0), (0.040, -0.005), (0.016, -0.005), (0.016, 0.008)], n=10), "bone", "oldbone", dict(sharp=50)),
    (gxf(g_bone(0.11, 0.0085, 1.7), T(-0.055, 0, 0.012) @ R('Y', 90)), "bone", "bone", dict(sharp=50)),
], "spine_01")

def belt_pt(a_deg, drop=0.0, out=0.012):
    a = math.radians(a_deg)
    rad = np.array((math.cos(a), math.sin(a), 0.0))
    return np.array((BRX * math.cos(a), bcy + BRY * math.sin(a), BZ - 0.03 - drop)) + rad * out, rad

# feather charms (left front), bone string (right front), spirit mask with moss-lit eyes (right hip)
fts = []
for i, (ad, ln, src) in enumerate(((-58, 0.20, "feather"), (-47, 0.16, "feather_dark"), (-37, 0.19, "feather"))):
    p, rad = belt_pt(ad)
    fts.append((gxf(g_feather(ln, 0.052, 0.004), hang(p, rad, 0.22) @ R('Z', 12 * (i - 1))), "cloth", src, dict(sharp=30, double_sided=True)))
for o in props("charm_feathers", np.eye(4), fts, "pelvis"):
    w_func(o, lambda co: {"pelvis": 0.6, "thigh_l": 0.4})
bns = []
for i, (ad, ln) in enumerate(((-140, 0.11), (-130, 0.15), (-121, 0.09))):
    p, rad = belt_pt(ad)
    bns.append((gxf(g_bone(ln, 0.0075, 1.7), hang(p, rad, 0.18)), "bone", "bone" if i % 2 else "oldbone", dict(sharp=50)))
for o in props("charm_bones", np.eye(4), bns, "pelvis"):
    w_func(o, lambda co: {"pelvis": 0.6, "thigh_r": 0.4})
p, rad = belt_pt(-172, drop=0.075, out=0.030)
MM = frame(p, rad, (0, 0, 1))
msk = [(g_lathe([(0.0, 0.016), (0.030, 0.014), (0.050, 0.004), (0.054, -0.006), (0.0, -0.006)], n=9, sy=1.25), "leather", "bark", dict(sharp=40)),
       (gxf(g_tube([(0, 0.05, 0.0), (0, 0.085, 0.004), (0, 0.105, 0.006)], 0.005, n=4), np.eye(4)), "leather", "strap", dict(sharp=60))]
for s in (1, -1):
    msk.append((gxf(g_prism(rhomb(0.014, 0.007), 0.006), T(0.019 * s, 0.014, 0.012) @ R('X', 90) @ R('Y', 18 * s)), "emis", "moss", dict(sharp=20)))
msk.append((gxf(g_prism(bar(0.014, 0.003), 0.006), T(0, -0.024, 0.010) @ R('X', 90)), "emis", "moss", dict(sharp=20)))
for o in props("spirit_mask", MM, msk, "pelvis"):
    w_func(o, lambda co: {"pelvis": 0.6, "thigh_r": 0.4})

# bark slabs hung on the kilt over the thighs (rune-cut on the left)
fyk = front_y(KS, 0.030)
apply_mods(kilt)
for s in (1, -1):
    pl = panel("bark_tasset_%s" % ("l" if s > 0 else "r"), s * 0.050, s * 0.185, 0.90, 0.57, fyk, "leather", "bark", nu=5, nv=6, thick=0.014,
               point=0.05)
    w_transfer(pl, kilt, smooth=1)
    apply_mods(pl)
    if s > 0:
        decal("rune_tasset_0", chevron(0.028, 0.026, 0.011), pl, (0.118, -0.5, 0.795), "emis", "moss", ray=(0, 1, 0), depth=0.006, lift=0.002)
        decal("rune_tasset_1", bar(0.024, 0.006), pl, (0.118, -0.5, 0.720), "emis", "moss", ray=(0, 1, 0), depth=0.006, lift=0.002)

# ------------------------------------------------------------------ arms: bandaged forearms + palms, cord, bark bracer (left), feathered armband (right)
mw = (B.w("lowerarm", "upperarm") > 0.5) & (np.abs(B.x) > 0.468) & (np.abs(B.x) < 0.742) & (B.w(*BONES_HAND) < 0.5)
shell(B, mw, 0.006, "wraps", "cloth", "wrap", smooth=1, sharp=180)
hide(c, mw & (np.abs(B.x) > 0.492) & (np.abs(B.x) < 0.720))
mp = (B.W[:, B.col["hand_l"]] + B.W[:, B.col["hand_r"]] > 0.55) & (np.abs(B.x) < 0.845)
shell(B, mp, 0.004, "palm_wraps", "cloth", "wrap", smooth=0, sharp=180)
cord = add("cord_r", g_tube(helix_on_arm(c, 0.50, 0.71, 2.6, side=-1, offset=0.011), 0.0058, n=5), "leather", "strap", sharp=180, bone="lowerarm_r")
mbr = (B.x > 0.495) & (B.x < 0.705) & (B.w("lowerarm") > 0.5) & (B.z > 1.428)
bracer = shell(B, mbr, 0.022, "bark_bracer", "leather", "bark", smooth=3, sharp=60, solidify=0.010)
w_rigid(bracer, "lowerarm_l")
apply_mods(bracer)
for i, (x, shp) in enumerate(((0.545, chevron(0.017, 0.016, 0.007)), (0.600, rhomb(0.010, 0.018)), (0.652, bar(0.016, 0.005)))):
    decal("rune_bracer_%d" % i, shp, bracer, (x, 0.070, 1.80), "emis", "moss", up=(1, 0, 0), ray=(0, 0, -1), depth=0.005, lift=0.002, bone="lowerarm_l")
for i, x in enumerate((0.520, 0.685)):
    angs = np.linspace(0, TAU, 14, endpoint=False)
    gpts = [(x, 0.070 + 0.12 * math.cos(a), 1.435 + 0.12 * math.sin(a)) for a in angs]
    k = sstep(np.sin(angs), -0.35, 0.25)[:, None]
    ring_p = wrap_path(c, gpts, 0.009) * (1 - k) + wrap_path(c, gpts, 0.037) * k
    add("bracer_cord_%d" % i, g_tube(ring_p, 0.006, n=5, closed=True), "leather", "strap", bone="lowerarm_l", sharp=180)
AB = [(-0.345, 0.075 + 0.13 * math.cos(a), 1.438 + 0.13 * math.sin(a)) for a in np.linspace(0, TAU, 14, endpoint=False)]
add("armband", g_tube(wrap_path(c, AB, 0.006), 1.0, n=6, closed=True, sx=0.016, sy=0.005, up=(1, 0, 0)), "leather", "strap", bone="upperarm_r", sharp=60)
ABP = wrap_path(c, [(-0.350, 0.075, 1.65)], 0.013)[0]
props("armband_charms", np.eye(4), [
    (gxf(g_feather(0.125, 0.044, 0.004), frame(ABP + np.array((0, -0.022, -0.004)), (-1.0, -0.10, 0.10), (0, 0, 1))), "cloth", "feather", dict(sharp=30, double_sided=True)),
    (gxf(g_feather(0.100, 0.038, 0.004), frame(ABP + np.array((0, 0.024, -0.004)), (-1.0, 0.16, 0.08), (0, 0, 1))), "cloth", "feather_dark", dict(sharp=30, double_sided=True)),
], "upperarm_r")

# ------------------------------------------------------------------ fang necklace + pelt forepaws knotted on the chest
npts = [(0.135 * t, -0.30, 1.478 - 0.120 * (1 - t * t)) for t in np.linspace(-1, 1, 17)]
NP = wrap_path(c, npts, 0.024)
neck = [g_tube(NP, 0.0042, n=4)]
for i in range(2, 15):
    t = abs(i - 8) / 8.0
    neck.append(gxf(g_tooth(0.050 - 0.022 * t, 0.0088, 0.008, 5), frame(NP[i] + np.array((0, -0.004, -0.002)), (0, -0.18, -1.0), (0, -1, 0))))
nk = add("necklace", gjoin(*neck), "bone", "bone", sharp=50)
w_transfer(nk, c.ref, smooth=0, only=["spine_03", "spine_02"])
for s in (1, -1):
    P = wrap_path(c, [(s * 0.120, -0.3, 1.50), (s * 0.110, -0.3, 1.42), (s * 0.098, -0.3, 1.34), (s * 0.092, -0.3, 1.27), (s * 0.090, -0.3, 1.215)], 0.036)
    paw = add("paw_%d" % s, g_tube(P, [0.040, 0.035, 0.029, 0.026, 0.032], n=8, sx=0.55, sy=1.0, up=(0, -1, 0)), "cloth", "pelt", sharp=180, subsurf=1)
    shag(paw, 0.006, 40.0, 5 + s)
    w_transfer(paw, c.ref, smooth=1, only=["spine_03", "spine_02"])
    cl = [gxf(g_tooth(0.034, 0.0075, 0.010, 5), frame(P[-1] + np.array((dx, -0.012, -0.022)), (dx * 4, -0.25, -1.0), (0, -1, 0))) for dx in (-0.020, 0.0, 0.020)]
    co_ = add("paw_claws_%d" % s, gjoin(*cl), "bone", "oldbone", sharp=40)
    w_transfer(co_, c.ref, smooth=0, only=["spine_03", "spine_02"])

# ------------------------------------------------------------------ wolf pelt mantle over both shoulders, back pelt and tail
zmin = 1.40 - 0.60 * B.y
core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zmin) & (B.z < 1.56) & (np.abs(B.x) < 0.23)
arms = (B.w("upperarm", "clavicle") > 0.5) & (np.abs(B.x) < 0.338) & (B.z > 1.395)
mm = (core | arms) & (B.w(*BONES_HAND, "lowerarm") < 0.3)
mantle = shell(B, mm, 0.044, "mantle", "cloth", "pelt", smooth=5, sharp=180)
hide(c, B.shrink(mm, 2))
edge_tufts(c, mantle, "pelt", "mantle_fringe", length=0.060, r=0.026, p=0.55, seed=4, down=1.0, out=0.55)
displace_noise(mantle, 0.012, 30.0, 6)
BP = [(0.84, 0, 0.02, 0.200, 0.18, 0.236), (1.05, 0, 0.012, 0.200, 0.16, 0.206), (1.25, 0, 0.012, 0.212, 0.16, 0.192), (1.44, 0, 0.02, 0.205, 0.15, 0.158)]
bp = robe(c, "pelt", BP, nu=20, nv=12, name="backpelt", cls="cloth", thick=0.014, ragged=0.03, u0=0.115, u1=0.385, close=False, calf_to_thigh=1.0,
          mod=tails_mod(k=3, depth=0.22, seed=2, power=1.5, lens=[0.55, 1.0, 0.6]))
shag(bp, 0.011, 28.0, 4)
tail = add("tail", g_tube([(0.0, 0.215, 1.00), (0.008, 0.250, 0.92), (0.020, 0.268, 0.82), (0.030, 0.270, 0.71), (0.034, 0.262, 0.62)],
                          [0.030, 0.050, 0.056, 0.042, 0.010], n=8), "cloth", "pelt", sharp=180, subsurf=1)
shag(tail, 0.010, 36.0, 8)
w_rigid(tail, "pelvis")

# bark pauldron lashed over the fur on the right shoulder, feather fan on the left
planks = []
for k, (yc, x1, dz) in enumerate(((-0.014, -0.385, 0.0), (0.064, -0.410, 0.012), (0.142, -0.372, -0.004))):
    pth = [(-0.150, yc * 0.96, 1.588 + dz), (-0.215, yc, 1.606 + dz), (-0.295, yc * 1.05, 1.592 + dz), (x1, yc * 1.10, 1.545 + dz)]
    planks.append(g_plank(pth, 0.072, 0.017, up=(0, 0, 1), taper=[0.86, 1.0, 1.0, 0.78]))
pd = [add("pauldron", gjoin(*planks), "leather", "bark", bevel=0.004, sharp=40)]
cords = [g_tube([(x, -0.058, zc - 0.012), (x, -0.014, zc + 0.002), (x, 0.064, zc + 0.016), (x, 0.142, zc), (x, 0.186, zc - 0.014)], 0.0068, n=5)
         for (x, zc) in ((-0.205, 1.627), (-0.315, 1.607))]
pd.append(add("pauldron_cord", gjoin(*cords), "leather", "strap", sharp=180))
pd.append(add("pauldron_rune", gxf(g_prism(chevron(0.024, 0.022, 0.009), 0.007), T(-0.262, 0.066, 1.6325) @ R('Y', -8) @ R('X', 90) @ R('Y', 90)), "emis", "moss", sharp=20))
for o in pd:
    w_func(o, lambda co: {"clavicle_r": 0.62, "upperarm_r": 0.38})
ff = []
for i, (ang, ln, src) in enumerate(((8, 0.24, "feather"), (34, 0.28, "feather_dark"), (60, 0.23, "feather"))):
    M = T(0.205, 0.105, 1.555) @ R('X', -28) @ R('Y', ang)
    ff.append((gxf(g_feather(ln, 0.062, 0.004), M), "cloth", src, dict(sharp=30, double_sided=True)))
ff.append((gxf(g_sphere(0.026, 8, 5), T(0.205, 0.105, 1.557)), "leather", "strap", dict(sharp=60)))
for o in props("feather_fan", np.eye(4), ff, "clavicle_l"):
    w_func(o, lambda co: {"clavicle_l": 0.62, "upperarm_l": 0.38})

# ------------------------------------------------------------------ wolf-head headdress: fur cowl + skull cap, snout, ears, moss-lit eyes
HST = [(1.470, 0.030, 0.200, 0.130, 0.170), (1.530, 0.020, 0.135, 0.110, 0.140), (1.580, 0.000, 0.118, 0.118, 0.125),
       (1.640, -0.015, 0.122, 0.140, 0.130), (1.700, -0.022, 0.128, 0.150, 0.138), (1.760, -0.022, 0.120, 0.145, 0.134),
       (1.810, -0.018, 0.098, 0.118, 0.115), (1.845, -0.012, 0.062, 0.075, 0.078), (1.865, -0.008, 0.0, 0.0, 0.0)]
hd, gh = cowl(c, "pelt", HST, name="wolfhood", thick=0.012, chin=1.525, brow=1.764, open_deg=74.0, peak=0.020, nu=28, nv=18)
shag(hd, 0.007, 34.0, 2)
hide(c, (B.w("Head") > 0.5) & ((B.z > 1.775) | ((B.y > 0.035) & (B.z > 1.61))))
WM = frame((0.0, -0.072, 1.816), (0, -1, -0.20), (0, 0, 1))
wolf = [
    (gxf(g_sphere(1.0, 12, 8), T(0, -0.004, -0.045) @ S(0.094, 0.068, 0.108)), "cloth", "pelt", dict(sharp=180)),
    (g_stations([(0.00, 0, -0.008, 0.066, 0.052, 0.050), (0.06, 0, -0.014, 0.052, 0.039, 0.035), (0.12, 0, -0.018, 0.043, 0.032, 0.027),
                 (0.165, 0, -0.018, 0.037, 0.028, 0.024), (0.188, 0, -0.018, 0.025, 0.019, 0.018)], nu=12, nv=7, p=2.8, cap1=True), "cloth", "pelt", dict(sharp=180)),
    (gxf(g_sphere(1.0, 8, 5), T(0, -0.006, 0.190) @ S(0.020, 0.015, 0.014)), "leather", "darkhide", dict(sharp=180)),
    (tuft((0.030, 0.050, -0.010), (0.25, 0.7, -0.7), 0.055, 0.017, 0.0), "cloth", "pelt_dark", dict(sharp=180)),
    (tuft((-0.030, 0.050, -0.010), (-0.25, 0.7, -0.7), 0.055, 0.017, 0.0), "cloth", "pelt_dark", dict(sharp=180)),
]
for s in (1, -1):
    wolf.append((gxf(g_lathe([(0.044, 0.0), (0.036, 0.05), (0.0, 0.130)], n=6, sy=0.42), frame((s * 0.060, 0.038, -0.090), (s * 0.36, 1.0, -0.30), (0, 0, 1))),
                 "cloth", "pelt_dark", dict(sharp=60)))
    wolf.append((gxf(g_sphere(1.0, 8, 5), T(s * 0.047, 0.036, 0.020) @ R('Z', -24 * s) @ S(0.019, 0.010, 0.014)), "emis", "moss", dict(sharp=180)))
    wolf.append((gxf(g_tooth(0.046, 0.0088, 0.010, 5), T(s * 0.026, -0.043, 0.150) @ R('X', 90) @ R('Z', -90)), "bone", "bone", dict(sharp=40)))
    for k, zz in enumerate((0.118, 0.090)):
        wolf.append((gxf(g_cone(0.006, 0.020, 4), T(s * (0.032 + 0.005 * k), -0.046, zz) @ R('X', 90)), "bone", "bone", dict(sharp=40)))
props("wolf", WM, wolf, "Head")
ruff = []
rr = np.random.RandomState(11)
for j in range(18):
    for i, sg in ((0, 1), (27, -1)):
        q = gh["v"][j * 28 + i]
        if 1.555 < q[2] < 1.695 and rr.rand() < 0.8:
            d = (sg * (0.55 + 0.4 * rr.rand()), -0.25 - 0.3 * rr.rand(), -0.85)
            ruff.append(tuft(q + np.array((sg * 0.004, 0.006, 0)), d, 0.045 + 0.045 * rr.rand(), 0.022 + 0.008 * rr.rand(), 0.016))
add("hood_ruff", gjoin(*ruff), "cloth", "pelt", bone="Head", sharp=180)
add_hair(c, "Hair_Beard")

# ------------------------------------------------------------------ totem war-clubs (right hand; weapon local +Z up the club, +X = striking side)
G = grip("r")
def grip_wrap(z0=-0.075, z1=0.080, r=0.0198):
    return (tube_w([(0, 0, z0), (0, 0, z1)], r, 8), "leather", "strap", dict(sharp=60))
# tier 1: root club -- a knotted burl on a crooked haft, one fang lashed to the striking side
h1 = lumpy(gxf(g_sphere(1.0, 12, 8), T(0, 0, 0.70) @ S(0.074, 0.068, 0.110)), 0.13, 34.0, 3, (0, 0, 0.70))
knobs = [gxf(g_lathe([(0.024, 0.0), (0.020, 0.028), (0.010, 0.048), (0.0, 0.054)], n=6), frame(np.array((0, 0, 0.70)) + np.array(d) * np.array((0.060, 0.055, 0.09)), d))
         for d in ((-0.7, 0.5, 0.5), (0.2, -0.9, 0.3), (-0.4, -0.6, -0.5), (0.3, 0.8, -0.3), (0.0, 0.0, 1.0))]
wpn("wpn_1", G, [
    (tube_w(gnarl(zline(-0.13, 0.62, 9), 0.011, 3), [0.021, 0.018, 0.017, 0.0175, 0.019, 0.022, 0.026, 0.031, 0.036], 8), "leather", "wood", dict(sharp=60)),
    (h1, "leather", "woodbox", dict(sharp=60)),
    (gjoin(*knobs), "leather", "woodbox", dict(sharp=60)),
    grip_wrap(),
    (gxf(g_tooth(0.085, 0.017, 0.022, 6), T(0.058, 0, 0.71) @ R('Y', 72)), "bone", "oldbone", dict(sharp=40)),
    (tube_w([(0, 0, 0.585), (0, 0, 0.625)], 0.038, 8), "leather", "strap", dict(sharp=60)),
])
# tier 2: carved totem club -- three stacked beast faces with bone beaks, fang crown, feathers
def totem_face(z, r=1.0):
    return g_lathe([(0.042 * r, z), (0.058 * r, z + 0.012), (0.062 * r, z + 0.052), (0.053 * r, z + 0.086), (0.064 * r, z + 0.096), (0.044 * r, z + 0.112)], n=10, sy=0.86)
faces, beaks, eyes2 = [], [], []
for i, z in enumerate((0.50, 0.612, 0.724)):
    r = 1.0 + 0.10 * i
    faces.append(totem_face(z, r))
    beaks.append(gxf(g_prism([(0.0, 0.024), (0.062, -0.012), (0.0, -0.030)], 0.030), T(0.050 * r, 0, z + 0.046)))
    for s in (1, -1):
        eyes2.append(gxf(g_cyl(0.011, 0.0, 0.008, 6), T(0.047 * r, s * 0.030 * r, z + 0.074) @ R('Y', 90)))
crown2 = [gxf(g_tooth(0.085, 0.014, 0.020, 5), T(0.036 * math.cos(a), 0.030 * math.sin(a), 0.835) @ R('Z', math.degrees(a)) @ R('Y', 26)) for a in np.linspace(0, TAU, 5, endpoint=False)]
wpn("wpn_2", G, [
    (tube_w(zline(-0.14, 0.51, 6), [0.021, 0.018, 0.018, 0.020, 0.024, 0.030], 8), "leather", "darkwood", dict(sharp=60)),
    (gjoin(*faces), "leather", "woodbox", dict(sharp=50)),
    (gjoin(*beaks), "bone", "oldbone", dict(sharp=30)),
    (gjoin(*eyes2), "bone", "bone", dict(sharp=40)),
    (gjoin(*crown2), "bone", "bone", dict(sharp=40)),
    (g_lathe([(0.0, -0.172), (0.019, -0.164), (0.026, -0.146), (0.019, -0.128)], n=8), "bone", "oldbone", dict(sharp=50)),
    grip_wrap(),
    (tube_w([(0, 0, 0.455), (0, 0, 0.498)], 0.031, 8), "leather", "strap", dict(sharp=60)),
    (gxf(g_feather(0.19, 0.055, 0.004), T(-0.030, 0.012, 0.47) @ R('Y', 200) @ R('Z', 90)), "cloth", "feather", dict(sharp=30, double_sided=True)),
    (gxf(g_feather(0.15, 0.048, 0.004), T(-0.026, -0.016, 0.46) @ R('Y', 186) @ R('Z', 70)), "cloth", "feather_dark", dict(sharp=30, double_sided=True)),
])
# tier 3: the great totem -- antlered beast skull with moss-lit sockets on a rune-cut haft
runes3 = []
for i, (z, shp) in enumerate(((0.165, chevron(0.011, 0.014, 0.0055)), (0.235, bar(0.010, 0.0035)), (0.300, rhomb(0.007, 0.015)), (0.370, hook(0.010, 0.012, 0.005)),
                              (0.440, chevron(0.012, 0.014, 0.0055)))):
    for s in (1, -1):
        runes3.append(gxf(g_prism(shp, 0.006), T(0, s * (float(np.interp(z, [0.15, 0.30, 0.45, 0.60], [0.0185, 0.021, 0.025, 0.031])) + 0.0012), z)))
ant = g_antler(0.62, tines=3)
skull = gjoin(gxf(g_sphere(1.0, 12, 8), T(0.0, 0, 0.715) @ S(0.078, 0.064, 0.088)),
              gxf(g_lathe([(0.052, 0.0), (0.045, 0.05), (0.037, 0.10), (0.026, 0.135), (0.0, 0.150)], n=10, sx=0.74), T(0.030, 0, 0.700) @ R('Y', 90)))
wpn("wpn_3", G, [
    (tube_w(zline(-0.15, 0.60, 6), [0.022, 0.0185, 0.0185, 0.021, 0.025, 0.031], 8), "leather", "darkwood", dict(sharp=60)),
    (skull, "bone", "bone", dict(sharp=60)),
    (gjoin(*[gxf(g_sphere(1.0, 8, 5), T(0.058, s * 0.036, 0.728) @ S(0.018, 0.012, 0.016)) for s in (1, -1)]), "emis", "moss", dict(sharp=180)),
    (gjoin(*[gxf(g_tooth(0.055, 0.0105, 0.012, 5), T(0.135, s * 0.020, 0.676) @ R('X', 180) @ R('Z', 90 - 90 * s)) for s in (1, -1)]), "bone", "oldbone", dict(sharp=40)),
    (gxf(ant, T(-0.012, 0.030, 0.775) @ R('Z', 90)), "bone", "oldbone", dict(sharp=60)),
    (gxf(mirror_x(ant), T(-0.012, -0.030, 0.775) @ R('Z', 90)), "bone", "oldbone", dict(sharp=60)),
    (gjoin(*runes3), "emis", "moss", dict(sharp=20)),
    (band(0.105, 0.0195, 0.014, 8), "emis", "moss", dict(sharp=60)), (band(0.500, 0.0285, 0.014, 8), "emis", "moss", dict(sharp=60)),
    (g_lathe([(0.0, -0.185), (0.016, -0.176), (0.028, -0.155), (0.020, -0.135)], n=8), "bone", "oldbone", dict(sharp=50)),
    grip_wrap(r=0.0205),
    (tube_w([(0, 0, 0.545), (0, 0, 0.640)], [0.034, 0.040], 8), "leather", "strap", dict(sharp=60)),
    (gxf(g_feather(0.22, 0.062, 0.004), T(-0.034, 0.014, 0.585) @ R('Y', 204) @ R('Z', 90)), "cloth", "feather", dict(sharp=30, double_sided=True)),
    (gxf(g_feather(0.18, 0.052, 0.004), T(-0.030, -0.018, 0.575) @ R('Y', 188) @ R('Z', 70)), "cloth", "feather_dark", dict(sharp=30, double_sided=True)),
    (gxf(g_bone(0.10, 0.007, 1.7), T(-0.036, 0.0, 0.56) @ R('Y', 170)), "bone", "bone", dict(sharp=50)),
])

finalize(c, CID, hair_tint=(0.20, 0.185, 0.165), skin=dict(gain=0.90, sat=0.75),
         tex={"cloth": 1024, "leather": 1024, "bone": 512}, decimate={"cloth": 0.74, "leather": 0.85},
         notes="Pose-tested: Sword_Regular_A, Sword_Idle, Spell_Simple_Shoot, Idle_Loop, Jog_Fwd_Loop. wpn_1..3 = totem war-clubs skinned "
               "100% to hand_r in the UAL sword fist grip (weapon +X = striking side). No metal material: wood, bark and stone-like "
               "parts live in the leather_ atlas, fur / feathers in cloth_.")
