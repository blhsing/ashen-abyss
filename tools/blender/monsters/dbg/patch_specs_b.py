p = r'Y:\tools\blender\monsters\specs_s4_b.py'
s = open(p, encoding='utf-8').read()


def rep(a, b):
    global s
    assert a in s, a[:90]
    s = s.replace(a, b, 1)


# ---- ice-breaker: plough on the forearm (a part), dimmer core in the boulder
rep("""        looks={'glow': {'strength': 2.6, 'mottle': 0.3}, 'hood': ['sealskin', {}]},""",
    """        looks={'glow': {'strength': 1.4, 'mottle': 0.5}, 'hood': ['sealskin', {}], 'oak': {'color': (0.13, 0.125, 0.115)}},""")
rep("""            dict(p='icicles', bone='Head', a=(-0.07, -0.145, 1.60), b=(0.07, -0.145, 1.60), count=5, length=0.07),
        ],
        weapons=[dict(w='plough'), dict(w='boulder_pick')],""",
    """            dict(p='icicles', bone='Head', a=(-0.07, -0.145, 1.60), b=(0.07, -0.145, 1.60), count=5, length=0.07),
            dict(p='arm_plough', side='l'),
        ],
        weapons=[dict(w='boulder_pick')],""")
rep("""left arm like a prow (wpn_plough on hand_l), a long pick with a frozen boulder for a head (wpn_pick, emis_ core), \"""",
    """left forearm like a prow (rigid on lowerarm_l: it stands upright in front of the arm when it hangs), a long pick with a "
              "frozen boulder for a head (wpn_pick, emis_ core), \"""")
# ---- hammer thrall
rep("""        looks={'skin_ash': {'color': (0.20, 0.185, 0.175), 'hands_dark': 0.9, 'dark': (0.02, 0.02, 0.02)}, 'ember': {'strength': 1.6},
               'apron': {'color': (0.05, 0.04, 0.032)}, 'iron': {'amount': 0.55, 'color': (0.13, 0.13, 0.14)}},
        parts=[
            dict(p='hammer_arm', side='r'),""",
    """        looks={'skin_ash': {'color': (0.20, 0.185, 0.175), 'hands_dark': 0.9, 'dark': (0.02, 0.02, 0.02)}, 'ember': {'strength': 1.5, 'mottle': 0.7},
               'apron': {'color': (0.05, 0.04, 0.032)}, 'iron': {'amount': 0.42, 'color': (0.105, 0.105, 0.115), 'rough': 0.5},
               'steel': {'amount': 0.3, 'color': (0.20, 0.20, 0.21)}},
        parts=[
            dict(p='hammer_arm', side='r'), dict(p='gag_plate', look='iron'),""")
# ---- huddle
rep("""        body=dict(type='flesh', decimate=0.55, gaunt=1.2, jaw=20.0, claws=0.06, toe_claws=0.03),
        props=dict(bulk=1.12, hips=1.0, waist=0.95, chest=1.1, neck=0.8, arm=0.8, leg=0.85, hunch=30),""",
    """        body=dict(type='flesh', decimate=0.46, gaunt=1.2, jaw=20.0, claws=0.06, toe_claws=0.03),
        props=dict(arm=0.85, leg=0.9, hunch=26),""")
rep("""            dict(p='fused_bodies', eyes='eye'),
            dict(p='blanket'),
            dict(p='glow_blob', bone='spine_03', at=(0.0, 0.17, 1.30), r=(0.07, 0.05, 0.09)),""",
    """            dict(p='huddle'),
            dict(p='glow_blob', bone='spine_03', at=(0.0, 0.16, 1.26), r=(0.085, 0.06, 0.12)),
            dict(p='glow_blob', bone='spine_03', at=(0.12, 0.10, 1.10), r=(0.05, 0.05, 0.07)),
            dict(p='glow_blob', bone='spine_03', at=(-0.13, 0.09, 1.12), r=(0.05, 0.05, 0.07)),""")
