"""Head of Osaros v2, part 1: a gaunt fired-clay king's face sculpted from the subdivided CC0 base head with numpy
brushes (ov2_sculpt): long narrow skull, heavy level brow over deep orbits, knife cheekbones over hollow cheeks, a thin
broken nose burnt short, a hard square chin, a corded neck.  Cold contempt.  (Part 2, ov2_dense.py: mouth, crown, breach.)"""
import math
import numpy as np
import bpy, bmesh
import monlib as M
import ov2_sculpt as SC
from monlib import V, tube, ellipsoid, box, join, horn, revolve, loft, lerp, unit

# landmarks of the base head (rest space, face towards -Y): see kit_flesh.FACE
EYE = np.array([0.031, -0.108, 1.668])
NOSE = np.array([0.0, -0.137, 1.626])
MOUTH = np.array([0.0, -0.119, 1.602])
CHIN = np.array([0.0, -0.105, 1.555])
BROW = np.array([0.0, -0.117, 1.69])
EYE_Z = 1.6665


def base_head(levels=2):
    """head + neck + a block of the upper chest of the base body, closed, subdivided"""
    b = M.load_body('male')
    b.W = None
    for co, no in (((0, 0, 1.452), (0, 0, -1)), ((0.135, 0, 0), (1, 0, 0)), ((-0.135, 0, 0), (-1, 0, 0))):
        b = SC.cut_keep(b, co, no)
    ok, bad = SC.is_closed(b)
    M.log("base head block: %d verts, %d faces, closed=%s (%d open edges)" % (len(b.v), len(b.f), ok, bad))
    if levels:
        b = M.subsurf(b, levels)
    b.smooth = 180.0
    return b


