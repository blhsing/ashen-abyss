"""asm_parts4.py -- whole-body shells for the stage-2 bases that are not flesh: bell, brass attendant, charred wood,
frozen flame, fungus growths.  Used with body show=[] (the base body is only the hidden reference).  Same rules as
asm_parts.py."""
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
from monlib import V, tube, ellipsoid, box, join, revolve, torus, hang, arc_pts, band, unit
from asm_parts import part, girth_of, cyl, item_parts
from asm_parts2 import z_weights, ring_disc
import asm


def bark_mod(seed=0, k=9, amp=0.07):
    def f(th, z):
        return 1.0 + amp * np.sin(k * th + 4.0 * z + seed) + 0.5 * amp * np.sin((k + 4) * th - 7.0 * z + 2 * seed)
    return f


def limb_tube(A, bones, r, look, n=7, skin=2, wob=0.0, seed=0, **kw):
    """tapered tube through the joints of a bone chain (rest pose), skinned to it. r = radius per joint (len(bones)+1)"""
    R = A.R
    pts = [R.H(bones[0])] + [R.T(b) for b in bones]
    m = 4 * len(bones) + 1
    P = M.polyline(pts, m)
    if wob:
        rng = np.random.default_rng(seed)
        P[1:-1] += rng.uniform(-wob, wob, (m - 2, 3))
    rr = np.interp(np.linspace(0, 1, m), np.linspace(0, 1, len(r)), r)
    t = tube(P, rr, n=n)
    KC.skin_cloth(t, body=A.rest, smooth=skin, limit=bones + [A.R.parent[bones[0]]])
    return A.add(t, look=look, **kw)


# ================================================================================================ bell
@part
def body_bell(A, look='bronze', R=0.37, z0=1.0, z1=1.87, bone='spine_03', strap='rust', rope='rope', rim='glow'):
    """a bronze bell locked down over head and shoulders to the elbows; a padlocked iron strap between the legs holds it
    on and a frayed bell-rope trails from the crown"""
    H = z1 - z0
    prof = [(R * 0.985, 0.0), (R * 1.0, 0.012), (R * 1.02, 0.035), (R * 0.95, 0.09), (R * 0.80, H * 0.33), (R * 0.68, H * 0.6), (R * 0.60, H * 0.8),
            (R * 0.47, H * 0.93), (R * 0.22, H * 0.995), (0.0, H)]
    b = revolve(prof, n=20, cap=False).scale((1.0, 0.9, 1.0))
    d = np.linalg.norm(b.v - np.array([R * 0.5, -R * 0.55, H * 0.4]), axis=1)
    b.v[:, 1] += np.exp(-(d / 0.09) ** 2) * 0.02                       # a dent
    A.add(b.move(0, 0.01, z0), bone=bone, look=look, smooth=60)
    inner = revolve([(R * 0.985, 0.0), (R * 0.93, 0.02), (R * 0.76, H * 0.33), (R * 0.5, H * 0.75), (0.0, H * 0.8)], n=14, cap=False).scale((1.0, 0.9, 1.0)).flip()
    A.add(inner.move(0, 0.01, z0), bone=bone, look='void')
    for zz, rr in ((0.012, R * 1.012), (H * 0.30, R * 0.83), (H * 0.335, R * 0.80)):
        A.add(torus(rr, 0.008, n=20, m=4, ry=rr * 0.9).move(0, 0.01, z0 + zz), bone=bone, look=rim if (rim and zz < 0.02) else look)
    top = V(0, 0.01, z1)
    A.add(torus(0.04, 0.012, n=10, m=5).rot('y', 90).move(top + V(0, 0, 0.03)), bone=bone, look=look)
    rp = M.polyline([top + V(0, 0.0, 0.05), top + V(0, 0.16, 0.0), top + V(0, 0.36, -0.35), V(0, 0.47, 0.9), V(0.03, 0.52, 0.45), V(0.0, 0.62, 0.2)], 16)
    t = tube(rp, 0.014, n=6)
    z_weights(t)
    A.add(t, look=rope, smooth=70)
    fr = hang(np.array([rp[-1] + V(-0.02, 0, 0), rp[-1], rp[-1] + V(0.02, 0, 0)]), 0.12, rows=4, out=np.tile((0.0, 1.0, 0.0), (3, 1)), rag=0.5, seed=2, tatter=0.5)
    fr.W = np.tile(t.W[-1], (len(fr.v), 1)).astype(np.float32)          # the frayed end moves exactly like the rope's last ring
    A.add(fr, look=rope)
    sp = M.polyline([(0, -R * 0.9 * 0.985 + 0.01, z0 + 0.03), (0, -0.20, 0.93), (0, -0.07, 0.845), (0, 0.06, 0.84), (0, 0.19, 0.93), (0, R * 0.9 * 0.985 + 0.01, z0 + 0.03)], 14)
    A.add(tube(sp, 0.005, n=4, r2=0.024, up=(1, 0, 0), phase=math.pi / 4), bone='pelvis', look=strap, smooth=30)
    A.add(box(0.06, 0.03, 0.075, bevel=0.008).move(0, -R * 0.9 - 0.01, z0 + 0.0), bone='pelvis', look=strap)
    A.add(torus(0.024, 0.006, n=8, m=4).rot('y', 90).move(0, -R * 0.9 - 0.012, z0 + 0.05), bone='pelvis', look=strap)


