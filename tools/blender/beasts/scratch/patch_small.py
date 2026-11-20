import os, ast
HERE = r"Y:\tools\blender\beasts"
def patch(n, pairs):
    p = os.path.join(HERE, n)
    s = open(p, encoding='utf8').read()
    for a, b in pairs:
        assert a in s, a[:80]
        s = s.replace(a, b)
    ast.parse(s)
    open(p + '.new', 'w', encoding='utf8').write(s); os.replace(p + '.new', p)
    print(n, 'ok')
# digger: paler dry grave soil and dust so it does not vanish on dark ground
patch(r'h1\spec_h1.py', [
    ("    C.look('soil', 'mud', 'bone_shell', color=(0.06, 0.046, 0.032), dry=(0.13, 0.105, 0.075), dry_amt=0.6)",
     "    C.look('soil', 'mud', 'bone_shell', color=(0.10, 0.08, 0.058), dry=(0.30, 0.25, 0.19), dry_amt=0.8)"),
    ("    C.look('limb', 'chitin', 'bone_shell', color=(0.03, 0.024, 0.02), rough=0.34, pit=170.0, dust=0.5, dust_color=(0.13, 0.10, 0.07), axis=(1, 0, 0))\n    C.look('belly', 'chitin', 'bone_shell', color=(0.05, 0.035, 0.025), rough=0.45, ridge=30.0, ridge_h=1.6, dust=0.0)\n    C.look('soil'",
     "    C.look('limb', 'chitin', 'bone_shell', color=(0.05, 0.038, 0.03), edge_color=(0.34, 0.26, 0.17), rough=0.34, pit=170.0, dust=0.7,\n           dust_color=(0.27, 0.22, 0.16), axis=(1, 0, 0))\n    C.look('belly', 'chitin', 'bone_shell', color=(0.05, 0.035, 0.025), rough=0.45, ridge=30.0, ridge_h=1.6, dust=0.0)\n    C.look('soil'"),
])
# hound: the two heat pits are its only accent - large enough to read from the game camera
patch('spec_b.py', [
    ("        C.rigid(P.blob(q - d * 0.004, 0.0105, 0.0105, 0.0105, n=6, m=4), 'head', 'pits', smooth=180)",
     "        C.rigid(P.blob(q - d * 0.003, 0.016, 0.016, 0.016, n=6, m=4), 'head', 'pits', smooth=180)"),
    ("        C.rigid(P.ring(q + d * 0.002, 0.0125, 0.0045, axis=d, n=8, m=4), 'head', 'hide', smooth=180)",
     "        C.rigid(P.ring(q + d * 0.004, 0.018, 0.0055, axis=d, n=8, m=4), 'head', 'hide', smooth=180)"),
    ("    C.look('pits', 'emis', 'emis_pits', emit=(1.0, 0.24, 0.03), strength=2.2, mottle=0.7, base_mul=0.5)",
     "    C.look('pits', 'emis', 'emis_pits', emit=(1.0, 0.30, 0.04), strength=1.6, mottle=0.4, base_mul=0.5)"),
])
patch('spec_d2.py', [
    ("    R.death_fall(dur=1.8, extra=lambda p, u, f: fold(p, 36 + 50 * f))", "    R.death_fall(dur=1.8, total=104.0, span=3, extra=lambda p, u, f: fold(p, 36 + 50 * f))"),
])
