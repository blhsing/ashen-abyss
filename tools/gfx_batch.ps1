# Graphics review batch: runs the game once per theme (through run.ps1, so runs stay serialised machine-wide),
# takes two screenshots each and collects the [perf] lines.
#   powershell -NoProfile -ExecutionPolicy Bypass -File Y:\tools\gfx_batch.ps1 -Out Y:\shots\gfx\base [-Only town,wastes] [-Extra "--gfx-q=3"] [-Class astromancer]
param([string]$Out = 'Y:\shots\gfx\base', [string[]]$Only = @(), [string]$Extra = '', [string]$Class = 'emberblade',
      [string]$ShotAt = '6,16', [int]$QuitAt = 18, [switch]$NoBuild, [switch]$NoBot)
if ($Only.Count -eq 1 -and $Only[0].Contains(',')) { $Only = $Only[0].Split(',') }
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$areas = @(
    @('town', 'town', 3), @('wastes', 'wastes', 5), @('frost', 'frost', 10), @('barrows', 'crypt', 10), @('mire', 'mire', 10),
    @('sands', 'sands', 28), @('sanctum', 'sanctum', 35), @('abyss', 'abyss', 42),
    @('crypt', 'wastes_d1', 5), @('cave', 'wastes_d2', 5), @('graveyard', 'wastes_boss', 6),
    @('icehall', 'frost_d1', 10), @('icecave', 'frost_d2', 10), @('wetcave', 'mire_d1', 10),
    @('brasshall', 'sands_d1', 28), @('sandtomb', 'sands_d2', 28), @('cathedral', 'sanctum_d1', 35),
    @('forge', 'abyss_d1', 42), @('lavacave', 'abyss_d2', 42), @('rift', 'rift:1', 45)
)
$perf = Join-Path $Out 'perf.txt'
$first = $true
foreach ($a in $areas) {
    $theme = $a[0]; $area = $a[1]; $lvl = $a[2]
    if ($Only.Count -gt 0 -and $Only -notcontains $theme) { continue }
    $bot = if ($NoBot) { '' } else { '--bot' }
    $args = "--win --mute --new=$Class --area=$area --level=$lvl --gear --god $bot --shot=$Out\$theme.png --shot-at=$ShotAt --quit-at=$QuitAt $Extra"
    $nb = $NoBuild -or (-not $first)
    $first = $false
    "=== $theme ($area) ==="
    $res = if ($nb) { & Y:\tools\run.ps1 -GameArgs $args -Timeout ($QuitAt + 75) -Tag gfx -NoBuild } else { & Y:\tools\run.ps1 -GameArgs $args -Timeout ($QuitAt + 75) -Tag gfx }
    $res | Where-Object { $_ -notmatch '^\s*[\d.]+ (INF|DBG) ' } | Select-Object -First 40
    if ($res -match '^BUILD FAILED') { break }
    $logLine = $res | Where-Object { $_ -match '^log: ' } | Select-Object -First 1
    if ($logLine -and $logLine -match '^log: (.+?)\s+lines=') {
        $lf = $Matches[1]
        $lines = Get-Content $lf -Encoding UTF8
        $p = @($lines | Where-Object { $_ -match '\[perf\]' })
        $b = @($lines | Where-Object { $_ -match "\[env\] built '" })
        $fps = @($p | ForEach-Object { if ($_ -match 'fps=(\d+)') { [int]$Matches[1] } })
        $ms = @($p | ForEach-Object { if ($_ -match 'frame=([\d.]+)ms') { [double]$Matches[1] } })
        $dr = @($p | ForEach-Object { if ($_ -match 'draws=(\d+)') { [int]$Matches[1] } })
        $pr = @($p | ForEach-Object { if ($_ -match 'prims=(\d+)') { [int]$Matches[1] } })
        $line = "{0,-10} fps={1} frame_ms={2} draws={3} prims={4}" -f $theme, ($fps -join '/'), ($ms -join '/'), ($dr -join '/'), ($pr -join '/')
        $line
        Add-Content -Path $perf -Value $line -Encoding UTF8
        if ($b.Count) { Add-Content -Path $perf -Value ("           " + ($b[-1] -replace '^\s*[\d.]+ INF ', '')) -Encoding UTF8 }
    }
}
"batch done -> $Out"
