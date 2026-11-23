#!/usr/bin/env bash
# Re-take the title-screen "before" (boss textures lossless again for a moment), then compress them back.
L=Y:/tools/roster/logs; S=$L/portrait.txt
imp() { for t in 1 2 3; do r=$(powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/import.ps1 2>&1); echo "$(date +%T) import($1): $(echo "$r" | head -2 | tr '\n' ' ')" >> $S; echo "$r" | grep -q "import exit=\|import retry exit=0" && break; done; }
cp Y:/tools/roster/tex_backup/boss/*.import Y:/game/assets/chars/
imp lossless-boss
for try in $(seq 1 40); do
  while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do sleep 30; done
  res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--win --mute --shot=Y:/shots/roster/tex/tex_before_title_true.png --shot-at=6,12 --quit-at=14" -Timeout 180 -Tag roster_tex -NoBuild -Priority 2>&1)
  echo "$res" | grep -q "held the lock" && continue
  break
done
echo "$(date +%T) tex_before_title_true $(echo "$res" | grep -o 'ERR=[0-9]*' | head -1)" >> $S
cd Y:/game/assets/chars && bash Y:/tools/roster/tex_compress.sh boss boss_*.jpg.import boss_*.png.import >> $S
imp boss-compressed
echo "$(date +%T) title_redo done" >> $S
