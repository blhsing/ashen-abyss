#!/bin/bash
# queues behind the shared run lock (run.ps1 gives up after 25 min) until the swap test has really run
out=Y:/tools/gear/render/swap5.txt; rm -f $out
for t in 1 2 3 4 5 6; do
  r=$(powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -GameArgs "--win --mute --suite=gear:swap --slot=94 --shot-at=9999 --quit-at=9999" -Timeout 400 -Tag render 2>&1)
  echo "$r" | grep -q "giving up" && continue
  echo "$r" | grep "ERR=\|exit=\|BUILD FAILED" > $out
  L=$(ls -t /c/Users/blhsi/AppData/Roaming/AshenAbyss/logs/game_*.log | head -5 | xargs grep -l "suite=gear:swap " | head -1)
  echo $L >> $out; grep -h "set swap\|FAIL\|DONE\| ERR " "$L" | cut -c1-500 >> $out
  break
done
echo SW5 >> $out
