# 64 px icon contact sheet: every base, v1 v2 v3 (magic / rare / legendary slot backgrounds), actual size.
param([string]$Dir = 'Y:\shots\gear\icons64', [string]$Out = 'Y:\shots\gear\BEST_icons64_sheet.png')
Add-Type -AssemblyName System.Drawing
$idx = Get-Content "$Dir\index.txt" -Encoding UTF8 | Where-Object { $_ -match '\|' }
$per = [Math]::Ceiling($idx.Count / 2); $labW = 150; $cell = 72; $colW = $labW + 3 * $cell + 20
$bmp = New-Object System.Drawing.Bitmap (2 * $colW), ($per * $cell + 40)
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::FromArgb(14, 12, 11)); $g.TextRenderingHint = 'AntiAlias'
$font = New-Object System.Drawing.Font('Microsoft JhengHei', 18, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$small = New-Object System.Drawing.Font('Consolas', 12, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$bg = @([System.Drawing.Color]::FromArgb(26, 30, 44), [System.Drawing.Color]::FromArgb(44, 38, 20), [System.Drawing.Color]::FromArgb(46, 28, 14))
$rim = @([System.Drawing.Color]::FromArgb(70, 100, 170), [System.Drawing.Color]::FromArgb(170, 145, 60), [System.Drawing.Color]::FromArgb(190, 100, 30))
$g.DrawString('v1 magic / v2 rare / v3 legendary - 64 px', $small, [System.Drawing.Brushes]::Gray, 8, 10)
for ($i = 0; $i -lt $idx.Count; $i++) {
  $p = $idx[$i].Split('|'); $b = $p[0]; $name = $p[1]
  $x0 = [Math]::Floor($i / $per) * $colW; $y = 34 + ($i % $per) * $cell
  $g.DrawString($name, $font, [System.Drawing.Brushes]::Gainsboro, $x0 + 8, $y + 14)
  $g.DrawString($b, $small, [System.Drawing.Brushes]::Gray, $x0 + 8, $y + 40)
  for ($v = 1; $v -le 3; $v++) {
    $x = $x0 + $labW + ($v - 1) * $cell
    $br = New-Object System.Drawing.SolidBrush $bg[$v - 1]; $g.FillRectangle($br, $x, $y + 2, 68, 68)
    $pen = New-Object System.Drawing.Pen $rim[$v - 1], 2; $g.DrawRectangle($pen, $x, $y + 2, 68, 68)
    $f = "$Dir\${b}_v$v.png"
    if (Test-Path $f) { $img = [System.Drawing.Image]::FromFile($f); $g.DrawImage($img, $x + 2, $y + 4, 64, 64); $img.Dispose() }
  }
}
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose(); "sheet: $Out"
