# RENDER agent progress (modular gear)

Scratch / tools folder: `Y:\tools\gear\render\` (glbinfo.py: `uv run --no-project python -I glbinfo.py <glb>`).
Test slot 94. No detached processes left running unless noted below.

## 2026-11-21 session 1
- Read AGENT_RULES, CONTRACT. Nothing from SYSTEMS / ART-A / ART-W exists yet (no game/assets/gear, EquipSlot still has `Weapon`).
- Plan:
  1. `GearVisual.cs`: manifest merge, Item→model adapter (reflection on Item.Base/Look so the SYSTEMS switch is one line),
     GearSet (loadout), stances, procedural placeholder weapons, `partial class CharModel` assembly (SetGear).
  2. `GearGripIK.cs`: SkeletonModifier3D that puts the left hand on the second grip of a 2H weapon.
  3. `ItemIcon3D.cs`: offscreen SubViewport renderer, lazy queue, per-frame budget, user:// PNG cache; hooked into
     `Icons.ForItem` (single funnel used by inventory, vendor, stash, tooltips, compare).
  4. Ground loot: one-line hook in `Loot.SpawnItem` (SYSTEMS file, minimal additive change).
  5. Player / HeroPortrait3D / MainMenu char-create use SetGear; `--suite=gear` screenshots.
- [x] GearVisual.cs written: GearAdapter (Item.Base / Look / ItemDb.LookFor / EquipSlot.MainHand+OffHand — SYSTEMS landed
      them already, so the adapter calls them directly), GearSet, Stances, GearCatalog (manifest merge + glb extras + fallback),
      GearGrips (charlib grip/staff_grip/shield_frame ported), CharModel partial (SetGear / armour skinned on the hero
      skeleton / region hiding / weapons on BoneAttachment3D / baked fallback / rarity rim), GearPlaceholders (primitive
      weapons always; --gear-ph bodies + armour cut from today's heroes by bone region, jewels).
- [x] GearGripIK.cs: SkeletonModifier3D, left hand onto grip2 of 2H melee weapons (own two-bone solve, elbow kept on the clip's side).
- [x] ItemIcon3D.cs + Icons.ForItem hook; Loot.SpawnItem hook; Player / HeroPortrait3D / MainMenu.SetHero use SetGear.
- [x] TestSuite.Gear.cs: --suite=gear[:diag|classes[-<class>]|stances|icons|loot|perf]
- [x] diag: grip frames match the baked weapons (emberblade/lampknight wpn_1 0.0°, hunter dagger 0.9°); 8 classes × 7 loadouts assemble, 0 ERR.
- [x] classes / stances shots with --gear-ph (Y:\shots\gear\ph_*.png, sheet_classes_a.jpg, sheet_stances.jpg).
- ART-A delivered hero_lampknight_base + arm_lampknight_*_t4; ART-W wpn_sword1_v1..3. I ran import.ps1 once (17:58).
- Build was broken by others' in-progress edits (InventoryUi / AiPilotPlan) at ~18:05; waiting.
- [x] Real files assemble: hero_lampknight_base + arm_lampknight_*_t4 + wpn_sword1_v1..3 (Y:\shots\gear\sheet_lampknight_inv.jpg:
      bare → partial → full in the inventory portrait). First load of a glb on the main thread cost 186–506 ms → later gear
      changes now wait for ResourceLoader threaded loads (old look kept meanwhile); the first loadout of a model stays synchronous.
- [x] Icons: gloves framed on the right hand only; primitive shield smaller / less shiny.

### Stance table (anim_lib.glb clips)
| stance | loadout | idle | attacks (skill clip → played) | left hand |
|---|---|---|---|---|
| TwoHandMelee | sword2 axe2 mace2 polearm | Sword_Idle | skill's Sword_* kept; Punch_* → Sword_Regular_A; Shield_OneShot → Sword_Attack; Pistol_Shoot → OverhandThrow | IK on grip2 (GearGripIK) |
| TwoHandStaff | staff, scythe2 | Idle_Loop | Spell_Simple_* kept; Shield_OneShot → Sword_Attack | free (staff_grip) |
| Ranged | bow (hand_l), crossbow (hand_r) | Pistol_Idle_Loop | Pistol_Shoot kept; Shield_OneShot → Sword_Attack | bow in it |
| OneHandShield | 1H + shield | Idle_Shield_Loop | Sword_* kept; Punch_* → Sword_Regular_A; Pistol_Shoot → OverhandThrow | shield on lowerarm_l |
| OneHandCaster | 1H + focus/totem/grimoire/lantern, or wand/sceptre alone | Idle_Loop | Shield_OneShot → Sword_Attack; Pistol_Shoot → Spell_Simple_Shoot | off-hand in hand_l |
| DualWield | 1H + 1H | Sword_Idle | Shield_OneShot / Punch_* → Sword_Regular_B | 2nd weapon in hand_l |
| Fists | fist (+ fist) | Idle_Loop | Sword_* → Punch_Jab / Punch_Cross / Melee_Hook | fist in hand_l |
| Unarmed | nothing | Idle_Loop | as Fists | — |
| OneHand | 1H melee alone | Sword_Idle | as TwoHandMelee without IK | — |

Gaps (closest clip used): no two-hand swing clips (Sword_* are one-handed; the IK keeps the 2nd hand on the hilt), no bow
draw/release (Pistol_Shoot), no off-hand / dual-wield left-hand swing (Sword_Regular_B), no lantern-in-left idle
(Idle_Lantern_Loop raises the RIGHT hand, so Idle_Loop), no staff-specific idle or a two-hand staff swing (Idle_Loop / Sword_Attack).
- 18:30 FIX: ART-W exports with export_yup=False, so weapon files are in the contract's axes (+Z up the weapon). Grips no
  longer convert; primitives are built Y-up and wrapped in GearGrips.G2B; grip2 defaults / manifest values used as is;
  icons stand Z-up files up first. ART-W's manifest grip2 values (sword2 -0.165, axe2 -0.36, polearm -0.45 …) are used.
- Ground loot rests on the model's thinnest axis (crossbow on its side, not on its limbs); icons widen thin weapons ≤1.8×.
- Modular meshes LodBias 100 (REGIONS.md: coarse LODs crack at region borders).
- Full run on real art (game_20261121_182819.log): 0 ERR; 59 icons rendered in 0.5 s, 38 read back from disk in 0.34 s;
  8/9 loot drops as own model (the 9th had no file); 24 re-dresses worst 7.6 ms; diag "failures" were the test reading
  before the async loadout applied (fixed: waits for GearPending).
- 18:45 run diag+stances+icons+loot on real art: 74 pass / 0 fail / 0 ERR (game_20261121_184247.log). Stance close-ups:
  Y:\shots\gear\sheet_stances_portrait.jpg. Icons: 80 rendered in 1.3 s, 39 from disk in 0.28 s; worst icon frame 70 ms
  → read-backs now budgeted per frame too; LRU of 40 gear scenes; corrupt disk pictures dropped and re-rendered.
- Perf A/B (Y:\tools\gear\render\perf_ab.sh, results perf_ab_result.txt): demo 140 s, --seed=7, --gear-off vs on.
- Perf A/B #1 (demo 140 s, --seed=7; binary before the LRU / loot-deferral fixes):
    before (--gear-off): avg frame 17.02 ms, avg worst-of-5s 57.4 ms, vram avg 3347 / peak 4513 MB, draws avg 306
    after:               avg frame 17.19 ms, avg worst-of-5s 88.4 ms, vram avg 4030 / peak 5318 MB, draws avg 415
  The demo showed each class's exact gear (dual-wield hunter, wand+focus astromancer, dagger+totem wildsoul …), 0 ERR.
  Worst-frame cause found: ground-loot models were loaded synchronously on drop → now GroundModel shows the old loot mesh
  and swaps in the model when the loader thread is done (SceneTree.ProcessFrame poll); gear scene cache capped (LRU 40).
- 19:15 final real-art run (game_20261121_191444.log): diag+classes+loot+perf 86 pass / 0 fail / 0 ERR. All 8 classes
  modular (ART-A bodies + t4 pieces; lampknight t2). Loot 9/9 own models. 24 re-dresses worst 3.1 ms.
  Sheets: Y:\shots\gear\BEST_classes_1.jpg, BEST_classes_2.jpg (portrait crops), sheet_stances_portrait.jpg, real2\r_inventory_icons.png,
  final\f_ground_loot.png.
- Perf A/B #2 (after the loot deferral): avg frame 16.93 ms (before 17.02), avg worst-of-5 s 65.5 ms (before 57.4, after#1 88.4),
  draws 464 (before 306). VRAM differs with the demo's scene choice (not deterministic even with --seed): after#2 avg 2668 /
  peak 4282 MB vs before 3347 / 4513 MB; so no VRAM regression is visible at this noise level.
- First loadout of a hero (scene start / level load, synchronous on purpose so it never appears naked): 376–500 ms.
- Exit "RID leaked" lines came from my loot fallback mesh orphaned when the model was ready at once → freed now.
- 19:30 loot+icons re-run: 0 ERR / 0 WRN / no engine leak lines. slot94 saves moved to saves_test_backup\render_slot94.*. check.ps1: BUILD OK. No background processes of mine left.
- 19:58 LEAD MUST-FIX icons: per-base framing in ItemIcon3D.Framing (top share of the weapon's length + fill: long weapons
  show the striking end at ~70 %, handle cropped; short pieces / off-hands whole at ~80 %), 38° diagonal, swords/bow widened
  (1.55 / 1.8), thin dark outline + rarity back-glow (Silhouette), dark models lifted ≤1.8×, disk cache Version 8.
  --suite=gear:iconsheet writes Y:\shots\gear\icons64\*.png; Y:\tools\gear\render\icon_sheet.ps1 → Y:\shots\gear\BEST_icons64_sheet.png.
  Armour on the ground: instance tint 0.68. Run game_20261121_195716.log 0 ERR / 0 WRN. slot94 moved to backup again.
- 20:26 gear sets: GearCatalog.PreloadSets (threaded load + material warm-up + pin; scene cap 40 → 56), Player coalesces
  Hero.Changed into one deferred rebuild per frame, CharModel.GearRebuilds counter, --suite=gear:swap. Result: 8/8 swaps one
  rebuild, 0 waits, worst frame 34.2 ms, mean 17.0 ms (game_20261121_202547.log, 0 ERR). Re-run when SYSTEMS' sets land and
  they call PreloadSets (REQUEST in CONTRACT.md).
- 22:55 lead: PreloadSets wired (Game.cs after new Hero; Hero.Sets.cs SaveSet/ClearSet/RenameSet → Hero.PreloadSetModels). Real-set swap test (pre-pool build): 6/6 key swaps one rebuild each, 0 waits, worst 23.2 ms (E swaps were refused by the 1.5 s set cooldown: test now paced). Pooled pieces (pre-built hidden, swap = show/hide) implemented; ItemIcon3D.Version 9. Re-run queued behind AI-PILOT (swap_retry.sh, background bash, writes swap4.txt).
- 23:07 pooled pieces live: CharModel keeps built pieces (≤40) hidden under the skeleton; Player.PrebuildSets builds every saved
  set's pieces one per frame; swap = toggle. Real sets: worst 18.8 ms / mean 16.6 ms, one rebuild per loadout change, 0 waits,
  0 ERR (game_20261121_230625.log); swap+diag 60 pass (diag all pass). Model-cache cap 56 (+16). No background jobs left.
