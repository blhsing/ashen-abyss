"""asm_weapons.py -- WEAPONS LIBRARY of the creature assembler (asm.py).

A weapon is a function  fn(A, hand='r', **kwargs)  registered with @weapon; a spec uses it as {w: '<name>', hand: ...}.
It is modelled in WEAPON SPACE: origin = centre of the closed fist, +Y = along the shaft / blade (pinky -> index),
+X = leading edge (towards the knuckles = along the forearm, outwards), Z = thickness (the back of the hand is +Z for
the right hand and -Z for the left).  emit() moves the pieces into the rest-pose fist (kit grip frame measured from
the UAL weapon clips) and adds them as mesh object wpn_<name>, 100 % on hand_r / hand_l.  Look 'wood' gets its grain
along the shaft automatically.  Bows do not exist on this rig: ranged types throw (javelin, disc, pot) or cast.
"""
import math
import numpy as np
import monlib as M
import kit_bones as KB
import kit_b as KB2
import kit_c as KC3
import kit_wisp as KW
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, blade, unit
import asm_parts as AP

WEAPONS = {}


def weapon(fn):
    WEAPONS[fn.__name__] = fn
    return fn


def emit(A, parts, name, hand='r', tilt=0.0):
    """[(Part, look, smooth)] in weapon space -> wpn_<name> in the fist of `hand`"""
    obj = 'wpn_' + name
    ax = KX.weapon_axis(tilt, hand)
    for p, lk, sm in KX.to_hand(list(parts), tilt=tilt, side=hand):
        if lk == 'wood':
            lk = A._wood('wood_' + name, ax, 'wood')
        A.add(p, look=lk, obj=obj, smooth=sm)


def cyl(p0, p1, r, n=7, r1=None):
    return KC3.cyl(V(p0), V(p1), r, n=n, r1=r1)


def shaft(back, front, r=0.016, n=7, look='wood', wraps=0, wrap_look='strap'):
    ps = [(tube([(0, -back, 0), (0, 0, 0), (0, front, 0)], r, n=n), look, 50)]
    for k in range(wraps):
        y = -0.04 + 0.08 * k
        ps.append((tube([(0, y - 0.03, 0), (0, y + 0.03, 0)], r + 0.004, n=n), wrap_look, 50))
    return ps


def dorsal(hand):
    return 1.0 if hand == 'r' else -1.0


# ------------------------------------------------------------------------------------------------ blades
@weapon
def sword(A, hand='r', length=0.80, notched=True, look='blade', hilt='rust', grip='strap', name='sword', width=1.0):
    """arming sword with a curved guard; notched = nicks along the edges"""
    g = tube([(0, -0.072, 0), (0, -0.02, 0), (0, 0.058, 0)], [0.0125, 0.0135, 0.0115], n=7)
    pommel = ellipsoid(0.025, 0.016, 0.021, n=10, m=6).move(0, -0.086, 0)
    guard = box(0.19, 0.02, 0.024, bevel=0.005).move(0.012, 0.068, 0)
    guard.v[:, 1] += 0.5 * np.abs(guard.v[:, 0]) ** 2 * 6.0
    L_ = length; w = 0.027 * width
    st = [(0.076, w, w, 0.0075), (0.19, w * 0.98, w * 0.98, 0.0072)]
    if notched:
        st += [(0.205, w * 0.98, w * 0.55, 0.007), (0.225, w * 0.96, w * 0.96, 0.007), (0.40, w * 0.9, w * 0.9, 0.0066),
               (0.415, w * 0.5, w * 0.9, 0.0064), (0.44, w * 0.89, w * 0.89, 0.0064)]
    st += [(L_ * 0.7, w * 0.82, w * 0.82, 0.006), (L_ - 0.09, w * 0.67, w * 0.67, 0.005), (L_ - 0.03, w * 0.4, w * 0.45, 0.004), (L_, 0.0, 0.002, 0.001)]
    emit(A, [(g, grip, 40), (pommel, hilt, 40), (guard, hilt, 40), (blade(st), look, 22)], name, hand)


@weapon
def dagger(A, hand='r', length=0.36, bend=-150.0, look='iron', grip='strap', name='dagger'):
    """curved ritual dagger (the Order's)"""
    s = length / 0.36
    g = tube([(0, -0.052, 0), (0, 0.0, 0), (0, 0.046, 0)], [0.0115, 0.013, 0.0115], n=7)
    pommel = torus(0.016, 0.006, n=10, m=5).rot('y', 90).move(0, -0.07, 0)
    guard = box(0.07, 0.014, 0.022, bevel=0.004).move(0.004, 0.054, 0)
    st = [(0.058, 0.019, 0.019, 0.006), (0.058 + 0.062 * s, 0.021, 0.019, 0.0058), (0.058 + 0.142 * s, 0.019, 0.016, 0.005),
          (0.058 + 0.222 * s, 0.013, 0.011, 0.004), (0.058 + 0.272 * s, 0.006, 0.005, 0.003), (0.058 + 0.302 * s, 0.0, 0.001, 0.001)]
    bl = blade(st)
    if bend:
        bl.bend('y', 'z', bend, origin=0.058)
    emit(A, [(g, grip, 40), (pommel, look, 40), (guard, look, 40), (bl, look, 22)], name, hand)


