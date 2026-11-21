"""v3 — ornate / glowing variants: black steel and gilt, ebony, bone; one emissive accent per base.
(sword1 v3 lives in defs_1h.) Accents: melee = ember, shadow blades = void, reapers = soul, star casters = star,
lamp gear = flame / holy, hunter = teal, wild = moss, pact = pact."""
from gearparts import *
from defs import variant
from defs_2h import limb, prod_path
from defs_off import (S_BACK, S_OUT, shield_parts, face_rivets, book_body, book_bail, book_corners, BOOK_X0, lantern_body,
                      lantern_yoke, HANG, totem_face)


def gem_pommel(w, z, metal="gilt", glow="ember", r=0.0125, h=0.058, claws=4, cup_r=0.0205):
    """cup + claws + a glowing crystal pointing down at height z (top of the cup)"""
    w(g_lathe([(cup_r * 0.61, z), (cup_r * 0.93, z - 0.008), (cup_r, z - 0.018), (cup_r * 0.73, z - 0.028), (0.0, z - 0.028)], n=10), metal, sharp=40)
    claw = g_tube([(cup_r * 0.73, 0, z - 0.022), (cup_r * 0.80, 0, z - 0.036), (cup_r * 0.51, 0, z - 0.050)], [0.0034, 0.0030, 0.0012], n=5)
    w(radial(claw, claws, 45), metal, sharp=50)
    w(gxf(crystal(r, h, 6, top=0.42, bot=0.16), T(0, 0, z - 0.020) @ R('X', 180)), glow, sharp=20)


# =============================================================================================== 1H
@variant("axe1", 3)
def axe1_v3(w):
    # ember moon axe: horned crescent of black steel with a burning glyph, gilt eye-band, rear fluke, spear tip, banded ebony haft
    st = slices(edge=[(0.262, 0.118), (0.300, 0.150), (0.360, 0.172), (0.420, 0.170), (0.480, 0.146), (0.520, 0.110)],
                back=[(0.262, 0.118), (0.286, 0.094), (0.318, 0.066), (0.348, 0.040), (0.366, -0.020), (0.424, -0.020), (0.440, 0.036),
                      (0.468, 0.062), (0.498, 0.088), (0.520, 0.110)],
                thick=[(0.262, 0.003), (0.300, 0.007), (0.348, 0.013), (0.366, 0.036), (0.424, 0.036), (0.440, 0.013), (0.480, 0.007),
                       (0.520, 0.003)], n=30)
    w(ribbon(st, sec_wedge(0.018, 0.34)), "blade_dark", sharp=16, hem=60)
    arc = [(0.108, 0.300), (0.130, 0.345), (0.138, 0.392), (0.132, 0.440), (0.112, 0.484)]
    wy = wedge_y(st, 0.018, 0.34)
    w(glow_line(smooth_path(arc, 11).tolist(), wy, r=0.0030), "ember", sharp=40)
    w(glow_line([(0.060, 0.395), (0.080, 0.395), (0.100, 0.395)], wy, r=0.0026, taper=False), "ember", sharp=40)
    w(g_box(0.052, 0.0410, 0.070, (0.000, 0, 0.395)), "gilt", bevel=0.0050, sharp=40)
    w(quillon([(-0.022, 0, 0.398), (-0.056, 0, 0.402), (-0.088, 0, 0.388), (-0.108, 0, 0.358)], [0.0150, 0.0115, 0.0072, 0.0012], n=6, flat=0.6),
      "blade_dark", sharp=30, hem=60)
    w(ribbon([sym(0.448, 0.0125, 0.0080), sym(0.480, 0.0150, 0.0074), sym(0.540, 0.0085, 0.0050), sym(0.578, 0.0, 0.0)], sec_diamond()),
      "blade_dark", sharp=20, hem=60)
    w(haft(-0.150, 0.452, 0.0150, 0.0136, n=10, seg=10, oval=0.84, bulge=[(0, 1.14), (0.07, 1.0), (1, 1.0)]), "ebony", sharp=60)
    w(rings((0.110, 0.200, 0.290, 0.340), 0.0142, h=0.009, n=10, bulge=1.10), "gilt", sharp=50)
    w(grip_wrap(-0.070, 0.075, 0.0148, turns=10.5, width=0.0068, thick=0.0022, oval=0.84), "grip_red", sharp=70)
    w(rings((-0.078, 0.083), 0.0156, h=0.007, n=10, bulge=1.10), "gilt", sharp=50)
    gem_pommel(w, -0.146, r=0.0105, h=0.046, cup_r=0.0185)


@variant("mace1", 3)
def mace1_v3(w):
    # tempest mace: six gilt ribs caging a crackling storm crystal, a spike on every rib, crown point, dark steel haft with gilt wire
    cz = 0.408
    w(gxf(g_gem(0.0250, 0.128, 6, 0.5), T(0, 0, cz)), "storm", sharp=20)
    rib = quillon([(0.012, 0, cz - 0.072), (0.040, 0, cz - 0.048), (0.055, 0, cz), (0.040, 0, cz + 0.048), (0.012, 0, cz + 0.072)],
                  [0.0062, 0.0068, 0.0074, 0.0068, 0.0062], n=6, flat=0.62)
    w(radial(rib, 6), "gilt", sharp=50)
    w(radial(gxf(spike(0.0085, 0.030, 5, z0=0.058), T(0, 0, cz) @ R('Y', 90)), 6), "darksteel", sharp=30, hem=60)
    w(g_lathe([(0.0112, cz - 0.100), (0.0190, cz - 0.090), (0.0215, cz - 0.076), (0.0130, cz - 0.066), (0.0, cz - 0.064)], n=12), "gilt", sharp=40)
    w(g_lathe([(0.0, cz + 0.064), (0.0130, cz + 0.066), (0.0200, cz + 0.076), (0.0150, cz + 0.088), (0.0, cz + 0.090)], n=12), "gilt", sharp=40)
    w(spike(0.0095, 0.048, 6, z0=cz + 0.086), "darksteel", sharp=30, hem=60)
    w(radial(gxf(spike(0.0048, 0.018, 4, z0=0.014), T(0, 0, cz + 0.080) @ R('Y', 52)), 6, 30), "gilt", sharp=30)
    w(g_lathe([(0.0, -0.132), (0.0108, -0.130), (0.0112, 0.100), (0.0150, 0.108), (0.0150, 0.118), (0.0105, 0.126), (0.0100, 0.300),
               (0.0112, 0.310)], n=10), "darksteel", sharp=40)
    w(helix(0.0108, 0.132, 0.300, 6.0, 0.0024, 0.0020, n=4, spt=10), "gilt", sharp=70)
    w(grip_wrap(-0.062, 0.068, 0.0112, turns=9.5, width=0.0066, thick=0.0022, oval=1.0), "grip_black", sharp=70)
    w(grip_wrap(-0.058, 0.064, 0.0126, turns=6.0, width=0.0022, thick=0.0018, oval=1.0, hand=-1), "gilt", sharp=70)
    w(rings((-0.070, 0.076), 0.0124, h=0.007, n=10, bulge=1.14), "gilt", sharp=50)
    gem_pommel(w, -0.128, glow="storm", r=0.0100, h=0.042, cup_r=0.0180)


