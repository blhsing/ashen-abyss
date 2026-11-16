"""Skill vignettes rendered from real models: hero weapons and props with emissive effects."""
import bpy, math, random
from mathutils import Vector, Euler
import iconlib as I
from iconlib import R
from registry import icon, reg
import props_items as PI

FIRE = (1.0, 0.42, 0.08)
FROST = (0.45, 0.80, 1.0)


def slash_arc(r=0.6, a0=20, a1=150, w=0.10, col_hot=(1.0, 0.9, 0.6), col=(1.0, 0.35, 0.05), loc=(0, 0.05, 0), rot=(0, 0, 0), strength=10.0):
    """crescent swipe: a flat arc band whose emission fades toward its tail"""
    n = 40
    vs = []; fs = []
    for i in range(n + 1):
        t = i / n
        a = R(a0 + (a1 - a0) * t)
        ww = w * math.sin(math.pi * t) ** 0.6 * (0.35 + 0.65 * t)
        vs.append(((r - ww * 0.5) * math.cos(a), 0, (r - ww * 0.5) * math.sin(a)))
        vs.append(((r + ww * 0.5) * math.cos(a), 0, (r + ww * 0.5) * math.sin(a)))
    for i in range(n):
        fs.append((2 * i, 2 * i + 1, 2 * i + 3, 2 * i + 2))
    m = I.fire(col_hot, col, strength, axis='X', key="slash%r%r" % (col, strength), soft=False)
    o = I.obj("slash", vs, fs, m, True)
    I.place(o, loc, rot)
    return o


@icon("eb_rupture", "vignette")
def eb_rupture():
    """燼火重擊: the warden's greatsword mid-swing, blade wreathed in ember fire"""
    main, _ = PI.weapon_parts("greatsword", 3)
    L = main.get("length", 1.5)
    PI.finish(main, 5, FIRE)
    I.place(main, rot=(-8, 40, 0))
    bpy.context.view_layer.update()
    rnd = random.Random(3)
    d = Vector((math.sin(R(40)), 0, math.cos(R(40))))
    for k in range(9):
        t = -0.05 + 0.5 * k / 8
        f = I.flame(L * rnd.uniform(0.16, 0.26), L * rnd.uniform(0.035, 0.05), I.fire(strength=16), "fl", seed=k)
        f.location = d * (L * t) + Vector((rnd.uniform(-0.02, 0.02), 0.02, 0))
        f.rotation_euler = Euler((R(rnd.uniform(-10, 10)), R(40 + rnd.uniform(-25, 5)), 0))
    slash_arc(L * 0.52, -35, 120, L * 0.16, loc=(0, 0.06, -L * 0.02), strength=9.0)
    I.sparks(22, (-L * 0.35, -0.05, -L * 0.3), (L * 0.45, 0.02, L * 0.5), r=L * 0.006, col=(1.0, 0.6, 0.2), seed=11)
    return dict(fill=0.86, samples=56)


@icon("as_frostlance", "vignette")
def as_frostlance():
    """霜槍: a lance of clear ice trailing frost shards"""
    ice = I.gem((0.55, 0.82, 1.0), 1.31, glow=0.9, rough=0.06)
    frost = I.look('ice')
    lance = I.crystal(0.022, 0.42, 6, 0.42, ice, "lance", jitter=0.12, seed=4, base_tip=0.12)
    core = I.crystal(0.009, 0.36, 5, 0.3, I.emit((0.7, 0.92, 1.0), 5.0), "core", base_tip=0.2); core.location = (0, 0, 0.03)
    parts = [lance, core]
    rnd = random.Random(8)
    for k in range(9):
        a = rnd.uniform(0, 2 * math.pi); rr = rnd.uniform(0.03, 0.055)
        c = I.crystal(rnd.uniform(0.006, 0.012), rnd.uniform(0.05, 0.10), 5, 0.5, frost if k % 3 == 0 else ice, "sh", jitter=0.2, seed=k + 20)
        c.location = (rr * math.cos(a), rr * math.sin(a), rnd.uniform(-0.01, 0.10))
        c.rotation_euler = Euler((R(rnd.uniform(-25, 25)), R(rnd.uniform(-25, 25)), 0))
        parts.append(c)
    g = I.group(parts); I.place(g, rot=(-10, 48, 0))
    bpy.context.view_layer.update()
    I.sparks(18, (-0.16, -0.04, -0.18), (0.12, 0.02, 0.12), r=0.0016, col=(0.75, 0.92, 1.0), seed=2, strength=25)
    return dict(fill=0.84, samples=72)
