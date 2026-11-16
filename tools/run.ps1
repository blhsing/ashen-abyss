# Build and run the game for a test session, then summarise what the logs say. Only one test run happens at a time
# machine-wide (named mutex), so several people can use this script without colliding on the build.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\run.ps1 -GameArgs "--win --mute --new=emberblade --area=wastes --level=5 --gear --bot --shot=Y:\shots\run\a.png --shot-at=8,20 --quit-at=25" -Timeout 90
# Pass ALL game switches as ONE space-separated string. Switches are listed in ARCHITECTURE.md (section 3.1).
# -Headless runs without a window (logic only, no screenshots). -Tag names the stdout capture file.
# -EngineArgs passes engine options (before the "--"), e.g. -EngineArgs "--rendering-driver d3d12".
param([string[]]$GameArgs = @(), [int]$Timeout = 120, [switch]$NoBuild, [switch]$Headless, [int]$MaxLines = 40, [string]$Tag = 'run', [string]$EngineArgs = '')
if (-not (Test-Path Y:\)) { cmd /c subst Y: "$(Split-Path $PSScriptRoot -Parent)" | Out-Null }
if ($GameArgs.Count -eq 1 -and $GameArgs[0].Contains(' ')) { $GameArgs = $GameArgs[0].Trim() -split '\s+' }
$G = "$env:LOCALAPPDATA\Programs\Godot_4.7.2_mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
function Trim-Line($s) { if ($s.Length -gt 260) { $s.Substring(0, 260) + ' …' } else { $s } }
$mx = New-Object System.Threading.Mutex($false, 'Local\AshenAbyssRun')
$held = $false
try {
    try { $held = $mx.WaitOne([TimeSpan]::FromMinutes(25)) } catch [System.Threading.AbandonedMutexException] { $held = $true }
    if (-not $held) { "run.ps1: another test run held the lock for 25 minutes; giving up"; exit 3 }
    if (-not $NoBuild) {
        Push-Location Y:\game
        $b = dotnet build AshenAbyss.csproj -c Debug -nologo -v q 2>&1 | ForEach-Object { "$_" }
        Pop-Location
        $errs = $b | Where-Object { $_ -match ': error ' } | ForEach-Object { ($_ -replace '\s*\[Y:\\game\\AshenAbyss\.csproj\]\s*$', '') -replace '^Y:\\game\\', '' } | Sort-Object -Unique
        if ($errs.Count -gt 0) { "BUILD FAILED: $($errs.Count) errors"; $errs | Select-Object -First 30; exit 1 }
        "build ok"
    }
    $logDir = Join-Path $env:APPDATA 'AshenAbyss\logs'
    $before = Get-Date
    $out = "Y:\tools\${Tag}_out.txt"; $err = "Y:\tools\${Tag}_err.txt"
    $a = @('--path', 'Y:\game')
    # Test runs with --win must open as a window from the very first frame. The project default is fullscreen, and
    # switching after start-up makes the screen flash to fullscreen first. These engine flags override the project
    # setting at launch, before any game code runs.
    if ($GameArgs -contains '--win') { $a += '--windowed', '--resolution', '1600x900', '--position', '160,90' }
    if ($Headless) { $a += '--headless' }
    if ($EngineArgs.Trim().Length -gt 0) { $a += ($EngineArgs.Trim() -split '\s+') }
    if ($GameArgs.Count -gt 0) { $a += '--'; $a += $GameArgs }
    $p = Start-Process -FilePath $G -ArgumentList $a -RedirectStandardOutput $out -RedirectStandardError $err -PassThru -WindowStyle Hidden
    $sw = [Diagnostics.Stopwatch]::StartNew()
    if (-not $p.WaitForExit($Timeout * 1000)) { "TIMEOUT after ${Timeout}s - killing"; try { $p.Kill() } catch { } ; Start-Sleep -Milliseconds 500 }
    "exit=$($p.ExitCode) seconds=$([int]$sw.Elapsed.TotalSeconds)"
    $log = Get-ChildItem $logDir -Filter 'game_*.log' -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $before.AddSeconds(-2) } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($log) {
        $lines = Get-Content $log.FullName -Encoding UTF8
        $e = @($lines | Where-Object { $_ -match '^\s*[\d.]+ ERR ' }); $w = @($lines | Where-Object { $_ -match '^\s*[\d.]+ WRN ' })
        "log: $($log.FullName)  lines=$($lines.Count)  ERR=$($e.Count)  WRN=$($w.Count)"
        if ($e.Count) { "--- distinct errors (count x message) ---"; $e | ForEach-Object { ($_ -replace '^\s*[\d.]+ ERR ', '') } | Group-Object | Sort-Object Count -Descending | Select-Object -First $MaxLines | ForEach-Object { Trim-Line ("{0,4} x {1}" -f $_.Count, $_.Name) } }
        if ($w.Count) { "--- distinct warnings ---"; $w | ForEach-Object { ($_ -replace '^\s*[\d.]+ WRN ', '') } | Group-Object | Sort-Object Count -Descending | Select-Object -First ([Math]::Min(20, $MaxLines)) | ForEach-Object { Trim-Line ("{0,4} x {1}" -f $_.Count, $_.Name) } }
        "--- last lines ---"; $lines | Select-Object -Last 8 | ForEach-Object { Trim-Line $_ }
    } else { "NO GAME LOG WRITTEN (the game did not reach Log.Init)" }
    $eng = @()
    foreach ($f in $out, $err) { if (Test-Path $f) { $eng += Get-Content $f -Encoding UTF8 | Where-Object { $_ -match 'ERROR|SCRIPT ERROR|Exception|Unhandled|at AshenAbyss|shader|Shader' -and $_ -notmatch '^\s*[\d.]+ (INF|WRN|ERR|DBG) ' } } }
    if ($eng.Count) { "--- engine output: distinct problem lines ($($eng.Count) total) ---"; $eng | Group-Object | Sort-Object Count -Descending | Select-Object -First $MaxLines | ForEach-Object { Trim-Line ("{0,4} x {1}" -f $_.Count, $_.Name.Trim()) } }
}
finally { if ($held) { $mx.ReleaseMutex() }; $mx.Dispose() }
