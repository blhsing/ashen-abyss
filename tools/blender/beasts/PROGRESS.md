# Beasts (rigs B-F, 46 non-humanoid creatures) - progress

## STATE WHEN PAUSED (2026-11-19, lead's order: stop, the game is made playable with what is complete)
- **28 of 46 creatures are in `Y:\game\assets\chars`** (complete files with the 8 embedded clips), all re-verified from
  the exported files with `bverify.py` just before the pause: 27 PASS; `mon_dusk_hound` fails ONE check only (4019
  triangles against the 4000 swarm budget I put it under as a pack swarmer; clips, loops, ground, feet, materials all
  pass). `rigs_bf_meta.json` = exactly these 28 models (flat `{model: {...}}`), every entry has all contract fields.
  `import.ps1` was run after the last file. No Blender job is running or queued; both helpers have ended.
- **Per rig:** B quadruped 11 / 11 done (hound = mine, 10 by helper H2 in `h2\`). C arthropod 14 / 15 done (beetle =
  mine, 13 by helper H1 in `h1\`; missing: `sexton_grub`). D serpent 1 / 7 (`lurk_eel`). E flyer 1 / 6 (`dusk_bat`).
  F rigid / floating 1 / 7 (`wraith_gasp`).
- **Not built (18):** D `lurk_leech`, `lurk_spitter`, `lurk_elder`, `ash_worm`, `char_root`, `hush_serpent`; C-family
  `sexton_grub`; E `dusk_crow`, `dusk_gadfly`, `rust_locust`, `fiend_wing`, `hush_wing`; F `cf_flicker`, `dusk_moths`,
  `lamp_lure`, `engine_heliostat`, `bell_hanging`, `forge_eye`. The game uses its stand-ins for them.
- **Written but NEVER RUN:** `spec_d.py` `looper_clips`, `lurk_leech`, `sexton_grub` (the `Looper` class of `rig_d.py`
  and the `Rearer` class have not been executed once: expect first-run bugs). `bshots.py` got the true game-camera view
  (512 px = 6.1 m) after the proofs were rendered: proofs' `_game.png` are still the older, closer view.
- **Not done at all:** the message to the lead with the five proofs (overtaken by the pause), family sheets
  `Y:\shots\chars\beast_<family>.jpg`, the five proof WebPs (`out\shots\<model>_webp\f_*.png` frames exist for the five
  proofs, `make_webp.py` was not run), `Y:\credits\beasts.md` (nothing third-party beyond the monsters kit's CC0 scans,
  already credited in `Y:\credits\monsters.md`), any look at the creatures IN THE ENGINE (no test run of the game was
  made: material prefixes, emission pulse, hover height, `rust_gear` spin, near-zero bone scales are unchecked there).
- Strips exist for all 28: `Y:\shots\chars\beast_mon_<id>_anim.jpg` (the bat strip crops the wings: re-render it with
  the wider framing noted below). Helper overviews: `h1\out\shots\h1_tile_sheet.jpg`, `h1_game_sheet.jpg`.

## NEXT STEPS (in order, when resumed)
1. `bshots.py`: strip framing (sample Walk / Run at 8 phases for the bounds, margin 1.2 so `sheet.ps1`'s 86 % crop does
   not cut wings); re-render the bat strip. Trim `dusk_hound` by 20+ triangles (`tris=2080` -> 2040 in `spec_b.py`).
2. Build `lurk_leech` + `sexton_grub` with `--nobake` first (debug `Looper.place`, `set_homes`), then bake.
3. D: `ash_worm`, `char_root`, `hush_serpent` on `Column` (copy `lurk_eel`); `lurk_spitter`, `lurk_elder` on `Rearer`.
4. E on `Flyer`: `dusk_crow`, `fiend_wing` (bat-like), `dusk_gadfly`, `rust_locust` (`buzz=True` wings), `hush_wing`
   (kite: no flap, flex bones, ribbon tails). F: `cf_flicker`, `bell_hanging`, `dusk_moths` (8 cluster bones x 2 flap
   bones, <= 28 bones), turrets `lamp_lure`, `engine_heliostat`, `forge_eye` (Walk / Run = `Rigid.copy_clip('Idle', ..)`).
5. `bmeta.py Y:\tools\blender\beasts\h1\out Y:\tools\blender\beasts\h2\out`, `import.ps1`, an engine look
   (`run.ps1 -GameArgs "--win --mute --bestiary --new=emberblade --area=wastes --god --spawn=<ids> --shot=... --shot-at=..
   --quit-at=.."`, only when `tasklist | grep -i AshenAbyss` prints nothing), family sheets, WebPs, the report.
- Helpers: H1 owns the additions in `rig_c.py` + `h1\spec_h1.py`; H2 those in `rig_b.py` + `h2\spec_h2.py`, `h2\h2lib.py`
  (their notes: `h1\PROGRESS.md`, `h2\PROGRESS.md`). Rebuild their creatures with
  `bb.ps1 bbuild.py "<ids> --path=Y:\tools\blender\beasts\hN --spec=spec_hN --out=Y:\tools\blender\beasts\hN\out"`.
- Known kit weakness (both helpers hit it): `Beast.audit` judges "inside" by the nearest triangle's normal and only
  searches 6 cm: false "detaches" near convex edges of faceted pieces and for joints buried deep in a fat body.

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
(verify column = `bverify.py` on the exported file, 2026-11-19; "swarm" = built to the 4,000 tris / 2.5 MB / 28 bones budget)
mon_dusk_hound | dusk | B | 4019 | 28 | 1.51 | FAIL (19 tris over the swarm budget; all other checks pass) | swarm build (lite skeleton, 1024 atlas); chest / haunch forms simple; mane tufts are flat cones; death pose legs stiff; leap IK stretch 0.17 m
mon_sexton_beetle | sexton | C | 4104 | 29 | 3.52 | PASS | head shield reads lumpy close up; legs thin for a beetle
mon_lurk_eel | lurk | D | 3026 | 13 | 2.98 | PASS | Attack2 is a low sweep of the column (tail stays buried); mound chunks are simple; no feet, speeds are nominal
mon_dusk_bat | dusk | E | 6170 | 15 | 3.82 | PASS | wing membrane stretches at the body on full upstroke; strip picture crops the wings
mon_wraith_gasp | wraith | F | 1438 | 8 | 1.13 | PASS | swarm build; glass is opaque (cracks glow instead: no transparency in the game shader); the wisp is hidden from behind
mon_dusk_boar | dusk | B | 7612 | 29 | 3.96 | PASS | (H2) mouth glow is a thin bright line; bristles are flat cones; plain hooves; Attack1 stretches the hind legs 3 cm
mon_dusk_lizard | dusk | B | 6648 | 32 | 4.12 | PASS | (H2) stands 2 cm clear of the ground rather than belly-down; sail thin from above; Attack2 is a hop-spin
mon_forge_hound | forge | B | 3528 | 28 | 1.53 | PASS | (H2) swarm; clinker yokes boxy; trap head thin from the side; rod legs thin; no tail / ear bones (4 chain bones)
mon_dusk_lynx | dusk | B | 6100 | 30 | 3.31 | PASS | (H2) fur tufts chunky; face more dog than cat; death legs stiff; pounce IK stretch 0.28 m
mon_hush_beast | hush | B | 4935 | 24 | 2.23 | PASS | (H2) chalk crazing visible; thin triangles round the hole; Spawn scales the bone `hip`
mon_effigy_hound | effigy | B | 4966 | 27 | 3.51 | PASS | (H2) lumpy bulldog rather than a crisp brick; notch reads as a saddle; Run is a 6-frame patter
mon_dusk_yak | dusk | B | 12268 | 33 | 4.35 | PASS | (H2) face a dark mass under the forelock; fringe = rigid ropes on 4 skirt bones; snow drift simple
mon_rime_packbeast | frost | B | 7986 | 31 | 4.09 | PASS | (H2) head simple; load rigid on one bone; Attack1 "kick" is a rearing fore-hoof strike; hoof dips 2.6 cm
mon_fiend_maw | fiend | B | 6192 | 25 | 4.49 | PASS | (H2) firebox only 0.2 m deep; body cracks coloured, not emissive; door = `jaw` bone about Z
mon_bog_toad | mire | B | 7240 | 30 | 3.43 | PASS | (H2) belly sack is a rigid glowing ellipsoid; belly slam barely squashes; death undramatic
mon_rust_helm | rust | C | 3788 | 25 | 1.33 | PASS | (H1) swarm; built 1.4 x brief size and still small on screen; crest = stiff spikes; legs long
mon_rust_stinger | rust | C | 7568 | 42 | 4.29 | PASS | (H1) tongs chitin-coloured; breastplate a simple domed plate; verdigris sting not emissive
mon_rust_gear | rust | C | 3532 | 32 | 3.47 | PASS | (H1) Run = tucked wheel centred at height 0.5, axle along X (the game spins it; meta `wheel_radius`); body sticks out of the hub
mon_rust_jar | rust | C | 5848 | 34 | 3.74 | PASS | (H1) more barrel than amphora; mouth light hidden from the side; wire is a static tangle on root-level bones
mon_cf_mite | coldfire | C | 3206 | 23 | 3.57 | PASS | (H1) glass opaque; flames bend as skinned geometry; tip light subtle
mon_sexton_digger | sexton | C | 4664 | 25 | 3.57 | PASS | (H1) walks on four legs (spades are free limbs); dark on dark; both attacks dig below ground (under_ok)
mon_sexton_matron | sexton | C | 9036 | 34 | 4.24 | PASS | (H1) lid passes through the root lashings in Attack2; grubs simple; wing-case flaps nearly hidden
mon_shroud_weaver | shroud | C | 5672 | 34 | 3.67 | PASS | (H1) six walking legs + two combs; shroud is a hanging rag
mon_shroud_ling | shroud | C | 2896 | 23 | 1.17 | PASS | (H1) swarm; built 1.25 x brief size; legs long, faceted close up
mon_shroud_dropper | shroud | C | 2876 | 32 | 1.61 | PASS | (H1) REST pose shows the 6 m thread (bone scaled to 0.001 in every clip but Spawn); plain legs
mon_shroud_matron | shroud | C | 13016 | 33 | 4.54 | PASS | (H1) green skin shows under the cocoons low down; Death is a belly collapse; laid sac shrinks away
mon_lamp_crawler | lamp | C | 3636 | 27 | 1.42 | PASS | (H1) swarm; legs hidden under the skirt (motion reads through the body ripple); curl opens gaps between plates
mon_hush_ribwalker | hush | C | 1832 | 18 | 0.83 | PASS | (H1) swarm; Death flattens to low hoops; ribs are simple blades
