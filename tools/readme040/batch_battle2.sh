#!/bin/bash
# round 2: designed packs of new bestiary types spawned aggro'd at the entry (--spawn), shots in the first 30 s
cd /y/tools/readme040
S='--shot-at=2,3.5,5,6.5,8,9.5,11,13,15,17,20,23,26,29 --quit-at=31'
B='--win --mute --slot=99 --gear --bot --focus --god --spawn-aggro'
while IFS='|' read -r n a; do
  [ -z "$n" ] && continue
  case "$n" in \#*) continue;; esac
  ./shoot.sh $n 200 "$B $a --shot=Y:/shots/readme040/raw/$n.png $S" > /dev/null
  (cd /tmp; uv run --with pillow python Y:/tools/readme040/sheet.py Y:/shots/readme040/candidates/$n.jpg "$n: $a" $(ls -v Y:/shots/readme040/raw/${n}_*.png 2>/dev/null))
  grep -E "\-\-spawn|--gear:" r_${n}_out.txt | cut -c1-200 >> runs.txt
done < "$1"
echo "BATCH DONE $(date +%T) $1" >> runs.txt
