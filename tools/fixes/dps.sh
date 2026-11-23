#!/bin/bash
TAG=${1:-fixes_dps}
out=$(bash Y:/tools/fixes/runq.sh $TAG -Headless -GameArgs "--mute --slot=94 --suite=dps:all:25,50:bases:40 --speed=4" -Timeout 2400 -MaxLines 10)
echo "$out" | head -20
L=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
grep -E "dps-bases .*: aps|FAIL" "$L" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/$TAG.txt
echo "DPS DONE $L"
