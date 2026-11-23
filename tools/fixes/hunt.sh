#!/bin/bash
until grep -a -q "SHORT ALL DONE" Y:/tools/fixes/short.log; do sleep 30; done
run() { out=$(bash Y:/tools/fixes/runq.sh $1 -Priority -Headless -GameArgs "--mute --slot=94 --suite=$2 --speed=6" -Timeout 600 -MaxLines 10)
  LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "$1: $(echo "$out" | grep -E '^log:|BUILD')"
  grep -a -E "dps-bases .*(: aps|fixed gear)" "$LOG" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/$1.txt; echo "$1 DONE"; }
run sw_hunter      "dps:hunter:50,50,50:bases:30"
run sw_hunter_low  "dps:hunter:50,50,50:bases:30:all:-:critlow"
run sw_lk30_low    "dps:lampknight:30,30,30:bases:30:all:-:critlow"
run sw_lk50_low    "dps:lampknight:50,50,50:bases:30:all:-:critlow"
echo HUNT DONE
