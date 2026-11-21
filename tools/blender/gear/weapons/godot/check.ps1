# Runs godot\check_gear.gd headless under the machine-wide run lock:  check.ps1 -Ids wpn_sword1_v2,wpn_axe1_v2
param([string[]]$Ids = @(), [string]$Script = 'check_gear.gd')
$G = "$env:LOCALAPPDATA\Programs\Godot_4.7.2_mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
if ($Ids.Count -eq 1 -and $Ids[0].Contains(',')) { $Ids = $Ids[0] -split ',' }
$mx = New-Object System.Threading.Mutex($false, 'Local\AshenAbyssRun')
$held = $false
try {
    try { $held = $mx.WaitOne([TimeSpan]::FromMinutes(20)) } catch [System.Threading.AbandonedMutexException] { $held = $true }
    if (-not $held) { "check.ps1: run lock busy"; exit 3 }
    $a = @('--headless', '--path', 'Y:\game', '-s', (Join-Path $PSScriptRoot $Script), '--') + $Ids
    & $G @a 2>&1 | ForEach-Object { "$_" } | Where-Object { $_ -notmatch '^Godot Engine|^$' }
}
finally { if ($held) { $mx.ReleaseMutex() }; $mx.Dispose() }
