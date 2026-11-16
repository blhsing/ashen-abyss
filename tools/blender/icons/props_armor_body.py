"""Gloves, pants and boots, tiers 1-5 (helpers and the helm / chest slots are in props_armor.py)."""
import bpy, math, random
from mathutils import Vector, Euler
import iconlib as I
from iconlib import R
from registry import icon
import props_items as PI
from props_armor import (Shell, sm, seam, hot, dark5, iron2, steel3, mail, tongue, clone, baked, buckle, on, pose, inside, void_disc, void,
                         surf, tube_surf, herm, dashes, TAN, LINEN, EMBER)


def aim(o, p, d):
    """put an object at p with its local +Z along d"""
    o.location = p
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = Vector(d).normalized().to_track_quat('Z', 'Y')
    return o


# ================================================================================================ GLOVES
# right hand seen from its back (back of the hand = -Y, thumb on -X), fingers up.  x, z of the knuckle, length, splay
FING = [(-0.0365, 0.096, 0.076, -8.0), (-0.0122, 0.101, 0.086, -2.5), (0.0122, 0.097, 0.080, 3.0), (0.0358, 0.088, 0.062, 9.5)]
PALM = [(0.0, 0.036, 0.021), (0.03, 0.046, 0.022), (0.07, 0.051, 0.020), (0.096, 0.049, 0.017), (0.106, 0.044, 0.013)]


def finger_pts(base, length, splay=0.0, curl=8.0, fr=(0.44, 0.31, 0.25)):
    """joints of a finger: knuckle + the ends of its phalanges; splay leans it toward +X, curl bends it toward +Y (the palm)"""
    P = [Vector(base)]
    for k, f in enumerate(fr):
        a = R(curl * (k + 0.6)); s = R(splay)
        P.append(P[-1] + Vector((math.sin(s) * math.cos(a), math.sin(a), math.cos(s) * math.cos(a))) * (length * f))
    return P


def digit(J, r, mat, taper=0.84, name="finger"):
    n = len(J)
    rad = [r * (1 - (1 - taper) * i / (n - 1)) for i in range(n)]
    tip = I.ball(rad[-1], mat, "tip", 2); tip.location = J[-1]
    return [I.tube(J, rad, 12, mat, name), tip]


def hand(mat, r=0.0120, curl=8.0):
    """-> (parts, palm Shell, finger joints [4 x 4 points], thumb joints [3 points])"""
    palm = Shell(PALM, power=2.6)
    parts = [palm.mesh(mat, 32, thick=0, name="palm")]
    cap = I.ball(0.048, mat, "knuckles", 2, (1.0, 0.36, 0.34)); cap.location = (0, 0.0, 0.099); parts.append(cap)
    J = []
    for (x, z, ln, sp) in FING:
        j = finger_pts((x, 0.0, z - 0.008), ln + 0.008, sp, curl); J.append(j)
        parts += digit(j, r if ln > 0.07 else r * 0.92, mat)
    T = finger_pts((-0.041, 0.004, 0.030), 0.076, -44.0, 5.0, fr=(0.52, 0.48))
    parts += digit(T, r * 1.12, mat, 0.86, "thumb")
    mound = I.ball(0.021, mat, "mound", 2, (1.0, 0.78, 1.35)); mound.location = (-0.034, 0.004, 0.034); parts.append(mound)
    return parts, palm, J, T


def lame(p0, p1, r, mat, a=0.07, b=0.93, name="lame", r1=None):
    p0, p1 = Vector(p0), Vector(p1)
    return I.tube([p0.lerp(p1, a), p0.lerp(p1, b)], [r, r1 or r * 0.95], 12, mat, name)


def knuckle_pts(y=-0.0155, dz=0.001):
    return [Vector((x, y, z + dz)) for (x, z, ln, sp) in FING]


GLOVE_POSE = (0, 14, 20)


@icon("gloves_1")
def gloves_1():
    """皮手套: stitched leather glove, soft flared cuff with a turned hem, wrist strap"""
    lt = I.leather(TAN, scale=64.0, bump=0.24); dk = I.leather(); th = I.cloth((0.50, 0.44, 0.33)); ir = iron2()
    parts, palm, J, T = hand(lt)
    C = Shell([(-0.094, 0.064, 0.049), (-0.050, 0.050, 0.036), (-0.008, 0.039, 0.024), (0.014, 0.0385, 0.023)], power=2.4,
              bump=lambda phi, z: 0.0026 * math.sin(R(phi * 5) + z * 60))
    parts.append(C.mesh(lt, 40, thick=0.004, name="cuff"))
    parts.append(C.band(-0.096, -0.074, lt, 0.0055, 0.0055, name="hem"))
    parts += C.stitch([(a, -0.066) for a in range(-120, 121, 20)], th, 0.002, 0.008, 0.007, 0.0015)
    parts.append(C.band(-0.011, 0.011, dk, 0.0048, 0.004, name="strap", follow=False))
    parts.append(on(C, buckle(ir, 0.017, 0.028, 0.0023), 36, 0.0, 0.0065, follow=False))
    for a in (-44, 0, 44):          # three welted points on the back of the hand
        parts.append(palm.line([(a, 0.032), (a * 0.9, 0.090)], 0.0027, lt, 0.0012, name="point"))
        parts += palm.stitch([(a + 9, 0.036), (a * 0.9 + 8, 0.088)], th, 0.0012, 0.006, 0.005, 0.0011)
    pose(parts, GLOVE_POSE)
    return dict(fill=0.84)


