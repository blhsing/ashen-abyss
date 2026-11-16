# Caster batch helper: build one character (optionally --fast) and render preview shots, one Blender run at a time,
# always through the throttled wrapper.
#   & Y:\tools\blender\heroes\kc_run.ps1 -Id hero_astromancer -Fast -Shots @()            # build fast + default shots
#   & Y:\tools\blender\heroes\kc_run.ps1 -Id hero_astromancer -NoBuild -Shots 'a:Idle_Loop:0.5:3:front','b:...'
param([string]$Id, [switch]$Fast, [switch]$NoBuild, [switch]$NoRender, [string[]]$Shots = @(), [switch]$Verify)
$H = 'Y:\tools\blender\heroes'
$pat = '\[hero\]|Error|Traceback|File "|WARNING: tri|assert|Exception'
if (-not $NoBuild) {
    $a = @('-b', '--factory-startup', '--python', "$H\$Id.py", '--')
    if ($Fast) { $a += '--fast' } else { $a += '--full' }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & Y:\tools\blender\run_blender.ps1 -A $a 2>&1
    $code = $LASTEXITCODE
    "BUILD exit=$code time=$([int]$sw.Elapsed.TotalSeconds)s"
    $out | Select-String -Pattern $pat | Where-Object { $_.Line -notmatch 'Missing image file' } | ForEach-Object { $_.Line } | Select-Object -Last 45
    if ($code -ne 0 -or -not ($out | Select-String -SimpleMatch 'EXPORTED')) { "BUILD FAILED"; $out | Select-Object -Last 30; exit 1 }
}
if (-not $NoRender) {
    $a = @('-b', '--factory-startup', '--python', "$H\render_char.py", '--', $Id) + $Shots
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & Y:\tools\blender\run_blender.ps1 -A $a 2>&1
    "RENDER exit=$LASTEXITCODE time=$([int]$sw.Elapsed.TotalSeconds)s"
    $out | Select-String -Pattern 'Error|Traceback|File "|montage' | Where-Object { $_.Line -notmatch 'Missing image file' } | ForEach-Object { $_.Line } | Select-Object -Last 12
}
if ($Verify) {
    $out = & Y:\tools\blender\run_blender.ps1 -A @('-b', '--factory-startup', '--python', "$H\verify.py", '--', $Id) 2>&1
    "VERIFY exit=$LASTEXITCODE"
    $out | Select-String -Pattern 'VERIFY|PROBLEM|ALL OK|SOME FAILED|Traceback|File "' | ForEach-Object { $_.Line }
}
