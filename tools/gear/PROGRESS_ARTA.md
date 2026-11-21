# PROGRESS — ART-A (base bodies + skinned armour)

Scripts: `Y:\tools\blender\gear\armor\` (run with `./rb.sh <script.py> <args>`; log `_build.log`; work files `_work\`).
Outputs: `game/assets/chars/hero_<class>_base.glb`, `game/assets/gear/armor/arm_<class>_<slot>_<tN>.glb`,
`game/assets/gear/gear_manifest_armor.json`, `game/assets/gear/armor/REGIONS.md` (regions, hides, layering, textures).
Contact sheets: `Y:\shots\gear\armor\`. No detached processes are left running between steps unless noted here.

How to rebuild one class: `./rb.sh build_base.py <class>` → `./rb.sh split_t4.py <class>` → `./rb.sh build_set.py <class> t1|t2|t3`
→ `python -I make_manifest.py` (Blender's bundled python) → `tools\import.ps1`.

## For RENDER (integration notes, valid from the first files on)
- Region tokens / mesh names / ownership / layering: `game/assets/gear/armor/REGIONS.md`. Tokens beyond the contract list:
  `neck`, `hips`, `beard`, `brows`, `wrap_loin`, `wrap_chest` (mesh names `body_neck`, `body_hips`, `beard`, `brows`,
  `wrap_loin`, `wrap_chest`). `GearVisual.HideRegion`'s prefix match already handles all of them.
- Every armour file may contain up to three mesh nodes: `arm_…` (armour), `arm_…_emis`, `arm_…_skin`. The `_skin` node is
  the visible remainder of the body regions the piece hides (same skin material + texture files as the base body); it must be
  attached like the armour mesh. Its material name starts with `skin_`.
- t4 pieces and carried skin reference textures of OTHER files by relative URI (`../../chars/hero_<class>_metal_c.jpg`,
  `../../chars/hero_<class>_base_skin_c.jpg`): verified with a headless Godot load (`gd_deps.gd`): the imported scene
  depends on the shared `res://assets/chars/*.jpg`, nothing is duplicated.
- Pants pieces hide `wrap_loin` + `hips`; chest pieces hide `wrap_chest`; helms/hoods hide `hair`.

## Log
- 2026-11-21 lampknight: base body (preview quality) + t4 split done end to end, import + headless load verified.

## 2026-11-21 21:04 PAUSED by user (lead note: agent died on API 503 / quota before writing its own wrap-up)
- Present: 8 base bodies; 135/160 armour pieces. Complete: astromancer, emberblade, forgebody, lampknight, soulbinder, wildsoul (t1-t4); pactbinder t1+t4; hunter t4 only.
- MISSING (25): hunter t1/t2/t3 (15), pactbinder t2/t3 (10). RENDER falls back to the nearest look.
- Lead re-ran make_manifest.py: the manifest had only lampknight t4; it now has 135 pieces + 8 bodies. No Blender left running. Last pieces written: wildsoul t3 (all 5 present, sizes sane).
- Deferred: skin restyle (orange/plastic) and the soulbinder leg seam (status unknown); the contact sheets for later classes were not reviewed by the lead.
