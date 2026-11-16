"""asm_parts2.py -- more parts: carried loads, back gear, head dress, chains, growths (see asm_parts.py for the rules)."""
import math
import numpy as np
import monlib as M
import kit_bones as KB
import kit_cloth as KC
import kit_flesh as KF
import kit_a as KA
import kit_b as KB2
import kit_c as KC3
import kit_wisp as KW
import kit_ice as KI
import kit_boss as KX
from monlib import V, tube, ellipsoid, box, join, revolve, torus, hang, arc_pts, band, unit
from asm_parts import part, girth_of, cyl, item_parts
import asm

asm.PRE.update(
    ashfall=asm._p('flat', 'rock', color=(0.40, 0.39, 0.38), rough=1.0),
    scar=asm._p('skin', 'skin', color=(0.28, 0.15, 0.13), blotch=(0.17, 0.07, 0.06), sore_amt=0.9, vein_amt=0.0, rough=0.35, hands_dark=0.0),
    mirror=asm._p('metal', 'metal', color=(0.78, 0.66, 0.36), rust=(0.3, 0.3, 0.2), rust2=(0.2, 0.2, 0.12), amount=0.04,
                  edge_color=(0.95, 0.85, 0.55), rough=0.07, hammer=4.0, edge=0.2),
)
Z_WEIGHTS = [(0.70, {'thigh_l': 0.35, 'thigh_r': 0.35, 'pelvis': 0.3}), (0.95, {'pelvis': 1.0}), (1.10, {'spine_01': 1.0}),
             (1.25, {'spine_02': 1.0}), (1.40, {'spine_03': 1.0})]


def z_weights(p, head=None, side=None):
    """torso weights by height for things hanging down the body (side 'l'/'r' sends the low end to one thigh)"""
    st = [(a, dict(b)) for a, b in Z_WEIGHTS]
    if side:
        st[0] = (0.70, {'thigh_' + side: 0.7, 'pelvis': 0.3})
    if head is not None:
        st += [(head - 0.06, {'spine_03': 1.0}), (head, {'Head': 1.0})]
    return M.weights_by(p, p.v[:, 2], st)


def ring_disc(r=0.16, w=0.035, th=0.007, n=26):
    """flat ring with knife edges, in the XY plane"""
    a = np.linspace(0, 2 * math.pi, n, endpoint=False)
    pts = np.stack([(r - w / 2) * np.cos(a), (r - w / 2) * np.sin(a), 0 * a], axis=1)
    return tube(pts, th / 2, n=4, r2=w / 2, closed=True, up=(0, 0, 1), power=1.0)


# ================================================================================================ straps, lashings, spikes
@part
def chest_strap(A, look='strap', z=1.30, rx=0.160, ry=0.128, w=0.032, tilt=-24.0, bone='spine_03', cy=0.0):
    """plain strap round the chest (skeleton ribs by default)"""
    A.add(band((0, cy, z), rx, ry, w, 0.008, tilt_y=tilt, n=22), bone=bone, look=look)


@part
def lashing(A, bone='lowerarm_l', t0=0.0, t1=0.14, r=0.03, turns=3.0, look='rope', thick=0.007):
    """cord wound round a limb bone between two fractions of its length"""
    A.add(KA.spiral(KC3.BP(bone, t0), KC3.BP(bone, t1), r, r, turns=turns, thick=thick), bone=bone, look=look, smooth=60)


@part
def spike(A, bone='upperarm_r', path=((-0.43, 0.07, 1.44), (-0.58, 0.05, 1.43), (-0.74, 0.03, 1.36)), r=0.016, look='bone', n=6, tip=0.002):
    """a curved spike / rib / thorn along a path, rigid on one bone"""
    A.add(M.horn(M.polyline(path, 8), r, n=n, r_tip=tip), bone=bone, look=look, smooth=50)


@part
def fixed_claw(A, bone='lowerarm_l', at=(0.74, 0.065, 1.44), side=1, n=3, length=0.17, r=0.013, look='bone', spread=0.035, droop=0.35):
    """a rigid hook-hand of long bone claws on the end of a limb (for limbs whose real hand was dropped): it cannot
    come apart in the clips because it has no joints of its own"""
    at = V(at)
    A.add(ellipsoid(0.03, 0.026, 0.02, n=8, m=5).move(at), bone=bone, look=look, smooth=50)
    for k in range(n):
        dy = (k - (n - 1) / 2) * spread
        pts = M.polyline([at + V(0, dy * 0.5, 0), at + V(side * length * 0.5, dy, -0.01), at + V(side * length * 0.85, dy * 1.2, -length * droop * 0.5),
                          at + V(side * length, dy * 1.2, -length * droop)], 7)
        A.add(M.horn(pts, r, n=5, r_tip=0.002), bone=bone, look=look, smooth=50)


@part
def trails(A, look='rag', items=(('Head', (0.0, 0.1, 1.72), (0.2, 1.0, 0.0), 0.3, 0.05),), droop=0.9, seed=40):
    """loose streaming strip ends: items = [(bone, base point, direction, length, width)]"""
    for k, (bone, base, d, ln, wd) in enumerate(items):
        A.add(KW.trail(V(base), d, ln, width=wd, rows=6, droop=droop, wave=0.02, seed=seed + k), bone=bone, look=look)


@part
def glow_blob(A, bone='spine_03', at=(0.0, -0.125, 1.33), r=(0.035, 0.012, 0.05), look='glow'):
    """a glowing lump (soul-light, ember, sore)"""
    A.add(ellipsoid(r[0], r[1], r[2], n=10, m=6).move(V(at)), bone=bone, look=look, smooth=90)


@part
def finger_caps(A, look='brass', length=0.035, side='both'):
    """thimbles / talon caps on every fingertip"""
    KF.claws(A.C, A.lk(look), body=A.rest, length=length, curl=0.1, sides=[{1: 'l', -1: 'r'}[s] for s in A.sides(side)])


