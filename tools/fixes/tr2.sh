#!/bin/bash
until grep -a -q "EXTRA DONE" Y:/tools/fixes/extra.log 2>/dev/null; do sleep 30; done
bash Y:/tools/fixes/runq.sh fixes_tr1c -Priority -GameArgs "--win --mute --new=forgebody --hands=dual --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\traits_forgebody --uido=@Y:\tools\fixes\scripts\traits.txt --quit-at=40" -Timeout 120 | grep "^log:\|BUILD"
echo TR2 DONE
