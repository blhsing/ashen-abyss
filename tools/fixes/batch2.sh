#!/bin/bash
R="powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1"
wait_free() { while tasklist | grep -qi AshenAbyss.exe; do sleep 30; done; }
wait_free; $R -NoBuild -GameArgs "--win --mute --new=emberblade --hands=dual --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\before900 --uido=@Y:\tools\fixes\scripts\inv2.txt --quit-at=80" -Timeout 140 -Tag fixes_inv2
wait_free; $R -NoBuild -EngineArgs "--resolution 1920x1080 --position 0,0" -GameArgs "--win --mute --new=pactbinder --hands=off --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\before1080 --uido=@Y:\tools\fixes\scripts\inv2.txt --quit-at=80" -Timeout 140 -Tag fixes_inv3
wait_free; $R -GameArgs "--win --mute --new=hunter --slot=94 --area=town --level=5 --hook-selftest --quit-at=12" -Timeout 100 -Tag fixes_hook
echo BATCH2 DONE
for c in lampknight pactbinder soulbinder; do
  wait_free; $R -NoBuild -GameArgs "--win --mute --menu=class:$c --uidir=Y:\shots\fixes\class_$c --uido=@Y:\tools\fixes\scripts\cls.txt --quit-at=40" -Timeout 90 -Tag fixes_cls
done
echo BATCH2B DONE