@part
def sores(A, look='scar', count=26, regions=('torso', 'upperarms', 'thighs'), r=(0.012, 0.022), seed=5, squash=0.3):
    """round scars / boils riding on the skin"""
    b = A.rest
    m = b.wsum(asm.region_bones(regions)) > 0.5
    KA.blobs(A.C, b, KA.scatter(b, m, count, seed=seed, min_dist=0.055), A.lk(look), r=r, seed=seed, sink=0.55, squash=squash)


@part
def cracks(A, look='ember', regions=('torso',), count=20, seed=3, width=0.018, z0=-1.0, z1=3.0):
    """glowing fissures / split skin: ribbons along the body's own edges (they keep the skin weights)"""
    b = A.rest
    m = (b.wsum(asm.region_bones(regions)) > 0.5) & (b.v[:, 2] > z0) & (b.v[:, 2] < z1)
    KA.add_cracks(A.C, b, m, A.lk(look), count=count, seed=seed, width=width)


# ================================================================================================ cloth pieces
@part
def stole(A, look='linen', length=0.9, w=0.10, x=0.10, y=-0.105, z=1.455, collar=True, seed=4):
    """long funeral stole: a collar band and two panels hanging down the front"""
    for sx in (1, -1):
        xs = np.linspace(x - w / 2, x + w / 2, 4) * sx
        top = np.stack([xs, np.full(4, y), np.full(4, z)], axis=1)
        p = hang(top, length * (1.0 if sx > 0 else 0.93), rows=9, out=np.tile((0.0, -1.0, 0.0), (4, 1)), flare=0.01, rag=0.15, seed=seed + sx,
                 wave=0.012, holes=0.1, tatter=0.1)
        z_weights(p, side='l' if sx > 0 else 'r')
        A.add(p, look=look)
    if collar:
        A.add(band((0, 0.005, z + 0.02), 0.125, 0.115, 0.075, 0.008, tilt_x=-14, n=20), bone='spine_03', look=look)


@part
def apron(A, look='apron', z=1.36, length=0.85, a0=-150.0, a1=-30.0, pad=0.012, strap='strap', seed=6):
    """bib apron: front panel from the chest to the knees, neck strap and waist tie"""
    cx, cy, rx, ry = girth_of(A, 'body', z)
    top, nr = arc_pts((0, cy, z), rx + pad, ry + pad + 0.01, a0, a1, 9)
    p = hang(top, length, rows=10, out=nr, flare=0.04, rag=0.06, seed=seed, wave=0.006)
    z_weights(p)
    A.add(p, look=look)
    A.add(band((0, cy, z + 0.07), 0.105, 0.12, 0.02, 0.006, tilt_x=-38, n=16), bone='spine_03', look=strap)
    cx, cy, rx, ry = girth_of(A, 'body', 1.06)
    A.add(band((0, cy, 1.06), rx + pad + 0.006, ry + pad + 0.012, 0.022, 0.006, n=20), bone='spine_01', look=strap)


@part
def waist_roll(A, look='robe', z=1.045, r=0.036, on='body', pad=0.02):
    """thick roll of cloth round the hips (a robe stripped to the waist)"""
    cx, cy, rx, ry = girth_of(A, on, z)
    a = np.linspace(0, 2 * math.pi, 22, endpoint=False)
    pts = np.stack([(rx + pad) * np.cos(a), cy + (ry + pad) * np.sin(a), z + 0.012 * np.sin(3 * a)], axis=1)
    t = tube(pts, r * 0.8, n=8, r2=r * 1.3, closed=True, up=(0, 0, 1))
    t.rough(0.013, 30.0, seed=4.0)
    t.rough(0.006, 70.0, seed=7.0)
    A.add(t, bone='pelvis', look=look, smooth=80)


@part
def hood_down(A, look='hood', seed=2):
    """cowl thrown back: a crumpled bundle behind the neck and a collar roll"""
    b = ellipsoid(0.150, 0.075, 0.105, n=12, m=8)
    b.rough(0.012, 16.0, seed=float(seed))
    b.v[:, 2] -= 0.35 * np.clip(b.v[:, 1], 0, 1)
    A.add(b.move(0, 0.118, 1.50), bone='spine_03', look=look, smooth=80)
    A.add(torus(0.108, 0.026, n=14, m=6, ry=0.115).rot('x', -22).move(0, 0.01, 1.515), bone='spine_03', look=look, smooth=80)


@part
def hood_tail(A, look='hood', y=0.215, z0=1.62, z1=0.60, r=0.05, seal=True):
    """a hood's long tail hanging down the back to the knees, weighted with a lead seal"""
    path = M.polyline([(0, 0.105, z0), (0, y - 0.03, z0 - 0.12), (0, y, z0 - 0.3), (0, y - 0.02, (z0 + z1) / 2), (0, y - 0.05, z1)], 12)
    rr = np.linspace(r, r * 0.42, 12)
    t = tube(path, rr * 0.45, n=7, r2=rr, up=(0, 1, 0))
    z_weights(t, head=z0 - 0.02)
    A.add(t, look=look, smooth=80)
    if seal:
        for p, lk in item_parts(A, 'seal', 1.25):
            A.add(p.move(path[-1] + V(0, 0, 0.01)), bone='pelvis', look=lk, smooth=40)


@part
def veil(A, look='cowl', front=0.75, back=0.5, rx=0.088, ry=0.102, z=1.742, seed=9, holes=0.3, hair=16, hair_look=None):
    """long veil of hair and shroud hanging straight down from the crown, longest over the face: a torn sheet plus
    separate lank strands lying over it"""
    n = 22
    a = np.radians(np.linspace(-90, 270, n))
    top = np.stack([rx * np.cos(a), -0.012 + ry * np.sin(a), np.full(n, z)], axis=1)
    nr = np.stack([np.cos(a), np.sin(a), 0 * a], axis=1)
    ln = back + (front - back) * (0.5 - 0.5 * np.sin(a))
    p = hang(top, ln * 0.8, rows=9, out=nr, flare=0.05, rag=0.5, seed=seed, wave=0.02, holes=holes, tatter=0.55)
    z_weights(p, head=1.55)
    A.add(p, look=look)
    A.add(ellipsoid(rx + 0.004, ry + 0.004, 0.05, n=12, m=6).move(0, -0.012, z - 0.004), bone='Head', look=look, smooth=80)
    if hair:
        rng = np.random.default_rng(seed)
        ah = np.radians(rng.uniform(-90, 270, hair))
        anc = np.stack([(rx + 0.008) * np.cos(ah), -0.012 + (ry + 0.008) * np.sin(ah), z + rng.uniform(-0.01, 0.02, hair)], axis=1)
        outs = np.stack([np.cos(ah), np.sin(ah), 0 * ah], axis=1)
        lens = (back + (front - back) * (0.5 - 0.5 * np.sin(ah))) * rng.uniform(0.75, 1.2, hair)
        for st in KB2.strands(anc, outs, lens, width=0.03, rows=8, seed=seed + 1, wave=0.02, taper=0.8, flare=0.03, rag=0.2):
            z_weights(st, head=1.55)
            A.add(st, look=hair_look or look)


