"""Item icons: weapons (from the heroes' own weapon meshes), armour tiers, jewellery, gems, uniques."""
import bpy, math, random
from mathutils import Vector, Euler
import iconlib as I
from iconlib import R
from registry import icon, reg

EMBER = (1.0, 0.42, 0.08)
CLASS_COL = dict(greatsword=(1.0, 0.45, 0.10), staff=(0.40, 0.65, 1.0), crossbow=(0.20, 0.90, 0.75), scythe=(0.62, 0.95, 0.62),
                 club=(0.50, 0.95, 0.30), gauntlet=(1.0, 0.52, 0.12), swordshield=(1.0, 0.80, 0.35), grimoire=(0.95, 0.22, 0.38))
WPN = dict(greatsword=("hero_emberblade", False), staff=("hero_astromancer", False), crossbow=("hero_hunter", True),
           scythe=("hero_soulbinder", False), club=("hero_wildsoul", False), gauntlet=("hero_forgebody", True),
           swordshield=("hero_lampknight", True), grimoire=("hero_pactbinder", True))


# ------------------------------------------------------------------------------------------------ weapons
def finish(o, tier, col):
    """tier finishes on the baked hero materials: 3 = bright steel, no glow; 4 = ash-forged (black, ember seams);
    5 = ember-crowned (warm metal, strong glow in the class colour)"""
    for i, m in enumerate(o.data.materials):
        if m is None:
            continue
        nm = m.name.split("_")[0]
        if tier <= 2:
            if nm == 'emis':
                o.data.materials[i] = I.tint_material(m, "t12", emit=col, emit_strength=0.25)
            continue
        if nm == 'emis':
            e = {3: (col, 0.5), 4: (EMBER, 3.2), 5: (col, 5.0)}[tier]
            o.data.materials[i] = I.tint_material(m, "t%d" % tier, emit=e[0], emit_strength=e[1])
        elif nm == 'metal':
            if tier == 4:
                o.data.materials[i] = I.tint_material(m, "t4", mul=(0.22, 0.22, 0.26), rough_add=0.12)
            elif tier == 5:
                o.data.materials[i] = I.tint_material(m, "t5", mul=(1.25, 1.02, 0.72), rough_add=-0.08)
        elif tier == 4 and nm in ('leather', 'cloth'):
            o.data.materials[i] = I.tint_material(m, "t4", mul=(0.45, 0.42, 0.42))


def weapon_parts(kind, tier):
    char, has_off = WPN[kind]
    part = "wpn_%d" % min(tier, 3)
    main = I.weapon(char, part)
    off = I.weapon(char, part + "_off") if has_off else None
    col = CLASS_COL[kind]
    for o in (main, off):
        if o is not None:
            finish(o, tier, col)
    return main, off


WIDEN = dict(greatsword=(1.75, 0.95, 0.0), staff=(2.3, 0.80, 0.006), scythe=(1.8, 0.82, 0.005), club=(1.5, 1.0, 0.0))   # widen, length, inflate


def glow_bits(kind, tier, main):
    col = CLASS_COL[kind]
    L = main.get("length", 1.0)
    if tier >= 4:
        c = EMBER if tier == 4 else col
        I.sparks(5 if tier == 4 else 9, (L * 0.02, -0.06, L * 0.05), (L * 0.36, -0.02, L * 0.42), r=L * 0.008, col=c, seed=tier)


def build_weapon(kind, tier):
    main, off = weapon_parts(kind, tier)
    L = main.get("length", 1.0)
    if kind in WIDEN:
        w, zs, inf = WIDEN[kind]
        if inf:
            I.inflate(main, inf)
        I.place(main, rot=(-8, 42, 0), scale=(w, w, zs))
    elif kind == "crossbow":
        I.inflate(main, 0.007)
        I.place(main, rot=(-14, 40, 0))
        if off is not None:
            bpy.data.objects.remove(off, do_unlink=True)
    elif kind == "swordshield":
        sl = off.get("length", 1.0)
        I.place(off, loc=(-L * 0.05, 0.06, 0), rot=(-8, -10, 0))
        k = sl * 1.3 / L
        I.place(main, loc=(sl * 0.16, -0.12, 0), rot=(-6, 38, 0), scale=(k * 1.5, k * 1.5, k))
    elif kind == "gauntlet":
        I.place(main, loc=(L * 0.40, 0, 0), rot=(-10, 14, 0))
        if off is not None:
            I.place(off, loc=(-L * 0.40, 0.06, L * 0.05), rot=(-10, -14, 0))
    elif kind == "grimoire":
        bl = off.get("length", 0.3) if off is not None else 0.3
        if off is not None:
            I.place(off, rot=(-14, -10, 0))
        k = bl * 1.45 / L
        I.place(main, loc=(bl * 0.30, -0.12, 0), rot=(-6, 44, 0), scale=(k * 1.6, k * 1.6, k))
    bpy.context.view_layer.update()
    glow_bits(kind, tier, main)
    return dict(fill=0.84)


