# Environment, font and icon credits (燼淵 Ashen Abyss)

Third-party assets used for the environment art, UI fonts and UI icons. Everything listed here is CC0 1.0, SIL OFL 1.1 or CC BY 3.0. Raw downloads (with their licence files and API metadata) are kept under `Y:\assets_src\env\`.

| section | count | licence | location |
|---|---|---|---|
| PBR texture sets | 80 | CC0 1.0 | `game/assets/env/tex/<name>/` |
| props | 199 | CC0 1.0 | `game/assets/env/models/<name>/<name>.glb` |
| skies (HDRI) | 8 | CC0 1.0 | `game/assets/env/sky/<name>.hdr` |
| fonts | 8 files, 3 families | SIL OFL 1.1 | `game/assets/fonts/` |
| UI icons | 4239 SVG | CC BY 3.0 (two authors CC0) | `game/assets/icons/gi/<author>/` |

Licence texts: CC0 1.0 https://creativecommons.org/publicdomain/zero/1.0/ · Poly Haven licence page https://polyhaven.com/license · ambientCG licence page https://docs.ambientcg.com/license/ · SIL OFL 1.1 https://openfontlicense.org · CC BY 3.0 https://creativecommons.org/licenses/by/3.0/

## PBR texture sets — `game/assets/env/tex/<name>/` (80 sets)

Sources: Poly Haven (60 sets) and ambientCG (20 sets), all CC0 1.0. Modifications: maps resized to 2048 px (52 sets) or 1024 px (28 sets) and re-encoded as JPEG; the OpenGL normal maps are used; ambientCG AO / roughness / metalness packed into `arm.jpg`; 23 normal maps had a tilt or gamma bias removed (`normal_fix` in `tex/manifest.json`).

| set | source | source asset | author | source URL | licence |
|---|---|---|---|---|---|
| ash_ground | Poly Haven | moon_02 | Greg Zaal, Rico Cilliers, Jenelle van Heerden, Dario Barresi | https://polyhaven.com/a/moon_02 | CC0 1.0 |
| burnt_soil | Poly Haven | burned_ground_01 | Rob Tuytel | https://polyhaven.com/a/burned_ground_01 | CC0 1.0 |
| dark_mud | Poly Haven | brown_mud_03 | Rob Tuytel | https://polyhaven.com/a/brown_mud_03 | CC0 1.0 |
| forest_soil | Poly Haven | mud_forest | eye-candy.xyz | https://polyhaven.com/a/mud_forest | CC0 1.0 |
| dead_leaves | Poly Haven | forest_leaves_03 | Rob Tuytel, Dimitrios Savva | https://polyhaven.com/a/forest_leaves_03 | CC0 1.0 |
| dead_grass | Poly Haven | withered_grass | Charlotte Baglioni | https://polyhaven.com/a/withered_grass | CC0 1.0 |
| root_ground | Poly Haven | roots | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/roots | CC0 1.0 |
| gravel_path | Poly Haven | rocky_trail | Amal Kumar | https://polyhaven.com/a/rocky_trail | CC0 1.0 |
| cliff_rock | Poly Haven | rock_face | Greg Zaal, Dario Barresi | https://polyhaven.com/a/rock_face | CC0 1.0 |
| grey_rock | Poly Haven | lichen_rock | Rico Cilliers | https://polyhaven.com/a/lichen_rock | CC0 1.0 |
| bark | Poly Haven | bark_willow_02 | Charlotte Baglioni | https://polyhaven.com/a/bark_willow_02 | CC0 1.0 |
| old_planks | Poly Haven | weathered_planks | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/weathered_planks | CC0 1.0 |
| flagstone | Poly Haven | large_grey_tiles | Rob Tuytel | https://polyhaven.com/a/large_grey_tiles | CC0 1.0 |
| crypt_cobble | Poly Haven | cobblestone_floor_06 | Amal Kumar | https://polyhaven.com/a/cobblestone_floor_06 | CC0 1.0 |
| cracked_tiles | Poly Haven | worn_tile_floor | Dimitrios Savva | https://polyhaven.com/a/worn_tile_floor | CC0 1.0 |
| slate_floor | Poly Haven | monastery_stone_floor | Amal Kumar | https://polyhaven.com/a/monastery_stone_floor | CC0 1.0 |
| castle_blocks | Poly Haven | stone_brick_wall_001 | Dimitrios Savva, Rico Cilliers | https://polyhaven.com/a/stone_brick_wall_001 | CC0 1.0 |
| big_blocks | Poly Haven | medieval_blocks_03 | Rob Tuytel | https://polyhaven.com/a/medieval_blocks_03 | CC0 1.0 |
| old_brick | Poly Haven | castle_brick_01 | Rob Tuytel | https://polyhaven.com/a/castle_brick_01 | CC0 1.0 |
| damp_plaster | Poly Haven | worn_mossy_plasterwall | Amal Kumar | https://polyhaven.com/a/worn_mossy_plasterwall | CC0 1.0 |
| mossy_stone | Poly Haven | mossy_stone_wall | Amal Kumar | https://polyhaven.com/a/mossy_stone_wall | CC0 1.0 |
| dark_planks | Poly Haven | dark_wooden_planks | Amal Kumar | https://polyhaven.com/a/dark_wooden_planks | CC0 1.0 |
| rusty_iron | Poly Haven | rusty_metal_04 | Amal Kumar | https://polyhaven.com/a/rusty_metal_04 | CC0 1.0 |
| iron_plate | Poly Haven | metal_plate_02 | Rob Tuytel | https://polyhaven.com/a/metal_plate_02 | CC0 1.0 |
| worn_marble | Poly Haven | floor_tiles_02 | Rob Tuytel | https://polyhaven.com/a/floor_tiles_02 | CC0 1.0 |
| dark_marble | ambientCG | Marble023 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Marble023 | CC0 1.0 |
| basalt | Poly Haven | dark_rock | Amal Kumar | https://polyhaven.com/a/dark_rock | CC0 1.0 |
| basalt_layered | Poly Haven | dark_rock_02 | Amal Kumar | https://polyhaven.com/a/dark_rock_02 | CC0 1.0 |
| scorched_cracked | Poly Haven | mud_cracked_dry_riverbed_002 | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/mud_cracked_dry_riverbed_002 | CC0 1.0 |
| obsidian | ambientCG | Rock035 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Rock035 | CC0 1.0 |
| obsidian_slabs | Poly Haven | slab_tiles | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/slab_tiles | CC0 1.0 |
| lava_molten | ambientCG | Lava004 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Lava004 | CC0 1.0 |
| lava_crust | ambientCG | Lava001 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Lava001 | CC0 1.0 |
| cobble_street | Poly Haven | cobblestone_floor_001 | Rob Tuytel | https://polyhaven.com/a/cobblestone_floor_001 | CC0 1.0 |
| timber_plaster | Poly Haven | yellow_plaster_02 | Rob Tuytel | https://polyhaven.com/a/yellow_plaster_02 | CC0 1.0 |
| log_wall | Poly Haven | wood_trunk_wall | Amal Kumar | https://polyhaven.com/a/wood_trunk_wall | CC0 1.0 |
| thatch | Poly Haven | thatch_roof_angled | Rob Tuytel, Dimitrios Savva | https://polyhaven.com/a/thatch_roof_angled | CC0 1.0 |
| roof_tiles | Poly Haven | roof_07 | Rob Tuytel | https://polyhaven.com/a/roof_07 | CC0 1.0 |
| packed_dirt | Poly Haven | dirt | Charlotte Baglioni | https://polyhaven.com/a/dirt | CC0 1.0 |
| rough_stone_wall | Poly Haven | stone_wall_04 | Amal Kumar | https://polyhaven.com/a/stone_wall_04 | CC0 1.0 |
| drystone_wall | Poly Haven | broken_wall | Rob Tuytel | https://polyhaven.com/a/broken_wall | CC0 1.0 |
| burlap | Poly Haven | hessian_230 | colormass, Rico Cilliers | https://polyhaven.com/a/hessian_230 | CC0 1.0 |
| canvas | ambientCG | Fabric063 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Fabric063 | CC0 1.0 |
| wicker | ambientCG | Wicker010B | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Wicker010B | CC0 1.0 |
| rope | ambientCG | Rope001 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Rope001 | CC0 1.0 |
| barrel_wood | Poly Haven | wooden_barrel | Dimitrios Savva | https://polyhaven.com/a/wooden_barrel | CC0 1.0 |
| rough_beam | Poly Haven | rough_wood | Rob Tuytel | https://polyhaven.com/a/rough_wood | CC0 1.0 |
| snow_fresh | Poly Haven | snow_02 | Rob Tuytel | https://polyhaven.com/a/snow_02 | CC0 1.0 |
| snow_trampled | Poly Haven | snow_01 | Rob Tuytel | https://polyhaven.com/a/snow_01 | CC0 1.0 |
| snow_patchy | ambientCG | Snow015 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Snow015 | CC0 1.0 |
| ice | ambientCG | Ice003 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Ice003 | CC0 1.0 |
| ice_pale | ambientCG | Ice004 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Ice004 | CC0 1.0 |
| mountain_rock | Poly Haven | rock_01 | Rob Tuytel | https://polyhaven.com/a/rock_01 | CC0 1.0 |
| dark_cliff | Poly Haven | quarry_wall | Dimitrios Savva | https://polyhaven.com/a/quarry_wall | CC0 1.0 |
| wet_mud | Poly Haven | brown_mud_02 | Rob Tuytel | https://polyhaven.com/a/brown_mud_02 | CC0 1.0 |
| mud_stones | Poly Haven | brown_mud_rocks_01 | Rob Tuytel | https://polyhaven.com/a/brown_mud_rocks_01 | CC0 1.0 |
| mossy_ground | Poly Haven | forest_leaves_02 | Rob Tuytel | https://polyhaven.com/a/forest_leaves_02 | CC0 1.0 |
| swamp_floor | Poly Haven | brown_mud_leaves_01 | Rob Tuytel | https://polyhaven.com/a/brown_mud_leaves_01 | CC0 1.0 |
| moss | ambientCG | Moss002 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Moss002 | CC0 1.0 |
| mossy_rock | Poly Haven | mossy_rock | Rob Tuytel | https://polyhaven.com/a/mossy_rock | CC0 1.0 |
| mossy_planks | Poly Haven | moss_wood | Rob Tuytel | https://polyhaven.com/a/moss_wood | CC0 1.0 |
| sand_dunes | ambientCG | Ground097 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Ground097 | CC0 1.0 |
| sand_fine | ambientCG | Ground093C | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Ground093C | CC0 1.0 |
| cracked_earth | Poly Haven | mud_cracked_dry_03 | Dario Barresi, Dimitrios Savva | https://polyhaven.com/a/mud_cracked_dry_03 | CC0 1.0 |
| sandstone_blocks | Poly Haven | sandstone_blocks_08 | Rob Tuytel | https://polyhaven.com/a/sandstone_blocks_08 | CC0 1.0 |
| sandstone_rough | Poly Haven | yellow_stone_wall | Sơn Nguyễn | https://polyhaven.com/a/yellow_stone_wall | CC0 1.0 |
| sandstone_worn | Poly Haven | old_sandstone_02 | Dimitrios Savva | https://polyhaven.com/a/old_sandstone_02 | CC0 1.0 |
| tarnished_bronze | ambientCG | Metal008 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Metal008 | CC0 1.0 |
| corroded_bronze | ambientCG | Metal017 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Metal017 | CC0 1.0 |
| mosaic_tiles | Poly Haven | marble_mosaic_tiles | Amal Kumar | https://polyhaven.com/a/marble_mosaic_tiles | CC0 1.0 |
| terracotta_tiles | Poly Haven | patterned_terracotta_tiling | Amal Kumar | https://polyhaven.com/a/patterned_terracotta_tiling | CC0 1.0 |
| marble_checker | ambientCG | Tiles074 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Tiles074 | CC0 1.0 |
| marble_floor | Poly Haven | marble_01 | Rob Tuytel | https://polyhaven.com/a/marble_01 | CC0 1.0 |
| gothic_stone | Poly Haven | stone_block_wall | Amal Kumar | https://polyhaven.com/a/stone_block_wall | CC0 1.0 |
| ornate_tiles | ambientCG | Tiles131 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Tiles131 | CC0 1.0 |
| dark_panel_wood | Poly Haven | dark_paneled_wood | Dimitrios Savva | https://polyhaven.com/a/dark_paneled_wood | CC0 1.0 |
| varnished_wood | ambientCG | Wood067 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Wood067 | CC0 1.0 |
| red_velvet | Poly Haven | velour_velvet | colormass, Rico Cilliers | https://polyhaven.com/a/velour_velvet | CC0 1.0 |
| red_carpet | ambientCG | Carpet015 | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Carpet015 | CC0 1.0 |
| aged_gold | ambientCG | Metal048A | Lennart Demes (ambientCG) | https://ambientcg.com/view?id=Metal048A | CC0 1.0 |

## Props — `game/assets/env/models/<name>/<name>.glb` (199 props)

All meshes and their textures come from Poly Haven (CC0 1.0). Modifications: objects joined, decimated, re-origined and re-exported as GLB with textures reduced to at most 1024 px (2048 px for cliff_a, cliff_c, cliff_d, statue_king); where noted the foliage was removed, the scan was rescaled or re-tinted, or a snow / moss layer was baked onto the up-facing surfaces (the moss layer uses the ambientCG Moss002 texture, CC0 1.0, by Lennart Demes).

| prop | source asset | author | source URL | licence | changes |
|---|---|---|---|---|---|
| dead_tree_a | island_tree_01 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/island_tree_01 | CC0 1.0 | removed leaves geometry |
| dead_tree_b | island_tree_02 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/island_tree_02 | CC0 1.0 | removed leaves geometry |
| dead_tree_big | jacaranda_tree | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/jacaranda_tree | CC0 1.0 | removed leaves + branches geometry; rescaled |
| dead_pine_a | fir_sapling | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/fir_sapling | CC0 1.0 | removed twig geometry; rescaled |
| dead_pine_b | pine_sapling_small | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/pine_sapling_small | CC0 1.0 | removed twig geometry; rescaled |
| dead_pine_c | fir_sapling | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/fir_sapling | CC0 1.0 | removed twig geometry; rescaled |
| quiver_tree_a | quiver_tree_01 | James Ray Cock, Dario Barresi, Rico Cilliers | https://polyhaven.com/a/quiver_tree_01 | CC0 1.0 | — |
| quiver_tree_b | quiver_tree_02 | Dario Barresi, Rico Cilliers | https://polyhaven.com/a/quiver_tree_02 | CC0 1.0 | — |
| dead_quiver | quiver_tree_01 | James Ray Cock, Dario Barresi, Rico Cilliers | https://polyhaven.com/a/quiver_tree_01 | CC0 1.0 | removed leaf geometry |
| dead_trunk | dead_quiver_trunk | James Ray Cock, Dario Barresi, Rico Cilliers | https://polyhaven.com/a/dead_quiver_trunk | CC0 1.0 | — |
| fallen_log_a | dead_tree_trunk | Rob Tuytel | https://polyhaven.com/a/dead_tree_trunk | CC0 1.0 | — |
| fallen_log_b | dead_tree_trunk_02 | Jenelle van Heerden, Rico Cilliers | https://polyhaven.com/a/dead_tree_trunk_02 | CC0 1.0 | — |
| stump_a | tree_stump_01 | Rob Tuytel | https://polyhaven.com/a/tree_stump_01 | CC0 1.0 | — |
| stump_b | tree_stump_02 | Rob Tuytel | https://polyhaven.com/a/tree_stump_02 | CC0 1.0 | — |
| roots_big | root_cluster_01 | Jenelle van Heerden, Rico Cilliers | https://polyhaven.com/a/root_cluster_01 | CC0 1.0 | — |
| roots_flat_a | pine_roots | Rob Tuytel | https://polyhaven.com/a/pine_roots | CC0 1.0 | — |
| roots_flat_b | root_cluster_02 | Jenelle van Heerden | https://polyhaven.com/a/root_cluster_02 | CC0 1.0 | — |
| root_piece_a | root_cluster_02 | Jenelle van Heerden | https://polyhaven.com/a/root_cluster_02 | CC0 1.0 | — |
| root_piece_b | root_cluster_02 | Jenelle van Heerden | https://polyhaven.com/a/root_cluster_02 | CC0 1.0 | — |
| branch_a | dry_branches_medium_01 | Rico Cilliers | https://polyhaven.com/a/dry_branches_medium_01 | CC0 1.0 | — |
| branch_b | dry_branches_medium_01 | Rico Cilliers | https://polyhaven.com/a/dry_branches_medium_01 | CC0 1.0 | — |
| branch_c | dry_branches_medium_01 | Rico Cilliers | https://polyhaven.com/a/dry_branches_medium_01 | CC0 1.0 | — |
| dead_stub_a | dead_quiver_branch_01 | Greg Zaal, Jenelle van Heerden | https://polyhaven.com/a/dead_quiver_branch_01 | CC0 1.0 | — |
| dead_stub_b | dead_quiver_branch_02 | Greg Zaal, Jenelle van Heerden | https://polyhaven.com/a/dead_quiver_branch_02 | CC0 1.0 | — |
| boulder_a | boulder_01 | Rico Cilliers | https://polyhaven.com/a/boulder_01 | CC0 1.0 | — |
| boulder_b | namaqualand_boulder_03 | Jenelle van Heerden, Dario Barresi | https://polyhaven.com/a/namaqualand_boulder_03 | CC0 1.0 | — |
| boulder_c | namaqualand_boulder_04 | Jenelle van Heerden | https://polyhaven.com/a/namaqualand_boulder_04 | CC0 1.0 | — |
| boulder_d | namaqualand_boulder_05 | Jenelle van Heerden, Dario Barresi | https://polyhaven.com/a/namaqualand_boulder_05 | CC0 1.0 | — |
| boulder_e | namaqualand_boulder_06 | Greg Zaal, Jenelle van Heerden | https://polyhaven.com/a/namaqualand_boulder_06 | CC0 1.0 | — |
| rock_cluster | namaqualand_boulder_02 | Greg Zaal, Rico Cilliers | https://polyhaven.com/a/namaqualand_boulder_02 | CC0 1.0 | — |
| rock_small_a | namaqualand_boulders_01 | Greg Zaal, Jenelle van Heerden | https://polyhaven.com/a/namaqualand_boulders_01 | CC0 1.0 | — |
| rock_small_b | namaqualand_boulders_01 | Greg Zaal, Jenelle van Heerden | https://polyhaven.com/a/namaqualand_boulders_01 | CC0 1.0 | — |
| rock_a | rock_moss_set_01 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_01 | CC0 1.0 | — |
| rock_b | rock_moss_set_01 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_01 | CC0 1.0 | — |
| rock_c | rock_moss_set_01 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_01 | CC0 1.0 | — |
| rock_d | rock_moss_set_01 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_01 | CC0 1.0 | — |
| rock_e | rock_moss_set_01 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_01 | CC0 1.0 | — |
| rock_f | rock_moss_set_01 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_01 | CC0 1.0 | — |
| rock_g | rock_moss_set_02 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_02 | CC0 1.0 | — |
| rock_h | rock_moss_set_02 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_02 | CC0 1.0 | — |
| rock_i | rock_moss_set_02 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_02 | CC0 1.0 | — |
| rock_j | rock_moss_set_02 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_02 | CC0 1.0 | — |
| cliff_a | rock_face_01 | Dario Barresi | https://polyhaven.com/a/rock_face_01 | CC0 1.0 | — |
| cliff_b | rock_face_02 | Dario Barresi, Rico Cilliers | https://polyhaven.com/a/rock_face_02 | CC0 1.0 | — |
| cliff_c | namaqualand_cliff_01 | Jenelle van Heerden, Rico Cilliers | https://polyhaven.com/a/namaqualand_cliff_01 | CC0 1.0 | — |
| cliff_d | mountainside | Dario Barresi, Rico Cilliers | https://polyhaven.com/a/mountainside | CC0 1.0 | — |
| spire_a | moon_rock_04 | Greg Zaal, Rico Cilliers, Jenelle van Heerden | https://polyhaven.com/a/moon_rock_04 | CC0 1.0 | scaled-up + darkened |
| spire_b | moon_rock_05 | Greg Zaal, Rico Cilliers, Jenelle van Heerden | https://polyhaven.com/a/moon_rock_05 | CC0 1.0 | scaled-up + darkened |
| spire_c | moon_rock_03 | Greg Zaal, Rico Cilliers, Jenelle van Heerden | https://polyhaven.com/a/moon_rock_03 | CC0 1.0 | scaled-up + darkened |
| shard_a | moon_rock_06 | Greg Zaal, Jenelle van Heerden, Rico Cilliers | https://polyhaven.com/a/moon_rock_06 | CC0 1.0 | scaled-up + darkened |
| basalt_boulder_a | moon_rock_01 | Greg Zaal, Rico Cilliers, Jenelle van Heerden, Dario Barresi | https://polyhaven.com/a/moon_rock_01 | CC0 1.0 | scaled-up + darkened |
| basalt_boulder_b | rock_09 | Jenelle van Heerden | https://polyhaven.com/a/rock_09 | CC0 1.0 | scaled-up + darkened |
| dry_bush_a | wild_rooibos_bush | James Ray Cock, Jenelle van Heerden | https://polyhaven.com/a/wild_rooibos_bush | CC0 1.0 | — |
| dry_bush_b | wild_rooibos_bush | James Ray Cock, Jenelle van Heerden | https://polyhaven.com/a/wild_rooibos_bush | CC0 1.0 | — |
| dry_bush_c | wild_rooibos_bush | James Ray Cock, Jenelle van Heerden | https://polyhaven.com/a/wild_rooibos_bush | CC0 1.0 | — |
| dry_bush_d | wild_rooibos_bush | James Ray Cock, Jenelle van Heerden | https://polyhaven.com/a/wild_rooibos_bush | CC0 1.0 | — |
| dead_bush_a | searsia_lucida | James Ray Cock, Jenelle van Heerden | https://polyhaven.com/a/searsia_lucida | CC0 1.0 | removed leaves geometry |
| dead_bush_b | searsia_lucida | James Ray Cock, Jenelle van Heerden | https://polyhaven.com/a/searsia_lucida | CC0 1.0 | removed leaves geometry |
| dead_bush_c | searsia_lucida | James Ray Cock, Jenelle van Heerden | https://polyhaven.com/a/searsia_lucida | CC0 1.0 | removed leaves geometry |
| fern_a | fern_02 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/fern_02 | CC0 1.0 | — |
| fern_b | fern_02 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/fern_02 | CC0 1.0 | — |
| fern_c | fern_02 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/fern_02 | CC0 1.0 | — |
| fern_d | fern_02 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/fern_02 | CC0 1.0 | — |
| swamp_plant_a | anthurium_botany_01 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/anthurium_botany_01 | CC0 1.0 | — |
| swamp_plant_b | anthurium_botany_01 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/anthurium_botany_01 | CC0 1.0 | — |
| swamp_plant_c | anthurium_botany_01 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/anthurium_botany_01 | CC0 1.0 | — |
| broadleaf_a | calathea_orbifolia_01 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/calathea_orbifolia_01 | CC0 1.0 | — |
| broadleaf_b | calathea_orbifolia_01 | Rob Tuytel, Rico Cilliers | https://polyhaven.com/a/calathea_orbifolia_01 | CC0 1.0 | — |
| reed_a | shrub_03 | Rico Cilliers | https://polyhaven.com/a/shrub_03 | CC0 1.0 | — |
| reed_b | shrub_03 | Rico Cilliers | https://polyhaven.com/a/shrub_03 | CC0 1.0 | — |
| statue_king | gothic_statue | Benny Weimer | https://polyhaven.com/a/gothic_statue | CC0 1.0 | — |
| bust_marble | marble_bust_01 | Rico Cilliers | https://polyhaven.com/a/marble_bust_01 | CC0 1.0 | — |
| bust_horse | horse_head | Tina | https://polyhaven.com/a/horse_head | CC0 1.0 | — |
| bust_lion | lion_head | Tina | https://polyhaven.com/a/lion_head | CC0 1.0 | — |
| bust_bull | bull_head | Tina | https://polyhaven.com/a/bull_head | CC0 1.0 | — |
| castle_door | large_castle_door | Tina | https://polyhaven.com/a/large_castle_door | CC0 1.0 | — |
| iron_gate | large_iron_gate | Josh Dean | https://polyhaven.com/a/large_iron_gate | CC0 1.0 | — |
| fort_wall_a | modular_fort_01 | Rico Cilliers | https://polyhaven.com/a/modular_fort_01 | CC0 1.0 | — |
| fort_wall_b | modular_fort_01 | Rico Cilliers | https://polyhaven.com/a/modular_fort_01 | CC0 1.0 | — |
| fort_gate | modular_fort_01 | Rico Cilliers | https://polyhaven.com/a/modular_fort_01 | CC0 1.0 | — |
| fort_corner | modular_fort_01 | Rico Cilliers | https://polyhaven.com/a/modular_fort_01 | CC0 1.0 | — |
| fort_wall_end | modular_fort_01 | Rico Cilliers | https://polyhaven.com/a/modular_fort_01 | CC0 1.0 | — |
| fort_stairs | modular_fort_01 | Rico Cilliers | https://polyhaven.com/a/modular_fort_01 | CC0 1.0 | — |
| fort_tower | modular_fort_01 | Rico Cilliers | https://polyhaven.com/a/modular_fort_01 | CC0 1.0 | — |
| pier_deck | modular_wooden_pier | Rico Cilliers | https://polyhaven.com/a/modular_wooden_pier | CC0 1.0 | — |
| pier_poles | modular_wooden_pier | Rico Cilliers | https://polyhaven.com/a/modular_wooden_pier | CC0 1.0 | — |
| pier_section | modular_wooden_pier | Rico Cilliers | https://polyhaven.com/a/modular_wooden_pier | CC0 1.0 | — |
| pier_frame | modular_wooden_pier | Rico Cilliers | https://polyhaven.com/a/modular_wooden_pier | CC0 1.0 | — |
| fire_pit | stone_fire_pit | Sebastian Platen | https://polyhaven.com/a/stone_fire_pit | CC0 1.0 | — |
| barrel | wine_barrel_01 | James Ray Cock | https://polyhaven.com/a/wine_barrel_01 | CC0 1.0 | — |
| barrel_old | wooden_barrels_01 | James Ray Cock | https://polyhaven.com/a/wooden_barrels_01 | CC0 1.0 | — |
| barrel_broken_a | wooden_barrels_01 | James Ray Cock | https://polyhaven.com/a/wooden_barrels_01 | CC0 1.0 | — |
| barrel_broken_b | wooden_barrels_01 | James Ray Cock | https://polyhaven.com/a/wooden_barrels_01 | CC0 1.0 | — |
| barrel_stave | wooden_barrels_01 | James Ray Cock | https://polyhaven.com/a/wooden_barrels_01 | CC0 1.0 | — |
| barrel_lid | wooden_barrels_01 | James Ray Cock | https://polyhaven.com/a/wooden_barrels_01 | CC0 1.0 | — |
| crate_a | wooden_crate_01 | James Ray Cock | https://polyhaven.com/a/wooden_crate_01 | CC0 1.0 | — |
| crate_b | wooden_crate_02 | James Ray Cock, Jurita Burger | https://polyhaven.com/a/wooden_crate_02 | CC0 1.0 | — |
| trough_a | planter_box_01 | James Ray Cock | https://polyhaven.com/a/planter_box_01 | CC0 1.0 | — |
| trough_b | planter_box_03 | James Ray Cock | https://polyhaven.com/a/planter_box_03 | CC0 1.0 | — |
| chest | treasure_chest | Rico Cilliers | https://polyhaven.com/a/treasure_chest | CC0 1.0 | — |
| vase_blue | antique_ceramic_vase_01 | James Ray Cock | https://polyhaven.com/a/antique_ceramic_vase_01 | CC0 1.0 | — |
| urn_a | ceramic_vase_01 | James Ray Cock | https://polyhaven.com/a/ceramic_vase_01 | CC0 1.0 | — |
| urn_b | ceramic_vase_02 | James Ray Cock | https://polyhaven.com/a/ceramic_vase_02 | CC0 1.0 | — |
| jug | ceramic_vase_04 | James Ray Cock | https://polyhaven.com/a/ceramic_vase_04 | CC0 1.0 | — |
| brass_urn | brass_vase_01 | Rico Cilliers | https://polyhaven.com/a/brass_vase_01 | CC0 1.0 | — |
| brass_ewer | brass_vase_02 | Rico Cilliers | https://polyhaven.com/a/brass_vase_02 | CC0 1.0 | — |
| brass_vial | brass_vase_03 | Rico Cilliers | https://polyhaven.com/a/brass_vase_03 | CC0 1.0 | — |
| brass_mug | brass_vase_04 | Rico Cilliers | https://polyhaven.com/a/brass_vase_04 | CC0 1.0 | — |
| clay_pot | planter_pot_clay | Amal Kumar | https://polyhaven.com/a/planter_pot_clay | CC0 1.0 | — |
| lidded_pot | ceramic_pot | Aron Łyczek | https://polyhaven.com/a/ceramic_pot | CC0 1.0 | — |
| cauldron | brass_pot_01 | Rico Cilliers | https://polyhaven.com/a/brass_pot_01 | CC0 1.0 | — |
| brass_basin | brass_pot_02 | Rico Cilliers | https://polyhaven.com/a/brass_pot_02 | CC0 1.0 | — |
| brass_pan | brass_pan_01 | Rico Cilliers | https://polyhaven.com/a/brass_pan_01 | CC0 1.0 | — |
| goblets | brass_goblets | Tina | https://polyhaven.com/a/brass_goblets | CC0 1.0 | — |
| bowl_a | wooden_bowl_01 | Oliver Harries | https://polyhaven.com/a/wooden_bowl_01 | CC0 1.0 | — |
| bowl_b | wooden_bowl_02 | Kuutti Siitonen | https://polyhaven.com/a/wooden_bowl_02 | CC0 1.0 | — |
| basket_a | wicker_basket_01 | Kuutti Siitonen | https://polyhaven.com/a/wicker_basket_01 | CC0 1.0 | — |
| basket_b | wicker_basket_02 | Kuutti Siitonen | https://polyhaven.com/a/wicker_basket_02 | CC0 1.0 | — |
| bucket | wooden_bucket_01 | James Ray Cock | https://polyhaven.com/a/wooden_bucket_01 | CC0 1.0 | — |
| tub | wooden_bucket_02 | James Ray Cock | https://polyhaven.com/a/wooden_bucket_02 | CC0 1.0 | — |
| candlestick | wooden_candlestick | Josh Dean | https://polyhaven.com/a/wooden_candlestick | CC0 1.0 | — |
| candle_brass | brass_candleholders | Tina | https://polyhaven.com/a/brass_candleholders | CC0 1.0 | — |
| candelabra_3 | brass_candleholders | Tina | https://polyhaven.com/a/brass_candleholders | CC0 1.0 | — |
| candelabra_5 | brass_candleholders | Tina | https://polyhaven.com/a/brass_candleholders | CC0 1.0 | — |
| lantern_oil | Lantern_01 | Rajil Jose Macatangay | https://polyhaven.com/a/Lantern_01 | CC0 1.0 | — |
| lantern_wood | wooden_lantern_01 | James Ray Cock | https://polyhaven.com/a/wooden_lantern_01 | CC0 1.0 | — |
| hanging_lamp | brass_diya_lantern | Bhargav Kubal | https://polyhaven.com/a/brass_diya_lantern | CC0 1.0 | — |
| chandelier | Chandelier_03 | Kirill Sannikov | https://polyhaven.com/a/Chandelier_03 | CC0 1.0 | — |
| chandelier_iron | lantern_chandelier_01 | Kirill Sannikov | https://polyhaven.com/a/lantern_chandelier_01 | CC0 1.0 | — |
| lamp_post | street_lamp_01 | Josh Dean | https://polyhaven.com/a/street_lamp_01 | CC0 1.0 | — |
| wall_lantern | street_lamp_02 | Josh Dean | https://polyhaven.com/a/street_lamp_02 | CC0 1.0 | — |
| fire_barrel | barrel_stove | MP | https://polyhaven.com/a/barrel_stove | CC0 1.0 | — |
| table_small | wooden_table_02 | Serhii Khromov | https://polyhaven.com/a/wooden_table_02 | CC0 1.0 | — |
| table_trestle | painted_wooden_table | Kirill Sannikov | https://polyhaven.com/a/painted_wooden_table | CC0 1.0 | — |
| table_round | round_wooden_table_01 | Ulan Cabanilla | https://polyhaven.com/a/round_wooden_table_01 | CC0 1.0 | — |
| table_low | WoodenTable_01 | Ethan Place | https://polyhaven.com/a/WoodenTable_01 | CC0 1.0 | — |
| table_gothic | gothic_coffee_table | Ulan Cabanilla | https://polyhaven.com/a/gothic_coffee_table | CC0 1.0 | — |
| console_gothic | ClassicConsole_01 | Kirill Sannikov | https://polyhaven.com/a/ClassicConsole_01 | CC0 1.0 | — |
| chair | painted_wooden_chair_02 | Kirill Sannikov | https://polyhaven.com/a/painted_wooden_chair_02 | CC0 1.0 | — |
| throne | WoodenChair_01 | Jake Mobley | https://polyhaven.com/a/WoodenChair_01 | CC0 1.0 | — |
| armchair_a | ArmChair_01 | Kirill Sannikov | https://polyhaven.com/a/ArmChair_01 | CC0 1.0 | — |
| armchair_b | GreenChair_01 | Kirill Sannikov | https://polyhaven.com/a/GreenChair_01 | CC0 1.0 | — |
| settee | Sofa_01 | Kirill Sannikov | https://polyhaven.com/a/Sofa_01 | CC0 1.0 | — |
| bench | painted_wooden_bench | Kirill Sannikov | https://polyhaven.com/a/painted_wooden_bench | CC0 1.0 | — |
| stool_a | painted_wooden_stool | Kirill Sannikov | https://polyhaven.com/a/painted_wooden_stool | CC0 1.0 | — |
| stool_b | wooden_stool_01 | Kuutti Siitonen | https://polyhaven.com/a/wooden_stool_01 | CC0 1.0 | — |
| footstool | wooden_stool_02 | Kuutti Siitonen | https://polyhaven.com/a/wooden_stool_02 | CC0 1.0 | — |
| camp_stool | folding_wooden_stool | Ulan Cabanilla | https://polyhaven.com/a/folding_wooden_stool | CC0 1.0 | — |
| bed | GothicBed_01 | Kirill Sannikov | https://polyhaven.com/a/GothicBed_01 | CC0 1.0 | — |
| cabinet_gothic | GothicCabinet_01 | Kirill Sannikov | https://polyhaven.com/a/GothicCabinet_01 | CC0 1.0 | — |
| commode | GothicCommode_01 | Kirill Sannikov | https://polyhaven.com/a/GothicCommode_01 | CC0 1.0 | — |
| cupboard | painted_wooden_cabinet | Kirill Sannikov | https://polyhaven.com/a/painted_wooden_cabinet | CC0 1.0 | — |
| hutch | painted_wooden_cabinet_02 | Kirill Sannikov | https://polyhaven.com/a/painted_wooden_cabinet_02 | CC0 1.0 | — |
| shelf_small | painted_wooden_shelves | Kirill Sannikov | https://polyhaven.com/a/painted_wooden_shelves | CC0 1.0 | — |
| bookshelf | wooden_bookshelf_worn | Ulan Cabanilla | https://polyhaven.com/a/wooden_bookshelf_worn | CC0 1.0 | — |
| shelf_plain | Shelf_01 | Gabriel Radić | https://polyhaven.com/a/Shelf_01 | CC0 1.0 | — |
| library_cabinet | vintage_cabinet_01 | Rico Cilliers | https://polyhaven.com/a/vintage_cabinet_01 | CC0 1.0 | — |
| drawer_chest | vintage_wooden_drawer_01 | James Ray Cock | https://polyhaven.com/a/vintage_wooden_drawer_01 | CC0 1.0 | — |
| picnic_table | wooden_picnic_table | Ulan Cabanilla | https://polyhaven.com/a/wooden_picnic_table | CC0 1.0 | — |
| books_row | book_encyclopedia_set_01 | John Malcolm | https://polyhaven.com/a/book_encyclopedia_set_01 | CC0 1.0 | — |
| journal | binder_notebook | DaDrood | https://polyhaven.com/a/binder_notebook | CC0 1.0 | — |
| mirror_ornate | ornate_mirror_01 | James Ray Cock | https://polyhaven.com/a/ornate_mirror_01 | CC0 1.0 | — |
| sign_board | standing_chalkboard_01 | ParzivalCG | https://polyhaven.com/a/standing_chalkboard_01 | CC0 1.0 | — |
| ladder_a | wooden_ladder | Miroslav Turura | https://polyhaven.com/a/wooden_ladder | CC0 1.0 | — |
| ladder_b | wooden_ladder_02 | JN_3DPRINTINGNERD | https://polyhaven.com/a/wooden_ladder_02 | CC0 1.0 | — |
| spinning_wheel | spinning_wheel_01 | Sofia Pahaoja | https://polyhaven.com/a/spinning_wheel_01 | CC0 1.0 | — |
| pedestal_table | side_table_tall_01 | James Ray Cock | https://polyhaven.com/a/side_table_tall_01 | CC0 1.0 | — |
| nightstand | ClassicNightstand_01 | Kirill Sannikov | https://polyhaven.com/a/ClassicNightstand_01 | CC0 1.0 | — |
| table_round_small | round_wooden_table_02 | Ulan Cabanilla | https://polyhaven.com/a/round_wooden_table_02 | CC0 1.0 | — |
| plate_wood | carved_wooden_plate | Jan Martens | https://polyhaven.com/a/carved_wooden_plate | CC0 1.0 | — |
| pickaxe | picke_dirty_01 | Dennis Halbeck | https://polyhaven.com/a/picke_dirty_01 | CC0 1.0 | — |
| spade | rusted_spade_01 | Blemonade | https://polyhaven.com/a/rusted_spade_01 | CC0 1.0 | — |
| axe | wooden_axe | Ulan Cabanilla | https://polyhaven.com/a/wooden_axe | CC0 1.0 | — |
| smith_hammer | cross_pein_hammer | Tics | https://polyhaven.com/a/cross_pein_hammer | CC0 1.0 | — |
| saw | handsaw_wood | Konrad Szałankiewicz | https://polyhaven.com/a/handsaw_wood | CC0 1.0 | — |
| broom | wooden_broom | Balen | https://polyhaven.com/a/wooden_broom | CC0 1.0 | — |
| vice | bench_vice_01 | Yann Kervran, Antanas Kep | https://polyhaven.com/a/bench_vice_01 | CC0 1.0 | — |
| shield | kite_shield | Ulan Cabanilla | https://polyhaven.com/a/kite_shield | CC0 1.0 | — |
| sword | antique_estoc | James Ray Cock, Ulan Cabanilla | https://polyhaven.com/a/antique_estoc | CC0 1.0 | — |
| war_hammer | ornate_war_hammer | James Ray Cock, Ulan Cabanilla | https://polyhaven.com/a/ornate_war_hammer | CC0 1.0 | — |
| mace | ornate_medieval_mace | Ulan Cabanilla | https://polyhaven.com/a/ornate_medieval_mace | CC0 1.0 | — |
| dagger | ornate_medieval_dagger | Sofia Pahaoja | https://polyhaven.com/a/ornate_medieval_dagger | CC0 1.0 | — |
| snow_boulder_a | boulder_01 | Rico Cilliers | https://polyhaven.com/a/boulder_01 | CC0 1.0 | snow cap baked onto up-facing surfaces |
| snow_boulder_b | namaqualand_boulder_04 | Jenelle van Heerden | https://polyhaven.com/a/namaqualand_boulder_04 | CC0 1.0 | snow cap baked onto up-facing surfaces |
| snow_rock_a | rock_moss_set_01 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_01 | CC0 1.0 | snow cap baked onto up-facing surfaces |
| snow_rock_b | rock_moss_set_02 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_02 | CC0 1.0 | snow cap baked onto up-facing surfaces |
| snow_rock_c | rock_moss_set_02 | Kless Gyzen | https://polyhaven.com/a/rock_moss_set_02 | CC0 1.0 | snow cap baked onto up-facing surfaces |
| snow_stump | tree_stump_02 | Rob Tuytel | https://polyhaven.com/a/tree_stump_02 | CC0 1.0 | snow cap baked onto up-facing surfaces |
| snow_log | dead_tree_trunk_02 | Jenelle van Heerden, Rico Cilliers | https://polyhaven.com/a/dead_tree_trunk_02 | CC0 1.0 | snow cap baked onto up-facing surfaces |
| mossy_log | dead_tree_trunk_02 | Jenelle van Heerden, Rico Cilliers | https://polyhaven.com/a/dead_tree_trunk_02 | CC0 1.0 | moss layer (ambientCG Moss002) baked onto up-facing surfaces |
| mossy_log_thin | dead_tree_trunk | Rob Tuytel | https://polyhaven.com/a/dead_tree_trunk | CC0 1.0 | moss layer (ambientCG Moss002) baked onto up-facing surfaces |
| mossy_stump | tree_stump_01 | Rob Tuytel | https://polyhaven.com/a/tree_stump_01 | CC0 1.0 | moss layer (ambientCG Moss002) baked onto up-facing surfaces |
| marsh_weed_a | shrub_03 | Rico Cilliers | https://polyhaven.com/a/shrub_03 | CC0 1.0 | scaled-up x3.6 (tall marsh stalk) |
| marsh_weed_b | shrub_03 | Rico Cilliers | https://polyhaven.com/a/shrub_03 | CC0 1.0 | scaled-up x4.2 (tall marsh stalk) |
| brazier_bowl | brass_pot_02 | Rico Cilliers | https://polyhaven.com/a/brass_pot_02 | CC0 1.0 | brass basin scaled up x2.6 into a fire bowl |
| colossus_head | marble_bust_01 | Rico Cilliers | https://polyhaven.com/a/marble_bust_01 | CC0 1.0 | marble bust scaled up x6 into a colossal statue |
| lion_monument | lion_head | Tina | https://polyhaven.com/a/lion_head | CC0 1.0 | lion head ornament scaled up x5 into a monument |
| bull_idol | bull_head | Tina | https://polyhaven.com/a/bull_head | CC0 1.0 | bull head ornament scaled up x5 into an idol |
| journal_open | binder_notebook | DaDrood | https://polyhaven.com/a/binder_notebook | CC0 1.0 | — |

## Skies — `game/assets/env/sky/` (8 HDRIs, 2K Radiance .hdr)

| file | source | source asset | author | source URL | licence |
|---|---|---|---|---|---|
| dusk.hdr | Poly Haven | qwantani_dusk_2_puresky | Greg Zaal, Jarod Guest | https://polyhaven.com/a/qwantani_dusk_2_puresky | CC0 1.0 |
| night.hdr | Poly Haven | kloppenheim_02_puresky | Greg Zaal, Jarod Guest | https://polyhaven.com/a/kloppenheim_02_puresky | CC0 1.0 |
| night_dark.hdr | Poly Haven | kloppenheim_07_puresky | Greg Zaal, Jarod Guest | https://polyhaven.com/a/kloppenheim_07_puresky | CC0 1.0 |
| cave.hdr | Poly Haven | drachenfels_cellar | Andreas Mischok | https://polyhaven.com/a/drachenfels_cellar | CC0 1.0 |
| overcast.hdr | Poly Haven | mud_road_puresky | Sergey Rudavin, Jarod Guest | https://polyhaven.com/a/mud_road_puresky | CC0 1.0 |
| snow_overcast.hdr | Poly Haven | snow_field_2_puresky | Jarod Guest, Sergej Majboroda | https://polyhaven.com/a/snow_field_2_puresky | CC0 1.0 |
| desert_dusk.hdr | Poly Haven | syferfontein_0d_clear_puresky | Greg Zaal, Jarod Guest | https://polyhaven.com/a/syferfontein_0d_clear_puresky | CC0 1.0 |
| swamp_fog.hdr | Poly Haven | kloofendal_misty_morning_puresky | Greg Zaal | https://polyhaven.com/a/kloofendal_misty_morning_puresky | CC0 1.0 |

## Fonts — `game/assets/fonts/`

All three families are licensed under the **SIL Open Font License, Version 1.1**; the licence text of each family is shipped next to the fonts (`OFL_NotoSerifTC.txt`, `OFL_NotoSansTC.txt`, `OFL_Cinzel.txt`). The fonts are used unmodified.

| file | family / style | copyright (from the font's name table) | source URL | licence |
|---|---|---|---|---|
| Cinzel-Variable.ttf | Cinzel Regular (variable wght) | Copyright 2020 The Cinzel Project Authors (https://github.com/NDISCOVER/Cinzel) | https://github.com/google/fonts/tree/main/ofl/cinzel | SIL OFL 1.1 (`OFL_Cinzel.txt`) |
| NotoSansTC-Bold.otf | Noto Sans TC Bold | © 2014-2021 Adobe (http://www.adobe.com/). | https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/TC | SIL OFL 1.1 (`OFL_NotoSansTC.txt`) |
| NotoSansTC-Medium.otf | Noto Sans TC Medium | © 2014-2021 Adobe (http://www.adobe.com/). | https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/TC | SIL OFL 1.1 (`OFL_NotoSansTC.txt`) |
| NotoSansTC-Regular.otf | Noto Sans TC Regular | © 2014-2021 Adobe (http://www.adobe.com/). | https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/TC | SIL OFL 1.1 (`OFL_NotoSansTC.txt`) |
| NotoSerifTC-Black.otf | Noto Serif TC Black | © 2017-2024 Adobe (http://www.adobe.com/). | https://github.com/notofonts/noto-cjk/tree/main/Serif/SubsetOTF/TC | SIL OFL 1.1 (`OFL_NotoSerifTC.txt`) |
| NotoSerifTC-Bold.otf | Noto Serif TC Bold | © 2017-2024 Adobe (http://www.adobe.com/). | https://github.com/notofonts/noto-cjk/tree/main/Serif/SubsetOTF/TC | SIL OFL 1.1 (`OFL_NotoSerifTC.txt`) |
| NotoSerifTC-Medium.otf | Noto Serif TC Medium | © 2017-2024 Adobe (http://www.adobe.com/). | https://github.com/notofonts/noto-cjk/tree/main/Serif/SubsetOTF/TC | SIL OFL 1.1 (`OFL_NotoSerifTC.txt`) |
| NotoSerifTC-Regular.otf | Noto Serif TC Regular | © 2017-2024 Adobe (http://www.adobe.com/). | https://github.com/notofonts/noto-cjk/tree/main/Serif/SubsetOTF/TC | SIL OFL 1.1 (`OFL_NotoSerifTC.txt`) |

## UI icons — `game/assets/icons/gi/<author>/<name>.svg` (4239 SVGs)

Source: the game-icons.net icon set, https://game-icons.net — repository https://github.com/game-icons/icons. Licence: **Creative Commons Attribution 3.0 Unported (CC BY 3.0)**, https://creativecommons.org/licenses/by/3.0/ ; the icons by Viscious Speed and Zeromancer are CC0. The repository's `license.txt` is shipped as `game/assets/icons/gi/license.txt`.

**Attribution text to show in the game's credits screen** (required by CC BY 3.0, wording asked for by the icon set):

> Icons made by Lorc, Delapouite, John Colburn, Felbrigg, John Redman, Carl Olsen, Sbed, PriorBlue, Willdabeast, Viscious Speed, Lord Berandas, Irongamer, HeavenlyDog, Lucas, Faithtoken, Skoll, Andy Meneely, Cathelineau, Kier Heyl, Aussiesim, Sparker, Zeromancer, Rihlsul, Quoting, Guard13007, DarkZaitzev, SpencerDub, GeneralAce135, Zajkonur, Catsu, Starseeker, Pepijn Poolman, Pierre Leducq, Caro Asercion, SeregaCthtuf. Available on https://game-icons.net — licensed under CC BY 3.0 (https://creativecommons.org/licenses/by/3.0/).

The author of an icon is the name of its folder; if the shipped game uses only some icons, the list may be cut down to the authors actually used. Modification made: the opaque black 512×512 background square was removed from each SVG (white glyph on transparent) so icons can be tinted. Untouched originals: `Y:\assets_src\env\game-icons\icons`.

Licence file of the repository (`license.txt`), quoted:

```
Icons provided under the Creative Commons 3.0 BY or CC0 if mentioned below.

