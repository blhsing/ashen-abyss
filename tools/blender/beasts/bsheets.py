"""Family sheets and game-view sheets of the 46 creatures (plain python, no Blender):
  python.exe -I bsheets.py            -> writes scratch\family_sheets.ps1 (UTF-8 with BOM) and prints the render commands
  powershell -NoProfile -ExecutionPolicy Bypass -File scratch\family_sheets.ps1
     -> Y:\\shots\\chars\\beast_<family>.jpg      3/4 tiles of every creature of a family (400 px tiles, 3 columns)
        Y:\\shots\\chars\\beast_gameview_<n>.jpg  the same creatures as the game camera shows them on dark ground
The tiles come from bshots.py (--tile --game), each creature from the shots folder next to its meta file."""
import os, json, sys
HERE = os.path.dirname(os.path.abspath(__file__))
DIRS = ['out', r'h1\out', r'h2\out', r'h3\out', r'h4\out']
names = json.load(open(os.path.join(HERE, 'out', 'names.json'), encoding='utf8'))
best = json.load(open(r"Y:\game\data\bestiary.json", encoding='utf8'))
fam_zh = {f['id']: f['name_zh'] for f in best['families']}
order = [t['model'] for t in best['types'] if t['rig'] in 'BCDEF']
where = {}
for d in DIRS:
    p = os.path.join(HERE, d)
    if os.path.isdir(p):
        for f in os.listdir(p):
            if f.endswith('.meta.json'):
                where[f[:-10]] = d                       # later folders win (the helpers' builds)
missing = [m for m in order if m not in where]
by_dir = {}
for m in order:
    if m in where:
        by_dir.setdefault(where[m], []).append(m[4:])
print("missing:", missing)
for d, ids in by_dir.items():
    full = os.path.join(HERE, d)
    extra = "" if d == 'out' else " --out=%s\\shots --meta=%s" % (full, full)
    print('RENDER %s: bb.ps1 bshots.py "%s --tile --game%s"' % (d, " ".join(ids), extra))
fams = {}
for m in order:
    fams.setdefault(names[m]['family'], []).append(m)
lines = []
sheet = r'& powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\sheet.ps1'
for fam, ms in fams.items():
    items = ",".join("%s\\shots\\%s_tile.png|%s" % (where.get(m, 'out'), m, names[m]['zh']) for m in ms)
    cols = min(3, len(ms))
    lines.append('%s -Out "Y:\\shots\\chars\\beast_%s.jpg" -Cols %d -Tile 400 -Dir "%s" -Title "%s" -Items "%s"' % (
        sheet, fam, cols, HERE, fam_zh.get(fam, fam), items))
per = 12
for k in range(0, len(order), per):
    ms = order[k:k + per]
    items = ",".join("%s\\shots\\%s_game.png|%s" % (where.get(m, 'out'), m, names[m]['zh']) for m in ms)
    lines.append('%s -Out "Y:\\shots\\chars\\beast_gameview_%d.jpg" -Cols 3 -Tile 400 -Dir "%s" -Title "遊戲視角・暗地 %d/%d" -Items "%s"' % (
        sheet, k // per + 1, HERE, k // per + 1, (len(order) + per - 1) // per, items))
out = os.path.join(HERE, 'scratch', 'family_sheets.ps1')
with open(out, 'w', encoding='utf-8-sig') as f:
    f.write("\n".join(lines) + "\n")
print("wrote", out, len(lines), "sheets;", len(fams), "families")
