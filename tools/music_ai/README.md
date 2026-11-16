# music_ai - locally generated soundtrack for 燼淵 Ashen Abyss

The music files of the game are generated on this laptop with **ACE-Step 1.5 XL turbo** (MIT) through
`diffusers.AceStepPipeline`, accelerated on the Radeon 8060S with AMD's PyTorch-for-ROCm build, then selected
by measurement, looped, mastered and encoded by the scripts in this folder. Nothing is sent anywhere after
installation; no accounts or tokens are used.

- Why this model: `DECISION.md`
- GPU recipe, measurements and pitfalls: `%LOCALAPPDATA%\AshenAbyssTools\GPU_NOTES.md`
- Credits, licences and the per-track result table: `Y:\credits\music_ai.md`
- Listening page of the pilot: `Y:\music_samples\index.html`
- The previous procedural tracks (fallback and baseline): `Y:\music_backup\procedural\` (with `SHA256SUMS.txt`)

## Where things live

| what | where |
|---|---|
| scripts (this folder) | `Y:\tools\music_ai\` |
| everything heavy | `%LOCALAPPDATA%\AshenAbyssTools\music_ai\` = `%MA_ROOT%` |
| - Python 3.12.16 (uv-managed, self-contained) | `%MA_ROOT%\python\` |
| - environments | `%MA_ROOT%\venvs\ace` (production: ROCm torch 2.9.0, diffusers 0.39.0, CLAP, librosa), `venvs\cpu` (PyPI torch 2.9.1: CPU fallback, CPU reference, speech check), `venvs\dml` (torch-directml, test only - can be deleted) |
| - model weights | `%MA_ROOT%\models\acestep-v15-xl-turbo-diffusers` (11.1 GB, used), `models\clap\larger_clap_music_and_speech` (0.8 GB), `models\whisper-small` (1 GB), and for the second pilot candidate `models\acestep-v15-turbo-diffusers` (5.9 GB) + its source `models\acestep_orig` (4.8 GB) |
| - downloaded wheels | `%MA_ROOT%\dl\rocm711\` (AMD ROCm 7.1.1 wheels, 1.7 GB) |
| - caches | `%MA_ROOT%\cache\` (pip, MIOpen kernel cache, numba) |
| - staging output | `%MA_ROOT%\stage\ace_xl\` (`cand\` raw candidates + their JSON, `final\` mastered OGG + JSON, `produce.log`, `gen.log`, `report.txt`) |

Note: the app sandbox redirects `AppData\Local\AshenAbyssTools` to
`%LOCALAPPDATA%\AshenAbyssTools` on disk.

Every script is started through the environment file, for example:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\music_ai\x.ps1 `
    %LOCALAPPDATA%\AshenAbyssTools\music_ai\venvs\ace\Scripts\python.exe Y:\tools\music_ai\produce.py --tracks boss
