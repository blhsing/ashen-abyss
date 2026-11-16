# Melee batch helper (same pattern as kc_run.ps1): ONE Blender run at a time, always through the throttled wrapper.
#   mel_run.ps1 -Script mel_probe.py -ScriptArgs a,b          # run any script of this folder, print [hero] lines
#   mel_run.ps1 -Id hero_hunter -Fast                          # build (fast preview materials) + default shots
#   mel_run.ps1 -Id hero_hunter -NoBuild -Shots 'a:Idle_Loop:0.5:3:front','b:...'
#   mel_run.ps1 -Id hero_hunter -NoRender -Verify              # full build + verify
param([string]$Id, [string]$Script, [string[]]$ScriptArgs = @(), [switch]$Fast, [switch]$NoBuild, [switch]$NoRender,
      [string[]]$Shots = @(), [switch]$Verify, [int]$Tail = 60)
$H = 'Y:\tools\blender\heroes'
# powershell -File passes "a,b" as one string: split comma lists here
$ScriptArgs = @($ScriptArgs | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' })
$Shots = @($Shots | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' })
$pat = '\[hero\]|Error|Traceback|File "|WARNING: tri|assert|Exception'
if ($Script) {
    $a = @('-b', '--factory-startup', '--python', "$H\$Script", '--') + $ScriptArgs
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & Y:\tools\blender\run_blender.ps1 -A $a 2>&1
    "SCRIPT exit=$LASTEXITCODE time=$([int]$sw.Elapsed.TotalSeconds)s"
    $out | Select-String -Pattern $pat | Where-Object { $_.Line -notmatch 'Missing image file' } | ForEach-Object { $_.Line }
    exit 0
}
if (-not $NoBuild) {
    $a = @('-b', '--factory-startup', '--python', "$H\$Id.py", '--')
    if ($Fast) { $a += '--fast' } else { $a += '--full' }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & Y:\tools\blender\run_blender.ps1 -A $a 2>&1
    $code = $LASTEXITCODE
    "BUILD exit=$code time=$([int]$sw.Elapsed.TotalSeconds)s"
    $out | Select-String -Pattern $pat | Where-Object { $_.Line -notmatch 'Missing image file' } | ForEach-Object { $_.Line } | Select-Object -Last $Tail
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
