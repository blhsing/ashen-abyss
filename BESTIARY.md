# 燼淵 Ashen Abyss — Bestiary

Creature design document: the complete monster roster other people build from. It is a design document only —
it contains no code and no models. The tables below and `Y:\game\data\bestiary.json` are generated from the
same source (`Y:\tools\bestiary\src`, built with `dotnet run Y:\tools\bestiary\build.cs`), so they always agree.
Edit the source and rebuild rather than editing this file by hand.

**Originality.** Every family here grows out of this world's own fiction — the dead Sun-Forge (日爐), ash and
embers, the endless dusk (灰暮), the beacons (燼塔), the Hushed Order (寂滅教團) that snuffs fires, the frost
pass, the barrows, the mire, the brass city, the cathedral city and the abyss. Nothing is adapted, renamed or
mirrored from another game, film or book, and the roster is not a counterpart list of anyone else's. Folklore
stock (the walking dead, hounds, spiders, raiders, statues that wake) appears only with an identity that the
ash and the silence gave it; each family section says what that is.

## 0. Conventions

- **Ids and files.** A type id is ASCII `snake_case`. The 18 existing ids are unchanged. The model file for a
  type is `mon_<id>.glb` in `Y:\game\assets\chars` (the cinderling `imp` uses `mon_fiend` at 0.6 scale today
  and gets `mon_imp`). Family ids are the `MonsterDef.Family` strings; the eight existing ones (`bone`, `husk`,
  `cult`, `wraith`, `fiend`, `frost`, `mire`, `sands`) are kept.
- **Prefixes.** New Frostbound types use `rime_`, new Mireborn types `bog_`, new Sandbound types `dune_`, so
  that `frost_` / `ice_` / `mire_` / `sand_` / `ember_` stay reserved for regional reskins made with
  `MonsterDef.Variant` (section 3.9).
- **Stats.** life× and dmg× are `MonsterDef.Hp` / `Dmg` (multipliers on the level curve), speed is
  `MonsterDef.Speed` in m/s, scale is `MonsterDef.Scale`. Statuses are written as the `St.*` helpers take
  them: "chill 0.3" is an amount, "burn 0.3 over 3 s" is a total weapon-coefficient over a duration,
  "slow 0.3 for 2 s" is a fraction and a duration.
- **AI column.** `Kind + Trait + Trait`. The kinds `Melee`, `Ranged`, `Caster`, `Exploder`, `Teleporter`,
  `Brute`, `Support` and `Leaper` exist in `Monster.cs`. Every other kind and every trait is new and is
  specified once in section 5.
- **Rigs.** **A** the shared humanoid skeleton with the 85-clip animation library (humanoids, oversized
  bruisers, hunched beast-men, legless robed floaters, giants and armoured constructs). **B** quadruped,
  **C** arthropod, **D** serpentine, **E** flyer, **F** rigid / floating — each of these still has to be
  built, with its own small skeleton and the standard clip set `Idle / Walk / Run / Attack1 / Attack2 / Hit /
  Death / Spawn`. For rigs B–F the animation column says what the two attack clips show.
- **Build column.** *existing (built)* is on disk today; *existing (planned)* is one of the current 17 models
  that is not on disk yet; *base* is a new sculpt (a new body builder in `Y:\tools\blender\monsters`);
  *variant of X* reuses X's body builder with different weapon, armour, headgear, proportions and materials —
  still its own model file and its own silhouette.
- **Body-plan classes** are counted by silhouette, not by material: a human-sized stone effigy or brass
  attendant is a "humanoid biped"; "oversized bruiser" is a biped at scale 1.15–1.6 with bruiser mass;
  "giant / construct" is scale 1.75 and above or a large non-human machine; "floating / incorporeal" has no
  legs on the ground; "swarm / turret / siege" does not walk at all.
- **Scale ceiling.** Regular giants stop at scale 1.9. The region bosses are 1.8–2.4 today; when the giants
  ship, no region boss should stay below 2.2. World bosses are ×4–5.
- **Champions and elites.** Any regular type can be promoted by the existing affix system except: `Exploder`,
  `Swarm` and `Turret` kinds, spawner mothers, and the support roles (as `Populate.RosterOf` already does for
  leaders). Fuel-bearer giants never lead a pack; they may appear alone as elites.
- **Death.** Everything still ends in the game's signature ash-and-sparks crumble; each family section says
  how its version differs, because those differences are part of what tells the families apart.

## 1. Summary

**25 families, 129 regular types** (18 existing ids kept, 46 new bases, 65 new variants), plus 28 regional reskins, 28 named rare elites, 7 stronghold bosses, 14 side-dungeon bosses, 7 region bosses, 4 world bosses and 8 pinnacle bosses. Regions in bold are the ones where the family is a signature family.

| # | family id | 中文名 | English | concept | regions | types | roles it has | deliberately lacks | rigs |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `bone` | 骸骨 | The Unburnt | The dead who were buried whole because there was no fire left to burn them, risen to look for the pyre they were promised. | wastes, frost, **crypt**, mire, sands, sanctum | 6 | swarmer, melee, ranged, shield-bearer, reviver, bruiser | caster | A |
| 2 | `husk` | 飢骸 | The Fireless | Living people who went too long without ember-light: the warmth left, the hunger stayed. | **wastes**, crypt, mire, sands | 5 | swarmer, melee (puller), exploder, bruiser (splits), summoner | ranged and caster | A |
| 3 | `cult` | 寂滅教團 | The Hushed Order | The order that calls the dusk a mercy and puts out every fire it finds. | frost, crypt, sands, **sanctum**, abyss | 9 | swarmer, melee, ranged, caster, bruiser, support (shielder), healer, debuffer, stealth pouncer | nothing | A |
| 4 | `wraith` | 縛魂 | The Bound | Souls that no pyre released, chained to their own shrouds by the Order as wardens and as proof. | frost, crypt, mire, sanctum, abyss | 3 | caster (teleporter), melee (phasing), swarmer (lantern wisps) | bruiser, ranged physical and every kind of support | A, F |
| 5 | `reaver` | 盜燼眾 | Ember-takers | People who stopped tending fire and started stealing it — raiders who sell stolen embers to anyone, the Order included. | wastes, frost, crypt, sands | 6 | swarmer, melee (puller), bomber, support (aura), bruiser, handler (with hounds) | caster and healer | A |
| 6 | `dusk` | 灰暮野物 | Dusk-fauna | The animals that survived a thousand sunless years by learning to hunt heat. | wastes, frost, crypt, mire, sands, sanctum, abyss | 9 | swarmer (pack), charger, bruiser, stealth pouncer, ranged spitter, flyer (thief), flyer (ceiling ambusher), flyer (drain), swarm | caster, support and reviver | B, E, F |
| 7 | `frost` | 霜縛 | Frostbound | The garrison of the pass, frozen at its posts and still holding them. | **frost** | 6 | melee, swarmer (leaper), support (breath), ambusher-tank (ice shell), bruiser, quadruped charger (bursts) | a true caster and a reviver | A, B |
| 8 | `coldfire` | 凍焰 | Frozen Flames | The last flames of dead beacons, frozen mid-flicker into blue glass that burns cold. | **frost** | 4 | swarmer (flicker), caster, bruiser, ranged crawler | line melee, support and anything fast on foot | A, C, F |
| 9 | `hauler` | 馱薪巨人 | Fuel-bearers | The giants who carried fuel to the Sun-Forge, still hauling a thousand years after it went out. | wastes, **frost**, sands, abyss | 4 | giant bruiser (sweeper), giant siege thrower, giant grabber, giant bomber | every small role | A |
| 10 | `sexton` | 葬甲 | Sexton-beetles | Carrion beetles the undertakers once kept to clean bones — grown huge on a thousand years of unburnt dead, and still burying everything they find. | **wastes**, crypt | 4 | swarmer (grubs), armoured melee, burrower, brood-mother | ranged, caster and support | C |
| 11 | `char` | 炭木 | Charwood | The half-burnt forest of the first winter — trees that were set alight and never finished burning. | **wastes**, mire | 4 | swarmer (stumps), melee, bruiser (reach), burrowing root (ambush turret) | ranged, caster and support | A, D |
| 12 | `ash` | 灰塑 | Ash-casts | Hollow figures of packed ash standing where people burned, walked by the wind that blows up from the abyss. | wastes, abyss | 4 | melee (reforms), caster (ash-fall), giant (splits), burrowing worm (breath) | swarmer, ranged physical, support and healer | A, D |
| 13 | `shroud` | 殮蛛 | Shroud-weavers | Spiders the barrow-wardens kept to wrap the dead in silk when there was no fire to burn them — still wrapping everything. | **crypt** | 5 | swarmer (spiderlings), ranged controller (web), ceiling ambusher, brood-mother, humanoid melee (puppeted cocoon) | caster and a hard-hitting bruiser | A, C |
| 14 | `effigy` | 臥像 | Lid-effigies | The carved likenesses on the barrow-lids, woken by the Order's rites of silence in place of the bones beneath them. | **crypt**, sanctum | 5 | armoured melee, caster, bruiser (thrower), ambusher (niche statue), pouncing quadruped (dormant) | swarmer and support | A, B |
| 15 | `mire` | 沼裔 | Mireborn | The boardwalk-folk who traded fire for the warmth of rot, and were kept by the peat. | **mire** | 6 | caster, bruiser, reach melee, ranged controller (net), swarmer (buried ambusher), puller (toad) | healer and reviver | A, B |
| 16 | `lamp` | 偽燈菌 | False-lamp | A fungus that learned to glow the colour of lantern-light, because warm bodies walk toward lamps. | **mire** | 5 | turret (lure), melee, bomber, swarmer (crawler), bruiser (aura) | support and anything fast | A, C, F |
| 17 | `lurk` | 泥潛 | Weeping Leeches | The things whose breathing gives the Weeping Mire its name — leeches that drink warmth instead of blood. | **mire** | 4 | swarmer (leeches), burrowing ambusher, ranged spitter (pools), giant bruiser (inhale) | every humanoid role, caster and support | D |
| 18 | `sands` | 砂葬 | Sandbound | The dead of the Brass Dynasty, buried standing to await a sun of their own making, and woken by the storm instead. | **sands** | 6 | fast melee, bruiser (construct), ranged (returning disc), shield-bearer (reflect), caster (lightning), support (empower) | swarmer and exploder | A |
| 19 | `rust` | 寄銅蟲 | Brass-hermits | Desert crawlers that eat verdigris and live inside the dynasty's hollow brass — helmets, gears and storm-jars that scuttle. | **sands** | 5 | swarmer (helm), bruiser (stinger), charger (gear), ranged (jar), flyer (locust) | caster and support | C, E |
| 20 | `engine` | 日輪機關 | Sun-engines | The machinery of the Brass Sun, still tracking a light that is not there — until someone brings one. | **sands** | 3 | turret, giant (carrier), support (winder) | melee troops, swarmers and casters | A, F |
| 21 | `wax` | 蠟封眾 | The Wax-sealed | The candle-makers of the cathedral city, sealed in their own wax by the Order and stood in the naves as unlit candles. | **sanctum** | 4 | melee (dormant, flammable), exploder (lit runner), bruiser (wax pools), support (reseals allies, lobs hot wax) | ranged and swarmer | A |
| 22 | `bell` | 啞鐘 | Mute Bells | The cathedral's bells with their tongues cut out — and the ringers the Order hung in their place. | **sanctum** | 3 | charger (melee), bruiser (pulse), floating pulse-caster | ranged, swarmer and healer | A, F |
| 23 | `fiend` | 燼魔 | Ember-fiends | Sparks of the Sun-Forge that refused to go out, fell into the abyss and learned to be afraid of the cold. | **abyss** | 6 | melee, ranged (cinderling), bruiser, flyer (strafing bomber), quadruped charger, caster-summoner | support, healer and shield roles | A, B, E |
| 24 | `forge` | 爐僕 | Forge-thralls | The Sun-Forge's inner work-crew, fused to their tools, still working the shift that never ended. | **abyss** | 6 | controller (bellows), grappling melee, bomber (slag), bruiser (hammer), turret (forge-eye), quadruped swarmer (slag-hound) | caster, support and healer | A, B, F |
| 25 | `hush` | 淵寂 | The Pale Hush | Pieces of Osaros's sleep that leak out of the abyss — white, soundless outlines where a creature should be. | sanctum, **abyss** | 7 | melee, caster (absorb), bruiser, swarmer (crawler), stealth pouncer (quadruped), burrower (serpent), flyer | ranged physical, support and summoner | A, B, C, D, E |

Global families (five, re-skinned per region): `bone`, `husk`, `cult`, `wraith`, `reaver`. `dusk` is the wildlife catch-all with a different set of beasts in each region. `hauler` is a deliberate "one giant in someone else's pack" family.

## 2. Families

### 2.1 骸骨 `bone` — The Unburnt

*The dead who were buried whole because there was no fire left to burn them, risen to look for the pyre they were promised.*

**What they were, what the dusk made them.** In 瑟蘭 the dead were always given to the fire and their ash carried up to the Sun-Forge. When the Forge died there was no fuel to waste on corpses, so a thousand years of dead went into the ground whole — 未焚者, the Unburnt. They rise carrying what they were buried with (kettle caps, grave-linen, funeral urns, coffin boards) and walk toward any warmth, because warmth is the pyre they are owed. The Hushed Order's bishops conscript them, but they need no master to envy a living fire. Everything they do is stiff soldier-drill or grave goods used as weapons; nothing is magic. Death: they fall apart joint by joint and the bones finally burn — one quick flare of pale ember, then ash: the cremation they were waiting for.

**Roles it has:** swarmer, melee, ranged, shield-bearer, reviver, bruiser.  
**Deliberately lacks:** caster — bones fight only with what they were buried with; their magic belongs to the Order priests who march with them, so a bone pack is dangerous in proportion to who is leading it.  
**Regions:** wastes, frost, **crypt** (signature), mire, sands, sanctum.  
**Rigs:** A ×6.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `skeleton` | 骸骨兵 | Bone Soldier | melee | Melee | A | existing (built) | physical | - | 0.9 | 1 | 3.9 | 1 | Sword_Regular_A, Sword_Regular_B; rises with LayToIdle | wastes, frost, crypt, mire, sands, sanctum |
| `skeleton_thrower` | 骸骨擲矛手 | Bone Javelineer | ranged | Ranged | A | existing (built) | physical | - | 0.7 | 0.9 | 3.6 | 1 | OverhandThrow; rises with LayToIdle | wastes, frost, crypt, mire, sands, sanctum |
| `bone_scrap` | 雜骨 | Scrapbone | swarmer | Melee | A | variant of `skeleton` | physical | - | 0.4 | 0.6 | 5.8 | 0.7 | Zombie_Scratch, Punch_Jab; idle Zombie_Idle_Loop, run Sprint_Loop | wastes, frost, crypt, sanctum |
| `bone_pallbearer` | 抬棺骸 | Pallbearer | shield-bearer | Shieldbearer | A | variant of `skeleton` | physical | knockback (shield bash) | 1.4 | 0.9 | 3.2 | 1.05 | Shield_OneShot, Shield_Dash; guard pose Sword_Block | wastes, frost, crypt, sanctum |
| `bone_urnbearer` | 捧甕骸 | Urn-bearer | support (reviver) | Reviver | A | variant of `skeleton` | shadow | - | 0.8 | 0.6 | 3.2 | 1 | Spell_Simple_Shoot (pours ash to revive), Interact; idle Idle_Lantern_Loop | crypt, sanctum |
| `bone_amalgam` | 疊骨塚衛 | Barrow Amalgam | bruiser | Brute | A | base | physical | stun 0.5 s | 3 | 1.8 | 3 | 1.45 | Sword_Attack, Sword_Heavy_Combo | crypt, sanctum |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `skeleton` | 骸骨兵 | Upright articulated skeleton, narrow and vertical, with light showing through the ribcage; dented kettle cap, a single pauldron and a notched rusty sword held low. The read is "thin soldier under a round hat brim". Materials: old bone, rust, grave linen; accent: faint pale-blue eye sockets. |
| `skeleton_thrower` | 骸骨擲矛手 | Light skeleton under a torn madder-red hood and shawl; a quiver of javelins fans up over the left shoulder like a broken wing and one javelin is cocked in the right hand. Hooded head plus a fan of spikes is the read. Accent: dull red cloth, no glow. |
| `bone_scrap` | 雜骨 | A child-sized skeleton wrongly assembled from leftovers: one arm far too long and dragging, the other a stump with a rib lashed on as a spike, the skull too big and tilted, no helmet. It scuttles bent double so the long arm trails behind — a lopsided little hook shape. Bone bound with grave cord; no accent. |
| `bone_pallbearer` | 抬棺骸 | Broad skeleton hidden from the front behind a coffin lid carried as a tower shield (tapered hexagon, iron cross-straps, bent nails along the rim), a short grave-digger's mattock in the other hand. From any angle the read is "a coffin plank with legs". Materials: rotten oak, iron straps; no accent. |
| `bone_urnbearer` | 捧甕骸 | Stooped skeleton in a long funeral stole hugging a large round-bellied urn against its chest with both arms, the lid tied to its wrist by a cord; ash runs in a thin stream from a crack. Silhouette: a hunched figure with a big sphere where the chest should be. Materials: glazed black clay, grey linen; accent: pale green glow in the urn mouth. |
| `bone_amalgam` | 疊骨塚衛 | Two skeletons' worth of bone stacked into one wide body: a doubled ribcage like a barrel, a second pair of small arms folded on the chest, and three skulls set in a row across the shoulders with the middle one largest. It swings a headstone lashed to a timber post. Materials: mixed bone, moss-stained granite, rope; accent: three pairs of pale green eye-lights. |

### 2.2 飢骸 `husk` — The Fireless

*Living people who went too long without ember-light: the warmth left, the hunger stayed.*

**What they were, what the dusk made them.** Husks are not the dead. They are villagers, pilgrims and beacon-keepers whose fire went out and who did not die of it — 失燼, ember-loss. The body cools to the temperature of the dusk, the mind narrows to a single want, and what is left follows warmth the way water runs downhill: lamps, cook-fires, the heat in another person's chest. Families chained them up or locked them out rather than kill them, so husks still wear nightshirts, bindings and tether-chains, and many clutch a dead lantern they cannot put down. They attack with hands and teeth and pile onto anything warm. Death: a husk does not crumble at once — the last of its body heat leaves as a breath of orange sparks, and only then does the grey flesh flake to ash.

**Roles it has:** swarmer, melee (puller), exploder, bruiser (splits), summoner.  
**Deliberately lacks:** ranged and caster — a husk fight is a pure rush from every side; the Coldcaller makes the rush bigger instead of giving it a back line.  
**Regions:** **wastes** (signature), crypt, mire, sands.  
**Rigs:** A ×5.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `husk` | 飢骸 | Husk | swarmer | Melee | A | existing (built) | physical | - | 0.6 | 0.75 | 5.6 | 1 | Zombie_Scratch; idle Zombie_Idle_Loop, walk Zombie_Walk_Fwd_Loop | wastes, crypt, mire, sands |
| `husk_bloated` | 腫脹飢骸 | Bloated Husk | exploder | Exploder | A | existing (built) | poison | poison 0.5 over 4 s (burst) | 1.5 | 2.4 | 2.9 | 1.15 | Zombie_Scratch (burst wind-up); Zombie idle and walk | wastes, crypt, mire, sands |
| `husk_chained` | 拖鏈飢骸 | Tethered Husk | melee (puller) | Melee + Hook | A | variant of `husk` | physical | pull; slow 0.3 for 2 s | 1.1 | 1 | 4.2 | 1.05 | Melee_Hook (chain lash), OverhandThrow (chain cast), Zombie_Scratch | wastes, crypt, mire |
| `husk_huddle` | 抱暖骸團 | The Huddle | bruiser (splits) | Brute + SplitOnDeath | A | base | physical | slow 0.3 for 2 s | 3 | 1.7 | 2.6 | 1.5 | Sword_Attack (double-arm slam), Melee_Hook; Zombie idle and walk | wastes, crypt |
| `husk_wailer` | 號寒骸 | Coldcaller | support (summoner) | Caller | A | variant of `husk` | physical | fear 1 s (wail, 4 m) | 0.8 | 0.6 | 3.6 | 1 | Spell_Simple_Enter + Spell_Simple_Idle_Loop (head-back wail), Zombie_Scratch | wastes, crypt, mire |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `husk` | 飢骸 | Emaciated grey figure with a hunched neck, hanging jaw, sunken ribbed torso and long claws reaching forward: a forward-leaning question mark in rags. Materials: ashen skin, rags; no accent. |
| `husk_bloated` | 腫脹飢骸 | Grotesquely swollen: a huge taut belly wider than the shoulders, split skin, stubby fat arms held away from the body — a near-sphere on short legs. Accent: sickly yellow-green pustules that brighten before it bursts. |
| `husk_chained` | 拖鏈飢骸 | A husk still wearing the iron collar and waist-chain its family locked on; the chain ends in the torn-out tether post, a heavy stake with a ring, which it drags behind and throws like a flail. Silhouette: a hunched body with a long sagging line running back to a block on the ground. Materials: rust iron, splintered post; no accent. |
| `husk_huddle` | 抱暖骸團 | Four or five husks who froze together sharing warmth and never let go: one walks, the rest are fused to its back and shoulders, so the mass has extra heads at odd heights and a fringe of limp arms that sway. A wide, lumpy, top-heavy mound with no clear neck, under one shared blanket frozen stiff. Accent: a faint orange warmth deep in the middle, visible between the bodies. On death three husks peel off. |
| `husk_wailer` | 號寒骸 | A thin husk standing unusually straight, head thrown back, jaw dislocated wide open, both hands cupped at the mouth; a dead lantern hangs from a cord round its neck and swings at the belly. A vertical "trumpet" pose, unmistakable against the stooped pack. No glow — its cue is the sound and a visible plume of breath. |

### 2.3 寂滅教團 `cult` — The Hushed Order

*The order that calls the dusk a mercy and puts out every fire it finds.*

**What they were, what the dusk made them.** The Order teaches that the Sun-Forge did not fail — it was allowed to rest — and that every lamp lit since is a scream in a house of sleep. Its members take the vow of quiet (lips sewn or masked), carry snuffers instead of torches, and travel with wet shrouds, cold censers and bells whose clappers have been cut out. They are living people, organised and drilled: a complete army with skirmishers, line troops, marksmen, priests, healers and champions, which is why they are the one family that has every role. Their weapons extinguish — snuffer-poles, bolts that eat light, smoke that mends their own. Death: silent; the body folds, the mask falls, and the ash is black with a rim of white sparks — their own fire, finally let out.

**Roles it has:** swarmer, melee, ranged, caster, bruiser, support (shielder), healer, debuffer, stealth pouncer.  
**Deliberately lacks:** nothing — the Order is the game's reference "full army"; every other family is defined by what it is missing compared with this one.  
**Regions:** frost, crypt, sands, **sanctum** (signature), abyss.  
**Rigs:** A ×9.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `cultist` | 寂滅教徒 | Hushed Acolyte | melee | Melee | A | existing (built) | physical | - | 0.85 | 1.05 | 4.6 | 1 | Sword_Regular_A, Sword_Regular_C, Melee_Hook | crypt, sands, sanctum, abyss |
| `cult_caster` | 寂滅祭司 | Hushed Priest | caster | Caster | A | existing (built) | shadow | - | 0.75 | 1.15 | 3.4 | 1 | Spell_Simple_Shoot | crypt, sands, sanctum, abyss |
| `knight` | 墮落騎士 | Fallen Knight | bruiser | Brute | A | existing (built) | physical | stun 0.6 s | 2.6 | 1.7 | 3.6 | 1.15 | Sword_Attack, Sword_Heavy_Combo | frost, crypt, sanctum, abyss |
| `choir` | 靜默唱詩者 | Silent Chorister | support (shielder) | Support | A (legless) | existing (built) | shadow | - | 0.9 | 0.8 | 3 | 1 | Spell_Simple_Shoot | sanctum, abyss |
| `cult_snuffer` | 捻燈人 | Snuffer | melee (debuffer, reach) | Melee + Drain + Extinguish | A | variant of `cultist` | shadow | weaken 0.25 for 4 s; resource drain | 1 | 0.95 | 4 | 1.05 | Sword_Dash (pole thrust), Sword_Attack (overhead cap) | frost, crypt, sands, sanctum, abyss |
| `cult_crossbow` | 緘口弩手 | Sealed Marksman | ranged | Ranged | A | variant of `cultist` | physical | vulnerable 3 s | 0.7 | 1 | 3.8 | 1 | Pistol_Aim_Neutral, Pistol_Shoot, Pistol_Reload | crypt, sands, sanctum |
| `cult_censer` | 冷香侍 | Cold Censer | support (healer) | Healer | A | variant of `cult_caster` | shadow | - | 0.85 | 0.6 | 3.2 | 1 | Spell_Simple_Idle_Loop (heal channel), Melee_Hook (censer swing); idle Idle_Lantern_Loop | crypt, sanctum, abyss |
| `cult_flagellant` | 熄膚者 | Skin-snuffed | swarmer | Melee | A | variant of `cultist` | physical | bleed 0.2 over 3 s | 0.5 | 0.8 | 5.8 | 0.95 | Punch_Jab, Punch_Cross, Zombie_Scratch; run Sprint_Loop | crypt, sanctum, abyss |
| `cult_assassin` | 滅跡者 | Trackless | stealth pouncer | Stalker | A | variant of `cultist` | shadow | vulnerable 3 s (opening strike) | 0.7 | 1.3 | 5.4 | 1 | Sword_Dash, Sword_Regular_Combo, NinjaJump_Start / NinjaJump_Land, Roll | sands, sanctum, abyss |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `cultist` | 寂滅教徒 | Hooded ash-grey robe with a rope belt, blank oval mask, candle wax crusted on the shoulders, curved dagger held reversed. A plain pointed-hood triangle — the baseline Order silhouette. Accent: cold white mask. |
| `cult_caster` | 寂滅祭司 | Dark robes with a pale stole, a tall cowl bent forward like a snuffed wick, and a staff topped by a ring of extinguished candles. Tall hooked hood plus ringed staff. Accent: cold white sigil on the chest. |
| `knight` | 墮落騎士 | Black corroded full plate, torn cape, a horned and crested helm with one horn snapped off, greatsword. Heavy inverted-triangle torso with an asymmetric horn. Accent: smouldering ember seams in the armour. |
| `choir` | 靜默唱詩者 | Floating bell-shaped vestment thinning into trailing cloth, a tall arched veil over a high comb framing a blindfolded stitched face, hands pressed round a small brass bell, a halo ring standing behind the head. Arch plus ring plus bell-skirt. Accent: cold white halo. |
| `cult_snuffer` | 捻燈人 | Robed figure with the hood thrown back to show a shaved, wax-sealed scalp, carrying a three-metre pole that ends in a large bell-shaped snuffer cone; a coil of wet shroud is slung like a bandolier across the chest. The pole with its cone stands a head and a half above every other cultist. Materials: blackened brass, grey wool; accent: a thread of white smoke leaking from the cone. |
| `cult_crossbow` | 緘口弩手 | Short hooded cape cut off at the elbows, the hood's long tail hanging to the knees and weighted with a lead seal, the lower face covered by a riveted iron gag-plate; he carries a heavy hand-crossbow whose bow-stave is wider than his own shoulders, braced level on the forearm, and a fan of bolts in a chest bandolier. A wide horizontal bar across a small body, with a long tail behind. Materials: iron, waxed wood; accent: white fletching on the bolt. |
| `cult_censer` | 冷香侍 | Wide-sleeved deacon's robe under a tall mitre split into two prongs like a tuning fork, with a veil hanging between them, swinging a censer the size of a head on a long chain; a second censer hangs at the hip. A forked hat plus a pendulum arc. Materials: tarnished silver, white linen; accent: pale blue-white smoke that pours downward from the censer. |
| `cult_flagellant` | 熄膚者 | Bare-chested penitent whose skin is mottled with round burn scars where torches were put out on it; shaved head bound in a rope crown, a snuffed torch stub in each fist. A lean, bare-armed runner with a spiky rope halo — the only unrobed Order silhouette. No glow; soot-black hands. |
| `cult_assassin` | 滅跡者 | Wrapped close in wet black shroud strips from crown to ankle, no robe skirt, forearms bare; two long snuffer-hooks (sickle blades with a cone pommel) held out wide and low. A slim, crouched X-shape with two crescents. No accent while stealthed; a white line across the eyes once revealed. |

### 2.4 縛魂 `wraith` — The Bound

*Souls that no pyre released, chained to their own shrouds by the Order as wardens and as proof.*

**What they were, what the dusk made them.** A soul was meant to rise with the smoke. With no pyre it stays where the body fell and slowly forgets everything but the cold. The Order finds these and keeps them — a chain twice round the shroud, a seal with the Order's mark — as wardens, and as evidence that rest is kinder than fire. Wraiths are drawn to the ember in the hero the way the Unburnt are, but they cannot hold it: their touch only takes heat. They drift through walls of bodies, blink away from blades, and nest in dead lanterns. Death: the chain drops, the shroud collapses empty, and one small warm spark rises straight up — the only monsters whose ash goes upward.

**Roles it has:** caster (teleporter), melee (phasing), swarmer (lantern wisps).  
**Deliberately lacks:** bruiser, ranged physical and every kind of support — wraiths are solitary griefs, each alone even in a pack; they never help each other, so the family has no force multiplier and is always a garnish on someone else's pack.  
**Regions:** frost, crypt, mire, sanctum, abyss.  
**Rigs:** A ×2, F ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `wraith` | 縛魂 | Wraith | caster (teleporter) | Teleporter | A (legless) | existing (built) | frost | chill 0.3 | 0.8 | 1.2 | 3.2 | 1 | Spell_Simple_Shoot | frost, crypt, mire, sanctum, abyss |
| `wraith_mourner` | 泣魂 | Mourner | melee (phasing) | Melee + Phasing | A (legless) | variant of `wraith` | frost | chill 0.3; fear 0.8 s (20% chance) | 0.9 | 1.1 | 4.4 | 1.1 | Zombie_Scratch, Melee_Hook; idle Zombie_Idle_Loop | frost, crypt, mire, sanctum |
| `wraith_gasp` | 燈魂 | Lantern-ghost | swarmer | Melee + Phasing + DeathBurst | F | base | frost | chill 0.2 | 0.3 | 0.7 | 5 | 1 | Attack1 lunge-flare, Attack2 shatter (death burst) | frost, crypt, mire, sanctum, abyss |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `wraith` | 縛魂 | Legless spectral torso whose shredded winding-sheet trails back into wisps, gaunt dark arms with long pale claws, a hollow cowl with two slanted soul-lights, a rusted chain cinched twice round the waist under the Order's seal. Accent: pale blue-white eyes. |
| `wraith_mourner` | 泣魂 | No cowl: a long veil of hair and shroud hangs straight down over a bowed head, and two arms far too long reach forward and down, the fingers trailing on the floor. The body is a narrow column bent at the top like a shepherd's crook; a broken chain swings loose from one wrist. Accent: a dim blue glow inside the chest where the heart was. |
| `wraith_gasp` | 燈魂 | A dead iron lantern floating at chest height with nobody holding it, its door hanging open and a small face-like wisp pressed against the cracked glass from inside; a short length of carrying-chain dangles underneath. Reads as a small box with a ring handle and a tail. Materials: rusted iron, soot-black glass; accent: cold blue-white wisp inside. |

### 2.5 盜燼眾 `reaver` — Ember-takers

*People who stopped tending fire and started stealing it — raiders who sell stolen embers to anyone, the Order included.*

**What they were, what the dusk made them.** Keeping a beacon alive costs a village everything it has. The Ember-takers decided long ago that it is easier to take a fire than to feed one: they break small beacons, strip caravans and carry the stolen embers off in iron cages to sell — to frightened towns at a ruinous price, or to the Order, which pays well for fires to put out. They are ordinary living people, and that is the unsettling part. They smear cold ash on their skin so husks cannot feel their warmth, wear strings of dead lanterns as tallies, and fight with a lamplighter's trade tools turned into weapons: hook-poles, wick-shears, oil pots. No magic at all. Death: they die like people — a real fall, no crumble; only when the stolen ember they carried gutters out does the body ash with it.

