"""Map / minimap marker tokens.

Thirty chunky tokens, drawn at 14-60 px: ONE simple object each, a silhouette that survives at 32 px and a colour code
carried by a glowing / enamelled element (display-true glows from props_markers_kit, see the note there). Everything is
modelled as a table-top token 0.3-0.6 m across, so the kit looks (scan grain, AO masks) land at a readable scale.
"""
import bpy, bmesh, math, random
from mathutils import Vector, Euler
import iconlib as I
from iconlib import R
from registry import icon
import props_items as PI
import props_markers_kit as K

V = Vector

# marker colours as they must SHOW on screen (sRGB)
GOLD = (1.0, 0.74, 0.22)
YGOLD = (1.0, 0.84, 0.22)
RED = (1.0, 0.12, 0.06)
REDOR = (1.0, 0.25, 0.05)
ORANGE = (1.0, 0.46, 0.08)
AMBER = (1.0, 0.63, 0.12)
ORGOLD = (1.0, 0.60, 0.13)
WGOLD = (1.0, 0.80, 0.34)
PGOLD = (1.0, 0.89, 0.60)
IVORY = (1.0, 0.94, 0.80)
CYBLUE = (0.20, 0.70, 1.0)
LBLUE = (0.50, 0.82, 1.0)
CYAN = (0.18, 0.92, 0.95)
VIOLET = (0.55, 0.30, 1.0)
PVIOLET = (0.76, 0.60, 1.0)
PGREEN = (0.52, 1.0, 0.60)
GREEN = (0.30, 0.92, 0.36)


def pose(parts, rot=(0, 0, 0), loc=(0, 0, 0), scale=1.0, name="grp"):
    g = I.group(parts, name)
    I.place(g, loc, rot, scale)
    return g


def bright_gold():
    return I.look('metal', color=(0.78, 0.55, 0.16), rust=(0.22, 0.14, 0.05), rust2=(0.10, 0.07, 0.03), amount=0.22, rough=0.26,
                  edge_color=(1.0, 0.86, 0.48))


def iron():
    """dark forged iron with some rust (the kit's rusty_iron preset is nearly all orange rust: it reads as copper)"""
    return I.look('metal', color=(0.20, 0.195, 0.19), rust=(0.16, 0.08, 0.04), rust2=(0.07, 0.04, 0.028), amount=0.5, rough=0.48, edge=1.0)


def at(o, loc, rot=None):
    o.location = loc
    if rot is not None:
        o.rotation_euler = Euler((R(rot[0]), R(rot[1]), R(rot[2])), 'XYZ')
    return o


# ================================================================================================ quest / objective
@icon("quest", "marker")
def quest():
    """任務目標: a rolled vellum scroll on its roller, closed with a big glowing gold seal"""
    vel = I.look('leather', color=(0.62, 0.52, 0.34), scuff_color=(0.74, 0.66, 0.48), stitch=0.0, rough=0.72)
    wd = I.wood((0.09, 0.06, 0.04)); gd = bright_gold(); bz = I.brass()
    L, r = 0.29, 0.058
    parts = [I.lathe([(0.0, -L / 2), (r * 0.93, -L / 2), (r, -L / 2 + 0.008), (r * 1.03, 0.0), (r, L / 2 - 0.008), (r * 0.93, L / 2), (0.0, L / 2)], 36, vel, "roll")]
    flap = I.lathe([(r * 1.09, -L / 2 + 0.004), (r * 1.115, 0.0), (r * 1.09, L / 2 - 0.004)], 12, vel, "flap", arc=85.0); I.solid(flap, 0.006)
    flap.rotation_euler = (0, 0, R(-165)); parts.append(flap)
    for s in (-1, 1):
        parts.append(at(I.lathe([(0.0, 0.0), (r * 0.52, 0.0)], 24, K.void(), "core"), (0, 0, s * (L / 2 + 0.0006))))
        parts.append(at(I.ball(0.029, gd, "knob", 3, (1, 1, 0.85)), (0, 0, s * (L / 2 + 0.045))))
    parts.append(I.lathe([(0.0, -L / 2 - 0.04), (0.017, -L / 2 - 0.04), (0.017, L / 2 + 0.04), (0.0, L / 2 + 0.04)], 16, wd, "rod"))
    rib = I.lathe([(r * 1.06, -0.036), (r * 1.10, 0.0), (r * 1.06, 0.036)], 36, I.cloth((0.15, 0.03, 0.025)), "ribbon"); I.solid(rib, 0.005, 1.0)
    parts.append(rib)
    # the seal: gold rim and boss, a glowing field between them, eight short rays
    sp = [I.lathe([(0.0, 0.022), (0.019, 0.022), (0.024, 0.014), (0.055, 0.014), (0.060, 0.023), (0.069, 0.018), (0.072, 0.004), (0.066, -0.008), (0.0, -0.008)], 40, gd, "seal")]
    sp.append(at(I.lathe([(0.0225, 0.0), (0.0565, 0.0)], 40, K.glow(GOLD, 3.0), "sealglow"), (0, 0, 0.0152)))
    for k in range(8):
        a = R(22.5 + 45 * k)
        sp.append(at(I.box(0.022, 0.009, 0.007, bz, "ray", 0.0015), (0.040 * math.cos(a), 0.040 * math.sin(a), 0.0175), (0, 0, 22.5 + 45 * k)))
    parts.append(pose(sp, (90, 0, 0), (0, -(r * 1.10 + 0.008), 0), 1.1, name="seal"))
    pose(parts, (0, 52, 10))
    return dict(fill=0.78)


@icon("objective", "marker")
def objective():
    """地城目標: a faceted golden marker stone (octahedron) in an iron girdle"""
    g = K.facet("objective", [(0.0, (0.55, 0.24, 0.02)), (0.36, (0.94, 0.52, 0.06)), (0.64, YGOLD), (0.92, (1.0, 0.92, 0.52))])
    pts = [(0, 0, 0.175), (0, 0, -0.175)] + [(0.112 * math.cos(R(a)), 0.112 * math.sin(R(a)), 0.0) for a in (0, 90, 180, 270)]
    parts = [I.hull(pts, g, "stone")]
    parts.append(I.lathe([(0.103, -0.016), (0.120, -0.016), (0.126, 0.0), (0.120, 0.016), (0.103, 0.016), (0.103, -0.016)], 4, iron(), "girdle", smooth=False))
    for a in (0, 90, 180, 270):
        parts.append(at(I.ball(0.017, iron(), "stud", 2), (0.126 * math.cos(R(a)), 0.126 * math.sin(R(a)), 0.0)))
    pose(parts, (8, 0, 26))
    return dict(fill=0.78, samples=56)


