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

## AI pilot: dodging and strategy (AI-PILOT agent, 2026-11-21) — slot 93
User request: "in ai-guided mode the bot always fails to beat a boss because it does not dodge any of the boss' high-damage
skills. make the bot actually play well enough to beat the game without dying".
- New files: `game/src/DangerMap.cs` (live register of hostile telegraphs + hit/dodge statistics),
  `game/src/AiPilotCombat.cs` (TestBot partial: per-frame dodge reflex, potions, defensive skills, kiting, retreat),
  `game/src/AiPilotPlan.cs` (AiPilot partial: level gate before story lairs, potion refill/rest before bosses, merchant
  upgrades, smith masterwork, paragon, `--pilot-test` harness).
- Hooks (one line each): Fx.cs Telegraph/TelegraphCone×2/TelegraphLine/TelegraphRing → DangerMap (+ Bind of the node);
  Projectile.cs `Live` list + getters; TestBot.cs (reflex before Think, pilot potion/evade replaced, Fight positioning,
  boss-test mode in _Ready, Errand hook); AiPilot.cs (_Ready, OnWorldEvent, Stop, Tick, OnDeath, Travel); Game.cs switch
  `pilot-test`. Noted in `Y:\tools\gear\CONTRACT.md` (CHANGE AI-PILOT).
- Harness: `Y:\tools\pilot\run_one.sh boss <class> <area> <level> [secs] [speed]` / `run_one.sh campaign <class> [secs] [speed]`
  (headless, slot 93, results appended to `Y:\tools\pilot\results\{boss,campaign}.txt`, saves moved to saves_test_backup/pilot).
