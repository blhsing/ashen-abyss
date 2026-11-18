"""asm_parts9.py -- the last stage-4 bases: husk huddle, ice-breaker's plough, gallows-tree, brazier-hulk, mirror-carrier,
crowned colony, drift-colossus, tallow-mound.  Same rules as asm_parts.py / asm_parts8.py (read its docstring first)."""
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
from asm_parts8 import root_weights, net_cords
import asm

rad = math.radians
ARMS = None


def arm_bones():
    global ARMS
    if ARMS is None:
        ARMS = M.RIG.chain('upperarm_l') + M.RIG.chain('upperarm_r')
    return ARMS


def sway_weights(p, c0, c1, top='spine_03', low='spine_01', amt=0.55, coord=None):
    """things that hang (or trail): `top` where coord = c0, blending to amt x `low` where coord = c1 (coord: z by default).
    The low end lags when the torso bends or twists: a cheap sway without extra bones."""
    x = p.v[:, 2] if coord is None else np.asarray(coord, float)
    t = np.clip((x - c0) / (c1 - c0 if abs(c1 - c0) > 1e-9 else 1e-9), 0, 1) * amt
    return M.weights_from(p, {top: 1.0 - t, low: t})


def place(p, cols, origin):
    """local (x, y, z) -> origin + x * cols[0] + y * cols[1] + z * cols[2]"""
    Mx = np.stack([np.asarray(c, float) for c in cols], axis=1)
    p.v = p.v @ Mx.T + np.asarray(origin, float)
    return p


def bar(pts, half_up, half_side, up=(0, 0, 1)):
    """rectangular bar along a path: half extents along the `up` hint and across it"""
    return tube(np.asarray(pts, float), half_up * 1.4142, n=4, r2=half_side * 1.4142, up=up, phase=math.pi / 4)


def chest_band(A, look='strap', z=1.33, w=0.035, th=0.007, tilt=0.0, on='body', pad=0.012):
    """strap / iron band round the chest, seated on the body (or on a published surface) and skinned like it"""
    S = A.S(on)
    cx, cy, rx, ry = girth_of(A, on, z)
    a = np.radians(np.linspace(0, 360, 28, endpoint=False))
    ring = np.stack([cx + (rx + 0.03) * np.cos(a), cy + (ry + 0.03) * np.sin(a), z + math.tan(rad(tilt)) * (rx + 0.03) * np.cos(a)], axis=1)
    P = np.array([S.nearest(q)[0] + unit(S.nearest(q)[1]) * pad for q in ring])
    t = tube(P, w * 0.7071, n=4, r2=th * 0.7071, up=(0, 0, 1), closed=True, phase=math.pi / 4)
    if on == 'body':
        A.add(t, look=look, skin=2, smooth=40)
    else:
        A.add(t, look=look, like=A.refs[on], smooth=40)
    return t


def rivet_ring(A, c, nrm, r, n, bone, look, rr=0.011, keep=False):
    fr = M.frame_from(nrm)
    for k in range(n):
        a = 2 * math.pi * (k + 0.5) / n
        A.add(KC3.rivet(V(c) + (fr[:, 0] * math.cos(a) + fr[:, 2] * math.sin(a)) * r, nrm, r=rr), bone=bone, look=look, keep=keep)


# ================================================================================================ frost: the arm plough
@part
def arm_plough(A, side='l', look='oak', rim='rust', length=1.2, w_out=0.46, w_in=0.24, top=0.36, yaw=30.0, half=42.0, fwd=0.26):
    """wooden snow-plough strapped to the forearm like the prow of a ship: two boards meeting in an iron-shod stem that
    stands upright in front of the arm when it hangs (rigid on the forearm, NOT on the hand: no wrist flapping).  The
    inner board is narrow so that it clears the hip; the rear lower corners are cut away like a prow."""
    for sx in A.sides(side):
        s = '_l' if sx > 0 else '_r'
        E = A.R.H('lowerarm' + s); Wp = A.R.H('hand' + s)
        ln = float(np.linalg.norm(Wp - E))
        a = unit(Wp - E)                                   # down when the arm hangs
        f = V(0, -1, 0); o = V(0, 0, 1)                    # forward / outward when the arm hangs (T-pose rest axes)
        b = math.cos(rad(yaw)) * f + math.sin(rad(yaw)) * o          # where the prow points
        p_out = V(0, math.sin(rad(yaw)), math.cos(rad(yaw)))         # across it, towards the outside
        e0 = E + b * fwd - a * top                         # top of the stem
        wood = A._wood('wood_plough' + s, a, look)
        d_out = None
        for (wd, sgn) in ((w_out, 1.0), (w_in, -1.0)):
            d = unit(-math.cos(rad(half)) * b + sgn * math.sin(rad(half)) * p_out)
            if sgn > 0:
                d_out = d
            nrm = np.cross(a, d)
            bd = box(length, wd, 0.036, bevel=0.006)
            x = bd.v[:, 0].copy(); y = bd.v[:, 1].copy()
            low = x > 0
            bd.v[low, 0] -= 0.30 * np.clip((y[low] + wd / 2) / wd, 0, 1) * (x[low] / (length / 2))
            A.add(place(bd, (a, d, nrm), e0 + a * (length / 2) + d * (wd / 2)), bone='lowerarm' + s, look=wood, smooth=30)
            for t in (0.16, 0.56):                         # iron straps round the board
                st = box(0.05, wd * 0.98, 0.052, bevel=0.004)
                A.add(place(st, (a, d, nrm), e0 + a * (length * t) + d * (wd * 0.5)), bone='lowerarm' + s, look=rim, smooth=30)
            seam = box(length * 0.86, 0.008, 0.04)         # the joint between two planks
            A.add(place(seam, (a, d, nrm), e0 + a * (length * 0.45) + d * (wd * 0.52)), bone='lowerarm' + s, look='void')
        stem = box(length * 1.03, 0.085, 0.075, bevel=0.014)
        A.add(place(stem, (a, b, np.cross(a, b)), e0 + a * (length / 2) + b * 0.012), bone='lowerarm' + s, look=rim, smooth=30)
        for t in (0.12, 0.85):                             # braces from the outer board to the arm, lashed on
            arm_pt = E + a * (ln * t)
            A.add(cyl(arm_pt, e0 + a * (top + ln * t) + d_out * (w_out * 0.55), 0.02, n=6), bone='lowerarm' + s, look=wood, smooth=40)
            A.add(cyl(arm_pt, e0 + a * (top + ln * t) + b * 0.0, 0.018, n=6), bone='lowerarm' + s, look=wood, smooth=40)
            A.add(KI.limb_ring('lowerarm' + s, t, 0.068, thick=0.014, n=12, m=4), bone='lowerarm' + s, look='rope')
            A.add(KI.limb_ring('lowerarm' + s, t + 0.045, 0.066, thick=0.012, n=12, m=4), bone='lowerarm' + s, look='rope')


# ================================================================================================ husk: the Huddle
@part
def huddle(A, look='skin', eyes='eye', blanket='rag', seed=3, ratio=0.17, sway=0.55, edge=0.42):
    """husks who froze together and never let go: head + torso + limp arms of five more figures fused to the walker's
    back and shoulders (one rigid cluster on spine_03; their hanging arms blend to spine_01, so the fringe of arms lags
    and sways), under ONE shared blanket frozen stiff -- a sheet draped from above over the real shapes below it
    (heightfield with a tent slope: it cannot fall steeper than frozen cloth would), ragged hem, short over the
    walker's face.  Keep the spine un-scaled in the spec (bulk / chest = 1): the cluster is built to the rest body."""
    import asm_parts7 as P7
    spots = [((0.17, 0.21, 1.00), 150, 24, 0.92), ((-0.20, 0.19, 0.95), -158, 30, 0.88), ((0.0, 0.31, 0.80), 180, 38, 0.95),
             ((0.35, 0.04, 1.08), 96, 28, 0.80), ((-0.34, 0.03, 1.02), -100, 34, 0.82)]
    figs = []
    piv = V(0, 0.02, 1.0)
    for k, (at, yaw, pitch, sc) in enumerate(spots):
        b = P7.half_body(ratio=ratio, arms_down=68.0 + 9 * (k % 3), gaunt=1.0)
        hang_t = np.clip((1.40 - b.v[:, 2]) / 0.55, 0, 1) * np.clip(b.wsum(arm_bones()) * 1.3, 0, 1)
        b.scale(sc, pivot=piv).rot('x', pitch, pivot=piv).rot('z', yaw, pivot=piv).move(V(at) - piv)
        M.weights_from(b, {'spine_03': 1.0 - sway * hang_t, 'spine_01': sway * hang_t})
        A.add(b, look=look, smooth=180)
        figs.append(b)
        if eyes:
            e = join([ellipsoid(0.008, 0.006, 0.008, n=6, m=4).move(sx * 0.031, -0.096, 1.668) for sx in (-1, 1)])
            e.rot('x', 14, pivot=M.RIG.H('Head'))
            e.scale(sc, pivot=piv).rot('x', pitch, pivot=piv).rot('z', yaw, pivot=piv).move(V(at) - piv)
            A.add(e, bone='spine_03', look=eyes)
    # ---- the blanket
    solid = join([A.rest.copy()] + [f.copy() for f in figs])
    S = KB2.Surf(solid)
    c = V(0.0, 0.17, 0.0)
    nr, na = 14, 36
    rng = np.random.default_rng(seed)
    ang = np.linspace(0, 2 * math.pi, na, endpoint=False)
    front = np.exp(-((np.degrees(np.arctan2(np.sin(ang + math.pi / 2), np.cos(ang + math.pi / 2)))) / 48.0) ** 2)      # 1 at the front (-Y)
    ed = edge * (1 + 0.13 * np.sin(3 * ang + 1.0) + 0.08 * np.sin(7 * ang + 0.3)) * (1 - 0.50 * front) * rng.uniform(0.9, 1.05, na)
    zp = 0.0
    for dx, dy in ((0, 0), (0.08, 0), (-0.08, 0), (0, 0.1), (0, -0.08)):
        h, _ = S.top(c[0] + dx, c[1] + dy)
        if h is not None:
            zp = max(zp, float(h[2]))
    zp += 0.03
    G = np.zeros((nr + 1, na, 3)); Zt = np.zeros((nr + 1, na))
    for j in range(na):
        for i in range(nr + 1):
            r = max(ed[j] * (i / nr) ** 0.9, 0.004)
            x = c[0] + r * math.cos(ang[j]); y = c[1] + r * 1.12 * math.sin(ang[j])
            h, _ = S.top(x, y)
            Zt[i, j] = float(h[2]) + 0.016 if h is not None else 0.0
            G[i, j] = (x, y, max(Zt[i, j], zp - 1.25 * r ** 1.3))
    for _ in range(3):                                      # relax the cloth, then lay it back on what it covers
        z = G[:, :, 2]
        zs = z.copy()
        zs[1:-1] = (z[1:-1] * 2 + z[:-2] + z[2:] + np.roll(z, 1, axis=1)[1:-1] + np.roll(z, -1, axis=1)[1:-1]) / 6.0
        G[:, :, 2] = np.maximum(zs, Zt)
    G[-1, :, 2] -= rng.uniform(0.0, 0.07, na) + 0.05 * (np.arange(na) % 2)
    G[-2, :, 2] -= 0.02
    sh = M.sheet(G, closed_u=True)
    sh = KW.punch(sh, (nr + 1, na), 0.22, seed=seed, t0=0.74)
    KX.crumple(sh, 0.007, 11.0, float(seed))
    sh.smooth = 180.0
    sway_weights(sh, 1.25, 0.85, amt=0.35)
    A.add(sh, look=blanket)
    A.refs['blanket'] = sh


