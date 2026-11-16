# 燼淵 UI review log (UI engineer)

Screens were looked at in real screenshots at 1600×900 (the 1920×1080 design scaled), at 標準 (text scale 0) and
特大 (text scale 2). Raw pictures: `Y:\shots\ui\` (標準), `Y:\shots\ui\x2\` (特大); final set: `Y:\shots\ui\final\`.

## Test harness (all in the UI layer — Ui.cs `UiScript`, UiTheme.cs, MainMenu.cs)
- `--textscale=0|1|2` forces the text size for one run (nothing is saved).
- `--menu=title|newgame|class:<key>|name|load|settings|credits` opens a title sub-screen at boot.
- `--uido=<steps>` or `--uido=@<file>`: scripted interface driver (one command per line in a file, `;`-separated inline).
  Commands: `wait s` · `open id[:arg]` · `close [id]` · `click|rclick|mclick|dbl|down|up|move|hover <x,y | @text[#n]>` ·
  `unhover` · `wheel x,y,n` · `key [Shift+]Name` · `keydown|keyup Name` · `text s` · `shot name` · `dump name` ·
  `menu page` · `scale 0|1|2` · `hud warn|toast|sub|title|flash …` · `give common|magic|rare|legendary|unique|gem|set [kind]` ·
  `gold n` · `mats n` · `xp n` · `points n` · `life pct` · `newgame <class> [level] [area] [paragon] [hc]` · `travel <area>` ·
  `waitplay` · `die` · `include <file>` · `log s` · `quit`.
  Positions are window pixels (= screenshot pixels at 1600×900) or `@text` (centre of the visible button/tab/label with that text).
  `shot`/`dump` write to `--uidir=<folder>` (default `Y:\shots\ui`). `dump` lists every visible text/click control with its
  rectangle and lints it (text trimmed, box smaller than content, off-screen, outside its frame, font below 20 px).
- Shorthands: `--uiclick=x,y;x,y`, `--uikeys=K;K`, `--hover=x,y`.
- Scripts used: `Y:\shots\ui\scripts\*.txt`.
  Example: `Y:\tools\run.ps1 -GameArgs "--win --mute --textscale=2 --uidir=Y:\shots\ui\x2 --uido=@Y:\shots\ui\scripts\all1.txt --quit-at=200" -Timeout 280 -Tag ui`

## Log (screen → problem → fix → after)

### Harness-driven findings that were real bugs
- **Tooltips of item cells / skill-tree nodes / paragon nodes / loot labels polled the OS cursor** (`GetGlobalMousePosition`,
  `GetLocalMousePosition`), which ignores synthetic input and lags when the window is not under the real cursor.
  Fix: `Ui.PointerPos(control)` / `Ui.PointerIn(control)` — the pointer as reported by the mouse events themselves
  (tracked in `Ui._Input`, reset when the pointer leaves the window). Used by InventoryUi (SlotBoard), SkillTreeUi,
  ParagonUi and HudWorld.
- **Item tooltip could cover the hovered cell** (bag cells in the middle columns: the old `TipPoint` fell back to the
  pointer position). Fix: SlotBoard now calls `Ui.ShowTooltipAt(cellRect, …)`: right of the cell, else left of it, the
  comparison card on the far side. After: `shots\ui\i2_tip_*.png`.

### Title screen (標準 `menu_*.png`, 特大 `x2\menu_*.png`)
- Title: fine at 標準. At 特大 the logo grew to 247 px and pushed the menu against the footer → the logo is now marked
  `UiTheme.FixedSize` and follows the text scale only by a third (`MainMenu.LogoScale`).
- Class choice: `--menu=class:<key>` hook added (deferred after the screen's own focus call). Sheet scrolls at 特大 (ok).
- Settings: at 特大 the display page lost its last row behind a scroll bar → the sheet grows with the text
  (`SettingsView.FitSheet`, 900 → max 1010 design px), called on text-scale changes by SettingsUi and MainMenu.

### Shared widgets
- **Tab rows had no visible selected state** (vendors: gold vs. grey lettering only; map and quest log: a modulate).
  New `UiTheme.TabTone(button, active)`: ember face + gold frame + bright lettering for the chosen tab, dark iron for the
  rest. Used by `ItemUi.Tabs` (vendors, character sheet), MapUi and QuestLogUi.

### HUD
- Action slot showed the reason a skill cannot be cast as red text over the glyph ("怒焰不足" permanently on builders'
  spenders in town). Now: dimmed glyph + dull red cell frame; the words stay in the centre-screen warning and are added
  to the slot's tooltip ("目前無法施放：…").
- Evade key cap "空白鍵" ran into the resource globe at 特大 → "空白".
- XP bar was a 6 px near-black strip → 8 px with a bronze frame.
- Level-up banner and area title were hard to read over the level-up light / bright ground → feathered soot veil behind
  both (`HudNotices.Veil`), banner between two hairlines.
- Toasts ran into the area title card → they start below the card while it is on screen.

### Vendors
- Jeweler's title lacked her name → "寶石匠 蔻拉".
- Columns were centred in the frame (visible at 特大 on the merchant: stock shifted right of the title) → left-aligned.

### World tier (特大)
- Cards III/IV scrolled because of the wrapped "以你的等級，野外魔物至少 N 級" line → now a figure row "野外魔物　至少 N 級";
  frame may use 1044 design px of height.

## Day 2 (after the overnight outage)

### Shared look
- **Window ornament**: every heavy frame (`UiTheme.Frame(true)`: all panels, dialogs, pause, settings, memorial card) now
  carries the same restrained ornament — a faint inner hairline, forged corner brackets and a small ember diamond set
  into the top edge (`UiTheme.DrawWindowOrnament`, drawn inside the frame's own padding, nothing allocated).
- **Tooltip accent**: tooltip cards get a thin band along the top edge in the colour of their title (item rarity,
  skill element, gold) — `Ui.AccentOf`; the comparison card takes the worn item's colour.
- **Open animation**: panels fade in over 0.13 s (`Ui.Open`), the backdrop over 0.14 s as before.

### HUD
- **Globe liquid was broken into flat rectangles** (close-up `shots\ui\crop\globe_life.png`): the sin-based noise hash is
  unstable on this GPU. New arithmetic hash, a third octave, lattice kept near the origin. Empty glass now keeps a
  faint tint of the class colour toward the rim, so a builder's empty resource globe no longer reads as a black hole.
  After: `shots\ui\crop\globes_all.png` (all 8 classes).
- Combo points / sigils above the resource globe enlarged (8 px → 10 px radius).
- Buff timers: a permanent buff showed "99+" → nothing under an icon that lasts an hour or more, minutes ("3分")
  above 99 s.
- **Dungeon objective showed a green ✓ on "擊敗〈首領〉" while the boss still stood** (Level.ObjectiveDone is true once the
  dungeon goal is met and the text switches to the next step). The HUD now shows "◆ 擊敗…" in gold while
  `Level.BossMon` is alive and keeps ✓ for a really finished objective.
- Boss frame: the reeling (踉蹌) meter was a dull grey bar → full gold bar with a breathing white core and a halo.
- While a conversation is open the bottom cluster and the corner now step aside (they showed through the dialogue box).

### Settings
- **操作說明 page was broken**: every description wrapped one character per line and both key columns collapsed to the
  left (the scroll page's margin wrapper did not ask to be stretched). Fixed in `SettingsView.Page`. Before:
  see log entry of flow1; after: `final\05_settings_controls.png`.

### Paragon
- The flash line under the boards ("尚未與已點亮的燼紋相連", "〈盤〉已接續…") ran over nodes and board titles → dark plate
  with hairlines behind it.
- Verified by clicking: light a node, right-click refund, drag the board, click the open gate slot, choose a board
  (接續此盤), light the new board's gate and walk to its socket, 嵌入 a glyph.

### Credits
- The roll was cut in half by the clip line at the bottom → the bottom fade is opaque by the clip line.

### Verified interactions (driver clicks; see the game logs of the runs)
- Inventory: sheet tabs, equip by left click, unequip by right click, Shift+click junk mark, 整理, Esc closes.
- Vendors: buy, sell (with confirm for precious items), buy back, 賭運, salvage selection + confirm, 精鑄, 附魔 (reroll and
  take), 銘刻, socket a gem, 合成, gem shop, stash in/out, every tab.
- Skill tree: learn, enhancement, branch, passive, refund (right click), spec change, bar assignment (arm slot, click
  skill), clear slot, 重置全部 confirm, Esc.
- Maps: area/world tabs, wheel zoom, hover cards, travel by clicking a lit region (town → 灰燼荒原 loaded).
- Quest log / renown tabs; dialogue advance and Esc skip.
- Pause → 設定 (all four tabs, live 介面文字大小 標準→特大→標準, settings.json unchanged afterwards) → Esc back to pause →
  返回標題畫面 → confirm → title.
- Title: load screen with filled / hardcore-dead / empty slots, Delete confirm, name screen slot choice with a taken
  slot, 專家模式 toggle, overwrite confirm, credits roll and Esc.
- Death: 重生 respawns at the checkpoint; hardcore death shows the memorial with only 返回標題畫面.

### Final pass (day 2, second half)
- `final.txt` drives the whole game in one run and produced the set in `Y:\shots\ui\final\` (see the list at the end);
  `fixups.txt` re-shot the handful the first pass missed; `combat2.txt` is the bot fight.
- Occultist 銘刻: the codex listed all 17 aspects in book order, so a hero's one recorded aspect sat somewhere in the
  middle of a grey list → recorded aspects come first.
- Driver lesson: `@text#n` counts labels as well as buttons, so action buttons that share their text with a tab or a
  caption (精鑄, 淬火, 嵌入) are clicked by coordinate or by their longer state text (嵌入選定的槽).

## Still imperfect / worth another look
- Class sheet of 縛淵術士 (the longest texts) needs a few pixels of scrolling at 標準; every class sheet scrolls at 特大.
- The area-title veil is a soft band rather than a true radial glow (kept allocation-free).
- Character silhouette in the inventory is a simple line drawing; a rendered portrait would look better (needs a
  SubViewport with the hero model — rendering work).
- 特大: hover points of the scripts are tuned for 標準, so the 特大 pass checks layout, not every tooltip.
- 力量/智力… attribute tooltips, rift panel when unlocked (needs the campaign flag) and the ending narration were not
  driven; the rift panel was reviewed in its locked state only.

## Requests to other owners
- QA / gameplay — `Loot.cs FloatText`: the "+137" gold pick-up number is a Label3D of 56 px × 0.0085 m and comes out about
  twice as tall as the HUD's damage numbers (it covers loot labels, see `final\10_hud_loot_labels.png`). Suggest
  `PixelSize ≈ 0.0045` or routing it through `Hud` as a floating number.
- QA / gameplay — `Level.ObjectiveDone` is true while the text reads "擊敗〈首領〉"; the HUD works around it through
  `Level.BossMon`, a separate "goal met / place cleared" flag would be cleaner.
- QA / harness — `Game.KnownSwitches` only matters for switches given WITHOUT the `--` separator; the UI switches
  (`uido`, `uidir`, `uiclick`, `uikeys`, `hover`, `menu`, `textscale`) work as user args today. Add them to the list if
  you want them accepted in the forgiving form too.
- Graphics — NPCs in town still stand in T-pose behind every panel screenshot; hero stand-ins for 鍛身者 / 誓燈騎士 on the
  class screen are the 燼刃衛 model.

## State at the 09:50 pause (so the work survives an interruption)
- DONE at 標準: every screen shot and reviewed; final set in `Y:\shots\ui\final\` (94 PNG; layout dumps in `final\dumps\`).
- DONE after the first final pass (all re-shot into `final\`):
  - HUD tooltips of the bottom cluster (skills, potion, evade, XP bar, buffs) now open ABOVE the control
    (`Ui.ShowTooltipAbove`); they used to cover the action bar or a globe.
  - Area-title and level-up veil is one smooth radial gradient texture (built once) instead of stepped rectangles.
  - Crafting cost lines: with three or more kinds of payment the materials go on their own line, so
    "幽塵 2（持有 301）" is never broken in the middle.
  - Enemy nameplates no longer write over each other when champions stand shoulder to shoulder (the later plate is
    skipped unless the pointer is on that enemy; its health bar stays).
- OPEN: one more pass of `final.txt` at 特大 into `Y:\shots\ui\x2final\` to confirm the last fixes at the largest text.
- My mistake to remember: at about 09:25 a patch script split a C# string literal over two lines in
  `InventoryUi.cs` (`sb.Append("⏎…")`) and the project did not compile until I repaired it; other engineers' builds
  failed meanwhile. Rule from now on: `Y:\tools\check.ps1` immediately after every multi-line edit, and no edits while
  one of my own runs is queued.

## Finished (after the resume)
- 特大 pass of the whole script: `Y:\shots\ui\x2final\` (76 PNG + dumps), contact sheets in `Y:\shots\ui\crop\x2final\`.
  Lint: 0 real findings (the 8 flags on the slot buttons of the name screen were the lint measuring a two-line button
  as one line; the lint now takes the widest line). Checked by eye: title (logo no longer oversized), class sheets,
  load screen, settings (display page fits without scrolling, controls page two columns), HUD, inventory, all vendors,
  skill trees, maps, quest log, dialogue, world tier (cards no longer scroll), paragon, death.
- From that pass: a crafting cost line could still break inside "（持有 300）" in the narrower 特大 column → gold and
  materials are always on separate lines now. Re-shot at 標準 (`final\12_vendor_smith_masterwork.png`,
  `…occultist_enchant.png`, `…occultist_imprint.png`, `…jeweler_socket_cost.png`).
- Last re-shoots with the final look: `10_hud_hunter_combo`, `10_hud_pactbinder_sigils`, `21_hud_boss`,
  `21_hud_boss_staggered`, `21_hud_dungeon_objective`.
- Checks at the end: project compiles (check.ps1: BUILD OK); 0 errors and 0 `[icons]` warnings in the logs of all UI
  runs of the day (no placeholder icon on any visited screen); `settings.json` identical to the copy taken before the
  settings tests; save folder holds only the harness slot (the test slots 1–3 the scripts created were removed by
  the scripts themselves).

## Final screenshot set — `Y:\shots\ui\final\` (標準, 1600×900)
- `01_title`, `01_title_continue` · `02_class_<8 classes>` · `03_name_entry`, `03_name_hardcore_slots` ·
  `04_load_empty`, `04_load_filled` · `05_settings_display|audio|game|controls` · `06_credits`
- `10_hud_town`, `10_hud_notices`, `10_hud_area_title`, `10_hud_levelup`, `10_hud_low_life`, `10_hud_loot_labels`,
  `10_hud_globe_readout`, `10_hud_skill_tooltip`, `10_hud_potion_tooltip`, `10_hud_xp_tooltip`, `10_hud_buff_tooltip`,
  `10_hud_hunter_combo`, `10_hud_pactbinder_sigils`
- `11_inventory`, `11_inventory_offense|defense|utility`, `11_tooltip_common|magic|rare|legendary|unique|sockets|gem|equipped`
- `12_vendor_merchant`, `12_vendor_merchant_gamble`, `12_vendor_smith_salvage`, `12_vendor_smith_salvage_confirm`,
  `12_vendor_smith_masterwork`, `12_vendor_smith_temper`, `12_vendor_occultist_enchant_pick`,
  `12_vendor_occultist_enchant`, `12_vendor_occultist_imprint`, `12_vendor_occultist_imprint_done`,
  `12_vendor_jeweler_socket_cost`, `12_vendor_jeweler_socket`, `12_vendor_jeweler_combine`, `12_vendor_jeweler_shop`,
  `12_vendor_stash`
- `13_skills_emberblade|astromancer|pactbinder` · `14_map_area`, `14_map_world`, `14_map_travel` · `15_quest_log`,
  `15_renown` · `16_dialogue`, `16_dialogue_intro` · `17_world_tier` · `18_rift` · `19_pause`, `19_pause_confirm`,
  `19_settings_ingame` · `20_paragon`, `20_paragon_board_choice`, `20_paragon_glyph_socketed`
- `21_hud_boss`, `21_hud_boss_staggered`, `21_hud_dungeon_objective` · `22_death`, `22_death_hardcore`
- HUD in combat (bot hunting the marked elites of 荒棄地窖): `30_hud_combat_01` … `30_hud_combat_14`
  (`_13_alt` with Alt held). Best single picture: `30_hud_combat_02`.
- Other folders: `Y:\shots\ui\` working shots of both days, `Y:\shots\ui\x2\` first 特大 pass (before the fixes),
  `Y:\shots\ui\x2final\` last 特大 pass, `Y:\shots\ui\crop\` close-ups and contact sheets, `Y:\shots\ui\scripts\` the
  driver scripts (`final.txt` regenerates the whole set).
