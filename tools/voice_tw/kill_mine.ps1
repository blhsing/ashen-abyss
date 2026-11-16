# Stop only OUR OWN stuck processes: the command line must match -Pattern AND mention voice_tw.
param([string]$Pattern = 'uv\.exe')
Get-CimInstance Win32_Process | Where-Object {
    $_.ProcessId -ne $PID -and $_.CommandLine -match $Pattern -and $_.CommandLine -match 'voice_tw' -and
    $_.Name -notmatch '^(bash|sh)\.exe$'
} | ForEach-Object { "stopping $($_.ProcessId) $($_.Name)"; Stop-Process -Id $_.ProcessId -Force }
