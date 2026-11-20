#!/usr/bin/env bash
# Queue one test run of the game for the roster work: waits while the user plays (polls every 30 s), retries when the
# machine-wide run lock was held too long, moves the harness's test saves aside afterwards.
#   bash Y:/tools/roster/runq.sh <tag> <timeout s> "<game args>" [extra run.ps1 switches, e.g. -Headless -NoBuild]
tag="$1"; timeout="$2"; args="$3"; shift 3
for attempt in 1 2 3 4; do
  while tasklist | grep -qi "ashenabyss.exe"; do sleep 30; done
  out=$(powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -GameArgs "$args" -Timeout "$timeout" -Tag "$tag" "$@" 2>&1)
  echo "$out"
  mkdir -p "$APPDATA/AshenAbyss/saves_test_backup"
  if [ -n "$KEEP90" ]; then mv "$APPDATA"/AshenAbyss/saves/slot0.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null; else mv "$APPDATA"/AshenAbyss/saves/slot0.* "$APPDATA"/AshenAbyss/saves/slot90.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null; fi
  if echo "$out" | grep -q "held the lock for 25 minutes"; then echo "lock busy, retrying ($attempt)"; continue; fi
  break
done
echo "runq $tag finished $(date +%H:%M:%S)"
