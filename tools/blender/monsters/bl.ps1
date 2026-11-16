# Run one kit script in headless Blender through the throttled launcher:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\monsters\bl.ps1 <script.py> "<args>" [-All]
param([string]$script, [string]$a = "", [switch]$All)
$run = "Y:\tools\blender\run_blender.ps1"
$t = Get-Date
$bargs = @("-b", "--factory-startup", "--python", "Y:\tools\blender\monsters\$script", "--")
if ($a) { $bargs += ($a.Split(" ") | Where-Object { $_ -ne "" }) }
$o = & $run -A $bargs 2>&1 | ForEach-Object { "$_" }
if ($All) { $o } else {
    $o | Select-String -Pattern "^\[mon\]|^\[montex\]|Traceback|Error|^\s+File |VERIFY|MANIFEST|WARNING|SMOKE" | ForEach-Object { $_.Line } | Select-Object -Last 80
}
"elapsed {0:n1}s" -f ((Get-Date) - $t).TotalSeconds