@icon("gloves_2")
def gloves_2():
    """鐵護手: leather glove armoured with riveted iron: back plate, knuckle bar with gadlings, finger lames, bell cuff"""
    dk = I.leather(); ir = iron2(); bd = I.iron(amount=0.42, color=(0.20, 0.195, 0.19))
    parts, palm, J, T = hand(dk, r=0.0106)
    parts.append(palm.band(0.016, 0.090, ir, 0.0056, 0.006, -84, 84, 16, 5, "backplate"))
    parts += palm.rivets(0.027, 4, bd, 0.0031, -72, 72, 0.0056)
    kn = knuckle_pts()
    parts.append(I.tube([kn[0] + Vector((-0.013, 0.007, -0.005))] + kn + [kn[-1] + Vector((0.012, 0.007, -0.005))], 0.0088, 10, ir, "knucklebar"))
    for p in kn:
        g = I.ball(0.0108, bd, "gadling", 2, (1, 0.8, 1)); g.location = p + Vector((0, -0.0045, 0.002)); parts.append(g)
    for j in J:
        parts.append(lame(j[0], j[1], 0.0130, ir, 0.26, 0.96)); parts.append(lame(j[1], j[2], 0.0123, ir, 0.08, 0.94))
    parts.append(lame(T[0], T[1], 0.0142, ir, 0.28, 0.95))
    C = Shell([(-0.100, 0.068, 0.053), (-0.046, 0.050, 0.037), (0.002, 0.041, 0.027), (0.016, 0.041, 0.026)], power=2.4)
    parts.append(C.mesh(ir, 40, thick=0.006, name="cuff"))
    parts.append(C.ring(-0.100, 0.0050, bd, 0.001))
    parts.append(C.band(-0.014, 0.016, bd, 0.0056, 0.006, name="wristlame"))
    parts += C.rivets(-0.082, 12, bd, 0.0035, off=0.001)
    parts.append(C.line([(0, -0.092), (0, -0.018)], 0.0042, bd, 0.001, name="ridge"))
    pose(parts, GLOVE_POSE)
    return dict(fill=0.84)


@icon("gloves_3")
def gloves_3():
    """鎖環手甲: mail mitten with a thumb under a flared steel cuff trimmed with brass"""
    ml = mail(cell=0.0088, rust=0.2); st = steel3(); br = I.brass(); dk = I.leather()

    def furrow(phi, z):
        d = 0.0
        for a in (-33.0, 0.0, 33.0):
            d -= 0.0032 * math.exp(-((phi - a) / 5.0) ** 2)
        return d * sm(0.085, 0.115, z) * sm(0.192, 0.172, z)
    M = Shell([(0.0, 0.037, 0.022), (0.035, 0.049, 0.0245), (0.085, 0.054, 0.023, 0.001), (0.135, 0.052, 0.021, 0.004), (0.168, 0.043, 0.019, 0.008),
               (0.187, 0.025, 0.013, 0.012), (0.194, 0.004, 0.003, 0.014)], power=2.5, bump=furrow)
    parts = [M.mesh(ml, 72, sub=4, thick=0, name="mitten")]
    c0 = Vector((-0.034, 0.003, 0.026)); c1 = Vector((-0.084, 0.008, 0.088)); ax = (c1 - c0).normalized()
    parts.append(tube_surf(c0, c1, 0.0170, 0.0138, ml, 8, 20, "thumb", 0))
    parts.append(tube_surf(c1, c1 + ax * 0.014, 0.0138, 0.0030, ml, 5, 20, "thumbtip", 0))
    parts.append(M.ring(0.030, 0.0042, dk, 0.001, name="binding"))
    C = Shell([(-0.112, 0.076, 0.060), (-0.052, 0.053, 0.040), (0.000, 0.043, 0.029), (0.018, 0.044, 0.0295)], power=2.4)
    parts.append(C.mesh(st, 44, thick=0.006, name="cuff"))
    parts.append(C.ring(-0.112, 0.0060, br, 0.001))
    parts.append(C.band(-0.012, 0.019, br, 0.0056, 0.005, name="wrist"))
    parts.append(C.line([(0, -0.106), (0, -0.016)], 0.0048, st, 0.001, name="ridge"))
    parts += C.rivets(-0.092, 12, br, 0.0038, off=0.001)
    pose(parts, GLOVE_POSE)
    return dict(fill=0.84)


@icon("gloves_4")
def gloves_4():
    """灰鑄臂鎧: ash-forged gauntlet on a long three-plate vambrace, clawed finger lames, ember light in every joint, swept fin"""
    ash = I.ashplate(); gl = seam(3.0); bl = I.leather((0.022, 0.022, 0.026))
    parts, palm, J, T = hand(bl, r=0.0100)
    for j in J:
        parts.append(I.tube(j[:3] + [j[2].lerp(j[3], 0.5)], 0.0114, 8, gl, "fingerglow"))
        for k in range(3):
            parts.append(lame(j[k], j[k + 1], 0.0131 - 0.0005 * k, ash, 0.24 if k == 0 else 0.09, 0.91 if k < 2 else 0.70))
        d = (j[3] - j[2]).normalized()
        parts.append(aim(I.cone(0.0124, 0.0006, 0.030, ash, "claw", 10), j[2].lerp(j[3], 0.62), d + Vector((0, 0.10, 0))))
    parts.append(I.tube(T[:2] + [T[1].lerp(T[2], 0.5)], 0.0126, 8, gl, "thumbglow"))
    for k in range(2):
        parts.append(lame(T[k], T[k + 1], 0.0144 - 0.0006 * k, ash, 0.26 if k == 0 else 0.09, 0.91 if k == 0 else 0.70))
    parts.append(aim(I.cone(0.0136, 0.0006, 0.030, ash, "claw", 10), T[1].lerp(T[2], 0.62), T[2] - T[1]))
    parts.append(palm.band(0.010, 0.091, ash, 0.0056, 0.006, -86, 86, 16, 5, "backplate"))
    parts.append(palm.line([(0, 0.014), (0, 0.086)], 0.0040, ash, 0.006, name="ridge"))
    for s in (-1, 1):
        parts.append(palm.line([(s * 13, 0.016), (s * 30, 0.086)], 0.0017, gl, 0.0056, name="seam"))
    for p in knuckle_pts(-0.017, 0.0):
        parts.append(aim(I.cone(0.0105, 0.0008, 0.022, ash, "spike", 4), p, Vector((0, -0.8, 0.6))))
    parts.append(palm.ring(0.094, 0.0020, gl, 0.0040, -78, 78, name="knuckleseam"))
    # vambrace: three plates over a glowing core
    V = Shell([(-0.236, 0.072, 0.063), (-0.172, 0.067, 0.059), (-0.088, 0.053, 0.045), (-0.020, 0.042, 0.029), (0.016, 0.042, 0.027)], power=2.3)
    parts.append(V.mesh(gl, 32, thick=0, off=-0.0025, name="core"))
    for (z0, z1) in ((-0.236, -0.160), (-0.153, -0.082), (-0.075, 0.016)):
        parts.append(V.band(z0, z1, ash, 0.0045, 0.0062, rows=4, name="plate"))
        parts += V.rivets(z0 + 0.012, 8, ash, 0.0034, off=0.005)
    parts.append(V.band(-0.248, -0.224, ash, 0.0085, 0.0062, name="elbowrim"))
    fin = I.extrude([(0.0, 0.030), (0.030, 0.018), (0.070, -0.030), (0.092, -0.105), (0.098, -0.168), (0.062, -0.110), (0.034, -0.086), (0.0, -0.080)], 0.009, ash, "fin", bev=0.003)
    fin.location = (0.052, 0.004, -0.078); parts.append(fin)
    parts.append(I.tube([(0.056, -0.0052, -0.054), (0.090, -0.0052, -0.068), (0.126, -0.0052, -0.126), (0.143, -0.0052, -0.196)], 0.0018, 6, gl, "finseam"))
    pose(parts, (0, 20, 18))
    return dict(fill=0.86)


