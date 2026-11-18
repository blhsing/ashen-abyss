# Review sheets (asm_review.py) through the throttled launcher:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\monsters\asm_review.ps1 -Ids mon_a,mon_b [-Res 420] [-Extra "--atk=Melee_Hook:0.5"]
# Output: Y:\shots\chars\asm\review_<id>.png
param([string]$Ids = "", [int]$Res = 420, [string]$Extra = "")
$run = "Y:\tools\blender\run_blender.ps1"
$here = "Y:\tools\blender\monsters"
$t0 = Get-Date
$a = @("-b", "--factory-startup", "--python", "$here\asm_review.py", "--") + @($Ids.Split(",") | Where-Object { $_ }) + @("--res=$Res")
if ($Extra) { $a += $Extra.Split(" ") }
$o = & $run -A $a 2>&1 | ForEach-Object { "$_" }
$o | Select-String -Pattern "^\[mon\] (REVIEW|MISSING)|Traceback|^\w*Error|^\s+File .*asm" | ForEach-Object { $_.Line }
"review elapsed {0:n0}s" -f ((Get-Date) - $t0).TotalSeconds
