#!/bin/bash
# named-encounter engineer, 23:15: last short recheck (priority lane) of the five adjusted after the final suites
export ASHEN_RUN_PRIORITY=1
cd Y:/tools/named
until [ -f batch15_done.txt ]; do sleep 20; done
F="rare_frost_thousandyear,rare_sands_ogan,sh_abyss,db_wastes_d2,db_abyss_d1"
./q2.sh suite9_fix 300 "--mute --new=emberblade --slot=91 --suite=named:$F --speed=6"
./q2.sh suite9_fix_bestiary 300 "--mute --new=emberblade --slot=91 --suite=named:$F --speed=6 --bestiary"
echo ALLDONE > batch16_done.txt
