"""Jewels and gems (they have no body mount: models for the 3D icons and for loot lying on the ground).

Modelled about 4.5 x life size (a ring is ~0.11 m across) so they are visible as ground loot and their baked textures hold
detail; the manifest says so (display_scale). Orientation matches the weapons' "face" view: the showy side faces +Y, so
an unrotated model lies flat on the ground and an icon camera looks along -Y.
  ring    hole axis = Y, stone at +Z
  amulet  pendant in the XZ plane, cord / chain loop towards +Z
  gem     table (top) faces +Y
"""
from gearparts import *
from defs import base

JW = dict(mount="none", grip="none", hands=0, display_scale=4.5)


def band(Rr, width, thick, n=28, m=8):
    """ring band in the XZ plane (hole axis Y): width along Y, thickness radial"""
    return g_tube(ring_path(Rr, n, "xz"), 1.0, n=m, closed=True, sx=width / 2, sy=thick / 2, up=(0, 1, 0))


def ylathe(profile, n=16, z=0.0, x=0.0, y=0.0, sx=1.0, sz=1.0, rot=0.0):
    """lathe whose axis points along +Y (profile [(r, height along Y)]), centred at (x, y, z)"""
    return gxf(g_lathe(profile, n=n), T(x, y, z) @ R('X', -90) @ S(sx, sz, 1.0) @ R('Z', rot))


def loop_path(z0, z1, half_w, n=22):
    """teardrop loop in the XZ plane from (0, z0) up round (0, z1) and back (cord / chain of a pendant)"""
    out = []
    for i in range(n + 1):
        t = i / n
        a = TAU * t
        k = math.sin(PI * t) ** 0.8
        out.append((half_w * math.sin(a) * (0.35 + 0.65 * k), 0.0, z0 + (z1 - z0) * (0.5 - 0.5 * math.cos(a))))
    return out