# ================================================================================================ brass attendant
@part
def pot_body(A, look='brass', z0=0.87, z1=1.50, R=0.25, glow='glow', bone='spine_02'):
    """round riveted pot for a body (rigid), collar plate on top, pilot-light port in the chest"""
    H = z1 - z0
    prof = [(0.0, 0.0), (R * 0.55, 0.0), (R * 0.8, H * 0.1), (R, H * 0.38), (R * 0.98, H * 0.6), (R * 0.78, H * 0.85), (R * 0.45, H * 0.97), (R * 0.42, H), (0.0, H)]
    p = revolve(prof, n=18).scale((1.0, 0.88, 1.0))
    A.add(p.move(0, 0.01, z0), bone=bone, look=look, smooth=50)
    for zz, rr in ((H * 0.38, R + 0.004), (H * 0.72, R * 0.9)):
        A.add(torus(rr, 0.009, n=18, m=4, ry=rr * 0.88).move(0, 0.01, z0 + zz), bone=bone, look=look)
        for a in np.linspace(0, 2 * math.pi, 14, endpoint=False):
            pt = V(rr * math.cos(a), 0.01 + rr * 0.88 * math.sin(a), z0 + zz - 0.03)
            A.add(KC3.rivet(pt, unit(V(math.cos(a), math.sin(a), 0)), r=0.007), bone=bone, look=look)
    A.add(revolve([(0.045, 0.0), (0.035, 0.012), (0.0, 0.014)], n=10).rot('x', 90).move(0, 0.01 - R * 0.88 * 0.97, z0 + H * 0.55), bone=bone, look=look)
    if glow:
        A.add(ellipsoid(0.022, 0.008, 0.022, n=8, m=4).move(0, 0.01 - R * 0.88 * 0.97 - 0.012, z0 + H * 0.55), bone=bone, look=glow)
    A.add(revolve([(R * 0.44, 0.0), (R * 0.3, 0.035), (0.05, 0.05), (0.05, 0.09), (0.0, 0.09)], n=12).move(0, 0.005, z1 - 0.005), bone='spine_03', look=look, smooth=50)


@part
def stick_limbs(A, look='brass', r=0.02, ball=0.036):
    """stick limbs of a construct: rods along the limb bones with ball joints, block feet and jointed block fingers"""
    R = A.R
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        for bn in ('upperarm', 'lowerarm', 'thigh', 'calf'):
            h = R.H(bn + s); t = R.T(bn + s)
            A.add(cyl(h, t, r if bn in ('upperarm', 'lowerarm') else r * 1.2, n=7), bone=bn + s, look=look, smooth=50)
            A.add(ellipsoid(ball, ball, ball, n=9, m=6).move(h), bone=bn + s, look=look, smooth=60)
            A.add(torus(r * 1.5, 0.006, n=8, m=4).tf(M.rot_to((0, 0, 1), unit(t - h))).move(h + (t - h) * 0.5), bone=bn + s, look=look)
        A.add(ellipsoid(ball * 0.9, ball * 0.9, ball * 0.9, n=8, m=5).move(R.H('hand' + s)), bone='hand' + s, look=look, smooth=60)
        A.add(box(0.07, 0.085, 0.03, bevel=0.006).move(R.H('hand' + s) + V(sx * 0.07, 0, 0)), bone='hand' + s, look=look)
        A.add(ellipsoid(ball, ball, ball, n=8, m=5).move(R.H('foot' + s)), bone='foot' + s, look=look, smooth=60)
        A.add(box(0.10, 0.22, 0.05, bevel=0.012).move(sx * 0.089, -0.055, 0.025), bone='foot' + s, look=look, smooth=40)
    KC3.finger_blocks(A.C, A.lk(look), w=0.015, h=0.014)
    A.add(cyl((0, 0.01, 0.86), (0, 0.01, 0.94), 0.06, n=10), bone='pelvis', look=look, smooth=50)
    A.add(cyl((-0.11, 0.0, 0.925), (0.11, 0.0, 0.925), 0.03, n=8), bone='pelvis', look=look, smooth=50)


