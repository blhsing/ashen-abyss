# Run one beast-kit script in headless Blender through the throttled launcher (never start blender.exe directly):
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\bb.ps1 bbuild.py "dusk_hound --nobake" [-All] [-Dir <folder of the script>]
param([string]$script, [string]$a = "", [switch]$All, [string]$Dir = "Y:\tools\blender\beasts")
$run = "Y:\tools\blender\run_blender.ps1"
$t = Get-Date
$bargs = @("-b", "--factory-startup", "--python", (Join-Path $Dir $script), "--")
if ($a) { $bargs += ($a.Split(" ") | Where-Object { $_ -ne "" }) }
$o = & $run -A $bargs 2>&1 | ForEach-Object { "$_" }
if ($All) { $o } else {
    $o | Select-String -Pattern "^\[beast\]|^\[mon\]|Traceback|Error|^\s+File |VERIFY|WARNING|NOTE|assert" | Where-Object { $_.Line -notmatch "More than one shader node tex image" } | ForEach-Object { $_.Line } | Select-Object -Last 120
}
"elapsed {0:n1}s" -f ((Get-Date) - $t).TotalSeconds
