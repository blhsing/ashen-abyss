p = r'Y:\tools\blender\monsters\asm_parts9.py'
s = open(p, encoding='utf-8').read()


def rep(a, b, count=1):
    global s
    assert a in s, a[:80]
    s = s.replace(a, b, count)


# helper: rectangular bar
rep('''def rivet_ring(A, c, nrm, r, n, bone, look, rr=0.011, keep=False):''',
    '''def bar(pts, half_up, half_side, up=(0, 0, 1)):
    """rectangular bar along a path: half extents along the `up` hint and across it"""
    return tube(np.asarray(pts, float), half_up * 1.4142, n=4, r2=half_side * 1.4142, up=up, phase=math.pi / 4)


def rivet_ring(A, c, nrm, r, n, bone, look, rr=0.011, keep=False):''')

# blanket: no hole at the crown
rep("            r = max(ed[j] * (i / nr) ** 0.9, 0.012)", "            r = max(ed[j] * (i / nr) ** 0.9, 0.004)")

# gallows: collar straddles the trunk surface; skull decimation
rep("    A.add(col.tf(M.rot_to((0, 0, 1), d)).move(sh - d * 0.06), bone='spine_03', look=look, smooth=35)               # the bough's collar on the trunk",
    "    A.add(col.tf(M.rot_to((0, 0, 1), d)).move(sh + d * 0.045), bone='spine_03', look=look, smooth=35)              # the bough's collar on the trunk")

# sun wedge: convex outline, proper bar extents
rep("    outl = [(hub * math.sin(a), -hub * math.cos(a)) for a in aa[::5]] + [(L_ * math.sin(a), -L_ * math.cos(a)) for a in aa[::-1]]",
    "    outl = [(hub * math.sin(a), -hub * math.cos(a)) for a in (aa[0], aa[-1])] + [(L_ * math.sin(a), -L_ * math.cos(a)) for a in aa[::-1]]")
rep("        ps.append((tube(pts, tk, n=6, r2=th * 0.5 + (0.012 if rr == L_ else 0.006), up=(0, 1, 0)), 'brass'))",
    "        ps.append((tube(pts, th * 0.5 + (0.014 if rr == L_ else 0.007), n=6, r2=tk, up=(0, 1, 0)), 'brass'))")
rep("        ps.append((tube(pts, 0.014, n=4, r2=th * 0.5 + 0.004, up=(0, 1, 0), phase=math.pi / 4), 'brass_dark'))",
    "        ps.append((bar(pts, th * 0.5 + 0.004, 0.012, up=(0, 1, 0)), 'brass_dark'))")
rep("            ps.append((tube(pts, [0.016, 0.004], n=4, r2=0.008, up=(0, 1, 0), phase=math.pi / 4), 'brass'))",
    "            ps.append((tube(pts, [0.012, 0.004], n=4, r2=0.008, up=(0, 1, 0), phase=math.pi / 4), 'brass'))")

# wedge placement: stands on its rim behind the back, leaning back at the top
rep('''    K = V(0, 0.30, 1.30)
    bone = 'spine_03'
    lean = 9.0
    apex = V(0, 0.30, 1.16 + fin)
    for q, lk in sun_wedge(L_=fin, half=20.0):
        q.rot('x', -lean).move(apex)
        A.add(q, bone=bone, look={'brass_bright': bright, 'brass': look, 'brass_dark': dark}[lk], smooth=30, keep=K)
    top = apex + V(0, 0.075, -fin * 0.34)''',
    '''    K = V(0, 0.30, 1.30)
    bone = 'spine_03'
    lean = 9.0
    B = V(0, 0.335, 1.085)                                          # where the middle of the rim stands
    for q, lk in sun_wedge(L_=fin, half=20.0):
        q.move(0, 0, fin).rot('x', -lean).move(B)
        A.add(q, bone=bone, look={'brass_bright': bright, 'brass': look, 'brass_dark': dark}[lk], smooth=30, keep=K)
    top = B + V(0, fin * 0.66 * math.sin(rad(lean)) + 0.07, fin * 0.66 * math.cos(rad(lean)))''')
rep("    A.add(box(0.72, 0.06, 0.045, bevel=0.01).move(0, 0.385, 1.045), bone=bone, look=dark, smooth=30, keep=K)                              # the ledge the rim stands on",
    "    A.add(box(0.78, 0.10, 0.045, bevel=0.01).move(0, 0.335, 1.045), bone=bone, look=dark, smooth=30, keep=K)                              # the ledge the rim stands on")
