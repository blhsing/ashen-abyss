#!/usr/bin/env bash
# freeze rule (chill resets when a freeze ends, no chill during CC immunity): sh_frost lv22 isolation, then campaigns
cd /y/tools/pilot
nb=" "
for cls in hunter hunter lampknight forgebody; do
  echo "$(date '+%T') $cls frost lv22" >> iso12.txt
  NOBUILD="$nb" bash run_one.sh boss "$cls" frost 22 900 2 > last_iso.txt 2>&1; nb=-NoBuild
  grep -a "TEST RESULT" results/boss.txt | tail -1 | cut -c1-240 >> iso12.txt
done
echo "iso done" >> iso12.txt
for cls in hunter lampknight forgebody; do
  echo "$(date '+%T') campaign $cls" >> camp12.txt
  NOBUILD=-NoBuild bash run_one.sh campaign "$cls" 18000 4 > last_camp.txt 2>&1
  tail -3 results/campaign.txt | cut -c1-400 >> camp12.txt
done
echo "campaigns done" >> camp12.txt
