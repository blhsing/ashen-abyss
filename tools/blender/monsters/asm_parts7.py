"""asm_parts7.py -- stage-4 parts: bodies fused into one mass, snow gear, drop-hammer arm, pillar loads.
Same rules as asm_parts.py."""
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
import asm

_HALF = {}


def half_body(kind='male', ratio=0.22, arms_down=75.0, gaunt=1.0, jaw=14.0):
    """a decimated head + torso + arms of the base body with the arms hanging limp: raw material for fused figures.
    Returned in rest space, origin unchanged (pelvis top about z = 1.0)."""
    key = (kind, ratio, arms_down, gaunt, jaw)
    if key not in _HALF:
        b = M.load_body(kind)
        if gaunt:
            KF.gaunt_face(b, gaunt)
        if jaw:
            KF.open_jaw(b, jaw)
        legs = M.RIG.chain('thigh_l') + M.RIG.chain('thigh_r')
        b = b.sub(~b.vmask_faces((b.wsum(legs) > 0.5) | (b.v[:, 2] < 0.98), 'any'))
        b = M.decimate(b, ratio)
        pose = {'upperarm_l': dict(rot=[('y', arms_down)]), 'upperarm_r': dict(rot=[('y', -arms_down)]),
                'lowerarm_l': dict(rot=[('y', 12)]), 'lowerarm_r': dict(rot=[('y', -12)]), 'Head': dict(rot=[('x', 14)])}
        M.pose_sculpt(b, pose)
        _HALF[key] = b
    return _HALF[key].copy()


@part
def fused_bodies(A, look='skin', spots=None, ratio=0.22, scale=0.9, seed=3, sink=0.1, eyes=None, melt=0, gaunt=1.0):
    """other bodies fused into the creature: head + torso + limp arms of extra figures, half sunk into the back and the
    shoulders (each one rigid on a bone, so a fringe of arms and extra heads at odd heights rides on the mass).
    spots = [(bone, (x, y, z) of the figure's waist, yaw deg, pitch deg forward, scale)]"""
    spots = spots or (('spine_03', (0.16, 0.20, 1.02), 150, 26, 0.92), ('spine_03', (-0.19, 0.17, 0.95), -160, 34, 0.88),
                      ('spine_02', (0.02, 0.26, 0.78), 180, 40, 0.95), ('clavicle_l', (0.33, 0.04, 1.12), 95, 30, 0.8))
    for k, (bone, at, yaw, pitch, sc) in enumerate(spots):
        b = half_body(ratio=ratio, arms_down=70.0 + 8 * (k % 3), gaunt=gaunt)
        if melt:
            M.smooth_verts(b, int(melt), 0.5)
        piv = (0, 0.02, 1.0)
        b.scale(sc * scale, pivot=piv).rot('x', pitch, pivot=piv).rot('z', yaw, pivot=piv).move(V(at) - V(piv))
        b.W = None
        K = b.center()                                   # the figure and its eyes share one keep anchor
        A.add(b, bone=bone, look=look, smooth=180, keep=K)
        if eyes:
            e = join([ellipsoid(0.008, 0.006, 0.008, n=6, m=4).move(sx * 0.031, -0.096, 1.668) for sx in (-1, 1)])
            e.rot('x', 14, pivot=M.RIG.H('Head'))
            e.scale(sc * scale, pivot=piv).rot('x', pitch, pivot=piv).rot('z', yaw, pivot=piv).move(V(at) - V(piv))
            A.add(e, bone=bone, look=eyes, keep=K)


@part
def blanket(A, look='rag', seed=6, length=0.75, rx=0.36, ry=0.33, z=1.62, holes=0.12):
    """one shared blanket frozen stiff over a huddle: a ragged dome draped from above the shoulders"""
    n = 22
    a = np.radians(np.linspace(-90, 270, n))
    top = np.stack([rx * 0.45 * np.cos(a), 0.12 + ry * 0.45 * np.sin(a), np.full(n, z) + 0.05 * np.sin(3 * a)], axis=1)
    nr = np.stack([np.cos(a), np.sin(a), 0 * a], axis=1)
    ln = length * (0.75 + 0.35 * np.sin(a) ** 2) * np.where(np.sin(a) < -0.5, 0.35, 1.0)        # short over the face
    p = hang(top, ln, rows=8, out=nr, flare=0.55, rag=0.35, seed=seed, wave=0.03, holes=holes, tatter=0.4, curve=2.0)
    M.weights_from(p, {'spine_03': 1.0})
    A.add(p, look=look)
    cap = ellipsoid(rx * 0.47, ry * 0.47, 0.07, n=14, m=6).move(0, 0.12, z + 0.0)
    cap.rough(0.01, 14.0, seed=float(seed))
    A.add(cap, bone='spine_03', look=look, smooth=80)