# ================================================================================================ boss / elite
def skull(col, strength=5.0, tag=""):
    """the game's skeleton skull, bare: its kettle hat, straps, rag and neck bones are stripped and the bone gets the kit
    look (the baked texture carries the hat's shadow, which read as a skullcap). Eyes glow in `col`.
    Returns (object, bbox lo, bbox hi, [eye centres])."""
    o = I.head_of("mon_skeleton")
    keep = []; em = []
    for i, m in enumerate(o.data.materials):
        nm = m.name if m is not None else ""
        if 'emis' in nm:
            o.data.materials[i] = K.glow(col, strength)
            keep.append(i); em.append(i)
        elif 'bone' in nm:
            o.data.materials[i] = I.bone()
            keep.append(i)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index not in keep], context='FACES')
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    seen = set(); drop = []                                        # connected pieces: drop the neck vertebrae (low, behind the jaw)
    for v0 in bm.verts:
        if v0 in seen:
            continue
        isl = []; stack = [v0]; seen.add(v0)
        while stack:
            v = stack.pop(); isl.append(v)
            for e in v.link_edges:
                w = e.other_vert(v)
                if w not in seen:
                    seen.add(w); stack.append(w)
        if max(v.co.z for v in isl) < 0.0 and min(v.co.y for v in isl) > 0.004:
            drop += isl
    if drop:
        bmesh.ops.delete(bm, geom=drop, context='VERTS')
    bm.to_mesh(o.data); bm.free()
    lo, hi = K.bounds(o)
    eyes = []
    for sgn in (-1, 1):
        cs = [pl.center for pl in o.data.polygons if pl.material_index in em and pl.center.x * sgn > 0]
        if cs:
            eyes.append(sum(cs, V()) / len(cs))
    return o, lo, hi, eyes


def eye_glow(eyes, col, r=0.017):
    """a bigger ember in each socket than the model's own eye, so the colour survives at 32 px"""
    return [at(I.ball(r, K.glow(col, 5.0), "eye", 3), (e.x, e.y + 0.002, e.z)) for e in eyes]


def crown(rc, bh, sh, mat, n=8):
    parts = []
    band = I.lathe([(rc * 0.98, 0.0), (rc * 1.03, bh * 0.5), (rc * 1.0, bh)], 40, mat, "band"); I.solid(band, rc * 0.08, 1.0)
    parts.append(band)
    for k in range(n):
        a = -90.0 + 360.0 * k / n
        tall = sh * (1.0 if k % 2 == 0 else 0.6)
        tw = rc * 0.52; tt = rc * 0.12
        sp = I.hull([(-tw / 2, -tt / 2, 0), (tw / 2, -tt / 2, 0), (tw / 2, tt / 2, 0), (-tw / 2, tt / 2, 0), (0, -tt * 0.3, tall), (0, tt * 0.3, tall)], mat, "spike")
        sp.location = (rc * 1.03 * math.cos(R(a)), rc * 1.03 * math.sin(R(a)), bh * 0.85)
        sp.rotation_euler = Euler((R(9), 0, R(a + 90)), 'XYZ')
        parts.append(sp)
    return parts


@icon("boss", "marker")
def boss():
    """首領: the skeleton's skull under a dark iron crown, eyes burning red"""
    o, lo, hi, eyes = skull(RED, 5.0, "boss")
    w = hi.x - lo.x; cx = (lo.x + hi.x) / 2; cy = (lo.y + hi.y) / 2
    cr = crown(w * 0.56, w * 0.20, w * 0.60, iron())
    cr.append(at(I.ball(w * 0.085, K.glow(RED, 5.0), "jewel", 2, (1, 0.5, 1.35)), (0, -w * 0.60, w * 0.10)))
    g = pose(cr, (-6, 0, 0), (cx, cy, hi.z - w * 0.42), name="crown")
    pose([o, g] + eye_glow(eyes, RED), (0, 0, 14))
    K.lamp((0.0, -0.20, 0.03), RED, 0.8)
    return dict(fill=0.80)


@icon("elite", "marker")
def elite():
    """被標記的菁英: the same skull, bare-headed and cocked to one side, with two heavy curved horns and an orange glow"""
    o, lo, hi, eyes = skull(ORANGE, 5.0, "elite")
    w = hi.x - lo.x; cx = (lo.x + hi.x) / 2; cy = (lo.y + hi.y) / 2
    hm = I.bone(color=(0.24, 0.185, 0.12), stain=(0.07, 0.045, 0.03))
    parts = [o] + eye_glow(eyes, ORANGE)
    for s in (-1, 1):
        pts = [(cx + s * 0.30 * w, cy, hi.z - 0.40 * w), (cx + s * 0.60 * w, cy, hi.z - 0.30 * w), (cx + s * 0.90 * w, cy - 0.03 * w, hi.z - 0.04 * w),
               (cx + s * 1.00 * w, cy - 0.06 * w, hi.z + 0.30 * w), (cx + s * 0.82 * w, cy - 0.08 * w, hi.z + 0.60 * w)]
        parts.append(K.limb(pts, [0.17 * w, 0.16 * w, 0.135 * w, 0.09 * w, 0.025 * w], hm, 22, 14, "horn"))
    pose(parts, (0, -13, -20))
    K.lamp((-0.05, -0.20, 0.03), ORANGE, 0.8)
    return dict(fill=0.80)


# ================================================================================================ doors
@icon("bossdoor", "marker")
def bossdoor():
    """首領之門: a heavy iron-bound double door in a stone frame, red light burning in the seam"""
    st = K.stone(0.85); wd = I.wood((0.075, 0.05, 0.032)); ir = iron()
    red = K.glow(REDOR, 5.0)
    W, H, T = 0.34, 0.40, 0.06
    parts = []
    for s in (-1, 1):
        parts.append(at(I.box(0.08, 0.13, H + 0.02, st, "jamb", 0.008), (s * (W / 2 + 0.04), 0.0, (H + 0.02) / 2)))
    parts.append(at(I.box(W + 0.24, 0.15, 0.095, st, "lintel", 0.010), (0, 0, H + 0.02 + 0.0475)))
    parts.append(at(I.box(W + 0.26, 0.19, 0.035, st, "sill", 0.007), (0, -0.015, -0.0175)))
    half = 0.013                                                   # half width of the seam
    for s in (-1, 1):
        lw = W / 2 - half
        xc = s * (half + lw / 2)
        parts.append(at(I.box(lw, T, H, wd, "leaf", 0.004), (xc, -0.01, H / 2)))
        for z in (0.065, 0.20, 0.335):
            parts.append(at(I.box(lw + 0.002, T + 0.014, 0.052, ir, "band", 0.005), (xc, -0.012, z)))
            for dx in (-0.045, 0.045):
                parts.append(at(I.ball(0.011, ir, "stud", 2), (xc + dx, -0.049, z)))
        parts.append(at(I.torus(0.022, 0.006, ir, "pull", 20, 8), (s * 0.055, -0.052, 0.20), (90, 0, 0)))
    parts.append(at(I.box(half * 2, 0.034, H, red, "seam"), (0, -0.026, H / 2)))
    parts.append(at(I.box(W, 0.03, 0.012, red, "threshold"), (0, -0.03, 0.006)))
    parts.append(at(I.extrude([(0, 0.055), (0.034, 0), (0, -0.055), (-0.034, 0)], 0.02, red, "lock"), (0, -0.046, 0.20)))
    pose(parts, (0, 0, -20))
    K.lamp((-0.02, -0.16, 0.20), REDOR, 2.0)
    return dict(fill=0.78)


