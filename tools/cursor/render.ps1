# Renders the mouse cursors through the throttled Blender launcher (background mode; it queues behind other artists).
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\cursor\render.ps1 [-Kinds default,attack] [-Builds hi,lo] [-Samples 96] [-Scale 4] [-Out <folder>]
param([string]$Kinds = 'all', [string]$Builds = 'hi,lo', [int]$Samples = 96, [double]$Scale = 4.0, [string]$Out = 'Y:\assets_src\cursor_gen\render')
$a = @('-b', '--factory-startup', '--python', 'Y:\tools\cursor\cursors.py', '--', '--kinds', $Kinds, '--builds', $Builds, '--samples', "$Samples", '--scale', "$Scale", '--out', $Out)
& Y:\tools\blender\run_blender.ps1 -A $a 2>&1 | Where-Object { $_ -match 'CURSOR|uiart|Error|Traceback|File "|Exception|error:|line \d+' -and $_ -notmatch 'Fra:|CUEW' } | Select-Object -First 160
"exit=$LASTEXITCODE"
