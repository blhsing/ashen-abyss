#!/usr/bin/env bash
# Frost stronghold (sh_frost) with the AI pilot's boss test, hunter lv24 --gear, both rosters (priority lane, ~4 min each).
L=Y:/tools/roster/logs
for ros in bestiary; do
  for try in $(seq 1 40); do
    while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do sleep 30; done
    res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--mute --$ros --new=hunter --slot=90 --area=frost --level=24 --gear --pilot-test=boss:600 --speed=3 --bossrush" -Headless -Timeout 290 -Tag roster_shf -Priority 2>&1)
    echo "$res" | grep -q "held the lock" && continue
    echo "$res" | grep -q "BUILD FAILED" && { sleep 60; continue; }
    f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
    [ -n "$f" ] && grep -aq "this test launch exits" "$f" && { sleep 60; continue; }
    break
  done
  f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-); [ -n "$f" ] && cp "$f" "$L/shfrost2_$ros.log"
  echo "== $ros $(echo "$res" | grep -o 'ERR=[0-9]*' | head -1)" >> $L/shfrost2.txt
  grep -a "TEST FIGHT\|TEST RESULT\|TEST fight ends" "$L/shfrost2_$ros.log" | cut -c1-300 >> $L/shfrost2.txt
  mv "$APPDATA"/AshenAbyss/saves/slot90.* "$APPDATA"/AshenAbyss/saves/slot0.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
done
echo "done $(date +%T)" >> $L/shfrost2.txt
