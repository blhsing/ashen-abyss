p = r'Y:\tools\blender\monsters\PROGRESS.md'
s = open(p, encoding='utf-8').read()
i0 = s.index("mon_ash_weeper | ash | 5424 | 5.04 MB")
head = s[:i0]
# the hush lines above are refreshed too (chalk look)
head = head.replace("mon_hush_blank | hush | 7277 | 1.42 MB | verify OK | spec | fingers curl into cages in relaxed clips (2x stretch off their joints); floats 2.5 cm; plain chalk look without texture interest",
                    "mon_hush_blank | hush | 7277 | 2.50 MB | verify OK | spec, rebuilt 11-18 with the cracked-chalk look | fingers curl into cages in relaxed clips (2x stretch off their joints); floats 2.5 cm")
head = head.replace("mon_hush_mouth | hush | 6995 | 2.05 MB | verify OK | spec | the hoop is skinned by height to the spine and pinches when the torso twists; a small gap shows between the pelvis stub and the legs from below; plain chalk surface",
                    "mon_hush_mouth | hush | 6995 | 4.02 MB | verify OK | spec, rebuilt 11-18 with the cracked-chalk look | the hoop is skinned by height to the spine and pinches when the torso twists; a small gap shows between the pelvis stub and the legs from below")
head = head.replace("mon_forge_pourer | forge | 11211 | 4.64 MB | verify OK | spec | crucible frame is rigid on spine_03 while the head moves under it; slag surface is mostly cooled crust with one bright patch; dipper bowl small",
                    "mon_forge_pourer | forge | 11211 | 4.64 MB | verify OK | spec, rebuilt 11-19 (slag look: deeper orange, real crust islands) | crucible frame is rigid on spine_03 while the head moves under it; dipper bowl small")