@icon("entrance", "marker")
def entrance():
    """地城入口: a tall stone archway, steps going down into the dark, ember light coming up from below"""
    st = K.stone(); dk = K.stone(0.55)
    ro, ri, hs, D = 0.205, 0.105, 0.27, 0.16
    parts = [I.extrude(K.arch_outline(ro, ri, hs), D, st, "arch", bev=0.008)]
    parts.append(K.frustum(0.050, D + 0.02, 0.084, D + 0.02, 0.115, st, "keystone", z=hs + ri - 0.004))
    for s in (-1, 1):
        parts.append(at(I.box(ro - ri + 0.02, D + 0.016, 0.032, st, "impost", 0.005), (s * (ri + ro) / 2, 0, hs)))
    parts.append(at(I.box(0.52, 0.27, 0.045, st, "plinth", 0.008), (0, -0.04, -0.0225)))
    # inside: the stair in profile going down to the right, black above, the glow coming up from below
    back = I.extrude(K.door_outline(ri, hs), 0.012, K.gradient("entr3", [(0.0, (1.0, 0.70, 0.20)), (0.30, ORANGE), (0.58, (0.62, 0.14, 0.02)), (0.82, (0.10, 0.02, 0.01)), (1.0, (0.0, 0.0, 0.0))]), "glow")
    parts.append(at(back, (0, D / 2 - 0.012, 0)))
    for k, (x0, x1, h) in enumerate(((-ri, -0.035, 0.135), (-0.035, 0.035, 0.088), (0.035, ri, 0.042))):
        parts.append(at(I.box(x1 - x0, 0.09, h, dk, "step", 0.004), ((x0 + x1) / 2, 0.02, h / 2)))
    pose(parts, (0, 0, -16))
    K.lamp((0.04, -0.035, 0.11), ORANGE, 5.0)
    return dict(fill=0.78)


@icon("exit", "marker")
def exit_():
    """出口: a plank door standing ajar, pale daylight spilling out of the frame"""
    wd = I.wood((0.17, 0.11, 0.06)); fr = I.wood((0.075, 0.05, 0.035)); ir = iron()
    W, H = 0.21, 0.37
    parts = []
    for s in (-1, 1):
        parts.append(at(I.box(0.05, 0.08, H + 0.03, fr, "post", 0.006), (s * (W / 2 + 0.025), 0, (H + 0.03) / 2)))
    parts.append(at(I.box(W + 0.15, 0.09, 0.055, fr, "head", 0.007), (0, 0, H + 0.03 + 0.0275)))
    parts.append(at(I.box(W + 0.20, 0.24, 0.03, K.stone(), "sill", 0.006), (0, -0.07, -0.015)))
    parts.append(at(I.box(W, 0.01, H, K.gradient("exit", [(0.0, (1.0, 0.90, 0.70)), (0.5, IVORY), (1.0, (1.0, 0.97, 0.90))]), "light"), (0, 0.01, H / 2)))
    # the leaf, hinged on the left post and swung toward the viewer
    leaf = [at(I.box(W, 0.03, H - 0.01, wd, "leaf", 0.004), (W / 2, 0, H / 2))]
    for z in (0.08, 0.29):
        leaf.append(at(I.box(W * 0.80, 0.042, 0.035, ir, "strap", 0.004), (W * 0.42, 0, z)))
    leaf.append(at(I.torus(0.022, 0.006, ir, "pull", 20, 8), (W * 0.80, -0.022, 0.185), (90, 0, 0)))
    parts.append(pose(leaf, (0, 0, -62), (-W / 2, -0.045, 0.0), name="leaf"))
    pose(parts, (0, 0, 10))
    K.lamp((0.03, -0.10, 0.20), IVORY, 6.0)
    return dict(fill=0.78)


# ================================================================================================ towers and fires
def pennant(length, hh, mat, wave=0.014, nx=14, nz=4):
    vs = []; fs = []
    for i in range(nx + 1):
        u = i / nx
        half = hh * (1.0 - 0.70 * u)
        y = wave * math.sin(u * 7.5) * u ** 0.6
        for j in range(nz + 1):
            vs.append((u * length, y, half * (1 - 2 * j / nz) - u * length * 0.10))
    for i in range(nx):
        for j in range(nz):
            a = i * (nz + 1) + j
            fs.append((a, a + nz + 1, a + nz + 2, a + 1))
    o = I.obj("flag", vs, fs, mat, True)
    I.solid(o, 0.005, 0.0)
    return o


@icon("stronghold", "marker")
def stronghold():
    """燼塔據點: a squat square keep with battlements, its gate and slits lit, an ember-orange pennant flying"""
    st = K.stone(); ir = iron()
    b, t, h = 0.31, 0.26, 0.27
    parts = [K.frustum(b, b, t, t, h, st, "keep")]
    parts.append(at(I.box(t + 0.05, t + 0.05, 0.04, st, "corbel", 0.006), (0, 0, h + 0.02)))
    e = (t + 0.05) / 2 - 0.035
    for ix in (-1, 0, 1):
        for iy in (-1, 0, 1):
            if ix == 0 and iy == 0:
                continue
            parts.append(at(I.box(0.07, 0.07, 0.075, st, "merlon", 0.006), (ix * e, iy * e, h + 0.04 + 0.0375)))
    gl = K.gradient("gate", [(0.0, (1.0, 0.72, 0.25)), (0.45, ORANGE), (1.0, (0.55, 0.13, 0.02))])
    parts.append(at(I.extrude(K.door_outline(0.045, 0.075), 0.03, gl, "gate"), (0, -b / 2 + 0.012, 0.0), (5.3, 0, 0)))
    for x in (-0.085, 0.085):
        parts.append(at(I.box(0.022, 0.03, 0.065, K.glow(ORANGE, 4.0), "slit"), (x, -(b + t) / 4 + 0.006, 0.185)))
    parts.append(at(I.box(0.03, 0.022, 0.065, K.glow(ORANGE, 4.0), "slit"), ((b + t) / 4 - 0.006, 0.0, 0.185)))
    parts.append(I.lathe([(0.0, h), (0.012, h), (0.012, h + 0.36), (0.0, h + 0.365)], 10, ir, "pole"))
    pose(parts, (0, 0, -30))
    fl = pennant(0.29, 0.088, K.lit(I.cloth((0.70, 0.13, 0.012)), ORANGE, 0.5))
    at(fl, (0.008, 0, h + 0.265))
    K.lamp((-0.10, -0.24, 0.06), ORANGE, 3.0)
    return dict(fill=0.78)


