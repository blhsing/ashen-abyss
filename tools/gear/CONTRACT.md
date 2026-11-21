# Modular gear — shared contract (lead, 2026-11-21)

The user's request (verbatim):
- "instead of raster images, all wearable/wieldable equipments should be modeled like the heroes are. the rendering of a hero should reflect the exact set of equipments they're wearing and wielding. bring the weapon system up to parity with the original game, which supports main-hand weapons, off-hand weapons/items, two-hand weapons and dual-wield weapons, depending on classes."
- "a hero without any equipment should be barely clad if not naked"

Four agents work in parallel, and each owns its own files. This file is the interface between them. If you need to change it, append a dated "CHANGE:" line at the end and say so in your report. Don't silently diverge.

| Agent | Owns |
|---|---|
| SYSTEMS (Opus) | Item data and rules: `Defs.cs`, `Items.cs`, `Loot.cs`, `Hero*.cs`, `Combat.cs` damage paths, save/load, `InventoryUi.cs`, `VendorUi.cs`, crafting, `AiPilot.cs`, `TestBot.cs`, `DemoMode.cs` gear picks |
| RENDER (Opus) | `CharModel.cs` hero assembly, new `GearVisual.cs`, `HeroPortrait3D.cs`, new `ItemIcon3D.cs` (3D icons), ground-loot visuals, stance/animation choice. Touches `InventoryUi`/`VendorUi`/tooltips only to swap the icon source, through one call `ItemIcon3D.Get(item, size)`; coordinate with SYSTEMS by message if needed. |
| ART-W (Fable xhigh) | Weapon and off-hand models: Blender scripts under `tools/blender/gear/weapons/`, output in `game/assets/gear/weapons/` |
| ART-A (Fable xhigh) | Barely-clad base bodies and skinned armor pieces: scripts under `tools/blender/gear/armor/`, output in `game/assets/gear/armor/` and `game/assets/chars/hero_<class>_base.glb` |

## 1. Slots
`EquipSlot`: MainHand, OffHand, Helm, Chest, Gloves, Pants, Boots, Amulet, Ring1, Ring2.
- A two-hand weapon occupies MainHand and blocks OffHand.
- Old saves map `Weapon` to `MainHand` without any loss.

## 2. Weapon bases (id: name, hands)
1H: `sword1` 劍, `axe1` 斧, `mace1` 錘, `dagger` 匕首, `wand` 魔杖, `scythe1` 鐮刃, `flail` 連枷, `fist` 拳刃 (one per hand), `sceptre` 權杖.
2H: `sword2` 巨劍, `axe2` 巨斧, `mace2` 巨錘, `staff` 法杖, `polearm` 長柄刀, `scythe2` 巨鐮, `bow` 長弓, `crossbow` 弩.
Off-hand (OffHand only): `shield` 盾, `focus` 法器 (orb or crystal), `totem` 圖騰, `grimoire` 魔典, `lantern` 燈 (lampknight).

## 3. Class loadouts (what each class may equip)
| Class | Two-hand | Main hand | Off hand | Dual-wield |
|---|---|---|---|---|
| Emberblade 燼刃 | sword2 axe2 mace2 polearm | sword1 axe1 mace1 | — | yes: two of sword1/axe1/mace1 |
| Astromancer 星術師 | staff | wand dagger sceptre | focus | no |
| Hunter 獵手 | bow crossbow | sword1 dagger | — | yes: two of sword1/dagger |
| Soulbinder 縛魂者 | scythe2 sword2 | scythe1 sword1 dagger wand | shield focus | no |
| Wildsoul 野魂 | staff mace2 axe2 | mace1 axe1 dagger | totem | no |
| Forgebody 鍛軀 | mace2 axe2 | fist | — | yes: fist + fist (the classic stance) |
| Lampknight 燈騎士 | sword2 mace2 polearm | sword1 mace1 flail | shield lantern | no |
| Pactbinder 契約者 | staff scythe2 | wand dagger sceptre | grimoire focus | no |


