# H4 (rig F rigid / floating, 6 creatures) - progress

Module `spec_h4.py` (this folder), work dir `h4\out`, shots `h4\out\shots`. Rig module `..\rig_f.py` (extended, additive;
a copy of the file as I found it: `scratch\rig_f.orig.py`). Commands: `..\HELPERS.md` with hN = h4.

## State: DONE (2026-11-20)
- All six are baked into `Y:\game\assets\chars\mon_<id>.glb`, pass `bverify.py` (last run: all six together, after the
  last rebuild), build with a silent audit, were looked at (tile / game / dbg1 / dbg2 + close-ups
  `out\shots\<model>_v_<name>.png`) and have a strip in `Y:\shots\chars\beast_mon_<id>_anim.jpg`.
- Meta files: `h4\out\mon_<id>.meta.json` (all from the baked builds; byte sizes equal the delivered files).
- `wraith_gasp` (spec_f.py) still builds with the extended `rig_f.py`: checked as a `--nobake` build into
  `h4\out\proof` (1438 tris, 8 bones, as before). `spec_f.py` and the other shared files were not edited.
- NOT done (lead's job): `import.ps1`, `bmeta.py` merge (`python -I bmeta.py Y:\tools\blender\beasts\h4\out`), any look in
  the engine.
- No job running, no detached processes.

## Extensions made
- `rig_f.py` (additive, defaults unchanged): `Rigid.turn` (rotation matrix about a pivot), `Rigid.turret` (Walk / Run =
  Idle, speeds 0), `scale_axes` (bone scale along armature axes), `hide`, `core` (skinned main body, as H1's), `stepped`
  (ratchet interpolation), `keyed` / `clank` (lid curves), `flight` (ballistic piece that lands), `low_point`,
  `HangChain` (world-space pendulum chain / limb that lies down on the ground; per-link blend `w`, `fold`, `off`,
  `bend`), `from_pose` (one-shot clips start / end exactly in Idle frame 0), `facet_tube` (cut-gem loft).
- `spec_h4.py`: look kinds `cfglass` (ice + blue light towards the tip + warm painted core; COLOUR emission mask),
  `mothwing`, `furnace` (white-hot to dull red by distance; COLOUR emission mask); helpers `enlarge` / `grow` (as H1's),
  `beam_x`, `band_x`, `gear_ring`, `facing`.
- `scratch\dbg_low.py`: `bb.ps1 dbg_low.py "<id> <clip> [limit]" -Dir Y:\tools\blender\beasts\h4\scratch` lists the parts
  that go below the ground in a clip (and the frame).

## Lessons
- A COLOUR socket can be returned as `emask` with `emit=((1, 1, 1), strength)`: the emission texture then carries
  several colours from one look (flicker: blue tips + orange core; furnace: white-hot to red).
- `C.rigid()` recalculates the winding of every part: inward-facing shells (bell inside, furnace hollow, mirror face)
  need `part.two = True` after they have been oriented by hand (`facing()`).
- Parts that hang below a floater (rag collar, legs, chains with a hook) go under the ground when it lands: give them
  a bone to squash / a `HangChain` whose ground height is at least their own radius; keep hooks INSIDE the last bone.
- Check clearances of swinging parts by arithmetic before building (the heliostat dish cut through its turntable, the
  forge-eye's stump showed inside the hollow, its counterweight sat in the cross-bar).
- From the game camera (52 deg down) a lid hinged at the top hides what is under it: the forge-eye looks 40 deg up
  for that reason, the heliostat dish faces up; a floater hides what hangs under it (the bell's legs are long).
- `N.hem()` is useless on parts whose every vertex lies on a border (two-sided single quads).
- bverify from bash: pass `"--meta=$H4\out"` in double quotes (a `\\` inside the word loses the separator).
- bb.ps1 output arrives when the run ends: wait with a Monitor on the log, do not poll.

## Creatures (model | family | rig | tris | bones | MB | verify | honest weaknesses)
mon_cf_flicker | coldfire | F | 342 | 21 | 1.03 | PASS | built 1.14 x (0.72 m flame = 1.4 x "knee-high") and still small in the game view; glass is opaque: the orange "pinpoint deep inside" is painted on the belly front and back (not seen from the sides); few, large facets; Attack2 / Death swap the flame for ten loose shards (bone scale 0.001)
mon_dusk_moths | dusk | F | 3584 | 26 | 1.86 | PASS | 448 moths, each two flat kites (folded paper close up, no bodies); the ball is flattened (2.3 m high, 3 m wide) to clear the ground; Death squashes the clusters flat with a non-uniform bone scale (the dead moths' normals may shade wrong in the engine); wing beat = 4-frame cycle of the flap bones, judged from stills only
mon_bell_hanging | bell | F | 8524 | 15 | 3.45 | PASS | legs longer / heavier than life (0.7 m out of the mouth) so they show under the bell from the game camera; the lip rim reads white rather than faint; shroud collar and the ringer's trunk are simple; in Death the legs slide out of the lying bell without a visible rope
mon_lamp_lure | lamp | F | 4196 | 19 | 3.63 | PASS | stays amber when attacking (emission is baked); stalk tan rather than iron-dark; Attack2 reads mainly through the roof flipping up; the dead stalk lies in one long wavy line
mon_engine_heliostat | engine | F | 8244 | 14 | 3.33 | PASS | the dish faces up and forward (30 deg from horizontal, 3.3 m to the vane), not level at the hero; pitch range only 25..47 deg (lower it would cut the turntable); Death = leans 53 deg propped on its dish rim, the buckled leg hangs limp; no braces between the legs
mon_forge_eye | forge | F | 7046 | 10 | 3.28 | PASS | the eye looks 40 deg up, not level; lever = mast + arm with the hanging weight + cross-bar for the chains: 2.4 m to its top, not squat; the Idle crack shows its glow from the front, not from the game camera; Death keeps the lid shut (glow hidden) and only tilts the stump 9 deg
