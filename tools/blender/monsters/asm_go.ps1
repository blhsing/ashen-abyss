# Build + verify (asm_batch.ps1 -NoShots) and then the review sheets (asm_review.ps1) of the given ids:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\monsters\asm_go.ps1 -Ids mon_a,mon_b [-Res 420]
# Review sheets: Y:\shots\chars\asm\review_<id>.png ; build log: asm_out\batch_<first id>.log
param([string]$Ids = "", [int]$Res = 420)
$here = "Y:\tools\blender\monsters"
& powershell -NoProfile -ExecutionPolicy Bypass -File "$here\asm_batch.ps1" -Ids $Ids -NoShots
& powershell -NoProfile -ExecutionPolicy Bypass -File "$here\asm_review.ps1" -Ids $Ids -Res $Res
