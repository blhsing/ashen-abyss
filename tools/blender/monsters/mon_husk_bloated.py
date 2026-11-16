"""mon_husk_bloated è…«è„¹é£¢éª¸ â€” grotesquely swollen husk: huge belly, glowing pustules, split skin, stubby fat arms."""
import math
import numpy as np
import monlib as M
import kit_flesh as KF
import kit_a as KA
from monlib import V, tube, ellipsoid, box, join, hang, arc_pts


def build():
    C = M.Creature('mon_husk_bloated', 15000)
    C.look('skin', 'skin', 'skin_husk', color=(0.36, 0.365, 0.33), blotch=(0.25, 0.17, 0.19), hands_dark=0.7, feet_dirt=0.8,
           sore_amt=0.9, sore_lo=0.62, vein_amt=0.95, vein_scale=8.0, bump=0.5)
    C.look('boil', 'skin', 'skin_husk', color=(0.42, 0.27, 0.21), blotch=(0.30, 0.11, 0.09), vein_amt=0.2, sore_amt=0.2, rough=0.35)
    C.look('gore', 'flat', 'skin_husk', color=(0.12, 0.012, 0.01), rough=0.32)
    C.look('pus', 'emis', 'emis_pustule', emit=(0.50, 0.92, 0.08), strength=1.25, base_mul=0.8, mottle=0.6, rough=0.3)
    C.look('rag', 'cloth', 'cloth_rag', color=(0.105, 0.085, 0.07), hem_lo=0.4, hem_hi=0.9, stain_amt=0.7)
    C.look('claw', 'bone', 'bone_claw', color=(0.16, 0.13, 0.10), stain=(0.05, 0.04, 0.035), crack_amt=0.2)
    C.look('tooth', 'bone', 'bone_claw', color=(0.50, 0.44, 0.31), stain=(0.2, 0.13, 0.07))

    b = M.load_body('male')
    b = M.decimate(b, 0.62)
    KF.gaunt_face(b, 0.7)
    mouth_c, gap = KF.open_jaw(b, 15.0)
    pose = {
        'pelvis': dict(scale=(1.55, 1.7, 1.0)),
        'spine_01': dict(scale=(1.72, 2.05, 1.0)),
        'spine_02': dict(scale=(1.72, 2.0, 1.0)),
        'spine_03': dict(scale=(1.42, 1.45, 1.0)),
        'neck_01': dict(scale=(1.75, 1.6, 1.0), move=(0, 0, -0.04)),
        'Head': dict(scale=(0.9, 0.92, 0.92)),
    }
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(scale=(1.0, 1.45, 1.4))
        pose['upperarm_' + s] = dict(scale=(1.0, 1.75, 1.75))
        pose['lowerarm_' + s] = dict(scale=(1.0, 1.5, 1.5))
        pose['hand_' + s] = dict(scale=(1.0, 1.25, 1.25))
        pose['thigh_' + s] = dict(scale=(1.25, 1.5, 1.0))
        pose['calf_' + s] = dict(scale=(1.35, 1.35, 1.0))
        pose['foot_' + s] = dict(scale=(1.25, 1.12, 1.0))
    M.pose_sculpt(b, pose)
    # belly: bulge forward and sag over the hips, fat rolls at the flanks and nape
    b.dent((0, -0.20, 1.10), 0.24, 0.13, direction=(0, -1, -0.3))
    b.dent((0, -0.26, 0.95), 0.16, 0.07, direction=(0, -0.5, -1))
    for sx in (-1, 1):
        b.dent((sx * 0.24, 0.02, 1.08), 0.16, 0.05, direction=(sx, 0, -0.2))
        b.dent((sx * 0.14, 0.10, 1.47), 0.12, 0.05, direction=(0, 0.3, 1))      # fat shoulders rising around the head
    b.dent((0, 0.16, 1.44), 0.13, 0.05, direction=(0, 0.6, 0.8))                 # neck hump
    dz = KA.ground(b)
    C.add(b, look='skin', smooth=180)

    def head_tf(p):
        """Head sculpt scale, then its pose, then the body's ground shift: the same steps the face mesh went through (pose_part alone
        skips the scale and the shift, which left maw / teeth / eyes ahead of and below the face)"""
        p.scale(pose['Head']['scale'], pivot=M.RIG.H('Head'))
        M.pose_part(p, pose, 'Head')
        p.v[:, 2] -= dz
        return p

    # face
    maw = ellipsoid(0.02, 0.013, max(gap * 0.42, 0.011), n=10, m=6).move(mouth_c + np.array([0, 0.026, 0.002]))
    C.add(head_tf(maw), bone='Head', look='gore')
    for i, a in enumerate(np.linspace(-50, 50, 6)):
        ar = math.radians(a)
        p = np.array(KF.FACE['mouth']) + np.array([0.022 * math.sin(ar), 0.03 - 0.02 * math.cos(ar), -0.003])
        t = M.cone_spike(0.004, 0.012 + 0.004 * (i % 2), n=4).rot('x', 180).move(p)
        C.add(head_tf(t), bone='Head', look='tooth')
    eyes = join([ellipsoid(0.008, 0.006, 0.008, n=7, m=5).move(sx * 0.031, -0.096, 1.668) for sx in (-1, 1)])
    C.add(head_tf(eyes), bone='Head', look='pus')

    # split skin on the belly: dark wet tears, the widest ones glowing from inside
    torso = b.wsum(['pelvis', 'spine_01', 'spine_02', 'spine_03']) > 0.55
    belly = torso & (b.v[:, 2] > 0.86) & (b.v[:, 2] < 1.38)
    paths = KA.crack_paths(b, belly, count=13, steps=(5, 10), seed=11, dir_hint=(0, 0, -1), hint_w=0.9, wander=0.5, branch=0.2)
    C.add(KA.ribbons(b, paths, width=0.06, height=0.004, sink=0.004, seed=1), look='gore')
    # (the previous filter `len(p) >= 9` matched no path with this seed, ribbons() returned None and the build
    #  crashed: glow the three longest tears instead)
    wide = sorted([p for p in paths if len(p) >= 6], key=len, reverse=True)[:3]
    glow = KA.ribbons(b, [p[1:-1] for p in wide], width=0.022, height=0.0065, sink=0.002, seed=2)
    if glow is not None:
        C.add(glow, look='pus')

    # pustules: big glowing ones in clusters on belly / back / shoulders, small boils everywhere
    upper = (b.wsum(['spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r', 'upperarm_l', 'upperarm_r', 'neck_01']) > 0.55) & (b.v[:, 2] > 0.95)
    big = KA.scatter(b, upper, 26, seed=3, min_dist=0.085)
    KA.blobs(C, b, big[:14], 'pus', r=(0.026, 0.056), seed=4, sink=0.34, n=10, m=6)
    KA.blobs(C, b, big[14:], 'boil', r=(0.022, 0.05), seed=5, sink=0.38, n=10, m=6)
    rng = np.random.default_rng(8)
    vn = b.normals()
    sat = []
    for vi in big[:14]:                       # satellites around the big glowing ones -> clusters
        d = np.linalg.norm(b.v - b.v[vi], axis=1)
        near = np.where((d > 0.035) & (d < 0.075) & upper)[0]
        if len(near):
            sat += [int(x) for x in rng.choice(near, size=min(2, len(near)), replace=False)]
    KA.blobs(C, b, sat, 'pus', r=(0.010, 0.02), seed=6, sink=0.3, n=7, m=4)
    whole = (b.wsum(['Head']) < 0.3) & (b.v[:, 2] > 0.35) & (np.abs(b.v[:, 0]) < 0.72)
    KA.blobs(C, b, KA.scatter(b, whole, 30, seed=9, min_dist=0.06), 'boil', r=(0.008, 0.017), seed=10, sink=0.3, n=6, m=4)

    KF.claws(C, 'claw', body=b, length=0.028, curl=0.35)

    # burst rags: wrap under the belly + a torn strap that no longer closes
    nz = np.array([M.fbm(p, 8.0, 5.0) for p in b.v])
    hips = (b.wsum(['pelvis', 'thigh_l', 'thigh_r']) > 0.5) & (b.v[:, 2] > 0.66 + 0.07 * nz) & (b.v[:, 2] < 0.93 + 0.04 * nz)
    C.add(KF.shell(b, hips, 0.012), look='rag')
    top, nr = arc_pts((0, -0.02, 0.82), 0.20, 0.20, -125, -55, 6)
    r1 = hang(top, 0.30, rows=5, out=nr, flare=0.02, rag=0.45, seed=2, wave=0.012, holes=0.15, tatter=0.3)
    M.weights_by(r1, r1.v[:, 2], [(0.55, {'thigh_l': 0.4, 'thigh_r': 0.4, 'pelvis': 0.2}), (0.82, {'pelvis': 1.0})])
    C.add(r1, look='rag')
    sash_d = np.abs(b.v[:, 0] * 0.55 + (b.v[:, 2] - 1.33) * 0.83)
    sash = (b.wsum(['spine_02', 'spine_03', 'clavicle_r', 'neck_01']) > 0.55) & (sash_d < 0.05 + 0.03 * nz) & (b.v[:, 2] > 1.22)
    C.add(KF.shell(b, sash, 0.012), look='rag')
    return C, "Bloated husk sculpted from the CC0 base body: huge sagging belly, split skin (gore ribbons), clusters of glowing emis_pustule boils, fat stubby arms, head sunk into the shoulders. No weapon."
