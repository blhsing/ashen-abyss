# 燼淵 UI frame art — working notes (UI art director / UI engineer)

Goal: replace the flat code-drawn style boxes by ornate raster chrome rendered from real 3D models, and rebuild the
interface on it. Everything is regenerable by script; see `Y:\tools\uiart\README.md`.

## Pipeline (all in `Y:\tools\uiart\`)
1. `kit.py`     Blender toolkit: orthographic camera straight down, sun from the top-left, a studio world made of soft
                lobes (key top-left, cool rim bottom-right), a shadow-catcher plane as the panel field (→ cast shadow and
                contact occlusion end up in the alpha), geometry helpers (mouldings from a cross-section swept round any
                outline, bevelled plates, studs, domes, faceted stones, tapering wire for filigree), procedural PBR
                materials with scanned CC0 detail (hammered iron, gold, bronze, stone, leather, wax, ember stone, enamel).
2. `styles.py`  Style directions: material roles (bar / plate / face / trim / stud / stone / floor / band + state
                variants), moulding cross-sections, motifs (corner casting, crest, sprig, jewel, stud, edge detail).
3. `pieces.py`  Every piece: name, kind (nine / h3 / v3 / sprite), sizes in x2 texture px, post-processing, build function.
4. `render.py` (`render.ps1`)  one Blender run renders any list of pieces → `Y:\assets_src\ui_gen\render\<style>\`.
5. `slice.py`   seamless edge tiles (two rendered periods are cross-faded), outer drop shadows, glows, ember bloom,
                field tiles from scans, the x1 set (Lanczos half), `manifest.json` → `Y:\game\assets\ui\{x2,x1}\`.
6. `mock.py`    composes a mock-up screen from a kit (style exploration).
Units: 2 texture px = 1 design px (1080p). The game picks the x2 set above 1080p, the x1 set otherwise, so art is
sampled ~1:1 without mipmaps (small node sprites carry `mip`: the game builds mipmaps for those).

## Step 1 — style exploration (done)
Mock-ups (inventory + legendary tooltip over a real town screenshot, 1600×900):
- `Y:\shots\uiart\style_A.png` — A: heavy forged iron, ember-gold inlay, Sun-Forge crest, ember stones, slate field.
- `Y:\shots\uiart\style_B.png` — B: ash-grey stone moulding, tarnished bronze caps with rosettes, votive candles and
  wax runs, ash-stained parchment field.
- `Y:\shots\uiart\style_C.png` — C: dark leather band with saddle stitching, iron straps and studs, lantern-window
  hasp, dark leather field.
(2x crops of each: `style_?_crop0..2.png`.)

**Chosen: A.** Why:
- Readability: the near-black slate field gives the highest contrast for 22–24 px parchment text and for the dim
  (secondary) text; B's parchment is necessarily lighter and warm-grey, which costs the dim text most; C's brown field
  merges with the game's warm firelit scenes behind semi-transparent HUD pieces.
- Hierarchy: A separates weights by construction, not by colour: window = wide moulding with a gold inlay line and cast
  corners, tooltip = thin moulding with a hairline and the rarity band, slot = rim with a hairline. In B the grey stone
  bar is as bright as the text and competes with it; in C the stitches turn into noise at 1080p.
- Identity: dead Sun-Forge crest (a rayed disc with an ember heart), ember stones as the only saturated accent, forged
  iron and soot — exactly the world's motifs; saturation stays reserved for gameplay (rarity, elements).
- Richness without noise: ornament sits on the frame (corners, crest), long edges carry one inlay line and a stud every
  128 design px.
B and C stay in `styles.py`: `render.ps1 -Style B -Pieces all` + `slice.py --style B` re-dresses the whole set.

## Step 2 — full asset set
(in progress — table of pieces and margins is kept in `Y:\tools\uiart\README.md` and at the end of this file)

## Step 3 — integration
- New: `UiArt.cs` (manifest, texture sets, draw helpers: Nine / Tile / Sprite / SpriteAt), `UiNine.cs` (a StyleBox that
  draws field tile + nine-slice + overlays + crest; shared instances).
- (in progress)

## STATE AT THE 0.3.0 PAUSE — 2026-11-21 (richer ornament pass: NOT STARTED in the art, plan below)
**Nothing of the richer set exists yet.** No piece was re-modelled or re-rendered, `Y:\tools\uiart\*` and
`Y:\game\assets\ui\` are byte-for-byte the pass-0 set, `UiArt.cs` / `UiNine.cs` are untouched, no Blender run of mine
is queued or running, no before/after pictures were made. Every panel is therefore wholly on the pass-0 look (style A,
17 px moulding) — there is no half-converted panel and no new texture name in the code. Whole project: check.ps1
BUILD OK (0 errors) after the last edit.

Per panel (all = pass-0 set, usable): inventory, vendors (5), skill tree, paragon (not mine), maps, quest log,
dialogue, world tier, rift, settings, pause, death, title menu / class select / save slots, HUD, tooltips.

### QA fixes that went in (code only; pictures in `Y:\shots\uiart\qa\`, driver scripts `Y:\tools\ui_tests\qa_fix\`)
| item | state | where | evidence |
|---|---|---|---|
| raster icons stained by rarity / element / class tint | fixed, seen in runs | `Icons.Tint(...)` at `UiTheme.ItemSlot`, `UiTheme.Icon`, `HudActionSlot.Bind`, `HudBuffs._Draw`, `HudMinimap.DrawOverlay`, `SkillTreeUi.IconAt` + `DrawBar`, `InventoryUi.FallbackSlot`, `VendorUi.KindIcon`, `MainMenu` signature skills; rarity stays on the slot rim (`SlotBox`) | `q1_inventory.png`, `q1_vendor_*.png`, `q1_skills.png`, `q1_hud_town.png`, `q1_class_lampknight.png` |
| toasts + area title over the hero | fixed, seen in runs | `HudNotices.Paint` / `LineAt` / `ToastLeft`: toasts in an upper-left lane below the boss band, title card at 20 % of the height (below a boss / target frame when one shows); `Hud` passes `BossBottom` | `r2_q2_town_title_toasts.png`, `r2_q2_title_under_boss_frame.png` |
| death panel late buttons, HUD shining through | fixed, seen in runs | `DeathUi.OnOpen`: veil, text and buttons fade together, usable 0.55 s after the panel opens (was ~2.05 s); `Hud._Process` hides toasts / title / warnings and ends the death flash while it is open. NOT mine: `Game.cs` opens the panel 1.1 s after the death (`_deathNext`) | `q3_death_1.8s_open0.7.png`, `q3_death_3.1s_open2.0.png` |
| `ObjectiveDone` true while the boss stands | fixed, seen in runs | `Hud.PollSlow`, `MapUi.RefreshAreaInfo` read `Level.Phase` (Boss → gold "◆ 擊敗…", Cleared → ✓ / （已完成）); the `Level.BossMon` workaround is gone | `q5_boss_alive_hud.png`, `q5_boss_alive_map.png`, `r2_q5_boss_dead_hud.png`, `r2_q5_boss_dead_map.png` |
| loot labels over the quest tracker | **written, SWITCHED OFF** (never seen in a run with a label beside the tracker — the driver could not stage it) | `Hud.KeepOutHud` hands the HUD rectangles to `HudWorld.KeepOut`, `HudWorld.BuildLabels` drops labels that overlap them. `Hud.LabelsYieldToHud = false` → no keep-out rectangles → old behaviour exactly. To finish: set it to true, stage loot under the minimap column (e.g. a `give`-style drop hook at a screen position in the `--uido` driver), look, keep. | — |
Not done from the icon notes (out of scope of the tint fix): `Icons.ForBuff` / `ForPassive` / `ForSpec` / `ForMarker` /
`ForClass` / `ForAffix` / `ForGlyph` call sites.

### Plan for the richer pass (decisions already taken — start here when resumed)
Keep the piece NAMES and the logical boxes; put the new models in a new module `Y:\tools\uiart\rich.py` that re-registers
pieces by name (add `replace=True` to `pieces.piece`; import it at the end of pieces.py unless `UIART_CLASSIC=1`), so the
pass-0 set stays reproducible. Sizes below are texture px of the x2 sheet (÷2 = design px).
1. `window`: moulding 58 wide in three tiers — outer lip 0–12 (z 13), channel 13.5–37.5 (floor z 5) with a gold guilloche
   (two tubes r 2.4, y = ±7.5·sin, period 32, z = zc ± 1.3·cos so they pass over / under, a gold bead in every eye),
   inner fillet, bead row (domes r 3.6 every 8 px, ONE mesh — add `kit.multi_dome`), bevel to the field. Tile period 64
   (px = py = 64; everything phase-locked to `ml` / `mt`). Corner castings reach 220 along both edges: iron L plate with
   scalloped inner edge and spear tips, web with a cusp on the diagonal (must stay outside the content rectangle:
   u < 84 or v < 76 from the outer corner), octagonal seat + gold bezel with claws + octagon-cut ember stone (r 13),
   gold leaves (add `kit.ribbon`: spine + half widths + raised midrib) on a wavy stem along each arm, iron volutes
   (`k.spiral` tubes, taper) at the scallops, studs at the tips. ml = mt = 28 pad + 220 + 24 blend = 272.
   In the game: `UiNine.Grow = 10` on WindowBox so the wider moulding grows OUTWARD and no panel layout changes
   (moulding then covers −10 … +19, content margin stays 32 × 28).
2. New sprites drawn by `UiNine` (add fields `Left`, `Right`, keep `Top`, `Bottom`; a `Centre` for rules):
   `clasp_l`, `clasp_r` (strap over the moulding, 120 long, rosette) at the middle of sides taller than ~500 px;
   `crest_foot` at the bottom centre; `crest` = a LOW sunburst (disc R 50, rays on an ellipse 200 × 92 → 200 design px
   wide incl. rays, ≤ 46 design px above the edge — panels have only ~44 px of room above their frame), wings on the
   moulding, pendant ≤ 27 design px below the edge.
3. Title cartouche: new h3 piece `title_plate` (88 high, scroll-finial caps 80 wide, dark `face_dn` face with a gold
   hairline). `UiTheme.SetTitle(frame, text)` stores the text and draws plate + title from the frame's `Draw` signal
   (after the stylebox, under the children) centred on the top edge (−18 … +26), over the lower half of the crest (the
   sun rises over the title). Title size 32 design px, following the text scale by 0.4 only (32 / 34 / 36). Then take
   the title label out of each panel's header row (Inventory 907, Vendor 247, SkillTree 594, Map 418 / 1158, QuestLog,
   WorldTier, Rift, Settings, Pause; Paragon via its owner) and replace the text 關閉 button by
   `UiTheme.CloseBoss(onClose)` (pieces `close*` exist but are unused: re-model as a round forged boss with an inlaid X).
4. Inside: `rule` + centre rosette, `header_line` flourish, `panel` / `well` as recessed wells with a gold line and corner
   studs, `scroll_track` / `scroll_grab` (forged rail, ember-lit grip), tabs with two hanger straps in the top padding +
   a `tab_rail` h3 drawn by the tab-row builders (`ItemUi.Tabs`, MapUi, QuestLogUi, SettingsUi).
5. `slot` (two-tier sculpted rim with a channel), `slot_rarity` as an inlaid ring sitting in that channel + glow,
   `btn*` with corner rivets and an ember under-glow on hover, `skill_slot*` to match.
6. Tooltip: thin moulding + small gold-leaf corners, `tooltip_crest`, rarity-tinted header plate behind the name (height
   from `RichTextLabel.GetParagraphOffset(1)`), ornamented separators.
7. HUD: `globe_rim` with claws / rayed ring / chain and a cast figure at the outer side (render `_l` and `_r`; try a
   voxel-remeshed hooded statuette, fall back to forged flame wings), `hud_plate` with end finials + `hud_crest`,
   `xp_frame` as an inlaid channel, `minimap_ring` heavier with compass studs, `tracker` / `title_rule` flourishes.
8. Menus: `menu_plate*` with end ornaments, `card*` + a `medallion` for `Icons.ForClass`, plaques for pause / settings.
   `MainMenu.Lair.cs` (title backdrop) is final — chrome only.
Checks still to do then: before/after pairs at 1600×900 in `Y:\shots\uiart\before_after_*.png` (inventory, HUD, tooltip,
skill tree, settings, title menu), 介面文字大小 大 / 特大, `refcheck.py`, `import.ps1` after every slice.
"Before" pictures can be taken from the current build at any time (nothing has changed visually except the QA fixes).

## Log
- 2026-11-21 richer-pass brief received; sources and pictures read, plan above written; QA fixes done by a helper and
  checked (4 of 5 seen in runs, the fifth switched off); paused for the 0.3.0 release before any art work.
- 16:40 pipeline written, first renders (window + crest) OK at first run; Cycles CPU, 96 samples + OIDN,
  ~11 kpx/s (AO + bevel nodes in the shaders dominate).
- Lighting lesson: with a bright soft box every dark dielectric turns grey (4 % specular of a bright world). The world is
  now a narrow key lobe + weak skirt, dull materials have a low specular level, iron is 85 % metallic with a dark base.
- 17:30 three style kits rendered and composited; A chosen; A's corner casting, crest and sprig reworked (the first
  sprig had a closed curl that read as an eye).
