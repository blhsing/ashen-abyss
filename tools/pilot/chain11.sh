#!/usr/bin/env bash
# sh_frost A/B for the proposed chill fix (hunter lv22, real stronghold), then forgebody + lampknight campaigns, then isolated checks
cd /y/tools/pilot
nb=" "
for run in "hunter -" "hunter --chill-grace" "hunter -" "hunter --chill-grace" "forgebody -" "forgebody --chill-grace"; do
  set -- $run; cls=$1; extra=$2; [ "$extra" = "-" ] && extra=""
  echo "$(date '+%T') $cls frost lv22 $extra" >> ab11.txt
  NOBUILD="$nb" bash run_one.sh boss "$cls" frost 22 900 2 "$extra" > last_ab.txt 2>&1; nb=-NoBuild
  grep -a "TEST RESULT" results/boss.txt | tail -1 | cut -c1-240 >> ab11.txt
done
echo "ab done" >> ab11.txt
for cls in forgebody lampknight; do
  echo "$(date '+%T') campaign $cls" >> camp11.txt
  NOBUILD=-NoBuild bash run_one.sh campaign "$cls" 18000 4 > last_camp.txt 2>&1
  tail -3 results/campaign.txt | cut -c1-400 >> camp11.txt
done
echo "campaigns done" >> camp11.txt
for cls in emberblade astromancer wildsoul; do
  for b in "sands 27" "abyss_boss 42"; do
    set -- $b
    echo "$(date '+%T') $cls $1 lv$2" >> iso11.txt
    NOBUILD=-NoBuild bash run_one.sh boss "$cls" "$1" "$2" 900 2 > last_iso.txt 2>&1
    grep -a "TEST RESULT" results/boss.txt | tail -1 | cut -c1-240 >> iso11.txt
  done
done
echo "chain11 done" >> iso11.txt
