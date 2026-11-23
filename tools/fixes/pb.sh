#!/bin/bash
out=$(bash Y:/tools/fixes/runq.sh low_after_pb -Priority -Headless -GameArgs "--mute --slot=94 --suite=dps:pactbinder:1,3,5:bases:30:starter:-:bare --speed=6" -Timeout 600 -MaxLines 10)
LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "low_after_pb: $(echo "$out" | grep -E '^log:|BUILD')"
grep -a -E "dps-bases .*: aps" "$LOG" | sed 's/^ *[0-9.]* INF \[suite\] //' > Y:/tools/fixes/low_after_pb.txt
out=$(bash Y:/tools/fixes/runq.sh ttk_after_pactbinder -Priority -NoBuild -Headless -GameArgs "--mute --new=pactbinder --slot=94 --area=wastes --level=1 --bot --seed=4711 --speed=4 --quit-at=120" -Timeout 300 -MaxLines 10)
LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-); echo "ttk_after: $(echo "$out" | grep -E '^log:')"
grep -a -E "balance: ttk|final summary" "$LOG" | tail -2 | sed 's/^ *[0-9.]* INF \[bot\] //' | cut -c1-230
echo PB DONE