def key_parts(shaft_len=0.2, bow=0.11, r=0.016):
    """butterfly key: stem along +Z from the origin, two wing loops at the top (in the XZ plane), a toothed bit below"""
    ps = [cyl((0, 0, 0), (0, 0, shaft_len), r, n=8)]
    for sx in (1, -1):
        w = torus(bow, r * 0.85, n=14, m=5, ry=bow * 0.72).rot('x', 90)
        w.v[:, 0] *= 1.0
        ps.append(w.rot('y', sx * 14).move(sx * bow * 0.86, 0, shaft_len + bow * 0.55))
    ps.append(ellipsoid(r * 2.0, r * 1.6, r * 2.0, n=8, m=5).move(0, 0, shaft_len))
    return join(ps)


@part
def key_head(A, look='brass', bow=0.125, z=1.50, turn=24.0):
    """instead of a head: a large butterfly winding key standing in the collar"""
    k = key_parts(0.17, bow, 0.017).rot('z', turn).move(0, 0.0, z + 0.05)
    A.add(k, bone='Head', look=look, smooth=50)


# ================================================================================================ charred wood
@part
def stump_body(A, look='charwood', z0=0.74, z1=1.50, R=0.27, rim='ember', seed=3):
    """sawn-off stump for a body: fluted bark, a perfectly flat axe-cut top with growth rings and an ember line round the
    cut, two legs that split into two splayed root-toes each, two short broken branches for arms"""
    secs = [(z0 - 0.12, 0, 0.02, R * 0.72, R * 0.70), (z0, 0, 0.02, R * 1.12, R * 1.06), (z0 + 0.12, 0, 0.015, R * 0.98, R * 0.92),
            (z0 + 0.4, 0, 0.01, R * 0.94, R * 0.88), (z1 - 0.03, 0, 0.01, R * 0.96, R * 0.9), (z1, 0, 0.01, R * 0.93, R * 0.87)]
    t = M.loft_z(secs, n=22, mod=bark_mod(seed, 9, 0.06))
    t.rough(0.006, 14.0, seed=float(seed))
    t.v[t.v[:, 2] > z1 - 0.004, 2] = z1
    z_weights(t)
    A.add(t, look=look, smooth=35)
    for k, rr in enumerate((0.25, 0.47, 0.68, 0.86)):
        A.add(torus(R * rr, 0.004, n=18, m=3, ry=R * rr * 0.93).move(0.012 * k, 0.01 + 0.008 * k, z1 + 0.001), bone='spine_03', look=look)
    if rim:
        A.add(torus(R * 0.945, 0.007, n=22, m=4, ry=R * 0.885).move(0, 0.01, z1 - 0.004), bone='spine_03', look=rim)
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        limb_tube(A, ['thigh' + s, 'calf' + s], [0.105, 0.085, 0.06], look, n=8, wob=0.012, seed=seed + (sx > 0), smooth=35)
        ank = A.R.H('foot' + s)
        for (dx, dy, bn) in ((0.07, -0.26, 'ball'), (0.13, 0.13, 'foot')):
            pts = M.polyline([ank + V(0, 0, 0.03), ank + V(sx * dx * 0.5, dy * 0.5, -0.03), ank + V(sx * dx, dy, -0.085)], 6)
            A.add(M.horn(pts, 0.05, n=6, r_tip=0.012), bone=bn + s if bn == 'ball' else 'foot' + s, look=look, smooth=35)
        sh = A.R.H('upperarm' + s)
        ln = 0.30 if sx > 0 else 0.20
        br = tube(M.polyline([sh + V(-sx * 0.03, 0, 0), sh + V(sx * ln * 0.5, 0.01, 0.012), sh + V(sx * ln, -0.01, 0.0)], 6), np.linspace(0.05, 0.034, 6), n=7)
        A.add(br, bone='upperarm' + s, look=look, smooth=35)
        for k in range(4):
            a = k * 1.7
            sp = M.cone_spike(0.012, 0.05 + 0.02 * (k % 2), n=4).rot('y', sx * 90).move(sh + V(sx * ln, 0.02 * math.cos(a), 0.02 * math.sin(a)))
            A.add(sp, bone='upperarm' + s, look=look, smooth=30)


