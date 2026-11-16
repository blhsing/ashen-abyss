"""hero_soulbinder -- gaunt summoner in tattered grave-vestments: deep cowl crowned with a bone band and antlers,
blank bone visor with soul-lit eye slits, rib harness with a soul gem, beast-skull pauldron, soul cage at the hip.
Scythe-headed staves in the right hand."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_cst import *

CID = "hero_soulbinder"
c = start("male")
gaunt(c, 0.10, 0.14)
B = c.B

# ------------------------------------------------------------------ source materials
mats.cloth("shroud", base=(0.040, 0.034, 0.050), folds=0.9, ash=0.6, ash_h=0.6, worn=0.6, var=0.35)
mats.cloth("wrap", base=(0.080, 0.050, 0.095), folds=0.6, ash=0.35, ash_h=0.5, worn=0.6, bands=[(0.0, 0.07, (0.17, 0.15, 0.115))])
mats.cloth("cowl", base=(0.030, 0.027, 0.034), folds=0.5, ash=0.1, worn=0.5)
mats.cloth("vest", base=(0.050, 0.043, 0.052), folds=0.4, ash=0.0)
mats.cloth("bandage", base=(0.19, 0.165, 0.125), folds=0.2, ash=0.3, worn=0.6, weave=2.0)
mats.cloth("hose", base=(0.036, 0.033, 0.035), folds=0.5, ash=0.7, ash_h=0.5)
mats.leather("boot", base=(0.035, 0.026, 0.02), ash=0.3, ash_h=0.25)
mats.leather("belt", base=(0.05, 0.032, 0.02))
mats.wood("wood", base=(0.06, 0.042, 0.03))
mats.wood("darkwood", base=(0.026, 0.022, 0.024))
mats.bone("bone")
mats.bone("oldbone", base=(0.37, 0.31, 0.215), stain=(0.12, 0.08, 0.04))
mats.metal("iron", base=(0.16, 0.155, 0.15), rough=(0.45, 0.7), rust_amt=0.4, ash=0.0, pattern="hammered")
mats.metal("blade", base=(0.36, 0.36, 0.37), rough=(0.22, 0.45), rust_amt=0.15, ash=0.0, pattern="brushed", edge_amt=1.0)
mats.metal("blade_dark", base=(0.10, 0.105, 0.12), rough=(0.25, 0.5), rust_amt=0.05, ash=0.0, pattern="brushed",
           edge=(0.5, 0.55, 0.5), edge_amt=1.0, soot=0.3)
mats.emis("soul", (0.10, 1.0, 0.22), strength=3.2)

# ------------------------------------------------------------------ under layer
shirt(c, "vest", bottom=1.0, top=1.52, sleeve=0.50, offset=0.010, smooth=3, name="vest")
mw = (B.w("lowerarm", "upperarm") > 0.5) & (np.abs(B.x) > 0.465) & (np.abs(B.x) < 0.735) & (B.w(*BONES_HAND) < 0.5)
shell(B, mw, 0.006, "wraps", "cloth", "bandage", smooth=1, sharp=180)
hide(c, mw & (np.abs(B.x) > 0.49) & (np.abs(B.x) < 0.715))
cpts = []
for t in np.linspace(0, 1, 30):
    a = TAU * 2.6 * t
    cpts.append((0.492 + 0.21 * t, 0.068 + 0.10 * math.cos(a), 1.435 + 0.10 * math.sin(a)))
cord = add("cord_l", g_tube(wrap_path(c, cpts, 0.010), 0.0055, n=5), "leather", "belt", sharp=180, bone="lowerarm_l")
mirrored(cord)
trousers(c, "hose", top=1.03, bottom=0.30, offset=0.009, smooth=3)
boots(c, "boot", top=0.36, offset=0.012, cuff=0.005)

# ------------------------------------------------------------------ tattered grave-skirt + hip wrap
SK = [(0.10, 0, 0.02, 0.275, 0.235, 0.26), (0.50, 0, 0.02, 0.235, 0.195, 0.215), (0.92, 0, 0.022, 0.192, 0.155, 0.162),
      (1.075, 0, 0.012, 0.166, 0.126, 0.126)]
skirt = robe(c, "shroud", SK, nu=42, nv=14, name="skirt", pleats=10, pleat_amp=0.04, thick=0.006, ragged=0.02,
             u0=0.75 + 0.045, u1=1.75 - 0.045, close=False, calf_to_thigh=0.55, mod=tails_mod(k=9, depth=0.36, seed=4, power=2.2))
OS = [(0.60, 0, 0.02, 0.247, 0.207, 0.227), (0.92, 0, 0.022, 0.206, 0.169, 0.176), (1.085, 0, 0.012, 0.177, 0.137, 0.137)]
wrap = robe(c, "wrap", OS, nu=34, nv=9, name="hipwrap", pleats=7, pleat_amp=0.04, thick=0.006, ragged=0.012,
            u0=0.68, u1=1.58, close=False, calf_to_thigh=0.0, mod=tails_mod(k=6, depth=0.24, seed=9, power=2.0))
BZ = 1.075
bcy = body_ring(c, BZ)[1]
BRX, BRY = 0.188, 0.148
belt(c, BZ, "belt", width=0.062, thick=0.009, rx=BRX, ry=BRY)
BK = frame((0.0, bcy - BRY - 0.012, BZ), (0, -1, 0), (0, 0, 1))
props("buckle", BK, [
    (g_lathe([(0.013, 0.007), (0.031, 0.009), (0.038, 0.0), (0.031, -0.004), (0.013, -0.004), (0.013, 0.007)], n=10), "bone", "oldbone", dict(sharp=50)),
    (g_box(0.07, 0.012, 0.008, (0, 0, 0.004)), "bone", "bone", dict(bevel=0.003, sharp=40)),
], "spine_01")
# finger-bone fringe on the left front of the belt
for i, (x, ln) in enumerate(((0.050, 0.075), (0.082, 0.10), (0.112, 0.068), (0.140, 0.088))):
    y = bcy - BRY * math.sqrt(1 - (x / BRX) ** 2) - 0.014
    n = np.array((x / BRX ** 2, (y - bcy) / BRY ** 2, 0.0))
    n /= np.linalg.norm(n)
    add("fringe_%d" % i, gxf(g_bone(ln, 0.0068, 1.7), frame((x, y, BZ - 0.034), n * 0.24 + np.array((0, 0, -1.0)), n)), "bone", "bone",
        bone="pelvis", sharp=50)
# tattered back cloak hanging from under the capelet
CK = [(0.42, 0, 0.02, 0.285, 0.24, 0.275), (0.90, 0, 0.022, 0.225, 0.18, 0.205), (1.075, 0, 0.012, 0.20, 0.16, 0.176),
      (1.30, 0, 0.02, 0.205, 0.16, 0.166), (1.40, 0, 0.026, 0.195, 0.15, 0.158)]
robe(c, "cowl", CK, nu=22, nv=16, name="backcloak", pleats=12, pleat_amp=0.05, thick=0.006, ragged=0.03, u0=0.105, u1=0.395,
     close=False, calf_to_thigh=0.6, mod=tails_mod(k=4, depth=0.42, seed=7, power=1.8))

# ------------------------------------------------------------------ soul cage (right front, in the slit of the hip wrap)
def soul_cage(r=0.036, h=0.10):
    top = g_lathe([(0, h / 2 + 0.014), (r * 0.5, h / 2 + 0.009), (r * 0.78, h / 2), (0, h / 2 - 0.004)], n=8)
    bot = g_lathe([(0, -h / 2 - 0.02), (r * 0.42, -h / 2 - 0.007), (r * 0.72, -h / 2), (0, -h / 2 + 0.004)], n=8)
    bars = []
    for i in range(5):
        a = TAU * i / 5
        pts = [(r * 0.7 * math.cos(a) * k, r * 0.7 * math.sin(a) * k, z)
               for (k, z) in ((1.0, -h / 2), (1.4, -h / 4), (1.52, 0.0), (1.4, h / 4), (1.0, h / 2))]
        bars.append(g_tube(pts, 0.0048, n=4))
    return gjoin(top, bot, *bars)
LC = np.array((-0.192, -0.132, 0.875))
props("cage", T(*LC), [
    (soul_cage(), "bone", "bone", dict(sharp=50)),
    (gxf(g_gem(0.021, 0.074, 6, 0.55), T(0, 0, -0.006)), "emis", "soul", dict(sharp=20)),
], "pelvis")
add("cage_cord", g_beads([LC + np.array((0.004 * k, 0.003 * k, 0.07 + 0.027 * k)) for k in range(5)], 0.0085, 6, 4), "bone", "oldbone",
    bone="pelvis", sharp=60)

# ------------------------------------------------------------------ rib harness with a soul gem on the sternum
def chest_pt(a_deg, z, gap=0.03):
    cx, cy, rx, ryf, ryb = body_ring(c, z, gap)
    a = math.radians(a_deg)
    return (rx * math.sin(a), cy - ryf * math.cos(a), z)
ribs = []
for zi in (1.335, 1.268, 1.200):
    for s in (1, -1):
        pts = []
        for t in np.linspace(0, 1, 9):
            p = chest_pt(9 + 54 * t, zi - 0.055 * (1 - t) ** 1.5)
            pts.append((s * p[0], p[1], p[2]))
        P = wrap_path(c, pts, 0.021)
        ribs.append(g_tube(P, list(np.linspace(0.0125, 0.0055, 9)), n=6))
rib_o = add("ribs", gjoin(*ribs), "bone", "bone", sharp=60)
w_transfer(rib_o, c.ref, smooth=2, only=["spine_03", "spine_02"])
P1 = wrap_path(c, [(0.0, -0.3, 1.372)], 0.017)[0]
P2 = wrap_path(c, [(0.0, -0.3, 1.125)], 0.017)[0]
SL = float(np.linalg.norm(P1 - P2))
SM = frame(P2, P1 - P2, (0, -1, 0))
s_out = [(0.0, 0.0), (0.020, 0.03), (0.033, SL * 0.42), (0.038, SL * 0.80), (0.026, SL), (-0.026, SL), (-0.038, SL * 0.80),
         (-0.033, SL * 0.42), (-0.020, 0.03)]
stern = add("sternum", gxf(g_prism(s_out, 0.013), SM), "bone", "oldbone", bevel=0.003, sharp=40)
w_transfer(stern, c.ref, smooth=0, only=["spine_03", "spine_02"])
add("soulgem", gxf(g_gem(0.025, 0.034, 6, 0.55), SM @ T(0, 0.004, SL * 0.60) @ R('X', -90)), "emis", "soul", bone="spine_03", sharp=20)

# ------------------------------------------------------------------ ragged capelet (body shell, so it follows the arms)
zmin = 1.34 - 0.6 * (B.y - 0.02)
mcap = (B.w(*BONES_TORSO, "clavicle", "neck_01", "upperarm") > 0.5) & (B.z > zmin) & (np.abs(B.x) < 0.345) & (B.z < 1.545) & \
    (B.w(*BONES_HAND, "lowerarm") < 0.3)
cape = shell(B, mcap, 0.027, "capelet", "cloth", "cowl", smooth=4, sharp=180, solidify=0.006)
chew_edge(cape, rounds=2, p=0.38, seed=3, keep=lambda co: co.z > 1.475 and abs(co.x) < 0.13)

# ------------------------------------------------------------------ cowl, bone band, antlers, blank visor
HST = [(1.455, 0.025, 0.215, 0.150, 0.175), (1.50, 0.018, 0.160, 0.128, 0.150), (1.55, 0.004, 0.106, 0.116, 0.112),
       (1.61, -0.015, 0.112, 0.134, 0.118), (1.69, -0.024, 0.124, 0.146, 0.136), (1.75, -0.024, 0.116, 0.138, 0.132),
       (1.80, -0.018, 0.094, 0.112, 0.112), (1.84, -0.008, 0.058, 0.070, 0.074), (1.862, 0.0, 0.024, 0.030, 0.032),
       (1.868, 0.0, 0.0, 0.0, 0.0)]
hd, gh = cowl(c, "cowl", HST, name="hood", thick=0.008, chin=1.575, brow=1.745, open_deg=60.0, peak=0.03, nu=28, nv=18)
BP = ring_of(gh, 28, 13, grow=0.012)
add("crown_band", g_tube(BP, 0.0115, n=6, closed=True), "bone", "oldbone", bone="Head", sharp=60)
teeth = []
for i in range(len(BP)):
    p = BP[i]
    if p[1] < 0.02 and i % 2 == 0:
        out = np.array((p[0], p[1] + 0.022, 0.0))
        out /= max(np.linalg.norm(out), 1e-6)
        teeth.append(gxf(g_tooth(0.05, 0.0095, 0.006, 5), frame(p + np.array((0, 0, 0.006)), out * 0.22 + np.array((0, 0, 1.0)), out)))
add("crown_teeth", gjoin(*teeth), "bone", "bone", bone="Head", sharp=40)
ant = g_antler(0.84, tines=3)
add("antler_l", gxf(ant, T(0.066, -0.012, 1.795) @ R('Y', 8)), "bone", "oldbone", bone="Head", sharp=60)
add("antler_r", gxf(mirror_x(ant), T(-0.066, -0.012, 1.795) @ R('Y', -8)), "bone", "oldbone", bone="Head", sharp=60)

mm = (B.w("Head") > 0.5) & (B.z > 1.640) & (B.z < 1.775) & (B.y < -0.040)
visor = shell(B, mm, 0.010, "visor", "bone", "bone", smooth=2, sharp=60)
w_rigid(visor, "Head")
hide(c, mm & (B.z > 1.668) & (B.y < -0.058))
hide(c, (B.w("Head") > 0.5) & ((B.z > 1.775) | ((B.y > 0.035) & (B.z > 1.61))))
drop_face_parts(c)
def rot2(outl, deg):
    a = math.radians(deg)
    return [(x * math.cos(a) - z * math.sin(a), x * math.sin(a) + z * math.cos(a)) for x, z in outl]
for s in (1, -1):
    decal("eye_%d" % s, rot2(rhomb(0.019, 0.0065), 14 * s), visor, (0.034 * s, -0.4, 1.707), "emis", "soul", bone="Head",
          ray=(0, 1, 0), depth=0.004, lift=0.003)

# ------------------------------------------------------------------ beast-skull pauldron (left) + rib spikes (right)
PM = T(0.243, 0.060, 1.580) @ R('Y', 17)
snout = gxf(g_lathe([(0.056, 0.0), (0.053, 0.045), (0.045, 0.10), (0.034, 0.14), (0.017, 0.163), (0.0, 0.17)], n=8),
            T(0.035, 0, -0.008) @ R('Y', 90) @ S(0.70, 1.0, 1.0))
pld = []
pld += props("pskull", PM, [
    (gxf(g_sphere(1.0, 10, 6), S(0.084, 0.072, 0.058)), "bone", "bone", dict(sharp=60)),
    (snout, "bone", "bone", dict(sharp=60)),
    (gxf(g_tooth(0.105, 0.018, 0.045, 6), T(-0.025, 0.046, 0.030) @ R('X', -30) @ R('Z', 180)), "bone", "oldbone", dict(sharp=50)),
    (gxf(g_tooth(0.105, 0.018, 0.045, 6), T(-0.025, -0.046, 0.030) @ R('X', 30) @ R('Z', 180)), "bone", "oldbone", dict(sharp=50)),
    (gxf(g_sphere(0.017, 6, 4), T(0.052, 0.049, 0.016)), "emis", "soul", dict(sharp=60)),
    (gxf(g_sphere(0.017, 6, 4), T(0.052, -0.049, 0.016)), "emis", "soul", dict(sharp=60)),
], "clavicle_l")
for o in pld:
    w_func(o, lambda co: {"clavicle_l": 0.62, "upperarm_l": 0.38})
spk = [gxf(g_sphere(1.0, 8, 5), T(-0.238, 0.06, 1.538) @ S(0.075, 0.07, 0.034))]
for i, (ln, tilt, yy, rr) in enumerate(((0.19, -6, -0.035, 0.017), (0.27, -20, 0.01, 0.019), (0.21, -36, 0.055, 0.017))):
    spk.append(gxf(g_tooth(ln, rr, 0.06, 6), T(-0.205 - 0.034 * i, 0.055 + yy, 1.535) @ R('Y', tilt) @ R('X', -8 + 10 * i)))
so = add("shoulder_spikes", gjoin(*spk), "bone", "oldbone", sharp=50)
w_func(so, lambda co: {"clavicle_r": 0.62, "upperarm_r": 0.38})

# ------------------------------------------------------------------ scythe staves (weapon local: +Z up the staff, blade towards +X)
G = staff_grip()
def blade(L, w0, drop, rise, th, zr, src, tilt=0.0):
    outl, back, edge = g_scythe(L, w0, drop, rise)
    M = T(0.010, 0, zr) @ R('Y', -tilt)
    return (gxf(g_prism(outl, th), M), "metal", src, dict(sharp=30)), M, back, edge

# tier 1: crooked staff with a crude iron sickle lashed on, bone charm
b1, M1, _, _ = blade(0.33, 0.075, 0.16, 0.035, 0.007, 0.945, "iron")
wpn("wpn_1", G, [
    (tube_w(gnarl(zline(-0.86, 0.99, 10), 0.012, 5), list(np.linspace(0.018, 0.0155, 10)), 8), "leather", "wood", dict(sharp=60)),
    b1,
    (tube_w([(0, 0, 0.865), (0, 0, 0.965)], 0.0215, 8), "leather", "belt", dict(sharp=60)),
    (tube_w([(0, 0, -0.08), (0, 0, 0.10)], 0.0205, 8), "leather", "belt", dict(sharp=60)),
    (gxf(g_bone(0.085, 0.0065, 1.7), T(-0.022, 0, 0.87) @ R('Y', -165)), "bone", "bone", dict(sharp=50)),
    (gxf(g_bone(0.06, 0.006, 1.7), T(-0.020, 0.012, 0.86) @ R('Y', -150)), "bone", "oldbone", dict(sharp=50)),
])
# tier 2: dark shaft, long steel blade on a vertebra socket, horned skull finial, back spike
b2, M2, _, _ = blade(0.45, 0.092, 0.24, 0.05, 0.008, 0.955, "blade")
wpn("wpn_2", G, [
    (tube_w(zline(-0.86, 0.99, 6), list(np.linspace(0.0175, 0.016, 6)), 8), "leather", "darkwood", dict(sharp=60)),
    b2,
    (g_lathe([(0.018, 0.865), (0.035, 0.885), (0.026, 0.915), (0.037, 0.945), (0.027, 0.975), (0.033, 0.995), (0.0, 1.005)], n=8), "bone", "bone", dict(sharp=50)),
    (gxf(g_sphere(1.0, 8, 6), T(0, 0, 1.045) @ S(0.040, 0.044, 0.046)), "bone", "oldbone", dict(sharp=60)),
    (gxf(g_tooth(0.085, 0.012, 0.03, 5), T(0, 0.022, 1.07) @ R('X', -35)), "bone", "oldbone", dict(sharp=50)),
    (gxf(g_tooth(0.085, 0.012, 0.03, 5), T(0, -0.022, 1.07) @ R('X', 35)), "bone", "oldbone", dict(sharp=50)),
    (gxf(g_tooth(0.13, 0.015, 0.035, 5), T(-0.02, 0, 0.935) @ R('Y', -105)), "metal", "blade", dict(sharp=40)),
    (g_lathe([(0.0, -0.93), (0.012, -0.88), (0.022, -0.86), (0.020, -0.80)], n=8), "metal", "iron", dict(sharp=50)),
    (band(0.16, 0.0195, 0.03), "bone", "bone", dict(sharp=50)), (band(-0.12, 0.0195, 0.03), "bone", "bone", dict(sharp=50)),
])
# tier 3: great dark blade with a soul-lit edge and bone teeth, rib cage holding a soul flame under the blade
b3, M3, back3, edge3 = blade(0.58, 0.115, 0.31, 0.075, 0.009, 1.0, "blade_dark")
cage3 = []
for i in range(4):
    a = TAU * (i + 0.5) / 4
    pts = [(0.02 * math.cos(a) * k, 0.02 * math.sin(a) * k, z) for (k, z) in ((1.0, 1.02), (2.3, 1.06), (2.75, 1.11), (2.3, 1.16), (1.0, 1.20))]
    cage3.append(g_tube(pts, [0.008, 0.0085, 0.009, 0.0085, 0.008], n=5))
teeth3 = [gxf(g_tooth(0.05 - 0.004 * k, 0.010, 0.012, 5), M3 @ T(back3[k + 1][0], 0, back3[k + 1][1] - 0.004) @ R('Y', 8 + 10 * k))
          for k in range(5)]
wpn("wpn_3", G, [
    (tube_w(zline(-0.88, 1.0, 6), list(np.linspace(0.018, 0.017, 6)), 8), "leather", "darkwood", dict(sharp=60)),
    b3,
    (gxf(g_prism(strip_along(edge3[1:], 0.017, up=True), 0.0125), M3), "emis", "soul", dict(sharp=20)),
    (gjoin(*cage3), "bone", "bone", dict(sharp=50)),
    (gxf(g_gem(0.030, 0.12, 6, 0.55), T(0, 0, 1.10)), "emis", "soul", dict(sharp=20)),
    (g_lathe([(0.019, 0.87), (0.036, 0.89), (0.027, 0.925), (0.038, 0.96), (0.028, 0.995), (0.034, 1.02), (0.0, 1.03)], n=8), "bone", "oldbone", dict(sharp=50)),
    (g_lathe([(0.0, 1.185), (0.024, 1.195), (0.030, 1.215), (0.018, 1.24), (0.0, 1.30)], n=8), "bone", "oldbone", dict(sharp=50)),
    (gjoin(*teeth3), "bone", "bone", dict(sharp=40)),
    (gxf(g_tooth(0.15, 0.016, 0.04, 5), T(-0.02, 0, 0.955) @ R('Y', -100)), "bone", "oldbone", dict(sharp=40)),
    (gxf(g_tooth(0.16, 0.016, 0.02, 6), T(0, 0, -0.86) @ R('X', 180)), "bone", "oldbone", dict(sharp=50)),
    (band(0.55, 0.0195, 0.014), "emis", "soul", dict(sharp=60)), (band(0.24, 0.0195, 0.014), "emis", "soul", dict(sharp=60)),
    (band(0.16, 0.0205, 0.03), "bone", "bone", dict(sharp=50)), (band(-0.12, 0.0205, 0.03), "bone", "bone", dict(sharp=50)),
])

finalize(c, CID, skin=dict(gain=0.88, sat=0.45, tint=(0.94, 0.97, 1.0)),
         tex={"cloth": 1024, "leather": 512, "bone": 512, "metal": 512}, decimate={"cloth": 0.85},
         notes="Pose-tested: Spell_Simple_Shoot, OverhandThrow, Sword_Idle, Idle_Loop, Jog_Fwd_Loop. Scythe staves wpn_1..3 are "
               "skinned 100% to hand_r, blade hooking forward (weapon +X = knuckle side). No hair material: the face is a bone "
               "visor under a cowl (eyes / brows removed).")
