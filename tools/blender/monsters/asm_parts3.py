"""asm_parts3.py -- parts for the stage-2 bases and their variants (raiders, effigies, marsh-folk, wax, forge ...).
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
from monlib import V, tube, ellipsoid, box, join, revolve, torus, hang, arc_pts, band, unit
from asm_parts import part, girth_of, cyl, item_parts
from asm_parts2 import z_weights, ring_disc
import asm

asm.PRE.update(
    coat=asm._p('leather', 'leather', color=(0.12, 0.085, 0.06), scuff=0.7, scale=26.0, dirt=(0.09, 0.08, 0.07)),
    patch=asm._p('cloth', 'cloth', color=(0.16, 0.13, 0.10), hem_lo=-2, hem_hi=-1, fold=20.0, stain_amt=0.7),
    trousers=asm._p('cloth', 'cloth', color=(0.085, 0.08, 0.075), hem_lo=-2, hem_hi=-1, fold=18.0, stain_amt=0.6),
    ashpaint=asm._p('skin', 'skin', color=(0.60, 0.59, 0.57), blotch=(0.34, 0.31, 0.29), sore_amt=0.0, vein_amt=0.0, rough=0.85, dry=1.0),
    blued=asm._p('metal', 'metal', color=(0.075, 0.09, 0.125), rust=(0.12, 0.08, 0.06), rust2=(0.04, 0.03, 0.03), amount=0.3,
                 edge_color=(0.42, 0.46, 0.55), rough=0.3, edge=1.0),
    reed=asm._p('wood', 'leather', color=(0.26, 0.22, 0.11), axis=(0, 0, 1)),
    wicker=asm._p('wood', 'leather', color=(0.17, 0.125, 0.07), axis=(0, 0, 1)),
    skin_bog=asm._p('skin', 'skin', color=(0.20, 0.24, 0.19), blotch=(0.09, 0.13, 0.10), vein=(0.05, 0.09, 0.07), vein_amt=0.5, sore_amt=0.2,
                    rough=0.3, hands_dark=0.6, feet_dirt=1.0, dark=(0.03, 0.035, 0.025)),
    limestone=asm._p('rock', 'rock', color=(0.22, 0.215, 0.20), ash=(0.36, 0.355, 0.33), ash_amt=0.55, scale=9.0, crack_w=0.02, rough=0.85),
    tallow=asm._p('wax', 'bone', color=(0.66, 0.58, 0.38), soot=0.12),
    bronze=asm._p('metal', 'metal', color=(0.22, 0.15, 0.07), rust=(0.07, 0.16, 0.13), rust2=(0.04, 0.08, 0.06), amount=0.5,
                  edge_color=(0.62, 0.48, 0.25), rough=0.42, hammer=14.0),
    chalk=asm._p('wax', 'bone', color=(0.80, 0.80, 0.78), soot=0.0),
    ashcrust=asm._p('rock', 'rock', color=(0.20, 0.195, 0.19), ash=(0.42, 0.41, 0.40), ash_amt=0.8, scale=16.0, crack_w=0.035, rough=0.95),
    charwood=asm._p('rock', 'rock', color=(0.028, 0.026, 0.025), ash=(0.50, 0.49, 0.47), ash_amt=0.35, scale=20.0, crack_w=0.06, bump=1.0,
                    heat=(0.5, 0.12, 0.02), heat_amt=0.25),
    fungus=asm._p('leather', 'leather', color=(0.30, 0.22, 0.12), scuff=0.2, scale=18.0, rough=0.7),
    silk=asm._p('wraps', 'cloth', axis=(0.2, 0.1, 1.0), bands=34.0, color=(0.46, 0.46, 0.45), dark=(0.16, 0.16, 0.17), stain=(0.30, 0.29, 0.27), stain_amt=0.3),
)


# ================================================================================================ raiders
@part
def patches(A, on='robe', look='patch', spots=((-70, 0.95, 1.20, 26), (40, 0.70, 0.90, 30), (150, 1.15, 1.38, 30), (-140, 0.72, 0.96, 24)), seed=3):
    """sewn-on patches lying on a robe / coat: spots = [(angle deg, z0, z1, angular width deg)]"""
    R = A.refs[on + '_rings']
    n = R.shape[1]
    for k, (ang, z0, z1, wd) in enumerate(spots):
        j0 = KB2.col_of(n, ang - wd / 2); j1 = KB2.col_of(n, ang + wd / 2)
        if j1 == j0:
            j1 = (j0 + 1) % n
        p = KB2.strip(R, j0, j1, z0=z0, z1=z1, offset=0.006, subdiv=2, rag=0.012, seed=seed + k)
        A.add(p, look=look, like=A.refs[on], smooth=180)


@part
def brim_hat(A, look='hide', R=0.26, crown=0.13, z=1.745, tilt=(5, -8), droop=0.035, band_look='strap'):
    """wide-brimmed hat (lamplighter's): tall crown, sagging brim"""
    prof = [(R, -droop), (R * 0.97, -droop + 0.006), (R * 0.6, 0.004), (0.118, 0.012), (0.105, 0.03), (0.098, crown * 0.75), (0.085, crown), (0.0, crown + 0.012)]
    h = revolve(prof, n=18).scale((0.95, 1.08, 1.0))
    a = np.arctan2(h.v[:, 1], h.v[:, 0])
    rr = np.linalg.norm(h.v[:, :2], axis=1)
    h.v[:, 2] += 0.022 * np.sin(2 * a + 0.6) * np.clip((rr - 0.12) / (R - 0.12), 0, 1)
    h.rot('y', tilt[0]).rot('x', tilt[1]).move(0.0, -0.015, z)
    A.add(h, bone='Head', look=look, smooth=50)
    A.add(band((0, -0.015, z + 0.03), 0.103, 0.118, 0.028, 0.005, tilt_x=tilt[1], n=18), bone='Head', look=band_look)


@part
def pot_rack(A, look='wood_z', pot_look='clay', rows=2, cols=3, z=1.50, y=0.21, r=0.085, cord='rope'):
    """yoke-frame on the back carrying round clay pots in rows, like a rack of skulls behind the shoulders"""
    w = (cols - 1) * r * 2.25
    for sx in (-1, 1):
        A.add(cyl((sx * (w / 2 + r), y - 0.02, 1.02), (sx * (w / 2 + r), y, z + (rows - 1) * r * 2.2 + r * 1.2), 0.014, n=6), bone='spine_03', look=look)
    for j in range(rows):
        zz = z + j * r * 2.2
        A.add(cyl((-w / 2 - r - 0.03, y, zz - r * 0.75), (w / 2 + r + 0.03, y, zz - r * 0.75), 0.012, n=6), bone='spine_03', look=look)
        for i in range(cols):
            x = -w / 2 + i * r * 2.25
            pot = revolve([(0.0, -r * 0.82), (r * 0.5, -r * 0.8), (r * 0.95, -r * 0.35), (r, 0.0), (r * 0.82, r * 0.5), (r * 0.42, r * 0.8), (r * 0.36, r * 0.92),
                           (r * 0.46, r * 1.0), (0.0, r * 0.98)], n=10)
            pot.rot('y', 9 * math.sin(i * 2.1 + j)).rot('x', 7 * math.cos(i + 2 * j))
            A.add(pot.move(x, y + 0.045, zz + 0.02), bone='spine_03', look=pot_look, smooth=70)
            A.add(torus(r * 0.44, 0.006, n=8, m=4).move(x, y + 0.045, zz + r * 0.86), bone='spine_03', look=cord)
    for z_, tl in ((1.22, 15.0), (1.38, -15.0)):
        cx, cy, rx, ry = girth_of(A, 'robe' if 'robe' in A.refs else 'body', z_)
        A.add(band((0, cy + 0.02, z_), rx + 0.014, ry + 0.035, 0.028, 0.007, tilt_y=tl, n=22), bone='spine_03', look='strap')


@part
def pelt_cloak(A, look='fur', seed=4, length=0.95, ears=True):
    """shaggy cloak of pelts with the head-skin pulled up as a hood (ragged ear points)"""
    sh, G = KW.poncho(X=0.40, Yf=0.19, Yb=0.24, seed=seed, rag=0.35, point=0.14)
    sh.rough(0.012, 22.0, seed=float(seed))
    A.add(sh, look=look, skin=3)
    A.refs['mantle'] = sh
    cp = KC.cape(width=0.50, length=length, z_top=1.47, y=0.17, rag=0.45, holes=0.05, tatter=0.5, seed=seed + 1, flare=0.22)
    cp.rough(0.01, 20.0, seed=float(seed) + 1)
    KC.cape_weights(cp, z_top=1.47)
    A.add(cp, look=look)
    hd = KW.rag_hood(seed=seed, rx=0.118, rz=0.14, tail=0.10, tail_drop=0.10)
    hd.rough(0.008, 26.0, seed=float(seed) + 2)
    A.add(hd, bone='Head', look=look)
    if ears:
        for sx in (1, -1):
            e = M.cone_spike(0.034, 0.10, n=5).scale((1.0, 0.45, 1.0)).rot('y', sx * 24).rot('x', -12)
            A.add(e.move(sx * 0.075, -0.035, 1.80), bone='Head', look=look, smooth=40)
        A.add(M.cone_spike(0.03, 0.12, n=5).scale((1.0, 0.6, 1.0)).rot('x', -75).move(0, -0.13, 1.775), bone='Head', look=look, smooth=40)   # snout flap


@part
def quilt_arm(A, side='l', look='patch', r=1.22):
    """thick quilted glove / sleeve wrapping one arm to the shoulder"""
    for sx in A.sides(side):
        s = '_l' if sx > 0 else '_r'
        b = A.rest
        m = b.wsum(['upperarm' + s, 'lowerarm' + s] + M.RIG.chain('hand' + s)) > 0.5
        p = KF.shell(b, m, 0.016)
        A.add(p, look=look, smooth=180)
        for bn, ts in (('upperarm' + s, (0.25, 0.6, 0.95)), ('lowerarm' + s, (0.3, 0.65))):
            for t in ts:
                rr = KI.limb_radius(b, bn, t) + 0.009            # the quilting cords bite into the padding (never stand off it)
                A.add(KI.limb_ring(bn, t, rr, thick=0.009), bone=bn, look='strap')


# ================================================================================================ stone effigies
@part
def tomb_slab(A, look='limestone', w=0.74, h=2.06, th=0.085, y=0.215, z0=0.03, bone='spine_02', glow='glow', pillow=True, seed=3):
    """half a tomb lid still fused to the back from the heels to above the head, a carved pillow behind the helm and a
    glowing inscription along its edges"""
    s = box(w, th, h, bevel=0.012)
    s.rough(0.004, 9.0, seed=float(seed))
    c = V(0, y + th / 2, z0 + h / 2)
    A.add(s.move(c), bone=bone, look=look, smooth=30)
    chip = KX.lump(0.16, 0.06, 0.12, seed=seed, amp=0.3)                      # broken corner
    A.add(chip.move(c + V(w * 0.42, 0, h * 0.47)), bone=bone, look=look, smooth=30)
    if pillow:
        p = box(0.34, 0.09, 0.22, bevel=0.04)
        p.v[:, 1] *= 1 - 0.5 * np.exp(-((p.v[:, 0] / 0.09) ** 2 + (p.v[:, 2] / 0.07) ** 2))
        A.add(p.move(0, y - 0.03, 1.70), bone=bone, look=look, smooth=60)
        for sx in (-1, 1):
            A.add(ellipsoid(0.028, 0.028, 0.028, n=7, m=5).move(sx * 0.17, y - 0.035, 1.60), bone=bone, look=look)
    if glow:
        for sx in (-1, 1):
            for k in range(7):
                zz = z0 + 0.14 + k * (h - 0.3) / 7
                A.add(box(0.006, 0.05, (h - 0.3) / 7 * 0.72).move(sx * (w / 2 + 0.001), y + th / 2, zz + 0.08), bone=bone, look=glow)


@part
def stone_armour(A, look='limestone', helm=True, visor='void'):
    """carved knight's harness in one material: great helm, gorget, pauldrons, breast, tassets, couters, poleyns"""
    if helm:
        h = M.loft_z([(1.545, 0, -0.012, 0.085, 0.095), (1.60, 0, -0.016, 0.103, 0.117), (1.70, 0, -0.018, 0.108, 0.122), (1.775, 0, -0.018, 0.098, 0.11),
                      (1.815, 0, -0.018, 0.06, 0.068), (1.825, 0, -0.018, 0.0, 0.0)], n=16)
        A.add(h, bone='Head', look=look, smooth=45)
        A.add(box(0.15, 0.012, 0.016).move(0, -0.136, 1.672), bone='Head', look=visor)
        A.add(box(0.012, 0.012, 0.12).move(0, -0.137, 1.63), bone='Head', look=look)
        A.add(box(0.012, 0.03, 0.26, bevel=0.003).move(0, -0.02, 1.83).rot('x', 90, pivot=(0, -0.02, 1.80)), bone='Head', look=look)
    A.add(KC3.torso_plate([(1.10, 0, 0.01, 0.168, 0.135), (1.22, 0, 0.008, 0.175, 0.145), (1.36, 0, 0.012, 0.235, 0.168), (1.46, 0, 0.014, 0.225, 0.145),
                           (1.515, 0, 0.012, 0.11, 0.105)], n=18, ridge=0.07), bone='spine_03', look=look, smooth=45)
    A.add(KC3.torso_plate([(0.93, 0, 0.02, 0.19, 0.15), (1.02, 0, 0.02, 0.182, 0.142), (1.11, 0, 0.012, 0.165, 0.13)], n=18), bone='pelvis', look=look, smooth=45)
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        for k, (rr, yy, zz, ang, bn) in enumerate(((0.115, 0.05, 1.485, 20, 'clavicle'), (0.10, 0.056, 1.455, 48, 'upperarm'))):
            A.add(KX.lame(rr, 0.07, n=12).rot('y', ang).move(0.205 + 0.06 * k, yy, zz), bone='clavicle_l' if bn == 'clavicle' else 'upperarm_l', look=look,
                  smooth=45, flip=sx < 0)
        A.add(KC3.limb_plate('upperarm' + s, [(0.3, 0.092), (0.62, 0.088), (0.95, 0.075)], n=10, lip=0.004), bone='upperarm' + s, look=look, smooth=45)
        A.add(KC3.limb_plate('lowerarm' + s, [(0.08, 0.075), (0.5, 0.068), (0.92, 0.05)], n=10, lip=0.004), bone='lowerarm' + s, look=look, smooth=45)
        A.add(KC3.limb_plate('thigh' + s, [(0.12, 0.125), (0.55, 0.112), (0.95, 0.09)], n=12, lip=0.004), bone='thigh' + s, look=look, smooth=45)
        A.add(KC3.limb_plate('calf' + s, [(0.04, 0.09), (0.4, 0.095), (0.9, 0.066)], n=12, lip=0.004), bone='calf' + s, look=look, smooth=45)
        A.add(ellipsoid(0.066, 0.07, 0.066, n=9, m=6).move(KC3.BP('calf' + s, 0.0, (0, -0.03, 0))), bone='calf' + s, look=look, smooth=45)
        A.add(ellipsoid(0.05, 0.055, 0.05, n=9, m=6).move(KC3.BP('lowerarm' + s, 0.0, (0, 0.02, 0))), bone='lowerarm' + s, look=look, smooth=45)
        A.add(box(0.105, 0.26, 0.085, bevel=0.02).move(sx * 0.089, -0.06, 0.045), bone='foot' + s, look=look, smooth=45)


# ================================================================================================ marsh folk
@part
def reed_kilt(A, look='reed', z=1.03, length=0.42, tiers=2, pad=0.012, seed=5):
    """kilt of reed blades hanging from a cord round the hips (real blades, two staggered tiers)"""
    cx, cy, rx, ry = girth_of(A, 'body', z)
    rng = np.random.default_rng(seed)
    for j in range(tiers):
        n = 30 + 4 * j
        for k in range(n):
            a = 2 * math.pi * (k + 0.5 * j) / n
            out = unit(V(math.cos(a) / (rx + pad), math.sin(a) / (ry + pad), 0))
            p0 = V((rx + pad + 0.006 * j) * math.cos(a), cy + (ry + pad + 0.006 * j) * math.sin(a), z - 0.02 * j)
            L_ = length * rng.uniform(0.7, 1.08) * (1 - 0.25 * j)
            bl = M.blade([(0.0, 0.012, 0.012, 0.003), (L_ * 0.6, 0.011, 0.011, 0.003), (L_, 0.0, 0.002, 0.001)])
            bl.rot('x', -90 - rng.uniform(6, 16) - 6 * j).rot('y', rng.uniform(-8, 8))
            bl.tf(M.frame_from(-out, (0, 0, 1)))
            bone = 'pelvis' if abs(p0[0]) < 0.04 else ('thigh_l' if p0[0] > 0 else 'thigh_r')
            if j == 0 and k % 3 == 0:
                bone = 'pelvis'
            A.add(bl.move(p0), bone=bone, look=look, smooth=30)
    A.add(band((0, cy, z + 0.005), rx + pad + 0.006, ry + pad + 0.006, 0.03, 0.01, n=22), bone='pelvis', look='rope')


@part
def trap_helm(A, look='wicker', Ln=0.62, R=0.168, z=1.495, seed=2):
    """conical wicker fish-trap worn over the whole head: mouth down round the neck, open point upward"""
    ts = np.linspace(0, 1, 7)
    secs = [(z + Ln * t, 0, -0.015, R * 0.97 * (1 - 0.78 * t ** 1.3), R * 0.97 * (1 - 0.78 * t ** 1.3)) for t in ts]
    cone = M.loft_z(secs, n=14, cap0=False, cap1=False)
    cone.rough(0.003, 40.0, seed=float(seed))
    A.add(cone, bone='Head', look=look, smooth=40)
    t = KX.eel_trap(Ln=Ln, R=R, ribs=11, hoops=5, seed=seed, r=0.0065)
    t.rot('x', 90)                         # taper direction +Y -> +Z
    A.add(t.move(0, -0.015, z), bone='Head', look=look, smooth=50)
    A.add(ellipsoid(0.085, 0.095, 0.108, n=10, m=7).move(0, -0.005, 1.675), bone='Head', look='void')
    A.add(ellipsoid(R * 0.2, R * 0.2, 0.01, n=8, m=3).move(0, -0.015, z + Ln * 0.985), bone='Head', look='void')


@part
def eel_string(A, bone='pelvis', at=(-0.15, -0.07, 1.02), count=3, look='skin_bog', length=0.38):
    """string of dead eels hanging at the belt"""
    at = V(at)
    for k in range(count):
        x = at[0] + 0.035 * (k - (count - 1) / 2)
        L_ = length * (0.8 + 0.25 * ((k * 5) % 3) / 2)
        pts = M.polyline([(x, at[1], at[2]), (x + 0.01, at[1] - 0.02, at[2] - L_ * 0.4), (x - 0.012, at[1] - 0.012, at[2] - L_ * 0.8), (x, at[1] - 0.02, at[2] - L_)], 8)
        A.add(tube(pts, [0.012, 0.016, 0.017, 0.016, 0.014, 0.011, 0.007, 0.003], n=6), bone=bone, look=look, smooth=70)
    A.add(cyl(at + V(-0.06, 0, 0.005), at + V(0.06, 0, 0.005), 0.005, n=4), bone=bone, look='rope')


# ================================================================================================ wax
@part
def wax_dome(A, look='tallow', R=0.185, z0=1.36, z1=1.90, bone='spine_03', wick=True, drips=9, seed=2, melted=None):
    """smooth dome of poured wax hiding head and shoulders (narrower than the shoulders: a candle-end), thick runs down
    its sides and a short wick on the crown.  melted = 'l' | 'r' cuts half of it away to show the jaw"""
    H = z1 - z0
    prof = [(R * 1.06, 0.0), (R * 1.10, H * 0.06), (R * 1.0, H * 0.22), (R * 0.96, H * 0.5), (R * 0.9, H * 0.72), (R * 0.68, H * 0.9), (R * 0.36, H * 0.985), (0.0, H)]
    d = revolve(prof, n=18, cap=False).scale((1.0, 0.92, 1.0))
    d.rough(0.006, 9.0, seed=float(seed))
    if melted:
        sx = 1 if melted == 'l' else -1
        t = np.clip((sx * d.v[:, 0] + 0.02) / R, 0, 1) * np.clip((d.v[:, 2] - H * 0.15) / (H * 0.5), 0, 1)
        d.v[:, 2] -= t * H * 0.42
        d.v[:, 0] -= sx * t * R * 0.25
    A.add(d.move(0, 0.0, z0), bone=bone, look=look, smooth=80)
    rng = np.random.default_rng(seed)
    for k in range(drips):
        a = 2 * math.pi * (k + rng.uniform(0.2, 0.8)) / drips
        L_ = rng.uniform(0.07, 0.2)
        x = R * 1.07 * math.cos(a); y = R * 1.07 * 0.92 * math.sin(a)
        pts = [(x, y, z0 + H * 0.2), (x * 1.03, y * 1.03, z0 + 0.02), (x * 1.02, y * 1.02, z0 - L_ * 0.6), (x * 1.0, y * 1.0, z0 - L_)]
        A.add(tube(np.array(pts), [0.016, 0.02, 0.014, 0.004], n=6), bone=bone, look=look, smooth=70)
    if wick:
        A.add(tube([(0, 0, z1 - 0.01), (0.004, 0, z1 + 0.03), (0.012, 0.004, z1 + 0.055)], 0.0045, n=5), bone=bone, look='wick')
    A.refs['dome_top'] = V(0.012, 0.004, z1 + 0.055)


@part
def wax_drips(A, look='tallow', side='both', seed=3, threads=True):
    """stalactites of wax along the forearms, at the elbows and on the fingertips (they hang 'down' when the arms hang)"""
    rng = np.random.default_rng(seed)
    for sx in A.sides(side):
        s = '_l' if sx > 0 else '_r'
        for bn, ts in (('lowerarm' + s, (0.05, 0.3, 0.55, 0.8)), ('upperarm' + s, (0.75, 0.95))):
            for t in ts:
                rr = KI.limb_radius(A.rest, bn, t)
                p0 = KC3.BP(bn, t) + V(0, rng.uniform(-0.02, 0.02), -rr * 0.6)
                L_ = rng.uniform(0.06, 0.15)
                A.add(tube([p0, p0 + V(sx * L_ * 0.35, 0, -rr * 0.5 - L_ * 0.5), p0 + V(sx * L_ * 0.7, 0, -rr * 0.5 - L_)], [0.017, 0.011, 0.003], n=5),
                      bone=bn, look=look, smooth=70)
        for fg in ('index', 'middle', 'ring', 'pinky'):
            bn = fg + '_03' + s
            p0 = A.R.T(bn)
            L_ = rng.uniform(0.05, 0.12)
            A.add(tube([p0 - V(sx * 0.02, 0, 0), p0 + V(sx * L_ * 0.5, 0, -0.004), p0 + V(sx * L_, 0, -0.008)], [0.012, 0.008, 0.002], n=5), bone=bn, look=look, smooth=70)
    A.add(KF.shell(A.rest, A.rest.wsum(M.RIG.chain('hand_l') + M.RIG.chain('hand_r')) > 0.5, 0.004), look=look, smooth=180)


# ================================================================================================ forge
@part
def bellows_chest(A, look='coat', hoop='iron', z0=1.08, z1=1.47, rx=0.34, ry=0.225, folds=5, handles='wood_x', pipe='iron', glow='ember'):
    """great leather concertina bellows in place of the chest, wider than the shoulders; two wooden handles stick out
    sideways; the nozzle is a long iron pipe rising in front of the face like a trunk"""
    prof = []
    H = z1 - z0
    for i in range(folds * 2 + 1):
        t = i / (folds * 2)
        prof.append((1.0 if i % 2 else 0.80, z0 + H * t))
    secs = [(z, 0, 0.0, rx * f, ry * f) for f, z in prof]
    b = M.loft_z(secs, n=20)
    A.add(b, bone='spine_02', look=look, smooth=30)
    for f, z in prof:
        if f == 1.0:
            A.add(band((0, 0, z), rx + 0.004, ry + 0.004, 0.012, 0.008, n=24), bone='spine_02', look=hoop)
    for z in (z0 - 0.012, z1 + 0.012):
        A.add(M.loft_z([(z - 0.014, 0, 0, rx * 0.86, ry * 0.86), (z + 0.014, 0, 0, rx * 0.86, ry * 0.86)], n=20), bone='spine_02', look='wood_z', smooth=30)
    wl = A._wood('wood_x', (1, 0, 0))
    for sx in (-1, 1):
        A.add(cyl((sx * rx * 0.7, 0.0, z1 + 0.014), (sx * (rx + 0.20), 0.0, z1 + 0.03), 0.022, n=7), bone='spine_02', look=wl)
        A.add(ellipsoid(0.03, 0.03, 0.03, n=7, m=5).move(sx * (rx + 0.20), 0.0, z1 + 0.03), bone='spine_02', look=wl)
    p0 = V(0, -ry * 0.8, z0 + H * 0.45)
    pts = M.polyline([p0, p0 + V(0, -0.14, -0.02), p0 + V(0, -0.24, 0.12), p0 + V(0, -0.25, 0.42), p0 + V(0, -0.33, 0.62)], 12)
    A.add(tube(pts, np.linspace(0.05, 0.03, 12), n=9, cap1=False), bone='spine_02', look=pipe, smooth=60)
    T = unit(pts[-1] - pts[-2])
    A.add(torus(0.036, 0.008, n=10, m=4).tf(M.rot_to((0, 0, 1), T)).move(pts[-1]), bone='spine_02', look=pipe)
    if glow:
        A.add(ellipsoid(0.027, 0.027, 0.008, n=9, m=4).tf(M.rot_to((0, 0, 1), T)).move(pts[-1] - T * 0.012), bone='spine_02', look=glow)
    A.add(revolve([(0.075, 0.0), (0.06, 0.03), (0.05, 0.035)], n=10).rot('x', 90).move(p0 + V(0, 0.02, 0)), bone='spine_02', look=pipe)


# ================================================================================================ ash / hush / shroud
@part
def body_hole(A, c=(0.0, 0.0, 1.31), r=0.085, rim='ember', lining='void', bone='spine_03'):
    """ragged hole clean through the torso (front to back): the body's faces inside it are removed, a dark tube lines
    the wound and a rim of sparks sits on both openings"""
    v = A.vis
    c = V(c)
    d = np.hypot(v.v[:, 0] - c[0], (v.v[:, 2] - c[2]) * 0.85)
    nz = np.array([M.fbm(p, 14.0, 2.0) for p in v.v])
    fm = v.vmask_faces(d < r * (1 + 0.25 * nz), 'any')
    q = v.sub(~fm)
    v.v, v.f, v.W = q.v, q.f, q.W
    ys = [p[1] for p, dd in zip(A.rest.v, np.hypot(A.rest.v[:, 0] - c[0], (A.rest.v[:, 2] - c[2]) * 0.85)) if dd < r * 1.3]
    y0, y1 = (min(ys) - 0.004, max(ys) + 0.004) if ys else (-0.14, 0.14)
    a = np.linspace(0, 2 * math.pi, 14, endpoint=False)
    rings = []
    for y in np.linspace(y0, y1, 4):
        rings.append(np.stack([c[0] + r * 1.12 * np.cos(a), np.full(14, y), c[2] + r * 1.12 / 0.85 * np.sin(a)], axis=1))
    t = M.loft(np.array(rings), cap0=False, cap1=False).flip()
    t.smooth = 180.0
    A.add(t, bone=bone, look=lining)
    if rim:
        for y in (y0 + 0.012, y1 - 0.012):
            pts = np.stack([c[0] + r * 1.08 * np.cos(a) * (1 + 0.08 * np.sin(5 * a)), np.full(14, y), c[2] + r * 1.08 / 0.85 * np.sin(a) * (1 + 0.08 * np.cos(4 * a))], axis=1)
            A.add(tube(pts, 0.006, n=4, closed=True), bone=bone, look=rim)


@part
def open_skull(A, z=1.715, lining='void', rim=None):
    """the top of the head broken away like an eggshell: jagged edge, dark hollow inside"""
    v = A.vis
    nz = np.array([M.fbm(p, 22.0, 5.0) for p in v.v])
    fm = v.vmask_faces((v.v[:, 2] > z + 0.018 * nz) & (v.wsum(['Head']) > 0.5), 'any')
    q = v.sub(~fm)
    v.v, v.f, v.W = q.v, q.f, q.W
    bowl = ellipsoid(0.078, 0.088, 0.075, n=12, m=7)
    bowl = bowl.sub(bowl.vmask_faces(bowl.v[:, 2] < 0.004, 'all')).flip()          # the lower half: a hollow, not a cap
    A.add(bowl.move(0, -0.02, z - 0.012), bone='Head', look=lining, smooth=180)


@part
def slot_head(A, look='chalk', H=0.40, W=0.165, D=0.15, slot=0.022, z=1.53):
    """a long vertical oval for a head, pierced by one clean vertical slot that can be seen through"""
    a = np.linspace(0, 2 * math.pi, 28, endpoint=False)
    ax = (W / 2 - slot / 2) / 2 + slot / 2
    pts = np.stack([ax * np.sin(a) * (1 + 0.0 * a), np.zeros(28), z + H / 2 - (H / 2 - 0.055) * np.cos(a)], axis=1)
    rr = (W / 2 - slot / 2) / 2 * (1.0 + 0.9 * np.abs(np.cos(a)) ** 3)
    t = tube(pts, D / 2, n=10, r2=rr, closed=True, up=(0, 1, 0))
    A.add(t.move(0, -0.015, 0), bone='Head', look=look, smooth=180)
    A.add(M.loft_z([(1.47, 0, 0.0, 0.05, 0.05), (z + 0.03, 0, -0.01, 0.036, 0.04)], n=10), bone='neck_01', look=look, smooth=180)


@part
def threads(A, look='silk', height=0.9, r=0.0035, points=(('clavicle_l', (0.21, 0.03, 1.50)), ('clavicle_r', (-0.21, 0.03, 1.50)), ('Head', (0.03, -0.03, 1.80)),
                                                         ('Head', (-0.03, 0.03, 1.80)))):
    """taut silk threads rising from shoulders and head into nothing (a marionette's strings)"""
    for bone, p in points:
        p = V(p)
        A.add(cyl(p - V(0, 0, 0.02), p + V(p[0] * 0.25, 0.02, height), r, n=4, r1=r * 0.5), bone=bone, look=look)


@part
def cocoon(A, look='silk', hem=0.0, seed=3):
    """tight silk winding: the torso and upper arms as one bowling-pin shell, the legs as a single bandaged block,
    a featureless oval for the head and torn webs between the arms and the flanks"""
    secs = [(hem, 0, 0.02, 0.17, 0.15), (0.25, 0, 0.03, 0.15, 0.125), (0.55, 0, 0.02, 0.165, 0.13), (0.90, 0, 0.02, 0.20, 0.155), (1.10, 0, 0.012, 0.215, 0.165),
            (1.30, 0, 0.012, 0.235, 0.175), (1.42, 0, 0.014, 0.225, 0.16), (1.50, 0, 0.012, 0.15, 0.125), (1.545, 0, 0.0, 0.085, 0.09)]
    g, R = KX.gown2(secs, n=30, mod=KX.pleat_mod(seed=seed, amp=0.035, hem_z=hem, top_z=1.45), rag=0.0, seed=seed)
    KX.crumple(g, 0.006, 14.0, float(seed))
    g.smooth = 180.0
    KC.skin_cloth(g, body=A.rest, smooth=6, pelvis_mix=0.8, mix_top=0.98, mix_bot=0.5, limit=['pelvis', 'spine_01', 'spine_02', 'spine_03', 'neck_01',
                                                                                              'thigh_l', 'thigh_r', 'calf_l', 'calf_r'])
    A.add(g, look=look)
    A.refs['robe'] = g; A.refs['robe_rings'] = R
    hd = ellipsoid(0.105, 0.115, 0.15, n=14, m=9)
    hd.rough(0.004, 20.0, seed=float(seed))
    A.add(hd.move(0, -0.012, 1.665), bone='Head', look=look, smooth=180)
    b = A.rest
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        m = b.wsum(['upperarm' + s]) > 0.5
        A.add(KF.shell(b, m, 0.012), look=look, smooth=180)
        top = np.array([(sx * x, 0.065, 1.40) for x in np.linspace(0.22, 0.47, 6)])
        wb = hang(top, np.linspace(0.34, 0.10, 6), rows=5, out=np.tile((0.0, 1.0, 0.0), (6, 1)), flare=0.0, rag=0.35, seed=seed + (sx > 0), holes=0.25, tatter=0.3)
        t = np.clip((1.40 - wb.v[:, 2]) / 0.3, 0, 1)
        M.weights_from(wb, {'upperarm' + s: 1 - 0.75 * t, 'spine_02': 0.75 * t})
        A.add(wb, look=look)
    for k, z in enumerate((0.35, 0.72, 1.02, 1.24)):                               # binding cords
        cx, cy, rx, ry = KI.girth(g, z)
        A.add(band((0, cy, z), rx + 0.004, ry + 0.004, 0.014, 0.007, tilt_y=6 * (-1) ** k, n=22), bone='pelvis' if z < 1.1 else 'spine_02', look='rope')
