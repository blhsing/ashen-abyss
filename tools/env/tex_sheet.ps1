# Contact sheet of all processed texture sets: each cell = albedo tiled 2x2 (to eyeball seams) + name.
# usage: tex_sheet.ps1 [-Map albedo|normal|arm|height] [-Names a,b,c]
param([string]$Map = 'albedo', [string]$NameList = '', [int]$Cell = 240, [int]$Cols = 8, [int]$PerSheet = 32, [string]$Out = 'Y:\shots\env\tex_sheet')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = 'Y:\game\assets\env\tex'
$m = Get-Content "$root\manifest.json" -Raw | ConvertFrom-Json
$names = $m.sets | ForEach-Object { $_.name }
if ($NameList) { $names = $NameList -split ',' }
$font = New-Object System.Drawing.Font('Consolas', 9)
$lab = 16
$sheet = 0
for ($s = 0; $s -lt $names.Count; $s += $PerSheet) {
  $chunk = $names[$s..([math]::Min($s + $PerSheet, $names.Count) - 1)]
  $rows = [math]::Ceiling($chunk.Count / $Cols)
  $bmp = New-Object System.Drawing.Bitmap ($Cols * $Cell), ($rows * ($Cell + $lab))
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.Color]::FromArgb(30, 30, 30))
  $g.InterpolationMode = 'HighQualityBicubic'
  $g.PixelOffsetMode = 'Half'
  for ($i = 0; $i -lt $chunk.Count; $i++) {
    $x = ($i % $Cols) * $Cell; $y = [math]::Floor($i / $Cols) * ($Cell + $lab)
    $p = "$root\$($chunk[$i])\$Map.jpg"
    if (Test-Path $p) {
      $img = [System.Drawing.Image]::FromFile($p)
      $h = [int](($Cell - 4) / 2)
      $small = New-Object System.Drawing.Bitmap $h, $h
      $g2 = [System.Drawing.Graphics]::FromImage($small); $g2.InterpolationMode = 'HighQualityBicubic'; $g2.PixelOffsetMode = 'Half'
      $ia = New-Object System.Drawing.Imaging.ImageAttributes; $ia.SetWrapMode('Tile')
      $g2.DrawImage($img, (New-Object System.Drawing.Rectangle 0, 0, $h, $h), 0, 0, $img.Width, $img.Height, 'Pixel', $ia); $g2.Dispose(); $img.Dispose()
      foreach ($dx in 0, 1) { foreach ($dy in 0, 1) { $g.DrawImageUnscaled($small, $x + 2 + $dx * $h, $y + 2 + $dy * $h) } }
      $small.Dispose()
    }
    $g.DrawString($chunk[$i], $font, [System.Drawing.Brushes]::White, $x + 2, $y + $Cell)
  }
  $g.Dispose()
  $o = "{0}_{1}_{2}.jpg" -f $Out, $Map, $sheet
  $enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
  $ep = New-Object System.Drawing.Imaging.EncoderParameters 1
  $ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality, [long]88)
  $bmp.Save($o, $enc, $ep); $bmp.Dispose()
  Write-Host "wrote $o"
  $sheet++
}
