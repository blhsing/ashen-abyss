#!/usr/bin/env bash
# sh_frost (凍骨關, the real stronghold with its crowd) in isolation at a campaign-like level, final code
cd /y/tools/pilot
nb=" "
for cls in hunter hunter emberblade astromancer; do
  echo "$(date '+%T') $cls frost lv19"
  NOBUILD="$nb" bash run_one.sh boss "$cls" frost 19 900 2 > last_frost.txt 2>&1; nb=-NoBuild
  grep -a "TEST RESULT" results/boss.txt | tail -1 | cut -c1-260
done
echo "frost check done"
