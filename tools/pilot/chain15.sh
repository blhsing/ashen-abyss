#!/usr/bin/env bash
# final round (lead GO 2026-11-23): fresh build, then campaigns in the lead's order; hunter + emberblade later (sword decision)
cd /y/tools/pilot
first=" "
for cls in "$@"; do
  echo "$(date '+%F %T') campaign $cls start" >> camp15.txt
  NOBUILD=$first bash run_one.sh campaign "$cls" 18000 4 > last_camp.txt 2>&1
  first=-NoBuild
  tail -1 results/campaign.txt >/dev/null
  awk -v c="$cls" '/^=== /{blk=""} {blk=blk $0 "\n"} END{printf "%s", blk}' results/campaign.txt | cut -c1-400 >> camp15.txt
  echo "$(date '+%F %T') campaign $cls end" >> camp15.txt
done
echo "chain15 done" >> camp15.txt
