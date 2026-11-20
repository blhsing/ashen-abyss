"""one-off patch (2026-11-21): hold the helpers' flyers / floaters to the lead's review bar.
- bparts: look kind `tipglow` (any kind + light growing with the distance from a centre)
- crow: slate-black feathers with a blue sheen instead of pure black, the singed primaries smoulder at their tips
- locust: faint violet static towards the tips of the brass leaves
- light entries in the meta (the game hangs a soft light on the creature): flicker, lure, gadfly, fiend wing"""
import os, ast, json
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


s = load('bparts.py')
if 'def k_tipglow' not in s:
    s = rep(s, "L.KINDS.update(cglass=k_cglass)", '''def k_tipglow(N, p):
    """any look (`base` = its kind) that smoulders towards its far end: emission grows with the distance from `center`
    (object space) between r0 and r1, broken up by noise - singed feather tips, static on wing edges, hot blade ends"""
    r = L.KINDS[p['base']](N, p)
    e = _c(p, 'emit', (1.0, 0.4, 0.06))
    vm = N.new('ShaderNodeVectorMath', operation='DISTANCE')
    N.set(vm.inputs[0], N.coord())
    N.set(vm.inputs[1], tuple(float(x) for x in p['center']))
    m = N.mapr(vm.outputs['Value'], p['r0'], p['r1'])
    m = N.math('MULTIPLY', m, N.mapr(N.noise(_c(p, 'glow_scale', 26.0), 3, 0.65, vec=N.mapping(N.coord(), loc=(9.1, 3.3, 6.7))), 0.42, 0.72))
    m = N.math('MULTIPLY', m, _c(p, 'gain', 1.0))
    r['color'] = N.mix(N.math('MULTIPLY', m, 0.7), r['color'], tuple(x * 0.6 for x in e))
    r['emit'] = (e, _c(p, 'strength', 1.2))
    r['emask'] = m
    return r


L.KINDS.update(cglass=k_cglass, tipglow=k_tipglow)
L.GRIME.update(tipglow=((0.02, 0.016, 0.012), 0.5))''')
    save('bparts.py', s)

s = load(r'h3\spec_h3.py')
s = rep(s, "    C.look('plume', 'bfur', 'hair_feather', color=(0.034, 0.034, 0.04), tip=(0.10, 0.10, 0.125), flow=(0, 1, -0.15), ash=0.4, scale=70.0, lock=34.0)",
        "    C.look('plume', 'bfur', 'hair_feather', color=(0.06, 0.06, 0.075), tip=(0.20, 0.20, 0.25), flow=(0, 1, -0.15), ash=0.5, scale=70.0, lock=34.0)")
s = rep(s, "    C.look('quill', 'feather', 'hair_feather', axis=(0, 1, -0.05), singe=0.55, sheen=(0.045, 0.052, 0.075))",
        "    C.look('quill', 'feather', 'hair_feather', axis=(0, 1, -0.05), singe=0.6, color=(0.05, 0.05, 0.064), sheen=(0.12, 0.14, 0.21),\n"
        "           ash=(0.34, 0.30, 0.26))")
s = rep(s, "    C.look('prim_l', 'feather', 'hair_feather', axis=(1, 0.75, 0), singe=0.8)\n    C.look('prim_r', 'feather', 'hair_feather', axis=(-1, 0.75, 0), singe=0.8)",
        "    # the singed primaries still smoulder at their tips (the lead's review: one bright accent, readable on dark ground)\n"
        "    for s_, sg_ in (('l', 1.0), ('r', -1.0)):\n"
        "        C.look('prim_' + s_, 'tipglow', 'hair_feather', base='feather', axis=(sg_, 0.75, 0), singe=0.85, color=(0.05, 0.05, 0.064),\n"
        "               sheen=(0.12, 0.14, 0.21), ash=(0.36, 0.31, 0.26), center=(0.0, 0.0, h), r0=0.62, r1=1.05, emit=(1.0, 0.36, 0.05),\n"
        "               strength=1.2, gain=0.9)")
s = rep(s, "    C.look('ember', 'emis', 'emis_ember', emit=(1.0, 0.40, 0.07), strength=1.5, mottle=0.6, base_mul=0.55, scale=60.0, crust=0.5)",
        "    C.look('ember', 'emis', 'emis_ember', emit=(1.0, 0.40, 0.07), strength=1.6, mottle=0.5, base_mul=0.55, scale=60.0, crust=0.3)")
s = rep(s, "    C.look('brass', 'metal', 'metal_brass', **dict(L.PRESET['brass'], color=(0.62, 0.40, 0.10), edge_color=(0.90, 0.72, 0.34), hammer=30.0, amount=0.2, rough=0.36))",
        "    # faint violet static towards the tips of the brass leaves (the brief's storm accent)\n"
        "    C.look('brass', 'tipglow', 'metal_brass', base='metal', center=(0.0, 0.0, h), r0=0.42, r1=0.8, emit=(0.55, 0.35, 1.0), strength=1.0,\n"
        "           gain=0.55, glow_scale=40.0,\n"
        "           **dict(L.PRESET['brass'], color=(0.62, 0.40, 0.10), edge_color=(0.90, 0.72, 0.34), hammer=30.0, amount=0.2, rough=0.36))")
save(r'h3\spec_h3.py', s)
