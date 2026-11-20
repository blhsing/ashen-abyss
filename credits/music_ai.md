# Music credits - AI-generated soundtrack (燼淵 Ashen Abyss)

The music files listed in `Y:\game\assets\audio\music\music_info.json` with a `seed` were generated locally on
the development laptop with an open text-to-music model, from text prompts written for this project, and then
selected, looped and mastered by the project's own scripts (`Y:\tools\music_ai`). Entries whose `seed` is `null`
are still the earlier procedural compositions (see `Y:\credits\audio.md`).

No recordings, samples, melodies or lyrics from any existing work were given to the model: the only inputs are
the prompts in `Y:\tools\music_ai\tracks.py` (instrumentation, mood, tempo, key and form in generic musical
terms - no artist, composer, band, game, film or song is named), the instrumental marker `[Instrumental]` in
place of lyrics, and a random seed.

## Model

| | |
|---|---|
| model | **ACE-Step 1.5 XL turbo** (4B-parameter diffusion transformer, 8 sampling steps), Diffusers-format checkpoint |
| authors | Junmin Gong, Yulin Song, Wenxiao Zhao, Sen Wang, Shengyuan Xu, Jing Guo; the project is "co-led by ACE Studio and StepFun" (copyright notice of the repository: "Copyright (c) 2026 ACEStep") |
| weights | https://huggingface.co/ACE-Step/acestep-v15-xl-turbo-diffusers (official repository, revision of 2026-05-01; SHA-256 of every weight file verified against the Hub on download) |
| code | https://github.com/ace-step/ACE-Step-1.5 ; pipeline implementation: `AceStepPipeline` in Hugging Face Diffusers 0.39.0, https://github.com/huggingface/diffusers |
| paper | "ACE-Step 1.5: Pushing the Boundaries of Open-Source Music Generation", https://arxiv.org/abs/2602.00744 |
| licence of the code | MIT |
| licence of the weights | MIT (model card: "Generated music can be used for commercial purposes"; training data described by the authors as "licensed music, royalty-free/public domain, and synthetic (MIDI-to-Audio) data") |

Components that ship inside that checkpoint:

| component | origin | licence |
|---|---|---|
| DiT transformer, condition encoder, audio VAE (48 kHz stereo) | ACE-Step | MIT |
| text encoder `Qwen3-Embedding-0.6B` | Alibaba Qwen team, https://huggingface.co/Qwen/Qwen3-Embedding-0.6B | Apache-2.0 |

Citation requested by the authors:

```
@misc{gong2026acestep,
  title = {ACE-Step 1.5: Pushing the Boundaries of Open-Source Music Generation},
  author = {Junmin Gong, Yulin Song, Wenxiao Zhao, Sen Wang, Shengyuan Xu, Jing Guo},
  howpublished = {\url{https://github.com/ace-step/ACE-Step-1.5}},
  year = {2026},
  note = {GitHub repository}
}
```

## What the licences mean for the game

- MIT places no restriction on the generated audio and no non-commercial clause on the weights; the game ships
  only audio files, not the model. Recording the model and its licence here covers the attribution side.
- The model's README carries a disclaimer the user should know: "we encourage users to verify the originality of
  generated works, clearly disclose AI involvement, and obtain appropriate permissions when adapting protected
  styles or materials", and "The authors are not responsible for any misuse of the model".
  Suggested credit line: "Music generated with ACE-Step 1.5 (MIT), selected and mastered by the developers."
- Whether purely AI-generated audio is protected by copyright differs by country, and some storefronts ask
  whether a product contains AI-generated content; check the rules that apply when the game is released.
- An automatic check cannot prove that a generated piece does not resemble an existing work. The prompts never
  ask for one, and every delivered track was screened for sung lyrics / speech and genre drift, but nobody has
  listened to every track against the world's repertoire; listening before release is recommended.

## Tools used to select and process the audio (not part of the shipped files)

