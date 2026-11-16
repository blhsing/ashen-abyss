"""asm_parts6.py -- parts for the stage-4 bruisers and giants (fuel-bearers, raider brute, bell-ringer ...).
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

asm.PRE.update(
    skin_bark=asm._p('skin', 'skin', color=(0.075, 0.068, 0.062), blotch=(0.03, 0.028, 0.026), vein=(0.02, 0.02, 0.02), vein_amt=0.2, sore_amt=0.0,
                     wrinkle=2.2, sinew=1.6, pore=0.5, rough=0.8, bump=1.0, dry=1.0, hands_dark=0.6, dark=(0.012, 0.012, 0.012)),
    tar=asm._p('wood', 'leather', color=(0.035, 0.03, 0.027), axis=(1, 0, 0)),
    log=asm._p('wood', 'leather', color=(0.11, 0.08, 0.055), axis=(0, 1, 0)),
    brand=asm._p('emis', 'emis', emit=(1.0, 0.42, 0.08), strength=1.6, mottle=0.5, base_mul=0.5),
    padded=asm._p('cloth', 'cloth', color=(0.17, 0.15, 0.12), hem_lo=0.3, hem_hi=0.9, fold=5.0, weave_size=1.6, stain_amt=0.7),
    sealskin=asm._p('leather', 'leather', color=(0.085, 0.085, 0.09), scuff=0.4, scale=14.0, rough=0.4),
)


def log_part(L_=0.6, r=0.06, seed=0, n=8):
    """a rough log along +Y, centred on the origin, with cut ends"""
    t = tube([(0, -L_ / 2, 0), (0, 0, 0), (0, L_ / 2, 0)], [r, r * 1.04, r * 0.96], n=n)
    t.rough(r * 0.07, 14.0, seed=float(seed))
    return t


def bundle(count=5, L_=0.62, r=0.058, seed=0):
    """[(Part, look)]: logs bound with two chains, axis +Y, hanging point at the origin (top of the bundle)"""
    ps = []
    R_ = r * (1.0 if count <= 3 else 1.75)
    for k in range(count):
        a = 2 * math.pi * k / max(count - 1, 1)
        c = V(0, 0, -R_ - r) if k == 0 else V(R_ * math.cos(a), 0, -R_ - r + R_ * math.sin(a))
        ps.append((log_part(L_ * (0.9 + 0.2 * ((k * 3) % 4) / 3), r * (0.9 + 0.15 * (k % 3) / 2), seed + k).rot('y', 20 * k).move(c + V(0, 0.03 * math.sin(k * 2.0), 0)), 'log'))
    for y in (-L_ * 0.28, L_ * 0.28):
        ps.append((torus(R_ + r + 0.008, 0.012, n=14, m=4).rot('x', 90).move(0, y, -R_ - r), 'rust'))
    return ps


# ================================================================================================ fuel-bearers
@part
def brand_ring(A, look='brand', at=(0.0, -0.175, 1.33), R=0.07, bone='spine_03'):
    """the Forge brand on the chest: a raised scar ring that still glows dully"""
    q, nq, _ = A.S('body').nearest(V(at))
    t = torus(R, 0.011, n=16, m=4).scale((1.0, 1.0, 0.6))
    A.add(t.tf(M.rot_to((0, 0, 1), nq)).move(q + nq * 0.002), look=look, ride=q, smooth=60)
    A.add(ellipsoid(0.014, 0.014, 0.008, n=6, m=4).tf(M.rot_to((0, 0, 1), nq)).move(q + nq * 0.002), look=look, ride=q)


@part
def yoke(A, look='tar', length=1.75, z=1.565, y=0.085, r=0.05, loads='logs', drop=0.34, bone='spine_03', chain='rust'):
    """shoulder-yoke as long as its bearer is tall, lying across the shoulders behind the neck, a chained bundle of logs
    hung from each end (rigid on spine_03: the swing has to come from the clips)"""
    xs = np.linspace(-length / 2, length / 2, 11)
    pts = np.stack([xs, np.full(11, y), z + 0.10 * (np.abs(xs) / (length / 2)) ** 2 - 0.02], axis=1)
    b = tube(pts, r * (1.0 - 0.25 * (np.abs(xs) / (length / 2)) ** 2), n=8, r2=r * 1.25 * (1.0 - 0.25 * (np.abs(xs) / (length / 2)) ** 2), up=(0, 1, 0))
    b.rough(0.004, 12.0, seed=3.0)
    A.add(b, bone=bone, look=look, smooth=40, keep=True)
    A.add(revolve([(0.13, -0.035), (0.14, 0.0), (0.13, 0.035)], n=10, cap=False).rot('x', 90).scale((1, 1, 0.8)).move(0, y, z - 0.03), bone=bone, look='strap', keep=True)   # neck pad
    for sx in (-1, 1):
        e = pts[0 if sx < 0 else -1] + V(-sx * 0.06, 0, 0)
        A.add(torus(r * 0.9, 0.012, n=10, m=4).rot('y', 90).move(e), bone=bone, look=chain, keep=True)
        A.add(M.chain(np.array([e + V(0, 0, -r * 0.8), e + V(0, 0, -drop)]), link=0.07, r=0.011), bone=bone, look=chain, smooth=60, keep=True)
        if loads == 'logs':
            for p, lk in bundle(5, 0.62, 0.058, seed=4 + sx):
                A.add(p.move(e + V(0, 0, -drop)), bone=bone, look=lk, smooth=40, keep=True)
        else:
            for p, lk in item_parts(A, loads, 2.2):
                A.add(p.move(e + V(0, 0, -drop)), bone=bone, look=lk, smooth=40, keep=True)


@part
def hod(A, look='wicker', frame='tar', z0=1.0, h=1.75, w=0.62, d=0.36, y=0.20, trunks=5, bone='spine_03', strap='strap'):
    """wicker-and-timber hod on the back rising a body-height above the head, stacked with tree trunks like arrows in a
    quiver"""
    c = V(0, y + d / 2, z0)
    for sx in (-1, 1):
        for sy in (0, 1):
            A.add(cyl(c + V(sx * w / 2, (sy - 0.5) * d, 0), c + V(sx * w / 2 * 1.15, (sy - 0.5) * d * 1.2, h), 0.028, n=6), bone=bone, look=frame, smooth=40, keep=True)
    for zz in (0.02, h * 0.45, h * 0.9):
        f = 1 + 0.15 * zz / h
        ring = np.array([c + V(sx * w / 2 * f, sy * d / 2 * f * 1.05, zz) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
        A.add(tube(ring, 0.02, n=5, closed=True), bone=bone, look=frame, smooth=40, keep=True)
    bk = M.loft_z([(z0 + 0.02, 0, y + d / 2, w / 2, d / 2, 4.0), (z0 + h * 0.6, 0, y + d / 2, w / 2 * 1.09, d / 2 * 1.1, 4.0)], n=16, cap1=False)
    bk.rough(0.004, 30.0, seed=2.0)
    A.add(bk, bone=bone, look=look, smooth=40, keep=True)
    rng = np.random.default_rng(5)
    for k in range(trunks):
        x = (k - (trunks - 1) / 2) * w / trunks * 0.9
        yy = y + d / 2 + rng.uniform(-0.08, 0.08)
        L_ = h * rng.uniform(1.0, 1.28)
        lg = log_part(L_, rng.uniform(0.055, 0.075), seed=k).rot('x', 90).rot('y', rng.uniform(-5, 5)).move(x, yy, z0 + 0.12 + L_ / 2)
        A.add(lg, bone=bone, look='log', smooth=40, keep=True)
    for z_, tl in ((1.22, 14.0), (1.40, -14.0)):
        cx, cy, rx, ry = girth_of(A, 'body', z_)
        A.add(band((0, cy + 0.04, z_), rx * 0.92 + 0.014, ry + 0.07, 0.04, 0.009, tilt_y=tl, n=22), bone=bone, look=strap)


@part
def cage_back(A, look='wicker', R=0.36, h=0.72, z0=1.02, y=0.20, bone='spine_03', glow='glow', strap='strap'):
    """domed wicker cage strapped to the back like an enormous lobster-pot, a faint warm glow inside"""
    c = V(0, y + R * 0.8, z0)
    ribs = 12
    for k in range(ribs):
        a = 2 * math.pi * k / ribs
        pts = [c + V(R * math.cos(a) * f, R * 0.8 * math.sin(a) * f, zz) for f, zz in ((0.8, 0.0), (1.0, h * 0.3), (0.93, h * 0.62), (0.6, h * 0.9), (0.0, h))]
        A.add(tube(M.polyline(pts, 8), 0.012, n=4), bone=bone, look=look, smooth=40, keep=True)
    for zz, f in ((0.0, 0.8), (h * 0.3, 1.0), (h * 0.62, 0.93), (h * 0.88, 0.63)):
        A.add(torus(R * f, 0.014, n=18, m=4, ry=R * 0.8 * f).move(c + V(0, 0, zz)), bone=bone, look=look, smooth=40, keep=True)
    inner = M.loft_z([(0.0, 0, 0, R * 0.76, R * 0.6), (h * 0.3, 0, 0, R * 0.95, R * 0.75), (h * 0.62, 0, 0, R * 0.88, R * 0.7), (h * 0.9, 0, 0, R * 0.55, R * 0.44), (h * 0.99, 0, 0, 0.02, 0.02)],
                      n=12)
    A.add(inner.move(c), bone=bone, look='void', smooth=60, keep=True)
    if glow:
        A.add(ellipsoid(R * 0.3, R * 0.25, R * 0.3, n=8, m=6).move(c + V(0, 0, h * 0.3)), bone=bone, look=glow, smooth=80, keep=True)
        for k in range(5):
            a = 2 * math.pi * (k + 0.5) / 5
            A.add(box(0.03, 0.012, h * 0.4).move(c + V(R * 0.9 * math.cos(a), R * 0.72 * math.sin(a), h * 0.4)).rot('z', math.degrees(a) + 90, pivot=c + V(R * 0.9 * math.cos(a), R * 0.72 * math.sin(a), 0)),
                  bone=bone, look=glow, keep=True)
    for z_, tl in ((1.22, 14.0), (1.40, -14.0)):
        cx, cy, rx, ry = girth_of(A, 'body', z_)
        A.add(band((0, cy + 0.05, z_), rx * 0.92 + 0.014, ry + 0.09, 0.04, 0.009, tilt_y=tl, n=22), bone=bone, look=strap)


@part
def face_shield(A, look='rust', window='void', w=0.25, h=0.30):
    """riveted iron face-shield with one square window: a flat rectangular head"""
    p = box(w, 0.016, h, bevel=0.006)
    p.v[:, 1] += 2.2 * p.v[:, 0] ** 2
    A.add(p.move(0, -0.15, 1.665), bone='Head', look=look, smooth=30)
    A.add(box(0.085, 0.01, 0.06).move(0, -0.161, 1.685), bone='Head', look=window)
    for sx in (-1, 1):
        for sz in (-1, 1):
            A.add(KC3.rivet(V(sx * (w / 2 - 0.02), -0.155 + 2.2 * (w / 2 - 0.02) ** 2, 1.665 + sz * (h / 2 - 0.02)), (0, -1, 0), r=0.009), bone='Head', look=look)
        A.add(tube([(sx * w / 2, -0.12, 1.70), (sx * 0.115, -0.02, 1.71), (0.0, 0.10, 1.70)], 0.005, n=4, r2=0.016, up=(0, 0, 1)), bone='Head', look=look, smooth=40)
    A.add(M.loft_z([(1.70, 0, -0.02, 0.10, 0.112), (1.76, 0, -0.02, 0.094, 0.106), (1.80, 0, -0.02, 0.05, 0.06), (1.805, 0, -0.02, 0.0, 0.0)], n=12), bone='Head', look=look, smooth=45)


# ================================================================================================ raider brute / ringer
@part
def bowl_helm(A, look='rust', R=0.30, z=1.42, h=0.42, slot='void'):
    """a beacon's iron brazier bowl worn upside-down as helmet and pauldrons in one piece: the rim rests on the shoulders,
    an eye-slot is hacked in the front (rigid on spine_03: the head turns inside it)"""
    prof = [(R, 0.0), (R * 1.03, 0.02), (R * 0.98, h * 0.3), (R * 0.82, h * 0.65), (R * 0.5, h * 0.92), (0.0, h)]
    b = revolve(prof, n=18, cap=False).scale((1.0, 0.86, 1.0))
    d = np.linalg.norm(b.v - np.array([-R * 0.6, -R * 0.4, h * 0.5]), axis=1)
    b.v[:, 2] -= np.exp(-(d / 0.09) ** 2) * 0.03
    A.add(b.move(0, 0.0, z), bone='spine_03', look=look, smooth=45)
    A.add(revolve([(R * 0.985, 0.0), (R * 0.9, h * 0.3), (R * 0.45, h * 0.88), (0.0, h * 0.94)], n=14, cap=False).scale((1.0, 0.86, 1.0)).flip().move(0, 0, z), bone='spine_03', look='void')
    A.add(torus(R * 1.02, 0.014, n=18, m=4, ry=R * 1.02 * 0.86).move(0, 0, z + 0.012), bone='spine_03', look=look)
    A.add(box(0.19, 0.03, 0.035).move(0, -R * 0.86 * 0.9, z + h * 0.6), bone='spine_03', look=slot)
    for k in range(3):                                   # the bowl's three stubby legs, now horns on top
        a = 2 * math.pi * k / 3 + 0.5
        A.add(cyl(V(R * 0.42 * math.cos(a), R * 0.36 * math.sin(a), z + h * 0.92), V(R * 0.6 * math.cos(a), R * 0.5 * math.sin(a), z + h + 0.12), 0.022, n=6),
              bone='spine_03', look=look, smooth=40)


@part
def ear_plugs(A, look='tallow', r=0.045):
    """wax plugs the size of fists sealing the ears"""
    for sx in (1, -1):
        p = ellipsoid(r * 0.8, r, r, n=9, m=6)
        p.rough(0.004, 30.0, seed=float(sx))
        A.add(p.move(sx * 0.094, -0.008, 1.66), bone='Head', look=look, smooth=80)
        A.add(tube([(sx * 0.10, -0.008, 1.63), (sx * 0.102, -0.01, 1.59), (sx * 0.095, -0.012, 1.56)], [0.014, 0.01, 0.004], n=5), bone='Head', look=look, smooth=70)


# ================================================================================================ bone amalgam / hush giant
@part
def amalgam(A, look='bone', eye='eye', thick=1.5):
    """a second skeleton stacked into the first: a doubled ribcage like a barrel, a second pair of small arms folded on
    the chest and two more skulls on the shoulders beside the main one"""
    C = A.C
    A.lk(look); A.lk(eye)
    KB.add_ribcage(C, look=look, thick=thick, wide=1.12, deep=1.30, bone='spine_03')
    n0 = len(C.parts)
    KB.add_ribcage(C, look=look, thick=thick * 0.9, wide=1.0, deep=1.15, bone='spine_02')
    for p in C.parts[n0:]:
        p.move(0, 0.0, -0.20)
    for sx, sc_, zz in ((1, 0.74, 1.585), (-1, 0.7, 1.575)):
        n0 = len(C.parts)
        KX.add_skull_seated(C, look=look, eye_look=eye, jaw_open=18.0 if sx > 0 else 4.0, origin=(sx * 0.215, 0.0, zz), tilt=-6.0, scale=sc_)
        for p in C.parts[n0:]:
            p.rot('y', sx * 16, pivot=(sx * 0.215, 0.0, zz))
            p.bone = 'clavicle_l' if sx > 0 else 'clavicle_r'
        A.add(tube([(sx * 0.215, 0.02, zz - 0.085), (sx * 0.19, 0.03, zz - 0.14), (sx * 0.15, 0.03, 1.455)], 0.022 * thick / 1.5, n=6),
              bone='clavicle_l' if sx > 0 else 'clavicle_r', look=look, smooth=50)
    for sx in (1, -1):                                   # small folded arms: shoulder -> elbow -> wrist crossing the sternum
        sh = V(sx * 0.15, -0.105, 1.37); el = V(sx * 0.205, -0.175, 1.19); wr = V(-sx * 0.05, -0.20, 1.27)
        A.add(KB.seg(sh, el, 0.013, 0.012, 1.6, 1.6, n=5), bone='spine_03', look=look, smooth=50)
        A.add(KB.seg(el, wr, 0.011, 0.010, 1.5, 1.5, n=5), bone='spine_03', look=look, smooth=50)
        for k in range(3):
            A.add(KB.seg(wr, wr + V(-sx * 0.05, -0.012 + 0.012 * k, -0.035 + 0.03 * k), 0.006, 0.004, 1.3, 1.0, n=4), bone='spine_03', look=look, smooth=50)
    A.add(band((0, 0.0, 1.30), 0.235, 0.185, 0.03, 0.008, tilt_y=10, n=24), bone='spine_03', look='rope')
    A.add(band((0, 0.0, 1.16), 0.215, 0.165, 0.03, 0.008, tilt_y=-8, n=24), bone='spine_02', look='rope')


@part
def shoulder_dome(A, look='chalk', R=0.21, z=1.50, h=0.17):
    """no head at all: the shoulders rise into a smooth dome"""
    d = revolve([(R * 1.25, -0.10), (R * 1.15, -0.03), (R, 0.03), (R * 0.72, h * 0.7), (R * 0.35, h * 0.95), (0.0, h)], n=16).scale((1.15, 0.8, 1.0))
    A.add(d.move(0, 0.01, z), bone='spine_03', look=look, smooth=180)


@part
def mitten_hands(A, look='chalk', size=0.26, th=0.05):
    """flat mitten-hands the size of shields instead of hands"""
    for sx in (1, -1):
        w = A.R.H('hand_l' if sx > 0 else 'hand_r')
        m = ellipsoid(size * 0.62, size * 0.5, th, n=14, m=7)
        m.v[:, 0] += 0.25 * size * np.clip(m.v[:, 0] / (size * 0.62), -1, 1) ** 2
        A.add(m.move(w + V(sx * size * 0.5, 0, 0)), bone='hand_l' if sx > 0 else 'hand_r', look=look, smooth=180)
        A.add(ellipsoid(size * 0.2, size * 0.14, th * 0.9, n=8, m=5).move(w + V(sx * size * 0.25, -size * 0.45, -0.01)), bone='hand_l' if sx > 0 else 'hand_r', look=look, smooth=180)
