"""mon_fiend ç‡¼é­” (0.6 scale = imp å°ç‡¼é­”) â€” horned demon of charred, cracked rock skin with glowing lava fissures,
clawed hands, digitigrade-looking legs (mesh only), short barbed tail on the hips."""
import math
import numpy as np
import monlib as M
import kit_flesh as KF
import kit_a as KA
from monlib import V, tube, ellipsoid, box, join, horn, rock

HS = 1.24   # head enlargement (keeps the imp readable)


def build():
    C = M.Creature('mon_fiend', 15000)
    C.look('char', 'rock', 'rock_char', color=(0.05, 0.043, 0.04), ash=(0.17, 0.155, 0.145), scale=24.0, heat=(0.50, 0.10, 0.015),
           heat_amt=0.6, ash_amt=0.45, bump=0.8, crack_w=0.05)
    C.look('slab', 'rock', 'rock_char', color=(0.034, 0.031, 0.031), ash=(0.2, 0.19, 0.18), scale=13.0, ash_amt=0.8, bump=0.9)
    C.look('horn', 'bone', 'bone_horn', color=(0.40, 0.35, 0.27), stain=(0.11, 0.075, 0.05), crack=40.0, blotch=5.0, bump=0.6, dirt_lo=1.55, dirt_hi=1.75, dirt=(0.02, 0.016, 0.014))
    C.look('lava', 'emis', 'emis_lava', emit=(1.0, 0.30, 0.04), strength=2.2, base_mul=0.8, mottle=0.5)
    R = M.RIG

    b = M.load_body('male')
    b = M.decimate(b, 0.58)
    KF.gaunt_face(b, 1.0)
    mouth_c, gap = KF.open_jaw(b, 17.0)
    b.dent(KF.FACE['brow'], 0.05, 0.012, direction=(0, -1, 0.2))        # heavy brow
    b.dent((0, -0.12, 1.60), 0.045, 0.02, direction=(0, -1, -0.2))       # muzzle
    pose = {
        'pelvis': dict(scale=(0.9, 0.9, 1.0)),
        'spine_01': dict(scale=(0.98, 0.95, 1.0)),
        'spine_02': dict(scale=(1.06, 1.04, 1.0), rot=('x', 5)),
        'spine_03': dict(scale=(1.22, 1.18, 1.0), rot=('x', 9)),
        'neck_01': dict(scale=(1.2, 1.2, 1.0), rot=('x', 14)),
        'Head': dict(scale=HS, rot=('x', -12)),
    }
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(rot=('x', -14), scale=(1.0, 1.15, 1.15))
        pose['upperarm_' + s] = dict(scale=(1.0, 1.12, 1.12))
        pose['lowerarm_' + s] = dict(scale=(1.0, 1.2, 1.2))
        pose['thigh_' + s] = dict(scale=(1.0, 1.15, 1.0))
        pose['calf_' + s] = dict(scale=(0.92, 0.98, 1.0))
        pose['foot_' + s] = dict(scale=(1.2, 1.25, 1.0))
    KA.hand_scale(pose, 1.38)
    M.pose_sculpt(b, pose)
    # --- digitigrade illusion: big haunch, hock jutting backwards, thin lower shin
    for sx in (-1, 1):
        cw = b.W[:, R.idx['calf_l' if sx > 0 else 'calf_r']]
        low = M.smoothstep(0.40, 0.26, b.v[:, 2]) * cw
        ax = np.array([sx * 0.089, 0.03])
        b.v[:, :2] = b.v[:, :2] + (ax - b.v[:, :2]) * (0.30 * low)[:, None]
        b.dent((sx * 0.089, 0.10, 0.46), 0.085, 0.06, direction=(0, 1, 0.25))
        b.dent((sx * 0.10, -0.10, 0.74), 0.13, 0.035, direction=(0, -1, 0))
    KA.ground(b)
    C.add(b, look='char', smooth=180)

    def head(p, look, smooth=None):
        p.scale(HS, pivot=R.H('Head'))
        M.pose_part(p, pose, 'Head')
        return C.add(p, bone='Head', look=look, smooth=smooth)

    # --- horns: big, ridged, swept back
    for sx in (1, -1):
        pts = M.polyline([(sx * 0.052, -0.05, 1.742), (sx * 0.10, -0.03, 1.815), (sx * 0.135, 0.05, 1.868), (sx * 0.132, 0.15, 1.878),
                          (sx * 0.105, 0.235, 1.842), (sx * 0.08, 0.30, 1.785)], 14)
        t = np.linspace(0, 1, 14)
        r = 0.036 * (1 - t) ** 0.85 * (1 + 0.10 * np.sin(t * 34)) + 0.002
        head(tube(pts, r, n=8), 'horn', 60)
        # small second horn / cheek spike
        head(horn(M.polyline([(sx * 0.075, -0.06, 1.63), (sx * 0.115, -0.07, 1.61), (sx * 0.15, -0.045, 1.635)], 5), 0.016, n=5), 'horn', 60)
    brow = tube([(-0.075, -0.098, 1.70), (-0.035, -0.125, 1.692), (0, -0.128, 1.682), (0.035, -0.125, 1.692), (0.075, -0.098, 1.70)],
                [0.012, 0.019, 0.016, 0.019, 0.012], n=6)
    head(brow, 'slab', 35)
    head(horn([(0, -0.10, 1.56), (0, -0.12, 1.535), (0, -0.115, 1.505)], 0.014, n=5), 'horn', 60)      # chin spike
    maw = ellipsoid(0.023, 0.014, max(gap * 0.45, 0.012), n=10, m=6).move(mouth_c + np.array([0, 0.024, 0.002]))
    head(maw, 'lava')
    for i, a in enumerate(np.linspace(-52, 52, 6)):
        ar = math.radians(a)
        p = np.array(KF.FACE['mouth']) + np.array([0.024 * math.sin(ar), 0.03 - 0.022 * math.cos(ar), -0.002])
        head(M.cone_spike(0.0048, 0.017 + 0.006 * (i % 2), n=4).rot('x', 180).move(p), 'horn')
    head(join([ellipsoid(0.011, 0.007, 0.008, n=8, m=5).rot('z', sx * -12).move(sx * 0.032, -0.099, 1.668) for sx in (-1, 1)]), 'lava')

    # --- spines down the back, shoulder slabs, elbow spikes
    for i, (z, y, h) in enumerate(((1.47, 0.12, 0.10), (1.40, 0.135, 0.14), (1.31, 0.13, 0.12), (1.22, 0.105, 0.09), (1.13, 0.09, 0.07))):
        bn = 'spine_03' if z > 1.32 else ('spine_02' if z > 1.18 else 'spine_01')
        sp = M.cone_spike(0.034, h, n=5).rot('x', -62).move(0, y, z)
        sp.smooth = 0
        C.add(M.pose_part(sp, pose, bn), bone=bn, look='slab')
    for sx in (1, -1):
        sl = rock(0.085, 0.075, 0.05, seed=3, n=16).rot('y', sx * 18).move(sx * 0.215, 0.07, 1.515)
        C.add(M.pose_part(sl, pose, 'spine_03'), bone='clavicle_l' if sx > 0 else 'clavicle_r', look='slab')
        sp = horn([(sx * 0.215, 0.07, 1.53), (sx * 0.25, 0.085, 1.60), (sx * 0.30, 0.10, 1.635)], 0.026, n=5)
        sp.smooth = 0
        C.add(M.pose_part(sp, pose, 'spine_03'), bone='clavicle_l' if sx > 0 else 'clavicle_r', look='slab')
        eb = horn([(sx * 0.455, 0.10, 1.44), (sx * 0.47, 0.17, 1.455), (sx * 0.50, 0.225, 1.49)], 0.024, n=5)
        C.add(eb, bone='upperarm_l' if sx > 0 else 'upperarm_r', look='horn', smooth=60)
        # legs: hock spur, shin plate, heel spur
        cb = 'calf_l' if sx > 0 else 'calf_r'; fb = 'foot_l' if sx > 0 else 'foot_r'
        hk = horn([(sx * 0.089, 0.12, 0.45), (sx * 0.089, 0.20, 0.50), (sx * 0.089, 0.255, 0.58)], 0.034, n=6)
        C.add(hk, bone=cb, look='horn', smooth=60)
        gv = rock(0.05, 0.028, 0.12, seed=7, n=14).move(sx * 0.089, -0.045, 0.33)
        C.add(gv, bone=cb, look='slab')
        kn = M.cone_spike(0.03, 0.08, n=5).rot('x', 115).move(sx * 0.089, -0.045, 0.52)
        kn.smooth = 0
        C.add(kn, bone=cb, look='slab')
        hs = horn([(sx * 0.089, 0.085, 0.06), (sx * 0.089, 0.15, 0.085), (sx * 0.089, 0.195, 0.14)], 0.022, n=5)
        C.add(hs, bone=fb, look='horn', smooth=60)

    # --- tail (rigid on the hips) with a barb
    tp = M.polyline([(0, 0.09, 0.99), (0, 0.24, 0.93), (0, 0.39, 0.83), (0, 0.52, 0.79), (0, 0.63, 0.83), (0, 0.69, 0.93)], 12)
    tr = np.linspace(0.052, 0.014, 12)
    C.add(tube(tp, tr, n=8), bone='pelvis', look='char', smooth=60)
    barb = M.blade([(0.0, 0.012, 0.012, 0.02), (0.03, 0.05, 0.05, 0.024), (0.06, 0.03, 0.03, 0.018), (0.12, 0.0, 0.002, 0.004)])
    barb.orient((0, 0.45, 0.9), (0, -1, 0.5), origin=(0, 0.685, 0.92))
    C.add(barb, bone='pelvis', look='horn', smooth=25)
    for k in (3, 5, 7):
        sp = M.cone_spike(0.016, 0.05, n=4).rot('x', -40).move(tp[k] + np.array([0, 0.0, tr[k] * 0.8]))
        C.add(sp, bone='pelvis', look='slab', smooth=0)

    KF.claws(C, 'horn', body=b, length=0.075, curl=0.5)
    KF.toe_claws(C, 'horn', length=0.05, r=0.011)

    # --- lava fissures (real geometry, emis_lava)
    allm = (b.v[:, 2] > 0.1) & (np.abs(b.v[:, 0]) < 0.80)
    KA.add_cracks(C, b, allm, 'lava', count=44, steps=(5, 12), seed=5, width=0.021, height=0.0045, sink=0.003, branch=0.35)
    chest = (b.wsum(['spine_02', 'spine_03']) > 0.6) & (b.v[:, 1] < 0.0)
    KA.add_cracks(C, b, chest, 'lava', count=7, steps=(4, 8), seed=9, width=0.034, height=0.005, sink=0.003, branch=0.5)
    return C, "Horned ember demon on the reshaped CC0 base body: charred rock skin, lava fissures as emis_lava geometry riding on the skin weights, ridged swept-back horns, clawed oversized hands, faked digitigrade legs (hock/heel spurs), barbed tail rigid on the pelvis. Designed to stay readable at 0.6 scale (imp). No weapon."
