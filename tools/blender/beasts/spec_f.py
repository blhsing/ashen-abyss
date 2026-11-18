"""Rig F creatures (rigid / floating things): each has its own small skeleton."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis
import bparts as P
import rig_f as RF

BUILD = {}


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


# ================================================================================================ wraith_gasp
@reg
def wraith_gasp(bake=True):
    """Lantern-ghost: a dead iron lantern floating at chest height, door hanging open, a small face-like wisp inside,
    a short carrying-chain dangling underneath"""
    h = 1.15
    z0, z1 = h - 0.18, h + 0.18
    hinge = V(0.112, -0.115, 0)
    sk = B.Sk()
    sk.bone('body', (0, 0, z0), (0, 0, z1), 'root')
    sk.bone('door', hinge + V(0, 0, z1 - 0.02), hinge + V(0, 0, z0 + 0.02), 'body')
    sk.bone('handle', (0, 0, z1 + 0.155), (0, 0, z1 + 0.28), 'body')
    sk.bone('wisp', (0, -0.02, h - 0.04), (0, -0.02, h + 0.08), 'body')
    ch = sk.chain('chain', [(0, 0, z0 - 0.01 - 0.1 * i) for i in range(4)], 'body')
    C = B.Beast('mon_wraith_gasp', sk, budget=4000, bone_budget=28, mb=2.5, tex=1024)     # swarmer
    C.meta.update(family='wraith', rig='F', scale=1.0, game_speed=5.0)
    R = RF.Rigid(C, 'body', hover=h)
    C.look('iron', 'metal', 'metal_iron', color=(0.16, 0.15, 0.145), rust=(0.20, 0.085, 0.03), rust2=(0.07, 0.035, 0.02), amount=0.8, rough=0.55,
           edge_color=(0.32, 0.30, 0.28))
    C.look('glass', 'cglass', 'emis_wisp', color=(0.014, 0.014, 0.018), band=(0.05, 0.05, 0.065), emit=(0.45, 0.72, 1.0), strength=1.6, crack=30.0)
    C.look('wisp', 'emis', 'emis_wisp', emit=(0.45, 0.72, 1.0), strength=1.6, mottle=0.35, base_mul=0.6, scale=30.0)
    C.look('void', 'flat', 'emis_wisp', color=(0.004, 0.008, 0.018), rough=0.9)
    # frame
    fr = [M.box(0.27, 0.27, 0.03, bevel=0.004).move(0, 0, z0 + 0.015), M.box(0.27, 0.27, 0.026, bevel=0.004).move(0, 0, z1 - 0.013)]
    for sx in (1, -1):
        for sy in (1, -1):
            fr.append(M.box(0.024, 0.024, 0.36).move(sx * 0.112, sy * 0.112, h))
    for sx, sy, ln in ((0, 1, 0), (1, 0, 1), (-1, 0, 1)):                      # mid rails on the three closed sides
        b = M.box(0.012 if ln else 0.22, 0.22 if ln else 0.012, 0.014).move(sx * 0.118, sy * 0.118, h + 0.02)
        fr.append(b)
    C.rigid(M.join(fr), 'body', 'iron', smooth=30)
    roof = M.loft_z([(z1 - 0.002, 0, 0, 0.205, 0.205), (z1 + 0.02, 0, 0, 0.19, 0.19), (z1 + 0.10, 0, 0, 0.07, 0.07), (z1 + 0.105, 0, 0, 0.05, 0.05),
                     (z1 + 0.14, 0, 0, 0.05, 0.05), (z1 + 0.145, 0, 0, 0.07, 0.07), (z1 + 0.165, 0, 0, 0.012, 0.012)], n=4, phase=math.pi / 4)
    roof.smooth = 25.0
    C.rigid(roof, 'body', 'iron')
    C.rigid(M.box(0.02, 0.012, 0.03).move(0, 0, z1 + 0.16), 'body', 'iron', smooth=30)
    ring = P.ring((0, 0, z1 + 0.155 + 0.062), 0.062, 0.008, axis=(0, 1, 0), n=16, m=6)
    C.rigid(ring, 'handle', 'iron', smooth=60)
    # sooted panes on three sides
    for sx, sy in ((0, 1), (1, 0), (-1, 0)):
        g = M.box(0.006 if sx else 0.205, 0.205 if sx else 0.006, 0.31).move(sx * 0.113, sy * 0.113, h)
        C.rigid(g, 'body', 'glass', smooth=20)
    # the door: frame + pane, hanging open on its upper hinge
    dr = [M.box(0.215, 0.012, 0.022).move(-0.1075, 0, z1 - 0.036), M.box(0.215, 0.012, 0.022).move(-0.1075, 0, z0 + 0.04),
          M.box(0.02, 0.012, 0.30).move(-0.012, 0, h), M.box(0.02, 0.012, 0.30).move(-0.205, 0, h),
          M.box(0.03, 0.02, 0.016).move(-0.215, -0.012, h)]                      # latch
    door = M.join(dr)
    pane = M.box(0.18, 0.005, 0.27).move(-0.108, 0, h)
    for q, lk in ((door, 'iron'), (pane, 'glass')):
        q.rot('z', 104).rot('y', -5, pivot=(0, 0, z1)).move(hinge[0], hinge[1], 0)
        C.rigid(q, 'door', lk, smooth=25)
    C.rigid(M.join([M.box(0.016, 0.03, 0.03).move(hinge[0] + 0.006, hinge[1] - 0.004, z1 - 0.05),
                    M.box(0.016, 0.03, 0.03).move(hinge[0] + 0.006, hinge[1] - 0.004, z0 + 0.06)]), 'body', 'iron', smooth=25)
    # the wisp: a face pressed forward into the door opening, with a guttering tail of flame above it
    w = P.blob((0, -0.05, h - 0.03), 0.082, 0.085, 0.10, n=14, m=8)
    w.v[:, 1] = np.maximum(w.v[:, 1], -0.118)                                   # flattened where it presses on the opening
    fl = P.cone((0.0, -0.01, h + 0.045), (0.15, -0.1, 1), 0.10, 0.04, n=7, curl=(-0.6, 0.2, 0.3), seg=4)
    C.rigid(M.join([w, fl]), 'wisp', 'wisp', smooth=180)
    face = [P.blob((sg * 0.03, -0.119, h - 0.005), 0.016, 0.006, 0.024, n=8, m=4) for sg in (1, -1)]
    face.append(P.blob((0, -0.119, h - 0.065), 0.02, 0.006, 0.032, n=8, m=4))
    for f in face:
        f.rot('z', 0)
    C.rigid(M.join(face), 'wisp', 'void', smooth=180)
    # carrying chain under the base, ending in a hook
    C.rigid(M.box(0.03, 0.03, 0.02).move(0, 0, z0 - 0.005), 'body', 'iron', smooth=25)
    RF.chain_parts(C, ch, 'iron', link=0.05, r=0.007)
    hook = P.tubep([(0, 0, z0 - 0.30), (0, 0.0, z0 - 0.34), (0, 0.03, z0 - 0.37), (0, 0.06, z0 - 0.35)], [0.007, 0.007, 0.006, 0.003], m=8, n=6)
    C.rigid(hook, ch[-1], 'iron', smooth=60)
    front = V(0, -0.14, h)

    def wisp(p, s, dz=0.0):
        p.scl('wisp', max(0.02, s)); p.move('wisp', 0, 0, dz)

    def idle(p, u):
        R.body(p, dz=0.03 * osc(u, 2), dx=0.015 * osc(u, 1), yaw=10 * osc(u, 1), pitch=3 * osc(u, 2, 0.1), roll=3 * osc(u, 1, 0.3))
        p.rot('handle', y=8 * osc(u, 2, 0.2), x=4 * osc(u, 2, 0.4))
        p.rot('door', z=6 * osc(u, 2, 0.4) + 3 * osc(u, 6) * bump(u, 0.55, 0.6, 0.7, 0.8))
        R.dangle(p, ch, u=u, amp=5, n=2, lag=0.15)
        wisp(p, 1 + 0.06 * osc(u, 5) + 0.04 * osc(u, 9, 0.3), 0.006 * osc(u, 3))
    C.clip('Idle', 3.0, idle)

    def travel(v, T, lean, trail, flap):
        def fn(p, u):
            R.body(p, dz=0.025 * osc(u, 2), pitch=lean + 3 * osc(u, 2, 0.15), roll=4 * osc(u, 1), yaw=5 * osc(u, 1, 0.25))
            p.rot('handle', x=-lean * 0.8 + 6 * osc(u, 2, 0.3), y=6 * osc(u, 1))
            p.rot('door', z=flap * (0.4 + 0.6 * osc(u, 2, 0.1)))
            R.dangle(p, ch, x=trail, u=u, amp=5, n=2, lag=0.14)
            wisp(p, 1.05 + 0.08 * osc(u, 4) + 0.04 * osc(u, 7, 0.3), 0.0)
        return fn
    C.clip('Walk', 1.2, travel(1.8, 1.2, 9.0, 18.0, 8.0)); C.meta['walk_speed'] = 1.8
    C.clip('Run', 0.8, travel(5.0, 0.8, 22.0, 42.0, 18.0)); C.meta['run_speed'] = 5.0

    def lunge(p, u):                      # Attack1: lunge-flare
        hu = 0.4 / 0.8
        w = bump(u, 0.0, hu * 0.5, hu * 0.7, hu)
        s = bump(u, hu * 0.7, hu, hu + 0.1, 0.95)
        R.body(p, dy=0.16 * w - 0.6 * s, dz=0.08 * w - 0.12 * s, pitch=-14 * w + 26 * s, yaw=-8 * w)
        p.rot('handle', x=16 * w - 30 * s)
        p.rot('door', z=-25 * w + 38 * s)
        R.dangle(p, ch, x=-20 * w + 50 * s, u=u, amp=4, n=3)
        wisp(p, 1 - 0.25 * w + 0.9 * bump(u, hu * 0.8, hu, hu + 0.06, hu + 0.3), 0.01 * s)
    C.clip('Attack1', 0.8, lunge, hit=0.4)
    C.reach('Attack1', 'body', front)

    def shatter(p, u):                    # Attack2: the light swells until the lantern bursts open
        hu = 0.7 / 1.2
        g = sm(u, 0.0, hu)
        b = bump(u, hu - 0.02, hu, hu + 0.05, hu + 0.3)
        sh = g * (1 - sm(u, hu, hu + 0.1))
        R.body(p, dz=0.1 * g * (1 - sm(u, hu, 0.95)) + 0.03 * b, pitch=5 * sh * osc(u, 17), roll=5 * sh * osc(u, 13, 0.2), yaw=6 * sh * osc(u, 11, 0.4))
        p.scl('body', 1 + 0.12 * b)
        p.rot('handle', y=10 * sh * osc(u, 15) + 0, x=-50 * b)
        p.rot('door', z=8 * sh * osc(u, 19) + 60 * b)
        R.dangle(p, ch, u=u, amp=8 * sh + 20 * b, n=8, lag=0.2)
        wisp(p, 1 + 0.5 * g * g * (1 - sm(u, hu, hu + 0.04)) + 0.75 * b - 0.4 * bump(u, hu + 0.1, hu + 0.2, 0.8, 0.98))
    C.clip('Attack2', 1.2, shatter, hit=0.7)
    C.meta['attack_reach'] = max(C.meta.get('attack_reach', 0.0), 0.74)

    def hit(p, u):
        k = bump(u, 0.0, 0.2, 0.3, 1.0)
        R.body(p, dy=0.14 * k, dz=-0.05 * k, pitch=-22 * k, yaw=40 * k, roll=14 * k)
        p.rot('handle', x=30 * k, y=-20 * k)
        p.rot('door', z=30 * k)
        R.dangle(p, ch, x=-35 * k, u=u, amp=10 * k, n=2)
        wisp(p, 1 - 0.45 * k)
    C.clip('Hit', 0.4, hit)

    land = 0.42

    def death(p, u):
        f = 1 - RF.fall_curve(u, land)
        on = sm(u, land - 0.02, land + 0.02)
        tip = sm(u, land + 0.02, land + 0.3) ** 1.5
        bn = bump(u, land, land + 0.05, land + 0.08, land + 0.2)
        R.body(p, dz=-(h - 0.2) * f + 0.05 * bn, roll=92 * tip, yaw=35 * f, pitch=10 * bump(u, 0.05, 0.2, land - 0.05, land))
        p.rot('handle', y=70 * tip, x=20 * on)
        p.rot('door', z=35 * on + 20 * tip)
        R.dangle(p, ch, x=88 * sm(u, 0.05, land * 0.8), u=u, amp=6 * (1 - on), n=3)
        wisp(p, 1 - sm(u, 0.0, 0.3) + 0.25 * bump(u, 0.1, 0.14, 0.16, 0.22), -0.03 * sm(u, 0, 0.3))
    C.clip('Death', 1.5, death, floor='body', settle=(land, land + 0.1))

    def spawn(p, u):
        r = sm(u, 0.0, 0.6)
        d = 1 - r
        sw = bump(u, 0.45, 0.6, 0.7, 0.98)
        R.body(p, dz=2.6 * d * d + 0.05 * sw * osc(u, 3), pitch=14 * d + 6 * sw * osc(u, 3), roll=10 * sw * osc(u, 3, 0.25), yaw=160 * d)
        p.rot('handle', x=-20 * d + 10 * sw * osc(u, 3, 0.3))
        p.rot('door', z=-40 * d + 20 * sw * osc(u, 4))
        R.dangle(p, ch, x=-30 * d, u=u, amp=10 * sw, n=3)
        wisp(p, sm(u, 0.35, 0.7) * (1 + 0.4 * bump(u, 0.55, 0.68, 0.72, 0.9)))
    C.clip('Spawn', 1.1, spawn)
    return C.finalize(bake=bake, notes="rig F proof")
