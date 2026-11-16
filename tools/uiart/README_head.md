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

