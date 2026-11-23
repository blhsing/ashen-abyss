#!/bin/bash
# final table (lead): forgebody, lampknight, hunter, emberblade, wildsoul at levels 30 and 50, 2 samples, bonuses ON
until grep -a -q "TR DONE" Y:/tools/fixes/tr.log 2>/dev/null; do sleep 30; done
run() { out=$(bash Y:/tools/fixes/runq.sh $1 -Priority -Headless -GameArgs "--mute --slot=94 --suite=$2 --speed=6" -Timeout 600 -MaxLines 10)
  LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "$1: $(echo "$out" | grep -E '^log:|BUILD')"
  grep -a -E "dps-bases .*(: aps|fixed gear)" "$LOG" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/$1.txt; echo "$1 DONE"; }
for c in forgebody lampknight hunter emberblade wildsoul; do
  for lv in 30 50; do run final_${c}_$lv "dps:$c:$lv,$lv:bases:30"; done
done
cat Y:/tools/fixes/final_*.txt > Y:/tools/fixes/final_all.txt
echo FINAL DONE