@part
def trunk_body(A, look='charwood', glow='ember', seed=4, long_arm='l'):
    """tall thin burnt trunk split up the middle into two legs, a jagged lightning-split crown instead of a head, one arm
    a long forked branch, the other snapped short; an ember glow deep in the split"""
    secs = [(0.90, 0, 0.02, 0.150, 0.118), (1.05, 0, 0.015, 0.158, 0.122), (1.30, 0, 0.012, 0.168, 0.122), (1.44, 0, 0.03, 0.225, 0.13), (1.56, 0, 0.012, 0.165, 0.115),
            (1.66, 0, 0.0, 0.118, 0.10), (1.74, 0, -0.005, 0.105, 0.09)]
    t = M.loft_z(secs, n=18, mod=bark_mod(seed, 7, 0.07), cap1=True)
    t.rough(0.006, 13.0, seed=float(seed))
    z_weights(t, head=1.70)
    A.add(t, look=look, smooth=35)
    rng = np.random.default_rng(seed)
    for k in range(7):                                           # the split crown
        a = 2 * math.pi * k / 7 + rng.uniform(-0.2, 0.2)
        hgt = rng.uniform(0.14, 0.42) * (1.5 if k in (1, 4) else 1.0)
        p0 = V(0.085 * math.cos(a), -0.005 + 0.07 * math.sin(a), 1.70)
        d = unit(V(0.35 * math.cos(a), 0.3 * math.sin(a), 1.0))
        sh = blade([(0.0, 0.05, 0.05, 0.03), (hgt * 0.5, 0.036, 0.03, 0.02), (hgt, 0.0, 0.004, 0.004)])
        sh.rot('x', 90).rot('z', math.degrees(a) + 90)
        sh.tf(M.rot_to((0, 0, 1), d))
        A.add(sh.move(p0), bone='Head', look=look, smooth=30)
    if glow:                                                      # ember light in the split up the middle
        A.add(box(0.016, 0.03, 0.52).move(0.004, -0.108, 1.24), bone='spine_02', look=glow)
        for sx in (-1, 1):
            A.add(box(0.026, 0.04, 0.56, bevel=0.006).move(sx * 0.026, -0.112, 1.24).rot('z', sx * 6, pivot=(0, -0.11, 1.24)), bone='spine_02', look=look, smooth=30)
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        limb_tube(A, ['thigh' + s, 'calf' + s], [0.092, 0.07, 0.05], look, n=8, wob=0.01, seed=seed + (sx > 0), smooth=35)
        ank = A.R.H('foot' + s)
        for (dx, dy, bn) in ((0.03, -0.22, 'ball'), (-0.02, 0.10, 'foot'), (0.10, -0.02, 'foot')):
            pts = M.polyline([ank + V(0, 0, 0.03), ank + V(sx * dx * 0.5, dy * 0.5, -0.04), ank + V(sx * dx, dy, -0.09)], 6)
            A.add(M.horn(pts, 0.04, n=6, r_tip=0.01), bone=bn + s, look=look, smooth=35)
        if s[1] == long_arm:
            limb_tube(A, ['upperarm' + s, 'lowerarm' + s, 'hand' + s], [0.055, 0.042, 0.034, 0.028], look, n=7, wob=0.012, seed=seed + 7, smooth=35)
            w = A.R.T('hand' + s)
            for (dy, dz, ln) in ((-0.10, 0.03, 0.34), (0.10, 0.05, 0.26), (0.0, -0.08, 0.2)):
                pts = M.polyline([w - V(sx * 0.03, 0, 0), w + V(sx * ln * 0.5, dy * 0.4, dz * 0.4), w + V(sx * ln, dy, dz)], 6)
                A.add(M.horn(pts, 0.024, n=5, r_tip=0.004), bone='hand' + s, look=look, smooth=35)
        else:
            sh = A.R.H('upperarm' + s)
            br = tube(M.polyline([sh + V(-sx * 0.04, 0, 0), sh + V(sx * 0.10, 0.01, 0.01), sh + V(sx * 0.20, 0, 0.0)], 5), np.linspace(0.052, 0.04, 5), n=7)
            A.add(br, bone='upperarm' + s, look=look, smooth=35)
            for k in range(4):
                a = k * 1.7
                A.add(M.cone_spike(0.012, 0.06 + 0.025 * (k % 2), n=4).rot('y', sx * 90).move(sh + V(sx * 0.20, 0.022 * math.cos(a), 0.022 * math.sin(a))),
                      bone='upperarm' + s, look=look, smooth=30)


