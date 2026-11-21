#!/usr/bin/env bash
cd /y/tools/pilot
while pgrep -f "run_one.sh boss hunter frost" > /dev/null 2>&1 || ps -ef | grep -v grep | grep -q "run_one.sh boss hunter frost"; do sleep 20; done
nb=" "
for cls in hunter hunter emberblade astromancer; do
  echo "$(date '+%T') $cls frost lv19"
  NOBUILD="$nb" bash run_one.sh boss "$cls" frost 19 900 2 > last_frost.txt 2>&1; nb=-NoBuild
  grep -a "TEST RESULT" results/boss.txt | tail -1 | cut -c1-260
done
echo "frost check done"
