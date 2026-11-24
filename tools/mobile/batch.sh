#!/usr/bin/env bash
# MOBILE batch: kite DPS table, then AI-pilot boss fights with the setting off (before) and on (after). Slot 92.
# Waits while the user plays; retries while the run lock is busy. Results: Y:/tools/mobile/dps.txt, boss_results.txt
set -u
OUT=/y/tools/mobile
LOGDIR="$APPDATA/AshenAbyss/logs"
backup() { mkdir -p "$APPDATA/AshenAbyss/saves_test_backup/mobile"; for f in "$APPDATA"/AshenAbyss/saves/slot92.* "$APPDATA"/AshenAbyss/saves/slot0.*; do [ -e "$f" ] && mv -f "$f" "$APPDATA/AshenAbyss/saves_test_backup/mobile/"; done; }
run() { # tag timeout args
  local tag=$1 tmo=$2 args=$3 res l
  for try in $(seq 1 30); do
    while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do echo "user is playing; waiting 30 s"; sleep 30; done
    res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$args" -Headless -Timeout $tmo -Tag "$tag" -NoBuild -MaxLines 5 2>&1)
    if echo "$res" | grep -q "giving up\|another test run held the lock"; then echo "lock busy, retry"; continue; fi
    l=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
    if [ -n "$l" ] && grep -aq "this test launch exits" "$l"; then echo "user's game runs; retry in 60 s"; sleep 60; continue; fi
    break
  done
  backup
  LAST_LOG=$(echo "$res" | grep -o 'log: [^ ]*' | head -1 | cut -c6-)
  echo "$tag -> $LAST_LOG $(echo "$res" | grep -o 'ERR=[0-9]*' | head -1)"
}
if [ "${SKIP_DPS:-0}" != 1 ]; then
  run mobiledps 2400 "--mute --slot=92 --suite=mobile:dps:all:30,50:40 --speed=4"
  { echo "=== $(date '+%F %T') dps log=$LAST_LOG"; grep -a "mobile-dps\|mobile:dps .* skills" "$LAST_LOG" | sed 's/^ *[0-9.]* INF //'; } >> $OUT/dps.txt
fi
for spec in "hunter frost 22" "astromancer frost 22" "soulbinder frost 22" "pactbinder frost 22" "hunter abyss_boss 42" "astromancer abyss_boss 42" "soulbinder abyss_boss 42" "pactbinder abyss_boss 42"; do
  set -- $spec; cls=$1; area=$2; lvl=$3
  for m in 0 1; do
    run "mob_${cls}_${area}_m$m" 600 "--mute --new=$cls --slot=92 --area=$area --level=$lvl --gear --seed=4242 --pilot-test=boss:600 --speed=3 --bossrush --mobile-cast=$m"
    { echo "=== $(date '+%F %T') $cls $area lv$lvl mobile-cast=$m log=$LAST_LOG"; grep -a "TEST \(RESULT\|FIGHT\|fight ends\|death\)\|kite + shoot\|KiteShots" "$LAST_LOG" | sed 's/^ *[0-9.]* INF pilot: //' | sort -u; } >> $OUT/boss_results.txt
  done
done
echo "BATCH DONE $(date '+%F %T')" >> $OUT/boss_results.txt
