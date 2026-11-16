"""asm_parts5.py -- parts for the stage-3 variants (hush hoop, wax flame and cauldron, stone lady and canopy, nets,
peat, forge limbs and crucible, fungus pagoda, ash mound).  Same rules as asm_parts.py."""
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
    net=asm._p('cloth', 'cloth', color=(0.20, 0.17, 0.11), hem_lo=-2, hem_hi=-1, fold=30.0, scale=3.0, weave_size=3.0, stain_amt=0.6),
    peatskin=asm._p('skin', 'skin', color=(0.03, 0.026, 0.02), blotch=(0.012, 0.01, 0.008), sore_amt=0.0, vein_amt=0.0, rough=0.3, dry=0.2),
    peat=asm._p('mud', 'rock', color=(0.03, 0.024, 0.018), dry=(0.10, 0.08, 0.055), dry_amt=0.6, scale=9.0),
    slag=asm._p('emis', 'emis', emit=(1.0, 0.36, 0.05), strength=3.0, base_mul=0.8, mottle=0.6, _name='lava'),
    flame=asm._p('emis', 'emis', emit=(1.0, 0.62, 0.22), strength=2.2, mottle=0.35, base_mul=1.0),
    pod=asm._p('skin', 'skin', color=(0.42, 0.44, 0.16), blotch=(0.25, 0.3, 0.1), sore_amt=0.0, vein_amt=0.2, rough=0.55),
)


# ================================================================================================ hush
@part
def hoop_torso(A, look='chalk', R=0.262, r=0.047, z=1.24, bone='spine_02'):
    """the torso is a hoop: a smooth ring from the hips to the collarbone that the level shows through
    (use with body hide = spine / clavicles)"""
    a = np.linspace(0, 2 * math.pi, 30, endpoint=False)
    pts = np.stack([R * 0.92 * np.cos(a), np.full(30, 0.012), z + R * 1.08 * np.sin(a)], axis=1)
    rr = r * (1.0 + 0.35 * np.abs(np.sin(a)) ** 4)
    t = tube(pts, r * 1.25, n=10, r2=rr, closed=True, up=(0, 1, 0))
    z_weights(t)
    A.add(t, look=look, smooth=180)
    for sx in (1, -1):                                   # shoulder knobs joining the arms to the ring
        A.add(ellipsoid(0.07, 0.06, 0.06, n=10, m=7).move(sx * 0.215, 0.03, 1.445), bone='clavicle_l' if sx > 0 else 'clavicle_r', look=look, smooth=180)
    A.add(M.loft_z([(0.90, 0, 0.02, 0.135, 0.105), (0.97, 0, 0.018, 0.11, 0.09), (1.0, 0, 0.012, 0.07, 0.06)], n=12), bone='pelvis', look=look, smooth=180)


# ================================================================================================ wax
@part
def flame(A, at=None, bone='spine_03', look='flame', h=0.34, r=0.07, sweep=(0.0, 0.55, 0.0), core=None):
    """a guttering flame standing on a point (default: the wick of a wax dome), streaming along `sweep`"""
    p0 = V(A.refs['dome_top'] if at is None else at)
    f = ellipsoid(r, r * 0.8, h * 0.5, n=10, m=9)
    t = np.clip((f.v[:, 2] + h * 0.5) / h, 0, 1)
    f.v[:, 0] *= np.sin(math.pi * np.clip(t * 0.9 + 0.12, 0, 1)) ** 0.8 * (1 - 0.6 * t)
    f.v[:, 1] *= np.sin(math.pi * np.clip(t * 0.9 + 0.12, 0, 1)) ** 0.8 * (1 - 0.6 * t)
    f.v[:, 2] += h * 0.5
    f.v += np.outer(t ** 1.6, V(sweep)) * h / 0.34
    f.v[:, 0] += 0.02 * np.sin(9 * t)
    A.add(f.move(p0 - V(0, 0, 0.02)), bone=bone, look=look, smooth=90)


