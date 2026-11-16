"""mon_knight 墮落騎士 — black corroded full plate (rigid plates over a dark under-suit), torn cape,
horned + crested helm (one horn snapped), greatsword, smouldering ember seams."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_c as K
from monlib import V, tube, ellipsoid, box, join, blade, horn, band, revolve


def greatsword(C, obj='wpn_greatsword'):
    grip = tube([(0, -0.105, 0), (0, 0.0, 0), (0, 0.125, 0)], [0.0135, 0.0145, 0.0125], n=7)
    pommel = revolve([(0.0, -0.03), (0.022, -0.022), (0.03, 0.0), (0.02, 0.02), (0.012, 0.03)], 8).rot('x', -90).move(0, -0.125, 0)
    guard = box(0.33, 0.026, 0.03, bevel=0.006).move(0, 0.138, 0)
    guard.v[:, 1] -= 0.5 * np.abs(guard.v[:, 0]) ** 2 * 3.0
    for sx in (-1, 1):
        pass
    st = [(0.15, 0.036, 0.036, 0.012), (0.24, 0.034, 0.034, 0.0115), (0.26, 0.052, 0.052, 0.0115), (0.42, 0.05, 0.05, 0.011),
          (0.56, 0.048, 0.048, 0.0105), (0.575, 0.048, 0.03, 0.0105), (0.60, 0.047, 0.047, 0.0105),
          (0.83, 0.044, 0.044, 0.0098), (0.845, 0.028, 0.044, 0.0098), (0.87, 0.043, 0.043, 0.0096),
          (1.10, 0.038, 0.038, 0.0085), (1.22, 0.028, 0.03, 0.007), (1.31, 0.0, 0.004, 0.002)]
    bl = blade(st)
    seam = box(0.007, 0.52, 0.0128).move(0, 0.56, 0)
    for p, lk in ((grip, 'grip'), (pommel, 'plate'), (guard, 'plate'), (bl, 'blade'), (seam, 'ember')):
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=(22 if p is bl else 40))


def build():
    C = M.Creature('mon_knight', 15000)
    C.look('plate', 'metal', 'metal_plate', **dict(L.PRESET['black_plate'], amount=0.5, rough=0.5, bump=0.6))
    C.look('plate2', 'metal', 'metal_plate', **dict(L.PRESET['black_plate'], amount=0.68, scale=11.0, rough=0.55, bump=0.7))
    C.look('blade', 'metal', 'metal_plate', color=(0.085, 0.085, 0.10), rust=(0.10, 0.07, 0.05), rust2=(0.03, 0.025, 0.02), amount=0.38,
           edge=1.0, edge_color=(0.42, 0.42, 0.46), rough=0.33)
    C.look('suit', 'cloth', 'cloth_cape', color=(0.028, 0.028, 0.033), hem_lo=-2, hem_hi=-1, fray=0.1)
    C.look('cape', 'cloth', 'cloth_cape', color=(0.085, 0.075, 0.08), dirt=(0.20, 0.185, 0.17), hem_lo=0.4, hem_hi=1.0, fold=8.0)
    C.look('grip', 'leather', 'leather_strap', color=(0.045, 0.032, 0.026))
    C.look('ember', 'emis', 'emis_ember', emit=(1.0, 0.15, 0.02), strength=2.6, mottle=0.6)

    su = K.under_suit(0.36)
    su = su.sub(su.vmask_faces(su.v[:, 2] > 0.09, 'any'))      # feet live inside the sabatons
    C.add(su, look='suit')
    C.add(K.gauntlet_hands(0.004, 0.7), look='plate')

    # ------------------------------------------------------------------ torso
    cu = [(1.225, 0, 0.02, 0.176, 0.146), (1.27, 0, 0.02, 0.198, 0.162), (1.34, 0, 0.02, 0.216, 0.176), (1.41, 0, 0.018, 0.212, 0.168),
          (1.465, 0, 0.012, 0.165, 0.125), (1.497, 0, 0.008, 0.11, 0.10)]
    rm = K.ridge_mod(0.085, 0.26)
    C.add(K.torso_plate(cu, n=20, mod=rm), bone='spine_03', look='plate')
    C.add(band((0, 0.02, 1.214), 0.166, 0.138, 0.016, 0.008), bone='spine_02', look='ember')
    C.add(K.torso_plate([(1.203, 0, 0.02, 0.170, 0.142), (1.17, 0, 0.02, 0.182, 0.152), (1.135, 0, 0.022, 0.178, 0.149)], n=18, mod=K.ridge_mod(0.04, 0.3)),
          bone='spine_02', look='plate2')
    C.add(K.torso_plate([(1.145, 0, 0.022, 0.168, 0.141), (1.10, 0, 0.025, 0.181, 0.151), (1.062, 0, 0.025, 0.18, 0.15)], n=18, mod=K.ridge_mod(0.04, 0.3)),
          bone='spine_01', look='plate')
    C.add(K.torso_plate([(1.072, 0, 0.026, 0.18, 0.151), (1.0, 0, 0.03, 0.203, 0.172), (0.945, 0, 0.03, 0.216, 0.184)], n=18), bone='pelvis', look='plate2')
    C.add(band((0, 0.026, 1.062), 0.188, 0.158, 0.036, 0.012), bone='pelvis', look='grip')
    C.add(box(0.06, 0.016, 0.06, bevel=0.007).move(0, -0.136, 1.062), bone='pelvis', look='plate')
    # the Hushed mark smouldering on the breastplate + hairline cracks
    C.add(K.surf_strip(cu, [(1.405, -90), (1.33, -90), (1.265, -90)], w=0.0055, th=0.005, mod=rm), bone='spine_03', look='ember')
    C.add(K.surf_strip(cu, [(1.372, -107), (1.388, -96), (1.384, -84), (1.366, -73)], w=0.005, th=0.005, mod=rm), bone='spine_03', look='ember')
    C.add(K.surf_strip(cu, [(1.29, -38), (1.325, -47), (1.32, -58), (1.36, -64)], w=0.0035, th=0.004, mod=rm), bone='spine_03', look='ember')
    C.add(K.surf_strip(cu, [(1.27, 60), (1.31, 72), (1.30, 88), (1.36, 100), (1.41, 96)], w=0.004, th=0.004, mod=rm), bone='spine_03', look='ember')
    C.add(K.surf_strip(cu, [(1.30, 150), (1.33, 135), (1.37, 140), (1.40, 122)], w=0.0035, th=0.004, mod=rm), bone='spine_03', look='ember')
    # gorget
    C.add(K.torso_plate([(1.482, 0, 0.012, 0.128, 0.118), (1.53, 0, 0.004, 0.098, 0.102), (1.575, 0, -0.004, 0.09, 0.098)], n=14), bone='neck_01', look='plate2')
    C.add(band((0, 0.008, 1.49), 0.12, 0.11, 0.008, 0.005), bone='spine_03', look='ember')

    # ------------------------------------------------------------------ helm
    def hmod(th, z):
        d = np.arctan2(np.sin(th + math.pi / 2), np.cos(th + math.pi / 2))
        return 1.0 + 0.17 * np.exp(-(d / 0.42) ** 2) * math.exp(-((z - 1.64) / 0.065) ** 2)
    hs = [(1.548, 0, -0.012, 0.100, 0.118), (1.60, 0, -0.018, 0.108, 0.128), (1.663, 0, -0.02, 0.113, 0.134), (1.679, 0, -0.02, 0.113, 0.134),
          (1.74, 0, -0.016, 0.107, 0.127), (1.79, 0, -0.012, 0.084, 0.10), (1.823, 0, -0.01, 0.042, 0.052)]
    helm = K.torso_plate(hs, n=22, mod=hmod)
    fc = helm.face_centers()
    sm = (fc[:, 2] > 1.664) & (fc[:, 2] < 1.678) & (fc[:, 1] < -0.055)
    C.add(helm.sub(~sm), bone='Head', look='plate', smooth=38)
    C.add(helm.sub(sm).inflate(-0.003), bone='Head', look='ember')
    cr = M.prism([(-0.105, 1.745), (-0.075, 1.86), (0.02, 1.945), (0.13, 1.915), (0.185, 1.80), (0.11, 1.735), (0.0, 1.80)], -0.008, 0.008)
    cr.v = np.stack([cr.v[:, 2], cr.v[:, 0], cr.v[:, 1]], axis=1)
    cr.recalc(); cr.bevel(0.004, 1, 50)
    C.add(cr, bone='Head', look='plate2', smooth=25)
    hl = horn(M.polyline([(0.085, 0.0, 1.72), (0.165, 0.0, 1.745), (0.225, -0.025, 1.83), (0.215, -0.07, 1.945)], 8), 0.034, n=7, r_tip=0.003, power=0.9)
    C.add(hl, bone='Head', look='plate2', smooth=50)
    hr = tube(M.polyline([(-0.085, 0.0, 1.72), (-0.15, 0.0, 1.74), (-0.19, -0.012, 1.785)], 4), [0.034, 0.031, 0.028, 0.02], n=7)
    hr.v[-1] += np.array([-0.012, 0, 0.02])                 # jagged snapped end
    C.add(hr, bone='Head', look='plate2', smooth=30)
    for sx in (1, -1):
        C.add(M.torus(0.037, 0.008, n=10, m=4).rot('y', 90).move(sx * 0.098, 0.0, 1.722), bone='Head', look='plate')

    # ------------------------------------------------------------------ arms (left, mirrored)
    dome = revolve([(0.0, 0.092), (0.05, 0.084), (0.104, 0.058), (0.134, 0.02), (0.14, 0.0)], n=12).scale((1.0, 0.96, 1.0))
    C.add(dome.copy().rot('y', 16).move(0.216, 0.07, 1.468), bone='clavicle_l', look='plate', smooth=38, mirror=True)
    C.add(M.torus(0.128, 0.008, n=14, m=4, ry=0.122).rot('y', 16).move(0.216, 0.07, 1.47), bone='clavicle_l', look='plate2', mirror=True)
    rdg = tube(M.polyline([(0.10, 0.07, 1.50), (0.2, 0.07, 1.565), (0.30, 0.07, 1.535), (0.35, 0.07, 1.475)], 7), 0.011, n=4, r2=0.007, up=(0, 0, 1))
    C.add(rdg, bone='clavicle_l', look='plate2', smooth=30, mirror=True)
    fl = box(0.014, 0.16, 0.10, bevel=0.004).rot('y', -16).move(0.128, 0.06, 1.545)
    C.add(fl, bone='clavicle_l', look='plate2', smooth=30, mirror=True)
    C.add(dome.copy().scale((0.78, 0.84, 0.72)).rot('y', 48).move(0.300, 0.076, 1.446), bone='upperarm_l', look='plate2', smooth=38, mirror=True)
    C.add(dome.copy().scale((0.66, 0.78, 0.6)).rot('y', 66).move(0.342, 0.078, 1.425), bone='upperarm_l', look='plate', smooth=38, mirror=True)
    C.add(band((0.268, 0.076, 1.452), 0.004, 0.084, 0.085, 0.006, n=10).rot('y', 0), bone='upperarm_l', look='ember', mirror=True) if False else None
    C.add(K.limb_plate('upperarm_l', [(0.44, 0.088, 0.086), (0.66, 0.085, 0.083), (0.9, 0.078, 0.076)], n=10, off=(0, 0.012, -0.008), up=(0, 0, 1), lip=0.006),
          look='plate', mirror=True)
    C.add(K.limb_plate('lowerarm_l', [(-0.02, 0.061, 0.061), (0.045, 0.061, 0.061)], n=10, off=(0, 0.013, -0.008), up=(0, 0, 1), cap=False), look='ember', mirror=True)
    co = ellipsoid(0.066, 0.07, 0.07, n=10, m=6).move(0.468, 0.108, 1.432)
    C.add(co, bone='lowerarm_l', look='plate2', mirror=True)
    C.add(M.cone_spike(0.03, 0.085, n=6).rot('x', -90).move(0.468, 0.16, 1.432), bone='lowerarm_l', look='plate2', smooth=30, mirror=True)
    C.add(K.limb_plate('lowerarm_l', [(0.1, 0.075, 0.071), (0.4, 0.071, 0.065), (0.76, 0.057, 0.053)], n=10, off=(0, 0.014, -0.008), up=(0, 0, 1), lip=0.005),
          look='plate', mirror=True)
    C.add(K.limb_plate('lowerarm_l', [(0.74, 0.058, 0.054), (0.86, 0.066, 0.062), (0.985, 0.078, 0.072)], n=10, off=(0, 0.012, -0.006), up=(0, 0, 1)),
          look='plate2', mirror=True)
    C.add(ellipsoid(0.052, 0.046, 0.02, n=10, m=5).move(0.80, 0.07, 1.455), bone='hand_l', look='plate2', mirror=True)

    # ------------------------------------------------------------------ legs (left, mirrored)
    for (z0, z1, r0, r1, lk) in ((0.945, 0.82, 0.122, 0.116, 'plate'), (0.845, 0.70, 0.113, 0.102, 'plate2')):
        ts = K.shell_arc([(z0, 0.089, -0.004, r0, r0 * 1.06), ((z0 + z1) / 2, 0.089, -0.004, (r0 + r1) / 2 + 0.004, (r0 + r1) / 2 * 1.06 + 0.004),
                          (z1, 0.089, -0.004, r1, r1 * 1.06)], -135, 25, n=8, th=0.012)
        C.add(ts, bone='thigh_l', look=lk, mirror=True)
    C.add(K.limb_plate('thigh_l', [(0.10, 0.108, 0.094), (0.5, 0.10, 0.09), (0.88, 0.082, 0.08)], n=10, off=(0.004, -0.004, 0), lip=0.005), look='plate', mirror=True)
    C.add(K.limb_plate('calf_l', [(-0.06, 0.066, 0.066), (0.05, 0.064, 0.064)], n=10, off=(0.006, 0.002, 0), cap=False), look='ember', mirror=True)
    C.add(ellipsoid(0.066, 0.058, 0.076, n=10, m=6).move(0.094, -0.05, 0.535), bone='calf_l', look='plate2', mirror=True)
    C.add(K.disc(0.05, 0.02, n=10, bevel=0.005).rot('z', 90).move(0.168, 0.004, 0.535), bone='calf_l', look='plate2', mirror=True)
    C.add(K.limb_plate('calf_l', [(0.1, 0.08, 0.074), (0.3, 0.086, 0.078), (0.72, 0.064, 0.06), (0.96, 0.062, 0.06)], n=10, off=(0.011, 0.01, 0), lip=0.005),
          look='plate', mirror=True)
    sab = tube([(0.108, 0.088, 0.046), (0.108, 0.02, 0.06), (0.108, -0.06, 0.046), (0.108, -0.116, 0.037)], [0.046, 0.06, 0.046, 0.037], n=10,
               r2=[0.05, 0.058, 0.064, 0.064], up=(0, 0, 1), power=2.8)
    C.add(sab, bone='foot_l', look='plate2', smooth=38, mirror=True)
    toe = tube([(0.108, -0.108, 0.035), (0.108, -0.18, 0.031), (0.108, -0.24, 0.022)], [0.035, 0.031, 0.012], n=10, r2=[0.064, 0.05, 0.014], up=(0, 0, 1), power=2.6)
    C.add(toe, bone='ball_l', look='plate', smooth=38, mirror=True)

    # ------------------------------------------------------------------ cape
    cp = K.cape_pleated(w_top=0.50, w_bot=0.80, length=1.05, z_top=1.485, y=0.222, cols=15, rows=12, pleats=4.6, amp=0.04, flare=0.2,
                        bow=0.085, rag=0.42, seed=5, holes=0.14, slope=0.035)
    KC.cape_weights(cp, z_top=1.485, z_hip=1.0, z_knee=0.5, thigh=0.45)
    C.add(cp, look='cape')
    for sx in (1, -1):
        C.add(K.disc(0.034, 0.016, n=10, bevel=0.004).move(sx * 0.19, 0.15, 1.488), bone='spine_03', look='plate2')

    greatsword(C)
    return C, ("Rigid black-plate harness (each plate bound to one bone) over a decimated dark under-suit; skinned gauntlet hands; "
               "torn cape; helm with pointed visor, dorsal crest, one whole and one snapped horn; ember seams at visor, waist, neck, elbows, "
               "knees and the Hushed mark on the breastplate; wpn_greatsword (1.3 m blade) in the right hand.")
