"""asm_parts8.py -- finishing pass: real cord nets, banded flames, and the bespoke shells of the last stage-4 bases
(gallows-tree, brazier-hulk, mirror-carrier, crowned colony, drift-colossus, tallow-mound).  Same rules as asm_parts.py.

Lessons built into these parts:
  * a carried assembly of several pieces uses ONE shared keep anchor (A.add(..., keep=<point>)), otherwise every piece
    follows the bone's scale with its own centre and the assembly comes apart on bulky bodies;
  * loads ride on the spine (spine_02 / spine_03), never on a limb that the clips swing;
  * only cloth_ / leather_ / spectral_ materials are double sided in the game -- open sheets and flat cords must use
    such a look, everything else has to be a closed solid;
  * things that hang get weights that blend down the spine (they lag when the torso bends: a cheap sway).
"""
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
import asm

asm.PRE.update(
    netcord=asm._p('cloth', 'cloth', color=(0.19, 0.155, 0.095), dirt=(0.07, 0.06, 0.04), hem_lo=-2, hem_hi=-1, scale=30.0, bump=0.8, stain_amt=0.5),
    flame_lo=asm._p('emis', 'emis', emit=(1.0, 0.46, 0.10), strength=1.5, mottle=0.25, base_mul=0.3),
    flame_mid=asm._p('emis', 'emis', emit=(1.0, 0.24, 0.035), strength=1.5, mottle=0.3, base_mul=0.3),
    flame_tip=asm._p('emis', 'emis', emit=(0.62, 0.09, 0.012), strength=1.5, mottle=0.4, base_mul=0.3),
    coldfire=asm._p('emis', 'emis', emit=(0.07, 0.26, 0.80), strength=1.5, mottle=0.9, base_mul=0.3, scale=7.0, crust=0.55),
    arc=asm._p('emis', 'emis', emit=(0.42, 0.30, 0.95), strength=1.6, mottle=0.3, base_mul=0.3),
    gill=asm._p('emis', 'emis', emit=(0.16, 0.24, 0.03), strength=1.4, mottle=0.7, base_mul=0.5, scale=40.0),
    spark=asm._p('emis', 'emis', emit=(0.70, 0.07, 0.012), strength=1.4, base_mul=0.2, mottle=0.7, crust=0.8),
    iron_black=asm._p('metal', 'metal', color=(0.045, 0.045, 0.05), rust=(0.13, 0.07, 0.04), rust2=(0.035, 0.025, 0.02), amount=0.55,
                      edge_color=(0.26, 0.26, 0.28), rough=0.55, edge=0.7),
    brass_dark=asm._p('metal', 'metal', color=(0.20, 0.135, 0.05), rust=(0.06, 0.13, 0.10), rust2=(0.03, 0.06, 0.05), amount=0.55,
                      edge_color=(0.62, 0.48, 0.22), rough=0.42),
    capskin=asm._p('leather', 'leather', color=(0.23, 0.15, 0.075), scuff=0.35, scale=9.0, rough=0.62, bump=0.9),
    stalk=asm._p('leather', 'leather', color=(0.42, 0.37, 0.25), scuff=0.2, scale=22.0, rough=0.7),
    lampglow=asm._p('emis', 'emis', emit=(1.0, 0.40, 0.07), strength=1.5, mottle=0.5, base_mul=0.3),
    ashpale=asm._p('rock', 'rock', color=(0.30, 0.295, 0.29), ash=(0.55, 0.54, 0.52), ash_amt=0.9, scale=22.0, crack_w=0.03, rough=0.97),
)


def root_weights(p, z_hi=0.5, z_lo=0.06):
    """things that trail on the floor: spine by height, pelvis at the hips, the ROOT bone where they lie on the ground
    (the trailing end stays put on the floor while the body lunges; the hanging part stretches between)"""
    st = [(z_lo, {'root': 1.0}), (z_hi, {'pelvis': 0.65, 'root': 0.35}), (0.95, {'pelvis': 1.0}), (1.10, {'spine_01': 1.0}),
          (1.25, {'spine_02': 1.0}), (1.40, {'spine_03': 1.0})]
    return M.weights_by(p, p.v[:, 2], st)


