def load(p):
    return open(p, encoding='utf-8').read()


def save(p, s):
    open(p, 'w', encoding='utf-8', newline='\n').write(s)


def rep(s, a, b, n=1):
    assert a in s, a[:110]
    return s.replace(a, b, n)


H = 'Y:/tools/blender/monsters/'
p = H + 'asm_parts9.py'
s = load(p)

# ---- gallows tree: a bulb-rooted, waisted trunk instead of a box on legs
s = rep(s, """    out_ = [(0.70, 0.03, 0.20, 0.19), (0.78, 0.03, 0.33, 0.31), (0.88, 0.028, 0.315, 0.29), (1.02, 0.024, 0.285, 0.26), (1.22, 0.02, 0.27, 0.245), (1.42, 0.02, 0.268, 0.24),
            (1.60, 0.02, 0.258, 0.232), (1.78, 0.02, 0.246, 0.222), (1.93, 0.02, 0.236, 0.214)]
    in_ = [(1.93, 0.02, 0.205, 0.186), (1.84, 0.02, 0.20, 0.18), (1.77, 0.02, 0.188, 0.168), (1.725, 0.02, 0.12, 0.11)]""",
        """    out_ = [(0.58, 0.03, 0.09, 0.09), (0.66, 0.03, 0.22, 0.21), (0.76, 0.03, 0.31, 0.29), (0.88, 0.028, 0.325, 0.30), (1.00, 0.024, 0.295, 0.27), (1.14, 0.022, 0.255, 0.235),
            (1.30, 0.02, 0.24, 0.222), (1.46, 0.02, 0.268, 0.238), (1.62, 0.02, 0.25, 0.224), (1.78, 0.02, 0.228, 0.206), (1.93, 0.02, 0.212, 0.192)]
    in_ = [(1.93, 0.02, 0.182, 0.165), (1.84, 0.02, 0.178, 0.16), (1.77, 0.02, 0.168, 0.15), (1.725, 0.02, 0.11, 0.10)]""")
s = rep(s, """        m_ = 1.0 + 0.055 * np.sin(5 * th_ + 2.2 * z_ + seed) + 0.04 * np.sin(9 * th_ - 3.1 * z_ + 2 * seed) + 0.03 * np.sin(14 * th_ + 5.0 * z_)
        return m_ + 0.11 * np.maximum(0.0, np.sin(4 * th_ + 0.7)) ** 3 * float(M.smoothstep(1.15, 0.78, z_))""",
        """        m_ = 1.0 + 0.07 * np.sin(5 * th_ + 2.2 * z_ + seed) + 0.05 * np.sin(9 * th_ - 3.1 * z_ + 2 * seed) + 0.035 * np.sin(14 * th_ + 5.0 * z_)
        return m_ + 0.16 * np.maximum(0.0, np.sin(4 * th_ + 0.7)) ** 3 * float(M.smoothstep(1.2, 0.74, z_))""")
s = rep(s, "    A.add(revolve([(0.0, 1.762), (0.12, 1.758), (0.155, 1.742), (0.0, 1.73)], n=14).move(eb), bone='spine_03', look=glow, smooth=60)      # embers in the hollow, seen from above",
        "    A.add(revolve([(0.0, 1.762), (0.11, 1.758), (0.14, 1.742), (0.0, 1.73)], n=14).move(eb), bone='spine_03', look=glow, smooth=60)       # embers in the hollow, seen from above")
s = rep(s, "        rr = 0.085 if k else 0.0\n        A.add(KX.lump(0.04, 0.035, 0.026, seed=k, amp=0.35)", "        rr = 0.075 if k else 0.0\n        A.add(KX.lump(0.036, 0.032, 0.024, seed=k, amp=0.35)")
s = rep(s, "    my = 0.02 - 0.243 * float(bark(np.array([-math.pi / 2]), mz)[0]) + off(mz)[1]", "    my = 0.02 - 0.228 * float(bark(np.array([-math.pi / 2]), mz)[0]) + off(mz)[1]")

# ---- colossus: sparks are small and sit in the creases
s = rep(s, """        c = V(at) + V(0.12 * math.sin(rad(yaw)), -0.12 * math.cos(rad(yaw)), 0.10 * sc)          # sparks in the crease below each figure
        if k % 2 == 0:
            A.add(KX.lump(0.04, 0.04, 0.03, seed=k, amp=0.3, n=7, m=4).move(c), bone=bone, look=spark, smooth=80, keep=Kf)""",
        """        c = V(at) + V(0.05 * math.sin(rad(yaw)), -0.05 * math.cos(rad(yaw)), 0.06 * sc)          # sparks in the crease below each figure
        if k % 2 == 0:
            A.add(KX.lump(0.03, 0.03, 0.02, seed=k, amp=0.3, n=7, m=4).move(c), bone=bone, look=spark, smooth=80, keep=Kf)""")

# ---- tallow mound: each arm is an upper body (waist in the cone, head for a fist), not a whole stretched figure
i0 = s.index("    # two arms that are whole bodies stretched thin")
new = '''    # two arms that are bodies: waist inside the cone, the trunk for an arm, the head for a fist (skinned to the arm bones)
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        b = P7.half_body(ratio=0.2, arms_down=86.0, gaunt=0.6, jaw=10.0)
        b.v[:, 0] *= 1.0 - 0.42 * np.clip(b.wsum(arm_bones()) * 1.2, 0, 1)                         # its own arms pressed to its sides
        M.smooth_verts(b, 2, 0.5)
        sh = R.H('upperarm' + s)
        L_ = float(np.linalg.norm(R.H('hand' + s) - sh)) + 0.12
        kz = (L_ + 0.12) / 0.78
        ax = V(sx, 0, 0)
        q = b.v.copy()
        # figure Z -> along the arm, figure Y stays (its face looks forward when the arm hangs), figure X -> -/+ Z
        b.v = np.outer((q[:, 2] - 0.98) * kz - 0.12, ax) + np.outer((q[:, 1] - 0.02) * 0.9, V(0, 1, 0)) + np.outer(q[:, 0] * 0.74, V(0, 0, -sx)) + sh
        b.recalc()
        KC.skin_cloth(b, body=A.rest, smooth=3, limit=['spine_03', 'clavicle' + s, 'upperarm' + s, 'lowerarm' + s, 'hand' + s])
        A.add(b, look=look, smooth=180)
        for t, rr in ((0.25, 0.07), (0.8, 0.06)):                   # wax runs hanging from the arm
            p0 = KC3.BP('lowerarm' + s, t) + V(0, 0.0, -rr)
            A.add(tube([p0 + V(0, 0, 0.03), p0 + V(sx * 0.02, 0, -0.05), p0 + V(sx * 0.03, 0, -0.12)], [0.02, 0.014, 0.004], n=5), bone='lowerarm' + s, look=look, smooth=80)
'''
s = s[:i0] + new + s[s.index("\n\n# ================================================================================================ small things"):]
save(p, s)

p = H + 'specs_s4_c.py'
s = load(p)
s = rep(s, "dict(p='cracks', look='spark', regions=('thighs', 'upperarms', 'forearms'), count=10, seed=5, width=0.012),", "dict(p='cracks', look='spark', regions=('thighs', 'upperarms', 'forearms'), count=6, seed=5, width=0.011),")
s = rep(s, """      "ridge of its back (the top one still burns, emis_), and two arms that are whole bodies stretched thin."),""",
        """      "ridge of its back (the top one still burns, emis_), and two arms that are bodies (waist in the cone, the head for a fist)."),""")
save(p, s)
print('round4 ok')
