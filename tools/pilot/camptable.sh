#!/usr/bin/env bash
# One block per campaign run in results/campaign.txt: the result line and each boss fight.
f=${1:-/y/tools/pilot/results/campaign.txt}
awk '
/^=== /{ print ""; print "### " $5 " (" $2 " " $3 ") " $NF; next }
/TEST RESULT/{ sub(/^.*TEST RESULT /, ""); print "RESULT " $0; next }
/TEST FIGHT/{ sub(/^.*TEST FIGHT /, ""); print "  " $0 }' "$f"
