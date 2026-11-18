# H2 - rig B quadrupeds (10 creatures) - progress

## STATE (2026-11-19) - DONE
- All 10 creatures are built with bake, exported to `Y:\game\assets\chars\mon_<id>.glb`, `bverify.py` PASS for all 10,
  build audit silent for all 10 (no floating / detaching parts), tile / game / dbg1 / dbg2 / strip tiles rendered and
  looked at (`h2\out\shots`), labelled strips in `Y:\shots\chars\beast_<model>_anim.jpg`.
- Files: `h2\spec_h2.py` (one function per creature id, `BUILD`), `h2\h2lib.py` (my helpers and look kinds), work dir
  `h2\out` (`<model>.meta.json` of the final baked builds, `verify.json`, `shots\`, `tex\`).
  `h2\out\rig_b.orig.py` = `rig_b.py` as I found it (for a diff).
- NOT done on purpose (lead's job): `import.ps1`, merging the meta into `rigs_bf_meta.json`, family sheets.
- No detached processes, nothing running.

## Commands
- Build:  `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\bb.ps1 bbuild.py "<ids> --path=Y:\tools\blender\beasts\h2 --spec=spec_h2 --out=Y:\tools\blender\beasts\h2\out [--nobake]"`
  (all ten with bake: about 4 minutes)
- Shots:  `... bb.ps1 bshots.py "<ids> --tile --game --strip --dbg --out=Y:\tools\blender\beasts\h2\out\shots --meta=Y:\tools\blender\beasts\h2\out [--src=Y:\tools\blender\beasts\h2\out]"`
- Verify: `"C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" -I Y:\tools\blender\beasts\bverify.py <ids> --meta=Y:\tools\blender\beasts\h2\out`
- Strip:  `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\bstrip.ps1 mon_a,mon_b -Dir Y:\tools\blender\beasts\h2\out\shots`

## Added to rig_b.py (dusk_hound in spec_b.py builds unchanged: its meta is identical field by field)
- `quad_J(..., bend=, paw=)`: scale the fore-aft zigzag of the legs / the paw length for bigger animals.
- `sprawl_J(...)`: joint table of a sprawler (lizard, toad) with the same keys / bone names as `quad_J`.
- `Quad`: `glide=` argument, `jaw_axis` ('z' = a door hinged at its side), `tail_axis` ('y' = upright tail),
  `minz(fn, u, part)` (lowest point of the posed mesh for a clip function; part = all / head / tail / body / trunk / core),
  `fit(make, val, us, lo, part, zmin)` (largest "head down" value that keeps a part above the ground),
  `extra=` hooks on every stock clip, `gait(..., side=, headyaw=, taillag=, tailph=, extra=)` (sprawl undulation),
  `idle(tail_lift=)`, `hit(drop=, roll=, pitch=)`.
- New stock clips: `gore`, `sweep`, `charge` (all three limit their head-down angle with `fit`), `swipe`, `rear`
  (stomp / fore-leg strike), `death_belly` (drop, head angle and tail lift solved on the mesh; `planted=True` for
  sprawlers), `spawn_stand` (gets up from the lying pose; frame 0 rests exactly on the ground); `Quad.FOLD`.
- `quad_flesh`: `torso_power`, `neck_power`, `torso_n`, `f_root` / `h_root`, `leg_m`, `leg_power`, `extra`, `inflate`.

## In h2lib.py
- Parts: `hooves`, `flat_horn`, `round_horn`, `sail_plate`, `tri_ear`, `bar`, `hoop`, `block` (faceted chunk with known
  extents), `trap_jaw`, `chain_on`, `rope`, `keg`, `spear`, `strap_over`, `pot`, `seat_rock`, `clean_mesh` (after booleans),
  `surf_top / surf_bottom / surf_side`, `host_bone`, `keep_bones`.
- Motion: `DragChain` (a chain hanging from a bone and dragging on the ground, solved per frame in world space).
- Look kinds (registered with `L.KINDS.update`): `crackwood` (wood whose cracks leak light), `wfur` (white pelt).
- `report_low(C)`: build-log line naming the lowest part of every clip that goes more than 2 cm under the ground.

## Creatures (model | family | rig | tris | bones | MB | verify | honest weaknesses)
mon_dusk_boar | dusk | B | 7612 | 29 | 3.96 | PASS | mouth glow is a thin bright line, not a soft glow; bristles are flat cones; plain hooves; Attack1 stretches the hind legs 3 cm
mon_dusk_lizard | dusk | B | 6648 | 32 | 4.12 | PASS | stands 2 cm clear of the ground rather than belly-down; sail is a thin line from straight above; tail straight at rest; Attack2 is a hop-spin (feet in the air)
mon_forge_hound | forge | B | 3528 | 28 | 1.53 | PASS | clinker yokes boxy; trap head thin from the side; rod legs thin; leap IK stretch 0.17 m (as the hound proof)
mon_dusk_lynx | dusk | B | 6100 | 30 | 3.31 | PASS | fur tufts are chunky cones; face more dog than cat; death pose legs stiff; pounce IK stretch 0.28 m
mon_hush_beast | hush | B | 4935 | 24 | 2.23 | PASS | crazing of the chalk look rather visible; long thin triangles round the hole; Spawn scales the bone `hip` (uniform)
mon_effigy_hound | effigy | B | 4966 | 27 | 3.51 | PASS | lumpy bulldog rather than a crisp brick; notch reads as a saddle; Run is a 6-frame patter (short legs at 5.4 m/s)
mon_dusk_yak | dusk | B | 12268 | 33 | 4.35 | PASS | face is a dark mass under the forelock; fringe = rigid ropes on 4 skirt bones (no finer secondary motion); snow drift simple
mon_rime_packbeast | frost | B | 7986 | 31 | 4.09 | PASS | head simple; whole load rigid on one bone; Attack1 "kick" is a fore-hoof strike while rearing; pawing hoof dips 2.6 cm
mon_fiend_maw | fiend | B | 6192 | 25 | 4.49 | PASS | firebox only 0.2 m deep (boolean cavity + glowing block); body cracks coloured, not emissive; slits are bars on the door
mon_bog_toad | mire | B | 7240 | 30 | 3.43 | PASS | belly sack is a rigid glowing ellipsoid (slosh = bone rotation / scale); belly slam barely squashes; death undramatic

## Notes for the lead
- Shared-file issue (worked around, not edited): `blib.Beast.audit` / `near_all` decides "inside" from the normal of the
  nearest triangle; near a convex edge or vertex of a faceted piece that is wrong for points up to 6 cm outside, so the
  piece counts as touching. I got false "detaches" from it (forge hound thighs vs. a `monlib.rock` hull) and it can also
  hide a real gap. Workaround: `h2lib.block` with real overlap where legs attach.
- `mon_dusk_hound` itself is 19 triangles over its swarm budget (4019 > 4000) and has the Attack2 IK note; both are
  in your build too, not caused by the rig changes.
- glTF exporter warning "more than 4 joint vertex influences" (boar, lizard, maw): midline vertices with equal weights
  on mirrored bones survive `monlib.normalize_weights` as 5 influences (ties); the exporter keeps 4. Harmless.
- `Quad.tailw(lift=)`: positive values LOWER a tail that points back; tails lying on the ground need `idle(tail_lift=0)`.
