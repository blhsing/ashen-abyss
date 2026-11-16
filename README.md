# 燼淵 Ashen Abyss

An original dark-fantasy action RPG in Traditional Chinese (zh-TW), built with Godot 4.7 (.NET / C#).
The Sun-Forge that warmed the world has gone out; the hero carries the last embers through seven regions to the abyss
beneath it.

**Version 0.1.0 is a playable prototype.** The campaign can be played from the first region to the ending, but many
parts are still in production (see *Status* below).

## Playing the prototype (Windows 10/11, 64-bit)

1. Download every part of the game build from the
   [0.1.0 release](../../releases/tag/v0.1.0): `AshenAbyss-0.1.0-win64.7z.001`, `.002`, … (GitHub limits a release
   file to 2 GB, so the build is split).
2. Put all parts in one folder and open the `.001` file with [7-Zip](https://www.7-zip.org/) (it joins the parts).
3. Extract, then run `AshenAbyss.exe`. The game starts in fullscreen; it keeps only one copy running at a time.

**Graphics:** a GPU with Vulkan 1.3 (NVIDIA GeForce GTX 10 series or newer, AMD Radeon RX 5000 / Radeon 680M or newer,
Intel Arc / Iris Xe) and an up-to-date driver. If Vulkan cannot start, the game falls back to Direct3D 12 by itself.
On laptops with two GPUs the game asks Windows for the high-performance one. 8 GB of system memory and 4 GB of video
memory or more are recommended; 設定 → 畫質 lowers the cost on smaller GPUs.

**Where things are kept**

| What | Where |
|---|---|
| saves (8 slots) and settings | `%APPDATA%\AshenAbyss\saves`, `%APPDATA%\AshenAbyss\settings.json` |
| logs (for bug reports) | `%APPDATA%\AshenAbyss\logs` |
| screenshots (PrtSc or F12) | `Pictures\AshenAbyss` |

## Controls

| Input | Action |
|---|---|
| Left mouse | move / attack / talk / pick up |
| Right mouse, 1–4 | skills (assign them in the skill page) |
| Space | evade |
| Q | potion |
| I or C | inventory and character |
| K / P | skills / paragon (燼紋) |
| M or Tab / J | map / quest log |
| Alt (hold) | show all loot labels |
| Esc | close a panel / pause menu |
| PrtSc or F12 / F11 | screenshot / fullscreen on-off |
| Shift + left click (bag) | mark as junk · Ctrl + left click (bag): drop on the ground |

Alt-Tab pauses the game.

## Building from source

The repository holds the code, data, shaders, scenes, tools and documents. Binary assets (models, textures, audio,
fonts, icons) are not in the repository; they are attached to each release.

1. Install the [Godot 4.7.2 .NET](https://godotengine.org/) editor and the .NET 10 SDK.
2. Clone this repository.
3. Download `AshenAbyss-0.1.0-assets.7z.001`, `.002`, … from the release and extract them into the repository root
   (they fill `game/assets/…`; the matching `.import` files are already in the repository).
4. Open `game/project.godot` in the editor (the first import takes several minutes), or run
   `tools\import.ps1` and then `tools\run.ps1`.
5. `tools\install.ps1` exports a release build and installs it with a Start-menu shortcut.

The scripts in `tools\` expect the project at a short path: they map it to the drive `Y:` with `subst` (Windows
path-length limits). `ARCHITECTURE.md` describes the code, the test switches and the tool scripts.

## Status of 0.1.0

Done: 8 classes, 7 regions and a town, 14 side dungeons, the story campaign with 7 region bosses, items with affixes,
legendary powers, sockets and crafting, skill trees, paragon boards, world tiers, the rift, 8 save slots, raster icons,
dark lighting, generated music (15 of 19 tracks) and voice (25 of 188 lines).

In production: the full bestiary (about 17 of 129 monster types appear in play today), named elites and the
stronghold, world and pinnacle bosses, non-humanoid creatures, the remaining music and voice, the ornate UI frame
pass. Known issues are listed in `QA_NOTES.md`.

## Credits and licences

Third-party assets and tools, with their licences, are listed in [`credits/`](credits/) (CC0 textures and models,
SIL OFL fonts, CC BY 3.0 icons with attribution, MIT / Apache-2.0 generative models). The music and voices were
generated locally with open models; no voice in the game imitates a real person.

No licence has been chosen yet for the game's own code and content; until one is added, all rights are reserved.

This is an independent project. It is not affiliated with or endorsed by any other game or publisher.
