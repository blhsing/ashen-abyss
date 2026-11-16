#!/bin/bash
# Runs the bot through totem/prisoner dungeons with several layouts; prints per run: spots moved/skipped, totals, door opened.
out=/y/tools/test_idols.txt; : > $out
for area in wastes_d2 crypt_d1 mire_d2; do
  for seed in 11 23 37 51; do
    if tasklist 2>/dev/null | grep -qi ashenabyss; then echo "user game running - stop" >> $out; exit 0; fi
    powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--mute --new=lampknight --slot=95 --area=$area --level=12 --gear --bot --focus --seed=$seed --speed=3 --quit-at=150" -Timeout 240 -Headless -NoBuild -Tag idols > /tmp/idol_run.txt 2>&1
    L=$(ls -t "$APPDATA"/AshenAbyss/logs/game_*.log | head -1)
    moved=$(grep -c 'moved to reachable floor' "$L"); skipped=$(grep -c 'cannot be reached from the entrance' "$L")
    opened=$(grep -c 'boss door opened' "$L"); sealed=$(grep -c 'boss door sealed' "$L")
    errs=$(grep -c ' ERR ' "$L"); obj=$(grep -o 'objective [0-9]*/[0-9]*\|摧毀寂滅圖騰（[0-9]/[0-9]）\|解救被囚的倖存者（[0-9]/[0-9]）' "$L" | tail -1)
    echo "$area seed=$seed sealed=$sealed moved=$moved skipped=$skipped door_opened=$opened errors=$errs last=$obj" >> $out
  done
done
echo done >> $out
