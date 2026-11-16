# Renders UI pieces through the throttled Blender launcher (ONE run at a time; it queues behind other artists).
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\uiart\render.ps1 -Style A -Pieces explore [-Samples 64] [-Scale 0.5] [-SkipExisting]
param([string]$Style = 'A', [string]$Pieces = 'explore', [int]$Samples = 0, [double]$Scale = 1.0, [switch]$SkipExisting)
$a = @('-b', '--factory-startup', '--python', 'Y:\tools\uiart\render.py', '--', '--style', $Style, '--pieces', $Pieces, '--scale', "$Scale")
if ($Samples -gt 0) { $a += '--samples', "$Samples" }
if ($SkipExisting) { $a += '--skip-existing' }
& Y:\tools\blender\run_blender.ps1 -A $a 2>&1 | Where-Object { $_ -match 'UIART|Error|Traceback|File "|Exception|error:|line \d+' -and $_ -notmatch 'Fra:' } | Select-Object -First 120
"exit=$LASTEXITCODE"
