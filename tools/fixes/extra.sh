#!/bin/bash
# lead: dual sword1 checks (low crit for emberblade + hunter lv30/50; one-implicit test flag lv50); normal-crit points come from final.sh
until grep -a -q "FINAL DONE" Y:/tools/fixes/final.log 2>/dev/null; do sleep 30; done
run() { out=$(bash Y:/tools/fixes/runq.sh $1 -Priority -Headless -GameArgs "--mute --slot=94 --suite=$2 --speed=6 $3" -Timeout 600 -MaxLines 10)
  LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "$1: $(echo "$out" | grep -E '^log:|BUILD')"
  grep -a -E "dps-bases .*(: aps|fixed gear)" "$LOG" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/$1.txt; echo "$1 DONE"; }
run ds_eb30_low  "dps:emberblade:30,30:bases:30:all:-:critlow"
run ds_eb50_low  "dps:emberblade:50,50:bases:30:all:-:critlow"
run ds_hu30_low  "dps:hunter:30,30:bases:30:all:-:critlow"
run ds_eb50_one  "dps:emberblade:50,50:bases:30" "--dual-one-implicit"
run ds_hu50_one  "dps:hunter:50,50:bases:30" "--dual-one-implicit"
echo EXTRA DONE
