#!/usr/bin/env bash
# Title-screen picture (boss close-up) + VRAM for the texture A/B.   bash title.sh <phase>
ph=$1; L=Y:/tools/roster/logs; S=$L/portrait.txt; tag=tex_${ph}_title; SHOT="Y:/shots/roster/tex/${tag}.png"
for try in $(seq 1 40); do
  while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do sleep 30; done
  res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--win --mute --shot=$SHOT --shot-at=6,12 --quit-at=14" -Timeout 180 -Tag roster_tex -NoBuild 2>&1)
  echo "$res" | grep -q "held the lock" && continue
  f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  [ -n "$f" ] && grep -aq "this test launch exits" "$f" && { sleep 60; continue; }
  break
done
f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-); [ -n "$f" ] && cp "$f" "$L/$tag.log"
echo "$(date +%T) $tag $(echo "$res" | grep -o 'ERR=[0-9]*' | head -1) $(grep -a 'vram=' "$L/$tag.log" 2>/dev/null | tail -1 | grep -o 'vram=[0-9]*MB')" >> $S
