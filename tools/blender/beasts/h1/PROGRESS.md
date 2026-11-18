# H1 (rig C arthropods, 13 creatures) - progress

Module `spec_h1.py` (this folder), work dir `h1\out`, shots `h1\out\shots`. Rig module `..\rig_c.py` (extended, see below).
Build / shots / verify / strip commands: `..\HELPERS.md` with hN = h1. Debug: `bb.ps1 dbg_parts.py "<id> <bone>" -Dir <this folder>`
lists the parts that come close to the pieces on a bone (for audit warnings).

## State: DONE (2026-11-19)
- All 13 are baked into `Y:\game\assets\chars\mon_<id>.glb`, pass `bverify.py`, build with a silent audit, were looked at
  (tile / game / dbg1 / dbg2) and have a strip in `Y:\shots\chars\beast_mon_<id>_anim.jpg`. Meta files: `h1\out\mon_<id>.meta.json`
  (all from the baked builds). Overview sheets: `h1\out\shots\h1_tile_sheet.jpg`, `h1_game_sheet.jpg`.
- NOT done (lead's job): `import.ps1`, `bmeta.py` merge (`python -I bmeta.py Y:\tools\blender\beasts\h1\out`).
- `sexton_beetle` (spec_c.py) still builds with the extended `rig_c.py` (checked as a --nobake build: 4104 tris, 29 bones, audit silent).
- No detached processes. `out\spec_h1.backup.py` is a copy of the final spec (the spec was once truncated by a bad patch
  script and rebuilt from the session; the rebuilt file compiled byte-identical to the last imported module).

## Lessons (for whoever touches this again)
- The audit starts its attachment tree from SKINNED geometry; without any it starts from the largest rigid piece, which
  gives nonsense hosts (legs "hosted" by a helmet or a thread). Make the main body skinned to one bone with `core()`.
- Leg roots must sit about 3-5 cm inside the host: nearer the surface the tracked contact points leave it when the femur
  turns, deeper than 6 cm the audit no longer finds the host surface at all (it only looks 6 cm far).
- Parts that are hidden by bone scale or that fall off (thread, egg sac, coffin lid, grubs) are added with `free=True`.
- Small fast creatures: Run = 6 frames, duty 0.18-0.25; the legs need the reach for a stride of v x duty x T. With so
  few frames the gait phases must lie on the frame grid (multiples of 1 / frames) and the first / last swing frame must
  be more than 1.2 cm above the ground, otherwise the foot-drift check counts a swing frame as planted.
- Never patch the spec with a script that opens it with 'w' directly: write a `.new` file and `os.replace` it.

## Extensions made
- `rig_c.py` (additive, defaults unchanged): `Arth.death_flip(..., roll=178)`, `Arth.death_drop(..., pitch=3)`,
  `Arth.claws(p, open, spread, lift, fold, side)`, `Arth.spawn_drop(dur, height, land, line, squash, tuck, extra)`.
- `spec_h1.py` helpers: `core`, `enlarge` / `grow` (build at brief size, deliver K times larger), `path_t`, `lock`, `wpath`,
  `domed_plate`, `mirror_outline`, `helm_shell`, `dome_pt`, `spider_front`, `fangs`, `spider_jaws`, `hide_in` (bones scaled
  away in all clips but some), `burial_mask`, `ring_prism`, `scaled`, `ubar`, `grub`, `spade_hand`, `cog`, `flame`, `tergite`;
  look kind `shard` (the `ice` look + light towards the tips).

## Creatures (model | family | rig | tris | bones | MB | verify | honest weaknesses)
mon_rust_helm | rust | C | 3788 | 25 | 1.33 | PASS | built 1.4 x helmet size (0.47 m long in game) and still small on screen; crest reads as stiff spikes, not hair; legs long for "thin legs"
mon_rust_stinger | rust | C | 7568 | 42 | 4.29 | PASS | tongs are chitin-coloured (sugar-tong shape only); breastplate is a simple domed plate with notches; verdigris sting is not emissive
mon_rust_gear | rust | C | 3532 | 32 | 3.47 | PASS | in the tucked pose the hermit's body and leg stubs stick out of both hub faces; teeth are plain blocks
mon_rust_jar | rust | C | 5848 | 34 | 3.74 | PASS | jar reads more barrel than tall amphora; the mouth light is hidden from most side views; wire is a static tangle on root-level bones
mon_cf_mite | coldfire | C | 3206 | 23 | 3.57 | PASS | glass is opaque (no transparency in the shader); the flames bend as skinned geometry; tip light is subtle
mon_sexton_digger | sexton | C | 4664 | 25 | 3.57 | PASS | walks on four legs (spades are free limbs); dark on dark at game size; ridges of the wing-case are buried under dust and clods; both attacks dig below the ground (under_ok)
mon_sexton_matron | sexton | C | 9036 | 34 | 4.24 | PASS | the opening lid passes through the root lashings in Attack2; grubs are simple; wing-case flaps are almost hidden
mon_shroud_weaver | shroud | C | 5672 | 34 | 3.67 | PASS | six walking legs + two combs; the shroud is a hanging rag, not a long trailing cloth
mon_shroud_ling | shroud | C | 2896 | 23 | 1.17 | PASS | built 1.25 x brief size; legs long, not "short" (10 m/s model speed needs the reach); faceted 5-sided legs close up
mon_shroud_dropper | shroud | C | 2876 | 32 | 1.61 | PASS | rest pose shows the 6 m thread (hidden by bone scale in all clips but Spawn); legs are plain tapering tubes
mon_shroud_matron | shroud | C | 13016 | 33 | 4.54 | PASS | green skin shows on the lower half of the abdomen (cocoons thin out there); Death is a belly collapse, not the family's leg curl; the laid sac just shrinks away after the hit
mon_lamp_crawler | lamp | C | 3636 | 27 | 1.42 | PASS | legs are hidden under the skirt, so Walk / Run read only through the body ripple; the curl opens gaps between the plates
mon_hush_ribwalker | hush | C | 1832 | 18 | 0.83 | PASS | Death flattens to low hoops rather than a flat line; ribs are simple tapering blades
