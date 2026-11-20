"""asm_parts10.py -- weak-point pass of 2026-11-20 (after the daylight line-ups in the game):
   mute_chest      the Mute's torso: a slab chest between big sloping shoulders with a ROUND see-through hole
   prow_plough     the ice-breaker's plough as a prow in front of the body (on the hips, the left arm holds its grip bar)
   brazier_hulk2   the brazier-hulk with a real frozen fire inside the basket, cabriole legs, rimed bars, more ice
   drift_mass2     the drift-colossus: pale casts with hollow faces bedded in a darker drift, sparks in every crease
Same rules as asm_parts.py / asm_parts8.py.  New assembler features used here: A.add(..., sway=(low bone, t)) and body
bind_w (see asm.py)."""
import math
import numpy as np
import monlib as M
import kit_bones as KB
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_c as KC3
import kit_wisp as KW
import kit_ice as KI
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, hang, arc_pts, band, unit, blade
from asm_parts import part, girth_of, cyl, item_parts
from asm_parts2 import z_weights, ring_disc
from asm_parts4 import bark_mod, limb_tube, flame_blade
from asm_parts9 import sway_weights, place, bar, arm_bones, rad
import asm

asm.PRE.update(
    ice_deep=asm._p('crystal', 'rock', color=(0.05, 0.13, 0.32), deep=(0.008, 0.03, 0.12), scale=10.0, crack=18.0, frost_amt=0.15),
    ashmass=asm._p('rock', 'rock', color=(0.115, 0.11, 0.105), ash=(0.30, 0.295, 0.29), ash_amt=0.7, scale=14.0, crack_w=0.04, rough=0.96),
)

SPINE_STOPS = [(0.95, 'pelvis'), (1.10, 'spine_01'), (1.25, 'spine_02'), (1.40, 'spine_03')]


def spine_scale(A, k):
    """z -> scale (k = 0: x, 1: y) that the creature's proportions give a mesh skinned by height to the spine"""
    full = asm.merge_pose(A.pose, A.post)
    vals = []
    for _, b in SPINE_STOPS:
        s = full.get(b, {}).get('scale')
        s = np.ones(3) if s is None else np.broadcast_to(np.asarray(s, float), (3,))
        vals.append(float(s[k]))
    zs = [z for z, _ in SPINE_STOPS]
    return lambda z: float(np.interp(z, zs, vals))


def spine_weights(p):
    return M.weights_by(p, p.v[:, 2], [(z, {b: 1.0}) for z, b in SPINE_STOPS])


