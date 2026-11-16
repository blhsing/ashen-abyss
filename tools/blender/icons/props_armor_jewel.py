"""Amulets and rings, tiers 1-5 (ring_4 lives in props_items.py)."""
import bpy, math, random
from mathutils import Vector, Euler
import iconlib as I
from iconlib import R
from registry import icon
import props_items as PI
from props_armor import sm, seam, hot, dark5, iron2, pose, void, EMBER

AMULET_POSE = (0, -9, 15)


def loop_pts(w=0.062, h=0.100, n=64, wob=0.004):
    """necklace loop in the XZ plane: pinched at the origin (where the pendant hangs), round at the top"""
    P = []
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        P.append(Vector((w * math.sin(a) * abs(math.sin(a / 2)) ** 0.75, wob * math.sin(3 * a), h * (1 - math.cos(a)) / 2)))
    return P


def bail(mat, z=0.005, Rm=0.0066, r=0.0024):
    o = I.torus(Rm, r, mat, "bail", 24, 8); I.place(o, loc=(0, 0, z), rot=(90, 0, 0))
    return o


# ================================================================================================ AMULETS
@icon("amulet_1")
def amulet_1():
    """骨製護符: a carved fang of old bone bound to a knotted leather cord, bone and wood beads"""
    bn = I.bone(); lt = I.leather((0.13, 0.08, 0.045)); dk = I.leather((0.05, 0.035, 0.025))
    P = loop_pts(0.060, 0.098)
    parts = [I.tube(P, 0.0042, 8, lt, "cord", closed=True)]
    F = []; rad = []
    for i in range(15):
        t = i / 14
        F.append(Vector((0.030 * t ** 2.2, -0.001, -0.006 - 0.088 * t)))
        rad.append(0.0155 * (1 - t) ** 0.6 + 0.0012)
    parts.append(I.tube(F, rad, 14, bn, "fang"))
    root = I.ball(0.0155, bn, "root", 2, (1, 1, 0.55)); root.location = F[0]; parts.append(root)
    for k in range(3):                       # cord binding round the root
        b = I.torus(0.0150 - 0.0007 * k, 0.0025, lt, "binding", 24, 6); b.location = (0.0004 * k, -0.001, -0.009 - 0.0045 * k); parts.append(b)
    for t in (0.36, 0.46, 0.56):             # carved notches
        i = int(round(t * 14))
        g = I.torus(rad[i] + 0.0002, 0.0010, dk, "notch", 20, 6); g.location = F[i]; g.rotation_euler = (0, R(-14 - 30 * t), 0); parts.append(g)
    parts.append(bail(lt, 0.004, 0.0068, 0.0026))
    for (idx, r, m) in ((2, 0.0092, bn), (-3, 0.0092, bn), (5, 0.0070, dk), (-6, 0.0070, dk)):
        bd = I.ball(r, m, "bead", 2, (1, 1, 0.85)); bd.location = P[idx]; parts.append(bd)
    pose(parts, AMULET_POSE)
    return dict(fill=0.82)


@icon("amulet_2")
def amulet_2():
    """銀墜: a plain silver drop on a silver chain"""
    sv = I.silver()
    P = loop_pts(0.060, 0.098)
    parts = I.chain(P + [P[0]], 0.0125, 0.0027, sv)
    parts.append(I.lathe([(0.0, 0.004), (0.0048, 0.0), (0.0078, -0.010), (0.0150, -0.031), (0.0196, -0.047), (0.0180, -0.059), (0.0112, -0.068), (0.0, -0.071)], 32, sv, "drop"))
    parts.append(bail(sv, 0.0065, 0.0072, 0.0026))
    c = I.torus(0.0064, 0.0023, sv, "collar", 24, 8); c.location = (0, 0, -0.004); parts.append(c)
    b = I.torus(0.0196, 0.0015, sv, "line", 32, 6); b.location = (0, 0, -0.049); parts.append(b)
    pose(parts, AMULET_POSE)
    return dict(fill=0.82, samples=56)


