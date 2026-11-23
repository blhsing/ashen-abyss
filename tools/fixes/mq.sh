#!/bin/bash
# item 4: mapquest suite x3 on slot 94
for i in 1 2 3; do
  NB=""; [ $i -gt 1 ] && NB="-NoBuild"
  out=$(bash Y:/tools/fixes/runq.sh fixes_mq $NB -GameArgs "--win --mute --suite=mapquest --slot=94 --quit-at=600" -Timeout 700)
  L=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  echo "run $i: $(echo "$out" | grep -E '^log:')"
  [ -n "$L" ] && grep -E "DONE suite|FAIL|objective target|pinned once" "$L" | cut -c1-220
done
echo MQ DONE
