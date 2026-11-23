# GOLEM agent — progress notes (resume from here)

Brief: build Y:\tools\golems\PLAN.md (亡者名冊, golems, forged iron golem, minion-system upgrades) with placeholder art.
Test slot 95 only. Never touch slots 1-8 / install / shortcut. No commit.

## Files (mine)
- game/src/ClassBook.cs        book data (generic ClassBook) + SaveData partial (Book, GolemForge, Minions)
- game/src/Hero.Book.cs        hero-side book rules (pick, sacrifice, fee, stats, migration)
- game/src/MinionSystem.cs     persistence, follow/leash, attack-my-target, corpse refill, caps, LOD, hooks
- game/src/Golems.cs           golem/mage/warrior kind data (ART HANDOFF table), golem controller, command
- game/src/GolemForge.cs       forging rules, inheritance, fuse visuals
- game/src/GolemItems.cs       off-hand affixes, legendary powers, uniques (registered at runtime)
- game/src/HudMinions.cs       minion frame by the life globe
- game/src/BookUi.cs           亡者名冊 page in the skill screen + forge picker
- game/src/TestSuite.Golem.cs  --suite=golem

## Small edits in shared files (report to lead)
(see bottom; keep updated)

## Progress log
- 2026-11-22 ~11:00 code written: ClassBook, Hero.Book, MinionSystem, Golems, GolemForge, GolemItems, HudMinions, BookUi,
  SkillTreeUi.Book, DemoMode.Golem, Monster.Minion, TestSuite.Golem. Build green. STATUS.md note for AI-PILOT appended.
- First smoke run (iron forged, lv20 wastes, bot) queued: Y:\tools\golems\smoke_out.txt

## Shared-file edits (all marked "[golem]")
- Defs.cs: Stat enum + names: GolemLife, GolemDmg, MageDmg, MinionAtkSpeed, CmdCdr (appended before Count)
- Hero.cs: Recalc calls ApplyBookStats(); Eff calls BookEff(id, e)
- SkillsData.cs: sb_golem_cmd skill; kp_sb_golemheart key passive; sb_legion / sb_offering / sb_blood text+stats
- SkillExec.cs: CoefOf, Raise (public); FireSummon → MinionSystem.RaiseCast for sb_raise; rally re-forms golem;
  SpawnMinion uses MinionDefs.Resolve + MinionSystem.OnSpawned; FireCommand → MinionSystem.Command for golem_cmd
- Monster.cs: PickTarget minion → MinionSystem.TargetFor; MinionAttack → Ctl.Attack; TakeHit minion → MinionIncoming; Die → OnMinionDied
- Player.cs: TakeHit → MinionSystem.RedirectHero
- Projectile.cs: OnMinionHit callback
- Game.cs: MinionSystem.Install in Boot
- Items.cs: Weight(): golem affixes only for the Soulbinder (FIXES owns Items.cs balance)
- GearEval.cs: MinionTerms for the Soulbinder
- SkillTreeUi.cs: BuildBookTab, LayoutBookTab, ShowBook(arg=="book")
- TestSuite.cs: case "golem"; skills suite expects 9 Soulbinder skills
- DemoMode.cs: DemoGolem(h) after Gear(h)
- STATUS.md: note to AI-PILOT
- assets/icons/r/skill/sb_golem_cmd.png (copy of fb_break), r/keypassive/kp_sb_golemheart.png (copy of kp_pb_blood): placeholder rasters
- 11:19 --suite=golem (headless): 113 pass / 3 fail (test bug: 擲矛手 throwers have no kind behaviour — test fixed), ERR=0.
  Fixed after: armour fuse keeps its maker's look (was resolved to the Soulbinder's cloth model).
- 11:25 queued: --suite=golem:shots,perf windowed (retry_run.sh), then bot fights (botruns.sh, one run at a time, ≤10 min each).
- 11:40 background (this session): retry_run.sh shots (golem:shots,perf windowed) and botruns.sh (11 bot fights, sequential,
  each queues on the run lock, -Headless -NoBuild, logs Y:\tools\golems\logs\). Stop: TaskStop from the GOLEM session, or kill the
  bash running botruns.sh (find with who_runs.ps1). Resume: bash Y:/tools/golems/botruns.sh <names...>.
- 13:30 shots + perf done (58 fps with 10 minions + 40 monsters); ui_book re-shot opaque (lead's request); chest plates fixed.
- Bot batch 1 (no god): golems 0 deaths everywhere; full sacrifice 8 deaths (region) / 5 deaths (boss 194 s) → buffed:
  warriors sac +15% dmg, mages +3% crit, golem +6% DR, all three lines 三線盡獻 +20% dmg +10% DR (Hero.Book.cs).
- 17:55 seq2.sh running in background: --suite=golem (fresh build) then sacrifice_region, sacrifice_boss, bone_frost26, iron_region.
- 22:10 all runs done; slot95 saves moved to saves_test_backup (golem_slot95.*). No background jobs left.
- 23:05 bag filter + tooltip forge line added (InventoryUi/Hero.Marks/Items small edits, cleared by lead); full suite 129/129; slot95 moved to backup.
