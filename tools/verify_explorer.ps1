# Launches the INSTALLED game through Explorer (so it runs like a Start-menu launch, outside this app's private file
# view) with test switches, waits for it to finish and reports the evidence visible from here. Pure ASCII.
$dir = Join-Path $env:USERPROFILE 'Games\AshenAbyss'
$exe = Join-Path $dir 'AshenAbyss.exe'
$lnk = Join-Path $dir '_verify.lnk'
$pics = Join-Path ([Environment]::GetFolderPath('MyPictures')) 'AshenAbyss'
$logDir = Join-Path $env:APPDATA 'AshenAbyss\logs'
$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = $dir
$sc.Arguments = '-- --mute --new=emberblade --slot=95 --area=wastes --level=5 --gear --bot --blur-at=8 --focus-at=16 --quit-at=24'
$sc.Save()
$before = Get-Date
Start-Process explorer.exe -ArgumentList "`"$lnk`""
Start-Sleep -Seconds 6
$deadline = (Get-Date).AddSeconds(120)
$seen = $false
while ((Get-Date) -lt $deadline) {
    $g = @(Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue)
    if ($g.Count -gt 0) { $seen = $true } elseif ($seen) { break }
    Start-Sleep -Seconds 2
}
"game process seen: $seen; waited $([int]((Get-Date) - $before).TotalSeconds) s"
if ($seen) {
    $p = Get-CimInstance Win32_Process -Filter "Name='AshenAbyss.exe'" -ErrorAction SilentlyContinue
    if ($p) { "STILL RUNNING pid $($p.ProcessId)" }
}
Remove-Item -LiteralPath $lnk -Force -ErrorAction SilentlyContinue
"--- screenshots newer than the launch in $pics"
Get-ChildItem $pics -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $before } | ForEach-Object { "{0}  {1:N0} bytes  {2:HH:mm:ss}" -f $_.Name, $_.Length, $_.LastWriteTime }
"--- game logs newer than the launch visible from here ($logDir)"
$logs = @(Get-ChildItem $logDir -Filter 'game_*.log' -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $before })
if ($logs.Count -eq 0) { "none visible from here (the launch wrote to the real user-data folder, which this app cannot list)" }
foreach ($l in $logs) {
    $lines = Get-Content $l.FullName -Encoding UTF8
    "log $($l.Name) lines=$($lines.Count) ERR=$(@($lines | Where-Object { $_ -match '^\s*[\d.]+ ERR ' }).Count) WRN=$(@($lines | Where-Object { $_ -match '^\s*[\d.]+ WRN ' }).Count)"
    $lines | Where-Object { $_ -match 'window|PrtSc|keyboard hook|\[shot\]|final summary|ERR |WRN ' } | Select-Object -First 14 | ForEach-Object { if ($_.Length -gt 230) { $_.Substring(0, 230) } else { $_ } }
}
