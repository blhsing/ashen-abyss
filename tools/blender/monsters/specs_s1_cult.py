"""Stage 1 specs: variants of the cultist / priest (cult family) and of the wraith."""

ROBED = dict(type='flesh', decimate=1.0, show=['hands'], look='skin_ash', eyes=None)
BLACK_WRAP = {'color': (0.055, 0.055, 0.06), 'dark': (0.010, 0.010, 0.012), 'stain': (0.10, 0.10, 0.11), 'bands': 30.0, 'stain_amt': 0.35}

SPECS = {
    'mon_cult_snuffer': dict(
        family='cult', scale=1.05, poses=["Sword_Idle:0.3", "Sword_Attack:0.45", "Walk_Loop:0.3"],
        body=dict(type='flesh', decimate=0.8, show=['hands', 'head', 'neck'], look='skin_ash', eyes='void', gaunt=0.7),
        looks={'brass': {'color': (0.15, 0.115, 0.06), 'amount': 0.62, 'rough': 0.5}, 'robe': {'color': (0.23, 0.225, 0.22)}},
        parts=[
            dict(p='robe', seed=6), dict(p='sleeves'), dict(p='hood_down'), dict(p='wax_cap'),
            dict(p='bandolier', items='coil', look='shroudcloth', on='robe', z=1.30, tilt=-32.0, pad=0.03),
            dict(p='rope_belt'), dict(p='foot_wraps'),
        ],
        weapons=[dict(w='pole', head='snuffer', look='brass', back=0.80, front=1.85, size=1.15, name='snuffer')],
        notes="Snuffer: robed, hood thrown back off a shaved wax-sealed scalp, a coil of wet shroud across the chest, a three-metre "
              "pole ending in a bell-shaped snuffer cone (wpn_snuffer)."),
    'mon_cult_crossbow': dict(
        family='cult', scale=1.0, poses='pistol',
        body=dict(type='flesh', decimate=0.8, show=['hands', 'head'], look='skin_ash', eyes='void', gaunt=0.6),
        looks={'linen': {'color': (0.62, 0.60, 0.55)}, 'wood': {'color': (0.085, 0.06, 0.04)}},
        parts=[
            dict(p='robe', hem=0.30, girth=0.94, rag=0.03, seed=4, chest=0.86, crumple=0.002),
            dict(p='poncho', look='hood', X=0.43, rag=0.12, point=0.04, holes=0.0, seed=8),
            dict(p='hood', void=False, tail=0.05, tail_drop=0.05), dict(p='hood_tail'),
            dict(p='gag_plate'),
            dict(p='bandolier', items='bolts', on='robe', z=1.30, tilt=-28.0, rx=0.205, ry=0.185, pad=0.0),
            dict(p='rope_belt', tail=0.2), dict(p='foot_wraps', z=0.37),
        ],
        weapons=[dict(w='crossbow')],
        notes="Gagged crossbowman: short cape cut off at the elbows, hood with a long tail weighted by a lead seal, riveted iron "
              "gag-plate, chest bandolier of white-fletched bolts, heavy hand-crossbow (wpn_crossbow, held like a pistol)."),
    'mon_cult_flagellant': dict(
        family='cult', scale=0.95, poses='punch',
        body=dict(type='flesh', decimate=0.7, look='skin_ash', eyes='void', gaunt=0.9),
        props=dict(bulk=0.9, waist=0.88, arm=0.84, leg=0.88, hunch=8),
        looks={'skin_ash': {'sore': (0.22, 0.07, 0.05), 'sore_amt': 1.0, 'sore_lo': 0.62, 'hands_dark': 1.0, 'dark': (0.012, 0.012, 0.012), 'scar': 1.6},
               'rag': {'color': (0.03, 0.028, 0.026)}},
        parts=[
            dict(p='sores', count=30), dict(p='rope_crown'),
            dict(p='robe', hem=0.34, top=1.07, rag=0.10, girth=0.97, seed=9), dict(p='waist_roll', on='robe', z=1.04),
        ],
        weapons=[dict(w='torch', hand='r', name='torch_r'), dict(w='torch', hand='l', name='torch_l')],
        notes="Flagellant: bare-chested penitent, round burn scars, rope crown with thorn pegs, robe stripped to the waist, a snuffed "
              "torch stub in each fist (wpn_torch_r, wpn_torch_l)."),
    'mon_cult_assassin': dict(
        family='cult', scale=1.0, poses=["Sword_Idle:0.3", "Sword_Regular_Combo:0.4", "Sprint_Loop:0.3"], emis=(0.92, 0.96, 1.0),
        body=dict(type='flesh', decimate=0.62, show=['forearms', 'hands'], look='skin_ash', eyes=None),
        props=dict(bulk=0.86, hunch=10),
        looks={'wraps': BLACK_WRAP, 'glow': {'strength': 3.0, 'mottle': 0.05}, 'iron': {'color': (0.13, 0.13, 0.14), 'amount': 0.3}},
        parts=[
            dict(p='shell', look='wraps', regions=('torso', 'thighs', 'calves', 'feet', 'upperarms', 'neck', 'head'), offset=0.006, sink=False),
            dict(p='eye_slit'),
            dict(p='trails', look='shroudcloth', items=(('Head', (0.03, 0.085, 1.70), (0.3, 1.0, 0.1), 0.36, 0.05),
                                                       ('pelvis', (-0.12, 0.10, 1.0), (-0.3, 1.0, 0.0), 0.42, 0.055),
                                                       ('calf_l', (0.10, 0.055, 0.20), (0.3, 1.0, 0.0), 0.24, 0.04),
                                                       ('upperarm_r', (-0.44, 0.10, 1.44), (0.0, 1.0, -0.2), 0.26, 0.04))),
            dict(p='belt', z=1.04, w=0.026, look='strap', buckle=None, pad=0.01),
            dict(p='hang_item', item='pouch', bone='pelvis', at=(0.14, -0.085, 1.03), cord=0.0),
        ],
        weapons=[dict(w='sickle', hand='r', name='hook_r'), dict(w='sickle', hand='l', name='hook_l')],
        notes="Assassin: wound in black shroud strips from crown to ankle, bare forearms, two long snuffer-hooks (wpn_hook_r, wpn_hook_l); "
              "emis_ line across the eyes (the game hides it while stealthed)."),
    'mon_cult_censer': dict(
        family='cult', scale=1.0, poses=["Idle_Lantern_Loop:0.3", "Melee_Hook:0.5", "Walk_Loop:0.3"], emis=(0.75, 0.88, 1.0),
        body=dict(type='flesh', decimate=0.8, show=['hands', 'head', 'neck'], look='skin_ash', eyes=None),
        looks={'robe': ['linen', {'color': (0.56, 0.54, 0.49), 'hem_lo': 0.04, 'hem_hi': 0.55}], 'linen': {'color': (0.60, 0.58, 0.53)},
               'glow': {'strength': 3.2, 'mottle': 0.3}},
        parts=[
            dict(p='robe', girth=1.04, flare=1.12, seed=7), dict(p='sleeves', r_cuff=0.17, droop=0.15, r_el=0.085),
            dict(p='fork_mitre'),
            dict(p='stole', look='shroudcloth', y=-0.172, x=0.085, length=0.85, z=1.45, collar=False),
            dict(p='capelet', look='shroudcloth', z_bot=1.43, r_bot=(0.235, 0.18), rag=0.01),
            dict(p='belt', z=1.06, on='robe', look='silver', buckle=None, w=0.03, pad=0.012, bone='spine_01'),
            dict(p='hang_item', item='censer', bone='pelvis', at=(-0.2, -0.04, 1.045), cord=0.10, size=1.0, cord_look='silver'),
            dict(p='foot_wraps'),
        ],
        weapons=[dict(w='censer', size=1.75, chain=0.44)],
        notes="Censer-deacon: white linen robe with wide sleeves, tall fork-pronged mitre with veils, dark stole, a head-sized censer "
              "on a chain (wpn_censer) and a second one at the hip; emis_ glow in both censers."),

    # ------------------------------------------------------------------------------------------ wraith
    'mon_wraith_mourner': dict(
        family='wraith', scale=1.1, poses='zombie', emis=(0.35, 0.72, 1.0),
        body=dict(type='shroud', fingers=1.9, claws=0.11, decimate=0.6),
        props=dict(chest=0.80, waist=0.74, neck=0.7, arm=0.6, forearm=0.58, arm_len=1.14, hunch=24, head_up=-0.5),
        looks={'glow': {'strength': 2.0, 'mottle': 0.4}, 'claw': {'color': (0.50, 0.56, 0.56), 'stain': (0.10, 0.16, 0.18), 'crack_amt': 0.25, '_slot': 'skin'},
               'rust': {'amount': 0.75, 'color': (0.17, 0.17, 0.18), 'rough': 0.5}},
        parts=[
            dict(p='shroud_gown'), dict(p='veil'),
            dict(p='sleeves', look='shroud', r_sh=0.066, r_el=0.058, r_cuff=0.064, droop=0.01, x1=0.50, lining=False, rag=0.08),
            dict(p='glow_blob', at=(0.0, -0.127, 1.33), r=(0.036, 0.012, 0.052)),
            dict(p='shackle', side='r'),
        ],
        notes="Mourner: no cowl -- a long veil of hair and shroud hangs over a bowed head; a narrow shrouded column bent like a crook, "
              "arms 1.14x long with fingers stretched to twice their length and claws, a broken chain on the right wrist, dim emis_ glow in the chest. "
              "Legless; the winding-sheet reaches the floor."),
}
