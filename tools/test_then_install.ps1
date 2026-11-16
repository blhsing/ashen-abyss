# Waits (up to 12 h) until the player's game is closed, rebuilds, runs the totem-dungeon bot test, and installs the
# new build only if every run opened the boss door with no errors. Never launches the player's game. Pure ASCII.
$log = 'Y:\tools\test_then_install.log'
function Say($t) { "$(Get-Date -Format 'HH:mm:ss') $t" | Out-File $log -Append -Encoding utf8 }
"" | Out-File $log -Encoding utf8
Say 'waiting for the player to close the game'
$deadline = (Get-Date).AddHours(12)
while ((Get-Date) -lt $deadline -and @(Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue).Count -gt 0) { Start-Sleep -Seconds 10 }
if (@(Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue).Count -gt 0) { Say 'gave up after 12 h'; exit 1 }
Say 'game closed; build + test'
& powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs '--mute --quit-at=2' -Timeout 120 -Headless -Tag idols 2>&1 | Select-String 'build|BUILD|error' | ForEach-Object { Say $_.Line }
& 'C:\Program Files\Git\bin\bash.exe' -c '/y/tools/test_idols.sh'
Get-Content 'Y:\tools\test_idols.txt' | ForEach-Object { Say $_ }
$rows = @(Get-Content 'Y:\tools\test_idols.txt' | Where-Object { $_ -match 'seed=' })
$bad = @($rows | Where-Object { $_ -notmatch 'door_opened=[1-9]' -or $_ -notmatch 'errors=0' })
if ($rows.Count -lt 12 -or $bad.Count -gt 0) { Say "NOT INSTALLED: $($rows.Count) runs, $($bad.Count) failed"; exit 2 }
if (@(Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue).Count -gt 0) { Say 'NOT INSTALLED: the player started the game again'; exit 3 }
& powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\install.ps1' 2>&1 | Select-String 'exported in|installed  |ERROR|done in' | ForEach-Object { Say $_.Line }
Say 'finished'
