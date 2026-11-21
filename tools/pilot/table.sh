#!/usr/bin/env bash
# Summarises results/boss.txt (or the file given) into a markdown table, one row per run (latest runs last).
f=${1:-/y/tools/pilot/results/boss.txt}
echo "| class | boss area | lv | result | run deaths | fight s | fight deaths | marked threats | dodged | taken | marked dmg | other dmg | ERR |"
echo "|---|---|---|---|---|---|---|---|---|---|---|---|---|"
awk '
/^=== /{ if (cls!="") emit(); cls=$5; area=$6; lv=$7; err=$NF; res="?"; rd="?"; fs="-"; fd="-"; th="-"; dg="-"; tk="-"; md="-"; od="-"; next }
/TEST RESULT/{ for(i=1;i<=NF;i++){ if($i ~ /^deaths=/) rd=substr($i,8); if($i ~ /^end=/) res=substr($i,5) } ; if ($0 ~ /end=boss/) res="killed"; }
/TEST FIGHT/{ for(i=1;i<=NF;i++){ split($i,kv,"="); if(kv[1]=="secs") fs=kv[2]; if(kv[1]=="deaths") fd=kv[2]; if(kv[1]=="threats") th=kv[2]; if(kv[1]=="dodged") dg=kv[2]; if(kv[1]=="taken") tk=kv[2]; if(kv[1]=="dmgTelegraphed") md=kv[2]; if(kv[1]=="dmgOther") od=kv[2]; if(kv[1]=="killed" && kv[2]=="False") res="not killed" } }
function emit(){ if (res=="?") return; printf "| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |\n", cls, area, lv, res, rd, fs, fd, th, dg, tk, md, od, err }
END{ if (cls!="") emit() }' "$f"
