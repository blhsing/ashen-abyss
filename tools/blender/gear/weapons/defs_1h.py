"""One-hand weapons: sword1, axe1, mace1, dagger, wand, scythe1, flail, fist, sceptre.
Weapon-local space: +Z up the weapon, +X edge / knuckles, +Y palm normal; origin = centre of the grip (the fist covers
about z -0.045 .. +0.045). v1 crude, v2 forged, v3 ornate / glowing."""
from gearparts import *
from defs import base

ONE = dict(mount="hand_r", grip="r", hands=1)


def nick(st, z, depth, side=1, w=0.007):
    """notch in a blade border: inserts three stations round z (side +1 = the +b border, -1 = the a border)"""
    zs = [q[0] for q in st]
    def at(zz):
        return [float(np.interp(zz, zs, [q[c] for q in st])) for c in (1, 2, 3)]
    out = [q for q in st if abs(q[0] - z) > w * 1.05]
    for dz, k in ((-w, 0.0), (0.0, 1.0), (w, 0.0)):
        a, b, t = at(z + dz)
        if side > 0:
            b -= depth * k
        else:
            a += depth * k
        out.append((z + dz, a, b, t))
    return sorted(out)


# =============================================================================================== sword1 劍
@base("sword1", info=ONE, texel=0.0010)
def sword1(w, v):
    if v == 1:
        # scavenged short sword: broad chipped blade ground from a bar, clipped tip, bent bar guard, cord-bound wooden grip
        st = dense([sym(0.058, 0.0300, 0.0068), sym(0.11, 0.0310, 0.0066), sym(0.30, 0.0295, 0.0060), sym(0.47, 0.0285, 0.0054),
                    sym(0.575, 0.0250, 0.0048), sym(0.635, 0.0165, 0.0040), sym(0.668, 0.0, 0.0)], 22)
        st[-2] = (0.650, -0.0105, 0.0075, 0.0036)
        st[-1] = (0.672, -0.0035, -0.0035, 0.0)
        for z, d, s in ((0.205, 0.0045, 1), (0.335, 0.0060, 1), (0.41, 0.0035, -1), (0.52, 0.0050, 1), (0.265, 0.0030, -1)):
            st = nick(st, z, d, s)
        w(ribbon(st, sec_hex(0.0085)), "crudeblade", sharp=14, hem=60)
        w(g_tube(smooth_path([(-0.088, 0, 0.049), (-0.045, 0, 0.056), (0, 0, 0.058), (0.04, 0, 0.0565), (0.082, 0, 0.062)], 9),
                 [0.0085, 0.0095, 0.0115, 0.012, 0.0125, 0.012, 0.0115, 0.0095, 0.0085], n=6, sy=0.82, up=(0, 1, 0)), "rustiron", sharp=50)
        w(grip_core(-0.060, 0.050, 0.0138, n=8), "wood", sharp=60)
        w(grip_wrap(-0.052, 0.042, 0.0138, turns=9.5, width=0.0052, thick=0.0042, n=6), "rope", sharp=80)
        w(gjoin(g_tube([(0.012, 0.006, -0.050), (0.021, 0.010, -0.066), (0.016, 0.012, -0.092)], [0.0026, 0.0026, 0.0022], n=5),
                g_tube([(0.010, -0.007, -0.050), (0.019, -0.011, -0.070), (0.024, -0.008, -0.101)], [0.0026, 0.0026, 0.0022], n=5)), "rope", sharp=80)
        w(g_lathe([(0.0, -0.090), (0.016, -0.088), (0.021, -0.078), (0.021, -0.066), (0.015, -0.059), (0.0, -0.059)], n=6), "rustiron", sharp=30)
    elif v == 2:
        # soldier's arming sword: short broad fullered blade, wide straight cross with flared ends, leather grip, wheel pommel
        fd = lambda s: 0.55 * float(sstep(s, 0.52, 0.42)) * float(sstep(s, 0.072, 0.10))
        st = dense([sym(0.074, 0.0345, 0.0062), sym(0.15, 0.0338, 0.0060), sym(0.36, 0.0312, 0.0054), sym(0.55, 0.0270, 0.0046),
                    sym(0.67, 0.0205, 0.0038), sym(0.730, 0.0115, 0.0030), sym(0.762, 0.0, 0.0)], 28)
        w(ribbon(st, sec_fuller(0.0075, 0.42, fd)), "blade", sharp=14, hem=60)
        q = [(0.014, 0, 0.0662), (0.060, 0, 0.0660), (0.105, 0, 0.0652), (0.142, 0, 0.0640)]
        w(both_x(quillon(q, [0.0108, 0.0094, 0.0086, 0.0084], n=8, flat=0.86)), "steel", sharp=50)
        w(both_x(g_prism([(0.136, 0.0560), (0.158, 0.0500), (0.164, 0.0640), (0.158, 0.0780), (0.136, 0.0720)], 0.0150)), "steel", bevel=0.0030, sharp=40)
        w(g_box(0.044, 0.0240, 0.0240, (0, 0, 0.0662)), "steel", bevel=0.0045, sharp=40)
        w(g_prism([(-0.0120, 0.0745), (0.0120, 0.0745), (0.0, 0.1040)], 0.0240), "steel", bevel=0.0015, sharp=40)
        w(grip_core(-0.056, 0.056, 0.0132, n=10), "grip", sharp=60)
        w(grip_wrap(-0.046, 0.046, 0.0132, turns=8.0, width=0.0060, thick=0.0022, n=4), "grip", sharp=70)
        w(rings((-0.0515, 0.0515), 0.0140, h=0.0075, n=10, bulge=1.10), "brass", sharp=50)
        w(wheel_pommel(0.0255, 0.0165, -0.0810, n=16), "steel", sharp=40)
        w(g_lathe([(0.0, -0.1105), (0.0055, -0.1095), (0.0068, -0.1040), (0.0, -0.1040)], n=6), "brass", sharp=40)
    else:
        # ember-fullered blade of dark steel: flared lugs over the ricasso, leaf swell, winged gilt guard, crystal pommel
        fd = lambda s: 0.62 * float(sstep(s, 0.72, 0.62)) * float(sstep(s, 0.170, 0.205))
        ctl = [sym(0.084, 0.0185, 0.0064), sym(0.150, 0.0185, 0.0064), sym(0.172, 0.0345, 0.0060), sym(0.205, 0.0262, 0.0060),
               sym(0.30, 0.0235, 0.0056), sym(0.46, 0.0236, 0.0052), sym(0.62, 0.0290, 0.0048), sym(0.74, 0.0262, 0.0042),
               sym(0.86, 0.0165, 0.0034), sym(0.925, 0.0075, 0.0026), sym(0.955, 0.0, 0.0)]
        st = ctl[:4] + dense(ctl[3:], 24)[1:]
        w(ribbon(st, sec_fuller(0.0075, 0.40, fd)), "blade_dark", sharp=14, hem=60)
        zs = np.linspace(0.218, 0.640, 12)
        w(inlay([(z, 0.0030 * (1 - 0.6 * (i / 11.0) ** 3), 0.0056 / 2 * (1 - 0.62) + 0.0002) for i, z in enumerate(zs)]), "ember", sharp=30)
        wing = [(0.016, 0, 0.0720), (0.050, 0, 0.0650), (0.086, 0, 0.0715), (0.112, 0, 0.0935), (0.121, 0, 0.1260)]
        w(both_x(quillon(wing, [0.0112, 0.0100, 0.0090, 0.0066, 0.0012], n=8, flat=0.80)), "gilt", sharp=50)
        hook = [(0.040, 0, 0.0640), (0.066, 0, 0.0520), (0.079, 0, 0.0330), (0.073, 0, 0.0180)]
        w(both_x(quillon(hook, [0.0072, 0.0062, 0.0046, 0.0010], n=6, flat=0.80)), "gilt", sharp=50)
        w(g_prism([(-0.024, 0.060), (0.024, 0.060), (0.030, 0.074), (0.012, 0.092), (-0.012, 0.092), (-0.030, 0.074)], 0.0260), "gilt",
          bevel=0.0040, sharp=40)
        w(both_y(gxf(g_gem(0.0100, 0.0105, 8, 0.75), T(0, 0.0118, 0.0745) @ R('X', -90))), "ember", sharp=20)
        w(grip_core(-0.058, 0.058, 0.0130, n=10), "grip_black", sharp=60)
        w(grip_wrap(-0.050, 0.050, 0.0130, turns=13.0, width=0.0026, thick=0.0020, n=4, gap=1.6), "gilt", sharp=70)
        w(rings((-0.0535, 0.0, 0.0535), 0.0140, h=0.0070, n=10, bulge=1.12), "gilt", sharp=50)
        w(g_lathe([(0.0125, -0.058), (0.0190, -0.066), (0.0205, -0.076), (0.0150, -0.086), (0.0, -0.086)], n=10), "gilt", sharp=40)
        claws = [g_tube([(0.0150, 0, -0.080), (0.0165, 0, -0.094), (0.0105, 0, -0.108)], [0.0034, 0.0030, 0.0012], n=5) for _ in range(1)]
        w(radial(claws[0], 4, 45), "gilt", sharp=50)
        w(gxf(crystal(0.0125, 0.058, 6, top=0.42, bot=0.16), T(0, 0, -0.078) @ R('X', 180)), "ember", sharp=20)