@icon("gloves_5")
def gloves_5():
    """燼握: dark gauntlet edged in tarnished gold, half closed in a grip, four ember stones burning on the knuckles"""
    dk = dark5(); gd = I.gold(); emb = hot(4.4, 150.0, 0.30, 1.9); bl = I.leather((0.03, 0.025, 0.022))
    parts, palm, J, T = hand(bl, r=0.0102, curl=22.0)
    for j in J:
        for k in range(3):
            parts.append(lame(j[k], j[k + 1], 0.0131 - 0.0006 * k, dk, 0.26 if k == 0 else 0.10, 0.95))
        for k in (1, 2):
            parts.append(aim(I.torus(0.0120 - 0.0006 * k, 0.0025, gd, "joint", 20, 6), j[k], j[k] - j[k - 1]))
    for k in range(2):
        parts.append(lame(T[k], T[k + 1], 0.0144 - 0.0006 * k, dk, 0.28 if k == 0 else 0.10, 0.95))
    parts.append(aim(I.torus(0.0132, 0.0026, gd, "joint", 20, 6), T[1], T[1] - T[0]))
    parts.append(palm.band(0.010, 0.090, dk, 0.0056, 0.006, -86, 86, 16, 5, "backplate"))
    parts.append(palm.ring(0.012, 0.0030, gd, 0.0058, -84, 84))
    for s in (-1, 1):
        parts.append(palm.line([(s * 84, 0.012), (s * 84, 0.088)], 0.0030, gd, 0.0058))
    kn = knuckle_pts(-0.0165, 0.002)
    parts.append(I.tube([kn[0] + Vector((-0.014, 0.008, -0.005))] + kn + [kn[-1] + Vector((0.013, 0.008, -0.005))], 0.0092, 10, gd, "knucklebar"))
    for p in kn:
        st = I.ball(0.0128, emb, "ember", 2, (1.0, 0.9, 1.0)); st.location = p + Vector((0, -0.0055, 0.0035)); parts.append(st)
        parts.append(aim(I.torus(0.0128, 0.0030, gd, "collet", 20, 6), p + Vector((0, -0.0030, 0.0020)), Vector((0, -0.85, 0.5))))
    C = Shell([(-0.122, 0.080, 0.064), (-0.062, 0.055, 0.042), (-0.006, 0.043, 0.029), (0.018, 0.044, 0.029)], power=2.4)
    parts.append(C.mesh(dk, 44, thick=0.0062, name="cuff"))
    parts.append(C.ring(-0.122, 0.0066, gd, 0.001))
    parts.append(C.band(-0.012, 0.018, gd, 0.0058, 0.005, name="wrist"))
    parts.append(C.line([(0, -0.116), (0, -0.016)], 0.0050, gd, 0.001, name="ridge"))
    tab = tongue(0.066, 0.078, 0.007, dk, 1.0, 2, "peak"); tabg = tongue(0.082, 0.094, 0.005, gd, 1.0, 2, "peakedge")
    tabg.location = (0, 0.003, 0.006)
    parts.append(on(C, [tab, tabg], 0, -0.108, 0.006, tilt=-20))
    pose(parts, GLOVE_POSE)
    return dict(fill=0.84)


# ================================================================================================ PANTS
def legs_shells(fat=1.0, hem=False, ankle=-0.64):
    """hips + two legs. hem=False: the hip shell dives inside the thighs (one garment); hem=True: it ends in a visible hem"""
    low = (-0.226, 0.210, 0.122) if hem else (-0.238, 0.166, 0.092)
    hips = Shell([low, (-0.150, 0.198, 0.129), (-0.065, 0.185, 0.126), (0.0, 0.167, 0.116)], power=2.4)
    L = {}
    for s in (-1, 1):
        L[s] = Shell([(ankle, 0.060 * fat, 0.064 * fat, 0.0, s * 0.152), (ankle + 0.12, 0.076 * fat, 0.080 * fat, 0.004, s * 0.141),
                      (-0.40, 0.079 * fat, 0.083 * fat, 0.0, s * 0.127), (-0.25, 0.097 * fat, 0.105 * fat, 0.0, s * 0.105), (-0.13, 0.092, 0.105, 0.0, s * 0.088)])
    return hips, L


