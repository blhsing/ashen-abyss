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
    """Hearth-bat: two dish ears each larger than its body, flat leaf-nose, stub tail, wing membranes torn into
    scallops along the trailing edge"""
    h = 1.6
    W = [(0.07, -0.06, h + 0.04), (0.34, 0.02, h + 0.09), (0.62, -0.12, h + 0.06), (0.93, 0.06, h + 0.0)]
    sk = RE.flyer_skel(body=((0, 0.14, h), (0, -0.12, h + 0.02)), head=((0, -0.13, h + 0.03), (0, -0.28, h + 0.01)),
                       jaw=((0, -0.165, h - 0.012), (0, -0.265, h - 0.03)), ears=((0.045, -0.15, h + 0.075), (0.19, -0.13, h + 0.40)),
                       wing=W, tail=[(0, 0.14, h - 0.01), (0, 0.23, h - 0.04)], legs=((0.045, 0.12, h - 0.04), (0.075, 0.21, h - 0.17)))
    C = B.Beast('mon_dusk_bat', sk)
    C.meta.update(family='dusk', rig='E', scale=1.0, game_speed=6.0)
    F = RE.Flyer(C, h)
    C.look('fur', 'bfur', 'hair_fur', color=(0.085, 0.07, 0.06), tip=(0.22, 0.19, 0.16), flow=(0, 1, -0.3), ash=0.35, scale=80.0)
    C.look('wing', 'membrane', 'skin_wing', color=(0.075, 0.055, 0.05), vein=(0.025, 0.014, 0.014), vein_scale=11.0, rough=0.45)
    C.look('ear', 'membrane', 'skin_wing', color=(0.17, 0.11, 0.10), vein=(0.07, 0.03, 0.03), vein_scale=20.0, rough=0.4)
    C.look('arm', 'skin', 'skin_wing', color=(0.16, 0.12, 0.11), blotch=(0.08, 0.06, 0.06), rough=0.5, sore_amt=0.0, scar=0.5)
    C.look('maw', 'skin', 'skin_wing', color=(0.20, 0.07, 0.06), blotch=(0.10, 0.03, 0.03), rough=0.3, sore_amt=0.0, scar=0.0)
    C.look('teeth', 'bone', 'bone_teeth', color=(0.62, 0.58, 0.46), dirt_lo=-2.0, dirt_hi=-1.5)
    # furry body + head (one skin)
    torso = P.blob((0, 0.02, h), 0.085, 0.17, 0.09, n=14, m=8)
    B.wbones(torso, ['body'])
    chest = P.blob((0, -0.06, h + 0.005), 0.095, 0.10, 0.10, n=12, m=7)
    B.wbones(chest, ['body'])
    head = P.blob((0, -0.19, h + 0.035), 0.068, 0.085, 0.066, n=12, m=7)
    B.wbones(head, ['head'])
    snout = P.tubep([(0, -0.22, h + 0.02), (0, -0.275, h + 0.012), (0, -0.295, h + 0.008)], [0.04, 0.03, 0.02], [0.045, 0.036, 0.026], m=6, n=10)
    B.wbones(snout, ['head'])
    body = P.organic([torso, chest, head, snout], voxel=0.008, tris=2600, smooth=6)
    C.skin(body, 'fur')
    bvh = P.bvh_of(body)

    def ruff(c, n):
        return (c[:, 1] > -0.16) & (c[:, 1] < 0.1) & (n[:, 2] > -0.6)
    tf = P.tufts(body, 150, length=(0.03, 0.06), radius=(0.007, 0.013), seed=3, mask=ruff, flow=(0, 1, -0.2), lay=0.55, sides=3, jitter=0.3)
    C.skin(tf, 'fur', smooth=180)
    # jaw, fangs, leaf-nose, eyes
    jaw = P.tubep([(0, -0.175, h - 0.012), (0, -0.24, h - 0.022), (0, -0.282, h - 0.026)], [0.016, 0.014, 0.009], [0.036, 0.03, 0.02], m=6, n=8)
    C.rigid(jaw, 'jaw', 'arm', smooth=180)
    C.rigid(P.tubep([(0, -0.19, h - 0.004), (0, -0.265, h - 0.014)], [0.005, 0.004], [0.022, 0.014], m=4, n=8), 'jaw', 'maw', smooth=180)
    jb = P.bvh_of(jaw)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(P.seated(bvh, (sg * 0.02, -0.282, h + 0.0), (sg * 0.1, -0.15, -1), 0.03, 0.0055, curl=(0, 1, 0), seg=3), 'head', 'teeth', smooth=50)
        C.rigid(P.seated(jb, (sg * 0.016, -0.272, h - 0.02), (sg * 0.1, -0.1, 1), 0.02, 0.0045), 'jaw', 'teeth', smooth=50)
        up = [(sg * (0.024 + 0.008 * k), -0.268 + 0.018 * k, h + 0.0) for k in range(1, 4)]
        C.rigid(P.tooth_row(bvh, up, (sg * 0.2, 0, -1), 0.011, 0.003, seed=2 + sg), 'head', 'teeth')
        C.rigid(P.blob((sg * 0.043, -0.235, h + 0.058), 0.011, 0.011, 0.011, n=7, m=4), 'head', 'teeth', smooth=180)
        # dish ear
        eb = V(sg * 0.045, -0.15, h + 0.075); et = V(sg * 0.19, -0.13, h + 0.40)
        ec = M.lerp(eb, et, 0.55)
        d = RE.dish(ec, (sg * 0.25, -1.0, 0.08), 0.185, depth=0.085, n=14, m=5, th=0.007, squash=0.82, upv=unit(et - eb))
        C.rigid(d, 'ear_' + s, 'ear', smooth=180)
        C.rigid(P.tubep([eb - V(sg * 0.02, 0, 0.04), eb + (ec - eb) * 0.5 + V(0, 0.04, 0)], [0.03, 0.022], m=4, n=7), 'ear_' + s, 'ear', smooth=180)
        # wing: arm bones, fingers, membrane
        Wp = mirror_pts(W, sg)
        sh, el, wr, tp = Wp
        C.rigid(P.tubep([sh - unit(el - sh) * 0.03, el], [0.02, 0.014], m=5, n=7), 'wing_%s1' % s, 'arm', smooth=180)
        C.rigid(P.tubep([el - unit(wr - el) * 0.01, wr], [0.015, 0.011], m=5, n=7), 'wing_%s2' % s, 'arm', smooth=180)
        f2 = V(sg * 0.80, 0.25, h - 0.005); f3 = V(sg * 0.64, 0.36, h - 0.01); f4 = V(sg * 0.36, 0.41, h - 0.02)
        hip = V(sg * 0.055, 0.15, h - 0.02)
        fing = [P.tubep([wr - unit(q - wr) * 0.008, q], [0.009, 0.0035], m=5, n=5) for q in (tp, f2, f3)]
        fing.append(P.cone(wr, (0, -1, 0.5), 0.05, 0.008, n=5, curl=(0, 0, -1), seg=2))            # thumb claw
        C.rigid(M.join(fing), 'wing_%s3' % s, 'arm', smooth=180)
        strut = P.tubep([el, f4], [0.009, 0.0035], m=5, n=5)
        C.skin(B.wbones(strut, ['wing_%s1' % s, 'wing_%s2' % s], sharp=3.0), 'arm', smooth=180)
        lead = [sh + V(-sg * 0.03, 0.0, -0.01), el, wr, tp]
        trail = [hip, f4, f3, f2, tp + V(-sg * 0.01, 0.01, 0)]
        mem = RE.membrane(lead, trail, nu=20, nv=5, camber=0.035, th=0.004, scallop=(4, 0.22), tears=0.12, seed=7 + sg)
        mem.v[:, 2] -= 0.004
        B.wbones(mem, ['body', 'wing_%s1' % s, 'wing_%s2' % s, 'wing_%s3' % s], sharp=3.0, bias={'body': 0.6})
        C.skin(mem, 'wing', smooth=180)
        # legs with hooked toes
        hp = V(sg * 0.045, 0.12, h - 0.04); ft = V(sg * 0.075, 0.21, h - 0.17)
        leg = [P.tubep([hp - unit(ft - hp) * 0.03, ft], [0.016, 0.008], m=5, n=6)]
        for k in (-1, 0, 1):
            leg.append(P.cone(ft, (k * 0.5 * 1.0, 0.3, -1), 0.035, 0.005, n=4, curl=(0, -1, 0), seg=2))
        C.rigid(M.join(leg), 'leg_' + s, 'arm', smooth=180)
    # leaf nose and stub tail
    C.rigid(RB.ear_sheet((0, -0.283, h + 0.022), (0, -0.30, h + 0.10), 0.03, cup=0.01, th=0.005, sink=0.012, face=(0, -1, 0.2)), 'head', 'ear', smooth=180)
    tl = P.tubep([(0, 0.14, h - 0.01), (0, 0.20, h - 0.03), (0, 0.24, h - 0.045)], [0.03, 0.018, 0.006], m=6, n=7)
    C.skin(B.wbones(tl, ['body', 'tail1'], sharp=3.0), 'fur', smooth=180)

    fk = dict(amp=(44.0, 24.0, 18.0), lag=(0.0, 0.1, 0.2), bias=(10.0, 0.0, -8.0), fold=(0.0, 10.0, 28.0), twist=12.0)

    def ears(p, a, sp=0.0):
        F.ears(p, a, spread=sp)
    F.hover('Idle', beats=6, T=0.3333, bob=0.04, pitch=-22.0, flap_kw=fk, extra=lambda p, u, ph: (ears(p, 6 * osc(u, 3), 4 * osc(u, 2, 0.2)), F.jaw(p, 4)))
    F.fly('Walk', 2.2, beats=2, T=0.3, pitch=4.0, flap_kw=fk, legs=40.0, extra=lambda p, u, ph: ears(p, 10))
    F.fly('Run', 6.0, beats=2, T=0.2333, pitch=20.0, bob=0.025, flap_kw=dict(fk, amp=(50.0, 26.0, 20.0)), legs=65.0, extra=lambda p, u, ph: ears(p, 28))
    F.swoop(dur=1.0, hit=0.5, fwd=0.8, drop=0.65, pitch=38.0, beats=3, gape=42.0, flap_kw=fk, extra=lambda p, u, w, s: ears(p, 30 * s - 10 * w))
    F.screech(dur=1.3, hit=0.6, beats=4, gape=50.0, flap_kw=fk, extra=lambda p, u, w, s: ears(p, -18 * w - 30 * s, 16 * s + 6 * s * osc(u, 12)))
    F.hit(flap_kw=fk, extra=lambda p, u, k: ears(p, 35 * k, -10 * k))
    F.death_fall(dur=1.6, belly=0.085, extra=lambda p, u, t, on: ears(p, 30 * on + 20 * t, 30 * on))
    F.spawn_drop(dur=1.2, flap_kw=fk, extra=lambda p, u, r: ears(p, 30 * (1 - r)))
    return C.finalize(bake=bake, notes="rig E proof")
