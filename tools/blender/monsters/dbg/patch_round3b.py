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
i0 = s.index("@part\ndef carrier(")
i1 = s.index("# ================================================================================================ lamp: the crowned colony")
new = '''def arc_light(A, c, nrm, r, bone, glow, housing, keep=False):
    """a small arc lamp: a glowing lens sunk in a turned collar"""
    Rm = M.rot_to((0, 0, 1), unit(nrm))
    col = revolve([(r * 1.55, 0.0), (r * 1.6, 0.012), (r * 1.25, 0.022), (r * 1.05, 0.016), (r * 1.05, 0.0)], n=14)
    A.add(col.tf(Rm).move(V(c)), bone=bone, look=housing, smooth=40, keep=keep)
    lens = revolve([(r * 1.05, 0.006), (r * 0.8, 0.014), (r * 0.4, 0.019), (0.0, 0.02)], n=12)
    A.add(lens.tf(Rm).move(V(c)), bone=bone, look=glow, smooth=180, keep=keep)


@part
def carrier(A, look='brass', dark='brass_dark', glow='arc', bright='brass_bright', fin=1.25, seed=2):
    """a towering two-legged brass porter: a blocky riveted body with a boiler hump, thick piston legs (sleeve + polished
    rod, drum knees with small arc lamps), a small sunken head-lantern, arms that are lifting forks (girder arms, two
    blunt tines at the wrist that point forward when the arm hangs), and on its back an A-frame carrying one huge wedge of
    the Brass Sun that stands above it like a sail.  Use with a hidden body (show=[])."""
    R = A.R
    N = 16
    # ---- body
    A.add(box(0.54, 0.38, 0.50, bevel=0.04).move(0, 0.025, 1.245), bone='spine_02', look=look, smooth=30)
    for sxx in (-0.15, 0.15):                                       # front straps with rivets
        A.add(box(0.05, 0.012, 0.44, bevel=0.004).move(sxx, -0.168, 1.245), bone='spine_02', look=dark, smooth=30)
        for zz in np.linspace(1.06, 1.43, 6):
            A.add(KC3.rivet(V(sxx, -0.173, zz), (0, -1, 0), r=0.011, n=6), bone='spine_02', look=look)
    for syy, zz in ((-0.168, 1.12), (-0.168, 1.40)):                # cross straps
        A.add(box(0.50, 0.01, 0.035, bevel=0.003).move(0, syy, zz), bone='spine_02', look=dark, smooth=30)
    arc_light(A, (0, -0.166, 1.27), (0, -1, 0), 0.05, 'spine_02', glow, dark)                                                           # chest port
    for k in range(3):                                              # louvres under the port
        A.add(box(0.16, 0.014, 0.012).move(0, -0.17, 1.165 - 0.022 * k), bone='spine_02', look=dark)
    A.add(box(0.58, 0.42, 0.045, bevel=0.012).move(0, 0.025, 1.02), bone='spine_02', look=dark, smooth=30)
    A.add(box(0.60, 0.43, 0.05, bevel=0.012).move(0, 0.025, 1.49), bone='spine_02', look=dark, smooth=30)
    for sxx in (-1, 1):
        for syy in (-1, 1):
            for zz in (1.02, 1.49):
                for xx in (0.09, 0.25):
                    A.add(KC3.rivet(V(sxx * xx, 0.025 + syy * 0.215, zz), (0, syy, 0), r=0.012, n=6), bone='spine_02', look=look)
        for zz in np.linspace(1.08, 1.42, 4):                       # side seams
            A.add(KC3.rivet(V(sxx * 0.272, 0.025, zz), (sxx, 0, 0), r=0.012, n=6), bone='spine_02', look=look)
        A.add(cyl((sxx * 0.20, 0.16, 1.50), (sxx * 0.20, 0.19, 1.74), 0.036, n=10), bone='spine_02', look=dark, smooth=40)              # twin stacks
        A.add(torus(0.04, 0.01, n=10, m=4).move(sxx * 0.20, 0.19, 1.74), bone='spine_02', look=look)
    A.add(box(0.42, 0.31, 0.20, bevel=0.03).move(0, 0.02, 0.93), bone='pelvis', look=look, smooth=30)
    A.add(cyl((0, 0.02, 0.985), (0, 0.02, 1.06), 0.14, n=N), bone='spine_01', look=dark, smooth=40)                                     # waist bearing
    # ---- head-lantern, small and sunken between the shoulders
    A.add(revolve([(0.13, 0.0), (0.145, 0.02), (0.135, 0.055), (0.10, 0.06), (0.10, 0.0)], n=N).move(0, 0.0, 1.512), bone='spine_03', look=dark, smooth=40)
    A.add(cyl((0, 0.0, 1.50), (0, 0.0, 1.60), 0.066, n=12), bone='Head', look=glow, smooth=60)
    for k in range(8):
        a = 2 * math.pi * k / 8
        A.add(cyl((0.072 * math.cos(a), 0.072 * math.sin(a), 1.50), (0.072 * math.cos(a), 0.072 * math.sin(a), 1.61), 0.009, n=4), bone='Head', look=dark)
    for zz in (1.535, 1.57):
        A.add(torus(0.072, 0.007, n=N, m=4).move(0, 0, zz), bone='Head', look=dark)
    A.add(revolve([(0.092, 0.0), (0.097, 0.012), (0.06, 0.04), (0.02, 0.055), (0.02, 0.075), (0.0, 0.078)], n=N).move(0, 0, 1.60), bone='Head', look=look, smooth=40)
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        # ---- legs: thick pistons
        hp = R.H('thigh' + s); kn = R.H('calf' + s); an = R.H('foot' + s)
        A.add(cyl(hp + V(-sx * 0.06, 0, 0), hp + V(sx * 0.085, 0, 0), 0.125, n=N), bone='pelvis', look=dark, smooth=40)                 # hip drum
        arc_light(A, hp + V(sx * 0.086, 0, 0), (sx, 0, 0), 0.034, 'pelvis', glow, look)
        tdir = unit(kn - hp); tl = float(np.linalg.norm(kn - hp))
        A.add(cyl(hp + tdir * 0.03, hp + tdir * tl * 0.64, 0.118, n=N, r1=0.11), bone='thigh' + s, look=look, smooth=40)                  # sleeve
        for tt in (0.14, 0.38, 0.64):
            A.add(torus(0.12, 0.013, n=N, m=4).tf(M.rot_to((0, 0, 1), tdir)).move(hp + tdir * tl * tt), bone='thigh' + s, look=dark)
        A.add(cyl(hp + tdir * tl * 0.58, kn, 0.058, n=12), bone='thigh' + s, look=bright, smooth=50)                                      # polished rod
        A.add(cyl(kn + V(-0.10, 0, 0), kn + V(0.10, 0, 0), 0.10, n=N), bone='calf' + s, look=dark, smooth=40)                              # knee drum
        for e in (-1, 1):
            arc_light(A, kn + V(e * 0.101, 0, 0), (e, 0, 0), 0.03, 'calf' + s, glow, look)
        cdir = unit(an - kn); cl = float(np.linalg.norm(an - kn))
        A.add(cyl(kn, kn + cdir * cl * 0.5, 0.055, n=12), bone='calf' + s, look=bright, smooth=50)
        A.add(cyl(kn + cdir * cl * 0.40, an + V(0, 0, 0.02), 0.105, n=N, r1=0.12), bone='calf' + s, look=look, smooth=40)
        for tt in (0.40, 0.72):
            A.add(torus(0.112, 0.013, n=N, m=4).tf(M.rot_to((0, 0, 1), cdir)).move(kn + cdir * cl * tt), bone='calf' + s, look=dark)
        A.add(box(0.23, 0.38, 0.105, bevel=0.022).move(sx * 0.092, -0.06, 0.053), bone='foot' + s, look=look, smooth=30)
        A.add(box(0.25, 0.14, 0.055, bevel=0.012).move(sx * 0.092, -0.215, 0.03), bone='ball' + s, look=dark, smooth=30)
        A.add(cyl(an + V(-0.085, 0, 0.0), an + V(0.085, 0, 0.0), 0.07, n=12), bone='foot' + s, look=dark, smooth=40)
        for xx in (-0.07, 0.07):
            A.add(KC3.rivet(V(sx * 0.092 + xx, -0.25, 0.058), (0, 0, 1), r=0.014, n=6), bone='ball' + s, look=look)
        # ---- arms: girders ending in lifting forks
        shp = R.H('upperarm' + s); el = R.H('lowerarm' + s); wr = R.H('hand' + s)
        A.add(cyl(shp + V(-sx * 0.10, 0, 0), shp + V(sx * 0.06, 0, 0), 0.115, n=N), bone='spine_03', look=dark, smooth=40)              # shoulder drum
        ul = float(np.linalg.norm(el - shp))
        A.add(box(ul, 0.085, 0.11, bevel=0.01).move((shp + el) / 2), bone='upperarm' + s, look=look, smooth=30)
        for fz in (-0.055, 0.055):
            A.add(box(ul * 0.96, 0.115, 0.018, bevel=0.004).move((shp + el) / 2 + V(0, 0, fz)), bone='upperarm' + s, look=dark, smooth=30)
        for xx in np.linspace(-0.3, 0.3, 4):
            A.add(KC3.rivet((shp + el) / 2 + V(ul * xx, -0.044, 0), (0, -1, 0), r=0.011, n=6), bone='upperarm' + s, look=look)
        A.add(cyl(el + V(0, -0.08, 0), el + V(0, 0.08, 0), 0.08, n=12), bone='lowerarm' + s, look=dark, smooth=40)
        arc_light(A, el + V(0, -0.081, 0), (0, -1, 0), 0.026, 'lowerarm' + s, glow, look)
        fl = float(np.linalg.norm(wr - el))
        A.add(box(fl + 0.08, 0.07, 0.095, bevel=0.008).move((el + wr) / 2 + V(sx * 0.04, 0, 0)), bone='lowerarm' + s, look=look, smooth=30)
        # fork carriage at the wrist: two tines side by side (T-pose Z = sideways once the arm hangs), pointing forward (-Y)
        A.add(box(0.085, 0.06, 0.32, bevel=0.01).move(wr + V(sx * 0.06, -0.01, 0)), bone='lowerarm' + s, look=dark, smooth=30)
        for tz in (-0.115, 0.115):
            hx = 0.017                                               # half thickness of a tine (along the arm), half width 0.04
            tine = M.loft(np.array([[(-hx, 0.0, -0.04), (hx, 0.0, -0.04), (hx, 0.0, 0.04), (-hx, 0.0, 0.04)],
                                    [(-hx, -0.30, -0.04), (hx, -0.30, -0.04), (hx, -0.30, 0.04), (-hx, -0.30, 0.04)],
                                    [(-hx * 0.9, -0.42, -0.036), (hx * 0.2, -0.42, -0.036), (hx * 0.2, -0.42, 0.036), (-hx * 0.9, -0.42, 0.036)],
                                    [(-hx * 0.9, -0.47, -0.028), (-hx * 0.5, -0.47, -0.028), (-hx * 0.5, -0.47, 0.028), (-hx * 0.9, -0.47, 0.028)]]), cap0=True, cap1=True).recalc()
            tine.smooth = 30.0
            if sx < 0:
                tine.mirror()
            A.add(tine.move(wr + V(sx * 0.09, -0.035, tz)), bone='lowerarm' + s, look=bright)
            A.add(box(0.22, 0.036, 0.08, bevel=0.006).move(wr + V(-sx * 0.01, -0.02, tz)), bone='lowerarm' + s, look=look, smooth=30)       # the tine's heel up the arm
            A.add(KC3.rivet(wr + V(sx * 0.06, -0.042, tz), (0, -1, 0), r=0.014, n=6), bone='lowerarm' + s, look=look)
    # ---- the A-frame and the wedge of the Brass Sun
    K = V(0, 0.30, 1.30)
    bone = 'spine_03'
    lean = 9.0
    B = V(0, 0.335, 1.085)                                          # where the middle of the rim stands
    for q, lk in sun_wedge(L_=fin, half=20.0):
        q.move(0, 0, fin).rot('x', -lean).move(B)
        A.add(q, bone=bone, look={'brass_bright': bright, 'brass': look, 'brass_dark': dark}[lk], smooth=30, keep=K)
    top = B + V(0, fin * 0.66 * math.sin(rad(lean)) + 0.07, fin * 0.66 * math.cos(rad(lean)))
    for sx in (1, -1):
        foot = V(sx * 0.30, 0.215, 1.02)
        A.add(tube([foot, top + V(sx * 0.03, 0, 0)], 0.024, n=6), bone=bone, look=dark, smooth=40, keep=K)
        A.add(tube([foot, foot + V(0, 0.15, 0.03)], 0.022, n=6), bone=bone, look=dark, smooth=40, keep=K)                                   # ledge arm under the rim
        A.add(tube([V(sx * 0.19, 0.20, 1.44), V(sx * 0.21, 0.24, 1.46)], 0.02, n=6), bone=bone, look=dark, smooth=40, keep=K)
        mid = foot + (top - foot) * 0.5
        A.add(tube([mid, V(sx * 0.16, 0.22, 1.47)], 0.016, n=5), bone=bone, look=dark, smooth=40, keep=K)                                   # stay to the body
    A.add(box(0.78, 0.10, 0.045, bevel=0.01).move(0, 0.335, 1.045), bone=bone, look=dark, smooth=30, keep=K)                              # the ledge the rim stands on
    A.add(box(0.46, 0.04, 0.04, bevel=0.008).move(0, 0.27, 1.46), bone=bone, look=dark, smooth=30, keep=K)
    A.add(box(0.10, 0.11, 0.07, bevel=0.012).move(top + V(0, -0.03, 0)), bone=bone, look=dark, smooth=30, keep=K)                          # the clamp at the top
    for sx in (1, -1):                                              # chains from the clamp to the wedge's cut edges
        e = B + V(sx * fin * 0.80 * math.sin(rad(20.0)) * 0.9, fin * 0.80 * math.sin(rad(lean)) + 0.03, fin * 0.80 * math.cos(rad(lean)) * 0.62)
        A.add(M.chain(np.array([top + V(sx * 0.04, 0, 0), e]), link=0.05, r=0.008), bone=bone, look=dark, smooth=60, keep=K)


'''
s = s[:i0] + new + s[i1:]
save(p, s)

