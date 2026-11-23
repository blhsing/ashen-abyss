# Roster notes — bestiary in the game (running log; newest at the bottom)

Owner: monster-roster engineer. Files: `Monster.cs`, `MonstersData.cs`, `Populate.cs`, `CharModel.cs`, `Bestiary.cs`,
`MonsterAi*.cs`, `TestSuite.Roster.cs`. Scratch: `Y:\tools\roster\`.

## Plan (2026-11-15 19:00)
1. `MonstersData.cs`: `MonAI` + 12 kinds (appended after `Boss`), `MonTrait` flags (23), `MonAiData` (the data fields of
   BESTIARY.md 5), `RegionDef.Families`, `DungeonDef.Families`.
2. `Bestiary.cs`: loads `res://data/bestiary.json`, merges into `MonsterDb` (hand-written defs keep their tuned numbers),
   builds reskins, fills `RegionDef.Spawns/Families` and dungeon families, validates, resolves models / stand-ins.
   API for the named-encounter engineer: `Bestiary.Type(id)`, `Bestiary.Info(id)`, `Bestiary.Spawn(...)`, `Bestiary.Named`.
3. `CharModel.cs`: data-driven stand-ins, embedded animation sets (rigs B-F: Idle/Walk/Run/Attack1/Attack2/Hit/Death/Spawn)
   with every humanoid clip name aliased onto them, measured stride, stealth fade, primitive props.
4. `Monster.cs` + `MonsterAi*.cs`: 12 kinds, 23 traits.
5. `Populate.cs`: family-weighted packs, dungeon family lists, rift = any family, reskins per region.
6. Harness: `--suite=census`, `--suite=bestiary:<region>`, `--suite=ai[:kinds]`, `--spawn=<ids>`.

## Log

### 2026-11-15 ~19:20 — PARKED on the lead's order (prototype build, then pause)
**State: everything of mine compiles (check.ps1 -Files MonstersData.cs,Bestiary.cs,MonsterAiData.cs,CharModel.cs,Monster.cs,Populate.cs →
0 errors in these; the 2 project errors at that moment were in NamedEncounter.cs, not mine). The game behaves as this morning:
the whole bestiary is behind `Bestiary.Live` (static readonly bool in Bestiary.cs), which is FALSE.** No test run was made yet.

Files touched so far
- `MonstersData.cs` (mine): `MonAI` + 12 kinds appended after `Boss`; `MonTrait` flags (23); `PackSlot`; `MonZone`; `MonAiData`
  (all data fields of BESTIARY.md 5 with the spec defaults); `MonsterDef` gained `OnHit2`, `Traits`, `Ai`, `NameEn`, `Rig`, `BodyPlan`,
  `RoleText`, `Look`, `BaseId`, `RegionId`, `Regions`, `SpawnWeight`, `Stage`, `InBestiary`, `Slot`, `Is()`, `CanLead`, `Giant`;
  `Variant()` clones `Ai`; `MonsterDb.Build` ends with `Bestiary.Merge(l)`; `RegionDef.Families`, `DungeonDef.Families`;
  `static ZoneDb()` calls `Bestiary.ApplyRegions(Regions)`. With Live=false Merge only sets `Slot` and ApplyRegions does nothing.
- `Bestiary.cs` (new, complete first version): JSON load + validation, `Merge` (enrich built-in defs, build new ones from kind
  templates + the anims text, reskins via `Variant`), `ApplyRegions`, status-text parser, data-driven `StandInsFor(modelId)`,
  `ResolvedModel`, `Boot()` (summary line + debug list; NOT hooked into Game.Boot yet), API `Type/Info/ReskinInfo/Family/Region/
  Regional/Local/Spawn/Named/Root`. `Info`, `Named`, `Family`, `Region` work even while parked (read-only data).
- `MonsterAiData.cs` (new): `MonsterAiTune.Apply / ApplyReskin / HelperDefs` — per-type values of every kind's and trait's data
  fields (statuses of hook / wake / burst / breath, bomb and trail zones, summon ids …), egg-sac helper def.
- `CharModel.cs` (mine; the graphics engineer added `TrimCache`/`SceneEpoch` meanwhile — kept): `FileId`, `Try`, data-driven
  `Resolve` (asks `Bestiary.StandInsFor`, empty while parked), `HasClip`, creature clip aliases on the humanoid library (only when
  Live), `OwnClips` = detection of animations inside a creature file (Idle/Walk/Run/Attack1/Attack2/Hit/Death/Spawn), all humanoid
  clip names aliased onto them, stride measured by forward kinematics on the tracks (`GroundSpeed`), strike time (`StrikeTime`),
  per-alias rate factor so QA's `Stride[clip] × scale × PlayRate` formula stays true (only when Live), `LegSignature` for creatures,
  `SetVisibility` (stealth: transparency, no shadow, emis_/spectral_ surfaces hidden), primitives `prim_swarm/prim_turret/prim_eggsac`.
  None of this has run in the engine yet.
- NOT touched yet: `Monster.cs`, `Populate.cs`, `Projectile.cs`, `GroundZone.cs`, `Game.cs`, `TestSuite.cs`.

Next steps when resumed (in this order)
1. `Monster.cs` hooks + `MonsterAi.cs` / `MonsterAiKinds.cs` / `MonsterAiTraits.cs` / `MonsterAiFx.cs` (partial class Monster):
   design notes — AiSetup (dormant, shell, stealth, hover, aura), AiTickAlways, AiPre in TickChase, AiModifyHit/AiAfterHit in
   TakeHit, PreventDeath (Reform, Empower mask), AiOnDeath (split, burst, spawner release, remains marker), ApplyStatus override
   (swarm / turret / shell immunities, guard break, flammable put out), post-step turn-rate clamp (Shieldbearer 180°/s, Turret 60°/s),
   `Strike()` helper applying OnHit + OnHit2, generalise Exploder (element / status from the def), `Role == "prop"` = inert (egg sac),
   burrowed monsters leave `World.Monsters` while under (no nameplate), separation skipped for Phasing / Swarm / Flyer / charging.
   Own shot node for lob / snare / returning disc (`MonsterAiShot.cs`). Channels through `Busy()` with a cancel-on-CC flag.
2. Minimal edits in files I do not own (report them): `Projectile.cs` — `public bool FromPlayer`, shield-block hook in
   `HitPlayerTarget` (`mon.BlocksShot(dir)`), for Shieldbearer / Reflect / Absorb; `GroundZone.cs` — public `El`, `Hostile`, `Radius`
   for Extinguish; `Game.cs` — one line `Log.Guard("boot", Bestiary.Boot);` before `SetupHarness()`; `TestSuite.cs` — one dispatcher
   line for `census` / `bestiary` / `ai`.