# ================================================================================================ frost road-clearer
@part
def snowshoes(A, look='wood_z', lace='rope', z=1.30, y=0.20, bone='spine_03'):
    """a pair of snowshoes lashed upright on the back like two paddles"""
    K = V(0, y, z + 0.2)
    for sx in (-1, 1):
        a = np.linspace(0, 2 * math.pi, 18, endpoint=False)
        pts = np.stack([0.11 * np.cos(a), np.zeros(18), 0.30 * np.sin(a) * (1 - 0.25 * np.sin(a))], axis=1)
        fr = tube(pts, 0.012, n=5, closed=True)
        lat = [cyl((-0.1 * math.sqrt(max(0.0, 1 - (zz / 0.3) ** 2)), 0, zz), (0.1 * math.sqrt(max(0.0, 1 - (zz / 0.3) ** 2)), 0, zz), 0.004, n=4) for zz in np.linspace(-0.2, 0.2, 6)]
        lat += [cyl((xx, 0, -0.24), (xx, 0, 0.2), 0.004, n=4) for xx in (-0.045, 0.0, 0.045)]
        c = V(sx * 0.14, y + 0.03 * (sx > 0), z + 0.2)
        A.add(fr.rot('y', sx * 14).move(c), bone=bone, look=look, smooth=50, keep=K)
        A.add(join(lat).rot('y', sx * 14).move(c), bone=bone, look=lace, keep=K)
    import asm_parts9 as P9
    on = 'robe' if 'robe' in A.refs else 'body'
    for zz, tl in ((1.40, 9.0), (1.28, -9.0)):             # two crossing straps seated on the coat and skinned like it (were one loose hoop)
        P9.chest_band(A, look='strap', z=zz, w=0.03, th=0.008, tilt=tl, on=on, pad=0.008)


