#!/usr/bin/env bash
# waits for the running boss batch, then runs the campaigns (fresh build first)
cd /y/tools/pilot
until grep -q "batch done" batch2b.txt 2>/dev/null; do sleep 20; done
first=" "
for cls in "$@"; do
  echo "$(date '+%T') campaign $cls"
  NOBUILD="$first" bash run_one.sh campaign "$cls" "${SECS:-18000}" "${SPEED:-4}" > "last_camp_$cls.txt" 2>&1
  first=-NoBuild
  tail -12 results/campaign.txt | cut -c1-500
done
echo "campaigns done"
