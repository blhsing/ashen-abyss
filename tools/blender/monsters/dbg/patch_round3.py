def load(p):
    return open(p, encoding='utf-8').read()


def save(p, s):
    open(p, 'w', encoding='utf-8', newline='\n').write(s)


def rep(s, a, b, n=1):
    assert a in s, a[:110]
    return s.replace(a, b, n)


H = 'Y:/tools/blender/monsters/'

# =================================================================== looks: deeper emission colours (sRGB lifts the green)
p = H + 'asm_parts8.py'
s = load(p)
s = rep(s, "flame_lo=asm._p('emis', 'emis', emit=(1.0, 0.62, 0.20), strength=1.5, mottle=0.25, base_mul=0.3),", "flame_lo=asm._p('emis', 'emis', emit=(1.0, 0.46, 0.10), strength=1.5, mottle=0.25, base_mul=0.3),")
s = rep(s, "flame_mid=asm._p('emis', 'emis', emit=(1.0, 0.40, 0.08), strength=1.5, mottle=0.3, base_mul=0.3),", "flame_mid=asm._p('emis', 'emis', emit=(1.0, 0.24, 0.035), strength=1.5, mottle=0.3, base_mul=0.3),")
s = rep(s, "flame_tip=asm._p('emis', 'emis', emit=(0.78, 0.20, 0.03), strength=1.5, mottle=0.4, base_mul=0.3),", "flame_tip=asm._p('emis', 'emis', emit=(0.62, 0.09, 0.012), strength=1.5, mottle=0.4, base_mul=0.3),")
s = rep(s, "coldfire=asm._p('emis', 'emis', emit=(0.30, 0.66, 1.0), strength=1.7, mottle=0.55, base_mul=0.55, scale=9.0),", "coldfire=asm._p('emis', 'emis', emit=(0.07, 0.26, 0.80), strength=1.5, mottle=0.9, base_mul=0.3, scale=7.0, crust=0.55),")
s = rep(s, "arc=asm._p('emis', 'emis', emit=(0.74, 0.62, 1.0), strength=2.2, mottle=0.2, base_mul=0.7),", "arc=asm._p('emis', 'emis', emit=(0.42, 0.30, 0.95), strength=1.6, mottle=0.3, base_mul=0.3),")
s = rep(s, "gill=asm._p('emis', 'emis', emit=(0.34, 0.46, 0.08), strength=1.6, mottle=0.5, base_mul=0.6, scale=40.0),", "gill=asm._p('emis', 'emis', emit=(0.16, 0.24, 0.03), strength=1.4, mottle=0.7, base_mul=0.5, scale=40.0),")
s = rep(s, "spark=asm._p('emis', 'emis', emit=(0.85, 0.16, 0.04), strength=1.4, base_mul=0.5, mottle=0.6, _name='ember'),", "spark=asm._p('emis', 'emis', emit=(0.70, 0.07, 0.012), strength=1.4, base_mul=0.2, mottle=0.7, crust=0.8),")
s = rep(s, "    ashpale=asm._p(", "    lampglow=asm._p('emis', 'emis', emit=(1.0, 0.40, 0.07), strength=1.5, mottle=0.5, base_mul=0.3),\n    ashpale=asm._p(")
# the Mute: a slab of a torso (wide down to the waist), the hole in its middle
s = rep(s, "def mute_torso(A, look='chalk', hole=0.118, c=(0.0, 0.0, 1.265), lining='void'):", "def mute_torso(A, look='chalk', hole=0.125, c=(0.0, 0.0, 1.25), lining='void'):")
s = rep(s, """    key = np.array([(1.00, 0.020, 0.172, 0.134), (1.10, 0.020, 0.196, 0.146), (1.20, 0.018, 0.236, 0.158), (1.30, 0.016, 0.276, 0.166), (1.38, 0.016, 0.304, 0.168),
                    (1.44, 0.016, 0.312, 0.164), (1.49, 0.014, 0.296, 0.154), (1.53, 0.012, 0.255, 0.138), (1.565, 0.010, 0.19, 0.114), (1.59, 0.008, 0.10, 0.07)])""",
        """    key = np.array([(0.98, 0.020, 0.18, 0.136), (1.04, 0.020, 0.235, 0.152), (1.12, 0.018, 0.268, 0.162), (1.22, 0.016, 0.288, 0.168), (1.32, 0.016, 0.304, 0.170),
                    (1.42, 0.016, 0.314, 0.166), (1.48, 0.014, 0.302, 0.156), (1.525, 0.012, 0.262, 0.14), (1.56, 0.010, 0.195, 0.116), (1.585, 0.008, 0.10, 0.07)])""")