# =============================================================================================== axe1 斧
@base("axe1", info=ONE, texel=0.0010)
def axe1(w, v):
    if v == 2:
        # bearded war axe: forged head with a hammer poll, langets riveted down an oval ash haft, leather grip, steel butt cap
        st = slices(edge=[(0.298, 0.110), (0.330, 0.132), (0.375, 0.143), (0.420, 0.141), (0.455, 0.130), (0.482, 0.112)],
                    back=[(0.298, 0.110), (0.318, 0.088), (0.345, 0.052), (0.372, 0.026), (0.385, -0.019), (0.440, -0.019),
                          (0.450, 0.024), (0.466, 0.066), (0.482, 0.112)],
                    thick=[(0.298, 0.003), (0.330, 0.007), (0.372, 0.013), (0.385, 0.034), (0.440, 0.034), (0.450, 0.014),
                           (0.466, 0.008), (0.482, 0.003)], n=24)
        w(ribbon(st, sec_wedge(0.016, 0.34)), "steel", sharp=16, hem=60)
        w(g_box(0.026, 0.030, 0.046, (-0.030, 0, 0.4125)), "steel", bevel=0.004, sharp=40)
        w(haft(-0.150, 0.452, 0.0152, 0.0138, n=10, seg=10, oval=0.82, bulge=[(0, 1.16), (0.07, 1.0), (0.5, 0.96), (1, 1.0)]), "wood", sharp=60)
        w(g_lathe([(0.0, 0.452), (0.0125, 0.452), (0.0105, 0.460), (0.0, 0.463)], n=10, sy=0.82), "steel", sharp=50)
        w(langets(0.285, 0.388, 0.0118, 0.0125, 0.0035), "steel", bevel=0.0012, sharp=40)
        w(both_y(rivet_row((0.300, 0.330, 0.360), 0.0148, (0, 1, 0), 0.0034)), "steel", sharp=60)
        w(grip_wrap(-0.070, 0.075, 0.0150, turns=10.5, width=0.0068, thick=0.0022, oval=0.82), "grip", sharp=70)
        w(rings((-0.078, 0.083), 0.0158, h=0.007, n=10, bulge=1.08), "steel", sharp=50)
        w(g_lathe([(0.0, -0.172), (0.012, -0.170), (0.0195, -0.160), (0.0195, -0.146), (0.0160, -0.140)], n=10, sy=0.84), "steel", sharp=40)
        w(gxf(torus(0.0085, 0.0024, 10, 5), T(0, 0, -0.178) @ R('X', 90)), "steel", sharp=60)


