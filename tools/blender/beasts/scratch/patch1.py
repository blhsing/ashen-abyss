p = r"Y:\tools\blender\beasts\rig_b.py"
s = open(p, encoding='utf8').read()
old = "fleg=(0.78, 0.58, 0.23, 0.055)"
assert old in s
s = s.replace(old, "fleg=(0.80, 0.56, 0.22, 0.055)")
old = "    J['f'] = [V(wf, y - 0.02, fleg[0] * hs), V(wf, y + 0.075 + sprawl * 0.0, fleg[1] * hs), V(wf * 0.95, y + 0.035, fleg[2] * hs),"
assert old in s
s = s.replace(old, "    J['f'] = [V(wf, y - 0.035, fleg[0] * hs), V(wf, y + 0.085, fleg[1] * hs), V(wf * 0.95, y + 0.02, fleg[2] * hs),")
old = "                lg.par = 'chest' if pre == 'f' else 'hip'\n"
assert old in s
s = s.replace(old, old + "                lg.glide = 0.07 if pre == 'f' else 0.04\n")
old = """            for k, lg in self.legs.items():
                Pg, sw, s = gait_foot(lg, u, S, duty, lift if (hlift is None or k[0] == 'f') else hlift)
                p.leg(lg, Pg, R3=rx(tuck * sw), R4=rx(tuck * 0.6 * sw))"""
assert old in s
s = s.replace(old, """            for k, lg in self.legs.items():
                Pg, sw, s = gait_foot(lg, u, S, duty, lift if (hlift is None or k[0] == 'f') else hlift)
                ln = pastern * 2.0 * (Pg[1] - lg.home[1]) / max(S, 1e-6) * (1 - sw)
                p.leg(lg, Pg, R3=rx(ln + tuck * sw), R4=rx(tuck * 0.6 * sw))""")
old = "ears=0.0, jaw=0.0):\n        S = v * duty * T"
assert old in s
s = s.replace(old, "ears=0.0, jaw=0.0, pastern=28.0):\n        S = v * duty * T")
s += '''

# ================================================================================================ flesh
LEGN = ('upper', 'lower', 'foot', 'toe')


def quad_flesh(C, J, S):
    """organic body of an upright quadruped from a shape table S:
      torso / neck = [(y, z centre, vertical radius, side radius), ...] from the rump to the breast / chest to head
      head = [closed Parts] (skull, snout ... already in place; they move with the head bone)
      fleg / hleg = [(fore-aft radius, side radius)] at [root inside the body, top joint, elbow|knee, wrist|hock, toe joint]
      fpaw / hpaw = (half width, half length, half height);  blobs = [(centre, (rx, ry, rz), [bones], mirror)]
      voxel, tris, sculpt(part)"""
    parts = []
    t = S['torso']
    torso = P.tubep([(0, a[0], a[1]) for a in t], [a[2] for a in t], [a[3] for a in t], m=S.get('torso_m', 16), n=16)
    B.wbones(torso, ['hip', 'spine1', 'spine2', 'chest'], sharp=3.0)
    parts.append(torso)
    t = S.get('neck')
    if t:
        neck = P.tubep([(0, a[0], a[1]) for a in t], [a[2] for a in t], [a[3] for a in t], m=9, n=12)
        B.wbones(neck, ['chest', 'neck1', 'neck2', 'head'], sharp=3.0)
        parts.append(neck)
    for hp in S.get('head', []):
        B.wbones(hp, ['head'])
        parts.append(hp)
    for pre, key, par in (('f', 'fleg', 'chest'), ('h', 'hleg', 'hip')):
        rad = S[key]
        pw = S[pre + 'paw']
        for s, sg in (('l', 1.0), ('r', -1.0)):
            pts = [J[pre][0] + V(0, 0, S.get(pre + '_up', 0.07))] + list(J[pre][:4])
            pts = [q * (sg, 1, 1) for q in pts]
            leg = P.tubep(pts, [r[0] for r in rad], [r[1] for r in rad], m=14, n=10, up=(0, -1, 0))
            bones = [par] + ['%s_%s_%s' % (pre, n, s) for n in LEGN]
            B.wbones(leg, bones, sharp=4.0)
            parts.append(leg)
            c = J[pre][3] * (sg, 1, 1) + V(0, -pw[1] * 0.5, 0)
            c[2] = pw[2] - 0.004
            paw = P.blob(c, pw[0], pw[1], pw[2], n=10, m=6)
            B.wbones(paw, bones[3:], bias={bones[4]: 2.5})
            parts.append(paw)
    for c, r, bones, mirror in S.get('blobs', []):
        for sg in ((1.0, -1.0) if mirror else (1.0,)):
            bl = P.blob(V(c) * (sg, 1, 1), r[0], r[1], r[2])
            bb = [(b[:-1] + ('l' if sg > 0 else 'r')) if (mirror and b.endswith('_l')) else b for b in bones]
            B.wbones(bl, bb, sharp=3.0)
            parts.append(bl)
    body = P.organic(parts, voxel=S.get('voxel', 0.011), tris=S.get('tris', 6500), sculpt=S.get('sculpt'), smooth=S.get('smooth', 3))
    return body


def add_tail(C, J, r0, r1, look, n=7, sink=0.05, rh=None):
    pts = [J['tail'][0] + unit(J['tail'][0] - J['tail'][1]) * sink] + list(J['tail'])
    k = len(pts)
    rr = [r0] + list(np.linspace(r0, r1, k - 1))
    tl = P.tubep(pts, rr, None if rh is None else [x * rh for x in rr], m=max(8, 3 * k), n=n)
    B.wbones(tl, ['hip'] + [b for b in C.rg.names if b.startswith('tail')], sharp=4.0)
    return C.skin(tl, look)


def add_claws(C, bvh, J, look, n=3, length=0.03, r=0.007, spread=0.016, drop=-0.3):
    for pre in ('f', 'h'):
        for s, sg in (('l', 1.0), ('r', -1.0)):
            tip = J[pre][4] * (sg, 1, 1)
            out = []
            for k in range(n):
                dx = spread * (k - (n - 1) / 2)
                base = tip + V(dx, 0.012, 0.004)
                out.append(P.seated(bvh, base, (dx * 6, -1.0, drop), length, r, sink=0.4, n=5, curl=(0, 0, -1), seg=2))
            C.rigid(M.join(out), '%s_toe_%s' % (pre, s), look, smooth=50)


def ear_sheet(base, tip, width, cup=0.02, th=0.005, rows=6, cols=5, sink=0.02, shape=0.7, face=(0, -1, 0)):
    """leaf-shaped ear from base to tip; `face` = the direction its hollow looks"""
    base = V(base); tip = V(tip)
    up = unit(tip - base)
    Ln = np.linalg.norm(tip - base)
    f = V(face) - up * float(V(face) @ up)
    f = unit(f)
    side = np.cross(up, f)
    G = np.zeros((rows + 1, cols, 3))
    for i in range(rows + 1):
        t = i / rows
        h = -sink + (Ln + sink) * t
        w = width * max(0.04, math.sin(math.pi * (0.12 + 0.88 * t)) ** shape) * (1.0 if t < 0.95 else 0.3)
        for j in range(cols):
            sx = -1 + 2 * j / (cols - 1)
            G[i, j] = base + up * h + side * (sx * w) + f * (cup * (sx * sx - 0.5) * math.sin(math.pi * min(1.0, t + 0.15)))
    return P.sheet_solid(G, th)
'''
open(p, 'w', encoding='utf8').write(s)
print('ok')
