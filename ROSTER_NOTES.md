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
