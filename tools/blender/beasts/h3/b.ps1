# H3 shortcut: build (and render) creatures of spec_h3.py through the throttled launcher (bb.ps1 -> run_blender.ps1).
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\blender\beasts\h3\b.ps1 -ids "dusk_crow dusk_gadfly" [-NoBake] [-NoBuild] [-NoShots] [-shots "--tile --game --dbg --strip"]
param([string]$ids, [switch]$NoBake, [string]$shots = "--tile --game --dbg", [switch]$NoShots, [switch]$NoBuild)
$H = "Y:\tools\blender\beasts\h3"
$bb = "Y:\tools\blender\beasts\bb.ps1"
if (-not $NoBuild) {
    $a = "$ids --path=$H --spec=spec_h3 --out=$H\out"
    if ($NoBake) { $a += " --nobake" }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $bb bbuild.py $a
}
if (-not $NoShots) {
    $a = "$ids $shots --out=$H\out\shots --meta=$H\out"
    if ($NoBake) { $a += " --src=$H\out" }
    & powershell -NoProfile -ExecutionPolicy Bypass -File $bb bshots.py $a
}
