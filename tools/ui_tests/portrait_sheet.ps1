# Crops the same rectangle out of several screenshots and lays the crops side by side (enlarged), to compare portrait shots.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\ui_tests\portrait_sheet.ps1 -Files a.png,b.png -Out sheet.png -X 970 -Y 140 -W 580 -H 300 -Scale 2 -Cols 2
param([string[]]$Files, [string]$Out, [int]$X = 970, [int]$Y = 140, [int]$W = 580, [int]$H = 300, [double]$Scale = 2, [int]$Cols = 2, [switch]$Nearest)
if ($Files.Count -eq 1 -and $Files[0] -match ',') { $Files = $Files[0].Split(',') }
Add-Type -AssemblyName System.Drawing
$cw = [int]($W * $Scale); $ch = [int]($H * $Scale)
$rows = [int][Math]::Ceiling($Files.Count / $Cols)
$dst = New-Object System.Drawing.Bitmap ($cw * [Math]::Min($Cols, $Files.Count)), ($ch * $rows)
$g = [System.Drawing.Graphics]::FromImage($dst)
$g.InterpolationMode = if ($Nearest) { [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor } else { [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic }
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$i = 0
foreach ($f in $Files) {
    if (-not (Test-Path $f)) { "missing $f"; $i++; continue }
    $src = [System.Drawing.Image]::FromFile($f)
    $col = $i % $Cols; $row = [int][Math]::Floor($i / $Cols)
    $g.DrawImage($src, (New-Object System.Drawing.Rectangle ($col * $cw), ($row * $ch), $cw, $ch), (New-Object System.Drawing.Rectangle $X, $Y, $W, $H), [System.Drawing.GraphicsUnit]::Pixel)
    $src.Dispose(); $i++
}
$g.Dispose()
$dst.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $dst.Dispose()
"saved $Out"
