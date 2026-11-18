import re


def load(p):
    return open(p, encoding='utf-8').read()


def save(p, s):
    open(p, 'w', encoding='utf-8', newline='\n').write(s)


def rep(s, a, b, n=1):
    assert a in s, a[:100]
    return s.replace(a, b, n)


H = 'Y:/tools/blender/monsters/'

# ------------------------------------------------------------------ wax dome: a real melted side, the wick stays on the highest point
p = H + 'asm_parts3.py'
s = load(p)
s = rep(s, "def wax_dome(A, look='tallow', R=0.185, z0=1.36, z1=1.90, bone='spine_03', wick=True, drips=9, seed=2, melted=None, melt_drop=0.42, melt_in=0.25):",
        "def wax_dome(A, look='tallow', R=0.185, z0=1.36, z1=1.90, bone='spine_03', wick=True, drips=9, seed=2, melted=None, melt_drop=0.42, melt_in=0.25,\n"
        "             melt_off=0.02, melt_w=1.0, melt_zw=0.5):")
s = rep(s, "        t = np.clip((sx * d.v[:, 0] + 0.02) / R, 0, 1) * np.clip((d.v[:, 2] - H * 0.15) / (H * 0.5), 0, 1)",
        "        t = np.clip((sx * d.v[:, 0] + melt_off) / (R * melt_w), 0, 1) * np.clip((d.v[:, 2] - H * 0.15) / (H * melt_zw), 0, 1)")
s = rep(s, """    if wick:
        A.add(tube([(0, 0, z1 - 0.01), (0.004, 0, z1 + 0.03), (0.012, 0.004, z1 + 0.055)], 0.0045, n=5), bone=bone, look='wick')
    A.refs['dome_top'] = V(0.012, 0.004, z1 + 0.055)""",
        """    tp = V(0, 0, z1)
    if melted:                                            # the wick stands on what is left: the highest point of the melted dome
        tp = d.v[int(np.argmax(d.v[:, 2]))].copy()
    if wick:
        A.add(tube([tp + V(0, 0, -0.012), tp + V(0.004, 0, 0.03), tp + V(0.012, 0.004, 0.055)], 0.0045, n=5), bone=bone, look='wick')
    A.refs['dome_top'] = tp + V(0.012, 0.004, 0.055)""")
save(p, s)

p = H + 'specs_s3_b.py'
s = load(p)
s = rep(s, "dict(p='wax_dome', melted='l', drips=14, melt_drop=0.66, melt_in=0.34)",
        "dict(p='wax_dome', melted='l', drips=14, melt_drop=0.68, melt_in=0.10, melt_off=0.065, melt_w=0.55, melt_zw=0.32)")
save(p, s)

# ------------------------------------------------------------------ emissive looks: dark base colour (the emission carries the colour)
p = H + 'asm_parts8.py'
s = load(p)
s = rep(s, "flame_lo=asm._p('emis', 'emis', emit=(1.0, 0.66, 0.24), strength=1.5, mottle=0.25, base_mul=0.9),", "flame_lo=asm._p('emis', 'emis', emit=(1.0, 0.62, 0.20), strength=1.5, mottle=0.25, base_mul=0.3),")
s = rep(s, "flame_mid=asm._p('emis', 'emis', emit=(1.0, 0.42, 0.09), strength=1.5, mottle=0.3, base_mul=0.9),", "flame_mid=asm._p('emis', 'emis', emit=(1.0, 0.40, 0.08), strength=1.5, mottle=0.3, base_mul=0.3),")
s = rep(s, "flame_tip=asm._p('emis', 'emis', emit=(0.78, 0.20, 0.03), strength=1.5, mottle=0.4, base_mul=0.8),", "flame_tip=asm._p('emis', 'emis', emit=(0.78, 0.20, 0.03), strength=1.5, mottle=0.4, base_mul=0.3),")
save(p, s)
p = H + 'asm_parts5.py'
s = load(p)
s = rep(s, "slag=asm._p('emis', 'emis', emit=(1.0, 0.36, 0.05), strength=2.0, base_mul=0.8, mottle=0.6, crust=1.0, scale=12.0),",
        "slag=asm._p('emis', 'emis', emit=(1.0, 0.36, 0.05), strength=2.0, base_mul=0.25, mottle=0.6, crust=1.0, scale=12.0),")
