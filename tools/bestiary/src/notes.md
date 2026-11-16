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
