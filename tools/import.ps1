# Re-import changed assets into Godot's cache (needed after any .glb / texture / audio file is added or replaced:
# test runs do NOT re-import by themselves and would keep showing the old version).
# Takes the same machine-wide lock as run.ps1, so it never runs while a test game is loading assets.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\import.ps1
if (-not (Test-Path Y:\)) { cmd /c subst Y: "$(Split-Path $PSScriptRoot -Parent)" | Out-Null }
$G = "$env:LOCALAPPDATA\Programs\Godot_4.7.2_mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$mx = New-Object System.Threading.Mutex($false, 'Local\AshenAbyssRun')
$held = $false
try {
    try { $held = $mx.WaitOne([TimeSpan]::FromMinutes(30)) } catch [System.Threading.AbandonedMutexException] { $held = $true }
    if (-not $held) { "import.ps1: the run lock was busy for 30 minutes; giving up"; exit 3 }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $o = & $G --headless --path Y:\game --import 2>&1 | ForEach-Object { "$_" }
    "import exit=$LASTEXITCODE seconds=$([int]$sw.Elapsed.TotalSeconds)"
    # The importer has crashed once (access violation) when a model file was still being written by an artist while
    # it was read. A crash leaves the cache consistent for everything already imported, so one retry finishes the job.
    if ($LASTEXITCODE -ne 0) {
        Start-Sleep -Seconds 15
        $o = & $G --headless --path Y:\game --import 2>&1 | ForEach-Object { "$_" }
        "import retry exit=$LASTEXITCODE seconds=$([int]$sw.Elapsed.TotalSeconds)"
    }
    $o | Where-Object { $_ -match 'ERROR|Failed|failed' } | Group-Object | Sort-Object Count -Descending | Select-Object -First 15 | ForEach-Object { $s = "{0,3} x {1}" -f $_.Count, $_.Name.Trim(); if ($s.Length -gt 230) { $s.Substring(0, 230) } else { $s } }
}
finally { if ($held) { $mx.ReleaseMutex() }; $mx.Dispose() }
