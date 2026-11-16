# UI frame art of 燼淵 — how it is made, how to regenerate it, how the game uses it

All interface chrome (window frames, panels, tooltips, buttons, tabs, slots, HUD rims, menu plates …) is raster art
rendered from real 3D models: bevelled mouldings, cast corners, wire filigree, rivets and faceted ember stones with
physically based materials, lit by a sun from the top-left, rendered orthographically by Cycles and sliced into
nine-slice textures by script. Nothing is hand-painted and no image generation is involved; every pixel can be
regenerated.

```
Y:\tools\uiart\kit.py        Blender toolkit: scene, light, geometry helpers, procedural PBR materials
Y:\tools\uiart\styles.py     style directions A / B / C (material roles, moulding profiles, motifs)
Y:\tools\uiart\pieces.py     every piece: kind, sizes, post-processing, model
Y:\tools\uiart\render.py     Blender entry point            (run it through render.ps1)
Y:\tools\uiart\render.ps1    wrapper around Y:\tools\blender\run_blender.ps1 (the throttled launcher: ONE run at a time)
Y:\tools\uiart\slice.py      renders → seamless nine-slices, shadows, glows, field tiles, x1 set, manifest.json
Y:\tools\uiart\mock.py       style mock-up (inventory + tooltip over a game screenshot)
Y:\tools\uiart\sheet.py      contact sheet of a folder of pieces
Y:\tools\uiart\doc.py        prints the piece table below
Y:\assets_src\ui_gen\render\<style>\*.png      raw renders (16-bit RGBA)
Y:\assets_src\ui_gen\explore\<style>\          sliced exploration kits used by the mock-ups
Y:\game\assets\ui\{x2,x1}\*.png + manifest.json  what the game loads (res://assets/ui)
```
`python.exe` below = Blender's bundled interpreter
`C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe` (numpy inside; Pillow in
`%LOCALAPPDATA%\AshenAbyssTools\pylibs`). It is NOT a Blender process, so slicing does not need the launcher.

## Regenerate
```powershell
# one or several pieces (always rendered), or everything:
powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\uiart\render.ps1 -Style A -Pieces btn,btn_hover
powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\uiart\render.ps1 -Style A -Pieces all          # ~8 min on 12 threads
#   -Samples 24 -Scale 0.5 = quick look (the slicer needs full-size renders); -SkipExisting = only missing ones
& "C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe" -I Y:\tools\uiart\slice.py --style A --pieces all
#   --pieces a,b updates only those entries of the manifest (add --no-fields to leave the field tiles alone)
powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\import.ps1                                       # Godot re-import
```
Never start blender.exe directly; render.ps1 goes through the machine-wide launcher (2 Blender processes at most).

## Change the look
- **Another style direction**: `render.ps1 -Style B -Pieces all`, then `slice.py --style B` (writes the same file names:
  the game needs no change). B = ash-grey stone + tarnished bronze + candle wax, C = dark leather + iron straps.
  The exploration only dressed B and C for window / tooltip / button / slot; the other pieces use the shared geometry
  with that style's materials and motif hooks, so review a contact sheet before shipping a switch
  (`python.exe -I sheet.py Y:\assets_src\ui_gen\render\B out.png`).
- **Materials**: `StyleA.mats` in styles.py (roles: `bar` mouldings, `plate` castings, `face*` text-carrying plates,
  `trim` gold inlay, `stud`, `stone` ember stones, `floor` recess floors; `finish()` adds the lit / dull variants).
- **Motifs**: `corner` (heavy bracket / light cap), `crest`, `crest_small`, `sprig` (flame filigree), `jewel`, `stud`,
  `edge` (periodic studs on long edges), `button`, `slot`.
- **A piece**: pieces.py. Sizes are x2 texture px (2 px = 1 design px at 1080p), all even. For a nine-slice the margins
  must cover: pad (shadow room) + corner ornament + `blend` px of plain moulding (the slicer cross-fades there).
  Pointed ends cannot tile vertically: make such a piece an `h3` (fixed height), not a `nine`.
- **Fields** (panel backgrounds): `FIELDS` in slice.py — a scanned CC0 texture mapped between two dark tones.
- Lighting lives in `kit.setup()` / `kit._world()`. Keep the world dim: a bright soft box turns every dark surface grey.

