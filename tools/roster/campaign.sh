#!/usr/bin/env bash
# Natural campaign with the bestiary: a level-1 hunter (no god mode) played by the bot at ×6, in chunks of 20 real minutes
# chained through test slot 90, until the bot reports that no main quest is left (or 14 chunks).
#   bash Y:/tools/roster/campaign.sh <extra switches, e.g. --bestiary or --no-bestiary> <tag>
mode="$1"; tag="${2:-roster_camp}"
logs="$APPDATA/AshenAbyss/logs"
for i in $(seq 1 14); do
  if [ "$i" = "1" ]; then start="--new=hunter --seed=777"; else start="--load=90"; fi
  out=$(bash Y:/tools/roster/runq.sh "${tag}_$i" 1500 "--mute $mode --slot=90 $start --bot --campaign --speed=6 --quit-at=1200" -Headless $([ "$i" = "1" ] || echo -NoBuild))
  echo "$out" | grep -E "exit=|log:|finished"
  # runq moved slot90 aside: bring the save back for the next chunk
  cp "$APPDATA"/AshenAbyss/saves_test_backup/slot90.* "$APPDATA/AshenAbyss/saves/" 2>/dev/null
  f=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  [ -n "$f" ] || { echo "no log for chunk $i"; break; }
  grep -E "balance: ttk|campaign: no main quest|boss .* killed|level up|final summary" "$f" | cut -c1-260 | tail -8
  if grep -q "campaign: no main quest is active" "$f"; then echo "CAMPAIGN OVER in chunk $i"; break; fi
done
mv "$APPDATA"/AshenAbyss/saves/slot90.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
echo "campaign $tag finished $(date +%H:%M:%S)"