# ================================================================================================ hush: the Mute
@part
def mute_chest(A, look='chalk', hole=0.095, zc=1.27, power=2.7, cap=0.135, n=56):
    """the headless giant's upper body: a slab of a chest NARROWER than the shoulder joints (so that the arms hang free
    beside it instead of being buried in it), big sloping shoulder masses on the upper arms, the shoulders rising into a
    low smooth dome where a neck would be -- and one ROUND hole clean through the chest: a real tunnel with a crisp edge
    and pale walls that the level shows through.  The section is designed in FINAL space and divided by the scale the
    creature's bulk gives each height, so the hole stays a circle (it used to be stretched into a wide oval: a mouth).
    Use with body hide = spine_02 / spine_03 / clavicles / neck / head."""
    R = A.R
    sxz = spine_scale(A, 0); syz = spine_scale(A, 1)
    key = np.array([(0.84, 0.020, 0.120, 0.095), (0.92, 0.020, 0.175, 0.135), (1.02, 0.020, 0.186, 0.150), (1.12, 0.018, 0.190, 0.146), (1.22, 0.016, 0.200, 0.134),
                    (1.32, 0.016, 0.208, 0.132), (1.42, 0.016, 0.215, 0.140), (1.49, 0.014, 0.205, 0.140), (1.545, 0.012, 0.175, 0.128),
                    (1.59, 0.010, 0.132, 0.105), (1.625, 0.008, 0.08, 0.068)])       # z, cy, FINAL rx, FINAL ry

    def sec(z):                                            # rest-space section at height z
        cy, rx, ry = (float(np.interp(z, key[:, 0], key[:, k])) for k in (1, 2, 3))
        return cy, rx / sxz(z), ry / syz(z)

    pw = lambda z: float(np.interp(z, [0.95, 1.12, 1.42, 1.59], [2.0, power, power, 2.0]))     # round at the hips, a slab at the chest, a round dome above
    zrows = np.unique(np.round(np.r_[key[:, 0], np.linspace(zc - hole * 1.4, zc + hole * 1.4, 15)], 4))
    t = M.loft_z([(z, 0) + sec(z) + (pw(z),) for z in zrows], n=n, cap0=False, cap1=True)
    sx_v = np.array([sxz(z) for z in t.v[:, 2]])
    dx = t.v[:, 0] * sx_v; dz = t.v[:, 2] - zc             # the hole is a circle in FINAL space
    d = np.hypot(dx, dz)
    ins = d < hole
    k = hole / np.maximum(d, 1e-6)
    nx = np.where(d > 1e-6, dx * k, hole); nz = np.where(d > 1e-6, dz * k, 0.0)
    znew = zc + nz
    t.v[ins, 2] = znew[ins]
    t.v[ins, 0] = (nx / np.array([sxz(z) for z in znew]))[ins]
    t = t.sub(~t.vmask_faces(ins, 'all'))
    spine_weights(t)
    A.add(t, look=look, smooth=180)
    m = 32
    fr = []; bk = []
    for ak in np.linspace(0, 2 * math.pi, m, endpoint=False):       # where the hole's edge meets the front / back surface
        z = zc + hole * math.sin(ak)
        x = hole * math.cos(ak) / sxz(z)
        cy, rx, ry = sec(z)
        p_ = pw(z)
        dy = ry * max(0.0, 1.0 - abs(x / rx) ** p_) ** (1.0 / p_)
        fr.append((x, cy - dy, z)); bk.append((x, cy + dy, z))
    fr = np.array(fr); bk = np.array(bk)
    tun = M.loft(np.array([fr + (bk - fr) * s for s in (-0.03, 0.3, 0.7, 1.03)]), cap0=False, cap1=False).flip()
    tun.smooth = 180.0
    A.add(tun, look=look, like=t)                          # pale walls: a tunnel, not a dark mouth
    for sx in (1, -1):                                     # the shoulder masses: on the upper arms, so the arms hang from them
        s = '_l' if sx > 0 else '_r'
        sh = R.H('upperarm' + s)
        c = ellipsoid(cap, cap * 0.93, cap * 0.9, n=14, m=8).move(sh + V(sx * 0.035, 0.006, -0.014))
        A.add(c, bone='upperarm' + s, look=look, smooth=180, keep=True)
        A.add(ellipsoid(cap * 0.8, cap * 0.8, cap * 0.62, n=12, m=6).move(sh + V(-sx * 0.05, 0.008, 0.035)), bone='spine_03', look=look, smooth=180, keep=True)


