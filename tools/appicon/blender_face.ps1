# Renders the Osaros face through the throttled launcher, always in background mode.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\appicon\blender_face.ps1 <out.png> [mode] [size]
param([Parameter(Mandatory = $true)][string]$Out, [string]$Mode = 'icon', [string]$Size = '1024')
$a = @('-b', '--factory-startup', '--python', 'Y:\tools\appicon\render_boss_face.py', '--', $Out, $Mode, $Size)
if ($a[0] -ne '-b') { throw 'background mode required' }
& 'Y:\tools\blender\run_blender.ps1' -A $a
exit $LASTEXITCODE
