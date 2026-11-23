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

## RESUMED 2026-11-20 (test slot 91 only)
- Full campaign (`--campaign --level=30 --god`, slot 91): **campaign finished** at play 384 s — all 7 named strongholds
  killed and liberated, all 7 region bosses killed, 0 errors. Log `Y:\tools\named\campaign_full_log.txt`.
- Side dungeons (`--named-live=dungeons --tour=wastes_d1,wastes_d2,frost,frost_d1,frost_d2`, unassisted bot):
  db_wastes_d1 24 s, db_wastes_d2 11 s (hero had levelled to 16-17), db_frost_d1 141 s (the bot never lures Od into
  brazier light: regrow ×11 — a player will), db_frost_d2 44 s; each: door → kill → chest → "此地已淨化" → portal; 0 errors.
  → `LiveDungeons = true`. Log `Y:\tools\named\tour_log.txt`.
- World boss: `--named=wb_barrowbearer@route` opens the gateway at the far side of the wastes; the field
  `rift~wb:wb_barrowbearer` fought by the unassisted bot: killed 173 s, cache claimed once (interval 0), bot left through
  the portal back to the wastes. → `LiveWorld = true`.
- New test switch `--named-live=dungeons,world,ladder|all` (turns a group on for one run).
- Ladder: `LiveLadder` stays false until the panel screenshot is checked (`Y:\shots\named\ladder.png`).
- Running now: `Y:\tools\named\resume.sh` (ladder shot, fight shot, suite run 4 of the 10 retuned; retries while the
  run lock is busy; writes `Y:\tools\named\resume_done.txt` at the end). Stop: kill the bash running resume.sh and
  any Godot with `--slot=91`.
- Ladder panel (windowed, tier 2, lv50): `Y:\shots\named\ladder.png` checked by eye — framed, 8 rungs readable, rung 1
  open, 2-8 closed; bug fixed after the shot: the tier line was tinted red (now gold). The rung route (enter rung 1 →
  kill → portal to rung 2) runs in `Y:\tools\named\resume2.sh` after resume.sh.

## STATE 2026-11-21 ~01:00: PAUSED again (0.3.0 release) — compiles (check.ps1: 0 errors), nothing of mine running
Live: `LiveRares`, `LiveStrongholds`, `LiveDungeons`, `LiveWorld` = true. Off: `LiveLadder` = false (panel checked on
screen; the rung route — enter rung 1, kill, portal to rung 2 — never got the run lock). vo_lines.json: 207 entries,
all complete, 19 named intros.
New since the last pause: `ZoneDb.AreaName` hook (MonstersData.cs, one inserted line) so encounter areas show their own
names; ladder tier line colour fix; `--named-live=` switch; fight shots `Y:\shots\named\sh_frost_1/2.png` (boss bar
and name read correctly; the banner prop is plain).
Suite run 4 (partial, stopped at the pause; `Y:\tools\named\suite_run4_partial_log.txt`): hamo 12.5 s (FAST),
whiteshadow 10.5 s (FAST), lastrun 29.8 s ok, namelessking 25.6 s ok, dafu 21.6 s ok but its required "nets" mech did
not fire (the bot stayed in melee: nets need 3.2 m+). Not re-measured: firstcantor, vesna, db_wastes_d1/d2, pin_7.
NEXT: rung route run (resume2.sh, without --named) → LiveLadder; re-tune hamo / whiteshadow up (≈ ×2 life), make dafu's
nets usable at melee range or mark it "?nets"; finish run 4. The run lock is heavily contended — expect long waits.

