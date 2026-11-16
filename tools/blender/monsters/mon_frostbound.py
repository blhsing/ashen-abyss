"""mon_frostbound 霜縛屍兵 — a long-dead garrison soldier still half entombed in the glacier that killed him:
frost-bitten corpse in a stiff hide coat and rimed fur mantle, scraps of iron (spangen cap with nasal, one pauldron,
chest disc, one greave), the whole left shoulder and left forearm locked in jagged ice, an icicle beard, icicle
fringes under the arms, and a spear whose head has frozen into a long ice blade."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_c as K
import kit_wisp as KW
import kit_ice as KI
from monlib import V, tube, ellipsoid, box, join, blade, horn, band, revolve, hang, arc_pts, rock


def spear(C, obj='wpn_spear'):
    """grip space: +Y towards the head, origin in the fist"""
    back, front = 0.52, 1.02
    r = 0.0125
    P = []
    sh = tube([(0, -back, 0), (0, -back * 0.4, 0.002), (0, front * 0.5, -0.002), (0, front, 0)], [r * 0.9, r, r, r * 0.95], n=6)
    P.append((sh, 'wood', 60))
    wrap = tube([(0, -0.085, 0), (0, -0.07, 0), (0, 0.0, 0), (0, 0.07, 0), (0, 0.085, 0)], [r * 1.05, r * 1.42, r * 1.34, r * 1.42, r * 1.05], n=6)
    P.append((wrap, 'strap', 60))
    butt = revolve([(0.0, -0.075), (r * 0.9, -0.015), (r * 1.4, 0.0), (r * 1.4, 0.035), (r * 0.9, 0.04)], 6).rot('x', -90).move(0, -back, 0)
    P.append((butt, 'iron', 40))
    sock = tube([(0, front - 0.10, 0), (0, front - 0.09, 0), (0, front + 0.04, 0)], [r * 1.05, r * 1.75, r * 1.3], n=6)
    P.append((sock, 'iron', 40))
    for sx in (-1, 1):            # the two lugs of a winged spear
        lug = blade([(0.0, 0.016, 0.016, 0.012), (0.03, 0.014, 0.016, 0.011), (0.075, 0.0, 0.004, 0.004)])
        lug.rot('z', -sx * 90).move(sx * r * 1.2, front - 0.03, 0)
        P.append((lug, 'iron', 25))
    # the head: a long faceted ice blade grown over the iron
    ice = K.crystal(0.058, 0.58, n=6, tip=0.26).scale((1.0, 0.40, 1.0)).rot('x', -90).move(0.004, front - 0.02, 0)
    ice.v[:, 0] += 0.012 * np.sin((ice.v[:, 1] - front) * 9.0)           # slightly crooked
    P.append((ice, 'ice', 0))
    vein = K.crystal(0.058, 0.50, n=6, tip=0.30).scale((0.13, 0.43, 1.0)).rot('x', -90).move(0.004, front + 0.0, 0)
    vein.v[:, 0] += 0.012 * np.sin((vein.v[:, 1] - front) * 9.0)
    P.append((vein, 'glow', 0))
    for i, (dx, dz, dy, h, rr, tilt) in enumerate(((0.03, 0.006, 0.02, 0.17, 0.022, 24), (-0.028, -0.004, 0.0, 0.13, 0.02, -30),
                                                   (0.006, 0.016, -0.05, 0.10, 0.018, 12), (-0.01, -0.016, -0.07, 0.08, 0.016, -16))):
        c = KI.crystal((dx, front + dy, dz), (math.sin(math.radians(tilt)), math.cos(math.radians(tilt)), dz * 8), h, rr, n=5, tip=0.4, spin=i * 31)
        P.append((c, 'ice2', 0))
    knot = rock(0.034, 0.05, 0.03, seed=21, n=14).move(0, front - 0.03, 0)
    P.append((knot, 'ice', 0))
    for p, lk, sm in P:
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('mon_frostbound', 15000)
    C.look('skin', 'frost_skin', 'skin_frost', color=(0.19, 0.225, 0.275), blotch=(0.075, 0.085, 0.135), vein=(0.04, 0.05, 0.12),
           sore=(0.08, 0.05, 0.10), sore_amt=0.5, hands_dark=0.9, dark=(0.03, 0.035, 0.05), hands_x0=0.5, hands_x1=0.8, bump=0.5,
           frost_amt=0.4, rime=0.2, frost_lo=0.4)
    C.look('maw', 'flat', 'skin_frost', color=(0.02, 0.025, 0.035), rough=0.5)
    C.look('tooth', 'bone', 'skin_frost', color=(0.50, 0.50, 0.46), stain=(0.2, 0.2, 0.22))
    C.look('iron', 'frost_metal', 'metal_frost', color=(0.085, 0.09, 0.105), rust=(0.085, 0.06, 0.05), rust2=(0.04, 0.032, 0.03), amount=0.45,
           edge=0.8, edge_color=(0.34, 0.36, 0.40), rough=0.45, frost_amt=0.5, frost_lo=0.55, rime=0.5, rime_lo=0.58, frost_edge=0.3, frost=(0.70, 0.82, 0.94))
    C.look('coat', 'frost_cloth', 'leather_hide', color=(0.115, 0.066, 0.042), dirt=(0.07, 0.065, 0.065), hem_lo=0.5, hem_hi=1.0,
           fold=10.0, frost_amt=0.45, frost_lo=0.4, rime=0.25, frost=(0.72, 0.82, 0.92))
    C.look('fur', 'fur', 'leather_hide', color=(0.062, 0.05, 0.04), tip=(0.17, 0.14, 0.115), frost=0.24, scale=38.0, bump=1.0)
    C.look('strap', 'frost_leather', 'leather_hide', color=(0.07, 0.05, 0.035), frost_amt=0.55, rime=0.3)
    C.look('wood', 'frost_wood', 'leather_hide', color=(0.10, 0.075, 0.055), axis=(0.152, 0.987, 0.05), frost_amt=0.6, rime=0.7,
           rime_lo=0.45, frost_scale=9.0)
    C.look('ice', 'crystal', 'rock_ice', color=(0.20, 0.42, 0.68), deep=(0.018, 0.085, 0.25), scale=8.0, crack=14.0, frost_amt=0.65)
    C.look('ice2', 'crystal', 'rock_ice', color=(0.40, 0.66, 0.90), deep=(0.09, 0.28, 0.52), scale=14.0, crack=30.0, frost_amt=0.35, edge=0.45)
    C.look('eye', 'emis', 'emis_frost', emit=(0.35, 0.72, 1.0), strength=2.8, mottle=0.15, base_mul=0.7)
    C.look('glow', 'emis', 'emis_frost', emit=(0.35, 0.72, 1.0), strength=2.8, mottle=0.5, base_mul=0.7, scale=30.0)
    R = M.RIG

    # ------------------------------------------------------------------ the corpse
    b = M.load_body('male')
    b = M.decimate(b, 0.5)
    KF.gaunt_face(b, 1.2)
    mouth_c, gap = KF.open_jaw(b, 19.0)
    lh = b.wsum(R.chain('hand_l')) > 0.55                       # the left hand is inside the ice block
    b = b.sub(~b.vmask_faces(lh, 'all'))
    pose = {
        'pelvis': dict(scale=(0.92, 0.88, 1.0)),
        'spine_01': dict(scale=(0.86, 0.80, 1.0)),
        'spine_02': dict(scale=(0.88, 0.84, 1.0), rot=('x', 4)),
        'spine_03': dict(scale=(0.97, 0.95, 1.0), rot=('x', 7)),
        'neck_01': dict(scale=(0.80, 0.80, 1.0), rot=('x', 12)),
        'Head': dict(rot=('x', -10)),
    }
    for s in ('l', 'r'):
        pose['upperarm_' + s] = dict(scale=(1.0, 0.74, 0.74))
        pose['lowerarm_' + s] = dict(scale=(1.0, 0.72, 0.72))
        pose['thigh_' + s] = dict(scale=(0.80, 0.80, 1.0))
        pose['calf_' + s] = dict(scale=(0.80, 0.80, 1.0))
    M.pose_sculpt(b, pose)
    dz = KA.ground(b)
    C.add(b, look='skin', smooth=180)
    H = KB2.Hunch(pose)

    def on(bone, p, look, smooth=None, pb=None, mirror=False):
        H.rigid(p, pb or bone)
        p.v[:, 2] -= dz
        return C.add(p, bone=bone, look=look, smooth=smooth, mirror=mirror)

    def ices(bone, parts, look='ice', pb=None, smooth=0):
        for p in parts:
            on(bone, p, look, smooth, pb)

    # face: dark maw, a few teeth, pale eyes
    on('Head', ellipsoid(0.021, 0.014, max(gap * 0.42, 0.012), n=10, m=6).move(mouth_c + np.array([0, 0.026, 0.002])), 'maw')
    for i, a in enumerate(np.linspace(-50, 50, 6)):
        ar = math.radians(a)
        p = np.array(KF.FACE['mouth']) + np.array([0.024 * math.sin(ar), 0.022 - 0.022 * math.cos(ar), -0.004])
        on('Head', M.cone_spike(0.0042, 0.012 + 0.004 * (i % 2), n=4).rot('x', 180).move(p), 'tooth')
    on('Head', join([ellipsoid(0.0105, 0.007, 0.0095, n=8, m=5).move(sx * 0.031, -0.095, 1.668) for sx in (-1, 1)]), 'eye')

    # ------------------------------------------------------------------ hide coat, boots, tabard strips
    nz = np.array([M.fbm(p, 8.0, 2.0) for p in b.v])
    core = b.wsum(['pelvis', 'spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r', 'neck_01', 'upperarm_l', 'upperarm_r',
                   'thigh_l', 'thigh_r'])
    cm = (core > 0.6) & (b.v[:, 2] > 0.66 + 0.09 * nz) & (np.abs(b.v[:, 0]) < 0.37 + 0.06 * nz) & (b.v[:, 2] < 1.53)
    coat = KF.shell(b, cm, 0.013)
    C.add(coat, look='coat')
    bm = (b.wsum(['calf_l', 'calf_r', 'foot_l', 'foot_r', 'ball_l', 'ball_r']) > 0.6) & (b.v[:, 2] < 0.36 + 0.05 * nz)
    boots = KF.shell(b, bm, 0.009)
    boots.v[:, 2] = np.maximum(boots.v[:, 2], 0.0015)
    C.add(boots, look='strap')
    cx, cy, rx, ry = KI.girth(coat, 1.0, 0.03, xlim=0.3)
    top, nr = arc_pts((cx, cy, 1.0), rx + 0.006, ry + 0.006, -128, -52, 7)
    r1 = hang(top, 0.46, rows=6, out=nr, flare=0.03, rag=0.3, seed=3, wave=0.012, holes=0.2, tatter=0.2)
    M.weights_by(r1, r1.v[:, 2], [(0.6, {'thigh_l': 0.4, 'thigh_r': 0.4, 'pelvis': 0.2}), (0.98, {'pelvis': 1.0})])
    C.add(r1, look='coat')
    top, nr = arc_pts((cx, cy, 1.0), rx + 0.006, ry + 0.006, 50, 130, 7)
    r2 = hang(top, 0.40, rows=6, out=nr, flare=0.04, rag=0.35, seed=8, wave=0.012, holes=0.25, tatter=0.25)
    M.weights_by(r2, r2.v[:, 2], [(0.64, {'thigh_l': 0.4, 'thigh_r': 0.4, 'pelvis': 0.2}), (0.98, {'pelvis': 1.0})])
    C.add(r2, look='coat')
    belt = band((cx, cy, 1.015), rx + 0.012, ry + 0.012, 0.046, 0.012, n=22)
    C.add(belt, bone='pelvis', look='strap')
    C.add(box(0.058, 0.014, 0.058, bevel=0.006).move(cx + 0.01, cy - ry - 0.016, 1.015), bone='pelvis', look='iron')
    # icicles under the belt, front-left and back
    fp, fn = arc_pts((cx, cy, 0.995), rx + 0.016, ry + 0.016, -75, 35, 6)
    for ic in KI.fringe(fp, (0, 0, -1), 0.13, r=0.011, seed=4):
        C.add(ic, bone='pelvis', look='ice2', smooth=30)

    # ------------------------------------------------------------------ rimed fur mantle (built in rest space, then hunched)
    mt, G = KW.poncho(X=0.335, Yf=0.150, Yb=0.178, n=40, rows=6, neck=(0.075, 0.100), neck_c=(0.0, -0.005),
                      ridge=((0.0, 1.566), (0.07, 1.558), (0.13, 1.534), (0.20, 1.514), (0.46, 1.502)),
                      depth_f=((0.0, 0.20), (0.1, 0.18), (0.2, 0.135), (0.3, 0.10), (0.42, 0.06)),
                      depth_b=((0.0, 0.27), (0.1, 0.24), (0.2, 0.165), (0.3, 0.11), (0.42, 0.06)),
                      rag=0.2, seed=6)
    mt.rough(0.008, 22.0, seed=3)
    KC.skin_cloth(mt, smooth=3)
    seat = lambda x, y, d=0.0: (KC.rest_on(mt, x, y) or 1.5) + d

    # right pauldron (two lames) on top of the fur, icicles from its outer rim
    dome = revolve([(0.0, 0.058), (0.035, 0.053), (0.070, 0.038), (0.094, 0.014), (0.101, 0.0)], n=12).scale((1.0, 0.94, 1.0))
    zs = seat(0.205, 0.055, -0.012)
    p1 = dome.copy().rot('y', 20).move(0.205, 0.055, zs).mirror()
    on('clavicle_r', p1, 'iron', 38)
    p2 = dome.copy().scale((0.8, 0.86, 0.72)).rot('y', 54).move(0.285, 0.060, zs - 0.03).mirror()
    on('upperarm_r', p2, 'iron', 38)
    rim = M.torus(0.098, 0.0075, n=12, m=4, ry=0.092).rot('y', 20).move(0.205, 0.055, zs + 0.002).mirror()
    on('clavicle_r', rim, 'iron', 50)
    pr, _ = arc_pts((-0.215, 0.055, zs - 0.012), 0.10, 0.094, 95, 265, 7)
    pr = pr[(pr[:, 1] < 0.0) | (pr[:, 1] > 0.125)]          # keep clear of the arm line
    ices('clavicle_r', KI.fringe(pr, (-0.12, 0, -1), 0.14, r=0.012, seed=5), 'ice2', smooth=30)

    # ------------------------------------------------------------------ the ice that still holds the left side
    zl = seat(0.21, 0.07)
    ices('clavicle_l', [rock(0.115, 0.10, 0.085, seed=11, n=20, jit=0.3).move(0.215, 0.072, zl + 0.022)])
    ices('spine_03', [rock(0.095, 0.085, 0.075, seed=12, n=18, jit=0.3).move(0.115, 0.135, seat(0.115, 0.135, 0.0)),
                      rock(0.07, 0.06, 0.06, seed=17, n=14, jit=0.3).move(-0.02, 0.17, 1.44)])
    ices('upperarm_l', [rock(0.085, 0.075, 0.068, seed=13, n=18, jit=0.3).move(0.315, 0.072, 1.478)])
    ices('clavicle_l', KI.cluster((0.225, 0.08, zl + 0.07), axis=(0.42, 0.18, 1.0), count=6, spread=42, h=(0.09, 0.30), r=(0.022, 0.044),
                                  radius=0.05, seed=3))
    ices('spine_03', KI.cluster((0.11, 0.16, zl + 0.05), axis=(0.1, 0.75, 0.75), count=4, spread=36, h=(0.08, 0.22), r=(0.02, 0.036),
                                radius=0.04, seed=7))
    ices('upperarm_l', KI.cluster((0.33, 0.085, 1.51), axis=(0.8, 0.3, 0.6), count=3, spread=34, h=(0.07, 0.16), r=(0.018, 0.03),
                                  radius=0.03, seed=9))
    # frozen left forearm and fist
    ices('lowerarm_l', [rock(0.095, 0.074, 0.068, seed=14, n=20, jit=0.28).move(0.665, 0.068, 1.44)])
    ices('hand_l', [rock(0.125, 0.095, 0.085, seed=15, n=22, jit=0.28).move(0.80, 0.066, 1.432)])
    ices('hand_l', KI.cluster((0.84, 0.07, 1.445), axis=(1.0, 0.1, 0.25), count=5, spread=40, h=(0.07, 0.20), r=(0.018, 0.034),
                              radius=0.04, seed=11))
    ices('lowerarm_l', KI.fringe(KI.line_pts((0.56, 0.10, 1.43), (0.72, 0.115, 1.425), 5, 0.006, 1), (0.25, 0.95, -0.12), 0.17,
                                 r=0.014, seed=6), 'ice2', smooth=30)
    ices('upperarm_l', KI.fringe(KI.line_pts((0.30, 0.10, 1.42), (0.45, 0.10, 1.42), 4, 0.006, 2), (0.6, 0.78, -0.15), 0.13,
                                 r=0.012, seed=7), 'ice2', smooth=30)

    # now hunch the mantle with the body
    H.skinned(mt)
    mt.v[:, 2] -= dz
    C.add(mt, look='fur')

    # ------------------------------------------------------------------ right arm: bracer + icicle fringes (hang when the arm is down / on guard)
    br = K.limb_plate('lowerarm_l', [(0.16, 0.044, 0.046), (0.45, 0.042, 0.044), (0.84, 0.036, 0.038)], n=9, lip=0.004, off=(0, 0.003, -0.004))
    on('lowerarm_r', br.mirror(), 'iron', 40)
    ices('lowerarm_r', KI.fringe(KI.line_pts((-0.52, 0.108, 1.435), (-0.69, 0.10, 1.435), 5, 0.005, 3), (-0.22, 0.95, -0.1), 0.15,
                                 r=0.012, seed=8), 'ice2', smooth=30)
    ices('upperarm_r', KI.fringe(KI.line_pts((-0.32, 0.098, 1.425), (-0.45, 0.10, 1.43), 4, 0.005, 4), (-0.6, 0.78, -0.15), 0.12,
                                 r=0.011, seed=9), 'ice2', smooth=30)

    # ------------------------------------------------------------------ chest: crossed straps and a dented disc
    gx, gy, grx, gry = KI.girth(coat, 1.31, 0.03, xlim=0.21)
    # (fitted to the already hunched coat, so these are added without posing them again)
    for tl in (-27, 27):
        C.add(band((gx, gy, 1.31), grx + 0.012, gry + 0.012, 0.030, 0.008, tilt_y=tl, n=22), bone='spine_03', look='strap')
    disc = K.disc(0.082, 0.022, n=14, bevel=0.007).move(gx, gy - gry - 0.018, 1.31)
    d = np.linalg.norm(disc.v - np.array([gx + 0.03, gy - gry - 0.03, 1.33]), axis=1)
    disc.v[:, 1] += np.exp(-(d / 0.03) ** 2) * 0.008
    C.add(disc, bone='spine_03', look='iron', smooth=38)
    C.add(ellipsoid(0.026, 0.016, 0.026, n=8, m=5).move(gx, gy - gry - 0.03, 1.31), bone='spine_03', look='iron')
    dp, _ = arc_pts((gx, gy - gry - 0.02, 1.31), 0.078, 0.001, 200, 340, 4)
    dp[:, 2] = 1.31 - np.sqrt(np.maximum(0.078 ** 2 - (dp[:, 0] - gx) ** 2, 0))
    for ic in KI.fringe(dp, (0, -0.05, -1), [0.05, 0.10, 0.075, 0.04], r=0.010, seed=10):
        C.add(ic, bone='spine_03', look='ice2', smooth=30)

    # ------------------------------------------------------------------ helm: spangen cap with nasal, ice crown on the left, icicle beard
    hs = [(1.700, 0, -0.012, 0.103, 0.124), (1.735, 0, -0.012, 0.103, 0.123), (1.775, 0, -0.012, 0.088, 0.106), (1.81, 0, -0.012, 0.06, 0.072),
          (1.835, 0, -0.012, 0.03, 0.036), (1.85, 0, -0.012, 0.008, 0.008)]
    helm = K.torso_plate(hs, n=18, cap0=False, cap1=True)
    dd = np.linalg.norm(helm.v - np.array([-0.06, -0.07, 1.79]), axis=1)
    helm.v[:, 2] -= np.exp(-(dd / 0.04) ** 2) * 0.012
    on('Head', helm, 'iron', 40)
    on('Head', band((0, -0.012, 1.704), 0.106, 0.127, 0.028, 0.011, n=18), 'iron', 40)
    for a in (-90, 0, 90, 180):
        on('Head', K.surf_strip(hs, [(1.705, a), (1.735, a), (1.775, a), (1.81, a), (1.838, a)], w=0.017, th=0.007), 'iron', 30)
    nasal = box(0.026, 0.009, 0.088, bevel=0.003).rot('x', -7).move(0, -0.143, 1.662)
    on('Head', nasal, 'iron', 30)
    ices('Head', [rock(0.07, 0.065, 0.05, seed=16, n=16, jit=0.3).move(0.05, 0.02, 1.80)])
    ices('Head', KI.cluster((0.052, 0.025, 1.81), axis=(0.45, 0.25, 1.0), count=4, spread=36, h=(0.06, 0.17), r=(0.016, 0.028),
                            radius=0.03, seed=13))
    hp, _ = arc_pts((0, -0.012, 1.692), 0.108, 0.129, 15, 165, 7)
    ices('Head', KI.fringe(hp, (0, 0.08, -1), 0.085, r=0.010, seed=12), 'ice2', smooth=30)
    for i, (x, L_) in enumerate(((-0.044, 0.05), (-0.027, 0.095), (-0.008, 0.165), (0.013, 0.12), (0.030, 0.08), (0.046, 0.045))):
        base = H.point('Head', (x, -0.094 + 0.45 * abs(x), 1.541)) - np.array([0, 0, dz])
        ic = K.icicle(base, L_ * 0.9, 0.013 * (0.6 + 0.4 * L_ / 0.165), (0, -0.30, -1), n=4, seed=40 + i)
        C.add(ic, bone='Head', look='ice2', smooth=30)

    # ------------------------------------------------------------------ legs: left greave + knee cop, right tasset hanging from the belt
    gr = K.limb_plate('calf_l', [(0.10, 0.064, 0.060), (0.30, 0.068, 0.062), (0.72, 0.052, 0.050), (0.95, 0.050, 0.048)], n=9,
                      off=(0.004, 0.006, 0), lip=0.004)
    on('calf_l', gr, 'iron', 40)
    on('calf_l', ellipsoid(0.054, 0.048, 0.062, n=10, m=6).move(0.091, -0.046, 0.535), 'iron', 40)
    ices('calf_l', KI.fringe([(0.06, -0.06, 0.50), (0.12, -0.062, 0.495), (0.092, -0.085, 0.50)], (0, -0.1, -1), [0.07, 0.05, 0.10],
                             r=0.010, seed=14), 'ice2', smooth=30)
    ts = K.shell_arc([(0.965, 0.089, -0.004, 0.097, 0.101), (0.86, 0.089, -0.004, 0.098, 0.100), (0.74, 0.089, -0.004, 0.088, 0.090)],
                     -120, 30, n=7, th=0.010)
    on('thigh_r', ts.mirror(), 'iron', 38)
    tp, _ = arc_pts((-0.089, -0.004, 0.745), 0.089, 0.091, 160, 290, 5)
    ices('thigh_r', KI.fringe(tp, (0, 0, -1), 0.09, r=0.010, seed=15), 'ice2', smooth=30)

    spear(C)
    return C, ("Frost-bitten corpse sculpted from the CC0 base body (left hand removed: it sits inside the ice), skin-tight hide coat and "
               "boots made from the body's own faces, fur mantle skinned by weight transfer. Rigid single-bone parts: spangen cap with "
               "nasal, right pauldron, chest disc, bracer, left greave, right tasset, and all ice (flat-shaded chunks and crystals on the "
               "left shoulder / back / helm, a block round the left forearm and fist, icicle beard and fringes). Arm icicles point along "
               "T-pose +Y so they hang down when the forearm is on guard and trail back when the arm hangs. wpn_spear in the right hand "
               "(0.52 m behind the fist, ice blade tip 1.58 m in front); emis_frost on the eyes and the vein of the spear blade.")
