#!/bin/bash
# pinned before/after for Forgebody 拳刃精通 + Lampknight 盾誓/燈誓 (after fair.sh), then skill-screen shots
until grep -a -q "FAIR ALL DONE" Y:/tools/fixes/fair.log; do sleep 30; done
OFF="--fistmastery=0 --shieldoath=0 --lanternoath=0"
run() { out=$(bash Y:/tools/fixes/runq.sh $1 -Headless -GameArgs "--mute --slot=94 --suite=dps:forgebody,lampknight:10,10,10,30,30,30,50,50,50:bases:30 --speed=6 $2" -Timeout 3000 -MaxLines 10)
  L=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "$1: $(echo "$out" | grep -E '^log:')"
  grep -a -E "dps-bases .*(: aps|fixed gear)" "$L" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/$1.txt; echo "$1 DONE"; }
run pin_before "$OFF"
run pin_after ""
bash Y:/tools/fixes/runq.sh fixes_tr1 -NoBuild -GameArgs "--win --mute --new=forgebody --hands=dual --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\traits_forgebody --uido=@Y:\tools\fixes\scripts\traits.txt --quit-at=40" -Timeout 90 | grep "^log:"
bash Y:/tools/fixes/runq.sh fixes_tr2 -NoBuild -GameArgs "--win --mute --new=lampknight --hands=off --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\traits_lampknight --uido=@Y:\tools\fixes\scripts\traits.txt --quit-at=40" -Timeout 90 | grep "^log:"
echo FAIR2 DONE
