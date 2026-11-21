#!/bin/bash
# Demo A/B: before (--gear-off) vs after. Usage: bash perf_ab.sh <tag>
for mode in off on; do
  extra=""; [ "$mode" = off ] && extra="--gear-off"
  tasklist | grep -qi AshenAbyss && { echo "user game running, abort"; exit 1; }
  powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -NoBuild -GameArgs "--win --mute --demo --demo-secs=25 --quit-at=140 --seed=7 $extra" -Timeout 240 -Tag render_perf_$mode > Y:/tools/gear/render/perf_$mode.txt 2>&1
  L=$(ls -t /c/Users/blhsi/AppData/Roaming/AshenAbyss/logs/game_*.log | while read f; do grep -q "userargs=\[--win --mute --demo --demo-secs=25 --quit-at=140 --seed=7 $extra\]" "$f" && { echo "$f"; break; }; done)
  echo "== $mode: $L" >> Y:/tools/gear/render/perf_ab_result.txt
  grep -h "\[perf\]\|\[gear\] hero_\|ERR " "$L" | cut -c1-330 >> Y:/tools/gear/render/perf_ab_result.txt
done
echo done >> Y:/tools/gear/render/perf_ab_result.txt
