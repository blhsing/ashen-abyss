# Monster credits (Ashen Abyss)

## Frost / sand monsters: mon_frostbound, mon_rime_stalker, mon_sand_revenant

Original designs built by script (`Y:\tools\blender\monsters\mon_frostbound.py`, `mon_rime_stalker.py`,
`mon_sand_revenant.py`, helpers in `kit_ice.py`). No third-party meshes other than the two below. Since the surface
upgrade of 2026-11-15 their textures (like those of every other monster) are baked from the kit's shader recipes
combined with the scanned CC0 texture sets listed in the next section.

| asset | author | source | licence | used for |
|---|---|---|---|---|
| Universal Base Characters (free version), male base body | Quaternius | https://quaternius.com (`Y:\assets_src\q\base`, `License_BaseCharacters.txt`) | CC0 1.0 | body mesh that all three creatures are sculpted from |
| Universal Animation Library 1, humanoid armature | Quaternius | https://quaternius.com (`Y:\assets_src\q\UAL1.glb`, `License_UAL.txt`) | CC0 1.0 | the unmodified shared rig embedded in each .glb |

## Scanned surface detail used by ALL monsters (look library v2, 2026-11-15)

Applies to every `mon_*.glb` in `Y:\game\assets\chars` (mon_brute, mon_choir, mon_cult_caster, mon_cultist, mon_fiend,
mon_frostbound, mon_husk, mon_husk_bloated, mon_knight, mon_rime_stalker, mon_sand_revenant, mon_skeleton,
mon_skeleton_thrower, mon_wraith) and to anything built later with `Y:\tools\blender\monsters\looks.py`.