def rope(shell, z, mat, off=0.008, rho=0.0050, r=0.0062, tw=26, n=10):
    """two-strand twisted rope round a shell"""
    out = []
    N = tw * n
    for st in (0, 1):
        P = []
        for i in range(N):
            phi = -180.0 + 360.0 * i / N
            tau = 2 * math.pi * tw * i / N + math.pi * st
            P.append(shell.pt(phi, z, off, follow=False) + shell.nrm(phi, z) * (rho * math.cos(tau)) + Vector((0, 0, rho * math.sin(tau))))
        out.append(I.tube(P, r, 6, mat, "rope", closed=True))
    return out


@icon("pants_1")
def pants_1():
    """粗布護腿: baggy patched homespun trousers, rope belt, ankles bound with linen strips"""
    cl = I.cloth((0.25, 0.20, 0.14), hem_lo=-0.95, hem_hi=-0.45)
    rp = I.cloth((0.47, 0.40, 0.28), weave='burlap', hem_lo=-5.0, hem_hi=-4.0)
    pa = I.cloth((0.37, 0.32, 0.25), hem_lo=-5.0, hem_hi=-4.0); pb = I.cloth((0.12, 0.115, 0.115), hem_lo=-5.0, hem_hi=-4.0)
    th = I.cloth((0.55, 0.49, 0.36), hem_lo=-5.0, hem_hi=-4.0); thd = I.leather((0.05, 0.035, 0.025))
    hips, L = legs_shells(1.14)
    wr = lambda phi, z: 0.0065 * math.sin(R(phi * 4) + z * 34) + 0.0035 * math.sin(R(phi * 2) - z * 80)
    hips.bump = wr
    parts = [hips.mesh(cl, 64, sub=4, thick=0.004, name="hips")]
    for s in (-1, 1):
        L[s].bump = wr
        parts.append(L[s].mesh(cl, 48, sub=4, thick=0.004, name="leg"))
        for k in range(3):
            z = -0.640 + 0.027 * k
            parts.append(L[s].band(z - 0.004, z + 0.030, pa, 0.0065 + 0.002 * (k % 2), 0.004, name="wrap", follow=False))
    parts.append(hips.band(-0.034, 0.004, cl, 0.006, 0.006, name="waistband", follow=False))
    parts += rope(hips, -0.016, rp, 0.0125)
    k = hips.pt(-12, -0.016, 0.022, follow=False)
    kn = I.ball(0.017, rp, "knot", 2, (1.15, 0.8, 1.0)); kn.location = k; parts.append(kn)
    for (dx, ln) in ((-0.022, 0.115), (0.016, 0.150)):
        e = k + Vector((dx, -0.006, -ln))
        parts.append(I.tube([k, k + Vector((dx * 0.7, -0.012, -ln * 0.4)), e], 0.0066, 8, rp, "ropeend"))
        tz = I.cone(0.0066, 0.0105, 0.026, rp, "tassel", 10); tz.location = e + Vector((0, 0, -0.024)); parts.append(tz)
    # patches
    parts.append(L[-1].band(-0.462, -0.338, pa, 0.0052, 0.003, -54, 40, 10, 5, "patch"))
    parts += L[-1].stitch([(-49, -0.455), (35, -0.455), (35, -0.345), (-49, -0.345), (-49, -0.455)], thd, 0.0062, 0.013, 0.011, 0.0019)
    parts.append(L[1].band(-0.292, -0.182, pb, 0.0052, 0.003, -34, 50, 10, 5, "patch2"))
    parts += L[1].stitch([(-29, -0.286), (45, -0.286), (45, -0.189), (-29, -0.189), (-29, -0.286)], th, 0.0062, 0.013, 0.011, 0.0019)
    pose(parts, (0, 0, 22))
    return dict(fill=0.86)


@icon("pants_2")
def pants_2():
    """鎖環護腿: mail chausses under a quilted linen breech, leather belt, iron knee roundels on leather garters"""
    ml = mail(cell=0.0165); cl = I.cloth(LINEN, hem_lo=-5.0, hem_hi=-4.0); lt = I.leather(); ir = iron2(); bd = I.iron(amount=0.42, color=(0.20, 0.195, 0.19))
    hips, L = legs_shells(0.95, hem=True)
    hips.bump = lambda phi, z: 0.009 * abs(math.sin(R(phi) * 11)) ** 0.55
    parts = [hips.mesh(cl, 176, sub=4, thick=0.006, name="breech")]
    parts.append(hips.band(-0.036, 0.006, lt, 0.014, 0.005, name="belt", follow=False))
    parts.append(on(hips, buckle(ir, 0.036, 0.052, 0.004), 0, -0.015, 0.017, follow=False))
    parts.append(hips.line([(-8, -0.016), (-13, -0.06), (-14, -0.125)], 0.0085, lt, 0.019, follow=False, name="tongue"))
    for s in (-1, 1):
        Ls = L[s]
        Ls.bump = lambda phi, z: 0.004 * math.sin(z * 70 + R(phi)) * sm(-0.50, -0.62, z)
        parts.append(Ls.mesh(ml, 64, sub=4, thick=0.004, z1=-0.150, name="chausse"))
        parts.append(Ls.band(-0.474, -0.448, lt, 0.0055, 0.004, name="garter", follow=False))
        parts.append(on(Ls, buckle(ir, 0.020, 0.032, 0.0027), 58 * s, -0.461, 0.0075, follow=False))
        dome = I.lathe([(0.0, 0.020), (0.020, 0.018), (0.040, 0.009), (0.048, 0.0)], 24, ir, "roundel"); I.solid(dome, 0.0062)
        dome.rotation_euler = (R(90), 0, 0)
        boss = I.ball(0.0085, bd, "boss", 2); boss.location = (0, -0.022, 0)
        parts.append(on(Ls, [dome, boss], -3 * s, -0.402, 0.004, follow=False))
        parts.append(Ls.band(-0.415, -0.390, lt, 0.0045, 0.004, 40, 320, 30, name="kneestrap", follow=False))
        parts.append(Ls.ring(-0.640, 0.0062, lt, 0.001, follow=False))
    pose(parts, (0, 0, 22))
    return dict(fill=0.86)


