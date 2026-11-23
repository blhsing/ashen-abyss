#!/bin/bash
# mace2 overpower: 2H sets at 120 s windows, lv50, 2 samples, with and without overpower (lampknight, wildsoul, emberblade)
for mode in with without; do
  extra="--fistmastery=0 --shieldoath=0 --lanternoath=0"; [ "$mode" = without ] && extra="$extra --no-overpower"
  out=$(bash Y:/tools/fixes/runq.sh fixes_op_$mode -Headless -GameArgs "--mute --slot=94 --suite=dps:lampknight,wildsoul,emberblade:50,50:bases:120:2h --speed=4 $extra" -Timeout 4000 -MaxLines 10)
  L=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  echo "$out" | grep -E "^log:"
  grep -a -E "dps-bases .*: aps" "$L" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/op_$mode.txt
  echo "OP $mode DONE $(wc -l < Y:/tools/fixes/op_$mode.txt) lines"
done
