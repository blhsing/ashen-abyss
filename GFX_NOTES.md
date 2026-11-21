# 燼淵 graphics pass — working log

Screenshots: baseline `Y:\shots\gfx\base\`, iterations `Y:\shots\gfx\it*\`, finals `Y:\shots\gfx\final\`.

## Tools added
- `EnvProbe` (src/EnvProbe.cs, created by Game for any `--gfx-*` switch; header lists them all). One process tours a
  list of areas (`--gfx-tour=wastes,frost,…`, uses `Game.HarnessTravel`, so no quest gating), takes shots
  (`--gfx-at=4,9 --gfx-out=…`), logs fps / GPU ms / draws / primitives per area to `<out>\perf.txt`, can render the 3D
  picture off screen at any size with the real upscaler (`--gfx-res=3840x2160`, `--gfx-beauty` saves the 4K frames),
  overrides preset / scale / upscaler without touching the saved settings (`--gfx-q`, `--gfx-scale`, `--gfx-up`),
  switches features off one by one and logs what each costs (`--gfx-sweep`), and lays one of every ground effect
  round the hero (`--gfx-fx`).
- `Y:\tools\gfx_batch.ps1` (one run per theme, used for the baseline), `gfx_sheet.ps1` (contact sheets),
  `gfx_stats.ps1` (mean luma, share below mid-grey / near black / clipped per screenshot),
  `gfx_tmp\themes.pl` (table of per-theme look values written into LevelThemes.cs).

## Baseline (before any change) — `Y:\shots\gfx\base\*_1.png, *_2.png`, numbers in `base\perf.txt`
All 20 themes at 1600x900 windowed ran at the 60 fps vsync cap (no head-room figure), 0.4-4.0 M primitives per frame.
- one flat veil over every outdoor region (distance fog between lens and ground: the camera hung 24 m away, so every
  pixel got the same ~17 % of fog colour) → monochrome blue / green / magenta frames with no blacks (near-black
  pixels 1-4 %, nothing above mid-grey either);
- the sun lit the scene from behind the camera (front light, shadows falling away up-screen): no shape;
- hero about 9 % of the frame height;
- burning ground = flat orange disc; slashes and crit flashes = opaque white blobs; friendly auras = grey discs;
- wastes: a 9 m × 1.4 m dark slab = `dead_trunk` (a 0.3 m wide model) fitted to a 0.6 m pillar footprint (×4.5);
- frost / wastes ground: the 3 m texture tile repeating as a visible grid;
- void pits: bright light-blue sheets; wall lids fogged to the region colour; wet cave nearly black;
- town houses textured again (the lead's wall-shader fix confirmed); NPC stand-ins in T-pose (not graphics).

## Systemic changes (problem → change → where to look)
1. **Framing** — hero 9 % → 13.5 % of frame height at zoom 1 (16 % in town, 9 % at the boss pull-back). CameraRig:
   pitch 52°→48°, FOV 30°→32°, distance 24→15.6 m, focus 0.9 m south of the hero (perspective shows 10 m up-screen
   but only 4 m down-screen, and the action bar covers the bottom). Wheel range 0.7-1.6 and scripted zoom unchanged.
   `CameraRig.ViewReach/FarGround` give level code the visible range; light culling, sun shadow distance, fog start and
   froxel depth follow the zoom (EnvLights.ApplyZoom).
2. **Fog** — veil removed. Distance fog is now depth-mode and starts a few metres *behind* the hero's plane (tracks
   zoom), height fog only below the floor (pits fill with it), and the region hue is carried by a slab of volumetric
   ground mist (a world-sized FogVolume with a small fog shader: density only in the first 2-3 m, drifting banks).
3. **Light direction** — the sun/moon always stands behind the scene and shines toward the lens
   (EnvAtmosphere.SunDir, `LevelTheme.SunElevation`): long shadows fall forward, wet ground glints, everything gets a
   lit edge. Indoors the dim fill comes straight down so wall faces stay dark.
4. **Tone** — ACES instead of Filmic, exposure recalibrated, 1-D colour-correction ramp for split toning (shadows
   lean to the ambient hue, highlights to fire), glow limited to emissives with five soft levels, vignette + faint
   grain as a canvas layer owned by the camera rig, cool shadowless rim spot that follows the hero from behind.
   The hero's own ember light is balanced per theme by EnvLights (it is created in Player.cs).
5. **Fire light** — steeper attenuation (pools, not floods) with more energy; ranges ×0.78; every flame card
   (env_flame) now carries a soft halo so candles and lanterns read as light sources from the high camera.
6. **Ground** — env_ground: loose ground layers are mixed with a second, 3.3× larger rotated look at the same
   texture in noise patches (kills the tile grid), slow value/hue drift, theme wetness (damp patches darken and turn
   glossy, puddles go mirror-flat), stronger baked occlusion next to walls.
7. **Occlusion cut** — the see-through disc also fades anything within 2-5 m of the lens (tree tops under the lower
   camera). `IN_SHADOW_PASS` is a real Godot 4.7 built-in (checked against the shader reference) and stays.
8. **Props** — tagged slots get the piece they were reserved for (`house`/`hall`/`inn` sized from `PropSlot.Half`,
   `forge` (new), `stall`, `tent`, `well`, `bench`, `lantern`, `torch`, `brazier`, `camp`, `ruin`); pillar / rock
   fits are capped in height and in scale relative to the model's natural size (the slab); border trees capped below
   the camera; plants no longer cast shadows; ground scatter (pebble drifts of the local stone + small natural
   props, hash-placed, clear of paths, slots and points of interest) fills empty floors.
9. **Pits** — lava / glow lights: one per block of shore cells, block size grows until the map needs ≤ 110, and
   the light manager keeps only the nearest 5-18 glow lights and 8-24 fires on (was one every 3 cells, unbounded).
   Void mist dimmed and lowered (was a bright sheet); wall lids no longer take fog.
10. **Effects** — see "Combat readability" below.
11. **Shader warnings** — char / spectral instance uniforms pinned to fixed `instance_index` slots (fixes the
    "same instance shader uniform 'dissolve' with different indices" warning flood).
12. **Performance** — see "Performance" below.

## Combat readability (Fx.cs + fx_* shaders) — galleries: `it2\fxw_1.png` (first), `it7\town_1.png`, `it7\fxc_1.png`
- **Ground zones** (`Fx.ZoneVisual`, `fx_zone.gdshader` rewritten, premultiplied blending so one quad can both darken
  and add light): per element a floor layer (scorch with breathing ember cracks / rime with fracture lines / charged
  ground with jumping arcs / slick venom pool with bubbles / a hole in the light with violet wisps / churned dust)
  plus a volume standing on it: fire = a MultiMesh field of flame cards (the torch flame shader, dimmer variant) +
  licking puffs + climbing embers + thin smoke + flickering light; frost / poison / shadow = a low bank of mist or
  fumes + rising motes; lightning = ground sparks + sky bolts; physical = bolt hail. Friendly zones end in a ragged
  soft edge, hostile ones are an exact circle with a hard pulsing `Palette.Danger` rim.
- **Telegraphs**: hostile fill + hard outline kept (unmistakable in every theme, see the red rings / cone / line);
  friendly marks are now a thin line with almost no fill, so the player's own skills never look like danger.
- **Impacts**: the white-hot core of `Hit` / `Burst` / meteor landings is smaller and tighter (gain 2.2 → 1.0-1.3,
  falloff power 1.6 → 3), light flashes slightly stronger instead — the hit lights the scene rather than hiding it.
- **Slashes, rings, beams, waves, pillars, portal**: brightness scaled back to sit just above the bloom threshold
  (they were 2-4× over it and bloomed into opaque white fans).
- **Hit flash on characters** (`char.gdshader`): rim-weighted (0.3 + fresnel × 1.3) instead of whiting out the whole
  body; every character keeps 3.5 % of its albedo as a floor and a stronger fresnel rim, so nothing sinks into black.
- **Loot beams** keep their rarity colour (legendary / unique are taller and haloed, not whiter); foot pools and
  auras use the new "plain glow" zone kind.
- Budgets unchanged (70 one-shot emitters, 12 flash lights, 10 soft lights); particle counts follow the preset.

## Per theme (LevelThemes.cs; the table of values lives in `Y:\tools\gfx_tmp\themes.pl`)
New LevelTheme fields used for the look: `Wet`, `GroundGloss`, `WallGloss`, `MistDensity/MistTop/MistColor`, `FogFar`,
`RimEnergy/RimColor`, `Vignette`, `Grade`, `SunElevation`, `Scatter/ScatterProps`. Fire colours were shifted from true
black-body values (which turn every wall crimson under ACES) to slightly less saturated ones.

| theme | accent / read | what was tuned |
|---|---|---|
| town | warm amber pools on wet cobbles under a cool violet dusk | low moon (38°), wet 0.3, amber mist, houses from tagged slots (cottages, stone keepers' hall with banner, inn), smithy with glowing hearth, stalls, well, tent |
| wastes | ash-grey, pale-blue moonlight, long tree shadows | brighter cool moon at 34°, darker ash ground (lum max 0.13), blue mist, pebble / twig scatter |
| frost | snow under cold light, warm torches the only colour | low sun 30°, far fog 0.85, almost dry, snow ground with macro variation |
| barrows | grey-green burial ground, sickly mist | desaturated green ambient, green mist 0.045/m, wet 0.3, candles among the graves |
| mire | yellow-green rot, wet ground, spores | wet 0.55, thick mist to 3.2 m, dark still water with a faint toxic shimmer (no sparkle) |
| sands | salmon dunes at dusk, long violet-brown shadows | sun at 24°, warm sun colour, violet ambient, dry, desaturated sand |
| sanctum | cold white marble city | glossy flagstones, wet 0.3, saturation 0.72, buildings get real tiled roofs (were black lids) |
| abyss | black-violet basalt, lava and embers the only warmth | violet moon, darker neutral ground, orange mist only from lava light, ember rocks |
| crypt | green-grey stone, warm torches | green ambient, wet 0.35, candles in the dark stretches |
| cave | brown root cave, lantern light | higher ambient + fill, lanterns on posts added, wet 0.45 |
| graveyard | moonlit burial ground, blue mist hugging the graves | low dense mist (0.065/m to 1.8 m), wet 0.4, candles |
| icehall | blue-grey garrison, lit ice | lit ice crystals as extra lights, stone wall tops (were snow slabs), glossy floor |
| icecave | glacier blue, glowing ice | glossy ice floor, crystal lights, dark water |
| wetcave | drowned green grotto, glowing fungus | wet 0.85, green mist, glowshroom lights |
| brasshall | warm brass and violet shadow | amber mist, glossy tiles, candles |
| sandtomb | dusty violet-rose tomb | amber mist, dry, candles |
| cathedral | marble nave, candle light | polished marble (gloss 0.6) darkened to lum 0.13 so candles do not blow it out |
| forge | soot-black stone, red forge glow | ember rocks, orange mist lit by lava, higher ambient |
| lavacave | obsidian and lava | ember rocks, lava bank lights capped |
| rift | violet air over whatever layout it rolled | `LevelBuilder.RiftVariant`: Level.cs never asks for the `rift` theme (it rolls a hall or cave theme), so the builder lays the rift's ambient, mist, motes and rim over the rolled theme |

## Performance (Gfx.cs, EnvLights.cs, EnvProps.cs) — numbers in `Y:\shots\gfx\perf4k\perf.txt`, `sweep*\perf.txt`, `presets*\perf.txt`
Method: `--gfx-res=3840x2160 --gfx-novsync --gfx-nogov` renders the real 4K pipeline off screen (internal 2573×1447 at
高) and logs the GPU time of that viewport; `--gfx-sweep` then switches features off one by one.
**Caveat:** the machine is shared with other agents' jobs (Blender builds, model inference, QA runs). The same static
scene measured 10 ms in a quiet minute and 29 ms ten minutes later; only quiet-window numbers are quoted below.

What the first 4K sweep showed (crypt / town / wastes, GPU ms, `it3\perf.txt`):
- FSR 2: 9-14 ms on its own → replaced by TAA at internal resolution + FSR 1 spatial upscale (about 3-5 ms together).
- fire-light shadows: 6-13 ms. Cause 1: cube shadow maps (6 scene passes per light) → dual paraboloid except at 極致.
  Cause 2: every prop material ran a vertex shader that reads TIME (wind sway), which marks all prop meshes as
  animated and forces every shadow map in reach to be redrawn every frame → the sway code is compiled only into the
  shader variant used by plants (`EnvAssets.PropShader`), and plants no longer cast shadows.
- sun shadows: 3-6 ms with two cascades → one 2048 map over the 27-48 m the lens shows (about 2 cm texels).
- SSAO medium + SSIL: 3-5 ms → SSAO low, SSIL only at 極致.
- burning ground: each flame card was a 3×3 flame-height quad for its halo → fields use tight quads without halo.
- lava bank lights: unbounded → ≤ 110 per map, nearest 12 on (5 / 8 / 12 / 18 by preset); fires 8 / 12 / 16 / 24.

Presets (4K output): 低 1080 lines, FSR 1 + FXAA, no fire shadows / SSAO / mist · 中 1296 lines, 1 fire shadow, SSAO,
48³ mist · 高 1440 lines, 2 fire shadows, SSAO, 64³ mist · 極致 1620 lines, 4 cube fire shadows with penumbrae, two sun
cascades at 4096, high SSAO, SSIL, SSR, SDFGI, 96³ mist, more particles and dressing.
Automatic render scale = a governor (`Gfx.Govern`, called by the camera rig): when the 3D viewport needs more than
14 ms of GPU time it trims the internal resolution in steps (never below 72 % of the preset's scale, never below 0.5),
and gives it back below 10.2 ms. A fixed scale chosen in the settings is never touched.

## Character readability (lead's note, 15 Nov) — check shots `it12\` (first attempt), `it13\` (actor lights)
Problem: in `Y:\shots\run\monlook.png` hero and monster were a dim blob on the green barrows ground; none of the new
2K PBR detail read. Findings and changes:
- `char.gdshader` used the ORM red channel as ambient occlusion for every material, but only the hero materials
  carry occlusion there (CharModel now logs every material's bindings with `--verbose`: albedo / normal ×1 / ORM are
  bound on all of them, roughness and metallic factors are 1 = texture driven). AO is now applied only when the
  imported material has an occlusion texture (`ao_amount`), otherwise 1.
- Characters are on render layer 2 as well (`CharModel.ActorLayer`); every level adds two shadowless directional
  lights with that cull mask (`EnvAtmosphere.AddActorLights`): **ActorKey** from the lens side (strength follows the
  theme's ambient: about one stop over unlit ground, nothing where a torch already lights the figure) and **ActorRim**
  from behind with specular 1.8 (plate glints, cloth stays matte, every silhouette gets a lit edge). Real lights, so
  the normal / roughness maps shape the result. `LevelTheme.ActorFill / ActorRim` scale them per theme.
- A first attempt with an emissive fill made figures read but flat and chalky (`it12\cave_3.png`) — kept only as a 5 %
  safety net.
- Normal map depth ×1.25 on characters (the lens is 15 m away), ORM sampled anisotropically, SPECULAR 0.6.
- Gameplay rim (class colour, affix colour, summon teal) is wider and stronger (`rim_color × (edge^1.7 × 2 + 0.05)`);
  hostile models (`mon_*`, `boss_*`) get a faint ember-red default rim until an affix colour replaces it.
- Hit flash is rim-weighted instead of whiting the body out.
- Barrows: neutral stone and ambient, green kept in fog / mist, moon neutral-cool, torches 2200 K, candles every 13 m.

## 15 Nov, 16:30 — characters looked self-lit (user) → fixed at the source; environment level A / B for the user to choose
Tools: `EnvProbe --gfx-mask` saves `<shot>_mask.png` (hero flat green, other actors flat magenta, time held);
`Y:\tools\gfx_luma.ps1 -Dir … [-Hud]` prints median / p75 / p95 sRGB luma per shot and, with a mask, mean luma of
hero, other actors and the ground within about 1.6 m of each, with ratios. `--gfx-dark` selects environment level B.

**Measured (hero or actors ÷ nearby ground, sRGB luma):**
| state | town | wastes | barrows | frost | crypt | cave |
|---|---|---|---|---|---|---|
| before (torso box, no mask) hero | 1.81 | 0.92-1.03 | 1.02-1.22 | 0.67-0.69 | 1.09-1.90 | 2.85 |
| first over-correction (albedo ×0.55) hero / actors | 0.67-0.79 / 0.62 | 0.48-0.50 / 0.38-0.52 | 0.31-0.49 / 0.21-0.74 | 0.21-0.29 / 0.57 | 0.42-0.82 / 0.45-0.92 | – |
| now, level A hero / actors | – | 0.89 / 0.64-1.27 | 0.38-0.51 (moon behind) / 0.62-1.25 | – | 1.45-1.69 / 1.01-1.24 | – |
| now, level B hero / actors | – | 0.51-0.69 / 1.04-1.38 | 1.04-1.18 / 1.03-1.10 | – | 0.79-0.94 / 1.12-1.15 | – |
(The spread inside a theme is geometry: a figure with the moon behind it shows its shadow side to the lens.)

**Causes found, each removed at the source:**
1. Two directional lights that only characters received (ActorKey, ActorRim — added the same morning for the
   "actors a stop brighter" note, now withdrawn) → deleted, together with the render-layer mask.
2. Unshaded albedo added in `char.gdshader` (`min_light`, then `scene_fill`) → deleted. Characters add no light.
3. `rim_color` emission on every actor (hero class colour, summons, elites; I had widened it and given every hostile
   a default ember rim) → a thin `fresnel³` edge; the hero's is scaled to a quarter, hostiles have none unless gameplay
   sets an affix colour. `scene_rim` is 5 % of the back-light colour on `fresnel⁴`: barely visible.
4. `emis_*` materials were multiplied ×2.2 and clamped to 2.5-9 with a full pulse → clamp 1-2.2, half pulse.
5. The hero's carried light (Player.cs: a metre from the head; EnvLights had raised it to energy 2.6-3.3) lit the
   hero several times more than the ground → hidden; the light manager hangs its own "HeroFire" 4.2 m up and 1.3 m
   towards the lens, where body and ground are about equally far from it.
6. The cool spot light that followed the lens → off.
7. Environment albedo is levelled into 0.04-0.17 and tinted per theme; characters used raw textures → the char
   shader now applies the theme's prop tint × 0.8 and 45 % of its desaturation (`CharModel.SetSceneGrade`).
8. Plate (metallic 1) had no diffuse and nothing to reflect: 0.4 × ground while cloth sat at 0.9 → half the metal is
   kept as diffuse. Roughness floor 0.24 so relief maps do not sparkle; normal depth back to 1.0.
9. (other direction) ORM red was used as occlusion on materials that have none.

**Environment levels** (`EnvAtmosphere.Dark`): A = as it was; B = moon ×0.11, exposure 1.0, no contrast crush,
inverse-square torches with 0.8 range, fog colour at 3.5 % energy, mist lit only by fires, sky out of the ambient,
hero fire 7.6 m. Finding: the ACES toe turns scene-linear 0.004 into 0.0006, and contrast 1.08 takes another four
points off — B's gains are set against measured output, not straight conversion.
| shot (HUD excluded) | median | p75 | p95 | lit > 18 % |
|---|---|---|---|---|
| wastes before / A / B | 23.1 / 17.3 / 2.7 | 32.9 / 38.4 / 11.4 | 45.5 / 73.3 / 40.8 | 63 / 49 / 17 % |
| barrows before / A / B | 32.5 / 35.7 / 5.5 | 44.7 / 48.2 / 16.5 | 55.7 / 69.8 / 54.1 | 74 / 88 / 23 % |
| crypt before / A / B | 5.9 / 23.9 / 11.0 | 23.1 / 45.1 / 20.0 | 62.4 / 65.9 / 47.1 | 31 / 58 / 29 % |
Images for the user: `Y:\shots\gfx\dark\{wastes,barrows,crypt}_{before,A_asnow,B_darker}.png`, sheet `sheet_ab.jpg`.

**亮度 setting:** `Settings.Brightness` (Save.cs, 0.8-1.3, default 1) → exposure × brightness² in
`Gfx.TuneEnvironment`; one row in SettingsUi's display tab (slider + calibration glyph, hint 調整到幾乎看不見左邊的符號).

## 15 Nov, 17:10 — indoor walls never hide walkable floor (systemic 13)
Seen in the level-A finals: in tall-wall themes (brasshall 6.5 m, cathedral 6.4 m, rift) the hero fought inside a
porthole; the corridor left and right of the 2.6 m hero cut stayed hidden behind the south wall, monsters included.
Fix: `EnvMats.SetWalkMask` builds one texel per nav cell (white = not solid, solid ring around the grid) and hands it
to the wall and lid materials; `floor_cut()` in `env_wall.gdshader` / `env_cap.gdshader` follows the ray from the lens
through each wall fragment down to y = 0 and dissolves the fragment (down to the 0.45 m stub) if it lands on open
floor. Walls backed by rock and the far walls of a room keep their full height; a 2 m partition keeps about 2.3 m.
Indoor themes only (outdoor cliffs, tree lines and roofed houses are untouched). Cost: not measurable (A/B with
`--gfx-nocut`, static brasshall at 4K 高: 20.8 / 21.1 ms with, 14.5 / 21.5 ms without — the spread is other
agents' load). Shots: `Y:\shots\gfx\cut1\` (before: `fin2\brasshall_2_full.png`, `fin2\cathedral_1_full.png`).
Also: water glare calmed (sun specular 0.6 → 0.3, water roughness 0.17 → 0.24, water SPECULAR 0.75 → 0.35) after a
white patch in `fin2\mire_2`.

## 15 Nov, 17:40 — the user chose the level: B indoors, B lifted a little outdoors
`EnvAtmosphere.Dark` is now `true` by default (no switch needed); `--gfx-bright` brings back the old level A for
comparison only, `--gfx-dark` is accepted and does nothing.
- Indoor gains are B exactly as in `dark\crypt_B_darker.png` (ambient ×0.6, fill ×0.55, torches ×1.9 inverse-square
  at 0.8 range, exposure 1.0, contrast 1.0, fog energy 0.035, no ambient in the mist).
- Outdoors only the moon and the ambient were raised: `SunGain` 0.42 → 0.95, `AmbientGainOutdoor` 1.25 → 1.55, then
  each region levelled by its own `AmbientEnergy` / `SunEnergy` (script `Y:\tools\gfx_tmp\night.pl id=factor …`) until
  the frame median sat at 7-10 % sRGB. Fog, vignette, torch falloff, hero fire and exposure stay B.
- Town: lifted like the other regions plus `TorchBoost = 1.9` (new LevelTheme field: energy × boost, reach
  × (1 + 0.4 (boost − 1)) for every fire and lamp of the theme), so its lantern and brazier pools are the best lit
  ground in the game and everything beyond them stays dusk-dark.

**Final levels, measured** (`Y:\shots\gfx\dark\<theme>_final_{walk,fight}.png`, sheet `dark\sheet_final.jpg`; 高 preset,
sRGB luma in %, HUD regions excluded; hero ÷ ground and other actors ÷ ground from the actor masks):
| frame | median | p75 | p95 | lit > 18 % | hero ÷ ground | actors ÷ ground |
|---|---|---|---|---|---|---|
| wastes walk (spawn, two torches) | 14.5 | 22.7 | 39.6 | 37 % | 0.75 | – |
| wastes fight | 11.4 | 18.4 | 36.5 | 26 % | 0.54 (aura decal under the feet counts as ground) | 0.89 |
| wastes, open field (lv5 1-2) | 7.5-7.8 | 13.7-15.3 | 27-37 | 13-20 % | 0.80-1.16 | 0.78-1.68 |
| barrows walk | 11.4 | 23.1 | 37.3 | 34 % | 0.91 | – |
| barrows fight | 7.1 | 12.2 | 21.2 | 9 % | 1.34 | 1.11 |
| crypt walk (entrance room) | 7.5 | 14.1 | 40.4 | 18 % | 0.89 | – |
| crypt fight (corridor) | 1.2 | 7.5 | 31.4 | 11 % | 0.83 | 0.68 |
| crypt, other frames (lv5 2-3) | 2.0-6.7 | 7.5-15.3 | 16-29 | 4-18 % | 0.95-1.14 | 0.89-2.40 |
| town walk (gate, lanterns, portal) | 18.4 | 26.7 | 83.5 | 53 % | 1.05 | 0.30 (keeper outside the pools) |
| town brazier | 7.8 | 14.5 | 44.3 | 17 % | 1.37 | – |
Other regions at the same gains (beauty frames, no HUD, before the last ±10 % trims): frost 12.9-14.1, mire 5.9-7.5,
sands 10.2-12.9, sanctum 11.0-18.4, abyss 4.3-4.7 (half the frame is void), graveyard 10.2-11.0.
Crypt medians are low because the black lid over solid rock fills half of a corridor frame; the floor itself sits
at 8-20 % inside the hero's fire and 2-5 % outside it.
Notes on the ratios: (1) the tool now ignores ground blocks under 1.6 % (lid, void); (2) a fire field or aura decal
under a figure counts as ground and pulls the ratio down; (3) ghosts are emissive by nature — `spectral.gdshader`
energy 1.0 → 0.7 after one measured 4.8 x its ground; (4) in the crypt the corridor cobbles were levelled far darker
than the room flagstones (band 0.055-0.17), so figures on them measured 1.5-2.4 x their ground → indoor floor sets
are now kept within a factor of 1.6 (`EnvMats.Ground`: min = max(GroundLumMin, 0.62 GroundLumMax)); after that the
crypt hero sits at 0.83-1.14.

## 15 Nov, 18:50 — QA findings worked in (effects, portal, video memory)
**Effects at the dark level** (script `gfx_tmp\edit62.pl`): against a scene whose floor sits at 2-10 % every additive
effect reads two stops brighter than it did at level A.
- Hit flash: `char.gdshader` adds `flash_color * min(flash, 1) * (0.07 + 0.5 fresnel⁴)` (was 0.3 + 1.3: a solid white
  figure); CharModel lets it decay in about 75 ms (was 170 ms).
- Slash (`fx_slash.gdshader`, Fx.Slash): a 3 cm bright edge where the tip is, a streaked body at 10-40 % behind it,
  gone a third of the swing behind the blade (lead³), life 0.16 s (spin 0.22 s); steel is warm grey (0.82, 0.72,
  0.58) at gain 0.8, elements keep their hue (8 % white) at gain 1.05. Was a flat near-white wedge at gain 1.1-1.5.
- Bursts / cast glows / impacts: orb gain × 0.6 and less white in the core (`fx_flash`: 45 % → 28 %; `Hot()` white
  share × 0.55), shock rings × 0.75, flash lights × 0.45 with a cap of 3.2 (an ultimate asked for energy 7 at 2-3 m:
  that, not the sprites, whited out the lampknight's lk_dawn close-up).
- Portal: tint pulled 30 % towards pale blue-white, swirl gain 1.4 → 0.6, rim 1.2 → 0.7 with 20 % white, ground pool
  0.7 → 0.42.
- `gfx_luma.ps1` prints `white(>90%)`: the share of the frame that is blown.
**Video memory** (script `gfx_tmp\edit63.pl`): every cache only grew — `EnvAssets.Maps` (texture sets), `EnvProps.Defs`
(prop models), `Skies`, `Surfaces`, and CharModel's `Scenes` / `Converted` (every monster model and its 2K maps).
`LevelBuilder.TrimCaches` runs when a level of another theme is built: texture maps move to a "previous" generation
(town and back costs nothing, a third theme pushes the first out), prop models, skies and surfaces are dropped and
reloaded on demand, `CharModel.TrimCache` forgets models not asked for during the last two levels and converted
materials that no mesh references any more (`GetReferenceCount() <= 1`; the hero's stay).

## 15 Nov, 19:10 — PARKED for the prototype build (state of the tree, open items, next steps)
**In the tree and seen on screen (last game run 18:44, `Y:\shots\gfx\fin4\`, `lv5\`, `lv6\`, `ice\`):**
- dark level as the default (B indoors, lifted moon/ambient outdoors, town with `TorchBoost` 1.9), per-region levels
  including the last trims (town ×1.12, icehall ×1.5, icecave ×1.4 on ambient/fill);
- indoor floor cut (`floor_cut`, walk mask), indoor floor sets levelled within ×1.6, ghost energy 0.7, calmer water;
- all 20 themes re-shot at these levels at 極致 into `Y:\shots\gfx\fin4\` (`<theme>_1|2.png` with HUD, `_full.png`
  without; sheets `sheetA.jpg`, `sheetB.jpg`). `Y:\shots\gfx\final\` is STILL EMPTY: nothing was copied because the
  effect re-grade below changes every fight frame.
**In the tree, compiles, but NOT yet seen on screen (made after the last run; no run was possible because other
agents' files broke the build, then work was parked):**
- effect re-grade (`edit62.pl`): hit flash (char.gdshader line 85, CharModel decay 13/s), slash trail
  (`fx_slash.gdshader` fragment rewritten, Fx.Slash colours/gains/life), orb / ring / flash-light gains in Fx.cs,
  `fx_flash` and `fx_swirl` constants, portal tint and gains. All are reductions except the slash, whose shape is
  new: if a slash looks too thin, widen `0.03` (edge) and raise `0.1 + 0.3 * streak` (body) in fx_slash.gdshader.
- `spectral.gdshader` has `albedo_tex` / `detail` (ghosts shaped by the model's own albedo); CharModel passes the
  texture but leaves `detail` at 0, so ghosts look exactly as in the last run.
- cache trimming for video memory (`edit63.pl`: `LevelBuilder.TrimCaches`, `EnvAssets.Trim`, `EnvProps.Trim`,
  `EnvAtmosphere.TrimSkies`, `CharModel.TrimCache`) is complete but SWITCHED OFF: `LevelBuilder.TrimEnabled = false`.
  `--gfx-trim` turns it on for a test.
**QA findings:** 1 effects — implemented, unverified; the sweep of all 8 classes at level B is not done.
2 portal — implemented, unverified. 3 video memory — implemented, off, unverified. 4 speed — not re-measured
(at 18:52 `who_runs.ps1` showed music generation on the CPU, voice generation, two Blender jobs with 12 threads each
and two other game runs; every figure taken today after 17:00 carries that load: 4K 高 with governor 36-56 fps at
the 1080-line floor, against 61-99 fps in the quiet window before). 5 town build — measured only: a warm build is
0.13 s; the 4 s is the first load of prop models (`props` step 0.4-2.5 s) and textures, not geometry.
PropSlot.Tag/Half: done earlier (`EnvProps.PlaceTagged`). Ghost textures: see above.
**Next steps when resumed:** (1) one run to confirm the shaders load and look right: `--gfx-fx` gallery in the crypt,
then a bot fight per class (emberblade, lampknight, astromancer, forgebody, hunter, pactbinder, soulbinder,
wildsoul) with `gfx_luma.ps1` → `white(>90%)` under about 1 %, and the town portal; (2) `--gfx-trim` tour
town → 7 regions → town, read `vram=` in the log, then default `TrimEnabled` to true; (3) quiet-machine 4K 高
measurement of all 20 themes with the governor (check `who_runs.ps1` first); (4) re-shoot fight frames, copy one or
two per theme from `fin4\` / the re-shoot into `Y:\shots\gfx\final\`; (5) final report.

## 20 Nov — graphics pass for 0.3 (list of ten; shots in `Y:\shots\gfx\v03\`)
Tools: `EnvProbe` got per-stop placement and staged moments (header lists them): `label=area@behind:tree:2`
(hero just north of the n-th nearest prop slot of a category; consecutive stops in one area do not reload it),
`--gfx-evade`, `--gfx-run` / `--gfx-runspeed`, `--gfx-life`, `--gfx-hug`, `--gfx-fov`, `--gfx-burst`.
`Y:\tools\gfx_tmp\v03\`: `q.sh` (one queued run; waits while the user plays), `sheet.ps1` (contact sheet / crop),
`edit70..75.pl` (the patches as applied), staged shader copies. Test slot 98; saves moved to `saves_test_backup`.
Baseline tour (same seed 4242, emberblade lv30, 23 stops): `v03\before\`, after: `v03\after\`.
Run queue: 5-8 other `run.ps1` jobs were waiting all evening; one run of mine came back in 10-20 minutes.

**State of the tree (all compile; see "verified" per item below, updated as runs come back):**
1. *Hero hidden by scenery* — `env_prop.gdshader` / `env_wall.gdshader`: `see_through()` now takes the figure's feet and
   has two parts: the wide disc round the line lens → chest (above stump height, as before but with narrow dither
   bands: 0.74 R..R, 0.45 m in height, 0.45-1.1 m in front) and the figure's own outline — the ray lens → fragment is
   followed to the upright plane the figure stands in, and whatever lands within 0.5 m (soft to 0.82 m) of the body
   segment feet..head is cut down to the ground, however close in front it stands. Props only do the outline part
   when they are tall (`prop_height` × instance scale ≥ 1.35..2.1 m), so crates and graves are never holed.
   The same test runs a second time for the enemy under the cursor / being attacked: new global uniform
   `focus_pos` (vec4, `project.godot [shader_globals]`; xyz + 0..1 weight) set by `CameraRig.UpdateFocusCut`
   from `Player.AttackTarget ?? Player.Hover`, radius × 0.7.
2. *Black shapes* — props: `lum_floor` (soft-knee lift of near-black albedo to 0.75 × `WallLumMin`), `sky_fill`
   (0.6 × ambient again on up-facing rock, outdoors) and `sky_rim` (fresnel³ sheen in the moon colour, 0.022 linear
   × moon strength) for natural non-plant props and for triplanar rock walls (`EnvMats.SkyFill/SkyRim`).
   Lid over solid rock indoors (`env_cap.gdshader`, `EnvMats.Lid`, `EnvWalls.BuildLids`): unshaded wall-top texture
   at the level of an unlit wall top (ambient + fill × `LidShade` 0.3 × `LidGain` 1.6), blotched by world noise,
   sinking to 22 % over ~9 m of depth into the mass (depth map = `WallD`, one texel per metre); the textured tops
   now fade to `LidShade` instead of black so there is no trench where the lid begins. Outdoor lids stay black.
3. *Boss inside the hero* — radii already scaled (`Radius = Def.Radius × scale`); nothing enforced them against the
   hero while a boss casts. `Monster.BodyBlock` (marked `[gfx 0.3, item 3]`, roster engineer told): bodies with
   radius ≥ 0.85 m carry the hero out to 0.9 × radius + hero radius at ≤ 9 m/s.
4. *Low-life / heavy-hit red* — `HudShaders.Vignette` + `Hud.cs`: rounded-box falloff that leaves the middle
   56 % × 50 % of the frame untouched, alpha capped at 0.55, strength low × (0.36 ± 0.12) + hit × 0.6.
6. *Cathedral floor* — `cathedral.RoomFloor` `marble_checker` → `worn_marble` (floors compared in `gfx_tmp\v03\floors.jpg`).
7. *Street lamp* — the "tilt" is the 32° lens (posts near the lower corners lean outward); the white pixels were the
   blended glass panes burnt out by the lamp's own omni light a hand away (and dithered by the near-lens fade).
   `EnvProps.Convert`: blended surfaces of light props become an unshaded warm film (torch colour, alpha 0.26);
   dissolving prop fragments now lose albedo, specular and emission (`solid`).
8. *Palm trunks* — same shader work as 1: narrow dither bands, and partly cut fragments sink to 30 % albedo without
   highlights instead of sparkling through FSR's sharpening.
9. *Paragon stray frame* — it was the board-sized ghost of an empty slot beyond a lit gate, cut off by the top bar.
   The ghost is now drawn only under the pointer; at rest the slot is a stub from the gate and a small plaque
   with the prompt (`ParagonUi.DrawAll`).

**Later the same evening (22:30-23:00) — changes after the first after-tour, and how the checks are run now**
- Props are opened only when the instance is ≥ 2 m tall (binary, no half-dithered boulders); trees keep their trunk:
  `disc_from` (0.3 dead trees / pines, 0.55 quiver trees, 0.6 bare trunks) × height is where the wide disc starts, the
  trunk below it is cut only where it covers a figure. `SkyFillShare` 0.6 → 0.42 (abyss rock read pale blue),
  `LidGain` 1.6 → 2.4 (the lid was there but at 1-2 % sRGB; ×5 boosted proof: `gfx_tmp\v03\dungB_boost.png`).
- Street lamp, second cause: the cage of `lamp_post` / `lantern_wood` (opaque material) is 0.2-0.3 m from the prop's
  own omni light, which falls off with the square of the distance: burnt to white pixels. `flame_guard` (1.6 m) in
  env_prop scales albedo and specular by (distance to that light / guard)², cancelling the falloff inside the guard.
- EnvProbe stop options (`label=area@opt+opt`): `class:<key>[:level[:paragon]]`, `bot`, `at:`, `fx`, `hud:`, `fov:`,
  `life:`, `hug:`, `evade:`, `run:`, `clips:`, `ui:<panel>`, `poi:` — a whole verification (tour, boss, effect gallery,
  8-class bot sweep, evade analysis, clip viewer, paragon panel) is ONE process: `gfx_tmp\v03\master.sh <out> <log>`.
  Reason: the run lock was held by a 25-minute headless suite and 6-8 jobs waited; two of my runs gave up after
  `run.ps1`'s 25-minute limit. `perf.sh <out> <log>` = 4K 高 with governor over all 20 themes, logs `who_runs` before/after.
- Background jobs of mine while this is written: `master.sh` and `perf.sh` (bash, each waits in `run.ps1` for the lock,
  retries up to 5 times). Stop: kill the `powershell … run.ps1 … --slot=98` processes (only those). Resume: run the
  two scripts again; outputs `gfx_tmp\v03\master1.txt`, `perf1.txt`, shots `shots\gfx\v03\after\` and `perf\`.
- 23:30 — the master run lost its place twice: `run.ps1` gives up after 25 minutes at the lock and a retry joins the
  back of the queue (9-10 jobs waiting, headless suites of 10-25 minutes among them). `gfx_tmp\v03\run_patient.ps1` is
  a copy of `run.ps1` that differs only in waiting 120 minutes for the SAME machine-wide lock (`master_p.sh`,
  `perf_p.sh` use it; logs `master2.txt`, `perf2.txt`). Whichever waiter of mine starts first runs; the duplicate
  waiters (`powershell … run*.ps1 … --slot=98`) are killed by hand as soon as one has run.
- Evade (item 5): `anim_lib.glb` parsed with `gfx_tmp\v03\glbanim.pl`: clip `Roll` is 1.467 s; the pelvis is turned
  59° at 0.30 s, 175° (upside down) at 0.53 s, back to 40° at 0.80 s, 20° from 0.90 s. `Player.TryEvade` played it at
  rate 1.9 over a 0.32 s dash: the dash ended at clip time 0.61 s (160°, mid-tumble) and the 0.12-0.2 s blend to
  idle / run swung the upside-down body back like a plank. Rate now 2.5 (clip time 0.80 s at the end of the dash).
  Marked `[gfx 0.3, item 5]` in Player.cs (gameplay engineer's file; one constant and a comment).
- Lid: `LidShade` is now the one level of wall tops and lid (0.72, tops no longer fade to black; lid = same texture,
  unlit, at ambient + fill × 0.72, sinking to 20 % over ~7 m of depth); `FlameGuard` 3.0 m.

**00:30 (21 Nov) — first full master run back (23:40, 174 shots, 0 errors): what it showed, what changed after it**
- 1 / 8 verified: mire trunks, wastes boulders, abyss rock, sands palms — hero visible in every stop; trunks beside
  the hero are solid columns again (no grain), a trunk in front opens only round the figure. `v03\pairs\01_*.jpg`,
  `08_sands_palms.jpg`, `review\sands_side.jpg`.
- 2 rock form verified (`pairs\02_abyss_rock.jpg`). Lid: measured 0.8-1.5 % sRGB with ambient + fill alone (floor in
  the hero's fire: 14 %) → `LidGain` 2.6 as an unlit lift shared by the indoor wall tops (new `WallFlags.Top`,
  env_wall `self_lit`) and the lid (env_cap `lid_gain`), full within 8 m of the hero, a third of it beyond 22 m.
  NOT yet seen at this gain (run `master3` queued).
- 3 not verified: the probe's hero was put into a boss that sleeps behind its sealed door (`Level.SetBoss` switches
  the boss's processing off until the fight is engaged) — nothing ran. The probe now provokes it first (`hug`).
- 4 verified in `after\bossA_1.png`, `abyssR0_1.png`: red only in the outer band.
- 5 clip viewer (`review\clips_zoom.png`): Roll at 1.9 on the astromancer = plank at 45° (0.08 s), horizontal bell
  (0.2 s), upside down (0.32 s = end of dash). Robed heroes (astromancer, soulbinder) now use `Shield_Dash` at 1.6
  (`Player.EvadeAnimOf`, also used by TestSuite.Anims' evade check); everyone else rolls at 2.5. The staged evade of
  the first run hit a stun (monsters round the hero) and never rolled: re-staged in town in `master3`.
- 6 verified (`pairs\06_cathedral_floor.jpg`). 7: cage no longer white (`review\lamp_after_crop.png`), `FlameGuard`
  3.0 → 2.2 (it had gone almost black). 9 verified (`after\paragon_1.png`); prompt alpha raised afterwards.
- 10: gallery `review\fx_gallery.jpg` (crypt + town: zones, telegraphs, beams, projectiles, portal all load and
  read); 8-class bot sweep in crypt_d1, 10 frames each (`review\fx8_<class>.jpg`, `fx8_luma.txt`): white(>90 %)
  mean 0.08-0.42 % per class, worst frame 1.41 % (pactbinder's 6 m fire field) → fire-zone cracks 1.9 → 1.45 and
  white-hot share 0.9 → 0.55 in fx_zone. Slash: thin warm arc, readable (`after\fx8_emberblade_5.png`).
  Frame rate, 4K 高 + governor, all 20 themes, 23:47-23:50, lock held (no other game run), voice generation
  (voxcpm) and a Godot import running: 52-111 fps (`v03\perf\perf.txt`); the governor left the preset's 0.67
  in 16 of 20 themes (mire 0.58, sands 0.62, abyss 0.62, sanctum 0.66). 15 Nov under load: 36-56 fps at 0.50.
- 11 (new, from the lead): portals inside a doorway block. Cause: `WorldObjects.Exit/Entrance` built an arch (two
  posts and a 3.7 m lintel, object-space triplanar smeared along it: "stacked timber") and hung `Fx.Portal` 1.35-
  1.45 m up, although Fx.Portal already lifts its swirl by its own height: the swirl sat half inside the lintel and
  its ground pool floated at 1.4 m. Patch staged in `gfx_tmp\v03\edit90.pl` (applied only after the "before" run
  `master3` has built): open ring of standing stones (`PortalRing`), swirl on the ground, `PortalSpot` moves the
  portal up to 3 m to stand clear of walls / blocking props, `Fx.Portal(c, r, onGround)`, `Surface(set, uv, world)`.

## 21 Nov, 01:00 — PAUSED for the 0.3.0 release: state per item (whole project compiles, `check.ps1` 0 errors)
No background job of mine is left (queued runs stopped, `slot98*` moved to `saves_test_backup`).
| # | item | state in the tree | seen on screen |
|---|---|---|---|
| 1 | hero hidden by trees / boulders / rock | IN (env_prop, env_wall: figure outline cut + wide disc; props under 2 m never cut; trees keep their trunks) | yes — `v03\pairs\01_*.jpg`, 23:40 run |
| 1b | cut round the hero's target (`focus_pos`, CameraRig) | IN | only with a held test point (`ent_before\focA_1`, `focB_1`: renders, no artefacts); NOT with a real hovered monster |
| 2 | black rock / void beyond dungeon walls | IN (lum_floor, sky fill 0.42, sky rim; lid + indoor wall tops textured) | rock yes (`pairs\02_abyss_rock.jpg`); lid seen at gain 1.0 (≈1 % sRGB, too dark) and 2.6 (≈7 %, `ent_before\lidA_1.png`, a little strong) — **shipped value 1.9 lies between the two and was not itself rendered** |
| 3 | boss inside the hero | IN (`Monster.BodyBlock`, roster engineer agreed) | yes — 00:37 run: hero put on 賽倫 is at 1.58 m within 0.25 s (touch distance 1.71 m), `ent_before\bossB_2.png` |
| 4 | red screen at low life / heavy hit | IN (HudShaders.Vignette, Hud.cs) | yes — `after\bossA_1.png`, `abyssR0_1.png` |
| 5 | astromancer "plank" | IN: cause = evade. Roll rate 1.9 → 2.5 for all; astromancer + soulbinder use `Shield_Dash` 1.6 (`Player.EvadeAnimOf`; TestSuite.Anims follows it) | pose log yes (Roll back to 18° at the end of the dash; Shield_Dash 16-30° throughout); frames `ent_before\rollA_*`, `rollE_*`, `rollS_*` shot but only the clip viewer sheet was looked at |
| 6 | cathedral checkerboard | IN (`worn_marble`) | yes — `pairs\06_cathedral_floor.jpg` |
| 7 | street lamp | IN (flame guard, warm glass film) | yes at guard 3.0 (`review\lamp_after_crop.png`); shipped 2.2 (1.9× brighter cage) not re-shot |
| 8 | grainy palm trunks | IN (same shader as 1) | yes — `pairs\08_sands_palms.jpg`, `review\sands_side.jpg` |
| 9 | paragon stray frame | IN (plaque at the gate; ghost only under the pointer) | yes — `after\paragon_1.png` |
| 10 | effects at level B, frame rate | fire-zone cracks toned down; everything else left as re-graded | gallery + 8-class sweep yes (white>90 %: class means 0.08-0.42 %, worst frame 1.41 % before / 1.07 % after the fire change); 4K 高 52-111 fps (`v03\perf\perf.txt`) with voice generation + an import running |
| 11 | portal inside the gate | **SWITCHED OFF** — `WorldObjects.BuiltGateways = false`: exits, entrances, conjured portals and the rift stone are built exactly as in 0.2.0 (arch + round swirl). | new gate: NEVER rendered |

**Item 11, where it stands.** Written and compiling, behind the switch: `Gateways.cs` (seven themed gates from the
level's own graded materials: masonry+keystone, timber with through beam / braces / iron bands, battered sandstone
pylons, rimed stone with icicles, gabled grave gate, gothic with pointed gable, basalt columns under obsidian; ember
inlays), `Fx.Veil` + `shaders/fx_veil.gdshader` (rectangular veil filling the opening, ground spill, rising motes;
loaded only on first use), `WorldObjects.Gateway / PortalSpot / FreeAround`, `WallFlags.Stand` (no indoor floor cut
for landmarks). Before-shots of all 7 regions, town, 2 dungeons, 2 dungeon entrances: `v03\ent_before\ent*.png`
(sheet `review\ent_before_a.jpg`). To resume: run `gfx_tmp\v03\tour.sh Y:/shots/gfx/v03/ent_after <log> "<the
ent* stops of master3>" --gfx-gates`, look at every gate (shader compiles? veil fills the opening? jamb obstacles do
not trap the arrival spot? indoor gates whole?), then set `BuiltGateways = true`. Conjured portals (`Portal()`) stay
round by design but should then stand on the ground (the flag also does that) — say so to the lead.
**Also open:** frame-rate figure in a truly quiet window (never had one: 6-10 jobs queued all night); `final\` is
still empty; the README gallery was shot before these changes.
**Files of others touched (small, marked `[gfx 0.3 …]`):** Monster.cs (BodyBlock), Player.cs (evade clip/rate),
TestSuite.Anims.cs (evade check), Hud.cs + HudGlobe.cs (vignette), WorldObjects.cs (gateway code behind the switch),
ParagonUi.cs (item 9), project.godot (`focus_pos` global), ARCHITECTURE.md (one sentence).

## 21 Nov — ground sigils: the arcs and circles that show a skill's range (user request)
"the arcs and circles that show the projected range of a skill should be a lot more decorated with arts and/or lines
that hint at the nature and/or the directions of the skill/spell". Shots: `Y:\shots\telegraphs\`. Work folder:
`Y:\tools\gfx_tmp\tele\` (shader work copies, CPU previewer, edit scripts, run scripts).

**What was there:** `Fx.Telegraph / TelegraphCone / TelegraphLine / TelegraphRing` drew a flat fill and one outline
(fx_telegraph), `Fx.Ring` and the ring of `Fx.Burst` a plain band (fx_ring), `Fx.Slash` a thin arc (fx_slash). The hero
has no aim or range preview of his own: his skills show their reach through exactly these three (friendly circles for
delayed / leap / trap skills, the burst ring of novas, the slash arc of melee skills), so all three were dressed.

**The school of a mark:** new `Sigil` value type (Fx.cs): Fire, Frost, Lightning, Poison, Shadow, Blade, Holy,
Arcane (+ Auto = read from the colour, None = plain ring). An `Element` converts by itself, so call sites pass
`d.El` / `Def.Element` / `el`. The old signatures still work: the danger red gives the generic Arcane mark, element
colours give their element, pale gold Holy, greys Blade (`Fx.SigilOf`).

**fx_telegraph (rewritten, all procedural, sized in metres):** four layers.
- *frame*: heavy rule + hairline + inner rule; the band between carries runes (16 runes of our own, drawn into a
  1024 x 64 L8 texture at start-up by `Fx.MakeGlyphs`) turning round a circle / running along a line; iron studs for
  Blade; plain graduations when the band is too narrow for runes; marks under ~0.5 m stay plain.
- *nature*: "arms" in arm space (across, along), radial on circles / cones / rings, along the travel on lines:
  fire = swaying flame tongues + embers (on a circle they stand on the rim and lick inward); frost = dendrites with
  60 degree branches inside a faceted frame; lightning = forked zigzags that strike anew 9 times a second;
  poison = hanging drops + bubbles; shadow = seven-pointed star, thorn crown, brambles; blade = three crescents across
  a cone / cracks round an impact / a gash along a charge; holy = sun disc and rays; arcane = astrolabe.
- *flow*: chevrons streaming away from the caster between the arms (cone), along the travel with a double head at the
  far end (line), darts on the inner rule pointing at the centre (circle), pale gold dotted ring + arrows pointing
  into the safe hole (ring; hostile only).
- *time*: the old fill (unchanged semantics: arrives when the blow lands) now lights the ornaments as it passes; the
  rim of a circle is run round by a bright head; flash in the last 14 %.
Hostile = red fill, hard red rim, ornaments in a warm accent per school (`Fx.HostileAccent`); friendly = the same
drawing in the skill's own colour, thin and dim, almost no fill. 12 instance uniforms (limit 16).
**fx_ring:** `kind >= 0` dresses the ring (tongues / ice spikes / jagged second ring / thrown drops / thorns / broken
ring with debris lines / rays / graduated double ring), always pointing the way the ring travels; the quad is scaled
up (`over`) so the ornament has room. `Fx.Burst` passes its element, `Fx.Ring` its `Sigil` (loot flourishes: None).
**fx_slash:** echo arcs behind the edge, chevrons on the trail pointing along the swing, and per element tongues /
shards / jagged arc / drops / thorns / sparks on the edge.

**Call sites given a school:** SkillExec (all 6; lamp knight fire skills = Holy via `SkillExec.Mark`), BossBrain
(all 20: `d.El`, phase ring `_kit.El`, add spawn marks Arcane), EliteFx (10: molten Fire, crystals Frost, shock
Lightning, plague Poison, orb / tether / raise dead Shadow, wall Blade), Monster / MonsterAi* (25: `Def.Element`,
hook / grab / shield dash Blade), NamedEncounter helpers (7: `el`), NamedRares / NamedDungeons (3), Level (channel
circle Arcane), Loot (3 x None). Everything else keeps the automatic mark.

**Tools:** `EnvProbe` gallery `--gfx-fx=telec|telecf|telek|telekf|telel|teler|telering|telemix|telestress` (stop option
`fx:telec` ...); `Fx.TelegraphCone(Vector2 pos, Vector2 dir, ...)` (cone without an actor). CPU previewer
`gfx_tmp\tele\preview.py` / `preview_scene.py` (+ `glsl_np.py`: runs the fragment() of a restricted .gdshader with
numpy, so the pattern can be drawn while the game may not be started) and `to_glsl.py` + the Khronos reference
compiler (`%LOCALAPPDATA%\AshenAbyssTools\glslang_dl`, downloaded from the official GitHub release) for a type check.

**Second pass (lead's notes, 09:50: the red must not drown the drawing; overlapping marks must not merge into a red
slab; the two sides must be told apart at a glance):** the mark is now composited in layers inside the shader
(premultiplied `acc` / `cov`): a thin veil (4 % + 7 % gathering toward the rim, + 11 % where the fill has passed;
it was 12 % + 24 % flat) → the rune band of a HOSTILE mark lies on a dark plate (45 % near-black: legible on top of
other marks, and it keeps neighbours apart) → ornaments → fill front → rim. The hostile rim is thinner (12 cm, was
22 cm), less over-bright (1.3-1.85 x, was 2.1 x) and drawn as a twisted cable (bright and dim strands). Shape language
per side: hostile = cable rim, dark plate, teeth on the inner rule, pulse, red veil; friendly = one plain thin line, no
plate, round beads instead of teeth, no pulse, no veil. Early `discard` outside the shape (a cone covers under a
third of its quad).

**State, 21 Nov 14:05 — DONE and seen in the game (the user closed the game 13:08-13:20 and 13:53-14:03):**
- The three shaders are in `game\shaders`, compile and load in Godot; `check.ps1` 0 errors; every one of the ten runs
  ERR=0 WRN=0 (last: `game_20261121_140211.log`, with the final shader).
- Real fights, HUD on: `shots\telegraphs\bossrush\br_1..18.png` (`--bossrush` in frost_d1; `br_17.png` = the frost
  boss cone with dendrites, `br_11.png` = a molten elite fire circle in a pack), `boss2\boss_f_*`, `boss_a_*`,
  `boss_m_*` (frost / abyss / mire boss lairs: snowflake circles, bramble cones and crossed lines, poison circles and
  lines), `boss\boss_s_*`, `boss_w_*` (sanctum / wastes bosses, first shader version), `classes\cls_*` (8-class bot
  sweep: slash arcs, burst rings, friendly circles — `cls_astromancer_3.png`), `demo\demo_1..12.png` (demo mode;
  `demo_12.png` = hostile cone over a friendly circle), `final2\boss_s_*` (final shader).
- Orderly galleries with the final tuning: `shots\telegraphs\final\` (wastes boss lair: `circ_1..3`, `circ_friendly`,
  `cone`, `cone_friendly`, `line`, `ring`, `shock`; `town_circ`, `town_friendly`, `snow_circ`), `final2\ring_*`.
  Earlier set `gal1\` (first version, also three deliberately crowded `*_mix` frames).
- Tuning after the first real shots: veil lowered again (4 % + 7 % toward the rim + 11 % where the fill has passed);
  lit ornaments 1.18 x instead of 1.5 x (the bloom made them fat); the last 14 % no longer flash the whole disc (veil
  +14 %, was +30 %) — the drawing and the rim flare instead; safe-hole arrows moved clear of the dotted ring and stay
  visible for most of their beat.
- Frame time: 60 fps (vsync cap) in every fight and gallery at 1600x900, gpu 2.7-7 ms (`*\perf.txt`). Fill-rate
  stress `telestress` (48 screen-filling marks about six deep, 3840x2160 at scale 1.0): crypt_d1 gpu 16.70 ms old ->
  19.13 ms new, wastes boss lair 23.05 -> 26.53 ms (`perf_old\`, `perf_new\`; measured with the
  second-pass shader, the later changes were constants only). The user saw that stress scene on screen and asked whether
  the game looks like that: it does not; it exists only behind `--gfx-fx=telestress`. Do not run it (or `telemix`)
  while the user is at the machine.
- One engine error line in one gallery run, not from this work as far as its stack shows: `Handle is not initialized`
  in `PackedScene.Instantiate` <- `EnvProps.Extract` <- `LevelBuilder.Build` (once in 19 area loads; did not recur).
- Not looked at one by one: every elite affix and every named encounter (they go through the same four functions);
  friendly cones and lines (the API draws them, no skill uses them yet); a 4K fight at the default 0.67 scale.
No background job of mine is left; `slot98*` is back in `saves_test_backup`.