@variant("dagger", 3)
def dagger_v3(w):
    # shadow kris: a waved blade of black steel with a violet-lit fuller, silver crescent guard, wire-bound grip, amethyst pommel
    zs = np.linspace(0.062, 0.318, 26)
    st = []
    for z in zs:
        t = (z - 0.062) / (0.335 - 0.062)
        xc = 0.0062 * math.sin(TAU * (z - 0.062) / 0.078) * (1 - t) ** 0.6 * float(sstep(z, 0.062, 0.085))
        hw = 0.0170 * (1 - t ** 1.7) * (1.0 - 0.10 * math.cos(TAU * (z - 0.062) / 0.078))
        st.append((float(z), xc - hw, xc + hw, 0.0052 - 0.0022 * t))
    st.append((0.336, 0.0, 0.0, 0.0))
    fd = lambda s: 0.6 * float(sstep(s, 0.270, 0.230)) * float(sstep(s, 0.064, 0.080))
    w(ribbon(st, sec_fuller(0.0052, 0.40, fd)), "blade_dark", sharp=14, hem=60)
    w(inlay([(q[0], 0.0022 * (1 - 0.5 * i / 16.0), 0.0052 / 2 * 0.42, (q[1] + q[2]) / 2) for i, q in enumerate(st[2:19])]), "void", sharp=30)
    horn = [(0.010, 0, 0.0540), (0.032, 0, 0.0515), (0.049, 0, 0.0610), (0.056, 0, 0.0800), (0.052, 0, 0.0960)]
    w(both_x(quillon(horn, [0.0078, 0.0072, 0.0062, 0.0042, 0.0008], n=8, flat=0.8)), "silver", sharp=50)
    w(g_prism([(-0.016, 0.046), (0.016, 0.046), (0.020, 0.056), (0.008, 0.070), (-0.008, 0.070), (-0.020, 0.056)], 0.0180), "silver", bevel=0.0028, sharp=40)
    w(both_y(gxf(g_gem(0.0062, 0.0070, 8, 0.75), T(0, 0.0082, 0.0575) @ R('X', -90))), "void", sharp=20)
    w(grip_core(-0.050, 0.046, 0.0116, n=10, swell=1.10), "grip_black", sharp=60)
    w(grip_wrap(-0.042, 0.040, 0.0116, turns=11.0, width=0.0022, thick=0.0018, n=4), "silver", sharp=70)
    w(rings((-0.0465, 0.0, 0.0435), 0.0122, h=0.006, n=10, bulge=1.12), "silver", sharp=50)
    gem_pommel(w, -0.050, metal="silver", glow="void", r=0.0088, h=0.040, cup_r=0.0150)


@variant("wand", 3)
def wand_v3(w):
    # star wand: twin silver vines twist up a starmetal rod and open into a cradle for a floating spark of blue light
    prof = [(0.0, -0.092), (0.0050, -0.090), (0.0105, -0.076), (0.0120, -0.040), (0.0112, 0.010), (0.0086, 0.044), (0.0066, 0.070),
            (0.0058, 0.140), (0.0052, 0.212)]
    w(g_lathe(prof, n=12), "starmetal", sharp=50)
    for ph in (0.0, PI):
        w(helix(0.0070, 0.050, 0.214, 3.2, 0.0030, 0.0024, n=4, spt=12, r1=0.0060, phase=ph), "silver", sharp=70)
    w(rings((-0.078, 0.046, 0.212), 0.0090, h=0.007, n=12, bulge=1.3, r_fn=lambda z: 0.0108 if z < 0 else (0.0088 if z < 0.1 else 0.0056)),
      "silver", sharp=50)
    cz = 0.276
    cr = [(0.0050, 0, 0.214), (0.0200, 0, 0.232), (0.0290, 0, 0.262), (0.0250, 0, 0.296), (0.0140, 0, 0.318)]
    w(both_x(quillon(cr, [0.0040, 0.0038, 0.0032, 0.0024, 0.0006], n=6, flat=0.7)), "silver", sharp=50)
    w(gxf(star_core(0.0078, 2.9), T(0, 0, cz)), "star", sharp=20)
    for k, (a, rr, dz) in enumerate(((20, 0.036, 0.030), (140, 0.040, -0.010), (260, 0.034, 0.050))):
        p = (rr * math.cos(math.radians(a)), rr * 0.5 * math.sin(math.radians(a)), cz + dz)
        w(gxf(g_gem(0.0030, 0.014, 4, 0.5), T(*p) @ R('Y', 30 * k)), "star", sharp=20)
    moon = plate([(x, z - 0.106) for x, z in crescent(0.0150, 0.0118, 0.0052, 9)], 0.0050)
    w(moon, "silver", bevel=0.0010, sharp=30)
    w(g_sphere(0.0062, 8, 6), "silver", M=T(0, 0, -0.094), sharp=60)


@variant("scythe1", 3)
def scythe1_v3(w):
    # soul sickle: a broad barbed hook of black steel whose inner edge burns green, vertebra haft of old bone, chain and skull charm
    P = smooth_path([(0.0, 0.292), (0.002, 0.360), (0.030, 0.436), (0.094, 0.486), (0.172, 0.488), (0.238, 0.442), (0.276, 0.362),
                     (0.284, 0.270)], 34)
    st = along(P, [(0.0, -0.016, 0.018, 0.0080), (0.10, -0.022, 0.028, 0.0078), (0.30, -0.028, 0.036, 0.0070), (0.55, -0.024, 0.034, 0.0060),
                   (0.78, -0.016, 0.024, 0.0048), (0.93, -0.007, 0.011, 0.0034), (1.0, 0.0, 0.0, 0.0)])
    w(ribbon(dense(st, 30), sec_single_fuller(0.013, 1.0, 0.45), path=P), "blade_dark", sharp=14, hem=60)
    fr = list(np.linspace(0.06, 0.90, 16))
    w(glow_line(path_pts(P, fr, lambda f: 0.012 + 0.010 * math.sin(PI * f)), 0.0030, r=0.0026), "soul", sharp=40)
    for f, ln in ((0.22, 0.034), (0.40, 0.040), (0.58, 0.032)):
        (bx, bz), (tx, tz) = path_pts(P, [f], -0.026)[0], path_pts(P, [f + 0.03], -0.026 - ln)[0]
        (cx, cz_) = path_pts(P, [f + 0.07], -0.024)[0]
        w(g_prism([(bx, bz), (tx, tz), (cx, cz_)], 0.0056), "blade_dark", bevel=0.0010, sharp=30, hem=60)
    w(g_lathe([(0.0135, 0.236), (0.0185, 0.246), (0.0175, 0.286), (0.0150, 0.306), (0.0, 0.310)], n=10), "darksteel", sharp=40)
    seg = []
    for i in range(7):
        z0 = -0.108 + i * 0.049
        seg += [(0.0105, z0), (0.0160, z0 + 0.010), (0.0170, z0 + 0.024), (0.0125, z0 + 0.040)]
    w(g_lathe([(0.0, -0.112)] + seg + [(0.0135, 0.238)], n=10, sy=0.88), "oldbone", sharp=50)
    w(gxf(torus(0.0100, 0.0028, 10, 5), T(0, 0, -0.120) @ R('X', 90)), "darksteel", sharp=60)
    cp = smooth_path([(0.0, 0, -0.128), (0.012, 0, -0.160), (0.030, 0, -0.184), (0.036, 0, -0.214)], 10)
    w(g_chain(cp, link=0.020, wire=0.0026, width=0.66, seg=6, n=4, up=(0, 1, 0)), "darksteel", sharp=60)
    for g_, kind in human_skull(0.52):
        w(gxf(g_, T(0.036, 0, -0.236)), "bone" if kind not in ('dark', 'nose') else "ebonybox", sharp=50)


@variant("flail", 3)
def flail_v3(w):
    # twin censer flail: two chains from a gilt cap, each ending in a ringed star-ball that burns from within
    w(haft(-0.120, 0.262, 0.0136, 0.0126, n=10, seg=6, bulge=[(0, 1.14), (0.1, 1.0), (1, 1.0)]), "ebony", sharp=60)
    w(grip_wrap(-0.064, 0.068, 0.0134, turns=9.5, width=0.0066, thick=0.0022, oval=1.0), "grip_red", sharp=70)
    w(helix(0.0134, 0.084, 0.230, 5.0, 0.0024, 0.0020, n=4, spt=10), "gilt", sharp=70)
    w(rings((-0.072, 0.076), 0.0142, h=0.007, n=10, bulge=1.10), "gilt", sharp=50)
    gem_pommel(w, -0.118, glow="flame", r=0.0100, h=0.042, cup_r=0.0180)
    w(g_lathe([(0.0130, 0.234), (0.0180, 0.242), (0.0190, 0.270), (0.0240, 0.280), (0.0170, 0.294), (0.0060, 0.300), (0.0, 0.301)], n=12), "gilt", sharp=40)
    for sgn, pts, bc, br in ((1, [(0.008, 0, 0.300), (0.030, 0, 0.342), (0.072, 0, 0.366), (0.120, 0, 0.352), (0.150, 0, 0.312), (0.160, 0, 0.268)], (0.162, 0.0, 0.212), 0.030),
                             (-1, [(-0.008, 0, 0.300), (-0.030, 0, 0.330), (-0.066, 0, 0.330), (-0.090, 0, 0.296), (-0.096, 0, 0.262)], (-0.098, 0.0, 0.214), 0.025)):
        w(gxf(torus(0.0080, 0.0026, 10, 5), T(sgn * 0.008, 0, 0.3035) @ R('X', 90)), "gilt", sharp=60)
        w(g_chain(smooth_path(pts, 14), link=0.022, wire=0.0030, width=0.66, seg=6, n=4, up=(0, 1, 0)), "darksteel", sharp=60)
        w(gxf(g_sphere(br * 0.80, 12, 8), T(*bc)), "flame", sharp=60)
        hoop = torus(br, br * 0.17, 16, 5)
        w(gxf(gjoin(hoop, gxf(hoop, R('X', 90)), gxf(hoop, R('Y', 90))), T(*bc)), "gilt", sharp=50)
        sp = [gxf(spike(br * 0.26, br * 0.82, 5, z0=br * 0.96), frame(bc, d)) for d in
              ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, -1), (0.7, 0.7, 0), (-0.7, 0.7, 0), (0.7, -0.7, 0), (-0.7, -0.7, 0),
               (0.7, 0, -0.7), (-0.7, 0, -0.7), (0, 0.7, -0.7), (0, -0.7, -0.7))]
        w(gjoin(*sp), "darksteel", sharp=30, hem=60)
        w(gxf(torus(br * 0.26, 0.0026, 8, 5), T(bc[0], 0, bc[2] + br * 1.18) @ R('X', 90)), "gilt", sharp=60)