@icon("waypoint", "marker")
def waypoint():
    """燼塔（傳送點）: an iron fire bowl on a stand, burning blue"""
    ir = iron()
    parts = [I.lathe([(0.0, 0.0), (0.108, 0.0), (0.114, 0.014), (0.088, 0.030), (0.046, 0.052), (0.034, 0.10), (0.032, 0.150), (0.052, 0.172),
                      (0.056, 0.188), (0.040, 0.205), (0.050, 0.225)], 28, ir, "stand")]
    bowl = I.lathe([(0.02, 0.218), (0.080, 0.228), (0.124, 0.260), (0.150, 0.306), (0.156, 0.324)], 36, ir, "bowl"); I.solid(bowl, 0.013, 1.0)
    parts.append(bowl)
    parts.append(at(I.torus(0.161, 0.015, ir, "rim", 40, 10), (0, 0, 0.324)))
    parts.append(I.lathe([(0.0, 0.314), (0.09, 0.310), (0.146, 0.296)], 24, I.ember(K.lin(CYBLUE), 1.0, 46.0, 0.45), "coals"))
    pose(parts, (0, 0, 20))
    K.fire((0, 0, 0.292), 0.37, 0.108, CYBLUE, 3, seed=4)
    K.lamp((0, -0.10, 0.40), CYBLUE, 6.0)
    return dict(fill=0.74)


@icon("beacon", "marker")
def beacon():
    """燼塔: a tall slender eight-sided beacon tower, an amber fire in the iron basket on top"""
    st = K.stone(); ir = iron()
    parts = [I.lathe([(0.0, 0.0), (0.150, 0.0), (0.155, 0.024), (0.132, 0.044), (0.108, 0.170), (0.092, 0.290), (0.092, 0.306), (0.126, 0.328),
                      (0.130, 0.352), (0.0, 0.352)], 8, st, "tower", smooth=False)]
    parts.append(at(I.box(0.028, 0.03, 0.066, K.glow(AMBER, 4.0), "slit"), (0, -0.087, 0.195)))
    parts.append(at(I.extrude(K.door_outline(0.030, 0.050), 0.03, K.void(), "door"), (0, -0.114, 0.044)))
    z0 = 0.352
    for k in range(6):
        a = 30 + 60 * k
        pts = [K.rz(q, a) for q in ((0.086, 0, z0 - 0.005), (0.124, 0, z0 + 0.032), (0.138, 0, z0 + 0.090))]
        parts.append(K.limb(pts, [0.014, 0.013, 0.008], ir, 8, 8, "prong"))
    parts.append(at(I.torus(0.127, 0.010, ir, "hoop", 32, 8), (0, 0, z0 + 0.038)))
    parts.append(I.lathe([(0.0, z0 + 0.024), (0.075, z0 + 0.020), (0.110, z0 + 0.004)], 20, I.ember(K.lin(AMBER), 1.0, 46.0, 0.45), "coals"))
    pose(parts, (0, 0, 22.5))
    K.fire((0, 0, z0), 0.27, 0.100, AMBER, 3, seed=7)
    K.lamp((0, -0.12, z0 + 0.06), AMBER, 5.0)
    return dict(fill=0.74)


@icon("brazier", "marker")
def brazier():
    """大火盆: a wide low iron fire bowl on three stubby legs, a big orange fire"""
    ir = iron()
    bowl = I.lathe([(0.03, 0.078), (0.12, 0.090), (0.195, 0.128), (0.238, 0.180), (0.246, 0.200)], 40, ir, "bowl"); I.solid(bowl, 0.015, 1.0)
    parts = [bowl, at(I.torus(0.252, 0.017, ir, "rim", 44, 10), (0, 0, 0.200))]
    for a in (-90, 30, 150):
        pts = [K.rz(p, a) for p in ((0.105, 0, 0.105), (0.165, 0, 0.060), (0.185, 0, 0.0))]
        parts.append(K.limb(pts, [0.026, 0.024, 0.030], ir, 8, 10, "leg"))
    parts.append(I.lathe([(0.0, 0.196), (0.15, 0.190), (0.236, 0.172)], 28, I.ember(K.lin(ORANGE), 1.0, 40.0, 0.45), "coals"))
    pose(parts, (0, 0, 15))
    K.fire((0, 0, 0.175), 0.42, 0.135, ORANGE, 5, seed=2, spread=0.56)
    K.lamp((0, -0.14, 0.30), ORANGE, 8.0)
    return dict(fill=0.74)


# ================================================================================================ portal / rift / event
@icon("portal", "marker")
def portal():
    """傳送門: an upright ring of stone blocks holding a swirling light-blue disc"""
    st = K.stone()
    prof = [(0.150, -0.046), (0.222, -0.046), (0.234, -0.032), (0.234, 0.032), (0.222, 0.046), (0.150, 0.046), (0.140, 0.032), (0.140, -0.032), (0.150, -0.046)]
    ring = I.lathe(prof, 14, st, "ring", smooth=False)
    disc = I.lathe([(0.0, 0.0), (0.147, 0.0)], 44, K.swirl("portal", (0.05, 0.22, 0.75), (0.30, 0.66, 1.0), (0.80, 0.94, 1.0), 0.147), "disc")
    up = pose([ring, disc], (90, 0, 0), (0, 0, 0.275), name="ring")
    parts = [up, K.frustum(0.40, 0.17, 0.33, 0.14, 0.065, st, "base")]
    for s in (-1, 1):
        parts.append(at(K.frustum(0.085, 0.13, 0.06, 0.11, 0.075, st, "foot"), (s * 0.135, 0, 0.06)))
    pose(parts, (0, 0, 20))
    K.lamp((0.0, -0.13, 0.275), LBLUE, 4.0)
    return dict(fill=0.78)


@icon("riftstone", "marker")
def riftstone():
    """裂隙石: a standing obelisk broken clean through, violet light pouring out of the crack"""
    st = K.stone(0.9)
    vio = K.glow(VIOLET, 5.0)
    a0, a1, hz, apex = 0.105, 0.066, 0.42, 0.53
    zc, m, gap = 0.215, 0.85, 0.046

    def half(z):
        return a0 + (a1 - a0) * z / hz
    cut = []
    for sx in (-1, 1):
        z = (zc + m * sx * a0) / (1 - m * sx * (a1 - a0) / hz)
        for sy in (-1, 1):
            cut.append((sx * half(z), sy * half(z), z))
    low = K.block([(sx * a0, sy * a0, 0.0) for sx in (-1, 1) for sy in (-1, 1)] + cut, st, "low", 0.007, 1)
    upb = K.block(cut + [(sx * a1, sy * a1, hz) for sx in (-1, 1) for sy in (-1, 1)] + [(0, 0, apex)], st, "up", 0.007, 2)
    core = K.block([(sx * half(z) * 0.70, sy * half(z) * 0.70, z) for z in (0.08, 0.40) for sx in (-1, 1) for sy in (-1, 1)], vio, "core")
    parts = [low, core]

    def vein(pts, r=0.0065):
        return I.tube([(x, -half(z) - 0.001, z) for (x, z) in pts], r, 6, vio, "vein")
    parts.append(vein([(-0.012, 0.185), (-0.030, 0.140), (-0.008, 0.095), (-0.026, 0.045)]))
    parts.append(vein([(0.040, 0.235), (0.052, 0.180), (0.036, 0.130)], 0.005))
    top = [upb, vein([(-0.004, 0.235), (0.018, 0.290), (-0.006, 0.340), (0.010, 0.395)]), vein([(-0.050, 0.200), (-0.040, 0.260), (-0.052, 0.305)], 0.005)]
    parts.append(pose(top, (0, -4, 4), (-0.010, 0.0, gap), name="top"))
    for k, (x, y, z, r) in enumerate(((0.150, -0.02, 0.215, 0.022), (0.128, 0.04, 0.300, 0.015), (-0.150, -0.01, 0.120, 0.017))):
        parts.append(at(I.shard(r, st, "chip", seed=k + 3), (x, y, z), (20 * k, 35, 10)))
    for k, (x, y, r) in enumerate(((-0.13, -0.06, 0.05), (0.125, -0.04, 0.042), (0.03, 0.12, 0.05))):
        parts.append(at(I.shard(r, st, "rubble", seed=k + 11, squash=(1.2, 1.0, 0.7)), (x, y, r * 0.5)))
    pose(parts, (0, 0, -25))
    K.lamp((0.02, -0.17, 0.25), VIOLET, 5.0)
    return dict(fill=0.78)


