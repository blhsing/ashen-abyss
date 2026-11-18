p = r'Y:\tools\blender\monsters\asm_parts7.py'
s = open(p, encoding='utf-8').read()
i0 = s.index("@part\ndef hammer_arm(")
i1 = s.index("# ================================================================================================ stone pillar bearer")
new = '''@part
def hammer_arm(A, side='r', look='iron', glow='ember', block=0.30, beam=0.62, face='steel'):
    """the arm and shoulder are an iron drop-hammer assembly: a riveted shoulder cowl (half drum on a flanged plate), an
    armoured upper arm, a crank drum at the elbow, an I-beam with a cranked step and a square block head the size of an
    anvil with a worn striking plate on its palm side (dull heat glow in its grooves).  Use with body hide of that
    forearm + hand.  A ballast drum hangs on a bracket behind the other shoulder, chained to the cowl across the back."""
    def rivet_ring(c, nrm, r, n, bone, rr=0.011):
        fr = M.frame_from(nrm)
        for k in range(n):
            a = 2 * math.pi * (k + 0.5) / n
            A.add(KC3.rivet(V(c) + (fr[:, 0] * math.cos(a) + fr[:, 2] * math.sin(a)) * r, nrm, r=rr), bone=bone, look=look)

    for sx in A.sides(side):
        s = '_l' if sx > 0 else '_r'
        o = '_r' if sx > 0 else '_l'
        sh = A.R.H('upperarm' + s); el = A.R.H('lowerarm' + s)
        # shoulder cowl: half drum lying front-to-back over the shoulder, on a flanged back plate
        c0 = sh + V(sx * 0.03, 0.0, 0.035)
        A.add(cyl(c0 + V(0, -0.15, 0), c0 + V(0, 0.15, 0), 0.15, n=14), bone='clavicle' + s, look=look, smooth=40)
        for yy in (-0.155, 0.155):
            fl = revolve([(0.0, 0.0), (0.165, 0.0), (0.165, 0.02), (0.0, 0.02)], n=14).rot('x', -90 if yy > 0 else 90)
            A.add(fl.move(c0 + V(0, yy, 0)), bone='clavicle' + s, look=look, smooth=30)
            rivet_ring(c0 + V(0, yy + (0.021 if yy > 0 else -0.021), 0), (0, 1 if yy > 0 else -1, 0), 0.125, 8, 'clavicle' + s)
            bolt = revolve([(0.0, 0.0), (0.04, 0.0), (0.04, 0.03), (0.022, 0.045), (0.0, 0.045)], n=6).rot('x', -90 if yy > 0 else 90)
            A.add(bolt.move(c0 + V(0, yy, 0)), bone='clavicle' + s, look=look, smooth=30)                 # pivot bolt heads
        A.add(box(0.07, 0.30, 0.22, bevel=0.012).move(sh + V(-sx * 0.085, 0, 0.0)), bone='clavicle' + s, look=look, smooth=30)
        A.add(KC3.limb_plate('upperarm' + s, [(0.12, 0.105), (0.55, 0.098), (1.0, 0.088)], n=10, lip=0.008, power=3.0), bone='upperarm' + s, look=look, smooth=38)
        for t in (0.3, 0.72):
            A.add(KI.limb_ring('upperarm' + s, t, 0.103 - 0.012 * t, thick=0.012, n=12, m=4), bone='upperarm' + s, look=look)
        # crank drum at the elbow
        A.add(cyl(el + V(0, -0.115, 0), el + V(0, 0.115, 0), 0.105, n=14), bone='lowerarm' + s, look=look, smooth=40)
        for yy in (-1, 1):
            rivet_ring(el + V(0, yy * 0.117, 0), (0, yy, 0), 0.075, 6, 'lowerarm' + s, rr=0.012)
            A.add(cyl(el + V(0, yy * 0.11, 0), el + V(0, yy * 0.145, 0), 0.035, n=8), bone='lowerarm' + s, look=look, smooth=30)
        # I-beam with a cranked step half way: web + two flanges per segment
        L1 = beam * 0.46; L2 = beam - L1; st = 0.07
        for (x0, ln, dz) in ((0.02, L1, 0.0), (L1 - 0.03, L2 + 0.05, -st)):
            c = el + V(sx * (x0 + ln / 2), 0, dz)
            A.add(box(ln, 0.036, 0.15, bevel=0.006).move(c), bone='lowerarm' + s, look=look, smooth=30)
            for fz in (-0.075, 0.075):
                A.add(box(ln, 0.12, 0.022, bevel=0.005).move(c + V(0, 0, fz)), bone='lowerarm' + s, look=look, smooth=30)
        A.add(box(0.07, 0.13, 0.24, bevel=0.008).move(el + V(sx * (L1 - 0.005), 0, -st / 2)), bone='lowerarm' + s, look=look, smooth=30)        # the crank web
        for dz in (0.0, -st):
            A.add(cyl(el + V(sx * (L1 - 0.005), -0.075, dz), el + V(sx * (L1 - 0.005), 0.075, dz), 0.022, n=7), bone='lowerarm' + s, look=look, smooth=30)
        # the block head
        hc = el + V(sx * (beam + block * 0.42), 0, -st - block * 0.18)
        A.add(box(block, block * 0.92, block * 1.08, bevel=0.03).move(hc), bone='lowerarm' + s, look=look, smooth=30)
        A.add(box(block * 0.34, block * 1.0, block * 0.5, bevel=0.012).move(hc + V(-sx * block * 0.42, 0, block * 0.2)), bone='lowerarm' + s, look=look, smooth=30)      # socket collar
        for yy in (-1, 1):
            for (dx, dz) in ((-0.3, 0.33), (0.3, 0.33), (-0.3, -0.3), (0.3, -0.3)):
                A.add(KC3.rivet(hc + V(dx * block, yy * block * 0.462, dz * block), (0, yy, 0), r=0.016, n=6), bone='lowerarm' + s, look=look)
        A.add(torus(0.045, 0.014, n=10, m=5).rot('x', 90).move(hc + V(0, 0, block * 0.54 + 0.03)), bone='lowerarm' + s, look=look, smooth=40)       # lifting eye
        fz = hc[2] - block * 0.54
        A.add(box(block * 0.86, block * 0.80, 0.035, bevel=0.01).move(hc[0], hc[1], fz - 0.012), bone='lowerarm' + s, look=face, smooth=30)        # striking plate
        if glow:                                                             # heat in the plate's grooves (a cross and a ring)
            A.add(box(block * 0.70, 0.022, 0.012).move(hc[0], hc[1], fz - 0.031), bone='lowerarm' + s, look=glow)
            A.add(box(0.022, block * 0.64, 0.012).move(hc[0], hc[1], fz - 0.031), bone='lowerarm' + s, look=glow)
            A.add(torus(block * 0.25, 0.011, n=14, m=4).move(hc[0], hc[1], fz - 0.031), bone='lowerarm' + s, look=glow)
        # ballast drum on a bracket behind the other shoulder
        so = A.R.H('upperarm' + o)
        dc = so + V(sx * 0.03, 0.215, 0.03)
        A.add(cyl(dc + V(-0.14, 0, 0), dc + V(0.14, 0, 0), 0.118, n=14), bone='clavicle' + o, look=look, smooth=40)
        for xx in (-0.10, 0.0, 0.10):
            A.add(torus(0.123, 0.011, n=14, m=4).rot('y', 90).move(dc + V(xx, 0, 0)), bone='clavicle' + o, look=look)
        for xx in (-0.145, 0.145):
            rivet_ring(dc + V(xx, 0, 0), (1 if xx > 0 else -1, 0, 0), 0.08, 6, 'clavicle' + o, rr=0.011)
        for xx in (-0.09, 0.09):
            A.add(cyl(dc + V(xx, -0.02, 0.10), so + V(sx * 0.03 + xx * 0.6, 0.02, 0.085), 0.016, n=5), bone='clavicle' + o, look=look, smooth=30)
            A.add(cyl(dc + V(xx, -0.06, -0.08), so + V(sx * 0.03 + xx * 0.6, 0.10, -0.10), 0.014, n=5), bone='clavicle' + o, look=look, smooth=30)
        A.add(band((0, 0.01, 1.33), 0.27, 0.2, 0.05, 0.01, tilt_y=sx * 22, n=24), bone='spine_03', look=look)
        ch = KB2.drape_path(dc + V(sx * 0.10, 0.02, 0.11), c0 + V(-sx * 0.02, 0.16, 0.10), sag=0.07, n=9)
        A.add(M.chain(ch, link=0.05, r=0.008), bone='spine_03', look=look, smooth=60)


'''
s = s[:i0] + new + s[i1:]
a = """    A.add(band((0, 0.02, 1.36), 0.24, 0.19, 0.03, 0.008, tilt_y=14, n=22), bone=bone, look='strap')"""
b = """    on = 'robe' if 'robe' in A.refs else 'body'
    for zz, tl in ((1.40, 9.0), (1.28, -9.0)):             # two crossing straps seated on the coat (were one loose hoop)
        cx, cy, rx, ry = girth_of(A, on, zz)
        A.add(band((0, cy, zz), rx + 0.007, ry + 0.007, 0.03, 0.008, tilt_y=tl, n=22), bone=bone, look='strap')"""
assert a in s
s = s.replace(a, b)
a = """    p.v[end, 2] += 0.09 * np.sin(5 * np.arctan2(p.v[end, 1], p.v[end, 0])) + 0.05 * np.sin(13 * np.arctan2(p.v[end, 1], p.v[end, 0]))     # the break"""
b = """    ang = np.arctan2(p.v[end, 1], p.v[end, 0])
    p.v[end, 2] += 0.035 * np.sin(3 * ang + 0.7) + 0.022 * np.sin(7 * ang) - 0.02                                               # the break: rough, not spiky"""
assert a in s
s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print("patched parts7")