# ================================================================================================ frost: the prow
@part
def prow_plough(A, look='oak', rim='rust', rope='rope', y_s=-0.60, z0=0.12, z1=1.36, half=40.0, w_l=0.46, w_r=0.42, rake=0.05, bone='pelvis',
                arm='l'):
    """the snow-plough as the prow of a ship IN FRONT of its bearer: two plank boards meeting in an iron-shod stem, the top
    edge falling away to the rear (the chest can lean over it), an iron cutting edge along the bottom.  It is carried on the
    hips (rigid on `bone`: the pelvis hardly tips in the clips, so the prow stays a prow while the pick arm swings) by two
    ropes to the belt, and the left arm reaches into it and holds the grip bar: give that arm a baked pose (props pose) and
    body bind_w = {'spine_02': ['upperarm_l'], 'pelvis': ['lowerarm_l', 'hand_l']} so that it stays on the bar.  The bar
    is put through wherever the posed wrist is."""
    R = A.R
    K = R.H(bone)
    full = asm.merge_pose(A.pose, A.post)
    wr = M.posed_point(full, 'hand_' + arm, R.H('hand_' + arm))[0]
    M.log("prow_plough: posed wrist at", np.round(wr, 3))
    down = V(0, 0, -1)
    H_ = z1 - z0
    wood = A._wood('wood_prow', (0, 0, 1), look)
    stem_top = V(0.02, y_s - rake, z1); stem_bot = V(0.02, y_s, z0)
    sdir = unit(stem_bot - stem_top)

    def add(p, lk, sm=30):
        A.add(p, bone=bone, look=lk, smooth=sm, keep=K)

    rear = {}
    for sgn, wd in ((1.0, w_l), (-1.0, w_r)):
        d = unit(V(sgn * math.sin(rad(half)), math.cos(rad(half)), 0.0))
        nrm = np.cross(sdir, d)
        bd = box(H_, wd, 0.038, bevel=0.006)
        x = bd.v[:, 0].copy(); tt = np.clip((bd.v[:, 1] + wd / 2) / wd, 0, 1)
        bd.v[:, 0] = np.where(x < 0, x + 0.34 * tt, x - 0.24 * tt)             # top edge falls to the rear, rear lower corner cut (clears the coat's hem)
        mid = (stem_top + stem_bot) / 2
        add(place(bd, (sdir, d, nrm), mid + d * (wd / 2)), wood)
        for u in (0.33, 0.66):                              # joints between the three planks of a board
            seam = box(H_ * (0.92 - 0.3 * u), 0.008, 0.044)
            add(place(seam, (sdir, d, nrm), mid + sdir * (0.10 * u) + d * (wd * u)), 'void')
        for tz in (0.30, 0.74):                             # iron straps across the planks, riveted
            st = box(0.05, wd * 0.98, 0.054, bevel=0.004)
            c = stem_top + sdir * (H_ * tz) + d * (wd * 0.5)
            add(place(st, (sdir, d, nrm), c), rim)
            for u in (0.18, 0.5, 0.82):
                add(KC3.rivet(stem_top + sdir * (H_ * tz) + d * (wd * u) + nrm * (0.027 * sgn), nrm * sgn, r=0.012, n=6), rim)
        edge = box(0.05, wd * 0.96, 0.05, bevel=0.006)      # the cutting edge
        ev = edge.v.copy(); et = np.clip((ev[:, 1] + wd * 0.48) / (wd * 0.96), 0, 1)
        edge.v[:, 0] -= 0.24 * et
        add(place(edge, (sdir, d, nrm), stem_bot + d * (wd * 0.5) - sdir * 0.02), rim)
        rear[sgn] = (stem_top + sdir * (H_ * 0.5) + d * wd, d)
    stem = box(H_ * 1.04, 0.09, 0.08, bevel=0.016)
    add(place(stem, (sdir, V(0, -1, 0), np.cross(sdir, V(0, -1, 0))), (stem_top + stem_bot) / 2 + V(0, -0.012, 0)), rim)
    add(M.cone_spike(0.04, 0.12, n=5).move(stem_top + V(0, -0.01, 0.0)), rim)          # the stem's iron finial
    # the grip bar through the posed wrist, and a lower brace
    def span(y, z):                                         # the inner faces of the two boards at depth y
        t_ = max(0.02, y - y_s)
        return V(t_ * math.tan(rad(half)) - 0.01, y, z), V(-t_ * math.tan(rad(half)) + 0.01, y, z)
    gy = float(np.clip(wr[1], y_s + 0.16, y_s + w_r * math.cos(rad(half)) - 0.03))
    a_, b_ = span(gy, wr[2] - 0.03)
    add(cyl(a_, b_, 0.022, n=7), wood, 40)
    a2, b2 = span(y_s + 0.24, z0 + 0.34)
    add(cyl(a2, b2, 0.02, n=6), wood, 40)
    for k in range(3):                                      # the wrist lashed to the bar
        add(torus(0.05 - 0.004 * k, 0.009, n=10, m=4).rot('x', 90).move(wr[0] + 0.03 * (k - 1), gy, wr[2] - 0.03), rope, 50)
    # two ropes from the boards' rear edges to the belt
    for sgn in (1.0, -1.0):
        p0, d = rear[sgn]
        p0 = V(p0[0], p0[1], 1.0)
        on = 'robe' if 'robe' in A.refs else 'body'
        cx, cy, rx, ry = girth_of(A, on, 1.07)              # rest-space girth; the bulk of the bearer widens it
        sx_ = spine_scale(A, 0)(1.07); sy_ = spine_scale(A, 1)(1.07)
        p1 = V(sgn * rx * sx_ * 0.90, cy - ry * sy_ * 0.42, 1.07)
        add(tube(KB2.drape_path(p0 - d * 0.03, p1, sag=0.05, n=7), 0.012, n=5), rope, 60)
        add(torus(0.03, 0.008, n=8, m=4).rot('y', 90).move(p0 - d * 0.03), rim, 40)