Each sub-folders in this archive correspond to a different contributor :

- Lorc, http://lorcblog.blogspot.com
- Delapouite, http://delapouite.com
- John Colburn, http://ninmunanmu.com
- Felbrigg, http://blackdogofdoom.blogspot.co.uk
- John Redman, http://www.uniquedicetowers.com
- Carl Olsen, https://twitter.com/unstoppableCarl
- Sbed, http://opengameart.org/content/95-game-icons
- PriorBlue
- Willdabeast, http://wjbstories.blogspot.com
- Viscious Speed, http://viscious-speed.deviantart.com - CC0
- Lord Berandas, http://berandas.deviantart.com
- Irongamer, http://ecesisllc.wix.com/home
- HeavenlyDog, http://www.gnomosygoblins.blogspot.com
- Lucas
- Faithtoken, http://fungustoken.deviantart.com
- Skoll
- Andy Meneely, http://www.se.rit.edu/~andy/
- Cathelineau
- Kier Heyl
- Aussiesim
- Sparker, http://citizenparker.com
- Zeromancer - CC0
- Rihlsul
- Quoting
- Guard13007, https://guard13007.com
- DarkZaitzev, http://darkzaitzev.deviantart.com
- SpencerDub
- GeneralAce135
- Zajkonur
- Catsu
- Starseeker
- Pepijn Poolman
- Pierre Leducq
- Caro Asercion
- SeregaCthtuf

