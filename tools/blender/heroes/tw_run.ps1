# Town / lampknight / forms batch helper: strictly ONE Blender run at a time, each through the throttled wrapper.
#   tw_run.ps1 -Id npc_elder -Fast -Shots 'a:Idle_Loop:0.5:0:front,b:...'   # fast (unbaked) build + chosen preview shots
#   tw_run.ps1 -Id npc_elder -Full                                           # full bake build only
#   tw_run.ps1 -Id npc_elder -Sheet -Cmp -Verify                             # standard sheet, raking-light close-ups, contract check
#   tw_run.ps1 -Id npc_elder -Shots '...'                                    # extra shots of the exported file (no build)
param([string]$Id, [switch]$Fast, [switch]$Full, [string]$Shots = '', [string]$CmpShots = '', [switch]$Sheet, [switch]$Cmp, [switch]$Verify)
$H = 'Y:\tools\blender\heroes'
function Run-B([string[]]$a, [string]$label, [string]$filter, [int]$tail = 30) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & Y:\tools\blender\run_blender.ps1 -A $a 2>&1
    $code = $LASTEXITCODE
    "$label exit=$code time=$([int]$sw.Elapsed.TotalSeconds)s"
    $out | Select-String -Pattern $filter | Where-Object { $_.Line -notmatch 'Missing image file' } | ForEach-Object { $_.Line } | Select-Object -Last $tail
    $script:lastOut = $out
    $script:lastCode = $code
}
if ($Fast -or $Full) {
    $mode = if ($Fast) { '--fast' } else { '--full' }
    Run-B @('-b', '--factory-startup', '--python', "$H\$Id.py", '--', $mode) 'BUILD' 'TRIS total|EXPORTED|UNWEIGHTED|WARNING: tri|over budget|Error|Traceback|File "|assert|Exception' 30
    if ($script:lastCode -ne 0 -or -not ($script:lastOut | Select-String -SimpleMatch 'EXPORTED')) { "BUILD FAILED"; $script:lastOut | Select-Object -Last 25; exit 1 }
}
if ($Shots -ne '') {
    $s = @($Shots -split ',' | Where-Object { $_ -ne '' })
    Run-B (@('-b', '--factory-startup', '--python', "$H\render_char.py", '--', $Id) + $s) 'SHOTS' 'Error|Traceback|File "|montage' 8
}
if ($Sheet) {
    Run-B @('-b', '--factory-startup', '--python', "$H\render_char.py", '--', $Id) 'SHEET' 'Error|Traceback|File "|montage' 8
}
if ($Cmp) {
    $s = @($CmpShots -split ',' | Where-Object { $_ -ne '' })
    Run-B (@('-b', '--factory-startup', '--python', "$H\twn_cmp.py", '--', $Id) + $s) 'CMP' 'rendered|Error|Traceback|File "' 10
}
if ($Verify) {
    Run-B @('-b', '--factory-startup', '--python', "$H\verify.py", '--', $Id) 'VERIFY' 'VERIFY|PROBLEM|ALL OK|SOME FAILED|Traceback|File "' 12
}
