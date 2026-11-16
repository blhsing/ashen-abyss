# Look-dev iteration: build one creature (no stock preview) and render its raking-light close-ups.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\monsters\it.ps1 <id> [-Close "<extra closeup args>"] [-Preview]
param([string]$id, [string]$Close = "", [switch]$Preview)
$here = "Y:\tools\blender\monsters"
& powershell -NoProfile -ExecutionPolicy Bypass -File "$here\mk.ps1" $id -NoPreview | Where-Object { $_ -notmatch "More than one shader node tex image" }
if ($LASTEXITCODE -ne 0) { exit 1 }
& powershell -NoProfile -ExecutionPolicy Bypass -File "$here\bl.ps1" closeup.py -a "$id --rake --auto $Close"
if ($Preview) { & powershell -NoProfile -ExecutionPolicy Bypass -File "$here\bl.ps1" preview.py -a "$id" }
