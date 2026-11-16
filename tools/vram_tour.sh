#!/bin/bash
# Two windowed tours over five regions: without and with --gfx-trim. Prints video memory per area from the [perf] lines.
out=/y/tools/vram_tour.txt; : > $out
for mode in off on; do
  if tasklist 2>/dev/null | grep -qi ashenabyss; then echo "user game running - stop" >> $out; exit 0; fi
  extra=""; [ $mode = on ] && extra="--gfx-trim"
  powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--win --mute --new=hunter --slot=95 --level=20 --gear --god --gfx-tour=wastes,frost,crypt,mire,sands,sanctum $extra --quit-at=260" -Timeout 420 -NoBuild -Tag vram > /tmp/vram_$mode.txt 2>&1
  L=$(ls -t "$APPDATA"/AshenAbyss/logs/game_*.log | head -1)
  echo "== trim $mode  errors=$(grep -c ' ERR ' "$L") warnings=$(grep -c ' WRN ' "$L")" >> $out
  grep -o 'area=[a-z_]* .*vram=[0-9]*MB' "$L" | sed -E 's/^area=([a-z_]*) .*vram=([0-9]*)MB/\1 \2/' | awk '{if($2>max[$1])max[$1]=$2; if(!($1 in seen)){seen[$1]=1; order[++n]=$1}} END{for(i=1;i<=n;i++) printf "%s=%sMB  ", order[i], max[order[i]]; print ""}' >> $out
  grep -E 'trim|released|cache' "$L" | head -6 | cut -c1-160 >> $out
done
mv -f "$APPDATA/AshenAbyss/saves/slot95."* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
echo done >> $out