## How the game uses it (C#)
- `UiArt` (static): reads `res://assets/ui/manifest.json`, picks the x2 set above 1080p and the x1 set otherwise
  (`UiArt.FitWindow`, called by `Ui`), lazy textures, and draws without allocating:
  `UiArt.P(name)` → `Piece` (cache it in a field), `UiArt.Nine(canvasItem, piece, boxRect[, modulate])`,
  `UiArt.Sprite(canvasItem, piece, boxRect[, modulate])` (logical box stretched onto the rect),
  `UiArt.SpriteAt(canvasItem, piece, anchorPoint[, modulate, scale])`, `UiArt.SpriteCentered(...)`,
  `UiArt.Tile(canvasItem, UiArt.Field("slate"), rect)`, `UiArt.Icon(name)` (a texture that reports its design size:
  theme icons, TextureRects), `UiArt.Has(name)`. Rectangles are design px and name the piece's LOGICAL box: shadow
  and glow padding is drawn outside it. `Piece.BoxSize`, `Piece.InL/InT/InR/InB` give the sizes in design px.
- `UiNine : StyleBox`: field tile + frame (nine / h3 / sprite) + two tinted overlays + top / bottom ornament + flat
  `Back` colour; `Small` / `SmallBelow` switch to a lighter frame on small boxes. Shared instances only.
- `UiTheme`: `Art` (is the art installed), `ArtBox(frame, padX, padY[, field, fieldInset])` (cached UiNine),
  `ArtBox(key, () => new UiNine(...){...})`, `WindowBox` (heavy, crest, auto-compact below 620×400),
  `WindowPlainBox`, `PanelBox`, `WellBox`, `TipBox`, `TipBoxDim`, `PlaqueBox`, `DrawTipBand`, `SlotBox(rarity, hover, bad)`,
  `Frame(heavy)`, `Well()`, `Rule()`, `TabTone(button, active)`, `Box(bg, border, px, radius)` (tinted `boxline` frame
  for square boxes, flat for round ones). The shared Theme dresses Button, CheckBox, CheckButton, OptionButton,
  LineEdit, sliders, vertical scroll bars, TabBar, PopupMenu, ItemList / Tree, ProgressBar, separators from the art.
- `MenuKit` (CreditsData.cs): `Box`, `Glass`, `Btn`, `Entry`, `Check`, `Slider`, `FocusBox` use the art the same way.
- Without `res://assets/ui/manifest.json` everything falls back to the old flat style boxes (the game still runs).
- Export: `manifest.json` is read as a plain file — keep `*.json` in the export filter (as for `res://data`).

