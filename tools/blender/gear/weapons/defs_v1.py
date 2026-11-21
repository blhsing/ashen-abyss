"""v1 — crude variants: scavenged iron, rough wood, rope, rag and bone. (sword1 v1 lives in defs_1h.)"""
from gearparts import *
from defs import variant
from defs_1h import nick
from defs_2h import limb, prod_path
from defs_off import (S_BACK, S_OUT, shield_parts, face_rivets, book_body, BOOK_X0, lantern_body, HANG)


# =============================================================================================== 1H
@variant("axe1", 1)
def axe1_v1(w):
    # hatchet: a rusty wedge head lashed to a crooked branch with rope, rag-wrapped grip
    st = slices(edge=[(0.292, 0.086), (0.325, 0.099), (0.365, 0.101), (0.400, 0.088)],
                back=[(0.292, 0.086), (0.310, 0.032), (0.316, -0.023), (0.378, -0.023), (0.384, 0.032), (0.400, 0.088)],
                thick=[(0.292, 0.004), (0.310, 0.013), (0.316, 0.037), (0.378, 0.037), (0.384, 0.013), (0.400, 0.004)], n=20)
    st = [(z, a, b - (0.0065 if abs(z - 0.349) < 0.004 else 0.0) - (0.0040 if abs(z - 0.326) < 0.004 else 0.0), t) for (z, a, b, t) in st]
    w(ribbon(st, sec_wedge(0.014, 0.40)), "rustiron", sharp=16, hem=60)
    w(haft(-0.125, 0.412, 0.0165, 0.0150, n=8, seg=9, wob=0.012, seed=7, oval=0.86, bulge=[(0, 1.22), (0.08, 1.0), (0.75, 0.98), (1, 1.10)]),
      "wood", sharp=60)
    w(rope_lash(0.258, 0.312, 0.0150, oval=0.90), "rope", sharp=80)
    for sy_ in (1, -1):
        w(tube([(-0.021, sy_ * 0.0212, 0.312), (0.006, sy_ * 0.0180, 0.348), (0.030, sy_ * 0.0150, 0.386)], 0.0026, n=5), "rope", sharp=80)
        w(tube([(0.030, sy_ * 0.0150, 0.310), (0.006, sy_ * 0.0186, 0.348), (-0.021, sy_ * 0.0212, 0.384)], 0.0026, n=5), "rope", sharp=80)
    w(rag_wrap(-0.066, 0.070, 0.0158, oval=0.86), "wrap", sharp=70)
    w(g_prism([(-0.010, 0.412), (0.010, 0.412), (0.006, 0.424), (-0.006, 0.424)], 0.0060), "rustiron", sharp=40)


@variant("mace1", 1)
def mace1_v1(w):
    # nail club: a knotted burl on a hand-cut haft, two rusty bands and a crown of long nails, cord grip and thong
    w(haft(-0.130, 0.290, 0.0150, 0.0240, n=8, seg=8, wob=0.008, seed=3, bulge=[(0, 1.18), (0.1, 0.94), (0.5, 0.92), (1, 1.0)]), "wood", sharp=60)
    head = lumpy(gxf(g_sphere(1.0, 12, 9), T(0, 0, 0.352) @ S(0.046, 0.044, 0.098)), 0.11, 36.0, 3, (0, 0, 0.352))
    w(head, "woodbox", sharp=60)
    w(gjoin(g_lathe([(0.0440, 0.318), (0.0472, 0.326), (0.0440, 0.334)], n=12), g_lathe([(0.0440, 0.384), (0.0470, 0.392), (0.0420, 0.400)], n=12)),
      "rustiron", sharp=50)
    nails = []
    for k, (zz, ph) in enumerate(((0.300, 0.0), (0.355, 0.5), (0.412, 0.0))):
        for i in range(5):
            a = TAU * (i + ph) / 5 + 0.3 * k
            d = np.array((math.cos(a), math.sin(a), 0.12 * (k - 1)))
            nails.append(gxf(spike(0.0052, 0.034, 4, z0=0.036), frame((0, 0, zz), d)))
    nails.append(gxf(spike(0.0060, 0.040, 4, z0=0.088), frame((0, 0, 0.352), (0.1, 0, 1))))
    w(gjoin(*nails), "rustiron", sharp=30, hem=60)
    w(rope_lash(-0.062, 0.066, 0.0142, d=0.0040), "cord_dark", sharp=80)
    w(gxf(torus(0.022, 0.0026, 12, 5, sx=0.55), T(0.004, 0, -0.150) @ R('X', 90)), "strap", sharp=60)


@variant("dagger", 1)
def dagger_v1(w):
    # shiv: a single-edged blade ground from a file, clipped point, no guard, cord-bound tang ending in a forged loop
    st = dense([(0.044, -0.0100, 0.0100, 0.0044), (0.060, -0.0125, 0.0150, 0.0044), (0.120, -0.0125, 0.0172, 0.0042),
                (0.185, -0.0120, 0.0158, 0.0038), (0.228, -0.0085, 0.0118, 0.0032), (0.266, 0.0030, 0.0030, 0.0)], 18)
    st = nick(nick(st, 0.150, 0.0030, 1, 0.005), 0.098, 0.0022, 1, 0.004)
    w(ribbon(st, sec_single(0.0085, 1.0)), "crudeblade", sharp=14, hem=60)
    w(g_box(0.0185, 0.0052, 0.110, (0.0, 0, -0.010)), "rustiron", bevel=0.0012, sharp=40)
    w(rope_lash(-0.050, 0.040, 0.0086, d=0.0034, oval=0.62), "cord_dark", sharp=80)
    w(gxf(torus(0.0115, 0.0030, 12, 5), T(0, 0, -0.074) @ R('X', 90)), "rustiron", sharp=60)
    w(tube([(0.004, 0.004, -0.050), (0.012, 0.007, -0.066), (0.016, 0.004, -0.090)], [0.0018, 0.0018, 0.0014], n=5), "cord_dark", sharp=80)


