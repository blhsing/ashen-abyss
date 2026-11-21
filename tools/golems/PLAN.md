# Golems: plan (not implemented)

The user (2026-11-21): "golems are featured in the original game. bring this game to parity on this front. modify existing classes and skill system where appropriate. plan this but not implement it yet"

## Where things stand
- In the genre's reference game, golems belong to the necromancer class. They are one of three permanent minion lines chosen in a minion book: skeleton warriors, skeleton mages and a golem.
  - Each line has variants and upgrades, or can be sacrificed for a personal bonus.
  - The golem has an active command.
- Our matching class is the **Soulbinder 喚魂師** (`SkillsData.cs` ~215). Today it has:
  - `sb_raise` 喚起骸骨: up to 4 skeleton warriors, with an upgrade for alternating throwers;
  - `sb_colossus` 骸骨巨像: a 20 s ultimate;
  - specialisations at level 15: `sb_legion` 骸軍, `sb_offering` 獻祭, `sb_blood` 血魂;
  - keystones `kp_sb_chorus` (per-minion damage) and `kp_sb_alone` (no minions).
- Minions are `Monster` instances with `Faction.Player` in `World.Minions`, spawned by `SkillExec.SpawnMinion`. Their life is `0.45 × hero life`. They have `Taunter`, `Cleave`, `LifeTime` and `MinionCoef`.
  - Bosses already consider minions (`BossBrain.cs` ~664).
  - The Pactbinder's fiend type is picked by its specialisation (`PactFiendId`). That is a precedent for choosing minion variants.
- To verify before building: whether minions persist across area changes, town and save/load. Likely not.

## Design (original names, our own world)

### 1. 亡者名冊 Roll of the Dead (Soulbinder only)
- **Where:** a new tab in the skill screen, unlocked at level 5. Free to change in town; a small fee elsewhere.
- **Lines:** three, each with three kinds. Each kind has 2 upgrade picks (choose one), or the line can be **獻祭** (sacrificed): no minions of that line, in exchange for a personal bonus.

| Line | Kinds | Sacrifice bonus |
|---|---|---|
| 骸骨戰士 Warriors (`sb_raise`) | 掠襲者 (fast, low life), 持盾者 (tank, taunt), 收割者 (slow, cleaving) | +10 % damage |
| 骸骨術士 Mages (new, raised by the same skill, alternating with warriors) | 影術士 (shadow bolts), 霜術士 (slowing frost), 骨術士 (pays life for strong bone spears) | +15 % resource regen |
| 魔像 Golem (new, permanent) | 骨魔像, 血魔像, 鐵魔像 (see §2) | +15 % life and armor |

- **Upgrade 1 (prerequisite):** `sb_raise` stops being the warriors' only source. Warriors and mages fill their caps from corpses automatically over time, and casting `sb_raise` raises immediately. This keeps the skill but takes away its maintenance chore.
- **Sacrifice build:** sacrificing all three lines turns `kp_sb_alone` 孤魂 into a real build path.

### 2. Golems
- **One at a time; permanent:**
  - follows the hero across areas and into town;
  - after death it re-forms after 20 s at the hero's side.
  - Large body: 2.4–3 m.
  - Scales with the hero's life, armor and minion stats.

| Golem | Role | Passive | Command (`sb_golem_cmd`) |
|---|---|---|---|
| 骨魔像 Bone | Tank | Taunts nearby enemies on hit; thorns | 「骨牢令」: taunts everything within 8 m for 5 s and takes 50 % less damage |
| 血魔像 Blood | Sustain | Absorbs 15 % of the hero's damage taken; heals from its own hits | 「血潮令」: drains life in 6 m, heals the hero and the golem and deals shadow damage over 3 s |
| 鐵魔像 Iron | Bruiser | Every 4th hit sends out a shockwave | 「鐵擊令」: leaps to the cursor and slams, stunning for 1.5 s in a 4 m radius |

- **Upgrades:** two per kind, e.g. Bone: 碎骨反擊 / 骨甲共享; Blood: 輸血 / 血爆; Iron: 熔鐵 (leaves a burning fissure) / 重鑄 (re-forms in 8 s).