# =============================================================================================== mace1 錘
@base("mace1", info=ONE, texel=0.0010)
def mace1(w, v):
    if v == 2:
        # flanged mace: seven forged flanges on a steel core, banded dark haft, leather grip, ball pommel with a lanyard ring
        w(haft(-0.130, 0.335, 0.0128, 0.0122, n=10, seg=6), "darkwood", sharp=60)
        w(g_lathe([(0.0122, 0.318), (0.0190, 0.326), (0.0225, 0.345), (0.0235, 0.380), (0.0235, 0.445), (0.0190, 0.470),
                   (0.0120, 0.482), (0.0, 0.486)], n=14), "steel", sharp=40)
        fl = [(0.014, 0.334), (0.034, 0.340), (0.057, 0.372), (0.062, 0.394), (0.054, 0.420), (0.036, 0.452), (0.014, 0.470)]
        w(radial(plate(fl, 0.0068), 7), "steel", bevel=0.0018, sharp=30, hem=60)
        w(rings((0.322, 0.474), 0.0200, h=0.008, n=14, bulge=1.12), "steel", sharp=50)
        w(spike(0.0088, 0.036, 6, z0=0.482), "steel", sharp=30)
        w(rings((0.150, 0.235), 0.0128, h=0.010, n=10, bulge=1.10), "steel", sharp=50)
        w(grip_wrap(-0.062, 0.068, 0.0128, turns=9.5, width=0.0066, thick=0.0022, oval=1.0), "grip", sharp=70)
        w(rings((-0.070, 0.076), 0.0136, h=0.007, n=10, bulge=1.10), "steel", sharp=50)
        w(g_lathe([(0.0128, -0.128), (0.0170, -0.134), (0.0200, -0.148), (0.0170, -0.162), (0.0090, -0.170), (0.0, -0.171)], n=12), "steel", sharp=50)
        w(gxf(torus(0.0090, 0.0025, 10, 5), T(0, 0, -0.178) @ R('X', 90)), "steel", sharp=60)


