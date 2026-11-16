"""mon_brass_sentinel 黃銅守衛 — hollow brass guardian shaped like the tomb-statue of a soldier: a barrel torso of riveted
plates with a round glass port where the storm-jar sits, a smooth domed head with a single vertical slot, tubular limbs
on ball joints, a long-hafted bronze fan-axe. Dome plus barrel, strictly symmetrical, every part rigid on one bone.
Violet-white arc light in the chest port, the head slot and the jar that rises behind the neck."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_c as K
import kit_ice as KI
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, band, unit


def fan_halberd(C, obj='wpn_halberd'):
    P = []
    for p in K.polearm(shaft_back=0.62, shaft_front=1.18, r=0.019, n=8):
        P.append((p, 'haft', 40))
    for y in (-0.36, 0.30, 0.62):
        P.append((torus(0.0215, 0.006, n=10, m=4).rot('x', 90).move(0, y, 0), 'dark', 60))
    P.append((tube([(0, -0.10, 0), (0, 0.0, 0), (0, 0.12, 0)], [0.0235, 0.0225, 0.0235], n=8), 'wrap', 50))
    yc = 1.02
    sock = revolve([(0.026, 0.0), (0.034, 0.02), (0.034, 0.30), (0.027, 0.33), (0.02, 0.36)], 10).rot('x', -90).move(0, yc - 0.18, 0)
    P.append((sock, 'brass2', 40))
    fan = KX.fan_axe(R=0.40, r0=0.03, a0=-58, a1=58, th=0.026, segs=12, c=(0.0, yc, 0.0), flute=0.4, scallop=0.06)
    P.append((fan, 'blade', 24))
    back = KX.fan_axe(R=0.16, r0=0.03, a0=-34, a1=34, th=0.024, segs=4, c=(0.0, yc, 0.0), flute=0.6, scallop=0.0)
    back.scale((-1, 1, 1))
    P.append((back, 'blade', 24))
    P.append((M.cone_spike(0.024, 0.26, n=6).rot('x', -90).move(0, yc + 0.17, 0), 'blade', 30))
    for sz in (-1, 1):
        P.append((K.rivets([(0.07, yc + dy, sz * 0.012) for dy in (-0.07, 0.0, 0.07)], [(0, 0, sz)] * 3, r=0.011), 'brass2', 60))
    for p, lk, sm in P:
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('mon_brass_sentinel', 15000)
    R = M.RIG
    C.look('brass', 'metal', 'metal_brass', **dict(L.PRESET['brass'], amount=0.40, bump=0.75))
    C.look('brass2', 'metal', 'metal_brass', **dict(L.PRESET['brass'], amount=0.60, scale=11.0, color=(0.33, 0.225, 0.07), bump=0.85))
    C.look('dark', 'metal', 'metal_brass', color=(0.085, 0.066, 0.04), rust=(0.05, 0.12, 0.09), rust2=(0.03, 0.05, 0.04), amount=0.5,
           edge_color=(0.5, 0.4, 0.2), rough=0.5)
    C.look('blade', 'metal', 'metal_brass', **dict(L.PRESET['brass'], amount=0.28, edge=1.0, rough=0.3, color=(0.50, 0.365, 0.14)))
    C.look('haft', 'wood', 'leather_haft', color=(0.06, 0.042, 0.03), axis=(0.152, 0.987, 0.05))
    C.look('wrap', 'leather', 'leather_haft', color=(0.075, 0.035, 0.03))
    C.look('arc', 'emis', 'emis_arc', emit=(0.70, 0.48, 1.0), strength=3.2, mottle=0.3, base_mul=0.7, scale=30.0)
    C.look('glass', 'emis', 'emis_arc', emit=(0.085, 0.04, 0.21), strength=3.2, mottle=1.0, scale=9.0, base_mul=0.6, rough=0.1)

    def add(p, bone, look, smooth=None, mirror=False):
        return C.add(p, bone=bone, look=look, smooth=smooth, mirror=mirror)

    # ------------------------------------------------------------------ hips: drum, apron plate, hip skirts
    psec = [(0.885, 0, 0.022, 0.175, 0.15), (0.92, 0, 0.022, 0.215, 0.182), (1.02, 0, 0.022, 0.222, 0.188), (1.07, 0, 0.02, 0.20, 0.168), (1.085, 0, 0.02, 0.15, 0.13)]
    sh, st, rv = KX.riveted_shell(psec, n=20, hoops=(1.035,), per_hoop=12, hoop_w=0.034, rivet_r=0.009)
    add(sh, 'pelvis', 'brass2', 38); add(st, 'pelvis', 'brass', 30); add(rv, 'pelvis', 'brass', 60)
    apron = K.shell_arc([(0.70, 0, 0.022, 0.13, 0.14), (0.82, 0, 0.022, 0.19, 0.17), (0.93, 0, 0.022, 0.222, 0.19)], -118, -62, n=6, th=0.012)
    apron.v[:, 0] *= np.interp(apron.v[:, 2], [0.70, 0.93], [0.55, 1.0])
    add(apron, 'pelvis', 'brass', 38)
    add(K.rivets([(sx * 0.04, -0.155, 0.80) for sx in (-1, 1)] + [(0, -0.14, 0.735)], [(0, -1, 0)] * 3, r=0.01), 'pelvis', 'brass2', 60)
    for (z0, z1, r0, r1, lk) in ((0.94, 0.80, 0.128, 0.122, 'brass'), (0.83, 0.67, 0.119, 0.108, 'brass2')):
        ts = K.shell_arc([(z1, 0.089, -0.002, r1, r1 * 1.05), ((z0 + z1) / 2, 0.089, -0.002, (r0 + r1) / 2 + 0.004, (r0 + r1) / 2 * 1.05 + 0.004),
                          (z0, 0.089, -0.002, r0, r0 * 1.05)], -100, 100, n=9, th=0.012)
        add(ts, 'thigh_l', lk, 38, mirror=True)
        rp, rn = K.ring_pts((0.089, -0.002, z1 + 0.03), r1 + 0.008, r1 * 1.05 + 0.008, 5, -80, 80)
        add(K.rivets(rp, rn, r=0.008), 'thigh_l', 'dark', 60, mirror=True)

    # ------------------------------------------------------------------ waist bellows + the barrel
    for k, z in enumerate((1.085, 1.118, 1.151)):
        add(torus(0.172 + 0.008 * k, 0.021, n=18, m=5, ry=0.146 + 0.007 * k).move(0, 0.02, z), 'spine_01', 'dark', 60)
    for k, z in enumerate((1.184, 1.217)):
        add(torus(0.185, 0.021, n=18, m=5, ry=0.158).move(0, 0.02, z), 'spine_02', 'dark', 60)
    bsec = [(1.165, 0, 0.02, 0.19, 0.162), (1.185, 0, 0.02, 0.238, 0.203), (1.27, 0, 0.02, 0.268, 0.228), (1.36, 0, 0.02, 0.272, 0.23),
            (1.44, 0, 0.02, 0.252, 0.212), (1.49, 0, 0.018, 0.21, 0.178), (1.515, 0, 0.012, 0.135, 0.125)]
    sh, st, rv = KX.riveted_shell(bsec, n=24, seams=(-142, -38, 38, 142, 90), hoops=(1.215, 1.455), per_seam=5, per_hoop=16, seam_w=0.022,
                                  hoop_w=0.034, rivet_r=0.0095, zpad=0.05)
    sh = sh.subdiv(1)
    for (c, r, a) in (((0.27, 0.02, 1.32), 0.06, -0.013), ((-0.27, 0.03, 1.38), 0.05, -0.011), ((-0.11, 0.23, 1.30), 0.06, -0.013),
                      ((0.12, 0.225, 1.38), 0.05, -0.011), ((0.2, -0.13, 1.25), 0.04, -0.008)):
        sh.dent(c, r, a)
    add(sh, 'spine_03', 'brass', 38); add(st, 'spine_03', 'brass2', 30); add(rv, 'spine_03', 'brass', 60)
    # the storm-jar port: bolted ring, bulging glass, arcs crawling inside
    pp, pn = K.loft_point(bsec, 1.335, -90)
    add(torus(0.094, 0.019, n=18, m=6).rot('x', 90).move(pp + (0, 0.002, 0)), 'spine_03', 'brass2', 50)
    add(ellipsoid(0.08, 0.034, 0.08, n=14, m=7).move(pp + (0, 0.012, 0)), 'spine_03', 'glass', 80)
    bp, bn = [], []
    for k in range(8):
        a = math.radians(22.5 + 45 * k)
        bp.append(pp + (0.094 * math.cos(a), -0.018, 0.094 * math.sin(a))); bn.append((0, -1, 0))
    add(K.rivets(bp, bn, r=0.009), 'spine_03', 'brass', 60)
    rng = np.random.default_rng(4)
    for k in range(3):
        a0 = rng.uniform(0, 6.28)
        pts = []
        for t in np.linspace(-1, 1, 6):
            q = np.array([math.cos(a0), 0, math.sin(a0)]) * 0.062 * t + np.array([-math.sin(a0), 0, math.cos(a0)]) * rng.uniform(-0.018, 0.018)
            pts.append(pp + q + (0, 0.012 - 0.034 * math.sqrt(max(0.0, 1 - (np.linalg.norm(q) / 0.08) ** 2)) - 0.002, 0))
        add(tube(pts, 0.0035, n=3), 'spine_03', 'arc', 30)

    # ------------------------------------------------------------------ the jar behind the neck (the same storm, seen from above)
    jy = 0.262
    add(K.cyl((0, jy, 1.27), (0, jy, 1.60), 0.05, n=10), 'spine_03', 'glass', 60)
    for k in range(2):                                                               # the arc that never stops climbing the jar
        pts = [(0.052 * math.cos(2.2 * t + 3 * k), jy + 0.052 * math.sin(2.2 * t + 3 * k), 1.29 + 0.29 * t / 6 + 0.012 * ((t * 7 + k) % 3 - 1)) for t in range(7)]
        add(tube(pts, 0.0045, n=3), 'spine_03', 'arc', 30)
    for z, r in ((1.27, 0.07), (1.44, 0.064), (1.60, 0.07)):
        add(K.disc(r, 0.03, n=10, bevel=0.006).rot('x', 90).move(0, jy, z), 'spine_03', 'brass2', 40)
    add(revolve([(0.062, 0.0), (0.05, 0.03), (0.022, 0.05), (0.0, 0.055)], 10).move(0, jy, 1.615), 'spine_03', 'brass', 40)
    for a in (20, 55, 90, 125, 160):
        ar = math.radians(a)
        add(K.cyl((0.064 * math.cos(ar), jy + 0.064 * math.sin(ar), 1.27), (0.064 * math.cos(ar), jy + 0.064 * math.sin(ar), 1.60), 0.0075, n=5),
            'spine_03', 'dark', 50)
    for sx in (-1, 1):
        add(tube(M.polyline([(sx * 0.05, jy, 1.30), (sx * 0.12, jy + 0.02, 1.24), (sx * 0.14, jy - 0.03, 1.20)], 5), 0.014, n=6), 'spine_03', 'dark', 60)

    # ------------------------------------------------------------------ collar + domed head with the slot
    add(K.torso_plate([(1.495, 0, 0.004, 0.125, 0.118), (1.53, 0, 0.0, 0.104, 0.104), (1.572, 0, -0.006, 0.098, 0.104)], n=16), 'neck_01', 'dark', 38)
    hsec = [(1.556, 0, -0.012, 0.100, 0.110), (1.585, 0, -0.012, 0.121, 0.133), (1.66, 0, -0.012, 0.127, 0.14), (1.73, 0, -0.012, 0.118, 0.13),
            (1.785, 0, -0.012, 0.094, 0.104), (1.822, 0, -0.012, 0.055, 0.061), (1.838, 0, -0.012, 0.014, 0.016)]
    sh, st, rv = KX.riveted_shell(hsec, n=22, hoops=(1.60,), per_hoop=14, hoop_w=0.03, rivet_r=0.008, th=0.007)
    sh.dent((0.09, -0.07, 1.76), 0.05, -0.007)
    sh.dent((-0.07, 0.09, 1.72), 0.055, -0.008)
    add(sh, 'Head', 'brass', 40); add(st, 'Head', 'brass2', 30); add(rv, 'Head', 'brass', 60)
    add(K.surf_strip(hsec, [(1.625, -90), (1.66, -90), (1.70, -90), (1.74, -90), (1.772, -90)], w=0.013, th=0.007, out=0.0015), 'Head', 'arc', 20)
    for da in (-7.5, 7.5):
        add(K.surf_strip(hsec, [(1.612, -90 + da), (1.66, -90 + da), (1.72, -90 + da), (1.785, -90 + da * 0.9)], w=0.009, th=0.013, out=0.002),
            'Head', 'dark', 20)
    add(revolve([(0.03, 0.0), (0.034, 0.012), (0.02, 0.03), (0.012, 0.05), (0.0, 0.056)], 8).move(0, -0.012, 1.832), 'Head', 'brass2', 40)
    for sx in (-1, 1):
        add(K.disc(0.036, 0.022, n=10, bevel=0.006).rot('z', 90).move(sx * 0.125, -0.012, 1.665), 'Head', 'brass2', 40)

    # ------------------------------------------------------------------ arms (left, mirrored): ball joints, tubes, bell cuffs, block hands
    Sx = np.array([0.245, 0.066, 1.452])
    add(KX.ball(Sx, 0.092), 'upperarm_l', 'dark', 60, mirror=True)
    lm = KX.lame(0.142, 0.085, n=12)
    add(lm.copy().rot('y', 20).move(0.255, 0.068, 1.505), 'clavicle_l', 'brass', 40, mirror=True)
    add(lm.copy().scale((0.82, 0.86, 0.72)).rot('y', 52).move(0.332, 0.07, 1.468), 'upperarm_l', 'brass2', 40, mirror=True)
    rp = [(0.255 + 0.118 * math.cos(math.radians(a)) * math.cos(math.radians(20)), 0.068 + 0.112 * math.sin(math.radians(a)),
           1.522 - 0.118 * math.cos(math.radians(a)) * math.sin(math.radians(20))) for a in (-140, -90, -40, 40, 90, 140)]
    add(K.rivets(rp, [(0.2, 0, 1)] * 6, r=0.0085), 'clavicle_l', 'brass2', 60, mirror=True)
    add(K.limb_plate('upperarm_l', [(0.38, 0.062, 0.062), (0.62, 0.064, 0.064), (0.90, 0.058, 0.058)], n=12, up=(0, 0, 1), lip=0.007), None, 'brass', 38, mirror=True)
    add(KI.limb_ring('upperarm_l', 0.63, 0.066, 0.008, n=12, m=4), None, 'brass2', 60, mirror=True)
    add(KX.ball(R.H('lowerarm_l'), 0.066), 'lowerarm_l', 'dark', 60, mirror=True)
    add(K.disc(0.04, 0.15, n=8, bevel=0.008).move(R.H('lowerarm_l')), 'lowerarm_l', 'brass2', 40, mirror=True)        # elbow axle
    add(K.limb_plate('lowerarm_l', [(0.16, 0.055, 0.055), (0.45, 0.062, 0.062), (0.80, 0.068, 0.068), (0.93, 0.08, 0.08)], n=12, up=(0, 0, 1), lip=0.007),
        None, 'brass', 38, mirror=True)
    add(KI.limb_ring('lowerarm_l', 0.50, 0.066, 0.008, n=12, m=4), None, 'brass2', 60, mirror=True)
    add(KX.ball(R.H('hand_l') + (0.012, 0, 0), 0.047), 'hand_l', 'dark', 60, mirror=True)
    palm = box(0.105, 0.098, 0.036, bevel=0.008).move(0.812, 0.068, 1.438)
    add(palm, 'hand_l', 'brass2', 35, mirror=True)
    add(K.rivets([(0.80, 0.045, 1.458), (0.80, 0.092, 1.458)], [(0, 0, 1)] * 2, r=0.008), 'hand_l', 'brass', 60, mirror=True)
    K.finger_blocks(C, 'brass', w=0.021, h=0.02)

    # ------------------------------------------------------------------ legs (left, mirrored)
    add(KX.ball(R.H('thigh_l') + (0.012, 0, 0), 0.088), 'thigh_l', 'dark', 60, mirror=True)
    add(K.limb_plate('thigh_l', [(0.22, 0.09, 0.086), (0.55, 0.085, 0.082), (0.88, 0.07, 0.07)], n=12, lip=0.007), None, 'brass', 38, mirror=True)
    add(KI.limb_ring('thigh_l', 0.56, 0.088, 0.009, n=12, m=4), None, 'brass2', 60, mirror=True)
    add(KX.ball(R.H('calf_l'), 0.074), 'calf_l', 'dark', 60, mirror=True)
    add(K.disc(0.05, 0.17, n=10, bevel=0.008).rot('z', 90).move(R.H('calf_l')), 'calf_l', 'brass2', 40, mirror=True)        # knee axle
    add(KX.lame(0.07, 0.04, n=10).rot('x', 90).move(R.H('calf_l') + (0, -0.064, 0.0)), 'calf_l', 'brass', 40, mirror=True)  # knee cap
    add(K.limb_plate('calf_l', [(0.14, 0.064, 0.064), (0.42, 0.078, 0.076), (0.80, 0.07, 0.07), (0.93, 0.092, 0.092)], n=12, off=(0, 0.004, 0), lip=0.007),
        None, 'brass', 38, mirror=True)
    add(KI.limb_ring('calf_l', 0.45, 0.08, 0.009, n=12, m=4, off=(0, 0.004, 0)), None, 'brass2', 60, mirror=True)
    add(KX.ball(R.H('foot_l') + (0, 0.0, 0.0), 0.052), 'foot_l', 'dark', 60, mirror=True)
    sab = tube([(0.089, 0.095, 0.05), (0.089, 0.03, 0.062), (0.089, -0.05, 0.05), (0.089, -0.112, 0.04)], [0.05, 0.062, 0.05, 0.04], n=10,
               r2=[0.054, 0.062, 0.066, 0.066], up=(0, 0, 1), power=3.0)
    add(sab, 'foot_l', 'brass2', 38, mirror=True)
    toe = tube([(0.089, -0.105, 0.038), (0.089, -0.18, 0.034), (0.089, -0.235, 0.026)], [0.038, 0.034, 0.024], n=10, r2=[0.066, 0.058, 0.04], up=(0, 0, 1), power=3.0)
    add(toe, 'ball_l', 'brass', 38, mirror=True)
    add(K.rivets([(0.089 + dx, -0.15, 0.07) for dx in (-0.03, 0.03)], [(0, 0, 1)] * 2, r=0.008), 'ball_l', 'brass2', 60, mirror=True)

    fan_halberd(C)
    return C, ("Brass construct, every part rigid on one bone: riveted barrel (strap seams + hoops) on spine_03 with a bolted glass port "
               "(emis_arc) and a storm-jar rising behind the neck, waist bellows, hip drum with apron and hip skirts, smooth domed head with "
               "one vertical lit slot, tubular limbs between dark ball joints with axle discs and bell cuffs, block fingers; wpn_halberd: "
               "long haft with a fluted bronze fan blade, back fluke and top spike.")