@variant("wand", 1)
def wand_v1(w):
    # hedge wand: a crooked twig forked at the tip, a raw quartz shard lashed into the fork, a bone bead and a feather on a cord
    P = smooth_path([(0, 0, -0.088), (0.003, 0.001, -0.020), (-0.004, 0.0, 0.060), (0.004, -0.002, 0.140), (0.0, 0.0, 0.212)], 14)
    w(tube_w(P, list(np.linspace(0.0108, 0.0064, 14)), 8), "bark", sharp=60)
    w(tube_w([(0.0, 0, 0.204), (0.0105, 0, 0.236), (0.0125, 0, 0.266)], [0.0056, 0.0046, 0.0026], 6), "bark", sharp=60)
    w(tube_w([(0.0, 0, 0.204), (-0.0115, 0, 0.230), (-0.0105, 0, 0.256)], [0.0054, 0.0044, 0.0026], 6), "bark", sharp=60)
    w(gxf(shard(0.0100, 0.074, 5, seed=3, skew=0.12), T(0.001, 0, 0.224)), "crystal_clear", sharp=20)
    w(rope_lash(0.198, 0.232, 0.0086, d=0.0030), "rope", sharp=80)
    w(rag_wrap(-0.066, 0.030, 0.0100, width=0.0100), "wrap", sharp=70)
    w(tube([(0.008, 0.0, 0.196), (0.016, 0.004, 0.180), (0.018, 0.004, 0.142)], 0.0010, n=4), "cord_dark", sharp=80)
    w(gxf(g_sphere(0.0058, 6, 5), T(0.018, 0.004, 0.150)), "bone", sharp=60)
    w(gxf(g_feather(0.060, 0.016, 0.0020), T(0.018, 0.004, 0.142) @ R('Y', 172)), "feather", sharp=40)


@variant("scythe1", 1)
def scythe1_v1(w):
    # reaping hook: a thin rusty crescent on a tang, driven into a plain turned handle
    P = smooth_path([(0.0, 0.205), (-0.014, 0.270), (0.004, 0.342), (0.058, 0.394), (0.130, 0.400), (0.190, 0.354), (0.214, 0.282), (0.204, 0.214)], 30)
    st = along(P, [(0.0, -0.0050, 0.0060, 0.0052), (0.12, -0.0075, 0.0115, 0.0050), (0.40, -0.0090, 0.0160, 0.0044),
                   (0.75, -0.0072, 0.0130, 0.0036), (0.94, -0.0030, 0.0060, 0.0028), (1.0, 0.0, 0.0, 0.0)])
    st = nick(nick(dense(st, 26), 0.26, 0.0040, 1, 0.006), 0.37, 0.0035, 1, 0.006)
    w(ribbon(st, sec_single(0.0090, 1.0), path=P), "crudeblade", sharp=14, hem=60)
    w(tube([(0, 0, 0.130), (0, 0, 0.212)], 0.0062, n=6), "rustiron", sharp=60)
    w(lathe_w([(0.0, -0.078), (0.0140, -0.074), (0.0170, -0.052), (0.0150, 0.0), (0.0160, 0.080), (0.0140, 0.134), (0.0, 0.138)], n=10), "wood", sharp=60)
    w(rings((0.128,), 0.0146, h=0.012, n=10, bulge=1.06), "rustiron", sharp=50)
    w(rope_lash(-0.040, 0.040, 0.0158, d=0.0040), "rope", sharp=80)


@variant("flail", 1)
def flail_v1(w):
    # threshing flail: an ash handle and a nail-studded swingle joined by iron staples and three links
    w(haft(-0.120, 0.300, 0.0148, 0.0132, n=8, seg=7, wob=0.008, seed=2, bulge=[(0, 1.16), (0.1, 1.0), (1, 1.0)]), "wood", sharp=60)
    w(rag_wrap(-0.064, 0.068, 0.0142), "wrap", sharp=70)
    w(rings((0.270,), 0.0140, h=0.014, n=10, bulge=1.06), "rustiron", sharp=50)
    w(gxf(torus(0.0105, 0.0030, 10, 5), T(0, 0, 0.3075) @ R('X', 90)), "rustiron", sharp=60)
    cp = [(0.0, 0, 0.314), (0.018, 0, 0.336), (0.046, 0, 0.338)]
    w(g_chain(cp, link=0.026, wire=0.0034, width=0.66, seg=6, n=4, up=(0, 1, 0)), "rustiron", sharp=60)
    A, B = np.array((0.058, 0.0, 0.333)), np.array((0.150, 0.0, 0.098))
    d = (B - A) / np.linalg.norm(B - A)
    w(gxf(torus(0.0100, 0.0030, 10, 5), T(*(A - d * 0.004)) @ R('X', 90)), "rustiron", sharp=60)
    w(tube_w([tuple(A + d * 0.006), tuple(A + d * 0.12), tuple(B)], [0.0170, 0.0205, 0.0235], 8), "wood", sharp=60)
    Mx = frame(tuple(A), tuple(d), (0, 1, 0))
    w(gxf(gjoin(g_lathe([(0.0178, 0.012), (0.0200, 0.020), (0.0182, 0.028)], n=10), g_lathe([(0.0232, 0.170), (0.0255, 0.180), (0.0238, 0.190)], n=10)), Mx),
      "rustiron", sharp=50)
    nails = [gxf(spike(0.0046, 0.024, 4, z0=0.021), Mx @ T(0, 0, 0.205 + 0.022 * (i % 2)) @ R('Z', 72.0 * i + 20) @ R('Y', 90)) for i in range(5)]
    nails += [gxf(spike(0.0046, 0.022, 4, z0=0.020), Mx @ T(0, 0, 0.150) @ R('Z', 90.0 * i) @ R('Y', 90)) for i in range(4)]
    w(gjoin(*nails), "rustiron", sharp=30, hem=60)