@icon("pants_3")
def pants_3():
    """鱗片腿甲: leather breeches, two scale tassets hung from a studded belt, steel knee cops with brass bosses, splinted shins"""
    st = steel3(); br = I.brass(); lt = I.leather((0.125, 0.078, 0.045)); dk = I.leather()
    hips, L = legs_shells(1.0)
    parts = [hips.mesh(lt, 56, sub=4, thick=0.004, name="hips")]
    parts.append(hips.band(-0.038, 0.008, dk, 0.012, 0.006, name="belt"))
    parts += hips.rivets(-0.015, 24, br, 0.0052, off=0.0125)
    plate = I.box(0.058, 0.008, 0.062, br, "beltplate", 0.003); hips.put(plate, 0, -0.015, 0.013); parts.append(plate)
    w, h = 0.052, 0.068
    proto = baked(tongue(w, h, 0.0045, st, 0.25, 6)); proto_b = None; used = False
    for k in range(4):
        z = -0.040 - 0.046 * k
        g = hips.girth(z - h * 0.5)
        for s in (-1, 1):
            a0, a1 = (13.0, 132.0) if s > 0 else (-132.0, -13.0)
            n = max(3, int(round(g * (a1 - a0) / 360.0 / (w * 0.86))))
            for i in range(n):
                if k == 3:
                    if proto_b is None:
                        proto_b = clone(proto, "scale_b", br); o = proto_b
                    else:
                        o = clone(proto_b)
                elif not used:
                    o = proto; used = True
                else:
                    o = clone(proto)
                hips.put(o, a0 + (a1 - a0) * (i + 0.25 + 0.5 * (k % 2)) / n, z, 0.0035, tilt=-8.5)
                parts.append(o)
    for s in (-1, 1):
        Ls = L[s]
        parts.append(Ls.mesh(lt, 44, sub=4, thick=0.004, name="leg"))
        dome = I.lathe([(0.0, 0.036), (0.024, 0.032), (0.050, 0.018), (0.064, 0.0)], 24, st, "cop"); I.solid(dome, 0.0045)
        dome.rotation_euler = (R(90), 0, 0)
        boss = I.ball(0.012, br, "boss", 2); boss.location = (0, -0.038, 0)
        wing = I.lathe([(0.0, 0.006), (0.030, 0.005), (0.040, 0.0), (0.0, -0.002)], 20, st, "wing")
        I.place(wing, loc=(s * 0.062, 0.020, 0.0), rot=(90, 0, s * 58))
        parts.append(on(Ls, [dome, boss, wing], -3 * s, -0.402, 0.004))
        for (z0, z1) in ((-0.372, -0.340), (-0.462, -0.432)):
            parts.append(Ls.band(z0, z1, st, 0.0065, 0.0045, -78, 78, 14, name="kneelame"))
        for a in (-36, 0, 36):
            parts.append(Ls.band(-0.628, -0.476, st, 0.0060, 0.0038, a - 10.5, a + 10.5, 4, 6, "splint"))
            for z in (-0.490, -0.612):
                rv = I.ball(0.0042, br, "rivet", 2); rv.location = Ls.pt(a, z, 0.0066); parts.append(rv)
        for z in (-0.520, -0.596):
            parts.append(Ls.band(z, z + 0.020, dk, 0.0042, 0.003, 52, 308, 30, name="strap"))
        parts.append(Ls.band(-0.648, -0.628, dk, 0.0060, 0.005, name="cuff"))
    pose(parts, (0, 0, 22))
    return dict(fill=0.86)


@icon("pants_4")
def pants_4():
    """灰鑄腿鎧: ash-forged leg harness: hip plate, pointed tassets, cuisses, finned poleyns, ridged greaves; ember in the seams"""
    ash = I.ashplate(); gl = seam(3.0); bl = I.leather((0.022, 0.022, 0.026))
    hips, L = legs_shells(0.98)
    parts = [hips.mesh(bl, 48, sub=4, thick=0.004, name="hips")]
    parts.append(hips.band(-0.082, 0.010, ash, 0.008, 0.0062, rows=4, name="hipplate"))
    parts.append(hips.ring(0.008, 0.0068, ash, 0.009))
    parts.append(hips.ring(-0.040, 0.0022, gl, 0.0088))
    parts.append(hips.ring(-0.088, 0.0024, gl, 0.0040))
    for s in (-1, 1):
        Ls = L[s]
        parts.append(Ls.mesh(bl, 40, sub=4, thick=0.004, name="leg"))
        # pointed tasset with a ridge and two ember lines
        t = tongue(0.132, 0.200, 0.008, ash, 1.15, 2, "tasset", bev=0.003)
        rd = I.tube([(0, -0.006, -0.012), (0, -0.006, -0.178)], [0.0052, 0.0030], 6, ash, "ridge")
        sl = [I.tube([(sx * 0.018, -0.0048, -0.014), (sx * 0.014, -0.0048, -0.105), (sx * 0.005, -0.0048, -0.172)], 0.0020, 6, gl, "seam") for sx in (-1, 1)]
        parts.append(on(hips, [t, rd] + sl, s * 44, -0.070, 0.0105, tilt=-2))
        parts.append(Ls.band(-0.368, -0.236, ash, 0.0075, 0.0062, -108, 108, 20, 5, "cuisse"))
        parts.append(Ls.line([(0, -0.360), (0, -0.244)], 0.0048, ash, 0.0085, name="ridge"))
        parts.append(Ls.ring(-0.374, 0.0023, gl, 0.0045, -104, 104))
        cop = I.lathe([(0.0, 0.066), (0.012, 0.050), (0.034, 0.026), (0.056, 0.008), (0.064, 0.0)], 20, ash, "poleyn"); I.solid(cop, 0.0062)
        cop.rotation_euler = (R(90), 0, 0)
        fn = I.extrude([(0.0, -0.034), (s * 0.050, -0.020), (s * 0.100, 0.022), (s * 0.052, 0.022), (0.0, 0.040)], 0.008, ash, "kneefin", bev=0.003)
        fn.location = (s * 0.052, 0.022, 0.0); fn.rotation_euler = (0, 0, R(s * 38))
        parts.append(on(Ls, [cop, fn], -3 * s, -0.404, 0.005))
        parts.append(Ls.band(-0.462, -0.436, ash, 0.0075, 0.0062, -84, 84, 14, name="kneelame"))
        parts.append(Ls.ring(-0.466, 0.0023, gl, 0.0045, -100, 100))
        parts.append(Ls.band(-0.640, -0.472, ash, 0.0075, 0.0062, -116, 116, 20, 6, "greave"))
        parts.append(Ls.line([(0, -0.632), (0, -0.480)], 0.0050, ash, 0.0085, name="ridge"))
        for sx in (-1, 1):
            parts.append(Ls.line([(sx * 15, -0.626), (sx * 13, -0.486)], 0.0019, gl, 0.0080, name="seam"))
        parts.append(Ls.band(-0.658, -0.630, ash, 0.0110, 0.006, name="anklerim"))
    pose(parts, (0, 0, 22))
    return dict(fill=0.86)


