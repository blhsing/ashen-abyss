#!/usr/bin/env bash
# waits for the run_one in progress (pid $1), then builds and runs the given classes' campaigns
cd /y/tools/pilot
while ps -p "$1" > /dev/null 2>&1; do sleep 20; done
shift
first=" "
for cls in "$@"; do
  echo "$(date '+%T') campaign $cls"
  NOBUILD="$first" bash run_one.sh campaign "$cls" "${SECS:-18000}" "${SPEED:-4}" > "last_camp_$cls.txt" 2>&1
  first=-NoBuild
  tail -14 results/campaign.txt | cut -c1-500
done
echo "campaigns done"