```

## Regenerate a track

1. Edit its entry in `tracks.py` (caption, BPM, key, lengths, seeds). Prompts must stay generic: instruments,
   mood, tempo, form - never the name of an artist, composer, band, game, film or song. Keep the tag-list style
   (see "Prompt experiments" below).
2. If the caption changed, delete the old candidates: `%MA_ROOT%\stage\ace_xl\cand\<track>__s*.*` and
   `final\<track>.*`. With unchanged prompts and seeds the batch reuses what is there; to get more candidates
   for a piece, add seed numbers to its entry.
3. Run the batch for that track (one batch at a time - a lock file enforces it):
   `run_batch.ps1 -Tracks boss` (detached; progress in `stage\ace_xl\produce.log`) or `produce.py --tracks boss`.
   It renders the missing seeds, measures and scores every candidate, loops / masters the best one and writes
   `final\boss.ogg` + `final\boss.json` (which seed won, every measurement, pass / fail).
4. `deliver.py` copies every *passing* final into `Y:\game\assets\audio\music\`, rewrites `music_info.json`
   and updates only the `duration` numbers of the music entries in `manifest_audio.json`
   (`deliver.py --dry-run` shows what it would do). `report_tables.py` refreshes the table in the credits.

To go back to a procedural track: copy it from `Y:\music_backup\procedural\`, delete that track's
`final\<track>.json`, run `deliver.py`.

## What the scripts do

| file | purpose |
|---|---|
| `env.ps1`, `x.ps1` | environment variables (caches under `%MA_ROOT%`, no telemetry, 10 CPU threads, MIOpen fast mode) |
| `fetch.py`, `fetch.ps1`, `jobs_*.txt`, `dlstatus.py` | IPv4-only, chunked, resumable downloader (wheels, Hugging Face repos with SHA-256 check) |
| `install_ace.ps1`, `install_extra.ps1`, `req\*.txt`, `convert_2b.ps1` | build the environments; convert the 2B checkpoint |
| `tracks.py` | the 19 pieces: prompts, tempo, key, lengths, seeds, loudness targets, label sets for the content flags |
| `gen_ace.py` | renders candidates (one process, model loaded once; seed -> CPU noise, so CPU and GPU give the same piece; memory wait, GPU canary, NaN check, heartbeat) |
| `audiolib.py` | measurements, loop finder, equal-power loop crossfade, mastering chain, seam metrics |
| `clap_score.py` | audio-text similarity and zero-shot content flags (LAION CLAP, on the GPU) |
| `speech_check.py` | speech recogniser (Whisper small, on the CPU) as the check for sung or spoken words |
| `produce.py`, `run_batch.ps1` | resumable batch: generate -> evaluate -> select -> loop / master / encode -> report |
| `deliver.py`, `report_tables.py` | copy passing tracks into the game, `music_info.json`, manifest durations, credits table |
| `make_samples.py`, `samples_models.json` | the listening page |
| `prompt_lab.py`, `lab.ps1`, `lab\*.json` | prompt experiments judged by measurement |
| `tests
ealism_probe.py` | weak zero-shot hint: "real recorded instruments" vs "sampled / MIDI" for old and new versions |
| `bench_gpu.py`, `bench_paths.ps1`, `compare_paths.py`, `compat_dml.py`, `tests\` | acceleration-path checks and self-tests |

### Generation safety

One generator process at a time (lock file `stage/generation.lock`), a keep-awake request is held while it
runs (`SetThreadExecutionState`), the batch watches a heartbeat file, and while a batch runs
`Y:/tools/music_ai/RUNNING.txt` holds its process ids, log path and stop command (the file is removed when the
batch ends; a stopped batch is simply started again and continues with what is missing).

The GPU is shared with everything else on the laptop and the ROCm runtime does not survive that gracefully
(see `GPU_NOTES.md`): when another application starts on the GPU, or memory runs short, a running render can
silently turn into garbage. Therefore:

- before loading the model the generator waits for 22 GB of free RAM, before every render for 7 GB;
- a cheap GPU-vs-CPU "canary" computation runs before each piece and after every sampling step; if it fails the
  output is discarded and a fresh process continues (at most 20 times per run);
- a render with NaN is discarded and its seed marked (`<stem>.nan.json`);
- on an exception on the GPU, or 7 minutes without a heartbeat (a hang), the generator is killed and **the GPU
  is not used again in that run**: the remaining candidates are rendered on the CPU (`venvs/cpu`, float32, 10
  threads, about 6 times slower), and scoring moves to the CPU environment as well.

`run_batch.ps1 -Device cpu -FirstSeeds 2` runs everything on the CPU with two candidates per piece - this is
how the last nine pieces of the first production run were made after the second GPU hang.

### Selection (nobody listens during a batch)

A candidate is rejected for: NaN, silence, clipping, DC offset, abrupt dropouts or runs of digital zero,
a third-octave hole in 200 Hz-8 kHz, a narrow resonance more than 20 dB above its surroundings in 2-12 kHz,
less than 6 kHz of bandwidth, a one-sample step at a decoder tile boundary (none was ever found), a tempo more than 6 % away from the requested
one or no clear pulse (rhythmic pieces only; x2 and x0.5 count as the same tempo), sung or spoken words
(audio-text model raises the suspicion, the speech recogniser must confirm it; every piece that may ship is
speech-checked in any case), genre drift (pop / electronic / rock / hip-hop / jazz) or a bright / comedic mood
(the last two only if also worse than the procedural version of the same piece, because zero-shot labels are
noisy: the audio-text model calls even the procedural combat tracks "playful").
Among the survivors the highest score wins: 100 x text similarity, minus penalties for the content flags, tempo
error and resonances, plus small bonuses for the requested key / mode and for a combat track whose tonal centre
matches its exploration track. If nothing survives, two more seeds are tried (twice); after that the
procedural track stays in the game.

### Post-processing

Loop pieces: the loop is cut from the body of a longer render,
choosing the window whose following audio best matches its head (onset pattern, chroma, level). For pieces with a
pulse (declared, or measured: town and menu_theme came out metric) the loop is a whole number of bars of the
measured beat period, which is refined on autocorrelation lags of up to 128 beats so that it is exact to a few
milliseconds over the whole loop; that following
audio is blended into the head with an equal-power crossfade (two bars, 3-6 s; 6 s for free-time pieces), so the
last sample is followed after the wrap by the sample that originally followed it. Mastering runs on three
back-to-back copies and keeps the middle one (steady state at the seam): 25 Hz high-pass, light stereo-linked
compression (1.6:1, 30/300 ms, threshold at the 70th percentile of the level), -18 LUFS (intro_bed -22),
look-ahead limiter to -1 dBTP, 48 -> 44.1 kHz, OGG Vorbis quality 0.6. The encoded file is decoded again and
checked (length to the sample, loudness, peak, seam jump relative to ordinary sample-to-sample movement).
`intro_bed` is not looped: silence is trimmed, the last 12 s rise by 5 dB (the "soft swell") and the last 4 s
fade out.

## Prompt experiments (prompt_lab.py, 150 s renders, three seeds each, scored by the audio-text model)

| piece | caption style | mean score | text match | "voice" flag | genre-drift flag |
|---|---|---|---|---|---|
| boss | **tag list starting "epic orchestral, cinematic, film score, battle, ..."** (used) | 33.6 | 0.433 | 0.13 | 0.08 |
| boss | same tags + section tags in the lyrics field | 28.3 | 0.423 | 0.24 | 0.09 |
| boss | tag list starting "dark orchestral, ominous, menacing, ..." | 24.5 | 0.394 | 0.19 | 0.23 |
| boss | descriptive sentences + section tags in the lyrics field | 14.5 | 0.386 | 0.43 | 0.35 |
| boss | tag list starting "dark fantasy soundtrack, sinister, ..." | 12.4 | 0.341 | 0.26 | 0.42 |
| boss | tag list starting "dark orchestral battle music, grim, ..." | 8.6 | 0.352 | 0.44 | 0.51 |
| wastes_base | **tag list starting "dark ambient, film score, solo viola, ..."** (used) | 37.5 | 0.415 | 0.11 | 0.10 |
| wastes_base | descriptive sentences | 27.8 | 0.346 | 0.13 | 0.27 |

The first pilot used descriptive sentences ("Intense boss battle music for a dark fantasy game ..."): two of
three boss candidates came out as hip-hop / jazz-like grooves at full length (the same caption was fine on
30 s clips - the model's "song" prior grows with the requested duration). The speech recogniser found no words
in them; the audio-text model's "rap vocals" label fires on hard percussion.

## Disk usage and removal

About 37 GB under `%MA_ROOT%`: models 24 GB (XL turbo 11.1, 2B turbo 5.9 + source 4.8, CLAP 0.8, Whisper 1.0),
environments 7.6 GB (ace 4.9, dml 1.7, cpu 1.0), downloaded wheels 1.7 GB, caches 0.5 GB, staging 2-3 GB.
Safe to delete at any time without affecting production: `venvs\dml`, `models\acestep_orig`,
`models\acestep-v15-turbo-diffusers`, `dl\`, `cache\pip`, `stage\bench`, `stage\selftest`.

To remove everything: delete `%LOCALAPPDATA%\AshenAbyssTools\music_ai` (nothing was installed
system-wide: no drivers, services, registry entries or PATH changes). `Y:\tools\music_ai`, `Y:\music_samples`
and `Y:\music_backup` are plain folders. The game keeps working: it only needs the `.ogg` files.

## State on 2026-11-15 19:18 (work paused)

15 of the 19 pieces in `Y:/game/assets/audio/music` are generated versions that passed every check; `sands_combat`,
`sanctum_combat`, `abyss_base` and `abyss_combat` are still the procedural tracks. `verify_delivery.py` reports
19 of 19 files complete and consistent with `music_info.json` and the manifest. What is left and the exact
resume commands are in `RUNNING.txt`.
