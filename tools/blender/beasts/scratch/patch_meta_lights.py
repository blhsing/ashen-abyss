import os, ast
HERE = r"Y:\tools\blender\beasts"
p = os.path.join(HERE, 'bmeta.py')
s = open(p, encoding='utf8').read()
a = "res = {}\nfor d in dirs:"
assert a in s
s = s.replace(a, '''# soft lights the game hangs on glowing creatures (at most 8 live at once in the game): used when a creature's own meta
# has no 'light' entry. Heights / ranges in metres, model space.
LIGHTS = {
    'mon_cf_flicker': dict(color=[0.45, 0.75, 1.0], energy=0.7, range=2.6, height=0.45),
    'mon_lamp_lure': dict(color=[1.0, 0.62, 0.22], energy=1.0, range=4.0, height=1.75),
    'mon_dusk_gadfly': dict(color=[0.72, 0.9, 0.2], energy=0.6, range=2.4, height=1.25),
    'mon_fiend_wing': dict(color=[1.0, 0.4, 0.08], energy=0.7, range=3.2, height=1.7),
}
res = {}
for d in dirs:''')
a = "        e.setdefault('hover_in_model', False)\n"
assert a in s
s = s.replace(a, a + "        if model in LIGHTS:\n            e.setdefault('light', LIGHTS[model])\n")
ast.parse(s)
open(p + '.new', 'w', encoding='utf8').write(s); os.replace(p + '.new', p)
p = os.path.join(HERE, 'spec_d2.py')
s = open(p, encoding='utf8').read()
a = "emit=(1.0, 0.30, 0.04), strength=1.0, gain=0.6,"
assert a in s
s = s.replace(a, "emit=(0.95, 0.26, 0.03), strength=1.0, gain=0.42,")
ast.parse(s)
open(p + '.new', 'w', encoding='utf8').write(s); os.replace(p + '.new', p)
print('ok')
