# Named encounters — working log (named-encounter engineer)

Scope: 28 rare elites, 7 stronghold bosses, 14 side-dungeon bosses, 4 world bosses, 8 pinnacle bosses (61 new);
the 7 region bosses stay as they are (BossBrain kits). Design: BESTIARY.md section 4, data: data/bestiary.json `named`.

## STATE 2026-11-19 ~00:50: PAUSED on the lead's order — compiles (check.ps1: 0 errors), nothing running

**Live in normal play** (feature flags in `NamedAreas.cs`): `LiveRares = true` (28 rares roll 30 % per overworld visit),
`LiveStrongholds = true` (the 7 stronghold leaders are the named bosses). **Off**: `LiveDungeons = false` (side
dungeons keep the old placeholder boss), `LiveWorld = false` (no world-boss gateways), `LiveLadder = false` (no Ember
Stair altar in town). With these off the hooks are inert for normal play: the Level/Quests hooks only act on "rift~"
area ids, the TestSuite hook only on `--suite=named`. Region bosses untouched.
All 61 stay reachable for tests: `--named=<id>`, `--area=rift~arena:<id>`, `--suite=named[:filter]`.

Evidence for what is live: suite runs 2+3 (every encounter spawned, killed, all required mechanics fired; 0 script
errors); campaign smoke (`--campaign --level=30 --god`, stopped at the pause after ~70 s of play-log): rare
gallowsfather killed in the wastes overworld, sh_wastes killed → 斷燈哨站 liberated, boss_gravedigger killed, sh_frost
killed → 凍骨關 liberated; 0 errors. Log `Y:\tools\named_campaign_out.txt`. The full campaign was NOT completed.

### NEXT STEPS (resume)
1. Full campaign smoke test (`Y:\tools\named\smoke.sh`, first line) to the end — then the strongholds are proven.
2. Turn on `LiveDungeons` and run the tour line of smoke.sh (wastes_d1/d2, frost_d1/d2); check the boss door, reward
   chest, portal and `dg_*` flags with the named boss.
3. `LiveWorld`: run the wbroute line (gateway appears, field loads, cache once per interval).
4. `LiveLadder`: windowed shot of the panel (smoke.sh last line), climb rung 1 → 2.
5. Run 4 of the suite for the ten retuned after run 3 (hamo, whiteshadow, lastrun, namelessking, dafu, firstcantor,
   vesna, db_wastes_d1, db_wastes_d2, pin_7) — their last measured times are before the last tuning.
6. Windowed screenshots of a fight (name plate, boss bar, telegraphs): smoke.sh shot1.

### Files (mine)
| file | what |
|---|---|
| `NamedData.cs` | definitions, staging, kill records (`SaveData.NamedKills/NamedCache`), registry (`"?key"` = optional mechanic) |
| `NamedEncounter.cs` | controller + block library (unchanged design; + `OtherDamaged`, roster-aware `AddDef`) |
| `NamedBlocks.cs` (new) | more blocks: Hold/Drive/Cast (borrow a body), Hide, Expose (engine stagger), StunMon, KillNamed/HurtNamed/HealNamed, Behind/InFront, LaneFan, HeroInLane, Orbit/SpinOrbs, NearestAdd/Prop, Mark |
| `NamedHub.cs` | module-initializer boot on World.Event/Damage/MonsterKilled; `Spawn` (clone def, tier, numbers, affixes, brain); `Slain` (loot by tier, records, toast, save); reflection readers for GroundZone / Projectile private fields (FireNear, SnuffHeroZones, AllShots, SetShotDir) |
| `NamedAreas.cs` (new) | area hook (stronghold leader, rares by spot rule, dungeon boss, town altar, encounter areas `rift~arena:<id>`, `rift~wb:<id>`, `rift~pin:<n>`), `Resolve` for Level.Resolve, `--named=<id>[@route]`, `NamedDriver` (deferred steps) |
| `NamedKits.cs` (new) | `NK` factories for BossMoveDef / BossKit |
| `NamedRares.cs` (new) | 28 rare scripts |
| `NamedStrongholds.cs` (new) | 7 kits + scripts (`BossScript` base) |
| `NamedDungeons.cs` (new) | 14 kits + scripts |
| `NamedWorld.cs` (new) | 4 kits + scripts |
| `NamedPinnacle.cs` (new) | 8 kits (Derive of region kits; rung 7 own kit) + scripts; `NamedOverlay` (screen veil: whiteout / dark with a hole round the hero) |
| `NamedLadderUi.cs` (new) | panel `named_ladder` (registered on first use) |
| `WorldEvents.cs` | + `WorldBossEvent` (timed: 1200 s of play per interval; gateway in either region overworld; cache once per interval) |
| `TestSuite.Named.cs` (new) | `--suite=named[:filter[:class[:tier]]]` |
| `BossBrain.cs` | unchanged since the seam |

### Hooks in other people's files (one line each, marked `// NAMED-HOOK`)
- `Level.cs` Resolve(): `if (NamedAreas.Resolve(this, areaId, seed) is { } named) { AreaId = named.AreaId; return named; }`
- `Quests.cs` CanEnter(): `if (NamedAreas.IsNamedArea(areaId)) return true;` (before the rift line)
- `TestSuite.cs` Root(): `case "named": yield return NamedSuite(); break;`