### 2a. Weapons and the forged iron golem (user-approved, 2026-11-21)
Inspired by the older game in the genre, where the iron golem is made from a consumed metal item and takes on its properties.

- **Bone and blood golems** have fixed built-in weapons, for looks only: the bone golem a bone cleaver fused to its forearm, the blood golem clotted claws. No item slot.
- **Iron golem 鐵魔像 is forged from an item:**
  - **Forging:**
    - When the iron golem is summoned for the first time, or the player chooses 「重新鑄造」, a picker shows the hero's bag, filtered to metal weapons, shields and armor.
    - The chosen item is **consumed**, after a confirmation that shows its rarity and name. Locked items need an explicit unlock first, and equipped items cannot be used.
    - Rings, amulets, gems and non-metal pieces (cloth or leather looks) can't be used.
    - Old forges keep working if a material tag changes.
  - **Inheritance:**
    - The golem takes on the item's affixes, mapped to golem stats at a minion scale:
      - weapon damage and speed become its hit damage and attack speed;
      - armor, block and resistances become its own;
      - offensive affixes (crit, elemental damage, damage vs elites) apply to the golem;
      - hero-only affixes (resource, skill ranks, movement) become a small general golem bonus so nothing is wasted.
    - Item power sets the golem's base level, capped at the hero's level.
    - A legendary power or unique effect applies if a golem can sensibly use it (on-hit, thorns, auras, damage procs); otherwise it is shown greyed as 「魔像無法使用」.
    - Tooltip preview before confirming: the golem's resulting stats against the current ones, using `GearEval`-style numbers.
  - **Look:**
    - The forged item's 3D model (the 0.4.0 `wpn_*` / `arm_*` models) is fused into the golem's body.
    - A 2H weapon becomes its striking arm; a 1H weapon is fused into the right fist; a shield becomes a shield arm on the left; armor becomes plates bolted onto the slag body (chest → torso plates, helm → head crown, etc.).
    - Its rarity glow is kept.
    - Attacks follow the weapon class: a 2H maul or axe gives slow overhead slams, a 1H a faster pair of swings, a polearm sweeps, and a shield adds a block chance and a shield-bash in place of every 4th hit.
  - **Death and re-forming:** the golem re-forms from the same item's essence (stored in the save as a full item copy). Dying never costs the item again. Forging a new item replaces the old essence, which is lost; the confirmation says so.
  - **Unforged iron golem:** if the player never forged one, the iron golem is plain slag with fists and base minion stats. It is still useful, so the forge is an upgrade, not a gate.
- **Changes elsewhere:**
  - **Minion frame:** a forge icon on the iron golem's portrait, and a tooltip listing the inherited affixes.
  - **Inventory filter:** a new quick mark 「可鑄造魔像」 and a verdict line on metal items for Soulbinders: 「鑄造後：魔像攻擊 +x%」.
  - **AI 代打:** it forges from the best salvage-bound metal item when the result beats the current golem. It never forges locked, equipped or legendary items without the player's setting allowing it.
  - **Saves:** a `GolemForge` item copy is stored in `HeroData`.
  - **Art:** the golem rig needs attachment points (`fuse_arm_r`, `fuse_arm_l`, `fuse_torso`, `fuse_head`) and a slag "weld" seam material where items join the body. RENDER's `GearVisual` mounts the item models on those points.

### 3. Skill-system changes
- **New active `sb_golem_cmd` 魔像號令**, in the Mastery category:
  - cooldown 14 s; its effect depends on the golem's kind;
  - if the golem is dead, casting it re-forms the golem at once (shared cooldown);
  - three upgrades in the usual way: 號令擴散 (a larger area), 急令 (−4 s cooldown), 共鳴 (skeletons copy a weak version).
- **Skill-tree data:** a new per-class "book" concept (`ClassBook`). It is generic, so another class could get one later. Choices are stored in `HeroData` and shown in the tooltip, character sheet and skill screen.
- **Specialisations at level 15:**
  - keep 骸軍 (now: +1 warrior cap, golem +25 % damage);
  - 獻祭 becomes "sacrifice bonuses +50 %";
  - keep 血魂, with blood-golem synergy.
- **Keystones:** new 「魔像之心」: while the golem lives, 20 % of the hero's damage taken is redirected to it, and the golem's hits restore resource.
- **`sb_colossus` stays.** It is the temporary ultimate. Its 號令 upgrade also empowers the golem.

