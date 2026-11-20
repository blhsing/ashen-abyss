#!/bin/bash
# named-encounter engineer, resume 2026-11-20: screenshots + suite run 4 (retries while the run lock is busy)
R='Y:\tools\run.ps1'
run() { local tag=$1 to=$2 args=$3 head=$4 out=Y:/tools/named/$tag.txt
  for i in 1 2 3 4 5 6; do
    while tasklist | grep -qi ashenabyss; do sleep 30; done
    powershell -NoProfile -ExecutionPolicy Bypass -File $R -GameArgs "$args" -Timeout $to -Tag $tag $head > $out 2>&1
    grep -q "giving up" $out || return 0
  done; }
[ -f Y:/shots/named/ladder.png ] || run named_shot2 300 "--win --mute --new=emberblade --slot=91 --level=50 --tier=2 --named=pin_1_gravedigger@route --ui=named_ladder --shot=Y:\shots\named\ladder.png --shot-at=4 --quit-at=7" ""
run named_shot1 400 "--win --mute --new=emberblade --slot=91 --level=13 --gear --god --bot --named=sh_frost --shot=Y:\shots\named\sh_frost.png --shot-at=24,34 --quit-at=38" ""
run named_run4 2400 "--mute --suite=named:rare_frost_hamo,rare_frost_whiteshadow,rare_frost_lastrun,rare_crypt_namelessking,rare_mire_dafu,rare_sanctum_firstcantor,rare_sanctum_vesna,db_wastes_d1,db_wastes_d2,pin_7 --speed=6" "-Headless"
echo DONE > Y:/tools/named/resume_done.txt
