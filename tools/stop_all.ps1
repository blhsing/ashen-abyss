# LEAD ONLY - DESTRUCTIVE: force-stops EVERY test game, Blender job, generator and launcher shell of this project.
# Agents must never run this. It exists for the lead to pause all work when the user asks.
# music/voice generation, and the keep-awake helper. Safe to run at any time; it never touches its own process tree.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\stop_all.ps1
$all = Get-CimInstance Win32_Process
$byId = @{}; foreach ($p in $all) { $byId[[int]$p.ProcessId] = $p }
# my own ancestors must survive
$keep = New-Object System.Collections.Generic.HashSet[int]
$cur = [int]$PID
while ($cur -and $byId.ContainsKey($cur) -and $keep.Add($cur)) { $cur = [int]$byId[$cur].ParentProcessId }
$marks = @('qa' + 'run', 'Y:\tools\run.ps1', 'Y:\tools\import.ps1', 'run_' + 'blender.ps1', '\mk.ps1', 'lk_' + 'run.ps1', 'lk_' + 'all.ps1',
           'mel_' + 'run.ps1', 'kc_' + 'run.ps1', 'keep' + 'awake.ps1', '/y/tools/', 'Y:\tools\qa', 'Y:\tools\gfx', 'Y:\tools\ui', 'produce.py', 'gen_ace.py', '/tmp/')
function Sweep {
    $n = 0
    foreach ($x in Get-CimInstance Win32_Process) {
        $id = [int]$x.ProcessId
        if ($keep.Contains($id)) { continue }
        $c = "$($x.CommandLine) $($x.ExecutablePath)"
        $hit = $false
        if ($x.Name -match '^(Godot.*|blender)\.exe$') { $hit = $true }
        elseif ($x.Name -match '^(bash|sh|powershell|pwsh|cmd|python|pythonw|dotnet)\.exe$') {
            foreach ($m in $marks) { if ($c.Contains($m)) { $hit = $true; break } }
            if ($x.Name -match '^pythonw?\.exe$' -and $c.Contains('AshenAbyssTools')) { $hit = $true }
        }
        if ($hit) { try { Stop-Process -Id $id -Force -ErrorAction Stop; "stopped $($x.Name) $id"; $n++ } catch { } }
    }
    return
}
Sweep
Start-Sleep -Seconds 8
Sweep
Remove-Item Y:\tools\keepawake.flag -ErrorAction SilentlyContinue
Start-Sleep -Seconds 12
$left = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(blender|python|pythonw|Godot.*)$' })
"still running after 20 s: $($left.Count) " + (($left | ForEach-Object { $_.ProcessName + ':' + $_.Id }) -join ' ')
