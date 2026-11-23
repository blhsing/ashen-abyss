# PROGRESS — SYSTEMS (modular gear)

## 2026-11-21
- [x] Defs.cs: EquipSlot MainHand..OffHand (OffHand appended = 9), ItemKind.OffHand (appended), Stat.Block.
- [x] Items.cs: Item.Base / Look / Implicits; GearBase table (22 bases); ClassGear loadouts (§3); generation by base; 2H affix ×1.5;
      off-hand affix pools; RollLook / LookFor; Migrate (0.1–0.3 weapons → class legacy base, DPS preserved); tooltip.
- [x] Hero.cs: MainHand/OffHand, TargetSlot/WhyCantEquip/Equip (2H clears off hand, bag-full refusal), dual-wield alternate, SpeedMul.
- [x] Save.cs: Equip length 10, SaveSystem.MigrateGear (+ legacy companion off-hand).
- [x] CONTRACT.md: SYSTEMS API line published.
- [ ] Combat block + strike hand, SkillExec NextStrike, Loot/vendors/crafting, InventoryUi, Demo/AiPilot/TestBot, tests.
- bot runs: Y:/tools/gear/systems/bots.sh (background bash; results bots_result.txt; stop: kill the bash + the Godot it started)
- [x] Combat.cs block (Stat.Block, blocked hits ×0.5) + strike hand; SkillExec.Fire calls Hero.NextStrike (multi-hit melee alternates per hit).
- [x] Loot.cs: weapon pity uses Hero.Weapon; loot mesh by LegacyFamily; merchant shelf = 2H + 1H + off-hand (or 2nd 1H) of class bases (MakeBase).
- [x] Crafting.cs: OffHand in temper recipe kinds; bag sort weapons → off-hands, by base. Salvage +1 scrap and sell ×1.15 for 2H.
- [x] InventoryUi.cs: two hand cells under the niche (主手 / 副手), off hand greyed + rimmed + "已鎖定" tip while 2H; Alt+click → off hand;
      2H equip moves off-hand piece to bag (message) or refuses "行囊已滿，無法卸下副手"; compare tooltip shows both worn pieces; sheet: block row, swings/s.
