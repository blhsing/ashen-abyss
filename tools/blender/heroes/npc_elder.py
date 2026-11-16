"""npc_elder -- 守燈長老 艾爾溫, the old lantern keeper of Lastlamp: long worn wool robe with deep hem folds, madder shoulder
mantle with its hood thrown back, wide sleeves over linen, rope girdle with the beacon keys and an oil flask, long white
hair and beard, a big caged brass lantern (emis_ flame) carried by its bail.

Lantern hand: Idle_Lantern_Loop (the clip the game plays for the elder) raises the RIGHT fist palm-down, so the lantern
is modelled to hang plumb from the right fist in that clip. Set SIDE = "l" for a left-hand lantern made for
Idle_Torch_Loop (the clip that raises the left fist)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_twn import *

CID = "npc_elder"
SIDE = "r"
# rest-space direction that is plumb (world down) in the clip, measured with twn_probe.py at t = 0.5
PLUMB = {"r": (0.037, 0.037, -0.999),        # hand_r in Idle_Lantern_Loop
         "l": (-0.229, 0.956, 0.186)}        # hand_l in Idle_Torch_Loop
c = start("male")
gaunt(c, chest=0.12, arms=0.20)
B = c.B

# ------------------------------------------------------------------ source materials
mats.cloth("wool_robe", base=(0.165, 0.150, 0.125), folds=0.9, ash=0.75, ash_h=0.6, worn=0.85, var=0.4)
mats.cloth("wool_robe_top", base=(0.165, 0.150, 0.125), folds=0.5, ash=0.2, worn=0.8, var=0.4)
mats.cloth("cowl_mantle", base=(0.150, 0.056, 0.032), folds=0.6, ash=0.4, ash_h=0.5, worn=0.8, var=0.35)
mats.cloth("under_linen", base=(0.30, 0.27, 0.21), folds=0.4, ash=0.2, worn=0.6)
mats.cloth("patch", base=(0.095, 0.080, 0.058), folds=0.0, ash=0.2, worn=0.7, weave=2.0)
mats.flat("rope", (0.24, 0.19, 0.12), rough=0.9)
mats.flat("wax", (0.55, 0.50, 0.38), rough=0.5)
mats.leather("shoe", base=(0.05, 0.035, 0.022), ash=0.7, ash_h=0.3)
mats.leather("flask", base=(0.09, 0.05, 0.025), worn=0.7)
mats.metal("brass", base=(0.36, 0.24, 0.075), rough=(0.30, 0.56), rust=(0.04, 0.07, 0.05), rust_amt=0.3, ash=0.1, pattern="hammered")
mats.metal("iron", base=(0.17, 0.165, 0.16), rough=(0.45, 0.72), rust_amt=0.4, ash=0.0)
mats.emis("flame", (1.0, 0.66, 0.18), strength=5.5)

# ------------------------------------------------------------------ linen under-sleeves, robe top, wide sleeves, shoes
mu = (B.w("lowerarm", "upperarm") > 0.5) & (np.abs(B.x) > 0.44) & (np.abs(B.x) < 0.708) & (B.w(*BONES_HAND) < 0.5)
shell(B, mu, 0.006, "undersleeves", "cloth", "under_linen", smooth=2, sharp=180)
mc = (B.w(*BONES_TORSO, "clavicle") > 0.5) & (B.z > 1.05) & (B.z < 1.50) & (np.abs(B.x) < 0.20)
top = shell(B, mc, 0.020, "robe_top", "cloth", "wool_robe_top", smooth=5, sharp=180)
wrinkle_shell(top, 0.005, 34.0, "z", 2, mask=lambda co: sstep(co[:, 2], 1.26, 1.14) * (1 - 0.5 * sstep(np.abs(co[:, 0]), 0.10, 0.17)))
sl = sleeve(c, "wool_robe_top", [(0.17, 0.0, 0.0, 0.094, 0.086, 0.086), (0.32, 0.010, 0.0, 0.086, 0.078, 0.078),
                                 (0.47, 0.026, 0.0, 0.094, 0.080, 0.080), (0.625, 0.062, 0.0, 0.138, 0.094, 0.094)], n=16, nv=11, mirror=False)
wrinkle_shell(sl, 0.006, 30.0, "x", 5, mask=lambda co: 0.45 + 0.55 * np.exp(-((co[:, 0] - 0.46) / 0.09) ** 2))
solidify(sl, 0.006)
mirrored(sl)
boots(c, "shoe", top=0.16, offset=0.012)
hide(c, (B.z < 1.47) & (np.abs(B.x) < 0.69) & (B.z > 0.14))

# ------------------------------------------------------------------ long robe skirt: irregular deep folds, ragged ashen hem, patches
ELD = [(0.05, 0, 0.03, 0.258, 0.212, 0.240), (0.50, 0, 0.025, 0.222, 0.182, 0.202), (0.92, 0, 0.02, 0.196, 0.158, 0.168),
       (1.08, 0, 0.0, 0.160, 0.122, 0.130), (1.15, 0, -0.004, 0.148, 0.112, 0.118)]
skirt = robe(c, "wool_robe", ELD, nu=48, nv=16, name="robe_skirt", calf_to_thigh=0.55, ragged=0.022,
             mod=fold_mod([(7, 0.095, 0.1), (12, 0.034, 0.5)], cy=0.025, hem_pow=1.0, top=0.05, sag=0.010))
apply_mods(skirt)
decal("patch_a", bar(0.045, 0.038), skirt, (0.105, -0.6, 0.40), "cloth", "patch", ray=(0, 1, 0), depth=0.004, lift=0.002)
decal("patch_b", [(-0.03, -0.035), (0.034, -0.03), (0.028, 0.036), (-0.036, 0.03)], skirt, (-0.13, -0.6, 0.68), "cloth", "patch", ray=(0, 1, 0),
      depth=0.004, lift=0.002)
decal("patch_c", bar(0.036, 0.05), skirt, (0.06, 0.6, 0.55), "cloth", "patch", ray=(0, -1, 0), depth=0.004, lift=0.002)

# ------------------------------------------------------------------ rope girdle (two turns, knot, hanging ends), keys, oil flask
GC = 0.012
for k, (zz, sd) in enumerate(((1.086, 1), (1.064, 2))):
    add("cord_%d" % k, g_tube(cord_ring(zz, 0.179, 0.137, GC, seed=sd), 0.0105, n=6, closed=True), "cloth", "rope", bone="spine_01", sharp=180)
KN = np.array((0.055, GC - 0.137 - 0.006, 1.072))
add("cord_knot", lumpy(gxf(g_sphere(1.0, 8, 6), T(*KN) @ S(0.026, 0.020, 0.024)), 0.18, 60.0, 4), "cloth", "rope", bone="spine_01", sharp=180)
def end_w(co):
    t = sstep(1.04 - co[:, 2], 0.0, 0.25) * 0.8
    return {"spine_01": 1 - t, "thigh_l": t}
for k, (dx, zb) in enumerate(((0.012, 0.70), (0.050, 0.79))):
    pth = [KN + np.array((0.0, -0.004, -0.012)), KN + np.array((dx * 0.5, -0.012, -0.10)), (KN[0] + dx, GC - 0.176, (KN[2] + zb) / 2),
           (KN[0] + dx * 1.3, GC - 0.196, zb)]
    e = add("cord_end_%d" % k, gjoin(g_tube(pth, 0.0095, n=6), gxf(g_sphere(1.0, 6, 4), T(*pth[-1]) @ S(0.016, 0.016, 0.022))), "cloth", "rope", sharp=180)
    w_func(e, end_w)
KR = np.array((-0.176, GC - 0.045, 1.035))
keys = [(gxf(g_ring(0.030, 0.0032, n=12, m=4), T(*KR) @ R('Z', 20) @ R('X', 90)), "metal", "iron", dict(sharp=60))]
for i, (a, ln) in enumerate(((-18, 0.095), (4, 0.110), (24, 0.085))):
    keys.append((gxf(g_key(ln, 0.011), T(KR[0] - 0.004 * i, KR[1] + 0.012 * (i - 1), KR[2] - 0.028) @ R('Z', 20 + 25 * (i - 1)) @ R('Y', a)), "metal",
                 "brass" if i == 1 else "iron", dict(sharp=40)))
keys.append((g_tube([KR + np.array((0.004, 0.0, 0.028)), KR + np.array((0.008, 0.0, 0.05))], 1.0, n=6, sx=0.003, sy=0.008, up=(1, 0, 0)), "leather", "flask", dict(sharp=60)))
props("keys", np.eye(4), keys, "pelvis")
FL = np.array((0.172, GC + 0.075, 0.915))
props("flask", T(*FL) @ R('Z', -30) @ R('X', 8), [
    (g_bottle(0.040, 0.125, 0.013, 8), "leather", "flask", dict(sharp=50)),
    (g_cyl(0.011, 0.122, 0.142, 6), "cloth", "wax", dict(sharp=50)),
    (g_tube([(0.0, 0.0, 0.112), (-0.006, -0.012, 0.150), (-0.010, -0.030, 0.172)], 1.0, n=6, sx=0.003, sy=0.008), "leather", "flask", dict(sharp=60)),
    (gxf(g_ring(0.041, 0.004, n=10, m=4, sy=1.0), T(0, 0, 0.060)), "leather", "shoe", dict(sharp=60)),
], "pelvis")

# ------------------------------------------------------------------ shoulder mantle (ragged, radiating folds) + hood thrown back
zmin = 1.30 - 0.42 * B.y
core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zmin) & (B.z < 1.555) & (np.abs(B.x) < 0.23)
arms = (B.w("upperarm", "clavicle") > 0.5) & (np.abs(B.x) < 0.372) & (B.z > 1.36)
mm = (core | arms) & (B.w(*BONES_HAND, "lowerarm") < 0.3)
mantle = shell(B, mm, lambda co: 0.036 + 0.016 * sstep(np.abs(co[:, 0]), 0.16, 0.24), "mantle", "cloth", "cowl_mantle", smooth=6, sharp=180)
chew_edge(mantle, rounds=1, p=0.28, seed=3)
co = get_co(mantle)
ang = np.arctan2(co[:, 1] - 0.03, co[:, 0])
rr = np.hypot(co[:, 0], co[:, 1] - 0.03)
d = 0.0075 * np.sin(9 * ang + 1.3 * np.sin(3 * ang + 0.7)) * sstep(1.52 - co[:, 2], 0.0, 0.12)
co[:, 0] += co[:, 0] / np.maximum(rr, 1e-6) * d
co[:, 1] += (co[:, 1] - 0.03) / np.maximum(rr, 1e-6) * d
set_co(mantle, co)
mantle["ds"] = True
path, rad = [], []
for i in range(14):
    a = TAU * i / 14
    s_, c_ = math.sin(a), math.cos(a)
    path.append((0.112 * c_, 0.030 + (0.122 if s_ > 0 else 0.098) * s_, 1.532 + (0.012 * s_ if s_ > 0 else 0.058 * s_)))
    rad.append(0.046 if s_ > -0.2 else 0.031)
roll = fur_roll(path, rad, "cowl_mantle", name="hood_roll", noise=0.009, scale=24.0, n=7)
w_func(roll, lambda co: {"neck_01": 0.3, "spine_03": 0.7})
fur_roll([(0, 0.110, 1.560), (0, 0.172, 1.480), (0, 0.196, 1.385), (0, 0.186, 1.290), (0, 0.176, 1.235)], [0.036, 0.047, 0.040, 0.022, 0.006],
         "cowl_mantle", name="hood_bag", closed=False, n=10, noise=0.010, scale=22.0, sy=2.3, bone="spine_03")
apply_mods(mantle)
lamp_sigil(mantle, 1.432, (0, 1, 0), "brooch", s=0.72, cls="metal", src="brass", depth=0.006)

# ------------------------------------------------------------------ long white hair and beard
add_hair(c, "Hair_Long")
add_hair(c, "Hair_Beard", scale=(1.06, 1.14, 1.70), pivot=(0, -0.05, 1.615))

# ------------------------------------------------------------------ the keeper's lantern: hangs plumb from the fist in the lantern idle
LM = hand_hang_frame(SIDE, PLUMB[SIDE])
props("lantern", LM, lantern_parts(h=0.185, r=0.062, n=6, frame_src="brass", ring=0.034, flame=1.1), "hand_" + SIDE)

report_unweighted()
tri_report()
finalize(c, CID, hair_tint=(0.56, 0.54, 0.51), hair_gain=1.15, skin=dict(gain=0.93, sat=0.68, age=1.0, stubble=0.0, grime=0.5),
         max_tris=20000, decimate={"skin": 0.72, "hair": 0.6, "cloth": 0.88},
         notes="Pose-tested: Idle_Lantern_Loop, Idle_Talking_Loop, Walk_Loop, Idle_Loop. The lantern is part of body_metal / body_emis, skinned "
               "100%% to hand_%s; it hangs plumb in Idle_Lantern_Loop (right fist raised palm-down) and swings with the hand in other clips." % SIDE)