@icon("amulet_3")
def amulet_3():
    """刻紋護符: a gold disc engraved with the rayed wheel of the dead Sun-Forge, a small blue stone at its hub"""
    gd = I.gold(); st = I.gem((0.10, 0.32, 1.0), 1.75, glow=0.55)
    P = loop_pts(0.060, 0.094)
    parts = I.chain(P + [P[0]], 0.0105, 0.0023, gd)
    Rd = 0.0345
    d = [I.lathe([(0.0, -0.0036), (Rd, -0.0036), (Rd + 0.0022, 0.0), (Rd, 0.0036), (Rd - 0.0055, 0.0036), (Rd - 0.0078, 0.0016), (0.0125, 0.0016),
                  (0.0105, 0.0046), (0.0, 0.0046)], 56, gd, "disc")]
    for i in range(12):                      # rays: long and short in turn
        a = R(30 * i)
        r0, r1 = (0.0145, 0.0262) if i % 2 == 0 else (0.0165, 0.0225)
        d.append(I.tube([(r0 * math.cos(a), r0 * math.sin(a), 0.0020), (r1 * math.cos(a), r1 * math.sin(a), 0.0020)], [0.0016, 0.0009], 6, gd, "ray"))
    rg = I.torus(0.0142, 0.0011, gd, "ring", 36, 6); rg.location = (0, 0, 0.0020); d.append(rg)
    stone = I.hull(PI.cut_round(0.0088, 8), st, "stone"); stone.location = (0, 0, 0.0060); d.append(stone)
    bz = I.torus(0.0090, 0.0019, gd, "bezel", 28, 8); bz.location = (0, 0, 0.0052); d.append(bz)
    g = I.group(d); I.place(g, loc=(0, -0.002, -0.0425), rot=(84, 0, 0))
    parts.append(g)
    parts.append(bail(gd, -0.001, 0.0066, 0.0024))
    pose(parts, AMULET_POSE)
    return dict(fill=0.82, samples=56)


@icon("amulet_4")
def amulet_4():
    """燼晶墜飾: a gold claw gripping a long ember crystal"""
    gd = I.gold(); st = I.gem((1.0, 0.20, 0.025), 1.8, glow=1.0)
    P = loop_pts(0.060, 0.092)
    parts = I.chain(P + [P[0]], 0.0105, 0.0023, gd)
    cry = I.crystal(0.0172, 0.078, 6, 0.44, st, "crystal", jitter=0.12, seed=4, base_tip=0.16)
    I.place(cry, loc=(0, 0, -0.006), rot=(180, 0, 12)); parts.append(cry)
    parts.append(I.lathe([(0.0, 0.002), (0.0085, 0.001), (0.0195, -0.013), (0.0202, -0.021), (0.0180, -0.022)], 24, gd, "cap"))
    for i in range(5):
        a = R(72 * i + 20)
        ca, sa = math.cos(a), math.sin(a)
        parts.append(I.tube([(0.0185 * ca, 0.0185 * sa, -0.016), (0.0200 * ca, 0.0200 * sa, -0.034), (0.0160 * ca, 0.0160 * sa, -0.048)],
                            [0.0040, 0.0033, 0.0016], 6, gd, "claw"))
    parts.append(bail(gd, 0.0065, 0.0068, 0.0026))
    pose(parts, AMULET_POSE)
    return dict(fill=0.82, samples=64)


