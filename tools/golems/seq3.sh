bash /y/tools/golems/retry_run.sh suite3 1200 1 "--mute --suite=golem --slot=95" | grep -E "log:|ERR|WRN|exit"
bash /y/tools/golems/botruns.sh bone_region sacrifice_region sacrifice_boss
