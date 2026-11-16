"""The eight unique items: four on the heroes' own weapon meshes, a helm, greave-boots, a ring and an amulet.
Uniques carry a faint old-bronze note next to their own accent."""
import bpy, bmesh, math, random
import numpy as np
from mathutils import Vector, Euler, Matrix
import iconlib as I
from iconlib import R
from registry import icon
import props_items as PI
from props_armor import (Shell, sm, seam, hot, dark5, iron2, mail, tongue, clone, baked, buckle, on, pose, inside, void_disc, void,
                         surf, tube_surf, herm, dashes, TAN, LINEN, EMBER)
import props_armor_body as B
import props_armor_jewel as J


def bronze():
    return I.brass(amount=0.62, color=(0.33, 0.21, 0.075), edge_color=(0.70, 0.52, 0.24))


# ------------------------------------------------------------------------------------------------ weapon uniques
def uweapon(kind, key, **tints):
    """the hero's tier-3 weapon mesh with its baked materials re-tinted per kind (metal / emis / leather / bone ...)"""
    char, _ = PI.WPN[kind]
    o = I.weapon(char, "wpn_3")
    for i, m in enumerate(o.data.materials):
        if m is None:
            continue
        nm = m.name.split("_")[0]
        if nm in tints:
            o.data.materials[i] = I.tint_material(m, key, **tints[nm])
    return o


def mat_index(o, prefix):
    for i, m in enumerate(o.data.materials):
        if m is not None and (m.name.startswith(prefix) or ("_" + prefix + "_") in m.name):
            return i
    return -1


def child(o, parent):
    o.parent = parent
    return o


@icon("u_regret")
def u_regret():
    """灰冠王的遺憾: the Ash-Crowned King's greatsword: ash-pale steel, an iron crown riveted round the blade above the guard,
    a slow ember glow in the fuller"""
    main = uweapon("greatsword", "regret",
                   metal=dict(mul=(2.5, 2.4, 2.25), rough_add=0.22),
                   leather=dict(mul=(0.45, 0.42, 0.42)),
                   emis=dict(emit=(1.0, 0.16, 0.02), emit_strength=3.0))
    ir = iron2(amount=0.45); bz = bronze()
    cx, cy = -0.061, -0.017
    C = Shell([(-0.305, 0.106, 0.056, cy, cx), (-0.215, 0.120, 0.064, cy, cx)], power=2.3, linear=True)
    kids = [C.band(-0.305, -0.215, ir, 0.0, 0.014, name="crownband")]
    kids.append(I.tube([(cx, cy - 0.013, -0.200), (cx, cy - 0.013, 0.200), (cx, cy - 0.012, 0.500)], [0.0075, 0.0068, 0.0040], 6, seam(2.4), "fuller"))
    kids.append(C.ring(-0.305, 0.0075, bz, 0.002))
    kids.append(C.ring(-0.215, 0.0060, ir, 0.002))
    for i in range(8):
        a = 360.0 * i / 8 + 22.5
        hgt = 0.135 if i % 2 == 0 else 0.095
        t = tongue(0.066, hgt, 0.013, ir, 1.0, 2, "crownpoint", bev=0.003)
        C.put(t, a, -0.222, 0.0, tilt=-8, roll=180); kids.append(t)
        rv = I.ball(0.0085, bz, "rivet", 2); rv.location = C.pt(a, -0.270, 0.003); kids.append(rv)
    for k in kids:
        child(k, main)
    w, zs, inf = PI.WIDEN["greatsword"]
    I.place(main, rot=(-8, 42, 0), scale=(w, w, zs))
    bpy.context.view_layer.update()
    L = main.get("length", 1.6)
    I.sparks(7, (L * 0.02, -0.06, L * 0.02), (L * 0.40, -0.02, L * 0.42), r=L * 0.006, col=(1.0, 0.36, 0.08), seed=4, strength=4.0)
    return dict(fill=0.84)


