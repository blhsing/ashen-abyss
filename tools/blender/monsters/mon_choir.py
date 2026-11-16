"""mon_choir 靜默唱詩者 — floating chorister of the Hushed Sanctum: a tall arched veil (cloth over a high comb) framing
a waxen, blindfolded face whose mouth is stitched shut, hands pressed together round the stem of a small brass bell,
bell-shaped vestments that thin into spectral trailing cloth, and a cold white halo ring standing behind the head.
The arms are part of the vestment sculpt (bound to the chest), so the figure keeps its prayer in every animation."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_flesh as KF
import kit_b as KB2
import kit_c as KC3
import kit_wisp as KW
from monlib import V, tube, ellipsoid, box, join, torus, revolve


def build():
    C = M.Creature('mon_choir', 15000)
    C.look('robe', 'cloth', 'cloth_vestment', color=(0.43, 0.44, 0.45), dirt=(0.16, 0.155, 0.15), hem_lo=0.45, hem_hi=0.95, hem_amt=0.65,
           fold=9.0, streak=0.35, stain_amt=0.4)
    C.look('veil', 'cloth', 'cloth_vestment', color=(0.64, 0.65, 0.67), dirt=(0.25, 0.24, 0.23), hem_lo=0.8, hem_hi=1.3, hem_amt=0.45,
           fold=14.0, streak=0.22, stain_amt=0.25, fold_shade=0.25)
    C.look('trim', 'cloth', 'cloth_vestment', color=(0.075, 0.075, 0.09), hem_lo=-2, hem_hi=-1, fold=12.0, stain_amt=0.3)
    C.look('thread', 'flat', 'cloth_vestment', color=(0.018, 0.014, 0.014), rough=0.9)
    C.look('skin', 'skin', 'skin_wax', color=(0.80, 0.76, 0.68), blotch=(0.60, 0.56, 0.53), vein=(0.36, 0.35, 0.42), vein_amt=0.3,
           sore_amt=0.0, rough=0.42, bump=0.3)
    C.look('trail', 'spectral', 'spectral_robe', color=(0.60, 0.66, 0.68), dark=(0.22, 0.27, 0.31), wisp=(0.82, 0.90, 0.92), fade_lo=0.1,
           fade_hi=0.8)
    C.look('bell', 'metal', 'metal_bell', **L.PRESET['gold_tarnish'])
    C.look('halo', 'emis', 'emis_halo', emit=(0.86, 0.93, 1.0), strength=5.0, mottle=0.05)

    # ---- waxen head + neck (skinned), blindfold, mouth sewn shut
    b = M.load_body('male')
    KF.gaunt_face(b, 0.9)
    hd = b.sub(b.vmask_faces(b.wsum(['Head', 'neck_01']) > 0.6, 'all'))
    hd.smooth = 180.0
    M.normalize_weights(hd)
    C.add(hd, look='skin')
    bf = KF.shell(hd, (hd.v[:, 2] > 1.648) & (hd.v[:, 2] < 1.698) & (hd.v[:, 1] < 0.04), 0.0045)
    C.add(bf, look='trim')
    fs = KB2.Surf(hd)
    mz = KF.FACE['mouth'][2] - 0.001
    pts = KB2.seam_pts(fs, (-0.026, 0.0, mz), (0.026, 0.0, mz), 6, from_dir=(0, 1, 0))      # ray from the front onto the lips
    KB2.stitches(C, fs, pts + np.array([0, -0.004, 0]), 'thread', bone='Head', length=0.03, r=0.0023, lift=0.003, cross=False)
    lip = tube(pts + np.array([0, -0.0008, 0]), 0.0016, n=4)
    C.add(lip, bone='Head', look='thread')

    # ---- vestment: bell-shaped robe, dark scapular sewn with a column of cross-stitches, cord
    secs = [(0.52, 0, 0.03, 0.215, 0.19), (0.72, 0, 0.03, 0.185, 0.165), (0.92, 0, 0.03, 0.158, 0.14), (1.08, 0, 0.025, 0.138, 0.122),
            (1.20, 0, 0.022, 0.150, 0.128), (1.34, 0, 0.02, 0.168, 0.138), (1.44, 0, 0.02, 0.170, 0.125), (1.50, 0, 0.012, 0.115, 0.105),
            (1.555, 0, 0.004, 0.08, 0.086)]
    n = 32
    rb, R = KB2.gown(secs, n=n, hem_z=0.52, pleat_top=1.05, pleat_amp=0.07, rag=0.10, seed=3)
    zstops = [(0.9, {'pelvis': 1.0}), (1.04, {'pelvis': 0.5, 'spine_01': 0.5}), (1.12, {'spine_01': 1.0}), (1.25, {'spine_02': 1.0}),
              (1.36, {'spine_03': 1.0}), (1.50, {'spine_03': 0.6, 'neck_01': 0.4}), (1.56, {'neck_01': 1.0})]
    M.weights_by(rb, rb.v[:, 2], zstops)
    C.add(rb, look='robe')
    jf = KB2.col_of(n, -90)
    st = KB2.strip(R, jf - 1, jf + 1, z0=0.56, z1=1.13, offset=0.008, rag=0.06, seed=2)
    M.weights_by(st, st.v[:, 2], zstops)
    C.add(st, look='trim')
    ssurf = KB2.Surf(st)
    spts = KB2.seam_pts(ssurf, (0.0, -0.5, 0.66), (0.0, -0.5, 1.02), 5)
    KB2.stitches(C, ssurf, spts, 'veil', length=0.05, r=0.0042, lift=0.004, cross=True, allowed=['pelvis', 'spine_01'])
    collar = tube(KW.ellipse_path((0, 0.004, 1.558), 0.082, 0.088, n=16, a0=0, a1=337.5), 0.012, n=5, closed=True)
    C.add(collar, bone='neck_01', look='trim', smooth=60)
    loop, tails = KC.rope_belt(z=1.10, rx=0.148, ry=0.132, cy=0.025, r=0.0095, tail=0.34, side=1)
    C.add(loop, bone='spine_01', look='trim')
    M.weights_by(tails, tails.v[:, 2], [(0.9, {'pelvis': 1.0}), (1.1, {'spine_01': 1.0})])
    C.add(tails, look='trim')

    # ---- spectral trailing cloth under the robe hem (pelvis only): the chorister floats
    secs2 = [(0.17, 0, 0.05, 0.125, 0.12), (0.30, 0, 0.04, 0.178, 0.164), (0.48, 0, 0.035, 0.19, 0.17), (0.66, 0, 0.03, 0.160, 0.143),
             (0.84, 0, 0.03, 0.130, 0.115)]
    tr, R2 = KB2.gown(secs2, n=n, hem_z=0.17, pleat_top=0.84, pleat_amp=0.10, rag=0.05, seed=12)
    M.weights_from(tr, {'pelvis': 1.0})
    C.add(tr, look='trail')
    # fringe of shreds from under the robe hem + a shorter ring low on the under-robe
    for k, (zc, rx, ry, cnt, l0, l1, sd) in enumerate(((0.545, 0.200, 0.176, 14, 0.28, 0.46, 5), (0.30, 0.178, 0.164, 9, 0.12, 0.22, 17))):
        for w in KB2.wisps((0, 0.032, zc), rx, ry, count=cnt, lmin=l0, lmax=l1, width=0.085, rows=7, seed=sd, swirl=14, flare=-0.04, wave=0.022):
            w.v[:, 2] = np.maximum(w.v[:, 2], 0.05)
            M.weights_from(w, {'pelvis': 1.0})
            C.add(w, look='trail')

    # ---- sleeves (static, on the chest): upper arms at the sides, forearms folded in to the sternum, cloth pouching at the elbows
    S = V(0.185, 0.025, 1.40); E = V(0.238, -0.02, 1.19); Wc = V(0.052, -0.192, 1.284)
    sl, ring = KW.bell_sleeve([S, V(0.226, 0.008, 1.31), E, V(0.165, -0.12, 1.218), Wc], 0.056, 0.043, hang_d=0.11, n=12, power=1.3,
                              pts=11, pouch=0.22)
    C.add(sl, bone='spine_03', look='robe', mirror=True)
    C.add(KW.ring_cap(ring, 0.96, push=(0.008, 0.008, 0.0)), bone='spine_03', look='trim', mirror=True)
    low = []
    for i in range(4, 11):                        # underside of the sleeve from above the elbow to the cuff
        rv = sl.v[i * 12:(i + 1) * 12]
        low.append(rv[np.argmin(rv[:, 2])] + V(0, 0, 0.014))
    low = np.array(low)
    tail = M.hang(low, np.linspace(0.36, 0.13, len(low)), rows=6, out=np.tile(M.unit((0.45, -0.9, 0.0)), (len(low), 1)), flare=0.035,
                  rag=0.16, seed=6, wave=0.014)
    C.add(tail, bone='spine_03', look='robe', mirror=True)
    wrist = V(0.017, -0.212, 1.292)
    hand = KW.praying_hand('male', wrist=wrist, fingers=(0.0, -0.33, 0.944), ratio=1.0)
    C.add(hand, bone='spine_03', look='skin', mirror=True)

    # ---- the bell: stem clasped between the palms, body hanging just below the heels of the hands
    crown = V(0.0, -0.226, 1.262)
    bl, cl = KB2.bell(h=0.12, R=0.054, n=14, thick=0.12)
    C.add(bl.move(crown), bone='spine_03', look='bell')
    cl.scale((1, 1, 1.22)).move(crown)
    C.add(cl, bone='spine_03', look='bell')
    stem = tube([crown + V(0, 0, -0.004), crown + V(0, -0.012, 0.035), crown + V(0, -0.03, 0.085)], [0.012, 0.008, 0.008], n=6)
    C.add(stem, bone='spine_03', look='bell', smooth=60)
    C.add(torus(0.016, 0.005, n=8, m=4).rot('y', 90).move(crown + V(0, -0.035, 0.1)), bone='spine_03', look='bell', smooth=60)

    # ---- tall veil over a high comb: arch seen from the front, sail seen from the side; open round the face, wings on the
    #      shoulders, long pointed back panel
    prof = [(2.056, 0, 0.040, 0.066, 0.014), (2.04, 0, 0.040, 0.122, 0.036), (2.00, 0, 0.036, 0.133, 0.048), (1.93, 0, 0.026, 0.127, 0.066),
            (1.86, 0, 0.010, 0.121, 0.09), (1.79, 0, -0.008, 0.118, 0.112), (1.70, 0, -0.012, 0.119, 0.117), (1.62, 0, -0.004, 0.126, 0.126),
            (1.52, 0, 0.005, 0.165, 0.146), (1.44, 0, 0.012, 0.25, 0.16), (1.33, 0, 0.02, 0.315, 0.18), (1.20, 0, 0.025, 0.335, 0.20),
            (1.00, 0, 0.035, 0.30, 0.22), (0.80, 0, 0.05, 0.25, 0.235)]

    def hem(th):       # open round the face, down to the elbows at the sides, two swallow-tails behind
        return 1.20 + 0.50 * KW.bump_deg(th, -90, 46) - 0.35 * (KW.bump_deg(th, 60, 24) + KW.bump_deg(th, 120, 24))

    vl, G = KW.drape(prof, hem, 2.056, n=40, rows=13, power=2.15, rag=0.02, seed=9, curve=1.3,
                     mod=lambda th, z: 1.0 + 0.07 * np.sin(np.radians(th) * 9.0 + 0.6) * M.smoothstep(1.42, 1.1, z))
    vstops = [(1.0, {'pelvis': 0.5, 'spine_01': 0.5}), (1.12, {'spine_01': 1.0}), (1.25, {'spine_02': 1.0}), (1.38, {'spine_03': 1.0}),
              (1.50, {'spine_03': 0.6, 'neck_01': 0.4}), (1.57, {'neck_01': 0.5, 'Head': 0.5}), (1.64, {'Head': 1.0})]
    M.weights_by(vl, vl.v[:, 2], vstops)
    C.add(vl, look='veil')
    C.add(KW.ring_cap(G[0], 1.0), bone='Head', look='veil', smooth=180)

    # ---- halo: two concentric cold rings standing behind the comb
    hl = join([KW.halo(R=0.262, r=0.012, n=30, m=5), KW.halo(R=0.222, r=0.0055, n=26, m=4)])
    C.add(hl.move(0.0, 0.165, 1.83), bone='Head', look='halo', smooth=60)

    return C, ("Floating support caster: waxen blindfolded head (base body, skinned Head/neck) with a cross-stitched mouth, framed by a tall "
               "arched veil (per-column hem, weighted Head -> spine by height); bell-shaped cloth_ vestment weighted by height to spine/pelvis; "
               "spectral_ trailing under-robe and wisps on the pelvis; emis_ double halo ring rigid on Head. DESIGN CHOICE: sleeves, praying hands "
               "and the brass bell are a static sculpt bound 100% to spine_03 (no mesh follows the arm or leg bones), so the hands stay clasped in "
               "every clip and casts read through body lean, bell and VFX rather than an arm gesture. Lowest cloth about 5-15 cm above the ground. "
               "No wpn_ object.")
