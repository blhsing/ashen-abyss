# 燼淵 icon style bible

One look for every icon, whether it was rendered in Blender or painted by the image model — and the same light and
materials as the ornate UI frames (frame artist: please keep to "Light", "Palette" and "Materials" below).

## Format
- Master: 256×256 PNG, RGBA, straight alpha (class crests and the title emblem: 512×512). Shown at 44–130 px.
- **Transparent background for every category.** The UI draws the slot well, frame and rarity colour; an icon is only
  the subject plus its own glow. No border, no plate, no text, no numbers, no watermark.
- One centred subject; its longest side fills 86 % of the frame (post.py normalises this for both sources).
- Three-quarter view. Long things lie on the bottom-left → top-right diagonal with the business end up-right.
- Must read instantly at 64 px: one bold silhouette, one idea. Thin real-world proportions are thickened
  (weapon shafts ×1.5–2.3 in width) — legibility before literal scale.

## Light (shared with the UI frames)
- **Key: warm, from the UPPER LEFT, in front** (direction to the light (-0.62, -0.55, +0.62) with the camera on -Y;
  colour (1.00, 0.80, 0.58) ≈ 3400 K firelight). Highlights and bevel catch-lights therefore sit top-left.
- **Rim: cold blue from behind, RIGHT** ((+0.70, +0.62, +0.38), colour (0.45, 0.68, 1.00)) — the ashen dusk; it
  outlines the right/bottom edge of every form against the dark slot.
- Weak cool fill from the right front; overcast HDRI (`res://assets/env/sky/overcast.hdr`) only for reflections.
- Shadows fall to the lower right and go nearly black: the game's panels are near-black (#080708 wells on #100E0F).
- Colour management: Standard view, "Medium High Contrast" look. Camera: orthographic, pitched 14° down.

## Palette
- Neutral base: soot black, ash grey, worn steel (0.27, 0.26, 0.25), rusted iron, dark leather (0.10, 0.065, 0.04),
  bone (0.56, 0.50, 0.38), old brass/bronze (0.40, 0.28, 0.09), tarnished gold (0.46, 0.33, 0.10).
- **Saturation is reserved for the accent**: one glowing element per icon in the element / class / rarity colour.
  Ember orange (1.00, 0.42, 0.08) is the world's signature (ember seams, coals, sparks).
  Elements: fire orange · frost pale blue (0.45, 0.80, 1.0) · lightning blue-white/violet · poison green ·
  shadow violet-black · physical = steel. Classes: emberblade (1, .5, .15) · astromancer (.4, .7, 1) ·
  hunter (.25, .85, .7) · soulbinder (.7, .9, .6) · wildsoul (.55, .85, .4) · forgebody (1, .38, .12) ·
  lampknight (1, .85, .45) · pactbinder (.9, .25, .45).
- UI frame metal should be the same family: dark forged iron with bronze/tarnished-gold trim and an ember inlay.

## Materials (physically real, worn)
Blender icons use the project's kit looks (`Y:\tools\blender\monsters\looks.py` v2 with scanned CC0 detail from
`montex.py`, imported read-only by `Y:\tools\blender\icons\iconlib.py`): `metal` presets worn_steel / rusty_iron / brass /
gold_tarnish / black_plate, `leather`, `cloth`, `bone`, `wood`, `rock`, `ice`, `wax`; plus iconlib's own `gem`
(faceted, per-facet brilliance), `glass`, `ember` (hot veins in dark crust), `emit`, `fire`. Models are built at life
size so the scan grain is right. Edges are bevelled; nothing is clean or new.

## Item tiers (must never be confusable inside one list)
1 cloth / leather, humble · 2 iron and mail · 3 steel plate with brass · 4 **ash-forged**: black plate with thin
ember-orange seams · 5 **ember-crowned**: dark plate with tarnished gold trim and a strong glowing ember element.
Weapons use the hero's own meshes (`wpn_1/2/3` in `hero_*.glb`): tier 1 = wpn_1, 2 = wpn_2, 3 = wpn_3 without glow,
4 = wpn_3 ash-forged (blackened, ember seams, a few sparks), 5 = wpn_3 warm metal with a strong class-colour glow.
Gems: tier 1 chipped rough shard, 2 clean cut, 3 larger cut with inner glow and glints; each kind has its own cut.

## Production routes
- **Things** (items, materials, containers, map / shrine tokens, paragon tokens, crests): Blender, Cycles 40–72
  samples + denoise, 512 px raw → `post.py render` (glow from the hottest pixels, grade, fit, 256 px).
- **Actions and abstractions** (skills, passives, specialisations, statuses, buffs, stat icons, elite affixes):
  image generation (Codex CLI, built-in image tool, gpt-image-2), nine icons per 3×3 sheet on pure black →
  `post.py sheets` (cut-out: alpha from brightness with enclosed dark areas kept opaque, same glow / grade / fit).
  A Blender test of two skill vignettes (burning greatsword, frost lance; kept as category `vignette`) read clearly
  worse at 64 px than the painted versions, so skills stay painted; the painted sheets are prompted as
  "realistic dark-fantasy 3D render" with this document's light so they sit next to the Blender renders.
- Sheet prompt (fixed part, in `imagegen.py`): "3 by 3 grid of nine separate square icons on a pure black background
  with wide black gaps … each icon is one centred subject filling about 80 percent of its cell, a realistic
  dark-fantasy 3D render with physically real materials (worn steel, leather, bone, cloth, stone, glowing ember), warm
  key light from the upper left and a cold blue rim light from the right, strong readable silhouette, saturated
  colour only in the magical effect. No text, no letters, no numbers, no borders, no frames, no watermark." followed
  by the nine subjects, each one plain sentence (subject, material, motion, colour of the effect).
- Originality: every subject is designed for this world (ash, embers, the dead Sun-Forge, lantern keepers, the Hushed
  Order). Prompts and models never name or imitate an existing game, film, artist or franchise.

## Quality gate
Contact sheets at 64 px on the UI background (`Y:\shots\icons\sheet_*.png`, legend in the .txt beside each). Redo
anything muddy, unreadable at 64 px, off-style, or carrying text / watermark / grid artefacts. Icons that share a
list (a class's eight skills, the five tiers of a slot, the six shrine blessings) must differ in silhouette or
colour, not only in detail.