def eye_material():
    key = "stareye"
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    b.inputs['Base Color'].default_value = (0.03, 0.05, 0.10, 1)
    b.inputs['Roughness'].default_value = 0.06
    b.inputs['Coat Weight'].default_value = 1.0
    b.inputs['Emission Color'].default_value = (0.25, 0.45, 1.0, 1)
    b.inputs['Emission Strength'].default_value = 0.35
    I._MATS[key] = m
    return m


def iris_material():
    """star-blue iris: radial streaks, brighter toward the pupil"""
    key = "stariris"
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    N = nt.nodes; lk = nt.links.new
    tc = N.new('ShaderNodeTexCoord')
    sep = N.new('ShaderNodeSeparateXYZ'); lk(tc.outputs['Object'], sep.inputs[0])
    at = N.new('ShaderNodeMath'); at.operation = 'ARCTAN2'; lk(sep.outputs['X'], at.inputs[0]); lk(sep.outputs['Y'], at.inputs[1])
    no = N.new('ShaderNodeTexNoise'); no.noise_dimensions = '1D'; no.inputs['Scale'].default_value = 9.0; no.inputs['Detail'].default_value = 3.0
    lk(at.outputs[0], no.inputs['W'])
    mr = N.new('ShaderNodeMapRange'); mr.inputs[1].default_value = 0.3; mr.inputs[2].default_value = 0.75
    mr.inputs[3].default_value = 0.55; mr.inputs[4].default_value = 1.7
    lk(no.outputs[0], mr.inputs[0])
    b.inputs['Base Color'].default_value = (0.05, 0.10, 0.25, 1)
    b.inputs['Roughness'].default_value = 0.15
    b.inputs['Emission Color'].default_value = (0.10, 0.34, 1.0, 1)
    lk(mr.outputs[0], b.inputs['Emission Strength'])
    I._MATS[key] = m
    return m


def watching_eye(Ro, lid, edge):
    """an eye-orb that looks along its local -Y: dark glassy ball, star-blue iris, slit pupil, bronze casing with two lids"""
    parts = [I.ball(Ro, eye_material(), "orb", 3)]
    a0, a1 = math.asin(0.20), math.asin(0.80)

    def cap(lo, hi, rr, mat, name, n=8):
        prof = [(rr * math.sin(lo + (hi - lo) * i / n), rr * math.cos(lo + (hi - lo) * i / n)) for i in range(n + 1)]
        o = I.lathe(prof, 40, mat, name); o.rotation_euler = (R(90), 0, 0)          # +Z -> -Y
        return o
    parts.append(cap(0.0, a1, Ro * 1.012, iris_material(), "iris"))
    pupil = I.ball(Ro * 0.44, void(), "pupil", 2, (0.34, 0.22, 1.0)); pupil.location = (0, -Ro * 0.985, 0); parts.append(pupil)
    gl = I.ball(Ro * 0.042, I.emit((0.9, 0.95, 1.0), 4.0), "glint", 2); gl.location = (-Ro * 0.26, -Ro * 0.985, Ro * 0.22); parts.append(gl)
    limb = I.torus(Ro * 0.80, Ro * 0.035, edge, "limbus", 40, 6); I.place(limb, loc=(0, -Ro * 0.625, 0), rot=(90, 0, 0)); parts.append(limb)
    # casing: back shell + two lids leaving an almond opening
    Rl = Ro * 1.05
    back = I.lathe([(Rl * math.sin(R(a)), -Rl * math.cos(R(a))) for a in range(0, 97, 8)], 40, lid, "socket"); back.rotation_euler = (R(90), 0, 0)
    I.solid(back, Ro * 0.06, 1.0); parts.append(back)
    for sgn in (1, -1):
        rows = []
        nu, nv = 28, 7
        for j in range(nv + 1):
            row = []
            for i in range(nu + 1):
                u = R(-96 + 192 * i / nu)
                ve = R(45 if sgn > 0 else 38) * max(0.0, math.cos(u * 90 / 96)) ** 0.70
                el = ve + (math.pi / 2 - ve) * j / nv
                row.append(Vector((Rl * math.cos(el) * math.sin(u), -Rl * math.cos(el) * math.cos(u), sgn * Rl * math.sin(el))))
            rows.append(row)
        ld = surf(rows, lid, "lid"); I.solid(ld, Ro * 0.07, 1.0); parts.append(ld)
        parts.append(I.tube([p * 1.02 for p in rows[0]], Ro * 0.050, 8, edge, "lidedge"))
    return parts