### Data
- `data/vo_lines.json`: 19 entries appended (`<id>_intro` for sh_* ×7, wb_* ×4, pin_* ×8); copy of the old file: `Y:\tools\named\vo_lines.before.json`.

### Test switches
- `--named=<id>`: after the first area loads, travel to the test arena `rift~arena:<id>` (also `--area=rift~arena:<id>` works directly).
- `--named=<id>@route`: to the encounter's real place (rare forced out in its region; world boss gateway forced).
- `--suite=named[:all|rare|stronghold|dungeon|world|pinnacle|id,id|prefix[:class[:tier]]]` with `--speed=6`, headless.

### Running / background
- Full suite run 1 started 2026-11-18 ~23:25 (`run.ps1 -Tag named_all`, output `Y:\tools\named\suite_all_1.txt`,
  stdout `Y:\tools\named_all_out.txt`). run.ps1 kills the game at its timeout; nothing else left running.

## Results
(see the report section at the bottom; filled in after the suite)

## Honest differences from the design (engine limits; closest version built)
General
- Base types the roster does not have yet (Bestiary.Live = false) use the stand-in table in NamedData (`StandIns`);
  adds named after future types (dusk_hound, lamp_post, peat_sleeper, wax_sealed, cinderling, …) fall back to existing
  monsters through `NamedEncounter.AddDef` (it asks `Bestiary.Local` first once Live is on).
- Elites have no stagger meter: where a rare's design says "stagger", a stun/freeze or one blow of 7-12 % of its life counts.
- "Hidden" (stealth, burrow, ascend, submerge) = model hidden + Untargetable; no fog/blizzard/water rendering.
- Reflection: shots near the mirror are destroyed and a new monster bolt is fired back (not the same projectile).
- Screen effects (whiteout, the dark) are a full-screen veil (`NamedOverlay`, under the HUD); telegraphs under a white
  veil are dimmed, not kept fully visible.
- No new arena geometry: lava channels, boardwalks, conveyor lines, gantries, rails and water are marked with props,
  rings, patches and ground zones on the standard arena layout.

