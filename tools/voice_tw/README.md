# voice_tw — Taiwanese-Mandarin voice-over for 《燼淵 Ashen Abyss》

Production voice-over made with **VoxCPM2 voice design** (OpenBMB, Apache-2.0): 23 voices invented from written
descriptions, **no reference audio and nobody's voice**. Output: `Y:\game\assets\audio\vo\<id>.ogg`
(mono 44.1 kHz OGG Vorbis, about -17 LUFS, true peak under -1 dBFS, 60 ms of padding).
Numbers for every line: `report.json` in this folder. Credits: `Y:\credits\voice_tw.md`.
Model comparison and the first listening test: `DECISION.md`, `Y:\voice_samples\`.
The final boss's voice variants and the consistency demonstration: `Y:\voice_samples\osaros\index.html`.

Nothing here touches `Y:\tools\voice` (the C# Kokoro fallback tool).

## How a voice stays the same (no reference recording)

Voice design invents a new voice every time it is given a description. So:

1. `voices.py` holds one description per character and one **anchor sentence**. The description is used
   exactly once: the model speaks the anchor (`stage\prod\anchors\<voice>.wav`). The design is measured and
   repeated (new seed = new voice, up to four times) until the anchor reads back exactly and its median pitch is
   inside the range wanted for that character — a description alone does not guarantee a register.
2. Every line of that character is generated as a **continuation of its own anchor** (the anchor's audio and
   text are the prompt, the anchor's audio is also the reference). The anchor is the model's own invention.
3. Each dry take is read back by a recogniser (SenseVoice); a take that does not match is repeated with another
   seed (up to three, then with Traditional input, then with fewer hints). Limit: error rate 0.15.
4. Speaker similarity (CAM++ embeddings) of every line to its anchor is measured; lines that drift are
   regenerated (`gen.py drift --fix`). Results per character are in `report.json` → `consistency`.

Measured in the demonstration (`Y:\voice_samples\osaros`): three lines made this way are 0.83-0.86 similar to each
other; the same lines made from the description alone 0.45-0.57 (the same fixed voice scores 0.85-0.95,
different voices 0.26 on average).

## Every voice must measure as its character (audit)

A description does not guarantee the voice: the first elder ("old man") was designed at 172 Hz and was heard as an
elderly woman; a "burly middle-aged man" came out at 276 Hz. So every design passes gates before it is used:

1. the anchor reads back exactly;
2. **median pitch** inside the range of the character (`voices.py`, `f0`: men at most 150 Hz, the elder 95-140 Hz,
   women at least 165 Hz), and the first three lines within 10 % of it;
3. **male / female classifier** (`gender.py`): CAM++ speaker embeddings + logistic regression, calibrated on the 100
   labelled speakers of the Kokoro model (99 % cross-validated there). On VoxCPM2 voices a man must score at least
   0.60, a woman at most 0.45;
4. harmonics-to-noise ratio above the floor of the character (bosses 5.5 dB: clean, not coarse);
5. not too close to another character's voice (speaker similarity at most 0.75).

A voice that fails is designed again with other seeds, and everything generated from the rejected anchor is
discarded. The result for every character - including rejected designs - is the table "Voice audit" in
`DECISION.md`, rewritten by `gen.py` (`gen.py audit` re-measures what exists). The final boss's voice was chosen
by the user by ear and is exempt from automatic redesign (`USER_APPROVED` in `gen.py`); its effect strength is
fixed (`LOCKED`): a line that does not read back at that strength gets a new take, never a weaker chain.

## New lines in vo_lines.json (incremental)

Run `gen.py all` again whenever the script changes. It generates only ids that have no take yet, or whose text or
voice changed (a take remembers its text and the anchor it came from); processed files are re-made only when their
take, chain or strength changed; `deliver` copies only files that differ from what is in the game folder.
A `boss_*` voice name that is not in `voices.py` gets a neutral boss voice of its own, designed automatically
(`voices.ensure`), and the neutral chain `boss_generic` - give it a proper description when its character is known.
`check_ids.py` compares the ids the game code can request (`Y:\game\src\Dialogue.cs`) with `vo_lines.json` and with
the audio in the game folder.

## Pronunciation (Taiwan readings)

VoxCPM2 reads mainland readings unless told otherwise (measured). `tw_text.py` gives it hints inside the text
it reads — one character replaced by its reading, e.g. 寂 → `{ji2}` — at four levels: Taiwan MOE readings
(寂 熄 息 惜 血 誰 薇 曙 薄, 和 as ㄏㄢˋ), full tones where the mainland uses a neutral tone (故事, 地方, 知道, 時候 …),
context-dependent polyphones (還 得 著 長 重 行 數 背 …), and rare characters in names (菈, 珂, 葛).
The model is given Simplified characters (it slips less on them — measured); the stored script is never changed.
To change a reading, edit the tables in `tw_text.py` and regenerate the lines concerned.

## Commands

All commands run inside the `voxcpm` environment through `x.ps1` (sets cache paths, UTF-8, no telemetry):

```powershell
$x  = 'Y:\tools\voice_tw\x.ps1'
$py = "$env:LOCALAPPDATA\AshenAbyssTools\voice_tw\venvs\voxcpm\Scripts\python.exe"
$g  = 'Y:\tools\voice_tw\gen.py'

