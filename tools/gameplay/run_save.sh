#!/bin/bash
# Save → load resume tests (headless). Slots 93/94/96/97/98 (private). Results: grep "gptest|levelstate|resume" in each log.
out=Y:/tools/gameplay/test_out
run() { # tag, args
  while tasklist | grep -qi AshenAbyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -GameArgs "$2" -Timeout 180 -Tag gameplay -Headless $NB > $out/$1.txt 2>&1
  log=$(grep -o "log: [^ ]*" $out/$1.txt | cut -c6-); [ -n "$log" ] && grep -a "gptest\|ERR \|levelstate\|\[save\] resume\|no resume\|hero resumed\|travel →\|entered \|chase\|run-up" "$log" >> $out/$1.txt
  NB="-NoBuild"
}
NB="$1"
run save_dungeon "--mute --new=emberblade --slot=93 --area=wastes_d2 --level=8 --gear --god --gptest=save --gpexpect=$out/exp_dungeon.json"
run verify_dungeon "--mute --load=93 --gptest=verify --gpexpect=$out/exp_dungeon.json"
run save_overworld "--mute --new=hunter --slot=94 --area=wastes --level=8 --gear --god --gptest=save --gpexpect=$out/exp_ow.json"
run verify_overworld "--mute --load=94 --gptest=verify --gpexpect=$out/exp_ow.json"
run save_town "--mute --new=lampknight --slot=96 --area=town --level=8 --gptest=save --gpexpect=$out/exp_town.json"
run verify_town "--mute --load=96 --gptest=verify --gpexpect=$out/exp_town.json"
run save_portal "--mute --new=wildsoul --slot=97 --area=wastes_d2 --level=8 --gear --god --gptest=portal --gpexpect=$out/exp_portal.json"
run verify_portal "--mute --load=97 --gptest=portalback --gpexpect=$out/exp_portal.json"
# an old-format save: slot 93 without the new fields, format 1
sd="$APPDATA/AshenAbyss/saves"
powershell -NoProfile -Command "\$j = Get-Content -Raw -Encoding UTF8 '$sd/slot93.json' | ConvertFrom-Json; foreach (\$n in 'Resume','Portal','Fog') { \$j.PSObject.Properties.Remove(\$n) }; \$j.Version = 1; [IO.File]::WriteAllText('$sd/slot98.json', (\$j | ConvertTo-Json -Depth 64 -Compress))"
run old_format "--mute --load=98 --gptest=idle"
echo ALLDONE > $out/save_done.txt
