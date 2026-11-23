#!/bin/bash
# named-encounter engineer: fight-length table from two suite logs (stand-ins, --bestiary) and the final pilot logs
# usage: table.sh <suite.log> <suite_bestiary.log> [pilot prefix, default final_pin]
cd Y:/tools/named/runs
S=$1; B=$2; P=${3:-final_pin}
rows() { grep -a "\[suite\]   " "$1" | sed -E 's/^.*\[suite\]   //' | awk '{id=$1; st=$2; if (st=="ALIVE") {t=$4; v="ALIVE "$3} else {t=$3; v=$6} print id"\t"t"\t"v}'; }
rows "$S" > /tmp/t_s.txt; rows "$B" > /tmp/t_b.txt
echo "| id | window | stand-ins: time | verdict | bestiary on: time | verdict | no-god AI pilot |"
echo "|---|---|---|---|---|---|---|"
while IFS=$'\t' read id t v; do
  w=$(grep -a "\[suite\]   $id " "$S" | grep -oE "window [0-9]+-[0-9]+" | head -1 | cut -c8-)
  bl=$(grep -P "^$id\t" /tmp/t_b.txt | head -1); bt=$(echo "$bl" | cut -f2); bv=$(echo "$bl" | cut -f3)
  pil=""
  if [[ $id == pin_* ]]; then r=${id:4:1}; f="$P$r.log"; [ -f "$f" ] && pil=$(grep -a "TEST FIGHT" "$f" | head -1 | grep -oE "killed=[A-Za-z]+ secs=[0-9]+ deaths=[0-9]+"); fi
  echo "| $id | $w s | $t | $v | ${bt:--} | ${bv:--} | ${pil:--} |"
done < /tmp/t_s.txt