@variant("fist", 1)
def fist_v1(w):
    # knuckle-duster: a forged iron bow over the knuckles set with four pyramid studs, wooden cross handle bound in rag
    w(tube_w([(0, 0, -0.054), (0, 0, 0.054)], 0.0116, 10), "wood", sharp=60)
    w(rag_wrap(-0.042, 0.042, 0.0116, width=0.0105), "wrap", sharp=70)
    bow_ = smooth_path([(-0.004, 0, -0.058), (0.028, 0, -0.063), (0.050, 0, -0.044), (0.056, 0, 0.0), (0.050, 0, 0.044), (0.028, 0, 0.063),
                        (-0.004, 0, 0.058)], 19)
    w(g_tube(bow_, 0.0118, n=8, sy=0.36, up=(0, 1, 0)), "rustiron", sharp=50)
    w(gjoin(*[gxf(spike(0.0092, 0.024, 4, z0=0.0), frame((0.0575, 0, z), (1, 0, 0.12 * np.sign(z) * abs(z) / 0.036))) for z in (-0.036, -0.012, 0.012, 0.036)]),
      "rustiron", sharp=30, hem=60)
    for s in (1, -1):
        w(gxf(rope_lash(-0.012, 0.012, 0.0125, d=0.0038), T(0.006, 0, s * 0.0585) @ R('Y', 90)), "strap", sharp=80)
    w(both_y(gjoin(*[gxf(stud(0.0042, n=6), frame((0.034 + 0.016 * (1 - abs(z) / 0.05), sy_ * 0.0118, z), (0, sy_, 0))) for z in (-0.05, 0.05) for sy_ in (1,)])),
      "rustiron", sharp=60)


@variant("sceptre", 1)
def sceptre_v1(w):
    # hedge-priest's rod: a twisted bar of wrought iron, a basket of four prongs holding raw smoky crystals, cord grip, curled foot
    path = [(0.0, 0.0, z) for z in np.linspace(-0.120, 0.320, 26)]
    w(g_tube(path, 0.0098, n=4, twist=TAU * 3.5), "rustiron", sharp=30)
    w(rope_lash(-0.062, 0.062, 0.0092, d=0.0040), "cord_dark", sharp=80)
    w(tube(smooth_path([(0, 0, -0.118), (0.004, 0, -0.140), (0.020, 0, -0.152), (0.032, 0, -0.140), (0.028, 0, -0.124), (0.018, 0, -0.124)], 12),
           [0.0070] * 6 + [0.0060, 0.0052, 0.0044, 0.0036, 0.0028, 0.0020], n=5), "rustiron", sharp=50)
    prong = tube(smooth_path([(0.006, 0, 0.300), (0.022, 0, 0.328), (0.030, 0, 0.366), (0.024, 0, 0.402), (0.012, 0, 0.420)], 9),
                 list(np.linspace(0.0046, 0.0016, 9)), n=5)
    w(radial(prong, 4, 45), "rustiron", sharp=50)
    w(rings((0.306,), 0.0110, h=0.012, n=8, bulge=1.25), "rustiron", sharp=40)
    w(gxf(shard(0.0170, 0.118, 6, seed=5, skew=0.08), T(0, 0, 0.318)), "crystal_smoke", sharp=20)
    w(gxf(shard(0.0105, 0.070, 5, seed=8, skew=0.10), T(0.010, 0.004, 0.322) @ R('Y', 24)), "crystal_smoke", sharp=20)
    w(gxf(shard(0.0095, 0.060, 5, seed=11, skew=0.10), T(-0.008, -0.006, 0.322) @ R('Y', -26) @ R('X', 14)), "crystal_clear", sharp=20)
    w(helix(0.0150, 0.326, 0.352, 3.0, 0.0018, 0.0018, n=4, spt=10), "copper", sharp=70)