@icon("pants_5")
def pants_5():
    """燼行脛甲: dark leg harness banded with tarnished gold, long loin plate, an ember stone burning in each knee"""
    dk = dark5(); gd = I.gold(); emb = hot(4.4, 62.0, 0.30, 1.9); bl = I.leather((0.03, 0.025, 0.022))
    hips, L = legs_shells(1.0)
    parts = [hips.mesh(bl, 48, sub=4, thick=0.004, name="hips")]
    parts.append(hips.band(-0.040, 0.010, gd, 0.013, 0.008, name="belt"))
    parts.append(hips.band(-0.104, -0.034, dk, 0.008, 0.0062, rows=3, name="fauld1"))
    parts.append(hips.band(-0.172, -0.096, dk, 0.0115, 0.0062, rows=3, name="fauld2"))
    parts.append(hips.ring(-0.104, 0.0040, gd, 0.0085))
    parts.append(hips.ring(-0.174, 0.0060, gd, 0.0120))
    # loin plate, gold edged
    lp = tongue(0.110, 0.270, 0.009, dk, 1.0, 2, "loin", bev=0.003)
    eg = I.tube([(-0.055, -0.004, 0.0), (-0.055, -0.004, -0.160), (0.0, -0.004, -0.270), (0.055, -0.004, -0.160), (0.055, -0.004, 0.0)], 0.0056, 6, gd, "loinedge")
    bs = I.ball(0.016, gd, "boss", 2, (1, 0.6, 1)); bs.location = (0, -0.006, -0.085)
    parts.append(on(hips, [lp, eg, bs], 0, -0.030, 0.019, tilt=-3))
    for s in (-1, 1):
        Ls = L[s]
        parts.append(Ls.mesh(bl, 40, sub=4, thick=0.004, name="leg"))
        parts.append(Ls.band(-0.362, -0.226, dk, 0.0075, 0.0062, -108, 108, 20, 5, "cuisse"))
        parts.append(Ls.ring(-0.362, 0.0052, gd, 0.0085, -108, 108))
        ring = I.torus(0.050, 0.0105, gd, "kneering", 32, 8); ring.rotation_euler = (R(90), 0, 0); ring.location = (0, -0.006, 0)
        stone = I.ball(0.044, emb, "kneestone", 3, (1.0, 0.55, 1.0)); stone.location = (0, -0.004, 0)
        back = I.lathe([(0.0, 0.012), (0.040, 0.010), (0.062, 0.0)], 24, dk, "kneeplate"); back.rotation_euler = (R(90), 0, 0); back.location = (0, 0.006, 0)
        parts.append(on(Ls, [ring, stone, back], -3 * s, -0.404, 0.010))
        parts.append(Ls.band(-0.640, -0.458, dk, 0.0075, 0.0062, -116, 116, 20, 6, "greave"))
        parts.append(Ls.ring(-0.458, 0.0052, gd, 0.0085, -116, 116))
        parts.append(Ls.line([(0, -0.632), (0, -0.468)], 0.0048, gd, 0.0085, name="ridge"))
        parts.append(Ls.band(-0.660, -0.626, gd, 0.0115, 0.0065, name="anklet"))
    pose(parts, (0, 0, 22))
    return dict(fill=0.86)


# ================================================================================================ BOOTS
# foot table: t (heel 0 .. toe 1), y, centre height, half width, half height     (toe points to -Y)
FOOT = [(0.00, 0.086, 0.040, 0.026, 0.034), (0.12, 0.060, 0.050, 0.042, 0.050), (0.35, -0.005, 0.058, 0.048, 0.058),
        (0.55, -0.075, 0.047, 0.051, 0.047), (0.75, -0.135, 0.037, 0.054, 0.037), (0.90, -0.180, 0.029, 0.047, 0.029), (1.00, -0.207, 0.020, 0.022, 0.018)]
POINTED = FOOT[:5] + [(0.90, -0.192, 0.027, 0.040, 0.026), (1.00, -0.290, 0.013, 0.005, 0.007)]
SHAFT = [(0.045, 0.048, 0.062, 0.026), (0.12, 0.046, 0.054, 0.034), (0.22, 0.053, 0.058, 0.037), (0.33, 0.064, 0.066, 0.040)]
XA = (Vector((1, 0, 0)), Vector((0, 0, 1)))


def foot_part(tab, t0, t1, mat, off=0.0, n=6, name="foot", power=2.5, lift=0.3):
    """slice of the foot between t0 and t1, grown by `off` (toe caps, sabaton lames)"""
    secs = []
    for i in range(n + 1):
        y, zc, rx, rz = herm(tab, t0 + (t1 - t0) * i / n)
        secs.append(((0, y, zc + off * lift), max(rx + off, 0.002), max(rz + off, 0.002), XA, power))
    return I.loft(secs, 24, mat, name, caps=(True, True))


def sole(tab, mat, t0=0.0, t1=1.0, zc=-0.010, rz=0.011, wide=1.08, name="sole", n=12):
    secs = []
    for i in range(n + 1):
        y, _, rx, _ = herm(tab, t0 + (t1 - t0) * i / n)
        secs.append(((0, y, zc), rx * wide + 0.003, rz, XA, 4.0))
    return I.loft(secs, 24, mat, name, caps=(True, True))


