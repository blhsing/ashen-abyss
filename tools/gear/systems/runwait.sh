#!/bin/bash
# SYSTEMS: run.ps1 that waits while the user plays or another test holds the game's instance lock, then retries.
# usage: bash runwait.sh <tag> <timeout> [-Headless] -- <game args...>
tag=$1; to=$2; shift 2
extra=""; if [ "$1" = "-Headless" ]; then extra="-Headless"; shift; fi
[ "$1" = "--" ] && shift
args="$*"
for try in $(seq 1 60); do
  while tasklist | grep -qi AshenAbyss; do sleep 30; done
  res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' $extra -GameArgs "$args" -Timeout $to -Tag $tag 2>&1)
  if echo "$res" | grep -q "giving up"; then echo "try $try: the run lock is held by a long run; waiting"; sleep 60; continue; fi
  if grep -q "exits without touching" /y/tools/${tag}_out.txt 2>/dev/null; then echo "try $try: instance lock held by another game; waiting 30 s"; sleep 30; continue; fi
  echo "$res" | grep -E "BUILD|ERR=|TIMEOUT|exit="
  exit 0
done
echo "gave up after 60 tries"
