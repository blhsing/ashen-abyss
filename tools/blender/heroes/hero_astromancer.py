"""hero_astromancer -- star-reader in layered robes: stepped star-metal mantle with a twin-horned standing collar,
plum stole with glowing star runes, open midnight coat over a slate under-robe, astrolabe and star-chart tome at the
hips. Staves in the right hand (wpn_3: crescent cradle with a floating star-crystal)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_cst import *

CID = "hero_astromancer"
c = start("female")
B = c.B

# ------------------------------------------------------------------ source materials
TRIM = (0.30, 0.26, 0.17)
mats.cloth("inner", base=(0.105, 0.115, 0.14), folds=0.7, ash=0.45, ash_h=0.45, worn=0.4)
mats.cloth("coat", base=(0.028, 0.038, 0.085), folds=0.9, ash=0.45, ash_h=0.5, bands=[(0.0, 0.045, TRIM)], var=0.35)
mats.cloth("coat_top", base=(0.028, 0.038, 0.085), folds=0.4, ash=0.0, var=0.35)
mats.cloth("sleeve", base=(0.028, 0.038, 0.085), folds=0.3, ash=0.0, bands=[(0.90, 1.0, TRIM)], var=0.35)
mats.cloth("stole", base=(0.085, 0.03, 0.065), folds=0.3, ash=0.2, bands=[(0.0, 0.035, TRIM)], var=0.3)
mats.flat("paper", (0.34, 0.30, 0.22), rough=0.85)
mats.metal("starmetal", base=(0.085, 0.115, 0.19), rough=(0.34, 0.58), edge=(0.42, 0.52, 0.68), edge_amt=0.9, rust_amt=0.08,
           rust=(0.03, 0.035, 0.05), pattern="hammered", ash=0.0)
mats.metal("silver", base=(0.42, 0.41, 0.38), rough=(0.25, 0.45), rust_amt=0.12, rust=(0.06, 0.055, 0.05), ash=0.0)
mats.metal("iron", base=(0.17, 0.165, 0.16), rough=(0.45, 0.7), rust_amt=0.35, ash=0.0)
mats.leather("boot", base=(0.04, 0.03, 0.03), ash=0.5, ash_h=0.3)
mats.leather("belt", base=(0.06, 0.04, 0.03))
mats.leather("tome", base=(0.035, 0.05, 0.075), ash=0.0, worn=0.7)
mats.wood("wood", base=(0.075, 0.048, 0.03))
mats.wood("darkwood", base=(0.03, 0.024, 0.026))
mats.emis("star", (0.02, 0.18, 1.0), strength=4.5)

# ------------------------------------------------------------------ under-robe (fitted top + long skirt)
shirt(c, "inner", bottom=1.04, top=1.49, sleeve=0.715, offset=0.008, smooth=3, name="inner_top")
boots(c, "boot", top=0.28, offset=0.011)
INNER = [(0.085, 0, 0.02, 0.238, 0.200, 0.226), (0.50, 0, 0.02, 0.214, 0.184, 0.200), (0.92, 0, 0.018, 0.196, 0.160, 0.166),
         (1.08, 0, -0.006, 0.140, 0.106, 0.112), (1.125, 0, -0.012, 0.128, 0.100, 0.104)]
inner = robe(c, "inner", INNER, nu=30, nv=13, name="inner_skirt", pleats=9, pleat_amp=0.03, ragged=0.006, calf_to_thigh=0.55)
hide(c, (B.z > 0.31) & (B.z < 1.06) & (B.w(*BONES_ARM, *BONES_HAND) < 0.3))

# ------------------------------------------------------------------ open coat: torso shell + bell sleeves + split skirt
mc = (B.w(*BONES_TORSO, "clavicle") > 0.5) & (B.z > 1.07) & (B.z < 1.475) & (np.abs(B.x) < 0.185)
coat_top = shell(B, mc, 0.019, "coat_top", "cloth", "coat_top", smooth=5, sharp=180)
OUTER = [(0.215, 0, 0.02, 0.282, 0.240, 0.270), (0.55, 0, 0.02, 0.250, 0.214, 0.236), (0.92, 0, 0.018, 0.220, 0.184, 0.190),
         (1.085, 0, -0.006, 0.160, 0.126, 0.132), (1.15, 0, -0.012, 0.146, 0.118, 0.122)]
GAP = 0.058
coat = robe(c, "coat", OUTER, nu=30, nv=13, name="coat_skirt", pleats=8, pleat_amp=0.05, thick=0.006, ragged=0.01,
            u0=0.75 + GAP, u1=1.75 - GAP, close=False, calf_to_thigh=0.55)
sleeve(c, "sleeve", [(0.165, 0.0, 0.0, 0.078, 0.072, 0.072), (0.30, 0.004, 0.0, 0.070, 0.064, 0.064),
                     (0.45, 0.012, 0.0, 0.074, 0.066, 0.066), (0.60, 0.030, 0.0, 0.108, 0.082, 0.082)],
       n=14, nv=8, thick=0.006)

# ------------------------------------------------------------------ sash + clasp + hanging stole with runes
SASH_RX, SASH_RY, SASH_Z = 0.176, 0.146, 1.105
sash = belt(c, SASH_Z, "stole", width=0.095, thick=0.008, cls="cloth", name="sash", rx=SASH_RX, ry=SASH_RY)
fy = front_y(INNER, 0.024)
stole = panel("stole", -0.052, 0.052, 1.075, 0.46, fy, "cloth", "stole", nu=5, nv=12, thick=0.006, taper=0.25, point=0.07)
apply_mods(inner)
w_transfer(stole, inner, smooth=1)
apply_mods(stole)
for i, (zz, shp) in enumerate(((0.92, star4(0.026)), (0.80, rhomb(0.013, 0.026)), (0.69, star4(0.020)), (0.59, rhomb(0.011, 0.022)))):
    decal("rune_stole_%d" % i, shp, stole, (0.0, -0.4, zz), "emis", "star", out=(0, -1, 0))
# round star-metal clasp on the sash
CL = frame((0.0, -SASH_RY - 0.012, SASH_Z), (0, -1, 0), (0, 0, 1))
props("clasp", CL, [
    (g_lathe([(0.0, 0.014), (0.022, 0.012), (0.044, 0.004), (0.050, -0.004), (0.0, -0.004)], n=12), "metal", "starmetal", dict(sharp=40)),
    (g_tube(ring_path(0.047, 14, "xy", (0, 0, 0.004)), 0.005, n=5, closed=True), "metal", "silver", dict(sharp=60)),
    (gxf(g_prism(star4(0.024), 0.005), T(0, 0, 0.015) @ R('X', 90)), "emis", "star", dict(sharp=20)),
], "spine_01")

# ------------------------------------------------------------------ star-metal mantle + twin-horned collar
ROWS = ((0.0, 0), (0.17, 0), (0.34, 0), (0.32, 1), (0.50, 1), (0.68, 1), (0.66, 2), (0.84, 2), (1.0, 2))
def mantle_fn(U, V):
    idx = np.clip(np.rint(V * (len(ROWS) - 1)).astype(int), 0, len(ROWS) - 1)
    t = np.array([r[0] for r in ROWS])[idx]
    st = np.array([r[1] for r in ROWS])[idx]
    a = U * TAU
    ca, sa = np.cos(a), np.sin(a)
    ryr = np.where(sa < 0, 0.186, 0.200)
    rx = 0.100 + (0.282 - 0.100) * t
    ry = 0.102 + (ryr - 0.102) * t
    zr = 1.512 - 0.105 * np.abs(sa) ** 1.6
    z = 1.558 + (zr - 1.558) * t ** 0.85 - 0.010 * st
    return np.stack([rx * ca, 0.028 + ry * sa, z], axis=-1)
gm = g_grid(mantle_fn, 28, len(ROWS), close_u=True)
mantle = add("mantle", gm, "metal", "starmetal", solidify=0.006, bone="spine_03", sharp=40)
add("mantle_trim", g_tube(rim_of(gm, 28), 0.0065, n=6, closed=True), "metal", "silver", bone="spine_03", sharp=60)

def collar_h(t):
    return 0.045 + 0.20 * np.exp(-((t - 0.26) / 0.125) ** 2) + 0.20 * np.exp(-((t - 0.74) / 0.125) ** 2)
def collar_fn(U, V):
    a = TAU * (-0.035 + 0.57 * U)
    h = collar_h(U)
    z = 1.548 + V * h
    k = 1 + 0.50 * V * (h / 0.2)
    return np.stack([0.116 * k * np.cos(a), 0.03 + 0.124 * (1 + 0.62 * V * (h / 0.2)) * np.sin(a), z], axis=-1)
gc = g_grid(collar_fn, 25, 5, close_u=False)
collar = add("collar", gc, "metal", "starmetal", solidify=0.006, bone="spine_03", sharp=40)
add("collar_trim", g_tube(rim_of(gc, 25), 0.006, n=6, closed=False), "metal", "silver", bone="spine_03", sharp=60)
apply_mods(mantle)
for i, (px, py, shp) in enumerate(((0.0, -0.3, star4(0.026)), (0.12, -0.22, rhomb(0.011, 0.022)), (-0.12, -0.22, rhomb(0.011, 0.022)),
                                   (0.245, 0.03, star4(0.021)), (-0.245, 0.03, star4(0.021)), (0.0, 0.33, star4(0.026)))):
    decal("rune_mantle_%d" % i, shp, mantle, (px, py, 1.49), "emis", "star", bone="spine_03", up=(0, 0, 1), out=(px, py, 0.6))

# ------------------------------------------------------------------ hip props: star-chart tome (left), astrolabe (right front)
cover, pages = g_book(0.135, 0.185, 0.046)
BK = T(0.262, 0.035, 0.885) @ R('Y', -7) @ R('Z', 90)
props("tome", BK, [
    (cover, "leather", "tome", dict(bevel=0.003, sharp=40)),
    (pages, "cloth", "paper", dict(sharp=40)),
    (gxf(g_corner(0.034), T(0.0675, -0.0245, -0.0925) @ R('Y', -90)), "metal", "silver", dict(sharp=30)),
    (gxf(g_corner(0.034), T(0.0675, -0.0245, 0.0925) @ R('Y', 180)), "metal", "silver", dict(sharp=30)),
    (gxf(g_prism(star4(0.026), 0.004), T(-0.014, -0.025, 0.0)), "metal", "silver", dict(sharp=30)),
    (g_box(0.012, 0.052, 0.189, (0.040, 0, 0)), "leather", "belt", dict(sharp=40)),
], "pelvis")
add("tome_strap", g_tube([(0.262, 0.015, 0.975), (0.236, 0.012, 1.03), (0.186, 0.004, 1.085)], 1.0, n=6, sx=0.004, sy=0.011,
                         up=(0, 1, 0)), "leather", "belt", bone="pelvis", sharp=60)
AN = np.array((-0.615, -0.788, 0.0))
AC = np.array((-0.137, -0.130, 0.945)) + AN * 0.016
AM = frame(AC, AN, (0, 0, 1))
props("astrolabe", AM, [
    (g_lathe([(0.041, -0.004), (0.054, -0.004), (0.054, 0.004), (0.041, 0.004), (0.041, -0.004)], n=16), "metal", "silver", dict(sharp=40)),
    (g_lathe([(0.0, 0.002), (0.041, 0.002), (0.041, -0.002), (0.0, -0.002)], n=16), "metal", "starmetal", dict(sharp=40)),
    (gxf(g_box(0.092, 0.011, 0.004, (0, 0, 0.005)), R('Z', 35)), "metal", "silver", dict(sharp=40)),
    (gxf(g_prism(star4(0.015), 0.004), T(0, 0, 0.008) @ R('X', 90)), "emis", "star", dict(sharp=20)),
    (g_tube(ring_path(0.012, 8, "xz", (0, 0.062, 0)), 0.003, n=4, closed=True), "metal", "silver", dict(sharp=60)),
], "pelvis")
add("astrolabe_chain", g_chain([AC + np.array((0, 0, 0.068)), AC + np.array((0.012, 0.006, 0.105)), (-0.118, -0.112, 1.07)],
                               link=0.022, wire=0.003), "metal", "silver", bone="pelvis", sharp=60)

# ------------------------------------------------------------------ hair
add_hair(c, "Hair_Long")

# ------------------------------------------------------------------ staves (weapon local: +Z up the staff)
G = staff_grip()

# tier 1: plain ash-wood staff with a crook wrapped in cord and an iron ferrule
p1, r1 = shaft(-0.80, 0.80, 0.0175, 0.0155, 0.012, 10, 3)
crook = [(0, 0, 0.80), (0.012, 0, 0.87), (0.05, 0, 0.93), (0.105, 0, 0.945), (0.150, 0, 0.905), (0.150, 0, 0.845), (0.110, 0, 0.815), (0.075, 0, 0.83)]
wpn("wpn_1", G, [
    (tube_w(p1, r1, 8), "leather", "wood", dict(sharp=60)),
    (tube_w(crook, [0.0155, 0.0155, 0.015, 0.014, 0.013, 0.012, 0.010, 0.007], 8), "leather", "wood", dict(sharp=60)),
    (g_lathe([(0.0, -0.86), (0.014, -0.82), (0.021, -0.80), (0.021, -0.75)], n=8), "metal", "iron", dict(sharp=50)),
    (tube_w([(0, 0, -0.07), (0, 0, 0.10)], 0.0205, 8), "leather", "belt", dict(sharp=60)),
    (band(0.76, 0.019), "metal", "iron", dict(sharp=50)),
])
# tier 2: dark shaft, star-metal armillary head with a silver star
p2, r2 = shaft(-0.82, 0.80, 0.017, 0.016, 0.0, 6)
HZ = 0.935
arm_rings = [
    (g_tube(ring_path(0.092, 20, "xz", (0, 0, HZ)), 0.008, n=6, closed=True), "metal", "starmetal", dict(sharp=60)),
    (g_tube(ring_path(0.092, 20, "yz", (0, 0, HZ)), 0.008, n=6, closed=True), "metal", "starmetal", dict(sharp=60)),
    (gxf(g_tube(ring_path(0.108, 20, "xy"), 0.0065, n=6, closed=True), T(0, 0, HZ) @ R('X', 24) @ R('Y', 16)), "metal", "silver", dict(sharp=60)),
]
star_core = gjoin(gxf(g_gem(0.020, 0.085, 4, 0.5), T(0, 0, HZ)), gxf(g_gem(0.016, 0.07, 4, 0.5), T(0, 0, HZ) @ R('X', 90)),
                  gxf(g_gem(0.016, 0.07, 4, 0.5), T(0, 0, HZ) @ R('Y', 90)))
wpn("wpn_2", G, [
    (tube_w(p2, r2, 8), "leather", "darkwood", dict(sharp=60)),
    (g_lathe([(0.0, -0.90), (0.012, -0.85), (0.022, -0.82), (0.020, -0.76)], n=8), "metal", "starmetal", dict(sharp=50)),
    (g_lathe([(0.019, 0.76), (0.026, 0.79), (0.021, 0.815), (0.030, 0.835), (0.012, 0.85)], n=8), "metal", "starmetal", dict(sharp=50)),
    (band(0.14, 0.0195), "metal", "silver", dict(sharp=50)), (band(-0.10, 0.0195), "metal", "silver", dict(sharp=50)),
    (band(0.45, 0.019), "metal", "silver", dict(sharp=50)),
    (star_core, "metal", "silver", dict(sharp=20)),
] + arm_rings)
# tier 3: twin crescents cradling a floating star-crystal, orbiting shards, glowing bands
p3, r3 = shaft(-0.82, 0.80, 0.0175, 0.0165, 0.0, 6)
CZ = 0.965
cres = g_prism(crescent(0.125, 0.105, 0.045, 10), 0.016)
shards = []
for i, (ang, rr, dz, sc) in enumerate(((20, 0.165, 0.10, 1.0), (140, 0.18, 0.02, 0.8), (255, 0.155, 0.19, 0.7), (320, 0.175, 0.27, 0.55))):
    a = math.radians(ang)
    shards.append((gxf(g_gem(0.013 * sc, 0.06 * sc, 4, 0.55), T(rr * math.cos(a), rr * math.sin(a), CZ + dz) @ R('X', 18 * (i - 1))),
                   "emis", "star", dict(sharp=20)))
wpn("wpn_3", G, [
    (tube_w(p3, r3, 8), "leather", "darkwood", dict(sharp=60)),
    (g_lathe([(0.0, -0.92), (0.010, -0.86), (0.023, -0.82), (0.020, -0.76)], n=8), "metal", "silver", dict(sharp=50)),
    (g_lathe([(0.019, 0.74), (0.028, 0.78), (0.022, 0.81), (0.034, 0.84), (0.020, 0.865)], n=8), "metal", "starmetal", dict(sharp=50)),
    (gxf(cres, T(0, 0, CZ)), "metal", "starmetal", dict(bevel=0.004, sharp=35)),
    (gxf(cres, T(0, 0, CZ) @ R('Z', 90) @ S(0.82, 1, 0.82)), "metal", "silver", dict(bevel=0.004, sharp=35)),
    (gxf(g_gem(0.050, 0.23, 6, 0.6), T(0, 0, CZ + 0.165)), "emis", "star", dict(sharp=20)),
    (band(0.60, 0.0185, 0.014), "emis", "star", dict(sharp=60)), (band(0.30, 0.0185, 0.014), "emis", "star", dict(sharp=60)),
    (band(0.16, 0.020), "metal", "silver", dict(sharp=50)), (band(-0.10, 0.020), "metal", "silver", dict(sharp=50)),
    (band(0.675, 0.020), "metal", "silver", dict(sharp=50)),
] + shards)

finalize(c, CID, hair_tint=(0.36, 0.34, 0.33), skin=dict(gain=0.97, sat=0.85),
         tex={"cloth": 1024, "leather": 512, "metal": 1024}, decimate={"cloth": 0.8},
         notes="Pose-tested: Spell_Simple_Idle_Loop, Spell_Simple_Shoot, Idle_Loop, Jog_Fwd_Loop, Roll. Staves wpn_1..3 are "
               "skinned 100% to hand_r (the UAL spell clips cast with the LEFT hand; the right hand carries the staff, which "
               "is open in those clips).")