@part
def shroud_gown(A, look='shroud', girth=1.0, rag=0.10, seed=5, pleat=0.11, hem=0.0):
    """winding-sheet of a legless floater: a narrow column from the shoulders to the floor (pelvis-weighted below the hips)"""
    g = girth
    secs = [(hem, 0, 0.10, 0.165 * g, 0.155 * g), (0.25, 0, 0.07, 0.135 * g, 0.125 * g), (0.60, 0, 0.045, 0.122 * g, 0.108 * g),
            (0.94, 0, 0.03, 0.126 * g, 0.110 * g), (1.16, 0, 0.024, 0.128 * g, 0.110 * g), (1.28, 0, 0.02, 0.150 * g, 0.125 * g),
            (1.40, 0, 0.02, 0.172 * g, 0.135 * g), (1.46, 0, 0.02, 0.168 * g, 0.125 * g), (1.53, 0, 0.012, 0.095, 0.095)]
    gw, R = KB2.gown(secs, n=32, hem_z=hem, pleat_top=1.12, pleat_amp=pleat, rag=rag, seed=seed)
    KW.shroud_weights(gw, smooth=5)
    A.add(gw, look=look)
    A.refs['robe'] = gw


# ================================================================================================ head dress
@part
def wax_cap(A, look='wax', drips=8, seed=3, z=1.712, th=0.007):
    """wax poured over a shaved scalp: a skin-tight coat made from the scalp's own faces, with runs down the temples
    and the nape"""
    b = A.rest
    nz = np.array([M.fbm(p, 30.0, float(seed)) for p in b.v])
    m = (b.wsum(['Head']) > 0.5) & (b.v[:, 2] > z + 0.014 * nz)
    cap = KF.shell(b, m, th)
    A.add(cap, look=look, smooth=180)
    S = A.S('body')
    rng = np.random.default_rng(seed)
    for k in range(drips):
        a = math.radians(-70 + 320 * (k + rng.uniform(0.2, 0.8)) / drips)
        L_ = rng.uniform(0.035, 0.085)
        pts = []
        for t in np.linspace(0, 1, 4):
            q, nq, _ = S.nearest(V(0.092 * math.cos(a), -0.02 + 0.105 * math.sin(a), z + 0.012 - L_ * t))
            pts.append(q + unit(q - V(0, -0.02, q[2])) * 0.004)
        A.add(tube(np.array(pts), [0.010, 0.0075, 0.0065, 0.009], n=5), bone='Head', look=look, smooth=70)


@part
def rope_crown(A, look='rope', spikes=9, spike_look='wood', z=1.728, length=0.075):
    """rope bound round the brow with thorn pegs pushed through it: a spiky halo"""
    c = V(0, -0.02, z); rx, ry = 0.097, 0.110
    a = np.linspace(0, 2 * math.pi, 20, endpoint=False)
    Rt = M.rot_axis('x', -9)
    for dz, ph in ((0.0, 0.0), (0.016, 0.16)):
        pts = np.stack([rx * np.cos(a + ph), ry * np.sin(a + ph), dz + 0.004 * np.sin(5 * a)], axis=1) @ Rt.T + c
        A.add(tube(pts, 0.0095, n=6, closed=True), bone='Head', look=look, smooth=70)
    for k in range(spikes):
        ak = 2 * math.pi * (k + 0.5) / spikes
        d = unit(V(math.cos(ak), math.sin(ak), 0.55 + 0.25 * math.sin(k * 2.3)))
        p0 = V(rx * math.cos(ak), ry * math.sin(ak), 0.008) @ Rt.T + c
        sp = M.cone_spike(0.0075, length * (0.8 + 0.4 * ((k * 7) % 3) / 2), n=4)
        A.add(sp.tf(M.rot_to((0, 0, 1), d)).move(p0 - d * 0.012), bone='Head', look=spike_look, smooth=30)
    A.add(KW.trail(V(0.02, 0.092, z - 0.01), (0.2, 0.5, -1.0), 0.16, width=0.02, rows=5, droop=0.3, seed=3), bone='Head', look=look)


@part
def gag_plate(A, look='rust', rivets=7, strap='strap'):
    """riveted iron plate strapped over the mouth and jaw"""
    secs = [(1.560, 0, -0.03, 0.079, 0.098), (1.595, 0, -0.03, 0.088, 0.108), (1.636, 0, -0.03, 0.090, 0.110)]
    A.add(KC3.shell_arc(secs, -162, -18, n=9, th=0.008), bone='Head', look=look, smooth=38)
    for z in (1.568, 1.628):
        for a in np.linspace(-150, -30, rivets):
            p, nrm = KC3.loft_point(secs, z, a, out=0.004)
            A.add(KC3.rivet(p, nrm, r=0.0065), bone='Head', look=look)
    A.add(band((0, -0.012, 1.60), 0.094, 0.108, 0.022, 0.006, tilt_x=10, n=18, a0=-20, a1=200), bone='Head', look=strap)


@part
def eye_slit(A, look='glow', y=-0.121, z=1.668, w=0.088, h=0.007, k=10.0):
    """a thin glowing line across the eyes"""
    b = box(w, 0.006, h, segs=1).subdiv(3)
    b.v[:, 1] += k * b.v[:, 0] ** 2
    A.add(b.move(0, y, z), bone='Head', look=look)


