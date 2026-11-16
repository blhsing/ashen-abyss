param([int]$Visit = 2)
$saves = Join-Path $env:APPDATA 'AshenAbyss\saves'
$c = Get-Content (Join-Path $saves 'slot1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$c.AreaVisits | Add-Member -NotePropertyName wastes_d2 -NotePropertyValue ($Visit - 1) -Force
$c | ConvertTo-Json -Depth 64 | Out-File (Join-Path $saves 'slot97.json') -Encoding utf8
& powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--mute --load=97 --area=wastes_d2 --quit-at=4" -Timeout 120 -Headless -Tag repro | Select-String 'BUILD|error CS' | ForEach-Object { $_.Line }
$L = Get-ChildItem (Join-Path $env:APPDATA 'AshenAbyss\logs') -Filter 'game_*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $L.FullName -Encoding UTF8 | Where-Object { $_ -match 'build area|objective at|walk-through|cut off|door sealed' } | ForEach-Object { if ($_.Length -gt 200) { $_.Substring(0, 200) } else { $_ } }
foreach ($f in 'slot97.json', 'slot97.bak') { $p = Join-Path $saves $f; if (Test-Path $p) { Move-Item $p (Join-Path $env:APPDATA 'AshenAbyss\saves_test_backup') -Force } }
