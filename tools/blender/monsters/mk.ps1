# Build + preview one creature:  powershell -File Y:\tools\blender\monsters\mk.ps1 <id> [-NoPreview] [-Prev "<preview args>"]
# Blender is always started through run_blender.ps1 (max 2 Blender processes machine-wide, capped threads, keeps the PC awake).
param([string]$id, [switch]$NoPreview, [string]$Prev = "", [string]$BuildFlags = "")
$run = "Y:\tools\blender\run_blender.ps1"
$here = "Y:\tools\blender\monsters"
$t = Get-Date
$bargs = @("-b", "--factory-startup", "--python", "$here\build.py", "--", $id)
if ($BuildFlags) { $bargs += $BuildFlags.Split(" ") }
$o = & $run -A $bargs 2>&1 | ForEach-Object { "$_" }
$o | Select-String -Pattern "^\[mon\]|Traceback|Error|^\s+File |WARNING: (OVER|feet|bad|more)" | ForEach-Object { $_.Line } | Select-Object -Last 40
if (-not ($o | Select-String -SimpleMatch "BUILD OK")) { "BUILD DID NOT COMPLETE"; $o | Select-Object -Last 25; exit 1 }
if (-not $NoPreview) {
    $pargs = @("-b", "--factory-startup", "--python", "$here\preview.py", "--", $id)
    if ($Prev) { $pargs += $Prev.Split(" ") }
    $p = & $run -A $pargs 2>&1 | ForEach-Object { "$_" }
    $p | Select-String -Pattern "^\[mon\]|Traceback|Error:|^\s+File " | ForEach-Object { $_.Line } | Select-Object -Last 20
}
"elapsed {0:n1}s" -f ((Get-Date) - $t).TotalSeconds
