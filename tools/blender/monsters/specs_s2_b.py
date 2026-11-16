"""Stage 2 specs (part B): bases whose body is a shell around the hidden reference body (bell, brass, wood, ice, fungus)."""

SPECS = {
    'mon_cf_spirit': dict(
        family='coldfire', scale=1.1, poses='caster', emis=(0.45, 0.8, 1.0),
        body=dict(type='flesh', show=[], decimate=0.5),
        looks={'ember': {'emit': (1.0, 0.45, 0.08), 'strength': 3.0, 'mottle': 0.3}},
        parts=[dict(p='flame_spirit')],
        notes="Cold-flame spirit: a frozen flame with a torso -- an inverted cone of blue glass balanced on its point (it touches the "
              "floor), the rim flaring upward into tongues, arms that are single flame-blades (skinned along the arm bones), a tall "
              "split flame-tongue for a head, emis_ orange core in the chest. No legs."),
    'mon_char_stump': dict(
        family='char', scale=0.75, poses=["Zombie_Idle_Loop:0.3", "Punch_Jab:0.5", "Zombie_Walk_Fwd_Loop:0.3"], emis=(1.0, 0.4, 0.06),
        body=dict(type='flesh', show=[], decimate=0.5),
        props=dict(leg_len=0.88),
        looks={'ember': {'strength': 2.2, 'mottle': 0.6}},
        parts=[dict(p='stump_body')],
        notes="Stump: a sawn-off charred stump with a perfectly flat axe-cut top (growth rings, emis_ ember line round the cut), "
              "two legs splitting into four splayed root-toes, two short broken branches for arms. No head."),
    'mon_char_walker': dict(
        family='char', scale=1.15, poses='heavy', emis=(1.0, 0.4, 0.06),
        body=dict(type='flesh', show=[], decimate=0.5),
        props=dict(leg_len=1.08, arm_len_l=1.4, lean=5),
        looks={'ember': {'strength': 2.4, 'mottle': 0.5}},
        parts=[dict(p='trunk_body')],
        notes="Charwood walker: tall thin burnt trunk split into two legs, a jagged lightning-split crown instead of a head, the left "
              "arm a long forked branch (1.4x), the right snapped short; emis_ ember glow in the trunk split."),
    'mon_lamp_bearer': dict(
        family='lamp', scale=1.0, poses=["Idle_Lantern_Loop:0.3", "Sword_Attack:0.45", "Walk_Loop:0.3"], emis=(1.0, 0.62, 0.18),
        body=dict(type='flesh', decimate=0.62, show=['head', 'neck', 'forearms', 'hand_l', 'calves', 'feet'], look='skin', eyes='void', gaunt=1.0),
        props=dict(hunch=14, head_roll=-16),
        looks={'rag': {'color': (0.085, 0.075, 0.06), 'stain_amt': 0.9}, 'glow': {'strength': 3.4, 'mottle': 0.3}},
        parts=[
            dict(p='robe', look='rag', hem=0.26, rag=0.22, girth=1.0, seed=8), dict(p='sleeves', look='rag', x1=0.50, rag=0.07, r_cuff=0.08),
            dict(p='poncho', look='rag', seed=5, rag=0.3, point=0.12, holes=0.2),
            dict(p='hood_down', look='rag'),
            dict(p='lamp_arm', side='r'), dict(p='shelf_fungus', bones=('upperarm_l', 'lowerarm_l'), count=5),
            dict(p='head_bracket', side=1),
            dict(p='rope_belt', tail=0.25),
        ],
        notes="Lamp-bearer: infected traveller in a rotted cloak; the right forearm is a stalk and the hand a glowing lantern-shaped "
              "fungus cap (emis_ gills; the clips raise that arm), the left arm is sheathed in shelf fungus, the head tilts under a "
              "bracket of mushrooms."),
    'mon_engine_winder': dict(
        family='engine', scale=0.7, poses=["Idle_Loop:0.3", "Interact:0.5", "Jog_Fwd_Loop:0.3"], emis=(0.6, 0.3, 1.0),
        body=dict(type='flesh', show=[], decimate=0.5),
        looks={'glow': {'strength': 3.0, 'mottle': 0.1}},
        parts=[dict(p='pot_body'), dict(p='stick_limbs'), dict(p='key_head')],
        weapons=[dict(w='key', tilt=38.0)],
        notes="Winder: child-sized brass attendant -- a round riveted pot for a body, stick limbs with ball joints, a butterfly winding "
              "key instead of a head, a second bigger key carried in the right hand (wpn_key); emis_ violet pilot light in the chest."),
    'mon_bell_hood': dict(
        family='bell', scale=1.05, poses=["Idle_Loop:0.3", "Shield_Dash:0.5", "Sprint_Loop:0.3"], emis=(0.85, 0.9, 1.0),
        body=dict(type='flesh', decimate=0.62, show=['forearms', 'hands', 'pelvis', 'thighs', 'calves', 'feet'], look='skin_ash', eyes=None),
        props=dict(lean=10),
        looks={'glow': {'strength': 1.2, 'mottle': 0.0}},
        parts=[
            dict(p='body_bell'),
            dict(p='shell', look='trousers', regions=('pelvis', 'thighs', 'calves'), z0=0.2, offset=0.011, sink=False),
            dict(p='foot_wraps', look='trousers', z=0.24),
            dict(p='bracer', side='both', look='strap', r=0.05),
        ],
        notes="Bell-hooded: a bronze bell locked down over head and shoulders to the elbows (rigid on spine_03), padlocked iron strap "
              "between the legs, frayed bell-rope trailing from the crown; only forearms and legs show; faint emis_ rim on the lip."),
}
