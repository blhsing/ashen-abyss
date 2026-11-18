"""Rig B creatures (quadrupeds). One function per bestiary type; BUILD maps the type id to it."""
import math
import numpy as np
import blib as B
from blib import M, V, unit, sm, bump, osc, rx, ry, rz, I3
import bparts as P
import rig_b as RB

BUILD = {}


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


def std_meta(C, family, rig, scale, speed, **kw):
    C.meta.update(family=family, rig=rig, scale=scale, game_speed=speed)
    C.meta.update(kw)


# ================================================================================================ dusk_hound
@reg
def dusk_hound(bake=True):
    """Cinder-hound: lean deep-chested eyeless hound, bat ears, ash-clotted mane, whip tail, two heat pits"""
    J = RB.quad_J(L=0.60, hs=0.66, hh=0.64, wf=0.08, wh=0.085, neck=(0.24, 0.17), head=0.28, sag=0.012, tail=(0.46, 2, -0.7))
    hj = J['hj']
    J['ear'] = (hj + V(0.042, 0.005, 0.045), hj + V(0.09, 0.035, 0.225))
    C = B.Beast('mon_dusk_hound', RB.quad_skel(J, lite=True), budget=4000, bone_budget=28, mb=2.5, tex=1024)     # pack swarmer
    std_meta(C, 'dusk', 'B', 1.0, 6.2)
    Q = RB.Quad(C, J)
    C.look('hide', 'skin', 'skin_hide', color=(0.40, 0.385, 0.37), blotch=(0.24, 0.21, 0.21), vein=(0.16, 0.12, 0.15),
           rough=0.55, sinew=1.4, scar=1.2, dry=0.25, feet_dirt=0.8, feet_hi=0.3, sore_amt=0.3, wrinkle=1.3, vein_amt=0.9)
    C.look('maw', 'skin', 'skin_hide', color=(0.16, 0.06, 0.055), blotch=(0.08, 0.03, 0.03), rough=0.3, sore_amt=0.0, scar=0.0)
    C.look('ear', 'membrane', 'skin_hide', color=(0.20, 0.15, 0.14), vein=(0.07, 0.035, 0.04), vein_scale=16.0)
    C.look('mane', 'bfur', 'hair_mane', color=(0.075, 0.07, 0.068), tip=(0.20, 0.19, 0.18), ash=0.9, flow=(0, 0.8, 0.5), scale=70.0)
    C.look('teeth', 'bone', 'bone_teeth', color=(0.55, 0.50, 0.40), stain=(0.22, 0.15, 0.08), dirt_lo=-2.0, dirt_hi=-1.5)
    C.look('pits', 'emis', 'emis_pits', emit=(1.0, 0.24, 0.03), strength=2.2, mottle=0.7, base_mul=0.5)

    def ribs(body):
        v = body.v
        n = body.normals()
        m = (np.clip((np.abs(v[:, 0]) - 0.035) / 0.03, 0, 1) * np.clip((v[:, 1] + 0.31) / 0.05, 0, 1) * np.clip((0.07 - v[:, 1]) / 0.06, 0, 1)
             * np.clip((v[:, 2] - 0.40) / 0.04, 0, 1) * np.clip((0.63 - v[:, 2]) / 0.05, 0, 1))
        ph = (v[:, 1] + 0.30 * (v[:, 2] - 0.5)) / 0.047
        body.v = v + n * (0.0055 * m * np.maximum(0, np.cos(2 * np.pi * ph)) ** 1.5 - 0.002 * m)[:, None]
        # hollow flank behind the ribs, hip bones
        body.dent((0.07, 0.17, 0.56), 0.07, -0.012)
        body.dent((-0.07, 0.17, 0.56), 0.07, -0.012)
        # brow ridge over where the eyes would be
        body.dent((0, -0.62, 0.875), 0.035, 0.006)

    S = dict(
        torso=[(0.42, 0.56, 0.06, 0.055), (0.30, 0.55, 0.10, 0.09), (0.10, 0.565, 0.085, 0.075), (-0.10, 0.535, 0.13, 0.095),
               (-0.25, 0.52, 0.145, 0.10), (-0.34, 0.545, 0.11, 0.08), (-0.40, 0.585, 0.07, 0.055)],
        neck=[(-0.28, 0.61, 0.10, 0.07), (-0.43, 0.725, 0.065, 0.052), (-0.545, 0.815, 0.055, 0.048)],
        head=[P.blob((0, -0.585, 0.83), 0.055, 0.08, 0.052),
              P.tubep([(0, -0.58, 0.815), (0, -0.70, 0.795), (0, -0.815, 0.776)], [0.04, 0.03, 0.022], [0.042, 0.034, 0.027], m=8, n=10)],
        fleg=[(0.06, 0.042), (0.06, 0.045), (0.04, 0.032), (0.024, 0.021), (0.021, 0.021)],
        hleg=[(0.08, 0.045), (0.085, 0.05), (0.05, 0.036), (0.023, 0.021), (0.021, 0.021)],
        fpaw=(0.032, 0.05, 0.022), hpaw=(0.03, 0.048, 0.022),
        blobs=[((0.07, 0.25, 0.48), (0.05, 0.10, 0.115), ['hip', 'h_upper_l'], True),
               ((0.075, -0.27, 0.51), (0.04, 0.07, 0.10), ['chest', 'f_upper_l'], True)],
        voxel=0.0105, tris=2080, sculpt=ribs)
    body = RB.quad_flesh(C, J, S)
    C.skin(body, 'hide')
    bvh = P.bvh_of(body)
    RB.add_tail(C, J, 0.022, 0.006, 'hide', n=6)
    # lower jaw + tongue + teeth
    jaw = P.tubep([(0, -0.585, 0.775), (0, -0.70, 0.762), (0, -0.80, 0.75)], [0.02, 0.016, 0.011], [0.036, 0.03, 0.022], m=8, n=10)
    C.rigid(jaw, 'jaw', 'hide', smooth=180)
    jb = P.bvh_of(jaw)
    tongue = P.tubep([(0, -0.60, 0.782), (0, -0.70, 0.775), (0, -0.775, 0.764)], [0.006, 0.006, 0.004], [0.02, 0.018, 0.012], m=6, n=8)
    C.rigid(tongue, 'jaw', 'maw', smooth=180)
    for sg in (1, -1):
        ys = np.linspace(-0.79, -0.64, 7)
        up = [(sg * (0.019 + 0.012 * (y + 0.79) / 0.15), y, 0.76 + 0.02 * (y + 0.79) / 0.15) for y in ys]
        C.rigid(P.tooth_row(bvh, up, (sg * 0.15, -0.05, -1), 0.016, 0.0042, seed=3 + sg), 'head', 'teeth')
        C.rigid(P.seated(bvh, (sg * 0.021, -0.795, 0.76), (sg * 0.12, -0.12, -1), 0.034, 0.006, curl=(0, 1, 0), seg=3), 'head', 'teeth', smooth=50)
        lo = [(sg * (0.015 + 0.012 * (y + 0.78) / 0.15), y, 0.765 + 0.012 * (y + 0.78) / 0.15) for y in np.linspace(-0.775, -0.65, 6)]
        C.rigid(P.tooth_row(jb, lo, (sg * 0.1, -0.05, 1), 0.013, 0.0038, seed=7 + sg), 'jaw', 'teeth')
        C.rigid(P.seated(jb, (sg * 0.016, -0.782, 0.757), (sg * 0.15, -0.1, 1), 0.026, 0.005, curl=(0, 1, 0), seg=3), 'jaw', 'teeth', smooth=50)
        # heat pits: glowing hollow + flared rim
        c = V(sg * 0.017, -0.812, 0.787)
        q, nrm, _ = P.nearest(bvh, c)
        d = unit(V(sg * 0.5, -1.0, 0.25))
        C.rigid(P.blob(q - d * 0.004, 0.0105, 0.0105, 0.0105, n=6, m=4), 'head', 'pits', smooth=180)
        C.rigid(P.ring(q + d * 0.002, 0.0125, 0.0045, axis=d, n=8, m=4), 'head', 'hide', smooth=180)
        # ears
        e = RB.ear_sheet(J['ear'][0] * (sg, 1, 1), J['ear'][1] * (sg, 1, 1), 0.058, cup=0.022, th=0.006, sink=0.03, rows=5, cols=4,
                         face=(sg * 0.5, -1, 0))
        C.rigid(e, 'ear_l' if sg > 0 else 'ear_r', 'ear', smooth=180)
    RB.add_claws(C, bvh, J, 'teeth', n=3, length=0.03, r=0.007, sides=4)
    # ash mane along neck and spine
    def mane(c, n):
        return (n[:, 2] > 0.45) & (np.abs(c[:, 0]) < 0.05) & (c[:, 1] > -0.54) & (c[:, 1] < 0.26) & (c[:, 2] > 0.55) & ((c[:, 1] > -0.30) | (c[:, 2] > 0.70))
    tf = P.tufts(body, 105, length=(0.06, 0.13), radius=(0.011, 0.021), seed=5, mask=mane, flow=(0, 1, 0.9), lay=0.45,
                 sink=0.3, sides=3, jitter=0.35)
    C.skin(tf, 'mane', smooth=180)

    # ---- clips
    Q.idle(dur=3.0, look=9.0, tail=7.0, sniff=6.0)
    Q.gait('Walk', 1.2, 0.66, 0.64, RB.WALK4, lift=0.06, bob=0.008, crouch=0.03, nod=2.5, headlow=8.0, tail=9.0, tuck=35.0)
    Q.gait('Run', 6.2, 0.40, 0.22, RB.GALLOP, lift=0.14, bob=0.03, bobn=1, bobph=0.35, pitch=6.0, pitchph=0.55, roll=0.6, yaw=0.8,
           flex=14.0, flexph=0.2, crouch=0.06, nod=3.0, tail=4.0, tuck=70.0, headlow=10.0, ears=-18.0, jaw=8.0)
    Q.bite(dur=0.8, hit=0.42, reach=0.22, gape=40.0)
    Q.leap(dur=1.1, hit=0.6, fwd=0.5, up=0.22, gape=42.0)
    Q.hit()
    Q.death_side(dur=1.6, half_w=0.09)
    Q.spawn_rise(dur=1.2)
    return C.finalize(bake=bake, notes="rig B proof")
