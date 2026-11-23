#!/usr/bin/env bash
# final round (lead GO 2026-11-23): fresh build, then campaigns in the lead's order; hunter + emberblade later (sword decision)
cd /y/tools/pilot
until grep -q "DUAL TRIM IN" /y/STATUS.md; do sleep 60; done
echo "$(date "+%F %T") DUAL TRIM IN seen" >> camp16.txt
first=" "
for cls in "$@"; do
  echo "$(date '+%F %T') campaign $cls start" >> camp16.txt
  NOBUILD=$first bash run_one.sh campaign "$cls" 18000 4 > last_camp.txt 2>&1
  first=-NoBuild
  tail -1 results/campaign.txt >/dev/null
  awk -v c="$cls" '/^=== /{blk=""} {blk=blk $0 "\n"} END{printf "%s", blk}' results/campaign.txt | cut -c1-400 >> camp16.txt
  echo "$(date '+%F %T') campaign $cls end" >> camp16.txt
done
echo "chain16 done" >> camp16.txt
