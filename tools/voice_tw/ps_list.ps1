# List processes whose command line matches a pattern:  ps_list.ps1 <regex>
param([string]$Pattern = 'uv\.exe|python')
Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match $Pattern -and $_.ProcessId -ne $PID } |
    ForEach-Object { "{0,6} {1,6} {2} | {3}" -f $_.ProcessId, $_.ParentProcessId, $_.CreationDate.ToString('HH:mm:ss'), ($_.CommandLine.Substring(0, [Math]::Min(260, $_.CommandLine.Length))) }