@weapon
def sickle(A, hand='r', reach=0.5, curve=330.0, look='iron', grip='strap', pommel='cone', name='sickle', handle=0.16, width=0.03):
    """long hooked blade on a short handle (snuffer-hook: pommel='cone' is a small snuffer cone)"""
    g = tube([(0, -0.07, 0), (0, handle - 0.07, 0)], 0.0125, n=7)
    y0 = handle - 0.075
    n = 9
    st = []
    for i in range(n):
        t = i / (n - 1)
        wd = width * (0.75 + 0.5 * math.sin(math.pi * min(t * 1.2, 1.0))) * (1 - t ** 3)
        st.append((y0 + reach * t, wd * 0.35, wd, 0.006 * (1 - 0.6 * t)))
    bl = blade(st)
    bl.bend('y', 'z', -curve, origin=y0 + reach * 0.25)
    ps = [(g, grip, 40), (bl, look, 22), (tube([(0, y0 - 0.01, 0), (0, y0 + 0.03, 0)], 0.016, n=7), look, 40)]
    if pommel == 'cone':
        ps.append((revolve([(0.0, 0.0), (0.012, -0.004), (0.03, -0.05), (0.033, -0.055), (0.027, -0.055)], n=8).rot('x', -90).move(0, -0.07, 0), look, 40))
    emit(A, ps, name, hand)


def axe_head(w=0.16, h=0.17, th=0.022, beard=0.3):
    """axe bit in weapon space at the origin: socket on the shaft, cutting edge towards +X"""
    out = np.array([(0.0, -h * 0.22), (w * 0.45, -h * (0.34 + beard * 0.3)), (w, -h * (0.5 + beard * 0.5)), (w * 1.06, 0.0),
                    (w, h * 0.5), (w * 0.45, h * 0.3), (0.0, h * 0.22)])
    p = M.prism(out, -th / 2, th / 2)
    t = np.clip(p.v[:, 0] / w, 0, 1)
    p.v[:, 2] *= (1 - 0.86 * t)
    sock = box(0.05, h * 0.5, th * 1.7, bevel=0.005)
    return join([p, sock])


@weapon
def axe(A, hand='r', length=0.55, head=1.0, look='iron', name='axe', back=0.1, r=0.015, beard=0.3, spike=False):
    """hatchet / axe: wooden haft, bearded iron bit, optional back spike"""
    ps = shaft(back, length, r, wraps=1)
    hd = axe_head(0.16 * head, 0.17 * head, 0.022, beard).move(0, length - 0.09 * head, 0)
    ps.append((hd, look, 25))
    if spike:
        ps.append((M.cone_spike(0.016, 0.09, n=5).rot('y', -90).move(-0.02, length - 0.09 * head, 0), look, 30))
    emit(A, ps, name, hand)


@weapon
def mattock(A, hand='r', length=0.62, look='rust', name='mattock', back=0.12):
    """grave-digger's mattock: pick spike one way, adze blade the other"""
    ps = shaft(back, length, 0.017, wraps=1)
    y = length - 0.05
    pick = M.horn(M.polyline([(0.0, y, 0), (0.12, y + 0.012, 0), (0.25, y - 0.03, 0)], 7), 0.02, n=5, r_tip=0.003)
    adze = blade([(0.0, 0.03, 0.03, 0.02), (0.07, 0.04, 0.04, 0.014), (0.15, 0.062, 0.062, 0.006), (0.165, 0.064, 0.064, 0.002)], sharp_tip=False)
    adze.rot('y', 90).rot('z', 102).move(-0.01, y, 0)       # along -X, edge across the shaft
    eye = box(0.07, 0.065, 0.05, bevel=0.008).move(0, y, 0)
    ps += [(pick, look, 30), (adze, look, 25), (eye, look, 30)]
    emit(A, ps, name, hand)


@weapon
def hammer(A, hand='r', length=0.34, head=(0.11, 0.05, 0.05), look='iron', name='hammer', back=0.08, r=0.013):
    """hammer / maul: square head across the haft (head = length, height, thickness)"""
    ps = shaft(back, length, r)
    ps.append((box(head[0], head[1], head[2], bevel=min(head) * 0.14).move(0.0, length - head[1] * 0.4, 0), look, 30))
    emit(A, ps, name, hand)


@weapon
def spear(A, hand='r', back=0.55, front=1.0, head=0.3, look='iron', name='spear', r=0.015, width=0.045, tilt=0.0):
    """spear: shaft through the fist, leaf blade + socket; tilt leans it like a carried staff"""
    ps = shaft(back, front, r)
    y = front
    ps.append((revolve([(r * 1.05, -0.07), (r * 1.3, -0.06), (r * 1.2, 0.0), (r * 0.8, 0.03)], n=7).rot('x', -90).move(0, y, 0), look, 40))
    ps.append((blade([(y + 0.0, 0.012, 0.012, 0.012), (y + head * 0.3, width, width, 0.009), (y + head * 0.75, width * 0.6, width * 0.6, 0.006),
                      (y + head, 0.0, 0.001, 0.001)]), look, 22))
    emit(A, ps, name, hand, tilt)


