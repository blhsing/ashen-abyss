"""mon_husk 飢骸 — emaciated grey corpse-thing: sunken torso with ribs, long claws, hunched neck, hanging jaw."""
import math
import numpy as np
import monlib as M
import kit_bones as KB
import kit_flesh as KF
from monlib import V, tube, ellipsoid, box, join, hang, arc_pts


def build():
    C = M.Creature('mon_husk', 15000)
    C.look('skin', 'skin', 'skin_husk', color=(0.33, 0.34, 0.31), blotch=(0.19, 0.16, 0.18), hands_dark=0.85, feet_dirt=0.8,
           sore_amt=0.5, bump=0.5)
    C.look('claw', 'bone', 'bone_claw', color=(0.16, 0.13, 0.10), stain=(0.05, 0.04, 0.035), crack_amt=0.2)
    C.look('tooth', 'bone', 'bone_claw', color=(0.52, 0.46, 0.33), stain=(0.2, 0.13, 0.07))
    C.look('rag', 'cloth', 'cloth_rag', color=(0.105, 0.085, 0.07), hem_lo=0.5, hem_hi=1.0, stain_amt=0.7)
    C.look('maw', 'flat', 'skin_husk', color=(0.035, 0.008, 0.008), rough=0.5)
    C.look('eye', 'emis', 'emis_eye', emit=(1.0, 0.82, 0.35), strength=2.5)

    b = M.load_body('male')
    b = M.decimate(b, 0.68)
    KF.gaunt_face(b, 1.2)
    mouth_c, gap = KF.open_jaw(b, 20.0)
    pose = {
        'pelvis': dict(scale=(0.86, 0.78, 1.0)),
        'spine_01': dict(scale=(0.80, 0.66, 1.0)),
        'spine_02': dict(scale=(0.78, 0.64, 1.0), rot=('x', 7)),
        'spine_03': dict(scale=(0.88, 0.86, 1.0), rot=('x', 13)),
        'neck_01': dict(scale=(0.70, 0.70, 1.0), rot=('x', 28)),
        'Head': dict(scale=(0.93, 0.97, 1.0), rot=('x', -16)),
    }
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(rot=('x', -20), scale=(1.0, 0.85, 0.85))
        pose['upperarm_' + s] = dict(scale=(1.0, 0.58, 0.58))
        pose['lowerarm_' + s] = dict(scale=(1.0, 0.60, 0.60))
        pose['hand_' + s] = dict(scale=(1.0, 1.0, 0.8))
        pose['thigh_' + s] = dict(scale=(0.60, 0.60, 1.0))
        pose['calf_' + s] = dict(scale=(0.64, 0.64, 1.0))
        pose['foot_' + s] = dict(scale=(0.85, 1.0, 0.85))
    rest = b.copy()
    M.pose_sculpt(b, pose)
    dz = float(b.v[:, 2].min())
    b.v[:, 2] -= dz
    M.log("husk ground shift dz = %.4f" % dz)
    C.add(b, look='skin', smooth=180)

    # mouth cavity + teeth. Everything on the head goes through head_tf(): the Head bone's sculpt SCALE, then its pose, then
    # the ground shift of the body -- the same three steps the face itself went through (the teeth used to skip the scale and
    # the shift and sat on the lip's outer surface, so they hung in the air in front of the mouth).
    R = M.RIG
    hs = pose['Head']['scale']

    def head_tf(p):
        p.scale(hs, pivot=R.H('Head'))
        M.pose_part(p, pose, 'Head')
        p.v[:, 2] -= dz
        return p

    maw = ellipsoid(0.021, 0.014, max(gap * 0.42, 0.012), n=10, m=6).move(mouth_c + np.array([0, 0.026, 0.002]))
    C.add(head_tf(maw), bone='Head', look='maw')
    hv = np.where(rest.wsum(['Head']) > 0.5)[0]
    hinge = np.array(KF.FACE['jaw_hinge']); Rj = M.rot_axis('x', 20.0)             # the same hinge rotation KF.open_jaw applied to the lower jaw
    m0 = np.array(KF.FACE['mouth'])
    up = Rj @ np.array([0.0, 0.0, 1.0])

    def seat(target):
        """the vertex of the (rest-space, jaw already open) face nearest to a target on the lip line"""
        return rest.v[hv[np.argmin(np.linalg.norm(rest.v[hv] - target, axis=1))]].copy()

    for i, a in enumerate(np.linspace(-56, 56, 7)):
        ar = math.radians(a)
        s = seat(m0 + np.array([0.026 * math.sin(ar), 0.020 * (1 - math.cos(ar)), 0.004]))          # upper gum: 6 mm behind the lip, root sunk 3 mm
        t = M.cone_spike(0.0042, 0.012 + 0.004 * (i % 2), n=4).rot('x', 180).move(s + np.array([0, 0.006, 0.003]))
        C.add(head_tf(t), bone='Head', look='tooth')
        tl = (m0 + np.array([0.024 * math.sin(ar), 0.020 * (1 - math.cos(ar)), -0.008]) - hinge) @ Rj.T + hinge
        s = seat(tl)                                                                                  # lower gum, in the hanging jaw
        t = M.cone_spike(0.004, 0.010 + 0.003 * ((i + 1) % 2), n=4)
        t.tf(M.rot_to((0, 0, 1), up)).move(s + np.array([0, 0.006, 0.0]) - up * 0.003)
        C.add(head_tf(t), bone='Head', look='tooth')
    eyes = join([ellipsoid(0.008, 0.006, 0.008, n=7, m=5).move(sx * 0.031, -0.094, 1.668) for sx in (-1, 1)])
    C.add(head_tf(eyes), bone='Head', look='eye')

    # ribs pushing through the skin of the sunken chest
    for k in range(2, 8):
        for sx in (1, -1):
            pts = KB.rib_path(k, sx, 9, wide=0.93, deep=0.90)
            pts[:, 1] -= 0.004
            rib = tube(pts, 0.011, n=5, r2=0.0075, up=(0, 0, 1))
            C.add(M.pose_part(rib, pose, 'spine_03'), bone='spine_03', look='skin', smooth=60)
    KF.spine_knobs(C, 'skin', pose=pose, n=9, r=0.02, y_off=-0.028)
    KF.claws(C, 'claw', body=b, length=0.075, curl=0.5)
    KF.toe_claws(C, 'claw', length=0.03, r=0.006)

    # rags: wrap around the hips made from the body's own faces + hanging strips, sash over one shoulder
    nz = np.array([M.fbm(p, 9.0, 3.0) for p in b.v])
    hips = (b.wsum(['pelvis', 'thigh_l', 'thigh_r', 'spine_01']) > 0.5) & (b.v[:, 2] > 0.80 + 0.07 * nz) & (b.v[:, 2] < 1.06 + 0.03 * nz)
    C.add(KF.shell(b, hips, 0.011), look='rag')
    sash_d = np.abs((b.v[:, 0] * 0.62 + (b.v[:, 2] - 1.27) * -0.78))
    sash = (b.wsum(['spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'neck_01']) > 0.55) & (sash_d < 0.055 + 0.03 * nz) & (b.v[:, 2] > 1.0)
    C.add(KF.shell(b, sash, 0.012), look='rag')
    top, nr = arc_pts((0, 0.01, 0.99), 0.125, 0.10, -128, -52, 6)
    r1 = hang(top, 0.36, rows=6, out=nr, flare=0.02, rag=0.4, seed=2, wave=0.012, holes=0.3, tatter=0.25)
    M.weights_by(r1, r1.v[:, 2], [(0.66, {'thigh_l': 0.4, 'thigh_r': 0.4, 'pelvis': 0.2}), (0.98, {'pelvis': 1.0})])
    C.add(r1, look='rag')
    top, nr = arc_pts((0, 0.02, 0.99), 0.125, 0.115, 60, 125, 6)
    r2 = hang(top, 0.30, rows=5, out=nr, flare=0.03, rag=0.45, seed=7, wave=0.012, holes=0.3, tatter=0.25)
    M.weights_by(r2, r2.v[:, 2], [(0.70, {'thigh_l': 0.7, 'pelvis': 0.3}), (0.98, {'pelvis': 1.0})])
    C.add(r2, look='rag')
    return C, "Emaciated husk sculpted from the CC0 base body (weights preserved): sunken belly, geometric ribs/vertebra knobs, hanging jaw, long rigid claws, torn rags. No weapon."
