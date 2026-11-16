# Contact sheet of full 16:9 screenshots (no cropping), labelled with the file name.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\gfx_sheet.ps1 -Dir Y:\shots\gfx\base -Pattern "*_2.png" -Out Y:\shots\gfx\base\sheet_2.jpg [-Cols 3] [-TileW 640] [-Names a,b,c]
param([string]$Dir, [string]$Pattern = '*.png', [string]$Out, [int]$Cols = 3, [int]$TileW = 640, [string[]]$Names = @())
Add-Type -AssemblyName System.Drawing
if ($Names.Count -eq 1 -and $Names[0].Contains(',')) { $Names = $Names[0].Split(',') }
$files = @(Get-ChildItem $Dir -Filter $Pattern | Sort-Object Name)
if ($Names.Count -gt 0) { $files = @($Names | ForEach-Object { $n = $_; $files | Where-Object { $_.BaseName -eq $n -or $_.BaseName -like "$n`_*" } | Select-Object -First 1 } | Where-Object { $_ }) }
if ($files.Count -eq 0) { "no files"; exit 1 }
$tileH = [int]($TileW * 9 / 16); $labelH = [int]($TileW * 0.06)
$rows = [Math]::Ceiling($files.Count / $Cols)
$bmp = New-Object System.Drawing.Bitmap ($Cols * $TileW), ($rows * ($tileH + $labelH))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = 'HighQualityBicubic'; $g.TextRenderingHint = 'AntiAlias'
$g.Clear([System.Drawing.Color]::FromArgb(40, 40, 44))
$font = New-Object System.Drawing.Font('Segoe UI', ([int]($labelH * 0.7)), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 230, 160))
for ($i = 0; $i -lt $files.Count; $i++) {
    $x = ($i % $Cols) * $TileW; $y = [Math]::Floor($i / $Cols) * ($tileH + $labelH)
    try {
        $img = [System.Drawing.Image]::FromFile($files[$i].FullName)
        $g.DrawImage($img, (New-Object System.Drawing.Rectangle ($x + 2), ($y + $labelH), ($TileW - 4), ($tileH - 2)))
        $img.Dispose()
    } catch { }
    $g.DrawString($files[$i].BaseName, $font, $brush, ($x + 6), ($y + 1))
}
$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
$ep = New-Object System.Drawing.Imaging.EncoderParameters 1
$ep.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 90L
$bmp.Save($Out, $codec, $ep)
$g.Dispose(); $bmp.Dispose()
"sheet: $Out ($($files.Count) images)"