# =================================================================== specs
p = H + 'specs_s4_c.py'
s = load(p)
s = rep(s, "looks={'ember': {'strength': 2.0, 'mottle': 0.6}, 'charwood': {'heat': (1.0, 0.32, 0.04), 'heat_amt': 0.22}},",
        "looks={'ember': {'emit': (1.0, 0.22, 0.025), 'strength': 1.8, 'mottle': 0.7, 'base_mul': 0.3}, 'charwood': {'heat': (1.0, 0.22, 0.03), 'heat_amt': 0.10}},\n"
        "        jpeg=(88, 86, 92),")
s = rep(s, "looks={'coldfire': {'emit': (0.20, 0.50, 0.95), 'strength': 1.5, 'mottle': 0.7, 'crust': 0.5, 'scale': 9.0}},", "looks={},")
s = rep(s, "looks={'arc': {'strength': 2.2, 'mottle': 0.15}, 'brass_bright': {'amount': 0.08, 'rough': 0.14}},", "looks={'brass_bright': {'amount': 0.05, 'rough': 0.12}},")
s = rep(s, "looks={'glow': {'strength': 1.6, 'mottle': 0.3}, 'gill': {'strength': 1.6},\n               'rag': {'color': (0.085, 0.075, 0.06), 'stain_amt': 0.9}},",
        "looks={'rag': {'color': (0.085, 0.075, 0.06), 'stain_amt': 0.9}},")