save(p, s)

p = H + 'asm_parts5.py'
s = load(p)
s = rep(s, "slag=asm._p('emis', 'emis', emit=(1.0, 0.36, 0.05), strength=2.0, base_mul=0.25, mottle=0.6, crust=1.0, scale=12.0),",
        "slag=asm._p('emis', 'emis', emit=(1.0, 0.20, 0.02), strength=2.0, base_mul=0.25, mottle=0.6, crust=1.0, scale=12.0),")
save(p, s)

# =================================================================== parts 9
p = H + 'asm_parts9.py'
s = load(p)

# ---- helper: a strap round the chest that is seated on the body and skinned like it
s = rep(s, "def rivet_ring(A, c, nrm, r, n, bone, look, rr=0.011, keep=False):",
        '''def chest_band(A, look='strap', z=1.33, w=0.035, th=0.007, tilt=0.0, on='body', pad=0.012):
    """strap / iron band round the chest, seated on the body (or on a published surface) and skinned like it"""
    S = A.S(on)
    cx, cy, rx, ry = girth_of(A, on, z)
    a = np.radians(np.linspace(0, 360, 28, endpoint=False))
    ring = np.stack([cx + (rx + 0.03) * np.cos(a), cy + (ry + 0.03) * np.sin(a), z + math.tan(rad(tilt)) * (rx + 0.03) * np.cos(a)], axis=1)
    P = np.array([S.nearest(q)[0] + unit(S.nearest(q)[1]) * pad for q in ring])
    t = tube(P, w * 0.7071, n=4, r2=th * 0.7071, up=(0, 0, 1), closed=True, phase=math.pi / 4)
    if on == 'body':
        A.add(t, look=look, skin=2, smooth=40)
    else:
        A.add(t, look=look, like=A.refs[on], smooth=40)
    return t


def rivet_ring(A, c, nrm, r, n, bone, look, rr=0.011, keep=False):''')

