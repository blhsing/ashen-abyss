#!/bin/bash
# named-encounter engineer: campaign smoke test + designed-route checks (run after the suite; each waits for the run lock)
wait_user() { while tasklist | grep -qi ashenabyss; do sleep 30; done; }
R='Y:\tools\run.ps1'
wait_user; powershell -NoProfile -ExecutionPolicy Bypass -File $R -GameArgs "--mute --new=emberblade --slot=92 --level=30 --gear --god --bot --campaign --speed=6" -Timeout 1200 -Tag named_campaign -Headless > Y:/tools/named/smoke_campaign.txt 2>&1
wait_user; powershell -NoProfile -ExecutionPolicy Bypass -File $R -GameArgs "--mute --new=emberblade --slot=93 --level=12 --gear --god --bot --area=wastes --tour=wastes_d1,wastes_d2,frost,frost_d1,frost_d2 --speed=6" -Timeout 1200 -Tag named_tour -Headless > Y:/tools/named/smoke_tour.txt 2>&1
wait_user; powershell -NoProfile -ExecutionPolicy Bypass -File $R -GameArgs "--mute --new=emberblade --slot=94 --level=16 --gear --god --named=wb_barrowbearer@route --quit-at=20" -Timeout 200 -Tag named_wbroute -Headless > Y:/tools/named/smoke_wbroute.txt 2>&1
wait_user; powershell -NoProfile -ExecutionPolicy Bypass -File $R -GameArgs "--win --mute --new=emberblade --slot=94 --level=13 --gear --god --bot --named=sh_frost --shot=Y:\shots\named\sh_frost.png --shot-at=22,32 --quit-at=36" -Timeout 300 -Tag named_shot1 > Y:/tools/named/smoke_shot1.txt 2>&1
wait_user; powershell -NoProfile -ExecutionPolicy Bypass -File $R -GameArgs "--win --mute --new=emberblade --slot=94 --level=50 --tier=2 --named=pin_1_gravedigger@route --ui=named_ladder --shot=Y:\shots\named\ladder.png --shot-at=4 --quit-at=7" -Timeout 200 -Tag named_shot2 > Y:/tools/named/smoke_shot2.txt 2>&1
echo ALLDONE > Y:/tools/named/smoke_done.txt
