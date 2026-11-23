#!/usr/bin/env bash
# Monster textures (assets/chars/mon_*.jpg) to VRAM compression: BC7 for colour / ORM / emission, BC5 (RGTC) for
# normal maps. Before: lossless (uncompressed RGBA8 in video memory, ~21 MB per 2K map). Backup of the .import files
# in Y:/tools/roster/tex_backup (restore: copy them back, then Y:/tools/import.ps1).
cd Y:/game/assets/chars || exit 1
[ -n "$(ls Y:/tools/roster/tex_backup/*.import 2>/dev/null | head -1)" ] || cp mon_*.jpg.import Y:/tools/roster/tex_backup/
n=0
for f in mon_*.jpg.import; do
  sed -i -e 's/^compress\/mode=0/compress\/mode=2/' -e 's/"vram_texture": false/"vram_texture": true/' "$f"
  case "$f" in
    *_nrm.jpg.import) sed -i -e 's/^compress\/normal_map=0/compress\/normal_map=1/' -e 's/^compress\/high_quality=true/compress\/high_quality=false/' "$f";;
    *) sed -i -e 's/^compress\/high_quality=false/compress\/high_quality=true/' "$f";;
  esac
  n=$((n+1))
done
echo "tex_vram: $n import files set to VRAM compression"
