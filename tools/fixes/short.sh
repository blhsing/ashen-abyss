#!/bin/bash
# FIXES balance + UI checks as short (<=5 min) priority runs (lead: priority lane for runs of 5 minutes or less)
OFF="--fistmastery=0 --shieldoath=0 --lanternoath=0"
run() { # tag suite speed extra
  out=$(bash Y:/tools/fixes/runq.sh $1 -Priority -Headless -GameArgs "--mute --slot=94 --suite=$2 --speed=$3 $4" -Timeout 600 -MaxLines 10)
  LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "$1: $(echo "$out" | grep -E '^log:|BUILD|exit=')"
  grep -a -E "dps-bases .*(: aps|fixed gear)" "$LOG" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/$1.txt; echo "$1 DONE"; }
for L in 10 30 50; do
  [ $L = 50 ] && run pin_before_$L "dps:forgebody,lampknight:$L,$L,$L:bases:30" 6 "$OFF"
  run pin_after_$L  "dps:forgebody,lampknight:$L,$L,$L:bases:30" 6 ""
done
Q="bash Y:/tools/fixes/runq.sh"
$Q fixes_tr1 -Priority -NoBuild -GameArgs "--win --mute --new=forgebody --hands=dual --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\traits_forgebody --uido=@Y:\tools\fixes\scripts\traits.txt --quit-at=40" -Timeout 90 | grep "^log:"
$Q fixes_tr2 -Priority -NoBuild -GameArgs "--win --mute --new=lampknight --hands=off --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\traits_lampknight --uido=@Y:\tools\fixes\scripts\traits.txt --quit-at=40" -Timeout 90 | grep "^log:"
$Q fixes_set2 -Priority -NoBuild -GameArgs "--win --mute --menu=settings --uidir=Y:\shots\fixes\settings --uido=@Y:\tools\fixes\scripts\settings2.txt --quit-at=40" -Timeout 90 | grep "^log:"
echo UI SHOTS DONE
run fair_crit "dps:lampknight:50,50,50:bases:120:off:-:critlow" 8 "$OFF"
for c in lampknight wildsoul emberblade; do
  run fair_op_$c   "dps:$c:50,50,50:bases:120:2h" 6 "$OFF"
  run fair_noop_$c "dps:$c:50,50,50:bases:120:2h" 6 "$OFF --no-overpower"
done
echo SHORT ALL DONE
