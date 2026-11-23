#!/bin/bash
# GOLEM bot fights (no god mode). Usage: bash Y:/tools/golems/botruns.sh [names...]; logs in Y:/tools/golems/logs/<name>.log
# Each run queues on the shared run lock (run.ps1). Stop: kill this script's bash pid (recorded in NOTES.md).
LOGDIR="$APPDATA/AshenAbyss/logs"
declare -A RUNS=(
  [bone_region]="--new=soulbinder --slot=95 --level=9 --gear --bot --focus --area=wastes --golem=bone --speed=3 --quit-at=420"
  [blood_region]="--new=soulbinder --slot=95 --level=9 --gear --bot --focus --area=wastes --golem=blood --speed=3 --quit-at=420"
  [iron_region]="--new=soulbinder --slot=95 --level=9 --gear --bot --focus --area=wastes --golem=iron --forge --speed=3 --quit-at=420"
  [bone_boss1]="--new=soulbinder --slot=95 --level=10 --gear --bot --focus --bossrush --area=wastes_boss --golem=bone --speed=3 --quit-at=480"
  [blood_boss1]="--new=soulbinder --slot=95 --level=10 --gear --bot --focus --bossrush --area=wastes_boss --golem=blood --speed=3 --quit-at=480"
  [iron_boss1]="--new=soulbinder --slot=95 --level=10 --gear --bot --focus --bossrush --area=wastes_boss --golem=iron --forge --speed=3 --quit-at=480"
  [bone_boss2]="--new=soulbinder --slot=95 --level=18 --gear --bot --focus --bossrush --area=frost_boss --golem=bone --speed=3 --quit-at=600"
  [blood_boss2]="--new=soulbinder --slot=95 --level=18 --gear --bot --focus --bossrush --area=frost_boss --golem=blood --speed=3 --quit-at=600"
  [iron_boss2]="--new=soulbinder --slot=95 --level=18 --gear --bot --focus --bossrush --area=frost_boss --golem=iron --forge --speed=3 --quit-at=600"
  [sacrifice_region]="--new=soulbinder --slot=95 --level=26 --gear --bot --focus --area=frost --sacrifice=all --speed=3 --quit-at=420"
  [bone_frost26]="--new=soulbinder --slot=95 --level=26 --gear --bot --focus --area=frost --golem=bone --speed=3 --quit-at=420"
  [sacrifice_boss]="--new=soulbinder --slot=95 --level=26 --gear --bot --focus --bossrush --area=frost_boss --sacrifice=all --speed=3 --quit-at=600"
)
ORDER=(bone_region blood_region iron_region bone_boss1 blood_boss1 iron_boss1 bone_boss2 blood_boss2 iron_boss2 sacrifice_region sacrifice_boss)
[ $# -gt 0 ] && ORDER=("$@")
for n in "${ORDER[@]}"; do
  args="${RUNS[$n]} --mute"
  echo "=== $n: $args  ($(date +%T))"
  while tasklist | grep -qi "^AshenAbyss"; do echo "user is playing; waiting 30 s"; sleep 30; done
  for att in 1 2 3 4 5 6; do
    powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "$args" -Timeout 900 -Headless -NoBuild -Tag "golems\bot_$n" > "/y/tools/golems/logs/${n}_run.txt" 2>&1
    grep -q "giving up" "/y/tools/golems/logs/${n}_run.txt" || break
    echo "lock busy (attempt $att)"
  done
  newest=$(grep -o "^log: [^ ]*" "/y/tools/golems/logs/${n}_run.txt" | head -1 | cut -c6-)
  [ -n "$newest" ] && cp "$(cygpath -u "$newest")" "/y/tools/golems/logs/$n.log"
  grep -E "^log:|exit=" "/y/tools/golems/logs/${n}_run.txt"
  grep -E '\[hero\] died:|\[boss\] .*(slain|disengaged|soft enrage)|final summary|golem stance|\[golem\] .*(fell|re-formed)| ERR ' "/y/tools/golems/logs/$n.log" | cut -c1-240 | head -40
  echo "=== $n done $(date +%T)"
done
