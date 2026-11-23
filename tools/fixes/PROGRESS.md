# FIXES agent progress (slot 94)
Started 2026-11-22. User playing at start (AshenAbyss.exe running) -> code work first, runs later.
| # | item | state |
|---|---|---|
| 9 | import err | cause found: skin_n.jpg.import re-uid'd by hand (bct4…), 5 glbs still point at btquws…; mipmaps on, md5s+TMPs moved to tools/fixes/import_bak; NEED import.ps1 run |
| 8 | PrtSc | user's installed session saw 701 key events via the hook (hook works); 0 PrtSc ever seen. Win11 PrtSc->Snipping setting absent=default ON. Repro built tools/fixes/hookrepro (sends F24 only) — run when user not playing |
| 2 | gems icon | cause: Icons.ForKind(OffHand) -> KindKey '_'=>gem -> 'gems' vector (empty off-hand ghost). Fixed in Icons.ForKind/OffHandGhostKey (class off-hand raster). verify visually |
| 1 | loot labels | Hud.LabelsYieldToHud=true + visibility guards; Ui.cs uido 'dropat x,y[,n]' staging cmd. needs staged run+shots |
| 3 | stuck corner | analysis: hero centre x=5.9 is inside the 3-cell Halls rim (floor x>=6.0) -> hero was embedded in a wall; 0.2.0 run had no --seed. Plan: seeded bot runs wastes_d1 to reproduce |
| 4 | flaky mapquest | TestSuite.MapQuest.cs: static target preferred; moving target followed until its cell is seen; same check. needs suite run |
| 6 | gamble off-hand | VendorUi.GambleTab: 副手 row after 武器 for classes with off-hand bases, list in FlexWell (scrolls). needs shots at all UI sizes |
| 10 | credits text | ClassBlurb.Weapon unused anywhere; class screen shows Role + ItemDb.LoadoutLines (可用武器). needs menu shots at 3 UI sizes |
batch1: Y:/tools/fixes/batch1.sh (bash, background; stop: TaskStop its task)
| 8 | PrtSc (cont.) | standalone repro: LL hook on own thread DOES see injected PrtSc (msg 100/101 scan 37 flags 11/91) and swallowing it stops Windows. Game test runs never focused -> flag path. Added WinKeys --hook-selftest (F24). |
| 7 | dps | TestSuite.cs: --suite=dps:classes:levels:bases (DpsBases) + _focusCdDmg. Script tools/fixes/dps.sh |
Queued scripts: batch1.sh (prtsc, inv before, gamble, labels), batch2.sh (inv2 900/1080, hook selftest, class screens), stuckhunt.sh, dps.sh
batch3.sh = combined UI runs with lock retry (runq.sh); started 11:10
## Results so far (11:40)
- 1 labels: VERIFIED shots/fixes/labels_alt.png, labels_plain.png (15 items dropped under minimap/tracker/bar; none drawn over HUD).
- 6 gamble: VERIFIED gamble_s0/s1/s2.png: 副手 row 2nd, list scrolls at 特大 (0 lint flags). Pending: scroll + off-hand roll shots (batch4).
- 5 inventory 特大 @1600x900: fits, 0 lint flags (inv_s0/s1/s2, filter, offence). 1080 pending (batch3 run 2).
- 2 gems: also Legacy map gems/gem -> item/gem_ruby_2; off-hand ghost now ItemIcon3D of class's first off-hand base (fallback rasters). Pending shot batch4.
- 8: dev runs' hook saw 210 key events (user typing elsewhere) -> hook works in dev build too.
- 7 dps BEFORE (25 s windows): tools/fixes/fixes_dps_before.txt. Bias found: hits not bound to swing rate (cooldown skills, minions, DoTs) use per-swing hit = DPS/aps -> slow bases favoured (forgebody 2H 1.57-1.80x fists @50; soulbinder 1H+off 0.70-0.83x 2H; pactbinder wand 0.69-0.73x). Crossbow/mace2 NOT ahead (below best base).
  FIX: Combat.PlayerHit: minion or cooldown skill hit x SpeedMul; Combat.DotDps x SpeedMul. AFTER run (40 s windows) -> fixes_dps_after.txt
