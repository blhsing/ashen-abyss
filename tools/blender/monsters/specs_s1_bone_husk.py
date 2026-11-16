"""Stage 1 specs: variants of the skeleton (bone family) and of the husk (husk family)."""

HUSK = dict(type='flesh', decimate=0.66, gaunt=1.2, jaw=20.0, ribs=True, knobs=True, claws=0.075, toe_claws=0.03)
HUSK_PROPS = dict(hips=0.86, waist=0.80, chest=0.88, depth=0.88, neck=0.7, arm=0.58, forearm=0.60, leg=0.60, calf=0.64, hunch=30,
                  pose={'clavicle_l': dict(rot=[('x', -20)]), 'clavicle_r': dict(rot=[('x', -20)]), 'Head': dict(scale=(0.93, 0.97, 1.0))})

SPECS = {
    # ------------------------------------------------------------------------------------------ bone
    'mon_bone_scrap': dict(
        family='bone', scale=0.7, poses=["Zombie_Idle_Loop:0.3", "Zombie_Scratch:0.45", "Sprint_Loop:0.3"],
        body=dict(type='skeleton', thick=1.25, jaw=22.0, eye='void', drop=['lowerarm_r', 'hand_r', 'hand_l']),
        props=dict(head=1.36, stoop=10, hunch=14, head_roll=14, forearm_len_l=1.55, leg_len=0.92, chest=0.86, hips=0.9),
        parts=[
            dict(p='spike', bone='upperarm_r', path=((-0.40, 0.07, 1.44), (-0.56, 0.045, 1.45), (-0.70, 0.0, 1.40), (-0.78, -0.06, 1.31)), r=0.017),
            dict(p='lashing', bone='upperarm_r', t0=0.60, t1=0.98, r=0.027, turns=5),
            dict(p='lashing', bone='lowerarm_l', t0=-0.05, t1=0.12, r=0.029, turns=3.5),
            dict(p='lashing', bone='neck_01', t0=0.1, t1=0.75, r=0.036, turns=3),
            dict(p='lashing', bone='calf_r', t0=-0.03, t1=0.10, r=0.035, turns=3),
            dict(p='lashing', bone='thigh_l', t0=0.35, t1=0.5, r=0.034, turns=3),
            dict(p='rags', look='rag', arcs=((-140, -55, 0.24),), z=1.0),
            dict(p='fixed_claw', bone='lowerarm_l', at=(0.74, 0.065, 1.44), n=3, length=0.19),
        ],
        notes="Scrapbone: child-sized mis-assembled skeleton (engine scale 0.7): oversized tilted skull, left forearm 1.55x long ending in a fixed hook of claws, right arm a "
              "stump with a rib lashed on as a spike, bent double, grave-cord bindings. No weapon (claws)."),
    'mon_bone_pallbearer': dict(
        family='bone', scale=1.05, poses='shield',
        body=dict(type='skeleton', thick=1.5, jaw=8.0, eye='void'),
        props=dict(chest=1.12, shoulders=1.08, hunch=8),
        looks={'linen': {'color': (0.30, 0.29, 0.27)}},
        parts=[
            dict(p='belt', z=1.043, on='body', pad=-0.006, look='strap', buckle='rust'),
            dict(p='rags', look='linen', arcs=((-150, -40, 0.42), (40, 140, 0.34)), z=1.03, rx=0.155, ry=0.112),
            dict(p='chest_strap', tilt=-26), dict(p='chest_strap', tilt=24, z=1.31),
            dict(p='lashing', bone='lowerarm_l', t0=0.25, t1=0.8, r=0.03, turns=5, look='strap', thick=0.009),
        ],
        weapons=[dict(w='shield', hand='l', shape='coffin', wd=0.62, ht=1.38, th=0.045, look='oak', rim='rust', cross=True, studs=16, boss=False,
                      offset=(-0.10, 0.14)),
                 dict(w='mattock', hand='r')],
        notes="Pallbearer: broad skeleton behind a coffin lid carried as a tower shield (wpn_shield on hand_l: hexagonal oak lid, iron "
              "cross-straps, bent nails), grave-digger's mattock (wpn_mattock) in the right hand."),
    'mon_bone_urnbearer': dict(
        family='bone', scale=1.0, poses=["Idle_Lantern_Loop:0.3", "Spell_Simple_Shoot:0.5", "Walk_Loop:0.3"], emis=(0.55, 1.0, 0.6),
        body=dict(type='skeleton', thick=1.1, jaw=6.0),
        props=dict(hunch=30),
        looks={'linen': {'color': (0.34, 0.33, 0.31)}, 'glow': {'strength': 3.0, 'mottle': 0.3}},
        parts=[
            dict(p='hood', style='rag', look='linen', void=False, seed=5),
            dict(p='stole', look='linen', length=0.95),
            dict(p='urn'),
            dict(p='hang_item', item='lid', bone='spine_03', at=(0.185, -0.20, 1.34), cord=0.12),
            dict(p='rags', look='linen', arcs=((-155, -25, 0.50), (25, 155, 0.56)), z=1.02, rx=0.15, ry=0.115, holes=0.2, seed=8),
            dict(p='belt', z=1.04, w=0.02, buckle=None, look='rope', pad=-0.012),
        ],
        notes="Urn-bearer: stooped skeleton in grave linen and a long funeral stole with a big glazed urn slung against its chest "
              "(rigid on spine_03, the arms animate freely around it); emis_ glow in the urn mouth, crack with a run of ash."),

    # ------------------------------------------------------------------------------------------ husk
    'mon_husk_chained': dict(
        family='husk', scale=1.05, poses=["Zombie_Idle_Loop:0.3", "Melee_Hook:0.5", "Zombie_Walk_Fwd_Loop:0.3"], emis=(1.0, 0.82, 0.35),
        body=dict(HUSK), props=dict(HUSK_PROPS),
        looks={'eye': {'strength': 2.5}},
        parts=[
            dict(p='rags', arcs=((-128, -52, 0.36), (60, 125, 0.30))),
            dict(p='collar', r=0.092, cy=-0.014),
            dict(p='chain', path=((0.0, -0.118, 1.49), (0.012, -0.135, 1.30), (0.0, -0.118, 1.07)), bone='spine_02', link=0.045, r=0.007),
            dict(p='waist_chain'),
            dict(p='drag_post'),
        ],
        notes="Chained husk: iron collar, chest chain and waist chain; a chain runs back from the waist to the ground and on to the "
              "torn-out tether post it drags (ground part bound to the root bone)."),
    'mon_husk_wailer': dict(
        family='husk', scale=1.0, poses=["Idle_Loop:0.3", "Spell_Simple_Idle_Loop:0.5", "Walk_Loop:0.3"], emis=(1.0, 0.82, 0.35),
        body=dict(HUSK, jaw=40.0, tooth_size=1.15),
        props=dict(HUSK_PROPS, hunch=-4, head_pitch=-30, neck_len=1.3, leg_len=1.06, pose={'neck_01': dict(rot=[('x', -10)]), 'Head': dict(scale=(0.93, 0.97, 1.0))}),
        looks={'eye': {'strength': 2.0}, 'iron': {'amount': 0.8}},
        parts=[
            dict(p='rags', arcs=((-150, -30, 0.46), (40, 150, 0.52)), seed=11),
            dict(p='collar', look='rope', r=0.092, cy=-0.014, w=0.014, ring=False, z=1.50),
            dict(p='hang_item', item='lantern', bone='spine_02', at=(0.0, -0.135, 1.47), cord=0.27, lit=False, size=0.9),
        ],
        notes="Wailer: a husk standing straight with a long neck, head thrown back and the jaw dislocated wide open; a dead lantern "
              "hangs on a cord from its neck to the belly. No glow besides the eyes."),
}
