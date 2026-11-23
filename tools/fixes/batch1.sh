#!/bin/bash
# FIXES batch 1 (slot 94). Waits while the user plays.
R="powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1"
wait_free() { while tasklist | grep -qi AshenAbyss.exe; do sleep 30; done; }
wait_free; $R -GameArgs "--win --mute --new=lampknight --slot=94 --area=town --level=5 --prtsc-at=8 --quit-at=14" -Timeout 120 -Tag fixes_prtsc
wait_free; $R -NoBuild -GameArgs "--win --mute --new=lampknight --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes\before --uido=@Y:\tools\fixes\scripts\inv.txt --quit-at=80" -Timeout 140 -Tag fixes_inv
wait_free; $R -NoBuild -GameArgs "--win --mute --new=lampknight --slot=94 --area=town --level=30 --gear --uidir=Y:\shots\fixes --uido=@Y:\tools\fixes\scripts\gamble.txt --quit-at=80" -Timeout 140 -Tag fixes_gamble
wait_free; $R -NoBuild -GameArgs "--win --mute --new=lampknight --slot=94 --area=wastes --level=10 --gear --god --uidir=Y:\shots\fixes --uido=@Y:\tools\fixes\scripts\labels.txt --quit-at=60" -Timeout 140 -Tag fixes_labels
echo BATCH1 DONE
