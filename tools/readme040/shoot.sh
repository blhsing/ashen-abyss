#!/bin/bash
# usage: shoot.sh <tag> <timeout> "<game args>"   (priority lane; waits while the user plays)
tag=$1; to=$2; shift 2
log=/y/tools/readme040/runs.txt
for try in $(seq 1 40); do
  while tasklist | grep -qi ashenabyss; do echo "$(date +%T) user playing, waiting" >> $log; sleep 30; done
  res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -Priority ${NB--NoBuild} -GameArgs "$*" -Timeout $to -Tag "readme040\r_$tag" 2>&1)
  if grep -q "exits without touching\|another instance" /y/tools/readme040/r_${tag}_out.txt 2>/dev/null; then sleep 30; continue; fi
  break
done
echo "=== $(date +%T) $tag: $(echo "$res" | grep -E 'exit=|ERR=|BUILD FAILED|TIMEOUT' | tr '\n' ' ')" >> $log
echo "$res" | head -40
