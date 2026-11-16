#!/usr/bin/env bash
# Runs the full inventory-portrait script (portrait.txt) for several classes, one game run each.
#   bash Y:/tools/ui_tests/portrait_batch.sh emberblade astromancer lampknight wildsoul forgebody
# Pictures go to Y:/shots/portrait/<class>/. Waits while the installed game is running (never tests over the player).
set -u
first=1
for c in "$@"; do
  while tasklist | grep -qi "AshenAbyss"; do echo "the game is running, waiting 30 s"; sleep 30; done
  mkdir -p "Y:/shots/portrait/$c"
  nb=""
  if [ "$first" = 0 ]; then nb="-NoBuild"; fi
  first=0
  powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--win --mute --new=$c --level=20 --gear --area=town --slot=96 --portrait-stats --uidir=Y:/shots/portrait/$c --uido=@Y:/tools/ui_tests/portrait.txt --quit-at=34" -Timeout 150 -Tag portrait $nb 2>&1 | grep -E "BUILD|exit=|ERR=|error| x " | head -8
  L=$(ls -t "$APPDATA/AshenAbyss/logs/"game_*.log | head -1)
  echo "== $c  ($L)"
  grep -E "portrait\] (model|figure|camera)|ERR" "$L" | cut -c1-230
  grep -E "stats:" "$L" | sed -n '2p;5p;9p' | sed -E 's/.*stats: //' | cut -c1-260
  ls "Y:/shots/portrait/$c" | wc -l
done
echo "batch done"