@part
def fork_mitre(A, look='linen', trim='silver', H=0.44, veil=0.27, lappets=0.34, seed=2):
    """tall mitre split into two prongs like a tuning fork, a veil stretched between them, a face veil and two lappets"""
    c = (0, -0.018)
    cap = M.loft_z([(1.685, c[0], c[1], 0.099, 0.110), (1.74, c[0], c[1], 0.102, 0.114), (1.80, c[0], c[1], 0.099, 0.110), (1.835, c[0], c[1], 0.085, 0.092)], n=18)
    A.add(cap, bone='Head', look=look, smooth=60)
    A.add(band((c[0], c[1], 1.70), 0.103, 0.115, 0.03, 0.006, n=20), bone='Head', look=trim)
    for sx in (1, -1):
        pr = M.loft_z([(1.80, sx * 0.058, c[1], 0.036, 0.062), (1.80 + H * 0.5, sx * 0.068, c[1], 0.031, 0.052),
                       (1.80 + H * 0.9, sx * 0.078, c[1], 0.024, 0.038), (1.80 + H, sx * 0.081, c[1], 0.012, 0.018)], n=10)
        A.add(pr, bone='Head', look=look, smooth=50)
        A.add(band((sx * 0.079, c[1], 1.80 + H * 0.93), 0.026, 0.04, 0.018, 0.005, n=10), bone='Head', look=trim)
        top = np.array([(sx * (0.035 + 0.008 * i), 0.085, 1.70) for i in range(3)])
        lp = hang(top, lappets, rows=6, out=np.tile((0.0, 1.0, 0.0), (3, 1)), flare=0.02, rag=0.1, seed=seed + sx, wave=0.008)
        A.add(lp, bone='Head', look=look)
    xs = np.linspace(-0.074, 0.074, 7)
    top = np.stack([xs, np.full(7, c[1]), 1.80 + H * 0.9 - 0.03 * (1 - (xs / 0.074) ** 2)], axis=1)
    A.add(hang(top, H * 0.82, rows=7, out=np.tile((0.0, -1.0, 0.0), (7, 1)), flare=0.0, rag=0.12, seed=seed, wave=0.01, holes=0.08),
          bone='Head', look=look)
    if veil:
        top, nr = arc_pts((c[0], c[1], 1.70), 0.104, 0.118, -158, -22, 9)
        A.add(hang(top, veil, rows=7, out=nr, flare=0.012, rag=0.08, seed=seed + 5, wave=0.008), bone='Head', look=look)


@part
def horn_ring(A, look='horn', R=0.17, r=0.028, z=2.03, y=-0.02, flame='ember', flame_r=0.07):
    """two horns grown up and together into a closed ring above the head, with a flame held in the middle"""
    a = np.radians(np.linspace(-90, 270, 25))
    pts = np.stack([R * np.cos(a), np.full(25, y), z + R * np.sin(a)], axis=1)
    rr = r * (0.62 + 0.38 * (0.5 - 0.5 * np.sin(a)))
    ring = tube(pts[:-1], rr[:-1], n=7, closed=True)
    ring.rough(0.004, 30.0, seed=2.0)
    A.add(ring, bone='Head', look=look, smooth=60)
    for sx in (1, -1):
        root = M.polyline([(sx * 0.06, y, 1.745), (sx * 0.11, y + 0.01, 1.80), (sx * 0.13, y, z - R * 0.8), (sx * R * 0.72, y, z - R * 0.70)], 7)
        A.add(tube(root, np.linspace(r * 1.35, r * 0.95, 7), n=7), bone='Head', look=look, smooth=60)
    if flame:
        f = ellipsoid(flame_r * 0.62, flame_r * 0.5, flame_r, n=9, m=7)
        f.v[:, 0] *= 1 - 0.55 * np.clip(f.v[:, 2] / flame_r, 0, 1); f.v[:, 1] *= 1 - 0.55 * np.clip(f.v[:, 2] / flame_r, 0, 1)
        f.v[:, 2] += 0.3 * np.clip(f.v[:, 2], 0, 1)
        A.add(f.move(0, y, z - 0.01), bone='Head', look=flame, smooth=90)
        # the flame stands on a thorn that grows up from the bottom of the ring (nothing hangs in the air)
        A.add(M.horn(np.array([(0, y, z - R + r * 0.3), (0.004, y, z - R * 0.6), (0, y, z - 0.01 - flame_r * 0.8)]), r * 0.55, n=6, r_tip=0.006),
              bone='Head', look=look, smooth=50)