**Roles it has:** swarmer, melee (puller), bomber, support (aura), bruiser, handler (with hounds).  
**Deliberately lacks:** caster and healer — they are thieves, not believers: nothing they own is magic and nobody stops to help anybody.  
**Regions:** wastes, frost, crypt, sands.  
**Rigs:** A ×6.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `reaver_cutter` | 剪芯匪 | Wick-cutter | swarmer | Melee | A | base | physical | bleed 0.25 over 3 s | 0.55 | 0.8 | 5.6 | 0.95 | Punch_Jab, Punch_Cross, Sword_Regular_A; Roll to dodge | wastes, frost, crypt, sands |
| `reaver_hook` | 鉤燈匪 | Hook-pole Reaver | melee (puller) | Melee + Hook | A | variant of `reaver_cutter` | physical | pull; slow 0.3 for 1.5 s | 1 | 1.05 | 4.4 | 1 | Sword_Dash (hook thrust), Sword_Attack (overhead pull), Sword_Regular_B | wastes, frost, crypt, sands |
| `reaver_firepot` | 潑油匪 | Oil-slinger | ranged (bomber) | Bomber | A | variant of `reaver_cutter` | fire | burn 0.3 over 3 s; burning ground 4 s | 0.7 | 1 | 3.8 | 1 | OverhandThrow | wastes, frost, crypt, sands |
| `reaver_cagebearer` | 提籠人 | Cage-bearer | support (aura) | Support + Aura | A | variant of `reaver_cutter` | fire | - | 1 | 0.6 | 3.6 | 1 | Melee_Hook (cage swing); idle Idle_Lantern_Loop | wastes, frost, crypt, sands |
| `reaver_brute` | 砸爐漢 | Hearth-breaker | bruiser | Brute | A | base | physical | knockback; stun 0.5 s | 2.8 | 1.8 | 3.2 | 1.4 | Sword_Attack, Sword_Heavy_Combo | wastes, frost, crypt, sands |
| `reaver_houndmaster` | 馴灰人 | Ash-handler | support (handler) | Caller + Escort | A | variant of `reaver_cutter` | physical | - | 0.9 | 0.8 | 4 | 1 | Sword_Regular_A (goad), Spell_Simple_Shoot (whistle) | wastes, frost, sands |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `reaver_cutter` | 剪芯匪 | Small, quick raider in a short patched coat, face smeared white with ash, carrying a pair of oversized wick-shears taken apart — one long blade in each hand. A string of thumb-sized dead lanterns crosses the chest. Compact silhouette with two long narrow triangles at the hands. Materials: leather, ash paint, blued iron; no accent. |
| `reaver_hook` | 鉤燈匪 | Lanky raider in a long coat with the collar turned up and a wide-brimmed lamplighter's hat, carrying a lamplighter's pole twice his height that ends in a hook-and-snuffer head; a coil of rope on one shoulder. Hat brim plus a very long thin diagonal pole with a hooked tip. No accent. |
| `reaver_firepot` | 潑油匪 | Stocky raider with a yoke-frame on the back carrying six round clay oil pots in two rows, like a rack of skulls behind the shoulders; a lit slow-match is wound round one forearm and a pot is held ready in the throwing hand. Leather hood with a glass eye-slit. Accent: a small warm-orange match glow at the wrist. |
| `reaver_cagebearer` | 提籠人 | Hunched carrier in a soot-blackened apron holding the gang's prize out at arm's length on a short pole: an iron cage the size of a birdcage with a live stolen ember inside. The other arm is wrapped to the shoulder in a thick quilted glove. Silhouette: a bent figure with a barred lantern-box out in front. Accent: the brightest warm amber light any regular enemy carries — it lights its allies. |
| `reaver_brute` | 砸爐漢 | Huge-bellied raider stripped to the waist, wearing a beacon's iron brazier bowl upside-down as helmet and pauldrons in one piece (the rim rests on his shoulders, an eye-slot hacked in), swinging a maul made from a beacon door-ring on a timber. The dome-on-shoulders head makes him neckless. Materials: soot-black iron, scarred skin, rope; no accent. |
| `reaver_houndmaster` | 馴灰人 | Lean handler in a shaggy cloak of cinder-hound pelts with the head-skin pulled up as a hood, a bone whistle on a cord, a short hooked goad, and three slack leashes trailing from the left fist. A shaggy outline with ragged ear-points on the hood. No accent. Arrives with two cinder-hounds. |

### 2.6 灰暮野物 `dusk` — Dusk-fauna

*The animals that survived a thousand sunless years by learning to hunt heat.*

**What they were, what the dusk made them.** Nothing that needed the sun is left. What remains has spent a thousand years learning one trick: find warmth and take it. Dusk-fauna are pale, large-eared or eyeless, with heat-pits instead of eyes and coats full of ash; they sleep on old hearth-stones and warm brass and follow travellers for the heat of their lamps. They are not evil — they are hungry — and they are the only family that comes to the hero out of appetite rather than belief or grief. Each region has its own beasts. Attacks are bites, charges, swoops and pounces; several steal the hero's resource (the ember) instead of life. Death: an animal death — they drop, kick once, and the ash that takes them is plain grey with no sparks at all.

**Roles it has:** swarmer (pack), charger, bruiser, stealth pouncer, ranged spitter, flyer (thief), flyer (ceiling ambusher), flyer (drain), swarm.  
**Deliberately lacks:** caster, support and reviver — they are animals; nothing in the family thinks about the fight.  
**Regions:** wastes, frost, crypt, mire, sands, sanctum, abyss.  
**Rigs:** B ×5, E ×3, F ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `dusk_hound` | 嗅燼犬 | Cinder-hound | swarmer (pack) | Melee | B | base | physical | bleed 0.2 over 3 s | 0.5 | 0.75 | 6.2 | 1 | Attack1 bite, Attack2 lunge | wastes, crypt, sands |
| `dusk_boar` | 犁牙豕 | Ploughtusk | charger | Charger | B | base | physical | knockback | 1.6 | 1.5 | 3.8 | 1.2 | Attack1 gore, Attack2 charge | wastes, mire |
| `dusk_yak` | 負霜牛 | Rimeback | bruiser | Brute | B | variant of `dusk_boar` | frost | chill 0.4; knockback | 3 | 1.8 | 3 | 1.5 | Attack1 horn sweep (cone), Attack2 stomp (circle) | frost |
| `dusk_lynx` | 雪隙貓 | Crevice-cat | stealth pouncer | Stalker | B | variant of `dusk_hound` | physical | bleed 0.3 over 3 s; vulnerable 2 s on pounce | 0.7 | 1.2 | 6.4 | 1 | Attack1 swipe, Attack2 pounce | frost |
| `dusk_lizard` | 臥銅蜥 | Brass-basker | ranged | Ranged | B | base | poison | vulnerable 2 s (corrosion) | 0.8 | 0.9 | 3.6 | 1 | Attack1 spit, Attack2 tail whip | sands |
| `dusk_crow` | 啄燼鴉 | Ember-pecker | flyer (thief) | Flyer + Drain | E | base | physical | resource drain; weaken 0.15 for 3 s | 0.4 | 0.6 | 6.5 | 1 | Attack1 swoop-peck, Attack2 hover-caw | wastes, sands, sanctum |
| `dusk_bat` | 倒懸蝠 | Hearth-bat | flyer (ambusher) | Flyer + Dormant(ceiling) | E | base | physical | bleed 0.25 over 3 s | 0.45 | 0.8 | 6 | 1 | Attack1 dive-bite, Attack2 screech (cone, slow) | frost, crypt, mire, abyss |
| `dusk_gadfly` | 吸暖虻 | Warmth-fly | flyer (drain) | Flyer + Drain | E | base | poison | poison 0.3 over 3 s; heals itself | 0.5 | 0.7 | 5.6 | 1.1 | Attack1 proboscis stab, Attack2 latch-and-drain | mire |
| `dusk_moths` | 撲火蛾群 | Flame-moths | swarm | Swarm | F | base | physical | weaken 0.2 while inside; dims the hero's light radius | 0.9 | 0.5 | 4.6 | 1 | Attack1 engulf pulse, Attack2 scatter-and-reform | wastes, crypt, mire, sands, sanctum |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `dusk_hound` | 嗅燼犬 | Lean, deep-chested hound with no eyes — a smooth brow over two flared heat-pit nostrils — upright bat-like ears and a ragged mane of ash-clotted fur along neck and spine, whip tail. A narrow greyhound line with a spiky ridge. Materials: pale hairless flank, ash mane; accent: dull orange inside the nostril pits. |
| `dusk_boar` | 犁牙豕 | Low, wedge-shaped boar that roots through ash for buried embers: a huge head a third of its length, lower tusks grown forward and flat like two ploughshares, a hump of clinker-crusted bristle over the shoulders, tiny hindquarters. A doorstop wedge. Accent: faint ember glow in the mouth. |
| `dusk_yak` | 負霜牛 | Massive shaggy mountain ox whose coat hangs to the ground in ice-matted ropes so that no legs are visible — a moving haystack — with one wide pair of horns curving down and forward past the jaw. A drift of snow rides its back. No glow; a heavy breath plume. |
| `dusk_lynx` | 雪隙貓 | Long-legged white cat with paws twice too large (snowshoes), a very short body, tufted ears as long as its head and a thick club tail held straight up as a balance. A high-shouldered, stilted outline. Accent: two pale green eye-shines — the only eyes in the family. |
| `dusk_lizard` | 臥銅蜥 | Low sprawling monitor lizard, belly on the ground and legs out sideways, with a sail of thin verdigris-green plates along the back (it basks on warm metal) and a throat sack that swells before it spits. Very flat and long with a notched sail. Accent: verdigris-green throat glow. |
| `dusk_crow` | 啄燼鴉 | Ragged oversized crow with a heavy pick-axe beak, a bare scorched head and throat (from eating coals) and wing feathers singed to uneven fingers; it carries a stolen ember in its beak. Broad ragged wings, big wedge beak. Accent: a single hot orange point at the beak. |
| `dusk_bat` | 倒懸蝠 | Bat with ears like two dishes, each larger than its body, a flat leaf-nose and a short stub tail; the wing membranes are torn into scallops along the trailing edge. Two big discs on a small body. No accent. |
| `dusk_gadfly` | 吸暖虻 | Bloated horsefly the size of a dog: a translucent abdomen like a hanging lamp-globe that fills and glows as it feeds, a long straight needle proboscis, legs dangling in a bunch. A globe hanging under blurred wings. Accent: yellow-green abdomen that brightens to amber when fed. |
| `dusk_moths` | 撲火蛾群 | A cloud of several hundred pale moths drawn as one actor: a churning ball about three metres across, denser at the core, that streams into a comet shape when it moves. No single body. Accent: the wings catch firelight — the cloud glitters warm near any flame and is ash-grey otherwise. |

### 2.7 霜縛 `frost` — Frostbound

*The garrison of the pass, frozen at its posts and still holding them.*

**What they were, what the dusk made them.** 凍骨關 was held by a garrison sworn to keep the pass beacon alight. When the fuel trains stopped they burned the stockade, then their spear-shafts, then sat down around the cold brazier in rank order and froze. Their oath outlasted them. The Frostbound still stand watch, still answer the horn, still walk the supply road behind a dead mule — and the mountain hunters who came to loot them froze too, and became the horned stalkers that run ahead of the line. Ice has grown through them like a second armour and fused their weapons to their hands. Attacks are drilled spear-work, horn-blasts of killing wind and the slow weight of ice. Death: they shatter — the ice goes first in a burst of shards, then the body inside falls to ash that the wind takes sideways.

**Roles it has:** melee, swarmer (leaper), support (breath), ambusher-tank (ice shell), bruiser, quadruped charger (bursts).  
**Deliberately lacks:** a true caster and a reviver — their only magic is the cold itself, delivered by horn and by weather; ranged pressure comes from the frost-skinned Unburnt javelineers that march with them.  
**Regions:** **frost** (signature).  
**Rigs:** A ×5, B ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `frostbound` | 霜縛屍兵 | Frostbound Soldier | melee | Melee | A | existing (built) | frost | chill 0.25 | 1 | 1.05 | 3.8 | 1 | Sword_Regular_A, Sword_Dash | frost |
| `rime_stalker` | 霜角獵者 | Rimehorn Stalker | swarmer (leaper) | Leaper | A | existing (built) | frost | - | 0.7 | 0.9 | 6 | 1 | Zombie_Scratch, Melee_Hook; NinjaJump_Start / NinjaJump_Land | frost |
| `rime_horn` | 凍號手 | Rime-hornblower | support (breath) | Caster + Breath | A | variant of `frostbound` | frost | chill 0.3 per tick, freeze 1 s on the 4th tick; knockback | 0.9 | 0.9 | 3.4 | 1 | Spell_Simple_Enter / Spell_Simple_Idle_Loop / Spell_Simple_Exit (horn blast), Melee_Hook | frost |
| `rime_sentry` | 立凍哨 | Standing Watch | ambusher (tank) | Melee + Dormant(statue) + Shell | A | variant of `frostbound` | frost | chill 0.4; root 1 s on the waking strike | 1.6 | 1.2 | 3 | 1.1 | Sword_Attack, Shield_OneShot; break-out with LayToIdle timing | frost |
| `rime_breaker` | 鑿冰巨卒 | Ice-breaker | bruiser | Brute | A | base | frost | chill 0.5; freeze 1 s at the slam's centre | 3.2 | 1.9 | 3 | 1.5 | Sword_Attack, Sword_Heavy_Combo | frost |
| `rime_packbeast` | 凍馱獸 | Frozen Pack-mule | charger (bursts) | Charger + DeathBurst | B | variant of `dusk_boar` | frost | chill 0.5; freeze 1 s (burst) | 1.2 | 1.6 | 3.4 | 1.15 | Attack1 kick, Attack2 charge | frost |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `frostbound` | 霜縛屍兵 | Frost-bitten corpse in a stiff hide coat and rimed fur mantle, spangen cap with nasal, the whole left shoulder and forearm locked in jagged ice, an icicle beard, and a spear whose head has frozen into a long ice blade. Accent: ice blue. |
| `rime_stalker` | 霜角獵者 | Lean hunched predator with crescent ice horns sweeping out and forward, a sail of ice needles down the spine, and fingers that end in long flat ice blades. Accent: a cold seam glowing under the spine sail. |
| `rime_horn` | 凍號手 | Garrison signaller with a curved signal horn two metres long frozen to his lips and both hands, so the horn points wherever he faces — a great crescent sweeping up over his head. A tattered guidon pole is strapped upright to his back. Crescent plus pole. Accent: ice-blue frost streaming from the horn's bell. |
| `rime_sentry` | 立凍哨 | A sentry frozen upright inside a rough pillar of clear ice as tall as a door — until he steps out wearing half of it: a slab still encases his head and one whole side like a sentry-box, a round shield is frozen to the other arm, short axe in hand. Reads as a standing block with one arm. Accent: pale blue light refracting through the ice slab. |
| `rime_breaker` | 鑿冰巨卒 | The garrison's road-clearer: a giant of a man in a hooded sealskin coat as stiff as a bell, with a wooden snow-plough blade — a V-shaped board taller than a door — strapped to his left arm like a ship's prow, and over the right shoulder a long pick whose head is a frozen boulder. A pair of snowshoes is lashed upright on his back like two paddles. A pointed bell with a prow in front. Materials: hide, grey timber, clear ice; accent: ice blue in the boulder's core. |
| `rime_packbeast` | 凍馱獸 | A dead supply mule still walking its route, head low, under a pack-saddle carrying two big kegs of frozen lamp-oil and a rack of bundled spears pointing up like a comb; icicles hang from belly and jaw. A boxy load with a row of spikes on top. Accent: blue-white glow inside the cracked kegs, brightening before it bursts. |

### 2.8 凍焰 `coldfire` — Frozen Flames

*The last flames of dead beacons, frozen mid-flicker into blue glass that burns cold.*

**What they were, what the dusk made them.** When a beacon dies in the high pass its last flame does not go out. It freezes: a tongue of fire turned to blue glass, still moving, a flicker that takes an hour. The Order holds these up as proof that fire itself wants to rest. They are right that the flames no longer suffer — they are hungry. A frozen flame burns backwards: it draws heat in. They drift down from the dead beacons, cluster on anything warm and take the fire out of it, and the glass-mites that graze on them carry shards of it on their backs. Every attack is cold: shard volleys, heat-draining auras, slow crushing embraces. Death: they crack with a sound like a struck glass, and for one instant the flame inside thaws and burns its true colour — a flash of warm orange — before it goes out.

**Roles it has:** swarmer (flicker), caster, bruiser, ranged crawler.  
**Deliberately lacks:** line melee, support and anything fast on foot — an elemental family that is all area and attrition, meant to be mixed with Frostbound infantry who pin the hero inside its zones.  
**Regions:** **frost** (signature).  
**Rigs:** A ×2, C ×1, F ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `cf_flicker` | 凍焰苗 | Frozen Flicker | swarmer | Melee + Phasing + DeathBurst | F | variant of `wraith_gasp` | frost | chill 0.25; shard burst on death | 0.35 | 0.7 | 5.2 | 1 | Attack1 lunge, Attack2 shatter | frost |
| `cf_spirit` | 寒焰靈 | Cold-flame Spirit | caster | Caster + Trail | A (legless) | base | frost | chill 0.35; frost ground 4 s | 0.8 | 1.2 | 3.2 | 1.1 | Spell_Simple_Shoot, Spell_Simple_Idle_Loop | frost |
| `cf_hulk` | 冰爐巨軀 | Brazier-hulk | bruiser | Brute + Aura + Hook | A | base | frost | chill 0.5; pull (heat-draw) | 3.4 | 1.9 | 2.8 | 1.55 | Sword_Attack (two-fist smash), Melee_Hook, Sword_Heavy_Combo | frost |
| `cf_mite` | 噬焰晶蟲 | Glass-mite | ranged (crawler) | Ranged | C | variant of `rust_helm` | frost | chill 0.25 | 0.6 | 0.85 | 4.2 | 0.9 | Attack1 shard flick, Attack2 pinch | frost |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `cf_flicker` | 凍焰苗 | A knee-high candle-flame shape made of faceted blue glass, hovering a hand above the ground and leaning the way it moves; three curled tongues, the middle one tallest. A clean teardrop with a forked tip. Accent: ice blue with a pinpoint of warm orange deep inside. |
| `cf_spirit` | 寒焰靈 | A man-high frozen flame that has grown a torso: its "robe" is a skirt of glass tongues that flares upward (wider at the top of the skirt than at the bottom), its two arms are single long flame-blades, and instead of a head it has a tall split flame-tongue. An inverted cone balanced on a point — everything flares upward. Accent: ice blue with an orange core in the chest. |
| `cf_hulk` | 冰爐巨軀 | A dead beacon's iron brazier — a basket of bars a metre wide — worn as a ribcage by a hulking body of blue ice that grew up through it; the frozen fire erupts from the basket top as a crown of glass tongues where a head would be, and the brazier's three legs stick out of its back. Arms are rough ice columns ending in fused clubs. Accent: ice blue glowing through black iron bars. |
| `cf_mite` | 噬焰晶蟲 | Flat crab-like crawler whose shell is a cluster of blue glass flame-shards growing upward like a crown of candles; small body, six needle legs, two flat plough-jaws for scraping frozen flame. A low disc with a spiky crown. Accent: ice-blue shard tips. |

### 2.9 馱薪巨人 `hauler` — Fuel-bearers

*The giants who carried fuel to the Sun-Forge, still hauling a thousand years after it went out.*

**What they were, what the dusk made them.** The Sun-Forge ate forests. To feed it the old kings bound a people of giants — 薪役, the Fuel-bearers — by an oath sworn on the Forge itself: carry until the fire is fed. The fire was never fed again, so the oath never ended. They walk the old fuel roads from the passes down to the abyss with hods the size of carts, picking up anything that will burn: timber, coal, oil, furniture and — now that there is little else — anything warm. A Fuel-bearer is not cruel; it does not distinguish between a log and a traveller. They appear singly among other families' packs. Attacks are loads swung, thrown and dropped. Death: the giant kneels, sets its load down with care, and only then crumbles; the hod stays behind as a prop.

**Roles it has:** giant bruiser (sweeper), giant siege thrower, giant grabber, giant bomber.  
**Deliberately lacks:** every small role — by design this is the "one giant in someone else's pack" family; it never spawns as a pack of its own and has no swarmers, casters or support.  
**Regions:** wastes, **frost** (signature), sands, abyss.  
**Rigs:** A ×4.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `hauler_yoke` | 負軛巨人 | Yoke-giant | giant bruiser | Brute | A | base | physical | knockback | 5 | 2.1 | 2.9 | 1.8 | Sword_Heavy_Combo (yoke spin), Sword_Attack, Melee_Hook | wastes, frost, sands |
| `hauler_hod` | 背薪巨人 | Hod-giant | giant ranged (siege) | Bomber | A | variant of `hauler_yoke` | physical | knockback; stun 0.6 s on a direct hit | 4.5 | 2.2 | 2.6 | 1.8 | OverhandThrow (log), Sword_Attack (close slam) | frost, sands |
| `hauler_collector` | 拾暖巨人 | Warmth-gatherer | giant special (grabber) | Brute + Grab | A | variant of `hauler_yoke` | physical | root while held 2 s, then thrown (knockback) | 4.8 | 2 | 3.1 | 1.75 | Melee_Hook (grab), PickUp_Table (stow), OverhandThrow (throw), Sword_Attack | wastes, frost, sands |
| `hauler_stoker` | 添爐巨人 | Stoker-giant | giant bomber | Bomber | A | variant of `hauler_yoke` | fire | burn 0.4 over 3 s; burning ground 5 s | 5.2 | 2.2 | 2.8 | 1.85 | OverhandThrow (slag fling), Sword_Attack (shovel slam) | abyss |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `hauler_yoke` | 負軛巨人 | Soot-black giant bent under a shoulder-yoke as long as he is tall, each end hung with a chained bundle of logs that swings out as he turns; small head pushed forward below the bar, short bowed legs. A wide "T" like a balance scale. Materials: tarred timber, chain, skin like bark; accent: the Forge brand on his chest, a dull orange ring. |
| `hauler_hod` | 背薪巨人 | Giant carrying a wicker-and-timber hod on his back that rises a full body-height above his head, stacked with tree trunks like arrows in a quiver; he reaches back over his shoulder for the next one. A tall rectangle towering behind a stooped body. Accent: the brand ring on his chest. |
| `hauler_collector` | 拾暖巨人 | Giant with a domed wicker cage strapped to his back like an enormous lobster-pot, things still moving inside it, and one arm wrapped to the shoulder in quilted hide that ends in iron grabbing-tongs as long as a man; the other hand is empty and open. A dome on the back plus one over-long pincer arm. Accent: the brand ring, and a faint warm glow leaking from the cage. |
| `hauler_stoker` | 添爐巨人 | Giant burnt hairless and glassy, in a slag-crusted leather apron to the ankles and a riveted iron face-shield with one square window, carrying a coal shovel with a blade the size of a door heaped with glowing slag. Flat rectangular head, apron bell, long shovel. Accent: lava orange on the shovel load and in the brand ring. |

### 2.10 葬甲 `sexton` — Sexton-beetles

*Carrion beetles the undertakers once kept to clean bones — grown huge on a thousand years of unburnt dead, and still burying everything they find.*

**What they were, what the dusk made them.** When the pyres stopped, the gravediggers needed helpers: burying-beetles, bred to strip and inter the dead quickly. The trade died; the beetles did not. Grown to the size of dogs and oxen on a thousand years of graves, they do what they were bred for with patient thoroughness: find a body, dig under it, let it sink, cover it. They are not particular about whether the body has stopped moving. They nest in coffins and wear grave goods caught on their shells. Attacks: bites, undermining the ground beneath the hero (rooted — interred to the knees), larvae that pour out of anything dead. Death: they flip onto their backs with the legs folding inward; the ash is black and greasy.

**Roles it has:** swarmer (grubs), armoured melee, burrower, brood-mother.  
**Deliberately lacks:** ranged, caster and support — pure vermin pressure from below; wastes packs get their reach from the Unburnt.  
**Regions:** **wastes** (signature), crypt.  
**Rigs:** C ×4.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `sexton_grub` | 墳蛆 | Grave-grub | swarmer | Melee | C | variant of `sexton_beetle` | physical | slow 0.15 for 2 s | 0.25 | 0.5 | 4.8 | 0.8 | Attack1 bite, Attack2 leap-latch | wastes, crypt |
| `sexton_beetle` | 葬甲蟲 | Sexton-beetle | melee (armoured) | Melee + FrontArmour | C | base | physical | bleed 0.2 over 3 s | 1.1 | 1 | 4 | 1 | Attack1 mandible snap, Attack2 shell ram | wastes, crypt |
| `sexton_digger` | 掘穴蟲 | Undertaker | burrower | Burrower | C | variant of `sexton_beetle` | physical | root 1.5 s (interred) | 0.9 | 1.1 | 4.4 | 1.05 | Attack1 emerge-bite, Attack2 undermine (pit under the target) | wastes, crypt |
| `sexton_matron` | 負棺母蟲 | Coffin-mother | mother (bruiser) | Spawner | C | variant of `sexton_beetle` | physical | knockback | 3 | 1.5 | 2.8 | 1.7 | Attack1 ram, Attack2 brood-spill | wastes, crypt |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `sexton_grub` | 墳蛆 | Fat segmented larva as long as a forearm, pale and ridged, with a hard black head-capsule and two short hook jaws; it moves in humping inchworm arcs. A string of beads with a dark bead at the front. No accent; wet sheen. |
| `sexton_beetle` | 葬甲蟲 | Dog-sized burying beetle: a domed black wing-case with two dull orange bands, a flat shovel-shaped head-shield wider than its body, clubbed antennae, and a scrap of shroud and a tin grave-marker caught on its back. A half-dome behind a wide flat shovel. Accent: the orange bands (not emissive; they read warm in firelight). |
| `sexton_digger` | 掘穴蟲 | Longer, flatter beetle with enormous spade forelegs held out front like a mole's hands and almost no visible head; the wing-case is ridged and caked with grave soil, a coffin handle hooked on one leg. A wedge with two big paddles. No accent. |
| `sexton_matron` | 負棺母蟲 | Ox-sized beetle carrying a whole coffin on her back, lashed on with roots and her own hardened secretion, the lid ajar and grubs spilling from the gap; her wing-cases are vestigial flaps under it. A long box on six legs with a small shovel head in front. Accent: sickly pale green glow from inside the coffin. |

### 2.11 炭木 `char` — Charwood

*The half-burnt forest of the first winter — trees that were set alight and never finished burning.*

**What they were, what the dusk made them.** In the first winter after the Forge died, people burned the forests of the wastes to stay alive. The fires went out before the trees were consumed, and charcoal holds heat for a very long time. The Charwood are those trees: black, split, hollow, with a seam of ember still creeping somewhere inside them after a thousand years. They want what any half-burnt thing wants — to finish. They move toward fire to complete their own burning, and they remember who lit them. Fire damage ignites them: a lit Charwood is faster and deadlier but is burning itself to death. Attacks: branch-clubs, roots from below, a noose of the ropes people were hanged with for hoarding wood. Death: they collapse inward into a heap of glowing charcoal that takes several seconds to go dark.

**Roles it has:** swarmer (stumps), melee, bruiser (reach), burrowing root (ambush turret).  
**Deliberately lacks:** ranged, caster and support — the forest only holds ground; it is slow, and the danger is being rooted in the middle of it. (In the mire it appears as waterlogged bog-wood that cannot be ignited.)  
**Regions:** **wastes** (signature), mire.  
**Rigs:** A ×3, D ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `char_stump` | 殘樁 | Stumpling | swarmer | Melee + Flammable | A | base | physical | none (lit: burn 0.2 over 3 s) | 0.5 | 0.7 | 4.6 | 0.75 | Punch_Jab, Zombie_Scratch; walk Zombie_Walk_Fwd_Loop | wastes, mire |
| `char_walker` | 炭木人 | Charwalker | melee | Melee + Flammable | A | base | physical | slow 0.2 for 2 s (lit: burn 0.3 over 3 s) | 1.2 | 1.1 | 3.6 | 1.15 | Sword_Attack, Melee_Hook, Sword_Regular_B | wastes, mire |
| `char_hangtree` | 弔枝木 | Gallows-tree | bruiser (reach) | Brute + Hook + Flammable | A | base | physical | pull; root 1 s (noose); knockback | 3.2 | 1.8 | 2.6 | 1.6 | Sword_Attack (bough slam), OverhandThrow (noose cast), Melee_Hook | wastes, mire |
| `char_root` | 蝕根 | Cinder-root | ambusher (burrowing turret) | Burrower + Flammable | D | variant of `ash_worm` | physical | root 1.2 s (lit: burn 0.3 over 3 s) | 0.9 | 1.2 | 5 | 1 | Attack1 lash, Attack2 coil-grab; Spawn = erupt | wastes, mire |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `char_stump` | 殘樁 | A sawn-off stump the height of a child walking on four splayed root-toes, flat-topped (axe-cut, with the growth rings showing), with two short broken branches for arms. A flat-headed block — the only enemy with a perfectly horizontal top. Materials: charcoal with white ash edges; accent: a thin orange ember line around the cut rim. |
| `char_walker` | 炭木人 | Tall thin burnt trunk split up the middle into two legs, one arm a long forked branch that reaches the ground, the other snapped short; the top ends in a jagged lightning-split crown with no head. A leaning, asymmetric upside-down "Y". Accent: ember-orange glow deep in the trunk split. |
| `char_hangtree` | 弔枝木 | Massive hollow trunk with one great horizontal bough — the gallows arm — growing out at shoulder height on its right, three frayed nooses and a rusted gibbet cage hanging from it and swinging as it walks; the left side is only a stump. An inverted "L": nothing else in the game is that lopsided. Accent: ember glow inside the hollow, seen through a knot-hole mouth. |
| `char_root` | 蝕根 | A single thick charred taproot that bursts from the ground and stands two metres tall like a rearing snake, tapering to a splintered tip, with a fringe of fine rootlets down its length that twitch. No head — a swaying hooked whip. It does not travel above ground. Accent: orange ember veins that pulse just before it strikes. |

### 2.12 灰塑 `ash` — Ash-casts

*Hollow figures of packed ash standing where people burned, walked by the wind that blows up from the abyss.*

**What they were, what the dusk made them.** Where many burned at once — the last great pyres, the towns that set themselves alight for one warm night — the ash kept their shapes. 灰塑 are hollow casts: a crust of packed ash around the space where a person was, standing or kneeling exactly as they died. The wind that rises from the abyss walks them. They are fragile and patient; break one and the ash sifts back together unless the pile is scattered. Fire cannot hurt what is already burnt. Attacks: choking embraces, ash blown into eyes and lungs (weaken), the sheer weight of a drift. Death: the game's signature death taken literally — they fall into a pile, and the pile may stand up again.

