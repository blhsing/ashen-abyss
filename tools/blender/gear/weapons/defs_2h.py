"""Two-hand weapons: sword2, axe2, mace2, staff, polearm, scythe2, bow, crossbow.
Weapon-local space: +Z up the weapon, +X edge / knuckles, +Y palm normal; origin = centre of the main (right) hand's
grip; grip2 = where the other hand holds. v1 crude, v2 forged, v3 ornate / glowing."""
from gearparts import *
from defs import base

TWO = dict(mount="hand_r", grip="both", hands=2)
STAFF_HINT = "long weapon: the hero scripts hold staves with charlib grip(tilt=46) @ R('X', 30) (kit_cst.staff_grip) so they stand upright"


# =============================================================================================== sword2 巨劍
@base("sword2", info=dict(TWO, grip2=(0.0, 0.0, -0.185)), texel=0.0012)
def sword2(w, v):
    if v == 2:
        # two-hand war sword: long leather-bound ricasso ending in hooked parrying lugs, fullered blade, ringed cross, long grip with a riser
        fd = lambda s: 0.55 * float(sstep(s, 1.08, 0.96)) * float(sstep(s, 0.470, 0.510))
        head = [sym(0.100, 0.0225, 0.0084), sym(0.392, 0.0225, 0.0084), sym(0.402, 0.0300, 0.0082), sym(0.418, 0.0580, 0.0078),
                sym(0.440, 0.0960, 0.0068), sym(0.449, 0.0640, 0.0072), sym(0.462, 0.0470, 0.0074), sym(0.480, 0.0425, 0.0074)]
        body = dense([sym(0.480, 0.0425, 0.0074), sym(0.70, 0.0410, 0.0068), sym(0.97, 0.0385, 0.0060), sym(1.18, 0.0330, 0.0050),
                      sym(1.30, 0.0235, 0.0040), sym(1.365, 0.0115, 0.0032), sym(1.402, 0.0, 0.0)], 30)
        w(ribbon(head + body[1:], sec_fuller(0.0085, 0.40, fd)), "blade", sharp=14, hem=60)
        w(g_lathe([(0.0250, 0.112), (0.0264, 0.205), (0.0250, 0.298)], n=12, sy=0.32), "grip", sharp=60)
        w(gjoin(*[g_lathe([(0.0262, z - 0.005), (0.0282, z), (0.0262, z + 0.005)], n=12, sy=0.34) for z in (0.116, 0.207, 0.296)]), "steel", sharp=50)
        q = [(0.016, 0, 0.0860), (0.070, 0, 0.0850), (0.135, 0, 0.0880), (0.190, 0, 0.0970), (0.214, 0, 0.1090)]
        w(both_x(quillon(q, [0.0135, 0.0118, 0.0100, 0.0088, 0.0078], n=8, flat=0.86)), "steel", sharp=50)
        w(both_x(gxf(g_sphere(0.0118, 8, 6), T(0.218, 0, 0.1118))), "steel", sharp=60)
        w(g_box(0.050, 0.0290, 0.0300, (0, 0, 0.0860)), "steel", bevel=0.0055, sharp=40)
        ring = quillon([(-0.034, 0.012, 0.086), (-0.030, 0.040, 0.090), (0.0, 0.052, 0.092), (0.030, 0.040, 0.090), (0.034, 0.012, 0.086)],
                       [0.0058, 0.0054, 0.0054, 0.0054, 0.0058], n=6, flat=1.0)
        w(both_y(ring), "steel", sharp=50)
        w(grip_core(-0.278, 0.072, 0.0160, n=10, swell=1.02, taper=0.98, oval=0.88), "grip", sharp=60)
        w(grip_wrap(-0.266, -0.108, 0.0160, turns=11.0, width=0.0068, thick=0.0022, oval=0.88), "grip", sharp=70)
        w(grip_wrap(-0.092, 0.062, 0.0160, turns=11.0, width=0.0068, thick=0.0022, oval=0.88, hand=-1), "grip", sharp=70)
        w(g_lathe([(0.0162, -0.110), (0.0210, -0.100), (0.0162, -0.090)], n=12, sy=0.88), "steel", sharp=50)
        w(rings((-0.2735, 0.0685), 0.0170, h=0.008, n=12, bulge=1.10), "steel", sharp=50)
        w(g_lathe([(0.0140, -0.278), (0.0235, -0.292), (0.0305, -0.318), (0.0270, -0.342), (0.0140, -0.360), (0.0060, -0.367), (0.0, -0.368)],
                  n=8), "steel", sharp=30)


