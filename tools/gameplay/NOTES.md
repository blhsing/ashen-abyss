# Gameplay engineer notes (click-to-attack run-up, exact save resume)

## Status (paused by the lead, 2026-11-19 00:45)
- Whole project compiles (check.ps1: BUILD OK, 0 errors).
- LIVE: click-to-attack run-up (Player.cs). Tested headless in one process (`--gptest=chaseall`, log
  game_20261119_001852.log): run-up + strike PASS for emberblade, astromancer, soulbinder, wildsoul (human, bear, wolf),
  forgebody, lampknight; hunter struck at 12.9 m instead of ~16 (route was a detour, test then too strict); pactbinder
  target 23 m away by a long detour not reached in 10 s (test timeout; pactbinder LOS case PASS). Cancel cases PASS:
  ground click, evade, target died, stun, unreachable boss behind sealed door ("no path"), Shift = attack in place,
  hover kinds; hold-to-keep-attacking PASS for hunter, FAIL for emberblade (0 blows in 4 s, cause not found yet:
  diagnostics `Player.ChaseDebug` added to the harness, not yet run). Test target picking was tightened afterwards
  (direct routes only) - not yet re-run.
- OFF: exact save resume. `LevelState.Enabled = false` (LevelState.cs). While off: saves stay format 1 (Resume/Portal
  null, Fog empty are written as extra null/empty fields only - older builds ignore them), loading wakes the old way,
  the town portal back rebuilds the place afresh as before. NOT TESTED AT ALL yet (the saveall/verifyall runs never
  got the run lock before the pause). To finish: set Enabled = true, run `bash Y:/tools/gameplay/run_all.sh save`
  (slots 93/94/96/97/98 - check first that no other engineer uses them: slot 92 and 94-98 ARE used by others).
- Still on regardless of the flag (behaviour-neutral): roster recording after each area load (LevelState.OnArrived),
  SaveNow("area") now runs after World.Emit("area") instead of before, Level.DungeonExitPortal/RiftExitPortal refactor.
- No processes of mine are running. Test saves: my slot-92 file was overwritten by another engineer; their current
  slot92/94/96/97/98 files are in place; two stale copies of their slot 92 sit in saves_test_backup/gameplay_slot92.*.

## Files touched
- Player.cs: `_ch*` run-up state, `Order()`, `ReachOf()/InReach()`, `TickChase()`, `HoverKind`, `AttackTarget`, test mouse
  (`Player.TestMouse/TestLeft/TestRight/TestShift`, `TestClick`).
- HudWorld.cs (ui-core, 1 line) / Hud.cs (1 line): hovered nameplate also shows `p.AttackTarget`.
- Save.cs: `FormatVersion = 2`, Repair for the new fields.
- LevelState.cs (new): `LevelSnapshot`, `PortalSnapshot`, `FogRec`, SaveData.Resume/Portal/Fog; capture/apply.
- Level.cs: `BuildCheckpoint`, `BossFellAt`, `RewardChests`, `DoorOpened`, `LiberatedAtBuild`, `OpenBossDoor(instant)`,
  `RestoreBossDefeated`, `RestoreRewardChest`, `RestoreLiberated`, calls LevelState.BeforePopulate / OnPopulated.
- Game.cs: `_resume`, `_returnState`, `TakeResume` (load), `CaptureResume` (every SaveNow), BuildArea seed/pending/
  ApplyHero, `SaveNow("area")` moved AFTER `World.Emit("area")` + `LevelState.OnArrived`; `--gptest` switch.
- WorldObjects.cs: `Interactable.Spend` for chest/shrine/breakable/idol/prisoner/emberaltar. Interactable.cs: `Spend`, `RestoreSpent`.
- Populate.cs (roster engineer's): ONE line `// SAVE-HOOK` `LevelState.LiberatedForBuild(...)` (told a9189dad600ef6776).
- GameplayTest.cs (new): harness.

## Order on area load
Level.Build → BeforePopulate (monster level + build checkpoint from snapshot) → _Ready: Populate (LiberatedForBuild hook)
→ OnPopulated (keeps pending) → BuildArea: hero placed, ApplyHero → AreaEntered → Emit("area") (named layer replaces
leaders/rares/boss) → LevelState.OnArrived (records roster, applies snapshot) → SaveNow("area").

## Coordination
- cursor artist (ad3aab400e28f1a03): asked for Player.HoverKind — added, told.
- named engineer (a8845208710eba0bf): ok with rift~ not resumed, events reset, order above.
- roster engineer (a9189dad600ef6776): confirmed Populate determinism; told about SAVE-HOOK.
