#!/usr/bin/env bash
# after batch.sh: melee references on the same boss/seed, chase regression suite, DPS sensitivity at 0.55 speed
set -u
until grep -q 'BATCH DONE' /y/tools/mobile/boss_results.txt 2>/dev/null; do sleep 30; done
source <(sed -n '/^backup()/p;/^run()/,/^}/p' /y/tools/mobile/batch.sh)
OUT=/y/tools/mobile
for cls in emberblade lampknight forgebody wildsoul; do
  run "mob_${cls}_frost_ref" 600 "--mute --new=$cls --slot=92 --area=frost --level=22 --gear --seed=4242 --pilot-test=boss:600 --speed=3 --bossrush --mobile-cast=1"
  { echo "=== $(date '+%F %T') $cls frost lv22 mobile-cast=1(melee-ref) log=$LAST_LOG"; grep -a "TEST \(RESULT\|FIGHT\)" "$LAST_LOG" | sed 's/^ *[0-9.]* INF pilot: //' | sort -u; } >> $OUT/boss_results.txt
done
run mobdps55 1800 "--mute --slot=92 --suite=mobile:dps:hunter,astromancer,soulbinder,pactbinder:50:40 --speed=4 --mobile-speed=0.55"
{ echo "=== $(date '+%F %T') dps speed 0.55 log=$LAST_LOG"; grep -a "mobile-dps .* speed" "$LAST_LOG" | sed 's/^ *[0-9.]* INF //'; } >> $OUT/dps.txt
bash /y/tools/mobile/run_chase.sh
echo "FOLLOWUP DONE $(date '+%F %T')" >> $OUT/boss_results.txt
