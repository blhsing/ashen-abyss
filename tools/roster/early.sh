#!/usr/bin/env bash
# Early-gentle verification: wastes lv1 / lv3 without gear (both rosters), then the pilot's Pactbinder first 600 s (slot 90).
L=Y:/tools/roster/logs; export GEARSW=""
rm -f $L/ab9_*.txt
bash Y:/tools/roster/area_ab3.sh wastes 1 emberblade ab9_w1
bash Y:/tools/roster/area_ab3.sh wastes 3 emberblade ab9_w3
for try in $(seq 1 40); do
  while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do sleep 30; done
  res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--mute --new=pactbinder --slot=90 --pilot-test=campaign:600 --speed=4" -Headless -Timeout 290 -Tag roster_pact -Priority 2>&1)
  echo "$res" | grep -q "held the lock" && continue
  f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  [ -n "$f" ] && grep -aq "this test launch exits" "$f" && { sleep 60; continue; }
  break
done
f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-); cp "$f" $L/pact600.log
mv "$APPDATA"/AshenAbyss/saves/slot90.* "$APPDATA"/AshenAbyss/saves/slot0.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
echo "early done $(date +%T)" > $L/early_done.txt