# ================================================================================================ char: the gallows-tree
def noose(top, L_, r=0.011, loop=(0.065, 0.10), seed=0):
    """a hanging rope with a hangman's knot and an open loop: from `top` straight down by L_ (unweighted Parts)"""
    top = V(top)
    kz = L_ - loop[1] * 2 - 0.07
    rope = tube([top, top + V(0.004 * math.sin(seed), 0.003, -kz * 0.5), top + V(0, 0, -kz)], r, n=5)
    knot = tube([top + V(0, 0, -kz + 0.01), top + V(0, 0, -kz - 0.07)], r * 2.3, n=7)
    a = np.linspace(0, 2 * math.pi, 14, endpoint=False)
    lp = np.stack([loop[0] * np.sin(a), 0.012 * np.sin(2 * a + seed), -loop[1] * (1 - np.cos(a))], axis=1) + top + V(0, 0, -kz - 0.06)
    ring = tube(lp, r, n=5, closed=True)
    tail = tube([top + V(0.012, 0, -kz - 0.03), top + V(0.03, 0.01, -kz - 0.12), top + V(0.026, 0.0, -kz - 0.2)], [r, r * 0.8, r * 0.3], n=4)
    p = join([rope, knot, ring.rot('z', 25.0 * seed, pivot=top), tail])
    p.smooth = 60.0
    return p


def gibbet(h=0.56, r=0.125, bars=8):
    """[(Part, look)]: a rusted gibbet cage hanging from the origin (hook ring at z = 0, cage below)"""
    ps = []
    ps.append((torus(0.03, 0.008, n=10, m=4).rot('x', 90).move(0, 0, -0.03), 'rust'))
    z0 = -0.07
    prof = [(0.03, z0), (r * 0.7, z0 - h * 0.12), (r, z0 - h * 0.32), (r, z0 - h * 0.75), (r * 0.72, z0 - h * 0.93), (r * 0.25, z0 - h)]
    for k in range(bars):
        a = 2 * math.pi * k / bars
        pts = [V(rr * math.cos(a), rr * math.sin(a), zz) for rr, zz in prof]
        ps.append((tube(M.polyline(pts, 9), 0.006, n=4, r2=0.012, up=(math.cos(a), math.sin(a), 0)), 'rust'))
    for rr, zz in prof[1:5]:
        ps.append((torus(rr, 0.008, n=14, m=4).move(0, 0, zz), 'rust'))
    ps.append((revolve([(0.0, z0 - h * 0.93), (r * 0.72, z0 - h * 0.93), (r * 0.7, z0 - h * 0.95), (0.0, z0 - h * 0.955)], n=10), 'rust'))
    return ps


