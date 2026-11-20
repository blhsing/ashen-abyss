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
(Superseded by "RESUMED 2026-11-20" below: most of the NOT-done list here has since been run.)
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

## RESUMED 2026-11-20 (lead: "finish the open items"; the cursors shipped in 0.2.0; my test slot is now 94)
**PAUSED 2026-11-21 00:55 on the lead's request (0.3.0 release).** Nothing of mine is running or queued: the second
test chain was stopped while it still waited for the run lock (it never started a game; `tests\_mine.ps1` lists /
ends only processes that carry the cursor stream's markers: none left). No Blender run, no import. `saves\slot94.*`
does not exist (the first chain's saves are in `saves_test_backup\slot94_cursor_20261120_214715.*`).
`check.ps1 -Files Cursors.cs,CursorTest.cs,Game.cs,Hud.cs,Save.cs,SettingsUi.cs` = 0 errors.

**What is active in the working tree versus the shipped 0.2.0 (Cursors.cs, +34 lines):**
1. ONE behaviour change: while a mouse button is held and `Player.AttackTarget` is alive (and no enemy is under the
   pointer) the arrow stays the blade. Complete, passed its harness run (12/12 + 62/62 samples, see below); the real
   mouse button was not exercised (no cursor test can: all of them use the simulated pointer). To go back to exactly
   0.2.0 behaviour delete the single line `if (p.Hover == null && HeldAttack(p)) …` in `Cursors.Context`.
2. Inert without their switches: `--cursor-drop` (test), `Cursors.FileName` (test accessor), comments.
CursorTest.cs (+390 lines) only runs with `--cursor-test`. Art, cursors.json, imports: untouched since 0.2.0.

**NOT done when paused (each needs one test run; the lock queue was 25+ minutes per turn tonight):**
- an enemy no path leads to -> red ring after the run-up gives up, blade again 2.5 s later (`-Only chase`; the stage
  search was loosened after the first run skipped it — that new search has never executed);
- fallback to the nearest complete size when a picture is missing at ONE size (`-Only fallsize`, expects 48 px);
- 恢復預設值 brings the game's cursors back (added to `tests\uido_menu.txt`; `-Only uidomenu`);
- a second level run with the corrected import check and the walk-to-a-wall step (`-Only level`): both changes are
  compile-checked only (the corrected import check itself did run and pass, on the title screen);
- `--cursor-shots` + `ingame.py` (the cursor pasted into screenshots at its real size): written, never executed —
  so there is still no picture of a cursor in the game, only the contact sheet and the logs;
- in CursorTest.cs / run_all.ps1, compile- / parse-checked only, never executed: the SHOT lines (no run passed
  --cursor-shots), the 3x3 wall search, the --cursor-resize gating, the reworded chase evidence lines, the runner's
  retry / no-stale-log logic and its ordered -Only dispatch. The wait-out-a-swing loop before each simulated click was
  patched in before the 22:16 chase run, but I cannot show from the log that that build already contained it.
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

## RESUMED 2026-11-20 (lead: "finish the open items"; the cursors shipped in 0.2.0; my test slot is now 94)
**PAUSED 2026-11-21 00:55 on the lead's request (0.3.0 release).** Nothing of mine is running or queued: the second
test chain was stopped while it still waited for the run lock (it never started a game; `tests\_mine.ps1` lists /
ends only processes that carry the cursor stream's markers: none left). No Blender run, no import. `saves\slot94.*`
does not exist (the first chain's saves are in `saves_test_backup\slot94_cursor_20261120_214715.*`).
`check.ps1 -Files Cursors.cs,CursorTest.cs,Game.cs,Hud.cs,Save.cs,SettingsUi.cs` = 0 errors.

**What is active in the working tree versus the shipped 0.2.0 (Cursors.cs, +34 lines):**
1. ONE behaviour change: while a mouse button is held and `Player.AttackTarget` is alive (and no enemy is under the
   pointer) the arrow stays the blade. Complete, passed its harness run (12/12 + 62/62 samples, see below); the real
   mouse button was not exercised (no cursor test can: all of them use the simulated pointer). To go back to exactly
   0.2.0 behaviour delete the single line `if (p.Hover == null && HeldAttack(p)) …` in `Cursors.Context`.
2. Inert without their switches: `--cursor-drop` (test), `Cursors.FileName` (test accessor), comments.
CursorTest.cs (+390 lines) only runs with `--cursor-test`. Art, cursors.json, imports: untouched since 0.2.0.

**NOT done when paused (each needs one test run; the lock queue was 25+ minutes per turn tonight):**
- an enemy no path leads to -> red ring after the run-up gives up, blade again 2.5 s later (`-Only chase`; the stage
  search was loosened after the first run skipped it — that new search has never executed);
- fallback to the nearest complete size when a picture is missing at ONE size (`-Only fallsize`, expects 48 px);
- 恢復預設值 brings the game's cursors back (added to `tests\uido_menu.txt`; `-Only uidomenu`);
- a second level run with the corrected import check and the walk-to-a-wall step (`-Only level`): both changes are
  compile-checked only (the corrected import check itself did run and pass, on the title screen);
- `--cursor-shots` + `ingame.py` (the cursor pasted into screenshots at its real size): written, never executed —
  so there is still no picture of a cursor in the game, only the contact sheet and the logs;
- everything changed in CursorTest.cs / run_all.ps1 after 22:20 is compile- / parse-checked only: the busy-wait before
  clicks and the SHOT lines did run (chase 22:16 was built with them); the 3x3 wall search, `--cursor-resize` gating,
  the runner's retry / no-stale-log logic and the ordered `-Only` dispatch did not.
To resume: `powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\cursor\tests\run_all.ps1 -Only chase,fallsize,uidomenu,level,menu -Shots`,
then `python.exe -I Y:\tools\cursor\ingame.py Y:\tools\cursor\tests\run_level.log Y:\tools\cursor\tests\run_chase.log Y:\tools\cursor\tests\run_menu_fresh.log`.

### Results of 2026-11-20 (first chain, all on the shipped art; logs in `tests\`)
| run | what | result | log |
|---|---|---|---|
| level (wastes -> town, display factor forced to 1) | objects, interface, sizes, journey | 36 PASS, 1 FAIL, 1 SKIP | run_level_first.log |
| chase (wastes, real display 125 % -> 40 px) | click-to-attack run-up | 10 PASS, 1 SKIP | run_chase_first.log |
| title screen, fresh profile | menu, shapes, art, import | 10 PASS | run_menu_fresh.log |
| fallback: `attack` missing at every size, fresh profile | never a half set | 1 PASS | run_fallback_off_fresh.log |
| --uido title + settings, fresh profile | settings row, setting on/off, text scale | read by hand, no errors | run_uido_menu_fresh.log |
| --uido interface in town | HUD, panels, map, pause, settings | read by hand, no errors | run_uido_level.log |
- The one FAIL was my new check, not the game: "import: loaded pictures equal the PNGs" compared colour at every
  alpha > 0, and Godot's importer ("fix alpha border") recolours nearly invisible border pixels. Measured in the
  title-screen run with the corrected check: alpha identical in all 50 pictures, colour identical wherever
  alpha >= 20/255; 7249 pixels of alpha <= 19 recoloured. So the imports ARE current and lossless (closes the old
  open item 2); what ships differs from the contact sheet only in pixels under 8 % opacity.
- The SKIPs: "unwalkable ground" (none in view where the hero woke; it passed on 11-19 and shows twice in the chase
  log as `-> Forbidden (unreachable ground …)`; the test now walks to a wall) and "an enemy no path leads to" (no
  5x5 block of wall with a standing place; search loosened to 3x3) — both were queued in the second chain, which never got a turn.
- Now covered, all PASS: totem (`idol`), town gate (`gate`), placed NPC, the LABEL of loot (`pointerObj=loot`, the HUD
  owns that click), live size switching (text scale 0/1/2 -> 32/40/40 px; window 1280x720 / 1920x1080 / 2560x1440 ->
  32/32/40 px; at 125 % display text 特大 -> 48 px), title menu entry -> PointingHand -> lit pointer, class screen's
  turn area -> Drag -> reaching gauntlet, name field -> Ibeam -> iron caret, map panel -> Drag, pause-menu entry ->
  PointingHand; setting 系統 -> `[cfg] systemCursor=True` -> `[cursor] setting: the system cursor is used`, 遊戲 -> back;
  settings.json of the fresh profile ends with `"SystemCursor": false`. The settings row on screen:
  `Y:\shots\cursor\cursor_settings_game.png`, `…_system.png`, `…_scale2.png` (特大: hint wraps to three lines, nothing
  cut; layout lint: 28 controls, 0 flagged at both scales), `cursor_level_settings_game.png` (from the pause menu).
- Fallback, half proven: a picture missing at EVERY size -> five warnings, "no size of the cursor set has every
  picture: the system cursor is used", feature off, no errors. A picture missing at ONE size (-> nearest complete
  size) has NOT run yet: that run got no turn on the lock. (My runner then copied the previous run's console capture
  under its name — `run_fallback_size_fresh.log` said "pass=10" and was the title-screen run. Caught by reading it:
  no "fallback:" line, byte-identical to run_menu_fresh.log. Deleted; the runner now removes the capture before a
  run and writes no log for a run that did not happen.)

### Click-to-attack and the cursor (the lead's question) — run_chase_first.log
Stage: monsters frozen, one moved to open ground 6.5 m from the hero (the cursor is what is checked, not the fight).
- Before the click, enemy out of reach: blade.
- Click: run-up started, **the blade on 15 of 15 samples while the hero ran at it** (0.55 s), ended 'struck' at 2.9 m.
  The pointer is ON the enemy here, so the blade comes from the hover (`Player.HoverKind == Attack`).
- Run-up in progress, button UP, pointer moved to open ground: the gauntlet pointer (Default). Deliberate: a click
  there would walk and cancel the run-up, and the cursor says what a click would do. (`Player.HoverKind` is Move
  there; the gameplay engineer had said Attack would stay during a chase — it does not in the shipped Player.cs.)
- Button HELD, pointer slipped onto open ground: this was the gap. The shipped 0.2.0 shows the gauntlet pointer while
  the hero keeps running at / striking the enemy (holding keeps attacking by design). **Changed after 0.2.0, in
  Cursors.cs only**: while a mouse button is down and `Player.AttackTarget` is alive, and no other enemy is under the
  pointer, the arrow stays the blade (`HeldAttack`, 12 lines; revert = delete the one `if` in `Context`). Run: blade
  on 12 of 12 samples during the run-up and on 62 of 62 while it kept striking (6 blows), gauntlet pointer 0.4 s
  after release. The "button" in the test is the harness flag `Player.TestLeft` (the same flag GameplayTest uses),
  not a real mouse button: the real-mouse path (`Input.IsMouseButtonPressed`) is the same line of code but was not
  exercised.
- NOT run: an enemy no path leads to (red ring for 2.5 s after the run-up gives up) — see the pause list above.

### Switches (all read by Cursors.cs / CursorTest.cs; pass them after the `--`, like every harness switch)
| switch | what it does |
|---|---|
| `--cursor-log` | one `[cursor]` log line per change of the arrow's picture (with the reason, size and hotspot) and per cursor shape a control asks for |
| `--cursor-test` | CursorTest.cs: in a level the object / interface / size run, on the title screen the menu run; quits when done |
| `--cursor-test=chase` | the click-to-attack run-up checks (needs a level with monsters; freezes them) |
| `--cursor-then=<area>` | level run: where the journey for the loading cursor goes (default town) |
| `--cursor-resize` | level run: also resize the test window (1280x720 / 1920x1080 / 2560x1440); off by default, it covers the desktop for a moment |
| `--cursor-size=<px>` | force a size of the set |
| `--cursor-display=<factor>` | stand in for the display scaling (this laptop is 125 %: `=1` shows the 32 px case, `=1.5` the 48 px one) |
| `--cursor-drop=<kind[_size]>[,…]` | pretend pictures are missing (fallback test); `--cursor-expect=<px|off>` says what the test should find |
| `--cursor-shots=<folder>` | screenshots + `SHOT` log lines at the checked moments; `ingame.py` pastes the cursor in afterwards |
Not in `Game.KnownSwitches` (so they are only accepted after the `--`; run.ps1 always passes them that way) and not in
ARCHITECTURE.md / QA_NOTES.md: those are the lead's files and list none of the other streams' harness switches either.
A line the lead can paste into ARCHITECTURE.md 3.1 if wanted:
`游標測試開關：--cursor-log（每次切換游標寫一行記錄）、--cursor-test[=chase]（游標自動測試）、--cursor-size=<px>、--cursor-display=<倍率>、--cursor-drop=<圖名>（模擬缺圖）；詳見 tools/cursor/NOTES.md。`
