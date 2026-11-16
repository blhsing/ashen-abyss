"""boss_bishop 腐骨主教・瑪爾薩 — Marsa the Bone-rot Bishop: a tall skeletal prelate. A two-horned mitre of bone and tarnished
gilt has fused to her skull; a rotted cope with gilt orphreys hangs open over a bare ribcage in which a green soul-fire
burns (the focal motif, echoed in her eyes, the mitre jewel and the crook of her crozier); a crimson stole, a stained alb
to the floor, and charms of bone, bells and medals hanging from stole, cincture and crozier."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_b as KB2
import kit_c as K
import kit_bones as KB
import kit_wisp as KW
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, band, unit, horn, hang, arc_pts, sheet

TILT = 58.0


def charm(kind, s=1.0):
    """small hanging charm, top at the origin, hanging along -Z: returns [(Part, look)]"""
    if kind == 'bell':
        b, cl = KB2.bell(h=0.07 * s, R=0.034 * s, n=10)
        return [(b, 'gilt'), (cl, 'gilt')]
    if kind == 'bone':
        return [(KB.seg(np.zeros(3), np.array([0.004, 0, -0.09 * s]), 0.006 * s, 0.0055 * s, 1.7, 1.6, n=5), 'bone')]
    if kind == 'medal':
        return [(K.disc(0.028 * s, 0.006, n=10, bevel=0.002).move(0, 0, -0.03 * s), 'gilt'), (torus(0.008, 0.0025, n=6, m=3).rot('x', 90), 'gilt')]
    sk = ellipsoid(0.03 * s, 0.036 * s, 0.032 * s, n=8, m=5).move(0, 0, -0.035 * s)
    return [(sk, 'bone'), (join([ellipsoid(0.008 * s, 0.006, 0.009 * s, n=5, m=3).move(sx * 0.012 * s, -0.031 * s, -0.032 * s) for sx in (-1, 1)]), 'void')]


def crozier(C, obj='wpn_crozier'):
    P = []
    ys = np.linspace(-0.78, 1.0, 13)
    sh = tube(np.stack([0.006 * np.sin(ys * 9), ys, 0.004 * np.cos(ys * 7)], axis=1), 0.0165 + 0.0035 * np.abs(np.sin(ys * 9.5)), n=7)
    P.append((sh, 'shaft', 50))
    for y in (-0.5, -0.18, 0.22, 0.58, 0.92):                                       # knuckle joints of the bone shaft, gilt collars
        P.append((ellipsoid(0.026, 0.02, 0.026, n=8, m=5).move(0.006 * math.sin(y * 9), y, 0.004 * math.cos(y * 7)), 'shaft', 60))
        P.append((torus(0.0225, 0.0045, n=8, m=4).rot('x', 90).move(0.006 * math.sin(y * 9), y + 0.028, 0.004 * math.cos(y * 7)), 'gilt', 60))
    P.append((revolve([(0.0, -0.10), (0.012, -0.05), (0.02, 0.0), (0.014, 0.02)], 6).rot('x', -90).move(0, -0.78, 0), 'gilt', 40))
    P.append((revolve([(0.02, 0.0), (0.034, 0.02), (0.03, 0.06), (0.04, 0.085), (0.024, 0.11)], 8).rot('x', -90).move(0, 0.98, 0), 'gilt', 40))
    ck, path, eye = KX.crook(R0=0.165, R1=0.04, turns=1.3, r0=0.026, r1=0.01, n=7, pts=28)
    P.append((ck.move(0, 1.07, 0), 'gilt', 50))
    path = path + (0, 1.07, 0); eye = eye + np.array([0, 1.07, 0])
    cen = path[8:20].mean(0)
    for k in range(4, 22, 3):                                                       # crockets along the outer curve
        d = unit(path[k] - cen)
        P.append((horn([path[k] + d * 0.012, path[k] + d * 0.05 + (0, 0.012, 0), path[k] + d * 0.075 + (0, 0.03, 0)], 0.011, n=4), 'shaft', 40))
    P.append((KX.lump(0.05, 0.05, 0.036, seed=3, amp=0.3, freq=26.0).move(eye), 'soul', 80))
    for k in range(5):
        a = 1.26 * k
        P.append((horn([eye, eye + (0.05 * math.cos(a), 0.05 * math.sin(a) + 0.02, 0.012 * (k % 2 - 0.5)), eye + (0.075 * math.cos(a + 0.5), 0.085 * math.sin(a + 0.5) + 0.05, 0)],
                       0.016, n=4), 'soul', 40))
    for k, (i, ln, kind) in enumerate(((13, 0.16, 'bell'), (16, 0.26, 'skull'), (11, 0.10, 'medal'), (18, 0.20, 'bone'))):   # charms under the volute
        p0 = path[i] + (0, -0.012, 0.0)
        p1 = p0 + np.array([0.0, -ln, 0.012 * (k % 2 * 2 - 1)])
        P.append((KX.chain_rigid([p0, p1], link=0.022, r=0.0034, n=5, m=3), 'gilt', 60))
        for q, lk in charm(kind, 1.1):
            P.append((q.rot('x', -90).move(p1), lk, 50))
    for p, lk, sm in KX.to_hand(P, TILT, 'r'):
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('boss_bishop', 40000)
    R = M.RIG
    C.look('bone', 'bone', 'bone_prelate', color=(0.56, 0.52, 0.40), stain=(0.21, 0.17, 0.09), crack_amt=0.75, pore=0.45, dirt_lo=0.0, dirt_hi=0.4)
    C.look('mitre', 'bone', 'bone_prelate', color=(0.60, 0.57, 0.47), stain=(0.25, 0.20, 0.11), crack=11.0, crack_amt=0.9, blotch=5.0)
    C.look('shaft', 'bone', 'bone_prelate', color=(0.42, 0.37, 0.27), stain=(0.13, 0.10, 0.06), crack_amt=0.5)
    C.look('void', 'flat', 'bone_prelate', color=(0.008, 0.008, 0.008), rough=1.0)
    C.look('cope', 'cloth', 'cloth_vest', color=(0.075, 0.042, 0.075), weave='wool_weave', dirt=(0.10, 0.09, 0.07), hem_lo=0.1, hem_hi=0.9, hem_amt=0.9,
           fold=7.0, fold_amt=0.9, stain_amt=0.75, streak=0.6, fray=0.6, dust=0.5)
    C.look('lining', 'cloth', 'cloth_vest', color=(0.20, 0.035, 0.035), dirt=(0.09, 0.07, 0.06), hem_lo=0.3, hem_hi=1.0, fold=12.0, stain_amt=0.7, fray=0.6)
    C.look('alb', 'cloth', 'cloth_vest', color=(0.36, 0.33, 0.26), dirt=(0.085, 0.07, 0.05), hem_lo=0.02, hem_hi=0.6, hem_amt=0.95, fold=9.0, stain_amt=0.75,
           streak=0.65, fray=0.5, weave='linen')
    C.look('orph', 'metal', 'cloth_vest', **dict(L.PRESET['gold_tarnish'], amount=0.62, bump=1.0, hammer=70.0))
    C.look('gilt', 'metal', 'metal_gilt', **dict(L.PRESET['gold_tarnish'], amount=0.5))
    C.look('soul', 'emis', 'emis_soul', emit=(0.30, 1.0, 0.30), strength=3.0, mottle=0.6, base_mul=0.6, scale=24.0)
    C.look('eye', 'emis', 'emis_soul', emit=(0.45, 1.0, 0.40), strength=3.0, mottle=0.1, base_mul=0.7)

    def add(p, bone, look, smooth=None, mirror=False):
        return C.add(p, bone=bone, look=look, smooth=smooth, mirror=mirror)

    # ------------------------------------------------------------------ the skeleton (legs stay inside the alb)
    KX.add_skull_seated(C, 'bone', 'eye', jaw_open=16.0, scale=1.06)
    KB.add_spine(C, 'bone', thick=1.2)
    KB.add_ribcage(C, 'bone', thick=1.35, wide=1.04, deep=1.06)
    KB.add_shoulder_girdle(C, 'bone', 1.2)
    KB.add_arms(C, 'bone', thick=1.25, claws=1.5)
    KB.add_pelvis(C, 'bone', 1.1)
    cc = np.array([0.0, -0.015, 1.325])                                               # the soul-fire in the ribcage
    add(KX.lump(0.058, 0.05, 0.075, seed=2, amp=0.4, freq=24.0), 'spine_03', 'soul', 80).move(cc)
    for k in range(7):
        a = 0.9 * k
        tip = cc + (0.05 * math.cos(a), 0.035 * math.sin(a) - 0.01, 0.10 + 0.03 * (k % 3))
        add(horn([cc + (0.03 * math.cos(a), 0.02 * math.sin(a), 0.0), (cc + tip) / 2 + (0.03 * math.sin(a * 2), 0, 0), tip], 0.022, n=5), 'spine_03', 'soul', 50)
    for k in range(4):                                                               # threads of it leaking down the spine
        a = 1.6 * k + 0.5
        add(tube(M.polyline([cc + (0, 0.02, -0.05), cc + (0.03 * math.cos(a), 0.03, -0.14), cc + (0.02 * math.cos(a * 2), 0.045, -0.24 - 0.02 * k)], 5),
                 [0.011, 0.009, 0.007, 0.005, 0.003], n=4), 'spine_02', 'soul', 40)

    # ------------------------------------------------------------------ alb: stained linen to the floor, real pleats, cincture with charms
    secs = [(0.0, 0, 0.03, 0.335, 0.305), (0.30, 0, 0.03, 0.25, 0.225), (0.62, 0, 0.03, 0.185, 0.165), (0.92, 0, 0.035, 0.145, 0.125), (1.06, 0, 0.035, 0.118, 0.10),
            (1.13, 0, 0.035, 0.105, 0.09)]
    alb, Rg = KX.gown2(secs, n=44, mod=KX.pleat_mod(seed=8, ks=(6, 10, 15), amp=0.14, hem_z=0.0, top_z=1.08, base=0.22, twist=0.6), rag=0.09, seed=3, spikes=0.25)
    alb = M.subsurf(alb, 1)
    KX.crumple(alb, 0.006, 11.0, 1.0)
    alb = KX.rot_holes(alb, 0.6, 0.07, seed=2)
    KC.skin_cloth(alb, smooth=5, pelvis_mix=0.45, mix_top=0.95, mix_bot=0.3)
    C.add(alb, look='alb')
    sl = KC.sleeve(r_sh=0.062, r_el=0.052, r_cuff=0.10, n=14, x0=0.17, x1=0.60, droop=0.07, rings=10, bell=1.6)
    sl.v[-14:, 0] += KC.ragged(14, 0.06, 4) - 0.03
    KX.crumple(sl, 0.008, 14.0, 4.0, zscale=1.0)
    sl = KX.rot_holes(sl, 2.0, 0.0, seed=1)
    KC.skin_cloth(sl, smooth=2)
    C.add(sl, look='alb', mirror=True)
    loop, tails = KC.rope_belt(z=1.075, rx=0.122, ry=0.106, cy=0.035, r=0.011, tail=0.46, side=1)
    add(loop, 'pelvis', 'lining', 60)
    M.weights_by(tails, tails.v[:, 2], [(0.62, {'thigh_l': 0.6, 'pelvis': 0.4}), (1.0, {'pelvis': 1.0})])
    C.add(tails, look='lining')
    for k, (a, ln, kind) in enumerate(((-150, 0.20, 'skull'), (-128, 0.12, 'bell'), (-60, 0.16, 'bone'), (-35, 0.24, 'medal'), (-20, 0.14, 'bell'))):
        ar = math.radians(a)
        p0 = np.array([0.128 * math.cos(ar), 0.035 + 0.112 * math.sin(ar), 1.07])
        p1 = p0 + np.array([0.03 * math.cos(ar), 0.035 * math.sin(ar), -ln])
        add(KX.chain_rigid([p0, p1], link=0.022, r=0.0034, n=5, m=3), 'pelvis', 'gilt', 60)
        for q, lk in charm(kind, 1.2):
            add(q.rot('z', a + 90).move(p1), 'pelvis', lk, 50)

    # ------------------------------------------------------------------ cope: open in front over the ribs, orphreys down both edges, long ragged back
    mt, G = KW.poncho(X=0.44, Yf=0.175, Yb=0.215, n=52, rows=13, neck=(0.085, 0.095), neck_c=(0.0, 0.012),
                      ridge=((0.0, 1.578), (0.07, 1.562), (0.13, 1.528), (0.20, 1.505), (0.46, 1.492)),
                      depth_f=((0.0, 0.98), (0.1, 0.95), (0.2, 0.62), (0.3, 0.36), (0.44, 0.22)),
                      depth_b=((0.0, 1.44), (0.1, 1.40), (0.2, 0.95), (0.3, 0.46), (0.44, 0.24)), rag=0.10, seed=7)
    Go = KX.open_front(G, gap=4)
    Go = Go * np.array([1.0, 1.0, 1.0])
    cope = sheet(Go)
    cope.smooth = 180.0
    nrm, side = KX.grid_normals(Go, closed=False)
    t = np.linspace(0, 1, Go.shape[0])[:, None]
    fold = 0.022 * t * np.sin(np.arange(Go.shape[1])[None, :] * 1.9 + 0.7) + 0.012 * t * np.sin(np.arange(Go.shape[1])[None, :] * 3.1)
    cope.v += (nrm * fold[:, :, None]).reshape(-1, 3)                                 # hanging folds, deeper towards the hem
    Gc = cope.v.reshape(Go.shape).copy()
    cope = KX.rot_holes(cope, 1.0, 0.22, seed=5)
    KC.skin_cloth(cope, smooth=3, pelvis_mix=0.6, mix_top=1.0, mix_bot=0.45)
    C.add(cope, look='cope')
    ref = sheet(Gc); KC.skin_cloth(ref, smooth=3, pelvis_mix=0.6, mix_top=1.0, mix_bot=0.45)
    nc = Gc.shape[1]
    for (j0, j1) in ((0, 2), (nc - 3, nc - 1)):                                       # the orphreys: gilt bands with bosses
        o = sheet(Gc[:, j0:j1 + 1] + nrm[:, j0:j1 + 1] * 0.006)
        o.smooth = 180.0
        C.add(KB2.skin_like(o, ref, smooth=0), look='orph')
        for i in range(1, Gc.shape[0] - 1, 2):
            bs = ellipsoid(0.016, 0.016, 0.008, n=7, m=3)
            KA_n = nrm[i, j0 + 1]
            bs.tf(M.rot_to((0, 0, 1), KA_n)).move(Gc[i, j0 + 1] + KA_n * 0.008)
            C.add(KB2.skin_like(bs, ref, smooth=0), look='soul' if i % 6 == 3 else 'gilt', smooth=60)
    jb = nc // 2                                                                     # the shield on the back: rings of tarnished thread
    for i, r in ((3, 0.07), (5, 0.10), (7, 0.07)):
        rg = torus(r, 0.009, n=16, m=4)
        rg.tf(M.rot_to((0, 0, 1), nrm[i, jb])).move(Gc[i, jb] + nrm[i, jb] * 0.008)
        C.add(KB2.skin_like(rg, ref, smooth=0), look='orph', smooth=60)
    add(K.disc(0.05, 0.016, n=12, bevel=0.005).move(0.0, -0.09, 1.50), 'spine_03', 'gilt', 38)          # the morse (clasp) + its jewel
    add(ellipsoid(0.022, 0.012, 0.022, n=8, m=4).move(0.0, -0.103, 1.50), 'spine_03', 'soul', 80)
    for sx in (-1, 1):
        add(tube([(0.0, -0.092, 1.50), (sx * 0.06, -0.085, 1.515), (sx * 0.11, -0.06, 1.52)], 0.008, n=5), 'spine_03', 'gilt', 50)

    # ------------------------------------------------------------------ stole: two crimson bands with gilt bars and bone fringes
    for sx in (-1, 1):
        top = np.array([(sx * 0.078, -0.096, 1.47), (sx * 0.112, -0.088, 1.468), (sx * 0.146, -0.072, 1.462)])
        st = hang(top, 0.96, rows=10, out=np.tile((0, -1, 0), (3, 1)), flare=0.07, rag=0.0, seed=3 + sx, wave=0.012, curve=0.6)
        st.v[:, 1] -= 0.035 * np.exp(-((st.v[:, 2] - 1.33) / 0.10) ** 2)            # rides over the ribs
        M.weights_by(st, st.v[:, 2], [(0.5, {'thigh_l' if sx > 0 else 'thigh_r': 0.55, 'pelvis': 0.45}), (0.95, {'pelvis': 1.0}), (1.1, {'spine_01': 1.0}),
                                      (1.22, {'spine_02': 1.0}), (1.36, {'spine_03': 1.0})])
        C.add(st, look='lining')
        Gs = st.v.reshape(11, 3, 3)
        for i in (2, 4, 6, 8, 10):
            bar = tube([Gs[i, 0] + (0, -0.006, 0), Gs[i, 2] + (0, -0.006, 0)], 0.007, n=4)
            C.add(KB2.skin_like(bar, st, smooth=0), look='orph', smooth=40)
        for k in range(3):
            p0 = Gs[10, k]
            for q, lk in charm('bone', 0.9 + 0.2 * k):
                C.add(KB2.skin_like(q.move(p0), st, smooth=0), look=lk, smooth=50)

    # ------------------------------------------------------------------ the mitre fused to the skull, lappets, jewel
    mi, ridge = KX.mitre(c=(0.0, -0.012, 1.775), rx=0.092, ry=0.108, H=0.34, n=22, rows=8, side=0.5, pinch=0.6)
    mi = mi.subdiv(1)
    mi.dent((0.06, -0.07, 1.95), 0.05, -0.014); mi.dent((-0.05, 0.08, 1.98), 0.05, -0.015)
    add(mi, 'Head', 'mitre', 50)
    add(band((0.0, -0.012, 1.795), 0.096, 0.112, 0.04, 0.008, n=22), 'Head', 'gilt', 38)
    rd = np.array(ridge)
    add(tube(rd[1:] + (0, -0.004, 0), 0.008, n=4, r2=0.014, up=(0, -1, 0)), 'Head', 'gilt', 30)
    add(ellipsoid(0.02, 0.012, 0.026, n=8, m=5).move(rd[3] + (0, -0.008, 0)), 'Head', 'soul', 80)
    for sx in (-1, 1):                                                               # bone has crept up from the temples
        add(horn(M.polyline([(sx * 0.082, -0.03, 1.74), (sx * 0.10, -0.045, 1.82), (sx * 0.095, -0.07, 1.90), (sx * 0.065, -0.085, 1.97)], 7), 0.016, n=5, r_tip=0.003),
            'Head', 'bone', 50)
        add(horn(M.polyline([(sx * 0.07, 0.06, 1.74), (sx * 0.098, 0.07, 1.83), (sx * 0.08, 0.085, 1.92)], 6), 0.014, n=5, r_tip=0.003), 'Head', 'bone', 50)
        top = np.array([(sx * 0.028, 0.092, 1.80), (sx * 0.062, 0.088, 1.80)])
        lp = hang(top, 0.52, rows=7, out=np.tile((0, 1, 0), (2, 1)), flare=0.07, rag=0.0, seed=8 + sx, wave=0.015, curve=0.7)
        M.weights_by(lp, lp.v[:, 2], [(1.42, {'spine_03': 1.0}), (1.60, {'neck_01': 0.5, 'Head': 0.5}), (1.74, {'Head': 1.0})])
        C.add(lp, look='lining')
        for q, lk in charm('bell', 0.8):
            C.add(KB2.skin_like(q.move(lp.v[-1] + (0, 0, 0.004)), lp, smooth=0), look=lk, smooth=50)

    crozier(C)
    return C, ("Tall skeletal prelate on the kit_bones skeleton (legs omitted inside the alb): two-horned bone mitre with gilt band, ridge "
               "and soul-jewel fused to the skull by creeping bone, open cope with fold geometry, rot holes, gilt orphreys with bosses and "
               "ring embroidery, crimson stole with gilt bars and bone fringe, pleated stained alb, cincture hung with bell / skull / "
               "medal / bone charms, green soul-fire in the bare ribcage (emis_soul); wpn_crozier: jointed bone shaft, gilt volute with "
               "crockets, soul-fire in its eye and hanging charms, tilted %d deg in the fist." % int(TILT))
