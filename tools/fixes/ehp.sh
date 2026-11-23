#!/bin/bash
tag=$1
out=$(bash Y:/tools/fixes/runq.sh $tag -Priority -Headless -GameArgs "--mute --slot=94 --suite=dps:emberblade,wildsoul,lampknight,forgebody:10,23,40:bases:5:starter --speed=6" -Timeout 600 -MaxLines 10)
LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "$tag: $(echo "$out" | grep -E '^log:|BUILD')"
grep -a "INF \[suite\] ehp " "$LOG" | sed 's/^ *[0-9.]* INF \[suite\] //' | tee Y:/tools/fixes/$tag.txt
echo "$tag DONE"
