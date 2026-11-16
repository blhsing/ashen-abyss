#!/bin/bash
# waitfor.sh [seconds] : wait until the batch stops or logs a problem, or the time is up; print one status line.
V="$LOCALAPPDATA"/AshenAbyssTools/voice_tw
L=$V/logs/gen.log
n0=$(grep -c "WARNING\|FAILED\|Traceback\|FAIL$\|=== voice" "$L" 2>/dev/null)
end=$(( $(date +%s) + ${1:-165} ))
while [ $(date +%s) -lt $end ]; do
  [ -f /y/tools/voice_tw/RUNNING.txt ] || break
  n=$(grep -c "WARNING\|FAILED\|Traceback\|FAIL$\|=== voice" "$L" 2>/dev/null)
  [ "$n" -gt "$n0" ] && break
  sleep 5
done
d=$(ls $V/stage/prod/dry/*.json 2>/dev/null | wc -l)
g=$(ls /y/game/assets/audio/vo/*.ogg 2>/dev/null | wc -l)
r=$([ -f /y/tools/voice_tw/RUNNING.txt ] && echo RUN || echo STOPPED)
echo "$(date +%H:%M) $r dry $d game $g | $(grep '=== voice' "$L" | tail -1 | cut -c14-60) | $(grep 'WARNING\|FAILED\|Traceback\|FAIL$' "$L" | tail -1 | cut -c10-150)"