@part
def cauldron_front(A, look='iron', wax='tallow', glow='ember', r=0.13, at=(0.0, -0.335, 1.10), bone='spine_02', strap='strap'):
    """small iron cauldron of molten wax carried on a chest harness in front, over a covered coal pan"""
    c = V(at)
    pot = revolve([(0.0, -r * 0.75), (r * 0.6, -r * 0.72), (r * 0.98, -r * 0.3), (r, 0.0), (r * 0.9, r * 0.28), (r * 0.96, r * 0.34), (r * 0.86, r * 0.34), (r * 0.8, r * 0.26)], n=14)
    A.add(pot.move(c), bone=bone, look=look, smooth=60)
    A.add(revolve([(0.0, r * 0.2), (r * 0.82, r * 0.2), (r * 0.82, r * 0.16)], n=14).move(c), bone=bone, look=wax, smooth=60)
    for a in (0.6, 2.2, 4.0):
        A.add(tube([c + V(r * 0.9 * math.cos(a), r * 0.9 * math.sin(a), r * 0.3), c + V(r * 1.0 * math.cos(a), r * 1.0 * math.sin(a), r * 0.05),
                    c + V(r * 0.98 * math.cos(a), r * 0.98 * math.sin(a), -r * 0.3)], [0.012, 0.01, 0.004], n=5), bone=bone, look=wax, smooth=70)
    pan = revolve([(0.0, -r * 1.3), (r * 0.7, -r * 1.28), (r * 0.8, -r * 1.05), (r * 0.74, -r * 1.05), (r * 0.6, -r * 1.2), (0.0, -r * 1.2)], n=12)
    A.add(pan.move(c), bone=bone, look=look, smooth=60)
    if glow:
        A.add(revolve([(0.0, -r * 1.12), (r * 0.6, -r * 1.14), (r * 0.6, -r * 1.2)], n=10).move(c), bone=bone, look=glow)
    for a in (0.8, 2.9, 5.0):
        A.add(cyl(c + V(r * 0.72 * math.cos(a), r * 0.72 * math.sin(a), -r * 1.08), c + V(r * 0.85 * math.cos(a), r * 0.85 * math.sin(a), -r * 0.5), 0.008, n=4),
              bone=bone, look=look)
    for sx in (1, -1):
        A.add(tube(M.polyline([c + V(sx * r * 0.95, 0.03, r * 0.2), V(sx * 0.15, -0.16, 1.40), V(sx * 0.13, -0.02, 1.52), V(sx * 0.10, 0.13, 1.45)], 9),
                   0.005, n=4, r2=0.016, up=(1, 0, 0)), bone='spine_03', look=strap, smooth=40)
        A.add(cyl(c + V(sx * r * 0.95, 0.03, r * 0.2), c + V(sx * 0.11, 0.17, 0.02), 0.009, n=5), bone=bone, look=look)


@part
def wick_bundle(A, bone='pelvis', at=(-0.19, 0.02, 1.02), count=12, length=0.42, look='linen'):
    """bundle of blank wicks hanging from the belt like a horse-tail"""
    at = V(at)
    rng = np.random.default_rng(4)
    A.add(ellipsoid(0.028, 0.028, 0.035, n=8, m=5).move(at), bone=bone, look='rope', smooth=60)
    for k in range(count):
        a = rng.uniform(0, 2 * math.pi)
        d = V(0.02 * math.cos(a), 0.02 * math.sin(a), 0)
        L_ = length * rng.uniform(0.8, 1.1)
        A.add(tube([at + d * 0.5, at + d * 1.5 + V(0, 0, -L_ * 0.5), at + d * 2.2 + V(0.01 * math.sin(k), 0, -L_)], 0.0045, n=4), bone=bone, look=look)


