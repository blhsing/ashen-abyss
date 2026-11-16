"""mon_mire_hag 沼巫 — bent old swamp-witch: a wide, flat woven basket-hat dripping moss over a deep hunch, a veil of long
stringy hair hiding the face (two dim yellow-green eyes behind it), a cloak of layered reed blades over wet eel-skin,
a mud-heavy burlap dress, fish skeletons and swollen bladders at the rope belt, and a crooked root-wood staff whose
forked top cradles a glowing fungus cap."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_wisp as KW
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, hang, arc_pts, torus, horn, unit

TILT = 58.0


def staff(C, H, obj='wpn_staff'):
    P = []
    path = M.polyline([(0, -0.60, 0), (0.014, -0.34, 0.008), (-0.012, -0.02, -0.008), (0.012, 0.36, 0.012), (-0.016, 0.66, -0.008),
                       (0.006, 0.90, 0.004)], 16)
    rr = np.linspace(0.0125, 0.021, 16) * (1 + 0.16 * np.sin(np.linspace(0, 17, 16)))
    sh = tube(path, rr, n=7)
    sh.rough(0.003, 14.0, seed=2)
    P.append((sh, 'wood', 50))
    for (y, r) in ((-0.21, 0.021), (0.25, 0.026), (0.58, 0.029)):                          # knots / burls
        P.append((ellipsoid(r, r * 1.3, r * 0.9, n=7, m=5).move(float(np.interp(y, path[:, 1], path[:, 0])), y, 0.004), 'wood', 60))
    # forked crown of roots cradling the cap
    yc = 1.08
    for k, (a, ln, rd) in enumerate(((20, 0.26, 0.105), (135, 0.24, 0.10), (255, 0.28, 0.108), (320, 0.14, 0.07))):
        ar = math.radians(a)
        d = np.array([math.cos(ar), 0, math.sin(ar)])
        k2 = rd / 0.105
        rt = KA.root([(0.004, 0.88, 0.0), d * 0.07 * k2 + (0, 0.95, 0), d * 0.15 * k2 + (0, 1.04, 0), d * 0.152 * k2 + (0, 1.10, 0),
                      d * 0.122 + (0, 1.12 + ln * 0.14, 0)], 0.016, 0.0035, n=5, wob=0.006, seed=3 + k, pts=9)
        P.append((rt, 'wood', 50))
    cap, gill = KB2.fungus_cap(R=0.118, h=0.062, n=16, lobes=0.07, seed=2)
    stalk = tube([(0, 0, -0.14), (0.004, 0, -0.07), (0, 0, -0.012)], [0.02, 0.016, 0.03], n=7)
    cap.rough(0.004, 22.0, seed=4)
    rimg = torus(0.117, 0.006, n=14, m=4).move(0, 0, -0.004)
    spots = []
    rng = np.random.default_rng(3)
    for k in range(12):                                                                    # lit weeping sores on the cap, denser at the rim
        a = 2.4 * k + rng.uniform(-0.6, 0.6); u = min(0.96, math.sqrt((k + 1.5) / 12.0) + rng.uniform(-0.08, 0.05))
        rr_, zz = 0.118 * u * 0.97, 0.062 * float(np.interp(u, [0, 0.3, 0.62, 0.9, 1.0], [1, 0.93, 0.7, 0.35, 0.0])) + 0.001
        sp = ellipsoid(0.012, 0.012 * rng.uniform(0.5, 1.0), 0.004, n=5, m=3).scale(rng.uniform(0.5, 2.3)).rot('z', rng.uniform(0, 180))
        sp.jitter(0.0025, seed=k)
        spots.append(sp.rot('y', 50 * u).rot('z', math.degrees(a)).move(
            rr_ * math.cos(a), rr_ * math.sin(a), zz))
    for p, lk in ((cap, 'cap'), (gill, 'gill'), (stalk, 'stalk'), (rimg, 'gill'), (join(spots), 'gill')):
        P.append((p.rot('x', -90).move(0, yc + 0.04, 0), lk, 60))
    for (y, a, r) in ((0.42, 40, 0.05), (0.50, 200, 0.036), (0.74, 300, 0.044)):          # bracket fungi up the shaft
        s = KA.shelf(r, r * 0.3, n=9).rot('x', -90).rot('y', a)
        x0 = float(np.interp(y, path[:, 1], path[:, 0]))
        P.append((s.move(x0, y, 0), 'gill', 60))
    # bladders and a fish skeleton tied under the crown, a toad skull lashed to the shaft
    for (a, ln, r) in ((70, 0.10, 0.030), (190, 0.16, 0.024), (300, 0.07, 0.027)):
        ar = math.radians(a)
        p0 = np.array([0.026 * math.cos(ar), 0.90, 0.026 * math.sin(ar)])
        p1 = p0 + np.array([0.035 * math.cos(ar), -ln, 0.035 * math.sin(ar)])
        P.append((KX.cord(p0, p1, 0.003), 'cordw', 60))
        P.append((KX.bladder(r).rot('x', -90).move(p1), 'bladder', 80))
    fb = KX.fishbone(0.19).rot('x', -90).rot('y', 35)
    P.append((fb.move(-0.03, 0.80, 0.022), 'bone', 50))
    sk, so = KX.toad_skull(0.042)
    for p, lk in ((sk, 'bone'), (so, 'dark')):
        P.append((p.rot('x', -90).move(0.0, 0.665, 0.034), lk, 60))
    P.append((torus(0.024, 0.0045, n=8, m=4).rot('x', 90).move(-0.006, 0.665, 0.0), 'cordw', 60))
    P.append((torus(0.022, 0.0045, n=8, m=4).rot('x', 90).move(0.004, 0.87, 0.0), 'cordw', 60))
    for p, lk, sm in KX.to_hand(P, TILT, 'r', H):
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('mon_mire_hag', 15000)
    R = M.RIG
    pose = {
        'spine_01': dict(rot=('x', 8)),
        'spine_02': dict(rot=('x', 15)),
        'spine_03': dict(rot=('x', 19)),
        'neck_01': dict(rot=('x', 14)),
        'Head': dict(rot=('x', -34)),
    }
    for s in ('l', 'r'):
        pose['clavicle_' + s] = dict(rot=('x', -14))
    H = KB2.Hunch(pose)
    C.look('skin', 'skin', 'skin_hag', color=(0.25, 0.285, 0.21), blotch=(0.13, 0.16, 0.115), vein=(0.06, 0.09, 0.07), vein_amt=0.9,
           wrinkle=1.8, sinew=1.5, sore_amt=0.5, hands_dark=0.55, dark=(0.045, 0.055, 0.04), feet_dirt=0.9, bump=0.75, dry=0.4)
    C.look('dress', 'cloth', 'cloth_reed', color=(0.105, 0.095, 0.062), weave='burlap', dirt=(0.045, 0.04, 0.028), hem_lo=0.0, hem_hi=0.75,
           hem_amt=0.95, fold=8.0, stain_amt=0.7, streak=0.6, fray=0.5, weave_amt=1.3)
    C.look('reedA', 'wood', 'cloth_reed', color=(0.30, 0.255, 0.115), axis=(0, 0.35, 0.94), grime_amt=0.9, bump=0.8)
    C.look('reedB', 'wood', 'cloth_reed', color=(0.17, 0.175, 0.075), axis=(0, 0.35, 0.94), grime_amt=0.9, bump=0.8)
    C.look('reedC', 'wood', 'cloth_reed', color=(0.115, 0.085, 0.045), axis=(0, 0.35, 0.94), grime_amt=0.9, bump=0.8)
    C.look('hat', 'cloth', 'cloth_reed', color=(0.24, 0.185, 0.085), weave='burlap', weave_size=2.6, weave_amt=2.4, dirt=(0.07, 0.085, 0.035),
           hem_lo=-2, hem_hi=-1, fold=30.0, fold_amt=0.15, stain_amt=0.6, dust=0.0)
    C.look('moss', 'moss', 'cloth_reed', color=(0.042, 0.062, 0.022), light=(0.115, 0.135, 0.04), dark=(0.014, 0.02, 0.01), clump=46.0, tips=0.45)
    C.look('hair', 'fur', 'cloth_reed', color=(0.075, 0.08, 0.065), tip=(0.26, 0.27, 0.22), scale=70.0)
    C.look('eel', 'leather', 'leather_eel', color=(0.028, 0.04, 0.034), scuff_color=(0.10, 0.13, 0.10), rough=0.3, scuff=0.25)
    C.look('cordw', 'leather', 'leather_eel', color=(0.12, 0.09, 0.05), stitch=0.0)
    ax = KX.weapon_axis(TILT, 'r', H)
    C.look('wood', 'wood', 'leather_eel', color=(0.085, 0.062, 0.04), axis=ax, bump=0.9)
    C.look('stalk', 'wood', 'leather_eel', color=(0.33, 0.31, 0.2), axis=ax)
    C.look('bone', 'bone', 'bone_fish', color=(0.60, 0.56, 0.42), stain=(0.22, 0.19, 0.09), crack_amt=0.4)
    C.look('dark', 'flat', 'bone_fish', color=(0.012, 0.012, 0.01), rough=1.0)
    C.look('cap', 'leather', 'leather_eel', color=(0.095, 0.075, 0.028), scuff_color=(0.20, 0.21, 0.075), rough=0.32, scuff=0.6, stitch=0.0, scale=60.0)
    C.look('gill', 'emis', 'emis_fungus', emit=(0.78, 1.0, 0.16), strength=2.4, mottle=0.3, base_mul=0.7)
    C.look('bladder', 'emis', 'emis_fungus', emit=(0.22, 0.33, 0.045), strength=2.4, mottle=0.9, base_mul=1.3, scale=55.0, rough=0.3)

    # ------------------------------------------------------------------ what shows of the body: head, forearms, hands, feet
    fb = KC.ref_body('female')
    b = M.decimate(M.load_body('female'), 0.5)
    hv = b.v[b.wsum(['Head']) > 0.6]
    nose = hv[np.argmin(hv[:, 1])].copy()
    KF.gaunt_face(b, 1.2)
    b.dent(nose, 0.026, 0.034, direction=(0, -1, -0.55))                    # long hooked nose
    b.dent(nose + (0, 0.03, -0.075), 0.03, 0.022, direction=(0, -1, -0.3))  # jutting chin
    KW.stretch_fingers(b, 1.28, 1.15)
    arms = R.chain('lowerarm_l') + R.chain('lowerarm_r')
    keep = (b.wsum(['Head', 'neck_01']) > 0.5) | (b.wsum(arms) > 0.5) | (b.wsum(['upperarm_l', 'upperarm_r']) > 0.5) & (np.abs(b.v[:, 0]) > 0.36) \
        | ((b.wsum(['foot_l', 'foot_r', 'ball_l', 'ball_r', 'calf_l', 'calf_r']) > 0.5) & (b.v[:, 2] < 0.26))
    b = b.sub(b.vmask_faces(keep, 'any'))
    bp = dict(pose)
    for s in ('l', 'r'):
        bp['upperarm_' + s] = dict(scale=(1.0, 0.82, 0.82))
        bp['lowerarm_' + s] = dict(scale=(1.0, 0.76, 0.76))
    KA.hand_scale(bp, 1.1)
    M.pose_sculpt(b, bp)
    b.v[:, 2] = np.maximum(b.v[:, 2], 0.0)
    C.add(b, look='skin', smooth=180)
    KF.claws(C, 'bone', body=b, length=0.026, curl=0.5, n=4)
    eyes = join([ellipsoid(0.0085, 0.006, 0.0075, n=7, m=5).move(sx * 0.031, -0.090, 1.674) for sx in (-1, 1)])
    C.add(H.rigid(eyes, 'Head'), bone='Head', look='gill')

    # ------------------------------------------------------------------ dress: heavy burlap with real folds, mud-soaked ragged hem
    secs = [(0.035, 0, 0.02, 0.315, 0.285), (0.30, 0, 0.02, 0.262, 0.238), (0.62, 0, 0.025, 0.222, 0.205), (0.92, 0, 0.03, 0.19, 0.176),
            (1.07, 0, 0.026, 0.158, 0.146), (1.20, 0, 0.022, 0.162, 0.146), (1.33, 0, 0.02, 0.176, 0.152), (1.43, 0, 0.02, 0.16, 0.132),
            (1.505, 0, 0.012, 0.082, 0.086)]
    pm = KX.pleat_mod(seed=4, ks=(5, 8, 13), amp=0.13, hem_z=0.03, top_z=1.12, base=0.2, twist=0.5)
    rb, Rg = KX.gown2(secs, n=40, mod=pm, rag=0.07, seed=4, spikes=0.18)
    rb.dent((0, 0.17, 1.40), 0.17, 0.07, direction=(0, 0.75, 0.6))                              # the hump
    KX.crumple(rb, 0.007, 9.0, 2.0)
    KC.skin_cloth(rb, body=fb, smooth=5, pelvis_mix=0.3, mix_top=0.95, mix_bot=0.3)
    # a ragged apron of matted moss over the skirt (same folds, so it lies on the dress)
    ap, _ = KX.gown2([(0.46, 0, 0.022, 0.256, 0.236), (0.62, 0, 0.025, 0.236, 0.219), (0.92, 0, 0.03, 0.204, 0.19), (1.085, 0, 0.026, 0.171, 0.159)],
                     n=40, mod=pm, rag=0.26, seed=9, spikes=0.5)
    rng = np.random.default_rng(12)
    fc = ap.face_centers()
    ap = ap.sub((fc[:, 2] > 0.90) | (rng.uniform(0, 1, len(fc)) > 0.42 * (0.94 - fc[:, 2]) / 0.3))
    KB2.skin_like(ap, rb, smooth=1)
    H.skinned(rb)
    H.skinned(ap)
    C.add(rb, look='dress')
    C.add(ap, look='moss')
    sl = KC.sleeve(r_sh=0.072, r_el=0.06, r_cuff=0.082, x1=0.60, droop=0.035, rings=8, n=12)
    sl.v[sl.v[:, 0] > 0.59, 0] += KC.ragged(12, 0.05, 3)[: int((sl.v[:, 0] > 0.59).sum())] - 0.02
    KX.crumple(sl, 0.006, 16.0, 5.0, zscale=1.0)
    KC.skin_cloth(sl, body=fb, smooth=2)
    H.skinned(sl)
    C.add(sl, look='dress', mirror=True)

    # ------------------------------------------------------------------ eel-skin cloak + two tiers of reed thatch
    pn, G = KW.poncho(X=0.37, Yf=0.158, Yb=0.20, n=40, rows=6, neck=(0.082, 0.094), neck_c=(0.0, 0.012),
                      depth_f=((0.0, 0.24), (0.1, 0.22), (0.2, 0.15), (0.3, 0.11), (0.42, 0.07)),
                      depth_b=((0.0, 0.50), (0.1, 0.46), (0.2, 0.28), (0.3, 0.15), (0.42, 0.08)), rag=0.2, seed=5)
    pn.dent((0, 0.17, 1.42), 0.17, 0.085, direction=(0, 0.75, 0.6))
    KC.skin_cloth(pn, body=fb, smooth=3)
    H.skinned(pn)
    C.add(pn, look='eel')
    Gh = pn.v.reshape(G.shape)
    looks_r = ('reedA', 'reedB', 'reedA', 'reedC', 'reedB', 'reedA')
    rng = np.random.default_rng(7)
    t1 = KX.thatch(Gh, i0=1, width=0.036, lift=0.012, over=(0.05, 0.20), seed=1, vee=0.007, flare=0.03)
    t2 = KX.thatch(KX.half_shift(Gh), i0=0, i1=3, width=0.034, lift=0.024, over=(0.03, 0.10), seed=2, vee=0.007, flare=0.02)
    for bl in t1 + t2:
        KB2.skin_like(bl, pn, smooth=0)
        C.add(bl, look=looks_r[int(rng.integers(len(looks_r)))], smooth=30)

    # ------------------------------------------------------------------ rope belt, fish bones and bladders
    loop, tails = KC.rope_belt(z=1.085, rx=0.172, ry=0.158, cy=0.026, r=0.012, tail=0.34, side=-1)
    C.add(H.rigid(loop, 'spine_01'), bone='spine_01', look='cordw')
    H.rigid(tails, 'spine_01')
    M.weights_by(tails, tails.v[:, 2], [(0.70, {'thigh_r': 0.5, 'pelvis': 0.5}), (1.0, {'pelvis': 1.0}), (1.09, {'spine_01': 1.0})])
    C.add(tails, look='cordw')
    for (a, ln, fl) in ((-60, 0.07, 0.21), (-38, 0.12, 0.17), (-125, 0.05, 0.15)):
        ar = math.radians(a)
        p0 = H.point('spine_01', (0.176 * math.cos(ar), 0.026 + 0.162 * math.sin(ar), 1.08))
        p1 = p0 + np.array([0.02 * math.cos(ar), 0.03 * math.sin(ar), -ln])
        C.add(KX.cord(p0, p1, 0.003), bone='pelvis', look='cordw')
        C.add(KX.fishbone(fl).rot('z', a + 90).move(p1), bone='pelvis', look='bone', smooth=50)
    for (a, ln, r) in ((-150, 0.06, 0.028), (-20, 0.10, 0.022)):
        ar = math.radians(a)
        p0 = H.point('spine_01', (0.176 * math.cos(ar), 0.026 + 0.162 * math.sin(ar), 1.08))
        p1 = p0 + np.array([0.025 * math.cos(ar), 0.03 * math.sin(ar), -ln])
        C.add(KX.cord(p0, p1, 0.003), bone='pelvis', look='cordw')
        C.add(KX.bladder(r).move(p1), bone='pelvis', look='bladder', smooth=80)

    # ------------------------------------------------------------------ basket hat (built upright where the hunched head ends up)
    top = H.point('Head', (0, -0.02, 1.775))
    hat, ribs, rim = KX.basket_hat(R=0.27, h=0.085, n=26, ribs=11, seed=3)
    Mh = np.eye(4); Mh[:3, :3] = M.rot_axis('x', -9) @ M.rot_axis('y', -5); Mh[:3, 3] = top + (0, 0.085, -0.018)
    rimw = rim @ Mh[:3, :3].T + Mh[:3, 3]
    C.add(hat.tf(Mh), bone='Head', look='hat', smooth=60)
    C.add(ribs.tf(Mh), bone='Head', look='reedC', smooth=50)
    cen = rimw.mean(0)
    outs = rimw - cen; outs[:, 2] = 0
    outs /= np.linalg.norm(outs, axis=1)[:, None]
    rng = np.random.default_rng(6)
    sel = [k for k in range(len(rimw)) if rng.uniform() < 0.8]
    for s in KB2.strands(rimw[sel] + (0, 0, -0.006), outs[sel], rng.uniform(0.03, 0.45, len(sel)) ** 2 + 0.03, width=0.04, rows=3, seed=6, wave=0.012,
                         taper=0.9, cols=3, flare=0.01):
        C.add(s, bone='Head', look='moss', smooth=180)
    rng = np.random.default_rng(11)
    for k in range(7):                                                                    # moss cushions + tiny lit mushrooms on the hat
        a = rng.uniform(0, 6.28); r_ = rng.uniform(0.06, 0.25)
        r_ *= 0.9
        q = np.array([r_ * math.cos(a), r_ * math.sin(a), 0.085 * (1 - r_ / 0.27) + 0.006])
        ms = ellipsoid(rng.uniform(0.035, 0.065), rng.uniform(0.03, 0.055), rng.uniform(0.014, 0.024), n=7, m=4).rot('z', rng.uniform(0, 180))
        ms.rough(0.006, 30.0, seed=float(k))
        C.add(ms.move(q).tf(Mh), bone='Head', look='moss', smooth=80)
        if k < 3:
            cp, st = KA.mushroom(r=rng.uniform(0.014, 0.022), h=0.013, stalk=rng.uniform(0.018, 0.03), sr=0.005, n=7)
            off = q + (0.03, 0.01, 0.012)
            C.add(cp.move(off).tf(Mh), bone='Head', look='gill', smooth=60)
            C.add(st.move(off).tf(Mh), bone='Head', look='stalk', smooth=60)

    # ------------------------------------------------------------------ the hair veil (hangs straight down from the hunched head)
    hc = H.point('Head', (0, -0.018, 1.735))
    Rh = M.posed_point(H.pose, 'Head', (0, 0, 0))[1][:3, :3]
    anchors = []; outs = []; lens = []
    rng = np.random.default_rng(5)
    for k in range(46):
        a = 2 * math.pi * k / 46 + rng.uniform(-0.05, 0.05)
        rr = rng.uniform(0.86, 1.0)
        loc = np.array([0.090 * math.cos(a) * rr, 0.104 * math.sin(a) * rr, rng.uniform(-0.012, 0.03)])
        anchors.append(hc + Rh @ loc)
        o = np.array([math.cos(a), math.sin(a), 0.0])
        outs.append(o)
        front = max(0.0, -math.sin(a))
        lens.append(rng.uniform(0.26, 0.42) + 0.22 * front - 0.10 * max(0.0, math.sin(a)))
    for s in KX.hair(anchors, outs, lens, width=0.019, rows=7, seed=9, wave=0.014, flare=0.045, cols=2, low=0.4):
        C.add(s, look='hair', smooth=180)

    staff(C, H)
    return C, ("Hunched swamp-witch: head / forearms / long-fingered hands / feet of the CC0 female base body (weights kept), burlap dress with "
               "modelled pleats and a ragged mud hem, eel-skin cloak under two tiers of V-folded reed blades, flat woven basket-hat with moss "
               "fringe and small lit mushrooms (rigid on Head), hair veil over the face, fish skeletons + bladders at the belt; wpn_staff: "
               "crooked root-wood with a forked crown holding a glowing fungus cap (emis_fungus), tilted %d deg in the fist." % int(TILT))