@icon("u_stargazer")
def u_stargazer():
    """觀星者之眼: the astromancer's staff whose crystal has become a watching eye: dark orb, star-blue iris, bronze lids"""
    main = uweapon("staff", "stargazer",
                   metal=dict(mul=(0.80, 0.66, 0.46)),
                   emis=dict(emit=(0.40, 0.66, 1.0), emit_strength=3.0))
    # drop the floating crystal and its shards (emissive faces above the cradle); the rings on the shaft stay
    me = main.data
    ei = mat_index(main, "emis")
    co = np.array([v.co[:] for v in me.vertices])
    sel = set()
    bm = bmesh.new(); bm.from_mesh(me)
    dead = [f for f in bm.faces if f.material_index == ei and f.calc_center_median().z > 0.60]
    for f in dead:
        for v in f.verts:
            sel.add(v.index)
    pts = co[sorted(sel)] if sel else co[co[:, 2] > 0.75]
    c = Vector(np.median(pts, axis=0))
    bmesh.ops.delete(bm, geom=dead, context='FACES')
    bm.to_mesh(me); bm.free()
    print("ICONDBG stargazer crystal centre %s  (%d verts)" % (tuple(round(x, 3) for x in c), len(sel)))
    w, zs, inf = PI.WIDEN["staff"]
    I.inflate(main, inf)
    I.place(main, rot=(-8, 42, 0), scale=(w, w, zs))
    bpy.context.view_layer.update()
    wc = main.matrix_world @ c
    eye = I.group(watching_eye(0.255, bronze(), I.gold()), "eye")
    I.place(eye, loc=wc + Vector((0.0, -0.12, 0.0)), rot=(-12, 0, -10))
    bpy.context.view_layer.update()
    L = main.get("length", 2.0)
    I.sparks(8, (L * 0.02, -0.5, L * 0.10), (L * 0.42, -0.4, L * 0.44), r=L * 0.0055, col=(0.55, 0.78, 1.0), seed=6, strength=6.0)
    return dict(fill=0.84)


def feather(length, width, mat, quill, bars=None, name="feather"):
    """flat feather hanging down from the origin (XZ plane, faces -Y)"""
    out = [(0.0, 0.0)]
    n = 10
    for i in range(1, n + 1):
        t = i / n
        out.append((width * 0.5 * math.sin(math.pi * t ** 0.8) ** 0.7 * (1.0 if t < 0.97 else 0.3), -length * (0.10 + 0.90 * t)))
    for i in range(n - 1, 0, -1):
        t = i / n
        out.append((-width * 0.5 * math.sin(math.pi * t ** 0.8) ** 0.7, -length * (0.10 + 0.90 * t)))
    parts = [I.extrude(out, 0.004, mat, name, bev=0.0012)]
    parts.append(I.tube([(0, -0.003, 0.004), (0, -0.003, -length * 0.5), (0, -0.003, -length * 0.97)], [0.0032, 0.0024, 0.0010], 6, quill, "quill"))
    if bars is not None:
        for t in (0.42, 0.62, 0.80):
            hw = width * 0.5 * math.sin(math.pi * t ** 0.8) ** 0.7 * 0.92
            parts.append(I.tube([(-hw, -0.0026, -length * (0.10 + 0.90 * t) + hw * 0.5), (0, -0.0030, -length * (0.10 + 0.90 * t)),
                                 (hw, -0.0026, -length * (0.10 + 0.90 * t) + hw * 0.5)], 0.0030, 5, bars, "bar"))
    return parts