Per encounter (F = the designed mechanics as written; S = simplified, the difference named)
| id | F/S | difference from the design |
|---|---|---|
| rare_wastes_oldhowl | F | hounds = rime_stalker stand-ins until dusk_hound exists |
| rare_wastes_nightwatch | S | "stagger" = stun/freeze or one blow ≥ 8 % of his life during the 1.2 s inhale |
| rare_wastes_ashbride | F | ash-fall = two overlapping zones (weaken, slow) |
| rare_wastes_gallowsfather | S | hoist = 2 s root with damage ticks (no lift animation); cages on any fire hit or burning (9 s cooldown) |
| rare_frost_hamo | F | rank of four Frostbound at 60 % life, only while ≤ 4 adds live (balance) |
| rare_frost_whiteshadow | S | no blizzard gusts; revealed by the hero's ground effects or fire under it (not by every area hit) |
| rare_frost_lastrun | S | walks to the farthest road cell and back; kegs are timed blasts with a jar look (no muleteer) |
| rare_frost_thousandyear | F | flickers = six orbiting orbs; breaks into six ice_wraith stand-ins at 15 % life |
| rare_crypt_belak | S | wall = 96 % frontal damage reduction + projectile block per member; "staggered" = stunned, a blow ≥ 12 %, or a member killed |
| rare_crypt_veilwife | F | ascended = hidden/untargetable; cocoon = breakable prop |
| rare_crypt_namelessking | F | |
| rare_crypt_hundredtears | F | split = two mourner champions (wraith stand-ins) beside the named one |
| rare_mire_olddrum | S | swallow = hero hidden and stunned 2 s, then flung 8 m; belly burst 15 % (16 s cooldown) |
| rare_mire_guidelight | S | no fog; it walks a four-leg route ahead of the hero, two lamp-posts (mire_hag stand-ins) wake per cluster |
| rare_mire_weepingriver | S | no boardwalk / channel; surfaces 9 m from the hero to inhale, bites, submerges |
| rare_mire_dafu | S | three mud patches placed round her home; haul = drag 6 m toward the nearest |
| rare_sands_ogan | S | reflection = shot destroyed + new lightning bolt back; frontal 85 % reduction |
| rare_sands_hundredshell | S | three gear props are the brass to re-armour from; the shed shell is a heap (does not conduct storm strikes) |
| rare_sands_tesa | S | "brass objects" = four lightning-rod props + sands-family monsters near the strike |
| rare_sands_thirteenth | S | discs = orbiting rings that cut; elite "stagger" = a blow ≥ 7 % (0.8 s stun) |
| rare_sanctum_firstcantor | F | |
| rare_sanctum_everquiet | S | no hit zones: "the legs" = hits from behind/close build the stagger meter (12 %) |
| rare_sanctum_kolen | S | no Lit Wick type yet: Wax-family monsters he brushes are enraged and lit |
| rare_sanctum_vesna | F | |
| rare_abyss_foreman | S | no rails: three shock lanes; thralls = fiend stand-ins |
| rare_abyss_firstborn | F | cinderlings = imp stand-ins; the call home lasts 8 s |
| rare_abyss_whitetide | F | rib-walkers = wraith stand-ins |
| rare_abyss_slagferry | S | no lava channel: he keeps to his home spot (half damage from melee there); scoop = drag to an island ringed by fire |
| sh_wastes | F | the beacon relights through the normal liberation once he dies |
| sh_frost | F | |
| sh_crypt | F | |
| sh_mire | F | plates absorb blows ≥ 1.2 % of his life |
| sh_sands | F | |
| sh_sanctum | S | the dormant Wax-sealed stand in a ring at the arena edge (husk stand-ins); grab = root 1 s |
| sh_abyss | F | |
| db_wastes_d1 | F | parts = two bloated-husk champions; each re-huddle halves the parts' life (so a slow party still finishes) |
| db_wastes_d2 | F | a stumpling (imp stand-in) next to a surfaced root lights it |
| db_frost_d1 | S | the four braziers stand lit (the prisoners are freed before the door opens) |
| db_frost_d2 | S | pillars are props; "perched" = held on the pillar top, striking from there |
| db_crypt_d1 | S | effigy_knight/knight stand-in model (Rig F sarcophagus not built) |
| db_crypt_d2 | F | |
| db_mire_d1 | S | no water rendering: islands are marked rings that shrink, water = slow outside them |
| db_mire_d2 | F | |
| db_sands_d1 | S | static stand-in model; conveyor lines are three gear props with their delivery points |
| db_sands_d2 | S | the real one's tell = rim glow + a bell sound (no separate eye glow) |
| db_sanctum_d1 | S | no desaturation: a ring closes on the hero + warning text; "a skill" = any action the hero starts |
| db_sanctum_d2 | F | |
| db_abyss_d1 | S | stand-in model; gantries are two marked slabs, slag channels = permanent fire zones round the rim |
| db_abyss_d2 | F | |
| wb_barrowbearer | S | stand-in model (rotspawn ×3.6, no chapel); pits = permanent slowing zones |
| wb_firstbearer | S | stand-in model; straps / cage door are props; the carried hero is held beside him |
| wb_daylessdisc | S | stand-in model; the shadow wedge is drawn as a repeating cone mark |
| wb_breathofsilence | S | stand-in model (wraith ×4); the coil is a ring of posts round the hero |
| pin_1_gravedigger | S | buried = rooted + untargetable, eight attack starts dig out (8 s at most) |
| pin_2_rimelord | S | whiteout = white veil at 70 % |
| pin_3_bishop | F | |
| pin_4_mire_mother | S | tide = a water patch over the arena + rings for the root-islands; "roots take double" = she takes double at low tide; elder leeches = rotspawn |
| pin_5_sand_tyrant | F | mirror-guards = brass_sentinel champions |
| pin_6_grand_hush | S | the staff = four orbs above him; the choir's shield = 30 % less damage to him |
| pin_7_firstkeeper | S | "three skills of the hero's class" = three moves of the class's archetype (melee / archer / caster); model hero_lampknight |
| pin_8_osaros | S | the dark = black veil with a hole round the hero; echoes are short versions of each rung's signature |

## Suite runs (headless, ×6, emberblade bot, rare gear; pinnacle tier 2 + paragon 50)
- Run 1 (2026-11-18 23:25, before tuning): 58 of 61 ran (stopped during pin_4); 0 script errors. Log `Y:\tools\named\suite_run1_log.txt`.
- Run 2 (2026-11-18 23:46, after tuning 1): all 61 — 217 checks pass, 25 fail (all kill-time windows or optional-turn
  mechanics), 0 errors, 0 script failures. Log `Y:\tools\named\suite_run2_log.txt`, rows `Y:\tools\named\run2_rows.txt`.
- Run 3 (queued 2026-11-19 00:05): the 29 that were out of window or changed after run 2. Log `Y:\tools\named_run3_out.txt`.
- Then `Y:\tools\named\smoke.sh` (campaign, two-region tour with side dungeons, world-boss route, two windowed screenshots).

Latest measured row per encounter (run 2, or run 3 where re-run): `Y:\tools\named\latest_rows.txt` — 51 of 61 inside
their window; out: rare hamo 61 s, whiteshadow 52 s, lastrun 48 s, firstcantor 52 s, vesna 48 s (slow), namelessking
20 s, dafu 17 s (fast), db_wastes_d1 92 s, db_wastes_d2 93 s (slow), pin_7 93 s (fast) — all retuned since, not re-measured.
Balance changes after run 3 not in the table: Hamo musters on every second blast (design: every blast), White Shadow
pounces every 4 s. Pinnacle tests ran at world tier 2 with paragon 50; many god-saves there (bot does not dodge).
Test saves slot9* / slot0* moved to `%APPDATA%\AshenAbyss\saves_test_backup\` at the pause.