Source: PBR texture sets from Poly Haven and ambientCG, all CC0 1.0 (public domain dedication; no attribution required,
recorded here anyway). Downloaded 2026-11-14 as 1K JPG maps (diffuse, OpenGL normal, roughness, displacement, AO) into
`Y:\assets_src\montex\<source asset>\`; the API listings they were picked from are in `Y:\assets_src\montex\_api\`.

How they are used (modifications): no set is shipped as a file. `montex_prep.py` reduces each albedo to a detail map
(large-scale lighting and most of the hue removed), integrates the normal map into a height field and keeps the
roughness map; `looks.py` tiles these box-projected in object space, mixes them with procedural detail and bakes the
result into each monster's own 2048 px base-colour / ORM / normal atlas, which is embedded in the .glb.

Licence texts: CC0 1.0 https://creativecommons.org/publicdomain/zero/1.0/ - Poly Haven licence page
https://polyhaven.com/license - ambientCG licence page https://docs.ambientcg.com/license/

### Sets sampled by the looks

| kit alias | source | source asset | author | source URL | licence | used for |
|---|---|---|---|---|---|---|
| burlap | Poly Haven | hessian_230 | colormass, Rico Cilliers | https://polyhaven.com/a/hessian_230 | CC0 1.0 | weave of warm / brown cloth: rags, shawls, rope, sackcloth |
| linen | Poly Haven | rough_linen | colormass, Rico Cilliers | https://polyhaven.com/a/rough_linen | CC0 1.0 | weave of grey / pale cloth: robes, veils, bandage wraps, spectral cloth |
| wool | Poly Haven | caban | colormass, Rico Cilliers | https://polyhaven.com/a/caban | CC0 1.0 | weave of dark cloth: under-suits, dark robes, capes |
| fleece | Poly Haven | curly_teddy_natural | colormass, Rico Cilliers | https://polyhaven.com/a/curly_teddy_natural | CC0 1.0 | fur pelts, moss |
| leather | Poly Haven | brown_leather | Rob Tuytel | https://polyhaven.com/a/brown_leather | CC0 1.0 | leather grain: straps, belts, grips |
| leather_old | ambientCG | Leather014 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Leather014 | CC0 1.0 | scuffs and creases of worn leather |
| hide | Poly Haven | leather_white | Rob Tuytel | https://polyhaven.com/a/leather_white | CC0 1.0 | pore grain of every skin / flesh look |
| mud_crk | Poly Haven | mud_cracked_dry_03 | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/mud_cracked_dry_03 | CC0 1.0 | dry cracked patches of dead skin, dried mud |
| coral | Poly Haven | coral_fort_wall_02 | Dimitrios Savva | https://polyhaven.com/a/coral_fort_wall_02 | CC0 1.0 | porosity of bone, horn, teeth, masks (bone stand-in) |
| plaster | Poly Haven | white_plaster_rough_01 | Rob Tuytel | https://polyhaven.com/a/white_plaster_rough_01 | CC0 1.0 | fine surface of bone (bone stand-in) |
| bronze | ambientCG | Metal017 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Metal017 | CC0 1.0 | micro surface of clean forged metal |
| iron | Poly Haven | rusty_metal_03 | Amal Kumar | https://polyhaven.com/a/rusty_metal_03 | CC0 1.0 | rust and corrosion on all metal looks |
| iron_scr | ambientCG | Metal054B | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Metal054B | CC0 1.0 | scratched / spotted wear and roughness breakup of metal |
| rock_crk | Poly Haven | rock_boulder_cracked | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/rock_boulder_cracked | CC0 1.0 | charred hide and rock surfaces |
| basalt | Poly Haven | dark_rock | Amal Kumar | https://polyhaven.com/a/dark_rock | CC0 1.0 | chunky facets of rock / basalt plates |
| burnt | Poly Haven | burned_ground_01 | Rob Tuytel | https://polyhaven.com/a/burned_ground_01 | CC0 1.0 | charred crust on rock looks, wet mud |
| obsidian | ambientCG | Rock035 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Rock035 | CC0 1.0 | volcanic glass |
| ice | ambientCG | Ice003 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Ice003 | CC0 1.0 | ice, glacier hide, ice crystals |
| snow | Poly Haven | snow_02 | Rob Tuytel | https://polyhaven.com/a/snow_02 | CC0 1.0 | hoar frost on ice, grain of sand |
| bark | Poly Haven | bark_brown_02 | Rob Tuytel | https://polyhaven.com/a/bark_brown_02 | CC0 1.0 | weathered wood (hafts, staves) |

### Sets downloaded and prepared but not sampled by any look yet

Kept in `Y:\assets_src\montex` and in the kit cache so a creature script can ask for them (`NB.scan('<alias>')`,
`weave='wool_weave'`); nothing from them is in any exported file.

| kit alias | source | source asset | author | source URL | licence |
|---|---|---|---|---|---|
| wool_weave | ambientCG | Fabric031 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Fabric031 | CC0 1.0 |
| leather_b | Poly Haven | fabric_leather_02 | Rob Tuytel | https://polyhaven.com/a/fabric_leather_02 | CC0 1.0 |
| scales | ambientCG | Leather008 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Leather008 | CC0 1.0 |
| rust | Poly Haven | rusty_metal_02 | Rob Tuytel | https://polyhaven.com/a/rusty_metal_02 | CC0 1.0 |
| plate | Poly Haven | metal_plate | Rob Tuytel | https://polyhaven.com/a/metal_plate | CC0 1.0 |
| verdigris | Poly Haven | green_metal_rust | Rob Tuytel | https://polyhaven.com/a/green_metal_rust | CC0 1.0 |
| rust_pit | Poly Haven | rust_coarse_01 | Dimitrios Savva, Rico Cilliers | https://polyhaven.com/a/rust_coarse_01 | CC0 1.0 |
| iron_b | Poly Haven | rusty_metal_04 | Amal Kumar | https://polyhaven.com/a/rusty_metal_04 | CC0 1.0 |
| steel_plate | Poly Haven | metal_plate_02 | Rob Tuytel | https://polyhaven.com/a/metal_plate_02 | CC0 1.0 |
| steel_scr | ambientCG | Metal038 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Metal038 | CC0 1.0 |
| mud_crk2 | Poly Haven | mud_cracked_dry_riverbed_002 | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/mud_cracked_dry_riverbed_002 | CC0 1.0 |
| lava | ambientCG | Lava001 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Lava001 | CC0 1.0 |
| oak | Poly Haven | oak_veneer_01 | Jenelle van Heerden | https://polyhaven.com/a/oak_veneer_01 | CC0 1.0 |

The preview renders in `Y:\shots\chars` are lit with `Y:\game\assets\env\sky\overcast.hdr` (credited in
`Y:\credits\env.md`); it is not part of any monster file.

## Mire / brass monsters and the seven region bosses (mon_mire_hag, mon_rotspawn, mon_brass_sentinel, boss_gravedigger, boss_rimelord, boss_bishop, boss_mire_mother, boss_sand_tyrant, boss_grand_hush, boss_osaros)
Original designs for Ashen Abyss, built procedurally with the monster kit (`Y:\tools\blender\monsters`, helper module `kit_boss.py`).
No new third-party asset was added: they use only what is already credited above (the unmodified Quaternius UAL rig, the
re-fitted Quaternius base bodies for visible hands / faces / limbs, and the CC0 scanned material sets sampled by `looks.py`).