save(p, s)

# ------------------------------------------------------------------ loads sit ON the back: anchors at their front face, real shoulder straps
p = H + 'asm_parts6.py'
s = load(p)
s = rep(s, """@part
def brand_ring(""", '''def load_straps(A, look='strap', y_back=0.16, z_top=1.50, z_bot=1.08, x=0.125, w=0.024, chest=1.30):
    """harness of a back load: a strap over each shoulder from the load down the chest to the belt and one round the chest,
    all seated on the body and skinned like it (they follow the bulk of the bearer instead of floating as hoops)"""
    S = A.S('body')
    for sx in (1, -1):
        pts = [V(sx * x, y_back, z_top - 0.04), V(sx * x, 0.06, z_top + 0.06), V(sx * x * 1.05, -0.08, z_top - 0.02), V(sx * x * 1.1, -0.16, chest),
               V(sx * x * 0.9, -0.16, (chest + z_bot) / 2), V(sx * x * 0.7, -0.14, z_bot)]
        P = KX.surf_path(S, pts, lift=0.009, n=16)
        A.add(tube(P, w, n=4, r2=0.006, up=(sx, 0, 0), phase=math.pi / 4), look=look, skin=2, smooth=40)
    cx, cy, rx, ry = girth_of(A, 'body', chest)
    a = np.radians(np.linspace(0, 360, 26, endpoint=False))
    ring = np.stack([cx + (rx + 0.02) * np.cos(a), cy + (ry + 0.02) * np.sin(a), np.full(26, chest) + 0.02 * np.sin(a)], axis=1)
    P = np.array([S.nearest(q)[0] + S.nearest(q)[1] * 0.011 for q in ring])
    A.add(tube(P, w, n=4, r2=0.006, up=(0, 0, 1), closed=True, phase=math.pi / 4), look=look, skin=2, smooth=40)


@part
def brand_ring(''')
# hod: touches the back, anchor on its front face, straps
s = rep(s, "def hod(A, look='wicker', frame='tar', z0=1.0, h=1.75, w=0.62, d=0.36, y=0.20, trunks=5, bone='spine_03', strap='strap'):",
        "def hod(A, look='wicker', frame='tar', z0=1.0, h=1.75, w=0.62, d=0.36, y=0.135, trunks=5, bone='spine_03', strap='strap'):")
s = rep(s, "    c = V(0, y + d / 2, z0)\n    K = c.copy()\n", "    c = V(0, y + d / 2, z0)\n    K = V(0, y, z0 + 0.3)                    # keep anchor on the hod's front face: it stays against the back whatever the bulk\n")
i0 = s.index("@part\ndef hod(")
i1 = s.index("@part\ndef cage_back(")
hod = s[i0:i1]
old = """    for z_, tl in ((1.22, 14.0), (1.40, -14.0)):
        cx, cy, rx, ry = girth_of(A, 'body', z_)
        A.add(band((0, cy + 0.04, z_), rx * 0.92 + 0.014, ry + 0.07, 0.04, 0.009, tilt_y=tl, n=22), bone=bone, look=strap)
"""
assert old in hod
hod = hod.replace(old, "    load_straps(A, look=strap, y_back=y)\n")
s = s[:i0] + hod + s[i1:]
# cage: the same
s = rep(s, "def cage_back(A, look='wicker', R=0.36, h=0.72, z0=1.02, y=0.20, bone='spine_03', glow='glow', strap='strap', load='rag'):",
        "def cage_back(A, look='wicker', R=0.36, h=0.72, z0=1.04, y=0.115, bone='spine_03', glow='glow', strap='strap', load='rag'):")
s = rep(s, "    K = c.copy()                                          # shared keep anchor: the cage moves as one rigid thing",
        "    K = V(0, y, z0 + h * 0.35)                            # shared keep anchor on the cage's front: it stays against the back")