def boot(foot_mat, shaft_mat, sole_mat, tab=FOOT, shaft=SHAFT, thick=0.0062, sole_rz=0.011, bump=None):
    """-> (parts, shaft Shell)"""
    Sh = Shell(shaft, power=2.2, bump=bump)
    parts = [foot_part(tab, 0.0, 1.0, foot_mat, 0.0, 14), Sh.mesh(shaft_mat, 36, sub=4, thick=thick, name="shaft"),
             sole(tab, sole_mat, rz=sole_rz, zc=-sole_rz + 0.002),
             sole(tab, sole_mat, 0.0, 0.27, zc=-2 * sole_rz - 0.008, rz=0.012, wide=1.0, name="heel", n=5)]
    top = Sh.L[-1]
    parts.append(void_disc((top[4], top[3], top[0] - 0.014), (0, 0, 1), min(top[1], top[2]) - thick * 0.6, 0.12))
    return parts, Sh


def pair(make, yaw=-54.0, gap=0.150, stagger=0.050):
    """two boots: the near one right-front, the far one left-behind and a little ahead; toes to the lower left"""
    d = Vector((math.sin(R(yaw)), -math.cos(R(yaw)), 0.0))
    side = Vector((-d.y, d.x, 0.0))
    a = I.group(make(0)); b = I.group(make(1))
    I.place(a, loc=side * (gap / 2), rot=(0, 0, yaw))
    I.place(b, loc=side * (-gap / 2) + d * stagger, rot=(0, 0, yaw + 8))
    return a, b


def welt(tab, mat, x_side=1, z=0.004, wide=1.08):
    P = []
    for i in range(40):
        y, _, rx, _ = herm(tab, 0.04 + 0.90 * i / 39)
        P.append(Vector((x_side * (rx * wide + 0.0042), y, z)))
    return dashes(P, mat, 0.009, 0.008, 0.0016)


@icon("boots_1")
def boots_1():
    """旅人靴: soft leather travel boots, turned-down cuff, ankle strap, thick stitched sole"""
    lt = I.leather(TAN, scale=64.0, bump=0.24); cf = I.leather((0.31, 0.21, 0.12), scale=64.0, bump=0.24); dk = I.leather(); so = I.leather((0.045, 0.032, 0.024))
    ir = iron2(); th = I.cloth((0.50, 0.44, 0.33))

    def make(i):
        parts, Sh = boot(lt, lt, so, bump=lambda phi, z: 0.0042 * math.sin(z * 110 + R(phi * 2)) * sm(0.215, 0.11, z))
        parts.append(Sh.band(0.258, 0.340, cf, 0.011, 0.009, rows=3, name="cuff"))
        parts += Sh.stitch([(a, 0.268) for a in range(-160, 161, 20)], th, 0.0116, 0.010, 0.009, 0.0017)
        parts.append(Sh.band(0.088, 0.112, dk, 0.0065, 0.004, name="strap", follow=False))
        parts.append(on(Sh, buckle(ir, 0.024, 0.036, 0.0030), 64, 0.100, 0.0085, follow=False))
        parts.append(foot_part(FOOT, 0.77, 1.0, lt, 0.0028, 5, "toecap"))
        parts += welt(FOOT, th)
        return parts
    pair(make)
    return dict(fill=0.84)


@icon("boots_2")
def boots_2():
    """鐵靴: heavy leather boots shod with iron: toe cap, heel cup, riveted shin plate held by two straps"""
    dk = I.leather((0.060, 0.040, 0.028)); sp = I.leather((0.035, 0.025, 0.018)); so = I.leather((0.03, 0.022, 0.016)); ir = iron2(); bd = I.iron(amount=0.42, color=(0.20, 0.195, 0.19))

    def make(i):
        parts, Sh = boot(dk, dk, so)
        parts.append(foot_part(FOOT, 0.68, 1.0, ir, 0.0050, 6, "toecap"))
        parts.append(foot_part(FOOT, 0.0, 0.17, ir, 0.0050, 4, "heelcup"))
        for sx in (-1, 1):
            y, zc, rx, rz = herm(FOOT, 0.72)
            rv = I.ball(0.0044, bd, "rivet", 2); rv.location = (sx * (rx + 0.0052), y, zc * 0.8); parts.append(rv)
        parts.append(Sh.band(0.092, 0.340, ir, 0.0090, 0.006, -66, 66, 14, 8, "shin"))
        for z in (0.112, 0.320):
            parts += Sh.rivets(z, 2, bd, 0.0046, -106, 106, 0.0095)
        parts.append(Sh.line([(0, 0.104), (0, 0.332)], 0.0045, bd, 0.0095, name="ridge"))
        parts.append(Sh.ring(0.340, 0.0048, bd, 0.0065, -66, 66))
        for z in (0.150, 0.272):
            parts.append(Sh.band(z, z + 0.026, sp, 0.0055, 0.004, 62, 298, 30, name="strap"))
            parts.append(on(Sh, buckle(bd, 0.022, 0.034, 0.0028), 82, z + 0.013, 0.0075))
        return parts
    pair(make)
    return dict(fill=0.84)