# ---- gallows tree: an organic trunk (irregular fluting, root buttresses, a slight lean), embers high in the hollow
i0 = s.index("    n = 26\n    out_ = [(0.72, 0.03, 0.19, 0.18)")
i1 = s.index("    # knot-hole mouth: a bark lip with the glow of the hollow behind it")
new = '''    n = 30
    out_ = [(0.70, 0.03, 0.20, 0.19), (0.78, 0.03, 0.33, 0.31), (0.88, 0.028, 0.315, 0.29), (1.02, 0.024, 0.285, 0.26), (1.22, 0.02, 0.27, 0.245), (1.42, 0.02, 0.268, 0.24),
            (1.60, 0.02, 0.258, 0.232), (1.78, 0.02, 0.246, 0.222), (1.93, 0.02, 0.236, 0.214)]
    in_ = [(1.93, 0.02, 0.205, 0.186), (1.84, 0.02, 0.20, 0.18), (1.77, 0.02, 0.188, 0.168), (1.725, 0.02, 0.12, 0.11)]

    def bark(th_, z_):                                    # irregular fluting, deep furrows, buttresses at the root
        m_ = 1.0 + 0.055 * np.sin(5 * th_ + 2.2 * z_ + seed) + 0.04 * np.sin(9 * th_ - 3.1 * z_ + 2 * seed) + 0.03 * np.sin(14 * th_ + 5.0 * z_)
        return m_ + 0.11 * np.maximum(0.0, np.sin(4 * th_ + 0.7)) ** 3 * float(M.smoothstep(1.15, 0.78, z_))

    def off(z_):                                          # the trunk leans and bows a little
        return V(0.032 * math.sin(2.2 * (z_ - 0.8)), 0.02 * math.sin(3.0 * (z_ - 0.8) + 1.0), 0.0)

    rings = M.rings_z([(z, 0, cy, rx, ry) for z, cy, rx, ry in out_], n, bark)
    rin = M.rings_z([(z, 0, cy, rx, ry) for z, cy, rx, ry in in_], n, bark_mod(seed, 8, 0.03))
    for rg, src in ((rings, out_), (rin, in_)):
        for k, q in enumerate(src):
            rg[k] += off(q[0])
    th = np.linspace(0, 2 * math.pi, n, endpoint=False)
    jag = np.maximum(0.0, 0.17 * np.abs(np.sin(1.5 * th + 0.6)) ** 2.0 + 0.07 * np.sin(5 * th + 1.0) + 0.04 * np.sin(11 * th))
    jag[np.abs(np.sin(th * 0.5 + 0.3)) < 0.25] *= 0.15                     # one side is broken low
    rings[-1][:, 2] += jag; rin[0][:, 2] += jag * 0.92
    rings[-2][:, 2] += jag * 0.35
    t = M.loft(np.concatenate([rings, rin]), cap0=True, cap1=True).recalc()
    t.rough(0.009, 9.0, seed=float(seed))
    z_weights(t)
    A.add(t, look=look, smooth=35)
    eb = off(1.75) + V(0, 0.02, 0)
    A.add(revolve([(0.0, 1.762), (0.12, 1.758), (0.155, 1.742), (0.0, 1.73)], n=14).move(eb), bone='spine_03', look=glow, smooth=60)      # embers in the hollow, seen from above
    for k in range(7):
        a = 2 * math.pi * k / 7 + 0.3
        rr = 0.085 if k else 0.0
        A.add(KX.lump(0.04, 0.035, 0.026, seed=k, amp=0.35).move(eb + V(rr * math.cos(a), rr * math.sin(a), 1.768)), bone='spine_03', look=look, smooth=40)
'''
s = s[:i0] + new + s[i1:]
s = rep(s, """    mz = 1.36
    my = 0.02 - 0.238 * 1.0
    lipp = np.stack([0.085 * np.cos(th[::2]) * (1 + 0.12 * np.sin(3 * th[::2])), np.zeros(n // 2), 0.115 * np.sin(th[::2])], axis=1) + V(0.01, my - 0.012, mz)
    A.add(tube(lipp, 0.03, n=6, r2=0.036, closed=True, up=(0, 1, 0)), bone='spine_03', look=look, smooth=40)
    A.add(ellipsoid(0.07, 0.022, 0.098, n=10, m=6).move(0.01, my - 0.004, mz), bone='spine_03', look=glow, smooth=80)""",
        """    mz = 1.36
    my = 0.02 - 0.243 * float(bark(np.array([-math.pi / 2]), mz)[0]) + off(mz)[1]
    mx = 0.01 + off(mz)[0]
    lipp = np.stack([0.085 * np.cos(th[::2]) * (1 + 0.12 * np.sin(3 * th[::2])), np.zeros(n // 2), 0.115 * np.sin(th[::2])], axis=1) + V(mx, my - 0.006, mz)
    A.add(tube(lipp, 0.034, n=6, r2=0.038, closed=True, up=(0, 1, 0)), bone='spine_03', look=look, smooth=40)
    A.add(ellipsoid(0.066, 0.02, 0.094, n=10, m=6).move(mx, my - 0.004, mz), bone='spine_03', look=glow, smooth=80)
    for k in range(4):                                    # snags of bark across the mouth, like teeth
        xx = mx + (k - 1.5) * 0.034
        A.add(M.cone_spike(0.012, 0.05 + 0.02 * (k % 2), n=4).rot('x', 180 if k % 2 else 0).move(xx, my - 0.022, mz + (0.085 if k % 2 else -0.085)), bone='spine_03', look=look, smooth=30)""")
s = rep(s, "        limb_tube(A, ['thigh' + s, 'calf' + s], [0.135, 0.105, 0.078], look, n=8, wob=0.014, seed=seed + (s2 > 0), smooth=35)",
        "        limb_tube(A, ['thigh' + s, 'calf' + s], [0.15, 0.115, 0.085], look, n=9, wob=0.016, seed=seed + (s2 > 0), smooth=35)\n"
        "        hp = R.H('thigh' + s)                               # a buttress root running from the trunk onto the thigh\n"
        "        A.add(M.horn(M.polyline([hp + V(s2 * 0.10, -0.10, 0.16), hp + V(s2 * 0.11, -0.13, -0.05), hp + V(s2 * 0.06, -0.10, -0.26)], 7), 0.07, n=6, r_tip=0.02), bone='thigh' + s,\n"
        "              look=look, smooth=35)")

# ---- brazier hulk: bigger clubs
s = rep(s, "        A.add(KC3.ice_chunk(0.185, 0.155, 0.16, seed=50 + (sx > 0), n=16).move(w + V(sx * 0.07, 0, -0.01)), bone='lowerarm' + s, look=ice, smooth=FA)",
        "        A.add(KC3.ice_chunk(0.215, 0.18, 0.185, seed=50 + (sx > 0), n=18).move(w + V(sx * 0.08, 0, -0.01)), bone='lowerarm' + s, look=ice, smooth=FA)")