@icon("u_owlbeak")
def u_owlbeak():
    """夜梟之喙: the hunter's hand crossbow in night blue-black, a hooked bone beak under its nose, two barred owl feathers"""
    main = uweapon("crossbow", "owlbeak",
                   metal=dict(mul=(0.34, 0.42, 0.66), rough_add=0.05),
                   leather=dict(mul=(0.45, 0.48, 0.62)),
                   emis=dict(mul=(2.2, 0.9, 1.25), emit=(0.62, 0.74, 1.0), emit_strength=0.70))
    bn = I.bone(); bz = bronze(); dk = I.cloth((0.17, 0.175, 0.21), hem_lo=-5.0, hem_hi=-4.0); pale = I.cloth((0.60, 0.59, 0.55), hem_lo=-5.0, hem_hi=-4.0)
    kids = []
    # beak: an arc that leaves the nose forward and hooks down (+X in the weapon's frame = down-right once posed)
    Ra = 0.092; z0 = 0.158; P = []; rad = []
    for i in range(17):
        t = i / 16; th = R(126 * t)
        P.append(Vector((0.012 + Ra * (1 - math.cos(th)), -0.020, z0 + Ra * math.sin(th))))
        rad.append(0.034 * (1 - t) ** 0.72 + 0.0016)
    beak = I.tube(P, rad, 14, bn, "beak"); beak.scale = (1.0, 0.72, 1.0); kids.append(beak)
    cere = I.torus(0.034, 0.0075, bz, "cere", 28, 8); cere.scale = (1.0, 0.72, 1.0); B.aim(cere, P[1] + Vector((0, 0.0055, 0)), P[2] - P[0]); kids.append(cere)
    nos = I.ball(0.0058, void(), "nostril", 2, (1.5, 0.6, 1.0)); nos.location = P[3] + Vector((-0.016, -0.019, 0.004)); kids.append(nos)
    # two feathers tied behind the prod
    tie = Vector((0.020, -0.040, 0.010))
    kn = I.ball(0.012, bz, "tie", 2); kn.location = tie; kids.append(kn)
    for (ln, wd, ang) in ((0.250, 0.074, -40.0), (0.205, 0.064, -66.0)):
        g = I.group(feather(ln, wd, dk, bn, pale), "feather")
        I.place(g, loc=tie, rot=(0, ang, 0)); kids.append(g)
    for k in kids:
        child(k, main)
    I.inflate(main, 0.007)
    I.place(main, rot=(-14, 40, 0))
    return dict(fill=0.84)


@icon("u_reaper")
def u_reaper():
    """寂夜收割者: the soulbinder's scythe gone near-black, its edge and eye burning a pale violet-white"""
    main = uweapon("scythe", "reaper",
                   metal=dict(mul=(0.13, 0.13, 0.17), rough_add=0.06),
                   bone=dict(mul=(0.62, 0.45, 0.25)),
                   leather=dict(mul=(0.38, 0.36, 0.42)),
                   emis=dict(emit=(0.80, 0.70, 1.0), emit_strength=4.6))
    w, zs, inf = PI.WIDEN["scythe"]
    I.inflate(main, inf)
    I.place(main, rot=(-8, 42, 0), scale=(w, w, zs))
    bpy.context.view_layer.update()
    L = main.get("length", 2.3)
    I.sparks(9, (L * 0.05, -0.08, L * 0.12), (L * 0.50, -0.03, L * 0.40), r=L * 0.0052, col=(0.82, 0.74, 1.0), seed=8, strength=6.0)
    return dict(fill=0.84)


