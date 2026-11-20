"""one-off patch: serpent family after the first look (2026-11-21): spitter frill, elder glow, grub height, sizes"""
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


s = load('spec_d2.py')
# ---- sglow: dimmer, narrower
s = rep(s, "    r['color'] = N.mix(N.math('MULTIPLY', em, 0.55), r['color'], tuple(x * 0.45 for x in e))",
        "    em = N.math('MULTIPLY', em, _c(p, 'gain', 1.0))\n    r['color'] = N.mix(N.math('MULTIPLY', em, 0.5), r['color'], tuple(x * 0.3 for x in e))")
s = rep(s, "    facing = N.mapr(N.math('MULTIPLY', ny, -1.0), 0.1, 0.8)", "    facing = N.mapr(N.math('MULTIPLY', ny, -1.0), 0.3, 0.95)")
s = rep(s, "ring=14.0, ring_h=1.6, wart=40.0, wart_h=1.5, rough=0.18, dry=0.3, z0=0.3, z1=1.42, emit=(1.0, 0.40, 0.07), strength=1.5)",
        "ring=14.0, ring_h=1.6, wart=40.0, wart_h=1.5, rough=0.18, dry=0.3, z0=0.36, z1=1.36, emit=(1.0, 0.30, 0.04), strength=1.0, gain=0.6,\n"
        "           vein_scale=11.0)")
# ---- grub: lower, enlarged; leech enlarged
s = rep(s, "    Lh, n, z = 0.62, 7, 0.086", "    Lh, n, z = 0.62, 7, 0.068")
s = rep(s, "    return C.finalize(bake=bake, notes=\"looper (rig C family)\")", "    C.rescale(1.25)\n    return C.finalize(bake=bake, notes=\"looper (rig C family)\")")
s = rep(s, "    return C.finalize(bake=bake, notes=\"looper\")", "    C.rescale(1.3)\n    return C.finalize(bake=bake, notes=\"looper\")")
# ---- spitter: a ruff of dark feathery fronds with glowing tips, swept back; bigger head
a = s.index("    rng = np.random.default_rng(4)\n    fronds = {}")
b = s.index("    C.rigid(P.ring(fc, 0.07, 0.022")
s = s[:a] + '''    rng = np.random.default_rng(4)
    fronds = {}
    angs = [(a, 1.0) for a in np.linspace(-146, 146, 15)] + [(a, 0.6) for a in np.linspace(-136, 136, 14)]
    for a, k in angs:
        a = a + rng.uniform(-4, 4)
        d = fdir(math.radians(a))
        ln = (0.44 if k == 1.0 else 0.29) * rng.uniform(0.78, 1.12)
        back = 0.0 if k == 1.0 else 0.02                      # the short ones sit a little behind
        base = fc + d * 0.045 - fn_ * back
        mid = fc + d * ln * 0.55 - fn_ * (back + 0.03 * ln)
        tipp = fc + d * ln - fn_ * (back + 0.3 * ln)          # swept back like a ruff, not a flat disc
        wd = 0.02 * (0.8 + 0.5 * k)
        fr = M.join([RB.ear_sheet(base, mid, wd, cup=0.008, th=0.005, rows=3, cols=3, sink=0.03, shape=0.5, face=fn_),
                     RB.ear_sheet(mid - unit(mid - base) * 0.03, tipp, wd * 0.8, cup=0.006, th=0.004, rows=3, cols=3, sink=0.0, shape=0.6, face=fn_)])
        # barbs: two small side leaves give the frond a feathered edge
        sd = unit(np.cross(d, fn_))
        for sg2 in (1, -1):
            fr = M.join([fr, RB.ear_sheet(mid, mid + (d * 0.55 + sd * sg2 * 0.8) * 0.07 * (0.6 + k), wd * 0.6, cup=0.004, th=0.004, rows=2,
                                          cols=3, sink=0.01, shape=0.6, face=fn_)])
        sgn = 'l' if a >= 0 else 'r'
        nme = 'frill%d_%s' % (1 if abs(a) < 50 else (2 if abs(a) < 100 else 3), sgn)
        fronds.setdefault(nme, []).append(fr)
    for nme, lst in fronds.items():
        C.rigid(M.join(lst), nme, 'frond', smooth=180)
''' + s[b:]
s = rep(s, "    C.look('frond', 'frond', 'emis_frill', color=(0.075, 0.085, 0.045), vein=(0.02, 0.03, 0.012), vein_scale=26.0, rough=0.4,\n"
           "           center=fc, r0=0.2, r1=0.4, emit=(0.72, 0.9, 0.12), strength=1.6)",
        "    C.look('frond', 'frond', 'emis_frill', color=(0.11, 0.035, 0.045), vein=(0.03, 0.008, 0.012), vein_scale=30.0, rough=0.4,\n"
        "           center=fc, r0=0.27, r1=0.43, emit=(0.62, 0.85, 0.10), strength=1.3)")
