"""Compare per-area monster counts of two census logs: python density.py <old.log> <new.log>"""
import re, sys
from collections import defaultdict
sys.stdout.reconfigure(encoding="utf-8")


def read(path):
    per = defaultdict(list)
    for line in open(path, encoding="utf-8", errors="replace"):
        m = re.search(r"\[census\] area=(\S+) mlvl=(\d+) monsters=(\d+) packs=(\d+) champions=(\d+) elites=(\d+)", line)
        if m:
            area = m.group(1)
            if area.startswith("rift"):
                area = "rift"
            per[area].append((int(m.group(3)), int(m.group(4)), int(m.group(5)), int(m.group(6))))
    return per


a, b = read(sys.argv[1]), read(sys.argv[2])
print(f"{'area':14} {'old n':>7} {'new n':>7} {'ratio':>6} {'old packs':>9} {'new packs':>9} {'old champ':>9} {'new champ':>9}")
to, tn = 0, 0
for area in sorted(set(a) | set(b)):
    def avg(rows, i):
        return sum(r[i] for r in rows) / len(rows) if rows else 0
    o, n = avg(a.get(area, []), 0), avg(b.get(area, []), 0)
    to += o; tn += n
    print(f"{area:14} {o:7.0f} {n:7.0f} {n / o if o else 0:6.2f} {avg(a.get(area, []), 1):9.0f} {avg(b.get(area, []), 1):9.0f} {avg(a.get(area, []), 2):9.1f} {avg(b.get(area, []), 2):9.1f}")
print(f"{'total':14} {to:7.0f} {tn:7.0f} {tn / to if to else 0:6.2f}")