# ------------------------------------------------------------------------------------------------ armour / jewellery uniques
@icon("u_snuffer")
def u_snuffer():
    """熄燈者面甲: helm of the Hushed Order: a smooth blank face plate with no eye slit at all, a tall candle-snuffer cone
    for a crest, cold dark steel with old bronze edging, a last thread of smoke"""
    st = I.look('metal', "cold_steel", color=(0.075, 0.090, 0.125), rust=(0.03, 0.035, 0.05), rust2=(0.015, 0.018, 0.025), amount=0.35, rough=0.32,
                edge=0.9, edge_color=(0.40, 0.46, 0.56))
    fc = I.look('metal', "cold_face", color=(0.50, 0.55, 0.64), rust=(0.10, 0.12, 0.16), rust2=(0.04, 0.05, 0.07), amount=0.16, rough=0.36,
                edge=0.5, edge_color=(0.78, 0.82, 0.90))
    bz = bronze()
    H = Shell([(-0.168, 0.117, 0.129), (-0.100, 0.110, 0.122), (-0.020, 0.113, 0.125), (0.050, 0.108, 0.120), (0.095, 0.088, 0.100), (0.122, 0.058, 0.066),
               (0.137, 0.030, 0.034)])
    parts = [H.mesh(st, 56, thick=0.0062, name="skull"), inside(0.10, (0, 0, -0.06), (1, 1.05, 1.1))]

    def hw(z):          # half-angle of the face plate: widest at the cheekbones
        return 64.0 * (1 - 0.42 * ((z + 0.035) / 0.125) ** 2)
    zs = [-0.160 + 0.225 * i / 18 for i in range(19)]
    seg = 36
    rows = []
    for z in zs:
        row = []
        for i in range(seg + 1):
            a = -hw(z) + 2 * hw(z) * i / seg
            keel = 0.011 * (1 - abs(a) / hw(z)) ** 1.6 * sm(0.07, 0.0, z)
            row.append(H.pt(a, z, 0.0085 + keel))
        rows.append(row)
    fp = surf(rows, fc, "faceplate"); I.solid(fp, 0.006); parts.append(fp)
    border = [r[0] for r in rows] + [rows[-1][i] for i in range(1, seg)] + [r[-1] for r in reversed(rows)] + [rows[0][i] for i in range(seg - 1, 0, -1)]
    parts.append(I.tube(border, 0.0056, 8, bz, "border", closed=True))
    for k in range(0, len(border), 9):
        rv = I.ball(0.0052, bz, "rivet", 2); rv.location = border[k] + Vector((0, -0.003, 0)); parts.append(rv)
    # neck guard
    T = Shell([(-0.225, 0.156, 0.166, 0.010), (-0.168, 0.119, 0.131, 0.0)], linear=True)
    parts.append(T.mesh(st, 40, thick=0.0062, a0=66, a1=294, name="tail"))
    parts.append(T.ring(-0.225, 0.0050, bz, 0.001, 66, 294))
    parts.append(H.ring(0.060, 0.0050, bz, 0.002, 58, 302))
    # candle-snuffer crest: a tall belled cone with its hanging ring
    cone = I.lathe([(0.076, 0.0), (0.064, 0.010), (0.047, 0.040), (0.030, 0.088), (0.0150, 0.136), (0.0070, 0.166), (0.0095, 0.174), (0.0095, 0.184), (0.0, 0.188)], 32, st, "snuffer")
    I.place(cone, loc=(0, 0.004, 0.122), rot=(8, 0, 0)); parts.append(cone)
    for (r, z, t) in ((0.074, 0.004, 0.0056), (0.030, 0.088, 0.0036)):
        b = I.torus(r, t, bz, "crestband", 32, 8); b.parent = cone; b.location = (0, 0, z); parts.append(b)
    hk = I.torus(0.0125, 0.0034, bz, "hook", 24, 8); hk.parent = cone; I.place(hk, loc=(0, 0, 0.198), rot=(90, 0, 0)); parts.append(hk)
    g = pose([p for p in parts if p.parent is None], (0, 0, 30))
    bpy.context.view_layer.update()
    tip = cone.matrix_world @ Vector((0, 0, 0.212))
    sm_ = I.fire(hot=(0.42, 0.47, 0.55), cool=(0.14, 0.16, 0.20), strength=0.55, key="coldsmoke2")
    for k, (h, r, dx) in enumerate(((0.110, 0.014, 0.0), (0.075, 0.010, 0.012))):
        f = I.flame(h, r, sm_, seed=5 + k, lean=(0, 10, 0)); f.location = tip + Vector((dx, 0, 0.004 + 0.02 * k)); f["nofit"] = 1
    return dict(fill=0.80)


