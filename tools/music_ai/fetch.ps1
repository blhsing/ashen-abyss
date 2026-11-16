# Starts fetch.py detached for a job file (resumable; safe to re-run).  fetch.ps1 -Jobs <job file> -LogName <name> [-Workers 4]
param([Parameter(Mandatory)][string]$Jobs, [string]$LogName = 'fetch', [int]$Workers = 4)
. "$PSScriptRoot\env.ps1"
$py = "$MA_ROOT\python\cpython-3.12.16-windows-x86_64-none\python.exe"
$p = Start-Process -FilePath $py -ArgumentList @('-I', "$PSScriptRoot\fetch.py", $Jobs, "$MA_ROOT\logs\$LogName.log", "$Workers") -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput "$MA_ROOT\logs\$LogName.out" -RedirectStandardError "$MA_ROOT\logs\$LogName.err"
"fetch pid $($p.Id)"
