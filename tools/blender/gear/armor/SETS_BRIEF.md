# Authoring armour looks (t1–t3) for one hero class — working brief

You write ONE file per class: `Y:\tools\blender\gear\armor\sets_<class>.py`. It defines `_mats()`, `t1(K)`, `t2(K)`,
`t3(K)` (and, only where asked, `t4x(K)`). Everything else in this folder is shared and FROZEN: do not edit
`gearlib.py gearfin.py gearpiece.py gearkit.py gearbase.py build_set.py build_base.py split_t4.py render_gear.py
poke_test.py t4map.py make_manifest.py` or anything under `Y:\tools\blender\heroes\`. If a shared helper is wrong or
missing, write your own helper function inside your `sets_<class>.py` and say so in your report.

## Rules that are not negotiable
- Blender ONLY through `./rb.sh <script.py> <args>` in this folder (Git Bash; it calls the throttled background launcher
  `Y:\tools\blender\run_blender.ps1`). Never start blender.exe yourself, never open any window, never use a Blender MCP.
  At most two Blender jobs run machine-wide; yours may wait in the queue — run long jobs in the background and wait
  for the completion notice instead of polling.
- Do NOT run `tools\import.ps1`, `make_manifest.py`, `build_base.py`, `split_t4.py`, do not start the game, do not
  commit or push. Do not touch files outside this folder except reading. Look at pictures with the Read tool.
- Bash heredocs longer than a few kB get truncated: write files with the Write tool.
- Original designs only (no recreation of an existing game's art). Dark-fantasy, grim, realistic, grimy.

## What exists (read these first, in this order)
1. `Y:\game\assets\gear\armor\REGIONS.md` — body regions, which slot may hide what, the layering table.
2. `gearkit.py` — the garment kit (`Kit` class). Read all of it: every method is a ready-made garment.
3. `sets_lampknight.py` and `sets_hunter.py` — two finished examples (plate class, leather class).
4. `Y:\tools\blender\heroes\hero_<class>.py` — today's hero: its materials (reusable by key in your looks, they are
   already registered when your functions run), its palette and its signature look = t4. Sheet of today's hero:
   `Y:\shots\chars\hero_<class>_sheet.jpg`. Naked base body: `Y:\shots\gear\armor\<class>_base.jpg` (may be re-rendered).
5. Finished example sheet: `Y:\shots\gear\armor\lampknight_looks2.jpg`.

`from gearkit import *` gives you the Kit plus the whole hero toolkit (`mats`, `add`, `shell`, `robe`, `drape`, `cloak`,
`sleeve`, `belt`, `strap`, `ribbon`, `decal`, `props`, `fur_roll`, `tuft`, `g_*` geometry generators, `w_rigid`,
`w_func`, `w_transfer`, `mirrored`, `chew_edge`, `wrinkle_shell`, `hat_wide`, `g_antler`, `g_feather`, `g_tooth`,
`g_bone`, `g_beads`, `g_chain`, `lantern_parts`, `sstep`, `T`, `S`, `R`, `frame`, `gxf`, `gjoin` ...): see
`heroes\mk.py`, `charlib.py`, `kit_cst.py`, `kit_mel.py`, `kit_twn.py` for signatures when you need one.
Coordinates: metres, rest pose = T-pose, +Z up, the character faces -Y, +X is the character's LEFT, arms along X at
z ≈ 1.44 (shoulder x 0.19, elbow 0.47, wrist 0.74), waist z ≈ 1.03, hip joint 0.93, knee 0.53, ankle 0.10, head
1.57–1.80. Model the left side and mirror (`mirrored(o)`), or use Kit helpers that mirror for you.

## The look functions
```python
def t2(K):
    _mats()                               # your mats.cloth / leather / metal / fur / bone / emis definitions
    with K.slot("helm"):   ...            # every part must be created inside a slot block
    with K.slot("chest"):  ...
    with K.slot("gloves"): ...
    with K.slot("pants"):  ...
    with K.slot("boots"):  ...
