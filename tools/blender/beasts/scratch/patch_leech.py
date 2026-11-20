import os, ast
p = r"Y:\tools\blender\beasts\spec_d2.py"
s = open(p, encoding='utf8').read()
def rep(a, b):
    global s
    assert a in s, a[:80]
    s = s.replace(a, b)
rep("    C.look('skin', 'slime', 'skin_body', color=(0.085, 0.07, 0.06), color2=(0.028, 0.03, 0.032), belly=(0.26, 0.14, 0.07), axis=(0, 1, 0),\n           ring=46.0, ring_h=1.0, wart=80.0, rough=0.16, dry=0.2)",
    "    C.look('skin', 'slime', 'skin_body', color=(0.17, 0.125, 0.10), color2=(0.06, 0.05, 0.05), belly=(0.42, 0.21, 0.09), axis=(0, 1, 0),\n           ring=46.0, ring_h=1.0, wart=80.0, rough=0.14, dry=0.15)\n    C.look('lip', 'slime', 'skin_body', color=(0.58, 0.38, 0.32), color2=(0.36, 0.2, 0.17), ring=0.0, wart=120.0, rough=0.14, dry=0.0)")
rep("    C.rigid(P.ring((0, hy, z), 0.042, 0.016, axis=(0, 1, 0), n=14, m=6), 'head', 'skin', smooth=180)",
    "    C.rigid(P.ring((0, hy, z), 0.046, 0.019, axis=(0, 1, 0), n=14, m=6), 'head', 'lip', smooth=180)      # the pale sucker lip: what reads on dark ground")
ast.parse(s)
open(p + '.new', 'w', encoding='utf8').write(s); os.replace(p + '.new', p)
print('ok')