# =============================================================================================== axe2 巨斧
@base("axe2", info=dict(TWO, grip2=(0.0, 0.0, -0.36)), texel=0.0012)
def axe2(w, v):
    if v == 2:
        # double-bitted battle axe: two crescent bits forged round the eye, top spike, long langets, two leather grips, butt spike
        st = slices(edge=[(0.648, 0.150), (0.690, 0.176), (0.770, 0.190), (0.850, 0.176), (0.892, 0.150)],
                    back=[(0.648, 0.150), (0.672, 0.108), (0.712, 0.056), (0.738, 0.026), (0.742, 0.0), (0.798, 0.0), (0.802, 0.026),
                          (0.828, 0.056), (0.868, 0.108), (0.892, 0.150)],
                    thick=[(0.648, 0.003), (0.690, 0.008), (0.738, 0.014), (0.742, 0.042), (0.798, 0.042), (0.802, 0.014),
                           (0.850, 0.008), (0.892, 0.003)], n=26)
        bit = gxf(ribbon(st, sec_wedge(0.020, 0.34)), T(0, 0, 0.770) @ S(1.20, 1.0, 1.16) @ T(0, 0, -0.770))
        w(both_x(bit), "steel", sharp=16, hem=60)
        w(haft(-0.500, 0.800, 0.0178, 0.0158, n=10, seg=14, oval=0.86, bulge=[(0, 1.10), (0.05, 1.0), (1, 1.0)]), "wood", sharp=60)
        w(g_lathe([(0.0200, 0.796), (0.0230, 0.806), (0.0170, 0.826), (0.0, 0.830)], n=10), "steel", sharp=40)
        w(spike(0.0120, 0.105, 6, z0=0.824), "steel", sharp=30, hem=60)
        w(langets(0.560, 0.745, 0.0138, 0.0140, 0.0038), "steel", bevel=0.0012, sharp=40)
        w(both_y(rivet_row((0.580, 0.625, 0.670, 0.715), 0.0170, (0, 1, 0), 0.0038)), "steel", sharp=60)
        w(grip_wrap(-0.078, 0.085, 0.0170, turns=10.5, width=0.0074, thick=0.0024, oval=0.86), "grip", sharp=70)
        w(grip_wrap(-0.430, -0.290, 0.0176, turns=9.0, width=0.0074, thick=0.0024, oval=0.86, hand=-1), "grip", sharp=70)
        w(rings((-0.438, -0.282, -0.086, 0.093), 0.0182, h=0.008, n=10, bulge=1.08), "steel", sharp=50)
        w(g_lathe([(0.0, -0.560), (0.0060, -0.540), (0.0200, -0.514), (0.0200, -0.494), (0.0180, -0.488)], n=8), "steel", sharp=40)


