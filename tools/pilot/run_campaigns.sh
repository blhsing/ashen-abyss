#!/usr/bin/env bash
# Full campaign, new character → 寂主・奧薩洛斯, for each class given: run_campaigns.sh emberblade hunter astromancer
cd /y/tools/pilot
for cls in "$@"; do
  echo "$(date '+%T') campaign $cls"
  bash run_one.sh campaign "$cls" "${SECS:-18000}" "${SPEED:-4}" > last_camp.txt 2>&1
  tail -3 results/campaign.txt | cut -c1-400
done
echo "campaigns done"