# ================================================================================================ frozen flame
def flame_blade(L_, w, th=0.03, curl=0.25, n=9, seed=0):
    """tapering flame tongue along +Z from the origin (width X, thickness Y), wavy, curling to +X at the tip"""
    st = []
    for i in range(n):
        t = i / (n - 1)
        ww = w * (0.55 + 0.75 * math.sin(math.pi * min(1.0, t * 1.5 + 0.12))) * (1 - t ** 2.2)
        st.append((L_ * t, max(ww, 0.002) * (1 + 0.2 * math.sin(7 * t + seed)), max(ww, 0.002) * (1 - 0.2 * math.sin(7 * t + seed)), th * (1 - 0.8 * t)))
    b = blade(st)
    b.rot('x', 90)
    b.v[:, 0] += curl * L_ * (b.v[:, 2] / L_) ** 2
    return b


from monlib import blade


@part
def flame_spirit(A, look='ice', look2='ice2', core='ember', tongues=10, seed=3):
    """a man-high frozen flame with a torso: an inverted cone of glass balanced on a point whose rim flares upward into
    tongues, a faceted trunk, two arms that are single long flame-blades, a tall split flame-tongue for a head and an
    orange core in the chest.  Use with a hidden body (show=[])."""
    rng = np.random.default_rng(seed)
    cone = M.loft_z([(0.0, 0, 0.02, 0.012, 0.012), (0.3, 0, 0.02, 0.07, 0.065), (0.7, 0, 0.02, 0.19, 0.17), (1.0, 0, 0.02, 0.30, 0.27), (1.06, 0, 0.02, 0.22, 0.2)],
                    n=9, phase=0.3)
    A.add(cone, bone='pelvis', look=look, smooth=18)
    for k in range(tongues):
        a = 2 * math.pi * (k + 0.5) / tongues
        L_ = rng.uniform(0.22, 0.5)
        b = flame_blade(L_, 0.085, th=0.035, curl=0.3, seed=k)
        b.rot('z', math.degrees(a))                         # curl outwards
        p0 = V(0.27 * math.cos(a), 0.02 + 0.245 * math.sin(a), 0.93)
        A.add(b.move(p0), bone='pelvis', look=look2 if k % 3 == 0 else look, smooth=18)
    trunk = M.loft_z([(0.98, 0, 0.015, 0.125, 0.11), (1.2, 0, 0.01, 0.12, 0.10), (1.38, 0, 0.012, 0.185, 0.125), (1.47, 0, 0.012, 0.17, 0.11), (1.53, 0, 0.005, 0.07, 0.065)],
                      n=8, phase=0.2)
    z_weights(trunk)
    A.add(trunk, look=look, smooth=18)
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        L_ = 1.05
        b = flame_blade(L_, 0.085, th=0.04, curl=0.0, n=11, seed=2 + sx)
        b.rot('y', 90)                                      # +Z -> +X
        b.v[:, 2] -= 0.10 * (b.v[:, 0] / L_) ** 2
        b.move(0.16, 0.065, 1.445)
        x = b.v[:, 0]
        M.weights_from(b, {'clavicle_l': M.smoothstep(0.30, 0.16, x), 'upperarm_l': M.smoothstep(0.16, 0.30, x) * M.smoothstep(0.52, 0.42, x),
                           'lowerarm_l': M.smoothstep(0.42, 0.52, x) * M.smoothstep(0.80, 0.70, x), 'hand_l': M.smoothstep(0.70, 0.80, x)})
        M.normalize_weights(b)
        A.add(b, look=look2, smooth=18, flip=sx < 0)
    for k, (dx, L_, cu) in enumerate(((0.035, 0.62, 0.22), (-0.03, 0.46, -0.3))):
        b = flame_blade(L_, 0.075, th=0.045, curl=cu, seed=5 + k)
        A.add(b.move(dx, 0.0, 1.50), bone='Head', look=look2 if k else look, smooth=18)
    if core:
        A.add(ellipsoid(0.055, 0.03, 0.075, n=9, m=6).move(0, -0.098, 1.30), bone='spine_03', look=core, smooth=80)
        for sx in (-1, 1):
            A.add(tube([(sx * 0.03, -0.112, 1.27), (sx * 0.075, -0.108, 1.18), (sx * 0.06, -0.105, 1.08)], 0.006, n=4), bone='spine_02', look=core)