- [x] VendorUi.cs: worn row 10 cells in hand-first order, blocked off hand dimmed; gamble row label 武器 (no off-hand gamble row: 8-row layout).
- [x] UiTheme.cs (lead's file, additive): tooltip compare passes Hero.ComparedExtra.  Icons.cs (additive): OffHand items use their base icon family.
- [x] Game.cs AutoGear (--gear) rolls hands by style; new switch --hands=2h|dual|off|1h. DemoMode cycles styles (least shown first). TestBot/AiPilot: Hero.IsUpgrade.
- [x] TestSuite.Items.cs (--suite=items): migration of 8 old-format fixtures (Y:/tools/gear/systems/fixtures), hands rules, loot spread, base DPS table. 111/111.
- [ ] bot runs per class × style (bots.sh running)

## 2026-11-21 (lead's added requests)
- [x] GearEval.cs: shared evaluator (simulated swap via Hero copy + real Recalc; offence DPS / defence EHP; verdict rule; weighting 平衡/偏重攻擊/偏重防禦 in Settings.GearWeight + SettingsUi 遊戲 tab row). Tooltip line 「升級：攻擊 +x%　防禦 −y%」, 含特殊效果, 無法使用. Used by filter/presets, TestBot, AiPilot (Hero.IsUpgrade kept as a wrapper for AiPilotPlan.cs).
- [x] Hero.Marks.cs: BagMark (回收 = D.Junk, 丟棄 = D.Discards), Item.Locked, MarkAll/ClearMarks/ToggleLock/DiscardMarked, BagFilter + presets.
- [x] InventoryUi: 篩選 panel in the doll's place (F), rarity toggles, kind/class/verdict/power/affix drop-downs, 僅新物品, presets, bulk mark/clear, badges 收/棄/鎖, 回收 (smith, materials confirm) and 丟棄 (rarity-count confirm) buttons, hotkeys R/X/L/F/C/Shift+R/X. SettingsUi controls list: 行囊 groups. VendorUi: 回收 naming, locked never bulk-picked.
- [ ] tests (--suite=items extended), screenshots in Y:\shots\gear\

## Design table (damage per second at power p: WA(p) = 5 + 0.42 p; 2H = 100%, 1H = 90%; one hit = DPS / speed ± spread)
| base | name | hands | speed (aps) | spread | implicit |
|---|---|---|---|---|---|
| sword1 | 劍 | 1H | 1.20 | ±20% | crit dmg 5–25% |
| axe1 | 斧 | 1H | 1.10 | ±30% | vulnerable dmg 5–25% |
| mace1 | 錘 | 1H | 1.00 | ±25% | overpower 8–40% |
| dagger | 匕首 | 1H | 1.40 | ±15% | crit chance 1–4% |
| wand | 魔杖 | 1H | 1.30 | ±20% | resource gen 3–12% |
| scythe1 | 鐮刃 | 1H | 1.15 | ±30% | life on hit 1–14 |
| flail | 連枷 | 1H | 1.05 | ±40% | dmg vs CC 5–25% |
| fist | 拳刃 | 1H | 1.45 | ±15% | attack speed 2–6% |
| sceptre | 權杖 | 1H | 1.10 | ±20% | CDR 2–6% |
| sword2 | 巨劍 | 2H | 0.95 | ±20% | crit dmg 10–50% |
| axe2 | 巨斧 | 2H | 0.90 | ±30% | vulnerable dmg 10–50% |
| mace2 | 巨錘 | 2H | 0.80 | ±25% | overpower 16–80% |
| staff | 法杖 | 2H | 1.00 | ±15% | core skill dmg 8–40% |
| polearm | 長柄刀 | 2H | 0.95 | ±25% | elite dmg 8–40% |
| scythe2 | 巨鐮 | 2H | 0.95 | ±30% | life on hit 2–28 |
| bow | 長弓 | 2H | 1.15 | ±15% | basic skill dmg 8–40% |
| crossbow | 弩 | 2H | 1.00 | ±25% | crit dmg 10–50% |
| shield | 盾 | off | — | — | block 14–24% (+armour 55% of a chest) |
| focus | 法器 | off | — | — | damage 6–15% |
| totem | 圖騰 | off | — | — | damage 6–15% |
| grimoire | 魔典 | off | — | — | damage 6–15% |
| lantern | 燈 | off | — | — | barrier 4–14%, fire dmg 4–12% |
2H affixes ×1.5. Dual-wield: speed = mean × 1.1, hands alternate per attack (per hit for multi-hit melee). Swing speed of skills =
stat speed × (weapon aps / class pace RefAps), clamped 0.7–1.35 (damage per hit carries the rest). Block: blocked hit −50%, cap 50%.

## 2026-11-21 (lead: weapon-class compatibility)
- [x] ItemDb.UsableBy / HowHeld / LoadoutLines (single source). Tooltip: red 「⊘ 你的職業無法使用」 under the name, 「可用職業：」 8 classes (own gold+bold, usable bright, others dim+struck), 「<class>：雙手武器／主手／主手／可雙持／副手」.
- [x] ItemUi.Slot: unusable weapon/off-hand → red veil + red rim + ⊘ corner badge, no hover glow (bag, stash, vendor, crafting pickers — every ItemUi.Slot).
- [x] Ground loot label: 「⊘ 名稱」 in a dimmed rarity colour. Loot: 10% of weapon/off-hand drops (kind not forced) are another class's base (ForeignShare); pity/vendor/gamble never.
- [x] Merchant: 「只顯示本職業可用」 switch (on by default, session-wide), shelf gets 1–2 other-class weapons; hidden count shown.
- [x] Character sheet 總覽: 「可用武器」 section + 目前持握. Main menu class intro: 「可用武器」 row from LoadoutLines (role line no longer carries the old 武器 text).
- [x] Stash hint: 「其他角色可用：名（職業）」 from SaveSystem.List() (read once per vendor visit). NOTE: the stash is per character in this codebase (SaveData.Stash), not shared.
- [x] Equip refusal: 「燼刃衛無法裝備魔杖」 in the panel + HUD warn toast. Drag-and-drop: the inventory has no dragging (click to equip), so no drag highlight.

## 2026-11-21 (lead: tooltip weapon stats, gear sets)
- [x] Tooltip: 「巨斧・雙手武器」/「斧・單手（可雙持）」/「盾・副手」; 每秒傷害（此武器）, 每擊傷害, 每秒攻擊 each with deltas; 「雙持後／改為雙手／改為單持：每秒攻擊 x（±y%）　每擊 a–b」 from GearEval.Simulate; plain rules (2H ×1.5 affixes, 1H 90%, dual +10%); shield block delta; focus/totem/grimoire/lantern main stat big; 裝備組 membership line.
- [x] Gear sets: Hero.Sets.cs (data in save, SaveSet/LoadSet/ClearSet/RenameSet/PreviewSet/MissingOf, IsProtected), GearSets.cs (F1–F6, E, 1.5 s shared cooldown for keys and in combat, toasts with ⚠ missing, HUD line bottom-left, Rebind/ResetKeys, KeyCapture, PrepareDemo for tests). Inventory: strip of 6 under the class line (left click load, right click 儲存/載入/重新命名/清除), sheet tab 裝備組 with stance + 每秒傷害/生命/護甲/抗性 as if worn and deltas vs worn. SettingsUi: rebindable key grid + reset. Ui.cs: hotkey hook (also while inventory is open) + UI-script command "sets". Protection hooked into marks, recycle/discard lists, smith bulk pick, TestBot, AiPilot, AiPilotPlan.

## Test results (2026-11-21)
- check.ps1: 0 errors (whole project).
- --suite=items: 147/148 on the last completed run (the one failure — set skill bar not cleared for empty entries — fixed in Hero.Sets.cs; re-run queued as final3.sh behind the AI-PILOT campaign run). Earlier runs 139/139.
- --suite=systems: 187/187 (after the gamble test learned that classes without off-hands are never offered one).
- Bot runs (bots.sh, slot 92, lv14 wastes, --gear --hands=<style> --bot, 75 s): 16/16 class×style runs ERR=0, deaths 0. Results: bots_result.txt, per-run stdout in runs/.
- Migration: 8 old-format fixtures (4 real old test saves + 4 class-swapped copies) load with nothing lost; per-weapon and hero damage per second preserved; second load idempotent.
- Loot spread (3000 rolls/class, lv30 champion): every base of every loadout drops; ~6–10% of weapon drops are other classes' bases (⊘).
- Screenshots: Y:\shots\gear\sys_*.png.
- Background helpers: bots.sh, runwait.sh, final*.sh in Y:\tools\gear\systems (no detached processes left once final3.sh ends).
- DONE: final3.sh finished — --suite=items 148/148 after the set-bar fix (no background processes left).
  slot92 files were already moved to saves_test_backup.

## 2026-11-22 武器指南 (weapon guide)
- New: WeaponGuide.cs (authored notes per base, class notes, class bonuses looked up by passive name, stance groups, alternatives, data-generated comparisons, rules), WeaponGuideUi.cs (panel "guide": class selector, list by stance with 3D icons, detail with data stats, worn card), TestSuite.Guide.cs (--suite=guide, 17/17).
- Small approved edits: Ui.cs (register "guide"; G: hovered weapon/off-hand → that base, else class page; G again closes), InventoryUi.cs (SlotBoard.HoveredItem; 「武器指南（G）」 button under 可用武器), SettingsUi.cs (controls row G), MainMenu.cs (武器指南 button in the class intro), Items.cs (tooltip hint 「G：武器指南」), TestSuite.cs (dispatch).
- Screenshots: Y:\shots\guide\1600x900 and \1920x1080 (rules, polearm with worn card, shield, fist, three text sizes; class intro). Slot 96 (lead-assigned) cleaned after each run.
- 2026-11-23: tooltip 可用職業 now lists only classes that can use the base (hero's own gold), joined with 、, wrapped by hand at 、 (≤14 glyphs/line). Shots Y:\shots\guide\compat (axe2 unusable / sword1 usable × 3 sizes, Soulbinder). items 149/149. Test-only: Ui.cs give command 'base <id> [rarity]'.
