"""round 6 (2026-11-20): weak-point pass.  Edits parts 6 / 9 / 10 and the specs.  Applied once; do not re-run."""
H = 'Y:/tools/blender/monsters/'


def load(p):
    return open(p, encoding='utf-8').read()


def save(p, s):
    import ast
    ast.parse(s)
    open(p, 'w', encoding='utf-8', newline='\n').write(s)


def rep(s, a, b, n=1):
    assert a in s, a[:110]
    return s.replace(a, b, n)


# =================================================================== parts 10: rivets on the OUTER faces, ropes end on the coat
p = H + 'asm_parts10.py'
s = load(p)
s = rep(s, "                add(KC3.rivet(stem_top + sdir * (H_ * tz) + d * (wd * u) + nrm * (0.027 * -sgn), nrm * -sgn, r=0.012, n=6), rim)",
        "                add(KC3.rivet(stem_top + sdir * (H_ * tz) + d * (wd * u) + nrm * (0.027 * sgn), nrm * sgn, r=0.012, n=6), rim)")
s = rep(s, """        p0 = V(p0[0], p0[1], 1.0)
        p1 = V(sgn * 0.27, -0.10, 1.07)""",
        """        p0 = V(p0[0], p0[1], 1.0)
        on = 'robe' if 'robe' in A.refs else 'body'
        cx, cy, rx, ry = girth_of(A, on, 1.07)              # rest-space girth; the bulk of the bearer widens it
        sx_ = spine_scale(A, 0)(1.07); sy_ = spine_scale(A, 1)(1.07)
        p1 = V(sgn * rx * sx_ * 0.90, cy - ry * sy_ * 0.42, 1.07)""")
s = rep(s, "        pits = [ellipsoid(0.017, 0.013, 0.015, n=6, m=3).move(sx * 0.032, -0.094, 1.668) for sx in (-1, 1)]\n        pits.append(ellipsoid(0.021, 0.014, 0.03, n=6, m=3).move(0.0, -0.102, 1.588))",
        "        pits = [ellipsoid(0.017, 0.013, 0.015, n=6, m=3).move(sx * 0.032, -0.098, 1.668) for sx in (-1, 1)]\n        pits.append(ellipsoid(0.021, 0.014, 0.03, n=6, m=3).move(0.0, -0.108, 1.588))")
save(p, s)

# =================================================================== parts 9
p = H + 'asm_parts9.py'
s = load(p)
# ---- mirror-carrier: real lifting forks (thick L-shaped tines on a carriage)
i0 = s.index("        # fork carriage at the wrist: two tines side by side")
i1 = s.index("    # ---- the A-frame and the wedge of the Brass Sun")
new = '''        # fork carriage at the wrist: two thick tines side by side (T-pose Z = sideways once the arm hangs), pointing forward (-Y)
        A.add(box(0.10, 0.075, 0.44, bevel=0.012).move(wr + V(sx * 0.06, -0.012, 0)), bone='lowerarm' + s, look=dark, smooth=30)
        A.add(cyl(wr + V(sx * 0.06, -0.012, -0.24), wr + V(sx * 0.06, -0.012, 0.24), 0.03, n=8), bone='lowerarm' + s, look=look, smooth=40)        # the carriage's axle
        for tz in (-0.145, 0.145):
            hx = 0.03; hw = 0.062                                    # half thickness of a tine (along the arm), half width
            tine = M.loft(np.array([[(-hx, 0.02, -hw), (hx, 0.02, -hw), (hx, 0.02, hw), (-hx, 0.02, hw)],
                                    [(-hx, -0.34, -hw), (hx, -0.34, -hw), (hx, -0.34, hw), (-hx, -0.34, hw)],
                                    [(-hx, -0.48, -hw * 0.9), (hx * 0.1, -0.48, -hw * 0.9), (hx * 0.1, -0.48, hw * 0.9), (-hx, -0.48, hw * 0.9)],
                                    [(-hx, -0.56, -hw * 0.7), (-hx * 0.55, -0.56, -hw * 0.7), (-hx * 0.55, -0.56, hw * 0.7), (-hx, -0.56, hw * 0.7)]]), cap0=True, cap1=True).recalc()
            tine.smooth = 30.0
            if sx < 0:
                tine.mirror()
            A.add(tine.move(wr + V(sx * 0.105, -0.04, tz)), bone='lowerarm' + s, look=bright)
            A.add(box(0.30, 0.06, 0.124, bevel=0.008).move(wr + V(-sx * 0.045, -0.025, tz)), bone='lowerarm' + s, look=look, smooth=30)        # the tine's shank up the arm
            for xx in (-0.14, -0.02):
                A.add(KC3.rivet(wr + V(sx * xx, -0.056, tz), (0, -1, 0), r=0.016, n=6), bone='lowerarm' + s, look=dark)
'''
s = s[:i0] + new + s[i1:]