def spade(Sh, mat, edge, z_tip=0.120, z_sh=0.245, z_top=0.365, half=0.064, Rc=0.092, back=0.030):
    """spade blade strapped on as a shin plate: curved sheet, square shoulders, rounded point down. -> (parts, front(z))"""
    cy = Sh.lv(0.2)[2] + back

    def hwf(z):
        if z >= z_sh:
            return half
        u = (z_sh - z) / (z_sh - z_tip)
        return half * math.sqrt(max(0.0, 1 - u * u)) ** 0.9
    zs = [z_tip + (z_sh - z_tip) * (1 - math.cos(math.pi / 2 * i / 9)) for i in range(10)] + [z_sh + (z_top - z_sh) * i / 5 for i in range(1, 6)]
    seg = 14; rows = []
    for z in zs:
        h = max(hwf(z), 1e-5)
        rows.append([Vector((Rc * math.sin(s / Rc), cy - Rc * math.cos(s / Rc), z)) for s in (-h + 2 * h * i / seg for i in range(seg + 1))])
    bl = surf(rows, mat, "spade"); I.solid(bl, 0.0062)
    parts = [bl]
    parts.append(I.tube(rows[-1], 0.0062, 8, edge, "tread"))                     # folded step along the top
    front = lambda z: Vector((0.0, cy - Rc, z))
    return parts, front


@icon("u_shovel")
def u_shovel():
    """葛魯姆的鐵脛: the gravedigger's greave-boots: rusted iron, each shin plate a spade blade with its broken-off socket,
    grave dirt still caked on"""
    ir = I.iron(); ir2 = I.iron(amount=0.70, color=(0.165, 0.155, 0.15)); dk = I.leather((0.055, 0.038, 0.027)); wd = I.wood((0.16, 0.105, 0.06))
    mud = I.look('mud', "gravedirt", color=(0.085, 0.062, 0.042), dry=(0.21, 0.17, 0.125)); bz = bronze()

    def make(i):
        rnd = random.Random(21 + i)
        parts, Sh = B.boot(dk, dk, dk)
        parts.append(B.foot_part(B.FOOT, 0.28, 0.60, ir, 0.0105, 5, "lame"))
        parts.append(B.foot_part(B.FOOT, 0.52, 1.0, ir2, 0.0070, 6, "toe"))
        parts.append(B.foot_part(B.FOOT, 0.0, 0.20, ir, 0.0070, 4, "heel"))
        sp, front = spade(Sh, ir2, ir)
        parts += sp
        # socket with the snapped ash shaft, frog running down the blade
        f0 = front(0.355)
        parts.append(I.tube([f0 + Vector((0, -0.006, -0.130)), f0 + Vector((0, -0.010, -0.020)), f0 + Vector((0, -0.012, 0.050))], [0.006, 0.019, 0.021], 12, ir, "socket"))
        parts.append(I.tube([f0 + Vector((0, -0.012, 0.045)), f0 + Vector((0, -0.013, 0.100))], [0.0165, 0.0155], 10, wd, "shaft"))
        for k in range(4):
            sv = I.cone(0.006, 0.0006, rnd.uniform(0.016, 0.036), wd, "splinter", 5)
            sv.location = f0 + Vector((rnd.uniform(-0.009, 0.009), -0.013 + rnd.uniform(-0.008, 0.008), 0.098)); sv.rotation_euler = (R(rnd.uniform(-14, 14)), R(rnd.uniform(-14, 14)), 0)
            parts.append(sv)
        parts.append(B.aim(I.torus(0.0215, 0.0042, bz, "ferrule", 20, 6), f0 + Vector((0, -0.012, 0.046)), Vector((0, -0.01, 1))))
        for z in (0.185, 0.290):
            parts.append(Sh.band(z, z + 0.028, dk, 0.0055, 0.004, 44, 316, 30, name="strap"))
            for sx in (-1, 1):
                rv = I.ball(0.0056, bz, "rivet", 2); rv.location = front(z + 0.014) + Vector((sx * 0.046, 0.011, 0)); parts.append(rv)
        # grave dirt: clods on the blade point, the toe and the sole
        for (p, r) in ((front(0.145) + Vector((0.010, -0.004, 0)), 0.024), (front(0.185) + Vector((-0.026, 0.004, 0)), 0.017),
                       (Vector((0.020, -0.150, 0.062)), 0.021), (Vector((-0.030, -0.185, 0.030)), 0.018), (Vector((0.056, -0.060, 0.004)), 0.020),
                       (Vector((0.050, 0.050, 0.000)), 0.017)):
            cl = I.shard(r, mud, "clod", seed=rnd.randint(1, 99), n=12, squash=(1.0, 0.75, 0.62)); cl.location = p
            cl.rotation_euler = (0, 0, rnd.uniform(0, 3)); parts.append(cl)
        return parts
    B.pair(make)
    rnd = random.Random(3)
    for k in range(6):
        cl = I.shard(rnd.uniform(0.008, 0.016), mud, "crumb", seed=40 + k, n=9, squash=(1.0, 0.9, 0.6))
        cl.location = (rnd.uniform(-0.16, 0.06), rnd.uniform(-0.17, -0.10), -0.034)
    return dict(fill=0.84)