@variant("fist", 3)
def fist_v3(w):
    # forge claws: three talons of black steel with ember-lit channels on a gilt knuckle plate, red grip between dark side bars
    w(tube_w([(0, 0, -0.056), (0, 0, 0.056)], 0.0112, 10), "grip_red", sharp=60)
    w(grip_wrap(-0.044, 0.044, 0.0112, turns=7.5, width=0.0060, thick=0.0020, oval=1.0), "grip_red", sharp=70)
    bar = g_prism([(-0.110, 0.0056), (-0.126, 0.0000), (-0.110, -0.0056), (0.052, -0.0062), (0.052, 0.0062)], 0.0085)
    w(gjoin(gxf(bar, T(0, 0, 0.0535)), gxf(bar, T(0, 0, -0.0535))), "darksteel", bevel=0.0016, sharp=40)
    w(gjoin(*[gxf(stud(0.0052, n=6), frame((0.0, sy_ * 0.0042, sz_ * 0.0535), (0, sy_, 0))) for sy_ in (1, -1) for sz_ in (1, -1)]), "gilt", sharp=60)
    w(g_prism([(0.044, -0.064), (0.066, -0.058), (0.074, 0.0), (0.066, 0.058), (0.044, 0.064)], 0.0190), "gilt", bevel=0.0035, sharp=40)
    for z0 in (-0.040, 0.0, 0.040):
        P = smooth_path([(0.060, z0), (0.140, z0 * 1.42), (0.225, z0 * 1.62), (0.300, z0 * 1.30 - 0.004)], 16)
        st = along(P, [(0.0, -0.0125, 0.0125, 0.0072), (0.25, -0.0115, 0.0115, 0.0066), (0.65, -0.0078, 0.0078, 0.0052),
                       (0.90, -0.0034, 0.0034, 0.0036), (1.0, 0.0, 0.0, 0.0)])
        w(ribbon(dense(st, 14), sec_ridge(0.0050, 1.5), path=P), "blade_dark", sharp=14, hem=60)
        w(glow_line(path_pts(P, list(np.linspace(0.06, 0.66, 9))), 0.0052, r=0.0022), "ember", sharp=40)
        w(both_y(gxf(g_gem(0.0058, 0.0060, 6, 0.75), T(0.060, 0.0100, z0) @ R('X', -90))), "ember", sharp=20)


@variant("sceptre", 3)
def sceptre_v3(w):
    # hierophant's sceptre: a pale sun floating in an open crown of six gilt leaves, a tilted halo, winged collar, red grip
    prof = [(0.0, -0.150), (0.0060, -0.148), (0.0150, -0.136), (0.0150, -0.124), (0.0100, -0.116), (0.0105, -0.070),
            (0.0105, 0.070), (0.0155, 0.078), (0.0155, 0.088), (0.0090, 0.098), (0.0082, 0.190), (0.0135, 0.198), (0.0135, 0.208),
            (0.0080, 0.216), (0.0074, 0.312), (0.0160, 0.322), (0.0190, 0.336), (0.0130, 0.348), (0.0, 0.350)]
    w(g_lathe(prof, n=12), "gilt", sharp=40)
    w(tube_w([(0, 0, -0.068), (0, 0, 0.068)], 0.0116, 10), "grip_red", sharp=60)
    w(grip_wrap(-0.060, 0.060, 0.0116, turns=9.0, width=0.0060, thick=0.0020, oval=1.0), "grip_red", sharp=70)
    w(helix(0.0086, 0.100, 0.188, 3.5, 0.0022, 0.0018, n=4, spt=10), "silver", sharp=70)
    oz = 0.420
    leaf = quillon([(0.0100, 0, 0.340), (0.0310, 0, 0.362), (0.0420, 0, oz - 0.012), (0.0380, 0, oz + 0.028), (0.0500, 0, oz + 0.064)],
                   [0.0050, 0.0054, 0.0056, 0.0044, 0.0010], n=6, flat=0.55)
    w(radial(leaf, 6), "gilt", sharp=50)
    w(gxf(g_sphere(0.0240, 16, 10), T(0, 0, oz)), "holy", sharp=60)
    w(gxf(torus(0.0640, 0.0034, 28, 5), T(0, 0, oz) @ R('X', 72) @ R('Y', 12)), "gilt", sharp=60)
    wing = plate([(0.014, 0.318), (0.052, 0.330), (0.082, 0.356), (0.060, 0.350), (0.070, 0.372), (0.044, 0.356), (0.048, 0.380),
                  (0.026, 0.356), (0.014, 0.346)], 0.0050)
    w(both_x(wing), "gilt", bevel=0.0010, sharp=30)
    w(gxf(g_gem(0.0080, 0.030, 6, 0.5), T(0, 0, -0.166)), "holy", sharp=20)


# =============================================================================================== 2H
@variant("sword2", 3)
def sword2_v3(w):
    # ember flamberge: a flame-waved blade of black steel with a burning fuller, horned crescent guard, red grip, ember pommel
    fd = lambda s: 0.6 * float(sstep(s, 1.10, 0.98)) * float(sstep(s, 0.350, 0.390))
    head = [sym(0.105, 0.0230, 0.0082), sym(0.262, 0.0230, 0.0082), sym(0.272, 0.0300, 0.0080), sym(0.292, 0.0640, 0.0072),
            sym(0.312, 0.0520, 0.0074), sym(0.338, 0.0400, 0.0076)]
    body = []
    for z in np.linspace(0.350, 1.340, 46):
        t = (z - 0.350) / (1.430 - 0.350)
        xc = 0.0075 * math.sin(TAU * (z - 0.350) / 0.118) * float(sstep(z, 0.350, 0.400)) * (1 - t) ** 0.3
        hw = (0.0395 - 0.0115 * t) * (1 - t ** 5) * (1.0 - 0.07 * math.cos(TAU * (z - 0.350) / 0.118))
        body.append((float(z), xc - hw, xc + hw, 0.0076 - 0.0036 * t))
    body.append((1.432, 0.0, 0.0, 0.0))
    w(ribbon(head + body, sec_fuller(0.0090, 0.38, fd)), "blade_dark", sharp=14, hem=60)
    w(inlay([(q[0], 0.0038 * (1 - 0.5 * i / 28.0), 0.0072 / 2 * 0.42, (q[1] + q[2]) / 2) for i, q in enumerate(body[2:30])]), "ember", sharp=30)
    w(g_lathe([(0.0255, 0.118), (0.0268, 0.185), (0.0255, 0.250)], n=12, sy=0.30), "grip_red", sharp=60)
    w(gjoin(*[g_lathe([(0.0265, z - 0.005), (0.0284, z), (0.0265, z + 0.005)], n=12, sy=0.32) for z in (0.120, 0.248)]), "gilt", sharp=50)
    horn = [(0.020, 0, 0.0900), (0.078, 0, 0.0830), (0.138, 0, 0.0960), (0.184, 0, 0.1340), (0.200, 0, 0.1900)]
    w(both_x(quillon(horn, [0.0145, 0.0128, 0.0108, 0.0075, 0.0012], n=8, flat=0.80)), "gilt", sharp=50)
    hook = [(0.060, 0, 0.0820), (0.098, 0, 0.0640), (0.118, 0, 0.0380), (0.110, 0, 0.0160)]
    w(both_x(quillon(hook, [0.0090, 0.0078, 0.0056, 0.0010], n=6, flat=0.80)), "gilt", sharp=50)
    w(g_prism([(-0.030, 0.070), (0.030, 0.070), (0.038, 0.088), (0.016, 0.112), (-0.016, 0.112), (-0.038, 0.088)], 0.0320), "gilt", bevel=0.0050, sharp=40)
    w(both_y(gxf(g_gem(0.0125, 0.0130, 8, 0.75), T(0, 0.0145, 0.0890) @ R('X', -90))), "ember", sharp=20)
    w(grip_core(-0.278, 0.068, 0.0158, n=10, swell=1.02, taper=0.98, oval=0.88), "grip_red", sharp=60)
    w(grip_wrap(-0.266, -0.108, 0.0158, turns=11.0, width=0.0068, thick=0.0022, oval=0.88), "grip_red", sharp=70)
    w(grip_wrap(-0.092, 0.060, 0.0158, turns=11.0, width=0.0068, thick=0.0022, oval=0.88, hand=-1), "grip_red", sharp=70)
    w(g_lathe([(0.0160, -0.110), (0.0210, -0.100), (0.0160, -0.090)], n=12, sy=0.88), "gilt", sharp=50)
    w(rings((-0.2735, 0.0645), 0.0168, h=0.008, n=12, bulge=1.12), "gilt", sharp=50)
    gem_pommel(w, -0.276, r=0.0150, h=0.070, cup_r=0.0245)


