"""form_wolf -- 狼靈形, the wildsoul's wolf-spirit form: a lean upright lupine beast on the shared humanoid rig. Mesh-only
proportions: deep narrow chest, tucked waist, neck craned forward, long-fingered clawed hands, haunches over thin shanks
with a raised hock and heel (digitigrade look faked by the leg mesh; the bones stay human). Grey pelt with a dark mane,
long-muzzled head with tall pointed ears, bushy tail rigid to the pelvis, moss-green spirit markings and eyes. No weapons."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_beast import *

CID = "form_wolf"
c = start("male")
spec = {"neck_01": 1.25}
for s in ("l", "r"):
    spec.update({"upperarm_" + s: 0.98, "lowerarm_" + s: 1.04, "hand_" + s: 1.08, "thigh_" + s: 1.20, "calf_" + s: 0.90})
spec.update(finger_spec(1.12, 1.1))
ANK = 0.036
def wolf_shape(co, B):
    w = np.clip(B.w("spine_02", "spine_03", "neck_01", "clavicle"), 0, 1)
    co[:, 1] -= 0.075 * sstep(co[:, 2], 1.28, 1.54) * w                      # neck and withers crane forward
    leg = np.clip(B.w("calf", "foot", "ball"), 0, 1)
    z = co[:, 2]
    co[:, 1] += leg * (0.125 * np.exp(-((z - 0.245) / 0.090) ** 2) - 0.050 * sstep(z, 0.34, 0.52))       # hock back, shin forward
    co[:, 1] -= 0.035 * np.clip(B.w("thigh"), 0, 1) * (1 - sstep(z, 0.52, 0.66))                          # stifle (knee) forward
    foot = np.clip(B.w("foot", "ball"), 0, 1)
    front = co[:, 1] < ANK
    co[:, 1] = np.where(front, ANK + (co[:, 1] - ANK) * (1 + 0.22 * foot), co[:, 1])                      # longer paw
    heel = foot * sstep(co[:, 1], 0.005, 0.075)
    co[:, 2] = co[:, 2] + heel * (0.115 - 0.5 * np.clip(co[:, 2], 0, 0.08))                               # heel off the ground
    co[:, 0] = co[:, 0] + (np.sign(co[:, 0]) * 0.089 - co[:, 0]) * 0.18 * heel                           # ... and narrower
    haunch = np.clip(B.w("thigh"), 0, 1) * sstep(z, 0.55, 0.80)
    co[:, 1] -= 0.022 * haunch
    co[:, 2] = np.where(leg > 0.5, np.maximum(co[:, 2], 0.0), co[:, 2])
    return co
beast_reshape(c, spec, torso=[(0.85, 1.0, 1.05), (1.00, 0.94, 1.00), (1.12, 0.90, 0.98), (1.28, 1.02, 1.22), (1.40, 1.06, 1.26), (1.50, 1.03, 1.16),
                              (1.58, 1.0, 1.0)], hand_len=1.30, extra=wolf_shape)
B = c.B

# ------------------------------------------------------------------ source materials
mats.fur("wolf_pelt", base=(0.062, 0.058, 0.054), tip=(0.36, 0.34, 0.30), locks=3.2)
mats.fur("wolf_mane", base=(0.026, 0.022, 0.019), tip=(0.13, 0.11, 0.085), locks=1.5)
mats.fur("wolf_pale", base=(0.13, 0.12, 0.105), tip=(0.52, 0.50, 0.45), locks=0.5)
mats.bone("claw", base=(0.10, 0.09, 0.08), stain=(0.025, 0.02, 0.018))
mats.bone("fang", base=(0.54, 0.49, 0.38))
mats.leather("nose", base=(0.016, 0.014, 0.013), rough=(0.35, 0.6), ash=0.0)
mats.fur("paw_pelt", base=(0.062, 0.058, 0.054), tip=(0.36, 0.34, 0.30), locks=0.0)
mats.emis("spirit", (0.36, 1.0, 0.22), strength=3.2)

# ------------------------------------------------------------------ pelt over the reshaped body
def fur_off(co):
    hands = (np.abs(co[:, 0]) > 0.735).astype(float)
    feet = 1 - sstep(co[:, 2], 0.05, 0.12)
    o = 0.012 + 0.014 * sstep(co[:, 2], 1.22, 1.45) * sstep(co[:, 1], 0.0, 0.12)
    return o * (1 - hands) * (1 - feet) + 0.004 * hands + 0.006 * feet * (1 - hands)
LOW = B.w("foot", "ball", "calf") > 0.5
fur = shell(B, (B.w("Head") < 0.5) & ~(LOW & (B.z < 0.085)), fur_off, "pelt", "cloth", "wolf_pelt", smooth=2, sharp=180)
paws = shell(B, LOW & (B.z < 0.135), fur_off, "paws", "cloth", "paw_pelt", smooth=2, sharp=180)
co = get_co(paws)
co[:, 2] = np.maximum(co[:, 2], 0.0)
set_co(paws, co)
displace_noise(fur, 0.006, 24.0, 5)
co = get_co(fur)
co[:, 2] = np.maximum(co[:, 2], 0.0)
set_co(fur, co)
drop_skin(c)

# ------------------------------------------------------------------ neck, mane, chest ruff, tail
HC = np.array((0.0, -0.165, 1.635))
neck = fur_blob("neck", (0.0, -0.060, 1.572), (0.098, 0.165, 0.104), "wolf_pelt", {"neck_01": 0.6, "Head": 0.4}, noise=0.010, scale=24.0, seed=2,
                rot=R('X', -14))
withers = fur_blob("withers", (0.0, 0.075, 1.515), (0.150, 0.150, 0.120), "wolf_mane", {"spine_03": 0.75, "neck_01": 0.25}, noise=0.014, scale=20.0, seed=3)
ruff = fur_blob("chest_ruff", (0.0, -0.118, 1.415), (0.105, 0.055, 0.115), "wolf_pale", "spine_03", noise=0.012, scale=24.0, seed=4)
TP = [(0.0, 0.130, 0.985), (0.0, 0.245, 0.930), (0.0, 0.380, 0.800), (0.0, 0.480, 0.640), (0.0, 0.535, 0.480), (0.0, 0.555, 0.380)]
tail = add("tail", g_tube(TP, [0.040, 0.062, 0.082, 0.078, 0.052, 0.012], n=9, sx=1.0, sy=0.85), "cloth", "wolf_pelt", sharp=180, subsurf=1)
shag(tail, 0.012, 26.0, 7)
w_rigid(tail, "pelvis")

# ------------------------------------------------------------------ wolf head (rigid to Head): long muzzle, tall ears, glowing eyes
skull = fur_blob("skull", HC, (0.116, 0.140, 0.114), "wolf_pelt", "Head", noise=0.006, scale=28.0, seed=9, nu=14, nv=9)
MD = np.array((0.0, -1.0, -0.24))
MDN = MD / np.linalg.norm(MD)
MO = HC + np.array((0.0, -0.085, -0.020))
head = [
    (muzzle_g(MO, MD, [(0.0, 0.086, 0.058, 0.064), (0.07, 0.066, 0.048, 0.046), (0.15, 0.048, 0.038, 0.031), (0.212, 0.038, 0.031, 0.024)], nu=12, nv=7),
     "cloth", "wolf_pelt", dict(sharp=180)),
    (muzzle_g(HC + np.array((0.0, -0.075, -0.074)), (0.0, -1.0, -0.42), [(0.0, 0.054, 0.028, 0.026), (0.08, 0.042, 0.022, 0.020), (0.165, 0.028, 0.017, 0.014),
                                                                         (0.200, 0.022, 0.014, 0.012)], nu=10, nv=6), "cloth", "wolf_pale", dict(sharp=180)),
    (gxf(g_pillow(0.036, 0.095, 0.016, e=1.0, nu=8, nv=5), T(*(HC + np.array((0.0, -0.200, -0.066)))) @ R('X', 17)), "leather", "nose", dict(sharp=180)),
    (gxf(g_pillow(0.024, 0.020, 0.019, e=0.8, nu=8, nv=6), T(*(MO + MDN * 0.216 + np.array((0, 0, 0.016))))), "leather", "nose", dict(sharp=180)),
]
for s in (1, -1):
    EM = frame((s * 0.060, HC[1] + 0.045, HC[2] + 0.070), (s * 0.34, 0.30, 1.0), (0, 1, 0))
    head += [
        (gxf(g_lathe([(0.054, 0.0), (0.048, 0.060), (0.027, 0.130), (0.0, 0.180)], n=7, sy=0.40), EM), "cloth", "wolf_mane", dict(sharp=60)),
        (gxf(g_lathe([(0.035, 0.014), (0.028, 0.066), (0.0, 0.144)], n=5, sy=0.30), EM @ T(0, -0.014, 0)), "leather", "nose", dict(sharp=60)),
        (gxf(g_sphere(1.0, 8, 6), T(s * 0.052, HC[1] - 0.112, HC[2] + 0.030) @ R('Y', -22 * s) @ S(0.0150, 0.009, 0.0085)), "emis", "spirit", dict(sharp=180)),
        (tuft((s * 0.050, HC[1] - 0.105, HC[2] + 0.050), (s * 0.55, -0.85, 0.05), 0.045, 0.014, 0.008), "cloth", "wolf_mane", dict(sharp=180)),
        (gxf(g_tooth(0.034, 0.0070, 0.007, 5), T(*(MO + MDN * 0.185 + np.array((s * 0.024, 0, -0.026)))) @ R('X', 180) @ R('Z', 90)), "bone", "fang", dict(sharp=40)),
        (gxf(g_tooth(0.024, 0.0060, 0.005, 5), T(s * 0.019, HC[1] - 0.238, HC[2] - 0.128) @ R('Z', 90)), "bone", "fang", dict(sharp=40)),
    ]
    for k in range(4):
        head.append((tuft((s * (0.092 - 0.006 * k), HC[1] + 0.01 + 0.030 * k, HC[2] - 0.030 - 0.020 * k), (s * 0.75, 0.35, -0.55), 0.085 + 0.022 * k, 0.026, 0.02),
                     "cloth", "wolf_pale" if k < 2 else "wolf_pelt", dict(sharp=180)))
props("head", np.eye(4), head, "Head")

# ------------------------------------------------------------------ claws, mane ridge, shaggy clumps
hand_claws(c, "claw", length=0.075, radius=0.0115, back=0.012)
foot_claws(c, n=4, src="claw", length=0.050, radius=0.0115, spread=0.07)
scatter_tufts(fur, 460, "wolf_pelt", "tufts", length=(0.06, 0.11), radius=(0.010, 0.018), sides=3, seed=2, gravity=0.72, jitter=0.3,
              mask=lambda cen, nrm: (np.abs(cen[:, 0]) < 0.72) & (cen[:, 2] > 0.30), flow=beast_flow)
scatter_tufts(fur, 90, "wolf_mane", "mane_back", length=(0.09, 0.15), radius=(0.010, 0.018), sides=3, seed=6, gravity=0.6,
              mask=lambda cen, nrm: (np.abs(cen[:, 0]) < 0.10) & (cen[:, 1] > 0.06) & (cen[:, 2] > 1.05) & (cen[:, 2] < 1.50), flow=lambda p: np.array((0.0, 0.8, 0.35)))
scatter_tufts(fur, 70, "wolf_pelt", "arm_fringe", length=(0.07, 0.12), radius=(0.010, 0.018), sides=3, seed=5, gravity=0.7,
              mask=lambda cen, nrm: (np.abs(cen[:, 0]) > 0.40) & (np.abs(cen[:, 0]) < 0.72) & (nrm[:, 2] < -0.25),
              flow=lambda p: np.array((0.25 if p[0] > 0 else -0.25, 0.2, -1.0)))
scatter_tufts(fur, 60, "wolf_pelt", "hock_fringe", length=(0.06, 0.10), radius=(0.010, 0.018), sides=3, seed=8, gravity=0.6,
              mask=lambda cen, nrm: (cen[:, 2] > 0.12) & (cen[:, 2] < 0.50) & (nrm[:, 1] > 0.3), flow=lambda p: np.array((0.0, 0.7, -0.7)))
scatter_tufts(withers, 60, "wolf_mane", "withers_tufts", length=(0.10, 0.16), radius=(0.010, 0.018), sides=3, seed=11, gravity=0.55, flow=lambda p: np.array((0.0, 0.7, 0.4)))
scatter_tufts(neck, 40, "wolf_mane", "neck_tufts", length=(0.08, 0.13), radius=(0.010, 0.018), sides=3, seed=12, gravity=0.5,
              mask=lambda cen, nrm: nrm[:, 2] > 0.2, flow=lambda p: np.array((0.0, 0.8, 0.3)))
scatter_tufts(neck, 50, "wolf_pale", "throat_tufts", length=(0.06, 0.10), radius=(0.010, 0.018), sides=3, seed=15, gravity=0.75,
              mask=lambda cen, nrm: nrm[:, 2] < 0.1, flow=lambda p: np.array((0.0, 0.25, -1.0)))
scatter_tufts(ruff, 45, "wolf_pale", "ruff_tufts", length=(0.07, 0.12), radius=(0.010, 0.018), sides=3, seed=16, gravity=0.8, flow=lambda p: np.array((0.0, -0.1, -1.0)))
scatter_tufts(tail, 70, "wolf_pelt", "tail_tufts", length=(0.08, 0.13), radius=(0.010, 0.018), sides=3, seed=13, gravity=0.6, flow=lambda p: np.array((0.0, 0.6, -0.8)))

# ------------------------------------------------------------------ spirit markings (same vocabulary as the bear form)
def inv_v(w, h, t):
    return [(-w, -h), (0.0, h), (w, -h), (w - t, -h), (0.0, h - 1.6 * t), (-w + t, -h)]
spirit_mark(skull, "mark_brow", rhomb(0.012, 0.028), (0.0, -0.8, HC[2] + 0.062), (0, 1, -0.2), up=(0, 0, 1), bone="Head")
for s in (1, -1):
    spirit_mark(skull, "mark_brow_%d" % s, bar(0.005, 0.026), (s * 0.040, -0.8, HC[2] + 0.076), (0, 1, -0.2), up=(s * 0.5, 0, 1), bone="Head")
    for k, (dx, w) in enumerate(((0.0, 0.046), (0.062, 0.038), (0.118, 0.028))):
        spirit_mark(fur, "mark_sh_%d_%d" % (s, k), chevron(w, w * 0.62, 0.014), (s * (0.215 + dx), 0.045, 2.2), (0, 0, -1), up=(-s, 0, 0))
    for k, x in enumerate((0.560, 0.665)):
        ring = [(s * x, 0.068 + 0.22 * math.cos(a), 1.44 + 0.22 * math.sin(a)) for a in np.linspace(0, TAU, 14, endpoint=False)]
        ribbon(c, ring, "spirit", width=0.016, thick=0.005, offset=0.007, name="mark_arm_%d_%d" % (s, k), cls="emis", closed=True, step=0.028, host=fur,
               wsmooth=0, sharp=180)
    spirit_mark(fur, "mark_thigh_%d_a" % s, inv_v(0.038, 0.026, 0.013), (s * 0.6, -0.01, 0.78), (-s, 0, 0), up=(0, 0, 1))
    spirit_mark(fur, "mark_thigh_%d_b" % s, bar(0.026, 0.007), (s * 0.6, -0.01, 0.715), (-s, 0, 0), up=(0, 0, 1))
spirit_mark(fur, "mark_chest_a", rhomb(0.020, 0.042), (0.0, -0.8, 1.215), (0, 1, 0))
for s in (1, -1):
    spirit_mark(fur, "mark_chest_%d" % s, [(0.0, 0.034), (s * 0.070, 0.062), (s * 0.070, 0.046), (0.0, 0.018)], (s * 0.024, -0.8, 1.215), (0, 1, 0))
spirit_line(c, fur, "mark_spine", [(0.0, 0.6, 1.33), (0.0, 0.6, 1.22), (0.0, 0.6, 1.11), (0.0, 0.6, 1.02)], width=0.018)
for k, zz in enumerate((1.27, 1.16, 1.06)):
    for s in (1, -1):
        spirit_line(c, fur, "mark_rib_%d_%d" % (k, s), [(s * 0.030, 0.6, zz), (s * 0.075, 0.6, zz - 0.030), (s * 0.125, 0.6, zz - 0.030)], width=0.013)
for k, i in enumerate((2, 3)):
    p = np.array(TP[i])
    rr = (0.082, 0.078)[k] + 0.10
    ring = [(rr * math.cos(a), p[1] + rr * math.sin(a) * 0.80, p[2] + rr * math.sin(a) * 0.60) for a in np.linspace(0, TAU, 14, endpoint=False)]
    o, _, _ = ribbon(c, ring, "spirit", width=0.016, thick=0.005, offset=0.008, name="mark_tail_%d" % k, cls="emis", closed=True, step=0.03, host=tail,
                     wsmooth=0, sharp=180)
    w_rigid(o, "pelvis")

report_unweighted()
tri_report(10)
finalize(c, CID, max_tris=35000,
         notes="Pose-tested: Zombie_Scratch, Sprint_Loop, NinjaJump_Start, Jog_Fwd_Loop, Idle_Loop. Lupine proportions and the digitigrade look "
               "(raised heel, hock, long paw) are mesh-only on the unmodified UAL rig; head / ears rigid to Head, tail rigid to pelvis. No skin, no "
               "weapons; emis_ = moss-green spirit markings and eyes (same colour as hero_wildsoul and form_bear).")
