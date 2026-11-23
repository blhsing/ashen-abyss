# Per-region table from the [roster] stats lines of one or more campaign logs.
#   uv run python -I Y:/tools/roster/campstats.py <log> [<log> ...]
import re, sys
from collections import defaultdict
sys.stdout.reconfigure(encoding="utf-8")
REG = ["wastes", "frost", "crypt", "mire", "sands", "sanctum", "abyss"]
pat = re.compile(r"\[roster\] stats area=(\S+) roster=(\S+) hero=lv(\d+) ttk normal=([\d.-]+)s?\(n=(\d+)\) champion=([\d.-]+)s?\(n=(\d+)\) elite=([\d.-]+)s?\(n=(\d+)\) boss=([\d.-]+)s?\(n=(\d+)\) \| taken (\d+)/min \(([\d.]+) life bars/min, (\d+) hits\) kills=(\d+) in (\d+)s")
for path in sys.argv[1:]:
    acc = defaultdict(lambda: defaultdict(float))
    roster = "?"
    for line in open(path, encoding="utf-8", errors="replace"):
        m = pat.search(line)
        if not m: continue
        area, roster = m.group(1), m.group(2)
        reg = next((r for r in REG if area.startswith(r)), "rift" if area == "rift" else "other")
        if area.endswith("_boss"): reg += "/boss"
        a = acc[reg]
        secs = float(m.group(16))
        for k, (t, n) in {"normal": (4, 5), "champion": (6, 7), "elite": (8, 9), "boss": (10, 11)}.items():
            n_ = int(m.group(n))
            if n_ and m.group(t) != "-": a[k + "_s"] += float(m.group(t)) * n_; a[k + "_n"] += n_
        a["bars"] += float(m.group(13)) * secs / 60.0
        a["secs"] += secs; a["kills"] += int(m.group(15))
    print(f"== {path} (roster {roster})")
    print(f"{'region':14} {'min':>5} {'kills':>6} {'ttk norm':>9} {'champ':>7} {'elite':>7} {'bars/min':>8}")
    tot = defaultdict(float)
    for reg in sorted(acc, key=lambda r: (REG.index(r.split('/')[0]) if r.split('/')[0] in REG else 99, r)):
        a = acc[reg]
        for k, v in a.items(): tot[k] += v
        f = lambda k: f"{a[k+'_s']/a[k+'_n']:.1f}" if a[k + "_n"] else "-"
        print(f"{reg:14} {a['secs']/60:5.1f} {int(a['kills']):6d} {f('normal'):>9} {f('champion'):>7} {f('elite'):>7} {a['bars']/max(a['secs']/60,0.05):8.2f}")
    a = tot; f = lambda k: f"{a[k+'_s']/a[k+'_n']:.1f}" if a[k + "_n"] else "-"
    print(f"{'ALL':14} {a['secs']/60:5.1f} {int(a['kills']):6d} {f('normal'):>9} {f('champion'):>7} {f('elite'):>7} {a['bars']/max(a['secs']/60,0.05):8.2f}")