@variant("axe2", 3)
def axe2_v3(w):
    # reaver's moon: a great bardiche crescent of black steel, its lower horn clasped to the haft, long ember glyphs,
    # a heavy back spike, long gilt langets on a banded ebony haft
    st = slices(edge=[(0.440, 0.052), (0.500, 0.150), (0.600, 0.236), (0.730, 0.276), (0.860, 0.266), (0.960, 0.220), (1.030, 0.160)],
                back=[(0.440, 0.014), (0.478, 0.018), (0.500, 0.078), (0.580, 0.140), (0.660, 0.112), (0.722, 0.040), (0.732, -0.026),
                      (0.812, -0.026), (0.822, 0.040), (0.900, 0.104), (0.975, 0.146), (1.030, 0.160)],
                thick=[(0.440, 0.0090), (0.500, 0.0100), (0.722, 0.0160), (0.732, 0.0500), (0.812, 0.0500), (0.822, 0.0160), (0.950, 0.0085),
                       (1.030, 0.0030)], n=44)
    w(ribbon(st, sec_wedge(0.024, 0.34)), "blade_dark", sharp=16, hem=60)
    wy = wedge_y(st, 0.024, 0.34)
    arc = [(0.112, 0.520), (0.186, 0.612), (0.228, 0.730), (0.222, 0.850), (0.184, 0.950), (0.150, 1.004)]
    w(glow_line(smooth_path(arc, 19).tolist(), wy, r=0.0050), "ember", sharp=40)
    for (x0, x1, z0, z1) in ((0.060, 0.176, 0.772, 0.772), (0.074, 0.150, 0.812, 0.842), (0.074, 0.150, 0.732, 0.702)):
        w(glow_line([(x0, z0), ((x0 + x1) / 2, (z0 + z1) / 2), (x1, z1)], wy, r=0.0036, taper=False), "ember", sharp=40)
    w(g_box(0.068, 0.0550, 0.098, (0.000, 0, 0.772)), "gilt", bevel=0.0060, sharp=40)
    w(g_box(0.056, 0.0320, 0.040, (0.026, 0, 0.459)), "gilt", bevel=0.0050, sharp=40)
    w(g_lathe([(0.0215, 0.436), (0.0245, 0.459), (0.0215, 0.482)], n=10, sy=0.9), "gilt", sharp=50)
    w(quillon([(-0.028, 0, 0.772), (-0.092, 0, 0.778), (-0.152, 0, 0.774), (-0.206, 0, 0.764)], [0.0225, 0.0165, 0.0092, 0.0012], n=6, flat=0.62),
      "blade_dark", sharp=30, hem=60)
    w(haft(-0.500, 0.846, 0.0178, 0.0158, n=10, seg=14, oval=0.86, bulge=[(0, 1.10), (0.05, 1.0), (1, 1.0)]), "ebony", sharp=60)
    w(g_lathe([(0.0170, 0.842), (0.0215, 0.850), (0.0185, 0.866), (0.0090, 0.880), (0.0, 0.896)], n=10), "gilt", sharp=40)
    w(langets(0.250, 0.722, 0.0138, 0.0140, 0.0038), "gilt", bevel=0.0012, sharp=40)
    w(both_y(rivet_row((0.275, 0.335, 0.395, 0.520, 0.585, 0.650), 0.0170, (0, 1, 0), 0.0038)), "gilt", sharp=60)
    w(grip_wrap(-0.078, 0.085, 0.0170, turns=10.5, width=0.0074, thick=0.0024, oval=0.86), "grip_red", sharp=70)
    w(grip_wrap(-0.430, -0.290, 0.0176, turns=9.0, width=0.0074, thick=0.0024, oval=0.86, hand=-1), "grip_red", sharp=70)
    w(rings((-0.438, -0.282, -0.086, 0.093, 0.200), 0.0182, h=0.008, n=10, bulge=1.10), "gilt", sharp=50)
    w(g_lathe([(0.0, -0.580), (0.0060, -0.550), (0.0210, -0.514), (0.0210, -0.494), (0.0180, -0.488)], n=8), "gilt", sharp=40)


@variant("mace2", 3)
def mace2_v3(w):
    # forge-heart maul: a block of living ember caged in black iron and gilt, toothed striking face, beak and crown spike
    hz = 0.800
    w(g_box(0.150, 0.070, 0.088, (0.010, 0, hz)), "ember", sharp=40)
    bars = [g_box(0.176, 0.016, 0.016, (0.010, sy_ * 0.040, hz + sz_ * 0.049)) for sy_ in (1, -1) for sz_ in (1, -1)]
    w(gjoin(*bars), "darksteel", bevel=0.0030, sharp=40)
    w(g_box(0.030, 0.100, 0.118, (0.0, 0, hz)), "gilt", bevel=0.0050, sharp=40)
    grate = [g_box(0.010, 0.082, 0.100, (x, 0, hz)) for x in (-0.046, 0.036, 0.064)]
    grate += [g_box(0.176, 0.080, 0.010, (0.010, 0, hz))]
    w(gjoin(*grate), "darksteel", bevel=0.0020, sharp=40)
    w(g_box(0.020, 0.098, 0.116, (0.094, 0, hz)), "darksteel", bevel=0.0060, sharp=40)
    w(g_box(0.018, 0.094, 0.112, (-0.074, 0, hz)), "darksteel", bevel=0.0060, sharp=40)
    teeth = [gxf(spike(0.0150, 0.026, 4, z0=0.0), frame((0.103, sy_ * 0.024, hz + sz_ * 0.029), (1, 0, 0)) @ R('Z', 45)) for sy_ in (1, -1) for sz_ in (1, -1)]
    w(gjoin(*teeth), "darksteel", sharp=30, hem=60)
    w(quillon([(-0.082, 0, hz + 0.004), (-0.126, 0, hz + 0.006), (-0.172, 0, hz - 0.014), (-0.204, 0, hz - 0.056)], [0.0250, 0.0190, 0.0118, 0.0020], n=6, flat=0.72),
      "darksteel", sharp=30, hem=60)
    w(spike(0.0150, 0.120, 6, z0=hz + 0.056), "gilt", sharp=30, hem=60)
    w(haft(-0.480, hz + 0.058, 0.0178, 0.0160, n=10, seg=14, bulge=[(0, 1.10), (0.05, 1.0), (1, 1.0)]), "ebony", sharp=60)
    w(langets(0.560, hz - 0.058, 0.0142, 0.0140, 0.0038, sides=((0, 1), (0, -1), (1, 0), (-1, 0))), "gilt", bevel=0.0012, sharp=40)
    w(grip_wrap(-0.078, 0.085, 0.0172, turns=10.5, width=0.0074, thick=0.0024, oval=1.0), "grip_red", sharp=70)
    w(grip_wrap(-0.410, -0.270, 0.0176, turns=9.0, width=0.0074, thick=0.0024, oval=1.0, hand=-1), "grip_red", sharp=70)
    w(rings((-0.418, -0.262, -0.086, 0.093, 0.300, 0.548), 0.0184, h=0.008, n=10, bulge=1.10), "gilt", sharp=50)
    gem_pommel(w, -0.478, r=0.0130, h=0.056, cup_r=0.0230)