rep("""notes="Huddle: four husks who froze together sharing warmth -- one walks, three more (head, torso, limp arms) are fused to its "
              "back and shoulders (rigid on spine / clavicle), under one shared blanket frozen stiff; a faint emis_ warmth between "
              "the bodies."),""",
    """notes="Huddle: six husks who froze together sharing warmth -- one walks, five more (head, torso, limp arms) are fused to its "
              "back and shoulders (one cluster on spine_03, the hanging arms blend to spine_01 and lag), under one shared blanket "
              "frozen stiff (a sheet draped over the real shapes); a faint emis_ warmth between the bodies."),""")
# ---- caryatid
rep("""        props=dict(bulk=1.3, chest=1.08, leg=1.08, pose={'upperarm_l': dict(rot=[('y', -70)]), 'upperarm_r': dict(rot=[('y', 70)])}),
        looks={'glow': {'strength': 1.6, 'mottle': 0.2}, 'robe': ['limestone', {}]},""",
    """        props=dict(bulk=1.3, chest=1.08, leg=1.08, pose={'upperarm_l': dict(rot=[('z', -10), ('y', -110)]), 'upperarm_r': dict(rot=[('z', 10), ('y', 110)])}),
        looks={'glow': {'strength': 1.5, 'mottle': 0.2}, 'robe': ['limestone', {}]}, jpeg=(86, 84, 90),""")
rep("""            dict(p='collar', look='glow', z=1.535, r=0.108, w=0.012, ring=False),""",
    """            dict(p='band_glow', z=1.538, r=0.086, w=0.007),""")
rep("""notes="Caryatid: a broad column-statue with its arms baked 70 deg up at the shoulders, carrying a broken length of fluted \"""",
    """notes="Caryatid: a broad column-statue with its arms baked 110 deg up at the shoulders (stiff: elbow / hand weights merged into the "
              "upper arm) so that they reach up to the load in the idle, carrying a broken length of fluted \"""")
open(p, 'w', encoding='utf-8', newline='\n').write(s)

# a thin glowing line round the neck joint
p = r'Y:\tools\blender\monsters\asm_parts9.py'
s = open(p, encoding='utf-8').read()
s += '''

# ================================================================================================ small things
@part
def band_glow(A, look='glow', z=1.538, r=0.086, w=0.007, bone='neck_01', cy=-0.012):
    """a thin glowing line round a joint (the neck joint of a statue): a closed ring, tight on the surface"""
    A.add(torus(r, w, n=18, m=4, ry=r * 1.08).rot('x', -8).move(0, cy, z), bone=bone, look=look)
'''
open(p, 'w', encoding='utf-8', newline='\n').write(s)

# boulder pick: the core shows on two broken faces instead of a snowball on top
p = r'Y:\tools\blender\monsters\asm_weapons.py'
s = open(p, encoding='utf-8').read()
a = """    if glow:
        ps.append((ellipsoid(r * 0.3, r * 0.3, r * 0.3, n=7, m=5).move(-r * 0.62, length + r * 0.5, 0.0), glow, 60))
    for k in range(3):
        ps.append((torus(0.04, 0.008, n=8, m=4).rot('x', 90).move(0, length - 0.05 + 0.06 * k, 0), 'rope', 50))"""
b = """    if glow:                                             # the core: seen through three windows in the boulder's faces
        for (dx, dy, dz, a_, b_, c_) in ((-0.70, 0.5, 0.0, 0.10, 0.34, 0.30), (0.1, 0.5, 0.60, 0.30, 0.30, 0.09), (0.05, 0.5, -0.62, 0.26, 0.32, 0.09)):
            ps.append((ellipsoid(r * a_, r * b_, r * c_, n=8, m=5).move(r * dx, length + r * dy, r * dz), glow, 30))
    for k in range(3):
        ps.append((torus(0.04, 0.008, n=8, m=4).rot('x', 90).move(0, length - 0.05 + 0.06 * k, 0), 'rope', 50))"""
assert a in s
s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
