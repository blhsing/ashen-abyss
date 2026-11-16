"""boss_gravedigger 掘墓者・葛魯姆 — Grum the Gravedigger: a giant hunched sexton in a heavy stitched coat and hide mantle,
a burlap sack tied over his head with a hangman's cord, a chipped headstone chained to his back (the focal motif),
a corpse-light lantern, grave keys and a rope coil at the belt, grave-dirt on shoulders and hem, and an enormous iron
spade."""
import math
import numpy as np
import monlib as M
import looks as L
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_c as K
import kit_wisp as KW
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, band, unit, horn

SPINE = ['pelvis', 'spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r']


def shovel(C, obj='wpn_shovel'):
    P = []
    sh = tube(M.polyline([(0, -0.52, 0), (0.004, -0.2, 0.002), (-0.003, 0.3, -0.002), (0.002, 0.7, 0.002), (0, 0.98, 0)], 12), np.linspace(0.027, 0.03, 12), n=8)
    sh.rough(0.0025, 9.0, seed=2)
    P.append((sh, 'haft', 45))
    # D-grip at the butt
    P.append((tube([(-0.075, -0.62, 0), (-0.07, -0.56, 0), (-0.02, -0.50, 0), (0.02, -0.50, 0), (0.07, -0.56, 0), (0.075, -0.62, 0)], 0.016, n=6), 'iron', 40))
    P.append((tube([(-0.085, -0.625, 0), (0.085, -0.625, 0)], 0.021, n=7), 'haft', 45))
    P.append((tube([(0, -0.11, 0), (0, 0.0, 0), (0, 0.13, 0)], [0.034, 0.033, 0.034], n=8), 'wrap', 50))
    for y in (-0.30, 0.42, 0.80):
        P.append((torus(0.031, 0.007, n=10, m=4).rot('x', 90).move(0, y, 0), 'iron', 60))
    P.append((revolve([(0.03, 0.0), (0.04, 0.03), (0.046, 0.16), (0.05, 0.24), (0.02, 0.30)], 10).rot('x', -90).move(0, 0.86, 0), 'iron', 40))
    bl = KX.spade(Ln=0.60, w=0.42, dish=0.05, th=0.012, cols=11, rows=9, seed=3).move(0, 1.04, 0.012)
    P.append((bl, 'spade', 30))
    for sx in (-1, 1):                                                              # turned foot-rests + rivets
        P.append((tube([(sx * 0.07, 1.055, 0.01), (sx * 0.205, 1.085, -0.004)], 0.012, n=5), 'iron', 40))
        P.append((K.rivets([(sx * 0.03, 1.10 + 0.07 * k, 0.03 - 0.008 * k) for k in range(3)], [(0, 0, 1)] * 3, r=0.011), 'iron', 60))
    P.append((KX.lump(0.13, 0.17, 0.045, seed=4, amp=0.5, freq=14.0).move(0.03, 1.36, -0.02), 'dirt', 180))
    P.append((KX.lump(0.06, 0.07, 0.03, seed=6, amp=0.5, freq=18.0).move(-0.10, 1.22, -0.012), 'dirt', 180))
    for p, lk, sm in P:
        p.to_grip('r')
        C.add(p, look=lk, obj=obj, smooth=sm)