```
All five slots must exist in every look. Each slot is baked and exported on its own
(`game/assets/gear/armor/arm_<class>_<slot>_<tN>.glb`), with ONE atlas per piece.

Materials: `mats.cloth(key, base=(r,g,b) linear, var, ash, ash_h, folds, weave, worn, bands=[(v0,v1,colour)], scan=,
tight=, hem=)` with scan one of linen burlap canvas wool twill velvet brocade fleece rope; `mats.leather(key, base,
worn, ash, scan=leather|leather_b|leather_old|hide)`; `mats.metal(key, base, rough=(lo,hi), rust_amt,
pattern=plate|hammered|mail|brushed|scale, ash, soot)`; `mats.fur(key, base, tip)`; `mats.bone(key, base, stain)`;
`mats.wood(key, base, luv_grain=False)`; `mats.emis(key, colour, strength)` (keep emissive parts tiny: heroes must
not glow brighter than their surroundings). Albedo values are dark: look at the hero script's numbers and stay in that
range. Prefix your new keys with two letters of the class so they cannot collide with the hero's keys. Tier styling:
t1 worn 0.8–0.95, heavy ash/rust, burlap / old leather / rusty hammered iron, torn edges (`chew=`); t2 plain, sound,
moderate wear; t3 better cloth (wool, velvet, brocade bands), brighter or trimmed metal, fur, small ornaments.

## Hard technical rules
- The game draws characters with BACK-FACE CULLING. Hanging / lofted cloth needs thickness: `K.skirt`, `K.panel`,
  `K.cloak`, `K.capelet`, `robe(thick=)`, `drape(thick=)` all have it; when you `add()` a sheet yourself pass
  `solidify=0.005` or call `solid(o, 0.005)` (never the "even" solidify on torn or sharply folded sheets: it shoots
  spikes — that is what `solid()` avoids). Body shells (`K.shell`, `K.tunic`, `K.pants`, `K.gloves`, `K.boots`,
  `K.sleeves`, `K.cap` ...) are single sheets lying 5–18 mm over the skin and are fine as they are.
- Body shells have the skin's own weights and never tear. Lofts and rigid plates are where deformation fails: keep
  rigid plates off the joints or weight them half / half across the joint as the Kit does, keep skirts loose.
- Layering (REGIONS.md §2): pants top at or below z 1.03 and at most ~11 mm off the skin there; the chest piece owns
  the whole waist zone (belts, sashes, skirts, tabards, pouches, anything hung from a belt) and its hem must come down
  to z ≈ 1.0 or lower, 15 mm or more off the skin (`K.tunic` does this by itself); tight sleeves at most 7 mm, glove
  cuffs / bracers 9–18 mm, wide sleeves 25 mm+; boot shafts 13–18 mm over pants legs of at most 11 mm. Chest pieces
  must not put close-fitting shells below z 1.0 (the base body's loincloth cord and flaps are there when no pants are
  worn; skirts there need 22 mm+ clearance — `K.skirt` defaults are fine).
- Which body regions a piece hides is computed automatically from what it covers; skin that stays visible inside a
  hidden region is carried along automatically. You only steer it when needed with
  `K.opts["<slot>"] = dict(ray=0.10, force_hide=("hair",), never_hide=("hips", "wrap_loin"))`. A hood / cap / helm
  that covers the scalp must end up hiding `hair`: check the PIECE log line (`hides=[...]`).
- Budgets: roughly 1.5–5k triangles per piece, at most ~9k for a chest, a full look under ~24k. `dbg_parts.py`
  prints them. Use `K.decimate["chest"] = 0.8` if a piece is too heavy.
- No part may be empty or have far-off vertices (the build warns: `EMPTY PART`, `WARNING part with far-off vertices`,
  `UNWEIGHTED`): fix the cause.

## Workflow (all commands from `Y:\tools\blender\gear\armor`, Git Bash)
```
./rb.sh dbg_parts.py <class> t1                 # 15 s: builds the parts only, lists slot / tris / bbox, catches errors
./rb.sh build_set.py <class> t1 --preview       # ~2 min: low-res bake + export of the five pieces
./rb.sh render_gear.py <class> wip base t1 t2 t3 --poses rest,restback,close,atk,run2,back --cell 640x720
        # sheet Y:\shots\gear\armor\<class>_wip.jpg, single frames in _frames\ (Read them; frames are sharper)
./rb.sh poke_test.py <class> t1 t2 t3 t4        # counts vertices that come through another worn piece per pose
```
`rb.sh` prints only `[hero]` log lines and errors; the full output is in `_work/last_<script>_<args>.txt`.
Outfit syntax for render / poke: `base`, `t1`..`t4`, or `label:helm=t1,chest=t3,gloves=t2,pants=t4,boots=t1`.
Poses: rest restback restclose close idle back atk atk2 run run2 sprint roll sword cast punch block legs top game.
The renders cull back faces like the game does: holes you see there are holes in the game.

Iterate: look at every sheet critically (silhouette at game size AND the close-up), fix, rebuild. Things to hunt:
spikes, parts floating off the body, parts sunk into the body or into each other, see-through sheets, skin gaps
between slots when a full look is worn, a look that reads the same as another look of the class, clay-smooth
surfaces, colours that glow. Poke numbers: today's hero look (t4) is the yardstick — a new look should not be clearly
worse than t4 of the same class in the same clips.

## Deliverables per class
1. `sets_<class>.py` (docstring at the top: the class identity and one line per look).
2. Final builds WITHOUT `--preview`: `./rb.sh build_set.py <class> t1`, `t2`, `t3` (and `t4` if you were asked for
   `t4x`). Check each `PIECE` line.
3. Sheets with the final files:
   `./rb.sh render_gear.py <class> looks base t1 t2 t3 t4 "mix:helm=t2,chest=t1,gloves=t3,pants=t2,boots=t4" --poses rest,restback,idle,atk,run,back`
   `./rb.sh render_gear.py <class> poses t1 t2 t3 t4 --poses atk2,atk,run2,sprint,roll,close --cell 640x720`
   (if a t4 slot does not exist for the class the mix simply shows the body there).
4. `./rb.sh poke_test.py <class> t1 t2 t3 t4 "mix:helm=t2,chest=t1,gloves=t3,pants=t2,boots=t4"` and keep the `POKE` lines.
5. Your final message: per class, one line per look (what it is), triangle counts per piece, the POKE totals next to
   t4's, the weakest pieces and every deformation problem you saw and could not fix, plus any shared-kit problem.
