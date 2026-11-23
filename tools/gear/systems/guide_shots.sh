#!/bin/bash
# SYSTEMS: 武器指南 screenshots at 1600x900 and 1920x1080 (slot 96), priority lane (short runs). Out: guide_shots.txt
out=/y/tools/gear/systems/guide_shots.txt
: > $out
run() {  # $1 = tag, $2 = engine args, rest = game args
  local tag=$1 eng=$2; shift 2
  for try in $(seq 1 30); do
    while tasklist | grep -qi AshenAbyss; do echo "user playing, waiting" >> $out; sleep 30; done
    res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -Priority -EngineArgs "$eng" -GameArgs "$*" -Timeout 150 -Tag $tag 2>&1)
    if grep -q "exits without touching" /y/tools/${tag}_out.txt 2>/dev/null; then sleep 30; continue; fi
    break
  done
  echo "=== $tag $eng : $(echo "$res" | grep -E 'ERR=|BUILD FAILED')" >> $out
  grep -E " ERR |WRN \[uitest" /y/tools/${tag}_out.txt | head -5 >> $out
}
for res in 1600x900 1920x1080; do
  mkdir -p "/y/shots/guide/$res"
  run sys_guide_play "--resolution $res" --win --mute --new=lampknight --slot=96 --level=20 --gear --hands=off "--uidir=Y:\\shots\\guide\\$res" "--uido=@Y:/tools/gear/systems/ui_guide.txt"
  run sys_guide_menu "--resolution $res" --win --mute "--uidir=Y:\\shots\\guide\\$res" "--uido=@Y:/tools/gear/systems/ui_guide_menu.txt"
done
mkdir -p "$APPDATA/AshenAbyss/saves_test_backup"
for f in "$APPDATA/AshenAbyss/saves/"slot96.*; do [ -e "$f" ] && mv "$f" "$APPDATA/AshenAbyss/saves_test_backup/systems_guide_$(basename "$f")"; done
echo DONE >> $out
