#!/bin/bash
# item 3: hunter bot in wastes_d1 (as smoke_020) over fixed seeds; collects stuck warnings
for s in 11 22 33 44 55 66; do
  out=$(bash Y:/tools/fixes/runq.sh fixes_stuck -NoBuild -Headless -GameArgs "--mute --new=hunter --slot=94 --area=wastes_d1 --level=8 --gear --bot --focus --seed=$s --speed=3 --quit-at=150" -Timeout 200)
  echo "$out" | grep -E "ERR=|exit=" | sed "s/^/seed $s: /"
  L=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  [ -n "$L" ] && grep -E "stuck|going nowhere|build area|final summary" "$L" | cut -c1-260 | sed "s/^/seed $s: /"
done
echo STUCKHUNT DONE