@variant("staff", 3)
def staff_v3(w):
    # astrolabe staff: a great silver ring and a tilted starmetal ring about a floating star, crescent finial, banded ebony shaft
    w(haft(-0.860, 0.800, 0.0166, 0.0150, n=10, seg=14), "ebony", sharp=60)
    w(g_lathe([(0.0, -0.905), (0.0080, -0.890), (0.0200, -0.862), (0.0200, -0.800), (0.0175, -0.792)], n=10), "silver", sharp=40)
    w(rings((-0.520, -0.300, 0.300, 0.520, 0.700), 0.0164, h=0.010, n=10, bulge=1.14), "silver", sharp=50)
    w(grip_wrap(-0.085, 0.105, 0.0160, turns=9.0, width=0.0110, thick=0.0026, oval=1.0, gap=0.3), "velvet_blue", sharp=70)
    w(grip_wrap(-0.080, 0.100, 0.0176, turns=6.0, width=0.0022, thick=0.0018, oval=1.0, hand=-1), "silver", sharp=70)
    w(rings((-0.094, 0.114), 0.0170, h=0.008, n=10, bulge=1.10), "silver", sharp=50)
    w(g_lathe([(0.0150, 0.770), (0.0220, 0.782), (0.0185, 0.806), (0.0270, 0.828), (0.0240, 0.852), (0.0130, 0.866), (0.0, 0.868)], n=12), "silver", sharp=40)
    cz, R1 = 0.985, 0.110
    w(gxf(torus(R1, 0.0085, 28, 6), T(0, 0, cz) @ R('X', 90)), "silver", sharp=60)
    w(gxf(torus(0.084, 0.0062, 24, 6), T(0, 0, cz) @ R('Z', 58) @ R('X', 64)), "starmetal", sharp=60)
    w(gxf(torus(0.060, 0.0042, 20, 5), T(0, 0, cz) @ R('Z', -30) @ R('X', 40)), "silver", sharp=60)
    w(gjoin(*[gxf(g_sphere(0.0075, 6, 5), T(R1 * math.cos(a), 0, cz + R1 * math.sin(a))) for a in (0.0, PI, PI / 2 * 1.0)]), "starmetal", sharp=60)
    w(gxf(star_core(0.0150, 3.0), T(0, 0, cz)), "star", sharp=20)
    w(gxf(gscale(star_core(0.0100, 2.4), 1.0), T(0, 0, cz) @ R('Z', 45) @ R('X', 45)), "star", sharp=20)
    moon = plate([(x, z + cz + R1 + 0.030) for x, z in crescent(0.0400, 0.0320, 0.0130, 10)], 0.0080)
    w(moon, "silver", bevel=0.0016, sharp=30)
    w(g_lathe([(0.0085, cz + R1 - 0.004), (0.0120, cz + R1 + 0.006), (0.0050, cz + R1 + 0.016)], n=8), "silver", sharp=40)


@variant("polearm", 3)
def polearm_v3(w):
    # dragon-tongue glaive: a recurved blade of black steel with an ember vein, winged back-hook, gilt socket with a red tassel
    st = dense([(0.725, -0.0170, 0.0180, 0.0090), (0.780, -0.0195, 0.0380, 0.0090), (0.870, -0.0240, 0.0760, 0.0084),
                (0.990, -0.0230, 0.0940, 0.0076)], 12)
    st += [(1.040, -0.0200, 0.0950, 0.0072), (1.062, -0.0040, 0.0945, 0.0070), (1.084, -0.0330, 0.0935, 0.0068)]
    st += dense([(1.084, -0.0330, 0.0935, 0.0068), (1.200, -0.0250, 0.0840, 0.0060), (1.320, -0.0090, 0.0620, 0.0050),
                 (1.400, 0.0100, 0.0420, 0.0042), (1.455, 0.0300, 0.0300, 0.0)], 14)[1:]
    w(ribbon(st, sec_single_fuller(0.018, 1.0, 0.40)), "blade_dark", sharp=14, hem=60)
    vein = [(0.012, 0.790), (0.030, 0.880), (0.040, 0.990), (0.038, 1.100), (0.034, 1.210), (0.032, 1.310), (0.030, 1.390)]
    w(glow_line(smooth_path(vein, 15).tolist(), 0.0036, r=0.0036), "ember", sharp=40)
    w(g_prism([(-0.020, 0.840), (-0.074, 0.866), (-0.112, 0.930), (-0.086, 0.914), (-0.094, 0.962), (-0.062, 0.930), (-0.060, 0.968), (-0.022, 0.930)],
              0.0076), "blade_dark", bevel=0.0014, sharp=30, hem=60)
    w(g_lathe([(0.0158, 0.620), (0.0215, 0.632), (0.0200, 0.700), (0.0170, 0.730), (0.0100, 0.742), (0.0, 0.744)], n=10), "gilt", sharp=40)
    w(rings((0.634, 0.700), 0.0212, h=0.008, n=10, bulge=1.10), "gilt", sharp=50)
    w(gxf(torus(0.0130, 0.0032, 10, 5), T(-0.030, 0, 0.660) @ R('X', 90)), "gilt", sharp=60)
    w(g_lathe([(0.0030, 0.0), (0.0095, -0.014), (0.0125, -0.070), (0.0160, -0.130), (0.0, -0.126)], n=8, sy=0.6), "rag_red",
      M=T(-0.034, 0, 0.648) @ R('Y', -8), sharp=60)
    w(haft(-0.800, 0.630, 0.0168, 0.0158, n=10, seg=16, oval=0.90), "ebony", sharp=60)
    w(langets(0.460, 0.622, 0.0142, 0.0130, 0.0036), "gilt", bevel=0.0012, sharp=40)
    w(grip_wrap(-0.085, 0.105, 0.0164, turns=12.0, width=0.0074, thick=0.0024, oval=0.90), "grip_red", sharp=70)
    w(grip_wrap(-0.530, -0.370, 0.0166, turns=10.0, width=0.0074, thick=0.0024, oval=0.90, hand=-1), "grip_red", sharp=70)
    w(rings((-0.538, -0.362, -0.093, 0.113, 0.300), 0.0172, h=0.008, n=10, bulge=1.10), "gilt", sharp=50)
    w(g_lathe([(0.0, -0.895), (0.0070, -0.858), (0.0200, -0.812), (0.0200, -0.780), (0.0170, -0.772)], n=8), "gilt", sharp=40)