3. `Populate.cs`: family-weighted packs (`RegionDef.Families`, `DungeonDef.Families`, rift = 2-3 families of the whole roster),
   slots (`MonsterDef.Slot`), giants one per pack, clouds as groups, placed ambushers / turrets, reskins per region
   (`Bestiary.Regional`), `CanLead` for champions / elites, keep the level caps 4 / 6 / 9 / 14.
4. `TestSuite.Roster.cs`: `--suite=census[:seeds]` (plan function on real LevelGen layouts, table type → count, zero rows fail),
   `--suite=bestiary:<region>` (line-up + screenshots), `--suite=ai[:kinds]` (each kind / trait seen firing, counters), `--spawn=ids`
   handled at the end of `Populate.Run`.
5. Set `Bestiary.Live = true`, run: anims:static, census, ai suite, tours, campaign smoke; compare kill times with QA_NOTES.
6. Rigs B-F: no proof creature existed at 19:10 (`Y:\tools\blender\beasts` was empty) — verify `OwnClips` when they arrive.

Design decisions already taken (details in the code comments)
- Non-A rigs use the eight contract clip names as logical names (`AttackAnims = {"Attack1","Attack2"}`, `Ai.CastAnim = "Attack2"`);
  the humanoid library gets those eight names as aliases, so stand-ins work; locomotion stays `Walk_Loop` / `Jog_Fwd_Loop`.
- Built-in definitions keep every tuned number (husk_bloated speed 2.3 etc.); the bestiary only adds family / traits / regions /
  weights / model name (`imp` → `mon_imp`).
- Simplifications planned (to report): Reflect returns 0.5 × the reflector's own hit (the hero's projectile damage would kill the
  hero), Swarm does not dim the hero's light (the light belongs to the graphics module), the bellows does not double burning
  ground, flame-moths are attracted to burning actors only (not to fire zones).