# =============================================================================================== dagger 匕首
@base("dagger", info=ONE, texel=0.0007)
def dagger(w, v):
    if v == 2:
        # soldier's dagger: stiff ridged blade, short knobbed guard, wire-bound black grip, faceted cap
        st = dense([sym(0.058, 0.0155, 0.0050), sym(0.095, 0.0152, 0.0050), sym(0.175, 0.0125, 0.0044), sym(0.245, 0.0085, 0.0034),
                    sym(0.285, 0.0042, 0.0026), sym(0.305, 0.0, 0.0)], 20)
        w(ribbon(st, sec_ridge(0.0052, 1.45)), "blade", sharp=14, hem=60)
        w(g_box(0.064, 0.0150, 0.0120, (0, 0, 0.0520)), "steel", bevel=0.0032, sharp=40)
        w(both_x(gxf(g_sphere(0.0078, 8, 6, sy=0.92), T(0.0345, 0, 0.0520))), "steel", sharp=60)
        w(g_prism([(-0.0075, 0.058), (0.0075, 0.058), (0.0, 0.076)], 0.0150), "steel", bevel=0.0012, sharp=40)
        w(grip_core(-0.050, 0.046, 0.0118, n=10, swell=1.10), "grip_black", sharp=60)
        w(grip_wrap(-0.042, 0.040, 0.0118, turns=12.0, width=0.0022, thick=0.0018, n=4), "steel", sharp=70)
        w(rings((-0.0465, 0.0435), 0.0122, h=0.006, n=10, bulge=1.10), "steel", sharp=50)
        w(g_lathe([(0.0110, -0.050), (0.0158, -0.057), (0.0162, -0.067), (0.0100, -0.078), (0.0040, -0.082), (0.0, -0.0825)], n=8), "steel", sharp=30)


