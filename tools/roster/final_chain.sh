#!/usr/bin/env bash
# After the tuned A/B batch: mire + abyss check, then the two bestiary proof campaigns.
L=Y:/tools/roster/logs
until [ -f $L/ab2_wastes.txt ] && grep -q done $L/ab2_wastes.txt; do sleep 20; done
rm -f $L/ab3_mire.txt $L/ab3_abyss.txt
bash Y:/tools/roster/area_ab2.sh mire 12 emberblade ab3_mire bestiary
bash Y:/tools/roster/area_ab2.sh abyss 43 emberblade ab3_abyss "bestiary no-bestiary"
bash Y:/tools/roster/chain.sh camp_ember camp_hunter
