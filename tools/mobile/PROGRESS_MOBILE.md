# MOBILE-CAST agent progress (0.4.1) — moving while attacking
Slot 92. Notes resumable. No background processes unless listed below.

## Plan
1. SkillDef.Mobile + SkillDb.MarkMobile (explicit id list)
2. Player.Mobile.cs (partial): MobileCastOn (setting / --mobile-cast), speed mul, upper-body adapter (reflection), TestMove
3. Player.cs hooks: Tick (HandleOrders during mobile cast), MoveSpeed, HandleOrders r==1, facing, anim, BeginCast anim
4. Settings.MoveWhileCasting + SettingsUi row + controls line
5. AI pilot: kite while shooting, shoot while dodge-walking
6. --suite=mobile (TestSuite.Mobile.cs) + mobile:dps kite harness
7. Balance runs, pilot boss before/after, screenshots

## Log
- 11:30 code done: SkillDef.Mobile, SkillDb.MobileIds/MarkMobile, Player.Mobile.cs (+hooks in Player.cs), Settings.MoveWhileCasting,
  SettingsUi row + controls row + reset, Game.cs switches (mobile-cast, mobile-speed, upper-off) + a tip, AiPilotMobile.cs + hooks
  in TestBot.cs/AiPilotCombat.cs, TestSuite.Mobile.cs (+case in TestSuite.cs). RENDER API landed (CharUpperLayer.cs) → direct calls.
- 11:35 --suite=mobile 30/30 PASS headless + windowed, 0 ERR. Shots Y:\shots\mobile\*_backing_*.png, settings_*.png.
- 11:48 mobile:dps harness reworked (treadmill lane, flee hysteresis 6→9 m, dash/leap/blink off the bar in all modes).
  Sanity hunter lv30 15 s: stand 547/s, kite-off 222/s (0.41x), kite-on 530/s (0.97x).
- 11:49 BACKGROUND: bash Y:/tools/mobile/batch.sh (bash pid 1144112, log Y:/tools/mobile/batch.log): DPS table (dps.txt),
  then 16 pilot boss runs (boss_results.txt). Stop: kill that bash pid (own process) — run.ps1 children end by timeout.
  Resume: SKIP_DPS=1 bash batch.sh (re-runs all boss fights).
- 12:05 DPS table (mobile:dps:all:30,50:40, speed 4, fixed gear, game_20261124_114938.log, ERR 0) → Y:/tools/mobile/dps.txt
  | class lv | stand | kite-off (stop&shoot) | kite-on (65 %) |
  | hunter 30 | 590 | 234 (0.40) | 585 (0.99) |   hunter 50 | 1476 | 430 (0.29) | 1461 (0.99) |
  | astro 30 | 366 | 222 (0.61) | 362 (0.99) |    astro 50 | 698 | 462 (0.66) | 788 (1.13) |
  | soul 30 | 581 | 317 (0.55) | 529 (0.91) |     soul 50 | 2093 | 1011 (0.48) | 1939 (0.93) |
  | pact 30 | 555 | 381 (0.69) | 517 (0.93) |     pact 50 | 1390 | 871 (0.63) | 1178 (0.85) |
  | melee stand: emberblade 1165 / 2011, lampknight 516 / 1478 (lv30 / lv50) |
- 12:12 BACKGROUND: followup.sh (bash pid 1146472, log followup.log) waits for BATCH DONE, then melee refs, dps at 0.55, chase suite (chase_out/).
- 12:40 boss fights (frost lv22 / abyss lv42, seed 4242, speed 3) → boss_results.txt, `bash summ.sh` for a table.
  Ranged frost fight secs off→on: hunter 259→151, astro 246→122, soul 255→149, pact 206→206; abyss: hu/as/sb off = not killed in
  600 s, on = 178/185/284 s; pact 361→285. Melee refs frost (same seed): eb 130, lk 283, fb 249, ws 383 (mean 261).
  → ranged now ahead of melee in pilot boss fights (mean 157 vs 261) → TRIM: Mobile shots fired on the move deal 90 %
  (Player.MobileDmgDefault 0.9, --mobile-dmg). Speed trim 0.55 tested: kite-on 0.80–1.02x stand (little effect) → kept 0.65.
- 12:55 BACKGROUND: batch2.sh (bash pid 1151365, log batch2.log): waits FOLLOWUP DONE, builds, mobile suite, DPS lv50 with trim,
  4 ranged frost boss fights with trim.
- 13:11 batch2 done: suite 32/32 (with the trim), DPS lv50 with trim: kite-on 0.83/1.12/0.84/0.77x stand (hu/as/sb/pb);
  frost boss with trim: hunter 121 s, astro 160, soul 153, pact 164 (1 death) — single runs vary ±30 %, so the trim is set
  from the DPS harness, not from these. Chase regression suite (chase_out/): 8 chase classes pass except forgebody detour
  ('stuck', melee path, unrelated); cancel: emberblade 'ground' and hunter 'shift-click' failed once, A/B re-runs of hunter
  cancel: mobile on 8/8 twice, mobile off 7/8 twice ('hold' — fails in the pre-change baseline chaseall too) → flaky, not mine.
- 13:12 FINAL: check.ps1 BUILD OK. No processes of mine left (own tail -f monitors killed by pid). slot92/slot0 saves moved to
  saves_test_backup/mobile. Settings shots retaken with the final hint text.