# =============================================================================================== wand 魔杖
@base("wand", info=ONE, texel=0.0007)
def wand(w, v):
    if v == 2:
        # turned dark-wood wand: bulbed handle, brass bands and wire, a long blue crystal held in a four-ribbed brass cage
        prof = [(0.0, -0.086), (0.0070, -0.084), (0.0108, -0.070), (0.0124, -0.035), (0.0120, 0.010), (0.0100, 0.045),
                (0.0078, 0.066), (0.0068, 0.120), (0.0062, 0.180), (0.0066, 0.222)]
        w(lathe_w(prof, n=12), "darkwood", sharp=60)
        w(gjoin(rings((-0.073,), 0.0108, h=0.007, n=12, bulge=1.18), rings((0.058,), 0.0088, h=0.008, n=12, bulge=1.22)), "brass", sharp=50)
        w(helix(0.0076, 0.072, 0.206, 4.5, 0.0020, 0.0018, n=4, spt=12, r1=0.0066), "brass", sharp=70)
        w(g_lathe([(0.0066, 0.212), (0.0100, 0.218), (0.0135, 0.228), (0.0120, 0.238), (0.0070, 0.242), (0.0, 0.242)], n=12), "brass", sharp=40)
        cz0, ch, cr = 0.236, 0.104, 0.0168
        add_tones(w, sector_tones(crystal(cr, ch, 6, top=0.36, bot=0.30, girdle=0.45), 6, pattern=(1, 2), z_split=ch * 0.30), "crystal_blue",
                  M=T(0, 0, cz0) @ R('Z', 30), recalc=False)
        rib = quillon([(0.0105, 0, 0.232), (0.0225, 0, 0.254), (0.0248, 0, 0.288), (0.0185, 0, 0.322), (0.0050, 0, 0.346)],
                      [0.0030, 0.0028, 0.0026, 0.0024, 0.0022], n=6, flat=0.7)
        w(radial(rib, 4, 45), "brass", sharp=50)
        w(gxf(torus(0.0246, 0.0020, 16, 5), T(0, 0, 0.288)), "brass", sharp=60)
        w(g_lathe([(0.0056, 0.342), (0.0082, 0.349), (0.0056, 0.356), (0.0030, 0.362), (0.0, 0.378)], n=8), "brass", sharp=40)
        w(g_sphere(0.0088, 10, 6), "brass", M=T(0, 0, -0.092), sharp=60)


