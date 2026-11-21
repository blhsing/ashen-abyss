#!/usr/bin/env bash
cd /y/tools/pilot
first=" "
for cls in "$@"; do
  echo "$(date '+%T') campaign $cls"
  NOBUILD="$first" bash run_one.sh campaign "$cls" "${SECS:-18000}" "${SPEED:-4}" > "last_camp_$cls.txt" 2>&1
  first=-NoBuild
  tail -14 results/campaign.txt | cut -c1-500
done
echo "campaigns done"
