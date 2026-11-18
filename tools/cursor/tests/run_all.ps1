# The cursor test runs, one after another (each queues on the machine-wide run lock). Never starts while the user's
# game runs. Results: Y:\tools\cursor\tests\run_*.log / .txt
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\cursor\tests\run_all.ps1 [-Only level,menu,uidomenu,uidolevel] [-NoImport]
param([string]$Only = 'level,menu,uidomenu,uidolevel', [switch]$NoImport, [int]$Slot = 91)
$T = 'Y:\tools\cursor\tests'
$want = $Only.Split(',')
$started = Get-Date
$sv = Join-Path $env:APPDATA 'AshenAbyss\saves'; $bk = Join-Path $env:APPDATA 'AshenAbyss\saves_test_backup'
# Slots 90-99 are shared by everyone's tests and have no registry: a save that is already there belongs to someone
# else's run (on 2026-11-19 another test was writing slot97 while this script waited in the queue). Never overwrite it.
if ((Get-ChildItem $sv -Filter "slot$Slot.*" -ErrorAction SilentlyContinue)) { "slot $Slot is in use by another test (saves\slot$Slot.* exists): pass -Slot <free 90-99>"; exit 4 }
function UserPlays { [bool](tasklist | Select-String -SimpleMatch 'AshenAbyss') }
function NewestLog($since) {
    Get-ChildItem (Join-Path $env:APPDATA 'AshenAbyss\logs') -Filter 'game_*.log' -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $since } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
if (-not $NoImport) { "== import"; & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\import.ps1 }
if ($want -contains 'level') {
    "== level (wastes -> town), simulated pointer"
    if (UserPlays) { "USER GAME RUNNING - skipped" } else {
        $t0 = Get-Date
        & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\run.ps1 -GameArgs "--win --mute --new=emberblade --slot=$Slot --area=wastes --level=12 --gear --god --cursor-test --cursor-display=1" -Timeout 170 -Tag cursor | Select-Object -Last 12
        $l = NewestLog $t0; if ($l) { Copy-Item $l.FullName "$T\run_level.log" -Force; "log -> $T\run_level.log" }
    }
}
if ($want -contains 'menu') {
    "== title screen, fresh profile, simulated pointer"
    if (UserPlays) { "USER GAME RUNNING - skipped" } else {
        & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\repo\fresh_profile_run.ps1 -GameArgs "--win --mute --cursor-test --cursor-display=1" -Timeout 90 | Select-Object -Last 8
        Copy-Item Y:\tools\fresh_out.txt "$T\run_menu_fresh.txt" -Force
    }
}
if ($want -contains 'uidomenu') {
    "== title screen + settings, fresh profile, --uido script"
    if (UserPlays) { "USER GAME RUNNING - skipped" } else {
        & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\repo\fresh_profile_run.ps1 -GameArgs "--win --mute --cursor-log --cursor-display=1 --uidir=Y:\shots\cursor --uido=@Y:\tools\cursor\tests\uido_menu.txt" -Timeout 90 | Select-Object -Last 8
        Copy-Item Y:\tools\fresh_out.txt "$T\run_uido_menu_fresh.txt" -Force
    }
}
if ($want -contains 'uidolevel') {
    "== interface in a level, normal harness, --uido script"
    if (UserPlays) { "USER GAME RUNNING - skipped" } else {
        $t0 = Get-Date
        & powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\run.ps1 -GameArgs "--win --mute --new=emberblade --slot=$Slot --area=town --level=12 --gear --cursor-log --uidir=Y:\shots\cursor --uido=@Y:\tools\cursor\tests\uido_level.txt" -Timeout 120 -Tag cursor -NoBuild | Select-Object -Last 8
        $l = NewestLog $t0; if ($l) { Copy-Item $l.FullName "$T\run_uido_level.log" -Force; "log -> $T\run_uido_level.log" }
    }
}
# this run's own test saves out of the way: only files of this slot written after the script started, and never over
# a file that is already in the backup folder (timestamped name)
if (Test-Path $sv) {
    New-Item -ItemType Directory -Force $bk | Out-Null
    $stamp = $started.ToString('yyyyMMdd_HHmmss')
    Get-ChildItem $sv -Filter "slot$Slot.*" -ErrorAction SilentlyContinue | Where-Object { $_.LastWriteTime -ge $started } | ForEach-Object {
        $to = Join-Path $bk ("slot{0}_cursor_{1}{2}" -f $Slot, $stamp, $_.Extension)
        Move-Item $_.FullName $to; "moved $($_.Name) -> saves_test_backup\$(Split-Path $to -Leaf)"
    }
}
"== done"
