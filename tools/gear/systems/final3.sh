#!/bin/bash
cd /y/tools/gear/systems
bash runwait.sh systems_ui 120 -- --win --mute --new=soulbinder --slot=92 --level=20 --gear --hands=off "--uidir=Y:\shots\gear" "--uido=@Y:/tools/gear/systems/ui_sets2.txt" > final3_ui.txt 2>&1
grep -E "sets\]| ERR |uitest\] target|WRN \[uitest" /y/tools/systems_ui_out.txt >> final3_ui.txt
bash runwait.sh systems 300 -Headless -- --mute --suite=items > final3_items.txt 2>&1
grep -E "FAIL|DONE" /y/tools/systems_out.txt >> final3_items.txt
for f in "$APPDATA/AshenAbyss/saves/"slot92.*; do [ -e "$f" ] && mv "$f" "$APPDATA/AshenAbyss/saves_test_backup/systems_sets2_$(basename "$f")"; done
echo F3-DONE >> final3_items.txt
