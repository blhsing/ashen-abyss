"""round 6e: tallow-mound arm-bodies -- wider weight blends at elbow and wrist (the splayed body is off the bone line, a
narrow blend sheared it).  Applied once; do not re-run."""
p = 'Y:/tools/blender/monsters/asm_parts9.py'
s = open(p, encoding='utf-8').read()
a = """        M.weights_by(b, along, [(-0.11, {'clavicle' + s: 1.0}), (0.02, {'upperarm' + s: 1.0}), (el - 0.05, {'upperarm' + s: 1.0}), (el + 0.05, {'lowerarm' + s: 1.0}),
                                (wr - 0.03, {'lowerarm' + s: 1.0}), (wr + 0.05, {'hand' + s: 1.0})])"""
b = """        M.weights_by(b, along, [(-0.11, {'clavicle' + s: 1.0}), (0.02, {'upperarm' + s: 1.0}), (el - 0.12, {'upperarm' + s: 1.0}), (el + 0.12, {'lowerarm' + s: 1.0}),
                                (wr - 0.09, {'lowerarm' + s: 1.0}), (wr + 0.08, {'hand' + s: 1.0})])"""
assert a in s
s = s.replace(a, b)
import ast; ast.parse(s)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('r6e ok')
