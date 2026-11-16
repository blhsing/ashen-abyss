# Music model decision (researched 2026-11-14)

Goal: instrumental dark-fantasy game score (solo cello/viola, low strings, harp/guitar, wordless choir, bowed
metal, frame drums/taiko, bells, drones; no pop vocals), 1-3 minute loops, generated locally on this laptop
(Ryzen AI Max+ 395, Radeon 8060S integrated GPU, 64 GB shared memory, Windows 11, no NVIDIA GPU).

Everything below was looked up on the day from primary sources (project repositories, model cards, the
Hugging Face API, AMD documentation); nothing is taken from memory. Benchmark claims are the authors' own unless
stated otherwise.

## Decision

**ACE-Step 1.5 XL turbo** (4B-parameter DiT, 8 sampling steps), official Diffusers-format weights
`ACE-Step/acestep-v15-xl-turbo-diffusers`, run through `diffusers.AceStepPipeline`.

Why:

1. **Licence** - code and weights are MIT (model card: "Generated music can be used for commercial purposes";
   training data described as "licensed music, royalty-free/public domain, and synthetic (MIDI-to-Audio) data").
   It is the only strong current model whose weights fit the project's licence rule (CC0 / MIT / OFL / CC-BY).
2. **Instrumental control** - the training-time marker `[Instrumental]` as the lyrics input, plus explicit
   conditioning on BPM, key, time signature and duration. No words are ever supplied.
3. **Length and fidelity** - 10 s to 10 min in one pass, 48 kHz stereo (we deliver 44.1 kHz).
4. **Runs here** - the project lists AMD/ROCm as a supported backend and its code has a specific path for
   "AMD iGPUs on Strix Halo" (float32 instead of bfloat16). A diffusion model with 8 steps is also the only
   class that stays practical if the GPU path fails and the CPU has to do the work; autoregressive 3B song
   models (YuE2, HeartMuLa, LeVo 2) are not.
5. **Quality** - the XL (4B) decoder is the project's best-scoring variant (its own table: SongEval 4.65-4.79 for
   XL, 4.59-4.72 for the 2B model, HeartMuLa 4.45-4.69, DiffRhythm 2 3.66-3.99, YuE v1 about 2.8-3.0).
   Independent round-ups from autumn 2026 name it the best all-round open model; they also note it is "not the
   absolute top on raw fidelity" against closed services.

**Measured on this machine** (details in `%LOCALAPPDATA%/AshenAbyssTools/GPU_NOTES.md`): on the
Radeon 8060S through AMD's PyTorch-for-ROCm 7.1.1 wheels, transformer in bfloat16 and VAE in float32, a
150-196 s piece renders in 55-130 s (0.35-0.66 s of compute per second of audio, 16 GB of GPU memory); the CPU
alone needs 2.6 s per second. A complete track (three or four candidates, scoring, mastering) therefore costs
about 6-10 minutes - far inside the 40-minutes-per-track limit, so no weaker model had to be substituted.

**What the model is not good at** (found in the pilot, see `README.md`): it is trained mostly on songs. With
descriptive captions and at full track length, rhythmic pieces drifted towards hip-hop / jazz-like grooves; plain
tag-list captions that start with "epic orchestral, cinematic, film score" fixed that for most seeds. Several
candidates per piece and automatic screening are part of the method, not an extra.

Not chosen as the main model, for the reasons given in the table: YuE2 (non-commercial weights, NVIDIA-only
reference path), Stable Audio 3 (gated download / Community Licence), HeartMuLa and DiffRhythm 2 (lyric-driven,
no documented instrumental mode), LeVo 2 / SongGeneration (licence could not be verified), MusicGen (NC weights,
32 kHz, 30 s windows), Magenta RealTime 2 (no AMD/Windows path).

## Comparison

