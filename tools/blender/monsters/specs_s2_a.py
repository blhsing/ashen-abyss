"""Stage 2 specs (part A): human-sized new bases built from the base body + library parts."""

SPECS = {
    'mon_reaver_cutter': dict(
        family='reaver', scale=0.95, poses='punch',
        body=dict(type='flesh', decimate=0.62, show=['head', 'neck', 'hands'], look='skin_live', head_look='ashpaint', eyes='void', gaunt=0.5),
        props=dict(bulk=0.92, hunch=10),
        parts=[
            dict(p='robe', look='coat', hem=0.62, top=1.50, girth=0.97, rag=0.035, pleat=0.035, seed=5, crumple=0.006),
            dict(p='sleeves', look='coat', r_cuff=0.066, droop=0.008, r_sh=0.085, r_el=0.07),
            dict(p='patches'),
            dict(p='shell', look='trousers', regions=('thighs', 'calves'), offset=0.012, sink=False),
            dict(p='shell', look='strap', regions=('feet', 'calves'), z1=0.34, offset=0.016, sink=False),
            dict(p='shell', look='hide', regions=('head',), z0=1.705, offset=0.008, sink=False),
            dict(p='belt', on='robe', z=1.06, bone='spine_01', pad=0.008),
            dict(p='bandolier', on='robe', z=1.30, tilt=-30.0, items='lanterns', count=5, pad=0.014),
        ],
        weapons=[dict(w='shear', hand='r', name='shear_r'), dict(w='shear', hand='l', name='shear_l')],
        notes="Wick-cutter: small quick raider in a short patched leather coat, face smeared white with ash, leather skullcap, a string "
              "of thumb-sized dead lanterns across the chest, one long shear blade in each hand (wpn_shear_r, wpn_shear_l)."),
    'mon_effigy_knight': dict(
        family='effigy', scale=1.1, poses='heavy', emis=(0.45, 1.0, 0.5),
        body=dict(type='flesh', decimate=0.45, look='limestone', eyes=None),
        looks={'glow': {'strength': 1.3, 'mottle': 0.5}},
        parts=[dict(p='stone_armour'), dict(p='tomb_slab')],
        weapons=[dict(w='sword', length=1.0, width=1.35, notched=False, look='limestone', hilt='limestone', grip='limestone')],
        notes="Tomb-knight: a carved limestone knight (great helm, harness) who stood up with half his tomb lid still fused to his back "
              "from the heels to above the head (rigid on spine_02), carved pillow behind the helm, emis_ inscription along the slab "
              "edges; stone sword (wpn_sword)."),
    'mon_bog_fisher': dict(
        family='mire', scale=1.0, poses=["Sword_Idle:0.3", "Sword_Dash:0.5", "Walk_Loop:0.3"],
        body=dict(type='flesh', decimate=0.62, look='skin_bog', eyes=None, gaunt=0.6),
        props=dict(bulk=0.86, arm=0.82, leg=0.84, hunch=14),
        looks={'iron': {'amount': 0.8}},
        parts=[dict(p='reed_kilt'), dict(p='trap_helm'), dict(p='eel_string')],
        weapons=[dict(w='pole', head='trident', look='iron', back=0.85, front=1.25, name='eelspear')],
        notes="Eel-fisher: lean slick-skinned marsh-man in a reed kilt, a conical wicker fish-trap over the whole head (point up), "
              "a string of dead eels at the belt, a three-pronged barbed eel-spear taller than himself (wpn_eelspear)."),
    'mon_wax_sealed': dict(
        family='wax', scale=1.0, poses='zombie',
        body=dict(type='flesh', decimate=0.9, show=['forearms', 'hands'], look='skin_ash', eyes=None),
        looks={'robe': ['linen', {'color': (0.40, 0.37, 0.31), 'hem_lo': 0.1, 'hem_hi': 0.7}]},
        parts=[
            dict(p='robe', hem=0.12, girth=1.0, seed=3, rag=0.04), dict(p='sleeves', x1=0.50, r_cuff=0.085, droop=0.02),
            dict(p='wax_dome'), dict(p='wax_drips'), dict(p='rope_belt'), dict(p='foot_wraps', look='robe'),
        ],
        notes="Wax-sealed: a citizen in a long smock whose head and shoulders are hidden under a smooth dome of poured tallow with a "
              "wick on the crown (rigid on spine_03); wax stalactites on forearms, elbows and fingertips. Unlit: no emissive "
              "(the game adds the flame when it is set alight)."),
    'mon_forge_bellows': dict(
        family='forge', scale=1.05, poses=["Idle_Loop:0.3", "Spell_Simple_Idle_Loop:0.5", "Walk_Loop:0.3"], emis=(1.0, 0.42, 0.08),
        body=dict(type='flesh', decimate=0.6, look='skin_ash', eyes='void', gaunt=0.9),
        props=dict(hunch=10, waist=0.9),
        looks={'skin_ash': {'color': (0.20, 0.185, 0.175), 'hands_dark': 0.9, 'dark': (0.02, 0.02, 0.02)}, 'ember': {'strength': 2.6}},
        parts=[
            dict(p='bellows_chest'),
            dict(p='shell', look='trousers', regions=('thighs', 'calves', 'pelvis'), z1=1.05, offset=0.01, sink=False),
            dict(p='rags', look='coat', arcs=((-140, -40, 0.45), (40, 140, 0.4)), z=1.03, rx=0.165, ry=0.13),
            dict(p='belt', z=1.045, look='strap', buckle='iron', pad=0.014),
            dict(p='shell', look='hide', regions=('head',), z0=1.70, offset=0.008, sink=False),
        ],
        notes="Bellows-thrall: the chest is a great leather concertina bellows wider than the shoulders (rigid on spine_02) with two "
              "wooden handles sticking out sideways and an iron nozzle pipe rising in front of the face; emis_ glow in the nozzle."),
    'mon_ash_cast': dict(
        family='ash', scale=1.0, poses='zombie', emis=(1.0, 0.22, 0.06),
        body=dict(type='flesh', decimate=0.62, look='ashcrust', eyes=None, melt=2, jaw=14.0, teeth=False),
        props=dict(bulk=0.94),
        looks={'ember': {'strength': 1.6, 'mottle': 0.7}},
        parts=[
            dict(p='body_hole'), dict(p='open_skull'),
            dict(p='spike', bone='lowerarm_l', path=((0.47, 0.07, 1.40), (0.475, 0.07, 1.30), (0.48, 0.07, 1.16)), r=0.02, look='ashfall'),
            dict(p='spike', bone='lowerarm_r', path=((-0.47, 0.07, 1.40), (-0.475, 0.07, 1.30), (-0.48, 0.07, 1.20)), r=0.018, look='ashfall'),
        ],
        notes="Ash-cast: hollow human figure of grey crust, a ragged hole clean through the chest (lined, rimmed with emis_ sparks) and "
              "the top of the head broken away like an eggshell; streams of ash at the elbows."),
    'mon_hush_blank': dict(
        family='hush', scale=1.1, poses=["Idle_Loop:0.3", "Sword_Regular_A:0.55", "Walk_Loop:0.3"], float=0.025,
        body=dict(type='flesh', decimate=0.62, look='chalk', eyes=None, melt=7, fingers=2.0, hide=['head']),
        props=dict(bulk=0.78, arm=0.72, leg=0.80, neck=0.7, leg_len=1.06, shoulders=0.92),
        parts=[dict(p='slot_head')],
        notes="Blank attendant: narrow chalk-white figure with the anatomy melted smooth, fingers twice human length, the head a long "
              "vertical oval pierced by one see-through slot; hovers 2.5 cm above the floor. No emissive."),
    'mon_shroud_wrapped': dict(
        family='shroud', scale=1.0, poses='zombie',
        body=dict(type='flesh', decimate=0.8, show=['forearms', 'hands'], look='skin', eyes=None),
        props=dict(arm=0.8),
        parts=[dict(p='cocoon'), dict(p='threads')],
        notes="Cocooned corpse: a human cocoon of grey silk (bowling-pin shell, legs wound into one block, featureless oval head), "
              "only the forearms torn free with torn webs to the flanks, four taut threads rising from shoulders and head."),
}