@icon("event", "marker")
def event():
    """區域事件: a spiky eight-pointed star of forge-hot iron"""
    stops = [(0.0, (1.0, 0.86, 0.48)), (0.15, (1.0, 0.72, 0.24)), (0.42, ORGOLD), (0.70, (0.92, 0.27, 0.03)), (0.92, (0.25, 0.03, 0.01)), (1.0, (0.02, 0.01, 0.01))]
    hot = K.radial("event", stops, 0.215, base=((0.035, 0.033, 0.035), 1.0, 0.42), noise=0.14, facet=0.16)
    pts = []
    for k in range(16):
        a = R(90 + k * 22.5)
        rad = 0.215 if k % 4 == 0 else (0.140 if k % 4 == 2 else 0.062)
        pts.append((rad * math.cos(a), 0.0, rad * math.sin(a)))
    vs = pts + [(0, -0.055, 0), (0, 0.055, 0)]
    fs = []
    for k in range(16):
        k2 = (k + 1) % 16
        fs.append((16, k, k2)); fs.append((17, k2, k))
    o = I.obj("star", vs, fs, hot, False)
    bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(o.data); bm.free()
    I.place(o, rot=(-8, 0, 16))
    return dict(fill=0.76)


# ================================================================================================ altar / shrine / idol / statue
@icon("altar", "marker")
def altar():
    """燼火祭壇: a low stone altar slab carrying three burning candles"""
    st = K.stone(); wax = I.look('wax', color=(0.62, 0.54, 0.38), soot=0.25)
    parts = [at(I.box(0.46, 0.24, 0.04, st, "plinth", 0.008), (0, 0, 0.02)),
             at(K.frustum(0.34, 0.17, 0.30, 0.15, 0.125, st, "body"), (0, 0, 0.04)),
             at(I.box(0.50, 0.26, 0.055, st, "slab", 0.010), (0, 0, 0.1925))]
    top = 0.22
    seats = []
    for x, hgt in ((-0.145, 0.070), (0.0, 0.115), (0.145, 0.070)):
        parts.append(at(I.lathe([(0.0, 0.0), (0.034, 0.0), (0.030, 0.006), (0.028, hgt), (0.012, hgt + 0.004), (0.0, hgt)], 18, wax, "candle"), (x, 0, top)))
        seats.append((x, 0.0, top + hgt))
    yaw = 16
    pose(parts, (0, 0, yaw))
    for k, s in enumerate(seats):
        p = K.rz(s, yaw)
        K.fire(p, 0.15 if k == 1 else 0.13, 0.045, (1.0, 0.72, 0.22), 1, seed=3 + k)
    K.lamp((0, -0.10, top + 0.12), WGOLD, 5.0)
    return dict(fill=0.76)


@icon("shrine", "marker")
def shrine():
    """神龕: a standing round-topped rune stone with a single glowing cyan mark (an upright drop round a dot)"""
    st = K.stone(); teal = (0.10, 0.95, 0.84)
    T = 0.09
    stele = I.extrude(K.door_outline(0.118, 0.275, 14), T, st, "stele", bev=0.014)
    parts = [at(stele, (0, 0, 0.04)), at(K.frustum(0.34, 0.19, 0.29, 0.15, 0.05, st, "base"), (0, 0, 0.0))]
    zc = 0.250; n = 40; a = 0.094; b = 0.098
    pts = []
    for i in range(n):
        th = 2 * math.pi * i / n
        pts.append((a * math.sin(th) * abs(math.sin(th / 2)) ** 1.5, -T / 2 - 0.003, zc + b * math.cos(th)))
    parts.append(I.tube(pts, 0.0135, 8, K.glow(teal, 4.5), "mark", closed=True))
    parts.append(at(I.ball(0.021, K.glow(teal, 4.5), "dot", 3, (1, 0.6, 1)), (0, -T / 2 - 0.004, zc - 0.036)))
    pose(parts, (0, 0, -16))
    K.lamp((-0.02, -0.14, zc), teal, 1.0)
    return dict(fill=0.78)


@icon("idol", "marker")
def idol():
    """寂滅圖騰: a carved totem column of the Hushed Order -- hollow eyes, no mouth, a red bar where it would be"""
    st = K.stone(0.85); red = K.glow(RED, 5.0)
    ax = (V((1, 0, 0)), V((0, 1, 0)))
    S = [(0.0, 0.108, 0.092), (0.036, 0.108, 0.092), (0.040, 0.078, 0.066), (0.100, 0.076, 0.064), (0.104, 0.088, 0.074), (0.128, 0.088, 0.074), (0.132, 0.075, 0.063),
         (0.196, 0.072, 0.061), (0.200, 0.098, 0.080), (0.372, 0.092, 0.076), (0.376, 0.106, 0.086), (0.402, 0.106, 0.086), (0.406, 0.080, 0.066), (0.430, 0.070, 0.058)]
    col = I.loft([((0, 0, z), rx, ry, ax, 4.0) for (z, rx, ry) in S], 28, st, "idol", caps=(True, True))
    I.flat(col, 35)
    parts = [col]
    for s in (-1, 1):
        parts.append(at(I.ball(0.030, K.void(), "socket", 3, (0.95, 0.45, 1.30)), (s * 0.040, -0.077, 0.305)))
        parts.append(at(I.ball(0.020, red, "eye", 3, (0.95, 0.5, 1.30)), (s * 0.040, -0.083, 0.302)))
    parts.append(at(I.box(0.018, 0.014, 0.062, red, "hush", 0.003), (0, -0.079, 0.238)))
    pose(parts, (0, 0, -16))
    K.lamp((-0.02, -0.17, 0.29), RED, 1.2)
    return dict(fill=0.78)