def sculpt(h):
    """the face. Works on the dense closed head (in place)."""
    # the subdivided triangle soup carries ripples: relax everything but the features of the face
    feat = M.smoothstep(-0.06, -0.095, h.v[:, 1]) * M.smoothstep(1.71, 1.695, h.v[:, 2]) * M.smoothstep(1.53, 1.56, h.v[:, 2])
    ear = M.smoothstep(0.074, 0.084, np.abs(h.v[:, 0])) * M.smoothstep(1.60, 1.625, h.v[:, 2]) * M.smoothstep(1.71, 1.69, h.v[:, 2])
    M.smooth_verts(h, 14, 0.5, mask=1.0 - np.maximum(feat, ear))
    v = h.v
    # ---------------------------------------------------------------- proportions: a long face on a tall narrow skull
    z = v[:, 2]
    k = 1.19
    zl = 1.668
    face = M.smoothstep(0.02, -0.04, v[:, 1])                    # front of the head only (the nape keeps its place)
    dz = np.where(z < zl, (z - zl) * (k - 1), 0.0)
    dz = np.where(z < 1.555, (1.555 - zl) * (k - 1) * M.smoothstep(1.47, 1.555, z), dz)
    v[:, 2] += dz * (0.35 + 0.65 * face)
    z = v[:, 2]
    v[:, 2] += np.where(z > 1.70, (z - 1.70) * 0.17, 0.0)
    z = v[:, 2]
    v[:, 0] *= 1 - 0.08 * M.smoothstep(1.70, 1.78, z)
    # the lower face narrows hard from the cheekbones to the chin
    v[:, 0] *= 1 - 0.19 * M.smoothstep(1.642, 1.575, z) * M.smoothstep(1.49, 1.545, z) * face
    SC.grab(h, (0.066, -0.066, 1.648), 0.034, (0.0072, -0.003, 0.001), sym=True)       # zygomatic arches stand out
    SC.line(h, [(0.038, -0.103, 1.646), (0.060, -0.084, 1.651), (0.078, -0.045, 1.655)], 0.011, 1.0, vec=(0.0050, -0.0042, 0.0016), sym=True)
    SC.line(h, [(0.042, -0.100, 1.641), (0.062, -0.082, 1.645), (0.079, -0.048, 1.648)], 0.0065, 1.0, kind='sharp', vec=(0.0018, -0.0022, 0.0), sym=True)  # a hard edge under the bone
    # ---------------------------------------------------------------- sockets: deep round orbits under the brow
    SC.grab(h, (0.032, -0.106, 1.667), 0.027, (0, 0.025, 0), sym=True, sc=(1.18, 1, 0.95))
    SC.grab(h, (0.031, -0.092, 1.667), 0.013, (0, 0.010, 0), sym=True, sc=(1.25, 1, 0.9), kind='round')
    SC.grab(h, (0.033, -0.098, 1.647), 0.012, (0, -0.0022, -0.001), sym=True)          # the lower rim of the orbit
    # ---------------------------------------------------------------- brow: a heavy, low, level bar overhanging the orbits
    SC.line(h, [(0.010, -0.115, 1.690), (0.03, -0.110, 1.693), (0.05, -0.100, 1.694), (0.068, -0.082, 1.690)], 0.018, 1.0,
            vec=(0, -0.0205, -0.0085), sym=True)
    SC.line(h, [(0.012, -0.128, 1.684), (0.03, -0.124, 1.686), (0.05, -0.114, 1.686), (0.066, -0.096, 1.683)], 0.006, 1.0, kind='sharp',
            vec=(0, -0.0020, -0.0024), sym=True)                                       # a hard lower edge to the brow: the orbit sits in its shadow
    SC.grab(h, (0.0, -0.122, 1.690), 0.017, (0, -0.007, -0.0035))                      # glabella knot
    SC.grab(h, (0.016, -0.121, 1.684), 0.012, (0, -0.002, -0.0055), sym=True)          # inner brow pulled down: the frown
    SC.grab(h, (0.058, -0.100, 1.70), 0.014, (0, -0.001, 0.0020), sym=True)            # outer brow a little up
    # ---------------------------------------------------------------- cheeks: long hollows under the cheekbones; temples
    SC.line(h, [(0.060, -0.074, 1.626), (0.050, -0.084, 1.596), (0.038, -0.086, 1.562)], 0.028, 1.0, vec=(-0.0135, 0.0200, 0), sym=True)
    SC.grab(h, (0.052, -0.080, 1.612), 0.016, (-0.003, 0.006, 0), sym=True, kind='round')   # the deepest point of the hollow
    SC.grab(h, (0.072, -0.046, 1.705), 0.032, (-0.0100, 0.003, 0), sym=True)
    # ---------------------------------------------------------------- nose: thin, bony, a broken bridge; the tip burnt short
    v = h.v
    nm = SC.fall(np.abs(v[:, 0]), 0.03) * M.smoothstep(-0.085, -0.105, v[:, 1]) * M.smoothstep(1.59, 1.625, v[:, 2]) * M.smoothstep(1.70, 1.675, v[:, 2])
    v[:, 0] *= 1 - 0.44 * nm
    SC.grab(h, (0.0, -0.130, 1.664), 0.015, (0, -0.0072, 0.0))                         # the hump of the bridge
    SC.grab(h, (0.0, -0.134, 1.646), 0.010, (0.0014, 0.002, 0.0))                      # ... and the break under it
    SC.grab(h, (0.0, -0.140, 1.622), 0.014, (0, 0.003, -0.0045))                       # tip drawn down and back
    SC.grab(h, (0.014, -0.118, 1.616), 0.011, (-0.003, 0.003, 0.0), sym=True)          # shrunken wings
    # ---------------------------------------------------------------- jaw and chin: hard planes, no flesh
    SC.line(h, [(0.016, -0.100, 1.522), (0.040, -0.066, 1.540), (0.058, -0.022, 1.588)], 0.010, 1.0, vec=(0.0034, -0.0026, -0.0048), sym=True)  # jaw line
    SC.line(h, [(0.014, -0.103, 1.519), (0.040, -0.068, 1.536), (0.058, -0.024, 1.584)], 0.005, 1.0, kind='sharp', vec=(0.0012, -0.0010, -0.0020), sym=True)
    SC.grab(h, (0.028, -0.062, 1.522), 0.024, (0.0, 0.009, 0.0085), sym=True)          # hollow under the jaw
    SC.grab(h, (0.0, -0.104, 1.520), 0.013, (0, -0.0045, -0.0045))                     # the point of the chin
    SC.grab(h, (0.021, -0.096, 1.532), 0.014, (-0.0045, 0.0035, 0.001), sym=True)      # ... its sides cut flat
    SC.grab(h, (0.0, -0.110, 1.548), 0.013, (0, 0.0035, 0.0))                          # the groove under the lower teeth
    SC.grab(h, (0.050, -0.040, 1.572), 0.018, (0.0035, -0.001, -0.003), sym=True)      # jaw angles
    # ---------------------------------------------------------------- mouth: lips burnt away, drawn back flat
    SC.grab(h, (0.0, -0.120, 1.588), 0.03, (0, 0.0045, 0.0), sc=(1.4, 1, 0.75))
    # ---------------------------------------------------------------- neck: gaunt, corded
    v = h.v
    z = v[:, 2]
    nk = M.smoothstep(1.47, 1.50, z) * M.smoothstep(1.565, 1.535, z)
    v[:, 0] = v[:, 0] * (1 - 0.18 * nk)
    v[:, 1] = 0.012 + (v[:, 1] - 0.012) * (1 - 0.13 * nk)
    SC.line(h, [(0.060, 0.012, 1.610), (0.045, -0.02, 1.555), (0.026, -0.05, 1.50), (0.014, -0.062, 1.47)], 0.013, 0.0042, sym=True)
    SC.grab(h, (0.0, -0.058, 1.528), 0.015, (0, -0.0045, 0.0))                         # larynx
    SC.grab(h, (0.0, -0.048, 1.492), 0.02, (0, 0.006, 0.0))                            # hollow of the throat
    # ---------------------------------------------------------------- ears: shrivelled against the skull
    v = h.v
    em = M.smoothstep(0.080, 0.090, np.abs(v[:, 0]))
    v[:, 0] = np.sign(v[:, 0]) * (np.abs(v[:, 0]) - (np.abs(v[:, 0]) - 0.080) * 0.55 * em)
    return h


