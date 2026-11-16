# Waits (up to 12 hours) until the game is no longer running, then installs the export that is already staged
# (install.ps1 -SkipExport). Never launches the game. Result in Y:\tools\install_when_closed.log. Pure ASCII.
$log = 'Y:\tools\install_when_closed.log'
"$(Get-Date -Format 'HH:mm:ss') waiting for AshenAbyss.exe to close" | Out-File $log -Encoding utf8
$deadline = (Get-Date).AddHours(12)
while ((Get-Date) -lt $deadline -and @(Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue).Count -gt 0) { Start-Sleep -Seconds 5 }
if (@(Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue).Count -gt 0) { "$(Get-Date -Format 'HH:mm:ss') gave up after 12 h" | Out-File $log -Append -Encoding utf8; exit 1 }
Start-Sleep -Seconds 3
"$(Get-Date -Format 'HH:mm:ss') game closed; installing" | Out-File $log -Append -Encoding utf8
& powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\install.ps1' -SkipExport 2>&1 | Out-File $log -Append -Encoding utf8
"$(Get-Date -Format 'HH:mm:ss') done (exit $LASTEXITCODE)" | Out-File $log -Append -Encoding utf8
