param([string[]]$Files)
Add-Type -AssemblyName System.Drawing
if ($Files.Count -eq 1 -and $Files[0].Contains(",")) { $Files = $Files[0].Split(",") }
function Mean($b, $x0, $y0, $x1, $y1) { $s = 0.0; $n = 0; for ($y = $y0; $y -lt $y1; $y += 3) { for ($x = $x0; $x -lt $x1; $x += 3) { $c = $b.GetPixel($x, $y); $s += 0.2126*$c.R + 0.7152*$c.G + 0.0722*$c.B; $n++ } }; return $s / $n }
foreach ($f in $Files) {
  $b = New-Object System.Drawing.Bitmap $f
  $hero = Mean $b 690 300 770 560; $bg = ((Mean $b 490 170 580 450) + (Mean $b 860 170 950 450)) / 2
  "{0,-12} hero {1,5:0.0}  background {2,5:0.0}  ratio {3,4:0.0}" -f [IO.Path]::GetFileNameWithoutExtension($f), $hero, $bg, ($hero / [Math]::Max($bg, 1))
  $b.Dispose()
}
