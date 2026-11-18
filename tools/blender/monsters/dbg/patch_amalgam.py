p = 'Y:/tools/blender/monsters/asm_parts6.py'
s = open(p, encoding='utf-8').read()
a = """    A.add(band((0, 0.0, 1.30), 0.235, 0.185, 0.03, 0.008, tilt_y=10, n=24), bone='spine_03', look='rope')
    A.add(band((0, 0.0, 1.16), 0.215, 0.165, 0.03, 0.008, tilt_y=-8, n=24), bone='spine_02', look='rope')"""
b = """    # grave cord lashing the two ribcages together: tight rope loops (they were wide flat hoops standing off the ribs)
    for (zz, rx_, ry_, tl, bn) in ((1.31, 0.192, 0.176, 9.0, 'spine_03'), (1.285, 0.19, 0.174, -7.0, 'spine_03'), (1.15, 0.176, 0.158, -8.0, 'spine_02'),
                                   (1.125, 0.172, 0.155, 6.0, 'spine_02')):
        A.add(torus(rx_, 0.011, n=24, m=5, ry=ry_).rot('y', tl).move(0, 0.012, zz), bone=bn, look='rope', smooth=60)"""
assert a in s
s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
