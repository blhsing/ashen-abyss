"""smoke test of toolkit functions that the first creatures did not exercise"""
import sys, os, traceback
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, numpy as np
import monlib as M, kit_cloth as KC, kit_flesh as KF, kit_bones as KB, looks as L
import kit_a, kit_b, kit_c, kit_ice, kit_wisp   # registers their extra look kinds (burlap, sand, frost_*, glacier, wraps, crystal)
M.reset_scene(); M.load_rig()


def t(name, fn):
    try:
        r = fn()
        print("SMOKE ok  ", name, (r.ntri if isinstance(r, M.Part) else r))
    except Exception:
        print("SMOKE FAIL", name)
        traceback.print_exc()


t("chain", lambda: M.chain([(0, 0, 1), (0.2, 0, 0.8), (0.4, 0, 0.9)], 0.04, 0.007))
t("cape", lambda: KC.cape_weights(KC.cape()))
t("remesh", lambda: M.remesh([M.ellipsoid(0.2, 0.2, 0.3).move(0, 0, 1), M.ellipsoid(0.15, 0.15, 0.15).move(0.2, 0, 1.2)], voxel=0.03, ratio=0.5))
t("subsurf", lambda: M.subsurf(M.box(0.2, 0.2, 0.2), 1))
t("solidify", lambda: M.sheet(np.array([[(x * 0.1, y * 0.1, 0) for x in range(4)] for y in range(4)], dtype=float)).solidify(0.01))
t("weld", lambda: M.box(0.1, 0.1, 0.1).weld())
t("subdiv", lambda: M.box(0.1, 0.1, 0.1).subdiv(1))
t("ico", lambda: M.ico(0.1, 2))
t("cone", lambda: M.cone_spike(0.02, 0.1))
t("bevelbox", lambda: M.box(0.1, 0.2, 0.3, bevel=0.01, segs=2))
t("revolve", lambda: M.revolve([(0, 0), (0.1, 0.05), (0.05, 0.2), (0, 0.25)], 12))
t("weights_from", lambda: M.weights_from(M.box(0.1, 0.1, 0.1), {'pelvis': 0.5, 'spine_01': 0.5}))
t("smooth_verts", lambda: M.smooth_verts(M.ico(0.1, 2), 2))
t("rough", lambda: M.ico(0.1, 2).rough(0.01, 20.0))
b = M.load_body('male')
t("transfer", lambda: M.transfer_weights(M.ellipsoid(0.2, 0.2, 0.3, n=10, m=6).move(0, 0, 1.2), b, smooth=2))
t("body female", lambda: M.load_body('female'))
t("shell", lambda: KF.shell(b, b.wsum(['spine_03']) > 0.5, 0.01))
t("via bevel mod", lambda: M.via_mods(M.box(0.1, 0.1, 0.1), [('BEVEL', dict(width=0.01, segments=2))]))
t("boolean union", lambda: M.boolean(M.box(0.2, 0.2, 0.2), M.ico(0.13, 2), 'UNION'))
t("bend", lambda: M.tube([(0, 0, z * 0.1) for z in range(8)], 0.02).bend('z', 'x', 60.0))
t("band", lambda: M.band((0, 0, 1), 0.2, 0.15, 0.03, 0.008))
# every look kind compiles
C = M.Creature('smoke_test', 99999)
for i, k in enumerate(L.KINDS):
    C.look('l_' + k, k, 'rock_' + k)
    C.add(M.ico(0.1, 2).move(i * 0.3 - 2, 0, 0.1), bone='root', look='l_' + k)
for name, ps in L.PRESET.items():
    C.look('p_' + name, 'metal', 'metal_' + name, **ps)
    C.add(M.ico(0.1, 2).move(0, 1, 0.1), bone='root', look='p_' + name)
try:
    C.finalize(bake=True, export=False)
    print("SMOKE ok   looks bake")
except Exception:
    print("SMOKE FAIL looks bake"); traceback.print_exc()