@variant("scythe2", 3)
def scythe2_v3(w):
    # reaper's scythe: a long double-curved blade of black steel with a soul-lit edge, horned twin skull at the socket, spine barbs
    P = smooth_path([(0.004, 0.905), (0.100, 0.958), (0.260, 0.978), (0.430, 0.942), (0.588, 0.856), (0.696, 0.734), (0.748, 0.596)], 38)
    st = along(P, [(0.0, -0.036, 0.046, 0.0092), (0.12, -0.036, 0.052, 0.0086), (0.40, -0.030, 0.048, 0.0072), (0.70, -0.020, 0.036, 0.0056),
                   (0.90, -0.010, 0.019, 0.0040), (1.0, 0.0, 0.0, 0.0)])
    w(ribbon(dense(st, 34), sec_single_fuller(0.018, 1.0, 0.40), path=P), "blade_dark", sharp=14, hem=60)
    fr = list(np.linspace(0.05, 0.92, 20))
    w(glow_line(path_pts(P, fr, lambda f: 0.022 + 0.012 * math.sin(PI * f)), 0.0032, r=0.0034), "soul", sharp=40)
    for f, ln in ((0.16, 0.050), (0.30, 0.060), (0.44, 0.052), (0.58, 0.040)):
        (bx, bz), (tx, tz) = path_pts(P, [f], -0.030)[0], path_pts(P, [f + 0.035], -0.030 - ln)[0]
        (cx, cz_) = path_pts(P, [f + 0.075], -0.028)[0]
        w(g_prism([(bx, bz), (tx, tz), (cx, cz_)], 0.0064), "blade_dark", bevel=0.0012, sharp=30, hem=60)
    w(g_lathe([(0.0160, 0.790), (0.0220, 0.802), (0.0220, 0.880), (0.0190, 0.900), (0.0, 0.904)], n=10), "darksteel", sharp=40)
    for sgn in (1, -1):
        for g_, kind in human_skull(1.30):
            w(gxf(g_, T(0.0, sgn * 0.006, 0.918) @ S(1, sgn, 1)), "oldbone" if kind not in ('dark', 'nose') else "ebonybox", sharp=50)
        w(gxf(g_tooth(0.120, 0.0130, 0.050, 6), T(sgn * 0.030, 0, 0.950) @ R('Z', 90 - 90 * sgn) @ R('Y', 30)), "horn", sharp=50)
    w(tube_w(smooth_path([(0.0, 0, -0.860), (0.010, 0, -0.400), (-0.012, 0, 0.100), (0.0, 0, 0.560), (0.0, 0, 0.800)], 20), list(np.linspace(0.0172, 0.0160, 20)), 10),
      "ebony", sharp=60)
    w(grip_wrap(-0.085, 0.105, 0.0166, turns=12.0, width=0.0074, thick=0.0024, oval=1.0), "grip_black", sharp=70)
    w(gxf(grip_wrap(-0.080, 0.080, 0.0170, turns=10.0, width=0.0074, thick=0.0024, oval=1.0, hand=-1), T(0.006, 0, -0.420)), "grip_black", sharp=70)
    w(rings((-0.093, 0.113), 0.0176, h=0.008, n=10, bulge=1.10), "oldbone", sharp=50)
    w(gxf(g_tooth(0.110, 0.0185, 0.012, 6), T(0, 0, -0.850) @ R('X', 180)), "oldbone", sharp=50)


@variant("bow", 3)
def bow_v3(w):
    # nightwing bow: black recurve with gilt-edged talon blades on the limbs, crystal sights and a string of teal light
    up = [(0.014, 0.066), (0.012, 0.200), (-0.018, 0.390), (-0.080, 0.580), (-0.150, 0.716), (-0.186, 0.776), (-0.168, 0.832)]
    for s in (1, -1):
        pts = [(x, s * z) for x, z in up]
        w(limb(pts, 0.0228, 0.0122), "ebony", sharp=60)
        w(gxf(g_tooth(0.070, 0.0105, 0.030, 6), T(pts[-1][0] + 0.002, 0, pts[-1][1] - s * 0.006) @ (R('Y', 20) if s > 0 else R('X', 180) @ R('Y', -20))),
          "gilt", sharp=50)
        bl = [(0.020, s * 0.140), (0.062, s * 0.200), (0.070, s * 0.300), (0.046, s * 0.420), (0.012, s * 0.520), (-0.014, s * 0.470), (0.006, s * 0.380),
              (0.014, s * 0.280), (0.010, s * 0.180)]
        w(g_prism(bl if s > 0 else bl[::-1], 0.0056), "blade_dark", bevel=0.0012, sharp=30, hem=60)
        ed = smooth_path([(0.024, s * 0.150), (0.064, s * 0.205), (0.071, s * 0.300), (0.047, s * 0.418), (0.014, s * 0.514)], 12)
        w(g_tube([(x, 0.0, z) for x, z in ed], 0.0034, n=5, sy=1.5, up=(0, 0, 1)), "gilt", sharp=50)
        for zz, rr in ((0.098, 0.0200), (0.560, 0.0142)):
            xx = float(np.interp(zz, [p[1] for p in up], [p[0] for p in up]))
            w(gxf(g_lathe([(rr, -0.010), (rr * 1.12, 0.0), (rr, 0.010)], n=10, sy=0.56), T(xx, 0, s * zz) @ R('Z', 90)), "gilt", sharp=50)
        w(gxf(g_gem(0.0070, 0.026, 6, 0.5), T(0.040, 0, s * 0.092)), "teal", sharp=20)
    w(tube_w([(0.014, 0, -0.074), (0.018, 0, -0.030), (0.018, 0, 0.030), (0.014, 0, 0.074)], [0.0185, 0.0200, 0.0200, 0.0185], 10,
             up=(0, 1, 0), sx=0.92, sy=0.86), "ebony", sharp=60)
    w(gxf(grip_wrap(-0.058, 0.058, 0.0192, turns=9.0, width=0.0068, thick=0.0022, oval=0.92), T(0.017, 0, 0) @ R('Z', 90)), "grip_black", sharp=70)
    t0, t1 = (up[-2][0] + 0.006, 0.0, up[-2][1] + 0.022), (up[-2][0] + 0.006, 0.0, -up[-2][1] - 0.022)
    w(tube([t1, (t1[0], 0, -0.03), (t0[0], 0, 0.03), t0], 0.0026, n=5), "teal", sharp=80)


@variant("crossbow", 3)
def crossbow_v3(w):
    # ravenlock arbalest: twin black prods with talon tips, a beaked gilt nose, gilt lock and rail, a string and bolt of teal light
    zt = 0.100
    til = g_prism([(-0.310, 0.036), (-0.330, 0.070), (-0.300, 0.110), (-0.220, zt), (0.450, zt - 0.002), (0.450, 0.058), (0.120, 0.052),
                   (-0.060, 0.052), (-0.200, 0.034)], 0.0380)
    w(til, "ebonybox", bevel=0.0050, sharp=40)
    w(tube_w([(0.006, 0, 0.056), (0.0, 0, 0.0), (-0.012, 0, -0.056), (-0.018, 0, -0.078)], [0.0150, 0.0160, 0.0165, 0.0185], 10,
             up=(0, 1, 0), sx=0.86, sy=1.0), "ebony", sharp=60)
    w(gxf(grip_wrap(-0.048, 0.040, 0.0160, turns=7.0, width=0.0066, thick=0.0022, oval=0.86), T(-0.004, 0, 0) @ R('Y', -8)), "grip_black", sharp=70)
    w(g_lathe([(0.0, -0.092), (0.0150, -0.088), (0.0195, -0.078), (0.0170, -0.070)], n=10), "gilt", M=T(-0.019, 0, 0) @ R('Y', -8), sharp=40)
    w(tube([(0.030, 0, 0.050), (0.060, 0, 0.030), (0.118, 0, 0.022), (0.140, 0, 0.026)], [0.0048, 0.0042, 0.0036, 0.0030], n=5), "gilt", sharp=50)
    pp = prod_path(0.360, 0.120, 0.428, 0.076, 15, recurve=0.045)
    w(g_tube(pp, [0.0130 - 0.0060 * abs(t) for t in np.linspace(-1, 1, 15)], n=6, up=(0, 0, 1), sx=1.6, sy=0.72), "darksteel", sharp=50)
    pq = prod_path(0.250, 0.095, 0.372, 0.108, 13, recurve=0.03)
    w(g_tube(pq, [0.0100 - 0.0050 * abs(t) for t in np.linspace(-1, 1, 13)], n=6, up=(0, 0, 1), sx=1.5, sy=0.72), "darksteel", sharp=50)
    tal = []
    for sy_ in (1, -1):
        tip = np.array(pp[-1 if sy_ > 0 else 0])
        tal.append(gxf(g_tooth(0.080, 0.0105, 0.030, 5), frame(tip, (0.75, sy_ * 0.66, 0.0), (0, 0, 1)) @ R('Z', 90 - 90 * sy_)))
        mid = np.array(pp[11 if sy_ > 0 else 3])
        tal.append(gxf(g_tooth(0.050, 0.0085, 0.016, 5), frame(mid, (1.0, sy_ * 0.2, 0.0), (0, 0, 1)) @ R('Z', 90 - 90 * sy_)))
    w(gjoin(*tal), "darksteel", sharp=40, hem=60)
    w(g_box(0.050, 0.046, 0.076, (0.416, 0, 0.088)), "gilt", bevel=0.0050, sharp=40)
    beak = [gxf(g_tooth(0.105, 0.0150, 0.026, 6), frame((0.436, 0, 0.076 + 0.018 * s), (1, 0, 0), (0, 1, 0)) @ R('Z', 90 + 90 * s)) for s in (1, -1)]
    w(gjoin(*beak), "gilt", sharp=40)
    w(g_box(0.064, 0.044, 0.024, (0.090, 0, zt + 0.005)), "gilt", bevel=0.0030, sharp=40)
    w(gxf(g_lathe([(0.0, -0.015), (0.0135, -0.015), (0.0135, 0.015), (0.0, 0.015)], n=10), T(0.090, 0, zt + 0.012) @ R('X', 90)), "darksteel", sharp=40)
    w(tube([pp[0], (0.094, 0, zt + 0.016), pp[-1]], 0.0022, n=5), "teal", sharp=80)
    w(tube([pq[0], (0.130, 0, zt + 0.030), pq[-1]], 0.0018, n=5), "teal", sharp=80)
    w(gxf(g_lathe([(0.0, 0.0), (0.0058, 0.0), (0.0058, 0.320), (0.0, 0.320)], n=6), T(0.100, 0, zt + 0.014) @ R('Y', 90)), "teal", sharp=60)
    w(gxf(g_lathe([(0.0, 0.0), (0.0130, 0.008), (0.0055, 0.032), (0.0, 0.070)], n=4), T(0.418, 0, zt + 0.014) @ R('Y', 90)), "darksteel", sharp=30, hem=60)
    w(g_box(0.300, 0.0400, 0.0040, (0.210, 0, zt + 0.001)), "gilt", bevel=0.0010, sharp=40)
    w(both_y(gxf(g_gem(0.0100, 0.0110, 6, 0.75), T(0.060, 0.0195, 0.076) @ R('X', -90))), "teal", sharp=20)
    w(g_box(0.014, 0.0430, 0.0800, (-0.318, 0, 0.072)), "gilt", bevel=0.0030, sharp=40)