**Roles it has:** melee (reforms), caster (ash-fall), giant (splits), burrowing worm (breath).  
**Deliberately lacks:** swarmer, ranged physical, support and healer — the standard cast effectively dies twice, which is the family's substitute for numbers; immune to burn.  
**Regions:** wastes, abyss.  
**Rigs:** A ×3, D ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `ash_cast` | 灰塑人 | Ash-cast | melee | Melee + Reform | A | base | physical | weaken 0.15 for 3 s | 0.7 | 0.9 | 4 | 1 | Zombie_Scratch, Melee_Hook; LayToIdle when it reforms | wastes, abyss |
| `ash_weeper` | 灰泣者 | Ash-weeper | caster | Caster + Eruption | A | variant of `ash_cast` | shadow | weaken 0.25 for 4 s; slow 0.3 inside the ash-fall | 0.8 | 1.1 | 2.8 | 1 | Spell_Simple_Enter / Spell_Simple_Idle_Loop / Spell_Simple_Shoot | wastes, abyss |
| `ash_colossus` | 灰積巨像 | Drift-colossus | giant (splits) | Brute + SplitOnDeath | A | base | physical | weaken 0.3 for 4 s; knockback | 5.5 | 2.2 | 2.6 | 1.9 | Sword_Attack (double fist), Sword_Heavy_Combo | wastes, abyss |
| `ash_worm` | 潛灰蚓 | Ash-sifter | burrower (breath) | Burrower + Breath | D | base | physical | weaken 0.2; slow 0.4 for 2 s (ash cone) | 1.4 | 1.2 | 3 | 1.2 | Attack1 bite, Attack2 ash-blast cone; Spawn = emerge | wastes, abyss |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `ash_cast` | 灰塑人 | A hollow human figure of grey crust with a ragged hole clean through the chest (the background shows through it) and the top of the head missing like a broken eggshell; arms held slightly out as if balancing, fine ash pouring from the elbows. No glow — only a rim of dull red sparks at the edge of the chest hole. |
| `ash_weeper` | 灰泣者 | The cast of someone who died begging the sky: it walks on its knees (the shins fused into a trailing skirt of ash), back arched, both arms raised straight up with the hands crumbled away to stumps, face tilted up with an open hollow mouth. A "Y" on a mound. Accent: grey; red sparks in the mouth while it casts. |
| `ash_colossus` | 灰積巨像 | A drift that stood up: a slope-shouldered mass with no neck whose surface is made of dozens of half-emerged ash-casts — faces, backs and reaching arms pressed outward like a relief — and whose fists are knots of fused bodies. It trails a plume from its back as it walks. A soft mountain shape with a ragged plume. Accent: dull red sparks in the deep creases. Bursts into three ash-casts. |
| `ash_worm` | 潛灰蚓 | A segmented worm as thick as a barrel made of baked ash plates like roof tiles, rising two metres out of the ground; its front end is a flat round sieve-plate full of holes (no jaws) which it tips toward the hero to blow ash. A column with a disc on top. Accent: dull red glow behind the sieve holes. |

### 2.13 殮蛛 `shroud` — Shroud-weavers

*Spiders the barrow-wardens kept to wrap the dead in silk when there was no fire to burn them — still wrapping everything.*

**What they were, what the dusk made them.** A wrapped corpse does not rise. The old barrow-wardens knew it and kept 織殮蛛, shroud-spiders, to bind each body in silk before the lid went on. The wardens are gone; the spiders have kept the rite. Every web in the Catacombs is a winding-sheet, the cocoons hang in neat rows like a congregation, and the weavers treat anything that moves as a body not yet properly dressed. Their silk is grey with ash. Some cocoons are neither empty nor dead, and walk on strings. Attacks: binding silk (root), drops from the vault, bites that numb (slow). Death: they curl up with the legs drawn in tight; the silk they carried unravels in the air as threads of ash.

**Roles it has:** swarmer (spiderlings), ranged controller (web), ceiling ambusher, brood-mother, humanoid melee (puppeted cocoon).  
**Deliberately lacks:** caster and a hard-hitting bruiser — the threat is being held still while something else arrives, not being hit hard by the spiders themselves.  
**Regions:** **crypt** (signature).  
**Rigs:** A ×1, C ×4.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `shroud_ling` | 幼殮蛛 | Shroudling | swarmer | Melee | C | variant of `shroud_weaver` | poison | slow 0.15 for 2 s | 0.25 | 0.5 | 6 | 0.6 | Attack1 bite, Attack2 hop | crypt |
| `shroud_weaver` | 織殮蛛 | Shroud-weaver | ranged (control) | Ranged + Snare | C | base | poison | root 1.5 s (web); slow 0.3 for 3 s | 0.8 | 0.8 | 4 | 1 | Attack1 web spit, Attack2 bite | crypt |
| `shroud_dropper` | 懸絲蛛 | Vault-dropper | ambusher | Melee + Dormant(ceiling) | C | variant of `shroud_weaver` | poison | poison 0.3 over 3 s; slow 0.3 for 2 s | 0.7 | 1.3 | 5.2 | 1 | Attack1 drop-stab, Attack2 fang strike | crypt |
| `shroud_matron` | 繭母 | Cocoon-mother | mother (bruiser) | Spawner | C | variant of `shroud_weaver` | poison | poison 0.3 over 4 s; knockback | 3 | 1.4 | 2.8 | 1.7 | Attack1 leg slam, Attack2 lay sac | crypt |
| `shroud_wrapped` | 繭屍 | The Wrapped | melee (puppet) | Melee | A | base | physical | slow 0.25 for 2 s | 1.2 | 1 | 3.4 | 1 | Zombie_Scratch, Melee_Hook (stiff swing); Zombie idle and walk | crypt |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `shroud_ling` | 幼殮蛛 | Spider a hand and a half across with a round pale abdomen wrapped in its own first silk like a tiny bandaged ball, short legs held high. A ball on bent pins. No accent. |
| `shroud_weaver` | 織殮蛛 | Wolf-sized spider with a long narrow abdomen held up and forward over its back like a distaff, spinnerets pointing at the target, and two front legs modified into flat combs that card the silk between them; a half-wound grey shroud trails from the combs. Accent: pale grey-green spinneret tip. |
| `shroud_dropper` | 懸絲蛛 | A small tight body under a cage of very long thin legs that meet the ground in a wide circle, like a hanging candelabrum turned upside down; two fangs as long as daggers point straight down. It descends on a visible thread. Accent: pale green fang tips. |
| `shroud_matron` | 繭母 | Huge spider whose swollen abdomen is plastered with dozens of small silk cocoons — some the size of a hand, some larger — like a nest of bandaged lumps; front legs thick and armoured, a human burial mask stuck over her eye cluster. A great lumpy sphere behind a small masked front. Accent: sickly green glow between the cocoons. |
| `shroud_wrapped` | 繭屍 | A human cocoon standing upright: wound so tightly that its arms are bound across the chest and only the forearms have torn free, the head a featureless oval, the feet a single bandaged block that it shuffles. Four taut silk threads rise from shoulders and head into nothing and twitch — it moves like a marionette. A bowling-pin with strings. No accent; grey silk. |

### 2.14 臥像 `effigy` — Lid-effigies

*The carved likenesses on the barrow-lids, woken by the Order's rites of silence in place of the bones beneath them.*

**What they were, what the dusk made them.** The barrow-lords lie under stone lids carved in their own likeness, flat on their backs with hands joined. The Order's rite of quiet is meant to still restless bones; said over a sarcophagus it does that — and wakes the stone instead. The effigies sit up and step down still shaped for lying: a slab of lid on their backs, hands that cannot unclasp, feet turned out — and the stone hounds carved at their feet get up with them. In the Sanctum the marble saints of the cathedral have followed them. They are slow, almost impossible to stagger and unimpressed by fire. Attacks: stone weight, blade-edges that were never sharpened, bowing prayers that send a line of crushing quiet across the floor. Death: they crack along old mason's joints and fall into rubble blocks — the one family that leaves stone, not ash; the ash sifts out of the cracks afterwards.

**Roles it has:** armoured melee, caster, bruiser (thrower), ambusher (niche statue), pouncing quadruped (dormant).  
**Deliberately lacks:** swarmer and support — stone is never numerous and never helps its neighbour; an effigy pack is three or four heavy pieces (and perhaps one hound) that must be fought in the order they reach you.  
**Regions:** **crypt** (signature), sanctum.  
**Rigs:** A ×4, B ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `effigy_knight` | 臥像騎士 | Lid-knight | melee (armoured) | Melee | A | base | physical | stun 0.4 s (30% chance) | 1.8 | 1.2 | 3 | 1.1 | Sword_Regular_A, Sword_Attack, Shield_OneShot | crypt, sanctum |
| `effigy_orant` | 合掌像 | Orant | caster | Caster | A | variant of `effigy_knight` | shadow | slow 0.4 for 2 s | 1.2 | 1.3 | 2.6 | 1.1 | Spell_Simple_Shoot (a bow from the waist), Spell_Simple_Idle_Loop | crypt, sanctum |
| `effigy_caryatid` | 托柱像 | Pillar-bearer | bruiser (thrower) | Bomber | A | base | physical | knockback; stun 0.6 s | 3.4 | 1.9 | 2.6 | 1.5 | Sword_Attack (overhead smash), OverhandThrow (pillar-drum throw) | crypt, sanctum |
| `effigy_warden` | 壁龕守像 | Niche-warden | ambusher | Melee + Dormant(statue) | A | variant of `effigy_knight` | physical | bleed 0.3 over 3 s; vulnerable 2 s on the waking strike | 1.4 | 1.4 | 3.8 | 1.05 | Sword_Dash (lunge), Sword_Regular_Combo; steps down with LayToIdle timing | crypt, sanctum |
| `effigy_hound` | 枕足犬 | Foot-hound | ambusher (pouncer) | Leaper + Dormant(statue) | B | variant of `dusk_hound` | physical | knockback; slow 0.3 for 2 s on the waking pounce | 0.9 | 1 | 5.4 | 1 | Attack1 bite, Attack2 pounce; Spawn = stands up from the lying pose | crypt, sanctum |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `effigy_knight` | 臥像騎士 | A stone knight who has stood up off his own tomb lid and brought half of it with him: a flat rectangular slab is still fused to his back from heels to above the head like a backboard, with a carved pillow behind the helm. Sword held point-down in both hands, feet splayed; from the side he is perfectly flat-backed. Materials: grey limestone (white marble in the Sanctum); accent: sickly green glow in the inscription along the slab edge. |
| `effigy_orant` | 合掌像 | A robed stone lady whose hands are carved as one block in prayer at the chest and cannot part; she casts by bowing. Tall pointed headdress with a stone veil falling to the hips in stiff vertical pleats, no feet visible — the gown meets the floor as a fluted column. A chess-bishop shape. Accent: green (crypt) or cold white (sanctum) light between the joined palms. |
| `effigy_caryatid` | 托柱像 | A column-statue that walked out from under its vault: a broad stone figure with both arms raised, carrying a broken length of fluted pillar across its shoulders like a yoke-log, a square capital block still sitting on its head as a hat. Rubble dribbles from the break. A thick "T" with a square on top. No glow except a thin line of green or white at the neck joint. |
| `effigy_warden` | 壁龕守像 | A slender guardian statue that stands in wall niches with a halberd upright in both hands — and still has the niche's pointed stone canopy fused above its head like a tall mitre, plus a fragment of plinth on one foot that makes it limp. A tall spike on top and a long halberd. Accent: eye-slots that light green or white only once it is awake. |
| `effigy_hound` | 枕足犬 | The stone hound that was carved lying at the barrow-lord's feet, risen with him: a short-legged, thick-necked dog with a perfectly flat underside (it was part of the lid) and a smooth hollow worn across its back where the lord's heels rested, a carved collar with a ring, ears chipped to stumps. It waits lying down exactly as carved and runs with a stiff rocking-horse gait. A low brick-shaped body with a notch in its back. Accent: green (crypt) or cold white (sanctum) light in the collar ring. |

### 2.15 沼裔 `mire` — Mireborn

*The boardwalk-folk who traded fire for the warmth of rot, and were kept by the peat.*

**What they were, what the dusk made them.** The marsh people could not keep fires lit on water, but they found another warmth: rot gives off heat. The hags taught them to sleep buried in fermenting peat, and for a generation it worked. Then the sleepers stopped getting up the same. 沼裔 are what the peat gave back — still fishing, still netting, still tending their hearth-toads, with bog-water for blood and a patience that is not human. They no longer want fire at all and will drown one wherever they find it. Attacks: barbed fish-spears, weighted nets, bile, things that rise from the mud under the boardwalk. Death: they sag and pour — the body slumps into black water and peat before it ashes, leaving a slick.

**Roles it has:** caster, bruiser, reach melee, ranged controller (net), swarmer (buried ambusher), puller (toad).  
**Deliberately lacks:** healer and reviver — their regeneration is saved for the Mire Mother's fight — and any fast flanker: everything in the mire comes slowly and from below.  
**Regions:** **mire** (signature).  
**Rigs:** A ×5, B ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `mire_hag` | 沼巫 | Mire-hag | caster | Caster | A | existing (planned) | poison | poison 0.4 over 4 s | 0.85 | 1.1 | 3.2 | 1 | Spell_Simple_Shoot | mire |
| `rotspawn` | 腐泥巨怪 | Rotspawn | bruiser | Brute | A | existing (planned) | poison | poison 0.3 over 3 s on the slam | 3.2 | 1.9 | 2.8 | 1.4 | Sword_Attack, Melee_Hook; Zombie idle and walk | mire |
| `bog_fisher` | 叉魚沼民 | Gaff-fisher | melee (reach) | Melee | A | base | physical | bleed 0.3 over 3 s | 1 | 1.1 | 4 | 1 | Sword_Dash (thrust), Sword_Regular_A, Sword_Regular_C | mire |
| `bog_netter` | 撒網沼民 | Net-caster | ranged (control) | Ranged + Snare | A | variant of `bog_fisher` | physical | root 1.8 s | 0.8 | 0.7 | 3.6 | 1 | OverhandThrow (net), Melee_Hook (net weights) | mire |
| `bog_sleeper` | 泥眠者 | Peat-sleeper | swarmer (ambusher) | Melee + Dormant(buried) | A | variant of `bog_fisher` | poison | slow 0.3 for 2 s; poison 0.2 over 3 s | 0.6 | 0.8 | 4.8 | 1 | Zombie_Scratch, Punch_Cross; rises with LayToIdle | mire |
| `bog_toad` | 鼓腹蟾 | Hearth-toad | special (puller) | Melee + Hook | B | variant of `dusk_lizard` | poison | pull; poison 0.3 over 3 s; slow 0.3 for 2 s | 1.6 | 1.2 | 2.6 | 1.2 | Attack1 tongue lash (line), Attack2 belly slam | mire |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `mire_hag` | 沼巫 | Bent old woman in a cloak of reeds and eel-skins under a wide flat basket-hat dripping moss, with a crooked staff hung with fish-bladders and a toad skull, one hand always raised. A flat disc hat over a hunch. Accent: toxic yellow-green bladders. |
| `rotspawn` | 腐泥巨怪 | A mound of peat that grew round a sleeper: no neck, a sloping heap of black mud and roots with a drowned boardwalk plank and a broken eel-trap stuck in its back, arms like wet sacks ending in root-knots; the sleeper's pale face shows low in the chest. A pear shape, heaviest at the bottom. Accent: yellow-green fungus glow on the back. |
| `bog_fisher` | 叉魚沼民 | Lean marsh-man with slick grey-green skin, wearing only a reed kilt and a conical fish-trap as a helmet (a woven funnel that covers the whole head, open point upward); he carries a three-pronged barbed eel-spear taller than himself and a string of dead eels at the belt. Funnel head plus trident. No accent. |
| `bog_netter` | 撒網沼民 | Broad, stooped marsh-woman with a huge weighted cast-net draped over one shoulder and trailing behind like a bridal train studded with stone sinkers; a lidded creel basket on her back, cork floats strung round the neck. A wide draped triangle with a dotted hem. No accent. |
| `bog_sleeper` | 泥眠者 | A sleeper not finished changing: naked, swollen, peat-black to the chest and corpse-pale above, with a long reed breathing-straw still bound to its mouth and sticking straight up past its head, and a slab of peat stuck to its back like a turtle's shell. The arms hang too long. A round-shouldered drip shape with one thin vertical line above it. No accent. |
| `bog_toad` | 鼓腹蟾 | A toad the size of a cart-wheel kept by the marsh-folk for the heat of its fermenting gut: a wide flat body, a translucent belly sack that glows and sloshes, tiny eyes on top, and a woven reed harness with a clay hearth-pot tied on its back. A wide low bun with a lump on top. Accent: yellow-green belly glow. |

### 2.16 偽燈菌 `lamp` — False-lamp

*A fungus that learned to glow the colour of lantern-light, because warm bodies walk toward lamps.*

**What they were, what the dusk made them.** In the fog of the mire there is a lamp ahead of you. There is always a lamp ahead of you. 偽燈菌 is the only light in 瑟蘭 that is not fire — a fungus whose fruiting bodies glow the exact amber of a lantern and hang at the height a person would carry one. It grows in warm bodies. The infected do not fall: they lift the "lamp" that has grown from their arm and walk out into the fog to stand where travellers will see them. It is patient, immobile where it can be, and fights by making the ground itself poisonous. Attacks: spore bursts, lobbed pods, clouds that weaken; almost no direct blows. Death: the glow goes out first — instantly, like a pinched wick — and then the body deflates in a puff of spores and ash.

**Roles it has:** turret (lure), melee, bomber, swarmer (crawler), bruiser (aura).  
**Deliberately lacks:** support and anything fast — an area-denial family: the fight is about where you are still allowed to stand.  
**Regions:** **mire** (signature).  
**Rigs:** A ×3, C ×1, F ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `lamp_lure` | 偽燈柱 | False Lamp-post | turret | Turret + Dormant(statue) | F | base | poison | poison 0.3 over 3 s; weaken 0.2 inside its cloud | 1.5 | 1 | 0 | 1 | Attack1 spore bolt, Attack2 cloud burst | mire |
| `lamp_bearer` | 提菌人 | Lamp-bearer | melee | Melee | A | base | poison | poison 0.25 over 3 s | 1 | 1 | 3.6 | 1 | Sword_Attack (overhead with the lamp arm), Melee_Hook; idle Idle_Lantern_Loop | mire |
| `lamp_bomber` | 擲孢者 | Spore-slinger | ranged (bomber) | Bomber | A | variant of `lamp_bearer` | poison | poison ground 5 s; weaken 0.2 for 3 s | 0.8 | 1 | 3 | 1 | OverhandThrow | mire |
| `lamp_crawler` | 馱菌蟲 | Fungus-back | swarmer (crawler) | Melee + DeathBurst | C | variant of `sexton_beetle` | poison | poison 0.2 over 3 s | 0.4 | 0.6 | 5 | 0.8 | Attack1 nip, Attack2 curl-and-puff | mire |
| `lamp_colony` | 菌冠巨軀 | Crowned Colony | bruiser | Brute + Aura | A | base | poison | poison 0.4 over 4 s; weaken 0.25 (aura) | 3.2 | 1.7 | 2.4 | 1.5 | Sword_Attack (two-arm slam), Melee_Hook | mire |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `lamp_lure` | 偽燈柱 | A fungal stalk exactly the shape of a roadside lantern-post: a leaning pole, a curved hook at the top, and a hanging cap-cluster shaped like a lantern with "panes". Only up close do the gills and the skirt of roots at its base show; it reads as a prop until it moves. Accent: warm lantern amber — deliberately the same colour as real lamps — turning yellow-green when it attacks. |
| `lamp_bearer` | 提菌人 | An infected traveller in a rotted travelling cloak with one arm locked straight up and forward as if holding a lantern high — the forearm has become a stalk and the hand a glowing lantern-shaped cap; the other arm hangs dead, sheathed in shelf fungus. The head tilts under a bracket of mushrooms. A figure with one raised arm ending in a lamp. Accent: lantern amber. |
| `lamp_bomber` | 擲孢者 | Squat infected whose back and shoulders are covered in a cluster of round puffball pods like a bunch of grapes, which it tears off and throws; where the head was, a tall stack of bracket fungus rises in tiers like a pagoda of plates. A stepped spire over a lumpy back. Accent: yellow-green pods, each with an amber pinpoint. |
| `lamp_crawler` | 馱菌蟲 | A flat armoured woodlouse the size of a shield whose plates are overgrown with a row of small upright glowing caps, like a tray of candles on legs; many short legs under a skirt of plates. A low oval with a comb of little lamps. Accent: lantern-amber caps. |
| `lamp_colony` | 菌冠巨軀 | Three infected fused back-to-back under a single enormous table-mushroom cap two metres across, its rim hung with a ring of lantern-fruit on threads; six legs but only two thick arms, each twisted together from several limbs. A walking parasol, or a carousel. Accent: a ring of amber lamps under the cap rim, yellow-green gills. |

### 2.17 泥潛 `lurk` — Weeping Leeches

*The things whose breathing gives the Weeping Mire its name — leeches that drink warmth instead of blood.*

**What they were, what the dusk made them.** The mire weeps: a thin rising whine out in the fog, like a child a long way off. It is breathing. 泣蛭 have a row of gill-flutes down each flank and they whistle as they taste the air for heat. They do not drink blood; a victim is found whole and cold, the warmth drawn out through a ring of small round marks. They live in the mud under the boardwalks and come up beneath whatever is warmest. Attacks: latching bites that chill and slow, miasma spat from a reared-up frill, ambush from below. Death: they knot themselves into a ball and deflate; the ash is wet and clumps.

**Roles it has:** swarmer (leeches), burrowing ambusher, ranged spitter (pools), giant bruiser (inhale).  
**Deliberately lacks:** every humanoid role, caster and support — a pure serpentine ambush family that is always mixed with Mireborn or False-lamp.  
**Regions:** **mire** (signature).  
**Rigs:** D ×4.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `lurk_leech` | 泣蛭 | Weeper | swarmer | Melee + Drain | D | variant of `lurk_eel` | frost | chill 0.2; slow 0.2 for 2 s; heals itself | 0.3 | 0.55 | 5 | 0.6 | Attack1 bite, Attack2 leap-latch | mire |
| `lurk_eel` | 潛泥鱔 | Mud-eel | burrower (ambusher) | Burrower | D | base | physical | bleed 0.3 over 3 s; slow 0.3 for 2 s | 1 | 1.3 | 3 | 1 | Attack1 lunge-bite, Attack2 tail sweep; Spawn = surface | mire |
| `lurk_spitter` | 噴瘴蚺 | Miasma-spitter | ranged (pools) | Bomber | D | variant of `lurk_eel` | poison | poison 0.4 over 4 s; poison ground 4 s | 0.8 | 1.1 | 3.4 | 1.05 | Attack1 spit, Attack2 frill-flare (cloud) | mire |
| `lurk_elder` | 吞暖巨蛭 | Elder Leech | bruiser (inhale) | Brute + Hook + Drain + SplitOnDeath | D | variant of `lurk_eel` | frost | chill 0.5; pull; resource drain | 4 | 1.9 | 2.4 | 1.8 | Attack1 slam, Attack2 inhale-and-bite | mire |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `lurk_leech` | 泣蛭 | Arm-length flattened leech, as broad as a hand at the middle, with a round sucker-mouth at the front and a visible row of small holes (gill-flutes) along each side; it moves in fast vertical loops. A leaf-shaped ribbon. No glow (the belly flushes dull orange after feeding). |
| `lurk_eel` | 潛泥鱔 | Thick-bodied eel with a blunt armoured head like a spade, tiny eyes, a jaw that opens sideways as two plates, and a tattered dorsal fin running the whole back like a row of torn pennants. It surfaces to a third of its length: a rearing column with a ragged crest. No accent. |
| `lurk_spitter` | 噴瘴蚺 | A slimmer serpent that rears upright and spreads a wide frill of feathery gill-fronds round its neck like a ruff or an opened fan; small head with a tube mouth. A fan on a stalk. Accent: toxic yellow-green frond tips that brighten before it spits. |
| `lurk_elder` | 吞暖巨蛭 | A leech as thick as a wine-tun rearing three metres, its front end a circular mouth a metre across ringed with small pale feeler-lobes like hands; the flanks are pierced by rows of flute-holes that visibly open and close, and smaller leeches cling along its sides. A fat tapering pillar with a ring on top. Accent: dull orange warmth glowing through the belly skin. Four weepers drop off when it dies. |

### 2.18 砂葬 `sands` — Sandbound

*The dead of the Brass Dynasty, buried standing to await a sun of their own making, and woken by the storm instead.*

**What they were, what the dusk made them.** The Brass Dynasty did not accept the dusk. They would build a sun: 黃銅日輪, a disc of polished brass a mile wide, to catch the light of the stars and the lightning of the storms and pour it down on the city. They spent the kingdom on it. It never lit. Their dead were buried upright in the sand, wound in linen and ringed in brass, facing the disc, to walk again on the first morning — and the storm-lightning that crawls over the ruins now gives them a cruel imitation of it. The Sandbound fight for a dawn that already failed: blade-dancers, disc-throwers, mirror-guards, the mask-makers who keep them presentable and the brass sentinels that guard them all. Attacks: fast curved blades (bleed), returning discs, reflected projectiles, called lightning. Death: the linen empties — sand pours out of the wrappings, the brass rings ring on the floor, and a violet spark earths itself.

**Roles it has:** fast melee, bruiser (construct), ranged (returning disc), shield-bearer (reflect), caster (lightning), support (empower).  
**Deliberately lacks:** swarmer and exploder — the dynasty's dead are disciplined and few; the numbers in the sands come from brass-hermits and sand-skinned husks.  
**Regions:** **sands** (signature).  
**Rigs:** A ×6.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `sand_revenant` | 砂葬者 | Sand-revenant | melee (fast) | Melee | A | existing (built) | physical | bleed 0.3 over 3 s | 0.85 | 1.1 | 5 | 1 | Sword_Regular_A, Sword_Regular_B, Sword_Regular_C | sands |
| `brass_sentinel` | 黃銅守衛 | Brass Sentinel | bruiser (construct) | Brute | A | existing (planned) | lightning | - | 3 | 1.8 | 3 | 1.3 | Sword_Attack, Sword_Heavy_Combo | sands |
| `dune_disc` | 擲輪衛 | Sun-disc Thrower | ranged | Ranged + Return | A | variant of `sand_revenant` | physical | bleed 0.2 over 3 s | 0.7 | 0.9 | 4.2 | 1 | OverhandThrow | sands |
| `dune_mirror` | 鏡盾衛 | Mirror-guard | shield-bearer (reflect) | Shieldbearer + Reflect | A | variant of `sand_revenant` | physical | stun 0.5 s (shield bash) | 1.5 | 1 | 3.4 | 1.05 | Shield_Dash, Shield_OneShot, Sword_Regular_A; guard pose Sword_Block | sands |
| `dune_stormjar` | 負雷甕者 | Storm-jar Bearer | caster | Caster + Eruption + DeathBurst | A | variant of `sand_revenant` | lightning | stun 0.3 s (20% chance) | 0.8 | 1.2 | 3.2 | 1 | Spell_Simple_Shoot, Spell_Simple_Idle_Loop | sands |
| `dune_masker` | 鑄面匠 | Mask-maker | support (empower) | Support + Empower | A | variant of `sand_revenant` | physical | - | 0.9 | 0.7 | 3.4 | 1 | Interact (fits a mask), Sword_Regular_A (hammer), Spell_Simple_Shoot | sands |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `sand_revenant` | 砂葬者 | Whip-thin desiccated blade-dancer wound in sun-bleached linen with streaming strip ends, a coiled head-wrap with two long tails, brass rings stacked on neck, arm and ankles, a curved ringed brass sword. Accent: faint violet eyes in the slit of the head-wrap. |
| `brass_sentinel` | 黃銅守衛 | A hollow brass guardian shaped like the tomb-statue of a soldier: a barrel torso of riveted plates with a round glass port in the chest where the storm-jar sits, a smooth domed head with a single vertical slot, tubular limbs with ball joints, and a long-hafted bronze fan-axe. Dome plus barrel, very symmetrical. Accent: violet-white arc light in the chest port and the head slot. |
| `dune_disc` | 擲輪衛 | Linen-wound guard with a stack of five razor-edged brass discs carried on the left forearm like bangles the size of plates, one more spinning on the right index finger, and a tall fan-shaped brass back-plate — a small sun-disc on a pole — rising behind the head. A head framed by a semicircle. Accent: violet glint on the disc edges. |
| `dune_mirror` | 鏡盾衛 | Heavier guard in a scale skirt and brass face-mask carrying a perfectly round, convex mirror-shield as tall as his torso — a segment of the Brass Sun — and a short stabbing spear. The shield is the brightest polished surface in the region and reflects the hero's own light. A big perfect circle in front of a figure. Accent: the mirror flashes violet-white when it reflects. |
| `dune_stormjar` | 負雷甕者 | Linen-wound priest bent forward under a sealed brass jar strapped upright on his back, taller than his head and crowned with three splayed copper rods like a trident antenna; brass wires run from the jar down both arms to thimbled fingers. A stooped body plus a tall jar with a fork on top. Accent: violet-white sparks crawling on the rods; the jar glows before it discharges. |
| `dune_masker` | 鑄面匠 | Squat artisan-priest with a leather apron over his wrappings and a pole strapped upright to his back on which six blank brass face-masks are threaded one above another like beads, rising well over his head and clattering as he walks; a small hammer in one hand and a glowing mask held in tongs in the other. His own face is a mask with no eye-holes. A low figure under a vertical string of ovals. Accent: the violet-hot mask in the tongs. |

### 2.19 寄銅蟲 `rust` — Brass-hermits

*Desert crawlers that eat verdigris and live inside the dynasty's hollow brass — helmets, gears and storm-jars that scuttle.*

**What they were, what the dusk made them.** Brass does not rot, so the dunes are full of it: helmets, masks, cog-wheels, jars. Something moved in. 寄銅蟲 are soft-bodied crawlers that graze on verdigris and wear whatever hollow brass fits them, trading up as they grow the way a shore-crab trades shells. A helmet that walks. A gear that rolls at you. A storm-jar that has learned to spit its lightning. They conduct every strike of the storm and are livelier for it. Attacks: pinches, a corroding sting that strips armour (vulnerable), rolling charges, arc discharges. Death: the brass drops with a clang and rolls away; a small pale thing shrivels out of it and ashes. The shell stays as a prop.

**Roles it has:** swarmer (helm), bruiser (stinger), charger (gear), ranged (jar), flyer (locust).  
**Deliberately lacks:** caster and support — vermin again, but armoured: the puzzle is the shell, which blocks from the front and from above.  
**Regions:** **sands** (signature).  
**Rigs:** C ×4, E ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `rust_helm` | 寄盔蟲 | Helm-hermit | swarmer | Melee + FrontArmour | C | base | physical | - | 0.45 | 0.6 | 5.2 | 0.7 | Attack1 pinch, Attack2 hop-clang | sands |
| `rust_stinger` | 鏽尾蠍 | Rust-tail | bruiser | Brute | C | base | poison | vulnerable 3 s; poison 0.3 over 3 s | 2.2 | 1.6 | 3.6 | 1.4 | Attack1 claw sweep (cone), Attack2 tail sting | sands |
| `rust_gear` | 滾齒蟲 | Cog-roller | charger | Charger | C | variant of `rust_helm` | physical | knockback; bleed 0.2 over 3 s | 0.9 | 1.3 | 3 | 1 | Attack1 nip, Attack2 tuck (Run clip is the tucked pose; code spins the mesh while it rolls) | sands |
| `rust_jar` | 貯雷蟹 | Jar-crab | ranged | Ranged + DeathBurst | C | variant of `rust_helm` | lightning | stun 0.3 s (20% chance); the bolt chains once | 0.8 | 1.1 | 2.8 | 1 | Attack1 arc bolt, Attack2 plant-and-overcharge | sands |
| `rust_locust` | 銅翅蝗 | Brass-wing | flyer | Flyer | E | variant of `dusk_gadfly` | physical | bleed 0.2 over 3 s | 0.35 | 0.7 | 7 | 0.9 | Attack1 swoop-slash, Attack2 wing-buzz (cone, slow) | sands |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `rust_helm` | 寄盔蟲 | A dented brass soldier's helmet with a nose-guard and a tattered horsehair crest, running on six thin legs with two small pincers poking out of the face-opening. A helmet shape and nothing else. No glow; a violet glint during storms. |
| `rust_stinger` | 鏽尾蠍 | Pony-sized scorpion wearing a brass breastplate over its back as a carapace, its tail threaded through brass rings and tipped with a corroded spearhead three hand-spans long, held high over the body; claws like sugar-tongs. A high question-mark tail over a flat body. Accent: verdigris green on the sting. |
| `rust_gear` | 滾齒蟲 | A toothed cog-wheel a metre across standing on edge with a soft many-legged body curled in its hub; it walks awkwardly with the wheel tilted on its back, then tips upright and rolls. A perfect toothed circle. Accent: sparks from the teeth and a violet glow in the hub. |
| `rust_jar` | 貯雷蟹 | A tall storm-jar lying tilted on eight splayed legs, mouth forward like a cannon, with two copper rods sticking out of the neck as feelers and a tangle of wire dragging behind. A tilted amphora on legs. Accent: violet-white glow in the jar's mouth. |
| `rust_locust` | 銅翅蝗 | A locust as long as an arm whose forewings are two stiff leaves of beaten brass (it has glued scavenged foil to them), held out flat like a biplane, with saw-edged hind legs trailing. A cross shape with long trailing legs. Accent: brass glint, and violet static along the wing edges in storms. |