@icon("u_candle")
def u_candle():
    """殘燭指環: a black-iron ring whose stone is a guttering candle stub in a bronze pricket cup, wax run down over the band,
    one small live flame"""
    ir = I.ashplate(); wx = I.look('wax', "candlewax2", color=(0.50, 0.44, 0.31), soot=0.28); bz = bronze()
    parts = [PI.band(ir, 0.0105, 0.0022)]
    for s in (-1, 1):
        sh = I.ball(0.0027, ir, "shoulder", 2, (1, 1, 1.2)); sh.location = (s * 0.0042, -0.0110, 0); parts.append(sh)
    bez = [I.lathe([(0.0, 0.0), (0.0034, 0.0), (0.0056, 0.0016), (0.0062, 0.0032), (0.0053, 0.0037), (0.0, 0.0032)], 28, bz, "cup")]
    bez.append(I.lathe([(0.0, 0.0030), (0.0044, 0.0030), (0.0046, 0.0066), (0.0045, 0.0100), (0.0038, 0.0112), (0.0022, 0.0106), (0.0, 0.0101)], 28, wx, "stub"))
    bez.append(I.torus(0.0052, 0.0011, wx, "pool", 28, 6)); bez[-1].location = (0, 0, 0.0036)
    rnd = random.Random(7)
    for a in (8, 60, 118, 188, 240, 300):          # runs of wax; the two along the hoop reach down onto its shoulders
        ca, sa = math.cos(R(a)), math.sin(R(a))
        if a in (8, 188):
            prof = [(0.0040, 0.0108), (0.0048, 0.0090), (0.0051, 0.0060), (0.0065, 0.0036), (0.0066, 0.0014), (0.0058, -0.0012)]
        else:
            prof = [(0.0040, 0.0108), (0.0048, 0.0090), (0.0050, rnd.uniform(0.0046, 0.0072))]
        pts = [(r * ca, r * sa, z) for (r, z) in prof]
        r0 = rnd.uniform(0.00090, 0.00125)
        bez.append(I.tube(pts, [r0 * 0.8] + [r0] * (len(pts) - 2) + [r0 * 1.25], 6, wx, "run"))
        dp = I.ball(r0 * 1.5, wx, "drop", 2, (1, 1, 1.3)); dp.location = pts[-1]; bez.append(dp)
    bez.append(I.tube([(0, 0, 0.0100), (0.0002, 0, 0.0112), (0.0005, 0, 0.0121)], 0.00038, 5, void(), "wick"))
    b = I.group(bez); I.place(b, loc=(0, -0.0122, 0), rot=(90, 0, 0)); parts.append(b)
    pose(parts, J.RING_POSE)
    bpy.context.view_layer.update()
    wt = b.matrix_world @ Vector((0.0005, 0, 0.0119))
    fl = I.flame(0.0132, 0.0036, I.fire(hot=(1.0, 0.62, 0.20), cool=(1.0, 0.22, 0.03), strength=2.7, key="candlefire3"), seed=3); fl.location = wt + Vector((0, 0, -0.0012))
    core = I.ball(0.0009, I.emit((1.0, 0.60, 0.20), 2.6), "core", 2, (1, 1, 1.7)); core.location = wt + Vector((0, 0, 0.0020))
    return dict(fill=0.76, samples=64, margin_glow=0.0)


