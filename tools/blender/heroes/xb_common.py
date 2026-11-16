"""Private helpers of fork B (hunter / soulbinder / wildsoul / elder). Shared libs stay untouched."""
import os, math
import numpy as np
from charlib import *


def tube(path, radius, n=8, **kw):
    """g_tube + 'luv' (u = angle fraction, v = metres along the path) so mats.wood grain follows the shaft."""
    g = g_tube(path, radius, n=n, **kw)
    P = np.asarray(path, dtype=float)
    m = len(P)
    L = np.concatenate([[0.0], np.cumsum(np.linalg.norm(P[1:] - P[:-1], axis=1))])
    luv = np.zeros((len(g["v"]), 2))
    for i in range(m):
        for k in range(n):
            luv[i * n + k] = (k / n, L[i])
    if len(g["v"]) > m * n:
        luv[m * n] = (0.5, 0.0)
        luv[m * n + 1] = (0.5, L[-1])
    g["luv"] = luv
    return g


def gnarl(path, amp=0.012, seed=1):
    """slightly crooked version of a straight path (hand-cut staff)"""
    rng = np.random.RandomState(seed)
    P = np.asarray(path, dtype=float).copy()
    P[1:-1, 0] += (rng.rand(len(P) - 2) - 0.5) * 2 * amp
    P[1:-1, 1] += (rng.rand(len(P) - 2) - 0.5) * 2 * amp
    return P


def zline(z0, z1, n=6):
    return [(0.0, 0.0, z0 + (z1 - z0) * i / (n - 1)) for i in range(n)]


def pistol_grip(side="r"):
    """Crossbow-local -> armature rest space. local +Y = barrel (level in Pistol_Aim_Neutral), +Z = up through the
    fist (pistol grip runs along -Z), +X = lateral (limbs)."""
    H = bone_frame("hand_" + side)
    y = np.array((0.03 if side == "r" else -0.03, 0.98, -0.18))
    y /= np.linalg.norm(y)
    z = np.array(GRIP_A[side], dtype=float)
    z = z - y * np.dot(z, y)
    z /= np.linalg.norm(z)
    x = np.cross(y, z)
    L = np.eye(4)
    L[:3, 0], L[:3, 1], L[:3, 2], L[:3, 3] = x, y, z, GRIP_C[side]
    return H @ L


def g_feather(length=0.14, width=0.03, thick=0.003):
    """feather in the XZ plane, quill at the origin, vane towards +Z"""
    w = width / 2
    outl = [(0.0, 0.0), (0.004, 0.02 * length / 0.14), (w, 0.30 * length), (w * 0.9, 0.62 * length), (w * 0.45, 0.9 * length),
            (0.0, length), (-w * 0.5, 0.86 * length), (-w * 0.95, 0.58 * length), (-w, 0.28 * length), (-0.004, 0.02 * length / 0.14)]
    return g_prism(outl, thick)


def g_tooth(length=0.06, r=0.012, bend=0.012, n=6):
    """curved fang / claw / bone spike along +Z, bending towards +X"""
    path = [(bend * (t ** 2), 0.0, length * t) for t in (0.0, 0.3, 0.6, 0.85, 1.0)]
    return g_tube(path, [r, r * 0.85, r * 0.6, r * 0.32, r * 0.05], n=n)


def g_antler(scale=1.0, tines=3, n=6, seed=0):
    """Left antler: main beam rising from the origin, sweeping out (+X) and back (+Y), with tines pointing up/forward."""
    s = scale
    beam = np.array([(0.0, 0.0, 0.0), (0.035, 0.012, 0.07), (0.085, 0.035, 0.14), (0.145, 0.06, 0.20), (0.20, 0.07, 0.275),
                     (0.225, 0.055, 0.36), (0.215, 0.03, 0.43)]) * s
    rad = np.array([0.021, 0.019, 0.017, 0.015, 0.013, 0.010, 0.004]) * s
    parts = [g_tube(beam, rad, n=n)]
    spec = [(1, (-0.01, -0.07, 0.09), 0.015), (2, (0.01, -0.09, 0.12), 0.014), (3, (0.03, -0.08, 0.14), 0.013),
            (4, (0.06, -0.05, 0.13), 0.011)]
    for (i, d, r) in spec[:tines]:
        a = beam[i]
        d = np.array(d) * s
        mid = a + d * 0.5 + np.array((0, 0, 0.012)) * s
        parts.append(g_tube([a, mid, a + d + np.array((0, 0, 0.03)) * s], [r * s, r * s * 0.7, r * s * 0.12], n=n))
    return gjoin(*parts)


def g_beads(path, r=0.012, nu=6, nv=4, sz=1.0):
    return gjoin(*[gxf(g_sphere(r, nu, nv, sz=sz), T(*p)) for p in path])


def face_mask(c, src, z_top=1.648, z_bot=1.545, y_max=-0.005, offset=0.007, cls="cloth", name="mask", smooth=2):
    """half mask over nose / mouth / jaw (shell of the lower face), rigid to the head"""
    B = c.B
    m = (B.w("Head", "neck_01") > 0.5) & (B.z < z_top) & (B.z > z_bot) & (B.y < y_max)
    o = shell(B, m, offset, name, cls, src, smooth=smooth, sharp=180)
    return o


def set_uv_const(obj, u, v=0.5):
    me = obj.data
    if not len(me.uv_layers):
        me.uv_layers.new(name="UVMap")
    uv = np.empty(len(me.loops) * 2, dtype=np.float32)
    uv[0::2] = u
    uv[1::2] = v
    me.uv_layers[0].data.foreach_set('uv', uv)


def emis_gradient(cid, c0, c1, size=64):
    """Make the (single) emis_ material sample a c0 -> c1 gradient along UV.u (set per part with set_uv_const)."""
    d = os.path.join(TEX, cid)
    os.makedirs(d, exist_ok=True)
    t = np.linspace(0, 1, size)[None, :, None]
    arr = np.array(c0)[None, None, :] * (1 - t) + np.array(c1)[None, None, :] * t
    arr = np.repeat(arr, size, axis=0)
    path = mats.save_rgb(mats.lin2srgb(arr), os.path.join(d, "emis_g.png"))
    orig = mats.final_material

    def fm(name, *a, **kw):
        m = orig(name, *a, **kw)
        if name.startswith("emis_") and not FAST:
            nt = m.node_tree
            bs = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
            tx = nt.nodes.new('ShaderNodeTexImage')
            tx.image = mats.load_img(path, 'sRGB')
            tx.extension = 'EXTEND'
            nt.links.new(tx.outputs['Color'], bs.inputs['Emission Color'])
        return m
    mats.final_material = fm