s = rep(s, "        limb_tube(A, ['upperarm' + s, 'lowerarm' + s], [0.098, 0.102, 0.118], ice, n=6, wob=0.012, seed=seed + 5 + (sx > 0), smooth=FA)",
        "        limb_tube(A, ['upperarm' + s, 'lowerarm' + s], [0.105, 0.11, 0.13], ice, n=6, wob=0.012, seed=seed + 5 + (sx > 0), smooth=FA)")

# ---- colony: calmer gills, amber lamps, rounder fruit
s = rep(s, "    zig = 0.014 * (np.arange(ng) % 2 * 2 - 1)", "    zig = 0.008 * (np.arange(ng) % 2 * 2 - 1)")
s = rep(s, "def colony(A, look='skin', cap='capskin', gill='gill', lamp='glow', stalk='stalk', R_=0.66, seed=5, lamps=11):",
        "def colony(A, look='skin', cap='capskin', gill='gill', lamp='lampglow', stalk='stalk', R_=0.66, seed=5, lamps=11):")
s = rep(s, "        fr = revolve([(0.0, 0.0), (0.02, -0.006), (0.047, -0.045), (0.05, -0.075), (0.036, -0.108), (0.0, -0.125)], n=9)",
        "        fr = revolve([(0.0, 0.0), (0.02, -0.006), (0.04, -0.03), (0.05, -0.058), (0.05, -0.082), (0.038, -0.108), (0.018, -0.122), (0.0, -0.126)], n=12)")
s = rep(s, "        A.add(fr.move(p0 + V(0, 0, -L_)), bone=bone, look=lamp, smooth=80, keep=K)", "        A.add(fr.move(p0 + V(0, 0, -L_)), bone=bone, look=lamp, smooth=180, keep=K)")

# ---- tallow mound: the arm-bodies are thicker, their limbs pressed to the trunk; lumpier wax, more runs
s = rep(s, """        M.pose_sculpt(b, {'upperarm_l': dict(rot=[('y', 82)]), 'upperarm_r': dict(rot=[('y', -82)]), 'Head': dict(rot=[('x', -22)])})
        M.smooth_verts(b, 3, 0.5)""",
        """        M.pose_sculpt(b, {'upperarm_l': dict(rot=[('y', 86)]), 'upperarm_r': dict(rot=[('y', -86)]), 'Head': dict(rot=[('x', -22)])})
        limb = np.clip(b.wsum(arm_bones()) + b.wsum(M.RIG.chain('thigh_l') + M.RIG.chain('thigh_r')), 0, 1)
        b.v[:, 0] *= 1.0 - 0.5 * limb                         # arms and legs pressed to the centre line: one stretched trunk
        M.smooth_verts(b, 5, 0.5)""")
s = rep(s, "        b.v = np.outer(q[:, 2] * kz - 0.10, ax) + np.outer(q[:, 1] * 0.62, V(0, 1, 0)) + np.outer(q[:, 0] * 0.60, V(0, 0, -sx)) + sh",
        "        q = b.v.copy()\n        b.v = np.outer(q[:, 2] * kz - 0.10, ax) + np.outer(q[:, 1] * 0.86, V(0, 1, 0)) + np.outer(q[:, 0] * 0.80, V(0, 0, -sx)) + sh")
s = rep(s, "    g.rough(0.01, 7.0, seed=float(seed))", "    g.rough(0.016, 5.0, seed=float(seed))\n    g.rough(0.005, 17.0, seed=float(seed) + 3.0)")
s = rep(s, "    for k in range(16):\n        a_deg = 360.0 * (k + rng.uniform(0.15, 0.85)) / 16", "    for k in range(26):\n        a_deg = 360.0 * (k + rng.uniform(0.15, 0.85)) / 26")