def heart_mesh(s, mat, name="heart"):
    """heart-shaped lump: a sphere pulled into two lobes with a cleft, tapered to a point"""
    o = I.ball(1.0, mat, name, 4)
    for v in o.data.vertices:
        x, y, z = v.co
        zz = z * 0.92 + 0.62 * abs(x) ** 0.85 - 0.18
        if z < 0:
            zz -= 0.38 * z * z
        k = 0.50 + 0.50 * sm(-1.0, 0.25, z)
        v.co = (x * k * 1.18 * s, y * k * 0.68 * s, zz * s)
    o.data.update()
    I.displace(o, s * 0.14, s * 0.55, seed=1)
    return o


@icon("u_emberheart")
def u_emberheart():
    """燼心: a heart-shaped lump of living ember gripped by an iron claw cage, old bronze collar, iron chain"""
    ir = I.ashplate(); bd = iron2(); bz = bronze(); emb = hot(4.4, 88.0, 0.36, 1.45)
    s = 0.037
    P = J.loop_pts(0.066, 0.086)
    parts = I.chain(P + [P[0]], 0.0145, 0.0033, bd)
    zc = -(1.08 * s + 0.010)                     # heart centre: the cap of the cage ends just under the loop's pinch
    heart = heart_mesh(s, emb); heart.location = (0, 0, zc); parts.append(heart)
    ez, eh = zc - 0.24 * s, 1.32 * s             # cage: an ellipse round the heart, from the cap down to under its point
    for a in (-52, 52, -128, 128, 0, 180):
        ca, sa = math.sin(R(a)), -math.cos(R(a))
        short = a in (0, 180)
        pts = []; rad = []
        n = 14
        for i in range(n + 1):
            t = i / n
            th = R(5 + (92 if short else 162) * t)
            rr = (1.02 if short else 1.36) * s * math.sin(th)
            pts.append((rr * ca, rr * sa * 0.76, ez + eh * math.cos(th)))
            rad.append(0.0042 * (1 - 0.60 * t))
        parts.append(I.tube(pts, rad, 8, ir, "claw"))
    cap = I.lathe([(0.0, 0.006), (0.0085, 0.004), (0.0135, -0.006), (0.0120, -0.012), (0.0, -0.012)], 20, ir, "cap"); cap.location = (0, 0, ez + eh); parts.append(cap)
    col = I.torus(0.0128, 0.0030, bz, "collar", 24, 8); col.location = (0, 0, ez + eh - 0.006); parts.append(col)
    parts.append(J.bail(bd, ez + eh + 0.010, 0.0078, 0.0032))
    pose(parts, J.AMULET_POSE)
    bpy.context.view_layer.update()
    I.sparks(6, (-0.06, -0.03, -0.11), (0.06, -0.02, 0.02), r=0.0011, col=(1.0, 0.42, 0.10), seed=11, strength=5.0)
    return dict(fill=0.82, samples=56)
