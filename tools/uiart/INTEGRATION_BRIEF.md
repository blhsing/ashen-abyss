# Integration brief — moving one group of panels onto the raster UI art

You are a UI engineer of 《燼淵 Ashen Abyss》 (Godot 4.7.2 .NET, C#, interface in Traditional Chinese, design size
1920×1080 stretched to the window). The game's interface chrome used to be flat code-drawn boxes. The UI art director
has produced a full set of forged-iron / ember-gold raster chrome rendered from 3D models and has already moved the
shared theme, the tooltips, the dialogs and the HUD onto it. YOUR job: do the same for the panels of your group
(named in your task), so that no flat code-drawn chrome is left in them, without changing behaviour.

## Read first (in this order)
1. `Y:\tools\uiart\README.md` — section "How the game uses it (C#)" and the piece table (names, sizes, margins).
2. `Y:\game\src\UiArt.cs`, `Y:\game\src\UiNine.cs` (short) and in `Y:\game\src\UiTheme.cs` the region
   "raster art" (`Art`, `ArtBox`, `Box`, `WindowBox`, `PanelBox`, `WellBox`, `TipBox`, `PlaqueBox`, `DrawTipBand`),
   plus `TabTone`, `Frame`, `Well`, `Rule`, `SlotBox`, `ItemSlot`.
3. Examples of finished custom-drawn integration: `HudBar.cs` (`_Draw`), `HudBossBar.cs` (`_Draw`, `LookArt`),
   `HudActionSlot.cs` (`EnsureStyles`), `HudWorld.cs` (`DrawBar`, `DrawLabels`), `CreditsData.cs` (`MenuKit.Btn`,
   `Entry`, `Check`).
4. Your own files, completely.
5. Pictures of the current state (1600×900 = the 1080p design scaled by 0.833): `Y:\shots\uiart\pass0\*.png` (new
   chrome, first pass: note what is already dressed by the theme) and `Y:\shots\ui\final\*.png` (the old look of every
   screen, with the file names telling which screen it is).

## Rules (hard)
- Edit ONLY the files of your group. Never touch `UiTheme.cs`, `UiArt.cs`, `UiNine.cs`, `Ui.cs`, `UiPanel.cs`,
  `Hud*.cs`, `Icons.cs`, the art, the tools, or any other file. If you need a helper or a piece that does not exist,
  use the nearest existing one and write the wish in your report.
- Environment: Windows 11, project on drive `Y:`. Scripts are PowerShell:
  `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\check.ps1 -Files A.cs,B.cs`. Never run Godot or
  `dotnet build` directly, never start Blender, never re-import assets.
- **Compile-check with `Y:\tools\check.ps1` after EVERY multi-line edit** (other engineers build the same project
  through the same launcher: the project must never stay broken for more than a minute). If the check reports errors
  in files that are not yours, ignore them (someone else is mid-edit) and look only at `-Files <yours>`.
