#!/usr/bin/env bash
# For campaign runs built before AiPilot.WallReached: stop the run after 6 deaths in one region (what the new rule does).
# Kills only the Godot started by the run.ps1 whose game args contain pilot-test=campaign.
while true; do
  f=$(ls -t "$APPDATA"/AshenAbyss/logs/game_*.log | head -1)
  if grep -aq "pilot-test=campaign" "$f" && ! grep -aq "TEST RESULT" "$f"; then
    for r in frost sands sanctum abyss crypt mire wastes; do
      n=$(grep -a "TEST death" "$f" | grep -c " in ${r}[ _]")
      if [ "$n" -ge 6 ]; then
        echo "$(date '+%T') $f: $n deaths in $r -> stopping (wall rule)" >> /y/tools/pilot/wall_watch.txt
        w=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\who_runs.ps1' 2>&1)
        rp=$(echo "$w" | grep "run.ps1" | grep -- "--slot=93 --pilot-te" | grep -o "pid *[0-9]*" | awk '{print $2}' | head -1)
        if [ -n "$rp" ]; then
          for p in $(echo "$w" | grep -i godot | grep -E "parent +$rp " | grep -o "pid *[0-9]*" | awk '{print $2}'); do taskkill //PID $p //T //F > /dev/null 2>&1; done
        fi
        sleep 90; break
      fi
    done
  fi
  grep -q "chain11 done" /y/tools/pilot/iso11.txt 2>/dev/null && exit 0
  sleep 30
done
