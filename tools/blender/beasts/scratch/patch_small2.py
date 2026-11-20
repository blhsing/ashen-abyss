import os, ast
p = r"Y:\tools\blender\beasts\h1\spec_h1.py"
s = open(p, encoding='utf8').read()
a = s.index("def sexton_digger")
b = s.index("C.look('limb', 'chitin'", a)
seg = s[a:b]
old = "dust_color=(0.13, 0.10, 0.07), edge=0.5, ridge=40.0, ridge_h=1.4)"
assert old in seg
seg = seg.replace(old, "dust_color=(0.28, 0.23, 0.17), edge=0.5, ridge=40.0, ridge_h=1.4)")
s = s[:a] + seg + s[b:]
ast.parse(s)
open(p + '.new', 'w', encoding='utf8').write(s); os.replace(p + '.new', p)
print('ok')
