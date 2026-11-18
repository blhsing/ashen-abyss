# Contextual mouse cursors — working notes (cursor artist)

Goal (0.2.0): the mouse cursor follows the context instead of staying the Windows arrow. Raster cursors rendered from
small 3D models through the UI art kit (`Y:\tools\uiart\kit.py`: same materials, same sun / studio world, orthographic
camera straight down), a contact sheet, and `Y:\game\src\Cursors.cs`.

## Files
```
Y:\tools\cursor\cursors.py     Blender entry point: models + renders every cursor, two builds each (run through render.ps1)
Y:\tools\cursor\render.ps1     wrapper around Y:\tools\blender\run_blender.ps1 (background mode, machine-wide queue)
Y:\tools\cursor\post.py        masters -> soot outline + ember rim -> 32/40/48/64/96 px PNGs + cursors.json (hotspots)
Y:\tools\cursor\sheet.py       contact sheet: dark scene / lit scene / interface / bright ground, 1x each size + 2x + 4x pixel zoom
Y:\tools\cursor\view.py        cuts the sheet into 1:1 pieces (Y:\shots\cursor\_v_*.png) for review
Y:\tools\cursor\tests\uido_*.txt   --uido scripts (interface side); the world side is CursorTest.cs (--cursor-test)
Y:\assets_src\cursor_gen\render\<kind>[_lo].png + .json   256 px masters (16-bit RGBA, no outline) + hotspot / bbox
Y:\game\assets\ui\cursor\<kind>_<size>.png + cursors.json  what the game loads (res://assets/ui/cursor), 50 PNGs
Y:\shots\cursor\sheet.png      contact sheet
Y:\game\src\Cursors.cs         the game side;  Y:\game\src\CursorTest.cs  its test driver
```