SYSTEMS designs the stats in the original game's spirit:
- Two-hand weapons have higher damage per hit and a larger affix budget.
- Dual-wield alternates hands; attack speed comes from both weapons.
- Off-hands carry defensive or caster affixes; a shield adds armor and block chance.
- Each base has its own attack speed and range class.
- The current class-locked uniques are remapped onto a fitting base.

## 4. Armor looks
Each class has its own armor (fit is per body). For each slot (helm, chest, gloves, pants, boots) there are **4 looks**:
- `t1` ragged / scavenged;
- `t2` plain soldier or acolyte;
- `t3` fine / ornate;
- `t4` the class's current signature look, split out of today's hero model so nothing gets worse.

Model id: `arm_<class>_<slot>_<t1..t4>`, file `game/assets/gear/armor/arm_<class>_<slot>_<tN>.glb`.
- The armor is skinned to the SAME skeleton (bone names) as `hero_<class>.glb`.
- Mesh parts the armor covers must hide or not intersect the base body.
- Each glb may set `hide_body_regions` metadata (e.g. "torso", "upper_arms", "hands", "feet", "head_hair") so RENDER can hide the matching body-mesh parts. ART-A defines and documents the region list in `game/assets/gear/armor/REGIONS.md`.

Look chosen for an item: SYSTEMS gives each item a `Look` string.
- Default: by item level band and rarity (common/magic → t1/t2; rare → t2/t3; legendary/unique → t3/t4).
- Uniques may name a specific look.

Base body `hero_<class>_base.glb`:
- Same skeleton, face, hair and proportions as today's hero.
- Barely clad: a plain linen loincloth or short wraps; a chest wrap on female bodies. No armor, no weapon. Tasteful, no explicit nudity.
- Split into named body region meshes (`body_torso`, `body_upper_arms`, `body_forearms`, `body_hands`, `body_thighs`, `body_shins`, `body_feet`, `body_head`, `hair`) so armor can hide what it covers.

## 5. Weapon and off-hand models
Model id: `wpn_<base>_<v1..v3>`, file `game/assets/gear/weapons/wpn_<base>_<vN>.glb`. The variants are:
- v1 crude;
- v2 forged;
- v3 ornate or glowing (emissive allowed).

That gives 22 bases × 3 = 66 models.

Each glb is a rigid mesh:
- The origin is at the grip centre, using the existing charlib convention: +Z up the weapon, +X toward the edge or knuckles, +Y the palm normal.
- Real-world scale in metres.
- Metadata: `grip` ("r", "l" or "both"); for 2H, `grip2_offset` (local position of the second hand); `length_m`.

Special cases:
- Shields: origin at the forearm strap centre, +Y facing outward, attached to `lowerarm_l` (RENDER handles this with the metadata `mount=forearm_l`).
- Bows: origin at the grip.
- Foci, grimoires, totems and lanterns: held in `hand_l`.

Materials are PBR jpg texture sets in the project's style (same as the hero textures). Budget: 1–6k tris per weapon.

