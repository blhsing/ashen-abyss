"""npc_merchant -- 行商 莉雅, the travelling trader: wide-brimmed felt hat with a saffron band and a feather, saffron scarf,
olive travel cloak (shoulder cape with a brass clasp + long back panel), russet dress with a deep-folded skirt and a
sash, and a big pack on a wooden frame: stuffed canvas sack with a hide flap, a banded carpet roll across the top, a
bedroll under it, a bolt of cloth and a kettle lashed to the sides. Coin purse and tally scrolls at the belt."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_twn import *

CID = "npc_merchant"
c = start("female")
B = c.B

# ------------------------------------------------------------------ source materials
SAF = (0.42, 0.24, 0.045)
mats.cloth("dress", base=(0.185, 0.078, 0.042), bands=[(0.0, 0.035, (0.07, 0.05, 0.035))], folds=0.9, ash=0.6, ash_h=0.5, worn=0.6, var=0.35)
mats.cloth("dress_top", base=(0.185, 0.078, 0.042), folds=0.5, ash=0.15, worn=0.6, var=0.35)
mats.cloth("cloak", base=(0.082, 0.098, 0.050), folds=0.9, ash=0.6, ash_h=0.9, worn=0.7, var=0.35)
mats.cloth("cloak_cape", base=(0.082, 0.098, 0.050), folds=0.5, ash=0.25, worn=0.7, var=0.35)
mats.cloth("under_linen", base=(0.33, 0.30, 0.24), folds=0.4, ash=0.2, worn=0.5)
mats.cloth("hat_felt", base=(0.062, 0.048, 0.036), folds=0.1, ash=0.45, ash_h=1.6, worn=0.6, scan="wool")
mats.cloth("scarf", base=SAF, folds=0.5, ash=0.2, worn=0.5)
mats.cloth("sash_band", base=SAF, folds=0.3, ash=0.2, worn=0.5)
mats.cloth("sack_burlap", base=(0.23, 0.195, 0.135), folds=0.4, ash=0.4, worn=0.6)
mats.cloth("carpet", base=(0.20, 0.045, 0.035), bands=[(0.03, 0.07, SAF), (0.16, 0.22, (0.05, 0.075, 0.15)), (0.27, 0.33, SAF),
                                                        (0.38, 0.44, (0.05, 0.075, 0.15)), (0.53, 0.57, SAF)], folds=0.0, ash=0.3, scan="brocade")
mats.cloth("bedroll", base=(0.115, 0.12, 0.13), folds=0.2, ash=0.4, scan="wool")
mats.cloth("bolt", base=(0.07, 0.10, 0.17), bands=[(0.0, 0.03, (0.3, 0.27, 0.2)), (0.52, 0.56, (0.3, 0.27, 0.2))], folds=0.0, ash=0.2, scan="twill")
mats.flat("feather", (0.30, 0.20, 0.10), rough=0.8)
mats.flat("rope", (0.24, 0.19, 0.12), rough=0.9)
mats.flat("parchment", (0.42, 0.36, 0.24), rough=0.8)
mats.leather("boot", base=(0.045, 0.03, 0.02), ash=0.7, ash_h=0.35)
mats.leather("strap", base=(0.075, 0.045, 0.024), worn=0.7)
mats.leather("flap_hide", base=(0.13, 0.09, 0.05), worn=0.8, ash=0.4)
mats.leather("purse", base=(0.10, 0.04, 0.03), worn=0.6)
mats.wood("frame", base=(0.10, 0.065, 0.038))
mats.metal("brass", base=(0.36, 0.24, 0.075), rough=(0.30, 0.56), rust=(0.04, 0.07, 0.05), rust_amt=0.3, ash=0.1)
mats.metal("tin", base=(0.20, 0.20, 0.205), rough=(0.4, 0.7), rust_amt=0.25, ash=0.2, pattern="hammered", soot=0.5)

# ------------------------------------------------------------------ linen under-sleeves, dress bodice with short sleeves, skirt, boots
mu = (B.w("lowerarm", "upperarm") > 0.5) & (np.abs(B.x) > 0.36) & (np.abs(B.x) < 0.708) & (B.w(*BONES_HAND) < 0.5)
us = shell(B, mu, 0.006, "undersleeves", "cloth", "under_linen", smooth=2, sharp=180)
wrinkle_shell(us, 0.003, 46.0, "x", 3)
bod = shirt(c, "dress_top", bottom=1.04, top=1.475, sleeve=0.405, offset=0.013, smooth=4, name="bodice")
toe_box(boots(c, "boot", top=0.40, offset=0.013, cuff=0.007, toe_cap=False))
DRS = [(0.30, 0, 0.02, 0.238, 0.192, 0.208), (0.62, 0, 0.01, 0.216, 0.178, 0.188), (0.92, 0, 0.02, 0.198, 0.168, 0.174),
       (1.06, 0, 0.012, 0.158, 0.118, 0.124), (1.12, 0, 0.0, 0.138, 0.100, 0.104)]
robe(c, "dress", DRS, nu=44, nv=13, name="dress_skirt", calf_to_thigh=0.5, ragged=0.008,
     mod=fold_mod([(8, 0.085, 0.2), (13, 0.028, 0.6)], cy=0.015, hem_pow=1.0, top=0.06, sag=0.008))
hide(c, (B.z < 1.46) & (np.abs(B.x) < 0.69) & (B.z > 0.36))
SZ = 1.085
scy = body_ring(c, SZ)[1]
srx, sry = body_ring(c, SZ, 0.020)[2], body_ring(c, SZ, 0.020)[3]
belt(c, SZ, "sash_band", width=0.085, thick=0.008, cls="cloth", name="sash", rx=srx, ry=sry)
KN = np.array((-0.060, scy - sry - 0.012, SZ))
add("sash_knot", lumpy(gxf(g_sphere(1.0, 8, 6), T(*KN) @ S(0.034, 0.022, 0.030)), 0.16, 50.0, 3), "cloth", "sash_band", bone="spine_01", sharp=180)
fy = front_y(DRS, 0.014)
for k, (x0, x1, zb, ph) in enumerate(((-0.105, -0.050, 0.66, 0.2), (-0.062, -0.012, 0.74, 0.6))):
    t_ = drape("sash_tail_%d" % k, x0, x1, SZ - 0.01, zb, fy, "cloth", "sash_band", nu=5, nv=8, thick=0.004, folds=((1.0, 0.006, ph),), point=0.03)
    w_func(t_, lambda co: {"pelvis": 1 - sstep(1.0 - co[:, 2], 0.0, 0.3) * 0.8, "thigh_r": sstep(1.0 - co[:, 2], 0.0, 0.3) * 0.8})
# coin purse (left hip) and tally scrolls (right hip, behind the knot)
PU = np.array((srx + 0.012, scy - 0.035, SZ - 0.105))
props("purse", T(*PU) @ R('Z', 75), [
    (g_pillow(0.050, 0.034, 0.060, e=0.75, nu=10, nv=7), "leather", "purse", dict(sharp=180)),
    (g_tube([(0.034 * math.cos(a), 0.022 * math.sin(a), 0.046) for a in np.linspace(0, TAU, 10, endpoint=False)], 0.005, n=4, closed=True), "cloth", "rope", dict(sharp=180)),
    (g_tube([(0, 0, 0.05), (0, -0.004, 0.085), (0, 0.004, 0.115)], 0.0045, n=4), "cloth", "rope", dict(sharp=180)),
], "pelvis")
tube, caps = g_scroll(0.20, 0.019, 0.022, 8)
SCR = T(-srx - 0.004, scy + 0.02, SZ - 0.13) @ R('Z', 100) @ R('X', 14)
props("scrolls", SCR, [(tube, "cloth", "parchment", dict(sharp=60)), (caps, "leather", "strap", dict(sharp=50)),
                       (gxf(tube, T(0.036, 0.006, -0.012) @ S(0.8, 0.8, 0.86)), "cloth", "parchment", dict(sharp=60)),
                       (g_tube([(0.0, 0.0, 0.10), (0.01, 0.0, 0.15)], 1.0, n=6, sx=0.003, sy=0.009), "leather", "strap", dict(sharp=60))], "pelvis")

# ------------------------------------------------------------------ travel cloak: shoulder cape (open in front, clasp) + long back panel, scarf
zmin = 1.275 - 0.55 * B.y
core = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > zmin) & (B.z < 1.535) & (np.abs(B.x) < 0.21)
arms = (B.w("upperarm", "clavicle") > 0.5) & (np.abs(B.x) < 0.395) & (B.z > 1.37)
vopen = (np.abs(B.x) < 0.014 + 0.42 * (1.44 - B.z)) & (B.y < 0.0) & (B.z < 1.44)
mm = (core | arms) & ~vopen & (B.w(*BONES_HAND, "lowerarm") < 0.3)
cape = shell(B, mm, lambda co: 0.030 + 0.014 * sstep(np.abs(co[:, 0]), 0.15, 0.22), "cape", "cloth", "cloak_cape", smooth=6, sharp=180)
chew_edge(cape, rounds=1, p=0.22, seed=5)
co = get_co(cape)
ang = np.arctan2(co[:, 1] - 0.02, co[:, 0])
rr = np.hypot(co[:, 0], co[:, 1] - 0.02)
d = 0.0075 * np.sin(10 * ang + 1.3 * np.sin(3 * ang + 0.4)) * sstep(1.50 - co[:, 2], 0.0, 0.12)
co[:, 0] += co[:, 0] / np.maximum(rr, 1e-6) * d
co[:, 1] += (co[:, 1] - 0.02) / np.maximum(rr, 1e-6) * d
set_co(cape, co)
ck = cloak(c, "cloak", z_top=1.43, z_bot=0.46, width=0.215, depth_top=0.118, depth_bot=0.225, arc=0.47, flare=0.05, ragged=0.035, nu=26, nv=13,
           pleats=6.0, pleat_amp=0.085, top_taper=0.9, cy=0.02, solid=False)
def cloak_legs(W, col, co):
    z, x = co[:, 2], co[:, 0]
    t = sstep(0.95 - z, 0.0, 0.3) * 0.35
    l = sstep(x, -0.06, 0.06)
    W = W * (1 - t)[:, None]
    W[:, col["thigh_l"]] += t * l
    W[:, col["thigh_r"]] += t * (1 - l)
    return W
w_mix(ck, cloak_legs)
props("clasp", frame((0.0, -0.118, 1.452), (0, -1, 0.25), (0, 0, 1)), [
    (g_lathe([(0.0, 0.010), (0.014, 0.008), (0.024, 0.002), (0.026, -0.004), (0.0, -0.004)], n=10), "metal", "brass", dict(sharp=40)),
    (gxf(g_prism(rhomb(0.008, 0.012), 0.004), T(0, 0, 0.011) @ R('X', 90)), "metal", "tin", dict(sharp=30)),
], "spine_03")
path, rad = [], []
for i in range(14):
    a = TAU * i / 14
    s_, c_ = math.sin(a), math.cos(a)
    path.append((0.098 * c_, 0.022 + (0.104 if s_ > 0 else 0.092) * s_, 1.512 + (0.016 * s_ if s_ > 0 else 0.046 * s_)))
    rad.append(0.037 if s_ > -0.3 else 0.030)
sc = fur_roll(path, rad, "scarf", name="scarf", noise=0.008, scale=30.0, n=7)
w_func(sc, lambda co: {"neck_01": 0.35, "spine_03": 0.65})
st_ = add("scarf_tail", g_tube([(0.050, -0.075, 1.490), (0.066, -0.118, 1.43), (0.074, -0.142, 1.33), (0.070, -0.146, 1.23)], [0.8, 1.0, 1.0, 0.8], n=8,
                               sx=0.010, sy=0.036, up=(0, -1, 0)), "cloth", "scarf", sharp=180)
wrinkle_shell(st_, 0.004, 50.0, "z", 4)
w_func(st_, lambda co: {"spine_03": sstep(co[:, 2], 1.28, 1.40), "spine_02": 1 - sstep(co[:, 2], 1.28, 1.40)})

# ------------------------------------------------------------------ hair + wide felt hat (band, feather)
add_hair(c, "Hair_Long")
HM = T(0.0, -0.008, 1.748) @ R('X', -7)
props("hat", HM, [
    (hat_wide(brim=0.262, crown=0.108, height=0.128, n=22), "cloth", "hat_felt", dict(sharp=180, solidify=0.006)),
    (g_tube([(0.1035 * math.cos(a), 0.1165 * math.sin(a), 0.030) for a in np.linspace(0, TAU, 20, endpoint=False)], 1.0, n=6, closed=True, sx=0.017,
            sy=0.0045, up=(0, 0, 1)), "cloth", "sash_band", dict(sharp=60)),
    (gxf(g_feather(0.21, 0.050, 0.004), T(0.103, 0.030, 0.026) @ R('Z', 100) @ R('Y', 16) @ R('X', -28)), "cloth", "feather", dict(sharp=30, double_sided=True)),
], "Head")

# ------------------------------------------------------------------ the pack: frame, sack + flap, carpet roll, bedroll, cloth bolt, kettle, rope
PY = 0.262
def packed(parts, prefix):
    for o in props(prefix, np.eye(4), parts, "spine_03"):
        w_func(o, lambda co: {"spine_03": 0.68, "spine_02": 0.32})
poles = [tube_w([(s * 0.128, 0.150, 0.985), (s * 0.130, 0.158, 1.30), (s * 0.126, 0.162, 1.66)], [0.015, 0.014, 0.012], 6) for s in (-1, 1)]
bars = [tube_w([(-0.150, 0.158, z), (0.150, 0.158, z)], 0.011, 6) for z in (1.06, 1.45)]
sack = lumpy(gxf(g_pillow(0.176, 0.120, 0.215, e=0.62, nu=14, nv=9), T(0, PY, 1.270)), 0.045, 17.0, 2, (0, PY, 1.27))
flap = gxf(g_pillow(0.184, 0.128, 0.224, e=0.62, nu=14, nv=5, t0=0.0, t1=0.40), T(0, PY, 1.272))
packed([
    (gjoin(*poles), "leather", "frame", dict(sharp=60)), (gjoin(*bars), "leather", "frame", dict(sharp=60)),
    (sack, "cloth", "sack_burlap", dict(sharp=180)),
    (flap, "leather", "flap_hide", dict(sharp=180, solidify=0.006)),
    (gjoin(*[g_tube([(s * 0.085, PY - 0.04, 1.500), (s * 0.088, PY - 0.118, 1.455), (s * 0.090, PY - 0.134, 1.36), (s * 0.090, PY - 0.130, 1.20)], 1.0, n=6,
                    sx=0.004, sy=0.014, up=(1, 0, 0)) for s in (-1, 1)]), "leather", "strap", dict(sharp=60)),
    (gxf(g_roll(0.62, 0.070, 10), T(0, PY - 0.012, 1.560)), "cloth", "carpet", dict(sharp=60)),
    (gjoin(*[g_tube([(s * 0.17, PY - 0.012 + 0.076 * math.cos(a), 1.560 + 0.076 * math.sin(a)) for a in np.linspace(0, TAU, 10, endpoint=False)], 0.006, n=4,
                    closed=True) for s in (-1, 1)]), "cloth", "rope", dict(sharp=180)),
    (gxf(g_roll(0.48, 0.062, 10), T(0, PY + 0.006, 1.010)), "cloth", "bedroll", dict(sharp=60)),
    (gjoin(*[g_tube([(s * 0.13, PY + 0.006 + 0.068 * math.cos(a), 1.010 + 0.068 * math.sin(a)) for a in np.linspace(0, TAU, 10, endpoint=False)], 1.0, n=6,
                    closed=True, sx=0.012, sy=0.0035, up=(1, 0, 0)) for s in (-1, 1)]), "leather", "strap", dict(sharp=60)),
    (gxf(g_roll(0.58, 0.040, 8), T(0.212, PY + 0.02, 1.325) @ R('Y', 90) @ R('X', 4)), "cloth", "bolt", dict(sharp=60)),
    (gxf(g_lathe([(0.0, 0.0), (0.058, 0.0), (0.072, 0.030), (0.068, 0.085), (0.058, 0.100), (0.062, 0.106), (0.0, 0.106)], n=10), T(-0.226, PY + 0.03, 1.105)),
     "metal", "tin", dict(sharp=50)),
    (g_tube([(-0.226, PY + 0.03 + 0.062 * math.cos(a), 1.211 + 0.070 * math.sin(a)) for a in np.linspace(0, math.pi, 7)], 0.0045, n=4), "metal", "tin", dict(sharp=60)),
    (g_tube([(-0.226, PY + 0.03, 1.281), (-0.200, PY + 0.02, 1.33), (-0.176, PY, 1.36)], 0.004, n=4), "cloth", "rope", dict(sharp=180)),
    (gxf(g_ring(0.058, 0.014, n=12, m=5), T(0.0, PY + 0.128, 1.30) @ R('X', 90)), "cloth", "rope", dict(sharp=180)),
], "pack")
# shoulder straps: over the cape from the pack top to the front edge of the cape, then round the ribs to the pack bottom
apply_mods(cape)
for s in (1, -1):
    ribbon(c, [(s * 0.104, 0.130, 1.555), (s * 0.108, 0.0, 1.585), (s * 0.112, -0.110, 1.490), (s * 0.122, -0.140, 1.375)], "strap", width=0.036, thick=0.006,
           offset=0.007, name="pack_strap_a%d" % s, closed=False, step=0.025, host=cape, only=["spine_03", "clavicle_l", "clavicle_r"])
    ribbon(c, [(s * 0.124, -0.150, 1.385), (s * 0.150, -0.110, 1.270), (s * 0.176, 0.000, 1.180), (s * 0.160, 0.130, 1.140)], "strap", width=0.036, thick=0.006,
           offset=0.019, name="pack_strap_b%d" % s, closed=False, step=0.025, only=["spine_03", "spine_02"])

report_unweighted("spine_03")
tri_report()
finalize(c, CID, hair_tint=(0.12, 0.065, 0.035), skin=dict(gain=1.16, sat=0.9, grime=0.35), max_tris=20000, decimate={"skin": 0.68, "hair": 0.6, "cloth": 0.78, "leather": 0.85},
         notes="Pose-tested: Idle_Talking_Loop, Idle_Loop, Walk_Loop. The pack (frame, sack, rolls, kettle) is rigid to spine_03 / spine_02 (0.68 / 0.32); "
               "the hat is rigid to Head.")
