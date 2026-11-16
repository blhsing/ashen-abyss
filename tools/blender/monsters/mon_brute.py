"""mon_brute 熔岩巨魔 — hulking lava troll: enormous shoulders and forearms, small head sunk between them,
slabs of basalt fused to the body, lava cracks, a huge stone maul."""
import math
import numpy as np
import monlib as M
import kit_flesh as KF
import kit_a as KA
from monlib import V, tube, ellipsoid, box, join, horn, rock


def maul(C, obj='wpn_maul'):
    parts = []
    shaft = tube([(0, -0.30, 0), (0, 0.0, 0), (0, 0.45, 0), (0, 0.92, 0)], [0.027, 0.025, 0.027, 0.03], n=8)
    parts.append((shaft, 'haft', 40))
    wrap = tube([(0, -0.11, 0), (0, 0.0, 0), (0, 0.13, 0)], [0.031, 0.03, 0.031], n=8)
    parts.append((wrap, 'wrap', 40))
    butt = M.revolve([(0.0, -0.05), (0.03, -0.04), (0.04, -0.015), (0.036, 0.012), (0.028, 0.02)], 8).rot('x', -90).move(0, -0.30, 0)
    parts.append((butt, 'iron', 35))
    head = box(0.40, 0.23, 0.24, bevel=0.025).subdiv(2)
    head.rough(0.016, 7.0, seed=4)
    head.v[:, 0] *= 1 + 0.25 * (np.abs(head.v[:, 0]) / 0.2) ** 2 * 0.3      # flared striking faces
    head.move(0, 0.80, 0)
    head.smooth = 0.0
    parts.append((head, 'basalt', 0))
    for x in (-0.085, 0.085):
        bandp = box(0.045, 0.262, 0.272, bevel=0.008).move(x, 0.80, 0)
        parts.append((bandp, 'iron', 35))
        for (yy, zz) in ((0.80, 0.14), (0.80, -0.14), (0.675, 0.0), (0.925, 0.0)):
            parts.append((ellipsoid(0.014, 0.014, 0.014, n=6, m=4).move(x, yy, zz), 'iron', 60))
    cap = M.cone_spike(0.05, 0.11, n=6).rot('x', -90).move(0, 0.91, 0)
    cap.smooth = 0
    parts.append((cap, 'iron', 0))
    # glowing seam through the stone head
    seam = tube([(-0.215, 0.74, 0.03), (-0.216, 0.78, -0.04), (-0.215, 0.83, 0.02), (-0.216, 0.87, -0.03)], 0.012, n=4)
    seam2 = tube([(0.215, 0.73, -0.05), (0.216, 0.79, 0.03), (0.215, 0.85, -0.01), (0.216, 0.88, 0.05)], 0.012, n=4)
    top = tube([(-0.18, 0.80, 0.122), (-0.03, 0.83, 0.125), (0.03, 0.77, 0.125), (0.18, 0.81, 0.122)], 0.011, n=4)
    for s in (seam, seam2, top):
        parts.append((s, 'lava', 20))
    for p, lk, sm in parts:
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('mon_brute', 15000)
    C.look('char', 'rock', 'rock_char', color=(0.055, 0.045, 0.04), ash=(0.16, 0.15, 0.14), scale=17.0, heat=(0.55, 0.11, 0.015),
           heat_amt=0.65, ash_amt=0.4, bump=0.85, crack_w=0.055)
    C.look('basalt', 'rock', 'rock_basalt', color=(0.03, 0.03, 0.033), ash=(0.21, 0.205, 0.2), scale=8.0, ash_amt=0.85, bump=0.9, crack_w=0.03)
    C.look('lava', 'emis', 'emis_lava', emit=(1.0, 0.30, 0.04), strength=2.2, base_mul=0.8, mottle=0.5)
    C.look('iron', 'metal', 'metal_iron', color=(0.14, 0.135, 0.13), amount=0.7, rough=0.5)
    C.look('haft', 'wood', 'leather_wrap', color=(0.07, 0.05, 0.035), axis=(0.15, 0.99, 0.05))
    C.look('wrap', 'leather', 'leather_wrap', color=(0.06, 0.04, 0.03))
    R = M.RIG

    b = M.load_body('male')
    b = M.decimate(b, 0.55)
    KF.gaunt_face(b, 0.8)
    mouth_c, gap = KF.open_jaw(b, 14.0)
    b.dent(KF.FACE['brow'], 0.055, 0.016, direction=(0, -1, 0.1))
    b.dent(KF.FACE['chin'], 0.06, 0.03, direction=(0, -1, -0.3))           # heavy underbite jaw
    pose = {
        'pelvis': dict(scale=(1.25, 1.25, 1.0)),
        'spine_01': dict(scale=(1.32, 1.38, 1.0)),
        'spine_02': dict(scale=(1.5, 1.55, 1.0), rot=('x', 6)),
        'spine_03': dict(scale=(1.62, 1.7, 1.0), rot=('x', 12)),
        'neck_01': dict(scale=(1.7, 1.6, 1.0), move=(0, 0, -0.075), rot=('x', 8)),
        'Head': dict(scale=0.84, rot=('x', -14)),
    }
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(rot=('x', -18), scale=(1.0, 1.75, 1.7))
        pose['upperarm_' + s] = dict(scale=(1.0, 1.95, 1.95))
        pose['lowerarm_' + s] = dict(scale=(1.0, 2.25, 2.25))
        pose['thigh_' + s] = dict(scale=(1.3, 1.4, 1.0))
        pose['calf_' + s] = dict(scale=(1.4, 1.4, 1.0))
        pose['foot_' + s] = dict(scale=(1.45, 1.25, 1.1))
    KA.hand_scale(pose, 1.4)
    M.pose_sculpt(b, pose)
    for sx in (-1, 1):       # trapezius / shoulder mass rising above the head
        b.dent((sx * 0.17, 0.08, 1.50), 0.15, 0.09, direction=(sx * 0.2, 0.2, 1))
    b.dent((0, 0.20, 1.45), 0.18, 0.08, direction=(0, 0.7, 0.7))
    dz = KA.ground(b)                                   # the body is lifted by -dz; the head parts below follow it (they used to stay behind)
    C.add(b, look='char', smooth=180)

    def on(bone, p, look='basalt', smooth=0, posed=True):
        if posed:
            M.pose_part(p, pose, bone)
        return C.add(p, bone=bone, look=look, smooth=smooth)

    def head(p, look, smooth=None):
        p.scale(0.84, pivot=R.H('Head'))
        M.pose_part(p, pose, 'Head')
        p.v[:, 2] -= dz
        return C.add(p, bone='Head', look=look, smooth=smooth)

    # --- head details: stone brow, tusks, glowing eyes and throat
    head(tube([(-0.08, -0.095, 1.705), (-0.04, -0.128, 1.70), (0, -0.132, 1.688), (0.04, -0.128, 1.70), (0.08, -0.095, 1.705)],
              [0.016, 0.026, 0.022, 0.026, 0.016], n=6), 'basalt', 0)
    for sx in (-1, 1):
        head(horn([(sx * 0.03, -0.108, 1.578), (sx * 0.042, -0.135, 1.60), (sx * 0.046, -0.14, 1.645)], 0.014, n=5), 'basalt', 50)
    head(ellipsoid(0.024, 0.014, max(gap * 0.45, 0.012), n=10, m=6).move(mouth_c + np.array([0, 0.024, 0.0])), 'lava')
    head(join([ellipsoid(0.011, 0.007, 0.007, n=8, m=5).move(sx * 0.033, -0.098, 1.668) for sx in (-1, 1)]), 'lava')

    # --- basalt slabs fused to the body
    rk = lambda rx, ry, rz, sd, n=16: rock(rx, ry, rz, seed=sd, n=n, jit=0.3)
    on('spine_03', rk(0.20, 0.13, 0.15, 1).rot('x', -25).move(0, 0.235, 1.50))                  # hump behind the head
    on('spine_03', rk(0.15, 0.10, 0.13, 2).rot('x', -15).move(0.13, 0.25, 1.36))
    on('spine_03', rk(0.15, 0.10, 0.13, 3).rot('x', -15).move(-0.14, 0.24, 1.34))
    on('spine_02', rk(0.14, 0.09, 0.11, 4).move(0.0, 0.21, 1.21))
    on('spine_03', rk(0.13, 0.07, 0.11, 5).rot('z', 12).move(0.13, -0.185, 1.37))               # chest plates
    on('spine_03', rk(0.13, 0.07, 0.11, 6).rot('z', -12).move(-0.13, -0.185, 1.37))
    on('spine_01', rk(0.15, 0.07, 0.09, 7).move(0, -0.155, 1.08))                               # gut plate
    on('pelvis', rk(0.11, 0.06, 0.13, 8).move(0, -0.12, 0.90), posed=False)                     # loin slab
    for sx in (1, -1):
        s = 'l' if sx > 0 else 'r'
        cl = 'clavicle_' + s; ua = 'upperarm_' + s; la = 'lowerarm_' + s
        p = rk(0.19, 0.17, 0.13, 10 + sx, 20).rot('y', sx * 20).move(sx * 0.27, 0.06, 1.66)
        C.add(M.pose_part(p, pose, 'spine_03'), bone=cl, look='basalt', smooth=0)                # shoulder boulders
        p = rk(0.10, 0.09, 0.14, 12 + sx).rot('y', sx * 30).move(sx * 0.19, 0.10, 1.78)
        C.add(M.pose_part(p, pose, 'spine_03'), bone=cl, look='basalt', smooth=0)
        on(ua, rk(0.14, 0.13, 0.10, 14 + sx).rot('y', sx * 10).move(sx * 0.37, 0.075, 1.60), posed=False)
        on(ua, rk(0.09, 0.13, 0.11, 30 + sx).move(sx * 0.42, 0.20, 1.45), posed=False)
        on(la, rk(0.14, 0.12, 0.09, 16 + sx).move(sx * 0.58, 0.07, 1.585), posed=False)          # bracer slabs
        on(la, rk(0.10, 0.10, 0.12, 18 + sx).move(sx * 0.60, 0.19, 1.45), posed=False)
        on(la, rk(0.09, 0.08, 0.12, 20 + sx).move(sx * 0.57, -0.065, 1.44), posed=False)
        on('hand_' + s, rk(0.06, 0.07, 0.035, 22 + sx).move(sx * 0.80, 0.07, 1.475), posed=False)  # knuckle stone
        on('thigh_' + s, rk(0.075, 0.09, 0.15, 24 + sx).move(sx * 0.205, 0.0, 0.74), posed=False)
        on('calf_' + s, rk(0.085, 0.06, 0.15, 26 + sx).move(sx * 0.095, -0.085, 0.34), posed=False)
        on('calf_' + s, rk(0.07, 0.07, 0.08, 28 + sx).move(sx * 0.095, -0.07, 0.535), posed=False)  # knee cap

    KF.claws(C, 'basalt', body=b, length=0.045, curl=0.3)
    KF.toe_claws(C, 'basalt', length=0.045, r=0.014)

    # --- lava cracks
    allm = (b.v[:, 2] > 0.12) & (np.abs(b.v[:, 0]) < 0.82)
    KA.add_cracks(C, b, allm, 'lava', count=38, steps=(5, 11), seed=3, width=0.03, height=0.006, sink=0.004, branch=0.35)
    core = (b.wsum(['spine_01', 'spine_02', 'spine_03']) > 0.6) & (b.v[:, 1] < 0.02)
    KA.add_cracks(C, b, core, 'lava', count=8, steps=(4, 8), seed=6, width=0.05, height=0.007, sink=0.004, branch=0.5)

    maul(C)
    return C, "Hulking lava troll on the reshaped CC0 base body (huge shoulders/forearms, small sunken head), rigid flat-shaded basalt slabs fused onto shoulders/back/forearms/shins, emis_lava fissures riding on the skin; wpn_maul (stone head, iron bands) in the right hand."
