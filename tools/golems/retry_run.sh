#!/bin/bash
# retry_run.sh <tag> <timeout> <headless 0/1> <game args...>: runs run.ps1 until it gets the lock (max 8 attempts)
tag=$1; to=$2; hl=$3; shift 3
for i in 1 2 3 4 5 6 7 8; do
  while tasklist | grep -qi "^AshenAbyss"; do sleep 30; done
  if [ "$hl" = "1" ]; then out=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$*" -Timeout $to -Headless -Tag "golems\$tag" 2>&1)
  elif [ "$hl" = "2" ]; then out=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$*" -Timeout $to -NoBuild -Tag "golems\$tag" 2>&1)
  else out=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$*" -Timeout $to -Tag "golems\$tag" 2>&1); fi
  if echo "$out" | grep -q "giving up"; then echo "attempt $i: lock busy, retrying $(date +%T)"; continue; fi
  echo "$out" > "/y/tools/golems/logs/${tag}_run.txt"
  echo "$out" | tail -70
  exit 0
done
echo "gave up after 8 attempts"
