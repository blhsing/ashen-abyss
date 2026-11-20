"""Stage 4 specs (part C): the last six oversized bases -- gallows-tree, brazier-hulk, mirror-carrier, crowned colony,
drift-colossus, tallow-mound (parts in asm_parts9.py).  Pose sets 'brute' / 'zbrute' = what the game really plays
(Idle_Loop + Walk_Loop unless the bestiary's anims text names a Zombie idle)."""

HIDDEN = dict(type='flesh', show=[], decimate=0.5)

SPECS = {
    'mon_char_hangtree': dict(
        family='char', scale=1.6, poses='brute', emis=(1.0, 0.4, 0.06),
        body=dict(HIDDEN),
        props=dict(leg_len=0.94),
        looks={'ember': {'emit': (1.0, 0.22, 0.025), 'strength': 1.8, 'mottle': 0.7, 'base_mul': 0.3}, 'charwood': {'heat': (1.0, 0.22, 0.03), 'heat_amt': 0.10}},
        jpeg=(88, 86, 92),
        parts=[dict(p='gallows_tree')],
        notes="Gallows-tree: a massive hollow charred trunk on two root-legs with ONE great horizontal bough at shoulder height on "
              "its right -- three frayed nooses and a rusted gibbet cage (a skull inside) hang from it -- and only a splintered "
              "stump on the left: an inverted L.  The broken top is open (embers glow in the hollow, seen from the game camera) and "
              "a knot-hole mouth glows on the front (emis_).  The bough is rigid on spine_03; nooses and cage blend to spine_01."),
    'mon_cf_hulk': dict(
        family='coldfire', scale=1.55, poses='brute', emis=(0.45, 0.8, 1.0),
        body=dict(HIDDEN),
        props=dict(shoulders=1.12, arm_len=1.08),
        looks={}, dress={'frost': 0.3},
        parts=[dict(p='brazier_hulk2')],
        notes="Brazier-hulk: a dead beacon's iron brazier basket worn as a ribcage by a hulking body of blue ice that grew up through "
              "it -- the frozen fire glows through the black bars (emis_), erupts from the basket top as a crown of glass tongues "
              "where a head would be, the brazier's three legs stick out of the back; arms are rough ice columns ending in fused "
              "clubs.  Faceted glass like the cold-flame spirit."),
    'mon_engine_carrier': dict(
        family='engine', scale=1.85, poses='brute', emis=(0.74, 0.62, 1.0), budget=22000,
        body=dict(HIDDEN),
        props=dict(shoulders=1.1),
        looks={'brass_bright': {'amount': 0.05, 'rough': 0.12}},
        parts=[dict(p='carrier')],
        notes="Mirror-carrier: a towering two-legged brass porter -- blocky riveted body, thick piston legs with arc-lit drum knees "
              "(emis_), a small sunken head-lantern, arms that are lifting forks, and on its back an A-frame carrying one huge "
              "polished wedge of the Brass Sun that stands above it like a sail (rigid on spine_03)."),
    'mon_lamp_colony': dict(
        family='lamp', scale=1.5, poses='brute', emis=(1.0, 0.62, 0.18),
        body=dict(type='flesh', decimate=0.36, look='skin', eyes='void', gaunt=1.0),
        props=dict(hunch=10, arm=1.2, head_pitch=14),
        looks={'rag': {'color': (0.085, 0.075, 0.06), 'stain_amt': 0.9}},
        parts=[
            dict(p='colony'),
            dict(p='rags', look='rag', arcs=((-165, -15, 0.5), (15, 165, 0.55)), z=1.02, rx=0.30, ry=0.34, cy=0.13, holes=0.3, seed=4),
            dict(p='shelf_fungus', bones=('upperarm_l', 'lowerarm_l', 'upperarm_r', 'lowerarm_r'), count=3, r=(0.05, 0.08)),
        ],
        notes="Crowned colony: three infected fused back-to-back under a single enormous table-mushroom cap (emis_ gills underneath, "
              "a ring of amber lantern-fruit hanging from the rim on threads), six legs -- four of them dangling -- and two thick "
              "arms each twisted together from three limbs.  A walking parasol."),
    'mon_ash_colossus': dict(
        family='ash', scale=1.9, poses='brute', emis=(1.0, 0.22, 0.06), budget=22000, jpeg=(84, 82, 88),
        body=dict(type='flesh', decimate=0.4, look='ashmass', eyes=None, melt=3, hide=['head', 'neck']),
        props=dict(bulk=1.42, shoulders=1.18, chest=1.08, hips=1.05, leg=1.3, arm=1.2, hunch=14, belly=0.15),
        looks={},
        parts=[
            dict(p='drift_mass2'),
        ],
        notes="Drift-colossus: a drift that stood up -- a slope-shouldered mass with no neck whose surface is made of half-emerged "
              "ash-casts (faces, backs, reaching arms pressed outward), fists that are knots of fused bodies, dull emis_ sparks in "
              "the creases, a ragged plume of ash trailing from its back."),
    'mon_wax_dripper': dict(
        family='wax', scale=1.5, poses='brute', emis=(1.0, 0.72, 0.30),
        body=dict(HIDDEN),
        props=dict(arm_len=1.08),
        looks={'tallow': {'color': (0.46, 0.40, 0.26), 'soot': 0.4}},
        parts=[dict(p='tallow_mound'), dict(p='flame3', h=0.13, r=0.03, sweep=(0.0, 0.12, 0.0), bone='spine_03')],
        notes="Tallow-mound: sealed chandlers who ran together -- a slumped cone of wax taller than a door (pelvis-rigid below the "
              "hips, no legs shown) with heads, shoulders and praying hands half-sunk in its surface, a comb of dead wicks down the "
              "ridge of its back (the top one still burns, emis_), and two arms that are bodies (waist in the cone, the head for a fist)."),
}
