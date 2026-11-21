# Same as Y:\tools\import.ps1 (same run lock, same command) but keeps the whole importer output in godot\import_full_log.txt
$G = "$env:LOCALAPPDATA\Programs\Godot_4.7.2_mono\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe"
$mx = New-Object System.Threading.Mutex($false, 'Local\AshenAbyssRun')
$held = $false
try {
    try { $held = $mx.WaitOne([TimeSpan]::FromMinutes(30)) } catch [System.Threading.AbandonedMutexException] { $held = $true }
    if (-not $held) { "import_full.ps1: run lock busy"; exit 3 }
    $o = & $G --headless --path Y:\game --import 2>&1 | ForEach-Object { "$_" }
    "import exit=$LASTEXITCODE"
    $o | Set-Content -Encoding UTF8 (Join-Path $PSScriptRoot 'import_full_log.txt')
}
finally { if ($held) { $mx.ReleaseMutex() }; $mx.Dispose() }
