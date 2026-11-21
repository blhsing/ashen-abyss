# Run a headless Godot tool script on the project (no window). Takes the same machine-wide lock as run.ps1 / import.ps1
# and refuses to start while the user's game runs.
#   powershell -NoProfile -ExecutionPolicy Bypass -File gd_run.ps1 -Script gd_deps.gd -A res://a.glb,res://b.glb
param([string]$Script, [string[]]$A = @())
if (@(Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue).Count -gt 0) { "gd_run: the game is running; not starting"; exit 4 }
$G = "$env:LOCALAPPDATA\Programs\Godot_4.7.2_mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$A = @($A | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' })
$mx = New-Object System.Threading.Mutex($false, 'Local\AshenAbyssRun')
$held = $false
try {
    try { $held = $mx.WaitOne([TimeSpan]::FromMinutes(20)) } catch [System.Threading.AbandonedMutexException] { $held = $true }
    if (-not $held) { "gd_run: run lock busy"; exit 3 }
    & $G --headless --path Y:\game --script "$PSScriptRoot\$Script" -- @A 2>&1 | ForEach-Object { "$_" }
}
finally { if ($held) { $mx.ReleaseMutex() }; $mx.Dispose() }