| tool | use | licence |
|---|---|---|
| PyTorch 2.9.0 + AMD ROCm 7.1.1 runtime for Windows (https://repo.radeon.com/rocm/windows/rocm-rel-7.1.1/) | inference on the Radeon 8060S | BSD-3-Clause (PyTorch); AMD's ROCm runtime under the licences shipped inside the wheels |
| Hugging Face Diffusers 0.39.0, Transformers 4.57.6 | pipeline, text encoder | Apache-2.0 |
| LAION CLAP `laion/larger_clap_music_and_speech` | audio-text similarity used to choose between candidates and to flag vocals / genre drift | Apache-2.0 |
| OpenAI Whisper small (`openai/whisper-small`) | speech recogniser used as the check for sung or spoken words | Apache-2.0 (model card); original release MIT |
| librosa 1.0.1, SciPy 1.18, NumPy 2.5, numba 0.68 | beat tracking, chroma, filters | ISC / BSD |
| pyloudnorm 0.2.0 | ITU-R BS.1770 loudness | MIT |
| python-soundfile 0.14.0 / libsndfile 1.2.2 (with libvorbis) | FLAC and OGG Vorbis I/O | BSD-3-Clause / LGPL-2.1 (libsndfile), BSD (libvorbis) |

## Per-track record

Model, seed, measured tempo, length and loop flag of every file are in
`Y:/game/assets/audio/music/music_info.json`; the full measurements (all candidate scores, loop window, seam
metrics, loudness, the exact prompt of every candidate) are in
`%LOCALAPPDATA%/AshenAbyssTools/music_ai/stage/ace_xl/` (`final/<name>.json`, `cand/<name>__s<seed>.json`).
The prompts themselves are in `Y:/tools/music_ai/tracks.py`.

Columns: *text match* = cosine similarity between the audio and the piece's written description (LAION CLAP;
the same measure for the procedural version in brackets); *flags* = share of the audio the audio-text model
assigns to unwanted labels (voice with words / pop-electronic-rock-hip-hop-jazz / bright or comedic); *loop seam* =
size of the sample jump at the loop point relative to ordinary sample-to-sample movement in the same file
(below 1 = no audible step), high-frequency energy at the seam relative to the surrounding half second, and the
level difference between the second before and the second after the seam. Every delivered file was also run
through a speech recogniser: no words were found in any of them.

