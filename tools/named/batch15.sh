#!/bin/bash
# named-encounter engineer, 2026-11-22 22:40: the remaining short runs (each <= 5 min) in the PRIORITY lane (lead's go)
# replaces batch10-14 (stopped by their own PIDs)
export ASHEN_RUN_PRIORITY=1
cd Y:/tools/named
Q=./q2.sh
B="--mute --new=emberblade --slot=91"
for ts in 0 1 2; do
  $Q shot3_ladder_$ts 200 "--win $B --level=50 --tier=2 --named=pin_1_gravedigger@route --ui=named_ladder --textscale=$ts --shot=Y:\shots\named\ladder3_ts$ts.png --shot-at=5 --quit-at=8" 0
done
$Q final_pin2b 420 "$B --level=50 --tier=2 --paragon=50 --gear --area=rift~pin:2 --pilot-test=boss:900 --speed=4"
$Q final_suite_pin48b 300 "$B --suite=named:pin_4,pin_8:emberblade:2:nogod:legendary --speed=6"
F="rare_frost_thousandyear,rare_sands_ogan,sh_abyss,db_wastes_d2,db_abyss_d1"
$Q suite8_fix 300 "$B --suite=named:$F --speed=6"
$Q suite8_fix_bestiary 300 "$B --suite=named:$F --speed=6 --bestiary"
echo ALLDONE > batch15_done.txt
