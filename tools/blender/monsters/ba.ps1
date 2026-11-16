# Before/after raking-light close-ups of one creature: "before" from out\v1_glb (suffix _v1), "after" from the game folder.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\monsters\ba.ps1 <id>
param([string]$id)
$here = "Y:\tools\blender\monsters"
& powershell -NoProfile -ExecutionPolicy Bypass -File "$here\bl.ps1" closeup.py -a "$id --rake --auto --src=$here\out\v1_glb --suffix=_v1"
& powershell -NoProfile -ExecutionPolicy Bypass -File "$here\bl.ps1" closeup.py -a "$id --rake --auto"
