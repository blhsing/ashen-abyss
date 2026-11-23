#!/bin/bash
cd /y/tools/readme040
S='--shot-at=3,5,7,9,11,13,15,17,19,21,23,25,27,29 --quit-at=31'
while IFS='|' read -r n a; do
  [ -z "$n" ] && continue
  ./shoot.sh $n 200 "--win --mute --slot=99 --gear --bot --god $a --shot=Y:/shots/readme040/raw/$n.png $S" > /dev/null
  (cd /tmp; uv run --with pillow python Y:/tools/readme040/sheet.py Y:/shots/readme040/candidates/$n.jpg "$n: $a" $(ls -v Y:/shots/readme040/raw/${n}_*.png 2>/dev/null))
done < "$1"
echo "BATCH DONE $(date +%T) $1" >> runs.txt
