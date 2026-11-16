# Look-dev helper (realism pass): ONE Blender run at a time, always through the throttled wrapper.
#   lk_run.ps1 -Script chartex_prep.py -ScriptArgs '--sheet'         # run any script of this folder
#   lk_run.ps1 -Id hero_emberblade                                    # full build (bake) of one character
#   lk_run.ps1 -Id hero_emberblade -Preview                           # quick look-dev build: 1K atlases, fewer samples (NOT for delivery)
#   lk_run.ps1 -Id hero_emberblade -NoBuild -Cmp after                # close-ups under raking light -> shots\chars\_cmp\<id>_<shot>_after.png
#   lk_run.ps1 -Id hero_emberblade -NoBuild -Cmp before -Glb Y:\tools\blender\heroes\_before\hero_emberblade.glb
#   lk_run.ps1 -Id hero_emberblade -NoBuild -Sheet -Verify            # standard sheet (render_char.py) + contract check
param([string]$Id, [string]$Script, [string[]]$ScriptArgs = @(), [switch]$NoBuild, [string]$Cmp, [string]$Glb,
      [string[]]$Shots = @(), [switch]$Sheet, [switch]$Verify, [switch]$Preview, [int]$Tail = 60)
if ($Preview) { $env:ASHEN_PREVIEW = '1' } else { $env:ASHEN_PREVIEW = '' }
$H = 'Y:\tools\blender\heroes'
$ScriptArgs = @($ScriptArgs | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' })
$Shots = @($Shots | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' })
$pat = '\[hero\]|Error|Traceback|File "|WARNING: tri|assert|Exception'
function Run-B([string[]]$a, [string]$label, [string]$filter) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & Y:\tools\blender\run_blender.ps1 -A $a 2>&1
    $code = $LASTEXITCODE
    "$label exit=$code time=$([int]$sw.Elapsed.TotalSeconds)s"
    $out | Select-String -Pattern $filter | Where-Object { $_.Line -notmatch 'Missing image file' } | ForEach-Object { $_.Line } | Select-Object -Last $Tail
    return
}
if ($Script) {
    Run-B (@('-b', '--factory-startup', '--python', "$H\$Script", '--') + $ScriptArgs) 'SCRIPT' $pat
    exit 0
}
if (-not $NoBuild) {
    $a = @('-b', '--factory-startup', '--python', "$H\$Id.py", '--', '--full')
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & Y:\tools\blender\run_blender.ps1 -A $a 2>&1
    $code = $LASTEXITCODE
    "BUILD exit=$code time=$([int]$sw.Elapsed.TotalSeconds)s"
    $out | Select-String -Pattern $pat | Where-Object { $_.Line -notmatch 'Missing image file' } | ForEach-Object { $_.Line } | Select-Object -Last $Tail
    if ($code -ne 0 -or -not ($out | Select-String -SimpleMatch 'EXPORTED')) { "BUILD FAILED"; $out | Select-Object -Last 40; exit 1 }
}
if ($Cmp) {
    if (-not $Glb) { $Glb = "Y:\game\assets\chars\$Id.glb" }
    Run-B (@('-b', '--factory-startup', '--python', "$H\render_cmp.py", '--', $Glb, $Id, $Cmp) + $Shots) 'CMP' 'rendered|Error|Traceback|File "'
}
if ($Sheet) {
    Run-B (@('-b', '--factory-startup', '--python', "$H\render_char.py", '--', $Id) + $Shots) 'RENDER' 'Error|Traceback|File "|montage'
}
if ($Verify) {
    Run-B @('-b', '--factory-startup', '--python', "$H\verify.py", '--', $Id) 'VERIFY' 'VERIFY|PROBLEM|ALL OK|SOME FAILED|Traceback|File "'
}