@weapon
def pole(A, hand='r', back=0.9, front=1.6, head='snuffer', look='brass', name='pole', r=0.017, tilt=0.0, size=1.0, glow=None):
    """long pole with a head: snuffer (bell-shaped cone hung on a goose-neck), hook, fork, cage, crescent"""
    ps = shaft(back, front, r, wraps=2)
    y = front; s = size
    if head == 'snuffer':
        neck = tube(M.polyline([(0, y - 0.03, 0), (0, y + 0.12 * s, 0), (0.07 * s, y + 0.22 * s, 0), (0.17 * s, y + 0.2 * s, 0)], 9), 0.012, n=6)
        cone = revolve([(0.0, 0.0), (0.02 * s, -0.01 * s), (0.055 * s, -0.09 * s), (0.105 * s, -0.25 * s), (0.125 * s, -0.31 * s),
                        (0.128 * s, -0.33 * s), (0.112 * s, -0.33 * s), (0.09 * s, -0.25 * s), (0.04 * s, -0.09 * s)], n=14, cap=False)
        cone.rot('x', -90).rot('z', 180).rot('z', -8).move(0.17 * s, y + 0.2 * s, 0)       # opening looks back down the pole
        ps += [(neck, look, 50), (cone, look, 60), (ellipsoid(0.022 * s, 0.022 * s, 0.022 * s, n=8, m=5).move(0.17 * s, y + 0.2 * s, 0), look, 50)]
        ps.append((revolve([(r, -0.05), (r * 1.6, -0.03), (r * 1.6, 0.03), (r, 0.05)], n=7).rot('x', -90).move(0, y - 0.04, 0), look, 40))
        if glow:
            ps.append((ellipsoid(0.07 * s, 0.07 * s, 0.01, n=10, m=4).rot('y', 90).rot('z', -8).move(0.17 * s, y - 0.1 * s, 0), glow, 60))
    elif head == 'hook':
        hk = M.horn(M.polyline([(0, y - 0.02, 0), (0.0, y + 0.1 * s, 0), (0.08 * s, y + 0.19 * s, 0), (0.18 * s, y + 0.13 * s, 0), (0.2 * s, y + 0.02 * s, 0)], 11),
                    0.014, n=6, r_tip=0.003)
        ps += [(hk, look, 40)]
    elif head == 'lamplighter':                      # hook to pull lamps down + a small snuffer cone
        hk = M.horn(M.polyline([(0, y - 0.02, 0), (0.0, y + 0.08 * s, 0), (0.07 * s, y + 0.16 * s, 0), (0.15 * s, y + 0.10 * s, 0), (0.16 * s, y + 0.0 * s, 0)], 11),
                    0.012, n=6, r_tip=0.003)
        cone = revolve([(0.0, 0.0), (0.012 * s, -0.006 * s), (0.04 * s, -0.07 * s), (0.065 * s, -0.15 * s), (0.068 * s, -0.16 * s), (0.055 * s, -0.16 * s)], n=10, cap=False)
        cone.rot('x', -90).rot('z', 180).move(-0.09 * s, y + 0.10 * s, 0)
        arm = tube([(0, y + 0.02, 0), (-0.05 * s, y + 0.09 * s, 0), (-0.09 * s, y + 0.10 * s, 0)], 0.008, n=5)
        ps += [(hk, look, 40), (cone, look, 60), (arm, look, 40)]
    elif head == 'halberd':                          # axe blade + top spike + back fluke
        hd = axe_head(0.20 * s, 0.26 * s, 0.022, 0.2).move(0, y - 0.06 * s, 0)
        ps += [(hd, look, 25), (M.cone_spike(0.016, 0.30 * s, n=5).rot('x', -90).move(0, y, 0), look, 30),
               (M.cone_spike(0.014, 0.11 * s, n=5).rot('y', -90).move(-0.02, y - 0.06 * s, 0), look, 30)]
    elif head == 'fork':
        for sx in (-1, 1):
            ps.append((M.horn(M.polyline([(0, y - 0.02, 0), (sx * 0.07 * s, y + 0.06 * s, 0), (sx * 0.08 * s, y + 0.3 * s, 0)], 7), 0.013, n=5, r_tip=0.002), look, 40))
    elif head == 'trident':                           # three barbed prongs (eel-spear)
        ps.append((box(0.19 * s, 0.03, 0.022, bevel=0.005).move(0, y, 0), look, 30))
        for sx in (-1, 0, 1):
            L_ = (0.34 if sx == 0 else 0.28) * s
            ps.append((M.horn(np.array([(sx * 0.082 * s, y, 0), (sx * 0.082 * s, y + L_ * 0.6, 0), (sx * 0.082 * s, y + L_, 0)]), 0.011, n=5, r_tip=0.002), look, 30))
            for b in (1, -1) if sx == 0 else (-sx,):
                ps.append((M.cone_spike(0.007, 0.05, n=4).rot('z', b * 150).move(sx * 0.082 * s, y + L_ * 0.86, 0), look, 30))
    elif head == 'crescent':
        a = np.radians(np.linspace(-70, 250, 17))
        pts = np.stack([0.15 * s * np.cos(a), y + 0.17 * s + 0.15 * s * np.sin(a), 0 * a], axis=1)
        rr = 0.004 + 0.016 * np.sin(np.linspace(0, math.pi, 17))
        ps.append((tube(pts, 0.006, n=6, r2=rr, up=(0, 0, 1), power=1.3), look, 30))
    emit(A, ps, name, hand, tilt)


