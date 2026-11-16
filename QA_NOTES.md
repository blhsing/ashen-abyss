# QA notes — 燼淵 Ashen Abyss (running list; newest state at the bottom of each section)

Format: bug → cause → fix → verified?

## Harness (added by QA; all optional, old switches unchanged)
| switch | what it does |
|---|---|
| `--speed=N` | game time scale N (0.05–20); hit-stop is skipped. The bot thinks in game time, so a ×4 run plays the same game faster |
| `--suite=skills[:classes[:levels[:area]]]` | every skill of each class at each level (default 1,12,25,50): 3 casts on target dummies, per-skill line with casts / fired / hits / damage / resource / pips / cooldown / minions / form / buffs + expiry |
| `--suite=systems` | items (all rarity × class × kind), loot rolls, equip, salvage → codex, masterwork, temper, enchant, imprint, sockets / gems, stash, vendors, gamble, junk, skill points / upgrades / respec / spec, paragon, world tiers |
| `--suite=objects[:area]` | loot of every rarity on the ground + pickup, gold / materials / globes, chests q0-3, all shrines, altar, breakables |
| `--suite=save` | 8 slots round trip, slot list, .bak fallback, corrupt / hand-edited saves, hardcore death, normal death + respawn, save → menu → load, settings (user's real slots are set aside and restored) |
| `--focus` | bot does what the place is for: dungeon objective → boss → portal; overworld stronghold → events |
| `--campaign` | bot follows the main quest (talk, travel, stronghold, story dungeon, boss) through all acts |
| `--allskills` | bot rotates every unlocked skill through the bar |
| `--hardcore`, `--slot=N` | `--new` character is hardcore / lives in slot N |
| `--die-at=s` | lethal blow at harness time s (death panel, respawn, hardcore marking) |
| `--prtsc-at=s`, `--f12-at=s` | PrtSc (through the low-level hook when the window has focus) / F12 screenshot paths |
| `--ui-tour[=ids]` | opens every panel in turn, one screenshot each (`<shot>_<panel>.png`) |
| `--paragon=N` | paragon level N at level 50, auto-allocated |
| `--tour=a,b,c` | after the starting area, visit these in turn (implies `--focus`); one result line per area; quits at the end |
| `--bossrush` | in dungeons, open the boss door without doing the objective and fight the boss |
| `--seed=N` | fixed world seed (same overworlds between runs that are compared) |
| `--perf-off=hud,ui,anim,world` | measurement only: switch parts off to see their cost in the perf line |
| `--suite=dps[:classes[:levels]]` | the bot's rotation on one dummy (30 s) and on six (20 s): damage per second vs `Balance.ExpectedDps`, per-skill shares |
| `--suite=anims` / `anims:static` / `anims:shots-town\|shots-heroes\|shots-monsters` | the animation audit (see below), its 10-second static part, close-up pictures |
| `--suite=anims:monsters` | only the live monster part of the audit (all 36 monster and minion types) |
| `--slot=90..99`, `--load=90..99` | private harness save slots, never listed by the menu: chain one campaign over several short runs |
Bot summary now ends with per-skill cast / hit / damage counts, time-to-kill per monster tier, damage taken per minute and level-up times.
`--quit-at` and `--shot-at` count from the moment the first area is on screen (loading excluded — about 6 s headless, 18 s windowed).
Wrappers: `Y:\tools\qa\qarun2.ps1 -GameArgs "<args>" -Timeout n -LockWait n [-Headless]` queues on the run lock for at most LockWait seconds and then
calls run.ps1 in-process (one bounded foreground command); the older `qarun.sh` / `qawait.sh` probe the lock and were starved when it is contended.

## Bugs

### Verified by suites (2026-11-14 night, headless, all logs in %APPDATA%\AshenAbyss\logs)
- `--suite=systems`: 186 checks, 0 failures (items 1280 generated OK, loot odds per quality, equip rules, salvage → codex, masterwork 12 ranks / 3 boosts,
  temper rare 1 / legendary 2 + charges, enchant, imprint, sockets 2 on chest / none on boots, gem combine, stash limits, merchant / jeweler stock,
  gamble all 8 kinds, junk selling, bag limit, per class: 8 skills / 6 categories / 3 specs, respec, ranks, upgrade A↔B swap, key passive swap,
  spec refused below 15, auto build; paragon allocate / refund / board attach / glyph upgrade / paragon XP; world tiers 0-3 scale monsters).
- `--suite=skills` all 8 classes × levels 1, 12, 25, 50 (at 50: base, A, B and each of the 3 specs; 512 skill tests): every skill fires, its damage
  carries its own id, generators generate, spenders pay, pips are gained / spent, cooldowns start, summons spawn and their hits count as minion hits,
  wildsoul forms switch, channels tick while held, dashes / leaps / blinks move the hero, buffs appear and expire. 0 real failures
  (sb_veil's 3 s buff and fb_spin B's refund were test artefacts; the suite now accounts for both).

## Bugs
1. **Unhandled exceptions could escape `_Process`** (Player, Monster, Projectile, GroundZone had no guard; an exception in one elite affix or boss
   script would repeat every frame in the engine log, outside the game log) → wrapped each in try/catch with a throttled `Log.Error`; a broken
   projectile / zone frees itself, a broken monster drops its wind-up, the hero drops the action in progress. Verified: compiles, runs clean.
2. **Max-life changes did not carry the hero's life** (`Player.OnHeroChanged` computed the gain after overwriting MaxLife, so it was always 0: a
   +life buff or item left the hero "wounded", and unequipping clamped) → life now keeps its fraction when MaxLife changes. Verified in suites.
