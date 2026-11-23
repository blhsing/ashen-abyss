#!/usr/bin/env bash
# Roster/bestiary test chain (slot 90). Each step waits while the user plays and retries while the run lock is busy.
# Logs: Y:/tools/roster/logs/<tag>.log, summary lines in Y:/tools/roster/logs/chain.txt.
#   bash chain.sh <step> [<step> ...]   steps: import perf creatures ai named demo camp_ember camp_hunter camp_ember_old
L=Y:/tools/roster/logs; S=$L/chain.txt
waituser() { while tasklist 2>/dev/null | grep -qi '^AshenAbyss'; do sleep 30; done; }
run() { # tag timeout "args" [run.ps1 switches]
  local tag=$1 tmo=$2 args=$3; shift 3
  for try in $(seq 1 40); do
    waituser
    res=$(powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$args" -Timeout $tmo -Tag "roster_$tag" "$@" 2>&1)
    if echo "$res" | grep -q "held the lock"; then continue; fi
    if echo "$res" | grep -q "BUILD FAILED"; then echo "$(date +%T) $tag build failed, retry" >> $S; sleep 90; continue; fi
    f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
    if [ -n "$f" ] && grep -aq "this test launch exits" "$f"; then sleep 60; continue; fi
    break
  done
  f=$(echo "$res" | grep -o "log: [^ ]*" | head -1 | cut -c6-)
  [ -n "$f" ] && cp "$f" "$L/$tag.log"
  echo "$(date '+%F %T') $tag: $(echo "$res" | grep -E 'exit=|ERR=|TIMEOUT' | head -3 | tr '\n' ' ')" >> $S
  mkdir -p "$APPDATA/AshenAbyss/saves_test_backup"
  mv "$APPDATA"/AshenAbyss/saves/slot90.* "$APPDATA"/AshenAbyss/saves/slot0.* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
}
for step in "$@"; do
  case $step in
    import) for try in $(seq 1 20); do waituser; r=$(powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/import.ps1 2>&1); echo "$(date +%T) import: $(echo "$r" | head -4 | tr '\n' ' ')" >> $S; echo "$r" | grep -q "import exit=" && break; done ;;
    perf) run vram_after_best 300 "--win --mute --bestiary --seed=4242 --slot=90 --suite=rosterperf:wastes:40"
          run vram_after_old 300 "--win --mute --no-bestiary --seed=4242 --slot=90 --suite=rosterperf:wastes:40" -NoBuild ;;
    creatures) run creatures 1500 "--win --mute --bestiary --slot=90 --suite=bestiary:creatures:shots --shot=Y:\shots\roster\cr.png --shot-at=100000" ;;
    ai) run ai 1500 "--mute --bestiary --slot=90 --suite=ai" -Headless ;;
    named) run named_rare 2400 "--mute --bestiary --new=emberblade --slot=90 --suite=named:rare --speed=6" -Headless ;;
    demo) run demo 600 "--win --mute --bestiary --demo --demo-secs=14 --quit-at=300" ;;
    camp_ember) run camp_ember_best 4800 "--mute --new=emberblade --slot=90 --pilot-test=campaign:18000 --speed=4 --bestiary" -Headless ;;
    camp_hunter) run camp_hunter_best 4800 "--mute --new=hunter --slot=90 --pilot-test=campaign:18000 --speed=4 --bestiary" -Headless ;;
    camp_ember_old) run camp_ember_old 4800 "--mute --new=emberblade --slot=90 --pilot-test=campaign:18000 --speed=4 --no-bestiary" -Headless ;;
  esac
done
echo "$(date +%T) chain done: $*" >> $S
