# Beasts (rigs B-F, 46 non-humanoid creatures) - progress

## STATE WHEN PAUSED (2026-11-21 00:56, lead's order: all work paused for the 0.3.0 release)
- **All 46 creatures are in `Y:\game\assets\chars`, complete, and all 46 PASS `bverify.py`** (full run 00:40 on `out`,
  `h1\out` .. `h4\out`; `lurk_leech` rebuilt and re-verified 00:53). `rigs_bf_meta.json` = exactly these 46 models, every
  entry has all contract fields (10 with `hover_in_model`, 5 with a `light` entry: lantern, flicker, lure, gadfly, fiend
  wing). Godot's import cache is newer than every one of the files (checked 00:55; `import.ps1` exit 0).
- No Blender job, import or game run of mine is running or queued; all four helpers have ended.
- Delivered: 46 strips `Y:\shots\chars\beast_mon_<id>_anim.jpg`, 18 family sheets `beast_<family>.jpg`, four dark game-view
  sheets `beast_gameview_1..4.jpg` (the leech tile in sheet 3 and in `beast_lurk.jpg` is one small colour tweak behind:
  re-run `bshots.py "lurk_leech --tile --game"` is done, re-run `scratch\family_sheets.ps1` to refresh the sheets),
  proof overview `beast_proofs_def.jpg`, five proof WebPs `Y:\shots\preview\beast_<b..f>_proof.webp`, credits
  `Y:\credits\beasts.md`, the table at the end of this file (`python -I btable.py` regenerates `out\table.md`).
