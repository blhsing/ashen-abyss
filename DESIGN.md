# 燼淵 Ashen Abyss — design brief (shared by everyone working on the game)

An ORIGINAL dark-fantasy isometric action RPG. All in-game text and voice are Traditional Chinese (zh-TW).
It borrows genre mechanics (classes, loot, skill/talent trees, dungeons, bosses) but its name, world,
characters, art and music are our own. Never reference or imitate any existing game's characters, names,
logos, music or specific look.

Engine: Godot 4.7.2 .NET (C#, Forward+ / Vulkan). Target machine: AMD Radeon 8060S, 4K display.

## Paths (workspace is mapped to drive Y: — always use Y:\ paths, the real path is too long for many tools)
- `Y:\game`                      Godot project (`res://`). Only the lead runs Godot on it.
- `Y:\game\assets\chars`         character .glb (mesh + rig, no animations) + `anim_lib.glb`
- `Y:\game\assets\weapons`       hero weapon .glb
- `Y:\game\assets\env`           environment textures / models / skies
- `Y:\game\assets\audio\{sfx,music,amb,vo}`
- `Y:\game\assets\fonts`, `Y:\game\assets\icons`
- `Y:\game\data`                 JSON data shared between tools and game
- `Y:\tools\<name>`              build tools/scripts (Blender Python, C# console tools)
- `Y:\assets_src`                raw downloads (each source in its own folder, keep licence files)
- `Y:\shots`                     previews / screenshots for review
- Blender 5.2: `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` (run with `-b` for headless)
- .NET SDK 10 (`dotnet`), no system Python (Blender bundles one). Keep file names short, ASCII, lower_snake.
- Licences: only CC0 / public domain / MIT / OFL / CC-BY (with attribution recorded). Record every
  third-party asset in `Y:\CREDITS.md` (name, author, source URL, licence).

## World
The Sun-Forge (日爐) died a thousand years ago. The world of 瑟蘭 lies under an endless ashen dusk (灰暮).
People survive around ember-fires (燼火) kept in beacons. Those who can carry ember in their flesh are
持燼者 (Emberbearers) — the player. The Hushed Order (寂滅教團) is snuffing the beacons to wake
寂主・奧薩洛斯 (Osaros, Lord of Silence), who sleeps in the abyss beneath the dead forge.

Tone: grim, grounded, quiet dread; small human warmth around fire. Visual pillars: painterly darkness
with warm fire pools against cold blue-grey dusk; saturation reserved for gameplay (skills, loot, danger).
Signature motif: embers and ash — enemies crumble into ash and sparks when slain.

## Regions (hub + seven; one boss each; a beacon 燼塔 to relight in every region becomes its waypoint)
| id | 名稱 | look | accent | boss |
|---|---|---|---|---|
| town    | 殘燈鎮 Lastlamp (hub)        | walled hamlet round a great brazier, timber + stone, lanterns | warm amber | — |
| wastes  | 灰燼荒原 Ashen Wastes         | dead forest, ash-grey soil, fog, ruined graves, cold dusk | pale blue | 掘墓者・葛魯姆 |
| frost   | 霜脊隘口 Frostspine Pass      | snowbound mountain pass, frozen ruins, blizzard | ice blue | 霜骸領主・赫爾蒙 |
| crypt   | 沉眠墓窟 Slumbering Catacombs | stone halls, pillars, torches, sarcophagi, cobwebs | sickly green | 腐骨主教・瑪爾薩 |
| mire    | 瘴泣沼澤 Weeping Mire         | fog-choked swamp, rot, glowing fungus, boardwalks | toxic yellow-green | 沼母・艾絲珂 |
| sands   | 鏽砂廢都 Rustsand Ruins       | ruins of a brass city in the dunes, sandstorm | brass / violet | 鏽砂暴君・伊薩克 |
| sanctum | 寂滅聖城 Hushed Sanctum       | gothic cathedral-city of marble, candles, bells | cold white | 寂滅大導師・賽倫 |
| abyss   | 燼淵 The Ember Abyss          | basalt caverns, lava rivers, obsidian spires, falling embers | lava orange / void white | 寂主・奧薩洛斯 |
Acts: 1 wastes → 2-4 frost / crypt / mire in any order → 5 sands → 6 sanctum → 7 abyss → epilogue.
Endgame: four world tiers, the endless 深淵裂隙 (rift) and region events.

## Classes (eight heroes; each has its own resource model and a class-only mechanic chosen at level 15)
| id | 名稱 | fantasy | resource | weapon | signature colours |
|---|---|---|---|---|---|
| emberblade  | 燼刃衛   | heavy plate melee warden                  | 怒焰 Fury (built by hitting)        | greatsword | ember orange |
| astromancer | 星術師   | caster of fire / frost / lightning        | 靈能 Mana (regenerates)             | staff | star blue |
| hunter      | 影獵手   | hooded skirmisher                         | 精力 Energy + combo points          | hand crossbow + dagger | teal / shadow |
| soulbinder  | 喚魂師   | bone-adorned summoner with minions        | 魂能 Essence + soul remnants        | scythe-staff | pale violet-green |
| wildsoul    | 荒靈行者 | shapeshifter (bear / wolf spirit forms), earth and storm | 靈息 Spirit (builder/spender) | totem war-club | moss green |
| forgebody   | 鍛身者   | unarmed martial artist tempered in the dead forge | 氣焰 Vigor (builder/spender) | fist gauntlets | forge ember |
| lampknight  | 誓燈騎士 | sword-and-shield holy warden, auras       | 信念 Faith (builder/spender)        | sword + shield | warm gold |
| pactbinder  | 縛淵術士 | binder of ember fiends                    | 咒力 Hex (regen) + 契印 Sigils (second resource) | wand + grimoire | ember red / violet |
Damage types: 物理, 火焰, 冰霜, 閃電, 毒素, 暗影.

## NPCs (town)
守燈長老 艾爾溫 (elder, lantern keeper, quest giver) · 鐵匠 布蘭 (blacksmith) · 行商 莉雅 (merchant)
· 祕術師 薇菈 (occultist, masked) · villagers.

## Monster families
- 骸骨 Bone: `skeleton` 骸骨兵 (melee) · `skeleton_thrower` 骸骨擲矛手 (ranged javelin)
- 飢骸 Husks: `husk` 飢骸 (fast swarmer) · `husk_bloated` 腫脹飢骸 (slow, explodes on death)
- 寂滅教團 Hushed Order: `cultist` 寂滅教徒 (melee dagger) · `cult_caster` 寂滅祭司 (caster, shadow bolts) · `knight` 墮落騎士 (armoured bruiser)
- 縛魂 Wraith: `wraith` (spectral caster, teleports)
- 燼魔 Ember fiends: `fiend` 燼魔 (horned demon; scaled 0.6 as `imp` 小燼魔) · `brute` 熔岩巨魔 (huge bruiser)
- 霜縛 Frostbound (frost): `frostbound` 霜縛屍兵 (melee spear) · `rime_stalker` 霜角獵者 (fast leaper)
- 沼裔 Mireborn (mire): `mire_hag` 沼巫 (poison caster) · `rotspawn` 腐泥巨怪 (slow bruiser)
- 砂葬 Sandbound (sands): `sand_revenant` 砂葬者 (fast melee) · `brass_sentinel` 黃銅守衛 (construct bruiser)
- 唱詩 Choir (sanctum): `choir` 靜默唱詩者 (spectral support caster that shields allies)
- Bosses: `boss_gravedigger` (wastes) · `boss_rimelord` (frost) · `boss_bishop` (crypt) · `boss_mire_mother` (mire)
  · `boss_sand_tyrant` (sands) · `boss_grand_hush` (sanctum) · `boss_osaros` (abyss, final)
- Hero-side models: `form_bear` / `form_wolf` (wildsoul spirit forms); pactbinder fiends reuse `mon_brute`, `mon_fiend`, `mon_wraith`.

## Animation set (shared humanoid rig, Quaternius Universal Animation Library 1+2, CC0)
Idle_Loop, Walk_Loop, Jog_Fwd_Loop, Sprint_Loop, Roll, Death01, Hit_Chest, Hit_Head, Hit_Knockback,
Sword_Attack, Sword_Idle, Sword_Regular_A/B/C(+_Rec), Sword_Regular_Combo, Sword_Heavy_Combo, Sword_Dash,
Sword_Block, Shield_Dash, Shield_OneShot, Melee_Hook, Punch_Jab, Punch_Cross, OverhandThrow,
Spell_Simple_Enter/Idle_Loop/Shoot/Exit, Pistol_Aim_Neutral/Shoot/Reload/Idle_Loop (hand crossbow),
NinjaJump_Start/Idle_Loop/Land, Slide_*, Zombie_Idle_Loop, Zombie_Walk_Fwd_Loop, Zombie_Scratch,
LayToIdle (rise from ground), Interact, PickUp_Table, Chest_Open, Consume, Idle_Talking_Loop,
Idle_FoldArms_Loop, Idle_Lantern_Loop, Idle_Torch_Loop, Yes, Idle_No_Loop, Sitting_*.