# ================================================================================================ stone
@part
def stone_lady(A, look='limestone', glow='glow', seed=4):
    """robed stone lady: gown meeting the floor as a fluted column, sleeves and hands carved as one praying block at the
    chest, tall pointed headdress with a stone veil falling to the hips in stiff pleats (use with body show=[])"""
    secs = [(0.0, 0, 0.02, 0.265, 0.24), (0.3, 0, 0.02, 0.215, 0.195), (0.7, 0, 0.02, 0.185, 0.165), (0.98, 0, 0.022, 0.172, 0.15), (1.10, 0, 0.02, 0.15, 0.128),
            (1.28, 0, 0.018, 0.172, 0.145), (1.40, 0, 0.018, 0.20, 0.15), (1.47, 0, 0.016, 0.185, 0.13), (1.52, 0, 0.01, 0.085, 0.085)]
    g, R = KX.gown2(secs, n=32, mod=KX.pleat_mod(seed=seed, ks=(16,), amp=0.07, hem_z=0.0, top_z=1.0, sharp=0.9), rag=0.0, seed=seed)
    g.smooth = 40.0
    KC.skin_cloth(g, body=A.rest, smooth=6, pelvis_mix=0.85, mix_top=1.0, mix_bot=0.55, limit=['pelvis', 'spine_01', 'spine_02', 'spine_03', 'thigh_l', 'thigh_r',
                                                                                             'calf_l', 'calf_r'])
    A.add(g, look=look)
    A.refs['robe'] = g
    head = M.loft_z([(1.50, 0, 0.0, 0.055, 0.058), (1.56, 0, -0.012, 0.074, 0.088), (1.64, 0, -0.02, 0.088, 0.104), (1.70, 0, -0.02, 0.086, 0.10), (1.76, 0, -0.015, 0.07, 0.08)], n=14)
    A.add(head, bone='Head', look=look, smooth=60)
    A.add(box(0.02, 0.02, 0.045, bevel=0.006).move(0, -0.118, 1.635), bone='Head', look=look)            # nose
    for sx in (1, -1):
        A.add(box(0.03, 0.008, 0.008).move(sx * 0.034, -0.112, 1.668), bone='Head', look='void')            # eye slots
    hd = M.loft_z([(1.70, 0, 0.0, 0.098, 0.112), (1.78, 0, 0.02, 0.092, 0.10), (1.95, 0, 0.07, 0.06, 0.062), (2.12, 0, 0.125, 0.028, 0.028), (2.20, 0, 0.15, 0.006, 0.006)], n=14)
    A.add(hd, bone='Head', look=look, smooth=50)
    top, nr = arc_pts((0, 0.0, 1.76), 0.105, 0.118, -10, 190, 12)
    vl = hang(top, 0.78, rows=6, out=nr, flare=0.09, rag=0.0, seed=seed, wave=0.0)
    vl.v[:, :2] *= 1 + 0.05 * np.sin(8 * np.arctan2(vl.v[:, 1], vl.v[:, 0]))[:, None]
    vl = vl.solidify(0.012)
    A.add(vl, bone='spine_03', look=look, smooth=40)
    for sx in (1, -1):                                                # sleeves converging on the praying block
        pts = M.polyline([(sx * 0.215, 0.03, 1.44), (sx * 0.25, -0.03, 1.27), (sx * 0.13, -0.15, 1.20), (sx * 0.02, -0.19, 1.27)], 9)
        A.add(tube(pts, np.linspace(0.07, 0.045, 9), n=9), bone='spine_03', look=look, smooth=50)
    hands = box(0.05, 0.075, 0.17, bevel=0.016)
    hands.v[:, 0] *= 1 - 0.5 * np.clip(hands.v[:, 2] / 0.085, 0, 1)
    A.add(hands.rot('x', -24).move(0, -0.205, 1.33), bone='spine_03', look=look, smooth=40)
    if glow:
        A.add(box(0.007, 0.07, 0.15).rot('x', -24).move(0, -0.208, 1.33), bone='spine_03', look=glow)


