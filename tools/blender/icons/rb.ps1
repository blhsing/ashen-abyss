# Icon render launcher: ONE icon Blender run at a time (named mutex shared by the icon lead and its helpers), and
# every run still goes through Y:\tools\blender\run_blender.ps1 (machine-wide limit of two Blender processes).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\icons\rb.ps1 helm_* chest_3 marker: force
#
# Arguments are passed to render_icons.py (icon ids, prefix*, category:, all:, force, res=N). Prints the ICON lines,
# failures and the summary. Afterwards run post.py render to produce the masters.
$sel = $args
if (-not $sel -or $sel.Count -eq 0) { Write-Output "rb.ps1: nothing selected"; exit 2 }
$mx = New-Object System.Threading.Mutex($false, 'Local\AshenIconsBlender')
$got = $false
try {
    try { $got = $mx.WaitOne([TimeSpan]::FromMinutes(45)) } catch [System.Threading.AbandonedMutexException] { $got = $true }
    if (-not $got) { Write-Output "rb.ps1: another icon render has been running for 45 min; giving up"; exit 3 }
    $a = @('-b', '--factory-startup', '--python', 'Y:\tools\blender\icons\render_icons.py', '--') + $sel
    & Y:\tools\blender\run_blender.ps1 -A $a 2>&1 | Where-Object { $_ -match '^ICON|Traceback|Error|^\s+File ' } | ForEach-Object { "$_" }
}
finally {
    if ($got) { [void]$mx.ReleaseMutex() }
    $mx.Dispose()
}
