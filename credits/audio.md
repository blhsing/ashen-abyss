# Audio credits (燼淵 Ashen Abyss)

All music in the game is an original composition written for this project, and every sound effect and ambience
loop is synthesised by the project's own audio tool (`Y:\tools\audio`, C# / .NET 10). No third-party recordings,
sample packs, loops or existing melodies are used. The only third-party material that ends up in shipped audio is
the instrument sample set of one SoundFont, used to render the sampled instruments in the music.

| section | files | what it is | third-party content |
|---|---|---|---|
| sound effects | 347 WAV, 167 groups | `game/assets/audio/sfx/<group>_<n>.wav` | none (own DSP synthesis), except the two stingers below |
| ambience | 9 OGG loops | `game/assets/audio/amb/amb_<zone>.ogg` | none (own DSP synthesis) |
| music | 19 OGG | `game/assets/audio/music/<name>.ogg` | SoundFont instrument samples (below) |

Measured levels, durations, loop flags and exact sample counts for every file are in
`game/assets/audio/manifest_audio.json`; the analysis table is `tools/audio/analysis.txt`.

## SoundFont — MuseScore_General.sf2

| | |
|---|---|
| file | `Y:\assets_src\soundfont\MuseScore_General.sf2` (215 614 036 bytes, version 0.2, 13 May 2020) |
| SHA-256 | `EE51D2C4B1525E70F19A45909C4FD7A2E26D91D115FA89DBF5A6BC413D8B9BF3` |
| source | MuseScore's official SoundFont distribution, https://ftp.osuosl.org/pub/musescore/soundfont/MuseScore_General/ |
| licence | MIT |
| licence text | `Y:\assets_src\soundfont\MuseScore_General_License.md` (authorship, the original Fluid README and the full MIT text), kept next to the file together with `MuseScore_General_Readme.md` and `MuseScore_General_Sample_Sources.csv` |

Authors and copyright notices (the licence asks that these be included in any derivative work):

- FluidR3 (original version) by Frank Wen, Copyright (c) 2000-02 (Fluid (R3) SoundFont, Copyright (c) 2000-2002, 2008 Frank Wen)
- Mono conversion (FluidR3Mono) by Michael Cowgill, Copyright (c) 2014-17
- Adaptation for MuseScore_General.sf2 by S. Christian Collins, Copyright (c) 2018-19
- Temple Blocks instrument provided by Ethan Winer, Copyright (c) 2002
- Drumline Cymbals provided by Michael Schorsch, Copyright (c) 2016

"MuseScore_General.sf2 is shared under the MIT license as described in COPYING, as was FluidR3Mono and FluidR3 before it."

How it is used: the tool plays the scores through the SoundFont offline and mixes the result with its own
synthesis; the `.sf2` file itself is a build input and is not shipped with the game. Presets used (General MIDI
numbering, bank 0): 8 Celesta, 11 Vibraphone, 14 Tubular Bells, 15 Dulcimer, 19 Church Organ, 24 Nylon String
Guitar, 25 Steel String Guitar, 40 Violin, 41 Viola, 42 Cello, 43 Contrabass, 44 Strings Tremolo, 45 Strings
Pizzicato, 46 Harp, 47 Timpani, 48 Strings Fast, 49 Strings Slow, 52 Choir Aahs, 53 Voice Oohs, 69 English Horn,
70 Bassoon, 71 Clarinet, 116 Taiko Drum; percussion bank 128: 0 Standard kit, 48 Orchestra Kit. None of these
presets carries a separate sample credit in `MuseScore_General_Sample_Sources.csv`, i.e. they use the FluidR3Mono
samples covered by the notices above.

Files that contain SoundFont-rendered instruments: all 19 music files, and the two stingers delivered as sound
effects, `sfx/victory_sting_1.wav` and `sfx/death_sting_1.wav`.

## Music (original compositions)

Written as note data in `tools/audio/MusicPieces1.cs`, `MusicZones.cs` and `MusicZones2.cs`; nothing is quoted from
or modelled on an existing melody or theme. Stereo 44.1 kHz OGG Vorbis, -18 LUFS (combat layers -19 LUFS).

| file | length | key, tempo, metre | notes |
|---|---|---|---|
| `menu_theme` | 2:40 loop | D minor, 54 BPM, 4/4 | the main ("ember") theme on solo cello over drone and choir |
| `town` | 2:15 loop | B minor / D major, 6/8 | guitar and harp melody |
| `boss` | 2:00 loop | G Phrygian, 12/8 | ostinato, choir stabs, drums |
| `boss_final` | 2:30 loop | D Phrygian, 112 BPM, 4/4 | the main theme inverted and darkened |
| `intro_bed` | 1:08, not looped | D minor, free | narration bed |
| `wastes_base` + `wastes_combat` | 2:00 loop | E Aeolian, 80 BPM, 4/4 | adaptive pair |
| `frost_base` + `frost_combat` | 2:00 loop | F# Aeolian, 96 BPM, 3/4 | adaptive pair |
| `crypt_base` + `crypt_combat` | 2:00 loop | C Phrygian, 72 BPM, 4/4 | adaptive pair |
| `mire_base` + `mire_combat` | 2:00 loop | A Phrygian, 80 BPM, 5/4 | adaptive pair |
| `sands_base` + `sands_combat` | 2:00 loop | E Phrygian, 104 BPM, 4/4 | adaptive pair |
| `sanctum_base` + `sanctum_combat` | 2:00 loop | G Aeolian, 64 BPM, 4/4 | adaptive pair |
| `abyss_base` + `abyss_combat` | 2:00 loop | D Phrygian, 96 BPM, 4/4 | adaptive pair |

Each pair has the same length to the sample (5 292 000 samples), tempo, metre and bar-by-bar harmony; the combat
file holds only the added ostinato strings and percussion and is mastered against its base so that the sum of the
two never passes -1 dBFS.

## Software libraries (NuGet, used by the build tool only)

These run on the developer machine to render and encode the audio. No library code is shipped in the game.

| package | version | author | licence | project | licence text |
|---|---|---|---|---|---|
| MeltySynth | 2.4.1 | Nobuaki Tanaka | MIT | https://github.com/sinshu/meltysynth/ | `Y:\tools\audio\licenses\MeltySynth_LICENSE.txt` |
| NVorbis | 0.10.5 | Andrew Ward | MIT | https://github.com/NVorbis/NVorbis | `Y:\tools\audio\licenses\NVorbis_LICENSE.txt` |
| OggVorbisEncoder | 1.2.2 | Steve Lillis | MIT | https://github.com/SteveLillis/.NET-Ogg-Vorbis-Encoder | package declares the SPDX expression `MIT` (https://licenses.nuget.org/MIT); no licence file inside the package |

- MeltySynth (SoundFont synthesizer): Copyright (C) 2021 Nobuaki Tanaka. Its licence file also carries the notices of
  the projects it derives from: C# Synth, Copyright (C) 2014 Alex Veltsistas; TinySoundFont, Copyright (C) 2017, 2018
  Bernhard Schelling, based on SFZero, Copyright (C) 2012 Steve Folta.
- NVorbis (Ogg Vorbis decoder, used only to read the encoded files back for measurement): Copyright (c) 2020 Andrew Ward.
- OggVorbisEncoder (managed Ogg Vorbis encoder, writes the `.ogg` files).
- The tool itself targets .NET 10 (Microsoft, MIT).

The Ogg container and Vorbis codec are open, royalty-free formats published by the Xiph.Org Foundation.
