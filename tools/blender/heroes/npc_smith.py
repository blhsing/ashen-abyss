"""npc_smith -- 鐵匠 布蘭, the blacksmith of Lastlamp: barrel-chested, thick-armed, shirt sleeves rolled above the elbow,
scorched hide apron (bib + skirt, neck strap) under a tool belt with tongs and a pouch, wool trousers, heavy boots,
sweat rag round the brow, cropped hair and a broad beard, a cross-peen forging hammer in the right fist."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_twn import *

CID = "npc_smith"
REV = bool(os.environ.get("SMITH_REV"))        # look-dev switch: hammer head below the fist instead of above it
c = start("male")
body_bulk(c, spec={"upperarm_l": 1.24, "upperarm_r": 1.24, "lowerarm_l": 1.22, "lowerarm_r": 1.22, "thigh_l": 1.10, "thigh_r": 1.10,
                   "calf_l": 1.08, "calf_r": 1.08},
          torso=[(0.88, 1.06, 1.10), (1.00, 1.14, 1.26), (1.12, 1.20, 1.36), (1.26, 1.16, 1.24), (1.40, 1.12, 1.10), (1.50, 1.04, 1.02), (1.56, 1.0, 1.0)])
B = c.B

# ------------------------------------------------------------------ source materials
mats.cloth("shirt_linen", base=(0.21, 0.185, 0.14), folds=0.7, ash=0.75, ash_h=1.0, worn=0.7, var=0.4)
mats.cloth("wool_pants", base=(0.050, 0.046, 0.042), folds=0.6, ash=0.8, ash_h=0.7)
mats.cloth("rag", base=(0.16, 0.085, 0.055), folds=0.3, ash=0.4, worn=0.8, weave=2.0)
mats.cloth("wrap", base=(0.20, 0.17, 0.12), folds=0.2, ash=0.6, worn=0.7, weave=2.0)
mats.leather("apron_char", base=(0.075, 0.046, 0.026), worn=0.9, crack=1.0, ash=0.7, ash_h=0.9, rough=(0.55, 0.85))
mats.leather("strap", base=(0.060, 0.036, 0.020), worn=0.7)
mats.leather("boot", base=(0.036, 0.025, 0.017), ash=0.8, ash_h=0.35)
mats.leather("pouch", base=(0.095, 0.058, 0.030), worn=0.7)
mats.wood("haft", base=(0.10, 0.062, 0.034))
mats.metal("iron", base=(0.15, 0.145, 0.14), rough=(0.45, 0.75), rust_amt=0.35, ash=0.2, pattern="hammered", soot=0.5)
mats.metal("face", base=(0.36, 0.36, 0.37), rough=(0.25, 0.5), rust_amt=0.1, ash=0.0, pattern="brushed", edge_amt=1.0)

# ------------------------------------------------------------------ shirt with rolled sleeves, trousers, boots, forearm guards
sh = shirt(c, "shirt_linen", bottom=1.0, top=1.50, sleeve=0.415, offset=0.010, smooth=4, name="shirt")
wrinkle_shell(sh, 0.0045, 30.0, "x", 4, mask=lambda co: sstep(np.abs(co[:, 0]), 0.22, 0.30))
ring = [(0.402, 0.068 + 0.084 * math.cos(a), 1.437 + 0.080 * math.sin(a)) for a in np.linspace(0, TAU, 14, endpoint=False)]
cf = fur_roll(ring, 0.019, "shirt_linen", name="cuff_l", noise=0.006, scale=38.0, n=7, bone="upperarm_l")
mirrored(cf)
tr = trousers(c, "wool_pants", top=1.03, bottom=0.30, offset=0.011, smooth=3)
wrinkle_shell(tr, 0.004, 26.0, "z", 3, mask=lambda co: 0.35 + 0.65 * np.exp(-((co[:, 2] - 0.55) / 0.11) ** 2))
boots(c, "boot", top=0.36, offset=0.016, cuff=0.008)
L = B.x > 0
mb = L & (B.w("lowerarm") > 0.5) & (B.x > 0.525) & (B.x < 0.705)
shell_band(c, mb, 0.012, "bracer_l", "leather", "strap", smooth=2, solidify=0.006)
for k, x in enumerate((0.560, 0.665)):
    P = wrap_path(c, [(x, 0.068 + 0.12 * math.cos(a), 1.437 + 0.12 * math.sin(a)) for a in np.linspace(0, TAU, 12, endpoint=False)], 0.022)
    add("bracer_strap_%d" % k, g_tube(P, 1.0, n=6, closed=True, sx=0.004, sy=0.011, up=(1, 0, 0)), "leather", "pouch", bone="lowerarm_l", sharp=60)
mw = (~L) & (B.w("lowerarm") > 0.5) & (B.x < -0.60) & (B.x > -0.735) & (B.w(*BONES_HAND) < 0.5)
shell_band(c, mw, 0.006, "wrap_r", "cloth", "wrap", smooth=1)

# ------------------------------------------------------------------ hide apron: bib on a neck strap + skirt, tool belt over it
GAP = 0.024
APR = [(z,) + body_ring(c, z, GAP) for z in (0.95, 1.02, 1.10, 1.18, 1.26, 1.34, 1.42)]
# stiff hide hangs plumb from the belly: below the waist the front never comes back in towards the thighs
yb = min(q[2] - q[4] for q in APR)
APR = [q if q[0] > 1.06 else (q[0], q[1], q[2], q[3], max(q[4], q[2] - yb + 0.004), q[5]) for q in APR]
lo = APR[0]
APR = [(0.36, 0, lo[2], lo[3] + 0.020, lo[4] + 0.034, lo[5]), (0.66, 0, lo[2], lo[3] + 0.012, lo[4] + 0.024, lo[5]),
       (0.86, 0, lo[2], lo[3] + 0.004, lo[4] + 0.014, lo[5])] + APR
fy = front_y(APR, 0.006)
tw = lambda co: {"spine_03": sstep(co[:, 2], 1.22, 1.30), "spine_02": (1 - sstep(co[:, 2], 1.22, 1.30)) * sstep(co[:, 2], 1.08, 1.14),
                 "spine_01": 1 - sstep(co[:, 2], 1.08, 1.14)}
bib = drape("apron_bib", -0.122, 0.122, 1.425, 1.06, fy, "leather", "apron_char", nu=11, nv=9, thick=0.007, folds=((1.5, 0.004, 0.2),), top=0.6,
            hem_pow=1.0, flare=0.46)
w_func(bib, tw)
def skirt_w(co):
    z, x = co[:, 2], co[:, 0]
    t = sstep(1.0 - z, 0.0, 0.13) * 0.93                # follows the thighs closely so a raised knee pushes the hide out
    l = sstep(x, -0.03, 0.03)
    return {"pelvis": 1 - t, "thigh_l": t * l, "thigh_r": t * (1 - l)}
ask = drape("apron_skirt", -0.182, 0.182, 1.085, 0.40, fy, "leather", "apron_char", nu=17, nv=12, thick=0.007, flare=0.08,
            folds=((2.0, 0.011, 0.3), (4.4, 0.004, 0.1)), zmod=lambda U, V, z: z + 0.07 * np.abs(2 * U - 1) ** 5 * (1 - V) ** 2)
w_func(ask, skirt_w)
ns, _, _ = ribbon(c, [(0.112, -0.30, 1.415), (0.100, -0.25, 1.50), (0.092, 0.0, 1.62), (0.0, 0.30, 1.535), (-0.092, 0.0, 1.62), (-0.100, -0.25, 1.50),
                      (-0.112, -0.30, 1.415)], "strap", width=0.030, thick=0.006, offset=0.017, name="apron_neck", closed=False, step=0.025,
                  only=["spine_03", "clavicle_l", "clavicle_r", "neck_01"])
apply_mods(ask)
decal("apron_pocket", [(-0.075, 0.045), (0.075, 0.045), (0.070, -0.045), (0.0, -0.058), (-0.070, -0.045)], ask, (0.0, -0.6, 0.90), "leather", "pouch",
      ray=(0, 1, 0), depth=0.006, lift=0.003)
BZ = 1.078
bx, by = body_ring(c, BZ, GAP + 0.012)[2], body_ring(c, BZ, GAP + 0.012)[3]
bcy = body_ring(c, BZ)[1]
belt(c, BZ, "strap", width=0.052, thick=0.009, rx=bx, ry=by, buckle="iron", bone="spine_01")
# tongs through a belt loop on the left hip, pouch on the right hip, rag tucked behind
TG = np.array((bx + 0.016, bcy - 0.02, BZ - 0.02))
bars = []
for s in (-1, 1):
    bars.append(gxf(gjoin(g_box(0.010, 0.012, 0.40, (0, 0, -0.10)), g_box(0.016, 0.012, 0.060, (s * 0.004, 0, -0.325))), R('Y', s * 3.5)))
props("tongs", T(*TG) @ R('Z', 80) @ R('X', -6), [
    (gjoin(*bars), "metal", "iron", dict(bevel=0.002, sharp=40)),
    (ydome([(0.0, 0.010), (0.007, 0.008), (0.009, 0.0)], n=6, y0=0.006, z0=-0.23), "metal", "iron", dict(sharp=60)),
    (g_tube([(0.0, -0.014, 0.03), (0.0, -0.016, -0.03), (0.0, 0.010, -0.035), (0.0, 0.012, 0.03)], 1.0, n=6, sx=0.016, sy=0.004, up=(1, 0, 0)), "leather", "strap",
     dict(sharp=60)),
], "pelvis")
props("pouch", T(-bx + 0.004, bcy + 0.02, BZ - 0.085) @ R('Z', -80), [
    (g_box(0.095, 0.050, 0.105), "leather", "pouch", dict(bevel=0.012, bevel_seg=2, sharp=60)),
    (g_box(0.100, 0.056, 0.036, (0, 0, 0.040)), "leather", "strap", dict(bevel=0.006, sharp=60)),
    (ydome([(0.0, 0.006), (0.006, 0.004), (0.008, 0.0)], n=6, y0=-0.028, z0=0.026), "metal", "iron", dict(sharp=60)),
], "pelvis")
rag = add("belt_rag", g_tube([(0.10, bcy + by + 0.012, BZ + 0.02), (0.105, bcy + by + 0.030, BZ - 0.04), (0.115, bcy + by + 0.036, BZ - 0.14),
                               (0.120, bcy + by + 0.030, BZ - 0.22)], [0.7, 1.0, 1.0, 0.75], n=8, sx=0.012, sy=0.040, up=(0, 1, 0)), "cloth", "rag",
          bone="pelvis", sharp=180)
wrinkle_shell(rag, 0.004, 60.0, "x", 7)

# ------------------------------------------------------------------ head: cropped hair, broad beard, sweat rag
add_hair(c, "Hair_Buzzed")
add_hair(c, "Hair_Beard", scale=(1.16, 1.14, 1.32), pivot=(0, -0.05, 1.625))
band = [(0.0745 * math.cos(a), -0.018 + (0.079 if math.sin(a) > 0 else 0.083) * math.sin(a), 1.730 + 0.014 * math.sin(a)) for a in np.linspace(0, TAU, 20, endpoint=False)]
hb = add("sweat_rag", g_tube(band, 1.0, n=6, closed=True, sx=0.0075, sy=0.019, up=(0, 0, 1)), "cloth", "rag", bone="Head", sharp=180)
displace_noise(hb, 0.0025, 70.0, 3)
add("sweat_rag_knot", gjoin(lumpy(gxf(g_sphere(1.0, 7, 5), T(0.012, 0.068, 1.744) @ S(0.020, 0.016, 0.018)), 0.2, 70.0, 2),
                           g_tube([(0.012, 0.074, 1.740), (0.030, 0.094, 1.695), (0.036, 0.100, 1.640)], [1.0, 1.0, 0.6], n=6, sx=0.004, sy=0.015, up=(0, 1, 0)),
                           g_tube([(0.008, 0.074, 1.740), (-0.010, 0.096, 1.708), (-0.018, 0.102, 1.668)], [1.0, 1.0, 0.6], n=6, sx=0.004, sy=0.014, up=(0, 1, 0))),
    "cloth", "rag", bone="Head", sharp=180)

# ------------------------------------------------------------------ cross-peen forging hammer in the right fist
# tilted towards the knuckles: with folded arms the head then stands in front of the left shoulder instead of the chin
G = grip("r", tilt=-48.0) @ (R('X', 180) if REV else np.eye(4))
ZH = 0.240
props("hammer", G, [
    (tube_w([(0, 0, -0.085), (0, 0, 0.0), (0, 0, 0.12), (0, 0, ZH + 0.026)], [0.0175, 0.0165, 0.0150, 0.0145], 8, sy=0.82), "leather", "haft", dict(sharp=60)),
    (gxf(g_prism([(0.066, -0.026), (0.066, 0.026), (-0.022, 0.026), (-0.074, 0.006), (-0.074, -0.006), (-0.022, -0.026)], 0.050), T(0, 0, ZH)), "metal", "iron",
     dict(bevel=0.004, sharp=40)),
    (gxf(g_box(0.004, 0.044, 0.046, (0.068, 0, 0)), T(0, 0, ZH)), "metal", "face", dict(bevel=0.0015, sharp=40)),
    (g_box(0.008, 0.024, 0.010, (0, 0, ZH + 0.029)), "metal", "face", dict(sharp=40)),
    (gjoin(g_box(0.004, 0.020, 0.070, (0.0, 0.0145, ZH - 0.058)), g_box(0.004, 0.020, 0.070, (0.0, -0.0145, ZH - 0.058))), "metal", "iron", dict(sharp=40)),
], "hand_r")

report_unweighted()
tri_report()
finalize(c, CID, hair_tint=(0.055, 0.045, 0.038), skin=dict(gain=0.90, sat=0.78, grime=1.0, age=0.8, scars=0.6), max_tris=20000,
         decimate={"skin": 0.75, "hair": 0.7, "cloth": 0.8},
         notes="Pose-tested: Idle_FoldArms_Loop, Idle_Talking_Loop, Idle_Loop, Walk_Loop. The hammer is part of body_leather / body_metal, skinned "
               "100% to hand_r in the UAL fist grip.")
