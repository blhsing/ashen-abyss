#!/bin/bash
out=Y:/tools/gear/render/leftarm.txt; rm -f $out
for t in 1 2 3 4 5 6; do
  r=$(powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -GameArgs "--win --mute --suite=gear:leftarm --slot=94 --shot=Y:\shots\gear\leftarm\la.png --shot-at=9999 --quit-at=9999" -Timeout 400 -Tag render 2>&1)
  echo "$r" | grep -q "giving up" && continue
  echo "$r" | grep "ERR=\|exit=\|BUILD FAILED\|error" > $out
  L=$(ls -t /c/Users/blhsi/AppData/Roaming/AshenAbyss/logs/game_*.log | head -5 | xargs grep -l "suite=gear:leftarm" | head -1)
  echo $L >> $out; grep -h "leftarm\|FAIL\|DONE\| ERR " "$L" | cut -c1-600 >> $out
  break
done
echo LA >> $out
