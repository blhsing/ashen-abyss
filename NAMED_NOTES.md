# Named encounters — working log (named-encounter engineer)

Scope: 28 rare elites, 7 stronghold bosses, 14 side-dungeon bosses, 4 world bosses, 8 pinnacle bosses (61 new);
the 7 region bosses stay as they are (BossBrain kits). Design: BESTIARY.md section 4, data: data/bestiary.json `named`.

## STATE: PARKED 2026-11-15 ~19:40 on the lead's order (prototype build, then pause)

**Nothing of the named layer is active in the game.** No encounter is implemented, none was run, no test exists yet.
What is on disk is the framework only; it compiles and is not reached by any game code path.

### Files touched
| file | state |
|---|---|
| `game/src/BossBrain.cs` (mine) | seam for named encounters added; region bosses behave exactly as before (all new kit fields default to the old constants, all new hooks are null). Original copy: `Y:\tools\named\BossBrain.cs.orig` |
| `game/src/NamedData.cs` (new) | `NamedKind`, `NamedDef`, `NamedDb` (loads `named` from data/bestiary.json, stages title / scale / tint / factors / arena theme / placement rule / intro line for all 68, stand-in table for base types, `Registry` + partial `Register*` methods), `SaveData` partial (`NamedKills`, `NamedCache`), `NamedRecords` (read-only tallies for the UI team). Never called yet (`NamedDb.Ensure()` has no caller) |
| `game/src/NamedEncounter.cs` (new) | `NamedScript` base, `LightSpot`, `NamedEncounter : Node` (engage / reset / death life cycle, brain attach, mechanic log `[named] <id> mech <key>`, blocks: AtLife, Every, After/Then/Run, Blast, RingBlast, Waves, LineBlast, ConeBlast, Zone, Shoot, Summon/SummonAfter/RunIn, Prop (attackable passive monster), Fling/Drag/Blow/Drain/StripBuff, Light/InFirelight, life Floor). Never instantiated yet |
| `game/src/NamedProps.cs` (new) | prop `MonsterDef` + primitive looks (banner, pylon, crystal, urn, coffin, brazier, jar, heap, gear, stake, snuffer, slab, column, orb, patch, ring, cage) |
| `game/src/NamedHub.cs` (new) | **stub**: only the monster → encounter registry (`Link/Unlink/Forget/BrainCreated`); `Slain` and `FireNear` are empty |
| `Y:\NAMED_NOTES.md` | this file |

Not touched: `WorldEvents.cs`, `data/vo_lines.json`, and no file of anybody else. **No `// NAMED-HOOK` line exists anywhere.**

