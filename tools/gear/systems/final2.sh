#!/bin/bash
cd /y/tools/gear/systems
bash runwait.sh systems 300 -Headless -- --mute --suite=items > final2_items.txt 2>&1
grep -E "FAIL|DONE" /y/tools/systems_out.txt >> final2_items.txt
bash runwait.sh systems_ui 150 -- --win --mute --new=emberblade --slot=92 --area=wastes --level=20 --gear --hands=2h --bot "--uidir=Y:\shots\gear" "--uido=@Y:/tools/gear/systems/ui_sets.txt" > final2_ui.txt 2>&1
grep -E "sets\]| ERR |uitest\] target|WRN \[uitest" /y/tools/systems_ui_out.txt >> final2_ui.txt
for f in "$APPDATA/AshenAbyss/saves/"slot92.*; do [ -e "$f" ] && mv "$f" "$APPDATA/AshenAbyss/saves_test_backup/systems_sets_$(basename "$f")"; done
echo F2-DONE >> final2_ui.txt
