# Run one osaros_v2 script in headless Blender through the throttled launcher (never start blender.exe directly):
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\monsters\osaros_v2\bl.ps1 <script.py> "<args>" [-All] [-Dir <folder>]
param([string]$script, [string]$a = "", [switch]$All, [string]$Dir = "Y:\tools\blender\monsters\osaros_v2")
$run = "Y:\tools\blender\run_blender.ps1"
$t = Get-Date
$bargs = @("-b", "--factory-startup", "--python", "$Dir\$script", "--")
if ($a) { $bargs += ($a.Split(" ") | Where-Object { $_ -ne "" }) }
$o = & $run -A $bargs 2>&1 | ForEach-Object { "$_" }
if ($All) { $o } else {
    $o | Select-String -Pattern "^\[mon\]|^\[ov2\]|Traceback|Error|^\s+File |VERIFY|MANIFEST|WARNING|Exception|^\w+Error" | Where-Object { $_.Line -notmatch "More than one shader node tex image" } | ForEach-Object { $_.Line } | Select-Object -Last 120
}
"elapsed {0:n1}s" -f ((Get-Date) - $t).TotalSeconds
