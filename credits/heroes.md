# Hero character credits (燼淵 Ashen Abyss)

Scope of this file: the hero and townsfolk models `hero_emberblade`, `hero_astromancer`, `hero_soulbinder`,
`hero_pactbinder`, `hero_hunter`, `hero_wildsoul`, `hero_forgebody`, `npc_villager_m`, `npc_villager_f`
(`Y:\game\assets\chars\<id>.glb`). All are ORIGINAL designs generated procedurally by the Blender scripts in
`Y:\tools\blender\heroes\` (`<id>.py` on top of the shared kit). Garments, armour, props and weapons contain no
third-party meshes. Third-party sources are limited to the two tables below; every one of them is CC0.

## 1. Rig, base body, hair (raw files + licence texts in `Y:\assets_src\q\`)

| used for | asset | author | source | licence | used by |
|---|---|---|---|---|---|
| skeleton / rest pose (unmodified), preview animation clips | Universal Animation Library (`UAL1.glb`, `UAL2.glb`) | Quaternius | https://quaternius.com | CC0 1.0 | all |
| base body mesh + painted skin / eye textures (re-fitted to the UAL rig, trimmed under clothing, re-atlased; micro detail added by `mats.skin_src`) | Universal Base Characters — `Superhero_Male_FullBody`, `Superhero_Female_FullBody` | Quaternius | https://quaternius.com | CC0 1.0 | every character that shows skin |
| hair, beard, stubble, eyebrow meshes + `T_Hair_1/2` textures (used as lock shading under the strand detail of `mats.hair_src`) | Universal Base Characters — hair set | Quaternius | https://quaternius.com | CC0 1.0 | every character with hair / brows |

Licence text: CC0 1.0 Universal — https://creativecommons.org/publicdomain/zero/1.0/
(`Y:\assets_src\q\License_UAL.txt`, `Y:\assets_src\q\License_BaseCharacters.txt`).

## 2. Scanned PBR sets used as the detail source of the baked materials (realism pass)

`Y:\tools\blender\heroes\chartex.py` lists them (alias -> source). `chartex_prep.py` converts each set to a tileable
detail tile (albedo detail multiplier, height integrated from the normal map, roughness); `mats.py` samples those tiles
box-projected in object space and bakes them into the per-character atlases, re-coloured by each character's palette.
No scan is shipped as a file of its own. Raw downloads: `Y:\assets_src\chartex\<source id>\` (1K JPG maps); the sets
marked *env* are the copies already shipped with the game in `Y:\game\assets\env\tex\<name>\` (see `Y:\credits\env.md`).

| alias (chartex.py) | asset | author | source | licence | used for |
|---|---|---|---|---|---|
| wool | Caban | colormass, Rico Cilliers | https://polyhaven.com/a/caban | CC0 1.0 | wool cloaks, hoods, dresses, tabard |
| linen | Rough Linen | colormass, Rico Cilliers | https://polyhaven.com/a/rough_linen | CC0 1.0 | shirts, wraps, bandages, aprons, parchment fibres |
| twill | Poly Wool Herringbone | colormass, Rico Cilliers | https://polyhaven.com/a/poly_wool_herringbone | CC0 1.0 | coats, hose, sleeves, vests |
| brocade | Quatrefoil Jacquard Fabric | colormass, Rico Cilliers | https://polyhaven.com/a/quatrefoil_jacquard_fabric | CC0 1.0 | raised trim bands, emblems |
| burlap (*env* `burlap`) | Hessian 230 | colormass, Rico Cilliers | https://polyhaven.com/a/hessian_230 | CC0 1.0 | scorched wraps, shrouds |
| velvet (*env* `red_velvet`) | Velour Velvet | colormass, Rico Cilliers | https://polyhaven.com/a/velour_velvet | CC0 1.0 | stoles |
| canvas (*env* `canvas`) | Fabric 063 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Fabric063 | CC0 1.0 | sashes, cloth shoes, masks, trousers |
| leather | Brown Leather | Rob Tuytel | https://polyhaven.com/a/brown_leather | CC0 1.0 | coats, jerkins, book covers |
| leather_old | Leather 014 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Leather014 | CC0 1.0 | belts, boots, straps |
| hide | Leather White | Rob Tuytel | https://polyhaven.com/a/leather_white | CC0 1.0 | hide garments, pebble grain on all leather, small props |
| steel | Metal 050 C | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Metal050C | CC0 1.0 | pitted plate, blades |
| iron | Metal 038 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Metal038 | CC0 1.0 | dark forged iron, second breakup layer of all metals |
| bronze (*env* `tarnished_bronze`) | Metal 008 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Metal008 | CC0 1.0 | bronze / brass trim, fine pitting on bone |
| rust_b (*env* `rusty_iron`) | Rusty Metal 04 | Amal Kumar | https://polyhaven.com/a/rusty_metal_04 | CC0 1.0 | rust and verdigris patches |
| bark | Bark Brown 02 | Rob Tuytel | https://polyhaven.com/a/bark_brown_02 | CC0 1.0 | bark armour slabs |
| bark_b (*env* `bark`) | Bark Willow 02 | Charlotte Baglioni | https://polyhaven.com/a/bark_willow_02 | CC0 1.0 | second bark layer |
| wood (*env* `rough_beam`) | Rough Wood | Rob Tuytel | https://polyhaven.com/a/rough_wood | CC0 1.0 | staffs, hafts, clubs |
| bone | Coral Fort Wall 02 | Dimitrios Savva | https://polyhaven.com/a/coral_fort_wall_02 | CC0 1.0 | porous mottling of bone, horn, antler |
| rope (*env* `rope`) | Rope 001 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Rope001 | CC0 1.0 | ropes, cords, knots |

Registered in `chartex.py` and downloaded, but not used by any current character material: Curly Teddy Natural
(`fleece`), Fabric Leather 02 (`leather_b`), Rusty Metal 02 (`rust`), Green Metal Rust (`verdigris`) — Poly Haven, CC0;
Metal 017 (`bronze_c`), Metal 048 A (`gold`) — ambientCG, CC0 (*env* copies). Downloaded and rejected (never used):
Metal Plate (Poly Haven, CC0; a tread-plate pattern), Metal 054 B (ambientCG, CC0; the archive in
`Y:\assets_src\chartex\Metal054B\` is an incomplete download and was never unpacked). The ambientCG archives also
contained `.blend` / `.usdc` / `.mtlx` / `.tres` preview files; only the JPG maps are read (nothing from the downloads
is ever executed or opened as a scene).

Synthesised by `chartex_prep.py` (no third-party data): `fur`, `fur_long` (layered lock tiles), `mail` (ring mail).
Preview lighting only (not in any .glb): `Y:\game\assets\env\sky\dusk.hdr` (Qwantani Dusk 2 Pure Sky, Greg Zaal &
Jarod Guest, Poly Haven, CC0).

## 3. Town / lampknight / spirit-form batch

Scope: `hero_lampknight`, `npc_elder`, `npc_smith`, `npc_merchant`, `npc_occultist`, `form_bear`, `form_wolf`
(`Y:\game\assets\chars\<id>.glb`; build scripts `Y:\tools\blender\heroes\<id>.py` on the shared kit plus the batch
modules `kit_twn.py` / `kit_beast.py`). All are ORIGINAL designs generated procedurally; armour, garments, props,
weapons, beast heads, claws and tails contain no third-party meshes. Third-party sources are the same CC0 ones as in
sections 1 and 2:

- rig, base body, skin / eye textures, hair / beard / eyebrow meshes: Quaternius Universal Animation Library and
  Universal Base Characters (CC0 1.0) — the two spirit forms and `npc_occultist` use the base body only as the hidden
  reference for the pelt / garment shells (no skin or hair is exported for them);
- scanned detail sets from the table in section 2 (all CC0 1.0), unchanged, plus one set that was registered but unused
  before this batch: **`gold` = Metal 048 A, Lennart Demes (ambientCG), https://ambientcg.com/view?id=Metal048A, CC0 1.0**
  (*env* copy `aged_gold`), used for the gilt trim of `hero_lampknight`;
- fur (`fur`, `fur_long`) and mail tiles are synthesised by `chartex_prep.py` (no third-party data).