s = rep(s, "family='ash', scale=1.9, poses='brute', emis=(1.0, 0.22, 0.06), budget=22000, jpeg=(86, 84, 90),", "family='ash', scale=1.9, poses='brute', emis=(1.0, 0.22, 0.06), budget=22000, jpeg=(84, 82, 88),")
s = rep(s, "props=dict(bulk=1.42, shoulders=1.18, chest=1.08, hips=1.05, leg=1.12, arm=1.2, hunch=14, belly=0.15),", "props=dict(bulk=1.42, shoulders=1.18, chest=1.08, hips=1.05, leg=1.3, arm=1.2, hunch=14, belly=0.15),")
s = rep(s, "looks={'spark': {'strength': 1.4}, 'ember': {'strength': 1.4, 'mottle': 0.7}},", "looks={},")
s = rep(s, "dict(p='cracks', look='spark', regions=('torso', 'thighs', 'upperarms'), count=18, seed=5, width=0.02),", "dict(p='cracks', look='spark', regions=('thighs', 'upperarms', 'forearms'), count=10, seed=5, width=0.012),")
save(p, s)

p = H + 'specs_s4_b.py'
s = load(p)
s = rep(s, "looks={'glow': {'strength': 1.4, 'mottle': 0.5}, 'hood': ['sealskin', {}], 'oak': {'color': (0.13, 0.125, 0.115)}},",
        "looks={'glow': {'emit': (0.10, 0.34, 0.9), 'strength': 1.4, 'mottle': 0.6, 'base_mul': 0.25}, 'hood': ['sealskin', {}], 'oak': {'color': (0.13, 0.125, 0.115)}},")
