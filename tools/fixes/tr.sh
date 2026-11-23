#!/bin/bash
until grep -a -q "HUNT DONE" Y:/tools/fixes/hunt.log; do sleep 30; done
Q="bash Y:/tools/fixes/runq.sh"
$Q fixes_tr1b -Priority -GameArgs "--win --mute --new=forgebody --hands=dual --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\traits_forgebody --uido=@Y:\tools\fixes\scripts\traits.txt --quit-at=40" -Timeout 120 | grep "^log:\|BUILD"
$Q fixes_tr2b -Priority -NoBuild -GameArgs "--win --mute --new=lampknight --hands=off --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\traits_lampknight --uido=@Y:\tools\fixes\scripts\traits.txt --quit-at=40" -Timeout 90 | grep "^log:"
echo TR DONE