# =============================================================================================== mace2 巨錘
@base("mace2", info=dict(TWO, grip2=(0.0, 0.0, -0.34)), texel=0.0012)
def mace2(w, v):
    if v == 2:
        # war maul: eight-sided steel hammer head, crow's-beak back spike, top spike, langets on a dark haft, butt cap
        hz = 0.790
        K = T(0, 0, hz) @ S(1.25) @ T(0, 0, -hz)
        w(gxf(g_box(0.066, 0.062, 0.092, (0, 0, hz)), K), "steel", bevel=0.008, sharp=40)
        w(gxf(g_lathe([(0.028, 0.024), (0.034, 0.040), (0.041, 0.072), (0.041, 0.112), (0.036, 0.122), (0.0, 0.122)], n=8),
              K @ T(0, 0, hz) @ R('Y', 90) @ R('Z', 22.5)), "steel", sharp=30)
        w(gxf(g_lathe([(0.0440, 0.060), (0.0465, 0.066), (0.0440, 0.072)], n=8), K @ T(0, 0, hz) @ R('Y', 90) @ R('Z', 22.5)), "steel", sharp=40)
        beak = quillon([(-0.026, 0, hz + 0.004), (-0.070, 0, hz + 0.004), (-0.116, 0, hz - 0.012), (-0.150, 0, hz - 0.046)],
                       [0.0235, 0.0180, 0.0115, 0.0020], n=6, flat=0.72)
        w(gxf(beak, K), "steel", sharp=30, hem=60)
        w(spike(0.0135, 0.100, 6, z0=hz + 0.054), "steel", sharp=30, hem=60)
        w(haft(-0.480, hz + 0.050, 0.0178, 0.0160, n=10, seg=14, bulge=[(0, 1.10), (0.05, 1.0), (1, 1.0)]), "darkwood", sharp=60)
        w(langets(0.560, hz - 0.044, 0.0142, 0.0140, 0.0038, sides=((0, 1), (0, -1), (1, 0), (-1, 0))), "steel", bevel=0.0012, sharp=40)
        w(gjoin(both_y(rivet_row((0.585, 0.640, 0.695), 0.0176, (0, 1, 0), 0.0038)),
                both_x(rivet_row((0.585, 0.640, 0.695), 0.0176, (1, 0, 0), 0.0038))), "steel", sharp=60)
        w(grip_wrap(-0.078, 0.085, 0.0172, turns=10.5, width=0.0074, thick=0.0024, oval=1.0), "grip", sharp=70)
        w(grip_wrap(-0.410, -0.270, 0.0176, turns=9.0, width=0.0074, thick=0.0024, oval=1.0, hand=-1), "grip", sharp=70)
        w(rings((-0.418, -0.262, -0.086, 0.093, 0.548), 0.0184, h=0.008, n=10, bulge=1.08), "steel", sharp=50)
        w(g_lathe([(0.0, -0.520), (0.0130, -0.516), (0.0215, -0.500), (0.0215, -0.478), (0.0180, -0.470)], n=10), "steel", sharp=40)


# =============================================================================================== staff 法杖
@base("staff", info=dict(TWO, grip2=(0.0, 0.0, 0.30), hint=STAFF_HINT), texel=0.0012)
def staff(w, v):
    if v == 2:
        # journeyman's staff: iron-shod dark shaft with bronze bands, leather grip, bronze crescent cradle holding a blue crystal
        w(haft(-0.860, 0.800, 0.0168, 0.0152, n=10, seg=14), "darkwood", sharp=60)
        w(g_lathe([(0.0, -0.900), (0.0090, -0.886), (0.0205, -0.862), (0.0205, -0.800), (0.0178, -0.792)], n=10), "iron", sharp=40)
        w(rings((-0.520, 0.420, 0.640), 0.0166, h=0.012, n=10, bulge=1.12), "bronze", sharp=50)
        w(grip_wrap(-0.085, 0.105, 0.0162, turns=12.0, width=0.0074, thick=0.0024, oval=1.0), "grip", sharp=70)
        w(rings((-0.094, 0.114), 0.0170, h=0.008, n=10, bulge=1.08), "bronze", sharp=50)
        w(g_lathe([(0.0152, 0.770), (0.0215, 0.782), (0.0190, 0.806), (0.0265, 0.826), (0.0290, 0.848), (0.0215, 0.862), (0.0120, 0.866)],
                  n=12), "bronze", sharp=40)
        horn = [(0.020, 0, 0.840), (0.052, 0, 0.862), (0.088, 0, 0.912), (0.100, 0, 0.978), (0.084, 0, 1.036), (0.058, 0, 1.070)]
        w(both_x(quillon(horn, [0.0125, 0.0120, 0.0105, 0.0085, 0.0058, 0.0016], n=8, flat=0.70)), "bronze", sharp=50)
        w(gxf(crystal(0.030, 0.165, 6, top=0.36, bot=0.20), T(0, 0, 0.856)), "crystal_blue", sharp=20)
        pr = tube([(0.014, 0, 0.862), (0.030, 0, 0.882), (0.031, 0, 0.912), (0.024, 0, 0.930)], [0.0036, 0.0034, 0.0028, 0.0012], n=5)
        w(radial(pr, 2, 90), "bronze", sharp=50)