# ---- drift colossus: one soft mass (figures sunk deeper, arms mostly pressed in, big drifts between them), a ragged thin plume
i0 = s.index("@part\ndef drift_mass(")
i1 = s.index("# ================================================================================================ wax: the tallow-mound")
new = '''@part
def drift_mass(A, look='ashcrust', pale='ashpale', spark='spark', seed=7, ratio=0.082):
    """a drift that stood up: the surface of the giant is made of half-emerged ash-casts -- faces, backs and a few
    reaching arms pressed outward like a relief -- bedded in big soft drifts so that the whole is one slope-shouldered
    mound with no neck; fists that are knots of fused bodies, dull sparks in the deep creases between the figures and a
    ragged plume of ash trailing from the back."""
    R = A.R
    rng = np.random.default_rng(seed)
    spots = [  # bone, waist point, yaw, pitch, scale, arms
        ('spine_03', (0.09, 0.14, 1.04), 168, 18, 0.80, 84), ('spine_03', (-0.12, 0.13, 1.08), -160, 24, 0.74, 84), ('spine_02', (0.02, 0.165, 0.82), 12, -12, 0.86, 86),
        ('clavicle_l', (0.24, 0.02, 1.10), 95, 22, 0.72, 20), ('clavicle_r', (-0.24, 0.03, 1.07), -98, 26, 0.70, 84),
        ('spine_03', (0.10, -0.10, 1.00), 18, 12, 0.66, 86), ('spine_02', (-0.09, -0.11, 0.84), -14, 16, 0.70, 84),
        ('spine_02', (0.18, 0.02, 0.80), 86, 24, 0.72, 84), ('spine_02', (-0.185, 0.04, 0.84), -82, 22, 0.70, 30),
        ('spine_03', (0.02, 0.04, 1.18), 186, 50, 0.80, 84), ('spine_03', (-0.05, -0.02, 1.22), -8, 46, 0.70, 84),
        ('pelvis', (0.12, 0.10, 0.62), 150, 18, 0.66, 86), ('pelvis', (-0.13, 0.09, 0.60), -150, 22, 0.64, 86), ('pelvis', (0.0, -0.10, 0.62), 4, 14, 0.60, 86),
    ]
    for k, (bone, at, yaw, pitch, sc, arms) in enumerate(spots):
        Kf = emerge(A, bone, at, yaw, pitch, sc, look if k % 3 else pale, ratio=ratio, arms=float(arms), melt=3)
        c = V(at) + V(0.12 * math.sin(rad(yaw)), -0.12 * math.cos(rad(yaw)), 0.10 * sc)          # sparks in the crease below each figure
        if k % 2 == 0:
            A.add(KX.lump(0.04, 0.04, 0.03, seed=k, amp=0.3, n=7, m=4).move(c), bone=bone, look=spark, smooth=80, keep=Kf)
    drifts = [  # x, y, z, rx, ry, rz, bone: the drift itself, bedding the casts
        (0.0, 0.04, 1.55, 0.25, 0.21, 0.15, 'spine_03'), (0.20, 0.03, 1.49, 0.16, 0.16, 0.12, 'clavicle_l'), (-0.20, 0.03, 1.49, 0.16, 0.16, 0.12, 'clavicle_r'),
        (0.0, 0.15, 1.38, 0.24, 0.15, 0.20, 'spine_03'), (0.0, -0.10, 1.34, 0.22, 0.13, 0.17, 'spine_03'), (0.0, 0.12, 1.10, 0.25, 0.16, 0.20, 'spine_02'),
        (0.0, -0.10, 1.08, 0.23, 0.14, 0.18, 'spine_02'), (0.17, 0.02, 1.22, 0.13, 0.17, 0.22, 'spine_02'), (-0.17, 0.02, 1.22, 0.13, 0.17, 0.22, 'spine_02'),
        (0.0, 0.03, 0.90, 0.25, 0.20, 0.15, 'pelvis'), (0.27, 0.02, 1.43, 0.12, 0.12, 0.10, 'upperarm_l'), (-0.27, 0.02, 1.43, 0.12, 0.12, 0.10, 'upperarm_r')]
    for k, (bx, by, bz, rx, ry, rz, bn) in enumerate(drifts):
        A.add(KX.lump(rx, ry, rz, seed=31 + k, amp=0.26, freq=6.0, n=12, m=7).move(bx, by, bz), bone=bn, look=look if k % 4 else pale, smooth=180, keep=True)
    for sx in (1, -1):                                              # fists: knots of fused bodies
        s = '_l' if sx > 0 else '_r'
        w = R.H('hand' + s)
        Kf = w + V(sx * 0.09, 0, 0)
        A.add(KX.lump(0.19, 0.165, 0.16, seed=5 + (sx > 0), amp=0.3, freq=7.0, n=12, m=7).move(Kf), bone='hand' + s, look=look, smooth=180, keep=Kf)
        for k, (yaw, roll, dz) in enumerate(((0, sx * 100, 0.05), (180, sx * 80, -0.03), (90 * sx, sx * 120, 0.0))):
            emerge(A, 'hand' + s, Kf + V(-sx * 0.14, 0.02 * (k - 1), dz), yaw, 18, 0.46, pale if k == 1 else look, roll=float(roll), ratio=ratio * 0.7, arms=86.0, K=Kf, melt=3)
        for t in (0.25, 0.7):                                       # casts riding on the forearms
            p = KC3.BP('lowerarm' + s, t)
            emerge(A, 'lowerarm' + s, p + V(-sx * 0.1, 0.0, 0.03), 180 if t < 0.5 else 0, 10, 0.44, look, roll=sx * 96.0, ratio=ratio * 0.7, arms=86.0, melt=3)
            A.add(KX.lump(0.10, 0.095, 0.09, seed=60 + int(t * 10), amp=0.28, n=9, m=5).move(p), bone='lowerarm' + s, look=look, smooth=180, keep=True)
    # the plume trailing from the back: thin ragged tongues, longest in the middle
    base = V(0.0, 0.20, 1.50)
    for k in range(11):
        u = (k - 5) / 5.0
        L_ = (0.34 + 0.44 * (1 - u * u)) * rng.uniform(0.8, 1.15)
        bl = flame_blade(L_, rng.uniform(0.045, 0.085), th=0.03, curl=rng.uniform(-0.3, 0.3), n=10, seed=k)
        bl.rough(0.012, 11.0, seed=float(k))
        bl.rot('x', -rng.uniform(52, 76)).rot('z', 26.0 * u + rng.uniform(-8, 8))
        bl.move(base + V(0.17 * u, rng.uniform(-0.03, 0.05), rng.uniform(-0.10, 0.06) - 0.06 * u * u))
        sway_weights(bl, base[1] + 0.1, base[1] + 0.7, top='spine_03', low='pelvis', amt=0.5, coord=bl.v[:, 1])
        A.add(bl, look=pale if k % 3 else look, smooth=40)


'''
s = s[:i0] + new + s[i1:]
s = rep(s, "def emerge(A, bone, at, yaw, pitch, sc, look, roll=0.0, ratio=0.11, arms=75.0, melt=2, gaunt=1.0, K=None):",
        "def emerge(A, bone, at, yaw, pitch, sc, look, roll=0.0, ratio=0.11, arms=75.0, melt=2, gaunt=1.0, K=None):")