Please, include a mention "Icons made by {author}" in your derivative work.

If you use them in one of your project, don't hesitate to drop a message to delapouite@gmail.com or ping @GameIcons on twitter.

More info and icons available at https://game-icons.net
```

Contributing authors as listed in `license.txt` (35), with the number of icons shipped per author folder:

| author | link / note | folder | icons |
|---|---|---|---|
| Lorc | http://lorcblog.blogspot.com | `gi/lorc/` | 1429 |
| Delapouite | http://delapouite.com | `gi/delapouite/` | 2022 |
| John Colburn | http://ninmunanmu.com | `gi/john-colburn/` | 1 |
| Felbrigg | http://blackdogofdoom.blogspot.co.uk | `gi/felbrigg/` | 5 |
| John Redman | http://www.uniquedicetowers.com | `gi/john-redman/` | 3 |
| Carl Olsen | https://twitter.com/unstoppableCarl | `gi/carl-olsen/` | 6 |
| Sbed | http://opengameart.org/content/95-game-icons | `gi/sbed/` | 95 |
| PriorBlue | — | `gi/priorblue/` | 7 |
| Willdabeast | http://wjbstories.blogspot.com | `gi/willdabeast/` | 7 |
| Viscious Speed | http://viscious-speed.deviantart.com - CC0 | `gi/viscious-speed/` | 121 |
| Lord Berandas | http://berandas.deviantart.com | `gi/lord-berandas/` | 10 |
| Irongamer | http://ecesisllc.wix.com/home | `gi/irongamer/` | 1 |
| HeavenlyDog | http://www.gnomosygoblins.blogspot.com | `gi/heavenly-dog/` | 2 |
| Lucas | — | `gi/lucasms/` | 5 |
| Faithtoken | http://fungustoken.deviantart.com | `gi/faithtoken/` | 10 |
| Skoll | — | `gi/skoll/` | 172 |
| Andy Meneely | http://www.se.rit.edu/~andy/ | `gi/andymeneely/` | 2 |
| Cathelineau | — | `gi/cathelineau/` | 29 |
| Kier Heyl | — | `gi/kier-heyl/` | 3 |
| Aussiesim | — | `gi/aussiesim/` | 52 |
| Sparker | http://citizenparker.com | `gi/sparker/` | 1 |
| Zeromancer | CC0 | `gi/zeromancer/` | 2 |
| Rihlsul | — | `gi/rihlsul/` | 3 |
| Quoting | — | `gi/quoting/` | 12 |
| Guard13007 | https://guard13007.com | `gi/guard13007/` | 3 |
| DarkZaitzev | http://darkzaitzev.deviantart.com | `gi/darkzaitzev/` | 32 |
| SpencerDub | — | `gi/spencerdub/` | 1 |
| GeneralAce135 | — | `gi/generalace135/` | 2 |
| Zajkonur | — | `gi/zajkonur/` | 2 |
| Catsu | — | `gi/catsu/` | 1 |
| Starseeker | — | `gi/starseeker/` | 1 |
| Pepijn Poolman | — | `gi/pepijn-poolman/` | 1 |
| Pierre Leducq | — | `gi/pierre-leducq/` | 3 |
| Caro Asercion | — | `gi/caro-asercion/` | 127 |
| SeregaCthtuf | — | `gi/seregacthtuf/` | 5 |

Folders in the repository without a line of their own in `license.txt`: `gi/badges/` (59 icons), `gi/various-artists/` (2 icons) — credit them as game-icons.net contributors (CC BY 3.0).