rep("        A.add(tube([foot, foot + V(0, 0.17, 0.04)], 0.02, n=6), bone=bone, look=dark, smooth=40, keep=K)                                   # ledge arm under the rim",
    "        A.add(tube([foot, foot + V(0, 0.15, 0.03)], 0.02, n=6), bone=bone, look=dark, smooth=40, keep=K)                                   # ledge arm under the rim")

# colony: keep the bodies closed (cap the shoulder stumps with fungus), cap centre, wart heights
rep('''        b = b.sub(~b.vmask_faces(b.wsum(arm_bones()) > 0.5, 'all'))
        b = M.decimate(b, 0.2)''',
    '''        b = b.sub(~b.vmask_faces(b.wsum(arm_bones()) > 0.5, 'all'))
        b = M.decimate(b, 0.2)
        caps = join([KX.lump(0.085, 0.08, 0.075, seed=11 + 2 * k + (sxx > 0), amp=0.3, freq=8.0).move(sxx * 0.205, 0.012, 1.43) for sxx in (-1, 1)])   # fungus over the arm stumps''')
rep('''        b.scale(0.95, pivot=(0, 0, 1.0))
        b.rot('z', yaw).move(C0 + V(0.17 * math.sin(rad(yaw)), -0.17 * math.cos(rad(yaw)), 0.0))
        b.v[:, 2] += 0.045 - b.v[:, 2].min()''',
    '''        dz = None
        for q in (b, caps):
            q.scale(0.95, pivot=(0, 0, 1.0))
            q.rot('z', yaw).move(C0 + V(0.17 * math.sin(rad(yaw)), -0.17 * math.cos(rad(yaw)), 0.0))
            dz = 0.045 - q.v[:, 2].min() if dz is None else dz
            q.v[:, 2] += dz
        A.add(caps, bone='spine_03', look=stalk, smooth=180)''')
rep('''    capm.v[-1, 2] = prof[-1][1] if abs(capm.v[-1, 0]) < 1e-6 and abs(capm.v[-1, 1]) < 1e-6 else capm.v[-1, 2]
''', '''    PR = [p_[0] for p_ in prof[1:]][::-1]; PZ = [p_[1] for p_ in prof[1:]][::-1]         # the cap's top profile, radius ascending
''')
rep("        zz = float(np.interp(r_, [p_[0] for p_ in prof[::-1]], [p_[1] for p_ in prof[::-1]]))", "        zz = float(np.interp(r_, PR, PZ))")
rep("        zz = float(np.interp(R_ * r_, [p_[0] for p_ in prof[::-1]], [p_[1] for p_ in prof[::-1]]))", "        zz = float(np.interp(R_ * r_, PR, PZ))")

# colossus: sparks share the figure's anchor
rep('''    A.add(b, bone=bone, look=look, smooth=180, keep=b.center() if K is None else K)
    return b''', '''    K = b.center() if K is None else K
    A.add(b, bone=bone, look=look, smooth=180, keep=K)
    return K''')
rep('''        emerge(A, bone, at, yaw, pitch, sc, look if k % 3 else pale, ratio=ratio, arms=float(arms))
        c = V(at) + V(0.10 * math.sin(rad(yaw)), -0.10 * math.cos(rad(yaw)), 0.13 * sc)          # sparks in the crease below each figure
        if k % 2 == 0:
            A.add(ellipsoid(0.03, 0.03, 0.022, n=6, m=4).move(c), bone=bone, look=spark, smooth=80, keep=True)''',
    '''        Kf = emerge(A, bone, at, yaw, pitch, sc, look if k % 3 else pale, ratio=ratio, arms=float(arms))
        c = V(at) + V(0.10 * math.sin(rad(yaw)), -0.10 * math.cos(rad(yaw)), 0.13 * sc)          # sparks in the crease below each figure
        if k % 2 == 0:
            A.add(ellipsoid(0.03, 0.03, 0.022, n=6, m=4).move(c), bone=bone, look=spark, smooth=80, keep=Kf)''')
rep('''        if sx < 0:
            b.flip() if False else None
        b.recalc()''', '''        b.recalc()''')
open(p, 'w', encoding='utf-8', newline='\n').write(s)
p2 = r'Y:\tools\blender\monsters\asm_parts.py'
t = open(p2, encoding='utf-8').read()
if "'asm_parts9'" not in t:
    t = t.replace("'asm_parts7', 'asm_parts8']", "'asm_parts7', 'asm_parts8', 'asm_parts9']")
    open(p2, 'w', encoding='utf-8', newline='\n').write(t)
print("ok")