3. **Every monster of a region animated all the time** (380 AnimationPlayers and CharModel._Process calls per frame even 200 m away, plus a
   `_PhysicsProcess` per monster) → monsters asleep (> 46 m) switch their AnimationPlayer off; CharModel only processes during a hit flash or the ash
   dissolve; Monster physics callback only while a leap is in flight.
4. **Skeletons "rose from the ground" at level load, unseen** → they now lie in the ash until their pack notices the hero (or they are hit), then rise.
5. Loading-screen labels ignored the text-size setting → sizes go through `UiTheme.Fs` and are refreshed by `ApplySettings` (lead's note).
6. **Mini-boss experience multiplied twice** (tier ×8 for "Elite" and ×14 for the mini-boss flag = 112× a normal kill — more than a region boss; a
   level-16 side-dungeon boss gave 12 096 XP, 1.5 levels) → the mini-boss factor replaces the tier factor (`Balance.MiniBossXp = 24`). Verified in logs.
7. **Thorns out of scale** (affix pool 6-620 at max power ≈ 2.4× the weapon's average damage per enemy melee hit; in a level 10-17 run "(other)" =
   thorns dealt 40 % of all damage and one-shot normal monsters) → pool 4-200. Paragon / gem thorns values left as they are.
8. Test-only: `--god` used to add 60 % damage reduction and 30 %/s regeneration, hiding real damage intake → now it only refuses death, counts the
   save (`god-saves=` in the bot's balance line) and refills life, so "taken per minute" is honest.

### Verified by runs
- `--suite=objects` 18/18: loot of every rarity (legendary / unique beams spawn), pickup, gold magnet, materials, globes, chests q0-3, six shrines,
  altar (+1 attributes once, remembered), no errors. (wastes overworld has no breakables: they only exist in dungeons.)
- `--suite=save` 32/32: 8 slots × 8 classes byte-exact round trip, slot list, .bak fallback, both files damaged → "corrupt" and refused, hand-edited
  JSON repaired, delete, hardcore death written at once + refused afterwards + list shows dead, normal death → death panel → respawn at checkpoint
  with full life, save → menu → load restores level / gold / gear / deaths / area, settings round trip. The user's real slots and settings are set
  aside and restored (also after an interrupted run).
- Tour `wastes → wastes_d1 (SlayElites) → wastes_d2 (DestroyIdols) → wastes_boss`: door seals / opens on the objective, mini-bosses engage and die,
  story boss runs phases 1→2→3 with adds and dies, reward chest + exit portal appear, 0 errors.
- `--focus` in wastes overworld: stronghold liberated (beacon → waypoint, quest step, save), all three world events (brazier, ritual, cursed chest)
  start and complete, side quests start on first visit.
- `--campaign --god --level=30 --gear --speed=6`: the whole main quest in one 7-minute run (40 min game time): talk → region → stronghold → story
  dungeon objective → boss → report, acts 1-7, three keys in any order, gating never dead-ends, rewards (+1 skill point per act, potion cap +1 twice),
  7 bosses, 0 errors.
9. **Cast at a target that was freed before the contact frame threw** (`ObjectDisposedException` in `SkillExec.Fire` → `Actor.Pos2`; happens when
   a target despawns between wind-up and contact: boss-fight reset, area change, harness clearing the field) → `Fire`, `BeginCast`, `TryCast` and the
   walk-into-reach order drop targets that are no longer valid.
10. **"Per second" zones dealt double** (sb_blight, the wildsoul storm zone, pb_rain: listed as X % per second, ticked X % every 0.5 s — the per-tick
    share computed in `ZoneSpec.Coef` was never applied for the Zone kind) → `SkillExec` multiplies by it.
11. **Zones of one skill stacked without limit on a target** (sb_blight has no cooldown: the bot kept 4-5 pools under one enemy = 78 % of a
    soulbinder's damage, 9-12× the monster curve's assumption) → an enemy takes one tick per interval from zones of the same skill
    (`Actor.ZoneTicks`). Different skills still stack.
12. **Multi-projectile casts shot-gunned** (hu_pierce "三叉" = 3 × 65 % all landing on one target at close range; hu_mark's +2 projectiles likewise)
    → projectiles of one volley share a hit list: one hit per enemy per cast, the extra ones pass through to hit others.

## Balance (measured with `--suite=dps`: the bot's rotation, auto build, nine rares of the level, 30 s on one dummy / 20 s on six)
Single-target damage per second ÷ `Balance.ExpectedDps` (what monster life is derived from), per class, after bugs 7 and 10-12 were fixed:

| level | emberblade | astromancer | hunter | soulbinder | wildsoul | forgebody | lampknight | pactbinder | median |
|---|---|---|---|---|---|---|---|---|---|
| 5  | 0.84 | 1.08 | 1.45 | 1.50 | 1.43 | 1.03 | 1.15 | 1.81 | 1.43 |
| 15 | 0.96 | 1.31 | 1.76 | 1.71 | 1.21 | 1.02 | 1.82 | 1.02 | 1.31 |
| 25 | 2.43 | 1.26 | 1.40 | 2.86 | 1.27 | 1.68 | 1.16 | 1.51 | 1.51 |
| 35 | 1.96 | 1.11 | 1.22 | 4.64 | 2.01 | 1.46 | 1.57 | 1.30 | 1.57 |
| 50 | 2.58 | 1.28 | 2.05 | 4.98 | 2.04 | 1.93 | 1.30 | 1.44 | 2.04 |

(before the fixes soulbinder was 7.5× / 8.4× / 11.9× at 25 / 35 / 50 and hunter 3.4-4.8×.)
Changes:
- `Balance.ExpectedDps` × (1.3 + 0.014·level): monster life now follows the measured median (normal ≈ 2.2 s single-target, elite ≈ 12 s).
- Soulbinder minions: sb_raise 50 % → 30 %, sb_colossus 260 % → 160 % (minions were 79 % of a level-50 soulbinder's damage).
- Experience curve: `XpToNext = 300 + 520·L^1.15` (×(0.35 + 0.13·L) below level 5). The old curve gave a bot one level every 37-42 s at level 10-12.
- Mini-boss XP 112× → 24× a normal kill; thorns affix 6-620 → 4-200.
Incoming damage (same suite): a normal monster's hit lands for 2-8 % of the hero's life (15-65 hits to die, astromancer / pactbinder the frailest).

### Balance, second pass (boss runs with `--bossrush`, honest `--god`)
- With monster life at 1.3 + 0.014·L: emberblade lv30-32 bosses 74 s / 71 s, normal 1.1 s; lv48-50 bosses 120 s / 55 s, normal 2.1 s (in range);
  but lv12-16 emberblade bosses 190-250 s, normal 4.8 s, 12 "deaths" in 18 minutes; astromancer lv12-15 bosses 118-222 s, normal 4.5 s (too slow:
  early heroes are close to the first estimate, and the levelling emberblade bot kept its level-10 bar — whirl, no rupture).
- Final curve: `ExpectedDps` × clamp(0.8 + 0.024·L, 0.8, 2.0) — 0.9× at level 5, 1.1× at 12, 1.4× at 25, 1.5× at 30, 2× at 50.
- Bot: a spinning channel is only started with 3+ enemies around (or a nearly full resource bar), so builders can afford their core skill.
- Still uneven (data, not touched): soulbinder remains the strongest single-target class at 35+ (minions inherit every bonus), astromancer and
  lampknight the weakest single-target at 50 (1.3× vs 2.0× median; both have very high area damage), emberblade / forgebody are slow before level 15.

## Animation audit (2026-11-15; after the user-reported "hero has no walk animation" bug, fixed by the lead in CharModel)
My first pass never looked at a moving character on screen and the bot checked no animation state at all, so that bug went through. Now:
- `AnimCheck` (Actor.cs), always on: seconds the hero / monsters moved under their own power while no locomotion clip was advancing on their
  model, or with a leg pose that did not change, seconds the hero stood still in a run clip, models built without a skeleton or animation player.
  `Game.Quit` writes the line to every log (`[anim] anim-check: …`), as an ERR when not clean, so run.ps1 shows it.
- `--suite=anims` (TestSuite.Anims.cs): library inventory; every clip named in code, SkillDb, MonsterDb and the boss move lists exists and loops
  when it is held; every character model (heroes, forms, NPCs, monsters, bosses — 40 ids) is rigged, the probe clips reach ≥ 90 % of its bones and
  move it off the bind pose; stride table and strike table re-measured from the clips; town people animate; per class: idle clip, run clip + legs
  moving + speed ÷ stride + facing, stop → idle, roll, every skill shows its clip and returns to idle, stun reaction, death stays down, respawn;
  per monster type: buried ones lie still then rise, walk / run clip, speed ÷ stride, facing, attack clip, death clip. `--suite=anims:static` is
  the 10-second part; `--suite=anims:shots-town|shots-heroes|shots-monsters` takes close-up pictures.
Found and fixed:
13. **Effects came out of the middle of the body**: the rig's bones are `hand_r`, `foot_l`, `Head`…; all code asked for `DEF-hand.R`, which never
    existed, so `CharModel.BonePos` always returned its fallback (projectile origins, chain beams, monster missiles) → `BoneIndex` resolves both
    spellings through an alias table and warns once per unknown bone; callers now say `hand_r`.
14. **`Sword_Idle` did not loop** (idle stance of emberblade, wildsoul and the bosses' brace: it played 1.7 s, froze, and was restarted with a pop)
    → marked looping when the library is built.
15. **Locomotion speed was guessed** (jog ÷ 5.2 for the hero, ÷ 4.6 or ÷ 2.2 for monsters regardless of model scale; measured strides are walk
    0.94, zombie walk 0.94, jog 5.36, sprint 8.0 m/s at scale 1) → `CharModel.Stride` + `Locomote(speed, clips…)`: picks the gait whose feet match
    the ground best (rate clamped to 0.5–2.4), scaled by model size. Big slow monsters and bosses now walk briskly instead of jogging on a
    treadmill; the bloated husk walks (speed 2.9 → 2.3).
16. **Ranged monsters kited backward while still turned to the hero** (a forward run played in reverse; forward·motion −0.3…−0.7) → they turn and run.
17. **Crowds shoved walkers backward / idle monsters slid** (separation push up to several m/s) → capped at 55 % of the walker's pace, 1.1 m/s when
    standing; a walker held up by the crowd or a wall idles instead of running on the spot (hero too).
18. **Blows did not land on their swings** (26 skill / monster attacks more than 0.25 s off: e.g. every third basic sword attack used the 2 s
    `Sword_Regular_C` and was cut before its swing; javelins appeared 0.3 s after the arm had finished; rotspawn's hook ended before its blow)
    → `CharModel.Strike` (measured fastest-hand time per clip) + `PlayAttack`: a clip that strikes late starts inside its wind-up, one that
    strikes early has the wind-up stretched; bosses use the same table in `PlayWind`. rotspawn / frostbound attack clips replaced; fb_spin slowed.
19. **A hero could stick on a big prop** (`LineWalk` only looked at cell types, so a straight click-to-move ran into trees and stalls) → big props
    (r ≥ 0.85) block the straight line and the A* path goes round them, with a squeeze-through fallback.
20. **A monster started its attack swing a second time while recovering** (`CharModel.Pause(false)` called `Play()` whenever nothing was playing,
    and Monster calls it every frame: every finished one-shot clip restarted) → un-pausing only resumes what `Pause(true)` froze.
21. **Ranged monsters stood still for ever when they had to walk round something** (`MoveToward` compared its stop distance — 80 % of the weapon
    range — with the distance to the next *waypoint* instead of the goal; also stopped inside range with no line of fire) → compares with the goal;
    without a line of fire the stop distance is 0.
22. Ranged monsters pivot before they run when kiting (30 % pace until turned), instead of gliding backward for a quarter second.
Audit status after these: hero checks pass for all 8 classes; 36 monster types pass except test artefacts; `anim-check` clean in every run.
Looked at with my own eyes (Y:\shots\qa\anim\): a_npc*.png (elder holds the lantern pose, smith folds his arms, all turn to the hero),
h_hero_*_idle|run|attack.png (run strides face the direction of travel; the blow's flash coincides with the swing; shots now leave the hand),
m_mon*_walk|fight.png (skeletons clawing out of the ground, husks loping, robed cultists walking).
23. **Vampiric mini-bosses could not be killed** (the affix healed 5 % of max life per bite; on a ×16-life mini-boss that is more than a hero of
    its level deals between two bites — frost_d1 and crypt_d1 timed out with the boss going 59k → 67k → 57k for four minutes) → the heal is 18 % of
    an ordinary monster's life of that level (never more than 5 % of its own).
24. **Walking straight at a round prop stopped the walker dead** (radial push-out only; props and path points both sit on cell centres, so
    head-on is the usual case — the campaign bot wedged beside the ember altar by a boss door) → `NavGrid.ResolveCircle` adds a tangential slide
    on head-on contact.
25. **Dead end: an object could wall off a one-cell passage** (the ember altar is put in the "most enclosed" cell near the boss door — in a
    corridor dungeon that is the corridor itself; radius 0.75 + hero > half a cell, so the opened door could not be reached: sanctum_d2 timed
    out, the campaign bot needed two teleports in sands_boss) → `Level.KeepsConnected(pos)` (does blocking this cell cut the level?); `WorldObjects.Block`
    leaves an object walk-through when it would, and the altar's nook search avoids such cells.
26. **Elite bruisers had 19× a normal monster's life** (tier ×5.5 on top of the def's own ×3.4: 60 s per marked elite at level 50, abyss_d2 ran out
    of time) → for champions, elites and named leaders the tier factor is divided by √(def life factor).
27. First levels: packs capped at 4 (level ≤ 2), 6 (≤ 4), 9 (≤ 8); elite ×3.5→5.5 and named-leader ×8→16 life grow over levels 1-10; story-boss life
    ×0.6→1.0 over levels 1-10; monster hit 5 + 3.3·L (was 8 + 3.2·L), hero base life 130 + 24·L (was 90). A level-1 hunter bot died 8 times before
    level 4 and 15 times by the first boss with the old numbers.

Side-dungeon tours (`--tour`, level 30 forgebody / level 45 wildsoul, god mode counting deaths): frost_d2, crypt_d2 (CollectEmbers), mire_d1,
sanctum_d1 (FreePrisoners), mire_d2, sands_d1, abyss_d1 (DestroyIdols), sands_d2 (SlayElites) all completed: objective → door → named boss → chest
→ portal. frost_d1 / crypt_d1 failed on bug 23, sanctum_d2 on bug 25, abyss_d2 on bug 26 (re-run after the fixes: see below).

## Progress log (so work survives an interruption)
- 2026-11-15 09:50 paused; resumed 15:50. From now on every test run is a foreground command (`Y:\tools\qa\qarun.sh` waits for the run lock only
  while the run still fits in its deadline; no detached queues).
- Results that existed at the pause: campaign regression acts 1-5 with the new balance (bosses 62-81 s, normal 2.3 s, renown tiers 1-2 granted in
  four regions, 0 errors, anim-check clean); windowed 5-minute soak in mire (60 fps steady, nodes 4867 → 3668, memory 278 → 269 MB, PrtSc flag
  path and F12 key path each wrote a PNG under Pictures\AshenAbyss); `--suite=systems` 187/187 incl. the new town-services check
  (elder → dialogue, smith / merchant / occultist / jeweler / stash → vendor, waypoint → map, tier statue → tier, rift stone → rift);
  re-run of frost_d1 and crypt_d1 after bug 23: both completed.
- Found from that partial batch and fixed right after the resume:
28. Bug 25's fix was incomplete: the connectivity test ran while the boss door was sealed, so the stub of corridor in front of the door looked
    like a harmless dead end and the altar could still plug it (sanctum_d2 again) → sealed door cells count as passable in `KeepsConnected`.
29. Bug 26's fix did nothing for named leaders (the mini-boss branch reset the multiplier after the division) → order corrected.
30. `AnimCheck` false positive (0.9 s "frozen legs" in the windowed soak): a hero who stops and starts restarts the run clip, so equal samples do
    not mean a frozen pose → the probe is reset whenever movement was interrupted.
- Still to run after the resume: full `--suite=anims`; dungeon 5-minute soak; rift:1 and rift:12; the four dungeons of bugs 23/25/26 once more;
  natural level-1 campaign with the gentler first levels; campaign chunk + `--load=0` continuation to the ending; objects / save suites windowed
  (loot-beam and death-panel pictures); ui-tour; skills regression.
- 16:00-16:10 (foreground runs): `--suite=anims` full: 121 pass / 3 fail — the 3 were one unreachable spawn spot in that random world (the test
  now picks spots it can path from); `--suite=anims:monsters` on two worlds: 50/50 (all 36 monster and minion types rise / walk / attack / die
  with the right clips, speed ÷ stride 1.0, reactions: flinch, stun, freeze-hold, thaw, death, crumble). sanctum_d2 after bug 28: completed in
  252 s game time, 0 unstuck, named knight 54k life killed in 58 s (was 87k), anim-check clean.
- Tools: `Y:\tools\qa\qawait.sh [s]` waits in the foreground for the shared run lock; `qarun.sh` never outlives QA_DEADLINE. This environment
  moves any command past ~180 s to the background, so each run is kept under ~110 s of game (`--speed` covers the game time).
- Private harness save slots 90-99 (`--slot=97`, `--load=97`): never listed by the menu; used to chain a campaign over several short runs.
- 16:10-16:30 (foreground runs):
  - abyss_d2 after bug 29: completed (SlayElites → door → named boss → chest → portal).
  - `--area=rift:1` (level 50, tier 3): progress bar → guardian (mire_bloated, 120 863 life, 105.8 s) → `[rift] tier 1 finished success=True
    timeLeft=319s best=1`; paragon levels 1-3 from rift XP, glyphs woke, +3 tempering attempts.
  - Lead's hand-over items: (1) `Loot.FloatText` pixel size 0.0085 → 0.0039 (same on-screen size as damage numbers); (2) `Level.DungeonPhase`
    { None, Objective, Boss, Cleared } + `Level.Phase`, `BossPending`, `Cleared` (`ObjectiveDone` keeps its old meaning; TestBot uses the new
    ones; Hud.cs:475 and MapUi.cs:1482 still read `ObjectiveDone` — for their owners); (4) the UI / gfx test switches are in `Game.KnownSwitches`.
  - (3) Town people: new numeric check in `--suite=anims` — every NPC's pose signature changes over 0.6 s, also with the quest log open and the
    game paused: 8/8 animate (elder Idle_Lantern_Loop, smith and one villager Idle_FoldArms_Loop, merchant and jeweler Idle_Talking_Loop,
    occultist and two villagers Idle_Loop). The T-pose in the UI reviewer's 08:52 screenshots predates the lead's `_Loop` alias fix in CharModel.
    hero_forgebody: `hero_forgebody.glb` was written 08:39 but Godot imported it at 09:08 (date of its `.import`); the class-selection
    screenshot was taken 08:52, so `ResourceLoader.Exists` was false and the stand-in was shown. Name mapping is right (MainMenu.cs:2296
    `CharModel.Create("hero_" + Names.ClassKey(c))`). **Still without `.import` (need one Godot import pass, by the lead):**
    `hero_lampknight.glb`, `npc_elder.glb` → they still show stand-ins.
  - `--suite=anims` full, 3× speed: 123 pass / 2 fail, both the same thing →
31. **A leaping monster dropped into its idle pose for the last tenth of its flight** (`NinjaJump_Start` is 0.97 s, played at 1.5× = 0.65 s,
    the leap takes 0.75 s) → the leap switches to `NinjaJump_Idle_Loop` at 80 % of the flight (Monster.TrySpecial, Leaper).
- 16:30-16:36 (foreground runs):
  - `--suite=anims:monsters` after bug 31: 50/50.
  - `--area=rift:12` (level 50, nine rares, tier 3, paragon 120, 6× speed): `[rift] tier 12: … mul=3.90` → `tier 12 timed out at progress 44%`
    (normal TTK 56 s, 58 god-saves): the time-out path works; tier 12 is out of reach for a hero in plain rares, as a ladder should be.
  - Dungeon soak, crypt_d2, soulbinder with minions, 300 s of game at 3× (headless): 145 fps flat, nodes 1574 → 733 as the dungeon empties,
    memory 1031 → 1039 MB (headless keeps textures in RAM; windowed runs show ~240-290 MB), 0 errors, cleared at 280 s game time, 0 deaths,
    0 unstuck; leaving through the exit into the still-locked crypt was refused by quest gating (correct), bot went back to town.
  - `--quit-at` counts play time (loading excluded): leave ≥ 14 s between `--quit-at` and the run time-out or the final summary is cut off.
  - Ten models have no `.import` at 16:35 (artists are writing them now): boss_bishop, boss_gravedigger, boss_mire_mother, boss_rimelord,
    boss_sand_tyrant, hero_lampknight, mon_brass_sentinel, npc_elder, npc_merchant, npc_smith → stand-ins until Godot imports them; re-run
    `--suite=anims:static` (30 s) after the import to check their rigs.
- 16:36-17:00 (foreground runs): natural play from level 1 (no gift gear, bot wears what drops, 6× speed):
  - hunter, no god mode: 1 death before level 4 (was 8), level 4 after 153 s; then 5 deaths at the first story boss (the bot neither dodges
    telegraphs nor clears the packs it drags into the arena); boss fight needs ~110 s at 18 dps.
  - emberblade with honest god mode on private slot 97, chained over two runs with `--load=97` (load continuity works: quest state, level,
    area, waypoints carried over): levels 2-7 at 45 / 97 / 122 / 213 / 287 / 468 s; boss 1 at level 4 in 46.6 s; frost stronghold leader
    (frost_knight lv8, 3676 life) 140.8 s; boss 2 (rimelord lv9, 7564 life) 158.8 s; normal TTK 5.9 s; 8 god-saves in ten minutes of frost.
    → a hero in found gear does ~0.55× `Balance.ExpectedDps` at levels 4-10 (the dps suite's nine rares do 0.84-1.8×).
32. Balance: `ExpectedDps` last factor `clamp(0.8 + 0.024·L)` → `clamp(0.56 + 0.032·L, 0.56, 2)`: monster life −28 % at level 1, −22 % at 5, −15 % at 10,
    −6 % at 20, equal at 30, +4 % at 40, equal from 45. Story-boss young factor on life 0.6→1 became 0.8→1 (the curve now does that job), and
    story-boss damage ×0.75→1 over levels 1-10.
- 17:00-17:20 (foreground runs): **natural campaign from level 1 to the ending, chained over 11 runs through private slot 97**
  (`--new=emberblade --slot=97 --bot --campaign --god --speed=6`, then `--load=97 …` ten times; every load restored level, area, quests, renown,
  waypoints; 0 errors in all 11 logs, anim-check clean in all):
  | act | boss (level, life) | fight | hero level | normal TTK | god-saves in that run |
  |---|---|---|---|---|---|
  | 1 wastes | gravedigger (4, 1 968) | 46.6 s | 4 | - | (summary cut) |
  | 2 frost | rimelord (9, 7 564) | 158.8 s | 9-10 | 5.9 s | 8 — before balance change 32 |
  | 3 crypt | bishop (13, 12 667) | 65.5 s | 13-14 | - | (summary cut) |
  | 4 mire | mire mother (19, 28 231) | 71.3 s | 19 | 1.6-2.7 s | 0 + 3 |
  | 5 sands | sand tyrant (27, 67 201) | 94.0 s | 25-26 | 3.4 s | 2 + 0 |
  | 6 sanctum | grand hush (34, 123 755) | 114.9 s | 32-33 | 3.1-4.0 s | 0 + 0 |
  | 7 abyss | Osaros (41, 237 953) | 65.6 s | 39-40 | 3.1-6.8 s | 2 + 2 |
  Ending: `[quest] ending narration joined to the epilogue`, `main_abyss complete`, world tier 2 unlocked, level 42 after ~73 min of game time.
  Stronghold leaders took 50-140 s (frost_knight 140.8, knight 50.3, rotspawn 89.0, brass_sentinel 67.3, knight 86.5, brute 126.7).
  The story-only hero falls behind the regions: level 5 → frost (8), 21 → sands (26), 27 → sanctum (33), 34 → abyss (40).
33. Balance after that chain: named leaders ×16 → ×13 a normal monster's life (before the bruiser correction); catch-up experience for prey
    above the hero's level +4 %/level (max +30 %) → +10 %/level (max +80 %); Osaros life factor 70 → 95 (the last boss was the shortest fight
    after the first: 65.6 s here, 55 s in the level-50 rare-gear run).
- Audio note for its owner: `[audio] voice 'ending_01' has no recording, using a timed stand-in` (WRN) during the ending.
- 17:20-17:35 (foreground runs): natural hunter from level 1 again (no god mode), twice:
  - after balance change 32: 0 deaths before the boss, normal TTK 2.8 s, elite 10.8 s, level 4 at 123 s — but three deaths at the first boss, each
    47-67 s in: `hu_shot` hit for 3.4 and `hu_pierce` for 20.9, i.e. the hero still held the starter weapon (power 10) at level 4 and did 17 dps.
  - next run (a weapon dropped by chance): boss 1 killed at the first attempt in 47.7 s; level 7 after 371 s; 1 death in wastes, 2 in frost
    (level 7 against level-8 packs); normal TTK 3.0 s, champion 8.9 s.
34. **A hero could reach the first boss with the starter weapon** (weapon damage is the base of every skill; a level-4 weapon is 2.4× the starter) →
    weapon pity in `Loot.RollDrops`: when the held weapon's power is under 6 × level, the next story boss or named leader — and every second
    elite — drops a weapon for the class at the hero's level, magic or better (`[loot] weapon pity: …`).
- 17:35-17:50 (foreground runs; new wrapper `Y:\tools\qa\qarun2.ps1 -GameArgs … -Timeout n -LockWait n [-Headless]`: it queues on the run lock like
  every other caller, but only for LockWait seconds, then calls run.ps1 in-process — the probe-and-sleep of qarun.sh was starved by blocking waiters):
  - `--suite=objects` windowed: 18/18; loot picture `Y:\shots\qa\objects_loot.png` (labels and beams for common / magic / rare / legendary and a
    gem all read well; four toasts + the area title cover the hero in the middle of the screen), `Y:\shots\qa\objects.png`.
  - `--suite=save` windowed: 32/32, user saves put back; `Y:\shots\qa\save_death.png` (one second after death: title and flavour line, no button
    yet, the HUD's area title still shines faintly through the black).
  - `--ui-tour` windowed (astromancer 30): all 17 panels open, 0 errors, 0 warnings; pictures in `Y:\shots\qa\ui\tour_*.png`.
    Rift panel correctly locked before the ending ("終結寂主之後，深淵裂隙才會開啟").
35. **Town map: service markers floated on black fog** (`tour_map.png`: 29 % explored, elder / smith / merchant / tier statue / rift stone labels
    over unexplored ground) → the town is revealed completely on arrival (Level.BuildNow / Reveal).
- Seen in other engineers' windowed logs (for the rendering owner): video memory climbs with every theme visited and does not come back —
  town 2.1 GB → after one dungeon 3.1 GB, still 3.1 GB back in town (game_20261115_174722.log); the 1440p quality-3 tours reach 4.2 GB after
  five regions and run at 10-36 fps (game_20261115_161603.log, 161825.log).
- 17:52 lead's notes on the finished models, done at once:
  - elder stays on `Idle_Lantern_Loop` always (nothing in the code switches a named NPC's clip; documented at `NpcDefs`); occultist
    `Idle_Loop` → `Idle_FoldArms_Loop`; smith already `Idle_FoldArms_Loop`; merchant `Idle_Talking_Loop`.
  - lampknight: idle is `Idle_Shield_Loop`; lk_bash `Shield_OneShot`, lk_aegis `Sword_Block`, lk_charge `Shield_Dash` already; the two buffs that
    used the two-handed `Idle_Rail_Call` now use shield-friendly clips: lk_aura → `Sword_Block`, lk_dawn → `Shield_OneShot`.
    (lk_strike keeps the sword swings, lk_smite `Spell_Simple_Shoot`, lk_lance `OverhandThrow`: right-hand clips.)
  - new audit check: elder / smith / occultist / merchant play their clip before, during and after a talk.
  - `--suite=anims` full run on the re-imported models (17:55): **126 pass / 0 fail, 0 errors, 0 warnings; 40 character models rigged,
    0 stand-ins left**; NPCs animate behind a panel 8/8.
- 18:07 regression after all of today's changes: `--suite=systems` 187/187, 0 errors, 0 warnings.
- 18:00-18:10 the shared build was broken four times by half-finished UI edits (MapUi.cs, DialogueUi.cs, QuestLogUi.cs, VendorUi.cs/ItemUi) and the
  run lock is contended (waits of 30-110 s): the windowed close-ups of the final NPC / lampknight models are still to be taken.
- 18:10 natural hunter from level 1 after all changes (no god mode, 6×, headless): `[loot] weapon pity: the hero's weapon has power 10 at level 3,
  husk_bloated drops one`; boss 1 killed at the first attempt in 48.7 s (hero at 24 % life); levels 2-7 at 45 / 103 / 122 / 198 / 212 / 264 s;
  deaths: 1 in wastes (level 2), 1 in frost (level 7); normal TTK 2.7 s, champion 8.3 s; 1.9 life bars lost per minute; anim-check clean; 0 errors.
- 18:17 `--suite=anims:shots-town` windowed with the final models, looked at: `Y:\shots\qa\anim_npc0_elder.png` (lantern hangs from the right
  hand, Idle_Lantern_Loop), `anim_npc1_smith.png` (arms folded, hammer on the shoulder), `anim_npc3_occultist.png` (hooded, arms folded) — as the
  lead specified. Log: every NPC `playing=True`, off-rest 1.5-7.6.
- 18:28 `--suite=anims:shots-hero-lampknight` (new: one class, idle / run / attack and every bar skill in mid-cast), looked at:
  `Y:\shots\qa\anim_hero_lampknight_idle.png` (final model, shield on the left arm held in front, Idle_Shield_Loop),
  `…_lk_aura.png` (guard pose, Sword_Block), `…_lk_bash.png`, `…_lk_aegis.png`, `…_lk_charge.png`, `…_lk_dawn.png` (Shield_OneShot; the cast
  burst whites out the whole close-up frame — for the effects owner).
- 18:34 regression `--suite=skills:lampknight,hunter:1,25`: 18/18. Whole project compiles (0 errors) at 18:36. Private slots 96/97 deleted.

## State at hand-over (2026-11-15 18:40)
Verified today on the final code: anims 126/0 (40 models, 0 stand-ins), systems 187/0, objects 18/0, save 32/0, skills (lampknight, hunter) 18/0,
ui-tour 17 panels, natural campaign level 1 → ending over 11 chained runs (0 errors), rift:1 success and rift:12 time-out, dungeon soak, all 14 side
dungeons, natural hunter start (first boss at the first attempt).
Not re-run after balance changes 32-34: the full 512-test skills suite (last full pass before them), the dps tables above (monster life below
level 30 is now 6-28 % lower, so the ratios there read higher: level-5 median ≈ 1.8×, level 15 ≈ 1.5×), a windowed five-minute soak.
Not tested at all: real keyboard / mouse / pad input (the bot drives the public API; PrtSc and F12 go through a simulated hook call and a synthetic
key event), sound by ear (every run is muted), world tiers 2-3 beyond the tier switch and one tier-3 rift, rift tiers above 12, a hardcore campaign.
New since 17:50: 29 more monster models are on disk without `.import` (mon_ash_cast … mon_wraith_mourner); no MonsterDef uses them yet. When they
get defs, `--suite=anims:static` (30 s) checks rigs and clips and `--suite=anims:monsters` the live behaviour.
