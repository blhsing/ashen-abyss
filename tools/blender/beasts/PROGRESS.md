# Beasts (rigs B-F, 46 non-humanoid creatures) - progress

## STATE WHEN PAUSED (2026-11-15 ~19:20, lead's order: stop for the playable prototype)
- **0 of 46 creatures built. Nothing of this job is in `Y:\game\assets\chars`, no `rigs_bf_meta.json`, no sheets, no import.**
- No Blender job of this agent is running or queued. The only Blender run so far was a 20 s API probe.
- What exists is the generator core, written but **NOT YET RUN in Blender** (expect first-run bugs):
  - `blib.py`  core: skeleton builder (`Sk`), numeric rest rig (`Rg`), pose engine (`Pose`: FK, two-bone IK, `leg()`
    for 2/3/4-bone legs with shoulder/hip glide, `aim_world`), `gait_foot`, `plant_legs`, `wbones` (weights by
    distance to bone segments), `Beast` (subclass of monlib.Creature: clips -> plain bone keyframes -> one NLA track
    per clip, floating-part audit per connected piece in rest pose and in sampled frames of every clip, ground
    bounds per clip, planted-foot drift check, bake through the monsters kit `looks.bake_and_assign`, .glb export
    with embedded animations, `out\<model>.meta.json`).
  - `bparts.py`  new look kinds registered into `looks.KINDS` (chitin, scales, slime = wet mucous skin, bfur =
    fur along a flow direction with ash clots / frost, membrane, chalk = Pale Hush white, bark = charred bark with
    ember fissures, glow = translucent skin lit from inside) + geometry parts (`tubep`, `blob`, `cone`, `seated`
    / `tooth_row` = spikes sunk into the nearest host surface, `tufts` = fur locks with copied weights, `organic` =
    union + voxel remesh + decimate + weight transfer, `sheet_solid`, `fan`, `plate_row`, `ring`, `seg_tube`, `dome`).
  - `rig_b.py`  quadruped rig: `quad_J` (joint table from a few proportions), `quad_skel`, `Quad` (body / spine /
    neck / jaw / tail / ears helpers, `gait` with WALK4 / TROT / GALLOP / BOUND phase tables, `idle`, `bite`, `leap`,
    `hit`, `death_side` (auto ground fix), `spawn_rise`), `quad_flesh` (organic body from a shape table),
    `add_tail`, `add_claws`, `ear_sheet`.
  - `scratch\probe_api.py` (+ `probe.glb`): the API probe that passed. `scratch\patch1.py`: already applied, can go.
- NOT written yet: `spec_b.py` (first spec = `dusk_hound`, numbers are in the notes below), `bbuild.py` (driver),
  `bverify.py`, `bshots.py`, rigs C, D, E, F.

## Facts established (do not re-derive)
- Blender 5.2: `action.fcurve_ensure_for_datablock(arm, path, index=i)` works after `arm.animation_data.action = act`;
  one NLA track + strip per action (track muted, `strip.action_slot = act.slots[0]`), then export with
  `export_animation_mode='ACTIONS', export_nla_strips=True, export_force_sampling=True, export_optimize_animation_size=True,
  export_optimize_animation_keep_anim_armature=True` gives glTF animations named exactly `Idle`, `Walk`, ... with
  T/R/S channels for every bone (constant channels collapse to 2 keys). 61 keys at 30 fps = 2.0 s. Verified in the probe.
