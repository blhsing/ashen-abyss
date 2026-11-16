"""Melee batch probe: actions present, hand frames in the clips the three heroes use, hand / body measurements."""
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit_mel import *

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
c = start(ARGS[0] if ARGS else "male")
arm = c.arm
B = c.B
acts = load_actions()
log("ACTIONS", ", ".join(sorted(acts.keys())))

# ---- rest-space measurements
for side in ("r", "l"):
    o, along, dorsal, thumb = hand_axes(side)
    log("HAND %s origin=%s along=%s dorsal=%s thumb=%s" % (side, np.round(o, 3), np.round(along, 2), np.round(dorsal, 2), np.round(thumb, 2)))
    HF = hand_frame(side)
    inv = np.linalg.inv(HF)
    for f in ("index", "middle", "ring", "pinky", "thumb"):
        for i in (1, 2, 3):
            b = arm.data.bones["%s_0%d_%s" % (f, i, side)]
            p = xf(np.array([b.head_local, b.tail_local]), inv)
            log("   %s_0%d head=%s tail=%s" % (f, i, np.round(p[0], 3), np.round(p[1], 3)))
    wh = B.W[:, B.col["hand_" + side]]
    m = wh > 0.6
    p = xf(B.co[m], inv)
    log("   hand verts (w>0.6) local bbox min=%s max=%s n=%d" % (np.round(p.min(0), 3), np.round(p.max(0), 3), m.sum()))
    fm = (B.w("index_", "middle_", "ring_", "pinky_") > 0.6) & ((B.x < 0) if side == "r" else (B.x > 0))
    p = xf(B.co[fm], inv)
    log("   finger verts local bbox min=%s max=%s" % (np.round(p.min(0), 3), np.round(p.max(0), 3)))
    tm = (B.w("thumb_") > 0.6) & ((B.x < 0) if side == "r" else (B.x > 0))
    p = xf(B.co[tm], inv)
    log("   thumb verts local bbox min=%s max=%s" % (np.round(p.min(0), 3), np.round(p.max(0), 3)))
    # hand cross sections along the hand
    allm = (B.w("hand", "index_", "middle_", "ring_", "pinky_", "thumb_", "lowerarm") > 0.6) & ((B.x < 0) if side == "r" else (B.x > 0))
    p = xf(B.co[allm], inv)
    for x in (-0.10, -0.06, -0.03, 0.0, 0.03, 0.06, 0.09, 0.12):
        s = np.abs(p[:, 0] - x) < 0.012
        if s.any():
            log("   sect x=%+.2f y=[%+.3f %+.3f] z=[%+.3f %+.3f]" % (x, p[s, 1].min(), p[s, 1].max(), p[s, 2].min(), p[s, 2].max()))

for z in (0.10, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90, 0.95, 1.0, 1.05, 1.10, 1.15, 1.20, 1.25, 1.30, 1.35, 1.40, 1.45, 1.50, 1.55, 1.60, 1.65,
          1.70, 1.75, 1.80, 1.84):
    s = B.slice(z, 0.015)
    if s:
        log("SLICE z=%.2f cx=%+.3f cy=%+.3f rx=%.3f ryf=%.3f ryb=%.3f" % ((z,) + tuple(s)))
# one leg sections
for z in (0.15, 0.30, 0.45, 0.53, 0.65, 0.80, 0.90):
    m = (np.abs(B.z - z) < 0.015) & (B.x > 0) & (B.w("thigh_l", "calf_l", "foot_l") > 0.5)
    if m.any():
        log("LEG z=%.2f x=[%.3f %.3f] y=[%+.3f %+.3f]" % (z, B.x[m].min(), B.x[m].max(), B.y[m].min(), B.y[m].max()))
for x in (0.22, 0.26, 0.30, 0.36, 0.42, 0.47, 0.52, 0.58, 0.64, 0.70, 0.74):
    m = (np.abs(B.x - x) < 0.012) & (B.w(*BONES_ARM, *BONES_HAND) > 0.5)
    if m.any():
        log("ARM x=%.2f y=[%+.3f %+.3f] z=[%.3f %.3f]" % (x, B.y[m].min(), B.y[m].max(), B.z[m].min(), B.z[m].max()))
