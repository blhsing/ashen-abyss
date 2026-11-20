# H3 - rig E flyers (5 creatures) - progress

Module `spec_h3.py` (this folder), work dir `h3\out`, shots `h3\out\shots`. Rig module `..\rig_e.py` (extended,
additive only: `git diff` shows 135 insertions, 0 deletions; `out\rig_e.orig.py` = the file as I found it).
`brief.py <ids>` prints the art briefs (run with Blender's python `-I`).

## STATE (2026-11-20, 23:00) - DONE
- All 5 are baked into `Y:\game\assets\chars\mon_<id>.glb`, pass `bverify.py`, build with a silent audit (no floating /
  detaching parts), were looked at (tile / game / top view / dbg1 / dbg2) and have a strip in
  `Y:\shots\chars\beast_mon_<id>_anim.jpg`. Meta files: `h3\out\mon_<id>.meta.json` (all from the final baked builds),
  `h3\out\verify.json`. Overview sheets: `h3\out\shots\h3_tile_sheet.jpg`, `h3_game_sheet.jpg`.
- `dusk_bat` (spec_e.py) still builds with the extended `rig_e.py`: `--nobake` build into `out\batcheck`, its meta is
  identical to `..\out\mon_dusk_bat.meta.json` field by field except `bytes` / `materials` (no bake).
- NOT done on purpose (lead's job): `import.ps1`, `bmeta.py` merge (`python -I bmeta.py Y:\tools\blender\beasts\h3\out`),
  any run of the game. NOTE: Godot `.import` files + extracted textures for the five models appeared in
  `Y:\game\assets\chars` at 22:54-22:56 (somebody else's editor / game run imported my intermediate files);
  `mon_rust_locust.glb` was rebuilt at 22:58, after that import.
- No detached processes, nothing running.

## Commands (all through the throttled launcher)
- Build + shots: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\h3\b.ps1 -ids "dusk_crow dusk_gadfly" [-NoBake] [-NoBuild] [-NoShots] [-shots "--tile --game --dbg --strip"]`
  (wraps `bb.ps1 bbuild.py / bshots.py` with `--path / --spec / --out` of this folder; never build the argument string
  from bash variables: bash eats the backslashes and the spec is then silently not found -> `KeyError: '<id>'`).
  All five with bake: about 4 minutes.
- Verify: `"C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" -I Y:\tools\blender\beasts\bverify.py dusk_crow dusk_gadfly rust_locust fiend_wing hush_wing --meta=Y:\tools\blender\beasts\h3\out`
- Strip: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\bstrip.ps1 mon_a,mon_b -Dir Y:\tools\blender\beasts\h3\out\shots`

## Added to rig_e.py (defaults of everything that existed are unchanged)
- `panes(lead, trail, sub, nv, th, notch, camber, droop)`: wing of flat, straight-edged panes (glass / slag / metal leaf)
  between matching lead / trail stations, trailing edge cut into V notches; `pane_weights(part, lead_bones, trail_bones)`
  skins it so that it stays on rigid spars (the bat's `membrane` + `wbones` lets finger spars come off the sheet).
- `kite_skel(h, half, front, back, hole, nx, nf, nb, ribbons, extra)`: body + flex chains `fx_<s>1..`, `ff1..`, `fb1..`
  + ribbon chains. Class `Kite(Flyer)`: `flex` (curl the four arms), `wave` (ray-like saddle undulation), `ribbons`.

## In spec_h3.py
- Look kinds (registered with `L.KINDS.update`): `feather` (barbs along an axis, sheen, singed patches), `iwing`
  (insect wing: long vein cells along an axis), `slag` (black glass, emissive cracks that get denser / brighter
  towards a leading-edge polyline given in object space).
- Parts: `feather()` (one feather / leaf / insect wing blade as a closed slab), `rag_profile()`, `lathe_on()`,
  `ringed_tube()` (segmented abdomen / knuckled whip, skinned). Motion: `dangle()` (hanging insect legs).
- Buzzing creatures (gadfly, locust): every clip has an EVEN number of frames, so the per-frame wing alternation of
  `buzz=True` closes the loops and one-shot clips end on the Idle wing phase.

## Lessons
- `N.edge()` (the baked "inside AO" mask) is 1 everywhere on slabs thinner than about 1 cm: edge wear cannot be used on
  feathers / leaves / wings (a metal leaf gets the edge colour all over).
- `Flyer.death_fall` indexes `crumple / rest / rest_fold` with `range(3)`: pass 3-tuples even for one-bone wings.
- From the game camera a thing that hangs straight under a body is hidden by it: the gadfly's globe had to trail back.
- Renders through a low camera make a wing that points away look as if it stood upright; check the top view
  (`--view=top:20:60:0:0:1.5:4.5`) before "fixing" it.
- A whip tail under a diving body goes through the ground between the sampled audit frames: `keep_clear()` in
  `fiend_wing` (bisection on the tail lift) keeps the cinder above the ground on every frame.

## Creatures (model | family | rig | tris | bones | MB | verify | honest weaknesses)
mon_dusk_crow | dusk | E | 10228 | 13 | 3.23 | PASS | beak long (hornbill rather than crow); feathers are flat rigid slabs (coverts read as tiles close up, wings fold as stacked plates in Hit); ruff / body tufts are cones; wingspan 2.2 m (oversized on purpose); one-shot clips end in the stock hover pose, not exactly Idle frame 0 (as the bat)
mon_dusk_gadfly | dusk | E | 5618 | 19 | 3.33 | PASS | each wing is a fan of three blades (my reading of "blurred"; they also alternate per frame); globe is opaque (vein-net emission), cannot turn amber when fed (only swells: bone `abdomen` scale); globe trails 45 deg back instead of hanging straight down; legs are stiff two-segment rods
mon_rust_locust | rust | E | 5564 | 25 | 3.22 | PASS | brass leaves are plain flat sheets of one tone (reads olive under a grey sky); hind wings are fans of three blades; no violet static (not built); small legs simple; small on screen (0.8 m body)
mon_fiend_wing | fiend | E | 5988 | 19 | 3.87 | PASS | glass is opaque; the crack net covers the whole wing (dimmer aft) - much emissive area; spars banded like a snake; body tiny and simple; the cinder comes back by swelling at the tail tip (bone scale); the corpse keeps glowing
mon_hush_wing | hush | E | 4704 | 25 | 2.75 | PASS | plain by brief (one chalk look, no face); 2.5 m wide x 2.1 m long (kite, not a square diamond); Death / Spawn scale the bone `body` non-uniformly (closes to a white line that stays on the ground) - unchecked in the engine; ribbons are flat strips