def net_cords(G, w=0.011):
    """open diamond net on a draped grid (rows+1, cols, 3): two crossing flat cords per cell (double-sided cloth look).
    Returns one Part (4 verts / 1 quad per cord)."""
    G = np.asarray(G, float)
    m, n, _ = G.shape
    du = np.zeros_like(G); dv = np.zeros_like(G)
    du[:, 1:-1] = G[:, 2:] - G[:, :-2]; du[:, 0] = G[:, 1] - G[:, 0]; du[:, -1] = G[:, -1] - G[:, -2]
    dv[1:-1] = G[2:] - G[:-2]; dv[0] = G[1] - G[0]; dv[-1] = G[-1] - G[-2]
    N = np.cross(du, dv)
    N /= np.maximum(np.linalg.norm(N, axis=2, keepdims=True), 1e-9)
    vs = []; fs = []
    for i in range(m - 1):
        for j in range(n - 1):
            for (a, b) in (((i, j), (i + 1, j + 1)), ((i, j + 1), (i + 1, j))):
                pa, pb = G[a], G[b]
                nn = N[a] + N[b]
                s = np.cross(pb - pa, nn)
                ln = np.linalg.norm(s)
                if ln < 1e-9:
                    continue
                s = s / ln * (w / 2)
                k = len(vs)
                vs += [pa - s, pa + s, pb + s, pb - s]
                fs.append((k, k + 1, k + 2, k + 3))
    p = M.Part(np.array(vs), fs)
    p.smooth = 180.0
    return p


def edge_cord(pts, r=0.006):
    return tube(np.asarray(pts, float), r, n=4)


# ================================================================================================ marsh: the cast-net
@part
def cast_net(A, look='netcord', seed=5, sinkers='stone', floats='wood_z', rows=30, cols=15, creel_at=(-0.07, 0.27, 1.24)):
    """huge weighted cast-net: gathered on the left shoulder, hanging down the back as a narrow bunch and spreading on
    the floor behind into a wide triangular train with stone sinkers along its hem (the part on the floor is bound to
    the root bone: it stays on the ground and the hanging part stretches when she lunges); a string of cork floats
    round the neck.  The net is real cord geometry (open diamond mesh), not a punched sheet."""
    S = A.S('body')
    zs = KC.rest_on(A.rest, 0.17, 0.02)
    zs = 1.47 if zs is None else zs
    ctrl = [(0.17, -0.115, zs - 0.16, 0.050), (0.17, -0.05, zs - 0.01, 0.060), (0.165, 0.03, zs + 0.012, 0.070), (0.155, 0.13, zs - 0.07, 0.085),
            (0.135, 0.245, 1.12, 0.10), (0.11, 0.33, 0.62, 0.13), (0.085, 0.47, 0.16, 0.19), (0.06, 0.66, 0.02, 0.33), (0.03, 0.98, 0.016, 0.52),
            (0.0, 1.38, 0.014, 0.70)]
    C_ = np.array(ctrl)
    t = np.linspace(0, 1, rows + 1) ** 0.9
    seg = np.r_[0, np.cumsum(np.linalg.norm(np.diff(C_[:, :3], axis=0), axis=1))]
    seg /= seg[-1]
    cen = np.stack([np.interp(t, seg, C_[:, k]) for k in range(3)], axis=1)
    hw = np.interp(t, seg, C_[:, 3])
    rng = np.random.default_rng(seed)
    G = np.zeros((rows + 1, cols, 3))
    cc = V(creel_at)
    for i in range(rows + 1):
        for j in range(cols):
            v = 2.0 * j / (cols - 1) - 1.0
            p = cen[i] + V(v * hw[i], 0.0, 0.0)
            on_floor = cen[i][2] < 0.05
            if on_floor:
                p[1] += 0.10 * (1 - v * v) * (t[i] - 0.6) + 0.02 * math.sin(7 * v + i)       # scalloped hem, bulging at the centre
                p[2] = 0.014 + 0.007 * math.sin(5.0 * v + 0.9 * i) ** 2
            else:
                p[2] -= 0.05 * v * v * min(1.0, (zs - cen[i][2]) * 2.0)                      # the edges of the bunch hang lower
                p[1] += 0.03 * math.sin(3.0 * v + 0.5 * i) * min(1.0, (zs - cen[i][2]) * 2.0)
                q, nq, _ = S.nearest(p)                                                       # lie on the body, never inside it
                d = float(np.dot(p - q, nq))
                if d < 0.012 and np.linalg.norm(p - q) < 0.12:
                    p = q + nq * 0.012
                e = (p - cc) / V(0.155, 0.125, 0.19)                                          # ... and round the creel
                if float(e @ e) < 1.0:
                    p[1] = cc[1] + 0.125 * math.sqrt(max(0.0, 1.0 - (e[0] ** 2 + e[2] ** 2))) + 0.012
            G[i, j] = p
    net = net_cords(G, w=0.012)
    root_weights(net)
    A.add(net, look=look, smooth=180)
    for side in (0, cols - 1):                                    # selvedge cords down both edges and along the hem
        e = edge_cord(G[:, side], 0.007); root_weights(e); A.add(e, look=look, smooth=60)
    e = edge_cord(G[-1], 0.008); root_weights(e); A.add(e, look=look, smooth=60)
    k = 0
    stones = [G[-1, j] for j in range(0, cols, 2)] + [G[i, s] for i in range(rows - 7, rows, 3) for s in (0, cols - 1)]
    for p in stones:                                               # the dotted hem: stone sinkers tied on
        st = M.rock(0.042, 0.036, 0.026, seed=seed + k, n=9).rot('z', 40.0 * k).move(V(p[0], p[1], 0.024))
        k += 1
        A.add(st, bone='root', look=sinkers, smooth=40)
    for k in range(9):
        a = math.radians(-160 + 140 * k / 8)
        A.add(ellipsoid(0.022, 0.022, 0.018, n=7, m=5).move(0.125 * math.cos(a), -0.01 + 0.115 * math.sin(a), 1.50 + 0.018 * math.cos(2 * a)), bone='spine_03',
              look=floats, smooth=60)


