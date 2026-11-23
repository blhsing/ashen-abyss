#!/bin/bash
# battle candidates: name|args (each run ~80 s, priority lane). Stop: kill this bash pid (see NOTES.md)
cd /y/tools/readme040
S='--shot-at=8,12,16,20,24,28,32,36,40,44,48,52,56,60 --quit-at=62'
B='--win --mute --slot=99 --gear --bot --focus --god'
while IFS='|' read -r n a; do
  [ -z "$n" ] && continue
  ./shoot.sh $n 220 "$B $a --shot=Y:/shots/readme040/raw/$n.png $S" > /dev/null
  (cd /tmp; uv run --with pillow python Y:/tools/readme040/sheet.py Y:/shots/readme040/candidates/$n.jpg "$n: $a" $(ls -v Y:/shots/readme040/raw/${n}_*.png 2>/dev/null)) 
  grep -E "golem stance|--gear:|--hands|named\]" r_${n}_out.txt | cut -c1-220 >> runs.txt
done < "${1:-battle_list.txt}"
echo "BATCH DONE $(date +%T) ${1:-battle_list.txt}" >> runs.txt
