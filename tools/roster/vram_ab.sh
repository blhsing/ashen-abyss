#!/usr/bin/env bash
# VRAM A/B: rosterperf (40 wastes monsters vs a standing hero, windowed) with both rosters, before and after the
# texture change. Logs copied to Y:/tools/roster/logs/<tag>.log.  bash vram_ab.sh <phase: before|after|both>
ph=${1:-both}
L=Y:/tools/roster/logs
perf() { # tag args
  out=$(bash Y:/tools/roster/runq.sh "$1" 300 "--win --mute $2 --seed=4242 --slot=90 --suite=rosterperf:wastes:40" $3)
  f=$(echo "$out" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  [ -n "$f" ] && cp "$f" "$L/$1.log"
  echo "$1: $(echo "$out" | grep -E 'exit=|ERR=' | head -2 | tr '\n' ' ') log=$f"
  [ -n "$f" ] && grep -a "\[perf\] fps" "$L/$1.log" | tail -3 | grep -o "fps=[0-9]*\|frame=[0-9.]*ms\|monsters=[0-9]*\|draws=[0-9]*\|vram=[0-9]*MB" | tr '\n' ' '; echo
}
if [ "$ph" = before ] || [ "$ph" = both ]; then
  perf vram_before_best "--bestiary" ""
  perf vram_before_old "--no-bestiary" "-NoBuild"
fi
if [ "$ph" = both ]; then
  while tasklist | grep -qi "ashenabyss.exe"; do sleep 30; done
  bash Y:/tools/roster/tex_vram.sh
  powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/import.ps1
fi
if [ "$ph" = after ] || [ "$ph" = both ]; then
  perf vram_after_best "--bestiary" ""
  perf vram_after_old "--no-bestiary" "-NoBuild"
fi
echo "vram_ab done $(date +%H:%M:%S)"
