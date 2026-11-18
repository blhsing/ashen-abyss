#!/bin/bash
# All gameplay tests in three game processes (fewer turns on the shared run lock). Headless.
#   1. chaseall  : run-up for all 8 classes (+ wildsoul forms) in wastes, cancel cases in wastes_d2   (slot 92)
#   2. saveall   : dungeon (93), overworld (94), town (96), town portal from a dungeon (97); quit
#   3. old-format copy of slot 93 → slot 98 (no Resume/Portal/Fog, Version 1)
#   4. verifyall : a new process loads 93 / 94 / 96 / 97 (portal back) / 98 and checks them
# Results: Y:/tools/gameplay/test_out/<step>.txt (gptest PASS/FAIL lines are appended from the game log).
out=Y:/tools/gameplay/test_out
mkdir -p $out
run() { # tag, args, [extra run.ps1 switches]
  while tasklist | grep -qi AshenAbyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File Y:/tools/run.ps1 -GameArgs "$2" -Timeout ${3:-400} -Tag gameplay -Headless > $out/$1.txt 2>&1
  log=$(grep -ao "log: [^ ]*" $out/$1.txt | cut -c6-)
  [ -n "$log" ] && { echo "--- gameplay lines from $log"; grep -a "gptest\| ERR \|levelstate\|\[save\] resume\|no resume\|resume state\|hero resumed\|run-up\|area\] travel\|area\] entered" "$log"; } >> $out/$1.txt
}
step=${1:-all}
if [ "$step" = all ] || [ "$step" = chase ]; then
  run chaseall "--mute --new=emberblade --slot=92 --area=wastes --level=8 --gear --god --gptest=chaseall" 600
fi
if [ "$step" = all ] || [ "$step" = save ]; then
  run saveall "--mute --new=emberblade --slot=93 --area=wastes_d2 --level=8 --gear --god --gptest=saveall" 400
  sd="$APPDATA/AshenAbyss/saves"
  powershell -NoProfile -Command "\$j = Get-Content -Raw -Encoding UTF8 '$sd/slot93.json' | ConvertFrom-Json; foreach (\$n in 'Resume','Portal','Fog') { \$j.PSObject.Properties.Remove(\$n) }; \$j.Version = 1; [IO.File]::WriteAllText('$sd/slot98.json', (\$j | ConvertTo-Json -Depth 64 -Compress))" > $out/oldformat.txt 2>&1
  cp "$sd/slot93.json" $out/slot93_saved.json 2>/dev/null
  run verifyall "--mute --load=93 --gptest=verifyall" 400
fi
echo ALLDONE > $out/done_$step.txt
