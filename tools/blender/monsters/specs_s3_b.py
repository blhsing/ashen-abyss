"""Stage 3 specs (part B): variants of the stage-2 bases (ash, effigy, mire, lamp, wax, forge, hush)."""

SPECS = {
    'mon_ash_weeper': dict(
        family='ash', scale=1.0, poses='caster', emis=(1.0, 0.22, 0.06),
        body=dict(type='flesh', decimate=0.62, look='ashcrust', eyes=None, melt=2, jaw=34.0, teeth=False, hide=['hands', 'calves', 'feet']),
        props=dict(bulk=0.94, head_pitch=-26, pose={'upperarm_l': dict(rot=[('y', -112)]), 'upperarm_r': dict(rot=[('y', 112)]), 'spine_03': dict(rot=[('x', -8)])}),
        looks={'ember': {'strength': 1.8, 'mottle': 0.6}},
        parts=[
            dict(p='ash_mound'),
            dict(p='glow_blob', bone='Head', at=(0.0, -0.10, 1.592), r=(0.018, 0.012, 0.016), look='ember'),
            dict(p='fixed_claw', bone='lowerarm_l', at=(0.735, 0.065, 1.44), n=2, length=0.05, r=0.02, look='ashcrust', side=1, droop=0.0),
            dict(p='fixed_claw', bone='lowerarm_r', at=(-0.735, 0.065, 1.44), n=2, length=0.05, r=0.02, look='ashcrust', side=-1, droop=0.0),
        ],
        notes="Ash-weeper: the cast of someone who died begging the sky -- the legs are fused into a mound of ash (pelvis-weighted "
              "skirt to the floor), the arms are baked 112 deg up at the shoulder joints so the idle reads as a Y, the hands "
              "crumbled to stumps, face tilted up with a hollow mouth (emis_ sparks inside)."),
    'mon_effigy_orant': dict(
        family='effigy', scale=1.1, poses='caster', emis=(0.45, 1.0, 0.5),
        body=dict(type='flesh', show=[], decimate=0.5),
        looks={'glow': {'strength': 3.0, 'mottle': 0.2}},
        parts=[dict(p='stone_lady')],
        notes="Praying effigy: a robed stone lady -- the gown meets the floor as a fluted column, sleeves and hands are one carved "
              "praying block at the chest (rigid on spine_03; the real arms are not modelled), tall pointed headdress with a stiff "
              "pleated stone veil to the hips, emis_ light between the palms."),
    'mon_effigy_warden': dict(
        family='effigy', scale=1.05, poses=["Sword_Idle:0.3", "Sword_Dash:0.5", "Walk_Loop:0.3"], emis=(0.45, 1.0, 0.5),
        body=dict(type='flesh', decimate=0.45, look='limestone', eyes=None),
        props=dict(bulk=0.88, arm=0.85, leg=0.88),
        looks={'glow': {'strength': 2.8, 'mottle': 0.1}},
        parts=[
            dict(p='stone_armour', visor='glow'), dict(p='niche_canopy'), dict(p='plinth_foot', side='l'),
        ],
        weapons=[dict(w='pole', head='halberd', look='limestone', back=0.75, front=1.45, r=0.02, name='halberd')],
        notes="Niche-warden: slender guardian statue with the pointed stone canopy of its niche still fused above the head like a tall "
              "mitre (rigid on Head), a fragment of plinth on the left foot, a stone halberd (wpn_halberd); emis_ eye-slot."),
    'mon_bog_netter': dict(
        family='mire', scale=1.0, poses='thrower',
        body=dict(type='flesh', kind='female', decimate=0.62, look='skin_bog', eyes='void', gaunt=0.5),
        props=dict(bulk=1.12, hips=1.1, hunch=22, belly=0.12),
        parts=[
            dict(p='reed_kilt', length=0.5), dict(p='shell', look='rag', regions=('torso',), z0=1.18, z1=1.46, noise=0.03, offset=0.012),
            dict(p='cast_net'), dict(p='creel'),
            dict(p='shell', look='rag', regions=('head',), z0=1.69, offset=0.01, sink=False),
        ],
        notes="Net-caster: broad stooped marsh-woman with a huge cast-net draped over the left shoulder and trailing behind like a train "
              "(stone sinkers on the ground bound to the root bone), a lidded creel basket on her back, cork floats round the neck."),
    'mon_bog_sleeper': dict(
        family='mire', scale=1.0, poses='zombie',
        body=dict(type='flesh', decimate=0.66, look='skin_bog', eyes='void', gaunt=0.3, fingers=1.5, claws=0.03),
        props=dict(bulk=1.1, belly=0.38, hunch=18, arm=0.9, neck=1.15),
        looks={'skin_bog': {'color': (0.42, 0.43, 0.38), 'blotch': (0.2, 0.22, 0.2), 'feet_dirt': 1.0, 'feet_hi': 1.32, 'dirt': (0.022, 0.018, 0.014), 'rough': 0.35}},
        parts=[dict(p='reed_straw'), dict(p='back_slab'),
               dict(p='shell', look='peatskin', regions=('thighs', 'calves', 'feet', 'pelvis', 'spine_01', 'spine_02'), z1=1.24, noise=0.07, freq=7.0, offset=0.004)],
        notes="Peat-sleeper: naked and swollen, peat-black to the chest and corpse-pale above, a long reed breathing-straw bound to the "
              "mouth and sticking straight up past the head, a slab of peat stuck to the back; stretched fingers."),
    'mon_lamp_bomber': dict(
        family='lamp', scale=1.0, poses='thrower', emis=(1.0, 0.62, 0.18),
        body=dict(type='flesh', decimate=0.62, show=['forearms', 'hands', 'calves', 'feet'], look='skin', eyes=None),
        props=dict(bulk=1.12, hunch=20, leg_len=0.92),
        looks={'rag': {'color': (0.085, 0.075, 0.06), 'stain_amt': 0.9}, 'glow': {'strength': 3.2, 'mottle': 0.3}},
        parts=[
            dict(p='robe', look='rag', hem=0.30, rag=0.2, girth=1.04, seed=12), dict(p='sleeves', look='rag', x1=0.50, rag=0.07, r_cuff=0.085),
            dict(p='pagoda_head'), dict(p='puffballs'), dict(p='rope_belt', tail=0.2),
        ],
        weapons=[dict(w='coal', r=0.07, name='pod', look='pod')],
        notes="Spore-thrower: squat infected whose back and shoulders carry a cluster of puffball pods (emis_ pinpoints), a pagoda of "
              "bracket fungus where the head was, a pod ready in the right hand (wpn_pod)."),
    'mon_wax_wick': dict(
        family='wax', scale=1.0, poses=["Zombie_Idle_Loop:0.3", "Zombie_Scratch:0.45", "Sprint_Loop:0.3"], emis=(1.0, 0.72, 0.30),
        body=dict(type='flesh', decimate=0.8, show=['forearms', 'hands', 'head', 'neck'], look='skin_ash', eyes='void', gaunt=1.2, jaw=24.0),
        looks={'robe': ['linen', {'color': (0.40, 0.37, 0.31), 'hem_lo': 0.1, 'hem_hi': 0.7}]},
        parts=[
            dict(p='robe', hem=0.12, girth=1.0, seed=3, rag=0.08), dict(p='sleeves', x1=0.50, r_cuff=0.085, droop=0.02),
            dict(p='wax_dome', melted='l', drips=14), dict(p='wax_drips'), dict(p='flame'), dict(p='rope_belt'), dict(p='foot_wraps', look='robe'),
        ],
        notes="Lit wick: a wax-sealed one already alight -- the dome has half melted away on the left so a bare jaw shows, wax runs off "
              "in ropes, a tall guttering emis_ flame streams back from the wick."),
    'mon_wax_chandler': dict(
        family='wax', scale=1.0, poses=["Idle_Loop:0.3", "OverhandThrow:0.3", "Walk_Loop:0.3"], emis=(1.0, 0.5, 0.15),
        body=dict(type='flesh', decimate=1.0, show=['hands'], look='skin_ash', eyes=None),
        props=dict(belly=0.2),
        looks={'apron': {'color': (0.06, 0.045, 0.035)}, 'hood': ['hide', {'color': (0.07, 0.05, 0.04)}], 'ember': {'strength': 1.4}},
        parts=[
            dict(p='robe', seed=5), dict(p='sleeves'), dict(p='hood'), dict(p='mask', slit=None),
            dict(p='apron', pad=0.05, z=1.34, length=0.8), dict(p='cauldron_front'), dict(p='wick_bundle'), dict(p='foot_wraps'),
        ],
        weapons=[dict(w='ladle')],
        notes="Wax-chandler: an Order lay-brother in a scorched leather apron and hood with a small iron cauldron of molten wax on a "
              "chest harness over a covered coal (dull emis_ glow), a bundle of blank wicks at the belt, a long ladle (wpn_ladle)."),
    'mon_forge_tongs': dict(
        family='forge', scale=1.05, poses=["Sword_Idle:0.3", "Melee_Hook:0.5", "Walk_Loop:0.3"], emis=(1.0, 0.42, 0.08),
        body=dict(type='flesh', decimate=0.6, look='skin_ash', eyes=None, hide=['lowerarm_r', 'hand_r', 'head']),
        props=dict(bulk=0.9, hunch=10),
        looks={'skin_ash': {'color': (0.20, 0.185, 0.175), 'hands_dark': 0.9, 'dark': (0.02, 0.02, 0.02)}, 'ember': {'strength': 2.2},
               'apron': {'color': (0.05, 0.04, 0.032)}},
        parts=[
            dict(p='iron_hood'), dict(p='tong_arm', side='r'), dict(p='chain_wrap', bones=('upperarm_l', 'lowerarm_l')),
            dict(p='apron', length=0.75), dict(p='shell', look='trousers', regions=('thighs', 'calves', 'pelvis'), z1=1.05, offset=0.01, sink=False),
        ],
        notes="Tongs-thrall: lean thrall in a riveted iron hood with one round grille; the right arm from the elbow is a pair of "
              "blacksmith's tongs 1.35 m long with open jaws (rigid on lowerarm_r, emis_ jaw tips), the left arm wound in chain, "
              "leather apron."),
    'mon_forge_pourer': dict(
        family='forge', scale=1.05, poses=["Sword_Idle:0.3", "OverhandThrow:0.3", "Walk_Loop:0.3"], emis=(1.0, 0.36, 0.05),
        body=dict(type='flesh', decimate=0.6, look='skin_ash', eyes='void', gaunt=0.9),
        props=dict(hunch=22),
        looks={'skin_ash': {'color': (0.20, 0.185, 0.175), 'hands_dark': 0.9, 'dark': (0.02, 0.02, 0.02)}, 'hood': ['hide', {'color': (0.05, 0.04, 0.035)}]},
        parts=[
            dict(p='crucible_head'), dict(p='hood', void=False, tail=0.05, tail_drop=0.04),
            dict(p='capelet', look='hood', z_bot=1.40, r_bot=(0.25, 0.19)),
            dict(p='apron', length=0.75), dict(p='shell', look='trousers', regions=('thighs', 'calves', 'pelvis'), z1=1.05, offset=0.01, sink=False),
        ],
        weapons=[dict(w='ladle', length=1.25, bowl=0.07, look='iron', glow='slag')],
        notes="Slag-pourer: thrall bent under a crucible of molten slag carried on his shoulders in an iron collar-frame directly "
              "above his head (rigid on spine_03, emis_ slag surface and drip), scorched hood, a long dipper (wpn_ladle)."),
    'mon_hush_mouth': dict(
        family='hush', scale=1.1, poses='caster', float=0.025,
        body=dict(type='flesh', decimate=0.62, look='chalk', eyes=None, melt=7, fingers=2.0,
                  hide=['head', 'spine_01', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r']),
        props=dict(bulk=0.78, arm=0.72, leg=0.80, neck=0.7, leg_len=1.06),
        parts=[dict(p='hoop_torso'), dict(p='slot_head')],
        notes="Sound-eater: a pale figure whose torso is a hoop -- from the collarbone to the hips only a wide round hole ringed by a "
              "smooth rim (skinned by height to the spine), the slotted oval head on top, the legs below. No emissive (the game "
              "fills the hole)."),
}
