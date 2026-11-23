#!/bin/bash
until grep -a -q "FAIR2 DONE" Y:/tools/fixes/fair2.log; do sleep 30; done
bash Y:/tools/fixes/runq.sh fixes_set2 -NoBuild -GameArgs "--win --mute --menu=settings --uidir=Y:\shots\fixes\settings --uido=@Y:\tools\fixes\scripts\settings2.txt --quit-at=40" -Timeout 90 | grep "^log:"
echo SET2 DONE
