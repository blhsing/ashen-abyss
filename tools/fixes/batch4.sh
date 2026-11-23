#!/bin/bash
Q="bash Y:/tools/fixes/runq.sh"
$Q fixes_ui2 -GameArgs "--win --mute --new=lampknight --slot=94 --area=town --level=30 --god --uidir=Y:\shots\fixes --uido=@Y:\tools\fixes\scripts\ui_fix2.txt --quit-at=90" -Timeout 160
$Q fixes_ui2b -NoBuild -GameArgs "--win --mute --new=pactbinder --slot=94 --area=town --level=30 --god --uidir=Y:\shots\fixes\pactbinder --uido=@Y:\tools\fixes\scripts\ui_fix2.txt --quit-at=90" -Timeout 160
echo BATCH4 DONE