@icon("tierstatue", "marker")
def tierstatue():
    """階位石像: a small robed stone figure on a plinth, holding a pale violet wreath over its head"""
    st = K.stone(2.4, ash_amt=0.3); dk = K.stone(1.0)
    parts = [at(I.box(0.24, 0.20, 0.065, dk, "plinth", 0.008), (0, 0, 0.0325)), at(I.box(0.20, 0.17, 0.025, dk, "cap", 0.005), (0, 0, 0.0775))]
    z0 = 0.09
    robe = I.lathe([(0.0, z0), (0.082, z0), (0.078, z0 + 0.02), (0.056, z0 + 0.12), (0.060, z0 + 0.165), (0.046, z0 + 0.19), (0.024, z0 + 0.20), (0.0, z0 + 0.20)], 24, st, "robe")
    robe.scale = (1.0, 0.78, 1.0); parts.append(robe)
    parts.append(at(I.ball(0.036, st, "head", 3, (1, 1, 1.15)), (0, 0, z0 + 0.228)))
    zw = z0 + 0.345; rw = 0.078
    for s in (-1, 1):
        pts = [(s * 0.050, 0, z0 + 0.168), (s * 0.090, -0.005, z0 + 0.215), (s * 0.096, -0.005, z0 + 0.275), (s * 0.080, 0, zw - 0.020)]
        parts.append(K.limb(pts, [0.019, 0.018, 0.016, 0.017], st, 14, 10, "arm"))
    parts.append(at(I.torus(rw, 0.019, K.glow(PVIOLET, 3.5), "wreath", 40, 12), (0, 0, zw), (90, 0, 0)))
    pose(parts, (0, 0, -14))
    K.lamp((0, -0.13, zw - 0.05), (0.72, 0.55, 1.0), 4.0)
    return dict(fill=0.78)


# ================================================================================================ prisoner / elder
@icon("prisoner", "marker")
def prisoner():
    """被囚的倖存者: an iron cage with heavy bars, a pale green light alive inside it"""
    ir = iron()
    parts = [I.lathe([(0.0, 0.0), (0.142, 0.0), (0.148, 0.014), (0.142, 0.030), (0.0, 0.030)], 32, ir, "floor")]
    n = 7
    for k in range(n):
        a = -90 + 360.0 * (k + 0.5) / n
        pts = [K.rz(p, a) for p in ((0.124, 0, 0.026), (0.124, 0, 0.130), (0.124, 0, 0.235), (0.104, 0, 0.292), (0.056, 0, 0.330), (0.010, 0, 0.342))]
        parts.append(K.limb(pts, [0.0125] * 6, ir, 18, 8, "bar"))
    parts.append(at(I.torus(0.124, 0.012, ir, "hoop", 40, 8), (0, 0, 0.236)))
    parts.append(at(I.ball(0.026, ir, "knob", 2), (0, 0, 0.345)))
    parts.append(at(I.torus(0.034, 0.010, ir, "ring", 24, 8), (0, 0, 0.398), (90, 0, 0)))
    pose(parts, (0, 0, 0))
    orb = I.ball(0.060, K.orb_mat(PGREEN), "wisp", 3); at(orb, (0, 0, 0.135)); K.noshadow([orb])
    K.lamp((0, 0, 0.135), PGREEN, 6.0, 0.05)
    return dict(fill=0.78)


@icon("elder", "marker")
def elder():
    """守燈長老: an old iron lantern, warm light behind its horn panes"""
    ir = iron()
    horn = K.radial("lantern", [(0.0, (1.0, 0.95, 0.72)), (0.30, (1.0, 0.84, 0.42)), (0.62, WGOLD), (1.0, (0.90, 0.48, 0.10))], 0.105)
    hw, ph = 0.075, 0.185
    parts = [at(I.box(0.176, 0.176, 0.026, ir, "foot", 0.005), (0, 0, 0.013)), at(I.box(0.170, 0.170, 0.022, ir, "top", 0.005), (0, 0, 0.026 + ph + 0.011))]
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(at(I.box(0.024, 0.024, ph, ir, "post", 0.004), (sx * hw, sy * hw, 0.026 + ph / 2)))
    for k in range(4):
        pn = I.box(hw * 2 - 0.02, 0.006, ph, horn, "pane")
        a = 90 * k
        parts.append(at(pn, K.rz((0, -hw + 0.004, 0.026 + ph / 2), a), (0, 0, a)))
    zr = 0.026 + ph + 0.022
    roof = I.cone(0.136, 0.034, 0.078, ir, "roof", 4); roof.rotation_euler = (0, 0, R(45)); roof.location = (0, 0, zr); I.flat(roof, 20)
    parts.append(roof)
    parts.append(I.lathe([(0.0, zr + 0.076), (0.040, zr + 0.076), (0.044, zr + 0.088), (0.026, zr + 0.106), (0.0, zr + 0.110)], 16, ir, "cap"))
    pose(parts, (0, 0, 32))
    at(I.torus(0.048, 0.011, ir, "bail", 28, 8), (0, 0, zr + 0.110 + 0.040), (90, 0, 0))
    K.lamp((0, -0.20, 0.10), WGOLD, 2.0)
    return dict(fill=0.78)


# ================================================================================================ traders
@icon("smith", "marker")
def smith():
    """鐵匠: an anvil on its block, a hammer resting across the face"""
    stl = I.steel(amount=0.16, color=(0.46, 0.45, 0.44)); wd = I.wood((0.38, 0.27, 0.15)); bark = I.wood((0.19, 0.125, 0.07))
    zb = 0.105
    out = [(-0.215, 0.186), (-0.105, 0.204), (0.150, 0.204), (0.150, 0.166), (0.070, 0.148), (0.048, 0.088), (0.105, 0.058), (0.118, 0.0),
           (-0.118, 0.0), (-0.105, 0.058), (-0.048, 0.088), (-0.066, 0.146), (-0.125, 0.160)]
    anv = I.extrude(out, 0.105, stl, "anvil")
    for v in anv.data.vertices:                                   # the horn tapers to a blunt point
        if v.co.x < -0.10:
            v.co.y *= max(0.22, min(1.0, (v.co.x + 0.215) / 0.11 + 0.22))
    anv.data.update()
    I.bevel(anv, 0.007, 2)
    parts = [at(anv, (0, 0, zb))]
    parts.append(I.lathe([(0.0, 0.0), (0.150, 0.0), (0.156, 0.012), (0.152, zb - 0.010), (0.142, zb), (0.0, zb)], 20, bark, "block"))
    parts.append(at(I.lathe([(0.0, 0.0), (0.140, 0.0)], 20, wd, "blocktop"), (0, 0, zb + 0.0008)))
    zt = zb + 0.204
    ham = [at(I.box(0.060, 0.115, 0.058, I.iron(amount=0.45), "head", 0.006), (0, 0, 0.029))]
    ham.append(at(I.lathe([(0.0, 0.0), (0.0145, 0.0), (0.016, 0.20), (0.019, 0.27), (0.0, 0.275)], 12, wd, "haft"), (0, 0, 0.029), (0, 90, 0)))
    parts.append(pose(ham, (0, 0, -24), (-0.050, -0.005, zt), name="hammer"))
    pose(parts, (0, 0, 18))
    return dict(fill=0.80)


def coin(mat, k=1.0):
    o = I.lathe([(0.0, 0.0), (0.037, 0.0), (0.040, 0.003), (0.040, 0.008), (0.037, 0.011), (0.031, 0.0092), (0.0, 0.0092)], 28, mat, "coin")
    o.scale = (k, k, k)
    return o