# =============================================================================================== rings
@base("ring", info=JW, prefix="jwl", kind="jewel", texel=0.0005, min_size=512, cap=512, sheet=dict(icon_roll=0.0))
def ring(w, v):
    Rr = 0.046
    if v == 1:
        # iron band: a hammered hoop with a square bezel holding a chip of old bone scratched with a rune
        w(band(Rr, 0.022, 0.0090, 30, 8), "iron", sharp=50)
        w(g_box(0.030, 0.026, 0.014, (0, 0, Rr + 0.0085)), "iron", bevel=0.0030, sharp=40)
        w(g_box(0.020, 0.017, 0.006, (0, 0, Rr + 0.0175)), "oldbone", bevel=0.0018, sharp=40)
        w(gjoin(g_box(0.0022, 0.012, 0.0016, (0.0, 0, Rr + 0.0208)), gxf(g_box(0.0022, 0.008, 0.0016), T(0.003, 0.002, Rr + 0.0208) @ R('Z', 50)),
                gxf(g_box(0.0022, 0.008, 0.0016), T(-0.003, 0.002, Rr + 0.0208) @ R('Z', -50))), "ebonybox", sharp=40)
        w(gjoin(*[gxf(stud(0.0032, n=6), frame((sx_ * 0.0125, sy_ * 0.0105, Rr + 0.0155), (0, 0, 1))) for sx_ in (1, -1) for sy_ in (1, -1)]), "iron", sharp=60)
    elif v == 2:
        # silver ring: tapered shoulders, a beaded bezel and a sapphire cabochon
        w(band(Rr, 0.015, 0.0075, 32, 8), "silver", sharp=50)
        for s in (1, -1):
            a = math.radians(90 - s * 34)
            p = ((Rr + 0.0030) * math.cos(a), 0.0, (Rr + 0.0030) * math.sin(a))
            w(gxf(g_lathe([(0.0090, -0.004), (0.0072, 0.004), (0.0040, 0.008), (0.0, 0.009)], n=8, sy=1.5), frame(p, (math.cos(a), 0, math.sin(a)))), "silver", sharp=40)
        w(g_lathe([(0.0130, Rr + 0.0010), (0.0215, Rr + 0.0050), (0.0225, Rr + 0.0130), (0.0190, Rr + 0.0160), (0.0, Rr + 0.0160)], n=16), "silver", sharp=40)
        w(gxf(g_sphere(1.0, 14, 8), T(0, 0, Rr + 0.0155) @ S(0.0182, 0.0182, 0.0115)), "gem_sapphire", sharp=60)
        w(gjoin(*[gxf(g_sphere(0.0030, 6, 4), T(0.0230 * math.cos(TAU * i / 14), 0.0230 * math.sin(TAU * i / 14), Rr + 0.0060)) for i in range(14)]),
          "silver", sharp=60)
        w(gjoin(band(Rr, 0.0024, 0.0090, 32, 5), ), "silver", M=T(0, 0.0086, 0), sharp=60)
        w(gjoin(band(Rr, 0.0024, 0.0090, 32, 5), ), "silver", M=T(0, -0.0086, 0), sharp=60)
    else:
        # ember signet: a heavy gilt hoop with corded borders, six claws round a burning stone, a ruby on each shoulder
        w(band(Rr, 0.020, 0.0090, 32, 8), "gilt", sharp=50)
        for sy_ in (1, -1):
            w(gjoin(*[gxf(g_sphere(0.0026, 6, 4), T((Rr + 0.0048) * math.cos(TAU * i / 40), sy_ * 0.0092, (Rr + 0.0048) * math.sin(TAU * i / 40))) for i in range(40)
                      if math.sin(TAU * i / 40) < 0.80]), "gilt", sharp=60)
        w(g_lathe([(0.0120, Rr + 0.0010), (0.0180, Rr + 0.0050), (0.0200, Rr + 0.0100), (0.0150, Rr + 0.0130), (0.0, Rr + 0.0130)], n=12), "gilt", sharp=40)
        claw = g_tube([(0.0180, 0, Rr + 0.0070), (0.0235, 0, Rr + 0.0180), (0.0225, 0, Rr + 0.0290), (0.0170, 0, Rr + 0.0350)], [0.0030, 0.0028, 0.0022, 0.0008], n=5)
        w(radial(claw, 6, 30), "gilt", sharp=50)
        w(gxf(g_gem(0.0185, 0.0400, 8, 0.50), T(0, 0, Rr + 0.0310)), "ember", sharp=20, heat=0.50)
        for s in (1, -1):
            a = math.radians(90 - s * 44)
            p = ((Rr + 0.0052) * math.cos(a), 0.0, (Rr + 0.0052) * math.sin(a))
            w(gxf(g_gem(0.0068, 0.0090, 8, 0.6), frame(p, (math.cos(a), 0, math.sin(a)))), "gem_ruby", sharp=20)
            w(gxf(g_lathe([(0.0086, -0.001), (0.0096, 0.002), (0.0074, 0.004)], n=8), frame(p, (math.cos(a), 0, math.sin(a)))), "gilt", sharp=40)