## Pieces
Sizes: "texture x2" is the PNG in `x2\` (the `x1\` one is exactly half). "Margins" are the nine-slice margins measured
inside the logical box; "pad" is the extra border the texture carries outside it (shadow / glow / ornaments).

| piece | kind | texture x2 (px) | logical box (design px) | margins L,T,R,B inside the box (design px) | pad (design px) | tile period (design px) | notes |
|---|---|---|---|---|---|---|---|
| `window` | nine | 672 x 672 | any (min 180 x 180) | 90, 90, 90, 90 | 14 | 128 x 128 |  |
| `crest` | sprite | 560 x 188 | 280 x 94 | - | 0 | - | anchor 140,50 |
| `modal` | nine | 328 x 328 | any (min 72 x 72) | 36, 36, 36, 36 | 14 | 64 x 64 |  |
| `crest_small` | sprite | 220 x 100 | 110 x 50 | - | 0 | - | anchor 55,20 |
| `panel` | nine | 256 x 256 | any (min 48 x 48) | 24, 24, 24, 24 | 8 | 64 x 64 |  |
| `well` | nine | 112 x 112 | any (min 24 x 24) | 12, 12, 12, 12 | 0 | 32 x 32 |  |
| `tooltip` | nine | 296 x 308 | any (min 60 x 66) | 30, 36, 30, 30 | 12 | 64 x 64 |  |
| `tooltip_band` | h3 | 104 x 28 | any width x 8 | 7, 0, 7, 0 | 3 | 32 | neutral: tint with Modulate |
| `nameplate` | h3 | 192 x 120 | any width x 48 | 26, 0, 26, 0 | 6 | 32 |  |
| `plaque` | nine | 168 x 80 | any (min 40 x 20) | 20, 10, 20, 10 | 6 | 32 x 8 | centre drawn (tiled) |
| `tracker` | nine | 144 x 80 | any (min 32 x 16) | 16, 8, 16, 8 | 4 | 32 x 16 | centre drawn (tiled); translucent fill |
| `title_rule` | sprite | 1160 x 88 | 564 x 28 | - | 8 | - | anchor 290,22; glow |
| `btn` | nine | 152 x 112 | any (min 32 x 28) | 16, 14, 16, 14 | 6 | 32 x 16 | centre drawn (tiled) |
| `btn_hover` | nine | 152 x 112 | any (min 32 x 28) | 16, 14, 16, 14 | 6 | 32 x 16 | centre drawn (tiled) |
| `btn_pressed` | nine | 152 x 112 | any (min 32 x 28) | 16, 14, 16, 14 | 6 | 32 x 16 | centre drawn (tiled) |
| `btn_disabled` | nine | 152 x 112 | any (min 32 x 28) | 16, 14, 16, 14 | 6 | 32 x 16 | centre drawn (tiled) |
| `btn_focus` | nine | 152 x 112 | any (min 32 x 28) | 16, 14, 16, 14 | 6 | 32 x 16 | glow |
| `tab` | nine | 152 x 104 | any (min 32 x 24) | 16, 14, 16, 10 | 6 | 32 x 16 | centre drawn (tiled) |
| `tab_hover` | nine | 152 x 104 | any (min 32 x 24) | 16, 14, 16, 10 | 6 | 32 x 16 | centre drawn (tiled) |
| `tab_on` | nine | 152 x 104 | any (min 32 x 24) | 16, 14, 16, 10 | 6 | 32 x 16 | centre drawn (tiled) |
| `close` | sprite | 88 x 88 | 36 x 36 | - | 4 | - |  |
| `close_hover` | sprite | 88 x 88 | 36 x 36 | - | 4 | - |  |
| `close_pressed` | sprite | 88 x 88 | 36 x 36 | - | 4 | - |  |
| `check_off` | sprite | 56 x 56 | 24 x 24 | - | 2 | - |  |
| `check_on` | sprite | 56 x 56 | 24 x 24 | - | 2 | - |  |
| `radio_off` | sprite | 56 x 56 | 24 x 24 | - | 2 | - |  |
| `radio_on` | sprite | 56 x 56 | 24 x 24 | - | 2 | - |  |
| `toggle_off` | sprite | 96 x 56 | 44 x 24 | - | 2 | - |  |
| `toggle_on` | sprite | 96 x 56 | 44 x 24 | - | 2 | - |  |
| `slider_track` | h3 | 64 x 32 | any width x 10 | 5, 0, 5, 0 | 3 | 16 |  |
| `slider_fill` | h3 | 56 x 32 | any width x 10 | 3, 0, 3, 0 | 3 | 16 | glow; neutral: tint with Modulate |
| `slider_grab` | sprite | 60 x 60 | 24 x 24 | - | 3 | - |  |
| `slider_grab_hover` | sprite | 60 x 60 | 24 x 24 | - | 3 | - |  |
| `scroll_track` | v3 | 24 x 64 | 12 x any height | 0, 8, 0, 8 | 0 | 16 |  |
| `scroll_grab` | v3 | 24 x 44 | 12 x any height | 0, 7, 0, 7 | 0 | 8 |  |
| `scroll_grab_hover` | v3 | 24 x 44 | 12 x any height | 0, 7, 0, 7 | 0 | 8 |  |
| `input` | nine | 88 x 88 | any (min 24 x 24) | 12, 12, 12, 12 | 2 | 16 x 16 | centre drawn (tiled) |
| `input_focus` | nine | 88 x 88 | any (min 24 x 24) | 12, 12, 12, 12 | 2 | 16 x 16 | centre drawn (tiled) |
| `arrow_down` | sprite | 36 x 36 | 14 x 14 | - | 2 | - |  |
| `arrow_right` | sprite | 36 x 36 | 14 x 14 | - | 2 | - |  |
| `row` | nine | 88 x 48 | any (min 24 x 12) | 12, 6, 12, 6 | 2 | 16 x 8 | centre drawn (tiled) |
| `row_hover` | nine | 88 x 48 | any (min 24 x 12) | 12, 6, 12, 6 | 2 | 16 x 8 | centre drawn (tiled) |
| `row_on` | nine | 88 x 48 | any (min 24 x 12) | 12, 6, 12, 6 | 2 | 16 x 8 | centre drawn (tiled) |
| `slot` | sprite | 152 x 152 | 64 x 64 | - | 6 | - |  |
| `slot_rarity` | sprite | 152 x 152 | 64 x 64 | - | 6 | - | glow; neutral: tint with Modulate |
| `slot_rarity_hi` | sprite | 152 x 152 | 64 x 64 | - | 6 | - | glow; neutral: tint with Modulate |
| `slot_hover` | sprite | 152 x 152 | 64 x 64 | - | 6 | - | glow |
| `slot_selected` | sprite | 152 x 152 | 64 x 64 | - | 6 | - | glow |
| `skill_slot` | sprite | 176 x 176 | 72 x 72 | - | 8 | - |  |
| `skill_slot_pressed` | sprite | 176 x 176 | 72 x 72 | - | 8 | - |  |
| `skill_slot_off` | sprite | 176 x 176 | 72 x 72 | - | 8 | - | neutral: tint with Modulate |
| `socket` | sprite | 60 x 60 | 24 x 24 | - | 3 | - | mipmaps |
| `gem_slot` | sprite | 96 x 96 | 36 x 36 | - | 6 | - | mipmaps |
| `pn_normal` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps |
| `pn_normal_lit` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; glow |
| `pn_normal_fill` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; neutral: tint with Modulate |
| `pn_magic` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps |
| `pn_magic_lit` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; glow |
| `pn_magic_fill` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; neutral: tint with Modulate |
| `pn_rare` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps |
| `pn_rare_lit` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; glow |
| `pn_rare_fill` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; neutral: tint with Modulate |
| `pn_legend` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps |
| `pn_legend_lit` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; glow |
| `pn_legend_fill` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; neutral: tint with Modulate |
| `pn_socket` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps |
| `pn_socket_lit` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; glow |
| `pn_socket_fill` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; neutral: tint with Modulate |
| `pn_gate` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps |
| `pn_gate_lit` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; glow |
| `pn_gate_fill` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; neutral: tint with Modulate |
| `pn_start` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps |
| `pn_start_lit` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; glow |
| `pn_start_fill` | sprite | 128 x 128 | 48 x 48 | - | 8 | - | mipmaps; neutral: tint with Modulate |
| `node_oct_locked` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps |
| `node_oct_avail` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps; glow |
| `node_oct_owned` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps; glow |
| `node_round_locked` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps |
| `node_round_avail` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps; glow |
| `node_round_owned` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps; glow |
| `node_key_locked` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps |
| `node_key_avail` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps; glow |
| `node_key_owned` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps; glow |
| `node_hub_off` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps |
| `node_hub_on` | sprite | 192 x 192 | 72 x 72 | - | 12 | - | mipmaps; glow |
| `rule` | h3 | 128 x 16 | any width x 4 | 22, 0, 22, 0 | 2 | 16 |  |
| `rule_gem` | sprite | 200 x 40 | 96 x 16 | - | 2 | - | anchor 50,10 |
| `header_line` | h3 | 96 x 24 | any width x 8 | 18, 0, 10, 0 | 2 | 16 |  |
| `bullet` | sprite | 36 x 36 | 14 x 14 | - | 2 | - | mipmaps |
| `bullet_gold` | sprite | 36 x 36 | 14 x 14 | - | 2 | - | mipmaps |
| `corner_orn` | sprite | 120 x 120 | 56 x 56 | - | 2 | - |  |
| `banner` | h3 | 208 x 96 | any width x 36 | 30, 0, 30, 0 | 6 | 32 |  |
| `hud_plate` | nine | 336 x 120 | any (min 84 x 24) | 42, 12, 42, 12 | 10 | 64 x 16 | centre drawn (tiled) |
| `globe_rim` | sprite | 464 x 464 | 172 x 172 | - | 30 | - |  |
| `xp_frame` | h3 | 160 x 44 | any width x 14 | 20, 0, 20, 0 | 4 | 32 |  |
| `buff_frame` | sprite | 112 x 112 | 48 x 48 | - | 4 | - |  |
| `boss_frame` | h3 | 428 x 120 | any width x 40 | 65, 0, 65, 0 | 10 | 64 |  |
| `target_frame` | h3 | 136 x 56 | any width x 22 | 15, 0, 15, 0 | 3 | 32 |  |
| `boss_stagger` | h3 | 120 x 36 | any width x 12 | 11, 0, 11, 0 | 3 | 32 |  |
| `plate_bar` | h3 | 72 x 32 | any width x 12 | 8, 0, 8, 0 | 2 | 16 |  |
| `minimap_ring` | sprite | 576 x 576 | 236 x 236 | - | 26 | - |  |
| `keycap` | nine | 60 x 60 | any (min 16 x 16) | 8, 8, 8, 8 | 3 | 8 x 8 | centre drawn (tiled) |
| `menu_plate` | h3 | 248 x 144 | any width x 56 | 38, 0, 38, 0 | 8 | 32 |  |
| `menu_plate_hover` | h3 | 248 x 144 | any width x 56 | 38, 0, 38, 0 | 8 | 32 |  |
| `card` | nine | 256 x 256 | any (min 48 x 48) | 24, 24, 24, 24 | 8 | 64 x 64 |  |
| `card_on` | nine | 256 x 256 | any (min 48 x 48) | 24, 24, 24, 24 | 8 | 64 x 64 |  |
| `emblem` | sprite | 1280 x 440 | 620 x 200 | - | 10 | - | anchor 320,110 |
| `boxline` | nine | 56 x 56 | any (min 16 x 16) | 8, 8, 8, 8 | 2 | 8 x 8 | neutral: tint with Modulate |
