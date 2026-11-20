# Creature credits - the 46 non-humanoid monsters (rigs B-F)

Applies to the quadrupeds, arthropods, serpents, flyers and rigid / floating creatures of the bestiary: every `mon_*.glb`
listed in `Y:\game\assets\chars\rigs_bf_meta.json`.

## Models, skeletons and animations

All 46 are original designs built entirely by script with the project's own generator (`Y:\tools\blender\beasts`:
`blib.py`, `bparts.py`, `rig_b.py` ... `rig_f.py`, the `spec_*.py` files and the helpers' folders `h1` ... `h4`). Meshes,
skeletons, skin weights and all eight animation clips per creature (`Idle`, `Walk`, `Run`, `Attack1`, `Attack2`, `Hit`,
`Death`, `Spawn`) are generated procedurally. **No third-party mesh, rig, motion-capture or animation data is used**,
and no asset of any existing game was used as a model or reference.

## Scanned surface detail (the only third-party material)

The textures are baked with the monsters' look library (`Y:\tools\blender\monsters\looks.py`, extended by the look kinds
in `bparts.py` and the spec files). That library mixes its procedural recipes with detail taken from scanned PBR texture
sets from Poly Haven and ambientCG, **all CC0 1.0** (public domain dedication; no attribution required, recorded here
anyway). No set is shipped as a file: each albedo is reduced to a detail map, its normal map is integrated into a height
field, and the result is tiled in object space, combined with procedural detail and baked into each creature's own
atlas (2048 px, or 1024 px for swarm creatures), which is embedded in the `.glb`. The full description of the
processing and the complete source list (downloads in `Y:\assets_src\montex`) is in `Y:\credits\monsters.md`,
section "Scanned surface detail used by ALL monsters".

Sets sampled by the looks the creatures use:

| kit alias | source | source asset | author | source URL | licence | used on the creatures for |
|---|---|---|---|---|---|---|
| hide | Poly Haven | leather_white | Rob Tuytel | https://polyhaven.com/a/leather_white | CC0 1.0 | pores of skin, wet mucous skin (eels, leeches, grubs, toad), wing and ear membranes, glowing sacs |
| leather | Poly Haven | brown_leather | Rob Tuytel | https://polyhaven.com/a/brown_leather | CC0 1.0 | straps, harness, saddle leather |
| leather_old | ambientCG | Leather014 | Lennart Demes | https://ambientcg.com/view?id=Leather014 | CC0 1.0 | worn chitin, membranes, old leather |
| scales | ambientCG | Leather008 | Lennart Demes | https://ambientcg.com/view?id=Leather008 | CC0 1.0 | reptile hide (lizard) |
| fleece | Poly Haven | curly_teddy_natural | colormass, Rico Cilliers | https://polyhaven.com/a/curly_teddy_natural | CC0 1.0 | fur and manes (hounds, lynx, yak, bat) |
| linen, wool, burlap | Poly Haven | rough_linen, caban, hessian_230 | colormass, Rico Cilliers | https://polyhaven.com/a/rough_linen (and /caban, /hessian_230) | CC0 1.0 | shrouds, silk wrappings, rags, rope |
| coral | Poly Haven | coral_fort_wall_02 | Dimitrios Savva | https://polyhaven.com/a/coral_fort_wall_02 | CC0 1.0 | bone, teeth, horn, tusks, claws |
| plaster | Poly Haven | white_plaster_rough_01 | Rob Tuytel | https://polyhaven.com/a/white_plaster_rough_01 | CC0 1.0 | bone, the chalk-white skin of the Pale Hush creatures |
| obsidian | ambientCG | Rock035 | Lennart Demes | https://ambientcg.com/view?id=Rock035 | CC0 1.0 | chitin shells, black glass (lantern panes, slag-glass wings) |
| bronze | ambientCG | Metal017 | Lennart Demes | https://ambientcg.com/view?id=Metal017 | CC0 1.0 | brass and bronze (helmet, breastplate, bell, heliostat, cog) |
| iron | Poly Haven | rusty_metal_03 | Amal Kumar | https://polyhaven.com/a/rusty_metal_03 | CC0 1.0 | rusted iron (lantern, chains, furnace door, hoops) |
| rock_crk | Poly Haven | rock_boulder_cracked | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/rock_boulder_cracked | CC0 1.0 | basalt, stone effigy, baked ash plates |
| basalt | Poly Haven | dark_rock | Amal Kumar | https://polyhaven.com/a/dark_rock | CC0 1.0 | basalt, slag |
| burnt | Poly Haven | burned_ground_01 | Rob Tuytel | https://polyhaven.com/a/burned_ground_01 | CC0 1.0 | charred bark and root, clinker |
| bark | Poly Haven | bark_brown_02 | Rob Tuytel | https://polyhaven.com/a/bark_brown_02 | CC0 1.0 | wood (coffin, headstock, kegs), charred root, fungus stalk |
| mud_crk | Poly Haven | mud_cracked_dry_03 | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/mud_cracked_dry_03 | CC0 1.0 | dry skin patches, mud and grave soil |
| ice | ambientCG | Ice003 | Lennart Demes | https://ambientcg.com/view?id=Ice003 | CC0 1.0 | ice, icicles, blue glass flame-shards |

Licence texts: CC0 1.0 https://creativecommons.org/publicdomain/zero/1.0/ - Poly Haven https://polyhaven.com/license -
ambientCG https://docs.ambientcg.com/license/

## Preview renders

The preview pictures (`Y:\shots\chars\beast_*.jpg`, `Y:\shots\preview\beast_*_proof.webp`) are lit with one of the game's own
sky HDRIs (`Y:\game\assets\env\sky`, credited in `Y:\credits\env.md`); it is not part of any creature file.
