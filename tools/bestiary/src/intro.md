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