# =============================================================================================== 2H
@variant("sword2", 1)
def sword2_v1(w):
    # butcher's greatsword: a broad bar of pitted iron with a clipped tip, bar guard, rope-bound grip, ring pommel
    st = dense([(0.095, -0.0440, 0.0440, 0.0088), (0.160, -0.0465, 0.0465, 0.0086), (0.600, -0.0475, 0.0480, 0.0076),
                (1.000, -0.0490, 0.0500, 0.0066), (1.080, -0.0490, 0.0500, 0.0062)], 16)
    st += [(1.125, -0.0490, 0.0330, 0.0058), (1.175, -0.0490, -0.0060, 0.0052), (1.215, -0.0460, -0.0460, 0.0)]
    for z, d, s in ((0.32, 0.008, 1), (0.47, 0.005, -1), (0.63, 0.010, 1), (0.71, 0.005, 1), (0.88, 0.007, -1), (0.96, 0.006, 1)):
        st = nick(st, z, d, s, 0.010)
    w(ribbon(st, sec_hex(0.0115)), "crudeblade", sharp=14, hem=60)
    w(g_box(0.200, 0.0300, 0.0270, (0, 0, 0.0820)), "rustiron", bevel=0.0045, sharp=40)
    w(both_y(gjoin(*[gxf(stud(0.0052, n=6), frame((x, 0.0150, 0.082), (0, 1, 0))) for x in (-0.060, 0.060)])), "rustiron", sharp=60)
    w(grip_core(-0.255, 0.070, 0.0168, n=8, swell=1.0, taper=1.0, oval=0.88), "wood", sharp=60)
    w(rope_lash(-0.245, 0.060, 0.0166, d=0.0064, oval=0.88), "rope", sharp=80)
    w(g_lathe([(0.0150, -0.255), (0.0190, -0.262), (0.0120, -0.272), (0.0, -0.274)], n=8), "rustiron", sharp=40)
    w(gxf(torus(0.0270, 0.0075, 14, 6), T(0, 0, -0.302) @ R('X', 90)), "rustiron", sharp=60)


@variant("axe2", 1)
def axe2_v1(w):
    # scrap cleaver: a broad slab cut from a plough-share, slotted through a thick crooked haft, strapped, riveted and roped
    st = slices(edge=[(0.490, 0.160), (0.540, 0.222), (0.640, 0.252), (0.760, 0.250), (0.850, 0.226), (0.905, 0.176)],
                back=[(0.490, -0.030), (0.850, -0.030), (0.905, 0.022)],
                thick=[(0.490, 0.0150), (0.905, 0.0130)], n=34)
    chips = ((0.585, 0.014, 0.012), (0.668, 0.008, 0.008), (0.742, 0.016, 0.013), (0.822, 0.007, 0.008))
    st = [(z, a, b - sum(d * max(0.0, 1 - abs(z - zc) / wd) for zc, d, wd in chips), t) for (z, a, b, t) in st]
    w(ribbon(st, sec_wedge(0.026, 0.55)), "rustiron", sharp=16, hem=60)
    w(gxf(g_lathe([(0.0, -0.0056), (0.0120, -0.0056), (0.0120, 0.0056), (0.0, 0.0056)], n=10), T(0.128, 0, 0.850) @ R('X', 90)), "ebonybox", sharp=40)
    w(haft(-0.500, 0.965, 0.0205, 0.0185, n=8, seg=14, wob=0.016, seed=5, oval=0.90, bulge=[(0, 1.14), (0.05, 1.0), (0.9, 1.0), (1, 1.10)]),
      "wood", sharp=60)
    for zz in (0.548, 0.842):
        w(g_box(0.128, 0.0500, 0.0260, (0.030, 0, zz)), "rustiron", bevel=0.0040, sharp=40)
        w(both_y(gjoin(*[gxf(stud(0.0062, n=6), frame((x, 0.0250, zz), (0, 1, 0))) for x in (0.050, 0.080)])), "rustiron", sharp=60)
    w(rope_lash(0.430, 0.488, 0.0200, d=0.0062, oval=0.92), "rope", sharp=80)
    w(rope_lash(0.908, 0.945, 0.0192, d=0.0062, oval=0.92), "rope", sharp=80)
    w(rag_wrap(-0.076, 0.084, 0.0196, oval=0.90), "wrap", sharp=70)
    w(rag_wrap(-0.420, -0.290, 0.0202, oval=0.90, hand=-1), "wrap", sharp=70)


@variant("mace2", 1)
def mace2_v1(w):
    # stone maul: a rough-hewn block of grey stone bound to a thick haft with iron straps and rope
    hz = 0.770
    w(gxf(boulder(0.100, 0.066, 0.074, amp=0.09, freq=34.0, seed=4), T(0.004, 0, hz)), "stone", sharp=50)
    w(haft(-0.480, hz + 0.092, 0.0195, 0.0180, n=8, seg=14, wob=0.012, seed=9, bulge=[(0, 1.14), (0.05, 1.0), (1, 1.0)]), "wood", sharp=60)
    for x in (-0.050, 0.056):
        w(gxf(torus(0.074, 0.0050, 14, 4, sx=0.92, sy=1.02), T(x, 0, hz) @ R('Y', 90)), "rustiron", sharp=50)
    for a in (38, -38):
        w(gxf(torus(0.081, 0.0038, 14, 5, sx=1.26, sy=0.88), T(0.004, 0, hz) @ R('X', 90) @ R('Z', a)), "rope", sharp=80)
    w(rope_lash(hz - 0.160, hz - 0.082, 0.0186, d=0.0058), "rope", sharp=80)
    w(rag_wrap(-0.076, 0.084, 0.0186), "wrap", sharp=70)
    w(rag_wrap(-0.400, -0.270, 0.0192, hand=-1), "wrap", sharp=70)
    w(rings((-0.470,), 0.0205, h=0.016, n=8, bulge=1.05), "rustiron", sharp=50)


