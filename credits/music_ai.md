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
(filled in by `report_tables.py`)
<!-- track-table:end -->