## 6. Manifest
`game/assets/gear/gear_manifest.json` holds one entry per model:
```json
{ "id": "wpn_sword1_v2", "kind": "weapon", "base": "sword1", "file": "res://assets/gear/weapons/wpn_sword1_v2.glb", "mount": "hand_r", "grip2": [0,0,-0.35], "length": 0.95 }
{ "id": "arm_emberblade_chest_t3", "kind": "armor", "class": "Emberblade", "slot": "chest", "file": "...", "hides": ["torso","upper_arms"] }
```
- ART-W and ART-A each write their own entries: `gear_manifest_weapons.json` and `gear_manifest_armor.json` (two files, so they never conflict). RENDER merges them at load.
- `game/assets/gear/` is binary assets. Add it to the git-ignored asset list exactly as `game/assets/chars` is handled (check `.gitignore` and `tools/release/release_*.sh`, but tell the lead; don't edit release scripts).

## 7. Rendering rules (RENDER)
- The hero is assembled as: base body + the armor piece of each equipped armor slot (body regions hidden per `hides`) + the weapon(s) on bones.
- Empty slot: the body part shows (barely clad). Missing model file: fall back to the nearest available look, then to today's baked hero, so the game always works while art is incomplete.
- Stances and animation sets follow the loadout: 2H melee, 2H staff, 2H ranged, 1H+shield, 1H+caster off-hand, dual-wield, unarmed. Pick from the existing animation library (see `tools/blender/heroes` and `CharModel.cs`), and report gaps.
- Everywhere a hero appears it must reflect the exact equipment: the game world, the inventory 3D hero (`HeroPortrait3D`), character select, the demo and the AI pilot.
- Item icons: 3D render-to-texture of the item's own model (cached per model id, size and rarity), replacing raster icons in the inventory, vendor, stash, tooltips, loot labels and the action bar. Gems, rings and amulets have no body model; ART-W also makes small models for them (`jwl_ring_v1..3`, `jwl_amulet_v1..3`, `gem_<kind>`), so every icon is 3D.
- Ground loot drops show the item's 3D model lying on the ground.

## 8. Shared rules
- Read and obey `Y:\tools\AGENT_RULES.md`.
- Use Blender only through `run_blender.ps1` (background).
- Don't commit, push or release.
- Test slots: SYSTEMS 92, RENDER 94, ART-W 95, ART-A 96.
- Append progress to `Y:\tools\gear\PROGRESS_<agent>.md`.

SYSTEMS API (2026-11-21, compiles):
- `enum EquipSlot { MainHand, Helm, Chest, Gloves, Pants, Boots, Amulet, Ring1, Ring2, OffHand }` — order kept for save compatibility (old `Weapon` = index 0 = `MainHand`; `OffHand` appended = 9). Use names, never ordinals; `D.Equip.Length == 10`.
- `enum ItemKind { Weapon, Helm, Chest, Gloves, Pants, Boots, Amulet, Ring, Gem, OffHand }` — `Weapon` = any wielded weapon, 1H or 2H (dual-wielders hold a `Weapon` in `EquipSlot.OffHand`); `OffHand` = shield/focus/totem/grimoire/lantern.
- `Item.Base` (string): weapon/off-hand base id exactly as §2 (`sword1` … `lantern`); armour `helm|chest|gloves|pants|boots`; `ring|amulet`; gems their kind (`ruby` …).
- `Item.Look` (string): model id. `wpn_<base>_v1..v3` (weapons AND off-hands, e.g. `wpn_shield_v2`), `arm_<class>_<slot>_t1..t4`, `jwl_ring_v1..3`, `jwl_amulet_v1..3`, `gem_<kind>`. Armour looks are stored with the class the piece was made for: ALWAYS resolve with `ItemDb.LookFor(item, heroClass)` (swaps the class key for the wearer).
- `Item.Hands` (0/1/2), `Item.TwoHand`, `Item.Gb` (GearBase: `Id, Name, Hands, Off, Aps, Spread`), `Item.BlockChance`.
- `Hero.MainHand`, `Hero.OffHand` (the items in those slots), `Hero.Weapon` (main-hand weapon, or a lone off-hand weapon), `Hero.OffHandBlocked` (2H in main hand), `Hero.DualWielding`, `Hero.StrikeHand` (0 main / 1 off; flips once per attack via `Hero.NextStrike()`, called in `SkillExec.Fire`) — use it to pick which hand swings.
- `ItemDb.Loadout(ClassId)` → `ClassGear { TwoHand, MainHand, OffHand, Dual, Starter, StarterOff }` = the §3 table in code; `ItemDb.Allows(cls, baseId, EquipSlot.MainHand|OffHand)`.
- Stance from the loadout: `MainHand?.TwoHand` → 2H (bow/crossbow ranged, staff caster, else melee); `DualWielding` → dual; `OffHand?.Kind == ItemKind.OffHand` → 1H + shield/caster (by `OffHand.Base`); `Weapon == null` → unarmed.

## RENDER notes (2026-11-21)
- RENDER reads, per model, in this order: the manifest entry (`gear_manifest*.json`: array, `{"entries":[…]}`, object keyed by id, or one JSON object per line all work), then the glb's node extras (`mount`, `grip2_offset`, `hide_body_regions`, as ART-A / ART-W already write them), then defaults. A file under `res://assets/gear/` is used only once imported (`Y:\tools\import.ps1`).
- Grip frames are charlib's `grip()` / `staff_grip()` / `shield_frame()` ported to C# (`GearGrips` in `game/src/GearVisual.cs`), checked against the baked heroes' weapons (0° off). Bone-local frames are the Blender ones; the weapon's Blender +Z ends up Godot +Y, which RENDER handles.
- REQUEST RENDER→ART-W: (1) For every 2H base write `grip2_offset` [x,y,z] (Blender weapon space, metres) in the node extras or the manifest; otherwise RENDER guesses -0.15 on Z for `sword2`, -0.34 for hafts and +0.32 for `staff`. (2) Crossbow: origin at the grip, barrel along +X, the grip running down -Z. Bow: held in `hand_l`, +Z up the limbs, +X toward the target, string on -X. (3) Put `jwl_*` / `gem_*` in `game/assets/gear/jewels/` (RENDER also looks in `weapons/`). (4) A 1H model with `mount: hand_r` is mirrored into `hand_l` automatically when it is dual-wielded. You don't need a separate file for that.
- REQUEST RENDER→ART-A: none blocking. RENDER hides a body mesh when its node name (minus `body_`) starts with a region in `hide_body_regions` (`hair`/`head_hair` → `hair`; `arms` → upper_arms+forearms; `legs` → thighs+shins). Defaults when a file sets nothing: chest → torso, upper_arms, wrap_chest; pants → hips, thighs, wrap_loin; gloves → hands; boots → feet; helm → nothing.
- RENDER small edits in others' files (all additive): `Loot.SpawnItem` (ground visual from `GearCatalog.GroundModel`, the old mesh otherwise), `Icons.ForItem` (3D first, old body moved to `ForItemRaster`, plus `MarkRaster`), `Player.cs` (`UpdateWeaponTier` → `SetGear`, stance idle, `Stances.Remap` of the skill clip), `MainMenu.SetHero` (starter gear), `Game.KnownSwitches` (`gear-off`, `gear-baked`, `gear-ph`, `gear-dir`, `icon3d-off`), `TestSuite.cs` (one `case "gear"`).
SYSTEMS API (2026-11-21, for AI-PILOT and everyone judging gear): one shared evaluator, `GearEval` (GearEval.cs). `GearEval.Judge(hero, item)` → `GearJudgement { Verdict (GearVerdict.Better/Similar/Worse/Unusable/NotGear), Off, Def (fractions), Total, Special }`; `GearEval.IsUpgrade(hero, item)` (wearable now and 升級). It simulates the swap (2H replaces main+off, rings the weaker ring) with the real Hero.Recalc and scores offence (DPS) and defence (effective life). Do NOT use the old `Worth()` (power × rarity) for equip/salvage/buy decisions; `Hero.IsUpgrade(it, worth, margin)` still compiles but now delegates to GearEval (worth ignored). AiPilot.TidyInTown and TestBot.ManageBag already call GearEval.
SYSTEMS API (2026-11-21, bag marks): `Item.Locked` (player lock: never touch it in any bulk action — salvage, sell, discard, mark); `hero.MarkOf(it)` → `BagMark.None/Recycle/Discard` (Recycle = the old junk set D.Junk; Discard = D.Discards); `hero.MarkAll(items, mark)` (skips locked), `hero.RecycleItems()`, `hero.DiscardItems()`. Presets: `new BagFilter().ApplyPreset("nonupgrade"|"lowcommon"|"otherclass")`, then `.Pick(hero)` — the pilot may use them; skip items with `Aspect`/`Unique` (含特殊效果) when acting on a preset.

CHANGE (AI-PILOT, 2026-11-21): `Fx.Telegraph`, `TelegraphCone` (both overloads), `TelegraphLine` and `TelegraphRing` now
register each hostile mark in `DangerMap` (new file `game/src/DangerMap.cs`: shape, land time, the returned node) — one
line at the top of each function plus `DangerMap.Bind(...)` around the returned node. Drawing is unchanged.
`Projectile.cs` gained a static `Live` list (`_EnterTree/_ExitTree`) and read-only getters (`HostileShot`, `Ground`,
`Heading`, `Speed`, `RangeLeft`, `HitRadius`). Pilot combat lives in `AiPilotCombat.cs` (TestBot partial) and
`AiPilotPlan.cs` (AiPilot partial); `AiPilot.cs` / `TestBot.cs` only got one-line hooks outside the gear-picking code.
- REQUEST RENDER→SYSTEMS (gear sets, 2026-11-21): whenever a hero's saved sets change (and once after loading a save), call
  `GearCatalog.PreloadSets("sets:" + hero.D.Name /* or any stable per-hero key */, hero.Class, sets)` where `sets` is
  `IEnumerable<IEnumerable<Item>>` (the items of each set; null slots fine). It loads those models on the loader thread,
  converts their materials once and pins them (never evicted). A swap may write the slots one by one with a Hero.Changed
  each: Player coalesces them into ONE rebuild at the end of the frame, before it is drawn. Stance clips need no preload
  (the whole humanoid library is resident). Measured by `--suite=gear:swap` (simulated sets until yours land): 8 full
  2H↔dual-wield swaps in combat, 8 rebuilds, 0 frames waiting for files, worst frame after a swap 34.2 ms (mean 17.0 ms).
  RENDER binds no keys (F1…F6 / E are yours).

CHANGE (ART-W, 2026-11-21 evening, polish pass): geometry and atlases were re-baked for 20 bases - sword1, axe1, mace1,
dagger, wand, scythe1, flail, fist, sword2, axe2, mace2, polearm, scythe2, bow, crossbow, shield, focus, totem, grimoire
and all gems (ids, file names, mounts and grip2 unchanged; `length` / `bbox` / `tris` changed in the manifest).
REQUEST ART-W -> RENDER: please bump `ItemIcon3D.Version` (8 -> 9) so every cached icon is rendered again, and re-run
the icon sheet. Visible changes: wand v2, focus v2 (now has a glow surface), totem v2, gems, sword1 v2, sword2 v2,
axe2 v1 and v3, darker black steel on the v3 models.
SYSTEMS API (2026-11-21, gear sets 裝備組): `hero.D.GearSets` (6 × `GearSetData { Name, Uids[10] by EquipSlot, ItemNames[10], Bar, Spec }`), `hero.SetAt(i)`, `hero.SetName(i)`, `hero.InSet(item)`, `hero.PreviewSet(i)` (a Hero copy wearing the set). Loading: `GearSets.Switch(i, fromKey)` → `hero.LoadSet(i, inTown)` → `Recalc()` → `Changed` — the same path as `hero.Equip`, so RENDER redraws as for a normal equip; to preload, resolve every uid of every set (worn, bag, stash) and call `ItemDb.LookFor(item, hero.Class)`. Keys: F1–F6 load, E quick swap (Settings.SetKeys, rebindable). PROTECTION: use `hero.IsProtected(item)` (= `item.Locked` or in any set) — not `item.Locked` alone — before any bulk salvage/sell/discard/mark (AiPilotPlan.cs line 134 changed to it, SYSTEMS edit).
- RENDER (2026-11-21, gear sets live): `Hero.PreloadSetModels()` (added in Hero.Sets.cs) is called by Game after the hero is built
  (load / new game / class change) and by SaveSet / ClearSet / RenameSet. Player also pre-builds every saved set's pieces
  hidden on the hero model (one piece per frame), so a swap only shows and hides nodes. Measured with the real sets
  (`--suite=gear:swap`, F1/F2/F3/E in combat, 2H ↔ dual-wield ↔ 1H+shield): one rebuild per loadout change, 0 frames waiting,
  worst frame after a swap 18.8 ms (mean 16.6 ms; was 34.2 ms before pooling).
