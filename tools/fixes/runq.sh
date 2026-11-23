#!/bin/bash
# runq TAG <run.ps1 args...> : waits while the user plays, retries while the shared run lock is busy
TAG=$1; shift
while true; do
  while tasklist | grep -qi AshenAbyss.exe; do sleep 30; done
  out=$(powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -Tag $TAG "$@" 2>&1)
  if echo "$out" | grep -q "giving up"; then echo "[$TAG] lock busy, retrying"; continue; fi
  if echo "$out" | grep -a -q "exits without touching it"; then echo "[$TAG] the user's game was running, retrying"; sleep 60; continue; fi
  echo "$out"; break
done
for f in "$APPDATA"/AshenAbyss/saves/slot94*; do [ -e "$f" ] && mv -f "$f" "$APPDATA/AshenAbyss/saves_test_backup/"; done
