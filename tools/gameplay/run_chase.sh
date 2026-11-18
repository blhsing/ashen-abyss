#!/bin/bash
# Click-to-attack run-up for all 8 classes, then the cancel cases (headless). Results: grep "gptest" in each log.
# Usage: bash Y:/tools/gameplay/run_chase.sh  (writes Y:/tools/gameplay/test_out/chase_<class>.txt)
out=Y:/tools/gameplay/test_out
nb=""
for c in emberblade astromancer hunter soulbinder wildsoul forgebody lampknight pactbinder; do
  while tasklist | grep -qi AshenAbyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -GameArgs "--mute --new=$c --slot=92 --area=wastes --level=8 --gear --god --gptest=chase" -Timeout 150 -Tag gameplay -Headless $nb > $out/chase_$c.txt 2>&1
  log=$(grep -o "log: [^ ]*" $out/chase_$c.txt | cut -c6-); [ -n "$log" ] && grep -a "gptest\|ERR \|levelstate\|run-up" "$log" >> $out/chase_$c.txt
  nb="-NoBuild"
done
for c in emberblade hunter; do
  while tasklist | grep -qi AshenAbyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -GameArgs "--mute --new=$c --slot=92 --area=wastes_d2 --level=8 --gear --god --gptest=cancel" -Timeout 150 -Tag gameplay -Headless -NoBuild > $out/cancel_$c.txt 2>&1
  log=$(grep -o "log: [^ ]*" $out/cancel_$c.txt | cut -c6-); [ -n "$log" ] && grep -a "gptest\|ERR \|run-up" "$log" >> $out/cancel_$c.txt
done
echo ALLDONE > $out/chase_done.txt
