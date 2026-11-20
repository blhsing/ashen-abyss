"""one-off patch: ghost face / smoke / spill of the lantern, fin / eyes / mud of the eel (2026-11-20)"""
import os, ast
HERE = r"Y:\tools\blender\beasts"


def load(n):
    return open(os.path.join(HERE, n), encoding='utf8').read()


def save(n, s):
    ast.parse(s)
    p = os.path.join(HERE, n)
    open(p + '.new', 'w', encoding='utf8').write(s)
    os.replace(p + '.new', p)
    print(n, 'ok')


def rep(s, a, b):
    assert a in s, a[:90]
    return s.replace(a, b)


s = load('spec_f.py')
s = rep(s, """L.KINDS.update(warm=k_warm)
L.GRIME.update(warm=((0.022, 0.017, 0.013), 0.85))""", '''def k_ghostface(N, p):
    """a face of glowing smoke: pale, lit from within, with dark hollows painted where the eye sockets and the mouth
    are (`holes` = [(centre, radius, z stretch), ...] in object space) and in every crease"""
    e = _c(p, 'emit', (0.6, 0.85, 1.0))
    col = _c(p, 'color', (0.55, 0.66, 0.72))
    st = N.noise(22, 3, 0.6, vec=N.mapping(N.warp(0.02, 6), scale=(1, 1, 0.22)))
    m = N.mapr(st, 0.2, 0.8, 0.62, 1.0)
    for c, r, zs in p.get('holes', []):
        vm = N.new('ShaderNodeVectorMath', operation='DISTANCE')
        N.set(vm.inputs[0], N.mapping(N.coord(), scale=(1, 1, 1.0 / zs)))
        N.set(vm.inputs[1], (float(c[0]), float(c[1]), float(c[2]) / zs))
        m = N.math('MULTIPLY', m, N.mapr(vm.outputs['Value'], r * 0.55, r * 1.25))
    m = N.math('MULTIPLY', m, N.mapr(N.cavity(), 0.75, 0.2, 0.35, 1.0))
    c = N.mix(m, (0.012, 0.02, 0.035), col)
    return dict(color=c, rough=0.85, metal=0.0, height=st, bump=(0.3, 0.004), emit=(e, _c(p, 'strength', 1.0)), emask=m)


L.KINDS.update(warm=k_warm, ghostface=k_ghostface)
L.GRIME.update(warm=((0.022, 0.017, 0.013), 0.85))
L.NO_FINISH = tuple(L.NO_FINISH) + ('ghostface',)''')
s = rep(s, """    T1 = [V(0.02, -0.03, h + 0.14), V(0.06, 0.03, h + 0.21), V(0.03, 0.10, h + 0.25), V(-0.05, 0.13, h + 0.22)]
    T2 = [V(-0.05, -0.04, h + 0.06), V(-0.14, -0.02, h + 0.10), V(-0.25, 0.02, h + 0.17), V(-0.31, 0.0, h + 0.27)]
    T3 = [V(0.05, 0.0, h + 0.0), V(0.13, 0.07, h + 0.03), V(0.23, 0.15, h + 0.10), V(0.28, 0.2, h + 0.21)]""",
        """    T1 = [V(0.01, -0.02, h + 0.15), V(0.03, 0.10, h + 0.20), V(-0.02, 0.24, h + 0.19), V(0.03, 0.40, h + 0.24)]
    T2 = [V(-0.06, -0.01, h + 0.05), V(-0.10, 0.12, h + 0.07), V(-0.07, 0.27, h + 0.04), V(-0.12, 0.39, h + 0.08)]
    T3 = [V(0.06, 0.0, h - 0.04), V(0.09, 0.11, h - 0.02), V(0.05, 0.22, h - 0.06), V(0.09, 0.31, h - 0.03)]""")
s = rep(s, "    gc = V(0.0, -0.075, h + 0.03)                            # centre of the ghost's head",
        "    gc = V(0.0, -0.07, h + 0.03)                             # centre of the ghost's head\n"
        "    EYES = [gc + V(0.043, -0.10, 0.045), gc + V(-0.043, -0.10, 0.045)]\n"
        "    MOUTH = gc + V(0, -0.088, -0.085)")
s = rep(s, "    C.look('ghost', 'emis', 'spectral_ghost', emit=(0.55, 0.80, 1.0), strength=1.0, mottle=0.5, base_mul=0.7, scale=25.0)",
        "    C.look('ghost', 'emis', 'spectral_ghost', emit=(0.55, 0.80, 1.0), strength=1.0, mottle=0.5, base_mul=0.7, scale=25.0)\n"
        "    C.look('face', 'ghostface', 'emis_face', emit=(0.62, 0.86, 1.0), strength=1.0,\n"
        "           holes=[(EYES[0], 0.034, 1.25), (EYES[1], 0.034, 1.25), (MOUTH, 0.036, 1.7)])")
