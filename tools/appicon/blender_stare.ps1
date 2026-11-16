# Renders the icon's "stare" close-up through the throttled launcher, always in background mode.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\appicon\blender_stare.ps1 <out.png> [tilt] [res] [half] [pitch]
param([Parameter(Mandatory = $true)][string]$Out, [string]$Tilt = '20', [string]$Res = '1536', [string]$Half = '0.27', [string]$Pitch = '2')
$a = @('-b', '--factory-startup', '--python', 'Y:\tools\appicon\render_stare.py', '--', $Out, $Tilt, $Res, $Half, $Pitch)
if ($a[0] -ne '-b') { throw 'background mode required' }
& 'Y:\tools\blender\run_blender.ps1' -A $a 2>&1 | ForEach-Object { "$_" } | Select-String -Pattern '^\[mon\]|Traceback|Error|^\s+File ' | ForEach-Object { $_.Line }
exit $LASTEXITCODE
