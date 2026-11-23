#!/usr/bin/env bash
# NPC / boss / gear texture compression A/B (priority lane for the short shots; seed fixes the gear roll).
L=Y:/tools/roster/logs; S=$L/portrait.txt
shot() { # tag "args"
  local tag=$1 args=$2
  for try in $(seq 1 40); do
    while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do sleep 30; done
    res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$args" -Timeout 240 -Tag roster_tex -NoBuild -Priority 2>&1)
    echo "$res" | grep -q "held the lock" && continue
    f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
    [ -n "$f" ] && grep -aq "this test launch exits" "$f" && { sleep 60; continue; }
    break
  done
  f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-); [ -n "$f" ] && cp "$f" "$L/$tag.log"
  echo "$(date +%T) $tag $(echo "$res" | grep -o 'ERR=[0-9]*' | head -1) $(grep -a '\[perf\] fps' "$L/$tag.log" 2>/dev/null | tail -1 | grep -o 'vram=[0-9]*MB\|draws=[0-9]*' | tr '\n' ' ')" >> $S
  mv "$APPDATA"/AshenAbyss/saves/slot90.* "$APPDATA"/AshenAbyss/saves/slot0.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
}
arm() { shot tex2_$1_armoured "--win --mute --new=forgebody --slot=90 --seed=4242 --area=town --level=30 --gear --ui-tour=inventory --shot=Y:/shots/roster/tex/tex2_$1_armoured.png --quit-at=40"; }
for step in "$@"; do
  case $step in
    before) arm before ;;
    compress) cd Y:/game/assets/chars && bash Y:/tools/roster/tex_compress.sh npc npc_*.jpg.import npc_*.png.import >> $S
              bash Y:/tools/roster/tex_compress.sh boss boss_*.jpg.import boss_*.png.import >> $S
              cd Y:/game/assets/gear && bash Y:/tools/roster/tex_compress.sh gear $(find . -name '*.jpg.import' -o -name '*.png.import' | sed 's|^\./||') >> $S
              for t in 1 2 3; do r=$(powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/import.ps1 2>&1); echo "$(date +%T) import: $(echo "$r" | head -3 | tr '\n' ' ')" >> $S; echo "$r" | grep -q "import exit=\|import retry exit=0" && break; done ;;
    after) arm after
           shot tex_after_title "--win --mute --shot=Y:/shots/roster/tex/tex_after_title.png --shot-at=6,12 --quit-at=14" ;;
  esac
done
echo "$(date +%T) set2_tex done: $*" >> $S
