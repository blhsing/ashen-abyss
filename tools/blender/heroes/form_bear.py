"""form_bear -- 熊靈形, the wildsoul's bear-spirit form: a hulking upright beast on the shared humanoid rig. Proportions come
from the mesh only (barrel trunk, enormous shoulders and forearms, thick hind legs, big paws); a shaggy dark pelt with a
shoulder hump, a broad bear head with a short pale muzzle thrust forward under the hump, heavy claws on every digit and
pale moss-green spirit markings (brow, shoulders, forearm rings, chest, spine, thighs) with glowing eyes. No weapons."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_beast import *

CID = "form_bear"
c = start("male")
spec = {"neck_01": 1.6}
for s in ("l", "r"):
    spec.update({"upperarm_" + s: 1.55, "lowerarm_" + s: 1.80, "hand_" + s: 1.45, "thigh_" + s: 1.72, "calf_" + s: 1.85, "foot_" + s: 1.42,
                 "ball_" + s: 1.38})
spec.update(finger_spec(1.55, 1.5))
def hunch(co, B):
    w = np.clip(B.w("spine_02", "spine_03", "neck_01", "clavicle"), 0, 1)
    co[:, 1] -= 0.055 * sstep(co[:, 2], 1.28, 1.52) * w              # upper trunk leans forward
    co[:, 2] = np.where(B.w("foot", "ball") > 0.5, np.maximum(co[:, 2], 0.0), co[:, 2])
    return co
beast_reshape(c, spec, torso=[(0.85, 1.22, 1.30), (1.00, 1.30, 1.48), (1.15, 1.34, 1.58), (1.30, 1.38, 1.55), (1.42, 1.36, 1.45), (1.50, 1.25, 1.35),
                              (1.56, 1.10, 1.20), (1.60, 1.0, 1.0)], hand_len=1.18, extra=hunch)
B = c.B

# ------------------------------------------------------------------ source materials
mats.fur("bear_pelt", base=(0.030, 0.024, 0.019), tip=(0.155, 0.125, 0.095), locks=3.2)
mats.fur("muzzle_fur", base=(0.070, 0.050, 0.034), tip=(0.30, 0.24, 0.17), locks=0.4)
mats.bone("claw", base=(0.135, 0.115, 0.095), stain=(0.03, 0.025, 0.02))
mats.bone("fang", base=(0.52, 0.47, 0.36))
mats.leather("nose", base=(0.018, 0.015, 0.014), rough=(0.35, 0.6), ash=0.0)
mats.fur("paw_pelt", base=(0.030, 0.024, 0.019), tip=(0.155, 0.125, 0.095), locks=0.0)
mats.emis("spirit", (0.36, 1.0, 0.22), strength=3.2)

# ------------------------------------------------------------------ pelt over the whole reshaped body
def fur_off(co):
    hands = (np.abs(co[:, 0]) > 0.735).astype(float)
    feet = 1 - sstep(co[:, 2], 0.06, 0.14)
    o = 0.018 + 0.022 * sstep(co[:, 2], 1.22, 1.45) * sstep(co[:, 1], 0.0, 0.14)
    return o * (1 - hands) * (1 - feet) + 0.006 * hands + 0.008 * feet * (1 - hands)
LOW = B.w("foot", "ball", "calf") > 0.5
fur = shell(B, (B.w("Head") < 0.5) & ~(LOW & (B.z < 0.085)), fur_off, "pelt", "cloth", "bear_pelt", smooth=2, sharp=180)
paws = shell(B, LOW & (B.z < 0.135), fur_off, "paws", "cloth", "paw_pelt", smooth=2, sharp=180)
co = get_co(paws)
co[:, 2] = np.maximum(co[:, 2], 0.0)
set_co(paws, co)
displace_noise(fur, 0.008, 20.0, 3)
co = get_co(fur)
co[:, 2] = np.maximum(co[:, 2], 0.0)
set_co(fur, co)
drop_skin(c)

# shoulder hump, shoulder mounds, chest ruff, tail stub
hump = fur_blob("hump", (0.0, 0.070, 1.575), (0.300, 0.215, 0.185), "bear_pelt", {"spine_03": 0.7, "neck_01": 0.3}, noise=0.016, scale=18.0, seed=2, nu=14, nv=9)
shl = fur_blob("shoulder_l", (0.262, 0.035, 1.505), (0.178, 0.185, 0.158), "bear_pelt", {"clavicle_l": 0.55, "upperarm_l": 0.45}, noise=0.014, scale=20.0, seed=4)
shr = mirrored(shl)
fur_blob("chest_ruff", (0.0, -0.150, 1.400), (0.200, 0.090, 0.170), "bear_pelt", "spine_03", noise=0.012, scale=22.0, seed=6)
belly = fur_blob("belly", (0.0, 0.000, 1.030), (0.292, 0.246, 0.300), "bear_pelt", {"pelvis": 0.65, "spine_01": 0.35}, noise=0.014, scale=18.0, seed=7,
                 nu=14, nv=9)
fur_blob("tail", (0.0, 0.270, 0.985), (0.055, 0.055, 0.048), "bear_pelt", "pelvis", noise=0.008, scale=30.0, seed=8, nu=8, nv=6)

# ------------------------------------------------------------------ bear head (rigid to Head), thrust forward under the hump
HC = np.array((0.0, -0.135, 1.640))
skull = fur_blob("skull", HC, (0.150, 0.165, 0.138), "bear_pelt", "Head", noise=0.008, scale=26.0, seed=9, nu=14, nv=9)
MD = np.array((0.0, -1.0, -0.20))
MO = HC + np.array((0.0, -0.120, -0.034))
head = [
    (muzzle_g(MO, MD, [(0.0, 0.094, 0.072, 0.068), (0.05, 0.082, 0.064, 0.054), (0.10, 0.068, 0.054, 0.042), (0.138, 0.055, 0.046, 0.035)], nu=14, nv=6),
     "cloth", "muzzle_fur", dict(sharp=180)),
    (muzzle_g(HC + np.array((0.0, -0.100, -0.092)), (0.0, -1.0, -0.44), [(0.0, 0.072, 0.036, 0.030), (0.06, 0.058, 0.028, 0.024), (0.118, 0.042, 0.022, 0.018)],
              nu=12, nv=5), "cloth", "bear_pelt", dict(sharp=180)),
    (gxf(g_pillow(0.052, 0.085, 0.022, e=1.0, nu=8, nv=5), T(*(HC + np.array((0.0, -0.185, -0.078)))) @ R('X', 14)), "leather", "nose", dict(sharp=180)),
    (gxf(g_pillow(0.033, 0.024, 0.023, e=0.8, nu=8, nv=6), T(*(MO + MD / np.linalg.norm(MD) * 0.140 + np.array((0, 0, 0.022))))), "leather", "nose", dict(sharp=180)),
]
for s in (1, -1):
    head += [
        (gxf(g_pillow(0.050, 0.022, 0.050, e=1.0, nu=10, nv=6), T(s * 0.118, HC[1] + 0.045, HC[2] + 0.128) @ R('Z', -18 * s) @ R('Y', 16 * s)), "cloth", "bear_pelt", dict(sharp=180)),
        (gxf(g_pillow(0.030, 0.010, 0.030, e=1.0, nu=8, nv=5), T(s * 0.118, HC[1] + 0.026, HC[2] + 0.126) @ R('Z', -18 * s) @ R('Y', 16 * s)), "leather", "nose", dict(sharp=180)),
        (gxf(g_sphere(1.0, 8, 6), T(s * 0.066, HC[1] - 0.132, HC[2] + 0.030) @ S(0.0135, 0.010, 0.0115)), "emis", "spirit", dict(sharp=180)),
        (tuft((s * 0.060, HC[1] - 0.128, HC[2] + 0.058), (s * 0.5, -0.9, -0.05), 0.060, 0.020, 0.012), "cloth", "bear_pelt", dict(sharp=180)),
        (gxf(g_tooth(0.036, 0.0095, 0.008, 5), T(s * 0.034, MO[1] - 0.118, MO[2] - 0.052) @ R('X', 180) @ R('Z', 90)), "bone", "fang", dict(sharp=40)),
        (gxf(g_tooth(0.028, 0.0085, 0.006, 5), T(s * 0.027, HC[1] - 0.198, HC[2] - 0.118) @ R('Z', 90)), "bone", "fang", dict(sharp=40)),
    ]
    for k in range(3):
        head.append((tuft((s * (0.128 - 0.010 * k), HC[1] - 0.02 + 0.035 * k, HC[2] - 0.045 - 0.022 * k), (s * 0.8, -0.05, -0.6), 0.085 + 0.02 * k, 0.030, 0.02),
                     "cloth", "bear_pelt", dict(sharp=180)))
props("head", np.eye(4), head, "Head")

# ------------------------------------------------------------------ claws, shaggy clumps
hand_claws(c, "claw", length=0.085, radius=0.0175, back=0.016)
foot_claws(c, n=4, src="claw", length=0.070, radius=0.017)
scatter_tufts(fur, 520, "bear_pelt", "tufts", length=(0.07, 0.13), radius=(0.010, 0.018), sides=3, seed=2, gravity=0.72, jitter=0.3,
              mask=lambda cen, nrm: (np.abs(cen[:, 0]) < 0.72) & (cen[:, 2] > 0.16), flow=beast_flow)
scatter_tufts(fur, 110, "bear_pelt", "arm_fringe", length=(0.10, 0.17), radius=(0.010, 0.018), sides=3, seed=5, gravity=0.7,
              mask=lambda cen, nrm: (np.abs(cen[:, 0]) > 0.40) & (np.abs(cen[:, 0]) < 0.72) & (nrm[:, 2] < -0.25),
              flow=lambda p: np.array((0.25 if p[0] > 0 else -0.25, 0.2, -1.0)))
for hb, sd in ((hump, 11), (shl, 12), (shr, 13)):
    scatter_tufts(hb, 70 if hb is hump else 45, "bear_pelt", hb.name + "_tufts", length=(0.06, 0.11), radius=(0.010, 0.018), sides=3, seed=sd, gravity=0.8, jitter=0.3,
                  flow=lambda p: np.array((0.0, 0.75, -0.65)))
scatter_tufts(belly, 60, "bear_pelt", "belly_tufts", length=(0.07, 0.12), radius=(0.010, 0.018), sides=3, seed=14, gravity=0.8, jitter=0.3,
              mask=lambda cen, nrm: cen[:, 2] < 1.0)

# ------------------------------------------------------------------ spirit markings
def inv_v(w, h, t):
    return [(-w, -h), (0.0, h), (w, -h), (w - t, -h), (0.0, h - 1.6 * t), (-w + t, -h)]
spirit_mark(skull, "mark_brow", rhomb(0.016, 0.034), (0.0, -0.8, HC[2] + 0.085), (0, 1, -0.15), up=(0, 0, 1), bone="Head")
for s in (1, -1):
    spirit_mark(skull, "mark_brow_%d" % s, bar(0.006, 0.030), (s * 0.052, -0.8, HC[2] + 0.100), (0, 1, -0.15), up=(s * 0.45, 0, 1), bone="Head")
    sh = shl if s > 0 else shr
    for k, (dx, w) in enumerate(((-0.030, 0.062), (0.040, 0.050), (0.100, 0.036))):
        spirit_mark(sh, "mark_sh_%d_%d" % (s, k), chevron(w, w * 0.62, 0.017), (s * (0.262 + dx), 0.02, 2.2), (0, 0, -1), up=(-s, 0, 0))
    for k, x in enumerate((0.565, 0.665)):
        ring = [(s * x, 0.068 + 0.26 * math.cos(a), 1.44 + 0.26 * math.sin(a)) for a in np.linspace(0, TAU, 16, endpoint=False)]
        ribbon(c, ring, "spirit", width=0.020, thick=0.005, offset=0.008, name="mark_arm_%d_%d" % (s, k), cls="emis", closed=True, step=0.03, host=fur,
               wsmooth=0, sharp=180)
    spirit_mark(fur, "mark_thigh_%d_a" % s, inv_v(0.045, 0.030, 0.015), (s * 0.6, 0.0, 0.80), (-s, 0, 0), up=(0, 0, 1))
    spirit_mark(fur, "mark_thigh_%d_b" % s, bar(0.030, 0.008), (s * 0.6, 0.0, 0.725), (-s, 0, 0), up=(0, 0, 1))
spirit_mark(fur, "mark_chest_a", rhomb(0.026, 0.050), (0.0, -0.8, 1.215), (0, 1, 0))
for s in (1, -1):
    spirit_mark(fur, "mark_chest_%d" % s, [(0.0, 0.040), (s * 0.090, 0.075), (s * 0.090, 0.055), (0.0, 0.020)], (s * 0.03, -0.8, 1.215), (0, 1, 0))
spirit_line(c, fur, "mark_spine", [(0.0, 0.6, 1.36), (0.0, 0.6, 1.24), (0.0, 0.6, 1.12), (0.0, 0.6, 1.02)], width=0.022)
for k, z in enumerate((1.30, 1.18, 1.07)):
    for s in (1, -1):
        spirit_line(c, fur, "mark_rib_%d_%d" % (k, s), [(s * 0.035, 0.6, z), (s * 0.090, 0.6, z - 0.035), (s * 0.150, 0.6, z - 0.035)], width=0.016)

report_unweighted()
tri_report(10)
finalize(c, CID, max_tris=35000,
         notes="Pose-tested: Melee_Hook, Punch_Cross, Zombie_Scratch, Jog_Fwd_Loop, Zombie_Idle_Loop. Beast proportions are mesh-only on the "
               "unmodified UAL rig; the bear head, hump and shoulder mounds are rigid / two-bone parts over a fully skinned pelt. No skin, no weapons; "
               "emis_ = moss-green spirit markings and eyes (same colour as hero_wildsoul).")
