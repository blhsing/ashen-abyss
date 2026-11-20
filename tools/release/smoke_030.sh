#!/bin/bash
# 0.2.0 release smoke tests: build once, then several windowed bot runs; summary per run.
out=/y/tools/release/smoke_030.txt; : > $out
run() { # tag, args, timeout
  while tasklist 2>/dev/null | grep -qi ashenabyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$2" -Timeout $3 -Tag smoke $4 > /tmp/smoke_run.txt 2>&1
  L=$(ls -t "$APPDATA"/AshenAbyss/logs/game_*.log | head -1)
  echo "== $1: $(grep -a -E "ERR=" /tmp/smoke_run.txt | sed 's/.*lines=/lines=/')" >> $out
  grep -E 'BUILD FAILED|error CS' /tmp/smoke_run.txt | head -3 >> $out
  grep -E ' ERR | WRN ' "$L" | sed 's/^ *[0-9.]* //' | sort | uniq -c | sort -rn | head -6 >> $out
  grep -E 'crossfade|\[cursor\]|\[named\].*(spawn|killed|announce)|rare|stronghold|final summary|anim-check' "$L" | sed 's/^ *[0-9.]* //' | cut -c1-170 | sort | uniq -c | sort -rn | head -8 >> $out
  for f in "$APPDATA"/AshenAbyss/saves/slot9* "$APPDATA"/AshenAbyss/saves/slot0*; do [ -e "$f" ] && mv -f "$f" "$APPDATA/AshenAbyss/saves_test_backup/"; done
}
mkdir -p /y/shots/release030
run wastes   "--win --new=emberblade --slot=99 --area=wastes --level=6 --gear --bot --shot=Y:\shots\release030\wastes.png --shot-at=20,60 --quit-at=90" 240 ""
run dungeon  "--win --mute --new=hunter --slot=99 --area=wastes_d1 --level=8 --gear --bot --focus --quit-at=120" 260 "-NoBuild"
run town     "--win --mute --new=lampknight --slot=99 --area=town --level=10 --gear --ui-tour --shot=Y:\shots\release030\tour.png --quit-at=120" 260 "-NoBuild"
run frost    "--win --mute --new=astromancer --slot=99 --area=frost --level=14 --gear --bot --shot=Y:\shots\release030\frost.png --shot-at=30,60 --quit-at=90" 240 "-NoBuild"
run stronghold "--win --mute --new=forgebody --slot=99 --level=20 --gear --god --named=sh_wastes --bot --quit-at=120" 260 "-NoBuild"
run osaros   "--win --mute --new=pactbinder --slot=99 --area=abyss_boss --level=45 --gear --god --bot --focus --bossrush --shot=Y:\shots\release030\osaros.png --shot-at=30 --quit-at=80" 240 "-NoBuild"
run demo     "--win --mute --demo --demo-secs=20 --shot=Y:\shots\release030\demo.png --shot-at=15,45,75 --quit-at=100" 240 "-NoBuild"
run pilot    "--win --mute --new=hunter --slot=99 --area=wastes --level=3 --pilot-at=5 --quit-at=120" 260 "-NoBuild"
echo done >> $out