# ================================================================================================ coldfire: the hulk
@part
def brazier_hulk2(A, ice='ice', ice2='ice2', deep='ice_deep', iron='iron_black', core='coldfire', seed=4):
    """a dead beacon's iron brazier -- a basket of bars a metre wide -- worn as a ribcage by a hulking body of blue ice that
    grew up through it.  Inside the bars the frozen fire still stands: a dark heart of ice with glowing tongues rising from
    the basket's floor (emis_), clear plates grown over it; the fire erupts from the basket top as a crown of glass tongues
    where a head would be; icicles hang from the hoops; the brazier's three cabriole legs stick out of the back; arms are
    rough ice columns ending in fused clubs.  Faceted like the cold-flame spirit.  Use with a hidden body (show=[])."""
    R = A.R
    rng = np.random.default_rng(seed)
    FA = 18
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        limb_tube(A, ['thigh' + s, 'calf' + s], [0.15, 0.122, 0.10], ice, n=7, wob=0.016, seed=seed + (sx > 0), smooth=FA)
        hp = R.H('thigh' + s); kn = R.H('calf' + s); an = R.H('foot' + s)
        A.add(KC3.ice_chunk(0.125, 0.135, 0.19, seed=80 + (sx > 0), n=13).move((hp + kn) / 2 + V(sx * 0.03, -0.035, 0.02)), bone='thigh' + s, look=ice2, smooth=FA)
        A.add(KC3.ice_chunk(0.10, 0.11, 0.16, seed=84 + (sx > 0), n=12).move((kn + an) / 2 + V(sx * 0.02, -0.03, 0.03)), bone='calf' + s, look=ice, smooth=FA)
        A.add(KC3.ice_chunk(0.12, 0.185, 0.085, seed=seed + 3 + (sx > 0), n=14).move(sx * 0.092, -0.05, 0.078), bone='foot' + s, look=ice, smooth=FA)
        for c_ in KI.cluster(kn + V(sx * 0.05, -0.08, 0.0), axis=(sx * 0.5, -0.7, 0.6), count=5, spread=42.0, h=(0.07, 0.18), r=(0.022, 0.042), seed=seed + 11 + (sx > 0)):
            A.add(c_, bone='calf' + s, look=ice2, smooth=FA)
    A.add(KC3.ice_chunk(0.26, 0.21, 0.17, seed=seed, n=16).move(0, 0.02, 0.985), bone='pelvis', look=ice, smooth=FA)
    for c_ in KI.fringe(KI.line_pts(V(-0.2, -0.16, 0.93), V(0.2, -0.16, 0.93), 6, jit=0.02, seed=3), (0, 0, -1), 0.16, r=0.02, seed=5):
        A.add(c_, bone='pelvis', look=ice2, smooth=FA)
    # ---- the frozen fire inside the basket
    heart = M.loft_z([(0.99, 0, 0.02, 0.13, 0.115), (1.10, 0, 0.02, 0.185, 0.16), (1.24, 0, 0.02, 0.20, 0.17), (1.36, 0, 0.02, 0.15, 0.13), (1.46, 0, 0.015, 0.07, 0.06)],
                      n=8, phase=0.2)
    heart.rough(0.014, 9.0, seed=2.0)
    z_weights(heart)
    A.add(heart, look=deep, smooth=FA)
    for k in range(10):                                             # glowing tongues standing round the heart
        a = 2 * math.pi * (k + 0.3) / 10
        L_ = rng.uniform(0.34, 0.6)
        bl = flame_blade(L_, rng.uniform(0.07, 0.10), th=0.045, curl=0.22, n=8, seed=30 + k)
        bl.rot('z', math.degrees(a) + rng.uniform(-25, 25))
        rr = rng.uniform(0.17, 0.235)
        bl.move(rr * math.cos(a), 0.02 + rr * 0.86 * math.sin(a), 1.03 + rng.uniform(0.0, 0.08))
        z_weights(bl)
        A.add(bl, look=core, smooth=FA)
    for k in range(4):                                              # clear plates grown over it, the fire shows between them
        a = 2 * math.pi * k / 4 + 0.9
        pl = KC3.ice_chunk(0.10, 0.04, 0.13, seed=30 + k, n=10)
        zc = 1.14 + 0.12 * (k % 2)
        rr = float(np.interp(zc, [1.0, 1.28, 1.5], [0.21, 0.30, 0.26]))
        pl.rot('z', math.degrees(a) + 90).move(rr * math.cos(a), 0.02 + rr * 0.86 * math.sin(a), zc)
        z_weights(pl)
        A.add(pl, look=ice2, smooth=FA)
    # ---- the brazier basket
    K = V(0, 0.02, 1.27)
    bone = 'spine_02'
    prof = [(0.215, 1.00), (0.30, 1.10), (0.365, 1.26), (0.385, 1.42), (0.395, 1.52)]
    nb = 12
    for k in range(nb):
        a = 2 * math.pi * (k + 0.5) / nb
        pts = [V(rr * math.cos(a), 0.02 + rr * 0.86 * math.sin(a), zz) for rr, zz in prof]
        A.add(tube(M.polyline(pts, 8), 0.011, n=4, r2=0.02, up=(math.cos(a), math.sin(a), 0), phase=math.pi / 4), bone=bone, look=iron, smooth=30, keep=K)
        A.add(M.cone_spike(0.014, 0.055, n=4).move(pts[-1] + V(0, 0, 0.02)), bone=bone, look=iron, smooth=30, keep=K)          # finials on the rim
    for (rr, zz, tk) in ((0.215, 1.00, 0.018), (0.365, 1.26, 0.016), (0.395, 1.52, 0.024)):
        A.add(torus(rr, tk, n=24, m=5, ry=rr * 0.86).move(0, 0.02, zz), bone=bone, look=iron, smooth=40, keep=K)
    A.add(revolve([(0.0, 0.985), (0.20, 0.985), (0.215, 1.0), (0.0, 1.005)], n=14).scale((1, 0.86, 1)).move(0, 0.02, 0), bone=bone, look=iron, smooth=40, keep=K)   # the pan
    for k in range(nb):                                             # rivet heads where bars cross the middle hoop
        a = 2 * math.pi * (k + 0.5) / nb
        A.add(KC3.rivet(V(0.383 * math.cos(a), 0.02 + 0.383 * 0.86 * math.sin(a), 1.26), unit(V(math.cos(a), math.sin(a), 0)), r=0.014, n=6), bone=bone, look=iron, keep=K)
    for hz, hr, cnt, L_ in ((1.245, 0.372, 9, 0.15), (0.99, 0.215, 7, 0.12)):      # icicles hanging from the hoops
        pts = [V(hr * math.cos(a), 0.02 + hr * 0.86 * math.sin(a), hz) for a in np.linspace(0.3, 2 * math.pi + 0.3, cnt, endpoint=False)]
        for c_ in KI.fringe(pts, (0, 0, -1), L_, r=0.016, seed=7 + cnt):
            A.add(c_, bone=bone, look=ice2, smooth=FA, keep=K)
    for k, a_deg in enumerate((90.0, 36.0, 144.0)):                 # the brazier's three cabriole legs, sticking out of the back
        a = rad(a_deg)
        b0 = V(0.24 * math.cos(a), 0.02 + 0.24 * 0.86 * math.sin(a), 1.03)
        dd = unit(V(0.8 * math.cos(a), 1.0, 0.0))
        up = V(0, 0, 1)
        path = M.polyline([b0 - dd * 0.04, b0 + dd * 0.14 + up * 0.07, b0 + dd * 0.30 + up * 0.03, b0 + dd * 0.42 - up * 0.05, b0 + dd * 0.50 - up * 0.02], 10)
        A.add(tube(path, np.linspace(0.036, 0.02, 10), n=6), bone=bone, look=iron, smooth=40, keep=K)
        A.add(ellipsoid(0.05, 0.05, 0.03, n=8, m=5).tf(M.rot_to((0, 0, 1), dd)).move(b0 + dd * 0.52 - up * 0.02), bone=bone, look=iron, smooth=40, keep=K)   # the paw
        A.add(torus(0.04, 0.012, n=8, m=4).tf(M.rot_to((0, 0, 1), dd)).move(b0 + dd * 0.0), bone=bone, look=iron, smooth=40, keep=K)
    # ---- the crown of frozen flame where a head would be
    nt = 12
    for k in range(nt):
        a = 2 * math.pi * (k + 0.5) / nt
        L_ = rng.uniform(0.34, 0.66)
        bl = flame_blade(L_, 0.10, th=0.04, curl=0.28, seed=k)
        bl.rot('z', math.degrees(a))
        A.add(bl.move(0.26 * math.cos(a), 0.02 + 0.22 * math.sin(a), 1.46), bone='spine_03', look=ice2 if k % 3 == 0 else ice, smooth=FA)
    for k, (dx, dy, L_, cu) in enumerate(((0.04, 0.0, 0.9, 0.16), (-0.07, 0.04, 0.68, -0.22), (0.02, -0.08, 0.58, 0.3), (0.0, 0.1, 0.52, -0.1), (0.09, 0.07, 0.44, 0.25))):
        bl = flame_blade(L_, 0.115, th=0.055, curl=cu, seed=20 + k)
        A.add(bl.rot('z', 70.0 * k).move(dx, 0.02 + dy, 1.50), bone='Head', look=ice if k % 2 else ice2, smooth=FA)
    A.add(KC3.ice_chunk(0.15, 0.13, 0.10, seed=9, n=12).move(0, 0.02, 1.56), bone='spine_03', look=ice, smooth=FA)
    # ---- arms: rough ice columns ending in fused clubs, crystal-crowned shoulders
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        A.add(KC3.ice_chunk(0.15, 0.14, 0.135, seed=40 + (sx > 0), n=14).move(sx * 0.335, 0.02, 1.455), bone='clavicle' + s, look=ice, smooth=FA)
        for c_ in KI.cluster(V(sx * 0.36, 0.03, 1.53), axis=(sx * 0.55, 0.15, 1.0), count=6, spread=40.0, h=(0.10, 0.30), r=(0.028, 0.055), seed=44 + (sx > 0)):
            A.add(c_, bone='clavicle' + s, look=ice2, smooth=FA)
        limb_tube(A, ['upperarm' + s, 'lowerarm' + s], [0.108, 0.112, 0.13], ice, n=7, wob=0.014, seed=seed + 5 + (sx > 0), smooth=FA)
        w = R.H('hand' + s); el = R.H('lowerarm' + s)
        A.add(KC3.ice_chunk(0.10, 0.10, 0.10, seed=46 + (sx > 0), n=11).move((R.H('upperarm' + s) + el) / 2 + V(0, 0.01, 0.04)), bone='upperarm' + s, look=ice2, smooth=FA)
        A.add(KC3.ice_chunk(0.215, 0.18, 0.185, seed=50 + (sx > 0), n=18).move(w + V(sx * 0.08, 0, -0.01)), bone='lowerarm' + s, look=ice, smooth=FA)
        for c_ in KI.cluster(w + V(sx * 0.14, 0, 0.0), axis=(sx, 0, 0.1), count=7, spread=55.0, h=(0.09, 0.22), r=(0.03, 0.05), seed=60 + (sx > 0)):
            A.add(c_, bone='lowerarm' + s, look=ice2, smooth=FA)
        for c_ in KI.cluster(el + V(0, 0.04, 0.03), axis=(-sx * 0.2, 0.6, 0.8), count=4, spread=38.0, h=(0.06, 0.15), r=(0.02, 0.035), seed=70 + (sx > 0)):
            A.add(c_, bone='lowerarm' + s, look=ice2, smooth=FA)
        A.add(ellipsoid(0.05, 0.06, 0.06, n=7, m=5).move(el + V(sx * 0.02, -0.075, 0.0)), bone='lowerarm' + s, look=core, smooth=FA)       # cold seam at the elbow


