#!/bin/bash
# Fair DPS baselines (lead 2026-11-22): fixed non-weapon gear per class+level, mid-range implicits, bonuses OFF.
OFF="--fistmastery=0 --shieldoath=0 --lanternoath=0"
run() { # tag suite extra
  out=$(bash Y:/tools/fixes/runq.sh $1 -Headless -GameArgs "--mute --slot=94 --suite=$2 --speed=6 $OFF $3" -Timeout 4000 -MaxLines 10)
  L=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  echo "$1: $(echo "$out" | grep -E '^log:|BUILD')"
  grep -a -E "dps-bases .*(: aps|fixed gear)" "$L" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/$1.txt
  echo "$1 DONE $(wc -l < Y:/tools/fixes/$1.txt) lines"
}
run fair_check "dps:forgebody:50:bases:30"
run fair_all   "dps:forgebody,lampknight:10,10,10,30,30,30,50,50,50:bases:30"
run fair_lk1h  "dps:lampknight:30,30,30,50,50,50:bases:120:off"
run fair_lk1hc "dps:lampknight:30,30,30,50,50,50:bases:120:off:cci"
run fair_crit  "dps:lampknight:50,50,50:bases:120:off:-:critlow"
run fair_op    "dps:lampknight,wildsoul,emberblade:50,50,50:bases:120:2h"
run fair_noop  "dps:lampknight,wildsoul,emberblade:50,50,50:bases:120:2h" "--no-overpower"
echo FAIR ALL DONE
