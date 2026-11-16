"""mon_cult_caster 寂滅祭司 — Hushed Order caster: dark robes + pale stole, tall bent cowl, staff with a ring of
extinguished candles, cold white sigil of the Order on the chest."""
import math
import numpy as np
import monlib as M
import kit_cloth as KC
import kit_b as KB2
from monlib import V, tube, ellipsoid, box, join, torus, revolve

TILT = 58.0


def staff(C, obj='wpn_staff'):
    parts = []   # (part, look)
    sh = tube(M.polyline([(0, -0.52, 0), (0.004, -0.2, 0.003), (-0.003, 0.3, -0.002), (0.003, 0.75, 0.002), (0, 1.04, 0)], 12),
              np.linspace(0.0135, 0.0165, 12), n=7)
    sh.rough(0.0025, 9.0, seed=2)
    parts.append((sh, 'wood'))
    parts.append((revolve([(0.012, 0), (0.02, 0.01), (0.021, 0.05), (0.014, 0.06)], 8).rot('x', -90).move(0, -0.56, 0), 'iron'))   # butt cap
    parts.append((revolve([(0.019, 0), (0.024, 0.012), (0.022, 0.07), (0.03, 0.085), (0.018, 0.1)], 8).rot('x', -90).move(0, 0.98, 0), 'iron'))
    for yy in (0.2, 0.62):
        parts.append((torus(0.0175, 0.005, n=8, m=4).rot('x', 90).move(0, yy, 0), 'iron'))
    R = 0.125
    yh = 1.17
    hoop = torus(R, 0.0085, n=18, m=5).rot('x', 90).move(0, yh, 0)
    parts.append((hoop, 'iron'))
    for k in range(3):
        a = math.radians(90 + 120 * k)
        e = np.array([R * math.cos(a), yh, R * math.sin(a)])
        arm = tube(M.polyline([(0, 1.03, 0), e * np.array([0.55, 0, 0.55]) + np.array([0, 1.06, 0]), e * np.array([0.95, 0, 0.95]) + np.array([0, 1.12, 0]), e], 7),
                   0.0065, n=5)
        parts.append((arm, 'iron'))
    hs = [0.07, 0.045, 0.085, 0.05, 0.065, 0.04]
    for k in range(6):
        a = math.radians(30 + 60 * k)
        c, wick = KC.candle(0.0155, hs[k], seed=10 + k, melt=0.7)
        pos = (R * math.cos(a), yh + 0.004, R * math.sin(a))
        parts.append((c.rot('x', -90).move(pos), 'wax'))
        parts.append((wick.rot('x', -90).move(pos), 'wick'))
    c, wick = KC.candle(0.02, 0.11, seed=3, melt=0.8)
    parts.append((c.rot('x', -90).move(0, 1.075, 0), 'wax'))
    parts.append((wick.rot('x', -90).move(0, 1.075, 0), 'wick'))
    # short chains with bone tokens hanging back down from the hoop
    for k, L in ((1, 0.11), (3, 0.16), (5, 0.08)):
        a = math.radians(60 * k)
        p0 = np.array([R * math.cos(a), yh - 0.006, R * math.sin(a)])
        for t, _ in KB2.links([p0, p0 + np.array([0, -L, 0])], link=0.026, r=0.0042):
            parts.append((t, 'iron'))
        tok = ellipsoid(0.012, 0.02, 0.007, n=6, m=4).move(p0 + np.array([0, -L - 0.02, 0]))
        parts.append((tok, 'wax'))
    for p, lk in parts:
        KB2.staff_tilt(p, TILT)
        p.to_grip('r')
        C.add(p, look=lk, obj=obj)