@icon("merchant", "marker")
def merchant():
    """行商: a bulging leather coin pouch, its mouth and the ground beside it full of gold coins"""
    lea = I.leather((0.21, 0.125, 0.065), scuff_color=(0.36, 0.27, 0.18))
    gd = K.lit(bright_gold(), GOLD, 0.25)
    bag = I.lathe([(0.0, 0.0), (0.075, 0.004), (0.112, 0.032), (0.126, 0.080), (0.114, 0.132), (0.080, 0.170), (0.052, 0.190), (0.048, 0.204),
                   (0.066, 0.228), (0.084, 0.250)], 36, lea, "bag")
    I.solid(bag, 0.007, 1.0); I.displace(bag, 0.016, 0.085)
    parts = [bag, at(I.lathe([(0.0, 0.0), (0.060, 0.0)], 20, K.void(), "dark"), (0, 0, 0.214))]
    parts.append(at(I.torus(0.053, 0.008, I.cloth((0.05, 0.04, 0.035)), "cord", 28, 8), (0, 0, 0.198)))
    parts.append(K.limb([(0.048, -0.024, 0.198), (0.078, -0.046, 0.170), (0.082, -0.054, 0.118)], [0.007, 0.007, 0.009], I.cloth((0.05, 0.04, 0.035)), 10, 8, "tail"))
    for (x, y, z, rx, rz_) in ((-0.020, 0.012, 0.232, 38, 20), (0.026, -0.004, 0.238, 30, -40), (0.000, -0.030, 0.228, 52, 0)):
        parts.append(at(coin(gd, 1.15), (x, y, z), (rx, 0, rz_)))
    for k in range(4):
        parts.append(at(coin(gd, 1.3), (0.176 + 0.004 * (k % 2), -0.030 - 0.005 * k, 0.0143 * k), (0, 0, 40 * k)))
    parts.append(at(coin(gd, 1.35), (0.110, -0.128, 0.054), (80, 0, 14)))
    parts.append(at(coin(gd, 1.3), (-0.030, -0.172, 0.0), (0, 0, 0)))
    parts.append(at(coin(gd, 1.3), (-0.150, -0.085, 0.050), (72, 0, -28)))
    pose(parts, (0, 0, 8))
    return dict(fill=0.80)


@icon("occultist", "marker")
def occultist():
    """祕術師: a crystal ball in a small clawed stand, violet mist turning inside it"""
    br = I.brass()
    zc, rb = 0.205, 0.108
    inner = I.ball(rb * 0.86, K.mist("occ", (0.07, 0.02, 0.34), VIOLET, (0.80, 0.72, 1.0)), "mist", 4); at(inner, (0, 0, zc))
    shell = I.ball(rb, I.glass((0.93, 0.88, 1.0), 0.0), "ball", 4); at(shell, (0, 0, zc))
    parts = [inner, shell]
    parts.append(I.lathe([(0.0, 0.0), (0.090, 0.0), (0.096, 0.012), (0.074, 0.028), (0.042, 0.044), (0.036, 0.070), (0.056, 0.092), (0.064, 0.104), (0.0, 0.104)], 28, br, "stand"))
    for a in (-30, -150, 90):
        pts = []
        for ph in (-78, -50, -20, 8, 30):
            rr = rb + 0.009
            pts.append(K.rz((rr * math.cos(R(ph)), 0, zc + rr * math.sin(R(ph))), a))
        parts.append(K.limb(pts, [0.016, 0.015, 0.013, 0.011, 0.006], br, 16, 8, "claw"))
    pose(parts, (0, 0, 0))
    K.lamp((0, -0.10, 0.075), VIOLET, 2.5)
    return dict(fill=0.78, samples=64)


@icon("jeweler", "marker")
def jeweler():
    """琢匠: a large cut cyan gem in a claw setting on a heavy ring"""
    sv = I.silver()
    parts = [I.torus(0.085, 0.019, sv, "band", 56, 14)]
    bez = [I.cone(0.034, 0.056, 0.036, sv, "bezel", 24)]
    for i in range(6):
        a = 2 * math.pi * (i + 0.5) / 6
        bez.append(K.limb([(0.040 * math.cos(a), 0.040 * math.sin(a), 0.020), (0.078 * math.cos(a), 0.078 * math.sin(a), 0.050), (0.070 * math.cos(a), 0.070 * math.sin(a), 0.082)],
                          [0.008, 0.008, 0.006], sv, 8, 8, "claw"))
    gm = I.hull(PI.cut_round(0.074, 8, table=0.56, crown=0.34, pav=0.62), I.gem((0.06, 0.78, 1.0), 1.75, glow=1.1), "gem")
    bez.append(at(gm, (0, 0, 0.066)))
    parts.append(pose(bez, (90, 0, 0), (0, -0.098, 0), name="setting"))
    pose(parts, (-58, 0, 22))
    return dict(fill=0.78, samples=64)


# ================================================================================================ chest / stash
@icon("chest", "marker")
def chest():
    """寶箱: a small closed treasure chest, dark wood bound in gold, gold light leaking under the lid"""
    wd = I.wood((0.11, 0.07, 0.04), axis=(1, 0, 0)); gd = K.lit(bright_gold(), GOLD, 0.16)
    Wd, Dp, Hb = 0.30, 0.21, 0.135
    zl = Hb + 0.014
    parts = [at(I.box(Wd, Dp, Hb, wd, "body", 0.004), (0, 0, Hb / 2))]
    parts.append(at(I.extrude(K.door_outline(Dp / 2, 0.014, 12, 0.80), Wd, wd, "lid", bev=0.004), (0, 0, zl), (0, 0, 90)))
    for x in (-0.098, 0.098):
        parts.append(at(I.box(0.042, Dp + 0.010, Hb + 0.004, gd, "band", 0.003), (x, 0, Hb / 2)))
        parts.append(at(I.extrude(K.door_outline(Dp / 2 + 0.005, 0.014, 12, 0.80), 0.042, gd, "lband", bev=0.003), (x, 0, zl), (0, 0, 90)))
    for z, hh in ((0.010, 0.020), (Hb - 0.008, 0.016), (zl + 0.007, 0.016)):
        parts.append(at(I.box(Wd + 0.012, Dp + 0.012, hh, gd, "rim", 0.003), (0, 0, z)))
    parts.append(at(I.box(Wd - 0.004, Dp - 0.004, 0.018, K.glow(GOLD, 4.0), "seam"), (0, 0, Hb + 0.006)))
    parts.append(at(I.box(0.064, 0.018, 0.090, gd, "lock", 0.004), (0, -Dp / 2 - 0.006, Hb - 0.008)))
    parts.append(at(I.ball(0.015, K.glow(PGOLD, 4.0), "keyhole", 2, (1, 0.6, 1.3)), (0, -Dp / 2 - 0.016, Hb - 0.012)))
    pose(parts, (7, 0, -28))
    K.lamp((-0.10, -0.24, Hb + 0.01), GOLD, 2.0)
    return dict(fill=0.80)


