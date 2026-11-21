#!/usr/bin/env bash
cd /y/tools/pilot
until grep -q "frost check done" frost_check2.txt 2>/dev/null; do sleep 30; done
SPEED=4 bash run_campaigns.sh hunter forgebody lampknight > camp8.txt 2>&1
# isolated re-checks: sh_sands (overworld stronghold) and 奧薩洛斯 for the classes not re-run in full
for cls in emberblade astromancer wildsoul; do
  echo "$(date '+%T') $cls sands lv27" >> iso8.txt
  NOBUILD=-NoBuild bash run_one.sh boss "$cls" sands 27 900 2 > last_iso.txt 2>&1
  grep -a "TEST RESULT" results/boss.txt | tail -1 | cut -c1-260 >> iso8.txt
  echo "$(date '+%T') $cls abyss_boss lv42" >> iso8.txt
  NOBUILD=-NoBuild bash run_one.sh boss "$cls" abyss_boss 42 900 2 > last_iso.txt 2>&1
  grep -a "TEST RESULT" results/boss.txt | tail -1 | cut -c1-260 >> iso8.txt
done
echo "chain8 done" >> iso8.txt
