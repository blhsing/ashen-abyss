#!/usr/bin/env bash
# release re-runs (lead 2026-11-23): astromancer, pactbinder, lampknight now; forgebody after FIXES' own
# "- <date> <time> FORGE STURDY IN" status line (a quote of the phrase elsewhere does not count)
cd /y/tools/pilot
run() {
  echo "$(date '+%F %T') campaign $1 start" >> camp17.txt
  NOBUILD=$2 bash run_one.sh campaign "$1" 18000 4 > last_camp.txt 2>&1
  awk '/^=== /{blk=""} {blk=blk $0 "\n"} END{printf "%s", blk}' results/campaign.txt | cut -c1-400 >> camp17.txt
  echo "$(date '+%F %T') campaign $1 end" >> camp17.txt
}
run astromancer " "
run pactbinder -NoBuild
run lampknight -NoBuild
until grep -qE '^- 20[0-9-]+ [0-9:]+ FORGE STURDY IN' /y/STATUS.md; do sleep 60; done
echo "$(date '+%F %T') FORGE STURDY IN seen: $(grep -E '^- 20[0-9-]+ [0-9:]+ FORGE STURDY IN' /y/STATUS.md | head -1 | cut -c1-120)" >> camp17.txt
run forgebody " "
echo "chain17 done" >> camp17.txt
