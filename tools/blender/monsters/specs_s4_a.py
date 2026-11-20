"""Stage 4 specs (part A): fuel-bearer giants (base + 3 variants), raider brute, bell clapper.
Oversized creatures: the engine scale does the size, the mesh gives the mass (bulk) and the load."""

GIANT = dict(type='flesh', decimate=0.7, look='skin_bark', eyes='void', gaunt=0.4)
GIANT_PROPS = dict(bulk=1.3, chest=1.12, shoulders=1.1, head=0.80, neck=1.2, hunch=22, leg_len=0.92, leg=1.12, arm=1.12, hands=1.3, belly=0.1)
LOIN = [dict(p='rags', look='rag', arcs=((-150, -30, 0.5), (30, 150, 0.55)), z=1.02, rx=0.22, ry=0.17, holes=0.15, seed=4),
        dict(p='belt', z=1.04, look='strap', buckle='rust', pad=0.016, w=0.06)]

SPECS = {
    'mon_hauler_yoke': dict(
        family='hauler', scale=1.8, poses='brute', emis=(1.0, 0.42, 0.08),
        body=dict(GIANT), props=dict(GIANT_PROPS),
        parts=[dict(p='yoke'), dict(p='brand_ring')] + LOIN,
        notes="Yoke-bearer: soot-black giant bent under a shoulder-yoke as long as he is tall, a chained bundle of logs hung from "
              "each end (rigid on spine_03), small head pushed forward, short thick legs; emis_ Forge brand ring on the chest."),
    'mon_hauler_hod': dict(
        family='hauler', scale=1.8, poses='bomber', emis=(1.0, 0.42, 0.08),
        body=dict(GIANT), props=dict(GIANT_PROPS, hunch=22),
        parts=[dict(p='hod', bone='spine_01'), dict(p='brand_ring')] + LOIN,
        weapons=[dict(w='log')],
        notes="Hod-bearer: giant with a wicker-and-timber hod on his back rising a body-height above the head, stacked with tree "
              "trunks; a trunk ready in the right hand (wpn_log); emis_ brand ring."),
    'mon_hauler_collector': dict(
        family='hauler', scale=1.75, poses=["Idle_Loop:0.3", "Melee_Hook:0.5", "Walk_Loop:0.3"], emis=(1.0, 0.55, 0.15),
        body=dict(GIANT, hide=['lowerarm_r', 'hand_r']), props=dict(GIANT_PROPS),
        looks={'glow': {'strength': 1.4, 'mottle': 0.6, 'crust': 0.6}, 'patch': {'color': (0.11, 0.08, 0.055)}},
        parts=[dict(p='cage_back', bone='spine_02'), dict(p='tong_arm', side='r', length=1.25, glow=None, look='rust'),
               dict(p='shell', look='patch', regions=('upperarm_r', 'clavicle_r'), offset=0.02, sink=False),
               dict(p='brand_ring')] + LOIN,
        notes="Warmth-collector: giant with a domed open-weave wicker cage on his back (a heap of stolen bundles, faint emis_ glow between them) and the right arm wrapped in "
              "quilted hide, ending from the elbow in iron grabbing-tongs as long as a man (rigid on lowerarm_r); left hand empty."),
    'mon_hauler_stoker': dict(
        family='hauler', scale=1.85, poses='brute', emis=(1.0, 0.36, 0.05),
        body=dict(GIANT, look='skin_bark'), props=dict(GIANT_PROPS, hunch=14),
        looks={'skin_bark': {'color': (0.11, 0.075, 0.06), 'rough': 0.35, 'wrinkle': 1.2}, 'apron': {'color': (0.045, 0.04, 0.036)},
               'slag': {'strength': 1.6}, 'brand': {'emit': (1.0, 0.24, 0.03)}},
        parts=[dict(p='face_shield'), dict(p='apron', z=1.36, length=1.22, pad=0.02), dict(p='brand_ring')] + LOIN[1:],
        weapons=[dict(w='shovel', load='slag')],
        notes="Stoker: giant burnt hairless and glassy in a slag-crusted leather apron to the ankles and a riveted iron face-shield "
              "with one square window, a coal shovel with a door-sized blade heaped with glowing slag (wpn_shovel, emis_)."),
    'mon_reaver_brute': dict(
        family='reaver', scale=1.4, poses='brute',
        body=dict(type='flesh', decimate=0.7, look='skin_live', eyes=None, hide=['head']),
        props=dict(bulk=1.28, belly=0.5, chest=1.05, hunch=10, leg=1.05, arm=1.1),
        looks={'skin_live': {'scar': 1.8, 'sore_amt': 0.3}},
        parts=[dict(p='bowl_helm', look='iron_black'),
               dict(p='shell', look='trousers', regions=('thighs', 'calves', 'pelvis'), z1=1.02, offset=0.012, sink=False),
               dict(p='shell', look='strap', regions=('feet', 'calves'), z1=0.34, offset=0.016, sink=False),
               dict(p='rope_belt', on='body', z=1.03, pad=0.03, r=0.02, tail=0.3),
               dict(p='lashing', bone='lowerarm_r', t0=0.3, t1=0.9, r=0.062, turns=5, look='rope', thick=0.01),
               dict(p='lashing', bone='lowerarm_l', t0=0.3, t1=0.9, r=0.062, turns=5, look='rope', thick=0.01)],
        weapons=[dict(w='maul')],
        notes="Hearth-breaker: huge-bellied raider stripped to the waist, a beacon's brazier bowl worn upside-down as helmet and "
              "pauldrons in one piece (rigid on spine_03, eye-slot hacked in; no head modelled under it), rope-bound forearms, a maul "
              "made of a beacon door-ring on a timber (wpn_maul)."),
    'mon_bell_clapper': dict(
        family='bell', scale=1.45, poses='brute',
        body=dict(type='flesh', decimate=0.7, show=['head', 'neck', 'hands'], look='skin_ash', eyes='void', gaunt=0.3),
        props=dict(bulk=1.25, chest=1.08, hunch=12, neck=1.2, hands=1.2),
        parts=[dict(p='robe', look='padded', hem=0.42, top=1.50, girth=1.0, rag=0.02, pleat=0.03, seed=7, crumple=0.008, flare=1.05),
               dict(p='sleeves', look='padded', r_cuff=0.075, droop=0.01, r_sh=0.092, r_el=0.08),
               dict(p='ear_plugs'),
               dict(p='bandolier', items='coil', look='rope', on='robe', z=1.08, tilt=6.0, pad=0.035, bone='spine_01'),
               dict(p='shell', look='trousers', regions=('thighs', 'calves'), offset=0.012, sink=False),
               dict(p='shell', look='strap', regions=('feet', 'calves'), z1=0.34, offset=0.016, sink=False)],
        weapons=[dict(w='clapper')],
        notes="Clapper-hand: a huge bald ringer in a padded bell-founder's coat, ears sealed with fist-sized wax plugs, a coil of "
              "bell-rope round the waist, carrying the iron clapper of a great bell (wpn_clapper: tapering shaft with a ball)."),
}