# =============================================================================================== scythe1 鐮刃
@base("scythe1", info=ONE, texel=0.0010)
def scythe1(w, v):
    if v == 2:
        # war sickle: forged crescent on a steel socket, spine spur, dark haft with a leather grip and a ringed butt cap
        P = smooth_path([(0.0, 0.300), (0.004, 0.362), (0.030, 0.428), (0.086, 0.470), (0.156, 0.472), (0.216, 0.432),
                         (0.252, 0.362), (0.264, 0.282)], 30)
        st = along(P, [(0.0, -0.013, 0.016, 0.0075), (0.10, -0.016, 0.022, 0.0072), (0.30, -0.019, 0.027, 0.0066),
                       (0.55, -0.017, 0.026, 0.0058), (0.78, -0.012, 0.019, 0.0046), (0.93, -0.006, 0.009, 0.0034), (1.0, 0.0, 0.0, 0.0)])
        st = dense(st, 26)
        w(ribbon(st, sec_single_fuller(0.012, 1.0, 0.45), path=P), "blade", sharp=14, hem=60)
        w(g_prism([(0.016, 0.452), (0.040, 0.474), (0.012, 0.520)], 0.0060), "blade", bevel=0.0012, sharp=30, hem=60)
        w(g_lathe([(0.0130, 0.238), (0.0175, 0.246), (0.0165, 0.290), (0.0140, 0.312), (0.0, 0.316)], n=10), "steel", sharp=40)
        w(rings((0.250, 0.296), 0.0172, h=0.006, n=10, bulge=1.08), "steel", sharp=50)
        w(haft(-0.110, 0.250, 0.0136, 0.0128, n=10, seg=6, oval=0.86, bulge=[(0, 1.14), (0.1, 1.0), (1, 1.0)]), "darkwood", sharp=60)
        w(grip_wrap(-0.062, 0.066, 0.0134, turns=9.5, width=0.0066, thick=0.0022, oval=0.86), "grip", sharp=70)
        w(rings((-0.070, 0.074), 0.0142, h=0.007, n=10, bulge=1.08), "steel", sharp=50)
        w(g_lathe([(0.0, -0.128), (0.0120, -0.126), (0.0178, -0.118), (0.0178, -0.106), (0.0150, -0.100)], n=10, sy=0.88), "steel", sharp=40)
        w(gxf(torus(0.0085, 0.0024, 10, 5), T(0, 0, -0.134) @ R('X', 90)), "steel", sharp=60)


# =============================================================================================== flail 連枷
@base("flail", info=ONE, texel=0.0010)
def flail(w, v):
    if v == 2:
        # soldier's flail: steel-capped ash handle, swivel ring, chain and a spiked iron ball (posed hanging over the cap)
        w(haft(-0.120, 0.262, 0.0138, 0.0128, n=10, seg=6, bulge=[(0, 1.14), (0.1, 1.0), (1, 1.0)]), "wood", sharp=60)
        w(grip_wrap(-0.064, 0.068, 0.0136, turns=9.5, width=0.0066, thick=0.0022, oval=1.0), "grip", sharp=70)
        w(rings((-0.072, 0.076, 0.170), 0.0142, h=0.007, n=10, bulge=1.08), "steel", sharp=50)
        w(g_lathe([(0.0, -0.138), (0.0120, -0.136), (0.0180, -0.128), (0.0180, -0.116), (0.0150, -0.110)], n=10), "steel", sharp=40)
        w(g_lathe([(0.0134, 0.236), (0.0178, 0.244), (0.0170, 0.276), (0.0120, 0.292), (0.0050, 0.298), (0.0, 0.299)], n=10), "steel", sharp=40)
        w(gxf(torus(0.0110, 0.0032, 12, 5), T(0, 0, 0.3065) @ R('X', 90)), "steel", sharp=60)
        cp = smooth_path([(0.0, 0, 0.314), (0.020, 0, 0.352), (0.062, 0, 0.374), (0.110, 0, 0.360), (0.140, 0, 0.318), (0.150, 0, 0.268)], 16)
        w(g_chain(cp, link=0.027, wire=0.0034, width=0.66, seg=6, n=4, up=(0, 1, 0)), "iron", sharp=60)
        bc = (0.152, 0.0, 0.214)
        ball, sp = spiked_ball(0.036, 14, 0.0095, 0.028, bc, skip=lambda d: d[2] > 0.82)
        w(ball, "iron", sharp=60)
        w(sp, "iron", sharp=30, hem=60)
        w(gxf(torus(0.0085, 0.0030, 10, 5), T(bc[0], 0, bc[2] + 0.041) @ R('X', 90)), "iron", sharp=60)


# =============================================================================================== fist 拳刃
@base("fist", info=dict(mount="hand_r", grip="r", hands=1, hint="one per hand; symmetric about Y=0, the same file fits hand_l"),
      texel=0.0008, sheet=dict(icon_roll=-45.0))
