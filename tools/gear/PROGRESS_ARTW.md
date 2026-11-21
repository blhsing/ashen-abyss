# ART-W progress — weapon, off-hand and jewel models

Scripts: `Y:\tools\blender\gear\weapons\` · output: `Y:\game\assets\gear\weapons\` · contact sheets: `Y:\shots\gear\weapons\<base>.jpg`
Manifest: `game/assets/gear/weapons/gear_manifest_weapons.json` (copy in `game/assets/gear/`), a JSON array, §6 fields plus
`variant, hands, grip, tris, emissive, bbox, textures, atlas_px, hint`.

## What RENDER needs to know (stable from the first batch on)

**Axes.** Every `.glb` is exported WITHOUT the Y-up conversion, so in Godot the mesh's local axes are literally the
contract's: **+Z up the weapon, +X towards the edge / knuckles, +Y the palm normal**, origin at the grip centre, metres.
(Checked in Godot: `wpn_sword1_v2` AABB is 0.23 × 0.03 × 0.99, long axis Z.) Consequences:
- an unrotated weapon lies flat on the ground (its flat faces ±Y = Godot up) with the long axis along world Z — handy for loot drops;
- for an icon, look along −Y (camera on the +Y side): that is the flat / face view the contact sheets show.

**Scene layout.** Root `Node3D` named after the file → one `MeshInstance3D` called `model`. Its metadata `extras`
(glTF node extras) holds `id, base, grip ("r" | "l" | "both"), mount, length_m` and, for two-hand weapons, `grip2_offset`.
Surface 0 uses material `wpn_<base>` (albedo + normal + ORM from the shared atlas); glowing parts are a second surface
`emis_<base>_<accent>` (named `emis…` so `CharModel.Convert` gives it the pulse; emission texture `gear_emis.png`, white
factor, `KHR_materials_emissive_strength` 3.2–5).

**Hand mount.** Bone-local transform of the weapon in `hand_r` / `hand_l` (Blender bone space = Godot bone space for this
rig; columns are the weapon's X, Y, Z axes, then the origin; from `charlib.grip()`):

| bone | weapon X | weapon Y | weapon Z | origin |
|---|---|---|---|---|
| `hand_r` | (0.00683, 0.98560, −0.16897) | (−0.99918, 0, −0.04037) | (−0.03979, 0.16911, 0.98479) | (−0.031, 0.103, −0.004) |
| `hand_l` | (−0.00683, 0.98560, −0.16897) | (−0.99918, 0, 0.04037) | (0.03979, 0.16911, 0.98479) | (0.031, 0.103, −0.004) |

In `hand_l` the weapon's +Y points to the BACK of the hand (the frame is a proper rotation, not a mirror). Every hand
item here is symmetric about its Y = 0 plane (apart from small charms and thongs), so the same file fits either hand (dual-wield, `fist` in both hands).

**Shield mount** (`mount = "forearm_l"`). Shield space: origin = centre of the forearm strap ON the forearm axis,
+X elbow → hand, +Y = face (outward), +Z = up the shield; the hand bar is at x = +0.258, y = −0.031. Suggested bone-local
transform in `lowerarm_l` (columns X, Y, Z, origin): X (0, 0.99985, −0.01724) · Y (−0.99918, 0.00070, 0.04036) ·
Z (0.04037, 0.01722, 0.99904) · origin (0, 0.11766, 0.00267) — i.e. 0.118 m down the forearm bone from the elbow.

**Long weapons** (`staff`, `polearm`, `scythe2`): origin is mid-shaft (about 0.86–0.90 m of shaft below the hand), as on
today's caster heroes, which hold staves with `kit_cst.staff_grip()` = `grip(tilt=46) @ R('X', 30)` so the staff stands
upright; with the plain grip above they point forward like a sword. The manifest carries this as `hint`.

**Hanging items.** With the arm hanging, weapon +X (knuckles) points down. `grimoire` hangs from a bail towards +X;
`lantern` hangs towards +X tilted 20° to the little-finger side (measured as the best fit to "down" over Idle / Walk /
Jog / Sword_Idle for `hand_l`). `focus` and `totem` stand above the fist (+Z).

**Bow / crossbow.** `bow`: `mount = "hand_l"`, grip at the origin, limbs along ±Z, string on the −X side,
`grip2` = nocking point. `crossbow`: `mount = "hand_r"`, pistol handle through the fist along Z, shoots along +X
(knuckles), prod spans ±Y, `grip2` = fore-stock.

**Textures.** One atlas per base, shared by its three variants: `wpn_<base>_c.jpg / _n.jpg / _orm.jpg` (512–1024 px),
referenced from the `.glb` files by relative URI (Godot loads ONE texture set for the three models; nothing is embedded).
Their `.import` files ask for VRAM compression (colour BPTC, ORM S3TC, normal RGTC — like `assets/env/tex`), about a
quarter of the memory of the lossless hero atlases. `gear_emis.png` (64 × 128, lossless, no mipmaps) is the heat ramp of
every glowing part of every gear model.

## Pipeline (resume here after an interruption)

```
cd Y:\tools\blender\gear\weapons
sh rb.sh build.py <base>[,<base>...] [variants e.g. 2 | 123] [--fast]     # --fast = flat-colour look-dev sheet only
sh rb.sh sheet.py <base> ...                                              # re-render contact sheets from the .glb files
"C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" merge_manifest.py
powershell -File Y:\tools\import.ps1 ; powershell -File godot\check.ps1 -Ids wpn_sword1_v2,...   # Godot import + headless check
```
`gearlib.py` pipeline (bake, export, glb patch, .import) · `gearparts.py` shapes · `gearmats.py` palette ·
`defs_1h.py / defs_2h.py / defs_off.py / defs_jewel.py` the designs · `sheet.py` contact sheets.
Depends read-only on `Y:\tools\blender\heroes` (mk, kit_*, mats, finalize, rigio).

## Log

### Batch 1 — v2 (forged) of all 22 bases — done, imported in Godot, in the manifest
`wpn_<base>_v2.glb` for sword1 axe1 mace1 dagger wand scythe1 flail fist sceptre · sword2 axe2 mace2 staff polearm scythe2
bow crossbow · shield focus totem grimoire lantern (1.1k–4.0k tris each). sword1 already has v1 and v3 too.
Godot headless check (`godot\check.ps1`): textures resolve to the shared files, tangents present, extras on `model`.
Sheets so far: `Y:\shots\gear\weapons\<base>.jpg`, overviews `_ov_1h_v2.jpg`, `_ov_2h_v2.jpg`, `_ov_off_v2.jpg`.
NOTE for RENDER: when v1 / v3 of a base are built later, its atlas is re-baked and all three `.glb` of that base are
rewritten together (UVs change); run `Y:\tools\import.ps1` again after each later batch.

### Batch 2 — v1 (crude) and v3 (ornate / glowing) of all 22 bases — done (66 weapon and off-hand models)
Each base was re-baked with its three variants in one atlas (`wpn_<base>_c/_n/_orm.jpg`, 512-1024 px); all three
`.glb` of every base were rewritten. Triangles: 1.1k-5.0k per model. Glow accents (second surface `emis_<base>_<accent>`):
ember (sword1/2, axe1/2, mace2, fist, polearm, shield), storm (mace1), void (dagger, focus), star (wand, staff),
soul (scythe1/2), flame (flail, lantern - all three lanterns carry a flame), holy (sceptre), teal (bow, crossbow),
moss (totem), pact (grimoire).
Changes against batch 1: sword2 is broader with a longer grip (grip2 is now (0, 0, -0.185)); polearm v2 shaft 10 cm shorter.

### Batch 3 — jewels and gems — done
`jwl_ring_v1..v3`, `jwl_amulet_v1..v3` (atlases `jwl_ring_*`, `jwl_amulet_*`), `gem_ruby / gem_sapphire / gem_topaz /
gem_emerald / gem_amethyst / gem_skull` (one shared atlas `gem_atlas_*`; kinds from `ItemDb.BuildGems`).
They are modelled about 4.5 x life size (bbox 0.09-0.27 m; manifest `display_scale: 4.5`) so they show as ground loot and
their bake holds detail. Showy side faces +Y like the weapons' flat: ring hole axis = Y with the stone at +Z; amulet
pendant in the XZ plane with the chain loop towards +Z; gem table faces +Y. `mount` is `"none"`.
Gems are 84-140 triangles (988 for the skull): below the 1k floor of the weapon budget on purpose.

### Batch 4 — verification and final state (78 models: 66 weapon / off-hand, 6 jewels, 6 gems)
- Contact sheets, every variant side by side (face view, three-quarter view, 256 px and 64 px icon row):
  `Y:\shots\gear\weapons\<base>.jpg` for the 22 bases plus `ring.jpg`, `amulet.jpg`, `gem.jpg`.
  Review montages `_rev_A..H.jpg`; all bases at icon size `_icons_v1.jpg`, `_icons_v2.jpg`, `_icons_v3.jpg`, `_icons_gems.jpg`.
- Godot: `Y:\tools\import.ps1` imports all 78 `.glb` and 75 atlas textures + `gear_emis.png`; no importer error names a
  gear/weapons file (see the importer note below). Headless check (`godot\check.ps1`; output `godot\check_all_out.txt`,
  last rebuilds `godot\check_last_out.txt`): 78/78 scenes load, 110 surfaces all with tangents, every main surface
  resolves the shared `_c/_n/_orm` files, all 32 glow surfaces resolve `gear_emis.png`, node extras present on all.
- Hand mount, Blender side, against today's hero `.glb` files with the bone frames published above
  (`mount_test.py`, sheets `mount_a..e.jpg`): greatsword / sword / flail / axe / sickle / wand in `hand_r`; lantern, focus,
  grimoire, totem, bow, claws in `hand_l`; shields on `lowerarm_l` (upright, facing forward in Idle_Shield_Loop); staff and
  great scythe with the staff tilt; crossbow level in Pistol_Aim_Neutral; two game-camera shots.
- NOT tested: mounting in the running game (RENDER's `GearVisual` did not exist when this was built), the look under
  `char.gdshader` in a level, icons as RENDER renders them. Slot 95 was not used (no game run was needed).
- Manifest: `game/assets/gear/weapons/gear_manifest_weapons.json` = `game/assets/gear/gear_manifest_weapons.json` (same
  content, 78 entries). Extra field `slot`: mainhand | offhand | ring | amulet | gem.
- Sizes: 33 MB on disk; 192.9k triangles in total (weapons 1.1k-5.0k each).

Weakest models (candidates for a second pass): wand v2 (plain rod), focus v2 (the orb reads as a painted ball - the game
shader has no transparency), totem v2 (round owl eyes are a little toy-like), the three bows (thin by nature; hard to read
at icon size), gems (opaque, 84-140 triangles, flat colour), lantern flames (faceted static shapes), flail chains (rigid
pose, wrong against gravity in many clips), black-steel blades on thin parts (the wear mask brightens them to grey steel).

For the lead:
- `.gitignore` already keeps `*.glb / *.jpg / *.png` out; the texture `.import` files (they carry the VRAM-compression
  settings) and the two manifest `.json` files are text and should be committed. The release asset archive needs
  `game/assets/gear/**` added next to `game/assets/chars` (release scripts not touched).
- One atlas per base instead of one per weapon family or shared tileable sets: the game shader samples one UV set with
  no detail map, so shared tileable metal / wood / leather sets would have lost the baked wear, rust and occlusion that
  the hero textures have; sharing is per base (3 models -> 1 texture set, external files) plus one glow ramp for all.
- Importer note: when `import.ps1` prints `N x ERROR: Condition "f.is_null()" is true. Continuing.` these are
  `get_multiple_md5` misses on armor / base-body textures that were being rewritten while the importer ran (full log
  `tools/blender/gear/weapons/godot/import_full_log.txt`: 3 lines, all on `arm_*` / `hero_*_base_*` files, none on
  `wpn_* / jwl_* / gem_*`). The gear/weapons files import cleanly.

### Polish pass (lead request, 2026-11-21 evening) - STOPPED on the lead's order; state at the stop
All 78 files are consistent: every changed base was re-baked as a whole (three .glb + its atlas), the manifest was
merged again, `import.ps1` ran and the headless check loads 78/78 (111 surfaces, all with tangents, 0 errors;
`godot/check_all_out.txt`). No Blender job of ART-W is running. File names, ids and grips are unchanged; `length`,
`bbox` and `tris` changed for the edited models.

DONE (baked, exported, imported):
- wand v2: a long blue crystal in a four-ribbed brass cage with hoop and finial (was a bare rod with a small tip).
- focus v2: orb of fitted blue crystal plates (three baked tones) over an emissive core that shows as seams
  (`emis_focus_star`, low heat), three bronze talons, cup and crown; the equator band was removed.
- totem v2: slit eyes under slanted lids with tear cuts (`totem_face(style="slit")`); v3 keeps the ringed, lit eyes.
  The slits were enlarged once and re-baked; that last bake was NOT looked at before the stop.
- gems: brilliant / step cuts with facets baked in three tones (`<key>_lo / <key> / <key>_hi`), one baked glint
  (`gem_glint`) and a small emissive light on the table; topaz is a trillion again. Seen in the flat preview only -
  the final baked `gem.jpg` was NOT looked at before the stop.
- black steel: `blade_dark` and `darksteel` wear / hone colours darkened (gearmats.py); the 16 bases using them were
  re-baked; checked on sword1/sword2/axe1/axe2 v3 (`_pol_c.jpg`, `heads.jpg`), not on every base.
- sword1 v2 (short broad blade 0.69 m, cross 0.33 m wide) and sword2 v2 (0.30 m ricasso, hooked lugs 0.19 m wide).
- axe2 v1 (scrap-iron cleaver slab strapped to a thick haft) and axe2 v3 (bardiche crescent clasped to the haft, back
  spike, long langets, arc + three ember rays). mace1/mace2 and scythe1/scythe2 were checked on `heads.jpg` (both models
  of a pair at one scale) and left as they are: heads already differ in shape and size.

REMAINING / not done:
- Look at the final bakes of totem v2 and the gems (`totem.jpg`, `gem.jpg`) and correct if needed.
- `_rev_A..H.jpg`, `_icons_v1..v3.jpg`, `_icons_gems.jpg` are from BEFORE the polish pass (stale). The per-base sheets
  `<base>.jpg` are current; staff, sceptre, lantern, ring, amulet were not touched. `heads.jpg` and `_pol_b/_pol_c.jpg`
  are current. Mount sheets `mount_a..e.jpg` show the old sword1 / sword2 / focus v2.
- Black steel not yet eyeballed on mace1, flail, fist, mace2, polearm, scythe2, bow, crossbow, shield, grimoire, dagger,
  scythe1 v3.
- RENDER asked for `jwl_*` / `gem_*` in `game/assets/gear/jewels/`; they are still in `weapons/` (RENDER also looks there).
- Importer lines seen in the last run (`f.is_null()`, `Unrecognized UID uid://btquws8y17j4g`): that UID is in no
  gear/weapons `.import` file; not traced further.
To resume: `sh rb.sh build.py <base>` re-bakes one base; `sh rb.sh heads.py`; `sh rb.sh overview.py <name> <px> face|icons <base>...`.