def build():
    C = M.Creature('boss_gravedigger', 40000)
    R = M.RIG
    C.look('coat', 'cloth', 'cloth_coat', color=(0.062, 0.055, 0.05), weave='wool', dirt=(0.13, 0.105, 0.075), hem_lo=0.15, hem_hi=0.75, hem_amt=0.95,
           fold=8.0, stain_amt=0.6, streak=0.5, fray=0.45, dust=0.5)
    C.look('patch', 'cloth', 'cloth_coat', color=(0.115, 0.085, 0.06), weave='burlap', dirt=(0.13, 0.105, 0.075), hem_lo=0.15, hem_hi=0.6, fold=14.0)
    C.look('sack', 'burlap', 'cloth_coat', color=(0.30, 0.235, 0.14), dirt=(0.10, 0.075, 0.05))
    C.look('rope', 'cloth', 'cloth_coat', color=(0.26, 0.20, 0.11), weave='burlap', weave_size=0.5, fold=40.0, fold_amt=0.1)
    C.look('void', 'flat', 'cloth_coat', color=(0.006, 0.006, 0.007), rough=1.0)
    C.look('hide', 'leather', 'leather_gear', color=(0.085, 0.058, 0.04), scuff_color=(0.21, 0.165, 0.12), scuff=0.6, rough=0.62)
    C.look('apron', 'leather', 'leather_gear', color=(0.05, 0.036, 0.03), scuff_color=(0.16, 0.12, 0.09), scuff=0.5, rough=0.5, dirt=(0.12, 0.095, 0.07),
           hem_lo=0.3, hem_hi=0.9)
    C.look('glove', 'leather', 'leather_gear', color=(0.07, 0.05, 0.035), scuff_color=(0.2, 0.16, 0.12), scuff=0.55, scale=70.0, stitch=0.0)
    C.look('boot', 'leather', 'leather_gear', color=(0.04, 0.03, 0.025), dirt=(0.12, 0.095, 0.07), hem_lo=0.02, hem_hi=0.4, rough=0.6)
    C.look('haft', 'wood', 'leather_gear', color=(0.085, 0.06, 0.04), axis=(0.152, 0.987, 0.05))
    C.look('wrap', 'leather', 'leather_gear', color=(0.05, 0.03, 0.025))
    C.look('stake', 'wood', 'leather_gear', color=(0.07, 0.055, 0.04), axis=(0.1, 0.3, 0.95))
    C.look('iron', 'metal', 'metal_iron', **dict(L.PRESET['rusty_iron'], amount=0.75, bump=0.7))
    C.look('spade', 'metal', 'metal_iron', color=(0.20, 0.19, 0.18), amount=0.6, edge=1.0, rough=0.38, bump=0.9)
    C.look('stone', 'rock', 'rock_grave', color=(0.15, 0.15, 0.14), ash=(0.30, 0.30, 0.27), scale=7.0, crack_w=0.02, ash_amt=0.5, bump=0.8)
    C.look('dirt', 'mud', 'rock_grave', color=(0.07, 0.055, 0.04), dry=(0.17, 0.14, 0.10), dry_amt=0.8, bump=1.1)
    C.look('moss', 'moss', 'rock_grave', color=(0.05, 0.06, 0.025), light=(0.11, 0.12, 0.05))
    C.look('glow', 'emis', 'emis_lantern', emit=(0.40, 0.66, 0.95), strength=2.2, mottle=0.7, base_mul=0.6, scale=30.0)
    C.look('eye', 'emis', 'emis_lantern', emit=(0.62, 0.84, 1.0), strength=2.2, mottle=0.1, base_mul=0.6)
    ref = KC.ref_body()

    # ------------------------------------------------------------------ gloved hands, forearms, boots (sculpted base body, weights kept)
    b = M.decimate(M.load_body('male'), 0.7)
    sp = {}
    for s in ('l', 'r'):
        sp['upperarm_' + s] = dict(scale=(1.0, 1.5, 1.5)); sp['lowerarm_' + s] = dict(scale=(1.0, 1.7, 1.7))
        sp['thigh_' + s] = dict(scale=(1.4, 1.4, 1.0)); sp['calf_' + s] = dict(scale=(1.55, 1.5, 1.0)); sp['foot_' + s] = dict(scale=(1.5, 1.3, 1.0))
        sp['ball_' + s] = dict(scale=(1.5, 1.3, 1.0))
    KA.hand_scale(sp, 1.42)
    M.smooth_verts(b, 14, 0.6, mask=np.clip(b.wsum(['foot_l', 'foot_r', 'ball_l', 'ball_r']) * 2.0, 0, 1))        # toes melt into boot leather
    M.pose_sculpt(b, sp)
    arms = b.wsum(R.chain('lowerarm_l') + R.chain('lowerarm_r')) > 0.5
    C.add(b.sub(b.vmask_faces(arms, 'any')), look='glove', smooth=180)
    legs = (b.wsum(['calf_l', 'calf_r', 'foot_l', 'foot_r', 'ball_l', 'ball_r']) > 0.5) & (b.v[:, 2] < 0.46)
    boots = b.sub(b.vmask_faces(legs, 'any')).inflate(0.016)
    boots.v[:, 2] = np.maximum(boots.v[:, 2], 0.0)
    C.add(boots, look='boot', smooth=180)
    for s, sx in (('l', 1), ('r', -1)):
        C.add(tube([(sx * 0.089, 0.135, 0.02), (sx * 0.089, 0.0, 0.02), (sx * 0.089, -0.125, 0.02)], 0.02, n=10, r2=[0.07, 0.092, 0.098], up=(0, 0, 1), power=3.0),
              bone='foot_' + s, look='boot', smooth=38)
        C.add(tube([(sx * 0.089, -0.115, 0.02), (sx * 0.089, -0.22, 0.02), (sx * 0.089, -0.295, 0.022)], [0.02, 0.02, 0.016], n=10, r2=[0.098, 0.094, 0.06], up=(0, 0, 1), power=3.0),
              bone='ball_' + s, look='boot', smooth=38)
        C.add(ellipsoid(0.085, 0.10, 0.06, n=10, m=5).move(sx * 0.089, -0.20, 0.062), bone='ball_' + s, look='boot', smooth=60)
        C.add(torus(0.105, 0.022, n=14, m=5, ry=0.11).move(sx * 0.089, 0.01, 0.45), bone='calf_' + s, look='boot', smooth=60)        # rolled boot tops
        for z in (0.16, 0.30):
            C.add(band((sx * 0.089, 0.025, z), 0.118, 0.124, 0.028, 0.008, n=14), bone='calf_' + s, look='hide', smooth=40)
            C.add(box(0.04, 0.012, 0.04, bevel=0.004).move(sx * 0.089 + sx * 0.02, -0.103, z), bone='calf_' + s, look='iron', smooth=30)

    # ------------------------------------------------------------------ the coat: heavy wool with real folds, stitched patches, leather apron
    secs = [(0.24, 0, 0.03, 0.415, 0.375), (0.50, 0, 0.03, 0.37, 0.335), (0.80, 0, 0.03, 0.335, 0.30), (1.0, 0, 0.02, 0.32, 0.29),
            (1.12, 0, 0.0, 0.325, 0.30), (1.25, 0, 0.0, 0.33, 0.285), (1.38, 0, 0.012, 0.31, 0.25), (1.47, 0, 0.02, 0.24, 0.19), (1.53, 0, 0.012, 0.125, 0.12)]
    n = 60
    pm = KX.pleat_mod(seed=3, ks=(5, 9, 14), amp=0.10, hem_z=0.24, top_z=1.02, base=0.12, twist=0.4)
    coat, Rg = KX.gown2(secs, n=n, mod=pm, rag=0.06, seed=5, spikes=0.2)
    coat = M.subsurf(coat, 1)
    coat.dent((0, 0.20, 1.42), 0.22, 0.10, direction=(0, 0.7, 0.7))                 # the hump under the headstone
    coat.dent((0, -0.30, 1.14), 0.22, 0.06, direction=(0, -1, -0.3))                # gut
    KX.crumple(coat, 0.009, 9.0, 3.0)
    KC.skin_cloth(coat, body=ref, smooth=5, pelvis_mix=0.35, mix_top=0.95, mix_bot=0.35)
    C.add(coat, look='coat')
    S = KB2.Surf(coat)
    j0 = KB2.col_of(n, -90)
    ap = KB2.strip(Rg, j0 - 6, j0 + 6, z0=0.36, z1=1.40, offset=0.014, rag=0.06, seed=2)
    KB2.skin_like(ap, coat)
    C.add(ap, look='apron')
    for (a, z0, z1, w, sd) in ((-35, 0.5, 0.82, 5, 1), (40, 0.62, 0.98, 4, 2), (150, 0.4, 0.75, 5, 3), (-150, 0.7, 1.0, 4, 4), (95, 1.05, 1.3, 3, 5)):
        jj = KB2.col_of(n, a)
        pt = KB2.strip(Rg, jj, jj + w, z0=z0, z1=z1, offset=0.011, seed=sd)
        KB2.skin_like(pt, coat)
        C.add(pt, look='patch')
        cs = [pt.v[k] for k in range(len(pt.v)) if (abs(pt.v[k][2] - z0) < 1e-3 or abs(pt.v[k][2] - z1) < 1e-3)]
        KB2.stitches(C, S, np.array(cs), 'rope', length=0.045, r=0.005, lift=0.016, allowed=SPINE + ['thigh_l', 'thigh_r'], every=2)
    for sx in (-1, 1):                                                              # apron edge seams, cross-stitched
        pts = KB2.seam_pts(S, (sx * 0.19, 0, 0.45), (sx * 0.17, 0, 1.36), 16, from_dir=(0, 1, 0))
        KB2.stitches(C, S, pts, 'rope', length=0.05, r=0.0055, lift=0.02, allowed=SPINE + ['thigh_l', 'thigh_r'], cross=True)
    sl = KC.sleeve(r_sh=0.125, r_el=0.108, r_cuff=0.125, n=16, x0=0.20, x1=0.66, droop=0.03, rings=11)
    KX.crumple(sl, 0.012, 14.0, 5.0, zscale=1.0)
    KC.skin_cloth(sl, body=ref, smooth=2)
    C.add(sl, look='coat', mirror=True)
    C.add(torus(0.118, 0.024, n=14, m=5).rot('y', 90).move(0.655, 0.068, 1.432), bone='lowerarm_l', look='patch', smooth=60, mirror=True)   # rolled cuffs
    # hide mantle over the shoulders, heavy seam down the back
    mt, G = KW.poncho(X=0.46, Yf=0.25, Yb=0.30, n=48, rows=7, neck=(0.150, 0.162), neck_c=(0.0, -0.012),
                      ridge=((0.0, 1.60), (0.09, 1.60), (0.18, 1.60), (0.28, 1.595), (0.5, 1.58)),
                      yc=((0.0, 0.012), (0.10, 0.03), (0.20, 0.066), (0.5, 0.068)),
                      depth_f=((0.0, 0.26), (0.1, 0.24), (0.2, 0.19), (0.3, 0.15), (0.46, 0.10)),
                      depth_b=((0.0, 0.34), (0.1, 0.31), (0.2, 0.23), (0.3, 0.16), (0.46, 0.10)), rag=0.16, seed=6)
    mt.dent((0, 0.22, 1.50), 0.22, 0.09, direction=(0, 0.7, 0.7))
    KX.crumple(mt, 0.008, 11.0, 2.0, zscale=1.0)
    KC.skin_cloth(mt, body=ref, smooth=3)
    C.add(mt, look='hide')
    S2 = KB2.Surf(M.join([coat, mt]))
    pts = KX.surf_path(S2, [(0.0, 0.34, 1.60), (0.0, 0.42, 1.48), (0.0, 0.45, 1.36)], 0.0, 9)
    KB2.stitches(C, S2, pts, 'rope', length=0.06, r=0.006, lift=0.006, allowed=SPINE, cross=True)

    # ------------------------------------------------------------------ belt, corpse-light lantern, keys, rope coil
    C.add(band((0, 0.008, 1.03), 0.338, 0.31, 0.075, 0.016, n=28), bone='pelvis', look='hide', smooth=40)
    C.add(box(0.11, 0.022, 0.10, bevel=0.01).move(0.03, -0.312, 1.03), bone='pelvis', look='iron', smooth=30)
    hk = np.array([0.30, -0.24, 0.99])
    cage, glow = KB2.lantern(h=0.24, r=0.075, bars=6, n=10)
    C.add(cage.move(hk), bone='pelvis', look='iron', smooth=50)
    C.add(glow.move(hk), bone='pelvis', look='glow', smooth=80)
    C.add(KX.key_ring(5, 0.05, seed=2).rot('z', 60).move(-0.20, -0.275, 0.985), bone='pelvis', look='iron', smooth=40)
    C.add(KX.rope_coil(0.13, 0.018, 4).rot('z', 78).move(-0.345, 0.03, 0.90), bone='pelvis', look='rope', smooth=60)

    # ------------------------------------------------------------------ the sack hood, hangman's cord, two holes
    hd, _ = KX.sack_hood(c=(0.0, -0.02, 1.74), rx=0.142, ry=0.158, rz=0.15, n=22, seed=4, skirt=0.05)
    C.add(hd, bone='Head', look='sack', smooth=180)
    Sh = KB2.Surf(hd)
    C.add(torus(0.150, 0.013, n=16, m=5, ry=0.166).rot('x', 6).move(0.0, -0.02, 1.632), bone='Head', look='rope', smooth=60)
    C.add(ellipsoid(0.026, 0.022, 0.03, n=8, m=5).move(0.06, -0.185, 1.628), bone='Head', look='rope', smooth=60)                    # the knot
    C.add(tube(M.polyline([(0.06, -0.19, 1.62), (0.075, -0.27, 1.50), (0.065, -0.315, 1.30), (0.08, -0.33, 1.12)], 8), 0.013, n=6), bone='spine_03', look='rope', smooth=60)
    for sx, r in ((1, 0.031), (-1, 0.023)):
        hit, _ = Sh.front(sx * 0.056, 1.762)
        C.add(ellipsoid(r, 0.012, r * 1.25, n=9, m=5).rot('z', sx * 8).move(hit + (0, 0.005, 0)), bone='Head', look='void')
        C.add(ellipsoid(0.0075, 0.006, 0.0075, n=6, m=4).move(hit + (-sx * 0.004, -0.004, -0.004)), bone='Head', look='eye')
    KB2.stitches(C, Sh, KB2.seam_pts(Sh, (-0.075, 0, 1.675), (0.075, 0, 1.668), 8, from_dir=(0, 1, 0)), 'rope', bone='Head', length=0.034, r=0.0045, lift=0.004)
    KB2.stitches(C, Sh, KB2.seam_pts(Sh, (0.105, 0, 1.66), (0.075, 0, 1.86), 7, from_dir=(0, 1, 0)), 'rope', bone='Head', length=0.04, r=0.0045, lift=0.004, cross=True)

    # ------------------------------------------------------------------ the headstone chained to his back, grave stakes, dirt
    st, inc = KX.headstone(w=0.52, h=0.88, th=0.11, seed=3)
    Ms = np.eye(4); Ms[:3, :3] = M.rot_axis('z', 5) @ M.rot_axis('x', -12) @ M.rot_axis('z', 180); Ms[:3, 3] = (0.02, 0.46, 0.98)
    C.add(st.tf(Ms), bone='spine_03', look='stone', smooth=32)
    C.add(inc.tf(Ms), bone='spine_03', look='void', smooth=20)
    tp = lambda x, y, z: (Ms @ np.array([x, y, z, 1.0]))[:3]
    for k, (x, z, r) in enumerate(((-0.12, 0.90, 0.10), (0.16, 0.80, 0.07), (0.02, 0.92, 0.06))):
        C.add(KX.lump(r, r * 0.6, r * 0.5, seed=k, amp=0.4, freq=20.0).move(tp(x, 0.0, z)), bone='spine_03', look='moss', smooth=180)
    for z in (0.26, 0.60):                                                         # chains round the slab
        loop = [tp(0.29 * math.cos(a), 0.085 * math.sin(a), z + 0.05 * math.cos(a)) for a in np.linspace(0, 2 * math.pi, 17)]
        C.add(KX.chain_rigid(loop, link=0.075, r=0.012), bone='spine_03', look='iron', smooth=60)
    for sx in (-1, 1):                                                             # ... over the shoulders and crossed on the chest
        gp = [tp(sx * 0.22, -0.02, 0.62), (sx * 0.27, 0.16, 1.62), (sx * 0.24, -0.14, 1.56), (sx * 0.08, -0.33, 1.34), (-sx * 0.16, -0.34, 1.12),
              (-sx * 0.33, -0.10, 1.02), (-sx * 0.30, 0.22, 1.02), tp(-sx * 0.24, -0.02, 0.26)]
        path = KX.surf_path(S2, gp, 0.016, 34)
        KX.chain_on(C, path, 'iron', S2, SPINE, link=0.075, r=0.012)
    for k, (dx, ln, rz) in enumerate(((0.33, 0.80, 7), (-0.34, 0.72, -9))):
        sk = KX.plank(ln, 0.085, 0.03, seed=k, warp=0.012).rot('x', 90)
        sk.v[sk.v[:, 2] < 0.12, :2] *= 0.25                                         # sharpened foot
        sk.rot('y', rz).rot('x', -12).move(dx, 0.36, 0.86)
        C.add(sk, bone='spine_03', look='stake', smooth=25)
    for (c, r, bn, sd) in (((0.30, 0.10, 1.57), (0.12, 0.11, 0.045), 'clavicle_l', 1), ((-0.31, 0.06, 1.57), (0.10, 0.12, 0.04), 'clavicle_r', 2),
                           ((0.12, 0.30, 1.50), (0.09, 0.08, 0.04), 'spine_03', 3)):
        C.add(KX.lump(r[0], r[1], r[2], seed=sd, amp=0.5, freq=16.0).move(c), bone=bn, look='dirt', smooth=180)

    shovel(C)
    KX.hunch_all(C, {'spine_01': dict(rot=('x', 7)), 'spine_02': dict(rot=('x', 11)), 'spine_03': dict(rot=('x', 14)), 'neck_01': dict(rot=('x', 10)),
                     'Head': dict(rot=('x', -20)), 'clavicle_l': dict(rot=('x', -12)), 'clavicle_r': dict(rot=('x', -12))})
    return C, ("Giant hunched sexton: heavy wool coat with modelled pleats, burlap patches and a leather apron sewn on with geometry "
               "stitches, hide mantle, burlap sack hood tied with a cord (two dark holes with pin-point lights), gloved hands and boots "
               "from the CC0 base body; a chipped headstone chained to the back with rigid-link chains crossing the chest, grave stakes, "
               "corpse-light lantern (emis_lantern), key ring and rope coil at the belt, dirt clods; wpn_shovel: enormous dished iron "
               "spade with D-grip.")
