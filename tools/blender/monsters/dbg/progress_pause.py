p = 'Y:/tools/blender/monsters/PROGRESS.md'
s = open(p, encoding='utf-8').read()
a = "No detached processes are left running.\n"
assert a in s
b = """Game run 2026-11-19 00:40 (windowed, run.ps1 -Tag rigA): --bestiary --area=wastes --level=30 --god --spawn=<the 22 types> --spawn-aggro ->
22 of 22 placed, bestiary boot "rig A 83/83 real models", log ERR=0 WRN=0, shutdown errors=0 (log game_20261119_004032.log; pictures
Y:\shots\run\rigA_finish_1..3.png; test save slot 93 moved to saves_test_backup).  Imported before it (import.ps1 exit 0, 00:12).
No detached processes are left running.

## PAUSED 2026-11-19 00:45 on the lead's order (user wants the game playable with what is finished)
State: nothing half-written.  Every .glb I wrote into game\assets\chars passed verify at its LAST build and was imported:
 rebuilt (4): ash_weeper, bog_netter, bog_sleeper, wax_wick;  reviewed + fixed (8): hauler_yoke / _hod / _collector / _stoker, reaver_brute,
 bell_clapper (no change needed), bone_amalgam, hush_mute;  new (10): husk_huddle, rime_breaker, cf_hulk, char_hangtree, ash_colossus,
 effigy_caryatid, lamp_colony, engine_carrier, wax_dripper, forge_hammer;  rebuilt for shared look changes (3): hush_blank, hush_mouth, forge_pourer.
Left / next steps when resumed (none of them blocks the game):
 1. Look at the three game screenshots more closely and do a daylight region run (the 00:40 run was a dark fight scene: the read from the
    game camera was only judged on the Blender game-camera tiles).
 2. Known weak points worth another pass, in this order: hush_mute (the chest hole still reads like a mouth from the front), ash_colossus
    (figures only read up close; few sparks), wax_dripper (arm-bodies hide in the cone's outline in the idle), cf_hulk (4.4k tris, plain core),
    rime_breaker (plough at the side, not in front), engine_carrier (thin fork tines).
 3. bell_clapper's family tile was re-rendered with the 'brute' pose set; the other 'heavy' specs of stages 1-3 still preview Sword_Idle
    (previews only -- the models are unaffected).
 4. dbg\patch_*.py are one-off edit scripts that are already applied (kept as a record; do not run them again).
"""
s = s.replace(a, b)
open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('ok')
