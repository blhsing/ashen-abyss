"""Stage 4 specs (part B): more oversized bases -- frost road-clearer, forge drop-hammer, husk huddle, pillar-bearer."""

SPECS = {
    'mon_rime_breaker': dict(
        family='frost', scale=1.5, poses='heavy', emis=(0.45, 0.78, 1.0), dress={'frost': 0.6},
        body=dict(type='flesh', decimate=1.0, show=['hands'], look='skin_ash', eyes=None),
        props=dict(bulk=1.25, chest=1.08, hunch=10, hands=1.2),
        looks={'glow': {'strength': 2.6, 'mottle': 0.3}, 'hood': ['sealskin', {}]},
        parts=[
            dict(p='robe', look='sealskin', hem=0.22, top=1.51, girth=1.06, flare=1.3, rag=0.02, pleat=0.03, seed=5, crumple=0.006, pelvis_mix=0.5),
            dict(p='sleeves', look='sealskin', r_cuff=0.085, droop=0.012, r_sh=0.095, r_el=0.082),
            dict(p='hood', look='hood', tail=0.08, tail_drop=0.06), dict(p='capelet', look='hood', z_bot=1.40, r_bot=(0.27, 0.2), rag=0.01),
            dict(p='snowshoes'),
            dict(p='belt', on='robe', z=1.07, bone='spine_01', pad=0.01, w=0.05),
            dict(p='foot_wraps', look='sealskin', z=0.3),
            dict(p='icicles', bone='Head', a=(-0.07, -0.145, 1.60), b=(0.07, -0.145, 1.60), count=5, length=0.07),
        ],
        weapons=[dict(w='plough'), dict(w='boulder_pick')],
        notes="Ice-breaker: giant road-clearer in a hooded sealskin coat as stiff as a bell, a V-shaped wooden snow-plough blade on the "
              "left arm like a prow (wpn_plough on hand_l), a long pick with a frozen boulder for a head (wpn_pick, emis_ core), "
              "snowshoes lashed upright on the back."),
    'mon_forge_hammer': dict(
        family='forge', scale=1.5, poses='heavy', emis=(1.0, 0.42, 0.08),
        body=dict(type='flesh', decimate=0.66, look='skin_ash', eyes='void', gaunt=0.6, hide=['lowerarm_r', 'hand_r']),
        props=dict(bulk=1.3, chest=1.1, hunch=12, leg=1.05, pose={'spine_02': dict(rot=[('y', -6)])}),
        looks={'skin_ash': {'color': (0.20, 0.185, 0.175), 'hands_dark': 0.9, 'dark': (0.02, 0.02, 0.02)}, 'ember': {'strength': 1.6},
               'apron': {'color': (0.05, 0.04, 0.032)}, 'iron': {'amount': 0.55, 'color': (0.13, 0.13, 0.14)}},
        parts=[
            dict(p='hammer_arm', side='r'),
            dict(p='apron', length=0.8, pad=0.03),
            dict(p='shell', look='trousers', regions=('thighs', 'calves', 'pelvis'), z1=1.05, offset=0.012, sink=False),
            dict(p='shell', look='hide', regions=('head',), z0=1.70, offset=0.008, sink=False),
        ],
        notes="Hammer-thrall: a massive thrall whose right arm and shoulder are an iron drop-hammer assembly -- shoulder housing, a "
              "beam from the elbow and a square block head the size of an anvil (rigid on lowerarm_r, emis_ heat on the face), "
              "counterweighted by a ballast drum bolted to the left shoulder."),
    'mon_husk_huddle': dict(
        family='husk', scale=1.5, poses='zombie', emis=(1.0, 0.55, 0.2),
        body=dict(type='flesh', decimate=0.55, gaunt=1.2, jaw=20.0, claws=0.06, toe_claws=0.03),
        props=dict(bulk=1.12, hips=1.0, waist=0.95, chest=1.1, neck=0.8, arm=0.8, leg=0.85, hunch=30),
        looks={'eye': {'strength': 2.2}, 'glow': {'strength': 1.6, 'mottle': 0.6}, 'rag': {'color': (0.12, 0.12, 0.125), 'stain_amt': 0.5}},
        dress={'frost': 0.35},
        parts=[
            dict(p='fused_bodies', eyes='eye'),
            dict(p='blanket'),
            dict(p='glow_blob', bone='spine_03', at=(0.0, 0.17, 1.30), r=(0.07, 0.05, 0.09)),
            dict(p='rags', arcs=((-140, -40, 0.4), (40, 140, 0.45)), z=1.0, rx=0.17, ry=0.14),
        ],
        notes="Huddle: four husks who froze together sharing warmth -- one walks, three more (head, torso, limp arms) are fused to its "
              "back and shoulders (rigid on spine / clavicle), under one shared blanket frozen stiff; a faint emis_ warmth between "
              "the bodies."),
    'mon_effigy_caryatid': dict(
        family='effigy', scale=1.5, poses=["Idle_Loop:0.3", "Sword_Attack:0.45", "Walk_Loop:0.3"], emis=(0.45, 1.0, 0.5),
        body=dict(type='flesh', decimate=0.5, look='limestone', eyes=None, melt=2),
        props=dict(bulk=1.3, chest=1.08, leg=1.08, pose={'upperarm_l': dict(rot=[('y', -70)]), 'upperarm_r': dict(rot=[('y', 70)])}),
        looks={'glow': {'strength': 1.6, 'mottle': 0.2}, 'robe': ['limestone', {}]},
        parts=[
            dict(p='pillar_load'),
            dict(p='robe', look='robe', hem=0.10, top=1.10, girth=1.12, rag=0.0, pleat=0.09, seed=4, crumple=0.0, pelvis_mix=0.6),
            dict(p='stone_armour', helm=False),
            dict(p='collar', look='glow', z=1.535, r=0.108, w=0.012, ring=False),
        ],
        notes="Caryatid: a broad column-statue with its arms baked 70 deg up at the shoulders, carrying a broken length of fluted "
              "pillar across its shoulders (rigid on spine_03) and a square capital block on its head; carved skirt; thin emis_ line "
              "at the neck joint."),
}
