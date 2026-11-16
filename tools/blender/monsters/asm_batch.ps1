# Batch driver of the creature assembler: ONE Blender run (through run_blender.ps1) builds the specs one after another,
# verifies them, renders the tiles (game camera, silhouette, raking-light bust, attack + walk pose) and writes the
# family sheets.  One run = one wait in the machine-wide 2-process queue.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\monsters\asm_batch.ps1 -Family bone
#   ... -Ids mon_a,mon_b [-Family fam]     build only these (the sheets still show the whole family)
#   ... -Family bone -NoBuild              sheets only          -NoShots / -NoVerify skip those steps
#   ... -Dry                               geometry only (no bake, nothing written): spec / part errors, triangle counts
# Sheets: Y:\shots\chars\family_<fam>.jpg, family_<fam>_sil.jpg, family_<fam>_detail<k>.jpg ; tiles in Y:\shots\chars\asm
# Full log of the last run: Y:\tools\blender\monsters\asm_out\batch_<name>.log
param([string]$Ids = "", [string]$Family = "", [switch]$NoBuild, [switch]$NoShots, [switch]$NoVerify, [switch]$Dry, [string]$ShotArgs = "")
$run = "Y:\tools\blender\run_blender.ps1"
$here = "Y:\tools\blender\monsters"
$py = "C:\Program Files\Blender Foundation\Blender 5.2\5.2\python\bin\python.exe"
$t0 = Get-Date
if ($Ids) { $list = @($Ids.Split(",") | Where-Object { $_ }) }
elseif ($Family) { $list = @((& $py "$here\asm_specs.py" --family $Family).Split(",") | Where-Object { $_ }) }
else { "give -Ids and / or -Family"; exit 2 }
$name = if ($Family) { $Family } else { $list[0] }
$shots = if ($Family) { "--family=$Family" } else { "--ids=" + ($list -join ",") }
if ($ShotArgs) { $shots += "+" + ($ShotArgs.Split(" ") -join "+") }
if ($Dry) {
    $a = @("-b", "--factory-startup", "--python", "$here\asm_dry.py", "--") + $list
} elseif ($NoBuild) {
    $a = @("-b", "--factory-startup", "--python", "$here\asm_shots.py", "--") + $shots.Split("+")
} else {
    $a = @("-b", "--factory-startup", "--python", "$here\asm_build.py", "--") + $list
    if (-not $NoVerify) { $a += "--verify" }
    if (-not $NoShots) { $a += "--shots=$shots+--fresh=" + ($list -join ",") }
}
$o = & $run -A $a 2>&1 | ForEach-Object { "$_" }
$o | Set-Content -Encoding UTF8 "$here\asm_out\batch_$name.log"
$o | Select-String -Pattern "^\[mon\] (asm \S+: |asm materials|asm budget|BUILD|WARNING|SHEET|SHOTS|MISSING|DRY)|^VERIFY|ERROR|Traceback|^\w*Error|^\s+File .*(asm|specs)" |
    ForEach-Object { $_.Line } | Where-Object { $_ -notmatch "More than one shader node tex image" }
"batch elapsed {0:n0}s" -f ((Get-Date) - $t0).TotalSeconds
