# Creature check table from the suite logs (last row per model wins). uv run python -I cr_table.py <log> [...]
import re, sys
sys.stdout.reconfigure(encoding="utf-8")
rows, res = {}, {}
for p in sys.argv[1:]:
    for line in open(p, encoding="utf-8", errors="replace"):
        m = re.search(r"\[suite\] creature (mon_\w+) \((\w+), rig (\w)([^)]*)\) scale=([\d.]+).*?idle low=([-\d.]+) high=([\d.]+) metaH=([\d.]+) ratio=([\d.]+) walk low=(\S+) hit=(\w+)\(([^)]*\)?)\)? frozen=([\d.]+)s death=(\w+) dead low=([-\d.]+) high=([\d.]+) removed=(\S+)", line)
        if m: rows[m.group(1)] = m.groups()
        r = re.search(r"(PASS|FAIL) creature (mon_\w+)", line)
        if r: res[r.group(2)] = r.group(1)
print("| model | rig | scale | height in game / meta | idle lowest m | walk lowest m | hit reaction | legs frozen s | death clip | corpse lowest m | body gone | result |")
print("|---|---|---|---|---|---|---|---|---|---|---|---|")
for k in sorted(rows):
    g = rows[k]
    rig = g[2] + g[3]
    print(f"| {k[4:]} | {rig} | {g[4]} | {g[6]} / {g[7]} (×{g[8]}) | {g[5]} | {g[9]} | {g[10]} ({g[11].rstrip(')')}) | {g[12]} | {g[13]} | {g[14]} | {g[16]} | {res.get(k,'?')} |")