for _k in WPN:
    for _t in range(1, 6):
        reg("wpn_%s_%d" % (_k, _t), "item", (lambda k=_k, t=_t: build_weapon(k, t)))


# ------------------------------------------------------------------------------------------------ armour: helms
def _dark_inside(r=0.09, loc=(0, 0, 0.02), squash=(1, 1, 1.1)):
    m = I.emit((0, 0, 0), 0.0, key="void_black")
    o = I.ball(r, m, "inside", 3, squash); o.location = loc
    return o


def _rivets(n, rad, z, mat, r=0.0045, a0=0.0, a1=360.0):
    out = []
    for i in range(n):
        a = R(a0 + (a1 - a0) * (i + 0.5) / n)
        o = I.ball(r, mat, "rivet", 2); o.location = (rad * math.cos(a), rad * math.sin(a), z)
        out.append(o)
    return out


def helm_shell(mat, h=0.135, r=0.108, thick=0.004, low=-0.035):
    prof = [(0.0, h), (r * 0.35, h * 0.965), (r * 0.66, h * 0.83), (r * 0.88, h * 0.58), (r * 0.985, h * 0.26), (r, 0.0), (r * 1.01, low)]
    o = I.lathe(prof, 48, mat, "helm_shell")
    I.solid(o, thick)
    return o


@icon("helm_3")
def helm_3():
    """守望者頭盔: steel sallet with a riveted brow band, a visor with an eye slit and a low crest"""
    st = I.steel(); br = I.brass()
    parts = [helm_shell(st, low=0.002)]
    parts.append(_dark_inside(0.100, (0, 0, -0.01), (1, 1, 1.05)))
    brow = I.lathe([(0.111, 0.004), (0.114, 0.012), (0.114, 0.030), (0.111, 0.038)], 48, br, "brow"); I.solid(brow, 0.003); parts.append(brow)
    # visor: lower face guard (front 170 degrees) leaving an eye slit under the brow band
    vis = I.lathe([(0.112, -0.020), (0.116, -0.045), (0.112, -0.085), (0.098, -0.118), (0.080, -0.128)], 36, st, "visor", arc=170.0)
    I.solid(vis, 0.004); vis.rotation_euler = (0, 0, R(185)); parts.append(vis)
    # neck guard at the back
    tail = I.lathe([(0.110, -0.030), (0.118, -0.070), (0.135, -0.105)], 36, st, "tail", arc=190.0)
    I.solid(tail, 0.004); tail.rotation_euler = (0, 0, R(-5)); parts.append(tail)
    crest = I.tube(I.arc_pts(0.112, 20, 160, 20, plane='YZ', c=(0, 0, 0.02)), 0.007, 8, br, "crest"); parts.append(crest)
    parts += _rivets(14, 0.116, 0.021, st, 0.0042)
    parts += _rivets(5, 0.117, -0.062, br, 0.004, 215, 325)
    g = I.group(parts); I.place(g, rot=(0, 0, 32))
    return dict(fill=0.80)


# ------------------------------------------------------------------------------------------------ armour: chests
def torso(mat, w=1.0, thick=0.005, name="torso", power=2.5):
    S = [((0, 0, 0.00), 0.150 * w, 0.108), ((0, 0, 0.09), 0.158 * w, 0.115), ((0, -0.006, 0.20), 0.186 * w, 0.132),
         ((0, -0.004, 0.30), 0.190 * w, 0.126), ((0, 0, 0.365), 0.150 * w, 0.100), ((0, 0.004, 0.40), 0.088, 0.078), ((0, 0.006, 0.425), 0.074, 0.070)]
    ax = (Vector((1, 0, 0)), Vector((0, 1, 0)))
    o = I.loft([(c, rx, ry, ax, power) for (c, rx, ry) in S], 40, mat, name)
    I.solid(o, thick)
    return o