@part
def gallows_tree(A, look='charwood', glow='ember', rope='rope', cage='rust', seed=6, reach=1.12, side='r'):
    """massive hollow charred trunk on two root-legs, its broken top open (the game camera looks down into the hollow: a
    bed of embers), a knot-hole mouth glowing on the front, ONE great horizontal bough -- the gallows arm -- at shoulder
    height on the right with three nooses and a gibbet cage, only a splintered stump on the left.  The bough is rigid on
    spine_03 (it sweeps when the trunk twists and never drags on the floor); nooses and cage blend to spine_01 so they
    lag and sway.  Use with a hidden body (show=[])."""
    R = A.R
    rng = np.random.default_rng(seed)
    sx = -1.0 if side == 'r' else 1.0
    n = 30
    out_ = [(0.58, 0.03, 0.09, 0.09), (0.66, 0.03, 0.22, 0.21), (0.76, 0.03, 0.31, 0.29), (0.88, 0.028, 0.325, 0.30), (1.00, 0.024, 0.295, 0.27), (1.14, 0.022, 0.255, 0.235),
            (1.30, 0.02, 0.24, 0.222), (1.46, 0.02, 0.268, 0.238), (1.62, 0.02, 0.25, 0.224), (1.78, 0.02, 0.228, 0.206), (1.93, 0.02, 0.212, 0.192)]
    in_ = [(1.93, 0.02, 0.182, 0.165), (1.84, 0.02, 0.178, 0.16), (1.77, 0.02, 0.168, 0.15), (1.725, 0.02, 0.11, 0.10)]

    def bark(th_, z_):                                    # irregular fluting, deep furrows, buttresses at the root
        m_ = 1.0 + 0.07 * np.sin(5 * th_ + 2.2 * z_ + seed) + 0.05 * np.sin(9 * th_ - 3.1 * z_ + 2 * seed) + 0.035 * np.sin(14 * th_ + 5.0 * z_)
        return m_ + 0.16 * np.maximum(0.0, np.sin(4 * th_ + 0.7)) ** 3 * float(M.smoothstep(1.2, 0.74, z_))

    def off(z_):                                          # the trunk leans and bows a little
        return V(0.032 * math.sin(2.2 * (z_ - 0.8)), 0.02 * math.sin(3.0 * (z_ - 0.8) + 1.0), 0.0)

    rings = M.rings_z([(z, 0, cy, rx, ry) for z, cy, rx, ry in out_], n, bark)
    rin = M.rings_z([(z, 0, cy, rx, ry) for z, cy, rx, ry in in_], n, bark_mod(seed, 8, 0.03))
    for rg, src in ((rings, out_), (rin, in_)):
        for k, q in enumerate(src):
            rg[k] += off(q[0])
    th = np.linspace(0, 2 * math.pi, n, endpoint=False)
    jag = np.maximum(0.0, 0.17 * np.abs(np.sin(1.5 * th + 0.6)) ** 2.0 + 0.07 * np.sin(5 * th + 1.0) + 0.04 * np.sin(11 * th))
    jag[np.abs(np.sin(th * 0.5 + 0.3)) < 0.25] *= 0.15                     # one side is broken low
    rings[-1][:, 2] += jag; rin[0][:, 2] += jag * 0.92
    rings[-2][:, 2] += jag * 0.35
    t = M.loft(np.concatenate([rings, rin]), cap0=True, cap1=True).recalc()
    t.rough(0.009, 9.0, seed=float(seed))
    z_weights(t)
    A.add(t, look=look, smooth=35)
    eb = off(1.75) + V(0, 0.02, 0)
    A.add(revolve([(0.0, 1.762), (0.11, 1.758), (0.14, 1.742), (0.0, 1.73)], n=14).move(eb), bone='spine_03', look=glow, smooth=60)       # embers in the hollow, seen from above
    for k in range(7):
        a = 2 * math.pi * k / 7 + 0.3
        rr = 0.075 if k else 0.0
        A.add(KX.lump(0.036, 0.032, 0.024, seed=k, amp=0.35).move(eb + V(rr * math.cos(a), rr * math.sin(a), 1.768)), bone='spine_03', look=look, smooth=40)
    # knot-hole mouth: a bark lip with the glow of the hollow behind it
    mz = 1.36
    my = 0.02 - 0.228 * float(bark(np.array([-math.pi / 2]), mz)[0]) + off(mz)[1]
    mx = 0.01 + off(mz)[0]
    lipp = np.stack([0.085 * np.cos(th[::2]) * (1 + 0.12 * np.sin(3 * th[::2])), np.zeros(n // 2), 0.115 * np.sin(th[::2])], axis=1) + V(mx, my - 0.006, mz)
    A.add(tube(lipp, 0.034, n=6, r2=0.038, closed=True, up=(0, 1, 0)), bone='spine_03', look=look, smooth=40)
    A.add(ellipsoid(0.066, 0.02, 0.094, n=10, m=6).move(mx, my - 0.004, mz), bone='spine_03', look=glow, smooth=80)
    for k in range(4):                                    # snags of bark across the mouth, like teeth
        xx = mx + (k - 1.5) * 0.034
        A.add(M.cone_spike(0.012, 0.05 + 0.02 * (k % 2), n=4).rot('x', 180 if k % 2 else 0).move(xx, my - 0.022, mz + (0.085 if k % 2 else -0.085)), bone='spine_03', look=look, smooth=30)
    for (kx, kz, kr) in ((0.14, 1.66, 0.045), (-0.17, 1.12, 0.05)):        # dead knots
        ky = 0.02 - 0.232 * math.sqrt(max(0.0, 1 - (kx / 0.26) ** 2))
        A.add(torus(kr, kr * 0.42, n=9, m=5).rot('x', 90).rot('z', math.degrees(math.atan2(kx, 0.25)) * -1).move(kx, ky - 0.004, kz), bone='spine_03' if kz > 1.4 else 'spine_01',
              look=look, smooth=40)
    # root legs
    for s2 in (1, -1):
        s = '_l' if s2 > 0 else '_r'
        limb_tube(A, ['thigh' + s, 'calf' + s], [0.15, 0.115, 0.085], look, n=9, wob=0.016, seed=seed + (s2 > 0), smooth=35)
        hp = R.H('thigh' + s)                               # a buttress root running from the trunk onto the thigh
        A.add(M.horn(M.polyline([hp + V(s2 * 0.10, -0.10, 0.16), hp + V(s2 * 0.11, -0.13, -0.05), hp + V(s2 * 0.06, -0.10, -0.26)], 7), 0.07, n=6, r_tip=0.02), bone='thigh' + s,
              look=look, smooth=35)
        ank = R.H('foot' + s)
        for (dx, dy, bn) in ((0.06, -0.27, 'ball'), (0.15, 0.10, 'foot'), (-0.04, 0.16, 'foot')):
            pts = M.polyline([ank + V(0, 0, 0.04), ank + V(s2 * dx * 0.5, dy * 0.5, -0.03), ank + V(s2 * dx, dy, -0.088)], 6)
            A.add(M.horn(pts, 0.055, n=6, r_tip=0.013), bone=bn + s, look=look, smooth=35)
    # the stump on the other side
    so = R.H('upperarm_l' if sx < 0 else 'upperarm_r')
    st = tube(M.polyline([so + V(sx * 0.06, 0, 0), so + V(-sx * 0.10, 0.01, 0.02), so + V(-sx * 0.22, -0.01, 0.03)], 6), np.linspace(0.085, 0.06, 6), n=8)
    st.rough(0.006, 14.0, seed=2.0)
    A.add(st, bone='spine_03', look=look, smooth=35)
    for k in range(5):
        a = k * 1.3
        A.add(M.cone_spike(0.016, 0.07 + 0.035 * (k % 2), n=4).rot('y', -sx * 90).move(so + V(-sx * 0.21, 0.035 * math.cos(a), 0.03 + 0.035 * math.sin(a))), bone='spine_03',
              look=look, smooth=30)
    # the gallows bough
    sh = R.H('upperarm_r' if sx < 0 else 'upperarm_l') + V(0, 0, 0.04)
    d = unit(V(sx * 1.0, -0.26, 0.05))
    bp = [sh - d * 0.10, sh + d * reach * 0.3 + V(0, 0, 0.03), sh + d * reach * 0.62 + V(0, 0.02, 0.01), sh + d * reach * 0.86 + V(0, 0, 0.04), sh + d * reach + V(0, -0.02, 0.07)]
    BP = M.polyline(bp, 13)
    bough = tube(BP, np.linspace(0.105, 0.052, 13), n=9)
    bough.rough(0.006, 13.0, seed=5.0)
    A.add(bough, bone='spine_03', look=look, smooth=35)
    col = M.loft_z([(-0.05, 0, 0, 0.16, 0.15), (0.05, 0, 0, 0.125, 0.12), (0.14, 0, 0, 0.108, 0.105)], n=10, mod=bark_mod(3, 5, 0.08))
    A.add(col.tf(M.rot_to((0, 0, 1), d)).move(sh + d * 0.045), bone='spine_03', look=look, smooth=35)              # the bough's collar on the trunk
    for (tt, up_, ln) in ((0.42, V(0, 0.2, 1.0), 0.26), (0.7, V(0, -0.5, 0.8), 0.18), (1.0, V(sx * 0.7, 0.3, 0.6), 0.24), (1.0, V(sx * 0.8, -0.5, -0.1), 0.20)):
        p0 = BP[int(round(tt * 12))]
        dd = unit(d * 0.5 + unit(up_))
        tw = M.horn(M.polyline([p0 - dd * 0.02, p0 + dd * ln * 0.55 + V(0, 0, 0.015), p0 + dd * ln], 6), 0.034 if tt < 1 else 0.03, n=5, r_tip=0.006)
        A.add(tw, bone='spine_03', look=look, smooth=35)
    zb = float(BP[:, 2].mean())
    for k, (tt, L_) in enumerate(((0.36, 0.46), (0.55, 0.66), (0.72, 0.52))):
        p0 = BP[int(round(tt * 12))]
        rb = float(np.interp(tt, [0, 1], [0.105, 0.052]))
        A.add(torus(rb + 0.008, 0.012, n=10, m=4).tf(M.rot_to((0, 0, 1), d)).move(p0), bone='spine_03', look=rope, smooth=50)
        A.add(torus(rb + 0.008, 0.012, n=10, m=4).tf(M.rot_to((0, 0, 1), d)).move(p0 + d * 0.022), bone='spine_03', look=rope, smooth=50)
        ns = noose(p0 + V(0, 0, -rb - 0.004), L_, seed=k + 1)
        sway_weights(ns, zb, zb - 0.75, amt=0.6)
        A.add(ns, look=rope)
    p0 = BP[11] + V(0, 0, -0.06)
    ch = M.chain(np.array([p0 + V(0, 0, 0.03), p0 + V(0, 0, -0.16)]), link=0.05, r=0.008)
    sway_weights(ch, zb, zb - 0.9, amt=0.6)
    A.add(ch, look=cage, smooth=60)
    A.add(torus(0.07, 0.011, n=10, m=4).tf(M.rot_to((0, 0, 1), d)).move(BP[11]), bone='spine_03', look=cage, smooth=50)
    for q, lk in gibbet():
        q.move(p0 + V(0, 0, -0.16))
        sway_weights(q, zb, zb - 0.9, amt=0.6)
        A.add(q, look=cage if lk == 'rust' else lk, smooth=40)
    P = KB.skull_parts(10.0, teeth=False)
    sk = join([P['skull'], P['jaw']]).scale(0.58).rot('x', 24).rot('z', 35).move(p0 + V(0.01, 0.0, -0.16 - 0.07 - 0.56 * 0.80))
    sk = M.decimate(sk, 0.5) if sk.ntri > 500 else sk
    sway_weights(sk, zb, zb - 0.9, amt=0.6)
    A.add(sk, look='bone', smooth=60)


# ================================================================================================ coldfire: the brazier-hulk
@part
def brazier_hulk(A, ice='ice', ice2='ice2', iron='iron_black', core='coldfire', seed=4):
    """a dead beacon's iron brazier -- a basket of bars a metre wide -- worn as a ribcage by a hulking body of blue ice
    that grew up through it: glowing core inside the black bars, a crown of frozen flame tongues erupting from the
    basket top where a head would be, the brazier's three legs sticking out of the back, arms of rough ice columns
    ending in fused clubs.  Faceted like the cold-flame spirit.  Use with a hidden body (show=[])."""
    R = A.R
    rng = np.random.default_rng(seed)
    FA = 18
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        limb_tube(A, ['thigh' + s, 'calf' + s], [0.145, 0.118, 0.10], ice, n=6, wob=0.014, seed=seed + (sx > 0), smooth=FA)
        A.add(KC3.ice_chunk(0.115, 0.175, 0.085, seed=seed + 3 + (sx > 0), n=14).move(sx * 0.092, -0.045, 0.078), bone='foot' + s, look=ice, smooth=FA)
        kn = R.H('calf' + s)
        for c_ in KI.cluster(kn + V(sx * 0.05, -0.07, 0.0), axis=(sx * 0.5, -0.7, 0.6), count=4, spread=42.0, h=(0.07, 0.16), r=(0.022, 0.04), seed=seed + 11 + (sx > 0)):
            A.add(c_, bone='calf' + s, look=ice2, smooth=FA)
    A.add(KC3.ice_chunk(0.25, 0.20, 0.165, seed=seed, n=16).move(0, 0.02, 0.985), bone='pelvis', look=ice, smooth=FA)
    cr = M.loft_z([(0.98, 0, 0.02, 0.16, 0.14), (1.10, 0, 0.02, 0.24, 0.20), (1.28, 0, 0.02, 0.285, 0.235), (1.44, 0, 0.02, 0.27, 0.22), (1.54, 0, 0.015, 0.17, 0.14),
                   (1.58, 0, 0.01, 0.07, 0.06)], n=9, phase=0.2)
    cr.rough(0.012, 9.0, seed=2.0)
    z_weights(cr)
    A.add(cr, look=core, smooth=FA)                                 # the frozen fire: seen through the bars
    for k in range(7):                                              # plates of clear ice grown over the core, leaving it visible between them
        a = 2 * math.pi * k / 7 + 0.5
        pl = KC3.ice_chunk(0.10, 0.045, 0.14, seed=30 + k, n=10)
        zc = 1.12 + 0.11 * (k % 3)
        rr = float(np.interp(zc, [1.0, 1.28, 1.5], [0.19, 0.265, 0.22]))
        pl.rot('z', math.degrees(a) + 90).move(rr * math.cos(a), 0.02 + rr * 0.84 * math.sin(a), zc)
        z_weights(pl)
        A.add(pl, look=ice if k % 2 else ice2, smooth=FA)
    # the brazier basket
    K = V(0, 0.02, 1.27)
    bone = 'spine_02'
    prof = [(0.215, 1.00), (0.30, 1.10), (0.365, 1.26), (0.385, 1.42), (0.395, 1.52)]
    nb = 12
    for k in range(nb):
        a = 2 * math.pi * (k + 0.5) / nb
        pts = [V(rr * math.cos(a), 0.02 + rr * 0.86 * math.sin(a), zz) for rr, zz in prof]
        A.add(tube(M.polyline(pts, 8), 0.011, n=4, r2=0.02, up=(math.cos(a), math.sin(a), 0), phase=math.pi / 4), bone=bone, look=iron, smooth=30, keep=K)
    for (rr, zz, tk) in ((0.215, 1.00, 0.018), (0.365, 1.26, 0.016), (0.395, 1.52, 0.024)):
        A.add(torus(rr, tk, n=24, m=5, ry=rr * 0.86).move(0, 0.02, zz), bone=bone, look=iron, smooth=40, keep=K)
    for k in range(nb):                                             # rivet heads where bars cross the middle hoop
        a = 2 * math.pi * (k + 0.5) / nb
        A.add(KC3.rivet(V(0.383 * math.cos(a), 0.02 + 0.383 * 0.86 * math.sin(a), 1.26), unit(V(math.cos(a), math.sin(a), 0)), r=0.014, n=6), bone=bone, look=iron, keep=K)
    for k, a_deg in enumerate((90.0, 38.0, 142.0)):                 # the brazier's three legs, sticking out of the back
        a = rad(a_deg)
        b0 = V(0.25 * math.cos(a), 0.02 + 0.25 * 0.86 * math.sin(a), 1.06)
        dd = unit(V(0.75 * math.cos(a), 1.0, 0.12 + 0.1 * k))
        A.add(tube([b0 - dd * 0.05, b0 + dd * 0.3, b0 + dd * 0.52], [0.03, 0.024, 0.017], n=6), bone=bone, look=iron, smooth=30, keep=K)
        A.add(revolve([(0.0, -0.012), (0.05, -0.012), (0.05, 0.012), (0.0, 0.012)], n=8).tf(M.rot_to((0, 0, 1), dd)).move(b0 + dd * 0.53), bone=bone, look=iron, smooth=30, keep=K)
    # the crown of frozen flame where a head would be
    nt = 11
    for k in range(nt):
        a = 2 * math.pi * (k + 0.5) / nt
        L_ = rng.uniform(0.34, 0.62)
        bl = flame_blade(L_, 0.10, th=0.04, curl=0.28, seed=k)
        bl.rot('z', math.degrees(a))
        A.add(bl.move(0.26 * math.cos(a), 0.02 + 0.22 * math.sin(a), 1.46), bone='spine_03', look=ice2 if k % 3 == 0 else ice, smooth=FA)
    for k, (dx, dy, L_, cu) in enumerate(((0.04, 0.0, 0.86, 0.16), (-0.07, 0.04, 0.66, -0.22), (0.02, -0.08, 0.56, 0.3), (0.0, 0.1, 0.5, -0.1))):
        bl = flame_blade(L_, 0.115, th=0.055, curl=cu, seed=20 + k)
        A.add(bl.rot('z', 70.0 * k).move(dx, 0.02 + dy, 1.50), bone='Head', look=ice if k % 2 else ice2, smooth=FA)
    A.add(KC3.ice_chunk(0.15, 0.13, 0.10, seed=9, n=12).move(0, 0.02, 1.56), bone='spine_03', look=ice, smooth=FA)
    # arms: rough ice columns ending in fused clubs
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        A.add(KC3.ice_chunk(0.14, 0.13, 0.125, seed=40 + (sx > 0), n=13).move(sx * 0.335, 0.02, 1.455), bone='clavicle' + s, look=ice, smooth=FA)
        limb_tube(A, ['upperarm' + s, 'lowerarm' + s], [0.105, 0.11, 0.13], ice, n=6, wob=0.012, seed=seed + 5 + (sx > 0), smooth=FA)
        w = R.H('hand' + s)
        A.add(KC3.ice_chunk(0.215, 0.18, 0.185, seed=50 + (sx > 0), n=18).move(w + V(sx * 0.08, 0, -0.01)), bone='lowerarm' + s, look=ice, smooth=FA)
        for c_ in KI.cluster(w + V(sx * 0.14, 0, 0.0), axis=(sx, 0, 0.1), count=6, spread=55.0, h=(0.09, 0.2), r=(0.03, 0.05), seed=60 + (sx > 0)):
            A.add(c_, bone='lowerarm' + s, look=ice2, smooth=FA)
        el = R.H('lowerarm' + s)
        for c_ in KI.cluster(el + V(0, 0.04, 0.03), axis=(-sx * 0.2, 0.6, 0.8), count=3, spread=38.0, h=(0.06, 0.14), r=(0.02, 0.035), seed=70 + (sx > 0)):
            A.add(c_, bone='lowerarm' + s, look=ice2, smooth=FA)
        A.add(ellipsoid(0.05, 0.06, 0.06, n=7, m=5).move(el + V(sx * 0.02, -0.075, 0.0)), bone='lowerarm' + s, look=core, smooth=FA)       # cold seam at the elbow


# ================================================================================================ engine: the mirror-carrier
def sun_wedge(L_=1.25, half=20.0, th=0.05, hub=0.09):
    """[(Part, look)]: one wedge-shaped segment of the Brass Sun, apex (the sun's centre) at the origin pointing +Z, the
    rim arc below; lies in the XZ plane (thickness along Y).  Polished faces, a rolled rim, engraved rays and rings."""
    ps = []
    aa = np.radians(np.linspace(-half, half, 11))
    outl = [(hub * math.sin(a), -hub * math.cos(a)) for a in (aa[0], aa[-1])] + [(L_ * math.sin(a), -L_ * math.cos(a)) for a in aa[::-1]]
    plate = M.prism(np.array(outl), -th / 2, th / 2).bevel(0.008)
    ps.append((plate.rot('x', 90), 'brass_bright'))
    for rr, tk in ((L_, 0.034), (L_ * 0.72, 0.014), (L_ * 0.44, 0.012), (hub, 0.02)):            # rim + engraved rings (on both faces)
        pts = np.stack([rr * np.sin(aa), np.zeros(len(aa)), -rr * np.cos(aa)], axis=1)
        ps.append((tube(pts, th * 0.5 + (0.014 if rr == L_ else 0.007), n=6, r2=tk, up=(0, 1, 0)), 'brass'))
    for a in np.radians((-half, half)):                                                          # the cut edges: dark, torn brass
        pts = [V(hub * math.sin(a), 0, -hub * math.cos(a)), V(L_ * math.sin(a), 0, -L_ * math.cos(a))]
        ps.append((bar(pts, th * 0.5 + 0.004, 0.012, up=(0, 1, 0)), 'brass_dark'))
    for a in np.radians((-half * 0.5, 0.0, half * 0.5)):                                         # rays
        for sy in (-1, 1):
            pts = [V(L_ * 0.16 * math.sin(a), sy * th * 0.5, -L_ * 0.16 * math.cos(a)), V(L_ * 0.95 * math.sin(a), sy * th * 0.5, -L_ * 0.95 * math.cos(a))]
            ps.append((tube(pts, [0.012, 0.004], n=4, r2=0.008, up=(0, 1, 0), phase=math.pi / 4), 'brass'))
    rv = []
    for a in aa[1:-1:2]:
        for sy in (-1, 1):
            rv.append(KC3.rivet(V((L_ - 0.07) * math.sin(a), sy * th * 0.5, -(L_ - 0.07) * math.cos(a)), (0, sy, 0), r=0.014, n=6))
    ps.append((join(rv), 'brass'))
    return ps


def arc_light(A, c, nrm, r, bone, glow, housing, keep=False):
    """a small arc lamp: a glowing lens sunk in a turned collar"""
    Rm = M.rot_to((0, 0, 1), unit(nrm))
    col = revolve([(r * 1.55, 0.0), (r * 1.6, 0.012), (r * 1.25, 0.022), (r * 1.05, 0.016), (r * 1.05, 0.0)], n=14)
    A.add(col.tf(Rm).move(V(c)), bone=bone, look=housing, smooth=40, keep=keep)
    lens = revolve([(r * 1.05, 0.006), (r * 0.8, 0.014), (r * 0.4, 0.019), (0.0, 0.02)], n=12)
    A.add(lens.tf(Rm).move(V(c)), bone=bone, look=glow, smooth=180, keep=keep)


@part
def carrier(A, look='brass', dark='brass_dark', glow='arc', bright='brass_bright', fin=1.25, seed=2):
    """a towering two-legged brass porter: a blocky riveted body with a boiler hump, thick piston legs (sleeve + polished
    rod, drum knees with small arc lamps), a small sunken head-lantern, arms that are lifting forks (girder arms, two
    blunt tines at the wrist that point forward when the arm hangs), and on its back an A-frame carrying one huge wedge of
    the Brass Sun that stands above it like a sail.  Use with a hidden body (show=[])."""
    R = A.R
    N = 16
    # ---- body
    A.add(box(0.54, 0.38, 0.50, bevel=0.04).move(0, 0.025, 1.245), bone='spine_02', look=look, smooth=30)
    for sxx in (-0.15, 0.15):                                       # front straps with rivets
        A.add(box(0.05, 0.012, 0.44, bevel=0.004).move(sxx, -0.168, 1.245), bone='spine_02', look=dark, smooth=30)
        for zz in np.linspace(1.06, 1.43, 6):
            A.add(KC3.rivet(V(sxx, -0.173, zz), (0, -1, 0), r=0.011, n=6), bone='spine_02', look=look)
    for syy, zz in ((-0.168, 1.12), (-0.168, 1.40)):                # cross straps
        A.add(box(0.50, 0.01, 0.035, bevel=0.003).move(0, syy, zz), bone='spine_02', look=dark, smooth=30)
    arc_light(A, (0, -0.166, 1.27), (0, -1, 0), 0.05, 'spine_02', glow, dark)                                                           # chest port
    for k in range(3):                                              # louvres under the port
        A.add(box(0.16, 0.014, 0.012).move(0, -0.17, 1.165 - 0.022 * k), bone='spine_02', look=dark)
    A.add(box(0.58, 0.42, 0.045, bevel=0.012).move(0, 0.025, 1.02), bone='spine_02', look=dark, smooth=30)
    A.add(box(0.60, 0.43, 0.05, bevel=0.012).move(0, 0.025, 1.49), bone='spine_02', look=dark, smooth=30)
    for sxx in (-1, 1):
        for syy in (-1, 1):
            for zz in (1.02, 1.49):
                for xx in (0.09, 0.25):
                    A.add(KC3.rivet(V(sxx * xx, 0.025 + syy * 0.215, zz), (0, syy, 0), r=0.012, n=6), bone='spine_02', look=look)
        for zz in np.linspace(1.08, 1.42, 4):                       # side seams
            A.add(KC3.rivet(V(sxx * 0.272, 0.025, zz), (sxx, 0, 0), r=0.012, n=6), bone='spine_02', look=look)
        A.add(cyl((sxx * 0.20, 0.16, 1.50), (sxx * 0.20, 0.19, 1.74), 0.036, n=10), bone='spine_02', look=dark, smooth=40)              # twin stacks
        A.add(torus(0.04, 0.01, n=10, m=4).move(sxx * 0.20, 0.19, 1.74), bone='spine_02', look=look)
    A.add(box(0.42, 0.31, 0.20, bevel=0.03).move(0, 0.02, 0.93), bone='pelvis', look=look, smooth=30)
    A.add(cyl((0, 0.02, 0.985), (0, 0.02, 1.06), 0.14, n=N), bone='spine_01', look=dark, smooth=40)                                     # waist bearing
    # ---- head-lantern, small and sunken between the shoulders
    A.add(revolve([(0.13, 0.0), (0.145, 0.02), (0.135, 0.055), (0.10, 0.06), (0.10, 0.0)], n=N).move(0, 0.0, 1.512), bone='spine_03', look=dark, smooth=40)
    A.add(cyl((0, 0.0, 1.50), (0, 0.0, 1.60), 0.066, n=12), bone='Head', look=glow, smooth=60)
    for k in range(8):
        a = 2 * math.pi * k / 8
        A.add(cyl((0.072 * math.cos(a), 0.072 * math.sin(a), 1.50), (0.072 * math.cos(a), 0.072 * math.sin(a), 1.61), 0.009, n=4), bone='Head', look=dark)
    for zz in (1.535, 1.57):
        A.add(torus(0.072, 0.007, n=N, m=4).move(0, 0, zz), bone='Head', look=dark)
    A.add(revolve([(0.092, 0.0), (0.097, 0.012), (0.06, 0.04), (0.02, 0.055), (0.02, 0.075), (0.0, 0.078)], n=N).move(0, 0, 1.60), bone='Head', look=look, smooth=40)
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        # ---- legs: thick pistons
        hp = R.H('thigh' + s); kn = R.H('calf' + s); an = R.H('foot' + s)
        A.add(cyl(hp + V(-sx * 0.06, 0, 0), hp + V(sx * 0.085, 0, 0), 0.125, n=N), bone='pelvis', look=dark, smooth=40)                 # hip drum
        arc_light(A, hp + V(sx * 0.086, 0, 0), (sx, 0, 0), 0.034, 'pelvis', glow, look)
        tdir = unit(kn - hp); tl = float(np.linalg.norm(kn - hp))
        A.add(cyl(hp + tdir * 0.03, hp + tdir * tl * 0.64, 0.118, n=N, r1=0.11), bone='thigh' + s, look=look, smooth=40)                  # sleeve
        for tt in (0.14, 0.38, 0.64):
            A.add(torus(0.12, 0.013, n=N, m=4).tf(M.rot_to((0, 0, 1), tdir)).move(hp + tdir * tl * tt), bone='thigh' + s, look=dark)
        A.add(cyl(hp + tdir * tl * 0.58, kn, 0.058, n=12), bone='thigh' + s, look=bright, smooth=50)                                      # polished rod
        A.add(cyl(kn + V(-0.10, 0, 0), kn + V(0.10, 0, 0), 0.10, n=N), bone='calf' + s, look=dark, smooth=40)                              # knee drum
        for e in (-1, 1):
            arc_light(A, kn + V(e * 0.101, 0, 0), (e, 0, 0), 0.03, 'calf' + s, glow, look)
        cdir = unit(an - kn); cl = float(np.linalg.norm(an - kn))
        A.add(cyl(kn, kn + cdir * cl * 0.5, 0.055, n=12), bone='calf' + s, look=bright, smooth=50)
        A.add(cyl(kn + cdir * cl * 0.40, an + V(0, 0, 0.02), 0.105, n=N, r1=0.12), bone='calf' + s, look=look, smooth=40)
        for tt in (0.40, 0.72):
            A.add(torus(0.112, 0.013, n=N, m=4).tf(M.rot_to((0, 0, 1), cdir)).move(kn + cdir * cl * tt), bone='calf' + s, look=dark)
        A.add(box(0.23, 0.38, 0.105, bevel=0.022).move(sx * 0.092, -0.06, 0.053), bone='foot' + s, look=look, smooth=30)
        A.add(box(0.25, 0.14, 0.055, bevel=0.012).move(sx * 0.092, -0.215, 0.03), bone='ball' + s, look=dark, smooth=30)
        A.add(cyl(an + V(-0.085, 0, 0.0), an + V(0.085, 0, 0.0), 0.07, n=12), bone='foot' + s, look=dark, smooth=40)
        for xx in (-0.07, 0.07):
            A.add(KC3.rivet(V(sx * 0.092 + xx, -0.25, 0.058), (0, 0, 1), r=0.014, n=6), bone='ball' + s, look=look)
        # ---- arms: girders ending in lifting forks
        shp = R.H('upperarm' + s); el = R.H('lowerarm' + s); wr = R.H('hand' + s)
        A.add(cyl(shp + V(-sx * 0.10, 0, 0), shp + V(sx * 0.06, 0, 0), 0.115, n=N), bone='spine_03', look=dark, smooth=40)              # shoulder drum
        ul = float(np.linalg.norm(el - shp))
        A.add(box(ul, 0.085, 0.11, bevel=0.01).move((shp + el) / 2), bone='upperarm' + s, look=look, smooth=30)
        for fz in (-0.055, 0.055):
            A.add(box(ul * 0.96, 0.115, 0.018, bevel=0.004).move((shp + el) / 2 + V(0, 0, fz)), bone='upperarm' + s, look=dark, smooth=30)
        for xx in np.linspace(-0.3, 0.3, 4):
            A.add(KC3.rivet((shp + el) / 2 + V(ul * xx, -0.044, 0), (0, -1, 0), r=0.011, n=6), bone='upperarm' + s, look=look)
        A.add(cyl(el + V(0, -0.08, 0), el + V(0, 0.08, 0), 0.08, n=12), bone='lowerarm' + s, look=dark, smooth=40)
        arc_light(A, el + V(0, -0.081, 0), (0, -1, 0), 0.026, 'lowerarm' + s, glow, look)
        fl = float(np.linalg.norm(wr - el))
        A.add(box(fl + 0.08, 0.07, 0.095, bevel=0.008).move((el + wr) / 2 + V(sx * 0.04, 0, 0)), bone='lowerarm' + s, look=look, smooth=30)
        # fork carriage at the wrist: two tines side by side (T-pose Z = sideways once the arm hangs), pointing forward (-Y)
        A.add(box(0.085, 0.06, 0.32, bevel=0.01).move(wr + V(sx * 0.06, -0.01, 0)), bone='lowerarm' + s, look=dark, smooth=30)
        for tz in (-0.115, 0.115):
            hx = 0.017                                               # half thickness of a tine (along the arm), half width 0.04
            tine = M.loft(np.array([[(-hx, 0.0, -0.04), (hx, 0.0, -0.04), (hx, 0.0, 0.04), (-hx, 0.0, 0.04)],
                                    [(-hx, -0.30, -0.04), (hx, -0.30, -0.04), (hx, -0.30, 0.04), (-hx, -0.30, 0.04)],
                                    [(-hx * 0.9, -0.42, -0.036), (hx * 0.2, -0.42, -0.036), (hx * 0.2, -0.42, 0.036), (-hx * 0.9, -0.42, 0.036)],
                                    [(-hx * 0.9, -0.47, -0.028), (-hx * 0.5, -0.47, -0.028), (-hx * 0.5, -0.47, 0.028), (-hx * 0.9, -0.47, 0.028)]]), cap0=True, cap1=True).recalc()
            tine.smooth = 30.0
            if sx < 0:
                tine.mirror()
            A.add(tine.move(wr + V(sx * 0.09, -0.035, tz)), bone='lowerarm' + s, look=bright)
            A.add(box(0.22, 0.036, 0.08, bevel=0.006).move(wr + V(-sx * 0.01, -0.02, tz)), bone='lowerarm' + s, look=look, smooth=30)       # the tine's heel up the arm
            A.add(KC3.rivet(wr + V(sx * 0.06, -0.042, tz), (0, -1, 0), r=0.014, n=6), bone='lowerarm' + s, look=look)
    # ---- the A-frame and the wedge of the Brass Sun
    K = V(0, 0.30, 1.30)
    bone = 'spine_03'
    lean = 9.0
    B = V(0, 0.335, 1.085)                                          # where the middle of the rim stands
    for q, lk in sun_wedge(L_=fin, half=20.0):
        q.move(0, 0, fin).rot('x', -lean).move(B)
        A.add(q, bone=bone, look={'brass_bright': bright, 'brass': look, 'brass_dark': dark}[lk], smooth=30, keep=K)
    top = B + V(0, fin * 0.66 * math.sin(rad(lean)) + 0.07, fin * 0.66 * math.cos(rad(lean)))
    for sx in (1, -1):
        foot = V(sx * 0.30, 0.215, 1.02)
        A.add(tube([foot, top + V(sx * 0.03, 0, 0)], 0.024, n=6), bone=bone, look=dark, smooth=40, keep=K)
        A.add(tube([foot, foot + V(0, 0.15, 0.03)], 0.022, n=6), bone=bone, look=dark, smooth=40, keep=K)                                   # ledge arm under the rim
        A.add(tube([V(sx * 0.19, 0.20, 1.44), V(sx * 0.21, 0.24, 1.46)], 0.02, n=6), bone=bone, look=dark, smooth=40, keep=K)
        mid = foot + (top - foot) * 0.5
        A.add(tube([mid, V(sx * 0.16, 0.22, 1.47)], 0.016, n=5), bone=bone, look=dark, smooth=40, keep=K)                                   # stay to the body
    A.add(box(0.78, 0.10, 0.045, bevel=0.01).move(0, 0.335, 1.045), bone=bone, look=dark, smooth=30, keep=K)                              # the ledge the rim stands on
    A.add(box(0.46, 0.04, 0.04, bevel=0.008).move(0, 0.27, 1.46), bone=bone, look=dark, smooth=30, keep=K)
    A.add(box(0.10, 0.11, 0.07, bevel=0.012).move(top + V(0, -0.03, 0)), bone=bone, look=dark, smooth=30, keep=K)                          # the clamp at the top
    for sx in (1, -1):                                              # chains from the clamp to the wedge's cut edges
        e = B + V(sx * fin * 0.80 * math.sin(rad(20.0)) * 0.9, fin * 0.80 * math.sin(rad(lean)) + 0.03, fin * 0.80 * math.cos(rad(lean)) * 0.62)
        A.add(M.chain(np.array([top + V(sx * 0.04, 0, 0), e]), link=0.05, r=0.008), bone=bone, look=dark, smooth=60, keep=K)


# ================================================================================================ lamp: the crowned colony
@part
def colony(A, look='skin', cap='capskin', gill='gill', lamp='lampglow', stalk='stalk', R_=0.66, seed=5, lamps=11):
    """three infected fused back-to-back under a single enormous table-mushroom cap: two more bodies (no arms, heads
    bowed, legs dangling just clear of the floor) grown onto the walker's back, a thick stalk rising between the three
    necks, the cap two metres across with warts and a sunken centre, glowing gills underneath and a ring of lantern-
    fruit hanging from its rim on threads; the walker's arms are each twisted together from three limbs."""
    R = A.R
    rng = np.random.default_rng(seed)
    legs = M.RIG.chain('thigh_l') + M.RIG.chain('thigh_r')
    C0 = V(0.0, 0.15, 0.0)
    for k, yaw in enumerate((120.0, -120.0)):
        b = M.load_body('male')
        KF.gaunt_face(b, 0.8)
        b = b.sub(~b.vmask_faces(b.wsum(arm_bones()) > 0.5, 'all'))
        b = M.decimate(b, 0.11)
        caps = join([KX.lump(0.085, 0.08, 0.075, seed=11 + 2 * k + (sxx > 0), amp=0.3, freq=8.0).move(sxx * 0.205, 0.012, 1.43) for sxx in (-1, 1)])   # fungus over the arm stumps
        M.pose_sculpt(b, {'Head': dict(rot=[('x', 30)]), 'neck_01': dict(rot=[('x', 16)]), 'spine_03': dict(rot=[('x', 10)]),
                          'thigh_l': dict(rot=[('x', -10 - 6 * k)]), 'calf_l': dict(rot=[('x', 24)]), 'thigh_r': dict(rot=[('x', -16 + 8 * k)]), 'calf_r': dict(rot=[('x', 20)]),
                          'foot_l': dict(rot=[('x', 28)]), 'foot_r': dict(rot=[('x', 24)])})
        dz = None
        for q in (b, caps):
            q.scale(0.95, pivot=(0, 0, 1.0))
            q.rot('z', yaw).move(C0 + V(0.17 * math.sin(rad(yaw)), -0.17 * math.cos(rad(yaw)), 0.0))
            dz = 0.045 - q.v[:, 2].min() if dz is None else dz
            q.v[:, 2] += dz
        A.add(caps, bone='spine_03', look=stalk, smooth=180)
        M.weights_by(b, b.v[:, 2], [(0.95, {'pelvis': 1.0}), (1.10, {'spine_01': 1.0}), (1.25, {'spine_02': 1.0}), (1.40, {'spine_03': 1.0})])
        A.add(b, look=look, smooth=180)
    # twisted arms: two more copies of each arm wound round it (they keep the arm's own weights)
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        arm = KC.body_piece(M.RIG.chain('upperarm' + s), body=A.rest)
        o = R.H('upperarm' + s); ax = unit(R.H('hand' + s) - o)
        for k in (1, 2):
            q = arm.copy()
            rel = q.v - o
            tpos = rel @ ax
            perp = rel - np.outer(tpos, ax)
            ang = rad(120.0 * k) + tpos * 2.6 * (1 if k == 1 else -1) * sx
            off = M.frame_from(ax)
            e1, e2 = off[:, 0], off[:, 2]
            ca, sa = np.cos(ang), np.sin(ang)
            p1 = perp @ e1; p2 = perp @ e2
            perp2 = np.outer(p1 * ca - p2 * sa, e1) + np.outer(p1 * sa + p2 * ca, e2)
            shift = np.outer(0.034 * np.cos(ang), e1) + np.outer(0.034 * np.sin(ang), e2)
            q.v = o + np.outer(tpos * (0.96 if k == 1 else 1.03), ax) + perp2 * 0.92 + shift
            A.add(q, look=look, smooth=180)
    # the stalk between the three necks
    K = V(0, 0.13, 1.60)
    bone = 'spine_03'
    zc = 2.02
    stp = M.loft_z([(1.40, 0, 0.13, 0.20, 0.19), (1.52, 0, 0.13, 0.15, 0.14), (1.70, 0, 0.13, 0.125, 0.12), (1.88, 0, 0.13, 0.14, 0.135), (zc - 0.02, 0, 0.13, 0.24, 0.23)], n=14,
                    mod=bark_mod(seed, 5, 0.05))
    stp.rough(0.008, 10.0, seed=3.0)
    A.add(stp, bone=bone, look=stalk, smooth=70, keep=K)
    A.add(torus(0.175, 0.03, n=14, m=5).scale((1, 1, 0.5)).move(0, 0.13, 1.80), bone=bone, look=stalk, smooth=70, keep=K)                 # the ring of the veil
    # the table cap
    n = 36
    prof = [(R_ * 0.985, -0.045), (R_, -0.012), (R_ * 0.985, 0.03), (R_ * 0.92, 0.075), (R_ * 0.78, 0.115), (R_ * 0.58, 0.135), (R_ * 0.36, 0.132), (R_ * 0.16, 0.112),
            (0.0, 0.10)]
    lobes = lambda th_, z_: 1.0 + 0.035 * np.sin(3 * th_ + 0.8) + 0.02 * np.sin(7 * th_ + 2.0)
    capm = M.loft(M.rings_z([(z_, 0, 0, r_, r_) for r_, z_ in prof[:-1]], n, lobes), cap0=False, cap1=True).recalc()
    PR = [p_[0] for p_ in prof[1:]][::-1]; PZ = [p_[1] for p_ in prof[1:]][::-1]         # the cap's top profile, radius ascending
    rr = np.hypot(capm.v[:, 0], capm.v[:, 1])
    capm.v[:, 2] += 0.012 * np.sin(rr / R_ * 22.0) * (rr > R_ * 0.3) + 0.02 * np.array([M.fbm(p, 5.0, 2.0) for p in capm.v])
    capm.smooth = 70.0
    A.add(capm.move(0, 0.13, zc), bone=bone, look=cap, smooth=70, keep=K)
    ng = 48
    ga = np.linspace(0, 2 * math.pi, ng, endpoint=False)
    zig = 0.008 * (np.arange(ng) % 2 * 2 - 1)
    gr = []
    for r_, z_ in ((R_ * 0.975, -0.046), (R_ * 0.80, -0.036), (R_ * 0.55, -0.012), (R_ * 0.30, 0.008), (0.20, 0.012)):
        gr.append(np.stack([r_ * np.cos(ga), r_ * np.sin(ga), z_ + zig * (r_ / R_)], axis=1))
    gl = M.loft(np.array(gr), cap0=False, cap1=False)
    gl = gl.recalc()
    if gl.normals()[:, 2].mean() > 0:
        gl.flip()
    gl.smooth = 20.0
    A.add(gl.move(0, 0.13, zc), bone=bone, look=gill, keep=K)
    for k in range(18):                                             # pale warts on the cap
        a = rng.uniform(0, 2 * math.pi); r_ = R_ * math.sqrt(rng.uniform(0.04, 0.8))
        zz = float(np.interp(r_, PR, PZ))
        s_ = rng.uniform(0.03, 0.065)
        A.add(KX.lump(s_, s_ * 0.9, s_ * 0.4, seed=40 + k, amp=0.25, n=7, m=4).move(r_ * math.cos(a), 0.13 + r_ * math.sin(a), zc + zz + 0.004), bone=bone, look=stalk, smooth=70, keep=K)
    for k, (a_deg, r_, s_) in enumerate(((40.0, 0.46, 0.13), (200.0, 0.40, 0.10), (290.0, 0.5, 0.085))):          # small caps growing on the big one
        a = rad(a_deg)
        zz = float(np.interp(R_ * r_, PR, PZ))
        c2, g2 = KB2.fungus_cap(R=s_, h=s_ * 0.55, seed=k)
        base = V(R_ * r_ * math.cos(a), 0.13 + R_ * r_ * math.sin(a), zc + zz)
        A.add(cyl(base - V(0, 0, 0.01), base + V(0, 0, s_ * 0.6), s_ * 0.22, n=6), bone=bone, look=stalk, smooth=70, keep=K)
        A.add(c2.move(base + V(0, 0, s_ * 0.6)), bone=bone, look=cap, smooth=70, keep=K)
        A.add(g2.move(base + V(0, 0, s_ * 0.6)), bone=bone, look=gill, smooth=70, keep=K)
    for k in range(lamps):                                          # lantern-fruit on threads round the rim
        a = 2 * math.pi * (k + 0.3) / lamps
        L_ = rng.uniform(0.13, 0.30)
        p0 = V(R_ * 0.955 * math.cos(a), 0.13 + R_ * 0.955 * math.sin(a), zc - 0.04)
        A.add(tube([p0 + V(0, 0, 0.02), p0 + V(0, 0, -L_)], 0.0045, n=4), bone=bone, look=stalk, keep=K)
        fr = revolve([(0.0, 0.0), (0.02, -0.006), (0.04, -0.03), (0.05, -0.058), (0.05, -0.082), (0.038, -0.108), (0.018, -0.122), (0.0, -0.126)], n=10)
        A.add(fr.move(p0 + V(0, 0, -L_)), bone=bone, look=lamp, smooth=180, keep=K)
        A.add(revolve([(0.0, 0.012), (0.026, 0.004), (0.032, -0.018), (0.02, -0.014), (0.0, -0.004)], n=7).move(p0 + V(0, 0, -L_)), bone=bone, look=stalk, smooth=60, keep=K)


# ================================================================================================ ash: the drift-colossus
def emerge(A, bone, at, yaw, pitch, sc, look, roll=0.0, ratio=0.11, arms=75.0, melt=2, gaunt=1.0, K=None):
    """one half-emerged figure (head + torso + arms) pressed out of a mass: rigid on `bone`, waist at `at`"""
    import asm_parts7 as P7
    b = P7.half_body(ratio=ratio, arms_down=arms, gaunt=gaunt)
    if melt:
        M.smooth_verts(b, int(melt), 0.5)
    piv = V(0, 0.02, 1.0)
    b.scale(sc, pivot=piv).rot('x', pitch, pivot=piv)
    if roll:
        b.rot('y', roll, pivot=piv)
    b.rot('z', yaw, pivot=piv).move(V(at) - piv)
    b.W = None
    K = b.center() if K is None else K
    A.add(b, bone=bone, look=look, smooth=180, keep=K)
    return K


@part
def drift_mass(A, look='ashcrust', pale='ashpale', spark='spark', seed=7, ratio=0.075):
    """a drift that stood up: the surface of the giant is made of half-emerged ash-casts -- faces, backs and a few
    reaching arms pressed outward like a relief -- bedded in big soft drifts so that the whole is one slope-shouldered
    mound with no neck; fists that are knots of fused bodies, dull sparks in the deep creases between the figures and a
    ragged plume of ash trailing from the back."""
    R = A.R
    rng = np.random.default_rng(seed)
    spots = [  # bone, waist point, yaw, pitch, scale, arms
        ('spine_03', (0.09, 0.14, 1.04), 168, 18, 0.80, 84), ('spine_03', (-0.12, 0.13, 1.08), -160, 24, 0.74, 84), ('spine_02', (0.02, 0.165, 0.82), 12, -12, 0.86, 86),
        ('clavicle_l', (0.24, 0.02, 1.10), 95, 22, 0.72, 20), ('clavicle_r', (-0.24, 0.03, 1.07), -98, 26, 0.70, 84),
        ('spine_03', (0.10, -0.10, 1.00), 18, 12, 0.66, 86), ('spine_02', (-0.09, -0.11, 0.84), -14, 16, 0.70, 84),
        ('spine_02', (0.18, 0.02, 0.80), 86, 24, 0.72, 84), ('spine_02', (-0.185, 0.04, 0.84), -82, 22, 0.70, 30),
        ('spine_03', (0.02, 0.04, 1.18), 186, 50, 0.80, 84), ('spine_03', (-0.05, -0.02, 1.22), -8, 46, 0.70, 84),
        ('pelvis', (0.12, 0.10, 0.62), 150, 18, 0.66, 86), ('pelvis', (-0.13, 0.09, 0.60), -150, 22, 0.64, 86), ('pelvis', (0.0, -0.10, 0.62), 4, 14, 0.60, 86),
    ]
    for k, (bone, at, yaw, pitch, sc, arms) in enumerate(spots):
        Kf = emerge(A, bone, at, yaw, pitch, sc, look if k % 3 else pale, ratio=ratio, arms=float(arms), melt=3)
        c = V(at) + V(0.05 * math.sin(rad(yaw)), -0.05 * math.cos(rad(yaw)), 0.06 * sc)          # sparks in the crease below each figure
        if k % 2 == 0:
            A.add(KX.lump(0.03, 0.03, 0.02, seed=k, amp=0.3, n=7, m=4).move(c), bone=bone, look=spark, smooth=80, keep=Kf)
    drifts = [  # x, y, z, rx, ry, rz, bone: the drift itself, bedding the casts
        (0.0, 0.04, 1.55, 0.25, 0.21, 0.15, 'spine_03'), (0.20, 0.03, 1.49, 0.16, 0.16, 0.12, 'clavicle_l'), (-0.20, 0.03, 1.49, 0.16, 0.16, 0.12, 'clavicle_r'),
        (0.0, 0.15, 1.38, 0.24, 0.15, 0.20, 'spine_03'), (0.0, -0.10, 1.34, 0.22, 0.13, 0.17, 'spine_03'), (0.0, 0.12, 1.10, 0.25, 0.16, 0.20, 'spine_02'),
        (0.0, -0.10, 1.08, 0.23, 0.14, 0.18, 'spine_02'), (0.17, 0.02, 1.22, 0.13, 0.17, 0.22, 'spine_02'), (-0.17, 0.02, 1.22, 0.13, 0.17, 0.22, 'spine_02'),
        (0.0, 0.03, 0.90, 0.25, 0.20, 0.15, 'pelvis'), (0.27, 0.02, 1.43, 0.12, 0.12, 0.10, 'upperarm_l'), (-0.27, 0.02, 1.43, 0.12, 0.12, 0.10, 'upperarm_r')]
    for k, (bx, by, bz, rx, ry, rz, bn) in enumerate(drifts):
        A.add(KX.lump(rx, ry, rz, seed=31 + k, amp=0.26, freq=6.0, n=12, m=7).move(bx, by, bz), bone=bn, look=look if k % 4 else pale, smooth=180, keep=True)
    for sx in (1, -1):                                              # fists: knots of fused bodies
        s = '_l' if sx > 0 else '_r'
        w = R.H('hand' + s)
        Kf = w + V(sx * 0.09, 0, 0)
        A.add(KX.lump(0.19, 0.165, 0.16, seed=5 + (sx > 0), amp=0.3, freq=7.0, n=12, m=7).move(Kf), bone='hand' + s, look=look, smooth=180, keep=Kf)
        for k, (yaw, roll, dz) in enumerate(((0, sx * 100, 0.05), (180, sx * 80, -0.03), (90 * sx, sx * 120, 0.0))):
            emerge(A, 'hand' + s, Kf + V(-sx * 0.14, 0.02 * (k - 1), dz), yaw, 18, 0.46, pale if k == 1 else look, roll=float(roll), ratio=ratio * 0.7, arms=86.0, K=Kf, melt=3)
        for t in (0.25, 0.7):                                       # casts riding on the forearms
            p = KC3.BP('lowerarm' + s, t)
            emerge(A, 'lowerarm' + s, p + V(-sx * 0.1, 0.0, 0.03), 180 if t < 0.5 else 0, 10, 0.44, look, roll=sx * 96.0, ratio=ratio * 0.7, arms=86.0, melt=3)
            A.add(KX.lump(0.10, 0.095, 0.09, seed=60 + int(t * 10), amp=0.28, n=9, m=5).move(p), bone='lowerarm' + s, look=look, smooth=180, keep=True)
    # the plume trailing from the back: thin ragged tongues, longest in the middle
    base = V(0.0, 0.20, 1.50)
    for k in range(11):
        u = (k - 5) / 5.0
        L_ = (0.34 + 0.44 * (1 - u * u)) * rng.uniform(0.8, 1.15)
        bl = flame_blade(L_, rng.uniform(0.045, 0.085), th=0.03, curl=rng.uniform(-0.3, 0.3), n=10, seed=k)
        bl.rough(0.012, 11.0, seed=float(k))
        bl.rot('x', -rng.uniform(52, 76)).rot('z', 26.0 * u + rng.uniform(-8, 8))
        bl.move(base + V(0.17 * u, rng.uniform(-0.03, 0.05), rng.uniform(-0.10, 0.06) - 0.06 * u * u))
        sway_weights(bl, base[1] + 0.1, base[1] + 0.7, top='spine_03', low='pelvis', amt=0.5, coord=bl.v[:, 1])
        A.add(bl, look=pale if k % 3 else look, smooth=40)


# ================================================================================================ wax: the tallow-mound
@part
def tallow_mound(A, look='tallow', wick='wick', seed=5, wicks=12, lit=True):
    """sealed chandlers who stood too close and ran together: a slumped cone of wax taller than a door (from the floor
    to a leaning tip, pelvis-rigid below the hips) with heads, shoulders and praying hands half-sunk in its surface,
    thick runs and a puddle skirt, a comb of dead wicks down the ridge of its back (one may still burn) and two arms
    that are whole bodies stretched thin (skinned to the arm bones, the head at the wrist).  Use with a hidden body."""
    import asm_parts7 as P7
    R = A.R
    rng = np.random.default_rng(seed)
    key = np.array([(0.0, 0.06, 0.54, 0.52), (0.04, 0.06, 0.50, 0.48), (0.22, 0.05, 0.40, 0.375), (0.55, 0.04, 0.335, 0.305), (0.95, 0.035, 0.295, 0.265),
                    (1.25, 0.03, 0.272, 0.238), (1.45, 0.03, 0.25, 0.212), (1.62, 0.04, 0.19, 0.168), (1.80, 0.055, 0.13, 0.118), (1.93, 0.075, 0.07, 0.064),
                    (1.99, 0.085, 0.012, 0.012)])
    sec = lambda z: tuple(float(np.interp(z, key[:, 0], key[:, k])) for k in (1, 2, 3))
    runs = lambda th, z: 1.0 + 0.055 * np.sin(7 * th + 2.2 * z + 1.0) + 0.035 * np.sin(13 * th - 3.0 * z)
    g = M.loft_z([(z, 0, cy, rx, ry) for z, cy, rx, ry in key], n=30, cap0=False, cap1=True, mod=runs)
    lowm = g.v[:, 2] < 0.03
    g.v[lowm, :2] *= (1 + 0.10 * np.sin(5 * np.arctan2(g.v[lowm, 1], g.v[lowm, 0])))[:, None]         # the puddle's lobes
    g.rough(0.016, 5.0, seed=float(seed))
    g.rough(0.005, 17.0, seed=float(seed) + 3.0)
    g.v[:, 2] = np.maximum(g.v[:, 2], 0.0)
    M.weights_by(g, g.v[:, 2], [(0.95, {'pelvis': 1.0}), (1.10, {'spine_01': 1.0}), (1.25, {'spine_02': 1.0}), (1.40, {'spine_03': 1.0})])
    A.add(g, look=look, smooth=80)
    A.refs['mound'] = g

    def bone_at(z):
        return 'pelvis' if z < 1.0 else 'spine_01' if z < 1.17 else 'spine_02' if z < 1.32 else 'spine_03'

    def surf(a_deg, z, out=0.0):
        cy, rx, ry = sec(z)
        a = rad(a_deg)
        return V((rx + out) * math.cos(a), cy + (ry + out) * math.sin(a), z)

    # heads and shoulders half-sunk in the wax (a_deg: -90 = front)
    for k, (a_deg, z, sc, pitch) in enumerate(((-70.0, 1.22, 0.84, 10), (35.0, 1.02, 0.80, 16), (150.0, 1.30, 0.78, 8), (-150.0, 0.86, 0.82, 14), (95.0, 0.62, 0.86, 18),
                                               (-25.0, 0.70, 0.78, 12))):
        p = surf(a_deg, z + 0.42 * sc, out=-0.115)
        b = P7.half_body(ratio=0.10, arms_down=86.0, gaunt=0.6, jaw=8.0)
        M.smooth_verts(b, 3, 0.5)
        piv = V(0, 0.02, 1.0)
        b.scale(sc, pivot=piv).rot('x', pitch, pivot=piv).rot('z', a_deg + 90.0, pivot=piv).move(p - V(0, 0, 0.42 * sc) - piv)
        b.W = None
        A.add(b, bone=bone_at(z + 0.3), look=look, smooth=180)
    # praying hands pressed out of the surface
    for k, (a_deg, z) in enumerate(((-95.0, 0.98), (60.0, 1.36), (175.0, 0.92), (-40.0, 1.46))):
        hl = KW.praying_hand(wrist=(0.016, -0.02, 0.0), fingers=(0.0, -0.45, 0.9), ratio=0.3)
        hr = hl.copy().mirror()
        pr = join([hl, hr])
        M.smooth_verts(pr, 1, 0.4)
        pr.rot('z', a_deg + 90.0).move(surf(a_deg, z, out=-0.03))
        A.add(pr, bone=bone_at(z), look=look, smooth=180)
    # runs of wax down the cone
    for k in range(26):
        a_deg = 360.0 * (k + rng.uniform(0.15, 0.85)) / 26
        z0 = rng.uniform(0.5, 1.75); L_ = rng.uniform(0.18, 0.5)
        zs = [z0, z0 - L_ * 0.4, z0 - L_ * 0.8, max(0.02, z0 - L_)]
        pts = [surf(a_deg + 3 * math.sin(i * 1.7 + k), zz, out=0.004) for i, zz in enumerate(zs)]
        tb = tube(np.array(pts), [0.014, 0.02, 0.024, 0.008], n=6)
        M.weights_by(tb, tb.v[:, 2], [(0.95, {'pelvis': 1.0}), (1.10, {'spine_01': 1.0}), (1.25, {'spine_02': 1.0}), (1.40, {'spine_03': 1.0})])
        A.add(tb, look=look, smooth=80)
    # the comb of dead wicks down the ridge of the back
    top_w = None
    for k in range(wicks):
        z = 1.985 - 0.058 * k - 0.004 * k * k * 0.12
        p = surf(90.0 + 4.0 * math.sin(k * 1.9), z, out=-0.004) if k else V(0, sec(1.99)[0], 1.99)
        up = unit(V(0.06 * math.sin(k * 2.3), 0.35 if k else 0.0, 1.0))
        hgt = rng.uniform(0.07, 0.13)
        A.add(M.loft_z([(0.0, 0, 0, 0.026, 0.026), (0.02, 0, 0, 0.016, 0.016), (0.034, 0, 0, 0.0, 0.0)], n=7).tf(M.rot_to((0, 0, 1), up)).move(p - up * 0.006), bone='spine_03',
              look=look, smooth=70)
        A.add(tube([p + up * 0.02, p + up * (0.02 + hgt * 0.6) + V(0.008, 0, 0), p + up * (0.02 + hgt) + V(0.018 * math.cos(k), 0.012, 0)], 0.0052, n=5), bone='spine_03', look=wick)
        if k == 0:
            top_w = p + up * (0.02 + hgt)
    A.refs['dome_top'] = top_w
    # two arms that are bodies: waist inside the cone, the trunk for an arm, the head for a fist (skinned to the arm bones)
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        b = P7.half_body(ratio=0.2, arms_down=86.0, gaunt=0.6, jaw=10.0)
        b.v[:, 0] *= 1.0 - 0.42 * np.clip(b.wsum(arm_bones()) * 1.2, 0, 1)                         # its own arms pressed to its sides
        M.smooth_verts(b, 2, 0.5)
        sh = R.H('upperarm' + s)
        L_ = float(np.linalg.norm(R.H('hand' + s) - sh)) + 0.12
        kz = (L_ + 0.12) / 0.78
        ax = V(sx, 0, 0)
        q = b.v.copy()
        # figure Z -> along the arm, figure Y stays (its face looks forward when the arm hangs), figure X -> -/+ Z
        b.v = np.outer((q[:, 2] - 0.98) * kz - 0.12, ax) + np.outer((q[:, 1] - 0.02) * 0.9, V(0, 1, 0)) + np.outer(q[:, 0] * 0.74, V(0, 0, -sx)) + sh
        b.recalc()
        # weights by the position ALONG the arm (a nearest-surface transfer gives the wide figure torso weights: the arm
        # then stays spread like a wing while the bones hang)
        along = (b.v - sh) @ ax
        el = float((R.H('lowerarm' + s) - sh) @ ax); wr = float((R.H('hand' + s) - sh) @ ax)
        M.weights_by(b, along, [(-0.11, {'clavicle' + s: 1.0}), (0.02, {'upperarm' + s: 1.0}), (el - 0.05, {'upperarm' + s: 1.0}), (el + 0.05, {'lowerarm' + s: 1.0}),
                                (wr - 0.03, {'lowerarm' + s: 1.0}), (wr + 0.05, {'hand' + s: 1.0})])
        A.add(b, look=look, smooth=180)
        for t, rr in ((0.25, 0.07), (0.8, 0.06)):                   # wax runs hanging from the arm
            p0 = KC3.BP('lowerarm' + s, t) + V(0, 0.0, -rr)
            A.add(tube([p0 + V(0, 0, 0.03), p0 + V(sx * 0.02, 0, -0.05), p0 + V(sx * 0.03, 0, -0.12)], [0.02, 0.014, 0.004], n=5), bone='lowerarm' + s, look=look, smooth=80)


# ================================================================================================ small things
@part
def band_glow(A, look='glow', z=1.538, r=0.086, w=0.007, bone='neck_01', cy=-0.012):
    """a thin glowing line round a joint (the neck joint of a statue): a closed ring, tight on the surface"""
    A.add(torus(r, w, n=18, m=4, ry=r * 1.08).rot('x', -8).move(0, cy, z), bone=bone, look=look)
