r"""Checks that every UI art piece the C# sources name exists in the manifest and on disk (x1, x2, imported).
   python.exe -I Y:\tools\uiart\refcheck.py
Looks for string literals passed to UiArt.P / Has / Icon / Field, UiTheme.ArtBox and new UiNine(...)."""
import os, re, json, glob
SRC = r"Y:\game\src"
UI = r"Y:\game\assets\ui"
man = json.load(open(os.path.join(UI, "manifest.json"), encoding="utf-8"))
pieces = set(man["pieces"])
tiles = set(man["tiles"])
pat_piece = re.compile(r'(?:UiArt\.(?:P|Has|Icon)|new UiNine|PutArt\([^,]+,[^,]+,)\s*\(?\s*"([a-z0-9_]+)"')
pat_artbox = re.compile(r'ArtBox\(\s*"([a-z0-9_]+)"\s*,\s*[0-9.\-]')
pat_field = re.compile(r'UiArt\.Field\(\s*"([a-z0-9_]+)"')
pat_nine_field = re.compile(r'new UiNine\(\s*"[a-z0-9_]+"\s*,\s*"([a-z0-9_]+)"')
pat_artbox_field = re.compile(r'ArtBox\(\s*"[a-z0-9_]+"\s*,\s*[0-9.f\-]+\s*,\s*[0-9.f\-]+\s*,\s*"([a-z0-9_]+)"')
pat_fmt = re.compile(r'UiArt\.(?:P|Has)\(\s*\$?"([a-z0-9_{}.]+)"')
missing = {}
used = set()
for f in sorted(glob.glob(os.path.join(SRC, "*.cs"))):
    s = open(f, encoding="utf-8", errors="replace").read()
    name = os.path.basename(f)
    for m in list(pat_piece.finditer(s)) + list(pat_artbox.finditer(s)):
        p = m.group(1)
        used.add(p)
        if p not in pieces and not p.startswith("field_"):
            missing.setdefault(p, set()).add(name)
    for m in list(pat_field.finditer(s)) + list(pat_nine_field.finditer(s)) + list(pat_artbox_field.finditer(s)):
        t = m.group(1)
        if t not in tiles:
            missing.setdefault("field:" + t, set()).add(name)
print("pieces in manifest:", len(pieces), "tiles:", len(tiles), "| distinct literal piece names used in code:", len(used))
for p, files in sorted(missing.items()):
    print("  MISSING", p, "<-", ", ".join(sorted(files)))
bad = 0
for p in sorted(pieces) + ["field_" + t for t in sorted(tiles)]:
    for d in ("x1", "x2"):
        png = os.path.join(UI, d, p + ".png")
        if not os.path.exists(png): print("  NO FILE", png); bad += 1
        elif not os.path.exists(png + ".import"): print("  NOT IMPORTED", png); bad += 1
print("files missing / not imported:", bad)
print("unused pieces:", ", ".join(sorted(pieces - used)))
