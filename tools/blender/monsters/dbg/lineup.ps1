# Daylight line-up of humanoid monsters in the real game (windowed test run through Y:\tools\run.ps1, test slot 95):
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\monsters\dbg\lineup.ps1 -Area frost -Ids rime_breaker,cf_hulk -Name A [-Aggro] [-NoBuild]
# The types stand in a row 7 m ahead of the hero (Populate.HarnessSpawns), the hero does not move (no bot, --god).
# Pictures: Y:\shots\run\lineup_<Name>_1..3.png.  Waits (polling every 30 s) while the user's installed game runs.
# Afterwards the test save of slot 95 is moved to saves_test_backup.
param([string]$Area = "frost", [string]$Ids = "", [string]$Name = "A", [switch]$Aggro, [switch]$NoBuild, [string]$Shots = "4,8,12", [int]$Quit = 14)
while (@(tasklist | Select-String -Pattern 'AshenAbyss').Count -gt 0) { "the user's game is running - waiting 30 s"; Start-Sleep -Seconds 30 }
$saves = Join-Path $env:APPDATA 'AshenAbyss\saves'
$a = "--win --mute --bestiary --new=emberblade --slot=95 --area=$Area --level=30 --gear --god --spawn=$Ids --shot=Y:\shots\run\lineup_$Name.png --shot-at=$Shots --quit-at=$Quit"
if ($Aggro) { $a += " --spawn-aggro" }
if ($NoBuild) { & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\run.ps1 -GameArgs $a -Timeout 150 -Tag rigA -NoBuild }
else { & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\run.ps1 -GameArgs $a -Timeout 150 -Tag rigA }
$bk = Join-Path $env:APPDATA 'AshenAbyss\saves_test_backup'
New-Item -ItemType Directory -Force -Path $bk | Out-Null
Get-ChildItem $saves -Filter 'slot95.*' -ErrorAction SilentlyContinue | Move-Item -Destination $bk -Force
"lineup $Name done"
