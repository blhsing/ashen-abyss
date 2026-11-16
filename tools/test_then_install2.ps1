# Waits (up to 12 h) until the player's game is closed, rebuilds, then
#  1. bot test: totem/prisoner dungeons, 3 areas x 4 seeds (all must open the boss door, 0 errors)
#  2. reproduction: copies of the player's save (slot 97, never slot 1) re-generate wastes_d2 at the player's recent
#     visit numbers, to see whether the fix had to move or drop a totem spot there
#  3. installs the new build only if step 1 passed and the player has not started the game again.
# Never launches the player's game; never writes the player's save. Pure ASCII.
$log = 'Y:\tools\test_then_install2.log'
function Say($t) { "$(Get-Date -Format 'HH:mm:ss') $t" | Out-File $log -Append -Encoding utf8 }
function Playing { @(Get-Process -Name AshenAbyss -ErrorAction SilentlyContinue).Count -gt 0 }
"" | Out-File $log -Encoding utf8
Say 'waiting for the player to close the game'
$deadline = (Get-Date).AddHours(12)
while ((Get-Date) -lt $deadline -and (Playing)) { Start-Sleep -Seconds 10 }
if (Playing) { Say 'gave up after 12 h'; exit 1 }
Say 'game closed; build'
& powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs '--mute --quit-at=2' -Timeout 120 -Headless -Tag idols 2>&1 | Select-String 'build ok|BUILD FAILED|error CS' | ForEach-Object { Say $_.Line }
& 'C:\Program Files\Git\bin\bash.exe' -c '/y/tools/test_idols.sh'
Get-Content 'Y:\tools\test_idols.txt' | ForEach-Object { Say ($_ -replace 'last=.*$', '') }
$rows = @(Get-Content 'Y:\tools\test_idols.txt' | Where-Object { $_ -match 'seed=' })
$bad = @($rows | Where-Object { $_ -notmatch 'door_opened=[1-9]' -or $_ -notmatch 'errors=0' })

# reproduction of the player's layout (copies only)
$saves = Join-Path $env:APPDATA 'AshenAbyss\saves'
$src = Join-Path $saves 'slot1.json'
if ((Test-Path $src) -and -not (Playing)) {
    $j = Get-Content $src -Raw -Encoding UTF8 | ConvertFrom-Json
    $v = 0; if ($j.AreaVisits -and $j.AreaVisits.wastes_d2) { $v = [int]$j.AreaVisits.wastes_d2 }
    Say "player's save: wastes_d2 generated $v time(s)"
    for ($k = [Math]::Max(1, $v - 2); $k -le [Math]::Max(1, $v); $k++) {
        if (Playing) { Say 'player started the game: reproduction stopped'; break }
        $c = Get-Content $src -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $c.AreaVisits) { $c | Add-Member -NotePropertyName AreaVisits -NotePropertyValue ([pscustomobject]@{}) }
        $c.AreaVisits | Add-Member -NotePropertyName wastes_d2 -NotePropertyValue ($k - 1) -Force
        $c | ConvertTo-Json -Depth 64 | Out-File (Join-Path $saves 'slot97.json') -Encoding utf8
        & powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\run.ps1' -GameArgs "--mute --load=97 --area=wastes_d2 --quit-at=6" -Timeout 120 -Headless -NoBuild -Tag repro | Out-Null
        $L = Get-ChildItem (Join-Path $env:APPDATA 'AshenAbyss\logs') -Filter 'game_*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $t = Get-Content $L.FullName -Encoding UTF8
        $moved = @($t | Where-Object { $_ -match 'moved to reachable floor' }).Count
        $skip = @($t | Where-Object { $_ -match 'cannot be reached from the entrance' }).Count
        $build = ($t | Where-Object { $_ -match 'build area=wastes_d2' } | Select-Object -First 1)
        Say "repro visit $k : moved=$moved skipped=$skip errors=$(@($t | Where-Object { $_ -match ' ERR ' }).Count) :: $($build -replace '^.*(seed=\S+).*$', '$1')"
        foreach ($f in 'slot97.json', 'slot97.bak') { $p = Join-Path $saves $f; if (Test-Path $p) { Move-Item $p (Join-Path $env:APPDATA 'AshenAbyss\saves_test_backup') -Force } }
    }
}
foreach ($f in 'slot95.json', 'slot95.bak') { $p = Join-Path $saves $f; if (Test-Path $p) { Move-Item $p (Join-Path $env:APPDATA 'AshenAbyss\saves_test_backup') -Force } }

if ($rows.Count -lt 12 -or $bad.Count -gt 0) { Say "NOT INSTALLED: $($rows.Count) runs, $($bad.Count) failed"; exit 2 }
if (Playing) { Say 'NOT INSTALLED: the player started the game again'; exit 3 }
& powershell -NoProfile -ExecutionPolicy Bypass -File 'Y:\tools\install.ps1' 2>&1 | Select-String 'exported in|installed  |ERROR|done in' | ForEach-Object { Say $_.Line }
Say 'finished'
