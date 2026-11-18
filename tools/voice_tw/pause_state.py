"""Used when the run is paused: validate the delivered files, remove stale old-voice files, write the state.

    python pause_state.py        (voxcpm venv; no speech generation, no ASR)
"""
import json
import os
import sys
import time

import numpy as np
import soundfile as sf

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.environ.get("VT_ROOT", os.path.join(os.environ["LOCALAPPDATA"], "AshenAbyssTools", "voice_tw"))
VO = r"Y:\game\assets\audio\vo"
PROD = os.path.join(ROOT, "stage", "prod")
lines = json.load(open(r"Y:\game\data\vo_lines.json", encoding="utf-8"))
by_id = {l["id"]: l for l in lines}
anchors = {}
for f in os.listdir(os.path.join(PROD, "anchors")):
    if f.endswith(".json") and "@" not in f:
        anchors[f[:-5]] = json.load(open(os.path.join(PROD, "anchors", f), encoding="utf-8"))

removed, bad, good = [], [], []
for f in sorted(os.listdir(VO)):
    if not f.endswith(".ogg"):
        continue
    i, p = f[:-4], os.path.join(VO, f)
    l = by_id.get(i)
    dry = None
    try:
        dry = json.load(open(os.path.join(PROD, "dry", i + ".json"), encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError):
        pass
    fin = None
    try:
        fin = json.load(open(os.path.join(PROD, "final", i + ".json"), encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError):
        pass
    a = anchors.get(l["voice"]) if l else None
    # a delivered file must come from the CURRENT anchor of its voice, and be the staged file byte for byte
    stale = not l or not dry or not a or dry.get("anchor_uid") != a.get("uid") or dry.get("text") != l["text"]
    src = os.path.join(PROD, "final", i + ".ogg")
    same = os.path.exists(src) and open(src, "rb").read() == open(p, "rb").read()
    ok = False
    try:
        x, sr = sf.read(p, dtype="float32")
        ok = sr == 44100 and x.ndim == 1 and len(x) > 4410 and bool(np.all(np.isfinite(x))) and float(np.abs(x).max()) > 0.01 \
            and bool(fin) and abs(len(x) / sr - fin.get("duration", -1)) < 0.05
    except Exception:
        ok = False
    if stale or not ok or not same:
        for q in (p, p + ".import"):
            if os.path.exists(q):
                os.remove(q)
        for d, exts in (("dry", (".wav", ".json")), ("final", (".ogg", ".json"))):
            if stale:
                for ext in exts:
                    q = os.path.join(PROD, d, i + ext)
                    if os.path.exists(q):
                        os.remove(q)
        (removed if stale else bad).append(i)
    else:
        good.append(i)

voices_done = {}
for i in good:
    voices_done.setdefault(by_id[i]["voice"], []).append(i)
per_voice = {v: (len(voices_done.get(v, [])), sum(1 for l in lines if l["voice"] == v)) for v in dict.fromkeys(l["voice"] for l in lines)}
left = [l["id"] for l in lines if l["id"] not in good]
state = dict(time=time.strftime("%Y-%m-%d %H:%M"), with_audio=len(good), total=len(lines), removed_stale=removed, removed_invalid=bad,
             per_voice=per_voice, left=left)
json.dump(state, open(os.path.join(HERE, "pause_state.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)

with open(os.path.join(HERE, "RUNNING.txt"), "w", encoding="utf-8") as f:
    f.write(f"voice_tw: NOT running - the run was stopped on purpose at {state['time']} (prototype build; all work paused).\n"
            f"{len(good)} of {len(lines)} ids have audio in {VO}; nothing is lost, the generator is incremental.\n"
            "Resume (CPU, 6 threads; check Y:\\tools\\music_ai\\RUNNING.txt first - never two GPU jobs, and this run uses the CPU):\n"
            "  powershell -ExecutionPolicy Bypass -File Y:\\tools\\voice_tw\\x.ps1 "
            "\"$env:LOCALAPPDATA\\AshenAbyssTools\\voice_tw\\venvs\\voxcpm\\Scripts\\python.exe\" Y:\\tools\\voice_tw\\gen.py all\n"
            "It continues voice by voice with what is missing (a designed but not yet audited voice is audited first; see DECISION.md).\n"
            "Optional: $env:VT_THREADS=8 before it when no other heavy CPU job runs (default 6).\n"
            "State: Y:\\tools\\voice_tw\\pause_state.json, log: %LOCALAPPDATA%\\AshenAbyssTools\\voice_tw\\logs\\gen.log\n")

dec = os.path.join(HERE, "DECISION.md")
text = open(dec, encoding="utf-8").read()
start, end = "<!-- pause-state-start -->", "<!-- pause-state-end -->"
done_v = [f"{v} {a}/{b}" for v, (a, b) in per_voice.items() if a]
todo_v = [f"{v} {a}/{b}" for v, (a, b) in per_voice.items() if a < b]
au = json.load(open(os.path.join(PROD, "audit.json"), encoding="utf-8")) if os.path.exists(os.path.join(PROD, "audit.json")) else {}
audited = [v for v in anchors if v in au]
unaudited = [v for v in anchors if v not in au]
block = "\n".join([start, f"## State when the run was paused ({state['time']})", "",
                   f"- **{len(good)} of {len(lines)} ids have audio** in `Y:\\game\\assets\\audio\\vo` (every file decoded and checked: mono 44.1 kHz OGG Vorbis, "
                   "complete, identical to the validated staging file, made from the current anchor of its voice).",
                   f"- Voices with audio: {', '.join(done_v)}.",
                   f"- Voices designed and audited so far: {', '.join(audited)}. Audit results: table below.",
                   f"- Anchor designed but not yet audited (the run stopped while making its first lines): {', '.join(unaudited) or 'none'}.",
                   f"- Removed from the game folder because they were takes of a rejected voice: {', '.join(removed) or 'none'}"
                   ".",
                   f"- Still to record: {len(left)} ids - " + ", ".join(todo_v) + ".",
                   "- Resume with `gen.py all` (see `RUNNING.txt`); nothing has to be redone.", end])
if start in text and end in text:
    text = text[:text.index(start)] + block + text[text.index(end) + len(end):]
else:
    marker = "<!-- voice-audit-start -->"
    text = text.replace(marker, block + "\n\n" + marker) if marker in text else text.rstrip("\n") + "\n\n" + block + "\n"
open(dec, "w", encoding="utf-8", newline="\n").write(text)
print(json.dumps(dict(with_audio=len(good), removed_stale=removed, removed_invalid=bad, per_voice={v: f"{a}/{b}" for v, (a, b) in per_voice.items() if a}),
                 ensure_ascii=False))
