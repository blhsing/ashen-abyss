# Crops the centre of each screenshot and tiles them with labels.  -Crop "x,y,w,h" in source pixels.
param([string]$Out, [string[]]$Files, [int]$Cols = 3, [string]$Crop = "520,60,560,700", [int]$TileW = 360)
Add-Type -AssemblyName System.Drawing
if ($Files.Count -eq 1 -and $Files[0].Contains(",")) { $Files = $Files[0].Split(",") }
$c = $Crop.Split(",") | ForEach-Object { [int]$_ }
$tileH = [int]($TileW * $c[3] / $c[2]); $lab = 26
$rows = [Math]::Ceiling($Files.Count / $Cols)
$bmp = New-Object System.Drawing.Bitmap ($Cols * $TileW), ($rows * ($tileH + $lab))
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::FromArgb(14,13,16))
$g.InterpolationMode = 'HighQualityBicubic'
$font = New-Object System.Drawing.Font('Consolas', 13, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
for ($i = 0; $i -lt $Files.Count; $i++) {
  $f = $Files[$i]; if (-not (Test-Path $f)) { continue }
  $img = [System.Drawing.Image]::FromFile($f)
  $x = ($i % $Cols) * $TileW; $y = [Math]::Floor($i / $Cols) * ($tileH + $lab)
  $g.DrawImage($img, (New-Object System.Drawing.Rectangle $x, ($y + $lab), $TileW, $tileH), (New-Object System.Drawing.Rectangle $c[0], $c[1], $c[2], $c[3]), 'Pixel')
  $g.DrawString([IO.Path]::GetFileNameWithoutExtension($f), $font, [System.Drawing.Brushes]::Gainsboro, $x + 4, $y + 5)
  $img.Dispose()
}
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Jpeg); $g.Dispose(); $bmp.Dispose()
