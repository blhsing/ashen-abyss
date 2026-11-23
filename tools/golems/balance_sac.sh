#!/bin/bash
# balance.sh <tag>: the four reference boss fights for the Soulbinder (pilot harness, priority lane); results appended to logs/balance_<tag>.txt
export ASHEN_RUN_PRIORITY=1
tag=$1; shift
out=/y/tools/golems/logs/balance_$tag.txt
for spec in "frost 22 --sacrifice=all" "abyss_boss 42 --sacrifice=all"; do
  set -- $spec
  NOBUILD=${NOBUILD:--NoBuild} bash /y/tools/pilot/run_one.sh boss soulbinder $1 $2 600 2 "${3:-}" > /tmp/g/ro.txt 2>&1
  log=$(grep -o "log: [^ ]*" /tmp/g/ro.txt | head -1 | cut -c6-)
  echo "=== $tag $1 lv$2 ${3:-} log=$log" >> $out
  [ -n "$log" ] && grep -aE "TEST FIGHT|TEST RESULT|\[hero\] died|\[balance\]" "$(cygpath -u "$log")" | cut -c1-420 | head -30 >> $out
done
echo done >> $out
