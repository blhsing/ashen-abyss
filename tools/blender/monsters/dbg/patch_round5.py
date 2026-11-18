p = 'Y:/tools/blender/monsters/asm_parts9.py'
s = open(p, encoding='utf-8').read()
a = """        b.recalc()
        KC.skin_cloth(b, body=A.rest, smooth=3, limit=['spine_03', 'clavicle' + s, 'upperarm' + s, 'lowerarm' + s, 'hand' + s])
        A.add(b, look=look, smooth=180)
        for t, rr in ((0.25, 0.07), (0.8, 0.06)):                   # wax runs hanging from the arm"""
b = """        b.recalc()
        # weights by the position ALONG the arm (a nearest-surface transfer gives the wide figure torso weights: the arm
        # then stays spread like a wing while the bones hang)
        along = (b.v - sh) @ ax
        el = float((R.H('lowerarm' + s) - sh) @ ax); wr = float((R.H('hand' + s) - sh) @ ax)
        M.weights_by(b, along, [(-0.11, {'clavicle' + s: 1.0}), (0.02, {'upperarm' + s: 1.0}), (el - 0.05, {'upperarm' + s: 1.0}), (el + 0.05, {'lowerarm' + s: 1.0}),
                                (wr - 0.03, {'lowerarm' + s: 1.0}), (wr + 0.05, {'hand' + s: 1.0})])
        A.add(b, look=look, smooth=180)
        for t, rr in ((0.25, 0.07), (0.8, 0.06)):                   # wax runs hanging from the arm"""
assert a in s
s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