def pauldron(mat, side=1, r=0.085, z=0.335, x=0.205, lames=3, trim=None):
    out = []
    for k in range(lames):
        s = 1.0 - 0.13 * k
        o = I.lathe([(0.0, r * 0.62), (r * 0.5, r * 0.55), (r * 0.9, r * 0.25), (r, 0.0)], 28, mat, "pauldron")
        I.solid(o, 0.004)
        o.scale = (s * 1.05, s * 0.95, s * 0.8)
        o.location = (side * (x + 0.012 * k), 0, z - 0.035 * k)
        o.rotation_euler = (0, R(side * (28 + 16 * k)), 0)
        out.append(o)
        if trim is not None and k == 0:
            t = I.torus(r * 0.99, 0.0045, trim, "ptrim", 36, 8)
            t.scale = o.scale; t.location = o.location; t.rotation_euler = o.rotation_euler
            out.append(t)
    return out


@icon("chest_4")
def chest_4():
    """灰鑄板甲: ash-forged black cuirass, layered pauldrons, ember light in the seams"""
    ash = I.ashplate(); seam = I.emit(EMBER, 4.0)
    parts = [torso(ash), _dark_inside(0.07, (0, 0.004, 0.40), (1, 1, 0.5))]
    for s in (-1, 1):
        parts += pauldron(ash, s, trim=I.iron())
    # breast ridge + glowing seams
    parts.append(I.tube([(0, -0.139, 0.10), (0, -0.146, 0.20), (0, -0.136, 0.30), (0, -0.108, 0.36)], 0.0075, 8, ash, "ridge"))
    for s in (-1, 1):
        parts.append(I.tube([(s * 0.012, -0.141, 0.11), (s * 0.016, -0.149, 0.20), (s * 0.014, -0.139, 0.30)], 0.0022, 6, seam, "seam"))
        parts.append(I.tube([(s * 0.03, -0.128, 0.345), (s * 0.10, -0.108, 0.325), (s * 0.165, -0.06, 0.30)], 0.0022, 6, seam, "seam2"))
    # fauld: two hoops of plate under the waist
    for k in range(2):
        f = I.lathe([(0.152 + 0.012 * k, 0.0 - 0.045 * k), (0.166 + 0.014 * k, -0.05 - 0.045 * k)], 40, ash, "fauld"); I.solid(f, 0.004)
        f.scale = (1.0, 0.74, 1.0); parts.append(f)
    belt = I.lathe([(0.153, -0.004), (0.157, 0.004), (0.157, 0.022), (0.153, 0.03)], 40, I.leather(), "belt"); belt.scale = (1.0, 0.735, 1.0); parts.append(belt)
    parts.append(I.ball(0.017, I.ember(EMBER, 4, 60, 0.3), "core", 3, (1, 0.5, 1)))
    parts[-1].location = (0, -0.143, 0.235)
    g = I.group(parts); I.place(g, rot=(0, 0, 26))
    return dict(fill=0.82)


# ------------------------------------------------------------------------------------------------ jewellery
def band(mat, Rm=0.0105, r=0.0017, flat=1.0):
    o = I.torus(Rm, r, mat, "band", 64, 12)
    o.scale = (1, 1, flat)
    return o


def claws(mat, n=4, r=0.0042, h=0.006, z=0.0, wire=0.0006):
    out = []
    for i in range(n):
        a = 2 * math.pi * (i + 0.5) / n
        p0 = Vector((r * 0.55 * math.cos(a), r * 0.55 * math.sin(a), z))
        p1 = Vector((r * math.cos(a), r * math.sin(a), z + h * 0.6))
        p2 = Vector((r * 0.72 * math.cos(a), r * 0.72 * math.sin(a), z + h))
        out.append(I.tube([p0, p1, p2], wire, 6, mat, "claw"))
    return out


def ring_pose(parts, tilt=62, yaw=28):
    """ring axis along Y would hide the band: tip it forward so the hoop reads as an ellipse with the bezel on top"""
    g = I.group(parts)
    I.place(g, rot=(tilt, 0, yaw))
    return g


@icon("ring_4")
def ring_4():
    """燼晶指環: gold hoop, claw setting, a glowing ember crystal"""
    gd = I.gold()
    parts = [band(gd, 0.0105, 0.0019)]
    # bezel on the +X side of the hoop (hoop lies in XY): build along local Z then lay it outward
    bez = [I.cone(0.0032, 0.0046, 0.004, gd, "bezel", 20)] + claws(gd, 6, 0.0046, 0.0062, 0.003)
    st = I.crystal(0.0041, 0.0115, 6, 0.42, I.gem((1.0, 0.36, 0.06), 1.8, glow=1.2), "stone", base_tip=0.3)
    st.location = (0, 0, 0.0025)
    b = I.group(bez + [st]); I.place(b, loc=(0, -0.0118, 0), rot=(90, 0, 0))
    for s in (-1, 1):
        parts.append(I.ball(0.0019, gd, "shoulder", 2)); parts[-1].location = (s * 0.0052, -0.0102, 0)
    g = I.group(parts + [b]); I.place(g, rot=(-62, 0, 24))
    return dict(fill=0.78)