## RESUMED 2026-11-22 (0.4.0 finish: ladder ON, fight lengths, bot hints, stand-ins) — test slot 91
Running: `Y:\tools\named\batch3.sh` (bash, background; replaced batch1/2 — their runs hit the run lock and someone
else's broken build; each step `q2.sh` waits while the user plays, then run.ps1, retrying on a busy lock or a broken
build) → AI-pilot no-god rungs 2-8, full suite `--bestiary`, full suite (stand-ins) after the retune, ladder shots ×3 text
sizes. Per-step output + game log copy: `Y:\tools\named\runs\<tag>.txt/.log`; `batch3_done.txt` at the end.
Stop: kill the bash running batch3.sh / q2.sh (by its PID) and any Godot started with `--slot=91`. Resume: rerun the missing lines.
- Baseline (before changes): AI pilot (`--pilot-test=boss`, no god, lv50 p50 tier 2 rare gear) beat rung 1 in 329 s, 0 deaths.

### Bot hints published (id → hint) — for AI-PILOT tests (`NamedScript.BotTarget` / `BotStand` / `BotPassive`, meaning unchanged)
New on 2026-11-22 marked **new**. T = BotTarget (hit this first), S = BotStand (stand here), P = BotPassive (leave the named one alone).
| id | hint |
|---|---|
| rare_wastes_ashbride | T the ash pile after her first reform (strike it within 3 s) |
| rare_crypt_belak | S behind the shield wall while it is closed |
| rare_crypt_veilwife | T the silk cocoon round the hero |
| rare_crypt_namelessking | S in front of him (his back is immune) |
| rare_mire_guidelight | P while it leads + S 5 m behind it on its route |
| rare_sands_ogan | S behind the mirror shield (closed) |
| rare_sands_hundredshell | T the brass heap he walks to re-armour from (within 9 m) |
| rare_sanctum_firstcantor | S in her drifting halo while barriers stand |
| rare_sanctum_vesna | T the planted snuffer cone (hero within 6 m of it) |
| rare_sanctum_everquiet | **new** S close to the bell (the legs are hit from close / behind) when farther than 3 m |
| rare_abyss_firstborn | **new** T the cinderling nearest the hero (< 10 m) while it calls them home |
| sh_frost | T the garrison banner |
| sh_sands | T the storm pylons once "shock" has fired |
| sh_sanctum | T the wax shell round the hero |
| sh_abyss | T the ember crystal he walks to eat (< 8 m of the hero) |
| db_wastes_d1 | T the bloated parts when the household sits at its floor |
| db_wastes_d2 | T a surfaced root (not while a root burns) |
| db_frost_d1 | S the nearest lit brazier while Od's ice shell stands outside the light |
| db_frost_d2 | T the pillar the matriarch perches on |
| db_crypt_d1 | **new** T its wraiths while the lid is shut (the last one forces the lid open) |
| db_crypt_d2 | T the bone stack it walks to restock from |
| db_mire_d1 | S the nearest island after its first strike while the eel is under |
| db_sands_d1 | T the drive gears while the core is shut |
| db_sanctum_d2 | **new** T a planted clapper < 5 m from the hero (two or more planted) |
| db_abyss_d1 | S a side gantry during BLOW |
| db_abyss_d2 | **new** S the edge (2.6 m) of his nearest leftover flame during HUSH, unless already lit |
| wb_barrowbearer | **new** T a coffin < 7 m from the hero before it opens |
| wb_firstbearer | T the hod straps when exposed / the cage door when carried |
| wb_daylessdisc | T the armed storm-jar; S behind a fallen mirror-segment before False Dawn |
| wb_breathofsilence | T the glowing scale; S out of the swallow / at an unlit brazier |
| pin_2_rimelord | S the nearest lit brazier when the cold meter passes 35 % |
| pin_3_bishop | T the urn-bearer by the next urn (or the urn) when she sits at her life floor |
| pin_4_mire_mother | S the nearest root-island at high tide |
| pin_5_sand_tyrant | **new** T the nearest mirror-guard (< 14 m) — ranged classes only (hunter, astromancer, soulbinder, pactbinder) |
| pin_6_grand_hush | T a choir member |
| pin_7_firstkeeper | S the central brazier while it burns (farther than 4 m) |
| pin_8_osaros | **new** S inside the drifting Extinguish ring; S in the shadow wedge during the tyrant echo |
- 11:51: batch3 (after its rung-6 run) and batch4 were replaced by `Y:\tools\named\batch5.sh` (bash pid 752619):
  suite `--bestiary`, suite (stand-ins), ladder shots ×3, then the FINAL no-god AI-pilot check of all 8 rungs
  (`--hands=2h`, tags `final_pin<n>`); `batch5_done.txt` at the end. Table: `Y:\tools\named\table.sh suite6.log suite6_bestiary.log`.