# ---- crowned colony: cap + stalk on spine_02 (it tips far less than spine_03), the lantern-fruit lag on their threads
s = rep(s, """    K = V(0, 0.13, 1.60)
    bone = 'spine_03'
    zc = 2.02""", """    K = V(0, 0.13, 1.60)
    bone = 'spine_02'
    zc = 2.02""")
s = rep(s, """        A.add(tube([p0 + V(0, 0, 0.02), p0 + V(0, 0, -L_)], 0.0045, n=4), bone=bone, look=stalk, keep=K)
        fr = revolve(""", """        th_ = tube([p0 + V(0, 0, 0.02), p0 + V(0, 0, -L_)], 0.0045, n=4)
        A.add(th_, bone=bone, look=stalk, keep=K, sway=('pelvis', 0.3 * np.clip((p0[2] - th_.v[:, 2]) / L_, 0, 1)))
        fr = revolve(""")
s = rep(s, "        A.add(fr.move(p0 + V(0, 0, -L_)), bone=bone, look=lamp, smooth=180, keep=K)", "        A.add(fr.move(p0 + V(0, 0, -L_)), bone=bone, look=lamp, smooth=180, keep=K, sway=('pelvis', 0.3))")
s = rep(s, "revolve([(0.0, 0.012), (0.026, 0.004), (0.032, -0.018), (0.02, -0.014), (0.0, -0.004)], n=7).move(p0 + V(0, 0, -L_)), bone=bone, look=stalk, smooth=60, keep=K)",
        "revolve([(0.0, 0.012), (0.026, 0.004), (0.032, -0.018), (0.02, -0.014), (0.0, -0.004)], n=7).move(p0 + V(0, 0, -L_)), bone=bone, look=stalk, smooth=60, keep=K, sway=('pelvis', 0.3))")

# ---- gallows-tree: nooses and gibbet hang nearer the vertical (their low ends follow the hips, not the lower spine)
s = rep(s, "        sway_weights(ns, zb, zb - 0.75, amt=0.6)", "        sway_weights(ns, zb, zb - 0.75, low='pelvis', amt=0.85)")
s = rep(s, "    sway_weights(ch, zb, zb - 0.9, amt=0.6)", "    sway_weights(ch, zb, zb - 0.9, low='pelvis', amt=0.85)")
s = rep(s, "        sway_weights(q, zb, zb - 0.9, amt=0.6)", "        sway_weights(q, zb, zb - 0.9, low='pelvis', amt=0.85)")
s = rep(s, "    sway_weights(sk, zb, zb - 0.9, amt=0.6)", "    sway_weights(sk, zb, zb - 0.9, low='pelvis', amt=0.85)")

# ---- tallow-mound: the arm-bodies splay outward when they hang (they were inside the cone's outline)
s = rep(s, """        b.v = np.outer((q[:, 2] - 0.98) * kz - 0.12, ax) + np.outer((q[:, 1] - 0.02) * 0.9, V(0, 1, 0)) + np.outer(q[:, 0] * 0.74, V(0, 0, -sx)) + sh
        b.recalc()""",
        """        b.v = np.outer((q[:, 2] - 0.98) * kz - 0.12, ax) + np.outer((q[:, 1] - 0.02) * 0.9, V(0, 1, 0)) + np.outer(q[:, 0] * 0.74, V(0, 0, -sx)) + sh
        # T-pose +Z is OUTWARD once the arm hangs: lift the body off the bone line, more towards the wrist, so that the
        # hanging arm lies on the outside of the cone instead of inside it
        b.v[:, 2] += splay[0] + splay[1] * np.clip((b.v - sh) @ ax, 0.0, None)
        b.recalc()""")