# ------------------------------------------------------------------------------------------------ gems
GEM_COL = dict(ruby=(0.95, 0.06, 0.05), sapphire=(0.08, 0.25, 1.0), topaz=(1.0, 0.62, 0.05), emerald=(0.05, 0.8, 0.18),
               amethyst=(0.55, 0.12, 0.95), skull=(0.85, 0.83, 0.75))


def cut_round(r=0.012, sides=8, table=0.55, crown=0.32, pav=0.78):
    pts = []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        pts.append((r * table * math.cos(a), r * table * math.sin(a), r * crown))
        a2 = a + math.pi / sides
        pts.append((r * math.cos(a2), r * math.sin(a2), 0.0))
        pts.append((r * math.cos(a2), r * math.sin(a2), -r * 0.06))
    pts.append((0, 0, -r * pav))
    return pts


def cut_step(rx=0.012, ry=0.009, crown=0.3, pav=0.7, corner=0.25):
    pts = []
    for (k, z) in ((0.5, crown), (0.78, crown * 0.55), (1.0, 0.0), (1.0, -0.05), (0.62, -pav * 0.55), (0.2, -pav)):
        x, y = rx * k, ry * k; c = min(x, y) * corner * 2
        for sx in (-1, 1):
            for sy in (-1, 1):
                pts.append((sx * (x - c), sy * y, rx * z)); pts.append((sx * x, sy * (y - c), rx * z))
    return pts


def build_gem(kind, tier):
    col = GEM_COL[kind]
    glow = (0.0, 0.15, 0.7)[tier - 1]
    m = I.gem(col, 1.72, glow=glow, rough=(0.22, 0.0, 0.0)[tier - 1])
    if tier == 1:           # chipped: a rough broken shard
        o = I.shard(0.011, m, "gem", seed=hash(kind) % 97 + 3, n=11, squash=(1.0, 0.8, 1.25))
        I.place(o, rot=(20, 15, 30))
    else:
        if kind == "ruby":
            pts = cut_round(0.012, 8)
        elif kind == "sapphire":
            pts = cut_round(0.012, 6, table=0.5, crown=0.42, pav=0.9)
        elif kind == "topaz":
            pts = cut_round(0.0125, 3, table=0.5, crown=0.34, pav=0.7)
        elif kind == "emerald":
            pts = cut_step(0.0125, 0.0095)
        else:
            pts = cut_round(0.012, 4, table=0.42, crown=0.4, pav=1.05)
        o = I.hull(pts, m, "gem")
        if kind == "amethyst":
            o.scale = (0.72, 1.25, 1.0)
        I.place(o, rot=(58, 0, 22 if kind != "emerald" else 35))
        if tier == 3:
            o.scale = tuple(s * 1.0 for s in o.scale)
            I.sparks(7, (-0.014, -0.012, -0.012), (0.014, -0.004, 0.014), r=0.0006, col=tuple(min(1.0, c + 0.4) for c in col), seed=5, strength=9)
    return dict(fill=(0.62, 0.74, 0.84)[tier - 1], samples=64)


for _k in ("ruby", "sapphire", "topaz", "emerald", "amethyst"):
    for _t in (1, 2, 3):
        reg("gem_%s_%d" % (_k, _t), "item", (lambda k=_k, t=_t: build_gem(k, t)))


def build_skull(tier):
    """亡者顱骨: the skull of the game's own skeleton soldier (mon_skeleton); 1 stained and dull, 2 clean, 3 eyes alight"""
    o = I.head_of("mon_skeleton")
    for i, m in enumerate(o.data.materials):
        if m is None:
            continue
        if 'emis' in m.name:
            o.data.materials[i] = I.tint_material(m, "sk%d" % tier, emit=(0.55, 1.0, 0.7) if tier == 3 else (0.0, 0.0, 0.0), emit_strength=(0.0, 0.0, 5.0)[tier - 1])
        elif tier == 1:
            o.data.materials[i] = I.tint_material(m, "sk1", mul=(0.50, 0.44, 0.38), rough_add=0.1)
    I.place(o, rot=(0, 0, 28 if tier != 1 else -24))
    bpy.context.view_layer.update()
    if tier == 3:
        I.sparks(7, (-0.10, -0.14, -0.06), (0.12, -0.10, 0.12), r=0.004, col=(0.6, 1.0, 0.75), seed=9)
    return dict(fill=(0.66, 0.76, 0.86)[tier - 1])


for _t in (1, 2, 3):
    reg("gem_skull_%d" % _t, "item", (lambda t=_t: build_skull(t)))
