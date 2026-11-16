# Rebuild every hero / NPC with the realism kit: strictly ONE Blender run at a time (each through run_blender.ps1).
#   lk_all.ps1                       # all nine: build -> close-ups (_cmp\*_after.png) -> sheet -> verify
#   lk_all.ps1 -Ids hero_hunter,npc_villager_m -NoSheet
# Progress: Y:\tools\blender\heroes\_all.log  (one line per step) and _build.log (bake details).
param([string[]]$Ids = @('hero_emberblade', 'hero_astromancer', 'hero_forgebody', 'hero_wildsoul', 'npc_villager_f',
                         'hero_hunter', 'hero_soulbinder', 'hero_pactbinder', 'npc_villager_m'),
      [switch]$NoBuild, [switch]$NoSheet, [switch]$NoCmp, [switch]$Before)
$H = 'Y:\tools\blender\heroes'
$Ids = @($Ids | ForEach-Object { $_ -split ',' } | Where-Object { $_ -ne '' })
$log = "$H\_all.log"
function Step([string]$label, [string[]]$a, [string]$filter) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $out = & Y:\tools\blender\run_blender.ps1 -A $a 2>&1
    $code = $LASTEXITCODE
    $lines = @($out | Select-String -Pattern $filter | ForEach-Object { $_.Line })
    $msg = "{0} {1} exit={2} {3}s" -f (Get-Date -Format 'HH:mm:ss'), $label, $code, [int]$sw.Elapsed.TotalSeconds
    Add-Content -Path $log -Value $msg -Encoding utf8
    foreach ($l in ($lines | Select-Object -Last 6)) { Add-Content -Path $log -Value ("      " + $l) -Encoding utf8 }
    $msg
    $lines | Select-Object -Last 6
}
Add-Content -Path $log -Value ("==== {0} start: {1}" -f (Get-Date -Format 'HH:mm:ss'), ($Ids -join ' ')) -Encoding utf8
foreach ($id in $Ids) {
    if ($Before) {
        Step "$id cmp-before" @('-b', '--factory-startup', '--python', "$H\render_cmp.py", '--', "$H\_before\$id.glb", $id, 'before') 'Traceback|Error|File "'
        continue
    }
    if (-not $NoBuild) {
        Step "$id build" @('-b', '--factory-startup', '--python', "$H\$id.py", '--', '--full') 'EXPORTED|WARNING: tri|over budget|Traceback|Error:|File "'
    }
    if (-not $NoCmp) {
        Step "$id cmp-after" @('-b', '--factory-startup', '--python', "$H\render_cmp.py", '--', "Y:\game\assets\chars\$id.glb", $id, 'after') 'Traceback|Error|File "'
    }
    if (-not $NoSheet) {
        Step "$id sheet" @('-b', '--factory-startup', '--python', "$H\render_char.py", '--', $id) 'montage|Traceback|Error|File "'
    }
    Step "$id verify" @('-b', '--factory-startup', '--python', "$H\verify.py", '--', $id) 'VERIFY|PROBLEM|Traceback|File "'
}
Add-Content -Path $log -Value ("==== {0} done" -f (Get-Date -Format 'HH:mm:ss')) -Encoding utf8
"ALL DONE"
