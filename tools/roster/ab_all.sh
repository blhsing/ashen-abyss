#!/usr/bin/env bash
# Per-region roster A/B (bestiary vs old, same seed, emberblade --gear, 600 s game each).
for a in "wastes 5" "mire 12" "frost 26" "sands 32" "sanctum 37" "abyss 43"; do set -- $a; rm -f Y:/tools/roster/logs/ab_$1.txt; bash Y:/tools/roster/area_ab.sh $1 $2 emberblade; done
