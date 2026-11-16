"""hero_emberblade  -- heavy plate warden with a beacon-crown great helm, fur mantle, tabard and greatsword."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from charlib import *

CID = "hero_emberblade"
c = start("male")
B = c.B

# ------------------------------------------------------------------ source materials
mats.metal("steel", base=(0.235, 0.23, 0.225), rough=(0.30, 0.58), rust_amt=0.22, pattern="hammered", soot=0.25)
mats.metal("mail", base=(0.115, 0.115, 0.12), rough=(0.45, 0.7), pattern="mail", edge_amt=0.0, rust_amt=0.12, bump=0.5)
mats.metal("bronze", base=(0.36, 0.215, 0.075), rough=(0.28, 0.5), rust=(0.045, 0.085, 0.06), rust_amt=0.22, ash=0.1)
mats.metal("iron", base=(0.19, 0.18, 0.17), rough=(0.45, 0.72), rust_amt=0.4, ash=0.0, pattern="hammered")
mats.metal("blade", base=(0.43, 0.43, 0.44), rough=(0.2, 0.42), rust_amt=0.08, ash=0.0, pattern="brushed", edge_amt=1.0)
mats.metal("blade_dark", base=(0.14, 0.13, 0.13), rough=(0.25, 0.5), rust_amt=0.05, ash=0.0, pattern="brushed",
           edge=(0.55, 0.5, 0.45), edge_amt=1.0, soot=0.3)
mats.cloth("tabard", base=(0.115, 0.022, 0.016), bands=[(0.03, 0.085, (0.24, 0.15, 0.05))], folds=0.5, ash=0.6)
mats.cloth("emblem", base=(0.27, 0.17, 0.055), folds=0.0, ash=0.2, var=0.2)
mats.cloth("cloak", base=(0.05, 0.036, 0.03), folds=0.9, ash=0.7, ash_h=0.9)
mats.fur("fur", base=(0.045, 0.036, 0.03), tip=(0.21, 0.175, 0.14))
mats.leather("leather", base=(0.055, 0.032, 0.018))
mats.emis("ember", (1.0, 0.32, 0.04), strength=6.0)

# ------------------------------------------------------------------ under layer: mail
m_mail = (~m_head(B)) & (~m_hands(B, 0.70)) & (B.z < 1.56)
mail = shell(B, m_mail, 0.010, "mail", "metal", "mail", smooth=2, sharp=180)
drop_skin(c)

# ------------------------------------------------------------------ torso plates
def ridge(amount, width=0.035):
    def mod(U, V, x, y, z):
        f = np.exp(-((U - 0.75) / width) ** 2)
        return x, y - amount * f, z
    return mod

g = g_stations([(1.20, 0, 0.012, 0.172, 0.152, 0.138), (1.26, 0, 0.018, 0.196, 0.164, 0.152),
                (1.34, 0, 0.026, 0.224, 0.176, 0.170), (1.42, 0, 0.032, 0.216, 0.168, 0.166),
                (1.48, 0, 0.035, 0.162, 0.128, 0.142), (1.515, 0, 0.03, 0.108, 0.098, 0.108)],
               nu=28, nv=9, p=2.3, mod=ridge(0.014))
breast = add("breastplate", g, "metal", "steel", solidify=0.007, bone="spine_03", sharp=50)
g = g_stations([(1.06, 0, 0.0, 0.160, 0.126, 0.120), (1.14, 0, 0.0, 0.158, 0.128, 0.120), (1.235, 0, 0.012, 0.168, 0.144, 0.130)],
               nu=28, nv=4, p=2.3, mod=ridge(0.008))
add("plackart", g, "metal", "steel", solidify=0.006, bone="spine_02", sharp=50)
g = g_stations([(0.995, 0, 0.02, 0.180, 0.146, 0.146), (1.085, 0, 0.004, 0.164, 0.132, 0.126)], nu=28, nv=3, p=2.3)
add("fauld1", g, "metal", "steel", solidify=0.006, bone="spine_01", sharp=50)
g = g_stations([(0.905, 0, 0.022, 0.196, 0.160, 0.160), (1.01, 0, 0.022, 0.182, 0.148, 0.148)], nu=28, nv=3, p=2.3)
add("fauld2", g, "metal", "steel", solidify=0.006, bone="pelvis", sharp=50)
# gorget
g = g_stations([(1.455, 0, 0.03, 0.150, 0.125, 0.135), (1.50, 0, 0.025, 0.108, 0.104, 0.112), (1.575, 0, 0.015, 0.102, 0.110, 0.112)],
               nu=20, nv=5)
gor = add("gorget", g, "metal", "steel", solidify=0.006, sharp=50)
w_func(gor, lambda co: {"neck_01": sstep(co[:, 2], 1.49, 1.54), "spine_03": 1 - sstep(co[:, 2], 1.49, 1.54)})

# belt + buckle
belt(c, 1.035, "leather", width=0.055, rx=0.181, ry=0.148, buckle="bronze", bone="spine_01")

# ------------------------------------------------------------------ tabard (front) + emblem
def tab_mod(U, V, x, y, z):
    t = (U - 0.665) / 0.17
    return x, y, z - 0.085 * (1 - np.abs(2 * t - 1)) * (1 - V) ** 3
g = g_stations([(0.53, 0, 0.02, 0.262, 0.212, 0.2), (0.80, 0, 0.02, 0.226, 0.194, 0.2), (1.045, 0, 0.02, 0.192, 0.166, 0.16)],
               nu=11, nv=12, close=False, u0=0.665, u1=0.835, mod=tab_mod)
tab = add("tabard", g, "cloth", "tabard", solidify=0.006, sharp=180, double_sided=True)
def tab_w(co):
    z, x = co[:, 2], co[:, 0]
    t = sstep(1.0 - z, 0.0, 0.28) * 0.88          # 0 at z>=1.0, grows downward
    l = sstep(x, -0.05, 0.05)
    return {"pelvis": 1 - t, "thigh_l": t * l, "thigh_r": t * (1 - l)}
w_func(tab, tab_w)
# ember-beacon emblem: a flame with three tongues above a small bar, conformed to the tabard
outl = [(-0.050, -0.055), (0.050, -0.055), (0.050, -0.040), (0.030, -0.040), (0.046, 0.000), (0.030, 0.040), (0.020, 0.012),
        (0.000, 0.075), (-0.020, 0.012), (-0.030, 0.040), (-0.046, 0.000), (-0.030, -0.040), (-0.050, -0.040)]
ge = g_prism(outl, 0.004)
ge = gxf(ge, T(0, 0, 0.80))
emb = add("emblem", ge, "cloth", "emblem", sharp=40)
apply_mods(tab)
co = get_co(emb)
for i, p in enumerate(co):
    ok, loc, nrm, fi = tab.ray_cast(Vector((p[0], -1.0, p[2])), Vector((0, 1, 0)))
    if ok:
        co[i, 1] = loc[1] + (-0.0045 if p[1] < 0 else -0.0005)
set_co(emb, co)
w_transfer(emb, tab, smooth=0)

# tassets (thigh plates hanging from the fauld), pointed at the bottom
def tasset_fn(U, V):
    a = TAU * (0.655 + 0.31 * U)
    r = 0.104 + 0.016 * (1 - V)
    z = 0.925 - V * (0.20 + 0.075 * (1 - (2 * U - 1) ** 2))
    return np.stack([0.092 + r * np.cos(a), 0.005 + r * np.sin(a), z], axis=-1)
tas = add("tasset_l", g_grid(tasset_fn, 7, 5, close_u=False), "metal", "steel", solidify=0.006, bone="thigh_l", sharp=50)
mirrored(tas)

# ------------------------------------------------------------------ cloak + fur mantle
ck = cloak(c, "cloak", z_top=1.40, z_bot=0.60, width=0.235, depth_top=0.175, depth_bot=0.255, arc=0.44, flare=0.05,
           ragged=0.05, nu=26, nv=12, pleats=5.0, pleat_amp=0.07, top_taper=1.0)
def cloak_legs(W, col, co):
    z, x = co[:, 2], co[:, 0]
    t = sstep(0.95 - z, 0.0, 0.3) * 0.35
    l = sstep(x, -0.06, 0.06)
    W = W * (1 - t)[:, None]
    W[:, col["thigh_l"]] += t * l
    W[:, col["thigh_r"]] += t * (1 - l)
    return W
w_mix(ck, cloak_legs)
# fur cape over the upper back
def fur_cape(U, V):
    z = 1.545 + (1.20 - 1.545) * V
    w = 0.255 - 0.02 * V
    d = 0.165 + 0.045 * np.sin(V * 3.0)
    x, y = ell(U, 0.0, 0.03, w, d, d, 2.2)
    return np.stack([x, y, z], axis=-1)
g = g_grid(fur_cape, 18, 7, close_u=False, u0=0.02, u1=0.48)
fc = add("fur_cape", g, "cloth", "fur", sharp=180, solidify=0.022, subsurf=1)
apply_mods(fc)
displace_noise(fc, 0.014, 30.0, 5)
w_func(fc, lambda co: {"spine_03": 1.0})
# fur collar roll
path = []
rad = []
for i in range(20):
    a = TAU * i / 20
    s, co_ = math.sin(a), math.cos(a)
    zz = 1.545 + (0.012 * s if s > 0 else 0.085 * s)
    path.append((0.190 * co_, 0.03 + (0.155 * s if s > 0 else 0.150 * s), zz))
    rad.append(0.056 if s > -0.2 else 0.040)
fr = fur_roll(path, rad, "fur", name="fur_collar", bone="spine_03", noise=0.016, n=10)

# ------------------------------------------------------------------ helm (rigid to Head)
HC = np.array((0.0, -0.022, 0.0))
HS, HZ = 1.10, 1.07
def helm_shape(g, grow=0.0):
    v = g["v"].copy()
    r = np.sqrt(v[:, 0] ** 2 + v[:, 1] ** 2) + 1e-9
    v[:, 0] *= (r * HS + grow) / r
    v[:, 1] *= (r * HS + grow) / r * 1.19
    v[:, 2] = 1.538 + (v[:, 2] - 1.538) * HZ
    front = np.clip(-v[:, 1] / 0.14, 0, 1)
    v[:, 1] -= 0.022 * np.exp(-(v[:, 0] / 0.045) ** 2) * front
    g = dict(g)
    g["v"] = v + HC
    return g
prof = [(0.119, 1.538), (0.114, 1.548), (0.117, 1.60), (0.120, 1.68), (0.118, 1.745), (0.108, 1.795), (0.086, 1.828),
        (0.046, 1.848), (0.0, 1.853)]
helm = add("helm", helm_shape(g_lathe(prof, n=24)), "metal", "steel", bone="Head", sharp=45)
# brow band + crown band (bronze), nasal bar
add("helm_brow", helm_shape(g_lathe([(0.1205, 1.703), (0.1215, 1.730)], n=24), 0.0015), "metal", "bronze",
    solidify=0.006, bone="Head", sharp=45)
add("helm_crown", helm_shape(g_lathe([(0.1085, 1.792), (0.104, 1.812)], n=24), 0.0015), "metal", "bronze",
    solidify=0.007, bone="Head", sharp=45)
add("helm_rim", helm_shape(g_lathe([(0.1185, 1.536), (0.1145, 1.556)], n=24), 0.0015), "metal", "bronze",
    solidify=0.006, bone="Head", sharp=45)
gb = g_box(0.026, 0.012, 0.165)
gb = gxf(gb, T(0, HC[1] - 0.120 * 1.19 * HS - 0.022 - 0.004, 1.538 + (1.622 - 1.538) * HZ) @ S(1, 1, HZ))
add("helm_nasal", gb, "metal", "bronze", bevel=0.003, bone="Head", sharp=40)
# visor slit (emissive), split by the nasal bar
for s in (1, -1):
    gs = g_lathe([(0.1203, 1.682), (0.1205, 1.701)], n=10, arc=0.118, start=(0.75 + 0.012) if s > 0 else (0.75 - 0.012 - 0.118))
    add("visor_%d" % s, helm_shape(gs, 0.0012), "emis", "ember", bone="Head", sharp=180, double_sided=True)
# dark recess band behind the slit (so the glow sits in a shadowed gap)
# crown tines: a ring of short iron spikes, like the rim of a beacon brazier
tines = []
for i in range(11):
    a = TAU * (i + 0.5) / 11
    gsp = g_lathe([(0.012, 0.0), (0.009, 0.035), (0.0, 0.078)], n=4)
    out = np.array((math.cos(a), math.sin(a), 0.0))
    gsp = gxf(gsp, frame(np.array((math.cos(a) * 0.104, math.sin(a) * 0.104, 1.806)), out * 0.28 + np.array((0, 0, 1.0)), out))
    tines.append(gsp)
add("helm_tines", helm_shape(gjoin(*tines)), "metal", "steel", bone="Head", sharp=30)

# ------------------------------------------------------------------ pauldrons (left, mirrored)
SH = np.array((0.222, 0.062, 1.478))
Mp = T(*SH) @ R('Y', 20)
gp = g_pauldron(0.150, 0.150, 0.088, th_out=104, th_in=52, th_fb=88, nu=18)
p1 = add("pauldron_l", gxf(gp, Mp), "metal", "steel", solidify=0.007, sharp=40)
weld(p1)
p3 = add("pauldron_trim_l", g_tube(xf(rim_of(gp, 18), Mp), 0.0085, n=6, closed=True), "metal", "bronze", sharp=60)
# raised neck guard (haute-piece) on the inner edge
def haute(U, V):
    a = TAU * (0.30 + 0.40 * U)
    r = 0.105
    return np.stack([-0.02 + r * np.cos(a) * 0.55, r * np.sin(a) * 1.25, 0.045 + V * (0.075 - 0.03 * (2 * U - 1) ** 2)], axis=-1)
p2 = add("pauldron_guard_l", gxf(g_grid(haute, 9, 3, close_u=False), Mp), "metal", "bronze", solidify=0.006, sharp=50)
for o in (p1, p2, p3):
    w_func(o, lambda co: {"clavicle_l": 0.62, "upperarm_l": 0.38})
    mirrored(o)

# ------------------------------------------------------------------ arms
def limb_plate(name, mask, bone, off=0.018, sm=4, src="steel"):
    o = shell(B, mask, off, name, "metal", src, smooth=sm, sharp=60, solidify=0.0)
    w_rigid(o, bone)
    return o
L = B.x > 0
rere = limb_plate("rerebrace_l", L & (B.w("upperarm") > 0.5) & (B.x > 0.275) & (B.x < 0.435), "upperarm_l", 0.024)
vamb = limb_plate("vambrace_l", L & (B.w("lowerarm") > 0.5) & (B.x > 0.50) & (B.x < 0.725), "lowerarm_l", 0.024)
mirrored(rere)
mirrored(vamb)
gc = g_sphere(0.060, 10, 6, sx=0.85, sy=1.0, sz=0.95)
cout = add("couter_l", gxf(gc, T(0.468, 0.092, 1.432)), "metal", "steel", sharp=60)
w_func(cout, lambda co: {"upperarm_l": 0.5, "lowerarm_l": 0.5})
mirrored(cout)
# flared gauntlet cuff
gcf = g_lathe([(0.084, 0.0), (0.068, 0.055), (0.058, 0.10)], n=12)
cuff = add("cuff_l", gxf(gcf, frame((0.635, 0.068, 1.437), (1, 0, 0))), "metal", "bronze", solidify=0.006, bone="lowerarm_l", sharp=50)
mirrored(cuff)
gloves(c, "steel", cuff=0.715, offset=0.006, cls="metal", name="gauntlets", smooth=1)

# ------------------------------------------------------------------ legs
cuis = limb_plate("cuisse_l", L & (B.w("thigh") > 0.5) & (B.z > 0.585) & (B.z < 0.89), "thigh_l", 0.030)
grv = limb_plate("greave_l", L & (B.w("calf") > 0.5) & (B.z > 0.11) & (B.z < 0.505), "calf_l", 0.028)
mirrored(cuis)
mirrored(grv)
gk = g_sphere(0.086, 10, 6, sx=0.95, sy=0.85, sz=1.05)
pol = add("poleyn_l", gxf(gk, T(0.090, -0.036, 0.532)), "metal", "steel", sharp=60)
w_func(pol, lambda co: {"thigh_l": 0.45, "calf_l": 0.55})
mirrored(pol)
gw = g_prism([(0.0, -0.05), (0.055, -0.03), (0.062, 0.02), (0.0, 0.05)], 0.008)
wing = add("poleyn_wing_l", gxf(gw, T(0.168, -0.02, 0.532) @ R('Z', 90)), "metal", "bronze", bevel=0.002, sharp=40)
w_func(wing, lambda co: {"thigh_l": 0.45, "calf_l": 0.55})
mirrored(wing)
boots(c, "steel", top=0.16, offset=0.022, cls="metal", name="sabatons", smooth=2, toe_cap=True)

# ------------------------------------------------------------------ greatswords (weapon local: +Z blade, X = edge)
G = grip("r")
def sword(owner, blade_st, blade_src, guard, pommel, extras=()):
    add(owner + "_blade", gxf(g_blade(blade_st), G), "metal", blade_src, owner=owner, bone="hand_r", sharp=30)
    gt_ = g_tube([(0, 0, -0.105), (0, 0, 0.0), (0, 0, 0.150)], [0.0165, 0.0185, 0.0165], n=8)
    add(owner + "_grip", gxf(gt_, G), "leather", "leather", owner=owner, bone="hand_r", sharp=60)
    for (g_, cls, src, kw) in list(guard) + list(pommel) + list(extras):
        add(owner + "_p%d" % len(mk.PARTS), gxf(g_, G), cls, src, owner=owner, bone="hand_r", **kw)

# tier 1: plain iron
sword("wpn_1",
      [(0.165, 0.036, 0.012), (0.60, 0.034, 0.011), (1.05, 0.029, 0.009), (1.21, 0.015, 0.006), (1.28, 0, 0)], "iron",
      guard=[(g_box(0.27, 0.030, 0.034, (0, 0, 0.158)), "metal", "iron", dict(bevel=0.005, sharp=40))],
      pommel=[(gxf(g_sphere(0.032, 8, 5, sy=0.7), T(0, 0, -0.128)), "metal", "iron", dict(sharp=60))])
# tier 2: engraved steel, bronze furniture
quill = [(-0.185, 0, 0.205), (-0.13, 0, 0.176), (-0.06, 0, 0.162), (0, 0, 0.158), (0.06, 0, 0.162), (0.13, 0, 0.176), (0.185, 0, 0.205)]
sword("wpn_2",
      [(0.175, 0.046, 0.013), (0.30, 0.041, 0.013), (0.75, 0.039, 0.012), (1.12, 0.033, 0.010), (1.29, 0.017, 0.007), (1.38, 0, 0)], "blade",
      guard=[(g_tube(quill, [0.012, 0.015, 0.018, 0.021, 0.018, 0.015, 0.012], n=8, sy=0.8), "metal", "bronze", dict(sharp=60)),
             (g_box(0.07, 0.036, 0.05, (0, 0, 0.165)), "metal", "bronze", dict(bevel=0.006, sharp=40))],
      pommel=[(gxf(g_lathe([(0, -0.02), (0.036, -0.012), (0.040, 0.0), (0.036, 0.012), (0, 0.02)], n=10), T(0, 0, -0.130) @ R('X', 90)),
               "metal", "bronze", dict(sharp=50))],
      extras=[(g_box(0.013, 0.0155, 0.52, (0, 0, 0.47)), "metal", "bronze", dict(bevel=0.002, sharp=40)),
              (g_lathe([(0.020, 0.02), (0.022, 0.035), (0.020, 0.05)], n=8), "metal", "bronze", dict(sharp=60)),
              (g_lathe([(0.020, -0.07), (0.022, -0.055), (0.020, -0.04)], n=8), "metal", "bronze", dict(sharp=60))])
# tier 3: ember-fullered legendary blade
cres_l = [(-0.03, 0, 0.165), (-0.10, 0, 0.170), (-0.165, 0, 0.205), (-0.205, 0, 0.270)]
cres_r = [(-x, y, z) for (x, y, z) in cres_l]
sword("wpn_3",
      [(0.185, 0.032, 0.016), (0.30, 0.032, 0.016), (0.335, 0.066, 0.013), (0.385, 0.047, 0.015), (0.56, 0.052, 0.014),
       (1.08, 0.046, 0.011), (1.30, 0.031, 0.008), (1.44, 0, 0)], "blade_dark",
      guard=[(g_tube(cres_l, [0.021, 0.019, 0.015, 0.006], n=8, sy=0.8), "metal", "bronze", dict(sharp=60)),
             (g_tube(cres_r, [0.021, 0.019, 0.015, 0.006], n=8, sy=0.8), "metal", "bronze", dict(sharp=60)),
             (g_box(0.075, 0.040, 0.055, (0, 0, 0.168)), "metal", "bronze", dict(bevel=0.007, sharp=40)),
             (gxf(g_gem(0.020, 0.05, 6, 0.5), T(0, -0.022, 0.168) @ R('X', 90)), "emis", "ember", dict(sharp=20)),
             (gxf(g_gem(0.020, 0.05, 6, 0.5), T(0, 0.022, 0.168) @ R('X', -90)), "emis", "ember", dict(sharp=20))],
      pommel=[(gxf(g_lathe([(0.012, 0.03), (0.030, 0.012), (0.034, -0.006), (0.020, -0.030), (0, -0.04)], n=8), T(0, 0, -0.135)),
               "metal", "bronze", dict(sharp=50))],
      extras=[(g_blade([(0.42, 0.0085, 0.0185), (1.00, 0.0075, 0.0150), (1.16, 0.0, 0.0)], ridge=1.0), "emis", "ember", dict(sharp=20)),
              (g_lathe([(0.020, 0.02), (0.022, 0.035), (0.020, 0.05)], n=8), "metal", "bronze", dict(sharp=60))])

finalize(c, CID, tex={"metal": 1024, "cloth": 1024, "leather": 512},
         notes="Pose-tested: Sword_Idle, Idle_Loop, Sword_Regular_A, Jog_Fwd_Loop, Roll. Weapons wpn_1..3 = greatswords "
               "skinned 100% to hand_r (fist grip of the UAL sword clips).")