# =============================================================================================== off-hand
@variant("shield", 3)
def shield_v3(w):
    # eclipse kite: tall kite of black steel, a gilt corona round an ember eye, burning seam to the point, gilt chevrons and wing finials
    M = T(0.085, S_OUT, 0.0)
    W_, ZT, ZB = 0.236, 0.310, -0.585
    wf, yf = kite_w(W_, head=0.23), shield_y(W_, 0.038, 0.89, -0.12, vdepth=0.012)
    w(gxf(g_shield_board(wf, yf, ZB, ZT, nu=11, nv=18), M), "blade_dark", recalc=False, solidify=0.0045, solid_offset=-1.0, sharp=40)
    yb = lambda x, z: yf(x, z) - 0.0046
    wb = lambda V: kite_w(W_, head=0.23)(V) - 0.004
    w(gxf(g_shield_board(wb, yb, ZB + 0.008, ZT - 0.003, nu=11, nv=18), M), "darkbox", recalc=False, solidify=0.010, solid_offset=-1.0, sharp=40)
    w(gxf(g_tube(shield_outline(wf, yf, ZB, ZT, nv=20), 1.0, n=6, closed=True, sx=0.0115, sy=0.0140, up=(0, 1, 0)), M), "gilt", sharp=50)
    ez = 0.085
    yc = float(yf(0, ez))
    w(gxf(ydome([(0.078, 0.0), (0.078, 0.010), (0.062, 0.017), (0.050, 0.013), (0.047, 0.003)], n=18, y0=yc, z0=ez), M), "gilt", sharp=40)
    w(gxf(ydome([(0.0, 0.016), (0.022, 0.014), (0.040, 0.008), (0.047, 0.003)], n=18, y0=yc, z0=ez), M), "ember", sharp=60)
    w(gxf(ydome([(0.0, 0.004), (0.010, 0.003), (0.014, 0.0)], n=8, y0=yc + 0.0155, z0=ez, sx=0.55, sz=2.4), M), "blade_dark", sharp=40)
    rays = []
    for i in range(12):
        a = TAU * (i + 0.5) / 12
        ln = 0.120 if i % 2 else 0.078
        rays.append(conform(gxf(g_prism([(-0.012, 0.0), (0.012, 0.0), (0.0, ln)], 0.0060), T(0.082 * math.sin(a), 0.003, ez + 0.082 * math.cos(a)) @ R('Y', math.degrees(a))), yf))
    w(gxf(gjoin(*rays), M), "gilt", sharp=40)
    w(gxf(conform(gxf(g_prism([(-0.0075, 0.0), (0.0075, 0.0), (0.0, -0.420)], 0.0060), T(0, 0.0034, -0.110)), yf), M), "ember", sharp=30)
    chev = [conform(gxf(g_prism(chevron(0.105 - 0.022 * k, 0.030, 0.012), 0.0070), T(0, 0.004, -0.190 - 0.100 * k)), yf) for k in range(3)]
    w(gxf(gjoin(*chev), M), "gilt", bevel=0.0012, sharp=40)
    fin = [gxf(g_tooth(0.105, 0.0220, 0.040, 6), T(s * 0.150, float(yf(0.150, 0.250)) - 0.004, 0.262) @ R('Z', 90 - 90 * s) @ R('Y', -26)) for s in (1, -1)]
    w(gxf(gjoin(*fin), M), "gilt", sharp=50)
    rv = [(-0.175, 0.190), (0.175, 0.190), (-0.160, -0.050), (0.160, -0.050), (-0.108, -0.260), (0.108, -0.260), (-0.050, -0.440), (0.050, -0.440)]
    w(gxf(face_rivets(rv, yf, 0.0072), M), "gilt", sharp=60)
    shield_parts(w, shield_back(yf, S_BACK, S_OUT, 0.0, thick=0.0150), M)


@variant("focus", 3)
def focus_v3(w):
    # void gyre: an orb of violet light adrift in two gimbal rings over a silver crescent, shards circling it
    w(tube_w([(0, 0, -0.056), (0, 0, 0.056)], 0.0118, 10), "grip_black", sharp=60)
    w(grip_wrap(-0.046, 0.046, 0.0118, turns=7.5, width=0.0060, thick=0.0020, oval=1.0), "grip_black", sharp=70)
    w(grip_wrap(-0.044, 0.044, 0.0132, turns=5.0, width=0.0022, thick=0.0018, oval=1.0, hand=-1), "silver", sharp=70)
    w(rings((-0.052, 0.052), 0.0130, h=0.008, n=12, bulge=1.14), "silver", sharp=50)
    w(g_lathe([(0.0118, -0.056), (0.0165, -0.066), (0.0120, -0.082), (0.0050, -0.104), (0.0, -0.112)], n=10), "silver", sharp=40)
    w(g_lathe([(0.0118, 0.056), (0.0100, 0.068), (0.0160, 0.078), (0.0190, 0.086), (0.0100, 0.094), (0.0, 0.094)], n=12), "silver", sharp=40)
    oz = 0.176
    cr = [(0.010, 0, 0.088), (0.040, 0, 0.100), (0.064, 0, 0.134), (0.068, 0, 0.176), (0.056, 0, 0.212)]
    w(both_x(quillon(cr, [0.0070, 0.0066, 0.0056, 0.0042, 0.0010], n=8, flat=0.7)), "silver", sharp=50)
    w(gxf(g_sphere(0.0300, 16, 10), T(0, 0, oz)), "void", sharp=60)
    w(gxf(torus(0.0500, 0.0040, 24, 6), T(0, 0, oz) @ R('X', 90)), "silver", sharp=60)
    w(gxf(torus(0.0410, 0.0032, 22, 5), T(0, 0, oz) @ R('Z', 62) @ R('X', 58)), "starmetal", sharp=60)
    w(gjoin(*[gxf(g_sphere(0.0048, 6, 5), T(s * 0.0500, 0, oz)) for s in (1, -1)]), "silver", sharp=60)
    for k, (a, rr, dz, tl) in enumerate(((35, 0.074, 0.040, 20), (150, 0.070, -0.012, -30), (215, 0.078, 0.062, 40), (320, 0.066, 0.014, -15))):
        p = (rr * math.cos(math.radians(a)), rr * 0.45 * math.sin(math.radians(a)), oz + dz)
        w(gxf(shard(0.0052, 0.030, 5, seed=3 + k, skew=0.2), T(*p) @ R('Y', tl) @ T(0, 0, -0.015)), "void", sharp=20)