@icon("amulet_5")
def amulet_5():
    """日爐殘片: a jagged, still-burning shard of the dead Sun-Forge caged in dark iron bands on a heavy iron chain"""
    ir = I.ashplate(); bd = iron2()
    sun = hot(5.0, 75.0, 0.24, 2.6, col=(1.0, 0.52, 0.11), deep=(1.0, 0.17, 0.016))
    P = loop_pts(0.064, 0.090)
    parts = I.chain(P + [P[0]], 0.0150, 0.0034, bd)
    sh = I.shard(0.031, sun, "shard", seed=5, n=16, squash=(0.86, 0.56, 1.80)); I.place(sh, loc=(0, 0, -0.058), rot=(0, 10, 0)); parts.append(sh)
    s2 = I.shard(0.016, sun, "splinter", seed=9, n=10, squash=(0.6, 0.5, 2.0)); I.place(s2, loc=(0.019, -0.004, -0.040), rot=(0, -24, 0)); parts.append(s2)
    s3 = I.shard(0.013, sun, "splinter", seed=12, n=10, squash=(0.6, 0.5, 2.0)); I.place(s3, loc=(-0.018, -0.002, -0.074), rot=(0, 28, 0)); parts.append(s3)
    for (z, rx, ry, tl) in ((-0.036, 0.0265, 0.0190, 10), (-0.076, 0.0235, 0.0170, -12)):          # hoops
        pts = [(rx * math.cos(R(a)), ry * math.sin(R(a)), 0.0) for a in range(0, 360, 12)]
        h = I.tube(pts, 0.0036, 8, ir, "hoop", closed=True); I.place(h, loc=(0, 0, z), rot=(0, tl, 0), scale=(1, 1, 1.5)); parts.append(h)
    for a in (-90, 30, 150):                                                                          # straps from cap to foot
        ca, sa = math.cos(R(a)), math.sin(R(a))
        pts = []
        for i in range(13):
            t = i / 12
            rr = 0.006 + 0.0235 * math.sin(math.pi * t) ** 0.6
            pts.append((rr * ca, rr * sa * 0.72, -0.006 - 0.108 * t))
        parts.append(I.tube(pts, 0.0030, 6, ir, "strap"))
        for t in (0.27, 0.66):
            i = int(round(t * 12))
            rv = I.ball(0.0040, bd, "rivet", 2); rv.location = Vector(pts[i]) * 1.06 + Vector((0, 0, pts[i][2] * -0.06)); parts.append(rv)
    parts.append(I.lathe([(0.0, 0.003), (0.0075, 0.002), (0.0110, -0.006), (0.0100, -0.010), (0.0, -0.010)], 20, ir, "cap"))
    ft = I.cone(0.0075, 0.0010, 0.014, ir, "foot", 12); I.place(ft, loc=(0, 0, -0.112), rot=(180, 0, 0)); parts.append(ft)
    parts.append(bail(bd, 0.0075, 0.0078, 0.0032))
    pose(parts, AMULET_POSE)
    bpy.context.view_layer.update()
    I.sparks(7, (-0.06, -0.03, -0.12), (0.06, -0.02, 0.0), r=0.0011, col=(1.0, 0.55, 0.16), seed=3, strength=5.0)
    return dict(fill=0.82, samples=56)


# ================================================================================================ RINGS
def wide_band(mat, Rm=0.0105, hw=0.0042, t=0.0018, bulge=0.0005, name="band"):
    """wide finger ring: lathe of a closed D profile (hoop in the XY plane like PI.band)"""
    ri = Rm - t / 2; ro = Rm + t / 2
    prof = [(ri, -hw), (ro - 0.0002, -hw), (ro + bulge * 0.6, -hw * 0.55), (ro + bulge, 0.0), (ro + bulge * 0.6, hw * 0.55), (ro - 0.0002, hw),
            (ri, hw), (ri - 0.0001, 0.0), (ri, -hw)]
    o = I.lathe(prof, 72, mat, name)
    I.bevel(o, 0.0003, 2, 50)
    return o


RING_POSE = (-62, 0, 24)


@icon("ring_1")
def ring_1():
    """銅戒: a plain, wide, hammered copper band"""
    cu = I.copper()
    parts = [wide_band(cu, 0.0105, 0.0040, 0.0021, 0.0007)]
    pose(parts, (-68, 6, 18))
    return dict(fill=0.72, samples=56)


@icon("ring_2")
def ring_2():
    """銀戒: a silver band with beaded edges and one tiny dark stone"""
    sv = I.silver(); st = I.gem((0.10, 0.015, 0.05), 1.7, glow=0.0, rough=0.04)
    parts = [wide_band(sv, 0.0105, 0.0023, 0.0017, 0.0003)]
    n = 46
    for z in (-0.0023, 0.0023):
        for i in range(n):
            a = 2 * math.pi * i / n
            b = I.ball(0.00078, sv, "bead", 1); b.location = (0.01150 * math.cos(a), 0.01150 * math.sin(a), z); parts.append(b)
    bez = [I.cone(0.0030, 0.0040, 0.0030, sv, "collet", 20)]
    rim = I.torus(0.0040, 0.0007, sv, "rim", 24, 6); rim.location = (0, 0, 0.0030); bez.append(rim)
    stone = I.ball(0.0034, st, "stone", 3, (1, 1, 0.62)); stone.location = (0, 0, 0.0030); bez.append(stone)
    b = I.group(bez); I.place(b, loc=(0, -0.0116, 0), rot=(90, 0, 0)); parts.append(b)
    for s in (-1, 1):
        sh = I.ball(0.0016, sv, "shoulder", 2); sh.location = (s * 0.0048, -0.0106, 0); parts.append(sh)
    pose(parts, RING_POSE)
    return dict(fill=0.74, samples=56)