@part
def niche_canopy(A, look='limestone', z0=1.80, h=0.62, w=0.30, bone='Head'):
    """the pointed stone canopy of a wall niche still fused above the head like a tall mitre"""
    out = np.array([(-w / 2, 0.0), (w / 2, 0.0), (w / 2, h * 0.32), (w * 0.34, h * 0.62), (0.0, h), (-w * 0.34, h * 0.62), (-w / 2, h * 0.32)])
    c = M.prism(out, -0.11, 0.11).bevel(0.012)
    c.rot('x', 90).move(0, 0.0, z0)
    A.add(c, bone=bone, look=look, smooth=30)
    A.add(M.prism(np.array([(-w * 0.3, 0.02), (w * 0.3, 0.02), (w * 0.3, h * 0.3), (0.0, h * 0.62), (-w * 0.3, h * 0.3)]), -0.125, -0.10).rot('x', 90).move(0, 0, z0),
          bone=bone, look='void')
    for sx in (-1, 1):
        A.add(M.cone_spike(0.03, 0.14, n=5).move(sx * w * 0.47, 0.0, z0 + h * 0.3), bone=bone, look=look, smooth=30)
    A.add(M.cone_spike(0.03, 0.12, n=5).move(0, 0.0, z0 + h * 0.97), bone=bone, look=look, smooth=30)
    A.add(box(w * 1.1, 0.25, 0.05, bevel=0.01).move(0, 0.0, z0 - 0.02), bone=bone, look=look, smooth=30)


@part
def plinth_foot(A, side='l', look='limestone', seed=2):
    """a fragment of plinth still stuck to one foot"""
    for sx in A.sides(side):
        b = KX.lump(0.17, 0.2, 0.075, seed=seed, amp=0.18, freq=6.0)
        b.v[:, 2] = np.clip(b.v[:, 2], -0.05, 0.06)
        A.add(b.move(sx * 0.095, -0.05, 0.05), bone='foot_l' if sx > 0 else 'foot_r', look=look, smooth=30)


# ================================================================================================ marsh
@part
def cast_net(A, look='net', seed=5, sinkers='stone', floats='wood_z'):
    """huge weighted cast-net draped over the left shoulder and trailing behind like a train, stone sinkers on its hem;
    a string of cork floats round the neck"""
    xs = np.linspace(-0.05, 0.30, 9)
    top = np.stack([xs, 0.09 + 0.05 * np.sin(np.linspace(0, math.pi, 9)), 1.49 + 0.03 * np.sin(np.linspace(0, math.pi, 9))], axis=1)
    out = np.tile((0.0, 1.0, 0.0), (9, 1))
    ln = 1.5 + 0.25 * np.sin(np.linspace(0, math.pi, 9))
    n = hang(top, ln, rows=14, out=out, flare=0.55, rag=0.12, seed=seed, wave=0.03, holes=0.0, curve=2.2)
    n.v[:, 2] = np.maximum(n.v[:, 2], 0.012 + 0.01 * np.sin(20 * n.v[:, 0]))
    low = n.v[:, 2] < 0.06
    n.v[low, 1] += (0.06 - n.v[low, 2]) * 6.0                    # the hem lies out on the ground behind
    n = KW.punch(n, (15, 9), 0.42, seed=seed, t0=0.0)
    z_weights(n)
    A.add(n, look=look, smooth=180)
    front = hang(np.stack([xs[3:], np.full(6, -0.105), np.full(6, 1.46)], axis=1), 0.42, rows=6, out=np.tile((0.0, -1.0, 0.0), (6, 1)), flare=0.03, rag=0.4, seed=seed + 1,
                 holes=0.35, tatter=0.3)
    z_weights(front)
    A.add(front, look=look, smooth=180)
    rng = np.random.default_rng(seed)
    hem = np.where(n.v[:, 2] < 0.05)[0]                         # stone sinkers tied to the hem: they ride on the net itself
    for k in range(9):
        vi = int(hem[rng.integers(len(hem))]) if len(hem) else int(np.argmin(n.v[:, 2]))
        st = M.rock(0.035, 0.03, 0.022, seed=seed + k, n=9).move(n.v[vi] + V(0, 0, 0.012))
        st.W = np.tile(n.W[vi], (len(st.v), 1)).astype(np.float32)
        A.add(st, look=sinkers, smooth=40)
    for k in range(9):
        a = math.radians(-160 + 140 * k / 8)
        A.add(ellipsoid(0.022, 0.022, 0.018, n=7, m=5).move(0.125 * math.cos(a), -0.01 + 0.115 * math.sin(a), 1.50 + 0.018 * math.cos(2 * a)), bone='spine_03',
              look=floats, smooth=60)