s = rep(s, """            p0 = KC3.BP('lowerarm' + s, t) + V(0, 0.0, -rr)
""", """            p0 = KC3.BP('lowerarm' + s, t) + V(0, 0.0, -rr)
            p0[2] += splay[0] + splay[1] * max(0.0, float((p0 - sh) @ ax))
""")
s = rep(s, "def tallow_mound(A, look='tallow', wick='wick', seed=5, wicks=12, lit=True):", "def tallow_mound(A, look='tallow', wick='wick', seed=5, wicks=12, lit=True, splay=(0.05, 0.27)):")
save(p, s)

# =================================================================== parts 6: the yoke's bundles hang on their chains
p = H + 'asm_parts6.py'
s = load(p)
s = rep(s, """        A.add(M.chain(np.array([e + V(0, 0, -r * 0.8), e + V(0, 0, -drop)]), link=0.07, r=0.011), bone=bone, look=chain, smooth=60, keep=K)
        if loads == 'logs':
            for p, lk in bundle(5, 0.62, 0.058, seed=4 + sx):
                A.add(p.move(e + V(0, 0, -drop)), bone=bone, look=lk, smooth=40, keep=K)""",
        """        # chain and load HANG: their low end follows the hips (sway), so they lag when the shoulders turn or tip
        ch_ = M.chain(np.array([e + V(0, 0, -r * 0.8), e + V(0, 0, -drop)]), link=0.07, r=0.011)
        A.add(ch_, bone=bone, look=chain, smooth=60, keep=K, sway=(sway[0], sway[1] * np.clip((e[2] - ch_.v[:, 2]) / drop, 0, 1)))
        if loads == 'logs':
            for p, lk in bundle(5, 0.62, 0.058, seed=4 + sx):
                A.add(p.move(e + V(0, 0, -drop)), bone=bone, look=lk, smooth=40, keep=K, sway=sway)""")
s = rep(s, "def yoke(A, look='tar', length=1.75, z=1.565, y=0.085, r=0.05, loads='logs', drop=0.34, bone='spine_03', chain='rust'):",
        "def yoke(A, look='tar', length=1.75, z=1.565, y=0.085, r=0.05, loads='logs', drop=0.34, bone='spine_03', chain='rust', sway=('pelvis', 0.4)):")
save(p, s)

# =================================================================== specs
p = H + 'specs_s4_a.py'
s = load(p)
s = rep(s, "parts=[dict(p='hod', bone='spine_02'), dict(p='brand_ring')] + LOIN,", "parts=[dict(p='hod', bone='spine_01'), dict(p='brand_ring')] + LOIN,")
s = rep(s, "parts=[dict(p='cage_back'), dict(p='tong_arm', side='r', length=1.25, glow=None, look='rust'),", "parts=[dict(p='cage_back', bone='spine_02'), dict(p='tong_arm', side='r', length=1.25, glow=None, look='rust'),")
s = rep(s, """        props=dict(bulk=1.3, shoulders=1.16, chest=1.12, arm=1.2, arm_len=1.15, hunch=18, leg=1.1),
        parts=[dict(p='mute_torso'), dict(p='mute_hands')],""",
        """        props=dict(bulk=1.3, shoulders=1.16, chest=1.12, arm=1.3, arm_len=1.15, hunch=10, leg=1.1),
        parts=[dict(p='mute_chest'), dict(p='mute_hands', size=0.42)],""")
s = rep(s, """flat mitten-hands the size of shields, one round hole clean through the chest (a real tunnel). Dry cracked chalk. No emissive."),""",
        """flat mitten-hands the size of shields, one ROUND hole clean through the slab of the chest (a see-through tunnel with pale walls; the "
              "chest is narrower than the shoulder joints so the arms hang free from big shoulder masses). Dry cracked chalk. No emissive."),""")
save(p, s)