# ================================================================================================ forge drop-hammer
@part
def hammer_arm(A, side='r', look='iron', glow='ember', block=0.30, beam=0.62, face='steel'):
    """the arm and shoulder are an iron drop-hammer assembly: a riveted shoulder cowl (half drum on a flanged plate), an
    armoured upper arm, a crank drum at the elbow, an I-beam with a cranked step and a square block head the size of an
    anvil with a worn striking plate on its palm side (dull heat glow in its grooves).  Use with body hide of that
    forearm + hand.  A ballast drum hangs on a bracket behind the other shoulder, chained to the cowl across the back."""
    def rivet_ring(c, nrm, r, n, bone, rr=0.011):
        fr = M.frame_from(nrm)
        for k in range(n):
            a = 2 * math.pi * (k + 0.5) / n
            A.add(KC3.rivet(V(c) + (fr[:, 0] * math.cos(a) + fr[:, 2] * math.sin(a)) * r, nrm, r=rr), bone=bone, look=look)

    for sx in A.sides(side):
        s = '_l' if sx > 0 else '_r'
        o = '_r' if sx > 0 else '_l'
        sh = A.R.H('upperarm' + s); el = A.R.H('lowerarm' + s)
        # shoulder cowl: half drum lying front-to-back over the shoulder, on a flanged back plate
        c0 = sh + V(sx * 0.03, 0.0, 0.035)
        A.add(cyl(c0 + V(0, -0.15, 0), c0 + V(0, 0.15, 0), 0.15, n=14), bone='clavicle' + s, look=look, smooth=40)
        for yy in (-0.155, 0.155):
            fl = revolve([(0.0, 0.0), (0.165, 0.0), (0.165, 0.02), (0.0, 0.02)], n=14).rot('x', -90 if yy > 0 else 90)
            A.add(fl.move(c0 + V(0, yy, 0)), bone='clavicle' + s, look=look, smooth=30)
            rivet_ring(c0 + V(0, yy + (0.021 if yy > 0 else -0.021), 0), (0, 1 if yy > 0 else -1, 0), 0.125, 8, 'clavicle' + s)
            bolt = revolve([(0.0, 0.0), (0.04, 0.0), (0.04, 0.03), (0.022, 0.045), (0.0, 0.045)], n=6).rot('x', -90 if yy > 0 else 90)
            A.add(bolt.move(c0 + V(0, yy, 0)), bone='clavicle' + s, look=look, smooth=30)                 # pivot bolt heads
        A.add(box(0.07, 0.30, 0.22, bevel=0.012).move(sh + V(-sx * 0.085, 0, 0.0)), bone='clavicle' + s, look=look, smooth=30)
        A.add(KC3.limb_plate('upperarm' + s, [(0.12, 0.105), (0.55, 0.098), (1.0, 0.088)], n=10, lip=0.008, power=3.0), bone='upperarm' + s, look=look, smooth=38)
        for t in (0.3, 0.72):
            A.add(KI.limb_ring('upperarm' + s, t, 0.103 - 0.012 * t, thick=0.012, n=12, m=4), bone='upperarm' + s, look=look)
        # crank drum at the elbow
        A.add(cyl(el + V(0, -0.115, 0), el + V(0, 0.115, 0), 0.105, n=14), bone='lowerarm' + s, look=look, smooth=40)
        for yy in (-1, 1):
            rivet_ring(el + V(0, yy * 0.117, 0), (0, yy, 0), 0.075, 6, 'lowerarm' + s, rr=0.012)
            A.add(cyl(el + V(0, yy * 0.11, 0), el + V(0, yy * 0.145, 0), 0.035, n=8), bone='lowerarm' + s, look=look, smooth=30)
        # I-beam with a cranked step half way: web + two flanges per segment
        L1 = beam * 0.46; L2 = beam - L1; st = 0.07
        for (x0, ln, dz) in ((0.02, L1, 0.0), (L1 - 0.03, L2 + 0.05, -st)):
            c = el + V(sx * (x0 + ln / 2), 0, dz)
            A.add(box(ln, 0.036, 0.15, bevel=0.006).move(c), bone='lowerarm' + s, look=look, smooth=30)
            for fz in (-0.075, 0.075):
                A.add(box(ln, 0.12, 0.022, bevel=0.005).move(c + V(0, 0, fz)), bone='lowerarm' + s, look=look, smooth=30)
        A.add(box(0.07, 0.13, 0.24, bevel=0.008).move(el + V(sx * (L1 - 0.005), 0, -st / 2)), bone='lowerarm' + s, look=look, smooth=30)        # the crank web
        for dz in (0.0, -st):
            A.add(cyl(el + V(sx * (L1 - 0.005), -0.075, dz), el + V(sx * (L1 - 0.005), 0.075, dz), 0.022, n=7), bone='lowerarm' + s, look=look, smooth=30)
        # the block head
        hc = el + V(sx * (beam + block * 0.42), 0, -st - block * 0.18)
        A.add(box(block, block * 0.92, block * 1.08, bevel=0.03).move(hc), bone='lowerarm' + s, look=look, smooth=30)
        A.add(box(block * 0.34, block * 1.0, block * 0.5, bevel=0.012).move(hc + V(-sx * block * 0.42, 0, block * 0.2)), bone='lowerarm' + s, look=look, smooth=30)      # socket collar
        for yy in (-1, 1):
            for (dx, dz) in ((-0.3, 0.33), (0.3, 0.33), (-0.3, -0.3), (0.3, -0.3)):
                A.add(KC3.rivet(hc + V(dx * block, yy * block * 0.462, dz * block), (0, yy, 0), r=0.016, n=6), bone='lowerarm' + s, look=look)
        A.add(torus(0.045, 0.014, n=10, m=5).rot('x', 90).move(hc + V(0, 0, block * 0.54 + 0.03)), bone='lowerarm' + s, look=look, smooth=40)       # lifting eye
        fz = hc[2] - block * 0.54
        A.add(box(block * 0.86, block * 0.80, 0.035, bevel=0.01).move(hc[0], hc[1], fz - 0.012), bone='lowerarm' + s, look=face, smooth=30)        # striking plate
        if glow:                                                             # heat in the plate's grooves (a cross and a ring)
            A.add(box(block * 0.70, 0.022, 0.012).move(hc[0], hc[1], fz - 0.031), bone='lowerarm' + s, look=glow)
            A.add(box(0.022, block * 0.64, 0.012).move(hc[0], hc[1], fz - 0.031), bone='lowerarm' + s, look=glow)
            A.add(torus(block * 0.25, 0.011, n=14, m=4).move(hc[0], hc[1], fz - 0.031), bone='lowerarm' + s, look=glow)
        # ballast drum on a bracket behind the other shoulder
        so = A.R.H('upperarm' + o)
        dc = so + V(sx * 0.03, 0.215, 0.03)
        A.add(cyl(dc + V(-0.14, 0, 0), dc + V(0.14, 0, 0), 0.118, n=14), bone='clavicle' + o, look=look, smooth=40)
        for xx in (-0.10, 0.0, 0.10):
            A.add(torus(0.123, 0.011, n=14, m=4).rot('y', 90).move(dc + V(xx, 0, 0)), bone='clavicle' + o, look=look)
        for xx in (-0.145, 0.145):
            rivet_ring(dc + V(xx, 0, 0), (1 if xx > 0 else -1, 0, 0), 0.08, 6, 'clavicle' + o, rr=0.011)
        for xx in (-0.09, 0.09):
            A.add(cyl(dc + V(xx, -0.02, 0.10), so + V(sx * 0.03 + xx * 0.6, 0.02, 0.085), 0.016, n=5), bone='clavicle' + o, look=look, smooth=30)
            A.add(cyl(dc + V(xx, -0.06, -0.08), so + V(sx * 0.03 + xx * 0.6, 0.10, -0.10), 0.014, n=5), bone='clavicle' + o, look=look, smooth=30)
        import asm_parts9 as P9
        P9.chest_band(A, look=look, z=1.31, w=0.05, th=0.01, tilt=sx * 18.0, pad=0.012)
        ch = KB2.drape_path(dc + V(sx * 0.10, 0.02, 0.11), c0 + V(-sx * 0.02, 0.16, 0.10), sag=0.07, n=9)
        A.add(M.chain(ch, link=0.05, r=0.008), bone='spine_03', look=look, smooth=60)