@weapon
def staff(A, hand='r', back=0.85, front=0.9, top='knob', look='iron', name='staff', r=0.018, tilt=0.0):
    """walking staff: knob | crook (kit crozier volute) | lantern (hung from an arm at the top)"""
    ps = shaft(back, front, r, wraps=1)
    if top == 'crook':
        ps.append((KX.crook().move(0, front, 0), 'wood', 50))
    elif top == 'lantern':
        arm = tube(M.polyline([(0, front - 0.02, 0), (0, front + 0.1, 0), (0.1, front + 0.14, 0), (0.17, front + 0.1, 0)], 8), 0.01, n=5)
        ps.append((arm, look, 40))
        for p, lk in AP.item_parts(A, 'lantern', 0.9):
            ps.append((p.rot('x', 90).move(0.17, front + 0.1, 0), lk, 50))
    else:
        ps.append((ellipsoid(r * 1.9, r * 2.2, r * 1.9, n=8, m=6).move(0, front, 0), look, 50))
    emit(A, ps, name, hand, tilt)


@weapon
def torch(A, hand='r', length=0.32, lit=False, name='torch', back=0.08, head_look='rag', glow='ember'):
    """torch stub: short stick, pitch-soaked wrapped head (snuffed = charred and cold, lit = ember core)"""
    ps = shaft(back, length, 0.014)
    hd = ellipsoid(0.034, 0.075, 0.034, n=9, m=7)
    hd.rough(0.006, 30.0, seed=2.0)
    ps.append((hd.move(0, length + 0.03, 0), head_look, 60))
    for k in range(3):
        ps.append((torus(0.03 - 0.003 * k, 0.005, n=9, m=4).rot('x', 90).move(0, length - 0.02 + 0.035 * k, 0), 'strap', 50))
    if lit:
        ps.append((ellipsoid(0.022, 0.04, 0.022, n=7, m=5).move(0, length + 0.085, 0), glow, 60))
    emit(A, ps, name, hand)


@weapon
def censer(A, hand='r', chain=0.42, size=1.6, name='censer', lit=True, axis=(0.25, -1.0, 0.0), look='silver'):
    """censer swung on a chain: handle ring in the fist, chain, pierced ball (hangs along `axis` in weapon space)"""
    d = unit(V(axis))
    ring = torus(0.028, 0.006, n=10, m=4).rot('x', 90)
    ch = M.chain(np.array([d * 0.03, d * chain]), link=0.034, r=0.0055)
    ps = [(ring, look, 40), (ch, look, 60)]
    Rm = M.rot_to((0, 0, -1), d)
    for p, lk in AP.item_parts(A, 'censer', size, lit):
        p.tf(Rm).move(d * chain)
        ps.append((p, look if lk == 'silver' else lk, 50))
    emit(A, ps, name, hand)


@weapon
def javelin(A, hand='r', length=1.34, back=0.50, name='javelin'):
    """throwing spear (kit javelin)"""
    ps = [(p, {'wood': 'wood', 'iron': 'steel', 'strap': 'strap'}[kind], 22 if kind == 'iron' else 50) for p, kind in KW.javelin(length=length, back=back)]
    emit(A, ps, name, hand)


def disc_part(r=0.16, w=0.035, th=0.007):
    a = np.linspace(0, 2 * math.pi, 28, endpoint=False)
    pts = np.stack([(r - w / 2) * np.cos(a), (r - w / 2) * np.sin(a), 0 * a], axis=1)
    return tube(pts, th / 2, n=4, r2=w / 2, closed=True, up=(0, 0, 1), power=1.0)


@weapon
def disc(A, hand='r', r=0.16, name='disc', look='brass_bright', edge=None):
    """razor-edged throwing ring spinning on the index finger (flat in the blade plane)"""
    ps = [(disc_part(r).move(0, 0.045 + r - 0.03, 0), look, 25)]
    if edge:
        ps.append((disc_part(r + 0.003, 0.006, 0.0085).move(0, 0.045 + r - 0.03, 0), edge, 25))
    emit(A, ps, name, hand)