hm = B.w("Head") > 0.6
log("HEAD bbox min=%s max=%s" % (np.round(B.co[hm].min(0), 3), np.round(B.co[hm].max(0), 3)))
for z in (1.56, 1.60, 1.64, 1.68, 1.72, 1.76, 1.80, 1.84):
    m = hm & (np.abs(B.z - z) < 0.012)
    if m.any():
        log("HEADSEC z=%.2f x=[%+.3f %+.3f] y=[%+.3f %+.3f]" % (z, B.x[m].min(), B.x[m].max(), B.y[m].min(), B.y[m].max()))
for bn in ("Head", "neck_01", "spine_03", "spine_02", "spine_01", "pelvis", "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l", "thigh_l",
           "calf_l", "foot_l", "ball_l"):
    b = arm.data.bones[bn]
    log("REST %-12s head=(%+.3f %+.3f %.3f) tail=(%+.3f %+.3f %.3f)" % (bn, *b.head_local, *b.tail_local))

if len(ARGS) > 1:
    sys.exit(0)
# ---- poses
tests = [("Pistol_Aim_Neutral", 0.5), ("Pistol_Shoot", 0.2), ("Pistol_Shoot", 0.6), ("Pistol_Idle_Loop", 0.5), ("Pistol_Reload", 0.5),
         ("Sword_Regular_Combo", 0.15), ("Sword_Regular_Combo", 0.4), ("Sword_Regular_Combo", 0.7), ("Sword_Dash", 0.3), ("Sword_Dash", 0.6),
         ("Sword_Regular_A", 0.3), ("Sword_Regular_A", 0.55), ("Sword_Idle", 0.5), ("Spell_Simple_Shoot", 0.45), ("Punch_Jab", 0.45),
         ("Punch_Cross", 0.45), ("Melee_Hook", 0.5), ("Idle_Loop", 0.5), ("Jog_Fwd_Loop", 0.3), ("Roll", 0.45)]
GA = {"r": Vector((-0.04, 0.17, 0.99)).normalized(), "l": Vector((0.04, 0.17, 0.99)).normalized()}
fingers = ("index", "middle", "ring", "pinky")
PG = {s: pistol_grip(s) for s in ("r", "l")}
for name, t in tests:
    if name not in acts:
        log("MISSING", name)
        continue
    set_pose(arm, acts[name], t)
    for side in ("r", "l"):
        hb = arm.pose.bones["hand_" + side]
        R3 = hb.matrix.to_3x3()
        curl = []
        for f in fingers:
            pts = [arm.pose.bones["%s_0%d_%s" % (f, i, side)].head for i in (1, 2, 3)] + [arm.pose.bones["%s_03_%s" % (f, side)].tail]
            chain = sum((pts[i + 1] - pts[i]).length for i in range(3))
            curl.append((pts[3] - pts[0]).length / chain)
        ax = (R3 @ GA[side]).normalized()
        fw = (R3 @ Vector((0, 1, 0))).normalized()
        P = np.array([list(r) for r in hb.matrix]) @ np.linalg.inv(bone_frame("hand_" + side)) @ PG[side]
        la = arm.pose.bones["lowerarm_" + side]
        fa = (la.tail - la.head).normalized()
        h = hb.head
        log("%-20s t=%.2f %s curl=%.2f wrist=(%+.2f %+.2f %.2f) tunnel=(%+.2f %+.2f %+.2f) along=(%+.2f %+.2f %+.2f) forearm=(%+.2f %+.2f %+.2f) pistol_barrel=(%+.2f %+.2f %+.2f) pistol_up=(%+.2f %+.2f %+.2f)" % (
            name, t, side, sum(curl) / 4, h.x, h.y, h.z, ax.x, ax.y, ax.z, fw.x, fw.y, fw.z, fa.x, fa.y, fa.z,
            P[0, 1], P[1, 1], P[2, 1], P[0, 2], P[1, 2], P[2, 2]))