def build():
    C = M.Creature('mon_cult_caster', 15000)
    C.look('robe', 'cloth', 'cloth_robe', color=(0.075, 0.073, 0.08), dirt=(0.11, 0.10, 0.09), hem_lo=0.03, hem_hi=0.6, fold=9.0, streak=0.35)
    C.look('cowl', 'cloth', 'cloth_robe', color=(0.05, 0.05, 0.058), hem_lo=-2, hem_hi=-1, fold=11.0)
    C.look('stole', 'cloth', 'cloth_robe', color=(0.50, 0.47, 0.40), dirt=(0.16, 0.13, 0.10), hem_lo=0.5, hem_hi=1.0, hem_amt=0.7,
           stain_amt=0.6, streak=0.5)
    C.look('void', 'flat', 'cloth_robe', color=(0.006, 0.006, 0.008), rough=1.0)
    C.look('mask', 'bone', 'bone_mask', color=(0.66, 0.64, 0.58), stain=(0.30, 0.26, 0.21), crack=13.0, crack_amt=0.4, blotch=11.0)
    C.look('wax', 'wax', 'bone_mask', color=(0.60, 0.55, 0.42), soot=0.3)
    C.look('wick', 'flat', 'bone_mask', color=(0.02, 0.02, 0.02), rough=1.0)
    C.look('skin', 'skin', 'skin_ash', color=(0.29, 0.285, 0.275), blotch=(0.15, 0.14, 0.14), vein_amt=0.4)
    C.look('iron', 'metal', 'metal_staff', amount=0.45, color=(0.16, 0.16, 0.17), rough=0.45)
    C.look('wood', 'wood', 'metal_staff', color=(0.075, 0.055, 0.04), axis=(0, 1, 0))
    C.look('sigil', 'emis', 'emis_sigil', emit=(0.82, 0.92, 1.0), strength=4.0, mottle=0.1)

    # ---- robe (slimmer and longer than the cultist's) + pale stole bands derived from the same rings
    secs = [(0.025, 0, 0.02, 0.305, 0.28), (0.30, 0, 0.02, 0.252, 0.232), (0.62, 0, 0.022, 0.208, 0.195), (0.92, 0, 0.03, 0.172, 0.162),
            (1.08, 0, 0.025, 0.148, 0.136), (1.14, 0, 0.022, 0.152, 0.14), (1.27, 0, 0.02, 0.17, 0.152), (1.38, 0, 0.02, 0.19, 0.16),
            (1.45, 0, 0.02, 0.182, 0.14), (1.52, 0, 0.012, 0.10, 0.10)]
    n = 36
    rb, R = KB2.gown(secs, n=n, hem_z=0.025, pleat_top=1.0, pleat_amp=0.075, rag=0.04, seed=6)
    KC.skin_cloth(rb, smooth=5, pelvis_mix=0.28, mix_top=0.95, mix_bot=0.3)
    C.add(rb, look='robe')
    for j0, j1, sd in ((29, 31, 1), (23, 25, 2)):
        st = KB2.strip(R, j0, j1, z0=0.52, z1=1.47, offset=0.009, rag=0.05, seed=sd)
        KB2.skin_like(st, rb)
        C.add(st, look='stole')
    surf = KB2.Surf(rb)
    mk, hit = KB2.place_front(KB2.order_mark(H=0.185, depth=0.008), surf, 0.0, 1.265, lift=0.003)
    C.add(mk, bone='spine_03', look='sigil')
    # small repeated marks near the stole ends
    sl = KC.sleeve(r_sh=0.074, r_el=0.058, r_cuff=0.078, droop=0.03, x1=0.705)
    KC.skin_cloth(sl, smooth=2)
    C.add(sl, look='robe', mirror=True)
    Rg = M.RIG
    cuff = ellipsoid(0.02, 0.042, 0.042, n=8, m=5).move(Rg.H('hand_l') + V(-0.06, 0.004, -0.016))
    C.add(cuff, bone='lowerarm_l', look='void', mirror=True)

    # ---- tall cowl, mantle, void head, mask
    cw = KB2.tall_cowl(height=0.40, lean=0.34, tip_drop=0.17)
    cw.W = M.rigid_W(len(cw.v), 'Head')
    C.add(cw, look='cowl')
    cp = KC.capelet(z_top=1.59, z_bot=1.36, r_top=(0.094, 0.108), r_bot=(0.275, 0.185), rag=0.05, seed=8)
    KC.skin_cloth(cp, smooth=3)
    C.add(cp, look='cowl')
    C.add(ellipsoid(0.08, 0.086, 0.098, n=12, m=8).move(0, -0.012, 1.69), bone='Head', look='void')
    mc = np.array([0, -0.112, 1.668])
    mask = ellipsoid(0.056, 0.02, 0.10, n=16, m=9)
    t = np.clip(-mask.v[:, 2] / 0.10, 0, 1)
    mask.v[:, 0] *= (1 - 0.42 * t ** 1.4)
    mask.v[:, 1] += 1.1 * mask.v[:, 0] ** 2 / 0.056 - 0.004 * np.exp(-(mask.v[:, 0] / 0.012) ** 2)
    mask.move(mc).rot('x', 4, pivot=mc)
    C.add(mask, bone='Head', look='mask', smooth=70)

    # ---- hands and wrapped feet
    C.add(KC.body_piece(KC.hand_bones('l') + KC.hand_bones('r')), look='skin')
    bd = KC.ref_body()
    ft = bd.sub(bd.vmask_faces((bd.wsum(['foot_l', 'foot_r', 'ball_l', 'ball_r', 'calf_l', 'calf_r']) > 0.5) & (bd.v[:, 2] < 0.2), 'all'))
    ft.inflate(0.005)
    ft.v[:, 2] -= ft.v[:, 2].min()
    C.add(ft, look='cowl')

    # ---- rope belt (dark cord) with the knot on the right
    loop, tails = KC.rope_belt(z=1.09, rx=0.166, ry=0.152, cy=0.024, r=0.011, tail=0.42, side=-1)
    C.add(loop, bone='spine_01', look='stole')
    M.weights_by(tails, tails.v[:, 2], [(0.66, {'thigh_r': 0.6, 'pelvis': 0.4}), (1.0, {'pelvis': 1.0}), (1.09, {'spine_01': 1.0})])
    C.add(tails, look='stole')

    staff(C)
    return C, ("Hushed Order caster: dark gown with pale stole bands (derived from the gown rings), tall bent cowl rigid on Head, blank bone mask, "
               "geometry emis_ sigil (the Order's mark) on the chest; wpn_staff with an iron hoop of unlit candle stubs, tilted %d deg in the fist." % int(TILT))