@weapon
def crossbow(A, hand='r', span=0.62, length=0.5, name='crossbow', look='iron', bolt=True, fletch='linen'):
    """heavy hand-crossbow held like a pistol: grip in the fist, tiller forwards along +X, steel prod across (Z)"""
    y = 0.075
    grip = tube([(0, -0.07, 0), (0.0, 0.0, 0), (0.01, y, 0)], [0.016, 0.017, 0.02], n=7)
    tiller = box(length, 0.05, 0.042, bevel=0.008).move(length / 2 - 0.13, y + 0.012, 0)
    x1 = length - 0.15
    zs = np.linspace(-span / 2, span / 2, 13)
    pts = np.stack([x1 - 0.10 * (np.abs(zs) / (span / 2)) ** 2, np.full(13, y + 0.02), zs], axis=1)
    prod = tube(pts, 0.007, n=6, r2=0.014 * (1 - 0.55 * (np.abs(zs) / (span / 2)) ** 2), up=(1, 0, 0))
    latch = (x1 - 0.30, y + 0.04, 0.0)
    string = join([cyl(pts[0], latch, 0.0028, n=4), cyl(pts[-1], latch, 0.0028, n=4)])
    stirrup = torus(0.035, 0.006, n=10, m=4).rot('y', 90).rot('z', 0).move(x1 + 0.035, y + 0.012, 0)
    lock = box(0.07, 0.03, 0.05, bevel=0.006).move(x1 - 0.30, y + 0.035, 0)
    trig = cyl((x1 - 0.30, y - 0.01, 0), (x1 - 0.25, y - 0.075, 0), 0.006, n=5)
    ps = [(grip, 'strap', 40), (tiller, 'wood', 30), (prod, look, 30), (string, 'rope', 40), (stirrup, look, 40), (lock, look, 30), (trig, look, 40)]
    if bolt:
        ps.append((cyl((x1 - 0.29, y + 0.045, 0), (x1 + 0.06, y + 0.045, 0), 0.006, n=5), 'wood', 40))
        ps.append((M.cone_spike(0.011, 0.045, n=4).rot('y', 90).move(x1 + 0.06, y + 0.045, 0), look, 30))
        for a in (0, 120, 240):
            f = box(0.07, 0.022, 0.003).move(0, 0.014, 0).rot('x', a).move(x1 - 0.25, y + 0.045, 0)
            ps.append((f, fletch, 40))
    emit(A, ps, name, hand)


@weapon
def tongs(A, hand='r', length=0.42, name='tongs', look='iron', held=None, held_look='glow'):
    """smith's tongs; held='mask' grips a blank face mask in the jaws"""
    ps = []
    for sz in (-1, 1):
        pts = M.polyline([(0, -0.09, sz * 0.012), (0, 0.06, sz * 0.006), (0, length * 0.72, -sz * 0.006), (0, length * 0.86, -sz * 0.03), (0, length, -sz * 0.008)], 9)
        ps.append((tube(pts, 0.007, n=5), look, 40))
    ps.append((cyl((0, length * 0.7, -0.012), (0, length * 0.7, 0.012), 0.009, n=6), look, 40))
    if held == 'mask':
        m = ellipsoid(0.058, 0.09, 0.016, n=14, m=7)
        m.v[:, 2] += 1.0 * m.v[:, 0] ** 2 / 0.06
        ps.append((m.move(0, length + 0.075, 0.0), held_look, 70))
    emit(A, ps, name, hand)