old = """    for z_, tl in ((1.22, 14.0), (1.40, -14.0)):
        cx, cy, rx, ry = girth_of(A, 'body', z_)
        A.add(band((0, cy + 0.05, z_), rx * 0.92 + 0.014, ry + 0.09, 0.04, 0.009, tilt_y=tl, n=22), bone=bone, look=strap)
"""
assert old in s
s = s.replace(old, "    load_straps(A, look=strap, y_back=y)\n")
save(p, s)

# ------------------------------------------------------------------ the Mute: a low wide arch, a bigger hole, fused mittens
p = H + 'asm_parts8.py'
s = load(p)
s = rep(s, "def mute_torso(A, look='chalk', hole=0.105, c=(0.0, 0.0, 1.29), lining='void'):", "def mute_torso(A, look='chalk', hole=0.118, c=(0.0, 0.0, 1.265), lining='void'):")
s = rep(s, """    key = np.array([(1.00, 0.020, 0.168, 0.132), (1.10, 0.020, 0.180, 0.142), (1.20, 0.018, 0.208, 0.152), (1.30, 0.016, 0.240, 0.160), (1.38, 0.016, 0.266, 0.162),
                    (1.44, 0.016, 0.276, 0.158), (1.50, 0.014, 0.266, 0.150), (1.555, 0.012, 0.228, 0.136), (1.60, 0.010, 0.168, 0.114), (1.635, 0.008, 0.09, 0.07)])""",
        """    key = np.array([(1.00, 0.020, 0.172, 0.134), (1.10, 0.020, 0.196, 0.146), (1.20, 0.018, 0.236, 0.158), (1.30, 0.016, 0.276, 0.166), (1.38, 0.016, 0.304, 0.168),
                    (1.44, 0.016, 0.312, 0.164), (1.49, 0.014, 0.296, 0.154), (1.53, 0.012, 0.255, 0.138), (1.565, 0.010, 0.19, 0.114), (1.59, 0.008, 0.10, 0.07)])""")
i0 = s.index("@part\ndef mute_hands(")
new = '''@part
def mute_hands(A, look='chalk', size=0.34, th=0.042):
    """flat mitten-hands the size of shields: one paddle with a thumb lobe, grown out of the forearm without a wrist
    (rigid on the forearm: with lengthened arms a hand-bound mitten swings off its joint and comes apart)"""
    for sx in (1, -1):
        s = '_l' if sx > 0 else '_r'
        w = A.R.H('hand' + s)
        m = ellipsoid(size * 0.62, size * 0.47, th, n=18, m=9)
        k = np.clip(m.v[:, 0] / (size * 0.62), -1, 1)
        m.v[:, 1] *= 1.0 - 0.38 * np.clip(-k, 0, 1) ** 2                                         # narrow at the wrist
        m.v[:, 2] *= 1.0 + 0.9 * np.clip(-k, 0, 1) ** 2                                          # ... and thick there, like a wrist
        m.v[:, 2] -= 0.11 * size * np.clip(k, 0, 1) ** 2                                         # the tip curls to the palm
        th_ = ellipsoid(size * 0.24, size * 0.14, th * 0.95, n=10, m=6).rot('z', -34).move(-size * 0.10, -size * 0.43, -0.006)
        g = join([m, th_])
        if sx < 0:
            g.mirror()
        A.add(g.move(w + V(sx * size * 0.50, 0, 0)), bone='lowerarm' + s, look=look, smooth=180)
'''
s = s[:i0] + new
save(p, s)

p = H + 'specs_s4_a.py'
s = load(p)
s = rep(s, "props=dict(bulk=1.3, shoulders=1.12, chest=1.1, arm=1.2, arm_len=1.15, hunch=24, leg=1.1),", "props=dict(bulk=1.3, shoulders=1.16, chest=1.12, arm=1.2, arm_len=1.15, hunch=26, leg=1.1),")
save(p, s)
print('round2 ok')
