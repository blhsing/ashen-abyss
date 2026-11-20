#!/bin/bash
R='Y:\tools\run.ps1'
until [ -f Y:/tools/named/resume_done.txt ]; do sleep 20; done
for i in 1 2 3 4 5 6; do
  while tasklist | grep -qi ashenabyss; do sleep 30; done
  powershell -NoProfile -ExecutionPolicy Bypass -File $R -GameArgs "--mute --new=emberblade --slot=91 --level=50 --tier=2 --paragon=50 --gear --god --bot --area=rift~pin:1 --speed=6 --quit-at=120" -Timeout 900 -Tag named_pinroute -Headless > Y:/tools/named/named_pinroute.txt 2>&1
  grep -q "giving up" Y:/tools/named/named_pinroute.txt || break
done
echo DONE > Y:/tools/named/resume2_done.txt
