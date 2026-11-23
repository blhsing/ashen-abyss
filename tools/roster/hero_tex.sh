#!/usr/bin/env bash
# Hero texture compression A/B with the priority lane (short runs only):
#   before: forgebody armoured portrait + title; then compress hero_* / form_* textures, import; after: bare + armoured.
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
portrait() { # phase kind
  if [ $2 = bare ]; then lv="--level=1"; else lv="--level=30 --gear"; fi
  local tag=tex_$1_forgebody_$2
  shot $tag "--win --mute --new=forgebody --slot=90 --area=town $lv --ui-tour=inventory --shot=Y:/shots/roster/tex/$tag.png --quit-at=40"
}
for step in "$@"; do
  case $step in
    before) portrait before armoured
            shot tex_before_title "--win --mute --shot=Y:/shots/roster/tex/tex_before_title.png --shot-at=6,12 --quit-at=14" ;;
    compress) cd Y:/game/assets/chars && bash Y:/tools/roster/tex_compress.sh heroes hero_*.jpg.import hero_*.png.import form_*.jpg.import form_*.png.import >> $S
              for t in 1 2 3; do r=$(powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/import.ps1 2>&1); echo "$(date +%T) import: $(echo "$r" | head -3 | tr '\n' ' ')" >> $S; echo "$r" | grep -q "import exit=\|import retry exit=0" && break; done ;;
    after) portrait after bare; portrait after armoured ;;
  esac
done
echo "$(date +%T) hero_tex done: $*" >> $S
