# First inventory open with a COLD shader and pipeline cache, without touching the user's own caches.
# The game is started with APPDATA and LOCALAPPDATA pointed at a fresh, empty folder (for that process only), so
# Godot's user:// (shader_cache, vulkan/ pipeline cache, settings, saves, logs) and the driver's per-user caches start
# empty. The user's settings.json is copied in (read only) so the run looks like theirs. Build first with run.ps1.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\ui_tests\portrait_cold.ps1 -Root <empty dir> -GameArgs "..." [-EngineArgs "--rendering-driver d3d12"]
param([Parameter(Mandatory)][string]$Root, [string]$GameArgs = '', [string]$EngineArgs = '', [int]$Timeout = 200)
$G = "$env:LOCALAPPDATA\Programs\Godot_4.7.2_mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$userSettings = Join-Path $env:APPDATA 'AshenAbyss\settings.json'
if (Test-Path $Root) { "root $Root exists: pass a new, empty folder"; exit 2 }
$roam = Join-Path $Root 'Roaming'; $local = Join-Path $Root 'Local'
New-Item -ItemType Directory -Force (Join-Path $roam 'AshenAbyss'), $local | Out-Null
if (Test-Path $userSettings) { Copy-Item $userSettings (Join-Path $roam 'AshenAbyss\settings.json') }
$mx = New-Object System.Threading.Mutex($false, 'Local\AshenAbyssRun')
$held = $false
try {
    try { $held = $mx.WaitOne([TimeSpan]::FromMinutes(25)) } catch [System.Threading.AbandonedMutexException] { $held = $true }
    if (-not $held) { "another test run held the lock for 25 minutes"; exit 3 }
    $a = @('--path', 'Y:\game', '--windowed', '--resolution', '1600x900', '--position', '160,90')
    if ($EngineArgs.Trim().Length -gt 0) { $a += ($EngineArgs.Trim() -split '\s+') }
    $a += '--'; $a += ($GameArgs.Trim() -split '\s+')
    $env:APPDATA = $roam; $env:LOCALAPPDATA = $local          # this process and the game only
    $p = Start-Process -FilePath $G -ArgumentList $a -RedirectStandardOutput (Join-Path $Root 'out.txt') -RedirectStandardError (Join-Path $Root 'err.txt') -PassThru -WindowStyle Hidden
    if (-not $p.WaitForExit($Timeout * 1000)) { "TIMEOUT - killing"; try { $p.Kill() } catch { } }
    "exit=$($p.ExitCode)"
    $log = Get-ChildItem (Join-Path $roam 'AshenAbyss\logs') -Filter 'game_*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($log) {
        $lines = Get-Content $log.FullName -Encoding UTF8
        "log: $($log.FullName) ERR=$(@($lines | Where-Object { $_ -match '^\s*[\d.]+ ERR ' }).Count)"
        $lines | Where-Object { $_ -match '\[gfx\] quality|level ready|uitest\] open|\[ui\] open|portrait\] (model|stats)|perf\] fps| ERR ' } | Select-Object -First 14 | ForEach-Object { if ($_.Length -gt 250) { $_.Substring(0, 250) } else { $_ } }
    } else { "NO GAME LOG" }
}
finally { if ($held) { $mx.ReleaseMutex() }; $mx.Dispose() }
