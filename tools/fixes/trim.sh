#!/bin/bash
run() { out=$(bash Y:/tools/fixes/runq.sh $1 -Priority -Headless -GameArgs "--mute --slot=94 --suite=$2 --speed=6" -Timeout 600 -MaxLines 10)
  LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "$1: $(echo "$out" | grep -E '^log:|BUILD')"
  grep -a -E "dps-bases .*(: aps|fixed gear|bare)" "$LOG" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/$1.txt; echo "$1 DONE"; }
run low_before_13 "dps:all:1,3:bases:30:starter:-:bare"
run low_before_5  "dps:all:5:bases:30:starter:-:bare"
for c in hunter emberblade forgebody; do for lv in 30 50; do run trim_${c}_$lv "dps:$c:$lv,$lv:bases:30"; done; done
echo TRIM DONE
