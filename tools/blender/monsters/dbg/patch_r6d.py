"""round 6d: the colossus' crease glow was a clean neon hoop round every cast -> three small ember specks per cast, half
sunk in the drift.  Applied once; do not re-run."""
p = 'Y:/tools/blender/monsters/asm_parts10.py'
s = open(p, encoding='utf-8').read()
a = """    if crease:                                             # the crease where the cast leaves the mass: the drift hides part of the ring
        c = torus(0.168, 0.024, n=12, m=4, ry=0.132).move(0.0, 0.02, 1.09)
        A.add(xf(c), bone=bone, look=crease, smooth=80, keep=K)
    return K"""
b = """    if crease:                                             # sparks in the crease where the cast leaves the mass: a few specks, never a ring
        rng = np.random.default_rng(int(abs(yaw) * 7 + abs(pitch) * 3 + sc * 100))
        sp = []
        for a_ in rng.uniform(0, 2 * math.pi, 3):
            sp.append(KX.lump(0.03, 0.022, 0.014, seed=int(a_ * 10), amp=0.3, n=5, m=3).rot('z', math.degrees(a_) + 90)
                      .move(0.155 * math.cos(a_), 0.02 + 0.12 * math.sin(a_), 1.05 + rng.uniform(0.0, 0.07)))
        A.add(xf(join(sp)), bone=bone, look=crease, smooth=80, keep=K)
    return K"""
assert a in s
s = s.replace(a, b)
import ast; ast.parse(s)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('r6d ok')
