#!/usr/bin/env bash
cd /y/tools/pilot
until grep -q "frost check done" frost_check2.txt 2>/dev/null; do sleep 30; done
first=" "
for cls in hunter forgebody lampknight; do
  echo "$(date "+%T") campaign $cls" >> camp9.txt
  NOBUILD="$first" bash run_one.sh campaign "$cls" 18000 4 > last_camp.txt 2>&1; first=-NoBuild
  tail -3 results/campaign.txt | cut -c1-400 >> camp9.txt
done
echo "campaigns done" >> camp9.txt
# isolated re-checks: sh_sands (overworld stronghold) and 奧薩洛斯 for the classes not re-run in full
for cls in emberblade astromancer wildsoul; do
  echo "$(date '+%T') $cls sands lv27" >> iso9.txt
  NOBUILD=-NoBuild bash run_one.sh boss "$cls" sands 27 900 2 > last_iso.txt 2>&1
  grep -a "TEST RESULT" results/boss.txt | tail -1 | cut -c1-260 >> iso9.txt
  echo "$(date '+%T') $cls abyss_boss lv42" >> iso9.txt
  NOBUILD=-NoBuild bash run_one.sh boss "$cls" abyss_boss 42 900 2 > last_iso.txt 2>&1
  grep -a "TEST RESULT" results/boss.txt | tail -1 | cut -c1-260 >> iso9.txt
done
echo "chain9 done" >> iso9.txt