### 2.20 日輪機關 `engine` — Sun-engines

*The machinery of the Brass Sun, still tracking a light that is not there — until someone brings one.*

**What they were, what the dusk made them.** The Brass Sun needed keepers that did not tire: mirror-frames that turned with the stars, carriers that walked the disc's segments up the scaffolds, little winders that kept every spring tight. They are still at it. The engines have no malice and no interest in the living; they have a single instruction — find light, focus it — and for a thousand years there has been none. A hero carrying ember is the brightest thing they have ever tracked. Attacks: focused beams with long telegraphs, mirror-flash stuns, the crushing tread of the carrier. Death: the springs let go — they fold up with a long unwinding rattle, shed plates, and the storm-jar inside bursts in a violet flash. They leave brass scrap, not ash.

**Roles it has:** turret, giant (carrier), support (winder).  
**Deliberately lacks:** melee troops, swarmers and casters — an emplacement family that never appears alone; it turns a Sandbound or brass-hermit fight into a positioning problem.  
**Regions:** **sands** (signature).  
**Rigs:** A ×2, F ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `engine_heliostat` | 聚光鏡座 | Heliostat | turret | Turret | F | base | fire | burn 0.5 over 3 s (beam) | 1.8 | 1.6 | 0 | 1 | Attack1 focus beam (line), Attack2 flash (cone, stun) | sands |
| `engine_carrier` | 馱鏡機 | Mirror-carrier | giant (construct) | Brute | A | base | lightning | stun 0.6 s (mirror flash); knockback | 5.5 | 2.2 | 2.8 | 1.85 | Sword_Attack (stomp-slam), Sword_Heavy_Combo, Shield_Dash (trample) | sands |
| `engine_winder` | 上弦僕 | Key-winder | support | Healer + Aura | A | base | physical | - | 0.6 | 0.5 | 4.6 | 0.7 | Interact (winds an ally), Punch_Jab | sands |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `engine_heliostat` | 聚光鏡座 | A tripod of brass legs taller than a man carrying a tilted concave mirror-dish two metres across in a gimbal ring, with a counterweight arm and a small sighting vane; it turns to face the hero with a slow clicking. A dish on a tripod. Accent: the dish gathers a point of white-violet light that grows as it charges. |
| `engine_carrier` | 馱鏡機 | A towering two-legged brass carrier built like a porter: thick piston legs, a small sunken head-lantern, and on its back an A-frame holding one huge wedge-shaped segment of the Brass Sun, a polished slice that stands above it like a sail or a shark's fin. Its arms are lifting-forks. A fin above a blocky body. Accent: violet-white arcs at the leg joints and in the head-lantern. |
| `engine_winder` | 上弦僕 | A child-sized brass attendant with a round pot body and stick limbs, and instead of a head a large butterfly key that turns constantly; it carries a second, bigger key over its shoulder like a rifle and runs with short quick steps. A pot with a bow-tie head. No glow except a tiny violet pilot-light in the chest. |

### 2.21 蠟封眾 `wax` — The Wax-sealed

*The candle-makers of the cathedral city, sealed in their own wax by the Order and stood in the naves as unlit candles.*

**What they were, what the dusk made them.** The holy city held the dusk off with ten thousand candles, and its people were chandlers. When the Order took it the chandlers were not killed. They were sealed: their own wax poured over mouth and eyes — the vow of quiet, administered — a wick set in the crown of the head, and each one stood in a nave or a street as a candle that must never be lit. Some have stood so long that they have run together. They hold very still until something warm comes close. And the wick is real: fire lights them, and a lit one burns fast, bright and with no thought but reaching you. Attacks: smothering blows, wax that hardens on you (slow, then root), burning runners. Death: unlit ones crack and slump; lit ones burn down to a pool in seconds — the only family whose death leaves a ground hazard by default.

**Roles it has:** melee (dormant, flammable), exploder (lit runner), bruiser (wax pools), support (reseals allies, lobs hot wax).  
**Deliberately lacks:** ranged and swarmer — they were made to stand still; the Order's own troops provide the reach and the speed.  
**Regions:** **sanctum** (signature).  
**Rigs:** A ×4.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `wax_sealed` | 蠟封者 | Wax-sealed | melee | Melee + Dormant(statue) + Flammable | A | base | physical | slow 0.3 for 2 s | 1.3 | 1 | 3 | 1 | Zombie_Scratch, Sword_Attack (two-hand smother); Zombie idle and walk | sanctum |
| `wax_wick` | 燃芯者 | Lit Wick | exploder | Exploder + Trail | A | variant of `wax_sealed` | fire | burn 0.5 over 4 s; burning wax ground 5 s (slow 0.3) | 0.7 | 2.2 | 5.4 | 1 | Zombie_Scratch (burst wind-up); run Sprint_Loop | sanctum |
| `wax_dripper` | 淌蠟巨軀 | Tallow-mound | bruiser | Brute + Trail | A | base | physical | slow 0.5 for 3 s; root 1 s on a second hit | 3.4 | 1.8 | 2.4 | 1.5 | Sword_Attack (double slam), Melee_Hook | sanctum |
| `wax_chandler` | 封蠟匠 | Sealing-chandler | support (caster) | Support + Trail | A | variant of `wax_sealed` | fire | slow 0.4 for 3 s; burn 0.2 over 3 s | 0.85 | 1 | 3.2 | 1 | OverhandThrow (ladle of wax), Spell_Simple_Shoot (seals an ally) | sanctum |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `wax_sealed` | 蠟封者 | A citizen in a long smock whose whole head and shoulders are hidden under a smooth dome of poured wax with a short wick standing from the crown; thick drips hang from elbows and fingertips like stalactites and trail threads to the ground. The dome is narrower than the shoulders — a candle-end, not a bell — and the arms are free. A round-topped column with fringes. Materials: yellowed tallow over linen; no glow unlit — once lit, a real warm flame on the head. |
| `wax_wick` | 燃芯者 | A sealed one already alight: the wax dome has half melted away on one side so that a bare jaw shows, the wick is a tall guttering flame that streams backwards as it sprints with arms flung wide, and wax runs off it in ropes. A running figure with a flame plume. Accent: a strong warm candle-flame — it reads as a friendly light until it reaches you. |
| `wax_dripper` | 淌蠟巨軀 | Five or six sealed chandlers who stood too close and ran together: a slumped cone of wax taller than a door with heads, shoulders and praying hands half-sunk in its surface, a dozen dead wicks standing along the top like a row of spines, and two arms that are whole bodies stretched thin. A melted cone with a comb of wicks. No glow; one wick may be burning as decoration. |
| `wax_chandler` | 封蠟匠 | An Order lay-brother in a scorched leather apron and hood carrying a long-handled ladle and, on a chest-harness in front of him, a small iron cauldron of molten wax over a covered coal — the only fire the Order permits. A bundle of blank wicks hangs from his belt like a horse-tail. A pot-bellied silhouette with a long ladle. Accent: a dull warm glow under the cauldron; white wax. |

### 2.22 啞鐘 `bell` — Mute Bells

*The cathedral's bells with their tongues cut out — and the ringers the Order hung in their place.*

**What they were, what the dusk made them.** A bell is the loudest thing a city owns, so the Order began with the bells: clappers unhooked and carried off, mouths stuffed with wet shroud. That was not quiet enough. The ringers were locked inside — some under the small bells, to walk the streets as warnings; some hung by the feet in the great ones, in place of the tongue. A bell rung with a body does not toll. It makes the opposite of a sound: a ring of dead air that rolls outward and puts things out. Attacks: blind charges, the weight of iron, pulses of silence that weaken and stun. Death: the bell falls and — once, finally — rings. One clear note; allies near it flinch.

**Roles it has:** charger (melee), bruiser (pulse), floating pulse-caster.  
**Deliberately lacks:** ranged, swarmer and healer — three heavy, slow pieces built around stun and weaken, intended as the anvil for the Order's hammer.  
**Regions:** **sanctum** (signature).  
**Rigs:** A ×2, F ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `bell_hood` | 罩鐘人 | Bell-hooded | melee (charger) | Charger | A | base | physical | stun 0.6 s (charge); knockback | 1.4 | 1.2 | 3.4 | 1.05 | Shield_Dash (headlong charge), Melee_Hook, Punch_Cross | sanctum |
| `bell_clapper` | 鐘舌手 | Tongue-bearer | bruiser | Brute + Pulse | A | base | physical | stun 0.8 s; weaken 0.2 for 3 s | 3.2 | 1.9 | 2.8 | 1.45 | Sword_Attack, Sword_Heavy_Combo | sanctum |
| `bell_hanging` | 懸鐘 | Hung Bell | support (pulse) | Melee + Pulse + Extinguish | F | base | shadow | weaken 0.3 for 4 s; stun 0.5 s within 3 m | 2 | 1.3 | 1.6 | 1.2 | Attack1 swing-toll (expanding ring), Attack2 drop-slam | sanctum |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `bell_hood` | 罩鐘人 | A man with a bronze bell locked down over his head and shoulders to the elbows, so that only forearms and legs show; a padlocked iron strap runs between the legs to hold it on, and a frayed bell-rope trails from the crown behind him. He leans forward and runs blind. A bell on legs — unmistakable. No glow; cold white rim-light on the bell's lip. |
| `bell_clapper` | 鐘舌手 | A huge ringer in a padded bell-founder's coat, bald, with his ears sealed by wax plugs the size of fists, carrying over one shoulder the iron clapper of a great bell: a tapering shaft as long as he is tall, ending in a ball like a wrecking weight. A coil of bell-rope round the waist. A figure with a giant drumstick. No glow; pale wax plugs. |
| `bell_hanging` | 懸鐘 | A great bronze bell floating mouth-down a metre off the ground under a broken stub of its own timber headstock, with a pair of bound legs hanging out of its mouth in place of the clapper, swaying. Chains trail from the headstock. Bell, crossbar on top, legs below. Accent: a cold white ring of light that rolls out from the lip when it "tolls". |

### 2.23 燼魔 `fiend` — Ember-fiends

*Sparks of the Sun-Forge that refused to go out, fell into the abyss and learned to be afraid of the cold.*

**What they were, what the dusk made them.** When the Forge died, not all of its fire died with it. The sparks that would not go out fell into the abyss and lay in the slag for centuries, and fire that is afraid of going out becomes something with a will. 燼魔 are not invaders from another place: they are the Forge's own orphans — clinker and basalt crusted round a core of old fire, feeding themselves anything that burns so that they never have to go dark. They hate the Silence more than they hate the hero, which is why a binder can strike a pact with them. Attacks: claws, hurled cinders, slams, dive-bombing, furnace-mouthed charges. Death: the crust cracks open, the fire inside gets out, and for a second it is beautiful; then slag and ash.

**Roles it has:** melee, ranged (cinderling), bruiser, flyer (strafing bomber), quadruped charger, caster-summoner.  
**Deliberately lacks:** support, healer and shield roles — a fiend feeds itself first; nothing in the family protects anything else.  
**Regions:** **abyss** (signature).  
**Rigs:** A ×4, B ×1, E ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `fiend` | 燼魔 | Ember-fiend | melee | Melee | A | existing (built) | fire | burn 0.3 over 3 s | 1.2 | 1.25 | 4.8 | 1 | Melee_Hook, Punch_Cross, Zombie_Scratch | abyss |
| `imp` | 小燼魔 | Cinderling | ranged (swarm) | Ranged | A | variant of `fiend` | fire | - | 0.45 | 0.7 | 5.4 | 0.6 | OverhandThrow | abyss |
| `brute` | 熔岩巨魔 | Magma Brute | bruiser | Brute | A | existing (built) | fire | - | 3.4 | 2 | 3.1 | 1.5 | Sword_Attack, Sword_Heavy_Combo | abyss |
| `fiend_wing` | 燼翼 | Cinder-wing | flyer (bomber) | Flyer + Trail | E | variant of `dusk_bat` | fire | burn 0.3 over 3 s; burning ground 3 s | 0.6 | 0.9 | 6.4 | 1.2 | Attack1 strafe-drop, Attack2 dive-claw | abyss |
| `fiend_maw` | 爐口獸 | Furnace-maw | charger (quadruped) | Charger + Breath | B | variant of `dusk_boar` | fire | burn 0.4 over 3 s; knockback | 2 | 1.7 | 3.8 | 1.3 | Attack1 bite / fire-belch, Attack2 charge | abyss |
| `fiend_caller` | 喚燼者 | Ember-caller | caster (summoner) | Caller + Eruption | A | variant of `fiend` | fire | burn 0.4 over 3 s | 1 | 1.3 | 3.4 | 1.2 | Spell_Simple_Shoot, Spell_Simple_Enter / Spell_Simple_Idle_Loop | abyss |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `fiend` | 燼魔 | Horned demon of charred, cracked rock skin with glowing lava fissures, clawed hands, digitigrade-looking legs and a short barbed tail. Accent: lava orange. |
| `imp` | 小燼魔 | Today this is mon_fiend at 0.6 scale. For its own model (mon_imp): a pot-bellied, big-headed spark-child with stubby legs, horns no more than nubs, oversized hands with one fist always holding a glowing coal, and a tail longer than its body ending in a live ember held up like a wick. A round head plus a long upright tail. Accent: lava-orange coal and tail tip. |
| `brute` | 熔岩巨魔 | Hulking lava troll: enormous shoulders and forearms, a small head sunk between them, slabs of basalt fused to the body, lava cracks, a huge stone maul. Accent: lava orange. |
| `fiend_wing` | 燼翼 | A fiend that is nearly all wing: two broad wings of thin black slag-glass with glowing cracks, a tiny horned body between them, and a long whip tail ending in a heavy cinder that it drops. Wingspan three metres. A wide flat "M" with a weighted tail. Accent: lava-orange cracks, brightest along the leading edges. |
| `fiend_maw` | 爐口獸 | A four-legged slab of basalt whose whole front is a mouth: the head is a furnace door that hinges open sideways to show a firebox throat; no eyes, heavy front legs and small hind legs, and a row of chimney-stub vents down the spine that smoke. A doorstop wedge with a row of pipes. Accent: lava orange in the maw and the vents. |
| `fiend_caller` | 喚燼者 | A tall, starved fiend whose horns have grown up and together into a closed ring above its head with a flame held in the middle — a living brazier; narrow shoulders, arms to the knees, a skirt of hanging slag plates. It carries nothing. A ring on a stalk. Accent: lava-orange flame in the horn-ring, white-hot while it summons. |

### 2.24 爐僕 `forge` — Forge-thralls

*The Sun-Forge's inner work-crew, fused to their tools, still working the shift that never ended.*

**What they were, what the dusk made them.** The giants carried fuel to the Forge; the 爐僕 worked inside it. Over generations in that heat the crews were rebuilt for the job — bellows for lungs, tongs for hands, a hammer where an arm had been — until it was hard to say where worker ended and tool began. The Forge has been cold a thousand years and nobody has rung the end of the shift. They pump air into dead hearths, pour slag that is not there, strike anvils with nothing on them, and when something warm walks in they finally have material to work. Every attack is tool-work: grip, blast, pour, strike. Death: the tool-parts lock, the body inside gives out, and the thrall stands dead on its feet for a moment before it topples; black iron remains, and ash sifts from the joints.

**Roles it has:** controller (bellows), grappling melee, bomber (slag), bruiser (hammer), turret (forge-eye), quadruped swarmer (slag-hound).  
**Deliberately lacks:** caster, support and healer — labour, not sorcery; they are mixed with fiends, who supply the fire they fan.  
**Regions:** **abyss** (signature).  
**Rigs:** A ×4, B ×1, F ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `forge_bellows` | 鼓風僕 | Bellows-thrall | special (control) | Caster + Breath | A | base | fire | knockback; burn 0.2 over 3 s; doubles burning ground it blows across | 1.1 | 0.9 | 3.4 | 1.05 | Spell_Simple_Enter / Spell_Simple_Idle_Loop / Spell_Simple_Exit (pump and blast), Melee_Hook | abyss |
| `forge_tongs` | 鉗役 | Tong-hand | melee (grabber) | Melee + Grab | A | variant of `forge_bellows` | physical | root while held 1.2 s; bleed 0.2 over 3 s | 1.2 | 1.1 | 4 | 1.05 | Melee_Hook (grab), Sword_Dash (lunge), Sword_Regular_A | abyss |
| `forge_pourer` | 傾渣僕 | Slag-pourer | ranged (bomber) | Bomber | A | variant of `forge_bellows` | fire | burn 0.4 over 3 s; burning ground 5 s | 0.9 | 1.1 | 3 | 1.05 | OverhandThrow (dipper fling), PickUp_Table (dip) | abyss |
| `forge_hammer` | 鍛錘役 | Trip-hammer | bruiser | Brute + Pulse | A | base | physical | stun 0.6 s; knockback | 3.4 | 2 | 2.8 | 1.5 | Sword_Attack, Sword_Heavy_Combo | abyss |
| `forge_eye` | 爐眼 | Forge-eye | turret | Turret | F | variant of `engine_heliostat` | fire | burn 0.5 over 3 s (beam) | 2 | 1.5 | 0 | 1 | Attack1 open-and-beam (line), Attack2 ember spray (fan) | abyss |
| `forge_hound` | 鏈渣犬 | Slag-hound | swarmer (quadruped) | Melee | B | variant of `dusk_hound` | fire | burn 0.2 over 3 s | 0.5 | 0.8 | 6 | 1 | Attack1 bite, Attack2 lunge | abyss |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `forge_bellows` | 鼓風僕 | A thrall whose chest has been replaced by a great leather concertina bellows wider than his shoulders, its two wooden handles sticking out sideways where arms should start (his real arms work them from below); the nozzle is a long iron pipe rising in front of the face like a trunk. An accordion torso with a forward pipe. Materials: cracked leather, iron hoops; accent: orange glow at the nozzle on the blast. |
| `forge_tongs` | 鉗役 | Lean thrall in a riveted iron hood with a single round grille, whose right arm from the elbow is a pair of blacksmith's tongs a metre and a half long, jaws open; the left arm is short and wrapped in chain. Leather apron full of burn holes. A body with one enormous scissor-limb. Accent: jaw tips glowing dull orange. |
| `forge_pourer` | 傾渣僕 | Thrall walking bent under a crucible carried on his own shoulders in an iron collar-frame — a bucket of molten slag directly above his head, with a pouring lip — and a long-handled dipper in both hands. A scorched hood protects the neck. A bucket-on-head silhouette with a long ladle. Accent: the lava-orange surface of the slag, dripping. |
| `forge_hammer` | 鍛錘役 | A massive thrall whose right arm and shoulder are an iron drop-hammer assembly: a square block head the size of an anvil on a cranked beam, counterweighted by a ballast drum bolted to the left shoulder; the body lists to one side under it and the left hand only steadies. Lopsided, one side all square iron. Accent: an orange heat-glow on the hammer face after each strike. |
| `forge_eye` | 爐眼 | A round iron furnace door on a short squat chimney-stump bolted to the floor, with a counterweighted lid that swings up like an eyelid; shut it is a black disc, open it is a white-hot hole. Chains hang from the lid arm. A disc on a stump. Accent: lava orange to white-hot when open, nothing at all when shut. |
| `forge_hound` | 鏈渣犬 | A hound-shape assembled from chain and clinker: a ribcage of iron hoops with a coal burning inside it, a head that is just a hinged iron jaw-trap, and a length of chain dragging from the collar it was once tethered by. No skin — you can see through it. An open hooped body with a dragging chain. Accent: the orange coal in the chest. |

### 2.25 淵寂 `hush` — The Pale Hush

*Pieces of Osaros's sleep that leak out of the abyss — white, soundless outlines where a creature should be.*

**What they were, what the dusk made them.** The Lord of Silence sleeps under the dead Forge, and his sleep is not empty. It leaks. 淵寂 are not born and are barely alive: they are holes in the world's noise and warmth that have taken on outlines — pale, smooth, featureless, pierced through. They make no sound at all; near them the hero's own footsteps and fire go quiet (the audio mix ducks, flames shrink). They do not hate. They are what the Order prays to become. They take — heat, sound, the hero's resource; they pull projectiles out of the air; burning ground dies where they pass. Death: no ash. They close, like an eye, to a white line and then to nothing; a moment later the sound comes back in a rush.

**Roles it has:** melee, caster (absorb), bruiser, swarmer (crawler), stealth pouncer (quadruped), burrower (serpent), flyer.  
**Deliberately lacks:** ranged physical, support and summoner — the Hush adds nothing to the world; every member only subtracts. It is deliberately spread over five rigs so that the endgame's signature family is the most varied in body.  
**Regions:** sanctum, **abyss** (signature).  
**Rigs:** A ×3, B ×1, C ×1, D ×1, E ×1.

| id | 中文名 | English | role | AI kind | rig | build | element | status applied | life× | dmg× | speed | scale | attack animations | regions |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `hush_blank` | 無面侍 | Faceless Attendant | melee | Melee + Drain | A | base | shadow | weaken 0.2 for 3 s; resource drain | 1.1 | 1.2 | 4.4 | 1.1 | Sword_Regular_A, Sword_Regular_C, Melee_Hook (open-hand strikes) | sanctum, abyss |
| `hush_mouth` | 噬聲者 | Sound-eater | caster | Caster + Absorb | A | variant of `hush_blank` | shadow | pull; weaken 0.25 for 3 s | 0.9 | 1.2 | 3 | 1.1 | Spell_Simple_Enter / Spell_Simple_Idle_Loop (inhale), Spell_Simple_Shoot (release) | sanctum, abyss |
| `hush_mute` | 默巨 | The Mute | bruiser | Brute + Pulse | A | base | shadow | weaken 0.3 for 4 s; slow 0.4 for 3 s | 3.4 | 1.9 | 2.8 | 1.55 | Sword_Attack, Sword_Heavy_Combo, Melee_Hook | abyss |
| `hush_ribwalker` | 肋行者 | Rib-walker | swarmer (crawler) | Melee | C | variant of `shroud_weaver` | shadow | weaken 0.1 for 2 s (stacks to 3) | 0.4 | 0.7 | 5.6 | 0.8 | Attack1 stab, Attack2 hop | sanctum, abyss |
| `hush_beast` | 寂獸 | Hush-hound | stealth pouncer | Stalker + Drain | B | variant of `dusk_hound` | shadow | vulnerable 3 s on pounce; resource drain | 0.8 | 1.3 | 6.4 | 1.1 | Attack1 swipe, Attack2 pounce | abyss |
| `hush_serpent` | 淵白蚺 | Pale Borer | burrower | Burrower + Extinguish | D | variant of `lurk_eel` | shadow | weaken 0.25 for 3 s; slow 0.3 for 2 s | 1.3 | 1.3 | 3 | 1.2 | Attack1 strike, Attack2 coil-pulse (ring); Spawn = rise | abyss |
| `hush_wing` | 寂翼 | Hush-wing | flyer | Flyer + Drain + Extinguish | E | variant of `dusk_bat` | shadow | weaken 0.2 for 3 s; resource drain | 0.5 | 0.8 | 6 | 1.2 | Attack1 glide-pass, Attack2 hover-pulse | sanctum, abyss |

| id | 中文名 | silhouette brief for the modeller |
|---|---|---|
| `hush_blank` | 無面侍 | A narrow chalk-white figure with no features, clothing or joints — smooth as a wax model — whose head is a long vertical oval pierced by one clean vertical slot that you can see through; fingers twice human length, held together like blades. Its feet do not quite touch the floor. An exclamation mark with a slit in the dot. Accent: void white with a black slot; no glow — it looks lit by a light that is not there. |
| `hush_mouth` | 噬聲者 | A pale figure whose torso is a hoop: from collarbone to hips there is only a wide round hole ringed by a smooth rim, the head sitting on top of the ring and the legs hanging below it; the arms are held out to the sides to keep the ring open. A ring with a head — the level shows through it. Accent: void white; the hole fills with black while it inhales, then flashes white. |
| `hush_mute` | 默巨 | A wide, sloping pale giant with no head at all — the shoulders rise into a smooth dome — and arms so long that the knuckles drag, each ending in a flat mitten-hand the size of a shield; a single round hole pierces the chest clean through. A hunched arch. Accent: void white; a thin black ring spreads over the ground from its slams. |
| `hush_ribwalker` | 肋行者 | A thing like a white ribcage with no spine, walking on the tips of its own ribs — six or eight curved prongs meeting the ground — with nothing inside the cage and no head; the "sternum" is a smooth keel on top. An upturned basket on its tines. Accent: void white. |
| `hush_beast` | 寂獸 | A smooth white quadruped with long front legs and a sloping back, no ears and no tail, and a head that is a blunt wedge with a round hole through it from side to side where the eyes would be. A hyena slope with a perfectly clean outline. Accent: void white; unseen until it pounces, showing only a ripple. |
| `hush_serpent` | 淵白蚺 | A white ribbon-serpent, flat as a strap and as wide as a door plank, that rises from the basalt and stands in a slow corkscrew; no head — the front end simply narrows to a point with a small hole near the tip like the eye of a needle. A spiral ribbon. Accent: void white with a black needle-eye. |
| `hush_wing` | 寂翼 | A flat white kite shape — a diamond three metres across with no body, flexing gently along its diagonals like a ray in water — with a round hole in the centre and two long ribbon tails. It never flaps. A diamond with a hole. Accent: void white. |

## 3. Region spawn tables

Weights are shares of 100 at pack level: the chance that a pack's core is drawn from that family. Inside a family each type has a relative weight by role (swarmers 5, line melee 3–4, ranged and casters about 2, special roles 1–1.5, bruisers 1, mothers and giants below 1); the flat list under each table is the product of the two and is what goes into `RegionDef.Spawns`. Where a regional reskin exists the reskin's id is listed. Each dungeon draws from the two or three families named for it.

| region | 名稱 | signature families | shared families | families | regular types present |
|---|---|---|---|---|---|
| `wastes` | 灰燼荒原 | `husk`, `sexton`, `char` | `bone`, `reaver`, `dusk`, `ash`, `hauler` | 8 | 33 |
| `frost` | 霜脊隘口 | `frost`, `coldfire`, `hauler` | `bone`, `wraith`, `reaver`, `dusk`, `cult` | 8 | 31 |
| `crypt` | 沉眠墓原 | `bone`, `shroud`, `effigy` | `cult`, `wraith`, `reaver`, `dusk`, `husk`, `sexton` | 9 | 43 |
| `mire` | 瘴泣沼澤 | `mire`, `lamp`, `lurk` | `husk`, `bone`, `dusk`, `wraith`, `char` | 8 | 32 |
| `sands` | 鏽砂廢都 | `sands`, `rust`, `engine` | `husk`, `bone`, `cult`, `reaver`, `dusk`, `hauler` | 9 | 36 |
| `sanctum` | 寂滅聖城 | `cult`, `wax`, `bell` | `effigy`, `wraith`, `hush`, `bone`, `dusk` | 8 | 36 |
| `abyss` | 燼淵 | `fiend`, `forge`, `hush` | `cult`, `ash`, `wraith`, `dusk`, `hauler` | 8 | 35 |

### 3.1 灰燼荒原 `wastes` — act 1, level 1

Act 1, so the mix is simple on purpose: a husk rush, things from below (beetles, roots), and the first human enemies. One giant at most per screen.

| family | 中文名 | | weight | types that spawn here |
|---|---|---|---|---|
| `husk` | 飢骸 | signature | 26 | `husk`, `husk_bloated`, `husk_chained`, `husk_huddle`, `husk_wailer` |
| `sexton` | 葬甲 | signature | 14 | `sexton_grub`, `sexton_beetle`, `sexton_digger`, `sexton_matron` |
| `char` | 炭木 | signature | 14 | `char_stump`, `char_walker`, `char_hangtree`, `char_root` |
| `bone` | 骸骨 | shared | 16 | `skeleton`, `skeleton_thrower`, `bone_scrap`, `bone_pallbearer` |
| `reaver` | 盜燼眾 | shared | 12 | `reaver_cutter`, `reaver_hook`, `reaver_firepot`, `reaver_cagebearer`, `reaver_brute`, `reaver_houndmaster` |
| `dusk` | 灰暮野物 | shared | 10 | `dusk_hound`, `dusk_boar`, `dusk_crow`, `dusk_moths` |
| `ash` | 灰塑 | shared | 6 | `ash_cast`, `ash_weeper`, `ash_colossus`, `ash_worm` |
| `hauler` | 馱薪巨人 | shared | 2 | `hauler_yoke`, `hauler_collector` |

Flat list for `RegionDef.Spawns`:

```
("husk", 13f), ("husk_bloated", 2.6f), ("husk_chained", 5.2f), ("husk_huddle", 2.08f), ("husk_wailer", 3.12f),
("sexton_grub", 6.93f), ("sexton_beetle", 4.16f), ("sexton_digger", 2.08f), ("sexton_matron", 0.83f),
("char_stump", 6.8f), ("char_walker", 4.08f), ("char_hangtree", 1.09f), ("char_root", 2.04f), ("skeleton", 5.04f),
("skeleton_thrower", 2.77f), ("bone_scrap", 6.3f), ("bone_pallbearer", 1.89f), ("reaver_cutter", 4.69f),
("reaver_hook", 2.81f), ("reaver_firepot", 1.88f), ("reaver_cagebearer", 0.94f), ("reaver_brute", 0.94f),
("reaver_houndmaster", 0.75f), ("dusk_hound", 5.26f), ("dusk_boar", 1.58f), ("dusk_crow", 2.11f),
("dusk_moths", 1.05f), ("ash_cast", 3.2f), ("ash_weeper", 1.44f), ("ash_colossus", 0.4f), ("ash_worm", 0.96f),
("hauler_yoke", 1f), ("hauler_collector", 1f)
```

| dungeon | 名稱 | families | boss | note |
|---|---|---|---|---|
| `wastes_d1` | 荒棄地窖 | `husk`, `reaver`, `sexton` | `db_wastes_d1` 窖中一家 | A cellar where a household hid and never came out; Ember-takers now use the upper rooms as a cache, and the beetles have the rest. |
| `wastes_d2` | 枯根洞 | `char`, `husk`, `dusk` | `db_wastes_d2` 枯根之心 | Root cave: charwood from the walls, husks and hounds sheltering in it. |
| `wastes_boss` | 掘墓者的墓園 | `bone`, `husk`, `sexton` | `boss_gravedigger` 掘墓者・葛魯姆 | Matches the boss's own adds (husks and skeletons). |

Stronghold 斷燈哨站: `sh_wastes` 斷燈哨長・莫葛. Region boss: `boss_gravedigger` 掘墓者・葛魯姆. World boss: `wb_barrowbearer` 負陵母蟲・葬列. Named rare elites: `rare_wastes_oldhowl` 老嚎, `rare_wastes_nightwatch` 守夜人・朵恩, `rare_wastes_ashbride` 灰燼新娘, `rare_wastes_gallowsfather` 絞林之父.

### 3.2 霜脊隘口 `frost` — act 2, level 8

Drilled infantry in the open, elemental attrition near the dead beacons, raiders on the road. The Order is only a small detachment here (snuffers and one knight).

