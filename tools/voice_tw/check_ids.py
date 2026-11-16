"""Compare the voice line ids the GAME can request with Y:\\game\\data\\vo_lines.json and with the audio on disk.

    python check_ids.py            (any of the tool's Python environments)

Sources read (read-only): Y:\\game\\src\\Dialogue.cs - every line is declared as L("profile", "id", "text") - and
the other C# files for ids that are built at run time (boss "<id>_intro/_phase/_death", hero barks, pools).
Prints: ids declared in code but missing in the JSON (and the reverse), profile or text differences, ids
requested at run time that no declaration provides, and declared ids that have no .ogg in the game yet.
"""
import json
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
SRC = r"Y:\game\src"
JSON = r"Y:\game\data\vo_lines.json"
VO = r"Y:\game\assets\audio\vo"


def cs_unescape(s: str) -> str:
    return s.replace('\\"', '"').replace("\\\\", "\\")


def main():
    code = open(os.path.join(SRC, "Dialogue.cs"), encoding="utf-8").read()
    decl = {}
    for m in re.finditer(r'\bL\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"\s*\)', code):
        decl[cs_unescape(m.group(2))] = dict(voice=cs_unescape(m.group(1)), text=cs_unescape(m.group(3)))
    data = json.load(open(JSON, encoding="utf-8"))
    js = {l["id"]: l for l in data}
    print(f"Dialogue.cs declares {len(decl)} lines; vo_lines.json has {len(js)} (unique ids {len(set(l['id'] for l in data))})")
    only_code = sorted(set(decl) - set(js))
    only_json = sorted(set(js) - set(decl))
    print("declared in code, missing in vo_lines.json:", only_code or "none")
    print("in vo_lines.json, not declared in code:   ", only_json or "none")
    diff_voice = [(i, decl[i]["voice"], js[i]["voice"]) for i in decl if i in js and decl[i]["voice"] != js[i]["voice"]]
    diff_text = [(i, decl[i]["text"], js[i]["text"]) for i in decl if i in js and decl[i]["text"] != js[i]["text"]]
    print("voice profile differs (id, code, json):", diff_voice or "none")
    print("text differs:", len(diff_text))
    for i, a, b in diff_text[:20]:
        print("   ", i, "| code:", a, "| json:", b)

    # ids built at run time
    all_code = ""
    for f in sorted(os.listdir(SRC)):
        if f.endswith(".cs"):
            all_code += "\n" + open(os.path.join(SRC, f), encoding="utf-8").read()
    boss_ids = sorted(set(re.findall(r'"(boss_[a-z_]+?)"', all_code)) - {"boss_"})
    boss_ids = [b for b in boss_ids if not re.search(r"_(intro|phase|death)$", b)]
    classes = sorted(set(re.findall(r'"hero_([a-z]+)_', code)))
    hero_what = sorted(set(re.findall(r'"hero_[a-z]+_([a-z]+)"', code)))
    missing_rt = []
    for b in boss_ids:
        if any((b + s) in decl for s in ("_intro", "_phase", "_death")):
            missing_rt += [b + s for s in ("_intro", "_phase", "_death") if (b + s) not in decl]
    for c in classes:
        missing_rt += [f"hero_{c}_{w}" for w in hero_what if f"hero_{c}_{w}" not in decl]
    print("boss ids named in the code:", boss_ids)
    print("  of these WITHOUT any voice line:", [b for b in boss_ids if (b + "_intro") not in decl] or "none")
    print("hero classes:", classes, "| hero line kinds:", hero_what)
    print("run-time ids that would not resolve:", missing_rt or "none")
    quoted = set(re.findall(r'"((?:intro|ending|elder|smith|merchant|occultist|jeweler|villager|dawn|side)_[a-z0-9_]+)"', all_code))
    unknown = sorted(q for q in quoted if q not in decl and not any(d.startswith(q) for d in decl))
    print("other quoted ids in the code that are neither a line nor a prefix of one:", unknown or "none")

    have = {f[:-4] for f in os.listdir(VO) if f.endswith(".ogg")} if os.path.isdir(VO) else set()
    no_audio = [i for i in js if i not in have]
    print(f"audio in the game folder: {len(have & set(js))} of {len(js)} ids; without audio yet: {len(no_audio)}")
    print("   first without audio:", no_audio[:12])
    stray = sorted(have - set(js))
    print("audio files whose id is not in vo_lines.json:", stray or "none")


if __name__ == "__main__":
    main()
