# Rebuilds the player's wastes_d2 layout (visit given by -Visit) from a COPY of slot 1 (slot 97) and lets the bot try
# to destroy every totem; reports objective progress, and where the bot got stuck. Never touches slot 1. Pure ASCII.
param([int]$Visit = 2)
$saves = Join-Path $env:APPDATA 'AshenAbyss\saves'
$c = Get-Content (Join-Path $saves 'slot1.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$c.AreaVisits | Add-Member -NotePropertyName wastes_d2 -NotePropertyValue ($Visit - 1) -Force
$c | ConvertTo-Json -Depth 64 | Out-File (Join-Path $saves 'slot97.json') -Encoding utf8
New-Item -ItemType Directory -Force -Path "Y:\shots\proto\repro$Visit" | Out-Null
& powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--win --mute --load=97 --area=wastes_d2 --bot --focus --god --shot=Y:\shots\proto\repro$Visit\r.png --shot-at=100,200,300 --quit-at=330" -Timeout 480 -Tag repro | Select-String 'ERR=|exit=' | ForEach-Object { $_.Line }
$L = Get-ChildItem (Join-Path $env:APPDATA 'AshenAbyss\logs') -Filter 'game_*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Get-Content $L.FullName -Encoding UTF8 | Where-Object { $_ -match 'build area|walk-through|narrow passage cell|cut off|toppled|door|stuck|final summary' } | Select-Object -First 60 | ForEach-Object { if ($_.Length -gt 210) { $_.Substring(0, 210) } else { $_ } }
foreach ($f in 'slot97.json', 'slot97.bak') { $p = Join-Path $saves $f; if (Test-Path $p) { Move-Item $p (Join-Path $env:APPDATA 'AshenAbyss\saves_test_backup') -Force } }