s = rep(s, "'ember': {'strength': 1.5, 'mottle': 0.7},\n               'apron':", "'ember': {'emit': (1.0, 0.2, 0.02), 'strength': 1.4, 'mottle': 0.8, 'base_mul': 0.25},\n               'apron':")
s = rep(s, "looks={'eye': {'strength': 2.2}, 'glow': {'strength': 1.6, 'mottle': 0.6}, 'rag': {'color': (0.12, 0.12, 0.125), 'stain_amt': 0.5}},",
        "looks={'eye': {'strength': 2.2}, 'glow': {'emit': (1.0, 0.30, 0.05), 'strength': 1.3, 'mottle': 0.8, 'base_mul': 0.2, 'crust': 0.5},\n"
        "               'rag': {'color': (0.12, 0.12, 0.125), 'stain_amt': 0.5}},")
s = rep(s, """            dict(p='glow_blob', bone='spine_03', at=(0.0, 0.16, 1.26), r=(0.085, 0.06, 0.12)),
            dict(p='glow_blob', bone='spine_03', at=(0.12, 0.10, 1.10), r=(0.05, 0.05, 0.07)),
            dict(p='glow_blob', bone='spine_03', at=(-0.13, 0.09, 1.12), r=(0.05, 0.05, 0.07)),""",
        """            dict(p='glow_blob', bone='spine_03', at=(0.0, 0.17, 1.22), r=(0.075, 0.055, 0.11)),
            dict(p='glow_blob', bone='spine_03', at=(0.11, 0.20, 1.06), r=(0.045, 0.04, 0.06)),
            dict(p='glow_blob', bone='spine_03', at=(-0.12, 0.19, 1.08), r=(0.045, 0.04, 0.06)),""")
save(p, s)

p = H + 'specs_s4_a.py'
s = load(p)
s = rep(s, "props=dict(bulk=1.3, shoulders=1.16, chest=1.12, arm=1.2, arm_len=1.15, hunch=26, leg=1.1),", "props=dict(bulk=1.3, shoulders=1.16, chest=1.12, arm=1.2, arm_len=1.15, hunch=18, leg=1.1),")
s = rep(s, "'slag': {'strength': 1.6}},", "'slag': {'strength': 1.6}, 'brand': {'emit': (1.0, 0.24, 0.03)}},")
save(p, s)

# =================================================================== verify: giants may reach 22,000 triangles
p = H + 'verify.py'
s = load(p)
a = "LIMITS = {'boss_osaros': dict(tris=45000, mb=12.6, mats=12)}"
assert a in s
s = s.replace(a, a + """
# giants of the bestiary (body plan "giant / construct", engine scale >= 1.75): the lead allows them 22,000 triangles
LIMITS.update({k: dict(tris=22000) for k in ('mon_ash_colossus', 'mon_engine_carrier', 'mon_hauler_yoke', 'mon_hauler_hod', 'mon_hauler_collector',
                                             'mon_hauler_stoker')})""")
save(p, s)
print('round3b ok')
