# Compose the labelled 9-tile animation strip of beasts from the tiles bshots.py --strip rendered:
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\bstrip.ps1 mon_dusk_hound[,mon_x...] [-Dir <shots dir>]
param([string[]]$Models, [string]$Dir = "Y:\tools\blender\beasts\out\shots")
if ($Models.Count -eq 1 -and $Models[0].Contains(",")) { $Models = $Models[0].Split(",") }
$lab = @("行走 1/3", "行走 2/3", "行走 3/3", "奔跑 1/3", "奔跑 2/3", "奔跑 3/3", "攻擊一 命中", "攻擊二 命中", "死亡")
foreach ($m in $Models) {
    if (-not $m.StartsWith("mon_")) { $m = "mon_$m" }
    $items = @(); for ($i = 0; $i -lt 9; $i++) { $items += "${m}_s$i.png|$($lab[$i])" }
    & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\sheet.ps1 -Out "Y:\shots\chars\beast_${m}_anim.jpg" -Cols 3 -Tile 400 -Dir $Dir -Items ($items -join ",")
}