# ================================================================================================ carried on the chest / back
@part
def urn(A, at=(0.0, -0.275, 1.215), r=0.185, h=0.42, bone='spine_03', look='clay', glow='glow', sling='rope', crack=True, tilt=-9.0):
    """big round-bellied funeral urn slung against the chest: glazed clay, glowing mouth, crack with a run of ash"""
    R_ = r
    prof = [(0.0, 0.0), (0.42 * R_, 0.0), (0.47 * R_, 0.03 * h), (0.86 * R_, 0.22 * h), (1.0 * R_, 0.43 * h), (0.93 * R_, 0.62 * h),
            (0.62 * R_, 0.80 * h), (0.42 * R_, 0.88 * h), (0.44 * R_, 0.95 * h), (0.53 * R_, 1.0 * h), (0.47 * R_, 1.0 * h),
            (0.36 * R_, 0.93 * h), (0.0, 0.92 * h)]
    u = revolve(prof, n=16)
    c = V(at)
    piv = (0, 0, 0.43 * h)
    A.add(u.rot('x', tilt, pivot=piv).move(c - V(piv)), bone=bone, look=look, smooth=70)
    if glow:
        A.add(revolve([(0.0, 0.006), (0.35 * R_, 0.004), (0.36 * R_, -0.004)], n=12).move(0, 0, 0.93 * h).rot('x', tilt, pivot=piv).move(c - V(piv)),
              bone=bone, look=glow, smooth=60)
    if crack:
        zz = np.linspace(0.62 * h, 0.2 * h, 6)
        rr = np.interp(zz, [p[1] for p in prof[:7]], [p[0] for p in prof[:7]])
        pts = np.stack([0.02 * np.sin(np.arange(6) * 2.1) + 0.05, -rr - 0.002, zz], axis=1)
        pts[:, 1] = -np.sqrt(np.maximum(rr ** 2 - pts[:, 0] ** 2, 0)) - 0.002
        A.add(tube(pts, 0.0045, n=4).rot('x', tilt, pivot=piv).move(c - V(piv)), bone=bone, look='void')
        p0 = (pts[-1] - V(piv)) @ M.rot_axis('x', tilt).T + V(piv) + c - V(piv)
        A.add(tube([p0, p0 + V(0.004, -0.004, -0.14), p0 + V(-0.004, 0.0, -0.36)], [0.007, 0.005, 0.002], n=5), bone=bone, look='ashfall')
    if sling:
        for sx in (1, -1):
            A.add(tube(M.polyline([c + V(sx * 0.06, 0.04, 0.44 * h), V(sx * 0.105, -0.10, 1.47), V(sx * 0.085, 0.03, 1.505), V(0.0, 0.085, 1.495)], 8), 0.007, n=5),
                  bone=bone, look=sling, smooth=60)
        A.add(torus(0.45 * R_, 0.008, n=14, m=4).move(0, 0, 0.885 * h).rot('x', tilt, pivot=piv).move(c - V(piv)), bone=bone, look=sling)


@part
def back_pole(A, z0=1.02, z1=2.25, y=0.165, x=0.0, r=0.014, lean=0.05, top=None, look='wood_z', bone='spine_03', top_look='brass', straps='strap',
              count=6, R=0.30, flag_look='rag', seed=3):
    """pole strapped upright to the back, rising over the head.  top: flag (tattered guidon) | sun (fan-shaped plate) |
    masks (blank face masks threaded on the pole) | None"""
    p0 = V(x, y, z0); p1 = V(x, y + lean, z1)
    A.add(tube([p0, (p0 + p1) / 2, p1], r, n=7), bone=bone, look=look, smooth=50)
    A.add(ellipsoid(r * 1.6, r * 1.6, r * 2.2, n=7, m=5).move(p1), bone=bone, look=top_look)
    if straps:
        for z, tl in ((1.20, 16.0), (1.36, -16.0)):
            cx, cy, rx, ry = girth_of(A, 'body', z)
            A.add(band((0, cy + 0.01, z), rx * 0.93 + 0.012, ry + 0.035, 0.026, 0.007, tilt_y=tl, n=22), bone=bone, look=straps)
    d = unit(p1 - p0)
    if top == 'flag':
        yard = cyl(p1 - d * 0.06, p1 - d * 0.06 + V(0.42, 0, 0.02), r * 0.7, n=5)
        A.add(yard, bone=bone, look=look)
        xs = np.linspace(0.02, 0.40, 8)
        tp = np.stack([x + xs, np.full(8, p1[1] + 0.004), np.full(8, p1[2] - 0.065)], axis=1)
        ln = 0.52 * (0.55 + 0.45 * np.abs(np.linspace(-1, 1, 8)))            # swallow-tailed
        fl = hang(tp, ln, rows=8, out=np.tile((0.0, 1.0, 0.0), (8, 1)), flare=0.0, rag=0.35, seed=seed, wave=0.02, holes=0.2, tatter=0.3)
        A.add(fl, bone=bone, look=flag_look)
    elif top == 'sun':
        cz = z1 - R * 0.12
        a = np.radians(np.linspace(-12, 192, 19))
        out = np.stack([R * np.cos(a), R * np.sin(a)], axis=1)
        out = np.vstack([out, [(-R * 0.16, -R * 0.03), (R * 0.16, -R * 0.03)]])
        pl = M.prism(out, -0.006, 0.006).rot('x', 90).move(x, p1[1] - 0.004, cz)
        A.add(pl, bone=bone, look=top_look, smooth=30)
        for ak in np.radians(np.linspace(0, 180, 9)):
            A.add(cyl((x + 0.05 * math.cos(ak), p1[1] - 0.013, cz + 0.05 * math.sin(ak)), (x + R * 0.97 * math.cos(ak), p1[1] - 0.013, cz + R * 0.97 * math.sin(ak)),
                      0.006, n=4), bone=bone, look=top_look)
        rim = np.stack([x + R * np.cos(a), np.full(19, p1[1] - 0.004), cz + R * np.sin(a)], axis=1)
        A.add(tube(rim, 0.011, n=5), bone=bone, look=top_look, smooth=50)
        A.add(revolve([(0.0, 0.03), (0.04, 0.022), (0.062, 0.0)], n=10).rot('x', 90).move(x, p1[1] - 0.012, cz), bone=bone, look=top_look, smooth=50)
    elif top == 'masks':
        zs = np.linspace(1.62, z1 - 0.1, count)
        for k, z in enumerate(zs):
            m = ellipsoid(0.066, 0.016, 0.098, n=12, m=7)
            m.v[:, 1] += 1.0 * m.v[:, 0] ** 2 / 0.06
            t = (z - z0) / (z1 - z0)
            m.rot('y', 9 * math.sin(k * 2.2)).rot('z', 14 * math.sin(k * 1.3 + 1)).move(x, y + lean * t - 0.012, z)
            A.add(m, bone=bone, look=top_look, smooth=70)


