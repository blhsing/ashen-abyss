#!/usr/bin/env bash
# waits for the emberblade campaign in progress (run_one pid $1), then runs the other classes on a fresh build
cd /y/tools/pilot
while kill -0 "$1" 2>/dev/null; do sleep 20; done
shift
first=" "
for cls in "$@"; do
  echo "$(date '+%T') campaign $cls"
  NOBUILD="$first" bash run_one.sh campaign "$cls" "${SECS:-18000}" "${SPEED:-4}" > "last_camp_$cls.txt" 2>&1
  first=-NoBuild
  tail -14 results/campaign.txt | cut -c1-500
done
echo "campaigns done"
