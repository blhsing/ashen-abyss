"""npc_villager_f -- Lastlamp townswoman: long wool dress, stained apron, shawl, hair in buns."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from charlib import *

CID = "npc_villager_f"
c = start("female")
B = c.B

mats.cloth("dress", base=(0.055, 0.07, 0.085), folds=0.8, ash=0.7, ash_h=0.5, worn=0.5)
mats.cloth("apron", base=(0.15, 0.135, 0.105), folds=0.4, ash=0.5, worn=0.7, weave=2.0)
mats.cloth("shawl", base=(0.10, 0.05, 0.035), folds=0.5, ash=0.2, bands=[(0.0, 0.12, (0.05, 0.03, 0.025))])
mats.leather("boot", base=(0.045, 0.028, 0.018), ash=0.7, ash_h=0.3)
mats.leather("belt", base=(0.07, 0.04, 0.022))

# bodice + sleeves (shell), long skirt (loft)
shirt(c, "dress", bottom=1.04, top=1.47, sleeve=0.66, offset=0.010, smooth=4, name="bodice")
boots(c, "boot", top=0.30, offset=0.012)
skirt = robe(c, "dress",
             [(0.075, 0, 0.02, 0.305, 0.255, 0.285), (0.50, 0, 0.02, 0.255, 0.215, 0.235), (0.92, 0, 0.018, 0.205, 0.172, 0.178),
              (1.085, 0, -0.008, 0.142, 0.112, 0.116), (1.13, 0, -0.012, 0.130, 0.104, 0.108)],
             nu=32, nv=14, name="skirt", pleats=10, pleat_amp=0.04, thick=0.0, ragged=0.012, calf_to_thigh=0.55)
hide(c, (B.z > 0.33) & (B.z < 1.06) & (B.w(*BONES_ARM, *BONES_HAND) < 0.3))
# apron: front panel from the waist to below the knee
def apron_fn(U, V):
    z = 1.105 - V * 0.62
    w = 0.115 + 0.035 * V
    x = (U * 2 - 1) * w
    d = np.interp(z, [0.48, 0.92, 1.105], [0.228, 0.186, 0.118]) + 0.012 + 0.022 * V
    rx = np.interp(z, [0.48, 0.92, 1.105], [0.27, 0.215, 0.14])
    y = np.interp(z, [0.48, 0.92, 1.105], [0.02, 0.018, -0.008]) - d * np.sqrt(np.clip(1 - (x / rx) ** 2, 0.05, 1))
    return np.stack([x, y, z], axis=-1)
ap = add("apron", g_grid(apron_fn, 9, 10, close_u=False), "cloth", "apron", solidify=0.005, sharp=180, double_sided=True)
w_transfer(ap, skirt, smooth=1)
belt(c, 1.10, "belt", width=0.035, gap=0.026, thick=0.007)
# shawl over the shoulders (full ring, short)
def shawl_mod(U, V, x, y, z):
    # triangular shawl: long point at the back, two shorter points knotted at the front, short over the arms
    sn = np.sin(U * TAU)
    back = np.clip(sn, 0, 1) ** 2
    front = np.clip(-sn, 0, 1) ** 3
    return x, y, z - (0.16 * back + 0.07 * front) * (1 - V) ** 2
sh = robe(c, "shawl", [(1.235, 0, 0.0, 0.236, 0.196, 0.190), (1.33, 0, 0.004, 0.226, 0.186, 0.176), (1.42, 0, 0.012, 0.196, 0.158, 0.152),
                       (1.485, 0, 0.03, 0.118, 0.102, 0.112), (1.51, 0, 0.03, 0.090, 0.082, 0.098)],
          nu=30, nv=8, p=2.0, name="shawl", pleats=11, pleat_amp=0.05, thick=0.006, calf_to_thigh=0.0, ragged=0.014, mod=shawl_mod,
          only=["spine_03", "spine_02", "clavicle_l", "clavicle_r", "neck_01"])
knot = add("shawl_knot", gxf(g_sphere(0.03, 8, 5, sx=1.3, sy=0.8), T(0, -0.158, 1.335)), "cloth", "shawl", bone="spine_03", sharp=180)

add_hair(c, "Hair_Buns")

finalize(c, CID, hair_tint=(0.11, 0.065, 0.035), skin=dict(gain=0.95, sat=0.85),
         tex={"cloth": 1024, "leather": 512}, max_tris=20000, decimate={"cloth": 0.75, "leather": 0.7, "hair": 0.6},
         notes="Pose-tested: Idle_Loop, Idle_Talking_Loop, Walk_Loop, Interact. Long skirt is weighted to the thighs/shins.")