@icon("boots_3")
def boots_3():
    """行者戰靴: steel sabatons of five sliding lames, ridged greave, brass rivets and top band"""
    st = steel3(); br = I.brass(); dk = I.leather(); so = I.leather((0.04, 0.03, 0.022))

    def make(i):
        parts, Sh = boot(st, st, so)
        for (t0, t1, off) in ((0.24, 0.47, 0.0115), (0.42, 0.62, 0.0095), (0.57, 0.76, 0.0075), (0.71, 0.89, 0.0055), (0.84, 1.0, 0.0035)):
            parts.append(foot_part(FOOT, t0, t1, st, off, 5, "lame"))
            y, zc, rx, rz = herm(FOOT, t1 - 0.05)
            for sx in (-1, 1):
                rv = I.ball(0.0044, br, "rivet", 2); rv.location = (sx * (rx + off + 0.0006), y, zc * 0.8); parts.append(rv)
        parts.append(foot_part(FOOT, 0.0, 0.20, st, 0.0065, 4, "heel"))
        parts.append(Sh.band(0.050, 0.108, st, 0.0065, 0.005, name="anklelame"))
        parts.append(Sh.line([(0, 0.128), (0, 0.300)], 0.0050, st, 0.001, name="ridge"))
        parts.append(Sh.band(0.298, 0.346, st, 0.0075, 0.005, name="topband"))
        parts.append(Sh.ring(0.346, 0.0054, br, 0.0075))
        parts.append(Sh.ring(0.298, 0.0038, br, 0.0080))
        parts += Sh.rivets(0.322, 10, br, 0.0044, off=0.0080)
        return parts
    pair(make)
    return dict(fill=0.84)


@icon("boots_4")
def boots_4():
    """灰鑄戰靴: ash-forged sabatons drawn out to a long point, ember light between the lames, knee point and spur fins"""
    ash = I.ashplate(); gl = seam(3.0)

    def make(i):
        parts, Sh = boot(ash, ash, ash, tab=POINTED)
        lam = ((0.24, 0.47, 0.0115), (0.42, 0.62, 0.0095), (0.57, 0.76, 0.0075), (0.71, 0.905, 0.0055))
        for k, (t0, t1, off) in enumerate(lam):
            parts.append(foot_part(POINTED, t0, t1, ash, off, 5, "lame"))
            nxt = lam[k + 1][2] if k + 1 < len(lam) else 0.0
            parts.append(foot_part(POINTED, t1 - 0.004, t1 + 0.009, gl, nxt + 0.0016, 2, "seam"))
        parts.append(foot_part(POINTED, 0.0, 0.20, ash, 0.0065, 4, "heel"))
        parts.append(Sh.band(0.050, 0.106, ash, 0.0065, 0.0062, name="anklelame"))
        parts.append(Sh.ring(0.112, 0.0030, gl, 0.0040))
        parts.append(Sh.band(0.118, 0.346, ash, 0.0065, 0.0062, rows=6, name="greave"))
        parts.append(Sh.line([(0, 0.128), (0, 0.338)], 0.0052, ash, 0.0075, name="ridge"))
        for sx in (-1, 1):
            parts.append(Sh.line([(sx * 17, 0.132), (sx * 14, 0.332)], 0.0019, gl, 0.0070, name="seam"))
        kp = tongue(0.076, 0.100, 0.008, ash, 1.0, 2, "kneepoint", bev=0.003)
        parts.append(on(Sh, [kp], 0, 0.318, 0.011, tilt=-8, roll=180))
        for (z, sc) in ((0.265, 1.0), (0.060, 0.62)):
            f = I.extrude([(0.0, -0.030), (0.050, -0.014), (0.118, 0.062), (0.070, 0.030), (0.034, 0.036), (0.0, 0.034)], 0.008, ash, "spur", bev=0.003)
            lv = Sh.lv(z)
            I.place(f, loc=(0, lv[2] + lv[1] - 0.006, z), rot=(0, 0, 90), scale=sc); parts.append(f)
        return parts
    pair(make)
    return dict(fill=0.84)


@icon("boots_5")
def boots_5():
    """踏燼者: dark sabatons trimmed with tarnished gold, winged cuff, thick soles of glowing cracked ember that still lick flame"""
    dk = dark5(); gd = I.gold(); emb = hot(4.6, 58.0, 0.34, 1.7); fl = I.fire(strength=4.5, key="solefire")

    def make(i):
        parts, Sh = boot(dk, dk, emb, sole_rz=0.017)
        for (t0, t1, off) in ((0.26, 0.50, 0.0105), (0.45, 0.68, 0.0080), (0.63, 0.86, 0.0055)):
            parts.append(foot_part(FOOT, t0, t1, dk, off, 5, "lame"))
            parts.append(foot_part(FOOT, t1 - 0.016, t1 + 0.004, gd, off + 0.0014, 2, "trim"))
        parts.append(foot_part(FOOT, 0.83, 1.0, gd, 0.0045, 4, "toecap"))
        parts.append(foot_part(FOOT, 0.0, 0.19, dk, 0.0065, 4, "heel"))
        parts.append(Sh.band(0.052, 0.090, gd, 0.0075, 0.006, name="anklet"))
        parts.append(Sh.line([(0, 0.128), (0, 0.300)], 0.0052, gd, 0.001, name="ridge"))
        parts.append(Sh.band(0.292, 0.346, gd, 0.0085, 0.0065, name="topband"))
        for s in (-1, 1):
            w = I.extrude([(0.0, -0.016), (0.052, -0.004), (0.090, 0.050), (0.104, 0.112), (0.064, 0.070), (0.030, 0.056), (0.0, 0.050)], 0.007, dk, "wing", bev=0.0028)
            lv = Sh.lv(0.300)
            w.location = (s * (lv[0] + 0.004), lv[2] - 0.012, 0.296); w.rotation_euler = Euler((R(s * 12), 0, R(90 - s * 14)), 'XYZ'); parts.append(w)
            e = I.tube([(0.0, 0, -0.016), (0.052, 0, -0.004), (0.090, 0, 0.050), (0.104, 0, 0.112)], 0.0040, 6, gd, "wingedge")
            e.location = w.location; e.rotation_euler = w.rotation_euler; parts.append(e)
        rnd = random.Random(5 + i)
        for k in range(5):
            t = rnd.uniform(0.05, 0.95)
            y, zc, rx, rz = herm(FOOT, t)
            f = I.flame(rnd.uniform(0.034, 0.058), 0.012, fl, seed=3 + k + 7 * i)
            f.location = (rnd.choice((-1, 1)) * (rx * 1.08 + 0.004), y, -0.012); f["nofit"] = 1; parts.append(f)
        return parts
    pair(make)
    return dict(fill=0.84)
