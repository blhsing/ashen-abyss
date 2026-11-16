"""boss_grand_hush 寂滅大導師・賽倫 — Seren, Grand Preceptor of the Hush: immensely tall in layered robes, black beneath and
ash-white above, a high standing collar behind a blank mask of smooth wax, a crown of dripping, snuffed candles whose wax
has run down mask and shoulders, a black stole set with cold white runes (the only light on him), and a great bronze
bell fused over his right fist with wax and iron (the focal motif and his weapon)."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_b as KB2
import kit_c as K
import kit_wisp as KW
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, band, unit, horn


def great_bell(C, obj='wpn_bell'):
    P = []
    P.append((tube([(0, -0.13, 0), (0, 0.0, 0), (0, 0.12, 0)], [0.024, 0.022, 0.03], n=8), 'iron', 40))
    P.append((revolve([(0.0, -0.03), (0.022, -0.022), (0.03, 0.0), (0.02, 0.02), (0.012, 0.03)], 8).rot('x', -90).move(0, -0.15, 0), 'iron', 40))
    bl, cl = KB2.bell(h=0.62, R=0.30, n=24, thick=0.10)
    bl = bl.subdiv(1)
    bl.dent((0.2, 0.1, -0.45), 0.09, -0.02); bl.dent((-0.15, -0.2, -0.3), 0.08, -0.016)
    y0 = 0.10
    P.append((bl.rot('x', 90).move(0, y0, 0), 'bell', 50))
    P.append((cl.rot('x', 90).move(0, y0, 0), 'iron', 50))
    for (z, r) in ((-0.10, 0.118), (-0.40, 0.19), (-0.585, 0.286)):                 # raised bands round the bell
        P.append((torus(r, 0.011, n=24, m=4).move(0, 0, z).rot('x', 90).move(0, y0, 0), 'bell2', 50))
    for k in range(4):                                                               # iron straps from the crown down to the fist
        a = math.radians(90 * k + 45)
        P.append((tube([(0.03 * math.cos(a), 0.02, 0.03 * math.sin(a)), (0.085 * math.cos(a), 0.075, 0.085 * math.sin(a)), (0.12 * math.cos(a), 0.16, 0.12 * math.sin(a))],
                       0.011, n=5), 'iron', 40))
    rng = np.random.default_rng(5)
    for k in range(7):                                                               # snuffed candle stubs welded to its shoulder by their own wax
        a = rng.uniform(0, 6.28); r = rng.uniform(0.07, 0.13)
        c, wick = KC.candle(0.016, rng.uniform(0.03, 0.09), seed=k, melt=0.9)
        pos = (r * math.cos(a), y0 + 0.012 + (r - 0.07) * 0.75, r * math.sin(a))
        P.append((c.rot('x', 90).move(pos), 'wax', 60)); P.append((wick.rot('x', 90).move(pos), 'wick', 40))
    for k in range(9):                                                               # wax runs down the bell
        a = rng.uniform(0, 6.28); ln = rng.uniform(0.15, 0.42)
        prof = lambda t: 0.30 * float(np.interp(t, [0.0, 0.14, 0.36, 0.64, 0.88, 1.0], [0.20, 0.44, 0.52, 0.62, 0.80, 1.0])) + 0.008
        ts = np.linspace(0.12, 0.12 + ln / 0.62, 5)
        P.append((tube([(prof(t) * math.cos(a), y0 + 0.62 * t, prof(t) * math.sin(a)) for t in ts], [0.014, 0.012, 0.011, 0.012, 0.016], n=5), 'wax', 60))
    for p, lk, sm in P:
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('boss_grand_hush', 40000)
    R = M.RIG
    C.look('black', 'cloth', 'cloth_robe', color=(0.03, 0.03, 0.036), weave='wool', dirt=(0.16, 0.15, 0.14), hem_lo=0.0, hem_hi=0.5, hem_amt=0.8, fold=7.0,
           fold_amt=0.9, streak=0.4, dust=0.5)
    C.look('white', 'cloth', 'cloth_robe', color=(0.50, 0.49, 0.45), weave='linen', dirt=(0.17, 0.16, 0.15), hem_lo=0.3, hem_hi=1.0, hem_amt=0.85, fold=8.0,
           fold_amt=0.9, stain_amt=0.6, streak=0.55, fray=0.4)
    C.look('stole', 'cloth', 'cloth_robe', color=(0.022, 0.022, 0.026), weave='wool_weave', hem_lo=-2, hem_hi=-1, fold=30.0, fold_amt=0.2)
    C.look('void', 'flat', 'cloth_robe', color=(0.006, 0.006, 0.008), rough=1.0)
    C.look('wax', 'wax', 'bone_wax', color=(0.66, 0.62, 0.50), soot=0.35)
    C.look('mask', 'wax', 'bone_wax', color=(0.72, 0.69, 0.60), soot=0.12)
    C.look('wick', 'flat', 'bone_wax', color=(0.02, 0.02, 0.02), rough=1.0)
    C.look('skin', 'skin', 'skin_ash', color=(0.33, 0.325, 0.315), blotch=(0.17, 0.16, 0.16), vein_amt=0.5, hands_dark=0.4)
    C.look('bell', 'metal', 'metal_bell', color=(0.20, 0.155, 0.085), rust=(0.10, 0.17, 0.14), rust2=(0.04, 0.07, 0.06), amount=0.5, edge_color=(0.62, 0.50, 0.28),
           rough=0.4, bump=0.9, hammer=24.0)
    C.look('bell2', 'metal', 'metal_bell', color=(0.27, 0.20, 0.10), rust=(0.10, 0.17, 0.14), rust2=(0.04, 0.07, 0.06), amount=0.35, edge_color=(0.7, 0.56, 0.3), rough=0.34)
    C.look('iron', 'metal', 'metal_bell', color=(0.15, 0.15, 0.16), amount=0.6, rough=0.5)
    C.look('rune', 'emis', 'emis_rune', emit=(0.85, 0.93, 1.0), strength=4.0, mottle=0.12)
    ref = KC.ref_body()

    # ------------------------------------------------------------------ black under-robe to the floor, deep pleats
    secs = [(0.0, 0, 0.02, 0.38, 0.345), (0.30, 0, 0.02, 0.30, 0.275), (0.62, 0, 0.022, 0.24, 0.222), (0.92, 0, 0.03, 0.195, 0.182),
            (1.08, 0, 0.025, 0.168, 0.152), (1.20, 0, 0.022, 0.176, 0.156), (1.33, 0, 0.02, 0.198, 0.168), (1.45, 0, 0.02, 0.19, 0.148), (1.53, 0, 0.012, 0.105, 0.105)]
    n = 48
    pm = KX.pleat_mod(seed=6, ks=(6, 10, 15), amp=0.15, hem_z=0.0, top_z=1.05, base=0.14, twist=0.4)
    rb, Rg = KX.gown2(secs, n=n, mod=pm, rag=0.035, seed=6, spikes=0.15)
    rb = M.subsurf(rb, 1)
    rb.v[:, 2] = np.maximum(rb.v[:, 2], 0.0)
    KC.skin_cloth(rb, body=ref, smooth=5, pelvis_mix=0.3, mix_top=0.95, mix_bot=0.3)
    C.add(rb, look='black')
    # ------------------------------------------------------------------ ash-white over-robe: open down the front, shorter, its own folds
    Ro = M.rings_z([(s[0], s[1], s[2], s[3] + 0.022, s[4] + 0.022) for s in secs[1:]], n, mod=KX.pleat_mod(seed=9, ks=(5, 8, 13), amp=0.13, hem_z=0.30, top_z=1.1, base=0.15))
    Ro[0, :, 2] += KC.ragged(n, 0.10, 4, 0.3) + 0.06
    jf = KB2.col_of(n, -90)
    Go = np.roll(Ro, -(jf + 4), axis=1)[:, : n - 7]
    ov = M.subsurf(M.sheet(Go), 1)
    KB2.skin_like(ov, rb, smooth=2)
    C.add(ov, look='white')
    S = KB2.Surf(M.join([rb, ov]))

    # ------------------------------------------------------------------ the stole: two black bands on the white, set with cold runes
    rng = np.random.default_rng(8)
    for jj in (jf - 7, jf + 5):
        st = KB2.strip(Ro, jj, jj + 2, z0=0.52, z1=1.47, offset=0.012, seed=2)
        KB2.skin_like(st, rb)
        C.add(st, look='stole')
        cols = st.v.reshape(-1, 3, 3)
        for i in range(1, len(cols) - 1):
            c = cols[i, 1]
            nr = unit(np.cross(cols[i, 2] - cols[i, 0], cols[i + 1, 1] - cols[i - 1, 1]))
            if nr[1] > 0:
                nr = -nr
            strokes = [box(0.008, 0.006, 0.05).rot('y', rng.uniform(-25, 25))]
            for q in range(int(rng.integers(1, 3))):
                strokes.append(box(0.007, 0.006, 0.028).rot('y', rng.choice([-55, 55, 90, -35])).move(rng.uniform(-0.012, 0.012), 0, rng.uniform(-0.018, 0.018)))
            g = join(strokes)
            g.tf(M.rot_to((0, -1, 0), nr)).move(c + nr * 0.004)
            C.add(KB2.skin_like(g, st, smooth=0), look='rune', smooth=20)
        tip = cols[0]
        for k in range(3):                                                           # weighted fringe: small bells that never ring
            b_, cl_ = KB2.bell(h=0.045, R=0.02, n=8)
            C.add(KB2.skin_like(b_.move(tip[k] + (0, -0.004, -0.004)), st, smooth=0), look='iron', smooth=50)

    # ------------------------------------------------------------------ wide sleeves (white over black), hands
    for (rr, lk, x1, dr) in ((1.0, 'black', 0.70, 0.03), (1.18, 'white', 0.60, 0.05)):
        sl = KC.sleeve(r_sh=0.08 * rr, r_el=0.066 * rr, r_cuff=0.115 * rr, n=16, x0=0.16, x1=x1, droop=dr, rings=10, bell=1.7)
        KX.crumple(sl, 0.008, 14.0, 3.0 + rr, zscale=1.0)
        KC.skin_cloth(sl, body=ref, smooth=2)
        C.add(sl, look=lk, mirror=True)
    C.add(KC.body_piece(KC.hand_bones('l') + KC.hand_bones('r')), look='skin')
    C.add(ellipsoid(0.02, 0.05, 0.05, n=8, m=5).move(R.H('hand_l') + V(-0.07, 0.004, -0.02)), bone='lowerarm_l', look='void', mirror=True)

    # ------------------------------------------------------------------ shoulder cape, high standing collar
    cp = KC.capelet(z_top=1.60, z_bot=1.28, r_top=(0.10, 0.112), r_bot=(0.34, 0.225), n=36, rows=7, rag=0.04, seed=8)
    KX.crumple(cp, 0.008, 12.0, 6.0)
    KC.skin_cloth(cp, body=ref, smooth=3)
    C.add(cp, look='white')
    C.add(band((0, 0.015, 1.30), 0.335, 0.222, 0.03, 0.006, n=36), bone='spine_03', look='stole', smooth=60)
    col = K.shell_arc([(1.52, 0, 0.02, 0.15, 0.14), (1.72, 0, 0.03, 0.20, 0.17), (1.92, 0, 0.05, 0.235, 0.19), (2.02, 0, 0.06, 0.22, 0.18)], -15, 195, n=14, th=0.012)
    col.v[:, 2] -= 0.10 * np.clip((0.02 - col.v[:, 1]) / 0.2, 0, 1) ** 1.5              # the collar sweeps down towards the front
    C.add(col, bone='spine_03', look='black', smooth=60)
    C.add(K.shell_arc([(1.53, 0, 0.02, 0.143, 0.133), (1.72, 0, 0.03, 0.192, 0.162), (1.90, 0, 0.05, 0.225, 0.18)], 0, 180, n=12, th=0.004), bone='spine_03', look='white', smooth=60)

    # ------------------------------------------------------------------ cowl, the wax mask, the crown of snuffed candles
    hd = KC.hood(c=(0, -0.01, 1.69), open_rx=0.092, open_rz=0.125, rx=0.124, rz=0.142, front=-0.150, back=0.11, tail=0.05, tail_drop=0.03)
    C.add(hd, bone='Head', look='black', smooth=180)
    C.add(ellipsoid(0.085, 0.09, 0.105, n=12, m=8).move(0, -0.012, 1.69), bone='Head', look='void')
    mc = np.array([0, -0.118, 1.672])
    mask = ellipsoid(0.066, 0.024, 0.115, n=18, m=11)
    t = np.clip(-mask.v[:, 2] / 0.115, 0, 1)
    mask.v[:, 0] *= (1 - 0.40 * t ** 1.4)
    mask.v[:, 1] += 1.0 * mask.v[:, 0] ** 2 / 0.066
    for sx in (-1, 1):
        mask.dent((sx * 0.026, -0.02, 0.022), 0.02, 0.006, direction=(0, 1, 0))       # the faintest hollows where eyes would be
    C.add(mask.move(mc).rot('x', 3, pivot=mc), bone='Head', look='mask', smooth=180)
    hc = np.array([0.0, -0.012, 1.835])
    C.add(torus(0.112, 0.012, n=18, m=5, ry=0.128).move(hc + (0, 0, -0.012)), bone='Head', look='iron', smooth=50)
    hs = [0.30, 0.14, 0.22, 0.09, 0.26, 0.12, 0.19, 0.07, 0.24, 0.15, 0.10]
    for k in range(11):
        a = math.radians(-90 + 360 * k / 11)
        c, wick = KC.candle(0.021, hs[k], seed=20 + k, melt=0.9)
        pos = hc + (0.112 * math.cos(a), 0.128 * math.sin(a), 0.0)
        C.add(c.move(pos), bone='Head', look='wax', smooth=60); C.add(wick.move(pos), bone='Head', look='wick', smooth=40)
        ln = 0.06 + 0.11 * ((k * 7) % 4) / 3.0                                         # wax that ran down the cowl before it set
        C.add(tube([pos + (0, 0, 0.004), pos + (0.012 * math.cos(a), 0.012 * math.sin(a), -ln * 0.5), pos + (0.02 * math.cos(a), 0.02 * math.sin(a), -ln)],
                   [0.011, 0.008, 0.012], n=5), bone='Head', look='wax', smooth=60)
    c, wick = KC.candle(0.03, 0.20, seed=3, melt=1.0)
    C.add(c.move(hc + (0, 0.01, 0.0)), bone='Head', look='wax', smooth=60); C.add(wick.move(hc + (0, 0.01, 0.0)), bone='Head', look='wick', smooth=40)
    C.add(KX.lump(0.10, 0.11, 0.03, seed=4, amp=0.3, freq=20.0).move(hc + (0, 0, -0.004)), bone='Head', look='wax', smooth=180)   # the pooled cap of wax
    for (x, ln) in ((0.03, 0.10), (-0.045, 0.16), (0.012, 0.05)):                      # drips down the mask
        C.add(tube([mc + (x, -0.024, 0.115), mc + (x * 1.05, -0.03, 0.115 - ln * 0.5), mc + (x * 1.1, -0.027, 0.115 - ln)], [0.009, 0.006, 0.01], n=5),
              bone='Head', look='wax', smooth=60)
    for sx in (-1, 1):                                                                 # and onto the shoulders
        C.add(KX.lump(0.06, 0.05, 0.016, seed=6 + sx, amp=0.3, freq=24.0).move(sx * 0.16, 0.02, 1.535), bone='spine_03', look='wax', smooth=180)

    # ------------------------------------------------------------------ the bell arm: wax and iron bind the bell to the forearm
    for t_, r_ in ((0.55, 0.10), (0.80, 0.115)):
        C.add(torus(r_, 0.013, n=14, m=4).rot('y', 90).move(R.H('lowerarm_r') * (1 - t_) + R.T('lowerarm_r') * t_ + (0, 0.003, -0.01)), bone='lowerarm_r', look='iron', smooth=50)
    C.add(KX.lump(0.085, 0.075, 0.07, seed=9, amp=0.35, freq=18.0).move(R.H('hand_r') + V(-0.05, 0.004, -0.01)), bone='hand_r', look='wax', smooth=180)

    great_bell(C)
    return C, ("Grand Preceptor: black pleated under-robe to the floor, ash-white over-robe open down the front, white shoulder cape and "
               "double sleeves, high standing collar, black cowl with a blank smooth wax mask, an iron circlet of eleven snuffed candles "
               "with wax run down cowl / mask / shoulders, two black stole bands set with geometry runes (emis_rune) and bell fringe; "
               "wpn_bell: a great dented bronze bell (clapper, raised bands, straps, candle stubs, wax runs) gripped at its crown in the "
               "right fist, iron rings and a wax mass binding the forearm.")
