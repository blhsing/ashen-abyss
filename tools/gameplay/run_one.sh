#!/bin/bash
# One harness run: run_one.sh <tag> "<game args>" [timeout]; output + gameplay lines in Y:/tools/gameplay/test_out/<tag>.txt
# Waits until the whole project compiles, retries when the run lock times out or the build fails.
out=Y:/tools/gameplay/test_out; mkdir -p $out
for try in 1 2 3 4 5 6 7 8; do
  until powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/check.ps1 2>&1 | grep -q "BUILD OK"; do sleep 45; done
  while tasklist | grep -qi AshenAbyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -GameArgs "$2" -Timeout ${3:-500} -Tag gameplay -Headless > $out/$1.txt 2>&1
  if grep -q -e "giving up" -e "BUILD FAILED" $out/$1.txt; then sleep 30; continue; fi
  break
done
log=$(grep -ao "log: [^ ]*" $out/$1.txt | cut -c6-)
[ -n "$log" ] && { echo "--- gameplay lines from $log"; grep -a "gptest\| ERR \| WRN \|levelstate\|\[save\] resume\|no resume\|resume state\|hero resumed\|given up\|area\] travel\|area\] entered\|\[save\] slot" "$log"; } >> $out/$1.txt
echo DONE >> $out/$1.txt