# =============================================================================================== polearm 長柄刀
@base("polearm", info=dict(TWO, grip2=(0.0, 0.0, -0.45), hint=STAFF_HINT), texel=0.0013)
def polearm(w, v):
    if v == 2:
        # glaive: broad single-edged blade with a parrying hook on a long socket, langets, ash shaft, two grips, butt spike
        st = dense([(0.725, -0.0160, 0.0170, 0.0085), (0.775, -0.0180, 0.0340, 0.0085), (0.850, -0.0200, 0.0620, 0.0080),
                    (0.980, -0.0215, 0.0800, 0.0074), (1.120, -0.0190, 0.0780, 0.0064), (1.240, -0.0100, 0.0640, 0.0054),
                    (1.320, 0.0050, 0.0440, 0.0044), (1.375, 0.0240, 0.0240, 0.0)], 32)
        w(ribbon(st, sec_single_fuller(0.016, 1.0, 0.42)), "blade", sharp=14, hem=60)
        w(g_prism([(-0.018, 0.850), (-0.066, 0.882), (-0.084, 0.930), (-0.058, 0.908), (-0.020, 0.930)], 0.0074), "blade", bevel=0.0014,
          sharp=30, hem=60)
        w(g_lathe([(0.0158, 0.630), (0.0200, 0.640), (0.0190, 0.700), (0.0165, 0.730), (0.0100, 0.742), (0.0, 0.744)], n=10), "steel", sharp=40)
        w(rings((0.642, 0.700), 0.0200, h=0.007, n=10, bulge=1.08), "steel", sharp=50)
        w(haft(-0.800, 0.640, 0.0168, 0.0158, n=10, seg=16, oval=0.90), "wood", sharp=60)
        w(langets(0.470, 0.632, 0.0142, 0.0130, 0.0036), "steel", bevel=0.0012, sharp=40)
        w(both_y(rivet_row((0.490, 0.530, 0.570, 0.610), 0.0166, (0, 1, 0), 0.0036)), "steel", sharp=60)
        w(grip_wrap(-0.085, 0.105, 0.0164, turns=12.0, width=0.0074, thick=0.0024, oval=0.90), "grip", sharp=70)
        w(grip_wrap(-0.530, -0.370, 0.0166, turns=10.0, width=0.0074, thick=0.0024, oval=0.90, hand=-1), "grip", sharp=70)
        w(rings((-0.538, -0.362, -0.093, 0.113), 0.0172, h=0.008, n=10, bulge=1.08), "steel", sharp=50)
        w(g_lathe([(0.0, -0.885), (0.0070, -0.850), (0.0195, -0.812), (0.0195, -0.780), (0.0170, -0.772)], n=8), "steel", sharp=40)


# =============================================================================================== scythe2 巨鐮
@base("scythe2", info=dict(TWO, grip2=(0.0, 0.0, -0.42), hint=STAFF_HINT), texel=0.0013)
def scythe2(w, v):
    if v == 2:
        # war scythe: long reinforced blade on a forged socket with a back spike, dark snath with two grips, steel shoe
        P = smooth_path([(0.004, 0.905), (0.090, 0.946), (0.230, 0.950), (0.380, 0.908), (0.510, 0.826), (0.600, 0.716), (0.640, 0.620)], 34)
        st = along(P, [(0.0, -0.030, 0.040, 0.0085), (0.12, -0.030, 0.044, 0.0080), (0.40, -0.024, 0.040, 0.0068),
                       (0.70, -0.016, 0.030, 0.0054), (0.90, -0.008, 0.016, 0.0040), (1.0, 0.0, 0.0, 0.0)])
        w(ribbon(dense(st, 30), sec_single_fuller(0.016, 1.0, 0.40), path=P), "blade", sharp=14, hem=60)
        w(g_lathe([(0.0160, 0.800), (0.0215, 0.812), (0.0215, 0.900), (0.0190, 0.936), (0.0120, 0.956), (0.0, 0.960)], n=10), "steel", sharp=40)
        w(g_box(0.060, 0.0200, 0.050, (0.020, 0, 0.920)), "steel", bevel=0.0040, sharp=40)
        w(quillon([(-0.018, 0, 0.918), (-0.058, 0, 0.930), (-0.098, 0, 0.962)], [0.0125, 0.0090, 0.0015], n=6, flat=0.7), "steel", sharp=30, hem=60)
        w(rings((0.812, 0.886), 0.0218, h=0.007, n=10, bulge=1.08), "steel", sharp=50)
        w(haft(-0.860, 0.812, 0.0170, 0.0158, n=10, seg=16, wob=0.010, seed=4), "darkwood", sharp=60)
        w(grip_wrap(-0.085, 0.105, 0.0166, turns=12.0, width=0.0074, thick=0.0024, oval=1.0), "grip", sharp=70)
        w(grip_wrap(-0.500, -0.340, 0.0168, turns=10.0, width=0.0074, thick=0.0024, oval=1.0, hand=-1), "grip", sharp=70)
        w(rings((-0.508, -0.332, -0.093, 0.113), 0.0176, h=0.008, n=10, bulge=1.08), "steel", sharp=50)
        w(g_lathe([(0.0, -0.905), (0.0110, -0.900), (0.0200, -0.880), (0.0200, -0.842), (0.0175, -0.834)], n=10), "steel", sharp=40)


