#!/bin/bash
for try in $(seq 1 30); do
  while tasklist | grep -qi AshenAbyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -Priority -NoBuild -EngineArgs "--resolution 1600x900" -GameArgs "--win --mute --new=lampknight --slot=96 --level=20 --gear --hands=off --uidir=Y:\shots\guide\1600x900 --uido=@Y:/tools/gear/systems/ui_guide.txt" -Timeout 120 -Tag sys_guide_play > /dev/null 2>&1
  grep -q "exits without" /y/tools/sys_guide_play_out.txt && { sleep 30; continue; }
  break
done
for f in "$APPDATA/AshenAbyss/saves/"slot96.*; do [ -e "$f" ] && mv "$f" "$APPDATA/AshenAbyss/saves_test_backup/systems_guide_$(basename "$f")"; done
grep -c " ERR " /y/tools/sys_guide_play_out.txt > /y/tools/gear/systems/reshoot_done.txt