# ================================================================================================ fungus
@part
def lamp_arm(A, side='r', look='fungus', gill='glow', R=0.115, stalk=0.014):
    """the forearm has become a stalk and the hand a glowing lantern-shaped cap (use with body hide=['hand_r'])"""
    for sx in A.sides(side):
        s = '_l' if sx > 0 else '_r'
        b = A.rest
        A.add(KF.shell(b, b.wsum(['lowerarm' + s]) > 0.5, stalk), look=look, smooth=180)
        w = A.R.H('hand' + s)
        cap, gl = KB2.fungus_cap(R=R, h=R * 0.75, seed=3)
        Rm = M.rot_to((0, 0, 1), (sx, 0, 0))
        A.add(cap.tf(Rm).move(w + V(sx * 0.02, 0, 0)), bone='hand' + s, look=look, smooth=70)
        A.add(gl.tf(Rm).move(w + V(sx * 0.02, 0, 0)), bone='hand' + s, look=gill, smooth=70)
        A.add(ellipsoid(0.03, 0.05, 0.05, n=8, m=5).move(w + V(sx * 0.0, 0, 0)), bone='hand' + s, look=gill, smooth=70)
        for t, rr in ((0.35, 0.045), (0.7, 0.035)):
            c2, g2 = KB2.fungus_cap(R=rr, h=rr * 0.7, seed=5)
            p = KC3.BP('lowerarm' + s, t) + V(0, 0, KI.limb_radius(b, 'lowerarm' + s, t) + 0.012)
            A.add(c2.move(p), bone='lowerarm' + s, look=look, smooth=70)
            A.add(g2.move(p), bone='lowerarm' + s, look=gill, smooth=70)


@part
def shelf_fungus(A, bones=('upperarm_l', 'lowerarm_l'), count=5, r=(0.05, 0.095), look='fungus', seed=4, along=1.0):
    """brackets of shelf fungus sheathing limbs (flat side on the limb, the shelf faces the shoulder end)"""
    rng = np.random.default_rng(seed)
    for bn in bones:
        h = A.R.H(bn); t = A.R.T(bn)
        ax = unit(t - h)
        fr = M.frame_from(ax)
        for k in range(count):
            f = (k + rng.uniform(0.2, 0.8)) / count
            a = rng.uniform(0, 2 * math.pi)
            out = unit(fr[:, 0] * math.cos(a) + fr[:, 2] * math.sin(a))
            rr = rng.uniform(r[0], r[1])
            sh = KA.shelf(r=rr, th=rr * 0.34)
            Mx = np.stack([np.cross(out, -ax * along), out, -ax * along], axis=1)
            p = h + (t - h) * f + out * (KI.limb_radius(A.rest, bn, f) * 0.8)
            A.add(sh.tf(Mx).move(p), bone=bn, look=look, smooth=60)


@part
def head_bracket(A, look='fungus', gill='glow', R=0.17, side=1, tilt=24.0, small=3, seed=2):
    """a bracket of mushrooms the head tilts under: one big cap over the crown, small ones on the shoulder"""
    cap, gl = KB2.fungus_cap(R=R, h=R * 0.5, seed=seed)
    for p, lk in ((cap, look), (gl, gill)):
        A.add(p.rot('y', -side * tilt).move(side * 0.05, -0.01, 1.775), bone='Head', look=lk, smooth=70)
    A.add(cyl((side * 0.04, -0.01, 1.70), (side * 0.05, -0.01, 1.78), 0.035, n=7), bone='Head', look=look, smooth=70)
    rng = np.random.default_rng(seed)
    for k in range(small):
        rr = rng.uniform(0.035, 0.06)
        c2, st = KA.mushroom(r=rr, h=rr * 0.6, stalk=rr * 1.0, sr=rr * 0.3)
        p = V(side * (0.13 + 0.05 * k), 0.02 * math.sin(k * 2.0), 1.50 - 0.012 * k)
        A.add(c2.move(p), bone='clavicle_l' if side > 0 else 'clavicle_r', look=look, smooth=70)
        A.add(st.move(p), bone='clavicle_l' if side > 0 else 'clavicle_r', look=look, smooth=70)
