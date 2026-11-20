"""Rig F creatures (rigid / floating things): each has its own small skeleton. The proof is the lantern-ghost."""
import math
import numpy as np
import blib as B
from blib import M, L, V, unit, sm, bump, osc, rx, ry, rz, I3, rot_axis
import bparts as P
import rig_f as RF

_c = L._c
BUILD = {}


def reg(fn):
    BUILD[fn.__name__] = fn
    return fn


def k_warm(N, p):
    """iron lit by a small flame at `center` (object space): the metal look plus a baked warm light on the faces that
    look at the flame, fading out over `reach` metres"""
    r = L.KINDS['metal'](N, p)
    e = _c(p, 'emit', (1.0, 0.42, 0.08))
    sub = N.new('ShaderNodeVectorMath', operation='SUBTRACT')
    N.set(sub.inputs[0], tuple(float(x) for x in p['center'])); N.set(sub.inputs[1], N.coord())
    ln = N.new('ShaderNodeVectorMath', operation='LENGTH'); N.set(ln.inputs[0], sub.outputs[0])
    nm = N.new('ShaderNodeVectorMath', operation='NORMALIZE'); N.set(nm.inputs[0], sub.outputs[0])
    dt = N.new('ShaderNodeVectorMath', operation='DOT_PRODUCT')
    N.set(dt.inputs[0], nm.outputs[0]); N.set(dt.inputs[1], N.geo().outputs['Normal'])
    m = N.math('MULTIPLY', N.mapr(ln.outputs['Value'], _c(p, 'reach', 0.4), 0.04), N.mapr(dt.outputs['Value'], 0.02, 0.55))
    m = N.math('MULTIPLY', m, m)
    r['color'] = N.mix(N.math('MULTIPLY', m, 0.6), r['color'], tuple(x * 0.5 for x in e))
    r['emit'] = (e, _c(p, 'strength', 1.0))
    r['emask'] = N.math('MULTIPLY', m, _c(p, 'gain', 0.7))
    return r


def k_ghostface(N, p):
    """a face of glowing smoke: pale, lit from within, with dark hollows painted where the eye sockets and the mouth
    are (`holes` = [(centre, radius, z stretch), ...] in object space) and in every crease"""
    e = _c(p, 'emit', (0.6, 0.85, 1.0))
    col = _c(p, 'color', (0.55, 0.66, 0.72))
    st = N.noise(22, 3, 0.6, vec=N.mapping(N.warp(0.02, 6), scale=(1, 1, 0.22)))
    m = N.mapr(st, 0.2, 0.8, 0.62, 1.0)
    for c, r, zs in p.get('holes', []):                    # the hollows reach 2.6 radii deep into the head (+Y)
        vm = N.new('ShaderNodeVectorMath', operation='DISTANCE')
        N.set(vm.inputs[0], N.mapping(N.coord(), scale=(1, 1.0 / 2.6, 1.0 / zs)))
        N.set(vm.inputs[1], (float(c[0]), float(c[1]) / 2.6, float(c[2]) / zs))
        m = N.math('MULTIPLY', m, N.mapr(vm.outputs['Value'], r * 0.6, r * 1.2))
    m = N.math('MULTIPLY', m, N.mapr(N.cavity(), 0.75, 0.2, 0.35, 1.0))
    c = N.mix(m, (0.012, 0.02, 0.035), col)
    return dict(color=c, rough=0.85, metal=0.0, height=st, bump=(0.3, 0.004), emit=(e, _c(p, 'strength', 1.0)), emask=m)


L.KINDS.update(warm=k_warm, ghostface=k_ghostface)
L.GRIME.update(warm=((0.022, 0.017, 0.013), 0.85))
L.NO_FINISH = tuple(L.NO_FINISH) + ('ghostface',)


def shard(pts2, th=0.007):
    """flat jagged piece of glass from 2D points (u across, v up): a thin slab standing in the XZ plane"""
    q = M.prism(np.asarray(pts2, dtype=float), -th / 2, th / 2)
    q.rot('x', 90.0)
    q.smooth = 20.0
    return q