- Changes so far (2026-11-22): Hamo musters every blast again (not while 3+ adds stand), blast every 9 s; White Shadow
  ground reveal only after 1 s hidden; Dafu nets at any range; Night Watch interrupt needs 12 % (was 8 %); named rares
  never roll armored + shielded together; Rimelord no longer clears the hero's post-freeze CC immunity (chain freeze,
  5 pilot deaths); Osaros' Extinguish ring drifts by run progress (was per frame); sh_frost banner: user decision
  "cap and slow" — 4 per muster every 22 s (was 15 s), at most 6 banner-mustered alive; life retune (NamedTuning.cs).
- Pilot (no god, random rare gear) before the pinnacle retune: r1 killed 329 s 0 deaths; r2 5 deaths (chain freeze);
  r3 killed 631 s 0 deaths; r4 not killed in 720 s (33 % left, 1 death); r5 not killed in 720 s (0 deaths) → pinnacle
  life cut to ~0.55-0.75 (the pilot lands ~35 % of the god-mode bot's damage).
- 12:40 the lead: the ladder is end-game content for strong players — do NOT calibrate it to the pilot. Restored pinnacle
  life (formula / pin_2 28, pin_3 50, pin_5 60, pin_6 50, pin_8 55; pin_7 50 stays — it ran FAST at 36 with the god bot)
  and damage (×0.85); kept the Rimelord fix. Acceptance: rungs 1-3 beaten by the AI pilot without god (campaign --gear);
  rungs 4-8 by the --gear suite bot without god at end-game gear. New suite options: `--suite=named:<filter>:<class>:<tier>:nogod[:legendary]`
  (nogod: deaths counted, potions at < 45 %, respawn after 3 s; legendary: the --gear set re-rolled legendary at power 600).
  Queue now `Y:\tools\named\batch6.sh` (bash pid 758783; the suite --bestiary run in flight is its own q2.sh, pid 753029):
  suite, ladder shots ×3, `final_pin1-3` (pilot), `final_suite_pin48` (bot, nogod, legendary); `batch6_done.txt`.
- Suite `--bestiary` (suite6_bestiary, 12:40): 0 ERR, all 61 spawned on their real base types (only pin_7 keeps a
  stand-in: hero_lampknight), all 8 rungs killed in window. Two stalls found and fixed: 千年凍燭 (flickers absorbed
  every hit — with its drain the hero landed < 0.6 hits/s; now a flicker takes 75 % of a blow, DoT ticks pass) and
  窖中一家 (a bloated part bursting by itself opened the 6 s window → re-huddled forever; the window now opens only when
  the mass is at ≤ 6 %). Queued: `batch7.sh` (pid 762210) = suite `--bestiary` again after batch6 (`suite7_bestiary`).
- 13:30 suite6 (stand-ins) done: 0 ERR; guidelight ALIVE (armored + vampiric) → at most ONE of armored / shielded /
  vampiric on a named rare; Keeper of Tongues 158 s (the clapper hint chased every clapper) → hint only for a clapper
  within its 3.6 m ring of the hero, clappers 1.2 % life (was 2.5 %); life retune from the mean of suite6 + suite6_bestiary.
  Queued `batch8.sh` = suite7 (stand-ins) after batch7 — the final table = suite7 + suite7_bestiary + final pilot/bot ladder runs.
- 14:00 BESTIARY's `named:rare --bestiary` (Y:\tools\roster\logs\named_rare.log) + mine → fixes: named rares never keep an
  Exploder AI (柯倫's base 燃芯者 blew himself up in 6 s); ash pile 12 % (was 7 %; a strong hero broke it before any reform);
  bestiary-weighted retune (oldhowl 28, kolen 12, dafu 8.5, foreman 18).
- 14:30 queue consolidated: `Y:\tools\named\batch10.sh` (bash pid 774340) replaces batch6-9 (killed by their own PIDs;
  the shot_ladder_1 run in flight finishes by itself). Order: final_pin1-3 (pilot, no god), final_suite_pin48 (suite
  bot, no god, legendary), suite7_bestiary, suite7, shot2_ladder_0/1/2 (`Y:\shots\named\ladder2_ts<n>.png`);
  `batch10_done.txt` at the end. Ladder panel: frame 1000 design px tall, tighter rows (8 rungs at 標準; 大/特大 scroll).
- 17:05 rung 2 pilot (final_pin2): 6 deaths — the script's chill ticks (every 0.5 s while cold) stacked into the game's chill→freeze on top of the meter's own freeze. Now: the meter is the one freeze (2 s, fills in 11 s), cold makes the hero slowed instead. Rerun queued: batch11.sh (pid 794818) → final_pin2b after batch10.
- 19:25 ladder acceptance so far: r1 pilot no god killed 308 s (1 death, then won; earlier run 329 s 0 deaths); r2 pilot 6 deaths
  (fixed, rerun queued); r3 pilot no god killed 801 s 0 deaths; suite bot no god legendary power 600: r5 82.7 s 0 deaths,
  r6 134 s 0, r7 117 s 0, r4 killed on the 4th try (3 deaths: high-tide poison after her pull → 1.5 s grace added),
  r8 killed on the 2nd try (1 death to a knight; that roll had life 1536). Rerun r4 + r8: batch12.sh → final_suite_pin48b.
- 21:15 final suites done (suite7 stand-ins 20:25, suite7_bestiary 19:35): all 61 killed in both modes, 0 ERR; table in
  `Y:\tools\named\fight_table.md`. Consistently out in BOTH modes → adjusted after: sh_abyss 56→42, thousandyear 9→12,
  ogan 11→15, db_wastes_d2 5→7, db_abyss_d1 36.5→42; check queued: batch14.sh → suite8_fix / suite8_fix_bestiary.
  Ladder panel: world tier moved into the title row (standard-size reshoot: batch13 → ladder3_ts0.png).
- 22:40 the lead opened the priority lane for runs <= 5 min: batch10-14 stopped by their own PIDs; all remaining runs in `Y:\tools\named\batch15.sh` (bash pid 857203, ASHEN_RUN_PRIORITY=1): shot3_ladder_0/1/2, final_pin2b, final_suite_pin48b, suite8_fix, suite8_fix_bestiary; `batch15_done.txt`.
- 23:15 recheck (suite8_fix / _bestiary): thousandyear ALIVE 1 % with the roster on (armored roll; flicker now takes 60 %, life 10), ogan 13, sh_abyss back to 34 (42 still 95-102 s), db_abyss_d1 38; 枯根之心 (db_wastes_d2) ALIVE in stand-in mode — it is gated on a burning root: roots now 15 % life (a fire hit killed them before they lit) and the hint sends the hero at a stumpling beside a surfaced root (any class can light one); stumpling checks no longer depend on the type id (roster reskins). Last recheck: batch16.sh (pid 864190) → suite9_fix / suite9_fix_bestiary.

## STATE 2026-11-22 23:30: DONE for 0.4.0 — compiles (check.ps1: 0 errors), nothing of mine running
All five groups live: `LiveRares`, `LiveStrongholds`, `LiveDungeons`, `LiveWorld`, **`LiveLadder` = true**.
Left running, NOT mine to stop: `tail -f Y:/tools/named_all_out.txt` (pid 185850, from the 2026-11-18 session) — the lead may close it.

### Ladder (燼階) — ON
- Route suite `--suite=named:ladder` (named-live): altar in town → panel (rung 1 open, 2 closed) → closed rung refused →
  「挑戰」 → rung 1 → kill recorded at tier 2 → rung 2 opens → 「登上燼階第二階」 portal → rung 2 boss stands; all PASS, 0 ERR.
- No-god acceptance (the lead's rule: r1-3 AI pilot with campaign `--gear`; r4-8 the `--gear` suite bot at end-game gear):
  | rung | check | result |
  |---|---|---|
  | 1 葛魯姆 | pilot, no god, rare power 560 | killed 308 s (1 death, then won); earlier run killed 329 s, 0 deaths |
  | 2 赫爾蒙 | pilot, no god | after the cold-meter fix: killed 378 s (1 death, then won); before: 6 deaths, not killed |
  | 3 瑪爾薩 | pilot, no god | killed 801 s, 0 deaths |
  | 4 艾絲珂 | suite bot, no god, legendary power 600 | killed 94.6 s, 0 deaths, 5 potions (after the soak grace; before: won on the 4th try) |
  | 5 伊薩克 | same | killed 82.7 s, 0 deaths |
  | 6 賽倫 | same | killed 134.4 s, 0 deaths |
  | 7 伊蕾 | same | killed 117.3 s, 0 deaths |
  | 8 奧薩洛斯 | same | killed 169.7 s, 0 deaths (rerun; the first run's roll had 1536 life: won on the 2nd try) |
- Panel `named_ladder` checked by eye at 標準 / 大 / 特大: `Y:\shots\named\ladder3_ts0/1/2.png` — all 8 rungs fit at 標準,
  大/特大 scroll; each row: rung + name, one zh-TW line on the fight, record / 「先攻克第N階」, 挑戰/封閉; tier in the title row.

### Fight lengths — `Y:\tools\named\fight_table.md` (god-mode suite bot, emberblade, rare gear of the right level)
suite7 (roster off, stand-ins) and suite7_bestiary (roster on): 61/61 killed in both, all required mechanics fired with the
roster on, 0 ERR. Re-measured after the last adjustment (suite9_fix / _bestiary, both modes): thousandyear 28.8 / 51.3 s,
ogan 34.2 / 33.9, sh_abyss 59.2 / 48.4, db_wastes_d2 39.9 / 21.9 (no longer stalls), db_abyss_d1 48.1 / 56.5.
Rares keep a spread (two random elite affixes; now at most one of armored / shielded / vampiric).

### Files changed this session (all mine)
NamedAreas.cs (LiveLadder on; suite gating; NamedData.cs ends unchanged — the pinnacle damage cut was reverted),
NamedDungeons.cs (hints Sarcophagus / Keeper / Half-Burnt / DeadRoots, Household window, DeadRoots roots + roster-proof
stumplings, clapper life), NamedHub.cs (affix filter, no Exploder AI on named rares), NamedLadderUi.cs (rebuilt panel),
NamedPinnacle.cs (Rimelord cold meter + post-freeze immunity, Osaros hints + drift fix, Tyrant hint, Mire Mother soak grace,
RangedHero), NamedRares.cs (Hamo, White Shadow, Dafu, Night Watch, Everquiet / FirstBorn hints, ThousandYear flickers,
ash pile), NamedStrongholds.cs (sh_frost cap and slow), NamedTuning.cs (life), NamedWorld.cs (BarrowBearer hint),
TestSuite.Named.cs (`ladder` route, `:nogod[:legendary]`, deaths/potions/respawn, stand-in column).
No new voice lines (no new speaking text). Tools: `Y:\tools\named\q2.sh`, `batch15.sh`, `batch16.sh`, `table.sh`, `runs\`.
