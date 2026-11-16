# READ-ONLY diagnostic: lists the project's running test games, Blender jobs, generators and launcher shells with
# their parent process. It never stops anything. (An earlier version of this file killed processes when run without
# arguments; that behaviour is gone. Stopping things is the lead's job, with Y:\tools\stop_all.ps1.)
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\who_runs.ps1
$all = Get-CimInstance Win32_Process
$rows = $all | Where-Object {
    $_.Name -match '^(Godot.*|blender|python|pythonw)\.exe$' -or
    ($_.Name -match '^(powershell|pwsh|bash|sh)\.exe$' -and ("$($_.CommandLine)" -match 'run\.ps1|import\.ps1|run_blender\.ps1|mk\.ps1|go\.ps1|_run\.ps1|qarun|produce\.py'))
} | Sort-Object CreationDate
if (-not $rows) { "nothing from the project is running"; return }
foreach ($p in $rows) {
    $c = "$($p.CommandLine)"
    "{0:HH:mm:ss} {1,-14} pid {2,6} parent {3,6} :: {4}" -f $p.CreationDate, ($p.Name -replace '\.exe$', ''), $p.ProcessId, $p.ParentProcessId, $c.Substring(0, [Math]::Min(170, $c.Length))
}
