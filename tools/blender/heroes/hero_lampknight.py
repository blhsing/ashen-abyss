"""hero_lampknight -- sword-and-shield warden of the beacons: open-faced barbute with a gilt-edged Y opening, broad
gorget, ivory tabard (two belted panels hung from shoulder straps) over a keeled cuirass and a mail skirt, layered
spaulders, couters, vambraces, poleyns and greaves, an empty scabbard on the left hip and a small caged beacon lantern
carried on an iron crook behind the left shoulder. Arming sword in the right hand (wpn_N) + shield strapped to the left
forearm and gripped in the left fist (wpn_N_off)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_twn import *

CID = "hero_lampknight"
c = start("female")
body_bulk(c, spec={"upperarm_l": 1.06, "upperarm_r": 1.06}, torso=[(0.90, 1.0, 1.0), (1.10, 1.06, 1.04), (1.40, 1.05, 1.0), (1.52, 1.0, 1.0)])
B = c.B

# ------------------------------------------------------------------ source materials
mats.metal("steel", base=(0.34, 0.335, 0.325), rough=(0.26, 0.54), rust_amt=0.12, pattern="hammered", soot=0.08, ash=0.15)
mats.metal("mail", base=(0.135, 0.135, 0.14), rough=(0.45, 0.7), pattern="mail", edge_amt=0.0, rust_amt=0.10, bump=0.5)
mats.metal("gilt", base=(0.47, 0.305, 0.085), rough=(0.24, 0.48), rust=(0.05, 0.07, 0.05), rust_amt=0.12, ash=0.05, scan="gold")
mats.metal("iron", base=(0.17, 0.165, 0.16), rough=(0.45, 0.72), rust_amt=0.35, ash=0.0, pattern="hammered")
mats.metal("blade", base=(0.45, 0.45, 0.46), rough=(0.2, 0.42), rust_amt=0.06, ash=0.0, pattern="brushed", edge_amt=1.0)
mats.metal("blade_pale", base=(0.56, 0.54, 0.50), rough=(0.16, 0.36), rust_amt=0.02, ash=0.0, pattern="brushed", edge=(0.9, 0.8, 0.6), edge_amt=1.0)
mats.cloth("tabard", base=(0.43, 0.375, 0.28), bands=[(0.035, 0.085, (0.40, 0.25, 0.06))], folds=0.7, ash=0.45, ash_h=0.5, worn=0.5)
mats.cloth("tabard_top", base=(0.43, 0.375, 0.28), folds=0.3, ash=0.15, worn=0.45)
mats.cloth("emblem", base=(0.44, 0.28, 0.065), folds=0.0, ash=0.1, var=0.2)
mats.cloth("hose", base=(0.075, 0.062, 0.05), folds=0.5, ash=0.4, ash_h=0.4)
mats.flat("wax", (0.55, 0.50, 0.38), rough=0.5)
mats.leather("boot", base=(0.05, 0.032, 0.02), ash=0.6, ash_h=0.3)
mats.leather("strap", base=(0.085, 0.05, 0.026), worn=0.7)
mats.leather("glove", base=(0.07, 0.045, 0.028), worn=0.6)
mats.leather("scabbard", base=(0.10, 0.045, 0.03), worn=0.5)
mats.wood("shieldwood", base=(0.115, 0.075, 0.045), luv_grain=False)
mats.emis("flame", (1.0, 0.70, 0.20), strength=5.0)

# ------------------------------------------------------------------ under layers: mail, padded hose, boots, gloves
m_mail = (~m_head(B)) & (B.z < 1.50) & (B.z > 1.0) & (np.abs(B.x) < 0.53) & (B.w(*BONES_HAND) < 0.3) & ~((np.abs(B.x) < 0.10) & (B.z > 1.26))
shell(B, m_mail, 0.008, "mail", "metal", "mail", smooth=2, sharp=180)
ho = trousers(c, "hose", top=0.76, bottom=0.40, offset=0.009, smooth=3)
wrinkle_shell(ho, 0.0035, 26.0, "z", 3, mask=lambda co: sstep(co[:, 2], 0.74, 0.68) * (0.4 + 0.6 * np.exp(-((co[:, 2] - 0.56) / 0.10) ** 2)))
toe_box(boots(c, "steel", top=0.17, offset=0.017, cls="metal", name="sabatons", smooth=2, toe_cap=False), height=0.050, length=0.110, grow=0.012)
gloves(c, "glove", cuff=0.715, offset=0.005, name="gloves", smooth=1)
face = m_head(B) & (B.y < -0.040) & (B.z > 1.585) & (B.z < 1.735) & (np.abs(B.x) < 0.064)
m_coif = (B.w("Head", "neck_01") > 0.5) & ~face & (B.y < -0.018) & (B.z > 1.52) & (B.z < 1.76)
shell(B, m_coif, 0.007, "coif", "metal", "mail", smooth=2, sharp=180)
hide(c, ~B.grow(face, 2))

# ------------------------------------------------------------------ cuirass, plackart, fauld lames, gorget
def ridge(amount, width=0.035):
    def mod(U, V, x, y, z):
        return x, y - amount * np.exp(-((U - 0.75) / width) ** 2), z
    return mod

CUI = [(1.185, 0, -0.015, 0.142, 0.120, 0.112), (1.25, 0, -0.012, 0.162, 0.142, 0.127), (1.32, 0, -0.008, 0.187, 0.160, 0.147),
       (1.39, 0, 0.0, 0.188, 0.152, 0.152), (1.45, 0, 0.012, 0.152, 0.117, 0.142), (1.495, 0, 0.018, 0.112, 0.092, 0.108),
       (1.52, 0, 0.02, 0.086, 0.078, 0.092)]
cuirass = add("cuirass", g_stations(CUI, nu=28, nv=10, p=2.3, mod=ridge(0.012)), "metal", "steel", solidify=0.006, bone="spine_03", sharp=50, bevel=0.002)
PLA = [(1.055, 0, 0.0, 0.160, 0.120, 0.127), (1.13, 0, -0.008, 0.146, 0.114, 0.111), (1.215, 0, -0.014, 0.155, 0.134, 0.121)]
add("plackart", g_stations(PLA, nu=28, nv=4, p=2.3, mod=ridge(0.008)), "metal", "steel", solidify=0.006, bone="spine_02", sharp=50, bevel=0.002)
F1 = [(0.975, 0, 0.02, 0.204, 0.166, 0.176), (1.058, 0, 0.006, 0.172, 0.136, 0.140)]
F2 = [(0.905, 0, 0.022, 0.216, 0.190, 0.192), (1.0, 0, 0.022, 0.197, 0.160, 0.171)]
add("fauld1", g_stations(F1, nu=28, nv=3, p=2.3), "metal", "steel", solidify=0.006, bone="spine_01", sharp=50, bevel=0.002)
add("fauld2", g_stations(F2, nu=28, nv=3, p=2.3), "metal", "steel", solidify=0.006, bone="pelvis", sharp=50, bevel=0.002)
GOR = [(1.43, 0, 0.014, 0.184, 0.152, 0.168), (1.47, 0, 0.016, 0.150, 0.126, 0.142), (1.505, 0, 0.016, 0.104, 0.100, 0.110), (1.548, 0, 0.010, 0.084, 0.098, 0.100)]
gor = add("gorget", g_stations(GOR, nu=22, nv=7), "metal", "steel", solidify=0.006, sharp=50, bevel=0.002)
w_func(gor, lambda co: {"neck_01": sstep(co[:, 2], 1.49, 1.54), "spine_03": 1 - sstep(co[:, 2], 1.49, 1.54)})
gg = g_stations(GOR, nu=22, nv=7)
gt = add("gorget_trim", g_tube(rim_of(gg, 22, 0), 0.006, n=5, closed=True), "metal", "gilt", bone="spine_03", sharp=60)

# ------------------------------------------------------------------ mail skirt under the fauld
SK = [(0.66, 0, 0.004, 0.220, 0.168, 0.164), (0.85, 0, 0.014, 0.210, 0.182, 0.180), (1.0, 0, 0.022, 0.186, 0.150, 0.162), (1.07, 0, 0.006, 0.160, 0.120, 0.128)]
robe(c, "mail", SK, nu=32, nv=7, name="mail_skirt", cls="metal", thick=0.004, calf_to_thigh=0.0,
     mod=fold_mod([(6, 0.030, 0.15), (11, 0.012, 0.6)], cy=0.015, hem_pow=1.0, top=0.0))

# ------------------------------------------------------------------ tabard: chest + back panels on shoulder straps, belted, split skirt
TOR = [(1.085, 0, 0.0, 0.176, 0.132, 0.142), (1.13, 0, -0.008, 0.152, 0.120, 0.117), (1.215, 0, -0.014, 0.160, 0.140, 0.127),
       (1.25, 0, -0.012, 0.166, 0.148, 0.133), (1.32, 0, -0.008, 0.190, 0.172, 0.153), (1.39, 0, 0.0, 0.191, 0.164, 0.158),
       (1.45, 0, 0.012, 0.155, 0.127, 0.148)]
SKO = [(0.44, 0, 0.004, 0.232, 0.178, 0.174), (0.66, 0, 0.004, 0.226, 0.178, 0.170), (0.905, 0, 0.022, 0.220, 0.196, 0.196),
       (1.0, 0, 0.022, 0.202, 0.168, 0.178), (1.085, 0, 0.0, 0.178, 0.138, 0.144)]
tw = lambda co: {"spine_03": sstep(co[:, 2], 1.20, 1.27), "spine_02": (1 - sstep(co[:, 2], 1.20, 1.27)) * sstep(co[:, 2], 1.07, 1.12),
                 "spine_01": 1 - sstep(co[:, 2], 1.07, 1.12)}
chest = drape("tabard_chest", -0.112, 0.112, 1.425, 1.07, front_y(TOR, 0.012), "cloth", "tabard_top", nu=11, nv=9, thick=0.005,
              folds=((2.0, 0.0035, 0.1),), top=0.5, hem_pow=1.0)
w_func(chest, tw)
backp = drape("tabard_back", -0.112, 0.112, 1.44, 1.07, back_y(TOR, 0.012), "cloth", "tabard_top", nu=11, nv=9, thick=0.005, out=1.0,
              folds=((2.0, 0.004, 0.4),), top=0.5, hem_pow=1.0)
w_func(backp, tw)
apply_mods(cuirass)
for k, ys in enumerate((-0.30, 0.30)):
    st, _, _ = ribbon(c, [(0.080, ys, 1.37), (0.080, ys, 1.425), (0.080, ys, 1.478)], "tabard_top", width=0.060, thick=0.005, offset=0.010,
                      name="tabard_strap%d_l" % k, cls="cloth", closed=False, step=0.02, host=cuirass, only=["spine_03"])
    mirrored(st)

def skirt_w(co):
    z, x = co[:, 2], co[:, 0]
    t = sstep(1.03 - z, 0.0, 0.30) * 0.86
    l = sstep(x, -0.05, 0.05)
    return {"pelvis": 1 - t, "thigh_l": t * l, "thigh_r": t * (1 - l)}
fy = front_y(SKO, 0.016)
fl = drape("tabard_fl", 0.002, 0.152, 1.085, 0.50, fy, "cloth", "tabard", nu=11, nv=12, thick=0.005, flare=0.22,
           folds=((1.4, 0.015, 0.15), (3.1, 0.006, 0.6)), zmod=lambda U, V, z: z - 0.07 * (1 - U) * (1 - V) ** 2)
fr = drape("tabard_fr", -0.152, 0.010, 1.085, 0.50, lambda x, z: fy(x, z) + 0.007, "cloth", "tabard", nu=11, nv=12, thick=0.005, flare=0.22,
           folds=((1.6, 0.014, 0.55), (2.7, 0.006, 0.1)), zmod=lambda U, V, z: z - 0.07 * U * (1 - V) ** 2)
bk = drape("tabard_bk", -0.165, 0.165, 1.085, 0.47, back_y(SKO, 0.016), "cloth", "tabard", nu=17, nv=12, thick=0.005, out=1.0, flare=0.20,
           folds=((2.5, 0.016, 0.1), (5.3, 0.006, 0.45)), zmod=lambda U, V, z: z + 0.09 * (1 - np.abs(2 * U - 1)) ** 1.5 * (1 - V) ** 2)
for o in (fl, fr, bk):
    w_func(o, skirt_w)

def lamp_sigil(host, zc, ray, name, s=1.0):
    """lantern glyph: roof chevron over a flame lozenge on a base bar, two ray ticks"""
    roof = [(-0.034 * s, -0.016 * s), (0.0, 0.018 * s), (0.034 * s, -0.016 * s), (0.024 * s, -0.016 * s), (0.0, 0.006 * s), (-0.024 * s, -0.016 * s)]
    y0 = -0.5 if ray[1] > 0 else 0.5
    decal(name + "_roof", roof, host, (0, y0, zc + 0.052 * s), "cloth", "emblem", ray=ray, depth=0.004, lift=0.002)
    decal(name + "_flame", rhomb(0.017 * s, 0.032 * s), host, (0, y0, zc), "cloth", "emblem", ray=ray, depth=0.004, lift=0.002)
    decal(name + "_base", bar(0.030 * s, 0.006 * s), host, (0, y0, zc - 0.048 * s), "cloth", "emblem", ray=ray, depth=0.004, lift=0.002)
    for sx in (-1, 1):
        decal(name + "_ray%d" % sx, rhomb(0.006 * s, 0.014 * s), host, (sx * 0.040 * s, y0, zc), "cloth", "emblem", ray=ray, depth=0.004, lift=0.002)
lamp_sigil(chest, 1.265, (0, 1, 0), "sigil_f")
lamp_sigil(backp, 1.275, (0, -1, 0), "sigil_b", 1.15)

BZ = 1.085
belt(c, BZ, "strap", width=0.05, thick=0.009, rx=0.165, ry=0.158, buckle="gilt", bone="spine_01")
bcy = body_ring(c, BZ)[1]

# empty scabbard on the left hip, hung from the belt by two straps
SC0, SC1 = np.array((0.212, bcy - 0.02, 1.03)), np.array((0.275, bcy + 0.235, 0.40))
sd = (SC1 - SC0) / np.linalg.norm(SC1 - SC0)
SCM = frame(SC0, sd, (1, 0, 0))
props("scabbard", SCM, [
    (g_tube([(0, 0, -0.01), (0, 0, 0.30), (0, 0, 0.60), (0, 0, 0.675)], [1.0, 0.95, 0.72, 0.45], n=8, sx=0.031, sy=0.0125), "leather", "scabbard", dict(sharp=50)),
    (g_tube([(0, 0, -0.016), (0, 0, 0.045)], [1.08, 1.04], n=8, sx=0.031, sy=0.0125), "metal", "gilt", dict(sharp=50)),
    (g_tube([(0, 0, 0.60), (0, 0, 0.675), (0, 0, 0.70)], [0.80, 0.52, 0.2], n=8, sx=0.031, sy=0.0125), "metal", "gilt", dict(sharp=50)),
    (g_tube([(0, 0, 0.17), (0, 0, 0.195)], [1.0, 0.98], n=8, sx=0.033, sy=0.0145), "metal", "gilt", dict(sharp=50)),
], "pelvis")
for k, zz in enumerate((0.02, 0.18)):
    p = SC0 + sd * zz
    add("scab_strap_%d" % k, g_tube([p + np.array((0, 0, 0.0)), (0.19 - 0.02 * k, bcy - 0.03 + 0.07 * k, 1.06), (0.176 - 0.02 * k, bcy - 0.03 + 0.075 * k, 1.085)],
                                    1.0, n=6, sx=0.004, sy=0.010, up=(1, 0, 0)), "leather", "strap", bone="pelvis", sharp=60)

# ------------------------------------------------------------------ helm: barbute, gilt edge, low comb
gh = g_barbute(eye_deg=42.0)
helm = add("helm", gh, "metal", "steel", bone="Head", sharp=50, solidify=0.005)
weld(helm)
rows = grid_rows(gh, 30)
zt = 1.70 + 0.145
jb = max(j for j in range(rows.shape[0]) if 1.555 + (zt - 1.555) * j / (rows.shape[0] - 1) < 1.728 + 0.008) + 1
edge = [rows[j, 0] for j in range(jb)] + [rows[j, -1] for j in reversed(range(jb))] + [rows[0, i] for i in range(28, 0, -1)]
pts = [edge[0]]
for p in edge[1:]:
    if np.linalg.norm(p - pts[-1]) > 0.004:
        pts.append(p)
cen = np.array((0.0, -0.012, 0.0))
pts = [p + (p - cen) * np.array((1, 1, 0)) / max(np.linalg.norm((p - cen)[:2]), 1e-6) * 0.003 for p in pts]
add("helm_edge", g_tube(pts, 0.0062, n=5, closed=True), "metal", "gilt", bone="Head", sharp=60)
rv = []
for i in list(range(4, 27, 2)):
    q = rows[1, i]
    d = (q - cen) * np.array((1, 1, 0))
    d /= np.linalg.norm(d)
    rv.append(gxf(g_lathe([(0.0, 0.0105), (0.0045, 0.009), (0.0062, 0.005)], n=5), frame(q + np.array((0, 0, 0.004)), d)))
add("helm_rivets", gjoin(*rv), "metal", "gilt", bone="Head", sharp=60)
def helm_pt(a, z, lift=0.004):
    k = math.sqrt(max(0.0, 1 - ((z - 1.70) / 0.145) ** 2)) if z > 1.70 else 1.0
    ry = 0.127 if math.cos(a) > 0 else 0.124
    return (0.0, -0.012 - (ry * k + lift) * math.cos(a), z + lift * (1 - k))
comb = [helm_pt(0.0, z) for z in (1.742, 1.775, 1.805, 1.828, 1.843)] + [helm_pt(math.pi, z) for z in (1.843, 1.826, 1.79, 1.74, 1.68)]
add("helm_comb", g_tube(comb, [0.5, 0.9, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0.9, 0.5], n=6, sx=0.010, sy=0.0055, up=(0, -1, 0.2)), "metal", "gilt", bone="Head", sharp=50)

# ------------------------------------------------------------------ shoulders and arms (left, mirrored)
SH = np.array((0.212, 0.066, 1.474))
Mp = T(*SH) @ R('Y', 18)
gp = g_pauldron(0.122, 0.118, 0.070, th_out=98, th_in=50, th_fb=84, nu=16)
p1 = add("spaulder_l", gxf(gp, Mp), "metal", "steel", solidify=0.006, sharp=40, bevel=0.002)
weld(p1)
p2 = add("spaulder_trim_l", g_tube(xf(rim_of(gp, 16), Mp), 0.0065, n=5, closed=True), "metal", "gilt", sharp=60)
for o in (p1, p2):
    w_func(o, lambda co: {"clavicle_l": 0.58, "upperarm_l": 0.42})
    mirrored(o)
l1 = add("arm_lame1_l", arm_lame(0.272, 0.368, 0.077, 0.070, arc=0.72, n=11), "metal", "steel", solidify=0.005, bone="upperarm_l", sharp=50, bevel=0.002)
mirrored(l1)
l2 = add("arm_lame2_l", arm_lame(0.346, 0.432, 0.071, 0.065, arc=0.70, n=11), "metal", "steel", solidify=0.005, bone="upperarm_l", sharp=50, bevel=0.002)
mirrored(l2)
cout = add("couter_l", gxf(g_sphere(1.0, 10, 6), T(0.470, 0.080, 1.436) @ S(0.050, 0.058, 0.054)), "metal", "steel", sharp=60)
w_func(cout, lambda co: {"upperarm_l": 0.5, "lowerarm_l": 0.5})
mirrored(cout)
cw = add("couter_wing_l", ydome([(0.0, 0.012), (0.020, 0.010), (0.036, 0.0), (0.036, -0.006), (0.0, -0.006)], n=8, x0=0.470, y0=0.136, z0=1.436), "metal", "gilt", sharp=50)
w_func(cw, lambda co: {"upperarm_l": 0.5, "lowerarm_l": 0.5})
mirrored(cw)
L = B.x > 0
vamb, _ = limb_plate(c, "vambrace_l", L & (B.w("lowerarm") > 0.5) & (B.x > 0.505) & (B.x < 0.700), "lowerarm_l", 0.017, 4)
cuff = add("cuff_l", gxf(g_lathe([(0.064, 0.0), (0.054, 0.040), (0.046, 0.078)], n=12), frame((0.640, 0.068, 1.437), (1, 0, 0))), "metal", "steel",
           solidify=0.005, bone="lowerarm_l", sharp=50, bevel=0.0015)
mirrored(cuff)
ct = add("cuff_trim_l", gxf(g_lathe([(0.067, -0.004), (0.070, 0.004), (0.066, 0.012)], n=12), frame((0.640, 0.068, 1.437), (1, 0, 0))), "metal", "gilt",
         bone="lowerarm_l", sharp=60)
mirrored(ct)

# ------------------------------------------------------------------ legs: poleyns with lames + wings, greaves
pol = add("poleyn_l", gxf(g_sphere(1.0, 10, 6), T(0.084, -0.020, 0.536) @ S(0.062, 0.056, 0.072)), "metal", "steel", sharp=60)
w_func(pol, lambda co: {"thigh_l": 0.45, "calf_l": 0.55})
mirrored(pol)
pla = add("poleyn_lame_a_l", leg_lame(0.585, 0.655, 0.074, 0.082, cx=0.100, cy=-0.004, arc=0.50, n=9, sy=1.42), "metal", "steel", solidify=0.005, bone="thigh_l",
          sharp=50, bevel=0.002)
mirrored(pla)
plb = add("poleyn_lame_b_l", leg_lame(0.440, 0.492, 0.070, 0.066, cx=0.086, cy=0.008, arc=0.50, n=9), "metal", "steel", solidify=0.005, bone="calf_l",
          sharp=50, bevel=0.002)
mirrored(plb)
wing = add("poleyn_wing_l", gxf(g_prism([(0.0, -0.045), (0.050, -0.028), (0.058, 0.018), (0.0, 0.046)], 0.007), T(0.146, -0.012, 0.536) @ R('Z', 90)),
           "metal", "gilt", bevel=0.002, sharp=40)
w_func(wing, lambda co: {"thigh_l": 0.45, "calf_l": 0.55})
mirrored(wing)
grv, _ = limb_plate(c, "greave_l", L & (B.w("calf") > 0.5) & (B.z > 0.135) & (B.z < 0.475), "calf_l", 0.017, 4)

# ------------------------------------------------------------------ beacon lantern on an iron crook behind the left shoulder
crook = [(0.118, 0.150, 1.26), (0.122, 0.172, 1.40), (0.134, 0.198, 1.60), (0.152, 0.216, 1.80), (0.176, 0.222, 1.925), (0.212, 0.220, 1.985),
         (0.258, 0.216, 1.990), (0.284, 0.214, 1.955)]
add("crook", g_tube(crook, [0.0095, 0.009, 0.0085, 0.008, 0.0078, 0.0075, 0.007, 0.006], n=6), "metal", "iron", bone="spine_03", sharp=60)
add("crook_mount", gjoin(g_box(0.060, 0.016, 0.050, (0.119, 0.152, 1.30)), g_box(0.052, 0.016, 0.040, (0.122, 0.166, 1.40))), "metal", "iron",
    bone="spine_03", bevel=0.003, sharp=40)
props("lantern", T(0.284, 0.214, 1.955), lantern_parts(h=0.140, r=0.050, n=6, frame_src="gilt", flame=1.25), "spine_03")

# ------------------------------------------------------------------ arming swords (right hand; weapon local +Z up the blade, X = edge)
G = grip("r")
def grip_parts(src="strap", r=0.0142, z0=-0.058, z1=0.060):
    return (tube_w([(0, 0, z0), (0, 0, 0.0), (0, 0, z1)], [r * 0.94, r, r * 0.92], 8), "leather", src, dict(sharp=60))
# tier 1: plain iron arming sword, straight bar guard, disc pommel
wpn("wpn_1", G, [
    (g_blade([(0.066, 0.026, 0.0085), (0.42, 0.0235, 0.0075), (0.74, 0.0175, 0.0055), (0.815, 0.0, 0.0)]), "metal", "iron", dict(sharp=30)),
    grip_parts(),
    (g_box(0.165, 0.022, 0.020, (0, 0, 0.068)), "metal", "iron", dict(bevel=0.004, sharp=40)),
    (gxf(g_lathe([(0, -0.012), (0.024, -0.009), (0.027, 0.0), (0.024, 0.009), (0, 0.012)], n=10), T(0, 0, -0.078) @ R('X', 90)), "metal", "iron", dict(sharp=50)),
])
# tier 2: fullered steel blade, down-swept gilt quillons with lozenge tips, wheel pommel
q2 = [(-0.108, 0, 0.046), (-0.075, 0, 0.060), (-0.035, 0, 0.068), (0, 0, 0.070), (0.035, 0, 0.068), (0.075, 0, 0.060), (0.108, 0, 0.046)]
wpn("wpn_2", G, [
    (g_blade([(0.072, 0.029, 0.009), (0.16, 0.0275, 0.009), (0.48, 0.025, 0.008), (0.77, 0.0185, 0.006), (0.86, 0.0, 0.0)]), "metal", "blade", dict(sharp=30)),
    (g_box(0.0085, 0.0105, 0.40, (0, 0, 0.30)), "metal", "gilt", dict(bevel=0.0015, sharp=40)),
    grip_parts(),
    (g_tube(q2, [0.008, 0.0095, 0.011, 0.013, 0.011, 0.0095, 0.008], n=8, sy=0.85), "metal", "gilt", dict(sharp=60)),
    (gjoin(*[gxf(g_gem(0.012, 0.030, 4, 0.5), T(s * 0.114, 0, 0.042) @ R('Y', s * 70)) for s in (-1, 1)]), "metal", "gilt", dict(sharp=30)),
    (g_box(0.046, 0.026, 0.030, (0, 0, 0.072)), "metal", "gilt", dict(bevel=0.005, sharp=40)),
    (gxf(g_lathe([(0, -0.014), (0.022, -0.012), (0.030, -0.004), (0.030, 0.004), (0.022, 0.012), (0, 0.014)], n=12), T(0, 0, -0.082) @ R('X', 90)),
     "metal", "gilt", dict(sharp=50)),
    (band(0.050, 0.0150, 0.010, 8), "metal", "gilt", dict(sharp=60)), (band(-0.050, 0.0145, 0.010, 8), "metal", "gilt", dict(sharp=60)),
])
# tier 3: the oath-light: pale blade with a burning fuller, guard built as a little lantern cage round an ember, flame finials
cage = []
for s in (-1, 1):
    for sy_ in (-1, 1):
        cage.append(g_tube([(s * 0.020, sy_ * 0.014, 0.052), (s * 0.023, sy_ * 0.016, 0.073), (s * 0.020, sy_ * 0.014, 0.094)], 0.0036, n=4))
q3l = [(-0.024, 0, 0.073), (-0.060, 0, 0.078), (-0.096, 0, 0.070), (-0.124, 0, 0.050), (-0.132, 0, 0.026)]
q3r = [(-x, y, z) for (x, y, z) in q3l]
wpn("wpn_3", G, [
    (g_blade([(0.094, 0.031, 0.010), (0.18, 0.029, 0.010), (0.50, 0.027, 0.009), (0.80, 0.020, 0.0065), (0.905, 0.0, 0.0)]), "metal", "blade_pale", dict(sharp=30)),
    (g_blade([(0.11, 0.0058, 0.0125), (0.56, 0.0050, 0.0105), (0.70, 0.0, 0.0)]), "emis", "flame", dict(sharp=20)),
    grip_parts("glove", 0.0146, -0.060, 0.050),
    (gjoin(*cage), "metal", "gilt", dict(sharp=60)),
    (g_box(0.052, 0.036, 0.008, (0, 0, 0.050)), "metal", "gilt", dict(bevel=0.002, sharp=40)),
    (g_box(0.058, 0.040, 0.010, (0, 0, 0.097)), "metal", "gilt", dict(bevel=0.003, sharp=40)),
    (gxf(g_gem(0.0125, 0.040, 6, 0.6), T(0, 0, 0.068)), "emis", "flame", dict(sharp=20)),
    (g_tube(q3l, [0.010, 0.0105, 0.0095, 0.008, 0.004], n=8, sy=0.85), "metal", "gilt", dict(sharp=60)),
    (g_tube(q3r, [0.010, 0.0105, 0.0095, 0.008, 0.004], n=8, sy=0.85), "metal", "gilt", dict(sharp=60)),
    (gxf(g_lathe([(0.0, -0.030), (0.018, -0.020), (0.024, -0.004), (0.016, 0.010), (0.010, 0.018)], n=8), T(0, 0, -0.082)), "metal", "gilt", dict(sharp=50)),
    (gxf(g_gem(0.010, 0.022, 6, 0.5), T(0, 0, -0.114) @ R('X', 180)), "emis", "flame", dict(sharp=20)),
    (band(0.042, 0.0152, 0.009, 8), "metal", "gilt", dict(sharp=60)), (band(-0.052, 0.0150, 0.009, 8), "metal", "gilt", dict(sharp=60)),
])

# ------------------------------------------------------------------ shields (left forearm + fist; shield local +Y = face, +Z = up, X = along the forearm)
BACK, OUT, UP = 0.07, 0.094, 0.0
SF = shield_frame("l", BACK, OUT, UP)
def rivets(pts, yfn, r=0.008):
    return gjoin(*[ydome([(0.0, r * 0.7), (r * 0.7, r * 0.5), (r, 0.0)], n=6, x0=x, y0=float(yfn(x, z)), z0=z) for (x, z) in pts])

# tier 1: plank heater shield, iron rim, iron boss and two iron straps
W1, ZT1, ZB1 = 0.212, 0.245, -0.365
w1, y1 = heater_w(W1), shield_y(W1, 0.032, 0.61, -0.06)
wpn("wpn_1_off", SF, [
    (g_shield_board(w1, y1, ZB1, ZT1), "leather", "shieldwood", dict(recalc=False, solidify=0.012, solid_offset=-1.0, bevel=0.002, sharp=40)),
    (g_tube(shield_outline(w1, y1, ZB1, ZT1), 1.0, n=6, closed=True, sx=0.010, sy=0.010, up=(0, 1, 0)), "metal", "iron", dict(sharp=50)),
    (ydome([(0.074, 0.0), (0.074, 0.005), (0.058, 0.007), (0.050, 0.020), (0.030, 0.038), (0.0, 0.045)], n=12, y0=float(y1(0, -0.02)), z0=-0.02), "metal", "iron", dict(sharp=50)),
    (gjoin(face_strip(-0.185, 0.185, 0.120, 0.150, y1), face_strip(-0.135, 0.135, -0.190, -0.160, y1)), "metal", "iron",
     dict(recalc=False, solidify=0.004, solid_offset=-1.0, sharp=40)),
    (rivets([(-0.15, 0.135), (0.15, 0.135), (-0.10, -0.175), (0.10, -0.175), (-0.062, -0.02), (0.062, -0.02)], y1, 0.0075), "metal", "iron", dict(sharp=60)),
] + shield_back(y1, BACK, OUT, UP), bone="hand_l")

# tier 2: steel-faced heater, gilt rim and pale, lantern sigil in relief
W2, ZT2, ZB2 = 0.222, 0.255, -0.395
w2, y2 = heater_w(W2, straight=0.34), shield_y(W2, 0.034, 0.65, -0.07)
sig2 = [conform(gxf(g_prism([(-0.062, -0.028), (0.0, 0.034), (0.062, -0.028), (0.044, -0.028), (0.0, 0.012), (-0.044, -0.028)], 0.008), T(0, 0.004, 0.115)), y2),
        conform(gxf(g_prism(rhomb(0.030, 0.058), 0.010), T(0, 0.005, 0.015)), y2),
        conform(gxf(g_prism(bar(0.056, 0.010), 0.008), T(0, 0.004, -0.075)), y2)]
wpn("wpn_2_off", SF, [
    (g_shield_board(w2, y2, ZB2, ZT2), "metal", "steel", dict(recalc=False, solidify=0.010, solid_offset=-1.0, bevel=0.002, sharp=40)),
    (g_tube(shield_outline(w2, y2, ZB2, ZT2), 1.0, n=6, closed=True, sx=0.011, sy=0.013, up=(0, 1, 0)), "metal", "gilt", dict(sharp=50)),
    (conform(g_box(0.030, 0.004, 0.20, (0, 0.002, -0.22)), y2), "metal", "gilt", dict(sharp=40)),
    (gjoin(*sig2), "metal", "gilt", dict(bevel=0.0015, sharp=40)),
    (rivets([(-0.165, 0.20), (0.165, 0.20), (-0.175, 0.05), (0.175, 0.05), (-0.14, -0.12), (0.14, -0.12), (-0.075, -0.26), (0.075, -0.26)], y2, 0.007),
     "metal", "gilt", dict(sharp=60)),
] + shield_back(y2, BACK, OUT, UP, thick=0.010), bone="hand_l")

# tier 3: the beacon ward: tall kite, gilt sunburst round a lantern window with a living flame, flame finials on the shoulders
W3, ZT3, ZB3 = 0.228, 0.300, -0.560
w3, y3 = kite_w(W3, head=0.23), shield_y(W3, 0.036, 0.86, -0.10, vdepth=0.012)
rays = []
for i in range(10):
    a = TAU * (i + 0.5) / 10
    ln = 0.115 if i % 2 else 0.080
    rays.append(conform(gxf(g_prism([(-0.011, 0.0), (0.011, 0.0), (0.0, ln)], 0.006), T(0.074 * math.sin(a), 0.003, 0.085 + 0.074 * math.cos(a)) @ R('Y', math.degrees(a))), y3))
fin = [gxf(g_tooth(0.085, 0.020, 0.030, 6), T(s * 0.150, float(y3(0.150, 0.245)) - 0.005, 0.250) @ R('Z', 90 - 90 * s) @ R('Y', -24)) for s in (-1, 1)]
yc3 = float(y3(0, 0.085))
wpn("wpn_3_off", SF, [
    (g_shield_board(w3, y3, ZB3, ZT3, nu=9, nv=14), "metal", "blade_pale", dict(recalc=False, solidify=0.010, solid_offset=-1.0, bevel=0.002, sharp=40)),
    (g_tube(shield_outline(w3, y3, ZB3, ZT3, nv=14), 1.0, n=6, closed=True, sx=0.012, sy=0.015, up=(0, 1, 0)), "metal", "gilt", dict(sharp=50)),
    (gjoin(*rays), "metal", "gilt", dict(sharp=40)),
    (ydome([(0.070, 0.0), (0.070, 0.010), (0.056, 0.016), (0.046, 0.012), (0.044, 0.002)], n=14, y0=yc3, z0=0.085), "metal", "gilt", dict(sharp=40)),
    (ydome([(0.0, 0.004), (0.044, 0.004)], n=14, y0=yc3, z0=0.085), "metal", "iron", dict(sharp=40)),
    (gxf(g_lathe([(0.0, -0.030), (0.020, -0.012), (0.024, 0.004), (0.013, 0.026), (0.0, 0.046)], n=6, sy=0.5), T(0, yc3 + 0.011, 0.080)), "emis", "flame", dict(sharp=180)),
    (conform(gxf(g_prism([(-0.008, 0.0), (0.008, 0.0), (0.0, -0.30)], 0.006), T(0, 0.003, -0.07)), y3), "emis", "flame", dict(sharp=30)),
    (conform(gxf(g_prism([(-0.020, 0.0), (0.020, 0.0), (0.0, -0.34)], 0.004), T(0, 0.001, -0.05)), y3), "metal", "gilt", dict(sharp=40)),
    (gjoin(*fin), "metal", "gilt", dict(sharp=50)),
    (rivets([(-0.17, 0.18), (0.17, 0.18), (-0.15, -0.06), (0.15, -0.06), (-0.10, -0.26), (0.10, -0.26)], y3, 0.0075), "metal", "gilt", dict(sharp=60)),
] + shield_back(y3, BACK, OUT, UP, thick=0.010), bone="hand_l")

report_unweighted()
tri_report()
finalize(c, CID, hair_tint=(0.16, 0.10, 0.055), skin=dict(gain=0.97, sat=0.85), decimate={"hair": 0.5, "leather": 0.8},
         notes="Pose-tested: Idle_Shield_Loop, Sword_Block, Shield_Dash, Shield_OneShot, Sword_Regular_A, Jog_Fwd_Loop. wpn_N = arming sword "
               "skinned 100% to hand_r (UAL sword fist grip); wpn_N_off = shield skinned 100% to hand_l: the board lies along the left forearm "
               "(elbow to knuckles), face = back of the hand, top = thumb side, so it stands upright and faces forward in the shield clips. "
               "The beacon lantern on the back crook is rigid to spine_03 (emis_ flame).")
