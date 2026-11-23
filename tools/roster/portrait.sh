#!/usr/bin/env bash
# Hero inventory-portrait close-ups (bare = level 1 starter, armoured = level 30 --gear) + VRAM in town, for the texture
# compression A/B.   bash portrait.sh <phase: before|after> [classes]
ph=$1; shift; cls=${*:-forgebody astromancer}
L=Y:/tools/roster/logs; S=$L/portrait.txt
for c in $cls; do for kind in bare armoured; do
  if [ $kind = bare ]; then lv="--level=1"; else lv="--level=30 --gear"; fi
  tag=tex_${ph}_${c}_$kind; SHOT="Y:/shots/roster/tex/${tag}.png"
  for try in $(seq 1 40); do
    while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do sleep 30; done
    res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--win --mute --new=$c --slot=90 --area=town $lv --ui-tour=inventory --shot=$SHOT --quit-at=40" -Timeout 240 -Tag roster_tex -NoBuild 2>&1)
    echo "$res" | grep -q "held the lock" && continue
    f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
    [ -n "$f" ] && grep -aq "this test launch exits" "$f" && { sleep 60; continue; }
    break
  done
  f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  [ -n "$f" ] && cp "$f" "$L/$tag.log"
  echo "$(date +%T) $tag $(echo "$res" | grep -o 'ERR=[0-9]*' | head -1) $(grep -a '\[perf\] fps' "$L/$tag.log" 2>/dev/null | tail -1 | grep -o 'vram=[0-9]*MB\|draws=[0-9]*' | tr '\n' ' ')" >> $S
  mv "$APPDATA"/AshenAbyss/saves/slot90.* "$APPDATA"/AshenAbyss/saves/slot0.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
done; done
echo "$(date +%T) portrait $ph done" >> $S