new = r"""mon_ash_weeper | ash | 5424 | 4.95 MB | verify OK | spec, REBUILT 11-18 | arms baked 122 deg up and made stiff casts (body merge_w: elbow / hand weights go to the upper arm) -> a clean Y in the idle, no ribbons in any clip; the arms cannot bend; shoulder skin is crumpled in the armpit; legs hidden in a pelvis-rigid ash mound (it does not kneel)
mon_bog_netter | mire | 14258 | 4.78 MB | verify OK | spec, REBUILT 11-18 | net is real cord geometry now (open diamond mesh, 12 mm cords): gathered on the shoulder, a triangular train on the floor with 12 sinker stones on its hem; train + stones are bound to the ROOT bone (they stay on the floor, the hanging part stretches in lunges, the train turns with her like a stiff board); cords are single flat quads (thin edge-on)
mon_bog_sleeper | mire | 14746 | 4.93 MB | verify OK | spec, REBUILT 11-18 | wet peat (mud look in the skin slot) to a ragged tide-line at the chest + hands / forearms + clods on the pale skin (clods read a little like dark spots); straw leans back 42 deg so that it stands upright in the Zombie stoop (it leans back in the rest pose); slab and reed are rigid
mon_wax_wick | wax | 12392 | 4.46 MB | verify OK | spec, REBUILT 11-18 | flame in three colour bands (part flame3), no white blob; the dome is really melted away on the left: half the face and the jaw are bare; wax runs round the dome rim are stiff cones (read like tusks); flame is rigid on spine_03
mon_hauler_yoke | hauler | 11316 | 5.43 MB | verify OK | spec, FIXED 11-18 | fault found: yoke, chains and bundles each followed the bone's bulk scale with their own centre (bundles hung in the air beside the yoke ends) -> one shared keep anchor; loads are rigid on spine_03 by design (no swing of their own)
mon_hauler_hod | hauler | 10452 | 5.57 MB | verify OK | spec, FIXED 11-18 | same fault (frame posts stood apart from the hoops and the basket; the hod hovered behind the back) -> shared anchor on its front face, bound to spine_02, straps seated on the body and skinned (were two loose hoops); the hod tips forward with the whole trunk in Sword_Attack / OverhandThrow; trunks are plain logs
mon_hauler_collector | hauler | 12217 | 4.72 MB | verify OK | spec, FIXED 11-18 | cage rebuilt: open weave (14 ribs, 5 hoops, floor), a heap of bundles with a faint glow between them instead of a black blob with floating glow bars; sits on the back, real shoulder straps; nothing moves inside it; tong arm drags close to the floor when the arm hangs
mon_hauler_stoker | hauler | 10926 | 5.95 MB | verify OK | spec, FIXED 11-18 | shovel load is a heap now: lava-orange slag with crust + dark clinker lumps (was one flat yellow lump); the square window of the face-shield is a dark plate on the surface, not a hole; brand ring half hidden by the apron bib
mon_reaver_brute | reaver | 12628 | 4.85 MB | verify OK | spec, FIXED 11-18 | bowl is soot-black iron with rust (was bright orange), eye-slot follows the bowl's curve (a black bar stood 3 cm proud); no head modelled under the bowl; rope belt is a clean ring
mon_bell_clapper | bell | 9040 | 5.11 MB | verify OK | spec, reviewed 11-18 (no change) | plain mask-like face; the coat is one padded surface without seams or buttons; clapper is carried like a sword, not over the shoulder
mon_bone_amalgam | bone | 14676 | 5.42 MB | verify OK | spec, FIXED 11-18 | the cord lashing of the doubled ribcage was two wide flat hoops standing off the ribs -> four tight rope loops; small bone pieces are still auto-decimated x0.4 / x0.86 to fit 15k (side skulls a little faceted); second ribcage intersects the first
mon_hush_mute | hush | 6622 | 5.08 MB | verify OK | spec, REDONE 11-18 | new torso (part mute_torso: a low wide arch with a real tunnel through the chest, skinned like the body), paddle hands fused to the forearm (hand-bound mittens came apart with the lengthened arms), cracked-chalk look for the whole family (was smooth clay); from the front the hole still reads a little like a mouth in a head; arms 1.15x swing slightly off the elbow
mon_husk_huddle | husk | 14745 | 5.35 MB | verify OK | spec (part huddle) | five half-figures in one rigid cluster on spine_03, their hanging arms blend to spine_01 (lag, no real swing); the blanket is a heightfield draped over the real shapes (frozen stiff, a few holes, strips over the walker's brow); body parts decimated x0.89; the fused figures have no legs (cut at the waist inside the mass)
mon_rime_breaker | frost | 9836 | 5.41 MB | verify OK | spec (part arm_plough) | the plough is rigid on the left forearm (stands upright in front of the arm when it hangs, swings up with every arm movement and can cut through the coat in Sword_Attack); it is at his side, not in front as a prow; no face (hood void); the boulder's core shows as three small glowing windows
mon_cf_hulk | coldfire | 4406 | 4.42 MB | verify OK | spec (bespoke part brazier_hulk, hidden body) | low-poly faceted ice (4.4k tris); the core is a plain glowing loft with dark crust patches; the basket is rigid on spine_02 while the arms pass between its bars (they can cut the bars in wide swings); the three brazier legs read like big nails
mon_char_hangtree | char | 6806 | 5.12 MB | verify OK | spec (bespoke part gallows_tree, hidden body) | the bough is rigid on spine_03 (the right arm bones move nothing; the bough sweeps and tips with the trunk, in Sword_Attack the cage reaches the floor); nooses / cage only lag by a weight blend, they do not hang true when the trunk tips; trunk is one loft with painted bark (no separate bark plates)
mon_ash_colossus | ash | 21750 | 5.53 MB | verify OK (giant limit 22k) | spec (part drift_mass) | 14 half-figures + big ash drifts, all rigid lumps on the spine bones (they slide against each other when the torso bends); the figures only read as figures up close, from the game camera it is one lumpy mound; sparks are few and small; plume tongues are stiff blades with a weight blend; legs are the plain body
mon_effigy_caryatid | effigy | 9998 | 5.07 MB | verify OK | spec (part pillar_load) | arms are stiff (baked 110 deg up, weights merged into the upper arm): the hands are near the pillar in the idle but leave it in every attack; pillar and capital rigid on spine_03 / Head (the capital tilts with the head); body is the smoothed base mesh under carved armour
mon_lamp_colony | lamp | 14750 | 5.53 MB | verify OK | spec (part colony) | the two extra bodies are skinned by height to the walker's spine and their legs dangle rigidly from the pelvis (they do not walk); cap, stalk and lantern-fruit are one rigid assembly on spine_03 (no swing; the cap tips with the trunk in attacks); twisted arms are three copies of the same arm; walker's body decimated x0.83
mon_engine_carrier | engine | 9740 | 5.08 MB | verify OK (giant) | spec (bespoke part carrier, hidden body) | rigid boxes and cylinders per bone (gaps open at hips and shoulders in deep bends); fork tines are thin; the wedge is low-roughness brass, not a mirror; A-frame chains are rigid
mon_wax_dripper | wax | 14736 | 4.12 MB | verify OK | spec (bespoke part tallow_mound, hidden body) | the cone is pelvis-rigid below the hips and stands on the floor (it slides, no legs); half-figures / hands are rigid lumps on the cone; the arm-bodies are weighted along the arm (they hang inside the cone's outline in the idle, the head-fist only reads up close); wax is smooth by nature (runs, soot, no cracks)
mon_forge_hammer | forge | 13453 | 5.24 MB | verify OK | spec (part hammer_arm) | hammer assembly is rigid on clavicle / upper arm / forearm (plates slide at shoulder and elbow); the block hangs at knee height in the idle and passes through the thigh in some frames of Walk_Loop; the left hand does not steady anything; body does not list (only a 6 deg spine roll)

## FINISHING PASS 2026-11-18 / 19 (second artist) -- STATE: all 83 rig-A types are built; nothing pending
Done: the 4 pending rebuilds, the 8 unreviewed stage-4 models looked at and fixed, the 10 remaining stage-4 bases built (lines above), hush_blank /
hush_mouth / forge_pourer rebuilt because shared looks changed (chalk, slag).  Every .glb passed verify; all are <= 6 MB.  Imported with
Y:\tools\import.ps1; game run with --bestiary --spawn=<the 22 types>: see the report / the end of this section.
New tools and rules (read before touching the assembler again):
 - asm_review.py / asm_review.ps1 -Ids a,b : one sheet per creature (Y:\shots\chars\asm\review_<id>.png, 4 x 2 tiles: game camera | front |
   back | side | attack early | attack late | walk | raking close-up).  asm_go.ps1 -Ids a,b = build + verify + these sheets.  The family
   tiles alone hide too much (their 'bust' frames the top 0.62 m, i.e. only the load of a carrier).
 - asm_specs.POSES 'brute' / 'bomber' / 'zbrute': what the GAME plays (Bestiary.cs: Idle_Loop + Walk_Loop unless the anims text names a Zombie
   idle).  'heavy' (Sword_Idle) is NOT what a monster idles in.
 - A.add(..., keep=<point>): pieces of ONE carried assembly share an anchor.  keep=True moves every piece with its own centre: on a body with
   bulk / chest scale the assembly comes apart (that was the fault of the yoke, hod and cage).  Put the anchor on the face that touches the body.
 - body merge_w={'upperarm_l': ['lowerarm_l', 'hand_l'], ...}: a limb baked far from its rest pose must be made stiff, otherwise forearm and
   hand swing round joints that are no longer where the mesh is (ash_weeper ribbons).  Rigid parts on merged bones are re-bound in finish().
 - never bind a mitten / club to the hand bone when arm_len != 1 (it swings off the wrist and separates): bind it to the forearm.
 - straps round a body are seated + skinned (asm_parts6.load_straps, asm_parts9.chest_band), not rigid bands (they float as hoops as soon as
   the body has bulk or a hunch).
 - skin weights of a wide thing lying along a limb: by position along the limb (weights_by), not by nearest surface (it picks up the torso).
 - only cloth_ / leather_ / spectral_ materials are double sided: flat cords, sheets and nets must use such a look (asm_parts8.net_cords).
 - emissive looks: sRGB lifts the green channel, a 'lava' colour of (1, 0.36, 0.05) renders yellow; use (1, 0.2, 0.02), base_mul <= 0.3, and
   crust=1 explicitly (the kind keys the crust on the look NAME, which the preset name hides).  One strength per emis_ slot (the first look).
 - spec keys: budget (22000 for giants; verify.LIMITS lists them), jpeg=(bc, orm, nrm) qualities for all-rock creatures that exceed 6 MB.
 - parts: asm_parts8.py (cast_net, flame3, mute_torso, mute_hands, looks), asm_parts9.py (arm_plough, huddle, gallows_tree, brazier_hulk,
   carrier, colony, drift_mass, tallow_mound, chest_band, band_glow), specs_s4_c.py.  dbg\ holds the one-off patch scripts and stretch.py
   (lists edges that stretch in a pose: finds weights left on bones that are far from the mesh).
Sheets: Y:\shots\chars\family_<family>.jpg (+ _sil, _detail<k>) for all 21 families; working sheets family_stage*/fix* deleted.
No detached processes are left running.
"""
open(p, 'w', encoding='utf-8', newline='\n').write(head + new)
print('progress written', len(head), len(new))
