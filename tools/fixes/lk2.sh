#!/bin/bash
until grep -a -q "TR2 DONE" Y:/tools/fixes/tr2.log 2>/dev/null; do sleep 30; done
run() { out=$(bash Y:/tools/fixes/runq.sh $1 -Priority -Headless -GameArgs "--mute --slot=94 --suite=$2 --speed=6" -Timeout 600 -MaxLines 10)
  LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "$1: $(echo "$out" | grep -E '^log:|BUILD')"
  grep -a -E "dps-bases .*(: aps|fixed gear)" "$LOG" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/$1.txt; echo "$1 DONE"; }
run final2_lampknight_30 "dps:lampknight:30,30:bases:30"
run final2_lampknight_50 "dps:lampknight:50,50:bases:30"
echo LK2 DONE