def wrinkles(h):
    """creases at the 2-4 mm scale: they survive in the normal map baked from this dense mesh"""
    n = h.normals()
    L = SC.line
    for x in (0.0065, -0.0075):                                                        # frown lines
        L(h, [(x, -0.127, 1.688), (x * 1.2, -0.124, 1.703), (x * 1.6, -0.119, 1.714)], 0.0032, -0.0016, nrm=n, taper=0.3)
    for k, zz in enumerate((1.722, 1.737, 1.753)):                                     # forehead
        L(h, [(-0.05, -0.098, zz - 0.004), (-0.025, -0.113, zz + 0.001), (0.0, -0.117, zz - 0.001 * k), (0.026, -0.112, zz + 0.002),
              (0.05, -0.097, zz - 0.003)], 0.0034, -0.0012, nrm=n, taper=0.25)
    for a in (-22, 4, 28):                                                             # crow's feet
        ar = math.radians(a)
        L(h, [(0.054, -0.090, 1.667), (0.054 + 0.016 * math.cos(ar), -0.082, 1.667 - 0.016 * math.sin(ar))], 0.0026, -0.001, nrm=n, sym=True, taper=0.3)
    L(h, [(0.016, -0.100, 1.650), (0.03, -0.102, 1.642), (0.046, -0.094, 1.647)], 0.0034, -0.0014, nrm=n, sym=True, taper=0.3)   # under the eye
    L(h, [(0.017, -0.118, 1.622), (0.027, -0.110, 1.604), (0.033, -0.102, 1.580), (0.033, -0.097, 1.560)], 0.0042, -0.0024, nrm=n, sym=True,
      taper=0.2)                                                                        # nasolabial
    for dx in (0.0, 0.011):                                                            # gaunt cheek folds
        L(h, [(0.050 + dx, -0.086 + dx * 1.1, 1.632), (0.046 + dx, -0.082 + dx * 1.2, 1.600), (0.038 + dx, -0.080 + dx * 1.2, 1.568)], 0.0036,
          -0.0014, nrm=n, sym=True, taper=0.3)
    for zz in (1.508, 1.488):                                                          # neck
        L(h, [(-0.04, -0.03, zz + 0.006), (-0.02, -0.052, zz), (0.0, -0.058, zz - 0.002), (0.02, -0.052, zz), (0.04, -0.03, zz + 0.006)], 0.004,
          -0.0012, nrm=n, taper=0.2)
    return h