- slot0 note: --suite=dps writes slot 0 (harness slot) regardless of --slot.
- import.ps1 started 11:44 detached (output tools/fixes/import.log, ends with IMPORT DONE); waits on the run lock
- 2 VERIFIED: ghost_inv_s2.png (empty off-hand = 3D shield ghost), no 'has no raster art' in logs since.
- 6 VERIFIED: gamble_s2_scrolled.png / gamble_s2_rolled_off.png (scrolls to 戒指; off-hand roll -> 灰染的守望者鳶盾, magic shield).
- 7 AFTER: tools/fixes/dps_compare.txt (before 25 s / after 40 s, ratio to the class's best loadout within the same run; absolute
  values move between runs because the rest of the auto gear is re-rolled). Spread fixed for soulbinder (0.67->0.94 min), pactbinder
  (0.74->0.86), forgebody lv25 (0.76->0.96). Remaining: forgebody lv50 fists 0.71 of 2H axe2 (cast rate is not fully swing-bound:
  jab casts 72 vs 54 in 40 s where swing speeds predict 72 vs 43; slow weapons keep an edge, largest for forgebody whose 2H swing at
  0.59-0.67 of pace) + axe implicit vulnerable dmg. Crossbow 0.85-0.89 of bow; wildsoul mace2 0.82-0.94: within noise/implicits, no nerf needed.
- import: first attempt gave up (lock 30 min); restarted 12:33 as background task with retries.
- 9 FIXED: import2 (13:09) reproduced the 3 errors once while the 5 glbs (touched .glb.import) re-imported against the stale deps;
  cache now holds uid bct4eqbescbpe for skin_n.jpg (0 refs to btquws8y17j4g); import3 (13:2x) exit 0, no ERROR lines.
- 4 mapquest run 1: 22/22 (goal SlayElites, moving elites) — runs 2/3 queued.
- 5 VERIFIED fits: inv3/ (setstab, defence, utility, tooltip at 特大, dual-wield), p1080/ (pactbinder), 0 lint flags. 1920x1080 = same
  1920x1080 logical layout (canvas_items/expand, 16:9) as 1600x900. No code change needed.
