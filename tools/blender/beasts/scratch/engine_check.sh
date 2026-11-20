#!/bin/bash
[ -f "Y:/tools/blender/beasts/scratch/PAUSED" ] && { echo "PAUSED file present: engine check skipped"; exit 0; }
# In-engine check of the 46 creatures: line-ups through the roster harness (--bestiary --spawn), test slot 96.
#   bash Y:/tools/blender/beasts/scratch/engine_check.sh [first group] [last group]
# Never runs while the user's game is open (polls every 30 s). Screenshots: Y:\tools\blender\beasts\out\engine\g<N>_<1|2>.png
OUT='Y:\tools\blender\beasts\out\engine'
mkdir -p "Y:/tools/blender/beasts/out/engine"
G=(
"wastes|dusk_hound,dusk_boar,dusk_yak,dusk_lynx,dusk_lizard|"
"wastes|dusk_crow,dusk_bat,dusk_gadfly,dusk_moths,wraith_gasp|"
"frost|rime_packbeast,cf_flicker,cf_mite,effigy_hound,bog_toad|"
"crypt|sexton_grub,sexton_beetle,sexton_digger,sexton_matron,char_root|"
"crypt|shroud_ling,shroud_weaver,shroud_dropper,shroud_matron,ash_worm|"
"mire|lurk_leech,lurk_eel,lurk_spitter,lurk_elder,lamp_lure|"
"sands|rust_helm,rust_stinger,rust_gear,rust_jar,rust_locust|"
"sanctum|lamp_crawler,engine_heliostat,bell_hanging,forge_eye,forge_hound|"
"abyss|fiend_wing,fiend_maw,hush_ribwalker,hush_beast,hush_serpent|"
"abyss|hush_wing,lurk_eel,dusk_bat,wraith_gasp,dusk_hound|--spawn-aggro"
"mire|lurk_leech,sexton_beetle,lurk_spitter,dusk_crow,cf_flicker|--spawn-aggro"
)
a=${1:-1}; b=${2:-${#G[@]}}
nb=""
for ((i=a; i<=b; i++)); do
  IFS='|' read -r area ids extra <<< "${G[$((i-1))]}"
  while tasklist | grep -qi "AshenAbyss"; do echo "user is playing - waiting"; sleep 30; done
  echo "=== group $i: $area $ids $extra"
  powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--win --mute --bestiary --new=emberblade --slot=96 --area=$area --level=30 --gear --god --spawn=$ids $extra --shot=$OUT\\g$i.png --shot-at=4,7.5 --quit-at=9" -Timeout 240 -Tag beasts $nb 2>&1 | grep -v "^$" | cut -c1-260 | head -30
  nb="-NoBuild"
  log=$(ls -t "$APPDATA/AshenAbyss/logs"/game_*.log 2>/dev/null | head -1)
  if [ -n "$log" ]; then grep -E "animates with its|lacks the clips|--spawn:|stand-in|rigs_bf_meta" "$log" | sed 's/^ *[0-9.]* [A-Z]* *//' | cut -c1-330 | sort -u | head -14; fi
done
mkdir -p "$APPDATA/AshenAbyss/saves_test_backup"
mv "$APPDATA/AshenAbyss/saves"/slot96* "$APPDATA/AshenAbyss/saves_test_backup/" 2>/dev/null
echo "engine check done"
