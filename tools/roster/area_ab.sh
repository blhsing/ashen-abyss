#!/usr/bin/env bash
# Short area A/B (both rosters, same seed): bot --focus, headless, priority lane (≤ 5 min each).
#   bash area_ab.sh <area> <level> <class> [tag]
area=$1; lv=$2; cls=$3; tag=${4:-ab_$area}; L=Y:/tools/roster/logs
for ros in bestiary no-bestiary; do
  for try in $(seq 1 40); do
    while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do sleep 30; done
    res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--mute --$ros --new=$cls --slot=90 --seed=777 --area=$area --level=$lv --gear --bot --focus --speed=4 --quit-at=150" -Headless -Timeout 290 -Tag roster_ab -Priority 2>&1)
    echo "$res" | grep -q "held the lock" && continue
    echo "$res" | grep -q "BUILD FAILED" && { sleep 60; continue; }
    break
  done
  f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-); [ -n "$f" ] && cp "$f" "$L/${tag}_$ros.log"
  echo "== $ros $(echo "$res" | grep -o 'ERR=[0-9]*' | head -1)" >> $L/${tag}.txt
  grep -a "\[roster\] running\|\[roster\] stats\|\[roster\] totals\|bot\] .*death\|died" "$L/${tag}_$ros.log" | cut -c1-700 | tail -6 >> $L/${tag}.txt
  mv "$APPDATA"/AshenAbyss/saves/slot90.* "$APPDATA"/AshenAbyss/saves/slot0.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
done
echo "done $(date +%T)" >> $L/${tag}.txt
