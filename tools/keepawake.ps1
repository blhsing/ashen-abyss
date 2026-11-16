# Keeps the PC and display awake while long unattended build jobs run (Modern Standby starting in the middle of
# heavy jobs hung this laptop). Holds the request until the flag file is deleted or the time limit passes.
#   start:  Start-Process powershell -WindowStyle Hidden -ArgumentList '-NoProfile','-File','Y:\tools\keepawake.ps1','-Hours','6'
#   stop:   Remove-Item Y:\tools\keepawake.flag
param([double]$Hours = 6)
$flag = Join-Path $PSScriptRoot 'keepawake.flag'
Add-Type -Namespace AshenAbyss -Name KeepAwake -MemberDefinition '[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint esFlags);'
Set-Content -Path $flag -Value ("started {0} pid {1} hours {2}" -f (Get-Date), $PID, $Hours)
$end = (Get-Date).AddHours($Hours)
try {
    while ((Test-Path $flag) -and (Get-Date) -lt $end) {
        [void][AshenAbyss.KeepAwake]::SetThreadExecutionState([uint32]2147483651)   # ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED
        Start-Sleep -Seconds 20
    }
}
finally {
    [void][AshenAbyss.KeepAwake]::SetThreadExecutionState([uint32]2147483648)       # ES_CONTINUOUS (clear)
    Remove-Item $flag -ErrorAction SilentlyContinue
}
