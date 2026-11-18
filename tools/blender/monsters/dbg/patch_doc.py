p = 'Y:/tools/blender/monsters/asm.py'
s = open(p, encoding='utf-8').read()
a = """side;  mirror = add both sides;  final = already in final space (skips the sculpt in finish());  keep = a carried
        object on a scaled bone keeps its own shape and size, only its position follows the bone's scale.\"\"\""""
b = """side;  mirror = add both sides;  final = already in final space (skips the sculpt in finish());  keep = a carried
        object on a scaled bone keeps its own shape and size, only its position follows the bone's scale: keep=True
        moves the piece with its own centre, keep=(x, y, z) with that anchor -- give EVERY piece of one assembly the
        same anchor (on the face that touches the body), otherwise the assembly comes apart on bulky bodies.\"\"\""""
assert a in s
s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('doc ok')
