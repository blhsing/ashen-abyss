#!/bin/bash
# 0.4.0 re-release: short checks on what changed since the full smoke (class select, book, filter, pulse rings, toasts).
out=/y/tools/release/smoke_040b.txt; : > $out
run() { # tag, args, timeout
  while tasklist 2>/dev/null | grep -qi ashenabyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$2" -Timeout $3 -Tag smoke $4 > /tmp/smoke_run.txt 2>&1
  L=$(ls -t "$APPDATA"/AshenAbyss/logs/game_*.log | head -1)
  echo "== $1: $(grep -a -E "ERR=" /tmp/smoke_run.txt | sed 's/.*lines=/lines=/')" >> $out
  grep -E 'BUILD FAILED|error CS' /tmp/smoke_run.txt | head -3 >> $out
  grep -aE 'DONE suite=|  failure [0-9]+:' "$L" | sed 's/^ *[0-9.]* //' | head -8 >> $out
  grep -E ' ERR | WRN ' "$L" | sed 's/^ *[0-9.]* //' | sort | uniq -c | sort -rn | head -6 >> $out
  grep -E 'crossfade|\[cursor\]|\[named\].*(spawn|killed|announce)|rare|stronghold|final summary|anim-check' "$L" | sed 's/^ *[0-9.]* //' | cut -c1-170 | sort | uniq -c | sort -rn | head -8 >> $out
  for f in "$APPDATA"/AshenAbyss/saves/slot9* "$APPDATA"/AshenAbyss/saves/slot0*; do [ -e "$f" ] && mv -f "$f" "$APPDATA/AshenAbyss/saves_test_backup/"; done
}
mkdir -p /y/shots/release040
run town     "--win --mute --new=lampknight --slot=99 --area=town --level=10 --gear --ui-tour --shot=Y:\shots\release040\b_tour.png --quit-at=120" 260 ""
run demo     "--win --mute --demo --demo-secs=20 --shot=Y:\shots\release040\b_demo.png --shot-at=15,45,75 --quit-at=100" 240 "-NoBuild"
run sanctum  "--win --mute --new=lampknight --slot=99 --area=sanctum --level=34 --gear --bot --shot=Y:\shots\release040\b_sanctum.png --shot-at=30,60 --quit-at=100" 240 "-NoBuild"
run pilot    "--win --mute --new=hunter --slot=99 --area=wastes --level=3 --pilot-at=5 --quit-at=120" 260 "-NoBuild"
run s_items  "--win --mute --slot=99 --suite=items --quit-at=900" 960 "-NoBuild"
run s_gear   "--win --mute --slot=99 --suite=gear --quit-at=900" 960 "-NoBuild"
run s_golem  "--win --mute --slot=99 --suite=golem --quit-at=1200" 1260 "-NoBuild"
run s_guide  "--win --mute --slot=99 --suite=guide --quit-at=600" 660 "-NoBuild"
echo done >> $out