def fist(w, v):
    if v == 2:
        # punch dagger: leather-wrapped cross handle between two side bars, broad ridged thrusting blade over the knuckles
        w(tube_w([(0, 0, -0.056), (0, 0, 0.056)], 0.0112, 10), "grip", sharp=60)
        w(grip_wrap(-0.044, 0.044, 0.0112, turns=7.5, width=0.0060, thick=0.0020, oval=1.0), "grip", sharp=70)
        bar = g_prism([(-0.150, 0.0052), (-0.158, 0.0000), (-0.150, -0.0052), (0.052, -0.0060), (0.052, 0.0060)], 0.0080)
        w(gjoin(gxf(bar, T(0, 0, 0.0530)), gxf(bar, T(0, 0, -0.0530))), "steel", bevel=0.0016, sharp=40)
        w(gjoin(*[gxf(stud(0.0052, n=6), frame((0.0, sy_ * 0.0040, sz_ * 0.0530), (0, sy_, 0))) for sy_ in (1, -1) for sz_ in (1, -1)]), "steel", sharp=60)
        w(g_box(0.018, 0.0150, 0.1180, (0.0520, 0, 0)), "steel", bevel=0.0030, sharp=40)
        P = [(0.058, 0.0), (0.320, 0.0)]
        st = dense([(0.0, -0.0470, 0.0470, 0.0070), (0.03, -0.0440, 0.0440, 0.0068), (0.12, -0.0300, 0.0300, 0.0056),
                    (0.20, -0.0160, 0.0160, 0.0042), (0.245, -0.0065, 0.0065, 0.0030), (0.262, 0.0, 0.0, 0.0)], 18)
        w(ribbon(st, sec_ridge(0.0075, 1.6), path=P), "blade", sharp=14, hem=60)
        w(g_prism([(0.058, 0.020), (0.105, 0.0), (0.058, -0.020)], 0.0115), "steel", bevel=0.0015, sharp=40)


# =============================================================================================== sceptre 權杖
@base("sceptre", info=ONE, texel=0.0009)
def sceptre(w, v):
    if v == 2:
        # bronze sceptre: knopped rod, dark leather grip between collars, a violet orb in a four-pronged cage, star finial
        prof = [(0.0, -0.150), (0.0060, -0.148), (0.0150, -0.136), (0.0150, -0.124), (0.0100, -0.116), (0.0105, -0.070),
                (0.0105, 0.070), (0.0150, 0.078), (0.0150, 0.088), (0.0090, 0.098), (0.0082, 0.190), (0.0130, 0.198), (0.0130, 0.208),
                (0.0080, 0.216), (0.0074, 0.318), (0.0150, 0.328), (0.0170, 0.340), (0.0120, 0.350), (0.0, 0.352)]
        w(g_lathe(prof, n=12), "bronze", sharp=40)
        w(tube_w([(0, 0, -0.068), (0, 0, 0.068)], 0.0116, 10), "grip_black", sharp=60)
        w(grip_wrap(-0.060, 0.060, 0.0116, turns=9.0, width=0.0060, thick=0.0020, oval=1.0), "grip_black", sharp=70)
        oz = 0.404
        pr = quillon([(0.0100, 0, 0.344), (0.0300, 0, 0.368), (0.0345, 0, oz), (0.0240, 0, oz + 0.034), (0.0060, 0, oz + 0.048)],
                     [0.0046, 0.0044, 0.0040, 0.0036, 0.0030], n=6, flat=1.0)
        w(radial(pr, 4, 45), "bronze", sharp=50)
        w(gxf(g_sphere(0.0285, 16, 10), T(0, 0, oz)), "crystal_violet", sharp=60)
        w(g_lathe([(0.0070, oz + 0.044), (0.0100, oz + 0.050), (0.0060, oz + 0.058), (0.0, oz + 0.060)], n=10), "bronze", sharp=50)
        star = plate([(x, z + oz + 0.086) for x, z in star4(0.030, 0.26)], 0.0050)
        w(gjoin(star, gxf(star, R('Z', 90))), "bronze", bevel=0.0012, sharp=30)
