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

## Resume (2026-11-20/21) — slots 92/93 only (AGENT_RULES)
- Fixes: `NavGrid.LineWalkWide` + `Path(from,to,width)` (straight walks that grazed wall corners pinned the hero:
  emberblade 0 blows / "stuck" at 3.3 m); run-up un-sticking: strike if a melee blow already reaches, then forced A*,
  then a side step, give up after 1.5 s without moving; detour time limit = route time ×1.6 + 4 s (min 12 s).
- Harness: `--gptest=chaseall` (run-up all classes + detour/LOS in wastes_d2 + cancel cases),
  `--gpresume` (switches the save resume on for a test run), `saveall --gpcases=…`, `verifyall --gpslots=… --gpold=…`.
  Scripts: `run_one.sh` (retries when the run lock times out), `run_save.sh` (A dungeon+overworld, B town+portal, C old format).
- chase2 (game_20261120_2214xx): all 10 run-ups PASS (8 classes + bear + wolf), emberblade cancel cases + hold PASS.
- verifyA (game_20261120_235123.log): 17/17 PASS (dungeon + overworld: seed, props, position 0.000 m, facing,
  totems, dead monsters, living count, chest, fog, ground loot). Overworld roster signature differs between builds
  (positions); restore matched by type+position, missed=0.
- 2026-11-21 lead: pause everything except click-to-attack. Save resume PARKED: `LevelState.Enabled = false`
  (verifyA 17/17 passed; town / portal / old-format verify (B, C) never got the run lock — not run). To finish later:
  `bash run_save.sh` (it uses `--gpresume`, slots 92/93), then set Enabled = true only if B and C pass.
- chase3 (01:0x): run-ups 10/10, detours 8/8 (all classes incl. emberblade + pactbinder), cancel cases all PASS;
  remaining FAILs were test artefacts (LOS check used the contact frame; soulbinder bolt still in flight after 0.7 s;
  hunter hold "walked toward cursor" = knock-back along the cursor line) → test criteria fixed, chase4 re-run.
- 2026-11-21 02:11 chase10 (game_20261121_020809.log): 36/36 PASS, 0 ERR/WRN — run-ups 10/10 (8 classes + bear + wolf),
  detours 7/8 classes (wildsoul: no detour target found that run; PASS in chase9), LOS 5/5 ranged-capable, cancel
  ground/evade/died/stun ×2, unreachable sealed boss, Shift, hold ×2. Further fixes after chase3: waypoint skipping
  when a prop sits on a cell centre, ResolveCircle never ends inside a wall + lets a body out of one + slides round a
  prop on its open side, side-step search (16 dirs, simulated steps, second try another way), A* that avoids the
  small-prop cells that pinned the hero (NavGrid.PathAvoiding), detour time limit set once from the first route.
- Save resume stays OFF (LevelState.Enabled = false). Test saves of slots 92/93 moved to saves_test_backup.
