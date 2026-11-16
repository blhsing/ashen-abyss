# Download Poly Haven thumbnails for a list of asset ids and build labelled contact sheets.
# usage: thumb_sheet.ps1 -Ids a,b,c -Out Y:\shots\env\cand_tex -Cols 6 -Cell 200 -PerSheet 36
param(
  [string[]]$Ids,
  [string]$IdFile,
  [string]$Out,
  [int]$Cols = 6,
  [int]$Cell = 200,
  [int]$PerSheet = 36,
  [switch]$Acg
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$ua = 'Mozilla/5.0 (compatible; asset-fetch/1.0)'
$tdir = 'Y:\assets_src\env\polyhaven\thumbs'
New-Item -ItemType Directory -Force $tdir | Out-Null
if ($IdFile) { $Ids = Get-Content $IdFile | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() } }
foreach ($id in $Ids) {
  $p = Join-Path $tdir "$id.jpg"
  if (-not (Test-Path $p)) {
    try {
      $tu = if ($Acg) { "https://acg-media.struffelproductions.com/file/ambientCG-Web/media/thumbnail/256-JPG-242424/$id.jpg" } else { "https://cdn.polyhaven.com/asset_img/thumbs/$id.png?width=256&height=256&format=jpg" }
      Invoke-WebRequest -Uri $tu -UserAgent $ua -OutFile $p -UseBasicParsing
    } catch { Write-Host "thumb fail: $id" }
  }
}
$font = New-Object System.Drawing.Font('Consolas', 9)
$lab = 16
$n = $Ids.Count
$sheet = 0
for ($s = 0; $s -lt $n; $s += $PerSheet) {
  $chunk = $Ids[$s..([math]::Min($s + $PerSheet, $n) - 1)]
  $rows = [math]::Ceiling($chunk.Count / $Cols)
  $bmp = New-Object System.Drawing.Bitmap ($Cols * $Cell), ($rows * ($Cell + $lab))
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear([System.Drawing.Color]::FromArgb(40, 40, 40))
  $g.InterpolationMode = 'HighQualityBicubic'
  for ($i = 0; $i -lt $chunk.Count; $i++) {
    $id = $chunk[$i]
    $x = ($i % $Cols) * $Cell; $y = [math]::Floor($i / $Cols) * ($Cell + $lab)
    $p = Join-Path $tdir "$id.jpg"
    if (Test-Path $p) {
      try { $img = [System.Drawing.Image]::FromFile($p); $g.DrawImage($img, $x + 2, $y + 2, $Cell - 4, $Cell - 4); $img.Dispose() } catch {}
    }
    $g.DrawString($id, $font, [System.Drawing.Brushes]::White, $x + 2, $y + $Cell)
  }
  $g.Dispose()
  $o = "{0}_{1}.jpg" -f $Out, $sheet
  $bmp.Save($o, [System.Drawing.Imaging.ImageFormat]::Jpeg)
  $bmp.Dispose()
  Write-Host "wrote $o ($($chunk.Count))"
  $sheet++
}

