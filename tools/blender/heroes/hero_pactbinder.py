"""hero_pactbinder -- binder of ember fiends: asymmetric oxblood long coat (sleeveless and burnt away on the left),
left arm charred black and branded with glowing sigils, chain bandolier, sealed scroll cases, iron circlet with one whole
and one broken horn-prong. Wand in the right hand (wpn_N) + chained grimoire carried by an iron bail in the left (wpn_N_off)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_cst import *

CID = "hero_pactbinder"
c = start("male")
B = c.B

# ------------------------------------------------------------------ source materials
mats.leather("coat", base=(0.027, 0.012, 0.012), edge=(0.13, 0.055, 0.04), ash=0.3, ash_h=0.5, worn=0.42, crack=0.8)
mats.leather("char", base=(0.013, 0.012, 0.013), edge=(0.11, 0.04, 0.03), crack=1.0, rough=(0.65, 0.95), ash=0.0, worn=0.5, bump=0.5)
mats.leather("boot", base=(0.026, 0.02, 0.017), ash=0.2, ash_h=0.22)
mats.leather("belt", base=(0.055, 0.034, 0.02))
mats.leather("tome", base=(0.060, 0.020, 0.020), ash=0.0, worn=0.8)
mats.leather("tome_dark", base=(0.022, 0.019, 0.024), ash=0.0, worn=0.8)
mats.bone("horn", base=(0.085, 0.07, 0.06), stain=(0.02, 0.015, 0.012))
mats.wood("wand", base=(0.036, 0.027, 0.022))
mats.cloth("shirt", base=(0.125, 0.022, 0.026), folds=0.5, ash=0.0, var=0.3)
mats.cloth("hose", base=(0.034, 0.032, 0.036), folds=0.5, ash=0.6, ash_h=0.45)
mats.flat("paper", (0.33, 0.29, 0.21), rough=0.85)
mats.flat("wax", (0.30, 0.02, 0.02), rough=0.45)
mats.metal("iron", base=(0.15, 0.145, 0.14), rough=(0.42, 0.7), rust_amt=0.4, ash=0.0, pattern="hammered")
mats.metal("brass", base=(0.32, 0.21, 0.07), rough=(0.3, 0.55), rust=(0.04, 0.07, 0.05), rust_amt=0.25, ash=0.0)
mats.metal("darksteel", base=(0.08, 0.08, 0.09), rough=(0.3, 0.55), rust_amt=0.1, ash=0.0, edge=(0.45, 0.42, 0.4), edge_amt=0.9)
mats.flat("garnet", (0.22, 0.015, 0.025), rough=0.18)
mats.emis("pact", (1.0, 0.012, 0.045), strength=3.2)

# ------------------------------------------------------------------ under layer
shirt(c, "shirt", bottom=1.0, top=1.52, sleeve=0.24, offset=0.007, smooth=3, name="shirt")
trousers(c, "hose", top=1.03, bottom=0.40, offset=0.010, smooth=3)
boots(c, "boot", top=0.47, offset=0.013, cuff=0.006)

# ------------------------------------------------------------------ coat: torso + right sleeve, left side burnt away at the shoulder
arm_r = (B.x < 0) & (B.w("upperarm", "lowerarm") > 0.5) & (B.x > -0.722) & (B.w(*BONES_HAND) < 0.3)
cap_l = (B.x > 0) & (B.w("upperarm", "clavicle") > 0.5) & (B.x < 0.268) & (B.w("lowerarm", *BONES_HAND) < 0.3)
core = (B.w(*BONES_TORSO, "clavicle", "neck_01", "thigh") > 0.5) & (B.z > 1.05) & (B.z < 1.50) & (B.w(*BONES_HAND) < 0.3)
vneck = (np.abs(B.x) < 0.012 + 0.17 * (B.z - 1.15)) & (B.y < 0.02) & (B.z > 1.15)
mcoat = (core | arm_r | cap_l) & ~vneck
coat = shell(B, mcoat, 0.019, "coat", "leather", "coat", smooth=4, sharp=180)
chew_edge(coat, rounds=2, p=0.42, seed=5, keep=lambda co: co.x < 0.2)
hide(c, arm_r & (B.x < -0.26) & (B.x > -0.70))
mg = m_hands(B, 0.70) & (B.x < 0)
shell(B, mg, 0.005, "glove_r", "leather", "boot", smooth=0, sharp=180)
hide(c, mg & (B.x < -0.72))

CS = [(0.34, 0, 0.02, 0.258, 0.220, 0.246), (0.62, 0, 0.02, 0.236, 0.198, 0.218), (0.92, 0, 0.022, 0.205, 0.168, 0.176),
      (1.085, 0, 0.012, 0.172, 0.132, 0.132)]
def hem(U, V, x, y, z):
    # long on the left side (u = 1.0), short on the right (u = 1.5): a diagonal hem
    rise = 0.33 * (0.5 - 0.5 * np.cos(TAU * (U - 1.0))) ** 0.8
    return x, y, z + rise * (1 - V) ** 1.15
skirt = robe(c, "coat", CS, nu=34, nv=12, name="coat_skirt", cls="leather", pleats=6, pleat_amp=0.045, thick=0.006, ragged=0.012,
             u0=0.785, u1=1.715, close=False, calf_to_thigh=1.0, mod=hem)
# short standing collar, open at the throat
gcol = g_stations([(1.485, 0, 0.032, 0.128, 0.108, 0.118), (1.53, 0, 0.022, 0.106, 0.100, 0.106), (1.585, 0, 0.015, 0.117, 0.108, 0.117)],
                  nu=20, nv=5, close=False, u0=0.85, u1=1.65)
col = add("collar", gcol, "leather", "coat", solidify=0.006, sharp=180, double_sided=True)
w_func(col, lambda co: {"neck_01": sstep(co[:, 2], 1.50, 1.57) * 0.6, "spine_03": 1 - sstep(co[:, 2], 1.50, 1.57) * 0.6})

# ------------------------------------------------------------------ belt, scroll cases
BZ, BRX, BRY = 1.075, 0.184, 0.144
bcy = body_ring(c, BZ)[1]
belt(c, BZ, "belt", width=0.06, thick=0.009, rx=BRX, ry=BRY, buckle="iron")
tube_a, caps_a = g_scroll(0.21, 0.025)
def seal(zc, r=0.025):
    return [(gxf(g_lathe([(0.0, 0.008), (0.013, 0.006), (0.017, 0.0), (0.0, -0.002)], n=7), T(0, -r - 0.004, zc) @ R('X', 90)), "cloth", "wax", dict(sharp=50)),
            (g_tube(ring_path(r + 0.004, 10, "xy", (0, 0, zc)), 1.0, n=4, closed=True, sx=0.003, sy=0.008), "leather", "belt", dict(sharp=60))]
SA = T(-0.150, -0.158, 0.935) @ R('Z', -33) @ R('X', -10) @ R('Y', 6)
sa = props("scroll_a", SA, [(tube_a, "cloth", "paper", dict(sharp=60)), (caps_a, "metal", "brass", dict(sharp=50))] + seal(0.03) + seal(-0.05), "pelvis")
sa.append(add("scroll_a_strap", g_tube([(-0.143, -0.150, 1.035), (-0.128, -0.128, 1.06), (-0.118, -0.115, 1.085)], 1.0, n=6, sx=0.004, sy=0.010,
                                       up=(0, 1, 0)), "leather", "belt", sharp=60))
for o in sa:
    w_func(o, lambda co: {"pelvis": 0.6, "thigh_r": 0.4})
tube_b, caps_b = g_scroll(0.25, 0.027)
tube_c, caps_c = g_scroll(0.20, 0.023)
by1 = c.B.slice(1.135, 0.02)
by2 = c.B.slice(1.20, 0.02)
SB = T(0.0, by1[1] + by1[4] + 0.021 + 0.029, 1.135) @ R('Y', 82) @ R('Z', 180)
SC = T(0.01, by2[1] + by2[4] + 0.021 + 0.025, 1.197) @ R('Y', 98) @ R('Z', 180)
props("scroll_b", SB, [(tube_b, "cloth", "paper", dict(sharp=60)), (caps_b, "metal", "iron", dict(sharp=50))] + seal(0.0, 0.027), "spine_01")
props("scroll_c", SC, [(tube_c, "cloth", "paper", dict(sharp=60)), (caps_c, "metal", "brass", dict(sharp=50))] + seal(0.02, 0.023), "spine_01")

# ------------------------------------------------------------------ chain bandolier (right shoulder -> left hip)
guide = [(-0.14, 0.03, 1.56), (-0.11, -0.12, 1.47), (-0.04, -0.17, 1.36), (0.05, -0.16, 1.24), (0.13, -0.13, 1.14), (0.19, -0.02, 1.10),
         (0.14, 0.12, 1.14), (0.05, 0.17, 1.26), (-0.05, 0.19, 1.38), (-0.12, 0.14, 1.49)]
gp_ = resample(guide + [guide[0]], 0.035)[:-1]
CP = wrap_path(c, gp_, 0.033)
for _ in range(3):
    CP = (np.roll(CP, 1, 0) + CP * 2 + np.roll(CP, -1, 0)) / 4
bando = add("bandolier", g_chain(list(CP) + [CP[0]], link=0.036, wire=0.0048), "metal", "iron", sharp=60)
w_transfer(bando, c.ref, smooth=2, only=["spine_01", "spine_02", "spine_03", "clavicle_r", "clavicle_l", "neck_01", "pelvis"])

# ------------------------------------------------------------------ right shoulder: layered leather pauldron with iron rim
gpl = g_pauldron(0.150, 0.150, 0.085, th_out=100, th_in=50, th_fb=84, nu=16)
MR = T(-0.224, 0.062, 1.484) @ R('Y', -20) @ S(-1, 1, 1)
p1 = add("pauldron", gxf(gpl, MR), "leather", "boot", solidify=0.007, sharp=40)
weld(p1)
p2 = add("pauldron_rim", g_tube(xf(rim_of(gpl, 16), MR), 0.0075, n=6, closed=True), "metal", "iron", sharp=60)
studs = [gxf(g_sphere(0.011, 6, 4, sz=0.7), MR @ T(0.07 * math.cos(a), 0.085 * math.sin(a), 0.079)) for a in np.radians((-60, 0, 60))]
p3 = add("pauldron_studs", gjoin(*studs), "metal", "iron", sharp=60)
for o in (p1, p2, p3):
    w_func(o, lambda co: {"clavicle_r": 0.62, "upperarm_r": 0.38})

# ------------------------------------------------------------------ charred left arm with brands, chain and shackles
mch = (B.x > 0.238) & (B.w(*BONES_ARM, *BONES_HAND) > 0.5)
char = shell(B, mch, 0.004, "char_arm", "leather", "char", smooth=0, sharp=180)
hide(c, mch & (B.x > 0.262))
def arm_ring(x, r_guide=0.11, n=14):
    return [(x, 0.072 + r_guide * math.cos(TAU * i / n), 1.432 + r_guide * math.sin(TAU * i / n)) for i in range(n)]
# glowing brand rings at the elbow and the wrist, sigils along the top and front of the arm
for i, x in enumerate((0.478, 0.728)):
    ro = add("brand_ring_%d" % i, g_tube(wrap_path(c, arm_ring(x), 0.0065), 0.0042, n=4, closed=True), "emis", "pact", sharp=180)
    w_transfer(ro, char, smooth=0)
top_marks = [(0.300, chevron(0.016, 0.014, 0.007)), (0.345, bar(0.017, 0.0045)), (0.390, hook(0.014, 0.014, 0.006)),
             (0.435, rhomb(0.009, 0.016)), (0.800, fork(0.016, 0.014, 0.0055))]
for i, (x, shp) in enumerate(top_marks):
    decal("brand_t%d" % i, shp, char, (x, 0.075, 1.75), "emis", "pact", up=(1, 0, 0), ray=(0, 0, -1), depth=0.004, lift=0.003)
front_marks = [(0.325, fork(0.015, 0.014, 0.0055)), (0.375, chevron(0.015, 0.013, 0.0065)), (0.425, bar(0.015, 0.0045)),
               (0.560, rhomb(0.009, 0.017)), (0.640, hook(0.012, 0.012, 0.0055))]
for i, (x, shp) in enumerate(front_marks):
    decal("brand_f%d" % i, shp, char, (x, -0.35, 1.432), "emis", "pact", up=(1, 0, 0), ray=(0, 1, 0), depth=0.004, lift=0.003)
# chain wound round the forearm, iron shackles at the wrist and the upper arm
hel = []
for t in np.linspace(0, 1, 34):
    a = TAU * 2.25 * t + 1.0
    hel.append((0.505 + 0.185 * t, 0.072 + 0.10 * math.cos(a), 1.432 + 0.10 * math.sin(a)))
add("arm_chain", g_chain(wrap_path(c, hel, 0.013), link=0.032, wire=0.0042), "metal", "iron", bone="lowerarm_l", sharp=60)
def shackle(r, w=0.03, t=0.008, n=12, sx=1.0):
    return g_lathe([(r, -w / 2), (r + t, -w / 2), (r + t, w / 2), (r, w / 2), (r, -w / 2)], n=n, sx=sx)
add("shackle_wrist", gxf(shackle(0.0375, 0.032), frame((0.703, 0.0705, 1.434), (1, 0, 0), (0, 1, 0))), "metal", "iron", bone="lowerarm_l", sharp=40)
add("shackle_upper", gxf(shackle(0.0815, 0.036, 0.008, 12, 0.91), frame((0.292, 0.0785, 1.422), (1, 0, 0), (0, 1, 0))), "metal", "iron",
    bone="upperarm_l", sharp=40)

# ------------------------------------------------------------------ head: hair, iron circlet with one whole and one broken horn-prong
add_hair(c, "Hair_SimpleParted")
bp = [(0.135 * math.sin(a), -0.02 - 0.145 * math.cos(a), 1.752 + 0.014 * abs(math.sin(a))) for a in np.radians(np.linspace(-102, 102, 17))]
BPW = wrap_path(c, bp, 0.007)
add("circlet", g_tube(BPW, 0.0085, n=6, sx=1.35, sy=0.6, up=(0, 0, 1)), "metal", "iron", bone="Head", sharp=50)
fc = BPW[8]
add("circlet_plate", gxf(g_prism(rhomb(0.017, 0.026), 0.006), frame(fc + np.array((0, -0.004, 0.004)), (0, 0, 1), (0, -1, 0))), "metal", "darksteel",
    bone="Head", bevel=0.002, sharp=40)
add("circlet_gem", gxf(g_gem(0.0095, 0.016, 4, 0.6), frame(fc + np.array((0, -0.008, 0.004)), (0, -1, 0.1), (0, 0, 1))), "emis", "pact", bone="Head", sharp=20)
hl, hr = BPW[13], BPW[3]
add("prong_l", gxf(g_tooth(0.165, 0.019, 0.07, 6), T(*(hl + np.array((0, 0, 0.004)))) @ R('Y', 20) @ R('Z', 90)), "metal", "iron", bone="Head", sharp=40)
stump = g_lathe([(0.019, 0.0), (0.017, 0.03), (0.0145, 0.052), (0.009, 0.060), (0.0, 0.050)], n=6)
add("prong_r", gxf(stump, T(*(hr + np.array((0, 0, 0.004)))) @ R('Y', -20) @ R('X', -14)), "metal", "iron", bone="Head", sharp=30)

# ------------------------------------------------------------------ wands (right hand; weapon local: +Z towards the tip)
GW = grip("r", tilt=8.0) @ S(1.28)
# tier 1: charred crooked stick, cord grip, iron nail
wpn("wpn_1", GW, [
    (tube_w(gnarl(zline(-0.10, 0.27, 7), 0.007, 3), list(np.linspace(0.0135, 0.0065, 7)), 6), "leather", "wand", dict(sharp=60)),
    (tube_w([(0, 0, -0.055), (0, 0, 0.045)], 0.0152, 6), "leather", "belt", dict(sharp=60)),
    (g_lathe([(0.0075, 0.262), (0.0085, 0.272), (0.004, 0.30), (0.0, 0.325)], n=5), "metal", "iron", dict(sharp=40)),
])
# tier 2: iron-bound dark steel wand, garnet in a three-claw setting
claws2 = [gxf(g_tooth(0.042, 0.0048, 0.012, 4), T(0.011 * math.cos(a), 0.011 * math.sin(a), 0.262) @ R('Z', math.degrees(a) + 180)) for a in (0.0, TAU / 3, 2 * TAU / 3)]
wpn("wpn_2", GW, [
    (g_lathe([(0.0, -0.118), (0.014, -0.106), (0.016, -0.090), (0.011, -0.074), (0.0125, -0.05), (0.0135, 0.0), (0.0125, 0.05), (0.020, 0.058),
              (0.020, 0.066), (0.0105, 0.076), (0.0085, 0.20), (0.0105, 0.262), (0.0, 0.266)], n=8), "metal", "darksteel", dict(sharp=40)),
    (band(0.062, 0.0205, 0.012, 8, 1.06), "metal", "brass", dict(sharp=50)), (band(0.14, 0.0098, 0.010, 8), "metal", "brass", dict(sharp=50)),
    (band(-0.082, 0.0142, 0.010, 8), "metal", "brass", dict(sharp=50)),
    (gjoin(*claws2), "metal", "iron", dict(sharp=40)),
    (gxf(g_gem(0.0125, 0.042, 6, 0.6), T(0, 0, 0.286)), "metal", "garnet", dict(sharp=20)),
])
# tier 3: twisted horn wand, floating ember shard held by iron claws
strands = []
for ph in (0.0, math.pi):
    pts = [(0.0062 * math.cos(TAU * 2.2 * t + ph) * (1 - 0.5 * t), 0.0062 * math.sin(TAU * 2.2 * t + ph) * (1 - 0.5 * t), 0.058 + 0.222 * t)
           for t in np.linspace(0, 1, 14)]
    strands.append(tube_w(pts, list(np.linspace(0.0105, 0.0052, 14)), 6))
claws3 = [gxf(g_tooth(0.085, 0.0058, 0.026, 5), T(0.008 * math.cos(a), 0.008 * math.sin(a), 0.262) @ R('Z', math.degrees(a)) @ R('Y', 24) @ R('Z', 180))
          for a in (0.3, 0.3 + TAU / 3, 0.3 + 2 * TAU / 3)]
wpn("wpn_3", GW, [
    (g_lathe([(0.0, -0.125), (0.010, -0.115), (0.017, -0.098), (0.011, -0.078), (0.0125, -0.05), (0.014, 0.0), (0.0125, 0.048), (0.022, 0.056),
              (0.022, 0.064), (0.012, 0.074)], n=8), "metal", "darksteel", dict(sharp=40)),
    (gjoin(*strands), "leather", "horn", dict(sharp=60)),
    (band(0.060, 0.0225, 0.010, 8, 1.06), "emis", "pact", dict(sharp=60)),
    (band(-0.088, 0.0152, 0.010, 8), "metal", "brass", dict(sharp=50)),
    (band(0.268, 0.0085, 0.014, 6, 1.3), "metal", "brass", dict(sharp=50)),
    (gjoin(*claws3), "metal", "iron", dict(sharp=40)),
    (gxf(g_gem(0.017, 0.075, 5, 0.6), T(0, 0, 0.335)), "emis", "pact", dict(sharp=20)),
])

# ------------------------------------------------------------------ chained grimoires (left hand): carried by an iron bail on the spine
GB = grip("l")
X0 = 0.050
def book(owner, w, h, t, cover_src, loops, extra):
    cover, pages = g_book(w, h, t)
    MB = T(X0 + w / 2, 0, 0)
    parts = [
        (gxf(cover, MB), "leather", cover_src, dict(bevel=0.003, sharp=40)),
        (gxf(pages, MB), "cloth", "paper", dict(sharp=40)),
        (g_tube([(X0 + 0.004, 0, -0.085), (0.018, 0, -0.080), (0.0, 0, -0.048), (0.0, 0, 0.048), (0.018, 0, 0.080), (X0 + 0.004, 0, 0.085)],
                0.0078, n=6), "metal", "iron", dict(sharp=60)),
        (gjoin(g_box(0.014, t + 0.008, 0.03, (X0 + 0.004, 0, -0.085)), g_box(0.014, t + 0.008, 0.03, (X0 + 0.004, 0, 0.085))), "metal", "iron",
         dict(bevel=0.003, sharp=40)),
    ]
    cs = []
    for sy_ in (1, -1):
        yy = sy_ * (t / 2 + 0.0015)
        for (cx, cz, ry) in ((X0 + w, -h / 2, -90), (X0 + w, h / 2, 180), (X0, -h / 2, 0), (X0, h / 2, 90)):
            cs.append(gxf(g_corner(0.036), T(cx, yy, cz) @ R('Y', ry)))
    parts.append((gjoin(*cs), "metal", "iron", dict(sharp=30)))
    m = 0.009
    for kind, pos in loops:
        if kind == "h":      # loop round the width at height pos
            path = [(X0 - m, -t / 2 - m, pos), (X0 + w + m, -t / 2 - m, pos), (X0 + w + m, t / 2 + m, pos), (X0 - m, t / 2 + m, pos), (X0 - m, -t / 2 - m, pos)]
        else:                # loop round the height at x = pos
            path = [(pos, -t / 2 - m, -h / 2 - m), (pos, -t / 2 - m, h / 2 + m), (pos, t / 2 + m, h / 2 + m), (pos, t / 2 + m, -h / 2 - m),
                    (pos, -t / 2 - m, -h / 2 - m)]
        parts.append((g_chain(path, link=0.048, wire=0.0052, up=(0.3, 1, 0.2)), "metal", "iron", dict(sharp=60)))
    parts += extra(w, h, t)
    return wpn(owner, GB, parts, bone="hand_l")

book("wpn_1_off", 0.165, 0.235, 0.050, "tome", [("h", 0.0)], lambda w, h, t: [])
def extra2(w, h, t):
    out = [(g_box(0.014, t + 0.014, 0.055, (X0 + w + 0.003, 0, 0)), "metal", "iron", dict(bevel=0.003, sharp=40)),
           (g_box(0.006, 0.02, 0.028, (X0 + w + 0.011, 0, 0)), "metal", "brass", dict(bevel=0.002, sharp=40))]
    for sy_ in (1, -1):
        Mc = T(X0 + w * 0.5, sy_ * (t / 2 + 0.002), 0) @ R('X', 90)
        out.append((gxf(g_lathe([(0.030, -0.003), (0.046, -0.003), (0.046, 0.003), (0.030, 0.003), (0.030, -0.003)], n=12), Mc), "metal", "brass", dict(sharp=40)))
        out.append((gxf(g_prism(rhomb(0.014, 0.024), 0.006), T(X0 + w * 0.5, sy_ * (t / 2 + 0.002), 0)), "metal", "garnet", dict(sharp=20)))
    return out
book("wpn_2_off", 0.175, 0.250, 0.056, "tome", [("h", 0.085), ("h", -0.085)], extra2)
def extra3(w, h, t):
    out = [(g_box(0.0045, 0.010, h - 0.03, (X0 + w - 0.0035, 0, 0)), "emis", "pact", dict(sharp=40)),
           (g_box(0.016, t + 0.016, 0.06, (X0 + w + 0.003, 0, 0)), "metal", "darksteel", dict(bevel=0.003, sharp=40))]
    for sz_ in (1, -1):
        out.append((gxf(g_tooth(0.085, 0.012, 0.035, 5), T(X0 + w + 0.006, 0, sz_ * 0.022) @ R('Y', 90 - 28 * sz_) @ R('Z', 90 + 90 * sz_)),
                    "leather", "horn", dict(sharp=40)))
    for sy_ in (1, -1):
        yy = sy_ * (t / 2 + 0.003)
        cx = X0 + w * 0.5
        out.append((gxf(g_tube(ring_path(0.052, 16, "xz"), 0.0045, n=4, closed=True), T(cx, yy, 0)), "emis", "pact", dict(sharp=60)))
        out.append((gxf(g_prism(chevron(0.026, 0.022, 0.010), 0.006), T(cx, yy, -0.004)), "emis", "pact", dict(sharp=20)))
        out.append((gxf(g_prism(rhomb(0.008, 0.013), 0.006), T(cx, yy, 0.028)), "emis", "pact", dict(sharp=20)))
    return out
book("wpn_3_off", 0.185, 0.265, 0.062, "tome_dark", [("h", 0.095), ("h", -0.095), ("v", X0 + 0.150)], extra3)

finalize(c, CID, hair_tint=(0.045, 0.035, 0.04), skin=dict(gain=0.92, sat=0.7),
         tex={"cloth": 1024, "leather": 1024, "metal": 1024}, decimate={"cloth": 0.85},
         notes="Pose-tested: Spell_Simple_Shoot, Spell_Simple_Idle_Loop, Interact, Idle_Loop, Jog_Fwd_Loop. wpn_N = wand, skinned 100% to "
               "hand_r; wpn_N_off = chained grimoire, skinned 100% to hand_l and carried by an iron bail on its spine (hangs below the "
               "fist in idle, is thrust forward with the casting hand in the Spell_Simple clips). The coat hem is long on the left "
               "and weighted to pelvis / thighs only.")
