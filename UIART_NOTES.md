# 燼淵 UI frame art — working notes (UI art director / UI engineer)

Goal: replace the flat code-drawn style boxes by ornate raster chrome rendered from real 3D models, and rebuild the
interface on it. Everything is regenerable by script; see `Y:\tools\uiart\README.md`.

## Pipeline (all in `Y:\tools\uiart\`)
1. `kit.py`     Blender toolkit: orthographic camera straight down, sun from the top-left, a studio world made of soft
                lobes (key top-left, cool rim bottom-right), a shadow-catcher plane as the panel field (→ cast shadow and
                contact occlusion end up in the alpha), geometry helpers (mouldings from a cross-section swept round any
                outline, bevelled plates, studs, domes, faceted stones, tapering wire for filigree), procedural PBR
                materials with scanned CC0 detail (hammered iron, gold, bronze, stone, leather, wax, ember stone, enamel).
2. `styles.py`  Style directions: material roles (bar / plate / face / trim / stud / stone / floor / band + state
                variants), moulding cross-sections, motifs (corner casting, crest, sprig, jewel, stud, edge detail).
3. `pieces.py`  Every piece: name, kind (nine / h3 / v3 / sprite), sizes in x2 texture px, post-processing, build function.
4. `render.py` (`render.ps1`)  one Blender run renders any list of pieces → `Y:\assets_src\ui_gen\render\<style>\`.
5. `slice.py`   seamless edge tiles (two rendered periods are cross-faded), outer drop shadows, glows, ember bloom,
                field tiles from scans, the x1 set (Lanczos half), `manifest.json` → `Y:\game\assets\ui\{x2,x1}\`.
6. `mock.py`    composes a mock-up screen from a kit (style exploration).
Units: 2 texture px = 1 design px (1080p). The game picks the x2 set above 1080p, the x1 set otherwise, so art is
sampled ~1:1 without mipmaps (small node sprites carry `mip`: the game builds mipmaps for those).

## Step 1 — style exploration (done)
Mock-ups (inventory + legendary tooltip over a real town screenshot, 1600×900):
- `Y:\shots\uiart\style_A.png` — A: heavy forged iron, ember-gold inlay, Sun-Forge crest, ember stones, slate field.
- `Y:\shots\uiart\style_B.png` — B: ash-grey stone moulding, tarnished bronze caps with rosettes, votive candles and
  wax runs, ash-stained parchment field.
- `Y:\shots\uiart\style_C.png` — C: dark leather band with saddle stitching, iron straps and studs, lantern-window
  hasp, dark leather field.
(2x crops of each: `style_?_crop0..2.png`.)

**Chosen: A.** Why:
- Readability: the near-black slate field gives the highest contrast for 22–24 px parchment text and for the dim
  (secondary) text; B's parchment is necessarily lighter and warm-grey, which costs the dim text most; C's brown field
  merges with the game's warm firelit scenes behind semi-transparent HUD pieces.
- Hierarchy: A separates weights by construction, not by colour: window = wide moulding with a gold inlay line and cast
  corners, tooltip = thin moulding with a hairline and the rarity band, slot = rim with a hairline. In B the grey stone
  bar is as bright as the text and competes with it; in C the stitches turn into noise at 1080p.
- Identity: dead Sun-Forge crest (a rayed disc with an ember heart), ember stones as the only saturated accent, forged
  iron and soot — exactly the world's motifs; saturation stays reserved for gameplay (rarity, elements).
- Richness without noise: ornament sits on the frame (corners, crest), long edges carry one inlay line and a stud every
  128 design px.
B and C stay in `styles.py`: `render.ps1 -Style B -Pieces all` + `slice.py --style B` re-dresses the whole set.

## Step 2 — full asset set
(in progress — table of pieces and margins is kept in `Y:\tools\uiart\README.md` and at the end of this file)

## Step 3 — integration
- New: `UiArt.cs` (manifest, texture sets, draw helpers: Nine / Tile / Sprite / SpriteAt), `UiNine.cs` (a StyleBox that
  draws field tile + nine-slice + overlays + crest; shared instances).
- (in progress)

## Log
- 16:40 pipeline written, first renders (window + crest) OK at first run; Cycles CPU, 96 samples + OIDN,
  ~11 kpx/s (AO + bevel nodes in the shaders dominate).
- Lighting lesson: with a bright soft box every dark dielectric turns grey (4 % specular of a bright world). The world is
  now a narrow key lobe + weak skirt, dull materials have a low specular level, iron is 85 % metallic with a dark base.
- 17:30 three style kits rendered and composited; A chosen; A's corner casting, crest and sprig reworked (the first
  sprig had a closed curl that read as an eye).