save(p, s)

# =================================================================== parts 7: skinned straps / bands instead of rigid hoops
p = H + 'asm_parts7.py'
s = load(p)
s = rep(s, """    on = 'robe' if 'robe' in A.refs else 'body'
    for zz, tl in ((1.40, 9.0), (1.28, -9.0)):             # two crossing straps seated on the coat (were one loose hoop)
        cx, cy, rx, ry = girth_of(A, on, zz)
        A.add(band((0, cy, zz), rx + 0.007, ry + 0.007, 0.03, 0.008, tilt_y=tl, n=22), bone=bone, look='strap')""",
        """    import asm_parts9 as P9
    on = 'robe' if 'robe' in A.refs else 'body'
    for zz, tl in ((1.40, 9.0), (1.28, -9.0)):             # two crossing straps seated on the coat and skinned like it (were one loose hoop)
        P9.chest_band(A, look='strap', z=zz, w=0.03, th=0.008, tilt=tl, on=on, pad=0.008)""")
s = rep(s, """        A.add(band((0, 0.01, 1.33), 0.27, 0.2, 0.05, 0.01, tilt_y=sx * 22, n=24), bone='spine_03', look=look)
        ch = KB2.drape_path(""", """        import asm_parts9 as P9
        P9.chest_band(A, look=look, z=1.31, w=0.05, th=0.01, tilt=sx * 18.0, pad=0.012)
        ch = KB2.drape_path(""")
save(p, s)

# =================================================================== weapons: boulder core, dimmer and smaller
p = H + 'asm_weapons.py'
s = load(p)
s = rep(s, "((-0.70, 0.5, 0.0, 0.10, 0.34, 0.30), (0.1, 0.5, 0.60, 0.30, 0.30, 0.09), (0.05, 0.5, -0.62, 0.26, 0.32, 0.09)):",
        "((-0.66, 0.5, 0.0, 0.07, 0.26, 0.22), (0.1, 0.5, 0.58, 0.22, 0.22, 0.06), (0.05, 0.5, -0.60, 0.2, 0.24, 0.06)):")
save(p, s)
print('round3 parts ok')
