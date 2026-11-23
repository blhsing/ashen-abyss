#!/usr/bin/env bash
# AI-pilot campaign on test slot 90 (roster stream), headless, no god mode.
#   bash pilot90.sh <class> <game secs> <speed> <roster switch: --bestiary|--no-bestiary> [tag]
# Waits while the user plays; retries on the run lock; copies the log to Y:/tools/roster/logs/<tag>.log and appends
# the pilot's TEST lines and the [roster] stats lines to Y:/tools/roster/logs/<tag>.txt.
cls=$1; secs=${2:-18000}; speed=${3:-4}; ros=${4:---bestiary}; tag=${5:-camp_${cls}${ros#--}}
L=Y:/tools/roster/logs
args="--mute --new=$cls --slot=90 --pilot-test=campaign:$secs --speed=$speed $ros"
tmo=$(( secs / speed + 300 ))
for try in $(seq 1 30); do
  while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do sleep 30; done
  res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$args" -Headless -Timeout $tmo -Tag "roster_$tag" 2>&1)
  if echo "$res" | grep -q "held the lock"; then continue; fi
  if echo "$res" | grep -q "BUILD FAILED"; then sleep 60; continue; fi
  l=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  if [ -n "$l" ] && grep -aq "this test launch exits" "$l"; then sleep 60; continue; fi
  break
done
log=$(echo "$res" | grep -o 'log: [^ ]*' | head -1 | cut -c6-)
[ -n "$log" ] && cp "$log" "$L/$tag.log"
{ echo "=== $(date '+%F %T') $cls $ros speed=$speed log=$log $(echo "$res" | grep -o 'ERR=[0-9]*' | head -1)"
  echo "$res" | head -8
  grep -a "TEST \(RESULT\|fight ends\|death\)\|\[roster\] stats\|campaign: no main" "$L/$tag.log" | cut -c1-400; } > "$L/$tag.txt"
mkdir -p "$APPDATA/AshenAbyss/saves_test_backup"
mv "$APPDATA"/AshenAbyss/saves/slot90.* "$APPDATA"/AshenAbyss/saves/slot0.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
echo "pilot90 $tag done $(date +%T)"
