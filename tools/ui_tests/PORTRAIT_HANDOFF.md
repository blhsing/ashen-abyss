# Inventory hero portrait — handoff

Task: replace the flat vector doll in the inventory panel with a live, real-time 3D rendering of the actual hero.
State: **DONE** (2026-11-16, second engineer). Compiles (`check.ps1 -Files InventoryUi.cs,HeroPortrait3D.cs,Game.cs,Player.cs`:
0 errors); every run ended with 0 errors / 0 warnings in the game log. Known problem 1 (first-open stall from MSAA) is
fixed — see "Second pass" right below. Nothing was committed or pushed. `MainMenu.cs`, `MainMenu.Lair.cs`, `CharModel.cs`
are untouched.

## Second pass (MSAA fix, cleanups, re-test)
- **No MSAA any more.** `BuildStage`/`ApplyQuality`: `Msaa3D = Disabled`, `UseTaa = false`, `PositionalShadowAtlas16Bits`
  copied from the root viewport (16-bit below 極致, like `Gfx.Apply`). Anti-aliasing = supersampling in `FitPixels`: up to
  2×2 rendered pixels per screen pixel within a budget of 0.9 MP (0.45 MP at 低); the factor snaps to exactly 2 when it
  nearly fits (the linear-filtered container then averages exact 2×2 blocks). Below 1.5× per axis (very large screens)
  FXAA is added. At 1600×900 標準: 610×490 rendered for a 305×245 picture (2.00×), at 低 too; `--portrait-scale=2.4`
  (4K-like): 1058×850 (1.45×) + FXAA.
- TAA mirroring turned out unnecessary: with the main view on TAA (高) or FXAA (低) the no-TAA portrait creates no
  pipeline on the spot (measured, below), so it was left out.
- **Measuring stalls independent of caches**: `--portrait-stats` lines now end with the renderer's pipeline-compilation
  counters since the last line / since the inventory became visible (`RenderingServer.RenderingInfo.PipelineCompilations*`):
  `draw` = compiled synchronously because a draw needed it (= the stall), `specialization/surface/mesh` = compiled ahead
  on worker threads.
  - Before (MSAA 4x / 2x): **draw +6** at every first open (高 and 低, Vulkan and D3D12).
  - After: **draw +0** in every run (高/低 × Vulkan/D3D12 with an empty Godot cache, warm runs, all 8 classes, the
    5-class batch, 特大, 4K-like + FXAA). Six `specialization` compiles still happen on the first open; they run on
    worker threads (no stall; first-open frame below).