@part
def creel(A, look='wicker', at=(0.0, 0.26, 1.24), r=0.13, h=0.28, bone='spine_03', strap='strap'):
    """lidded creel basket on the back"""
    c = V(at)
    b = M.loft_z([(-h / 2, 0, 0, r * 0.8, r * 0.62), (-h * 0.2, 0, 0, r, r * 0.75), (h * 0.3, 0, 0, r * 0.96, r * 0.72), (h / 2, 0, 0, r * 0.8, r * 0.6)], n=14)
    A.add(b.move(c), bone=bone, look=look, smooth=50)
    A.add(M.loft_z([(h / 2, 0, 0, r * 0.86, r * 0.66), (h / 2 + 0.03, 0, 0, r * 0.7, r * 0.52), (h / 2 + 0.045, 0, 0, 0.0, 0.0)], n=14).move(c), bone=bone, look=look, smooth=50)
    for zz in (-h * 0.3, 0.0, h * 0.3):
        A.add(band(c + V(0, 0, zz), r * 0.99 + 0.004, r * 0.74 + 0.004, 0.012, 0.006, n=16), bone=bone, look=look)
    for sx in (1, -1):
        A.add(tube(M.polyline([c + V(sx * r * 0.7, -r * 0.5, h * 0.3), V(sx * 0.16, 0.10, 1.50), V(sx * 0.15, -0.09, 1.46), V(sx * 0.17, -0.11, 1.26)], 9), 0.005, n=4, r2=0.014,
                   up=(1, 0, 0)), bone=bone, look=strap, smooth=40)


@part
def reed_straw(A, look='reed', length=0.62, cord='rope'):
    """long reed breathing-straw bound to the mouth, sticking straight up past the head"""
    m = V(0.0, -0.118, 1.60)
    pts = M.polyline([m + V(0, 0.01, 0), m + V(0, -0.045, 0.02), m + V(0, -0.055, 0.12), m + V(0.01, -0.04, length)], 9)
    A.add(tube(pts, 0.0085, n=6), bone='Head', look=look, smooth=50)
    A.add(band((0, -0.012, 1.60), 0.094, 0.108, 0.014, 0.006, n=18), bone='Head', look=cord)
    for zz in (0.05, 0.3):
        A.add(torus(0.012, 0.004, n=8, m=4).move(pts[3 if zz < 0.1 else 6]), bone='Head', look=cord)


@part
def back_slab(A, look='peat', rx=0.24, ry=0.10, rz=0.33, at=(0.0, 0.19, 1.27), bone='spine_02', seed=3):
    """a slab stuck to the back like a turtle shell (peat, slag, ice)"""
    s = KX.lump(rx, ry, rz, seed=seed, amp=0.2, freq=7.0)
    A.add(s.move(V(at)), bone=bone, look=look, smooth=50)


# ================================================================================================ forge
@part
def tong_arm(A, side='r', look='iron', length=1.35, glow='ember'):
    """the arm from the elbow down is a pair of blacksmith's tongs, jaws open (use with body hide of that forearm + hand)"""
    for sx in A.sides(side):
        bn = 'lowerarm_l' if sx > 0 else 'lowerarm_r'
        e = A.R.H(bn)
        A.add(ellipsoid(0.07, 0.065, 0.065, n=10, m=7).move(e), bone=bn, look=look, smooth=50)
        A.add(revolve([(0.075, 0.0), (0.06, 0.05), (0.03, 0.09)], n=10).rot('y', sx * 90).move(e), bone=bn, look=look, smooth=40)
        piv = e + V(sx * length * 0.55, 0, 0)
        for sz in (-1, 1):
            pts = M.polyline([e + V(sx * 0.05, 0, sz * 0.02), piv + V(-sx * 0.2, 0, sz * 0.05), piv, piv + V(sx * length * 0.2, 0, -sz * 0.11),
                              e + V(sx * length, 0, -sz * 0.06)], 12)
            A.add(tube(pts, 0.016, n=6, r2=0.024, up=(0, 1, 0)), bone=bn, look=look, smooth=40)
            if glow:
                A.add(ellipsoid(0.03, 0.022, 0.02, n=7, m=5).move(e + V(sx * (length - 0.02), 0, -sz * 0.058)), bone=bn, look=glow, smooth=60)
        A.add(cyl(piv + V(0, -0.03, 0), piv + V(0, 0.03, 0), 0.022, n=8), bone=bn, look=look, smooth=40)


