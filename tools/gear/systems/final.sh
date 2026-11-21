#!/bin/bash
# SYSTEMS: the last checks in order (each waits for the run lock / the instance lock)
cd /y/tools/gear/systems
bash runwait.sh systems 300 -Headless -- --mute --suite=items > final_items.txt 2>&1
grep -E "FAIL|DONE" /y/tools/systems_out.txt >> final_items.txt
bash runwait.sh systems_ui 120 -- --win --mute --new=lampknight --slot=92 --level=20 --gear --hands=off "--uidir=Y:\shots\gear" "--uido=@Y:/tools/gear/systems/ui_compat2.txt" > final_ui.txt 2>&1
bash runwait.sh systems_ui 120 -- --win --mute --new=lampknight --slot=92 --level=20 --gear --hands=off "--uidir=Y:\shots\gear" "--uido=@Y:/tools/gear/systems/ui_filter_large.txt" >> final_ui.txt 2>&1
bash runwait.sh systems_sys 600 -Headless -- --mute --suite=systems > final_systems.txt 2>&1
grep -E "FAIL|DONE" /y/tools/systems_sys_out.txt >> final_systems.txt
mkdir -p "$APPDATA/AshenAbyss/saves_test_backup"
for f in "$APPDATA/AshenAbyss/saves/"slot92.* "$APPDATA/AshenAbyss/saves/"slot0.*; do [ -e "$f" ] && mv "$f" "$APPDATA/AshenAbyss/saves_test_backup/systems_final_$(basename "$f")"; done
echo FINAL-DONE >> final_systems.txt