@part
def jar_back(A, r=0.165, h=0.80, z0=1.06, y=0.315, bone='spine_03', look='brass', rods='copper', glow='glow', straps='strap'):
    """tall sealed jar strapped upright on the back, crowned with three splayed rods like a trident antenna"""
    prof = [(0.0, 0.0), (0.72 * r, 0.0), (0.8 * r, 0.03 * h), (1.0 * r, 0.3 * h), (1.0 * r, 0.62 * h), (0.78 * r, 0.82 * h), (0.5 * r, 0.9 * h),
            (0.56 * r, 0.93 * h), (0.56 * r, 0.97 * h), (0.3 * r, 1.0 * h), (0.0, 1.005 * h)]
    j = revolve(prof, n=16)
    c = V(0, y, z0)
    A.add(j.move(c), bone=bone, look=look, smooth=60)
    for zz in (0.3 * h, 0.62 * h):
        A.add(torus(1.0 * r + 0.004, 0.011, n=16, m=4).move(c + V(0, 0, zz)), bone=bone, look=look)
    if glow:
        A.add(torus(0.80 * r, 0.012, n=16, m=4).move(c + V(0, 0, 0.815 * h)), bone=bone, look=glow)
    top = c + V(0, 0, h)
    for dx in (-0.42, 0.0, 0.42):
        d = unit(V(dx, 0.06 * (dx == 0), 1.0))
        A.add(cyl(top - d * 0.02, top + d * 0.40, 0.008, n=5), bone=bone, look=rods)
        A.add(ellipsoid(0.02, 0.02, 0.02, n=7, m=5).move(top + d * 0.41), bone=bone, look=glow or rods)
    A.add(revolve([(0.06, 0.0), (0.04, 0.03), (0.0, 0.035)], n=8).move(top), bone=bone, look=rods)
    if straps:
        for z, tl in ((1.22, 14.0), (1.40, -14.0)):
            cx, cy, rx, ry = girth_of(A, 'body', z)
            A.add(band((0, cy + 0.07, z), rx * 0.9 + 0.012, ry + 0.11, 0.03, 0.007, tilt_y=tl, n=22), bone=bone, look=straps)
    A.refs['jar_top'] = top


@part
def arm_wires(A, look='copper', r=0.0055, start=(0.10, 0.20, 1.70)):
    """wires running from a back load over the shoulders and down both arms to the hands"""
    for sx in (1, -1):
        s = V(start)
        pts = M.polyline([s, (0.17, 0.10, 1.585), (0.25, 0.066, 1.538), (0.36, 0.068, 1.527), (0.47, 0.07, 1.513), (0.60, 0.068, 1.497), (0.735, 0.066, 1.485)], 16)
        t = tube(pts, r, n=5)
        KC.skin_cloth(t, body=A.rest, smooth=1)
        A.add(t, look=look, smooth=60, flip=sx < 0)


@part
def bangles(A, look='brass', bone='lowerarm_l', count=5, r=0.135, w=0.075, t0=0.26, t1=0.80, edge=None):
    """a stack of plate-sized razor rings threaded on a limb"""
    for k, t in enumerate(np.linspace(t0, t1, count)):
        d = ring_disc(r * (1 - 0.05 * (k % 2)), w, 0.008).rot('y', 90).rot('z', 7 * math.sin(k * 1.9)).rot('y', 6 * math.cos(k * 2.3))
        A.add(d.move(KC3.BP(bone, t)), bone=bone, look=look, smooth=25)
        if edge:
            e = ring_disc(r * (1 - 0.05 * (k % 2)) + 0.003, 0.006, 0.0095).rot('y', 90).rot('z', 7 * math.sin(k * 1.9)).rot('y', 6 * math.cos(k * 2.3))
            A.add(e.move(KC3.BP(bone, t)), bone=bone, look=edge, smooth=25)


@part
def skirt_plates(A, look='brass', z=1.0, tiers=3, count=14, h=0.14, kind='scale', pad=0.012, drop=0.095, seed=4):
    """skirt of overlapping plates in tiers round the hips: scale (bevelled metal lames) | slag (rough hanging slabs)"""
    rng = np.random.default_rng(seed)
    for j in range(tiers):
        zj = z - j * drop
        cx, cy, rx, ry = girth_of(A, 'body', max(zj - 0.03, 0.7))
        rx += pad + 0.012 * j; ry += pad + 0.014 * j
        n = count + 2 * j
        for k in range(n):
            a = 2 * math.pi * (k + 0.5 * (j % 2)) / n
            out = unit(V(math.cos(a) / rx, math.sin(a) / ry, 0))
            p = V(rx * math.cos(a), cy + ry * math.sin(a), zj)
            wdt = 2 * math.pi * (rx + ry) / 2 / n * 1.25
            if kind == 'slag':
                hh = h * rng.uniform(0.7, 1.5)
                pl = M.rock(wdt * 0.55, 0.014, hh * 0.5, seed=seed + 31 * j + k, n=10)
                pl.move(0, 0, -hh * 0.5)
            else:
                pl = box(wdt, 0.007, h, bevel=0.003).move(0, 0, -h * 0.5)
                pl.v[:, 0] *= 1 - 0.35 * np.clip(-pl.v[:, 2] / h, 0, 1) ** 2            # rounded tongue
            pl.rot('x', -11 - 3 * j)
            pl.tf(M.frame_from(-out, (0, 0, 1)))
            bone = 'pelvis' if j == 0 else ('thigh_l' if p[0] > 0 else 'thigh_r')
            if j > 0 and abs(p[0]) < 0.035:
                bone = 'pelvis'
            A.add(pl.move(p + out * 0.004), bone=bone, look=look, smooth=30)


# ================================================================================================ chains and dragged things
@part
def waist_chain(A, look='rust', z=1.03, pad=0.014, link=0.05, r=0.008, tail=0.22):
    """chain locked round the waist with a hanging end at the front"""
    cx, cy, rx, ry = girth_of(A, 'body', z)
    a = np.linspace(0, 2 * math.pi, 40)
    pts = np.stack([(rx + pad) * np.cos(a), cy + (ry + pad) * np.sin(a), z + 0.008 * np.sin(4 * a)], axis=1)
    A.add(M.chain(pts, link=link, r=r), bone='pelvis', look=look, smooth=60)
    if tail:
        s = V(0.03, cy - ry - pad - 0.004, z)
        A.add(M.chain(np.array([s, s + V(0.01, -0.008, -tail)]), link=link, r=r), bone='pelvis', look=look, smooth=60)
    A.add(box(0.05, 0.02, 0.055, bevel=0.006).move(0.0, cy - ry - pad - 0.006, z), bone='pelvis', look=look)


