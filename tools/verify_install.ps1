# Launches the INSTALLED game once with test switches (private save slot 95, muted, bot, PrtSc at 12 s, quits at 25 s)
# and reports what its log says about the window, the screenshot and the save. Read-only apart from that test slot.
$exe = Join-Path $env:LOCALAPPDATA 'Programs\AshenAbyss\AshenAbyss.exe'
$logDir = Join-Path $env:APPDATA 'AshenAbyss\logs'
$pics = Join-Path ([Environment]::GetFolderPath('MyPictures')) 'AshenAbyss'
$before = Get-Date
$p = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -ArgumentList '--', '--mute', '--new=emberblade', '--slot=95', '--area=wastes', '--level=5', '--gear', '--bot', '--prtsc-at=16', '--f12-at=20', '--quit-at=27' -PassThru
if (-not $p.WaitForExit(120000)) { "TIMEOUT - killing"; try { $p.Kill() } catch { } }
"exit=$($p.ExitCode) seconds=$([int]((Get-Date) - $before).TotalSeconds)"
$log = Get-ChildItem $logDir -Filter 'game_*.log' | Where-Object { $_.LastWriteTime -ge $before.AddSeconds(-2) } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $log) { "NO GAME LOG WRITTEN"; exit 1 }
$lines = Get-Content $log.FullName -Encoding UTF8
"log: $($log.Name) lines=$($lines.Count) ERR=$(@($lines | Where-Object { $_ -match '^\s*[\d.]+ ERR ' }).Count) WRN=$(@($lines | Where-Object { $_ -match '^\s*[\d.]+ WRN ' }).Count)"
$lines | Where-Object { $_ -match 'window|fullscreen|screen=|resolution|\[shot\]|screenshot|PrtSc|\[save\]|\[icons\]|\[audio\]|\[vo\]|renderer|adapter|GPU|ERR |WRN |final summary|fps=' } | Select-Object -First 45 | ForEach-Object { if ($_.Length -gt 250) { $_.Substring(0, 250) } else { $_ } }
"--- newest screenshots in $pics"
Get-ChildItem $pics -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 3 | ForEach-Object { "{0}  {1:N0} bytes  {2:HH:mm:ss}" -f $_.Name, $_.Length, $_.LastWriteTime }
