#!/usr/bin/env bash
# after chain13's isolated sh_frost runs: stop chain13 (its campaigns would use the old build), run the pilot suite on a
# fresh build, then the campaigns
cd /y/tools/pilot
until grep -q "iso done" iso13.txt 2>/dev/null; do sleep 10; done
kill $(cat chain13.pid) 2>/dev/null; sleep 2
pkill -f "run_one.sh campaign" 2>/dev/null
for p in $(ps -ef | grep -v grep | grep "run_one.sh campaign" | awk '{print $2}'); do kill $p; done
for try in $(seq 1 20); do
  res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--mute --suite=pilot" -Headless -Timeout 300 -Tag pilot_suite 2>&1)
  echo "$res" | grep -q "BUILD FAILED" && { sleep 60; continue; }; break
done
l=$(echo "$res" | grep -o 'log: [^ ]*' | head -1 | cut -c6-); grep -a "PASS\|FAIL\|DONE" "$l" | cut -c1-200 > suite14.txt
for cls in hunter lampknight forgebody; do
  echo "$(date '+%T') campaign $cls" >> camp14.txt
  NOBUILD=-NoBuild bash run_one.sh campaign "$cls" 18000 4 > last_camp.txt 2>&1
  tail -3 results/campaign.txt | cut -c1-400 >> camp14.txt
done
echo "campaigns done" >> camp14.txt