@variant("totem", 3)
def totem_v3(w):
    # elder totem: two spirit faces stacked in pale wood, eyes and rune lines alight with moss fire, wings, a great antler crown
    w(haft(-0.090, 0.090, 0.0150, 0.0170, n=10, seg=5, bulge=[(0, 1.12), (0.15, 1.0), (1, 1.0)]), "darkwood", sharp=60)
    w(grip_wrap(-0.060, 0.062, 0.0152, turns=6.5, width=0.0090, thick=0.0026, oval=1.0, gap=0.5), "hide", sharp=70)
    w(g_box(0.078, 0.058, 0.108, (0, 0, 0.146)), "palebox", bevel=0.011, bevel_seg=2, sharp=40)
    w(g_box(0.062, 0.050, 0.104, (0, 0, 0.256)), "palebox", bevel=0.010, bevel_seg=2, sharp=40)
    w(g_box(0.086, 0.064, 0.016, (0, 0, 0.202)), "darkbox", bevel=0.005, sharp=40)
    w(g_box(0.072, 0.058, 0.020, (0, 0, 0.312)), "palebox", bevel=0.007, sharp=40)
    for sgn in (1, -1):
        for (g_, src, kw) in totem_face(0.142, wx=0.076, dy=0.030, s=1.18, eye="oldbone", dark="moss", mouth="ebonybox"):
            w(gxf(g_, S(1, sgn, 1)), "palebox" if src == "woodbox" else src, **kw)
        for (g_, src, kw) in totem_face(0.258, wx=0.062, dy=0.026, s=0.98, eye="oldbone", dark="moss", mouth="ebonybox"):
            w(gxf(g_, S(1, sgn, 1)), "palebox" if src == "woodbox" else src, **kw)
    for sx_ in (1, -1):
        zig = [(sx_ * 0.0400, 0.0, 0.100 + 0.019 * i + 0.0) for i in range(6)]
        zig = [(x, (0.012 if i % 2 else -0.012), z) for i, (x, y, z) in enumerate(zig)]
        w(tube(zig, 0.0024, n=4), "moss", sharp=60)
        wing = plate([(0.036, 0.215), (0.082, 0.236), (0.122, 0.292), (0.096, 0.282), (0.104, 0.322), (0.074, 0.296), (0.070, 0.330), (0.036, 0.296)], 0.010)
        w(gxf(wing, S(sx_, 1, 1)), "palebox", bevel=0.0025, sharp=30)
    ant = gxf(g_antler(0.62, 4, 6), T(0.018, 0, 0.318) @ R('Z', -18))
    w(gjoin(ant, mirror_x_g(ant)), "oldbone", sharp=50)
    w(helix(0.0195, 0.076, 0.094, 3.2, 0.0050, 0.0044, n=5, spt=8), "rope", sharp=80)
    for sx_ in (1, -1):
        bp = [(sx_ * (0.030 + 0.004 * i), 0.014, 0.086 - 0.018 * i) for i in range(5)]
        w(tube(bp, 0.0012, n=4), "cord_dark", sharp=80)
        w(g_beads(bp[1:4], r=0.0064, nu=6, nv=4), "bone", sharp=60)
        w(gxf(g_gem(0.0058, 0.022, 6, 0.5), T(*bp[4])), "moss", sharp=20)
        w(gxf(g_feather(0.120, 0.032, 0.0028), T(sx_ * 0.030, -0.008, 0.088) @ R('Y', 180 + sx_ * 22) @ R('Z', -15 * sx_)), "feather_dark", sharp=40)


@variant("grimoire", 3)
def grimoire_v3(w):
    # pact grimoire: black hide boards chained and padlocked, horned fore-edge, a sigil and the page edges burning red
    bw, bh, t = 0.190, 0.265, 0.064
    X0 = BOOK_X0
    book_body(w, bw, bh, t, "tome_dark")
    book_bail(w, t, src="darksteel", grip_src="grip_black", z=0.092)
    book_corners(w, bw, bh, t, "darksteel", 0.046)
    w(g_box(0.0050, t * 0.62, bh - 0.040, (X0 + bw - 0.0045, 0, 0)), "pact", sharp=40)
    w(gjoin(*[g_box(0.011, t + 0.007, 0.014, (X0 + 0.003, 0, z)) for z in (-0.082, -0.030, 0.030, 0.082)]), "tome_dark", bevel=0.003, sharp=40)
    for sz_ in (1, -1):
        w(gxf(g_tooth(0.095, 0.0130, 0.040, 6), T(X0 + bw + 0.004, 0, sz_ * (bh / 2 - 0.020)) @ R('Y', 90 - 34 * sz_) @ R('Z', 90 + 90 * sz_)), "horn", sharp=40)
    m = 0.010
    for pos in (-0.070, 0.070):
        path = [(X0 - m, -t / 2 - m, pos), (X0 + bw + m, -t / 2 - m, pos), (X0 + bw + m, t / 2 + m, pos), (X0 - m, t / 2 + m, pos), (X0 - m, -t / 2 - m, pos)]
        w(g_chain(path, link=0.044, wire=0.0050, width=0.62, seg=6, n=4, up=(0.3, 1, 0.2)), "iron", sharp=60)
    w(g_box(0.022, 0.034, 0.060, (X0 + bw + 0.014, 0, 0)), "darksteel", bevel=0.0040, sharp=40)
    w(gxf(torus(0.0190, 0.0046, 12, 5, sx=0.8), T(X0 + bw + 0.014, 0, 0.036) @ R('Z', 90) @ R('X', 90)), "darksteel", sharp=60)
    w(both_y(gxf(g_gem(0.0060, 0.0070, 6, 0.75), T(X0 + bw + 0.014, 0.0180, -0.004) @ R('X', -90))), "pact", sharp=20)
    for sy_ in (1, -1):
        yy = sy_ * (t / 2 + 0.0030)
        cx = X0 + bw * 0.48
        w(gxf(g_tube(ring_path(0.056, 20, "xz"), 0.0046, n=4, closed=True), T(cx, yy, 0)), "pact", sharp=60)
        w(gxf(g_prism([(0.0, 0.044), (0.040, -0.026), (-0.040, -0.026), (0.0, 0.044), (0.0, 0.028), (-0.026, -0.018), (0.026, -0.018), (0.0, 0.028)], 0.0060), T(cx, yy, 0)),
          "pact", sharp=20)
        w(gxf(g_prism(rhomb(0.0070, 0.0150), 0.0070), T(cx, yy, 0.0)), "pact", sharp=20)
        w(gxf(g_lathe([(0.062, 0.0), (0.070, 0.0), (0.070, 0.0030), (0.062, 0.0030)], n=20), T(cx, sy_ * (t / 2 + 0.0010), 0) @ R('X', -90 * sy_)), "darksteel", sharp=40)


@variant("lantern", 3)
def lantern_v3(w):
    # reliquary lantern: a tall eight-sided gilt lantern with a spired roof and pinnacles, a shard of living flame in place of a candle
    H = np.array((0.104, 0.0, -0.038))
    lantern_yoke(w, "gilt", H, grip_src="grip_red")
    M = T(*H) @ HANG
    zb = lantern_body(w, "gilt", r=0.050, h=0.178, n=8, roof=1.55, flame=0.0, M=M, wax=None, ring=0.013)
    cz = zb + 0.178 * 0.52
    w(gxf(g_gem(0.0200, 0.110, 6, 0.55), M @ T(0, 0, cz)), "flame", sharp=20)
    w(gxf(g_gem(0.0090, 0.040, 4, 0.5), M @ T(0.020, 0.008, cz + 0.046)), "flame", sharp=20)
    w(gxf(g_gem(0.0075, 0.032, 4, 0.5), M @ T(-0.018, -0.010, cz - 0.040)), "flame", sharp=20)
    zt = zb + 0.178
    pin = [gxf(spike(0.0052, 0.026, 4, z0=0.0), M @ T(0.058 * math.cos(TAU * (i + 0.5) / 8), 0.058 * math.sin(TAU * (i + 0.5) / 8), zt + 0.004)) for i in range(8)]
    w(gjoin(*pin), "gilt", sharp=30)
    w(gxf(g_lathe([(0.0, zb - 0.044), (0.0100, zb - 0.054), (0.0060, zb - 0.070), (0.0, zb - 0.086)], n=6), M), "gilt", sharp=40)
    w(gxf(g_gem(0.0070, 0.026, 6, 0.5), M @ T(0, 0, zb - 0.098)), "flame", sharp=20)