@variant("staff", 1)
def staff_v1(w):
    # hermit's staff: a gnarled branch forked at the top, a raw crystal bound into the fork, bone and feather charms
    w(tube_w(gnarl(zline(-0.860, 0.800, 13), 0.014, 5), list(np.linspace(0.0190, 0.0165, 13)), 8), "wood", sharp=60)
    w(tube_w(smooth_path([(0.0, 0, 0.790), (0.026, 0, 0.862), (0.056, 0, 0.946), (0.052, 0, 1.024)], 9), list(np.linspace(0.0150, 0.0062, 9)), 8), "wood", sharp=60)
    w(tube_w(smooth_path([(0.0, 0, 0.790), (-0.034, 0, 0.856), (-0.062, 0, 0.928), (-0.046, 0, 0.990)], 9), list(np.linspace(0.0140, 0.0058, 9)), 8), "wood", sharp=60)
    w(gxf(shard(0.0260, 0.150, 6, seed=2, skew=0.10), T(-0.002, 0, 0.842)), "crystal_clear", sharp=20)
    for zz, rr, tl in ((0.872, 0.040, 12), (0.905, 0.052, -9)):
        w(gxf(torus(rr, 0.0034, 14, 5, sx=1.0, sy=0.50), T(-0.003, 0, zz) @ R('Y', tl)), "rope", sharp=80)
    w(rope_lash(0.760, 0.800, 0.0178, d=0.0050), "rope", sharp=80)
    w(rag_wrap(-0.084, 0.104, 0.0180), "wrap", sharp=70)
    w(tube([(-0.046, 0.0, 0.980), (-0.060, 0.004, 0.940), (-0.064, 0.004, 0.850)], 0.0012, n=4), "cord_dark", sharp=80)
    w(gxf(g_bone(0.060, 0.0050, 1.7), T(-0.064, 0.004, 0.850) @ R('Y', 176)), "bone", sharp=50)
    w(gxf(g_feather(0.110, 0.028, 0.0026), T(-0.062, 0.004, 0.900) @ R('Y', 168) @ R('Z', 20)), "feather_dark", sharp=40)
    w(tube([(0.052, 0.0, 1.016), (0.062, -0.004, 0.980), (0.064, -0.004, 0.930)], 0.0012, n=4), "cord_dark", sharp=80)
    w(g_beads([(0.064, -0.004, 0.930 - 0.016 * i) for i in range(3)], r=0.0064, nu=6, nv=4), "bone", sharp=60)


@variant("polearm", 1)
def polearm_v1(w):
    # bill: a heavy knife blade nailed and lashed to the split end of a pole
    st = dense([(0.640, -0.0100, 0.0100, 0.0070), (0.740, -0.0150, 0.0150, 0.0070), (0.790, -0.0165, 0.0420, 0.0070),
                (0.950, -0.0165, 0.0540, 0.0064), (1.100, -0.0150, 0.0480, 0.0054), (1.180, -0.0060, 0.0320, 0.0044),
                (1.228, 0.0120, 0.0120, 0.0)], 28)
    st = nick(nick(nick(st, 0.90, 0.007, 1, 0.009), 1.02, 0.005, 1, 0.008), 0.83, 0.004, 1, 0.007)
    w(ribbon(st, sec_single(0.0150, 1.0)), "crudeblade", sharp=14, hem=60)
    w(haft(-0.800, 0.740, 0.0185, 0.0170, n=8, seg=16, wob=0.016, seed=6, oval=0.9, bulge=[(0, 1.08), (0.05, 1.0), (1, 1.0)]), "wood", sharp=60)
    w(rope_lash(0.612, 0.700, 0.0170, d=0.0056, oval=0.92), "rope", sharp=80)
    w(rings((0.592, 0.722), 0.0178, h=0.012, n=8, bulge=1.05), "rustiron", sharp=50)
    w(both_y(rivet_row((0.652, 0.676), 0.0160, (0, 1, 0), 0.0046)), "rustiron", sharp=60)
    w(rag_wrap(-0.084, 0.104, 0.0178, oval=0.9), "wrap", sharp=70)
    w(rag_wrap(-0.500, -0.360, 0.0182, oval=0.9, hand=-1), "wrap", sharp=70)


@variant("scythe2", 1)
def scythe2_v1(w):
    # mowing scythe: a thin rusty blade ringed to a bent snath with two wooden nibs
    sn = smooth_path([(0.0, 0, -0.860), (0.014, 0, -0.420), (-0.016, 0, 0.060), (-0.004, 0, 0.520), (0.026, 0, 0.870)], 22)
    w(tube_w(sn, list(np.linspace(0.0185, 0.0165, 22)), 8), "wood", sharp=60)
    for zz, ln in ((-0.300, 0.105), (0.250, 0.095)):
        x0 = float(np.interp(zz, sn[:, 2], sn[:, 0]))
        w(tube_w([(x0, 0, zz), (x0 + ln * 0.6, 0, zz + 0.012), (x0 + ln, 0, zz + 0.022)], [0.0130, 0.0125, 0.0150], 8), "wood", sharp=60)
        w(gxf(rope_lash(-0.014, 0.014, 0.0188, d=0.0044), T(x0, 0, zz)), "rope", sharp=80)
    P = smooth_path([(0.030, 0.878), (0.120, 0.914), (0.280, 0.908), (0.440, 0.850), (0.565, 0.748), (0.640, 0.628)], 32)
    st = along(P, [(0.0, -0.010, 0.016, 0.0050), (0.10, -0.012, 0.030, 0.0050), (0.40, -0.011, 0.028, 0.0044), (0.72, -0.008, 0.020, 0.0036),
                   (0.92, -0.004, 0.009, 0.0028), (1.0, 0.0, 0.0, 0.0)])
    st = nick(nick(nick(dense(st, 28), 0.21, 0.006, 1, 0.008), 0.36, 0.004, 1, 0.007), 0.50, 0.005, 1, 0.008)
    w(ribbon(st, sec_single(0.0120, 1.0), path=P), "crudeblade", sharp=14, hem=60)
    w(g_box(0.060, 0.0100, 0.022, (0.036, 0, 0.872)), "rustiron", bevel=0.0020, sharp=40)
    w(gjoin(g_lathe([(0.0180, 0.842), (0.0205, 0.852), (0.0180, 0.862)], n=10), g_lathe([(0.0180, 0.800), (0.0200, 0.808), (0.0180, 0.816)], n=10)),
      "rustiron", M=T(0.022, 0, 0), sharp=50)
    w(rag_wrap(-0.084, 0.104, 0.0178), "wrap", sharp=70)


