# Lists (and with -Kill ends) the processes of the cursor test chain only: command lines that carry the cursor
# stream's own markers (run_all.ps1 of this folder, --cursor-test / --cursor-log / --cursor-shots, tag cursor/cursorfresh).
param([switch]$Kill)
$marks = @('cursor\tests\run_all.ps1', '--cursor-test', '--cursor-log', '--cursor-shots', '--cursor-drop', '-Tag cursor')
$mine = @(Get-CimInstance Win32_Process | Where-Object {
    $c = $_.CommandLine
    $c -and $_.ProcessId -ne $PID -and ($_.Name -match '^(powershell|Godot)') -and (@($marks | Where-Object { $c.Contains($_) }).Count -gt 0) -and (-not $c.Contains('_mine.ps1'))
})
if ($mine.Count -eq 0) { 'none of the cursor chain is running'; return }
foreach ($p in $mine) {
    $c = $p.CommandLine; $tail = $c.Substring([Math]::Max(0, $c.Length - 110))
    '{0:HH:mm:ss} pid={1} {2} ...{3}' -f $p.CreationDate, $p.ProcessId, $p.Name, $tail
    if ($Kill) { try { Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop; '   ended' } catch { '   could not end: ' + $_.Exception.Message } }
}