a = s.index("    head = P.blob(gc, 0.088, 0.10, 0.13, n=18, m=11)")
b = s.index("    stem = P.tubep(")
s = s[:a] + '''    head = P.blob(gc, 0.10, 0.11, 0.155, n=18, m=11)
    k = np.clip((gc[2] + 0.01 - head.v[:, 2]) / 0.14, 0, 1)
    head.v[:, 0] *= 1 - 0.45 * k                                    # long narrow jaw
    for sg in (1, -1):
        head.dent(gc + V(sg * 0.075, -0.07, -0.035), 0.05, -0.024)  # sunken cheeks
        head.dent(gc + V(sg * 0.045, -0.10, 0.095), 0.04, 0.012)    # brow
        head.dent(gc + V(sg * 0.07, -0.085, 0.01), 0.03, 0.008)     # cheekbone
    cut = False
    try:
        hb = head
        for c, r3, tilt in ((EYES[0], (0.03, 0.07, 0.04), -18.0), (EYES[1], (0.03, 0.07, 0.04), 18.0), (MOUTH, (0.03, 0.07, 0.062), 0.0)):
            e = M.ellipsoid(r3[0], r3[1], r3[2], n=10, m=6)
            e.rot('y', tilt).move(*c)
            hb = M.boolean(hb, e, 'DIFFERENCE')
        if hb.ntri > head.ntri * 0.8:
            head = hb.recalc()
            cut = True
    except Exception as ex:
        B.log("ghost face boolean failed:", ex)
    if not cut:
        for c in EYES + [MOUTH]:
            head.dent(c, 0.035, -0.04)
    head.smooth = 60.0
    C.rigid(head, 'wisp', 'face')
    # a cowl of thinner smoke round the back of the head (ghost matter: additive in the game)
    veil = P.blob(gc + V(0, 0.055, 0.0), 0.125, 0.125, 0.185, n=14, m=8)
    C.rigid(veil, 'wisp', 'ghost', smooth=180)
''' + s[b:]
a = s.index("    for i, rr in enumerate((0.95, 0.7, 0.47, 0.27)):")
b = s.index("    # ---- carrying chain under the floor")
s = s[:a] + '''    rgs = np.random.default_rng(5)
    for i, rr in enumerate((0.8, 0.46)):
        n = 28
        rad = rr * (1 + 0.16 * np.sin(np.arange(n) * 2 * math.pi * 3 / n + i) + rgs.uniform(-0.07, 0.07, n))
        v = [(0, 0, 0.07 + 0.004 * i)] + [(rad[j] * math.cos(2 * math.pi * j / n), rad[j] * math.sin(2 * math.pi * j / n), 0.07 + 0.004 * i)
                                           for j in range(n)]
        d = M.Part(np.array(v), [(0, 1 + j, 1 + (j + 1) % n) for j in range(n)])
        d.two = True
        d.smooth = 180.0
        C.rigid(d, 'spill', 'ghost', free=True)
''' + s[b:]
s = rep(s, "        t = P.tubep([T[0] - unit(T[1] - T[0]) * 0.03] + T, [0.034, 0.03, 0.022, 0.013, 0.004], m=12, n=6)",
        "        t = P.tubep([T[0] - unit(T[1] - T[0]) * 0.03] + T, [0.03, 0.026, 0.02, 0.012, 0.003], [0.045, 0.04, 0.03, 0.018, 0.004], m=14, n=6)")
save('spec_f.py', s)

s = load('spec_d.py')
a = s.index("    bl = back_line(pts, rf[:-1], off=-0.014, per=4, z0=0.14)")
b = s.index("    # ---- skull: a wide flat armoured spade with a flat palate")
s = s[:a] + '''    bl = back_line(pts, rf[:-1], off=-0.016, per=4, z0=0.16)
    rng = np.random.default_rng(11)
    saw = (0.04, 0.09, 0.14, 0.18, 0.21)                     # each pennant: tall at its upper corner, torn away below
    rows = 4
    G = np.zeros((len(bl), rows + 1, 3))
    for i, (q, nr) in enumerate(bl):
        hgt = saw[i % 5] * S * rng.uniform(0.75, 1.2) * (0.7 + 0.3 * math.sin(i * 0.37))
        if rng.random() < 0.14:
            hgt *= 0.4                                        # a tear
        tl = unit(bl[min(i + 1, len(bl) - 1)][0] - bl[max(i - 1, 0)][0])
        for j in range(rows + 1):
            t = j / float(rows)
            G[i, j] = (q - nr * 0.02 * (1 - t) + nr * hgt * t - tl * hgt * 0.35 * t * t + V(0, 0, -1) * hgt * 0.45 * t * t
                       + rng.normal(0, 0.004, 3) * t)
    fin = P.sheet_solid(G, 0.008)
    C.skin(RD.skin_to(fin, seg + ['head'], sharp=4.0), 'fin', smooth=180)
''' + s[b:]
s = rep(s, "C.look('eye', 'emis', 'emis_eyes', emit=(0.75, 0.9, 0.7), strength=1.3, mottle=0.5, base_mul=0.7, scale=60.0)",
        "C.look('eye', 'emis', 'emis_eyes', emit=(0.50, 0.66, 0.42), strength=1.0, mottle=0.9, base_mul=0.8, scale=90.0)")
s = rep(s, "    lip = P.ring((0, 0, 0.012), 0.265 * S, 0.085 * S, n=20, m=6).scale((1, 1, 0.5))\n    lip.rough(0.02, 7.0, seed=6)",
        "    lip = P.ring((0, 0, 0.012), 0.28 * S, 0.11 * S, n=22, m=6).scale((1, 1, 0.42))\n    lip.rough(0.028, 6.0, seed=6)")
s = rep(s, "        C.rigid(P.ring((0, 0, 0.008), 0.44 * S, 0.024 * S, n=22, m=5).scale((1, 1, 0.55)), b, 'mud', smooth=180, free=True)",
        "        rp = P.ring((0, 0, 0.03), 0.47 * S, 0.06 * S, n=24, m=5).scale((1, 1, 0.2))        # a low wide swell of mud, not a hoop\n"
        "        rp.rough(0.012, 9.0, seed=4)\n"
        "        C.rigid(rp, b, 'mud', smooth=180, free=True)")
s = rep(s, "    C.look('mud', 'mud', 'rock_mound', color=(0.06, 0.048, 0.035), dry=(0.15, 0.125, 0.09))",
        "    C.look('mud', 'mud', 'rock_mound', color=(0.075, 0.06, 0.042), dry=(0.19, 0.16, 0.115))")
save('spec_d.py', s)