@variant("bow", 1)
def bow_v1(w):
    # self bow: a single stave of pale wood with knots left in, rope string, rag grip
    up = [(0.010, 0.060), (0.004, 0.220), (-0.030, 0.430), (-0.086, 0.600), (-0.128, 0.678)]
    for s in (1, -1):
        pts = [(x, s * z) for x, z in up]
        w(limb(pts, 0.0205, 0.0112, n=8, width=1.0, thick=0.72, seg=18), "palewood", sharp=60)
        w(gxf(rope_lash(-0.010, 0.010, 0.0082, d=0.0030), T(pts[-1][0] + 0.006, 0, pts[-1][1] - s * 0.018) @ R('Y', -32 * s)), "rope", sharp=80)
        w(gxf(g_sphere(1.0, 6, 5), T(-0.012, 0.002, s * 0.335) @ S(0.0180, 0.0200, 0.0260)), "palewood", sharp=60)
    w(tube_w([(0.010, 0, -0.064), (0.012, 0, 0.0), (0.010, 0, 0.064)], [0.0176, 0.0186, 0.0176], 8, up=(0, 1, 0), sx=1.0, sy=0.84), "palewood", sharp=60)
    w(gxf(rag_wrap(-0.056, 0.056, 0.0180, oval=0.84), T(0.011, 0, 0) @ R('Z', 90)), "wrap", sharp=70)
    t0, t1 = (up[-1][0] + 0.004, 0.0, up[-1][1] - 0.014), (up[-1][0] + 0.004, 0.0, -up[-1][1] + 0.014)
    w(tube([t1, (t1[0], 0, -0.03), (t0[0], 0, 0.03), t0], 0.0022, n=5), "rope", sharp=80)


@variant("crossbow", 1)
def crossbow_v1(w):
    # poacher's crossbow: plank tiller with a whittled handle, a stout wooden prod lashed on with rope, rope string, iron tickler
    zt = 0.094
    til = g_prism([(-0.270, 0.046), (-0.270, 0.098), (0.400, zt), (0.400, 0.058), (0.110, 0.052), (-0.050, 0.052), (-0.180, 0.042)], 0.0400)
    w(til, "woodbox", bevel=0.0040, sharp=40)
    w(tube_w([(0.006, 0, 0.056), (0.0, 0, 0.0), (-0.014, 0, -0.058), (-0.020, 0, -0.078)], [0.0150, 0.0162, 0.0166, 0.0180], 8, up=(0, 1, 0), sx=0.86, sy=1.0),
      "wood", sharp=60)
    w(gxf(rag_wrap(-0.046, 0.038, 0.0164, oval=0.86, width=0.0105), T(-0.004, 0, 0) @ R('Y', -9)), "wrap", sharp=70)
    w(tube([(0.030, 0, 0.050), (0.058, 0, 0.030), (0.112, 0, 0.022)], [0.0046, 0.0040, 0.0032], n=5), "rustiron", sharp=50)
    pp = prod_path(0.300, 0.088, 0.376, 0.076, 13)
    P = np.array(pp)
    rad = [0.0165 - 0.0078 * abs(t) for t in np.linspace(-1, 1, 13)]
    w(tube_w(P, rad, 8, up=(0, 0, 1), sx=1.15, sy=0.86), "wood", sharp=60)
    for sy_ in (1, -1):
        w(gxf(rope_lash(-0.016, 0.016, 0.0300, d=0.0052), T(0.372, sy_ * 0.012, 0.076) @ R('Z', 90) @ R('X', 90) @ R('Y', 26 * sy_)), "rope", sharp=80)
    w(gxf(rope_lash(-0.020, 0.020, 0.0310, d=0.0052, oval=0.82), T(0.372, 0, 0.076) @ R('Y', 90)), "rope", sharp=80)
    w(gxf(g_lathe([(0.0, -0.016), (0.0120, -0.016), (0.0120, 0.016), (0.0, 0.016)], n=8), T(0.080, 0, zt + 0.006) @ R('X', 90)), "woodbox", sharp=40)
    w(tube([pp[0], (0.084, 0, zt + 0.012), pp[-1]], 0.0032, n=5), "rope", sharp=80)
    w(gxf(g_lathe([(0.0, 0.0), (0.0055, 0.0), (0.0055, 0.290), (0.0, 0.290)], n=6), T(0.090, 0, zt + 0.010) @ R('Y', 90)), "wood", sharp=60)
    w(gxf(g_lathe([(0.0, 0.0), (0.0100, 0.006), (0.0, 0.050)], n=4), T(0.378, 0, zt + 0.010) @ R('Y', 90)), "rustiron", sharp=30, hem=60)
    w(gjoin(*[gxf(g_prism([(0.0, 0.0), (0.052, 0.0), (0.044, 0.014), (0.008, 0.018)], 0.0016), T(0.094, 0, zt + 0.012) @ R('X', a)) for a in (35, 145)]),
      "feather", sharp=40)