- 10 VERIFIED: class/cls_*_s0/s1/s2 (lampknight, pactbinder, soulbinder, wildsoul), 0 lint flags. Weapon field of ClassBlurb unused.
## Item 8 extended (lead 14:1x: PrtSc -> clipboard)
- hook self-test (14:09): SendInput F24 accepted (2/2), window focused, game hook saw 0 injected events (standalone sees them; game hook sees real keys) -> swallowing can't be proven in tests; fallback implemented.
- ScreenClipboard.cs (new): CF_DIB + registered "PNG", OpenClipboard(game HWND), worker thread, optional delay; Done queue.
- Game.TakeScreenshot(prtsc): mode 0 clipboard (default) / 1 file / 2 both; PrtSc -> frame now, clipboard +350 ms (ours wins over Windows' desktop copy);
  toasts 已複製畫面到剪貼簿 / 畫面已保存：<file>（圖片\AshenAbyss）; test switch --shotmode=N (memory only). Loading tip updated.
- Save.cs Settings.ScreenshotMode (+1 line, SAVE-RESTORE's file). SettingsUi 遊戲 tab row 截圖方式 (hint documents Snipping Tool limitation), Refresh, ResetDefaults, controls row 擷取畫面（預設複製到剪貼簿）.
- README rows updated. Test: tools/fixes/clip.sh (clipboard readback) running.
killed own run.ps1 pid 34108 (fixes_clip0, waiting on lock) 14:21
killed own orphan clip chain pids 35516,60552,28224,58364,20524 14:29
stopped own clip chain pids 29084,60744,8772 14:38 (user playing)
- 7 summary: tools/fixes/dps_summary.txt (worst loadout vs class best, before/after)
- 4 VERIFIED: mapquest 22/22 twice (12:54 and 15:34, both goal=SlayElites = moving elite); run 2 was a no-op (the user's game was open).
- whole-project check.ps1 15:40: BUILD OK, 0 errors.
## Files touched by FIXES
Hud.cs (LabelsYieldToHud on + visibility guards), Ui.cs (uido dropat), Icons.cs (off-hand ghost 3D/raster, gems legacy map),
TestSuite.MapQuest.cs (robust check, --slot honoured), VendorUi.cs (gamble 副手 row + FlexWell), TestSuite.cs (dps bases mode),
Combat.cs (cooldown/minion/DoT hit at class pace), WinKeys.cs (self-test, flag-only simulate, docs), ScreenClipboard.cs (new),
Game.cs (TakeScreenshot modes, toasts, tip, --shotmode), Save.cs (+ScreenshotMode, SAVE-RESTORE's file), SettingsUi.cs (截圖方式 row,
controls row), README.md (PrtSc rows), assets/chars/hero_emberblade_base_skin_n.jpg.import (mipmaps on), 5 emberblade .glb.import touched.
## 16:25 status: still queued (lock held by BESTIARY roster_camp_ember_best, 80 min timeout from 15:46)
- clip.sh (item 8 clipboard/file/both read-back + settings shots) and stuckhunt.sh (item 3, seeds 22..66) are waiting in runq.sh
  (they wait while the user plays; clip.sh only touches the clipboard while the user's game is closed).
  PIDs of these chains: 54636  53228  23264  6328  60596  19620  8772  33904  60076  58544  57696  6532  56564  60096     Stop: taskkill //PID <pid> //F (all FIXES-owned). Rerun: bash Y:/tools/fixes/clip.sh > Y:/tools/fixes/clip.log; bash Y:/tools/fixes/stuckhunt.sh > Y:/tools/fixes/stuckhunt.log
- fm.sh: Forgebody/Lampknight bonus measurement (before = --fistmastery=0 --shieldoath=0 --lanternoath=0)
- op.sh: mace2 overpower check (2H only, 120 s, with/without --no-overpower)
- fair.sh (17:1x): corrected method runs -> tools/fixes/fair_*.txt
- 8 clipboard VERIFIED (17:0x-17:26): mode 0 PrtSc+F12 -> clipboard 1600x900 (read back); mode 1 -> PNG only (log; the clipboard read-back showed a 3840x2092 desktop image put there by something outside the game during that minute, Snipping Tool pid 54792 still open); mode 2 F12 -> PNG + clipboard 1600x900. 0 ERR.
- 18:1x fair_all v1 invalid (model kept previous stance: same fists 3264 vs 2304); suite now Notify()+0.5 s per loadout; fair.sh restarted
- 3 stuck hunt DONE: seeds 22,33,44,55,66 (hunter lv8, --bot --focus, 150 s game at x3): 0 stuck, 0 unstuck, ERR=0 WRN=0 each. Not reproduced.
- fair_lk1h / fair_lk1hc (120 s, pinned): see conversation table; sword1 -9..-11 % vs mace1 on CC-immune; flail -3..-4 %.
- 19:5x switched to short.sh (<=5 min priority runs); fair.sh/fair2.sh/set2.sh stopped (own PIDs)
- 23:xx mace2 kept (0.92 with overpower @lv50 lampknight); 燈誓 6 %; suite pairs every 1H with every off-hand; final.sh queued
- mace2 overpower (lv50, 120 s, 3 samples) with/without: lampknight 0.92/0.87, wildsoul 1.00/0.98 (best 2H), emberblade 0.83/0.81. Overpower worth 2-5 pts. mace2 KEPT.
## 03:15 2026-11-23 FINAL STATE
- Balance code: Hero.cs 拳刃精通 +8 % (dual fist, Forgebody), 盾誓 +0.1 %/1 % shield block (Lampknight 1H+shield), 燈誓 0 % (off: lantern base alone already = weakest 2H);
  Items.cs sword1 crit-dmg implicit 10-35 %; Combat.cs cooldown/minion/DoT hits at class pace; mace2 unchanged.
- UI: tooltips (Items.ClassBonusLine), skill screen 職業特性 (SkillTreeUi, compact fallback at 特大), class intro (Defs.cs), WeaponGuide.Bonuses (燈誓 entry removed).
- Guide table: tools/fixes/balance_baselines.txt (lampknight rows still with 燈誓 6 % until lk2 lands; re-run baselines.sh after lk2).
- Dual sword: off-hand implicit is not the cause (eb lv50 0.87 vs 0.88 with one implicit); DualSpeed 1.1 + per-cast overhead favour dual; emberblade dual axe1 1.01-1.19 (pre-existing).
- STILL QUEUED (behind a campaign from 02:55): ds_hu50_one, tr2 (forgebody 特大 re-shot), lk2 (lampknight 30/50 with 燈誓 0). PIDs: 52456  26732  53160  57752  50916  62924  50556  40836  61876  37620  36600  47400  25060  
  After lk2: bash Y:/tools/fixes/baselines.sh (refreshes the guide table). Stop: taskkill //PID <pid> //F (all FIXES-owned).
- ds_hu50_one DONE: hunter lv50 dual sword1 0.92 of bow with the off-hand implicit removed (vs 1.06 normal): for the crit-heavy Hunter the stacked crit-damage implicit IS most of the premium (Emberblade: no effect).
- tr2 DONE: forgebody skill screen at 特大 shows 職業特性 ◆ 拳刃精通 +8% (compact form), 0 lint flags (shots/fixes/traits_forgebody/traits_s2.png)
- lk2 DONE: lampknight (燈誓 0) all 1H setups <= weakest 2H (mace2) at lv30/50; guide table refreshed (balance_baselines.txt). ALL FIXES RUNS COMPLETE.
## 07:5x 2026-11-23 dual trim + pactbinder
- Items.cs: DualSpeed 1.10 -> 1.05, OffHandImplicitMul 0.5 (Hero.Recalc; tooltip shows halved value 「（副手：效果減半）」 + note on dual-capable weapons).
  Texts follow DualSpeed: Items tooltip, WeaponGuide rules + fist text, WeaponGuideUi, InventoryUi sheet row.
- After trim (lv30/lv50, share of best 2H): hunter dual sword1 0.95/0.98; emberblade dual sword1 0.95/0.87, dual axe1 1.09/0.84 (lv50 2H line noisy).
- 拳刃精通 8 -> 12 %: forgebody fists 0.89/0.91 (dual fist), 0.92/0.89 (starter).
- balance_baselines.txt refreshed (trim_/fm12_/final2_ data).
- Pactbinder: dummy starter DPS lv1/3/5 = 9/22/64 (median 9/24.5/44): not low. Bot lv1 wastes run: ttk 0.8 s, 35.8 kills/min (lowest; median 38.5).
  Change: StarterOff = grimoire (early kit only) -> starter DPS 9/23/66 (+2-3 %). Bot rerun not comparable (different route).

## Forgebody sturdier (鍛體)
- Hero.ForgedBodyDrPct = 8 (innate DmgReduction, added before the 75 % clamp). Defs class intro + SkillTreeUi 職業特性 (one-row merge fallback when two lines don't fit).
- TestSuite dps-bases: ehp line (also for bare), --samegear = one gear seed for all classes. Data: ehp_same.txt / ehp_bare.txt (before), ehp_after.txt.
- Shot: Y:/shots/fixes/traits_forgebody_dr/traits_s2.png. STATUS marker written 14:24.

## Pactbinder early bonus (契約初成)
- Hero.PactEarlyDmgPct 25 / PactEarlyLifePct 15, PactEarlyFrac(level) = (8-level)/7 clamped; applied in Recalc (DmgAll, LifePct). Defs intro line; SkillTreeUi trait while active. Data: pact_early.txt vs low_after_pb.txt.
