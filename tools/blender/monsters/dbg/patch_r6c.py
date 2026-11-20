"""round 6c (2026-11-20): corrections after looking at the round-6 review sheets.  Applied once; do not re-run."""
H = 'Y:/tools/blender/monsters/'


def load(p):
    return open(p, encoding='utf-8').read()


def save(p, s):
    import ast
    ast.parse(s)
    open(p, 'w', encoding='utf-8', newline='\n').write(s)


def rep(s, a, b, n=1):
    assert a in s, a[:110]
    return s.replace(a, b, n)


p = H + 'asm_parts10.py'
s = load(p)
# ---- the Mute: the chest slab ended in a hard ledge above the hips -> it now tapers down INTO the hips (rounder section there)
s = rep(s, """    key = np.array([(0.94, 0.020, 0.200, 0.165), (1.02, 0.020, 0.188, 0.158), (1.12, 0.018, 0.190, 0.148), (1.22, 0.016, 0.200, 0.134),""",
        """    key = np.array([(0.84, 0.020, 0.120, 0.095), (0.92, 0.020, 0.175, 0.135), (1.02, 0.020, 0.186, 0.150), (1.12, 0.018, 0.190, 0.146), (1.22, 0.016, 0.200, 0.134),""")
s = rep(s, "    pw = lambda z: float(np.interp(z, [1.42, 1.59], [power, 2.0]))          # slab below, round dome above",
        "    pw = lambda z: float(np.interp(z, [0.95, 1.12, 1.42, 1.59], [2.0, power, power, 2.0]))     # round at the hips, a slab at the chest, a round dome above")
# ---- brazier-hulk: the tallest tongue of the crown is glass like the others (as an emissive blade it read as flat blue paper)
s = rep(s, "look=core if k == 0 else (ice if k % 2 else ice2), smooth=FA)", "look=ice if k % 2 else ice2, smooth=FA)")
# ---- drift-colossus: the sparks were hidden inside the casts -> a ring of dull glow round each cast's waist, half buried in the drift
s = rep(s, """def cast_figure(A, bone, at, yaw, pitch, sc, look, roll=0.0, ratio=0.09, arms=75.0, melt=1, face='void', spark=None, K=None):""",
        """def cast_figure(A, bone, at, yaw, pitch, sc, look, roll=0.0, ratio=0.09, arms=75.0, melt=1, face='void', spark=None, K=None, crease=None):""")
s = rep(s, """    if spark:
        e = ellipsoid(0.012, 0.008, 0.016, n=5, m=3).move(0.0, -0.112, 1.586).rot('x', 14, pivot=hp)
        A.add(xf(e), bone=bone, look=spark, keep=K)
    return K""",
        """    if spark:
        e = ellipsoid(0.012, 0.008, 0.016, n=5, m=3).move(0.0, -0.112, 1.586).rot('x', 14, pivot=hp)
        A.add(xf(e), bone=bone, look=spark, keep=K)
    if crease:                                             # the crease where the cast leaves the mass: the drift hides part of the ring
        c = torus(0.168, 0.024, n=12, m=4, ry=0.132).move(0.0, 0.02, 1.09)
        A.add(xf(c), bone=bone, look=crease, smooth=80, keep=K)
    return K""")
s = rep(s, """        Kf = cast_figure(A, bone, at, yaw, pitch, sc, pale, ratio=ratio, arms=float(arms), face=face, spark=spark if k % 3 == 0 else None)
        # the crease where the cast leaves the mass: a crescent of dull sparks under its chest
        out = V(math.sin(rad(yaw)), -math.cos(rad(yaw)), 0.0)
        c = V(at) + out * 0.085 * sc + V(0, 0, 0.10 * sc)
        cr = KX.lump(0.075 * sc, 0.03, 0.022, seed=k, amp=0.3, n=7, m=4).rot('z', yaw).move(c)
        A.add(cr, bone=bone, look=spark, smooth=80, keep=Kf)""",
        """        cast_figure(A, bone, at, yaw, pitch, sc, pale, ratio=ratio, arms=float(arms), face=face, spark=spark if k % 3 == 0 else None, crease=spark)""")
save(p, s)

p = H + 'asm_parts9.py'
s = load(p)
# ---- gallows-tree: SOLID hanging things (gibbet, skull) take ONE blend weight (a gradient stretched the cage); only the ropes
#      and the chain, which may stretch, keep a gradient
s = rep(s, "        sway_weights(ns, zb, zb - 0.75, low='pelvis', amt=0.85)", "        sway_weights(ns, zb, zb - 0.75, low='pelvis', amt=0.6)")
s = rep(s, "    sway_weights(ch, zb, zb - 0.9, low='pelvis', amt=0.85)", "    sway_weights(ch, zb - 0.06, zb - 0.25, low='pelvis', amt=0.5)")
s = rep(s, "        sway_weights(q, zb, zb - 0.9, low='pelvis', amt=0.85)", "        M.weights_from(q, {'spine_03': 0.5, 'pelvis': 0.5})")
s = rep(s, "    sway_weights(sk, zb, zb - 0.9, low='pelvis', amt=0.85)", "    M.weights_from(sk, {'spine_03': 0.5, 'pelvis': 0.5})")
save(p, s)
print('r6c ok')
