# Voice model for 《燼淵 Ashen Abyss》 — comparison (researched 2026-11-14)

**Decision (2026-11-15, by the user after listening to `Y:\voice_samples\index.html`): VoxCPM2 in voice-design
mode, no reference audio.** All 23 voices are designed from text descriptions (`voices.py`); a character's voice
is kept the same by making every line a continuation of that character's own anchor take (README.md).
The user also found the first boss sample too unintelligible and too coarse: boss voices are now described for
clarity, measured (pitch, harmonics-to-noise) and the boss effect chains were rebuilt clarity-first
(`profiles.py`, `Y:\voice_samples\osaros\index.html`). The rest of this file is the comparison as it stood
before the choice.

Requirement: natural **Taiwanese-accented Mandarin** (臺灣華語 readings and prosody, Traditional input,
correct polyphones), ~23 distinguishable voices, runs fully offline on this Windows machine
(Ryzen AI Max+ 395 / Radeon 8060S, no NVIDIA), licence that allows use in the game.

| Model (late 2026) | Taiwan accent | Naturalness / control | Licence | Windows, no NVIDIA | Multi-voice | Verdict |
|---|---|---|---|---|---|---|
| **BreezyVoice** (MediaTek Research, 2025-02; CosyVoice-300M fine-tuned on Taiwanese Mandarin) | Built for it; bopomofo input `字[:ㄅㄆㄇ3]` for exact Taiwan readings | Good, 22 kHz, no emotion/instruct control | Apache-2.0 code + weights | Yes, CPU ("runnable on CPU" per README) | Zero-shot from a 5-15 s reference clip | **Candidate** — the only open model whose *own* phonetics are Taiwanese |
| BreezyVoice-26 (MediaTek, 2026) | Taiwanese **Hokkien** (台語), not Mandarin | — | weights not published (LINE demo only; repo issue #14 still open) | — | — | Not available |
| **Fun-CosyVoice3-0.5B-2512** (FunAudioLLM, 2025-12) | No Taiwan instruction (dialect list has 閩南話 only); accent follows the reference clip | Very good, 24 kHz, speed/emotion instructions, pinyin hot-fix tokens `[j][í]` | Apache-2.0 code + weights | Yes, CPU (PyTorch) | Zero-shot | **Candidate** (CosyVoice family) |
| **VoxCPM2** (OpenBMB, 2026-04, 2 B params) | Not listed; learned mostly mainland readings. A Taiwanese project (jt-live-whisper) uses it as its default 臺灣華語 reader after tuning readings | Excellent, 48 kHz, **voice design from a text description** (no reference audio needed), controllable cloning | Apache-2.0 code + weights | Yes, CPU; GGUF/Vulkan ports exist (llama.cpp-omni, VoxCPM.cpp) | Unlimited designed voices + zero-shot | **Candidate** |
| Qwen3-TTS 1.7B (Alibaba, 2026-01) | Reported mainland accent; Traditional-character OOV garbling reported | Very good, voice design + 9 preset voices | Apache-2.0 | CPU "works but not recommended" | Design + presets | Not sampled (4.5 GB more on a ~3 MB/s link; weakest on Traditional text) |
| IndexTTS-2 / 2.5 (bilibili, 2026-08) | Follows reference | Excellent emotion control | **bilibili Model Licence: commercial use needs written permission** | CPU-only not supported in practice | Zero-shot | Rejected: licence |
| F5-TTS (+ community fine-tunes) | Follows reference; no zh-TW fine-tune found | Good | Weights **CC-BY-NC** | Yes | Zero-shot | Rejected: licence |
| Fish-Speech / OpenAudio S1, Fish Audio S2 | Follows reference | Excellent | **Non-commercial** research licences | — | Zero-shot | Rejected: licence |
| Spark-TTS 0.5B | Follows reference | Good | Weights re-licensed **CC-BY-NC-SA** | Yes | Zero-shot + attributes | Rejected: licence |
| GPT-SoVITS v2-v4 | Follows reference (needs a Taiwanese clip per voice) | Good with fine-tuning | MIT | Yes | Zero/few-shot | Not sampled: no advantage over the three candidates without real Taiwanese recordings |
| Kokoro v1.1-zh (current fallback) | None (mainland Mandarin) | Fair, 24 kHz, 100 fixed speakers | Apache-2.0 | Yes, very fast | 100 speakers | Baseline in the listening test |

Key consequence: apart from BreezyVoice, every strong open model takes its *accent from the
reference clip*. The rules forbid cloning real people, so the Taiwanese accent has to come from
BreezyVoice itself — either directly, or by letting BreezyVoice speak a neutral sentence in a synthetic
(Kokoro) timbre and using that as the reference clip for a higher-fidelity model ("second-generation
synthetic reference"). The listening test contains both routes.

Sources: github.com/mtkresearch/BreezyVoice · huggingface.co/MediaTek-Research/BreezyVoice ·
arxiv.org/abs/2501.17790 · github.com/FunAudioLLM/CosyVoice · huggingface.co/FunAudioLLM/Fun-CosyVoice3-0.5B-2512 ·
github.com/OpenBMB/VoxCPM · huggingface.co/openbmb/VoxCPM2 · github.com/jasoncheng7115/jt-live-whisper ·
huggingface.co/IndexTeam/IndexTTS-2.5 · github.com/SWivid/F5-TTS · rocm.docs.amd.com (Windows matrix)

## Measured on this machine (CPU, 8 threads)

Six lines per column, see `Y:\voice_samples\index.html` (players), `README.txt`, `report.json` (every number).
Speeds were taken with 6-8 threads while Blender renders used 11-19 cores, so they are conservative.

| Column | CPU s per 1 s audio | Mean CER (homophones forgiven) | 寂 spoken ㄐㄧˊ (rising) | Retroflex index |
|---|---|---|---|---|
| Kokoro v1.1-zh (baseline) | 0.2 | 0.075 (0.021) | 0 of 2 | 0.65 |
| BreezyVoice, LM unprompted, Kokoro timbre clip | 12.1 | 0.083 (0.041) | 3 of 3 | 0.67 |
| BreezyVoice, official zero-shot, bundled Taiwanese clip | 16.8 | 0.108 (0.046) | 2 of 3 (one falling) | 0.86 |
| BreezyVoice, LM unprompted, bundled Taiwanese clip as timbre | 12.6 | 0.131 (0.042) | 2 of 3 (one flat) | 0.87 |
| Fun-CosyVoice3, zero-shot, bundled Taiwanese clip | 8.2 | 0.087 (0.016) | 3 of 3 | 0.79 |
| VoxCPM2, voice design (no reference) | 14.8 (10-12 at night) | 0.100 (0.052) | 3 of 3 | 0.85 |
| VoxCPM2, continuation, bundled Taiwanese clip | 13.0 (9-10 at night) | 0.059 (0.011) | 3 of 3 | 0.82 |

Retroflex index = hiss centre of ㄓㄔㄕ / ㄗㄘㄙ in 2-7.5 kHz; nearer 1 = more merged = more Taiwan-like; anchors
with the same code: Kokoro voices 0.65-0.70, the real Taiwanese speaker of the bundled clip about 0.82. It is a
weak indicator (see `accent_probe.py`); the 寂 tone check is the solid one.

What the measurements say:

1. Readings are controllable in all three (BreezyVoice `寂[:ㄐㄧ2]`, CosyVoice 3 `[j][í]`, VoxCPM2 `{ji2}` -
   undocumented but obeyed). Unhinted, VoxCPM2 says mainland jì even when the voice description asks for a
   Taiwanese accent. BreezyVoice misses hints now and then when its LM continues a prompt.
2. The *sound* of the accent follows the reference clip - even in BreezyVoice (0.67 with a Kokoro clip, 0.86
   with the Taiwanese clip). Synthetic Kokoro reference clips are therefore not enough; a reference-driven
   model needs Taiwanese reference speech.
3. VoxCPM2 voice design is the only route with no reference audio; whether its voices pass as Taiwanese has to
   be judged by ear. Three small first-take slips in five lines (left in the samples, noted on the page).
4. Nothing is near real time on CPU; CosyVoice 3 is the fastest of the large models.

**Recommendation (measurement-based; the user decides by listening):** Fun-CosyVoice3-0.5B-2512 with Taiwanese
reference clips. Alternative if its voice-design samples already sound Taiwanese: VoxCPM2 (design each
character once, reuse the clip). BreezyVoice only if it clearly sounds the most Taiwanese: slowest, 22 kHz,
no style control. Open question for any reference-driven choice: the source of the 23 reference voices
(single bundled clip shifted per role / Common Voice zh-TW CC0 clips / VoxCPM2-designed clips).

## AMD GPU (not started - follows the choice)

Radeon 8060S, ASUS OEM driver 32.0.22032.6002 (Adrenalin 25.20.32.06, 2025-12). AMD's PyTorch for Windows
(ROCm 7.2.1, torch 2.9.1, Python 3.12) lists the Ryzen AI Max+ 395 but documents graphics driver 26.2.2 as
required -> probably blocked without a driver update (not to be done). To try first for the chosen model:
ONNX Runtime + DirectML (CosyVoice 3 and BreezyVoice ship an official ONNX export of the flow-decoder
estimator), Vulkan GGUF runtimes (VoxCPM2: VoxCPM.cpp / llama.cpp-omni, community ports), else CPU.

<!-- pause-state-start -->
## State when the run was paused (2026-11-19 00:41)

- **99 of 207 ids have audio** in `Y:\game\assets\audio\vo` (every file decoded and checked: mono 44.1 kHz OGG Vorbis, complete, identical to the validated staging file, made from the current anchor of its voice).
- Voices with audio: narrator 16/19, elder 66/66, merchant 8/8, boss_gravedigger 3/4, boss_rimelord 3/4, boss_osaros 3/4.
- Voices designed and audited so far: boss_gravedigger, boss_osaros, boss_rimelord, elder, merchant, narrator. Audit results: table below.
- Anchor designed but not yet audited (the run stopped while making its first lines): boss_bishop.
- Removed from the game folder because they were takes of a rejected voice: none.
- Still to record: 108 ids - narrator 16/19, occultist 0/11, smith 0/9, jeweler 0/7, villager_m 0/5, villager_f 0/5, boss_gravedigger 3/4, boss_rimelord 3/4, boss_bishop 0/4, boss_mire_mother 0/4, boss_sand_tyrant 0/4, boss_grand_hush 0/4, boss_osaros 3/4, hero_emberblade 0/5, hero_astromancer 0/5, hero_hunter 0/5, hero_soulbinder 0/5, hero_wildsoul 0/5, hero_forgebody 0/5, hero_lampknight 0/5, hero_pactbinder 0/5, boss_sh_wastes 0/1, boss_sh_frost 0/1, boss_sh_crypt 0/1, boss_sh_mire 0/1, boss_sh_sands 0/1, boss_sh_sanctum 0/1, boss_sh_abyss 0/1, boss_wb_firstbearer 0/1, boss_firstkeeper 0/1.
- Resume with `gen.py all` (see `RUNNING.txt`); nothing has to be redone.
<!-- pause-state-end -->

<!-- voice-audit-start -->
## Voice audit - does every designed voice measure as its character?

Written by `gen.py` (2026-11-19 00:35). Gates: (1) median pitch of the anchor inside the range wanted for the
character, and of its first three lines within 10 % of it (men at most 150 Hz - the elder 140 Hz -, women at least
165 Hz); (2) the male / female classifier of `gender.py` (CAM++ speaker embeddings, calibrated on the 100 labelled
Kokoro speakers: 0.99 cross-validated accuracy there; on VoxCPM2 voices a man must score at least
0.6, a woman at most 0.45). A voice that fails is designed again (other seeds) and all of its lines with it.

| character | intended | pitch wanted (Hz) | anchor pitch | lines pitch (n) | p(male) anchor / lines | gate | action |
|---|---|---|---|---|---|---|---|
| narrator | man, middle-aged storyteller | 80-135 | 108 | 94 (3) | 0.69 / 0.67 | pass | kept |
| elder (rejected) | old man | 95-140 | 172 | 170 (3) | 0.33 / 0.31 | **failed** (pitch classifier) | rejected: the user heard an elderly woman (first design, pitch range then allowed up to 190 Hz); designed again, all lines redone |
| elder | old man | 95-140 | 122 | 116 (3) | 0.7 / 0.67 | pass | kept (design round 2) |
| smith | man, middle-aged, burly | 85-140 | - | - | - | not designed yet | - |
| merchant | young woman | 200-330 | 296 | 276 (3) | 0.36 / 0.39 | pass | kept |
| occultist | woman, mature | 170-250 | - | - | - | not designed yet | - |
| jeweler | woman, elderly | 165-250 | - | - | - | not designed yet | - |
| villager_m | man, middle-aged | 95-150 | - | - | - | not designed yet | - |
| villager_f | woman, middle-aged | 175-270 | - | - | - | not designed yet | - |
| hero_emberblade | young man | 85-140 | - | - | - | not designed yet | - |
| hero_astromancer | young woman | 190-310 | - | - | - | not designed yet | - |
| hero_hunter | young woman | 165-240 | - | - | - | not designed yet | - |
| hero_soulbinder | young man | 90-150 | - | - | - | not designed yet | - |
| hero_wildsoul | man in his prime | 90-150 | - | - | - | not designed yet | - |
| hero_forgebody | young man | 95-150 | - | - | - | not designed yet | - |
| hero_lampknight | woman, adult | 170-250 | - | - | - | not designed yet | - |
| hero_pactbinder | man, adult | 95-150 | - | - | - | not designed yet | - |
| boss_gravedigger | old man | 70-120 | 77 | 79 (3) | 0.67 / 0.62 | pass | kept |
| boss_rimelord | man, middle-aged soldier | 75-125 | 103 | 101 (3) | 0.71 / 0.69 | pass | kept |
| boss_bishop | woman, elderly cleric (she in the script) | 175-290 | - | - | - | not designed yet | - |
| boss_mire_mother | woman, middle-aged | 165-235 | - | - | - | not designed yet | - |
| boss_sand_tyrant | man, middle-aged king | 90-150 | - | - | - | not designed yet | - |
| boss_grand_hush | old man | 90-150 | - | - | - | not designed yet | - |
| boss_osaros | man, ageless | 65-125 | 124 | 117 (3) | 0.55 / 0.52 | **failed** (classifier) | kept: chosen by the user by ear on the choice page |
<!-- voice-audit-end -->
