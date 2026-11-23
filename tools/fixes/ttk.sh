#!/bin/bash
# fresh level-1 heroes (starter kit only), TestBot in the wastes, 120 s of game at x4: time per normal kill
tag=${1:-ttk_before}
for c in emberblade astromancer hunter soulbinder wildsoul forgebody lampknight pactbinder; do
  out=$(bash Y:/tools/fixes/runq.sh ${tag}_$c -Priority -Headless -GameArgs "--mute --new=$c --slot=94 --area=wastes --level=1 --bot --seed=4711 --speed=4 --quit-at=120" -Timeout 300 -MaxLines 10)
  LOG=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  echo "$c: $(echo "$out" | grep -E '^log:' | sed 's/.*lines=/lines=/')"
  grep -a -E "balance: ttk|final summary" "$LOG" | tail -2 | sed 's/^ *[0-9.]* INF \[bot\] //' | cut -c1-230
done
echo "$tag DONE"