powershell -ExecutionPolicy Bypass -File $x $py $g all                       # everything that is missing, then deliver
powershell -ExecutionPolicy Bypass -File $x $py $g lines --only elder_first_02 --force     # ONE line again (new take)
powershell -ExecutionPolicy Bypass -File $x $py $g fx --only elder_first_02               # ... process it
powershell -ExecutionPolicy Bypass -File $x $py $g report                                 # ... refresh report.json
powershell -ExecutionPolicy Bypass -File $x $py $g deliver --only elder_first_02          # ... copy it into the game
powershell -ExecutionPolicy Bypass -File $x $py $g lines --voice smith --force            # all lines of one character
powershell -ExecutionPolicy Bypass -File $x $py $g drift --fix                            # re-check consistency, repair drift
```

`gen.py all` works one voice at a time, start to finish (anchor, lines, consistency repair, effects, delivery,
report), so finished voices are already in the game while later ones are still being generated. It is resumable:
stop it at any time (`RUNNING.txt` in this folder names the process and the log while a batch is running) and start
it again; finished anchors and lines are skipped. Log: `...\voice_tw\logs\gen.log`. Progress at a glance:

```powershell
powershell -ExecutionPolicy Bypass -File Y:\tools\voice_tw\status.ps1
```
The fallback tool's files in `Y:\game\assets\audio\vo` are only overwritten for lines that passed every check;
`deliver` waits while the fallback generator (`voice.exe` / `dotnet ... tools\voice`) is running.

### Re-apply a boss effect chain from the dry takes (seconds, no speech generation)

Every line's dry take is kept in `stage\prod\dry\<id>.wav`. The chains are in `profiles.py`.

```powershell
powershell -ExecutionPolicy Bypass -File $x $py $g fx --voice boss_osaros --strength light    # dry | light | medium
powershell -ExecutionPolicy Bypass -File $x $py $g report
powershell -ExecutionPolicy Bypass -File $x $py $g deliver --voice boss_osaros
```

The chosen strength per boss is remembered in `boss_fx.json`. After editing a chain in `profiles.py`, run the
same three commands without `--strength`. Boss lines whose processed file no longer reads back (error above 0.15)
are automatically rendered with the next weaker chain; `report.json` shows the strength actually used.

### Another description for the final boss

`Y:\voice_samples\osaros\index.html` offers three descriptions. To switch: set `OSAROS_DEFAULT` in `voices.py`, then

```powershell
powershell -ExecutionPolicy Bypass -File $x $py $g osaros --variant low_soft --strength medium
powershell -ExecutionPolicy Bypass -File $x $py $g lines --voice boss_osaros
powershell -ExecutionPolicy Bypass -File $x $py $g fx --voice boss_osaros
powershell -ExecutionPolicy Bypass -File $x $py $g report
powershell -ExecutionPolicy Bypass -File $x $py $g deliver --voice boss_osaros
```

### A new voice for a character

Edit its description in `voices.py`, delete `stage\prod\anchors\<voice>.*`, then `gen.py lines --voice <voice> --force`,
`gen.py fx --voice <voice>`, `gen.py report`, `gen.py deliver --voice <voice>`.

## Speed and the GPU

Everything ran on the **CPU with 6 threads** (PyTorch 2.8.0, float32): the GPU was occupied by the music
generation job for the whole run (`Y:\tools\music_ai\RUNNING.txt`), and two GPU jobs must not share this
integrated GPU. Measured speed is in `report.json` → `speed`.

A GPU route exists on this machine and is documented by the music engineer in
`%LOCALAPPDATA%\AshenAbyssTools\GPU_NOTES.md`: AMD's PyTorch wheels for ROCm 7.1.1 (torch 2.9.0, Python 3.12) work
with the installed driver (Adrenalin 25.20.32.06) as device `"cuda"`, bfloat16 for large transformers. It was
**not** tried for VoxCPM2 because the GPU was busy; note that those notes also measure an autoregressive model
(Whisper) running far slower on that GPU than on the CPU because of per-operation overhead, and VoxCPM2 is
autoregressive too. To try it later: a second venv per the recipe in those notes, `pip install voxcpm --no-deps`
plus `req\voxcpm.txt` without the torch lines, `device="cuda"` in `engine.py`, and validate three lines against
the CPU takes (error rate, duration, no NaN) before using it. No driver update or reboot was done.

## Where things live

| What | Where |
|---|---|
| Scripts (this folder) | `Y:\tools\voice_tw\` |
| Python, venvs, weights, caches, staging, logs | `%LOCALAPPDATA%\AshenAbyssTools\voice_tw\` (see note) |
| Production staging | `...\voice_tw\stage\prod\` : `anchors\`, `dry\`, `final\` |
| Listening tests | `Y:\voice_samples\` and `Y:\voice_samples\osaros\` |

Note on the path: the tools were started from inside the Claude desktop app, a packaged (MSIX) app. Windows redirects
such an app's writes under `%LOCALAPPDATA%`, so on disk the folder is really
`%LOCALAPPDATA%\AshenAbyssTools\voice_tw\`.
Processes started from the app see it at the short path; File Explorer shows it at the long one.

Inside it: `python\` (uv-managed CPython 3.10.22 and 3.12.16, 0.12 GB), `venvs\voxcpm` (production: PyTorch 2.8.0
CPU, voxcpm 2.0.3, sherpa-onnx; 2.1 GB), `venvs\cosy` (only its onnxruntime is still used, for the CAM++ speaker
check; 2.6 GB), `venvs\common` (0.29 GB), `venvs\breezy` (first listening test only, 2.6 GB), `models\voxcpm2`
(4.96 GB), `models\cosy3` (4.44 GB; only `campplus.onnx`, 28 MB, is still used), `models\breezy` (2.3 GB, first
listening test only), `wheels\` + `cache\` (1.6 GB, safe to delete), `src\` (git clones for the first test), `stage\`,
`logs\`. Total about 21 GB; everything except `venvs\voxcpm`, `venvs\cosy`, `models\voxcpm2`,
`models\cosy3\campplus.onnx`, `python\` and `stage\prod` can be deleted without affecting production.

## Rebuild the environment from nothing

```powershell
powershell -ExecutionPolicy Bypass -File Y:\tools\voice_tw\install.ps1 common voxcpm cosy
$c = "$env:LOCALAPPDATA\AshenAbyssTools\voice_tw\venvs\common\Scripts\python.exe"
powershell -ExecutionPolicy Bypass -File Y:\tools\voice_tw\x.ps1 $c Y:\tools\voice_tw\fetch_fast.py voxcpm2 cosy3
```

Anonymous downloads from PyPI, download.pytorch.org and huggingface.co; afterwards everything runs offline.
(`install.ps1 breezy` and `fetch_fast.py breezy` are only needed to rebuild the first listening test.)

## Files

| File | Purpose |
|---|---|
| `gen.py` | production: anchors, lines, drift check, effects, report, delivery |
| `engine.py` | VoxCPM2 engine: design, continuation, ASR round-trip with retries, staging, speaker embeddings |
| `voices.py` | the 23 voice descriptions, anchor sentences, pitch ranges, the final boss's variants |
| `tw_text.py` | Taiwan readings and hint tables; TTS input for each model |
| `profiles.py`, `fx.py` | effect chains per voice; DSP (formant-preserving pitch shift, layers, EQ, compression, reverb, loudness, limiter) |
| `vt.py`, `spk.py`, `accent_probe.py` | audio I/O, error rates, ASR, pitch / harmonics-to-noise; CAM++ speaker embeddings; accent indicators |
| `osaros_page.py` | builds `Y:\voice_samples\osaros` (boss variants and the consistency demonstration) |
| `report.json`, `boss_fx.json` | per-line results; chosen effect strength per boss |
| `env.ps1`, `x.ps1`, `install.ps1`, `req\`, `pget.py`, `fetch_fast.py`, `fetch_models.py`, `cpu_cap.py`, `stubs.py`, `_ipv4only.py` | environment and downloads |
| `samples_*.py`, `synth_breezy.py`, `synth_cosy3.py`, `synth_voxcpm2.py`, `kokoro_tts.py` | the first listening test (`Y:\voice_samples\index.html`) |
| `exp_pace.py`, `exp_tts.py`, `profile_breezy.py`, `ps_list.ps1`, `kill_mine.ps1`, `net_rate.ps1`, `cpu_top.ps1`, `hf_info.py` | experiments and diagnostics |

## Remove everything

```powershell
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\AshenAbyssTools\voice_tw"      # from a shell started inside the app
# or, from a normal shell / File Explorer:
Remove-Item -Recurse -Force "%LOCALAPPDATA%\AshenAbyssTools\voice_tw"
Remove-Item -Recurse -Force Y:\tools\voice_tw, Y:\voice_samples               # scripts and listening tests
```

The delivered `.ogg` files in `Y:\game\assets\audio\vo` and `Y:\credits\voice_tw.md` are not touched by this.
No system-wide setting, driver or PATH entry was changed; nothing was installed outside those folders
(uv itself was already on the machine).