| family | 中文名 | | weight | types that spawn here |
|---|---|---|---|---|
| `frost` | 霜縛 | signature | 28 | `frostbound`, `rime_stalker`, `rime_horn`, `rime_sentry`, `rime_breaker`, `rime_packbeast` |
| `coldfire` | 凍焰 | signature | 14 | `cf_flicker`, `cf_spirit`, `cf_hulk`, `cf_mite` |
| `hauler` | 馱薪巨人 | signature | 4 | `hauler_yoke`, `hauler_hod`, `hauler_collector` |
| `bone` | 骸骨 | shared | 14 | `frost_skeleton`, `frost_thrower`, `frost_scrap`, `frost_pallbearer` |
| `wraith` | 縛魂 | shared | 10 | `ice_wraith`, `frost_mourner`, `wraith_gasp` |
| `reaver` | 盜燼眾 | shared | 12 | `reaver_cutter`, `reaver_hook`, `reaver_firepot`, `reaver_cagebearer`, `reaver_brute`, `reaver_houndmaster` |
| `dusk` | 灰暮野物 | shared | 12 | `dusk_yak`, `dusk_lynx`, `dusk_bat` |
| `cult` | 寂滅教團 | shared | 6 | `frost_knight`, `frost_snuffer` |

Flat list for `RegionDef.Spawns`:

```
("frostbound", 8.82f), ("rime_stalker", 8.82f), ("rime_horn", 2.65f), ("rime_sentry", 3.31f), ("rime_breaker", 2.2f),
("rime_packbeast", 2.2f), ("cf_flicker", 7f), ("cf_spirit", 2.8f), ("cf_hulk", 1.4f), ("cf_mite", 2.8f),
("hauler_yoke", 1.33f), ("hauler_hod", 1.33f), ("hauler_collector", 1.33f), ("frost_skeleton", 4.41f),
("frost_thrower", 2.43f), ("frost_scrap", 5.51f), ("frost_pallbearer", 1.65f), ("ice_wraith", 2.22f),
("frost_mourner", 3.33f), ("wraith_gasp", 4.44f), ("reaver_cutter", 4.69f), ("reaver_hook", 2.81f),
("reaver_firepot", 1.88f), ("reaver_cagebearer", 0.94f), ("reaver_brute", 0.94f), ("reaver_houndmaster", 0.75f),
("dusk_yak", 2.86f), ("dusk_lynx", 3.43f), ("dusk_bat", 5.71f), ("frost_knight", 2.4f), ("frost_snuffer", 3.6f)
```

| dungeon | 名稱 | families | boss | note |
|---|---|---|---|---|
| `frost_d1` | 冰封哨站 | `frost`, `bone`, `cult` | `db_frost_d1` 百年哨・歐德 | Garrison hall held by the Order; the prisoners relight the braziers. |
| `frost_d2` | 霜牙洞窟 | `frost`, `coldfire`, `dusk` | `db_frost_d2` 霜牙女獵首 | Stalker den: rime-horn stalkers, glass-mites and flickers, hearth-bats on the ceiling. |
| `frost_boss` | 赫爾蒙的冰廳 | `frost`, `coldfire`, `wraith` | `boss_rimelord` 霜骸領主・赫爾蒙 | Matches the boss's Frostbound musters. |

Stronghold 凍骨關: `sh_frost` 凍骨關守將・瓦雷克. Region boss: `boss_rimelord` 霜骸領主・赫爾蒙. World boss: `wb_firstbearer` 首役・負山者. Named rare elites: `rare_frost_hamo` 無主號角・哈莫, `rare_frost_whiteshadow` 白影, `rare_frost_lastrun` 末班馱隊, `rare_frost_thousandyear` 千年凍燭.

### 3.3 沉眠墓原 `crypt` — act 2, level 8

The home of the properly dead. Bone and Order packs are the standard; spiders hold the side halls, effigies the royal barrows, tomb-robbing Ember-takers the entrances.

| family | 中文名 | | weight | types that spawn here |
|---|---|---|---|---|
| `bone` | 骸骨 | signature | 24 | `skeleton`, `skeleton_thrower`, `bone_scrap`, `bone_pallbearer`, `bone_urnbearer`, `bone_amalgam` |
| `shroud` | 殮蛛 | signature | 14 | `shroud_ling`, `shroud_weaver`, `shroud_dropper`, `shroud_matron`, `shroud_wrapped` |
| `effigy` | 臥像 | signature | 10 | `effigy_knight`, `effigy_orant`, `effigy_caryatid`, `effigy_warden`, `effigy_hound` |
| `cult` | 寂滅教團 | shared | 18 | `cultist`, `cult_caster`, `knight`, `cult_snuffer`, `cult_crossbow`, `cult_censer`, `cult_flagellant` |
| `wraith` | 縛魂 | shared | 10 | `wraith`, `wraith_mourner`, `wraith_gasp` |
| `reaver` | 盜燼眾 | shared | 8 | `reaver_cutter`, `reaver_hook`, `reaver_firepot`, `reaver_cagebearer`, `reaver_brute` |
| `dusk` | 灰暮野物 | shared | 6 | `dusk_hound`, `dusk_bat`, `dusk_moths` |
| `husk` | 飢骸 | shared | 5 | `husk`, `husk_bloated`, `husk_chained`, `husk_huddle`, `husk_wailer` |
| `sexton` | 葬甲 | shared | 5 | `sexton_grub`, `sexton_beetle`, `sexton_digger`, `sexton_matron` |

Flat list for `RegionDef.Spawns`:

```
("skeleton", 6.44f), ("skeleton_thrower", 3.54f), ("bone_scrap", 8.05f), ("bone_pallbearer", 2.42f),
("bone_urnbearer", 1.93f), ("bone_amalgam", 1.61f), ("shroud_ling", 5.47f), ("shroud_weaver", 2.41f),
("shroud_dropper", 2.19f), ("shroud_matron", 0.66f), ("shroud_wrapped", 3.28f), ("effigy_knight", 3.12f),
("effigy_orant", 1.88f), ("effigy_caryatid", 0.83f), ("effigy_warden", 2.08f), ("effigy_hound", 2.08f),
("cultist", 4.26f), ("cult_caster", 2.13f), ("knight", 1.07f), ("cult_snuffer", 1.6f), ("cult_crossbow", 2.34f),
("cult_censer", 1.28f), ("cult_flagellant", 5.33f), ("wraith", 2.22f), ("wraith_mourner", 3.33f),
("wraith_gasp", 4.44f), ("reaver_cutter", 3.33f), ("reaver_hook", 2f), ("reaver_firepot", 1.33f),
("reaver_cagebearer", 0.67f), ("reaver_brute", 0.67f), ("dusk_hound", 3.75f), ("dusk_bat", 1.5f),
("dusk_moths", 0.75f), ("husk", 2.5f), ("husk_bloated", 0.5f), ("husk_chained", 1f), ("husk_huddle", 0.4f),
("husk_wailer", 0.6f), ("sexton_grub", 2.48f), ("sexton_beetle", 1.49f), ("sexton_digger", 0.74f),
("sexton_matron", 0.3f)
```

| dungeon | 名稱 | families | boss | note |
|---|---|---|---|---|
| `crypt_d1` | 無名陵寢 | `effigy`, `bone`, `wraith` | `db_crypt_d1` 行棺 | Royal barrow: stone and what the stone was guarding. |
| `crypt_d2` | 骨倉 | `bone`, `shroud`, `sexton` | `db_crypt_d2` 骨倉總管 | Bone store: the Unburnt, the spiders that wrap them and the beetles that clean them. |
| `crypt_boss` | 腐骨聖所 | `cult`, `bone`, `wraith` | `boss_bishop` 腐骨主教・瑪爾薩 | Matches the boss's adds (skeletons, javelineers, acolytes). |

Stronghold 守陵人營地: `sh_crypt` 叛誓守陵人・柯瓦. Region boss: `boss_bishop` 腐骨主教・瑪爾薩. World boss: `wb_barrowbearer` 負陵母蟲・葬列. Named rare elites: `rare_crypt_belak` 陵衛長・貝拉克, `rare_crypt_veilwife` 垂紗婆, `rare_crypt_namelessking` 無名王臥像, `rare_crypt_hundredtears` 百泣結.

### 3.4 瘴泣沼澤 `mire` — act 2, level 8

Everything is slow, poisonous and underneath you. Fast pressure comes only from the mire-skinned husks and the warmth-flies.

| family | 中文名 | | weight | types that spawn here |
|---|---|---|---|---|
| `mire` | 沼裔 | signature | 24 | `mire_hag`, `rotspawn`, `bog_fisher`, `bog_netter`, `bog_sleeper`, `bog_toad` |
| `lamp` | 偽燈菌 | signature | 16 | `lamp_lure`, `lamp_bearer`, `lamp_bomber`, `lamp_crawler`, `lamp_colony` |
| `lurk` | 泥潛 | signature | 12 | `lurk_leech`, `lurk_eel`, `lurk_spitter`, `lurk_elder` |
| `husk` | 飢骸 | shared | 18 | `mire_husk`, `mire_bloated`, `mire_chained`, `mire_wailer` |
| `bone` | 骸骨 | shared | 8 | `mire_skeleton`, `mire_thrower` |
| `dusk` | 灰暮野物 | shared | 10 | `dusk_boar`, `dusk_bat`, `dusk_gadfly`, `dusk_moths` |
| `wraith` | 縛魂 | shared | 6 | `mire_wraith`, `mire_mourner`, `wraith_gasp` |
| `char` | 炭木 | shared | 6 | `mire_stump`, `mire_walker`, `mire_hangtree`, `mire_root` |

Flat list for `RegionDef.Spawns`:

```
("mire_hag", 3.38f), ("rotspawn", 1.69f), ("bog_fisher", 6.76f), ("bog_netter", 3.38f), ("bog_sleeper", 6.76f),
("bog_toad", 2.03f), ("lamp_lure", 1.48f), ("lamp_bearer", 4.92f), ("lamp_bomber", 2.46f), ("lamp_crawler", 6.15f),
("lamp_colony", 0.98f), ("lurk_leech", 6.25f), ("lurk_eel", 2.5f), ("lurk_spitter", 2.5f), ("lurk_elder", 0.75f),
("mire_husk", 9.78f), ("mire_bloated", 1.96f), ("mire_chained", 3.91f), ("mire_wailer", 2.35f),
("mire_skeleton", 5.16f), ("mire_thrower", 2.84f), ("dusk_boar", 2.31f), ("dusk_bat", 3.08f), ("dusk_gadfly", 3.08f),
("dusk_moths", 1.54f), ("mire_wraith", 1.33f), ("mire_mourner", 2f), ("wraith_gasp", 2.67f), ("mire_stump", 2.91f),
("mire_walker", 1.75f), ("mire_hangtree", 0.47f), ("mire_root", 0.87f)
```

| dungeon | 名稱 | families | boss | note |
|---|---|---|---|---|
| `mire_d1` | 溺亡者地窟 | `lurk`, `husk`, `wraith` | `db_mire_d1` 溺窟長鱔 | Flooded cellar: leeches in the water, the drowned on the islands. |
| `mire_d2` | 沼巫巢穴 | `mire`, `lamp` | `db_mire_d2` 泥釜三嫗 | Hag warren lit only by false lamps. |
| `mire_boss` | 沼母之庭 | `mire`, `lurk`, `husk` | `boss_mire_mother` 沼母・艾絲珂 | Matches the boss's brood (mire husks). |

Stronghold 沉木寨: `sh_mire` 沉木寨主・老泥. Region boss: `boss_mire_mother` 沼母・艾絲珂. World boss: `wb_firstbearer` 首役・負山者. Named rare elites: `rare_mire_olddrum` 老鼓腹, `rare_mire_guidelight` 引路燈人, `rare_mire_weepingriver` 泣河老蛭, `rare_mire_dafu` 網婆・妲芙.

### 3.5 鏽砂廢都 `sands` — act 5, level 26

Act 5: disciplined dead, armoured vermin and machine emplacements. Reflect, returning discs and beams punish standing still and blind fire.

| family | 中文名 | | weight | types that spawn here |
|---|---|---|---|---|
| `sands` | 砂葬 | signature | 24 | `sand_revenant`, `brass_sentinel`, `dune_disc`, `dune_mirror`, `dune_stormjar`, `dune_masker` |
| `rust` | 寄銅蟲 | signature | 14 | `rust_helm`, `rust_stinger`, `rust_gear`, `rust_jar`, `rust_locust` |
| `engine` | 日輪機關 | signature | 6 | `engine_heliostat`, `engine_carrier`, `engine_winder` |
| `husk` | 飢骸 | shared | 12 | `sand_husk`, `sand_bloated` |
| `bone` | 骸骨 | shared | 10 | `sand_skeleton`, `sand_thrower` |
| `cult` | 寂滅教團 | shared | 12 | `sand_cultist`, `sand_caster`, `sand_snuffer`, `cult_crossbow`, `cult_assassin` |
| `reaver` | 盜燼眾 | shared | 10 | `reaver_cutter`, `reaver_hook`, `reaver_firepot`, `reaver_cagebearer`, `reaver_brute`, `reaver_houndmaster` |
| `dusk` | 灰暮野物 | shared | 9 | `dusk_hound`, `dusk_lizard`, `dusk_crow`, `dusk_moths` |
| `hauler` | 馱薪巨人 | shared | 3 | `hauler_yoke`, `hauler_hod`, `hauler_collector` |

Flat list for `RegionDef.Spawns`:

```
("sand_revenant", 8.35f), ("brass_sentinel", 2.09f), ("dune_disc", 4.59f), ("dune_mirror", 3.13f),
("dune_stormjar", 3.76f), ("dune_masker", 2.09f), ("rust_helm", 6.09f), ("rust_stinger", 1.22f),
("rust_gear", 1.83f), ("rust_jar", 2.43f), ("rust_locust", 2.43f), ("engine_heliostat", 2f),
("engine_carrier", 1.2f), ("engine_winder", 2.8f), ("sand_husk", 10f), ("sand_bloated", 2f),
("sand_skeleton", 6.45f), ("sand_thrower", 3.55f), ("sand_cultist", 4.4f), ("sand_caster", 2.2f),
("sand_snuffer", 1.65f), ("cult_crossbow", 2.42f), ("cult_assassin", 1.32f), ("reaver_cutter", 3.91f),
("reaver_hook", 2.34f), ("reaver_firepot", 1.56f), ("reaver_cagebearer", 0.78f), ("reaver_brute", 0.78f),
("reaver_houndmaster", 0.62f), ("dusk_hound", 4.5f), ("dusk_lizard", 1.8f), ("dusk_crow", 1.8f),
("dusk_moths", 0.9f), ("hauler_yoke", 1f), ("hauler_hod", 1f), ("hauler_collector", 1f)
```

| dungeon | 名稱 | families | boss | note |
|---|---|---|---|---|
| `sands_d1` | 黃銅工坊 | `engine`, `rust`, `sands` | `db_sands_d1` 工坊總樞 | Workshop: engines, the hermits living in their scrap, sentinels on guard. |
| `sands_d2` | 沙掩墓道 | `sands`, `husk`, `bone` | `db_sands_d2` 墓道劍舞・伊芙塔 | Tomb road: the dynasty's dead and the common dead around them. |
| `sands_boss` | 暴君王座 | `sands`, `engine`, `cult` | `boss_sand_tyrant` 鏽砂暴君・伊薩克 | Matches the boss's adds (revenants, sentinels) plus the Order's envoys. |

Stronghold 斷柱商站: `sh_sands` 商站看守・第七號. Region boss: `boss_sand_tyrant` 鏽砂暴君・伊薩克. World boss: `wb_daylessdisc` 無晝日輪. Named rare elites: `rare_sands_ogan` 正午之盾・歐岡, `rare_sands_hundredshell` 百殼老蠍, `rare_sands_tesa` 雷甕長老・泰薩, `rare_sands_thirteenth` 第十三輪.

### 3.6 寂滅聖城 `sanctum` — act 6, level 33

The Order at full strength: every role present. Wax-sealed line the streets as dormant hazards, bells are the heavy pieces, and the first of the Pale Hush leak up through the crypts.

| family | 中文名 | | weight | types that spawn here |
|---|---|---|---|---|
| `cult` | 寂滅教團 | signature | 34 | `cultist`, `cult_caster`, `knight`, `choir`, `cult_snuffer`, `cult_crossbow`, `cult_censer`, `cult_flagellant`, `cult_assassin` |
| `wax` | 蠟封眾 | signature | 14 | `wax_sealed`, `wax_wick`, `wax_dripper`, `wax_chandler` |
| `bell` | 啞鐘 | signature | 8 | `bell_hood`, `bell_clapper`, `bell_hanging` |
| `effigy` | 臥像 | shared | 10 | `effigy_knight`, `effigy_orant`, `effigy_caryatid`, `effigy_warden`, `effigy_hound` |
| `wraith` | 縛魂 | shared | 8 | `wraith`, `wraith_mourner`, `wraith_gasp` |
| `hush` | 淵寂 | shared | 8 | `hush_blank`, `hush_mouth`, `hush_ribwalker`, `hush_wing` |
| `bone` | 骸骨 | shared | 10 | `skeleton`, `skeleton_thrower`, `bone_scrap`, `bone_pallbearer`, `bone_urnbearer`, `bone_amalgam` |
| `dusk` | 灰暮野物 | shared | 8 | `dusk_crow`, `dusk_moths` |

Flat list for `RegionDef.Spawns`:

```
("cultist", 7.05f), ("cult_caster", 3.52f), ("knight", 1.76f), ("choir", 2.11f), ("cult_snuffer", 2.64f),
("cult_crossbow", 3.88f), ("cult_censer", 2.11f), ("cult_flagellant", 8.81f), ("cult_assassin", 2.11f),
("wax_sealed", 7.67f), ("wax_wick", 1.92f), ("wax_dripper", 1.53f), ("wax_chandler", 2.88f), ("bell_hood", 4.8f),
("bell_clapper", 1.6f), ("bell_hanging", 1.6f), ("effigy_knight", 3.12f), ("effigy_orant", 1.88f),
("effigy_caryatid", 0.83f), ("effigy_warden", 2.08f), ("effigy_hound", 2.08f), ("wraith", 1.78f),
("wraith_mourner", 2.67f), ("wraith_gasp", 3.56f), ("hush_blank", 2.5f), ("hush_mouth", 1.12f),
("hush_ribwalker", 3.12f), ("hush_wing", 1.25f), ("skeleton", 2.68f), ("skeleton_thrower", 1.48f),
("bone_scrap", 3.36f), ("bone_pallbearer", 1.01f), ("bone_urnbearer", 0.81f), ("bone_amalgam", 0.67f),
("dusk_crow", 5.33f), ("dusk_moths", 2.67f)
```

| dungeon | 名稱 | families | boss | note |
|---|---|---|---|---|
| `sanctum_d1` | 靜默修院 | `cult`, `wax` | `db_sanctum_d1` 止息院長・艾妲 | Abbey: the Order and the citizens it sealed. |
| `sanctum_d2` | 鐘樓地庫 | `bell`, `bone`, `effigy` | `db_sanctum_d2` 藏舌人 | Bell-tower vault: bells, marble saints and the old dead beneath them. |
| `sanctum_boss` | 無聲大聖堂 | `cult`, `bell`, `hush` | `boss_grand_hush` 寂滅大導師・賽倫 | Matches the boss's choir and acolytes. |

Stronghold 燭匠街壘: `sh_sanctum` 封街騎士長・葛蘭. Region boss: `boss_grand_hush` 寂滅大導師・賽倫. World boss: `wb_daylessdisc` 無晝日輪. Named rare elites: `rare_sanctum_firstcantor` 首席領唱, `rare_sanctum_everquiet` 大鐘「永寂」, `rare_sanctum_kolen` 不熄者・柯倫, `rare_sanctum_vesna` 審默官・薇絲娜.

### 3.7 燼淵 `abyss` — act 7, level 40

Fire against silence: fiends and thralls burn the ground, the Hush puts it out and drains the hero. The most varied body-plan mix in the game (all six rigs appear).

| family | 中文名 | | weight | types that spawn here |
|---|---|---|---|---|
| `fiend` | 燼魔 | signature | 28 | `fiend`, `imp`, `brute`, `fiend_wing`, `fiend_maw`, `fiend_caller` |
| `forge` | 爐僕 | signature | 18 | `forge_bellows`, `forge_tongs`, `forge_pourer`, `forge_hammer`, `forge_eye`, `forge_hound` |
| `hush` | 淵寂 | signature | 18 | `hush_blank`, `hush_mouth`, `hush_mute`, `hush_ribwalker`, `hush_beast`, `hush_serpent`, `hush_wing` |
| `cult` | 寂滅教團 | shared | 12 | `cultist`, `cult_caster`, `ember_knight`, `choir`, `cult_snuffer`, `cult_censer`, `cult_flagellant`, `cult_assassin` |
| `ash` | 灰塑 | shared | 10 | `ash_cast`, `ash_weeper`, `ash_colossus`, `ash_worm` |
| `wraith` | 縛魂 | shared | 6 | `wraith`, `wraith_gasp` |
| `dusk` | 灰暮野物 | shared | 5 | `dusk_bat` |
| `hauler` | 馱薪巨人 | shared | 3 | `hauler_stoker` |

Flat list for `RegionDef.Spawns`:

```
("fiend", 8.18f), ("imp", 8.18f), ("brute", 2.04f), ("fiend_wing", 4.09f), ("fiend_maw", 3.07f),
("fiend_caller", 2.45f), ("forge_bellows", 2.16f), ("forge_tongs", 4.32f), ("forge_pourer", 2.88f),
("forge_hammer", 1.44f), ("forge_eye", 1.44f), ("forge_hound", 5.76f), ("hush_blank", 4.29f), ("hush_mouth", 1.93f),
("hush_mute", 1.07f), ("hush_ribwalker", 5.36f), ("hush_beast", 1.61f), ("hush_serpent", 1.61f),
("hush_wing", 2.14f), ("cultist", 2.81f), ("cult_caster", 1.4f), ("ember_knight", 0.7f), ("choir", 0.84f),
("cult_snuffer", 1.05f), ("cult_censer", 0.84f), ("cult_flagellant", 3.51f), ("cult_assassin", 0.84f),
("ash_cast", 5.33f), ("ash_weeper", 2.4f), ("ash_colossus", 0.67f), ("ash_worm", 1.6f), ("wraith", 2f),
("wraith_gasp", 4f), ("dusk_bat", 5f), ("hauler_stoker", 3f)
```

| dungeon | 名稱 | families | boss | note |
|---|---|---|---|---|
| `abyss_d1` | 熔爐遺構 | `forge`, `fiend`, `hauler` | `db_abyss_d1` 大風箱 | Forge works: thralls at their posts, fiends in the hearths, a stoker-giant on the floor. |
| `abyss_d2` | 黑曜裂谷 | `fiend`, `hush`, `cult` | `db_abyss_d2` 半燼騎士 | Obsidian rift where the Order meets what it prays to. |
| `abyss_boss` | 日爐之心 | `hush`, `cult`, `fiend` | `boss_osaros` 寂主・奧薩洛斯 | The last approach: the Hush, its priests and the fire that still resists them. |

Stronghold 餘火前哨: `sh_abyss` 餘火吞食者・烏爾格. Region boss: `boss_osaros` 寂主・奧薩洛斯. World boss: `wb_breathofsilence` 寂主之息. Named rare elites: `rare_abyss_foreman` 末班工頭, `rare_abyss_firstborn` 長子火, `rare_abyss_whitetide` 白潮, `rare_abyss_slagferry` 渣河擺渡人.

### 3.8 The rift (深淵裂隙)

A rift floor draws two or three families at random from the whole roster — one "line" family (any with swarmer and melee roles), one "pressure" family (casters, bombers, turrets or burrowers) and, from rift tier 5, one giant or mother as a guest. This is where the single-region families get their reuse.

### 3.9 Regional reskins of the global families

Same model and behaviour as the base type; material swap, element and status change (`MonsterDef.Variant`). The ten marked *existing* are already in `MonstersData.cs`.

| reskin id | base | region | 中文名 | element | status | state | look |
|---|---|---|---|---|---|---|---|
| `frost_skeleton` | `skeleton` | frost | 霜骸兵 | frost | chill 0.2 | new | Rime-crusted bone, an icicle fringe under the cap brim, the sword sheathed in ice; ice-blue eye-lights. |
| `frost_thrower` | `skeleton_thrower` | frost | 霜骸擲矛手 | frost | chill 0.25 | existing | Hood and shawl stiff with frost; the javelins are icicles. |
| `frost_scrap` | `bone_scrap` | frost | 霜雜骨 | frost | chill 0.15 | new | Joints fused with ice so it runs with a stiff skitter; frost-blue bindings. |
| `frost_pallbearer` | `bone_pallbearer` | frost | 冰棺骸 | frost | chill 0.3; knockback | new | The coffin lid is sheathed in a pane of clear ice with a face frozen inside it. |
| `frost_knight` | `knight` | frost | 冰封騎士 | frost | chill 0.5 | existing | Plate glazed in ice, ember seams replaced by ice-blue seams, a cape frozen mid-flap. |
| `frost_snuffer` | `cult_snuffer` | frost | 霜袍捻燈人 | shadow | weaken 0.25 for 4 s; chill 0.2; resource drain | new | Fur-lined grey robe; the snuffer cone is rimed and trails cold fog instead of smoke. |
| `ice_wraith` | `wraith` | frost | 霜魂 | frost | chill 0.3 | existing | Shroud stiffened into icicles, paler blue soul-lights. |
| `frost_mourner` | `wraith_mourner` | frost | 雪泣魂 | frost | chill 0.4; fear 0.8 s (20% chance) | new | The hair-veil is a curtain of hanging frost; fingertips leave rime on the floor. |
| `mire_husk` | `husk` | mire | 沼屍 | poison | poison 0.2 over 3 s | existing | Skin green-black and slick, duckweed and leeches on the shoulders. |
| `mire_bloated` | `husk_bloated` | mire | 脹氣沼屍 | poison | poison 0.5 over 4 s (burst) | existing | Belly translucent with marsh gas, a brighter yellow-green. |
| `mire_chained` | `husk_chained` | mire | 拖錨沼屍 | poison | pull; slow 0.3 for 2 s; poison 0.2 over 3 s | new | Drags a small boat anchor on a mooring rope instead of a tether post. |
| `mire_wailer` | `husk_wailer` | mire | 霧號沼屍 | poison | fear 1 s (wail, 4 m) | new | A fog-horn of bark is tied over its mouth; mud to the chest. Calls mire husks. |
| `mire_skeleton` | `skeleton` | mire | 沼骸兵 | poison | poison 0.2 over 3 s | new | Bog-stained brown bone hung with weed, the sword green with corrosion. |
| `mire_thrower` | `skeleton_thrower` | mire | 沼骸擲矛手 | poison | poison 0.25 over 3 s | existing | Shawl of rotted net; fishing spears instead of javelins. |
| `mire_wraith` | `wraith` | mire | 溺魂 | poison | slow 0.3 for 2 s | new | The shroud hangs wet and heavy and drips; soul-lights the yellow-green of marsh gas. |
| `mire_mourner` | `wraith_mourner` | mire | 溺泣魂 | poison | slow 0.3 for 2 s; fear 0.8 s (20% chance) | new | Weed instead of hair; water runs from the sleeves. |
| `mire_stump` | `char_stump` | mire | 沉木樁 | poison | slow 0.2 for 2 s | new | Waterlogged black bog-oak with bracket fungus on the cut top. Not flammable. |
| `mire_walker` | `char_walker` | mire | 沉木人 | poison | slow 0.3 for 2 s; poison 0.2 over 3 s | new | Bog-oak trunk hung with moss curtains; the split glows fungus green. Not flammable. |
| `mire_hangtree` | `char_hangtree` | mire | 沉木弔枝 | poison | pull; root 1 s; knockback | new | The bough is hung with eel-traps and drowned nets instead of nooses. Not flammable. |
| `mire_root` | `char_root` | mire | 沼根 | poison | root 1.2 s; poison 0.2 over 3 s | new | A slick mangrove root with air-roots fringing it. Not flammable. |
| `sand_husk` | `husk` | sands | 乾屍 | physical | - | existing | Sun-dried, leather-brown, wrapped in scraps of bleached linen. |
| `sand_bloated` | `husk_bloated` | sands | 鼓砂乾屍 | physical | weaken 0.3 for 4 s (bursts into blinding sand) | new | A dried skin stretched tight over a belly full of sand that hisses from the splits. |
| `sand_skeleton` | `skeleton` | sands | 砂骸兵 | physical | bleed 0.2 over 3 s | new | Sand-polished bone, a brass cap and a brass sickle-sword. |
| `sand_thrower` | `skeleton_thrower` | sands | 砂骸擲矛手 | physical | - | existing | Bleached shawl; brass-tipped javelins. |
| `sand_cultist` | `cultist` | sands | 砂語教徒 | lightning | - | new | Sand-coloured robe, a brass mask, the dagger wired to a small storm-jar at the belt. |
| `sand_caster` | `cult_caster` | sands | 砂語祭司 | lightning | - | existing | Ochre robes; the candle-ring on the staff replaced by copper rods. |
| `sand_snuffer` | `cult_snuffer` | sands | 砂語捻燈人 | lightning | weaken 0.25 for 4 s; resource drain | new | Brass snuffer cone that crackles; a dust-veil over the sealed scalp. |
| `ember_knight` | `knight` | abyss | 燼騎士 | fire | burn 0.4 over 3 s | existing | Plate heat-blued and glowing at the seams, the cape burnt to a fringe. |

### 3.10 Cosmetic regional skins (no new id, no gameplay change)

These families are re-dressed per region by material and prop swap only; the id, stats and behaviour stay the
same, and the level's region picks the skin.

| family / type | wastes | frost | crypt | mire | sands | sanctum | abyss |
|---|---|---|---|---|---|---|---|
| `reaver` (all six) | ash-smeared leather, dead-lantern strings | fur mantles, snow goggles, ice-crampons | tomb-robber: dust masks, grave-goods jewellery, sacks | — | bleached head-wraps, brass goggles | — | — |
| `dusk_hound` | ash-grey mane | — | bone-pale, hairless | — | tawny, sand-scoured | — | — |
| `dusk_crow` | charcoal black | — | — | — | dust-brown, brass leg-band | marble-dust grey | — |
| `dusk_bat` | — | white-furred | grey | mossy green-black | — | — | soot black, ember-edged ears |
| `dusk_moths` | ash grey | — | bone white | pale green | sand yellow | candle-wax white | — |
| `hauler` (yoke, hod, gatherer) | charred logs | ice-crusted logs, frozen loads | — | — | loads of brass scrap and broken statues | — | (stoker only) |
| `effigy` (all five) | — | — | grey limestone, green inscription light | — | — | white marble, cold-white light | — |
| `wraith_gasp` | — | storm lantern cased in ice | funeral lantern | fisherman's float-lamp | — | processional candle-lantern | miner's cage lamp |
| `ash` (all four) | grey ash | — | — | — | — | — | black ash with red sparks |

## 4. Named monsters

Rare elites are fixed named spawns in the open region (a low chance per visit, announced on the map when found); they keep their base creature's behaviour, roll two ordinary affixes on top, and add the mechanics listed. Stronghold and side-dungeon bosses replace today's scaled-up regular monsters (`RegionDef.StrongholdBoss`, `DungeonDef.MiniBoss`). "Look" is the dressing that turns the base model into the named one.

### 4.1 Named rare elites (28)

- **`rare_wastes_oldhowl` 老嚎** — Old Howl  
  Base: `dusk_hound` 嗅燼犬. Where: Roams the dead-forest road at the head of a pack (wastes).  
  Look: Cinder-hound at twice the size, grey-muzzled, one ear gone, the mane burnt down to a collar of live cinders; a snapped reaver leash still trails from its neck.  
  Signature mechanics:
  1. Pack-father: howls at 75 / 50 / 25% life — every hound within 30 m gains Enrage for 8 s and four fresh hounds run in from off-screen.
  2. If he dies while four or more hounds live, the survivors flee with no loot; kill the pack first for the full drop.