<!-- track-table:begin -->
| file | in the game | length | tempo (asked) | key heard (asked) | seed (candidates tried) | rendered on | text match (procedural) | vocal / genre / mood flags | words found | loop seam: jump vs normal motion, click, level step | loudness, true peak |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `menu_theme` | AI | 2:56.00 | free (60) | A minor (D minor) | 7103 (6) | GPU, bfloat16 | 0.54 (0.55) | 0.00 / 0.01 / 0.12 | none (7 windows) | 0.05x, +6.0 dB, +3.9 dB | -18.0 LUFS, -7.0 dBTP |
| `town` | AI | 2:15.78 | free (76) | B minor (B minor) | 7112 (3) | GPU, bfloat16 | 0.48 (0.56) | 0.01 / 0.03 / 0.36 | none (6 windows) | 0.17x, +12.0 dB, +0.2 dB | -18.0 LUFS, -10.3 dBTP |
| `intro_bed` | AI | 1:05.30 | free (60) | D minor (D minor) | 7120 (3) | GPU, bfloat16 | 0.17 (0.22) | 0.03 / 0.15 / 0.01 | none (3 windows) | not looped | -22.0 LUFS, -5.2 dBTP |
| `boss` | AI | 1:55.84 | 116 (116) | D minor (G minor) | 7133 (7) | GPU, bfloat16 | 0.48 (0.65) | 0.05 / 0.06 / 0.27 | none (5 windows) | 0.04x, +5.4 dB, +2.9 dB | -18.0 LUFS, -8.8 dBTP |
| `boss_final` | AI | 2:32.00 | 120 (120) | D minor (D minor) | 7141 (4) | GPU, bfloat16 | 0.43 (0.52) | 0.01 / 0.01 / 0.14 | none (6 windows) | 0.18x, +8.4 dB, +0.3 dB | -18.0 LUFS, -8.5 dBTP |
| `wastes_base` | AI | 2:15.50 | free (56) | E major (E minor) | 7154 (6) | GPU, bfloat16 | 0.45 (0.53) | 0.01 / 0.03 / 0.01 | none (6 windows) | 0.15x, -0.5 dB, +0.2 dB | -18.0 LUFS, -8.1 dBTP |
| `wastes_combat` | AI | 1:51.42 | 112 (112) | E minor (E minor) | 7163 (4) | GPU, bfloat16 | 0.43 (0.62) | 0.03 / 0.02 / 0.24 | none (5 windows) | 0.32x, +1.6 dB, -2.3 dB | -18.0 LUFS, -9.1 dBTP |
| `frost_base` | AI | 2:02.50 | free (54) | F# minor (F# minor) | 7172 (3) | GPU, bfloat16 | 0.31 (0.37) | 0.03 / 0.08 / 0.00 | none (6 windows) | 0.12x, +2.4 dB, -2.5 dB | -18.0 LUFS, -7.7 dBTP |
| `frost_combat` | AI | 1:53.33 | 108 (108) | F# minor (F# minor) | 7183 (4) | GPU, bfloat16 | 0.54 (0.60) | 0.01 / 0.03 / 0.25 | none (5 windows) | 0.28x, -0.5 dB, -0.4 dB | -18.0 LUFS, -10.1 dBTP |
| `crypt_base` | AI | 2:00.00 | free (52) | C minor (C minor) | 7191 (3) | GPU, bfloat16 | 0.39 (0.42) | 0.07 / 0.04 / 0.00 | none (6 windows) | 0.31x, +1.9 dB, +4.1 dB | -18.0 LUFS, -6.5 dBTP |
| `crypt_combat` | AI | 2:08.00 | 105 (104) | F major (C minor) | 7201 (2) | CPU, float32 | 0.39 (0.47) | 0.04 / 0.39 / 0.33 | none (5 windows) | 0.33x, +4.1 dB, +0.3 dB | -18.0 LUFS, -7.8 dBTP |
| `mire_base` | AI | 2:00.00 | free (50) | F major (Bb minor) | 7210 (2) | CPU, float32 | 0.46 (0.47) | 0.01 / 0.49 / 0.01 | none (6 windows) | 0.22x, +0.6 dB, -4.3 dB | -18.0 LUFS, -4.9 dBTP |
| `mire_combat` | AI | 1:55.20 | 101 (100) | F major (Bb minor) | 7221 (2) | CPU, float32 | 0.32 (0.45) | 0.03 / 0.02 / 0.03 | none (5 windows) | 0.24x, +3.5 dB, +0.2 dB | -18.0 LUFS, -8.2 dBTP |
| `sands_base` | AI | 2:12.38 | free (58) | A minor (A minor) | 7231 (2) | CPU, float32 | 0.42 (0.50) | 0.08 / 0.18 / 0.02 | none (6 windows) | 0.20x, +8.5 dB, -0.8 dB | -18.0 LUFS, -2.5 dBTP |
| `sands_combat` | AI | 2:04.82 | 116 (116) | A minor (A minor) | 7542 (4) | CPU, float32 | 0.27 (0.47) | 0.01 / 0.08 / 0.12 | none (5 windows) | 0.19x, +5.1 dB, +0.4 dB | -18.0 LUFS, -7.8 dBTP |
| `sanctum_base` | AI | 2:04.00 | free (55) | G minor (G minor) | 7251 (2) | CPU, float32 | 0.35 (0.57) | 0.03 / 0.06 / 0.00 | none (6 windows) | 0.21x, +2.4 dB, -0.8 dB | -18.0 LUFS, -6.1 dBTP |
| `sanctum_combat` | AI | 1:53.45 | 110 (110) | G minor (G minor) | 7262 (4) | CPU, float32 | 0.30 (0.48) | 0.06 / 0.32 / 0.17 | none (5 windows) | 0.67x, +7.0 dB, +0.8 dB | -18.0 LUFS, -6.6 dBTP |
| `abyss_base` | AI | 2:08.00 | free (60) | A minor (D minor) | 7474 (5) | CPU, float32 | 0.37 (0.41) | 0.04 / 0.22 / 0.04 | none (6 windows) | 0.13x, +0.5 dB, -1.5 dB | -18.0 LUFS, -7.3 dBTP |
| `abyss_combat` | AI | 2:08.00 | 120 (120) | A minor (A minor) | 7485 (6) | CPU, float32 | 0.49 (0.61) | 0.07 / 0.12 / 0.04 | none (5 windows) | 0.41x, +12.8 dB, +0.1 dB | -18.0 LUFS, -8.5 dBTP |
<!-- track-table:end -->
