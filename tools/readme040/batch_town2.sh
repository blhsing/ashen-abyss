#!/bin/bash
cd /y/tools/readme040
S='--shot-at=1,1.8,2.6,3.4,4.2,5,5.8,6.6,7.4,8.2,9,10 --quit-at=11'
while IFS='|' read -r n a; do
  [ -z "$n" ] && continue
  ./shoot.sh $n 200 "--win --mute --slot=99 --gear --bot --god $a --shot=Y:/shots/readme040/raw/$n.png $S" > /dev/null
  (cd /tmp; uv run --with pillow python Y:/tools/readme040/sheet.py Y:/shots/readme040/candidates/$n.jpg "$n: $a" $(ls -v Y:/shots/readme040/raw/${n}_*.png 2>/dev/null))
done < "$1"
echo "BATCH DONE $(date +%T) $1" >> runs.txt
