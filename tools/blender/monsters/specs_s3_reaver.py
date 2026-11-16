"""Stage 3 specs: variants of the wick-cutter (reaver family) -- pure specs on the cutter's body recipe."""

BODY = dict(type='flesh', decimate=0.62, show=['head', 'neck', 'hands'], look='skin_live', head_look='ashpaint', eyes='void', gaunt=0.5)
LEGS = [dict(p='shell', look='trousers', regions=('thighs', 'calves'), offset=0.012, sink=False),
        dict(p='shell', look='strap', regions=('feet', 'calves'), z1=0.34, offset=0.016, sink=False)]

SPECS = {
    'mon_reaver_hook': dict(
        family='reaver', scale=1.0, poses=["Sword_Idle:0.3", "Sword_Dash:0.5", "Walk_Loop:0.3"],
        body=dict(BODY), props=dict(bulk=0.88, leg_len=1.06, neck_len=1.15, hunch=6),
        looks={'coat': {'color': (0.075, 0.07, 0.07)}, 'hide': {'color': (0.06, 0.05, 0.045)}, 'brass': {'color': (0.17, 0.13, 0.07), 'amount': 0.6}},
        parts=[
            dict(p='robe', look='coat', hem=0.30, top=1.50, girth=0.95, rag=0.05, pleat=0.05, seed=11, crumple=0.006),
            dict(p='sleeves', look='coat', r_cuff=0.066, droop=0.008, r_sh=0.085, r_el=0.07),
            dict(p='capelet', look='coat', z_top=1.625, z_bot=1.47, r_top=(0.115, 0.125), r_bot=(0.135, 0.145), rag=0.0, rows=3),
            dict(p='brim_hat'),
            dict(p='bandolier', items='coil', look='rope', on='robe', z=1.31, tilt=30.0, pad=0.03),
            dict(p='belt', on='robe', z=1.06, bone='spine_01', pad=0.008),
        ] + LEGS,
        weapons=[dict(w='pole', head='lamplighter', look='brass', back=1.05, front=2.25, r=0.015, name='lamppole')],
        notes="Lamp-hooker: lanky raider in a long dark coat with the collar turned up and a wide-brimmed lamplighter hat, a coil of "
              "rope across the chest, a lamplighter pole twice his height with a hook-and-snuffer head (wpn_lamppole)."),
    'mon_reaver_firepot': dict(
        family='reaver', scale=1.0, poses='thrower', emis=(1.0, 0.5, 0.12),
        body=dict(BODY, show=['hands']), props=dict(bulk=1.08, belly=0.18, hunch=8),
        looks={'glow': {'strength': 3.0, 'mottle': 0.2}, 'clay': {'color': (0.20, 0.10, 0.06), 'band': (0.10, 0.06, 0.04), 'rough': 0.5}},
        parts=[
            dict(p='robe', look='coat', hem=0.58, top=1.50, girth=1.0, rag=0.035, pleat=0.035, seed=6, crumple=0.006),
            dict(p='sleeves', look='coat', r_cuff=0.07, droop=0.008, r_sh=0.088, r_el=0.074),
            dict(p='hood', look='hide', tail=0.06, tail_drop=0.05), dict(p='mask', look='hide', slit=None, w=0.07, h=0.10),
            dict(p='eye_slit', look='blued', y=-0.134, z=1.678, w=0.105, h=0.02, k=14.0),
            dict(p='pot_rack'),
            dict(p='lashing', bone='lowerarm_l', t0=0.3, t1=0.85, r=0.045, turns=5, look='rope', thick=0.008),
            dict(p='glow_blob', bone='lowerarm_l', at=(0.72, 0.065, 1.475), r=(0.014, 0.014, 0.014)),
            dict(p='belt', on='robe', z=1.06, bone='spine_01', pad=0.008),
        ] + LEGS,
        weapons=[dict(w='pot')],
        notes="Fire-potter: stocky raider with a yoke-frame of six clay oil pots behind the shoulders, leather hood with a dark glass "
              "eye-slit, a slow-match wound round the left forearm (emis_ glow at the wrist), a pot ready in the right hand (wpn_pot)."),
    'mon_reaver_cagebearer': dict(
        family='reaver', scale=1.0, poses=["Idle_Lantern_Loop:0.3", "Melee_Hook:0.5", "Walk_Loop:0.3"], emis=(1.0, 0.62, 0.16),
        body=dict(BODY), props=dict(bulk=0.98, hunch=24),
        looks={'ember': {'emit': (1.0, 0.62, 0.16), 'strength': 6.0, 'mottle': 0.3, 'crust': 0.3}, 'apron': {'color': (0.03, 0.027, 0.025)},
               'patch': {'color': (0.20, 0.17, 0.13)}},
        parts=[
            dict(p='robe', look='coat', hem=0.60, top=1.50, girth=0.98, rag=0.035, pleat=0.035, seed=8, crumple=0.006),
            dict(p='sleeves', look='coat', r_cuff=0.066, droop=0.008, r_sh=0.085, r_el=0.07, side='r'),
            dict(p='apron', z=1.34, length=0.8, pad=0.045), dict(p='quilt_arm', side='l'),
            dict(p='shell', look='hide', regions=('head',), z0=1.705, offset=0.008, sink=False),
        ] + LEGS,
        weapons=[dict(w='cage_pole')],
        notes="Cage-bearer: hunched carrier in a soot-black apron, the left arm wrapped to the shoulder in a quilted glove, holding out an "
              "iron cage on a short pole with a live stolen ember inside (wpn_cage; the brightest emis_ a regular enemy carries)."),
    'mon_reaver_houndmaster': dict(
        family='reaver', scale=1.0, poses=["Idle_Loop:0.3", "Sword_Regular_A:0.55", "Walk_Loop:0.3"],
        body=dict(BODY), props=dict(bulk=0.9, hunch=10),
        looks={'fur': {'color': (0.05, 0.045, 0.04), 'tip': (0.20, 0.18, 0.16)}, 'iron': {'amount': 0.7}},
        parts=[
            dict(p='robe', look='coat', hem=0.60, top=1.50, girth=0.96, rag=0.035, pleat=0.035, seed=9, crumple=0.006),
            dict(p='sleeves', look='coat', r_cuff=0.066, droop=0.008, r_sh=0.085, r_el=0.07),
            dict(p='pelt_cloak'),
            dict(p='hang_item', item='bones', bone='spine_03', at=(0.0, -0.20, 1.40), cord=0.10, cord_look='strap'),
            dict(p='belt', on='robe', z=1.06, bone='spine_01', pad=0.008),
        ] + LEGS,
        weapons=[dict(w='pole', head='hook', look='iron', back=0.14, front=0.55, r=0.013, size=0.7, name='goad'), dict(w='leashes', hand='l')],
        notes="Ash-handler: lean handler in a shaggy cloak of cinder-hound pelts with the head-skin pulled up as a hood (ragged ear "
              "points), a bone whistle on a cord, a short hooked goad (wpn_goad) and three slack leashes in the left fist (wpn_leashes)."),
}
