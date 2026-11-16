# 燼淵 icon production log (icon art lead)

Goal: every icon in the game is raster art — Blender renders of real textured models for things, image generation
for actions/abstractions — under `res://assets/icons/r/<category>/<id>.png`. Inventory: `Y:\tools\icons\icons.json`,
style bible: `Y:\tools\icons\STYLE.md`, how to regenerate: `Y:\tools\icons\README.md`.

## Image generation route (WORKS — verified 2026-11-15 16:24)

No install was needed: the Codex desktop app already ships the official Codex CLI and is logged in with the user's
ChatGPT account (`codex login status` → "Logged in using ChatGPT"; feature `image_generation` = stable, true).

- CLI: `%LOCALAPPDATA%\OpenAI\Codex\bin\<hash>\codex.exe` (today `9e1da79431da526b`, codex-cli
  0.167.0-alpha.4.1; the hash folder changes with app updates — take the newest `codex.exe` under `...\Codex\bin\*`).
- Official doc (learn.chatgpt.com/codex/image-generation): in the CLI, put `$imagegen` in the prompt to call the
  image-generation skill; the built-in tool uses `gpt-image-2` and counts toward the general Codex usage limits
  (image turns use the allowance 3–5× faster than ordinary turns). Bundled skill text: `~\.codex\skills\.system\imagegen\SKILL.md`.
- Working non-interactive command (one image per call):
  ```
  codex.exe exec --skip-git-repo-check --ephemeral -s read-only -C <EMPTY REAL FOLDER> --json "$imagegen <prompt>" < NUL
  ```
  The first JSONL line is `{"type":"thread.started","thread_id":"<id>"}`; the picture lands in
  `%USERPROFILE%\.codex\generated_images\<thread_id>\exec-<uuid>.png`. Copy it out (the CLI does not take an output path).
