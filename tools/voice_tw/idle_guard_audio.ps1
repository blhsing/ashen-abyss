# AUDIO agent (2026-11-22): while AshenAbyss.exe runs (user's game or a test), put voice_tw (gen.py) and music_ai
# Python processes at Idle priority (AGENT_RULES.md "While the user plays"). Ends after 4 min with neither job running.
# Stop: Stop-Process -Id <pid of this powershell> (pid is in Y:\tools\voice_tw\audio_agent_notes.txt)
$gone = 0
while ($gone -lt 16) {
    $ps = Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'python.exe' -and $_.CommandLine -match '(voice_tw.gen\.py|music_ai.(produce|gen_ace|clap_score|speech_check)\.py)' }
    if ($ps) { $gone = 0 } else { $gone++ }
    if (Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue) {
        foreach ($p in $ps) { try { $q = Get-Process -Id $p.ProcessId -ErrorAction Stop; if ($q.PriorityClass -ne 'Idle') { $q.PriorityClass = 'Idle'; "$(Get-Date -Format s) idle: $($p.ProcessId)" } } catch {} }
    }
    Start-Sleep -Seconds 15
}
