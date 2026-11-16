"""Stage 1 specs: variants of the frostbound (frost), the sand revenant (sands) and the fiend (fiend family)."""

FROST = dict(type='from', module='mon_frostbound', drop=[dict(obj='wpn_spear'), dict(look='ice')])
SAND = dict(type='from', module='mon_sand_revenant', drop=[dict(obj='wpn_sabre')])
FIEND_LOOKS = {'tooth': {'color': (0.30, 0.25, 0.2)}}

SPECS = {
    # ------------------------------------------------------------------------------------------ frost
    'mon_rime_horn': dict(
        family='frost', scale=1.0, poses=["Idle_Loop:0.3", "Spell_Simple_Idle_Loop:0.5", "Walk_Loop:0.3"], emis=(0.35, 0.72, 1.0),
        body=dict(FROST), dress={'frost': 0.55},
        looks={'glow': {'strength': 3.0, 'mottle': 0.5, 'base_mul': 0.7}, 'madder': {'color': (0.22, 0.07, 0.05), 'hem_lo': -2, 'hem_hi': -1}},
        parts=[
            dict(p='signal_horn', trim='iron'),
            dict(p='back_pole', top='flag', flag_look='madder', z1=2.3, top_look='iron'),
            dict(p='crystals', bone='Head', at=(0.0, -0.20, 1.55), axis=(0.2, -0.6, -1.0), count=4, h=(0.04, 0.09), r=(0.012, 0.02), look='ice2'),
            dict(p='icicles', bone='Head', a=(0.0, -0.62, 1.66), b=(0.0, -0.66, 1.92), count=5, length=0.10),
        ],
        notes="Horn-blower: frostbound signaller with a two-metre curved signal horn frozen to his lips (rigid on Head, sweeping "
              "forwards and up over the head as a crescent), a tattered guidon pole strapped to his back; emis_ frost in the bell."),
    'mon_rime_sentry': dict(
        family='frost', scale=1.1, poses=["Sword_Idle:0.3", "Sword_Attack:0.45", "Walk_Loop:0.3"], emis=(0.45, 0.78, 1.0),
        body=dict(FROST, cut=['upperarm_l', 'lowerarm_l', 'hand_l']), dress={'frost': 0.55},
        looks={'glow': {'strength': 2.6, 'mottle': 0.2}},
        parts=[
            dict(p='ice_block'),
            dict(p='arm_shield', side='r'),
        ],
        weapons=[dict(w='axe', hand='r', length=0.5, look='iron')],
        notes="Frozen sentry: steps out of his ice pillar still wearing half of it -- a slab encases the head and the whole left side "
              "(the left arm is removed, the slab is rigid on spine_03), a round shield is frozen to the right forearm, short axe "
              "(wpn_axe) in the right hand; emis_ fracture lights in the slab."),

    # ------------------------------------------------------------------------------------------ sands
    'mon_dune_disc': dict(
        family='sands', scale=1.0, poses='thrower', emis=(0.55, 0.28, 1.0),
        body=dict(SAND),
        looks={'glow': {'strength': 2.4, 'mottle': 0.1}},
        parts=[
            dict(p='bangles', bone='lowerarm_l', look='blade', edge='glow'),
            dict(p='back_pole', top='sun', top_look='brass', look='brass', z1=2.02, R=0.31, lean=0.03),
        ],
        weapons=[dict(w='disc', look='blade', edge='glow')],
        notes="Disc-guard: five razor rings stacked on the left forearm, one more on the right hand (wpn_disc), a fan-shaped brass "
              "sun-plate on a pole behind the head; emis_ violet edges on the rings."),
    'mon_dune_mirror': dict(
        family='sands', scale=1.05, poses='shield', emis=(0.75, 0.6, 1.0),
        body=dict(SAND),
        parts=[
            dict(p='skirt_plates', look='brass', tiers=3, count=13),
            dict(p='mask', look='brass', slit=None, y=-0.128, z=1.662, w=0.068, h=0.102),
        ],
        weapons=[dict(w='shield', hand='l', shape='round', wd=0.80, dome=0.07, look='brass', rim='brass', face='mirror', boss=False),
                 dict(w='spear', hand='r', back=0.35, front=0.70, head=0.28, look='blade')],
        notes="Mirror-guard: scale skirt, blank brass face-mask, a round convex mirror shield as tall as the torso (wpn_shield on hand_l, "
              "polished low-roughness brass) and a short stabbing spear (wpn_spear)."),
    'mon_dune_stormjar': dict(
        family='sands', scale=1.0, poses='caster', emis=(0.75, 0.6, 1.0),
        body=dict(SAND), post=dict(hunch=26),
        looks={'glow': {'strength': 3.2, 'mottle': 0.4}},
        parts=[
            dict(p='jar_back'), dict(p='arm_wires'), dict(p='finger_caps', look='copper', length=0.03),
        ],
        notes="Storm-jar bearer: bent forward under a sealed brass jar taller than his head, crowned with three splayed copper rods; "
              "copper wires run down both arms to thimbled fingers; emis_ sparks on the rod tips and a glowing band on the jar."),
    'mon_dune_masker': dict(
        family='sands', scale=1.0, poses=["Idle_Loop:0.3", "Sword_Regular_A:0.55", "Walk_Loop:0.3"], emis=(0.7, 0.35, 1.0),
        body=dict(SAND), post=dict(hunch=12),
        looks={'glow': {'strength': 3.0, 'mottle': 0.5}},
        parts=[
            dict(p='apron'),
            dict(p='back_pole', top='masks', count=6, z1=2.52, top_look='brass', look='brass'),
            dict(p='mask', look='brass', slit=None, y=-0.128, z=1.662, w=0.068, h=0.102),
        ],
        weapons=[dict(w='hammer', hand='r', length=0.30), dict(w='tongs', hand='l', held='mask', held_look='glow')],
        notes="Mask-maker: leather apron over the wrappings, a pole on the back threaded with six blank brass masks, his own face a "
              "mask with no eye-holes, a small hammer (wpn_hammer) and tongs holding a violet-hot mask (wpn_tongs, emis_)."),

    # ------------------------------------------------------------------------------------------ fiend
    'mon_imp': dict(
        family='fiend', scale=0.6, poses='thrower', emis=(1.0, 0.3, 0.04),
        body=dict(type='flesh', decimate=0.6, look='skin_char', jaw=12.0, eyes='ember', eye_r=0.012, claws=0.035),
        props=dict(head=1.42, belly=0.55, leg_len=0.86, hands=1.5, arm=0.92, neck=1.1, hunch=10, chest=0.95, leg=1.05),
        looks=dict(FIEND_LOOKS),
        parts=[
            dict(p='horns', style='nub', length=0.055, r=0.024),
            dict(p='tail'),
            dict(p='cracks', count=14, seed=4),
        ],
        weapons=[dict(w='coal')],
        notes="Cinderling: pot-bellied big-headed spark-child, nub horns, oversized hands, a glowing coal in the right fist (wpn_coal), "
              "a tail longer than the body held up with a live ember at its tip."),
    'mon_fiend_caller': dict(
        family='fiend', scale=1.2, poses='caster', emis=(1.0, 0.3, 0.04),
        body=dict(type='flesh', decimate=0.6, look='skin_char', gaunt=1.3, jaw=10.0, eyes='ember', claws=0.10, fingers=1.5),
        props=dict(bulk=0.82, waist=0.74, chest=0.9, shoulders=0.86, arm_len=1.14, leg_len=1.06, neck_len=1.4, neck=0.85, hunch=12, arm=0.66, leg=0.74),
        looks=dict(FIEND_LOOKS),
        parts=[
            dict(p='horn_ring'),
            dict(p='skirt_plates', kind='slag', look='slab', tiers=2, count=9, h=0.2, z=1.0),
            dict(p='cracks', count=24, seed=7),
        ],
        notes="Ember-caller: tall starved fiend whose horns close into a ring above the head with a flame held in it (emis_), narrow "
              "shoulders, arms to the knees, a skirt of hanging slag plates. Carries nothing."),
}