# =============================================================================================== bow 長弓
def limb(pts, r0, r1, n=8, width=1.0, thick=0.52, seg=22):
    """bow limb through (x, z) points in the XZ plane; flat section (wide along Y)"""
    P = smooth_path([(x, 0.0, z) for x, z in pts], seg)
    rad = np.linspace(r0, r1, len(P))
    return tube_w(P, list(rad), n, up=(0, 1, 0), sx=width, sy=thick)


@base("bow", info=dict(mount="hand_l", grip="both", hands=2, grip2=(-0.160, 0.0, 0.0)), texel=0.0011)
def bow(w, v):
    if v == 2:
        # hunter's recurve: laminated limbs with horn nocks, sinew bindings, leather-wrapped riser with an arrow shelf, waxed string
        up = [(0.012, 0.062), (0.008, 0.190), (-0.022, 0.370), (-0.078, 0.555), (-0.136, 0.690), (-0.165, 0.748), (-0.158, 0.790)]
        for s in (1, -1):
            pts = [(x, s * z) for x, z in up]
            w(limb(pts, 0.0230, 0.0125), "palewood", sharp=60)
            w(gxf(g_tooth(0.048, 0.0100, 0.014, 6), T(pts[-1][0] + 0.001, 0, pts[-1][1] - s * 0.004) @ (R('Y', 0) if s > 0 else R('X', 180))),
              "horn", sharp=50)
            for k, (zz, rr) in enumerate(((0.095, 0.0236), (0.300, 0.0206), (0.640, 0.0150))):
                xx = float(np.interp(zz, [p[1] for p in up], [p[0] for p in up]))
                w(gxf(g_lathe([(rr, -0.012), (rr * 1.10, 0.0), (rr, 0.012)], n=10, sy=0.56), T(xx, 0, s * zz) @ R('Y', -8 * s * k) @ R('Z', 90)),
                  "cord_dark", sharp=60)
        w(tube_w([(0.012, 0, -0.070), (0.016, 0, -0.030), (0.016, 0, 0.030), (0.012, 0, 0.070)], [0.0185, 0.0200, 0.0200, 0.0185], 10,
                 up=(0, 1, 0), sx=0.92, sy=0.86), "palewood", sharp=60)
        w(gxf(grip_wrap(-0.058, 0.058, 0.0192, turns=9.0, width=0.0068, thick=0.0022, oval=0.92), T(0.015, 0, 0) @ R('Z', 90)), "grip", sharp=70)
        w(g_box(0.014, 0.012, 0.010, (0.024, 0.0, 0.064)), "horn", bevel=0.002, sharp=40)
        t0, t1 = (up[-2][0] + 0.004, 0.0, up[-2][1] + 0.016), (up[-2][0] + 0.004, 0.0, -up[-2][1] - 0.016)
        w(tube([t1, (t1[0], 0, -0.03), (t0[0], 0, 0.03), t0], 0.0021, n=5), "cord_dark", sharp=80)
        w(tube([(t0[0], 0, -0.035), (t0[0], 0, 0.035)], 0.0030, n=6), "cord_red", sharp=80)


# =============================================================================================== crossbow 弩
def prod_path(span, sweep, x0, z, n=13, recurve=0.0):
    return [(x0 - sweep * abs(t) ** 1.7 + recurve * max(0.0, abs(t) - 0.72) ** 2 * 12.0, span * t, z) for t in np.linspace(-1, 1, n)]


@base("crossbow", info=dict(TWO, grip2=(0.270, 0.0, 0.040), hint="aims along +X (knuckles); prod spans +-Y; handle through the fist along Z"),
      texel=0.0011, sheet=dict(pre_rot=(-90, 0, 0), icon_roll=-45.0))