- Facing: build facing **-Y** in Blender (+X = the creature's left), exported +Y up -> the game's +Z forward, same as
  the humanoid files (monlib builds in the UAL armature space, facing -Y).
- `CharModel.Convert` / `char.gdshader`: only the prefixes `emis*` (pulse, energy clamp) and `spectral*` (spectral
  shader, ignores textures) change shading; `skin_ / hair_ / bone_ / leather_ / metal_ / rock_ / cloth_` all get the
  same PBR path. So "chitin under bone_ or leather_" makes no difference in the engine: **chosen `bone_`** (not yet
  looked at in the engine). Fur = `hair_`.
- The game shader is `cull_back` for every material (the glTF doubleSided flag is lost): thin sheets (wing
  membranes, fins, ears, moth wings) need real two-sided geometry -> `sheet_solid` / `Part.solidify` (closed slab) or
  `blib.two_sided` (duplicate faces; `Beast._make_objects` therefore does NOT run a mesh-wide normal recalc).
- Emission works on any slot whose look returns `emit` (the kit bakes an emission texture); `emis_` only adds pulse.
- `Y:\tools\blender\monsters\verify.py` with no ids globs every `mon_*.glb` and expects the humanoid skeleton and NO
  animations: the 46 beast files will FAIL there and would be written into `manifest_monsters.json`. The rig-A artist /
  lead must exclude them (the beasts get their own `bverify.py`).
- Plain python for file chores: `"C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" -I`.
  Patching files through bash heredocs breaks on quotes: use the Edit / Write tools.

## Design decisions per rig (taken; revise only with a reason)
- **Sizes.** Model is authored at brief size / `scale` (the game multiplies by `MonsterDef.Scale`): e.g. lurk_elder
  "rears three metres" at scale 1.8 -> 1.67 m in the file. `run_speed` in the meta = bestiary `speed` / `scale`,
  `walk_speed` about 0.3-0.4 of that; both are exact for the clip (stride = v x duty x period by construction).
- **Root** bone `root` at the origin, never keyed; whole-body motion goes on the first child (`hip` / `body`).
- **Every clip keys every bone** (rotation always; location / scale when used), 30 fps, loops have frame N = frame 0.
  One-shot clips start (Attack / Hit: also end) in the rest stance = Idle frame 0.
- **B quadruped** - bones as in `rig_b.py` (31 + extras). Legs = 4 bones, IK on upper + lower to the wrist / hock,
  pastern leans towards the body in stance and tucks in swing, paw flat; front legs may glide 7 cm at the shoulder
  (scapula), hind 4 cm. Walk = 4-beat lateral walk, Run = rotary gallop (hounds, cats) or trot (boar group), with
  crouch to gain reach. Sprawlers (lizard, toad) use the same bone names with a sideways joint table (to write).
  Body = union/remesh of tubes and blobs; lower jaw is a separate rigid piece tucked into the snout; teeth are
  `seated` on the remeshed head (upper) / jaw (lower); thin tail and ears are separate.
- **C arthropod** (to write) - `body`, `abdomen1..2`, `head`, `mandible_l/r`, `antenna_l/r`, legs
  `leg<i>_<s>_a/b/c` (rigid chitin segments, one per bone, knuckles hide the joints; IK on a + b, c keeps its world
  angle), scorpion adds `claw_*`, `tail1..6`. Gaits: alternating tripod (6 legs) / tetrapod (8), wave for the
  woodlouse. sexton_grub is a legless inchworm chain although it is listed under rig C. Swarm types: 2 bones per leg.
- **D serpent** (to write) - one chain `s1..sN` (10-14) + head / jaw plates / frill bones. Burrowers (ash_worm,
  char_root, lurk_eel, hush_serpent) are columns standing out of the ground with a buried base; their Walk / Run is
  the leaning, ploughing column. Leech = vertical looping, spitter / elder = lateral undulation with a steady reared
  front; reported speed = wave speed (wavelength x frequency).
- **E flyer** (to write) - `body`, neck / head / jaw, `wing_<s>1..3`, tail, dangling legs. **Hover height is baked
  into the model and every clip** (rest pose hovers above the origin, Death falls to the ground and ends as a corpse
  on z = 0, Spawn drops in): the meta carries `fly_height` and `height_baked: true`, the game must NOT add it again.
  Tell the lead / roster engineer when the proofs go out.
- **F rigid / floating** (to write) - small per-creature skeletons (lantern: body, door hinge, handle, chain, wisp;
  flicker: three tongues; bell: body, swing, hanging legs, chains; heliostat: yaw ring, dish pitch, counterweight;
  forge-eye: lid arm, chains; lure: stalk chain, hook, cap, gills; moths: ~14 cluster bones each with two "flap"
  child bones that translate the wing tips, about 300 two-sided moths). Floaters are authored at their float height;
  turrets' Walk / Run = Idle (to be said in the meta).
- **Textures**: one atlas, 2048 regular / 1024 swarm; normal JPEG q90 4:4:4, ORM q84, base colour q88 (the kit's
  q95 normals would break the 6 MB budget); raise `C.meta['_qn']` per creature if there is room.

## dusk_hound spec numbers (the first proof, not yet written as code)
J = quad_J(L=0.60, hs=0.66, hh=0.64, wf=0.08, wh=0.085, neck=(0.24, 0.17), head=0.28, sag=0.012, tail=(0.46, 4, -0.7)).
torso (y, zc, rv, rh): (0.40,0.55,0.05,0.045) (0.30,0.545,0.10,0.085) (0.10,0.565,0.085,0.072) (-0.10,0.53,0.135,0.095)
(-0.27,0.515,0.15,0.10) (-0.37,0.56,0.085,0.065); neck: (-0.30,0.615,0.085,0.065) (-0.43,0.725,0.06,0.05)
(-0.545,0.815,0.052,0.046); skull blob (0,-0.575,0.835) r (0.052,0.07,0.05); snout tube (0,-0.58,0.815)
(0,-0.70,0.795) (0,-0.815,0.776) rv 0.04/0.03/0.022 rh 0.042/0.034/0.027; jaw (rigid, bone jaw) tube (0,-0.585,0.775)
(0,-0.70,0.762) (0,-0.80,0.75) rv 0.02/0.016/0.011 rh 0.036/0.03/0.022. Leg radii (fore-aft, side) front
(0.055,0.04) (0.055,0.042) (0.036,0.03) (0.022,0.02) (0.02,0.02); hind (0.08,0.045) (0.085,0.05) (0.05,0.036)
(0.022,0.02) (0.02,0.02); paws (0.032,0.05,0.022) / (0.03,0.048,0.022); haunch blob (0.075,0.27,0.46) r
(0.055,0.10,0.11); shoulder blob (0.08,-0.28,0.50) r (0.045,0.07,0.10). Rib ridges by sculpt on the flank. Bat ears
0.19 m (ear_sheet), ash mane = ~230 tufts on the dorsal strip + 40 clots, whip tail 0.022 -> 0.006, two emissive heat
pits + rims at the snout tip, 7 teeth a side + canines, 3 claws a paw. Looks: hide = skin (pale grey), maw, ear =
membrane, mane = bfur ash 0.9 (hair_mane), teeth / claws = bone (bone_teeth), pits = emis (emis_pits).
Clips: Walk v 0.8 T 0.8 duty 0.66 WALK4 crouch 0.03; Run v 6.2 T 0.40 duty 0.22 GALLOP crouch 0.06 flex ~14
pitch ~6; Attack1 bite; Attack2 leap; Hit; death_side; spawn_rise.

## Next steps (in order)
1. Write `spec_b.py` (dusk_hound), `bbuild.py` (ids, `--nobake`), `bshots.py` (import the .glb, game-camera + 3/4
   tile, 9-tile strip: Walk x3, Run x3, Attack1 hit, Attack2 hit, Death end; tiles 400 px, labels via
   `Y:\tools\sheet.ps1`), `bverify.py` (clips, loops closed, budgets, bounds, NaN, material prefixes, embedded JPEGs,
   root unkeyed, foot drift from the exported file).
2. First run with `--nobake` to debug the core, then bake; fix what the strip shows. That proves rig B.
3. Proofs for C (sexton_beetle), D (lurk_eel or lurk_leech), E (dusk_bat), F (wraith_gasp); WebP per proof
   (`Y:\tools\make_webp.py`); message the lead with sheet / strip / WebP paths + meta JSON + the fly-height convention
   + the verify.py glob caveat.
4. Mass production (optionally two helpers, e.g. C to one, D+E to another), `import.ps1` after each batch,
   `rigs_bf_meta.json` merged from `out\*.meta.json`, family sheets `Y:\shots\chars\beast_<family>.jpg`, strips
   `beast_<model>_anim.jpg`, one line per creature below.

## Creatures (model | family | rig | tris | bones | MB | verify | weaknesses)
(none yet)
