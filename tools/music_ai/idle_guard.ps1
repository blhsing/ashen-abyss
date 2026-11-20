# While the music batch runs: whenever AshenAbyss.exe (the user's game or a test run) is running, put the music_ai
# Python processes at Idle priority (AGENT_RULES.md, "While the user plays"). Ends by itself when no music_ai
# process has been seen for 2 minutes.   Stop: Stop-Process -Id <pid of this powershell>
$gone = 0
while ($gone -lt 8) {
    $ps = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'python.exe' -and $_.CommandLine -match 'music_ai.(produce|gen_ace|clap_score|speech_check).py' }
    if ($ps) { $gone = 0 } else { $gone++ }
    if (Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue) {
        foreach ($p in $ps) {
            try { $q = Get-Process -Id $p.ProcessId -ErrorAction Stop; if ($q.PriorityClass -ne 'Idle') { $q.PriorityClass = 'Idle'; "$(Get-Date -Format s) idle: $($p.ProcessId)" } } catch {}
        }
    }
    Start-Sleep -Seconds 15
}
