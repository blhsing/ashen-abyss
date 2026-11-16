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
        A.add(b, bone=bone, look=look, smooth=180, keep=True)
        if eyes:
            e = join([ellipsoid(0.008, 0.006, 0.008, n=6, m=4).move(sx * 0.031, -0.096, 1.668) for sx in (-1, 1)])
            e.rot('x', 14, pivot=M.RIG.H('Head'))
            e.scale(sc * scale, pivot=piv).rot('x', pitch, pivot=piv).rot('z', yaw, pivot=piv).move(V(at) - V(piv))
            A.add(e, bone=bone, look=eyes, keep=True)


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
    for sx in (-1, 1):
        a = np.linspace(0, 2 * math.pi, 18, endpoint=False)
        pts = np.stack([0.11 * np.cos(a), np.zeros(18), 0.30 * np.sin(a) * (1 - 0.25 * np.sin(a))], axis=1)
        fr = tube(pts, 0.012, n=5, closed=True)
        lat = [cyl((-0.1 * math.sqrt(max(0.0, 1 - (zz / 0.3) ** 2)), 0, zz), (0.1 * math.sqrt(max(0.0, 1 - (zz / 0.3) ** 2)), 0, zz), 0.004, n=4) for zz in np.linspace(-0.2, 0.2, 6)]
        lat += [cyl((xx, 0, -0.24), (xx, 0, 0.2), 0.004, n=4) for xx in (-0.045, 0.0, 0.045)]
        c = V(sx * 0.14, y + 0.03 * (sx > 0), z + 0.2)
        A.add(fr.rot('y', sx * 14).move(c), bone=bone, look=look, smooth=50, keep=True)
        A.add(join(lat).rot('y', sx * 14).move(c), bone=bone, look=lace, keep=True)
    A.add(band((0, 0.02, 1.36), 0.24, 0.19, 0.03, 0.008, tilt_y=14, n=22), bone=bone, look='strap')


# ================================================================================================ forge drop-hammer
@part
def hammer_arm(A, side='r', look='iron', glow='ember', block=0.34, beam=0.75):
    """the arm and shoulder are an iron drop-hammer assembly: riveted shoulder housing, a cranked beam from the elbow and a
    square block head the size of an anvil (use with body hide of that forearm + hand); a ballast drum is bolted to the
    other shoulder"""
    for sx in A.sides(side):
        s = '_l' if sx > 0 else '_r'
        o = '_r' if sx > 0 else '_l'
        sh = A.R.H('upperarm' + s); el = A.R.H('lowerarm' + s)
        A.add(box(0.30, 0.26, 0.24, bevel=0.03).move(sh + V(sx * 0.04, 0, 0.03)), bone='clavicle' + s, look=look, smooth=30)
        A.add(KC3.limb_plate('upperarm' + s, [(0.15, 0.10), (0.6, 0.095), (1.0, 0.085)], n=8, lip=0.006, power=4.0), bone='upperarm' + s, look=look, smooth=30)
        A.add(cyl(el + V(0, -0.11, 0), el + V(0, 0.11, 0), 0.10, n=12), bone='lowerarm' + s, look=look, smooth=40)
        bm = box(beam, 0.11, 0.13, bevel=0.012).move(el + V(sx * beam / 2, 0, 0))
        A.add(bm, bone='lowerarm' + s, look=look, smooth=30)
        hd = box(block, block * 0.92, block * 1.05, bevel=0.025).move(el + V(sx * (beam + block * 0.35), 0, -block * 0.25))
        A.add(hd, bone='lowerarm' + s, look=look, smooth=30)
        if glow:
            A.add(box(block * 0.86, block * 0.8, 0.012).move(el + V(sx * (beam + block * 0.35), 0, -block * 0.25 - block * 0.53)), bone='lowerarm' + s, look=glow)
        for k in range(4):
            a = k * math.pi / 2 + 0.5
            A.add(KC3.rivet(el + V(0, -0.115, 0) + V(0.07 * math.cos(a), 0, 0.07 * math.sin(a)), (0, -1, 0), r=0.014), bone='lowerarm' + s, look=look)
        so = A.R.H('upperarm' + o)
        dr = cyl(so + V(-sx * 0.02, -0.17, 0.10), so + V(-sx * 0.02, 0.19, 0.10), 0.17, n=14)
        A.add(dr, bone='clavicle' + o, look=look, smooth=40)
        for yy in (-0.12, 0.0, 0.13):
            A.add(torus(0.175, 0.012, n=14, m=4).rot('x', 90).move(so + V(-sx * 0.02, yy, 0.10)), bone='clavicle' + o, look=look)
        A.add(band((0, 0.01, 1.33), 0.27, 0.2, 0.05, 0.01, tilt_y=sx * 22, n=24), bone='spine_03', look=look)


# ================================================================================================ stone pillar bearer
@part
def pillar_load(A, look='limestone', L_=1.5, r=0.165, z=1.60, y=0.10, capital=True, bone='spine_03'):
    """a broken length of fluted pillar carried across the shoulders like a yoke-log; a square capital block sits on the
    head as a hat"""
    def flutes(th, zz):
        return 1.0 - 0.07 * np.abs(np.sin(8 * th))
    p = M.loft_z([(-L_ / 2, 0, 0, r, r), (-L_ * 0.2, 0, 0, r * 0.98, r * 0.98), (L_ * 0.3, 0, 0, r * 0.96, r * 0.96), (L_ / 2, 0, 0, r * 0.95, r * 0.95)], n=24, mod=flutes)
    end = p.v[:, 2] > L_ / 2 - 0.01
    p.v[end, 2] += 0.09 * np.sin(5 * np.arctan2(p.v[end, 1], p.v[end, 0])) + 0.05 * np.sin(13 * np.arctan2(p.v[end, 1], p.v[end, 0]))     # the break
    A.add(p.rot('y', 90).rot('z', 4).move(0.06, y, z), bone=bone, look=look, smooth=35, keep=True)
    for sx in (-1,):
        A.add(M.loft_z([(0, 0, 0, r * 1.12, r * 1.12), (0.06, 0, 0, r * 1.12, r * 1.12), (0.09, 0, 0, r * 0.98, r * 0.98)], n=20).rot('y', 90).move(0.06 - L_ / 2 - 0.02, y - 0.05, z),
              bone=bone, look=look, smooth=35, keep=True)
    for k in range(4):
        A.add(KX.lump(0.05, 0.045, 0.04, seed=k, amp=0.3).move(0.06 + L_ / 2 + 0.02 * k, y + 0.04 * math.sin(k * 2.0), z - 0.14 - 0.03 * k), bone=bone, look=look, smooth=30, keep=True)
    if capital:
        A.add(box(0.36, 0.36, 0.13, bevel=0.015).move(0, -0.02, 1.86), bone='Head', look=look, smooth=30)
        A.add(M.loft_z([(1.74, 0, -0.02, 0.105, 0.115), (1.80, 0, -0.02, 0.15, 0.15)], n=16), bone='Head', look=look, smooth=40)
