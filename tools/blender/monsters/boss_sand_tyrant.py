"""boss_sand_tyrant 鏽砂暴君・伊薩克 — Isak the Rust-sand Tyrant: a colossal corroded brass king-automaton. A cracked mask-face
under a tall crown of blades, a cuirass torn open at the front over turning gearwork lit violet from behind (the focal
motif), stacked pauldrons, a mechanical waist of pistons, a skirt of brass lames, three tattered royal banners hung from
a yoke on his back as a cape, sand lying in every hollow, and an enormous glaive. Every hard part is rigid on one bone."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_b as KB2
import kit_c as K
import kit_ice as KI
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, band, unit, blade, hang, horn


def glaive(C, obj='wpn_glaive'):
    P = []
    for p in K.polearm(shaft_back=0.64, shaft_front=1.10, r=0.023, n=8):
        P.append((p, 'haft', 40))
    P.append((tube([(0, -0.12, 0), (0, 0.0, 0), (0, 0.14, 0)], [0.028, 0.027, 0.028], n=8), 'banner', 50))
    for y in (-0.40, 0.36, 0.72):
        P.append((torus(0.026, 0.007, n=10, m=4).rot('x', 90).move(0, y, 0), 'dark', 60))
    P.append((revolve([(0.028, 0.0), (0.04, 0.02), (0.038, 0.20), (0.05, 0.23), (0.03, 0.27)], 10).rot('x', -90).move(0, 0.86, 0), 'brass2', 40))
    bl = blade([(1.04, 0.03, 0.03, 0.024), (1.10, 0.04, 0.10, 0.024), (1.32, 0.045, 0.15, 0.021), (1.60, 0.045, 0.165, 0.018), (1.82, 0.035, 0.12, 0.013),
                (1.97, 0.0, 0.012, 0.004)])
    bl.bend('y', 'z', 16.0, origin=1.04)
    bl.dent((0.14, 1.50, 0), 0.05, 0.03, direction=(-1, 0, 0))                      # notched edge
    P.append((bl, 'blade', 22))
    P.append((horn([(-0.03, 1.22, 0), (-0.11, 1.26, 0), (-0.15, 1.36, 0)], 0.022, n=5, r2=0.008), 'blade', 30))       # back hook
    for y in (1.30, 1.44, 1.58):
        P.append((torus(0.024, 0.0045, n=10, m=4).rot('x', 90).move(-0.062 - 0.045 * (y - 1.04), y, 0), 'brass2', 60))
    for sz in (-1, 1):
        P.append((K.rivets([(0.02, 1.08 + 0.05 * k, sz * 0.012) for k in range(3)], [(0, 0, sz)] * 3, r=0.011), 'brass2', 60))
        G = np.array([[(-0.028 + 0.004 * i * sz, 1.00 - (0.06 + 0.012 * sz) * i, sz * (0.029 + 0.005 * i)), (0.028 - 0.006 * i, 1.00 - (0.06 + 0.012 * sz) * i, sz * (0.029 + 0.005 * i))]
                      for i in range(6)])
        st = M.sheet(G)
        P.append((st, 'banner', 180))
    for p, lk, sm in P:
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('boss_sand_tyrant', 40000)
    R = M.RIG
    br = L.PRESET['brass']
    C.look('brass', 'metal', 'metal_brass', **dict(br, amount=0.55, bump=0.85))
    C.look('brass2', 'metal', 'metal_brass', **dict(br, amount=0.74, scale=11.0, color=(0.32, 0.215, 0.07), bump=0.95))
    C.look('dark', 'metal', 'metal_brass', color=(0.08, 0.062, 0.04), rust=(0.05, 0.12, 0.09), rust2=(0.03, 0.05, 0.04), amount=0.55, edge_color=(0.5, 0.4, 0.2), rough=0.5)
    C.look('blade', 'metal', 'metal_brass', **dict(br, amount=0.35, edge=1.0, rough=0.3, color=(0.48, 0.35, 0.13)))
    C.look('banner', 'cloth', 'cloth_banner', color=(0.20, 0.035, 0.05), weave='wool_weave', dirt=(0.30, 0.22, 0.12), hem_lo=0.2, hem_hi=1.1, hem_amt=0.9, fold=7.0,
           fold_amt=0.9, stain_amt=0.6, streak=0.5, fray=0.7, dust=0.7)
    C.look('banner2', 'cloth', 'cloth_banner', color=(0.10, 0.045, 0.13), weave='wool_weave', dirt=(0.30, 0.22, 0.12), hem_lo=0.2, hem_hi=1.1, hem_amt=0.9, fold=7.0,
           fold_amt=0.9, stain_amt=0.6, fray=0.7, dust=0.7)
    C.look('orph', 'metal', 'cloth_banner', **dict(L.PRESET['gold_tarnish'], amount=0.6, bump=1.0))
    C.look('haft', 'wood', 'cloth_banner', color=(0.06, 0.042, 0.03), axis=(0.152, 0.987, 0.05))
    C.look('sand', 'sand', 'rock_sand', color=(0.46, 0.36, 0.21))
    C.look('arc', 'emis', 'emis_arc', emit=(0.70, 0.46, 1.0), strength=3.2, mottle=0.3, base_mul=0.7, scale=30.0)
    C.look('core', 'emis', 'emis_arc', emit=(0.34, 0.16, 0.70), strength=3.2, mottle=0.9, scale=12.0, base_mul=0.6, rough=0.3)

    def add(p, bone, look, smooth=None, mirror=False):
        return C.add(p, bone=bone, look=look, smooth=smooth, mirror=mirror)

    def sand(c, r, bone, sd):
        e = KX.lump(r[0], r[1], r[2], seed=sd, amp=0.25, freq=20.0, n=9, m=5).move(c)
        e.v[:, 2] = np.maximum(e.v[:, 2], c[2] - r[2] * 0.25)
        return add(e, bone, 'sand', 180)

    # ------------------------------------------------------------------ hips: drum, royal tabard plate with the sun-disc, skirt of lames
    psec = [(0.88, 0, 0.022, 0.19, 0.16), (0.92, 0, 0.022, 0.235, 0.20), (1.02, 0, 0.022, 0.24, 0.205), (1.07, 0, 0.02, 0.21, 0.18), (1.09, 0, 0.02, 0.15, 0.13)]
    sh, st, rv = KX.riveted_shell(psec, n=22, seams=(-140, -40, 40, 140), hoops=(1.035,), per_seam=3, per_hoop=14, hoop_w=0.036, rivet_r=0.01, zpad=0.03)
    add(sh, 'pelvis', 'brass2', 38); add(st, 'pelvis', 'brass', 30); add(rv, 'pelvis', 'brass', 60)
    tab = K.shell_arc([(0.46, 0, 0.022, 0.10, 0.19), (0.70, 0, 0.022, 0.19, 0.225), (0.93, 0, 0.022, 0.24, 0.21)], -112, -68, n=6, th=0.014)
    tab.v[:, 0] *= np.interp(tab.v[:, 2], [0.46, 0.60, 0.93], [0.15, 1.0, 1.0])
    add(tab, 'pelvis', 'brass', 38)
    add(K.disc(0.062, 0.02, n=14, bevel=0.006).move(0, -0.238, 0.76), 'pelvis', 'brass2', 38)
    for k in range(8):
        a = math.radians(45 * k)
        add(box(0.016, 0.012, 0.05, bevel=0.003).rot('y', math.degrees(a)).move(0.092 * math.sin(a), -0.236, 0.76 + 0.092 * math.cos(a)), 'pelvis', 'brass2', 30)
    for k, (a0, a1, lk) in enumerate(((-60, 20, 'brass'), (15, 85, 'brass2'))):       # hip lames (left, mirrored) + back lames
        for (z0, z1, dr) in ((0.95, 0.74, 0.0), (0.80, 0.56, 0.012)):
            ts = K.shell_arc([(z1, 0.02, 0.022, 0.26 + dr, 0.225 + dr), ((z0 + z1) / 2, 0.02, 0.022, 0.262 + dr, 0.227 + dr), (z0, 0.02, 0.022, 0.25 + dr, 0.214 + dr)],
                             a0, a1, n=7, th=0.013)
            add(ts, 'thigh_l', lk if dr == 0 else ('brass2' if lk == 'brass' else 'brass'), 38, mirror=True)
    sand((0.0, 0.02, 1.085), (0.17, 0.14, 0.03), 'pelvis', 1)

    # ------------------------------------------------------------------ mechanical waist: column, pistons, bellows ring
    add(K.cyl((0, 0.03, 1.07), (0, 0.03, 1.24), 0.075, n=10), 'spine_01', 'dark', 40)
    for k in range(6):
        a = math.radians(60 * k + 30)
        p0 = np.array([0.13 * math.cos(a), 0.03 + 0.11 * math.sin(a), 1.075])
        add(K.cyl(p0, p0 + (0, 0, 0.09), 0.02, n=7), 'spine_01', 'brass2', 40)
        add(K.cyl(p0 + (0, 0, 0.08), p0 + (0, 0, 0.19), 0.011, n=6), 'spine_02', 'blade', 40)
    add(torus(0.165, 0.022, n=18, m=5, ry=0.14).move(0, 0.03, 1.215), 'spine_02', 'dark', 60)

    # ------------------------------------------------------------------ the cuirass, torn open over the gearwork
    csec = [(1.20, 0, 0.02, 0.21, 0.18), (1.24, 0, 0.02, 0.275, 0.228), (1.33, 0, 0.02, 0.305, 0.248), (1.42, 0, 0.02, 0.30, 0.24), (1.49, 0, 0.02, 0.255, 0.205),
            (1.525, 0, 0.016, 0.17, 0.15)]
    cu = K.shell_arc(csec, -52, 232, n=26, th=0.018).subdiv(1)
    rng = np.random.default_rng(3)
    ang = np.degrees(np.arctan2(cu.v[:, 1] - 0.02, cu.v[:, 0]))
    edge = (np.abs(((ang + 52 + 180) % 360) - 180) < 9) | (np.abs(((ang - 232 + 180) % 360) - 180) < 9)
    cu.v[edge, 0] += rng.uniform(-0.03, 0.02, int(edge.sum())) * np.sign(cu.v[edge, 0])   # the torn, bent edges of the breach
    cu.v[edge, 1] -= rng.uniform(0.0, 0.03, int(edge.sum()))
    for (c, r, a) in (((0.29, 0.05, 1.34), 0.07, -0.016), ((-0.27, 0.12, 1.40), 0.06, -0.014), ((0.05, 0.26, 1.33), 0.08, -0.016)):
        cu.dent(c, r, a)
    add(cu, 'spine_03', 'brass', 38)
    for z in (1.235, 1.50):
        add(K.surf_strip(csec, [(z, a) for a in np.linspace(-180, 180, 29)], w=0.04, th=0.012), 'spine_03', 'brass2', 30)
        pts = [K.loft_point(csec, z, a, None, out=0.011) for a in np.linspace(-180, 180, 18, endpoint=False)]
        add(K.rivets([p for p, _ in pts], [q for _, q in pts], r=0.011), 'spine_03', 'brass', 60)
    for a in (140, 90, 40):
        add(K.surf_strip(csec, [(z, a) for z in np.linspace(1.25, 1.49, 6)], w=0.024, th=0.009), 'spine_03', 'brass2', 30)
    add(K.torso_plate([(1.22, 0, 0.05, 0.20, 0.15), (1.36, 0, 0.05, 0.235, 0.165), (1.50, 0, 0.05, 0.19, 0.14)], n=16), 'spine_03', 'dark', 38)   # inner wall
    add(ellipsoid(0.15, 0.03, 0.13, n=14, m=6).move(0.0, -0.105, 1.36), 'spine_03', 'core', 80)                                               # the light behind
    for (x, y, z, r, teeth, lk) in ((0.035, -0.155, 1.375, 0.112, 14, 'brass2'), (-0.105, -0.17, 1.285, 0.072, 10, 'dark'), (0.125, -0.175, 1.262, 0.054, 8, 'blade'),
                                    (-0.085, -0.15, 1.455, 0.06, 9, 'dark'), (0.15, -0.16, 1.44, 0.045, 7, 'brass2')):
        add(K.gear(r=r, teeth=teeth, th=0.02, depth=0.2, hub=0.3).rot('y', 13 * teeth).move(x, y, z), 'spine_03', lk, 25)
        add(ellipsoid(r * 0.2, 0.012, r * 0.2, n=7, m=4).move(x, y - 0.022, z), 'spine_03', 'arc', 60)
    add(K.cyl((-0.16, -0.19, 1.25), (0.17, -0.19, 1.47), 0.011, n=6), 'spine_03', 'blade', 40)                                              # a snapped drive rod
    add(K.torso_plate([(1.50, 0, 0.012, 0.155, 0.145), (1.545, 0, 0.004, 0.118, 0.118), (1.585, 0, -0.004, 0.105, 0.11)], n=16), 'neck_01', 'brass2', 38)

    # ------------------------------------------------------------------ the mask-face and the crown of blades
    hsec = [(1.556, 0, -0.012, 0.098, 0.108), (1.59, 0, -0.012, 0.118, 0.13), (1.66, 0, -0.012, 0.124, 0.137), (1.73, 0, -0.012, 0.116, 0.128),
            (1.785, 0, -0.012, 0.093, 0.103), (1.82, 0, -0.012, 0.055, 0.06), (1.836, 0, -0.012, 0.014, 0.016)]
    add(K.torso_plate(hsec, n=20), 'Head', 'dark', 40)
    mk = ellipsoid(0.097, 0.062, 0.135, n=16, m=10)
    mk = mk.sub(mk.face_centers()[:, 1] < -0.004)
    t = np.clip(-mk.v[:, 2] / 0.135, 0, 1)
    mk.v[:, 0] *= 1 - 0.38 * t ** 1.5                                               # narrow chin
    mk = mk.solidify(0.008).recalc()
    mc = np.array([0.0, -0.09, 1.672])
    add(mk.move(mc), 'Head', 'brass', 50)
    add(tube([mc + (-0.08, -0.03, 0.045), mc + (-0.035, -0.058, 0.055), mc + (0, -0.062, 0.045), mc + (0.035, -0.058, 0.055), mc + (0.08, -0.03, 0.045)],
             [0.008, 0.012, 0.01, 0.012, 0.008], n=5), 'Head', 'brass2', 40)                                                                 # brow
    add(M.prism([(-0.012, 0.045), (0.012, 0.045), (0.02, -0.035), (-0.02, -0.035)], 0.0, 0.03).rot('x', 90).move(mc + (0, -0.05, 0.0)), 'Head', 'brass2', 25)
    add(box(0.04, 0.012, 0.011).rot('z', -24).move(mc + (0.038, -0.049, 0.028)), 'Head', 'arc', 20)                                                         # the lit eye
    add(box(0.04, 0.012, 0.014).rot('z', 24).move(mc + (-0.038, -0.049, 0.028)), 'Head', 'dark', 20)                                                       # the dead eye
    add(box(0.06, 0.012, 0.008).move(mc + (0, -0.045, -0.065)), 'Head', 'dark', 20)                                                            # mouth slot
    crack = [mc + (-0.03, -0.06, 0.03), mc + (-0.05, -0.052, 0.065), mc + (-0.035, -0.045, 0.10), mc + (-0.055, -0.03, 0.135), mc + (-0.04, -0.012, 0.165)]
    add(tube(crack, 0.0055, n=4), 'Head', 'arc', 20)
    add(tube([mc + (-0.03, -0.06, 0.03), mc + (-0.045, -0.056, -0.01), mc + (-0.03, -0.05, -0.05), mc + (-0.05, -0.03, -0.09)], 0.005, n=4), 'Head', 'arc', 20)
    add(band((0.0, -0.012, 1.762), 0.118, 0.13, 0.05, 0.012, n=22), 'Head', 'brass2', 38)
    for k in range(11):
        a = math.radians(-90 + 360 * k / 11)
        h = (0.30 if k == 0 else 0.20 + 0.06 * ((k * 5) % 3)) * (0.35 if k in (3, 8) else 1.0)      # two blades snapped off
        sp = blade([(0.0, 0.022, 0.022, 0.012), (h * 0.55, 0.026, 0.026, 0.01), (h, 0.0, 0.004, 0.003)]) if k not in (3, 8) else \
            blade([(0.0, 0.022, 0.022, 0.012), (h, 0.02, 0.008, 0.009)], sharp_tip=False)
        sp.rot('x', 90).rot('x', -14).rot('z', math.degrees(a) + 90).move(0.118 * math.cos(a), -0.012 + 0.13 * math.sin(a), 1.775)
        add(sp, 'Head', 'brass' if k % 2 else 'blade', 22)
    sand((0.0, -0.012, 1.80), (0.09, 0.10, 0.03), 'Head', 2)

    # ------------------------------------------------------------------ arms (left, mirrored): stacked pauldrons, tubes, finned vambraces, block hands
    add(KX.ball((0.26, 0.066, 1.452), 0.10), 'upperarm_l', 'dark', 60, mirror=True)
    lm = KX.lame(0.20, 0.12, n=14)
    add(lm.copy().rot('y', 16).move(0.275, 0.068, 1.535), 'clavicle_l', 'brass', 40, mirror=True)
    add(lm.copy().scale((0.84, 0.88, 0.75)).rot('y', 44).move(0.37, 0.07, 1.50), 'upperarm_l', 'brass2', 40, mirror=True)
    add(lm.copy().scale((0.68, 0.76, 0.6)).rot('y', 66).move(0.43, 0.072, 1.455), 'upperarm_l', 'brass', 40, mirror=True)
    add(M.cone_spike(0.035, 0.17, n=6).rot('y', 30).move(0.30, 0.068, 1.63), 'clavicle_l', 'blade', 30, mirror=True)
    rp = [(0.275 + 0.165 * math.cos(math.radians(a)), 0.068 + 0.16 * math.sin(math.radians(a)), 1.565 - 0.05 * math.cos(math.radians(a))) for a in range(-150, 151, 50)]
    add(K.rivets(rp, [(0.2, 0, 1)] * len(rp), r=0.011), 'clavicle_l', 'brass2', 60, mirror=True)
    add(K.limb_plate('upperarm_l', [(0.50, 0.07, 0.07), (0.70, 0.072, 0.072), (0.92, 0.064, 0.064)], n=12, up=(0, 0, 1), lip=0.008), None, 'brass', 38, mirror=True)
    add(KX.ball(R.H('lowerarm_l'), 0.074), 'lowerarm_l', 'dark', 60, mirror=True)
    add(K.disc(0.046, 0.17, n=8, bevel=0.008).move(R.H('lowerarm_l')), 'lowerarm_l', 'brass2', 40, mirror=True)
    add(K.limb_plate('lowerarm_l', [(0.16, 0.064, 0.064), (0.45, 0.074, 0.074), (0.80, 0.078, 0.078), (0.93, 0.092, 0.092)], n=12, up=(0, 0, 1), lip=0.008),
        None, 'brass', 38, mirror=True)
    fin = blade([(0.0, 0.03, 0.03, 0.014), (0.16, 0.05, 0.02, 0.01), (0.27, 0.0, 0.004, 0.003)]).rot('z', 90).rot('x', -90)
    add(fin.move(0.52, 0.135, 1.44), 'lowerarm_l', 'blade', 22, mirror=True)
    add(KI.limb_ring('lowerarm_l', 0.50, 0.078, 0.009, n=12, m=4), None, 'brass2', 60, mirror=True)
    add(KX.ball(R.H('hand_l') + (0.012, 0, 0), 0.052), 'hand_l', 'dark', 60, mirror=True)
    add(box(0.11, 0.105, 0.04, bevel=0.008).move(0.815, 0.068, 1.438), 'hand_l', 'brass2', 35, mirror=True)
    K.finger_blocks(C, 'brass', w=0.023, h=0.022)
    sand((0.27, 0.07, 1.63), (0.10, 0.09, 0.028), 'clavicle_l', 3); sand((-0.29, 0.10, 1.62), (0.11, 0.08, 0.03), 'clavicle_r', 4)

    # ------------------------------------------------------------------ legs (left, mirrored)
    add(KX.ball(R.H('thigh_l') + (0.012, 0, 0), 0.09), 'thigh_l', 'dark', 60, mirror=True)
    add(K.limb_plate('thigh_l', [(0.22, 0.094, 0.09), (0.55, 0.09, 0.086), (0.88, 0.074, 0.074)], n=12, lip=0.008), None, 'brass', 38, mirror=True)
    add(KX.ball(R.H('calf_l'), 0.078), 'calf_l', 'dark', 60, mirror=True)
    add(K.disc(0.052, 0.18, n=10, bevel=0.008).rot('z', 90).move(R.H('calf_l')), 'calf_l', 'brass2', 40, mirror=True)
    add(KX.lame(0.08, 0.05, n=10).rot('x', 90).move(R.H('calf_l') + (0, -0.066, 0.0)), 'calf_l', 'brass', 40, mirror=True)
    add(M.cone_spike(0.022, 0.09, n=5).rot('x', 90).move(R.H('calf_l') + (0, -0.11, 0.0)), 'calf_l', 'blade', 30, mirror=True)
    add(K.limb_plate('calf_l', [(0.14, 0.068, 0.068), (0.40, 0.086, 0.082), (0.80, 0.074, 0.074), (0.93, 0.10, 0.10)], n=12, off=(0, 0.004, 0), lip=0.008),
        None, 'brass', 38, mirror=True)
    add(KI.limb_ring('calf_l', 0.42, 0.088, 0.01, n=12, m=4, off=(0, 0.004, 0)), None, 'brass2', 60, mirror=True)
    add(KX.ball(R.H('foot_l'), 0.055), 'foot_l', 'dark', 60, mirror=True)
    sab = tube([(0.089, 0.10, 0.052), (0.089, 0.03, 0.066), (0.089, -0.05, 0.052), (0.089, -0.115, 0.042)], [0.052, 0.066, 0.052, 0.042], n=10,
               r2=[0.058, 0.066, 0.07, 0.07], up=(0, 0, 1), power=3.0)
    add(sab, 'foot_l', 'brass2', 38, mirror=True)
    toe = tube([(0.089, -0.108, 0.04), (0.089, -0.19, 0.036), (0.089, -0.26, 0.026)], [0.04, 0.036, 0.016], n=10, r2=[0.07, 0.058, 0.02], up=(0, 0, 1), power=3.0)
    add(toe, 'ball_l', 'brass', 38, mirror=True)

    # ------------------------------------------------------------------ the yoke on his back and the three tattered banners
    for sx in (-1, 1):
        add(tube([(sx * 0.17, 0.24, 1.40), (sx * 0.22, 0.30, 1.56), (sx * 0.25, 0.33, 1.70)], 0.022, n=7), 'spine_03', 'brass2', 40)
        add(KX.ball((sx * 0.55, 0.33, 1.70), 0.04), 'spine_03', 'brass', 60)
    add(K.cyl((-0.55, 0.33, 1.70), (0.55, 0.33, 1.70), 0.02, n=8), 'spine_03', 'dark', 40)
    for k, (x0, x1, ln, lk, sd) in enumerate(((-0.17, 0.17, 1.50, 'banner', 4), (0.21, 0.51, 1.08, 'banner2', 6), (-0.51, -0.21, 1.20, 'banner2', 9))):
        xs = np.linspace(x0, x1, 7)
        top = np.stack([xs, np.full(7, 0.335), np.full(7, 1.70)], axis=1)
        out = np.tile((0.0, 1.0, 0.0), (7, 1))
        bn = hang(top, ln, rows=13, out=out, flare=0.16, rag=0.3, seed=sd, wave=0.035, holes=0.16, tatter=0.35, curve=1.2)
        KC.cape_weights(bn, z_top=1.70, z_hip=1.0, z_knee=0.5, thigh=0.4)
        C.add(bn, look=lk)
        for x in xs[::2]:
            add(torus(0.026, 0.006, n=8, m=4).rot('y', 90).move(x, 0.33, 1.70), 'spine_03', 'brass', 60)
        if k == 0:                                                                  # the dead sun-disc sewn on the royal banner
            c0 = np.array([0.0, 0.335 + 0.16 * (0.36 / ln) ** 1.2 + 0.012, 1.70 - 0.36])
            em = [torus(0.085, 0.012, n=16, m=4).rot('x', 90), K.disc(0.045, 0.01, n=12)]
            for q in range(8):
                a = math.radians(45 * q)
                em.append(box(0.016, 0.01, 0.06).rot('y', math.degrees(a)).move(0.125 * math.sin(a), 0, 0.125 * math.cos(a)))
            e = join(em).move(c0)
            KC.cape_weights(e, z_top=1.70, z_hip=1.0, z_knee=0.5, thigh=0.4)
            C.add(e, look='orph', smooth=40)

    glaive(C)
    return C, ("Colossal brass king-automaton, every hard part rigid on one bone: riveted hip drum with a royal tabard plate and lame "
               "skirt, piston waist, a cuirass torn open at the front (bent edges) over five gears with a violet light behind them "
               "(emis_arc), collar, mask-face with brow / nose / a lit and a dead eye and a lit crack running into a crown of eleven "
               "blades (two snapped), triple pauldrons with spikes, tubular limbs on dark ball joints, finned vambraces, block fingers, "
               "sand drifts in the hollows (rock_sand), a yoke on the back carrying three tattered banners (cape weights) with a sun-disc "
               "emblem; wpn_glaive: long haft, curved notched blade with back hook, rings and streamers.")
