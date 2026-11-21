# MAP-QUEST notes (agent: quest targets on the maps)
Files: game/src/QuestMarks.cs (new), TestSuite.MapQuest.cs (new), MapUi.cs, HudMinimap.cs, Level.cs (marker follows monster),
Game.cs (explored fog of town+regions saved even with LevelState off), TestSuite.cs (one case line).
Test: run.ps1 -GameArgs "--win --mute --suite=mapquest --slot=90 --shot=Y:\shots\mapquest\mq.png" -Tag mapquest
After runs: move %APPDATA%\AshenAbyss\saves\slot9* / slot0* -> saves_test_backup.
2026-11-21 18:37: DONE. --suite=mapquest 22/22 pass, ERR=0 WRN=0 (log game_20261121_183536.log). Shots Y:\shots\mapquest\mq_*.png.
Slot 90 saves moved to saves_test_backup. Nothing left running.
2026-11-21 18:57 lead follow-up done: minimap pins r=15 design x UI text scale (~12.5 px at 1600x900) with dark rim + glow; map pins 16.5 (1.5x);
edge pins kept inside ring/pane by their reach, main pulses, collapsed ones get a ×n badge on the inner side. Suite 22/22, ERR=0 (game_20261121_185519.log).
