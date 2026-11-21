#!/usr/bin/env bash
# All seven story bosses for the given classes: run_bosses.sh emberblade hunter astromancer ...
# Levels: what the pilot's level gate (AiPilot.WantedLevel) asks before each lair.
cd /y/tools/pilot
BOSSES="wastes_boss:5 frost_boss:10 crypt_boss:11 mire_boss:13 sands_boss:28 sanctum_boss:35 abyss_boss:42"
[ -n "${ONLY:-}" ] && BOSSES="$ONLY"
NB=${FIRSTNB:- }
for cls in "$@"; do
  for b in $BOSSES; do
    area=${b%%:*}; lvl=${b##*:}
    echo "$(date '+%T') $cls $area lv$lvl"
    NOBUILD=${NB:--NoBuild} bash run_one.sh boss "$cls" "$area" "$lvl" "${SECS:-900}" "${SPEED:-2}" > last_run.txt 2>&1; NB=-NoBuild
    tail -1 results/boss.txt
  done
done
echo "batch done"