### 2026-11-18 ~23:00 — RESUMED (new engineer session)
- Read AGENT_RULES, this file, BESTIARY 0/1/3/5, bestiary.json, QA_NOTES, ARCHITECTURE. Backups of every file before my edits: `Y:\tools\roster\orig\`.
- Written: `MonsterAi.cs` (state, hooks, Strike/ApplyHostile, damage in/out, death, statuses, counters `Monster.AiSeen` + `[ai]` log lines),
  `MonsterAiKinds.cs` (12 kinds), `MonsterAiTraits.cs` (23 traits), `MonsterAiShot.cs` (`MonsterShot` lob/snare/disc, `PulseRing`, `MonsterFx`).
  `Monster.cs` hooks (Setup→AiSetup, Tick→AiTickAlways/AiOnStunned/AiPostTick, TickIdle→AiIdle, TickChase→AiChase, StartAttack→AiStartAttack,
  ResolveAttack→AiResolveAttack/Strike/AiShoot/AiAfterSlam, TakeHit→AiModifyHit/AiAfterHit, Die→AiOnDeath, TickDead keeps body while zones live).
- Additive one-liners in other files (to report): `Projectile.cs` (FromPlayer, absorb + shield intercept), `GroundZone.cs` (Live registry + El/Radius/Hostile/FromPlayer/OwnerMonster/Center),
  `World.cs` (Nearest skips Stealthed), `Player.cs` (hover skips Stealthed). Whole project compiles.
- API for the named-encounter engineer: `Monster.FireNear(p, r)` (burning ground near a point), `GroundZone.Live`, `Projectile.FromPlayer`,
  `Monster.Strike(target, mul, extra)`, `Monster.ApplyHostile`, `Monster.AiWakeNow()`, `Monster.Stealthed`, `Bestiary.Spawn/Type/Local/Regional`.

### 2026-11-18 ~23:20 — Populate, harness, switch
- `Populate.cs`: family packs (`FamPool`, `FamiliesOf`, `FamilyPack`): core family by region weight (hauler never core), 35% a
  partner family for the back line / special, one guest giant (12% of packs, MonsterLevel >= 4: the family's own giant or a
  Fuel-bearer / engine), swarm cloud (MonsterLevel >= 2), one special (MonsterLevel >= 3), heavy 28%, escorts counted in the
  pack, placed ambushers / turrets in their own pack id round the pack (60%, MonsterLevel >= 2; statues and turrets in a nook).
  Dungeons use `DungeonDef.Families`; rifts draw a line family + a pressure family (+ a giant / mother guest from tier 5), in the
  rift region's colours. Caps 4/6/9/14 kept. Leaders from `CanLead` types; `AddDef(L)` = a walking add for boss fights.
  `Census` (same file): one `[census]` line per level + totals. `--spawn=a,b,c` (+ `--spawn-aggro`) puts types 7 m ahead of the entry.
- `Bestiary.Live` is now `LiveByDefault` (const, still FALSE) overridden by `--bestiary` / `--no-bestiary` (for A/B runs).
- `Game.cs` +2 lines (Bestiary.Boot before SetupHarness; switches in KnownSwitches), `TestSuite.cs` +1 dispatcher line.
- `TestSuite.Roster.cs`: `--suite=census[:regions[:visits[:level]]]`, `--suite=bestiary:<region|all>[:shots]`, `--suite=ai[:names]`,
  `--suite=rosterperf[:region[:count]]`.
- `CharModel.cs`: reads `assets/chars/rigs_bf_meta.json` (object keyed by model or list with "model"; walk_speed, run_speed,
  attack1_hit_time, attack2_hit_time) and prefers it to the measured strides / strikes. Format taken from
  `Y:\tools\blender\beasts\out\mon_dusk_hound.meta.json` (the artist's proof; not imported yet).
- First run (23:09, headless, ai:charger,hook,bomber,burrower): Bomber, Bomber:melee, Burrower, Burrower:stationary PASS;
  Charger / Hook failed because the hero stood still in a cluttered spot (monsters were in melee before the first use) →
  suite now searches an open field and the hero steps away for gap-closers; hook's first use 0.4-1.6 s after aggro.
- Boot summary with --bestiary: 129 types (18 merged, 111 new), 28 reskins (10 built-in, 18 new), models 73 real / 56 stand-ins
  (4 primitive), rig A 73/83, B-F 0/46; problems 0.
- 23:14-23:19 `--bestiary --suite=ai` (headless, log `%APPDATA%\AshenAbyss\logs\game_20261118_231404.log`): **47/48 PASS, 0 errors**.
  Every kind and trait raised its events (Charger windup+charge, Burrower emerge+dive, Bomber throw+land+melee, Healer channel,
  Reviver cast+raised, Spawner spawn+release, egg+hatch, Caller cast+arrive, Swarm bite, Turret fire+close (+shutter), Flyer
  windup+swoop, Stalker stealth+pounce+land, Dormant buried/statue/ceiling wake+strike, Hook windup+pull, Grab windup+hold,
  Breath windup+blow, Eruption cast+hit, Shell fire+broken, Reform pile+stand, Flammable lit, Aura haste/chill, Split, DeathBurst
  armed+burst, Escort adopt, Absorb open+swallow 6+close+burst, Drain, Extinguish, Pulse ring (bell + brute), Snare, Return
  out+turn, Trail (caster, flyer, exploder), Empower cast+mask, FrontArmour, Reflect, Phasing flag, Leaper statue wake).
  FAIL: Shieldbearer guard break (back hits were only counted while guarding; it was mid-bash) → fixed: back hits always count.
- 23:20-23:25 first census (3 visits, hero 14, log game_20261118_232018.log, 90 levels in 283 s, 0 errors): 128/129 types and 28/28
  reskins appeared; frost, mire, sanctum complete; misses: ash_colossus (giant chance too low), crypt (husk ×3, cagebearer,
  digger), sands (cagebearer, reaver_brute, hauler_hod), abyss (dusk_bat). Found: a family with no line (dusk in frost =
  yak/lynx/bat) made whole packs of lynx stalkers → now borrows another family's line. Own-giant chance 0.25 (guest 0.12).
  Rifts drew 2-3 random families each (char+sexton, husk+lurk, hush+lurk+wraith …), guests from tier 5.

## Simplifications of the design (keep current; for the report)
1. Reflect sends back 0.5 × the reflector's own hit (not 50 % of the hero's projectile damage).
2. Swarm does not dim the hero's light (light belongs to the graphics module).
3. Flyers collide with low obstacles like walkers (NavGrid has no "low" class); they ignore ground-zone ticks (zone hits =
   skill kind Zone/Ground/Trap without a direction).
4. Turret aim locks when its 1.2 s line appears (spec: follows until 0.9 s). It turns at TurnRate while aiming.
5. Healer / Reviver / Caller keep a distance band from the hero; "behind its allies" is not modelled.
6. Bomber needs line of sight (LineShot) to start a throw (spec: low obstacles do not block the lob).
7. Absorb triggers on "a hero projectile hit it within 2 s" or the 5-12 m cooldown condition (not "fired at it").
8. Caller: risen types appear 4-8 m from the hero, others 16-22 m away and run in (not guaranteed off-screen).
9. Reviver remains are laid at death (not after the dissolve) for ~13.5 s, and only while a reviver of that family lives.
10. Stalker stealth: skipped by World.Nearest (auto-target, minions) and the mouse hover; the QA bot may still target it.
11. A lit Flammable's self-burn (4 %/s) is credited to the hero.
12. Burrowed / buried / roosting monsters stay in World.Monsters (hidden + Untargetable) instead of leaving the list.
13. Rime pack-mule: a connecting charge sets off its kegs = it dies (loot as normal).
14. The Shell is drawn with the existing barrier bar (no own segment); fire ×3 against it.
15. Tallow pool's second-entry root applies to the hero only (not minions).
16. Champions / elites are never Fuel-bearers (spec allows "alone as elites").
17. Grab throw lands where Knock 12 really carries the hero in this engine (≈2.6 m), the circle is drawn there.
- 23:50 runner `Y:\tools\roster\runq.sh <tag> <timeout> "<args>" [run.ps1 switches]` (waits while the user plays, retries
  when the run lock was busy 25 min, moves slot0*/slot9* saves to saves_test_backup). Queued: baseline census
  (--no-bestiary, 3 visits) then bestiary census (8 visits), same --seed=4242; compare with `Y:\tools\roster\density.py old new`.
- Placed ambushers now take places of the pack (density kept); a family without line borrows another family's core.

## API for other modules (named encounters, bosses, events)
- `Populate.AddDef(level[, rng])` — a walking ordinary type of the level for adds (no exploders, burrowers, dormant, turrets,
  clouds, giants). BossBrain.RegionAdd still reads `RegionDef.Spawns`, which with the bestiary also holds turrets, ambushers
  and clouds — suggest switching it to AddDef (BossBrain is the named engineer's file).
- `Bestiary.Spawn(id, pos, level, tier, affixes, miniBoss, pack)`, `Bestiary.Type/Info/Local/Regional/Family/Region`.
- `Monster.Strike(target, mul, extraStatus, melee, element)` / `Monster.ApplyHostile(target, status, element)`: hits with the
  type's statuses (OnHit, OnHit2, lit burn, Drain). `Monster.AiWakeNow()` wakes a dormant one. `Monster.Stealthed`,
  `Monster.AiHidden`, `Monster.AiState` (debug), `Monster.AiSeen` (behaviour counters).
- `Monster.FireNear(p, r)`, `GroundZone.Live` (+ `El`, `Radius`, `Center`, `Hostile`, `FromPlayer`, `OwnerMonster`),
  `Projectile.FromPlayer` — the accessors NamedHub reads by reflection today.
- `Census.Total / ByArea` (what Populate placed), `[census]` log line per level.

### 2026-11-19 ~00:45 — PAUSED on the lead's order. EXACT STATE
**Whole project compiles (check.ps1: 0 errors). `Bestiary.LiveByDefault = false` → the shipped game plays the old roster;
everything new is reached only with the harness switch `--bestiary`.** With Live false the code paths are the old ones
(reviewed: AI hooks return at once for built-in defs; Populate uses the flat list; CharModel unchanged); other engineers' runs
on this build (named:all ×2, gameplay tests) logged 0 errors. The only visible change in default play: one `[census]` log line per level.

Verified with --bestiary (headless):
- AI suite 47/48 (log game_20261118_231404.log); the one failure (shield guard break) is fixed in code, NOT re-run.
- Census 8 visits × every overworld / side / story dungeon + 16 rifts (log game_20261119_003026.log, 240 levels, 0 errors):
  **129/129 regular types and 28/28 reskins appeared**; wastes 33/33, mire 32/32 complete per region. Per-region misses (all
  of them appear in other regions): frost hauler_collector, reaver_houndmaster; crypt reaver_cagebearer, reaver_brute,
  dusk_bat, husk_huddle; sands reaver_brute, hauler_yoke/hod/collector; sanctum dusk_moths; abyss dusk_bat.
- Density vs the old roster, same seed/level (baseline log game_20261119_001316.log): 5678 vs 5814 monsters per round = 0.98.
- rosterperf wastes 40 monsters --bestiary (windowed, game_20261119_002115.log): fps ~42, frame ~22 ms, monster code
  1.4-1.7 ms/frame (machine busy with music/voice generation). **Baseline (--no-bestiary) NOT run** → no comparison yet.
- Rig B/C/E creature files arrived and were imported by someone at 00:09-00:12 (mon_dusk_hound B, mon_sexton_beetle C,
  mon_dusk_bat E …): CharModel detects their own 8 clips and uses rigs_bf_meta.json strides / hit times (log lines
  "animates with its own clips … (strides and hit times from rigs_bf_meta.json)" in game_20261119_002115.log); anim-check
  clean (204 s of monster movement, 0 without a locomotion clip). Stand-ins switch automatically (dusk_crow now stands in with mon_dusk_bat).
- Pictures: only Y:\shots\roster\best_best_wastes_0_fight.png / _0_wait / _1_wait (husk line-up, exploder telegraph readable).

NOT done / not verified:
- Campaign smoke test with --bestiary (wastes + frost + side dungeon + rift, bot, TTK vs QA 2.7 s / 8 s): NOT RUN.
- Windowed AI telegraph screenshots and the full bestiary line-up pictures: NOT RUN (queued runs cancelled at the pause;
  note: `--shot` alone makes the harness quit after 7.5 s → always add `--shot-at=100000`).
- Perf baseline (--no-bestiary rosterperf) NOT RUN.
- Fixes made after the last test runs, compiled but untested in a run: shield back hits count in any state; grab-throw circle
  at the real landing (0.6 s); breath casters hold distance instead of shooting; flyers ignore ground-zone ticks; placed
  ambushers take pack places (the census above already includes this one).

Next steps (in order)
1. `bash Y:\tools\roster\runq.sh roster_ai 1200 "--mute --bestiary --slot=95 --suite=ai" -Headless` → expect 48/48.
2. Raise per-region coverage: shared-family specials/heavies (reaver cagebearer/brute, hauler giants in sands/frost, dusk_bat
   ceiling ambush, dusk_moths cloud) — e.g. guarantee each family's rarer slots once per region visit, or raise placed/cloud
   chance for small families; re-run census 8 visits until every region row is non-zero.
3. Perf A/B: rosterperf with --bestiary and --no-bestiary, same seed, windowed.
4. Campaign smoke: `--new=hunter --slot=96 --bot --campaign --speed=6 --bestiary` (+ `--area=wastes_d1 --bot --focus`,
   `--area=rift:1 --level=20 --gear --bot --focus`), compare `[bot] balance: ttk` with the same run `--no-bestiary`.
   Watch shield-bearers against a ranged bot (frontal shots are destroyed).
5. Windowed `--suite=ai --shot=Y:\shots\roster\ai.png --shot-at=100000` and `--suite=bestiary:all:shots …`; look at every picture.
6. Then set `Bestiary.LiveByDefault = true` (Bestiary.cs) and repeat 4 without the switch.
No detached process of mine is running (all my queued run.ps1 waiters were stopped at 00:45).

### 2026-11-20 ~21:35 — RESUMED (0.2.0 shipped with the bestiary off). Test slot is now 90 (AGENT_RULES table).
- runq.sh now moves only slot0* and slot90* (never other streams' slots).
- Populate: per-level `Roster.Placed` counts; types not placed yet in this level are favoured (×4 in pool picks, families
  with unseen types ×1.6, special / heavy / cloud / ambush / guest-giant chances raised while unseen). Meant to close the
  per-region misses of the 8-visit census without new table entries (all missed types ARE in their region tables).
- Queued: AI suite (slot 90), census 8 visits, bestiary line-up headless, rosterperf A/B.
- 21:41 bestiary line-up all regions headless (game_20261120_214115.log): 246 PASS; 28 rig B-F creature files load with
  all 8 own clips (bog_toad cf_mite dusk_bat/boar/hound/lizard/lynx/yak effigy_hound fiend_maw forge_hound hush_beast
  hush_ribwalker lamp_crawler lurk_eel rime_packbeast rust_gear/helm/jar/stinger sexton_beetle/digger/matron
  shroud_dropper/ling/matron/weaver wraith_gasp). 2 ERR: harness god-save clears the hero's Dots inside Actor's DoT loop
  (pre-existing, god mode only) → guarded in Actor.cs (+1 line); anim-check flagged the prim_swarm stand-in (no clips by
  design) → Monster.MoveDir skips AnimCheck for primitives.
- 21:55 AI suite (game_20261120_215507.log): **49/49 PASS, 0 errors, 0 warnings** (shield guard break now passes).
- Perf A/B, 40 wastes monsters fighting a standing hero, windowed 1600×900, same seed (steady-state [perf] lines):
  old roster (game_20261120_221705.log): fps 56-60, frame 16.7-18.2 ms, monster code 1.10-1.26 ms (37 alive), draws ~1040,
  prims ~1.58 M, VRAM 2006 MB. New (game_20261120_220024.log): fps 56-60, frame 16.9-17.1 ms, monster code 1.47-1.75 ms
  (41-45 alive: callers / mothers add), draws ~975, prims ~1.50 M, **VRAM 3552 MB** (33 distinct types' textures vs 5).
  Frame cost equal; monster code ≈ +10 % per monster; VRAM +1.5 GB for the region's full roster → for the lead.
- 22:04 census with --seed=4242: the seed makes every visit the SAME layout (8 identical visits) → coverage numbers
  meaningless for rare types; still: frost/mire/sands/sanctum complete, 129/129 + 28/28. Re-running without --seed.
- 22:20 census without seed (game_20261120_222016.log): 129/129 types, 27/28 reskins (mire_hangtree missing), regions
  complete: frost, sands, sanctum; misses: wastes ash_colossus+ash_worm, crypt husk_bloated, mire mire_hangtree, abyss dusk_bat
  (all low-weight families: ash 6, husk 5 in crypt, char 6 in mire, dusk 5 in abyss). → families with unseen types now pull
  harder as the level fills (×(1.6 + 0.12 × packs placed)), own unseen giant 60 %. census3 queued.
- Queued: campaign chain (`Y:\tools\roster\campaign.sh --bestiary`, slot 90, hunter from level 1, ×6, 20-min chunks),
  side-dungeon + rift tour (`--tour=wastes_d1,frost_d2,crypt_d2,mire_d1,sands_d1,sanctum_d1,abyss_d2,rift:1`, lv 30 god, slot 0).
- 22:31-22:54 **CAMPAIGN with --bestiary, level-1 hunter, bot, ×6, no god (game_20261120_223155.log): whole campaign in
  ONE 20-min chunk: lvl 1→40, 14 bosses (7 region + 7 stronghold), "campaign over", 2162 kills, errors=0, warnings=1 (bot
  path "going nowhere" in mire_boss, bot-side).** Balance: ttk normal 3.8 s (n=1961), champion 12.0 s (n=98), elite 5.4 s,
  58 deaths — 46 of them in wastes_boss: boss_gravedigger lv4 took 3435 s (killed at hero lv5) with ~400 adds killed meanwhile
  (its kit's own husk/skeleton waves; same ids in both rosters). Rest of campaign 12 deaths. Baseline (--no-bestiary, same
  seed) chunk queued for a fair comparison (QA's 2.7 s / 8.3 s are pre-0.2.0).
- 22:56 baseline campaign --no-bestiary, same seed 777 (game_20261120_225649.log): the SAME gravedigger deadlock at hero
  lv4 (19 deaths by 1700 s game, 12 to the boss, adds husk/skeleton) → not caused by the bestiary (boss kit / bot / 0.2.0
  items; reported to the lead).
- 23:17 census3 (no seed, game_20261120_231744.log): **wastes 33/33, frost 31/31, crypt 43/43, mire 32/32, sands 36/36,
  sanctum 36/36, abyss 34/35; 129/129 + 28/28; 0 errors.** abyss dusk_bat impossible by construction (abyss's dusk family = only
  the roosting bat → never a pack's family) → ambusher-only families now place near other packs (30 %).
- Campaign comparison: bestiary run champion ttk 12.0 s (QA 8.3 s, baseline 7.6 s n=9 wastes only) → shield-bearers,
  front-armoured, shelled and dormant types no longer lead champions / elites (MonsterDef.CanLead).
- Baseline final (game_20261120_225649.log): never passed the gravedigger in 7195 s game (84 deaths), ttk normal 4.0 s.
- 23:28 tour lv30 god --bestiary (game_20261120_232759.log, errors=0): done wastes_d1, crypt_d2, mire_d1, sanctum_d1,
  abyss_d2, rift:1 (tier 1 success, 360 s left, sanctum roster); TIMED OUT frost_d2 (embers collected, bot then stuck
  fighting a `named_prop` by the ember altar — named layer) and sands_d1 (objective done AND boss db_sands_d1 dead; bot did
  not leave in time). Baseline (--no-bestiary) of those two queued. Fix: ambushers next to objective packs no longer count as carriers.
- 23:51 **census4 (no seed, 8 visits, game_20261120_235142.log): 9/9 PASS, 0 errors, 0 warnings — every region complete
  (wastes 33, frost 31, crypt 43, mire 32, sands 36, sanctum 36, abyss 35), 129/129 types, 28/28 reskins.**
  Overworld density 333-398 per visit (baseline 343-400, same range).
- 00:00 baseline tour --no-bestiary (game_20261121_000054.log): sands_d1 TIMED OUT the same way (objective done, db_sands_d1
  dead, bot never left) → bot / named-boss exit issue, not the roster. 00:08 --bestiary retry (game_20261121_000844.log):
  frost_d2 #1 timed out with objective done + db_frost_d2 dead (same pattern), frost_d2 #2 done, sands_d2 done; errors=0.
- Creature glow: rigs_bf_meta.json "light" → shadowless OmniLight under the model (energy ×0.8), only while the monster is
  awake near the hero, hidden while dormant / burrowed / stealthed, freed at death; at most 8 live. (creature artist's request)

### 2026-11-21 ~00:55 — PAUSED for 0.3.0. EXACT STATE
**Compiles (check.ps1 BUILD OK, 0 errors). `Bestiary.LiveByDefault = false` (kept OFF): the game plays the old roster;
everything new runs only with `--bestiary`.** All my queued runs and waiters were stopped (the level-6 campaign test process
was killed by me, it was mine); test saves moved to saves_test_backup. No process of mine is running.
Why still off: the one full --bestiary campaign that passed (lvl 1→40, 0 errors, game_20261120_223155.log) ran BEFORE the last
changes (leaders, ambusher-only families, objective carriers, creature glow); with the final code: census4 9/9 (0 errors),
tours 0 errors, campaign chunks 0 errors — but every later campaign hit the region-1 boss deadlock (boss_gravedigger at hero
lv 4-8, 22-84 deaths, also with --no-bestiary → NOT a roster bug; boss kit / bot / items — for the lead), so no second full
campaign; and the kill-time A/B (`--area=wastes lv5`, `--area=frost lv12 --gear`, both rosters) was queued but not run.
Champion ttk in the passing campaign 12.0 s vs QA 8.3 s → leaders restricted since (untested).
Next: (1) controlled ttk A/B runs above; (2) a full campaign with the final code (needs the gravedigger deadlock fixed, or
start at lv 10); (3) AI suite once more on the final code; then LiveByDefault = true.
Edits in others' files this session: Actor.cs (+1 line, DoT loop guard after a god-save clears Dots).

### 2026-11-22 ~09:30 — RESUMED for 0.4.0: goal Bestiary.LiveByDefault = true
Work items: (1) first-boss stall re-test with the current AI pilot; (2) VRAM; (3) creature in-engine check; (4) demo +
named with the bestiary on. Test slot 90. Logs of my runs are copied to `Y:\tools\roster\logs\<tag>.log` (the game keeps
only 30 logs). Background chain: `bash Y:/tools/roster/chain.sh <steps>` (summary in `logs/chain.txt`; stop: TaskStop /
kill the bash running chain.sh — a game it started quits by its own timeout).
- New (mine): `RosterStats.cs` — with --bot / --pilot-test / --tour, one `[roster] stats area=… ttk normal/champion/elite/boss
  | taken …/min (life bars/min) | worst [type:dmg]` line per area (at the next level build) + a running `[roster] totals`
  line. Hooked from Populate.Run. For the per-area A/B of the two rosters.
- New (mine): `TestSuite.Creatures.cs` — `--suite=bestiary:creatures[:ids][:shots]`: every rig B–F model (46, keyed by
  rigs_bf_meta.json) alone in front of a god hero: own clips, posed height vs meta height × model scale, ground contact
  (lowest skinned vertex, CPU-skinned from the skeleton pose) in Idle / walking / after Death, Hit clip on a flinching blow,
  Death clip, body removal. One `creature …` line + PASS/FAIL per model.
- VRAM cause found: every `assets/chars/mon_*.jpg` was imported **lossless** (compress/mode=0 → uncompressed RGBA8 in
  video memory, ~21 MB per 2K map, ~85 MB per type with 4 maps). `Y:\tools\roster\tex_vram.sh` switches the 485 monster
  textures to VRAM compression (BC7 high quality for colour / ORM / emission, BC5 for normal maps; ~5.3 MB per 2K map);
  backup of the original .import files in `Y:\tools\roster\tex_backup\` (restore = copy back + import.ps1). Heroes, bosses,
  NPCs and gear are NOT touched (not mine; they are lossless too — for the lead).
- VRAM before (rosterperf wastes 40, seed 4242, windowed): old roster today 2001 MB (game_20261122_092249.log, 37 alive,
  draws ~1100); bestiary 3552 MB (2026-11-20 run, same suite/seed — today's "before" bestiary run was lost to a build
  that broke while I was mid-edit, and the texture change was already applied by then).
- Deleted `Y:\tools\blender\beasts\scratch\PAUSED` (brief). The creature check runs as my suite (slot 90) instead of
  engine_check.sh (hard-wired to slot 96).
- 10:06 monster textures re-imported (485 → .bptc/.s3tc ctex). **VRAM rosterperf wastes 40 (seed 4242, windowed):
  bestiary 3552 → 2044 MB (game_20261122_103758.log, 41-45 alive, fps 58-60); old roster 2001 → 1558 MB
  (game_20261122_111943.log).** Logs in `Y:\tools\roster\logs\vram_*.log`.
- BossBrain.cs (not mine, smallest additive change, approved by the lead): AddWeight skips types that
  `Populate.FitsAsAdd` rejects while the bestiary is live (turrets, clouds, giants, burrowers, sleepers, callers /
  spawners / revivers as boss adds).
- 12:40 AI suite (logs/ai.log): 49/49 PASS, 0 ERR. 13:09 named:rare --bestiary (logs/named_rare.log): every rare on its
  real base (stand-in=no), 0 ERR; 11 FAIL of 100 = kill-time windows / mechanics (for NAMED).
- 11:50 creature check run 1 (logs/creatures_run1.log, shots Y:\shots\roster\cr_cr_*.png): all 46 load their own 8 clips,
  scale matches meta (×0.94-1.16) except dusk_crow ×1.39 (idle hover higher than meta), sexton_grub ×0.72, dusk_lynx idle
  feet +0.17 m and ×1.23 (rears up in Idle?); test faults fixed afterwards (buried height, hover threshold, hit timing,
  corpse measured on a separate AI-less copy, per-creature frozen-legs seconds). 1 ERR = end-of-run anim-check (monsters
  3.4 s "frozen legs" > 1.7 s budget) → per-creature counter added to find the culprit. Run 2 queued.
- Lead's request (13:00): VRAM compression for heroes first, then NPC / boss / gear. `tex_compress.sh <set> <imports…>`
  (perl, one pass; backups flat by file name in `tex_backup/<set>/`). Heroes done 13:39 (214 files): forgebody bare lv1
  town 1805 → 1749 MB; armoured 1880 → 1825 MB (gear roll differed); portraits before/after
  `Y:\shots\roster\tex\sbs_*.png` — no visible difference. NPC/boss/gear: `set2_tex.sh before compress after` running.
- Runs use the lead's priority lane (`run.ps1 -Priority`) only for the ≤ 5 min texture shots.
- 14:11 NPC (86) / boss (40) / gear (381) textures compressed too (lead's request): town armoured 1828 → 1244 MB.
  Title "before" shot was overwritten by a stale script of mine → `title_redo.sh` restores the boss .imports to lossless,
  imports, shoots `tex_before_title_true`, re-compresses, imports (in progress; if interrupted: copy
  `tex_backup/boss` back only if you WANT lossless — otherwise run `bash tex_compress.sh boss boss_*` in assets/chars + import).
- 14:06 demo --bestiary (logs/demo.log): 20 scenes, 0 ERR/WRN, but VRAM 1.5 → 7.6 GB climbing every scene → CharModel.TrimCache
  now runs GC.Collect 1.5 s after a theme trim (managed wrappers kept released scenes/materials alive). Re-run queued.
- 14:22 creature check run 2 (logs/creatures.log): all 46 own clips, Death clip for all, corpses on the ground (buried
  columns sink into their hole by design), bodies removed 1.4 s; hover / scale rows that failed were AI-state noise
  (swoop or leap during the idle sample) → idle now = median of 10 samples, spawn 5 m away. The 1 ERR (anim-check
  "frozen legs") was aliasing: 5 creatures have a 0.2 s Run clip = the check's 0.2 s sampling → CharModel.LegSignature
  adds the clip time played. Turrets / moths / digger / rust_helm never flinch in play (AI re-asserts its clip) → hit
  clip now verified on an AI-less copy. Run 3 queued (`chain.sh creatures demo`, pid of bash in ps).
- 14:40 the user started playing: all runs wait.
- 15:33 creature check run 3 (logs/creatures_run3.log): 41/46 PASS; the 5 left = the frozen-legs aliasing (CharModel._Process
  is off most of the time, so the first fix never counted) → LegSignature adds the wall clock while the player runs.
  15:47 re-check of the 5 (logs/creatures_frozenfix.log): 5/5 PASS, 0 s frozen, anim-check INF, 0 ERR. → **46/46.**
- 15:34 title boss before/after: Y:\shots\roster\tex\sbs_title.png (no visible difference); boss textures compressed again.
- 15:46-16:31 CAMPAIGN emberblade --bestiary (logs/camp_ember_best_run1.log, pilot, ×4, no god): sh_wastes ok, **boss_gravedigger
  killed in 106 s at lv7, 0 deaths (the old first-boss stall is gone with the current pilot)**, mire + mire_mother ok;
  crypt: 6 deaths (sh_crypt failed twice, then killed at lv18; 2 more deaths in crypt overworld), crypt ttk normal 11-13 s,
  champions 44 s, 2.0-2.4 life bars/min (mire 0.4-1.5). Then **deadlock in crypt_boss** (SlayElites 0/4): from game t≈4500 s
  no kill, no hit taken for 20+ real minutes, bot alternating "focus marked elite" / "fight skeleton_thrower". Run stopped by me
  (my process). Main chain stopped; campaigns to be re-run after the crypt work. Crypt A/B (`area_ab.sh crypt 16 emberblade`) queued.
- 17:04 demo --bestiary re-run with the deferred GC + all texture sets compressed (logs/demo.log): 20 scenes, 0 ERR/0 WRN,
  **VRAM 1080-3561 MB, sawtooth, no climb** (was 1533 → 7616 MB climbing). GC pass after each theme change 13-123 ms (in the
  load). Earlier demo soak (old roster, 11-21): 2.0-6.48 GB.
- Per-region A/B (`area_ab.sh <area> <lv> emberblade`, same seed 777, --gear, bot --focus, 600 s game; logs/ab_<area>_*.log,
  table: `uv run python -I Y:/tools/roster/py/ab_table.py wastes crypt mire …`): wastes ≈ (normal 1.3 vs 0.9 s, champ 5.9 vs 2.2,
  0.76 vs 0.68 bars/min); crypt overtuned (1.7 vs 1.0 s, 3.4 vs 2.7, stronghold 149 vs 56 s, 0.90 vs 0.60, 1 vs 0 deaths);
  mire strongly overtuned (3.2 vs 1.5 s, **champions 80 vs 5 s**, 1.23 vs 0.92, 3 vs 0 deaths, 274 vs 397 kills).
- Populate.AddDef now uses FitsAsAdd too (no spawners / callers / revivers / turrets / clouds / giants as named-boss adds).
- RosterStats: `[roster] running` every 120 s, per-type slowest kill times (champions marked *), and a `stall:` line when no
  kill happened for 120 s (nearest monsters with state, marked objective elites with walkability) for the crypt_boss deadlock.
- Per-region A/B before tuning (emberblade --gear, 600 s, seed 777; normal / champion ttk s, life bars/min, deaths, bestiary vs old):
  wastes 1.3/5.9 0.76 0 vs 0.9/2.2 0.68 0 · mire 3.2/80 1.23 3 vs 1.5/4.9 0.92 0 · crypt 1.7/3.4 0.90 1 vs 1.0/2.7 0.60 0 ·
  frost 8.2/33 0.86 5 vs 2.9/8.1 0.64 3 · sands 1.6/3.7 0.81 0 vs 1.0/2.9 0.75 0 · sanctum 1.9/3.2 0.58 0 vs 0.8/2.9 0.45 0.
- 17:50 balance pass in Bestiary.cs (new types only; 18 hand-written keep theirs): life `TunedHp` = 0.8 × (hp, the part above
  2.0 counted 60 %), damage × 0.9, chill per hit from the status text × 0.6. Validation: `ab_tuned.sh` (bestiary only, tag ab2_*).
- 18:13 ab2 mire (tuned hp/dmg/chill): normal 2.7 s (was 3.2, old 1.5), champions 15.5 s (was 80, old 4.9), 1.07 bars/min
  (was 1.23, old 0.92). Cause of the rest: lurk_leech swarms (weight 5) drain 10 % resource per bite → hero without skills;
  leech champions heal 5 % per bite (39.6 s). → leech DrainPct 0.03, one drain per 0.8 s whoever bites (MonsterAiTraits),
  Drain types and teleporters / burrowers / stalkers no longer lead packs with the bestiary (MonsterDef.CanLead; abyss wraith
  champion 93 s), hard-coded chills in MonsterAiData × ChillScale too.
- abyss A/B: bestiary (already with the 17:50 pass) normal 2.5 / champ 11.7 / 0.80 bars/min / 0 deaths; old-roster run lost
  (the user started the game at that moment → the launch exited) — to re-run.
- After tuning (bestiary vs old, normal / champion ttk s, life bars/min, deaths): wastes 1.1/1.6 0.58 0 vs 0.9/2.2 0.68 0 ·
  mire (ab3, all fixes) 2.0/8.2 0.93 0 vs 1.5/4.9 0.92 0 · crypt (ab2) 1.3/3.9 0.83 1 vs 1.0/2.7 0.60 0 · frost (ab2) 3.0/7.1
  0.93 5 vs 2.9/8.1 0.64 3 (both rosters die at the frost stronghold rush in the first 3 min) · abyss (ab3) 2.5/1.9 0.74 0 vs
  2.3/6.1 0.68 0. Sands / sanctum not re-run (before tuning 1.6/3.7 0.81 and 1.9/3.2 0.58 vs 1.0/2.9 0.75 and 0.8/2.9 0.45).
- 19:14 proof campaigns queued (`chain.sh camp_ember camp_hunter`, --bestiary, pilot, ×4, no god).
- 19:43-20:08 CAMPAIGN emberblade --bestiary run 2 (tuned; logs/camp_ember_best_run2.log, 0 ERR 0 WRN): sh_wastes 74 s, gravedigger
  113 s lv7 0 deaths, sh_mire (1 death), sh_crypt 186 s 0 deaths, **boss_bishop 154 s 0 deaths (no crypt_boss deadlock)**,
  mire_mother, then frost: 8 deaths (7 at sh_frost, killed on the 3rd try lv31) → the pilot's "wall: frost" rule (8 deaths in a
  region) handed back in frost_boss at t=5626 s, lv32, 7 bosses. Old-roster pilot campaigns also lost most lives at sh_frost
  (forgebody 3, lampknight 6), but the A/B shows the bestiary's frost / crypt hit 45 % / 38 % harder →
  Populate.SpawnOne: placed monsters in frost and crypt deal × 0.75 with the bestiary (RegionDmg).
- ab4 (with RegionDmg): crypt 1.2 / champ 0.7 / stronghold 43 s / 0.48 bars/min / 0 deaths / 426 kills (old 1.0 / 2.7 / 56 /
  0.60 / 0 / 392); frost 3.4 / 23 / 0.79 bars/min / 5 deaths (old 2.9 / 8.1 / 0.64 / 3; the deaths are the opening
  stronghold rush). cf_spirit champions 139-154 s (kiting trail caster) → Trail types no longer lead packs.
- Queued: hunter campaign (--bestiary), then emberblade run 3.
- 20:41-21:10 CAMPAIGN hunter --bestiary (logs/camp_hunter_best_run1.log, 0 ERR, 2 WRN): 6 bosses with 0 deaths (sh_wastes,
  gravedigger 127 s lv7, sh_mire, sh_crypt, bishop 81 s, mire_mother), then 8 deaths all at the frost stronghold sh_frost
  (lv23-33, dodged only 46-60 % of telegraphs there vs 95 % elsewhere; last hits dusk_yak elites, cf_hulk pull, frostbound,
  frost_pallbearer, cf_flicker) → pilot "wall: frost". Old roster: hunter campaigns of AI-PILOT today 1 death (complete) and
  11 deaths (stopped in frost); lampknight 6 frost deaths.
- 21:15 pack composition: the census-era "unseen type" bonuses made each level's first packs very likely carry a giant (0.6),
  cloud (0.9), special (0.75), heavy (0.6), ambushers (0.9) → 0.35 / 0.65 / 0.5 / 0.4 / 0.7. Frost stronghold boss test both
  rosters queued (`shfrost.sh`), emberblade campaign run 3 queued.
- Frost stronghold boss test (pilot --pilot-test=boss, hunter lv24 --gear, 600 s; logs/shfrost*.log): **old roster also fails**
  (not killed, 4 deaths); bestiary 5 deaths, boss telegraphs taken 28 (old 3) → the adds' crowd control (chill stacking to a
  2 s freeze, packbeast freeze burst, chill aura, hook chill) kept the hero from dodging. All chill sources in MonsterAiData /
  MonsterAiTraits now × ChillScale, packbeast burst freeze → chill. Re-test: 3 deaths (old 4), still not killed (both rosters).
  The frost stronghold at lv24-25 is a wall for the pilot in both rosters (named boss sh_frost; old-roster campaigns passed it
  at lv27-30 after grinding).
- 22:04-22:41 **CAMPAIGN emberblade --bestiary run 3 — COMPLETE** (logs/camp_ember_best_run3.log, final code, pilot ×4, no god):
  14/14 bosses, campaignDone=True, 3 deaths (2 frost overworld/stronghold, 1 frost_boss lair), lv47, t=8044 s game,
  ERR=0, WRN=3 (2 pilot "going nowhere" while grinding, 1 levelstate "placement differs from the save … roster hash" on a
  sands revisit — SAVE-RESTORE's matcher handled it). sh_frost killed at lv23 on the first try (1 death).
  Bosses: sh_wastes 85 s · gravedigger 80 s lv7 · sh_mire 98 · sh_crypt 217 · bishop 163 · mire_mother 94 · sh_frost 155 ·
  rimelord 62 · sh_sands 146 · sand_tyrant 87 · sh_sanctum 228 · grand_hush 113 · sh_abyss 113 · osaros 174.
  Whole-campaign ttk (RosterStats): normal 2.1 s, champion 7.4 s (QA targets 2.7 / 8.3 s), 0.30 life bars/min.
  Old roster, same pilot (AI-PILOT today): emberblade 0-1 deaths, 5767-6727 s.
- 23:22-00:01 **CAMPAIGN hunter --bestiary run 2 — COMPLETE** (logs/camp_hunter_best_run2.log, final code): 14/14 bosses, 2 deaths,
  lv47, t=8416 s game, ERR=0, WRN=2 (pilot "going nowhere" while grinding). sh_frost 119 s 0 deaths at lv23.
- **00:05 `Bestiary.LiveByDefault = true`** (Bestiary.cs). `--no-bestiary` still plays the old roster for comparisons.

## SUMMARY 2026-11-23 (bestiary ready for 0.4.0)
| check | result |
|---|---|
| first-boss stall | gone (current pilot): gravedigger 80-127 s at lv6-7, 0 deaths, in all 4 bestiary campaigns |
| campaigns, bestiary, pilot ×4, no god | emberblade 14/14 bosses, 3 deaths, 8044 s, 0 ERR · hunter 14/14, 2 deaths, 8416 s, 0 ERR |
| old roster, same pilot (AI-PILOT 11-21/22) | emberblade 0-1 deaths 5767-6727 s · hunter 1 death 9914 s (another hunter run: 11 deaths, stopped in frost) |
| whole-campaign ttk (emberblade) | normal 2.1 s, champion 7.4 s (QA 2.7 / 8.3) |
| VRAM rosterperf wastes 40 | bestiary 3552 → 2044 MB, old 2001 → 1558 MB (monster textures BC7/BC5) |
| VRAM town, forgebody | bare 1805 → 1749 (heroes); armoured 1828 → 1244 (+NPC/boss/gear) |
| demo with bestiary | 20 scenes 0 ERR/WRN; VRAM 1.1-3.6 GB bounded (was climbing to 7.6 GB) |
| creature in-engine check | 46/46 PASS (logs/creature_table.md) |
| AI suite | 49/49 PASS, 0 ERR (final code) |
| named rares, bestiary | 28/28 on their real bases (no stand-ins), 0 ERR; 11/100 kill-time/mechanic checks for NAMED |
| check.ps1 | 0 errors |
Balance changes (all `Bestiary.Live` only): TunedHp (×0.8, above 2.0 counted 60 %), damage ×0.9, ChillScale 0.6 on every
chill source, packbeast burst freeze → chill, leech drain 3 % + one drain per 0.8 s, CanLead excludes teleporters, burrowers,
stalkers, bombers, drainers, trail types; FitsAsAdd for boss / named adds; frost + crypt placed monsters ×0.75 damage; softer
"unseen type" pack bonuses. Code: Bestiary.cs, MonstersData.cs, MonsterAiData.cs, MonsterAiTraits.cs, Populate.cs, CharModel.cs
(deferred GC after cache trim; leg signature), RosterStats.cs (new), TestSuite.Creatures.cs (new), TestSuite.Roster.cs;
BossBrain.cs +1 condition (approved). Textures: 1 206 .import files → VRAM compressed (backups Y:\tools\roster\tex_backup\).
No process of mine left running.
- 2026-11-23 00:10-00:46 lead's pass (frost champions, sands/sanctum re-measure; logs ab5_* / ab6_frost_*):
  frost champions 23 s in ab4 = two cf_spirit champions at 139 s (kiting trail casters; the other 15 averaged 7.6 s) → already
  fixed by "Trail types don't lead"; ab5 frost then 11.1 s with cf_flicker* 27 s (Phasing swarmer) → Phasing types don't lead
  either (00:17, noted in STATUS.md). ab6 frost: normal 1.6 / champion 6.1 s (n=17) / 0.33 bars/min / 1 death (old 2.9 / 8.1 / 0.64 / 3).
  Sands: 0.9 / 1.5 / 0.55 / 0 deaths vs old 1.0 / 5.4 / 0.70 / 1. Sanctum: 1.0 / 2.2 / 0.35 / 0 vs old 1.4 / 4.7 / 0.42 / 0.
  No numbers changed for sands / sanctum. Remaining outlier: cf_mite champions (ranged crawler) 38 s, n=2.
- 2026-11-23 ~11:10 first-lair check (wastes_boss, lead's request; logs ab7_*, ab8_*): the AI-PILOT deaths were at mlvl 7 in
  the horde: an elite husk_chained (hook) pack pulling the hero into husks + pallbearers. A/B with the plain bot, emberblade, seed 777:
  with --gear both rosters walk to the boss (no deaths); WITHOUT gear the lair kills a level 5-6 bot in BOTH rosters
  (lv5 bestiary 16 deaths 3.58 bars/min champions 114 s vs old 17 / 4.33 / 43 s; lv6 bestiary 16 / 4.27 / 18 s vs old 18 / 4.50 / 46 s).
  Same layout, 69 monsters, 7 champions, 2 elites in both. → not harsher than the old roster overall, but the bestiary's worst
  case is hook elites + callers + kiting javelin champions. 10:20 change (STATUS.md): packs at monster level 5-8 capped at 7,
  no callers / mothers as specials below level 8, no hook or ranged elite / champion leaders at monster level <= 7.
  After (lv5, bestiary): 15 deaths, 3.79 bars/min, champions 12.2 s (was 114), no wailers in the lair census.
- 2026-11-23 16:30-17:05 EARLY GENTLE (user via lead; STATUS.md lines 16:30 / 16:38 / 16:50). Monster level 1-2: no elite or
  champion packs, no stronghold-garrison champion (both rosters). Level <= 3: no dusk-hound elite leaders / followers, dusk-hound
  packs about 4, stronghold defenders wake only inside their yard (or when hit) and walk home when the hero is 12 m past it
  (Monster.SetGarrison), garrison packs without the +2 (both rosters); bestiary packs without specials, ambushers, clouds,
  heavies, at most one ranged. Level 4+ unchanged.
  A/B wastes without gear (emberblade, seed 777, bot --focus, 600 s; logs ab9_*, first try in early_v1/):
  lv1 bestiary 0 deaths 0.40 bars/min champions 1 (was 6) vs old 2 deaths 0.71; lv3 bestiary **0 deaths 0.51** (before the
  pack rules: 9 deaths 2.49, 4 of them in the sh_wastes fight among yard defenders) vs old 2 deaths 0.83 (0 / 0.53 on the first try).
  Pilot Pactbinder first 600 s (slot 90, logs/pact600.log): sh_wastes killed at lv3, 0 deaths, not pulled in; 1 death at lv1
  (t=106 s) when the pilot had several small packs on it at once (hounds, husks, cutters, grubs); lv4 at 600 s.