# ================================================================================================ wraith_gasp
@reg
def wraith_gasp(bake=True):
    """Lantern-ghost: a big dead iron lantern floating at chest height, its door hanging open and its panes broken to
    jagged shards; inside, over a dying orange ember, a gaunt hollow-eyed face of pale ghost-smoke that peers out and
    lunges; a carrying-chain with a hook sways underneath, cold light spills on the ground below"""
    h = 1.30
    a = 0.195                                                # post centres
    z0, z1 = h - 0.28, h + 0.28
    hinge = V(a, -a - 0.012, 0)
    em_c = V(0.0, 0.05, z0 + 0.135)                          # the ember
    gc = V(0.0, -0.07, h + 0.03)                             # centre of the ghost's head
    EYES = [gc + V(0.043, -0.10, 0.045), gc + V(-0.043, -0.10, 0.045)]
    MOUTH = gc + V(0, -0.088, -0.09)
    NOSE = gc + V(0, -0.108, -0.012)
    T1 = [V(0.01, -0.02, h + 0.15), V(0.03, 0.10, h + 0.20), V(-0.02, 0.24, h + 0.19), V(0.03, 0.40, h + 0.24)]
    T2 = [V(-0.06, -0.01, h + 0.05), V(-0.10, 0.12, h + 0.07), V(-0.07, 0.27, h + 0.04), V(-0.12, 0.39, h + 0.08)]
    T3 = [V(0.06, 0.0, h - 0.04), V(0.09, 0.11, h - 0.02), V(0.05, 0.22, h - 0.06), V(0.09, 0.31, h - 0.03)]
    sk = B.Sk()
    sk.bone('body', (0, 0, z0), (0, 0, z1), 'root')
    sk.bone('door', hinge + V(0, 0, z1 - 0.04), hinge + V(0, 0, z0 + 0.04), 'body')
    sk.bone('handle', (0, 0, z1 + 0.245), (0, 0, z1 + 0.44), 'body')
    sk.bone('shards', (0, 0, h), (0, 0, h + 0.12), 'body')
    sk.bone('ember', em_c, em_c + V(0, 0, 0.05), 'body')
    sk.bone('wisp', gc + V(0, 0.05, -0.1), gc + V(0, 0.05, 0.1), 'body')
    for i, T in enumerate((T1, T2, T3)):
        sk.bone('smoke%d' % (i + 1), T[0], T[2], 'wisp')
    ch = sk.chain('chain', [(0, 0, z0 - 0.01 - 0.115 * i) for i in range(6)], 'body')
    C = B.Beast('mon_wraith_gasp', sk, budget=4000, bone_budget=28, mb=2.5, tex=1024)     # swarmer
    C.meta.update(family='wraith', rig='F', scale=1.0, game_speed=5.0)
    # the pool of cold light it throws on the ground: a soft shadowless light the game hangs on the creature
    C.meta['light'] = dict(color=[0.55, 0.78, 1.0], energy=0.9, range=3.2, height=round(h - 0.05, 2))
    R = RF.Rigid(C, 'body', hover=h)
    iron = dict(color=(0.15, 0.14, 0.135), rust=(0.22, 0.09, 0.03), rust2=(0.07, 0.035, 0.02), amount=0.8, rough=0.55, edge_color=(0.34, 0.31, 0.28))
    C.look('iron', 'metal', 'metal_iron', **iron)
    C.look('inner', 'warm', 'metal_iron', center=em_c, reach=0.42, strength=1.0, gain=0.8, **iron)
    C.look('glass', 'glass', 'metal_iron', color=(0.02, 0.022, 0.028), band=(0.07, 0.075, 0.09), rough=0.12)
    C.look('wax', 'wax', 'metal_iron', color=(0.50, 0.44, 0.31), soot=0.7)
    C.look('ember', 'emis', 'emis_ember', emit=(1.0, 0.40, 0.06), strength=1.6, mottle=0.6, base_mul=0.6, scale=60.0, crust=0.4)
    C.look('ghost', 'emis', 'spectral_ghost', emit=(0.55, 0.80, 1.0), strength=1.0, mottle=0.5, base_mul=0.7, scale=25.0)
    C.look('face', 'ghostface', 'emis_face', emit=(0.62, 0.86, 1.0), strength=1.0,
           holes=[(EYES[0], 0.036, 1.15), (EYES[1], 0.036, 1.15), (MOUTH, 0.046, 1.15), (NOSE, 0.016, 1.6)])
    # ---- frame: floor (skinned: the host of everything), posts, top, roof, ring
    floor = M.box(0.47, 0.47, 0.045, bevel=0.006).move(0, 0, z0 + 0.0225)
    C.skin(B.wbones(floor, ['body']), 'inner', smooth=30)
    fr = [M.box(0.47, 0.47, 0.04, bevel=0.006).move(0, 0, z1 - 0.02)]
    for sx in (1, -1):
        for sy in (1, -1):
            fr.append(M.box(0.036, 0.036, 0.56).move(sx * a, sy * a, h))
    C.rigid(M.join(fr), 'body', 'inner', smooth=30)
    roof = M.loft_z([(z1 - 0.002, 0, 0, 0.32, 0.32), (z1 + 0.03, 0, 0, 0.295, 0.295), (z1 + 0.155, 0, 0, 0.11, 0.11), (z1 + 0.163, 0, 0, 0.08, 0.08),
                     (z1 + 0.215, 0, 0, 0.08, 0.08), (z1 + 0.223, 0, 0, 0.11, 0.11), (z1 + 0.255, 0, 0, 0.02, 0.02)], n=4, phase=math.pi / 4)
    roof.smooth = 25.0
    C.rigid(roof, 'body', 'iron')
    C.rigid(M.box(0.03, 0.018, 0.045).move(0, 0, z1 + 0.25), 'body', 'iron', smooth=30)
    C.rigid(P.ring((0, 0, z1 + 0.245 + 0.095), 0.095, 0.012, axis=(0, 1, 0), n=16, m=6), 'handle', 'iron', smooth=60)
    # ---- broken panes: jagged shards left in the corners of the three closed sides (the inside shows from every side)
    A = [(-0.177, 0.0), (0.06, 0.0), (0.0, 0.10), (-0.05, 0.13), (-0.10, 0.27), (-0.177, 0.33)]
    Bq = [(0.177, 0.475), (0.177, 0.25), (0.12, 0.31), (0.09, 0.40), (0.0, 0.475)]
    Cq = [(0.177, 0.0), (0.177, 0.12), (0.13, 0.05), (0.10, 0.0)]
    sh = []
    for rot, flip in ((180.0, False), (90.0, True), (-90.0, False)):          # back, left (+X... seen from outside), right
        for poly in (A, Bq, Cq):
            pts2 = [(-u, v) for u, v in poly][::-1] if flip else poly
            s_ = shard(pts2)
            s_.move(0, -a, z0 + 0.045).rot('z', rot)
            sh.append(s_)
    C.rigid(M.join(sh), 'shards', 'glass', smooth=20, free=True)
    # ---- the door: iron frame with one shard left, hanging open on its hinges
    dr = [M.box(0.39, 0.018, 0.034).move(-0.195, 0, z1 - 0.06), M.box(0.39, 0.018, 0.034).move(-0.195, 0, z0 + 0.07),
          M.box(0.03, 0.018, 0.47).move(-0.015, 0, h), M.box(0.03, 0.018, 0.47).move(-0.375, 0, h),
          M.box(0.045, 0.03, 0.024).move(-0.39, -0.014, h)]                      # latch
    door = M.join(dr)
    dsh = shard([(-0.36, 0.0), (-0.13, 0.0), (-0.2, 0.09), (-0.26, 0.11), (-0.3, 0.24), (-0.36, 0.29)]).move(0, 0, z0 + 0.085)
    for q, lk in ((door, 'iron'), (dsh, 'glass')):
        q.rot('z', 112).rot('y', -4, pivot=(0, 0, z1)).move(hinge[0], hinge[1], 0)
        C.rigid(q, 'door', lk, smooth=25)
    C.rigid(M.join([M.box(0.02, 0.04, 0.04).move(hinge[0] + 0.004, hinge[1] + 0.004, z1 - 0.07),
                    M.box(0.02, 0.04, 0.04).move(hinge[0] + 0.004, hinge[1] + 0.004, z0 + 0.09)]), 'body', 'iron', smooth=25)
    # ---- a guttered candle stub with a dying ember
    cb = z0 + 0.045
    wax = P.lathe([(0.062, 0.0), (0.058, 0.02), (0.05, 0.06), (0.046, 0.078), (0.02, 0.084)], n=12).move(em_c[0], em_c[1], cb - 0.002)
    wax.rough(0.004, 30.0, seed=3)
    C.rigid(wax, 'body', 'wax', smooth=180)
    C.rigid(P.blob(em_c, 0.022, 0.022, 0.018, n=8, m=5), 'ember', 'ember', smooth=180)
    # ---- the ghost: a gaunt face of smoke with hollow eyes and a gaping mouth, a stem down to the ember, three tendrils
    head = P.blob(gc, 0.10, 0.11, 0.155, n=18, m=11)
    k = np.clip((gc[2] + 0.01 - head.v[:, 2]) / 0.14, 0, 1)
    head.v[:, 0] *= 1 - 0.45 * k                                    # long narrow jaw
    for sg in (1, -1):
        head.dent(gc + V(sg * 0.075, -0.07, -0.035), 0.05, -0.024)  # sunken cheeks
        head.dent(gc + V(sg * 0.045, -0.10, 0.095), 0.04, 0.012)    # brow
        head.dent(gc + V(sg * 0.07, -0.085, 0.01), 0.03, 0.008)     # cheekbone
    cut = False
    try:
        hb = head
        for c, r3, tilt in ((EYES[0], (0.034, 0.07, 0.038), -12.0), (EYES[1], (0.034, 0.07, 0.038), 12.0), (MOUTH, (0.045, 0.07, 0.05), 0.0),
                            (NOSE, (0.013, 0.05, 0.024), 0.0)):
            e = M.ellipsoid(r3[0], r3[1], r3[2], n=10, m=6)
            e.rot('y', tilt).move(*c)
            hb = M.boolean(hb, e, 'DIFFERENCE')
        if hb.ntri > head.ntri * 0.8:
            head = hb.recalc()
            cut = True
    except Exception as ex:
        B.log("ghost face boolean failed:", ex)
    if not cut:
        for c in EYES + [MOUTH, NOSE]:
            head.dent(c, 0.035, -0.04)
    head.smooth = 60.0
    C.rigid(head, 'wisp', 'face')
    # a cowl of thinner smoke round the back of the head (ghost matter: additive in the game)
    veil = P.blob(gc + V(0, 0.055, 0.0), 0.125, 0.125, 0.185, n=14, m=8)
    C.rigid(veil, 'wisp', 'ghost', smooth=180)
    stem = P.tubep([gc + V(0, 0.02, -0.09), V(0.02, -0.02, h - 0.14), V(-0.01, 0.03, h - 0.19), em_c + V(0, 0, 0.02)], [0.062, 0.04, 0.024, 0.012],
                   m=12, n=8)
    wz = np.clip((stem.v[:, 2] - (em_c[2] + 0.02)) / (gc[2] - 0.09 - em_c[2] - 0.02), 0, 1)
    Wm = np.zeros((len(stem.v), C.rig.nb), dtype=np.float32)
    Wm[:, C.rig.idx['wisp']] = wz; Wm[:, C.rig.idx['body']] = 1 - wz
    stem.W = Wm
    C.skin(stem, 'ghost', smooth=180)
    for i, T in enumerate((T1, T2, T3)):
        t = P.tubep([T[0] - unit(T[1] - T[0]) * 0.03] + T, [0.03, 0.026, 0.02, 0.012, 0.003], [0.045, 0.04, 0.03, 0.018, 0.004], m=14, n=6)
        C.rigid(t, 'smoke%d' % (i + 1), 'ghost', smooth=180)
    # ---- carrying chain under the floor, ending in a hook
    C.rigid(M.box(0.045, 0.045, 0.03).move(0, 0, z0 - 0.008), 'body', 'iron', smooth=25)
    RF.chain_parts(C, ch, 'iron', link=0.075, r=0.0095)
    zc = z0 - 0.01 - 0.115 * 5
    hook = P.tubep([(0, 0, zc + 0.02), (0, 0.0, zc - 0.05), (0, 0.04, zc - 0.095), (0, 0.09, zc - 0.07)], [0.0105, 0.0105, 0.009, 0.004], m=8, n=6)
    C.rigid(hook, ch[-1], 'iron', smooth=60)
    front = V(0, -0.3, h)

    # ---- motion
    def ghost(p, s=1.0, yaw=0.0, pitch=0.0, out=0.0, up=0.0, gape=0.0, u=0.0, wave=6.0, trail=0.0):
        """s = size, out = pushes forward out of the door, gape = the face stretches into a scream"""
        s = max(0.001, s)
        p.rot('wisp', x=pitch, z=yaw)
        p.scl('wisp', (s, s * (1 + gape), s))
        p.move('wisp', 0, -out, up)
        for i in (1, 2, 3):
            p.rot('smoke%d' % i, x=trail + wave * osc(u, 2, 0.21 * i), z=wave * 1.3 * osc(u, 1, 0.3 * i), y=wave * osc(u, 2, 0.1 + 0.17 * i))

    def glow(p, s=1.0, dx=0.0, dy=0.0, ember=1.0):
        p.scl('ember', max(0.001, ember))

    def idle(p, u):
        R.body(p, dz=0.035 * osc(u, 2), dx=0.02 * osc(u, 1), yaw=10 * osc(u, 1), pitch=3 * osc(u, 2, 0.1), roll=3 * osc(u, 1, 0.3))
        p.rot('handle', y=8 * osc(u, 2, 0.2), x=4 * osc(u, 2, 0.4))
        p.rot('door', z=7 * osc(u, 2, 0.4) + 3 * osc(u, 6) * bump(u, 0.55, 0.6, 0.7, 0.8))
        R.dangle(p, ch, u=u, amp=9, n=2, lag=0.13, grow=0.25)
        ghost(p, 1 + 0.05 * osc(u, 3), yaw=38 * osc(u, 1, 0.1), pitch=6 * osc(u, 2), out=0.02 + 0.02 * osc(u, 2, 0.3), gape=0.06 + 0.06 * osc(u, 2, 0.2), u=u)
        glow(p, 1 + 0.06 * osc(u, 3), ember=1 + 0.15 * osc(u, 5))
    C.clip('Idle', 4.0, idle)

    def travel(lean, trail, flap, out):
        def fn(p, u):
            R.body(p, dz=0.03 * osc(u, 2), pitch=lean + 3 * osc(u, 2, 0.15), roll=4 * osc(u, 1), yaw=5 * osc(u, 1, 0.25))
            p.rot('handle', x=-lean * 0.8 + 6 * osc(u, 2, 0.3), y=6 * osc(u, 1))
            p.rot('door', z=flap * (0.4 + 0.6 * osc(u, 2, 0.1)))
            R.dangle(p, ch, x=trail, u=u, amp=8, n=2, lag=0.13, grow=0.25)
            ghost(p, 1.05 + 0.05 * osc(u, 2), yaw=8 * osc(u, 1), pitch=-lean * 0.5, out=out + 0.02 * osc(u, 2), gape=0.12 + 0.06 * osc(u, 2), u=u, trail=trail * 0.6)
            glow(p, 1 + 0.05 * osc(u, 2), ember=1 + 0.2 * osc(u, 4))
        return fn
    C.clip('Walk', 1.2, travel(9.0, 16.0, 8.0, 0.04)); C.meta['walk_speed'] = 1.8
    C.clip('Run', 0.8, travel(20.0, 38.0, 18.0, 0.09)); C.meta['run_speed'] = 5.0

    def lunge(p, u):                      # Attack1: lunge-flare - the lantern swings forward and the face bursts out of the door
        hu = 0.4 / 0.8
        w = bump(u, 0.0, hu * 0.5, hu * 0.7, hu)
        s = bump(u, hu * 0.7, hu, hu + 0.1, 0.95)
        f = bump(u, hu * 0.8, hu, hu + 0.08, hu + 0.3)
        R.body(p, dy=0.16 * w - 0.6 * s, dz=0.08 * w - 0.14 * s, pitch=-14 * w + 24 * s, yaw=-8 * w)
        p.rot('handle', x=16 * w - 30 * s)
        p.rot('door', z=-25 * w + 30 * s)
        R.dangle(p, ch, x=-20 * w + 50 * s, u=u, amp=6, n=3, grow=0.25)
        ghost(p, 1 - 0.2 * w + 0.45 * f, pitch=-10 * w + 8 * s, out=0.02 - 0.04 * w + 0.2 * f, gape=0.06 + 0.5 * f, u=u, trail=30 * s)
        glow(p, 1 - 0.2 * w + 0.5 * f, dy=0.16 * w - 0.6 * s, ember=1 + 0.6 * f)
    C.clip('Attack1', 0.8, lunge, hit=0.4)
    C.reach('Attack1', 'body', front)

    def shatter(p, u):                    # Attack2: the face swells until it bursts out of the lantern on every side
        hu = 0.7 / 1.2
        g = sm(u, 0.0, hu)
        b = bump(u, hu - 0.02, hu, hu + 0.05, hu + 0.3)
        shk = g * (1 - sm(u, hu, hu + 0.1))
        R.body(p, dz=0.1 * g * (1 - sm(u, hu, 0.95)) + 0.03 * b, pitch=5 * shk * osc(u, 17), roll=5 * shk * osc(u, 13, 0.2), yaw=6 * shk * osc(u, 11, 0.4))
        p.rot('handle', y=10 * shk * osc(u, 15), x=-40 * b)
        p.rot('door', z=8 * shk * osc(u, 19) + 50 * b)
        p.scl('shards', 1 + 0.9 * b); p.rot('shards', z=25 * b)
        R.dangle(p, ch, u=u, amp=8 * shk + 20 * b, n=8, lag=0.2, grow=0.25)
        ghost(p, 1 + 0.35 * g * g * (1 - sm(u, hu, hu + 0.04)) + 0.9 * b - 0.3 * bump(u, hu + 0.1, hu + 0.2, 0.8, 0.98),
              yaw=20 * shk * osc(u, 9), gape=0.06 + 0.3 * g + 0.4 * b, u=u, wave=6 + 14 * b)
        glow(p, 1 + 0.3 * g + 0.8 * b, ember=1 + 0.8 * g)
    C.clip('Attack2', 1.2, shatter, hit=0.7)
    C.meta['attack_reach'] = max(C.meta.get('attack_reach', 0.0), 1.2)

    def hit(p, u):
        k = bump(u, 0.0, 0.2, 0.3, 1.0)
        R.body(p, dy=0.16 * k, dz=-0.06 * k, pitch=-22 * k, yaw=40 * k, roll=14 * k)
        p.rot('handle', x=30 * k, y=-20 * k)
        p.rot('door', z=30 * k)
        R.dangle(p, ch, x=-35 * k, u=u, amp=12 * k, n=2, grow=0.25)
        ghost(p, 1 - 0.35 * k, yaw=-30 * k, out=0.02 - 0.05 * k, gape=0.06 + 0.3 * k, u=u, wave=6 + 10 * k)
        glow(p, 1 - 0.4 * k, dy=0.16 * k, ember=1 - 0.5 * k)
    C.clip('Hit', 0.4, hit)

    land = 0.42

    def death(p, u):                      # the ghost tears loose upward and is gone, the ember dies, the lantern drops and tips over
        f = 1 - RF.fall_curve(u, land)
        on = sm(u, land - 0.02, land + 0.02)
        tip = sm(u, land + 0.02, land + 0.3) ** 1.5
        bn = bump(u, land, land + 0.05, land + 0.08, land + 0.2)
        go = sm(u, 0.0, 0.3)
        R.body(p, dz=-(h - 0.3) * f + 0.05 * bn, roll=92 * tip, yaw=35 * f, pitch=10 * bump(u, 0.05, 0.2, land - 0.05, land))
        p.rot('handle', y=70 * tip, x=20 * on)
        p.rot('door', z=35 * on + 20 * tip)
        R.dangle(p, ch, x=88 * sm(u, 0.05, land * 0.8), u=u, amp=8 * (1 - on), n=3, grow=0.25)
        ghost(p, (1 + 0.3 * bump(u, 0.02, 0.1, 0.14, 0.24)) * (1 - go), up=0.9 * go, gape=0.06 + 0.7 * bump(u, 0.0, 0.1, 0.2, 0.3), u=u, wave=6 + 12 * go)
        glow(p, 1 - go, ember=1 - sm(u, 0.1, 0.35))
    C.clip('Death', 1.5, death, floor='body', settle=(land, land + 0.1))

    def spawn(p, u):                      # drops in dark; the ember catches, the face wells up out of it
        r = sm(u, 0.0, 0.5)
        d = 1 - r
        sw = bump(u, 0.4, 0.55, 0.7, 0.98)
        lit = sm(u, 0.45, 0.8)
        R.body(p, dz=2.6 * d * d + 0.05 * sw * osc(u, 3), pitch=14 * d + 6 * sw * osc(u, 3), roll=10 * sw * osc(u, 3, 0.25), yaw=160 * d)
        p.rot('handle', x=-20 * d + 10 * sw * osc(u, 3, 0.3))
        p.rot('door', z=-40 * d + 20 * sw * osc(u, 4))
        R.dangle(p, ch, x=-30 * d, u=u, amp=12 * sw, n=3, grow=0.25)
        ghost(p, lit * (1 + 0.3 * bump(u, 0.6, 0.72, 0.78, 0.95)), up=-0.12 * (1 - lit), gape=0.06 + 0.4 * bump(u, 0.55, 0.7, 0.8, 0.95), u=u)
        glow(p, lit, ember=sm(u, 0.35, 0.55))
    C.clip('Spawn', 1.3, spawn)
    return C.finalize(bake=bake, notes="rig F proof")
