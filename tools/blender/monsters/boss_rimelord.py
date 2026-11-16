"""boss_rimelord 霜骸領主・赫爾蒙 — Helmon the Rime-lord: an upright frozen warlord. A very broad shaggy fur mantle crusted
with ice over a bare ribcage in which a cold blue core burns (the focal motif, repeated in the eyes), a skull under a
crown-band whose icicles have grown into two antlers, a beard of icicles, rimed black plate on limbs and hips, a long
frozen cape, and a huge bearded war axe sheathed in hoar frost."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_a as KA
import kit_b as KB2
import kit_c as K
import kit_bones as KB
import kit_wisp as KW
import kit_ice as KI
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, band, unit, horn, hang, arc_pts


def war_axe(C, obj='wpn_axe'):
    P = []
    for p in K.polearm(shaft_back=0.58, shaft_front=1.22, r=0.024, n=8):
        P.append((p, 'haft', 40))
    P.append((tube([(0, -0.12, 0), (0, 0.0, 0), (0, 0.14, 0)], [0.029, 0.028, 0.029], n=8), 'strap', 50))
    for y in (-0.42, 0.34, 0.70):
        P.append((torus(0.027, 0.007, n=10, m=4).rot('x', 90).move(0, y, 0), 'iron2', 60))
    yc = 1.02
    P.append((revolve([(0.03, 0.0), (0.042, 0.02), (0.042, 0.36), (0.03, 0.40)], 10).rot('x', -90).move(0, yc - 0.20, 0), 'iron2', 40))
    bl = KX.fan_axe(R=0.47, r0=0.03, a0=-72, a1=40, th=0.05, segs=12, c=(0.0, yc, 0.0), flute=0.8, scallop=0.025)
    bl.dent((0.40, yc - 0.20, 0.0), 0.06, 0.035, direction=(-1, 0.3, 0))           # a bite out of the edge
    P.append((bl, 'steel', 24))
    bk = KX.fan_axe(R=0.20, r0=0.03, a0=-22, a1=22, th=0.045, segs=4, c=(0.0, yc, 0.0), flute=0.7, scallop=0.0)
    bk.scale((-1, 1, 1))
    P.append((bk, 'steel', 24))
    P.append((M.cone_spike(0.03, 0.24, n=6).rot('x', -90).move(0, yc + 0.20, 0), 'steel', 30))
    for sz in (-1, 1):
        P.append((K.rivets([(0.085, yc + dy, sz * 0.022) for dy in (-0.08, 0.0, 0.08)], [(0, 0, sz)] * 3, r=0.013), 'iron2', 60))
    # hoar frost: crystals grown on the head, icicles from the beard
    for k, (c, ax, cnt, hh) in enumerate((((-0.06, yc + 0.02, 0.0), (-1, 0.5, 0.2), 6, 0.22), ((0.10, yc + 0.10, 0.02), (0.2, 1, 0.5), 5, 0.16),
                                          ((0.12, yc - 0.10, -0.02), (0.1, -0.3, -1), 4, 0.12))):
        for cr in KI.cluster(c, axis=ax, count=cnt, spread=46, h=(0.06, hh), r=(0.016, 0.034), radius=0.035, seed=30 + k):
            P.append((cr, 'ice', 0))
    pts = [(0.47 * math.cos(math.radians(a)), yc + 0.47 * math.sin(math.radians(a)) + 0.01, 0.0) for a in (-70, -62, -54, -46, -38)]
    for ic in KI.fringe(pts, (0.15, -1, 0), [0.20, 0.13, 0.17, 0.09, 0.12], r=0.016, seed=3):
        P.append((ic, 'ice2', 30))
    for p, lk, sm in P:
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('boss_rimelord', 40000)
    R = M.RIG
    bp = dict(L.PRESET['black_plate'])
    C.look('iron', 'frost_metal', 'metal_rime', **dict(bp, amount=0.5, rough=0.5, bump=0.7, frost_amt=0.75, rime=0.6))
    C.look('iron2', 'frost_metal', 'metal_rime', **dict(bp, amount=0.7, scale=11.0, rough=0.55, bump=0.8, frost_amt=0.6, rime=0.45))
    C.look('steel', 'frost_metal', 'metal_rime', color=(0.16, 0.17, 0.19), amount=0.4, edge=1.0, edge_color=(0.55, 0.58, 0.62), rough=0.34,
           frost_amt=0.8, rime=0.75, rime_lo=0.45)
    C.look('bone', 'frost_bone', 'bone_frost', color=(0.52, 0.52, 0.47), stain=(0.16, 0.17, 0.19), frost_amt=0.7, rime=0.5)
    C.look('fur', 'fur', 'cloth_fur', color=(0.13, 0.115, 0.10), tip=(0.40, 0.39, 0.37), frost=0.95)
    C.look('fur2', 'fur', 'cloth_fur', color=(0.075, 0.07, 0.068), tip=(0.30, 0.31, 0.32), frost=0.8)
    C.look('cape', 'frost_cloth', 'cloth_fur', color=(0.045, 0.055, 0.075), dirt=(0.16, 0.19, 0.22), hem_lo=0.3, hem_hi=1.0, fold=8.0, frost_amt=0.7,
           rime=0.5, weave='wool')
    C.look('suit', 'frost_cloth', 'cloth_fur', color=(0.03, 0.033, 0.04), hem_lo=-2, hem_hi=-1, frost_amt=0.5, rime=0.4)
    C.look('strap', 'frost_leather', 'cloth_fur', color=(0.06, 0.045, 0.035), frost_amt=0.5, rime=0.3)
    C.look('haft', 'frost_wood', 'cloth_fur', color=(0.09, 0.07, 0.055), axis=(0.152, 0.987, 0.05), frost_amt=0.6, rime=0.7, rime_lo=0.45)
    C.look('ice', 'crystal', 'rock_ice', color=(0.20, 0.42, 0.68), deep=(0.018, 0.085, 0.25), scale=8.0, crack=14.0, frost_amt=0.65)
    C.look('ice2', 'crystal', 'rock_ice', color=(0.40, 0.66, 0.90), deep=(0.09, 0.28, 0.52), scale=14.0, crack=30.0, frost_amt=0.35, edge=0.45)
    C.look('core', 'emis', 'emis_core', emit=(0.30, 0.70, 1.0), strength=3.2, mottle=0.6, base_mul=0.7, scale=26.0)
    C.look('eye', 'emis', 'emis_core', emit=(0.45, 0.80, 1.0), strength=3.2, mottle=0.1, base_mul=0.7)
    S = 1.24                                                                         # limb girth factor of this giant

    def add(p, bone, look, smooth=None, mirror=False):
        return C.add(p, bone=bone, look=look, smooth=smooth, mirror=mirror)

    # ------------------------------------------------------------------ frozen limbs under the plate (base body, weights kept)
    su = K.under_suit(0.55)
    chest = (su.wsum(['spine_01', 'spine_02', 'spine_03', 'neck_01', 'clavicle_l', 'clavicle_r', 'Head']) > 0.5) & (su.v[:, 2] > 1.07)
    su = su.sub(~su.vmask_faces(chest, 'any'))
    sp = {}
    for s in ('l', 'r'):
        sp['upperarm_' + s] = dict(scale=(1.0, S, S)); sp['lowerarm_' + s] = dict(scale=(1.0, S, S))
        sp['thigh_' + s] = dict(scale=(S, S, 1.0)); sp['calf_' + s] = dict(scale=(S, S, 1.0))
    sp['pelvis'] = dict(scale=(1.15, 1.15, 1.0))
    M.pose_sculpt(su, sp)
    su = su.sub(su.vmask_faces(su.v[:, 2] > 0.09, 'any'))
    C.add(su, look='suit')
    C.add(K.gauntlet_hands(0.006, 0.75), look='iron')

    # ------------------------------------------------------------------ the bare ribcage and the core that burns in it
    KB.add_spine(C, 'bone', thick=1.55, zmin=1.03)
    KB.add_ribcage(C, 'bone', thick=1.9, wide=1.30, deep=1.28)
    KB.add_shoulder_girdle(C, 'bone', 1.5)
    cc = np.array([0.0, -0.005, 1.325])
    add(KX.lump(0.062, 0.055, 0.08, seed=2, amp=0.35, freq=22.0), 'spine_03', 'core', 80).move(cc)
    for cr in KI.cluster(cc + (0, 0, -0.02), axis=(0.1, -0.2, 1), count=9, spread=85, h=(0.06, 0.15), r=(0.016, 0.03), radius=0.03, seed=5):
        add(cr, 'spine_03', 'core', 0)
    for k in range(5):                                                               # rime veins from the core to the ribs
        a = 1.26 * k + 0.4
        e = cc + (0.15 * math.cos(a), -0.06 - 0.03 * (k % 2), 0.02 + 0.10 * math.sin(a))
        add(tube(M.polyline([cc, (cc + e) / 2 + (0, 0.02, 0.02), e], 5), [0.012, 0.009, 0.007, 0.005, 0.003], n=4), 'spine_03', 'core', 30)
    add(K.shell_arc([(1.19, 0, 0.02, 0.165, 0.15), (1.33, 0, 0.02, 0.19, 0.165), (1.46, 0, 0.02, 0.17, 0.145)], 25, 155, n=10, th=0.012), 'spine_03', 'iron2', 38)  # back plate

    # ------------------------------------------------------------------ hips: fauld lames, tassets, fur loincloths
    hsec = [(0.905, 0, 0.028, 0.236, 0.20), (0.96, 0, 0.028, 0.24, 0.203), (1.02, 0, 0.028, 0.226, 0.19), (1.07, 0, 0.028, 0.205, 0.172), (1.10, 0, 0.028, 0.17, 0.145)]
    sh, st, rv = KX.riveted_shell(hsec, n=22, seams=(-128, -52, 52, 128), hoops=(0.94, 1.03), per_seam=4, per_hoop=14, seam_w=0.02, hoop_w=0.03,
                                  rivet_r=0.009, mod=K.ridge_mod(0.07, 0.22), zpad=0.03)
    sh = sh.subdiv(1)
    sh.dent((0.2, -0.1, 0.98), 0.06, -0.012); sh.dent((-0.16, 0.13, 1.0), 0.07, -0.012)
    add(sh, 'pelvis', 'iron', 38); add(st, 'pelvis', 'iron2', 30); add(rv, 'pelvis', 'iron2', 60)

    add(band((0, 0.028, 1.085), 0.212, 0.18, 0.05, 0.014, n=24), 'spine_01', 'strap', 40)
    add(K.disc(0.062, 0.03, n=12, bevel=0.008).move(0, -0.165, 1.085), 'spine_01', 'iron2', 38)
    add(KI.crystal((0, -0.18, 1.085), (0, -1, 0.1), 0.07, 0.03, n=6), 'spine_01', 'ice2', 0)
    for (z0, z1, r0, r1, lk) in ((0.93, 0.78, 0.15, 0.142, 'iron'), (0.81, 0.64, 0.138, 0.124, 'iron2')):
        ts = K.shell_arc([(z1, 0.105, -0.004, r1, r1 * 1.06), ((z0 + z1) / 2, 0.105, -0.004, (r0 + r1) / 2 + 0.005, (r0 + r1) / 2 * 1.06 + 0.005),
                          (z0, 0.105, -0.004, r0, r0 * 1.06)], -130, 30, n=9, th=0.013)
        add(ts, 'thigh_l', lk, 38, mirror=True)
    for (a0, a1, ln, sd) in ((-118, -62, 0.50, 2), (60, 120, 0.58, 4)):
        top, nr = arc_pts((0, 0.028, 0.93), 0.245, 0.21, a0, a1, 8)
        fl = hang(top, ln, rows=7, out=nr, flare=0.05, rag=0.35, seed=sd, wave=0.02, tatter=0.3)
        M.weights_by(fl, fl.v[:, 2], [(0.5, {'thigh_l': 0.4, 'thigh_r': 0.4, 'pelvis': 0.2}), (0.92, {'pelvis': 1.0})])
        C.add(fl, look='fur2')

    # ------------------------------------------------------------------ legs and arms (left, mirrored): rimed plate
    add(K.limb_plate('thigh_l', [(0.14, 0.108 * S, 0.096 * S), (0.5, 0.10 * S, 0.09 * S), (0.88, 0.082 * S, 0.08 * S)], n=12, off=(0.004, -0.004, 0), lip=0.006),
        None, 'iron', 38, mirror=True)
    add(ellipsoid(0.08, 0.072, 0.092, n=10, m=6).move(0.094, -0.062, 0.535), 'calf_l', 'iron2', 40, mirror=True)
    add(KI.crystal((0.094, -0.125, 0.545), (0, -1, 0.35), 0.11, 0.03, n=5), 'calf_l', 'ice2', 0, mirror=True)
    add(K.limb_plate('calf_l', [(0.1, 0.08 * S, 0.074 * S), (0.3, 0.088 * S, 0.08 * S), (0.72, 0.066 * S, 0.062 * S), (0.96, 0.064 * S, 0.062 * S)], n=12,
                     off=(0.011, 0.01, 0), lip=0.006), None, 'iron', 38, mirror=True)
    sab = tube([(0.108, 0.10, 0.05), (0.108, 0.02, 0.066), (0.108, -0.06, 0.05), (0.108, -0.122, 0.04)], [0.05, 0.066, 0.05, 0.04], n=10,
               r2=[0.056, 0.064, 0.072, 0.072], up=(0, 0, 1), power=2.8)
    add(sab, 'foot_l', 'iron2', 38, mirror=True)
    toe = tube([(0.108, -0.112, 0.038), (0.108, -0.19, 0.034), (0.108, -0.255, 0.024)], [0.038, 0.034, 0.014], n=10, r2=[0.072, 0.056, 0.016], up=(0, 0, 1), power=2.6)
    add(toe, 'ball_l', 'iron', 38, mirror=True)
    for z in (0.20, 0.36):
        add(band((0.10, 0.02, z), 0.094, 0.10, 0.03, 0.01, n=14), 'calf_l', 'fur2', 180, mirror=True)
    add(K.limb_plate('lowerarm_l', [(0.1, 0.075 * S, 0.071 * S), (0.4, 0.073 * S, 0.067 * S), (0.76, 0.06 * S, 0.056 * S)], n=12, off=(0, 0.014, -0.008),
                     up=(0, 0, 1), lip=0.006), None, 'iron', 38, mirror=True)
    add(ellipsoid(0.076, 0.08, 0.08, n=10, m=6).move(0.468, 0.108, 1.432), 'lowerarm_l', 'iron2', 40, mirror=True)
    for cr in KI.cluster((0.49, 0.13, 1.45), axis=(0.2, 0.9, 0.4), count=4, spread=40, h=(0.06, 0.18), r=(0.016, 0.03), radius=0.03, seed=11):
        add(cr, 'lowerarm_l', 'ice', 0, mirror=True)
    add(K.limb_plate('upperarm_l', [(0.50, 0.086 * S, 0.084 * S), (0.72, 0.083 * S, 0.08 * S), (0.92, 0.076 * S, 0.074 * S)], n=12, off=(0, 0.012, -0.008),
                     up=(0, 0, 1), lip=0.006), None, 'iron2', 38, mirror=True)
    add(torus(0.082, 0.016, n=12, m=5, ry=0.086).rot('y', 90).move(0.70, 0.068, 1.435), 'lowerarm_l', 'fur2', 180, mirror=True)

    # ------------------------------------------------------------------ the mantle: shaggy pelt in three tiers of locks, crusted with ice
    mt, G = KW.poncho(X=0.56, Yf=0.235, Yb=0.31, n=56, rows=8, neck=(0.115, 0.125), neck_c=(0.0, 0.01),
                      ridge=((0.0, 1.62), (0.10, 1.605), (0.20, 1.59), (0.32, 1.585), (0.56, 1.555)),
                      yc=((0.0, 0.012), (0.10, 0.03), (0.20, 0.066), (0.56, 0.068)),
                      flat=((0.0, 0.10), (0.12, 0.12), (0.2, 0.09), (0.3, 0.07), (0.56, 0.06)),
                      depth_f=((0.0, 0.14), (0.1, 0.17), (0.2, 0.24), (0.3, 0.26), (0.5, 0.20)),
                      depth_b=((0.0, 0.50), (0.1, 0.46), (0.2, 0.34), (0.3, 0.26), (0.5, 0.20)), rag=0.14, seed=4)
    mt.rough(0.012, 16.0, seed=3)
    KC.skin_cloth(mt, smooth=3)
    C.add(mt, look='fur')
    Gm = mt.v.reshape(G.shape)
    rng = np.random.default_rng(3)
    for (g, i0, i1, lf, ov, sd) in ((Gm, 5, 8, 0.012, (0.05, 0.14), 1), (KX.half_shift(Gm), 2, 5, 0.026, (0.03, 0.09), 2), (Gm, 0, 2, 0.04, (0.03, 0.08), 3)):
        for bl in KX.thatch(g, i0=i0, i1=i1, width=0.075, fill=1.5, lift=lf, over=ov, seed=sd, vee=0.014, flare=0.03, taper=0.2):
            KB2.skin_like(bl, mt, smooth=0)
            C.add(bl, look='fur' if rng.uniform() < 0.6 else 'fur2', smooth=60)
    collar = torus(0.135, 0.05, n=18, m=6, ry=0.145).move(0.0, 0.01, 1.60)
    collar.rough(0.012, 30.0, seed=5)
    add(collar, 'spine_03', 'fur2', 180)
    hem = Gm[-1]
    for ic in KI.fringe(hem[::3] + (0, 0, 0.01), (0, 0, -1), 0.24, r=0.016, seed=6):
        C.add(KB2.skin_like(ic, mt, smooth=0), look='ice2', smooth=30)
    for sx, s in ((1, 'l'), (-1, 'r')):                                               # the crust on the shoulders
        c0 = np.array([sx * 0.33, 0.06, 1.60])
        add(K.ice_chunk(0.15, 0.13, 0.07, seed=20 + sx, n=18), 'clavicle_' + s, 'ice', 0).move(c0)
        add(K.ice_chunk(0.10, 0.09, 0.06, seed=24 + sx, n=14), 'upperarm_' + s, 'ice', 0).move(c0 + (sx * 0.15, 0.02, -0.03))
        for cr in KI.cluster(c0 + (0, 0.0, 0.03), axis=(sx * 0.5, 0.15, 1.0), count=8, spread=44, h=(0.10, 0.42), r=(0.024, 0.052), radius=0.07, seed=7 + sx, big=1.0):
            add(cr, 'clavicle_' + s, 'ice', 0)
        for cr in KI.cluster(c0 + (sx * 0.16, 0.03, -0.01), axis=(sx * 1.0, 0.2, 0.55), count=5, spread=40, h=(0.08, 0.26), r=(0.02, 0.04), radius=0.05, seed=12 + sx):
            add(cr, 'upperarm_' + s, 'ice2', 0)
    for cr in KI.cluster((0.04, 0.24, 1.56), axis=(0.1, 0.8, 0.7), count=6, spread=42, h=(0.08, 0.28), r=(0.02, 0.045), radius=0.06, seed=17):
        add(cr, 'spine_03', 'ice', 0)

    # ------------------------------------------------------------------ frozen cape
    cp = K.cape_pleated(w_top=0.62, w_bot=1.0, length=1.28, z_top=1.50, y=0.30, cols=17, rows=13, pleats=5.2, amp=0.05, flare=0.24, bow=0.09, rag=0.4,
                        seed=5, holes=0.12, slope=0.04)
    KC.cape_weights(cp, z_top=1.50, z_hip=1.0, z_knee=0.5, thigh=0.45)
    C.add(cp, look='cape')

    # ------------------------------------------------------------------ skull, crown-band, icicle antlers, beard of icicles
    KX.add_skull_seated(C, 'bone', 'eye', jaw_open=7.0, scale=1.22)
    hc = np.array([0.0, -0.015, 1.705])
    add(band(hc + (0, 0.0, 0.058), 0.108, 0.128, 0.042, 0.012, tilt_x=-8, n=22), 'Head', 'iron2', 38)
    for k in range(9):                                                               # crown points: short icicles growing upward
        a = math.radians(-150 + 37.5 * k) - math.pi / 2
        p = hc + (0.108 * math.cos(a), 0.128 * math.sin(a), 0.07 - 0.018 * math.sin(a))
        add(KI.crystal(p, (0.25 * math.cos(a), 0.25 * math.sin(a), 1.0), 0.07 + 0.05 * ((k * 5) % 3), 0.02, n=5), 'Head', 'ice2', 0)
    for sx in (1, -1):                                                               # the antlers
        beam = M.polyline([hc + (sx * 0.10, 0.02, 0.07), hc + (sx * 0.21, 0.05, 0.17), hc + (sx * 0.30, 0.03, 0.34), hc + (sx * 0.31, -0.04, 0.52),
                           hc + (sx * 0.25, -0.10, 0.66)], 9)
        bm = horn(beam, 0.036, n=5, r_tip=0.004, power=0.8)
        bm.smooth = 0.0
        add(bm, 'Head', 'ice', 0)
        for (i, d, ln, r) in ((2, (sx * 0.5, -0.5, 0.75), 0.20, 0.022), (4, (sx * 0.9, 0.1, 0.5), 0.24, 0.022), (5, (sx * 0.2, -0.75, 0.6), 0.22, 0.02),
                              (6, (sx * 0.7, 0.3, 0.7), 0.17, 0.017), (7, (-sx * 0.3, -0.5, 0.8), 0.13, 0.014)):
            add(KI.crystal(beam[i], d, ln, r, n=5, tip=0.3, sink=0.1), 'Head', 'ice2' if i % 2 else 'ice', 0)
    jaw = [hc + (0.075 * math.sin(a), -0.085 - 0.045 * math.cos(a), -0.15) for a in np.radians(np.linspace(-70, 70, 9))]
    for ic in KI.fringe(jaw, (0, -0.12, -1), [0.07, 0.12, 0.16, 0.22, 0.27, 0.20, 0.17, 0.11, 0.06], r=0.015, seed=9):
        add(ic, 'Head', 'ice2', 30)

    war_axe(C)
    return C, ("Upright frozen warlord: bare kit_bones ribcage / spine (frost_bone) with an emissive ice core and rime veins inside "
               "(emis_core), skull with an iron crown-band whose icicles grow into two faceted ice antlers, icicle beard, a very broad fur "
               "mantle built from three tiers of V-folded locks with icicle fringe and crystal crusts on the shoulders, rimed black plate "
               "on hips / limbs over a dark suit, pleated frozen cape; wpn_axe: bearded war axe with crystals and icicles.")