@part
def drag_post(A, look='rust', start=(0.0, 0.13, 1.0), ground=(0.06, 0.82, 0.035), post=(0.16, 1.30, 0.0), length=0.82, r=0.05):
    """chain running from the waist back to the ground and on to a torn-out tether post that is dragged behind
    (the part on the ground is bound to the root bone so it stays on the floor)"""
    p1 = KB2.drape_path(V(start), V(ground), sag=0.20, n=9)
    A.add(M.chain(p1, link=0.055, r=0.009), bone='pelvis', look=look, smooth=60)
    d = unit(V(0.38, 1.0, 0.0))
    c = V(post) + V(0, 0, r + 0.004)
    g2 = c + V(-0.02, -0.03, 0.035)
    A.add(M.chain(np.array([V(ground), (V(ground) + g2) / 2 + V(0.03, 0, -0.012), g2]), link=0.055, r=0.009), bone='root', look=look, smooth=60,
          final=True, noground=True)
    st = tube([c - d * 0.05, c, c + d * length * 0.72, c + d * length], [r * 0.9, r, r * 0.9, r * 0.12], n=8)
    st.rough(0.005, 16.0, seed=3.0)
    A.add(st, bone='root', look=A._wood('wood_post', d, 'oak'), smooth=40, final=True, noground=True)
    A.add(torus(0.055, 0.010, n=10, m=4).rot('y', 90).rot('z', math.degrees(math.atan2(d[1], d[0])) - 90).move(c + V(0, 0, r * 0.5)),
          bone='root', look=look, final=True, noground=True)
    A.add(band(c + d * 0.02, r + 0.004, r + 0.004, 0.035, 0.006, n=10).rot('y', 90, pivot=c + d * 0.02).rot('z', math.degrees(math.atan2(d[1], d[0])), pivot=c + d * 0.02),
          bone='root', look=look, final=True, noground=True)


@part
def shackle(A, side='r', look='rust', length=0.30, r=0.034, link=0.045):
    """iron cuff with a broken chain trailing past the hand"""
    for sx in A.sides(side):
        bn = 'lowerarm_l' if sx > 0 else 'lowerarm_r'
        A.add(KI.limb_ring(bn, 0.9, r, thick=0.011), bone=bn, look=look)
        w = KC3.BP(bn, 0.9)
        A.add(M.chain(np.array([w + V(0, 0, -r), w + V(sx * length * 0.5, 0.01, -r - 0.03), w + V(sx * length, 0.0, -r - 0.045)]), link=link, r=0.007),
              bone=bn, look=look, smooth=60)


# ================================================================================================ ice / frost props
@part
def signal_horn(A, look='horn', trim='iron', glow='glow', bone='Head', mouth=(0.0, -0.165, 1.565), reach=1.0):
    """great curved signal horn frozen to the lips, sweeping forwards and up over the head like a crescent"""
    s = reach
    m = V(mouth)
    pts = M.polyline([m, m + V(0, -0.14 * s, -0.045), m + V(0, -0.36 * s, 0.03 * s), m + V(0, -0.52 * s, 0.30 * s), m + V(0, -0.50 * s, 0.62 * s),
                      m + V(0, -0.33 * s, 0.90 * s), m + V(0, -0.08 * s, 1.04 * s), m + V(0, 0.16 * s, 1.02 * s)], 22)
    rr = np.array([0.011, 0.012, 0.014, 0.016, 0.019, 0.022, 0.025, 0.029, 0.033, 0.037, 0.041, 0.045, 0.049, 0.053, 0.058, 0.063, 0.069,
                   0.076, 0.086, 0.100, 0.118, 0.130])
    A.add(tube(pts, rr, n=10, cap1=False), bone=bone, look=look, smooth=60)
    T = unit(pts[-1] - pts[-2])
    A.add(ellipsoid(rr[-1] * 0.93, rr[-1] * 0.93, 0.012, n=12, m=4).tf(M.rot_to((0, 0, 1), T)).move(pts[-1] - T * 0.035), bone=bone, look=glow or 'void')
    for i in (3, 8, 13, 18):
        t = unit(pts[i + 1] - pts[i - 1])
        A.add(torus(rr[i] + 0.004, 0.006, n=10, m=4).tf(M.rot_to((0, 0, 1), t)).move(pts[i]), bone=bone, look=trim)
    A.add(revolve([(0.0, 0.012), (0.02, 0.008), (0.024, 0.0), (0.011, -0.01)], n=8).rot('x', 100).move(m + V(0, 0.012, 0.004)), bone=bone, look=trim)
    A.refs['horn_path'] = pts


@part
def ice_block(A, c=(0.215, 0.02, 1.27), size=(0.25, 0.27, 0.78), bone='spine_03', look='ice', look2='ice2', glow='glow', seed=5):
    """rough pillar of ice still worn like a sentry-box: one big faceted slab with shards at its foot and crown"""
    blk = KC3.ice_chunk(size[0], size[1], size[2], seed=seed, n=18, jit=0.22).move(V(c))
    A.add(blk, bone=bone, look=look, smooth=18)
    S = KB2.Surf(blk)
    rng = np.random.default_rng(seed)
    for k in range(7):
        a = rng.uniform(0, 2 * math.pi)
        zz = c[2] + size[2] * rng.choice([-0.82, -0.6, 0.55, 0.8, 0.9])
        q, nq, _ = S.nearest(V(c[0] + size[0] * math.cos(a), c[1] + size[1] * math.sin(a), zz))
        A.add(KC3.ice_chunk(rng.uniform(0.05, 0.1), rng.uniform(0.05, 0.1), rng.uniform(0.09, 0.2), seed=seed + k + 1, n=9).move(q), bone=bone,
              look=look2, smooth=18)
    if glow:
        for k in range(4):                                  # light caught in fracture planes: seated on the real front face
            x = c[0] + size[0] * rng.uniform(-0.5, 0.4)
            z0 = c[2] + size[2] * rng.uniform(-0.6, 0.25)
            pts = []
            for i in range(4):
                hit, nrm = S.front(x + 0.03 * math.sin(i * 1.7 + k), z0 + 0.09 * i)
                if hit is not None:
                    pts.append(hit + V(0, -0.002, 0))
            if len(pts) >= 2:
                A.add(tube(np.array(pts), 0.006, n=4), bone=bone, look=glow)