SPECS.update({
    'mon_bone_amalgam': dict(
        family='bone', scale=1.45, poses='brute', emis=(0.45, 1.0, 0.55),
        body=dict(type='skeleton', thick=1.75, jaw=14.0),
        props=dict(chest=1.22, shoulders=1.3, hips=1.2, head=1.12, hunch=10),
        looks={'stone': {'color': (0.13, 0.135, 0.12), 'ash': (0.16, 0.22, 0.10), 'ash_amt': 0.5}, 'eye': {'strength': 3.0}},
        parts=[dict(p='amalgam'),
               dict(p='rags', look='rag', arcs=((-150, -40, 0.4), (40, 150, 0.45)), z=1.03, rx=0.19, ry=0.14),
               dict(p='belt', z=1.043, on='body', pad=0.03, look='rope', buckle=None, w=0.03)],
        weapons=[dict(w='headstone')],
        notes="Barrow amalgam: two skeletons stacked into one wide body -- doubled barrel ribcage, a second pair of small arms folded "
              "on the chest, three skulls across the shoulders (the middle one largest), a mossy headstone lashed to a timber post "
              "(wpn_headstone); three pairs of emis_ eye-lights."),
    'mon_hush_mute': dict(
        family='hush', scale=1.55, poses='brute', float=0.0,
        body=dict(type='flesh', decimate=0.62, look='chalk', eyes=None, melt=7,
                  hide=['head', 'neck', 'hands', 'spine_02', 'spine_03', 'clavicle_l', 'clavicle_r']),
        props=dict(bulk=1.3, shoulders=1.16, chest=1.12, arm=1.3, arm_len=1.15, hunch=10, leg=1.1),
        parts=[dict(p='mute_chest'), dict(p='mute_hands', size=0.42)],
        notes="The Mute: a wide sloping chalk-white giant with no head (the shoulders rise into a smooth dome), long arms ending in "
              "flat mitten-hands the size of shields, one ROUND hole clean through the slab of the chest (a see-through tunnel with pale walls; the "
              "chest is narrower than the shoulder joints so the arms hang free from big shoulder masses). Dry cracked chalk. No emissive."),
})
