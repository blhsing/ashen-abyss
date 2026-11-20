#!/bin/bash
# Exact save resume tests (headless, slots 92/93 only, --gpresume switches the feature on for the run).
#  A: dungeon (92) + overworld (93): saveall → new process verifyall
#  B: town (92) + town portal opened from a dungeon (93): saveall → verifyall (town spot, portal back, dungeon as left)
#  C: old format: phase-A slot 92 with Resume/Portal/Fog removed and Version 1 → loads the old way
out=Y:/tools/gameplay/test_out; sd="$APPDATA/AshenAbyss/saves"; R=Y:/tools/gameplay/run_one.sh
bash $R saveA "--mute --new=emberblade --slot=92 --area=wastes_d2 --level=8 --gear --god --gpresume --gptest=saveall --gpcases=92:emberblade:wastes_d2,93:hunter:wastes"
cp "$sd/slot92.json" $out/A_slot92.json; cp "$sd/slot93.json" $out/A_slot93.json
bash $R verifyA "--mute --load=92 --gpresume --gptest=verifyall --gpslots=92,93"
bash $R saveB "--mute --new=lampknight --slot=92 --area=town --level=8 --gear --god --gpresume --gptest=saveall --gpcases=92:lampknight:town,93:wildsoul:wastes_d2:portal"
cp "$sd/slot92.json" $out/B_slot92.json; cp "$sd/slot93.json" $out/B_slot93.json
bash $R verifyB "--mute --load=92 --gpresume --gptest=verifyall --gpslots=92,93"
powershell -NoProfile -Command "\$j = Get-Content -Raw -Encoding UTF8 '$out/A_slot92.json' | ConvertFrom-Json; foreach (\$n in 'Resume','Portal','Fog') { \$j.PSObject.Properties.Remove(\$n) }; \$j.Version = 1; [IO.File]::WriteAllText('$sd/slot92.json', (\$j | ConvertTo-Json -Depth 64 -Compress))" > $out/oldformat.txt 2>&1
rm -f "$sd/slot92.bak"
cp "$out/exp_92.json" "$out/exp_92_A.json" 2>/dev/null
bash $R verifyC "--mute --load=92 --gpresume --gptest=verifyall --gpslots=92 --gpold=92"
echo ALLDONE > $out/save_done.txt