| model (release) | size | licence: code / weights | instrumental control | max length, output | runs on this machine? |
|---|---|---|---|---|---|
| **ACE-Step 1.5 XL turbo** (XL: 2026-04; repo v0.1.8, 2026-05) | 4B DiT (+0.6B text encoder, VAE); 11.1 GB Diffusers format (bf16), 20 GB original format | MIT / MIT | `[Instrumental]` lyrics marker; caption; BPM, key, time signature, duration | 10 min; 48 kHz stereo | yes - ROCm (official backend), CPU fallback is practical (8 steps) |
| ACE-Step 1.5 (2B) turbo / sft (2026-01) | 2B DiT, 4.8 GB; optional 0.6-4B "5 Hz" planner LM | MIT / MIT | same | same | yes; smaller and faster, lower scores than XL |
| YuE2-3B (2026-09-10; instrumental mode 2026-09-25) | 3B AR/NAR, 7.3 GB + VAE 0.5 GB + MERT | Apache-2.0 / **CC BY-NC 4.0 "with additional creator permission"** (individual creators may monetise outputs; companies need a commercial licence from the authors) | instrumental via the `yue2-music` skill (description, ABC score or reference) | multi-minute; 48 kHz stereo | reference path is Linux + NVIDIA 24 GB (bf16). Its hardware doc: ROCm only on Linux, "native Windows AMD acceptance is pending", no CPU figures. Authors report 71 s per 3.6-min song on an RTX 4090 |
| Stable Audio 3 Medium / Small-Music (2026-05-20) | 1.4-2B / 459M; 10.4 GB / 3.5 GB | weights: **Stability AI Community License** (free below US$1M annual revenue, otherwise enterprise licence) + Gemma terms for the T5Gemma text encoder | instrumental by design (no vocals at all) | 6 min 20 s (Medium), 2 min (Small); 44.1 kHz stereo | weights repos are **gated** (Hugging Face login + contact details) - excluded by the "no accounts or tokens" rule. An ungated `stable-audio-3-optimized` repo has ONNX builds (14.6 GB / 3.7 GB) that could be driven through ONNX Runtime DirectML, but that needs a hand-written sampler and the licence still applies |
| HeartMuLa-oss-3B (2026-01/02) | 3B AR + codec; 15.8 GB + 6.6 GB | Apache-2.0 / Apache-2.0 | lyrics + tags; no documented instrumental mode | 6 min; 48 kHz | CUDA-centred autoregressive model; no AMD/Windows path documented |
| DiffRhythm 2 (2025-10-30) | 5.1 GB | Apache-2.0 / Apache-2.0 | "Instrumental music generation" is still an unchecked TODO in the repo | full-length songs | not evaluated further (no instrumental mode) |
| SongGeneration 2 / LeVo 2 (2026-03) | large 12.9 GB (22-28 GB GPU memory) | licence file not verifiable from a primary source today (official repo URL returned 404, the Hugging Face model needs authentication); third-party round-ups say commercial use is blocked | `--bgm` flag for instrumental | 4 min 30 s | no (GPU memory, licence) |
| StepAudio 3 Music (2026-09-15) | - | no downloadable weights found (Hugging Face repo needs authentication; described elsewhere as API-only) | instrumental task listed in the report | 5 min 30 s | no |
| Magenta RealTime 2 (2026-06-04) | 2.4B (230M real-time variant) | Apache-2.0 / CC BY 4.0 | instrumental by design; live steering by text / MIDI / audio | streaming, 48 kHz stereo | no - Apple Silicon (real time) or NVIDIA (offline) only |
| MiniMax-Music3 (2026) | under 24 GB VRAM | Community Licence (attribution in the product; permission above US$20M/yr) | song model with vocals | about 5 min | not evaluated (licence, NVIDIA) |
| YuE v1 (2025-01) | two-stage AR | Apache-2.0 / Apache-2.0 | lyric-driven, rigid input format | minutes | far too slow (about 6 min of RTX 4090 time per 30 s of audio) |
| MusicGen large / stereo (2023) | 3.3B; 20.5 GB repo | MIT / **CC BY-NC 4.0** | instrumental by design | 30 s windows, 32 kHz | CPU possible, but dated quality and NC weights |
| Stable Audio Open 1.0 (2024) | 1.1B | Stability AI Community License; gated | instrumental / SFX | 47 s, 44.1 kHz | gated; too short |

Hugging Face facts were read from the Hub API on 2026-11-14 (`gated`, `license`, file sizes).

## Candidates that were actually run for the samples page

`Y:/music_samples/index.html` shows, next to the procedural baseline:

1. **ACE-Step 1.5 XL turbo** (4B) - the recommended model, used for the full set.
2. **ACE-Step 1.5 turbo** (2B) - the same family at half the size, converted from the official checkpoint with
   the official Diffusers conversion script; faster and lighter, lower scores in the authors' own tables.

No third family could be added within the rules of this job: YuE2's weights are non-commercial and its only
supported path is Linux + NVIDIA (its hardware document lists native Windows AMD as "pending"); Stable Audio 3
needs a Hugging Face account (gated repositories) and falls under the Stability AI Community License; the
remaining song models have no instrumental mode or no verifiable licence.

## Sources

- ACE-Step 1.5: https://github.com/ace-step/ACE-Step-1.5 (README, `docs/en/INSTALL.md`, `docs/en/INFERENCE.md`,
  releases), https://huggingface.co/ACE-Step/acestep-v15-xl-turbo-diffusers, https://huggingface.co/ACE-Step/acestep-v15-xl-sft,
  https://arxiv.org/abs/2602.00744, project page https://ace-step.github.io/ace-step-v1.5.github.io/
- YuE2: https://github.com/multimodal-art-projection/YuE (README, `docs/hardware.md`), https://huggingface.co/m-a-p/YuE2-3B
- Stable Audio 3: https://stability.ai/news-updates/meet-stable-audio-3-the-model-family-built-for-artistic-experimentation-with-open-weight-models,
  https://huggingface.co/stabilityai/stable-audio-3-medium, https://huggingface.co/stabilityai/stable-audio-3-optimized
- HeartMuLa: https://github.com/HeartMuLa/heartlib ; DiffRhythm 2: https://github.com/ASLP-lab/DiffRhythm2
- LeVo 2: https://arxiv.org/abs/2606.30642 ; StepAudio 3 Music: https://arxiv.org/abs/2609.16034
- Magenta RealTime 2: https://magenta.withgoogle.com/magenta-realtime-2
- Round-ups (secondary, autumn 2026): https://boppy.me/blog/best-open-source-ai-music-models (vendor of a hosted
  ACE-Step service, so biased), https://usefulai.com/models/music-generation,
  https://medium.com/@aitoolscan/ace-step-1-5-vs-heartmula-the-state-of-local-ai-music-generation-in-2026-c37cb86351bf
- AMD: https://rocm.docs.amd.com/projects/radeon-ryzen/en/latest/docs/install/installrad/windows/install-pytorch.html,
  https://rocm.docs.amd.com/projects/radeon-ryzen/en/latest/docs/compatibility/compatibilityryz/windows/windows_compatibility.html,
  https://rocm.docs.amd.com/en/latest/compatibility/compatibility-matrix.html
