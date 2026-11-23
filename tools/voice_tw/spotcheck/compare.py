"""Compare whisper_asr.py output with the script: strict CER and sound-based CER (vt.py). voxcpm venv, run from Y:\tools\voice_tw."""
import json, sys
sys.path.insert(0, r"Y:\tools\voice_tw")
import vt
lines = {l["id"]: l for l in json.load(open(vt.VO_LINES, encoding="utf-8"))}
heard = json.load(open(sys.argv[1], encoding="utf-8"))
rows = []
for i, h in heard.items():
    t = lines[i]["text"]
    rows.append((i, lines[i]["voice"], round(vt.cer_strict(t, h), 3), round(vt.cer_sound(t, h), 3), t, h))
for r in rows:
    print(" | ".join(map(str, r)))
print("mean sound-CER", round(sum(r[3] for r in rows) / len(rows), 3), "max", max(r[3] for r in rows), "n", len(rows))
