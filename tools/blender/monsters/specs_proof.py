"""Proof of the assembler: two EXISTING hand-built creatures re-created as pure specs (scratch ids, written to asm_out)."""
SPECS = {
    'x_cultist': dict(
        scratch=True, family='proof', scale=1.0, poses='sword', emis=(0.80, 0.90, 1.0),
        body=dict(type='flesh', decimate=1.0, show=['hands'], look='skin_ash', eyes=None),
        looks={'glow': {'strength': 3.5, 'mottle': 0.1}, 'strap': {'color': (0.05, 0.04, 0.035)}},
        parts=[dict(p='robe'), dict(p='sleeves'), dict(p='hood'), dict(p='capelet'), dict(p='mask'), dict(p='foot_wraps'),
               dict(p='rope_belt'), dict(p='candles')],
        weapons=[dict(w='dagger')],
        notes="Spec re-creation of mon_cultist."),
    'x_skeleton_thrower': dict(
        scratch=True, family='proof', scale=1.0, poses='thrower', emis=(0.30, 1.0, 0.45),
        body=dict(type='skeleton', thick=1.0, jaw=9.0),
        looks={'hood': {'color': (0.19, 0.062, 0.048), 'stain_amt': 0.6}},
        parts=[dict(p='poncho'), dict(p='hood', style='rag', void=False), dict(p='quiver'), dict(p='chest_strap'),
               dict(p='bracer', side='r'), dict(p='belt', z=1.04, w=0.018, on='body', buckle=None, pad=-0.012),
               dict(p='hang_item', item='bones', bone='pelvis', at=(-0.07, -0.105, 1.036), cord=0.09, cord_look='strap')],
        weapons=[dict(w='javelin')],
        notes="Spec re-creation of mon_skeleton_thrower."),
}