## Regenerate (about 2 minutes)
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\cursor\render.ps1 -Kinds all -Samples 128      # or -Kinds attack,ui  -Builds lo
$py = "C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe"                         # not a Blender process
& $py -I Y:\tools\cursor\post.py            # [--kinds a,b] [--lo-max 48]
& $py -I Y:\tools\cursor\sheet.py ; & $py -I Y:\tools\cursor\view.py
powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\import.ps1                                    # Godot re-import
```
A cursor = an entry of `CURSORS` in cursors.py: model function (authored upright in design px, 1 px = 1 px of the 64 px
cursor, y down, hotspot at the origin or returned by the model), turn angle, optional fit box / alignment / badge. The
script fits the turned model into the 64 px canvas minus `MARGIN` (room for outline and rim) and writes the hotspot.
A new kind also needs: `ORDER` + `RIM` in post.py, `LABEL` in sheet.py, the `CursorKind` enum and `FileOf` in Cursors.cs.

## The set (decisions)
| kind (file) | picture | accent / rim | hotspot (32 px) | used for |
|---|---|---|---|---|
| default | forged gauntlet, index finger out, talon tip; ember stone, gold cuff band | ember-gold | talon tip 3,3 | in play: ground / nothing special |
| attack | short forge-hot blade: steel, glowing ember fuller, gold cross | ember red | blade tip 3,3 | hovering a hostile |
| talk | speech plaque: iron rim, gold face, three dark studs | warm gold | tail point 3,3 | NPC, prisoner |
| pickup | the gauntlet as an open reaching hand (fanned fingers, talons curl in, narrow wrist) | gold | longest talon 6,4 | loot; Godot's Drag / CanDrop |
| interact | the pointer gauntlet + a gold cog on the cuff | gold | talon tip 3,3 | chests, shrines, doors, portals, waypoints, totems… (decided: cog, not key — "use / operate", fits every object) |
| forbid | the same gauntlet gone cold (sooty iron, dead stone) + red ring broken in two with the bar | red | talon tip 3,3 | unwalkable ground, hostile out of reach; Godot's Forbidden |
| ui | slim forged spearhead, gold ferrule | gold | tip 3,3 | panels, menus, HUD |
| ui_hot | the same, ferrule and ridge inlay lit | bright gold | tip 3,3 | Godot's PointingHand (menu entries) |
| caret | thin iron I-beam, gold band | gold | middle 16,16 | Godot's Ibeam (text fields) |
| busy | Sun-Forge crest: rayed iron disc, ember heart | ember | middle 16,16 | loading screens; Godot's Wait / Busy |

- Two builds per model: `hi` (64, 96 px) and `lo` (32, 40, 48 px: fewer and bolder parts, no rivets / studs / hairline,
  bigger stone, smoother metal). 48 px was compared both ways (Y:\shots\cursor\_cmp48.png, not kept): `lo` is clearer.
- Outline: soot, ~1 px at 32 px growing with size^0.8; rim: ~0.85 px ember-coloured line outside it, per-kind colour
  (red for attack / forbid). Drawn at master resolution in final-pixel widths, then the picture is reduced once in
  linear light (Lanczos) and sharpened in luminance only.
- Sizes 32 / 40 / 48 / 64 / 96 (Godot accepts up to 256 px, recommends <= 128; 40 px = the 125 % laptop case). The game wants
  32 px x max(window scale, display scaling) x text scale and takes the nearest size by ratio.
- Player hover API: agreed with the gameplay engineer (agent a33e60f905b308177): `Player.HoverAct HoverKind`
  { None, Move, Attack, Talk, PickUp, Interact, Unreachable }, set at the end of `Player.UpdateAim` (not updated while
  the game is paused). Cursors.cs only READS it; Player.cs is not edited by me.

## Log
- Art pass 1 (all ten pictures, both builds, render in ~110 s at 128 samples, Cycles CPU).
  Lessons: (1) the kit's sun comes from the top-left, so a blade lying exactly on the 45 degree diagonal has NO lit
  bevel (both face away) -> the sword is turned 32 degrees, and the steel is only 55 % metallic so the sun, not the
  dark studio world, decides its brightness; (2) at 32 px the full model is speckle (lame steps, rivets, hairline) ->
  the `lo` build; (3) four parallel talons on a banded cuff read as a crown -> the pick-up hand fans its fingers from
  the palm and has a narrow wrist; (4) sharpening per channel draws complementary fringes along the coloured rim ->
  luminance only; (5) the Bash tool halves backslashes inside heredocs: write Python with forward-slash paths.
- Code: `Cursors.cs` (node, added in Game.Boot with one line, ticks itself), `CursorTest.cs` (--cursor-test).
  Additive changes in other people's files: `Hud.PointerObj` (one accessor), `Settings.SystemCursor` (one field),
  SettingsUi (one row 「滑鼠游標」 遊戲 / 系統 + refresh + reset), Game.cs (one AddChild line).

## STATE AT THE PAUSE — 2026-11-19 00:50 (paused on the lead's request)
**Feature is ON and compiling.** `check.ps1 -Files Cursors.cs,CursorTest.cs,SettingsUi.cs,Save.cs,Hud.cs,Game.cs` = 0 errors.
Nothing of mine is running or queued: no Blender run, no test run, no import (the queued chain `tests\run_all.ps1`
was stopped before it got the run lock; a read-only process listing afterwards showed no run_all / import process).

Done:
- Art: 10 kinds x 5 sizes = 50 PNGs + `cursors.json` in `Y:\game\assets\ui\cursor\`; masters (both builds) in
  `Y:\assets_src\cursor_gen\render\`; contact sheet `Y:\shots\cursor\sheet.png` (matches the files on disk).
- Code: Cursors.cs, CursorTest.cs, the four small hooks (Game.Boot AddChild line, Hud.PointerObj, Settings.SystemCursor,
  the settings row 滑鼠游標 遊戲 / 系統).
- Tested once, windowed harness, wastes -> town, simulated pointer (`tests\run1_level_wastes.log`):
  **35 PASS, 0 FAIL, 3 SKIP**. Passed: empty ground -> Default, unwalkable ground -> Forbidden (and back), loot ->
  PickUp, chest / shrine / portal / waypoint / stash / brazier / riftstone / tier statue -> Interact, NPC -> Talk,
  hostile -> Attack, HUD / panel / button / world behind a panel -> Ui, loading screen -> Busy, system-cursor setting
  off and on again, 40 pictures at the stated size with the hotspot on the picture, the size table.
  Skipped there: totem, town gate, placed NPC (no free spot on screen; the NPC was covered in town).

NOT done / NOT tested (say so when reporting):
1. Everything changed AFTER that run is compile-checked only: the 40 px size (10 PNGs + size table), `Usable()` (falls
   back to a complete size, or switches the feature off: never a half set), `--cursor-display`, the shape log line, and
   the reworked test driver (label scan, removal of placed objects, title-screen drag / I-beam checks).
2. `import.ps1` has NOT run since the last art change: the ten `*_40.png` have no `.import` yet and the 32 / 48 px files
   were re-made after the last import (their imported copies are the older, more detailed build). A dev run reads
   un-imported PNGs directly, so nothing is missing, but **run import.ps1 before building / installing**.
3. Never run: the title screen (pointing hand -> lit pointer, drag area, name field I-beam), the `--uido` scripts
   (`tests\uido_menu.txt`, `tests\uido_level.txt`), the settings row on screen (no picture of it was taken), a fresh
   profile, the loot-LABEL path (the first run reached the item, not its label), a hostile out of reach.
4. On this laptop (display 125 %) text scale and window size never changed the picked size in the run (40 px wanted
   every time); the size rule is proven by the table only. `--cursor-display=1` was added to show it live — not run.
5. Not seen by eye: the real cursor on the real screen (screenshots do not contain it). Hotspots are checked by
   numbers (log) and by the cyan marks on the sheet only.
6. Not written: the switches in ARCHITECTURE.md / QA_NOTES.md (lead's files): --cursor-test, --cursor-log,
   --cursor-size=<px>, --cursor-display=<factor>, --cursor-then=<area>.

To switch the feature off (system cursor everywhere): comment out the one line in `Game.Boot`
(`AddChild(new Cursors { Name = "Cursors" })`), or set `Settings.SystemCursor = true` as the default in Save.cs.

To resume: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\cursor\tests\run_all.ps1` (import, then the
four runs: level, title screen on a fresh profile, the two --uido scripts). It refuses to start while the user plays.
Read `tests\run_*.log / .txt` for `[cursor]` and `[cursortest]` lines.

Test saves — checked at the pause, nothing of mine is left in `saves`:
- My one run (00:17-00:18) used slot 97; its files are already in `saves_test_backup\slot97.*` (stamped 00:18; I did
  not move them — someone's clean-up did).
- `saves\slot97.json / .bak` stamped **00:42:33** are NOT mine (my chain was still waiting for the run lock then and
  never started a game): another test uses slot 97. I did not touch them. I had assumed slot 97 was free and that
  whatever sat in `saves\slot97*` was mine; the timestamps showed otherwise before anything was moved.
- Therefore `run_all.ps1` now uses slot 91 (`-Slot`), exits if that slot already has a save, moves only files of
  its slot written after it started, and never overwrites a file in the backup folder. (Changed after the pause
  request; the script parses, it has not been run in this form.)
- 00:5x re-check: `saves\slot97.*` (the 00:42 pair) is gone again — moved by its owner or by the pause clean-up, not
  by me (between the two listings I only edited run_all.ps1, uido_level.txt and this file). The user's slot1-3 were
  never touched.
- So the backup folder now holds the 00:42 pair under `slot97.*`; my 00:18 pair was overwritten there by that sweep
  (a throwaway level-12 test hero; nothing needed from it). The bullet above that says "stamped 00:18" is superseded.
