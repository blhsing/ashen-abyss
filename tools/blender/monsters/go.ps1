# Build + stock previews (with custom animation specs) + raking close-ups + optional verify, strictly one Blender run after another.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\monsters\go.ps1 <id> [-Prev "<preview args>"] [-Close "<closeup specs>"] [-Verify]
param([string]$id, [string]$Prev = "", [string]$Close = "", [switch]$Verify, [switch]$NoRake)
$here = "Y:\tools\blender\monsters"
if ($Prev) { & powershell -NoProfile -ExecutionPolicy Bypass -File "$here\mk.ps1" $id -Prev $Prev | Where-Object { $_ -notmatch "More than one shader node tex image" } }
else { & powershell -NoProfile -ExecutionPolicy Bypass -File "$here\mk.ps1" $id | Where-Object { $_ -notmatch "More than one shader node tex image" } }
if ($LASTEXITCODE -ne 0) { exit 1 }
if ($Close) {
    $rk = "--rake"; if ($NoRake) { $rk = "" }
    & powershell -NoProfile -ExecutionPolicy Bypass -File "$here\bl.ps1" closeup.py -a "$id $rk $Close"
}
if ($Verify) { & powershell -NoProfile -ExecutionPolicy Bypass -File "$here\bl.ps1" verify.py -a "$id" }