def crossbow(w, v):
    if v == 2:
        # soldier's crossbow: dark tiller with a pistol handle and tickler, steel prod lashed in, stirrup, brass lock, loaded bolt
        zt = 0.098
        til = g_prism([(-0.300, 0.040), (-0.300, 0.104), (-0.220, zt), (0.440, zt - 0.002), (0.440, 0.058), (0.120, 0.052), (-0.060, 0.052),
                       (-0.200, 0.036)], 0.0380)
        w(til, "darkbox", bevel=0.0050, sharp=40)
        w(tube_w([(0.006, 0, 0.056), (0.0, 0, 0.0), (-0.012, 0, -0.056), (-0.018, 0, -0.078)], [0.0150, 0.0160, 0.0165, 0.0185], 10,
                 up=(0, 1, 0), sx=0.86, sy=1.0), "darkwood", sharp=60)
        w(gxf(grip_wrap(-0.048, 0.040, 0.0160, turns=7.0, width=0.0066, thick=0.0022, oval=0.86), T(-0.004, 0, 0) @ R('Y', -8)), "grip", sharp=70)
        w(g_lathe([(0.0, -0.092), (0.0150, -0.088), (0.0195, -0.078), (0.0170, -0.070)], n=10), "brass", M=T(-0.019, 0, 0) @ R('Y', -8), sharp=40)
        w(tube([(0.030, 0, 0.050), (0.060, 0, 0.030), (0.118, 0, 0.022), (0.140, 0, 0.026)], [0.0048, 0.0042, 0.0036, 0.0030], n=5), "steel", sharp=50)
        pp = prod_path(0.335, 0.105, 0.418, 0.078, 15, recurve=0.03)
        rad = [0.0125 - 0.0058 * abs(t) for t in np.linspace(-1, 1, 15)]
        w(g_tube(pp, rad, n=6, up=(0, 0, 1), sx=1.55, sy=0.72), "steel", sharp=50)
        w(g_box(0.046, 0.044, 0.050, (0.412, 0, 0.078)), "steel", bevel=0.0050, sharp=40)
        w(gjoin(*[gxf(torus(0.027, 0.0045, 10, 5, sx=1.0, sy=0.85), T(0.400, sy_ * 0.030, 0.078) @ R('X', 90) @ R('Z', 28 * sy_)) for sy_ in (1, -1)]),
          "rope", sharp=70)
        w(gxf(torus(0.046, 0.0055, 14, 6, sx=1.0, sy=0.85), T(0.480, 0, 0.076)), "steel", sharp=60)
        w(g_box(0.060, 0.042, 0.022, (0.090, 0, zt + 0.004)), "brass", bevel=0.0030, sharp=40)
        w(gxf(g_lathe([(0.0, -0.014), (0.0130, -0.014), (0.0130, 0.014), (0.0, 0.014)], n=10), T(0.090, 0, zt + 0.010) @ R('X', 90)), "steel", sharp=40)
        w(tube([pp[0], (0.094, 0, zt + 0.014), pp[-1]], 0.0026, n=5), "cord_dark", sharp=80)
        w(gxf(g_lathe([(0.0, 0.0), (0.0052, 0.0), (0.0052, 0.330), (0.0, 0.330)], n=6), T(0.100, 0, zt + 0.012) @ R('Y', 90)), "wood", sharp=60)
        w(gxf(g_lathe([(0.0, 0.0), (0.0115, 0.008), (0.0050, 0.030), (0.0, 0.062)], n=4), T(0.428, 0, zt + 0.012) @ R('Y', 90)), "steel", sharp=30, hem=60)
        w(gjoin(*[gxf(g_prism([(0.0, 0.0), (0.060, 0.0), (0.050, 0.016), (0.008, 0.020)], 0.0016), T(0.104, 0, zt + 0.014) @ R('X', a)) for a in (35, 145)]),
          "feather_dark", sharp=40)
        w(g_box(0.300, 0.0400, 0.0040, (0.200, 0, zt + 0.001)), "brass", bevel=0.0010, sharp=40)
        w(g_box(0.012, 0.0420, 0.0700, (-0.304, 0, 0.072)), "brass", bevel=0.0030, sharp=40)
