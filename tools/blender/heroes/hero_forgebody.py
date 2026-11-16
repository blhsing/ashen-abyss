"""hero_forgebody -- unarmed martial artist tempered in the dead forge: bare, heavily muscled arms and right chest cracked
with faintly glowing ember scars and a forge brand on the back, a scorched one-shoulder wrap, rust-red waist sash with
hanging tails, loose soot-grey trousers bound at the shins, heavy iron prayer beads, shaved head with a bound topknot
and a headband. Fist gauntlets on both hands: wpn_N (right) + wpn_N_off (left)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_mel import *

CID = "hero_forgebody"
c = start("male")
body_bulk(c, spec={"upperarm_l": 1.13, "upperarm_r": 1.13, "lowerarm_l": 1.10, "lowerarm_r": 1.10},
          torso=[(0.90, 1.0, 1.0), (1.10, 0.97, 0.97), (1.30, 1.05, 1.04), (1.42, 1.06, 1.05), (1.52, 1.0, 1.0)])
B = c.B

# ------------------------------------------------------------------ source materials
mats.cloth("wrap", base=(0.105, 0.088, 0.066), folds=0.3, ash=0.45, ash_h=0.6, worn=0.7, weave=2.0)
mats.cloth("char", base=(0.030, 0.026, 0.024), folds=0.6, ash=0.5, ash_h=1.6, worn=0.6, var=0.4)
mats.cloth("sash", base=(0.210, 0.046, 0.024), folds=0.5, ash=0.25, ash_h=0.9, worn=0.55, var=0.3)
mats.cloth("pants", base=(0.060, 0.052, 0.045), folds=1.0, ash=0.65, ash_h=0.55, worn=0.5)
mats.cloth("shoe", base=(0.026, 0.022, 0.020), folds=0.2, ash=0.6, ash_h=0.15)
mats.flat("knot", (0.020, 0.017, 0.015), rough=0.6)
mats.leather("strap", base=(0.050, 0.032, 0.020), worn=0.7)
mats.metal("iron", base=(0.135, 0.13, 0.125), rough=(0.42, 0.7), rust_amt=0.35, ash=0.0, pattern="hammered", soot=0.3)
mats.metal("steel", base=(0.30, 0.30, 0.31), rough=(0.25, 0.5), rust_amt=0.12, ash=0.0, pattern="hammered", edge_amt=1.0)
mats.metal("bronze", base=(0.34, 0.20, 0.07), rough=(0.28, 0.5), rust=(0.045, 0.085, 0.06), rust_amt=0.2, ash=0.0)
mats.metal("blackiron", base=(0.050, 0.048, 0.050), rough=(0.35, 0.6), rust_amt=0.1, ash=0.0, pattern="hammered", edge=(0.50, 0.36, 0.24),
           edge_amt=1.0, soot=0.4)
mats.emis("ember", (1.0, 0.20, 0.025), strength=2.0)

# ------------------------------------------------------------------ loose trousers bound at the shins, cloth shoes
ml = m_legs(B, 0.30, 1.05)
# each leg is a wide lofted tube (not a body shell): gathered into the shin wraps, ballooning over the knee, overlapping at the crotch
PL = [(0.300, 0.098, 0.045, 0.062, 0.072, 0.072), (0.385, 0.098, 0.040, 0.090, 0.102, 0.106), (0.55, 0.098, 0.018, 0.096, 0.122, 0.116),
      (0.75, 0.100, 0.010, 0.106, 0.132, 0.126), (0.92, 0.102, 0.022, 0.110, 0.134, 0.152), (1.045, 0.100, 0.022, 0.106, 0.122, 0.136)]
pl = robe(c, "pants", PL, nu=20, nv=14, p=2.1, name="pant_l", pleats=6, pleat_amp=0.045, calf_to_thigh=0.25, smooth=4,
          only=["pelvis", "spine_01", "thigh_l", "calf_l"])
mirrored(pl)
shell(B, m_legs(B, 0.74, 1.05), 0.012, "pants_seat", "cloth", "pants", smooth=3, sharp=180)
hide(c, ml & (B.z > 0.33) & (B.z < 1.02))
boots(c, "shoe", top=0.13, offset=0.010, cls="cloth", name="shoes")
msh = (B.w("calf", "foot") > 0.5) & (B.z > 0.085) & (B.z < 0.365)
shell(B, msh, 0.013, "shin_wraps", "cloth", "wrap", smooth=1, sharp=180)
hide(c, msh & (B.z > 0.11) & (B.z < 0.34))
sc_ = add("shin_cord_l", g_tube(helix_on_leg(c, 0.12, 0.345, 3.0, cx=0.098, cy=0.045, r_guide=0.085, offset=0.019, n=40), 0.0055, n=5),
          "leather", "strap", bone="calf_l", sharp=180)
mirrored(sc_)

# ------------------------------------------------------------------ scorched one-shoulder wrap (left shoulder -> right waist)
ztop = 1.52 + 1.25 * (B.x - 0.08)
tor = (B.w(*BONES_TORSO, "clavicle", "neck_01") > 0.5) & (B.z > 1.10) & (B.z < np.minimum(ztop, 1.535)) & (B.w("upperarm") < 0.45) & (np.abs(B.x) < 0.225)
capL = (B.x > 0.09) & (B.x < 0.262) & (B.w("clavicle", "upperarm", "spine_03") > 0.5) & (B.z > 1.395) & (B.w("lowerarm") < 0.2)
mk_ = tor | capL
kas = shell(B, mk_, 0.017, "kasaya", "cloth", "char", smooth=6, sharp=180, pin_boundary=True)
hide(c, B.shrink(mk_, 2))
# rolled, folded-over hem along the diagonal neckline (front and back), tucked into the sash at the right flank
def ray_pt(x, z, front, lift=0.020):
    ok_, loc, nrm, _f = c.ref.ray_cast(Vector((x, -1.0 if front else 1.0, z)), Vector((0, 1.0 if front else -1.0, 0)))
    return (np.array(loc) + np.array(nrm) * lift) if ok_ else None
xs = np.linspace(0.086, -0.205, 22)
zh = [min(1.52 + 1.25 * (x - 0.08), 1.530) - 0.004 for x in xs]
hem = ([ray_pt(x, z, True) for x, z in zip(xs, zh)] + list(wrap_path(c, [(-0.25, 0.03, 1.155)], 0.020)) +
       [ray_pt(x, z, False) for x, z in zip(xs[::-1], zh[::-1])] + list(wrap_path(c, [(0.098, 0.03, 1.62)], 0.020)))
hem = [q for q in hem if q is not None]
HP = resample(hem + [hem[0]], 0.028)[:-1]
for _ in range(2):
    HP = (np.roll(HP, 1, 0) + HP * 2 + np.roll(HP, -1, 0)) / 4
hr = add("kasaya_roll", g_tube(HP, 0.0175, n=7, closed=True), "cloth", "char", sharp=180)
displace_noise(hr, 0.004, 55.0, 3)
w_transfer(hr, c.ref, smooth=2, only=["spine_01", "spine_02", "spine_03", "clavicle_l", "clavicle_r", "neck_01"])

# ------------------------------------------------------------------ waist sash with a knot and hanging tails (left hip)
SCY = [body_ring(c, z)[1] for z in (1.02, 1.09, 1.165)]
def sash_mod(U, V, x, y, z):
    return x, y, z + 0.006 * np.sin(TAU * 3 * U + 9 * V) + 0.022 * (V - 0.5) * np.sin(TAU * (U - 0.1))
SS = [(1.015, 0, SCY[0], 0.182, 0.148, 0.150), (1.09, 0, SCY[1], 0.170, 0.136, 0.134), (1.168, 0, SCY[2], 0.166, 0.132, 0.128)]
sash = add("sash", g_stations(SS, nu=30, nv=7, p=2.3, mod=sash_mod), "cloth", "sash", solidify=0.011, sharp=180, double_sided=True)
w_transfer(sash, c.ref, smooth=2, only=["pelvis", "spine_01", "spine_02"])
KP = np.array((0.158, -0.082, 1.085))
knot = add("sash_knot", lumpy(gxf(g_sphere(1.0, 10, 6), T(*KP) @ S(0.050, 0.042, 0.046)), 0.16, 60.0, 4, KP), "cloth", "sash", sharp=180)
w_func(knot, lambda co: {"pelvis": 0.5, "spine_01": 0.5})
for i, (dx, zb, wd) in enumerate(((0.020, 0.60, 0.050), (-0.022, 0.70, 0.044))):
    pth = [KP + np.array((dx * 0.3, -0.012, -0.03)), KP + np.array((dx + 0.020, -0.030, -0.16)), KP + np.array((dx + 0.034, -0.030, -0.30)),
           KP + np.array((dx + 0.036, -0.022, zb - 1.085))]
    tl = add("sash_tail_%d" % i, g_tube(resample(pth, 0.05), 1.0, n=6, sx=0.0045, sy=wd, up=(0.3, -1, 0)), "cloth", "sash", sharp=180)
    w_func(tl, lambda co: {"pelvis": 1 - 0.8 * sstep(1.0 - co[:, 2], 0.0, 0.3), "thigh_l": 0.8 * sstep(1.0 - co[:, 2], 0.0, 0.3)})

# scorched loin flaps hanging from the sash (front + back): they also close the seam where the two trouser legs meet
def flap_w(co):
    z, x = co[:, 2], co[:, 0]
    t = sstep(1.0 - z, 0.0, 0.28) * 0.85
    l = sstep(x, -0.05, 0.05)
    return {"pelvis": 1 - t, "thigh_l": t * l, "thigh_r": t * (1 - l)}
ff_ = panel("loin_front", -0.072, 0.072, 1.045, 0.66, lambda x, z: -0.126 - 0.030 * sstep(1.0 - z, 0.0, 0.30) + 0.9 * x * x, "cloth", "char",
            nu=5, nv=9, thick=0.006, taper=-0.12, point=0.06)
w_func(ff_, flap_w)
fb_ = panel("loin_back", -0.066, 0.066, 1.045, 0.74, lambda x, z: 0.170 + 0.018 * sstep(1.0 - z, 0.0, 0.25) - 0.9 * x * x, "cloth", "char",
            nu=5, nv=7, thick=0.006, taper=-0.10, point=0.04)
w_func(fb_, flap_w)

# ------------------------------------------------------------------ forearm + palm wraps, iron arm ring
mw = (B.w("lowerarm", "upperarm") > 0.5) & (np.abs(B.x) > 0.492) & (np.abs(B.x) < 0.742) & (B.w(*BONES_HAND) < 0.5)
shell(B, mw, 0.006, "wraps", "cloth", "wrap", smooth=1, sharp=180)
hide(c, mw & (np.abs(B.x) > 0.515) & (np.abs(B.x) < 0.720))
mp = (B.W[:, B.col["hand_l"]] + B.W[:, B.col["hand_r"]] > 0.55) & (np.abs(B.x) < 0.845)
shell(B, mp, 0.004, "palm_wraps", "cloth", "wrap", smooth=0, sharp=180)
for s in (1, -1):
    add("wrap_cord_%d" % s, g_tube(helix_on_arm(c, 0.52, 0.71, 2.2, side=s, offset=0.011, phase=1.0 * s), 0.0055, n=5), "leather", "strap", sharp=180,
        bone="lowerarm_l" if s > 0 else "lowerarm_r")
AR = wrap_path(c, [(0.335, 0.075 + 0.14 * math.cos(a), 1.438 + 0.14 * math.sin(a)) for a in np.linspace(0, TAU, 16, endpoint=False)], 0.004)
add("arm_ring", g_tube(AR, 1.0, n=6, closed=True, sx=0.014, sy=0.006, up=(1, 0, 0)), "metal", "iron", bone="upperarm_l", sharp=50)

# ------------------------------------------------------------------ iron prayer beads (heavy necklace) with a forge tag
bp = []
for t in np.linspace(0, TAU, 80, endpoint=False):
    k = ((1 - math.cos(t)) / 2) ** 1.5
    bp.append((0.118 * math.sin(t), 0.105 * math.cos(t) - 0.02 * k, 1.512 - 0.215 * k))
BPW = wrap_path(c, bp, 0.040)
for _ in range(3):
    BPW = (np.roll(BPW, 1, 0) + BPW * 2 + np.roll(BPW, -1, 0)) / 4
BE = resample(list(BPW) + [BPW[0]], 0.041)[:-1]
beads = add("beads", g_beads(BE, 0.0195, 8, 5), "metal", "iron", sharp=60)
w_transfer(beads, c.ref, smooth=0, only=["spine_03", "neck_01", "clavicle_l", "clavicle_r"])
low = BE[int(np.argmin(BE[:, 2]))]
tag = props("bead_tag", T(low[0], low[1] - 0.004, low[2] - 0.022), [
    (gxf(g_sphere(0.027, 8, 5), T(0, 0, 0)), "metal", "bronze", dict(sharp=60)),
    (gxf(g_prism([(-0.020, 0.0), (0.020, 0.0), (0.026, -0.050), (0.0, -0.072), (-0.026, -0.050)], 0.009), T(0, -0.002, -0.026)), "metal", "iron", dict(bevel=0.002, sharp=40)),
    (gxf(g_prism(chevron(0.012, 0.012, 0.006), 0.012), T(0, -0.002, -0.058)), "emis", "ember", dict(sharp=20)),
], "spine_03")

# ------------------------------------------------------------------ ember scars: cooling-metal cracks on the arms / chest, forge brand on the back
SK = dict(amp=0.015, seg=0.030)
cracks(c, "scar_arm_r_top", (-0.245, 0.060, 1.60), (-1, 0.05, 0), 0.215, "ember", width=0.013, seed=3, branches=2, side_hint=(0, 0, 1), **SK)
cracks(c, "scar_arm_r_front", (-0.275, -0.06, 1.475), (-1, 0, -0.28), 0.17, "ember", width=0.012, seed=5, branches=1, side_hint=(0, 1, 0), **SK)
cracks(c, "scar_arm_r_back", (-0.30, 0.20, 1.46), (-1, 0, -0.1), 0.15, "ember", width=0.012, seed=8, branches=1, side_hint=(0, 1, 0), **SK)
cracks(c, "scar_arm_l_top", (0.300, 0.085, 1.60), (1, -0.08, 0), 0.165, "ember", width=0.013, seed=12, branches=2, side_hint=(0, 0, 1), **SK)
cracks(c, "scar_arm_l_front", (0.34, -0.06, 1.47), (1, 0, -0.2), 0.12, "ember", width=0.012, seed=14, branches=1, side_hint=(0, 1, 0), **SK)
cracks(c, "scar_chest", (-0.215, -0.20, 1.475), (0.62, 0, -1), 0.20, "ember", width=0.013, seed=21, branches=2, side_hint=(0, 1, 0), **SK)
cracks(c, "scar_ribs", (-0.21, -0.20, 1.30), (0.35, 0, -1), 0.11, "ember", width=0.011, seed=23, branches=1, side_hint=(0, 1, 0), **SK)
cracks(c, "scar_back", (-0.225, 0.30, 1.47), (0.45, 0, -1), 0.15, "ember", width=0.012, seed=31, branches=1, side_hint=(0, 1, 0), **SK)
# forge brand on the bare right shoulder blade: ring + hammer mark, laid out in the tangent plane of the back
ok_, bp0, bn0, _fi = c.ref.closest_point_on_mesh(Vector((-0.138, 0.34, 1.405)))
bp0, bn0 = np.array(bp0), np.array(bn0) / np.linalg.norm(bn0)
bu = np.array((0.0, 0.0, 1.0)) - bn0 * bn0[2]
bu /= np.linalg.norm(bu)
bv = np.cross(bn0, bu)
def bpt(a, b):
    return bp0 + bn0 * 0.012 + bv * a + bu * b
welt(c, "brand_ring", [bpt(0.052 * math.cos(a), 0.052 * math.sin(a)) for a in np.linspace(0, TAU, 24, endpoint=False)], "ember", width=0.013, closed=True)
welt(c, "brand_bar", [bpt(0, 0.026), bpt(0, -0.030)], "ember", width=0.013)
welt(c, "brand_head", [bpt(-0.026, 0.022), bpt(0.026, 0.022)], "ember", width=0.017)
welt(c, "brand_foot", [bpt(-0.016, -0.030), bpt(0.016, -0.030)], "ember", width=0.011)

# ------------------------------------------------------------------ head: stubble, bound topknot, headband with tails
add_hair(c, "Hair_Buzzed")
TK = np.array((0.0, 0.028, 1.800))
props("topknot", T(*TK), [
    (gxf(g_sphere(1.0, 10, 6), T(0, 0.004, 0.030) @ S(0.036, 0.040, 0.032)), "cloth", "knot", dict(sharp=180)),
    (g_lathe([(0.030, -0.030), (0.021, -0.006), (0.019, 0.008)], n=10), "cloth", "knot", dict(sharp=180)),
    (g_lathe([(0.0205, -0.002), (0.0255, 0.004), (0.0205, 0.010)], n=10), "metal", "bronze", dict(sharp=50)),
    (g_tube([(0, 0.020, 0.040), (0, 0.060, 0.050), (0, 0.092, 0.030), (0, 0.108, -0.010)], [0.018, 0.016, 0.012, 0.004], n=6), "cloth", "knot", dict(sharp=180)),
], "Head")
hb = [(0.105 * math.sin(a), -0.020 - 0.115 * math.cos(a), 1.738 + 0.012 * math.cos(a)) for a in np.linspace(0, TAU, 24, endpoint=False)]
HB = wrap_path(c, hb, 0.010)
add("headband", g_tube(HB, 1.0, n=6, closed=True, sx=0.019, sy=0.004, up=(0, 0, 1)), "cloth", "sash", bone="Head", sharp=60)
bk = HB[int(np.argmax(HB[:, 1]))]
hbt = [(gxf(g_sphere(0.018, 8, 5), T(bk[0], bk[1] + 0.010, bk[2])), "cloth", "sash", dict(sharp=180))]
for s, ln in ((1, 0.22), (-1, 0.17)):
    pth = [bk + np.array((s * 0.006, 0.012, -0.004)), bk + np.array((s * 0.022, 0.040, -0.06)), bk + np.array((s * 0.034, 0.052, -0.06 - ln * 0.5)),
           bk + np.array((s * 0.040, 0.048, -0.06 - ln))]
    hbt.append((g_tube(resample(pth, 0.04), 1.0, n=6, sx=0.003, sy=0.017, up=(0, 1, 0)), "cloth", "sash", dict(sharp=180)))
props("headband_tails", np.eye(4), hbt, "Head")

# ------------------------------------------------------------------ fist gauntlets (hand-local: x = wrist -> knuckles, y = thumb side, z = back of the hand)
KY = (0.031, 0.005, -0.019, -0.043)          # knuckle positions across the hand
def palm_band(src="strap", x=0.084, w=0.032):
    return (loop_yz(x, (0.048, 0.0, -0.050, -0.057, -0.047, 0.0, 0.040, 0.053), (0.023, 0.027, 0.019, -0.008, -0.039, -0.045, -0.041, -0.010), 6, w, 0.006),
            "leather", src, dict(sharp=60))
def wrist_band(src="strap", x=-0.010, w=0.026):
    return (loop_yz(x, (0.034, 0.0, -0.038, -0.044, -0.034, 0.0, 0.030, 0.040), (0.022, 0.027, 0.021, -0.008, -0.040, -0.046, -0.040, -0.010), 6, w, 0.006),
            "leather", src, dict(sharp=60))
def tier1():
    studs = gjoin(*[gxf(g_sphere(0.0115, 6, 4), T(0.137, y, 0.037)) for y in KY])
    return [palm_band(), wrist_band(),
            (g_shell_plate(0.004, 0.104, 0.034, 0.048, 0.025, 0.007, 0.006), "metal", "iron", dict(solidify=0.005, sharp=40)),
            (g_box(0.034, 0.104, 0.022, (0.122, -0.006, 0.034)), "metal", "iron", dict(bevel=0.005, sharp=40)),
            (studs, "metal", "iron", dict(sharp=50)),
            (g_box(0.010, 0.014, 0.016, (-0.010, 0.0, 0.031)), "metal", "iron", dict(sharp=40))]
def tier2():
    spikes = gjoin(*[gxf(g_cone(0.0145, 0.042, 4), frame((0.132, y, 0.040), (1.0, 0.0, 0.42), (0, 1, 0))) for y in KY])
    cuff = half_cuff(-0.062, -0.004, 0.054, 0.044, arc=0.58)
    return [palm_band(), wrist_band(),
            (g_shell_plate(0.040, 0.108, 0.044, 0.052, 0.027, 0.008, 0.007), "metal", "steel", dict(solidify=0.005, sharp=40)),
            (g_shell_plate(-0.004, 0.052, 0.036, 0.046, 0.031, 0.008, 0.007), "metal", "steel", dict(solidify=0.005, sharp=40)),
            (g_box(0.105, 0.010, 0.008, (0.052, -0.004, 0.041)), "metal", "bronze", dict(bevel=0.002, sharp=40)),
            (g_box(0.036, 0.110, 0.026, (0.123, -0.006, 0.036)), "metal", "steel", dict(bevel=0.006, sharp=40)),
            (g_box(0.008, 0.112, 0.010, (0.106, -0.006, 0.050)), "metal", "bronze", dict(bevel=0.002, sharp=40)),
            (spikes, "metal", "steel", dict(sharp=30)),
            (g_box(0.100, 0.007, 0.052, (0.056, -0.0615, -0.006)), "metal", "steel", dict(bevel=0.003, sharp=40)),
            (cuff, "metal", "steel", dict(solidify=0.005, sharp=40)),
            (gxf(g_tube(arc_pts(0.0565, 76, 284, 9, "xy"), 0.0045, n=5), T(-0.064, -0.004, -0.008) @ R('Y', 90)), "metal", "bronze", dict(sharp=60))]
def tier3():
    horns = gjoin(*[gxf(g_tooth(0.066 - 0.008 * abs(i - 1.5), 0.0135, 0.016, 5), frame((0.130, y, 0.041), (1.0, 0.0, 0.30), (0, 1, 0)) @ R('Z', 90))
                    for i, y in enumerate(KY)])
    cuff = half_cuff(-0.078, -0.004, 0.060, 0.045, arc=0.60)
    fins = gjoin(*[gxf(g_prism([(0.0, 0.0), (-0.060, 0.030), (-0.074, 0.0)], 0.007), T(-0.006, y, 0.036) @ R('X', 10 * sg)) for y, sg in ((0.022, -1), (-0.030, 1))])
    vents = gjoin(*[g_box(0.030, 0.007, 0.006, (-0.045, y, 0.049 - 0.0035 * abs(i - 1) * 3)) for i, y in enumerate((0.018, -0.004, -0.026))])
    glow = [(g_box(0.005, 0.094, 0.009, (0.1415, -0.006, 0.030)), "emis", "ember", dict(sharp=40)),
            (gxf(g_prism(chevron(0.020, 0.019, 0.008), 0.006), T(0.062, -0.004, 0.0425) @ R('X', 90) @ R('Y', -90)), "emis", "ember", dict(sharp=20)),
            (gxf(g_prism(chevron(0.016, 0.015, 0.007), 0.006), T(0.022, -0.004, 0.0445) @ R('X', 90) @ R('Y', -90)), "emis", "ember", dict(sharp=20)),
            (vents, "emis", "ember", dict(sharp=40)),
            (g_box(0.080, 0.004, 0.008, (0.060, -0.0655, -0.004)), "emis", "ember", dict(sharp=40))]
    return [palm_band(), wrist_band(),
            (g_shell_plate(0.038, 0.110, 0.046, 0.055, 0.028, 0.009, 0.008), "metal", "blackiron", dict(solidify=0.006, sharp=40)),
            (g_shell_plate(-0.006, 0.052, 0.038, 0.048, 0.032, 0.009, 0.008), "metal", "blackiron", dict(solidify=0.006, sharp=40)),
            (g_box(0.038, 0.116, 0.028, (0.124, -0.006, 0.037)), "metal", "blackiron", dict(bevel=0.006, sharp=40)),
            (g_box(0.008, 0.118, 0.010, (0.106, -0.006, 0.052)), "metal", "bronze", dict(bevel=0.002, sharp=40)),
            (horns, "metal", "bronze", dict(sharp=40)),
            (g_box(0.104, 0.008, 0.056, (0.056, -0.0625, -0.006)), "metal", "blackiron", dict(bevel=0.003, sharp=40)),
            (cuff, "metal", "blackiron", dict(solidify=0.006, sharp=40)),
            (gxf(g_tube(arc_pts(0.0635, 72, 288, 9, "xy"), 0.005, n=5), T(-0.080, -0.004, -0.008) @ R('Y', 90)), "metal", "bronze", dict(sharp=60)),
            (fins, "metal", "bronze", dict(bevel=0.0015, sharp=40))] + glow

GS = T(0.05, -0.004, -0.008) @ S(1.13) @ T(-0.05, 0.004, 0.008)      # slightly oversized so the fists read from the game camera
HR, HL = hand_frame("r") @ GS, hand_frame("l") @ GS
for n_, fn in ((1, tier1), (2, tier2), (3, tier3)):
    parts = fn()
    wpn("wpn_%d" % n_, HR, parts, bone="hand_r")
    wpn("wpn_%d_off" % n_, HL, [(mirror_y(g_), cls, src, kw) for (g_, cls, src, kw) in parts], bone="hand_l")

report_unweighted()
finalize(c, CID, hair_tint=(0.035, 0.03, 0.028), skin=dict(gain=0.95, sat=0.85),
         tex={"cloth": 1024, "leather": 256, "metal": 1024},
         notes="Pose-tested: Punch_Jab, Punch_Cross, Melee_Hook, Idle_Loop, Jog_Fwd_Loop. Fist gauntlets: wpn_N skinned 100% to hand_r, "
               "wpn_N_off (mirrored) 100% to hand_l; they cover the back of the hand, the knuckle line and the outer edge and leave the "
               "fingers free, so open-hand clips do not pierce them. The ember scars are separate emis_ geometry that follows the skin "
               "weights. The topknot is baked into the cloth_ atlas (hair_ holds stubble + brows).")
