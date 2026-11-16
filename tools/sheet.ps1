# Compose a labelled contact sheet from preview renders. Text is sized relative to the tile so it stays readable when
# the image is shown scaled down: keep to 3 columns or fewer.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\sheet.ps1 -Out Y:\shots\preview\heroes_1.jpg -Cols 3 -Tile 400 -Title "英雄" -Items "hero_emberblade_idle.png|燼刃衛,hero_astromancer_idle.png|星術師"
param([string]$Out, [int]$Cols = 3, [int]$Tile = 400, [string[]]$Items, [string]$Dir = 'Y:\shots\chars', [string]$Title = '')
Add-Type -AssemblyName System.Drawing
if ($Items.Count -eq 1 -and $Items[0].Contains(",")) { $Items = $Items[0].Split(",") }
$n = $Items.Count; $rows = [Math]::Ceiling($n / $Cols)
$fontPx = [int]($Tile * 0.115); $titlePx = [int]($Tile * 0.135)
$labelH = [int]($fontPx * 1.7); $titleH = if ($Title) { [int]($titlePx * 1.9) } else { 0 }
$W = $Cols * $Tile; $H = $rows * ($Tile + $labelH) + $titleH
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'AntiAlias'
$g.Clear([System.Drawing.Color]::FromArgb(14, 13, 16))
$font = New-Object System.Drawing.Font('Microsoft JhengHei', $fontPx, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$tfont = New-Object System.Drawing.Font('Microsoft JhengHei', $titlePx, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 232, 200))
$tbrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 190, 90))
$fmt = New-Object System.Drawing.StringFormat; $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'; $fmt.FormatFlags = 'NoWrap'
if ($Title) { $g.DrawString($Title, $tfont, $tbrush, (New-Object System.Drawing.RectangleF 0, 0, $W, $titleH), $fmt) }
for ($i = 0; $i -lt $n; $i++) {
    $parts = $Items[$i].Split('|'); $file = Join-Path $Dir $parts[0]; $label = if ($parts.Count -gt 1) { $parts[1] } else { $parts[0] }
    $x = ($i % $Cols) * $Tile; $y = $titleH + [Math]::Floor($i / $Cols) * ($Tile + $labelH)
    if (Test-Path $file) {
        $img = [System.Drawing.Image]::FromFile($file)
        # centre-crop to a square (a little tighter than the full frame so the character fills the tile), then scale
        $s = [int]([Math]::Min($img.Width, $img.Height) * 0.86)
        $src = New-Object System.Drawing.Rectangle ([int](($img.Width - $s) / 2)), ([int](($img.Height - $s) / 2)), $s, $s
        $dst = New-Object System.Drawing.Rectangle ($x + 4), ($y + 4), ($Tile - 8), ($Tile - 8)
        $g.DrawImage($img, $dst, $src, [System.Drawing.GraphicsUnit]::Pixel)
        $img.Dispose()
    }
    $g.DrawString($label, $font, $brush, (New-Object System.Drawing.RectangleF $x, ($y + $Tile), $Tile, $labelH), $fmt)
}
New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$ep = New-Object System.Drawing.Imaging.EncoderParameters 1
$ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 90L
$bmp.Save($Out, $enc, $ep); $g.Dispose(); $bmp.Dispose()
"wrote $Out ${W}x${H} label=${fontPx}px"