# =============================================================================================== off-hand
def plank(x0, x1, zfn, yfn, nu=3, nv=9):
    """one board of a plank shield (board space): grid over x0..x1, z from -zfn(x) to +zfn(x)"""
    def fn(U, V):
        x = x1 + (x0 - x1) * U
        zz = zfn(x)
        z = zz[0] + (zz[1] - zz[0]) * V
        return np.stack([x, yfn(x, z), z], axis=-1)
    return g_grid(fn, nu, nv, close_u=False)


@variant("shield", 1)
def shield_v1(w):
    # plank roundshield: five warped boards, a domed iron boss, two riveted iron straps, a split in the rim, hide enarmes
    M = T(0.085, S_OUT, -0.030)
    Rr = 0.280
    yf = shield_y(Rr, 0.030, 2 * Rr, 0.0, vdepth=0.030)
    edges = [-Rr, -0.168, -0.058, 0.060, 0.170, Rr]
    for i in range(5):
        x0, x1 = edges[i] + 0.0022, edges[i + 1] - 0.0022

        def zfn(x, i=i):
            zz = np.sqrt(np.clip(Rr ** 2 - np.asarray(x) ** 2, 0.0, None))
            top = zz * (0.86 if i == 4 else 1.0) - (0.030 if i == 1 else 0.0)
            return (-zz + (0.012 if i == 3 else 0.0), np.maximum(top, -zz + 0.02))
        x0c, x1c = max(x0, -Rr + 0.006), min(x1, Rr - 0.006)
        w(gxf(plank(x0c, x1c, zfn, yf, nu=4, nv=11), M), "woodbox", recalc=False, solidify=0.014, solid_offset=-1.0, sharp=40)
    w(gxf(ydome([(0.092, 0.0), (0.092, 0.006), (0.074, 0.009), (0.062, 0.026), (0.036, 0.048), (0.0, 0.056)], n=14, y0=float(yf(0, 0)), z0=0.0), M),
      "rustiron", sharp=40)
    w(gxf(face_rivets([(0.083 * math.cos(TAU * i / 8), 0.083 * math.sin(TAU * i / 8)) for i in range(8)], lambda x, z: yf(x, z) + 0.006, 0.0062), M),
      "rustiron", sharp=60)
    w(gxf(gjoin(face_strip(-0.225, 0.225, 0.140, 0.172, yf, n=10), face_strip(-0.215, 0.215, -0.182, -0.150, yf, n=10)), M), "rustiron",
      recalc=False, solidify=0.0042, solid_offset=-1.0, sharp=40)
    w(gxf(face_rivets([(x, z) for z in (0.156, -0.166) for x in (-0.19, -0.115, 0.115, 0.19)], lambda x, z: yf(x, z) + 0.0036, 0.0062), M),
      "rustiron", sharp=60)
    shield_parts(w, shield_back(yf, S_BACK, S_OUT, -0.030, thick=0.0150, src="hide"), M)


@variant("focus", 1)
def focus_v1(w):
    # hedge focus: a fist of raw amethyst on its matrix rock, bound with hide thongs to a length of old bone
    w(gxf(g_bone(0.150, 0.0112, 1.55, n=8), T(0, 0, -0.082)), "oldbone", sharp=50)
    w(rag_wrap(-0.046, 0.046, 0.0116, width=0.0095), "hide", sharp=70)
    w(gxf(boulder(0.030, 0.027, 0.022, amp=0.12, freq=60.0, seed=3), T(0, 0, 0.086)), "stone", sharp=50)
    spec = [(0, 0, 0.0, 0.0200, 0.110, 2), (0.015, 0.004, 28, 0.0130, 0.078, 5), (-0.014, 0.008, -30, 0.0125, 0.070, 7),
            (0.002, -0.016, 0, 0.0115, 0.066, 9), (-0.004, 0.018, 0, 0.0100, 0.052, 12)]
    sh = []
    for k, (x, y, ty, r, h, sd) in enumerate(spec):
        tx = (-26 if k == 3 else (24 if k == 4 else 0))
        sh.append(gxf(shard(r, h, 6, seed=sd, skew=0.08), T(x, y, 0.086) @ R('Y', ty) @ R('X', tx)))
    w(gjoin(*sh), "crystal_violet", sharp=20)
    for a in (40, -40):
        w(gxf(torus(0.031, 0.0026, 12, 5, sx=1.0, sy=0.92), T(0, 0, 0.084) @ R('X', 90) @ R('Z', a) @ R('X', 20)), "cord_dark", sharp=80)
    w(rope_lash(0.056, 0.074, 0.0150, d=0.0036), "cord_dark", sharp=80)


