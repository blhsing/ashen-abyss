"""npc_villager_m -- Lastlamp townsman: patched linen tunic, leather jerkin, wool hose, scarf."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from charlib import *

CID = "npc_villager_m"
c = start("male")
B = c.B

mats.cloth("linen", base=(0.135, 0.112, 0.08), folds=0.6, ash=0.5, worn=0.6)
mats.cloth("wool", base=(0.055, 0.05, 0.045), folds=0.5, ash=0.8, ash_h=0.6)
mats.cloth("scarf", base=(0.06, 0.085, 0.06), folds=0.3, ash=0.2)
mats.leather("jerkin", base=(0.085, 0.048, 0.025), worn=0.7)
mats.leather("boot", base=(0.04, 0.026, 0.017), ash=0.8, ash_h=0.35)
mats.metal("iron", base=(0.2, 0.19, 0.18), rough=(0.45, 0.7), rust_amt=0.4, ash=0.0)

# slimmer, less heroic build: narrow the shoulders / chest / arms a little through the mesh only
def slim(co, B):
    k = sstep(co[:, 2], 1.05, 1.30) * (1 - sstep(co[:, 2], 1.50, 1.56))
    arm = B.w(*BONES_ARM, *BONES_HAND)
    co[:, 1] = co[:, 1] * (1 - 0.10 * k * (1 - arm)) + 0.004 * k
    # thinner arms: pull towards the arm axis (y=0.068, z=1.44)
    a = np.clip(arm, 0, 1) * 0.14 * (1 - B.w(*BONES_HAND))
    co[:, 1] = co[:, 1] + (0.068 - co[:, 1]) * a
    co[:, 2] = co[:, 2] + (1.44 - co[:, 2]) * a
    return co
reshape_body(c, slim)

shirt(c, "linen", bottom=1.0, top=1.50, sleeve=0.64, offset=0.011, smooth=4, name="tunic_top")
trousers(c, "wool", top=1.03, bottom=0.30, offset=0.010, smooth=3)
boots(c, "boot", top=0.36, offset=0.013, cuff=0.006)
# tunic skirt below the belt
robe(c, "linen", [(0.74, 0, 0.02, 0.215, 0.165, 0.175), (0.90, 0, 0.02, 0.195, 0.150, 0.158), (1.045, 0, 0.012, 0.166, 0.124, 0.122)],
     nu=24, nv=6, name="tunic_skirt", pleats=7, pleat_amp=0.06, thick=0.005, ragged=0.012, calf_to_thigh=0.0)
# sleeveless leather jerkin
mj = (B.w(*BONES_TORSO, "clavicle") > 0.5) & (B.z > 1.04) & (B.z < 1.475) & (np.abs(B.x) < 0.205)
jer = shell(B, mj, 0.021, "jerkin", "leather", "jerkin", smooth=5, sharp=180, solidify=0.0)
belt(c, 1.045, "boot", width=0.045, gap=0.026, buckle="iron")
# pouch on the right hip
gp = g_box(0.075, 0.05, 0.09)
pouch = add("pouch", gxf(gp, T(-0.175, -0.045, 0.975) @ R('Z', -25)), "leather", "jerkin", bevel=0.012, bevel_seg=2, bone="pelvis", sharp=60)
# scarf / cowl roll round the neck
path = []
for i in range(16):
    a = TAU * i / 16
    s_, c_ = math.sin(a), math.cos(a)
    path.append((0.112 * c_, 0.028 + 0.105 * s_, 1.515 + (0.012 * s_ if s_ > 0 else 0.05 * s_)))
sc = fur_roll(path, 0.036, "scarf", name="scarf", noise=0.006, scale=40.0, n=8)
w_func(sc, lambda co: {"neck_01": 0.4, "spine_03": 0.6})

add_hair(c, "Hair_SimpleParted")
add_hair(c, "Hair_Beard")

finalize(c, CID, hair_tint=(0.075, 0.05, 0.035), skin=dict(gain=0.92, sat=0.8),
         tex={"cloth": 1024, "leather": 512, "metal": 256}, max_tris=20000, decimate={"cloth": 0.7, "leather": 0.7},
         notes="Pose-tested: Idle_Loop, Idle_Talking_Loop, Walk_Loop, Interact.")