@icon("ring_3")
def ring_3():
    """刻紋金戒: a wide gold ring, raised rims, a chased zig-zag with pellets running round it"""
    gd = I.gold()
    parts = [wide_band(gd, 0.0105, 0.0060, 0.0019, 0.0002)]
    for z in (-0.0060, 0.0060):
        t = I.torus(0.01155, 0.00085, gd, "rim", 72, 8); t.location = (0, 0, z); parts.append(t)
    nz = 15; rr = 0.01168
    pts = []
    for i in range(nz * 2):
        a = 2 * math.pi * i / (nz * 2)
        pts.append((rr * math.cos(a), rr * math.sin(a), 0.0037 if i % 2 == 0 else -0.0037))
    parts.append(I.tube(pts, 0.00058, 6, gd, "zigzag", closed=True))
    for i in range(nz * 2):
        a = 2 * math.pi * (i + 0.0) / (nz * 2)
        b = I.ball(0.00072, gd, "pellet", 1); b.location = (rr * math.cos(a), rr * math.sin(a), -0.0031 if i % 2 == 0 else 0.0031); parts.append(b)
    pose(parts, (-50, 12, 34))
    return dict(fill=0.74, samples=56)


@icon("ring_5")
def ring_5():
    """灰冠王印戒: heavy black-iron signet; the bezel is a little tarnished crown standing round a flat ember seal"""
    ir = I.ashplate(); gd = I.gold(); emb = hot(3.4, 560.0, 0.34, 1.9)
    parts = [PI.band(ir, 0.0105, 0.0024)]
    for s in (-1, 1):
        for (x, y, r) in ((0.0036, -0.0112, 0.0030), (0.0062, -0.0094, 0.0026)):
            sh = I.ball(r, ir, "shoulder", 2, (1, 1, 1.25)); sh.location = (s * x, y, 0); parts.append(sh)
    bez = [I.lathe([(0.0, 0.0), (0.0058, 0.0), (0.0072, 0.0012), (0.0073, 0.0038), (0.0066, 0.0045), (0.0, 0.0045)], 32, ir, "table")]
    bez.append(I.lathe([(0.0, 0.0044), (0.0055, 0.0044), (0.0055, 0.0051), (0.0, 0.0053)], 32, emb, "seal"))
    sig = I.torus(0.0029, 0.00055, ir, "sigil", 24, 6); sig.location = (0, 0, 0.0052); bez.append(sig)
    dot = I.ball(0.0010, ir, "sigildot", 1, (1, 1, 0.5)); dot.location = (0, 0, 0.0052); bez.append(dot)
    rim = I.torus(0.0066, 0.0010, gd, "crownband", 32, 8); rim.location = (0, 0, 0.0046); bez.append(rim)
    for i in range(8):
        a = 2 * math.pi * i / 8
        p = I.cone(0.0015, 0.0002, 0.0040 if i % 2 == 0 else 0.0029, gd, "point", 4)
        p.location = (0.0066 * math.cos(a), 0.0066 * math.sin(a), 0.0048); p.rotation_euler = (0, 0, a + R(45)); bez.append(p)
        if i % 2 == 0:
            b = I.ball(0.00062, gd, "pearl", 1); b.location = (0.0066 * math.cos(a), 0.0066 * math.sin(a), 0.0089); bez.append(b)
    b = I.group(bez); I.place(b, loc=(0, -0.0120, 0), rot=(90, 0, 0)); parts.append(b)
    pose(parts, RING_POSE)
    return dict(fill=0.78, samples=56)