@part
def chain_wrap(A, bones=('upperarm_l', 'lowerarm_l'), look='rust', turns=4.0, link=0.04):
    """a limb wound in chain"""
    for bn in bones:
        h = A.R.H(bn); t = A.R.T(bn)
        ax = unit(t - h); fr = M.frame_from(ax)
        L_ = np.linalg.norm(t - h)
        pts = []
        for s in np.linspace(0.08, 0.95, int(turns * 12)):
            a = 2 * math.pi * turns * s
            rr = KI.limb_radius(A.rest, bn, float(s)) + 0.012
            pts.append(h + ax * L_ * s + (fr[:, 0] * math.cos(a) + fr[:, 2] * math.sin(a)) * rr)
        A.add(M.chain(np.array(pts), link=link, r=0.0065), bone=bn, look=look, smooth=60)


@part
def iron_hood(A, look='rust', grille='void'):
    """riveted iron hood with a single round grille"""
    h = M.loft_z([(1.535, 0, -0.012, 0.092, 0.10), (1.60, 0, -0.016, 0.108, 0.122), (1.70, 0, -0.018, 0.112, 0.126), (1.77, 0, -0.018, 0.098, 0.11), (1.805, 0, -0.018, 0.05, 0.056),
                  (1.81, 0, -0.018, 0.0, 0.0)], n=14)
    A.add(h, bone='Head', look=look, smooth=45)
    A.add(revolve([(0.0, 0.012), (0.045, 0.012), (0.052, 0.0)], n=12).rot('x', 90).move(0, -0.14, 1.665), bone='Head', look=look, smooth=40)
    A.add(revolve([(0.0, 0.016), (0.036, 0.016), (0.036, 0.01)], n=10).rot('x', 90).move(0, -0.14, 1.665), bone='Head', look=grille)
    for k in range(3):
        A.add(box(0.07, 0.006, 0.005).move(0, -0.158, 1.648 + 0.017 * k), bone='Head', look=look)
    for a in np.linspace(0, 2 * math.pi, 10, endpoint=False):
        p = V(0.11 * math.cos(a), -0.018 + 0.124 * math.sin(a), 1.60)
        A.add(KC3.rivet(p, unit(V(math.cos(a), math.sin(a), 0)), r=0.007), bone='Head', look=look)


@part
def crucible_head(A, look='iron', slag='slag', r=0.19, h=0.30, z=1.86, bone='spine_03'):
    """a crucible of molten slag carried on the shoulders in an iron collar-frame, directly above the head, with a
    pouring lip towards the front"""
    c = V(0, 0.0, z)
    b = revolve([(0.0, 0.0), (r * 0.7, 0.0), (r * 0.82, h * 0.1), (r, h * 0.9), (r * 1.05, h), (r * 0.95, h), (r * 0.9, h * 0.9), (0.0, h * 0.85)], n=16)
    lip = (b.v[:, 1] < -r * 0.6) & (b.v[:, 2] > h * 0.8)
    b.v[lip, 1] -= 0.07
    A.add(b.move(c), bone=bone, look=look, smooth=50)
    A.add(revolve([(0.0, h * 0.86), (r * 0.9, h * 0.88), (r * 0.9, h * 0.84)], n=16).move(c), bone=bone, look=slag, smooth=60)
    A.add(tube([c + V(0, -r * 1.02 - 0.06, h * 0.9), c + V(0, -r * 1.02 - 0.075, h * 0.6), c + V(0, -r * 1.0 - 0.07, h * 0.2)], [0.014, 0.011, 0.004], n=5), bone=bone, look=slag)
    for zz in (h * 0.25, h * 0.7):
        A.add(torus(r * (0.86 + 0.14 * zz / h) + 0.006, 0.012, n=16, m=4).move(c + V(0, 0, zz)), bone=bone, look=look)
    for sx in (1, -1):
        for y in (-0.11, 0.13):
            A.add(cyl(V(sx * 0.19, y * 0.6, 1.49), c + V(sx * r * 0.75, y, 0.01), 0.014, n=5), bone=bone, look=look)
        A.add(cyl(V(sx * 0.19, -0.11 * 0.6, 1.49), V(sx * 0.19, 0.13 * 0.6, 1.49), 0.016, n=5), bone=bone, look=look)
    A.add(band((0, 0.01, 1.50), 0.21, 0.13, 0.035, 0.01, n=20), bone=bone, look=look)