- Background jobs (started by AI-PILOT, 2026-11-21 ~18:50): `run_bosses.sh hunter astromancer` (batch2b, parent subshell
  pid in `Y:\tools\pilot\batch.pid`) and `chain_campaigns.sh emberblade hunter astromancer` (pid in `camp.pid`, waits for
  batch2b). Stop: `kill <pid>` (the run in progress finishes by itself; its game quits at its --pilot-test limit or
  run.ps1's timeout). Resume: rerun the script with the classes not yet in `results/*.txt`.
- Results so far (boss test, `--bossrush --gear`, speed 2, v2 code): emberblade killed all 7 story bosses, 0 deaths in
  every boss fight (frost run: 2 deaths to trash packs before the fight with the v1 code → damage-intake potions/retreat
  added). Marked boss hits dodged: 62/68, 91/108, 134/137, 103/107, 75/85, 69/77, 170/176.
- 19:45 hunter: all 7 story bosses killed, 0 deaths in every boss fight (1 death to a frost elite pack before the
  rimelord fight). Astromancer (old build): wastes clean; frost_boss = freeze-lock deaths (chill stacks → 2 s freeze →
  no move/roll/potion while the boss hits). Fix: `PilotShedChill` rolls the chill off at 60 % (the roll clears CC);
  retreat now triggers on damage intake (≥30 %/2 s under 60 % life) and in boss fights (shorter step); ranged kite
  sooner when the intake is high; cast gate (`BossBrain.SpecialIn/BasicIn/BasicReach`, read-only getters added) holds a
  long swing back when the boss can strike before it ends. Boss batch for astromancer stopped after frost to start the
  campaigns (chain_campaigns.sh builds first, so the campaigns run the new code).
- 20:00–20:45 frost findings. Campaign v3 (emberblade) entered frost at lv5–6 (region 8) and died 12× there → level
  gate now = region level for a new region (lair = region + 2; grinding where the region level ≤ hero). Frost packs:
  every frost blow chills 0.25 and the 4th freezes 2 s (1.5 s immunity, then again) → a hero in a pack is frozen half the
  time. Tried: chill roll-off (kept), step-back from slow frost packs (now only when about to freeze with no roll),
  respawn walk-away, pack-mode (plain monsters' swing cones are not dodged when ≥3 enemies are close: dodging them cost
  the DPS race — baseline plain bot 4 deaths/300 s at `--area=frost --level=8 --gear`, pilot 7–9). Strategy changes:
  act-2 regions in the order crypt → mire → frost (AiPilot.RegionRank), defensive skills take the crowd-control
  immunity specialisation when they have one (PreferUnstoppable; e.g. 戰吼「無可阻擋」), merchant sells plain/magic junk
  for gold before buying upgrades.
- 20:46 campaigns started: `chain3.sh emberblade hunter astromancer` (pid in `Y:\tools\pilot\camp.pid`), speed 4,
  5 h game-time cap each, results in `results/campaign.txt`.
- 21:30 campaign emberblade (v4 build, 20:46): wastes, crypt, mire done with 0 deaths (sh_wastes, gravedigger, sh_crypt,
  bishop, sh_mire, mire mother); unstoppable war cry taken at lv5–6; frost stronghold sh_frost at lv18–20: 4 deaths
  (frozen, then pounded by sh_frost + frost elites) before it fell. Added `PilotCcShield` (cast the CC-immunity
  defensive before the freeze when frost packs close in / boss fight with adds). chain3 stopped; `chain4.sh` waits for
  this emberblade run (run_one pid 619601) and then builds + runs hunter and astromancer (pid in camp.pid).
- 22:05 campaign emberblade v4 result (game_20261121_203837.log, stopped by me at t=7742 s): new character → lv49,
  bosses killed in order: sh_wastes, gravedigger, sh_crypt, bishop, sh_mire, mire mother, sh_frost (after 4 deaths),
  rimelord, sh_sands, sand tyrant, sh_sanctum; total deaths 4 (all at sh_frost, frozen). Then a loop in 無聲大聖堂:
  bag stayed ≥34/40 after town (kept legendaries + gems) → bag trip every 60 s → the lair rebuilt afresh each return
  → idols never done. Fixed: no bag trips out of a dungeon/rift (AiPilot.BagTrip, 1 line), merchant errand now also
  stashes/sells/salvages non-upgrades until the bag is under 16. (The SYSTEMS agent changed one line of my Shop to
  `hero.IsProtected(it)`; kept.)
- 22:06 `chain5.sh emberblade hunter astromancer` started on a fresh build (pid in camp.pid).
- 21:56 CAMPAIGN emberblade (v5 build 21:14, game_20261121_211423.log): new character → campaign done, 14/14 bosses
  (7 story + 7 strongholds), **1 death** (sh_frost, frozen by a frost elite's crystals in the stronghold crowd),
  level 46, 6727 s game time (1 h 52 min; speed 4 → ~42 min of wall clock plus loads), 0 ERR, 2 WRN (the bot's own
  "going nowhere … focus boss" notices in two lairs). Marked hits: 1981 threats, 1862 dodged, 119 taken; boss marks
  1692/1615/77. Level gates hit: wastes_boss 4, crypt 8, sands 26, sands_boss 28, sanctum 33, sanctum_boss 35, abyss 40.
- 22:40 hunter campaign (v5 build) in progress: 0 deaths through crypt and mire, but sh_mire took ~17 min: the boss
  reset at 18 % life ("the brain let go") because kiting/chasing adds took the hero > 30 m outside the arena (BossBrain
  leash). Added an arena leash to the pilot (ArenaCost on every dodge/kite/retreat spot, adds outside the arena not
  chased, walk back when > ArenaR + 18 m). Not in the running hunter build; `chain6.sh` waits for the hunter run, then
  builds and runs astromancer (pid in camp.pid).
- 23:3x CAMPAIGN hunter (v5 build, game_20261121_214419.log): campaign done, 14/14 bosses, **1 death** (sh_frost, frost
  knights' chill 0.5/hit + crowd), level 45, 9914 s game time (2 h 45 min; slower than emberblade: long stronghold
  fights where kiting dragged the boss out of its arena → resets; arena leash added after). Marked hits 2678 threats,
  2596 dodged, 82 taken; boss marks 2261/2224/37.
- Heavy hitters' swing cones (knights, brutes: Def.Dmg > 1.15) are never "minor" any more (frost knights chill 0.5).
- 00:0x CAMPAIGN astromancer (final build 22:27, game_20261121_222732.log): campaign done, 14/14 bosses, **0 deaths**,
  level 45, 7928 s game time (2 h 12 min), 0 ERR, 3 WRN (bot's own "going nowhere" notices). Marked hits: 1914 threats,
  1899 dodged, 15 taken; boss marks 1578/1566/12. sh_frost 0 deaths (heavy-hitter + arena fixes in this build).
- After it: `chain7.sh` runs `run_bosses.sh` with ONLY="frost_boss:10 abyss_boss:42" for soulbinder, wildsoul,
  forgebody, lampknight, pactbinder (batch3.txt).
- 23:50 batch3 (final build, `--gear --bossrush`, speed 2): frost_boss lv10 / abyss_boss lv42 —
  soulbinder killed/killed 0+0 deaths; wildsoul killed (1 death, freeze chain at the rimelord) / NOT killed in 900 s
  (0 deaths, boss at 21 %); forgebody killed / NOT killed (1 death, enraged osaros hit 543 = 48 % of a 1138 life bar,
  boss at 33 %); lampknight killed / NOT killed (0 deaths, boss at 34 %); pactbinder killed/killed 0+0.
  The three time-outs are DPS races lost to the soft enrage (240 s) with harness gear (life ~1100 at lv42 vs ~3000 for
  the campaign heroes at lv44–45); the cast gate (PilotMayCast) costs melee some damage there. Not campaign-tested.
- FINAL: check.ps1 BUILD OK, 0 errors. No background jobs of mine left running. Test saves (slot93, plus a slot0 the
  runs left) are in `%APPDATA%\AshenAbyss\saves_test_backup\pilot\`.
- Tables: `bash Y:/tools/pilot/table.sh` (boss tests), `bash Y:/tools/pilot/camptable.sh` (campaigns; the per-fight
  lines are `TEST FIGHT` in each campaign log).

## 2026-11-22 — lead's final-code campaigns: hunter 11 deaths at sh_frost (凍骨關); forgebody stuck there too
Diagnosis (logs game_20261122_002409 hunter, 003559 wildsoul, frost isolation runs):
- 瓦雷克's garrison banner: while it stands 4 frostbound muster every 15 s and frost monsters near it can't be slowed.
  The script publishes the counter as `NamedScript.BotTarget` (and BotStand/BotPassive); the pilot ignored it → endless
  frost crowd → freeze chains. Fixed: `PilotNamedHint` follows the encounter hints in every named fight (also 第七號's
  pylons that reflect melee damage — wildsoul's only death was at sh_sands).
- My arena leash marched the hero back from the checkpoint into the crowd after every respawn (hunter deaths 8–11
  ~5 s apart). Now only when the boss is < 90 %, hero ≥ 70 % life with ≥ 2 potions, ≥ 15 s after a respawn, no crowd near.
- Level: the final hunter reached sh_frost at 17–19 (the v5 hunter at 24, 1 death). Isolated sh_frost at lv19 with
  harness gear kills every class 2–5× (hunter 3/5, emberblade 4, astromancer 2) → per-region stronghold level floor
  (`AiPilot.StrongholdLevel`, frost = 22; otherwise region level + 2). Below it the pilot levels elsewhere first.
- Two deaths without progress now regroup in another region for 3 levels (ChooseRegroup) instead of the same place.
- Bugs found on the way: DangerMap only subscribed to World.Damage after the first telegraph (damage intake = 0
  early: potions/retreat blind in the first minutes); act-1 lair level 4 → 6 (a level-4 hunter ran dry of potions in
  the graveyard); the new "ranged retreat in any pack" rule pulled the next packs (removed); retreats now avoid
  monsters that have not noticed the hero; no walking into a lair mid-fight; ranged heroes keep dodging plain swings.
- Proposed game-side (NOT changed, lead asking the user): chill keeps stacking during the 1.5 s CC immunity after a
  freeze, so in a frost pack the next blow re-freezes at once (~57 % of the time frozen). Reset chill when a freeze
  ends, or ignore chill while CcImmune > 0 (Actor.ApplyStatus, case Chill).
- Runs: `chain10.sh` = campaigns hunter, forgebody, lampknight (camp10.txt), then isolated sh_sands + 奧薩洛斯 for
  emberblade/astromancer/wildsoul (iso10.txt). pid in chain10.pid.
- 2026-11-22 05:00 results on the fixed code (game_20261122_*):
  - Campaign forgebody (032332): done, 14/14, 4 deaths (all at frost: 3 on the first sh_frost attempt at lv22–23, 1 in
    the frost overworld), lv46, 0 ERR. The lead's earlier forgebody run (old code) had 10+ deaths and was still stuck
    at lv30.
  - Campaign lampknight (035510): 8 deaths (2 crypt cultist packs, 6 at sh_frost); sh_frost killed on the 3rd attempt
    at lv30; stopped at sands_boss lv36 by my wall watcher (the 6-deaths-per-region rule as it stood then; now 8).
  - Campaign hunter (022655): wastes/crypt/mire 0 deaths; sh_frost a wall from lv22 to lv33 (7 deaths); I stopped it.
  - sh_frost A/B at lv22 (`--chill-grace`, test-only switch): hunter 5/4 deaths → 3/0; forgebody 8 → 12.
  - Isolated (sh_sands lv27 / 奧薩洛斯 lv42): emberblade 0/0 killed both; astromancer 0/0 killed both; wildsoul
    sh_sands NOT killed in 900 s (0 deaths, damage race), 奧薩洛斯 killed, 0 deaths.
  - Code after those runs (compiles; NOT campaign-tested): defensive skill fired when 3+ frost melee close in;
    AiPilot.WallReached hands back after 8 deaths in one region (reason shown to the player).
  - No processes of mine running; test saves moved to saves_test_backup/pilot by run_one.sh.
- 2026-11-22 freeze rule (user decision via lead), real game behaviour for every Actor (hero, minions, monsters):
  Actor.TickStatuses — when a freeze ends, chill (time and stacks) resets to 0; Actor.ApplyStatus — no chill is added
  while CcImmune > 0. `--chill-grace` switch removed (Actor.ChillGrace, AiPilot, Game.cs list). Buff-row tooltip for
  凍結/冰緩 adds 「凍結結束時，冰緩層數歸零」 (HudBuffs.cs, one line). check.ps1 0 errors.
  Runs: `chain12.sh` (pid chain12.pid): sh_frost lv22 hunter×2, lampknight, forgebody (iso12.txt), then campaigns
  hunter, lampknight, forgebody (camp12.txt).