@variant("totem", 1)
def totem_v1(w):
    # fetish stick: a small horned beast skull tied to a crooked branch, crow feathers and knucklebones on twine
    w(tube_w(gnarl(zline(-0.095, 0.215, 9), 0.006, 4), list(np.linspace(0.0150, 0.0120, 9)), 8), "bark", sharp=60)
    w(rag_wrap(-0.060, 0.060, 0.0146, width=0.0105), "hide", sharp=70)
    Ms = T(-0.004, 0, 0.250) @ R('Y', -8)
    for g_, kind in animal_skull(1.0):
        w(gxf(g_, Ms), "oldbone" if kind == 'bone' else "ebonybox", sharp=50)
    for sy_ in (1, -1):
        w(gxf(g_tooth(0.085, 0.0090, 0.030, 6), Ms @ T(-0.012, sy_ * 0.020, 0.018) @ R('X', -22 * sy_) @ R('Y', -28) @ R('Z', 180)), "horn", sharp=50)
    w(rope_lash(0.198, 0.228, 0.0130, d=0.0040), "rope", sharp=80)
    for a in (35, -35):
        w(gxf(torus(0.034, 0.0022, 12, 5, sx=1.0, sy=0.80), T(-0.004, 0, 0.243) @ R('X', 90) @ R('Z', a)), "rope", sharp=80)
    for k, (sx_, sy_, ln, src) in enumerate(((1, 1, 0.105, "feather_dark"), (-1, 1, 0.090, "feather_dark"), (1, -1, 0.080, "feather"))):
        w(gxf(g_feather(ln, 0.028, 0.0026), T(sx_ * 0.016, sy_ * 0.010, 0.205) @ R('Y', 180 + sx_ * 20) @ R('Z', 25 * sy_ * sx_)), src, sharp=40)
    bp = [(-0.018 - 0.003 * i, -0.010, 0.200 - 0.019 * i) for i in range(4)]
    w(tube(bp, 0.0012, n=4), "cord_dark", sharp=80)
    w(gjoin(*[gxf(g_bone(0.014, 0.0030, 1.9, n=5), T(*p) @ R('Y', 180)) for p in bp[1:]]), "bone", sharp=50)


@variant("grimoire", 1)
def grimoire_v1(w):
    # traveller's journal: a limp brown cover tied shut with a strap, loose leaves and a quill poking out, a rope carrying loop
    bw, bh, t = 0.150, 0.205, 0.040
    X0 = BOOK_X0
    book_body(w, bw, bh, t, "tome_brown", ct=0.0050, bevel=0.0022)
    w(tube(smooth_path([(X0 + 0.006, 0, -0.070), (0.022, 0, -0.072), (0.0, 0, -0.050), (0.0, 0, 0.050), (0.022, 0, 0.072), (X0 + 0.006, 0, 0.070)], 16),
           0.0062, n=6), "rope", sharp=80)
    for z in (-0.070, 0.070):
        w(gxf(torus(0.0090, 0.0030, 8, 5), T(X0 + 0.004, 0, z) @ R('X', 90)), "rustiron", sharp=60)
    w(g_box(bw + 0.006, t + 0.0060, 0.020, (X0 + bw / 2, 0, 0.012)), "strap", bevel=0.0015, sharp=40)
    w(g_box(0.020, t + 0.0060, bh + 0.006, (X0 + bw * 0.62, 0, 0)), "strap", bevel=0.0015, sharp=40)
    w(g_box(0.026, 0.0050, 0.026, (X0 + bw * 0.62, t / 2 + 0.0050, 0.012)), "rustiron", bevel=0.0015, sharp=40)
    pg = [gxf(g_box(bw * 0.90, 0.0012, bh * 0.86), T(X0 + bw * 0.56 + dx, dy, dz) @ R('Y', a)) for (dx, dy, dz, a) in
          ((0.012, 0.006, 0.006, 3.0), (0.008, -0.004, -0.008, -4.0), (0.016, 0.001, 0.002, 6.0))]
    w(gjoin(*pg), "paper", sharp=40)
    w(gxf(g_feather(0.150, 0.026, 0.0024), T(X0 + bw * 0.30, 0.004, bh / 2 - 0.035) @ R('Y', -14)), "feather", sharp=40)
    w(gjoin(*[gxf(rope_lash(-0.006, 0.006, t / 2 + 0.004, d=0.0030, oval=0.14), T(X0 + 0.002, 0, z) @ R('Z', 90)) for z in (-0.060, -0.020, 0.020, 0.060)]),
      "cord_dark", sharp=80)
    w(gxf(torus(0.0110, 0.0030, 10, 5), T(X0 + bw * 0.62, t / 2 + 0.0082, 0.012) @ R('X', 90)), "rustiron", sharp=60)
    w(gjoin(*[gxf(g_box(0.012, 0.0030, 0.050), T(X0 + bw * 0.62 + dx, t / 2 + 0.0070, -0.020) @ R('Y', a)) for dx, a in ((-0.006, 12), (0.007, -16))]),
      "strap", bevel=0.0008, sharp=40)
    w(gjoin(*[gxf(g_corner(0.030, 0.0030), T(cx, sy_ * (t / 2 + 0.0012), cz) @ R('Y', ry)) for sy_ in (1, -1)
              for (cx, cz, ry) in ((X0 + bw, -bh / 2, -90), (X0 + bw, bh / 2, 180))]), "tome_brown", sharp=30)


@variant("lantern", 1)
def lantern_v1(w):
    # tinker's lantern: a square cage of rusty strap iron with a flat cap, a stub of candle, a wire bail on a wooden grip
    H = np.array((0.086, 0.0, -0.031))
    w(tube_w([(0, 0, -0.056), (0, 0, 0.056)], 0.0118, 8), "wood", sharp=60)
    w(rope_lash(-0.030, 0.030, 0.0120, d=0.0040), "rope", sharp=80)
    for s in (1, -1):
        w(tube(smooth_path([(0.0, 0, s * 0.056), (0.010, 0, s * 0.068), (H[0] * 0.55, 0, H[2] + s * 0.046), (H[0] * 0.94, 0, H[2] + s * 0.010), tuple(H)], 10),
               0.0034, n=5), "rustiron", sharp=60)
    lantern_body(w, "rustiron", r=0.040, h=0.112, n=4, roof=0.55, flame=0.75, M=T(*H) @ HANG @ R('Z', 45), finial=False)
