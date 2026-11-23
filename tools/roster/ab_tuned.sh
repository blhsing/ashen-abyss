#!/usr/bin/env bash
# After the balance pass: bestiary-only re-runs (tag ab2_<area>) for the regions that were overtuned.
for a in "mire 12" "frost 26" "crypt 16" "wastes 5"; do set -- $a; rm -f Y:/tools/roster/logs/ab2_$1.txt; bash Y:/tools/roster/area_ab2.sh $1 $2 emberblade ab2_$1 bestiary; done