# ================================================================================================ wax: a banded flame
@part
def flame3(A, at=None, bone='spine_03', h=0.36, r=0.075, sweep=(0.0, 0.5, 0.0), looks=('flame_lo', 'flame_mid', 'flame_tip')):
    """a guttering candle flame in three colour bands (warm yellow body, orange shoulder, dull red tip): no blown-out
    white blob.  Stands on a point (default: the wick of a wax dome) and streams along `sweep`."""
    p0 = V(A.refs['dome_top'] if at is None else at)
    n, m = 12, 15
    rings = []
    for i in range(m):
        t = i / (m - 1)
        prof = math.sin(math.pi * min(1.0, t * 0.92 + 0.10)) ** 0.75 * (1 - 0.62 * t)
        c = p0 + V(0, 0, -0.02 + h * t) + V(sweep) * (t ** 1.6) * h / 0.34 + V(0.018 * math.sin(9 * t), 0, 0)
        a = np.linspace(0, 2 * math.pi, n, endpoint=False)
        rr = max(r * prof, 0.002)
        rings.append(np.stack([c[0] + rr * np.cos(a), c[1] + rr * 0.85 * np.sin(a), np.full(n, c[2])], axis=1))
    R_ = np.array(rings)
    cuts = (0, 6, 10, m - 1)
    for k in range(3):
        seg = M.loft(R_[cuts[k]:cuts[k + 1] + 1], cap0=(k == 0), cap1=(k == 2)).recalc()
        A.add(seg, bone=bone, look=looks[k], smooth=180)


