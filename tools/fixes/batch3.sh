#!/bin/bash
Q="bash Y:/tools/fixes/runq.sh"
$Q fixes_ui -GameArgs "--win --mute --new=lampknight --slot=94 --area=town --level=30 --god --uidir=Y:\shots\fixes --uido=@Y:\tools\fixes\scripts\ui_all.txt --quit-at=200" -Timeout 300
$Q fixes_ui1080 -NoBuild -EngineArgs "--resolution 1920x1080 --position 0,0" -GameArgs "--win --mute --new=pactbinder --hands=off --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\p1080 --uido=@Y:\tools\fixes\scripts\inv2.txt --quit-at=80" -Timeout 140
$Q fixes_cls -NoBuild -GameArgs "--win --mute --menu=class:lampknight --uidir=Y:\shots\fixes\class --uido=@Y:\tools\fixes\scripts\cls_all.txt --quit-at=60" -Timeout 120
$Q fixes_hook -NoBuild -GameArgs "--win --mute --new=hunter --slot=94 --area=town --level=5 --hook-selftest --quit-at=12" -Timeout 100
echo BATCH3 DONE