s = rep(s, "    C.rigid(P.blob(hj + hd * 0.06 + V(0, 0, 0.012), 0.056, 0.085, 0.052, n=12, m=7, rot=M.rot_to((0, -1, 0), hd)), 'head', 'skin', smooth=180)\n"
           "    C.rigid(P.tubep([hj + hd * 0.10, hj + hd * 0.19, hj + hd * 0.245], [0.034, 0.026, 0.024], m=6, n=10), 'head', 'skin', smooth=180)\n"
           "    C.rigid(P.ring(hj + hd * 0.245, 0.024, 0.009, axis=hd, n=10, m=5), 'head', 'skin', smooth=180)\n"
           "    C.rigid(P.blob(hj + hd * 0.247, 0.02, 0.006, 0.02, n=8, m=4), 'head', 'maw', smooth=180)\n"
           "    for sg in (1, -1):\n"
           "        C.rigid(P.blob(hj + hd * 0.09 + V(sg * 0.045, 0, 0.03), 0.012, 0.012, 0.012, n=7, m=4), 'head', 'eye', smooth=180)",
        "    C.rigid(P.blob(hj + hd * 0.07 + V(0, 0, 0.015), 0.078, 0.115, 0.07, n=12, m=7, rot=M.rot_to((0, -1, 0), hd)), 'head', 'skin', smooth=180)\n"
        "    C.rigid(P.tubep([hj + hd * 0.12, hj + hd * 0.23, hj + hd * 0.30], [0.048, 0.037, 0.034], m=6, n=10), 'head', 'skin', smooth=180)\n"
        "    C.rigid(P.ring(hj + hd * 0.30, 0.034, 0.013, axis=hd, n=10, m=5), 'head', 'maw', smooth=180)\n"
        "    C.rigid(P.blob(hj + hd * 0.302, 0.028, 0.008, 0.028, n=8, m=4), 'head', 'maw', smooth=180)\n"
        "    for sg in (1, -1):\n"
        "        C.rigid(P.blob(hj + hd * 0.10 + V(sg * 0.062, 0, 0.04), 0.02, 0.02, 0.02, n=8, m=5), 'head', 'eye', smooth=180)")
s = rep(s, "    C.look('eye', 'bone', 'skin_body', color=(0.6, 0.62, 0.45), dirt_lo=-2.0, dirt_hi=-1.5)",
        "    C.look('eye', 'emis', 'emis_frill', emit=(0.62, 0.85, 0.10), strength=1.2, mottle=0.4, base_mul=0.7, scale=60.0)")
s = rep(s, "    C = B.Beast('mon_lurk_spitter', RD.rearer_skel(bend, neck, tail, hj + hd * 0.22, extra))",
        "    C = B.Beast('mon_lurk_spitter', RD.rearer_skel(bend, neck, tail, hj + hd * 0.30, extra))")
save('spec_d2.py', s)
