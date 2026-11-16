"""mon_rime_stalker 霜角獵者 — lean, hunched glacier predator: narrow waist under a deep keel of a chest, long head
slung low between crescent ice horns that sweep out and FORWARD, a sail of ice needles down the spine with a cold
seam glowing under it, and hands whose fingers end in long flat ice blades. Hide like cracked glacier. No weapon."""
import math
import numpy as np
import monlib as M
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_c as K
import kit_ice as KI
from monlib import V, tube, ellipsoid, box, join, horn, rock, unit

HS = np.array([0.84, 1.20, 0.90])      # anisotropic head scale (narrow, long, low)
JAW = 25.0


def build():
    C = M.Creature('mon_rime_stalker', 15000)
    C.look('hide', 'glacier', 'skin_glacier', color=(0.44, 0.60, 0.72), light=(0.74, 0.86, 0.93), deep=(0.012, 0.07, 0.19),
           dark_ice=(0.07, 0.20, 0.38), blotch=3.2, blotch_amt=0.8, scale=17.0, crack_w=0.05, ext_dark=0.8, ext_x0=0.48, ext_x1=0.80, dark=(0.02, 0.08, 0.20),
           frost_amt=0.45, rough=0.32, bump=1.0)
    C.look('maw', 'flat', 'skin_glacier', color=(0.008, 0.02, 0.05), rough=0.4)
    C.look('ice', 'crystal', 'rock_ice', color=(0.26, 0.50, 0.76), deep=(0.02, 0.10, 0.29), scale=10.0, crack=16.0, frost_amt=0.4,
           edge=0.4)
    C.look('blade', 'crystal', 'rock_ice', color=(0.44, 0.70, 0.92), deep=(0.10, 0.32, 0.58), scale=16.0, crack=34.0, frost_amt=0.2,
           edge=0.6, vein_amt=0.3)
    C.look('glow', 'emis', 'emis_frost', emit=(0.25, 0.68, 1.0), strength=3.0, mottle=0.3, base_mul=0.7, scale=30.0)
    R = M.RIG
    HH = R.H('Head')

    # ------------------------------------------------------------------ body
    b = M.load_body('male')
    b = M.decimate(b, 0.72)
    KF.gaunt_face(b, 1.3)
    mouth_c, gap = KF.open_jaw(b, JAW)
    b.dent(KF.FACE['nose'], 0.03, 0.024, direction=(0, 1, 0.25))                 # no nose: a flat wedge of a face
    b.dent((0, -0.11, 1.60), 0.062, 0.03, direction=(0, -1, -0.12))              # muzzle pushed forward
    b.dent(KF.FACE['brow'], 0.05, 0.013, direction=(0, -1, 0.25))                # brow shelf
    for sx in (1, -1):
        b.dent((sx * 0.088, -0.01, 1.66), 0.036, 0.022, direction=(-sx, 0, 0))   # no ears
    # fingers become blades: drop everything beyond the first phalanx (second for the thumb)
    distal = []
    for s in ('l', 'r'):
        for fg in ('index', 'middle', 'ring', 'pinky'):
            distal += ['%s_02_%s' % (fg, s), '%s_03_%s' % (fg, s), '%s_04_leaf_%s' % (fg, s)]
        distal += ['thumb_03_' + s, 'thumb_04_leaf_' + s]
    b = b.sub(~b.vmask_faces(b.wsum(distal) > 0.5, 'all'))
    rest = b.v.copy()

    pose = {
        'pelvis': dict(scale=(0.80, 0.80, 1.0)),
        'spine_01': dict(scale=(0.70, 0.68, 1.0), rot=('x', 4)),
        'spine_02': dict(scale=(0.80, 0.88, 1.0), rot=('x', 9)),
        'spine_03': dict(scale=(0.98, 1.12, 1.0), rot=('x', 15)),
        'neck_01': dict(scale=(0.76, 0.80, 1.0), rot=('x', 22)),
        'Head': dict(scale=tuple(HS), rot=('x', -34)),
    }
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(rot=('x', -18), scale=(1.0, 0.9, 0.9))
        pose['upperarm_' + s] = dict(scale=(1.0, 0.70, 0.70))
        pose['lowerarm_' + s] = dict(scale=(1.0, 0.80, 0.80))
        pose['thigh_' + s] = dict(scale=(0.78, 0.88, 1.0))
        pose['calf_' + s] = dict(scale=(0.66, 0.72, 1.0))
    KA.hand_scale(pose, 1.25)
    M.pose_sculpt(b, pose)
    dz = KA.ground(b)
    C.add(b, look='hide', smooth=180)
    H = KB2.Hunch(pose)
    vn = b.normals()

    def on(bone, p, look, smooth=None, pb=None):
        H.rigid(p, pb or bone)
        p.v[:, 2] -= dz
        return C.add(p, bone=bone, look=look, smooth=smooth)

    def head(p, look, smooth=None):
        p.v = HH + (p.v - HH) * HS
        return on('Head', p, look, smooth)

    def mark(cond, key):
        """index of a body vertex chosen on the REST shape (indices survive the sculpt)"""
        idx = np.where(cond)[0]
        if len(idx) == 0:
            return None
        return int(idx[np.argmax(key[idx])])

    def dom(i, allowed):
        return allowed[int(np.argmax(b.W[i, R.ids(allowed)]))]

    # ------------------------------------------------------------------ head: maw, ice fangs, four eyes, horns
    mo = np.array([0, -0.027, -0.003])                 # the muzzle dent moved the mouth
    head(ellipsoid(0.024, 0.02, max(gap * 0.5, 0.016), n=10, m=6).move(mouth_c + mo + np.array([0, 0.03, 0.0])), 'maw')
    hinge = np.array(KF.FACE['jaw_hinge'])
    Rj = M.rot_axis('x', JAW)
    for i, a in enumerate(np.linspace(-56, 56, 7)):
        ar = math.radians(a)
        p = np.array(KF.FACE['mouth']) + mo + np.array([0.025 * math.sin(ar), 0.03 - 0.022 * math.cos(ar), 0.001])
        ln = 0.046 if i in (1, 5) else (0.02 + 0.007 * (i % 2))
        head(M.cone_spike(0.0058 if i in (1, 5) else 0.0045, ln, n=4).rot('x', 180).move(p), 'blade', 20)
        if i in (0, 2, 4, 6):
            q = np.array(KF.FACE['mouth']) + mo + np.array([0.021 * math.sin(ar), 0.032 - 0.02 * math.cos(ar), -0.014])
            t = M.cone_spike(0.0045, 0.024, n=4).move(q)
            t.v = (t.v - hinge) @ Rj.T + hinge
            head(t, 'blade', 20)
    eyes = []
    for sx in (-1, 1):
        eyes.append(ellipsoid(0.014, 0.008, 0.008, n=8, m=5).rot('y', sx * -18).move(sx * 0.031, -0.093, 1.667))
        eyes.append(ellipsoid(0.010, 0.007, 0.006, n=7, m=5).rot('y', sx * -30).move(sx * 0.053, -0.078, 1.689))
    head(join(eyes), 'glow')
    for sx in (1, -1):
        pts = M.polyline([(sx * 0.058, 0.005, 1.730), (sx * 0.125, 0.020, 1.785), (sx * 0.180, -0.020, 1.832), (sx * 0.200, -0.100, 1.872),
                          (sx * 0.186, -0.180, 1.905), (sx * 0.150, -0.245, 1.930)], 11)
        t = np.linspace(0, 1, 11)
        r = 0.037 * (1 - t) ** 0.8 + 0.0025
        hn = tube(pts, r, n=6, r2=r * 0.8, up=(0, 0, 1))
        head(hn, 'ice', 20)
        ring = M.torus(0.040, 0.007, n=10, m=4).tf(M.rot_to((0, 0, 1), unit(pts[1] - pts[0]))).move(pts[0] + (pts[1] - pts[0]) * 0.55)
        head(ring, 'glow', 40)
        # a short back tine at the root of each horn
        head(KI.crystal((sx * 0.075, 0.03, 1.745), (sx * 0.5, 0.75, 0.55), 0.11, 0.02, n=5, tip=0.4), 'ice', 0)

    # ------------------------------------------------------------------ dorsal sail of ice needles + the cold seam under it
    SP = ['pelvis', 'spine_01', 'spine_02', 'spine_03', 'neck_01', 'Head']
    path = []
    for z in np.linspace(0.98, 1.60, 22):
        i = mark((np.abs(rest[:, 0]) < 0.03) & (np.abs(rest[:, 2] - z) < 0.022) & (rest[:, 1] > 0.02), rest[:, 1] - 3.0 * np.abs(rest[:, 0]))
        if i is not None and i not in path:
            path.append(i)
    seam = KA.ribbons(b, [path], width=0.034, height=0.007, sink=0.004, seed=2, taper=0.15, wmin=0.5)
    C.add(seam, look='glow')
    K_ = 10
    for k in range(K_):
        t = (k + 0.5) / K_
        i = path[int(round(t * (len(path) - 1)))]
        ln = 0.09 + 0.23 * math.sin(math.pi * t ** 0.85) ** 1.3
        d = unit(vn[i] * 0.55 + np.array([0.0, 0.45, 0.70]) + np.array([0.12 * math.sin(k * 2.3), 0, 0]))
        cr = KI.crystal(b.v[i] - vn[i] * 0.01, d, ln, 0.017 + 0.045 * ln, n=5, tip=0.3, sink=0.12, spin=k * 29)
        C.add(cr, bone=dom(i, SP), look='ice', smooth=0)
        if k % 2 == 0 and 0.15 < t < 0.9:          # smaller flanking needle
            sx = 1 if (k // 2) % 2 else -1
            d2 = unit(d + np.array([sx * 0.55, 0.1, -0.1]))
            c2 = KI.crystal(b.v[i] + np.array([sx * 0.025, 0, 0]) - vn[i] * 0.01, d2, ln * 0.55, 0.014 + 0.03 * ln, n=5, tip=0.35, sink=0.15,
                            spin=k * 17)
            C.add(c2, bone=dom(i, SP), look='ice', smooth=0)
    # skull crest between the horns
    for (y, ln) in ((-0.035, 0.07), (0.01, 0.10), (0.05, 0.085)):
        i = mark((np.abs(rest[:, 0]) < 0.022) & (np.abs(rest[:, 1] - y) < 0.022) & (rest[:, 2] > 1.72), rest[:, 2])
        if i is not None:
            C.add(KI.crystal(b.v[i] - vn[i] * 0.008, unit(vn[i] * 0.6 + np.array([0, 0.6, 0.5])), ln, 0.018, n=5, tip=0.35), bone='Head',
                  look='ice', smooth=0)
    # shoulder shards
    for sx, s in ((1, 'l'), (-1, 'r')):
        i = mark((np.abs(rest[:, 0] - sx * 0.165) < 0.03) & (np.abs(rest[:, 1] - 0.06) < 0.05) & (rest[:, 2] > 1.44), rest[:, 2])
        if i is not None:
            ax = unit(vn[i] * 0.5 + np.array([sx * 0.45, 0.45, 0.6]))
            for cr in KI.cluster(b.v[i] - vn[i] * 0.01, axis=ax, count=3, spread=30, h=(0.06, 0.15), r=(0.015, 0.024), radius=0.025,
                                 seed=5 + (sx > 0)):
                C.add(cr, bone='clavicle_' + s, look='ice', smooth=0)

    # ------------------------------------------------------------------ blade fingers
    LEN = dict(index=0.22, middle=0.265, ring=0.23, pinky=0.17, thumb=0.14)
    for s in ('l', 'r'):
        for fg, ln in LEN.items():
            bn = ('%s_02_%s' if fg == 'thumb' else '%s_01_%s') % (fg, s)
            a = R.H(bn); d = unit(R.T(bn) - a)
            _, G = M.posed_point(H.pose, bn, (0, 0, 0))
            pa = (G @ np.r_[a, 1.0])[:3] - np.array([0, 0, dz])
            pd = G[:3, :3] @ d
            pp = G[:3, :3] @ np.array([0.0, 0.0, -1.0])
            sel = b.W[:, R.idx[bn]] > 0.35
            if sel.sum() >= 3:
                sv = (b.v[sel] - pa) @ pd
                tip = float(sv.max())
                rad = float(np.linalg.norm((b.v[sel] - pa) - np.outer(sv, pd), axis=1)[sv > tip - 0.02].mean())
            else:
                tip = float(np.linalg.norm(R.T(bn) - a)) * 1.2; rad = 0.012
            base = pa + pd * (tip - 0.012)
            tal = KI.talon(ln, 0.042 * (ln / 0.265) ** 0.5, 0.012, curve=0.26, n=7, hook=1.7)
            KI.place_talon(tal, base, pd, pp)
            C.add(tal, bone=bn, look='blade', smooth=18)
            kn = rock(rad * 1.45, rad * 1.45, rad * 1.45, seed=30 + len(C.parts), n=12, jit=0.2).move(base + pd * 0.006)
            C.add(kn, bone=bn, look='ice', smooth=0)

    # ------------------------------------------------------------------ elbow and calf spurs, toe claws
    for sx, s in ((1, 'l'), (-1, 'r')):
        el = KI.talon(0.23, 0.052, 0.013, curve=0.28, n=6, hook=1.5)
        KI.place_talon(el, (sx * 0.452, 0.098, 1.441), (-sx * 0.45, 0.88, 0.12), (-sx * 1.0, -0.2, 0.0))
        on('lowerarm_' + s, el, 'blade', 18)
        on('lowerarm_' + s, rock(0.03, 0.03, 0.028, seed=50, n=12, jit=0.2).move(sx * 0.455, 0.098, 1.441), 'ice', 0)
        cf = KI.talon(0.17, 0.045, 0.012, curve=0.25, n=6, hook=1.5)
        KI.place_talon(cf, (sx * 0.089, 0.06, 0.43), (0.0, 0.80, 0.60), (0.0, -0.3, 1.0))
        on('calf_' + s, cf, 'blade', 18)
        # plates of clear ice standing off the forearm and the outer thigh
        for k, (x, ln) in enumerate(((0.55, 0.10), (0.63, 0.075))):
            on('lowerarm_' + s, KI.crystal((sx * x, 0.075, 1.468), (sx * 0.55, 0.35, 0.75), ln, 0.022, n=5, tip=0.35, spin=k * 40), 'ice', 0)
        for k, (z, ln) in enumerate(((0.80, 0.11), (0.70, 0.085))):
            on('thigh_' + s, KI.crystal((sx * 0.150, 0.01, z), (sx * 0.75, 0.25, 0.6), ln, 0.024, n=5, tip=0.35, spin=k * 50), 'ice', 0)
    KF.toe_claws(C, 'blade', length=0.065, r=0.011)
    return C, ("Lean hunched predator sculpted from the CC0 base body (weights preserved; distal finger faces removed). Fingers end in "
               "long flat ice blades bound rigidly to the FIRST phalanx (thumb: second), so in a fist they hook palm-wards instead of "
               "folding back into the wrist. Crescent ice horns sweeping forward, fangs, four eyes and horn-root rings rigid on Head; "
               "dorsal sail of ice needles rigid on the nearest spine bone; emis_frost seam along the spine (rides on the skin weights), "
               "eyes and horn rings; elbow / calf spurs; toe claws. No weapon.")