### 4. Minion-system upgrades (needed for parity, shared by all summoners)
- **Persistence:** minions follow through area transitions, town and save/load (warrior and mage counts and the golem's state).
- **Follow behaviour:** a leash to the hero (teleport if over 25 m or out of sight); an "attack my target" rule (the hero's last hit target, else the nearest threat); a passive mode in town.
- **Pathing:** a large-body clearance for the golem; it must not block narrow doors (pass-through when stuck).
- **HUD:** a minion frame by the life globe, showing the golem's portrait, life and re-form timer, plus warrior and mage counts.
- **Scaling:**
  - minion life, damage and armor from the hero's stats plus minion affixes;
  - the minion attack speed affix;
  - minion resistances follow the hero's.
- **Threat:** bosses respect the bone golem's taunt (except hard-coded phase moves), using the existing `BossBrain` minion awareness.
- **Performance:** a cap on total minion count; cheaper animation updates and shadows for minions far from the camera.

### 5. Items (fits the 0.4.0 weapon system)
- **Off-hand affixes:** golem life and damage, mage damage, minion attack speed and command cooldown, on the Soulbinder's focus and shield.
- **New legendary powers** (about 6), e.g.:
  - the bone golem's thorns burst into bone shards;
  - the iron slam leaves a fissure;
  - the blood drain also raises a skeleton per kill;
  - commanding also commands the warriors;
  - the golem re-forms instantly once per 60 s;
  - sacrificed lines grant double bonuses.
- **Uniques:** two Soulbinder uniques built on golems.
- **Gear evaluation:** `GearEval` (the upgrade verdict) learns minion stats for minion builds.

### 6. Art and audio (Fable xhigh for models and VFX)
- **Models:**
  - 3 golem models on the heavy humanoid rig used by the bestiary brutes. Clips: idle, walk, attack ×2, slam, taunt roar, drain channel, death collapse, re-form (rising from bones, blood or slag).
  - 3 skeleton-mage variants.
  - Warrior kinds as material and prop variants of the existing skeleton.
- **VFX:** command effects in the new ground-sigil style (friendly), a bone cage, a blood tether, an iron slam crater, and re-form effects.
- **SFX:** golem footsteps, slam, roar, drain loop. Optional hero voice lines when commanding (VoxCPM2, zh-TW), to be added to the voice queue.
- **Icons:** 3D-rendered portraits for the minion frame and the book (using the new `ItemIcon3D` path).

### 7. Other systems
- **AI 代打:** knows the book (a sensible default per level), uses the command (bone taunt when danger is high, blood drain when low on life, iron slam on packs) and re-forms a dead golem.
- **Demo mode:** a Soulbinder scene with each golem kind.
- **Test bots:** a golem stance per kind.
- **Save migration:** old Soulbinder saves get the default book (warriors 掠襲者, mages 影術士, golem 骨魔像) and one free respec.
- **Text:** all in Traditional Chinese (Taiwan); tooltips show exact numbers.

### 8. Other classes
Golems stay with the Soulbinder, which is how the reference game places them. Two ideas are left out:
- a 鍛軀 Forgebody forge-golem, because it would blur that class's self-as-construct identity;
- a Pactbinder golem, because its demonic fiends already fill the pet slot.

The generic `ClassBook` and the minion-system upgrades also benefit the Pactbinder's fiends: persistence, the HUD frame and the follow logic.

## Order of work (after 0.4.0, since it touches the same files)
1. **SYSTEMS agent (Opus):** minion persistence, follow, HUD, scaling and threat → the `ClassBook` data and UI → golem behaviours and the command skill → items and affixes → save migration → AI pilot, demo and bots.
2. **ART agent (Fable xhigh), in parallel from the start:** 3 golems, 3 mages, warrior variants, the clips, the VFX sigils.
3. **Balance pass:** a Soulbinder campaign with each golem and with full sacrifice; boss fights against taunt; a minion-count perf test with 4 warriors, 4 mages, the golem and the colossus in a dense fight.
4. Release as part of the next version.

**Estimate:** 2 agents, roughly the size of the 0.4.0 gear work for systems, smaller for art.