# ------------------------------------------------------------------------------------------------ shields
@weapon
def shield(A, hand='l', shape='round', wd=0.6, ht=0.6, th=0.03, look='wood', rim='rust', name='shield', boss=True, dome=0.0,
           offset=(-0.10, 0.0), face=None, studs=0, cross=False):
    """shield (wd x ht) carried on the fist / forearm: the board lies in the hand's XY plane on the back-of-hand side.
    shape round | coffin (tapered hexagon) | kite;  dome = bulge (m);  face = look of a polished front disc (mirror);
    cross = iron cross-straps;  studs = nails along the rim;  offset = (x, y) of the board centre from the fist"""
    dz = dorsal(hand)
    zc = dz * 0.05
    ps = []
    if shape == 'round':
        n = 22
        prof = [(0.0, dome + th / 2), (wd * 0.2, dome * 0.93 + th / 2), (wd * 0.36, dome * 0.62 + th / 2), (wd * 0.47, dome * 0.2 + th / 2),
                (wd * 0.5, th / 2), (wd * 0.5, -th / 2), (wd * 0.3, -th / 2 + dome * 0.6), (0.0, -th / 2 + dome * 0.9)]
        b = revolve(prof, n=n)
        if dz < 0:
            b.rot('x', 180)
        out = np.array([(wd / 2 * math.cos(a), wd / 2 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, n, endpoint=False)])
    else:
        if shape == 'coffin':
            out = np.array([(-wd * 0.30, -ht * 0.5), (wd * 0.30, -ht * 0.5), (wd * 0.5, ht * 0.22), (wd * 0.33, ht * 0.5), (-wd * 0.33, ht * 0.5), (-wd * 0.5, ht * 0.22)])
        else:
            out = np.array([(0.0, -ht * 0.5), (wd * 0.42, -ht * 0.1), (wd * 0.5, ht * 0.35), (wd * 0.3, ht * 0.5), (-wd * 0.3, ht * 0.5), (-wd * 0.5, ht * 0.35), (-wd * 0.42, -ht * 0.1)])
        b = M.prism(out, -th / 2, th / 2).bevel(th * 0.22)
    c = V(offset[0], offset[1], zc)
    ps.append((b.move(c), look, 30))
    front = dz * (th / 2 + dome)
    if rim and shape == 'round':
        ps.append((torus(wd / 2, th * 0.62, n=22, m=5).move(c), rim, 40))
    if cross:                                           # iron straps across the board, proud of the front
        for (p0, p1) in (((-wd * 0.4, ht * 0.22), (wd * 0.4, ht * 0.22)), ((0.0, -ht * 0.47), (0.0, ht * 0.47)), ((-wd * 0.27, -ht * 0.3), (wd * 0.27, -ht * 0.3))):
            L_ = math.hypot(p1[0] - p0[0], p1[1] - p0[1]); ang = math.degrees(math.atan2(p1[1] - p0[1], p1[0] - p0[0]))
            s = box(L_, 0.045, 0.008, bevel=0.002).rot('z', ang).move((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2, dz * (th / 2 + 0.003))
            ps.append((s.move(c), rim, 30))
    if studs:
        k = len(out)
        for i in range(studs):
            t = i / studs * k
            a = out[int(t) % k]; bq = out[(int(t) + 1) % k]
            q = a + (bq - a) * (t - int(t))
            q = q * 0.93
            nail = M.cone_spike(0.007, 0.03, n=4)
            nail.rot('x', 25 * math.sin(i * 2.4)).rot('y', 30 * math.cos(i * 1.7))
            if dz < 0:
                nail.rot('x', 180)
            ps.append((nail.move(q[0], q[1], dz * th * 0.3).move(c), rim, 30))
    if boss:
        bs = revolve([(0.0, 0.05), (0.035, 0.042), (0.06, 0.018), (0.075, 0.0)], n=10)
        if dz < 0:
            bs.rot('x', 180)
        ps.append((bs.move(c + V(0, 0, front)), rim, 40))
    if face:                                            # polished convex disc on the front
        fd = revolve([(0.0, dome + 0.004), (wd * 0.2, dome * 0.93 + 0.004), (wd * 0.36, dome * 0.62 + 0.004), (wd * 0.455, dome * 0.22 + 0.003), (wd * 0.455, 0.0)], n=22)
        if dz < 0:
            fd.rot('x', 180)
        ps.append((fd.move(c + V(0, 0, dz * th / 2)), face, 60))
    # arm straps on the back
    for x in (-0.02, offset[0] * 1.9):
        ps.append((tube([(x, -0.045, zc - dz * th / 2), (x, -0.03, zc - dz * (th / 2 + 0.03)), (x, 0.03, zc - dz * (th / 2 + 0.03)), (x, 0.045, zc - dz * th / 2)],
                        0.004, n=4, r2=0.014, up=(1, 0, 0)), 'strap', 40))
    emit(A, ps, name, hand)


@weapon
def coal(A, hand='r', r=0.05, name='coal', look='ember'):
    """a glowing lump clenched in the fist (thrown by cinderlings)"""
    c = M.rock(r, r * 0.85, r * 0.9, seed=5, n=12)
    emit(A, [(c.move(0.0, 0.01, 0.0), look, 40)], name, hand)


@weapon
def shear(A, hand='r', length=0.56, look='blued', name='shear'):
    """one half of a pair of oversized wick-shears: a long narrow single-edged blade, finger loop below the fist"""
    bl = blade([(0.05, 0.006, 0.036, 0.008), (0.17, 0.005, 0.032, 0.007), (length * 0.7, 0.004, 0.019, 0.005), (length, 0.0, 0.002, 0.001)])
    grip = tube([(0, -0.058, 0), (0, 0.05, 0)], 0.0125, n=7)
    loop = torus(0.034, 0.007, n=12, m=4).move(0.012, -0.092, 0)
    boss = cyl((0.012, 0.062, -0.013), (0.012, 0.062, 0.013), 0.014, n=8)
    emit(A, [(bl, look, 22), (grip, 'strap', 40), (loop, look, 40), (boss, look, 30)], name, hand)


@weapon
def cage_pole(A, hand='r', length=0.55, name='cage', look='iron', glow='ember', r=0.11, h=0.26):
    """iron cage the size of a birdcage held out on a short pole, a live stolen ember inside"""
    ps = shaft(0.12, length, 0.016)
    c = V(0, length + 0.02, 0)
    arm = tube(M.polyline([(0, length - 0.02, 0), (0, length + 0.06, 0), (0.07, length + 0.12, 0), (0.15, length + 0.10, 0)], 8), 0.01, n=5)
    ps.append((arm, look, 40))
    top = V(0.15, length + 0.10, 0)
    d = unit(V(1.0, -0.25, 0.0))                       # the cage hangs along the knuckle direction (down when the arm is held out)
    Rm = M.rot_to((0, 0, -1), d)
    bars = [torus(r, 0.007, n=12, m=4).move(0, 0, -zz) for zz in (0.05, 0.05 + h * 0.5, 0.05 + h)]
    for k in range(8):
        a = 2 * math.pi * k / 8
        bars.append(cyl((r * math.cos(a), r * math.sin(a), -0.05), (r * math.cos(a), r * math.sin(a), -0.05 - h), 0.005, n=4))
        bars.append(cyl((r * math.cos(a), r * math.sin(a), -0.05), (0, 0, -0.005), 0.005, n=4))
    bars.append(revolve([(0.0, -0.05 - h - 0.012), (r, -0.05 - h - 0.008), (r, -0.05 - h + 0.006)], n=12))
    bars.append(torus(0.018, 0.005, n=8, m=4).rot('x', 90).move(0, 0, 0.012))
    cg = join(bars).tf(Rm).move(top)
    ps.append((cg, look, 40))
    em = M.rock(r * 0.5, r * 0.45, r * 0.55, seed=3, n=12).move(0, 0, -0.05 - h * 0.62).tf(Rm).move(top)
    ps.append((em, glow, 40))
    emit(A, ps, name, hand)


@weapon
def key(A, hand='r', length=0.95, bow=0.14, name='key', look='brass', tilt=0.0):
    """a great winding key carried over the shoulder like a rifle: bow (two wing loops) at the far end, bit near the fist"""
    import asm_parts4 as P4
    k = P4.key_parts(length, bow, 0.019).rot('x', -90)            # stem +Z -> +Y
    k.move(0, -0.16, 0)
    bit = join([box(0.07, 0.022, 0.016).move(0.045, -0.10 + 0.045 * i, 0) for i in range(2)])
    emit(A, [(k, look, 50), (bit, look, 30)], name, hand, tilt)


@weapon
def pot(A, hand='r', r=0.085, name='pot', look='clay', cord='rope'):
    """round clay oil pot held ready in the throwing hand"""
    p = revolve([(0.0, -r * 0.82), (r * 0.5, -r * 0.8), (r * 0.95, -r * 0.35), (r, 0.0), (r * 0.82, r * 0.5), (r * 0.42, r * 0.8), (r * 0.36, r * 0.92),
                 (r * 0.46, r * 1.0), (0.0, r * 0.98)], n=12)
    p.rot('x', -90).move(0.02, r * 0.75, 0.0)
    rag = ellipsoid(r * 0.3, r * 0.3, r * 0.3, n=6, m=4).move(0.02, r * 1.75, 0)
    emit(A, [(p, look, 70), (rag, cord, 60)], name, hand)


@weapon
def leashes(A, hand='l', count=3, length=0.95, name='leashes', look='strap'):
    """slack leashes trailing from the fist (they hang along the knuckle direction: down when the arm hangs)"""
    ps = [(tube([(0, -0.05, 0), (0, 0.05, 0)], 0.013, n=6), look, 40)]
    for k in range(count):
        dz = (k - (count - 1) / 2) * 0.05
        L_ = length * (1 - 0.12 * k)
        pts = M.polyline([(0.0, -0.03 + 0.03 * k, 0), (L_ * 0.35, -0.10, dz * 2), (L_ * 0.7, -0.03, dz * 4), (L_, -0.14, dz * 6)], 10)
        ps.append((tube(pts, 0.006, n=4), look, 60))
        ps.append((torus(0.035, 0.007, n=8, m=4).rot('y', 90).move(L_ + 0.03, -0.14, dz * 6), 'iron', 40))
    emit(A, ps, name, hand)


@weapon
def ladle(A, hand='r', length=1.0, bowl=0.06, name='ladle', look='iron', glow=None, back=0.25):
    """long-handled ladle / dipper; glow = look of what is in the bowl"""
    ps = shaft(back, length, 0.012, look=look)
    c = V(0.0, length + bowl * 0.8, 0.0)
    b = revolve([(0.0, -bowl * 0.8), (bowl * 0.6, -bowl * 0.7), (bowl, 0.0), (bowl * 0.92, 0.0), (bowl * 0.55, -bowl * 0.6), (0.0, -bowl * 0.68)], n=10)
    ps.append((b.move(c), look, 50))
    if glow:
        ps.append((revolve([(0.0, -bowl * 0.2), (bowl * 0.8, -bowl * 0.2), (bowl * 0.8, -bowl * 0.26)], n=10).move(c), glow, 60))
    emit(A, ps, name, hand)


@weapon
def maul(A, hand='r', length=1.05, name='maul', look='rust', head='ring', back=0.25, r=0.024):
    """maul on a timber: head 'ring' = a beacon door-ring (iron hoop) lashed on, 'block' = a square iron block"""
    ps = shaft(back, length, r, wraps=1)
    y = length
    if head == 'ring':
        ps.append((torus(0.17, 0.04, n=16, m=7).move(0, y + 0.10, 0), look, 40))
        ps.append((box(0.07, 0.16, 0.07, bevel=0.01).move(0, y - 0.04, 0), look, 30))
        for k in range(3):
            ps.append((torus(r + 0.012, 0.008, n=8, m=4).rot('x', 90).move(0, y - 0.14 - 0.05 * k, 0), 'rope', 50))
    else:
        ps.append((box(0.30, 0.19, 0.19, bevel=0.02).move(0, y, 0), look, 30))
    emit(A, ps, name, hand)


@weapon
def clapper(A, hand='r', length=1.55, ball=0.15, name='clapper', look='iron', back=0.3):
    """the iron clapper of a great bell: a tapering shaft ending in a ball like a wrecking weight"""
    pts = [(0, -back, 0), (0, 0, 0), (0, length * 0.6, 0), (0, length - ball * 0.6, 0)]
    sh = tube(pts, [0.02, 0.022, 0.03, 0.05], n=8)
    b = ellipsoid(ball, ball * 1.05, ball, n=12, m=9).move(0, length, 0)
    eye = torus(0.035, 0.012, n=10, m=4).rot('x', 90).rot('z', 90).move(0, -back - 0.03, 0)
    emit(A, [(sh, look, 50), (b, look, 60), (eye, look, 40)], name, hand)


@weapon
def shovel(A, hand='r', length=1.25, name='shovel', look='rust', load=None, back=0.45, size=1.0):
    """coal shovel with a blade the size of a door; load = look of the glowing heap on it"""
    ps = shaft(back, length, 0.022, wraps=1)
    bl = KX.spade(Ln=0.62 * size, w=0.46 * size, dish=0.06 * size)
    ps.append((bl.move(0, length - 0.03, 0), look, 30))
    if load:                                             # a heap: glowing slag between darker, cooled clinker lumps
        h = KX.lump(0.15 * size, 0.2 * size, 0.06 * size, seed=4, amp=0.3, n=12, m=7)
        ps.append((h.move(0, length + 0.30 * size, 0.04 * size), load, 60))
        for k, (dx, dy, rr) in enumerate(((0.07, 0.17, 0.075), (-0.08, 0.26, 0.085), (0.05, 0.38, 0.07), (-0.03, 0.45, 0.06), (0.10, 0.30, 0.05), (-0.11, 0.40, 0.05))):
            c = KX.lump(rr * size, rr * 0.9 * size, rr * 0.62 * size, seed=10 + k, amp=0.38, freq=11.0)
            ps.append((c.move(dx * size, length + dy * size, (0.075 + 0.02 * (k % 2)) * size), 'clinker' if k % 3 else load, 50))
    emit(A, ps, name, hand)


@weapon
def log(A, hand='r', length=0.8, r=0.065, name='log'):
    """a tree trunk held ready to throw"""
    import asm_parts6 as P6
    emit(A, [(P6.log_part(length, r, seed=3).move(0, length * 0.25, 0), 'log', 40)], name, hand)


@weapon
def headstone(A, hand='r', length=1.0, name='headstone', look='stone', back=0.3, moss=None):
    """a headstone lashed to a timber post"""
    ps = shaft(back, length, 0.028, wraps=1)
    st, inc = KX.headstone(w=0.42, h=0.62, th=0.09, seed=3)
    for p, lk in ((st, look), (inc, 'void')):
        ps.append((p.rot('x', -90).move(0, length - 0.22, 0), lk, 30))        # standing on the post: height along +Y
    for k in range(4):
        ps.append((torus(0.07, 0.011, n=10, m=4, ry=0.085).rot('x', 90).move(0, length - 0.12 + 0.09 * k, 0), 'rope', 50))
    emit(A, ps, name, hand)


@weapon
def plough(A, hand='l', h=1.55, w=0.55, name='plough', look='oak', rim='rust'):
    """wooden snow-plough blade strapped to the forearm like the prow of a ship: two boards meeting in a vertical edge"""
    dz = dorsal(hand)
    ps = []
    for sx in (-1, 1):
        b = box(w, h, 0.04, bevel=0.008).move(sx * w / 2, 0, 0)
        b.rot('y', -sx * dz * 52, pivot=(0, 0, 0))
        ps.append((b.move(-0.10, 0.12, dz * 0.42), look, 30))
        st = box(w * 0.9, 0.05, 0.012).move(sx * w / 2, 0, 0).rot('y', -sx * dz * 52, pivot=(0, 0, 0))
        for yy in (-h * 0.3, h * 0.3):
            ps.append((st.copy().move(-0.10, 0.12 + yy, dz * 0.445), rim, 30))
    ps.append((box(0.05, h, 0.06, bevel=0.01).move(-0.10, 0.12, dz * 0.44), rim, 30))
    for yy in (-0.2, 0.25):
        ps.append((cyl((-0.10, 0.12 + yy, dz * 0.40), (-0.06, 0.02 + yy * 0.2, dz * 0.03), 0.014, n=5), 'wood', 40))
    emit(A, ps, name, hand)


@weapon
def boulder_pick(A, hand='r', length=1.35, name='pick', look='ice', glow='glow', back=0.35, r=0.2):
    """a long pick whose head is a frozen boulder"""
    ps = shaft(back, length, 0.024, wraps=1)
    ps.append((KC3.ice_chunk(r, r * 0.85, r * 0.9, seed=4, n=14).move(0, length + r * 0.5, 0), look, 18))
    ps.append((M.cone_spike(0.05, 0.3, n=5).rot('y', 90).move(r * 0.6, length + r * 0.5, 0), look, 18))
    if glow:                                             # the core: seen through three windows in the boulder's faces
        for (dx, dy, dz, a_, b_, c_) in ((-0.66, 0.5, 0.0, 0.07, 0.26, 0.22), (0.1, 0.5, 0.58, 0.22, 0.22, 0.06), (0.05, 0.5, -0.60, 0.2, 0.24, 0.06)):
            ps.append((ellipsoid(r * a_, r * b_, r * c_, n=8, m=5).move(r * dx, length + r * dy, r * dz), glow, 30))
    for k in range(3):
        ps.append((torus(0.04, 0.008, n=8, m=4).rot('x', 90).move(0, length - 0.05 + 0.06 * k, 0), 'rope', 50))
    emit(A, ps, name, hand)
