# The cursor test runs, one after another (each queues on the machine-wide run lock). Never starts while the user's
# game runs. Results: Y:\tools\cursor\tests\run_<name>.log (the game's own log, or its console output for the runs on
# a fresh profile, whose log folder is temporary).
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\cursor\tests\run_all.ps1 [-Only level,chase,menu,fallsize,falloff,uidomenu,uidolevel] [-Shots] [-Import] [-NoBuild]
# Slot: 94 = the cursor / UI stream's own test slot (Y:\tools\AGENT_RULES.md). If it already holds a save, stop and
# tell the lead; this script never overwrites one it did not write.
param([string]$Only = 'level,chase,menu,fallsize,falloff,uidomenu,uidolevel', [switch]$Import, [switch]$NoBuild, [switch]$Shots, [int]$Slot = 94)
$T = 'Y:\tools\cursor\tests'
$want = $Only.Split(',')
$started = Get-Date
$sv = Join-Path $env:APPDATA 'AshenAbyss\saves'; $bk = Join-Path $env:APPDATA 'AshenAbyss\saves_test_backup'
if ((Get-ChildItem $sv -Filter "slot$Slot.*" -ErrorAction SilentlyContinue)) { "slot $Slot already holds a save (saves\slot$Slot.*): not mine to overwrite - stop and tell the lead"; exit 4 }
$script:built = [bool]$NoBuild
function UserPlays { [bool](tasklist | Select-String -SimpleMatch 'AshenAbyss') }
function WaitUser {
    # the user's game has the right of way: poll every 30 s, give up after 20 minutes
    $n = 0
    while (UserPlays) { if ($n -eq 0) { "the user's game is running: waiting (poll every 30 s)" }; $n++; if ($n -gt 40) { return $false }; Start-Sleep -Seconds 30 }
    return $true
}
function NewestLog($since) {
    # other streams' test runs write into the same folder: mine is the one started with my slot
    Get-ChildItem (Join-Path $env:APPDATA 'AshenAbyss\logs') -Filter 'game_*.log' -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $since } | Sort-Object LastWriteTime -Descending |
        Where-Object { Select-String -Path $_.FullName -SimpleMatch "--slot=$Slot" -Quiet } | Select-Object -First 1
}
# one run on the normal profile: the game's log is copied to run_<name>.log
function Run($name, $gameArgs, $timeout) {
    "== $name"
    if (-not (WaitUser)) { "USER GAME STILL RUNNING - $name skipped"; return }
    $t0 = Get-Date
    $o = Launch $gameArgs $timeout 'cursor'
    if (-not $o) { "$name DID NOT RUN (no turn on the run lock after 3 tries): no log"; return }
    $l = NewestLog $t0
    if ($l) { Copy-Item $l.FullName "$T\run_$name.log" -Force; "log -> $T\run_$name.log" } else { "NO LOG of this run found" }
}
# run.ps1 gives up when it has waited 25 minutes for the run lock (the queue is not first-come-first-served): try
# again, three times at most. Returns $true when the game really ran. Prints the tail of run.ps1's output.
function Launch($gameArgs, $timeout, $tag) {
    for ($try = 1; $try -le 3; $try++) {
        $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', 'Y:\tools\run.ps1', '-GameArgs', $gameArgs, '-Timeout', "$timeout", '-Tag', $tag)
        if ($script:built) { $a += '-NoBuild' }
        $o = @(& powershell @a)
        $o | Select-Object -Last 14 | Out-Host
        if ($o -match 'BUILD FAILED') { return $false }
        if (-not ($o -match 'giving up')) { $script:built = $true; return [bool]($o -match '^exit=') }
        "(try $try of 3: no turn on the run lock)" | Out-Host
        if (-not (WaitUser)) { return $false }
    }
    return $false
}
# one run on a temporary, empty profile (no saves, default settings): the console output is the record
function RunFresh($name, $gameArgs, $timeout) {
    "== $name (fresh profile)"
    if (-not (WaitUser)) { "USER GAME STILL RUNNING - $name skipped"; return }
    $tmp = Join-Path $env:TEMP ('aa_fresh_' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Force -Path $tmp | Out-Null
    $old = $env:APPDATA; $env:APPDATA = $tmp
    # the console capture of an EARLIER run must never pass for this one (2026-11-20: a run that got no turn on the lock
    # left the previous run's capture in place, and it was copied under the new name)
    Remove-Item Y:\tools\cursorfresh_out.txt -Force -ErrorAction SilentlyContinue
    $ran = $false
    try {
        $ran = Launch $gameArgs $timeout 'cursorfresh'
        $cfg = Join-Path $tmp 'AshenAbyss\settings.json'
        if ($ran -and (Test-Path $cfg)) { "settings.json of the fresh profile after the run: " + ((Get-Content $cfg -Raw) -replace '\s+', ' ') }
    }
    finally { $env:APPDATA = $old; Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
    if (-not $ran) { "$name DID NOT RUN (no turn on the run lock after 3 tries): no log"; return }
    if (Test-Path Y:\tools\cursorfresh_out.txt) { Copy-Item Y:\tools\cursorfresh_out.txt "$T\run_$name.log" -Force; "console -> $T\run_$name.log" } else { "NO CONSOLE CAPTURE of this run found" }
}
if ($Import) { "== import"; & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\import.ps1 }
$new = "--win --mute --new=emberblade --slot=$Slot --level=12 --gear"
# -Shots: screenshots + "SHOT" log lines at the checked moments (ingame.py pastes the cursor in afterwards)
$sh = ''
if ($Shots) { $sh = ' --cursor-shots=Y:/shots/cursor/raw' }
# the runs, in the order -Only names them
foreach ($w in $want) {
    switch ($w.Trim()) {
        'level' { Run 'level' "$new --area=wastes --god --cursor-test --cursor-display=1$sh" 200 }
        'chase' { Run 'chase' "$new --area=wastes --god --cursor-test=chase$sh" 150 }
        'menu' { RunFresh 'menu_fresh' "--win --mute --cursor-test$sh" 100 }
        'fallsize' { RunFresh 'fallback_size_fresh' "--win --mute --cursor-test --cursor-display=1.25 --cursor-drop=attack_40,talk_40 --cursor-expect=48" 80 }
        'falloff' { RunFresh 'fallback_off_fresh' "--win --mute --cursor-test --cursor-drop=attack --cursor-expect=off" 80 }
        'uidomenu' { RunFresh 'uido_menu_fresh' "--win --mute --cursor-log --uidir=Y:\shots\cursor --uido=@Y:\tools\cursor\tests\uido_menu.txt" 100 }
        'uidolevel' { Run 'uido_level' "$new --area=town --cursor-log --uidir=Y:\shots\cursor --uido=@Y:\tools\cursor\tests\uido_level.txt" 130 }
        default { "unknown run '$w'" }
    }
}
# this run's own test saves out of the way: only files of my slot written after the script started, under a
# timestamped name (never over a file that is already in the backup folder)
if (Test-Path $sv) {
    New-Item -ItemType Directory -Force $bk | Out-Null
    $stamp = $started.ToString('yyyyMMdd_HHmmss')
    Get-ChildItem $sv -Filter "slot$Slot.*" -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $started } | ForEach-Object {
        $to = Join-Path $bk ("slot{0}_cursor_{1}{2}" -f $Slot, $stamp, $_.Extension)
        Move-Item $_.FullName $to; "moved $($_.Name) -> saves_test_backup\$(Split-Path $to -Leaf)"
    }
}
"== summary"
Get-ChildItem $T -Filter 'run_*.log' | Where-Object { $_.LastWriteTime -ge $started } | ForEach-Object {
    $d = Select-String -Path $_.FullName -Pattern '\[cursortest\] done ' | Select-Object -Last 1
    $f = @(Select-String -Path $_.FullName -Pattern '\[cursortest\] FAIL ').Count
    $e = @(Select-String -Path $_.FullName -Pattern '^\s*[\d.]+ ERR ').Count
    "{0,-34} {1}  FAIL lines={2}  ERR lines={3}" -f $_.Name, $(if ($d) { ($d.Line -replace '^.*\[cursortest\] ', '') } else { '(no cursortest summary: a --uido run)' }), $f, $e
}
"== done"