@icon("stash", "marker")
def stash():
    """倉庫: a large plain trunk of pale tan boards bound in dark iron, flat lid, carrying ring on the end"""
    tan = I.wood((0.46, 0.33, 0.18), axis=(1, 0, 0)); ir = iron()
    Wd, Dp, Hb, Hl = 0.47, 0.25, 0.185, 0.058
    zl = Hb + 0.004 + Hl / 2
    parts = [at(I.box(Wd, Dp, Hb, tan, "body", 0.005), (0, 0, Hb / 2)), at(I.box(Wd + 0.016, Dp + 0.016, Hl, tan, "lid", 0.008), (0, 0, zl))]
    for x in (-0.135, 0.135):
        parts.append(at(I.box(0.046, Dp + 0.010, Hb + 0.002, ir, "strap", 0.003), (x, 0, Hb / 2)))
        parts.append(at(I.box(0.046, Dp + 0.026, Hl + 0.008, ir, "lstrap", 0.003), (x, 0, zl)))
        parts.append(at(I.box(0.030, 0.014, 0.060, ir, "hasp", 0.003), (x, -Dp / 2 - 0.014, Hb - 0.012)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            parts.append(at(I.box(0.052, 0.052, 0.050, ir, "corner", 0.004), (sx * (Wd / 2 - 0.021), sy * (Dp / 2 - 0.021), 0.024)))
            parts.append(at(I.box(0.052, 0.052, Hl + 0.008, ir, "lcorner", 0.004), (sx * (Wd / 2 - 0.013), sy * (Dp / 2 - 0.013), zl)))
    parts.append(at(I.box(0.062, 0.014, 0.080, ir, "lock", 0.004), (0, -Dp / 2 - 0.007, Hb - 0.016)))
    parts.append(at(I.box(0.014, 0.070, 0.030, ir, "mount", 0.003), (Wd / 2 + 0.007, 0, Hb * 0.66)))
    parts.append(at(I.torus(0.040, 0.009, ir, "handle", 28, 8), (Wd / 2 + 0.018, 0, Hb * 0.66 - 0.036), (0, 90, 0)))
    pose(parts, (7, 0, -28))
    return dict(fill=0.80)


# ================================================================================================ people
def skin():
    key = "mk_skin"
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    b.inputs['Base Color'].default_value = (0.47, 0.32, 0.225, 1)
    b.inputs['Roughness'].default_value = 0.62
    I._MATS[key] = m
    return m


def bust(cl, collar=None, collar_prof=None):
    """peg-like hooded head and shoulders: a plain round head inside a hood that is open at the front"""
    ax = (V((1, 0, 0)), V((0, 1, 0)))
    S = [(0.0, 0.152, 0.090), (0.050, 0.162, 0.096), (0.105, 0.146, 0.092), (0.146, 0.100, 0.076), (0.176, 0.062, 0.058)]
    parts = [I.loft([((0, 0, z), rx, ry, ax, 2.4) for (z, rx, ry) in S], 32, cl, "body", caps=(True, True))]
    top = I.lathe([(0.0, 0.384), (0.024, 0.372), (0.054, 0.342), (0.076, 0.306)], 32, cl, "hood_top")
    for v in top.data.vertices:                                    # the point of the hood falls back
        v.co.y += (v.co.z - 0.306) * 0.45
    top.data.update()
    gap = 84.0
    mid = I.lathe([(0.076, 0.306), (0.088, 0.270), (0.093, 0.236), (0.089, 0.200), (0.076, 0.172)], 32, cl, "hood_mid", arc=360.0 - gap)
    I.solid(mid, 0.014)
    mid.rotation_euler = (0, 0, R(-90 + gap / 2))
    low = I.lathe([(0.076, 0.172), (0.062, 0.158), (0.046, 0.150), (0.0, 0.150)], 32, cl, "hood_low")
    parts += [top, mid, low]
    parts.append(at(I.ball(0.074, K.void(), "inside", 3, (1.0, 1.0, 1.25)), (0, 0.004, 0.236)))
    parts.append(at(I.ball(0.060, skin(), "face", 3, (1.0, 0.92, 1.16)), (0, -0.020, 0.232)))
    prof = collar_prof or [(0.060, 0.150), (0.104, 0.150), (0.118, 0.165), (0.110, 0.186), (0.074, 0.196)]
    co = I.lathe(prof, 30, collar or cl, "collar"); co.scale = (1.0, 0.86, 1.0)
    parts.append(co)
    return parts


@icon("villager", "marker")
def villager():
    """鎮民: a plain hooded figure, head and shoulders, in muted grey-green"""
    parts = bust(I.cloth((0.20, 0.26, 0.19)))
    pose(parts, (0, 0, 12))
    return dict(fill=0.80)


@icon("npc", "marker")
def npc():
    """someone to talk to: the same figure in a warm grey cloak with a bright green scarf"""
    gr = K.lit(I.cloth((0.09, 0.52, 0.11)), GREEN, 0.16)
    parts = bust(I.cloth((0.19, 0.165, 0.14)), gr, [(0.060, 0.142), (0.116, 0.140), (0.136, 0.164), (0.124, 0.198), (0.074, 0.210)])
    parts.append(at(I.box(0.066, 0.018, 0.150, gr, "scarf", 0.005), (0.052, -0.098, 0.090), (-5, 0, 5)))
    pose(parts, (0, 0, 12))
    return dict(fill=0.80)


# ================================================================================================ start / hero
@icon("start", "marker")
def start():
    """來處: a small ring of standing stones on a round slab, plain grey"""
    st = K.stone(1.25)
    rnd = random.Random(5)
    parts = [I.lathe([(0.0, 0.0), (0.205, 0.0), (0.212, 0.016), (0.196, 0.034), (0.0, 0.034)], 14, st, "slab", smooth=False)]
    n = 6
    for k in range(n):
        a = 360.0 * k / n + 30
        w = rnd.uniform(0.060, 0.078); d = rnd.uniform(0.046, 0.058); h = rnd.uniform(0.105, 0.165)
        s = K.frustum(w, d, w * rnd.uniform(0.62, 0.8), d * rnd.uniform(0.6, 0.8), h, st, "stone")
        p = K.rz((0.148, 0, 0.030), a)
        parts.append(at(s, p, (rnd.uniform(-5, 5), rnd.uniform(-5, 5), a + 90 + rnd.uniform(-14, 14))))
    parts.append(at(I.shard(0.045, st, "mark", seed=4, squash=(1.3, 1.1, 0.45)), (0, 0, 0.040)))
    pose(parts, (30, 0, 0))
    return dict(fill=0.80)


@icon("hero", "marker")
def hero():
    """你的位置: a bold pale-gold arrowhead pointing up (the game turns it to show the facing)"""
    pg = K.lit(I.look('metal', color=(0.74, 0.60, 0.33), rust=(0.22, 0.16, 0.08), rust2=(0.10, 0.08, 0.05), amount=0.14, rough=0.27,
                      edge_color=(1.0, 0.92, 0.66)), PGOLD, 0.20)
    out = [(0.0, 0.215), (0.150, -0.165), (0.0, -0.075), (-0.150, -0.165)]
    parts = [I.extrude(out, 0.06, pg, "arrow", bev=0.014)]
    k = 0.52
    inl = I.extrude([(x * k, (z + 0.012) * k) for (x, z) in out], 0.012, K.glow(PGOLD, 3.0), "inlay")
    parts.append(at(inl, (0, -0.030, 0)))
    pose(parts, (-14, 0, 0))
    return dict(fill=0.80)