- **NOT DONE: the in-engine check.** No creature of rigs B-F has been looked at in the game by me. The check is
  prepared: `bash scratch/engine_check.sh 1 11` (eleven `--bestiary --spawn` line-ups on test slot 96, pictures
  `out\engine\g<N>_<1|2>.png`, never runs while AshenAbyss.exe is open, moves slot96* to saves_test_backup at the end).
  It is blocked by the file `scratch\PAUSED`: delete that file first. Unchecked in the engine: material prefixes
  (`hair_`, `spectral_ghost` on the lantern's smoke), emission clamp / pulse, hover heights, the meta lights, the
  `rust_gear` wheel spin, near-zero and non-uniform bone scales, 360-degree turns in looping clips (`hush_serpent`).
- **Next when resumed:** (1) delete `scratch\PAUSED`, run the engine check, look at every picture, fix what it shows;
  (2) `powershell -File scratch\family_sheets.ps1`; (3) final report to the lead (table, shared skeletons, deviations,
  what could not be checked).
- Lead's review bar (2026-11-20/21, proofs approved): every creature must read as a creature at game-camera size on
  dark ground - real head / face, S-bends instead of straight columns, one bright accent, enlarged where it was a
  speck. Applied after the helpers finished: crow (slate feathers, smouldering primaries), locust (violet static),
  digger (paler soil), hound (larger heat pits), spitter (S-bend, dark feathered frill with glowing tips), elder
  (dimmer belly), leech x1.3 (lighter skin, pale sucker lip), grub x1.25.
- Kit additions since the first pause: `Beast.rescale(K)`; `bshots.py` game view = the REAL camera (CameraRig.cs: pitch 48,
  15.6 m, FOV 32 -> 121 px per metre; ARCHITECTURE.md's 24 m is out of date) on dark ground, `_game2.png` = Run,
  `spectral_*` materials previewed as additive ghost matter, a preview light from the meta `light` entry, margins 1.2;
  look kinds `slick`, `warm`, `ghostface`, `frond`, `sglow`, `tipglow`, `cglass`; `rig_d.Looper` rewritten (flat end links,
  solved hump), `Rearer.tailwave` runs backwards; meta `light` entry (the roster engineer hangs a soft shadowless light
  on the creature, max 8 live; `bmeta.py` LIGHTS table); `bsheets.py`, `btable.py`; `HELPERS.md` has the helpers' lessons.
- Rule learnt the hard way: never write Python through bash heredocs (quotes break); write a patch file with the
  Write tool (`scratch\patch_*.py`, `.new` + `os.replace`) and run it. In bash, quote Windows paths ('Y:\...').

## How to run (all through the throttled launcher)
- Build:   `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\bb.ps1 bbuild.py "dusk_hound sexton_beetle [--nobake] [--path=<dir>] [--spec=<module>] [--out=<work dir>]"`
  (a build with bake takes 1-2 min; `--nobake` writes a flat preview into the work dir for rig / clip debugging).
- Shots:   `... bb.ps1 bshots.py "dusk_hound --tile --game --strip --dbg [--webp] [--view=name:yaw:pitch:tx:ty:tz:dist[:clip:time]] [--src=<dir>] [--out=<dir>] [--meta=<dir>]"`
  -> `out\shots\<model>_tile.png` (sheet tile), `_game.png` (game camera), `_s0..8.png` (strip tiles), `_dbg1.jpg`
  (Idle 2, Walk 4, Run 4, Hit 2) and `_dbg2.jpg` (Attack1 3, Attack2 3, Death 3, Spawn 3). LOOK at dbg1 / dbg2 for every creature.
- Strip:   `powershell ... -File bstrip.ps1 mon_dusk_hound[,mon_x]` -> `Y:\shots\chars\beast_<model>_anim.jpg` (labelled 3 x 3).
- Verify (no Blender): `"C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" -I bverify.py <ids> [--src=] [--meta=]`
  -> PASS / FAIL per model from the exported file (clips, loops, budgets, ground, foot drift, materials, meta).
- Meta: every build writes `out\<model>.meta.json`; `bmeta.py` merges them into `Y:\game\assets\chars\rigs_bf_meta.json`.

## Files
- `blib.py` core (skeleton `Sk`, pose engine `Pose` + IK, `Beast`: clips, audit, bake, export, meta). `bparts.py` looks + parts.
- `rig_b.py` quadrupeds (`Quad`), `rig_c.py` arthropods (`Arth`), `rig_d.py` serpents (`Column`, `Rearer`, `Looper`),
  `rig_e.py` flyers (`Flyer`), `rig_f.py` rigid / floating helpers (`Rigid`).
- `spec_b.py` ... `spec_f.py`: one function per creature, registered in `BUILD`. Helpers add their own spec module in
  their sub-folder and pass `--path=<folder> --spec=<module>`.
- `bbuild.py` driver, `bshots.py` renders, `bverify.py` contract check, `bstrip.ps1` strip composer, `bb.ps1` launcher.

## Facts established (do not re-derive)
- Blender 5.2: `action.fcurve_ensure_for_datablock(arm, path, index=i)` works after `arm.animation_data.action = act`;
  one NLA track + strip per action (track muted, `strip.action_slot = act.slots[0]`), then export with
  `export_animation_mode='ACTIONS', export_nla_strips=True, export_force_sampling=True, export_optimize_animation_size=True,
  export_optimize_animation_keep_anim_armature=True` gives glTF animations named exactly `Idle`, `Walk`, ...
- Facing: build facing **-Y** in Blender (+X = the creature's left), exported +Y up -> the game's +Z forward.
- Rotations in `Pose.rot(bone, x, y, z)`: degrees about the ARMATURE axes carried along by the parent. For a -Y facing
  bone: x > 0 = nose / tip down (a vertical bone leans forward), z > 0 turns the nose to +X (its left), y = roll
  (a +X pointing bone: y > 0 lowers its tip). `Pose.setR(bone, rot_axis(axis_in_rest_space, deg))` for any other hinge.
- `CharModel.Convert` / `char.gdshader`: only the prefixes `emis*` (pulse, energy clamp) and `spectral*` change shading;
  `skin_ / hair_ / bone_ / leather_ / metal_ / rock_ / cloth_` share one PBR path. Chitin goes under **`bone_`**, fur
  under `hair_` (the monsters kit logs "bad material prefix hair_..." - harmless, its list is older than the brief).
- The game shader culls back faces: thin sheets must be closed slabs (`P.sheet_solid`, `Part.solidify`) or
  `blib.two_sided`. `Beast._make_objects` does NOT recalc normals mesh-wide.
- Emission: any look that returns `emit` bakes into the emission texture; non-emissive looks may share an `emis_` slot
  (their texels bake black).
- `Y:\tools\blender\monsters\verify.py` without ids globs every `mon_*.glb`, expects the humanoid skeleton and no
  animations: never run it without ids while beast files are in `Y:\game\assets\chars`.
- A baked build takes 60-100 s; the Blender semaphore is shared with the rig-A artist (expect queueing).
- Gait periods are snapped to whole frames; stride = v x duty x T, so planted feet do not slide at the stated speed
  (verified from the exported file: hound walk 0.9 cm, everything else 0).
- Fast small creatures need short cycles (>= 6 frames) with duty < 0.5: the stride cannot exceed the reach of the legs.
- Audit (`Beast.audit`): every rigid piece must be connected to the main body through touching pieces in the rest
  pose ("floating in rest pose") and keep touching one of its rest neighbours in sampled frames of every clip
  ("detaches in <clip>"). Fix the geometry (sink roots into their host, put joints on the bone head) until it is silent.
- Death: `C.clip('Death', ..., floor=<body bone>, settle=(u0, u1))` lifts the posed mesh out of the ground on every
  frame and lowers it onto the ground from u0 to u1, so corpses end exactly on z = 0.

## Design decisions per rig (taken; revise only with a reason)
- **Sizes.** Model is authored at brief size / `scale` (the game multiplies by `MonsterDef.Scale`). `run_speed` in the
  meta = bestiary `speed` / `scale`; `walk_speed` about 0.3-0.4 of that (lower when the legs cannot reach).
- **Root** bone `root` at the origin, never keyed; whole-body motion goes on the first child (`hip` / `body` / `s1`).
- **Every clip keys every bone**, 30 fps, loops have frame N = frame 0. One-shot clips start (Attack / Hit: also end)
  in the rest stance = Idle frame 0.
- **B quadruped** - `rig_b.py`: 31 bones + extras; 4-bone legs, IK on upper + lower, pastern / toe aimed in world
  space; WALK4 / TROT / GALLOP / BOUND phase tables; body = union + voxel remesh (`P.organic`) of tubes and blobs.
- **C arthropod** - `rig_c.py`: `body`, `head`, `mandible_l/r`, `antenna<i>_l/r`, `abdomen<i>`, `tail<i>`,
  `leg<i>_<s>_a/b/c` (rigid chitin segments; IK on a + b, tarsus c keeps its world angle), alternating tripod / wave
  phase tables, snap / ram / hop attacks, death_flip / death_drop, spawn_dig. Extra bones per creature via `extra=`.
- **D serpent** - `rig_d.py`: `Column` (burrowers: chain `s1..sN` from a buried base, `meta.buried`, Walk / Run =
  the leaning, weaving column; Death = topple; Spawn = erupt), `Rearer` (`body`, `neck<i>`, `head`, `tail<i>`),
  `Looper` (chain placed between two gait targets as a circular arc: inchworm loops with planted ends).
- **E flyer** - `rig_e.py`: `body`, `head`, `jaw`, `ear_l/r`, `wing_<s>1..3`, `tail<i>`, `leg_l/r`. The hover height is
  baked into the rest pose and every clip: meta `fly_height` + `hover_in_model: true` - the game must NOT add it.
  Death falls and ends on z = 0, Spawn drops in from 3 m above.
- **F rigid / floating** - per-creature skeletons, helpers in `rig_f.py`. Floaters as E (hover in the model);
  turrets stand on the ground, Walk / Run = Idle, speeds 0.
- **Textures**: one 2048 atlas (1024 for swarm creatures): normal JPEG q90 4:4:4, ORM q84, base colour / emission q88.

## Creatures (model | family | rig | tris | bones | MB | verify | weaknesses)
(generated by btable.py from the verify results of the exported files, 2026-11-21; (H1)..(H4) = built by that helper;
swarm = built to the 4,000 tris / 2.5 MB / 28 bones budget; xK = enlarged against the brief size for readability)
mon_wraith_gasp | wraith | F | 3170 | 15 | 1.27 | PASS | x1.55, swarm build; the ghost face is opaque glowing geometry (only its cowl and smoke trail are ghost matter); the pool of light on the ground is a game light (meta `light`), not part of the model; glass shards opaque
mon_dusk_hound | dusk | B | 3969 | 28 | 1.49 | PASS | swarm build (lite skeleton, 1024 atlas); chest / haunch forms simple; mane tufts are flat cones; death pose legs stiff; leap IK stretch 0.17 m
mon_dusk_boar | dusk | B | 7612 | 29 | 3.96 | PASS | (H2) mouth glow is a thin bright line, not a soft glow; bristles are flat cones; plain hooves; Attack1 stretches the hind legs 3 cm
mon_dusk_yak | dusk | B | 12268 | 33 | 4.35 | PASS | (H2) face is a dark mass under the forelock; fringe = rigid ropes on 4 skirt bones (no finer secondary motion); snow drift simple
mon_dusk_lynx | dusk | B | 6100 | 30 | 3.31 | PASS | (H2) fur tufts are chunky cones; face more dog than cat; death pose legs stiff; pounce IK stretch 0.28 m
mon_dusk_lizard | dusk | B | 6648 | 32 | 4.12 | PASS | (H2) stands 2 cm clear of the ground rather than belly-down; sail is a thin line from straight above; tail straight at rest; Attack2 is a hop-spin (feet in the air)
mon_dusk_crow | dusk | E | 10228 | 13 | 3.43 | PASS | (H3) beak long (hornbill rather than crow); feathers are flat rigid slabs (coverts read as tiles close up, wings fold as stacked plates in Hit); ruff / body tufts are cones; wingspan 2.2 m (oversized on purpose); one-shot clips end in the stock hover pose, not exactly Idle frame 0 (as the bat) Since then: slate feathers with a blue sheen, primaries smoulder at their tips.
mon_dusk_bat | dusk | E | 7292 | 15 | 4.01 | PASS | x1.2; wing membrane stretches at the body on the full upstroke; one-shot clips end in the hover pose, not exactly Idle frame 0
mon_dusk_gadfly | dusk | E | 5618 | 19 | 3.33 | PASS | (H3) each wing is a fan of three blades (my reading of "blurred"; they also alternate per frame); globe is opaque (vein-net emission), cannot turn amber when fed (only swells: bone `abdomen` scale); globe trails 45 deg back instead of hanging straight down; legs are stiff two-segment rods
mon_dusk_moths | dusk | F | 3584 | 26 | 1.86 | PASS | (H4) 448 moths, each two flat kites (folded paper close up, no bodies); the ball is flattened (2.3 m high, 3 m wide) to clear the ground; Death squashes the clusters flat with a non-uniform bone scale (the dead moths' normals may shade wrong in the engine); wing beat = 4-frame cycle of the flap bones, judged from stills only
mon_rime_packbeast | frost | B | 7986 | 31 | 4.09 | PASS | (H2) head simple; whole load rigid on one bone; Attack1 "kick" is a fore-hoof strike while rearing; pawing hoof dips 2.6 cm
mon_cf_flicker | coldfire | F | 342 | 21 | 1.03 | PASS | (H4) built 1.14 x (0.72 m flame = 1.4 x "knee-high") and still small in the game view; glass is opaque: the orange "pinpoint deep inside" is painted on the belly front and back (not seen from the sides); few, large facets; Attack2 / Death swap the flame for ten loose shards (bone scale 0.001)
mon_cf_mite | coldfire | C | 3206 | 23 | 3.57 | PASS | (H1) glass is opaque (no transparency in the shader); the flames bend as skinned geometry; tip light is subtle
mon_sexton_grub | sexton | C | 1604 | 11 | 1.08 | PASS | x1.25, swarm build, built on the looper (rig C family); bounding Run (each end planted for one frame of eight); stub legs do not move
mon_sexton_beetle | sexton | C | 4104 | 29 | 3.52 | PASS | head shield reads lumpy close up; legs thin for a beetle
mon_sexton_digger | sexton | C | 4664 | 25 | 3.65 | PASS | (H1) walks on four legs (spades are free limbs); dark on dark at game size; ridges of the wing-case are buried under dust and clods; both attacks dig below the ground (under_ok) Since then: paler grave soil and dust so it shows on dark ground.
mon_sexton_matron | sexton | C | 9036 | 34 | 4.24 | PASS | (H1) the opening lid passes through the root lashings in Attack2; grubs are simple; wing-case flaps are almost hidden
mon_char_root | char | D | 2760 | 13 | 3.66 | PASS | slender; rootlets are cones that move with the body only; no head by brief; does not travel above ground (Walk / Run are a weaving column)
mon_ash_worm | ash | D | 4632 | 11 | 4.63 | PASS | Death sinks back into the hole leaving a stump; the sieve holes are glowing discs, not real holes; Walk / Run = leaning weaving column
mon_shroud_ling | shroud | C | 2896 | 23 | 1.17 | PASS | (H1) built 1.25 x brief size; legs long, not "short" (10 m/s model speed needs the reach); faceted 5-sided legs close up
mon_shroud_weaver | shroud | C | 5672 | 34 | 3.67 | PASS | (H1) six walking legs + two combs; the shroud is a hanging rag, not a long trailing cloth
mon_shroud_dropper | shroud | C | 2876 | 32 | 1.61 | PASS | (H1) rest pose shows the 6 m thread (hidden by bone scale in all clips but Spawn); legs are plain tapering tubes
mon_shroud_matron | shroud | C | 13016 | 33 | 4.54 | PASS | (H1) green skin shows on the lower half of the abdomen (cocoons thin out there); Death is a belly collapse, not the family's leg curl; the laid sac just shrinks away after the hit
mon_effigy_hound | effigy | B | 4966 | 27 | 3.51 | PASS | (H2) lumpy bulldog rather than a crisp brick; notch reads as a saddle; Run is a 6-frame patter (short legs at 5.4 m/s)
mon_bog_toad | mire | B | 7240 | 30 | 3.43 | PASS | (H2) belly sack is a rigid glowing ellipsoid (slosh = bone rotation / scale); belly slam barely squashes; death undramatic
mon_lamp_lure | lamp | F | 4196 | 19 | 3.63 | PASS | (H4) stays amber when attacking (emission is baked); stalk tan rather than iron-dark; Attack2 reads mainly through the roof flipping up; the dead stalk lies in one long wavy line
mon_lamp_crawler | lamp | C | 3636 | 27 | 1.42 | PASS | (H1) legs are hidden under the skirt, so Walk / Run read only through the body ripple; the curl opens gaps between the plates
mon_lurk_leech | lurk | D | 1848 | 10 | 0.76 | PASS | x1.3, swarm build; small and dark on screen; belly rings read like wood grain close up; Run is bounding loops, not a crawl
mon_lurk_eel | lurk | D | 6012 | 19 | 3.23 | PASS | x1.2; no feet, speeds nominal; Attack2 is a low sweep of the body (the tail stays buried); in Death it sinks back into its hole and only a stump and the head stay above ground; mud ripples are geometry rings
mon_lurk_spitter | lurk | D | 6724 | 20 | 3.68 | PASS | frond tips glow while it is dead; the frill is rigid sheets on six fold bones; Walk / Run frames differ little (tail wave + steady head)
mon_lurk_elder | lurk | D | 7152 | 13 | 3.1 | PASS | giant; flute-holes are painted discs that do not open and close; clinging leeches are static; tail wave is weak on the short fat tail
mon_rust_helm | rust | C | 3788 | 25 | 1.33 | PASS | (H1) built 1.4 x helmet size (0.47 m long in game) and still small on screen; crest reads as stiff spikes, not hair; legs long for "thin legs"
mon_rust_stinger | rust | C | 7568 | 42 | 4.29 | PASS | (H1) tongs are chitin-coloured (sugar-tong shape only); breastplate is a simple domed plate with notches; verdigris sting is not emissive
mon_rust_gear | rust | C | 3532 | 32 | 3.47 | PASS | (H1) in the tucked pose the hermit's body and leg stubs stick out of both hub faces; teeth are plain blocks
mon_rust_jar | rust | C | 5848 | 34 | 3.74 | PASS | (H1) jar reads more barrel than tall amphora; the mouth light is hidden from most side views; wire is a static tangle on root-level bones
mon_rust_locust | rust | E | 5564 | 25 | 3.35 | PASS | (H3) brass leaves are plain flat sheets of one tone (reads olive under a grey sky); hind wings are fans of three blades; no violet static (not built); small legs simple; small on screen (0.8 m body) Since then: faint violet static towards the tips of the brass leaves.
mon_engine_heliostat | engine | F | 8244 | 14 | 3.33 | PASS | (H4) the dish faces up and forward (30 deg from horizontal, 3.3 m to the vane), not level at the hero; pitch range only 25..47 deg (lower it would cut the turntable); Death = leans 53 deg propped on its dish rim, the buckled leg hangs limp; no braces between the legs
mon_bell_hanging | bell | F | 8524 | 15 | 3.45 | PASS | (H4) legs longer / heavier than life (0.7 m out of the mouth) so they show under the bell from the game camera; the lip rim reads white rather than faint; shroud collar and the ringer's trunk are simple; in Death the legs slide out of the lying bell without a visible rope
mon_fiend_wing | fiend | E | 5988 | 19 | 3.87 | PASS | (H3) glass is opaque; the crack net covers the whole wing (dimmer aft) - much emissive area; spars banded like a snake; body tiny and simple; the cinder comes back by swelling at the tail tip (bone scale); the corpse keeps glowing
mon_fiend_maw | fiend | B | 6192 | 25 | 4.49 | PASS | (H2) firebox only 0.2 m deep (boolean cavity + glowing block); body cracks coloured, not emissive; slits are bars on the door
mon_forge_eye | forge | F | 7046 | 10 | 3.28 | PASS | (H4) the eye looks 40 deg up, not level; lever = mast + arm with the hanging weight + cross-bar for the chains: 2.4 m to its top, not squat; the Idle crack shows its glow from the front, not from the game camera; Death keeps the lid shut (glow hidden) and only tilts the stump 9 deg
mon_forge_hound | forge | B | 3528 | 28 | 1.53 | PASS | (H2) clinker yokes boxy; trap head thin from the side; rod legs thin; leap IK stretch 0.17 m (as the hound proof)
mon_hush_ribwalker | hush | C | 1832 | 18 | 0.83 | PASS | (H1) Death flattens to low hoops rather than a flat line; ribs are simple tapering blades
mon_hush_beast | hush | B | 4935 | 24 | 2.23 | PASS | (H2) crazing of the chalk look rather visible; long thin triangles round the hole; Spawn scales the bone `hip` (uniform)
mon_hush_serpent | hush | D | 1752 | 13 | 1.47 | PASS | plain by brief (one chalk look); the needle-eye is small; Idle / Walk / Run turn the whole corkscrew 360 degrees per loop (unchecked in the engine)
mon_hush_wing | hush | E | 4704 | 25 | 2.75 | PASS | (H3) plain by brief (one chalk look, no face); 2.5 m wide x 2.1 m long (kite, not a square diamond); Death / Spawn scale the bone `body` non-uniformly (closes to a white line that stays on the ground) - unchecked in the engine; ribbons are flat strips