# ================================================================================================ ash: the colossus
def cast_figure(A, bone, at, yaw, pitch, sc, look, roll=0.0, ratio=0.09, arms=75.0, melt=1, face='void', spark=None, K=None, crease=None):
    """one half-emerged ash-cast (head + torso + arms) pressed out of a mass, rigid on `bone`, waist at `at`.  face = look of
    the hollow eye sockets and the open mouth (they are what makes a lump read as a head from the game camera);
    spark = look of a glow deep in the mouth.  Returns the keep anchor."""
    import asm_parts7 as P7
    b = P7.half_body(ratio=ratio, arms_down=arms, gaunt=1.2, jaw=22.0)
    if melt:
        M.smooth_verts(b, int(melt), 0.5)
    piv = V(0, 0.02, 1.0)

    def xf(p):
        p.scale(sc, pivot=piv).rot('x', pitch, pivot=piv)
        if roll:
            p.rot('y', roll, pivot=piv)
        return p.rot('z', yaw, pivot=piv).move(V(at) - piv)

    xf(b)
    b.W = None
    K = b.center() if K is None else K
    A.add(b, bone=bone, look=look, smooth=180, keep=K)
    hp = M.RIG.H('Head')
    if face:
        pits = [ellipsoid(0.017, 0.013, 0.015, n=6, m=3).move(sx * 0.032, -0.098, 1.668) for sx in (-1, 1)]
        pits.append(ellipsoid(0.021, 0.014, 0.03, n=6, m=3).move(0.0, -0.108, 1.588))
        f = join(pits).rot('x', 14, pivot=hp)
        A.add(xf(f), bone=bone, look=face, keep=K)
    if spark:
        e = ellipsoid(0.012, 0.008, 0.016, n=5, m=3).move(0.0, -0.112, 1.586).rot('x', 14, pivot=hp)
        A.add(xf(e), bone=bone, look=spark, keep=K)
    if crease:                                             # sparks in the crease where the cast leaves the mass: a few specks, never a ring
        rng = np.random.default_rng(int(abs(yaw) * 7 + abs(pitch) * 3 + sc * 100))
        sp = []
        for a_ in rng.uniform(0, 2 * math.pi, 3):
            sp.append(KX.lump(0.03, 0.022, 0.014, seed=int(a_ * 10), amp=0.3, n=5, m=3).rot('z', math.degrees(a_) + 90)
                      .move(0.155 * math.cos(a_), 0.02 + 0.12 * math.sin(a_), 1.05 + rng.uniform(0.0, 0.07)))
        A.add(xf(join(sp)), bone=bone, look=crease, smooth=80, keep=K)
    return K


