H = 'Y:/tools/blender/monsters/'
def rep(s, a, b):
    assert a in s, a[:90]
    return s.replace(a, b, 1)
p = H + 'specs_s4_b.py'
s = open(p, encoding='utf-8').read()
s = rep(s, "pose={'upperarm_l': dict(rot=[('y', 45), ('z', -80)]), 'lowerarm_l': dict(rot=[('z', -25)])}),",
        "pose={'upperarm_l': dict(rot=[('y', 36), ('z', -92)]), 'lowerarm_l': dict(rot=[('z', -10)])}),")
open(p, 'w', encoding='utf-8', newline='\n').write(s)
p = H + 'asm_parts10.py'
s = open(p, encoding='utf-8').read()
s = rep(s, "def prow_plough(A, look='oak', rim='rust', rope='rope', y_s=-0.66, z0=0.12, z1=1.36,", "def prow_plough(A, look='oak', rim='rust', rope='rope', y_s=-0.60, z0=0.12, z1=1.36,")
s = rep(s, "        bd.v[:, 0] = np.where(x < 0, x + 0.34 * tt, x - 0.12 * tt)             # top edge falls to the rear, rear lower corner cut",
        "        bd.v[:, 0] = np.where(x < 0, x + 0.34 * tt, x - 0.24 * tt)             # top edge falls to the rear, rear lower corner cut (clears the coat's hem)")
s = rep(s, "        edge.v[:, 0] -= 0.12 * et", "        edge.v[:, 0] -= 0.24 * et")
import ast; ast.parse(s)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('r6b ok')