- Pitfalls found:
  1. stdin MUST be redirected (`< NUL` / `< /dev/null`), otherwise it prints "Reading additional input from stdin..." and waits.
  2. `-C` and `-o` must be REAL paths. The `Y:` subst drive is rejected ("path contains a reparse point"). Use an empty
     folder such as `%LOCALAPPDATA%\AshenAbyssTools\icons_gen_work\empty`
     (also keeps project files out of the model's reach: prompts carry only the picture description).
  3. stderr is noisy but harmless: `rmcp::transport::worker … HTTP 401 invalid_issuer` and `Failed to refresh token
     status=401` come from the app's plugin/MCP transports; exit code is 0 and the image is written.
  4. Output is RGB without alpha, 1254×1254 for a square request (1672×941 landscape). For cut-outs ask for a
     "pure black background" and derive alpha afterwards (`Y:\tools\icons\post.py`), or ask for a transparent background.
  5. One call ≈ 85 s. To spare the user's allowance generate SHEETS: one image holding a 3×3 grid of nine icons on
     black with wide gaps (prompt wording in STYLE.md), then slice. The model kept the grid clean on the first try.
  6. `--ephemeral` keeps the run out of the user's Codex session history; the image is still saved.
- Driver with cache (never regenerates an existing sheet): `Y:\tools\icons\imagegen.py` (see README.md).
  Every result is stored with its prompt, date and thread id under `Y:\assets_src\icons_gen\`.

## Requests for the UI frame artist
(filled in once Icons.cs exposes the raster API — see below in this file)

## Progress log
- 16:25 inventory sources read (Icons.cs, Items, SkillsData, HudBuffs, HudMinimap, MapUi, Paragon, MonstersData, SkillTreeUi);
  imagegen probe sheet OK (`Y:\assets_src\icons_gen\_probe\probe1_sheet.png`).
- 18:20 MAP MARKERS (marker artist): all 30 ids built in `Y:\tools\blender\icons\props_markers.py` (helpers in `props_markers_kit.py`)
  -> masters `res://assets/icons/r/marker/<id>.png`: quest objective boss elite bossdoor entrance exit stronghold waypoint beacon
  brazier portal riftstone event altar shrine chest stash idol tierstatue prisoner elder smith merchant occultist jeweler villager npc start hero.
  Sheets: `Y:\shots\icons\sheet_markers.png` (64 px), `sheet_markers32.png` (32 px), `sheet_markers128.png`; also judged at 22 / 16 px on dark, ash and moss grounds.
  Method: table-top tokens 0.3-0.6 m so the kit grain reads; colour code by display-true glows (`K.glow / fire_mat / gradient / radial / facet`: only
  the dominant channel goes above 1 -- `I.emit(c, 3-6)` clips to pale yellow under the Standard view), opaque pointed flame tongues, `fx_` point lamps for spill.
  Skull (boss / elite): `I.head_of("mon_skeleton")` arrives with kettle hat, straps, rag and neck bones -> stripped by material and connected piece, bone
  re-skinned with the kit bone look (the baked texture keeps the hat's shadow). Kit `rusty_iron` reads as copper and kit rock as black: own `iron()` / `K.stone()`.
  Weak spots: smith (darkest token; at 16 px carried by its tan block), start (grey, no glow by design), entrance (the stair is shown in profile inside the
  arch; slightly kiln-like at 128 px), beacon / altar / brazier part by silhouette more than hue, boss crown is dark on the dark panel (eyes + jewel carry
  the red), hero's highlight is baked upper-left and turns with the arrow. Scratch files: none in the Y:\ root any more (collision with another
  artist's patchN.py reported to the lead and resolved).
- 18:35 ARMOUR / JEWELLERY / UNIQUES (armour artist): 40 ids -> `res://assets/icons/r/item/<id>.png`: helm_1 2 4 5, chest_1 2 3 5, gloves_1-5, pants_1-5,
  boots_1-5, amulet_1-5, ring_1 2 3 5, u_regret u_stargazer u_owlbeak u_reaper u_snuffer u_shovel u_candle u_emberheart (helm_3, chest_4, ring_4 = lead's, not overridden).
  Code: `tools\blender\icons\props_armor.py` (helpers, helms, chests) + `props_armor_body.py` (gloves / pants / boots), `props_armor_jewel.py`, `props_armor_unique.py`.
  Sheets: `Y:\shots\icons\sheet_armor.png` (25, tier order), `sheet_jewel.png`, `sheet_unique.png` (64 px), `sheet_armor160.png` (all 43 at 160 px).
  Reusable: `Shell` (stacked super-elliptic rings, metric UVs; mesh / band / line / ring / rivets / put / rim / stitch, front openings, radial bump), `mail()`, `hot()`, `seam()`, `void()`.
  Findings: kit metal's edge-wear mask (inside AO 6 mm) makes any shell thinner than ~5.5 mm one bright worn edge (dark plate turned to polished steel) -> dark / iron shells
  are 6.2 mm (PI.pauldron's 4 mm is why chest_4's pauldrons are greyer than its torso); EMBER x 3-4 clips to yellow -> seams use (1, .13, .012) x 3, so chest_4's seams are
  yellower than helm_4 / pants_4 / boots_4; `PI._dark_inside` is glossy -> matte `void()`; kit rusty_iron reads brown -> `iron2()`, kit steel rusts between lames -> `steel3()`.
  Weak spots: u_snuffer small in frame (tall crest, smoke thread nearly invisible); u_candle flame small at 64 px; gloves_4 / 5 fill less of the cell; boots_2 toe caps
  still rust-orange. Scratch: `tools\blender\icons\scratch\armour\armour_*.py` (one-shot patches, already applied -- do not re-run).
- 18:56 CURRENCY / MENU OBJECTS / PARAGON TOKENS / CRESTS (tokens artist): 41 ids, all in `Y:\tools\blender\icons\props_tokens.py`.
  misc (13, 256 px): gold scrap dust core stash chest key quest map inventory settings lock paragon · paragon (19, 256 px): node_start node_normal node_magic
  node_rare node_legendary node_socket node_gate + glyph stones g_steel g_kiln g_bastion (ember red-orange) g_orbit g_tide g_thrall (blue) g_eye g_gale g_venom (green)
  g_vigil g_surge g_reaper (violet) · crest (9, raw 1024 -> 512 px): emberblade astromancer hunter soulbinder wildsoul forgebody lampknight pactbinder title.
  Sheets: `Y:\shots\icons\sheet_misc.png`, `sheet_paragon.png`, `sheet_crest.png` (64 px), `sheet_crest160.png` (160 px).
  Method: every part's transform is baked into its mesh (`xf`) so the object-space kit looks differ from part to part; glyphs are real grooves (EXACT boolean cut +
  a glowing bed, `check_cuts` switches off a cut that eats the stone); crests share one `medallion()` (iron field, enamel ring, gold rim, bezel stone) with the hero's
  wpn_3 laid across it by `mount()`; glows are hue-safe (`lum` / `emberstone` / `grain_glow`: only the strongest channel goes above 1, and the weaker ones are set LOWER than
  the target because post.py's bloom screens a wide glow onto itself and lifts them -- e.g. ember (1, .17, .02) lands as orange, (1, .42, .08) x 3 lands as yellow-white).
  Toolkit problems worked round in my file: (1) registry keys are ids only and the marker set also has quest / chest / stash -> mine are registered as `./quest`,
  `./chest`, `./stash` (same output file; render with `rb.ps1 ./chest ...` or `misc:`); (2) `iconlib.extrude` closes the notches of CONCAVE outlines -> own `prism()`
  (mathutils tessellation); (3) `I.tube(closed=True)` leaves an unwelded seam -> `I.weld` before using it as a boolean cutter; (4) boolean `use_self=True` crashed
  Blender 5.2 (access violation, stdout of the whole batch lost) -> not used; (5) a 1024 px crest takes ~85 s, nine in one rb.ps1 call exceed a 10-minute tool timeout
  -> render crests four at a time; (6) the kit's edge-wear mask turns thin plate (< 6 mm) into bright steel -> `edge=0.2-0.3` on the scrap.
  Weak spots: scrap is a busy heap (blade tip + rust colour carry it); g_venom's fang reads as a pennant with a drop; g_bastion has one cut on fallback (bed lies in
  the surface); the map's ember mark is faint at 64 px; hunter / soulbinder / wildsoul are three greens (teal, pale sage, leaf -- the weapons part them), emberblade
  amber vs lampknight pale gold likewise (the shield parts them); title is toothed like the settings cog at 64 px (wedge rays + ember heart tell it apart at >= 128).
  Scratch: `tools\blender\icons\scratch\tokens\tokens_*.py` (one-shot patches / code chunks, already applied -- do not re-run).
- 18:30 MAP MARKERS paused on request, state: all 30 marker ids are rendered, post-processed and on the final sheets (sheet_markers.png 64 px, sheet_markers32.png 32 px,
  sheet_markers128.png); nothing is queued or half-written. Nothing is left to build; optional polish only (smith brightness at 16 px, entrance at 128 px).
  Current scripts: Y:\tools\blender\icons\props_markers.py + props_markers_kit.py (no other marker files; no scratch files in the Y:\ root).

## STATE AT THE PAUSE — 2026-11-15 19:15 (icon lead)
**Everything the inventory asks for exists as a finished raster master.** `Y:\tools\icons\icons.json` (written by
`Y:\tools\icons\inventory.py`, which also verifies size/mode of every file): 372 entries + the 9 icons of the probe
sheet `hunter_a` = **381 PNGs** under `res://assets/icons/r/`, 0 missing, 0 unreadable.

| category | count | method | | category | count | method |
|---|---|---|---|---|---|---|
| item (40 weapons, 35 armour/jewellery tiers, 18 gems, 8 uniques) | 101 | Blender | | skill | 64 | imagegen |
| misc (potion, gold, scrap, dust, core, chest, key …) | 14 | Blender | | passive | 32 | imagegen |
| marker (map / minimap tokens) | 30 | Blender | | keypassive | 16 | imagegen |
| paragon (7 node tokens, 12 glyph stones) | 19 | Blender | | spec | 24 | imagegen |
| crest (8 classes + title, 512 px) | 9 | Blender | | status 13 · buff 5 · stat 30 | 48 | imagegen |
| | | | | affix 16 · shrine 6 · hud 2 | 24 | imagegen |
Blender 173, image generation 208 (24 sheets of nine, ~90 s each; all cached with prompt/date/thread id in
`Y:\assets_src\icons_gen\sheets\`; 8 unused crest drafts in `Y:\assets_src\icons_gen\crestgen\`).

**Code:** `Icons.cs` compiles (check.ps1 -Files Icons.cs: 0 errors in the file). It now resolves raster first and
falls back to the old vector glyph (one `[icons]` warning per fallback): `ForItem` (kind + base tier from power /125,
unique id, weapon type by class, gem kind + tier), `ForSkill` (skill id), `ForKind` (tier-2 ghost), `Get` (raster id
"cat/id", or any old glyph name / shorthand through the `Legacy` table — every name the panels pass today is mapped),
new `Raster`, `IsRaster`, `Tint`, `ForPassive`, `ForSpec`, `ForStatus`, `ForMarker`, `ForAffix`, `ForClass`,
`TitleEmblem`, `ForGlyph`, `ForParagonNode`, `ForBuff`. NOT yet run in the game (no test run was made).
**Loading:** `res://assets/icons/` carries a `.gdignore`, so the PNGs are NOT imported by Godot; Icons.cs reads the
PNG bytes itself (res:// in a dev run, `<exe folder>\assets\icons\r\…` in an installed build — the same route as the
vector set) and builds mipmaps at run time. **install.ps1 must copy `assets\icons\r` next to the exe** like `gi`.

**Lead's quality notes — OPEN (nothing of this is done yet):**
1. Gems (`item/gem_*`, 15 cut stones) still read as flat-shaded plastic. Plan: new `props_gems.py` — real refraction
   (transmission 1, IOR ~2) lit by hidden light cards (`visible_camera=False`), proper brilliant / step / trillion /
   crystal cuts with more facets, inner glow core, inclusions, glints on tier 3. `gem_skull_*` still wears the
   skeleton's kettle hat: reuse `props_markers.skull()` (bare skull, kit bone look).
2. Potion glass: thickness, liquid touching the glass, meniscus, bubbles, dust on the glass, wax over the cork.
3. Worn metal: `helm_3`, `chest_4`, `ring_4` (mine, thin shells → whole object reads as one worn edge, "smooth clay")
   were handed to the armour artist (agent a4d82283fe9dcef3f) together with a wear pass on jewellery (build several
   times life-size in the mesh), `gloves_4/5` fill, `u_snuffer`, `u_candle`, `boots_2` — that follow-up had only just
   started when the pause came; check `props_armor*.py` parse and re-render before trusting it.
4. Thin weapons (greatsword, scythe, staff rows of `sheet_weapons.png`) are still too thin/dark at 64 px; tiers 3/4/5
   of club / crossbow are too alike and the ember / class glows clip to yellow-white. Plan: hue-safe emission (only
   the dominant channel above 1), more widening, a faint halo behind the weapon (post.fit must then take the bounding
   box at alpha > 0.5), re-render `wpn_*`.
5. Not yet reviewed by me on contact sheets: armour / jewel / unique / misc / paragon / crest / marker sheets of the
   helpers (their own notes above list weak spots) and the generated sheets of soulbinder, wildsoul, forgebody,
   lampknight, pactbinder, passives, specs, affixes (first four sheets looked very good at 64 px: `sheet_gen1.png`).

**Helpers:** armour a4d82283fe9dcef3f (follow-up interrupted, see 3), tokens a7bc805c7cd7775cb (done), markers
a1ae4019066d8ed4f (done). Scratch rule: only `Y:\tools\blender\icons\scratch\<role>\`, nothing in the Y:\ root.

**Next steps when resumed:** (a) points 1–4 above, then all contact sheets `Y:\shots\icons\sheet_*.png` by category;
(b) run the game through run.ps1 (inventory, vendor, skill tree ×3, action bar, buff row, paragon, maps, class
selection, HUD), confirm zero `[icons]` warnings; (c) README.md + `Y:\credits\icons.md`; (d) the requests below.

## Requests for the UI frame artist (exact changes; Icons.cs side is ready)
Raster icons are full-colour art: multiplying them by an element / rarity / class colour stains them. Icons.cs
exposes `Icons.IsRaster(tex)` and `Icons.Tint(tex, wanted)` (vector glyph → `wanted` unchanged; raster → neutral
white that keeps only the brightness and alpha of `wanted`, so dimmed / locked / ghost states still work).
Please wrap the modulate at every place an icon is tinted:
- `UiTheme.ItemSlot`: `Modulate = Icons.Tint(tex, ItemTint(it))` (tex = `Icons.ForItem(it)`); `UiTheme.Icon(...)`: same.
- `HudActionSlot.Bind(tex, tint)`: `_tint = Icons.Tint(tex, tint)`; potion / evade slots likewise (`heart-bottle` → potion flask, `dodging` → evade already resolve to raster).
- `HudBuffs._Draw`: `DrawTextureRect(en.Tex, r.Grow(-4f), false, Icons.Tint(en.Tex, en.Tint))`; and in `IconFor`:
  `t = skill != null ? Icons.ForSkill(skill) : Icons.ForBuff(d) ?? Icons.Get(GenericIcon(d));` (shrine blessings get their own icons);
  afflictions may use `Icons.ForStatus(id)` instead of the name table.
- `HudMinimap`: `st.Tex = Icons.ForMarker(kind) ?? Icons.Get(s.Icon)` and draw with `Icons.Tint(st.Tex, col)`.
- `SkillTreeUi`: `IconAt(...)` / `DrawTextureRect(n.Icon, …, ic)` → colour `Icons.Tint(n.Icon, ic)`; passive nodes
  `Icons.ForPassive(p)` (minor and key passives have their own icons: `passive/<id>`, `keypassive/<id>`), specs `Icons.ForSpec(sp.Id)`.
- `InventoryUi.FallbackSlot`, `VendorUi.KindIcon`, `MainMenu` signature-skill icons (`_skIcon[i].Modulate`): wrap with `Icons.Tint`.
- New art that has no call site yet: class crests `Icons.ForClass(ClassId)` and `Icons.TitleEmblem` (512 px, class
  selection / title), elite affixes `Icons.ForAffix(id)` (nameplates / boss bar), paragon `Icons.ForParagonNode(type)`
  and `Icons.ForGlyph(id)` (ParagonUi draws nodes and glyph rows from primitives today), map markers in MapUi
  (`MapGlyphs.Draw` → `Icons.ForMarker(kind)` on its dark disc; markers already carry a soot outline).
- Icons are borderless with transparent backgrounds and fill 86 % of their square: 3–5 px of padding inside a slot frame is enough.
- 19:00 PAUSE (tokens artist): all 41 ids (misc 13, paragon 19, crest 9) are rendered and posted from the CURRENT `props_tokens.py`; masters verified readable, final sheets
  sheet_misc / sheet_paragon / sheet_crest / sheet_crest160 are up to date. No Blender run of mine is queued or running. Nothing is left to do except optional polish
  (scrap heap, g_venom fang, g_bastion's one fallback cut, map ember mark). `scratch\tokens\tokens_*.py` are applied one-shot patches -- do not re-run.
- 18:50 ARMOUR artist PAUSED (on request). Rendered + mastered and current: all 40 of my ids (+ the lead's helm_3 / chest_4 / ring_4 untouched); sheets `sheet_armor.png`,
  `sheet_jewel.png`, `sheet_unique.png`, `sheet_armor160.png` match the scripts `props_armor.py`, `props_armor_body.py`, `props_armor_jewel.py`, `props_armor_unique.py` as they stand.
  NOT started (no file changed, nothing queued): the lead's follow-up pass = (1) re-register helm_3 sallet / chest_4 / ring_4 in my modules; (2) wear pass: own-keyed kit metals
  + a scratch / pit / patina layer in props_armor.py, jewellery baked several times life-size (rings ~x14, amulets ~x3) with beaded / engraved detail; (3) gloves_4 shorter
  vambrace, gloves_5 without the long cuff tab, squat u_snuffer without smoke, opaque u_candle flame that casts no light, iron-grey boots_2 toe caps; (4) fill / nofit check; then re-render all 43.
- 19:30 correction to point 3 of the pause state (icon lead): the armour artist confirms its follow-up pass was NOT started —
  `props_armor*.py` are unchanged and match the sheets on disk; `helm_3`, `chest_4`, `ring_4` are still the lead's versions in
  `props_items.py`. The whole follow-up (takeover of those three, wear pass, weak-spot fixes, fill / nofit check) is still to do.