- **`rare_wastes_nightwatch` 守夜人・朵恩** — Dorn the Night-watch  
  Base: `husk_wailer` 號寒骸. Where: Beside the toppled watch-fire north of 斷燈哨站 (wastes).  
  Look: Coldcaller in a keeper's oilskin coat with the outpost's hand-bell rusted into his fist and a lamplighter's hook still strapped to his back.  
  Signature mechanics:
  1. Still calling the alarm: his wail is a 12 m ring (fear 1.5 s) and every wail pulls three husks out of the ground inside the ring.
  2. Only a stagger or stun during the 1.2 s inhale (visible breath plume) stops a wail.
- **`rare_wastes_ashbride` 灰燼新娘** — The Ash Bride  
  Base: `ash_weeper` 灰泣者. Where: The burnt chapel field (wastes).  
  Look: Ash-weeper with a veil and train of ash that streams behind her; her hands stay clasped at the chest until she casts.  
  Signature mechanics:
  1. Reforms three times instead of once; each pile lasts 3 s and must be struck to stop it.
  2. Every reform leaves an ash-fall zone (8 s, weaken and slow) centred on the pile, so the field fills up if she is allowed to stand.
- **`rare_wastes_gallowsfather` 絞林之父** — Father of the Hanging Wood  
  Base: `char_hangtree` 弔枝木. Where: The gallows grove (wastes).  
  Look: Gallows-tree with a bough twice as long carrying seven nooses, and a crown of ember-peckers perched along it that scatter when he is struck.  
  Signature mechanics:
  1. Casts three noose-lines in a fan: a caught hero is hoisted for 2 s taking damage (freed early by dealing 15% of his life).
  2. When ignited he drops his burning gibbet cages as three delayed fire circles.
- **`rare_frost_hamo` 無主號角・哈莫** — Hamo of the Masterless Horn  
  Base: `rime_horn` 凍號手. Where: The signal ledge above the switchbacks (frost).  
  Look: Hornblower whose horn has grown into a great spiral of ice-sheathed ram's horn; his guidon still shows the garrison colours.  
  Signature mechanics:
  1. His blast cone is 14 m long and builds freeze in two ticks instead of four.
  2. Every blast musters a rank: four Frostbound soldiers rise in a line across the far end of the cone and advance in step.
- **`rare_frost_whiteshadow` 白影** — White Shadow  
  Base: `dusk_lynx` 雪隙貓. Where: Shadows the hero along the cliff path and strikes during blizzard gusts (frost).  
  Look: A larger crevice-cat, pure white with black ear-tips; in play usually only its breath plume and eye-shine are visible.  
  Signature mechanics:
  1. Never fully visible: re-stealths 1.5 s after every pounce and leaves three decoy snow-ripples as it does.
  2. The pounce marks its target (bleed and vulnerable 4 s). Any area damage or burning ground reveals it for 3 s.
- **`rare_frost_lastrun` 末班馱隊** — The Last Supply Run  
  Base: `rime_packbeast` 凍馱獸. Where: Walks the supply road from end to end, forever (frost).  
  Look: Pack-mule loaded three kegs high under a garrison banner, dragging the frozen muleteer who still holds its halter.  
  Signature mechanics:
  1. Does not burst on its first charge: every charge drops one keg that detonates after 2.5 s (frost circle, freeze 1.5 s), up to six on the field.
  2. On death the whole load goes: a 9 m burst with a 3 s telegraph.
- **`rare_frost_thousandyear` 千年凍燭** — The Thousand-year Candle  
  Base: `cf_spirit` 寒焰靈. Where: The dead beacon at the summit (frost).  
  Look: A cold-flame spirit grown tall and straight like a great candle, with "wax" runs of ice down its sides and six frozen flickers circling it.  
  Signature mechanics:
  1. Six flickers orbit it as a shield: each absorbs one hit and re-freezes after 10 s.
  2. Heat-draw: a 10 m pull aura that also drains resource while the hero is chilled. Breaks into six flickers on death.
- **`rare_crypt_belak` 陵衛長・貝拉克** — Warden-captain Belak  
  Base: `bone_pallbearer` 抬棺骸. Where: The processional avenue (crypt).  
  Look: Pallbearer whose shield is a carved stone lid with an effigy in relief; a crest of ribs stands along his helmet.  
  Signature mechanics:
  1. Shield wall: spawns with three pallbearers who lock lids edge to edge with his and advance as one front that blocks all frontal damage and projectiles.
  2. The wall opens for 4 s whenever any member is staggered or struck from behind.
- **`rare_crypt_veilwife` 垂紗婆** — Old Veil-wife  
  Base: `shroud_dropper` 懸絲蛛. Where: Vaulted halls hung with cocoons (crypt).  
  Look: Vault-dropper trailing a bridal veil of web from her hind legs; the markings on her abdomen read as a sleeping face.  
  Signature mechanics:
  1. Drops, strikes and re-ascends every 6 s; each drop leaves a web patch (root 1 s on entry, lasts 10 s).
  2. Every third drop cocoons the target (root 2.5 s, broken by damage to the cocoon); she bites cocooned targets for triple damage.
- **`rare_crypt_namelessking` 無名王臥像** — Effigy of the Nameless King  
  Base: `effigy_knight` 臥像騎士. Where: The unmarked royal barrow (crypt).  
  Look: Lid-knight with a carved crown and a face that has been chiselled off; the slab on his back is the whole royal lid, twice the usual width.  
  Signature mechanics:
  1. The slab makes him immune to hits from behind — the reverse of every other enemy — and he cannot be staggered.
  2. Lid-slam: he falls flat forward, sending three crushing lines down the hall, and is vulnerable for 2 s while he gets up.
- **`rare_crypt_hundredtears` 百泣結** — The Knot of a Hundred Tears  
  Base: `wraith_mourner` 泣魂. Where: The mass grave of the unburnt (crypt).  
  Look: Three veiled heads on one column of shroud, with a dozen long arms.  
  Signature mechanics:
  1. Fear aura, 5 m: fear 1 s every 4 s unless the hero stands in firelight or on burning ground.
  2. Splits into three mourners at 50%, and each of those releases two lantern-ghosts when it dies.
- **`rare_mire_olddrum` 老鼓腹** — Old Drumbelly  
  Base: `bog_toad` 鼓腹蟾. Where: The sunken hearth-hall (mire).  
  Look: One-eyed hearth-toad carrying three hearth-pots on its harness, the belly scarred with old burn rings.  
  Signature mechanics:
  1. Swallows on a successful tongue-pull: the hero is inside for 2 s (poison, cannot act) and is then spat 8 m.
  2. Fire ignites the belly gas: the toad bursts for 20% of its own life and is stunned 3 s — the intended counter.
- **`rare_mire_guidelight` 引路燈人** — The Guide-light  
  Base: `lamp_bearer` 提菌人. Where: Appears ahead of the hero in thick fog on the boardwalks (mire).  
  Look: Lamp-bearer wearing a real traveller's hood and pack; from a distance it cannot be told from a friendly lantern-carrier.  
  Signature mechanics:
  1. Neutral until attacked or until it arrives: it walks ahead through the fog, leading the hero past three clusters of dormant False Lamp-posts that wake as it passes.
  2. At the end of the walk it turns hostile with a spore aura. Killing it early skips the ambushes but halves its loot.
- **`rare_mire_weepingriver` 泣河老蛭** — The River that Weeps  
  Base: `lurk_elder` 吞暖巨蛭. Where: The deep channel under the long boardwalk (mire).  
  Look: Bone-pale elder leech; only the ring of its mouth and a line of flute-holes break the surface.  
  Signature mechanics:
  1. Stays submerged except to feed: the inhale reaches 14 m and drags the hero along the boardwalk into its strike.
  2. Sheds six weepers per 20% life lost; each weeper that feeds on the hero and returns heals it 3%.
- **`rare_mire_dafu` 網婆・妲芙** — Net-wife Dafu  
  Base: `bog_netter` 撒網沼民. Where: The drying racks of the drowned village (mire).  
  Look: Net-caster a head taller, one arm massively overgrown, carrying three nets and a necklace of drowned fishermen's boat-tokens.  
  Signature mechanics:
  1. Throws three nets in a fan; a netted hero is hauled 6 m toward the nearest mud patch, where two peat-sleepers rise.
  2. She re-nets the moment a root ends unless she is in melee, where she cannot throw.
- **`rare_sands_ogan` 正午之盾・歐岡** — Ogan, Shield of Noon  
  Base: `dune_mirror` 鏡盾衛. Where: The parade stair facing the fallen disc (sands).  
  Look: Mirror-guard in a gilded mask; his mirror is etched with a noon sun-face.  
  Signature mechanics:
  1. Reflects 100% of projectiles and beams at full damage; after three reflections the mirror overheats and flashes (60° cone, stun 1.5 s).
  2. Open from behind, and for 3 s after a shield-bash that misses.
- **`rare_sands_hundredshell` 百殼老蠍** — The Hundred-shell  
  Base: `rust_stinger` 鏽尾蠍. Where: The armoury dunes (sands).  
  Look: Rust-tail armoured in mismatched brass — helmets, a bell, a gear, breastplates — rusted together; without it, a pale soft body.  
  Signature mechanics:
  1. Moults at 66% and 33%: abandons its brass (which stays as an obstacle that conducts storm strikes), turns fast and takes 40% more damage, and runs for the nearest large brass prop to re-armour.
  2. Destroying the marked props beforehand denies it a new shell.
- **`rare_sands_tesa` 雷甕長老・泰薩** — Storm-elder Tesa  
  Base: `dune_stormjar` 負雷甕者. Where: The lightning-rod court (sands).  
  Look: Storm-jar bearer carrying three jars on a frame like organ pipes, with a long white beard of wire.  
  Signature mechanics:
  1. His called strikes chain to every brass object within 6 m of the impact (props, mirror-shields, sentinels, brass-hermits), so position decides the damage.
  2. Each jar lost (at 66 / 33 / 0%) discharges a nova; the last has a 3 s telegraph.
- **`rare_sands_thirteenth` 第十三輪** — The Thirteenth Disc  
  Base: `dune_disc` 擲輪衛. Where: The roofless observatory (sands).  
  Look: Sun-disc thrower with twelve discs fanned on his back like a peacock's tail and the thirteenth, a huge one, in his hand.  
  Signature mechanics:
  1. His discs do not return: each stays in the arena on a wide orbit round him as a moving blade hazard, up to six.
  2. Staggering him drops every disc at once.
- **`rare_sanctum_firstcantor` 首席領唱** — The First Cantor  
  Base: `choir` 靜默唱詩者. Where: The processional square (sanctum).  
  Look: Chorister with a double halo, a veil twice as tall, and a conductor's rod of bone instead of the bell.  
  Signature mechanics:
  1. Every 8 s she holds a rest: all allies within 14 m gain her barrier at once.
  2. The barriers fail only while the hero stands inside her own halo — a 3 m ring on the ground that drifts away from the hero. Blinks 10 m when struck in melee (6 s cooldown).
- **`rare_sanctum_everquiet` 大鐘「永寂」** — The Great Bell Everquiet  
  Base: `bell_hanging` 懸鐘. Where: The fallen-belfry plaza (sanctum).  
  Look: The largest bell in the city, cracked from lip to shoulder, with two pairs of legs hanging from its mouth and an inscription band that glows white.  
  Signature mechanics:
  1. Tolls three expanding rings in a row with different gaps, and each ring deletes the hero's ground effects and strips buffs from summons.
  2. The hanging legs are a weak point (hits there build stagger); staggered, it falls and rings — stunning its own allies for 3 s.
- **`rare_sanctum_kolen` 不熄者・柯倫** — Kolen the Unsnuffed  
  Base: `wax_wick` 燃芯者. Where: The chandlers' quarter (sanctum).  
  Look: A lit one burning from the head to the waist yet still whole, with his chandler's dipping-rack on his back like two wings of hanging candles.  
  Signature mechanics:
  1. He never detonates: he sprints in long arcs leaving a trail of burning wax (6 s), and every Wax-sealed he brushes past catches fire and becomes a Lit Wick.
  2. Only chill or freeze slows him; freezing him puts the trail out.
- **`rare_sanctum_vesna` 審默官・薇絲娜** — Vesna, Examiner of Quiet  
  Base: `cult_snuffer` 捻燈人. Where: The tribunal steps (sanctum).  
  Look: Snuffer in a white mask painted with a stitched mouth, a gilded snuffer cone, and tribunal scroll-ribbons pinned down her robe.  
  Signature mechanics:
  1. Each snuffer hit strips one buff from the hero and drains 25% resource.
  2. Plants the snuffer cone as a 5 m hush zone (10 s): inside it the hero's resource does not regenerate and fire effects go out, and she fights barehanded and faster while it stands. The cone can be destroyed.
- **`rare_abyss_foreman` 末班工頭** — The Last Foreman  
  Base: `forge_hammer` 鍛錘役. Where: The dead rolling-floor (abyss).  
  Look: Trip-hammer with a whistle-pipe on his helmet, tally-chains across the chest, and shift marks chalked on his ballast drum.  
  Signature mechanics:
  1. Rings the shift-bell on his own ballast drum: every Forge-thrall within 20 m enrages for 10 s, and any thrall that dies while he lives stands back up once at 40%.
  2. His hammer-blows send shock-lines along the floor's rails (telegraphed).
- **`rare_abyss_firstborn` 長子火** — First-born Spark  
  Base: `fiend_caller` 喚燼者. Where: The slag terraces (abyss).  
  Look: Ember-caller whose horns form two concentric rings, holding a white-gold flame.  
  Signature mechanics:
  1. Summons three cinderlings every 8 s (cap 9).
  2. At 50% it calls them all home: each cinderling that reaches it heals 4% and adds a ring to its next eruption volley — kill or freeze them on the way in.
- **`rare_abyss_whitetide` 白潮** — The White Tide  
  Base: `hush_mouth` 噬聲者. Where: The obsidian shelf above the void (abyss).  
  Look: Sound-eater whose hoop is twice as wide, with rib-walkers clinging all round its rim.  
  Signature mechanics:
  1. Its absorb field is 9 m wide and lasts 4 s: every projectile in the room curves into it, and it releases what it ate as a white ring (damage scales with the count, cap ×3).
  2. Rib-walkers pour from the hole while it inhales (two per second). Melee it, or stop shooting.
- **`rare_abyss_slagferry` 渣河擺渡人** — Ferryman of the Slag River  
  Base: `hauler_stoker` 添爐巨人. Where: Wades the lava channel beside the causeway (abyss).  
  Look: Stoker-giant waist-deep in lava, using his shovel as a punt-pole, with a slag-lantern hung on a hook over his shoulder.  
  Signature mechanics:
  1. Stands in the lava where melee cannot reach and flings slag in five-circle volleys.
  2. Every 20 s he wades to the bank, scoops the hero up and throws them onto a marked island ringed by burning ground; he is then ashore and vulnerable for 8 s.

### 4.2 Stronghold bosses (7)

- **`sh_wastes` 斷燈哨長・莫葛** — Mog, Warden of the Broken Lamp  
  Base: `husk_bloated` 腫脹飢骸. Where: 斷燈哨站 (wastes).  
  Look: Bloated husk in a keeper-captain's coat burst open over the belly; the beacon's last ember glows orange through the stretched skin, and the tower keys still hang at his belt.  
  Signature mechanics:
  1. He swallowed the beacon's last ember rather than let it go out. He does not burst on contact: he vents a poison cloud behind him as he walks.
  2. At 66% and 33% he ruptures (8 m poison nova, 1.5 s telegraph) and three husks climb down from the dead beacon tower.
  3. On death the ember is freed and relights the beacon — the stronghold's liberation moment.
- **`sh_frost` 凍骨關守將・瓦雷克** — Varek, Castellan of Frozenbone Gate  
  Base: `knight` 墮落騎士. Where: 凍骨關 (frost).  
  Look: Frost-skinned Fallen Knight in a castellan's fur mantle with an ice-sealed visor, carrying the garrison banner.  
  Signature mechanics:
  1. Plants the garrison banner (destructible: 8% of his life). While it stands, Frostbound within 15 m cannot be staggered or slowed and a fresh rank of four rises every 15 s.
  2. Breaking the banner stops the musters and makes him vulnerable for 6 s; he plants it again after 25 s.
- **`sh_crypt` 叛誓守陵人・柯瓦** — Kova the Oath-broken Warden  
  Base: `knight` 墮落騎士. Where: 守陵人營地 (crypt).  
  Look: Fallen Knight wearing a barrow-warden's tabard with the key emblem scored out, and the wardens' lantern-staff, dark, across his back.  
  Signature mechanics:
  1. The warden who opened the barrows to the Order. Drives his sword into the ground to draw a 7 m desecration circle (12 s): skeletons climb out of the camp's graves inside it.
  2. Every skeleton that dies inside the circle heals him 3% — fight the dead outside the ring, or pull him out of it.
- **`sh_mire` 沉木寨主・老泥** — Old Peat, Master of the Stockade  
  Base: `rotspawn` 腐泥巨怪. Where: 沉木寨 (mire).  
  Look: Rotspawn with the stockade's palisade stakes driven through its back like spines and boardwalk planks plastered over chest and arms.  
  Signature mechanics:
  1. Wears the stockade: tears up planks to plate himself (four plates, each absorbs one heavy hit and falls as debris).
  2. Sinks into the mud and rises under the hero (4.5 m circle), leaving two peat-sleepers behind each time.
- **`sh_sands` 商站看守・第七號** — Custodian Seven  
  Base: `brass_sentinel` 黃銅守衛. Where: 斷柱商站 (sands).  
  Look: Brass sentinel with a merchant's scales welded to one shoulder, a numeral plate on the chest, and a market bell where its left hand should be.  
  Signature mechanics:
  1. Still enforcing closing hour. Arcs tether it to two storm-pylons at the corners of the market: while a pylon stands it has a lightning barrier that returns 30% of melee damage as shock. Destroy the pylons to earth it.
  2. Closing bell: three arena-wide shock rings with safe gaps, repeated faster below 30% life.
