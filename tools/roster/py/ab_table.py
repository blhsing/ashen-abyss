# Table of the per-region A/B logs (last [roster] running line + bot deaths/kills).
import re, sys, os
sys.stdout.reconfigure(encoding="utf-8")
L = "Y:/tools/roster/logs"
print(f"{'region':8} {'roster':8} {'normal':>7} {'champ':>7} {'elite':>7} {'boss':>7} {'bars/m':>7} {'kills':>6} {'deaths':>6}  slowest normal types")
for reg in sys.argv[1:]:
    for ros in ("bestiary", "no-bestiary"):
        p = f"{L}/ab_{reg}_{ros}.log"
        if not os.path.exists(p): continue
        txt = open(p, encoding="utf-8", errors="replace").read()
        runs = re.findall(r"\[roster\] running area=\S+ hero=lv\d+ (.*)", txt)
        fin = re.findall(r"final summary .*?kills=(\d+).*?deaths=(\d+)", txt)
        if not runs: print(reg, ros, "no data"); continue
        r = runs[-1]
        g = lambda k: (re.search(k + r"=([\d.-]+)s?\(n=(\d+)\)", r) or [None, "-", "0"])
        m = {k: re.search(k + r"=([\d.-]+)s?\(n=(\d+)\)", r) for k in ("normal", "champion", "elite", "boss")}
        v = {k: (m[k].group(1) if m[k] else "-") for k in m}
        bars = re.search(r"\(([\d.]+) life bars/min", r).group(1)
        slow = (re.search(r"slowest \[(.*?)\]", r) or [None, ""])[1]
        k, d = fin[-1] if fin else ("?", "?")
        print(f"{reg:8} {ros[:8]:8} {v['normal']:>7} {v['champion']:>7} {v['elite']:>7} {v['boss']:>7} {bars:>7} {k:>6} {d:>6}  {slow[:110]}")