# ================================================================================================ stone pillar bearer
@part
def pillar_load(A, look='limestone', L_=1.5, r=0.165, z=1.60, y=0.10, capital=True, bone='spine_03'):
    """a broken length of fluted pillar carried across the shoulders like a yoke-log; a square capital block sits on the
    head as a hat"""
    K = V(0.06, y, z)
    def flutes(th, zz):
        return 1.0 - 0.07 * np.abs(np.sin(8 * th))
    p = M.loft_z([(-L_ / 2, 0, 0, r, r), (-L_ * 0.2, 0, 0, r * 0.98, r * 0.98), (L_ * 0.3, 0, 0, r * 0.96, r * 0.96), (L_ / 2, 0, 0, r * 0.95, r * 0.95)], n=24, mod=flutes)
    end = p.v[:, 2] > L_ / 2 - 0.01
    ang = np.arctan2(p.v[end, 1], p.v[end, 0])
    p.v[end, 2] += 0.035 * np.sin(3 * ang + 0.7) + 0.022 * np.sin(7 * ang) - 0.02                                               # the break: rough, not spiky
    A.add(p.rot('y', 90).rot('z', 4).move(0.06, y, z), bone=bone, look=look, smooth=35, keep=K)
    for sx in (-1,):
        A.add(M.loft_z([(0, 0, 0, r * 1.12, r * 1.12), (0.06, 0, 0, r * 1.12, r * 1.12), (0.09, 0, 0, r * 0.98, r * 0.98)], n=20).rot('y', 90).move(0.06 - L_ / 2 - 0.02, y - 0.05, z),
              bone=bone, look=look, smooth=35, keep=K)
    for k in range(4):
        A.add(KX.lump(0.05, 0.045, 0.04, seed=k, amp=0.3).move(0.06 + L_ / 2 + 0.02 * k, y + 0.04 * math.sin(k * 2.0), z - 0.14 - 0.03 * k), bone=bone, look=look, smooth=30, keep=K)
    if capital:
        A.add(box(0.36, 0.36, 0.13, bevel=0.015).move(0, -0.02, 1.86), bone='Head', look=look, smooth=30)
        A.add(M.loft_z([(1.74, 0, -0.02, 0.105, 0.115), (1.80, 0, -0.02, 0.15, 0.15)], n=16), bone='Head', look=look, smooth=40)
