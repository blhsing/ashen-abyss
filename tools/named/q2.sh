#!/bin/bash
# named-encounter engineer: one queued test run. usage: q2.sh <tag> <timeout_s> "<game args>" [headless(1/0)]
# waits while the user's game runs, then run.ps1; retries while another agent's run holds the run lock and while
# someone else's half-done edit breaks the build; copies the game log to Y:/tools/named/runs/<tag>.log
tag=$1; to=$2; args=$3; hl=${4:-1}
H=""; [ "$hl" = "1" ] && H="-Headless"
for try in $(seq 1 80); do
  while tasklist | grep -qi ashenabyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$args" -Timeout $to -Tag named_$tag $H > Y:/tools/named/runs/$tag.txt 2>&1
  if grep -q "BUILD FAILED" Y:/tools/named/runs/$tag.txt; then echo "retry $try (build broken)" >> Y:/tools/named/runs/${tag}_retries.txt; sleep 90; continue; fi
  if grep -q "this test launch exits without touching it" Y:/tools/named/runs/$tag.txt; then echo "retry $try (another game instance running)" >> Y:/tools/named/runs/${tag}_retries.txt; sleep 60; continue; fi
  grep -q "giving up" Y:/tools/named/runs/$tag.txt || break
  echo "retry $try (run lock busy)" >> Y:/tools/named/runs/${tag}_retries.txt
done
log=$(grep -o "log: [^ ]*" Y:/tools/named/runs/$tag.txt | head -1 | cut -c6-)
[ -n "$log" ] && cp "$log" Y:/tools/named/runs/$tag.log
mkdir -p "$APPDATA/AshenAbyss/saves_test_backup"
for f in "$APPDATA"/AshenAbyss/saves/slot91* "$APPDATA"/AshenAbyss/saves/slot0*; do [ -e "$f" ] && mv -f "$f" "$APPDATA/AshenAbyss/saves_test_backup/"; done
echo DONE >> Y:/tools/named/runs/$tag.txt