# ================================================================================================ fungus / ash
@part
def pagoda_head(A, look='fungus', gill='glow', tiers=5, R=0.19, z=1.52, bone='spine_03'):
    """where the head was, a tall stack of bracket fungus rises in tiers like a pagoda of plates"""
    zz = z
    for k in range(tiers):
        rr = R * (1 - 0.16 * k)
        cap, gl = KB2.fungus_cap(R=rr, h=rr * 0.34, seed=3 + k)
        tl = 7 * math.sin(k * 1.9)
        for p, lk in ((cap, look), (gl, gill)):
            A.add(p.rot('y', tl).move(0.012 * math.sin(k * 2.3), 0.0, zz), bone=bone if k < 2 else 'Head', look=lk, smooth=70)
        A.add(cyl((0, 0, zz - 0.02), (0, 0, zz + rr * 0.34 + 0.07), 0.045 * (1 - 0.1 * k), n=7), bone=bone if k < 2 else 'Head', look=look, smooth=70)
        zz += rr * 0.34 + 0.085


@part
def puffballs(A, look='pod', pin='glow', count=16, r=(0.045, 0.085), seed=6):
    """a cluster of round puffball pods like a bunch of grapes over the back and shoulders, each with a glowing pinpoint"""
    b = A.rest
    m = (b.wsum(['spine_02', 'spine_03', 'clavicle_l', 'clavicle_r']) > 0.5) & (b.v[:, 1] > 0.02) & (b.v[:, 2] > 1.12)
    idx = KA.scatter(b, m, count, seed=seed, min_dist=0.075)
    n0 = len(A.C.parts)
    KA.blobs(A.C, b, idx, A.lk(look), r=r, seed=seed, sink=0.15, squash=1.0, n=9, m=6)
    rng = np.random.default_rng(seed)
    for p in list(A.C.parts[n0:]):
        c = p.center(); rr = (p.v[:, 2].max() - p.v[:, 2].min()) / 2
        d = unit(c - V(0, 0.0, c[2] - 0.15))
        e = ellipsoid(0.012, 0.012, 0.012, n=6, m=4).move(c + d * rr * 0.97)
        e.W = np.tile(p.W[0], (len(e.v), 1)).astype(np.float32)
        A.add(e, look=pin, smooth=60)


@part
def ash_mound(A, look='ashcrust', z_top=1.0, R=0.42, seed=5):
    """the shins are fused into a trailing skirt of ash: a mound from the hips to the floor, wider behind"""
    secs = [(0.0, 0, 0.10, R, R * 1.15), (0.12, 0, 0.09, R * 0.86, R * 0.98), (0.4, 0, 0.06, R * 0.6, R * 0.66), (0.75, 0, 0.035, R * 0.44, R * 0.42), (z_top, 0, 0.02, 0.17, 0.14),
            (z_top + 0.07, 0, 0.015, 0.155, 0.125)]
    g, Rr = KX.gown2(secs, n=26, mod=KX.pleat_mod(seed=seed, amp=0.10, hem_z=0.0, top_z=0.9), rag=0.0, seed=seed)
    g.rough(0.012, 9.0, seed=float(seed))
    g.v[:, 2] = np.maximum(g.v[:, 2], 0.0)
    g.smooth = 60.0
    M.weights_from(g, {'pelvis': 1.0})
    A.add(g, look=look)
