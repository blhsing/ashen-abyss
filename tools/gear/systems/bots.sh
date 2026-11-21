#!/bin/bash
# SYSTEMS: bot run per class x hand style (slot 92). Results -> Y:/tools/gear/systems/bots_result.txt,
# each run's game stdout -> Y:/tools/gear/systems/runs/<class>_<style>.txt (read the run's own stdout: the log run.ps1
# picks can be another agent's game). Retries while the user plays or another game holds the instance lock.
# usage: bash bots.sh [class:style ...]   (default: every class with each of its styles)
out=/y/tools/gear/systems/bots_result.txt
mkdir -p /y/tools/gear/systems/runs
: > $out
runs="$*"
[ -z "$runs" ] && runs="emberblade:2h emberblade:dual astromancer:2h astromancer:off hunter:2h hunter:dual soulbinder:2h soulbinder:off wildsoul:2h wildsoul:off forgebody:2h forgebody:dual lampknight:2h lampknight:off pactbinder:2h pactbinder:off"
built=""
for r in $runs; do
  c=${r%%:*}; s=${r##*:}
  for try in $(seq 1 40); do
    while tasklist | grep -qi AshenAbyss; do echo "user playing, waiting" >> $out; sleep 30; done
    nb=""; [ -n "$built" ] && nb="-NoBuild"
    res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' $nb -Headless -GameArgs "--mute --new=$c --slot=92 --area=wastes --level=14 --gear --hands=$s --bot --quit-at=75" -Timeout 150 -Tag systems_bot 2>&1)
    echo "$res" | grep -q "build ok" && built=1
    if grep -q "exits without touching" /y/tools/systems_bot_out.txt; then sleep 30; continue; fi
    break
  done
  f=/y/tools/gear/systems/runs/${c}_${s}.txt
  cp /y/tools/systems_bot_out.txt $f 2>/dev/null
  nerr=$(grep -c " ERR " $f)
  echo "=== $c $s : ERR=$nerr $(echo "$res" | grep -E 'BUILD FAILED|TIMEOUT' )" >> $out
  grep -E "\-\-gear: rare|final summary" $f | cut -c1-230 >> $out
  grep -E " ERR " $f | head -6 | cut -c1-300 >> $out
done
mkdir -p "$APPDATA/AshenAbyss/saves_test_backup"
for f in "$APPDATA/AshenAbyss/saves/"slot92.*; do [ -e "$f" ] && mv "$f" "$APPDATA/AshenAbyss/saves_test_backup/systems_$(date +%H%M%S)_$(basename "$f")"; done
echo DONE >> $out
