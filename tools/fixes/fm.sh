#!/bin/bash
# Forgebody 拳刃精通 + Lampknight 盾誓/燈誓: before (all forced to 0) / after; 3 samples at levels 10, 30, 50
for mode in "$@"; do
  extra=""; [ "$mode" = before ] && extra="--fistmastery=0 --shieldoath=0 --lanternoath=0"
  out=$(bash Y:/tools/fixes/runq.sh fixes_fm_$mode -Headless -GameArgs "--mute --slot=94 --suite=dps:forgebody,lampknight:10,10,10,30,30,30,50,50,50:bases:30 --speed=4 $extra" -Timeout 3000 -MaxLines 10)
  L=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  echo "$out" | grep -E "^log:|BUILD"
  grep -a -E "dps-bases .*: aps" "$L" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/fm_$mode.txt
  echo "FM $mode DONE $(wc -l < Y:/tools/fixes/fm_$mode.txt) lines"
done