p = H + 'specs_s4_b.py'
s = load(p)
# ice-breaker: the plough is a prow in front, carried on the hips; the left arm holds its grip bar
s = rep(s, """        body=dict(type='flesh', decimate=1.0, show=['hands'], look='skin_ash', eyes=None),
        props=dict(bulk=1.25, chest=1.08, hunch=10, hands=1.2),""",
        """        body=dict(type='flesh', decimate=1.0, show=['hands'], look='skin_ash', eyes=None,
                  bind_w={'spine_02': ['upperarm_l'], 'pelvis': ['lowerarm_l', 'hand_l']}),
        props=dict(bulk=1.25, chest=1.08, hunch=10, hands=1.2,
                   pose={'upperarm_l': dict(rot=[('y', 45), ('z', -80)]), 'lowerarm_l': dict(rot=[('z', -25)])}),""")
s = rep(s, "            dict(p='arm_plough', side='l'),", "            dict(p='prow_plough'),")
s = rep(s, """left forearm like a prow (rigid on lowerarm_l: it stands upright in front of the arm when it hangs), a long pick with a \"""",
        """hips IN FRONT of him like the prow of a ship (rigid on the pelvis, two ropes to the belt; the left arm is baked forward on its "
              "grip bar and bound to spine_02 / pelvis, so it never leaves the bar), a long pick with a \"""")
# caryatid: the arms are one block with the chest and the pillar
s = rep(s, """                  merge_w={'upperarm_l': ['lowerarm_l', 'hand_l'], 'upperarm_r': ['lowerarm_r', 'hand_r']}),
        props=dict(bulk=1.3, chest=1.08, leg=1.08, pose={'upperarm_l': dict(rot=[('z', -10), ('y', -110)]), 'upperarm_r': dict(rot=[('z', 10), ('y', 110)])}),""",
        """                  bind_w={'spine_03': ['upperarm_l', 'lowerarm_l', 'hand_l', 'upperarm_r', 'lowerarm_r', 'hand_r', 'clavicle_l', 'clavicle_r']}),
        props=dict(bulk=1.3, chest=1.08, leg=1.08,
                   pose={'upperarm_l': dict(rot=[('y', -15)]), 'lowerarm_l': dict(rot=[('y', -75)]), 'hand_l': dict(rot=[('x', 90), ('z', 90)]),
                         'upperarm_r': dict(rot=[('y', 15)]), 'lowerarm_r': dict(rot=[('y', 75)]), 'hand_r': dict(rot=[('x', 90), ('z', -90)])}),""")
s = rep(s, """notes="Caryatid: a broad column-statue with its arms baked 110 deg up at the shoulders (stiff: elbow / hand weights merged into the "
              "upper arm) so that they reach up to the load in the idle, carrying a broken length of fluted \"""",
        """notes="Caryatid: a broad column-statue whose arms are carved in one block with the chest and the load (posed elbows out, forearms "
              "up, hands laid over the pillar, then bound to spine_03 like the pillar: they never leave it), carrying a broken length of fluted \"""")
save(p, s)

p = H + 'specs_s4_c.py'
s = load(p)
s = rep(s, "        looks={},\n        parts=[dict(p='brazier_hulk')],", "        looks={}, dress={'frost': 0.3},\n        parts=[dict(p='brazier_hulk2')],")
s = rep(s, """        body=dict(type='flesh', decimate=0.4, look='ashcrust', eyes=None, melt=3, hide=['head', 'neck']),""",
        """        body=dict(type='flesh', decimate=0.4, look='ashmass', eyes=None, melt=3, hide=['head', 'neck']),""")
s = rep(s, """            dict(p='drift_mass'),
            dict(p='cracks', look='spark', regions=('thighs', 'upperarms', 'forearms'), count=6, seed=5, width=0.011),""",
        """            dict(p='drift_mass2'),""")
s = rep(s, "        looks={'tallow': {'soot': 0.3}},", "        looks={'tallow': {'color': (0.46, 0.40, 0.26), 'soot': 0.4}},")
save(p, s)

p = H + 'asm_parts.py'
s = load(p)
if "'asm_parts10'" not in s:
    s = rep(s, "'asm_parts8', 'asm_parts9']", "'asm_parts8', 'asm_parts9', 'asm_parts10']")
    save(p, s)
print('round6 parts + specs ok')