@part
def arm_shield(A, side='r', r=0.25, look='oak', rim='rust', dome=0.035, ice='ice2', seed=2):
    """round shield frozen to the forearm (board in the plane of the arm, facing up in the T-pose = outwards in guard)"""
    for sx in A.sides(side):
        bn = 'lowerarm_l' if sx > 0 else 'lowerarm_r'
        c = KC3.BP(bn, 0.55) + V(0, 0, 0.062)
        th = 0.028
        b = revolve([(0.0, dome + th / 2), (r * 0.5, dome * 0.8 + th / 2), (r * 0.9, dome * 0.25 + th / 2), (r, th / 2), (r, -th / 2), (0.0, -th / 2)], n=20)
        A.add(b.move(c), bone=bn, look=look, smooth=30)
        A.add(torus(r, th * 0.62, n=20, m=5).move(c), bone=bn, look=rim, smooth=40)
        A.add(revolve([(0.0, 0.05), (0.035, 0.042), (0.06, 0.018), (0.075, 0.0)], n=10).move(c + V(0, 0, dome + th / 2)), bone=bn, look=rim, smooth=40)
        if ice:
            rng = np.random.default_rng(seed)
            for k in range(6):
                a = rng.uniform(0, 2 * math.pi)
                p = c + V(r * 0.95 * math.cos(a), r * 0.95 * math.sin(a), 0.0)
                for q in KI.cluster(p, axis=(math.cos(a), math.sin(a), 0.5), count=2, h=(0.04, 0.09), r=(0.012, 0.022), seed=seed + k):
                    A.add(q, bone=bn, look=ice, smooth=20)


# ================================================================================================ fiend parts
@part
def tail(A, look='skin_char', path=((0, 0.10, 0.95), (0, 0.40, 0.72), (0, 0.66, 0.86), (0, 0.74, 1.30), (0, 0.66, 1.72), (0, 0.52, 2.02)), r0=0.05,
         r1=0.014, tip='ember', tip_r=0.05, bone='pelvis'):
    """tail on the pelvis (rigid); tip = look of a glowing knob at its end (a live ember held up like a wick)"""
    pts = M.polyline(path, 16)
    A.add(tube(pts, np.linspace(r0, r1, 16), n=7), bone=bone, look=look, smooth=70)
    if tip:
        e = ellipsoid(tip_r * 0.75, tip_r * 0.75, tip_r, n=9, m=6)
        e.rough(0.004, 40.0, seed=1.0)
        A.add(e.move(pts[-1] + unit(pts[-1] - pts[-2]) * tip_r * 0.6), bone=bone, look=tip, smooth=90)


@part
def quiver(A, look='strap', count=3, length=1.10, on='mantle', seam='bone'):
    """leather quiver across the back with spare javelins fanning up over the left shoulder (harvested from the bone
    javelineer); its carrying strap rides on a published surface (`on`)"""
    axis = unit(V(0.30, -0.012, 2.02) - V(-0.13, 0.0, 0.99))
    base = V(-0.125, 0.232, 0.99)
    side = unit(np.cross(axis, (0, 1, 0)))
    wl = A._wood('wood_quiver', (0.39, 0, 0.92))
    spec = ((-0.040, -5.5, 0.0, 0.0), (0.0, 0.0, 0.075, 0.02), (0.040, 6.0, 0.02, -0.04), (0.02, 3.0, 0.05, -0.02), (-0.02, -3.0, 0.03, 0.01))[:count]
    for k, (dx, da, ds, dl) in enumerate(spec):
        d = M.rot_axis('y', da) @ axis
        jv = KW.javelin(length=length + dl, back=0.0, wrap=False, ferrule=False, head=0.2)
        KW.place(jv, d, base + side * dx + axis * ds + V(0, 0.004 * (k % 2), 0), zhint=(0, 1, 0))
        for p, kind in jv:
            A.add(p, bone='spine_03', look=wl if kind == 'wood' else {'iron': 'steel', 'strap': look}[kind])
    q0 = base - axis * 0.03; q1 = base + axis * 0.2; q2 = base + axis * 0.43
    qv = tube([q0, q0 + axis * 0.02, q1, q2 - axis * 0.02, q2], [0.022, 0.028, 0.03, 0.031, 0.034], n=10,
              r2=[0.05, 0.062, 0.072, 0.078, 0.082], up=(0, 1, 0), cap0=True, cap1=False)
    A.add(qv, bone='spine_03', look=look, smooth=60)
    a = np.linspace(0, 2 * math.pi, 12, endpoint=False)
    rim = tube(q2[None, :] + np.cos(a)[:, None] * side * 0.088 + np.sin(a)[:, None] * V(0, 0.04, 0), 0.0085, n=5, closed=True)
    A.add(rim, bone='spine_03', look=look, smooth=60)
    for s in np.linspace(0.02, 0.38, 7):
        c = base + axis * s + V(0, 0.031 + 0.004 * s, 0) + side * 0.012
        A.add(tube([c - side * 0.016 - axis * 0.012, c + V(0, 0.004, 0), c + side * 0.016 + axis * 0.012], 0.0035, n=4), bone='spine_03', look=seam, smooth=60)
    if on in A.refs:
        pts = KW.seat_path(A.S(on), [(0.0, 0.176), (0.045, 0.15), (0.095, 0.09), (0.118, 0.02), (0.112, -0.05), (0.092, -0.115), (0.078, -0.148)], n=16, lift=0.006)
        st = tube(pts, 0.0045, n=4, r2=0.021, up=(0, 0, 1), phase=math.pi / 4)
        st.smooth = 30.0
        A.add(st, look=look, like=A.refs[on])
        A.add(tube([pts[0] + V(0, 0.0, 0.01), q2 + V(0.03, -0.01, -0.03)], 0.0045, n=4, r2=0.02, up=(0, 1, 0), phase=math.pi / 4),
              bone='spine_03', look=look, smooth=30)