@part
def drift_mass2(A, mass='ashmass', pale='ashpale', spark='spark', face='void', seed=7, ratio=0.09):
    """a drift that stood up.  The mass itself is dark, settled ash (the body + big soft drifts); pressed out of it are the
    PALE casts of the people inside -- heads with hollow eye sockets and open mouths, shoulders, a few reaching arms --
    so that from the game camera the mound is dotted with faces, not with pebbles.  Dull sparks glow in the crease under
    every cast and deep in some mouths; the fists are knots of fused casts; a ragged plume of ash trails from the back."""
    R = A.R
    rng = np.random.default_rng(seed)
    spots = [  # bone, waist point, yaw, pitch, scale, arms down (small = the arm reaches out)
        ('spine_03', (0.10, 0.15, 1.03), 168, 18, 0.82, 84), ('spine_03', (-0.13, 0.14, 1.07), -158, 24, 0.76, 28), ('spine_02', (0.02, 0.175, 0.80), 10, -12, 0.86, 86),
        ('clavicle_l', (0.25, 0.02, 1.09), 95, 22, 0.74, 24), ('clavicle_r', (-0.25, 0.03, 1.06), -98, 26, 0.72, 84),
        ('spine_03', (0.11, -0.11, 0.99), 18, 12, 0.70, 86), ('spine_02', (-0.10, -0.12, 0.83), -14, 16, 0.72, 84),
        ('spine_02', (0.19, 0.02, 0.79), 86, 24, 0.72, 84), ('spine_02', (-0.195, 0.04, 0.83), -82, 22, 0.72, 34),
        ('spine_03', (0.02, 0.04, 1.17), 186, 48, 0.80, 84), ('spine_03', (-0.05, -0.03, 1.21), -8, 44, 0.74, 84),
        ('pelvis', (0.13, 0.10, 0.60), 150, 18, 0.68, 86), ('pelvis', (-0.14, 0.09, 0.58), -150, 22, 0.66, 86), ('pelvis', (0.0, -0.11, 0.60), 4, 14, 0.64, 86),
    ]
    for k, (bone, at, yaw, pitch, sc, arms) in enumerate(spots):
        cast_figure(A, bone, at, yaw, pitch, sc, pale, ratio=ratio, arms=float(arms), face=face, spark=spark if k % 3 == 0 else None, crease=spark)
    drifts = [  # x, y, z, rx, ry, rz, bone: the drift itself, bedding the casts
        (0.0, 0.04, 1.55, 0.25, 0.21, 0.15, 'spine_03'), (0.20, 0.03, 1.49, 0.16, 0.16, 0.12, 'clavicle_l'), (-0.20, 0.03, 1.49, 0.16, 0.16, 0.12, 'clavicle_r'),
        (0.0, 0.15, 1.38, 0.24, 0.15, 0.20, 'spine_03'), (0.0, -0.10, 1.34, 0.22, 0.13, 0.17, 'spine_03'), (0.0, 0.12, 1.10, 0.25, 0.16, 0.20, 'spine_02'),
        (0.0, -0.10, 1.08, 0.23, 0.14, 0.18, 'spine_02'), (0.17, 0.02, 1.22, 0.13, 0.17, 0.22, 'spine_02'), (-0.17, 0.02, 1.22, 0.13, 0.17, 0.22, 'spine_02'),
        (0.0, 0.03, 0.90, 0.25, 0.20, 0.15, 'pelvis'), (0.27, 0.02, 1.43, 0.12, 0.12, 0.10, 'upperarm_l'), (-0.27, 0.02, 1.43, 0.12, 0.12, 0.10, 'upperarm_r')]
    for k, (bx, by, bz, rx, ry, rz, bn) in enumerate(drifts):
        A.add(KX.lump(rx, ry, rz, seed=31 + k, amp=0.26, freq=6.0, n=10, m=6).move(bx, by, bz), bone=bn, look=mass, smooth=180, keep=True)
    for sx in (1, -1):                                              # fists: knots of fused casts
        s = '_l' if sx > 0 else '_r'
        w = R.H('hand' + s)
        Kf = w + V(sx * 0.09, 0, 0)
        A.add(KX.lump(0.19, 0.165, 0.16, seed=5 + (sx > 0), amp=0.3, freq=7.0, n=10, m=6).move(Kf), bone='hand' + s, look=mass, smooth=180, keep=Kf)
        for k, (yaw, roll, dz) in enumerate(((0, sx * 100, 0.05), (180, sx * 80, -0.03))):
            cast_figure(A, 'hand' + s, Kf + V(-sx * 0.15, 0.03 * (2 * k - 1), dz), yaw, 18, 0.50, pale, roll=float(roll), ratio=ratio * 0.8, arms=86.0, face=face, K=Kf)
        for t in (0.25, 0.7):
            p = KC3.BP('lowerarm' + s, t)
            A.add(KX.lump(0.105, 0.10, 0.095, seed=60 + int(t * 10), amp=0.28, n=9, m=5).move(p), bone='lowerarm' + s, look=mass, smooth=180, keep=True)
    base = V(0.0, 0.20, 1.50)                                       # the plume: thin ragged tongues, longest in the middle
    for k in range(11):
        u = (k - 5) / 5.0
        L_ = (0.34 + 0.44 * (1 - u * u)) * rng.uniform(0.8, 1.15)
        bl = flame_blade(L_, rng.uniform(0.045, 0.085), th=0.03, curl=rng.uniform(-0.3, 0.3), n=10, seed=k)
        bl.rough(0.012, 11.0, seed=float(k))
        bl.rot('x', -rng.uniform(52, 76)).rot('z', 26.0 * u + rng.uniform(-8, 8))
        bl.move(base + V(0.17 * u, rng.uniform(-0.03, 0.05), rng.uniform(-0.10, 0.06) - 0.06 * u * u))
        sway_weights(bl, base[1] + 0.1, base[1] + 0.7, top='spine_03', low='pelvis', amt=0.5, coord=bl.v[:, 1])
        A.add(bl, look=pale if k % 3 else mass, smooth=40)
