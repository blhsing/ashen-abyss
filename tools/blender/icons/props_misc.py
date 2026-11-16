"""Consumables, currency, crafting materials, containers, map / shrine / paragon tokens, class crests."""
import bpy, math, random
from mathutils import Vector, Euler
import iconlib as I
from iconlib import R
from registry import icon, reg

EMBER = (1.0, 0.42, 0.08)


def liquid(col, glow=1.2, key=None):
    key = key or "liquid%r" % (col,)
    if key in I._MATS:
        return I._MATS[key]
    m, nt, b = I._pbr(key)
    b.inputs['Base Color'].default_value = tuple(col) + (1,)
    b.inputs['Roughness'].default_value = 0.08
    b.inputs['Subsurface Weight'].default_value = 0.0
    b.inputs['Emission Color'].default_value = tuple(col) + (1,)
    b.inputs['Emission Strength'].default_value = glow
    I._MATS[key] = m
    return m


def flask(col=(0.85, 0.05, 0.04), fillz=0.062, glow=1.4):
    """round-bottomed glass flask, cork, brass collar, leather cord; liquid up to fillz"""
    body = [(0.0, 0.0), (0.020, 0.002), (0.034, 0.012), (0.040, 0.030), (0.036, 0.050), (0.024, 0.066), (0.0135, 0.076), (0.012, 0.100), (0.0145, 0.106)]
    g = I.lathe(body, 40, I.glass((0.80, 0.90, 0.86)), "glass"); I.solid(g, 0.0022, 1.0)
    inner = [(0.0, 0.003)] + [(max(r - 0.0032, 0.0), z + 0.001) for (r, z) in body[1:] if z <= fillz] + [(0.0, fillz)]
    lq = I.lathe(inner, 40, liquid(col, glow), "liquid")
    cork = I.lathe([(0.0, 0.098), (0.0105, 0.098), (0.0125, 0.122), (0.0, 0.124)], 20, I.wood((0.30, 0.20, 0.11)), "cork")
    collar = I.torus(0.0135, 0.0026, I.brass(), "collar", 32, 8); collar.location = (0, 0, 0.079)
    cord = I.torus(0.0142, 0.0016, I.leather(), "cord", 32, 6); cord.location = (0, 0, 0.088)
    tag = I.tube([(0.014, 0, 0.088), (0.026, -0.004, 0.074), (0.030, -0.006, 0.056)], 0.0012, 6, I.leather(), "tagcord")
    return [g, lq, cork, collar, cord, tag]


@icon("potion", "misc")
def potion():
    """healing draught: red ember-warm liquid in a round flask"""
    parts = flask()
    g = I.group(parts); I.place(g, rot=(0, -14, 20))
    return dict(fill=0.80, samples=72)