- **`sh_sanctum` 封街騎士長・葛蘭** — Gran, Knight-sealer of Chandlers' Row  
  Base: `knight` 墮落騎士. Where: 燭匠街壘 (sanctum).  
  Look: Fallen Knight in wax-spattered white plate, a defaced chandlers' guild banner for a cape, swinging a small cauldron of wax on a chain in his off hand.  
  Signature mechanics:
  1. The barricade's edge is lined with dormant Wax-sealed: his sweeps knock the hero toward them, and any the hero touches wake and grab (root 1 s).
  2. Seal: throws a cauldron of wax; a direct hit encases the hero (rooted until 10% of the hero's life in damage is dealt to the shell). Fire near the edge lights the sealed ones — a risk the player controls.
- **`sh_abyss` 餘火吞食者・烏爾格** — Urg, Eater of the Last Embers  
  Base: `brute` 熔岩巨魔. Where: 餘火前哨 (abyss).  
  Look: Magma Brute with ember-crystal shards grown out of its back and a belly that glows like an open furnace.  
  Signature mechanics:
  1. Six ember-crystals lie around the yard: every 12 s he lumbers to the nearest and eats it (+8% damage, larger, heals 5%, permanent). The hero can shatter crystals first.
  2. If he eats four he overheats: a 10 m eruption, after which he is staggered and vulnerable for 6 s.

### 4.3 Side-dungeon bosses (14)

- **`db_wastes_d1` 窖中一家** — The Household  
  Base: `husk_huddle` 抱暖骸團. Where: wastes_d1 荒棄地窖 (wastes).  
  Look: A larger Huddle of seven under a kitchen tablecloth, one of them still clutching the family's hearth-pot.  
  Signature mechanic: Shedding — every 15% life lost, one body peels off as a husk and the mass shrinks and speeds up (+8% move speed each). At 25% the last three split apart and must all die within 6 s of each other, or they huddle again at 20%.
- **`db_wastes_d2` 枯根之心** — Heart of the Dead Roots  
  Base: `char_root` 蝕根. Where: wastes_d2 枯根洞 (wastes).  
  Look: A hollow stump three metres across with an ember core showing through its splits, and four cinder-roots as thick as a man.  
  Signature mechanic: Burn the roots — the heart-stump cannot be damaged while its four roots are underground. Roots erupt in telegraphed lines and rings; a root set alight (fire damage, or a lit Stumpling lured into it) stays up and burning for 8 s, and the heart takes damage only while at least one root burns.
- **`db_frost_d1` 百年哨・歐德** — Od of the Hundred-year Watch  
  Base: `rime_sentry` 立凍哨. Where: frost_d1 冰封哨站 (frost).  
  Look: Standing Watch whose ice pillar encases him completely, the garrison's watch-bell frozen into it above his head.  
  Signature mechanic: Thaw — his ice shell regrows completely every 10 s unless he stands in the light of one of the hall's four braziers, which the freed prisoners relight as the hero rescues them. Lure him into brazier-light to fight the man instead of the ice.
- **`db_frost_d2` 霜牙女獵首** — The Rimefang Matriarch  
  Base: `rime_stalker` 霜角獵者. Where: frost_d2 霜牙洞窟 (frost).  
  Look: Rimehorn stalker half again as large, with branching antlers of ice and a mantle of stalker pelts.  
  Signature mechanic: Pillar-leaping — she leaps between the cave's ice pillars instead of walking, and every landing shakes icicles loose over a marked area. Shattering the pillar she is perched on drops her, stunned for 3 s — the only time she stays on the ground.
- **`db_crypt_d1` 行棺** — The Walking Sarcophagus  
  Base: `effigy_knight` 臥像騎士. Where: crypt_d1 無名陵寢 (crypt).  
  Look: New rigid model (Rig F): a stone sarcophagus three metres long walking on four carved lion-feet, its lid carved with a lying effigy; a bound occupant inside.  
  Signature mechanic: The lid — closed, it takes 90% less damage and attacks by slam and charge. Every 14 s the lid slides open to release two wraiths, and for 5 s the occupant inside is exposed and takes full damage. Killing its wraiths quickly forces the lid open again sooner.
- **`db_crypt_d2` 骨倉總管** — The Ossuary Steward  
  Base: `bone_amalgam` 疊骨塚衛. Where: crypt_d2 骨倉 (crypt).  
  Look: Barrow Amalgam with four working arms, a shelf-ladder strapped to its back, and a tally-string of small skulls.  
  Signature mechanic: Restocking — at 70 / 40 / 10% it walks to the nearest bone-stack and rebuilds: heals 12% and gains one more swing per combo. The stacks are destructible; a stack destroyed in advance is a heal denied.
- **`db_mire_d1` 溺窟長鱔** — The Long Eel of the Drowned Cellar  
  Base: `lurk_eel` 潛泥鱔. Where: mire_d1 溺亡者地窟 (mire).  
  Look: A pale mud-eel twelve metres long with boardwalk planks stuck in its dorsal fin.  
  Signature mechanic: Rising water — the cellar floods in three stages. The eel travels and strikes only through water; standing on the shrinking dry islands forces it to breach onto land to reach the hero, where it is slow and exposed for 4 s. In water it is untargetable between strikes.
- **`db_mire_d2` 泥釜三嫗** — The Three of the Mud-cauldron  
  Base: `mire_hag` 沼巫. Where: mire_d2 沼巫巢穴 (mire).  
  Look: Three mire-hags told apart by their hats — basket, eel-trap, reed crown — round one cauldron.  
  Signature mechanic: One life, three bodies — the three share one life bar. Only the hag farthest from the hero casts (empowered volleys and rain); the nearest always sinks into the mud and re-emerges on the far side. Keeping two of them close together fixes which one is the caster — and exposes her.
- **`db_sands_d1` 工坊總樞** — The Workshop Hub  
  Base: `engine_heliostat` 聚光鏡座. Where: sands_d1 黃銅工坊 (sands).  
  Look: New static model (Rig F): a fixed brass hub five metres across at the meeting of three conveyor lines, with a heliostat arm on top.  
  Signature mechanic: The assembly line — every 18 s each running line delivers a construct (key-winder, then brass-hermits, then a sentinel). Destroying a line's drive-gear jams it; with all three jammed the hub overloads and opens its core for 8 s. Its heliostat arm sweeps a beam across the floor throughout.
- **`db_sands_d2` 墓道劍舞・伊芙塔** — Iveta, Blade-dancer of the Tomb Road  
  Base: `sand_revenant` 砂葬者. Where: sands_d2 沙掩墓道 (sands).  
  Look: Sand-revenant with twin ringed blades, ankle-bells and a long veil of linen strips.  
  Signature mechanic: Sand doubles — at 75 / 50 / 25% she bursts into a sand-cloud and three dancers step out. The doubles die in one hit but burst into blinding sand (weaken, light radius dimmed 3 s). The real one is the only one whose brass rings make a sound and whose eyes glow.
- **`db_sanctum_d1` 止息院長・艾妲** — Abbess Aida of the Held Breath  
  Base: `choir` 靜默唱詩者. Where: sanctum_d1 靜默修院 (sanctum).  
  Look: Chorister with a triple-arched veil and a bell on a long conductor's staff.  
  Signature mechanic: Measures of rest — every 12 s she raises her bell for a two-second rest (the screen desaturates; a ring closing on the hero is the telegraph): any skill the hero uses during the rest is answered by a heavy shadow strike. Moving is allowed. She takes 50% more damage for 4 s after each rest in which nothing was cast.
- **`db_sanctum_d2` 藏舌人** — The Keeper of Tongues  
  Base: `bell_clapper` 鐘舌手. Where: sanctum_d2 鐘樓地庫 (sanctum).  
  Look: Tongue-bearer with a rack of stolen clappers on his back like organ pipes.  
  Signature mechanic: Planted clappers — he throws clappers that stick upright in the floor (up to five). His slam makes every planted clapper emit a stun ring at once, so each one left standing multiplies the overlap. Clappers can be knocked down, or the hero can stand where the rings miss.
- **`db_abyss_d1` 大風箱** — The Great Bellows  
  Base: `forge_bellows` 鼓風僕. Where: abyss_d1 熔爐遺構 (abyss).  
  Look: New static model (Rig F body, Rig A rider): a bellows the size of a house with its thrall still pumping on top.  
  Signature mechanic: Wind phases — it alternates 5 s of BLOW (pushes the hero toward the slag channels at the arena's edge and fans every burning patch to double size) and 5 s of DRAW (pulls the hero toward the nozzle and its tong-hand guards). The thrall on top is the only damageable part, reachable during DRAW or from the two side gantries.
- **`db_abyss_d2` 半燼騎士** — The Half-burnt Knight  
  Base: `knight` 墮落騎士. Where: abyss_d2 黑曜裂谷 (abyss).  
  Look: Fallen Knight split down the middle: the left half burning along its ember seams, the right half smooth void-white with a slot through the helm.  
  Signature mechanic: Two halves — he changes stance every 15 s (the lit half shows which). FIRE: his swings leave burning ground and he takes full damage. HUSH: he puts out all burning ground, drains resource in an aura, and takes 60% less damage unless the hero stands in firelight — which only his own leftover flames or the hero's fire skills provide.

### 4.4 Region bosses (7)

These exist today with full `BossKit`s and are kept as they are; listed for completeness.

- **`boss_gravedigger` 掘墓者・葛魯姆** — Grum the Gravedigger  
  Base: `boss_gravedigger`. Where: wastes_boss 掘墓者的墓園 (wastes).  
  Look: Existing boss model (scale 1.9).  
  Signature mechanics:
  1. A shovel, a graveyard and everything buried in it: wide shovel sweeps with knockback, grave-hands eruptions (slow), a grave rush, unearthed husks and skeletons.
  2. Phase 2 headstone rain; phase 3 tomb-quake shock rings.
- **`boss_rimelord` 霜骸領主・赫爾蒙** — Helmon the Rime-lord  
  Base: `boss_rimelord`. Where: frost_boss 赫爾蒙的冰廳 (frost).  
  Look: Existing boss model (scale 2.0).  
  Signature mechanics:
  1. Cold steel and a mountain's worth of winter: chilling cleaves, ice-lance fans, hailfall, glacier leap, Frostbound musters.
  2. Phase 2 blizzard (safe only next to him); phase 3 permafrost rings.
- **`boss_bishop` 腐骨主教・瑪爾薩** — Marsa the Bone-rot Bishop  
  Base: `boss_bishop`. Where: crypt_boss 腐骨聖所 (crypt).  
  Look: Existing boss model (scale 1.8).  
  Signature mechanics:
  1. A caster who keeps her distance and lets the dead do the walking: bolt fans, bone prison, raised dead, grave-step blinks, shadow nova.
  2. Phase 2 spiral orbs; phase 3 ossuary rain.
- **`boss_mire_mother` 沼母・艾絲珂** — Esko the Mire-mother  
  Base: `boss_mire_mother`. Where: mire_boss 沼母之庭 (mire).  
  Look: Existing boss model (scale 2.1).  
  Signature mechanics:
  1. She does not chase; the swamp brings you to her: root lashes, mire rain that leaves poison pools, strangleroot lines (root), spore volleys, brood calls, the devouring pull.
  2. Phase 2 homing spore-blooms; phase 3 bog surge.
- **`boss_sand_tyrant` 鏽砂暴君・伊薩克** — Isak the Rust-sand Tyrant  
  Base: `boss_sand_tyrant`. Where: sands_boss 暴君王座 (sands).  
  Look: Existing boss model (scale 2.3).  
  Signature mechanics:
  1. A king of brass who still rules by the glaive: 180° glaive sweeps, the tyrant's charge, storm crosses, shock rings.
  2. Phase 2 musters brass sentinels; phase 3 sandstorm bolts.
- **`boss_grand_hush` 寂滅大導師・賽倫** — Seren, Grand Preceptor of the Hush  
  Base: `boss_grand_hush`. Where: sanctum_boss 無聲大聖堂 (sanctum).  
  Look: Existing boss model (scale 2.1).  
  Signature mechanics:
  1. Bells, silence and the choir that keeps both: bell-toll rings, seeking orbs, silent beams, hush-step blinks.
  2. Phase 2 silence fields and choir calls; phase 3 requiem spiral.
- **`boss_osaros` 寂主・奧薩洛斯** — Osaros, Lord of Silence  
  Base: `boss_osaros`. Where: abyss_boss 日爐之心 (abyss).  
  Look: Existing boss model (scale 2.4).  
  Signature mechanics:
  1. By phase three he does not stop: ashen combos, crossing slashes, void rain, rift strikes.
  2. Phase 2 void spiral, Extinguish (safe only next to him) and fallen knights; phase 3 umbral quake at almost no global cooldown.

### 4.5 World bosses (4)

One per pair of neighbouring regions (the abyss has its own). Each is huge (×4–5), spawns on a timer in a dedicated field and is built on a different rig.

- **`wb_barrowbearer` 負陵母蟲・葬列** — The Barrow-bearer  
  Base: `sexton_matron` 負棺母蟲. Where: The Procession Field on the border road between the Wastes and the Barrows (wastes + crypt).  
  Look: Rig C at about ×4 (new sculpt on the beetle build): a sexton-beetle the size of a house carrying a whole burial chapel — roof, bell-cote and stacked coffins — on its back.  
  Signature mechanics:
  1. Interment — undermines three large marked zones at a time; after 2 s they collapse into pits (heavy damage, root 2 s). The pits stay for the rest of the fight and shrink the arena.
  2. Coffin-fall — shakes her load: six to ten coffins drop on marked circles; each coffin left standing opens after 10 s and releases Unburnt (grub swarms at low life). Coffins can be smashed before they open.
  3. The Procession — she burrows completely and follows the party as a moving grave-mound for four tolls of the chapel bell on her back, erupting under the densest group on the fourth with a ring of thrown earth.
- **`wb_firstbearer` 首役・負山者** — The First Bearer  
  Base: `hauler_hod` 背薪巨人. Where: The ford of the Fuel Road, where timber from the pass met peat from the mire (frost + mire).  
  Look: Rig A at about ×4 (hauler body, new dressing): the oldest Fuel-bearer, carrying an entire toppled beacon-tower in his hod.  
  Signature mechanics:
  1. Stoking throws — tree-trunks rolled down 30 m lanes and peat-bricks lobbed as circles that smoulder for 12 s (burning and slowing ground).
  2. The straps — the hod's two shoulder-straps are separate targets, exposed for 6 s whenever he kneels to pick up a load. Breaking both drops the hod: the beacon-tower falls along a marked line (huge damage to everything, himself included) and he is staggered; he shoulders it again after 40 s.
  3. Gathering — sweeps an open hand through a 180° arc: anyone caught is put in the hod and carried, taking damage, until the cage door is broken (5% of his life) or 6 s pass and he throws them into a marked zone.
- **`wb_daylessdisc` 無晝日輪** — The Dayless Disc  
  Base: `engine_heliostat` 聚光鏡座. Where: The Plain of Noon between the brass city and the cathedral city (sands + sanctum).  
  Look: Unique Rig F model: the hub of the Brass Sun — concentric brass rings twelve metres across turning round a core of storm-jars — hanging in the air.  
  Signature mechanics:
  1. Eclipse sweep — the disc tilts and throws a rotating 90° wedge of shadow across the arena; everywhere outside the shadow is under its focused light (burn every second). The wedge speeds up and reverses.
  2. Storm-jars — four jars orbit the hub and arm one after another (lit numerals); the armed jar must be destroyed before its 10 s charge completes or it discharges chain-lightning across the whole arena. Destroying a jar out of turn discharges it at once.
  3. False dawn — every 60 s it rises and charges for 6 s to "light": anyone not standing behind one of the fallen mirror-segments (each survives two dawns) takes the full-arena flash and is stunned 3 s.
- **`wb_breathofsilence` 寂主之息** — Breath of the Silent Lord  
  Base: `hush_serpent` 淵白蚺. Where: The Basalt Amphitheatre above the void (abyss (and its rifts)).  
  Look: Rig D at about ×5 (new sculpt on the serpent build): a white ribbon-serpent sixty metres long whose coils are part of the arena.  
  Signature mechanics:
  1. The Quiet — silence zones spread from wherever its coils lie: inside them resource does not regenerate and every fire goes out. Four great braziers round the arena can be relit (2 s channel) to burn a zone back for 20 s each.
  2. Coil — it surfaces in a closed ring round the party, its body a wall, and tightens over 10 s. One segment glows as the weak point: breaking it (6% of its life) opens the ring and staggers it; failing means crushing damage and the ring resets.
  3. Swallowed sound — it inhales for 4 s, pulling every projectile, ground effect and summon attack in the arena into itself, then exhales them as a white cone whose size and damage scale with what it ate. Hold fire — or feed it and stand behind it.

### 4.6 Pinnacle ladder bosses (8)

The endgame ladder is 燼階, the Ember Stair: eight rungs fought in order, each an "ember-memory" (燼憶) of a boss as the abyss remembers it. Seven are remixes of the region bosses; rung 7 is new.

- **`pin_1_gravedigger` 不眠掘墓者・葛魯姆** — Grum Unsleeping  
  Base: `boss_gravedigger`. Where: An endless graveyard at night (ladder rung 1).  
  Look: Remix of boss_gravedigger: lantern on the shovel, grave-soil streaming from his coat.  
  Signature mechanics:
  1. Open graves — every grave-hands eruption leaves an open pit for the rest of the fight (falling in roots for 2 s and hurts); a wider shovel sweep knocks the hero toward them.
  2. Burial — at 60% and 30% he buries the hero (untargetable; dig out with eight attacks) while husks rise; headstone-rain stones stay standing as walls that block projectiles.
- **`pin_2_rimelord` 永冬・赫爾蒙** — Helmon of the Long Winter  
  Base: `boss_rimelord`. Where: The summit in a whiteout (ladder rung 2).  
  Look: Remix of boss_rimelord: ice grown into a crown and a cloak of driven snow.  
  Signature mechanics:
  1. Permanent blizzard — a cold meter fills whenever the hero is more than 6 m from one of three braziers; when full, the hero is frozen 3 s. His glacier leap targets the braziers and snuffs them for 15 s.
  2. Whiteout replaces the blizzard ring: the screen fades to white for 6 s and only telegraphs and brazier-light can be seen.
- **`pin_3_bishop` 空棺・瑪爾薩** — Marsa of the Empty Casket  
  Base: `boss_bishop`. Where: The ossuary nave (ladder rung 3).  
  Look: Remix of boss_bishop: she has joined the Unburnt — vestments over bare bone, her own empty casket carried behind her by pallbearers.  
  Signature mechanics:
  1. Three urns — she keeps her life in three urns round the nave and cannot fall below 66 / 33 / 1% until the matching urn is broken; each urn is guarded by an urn-bearer who revives her adds.
  2. Bone prison now closes on the hero's last three positions at once.
- **`pin_4_mire_mother` 滿潮・艾絲珂** — Esko at High Tide  
  Base: `boss_mire_mother`. Where: Her court, flooded (ladder rung 4).  
  Look: Remix of boss_mire_mother: half submerged, crowned with false-lamp fruit.  
  Signature mechanics:
  1. The tide — water rises and falls on a 30 s cycle. At high tide only the root-islands are dry and standing in water stacks poison and slow; at low tide her roots are exposed and take double damage.
  2. The devouring pull now runs along the water's surface from any range, and her brood are elder leeches.
- **`pin_5_sand_tyrant` 日蝕王・伊薩克** — Isak, King of the Eclipse  
  Base: `boss_sand_tyrant`. Where: The throne under the dead disc (ladder rung 5).  
  Look: Remix of boss_sand_tyrant: his armour polished to mirror, the dead disc filling the sky behind him.  
  Signature mechanics:
  1. Light and shadow — a wedge of shadow crosses the dais. In light, each glaive strike is followed by a delayed mirror-image of the same swing; in shadow, his storm crosses strike twice as often.
  2. Two mirror-guards flank the throne and reflect projectiles until they are killed; a mask-maker re-forges them at 50%.
- **`pin_6_grand_hush` 終止式・賽倫** — Seren, the Final Cadence  
  Base: `boss_grand_hush`. Where: The silent cathedral, its bells hung with bodies (ladder rung 6).  
  Look: Remix of boss_grand_hush: a staff of four hanging bells, vestments white instead of black.  
  Signature mechanics:
  1. The score — a visible staff of four measures loops: on rest measures any skill the hero uses is answered by a shadow strike, on toll measures his bell-toll rings come in triplets.
  2. Two Hung Bells drift in the nave and extinguish ground effects; the choir he calls shields him, not themselves.
- **`pin_7_firstkeeper` 初代守燈人・伊蕾** — Ire, the First Lamp-keeper  
  Base: NEW (Rig A, hero-quality sculpt). Where: The first beacon, before the dusk (ladder rung 7).  
  Look: New humanoid duelist: a keeper in plain travelling leathers with a lantern-staff in one hand and a short sword in the other, lit warm from within — the only boss in the game who glows like the hero.  
  Signature mechanics:
  1. Mirror match — the first person who ever carried ember, as the fire remembers her: she uses three skills taken from the hero's own class kit, at boss scale.
  2. The brazier — one brazier burns in the centre; whoever stands in its light regenerates and deals 30% more damage, and she fights for it. At 40% she snuffs it herself, and the last phase is fought in the dark by the light of skills alone.
- **`pin_8_osaros` 醒寂・奧薩洛斯** — Osaros Awake  
  Base: `boss_osaros`. Where: The heart of the dead Forge, lightless (ladder rung 8).  
  Look: Remix of boss_osaros: no longer dreaming — the pale outline seen through cracks in his armour now covers half of him.  
  Signature mechanics:
  1. The dark — the arena is black except for a ring of light round the hero that shrinks while he lives and grows with every stagger. Extinguish is cast twice a phase and its safe zone moves.
  2. Echoes — in phase 3 he calls one signature move from each of the six rungs below, in order. His hush stance drains resource; the only refills are the embers dropped when his knights die.

## 5. New AI behaviours

Existing kinds (`Melee`, `Ranged`, `Caster`, `Exploder`, `Teleporter`, `Brute`, `Support`, `Leaper`) are unchanged. The twelve new kinds below extend `MonAI`; the traits are data flags on `MonsterDef` that can be combined with any kind. "x" after a number is a multiple of the monster's `BaseDamage`. Every dangerous effect is telegraphed with `Fx.Telegraph*` for at least 0.6 s, damage goes through `Combat.MonsterHit` / `Combat.MonsterArea`, ground effects through `GroundZone.SpawnMonster`.

### 5.1 New AI kinds

#### Charger 衝撞

- **Trigger:** Target 6–14 m away with a clear straight walkable lane (NavGrid.LineWalk) and the charger not rooted. Otherwise it fights as Melee.
- **Telegraph:** Line from the charger along its aim, length ChargeLen (14 m), width 2 x Radius + 0.8 m, 0.8 s; facing is locked when the line appears.
- **Effect:** Runs the lane at ChargeSpeed (14 m/s), ignoring unit separation. First contact with each hostile: 1.5x + Knock(8) + OnHit. Stops at the lane's end or a wall; hitting a wall or obstacle stuns the charger 1.2 s and makes it Vulnerable 2 s (the reward for side-stepping). 0.6 s recovery.
- **Cooldown:** 7 s; first use 2–5 s after aggro.
- **Data fields:** `ChargeSpeed`, `ChargeLen`
- **Used by (5):** `dusk_boar`, `rime_packbeast`, `rust_gear`, `bell_hood`, `fiend_maw`

#### Shieldbearer 持盾

- **Trigger:** Passive guard whenever it is not winding up or recovering. Turn rate is capped at 180°/s so it can be flanked.
- **Telegraph:** None for the guard (the raised shield is the read). Bash: none (short). Shield dash: 6 m lane, 0.6 s.
- **Effect:** Hits arriving within ±60° of its facing deal 15% damage and never flinch it; projectiles from that arc are destroyed. Guard breaks for 2.5 s (Hit_Knockback, +25% damage taken) on any frontal hit that applies stun, freeze or knockback, or after three hits from behind. Attacks: Shield_OneShot bash (Knock 6); after blocking three hits within 4 s it answers with Shield_Dash (stun 0.5 s).
- **Cooldown:** Bash 1.8 s; dash 6 s.
- **Data fields:** `GuardArc`, `GuardDr`
- **Used by (2):** `bone_pallbearer`, `dune_mirror`

#### Burrower 潛地

- **Trigger:** Cycle: burrowed → emerge → surfaced → dive. Emerges when it is within 1.5 m of the target's position. With StationaryEmerge (roots) it picks a point within 2 m of the target and does not move while up.
- **Telegraph:** While burrowed, a moving ground ripple and dust trail show where it is. Emerge: circle r 2.2 m, 0.7 s.
- **Effect:** Burrowed: untargetable, no collision, moves at BurrowSpeed (6 m/s; blocked by walls, not by units). Emerge hit: area 1.3x + OnHit. Surfaced for SurfaceTime (5 s): attacks as Melee with its Attack1 / Attack2, takes full damage. Dives (0.5 s, still hittable) when the time is up or the target is more than 8 m away; stun or freeze delays the dive.
- **Cooldown:** 2.5 s underground before the next emerge.
- **Data fields:** `BurrowSpeed`, `SurfaceTime`, `StationaryEmerge`
- **Used by (5):** `sexton_digger`, `char_root`, `ash_worm`, `lurk_eel`, `hush_serpent`

#### Bomber 投彈

- **Trigger:** Keeps 7–11 m like Ranged. Throws at the target's current position whenever its attack cooldown is ready; low obstacles do not block the lob.
- **Telegraph:** Circle of BombRadius (2.4 m) at the landing point for the whole 0.9 s flight; the projectile is a visible arc.
- **Effect:** Impact: area 1.0x + OnHit, then a GroundZone of the same radius for ZoneDur (3–5 s, per type) ticking 0.15x every 0.5 s with the zone status (burn / poison / slow). At most three live zones per bomber (oldest removed). Melee fallback: a bomber whose anim list includes a melee swing uses a Brute-style telegraphed cone when the target is inside 4 m.
- **Cooldown:** Def.Cooldown (about 3.5 s).
- **Data fields:** `BombRadius`, `ZoneDur`, `ZoneStatus`
- **Used by (7):** `reaver_firepot`, `hauler_hod`, `hauler_stoker`, `effigy_caryatid`, `lamp_bomber`, `lurk_spitter`, `forge_pourer`

#### Healer 療癒

- **Trigger:** An aggro'd ally within 12 m below 70% life (lowest percentage first, never itself; HealFilter can restrict it to a family or to constructs). Moves like Support: 9–12 m from the hero, behind its allies.
- **Telegraph:** A visible tether (smoke / beam) from healer to ally and a ring under the ally for the whole channel.
- **Effect:** Channels 3 s, restoring 6% of the ally's max life per second (half on champions and elites, never on bosses). The channel breaks on stun, freeze, fear, flinch, death of either, or loss of line of sight. It has no ranged attack; cornered inside 3 m it uses its melee anim.
- **Cooldown:** 8 s
- **Data fields:** `HealPct`, `HealFilter`
- **Used by (2):** `cult_censer`, `engine_winder`

#### Reviver 喚起

- **Trigger:** Normal-tier monsters of ReviveFamilies (default: its own family) leave a remains marker (a small prop) for 12 s after the model dissolves. Triggers on a marker within 14 m that has not been revived before. Movement as Healer.
- **Telegraph:** 1.5 s cast with a beam to the remains and a ring round them; broken by stun, freeze, flinch or death.
- **Effect:** The same def stands up (LayToIdle) at 60% life as Normal tier, with no loot, 25% XP, and flagged so it cannot be revived again.
- **Cooldown:** 9 s
- **Data fields:** `ReviveFamilies`
- **Used by (1):** `bone_urnbearer`

#### Spawner 孕母

- **Trigger:** While aggro'd, every SpawnEvery seconds (6) as long as fewer than SpawnCap (6) of its young are alive. Mobile mothers keep 8–10 m from the hero; nests have Speed 0.
- **Telegraph:** 0.8 s Attack2 animation, with a small mark on the ground where each young will appear (within 2.5 m).
- **Effect:** Spawns SpawnCount (2) x SpawnDef as Minion tier (no loot, 20% XP), already aggro'd. On death it releases SpawnOnDeath (3) at once. Cocoon-mother variant: lays an egg-sac prop (life = 20% of hers) that hatches three shroudlings after 4 s unless it is destroyed first.
- **Cooldown:** SpawnEvery (6 s)
- **Data fields:** `SpawnDef`, `SpawnCount`, `SpawnCap`, `SpawnEvery`, `SpawnOnDeath`
- **Used by (2):** `sexton_matron`, `shroud_matron`

#### Caller 喚援

- **Trigger:** Aggro'd, fewer than CallCap (6) called monsters alive, cooldown ready. Moves like Caster (8–10 m); its basic attack is whatever the def has (an Eruption cast, or a melee anim when cornered).
- **Telegraph:** 1.2 s cast (head-back wail / whistle) with a ring on the caller; broken by stun, freeze or flinch.
- **Effect:** CallCount (3) x CallDef arrive one second later: defs that rise from the ground appear 4–8 m from the hero, others run in from off-screen. Called monsters give no loot and 25% XP. With Escort: while any escort lives the call instead gives them Enrage for 8 s, and replacements are called only when all are dead.
- **Cooldown:** 12 s
- **Data fields:** `CallDef`, `CallCount`, `CallCap`
- **Used by (3):** `husk_wailer`, `reaver_houndmaster`, `fiend_caller`

#### Swarm 蟲群

- **Trigger:** Always on: one actor is one cloud. It drifts onto its target and stays overlapping it; Attract can bias target choice (flame-moths prefer burning actors and fire zones).
- **Telegraph:** None — damage is continuous and low; the cloud itself is the read.
- **Effect:** No collision, never flinches, immune to knockback, pull, root and bleed. Radius 1.6 m, shrinking to 0.8 m as life falls. Every 0.5 s each hostile inside takes 0.25x + OnHit, and the hero's light radius is x0.6 while inside. Damage taken: x0.35 from single-target hits and projectiles, x1.0 from area hits and ground zones, x1.5 from damage-over-time ticks.
- **Cooldown:** none
- **Data fields:** `SwarmRadius`, `Attract`
- **Used by (1):** `dusk_moths`

#### Turret 砲座

- **Trigger:** Immobile (Speed 0) and immune to displacement. Fires whenever the hero is inside Def.Range (16 m) with line of sight. Turns at TurnRate (60°/s), so it can be out-run round it.
- **Telegraph:** Attack1: line of Def.Range x 1.6 m for 1.2 s; the aim locks at 0.9 s. Attack2 (per type, when the hero is within 4 m): cone or cloud, 0.8 s.
- **Effect:** Attack1: beam or bolt 1.0x + OnHit along the line. Attack2: per type (flash stun, spore cloud, ember fan). Shutter option (forge-eye): between attacks it is shut and takes 30% damage; it is open for the wind-up and 1.5 s after firing.
- **Cooldown:** Attack1 Def.Cooldown (about 4 s); Attack2 8 s.
- **Data fields:** `TurnRate`, `Shutter`
- **Used by (3):** `lamp_lure`, `engine_heliostat`, `forge_eye`

#### Flyer 飛行

- **Trigger:** Orbits the target at 7–9 m. Swoops when the cooldown is ready, the target is 5–12 m away and NavGrid.LineShot is clear.
- **Telegraph:** Line from the flyer through the target, 11 m x 1.6 m, 0.6 s.
- **Effect:** Moves over low obstacles and through other units (LineShot instead of LineWalk; full walls still block). Hover height 1.6–2.4 m is cosmetic: it is always targetable, but ground zones do not affect it. Swoop: flies the lane at 16 m/s, every hostile on it takes 1.0x + OnHit; it ends 3 m past the target and hangs low for 0.5 s. With Trail it drops three zones along the lane; with Dormant(ceiling) it starts roosting.
- **Cooldown:** 4.5 s
- **Data fields:** `SwoopLen`, `OrbitDist`
- **Used by (6):** `dusk_crow`, `dusk_bat`, `dusk_gadfly`, `rust_locust`, `fiend_wing`, `hush_wing`

#### Stalker 潛獵

- **Trigger:** Stealthed while it is neither attacking nor revealed: model at 12% alpha with a shimmer, ignored by auto-targeting and minions (area damage still hits). Circles toward a point 6–8 m behind the hero. Pounces when it is 5–10 m away and outside the hero's front 120° arc, or after 4 s of stalking regardless.
- **Telegraph:** It becomes visible and a circle (r 2.2 m) appears on the target for 0.6 s.
- **Effect:** Leaper jump; the landing is an area hit of 1.5x plus its opening-strike status. It then fights visibly as Melee for 5 s. Any damage reveals it for 3 s; it re-stealths 8 s after a pounce once it has not been hit for 2 s.
- **Cooldown:** 8 s (re-stealth)
- **Data fields:** `StalkDist`, `RevealTime`
- **Used by (3):** `cult_assassin`, `dusk_lynx`, `hush_beast`

### 5.2 Traits

#### Dormant 伏擊

- **Trigger:** Spawns inactive in one of three modes. buried: invisible apart from a faint mound or bubbles, untargetable. statue: visible as a prop or statue, takes 20% damage, shows no health bar. ceiling: not drawn, a faint thread or shadow on the floor. Wakes when the hero comes within WakeRadius (5.5 m; statue 4.5 m), when its pack is aggro'd, or (statue) when it is damaged.
- **Telegraph:** Circle r 2 m for 0.6 s at its own position (ceiling: at the hero's position) while it plays Spawn or LayToIdle.
- **Effect:** Its first attack follows at once at 1.3x with the listed waking status; after that it behaves as its AI kind.
- **Cooldown:** once
- **Data fields:** `DormantMode`, `WakeRadius`
- **Used by (8):** `dusk_bat`, `rime_sentry`, `shroud_dropper`, `effigy_warden`, `effigy_hound`, `bog_sleeper`, `lamp_lure`, `wax_sealed`

#### Hook 鉤拉

- **Trigger:** Target at 4–9 m with line of sight.
- **Telegraph:** Line to the target, 9 m x 1.0 m, 0.6 s.
- **Effect:** The first hostile on the line takes 0.6x and is pulled to 1.5 m in front of the attacker (St.Pull) with the listed slow or root; the attacker's attack cooldown is cut to 0.3 s so that a blow follows.
- **Cooldown:** 8 s
- **Data fields:** `HookRange`
- **Used by (6):** `husk_chained`, `reaver_hook`, `cf_hulk`, `char_hangtree`, `bog_toad`, `lurk_elder`

#### Grab 擒抱

- **Trigger:** Target inside melee reach.
- **Telegraph:** Cone 70°, reach + 1 m, 0.7 s, in a colour distinct from ordinary swings.
- **Effect:** On a hit the hero is rooted and carried for HoldTime (1.2–2 s), taking 0.3x every 0.5 s. The hold ends early if the grabber takes 8% of its max life or is stunned. Giants finish by throwing the hero (Knock 12; 0.8x on landing in a marked circle).
- **Cooldown:** 10 s
- **Data fields:** `HoldTime`, `GrabThrow`
- **Used by (2):** `hauler_collector`, `forge_tongs`

#### Breath 吹息

- **Trigger:** Target inside the cone length (7 m) with line of sight. On Caster and Ranged kinds it replaces the projectile attack (preferred distance 0.7 x cone length); on other kinds it is a special used at 4–9 m.
- **Telegraph:** Cone 7 m x 50°, 0.8 s; the direction is locked when it appears.
- **Effect:** Channels 2 s in four ticks; each tick 0.35x + OnHit along the cone (chill stacking to a freeze on the fourth tick, or knockback 3 per tick, or weaken — per type). The user stands still while blowing; stun or freeze interrupts it.
- **Cooldown:** 7 s
- **Data fields:** `BreathLen`, `BreathArc`
- **Used by (4):** `rime_horn`, `ash_worm`, `fiend_maw`, `forge_bellows`

#### Eruption 地擊

- **Trigger:** Replaces a caster's projectile: used whenever the attack cooldown is ready and the target is inside Def.Range.
- **Telegraph:** Circle r 2.2 m under the target's current position, 0.9 s.
- **Effect:** Area 1.1x + OnHit at the mark. With ZoneDur set it leaves a zone (ash-fall 6 s: weaken and slow).
- **Cooldown:** Def.Cooldown (2.4–2.8 s)
- **Data fields:** `EruptRadius`, `ZoneDur`
- **Used by (3):** `ash_weeper`, `dune_stormjar`, `fiend_caller`

#### Shell 外殼

- **Trigger:** Always, until broken.
- **Telegraph:** The shell is a visible outer layer with its own bar segment.
- **Effect:** ShellHp = ShellMul x MaxLife (1.0 for the Standing Watch). While it lasts, damage goes to the shell, the monster cannot be displaced or flinched and moves at 80% speed. Fire deals triple damage to ice shells. The shell does not regrow (boss variants excepted). When it breaks: burst FX and a 1 s stagger.
- **Cooldown:** none
- **Data fields:** `ShellMul`
- **Used by (1):** `rime_sentry`

#### Reform 復形

- **Trigger:** Its first death only.
- **Telegraph:** The body falls into a visible ash pile (targetable, 1 life) for 4 s.
- **Effect:** Striking the pile destroys it: normal loot and XP. Left alone, the monster stands up again (LayToIdle) with 50% life, and its second death is final. Loot and XP are granted only on the final death. No pile is left if it dies frozen or standing in a frost zone (the ash sets hard).
- **Cooldown:** once
- **Data fields:** `ReformTime`
- **Used by (1):** `ash_cast`

#### Flammable 易燃

- **Trigger:** It takes fire damage or enters burning ground.
- **Telegraph:** It visibly catches: flame FX, emissive seams, it lights its surroundings.
- **Effect:** Lit for the rest of its life: move speed x1.4, attack speed x1.2, its hits apply the listed burn, and it loses 4% of its max life per second. A lit death leaves a 2 s ember patch. Chill or freeze puts it out.
- **Cooldown:** none
- **Data fields:** `LitBurn`
- **Used by (5):** `char_stump`, `char_walker`, `char_hangtree`, `char_root`, `wax_sealed`

#### Aura 光環

- **Trigger:** Passive while alive.
- **Telegraph:** A ring drawn on the ground at the aura radius (8 m for buffs, 5 m for hostile auras).
- **Effect:** Per type. Cage-bearer: allies +25% move and attack speed; on death it drops a stolen ember (pickup: 25% resource and +15% damage for 10 s). Key-winder: constructs +20% attack speed. Brazier-hulk: chill 0.3 on hostiles within 5 m. Crowned Colony: weaken 0.25 on hostiles within 5 m.
- **Cooldown:** none
- **Data fields:** `AuraRadius`, `AuraEffect`
- **Used by (4):** `reaver_cagebearer`, `cf_hulk`, `lamp_colony`, `engine_winder`

#### SplitOnDeath 分裂

- **Trigger:** Death.
- **Telegraph:** The body visibly comes apart for 0.4 s before the pieces stand.
- **Effect:** Spawns SplitCount x SplitDef round the corpse as Normal tier with half XP and no loot. The Huddle: three husks. Drift-colossus: three ash-casts (these cannot Reform). Elder Leech: four weepers.
- **Cooldown:** once
- **Data fields:** `SplitDef`, `SplitCount`
- **Used by (3):** `husk_huddle`, `ash_colossus`, `lurk_elder`

#### DeathBurst 殞爆

- **Trigger:** Death (not expiry). The pack-mule also bursts at the end of a charge that connected.
- **Telegraph:** Circle of the burst radius for 0.6 s (0.4 s for the small swarmers).
- **Effect:** Area damage and status per type. Lantern-ghost r 1.8 m 0.6x chill; Frozen Flicker r 2 m 0.7x chill; Frozen Pack-mule r 4.5 m 1.6x freeze 1 s; Fungus-back r 2 m poison cloud 3 s; Storm-jar Bearer r 4 m 1.0x lightning; Jar-crab r 3 m 0.8x lightning.
- **Cooldown:** once
- **Data fields:** `BurstRadius`, `BurstDmg`
- **Used by (6):** `wraith_gasp`, `rime_packbeast`, `cf_flicker`, `lamp_crawler`, `dune_stormjar`, `rust_jar`

#### Escort 隨從

- **Trigger:** Spawn.
- **Telegraph:** none
- **Effect:** Spawns with EscortDefs placed within 2 m in the same pack (they count toward the pack's size). Ash-handler: two cinder-hounds; until Rig B exists, two wick-cutters stand in.
- **Cooldown:** once
- **Data fields:** `EscortDefs`
- **Used by (1):** `reaver_houndmaster`

#### Absorb 吞噬

- **Trigger:** The hero has fired a projectile at it within the last 2 s, or the cooldown is ready and the hero is 5–12 m away.
- **Telegraph:** A dark disc r 4 m appears in front of it, 0.6 s.
- **Effect:** Channels 2.5 s: hostile projectiles entering the disc are deleted and counted, and hostile actors inside are pulled (Pull 3 every 0.5 s). It ends with a ring burst (r 6 m, telegraphed 0.6 s) of 0.5x + 0.15x per absorbed projectile, capped at 1.5x.
- **Cooldown:** 10 s
- **Data fields:** `AbsorbRadius`
- **Used by (1):** `hush_mouth`

#### Drain 汲取

- **Trigger:** Any of its hits that lands on the hero.
- **Telegraph:** none (the hit itself)
- **Effect:** Removes DrainPct (10%) of the hero's maximum class resource. Types listed as healing themselves also recover 5% of their max life per hit. No effect on minions. True silencing of skills is deliberately not used anywhere in the regular roster.
- **Cooldown:** per hit
- **Data fields:** `DrainPct`, `DrainHeal`
- **Used by (8):** `cult_snuffer`, `dusk_crow`, `dusk_gadfly`, `lurk_leech`, `lurk_elder`, `hush_blank`, `hush_beast`, `hush_wing`

#### Extinguish 熄滅

- **Trigger:** Any of its attacks or pulses.
- **Telegraph:** none (rides on the attack's own telegraph)
- **Effect:** Within the attack's area: removes fire ground zones (the hero's included), clears burn from its allies, and prevents new fire zones there for 2 s.
- **Cooldown:** per attack
- **Data fields:** none
- **Used by (4):** `cult_snuffer`, `bell_hanging`, `hush_serpent`, `hush_wing`

#### Pulse 脈衝

- **Trigger:** Target within 9 m. On Brute kinds it follows their slam.
- **Telegraph:** The ring's origin is marked for 0.8 s; the ring itself is visible as it travels.
- **Effect:** One or two expanding rings from the monster (8 m/s, 1.5 m wide, out to 9 m). A ring hit deals 0.8x plus the listed stun or weaken; rings are avoided by dodging through them or standing in the gap between two.
- **Cooldown:** 9 s
- **Data fields:** `PulseRings`
- **Used by (4):** `bell_clapper`, `bell_hanging`, `forge_hammer`, `hush_mute`

#### Snare 束縛彈

- **Trigger:** Modifies the kind's projectile.
- **Telegraph:** The projectile is large and slow enough to read: radius 0.8 m, speed 11 m/s.
- **Effect:** On hit: 0.4x and the listed root (the hero can still attack while rooted). On a miss it leaves a net or web patch (r 1.6 m, 5 s, slow 0.4).
- **Cooldown:** Def.Cooldown
- **Data fields:** `SnareRoot`
- **Used by (2):** `shroud_weaver`, `bog_netter`

#### Return 迴旋

- **Trigger:** Modifies the kind's projectile.
- **Telegraph:** The disc is visible both ways; a thin line marks its return path.
- **Effect:** The projectile flies to its maximum range (12 m) and comes back to the thrower along the same line. It can hit once each way; the return hit deals 0.6x.
- **Cooldown:** Def.Cooldown
- **Data fields:** none
- **Used by (1):** `dune_disc`

#### Trail 殘留

- **Trigger:** Its attacks leave ground behind.
- **Telegraph:** The zone itself (standard GroundZone visuals).
- **Effect:** The impact point of its attack (the swoop lane of a Flyer; the burst area of an Exploder) becomes a GroundZone. Cold-flame Spirit: frost patch r 1.8 m 4 s. Tallow-mound: wax pool r 2.5 m 6 s, slow 0.5, a second entry roots 1 s. Lit Wick: burning wax r 3.6 m 5 s. Sealing-chandler: wax pool r 2 m 5 s. Cinder-wing: three fire patches r 1.6 m 3 s.
- **Cooldown:** per attack
- **Data fields:** `TrailZone`
- **Used by (5):** `cf_spirit`, `wax_wick`, `wax_dripper`, `wax_chandler`, `fiend_wing`

#### Empower 加持

- **Trigger:** An ally within 10 m that does not have the buff.
- **Telegraph:** 1.0 s cast (Interact) with a tether to the ally.
- **Effect:** The ally gains a Brass Mask for 12 s: it takes 40% less damage and cannot be flinched, and a masked ally that falls is raised once at 40% life. The mask is visible on the model.
- **Cooldown:** 10 s
- **Data fields:** none
- **Used by (1):** `dune_masker`

#### FrontArmour 前甲

- **Trigger:** Passive.
- **Telegraph:** none (the shell is the read)
- **Effect:** Hits arriving within the front 120° deal 50% damage. No projectile blocking and no bash; hits from the side or behind, ground zones and damage-over-time are unaffected.
- **Cooldown:** none
- **Data fields:** `ArmourArc`
- **Used by (2):** `sexton_beetle`, `rust_helm`

#### Phasing 穿行

- **Trigger:** Passive.
- **Telegraph:** none
- **Effect:** Ignores unit separation: passes through other monsters and the hero and cannot body-block. Walls still stop it.
- **Cooldown:** none
- **Data fields:** none
- **Used by (3):** `wraith_mourner`, `wraith_gasp`, `cf_flicker`

#### Reflect 反射

- **Trigger:** With Shieldbearer only.
- **Telegraph:** The shield flashes when it turns a projectile.
- **Effect:** Projectiles blocked by the guard are sent back at their owner at 50% damage instead of being destroyed.
- **Cooldown:** none
- **Data fields:** `ReflectPct`
- **Used by (1):** `dune_mirror`

## 6. Production plan

The order below reaches full coverage fastest for the work spent: first everything that needs no new sculpt, then the Rig A bases that unlock the most variants, then the variants, then the oversized bodies, and only then the new rigs — cheapest and widest-reaching first. Rig A is complete at the end of stage 4 with 83 of the 129 types, which is enough to ship every region with its signature families; rigs B–F then add the other body plans without changing any spawn table (the types are already listed and simply start appearing).

| stage | what | models built | regular types available | new behaviours first needed |
|---|---|---|---|---|
| 0 | On disk today | 14 (total 14) | 15 | — |
| 1 | Finish the 17, then every variant of an existing base | 22 (total 36) | 36 | Shieldbearer, Reviver, Hook, Caller, Drain, Extinguish, Healer, Stalker, Phasing, Breath, Dormant, Shell, Return, Reflect, Eruption, DeathBurst, Empower |
| 2 | Human-sized Rig A bases | 14 (total 50) | 50 | Trail, Flammable, Reform, Aura, Charger |
| 3 | Variants of the stage-2 bases | 15 (total 65) | 65 | Bomber, Escort, Snare, Grab, Absorb |
| 4 | Rig A bruisers and giants | 18 (total 83) | 83 | SplitOnDeath, Pulse |
| 5 | Rig F (rigid / floating) | 7 (total 90) | 90 | Swarm, Turret |
| 6 | Rig B (quadruped) | 11 (total 101) | 101 | — |
| 7 | Rig C (arthropod) | 15 (total 116) | 116 | FrontArmour, Burrower, Spawner |
| 8 | Rig D (serpentine) | 7 (total 123) | 123 | — |
| 9 | Rig E (flyer) | 6 (total 129) | 129 | Flyer |

**Stage 0 — On disk today.** The 14 models already built. They cover 15 type ids because the cinderling (`imp`) is `mon_fiend` at 0.6 scale.

On disk: `mon_skeleton`, `mon_skeleton_thrower`, `mon_husk`, `mon_husk_bloated`, `mon_cultist`, `mon_cult_caster`, `mon_knight`, `mon_choir`, `mon_wraith`, `mon_frostbound`, `mon_rime_stalker`, `mon_sand_revenant`, `mon_fiend`, `mon_brute`.

**Stage 1 — Finish the 17, then every variant of an existing base.** First the three planned models that are not on disk yet (`mon_mire_hag`, `mon_rotspawn`, `mon_brass_sentinel`). Then all variants whose base body already exists — skeleton, husk, cultist / priest, wraith, frostbound, sand-revenant, fiend — which need new parts on an existing body builder and no new sculpt. This is the cheapest stage per type and it completes the role sets of seven of the eight current families; the cinderling gets its own model here.

- Finish (3): `mire_hag` 沼巫, `rotspawn` 腐泥巨怪, `brass_sentinel` 黃銅守衛.
- Variants (19):
  - on `fiend`: `imp` 小燼魔, `fiend_caller` 喚燼者
  - on `skeleton`: `bone_scrap` 雜骨, `bone_pallbearer` 抬棺骸, `bone_urnbearer` 捧甕骸
  - on `husk`: `husk_chained` 拖鏈飢骸, `husk_wailer` 號寒骸
  - on `cultist`: `cult_snuffer` 捻燈人, `cult_crossbow` 緘口弩手, `cult_flagellant` 熄膚者, `cult_assassin` 滅跡者
  - on `cult_caster`: `cult_censer` 冷香侍
  - on `wraith`: `wraith_mourner` 泣魂
  - on `frostbound`: `rime_horn` 凍號手, `rime_sentry` 立凍哨
  - on `sand_revenant`: `dune_disc` 擲輪衛, `dune_mirror` 鏡盾衛, `dune_stormjar` 負雷甕者, `dune_masker` 鑄面匠

**Stage 2 — Human-sized Rig A bases.** Fourteen new sculpts covering every new family that has human-sized members; eight of them unlock the variants of stage 3. After this stage every region has at least one new signature family on screen.

- Bases (14): `reaver_cutter` 剪芯匪, `cf_spirit` 寒焰靈, `char_stump` 殘樁, `char_walker` 炭木人, `ash_cast` 灰塑人, `shroud_wrapped` 繭屍, `effigy_knight` 臥像騎士, `bog_fisher` 叉魚沼民, `lamp_bearer` 提菌人, `engine_winder` 上弦僕, `wax_sealed` 蠟封者, `bell_hood` 罩鐘人, `forge_bellows` 鼓風僕, `hush_blank` 無面侍.

**Stage 3 — Variants of the stage-2 bases.** Fifteen variants (weapon, headgear, proportions, materials on a stage-2 body). Completes the Ember-takers and most role sets of the new Rig A families.

- Variants (15):
  - on `reaver_cutter`: `reaver_hook` 鉤燈匪, `reaver_firepot` 潑油匪, `reaver_cagebearer` 提籠人, `reaver_houndmaster` 馴灰人
  - on `ash_cast`: `ash_weeper` 灰泣者
  - on `effigy_knight`: `effigy_orant` 合掌像, `effigy_warden` 壁龕守像
  - on `bog_fisher`: `bog_netter` 撒網沼民, `bog_sleeper` 泥眠者
  - on `lamp_bearer`: `lamp_bomber` 擲孢者
  - on `wax_sealed`: `wax_wick` 燃芯者, `wax_chandler` 封蠟匠
  - on `forge_bellows`: `forge_tongs` 鉗役, `forge_pourer` 傾渣僕
  - on `hush_blank`: `hush_mouth` 噬聲者

**Stage 4 — Rig A bruisers and giants.** Twelve oversized bases (one bruiser per family that has one), three giant bases, and the three Fuel-bearer variants. Rig A is complete after this stage: 83 types.

- Bases (15): `bone_amalgam` 疊骨塚衛, `husk_huddle` 抱暖骸團, `reaver_brute` 砸爐漢, `rime_breaker` 鑿冰巨卒, `cf_hulk` 冰爐巨軀, `hauler_yoke` 負軛巨人, `char_hangtree` 弔枝木, `ash_colossus` 灰積巨像, `effigy_caryatid` 托柱像, `lamp_colony` 菌冠巨軀, `engine_carrier` 馱鏡機, `wax_dripper` 淌蠟巨軀, `bell_clapper` 鐘舌手, `forge_hammer` 鍛錘役, `hush_mute` 默巨.
- Variants (3):
  - on `hauler_yoke`: `hauler_hod` 背薪巨人, `hauler_collector` 拾暖巨人, `hauler_stoker` 添爐巨人

**Stage 5 — Rig F (rigid / floating).** The cheapest new rig — no deformation, at most a hinge, a gimbal or a swarm shader — and its seven creatures touch all seven regions (lantern-ghosts, flame-moths, the three turrets, flickers, the hung bell). Build it first among the new rigs.

- Bases (5): `wraith_gasp` 燈魂, `dusk_moths` 撲火蛾群, `lamp_lure` 偽燈柱, `engine_heliostat` 聚光鏡座, `bell_hanging` 懸鐘.
- Variants (2):
  - on `wraith_gasp`: `cf_flicker` 凍焰苗
  - on `engine_heliostat`: `forge_eye` 爐眼

**Stage 6 — Rig B (quadruped).** One skeleton, three body builds (light hound, heavy boar, sprawler). Eleven creatures; gives every region its beasts and the Ash-handler his hounds.

- Bases (3): `dusk_hound` 嗅燼犬, `dusk_boar` 犁牙豕, `dusk_lizard` 臥銅蜥.
- Variants (8):
  - on `dusk_boar`: `dusk_yak` 負霜牛, `rime_packbeast` 凍馱獸, `fiend_maw` 爐口獸
  - on `dusk_hound`: `dusk_lynx` 雪隙貓, `effigy_hound` 枕足犬, `forge_hound` 鏈渣犬, `hush_beast` 寂獸
  - on `dusk_lizard`: `bog_toad` 鼓腹蟾

**Stage 7 — Rig C (arthropod).** One skeleton, four body builds (beetle, spider, scorpion, hermit-crab). Fifteen creatures, the largest pay-off of any new rig: the arthropod members of three families (sexton-beetles, shroud-weavers, brass-hermits) plus three singles.

- Bases (4): `sexton_beetle` 葬甲蟲, `shroud_weaver` 織殮蛛, `rust_helm` 寄盔蟲, `rust_stinger` 鏽尾蠍.
- Variants (11):
  - on `rust_helm`: `cf_mite` 噬焰晶蟲, `rust_gear` 滾齒蟲, `rust_jar` 貯雷蟹
  - on `sexton_beetle`: `sexton_grub` 墳蛆, `sexton_digger` 掘穴蟲, `sexton_matron` 負棺母蟲, `lamp_crawler` 馱菌蟲
  - on `shroud_weaver`: `shroud_ling` 幼殮蛛, `shroud_dropper` 懸絲蛛, `shroud_matron` 繭母, `hush_ribwalker` 肋行者

**Stage 8 — Rig D (serpentine).** One spine-chain skeleton, two builds (serpent, thick worm). Seven creatures; completes the mire and gives the burrower role its proper bodies.

- Bases (2): `ash_worm` 潛灰蚓, `lurk_eel` 潛泥鱔.
- Variants (5):
  - on `ash_worm`: `char_root` 蝕根
  - on `lurk_eel`: `lurk_leech` 泣蛭, `lurk_spitter` 噴瘴蚺, `lurk_elder` 吞暖巨蛭, `hush_serpent` 淵白蚺

**Stage 9 — Rig E (flyer).** One skeleton with three wing builds (feathered, membrane, insect). Six creatures; last because flyers are garnish everywhere and the core of no family.

- Bases (3): `dusk_crow` 啄燼鴉, `dusk_bat` 倒懸蝠, `dusk_gadfly` 吸暖虻.
- Variants (3):
  - on `dusk_gadfly`: `rust_locust` 銅翅蝗
  - on `dusk_bat`: `fiend_wing` 燼翼, `hush_wing` 寂翼

**Stage 10 — Named content.** Dressing passes on existing bodies for the 28 rare elites, 7 stronghold bosses and 11 of the 14 side-dungeon bosses; three new rigid side-dungeon models (the Walking Sarcophagus, the Workshop Hub, the Great Bellows); four world bosses (one each on rigs C, A, F, D at ×4–5 scale); one new pinnacle model (the First Lamp-keeper) and remix dressings for the seven region bosses.

### 6.1 Rig A bases, by how many variants each unlocks

| base | 中文名 | body | state | variants it unlocks |
|---|---|---|---|---|
| `skeleton` | 骸骨兵 | humanoid biped | built | 4: `skeleton_thrower`, `bone_scrap`, `bone_pallbearer`, `bone_urnbearer` |
| `cultist` | 寂滅教徒 | humanoid biped | built | 4: `cult_snuffer`, `cult_crossbow`, `cult_flagellant`, `cult_assassin` |
| `sand_revenant` | 砂葬者 | humanoid biped | built | 4: `dune_disc`, `dune_mirror`, `dune_stormjar`, `dune_masker` |
| `reaver_cutter` | 剪芯匪 | humanoid biped | new (stage 2) | 4: `reaver_hook`, `reaver_firepot`, `reaver_cagebearer`, `reaver_houndmaster` |
| `hauler_yoke` | 負軛巨人 | giant / construct | new (stage 4) | 3: `hauler_hod`, `hauler_collector`, `hauler_stoker` |
| `husk` | 飢骸 | humanoid biped | built | 2: `husk_chained`, `husk_wailer` |
| `frostbound` | 霜縛屍兵 | humanoid biped | built | 2: `rime_horn`, `rime_sentry` |
| `fiend` | 燼魔 | humanoid biped | built | 2: `imp`, `fiend_caller` |
| `effigy_knight` | 臥像騎士 | humanoid biped | new (stage 2) | 2: `effigy_orant`, `effigy_warden` |
| `bog_fisher` | 叉魚沼民 | humanoid biped | new (stage 2) | 2: `bog_netter`, `bog_sleeper` |
| `wax_sealed` | 蠟封者 | humanoid biped | new (stage 2) | 2: `wax_wick`, `wax_chandler` |
| `forge_bellows` | 鼓風僕 | humanoid biped | new (stage 2) | 2: `forge_tongs`, `forge_pourer` |
| `cult_caster` | 寂滅祭司 | humanoid biped | built | 1: `cult_censer` |
| `wraith` | 縛魂 | floating / incorporeal | built | 1: `wraith_mourner` |
| `ash_cast` | 灰塑人 | humanoid biped | new (stage 2) | 1: `ash_weeper` |
| `lamp_bearer` | 提菌人 | humanoid biped | new (stage 2) | 1: `lamp_bomber` |
| `hush_blank` | 無面侍 | humanoid biped | new (stage 2) | 1: `hush_mouth` |
| `skeleton_thrower` | 骸骨擲矛手 | humanoid biped | built | — |
| `husk_bloated` | 腫脹飢骸 | oversized bruiser | built | — |
| `knight` | 墮落騎士 | oversized bruiser | built | — |
| `choir` | 靜默唱詩者 | floating / incorporeal | built | — |
| `rime_stalker` | 霜角獵者 | humanoid biped | built | — |
| `brute` | 熔岩巨魔 | oversized bruiser | built | — |
| `mire_hag` | 沼巫 | humanoid biped | planned | — |
| `rotspawn` | 腐泥巨怪 | oversized bruiser | planned | — |
| `brass_sentinel` | 黃銅守衛 | oversized bruiser | planned | — |
| `cf_spirit` | 寒焰靈 | floating / incorporeal | new (stage 2) | — |
| `char_stump` | 殘樁 | humanoid biped | new (stage 2) | — |
| `char_walker` | 炭木人 | humanoid biped | new (stage 2) | — |
| `shroud_wrapped` | 繭屍 | humanoid biped | new (stage 2) | — |
| `engine_winder` | 上弦僕 | humanoid biped | new (stage 2) | — |
| `bell_hood` | 罩鐘人 | humanoid biped | new (stage 2) | — |
| `bone_amalgam` | 疊骨塚衛 | oversized bruiser | new (stage 4) | — |
| `husk_huddle` | 抱暖骸團 | oversized bruiser | new (stage 4) | — |
| `reaver_brute` | 砸爐漢 | oversized bruiser | new (stage 4) | — |
| `rime_breaker` | 鑿冰巨卒 | oversized bruiser | new (stage 4) | — |
| `cf_hulk` | 冰爐巨軀 | oversized bruiser | new (stage 4) | — |
| `char_hangtree` | 弔枝木 | oversized bruiser | new (stage 4) | — |
| `ash_colossus` | 灰積巨像 | giant / construct | new (stage 4) | — |
| `effigy_caryatid` | 托柱像 | oversized bruiser | new (stage 4) | — |
| `lamp_colony` | 菌冠巨軀 | oversized bruiser | new (stage 4) | — |
| `engine_carrier` | 馱鏡機 | giant / construct | new (stage 4) | — |
| `wax_dripper` | 淌蠟巨軀 | oversized bruiser | new (stage 4) | — |
| `bell_clapper` | 鐘舌手 | oversized bruiser | new (stage 4) | — |
| `forge_hammer` | 鍛錘役 | oversized bruiser | new (stage 4) | — |
| `hush_mute` | 默巨 | oversized bruiser | new (stage 4) | — |

Note: `skeleton_thrower` is itself a built variant of `skeleton`; `cult_censer` hangs off `cult_caster`.

### 6.2 Rigs B–F and the creatures on each

Each rig is one small skeleton with the standard clip set (`Idle / Walk / Run / Attack1 / Attack2 / Hit / Death / Spawn`). A "body build" is a base sculpt on that skeleton; the creatures listed after it reuse its builder with new parts and proportions.

| rig | body build (base) | creatures built on it | count |
|---|---|---|---|
| **F** rigid / floating — 7 creatures | `wraith_gasp` 燈魂 | `cf_flicker` 凍焰苗 | 2 |
|  | `dusk_moths` 撲火蛾群 | — | 1 |
|  | `lamp_lure` 偽燈柱 | — | 1 |
|  | `engine_heliostat` 聚光鏡座 | `forge_eye` 爐眼 | 2 |
|  | `bell_hanging` 懸鐘 | — | 1 |
| **B** quadruped — 11 creatures | `dusk_hound` 嗅燼犬 | `dusk_lynx` 雪隙貓, `effigy_hound` 枕足犬, `forge_hound` 鏈渣犬, `hush_beast` 寂獸 | 5 |
|  | `dusk_boar` 犁牙豕 | `dusk_yak` 負霜牛, `rime_packbeast` 凍馱獸, `fiend_maw` 爐口獸 | 4 |
|  | `dusk_lizard` 臥銅蜥 | `bog_toad` 鼓腹蟾 | 2 |
| **C** arthropod — 15 creatures | `sexton_beetle` 葬甲蟲 | `sexton_grub` 墳蛆, `sexton_digger` 掘穴蟲, `sexton_matron` 負棺母蟲, `lamp_crawler` 馱菌蟲 | 5 |
|  | `shroud_weaver` 織殮蛛 | `shroud_ling` 幼殮蛛, `shroud_dropper` 懸絲蛛, `shroud_matron` 繭母, `hush_ribwalker` 肋行者 | 5 |
|  | `rust_helm` 寄盔蟲 | `cf_mite` 噬焰晶蟲, `rust_gear` 滾齒蟲, `rust_jar` 貯雷蟹 | 4 |
|  | `rust_stinger` 鏽尾蠍 | — | 1 |
| **D** serpentine — 7 creatures | `ash_worm` 潛灰蚓 | `char_root` 蝕根 | 2 |
|  | `lurk_eel` 潛泥鱔 | `lurk_leech` 泣蛭, `lurk_spitter` 噴瘴蚺, `lurk_elder` 吞暖巨蛭, `hush_serpent` 淵白蚺 | 5 |
| **E** flyer — 6 creatures | `dusk_crow` 啄燼鴉 | — | 1 |
|  | `dusk_bat` 倒懸蝠 | `fiend_wing` 燼翼, `hush_wing` 寂翼 | 3 |
|  | `dusk_gadfly` 吸暖虻 | `rust_locust` 銅翅蝗 | 2 |

### 6.3 Regular types available per region after each stage

| region | stage 0 | stage 1 | stage 2 | stage 3 | stage 4 | stage 5 | stage 6 | stage 7 | stage 8 | stage 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| 灰燼荒原 `wastes` | 4 | 8 | 12 | 17 | 23 | 24 | 26 | 30 | 32 | 33 |
| 霜脊隘口 `frost` | 6 | 12 | 14 | 18 | 24 | 26 | 29 | 30 | 30 | 31 |
| 沉眠墓原 `crypt` | 8 | 18 | 21 | 26 | 30 | 32 | 34 | 42 | 42 | 43 |
| 瘴泣沼澤 `mire` | 5 | 10 | 14 | 17 | 19 | 22 | 24 | 25 | 30 | 32 |
| 鏽砂廢都 `sands` | 7 | 15 | 17 | 21 | 26 | 28 | 30 | 34 | 34 | 36 |
| 寂滅聖城 `sanctum` | 7 | 16 | 20 | 25 | 29 | 32 | 33 | 34 | 34 | 36 |
| 燼淵 `abyss` | 8 | 13 | 16 | 20 | 24 | 26 | 29 | 30 | 32 | 35 |

### 6.4 Totals

| | models |
|---|---|
| On disk today | 14 |
| Planned in the current 17, still to build | 3 |
| New Rig A bases | 29 |
| New Rig A variants (including the cinderling's own model) | 37 |
| Rigs B–F (17 bases + 29 variants) | 46 |
| **Regular models at completion** | **129** |
| Still to build | 115 |
| Named content: new models | 8 (3 rigid side-dungeon bosses, 4 world bosses, 1 pinnacle boss) |
| Named content: dressing passes on existing bodies | 53 (28 rare elites, 7 stronghold bosses, 11 side-dungeon bosses, 7 pinnacle remixes) |

## 7. Machine-readable appendix

`Y:\game\data\bestiary.json` (UTF-8, generated from the same source as this document) contains:

- `meta` — title, counts, the region-letter, rig and body-plan legends.
- `families[]` — id, name_zh, name_en, scope, concept, lore, roles_have, roles_lack, regions, signature_in, rigs, types.
- `types[]` — every regular type with all fields of the tables in section 2: id, name_zh, name_en, family, role, ai, ai_kind, ai_traits, rig, rig_note, body_plan, build, variant_of, model, element, status, hp, dmg, speed, scale, anims, regions, spawn_weight, stage, silhouette.
- `reskins[]` — the regional reskins of section 3.9.
- `regions[]` — act, level, families with weights and signature flags, the flat spawn list, dungeons with their families and bosses, stronghold, region boss, world boss and rare elites.
- `named` — `rare_elites`, `stronghold_bosses`, `dungeon_bosses`, `region_bosses`, `world_bosses`, `pinnacle_bosses`: id, name_zh, name_en, base, region, where, look, mechanics.
- `behaviours[]` — the specs of section 5 with the types that use each.
- `production[]` — the stages of section 6 with their model lists.

## 8. Count check

Computed by the build from the tables above (129 regular types in 25 families).

**Types per family** (target 4–6; smallest 2–3; largest 8–10 for the catch-alls)

| family | 中文名 | types | humanoid | bruiser | other body plans | rigs |
|---|---|---|---|---|---|---|
| `bone` | 骸骨 | 6 | 5 | 1 |  | A×6 |
| `husk` | 飢骸 | 5 | 3 | 2 |  | A×5 |
| `cult` | 寂滅教團 | 9 | 7 | 1 | floating / incorporeal ×1 | A×9 |
| `wraith` | 縛魂 | 3 | 0 | 0 | floating / incorporeal ×3 | A×2, F×1 |
| `reaver` | 盜燼眾 | 6 | 5 | 1 |  | A×6 |
| `dusk` | 灰暮野物 | 9 | 0 | 0 | quadruped beast ×5, flyer ×3, swarm / turret / siege ×1 | B×5, E×3, F×1 |
| `frost` | 霜縛 | 6 | 4 | 1 | quadruped beast ×1 | A×5, B×1 |
| `coldfire` | 凍焰 | 4 | 0 | 1 | floating / incorporeal ×2, arthropod / crawler ×1 | A×2, C×1, F×1 |
| `hauler` | 馱薪巨人 | 4 | 0 | 0 | giant / construct ×4 | A×4 |
| `sexton` | 葬甲 | 4 | 0 | 0 | arthropod / crawler ×4 | C×4 |
| `char` | 炭木 | 4 | 2 | 1 | serpentine ×1 | A×3, D×1 |
| `ash` | 灰塑 | 4 | 2 | 0 | giant / construct ×1, serpentine ×1 | A×3, D×1 |
| `shroud` | 殮蛛 | 5 | 1 | 0 | arthropod / crawler ×4 | A×1, C×4 |
| `effigy` | 臥像 | 5 | 3 | 1 | quadruped beast ×1 | A×4, B×1 |
| `mire` | 沼裔 | 6 | 4 | 1 | quadruped beast ×1 | A×5, B×1 |
| `lamp` | 偽燈菌 | 5 | 2 | 1 | swarm / turret / siege ×1, arthropod / crawler ×1 | A×3, C×1, F×1 |
| `lurk` | 泥潛 | 4 | 0 | 0 | serpentine ×4 | D×4 |
| `sands` | 砂葬 | 6 | 5 | 1 |  | A×6 |
| `rust` | 寄銅蟲 | 5 | 0 | 0 | arthropod / crawler ×4, flyer ×1 | C×4, E×1 |
| `engine` | 日輪機關 | 3 | 1 | 0 | swarm / turret / siege ×1, giant / construct ×1 | A×2, F×1 |
| `wax` | 蠟封眾 | 4 | 3 | 1 |  | A×4 |
| `bell` | 啞鐘 | 3 | 1 | 1 | floating / incorporeal ×1 | A×2, F×1 |
| `fiend` | 燼魔 | 6 | 3 | 1 | flyer ×1, quadruped beast ×1 | A×4, B×1, E×1 |
| `forge` | 爐僕 | 6 | 3 | 1 | swarm / turret / siege ×1, quadruped beast ×1 | A×4, B×1, F×1 |
| `hush` | 淵寂 | 7 | 2 | 1 | arthropod / crawler ×1, quadruped beast ×1, serpentine ×1, flyer ×1 | A×3, B×1, C×1, D×1, E×1 |
| **total** | 25 families | **129** | 56 | 17 | 56 | |

Smallest family: 3 types (wraith, engine, bell). Largest: 9 (cult, dusk). Families with 4–6 types: 19 of 25.

**Per body plan**

| body plan | types | share | target |
|---|---|---|---|
| humanoid biped | 56 | 43.4% | about 45% |
| oversized bruiser | 17 | 13.2% | 12–15% |
| arthropod / crawler | 15 | 11.6% | about 12% |
| quadruped beast | 11 | 8.5% | 8–10% |
| serpentine | 7 | 5.4% | about 6% |
| flyer | 6 | 4.7% | about 5% |
| floating / incorporeal | 7 | 5.4% | about 5% |
| giant / construct | 6 | 4.7% | about 5% |
| swarm / turret / siege | 4 | 3.1% | 3–4% |
| **total** | **129** | 100% | |

**Per rig**

| rig | types | existing | new bases | new variants | body plans on it |
|---|---|---|---|---|---|
| A — shared humanoid skeleton (available now) | 83 | 18 | 29 | 36 | humanoid biped ×56, oversized bruiser ×17, giant / construct ×6, floating / incorporeal ×4 |
| B — quadruped | 11 | 0 | 3 | 8 | quadruped beast ×11 |
| C — arthropod | 15 | 0 | 4 | 11 | arthropod / crawler ×15 |
| D — serpentine | 7 | 0 | 2 | 5 | serpentine ×7 |
| E — flyer | 6 | 0 | 3 | 3 | flyer ×6 |
| F — rigid / floating | 7 | 0 | 5 | 2 | swarm / turret / siege ×4, floating / incorporeal ×3 |
| **total** | **129** | 18 | 46 | 65 | rigs B–F together: 46 |

(The cinderling `imp` is counted as an existing id; its own model is one of the stage-1 variants.)

**Per region**

| region | families | signature | shared | regular types present | of which Rig A | dungeons (families each) | rare elites |
|---|---|---|---|---|---|---|---|
| 灰燼荒原 `wastes` | 8 | 3 | 5 | 33 | 23 | 3 / 3 / 3 | 4 |
| 霜脊隘口 `frost` | 8 | 3 | 5 | 31 | 24 | 3 / 3 / 3 | 4 |
| 沉眠墓原 `crypt` | 9 | 3 | 6 | 43 | 30 | 3 / 3 / 3 | 4 |
| 瘴泣沼澤 `mire` | 8 | 3 | 5 | 32 | 19 | 3 / 2 / 3 | 4 |
| 鏽砂廢都 `sands` | 9 | 3 | 6 | 36 | 26 | 3 / 3 / 3 | 4 |
| 寂滅聖城 `sanctum` | 8 | 3 | 5 | 36 | 29 | 2 / 3 / 3 | 4 |
| 燼淵 `abyss` | 8 | 3 | 5 | 35 | 24 | 3 / 3 / 3 | 4 |

Families present in the most regions: `dusk` 7, `bone` 6, `cult` 5, `wraith` 5, `husk` 4, `reaver` 4. Single-region families: 13.

**Per role and per AI kind**

| main role | types | | AI kind | types |
|---|---|---|---|---|
| melee | 20 | | Melee | 40 |
| bruiser | 19 | | Brute | 22 |
| swarmer | 17 | | Ranged | 9 |
| ranged | 13 | | Caster | 9 |
| support | 11 | | Bomber (new) | 7 |
| caster | 9 | | Flyer (new) | 6 |
| flyer | 6 | | Charger (new) | 5 |
| ambusher | 5 | | Burrower (new) | 5 |
| charger | 4 | | Support | 4 |
| burrower | 4 | | Caller (new) | 3 |
| stealth pouncer | 3 | | Stalker (new) | 3 |
| turret | 3 | | Turret (new) | 3 |
| shield-bearer | 2 | | Shieldbearer (new) | 2 |
| exploder | 2 | | Exploder | 2 |
| mother | 2 | | Healer (new) | 2 |
| giant | 2 | | Leaper | 2 |
| special | 2 | | Spawner (new) | 2 |
| swarm | 1 | | Reviver (new) | 1 |
| giant bruiser | 1 | | Teleporter | 1 |
| giant ranged | 1 | | Swarm (new) | 1 |
| giant special | 1 | |  |  |
| giant bomber | 1 | |  |  |

**Named content**

| kind | count | target |
|---|---|---|
| named rare elites | 28 | 28 (4 per region) |
| stronghold bosses | 7 | 7 |
| side-dungeon bosses | 14 | 14 (2 per region) |
| region bosses (existing) | 7 | 7 |
| world bosses | 4 | 3–4 |
| pinnacle ladder bosses | 8 | 8 |
| regional reskins | 28 (10 existing, 18 new) | — |
| new AI kinds / traits | 12 / 23 | — |

## 9. What did not fit, and things to know before building

- **Body-plan mix is close but not exact.** Humanoid bipeds land a little under the 45% target (see section
  8); every other band is met. Raising the humanoid share further would have meant either going past 130 types
  or cutting arthropods and serpents, which carry three whole region-signature families. Rigs B–F carry 46
  creatures, one more than the 40–45 asked for: the eleventh quadruped (the effigies' foot-hound) is what
  brings quadrupeds into their band and gives the sanctum its only beast.
- **Crypt and sands have six shared families, not three to five.** Both sit at nine families in total, which
  is inside the 6–9 band; the extra shared family is the price of keeping the existing spawn-table reskins
  (`sand_husk`, `sand_thrower`, `sand_caster`) and of letting tomb-robbing Ember-takers into the barrows.
- **Thirteen families live in a single region.** That is what "signature" costs. Their reuse comes from the
  rift (section 3.8), the world bosses' adds, and the named content; if more overworld reuse is wanted, the
  cheapest candidates are shroud-weavers in the sanctum catacombs and false-lamps in the crypt's wet galleries.
- **Pinnacle ladder is seven remixes plus one new boss** (the First Lamp-keeper), not six plus two. Every
  region boss gets a rung; a second new boss would have pushed the ladder to nine.
- **No mounted pair.** The role list offered one; nothing in this world rides, so the handler-and-hounds pair
  (the Ash-handler with `Escort`) fills that slot instead.
- **No true silence status.** The Order and the Pale Hush "silence" through `weaken`, resource drain and
  extinguishing ground effects. Locking the hero's skills was left out on purpose; it appears only as two
  telegraphed boss mechanics (the Abbess, Seren's ladder rung).
- **Three of the "17 existing models" are not on disk**: `mon_mire_hag`, `mon_rotspawn` and
  `mon_brass_sentinel` have MonsterDefs but no `.glb` or build script yet, so their silhouette briefs here are
  new and should be treated as the brief for those models. None of the seven `boss_*.glb` files is on disk
  either (the bosses run on the placeholder).
- **Region-boss scale.** See section 0: regular giants reach 1.9, so the smallest region bosses (1.8–1.9)
  need to grow when the giants ship.
- **Region name mismatch (already present).** `DESIGN.md` calls the crypt region 沉眠墓窟, `MonstersData.cs`
  calls it 沉眠墓原. This document uses the name in code.
- **Spawn-table shape.** `RegionDef.Spawns` is a flat list of (monster, weight). Section 3 gives family weights
  and the flat list derived from them; using families properly (a pack drawn from two or three families, a
  dungeon restricted to its listed families) needs a small `Populate` change — a `Families` list on
  `RegionDef` / `DungeonDef` — which is implied by this design but not specified here.
- **Roles the spawner has to learn.** `Populate.RosterOf` sorts by `Role` into core / ranged / bruiser. The
  new role strings (shield-bearer, ambusher, charger, burrower, flyer, turret, swarm, mother, giant, stealth
  pouncer …) need a mapping: turrets, dormant ambushers and nests should be placed by the level layout rather
  than rolled into walking packs, and giants should be capped at one per pack.
- **Numbers are first-pass.** Life, damage, speed, weights and every cooldown here follow the existing defs'
  ranges but have not been played; treat them as starting values for tuning, not as tested balance.
