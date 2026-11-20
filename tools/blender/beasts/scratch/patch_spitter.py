"""one-off patch (2026-11-21): the spitter rears in an S-bend and its tail lies in a curve (the eel's lesson: no pipes)"""
import os, ast
HERE = r"Y:\tools\blender\beasts"
p = os.path.join(HERE, 'spec_d2.py')
s = open(p, encoding='utf8').read()


def rep(s, a, b):
    assert a in s, a[:90]
    return s.replace(a, b)


s = rep(s, """    neck = [V(0, -0.07, 0.30), V(0, -0.115, 0.53), V(0, -0.13, 0.76), V(0, -0.12, 0.98), V(0, -0.13, 1.16)]
    tail = [V(0, 0.24, 0.08), V(0, 0.48, 0.075), V(0, 0.72, 0.07), V(0, 0.96, 0.06), V(0, 1.18, 0.045), V(0, 1.38, 0.025)]
    hj = neck[-1]
    hd = unit(V(0, -1, -0.12))""",
        """    neck = [V(0, 0.10, 0.30), V(0, 0.15, 0.56), V(0, 0.08, 0.82), V(0, -0.06, 1.03), V(0, -0.20, 1.17)]     # an S: back, then forward
    tail = [V(0.10, 0.23, 0.085), V(0.20, 0.45, 0.08), V(0.14, 0.69, 0.072), V(-0.05, 0.86, 0.062), V(-0.19, 1.05, 0.046), V(-0.14, 1.29, 0.026)]
    hj = neck[-1]
    hd = unit(V(0, -1, -0.22))""")
s = rep(s, "    rv = [0.012, 0.03, 0.045, 0.056, 0.064, 0.07, 0.074, 0.072, 0.068, 0.064, 0.058, 0.052, 0.046]",
        "    rv = [0.012, 0.034, 0.052, 0.066, 0.076, 0.084, 0.09, 0.09, 0.084, 0.074, 0.064, 0.056, 0.05]")
s = rep(s, "    R.idle(dur=3.6, sway=4.0, tail=5.0, look=12.0, extra=lambda p, u: fold(p, 36 + 6 * osc(u, 2)))",
        "    R.idle(dur=3.6, sway=5.0, tail=6.0, look=14.0, extra=lambda p, u: fold(p, 30 + 8 * osc(u, 2)))")
ast.parse(s)
open(p + '.new', 'w', encoding='utf8').write(s); os.replace(p + '.new', p)
print('ok')