- **Cold-cache first open** (`portrait_cold.ps1`: the game runs with APPDATA/LOCALAPPDATA pointed at a fresh empty
  folder, so Godot's `shader_cache` and `vulkan/` pipeline cache start empty; the user's caches are not touched; the
  user's settings.json is copied in). Longest frame in the first 2 s after opening, script `portrait_perf.txt` (no
  screenshots), display at 32 Hz (31 ms = one frame):

  | | Vulkan 高 | Vulkan 低 | D3D12 高 | D3D12 低 |
  |---|---|---|---|---|
  | after, portrait on | 45.1 ms | 35.2 ms | 38.8 ms | 41.5 ms |
  | after, `--portrait-off` | 34.6 ms | 35.0 ms | 52.2 ms | 36.9 ms |
  | before (MSAA), same method, `portrait_look.txt` (frame incl. screenshots) | 75.6 ms | 78.1 ms | 65.8 ms | 66.4 ms |
  | before (MSAA), first pass, cold driver cache | — | 1444 ms | 865 ms | — |

  Logs of these runs: `%TEMP%\ashen_portrait_cold\cold_<label>_<driver>_q<n>\Roaming\AshenAbyss\logs\`.
  Limit: the AMD driver keeps its own compiled-pipeline cache outside the user folder (it did not follow LOCALAPPDATA),
  and it is already warm for the old MSAA variants, so the 865/1444 ms stalls cannot be re-created here; the counter
  above (draw +6 → +0) is the cache-independent evidence that nothing is compiled on the spot any more.
- **Cost** (uncapped, `--gfx-novsync`, 高, 1600×900, emberblade): closed 201–214 fps; open 179–187 fps with the
  portrait, 185–193 with `--portrait-off`. Renderer timers for the portrait viewport: **0.22–0.25 ms CPU + 0.28–0.29 ms
  GPU** at 610×490 (2×); 4K-like 1058×850 + FXAA: 0.25 ms CPU + 0.58–0.60 ms GPU (172–177 fps). (MSAA 4x at 305×245
  was 0.21–0.33 CPU + 0.21–0.45 GPU.) At 32 Hz vsync the D3D12/Vulkan cold runs show 0.24–0.43 ms CPU, 0.52–0.80 ms GPU.
- **Edges**: `Y:\shots\portrait\cmp\edges_emberblade.png`, `edges_lampknight.png` (left MSAA 4x, right 2× supersampled,
  nearest-neighbour ×4): edges at least as clean, and texture detail inside the figure is crisper (supersampling also
  smooths shading/texture aliasing). `cmp\q0_before_after.png`: 低 before / after / dragged — fine (no key shadow at 低,
  by design).
- **Cleanups**: `Game.KnownSwitches` lists `portrait-stats`, `portrait-off`, `portrait-scale`, `portrait-model`.
  `Player.IdleAnimOf(ClassId, form)` and `Player.WeaponTierOf(Item)` are public statics (the private `IdleAnim` /
  `UpdateWeaponTier` now call them, same rules); the portrait uses them, its duplicate copies are gone.
- **Tests re-run** (pictures looked at): all 8 classes `Y:\shots\portrait\all_ss\` (`sheet_ss.png`, same look as
  `all\sheet_final.png`); 5-class batch `Y:\shots\portrait\<class>\`; 特大 `Y:\shots\portrait\x2\`; 低 `q0_ss\`;
  frame rate `perf\` logs; `final\` re-shot with the final settings.
- **Save slot note**: the `newgame` command of the UI-test layer (`Ui.cs`) always starts the next hero in
  `Game.HarnessSlot` = slot 0 (not the `--slot` value), so `portrait_all.txt` / `portrait_x2.txt` write `slot0.json`
  (the harness slot, never listed by the menu; the first pass's runs already did the same). The `slot9*` moves after
  runs do not cover it. Left as is (Ui.cs not in scope); slots 1–8 untouched.

## Files changed
- `Y:\game\src\HeroPortrait3D.cs` — NEW (about 1000 lines): the reusable control.
- `Y:\game\src\InventoryUi.cs` — the doll: vector silhouette code and its polygon tables removed (nothing else used
  them; `VendorUi.cs` has no doll), niche + portrait + rim + cell layer, layout constants, `LayoutDoll`, `OnTextScale`.
- Test helpers (new): `Y:\tools\ui_tests\portrait*.txt`, `portrait_batch.sh`, `portrait_sheet.ps1`, `glb_nodes.ps1`,
  `portrait_cold.ps1` (second pass).
- Second pass: `Y:\game\src\Game.cs` (one line in `KnownSwitches`), `Y:\game\src\Player.cs` (`IdleAnimOf`,
  `WeaponTierOf` made public statics; behaviour unchanged).
- Pictures: `Y:\shots\portrait\…`.

## What is built (HeroPortrait3D)
- **Structure**: `HeroPortrait3D : Control` → `SubViewportContainer` (stretch) → `SubViewport` with its OWN `World3D`
  (nothing of the level shows, the level never sees it). Opaque dark backdrop (not transparent).
- **Sharpness**: `FitPixels()` sizes the viewport to the control's size in real screen pixels
  (`GetViewportTransform() * GetGlobalTransform()`), by giving the container that pixel size and the inverse scale.
  At a 1600×900 window the picture is 305×245 px at 標準, 305×225 at 大, 305×205 at 特大. Anti-aliasing: 2×2
  supersampling within a pixel budget (see "Second pass"; was MSAA 4x/2x).
- **Model**: `CharModel.Create(id)` — same file, character shader and shared animation library as the world. When the
  hero is the one being played it mirrors `Player.ActiveModel` (model id incl. wildsoul forms, scale, and the visibility
  of every `wpn_*` mesh, so main hand and off-hand match the world exactly); otherwise `hero_<classKey>` and the tier
  rule. Idle per class as in `Player.IdleAnim` (`Sword_Idle`, `Idle_Shield_Loop`, `Pistol_Idle_Loop`, `Idle_Loop`;
  `Zombie_Idle_Loop` for a form). `SetRim(classColor × 0.35)` as in the world.
  The idle and the weapon-tier rule come from `Player.IdleAnimOf` / `Player.WeaponTierOf` (second pass; the
  control's former duplicates were removed).
- **Shader independence**: `char.gdshader` uses no global shader parameter (`hero_pos` is only in the env shaders), but
  its `grade_tint` / `grade_desat` / `scene_rim` uniforms are rewritten per level on the SHARED materials
  (`CharModel.SetSceneGrade`). `OwnMaterials()` gives the portrait's meshes shallow copies with a fixed neutral grade
  (0.74, desat 0.10, no scene rim); textures and shader stay shared; the level is not touched.
- **Framing**: `BuildProxy()` reads each visible mesh back once per session (`Mesh.SurfaceGetArrays`, cached in the
  static `Proxies` by file/mesh name) and keeps per bone the outermost vertices in bone space. `Measure()` seeks the
  idle to 8 times, poses those points with the skeleton and keeps 44 hull points per pose. `Solve()` fits the lens
  (FOV 24°, pitched 7° down) so every point is inside with head-room 5.5 %, foot-room 9.5 %, side-room 5 %; the body
  axis stays centred unless a weapon forces the frame to slide. Solved twice: resting view and "turned any way"; while
  the player drags, the lens eases back to the second so a long weapon never leaves the picture.
  Verified with a model scaled 1.3 (`--portrait-model=form_bear@1.3`).
- **Lights** (laid out in units of the figure's height): warm key `SpotLight3D` from the upper front-left (the only
  shadow caster, 1024 atlas, off at 低), cool rim `DirectionalLight3D` from behind right, weaker cool kicker from behind
  left, faint breathing ember `OmniLight3D` from below front. Rim/kicker/ember are masked off the floor by layers.
  Own `Environment`: flat ambient, ACES / white 7 like the levels, exposure follows the 亮度 setting, glow, no fog,
  a dim procedural dome used only for reflections. Lit floor disc (receives the real shadow) + contact-shadow blob that
  turns with the feet, backdrop quad with a faint haze (hint of the class colour), 10 ember motes (`CpuParticles3D`).
- **Alive**: `ProcessMode = Always`; idle plays continuously; slow sway ±0.075 rad around a 0.30 rad three-quarter view.
- **Drag**: left button on the portrait turns the figure (0.0125 rad per design px); 2.4 s after release it eases
  back. `MouseFilter = Pass`, only the left press/drag is accepted. The portrait's rectangle does not overlap any cell.
- **Sync**: follows `Hero.Changed` itself and `InventoryUi.Rebuild()` calls `SetHero(h)`; applied on the control's next
  frame. Rebuilds when the model id changes (new game / load / other class).
- **Cheap**: renders only while visible in the tree (update mode Always/Disabled, animation asleep when hidden);
  `InventoryUi.OnClose` / `_ExitTree` call `Unload()` → model freed, render target shrunk to 16×16.
- **Test switches** (listed in `Game.KnownSwitches` since the second pass): `--portrait-stats` (every 2 s: viewport CPU/GPU
  render time, main view's, longest frame, pose signature, yaw, pipeline compilations), `--portrait-off`, `--portrait-scale=f` (f × pixels),
  `--portrait-model=<file id>[@scale]`.

## InventoryUi layout
- Doll children bottom to top: niche field (`DrawNiche`), `_portrait`, `_nicheRim` (the hollow "well" nine-patch drawn
  OVER the picture), `_cells` (what `BuildDoll` clears and refills; the portrait survives rebuilds).
- `NicheW` 236 → 372, `DollTagW` 100 → 60 (two glyphs are 52 px at 特大). Slot meaning unchanged: head/chest/gloves/legs
  left, amulet/rings/boots right, weapon under the niche.
- `DollGrow`: the doll is 48 design px taller at 標準, 24 at 大, 0 at 特大; side cells spread, weapon cell stays under
  the niche. Measured frame height: about 1003 design px at 標準 and 大, 996 at 特大 (crest still on screen).
  `OnTextScale()` re-lays it out; verified live (標準→大→特大→標準).

## What was tested and what the pictures showed
All at a 1600×900 window, town, level 20, `--gear`. I looked at every picture named here.
- `Y:\shots\portrait\all\sheet_final.png` — all 8 classes (final lighting, key 3.0): own model, own idle, weapon in hand
  (lampknight shield + lantern + sword, hunter aiming, forgebody fist weapons…), whole figure in frame with head-room,
  feet on the disc, no T-pose, no level geometry or sky, sharp edges.
- `Y:\shots\portrait\<class>\p1…p12` + `sheet.png` for emberblade, astromancer, lampknight, wildsoul, forgebody
  (taken with key 3.3, slightly brighter than final): `p1`/`p2` a second apart (pose differs), `p3` dragged (turned
  ~86°, lens backed off, nothing clipped), `p4` helm tooltip while turned, `p5` eased back, `p6` legendary weapon
  equipped from the bag (mesh `wpn_3`: e.g. taller crystal staff, kite shield, antler club), `p7` common (`wpn_1`),
  `p8` weapon unequipped by right click, `p9` rare again, `p10`/`p11` pause menu on top, `p12` closed.
- `Y:\shots\portrait\x2\` — `--textscale=2`: five classes ×2 shots, emberblade dragged + tooltip, `x2_inventory.txt`
  (layout dump: 75 controls, 0 flagged). `Y:\shots\portrait\scale\` — live scale switch.
- `Y:\shots\portrait\forms\sheet.png` — `form_bear@1.3`. `Y:\shots\portrait\q0\` — 低 preset (run OK; picture not reviewed).
- `Y:\shots\portrait\final\` — five picked shots (01 emberblade idle, 02 astromancer legendary staff, 03 lampknight
  dragged, 04 wildsoul idle, 05 forgebody at 特大).
- Animation while paused: the inventory itself pauses the game; pose signatures in the `--portrait-stats` lines keep
  changing, also with the pause menu on top (that menu dims the inventory to near black, so `p10`/`p11` show little).

### Performance (AMD Radeon 8060S integrated, 高 preset, 1600×900)
- The test display is a remote display at 32 Hz, so the normal `[perf]` line reads **fps=32 closed and fps=32 open**
  (frame 31.2–31.3 ms both) and cannot show small costs.
- With vsync off (`--gfx-novsync --gfx-stay --gfx-at=300`): closed 153–170 fps, open 142–176 fps with the portrait;
  closed 149–197, open 148–183 with `--portrait-off`. The run-to-run noise is larger than the difference.
- Renderer timers for the portrait's viewport: **0.21–0.33 ms CPU + 0.21–0.45 ms GPU** per frame at 305×245 px
  (main picture: 1.0–1.6 ms CPU, 2.6–9 ms GPU). At 4K-equivalent pixels (`--portrait-scale=2.4`, 732×588): 0.25–0.37 ms
  CPU, 0.5–1.0 ms GPU.
- Opening: model 1.3–5 ms; measuring 14–37 ms the first time a class is shown in a session (mesh read-back),
  1.3–6 ms afterwards. With a warm pipeline cache the frame after opening was 17.5 ms with the portrait vs 20.0 ms
  without. The `worst=250.0ms` in the `[perf]` line at the first open is there with `--portrait-off` too, and was
  there in a log from before this work (13:00).

## Known problems
1. **FIXED in the second pass** (see "Second pass"; text below is the first pass's analysis).
   **First-ever open stalls (MSAA pipelines).** With a cold pipeline cache the first open took **865 ms**
   (`--rendering-driver d3d12`, MSAA 4x; `Y:\shots\portrait\d3d12`) and **1444 ms** at the 低 preset (first use of
   MSAA 2x; `q0`). The main view never uses MSAA, so the portrait's MSAA makes the renderer compile new pipeline
   variants for the hero's materials on first draw. Later opens are fast (17–35 ms) because the driver cache is on disk.
   Planned fix, NOT started: in `BuildStage`/`ApplyQuality` set `Msaa3D = Disabled`, anti-alias by supersampling
   instead (in `FitPixels` render at up to 2× the screen pixels within a pixel budget, e.g. 0.9 MP / 0.45 MP at 低, the
   container already scales the picture down; optionally FXAA), and mirror the main viewport's
   `PositionalShadowAtlas16Bits` so the shadow pass matches too. Then re-measure the first open with a cold cache.
   Unverified assumption: that the plain (no-TAA, 1-sample) colour-pass pipelines already exist when the main view
   runs TAA (中 and above); if they do not, mirroring `UseTaa` of the root viewport is the fallback. I did not clear the
   user's shader cache to test; d3d12 was the only cold cache I had and it is now warm for MSAA 4x.
2. The portrait's own small shaders (floor, blob, backdrop, ember material) also compile on the very first use; the
   second pass's counters show no draw-time pipeline compile at the first open (their pipelines are built ahead), and the
   first-open frame with an empty Godot cache is no longer than without the portrait.
3. With no weapon equipped the model still holds the tier-1 weapon — that is what the world does
   (`Player.UpdateWeaponTier`), mirrored on purpose (`p8_weapon_off.png`).
4. Armour never changes the model; only the weapon tier (`wpn_1/2/3`, plus `_off`) is visible gear.
5. At 特大 the helm tooltip opens to the right and covers the portrait (existing tooltip placement, not changed).
6. Not tested: the real wildsoul shape-shift path through `Player.Form` (only the forced `form_bear` file), the 極致
   preset, a real 4K screen (display here is 1080p), `UiTheme.Art == false` fallback frame, vendor panels (they have
   no doll, so nothing was added there).
7. My mistake, cleaned up: a mis-escaped path created `Y:\shots\portrait$c\` during one batch; I deleted that folder
   (it held only that batch's 12 shots and a sheet). `portrait_batch.sh` uses forward slashes to avoid it.

## What remains
- Done in the second pass: problem 1, re-test at 高 and 低 with an empty Godot cache, `final\` re-shot, 低 picture
  reviewed. Not tested: 中 and 極致 cold (中 uses TAA like 高; 極致 uses FSR2 and a 32-bit shadow atlas, which the
  portrait now follows), a real 4K screen.
- Optional: decide whether a shape-shifted wildsoul should show the beast or the human.
- Optional (Ui.cs owner): let `newgame` use the `--slot` value instead of slot 0.
- MainMenu: the class screen renders its hero inside its own full-screen stage, so this control is not a drop-in
  replacement; `SetClass(ClassId, tier)` exists for a framed preview, and the measuring/solver could be lifted out for
  the menu to frame each class by real bounds instead of `Height × 3.45`.
- Suggestion for the lead: show gear visually (armour tiers / tints per rarity).

## Commands
Before every run: `tasklist | grep -i AshenAbyss` must print nothing. After runs: move
`%APPDATA%\AshenAbyss\saves\slot9*` to `%APPDATA%\AshenAbyss\saves_test_backup\` (done for all runs so far).
- Compile check: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\check.ps1 -Files InventoryUi.cs,HeroPortrait3D.cs,Game.cs,Player.cs`
- Cold Godot cache (build first with run.ps1; `<root>` must be a NEW folder outside `Y:\`):
  `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\ui_tests\portrait_cold.ps1 -Root <root> [-EngineArgs "--rendering-driver d3d12"] -GameArgs "--win --mute --new=lampknight --level=20 --gear --area=town --slot=96 --portrait-stats --gfx-q=2 --gfx-stay --gfx-at=300 --gfx-out=<root>/gfx --uido=@Y:/tools/ui_tests/portrait_perf.txt --quit-at=32"`
  then read the first `stats:` line after `PERF open-1` (longest frame, `pipelines compiled draw +N`).
- One class, full script: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\run.ps1 -GameArgs "--win --mute --new=emberblade --level=20 --gear --area=town --slot=96 --portrait-stats --uidir=Y:/shots/portrait/emberblade --uido=@Y:/tools/ui_tests/portrait.txt --quit-at=34" -Timeout 150 -Tag portrait`
- Five classes: `bash Y:/tools/ui_tests/portrait_batch.sh emberblade astromancer lampknight wildsoul forgebody` (about 5 min; run in the background)
- All 8 classes, two shots each: same run line with `--uidir=Y:/shots/portrait/all --uido=@Y:/tools/ui_tests/portrait_all.txt --quit-at=140 -Timeout 260`
- 特大: add `--textscale=2`, `--uido=@Y:/tools/ui_tests/portrait_x2.txt --quit-at=70 -Timeout 200`, `--uidir=Y:/shots/portrait/x2`
- Live text scale: `portrait_scale.txt` (`--quit-at=24`)
- Frame rate: `portrait_perf.txt` with `--portrait-stats --quit-at=58 -Timeout 180`; add `--gfx-novsync --gfx-stay --gfx-at=300 --gfx-out=Y:/shots/portrait/perf` for uncapped numbers; add `--portrait-off` for the baseline
- Cold-cache first open: add `-EngineArgs "--rendering-driver d3d12"`; preset: `--gfx-q=0..3 --gfx-stay --gfx-at=300 --gfx-out=<dir>`
- Read the numbers: `grep -E "portrait\]|perf\] fps" <newest %APPDATA%\AshenAbyss\logs\game_*.log>`
- Compare crops: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\ui_tests\portrait_sheet.ps1 -Files a.png,b.png -Out sheet.png -X 1095 -Y 122 -W 330 -H 262 -Scale 1.9 -Cols 3`

Script coordinates in `portrait.txt` are for 標準 at 1600×900: portrait centre 1260,255; helm cell 1013,162; weapon cell
1260,413; bag row 1 at y 532, columns from x 1005 in steps of 57 (`give set` puts the common weapon in column 1, the
rare one in 7, the legendary one in 10). Paths in bash: use forward slashes, never `\\$var`.
