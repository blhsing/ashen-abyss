#!/bin/bash
cd /y/tools/readme040
S='--shot-at=30,40,50,60,70,80,90,100,110,120,130,140,150,160,170,180,190,200 --quit-at=204'
while IFS='|' read -r n a; do
  [ -z "$n" ] && continue
  ./shoot.sh $n 290 "--win --mute --slot=99 --gear --bot --focus --god --bossrush $a --shot=Y:/shots/readme040/raw/$n.png $S" > /dev/null
  (cd /tmp; uv run --with pillow python Y:/tools/readme040/sheet.py Y:/shots/readme040/candidates/$n.jpg "$n: $a" $(ls -v Y:/shots/readme040/raw/${n}_*.png 2>/dev/null))
done < "$1"
echo "BATCH DONE $(date +%T) $1" >> runs.txt