# ================================================================================================ hush: the Mute
@part
def mute_torso(A, look='chalk', hole=0.125, c=(0.0, 0.0, 1.25), lining='void'):
    """the headless giant's upper body in one piece: the shoulders rise into a low smooth dome (no neck, no head) and a
    single round hole pierces the chest clean through (a real tunnel with a rounded lip, dark inside).  Replaces the
    body's own torso (use with body hide = spine_02 / spine_03 / clavicles / neck / head): skinned like the body."""
    c = V(c)
    n = 32
    key = np.array([(0.98, 0.020, 0.18, 0.136), (1.04, 0.020, 0.235, 0.152), (1.12, 0.018, 0.268, 0.162), (1.22, 0.016, 0.288, 0.168), (1.32, 0.016, 0.304, 0.170),
                    (1.42, 0.016, 0.314, 0.166), (1.48, 0.014, 0.302, 0.156), (1.525, 0.012, 0.262, 0.14), (1.56, 0.010, 0.195, 0.116), (1.585, 0.008, 0.10, 0.07)])
    zs = np.unique(np.r_[key[:, 0], np.linspace(c[2] - hole * 1.3, c[2] + hole * 1.3, 11)])
    sec = lambda z: tuple(float(np.interp(z, key[:, 0], key[:, k])) for k in (1, 2, 3))
    t = M.loft_z([(z, 0) + sec(z) for z in zs], n=n, cap0=False, cap1=True)
    d = np.hypot(t.v[:, 0] - c[0], t.v[:, 2] - c[2])
    ins = d < hole
    dirs = np.stack([t.v[:, 0] - c[0], np.zeros(len(t.v)), t.v[:, 2] - c[2]], axis=1)
    dirs[d < 1e-6] = (1.0, 0.0, 0.0)
    dirs /= np.maximum(np.linalg.norm(dirs, axis=1, keepdims=True), 1e-9)
    t.v[ins] += dirs[ins] * (hole - d[ins])[:, None]             # the wall opens into a round hole, front and back
    t = t.sub(~t.vmask_faces(ins, 'all'))
    KC.skin_cloth(t, body=A.rest, smooth=4, limit=['pelvis', 'spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r', 'upperarm_l', 'upperarm_r'])
    A.add(t, look=look, smooth=180)
    m = 24
    a = np.linspace(0, 2 * math.pi, m, endpoint=False)
    fr = []; bk = []
    for ak in a:                                                  # where the hole's edge meets the front / back surface
        x = c[0] + hole * math.cos(ak); z = c[2] + hole * math.sin(ak)
        cy, rx, ry = sec(z)
        dy = ry * math.sqrt(max(0.0, 1.0 - (x / rx) ** 2))
        fr.append((x, cy - dy, z)); bk.append((x, cy + dy, z))
    fr = np.array(fr); bk = np.array(bk)
    rings = [fr + (bk - fr) * s for s in (-0.02, 0.33, 0.67, 1.02)]
    tun = M.loft(np.array(rings), cap0=False, cap1=False).flip()
    tun.smooth = 180.0
    A.add(tun, look=lining, like=t)
    for loop in (fr, bk):                                         # rounded lips
        A.add(tube(loop, 0.02, n=6, closed=True), look=look, smooth=180, like=t)


@part
def mute_hands(A, look='chalk', size=0.34, th=0.042):
    """flat mitten-hands the size of shields: one paddle with a thumb lobe, grown out of the forearm without a wrist
    (rigid on the forearm: with lengthened arms a hand-bound mitten swings off its joint and comes apart)"""
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        w = A.R.H('hand' + s)
        m = ellipsoid(size * 0.62, size * 0.47, th, n=18, m=9)
        k = np.clip(m.v[:, 0] / (size * 0.62), -1, 1)
        m.v[:, 1] *= 1.0 - 0.38 * np.clip(-k, 0, 1) ** 2                                         # narrow at the wrist
        m.v[:, 2] *= 1.0 + 0.9 * np.clip(-k, 0, 1) ** 2                                          # ... and thick there, like a wrist
        m.v[:, 2] -= 0.11 * size * np.clip(k, 0, 1) ** 2                                         # the tip curls to the palm
        th_ = ellipsoid(size * 0.24, size * 0.14, th * 0.95, n=10, m=6).rot('z', -34).move(-size * 0.10, -size * 0.43, -0.006)
        g = join([m, th_])
        if sx < 0:
            g.mirror()
        A.add(g.move(w + V(sx * size * 0.50, 0, 0)), bone='lowerarm' + s, look=look, smooth=180)
