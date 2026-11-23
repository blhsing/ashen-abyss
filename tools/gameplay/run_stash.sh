#!/bin/bash
# Shared stash tests (headless, slot 92 and its own test stash files slot92.stash*.json only). Save-restore agent.
out=Y:/tools/gameplay/test_out; sd="$APPDATA/AshenAbyss/saves"; R=Y:/tools/gameplay/run_one.sh
bash $R stash "--mute --new=emberblade --slot=92 --area=town --stash-keep --gptest=stash" 900
ts=$(date +%m%d_%H%M); bk="$APPDATA/AshenAbyss/saves_test_backup/stash_$ts"; mkdir -p "$bk"
mv "$sd"/slot92.* "$bk/" 2>/dev/null
grep -a "gptest\] \(PASS\|FAIL\|done\)" $out/stash.txt | sed 's/^ *[0-9.]* INF \[gptest\] //' > $out/stash_summary.txt
echo ALLDONE > $out/stash_done.txt