# =============================================================================================== amulets
@base("amulet", info=JW, prefix="jwl", kind="jewel", texel=0.0005, min_size=512, cap=512, sheet=dict(icon_roll=0.0))
def amulet(w, v):
    if v == 1:
        # fang charm: a big yellowed tooth bound in twine on a leather cord, knucklebone and wooden beads
        w(gxf(g_tooth(0.092, 0.0165, 0.026, 8), T(0, 0, 0.006) @ R('X', 180)), "oldbone", sharp=50)
        w(g_lathe([(0.0, 0.016), (0.0120, 0.012), (0.0172, 0.002), (0.0160, -0.006)], n=8), "oldbone", sharp=50)
        w(rope_lash(-0.010, 0.010, 0.0170, d=0.0036), "rope", sharp=80)
        lp = loop_path(0.014, 0.150, 0.050, 26)
        w(tube(lp, 0.0030, n=6), "cord_dark", sharp=80)
        for i in (3, 23):
            w(gxf(g_bone(0.016, 0.0036, 1.9, n=5), frame(lp[i], np.array(lp[i + 1]) - np.array(lp[i - 1]))), "bone", sharp=50)
        for i in (5, 21):
            w(gxf(g_sphere(0.0066, 8, 6), T(*lp[i])), "darkbox", sharp=60)
        w(gxf(torus(0.0075, 0.0022, 10, 5), T(0, 0, 0.018) @ R('Y', 90)), "rustiron", sharp=60)
    elif v == 2:
        # silver medallion: a rimmed disc cut with a four-point star, a sapphire boss on each face, bail and fine chain
        w(ylathe([(0.0, -0.0040), (0.0320, -0.0040), (0.0370, -0.0022), (0.0370, 0.0022), (0.0320, 0.0040), (0.0, 0.0040)], n=24), "silver", sharp=40)
        w(gxf(torus(0.0345, 0.0034, 24, 6), R('X', 90)), "silver", sharp=60)
        w(g_prism(star4(0.0270, 0.30), 0.0125), "silver", bevel=0.0012, sharp=30)
        for sy_ in (1, -1):
            w(gxf(g_sphere(1.0, 12, 6), T(0, sy_ * 0.0060, 0) @ S(0.0098, 0.0058, 0.0098)), "gem_sapphire", sharp=60)
            w(gxf(torus(0.0105, 0.0020, 12, 5), T(0, sy_ * 0.0056, 0) @ R('X', 90)), "silver", sharp=60)
        w(g_lathe([(0.0046, 0.035), (0.0070, 0.040), (0.0046, 0.045)], n=8), "silver", sharp=50)
        w(gxf(torus(0.0085, 0.0026, 12, 5), T(0, 0, 0.0520) @ R('Y', 90)), "silver", sharp=60)
        w(g_chain(loop_path(0.058, 0.170, 0.050, 28), link=0.0130, wire=0.0019, width=0.66, seg=6, n=3, up=(0, 1, 0)), "silver", sharp=60)
    else:
        # ember sun: a burning stone in a gilt corona of twelve rays, crescent drop, fleur bail and a gold chain with ruby beads
        for sy_ in (1, -1):
            w(gxf(g_gem(0.0170, 0.0180, 8, 0.75), T(0, sy_ * 0.0030, 0) @ R('X', -90 * sy_)), "ember", sharp=20, heat=0.55)
        w(ylathe([(0.0165, -0.0050), (0.0230, -0.0050), (0.0250, 0.0), (0.0230, 0.0050), (0.0165, 0.0050), (0.0150, 0.0)], n=20), "gilt", sharp=40)
        rays = []
        for i in range(12):
            a = TAU * i / 12
            ln = 0.034 if i % 2 == 0 else 0.020
            rays.append(gxf(g_prism([(-0.0062, 0.0), (0.0062, 0.0), (0.0, ln)], 0.0060), T(0.0235 * math.sin(a), 0, 0.0235 * math.cos(a)) @ R('Y', math.degrees(a))))
        w(gjoin(*rays[1:]), "gilt", bevel=0.0008, sharp=30)
        w(plate([(x, -z - 0.076) for x, z in crescent(0.0200, 0.0160, 0.0068, 10)], 0.0056), "gilt", bevel=0.0010, sharp=30)
        w(gxf(g_gem(0.0052, 0.016, 6, 0.5), T(0, 0, -0.050)), "ember", sharp=20)
        w(g_prism([(-0.0090, 0.022), (0.0090, 0.022), (0.0140, 0.040), (0.0050, 0.036), (0.0, 0.052), (-0.0050, 0.036), (-0.0140, 0.040)], 0.0060), "gilt",
          bevel=0.0010, sharp=30)
        w(gxf(torus(0.0085, 0.0026, 12, 5), T(0, 0, 0.0590) @ R('Y', 90)), "gilt", sharp=60)
        lp = loop_path(0.066, 0.180, 0.052, 30)
        w(g_chain(lp, link=0.0125, wire=0.0019, width=0.66, seg=6, n=3, up=(0, 1, 0)), "gilt", sharp=60)
        for i in (6, 24):
            w(gxf(g_sphere(0.0062, 8, 6), T(*lp[i])), "gem_ruby", sharp=60)


