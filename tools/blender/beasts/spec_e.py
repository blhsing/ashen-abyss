"""Rig E creatures (flyers). The hover height is inside the model (meta fly_height / hover_in_model)."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis
import bparts as P
import rig_e as RE
import rig_b as RB

BUILD = {}


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


def mirror_pts(pts, sg):
    return [V(q) * (sg, 1, 1) for q in pts]


# ================================================================================================ dusk_bat
@reg
def dusk_bat(bake=True):
    """Hearth-bat: two dish ears each larger than its head, flat leaf-nose, a deep-chested furry body, a snarling
    mouth of fangs, ember eye-shine, stub tail, wing membranes torn into scallops along the trailing edge.
    Built at 1 / 1.2 size and enlarged at the end (C.rescale)."""
    K = 1.2
    h = 1.45
    W = [(0.105, -0.06, h + 0.05), (0.36, 0.02, h + 0.10), (0.64, -0.12, h + 0.07), (0.95, 0.06, h + 0.0)]
    sk = RE.flyer_skel(body=((0, 0.16, h), (0, -0.13, h + 0.025)), head=((0, -0.14, h + 0.04), (0, -0.345, h + 0.015)),
                       jaw=((0, -0.195, h - 0.012), (0, -0.33, h - 0.04)), ears=((0.058, -0.175, h + 0.11), (0.215, -0.15, h + 0.47)),
                       wing=W, tail=[(0, 0.17, h - 0.01), (0, 0.28, h - 0.05)], legs=((0.06, 0.14, h - 0.06), (0.095, 0.245, h - 0.21)))
    C = B.Beast('mon_dusk_bat', sk)
    C.meta.update(family='dusk', rig='E', scale=1.0, game_speed=6.0)
    F = RE.Flyer(C, h)
    C.look('fur', 'bfur', 'hair_fur', color=(0.10, 0.08, 0.065), tip=(0.30, 0.25, 0.20), flow=(0, 1, -0.3), ash=0.3, scale=80.0)
    C.look('wing', 'membrane', 'skin_wing', color=(0.085, 0.06, 0.055), vein=(0.03, 0.015, 0.015), vein_scale=11.0, rough=0.45)
    C.look('ear', 'membrane', 'skin_wing', color=(0.21, 0.13, 0.115), vein=(0.08, 0.03, 0.03), vein_scale=20.0, rough=0.4)
    C.look('arm', 'skin', 'skin_wing', color=(0.18, 0.13, 0.12), blotch=(0.09, 0.065, 0.065), rough=0.5, sore_amt=0.0, scar=0.5)
    C.look('face', 'skin', 'skin_wing', color=(0.20, 0.13, 0.12), blotch=(0.10, 0.06, 0.06), rough=0.45, sore_amt=0.0, scar=0.3, wrinkle=2.0)
    C.look('maw', 'skin', 'skin_wing', color=(0.36, 0.08, 0.07), blotch=(0.16, 0.03, 0.03), rough=0.25, sore_amt=0.0, scar=0.0)
    C.look('teeth', 'bone', 'bone_teeth', color=(0.80, 0.76, 0.62), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('eye', 'emis', 'emis_eyes', emit=(1.0, 0.42, 0.06), strength=1.5, mottle=0.3, base_mul=0.6, scale=60.0)
    # ---- furry body: deep keeled chest, shoulders, round belly; the head is part of the same skin
    torso = P.blob((0, 0.035, h - 0.005), 0.112, 0.20, 0.118, n=14, m=8)
    B.wbones(torso, ['body'])
    chest = P.blob((0, -0.055, h - 0.012), 0.135, 0.13, 0.145, n=14, m=8)
    B.wbones(chest, ['body'])
    sh = [P.blob((sg * 0.095, -0.06, h + 0.05), 0.06, 0.075, 0.06, n=10, m=6) for sg in (1, -1)]
    for q in sh:
        B.wbones(q, ['body'])
    head = P.blob((0, -0.215, h + 0.05), 0.09, 0.105, 0.088, n=14, m=8)
    B.wbones(head, ['head'])
    snout = P.tubep([(0, -0.25, h + 0.035), (0, -0.315, h + 0.03), (0, -0.348, h + 0.024)], [0.05, 0.04, 0.027], [0.062, 0.052, 0.036], m=6, n=10)
    B.wbones(snout, ['head'])

    def face(b):
        for sg in (1, -1):
            b.dent((sg * 0.05, -0.285, h + 0.085), 0.03, 0.012)          # brow ridge
            b.dent((sg * 0.055, -0.30, h + 0.055), 0.022, -0.012)        # eye socket
            b.dent((sg * 0.045, -0.33, h + 0.01), 0.02, 0.006)           # snarling lip
        b.dent((0, -0.30, h + 0.075), 0.02, -0.006)
    body = P.organic([torso, chest, head, snout] + sh, voxel=0.008, tris=3200, smooth=6, sculpt=face)
    C.skin(body, 'fur')
    bvh = P.bvh_of(body)

    def ruff(c, n):
        return (c[:, 1] > -0.17) & (c[:, 1] < 0.16) & (n[:, 2] > -0.75)
    tf = P.tufts(body, 200, length=(0.035, 0.075), radius=(0.008, 0.015), seed=3, mask=ruff, flow=(0, 1, -0.2), lay=0.55, sides=3, jitter=0.3)
    C.skin(tf, 'fur', smooth=180)
    # ---- the face: bare muzzle patch, open snarling jaw, fangs, leaf nose, ember eyes
    C.rigid(P.blob((0, -0.318, h + 0.022), 0.05, 0.04, 0.036, n=10, m=6), 'head', 'face', smooth=180)                 # bare muzzle
    C.rigid(P.blob((0, -0.262, h + 0.0), 0.04, 0.07, 0.014, n=10, m=5), 'head', 'maw', smooth=180)                    # palate
    jd = unit(V(0, -0.135, -0.062))                                                                                    # built 14 degrees open
    j0 = V(0, -0.195, h - 0.012)
    jaw = P.tubep([j0 - jd * 0.02, j0 + jd * 0.07, j0 + jd * 0.135], [0.02, 0.017, 0.011], [0.046, 0.04, 0.027], m=7, n=10)
    C.rigid(jaw, 'jaw', 'face', smooth=180)
    C.rigid(P.tubep([j0 + jd * 0.01 + V(0, 0, 0.012), j0 + jd * 0.115 + V(0, 0, 0.012)], [0.006, 0.005], [0.03, 0.017], m=4, n=8), 'jaw', 'maw',
            smooth=180)
    jb = P.bvh_of(jaw)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(P.seated(bvh, (sg * 0.03, -0.338, h + 0.006), (sg * 0.12, -0.2, -1), 0.06, 0.009, curl=(0, 1, 0), seg=3), 'head', 'teeth', smooth=50)
        C.rigid(P.seated(jb, j0 + jd * 0.125 + V(sg * 0.02, 0, 0.008), (sg * 0.12, -0.15, 1), 0.04, 0.007, curl=(0, 1, 0), seg=2), 'jaw', 'teeth',
                smooth=50)
        up = [(sg * (0.037 + 0.006 * k), -0.325 + 0.022 * k, h + 0.004) for k in range(1, 5)]
        C.rigid(P.tooth_row(bvh, up, (sg * 0.25, 0, -1), 0.018, 0.0045, seed=2 + sg), 'head', 'teeth')
        lo = [j0 + jd * (0.11 - 0.02 * k) + V(sg * (0.026 + 0.004 * k), 0, 0.008) for k in range(1, 4)]
        C.rigid(P.tooth_row(jb, lo, (sg * 0.2, 0, 1), 0.014, 0.004, seed=5 + sg), 'jaw', 'teeth')
        C.rigid(P.blob((sg * 0.056, -0.302, h + 0.058), 0.016, 0.016, 0.016, n=8, m=5), 'head', 'eye', smooth=180)
        # dish ear
        eb = V(sg * 0.058, -0.175, h + 0.11); et = V(sg * 0.215, -0.15, h + 0.47)
        ec = M.lerp(eb, et, 0.55)
        d = RE.dish(ec, (sg * 0.25, -1.0, 0.08), 0.20, depth=0.09, n=14, m=5, th=0.007, squash=0.82, upv=unit(et - eb))
        C.rigid(d, 'ear_' + s, 'ear', smooth=180)
        C.rigid(P.tubep([eb - V(sg * 0.025, 0, 0.05), eb + (ec - eb) * 0.5 + V(0, 0.04, 0)], [0.034, 0.024], m=4, n=7), 'ear_' + s, 'ear', smooth=180)
        # wing: arm bones, fingers, membrane
        Wp = mirror_pts(W, sg)
        sh0, el, wr, tp = Wp
        C.rigid(P.tubep([sh0 - unit(el - sh0) * 0.05, el], [0.026, 0.016], m=5, n=7), 'wing_%s1' % s, 'arm', smooth=180)
        C.rigid(P.tubep([el - unit(wr - el) * 0.012, wr], [0.017, 0.012], m=5, n=7), 'wing_%s2' % s, 'arm', smooth=180)
        f2 = V(sg * 0.82, 0.26, h - 0.005); f3 = V(sg * 0.66, 0.38, h - 0.01); f4 = V(sg * 0.38, 0.43, h - 0.02)
        hip = V(sg * 0.07, 0.17, h - 0.03)
        fing = [P.tubep([wr - unit(q - wr) * 0.008, q], [0.009, 0.0035], m=5, n=5) for q in (tp, f2, f3)]
        fing.append(P.cone(wr, (0, -1, 0.5), 0.055, 0.009, n=5, curl=(0, 0, -1), seg=2))            # thumb claw
        C.rigid(M.join(fing), 'wing_%s3' % s, 'arm', smooth=180)
        strut = P.tubep([el, f4], [0.009, 0.0035], m=5, n=5)
        C.skin(B.wbones(strut, ['wing_%s1' % s, 'wing_%s2' % s], sharp=3.0), 'arm', smooth=180)
        lead = [sh0 + V(-sg * 0.04, 0.0, -0.01), el, wr, tp]
        trail = [hip, f4, f3, f2, tp + V(-sg * 0.01, 0.01, 0)]
        mem = RE.membrane(lead, trail, nu=20, nv=5, camber=0.035, th=0.004, scallop=(4, 0.22), tears=0.12, seed=7 + sg)
        mem.v[:, 2] -= 0.004
        B.wbones(mem, ['body', 'wing_%s1' % s, 'wing_%s2' % s, 'wing_%s3' % s], sharp=3.0, bias={'body': 0.6})
        C.skin(mem, 'wing', smooth=180)
        # legs with hooked toes
        hp = V(sg * 0.06, 0.14, h - 0.06); ft = V(sg * 0.095, 0.245, h - 0.21)
        leg = [P.tubep([hp - unit(ft - hp) * 0.04, ft], [0.02, 0.009], m=5, n=6)]
        for k in (-1, 0, 1):
            leg.append(P.cone(ft, (k * 0.5 * 1.0, 0.3, -1), 0.04, 0.006, n=4, curl=(0, -1, 0), seg=2))
        C.rigid(M.join(leg), 'leg_' + s, 'arm', smooth=180)
    # leaf nose and stub tail
    C.rigid(RB.ear_sheet((0, -0.338, h + 0.04), (0, -0.36, h + 0.135), 0.038, cup=0.012, th=0.006, sink=0.015, face=(0, -1, 0.2)), 'head', 'ear',
            smooth=180)
    tl = P.tubep([(0, 0.17, h - 0.01), (0, 0.24, h - 0.035), (0, 0.285, h - 0.055)], [0.034, 0.02, 0.007], m=6, n=7)
    C.skin(B.wbones(tl, ['body', 'tail1'], sharp=3.0), 'fur', smooth=180)

    fk = dict(amp=(44.0, 24.0, 18.0), lag=(0.0, 0.1, 0.2), bias=(10.0, 0.0, -8.0), fold=(0.0, 10.0, 28.0), twist=12.0)

    def ears(p, a, sp=0.0):
        F.ears(p, a, spread=sp)
    # the jaw is built 14 degrees open: -8 closes it to a snarl, positive values gape
    F.hover('Idle', beats=6, T=0.3333, bob=0.04, pitch=-22.0, flap_kw=fk,
            extra=lambda p, u, ph: (ears(p, 6 * osc(u, 3), 4 * osc(u, 2, 0.2)), F.jaw(p, -4 + 5 * osc(u, 2))))
    F.fly('Walk', 2.2, beats=2, T=0.3, pitch=4.0, flap_kw=fk, legs=40.0, extra=lambda p, u, ph: (ears(p, 10), F.jaw(p, -4)))
    F.fly('Run', 6.0, beats=2, T=0.2333, pitch=20.0, bob=0.025, flap_kw=dict(fk, amp=(50.0, 26.0, 20.0)), legs=65.0,
          extra=lambda p, u, ph: (ears(p, 28), F.jaw(p, 6)))
    F.swoop(dur=1.0, hit=0.5, fwd=0.8, drop=0.65, pitch=38.0, beats=3, gape=38.0, flap_kw=fk, extra=lambda p, u, w, s: ears(p, 30 * s - 10 * w))
    F.screech(dur=1.3, hit=0.6, beats=4, gape=44.0, flap_kw=fk, extra=lambda p, u, w, s: ears(p, -18 * w - 30 * s, 16 * s + 6 * s * osc(u, 12)))
    F.hit(flap_kw=fk, extra=lambda p, u, k: ears(p, 35 * k, -10 * k))
    F.death_fall(dur=1.6, belly=0.12, extra=lambda p, u, t, on: ears(p, 30 * on + 20 * t, 30 * on))
    F.spawn_drop(dur=1.2, flap_kw=fk, extra=lambda p, u, r: ears(p, 30 * (1 - r)))
    C.rescale(K)
    C.meta.update(walk_speed=2.2, run_speed=6.0, radius=0.45)            # nominal speeds (no feet); radius = the body, not the wing span
    return C.finalize(bake=bake, notes="rig E proof")
