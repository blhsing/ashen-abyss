# Voice-over credits (燼淵 Ashen Abyss) — Taiwanese-Mandarin voices

Every spoken line in `game/assets/audio/vo/<id>.ogg` is synthetic speech generated offline on the developer's
machine with one openly licensed model. **No recording of any person is used anywhere**: no voice actor, no
reference clip, no voice cloning. All 32 voices are *designed* — invented by the model from a short written
description of each character (listed in `tools/voice_tw/voices.py`).

## Speech model — VoxCPM2

| | |
|---|---|
| model | VoxCPM2 (2 B parameters, 48 kHz, tokenizer-free diffusion-autoregressive TTS), used in its "voice design" mode |
| by | OpenBMB |
| authors | Yixuan Zhou, Guoyang Zeng, Xin Liu, Xiang Li, Renjie Yu, Jiancheng Gui, Jiaheng Wu, Ziyang Wang, Xudong Shen, Runchuan Ye, Zhisheng Zhang, Jiuyang Zhou, Bingsong Bai, Weiyue Sun, Mengyuan Deng, Qundong Shi, Zhiyong Wu, Zhiyuan Liu |
| paper | "VoxCPM2 Technical Report", arXiv:2606.06928 (2026); first version: "VoxCPM: Tokenizer-free TTS for context-aware speech generation and true-to-life voice cloning", arXiv:2509.24650 (2025) |
| code | https://github.com/OpenBMB/VoxCPM — Python package `voxcpm` 2.0.3 from PyPI — **Apache-2.0** |
| weights | https://huggingface.co/openbmb/VoxCPM2 (`model.safetensors`, `audiovae.pth`; 4.96 GB) — **Apache-2.0** |
| how it was used | unmodified code and weights, CPU inference (PyTorch 2.8.0, float32). Each character's description is spoken once (its "anchor" sentence); every line of that character continues its own anchor, so the voice stays the same. The anchors are the model's own output. |

The README states that weights and code are free for commercial use, forbids impersonation, fraud and
disinformation, and recommends marking AI-generated content clearly; this file is that marking for the game.

## How the lines were made

- Script: `game/data/vo_lines.json` (207 lines, 32 voices: narrator, 7 town characters, 8 heroes, 16 bosses and named foes), Traditional Chinese, original writing for this game.
- Pronunciation: Taiwan readings (教育部《重編國語辭典修訂本》) are given to the model as hints in the text it reads
  (e.g. 寂 ㄐㄧˊ, 血 ㄒㄧㄝˇ, 熄 ㄒㄧˊ, 誰 ㄕㄟˊ, 薇 ㄨㄟˊ, full tones in 故事 / 地方 / 知道). The stored script is not changed.
  Table: `tools/voice_tw/tw_text.py`.
- Post-processing is the project's own code (`tools/voice_tw/fx.py`, `profiles.py`; numpy / scipy): light EQ,
  gentle compression and a little room for human characters; for the bosses and named foes a clarity-first chain (small
  formant-preserving pitch-down, quiet octave-down layer, reverb behind the voice). Loudness about -17 LUFS,
  true peak under -1 dBFS, mono 44.1 kHz OGG Vorbis.
- Every designed voice is measured before its lines are used (median pitch in the character's range, a male / female
  speaker classifier, harmonics-to-noise ratio above a floor, distinct from the other voices); voices that failed were
  designed again from a new description. Results: the "Voice audit" table in `tools/voice_tw/DECISION.md`.
- Generator, measurements and how to regenerate: `tools/voice_tw/README.md`, `tools/voice_tw/report.json`.

## Tools used for checking only (nothing of them is in the shipped audio)

| tool | use | source | licence |
|---|---|---|---|
| SenseVoice-Small (int8 ONNX export by k2-fsa) through `sherpa-onnx` | reads every generated line back; lines that do not match the script are regenerated | https://github.com/FunAudioLLM/SenseVoice, https://github.com/k2-fsa/sherpa-onnx | model: FunASR model licence (see `assets_src/voice/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17/LICENSE`); sherpa-onnx: Apache-2.0 |
| CAM++ speaker encoder (`campplus.onnx` from the CosyVoice 3 weights) | measures that all lines of a character are the same voice | https://huggingface.co/FunAudioLLM/Fun-CosyVoice3-0.5B-2512 | Apache-2.0 |
| PyTorch, NumPy, SciPy, soundfile / libsndfile + libvorbis (OGG encoding), pyloudnorm, pypinyin, OpenCC (Python reimplementation) | runtime, DSP, loudness, text handling | PyPI | BSD-3-Clause / BSD / LGPL-2.1 (libsndfile, used as a separate library) / BSD (libvorbis) / MIT / MIT / Apache-2.0 |

## Evaluated but not used in the game

For the first listening test (`voice_samples/`, not shipped) the following were also run: BreezyVoice (MediaTek
Research, Apache-2.0), Fun-CosyVoice3-0.5B-2512 (FunAudioLLM, Apache-2.0) and Kokoro v1.1-zh (hexgrad,
Apache-2.0, the earlier fallback voices). Some of those test samples used the example prompt bundled with the
BreezyVoice repository as a reference clip; **none of that material is in the game** — the user chose VoxCPM2
voice design with no reference audio.