- Run the game only through `Y:\tools\run.ps1` (one run at a time machine-wide; you will queue behind others — that is
  normal, wait for it; do not start a second run while one of yours is queued):
  `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\run.ps1 -GameArgs "--win --mute --uidir=Y:\shots\uiart\wip\<group> --uido=@Y:\shots\uiart\wip\<group>\s1.txt --quit-at=60" -Timeout 120 -Tag <group>`
  ("BUILD FAILED" with errors in someone else's file = wait a minute and try again). Always `--win --mute --quit-at=…`.
  Add `--textscale=2` for the 特大 pass (fonts ×1.3). The driver script (`--uido=@file`, one command per line):
  `wait s` · `open id[:arg]` · `close [id]` · `click|rclick|dbl|hover <x,y | @text[#n]>` · `unhover` · `wheel x,y,n` ·
  `key Name` · `shot name` · `dump name` (lists every visible text/click control and lints clipping) ·
  `menu title|newgame|class:<key>|name|load|settings|credits` · `newgame <class> [level] [area] [paragon] [hc]` ·
  `give common|magic|rare|legendary|unique|gem|set` · `gold n` · `mats n` · `points n` · `travel <area>` · `quit`.
  Panel ids: inventory, skills, paragon, map (arg travel), quests, vendor (arg merchant|smith|occultist|jeweler|stash),
  dialogue, pause, settings, death, tier, rift. Coordinates are window pixels at 1600×900. Ready-made flows for every
  screen are in `Y:\shots\ui\scripts\*.txt` (`final.txt` drives the whole game; copy the part for your screens).
  A test session starts at the title screen; `newgame emberblade 20 town` + `give set` + `gold 250000` + `mats 300` +
  `wait 4.5` puts a geared hero in town. Class keys: emberblade astromancer hunter soulbinder wildsoul forgebody
  lampknight pactbinder. Use slot-neutral commands only (do not delete or overwrite the user's saves).
- LOOK at your screenshots (Read tool) after every run, at 標準 (`--textscale=0`) and at 特大 (`--textscale=2`).
- No allocations in `_Draw` / per-frame code: look pieces up once (`UiArt.P("name")` into a static or instance field,
  lazily after `UiTheme.Art` is known), share `UiNine` boxes through `UiTheme.ArtBox(...)` (they are cached by key),
  never build style boxes per frame or per redraw.
- Keep a working fallback: when `UiTheme.Art` is false (art not installed) or a piece is null, the old flat drawing
  must still happen. Pattern: `if (piece != null) UiArt.Nine(this, piece, rect); else DrawRect(...);`.
- Every user-visible string stays Traditional Chinese and unchanged. Do not change gameplay, input handling, sounds,
  tooltips' content, the test hooks, or panel ids.
- ORIGINALITY: this game has its own look (dead Sun-Forge, embers, forged iron, soot). Do not reference or imitate
  any existing game's interface.

## What "done" looks like
- Hierarchy of weights: window (`UiTheme.Frame(true)` → heavy moulding, cast corners, crest) > sub-panel / card
  (`UiTheme.Frame(false)` = `PanelBox`, or pieces `card` / `card_on` for selectable cards) > inset well for lists and
  grids (`UiTheme.Well()` / `WellBox`, piece `well` + field `well`) > rows (`row`, `row_hover`, `row_on`) > tooltip.
  Do not nest heavy frames. Ornament frames the content and never sits behind text.
- Flat `DrawRect` borders, `StyleBoxFlat` boxes, hairline `ColorRect` rules and vector rings that act as CHROME are
  replaced by pieces: separators → `UiTheme.Rule()` or pieces `rule` / `rule_gem` / `header_line`; bullets → `bullet` /
  `bullet_gold`; selection / state borders → `boxline` tinted, `slot_hover`, `slot_selected`, `card_on`, `row_on`;
  buttons / tabs come from the theme (`UiTheme.Button`, `UiTheme.TabTone`); item cells → `UiTheme.SlotBox` / `slot` +
  `slot_rarity` (tinted with `Palette.Rarity`) ; skill cells → `skill_slot`; sockets → `socket` / `gem_slot`; key hints
  → `keycap`; small notices → `plaque`. Things that are CONTENT (map tiles, liquid fills, bars' coloured fills,
  connection lines of a tree, damage numbers, icons) stay as they are.
- Content margins: the heavy frame's moulding is 17 design px thick and its content margin is 32 × 28 (was 24 × 20);
  sub-panels 18 × 16 (was 14 × 12). Panels must still fit 1920×1080 at both text scales: nothing clipped, nothing
  overlapping, no text touching a moulding (keep ≥ 8 px of air), corner castings (they reach 27 px diagonally into
  the field) and the crest's pendant (27 px below the top edge, 60 px wide at the centre) never under text.
  Full-screen panels whose top edge is closer than 30 px to the screen top should use `UiTheme.WindowPlainBox`
  (no crest) or keep ≥ 34 px of room above the frame for the crest.
- Hover / pressed / focus / selected feedback is still visible everywhere. Rarity colours still read at a glance.
- Text stays readable: 20–24 px Chinese on the fields; never put text on a busy ornament; tinted layers must not
  reduce text contrast.
- Pieces that are small on screen (tree nodes) are flagged `mip` and can be drawn at any size.
- Some pieces are being re-rendered while you work (`row*`, `menu_plate*`, `target_frame`, darker slot floors and
  button faces): the names are final; a re-import will land during your session. If a piece is not there yet,
  `UiArt.P` returns null (logged once) — your fallback path covers it.

## Report (your final message; concise)
Per file: what was replaced by which piece / helper. Then: wishes (pieces or helpers you lacked), any chrome that is
still flat and why, anything you found broken, and the paths of your last 標準 and 特大 screenshots
(`Y:\shots\uiart\wip\<group>\`).