# =============================================================================================== gems
GEMS = {1: "ruby", 2: "sapphire", 3: "topaz", 4: "emerald", 5: "amethyst", 6: "skull"}


def cut_gem(r, crown=0.30, pav=0.62, table=0.58, n=8, sx=1.0, sz=1.0, rot=0.0, girdle=0.05, turned=True, mod=None):
    """Faceted stone, table towards +Y. Returns ({tone: geometry}, inner-light plate, table height, table radius).
    turned: alternate rings are turned half a facet (brilliant cut: star and kite facets); otherwise straight tiers (step
    cut). mod(angle) -> radial scale shapes the outline (trillion ...). Facets are grouped in three tones (0 dark, 1 mid,
    2 light) so the cut is baked into the texture and reads at icon size."""
    g = r * girdle
    half = 180.0 / n if turned else 0.0
    ht = g + r * crown
    rings_ = [(0.0, -g - r * pav, 0.0), (r * 0.54, -g - r * pav * 0.42, half), (r, -g, 0.0), (r, g, 0.0),
              (r * (1 + table) / 2 * (1.02 if turned else 1.0), g + r * crown * 0.56, half), (r * table, ht, 0.0), (0.0, ht, 0.0)]
    verts, rows = [], []
    for (rr, h, turn) in rings_:
        if rr <= 1e-7:
            rows.append([len(verts)])
            verts.append((0.0, 0.0, h))
        else:
            row = []
            for i in range(n):
                a_ = math.radians(turn) + TAU * i / n
                k = mod(a_) if mod else 1.0
                row.append(len(verts))
                verts.append((rr * k * math.cos(a_), rr * k * math.sin(a_), h))
            rows.append(row)
    faces, tn = [], []
    for j in range(len(rows) - 1):
        ra, rb = rows[j], rows[j + 1]
        ta, tb = rings_[j][2], rings_[j + 1][2]
        for i in range(n):
            i2, e = (i + 1) % n, i % 2
            if len(ra) == 1:
                faces.append((ra[0], rb[i2], rb[i]))
                tn.append((0, 1)[e])
            elif len(rb) == 1:
                faces.append((ra[i], ra[i2], rb[0]))
                tn.append(1)
            elif abs(ta - tb) < 1e-6:
                faces.append((ra[i], ra[i2], rb[i2], rb[i]))
                tn.append(1 if j == 2 else ((1, 0)[e] if j < 2 else ((1, 0)[e] if j == 3 else (2, 1)[e])))
            elif tb > ta:
                faces.append((ra[i], ra[i2], rb[i]))
                tn.append((2, 1)[e])
                faces.append((ra[i2], rb[i2], rb[i]))
                tn.append(0)
            else:
                faces.append((ra[i], rb[i], rb[(i - 1) % n]))
                tn.append(2 if j >= 3 else (1, 2)[e])
                faces.append((ra[i], ra[i2], rb[i]))
                tn.append((1, 0)[e] if j >= 3 else 0)
    M = R('X', -90) @ S(sx, sz, 1.0) @ R('Z', rot)
    body = geo(np.array(verts), faces)
    parts = {t: gxf(g_, M) for t, g_ in by_tone(body, lambda fi, c: tn[fi]).items()}
    tv = np.array([verts[i] for i in rows[-2]])
    gv = [tuple(p * np.array((0.52, 0.52, 1.0)) + np.array((0, 0, 0.0006))) for p in tv]
    gv += [tuple(p * np.array((0.44, 0.44, 1.0)) - np.array((0, 0, 0.0030))) for p in tv] + [(0.0, 0.0, ht + 0.0006)]
    gf = [(i, (i + 1) % n, 2 * n) for i in range(n)] + [(i, n + i, n + (i + 1) % n, (i + 1) % n) for i in range(n)]
    glow = gxf(geo(np.array(gv), gf), M)
    return parts, glow, ht, r * table