### Compile
`check.ps1 -Files BossBrain.cs,NamedData.cs,NamedEncounter.cs,NamedProps.cs,NamedHub.cs,WorldEvents.cs` → 0 errors in my files.
Whole project at 19:40: 1 error, `CharModel.cs(316,21) CS0136` (roster engineer's file, in progress — not mine, not fixed).

### What behaves differently from this morning
Nothing. The only runtime difference is one no-op call (`NamedHub.BrainCreated`) in the BossBrain constructor.

## Architecture decisions (kept for the resume)
- A named monster is spawned from a clone of its base `MonsterDef` (own Def.Id = the named id, so story-boss quest
  flags, `loot_first_*` and StoryVoice never mix with the 7 region bosses). Life / damage / XP are set after the
  spawn from the encounter's own factors (`NamedDef.Hp` = ordinary monster lives of its level), independent of Monster.Setup.
- Control: rare elites keep the base AI and get an overlay script (`Monster.Busy` for casts, `BossBrain.Remote` for
  patrols); stronghold / dungeon / world / pinnacle bosses run a `BossBrain` with a kit registered under the named id
  (`BossKits.Register`, `BossKit.Derive` for the remixes; `NoRoar`, `Immobile`, `Tempo`, `EnrageAt`, `EngageRange`).
  Script hooks on the brain: `Engaging`, `Disengaged`, `PhaseBegan`, `MoveStarted`, `Dropped`, `Held`, `Veto`, `Force`, `Suspend`, `Adopt`, `SetArena`.
- Things the hero must destroy (banner, pylons, urns, jars …) are passive monsters (`Busy` forever, model hidden, own look).
- Life floors / "cannot die yet" rules: `World.Damage` fires after the blow and before the death check, so the encounter
  puts life back there (`NamedEncounter.Floor`) — no hook in Monster.cs needed. Directional rules: `DrMul` per frame.
- `DialogueDb` lines are declared in Dialogue.cs, not read from vo_lines.json: the encounter speaks its own intro
  (`Hud.Subtitle` + `Audio.PlayVoice(id + "_intro")`); vo_lines.json only gets the entries for the voice run.
- Loot: named monsters are `NoLoot` for Loot.RollDrops (XP, counters and events still go through Loot.OnMonsterDeath)
  and drop through a `NamedLoot` by tier (1 rare, 2 stronghold/dungeon, 3 world + cache, 4 pinnacle).
- Entry without touching Game.cs: subscribe to `World.Event("area")` from a `[ModuleInitializer]` (to be verified in a run).

## NEXT STEPS (in order)
1. `NamedHub`: module-initializer boot (`World.Event`, `World.Damage`, `World.MonsterKilled`), `Spawn(def, pos, level)`
   (clone def, tier, scale, rim, numbers, affixes for rares, `Brain = new BossBrain(m)` when a kit is registered),
   `Slain` (NamedLoot, `NamedRecords.Count`, save, toast), `FireNear` (needs a GroundZone accessor hook).
2. Area hook: overworld → replace the stronghold placeholder mini-boss in `Level.StrongholdMobs` with `sh_<region>`,
   roll the 4 rares (placement rules `road / camp_north / open / grove / ledge / far / nook / shore`, marker kind "boss"
   hidden until seen); side dungeon → replace `Level.BossMon` placeholder with `db_<dungeon>` + `Level.SetBoss`;
   town → pinnacle altar near the rift stone.
3. `NamedAreas`: `ladder:<n>`, `field:<wb id>`, `arena:<named id>` via `AreaKind.Arena`. Hooks needed (one line each,
   `// NAMED-HOOK`): `Game.IsKnownArea`, `Level.Resolve`, `ZoneDb.AreaName`; plus accessors `Projectile.FromPlayer`,
   `GroundZone` owner / element / radius; plus `case "named"` in `TestSuite.Root`.
4. Encounter scripts: `NamedRares.cs` (28), `NamedStrongholds.cs` (7), `NamedDungeons.cs` (14), `NamedWorld.cs` (4),
   `NamedPinnacle.cs` (8), each implementing `static partial void Register…(Registry r)` of `NamedDb`.
5. `WorldBossEvent` in WorldEvents.cs (gateway in the region overworld, interval from `SaveData.PlayTime`, cache once per
   interval via `SaveData.NamedCache`), `NamedLadderUi.cs` (minimal list panel, `Ui.Register`).
6. Append the 19 intro lines to data/vo_lines.json (ids `<named id>_intro`; texts are already in `NamedDb.Stage`):
   sh_wastes, sh_frost, sh_crypt, sh_mire, sh_sands, sh_sanctum, sh_abyss, wb_barrowbearer, wb_firstbearer,
   wb_daylessdisc, wb_breathofsilence, pin_1_gravedigger … pin_8_osaros.
7. `NamedSuite.cs` (partial `TestSuite`: `--suite=named[:filter]`) and `--named=<id>`; run all 61, tune `NamedDef.Hp`
   to the kill-time targets; smoke-test two regions of the campaign with their side dungeons.

## Hooks in other people's files
None inserted yet.

## Status per encounter
None implemented, none tested.
