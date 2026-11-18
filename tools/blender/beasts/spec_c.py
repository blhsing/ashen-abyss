"""Rig C creatures (arthropods). The rig proof is sexton_beetle; the other 14 live in the helper's spec file."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3
import bparts as P
import rig_c as RC

BUILD = {}


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


def drape(bvh, x0, x1, y0, y1, nx=9, ny=9, off=0.007, hang=0.12, seed=0, rag=0.5, th=0.004, wrinkle=0.006):
    """scrap of cloth lying on a host surface: a grid cast down onto the host; past its rim the cloth hangs.
    Columns run along +X from x0 to x1 (x0 on the host)."""
    rng = np.random.default_rng(seed)
    G = np.zeros((ny, nx, 3))
    for j in range(ny):
        y = y0 + (y1 - y0) * j / (ny - 1)
        last = None
        drop = 0.0
        ext = hang * (1 - rag * rng.random())
        for i in range(nx):
            x = x0 + (x1 - x0) * i / (nx - 1)
            q, n = P.surf(bvh, (x, y, 2.0), (0, 0, -1), 4.0)
            if q is not None and (last is None or q[2] > last[2] - 0.08):
                pt = q + n * off
                pt[2] += wrinkle * math.sin(7.0 * i + 3.1 * j) * (0.5 + rng.random())
                last = pt.copy()
            else:
                step = abs(x1 - x0) / (nx - 1)
                drop += step
                k = min(drop, ext)
                pt = V(last[0] + math.copysign(min(0.015, step * 0.3) * (drop / step), x1 - x0), y + 0.01 * math.sin(drop * 40 + j), last[2] - k)
            G[j, i] = pt
    return P.sheet_solid(G, th)


# ================================================================================================ sexton_beetle
@reg
def sexton_beetle(bake=True):
    """Sexton-beetle: dog-sized burying beetle - domed black wing-case with two dull orange bands, flat shovel
    head-shield wider than the body, clubbed antennae, a scrap of shroud and a tin grave-marker on its back"""
    legs = [RC.leg_def((0.12, -0.17, 0.15), (0.29, -0.30, 0.31), (0.40, -0.41, 0.055), (0.44, -0.46, 0.0), r=(0.04, 0.026, 0.014)),
            RC.leg_def((0.13, -0.02, 0.15), (0.35, -0.02, 0.33), (0.48, 0.02, 0.055), (0.53, 0.03, 0.0), r=(0.04, 0.026, 0.014)),
            RC.leg_def((0.12, 0.12, 0.15), (0.29, 0.29, 0.31), (0.40, 0.46, 0.055), (0.43, 0.52, 0.0), r=(0.043, 0.028, 0.015))]
    sk = RC.arth_skel(body=((0, 0.05, 0.22), (0, -0.24, 0.22)), head=((0, -0.26, 0.19), (0, -0.58, 0.15)), legs=legs,
                      abd=[(0, 0.03, 0.22), (0, 0.46, 0.20)], mand=((0.045, -0.50, 0.10), (0.018, -0.66, 0.085)),
                      ant=[(0.11, -0.40, 0.19), (0.25, -0.53, 0.235), (0.30, -0.63, 0.25)],
                      extra=lambda sk: sk.bone('marker', (-0.085, 0.27, 0.40), (-0.11, 0.33, 0.62), 'abdomen1'))
    C = B.Beast('mon_sexton_beetle', sk)
    C.meta.update(family='sexton', rig='C', scale=1.0, game_speed=4.0)
    A = RC.Arth(C, legs, pivot=(0, 0.05, 0.2))
    C.look('shell', 'chitin', 'bone_shell', color=(0.016, 0.015, 0.016), bands=[(0.05, 0.14), (0.27, 0.35)], band_color=(0.46, 0.17, 0.03),
           rough=0.26, dust=0.45, edge=0.5)
    C.look('limb', 'chitin', 'bone_shell', color=(0.03, 0.024, 0.02), rough=0.34, pit=170.0, dust=0.2, axis=(1, 0, 0))
    C.look('belly', 'chitin', 'bone_shell', color=(0.05, 0.035, 0.025), rough=0.45, ridge=30.0, ridge_h=1.6, dust=0.0)
    C.look('shroud', 'cloth', 'cloth_shroud', color=(0.36, 0.33, 0.27), dirt=(0.10, 0.08, 0.06), weave='linen', fold=22.0)
    C.look('tin', 'metal', 'metal_tin', color=(0.20, 0.205, 0.20), rust=(0.16, 0.08, 0.035), amount=0.7, rough=0.55)
    # wing-case (elytra) + abdomen
    el = RC.shell(0.58, 0.47, 0.27, n=20, m=7, power=2.3, front=0.9, rear=0.55, groove=0.007).move(0, 0.19, 0.155)
    C.rigid(el, 'abdomen1', 'shell', smooth=60)
    C.rigid(P.blob((0, 0.17, 0.16), 0.19, 0.27, 0.075, n=14, m=6), 'abdomen1', 'belly', smooth=180)
    # thorax shield + underside
    pr = RC.shell(0.27, 0.40, 0.165, n=18, m=6, power=2.6, front=0.78, rear=1.0).move(0, -0.165, 0.175)
    C.rigid(pr, 'body', 'shell', smooth=60)
    C.rigid(P.blob((0, -0.10, 0.165), 0.17, 0.19, 0.065, n=14, m=6), 'body', 'belly', smooth=180)
    # head: capsule under a wide flat shovel
    C.rigid(P.blob((0, -0.375, 0.13), 0.10, 0.155, 0.058, n=12, m=6), 'head', 'limb', smooth=180)
    half = [(0.0, -0.245), (0.13, -0.25), (0.21, -0.29), (0.285, -0.37), (0.30, -0.45), (0.25, -0.535), (0.14, -0.585), (0.0, -0.60)]
    outl = half + [(-x, y) for x, y in reversed(half[1:-1])]
    sh = RC.plate(outl[::-1], th=0.024, z=0.19, dome=0.018, tilt=0.0, bevel=0.35)
    sh.subdiv(1)
    sh.v[:, 2] -= 0.16 * np.clip((-sh.v[:, 1] - 0.25), 0, 1) + 0.25 * np.abs(sh.v[:, 0]) ** 2      # droops forward and at the corners
    sh.v[:, 2] += 0.012 * np.exp(-(sh.v[:, 0] / 0.05) ** 2)                                           # median keel
    C.rigid(sh, 'head', 'shell', smooth=45)
    for sg in (1, -1):
        s = 'l' if sg > 0 else 'r'
        C.rigid(P.blob((sg * 0.105, -0.40, 0.165), 0.022, 0.03, 0.022, n=8, m=5), 'head', 'limb', smooth=180)     # eye
        C.rigid(RC.mandible((sg * 0.045, -0.49, 0.102), (sg * 0.018, -0.66, 0.085), 0.02, curve=0.28, side=sg), 'mandible_' + s, 'limb')
        C.rigid(RC.antenna([(sg * 0.10, -0.40, 0.18), (sg * 0.18, -0.47, 0.21), (sg * 0.25, -0.53, 0.235)], 0.009, 0.007, n=5),
                'antenna1_' + s, 'limb')
        C.rigid(RC.antenna([(sg * 0.245, -0.525, 0.233), (sg * 0.275, -0.58, 0.243)], 0.007, 0.007, club=0.075, n=5), 'antenna2_' + s, 'limb')
    RC.add_legs(C, legs, 'limb', n=7, spur=0.035)
    # a scrap of shroud over the left rear of the wing-case and a tin grave-marker stuck through it
    bv = P.bvh_of(el)
    C.rigid(drape(bv, -0.03, 0.34, 0.12, 0.43, nx=11, ny=9, hang=0.16, seed=4), 'abdomen1', 'shroud', smooth=180)
    mk = M.box(0.11, 0.006, 0.15, bevel=0.002)
    mk.v[:, 0] *= 1 - 0.45 * np.clip((mk.v[:, 2] - 0.02) / 0.055, 0, 1) * (mk.v[:, 2] > 0.02)       # pointed (gabled) top
    mk.move(0, 0, 0.115)
    st = M.box(0.012, 0.006, 0.12).move(0, 0, 0.0)
    marker = M.join([mk, st])
    marker.rot('x', -22).rot('y', 14).move(-0.085, 0.27, 0.385)
    C.rigid(marker, 'marker', 'tin', smooth=20)

    # ---- clips
    A.idle(dur=3.0, feel=14.0, jaw=8.0)
    A.gait('Walk', 1.4, 0.40, 0.62, RC.tripod(3), lift=0.045, bob=0.004, roll=1.5, yaw=2.0, feel=8.0)
    A.gait('Run', 4.0, 0.2667, 0.42, RC.tripod(3), lift=0.07, bob=0.006, roll=2.0, yaw=2.5, feel=5.0, pitch=2.0, crouch=0.01)
    A.snap(dur=0.7, hit=0.36, reach=0.2, gape=40.0, drop=0.0, extra=lambda p, u, w, s: p.rot('head', x=-10 * s))
    A.ram(dur=1.0, hit=0.55, reach=0.26, lower=5.0, back=0.08, extra=lambda p, u, w, s: (p.rot('head', x=-9 * s - 4 * w), p.move('body', 0, 0, 0.03 * s)))
    A.hit()
    def fold(p, u, f):                      # the marker is pressed flat against the wing-case as it lands
        p.rot('marker', x=-62 * sm(u, 0.3, 0.5), y=-10 * sm(u, 0.3, 0.5))
    A.death_flip(dur=1.5, back_h=0.27, extra=fold)
    A.spawn_dig(dur=1.2)
    return C.finalize(bake=bake, notes="rig C proof")