def stone(w, key, glow_key, heat, **kw):
    """cut stone = toned facets (baked) + a small inner light on the table (emissive) + one baked glint"""
    parts, glow, ht, rt = cut_gem(**kw)
    add_tones(w, parts, key, recalc=False)
    w(glow, glow_key, sharp=30, heat=heat)
    w(glint(kw["r"] * 0.15, ht + 0.0014, x=rt * 0.42 * kw.get("sx", 1.0), z=rt * 0.42 * kw.get("sz", 1.0)), "gem_glint", sharp=30)


@base("gem", info=JW, prefix="gem", kind="gem", variants=(1, 2, 3, 4, 5, 6), texel=0.0005, min_size=512, cap=512, tex_name="gem_atlas",
      ids={k: "gem_" + n for k, n in GEMS.items()}, entry_base=GEMS, sheet=dict(icon_roll=0.0))
def gem(w, v):
    if v == 1:        # ruby: round brilliant
        stone(w, "gem_ruby", "ruby", 0.34, r=0.046, crown=0.34, pav=0.70, table=0.54, n=8)
    elif v == 2:      # sapphire: long oval brilliant, ten-sided
        stone(w, "gem_sapphire", "sapphire", 0.40, r=0.038, crown=0.36, pav=0.72, table=0.56, n=10, sz=1.42)
    elif v == 3:      # topaz: trillion (three-sided brilliant, clipped corners)
        stone(w, "gem_topaz", "topaz", 0.30, r=0.050, crown=0.30, pav=0.58, table=0.50, n=6, rot=-90.0,
              mod=lambda a_: 1.0 + 0.26 * math.cos(3 * a_))
    elif v == 4:      # emerald: step-cut rectangle with clipped corners
        stone(w, "gem_emerald", "emerald", 0.34, r=0.050, crown=0.26, pav=0.52, table=0.58, n=8, sx=0.80, sz=1.18, rot=22.5, turned=False)
    elif v == 5:      # amethyst: a raw double-ended crystal with two smaller points grown from its side
        Mm = T(0, 0.004, -0.058) @ R('Z', 30)
        add_tones(w, sector_tones(crystal(0.026, 0.118, 6, top=0.30, bot=0.24, girdle=0.5), 6, pattern=(1, 2), z_split=0.118 * 0.24), "gem_amethyst", M=Mm, recalc=False)
        add_tones(w, sector_tones(crystal(0.014, 0.060, 6, top=0.36, bot=0.10), 6, pattern=(2, 0)), "gem_amethyst", M=T(0.014, 0.0, -0.030) @ R('Y', 48), recalc=False)
        add_tones(w, sector_tones(crystal(0.011, 0.044, 6, top=0.36, bot=0.10), 6, pattern=(1, 0)), "gem_amethyst", M=T(-0.012, 0.0, -0.012) @ R('Y', -58), recalc=False)
        w(gxf(g_gem(0.0080, 0.062, 4, 0.5), T(0, 0.0250, 0.002) @ S(1.0, 0.22, 1.0)), "amethyst", sharp=30, heat=0.40)
        w(glint(0.0070, 0.0268, x=0.010, z=0.030), "gem_glint", sharp=30)
    else:             # skull 亡者顱骨: a small yellowed skull, a cold light deep in the sockets
        for g_, kind in human_skull(1.55):
            if kind == 'dark':
                w(g_, "ebonybox", sharp=40)
            elif kind == 'nose':
                w(g_, "ebonybox", sharp=40)
            else:
                w(g_, "bone_clean", sharp=50)
        sk = 1.55
        w(gjoin(*[gxf(g_sphere(0.0017 * sk, 6, 4), T(sx_ * 0.0122 * sk, 0.0286 * sk, -0.0045 * sk)) for sx_ in (1, -1)]), "bonefire", sharp=60, heat=0.30)
