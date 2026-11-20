# 展示模式 + AI 代打 — working notes (demo/pilot engineer)

## Built (2026-11-21)
- `game/src/DemoMode.cs` (new): attract loop. Started by title entry 「展示模式」, by title idle (60 s, `DemoMode.DefaultIdleSeconds`), or `--demo`.
  Test switches: `--demo`, `--demo-secs=N` (scene length), `--demo-idle=N` (idle seconds).
- `game/src/AiPilot.cs` (new): AI 代打. F8 / pause-menu button. Test switch `--pilot-at=<s>`.
- Small hooks: `Game.cs` (NoSave, Card* overlay text, DemoSession, HideMenu/ShowMenu reuse, AiPilot node at boot, no pause-on-focus-loss in demo,
  switches), `TestBot.cs` (Pilot / Demo modes, BotDevice marker on synthetic keys, legal travel + no teleports in pilot), `Hud.cs` (`Lean`),
  `PauseMenu.cs` (toggle button), `MainMenu.cs` (entry, idle watcher, focus restore).

## Test slots
- AI pilot: 95 (new game), 96 (mid-campaign save) — assigned by the lead. Move slot95*/slot96* and slot0* to saves_test_backup afterwards.

## Progress log
- compile OK after first implementation.
- run 1 (`--demo --demo-secs=14`, 125 s, game_20261121_012724.log): 9 scenes, 8/8 classes, 0 ERR/0 WRN, no save written.
  Found: named arenas missing (NamedDb not loaded yet → `NamedDb.Ensure()`), hero arrives under the exit arch (→ `OpenGround`
  relocation), buff row + quest toasts still on screen (→ Hud.Lean hides them every frame, Toast suppressed), VRAM 1.7→5.4 GB in 9 scenes
  (to watch in the long run; LevelBuilder.TrimCaches keeps two themes).
- run 2: title click on 展示模式 (uido `Y:\tools\demo\demo_menu.uido`), 15 s scenes for ~8 min, Space back to the title.
  Result (game_20261121_013900.log): click → demo in 0.5 s, 26 scenes / 412 s, 8/8 classes, 15 looks, 0 ERR/0 WRN, Space → title,
  menu music back, focus back on 展示模式 (menu_back.png). VRAM swings 1.5–5.9 GB (drops on overworld builds), mem 295–390 MB.
- slot 96 setup (headless bot+campaign+god ×6, 260 s): lampknight lv13 at crypt_boss (ch.3); copy in `slot96_setup.json`.
- pilot new game (slot 95, game_20261121_015952.log) started 02:00: gate → wastes, stronghold, boss lair entrance by 3 min.
  → handed back at 296 s "dying repeatedly" (3 deaths at the first boss, lv4) — hand-back path works, rule too eager.
  Added: after 2 deaths the pilot grinds 4 min / +2 levels in the region overworld; hand back at the 4th death.
- rerun (game_20261121_021106.log): ch.1 done at 6:17 (boss killed, portal home, elder, travel map → frost); lv 8 by 9 min;
  handed back at 10:29 after 4 deaths (2 while grinding in frost). Rule changed again: deaths count only since the last
  progress (levelup/boss/miniboss/stronghold/dungeon/waypoint); pilot drinks at 62 % (bot 50 %), rolls at 45 %.
- pilot mid-campaign run (slot 96, game_20261121_022508.log): PASS, 0 ERR/0 WRN. F8 on at 15 s; simulated Alt-Tab at 130 s →
  pause menu stays (pilot leaves it) until the "player" clicks 繼續旅途; player-opened inventory left alone; ch.3 boss 腐骨主教 killed,
  town portal → elder → travel map → mire, stronghold liberated, into 沼母之庭; lv 13→17, 0 deaths. W → OFF, F8 → ON, world click → OFF.
  slot96.json afterwards: same 52 fields as before, bar layout unchanged → a normal save.
- pilot new game attempt 3 (game_20261121_023417.log): hero wedged at (131.6,39) in wastes; the town portal escape came straight back
  through the return portal → handed back at 375 s ("stuck"). Fixed: 1st stuck → step aside with WASD + dodge roll; 2nd → town portal
  and walk out through the gate (not the portal back); 3rd → hand back. Progress events also reset the stuck count.
- pilot new game attempt 4 (game_20261121_024412.log): PASS. 0 ERR, 2 WRN (bot's own "going nowhere" notices). lv1 → lv13 in 19 min;
  ch.1 (wastes stronghold, 掘墓者) and ch.2 (frost stronghold, 赫爾蒙) complete, into crypt. Deaths 4 (2 at the first boss → grind
  lv4→6 → boss killed), stall in frost → step aside, then town portal + waypoint back (legal). 2 bag trips (town portal, salvage 32/19
  junk pieces). 44 normal autosaves. F8 off at 1180 s, F8 on at 1184 s, world click → off at 1194 s. slot95.json: 52 fields like
  any save, bar = the bot-built hunter bar.
- demo soak (game_20261121_030738.log): PASS. Title click → 1808 s of demo → Esc → title (menu music). 0 ERR / 0 WRN.
  49 scenes (Region 8, Dungeon 9, StoryBoss 9, Named 13, Rift 5, Town 5), 8/8 classes, 18 looks, 41 areas, 44 distinct
  stars incl. all 7 story bosses. Level builds behind the card: median 0.76 s, p90 3.3 s, max 5.9 s. VRAM sawtooth 2.0–6.48 GB,
  peak reached at 10–15 min and never exceeded in the next 20 min; static mem 315–423 MB. No save written; settings.json identical.
  Showcase shots: Y:\shots\demo\demo_best_1..6_*.png (all shots: soak_*.png, t1_*, t2_*).
- idle test (game_20261121_034553.log, --demo-idle=6 --textscale=2): idle → demo at 6 s; mouse move ends it (after the fix: scripted
  motion has no Relative → position delta now used); Alt-Tab (blur) ends a running demo and none starts while unfocused (22 s);
  after focus returns it starts again after 6 s; a key ends it. Sub-screen test (game_20261121_034836.log): 36 s idle on 設定 /
  class select / 製作名單 → nothing; back on the title → starts after 6 s. settings.json identical.
- 7 title entries at 特大 text touched the key-hint row → emblem ×0.52 and motto gap ≥4 px at 特大 (title_ts1/ts2.png checked).
- named-arena captions now name the region (「鏽砂廢都・荒靈行者 對 末班工頭」), not 「試煉場・末班工頭・… 對 末班工頭」.
- test saves moved: %APPDATA%\AshenAbyss\saves_test_backup\demo_pilot_slot95/96.*  (no slot0 written).
- DONE 03:56. No background processes left running.
- scripts: `demo_menu.uido`, `demo_idle.uido`, `pilot_new.uido`, `pilot_mid.uido` in this folder.
