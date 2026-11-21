#!/usr/bin/env bash
# One AI-pilot test run (headless). Usage:
#   run_one.sh boss <class> <area> <level> [game_secs] [speed] [extra switches]
#   run_one.sh campaign <class> [game_secs] [speed]
# Appends the TEST lines of the run's log to Y:/tools/pilot/results/<mode>.txt. Waits while the user plays.
set -u
mode=$1; cls=$2
LOGDIR="$APPDATA/AshenAbyss/logs"
OUT=/y/tools/pilot/results; mkdir -p "$OUT"
while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do echo "user is playing; waiting 30 s"; sleep 30; done
if [ "$mode" = boss ]; then
  area=$3; lvl=$4; secs=${5:-600}; speed=${6:-2}; extra=${7:-}
  args="--mute --new=$cls --slot=93 --area=$area --level=$lvl --gear --pilot-test=boss:$secs --speed=$speed --bossrush $extra"
  tag="pilot_${cls}_${area}"
else
  secs=${3:-14400}; speed=${4:-3}
  args="--mute --new=$cls --slot=93 --pilot-test=campaign:$secs --speed=$speed"
  tag="pilot_camp_${cls}"
fi
tmo=$(( secs / speed + 240 ))
nb=${NOBUILD:--NoBuild}
for try in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
  res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$args" -Headless -Timeout $tmo -Tag "$tag" $nb 2>&1)
  echo "$res" | head -5
  if echo "$res" | grep -q "another test run held the lock"; then echo "lock busy, retry $try"; continue; fi
  if echo "$res" | grep -q "BUILD FAILED"; then echo "build broken (someone is mid-edit); retry in 60 s"; sleep 60; continue; fi
  l=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  if [ -n "$l" ] && grep -aq "this test launch exits" "$l"; then echo "another game instance runs; retry in 60 s"; sleep 60; nb=-NoBuild; continue; fi
  break
done
log=$(echo "$res" | grep -o 'log: [^ ]*' | head -1 | cut -c6-)
errs=$(echo "$res" | grep -o 'ERR=[0-9]*' | head -1)
{
  echo "=== $(date '+%F %T') $mode $cls ${area:-campaign} lv${lvl:--} speed=$speed log=$log $errs"
  if [ -n "$log" ]; then grep -a "TEST \(RESULT\|FIGHT\|fight ends\|death\)" "$(cygpath -u "$log" 2>/dev/null || echo "$log")" | sed 's/^ *[0-9.]* INF pilot: //' | sort -u; fi
} >> "$OUT/$mode.txt"
# test saves out of the way (AGENT_RULES)
mkdir -p "$APPDATA/AshenAbyss/saves_test_backup/pilot"
for f in "$APPDATA"/AshenAbyss/saves/slot93.* "$APPDATA"/AshenAbyss/saves/slot0.*; do [ -e "$f" ] && mv -f "$f" "$APPDATA/AshenAbyss/saves_test_backup/pilot/"; done
echo done
