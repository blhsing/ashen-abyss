#!/bin/bash
# Exact save resume tests (headless, ONE private slot: 92). Save-restore agent, 0.4.0.
#  1. save process: every case is played into slot 92, saved, copied to test_out/case_<name>.json (+ exp_<name>.json)
#  2. verify process: each case (or a file derived from one: old format, truncated, corrupt) is put into slot 92,
#     loaded and checked; "gptest PASS/FAIL" lines in the log.
#  3. slot 92 (and a slot 0 if one appeared) are moved to saves_test_backup/saverestore_<time>.
out=Y:/tools/gameplay/test_out; sd="$APPDATA/AshenAbyss/saves"; R=Y:/tools/gameplay/run_one.sh
mkdir -p $out
# a real 0.3-era save (format 1, 9 equipment slots, no gear sets): the SYSTEMS migration fixture
cp Y:/tools/gear/systems/fixtures/old_emberblade_l50.json $out/case_real03.json
if [ -e "$sd/slot92.json" ]; then echo "slot 92 already holds a save: moving it aside"; mkdir -p "$APPDATA/AshenAbyss/saves_test_backup/saverestore_pre"; mv "$sd"/slot92.* "$APPDATA/AshenAbyss/saves_test_backup/saverestore_pre/"; fi

SAVE="dun:emberblade:wastes_d2,ow:hunter:wastes,town:lampknight:town,portal:wildsoul:wastes_d2:portal,boss:forgebody:wastes_d2:boss,dead:astromancer:wastes_d2:dead,named:soulbinder:wastes:named,event:pactbinder:wastes:event,rift:emberblade:rift=1,hc:hunter:wastes_d2:hc,hcdead:lampknight:wastes_d2:hcdead"
VERIFY="dun,ow,town,portal:portal,boss,dead,named,event,rift,hc,old:old:dun,oldtown:old:town,oldow:old:ow,real03:real03,trunc:trunc:dun:ow,badtypes:badtypes:dun,badpos:badpos:dun,badrecs:badrecs:dun,hcdead:hcdead"
[ "$1" != "verify" ] && bash $R save "--mute --new=emberblade --slot=92 --area=town --level=8 --gear --god --gptest=saveall --gpcases=$SAVE" 1200
bash $R verify "--mute --new=emberblade --slot=92 --area=town --god --gptest=verifyall --gpcases=$VERIFY" 1200
ts=$(date +%m%d_%H%M); bk="$APPDATA/AshenAbyss/saves_test_backup/saverestore_$ts"; mkdir -p "$bk"
mv "$sd"/slot92.* "$bk/" 2>/dev/null
grep -a "PASS\|FAIL\| ERR \|done mode" $out/verify.txt | grep -a gptest | sed 's/^ *[0-9.]* INF \[gptest\] //' > $out/verify_summary.txt
echo ALLDONE > $out/save_done.txt
