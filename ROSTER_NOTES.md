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
