#!/usr/bin/env bash
# after followup.sh: with the -10 % moving-shot damage: mobile suite, DPS lv50, ranged frost boss fights (on)
set -u
until grep -q 'FOLLOWUP DONE' /y/tools/mobile/boss_results.txt 2>/dev/null; do sleep 20; done
source <(sed -n '/^backup()/p;/^run()/,/^}/p' /y/tools/mobile/batch.sh)
OUT=/y/tools/mobile
source <(sed -n '/^run()/,/^}/p' /y/tools/mobile/batch.sh | sed 's/-NoBuild/$NB/; s/^run()/runb()/'); NB= ; runb mobbuild 120 "--mute --slot=92 --suite=mobile:dps:hunter:1:10 --speed=4"; NB=-NoBuild
run mobsuite 600 "--mute --slot=92 --suite=mobile:noshots --speed=2"
{ echo "=== $(date '+%F %T') suite log=$LAST_LOG"; grep -a "suite\] \(FAIL\|DONE\)" "$LAST_LOG"; } >> $OUT/suite.txt
run mobdpsT 1800 "--mute --slot=92 --suite=mobile:dps:hunter,astromancer,soulbinder,pactbinder:50:40 --speed=4"
{ echo "=== $(date '+%F %T') dps speed 0.65 dmg 0.90 log=$LAST_LOG"; grep -a "mobile-dps .* speed" "$LAST_LOG" | sed 's/^ *[0-9.]* INF //'; } >> $OUT/dps.txt
for cls in hunter astromancer soulbinder pactbinder; do
  run "mob_${cls}_frost_trim" 600 "--mute --new=$cls --slot=92 --area=frost --level=22 --gear --seed=4242 --pilot-test=boss:600 --speed=3 --bossrush --mobile-cast=1"
  { echo "=== $(date '+%F %T') $cls frost lv22 mobile-cast=1(trim0.9) log=$LAST_LOG"; grep -a "TEST \(RESULT\|FIGHT\)" "$LAST_LOG" | sed 's/^ *[0-9.]* INF pilot: //' | sort -u; } >> $OUT/boss_results.txt
done
echo "BATCH2 DONE $(date '+%F %T')" >> $OUT/boss_results.txt
